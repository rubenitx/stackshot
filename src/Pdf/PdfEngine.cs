// Stackshot - PDF pages drawn by the engine that comes with Windows 10 and 11 (Windows.Data.Pdf): nothing to download,
// nothing leaves the computer.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage.Streams;
using Rect = System.Windows.Rect;

namespace Stackshot
{
    // An open PDF. Windows' engine is only ever used from one worker thread, which takes requests in priority order;
    // results come back on the thread that asked (through its dispatcher). Page sizes are in DIPs (1/96 inch), as the
    // engine gives them, already turned by the page's own rotation.
    public sealed class PdfEngine : IDisposable
    {
        public const long MemoryLimit = 200L * 1024 * 1024;   // bigger files are read from disk as they are drawn
        public const int MaxSide = 8192;                     // a whole page is never drawn bigger than this (pixels)

        public string Path { get; private set; }
        public int Pages { get; private set; }
        public System.Windows.Size[] Sizes { get; private set; }
        public bool Protected { get; private set; }

        readonly object gate = new object();
        readonly List<Job> queue = new List<Job>();
        readonly Thread thread;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile bool stop;
        PdfDocument doc;
        IRandomAccessStream winStream;

        sealed class Job
        {
            public int Page, Priority;
            public long Order;
            public Action Run;
            public string Key;     // a later job with the same key replaces this one
        }

        long order;

        PdfEngine(string path)
        {
            Path = path;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Name = "Stackshot: PDF";
        }

        // The reason a PDF could not be opened, in plain words; NeedsPassword when it asks for one (wrong or missing).
        public const string NeedsPassword = "password";

        // Opens the file on the engine's thread and waits for it (call it off the UI thread). Null with error on failure.
        public static PdfEngine Open(string path, string password, out string error)
        {
            error = null;
            PdfEngine e = new PdfEngine(path);
            string err = null;
            ManualResetEvent done = new ManualResetEvent(false);
            lock (live) live.Add(e);
            used = true;
            e.thread.Start();
            e.Enqueue(-1, 0, null, delegate
            {
                try { err = e.Load(password); }
                catch (Exception ex) { err = Explain(ex); }
                done.Set();
            });
            done.WaitOne();
            if (err != null) { error = err; e.Dispose(); return null; }
            return e;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        string Load(string password)
        {
            FileInfo fi = new FileInfo(Path);
            if (!fi.Exists) return "No encuentro el archivo: " + Path;
            if (fi.Length == 0) return "El archivo est\u00E1 vac\u00EDo.";
            try
            {
                // Read whole when it fits: a copy changed on disk meanwhile can't confuse the engine halfway through.
                if (fi.Length <= MemoryLimit)
                {
                    winStream = Memory(File.ReadAllBytes(Path));
                    doc = Wait(string.IsNullOrEmpty(password) ? PdfDocument.LoadFromStreamAsync(winStream) : PdfDocument.LoadFromStreamAsync(winStream, password));
                }
                else
                {
                    Windows.Storage.StorageFile f = Wait(Windows.Storage.StorageFile.GetFileFromPathAsync(fi.FullName));
                    try { doc = Wait(string.IsNullOrEmpty(password) ? PdfDocument.LoadFromFileAsync(f) : PdfDocument.LoadFromFileAsync(f, password)); }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(f); }
                }
            }
            catch (Exception ex)
            {
                if (IsPassword(ex)) return NeedsPassword;
                throw;
            }
            Protected = doc.IsPasswordProtected;
            Pages = (int)doc.PageCount;
            if (Pages == 0) return "El PDF no tiene p\u00E1ginas.";
            System.Windows.Size[] s = new System.Windows.Size[Pages];
            for (int i = 0; i < Pages; i++)
            {
                using (PdfPage p = doc.GetPage((uint)i))
                {
                    Windows.Foundation.Size z = p.Size;
                    s[i] = new System.Windows.Size(Math.Max(1, z.Width), Math.Max(1, z.Height));
                }
            }
            Sizes = s;
            return null;
        }

        static bool IsPassword(Exception ex)
        {
            int h = ex.HResult;
            string m = (ex.Message ?? "").ToLowerInvariant();
            return h == unchecked((int)0x80070056) || h == unchecked((int)0x8007052B) || m.Contains("password") || m.Contains("contrase");
        }

        static string Explain(Exception ex)
        {
            ShotStack.Log("PDF: " + ex);
            if (ex is TypeLoadException || ex is FileNotFoundException && ex.Message.Contains("Windows")) return "Este Windows no puede abrir PDF por su cuenta.";
            if (ex is UnauthorizedAccessException) return "No tengo permiso para leer el archivo.";
            if (ex is IOException) return "El archivo est\u00E1 en uso o no se puede leer.";
            if (ex is OutOfMemoryException) return "El PDF es demasiado grande para abrirlo.";
            return "No parece un PDF v\u00E1lido, o est\u00E1 da\u00F1ado.";
        }

        // ---- Requests

