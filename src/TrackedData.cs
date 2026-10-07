// Stackshot - Imagen en el portapapeles que avisa cuando se pega.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

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

        // pngFromFile: la imagen es la del fichero (un PNG): sus bytes se leen tal cual, sin volver a codificar.
        public TrackedData(string path, Bitmap image, bool fromCapture, bool pngFromFile, Action<TrackedData> onUsed)
        {
            FilePath = path;
            this.image = image;
            this.onUsed = onUsed;
            // El PNG (lento en capturas grandes) se prepara en otro hilo: leído del fichero (en cuanto esté escrito) o
            // codificado desde una copia de verdad (Clone con el mismo formato puede compartir los píxeles, y GDI+ no
            // admite usarlos desde dos hilos). Quien lo pida mientras tanto, espera.
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
                    // Si el fichero no se pudo leer, el PNG queda vacío y las aplicaciones usan el mapa de bits.
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
                        if (released) png.Dispose(); // la soltaron mientras se preparaba
                    }
                }
            });
            SetData(DataFormats.Bitmap, true, image);
            SetData("PNG", false, png);
            if (fromCapture)
            {
                // Ya está en el historial de Win+V: que esta copia no salga repetida.
                SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
                SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
            }
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

        // Copia independiente de los píxeles (memoria con memoria: unos pocos ms aunque la captura sea enorme).
        static Bitmap DeepCopy(Bitmap src)
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

        // Sin esperar al PNG: si aún se está preparando, lo suelta el hilo que lo prepara.
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
