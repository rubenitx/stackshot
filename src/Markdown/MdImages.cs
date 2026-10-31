// Stackshot - Images in Markdown documents: where a link points, loading pictures for the preview and the PDF (off the UI
// thread, one at a time, cached), and saving pasted or dropped ones next to the document.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Stackshot
{
    internal static class MdImages
    {
        public const string Folder = "imagenes";
        // Wider pictures are decoded smaller: enough for the preview on a 4K screen and for print.
        public const int MaxWidth = 2000, MaxPixels = 16 * 1000 * 1000;
        const long MaxFile = 64L * 1024 * 1024, Budget = 160L * 1024 * 1024;
        static readonly string[] Exts = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".svg" };
        static readonly Regex SchemeRx = new Regex(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.Compiled);
        static readonly Regex DriveRx = new Regex(@"^[A-Za-z]:[\\/]", RegexOptions.Compiled);

        public static bool IsImageFile(string path)
        {
            try { return path != null && Array.IndexOf(Exts, System.IO.Path.GetExtension(path).ToLowerInvariant()) >= 0; }
            catch (ArgumentException) { return false; }
        }

        // The local file an image link points to, or null: a web address, a data URI, another scheme, or a relative link
        // in a document that has no folder yet. Relative links count from the document's folder.
        public static string LocalPath(string url, string baseDir)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string u = url.Trim();
            try
            {
                if (DriveRx.IsMatch(u)) return System.IO.Path.GetFullPath(Uri.UnescapeDataString(u).Replace('/', '\\'));
                if (u.StartsWith(@"\\", StringComparison.Ordinal)) return System.IO.Path.GetFullPath(u);
                if (SchemeRx.IsMatch(u))
                {
                    if (!u.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return null;
                    Uri f;
                    return Uri.TryCreate(u, UriKind.Absolute, out f) && f.IsFile ? f.LocalPath : null;
                }
                if (baseDir == null || u.StartsWith("/", StringComparison.Ordinal)) return null;
                int cut = u.IndexOfAny(new char[] { '?', '#' });
                if (cut >= 0) u = u.Substring(0, cut);
                if (u.Length == 0) return null;
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, Uri.UnescapeDataString(u).Replace('/', '\\')));
            }
            catch (Exception) { return null; }
        }

        // A picture on another computer (\\server\share\...) for a document that is not on that same server: showing it
        // would make Windows connect to whatever server the text names (and offer it the user's credentials) just
        // because the document was opened. Such pictures stay as text, like web addresses.
        public static bool Remote(string path, string baseDir)
        {
            string host = Host(path);
            if (host == null) return false;
            string own = Host(baseDir);
            return own == null || !string.Equals(host, own, StringComparison.OrdinalIgnoreCase);
        }

        static string Host(string p)
        {
            if (p == null || !p.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            string s = p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? p.Substring(8) : p.Substring(2);
            int cut = s.IndexOfAny(new char[] { '\\', '/' });
            return cut < 0 ? s : s.Substring(0, cut);
        }

        // A path for a link: '/' between folders, and what would end or break the link escaped.
        public static string Escape(string rel)
        {
            StringBuilder sb = new StringBuilder(rel.Length + 8);
            foreach (char c in rel.Replace('\\', '/'))
            {
                if (c == ' ') sb.Append("%20");
                else if (c == '%') sb.Append("%25");
                else if (c == '(') sb.Append("%28");
                else if (c == ')') sb.Append("%29");
                else if (c == '<') sb.Append("%3C");
                else if (c == '>') sb.Append("%3E");
                else if (c == '#') sb.Append("%23");
                else if (c == '?') sb.Append("%3F");
                else if (c == '[') sb.Append("%5B");
                else if (c == ']') sb.Append("%5D");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Link(string rel) { return "![](" + Escape(rel) + ")"; }

        // A file's path from a folder ('/' between folders), or null when it is not inside it.
        public static string Relative(string dir, string path)
        {
            try
            {
                string d = System.IO.Path.GetFullPath(dir).TrimEnd('\\') + "\\", p = System.IO.Path.GetFullPath(path);
                return p.StartsWith(d, StringComparison.OrdinalIgnoreCase) && p.Length > d.Length ? p.Substring(d.Length).Replace('\\', '/') : null;
            }
            catch (Exception) { return null; }
        }

        // ---- Loading

        sealed class Entry
        {
            public BitmapSource Bmp;
            public string Error;
            public DateTime Stamp;
            public long Size, Bytes, Used;
        }

        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static readonly object gate = new object();
        static long tick, total;
        static int generation;

        // Forgets every picture (the window closed: nothing stays in memory).
        public static void Clear()
        {
            lock (jobs) jobs.Clear();
            lock (gate)
            {
                cache.Clear();
                total = 0;
                generation++;
            }
        }

        // The picture last loaded from this file, if any (it may have changed since: Request checks).
        public static BitmapSource Peek(string path)
        {
            lock (gate)
            {
                Entry e;
                return cache.TryGetValue(path, out e) ? e.Bmp : null;
            }
        }

        // The picture in a file, decoded again only when the file changed. Any thread; the result is frozen.
        public static BitmapSource Load(string path, out string error)
        {
            error = null;
            DateTime stamp;
            long size;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists) { error = I18n.T("No encuentro la imagen", "Cannot find the image"); return null; }
                stamp = fi.LastWriteTimeUtc;
                size = fi.Length;
            }
            catch (Exception ex) { error = I18n.T("No puedo leer la imagen", "Cannot read the image"); ShotStack.Log("Imagen " + path + ": " + ex.Message); return null; }
            int gen;
            lock (gate)
            {
                Entry e;
                if (cache.TryGetValue(path, out e) && e.Stamp == stamp && e.Size == size) { e.Used = ++tick; error = e.Error; return e.Bmp; }
                gen = generation;
            }
            BitmapSource bmp = null;
            string err = null;
            if (size > MaxFile) err = I18n.T("La imagen es demasiado grande para mostrarla", "The image is too big to show");
            else if (string.Equals(System.IO.Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase)) err = I18n.T("No puedo mostrar im\u00E1genes SVG", "Cannot show SVG images");
            else
            {
                try { bmp = Decode(path); }
                catch (Exception ex) { err = I18n.T("No puedo mostrar esta imagen", "Cannot show this image"); ShotStack.Log("Imagen " + path + ": " + ex.Message); }
            }
            Keep(path, new Entry { Bmp = bmp, Error = err, Stamp = stamp, Size = size, Bytes = bmp == null ? 0 : (long)bmp.PixelWidth * bmp.PixelHeight * 4 }, gen);
            error = err;
            return bmp;
        }

        static void Keep(string path, Entry n, int gen)
        {
            lock (gate)
            {
                // Decoded while the cache was being emptied: not kept.
                if (gen != generation) return;
                Entry old;
                if (cache.TryGetValue(path, out old)) total -= old.Bytes;
                n.Used = ++tick;
                cache[path] = n;
                total += n.Bytes;
                // The least recently used go first once over budget (the newest always stays).
                while (total > Budget && cache.Count > 1)
                {
                    string worst = null;
                    long at = long.MaxValue;
                    foreach (KeyValuePair<string, Entry> kv in cache) if (kv.Value.Used < at && kv.Value != n) { at = kv.Value.Used; worst = kv.Key; }
                    if (worst == null) break;
                    total -= cache[worst].Bytes;
                    cache.Remove(worst);
                }
            }
        }

        // Decoded whole into memory (the file is not kept open), at most MaxWidth pixels wide, at 96 dpi so a picture
        // shows at its pixel size as on the web.
        static BitmapSource Decode(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                BitmapDecoder dec = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                BitmapSource f = dec.Frames[0];
                // Very tall pictures (scrolling captures) are kept under MaxPixels too.
                double k = f.PixelWidth > MaxWidth ? MaxWidth / (double)f.PixelWidth : 1;
                double area = (double)f.PixelWidth * f.PixelHeight * k * k;
                if (area > MaxPixels) k *= Math.Sqrt(MaxPixels / area);
                if (k < 1) f = new TransformedBitmap(f, new ScaleTransform(k, k));
                FormatConvertedBitmap px = new FormatConvertedBitmap(f, PixelFormats.Pbgra32, null, 0);
                int w = px.PixelWidth, h = px.PixelHeight, stride = w * 4;
                byte[] data = new byte[(long)stride * h];
                px.CopyPixels(data, stride, 0);
                BitmapSource b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, data, stride);
                b.Freeze();
                return b;
            }
        }

        sealed class Job
        {
            public string Path;
            public Dispatcher To;
            public Action<BitmapSource, string> Done;
        }

        static readonly Queue<Job> jobs = new Queue<Job>();
        static bool working;

        // Loads off the UI thread, one picture at a time in the order asked; done runs on the caller's dispatcher.
        public static void Request(string path, Action<BitmapSource, string> done)
        {
            Job j = new Job { Path = path, To = Dispatcher.CurrentDispatcher, Done = done };
            lock (jobs)
            {
                jobs.Enqueue(j);
                if (working) return;
                working = true;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Work(); });
        }

        static void Work()
        {
            while (true)
            {
                Job j;
                lock (jobs)
                {
                    if (jobs.Count == 0) { working = false; return; }
                    j = jobs.Dequeue();
                }
                string err = null;
                BitmapSource b = null;
                try { b = Load(j.Path, out err); }
                catch (Exception ex) { err = I18n.T("No puedo mostrar esta imagen", "Cannot show this image"); ShotStack.Log("Imagen: " + ex.Message); }
                Job k = j;
                try { if (!k.To.HasShutdownStarted) k.To.BeginInvoke(new Action(delegate { k.Done(b, err); })); }
                catch (Exception ex) { ShotStack.Log("Imagen aviso: " + ex.Message); }
            }
        }

        // ---- Saving next to the document

        // A pasted picture as imagenes\imagen-yyyyMMdd-HHmmss.png beside the document; png: the clipboard's own PNG bytes,
        // otherwise the bitmap is encoded. The path for the link, or null with the reason.
        public static string SavePasted(string docDir, byte[] png, BitmapSource bmp, DateTime when, out string error)
        {
            error = null;
            try
            {
                string dir = System.IO.Path.Combine(docDir, Folder);
                Directory.CreateDirectory(dir);
                string target = Unique(dir, "imagen-" + when.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture), ".png");
                byte[] data = png ?? Encode(bmp);
                using (FileStream fs = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) fs.Write(data, 0, data.Length);
                return Folder + "/" + System.IO.Path.GetFileName(target);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Pegar imagen: " + ex.Message);
                error = I18n.T("No he podido guardar la imagen: ", "Could not save image: ") + ex.Message;
                return null;
            }
        }

        // PNG bytes of a frozen bitmap, through GDI+ (no WPF encoder on a worker thread).
        static byte[] Encode(BitmapSource bmp)
        {
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            using (System.Drawing.Bitmap b = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                System.Drawing.Imaging.BitmapData bd = b.LockBits(new System.Drawing.Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try { bmp.CopyPixels(new Int32Rect(0, 0, w, h), bd.Scan0, bd.Stride * h, bd.Stride); }
                finally { b.UnlockBits(bd); }
                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        // A dropped file: its path from the document's folder when it is inside it; otherwise a copy in imagenes (the
        // same file already copied there is reused).
        public static string Adopt(string docDir, string file, out string error)
        {
            error = null;
            try
            {
                string full = System.IO.Path.GetFullPath(file);
                string rel = Relative(docDir, full);
                if (rel != null) return rel;
                string dir = System.IO.Path.Combine(docDir, Folder);
                Directory.CreateDirectory(dir);
                string name = System.IO.Path.GetFileNameWithoutExtension(full), ext = System.IO.Path.GetExtension(full);
                string same = System.IO.Path.Combine(dir, name + ext);
                if (File.Exists(same) && SameBytes(same, full)) return Folder + "/" + name + ext;
                string target = Unique(dir, name, ext);
                File.Copy(full, target, false);
                return Folder + "/" + System.IO.Path.GetFileName(target);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Soltar imagen: " + ex.Message);
                error = I18n.Fmt("No he podido copiar {0}: {1}", "Could not copy {0}: {1}", System.IO.Path.GetFileName(file), ex.Message);
                return null;
            }
        }

        static bool SameBytes(string a, string b)
        {
            FileInfo fa = new FileInfo(a), fb = new FileInfo(b);
            if (fa.Length != fb.Length || fa.Length > MaxFile) return false;
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        static string Unique(string dir, string name, string ext)
        {
            string p = System.IO.Path.Combine(dir, name + ext);
            for (int n = 2; File.Exists(p) || Directory.Exists(p); n++) p = System.IO.Path.Combine(dir, name + "-" + n + ext);
            return p;
        }
    }
}