        // Draws a page (or the part src of it, in the page's DIPs) at width x height pixels and hands the frozen image to
        // done, on ui's thread. A newer request with the same key replaces an older one still waiting. Lower priority first.
        public void Render(int page, int width, int height, Rect? src, int priority, string key, Dispatcher ui, Action<BitmapSource> done)
        {
            if (page < 0 || page >= Pages || width < 1 || height < 1) return;
            Enqueue(page, priority, key, delegate
            {
                BitmapSource b = null;
                try { b = Draw(page, width, height, src); }
                catch (Exception ex) { ShotStack.Log("PDF, p\u00E1gina " + (page + 1) + ": " + ex.Message); }
                if (ui == null) { done(b); return; }
                try { ui.BeginInvoke(new Action(delegate { done(b); })); }
                catch { }
            });
        }

        // The same, blocking (off the UI thread): a page as a GDI+ bitmap, for the OCR, printing and exports.
        public System.Drawing.Bitmap RenderGdi(int page, int width, int height, int timeoutMs)
        {
            System.Drawing.Bitmap r = null;
            ManualResetEvent done = new ManualResetEvent(false);
            bool queued = Enqueue(page, 50, null, delegate
            {
                try { r = DrawGdi(page, width, height); }
                catch (Exception ex) { ShotStack.Log("PDF, p\u00E1gina " + (page + 1) + ": " + ex.Message); }
                done.Set();
            });
            // A closed engine draws nothing: no waiting for it (nor any request queued after the close).
            if (!queued) return null;
            while (!done.WaitOne(250))
            {
                timeoutMs -= 250;
                if (timeoutMs <= 0 || stop) return null;
            }
            return r;
        }

        // Drops the waiting requests whose key starts with prefix (a zoom that changed, a window that closed).
        public void Cancel(string prefix)
        {
            lock (gate) queue.RemoveAll(delegate(Job j) { return j.Key != null && j.Key.StartsWith(prefix, StringComparison.Ordinal); });
        }

        bool Enqueue(int page, int priority, string key, Action run)
        {
            lock (gate)
            {
                if (stop) return false;
                if (key != null) queue.RemoveAll(delegate(Job j) { return j.Key == key; });
                queue.Add(new Job { Page = page, Priority = priority, Key = key, Run = run, Order = order++ });
            }
            wake.Set();
            return true;
        }

        void Loop()
        {
            while (!stop)
            {
                Job next = null;
                lock (gate)
                {
                    foreach (Job j in queue)
                        if (next == null || j.Priority < next.Priority || (j.Priority == next.Priority && j.Order < next.Order)) next = j;
                    if (next != null) queue.Remove(next);
                }
                if (next == null) { wake.WaitOne(500); continue; }
                try { next.Run(); }
                catch (Exception ex) { ShotStack.Log("PDF: " + ex.Message); }
            }
            // Released here, on the thread that made them: left to the collector, Windows' engine is still alive when
            // the process ends and its unloading spins forever (the process never exits).
            Release();
        }

        void Release()
        {
            try { if (winStream != null) winStream.Dispose(); }
            catch { }
            try { if (doc != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(doc); }
            catch { }
            try { if (winStream != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(winStream); }
            catch { }
            doc = null;
            winStream = null;
            lock (live) live.Remove(this);
            released.Set();
        }

        readonly ManualResetEvent released = new ManualResetEvent(false);
        static readonly List<PdfEngine> live = new List<PdfEngine>();

        // The file's bytes as the stream Windows' engine reads, written in pieces so a big file is not copied whole twice.
        static IRandomAccessStream Memory(byte[] bytes)
        {
            InMemoryRandomAccessStream s = new InMemoryRandomAccessStream();
            using (DataWriter w = new DataWriter(s))
            {
                const int Piece = 4 << 20;
                for (int at = 0; at < bytes.Length; at += Piece)
                {
                    int n = Math.Min(Piece, bytes.Length - at);
                    if (at == 0 && n == bytes.Length) w.WriteBytes(bytes);
                    else { byte[] part = new byte[n]; System.Buffer.BlockCopy(bytes, at, part, 0, n); w.WriteBytes(part); }
                    Wait(w.StoreAsync());
                }
                w.DetachStream();
            }
            s.Seek(0);
            return s;
        }

        // ---- Drawing (engine thread)

        // Windows' engine takes the size asked in DIPs and multiplies it by the scale of a display (125 % gives 750 px for
        // 600): how much it does is learnt from what it returns, and asked for in its terms, so a page comes out at the
        // pixels wanted whatever the screens are. Shared by every engine of the process.
        static double dipScale = 1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        byte[] Bmp(int page, int width, int height, Rect? src)
        {
            byte[] b = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                double k = Volatile.Read(ref dipScale);
                uint dw = (uint)Math.Max(1, Math.Round(width / k)), dh = (uint)Math.Max(1, Math.Round(height / k));
                b = Render(page, dw, dh, src);
                int gotW, gotH;
                if (!BmpSize(b, out gotW, out gotH) || (Math.Abs(gotW - width) <= 1 && Math.Abs(gotH - height) <= 1)) return b;
                // Another scale than thought (the first page, or the screens changed): learnt, and drawn again exactly.
                Volatile.Write(ref dipScale, Math.Max(0.25, Math.Min(8, (gotW / (double)dw + gotH / (double)dh) / 2)));
            }
            return b;
        }

        byte[] Render(int page, uint dw, uint dh, Rect? src)
        {
            using (PdfPage p = doc.GetPage((uint)page))
            using (InMemoryRandomAccessStream s = new InMemoryRandomAccessStream())
            {
                PdfPageRenderOptions o = new PdfPageRenderOptions();
                o.DestinationWidth = dw;
                o.DestinationHeight = dh;
                o.BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId;
                if (src.HasValue) o.SourceRect = new Windows.Foundation.Rect(src.Value.X, src.Value.Y, src.Value.Width, src.Value.Height);
                try { Wait(p.RenderToStreamAsync(s, o)); }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(o); }   // let go now, not at exit
                byte[] b = new byte[s.Size];
                using (IInputStream input = s.GetInputStreamAt(0))
                using (DataReader r = new DataReader(input))
                {
                    Wait(r.LoadAsync((uint)s.Size));
                    r.ReadBytes(b);
                }
                return b;
            }
        }

