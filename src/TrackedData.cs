// Stackshot - Clipboard image that reports when it is pasted.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Stackshot
{
    public class TrackedData : DataObject
    {
        public readonly string FilePath;
        readonly DateTime since = DateTime.Now;
        readonly Action<TrackedData> onUsed;
        readonly Bitmap image;
        readonly MemoryStream png = new MemoryStream();
        readonly object pngLock = new object();
        bool used, pngReady, released;

        // pngFromFile: the image is the file's PNG, so its bytes are used as-is without re-encoding.
        public TrackedData(string path, Bitmap image, bool fromCapture, bool pngFromFile, Action<TrackedData> onUsed)
        {
            FilePath = path;
            this.image = image;
            this.onUsed = onUsed;
            // The PNG (slow for large captures) is prepared on a worker thread, either read from the file once written
            // or encoded from a deep copy (a same-format Clone may share pixels and GDI+ objects aren't thread-safe).
            // Readers block until it's ready.
            Bitmap copy = pngFromFile ? null : DeepCopy(image);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    bool done = false;
                    if (copy == null)
                    {
                        ShotStack.WaitWritten(path);
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(path);
                            png.Write(bytes, 0, bytes.Length);
                            done = true;
                        }
                        catch (Exception ex) { ShotStack.Log("PNG del portapapeles (fichero): " + ex.Message); }
                    }
                    // If the file couldn't be read, the PNG stays empty and apps fall back to the bitmap.
                    if (!done && copy != null) copy.Save(png, ImageFormat.Png);
                    png.Position = 0;
                }
                catch (Exception ex) { ShotStack.Log("PNG del portapapeles: " + ex.Message); }
                finally
                {
                    if (copy != null) copy.Dispose();
                    lock (pngLock)
                    {
                        pngReady = true;
                        System.Threading.Monitor.PulseAll(pngLock);
                        if (released) png.Dispose(); // released while it was being prepared
                    }
                }
            });
            SetData(DataFormats.Bitmap, true, image);
            SetData("PNG", false, png);
            // Screenshots often contain private data: never sync them to other devices through the cloud clipboard.
            SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
            // Already in the Win+V history: keep this copy out of it.
            if (fromCapture) SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
        }

        public override object GetData(string format, bool autoConvert)
        {
            if (format == "PNG") lock (pngLock) { while (!pngReady) System.Threading.Monitor.Wait(pngLock, 10000); }
            object o = base.GetData(format, autoConvert);
            if (!used && o != null && !format.StartsWith("Can") && (DateTime.Now - since).TotalMilliseconds > 1000)
            {
                used = true;
                if (onUsed != null) onUsed(this);
            }
            return o;
        }

        // Deep pixel copy (memory to memory, a few ms even for huge captures).
        internal static Bitmap DeepCopy(Bitmap src)
        {
            PixelFormat f = src.PixelFormat;
            if (f != PixelFormat.Format32bppArgb && f != PixelFormat.Format32bppPArgb && f != PixelFormat.Format32bppRgb)
            {
                Bitmap b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImageUnscaled(src, 0, 0);
                return b;
            }
            Rectangle r = new Rectangle(0, 0, src.Width, src.Height);
            Bitmap dst = new Bitmap(src.Width, src.Height, f);
            BitmapData s = src.LockBits(r, ImageLockMode.ReadOnly, f);
            BitmapData d = dst.LockBits(r, ImageLockMode.WriteOnly, f);
            try
            {
                for (int y = 0; y < r.Height; y++)
                    Native.CopyMemory(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), new IntPtr(s.Scan0.ToInt64() + (long)y * s.Stride), new UIntPtr((uint)(r.Width * 4)));
            }
            finally
            {
                src.UnlockBits(s);
                dst.UnlockBits(d);
            }
            return dst;
        }

        // Doesn't wait for the PNG: if it's still being prepared, the worker releases it.
        public void Release()
        {
            lock (pngLock)
            {
                released = true;
                if (pngReady) png.Dispose();
            }
            image.Dispose();
        }
    }

}