        // Width and height from a BMP's header (height may be stored negative: top-down rows).
        static bool BmpSize(byte[] b, out int w, out int h)
        {
            w = h = 0;
            if (b == null || b.Length < 26 || b[0] != 'B' || b[1] != 'M') return false;
            w = BitConverter.ToInt32(b, 18);
            h = Math.Abs(BitConverter.ToInt32(b, 22));
            return w > 0 && h > 0;
        }

        BitmapSource Draw(int page, int width, int height, Rect? src)
        {
            byte[] b = Bmp(page, width, height, src);
            using (MemoryStream ms = new MemoryStream(b))
            {
                BmpBitmapDecoder d = new BmpBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource f = d.Frames[0];
                // The engine paints the page on white: shown opaque, with no alpha to blend.
                if (f.Format != PixelFormats.Bgr32) f = new FormatConvertedBitmap(f, PixelFormats.Bgr32, null, 0);
                WriteableBitmap w = new WriteableBitmap(f);
                w.Freeze();
                return w;
            }
        }

        System.Drawing.Bitmap DrawGdi(int page, int width, int height)
        {
            byte[] b = Bmp(page, width, height, null);
            using (MemoryStream ms = new MemoryStream(b))
            using (System.Drawing.Bitmap raw = new System.Drawing.Bitmap(ms))
            {
                System.Drawing.Bitmap o = new System.Drawing.Bitmap(raw.Width, raw.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(o))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.DrawImageUnscaled(raw, 0, 0);
                }
                return o;
            }
        }

        static T Wait<T>(IAsyncOperation<T> op)
        {
            while (op.Status == AsyncStatus.Started) Thread.Sleep(1);
            if (op.Status != AsyncStatus.Completed) { Exception e = op.ErrorCode; throw e ?? new InvalidOperationException("PDF: " + op.Status); }
            return op.GetResults();
        }

        static void Wait(IAsyncAction op)
        {
            while (op.Status == AsyncStatus.Started) Thread.Sleep(1);
            if (op.Status != AsyncStatus.Completed) { Exception e = op.ErrorCode; throw e ?? new InvalidOperationException("PDF: " + op.Status); }
        }

        // Stops the engine and waits (briefly) until Windows' document is let go on its own thread.
        public void Dispose()
        {
            lock (gate) { stop = true; queue.Clear(); }
            wake.Set();
            if (Thread.CurrentThread != thread && thread.IsAlive) released.WaitOne(1500);
        }

        // Stackshot is closing: every engine still around is let go first, or the process can't end (see Release).
        public static void ShutdownAll(int waitMs)
        {
            List<PdfEngine> all;
            lock (live) all = new List<PdfEngine>(live);
            DateTime end = DateTime.UtcNow.AddMilliseconds(waitMs);
            foreach (PdfEngine e in all)
            {
                lock (e.gate) { e.stop = true; e.queue.Clear(); }
                e.wake.Set();
            }
            foreach (PdfEngine e in all)
            {
                int left = (int)Math.Max(0, (end - DateTime.UtcNow).TotalMilliseconds);
                if (e.thread.IsAlive) e.released.WaitOne(left);
            }
        }

        static volatile bool used;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint code);

        // The very last step of a process that opened a PDF, once everything is saved: it ends without unloading the
        // graphics driver, which Windows' PDF engine left in a state where unloading it spins forever (seen with Intel's
        // driver: the process never went away). Ending a process this way is what the system does with it anyway.
        public static void EndProcess(int code)
        {
            if (!used) return;
            try { Console.Out.Flush(); } catch { }
            TerminateProcess(GetCurrentProcess(), (uint)code);
        }

        // The last thing before the process ends, once a PDF was opened: engines and page readers stopped and waited for,
        // and what Windows' engine and OCR left for the collector released now, while the runtime is whole. Otherwise a
        // leftover can make the end of the process spin (seen: the process never went away).
        public static void Settle()
        {
            if (!used) return;
            ShutdownAll(2500);
            PdfReader.StopAll(2500);
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { }
        }
    }
}
