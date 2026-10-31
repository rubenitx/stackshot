// Stackshot - Covering the personal data of a capture: the editor's own Redact mark painted over what Sensitive finds,
// the job a new capture goes through when the option is on, and the uncovered originals kept privately so the editor
// can show the covers as marks that can be removed.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Stackshot
{
    public static class SensitiveCover
    {
        // The boxes painted with the editor's Redact mark, so a cover in a capture and one drawn in the editor are the
        // same pixels.
        public static void Paint(Bitmap img, IList<Rectangle> boxes)
        {
            if (boxes == null || boxes.Count == 0) return;
            using (Graphics g = Graphics.FromImage(img))
                foreach (Rectangle r in boxes) Painter.Draw(g, Mark(r), img);
        }

        public static Shape Mark(Rectangle r)
        {
            Shape s = new Shape();
            s.Kind = Tool.Redact;
            s.A = new PointF(r.Left, r.Top);
            s.B = new PointF(r.Right, r.Bottom);
            return s;
        }

        public static List<Rectangle> Boxes(IList<SensitiveFinding> found)
        {
            List<Rectangle> o = new List<Rectangle>();
            foreach (SensitiveFinding f in found) if (!f.Box.IsEmpty && !o.Contains(f.Box)) o.Add(f.Box);
            return o;
        }

        // SaveCapture's things for a capture about to be covered: the thumbnail becomes a veil, the uncovered copy for the
        // clipboard is dropped (the job remembers it was wanted) and CoverStore expects its original. The job owns shot.
        public static CoverJob Prepare(Bitmap shot, string path, ref Bitmap preview, ref Bitmap forClipboard)
        {
            preview = Veil(preview);
            CoverJob job = new CoverJob();
            job.Shot = shot;
            job.Path = path;
            job.WantCopy = forClipboard != null;
            if (forClipboard != null) { forClipboard.Dispose(); forClipboard = null; }
            CoverStore.Expect(path);
            return job;
        }

        // A thumbnail too blurred to read anything in, shown while the capture is checked. Takes the preview over.
        public static Bitmap Veil(Bitmap preview)
        {
            int w = preview.Width, h = preview.Height;
            Bitmap o = null;
            try
            {
                o = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Bitmap tiny = new Bitmap(Math.Max(1, w / 28), Math.Max(1, h / 28), PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(tiny))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(preview, new Rectangle(0, 0, tiny.Width, tiny.Height));
                    }
                    using (Graphics g = Graphics.FromImage(o))
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(tiny, new Rectangle(0, 0, w, h), 0, 0, tiny.Width, tiny.Height, GraphicsUnit.Pixel, ia);
                    }
                }
            }
            catch (Exception ex)
            {
                // Out of memory: a plain grey face, never the capture itself.
                ShotStack.Log("Datos sensibles, miniatura: " + ex.Message);
                if (o != null) o.Dispose();
                o = new Bitmap(16, 9, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(o)) g.Clear(Color.FromArgb(128, 128, 132));
            }
            preview.Dispose();
            return o;
        }

        // The note on a capture's thumbnail: "2 datos tapados", or that it could not be checked.
        public static string Note(CoverOutcome r)
        {
            if (r == null) return null;
            if (r.Error != null) return I18n.T("Sin revisar", "Unchecked");
            int n = r.Found.Count;
            return n == 0 ? null : n == 1 ? I18n.T("1 dato tapado", "1 piece of data redacted") : (I18n.IsSpanish ? n + " datos tapados" : n + " items redacted");
        }

        // Same pixels, a pixel fully transparent in both counting as the same whatever its colour.
        public static bool SamePixels(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            Rectangle r = new Rectangle(0, 0, a.Width, a.Height);
            BitmapData da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                BitmapData db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] ra = new int[r.Width], rb = new int[r.Width];
                    for (int y = 0; y < r.Height; y++)
                    {
                        Marshal.Copy(new IntPtr(da.Scan0.ToInt64() + (long)y * da.Stride), ra, 0, r.Width);
                        Marshal.Copy(new IntPtr(db.Scan0.ToInt64() + (long)y * db.Stride), rb, 0, r.Width);
                        for (int x = 0; x < r.Width; x++)
                            if (ra[x] != rb[x] && ((ra[x] | rb[x]) & unchecked((int)0xFF000000)) != 0) return false;
                    }
                    return true;
                }
                finally { b.UnlockBits(db); }
            }
            finally { a.UnlockBits(da); }
        }
    }

    // What checking one capture gave: what was found (nothing when it could not be read: Error says why), the thumbnail
    // and the copy for the clipboard of the image as it is now (both the receiver's to dispose; Copy is null when it
    // wasn't asked for or the capture could not be checked), and how long it took.
    public sealed class CoverOutcome
    {
        public List<SensitiveFinding> Found = new List<SensitiveFinding>();
        public string Error;
        public Bitmap Preview, Copy;
        public Size Size;
        public long Ms;
    }

    // A new capture checked before anything shows it: read with OCR, its personal data covered in place, then written
    // (Write takes the image) and handed back (Done gets the outcome). The uncovered original is kept in CoverStore. Run
    // blocks: call it off the UI thread. Nothing is ever handed out uncovered when something was found, and the copy for
    // the clipboard is left out when the capture could not be checked.
    public sealed class CoverJob
    {
        public Bitmap Shot;
        public string Path;
        public bool WantCopy;
        public Action<Bitmap> Write;
        public Action<CoverOutcome> Done;

        // Jobs run one after another on their own thread: a burst of captures waits its turn instead of running several
        // OCRs at once or holding pool threads the PNG writes need.
        static readonly Queue<CoverJob> queue = new Queue<CoverJob>();
        static bool running;

        // Captures started and not yet handed to Write: their file is not even on its way.
        static readonly HashSet<string> checking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsChecking(string path)
        {
            if (path == null) return false;
            lock (checking) return checking.Contains(path);
        }

        public static List<string> Checking()
        {
            lock (checking) return new List<string>(checking);
        }

        // Waits (up to ms) until the capture's check is over. Blocks: never on the UI thread.
        public static void WaitChecked(string path, int ms)
        {
            if (path == null) return;
            DateTime until = DateTime.UtcNow.AddMilliseconds(ms);
            lock (checking)
                while (checking.Contains(path))
                {
                    int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                    if (left <= 0) break;
                    Monitor.Wait(checking, left);
                }
        }

        static void Checked(string path)
        {
            if (path == null) return;
            lock (checking)
            {
                checking.Remove(path);
                Monitor.PulseAll(checking);
            }
        }

        public void Start()
        {
            if (Path != null) lock (checking) checking.Add(Path);
            lock (queue)
            {
                queue.Enqueue(this);
                if (running) return;
                running = true;
            }
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    CoverJob next;
                    lock (queue)
                    {
                        if (queue.Count == 0) { running = false; return; }
                        next = queue.Dequeue();
                    }
                    try { next.Run(); }
                    catch (Exception ex) { ShotStack.Log("Datos sensibles: " + ex); }
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Name = "Datos sensibles";
            t.Start();
        }

        public void Run()
        {
            Stopwatch sw = Stopwatch.StartNew();
            CoverOutcome r = new CoverOutcome();
            Bitmap original = null;
            bool written = false, answered = false;
            try
            {
                try
                {
                    r.Size = Shot.Size;
                    CoverStore.DropStale(Path);
                    string error;
                    List<Ocr.Word> words = Ocr.ReadWords(Shot, out error);
                    if (words == null) r.Error = error ?? I18n.T("No se ha podido leer la imagen.", "Could not read the image.");
                    else
                    {
                        r.Found = Sensitive.Find(words, Shot.Size);
                        if (r.Found.Count > 0)
                        {
                            // Without memory for the original the capture is still covered (the covers just can't be removed).
                            try { original = TrackedData.DeepCopy(Shot); }
                            catch (Exception ex) { ShotStack.Log("Datos sensibles, original: " + ex.Message); }
                            SensitiveCover.Paint(Shot, SensitiveCover.Boxes(r.Found));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Datos sensibles: " + ex);
                    r.Error = ex is OutOfMemoryException ? I18n.T("No hay memoria suficiente para revisar una imagen tan grande.", "Not enough memory to check such a large image.")
                                                         : I18n.T("No se ha podido revisar la imagen.", "Could not check the image.");
                }
                try { r.Preview = ShotStack.Preview(Shot, 600); }
                catch (Exception ex) { ShotStack.Log("Datos sensibles, miniatura: " + ex.Message); }
                if (WantCopy && r.Error == null)
                {
                    try { r.Copy = TrackedData.DeepCopy(Shot); }
                    catch (Exception ex) { ShotStack.Log("Datos sensibles, portapapeles: " + ex.Message); }
                }
                written = true;
                if (Write != null) Write(Shot);
                else Shot.Dispose();
                Checked(Path);
                r.Ms = sw.ElapsedMilliseconds;
                answered = true;
                if (Done != null) Done(r);
                else
                {
                    if (r.Preview != null) r.Preview.Dispose();
                    if (r.Copy != null) r.Copy.Dispose();
                }
                // Kept after the covered capture is on its way, so nothing that shows it waits for this.
                if (original != null && r.Error == null) CoverStore.Save(Path, original, r.Found);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Datos sensibles: " + ex);
                if (!written) { if (Write != null) Write(Shot); else Shot.Dispose(); }
                // Whoever waits for the answer always gets one; nothing goes to the clipboard after a failure.
                if (!answered && Done != null)
                {
                    if (r.Copy != null) { r.Copy.Dispose(); r.Copy = null; }
                    if (r.Error == null) r.Error = I18n.T("No se ha podido revisar la imagen.", "Could not check the image.");
                    try { Done(r); }
                    catch (Exception again) { ShotStack.Log("Datos sensibles: " + again.Message); }
                }
            }
            finally
            {
                if (original != null) original.Dispose();
                Checked(Path);
                CoverStore.Settle(Path);
            }
        }
    }

    // The uncovered originals of covered captures, in the data folder (never next to the capture, so nothing lists, shares
    // or reads them by mistake), with the boxes covered. One stays only while its capture is in the temporary folder and
    // unchanged since it was covered.
    public static class CoverStore
    {
        public static string Dir = System.IO.Path.Combine(Settings.DataDir, "originales");   // tests point it elsewhere
        static readonly HashSet<string> pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static string ImageFile(string capture) { return System.IO.Path.Combine(Dir, System.IO.Path.GetFileNameWithoutExtension(capture) + ".png"); }
        static string ListFile(string capture) { return System.IO.Path.Combine(Dir, System.IO.Path.GetFileNameWithoutExtension(capture) + ".txt"); }

        // A capture being checked: its original may still come.
        public static void Expect(string capture) { lock (pending) pending.Add(capture); }

        public static void Settle(string capture)
        {
            lock (pending)
            {
                pending.Remove(capture);
                Monitor.PulseAll(pending);
            }
        }

        // An original is kept (or may still come) for this capture.
        public static bool Has(string capture)
        {
            if (capture == null) return false;
            lock (pending) if (pending.Contains(capture)) return true;
            try { return File.Exists(ListFile(capture)); }
            catch { return false; }
        }

        static void WaitPending(string capture, int ms)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(ms);
            lock (pending)
                while (pending.Contains(capture))
                {
                    int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                    if (left <= 0) break;
                    Monitor.Wait(pending, left);
                }
        }

        // Written under temporary names and renamed: the list (which says the original is there) goes last.
        public static void Save(string capture, Bitmap original, IList<SensitiveFinding> found)
        {
            string img = ImageFile(capture), list = ListFile(capture);
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                foreach (SensitiveFinding f in found)
                    sb.Append(f.Box.X.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(f.Box.Y.ToString(CultureInfo.InvariantCulture)).Append(' ')
                      .Append(f.Box.Width.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(f.Box.Height.ToString(CultureInfo.InvariantCulture)).Append(' ')
                      .Append(f.Kind.ToString()).Append('\n');
                original.Save(img + ".part", ImageFormat.Png);
                Replace(img + ".part", img);
                File.WriteAllText(list + ".part", sb.ToString(), new UTF8Encoding(false));
                Replace(list + ".part", list);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Datos sensibles, original: " + ex.Message);
                Forget(capture);
                try { File.Delete(img + ".part"); } catch { }
                try { File.Delete(list + ".part"); } catch { }
            }
        }

        static void Replace(string from, string to)
        {
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        // The original of a capture and the boxes covered in it, when the capture is still exactly the original with those
        // covers (an edited capture drops its original). Waits for a capture still being checked and written. null
        // otherwise. Blocks: call it off the UI thread.
        public static Bitmap Restore(string capture, out List<Rectangle> boxes)
        {
            boxes = null;
            WaitPending(capture, 20000);
            string img = ImageFile(capture), list = ListFile(capture);
            if (!File.Exists(list) || !File.Exists(img)) return null;
            List<Rectangle> got = new List<Rectangle>();
            foreach (string line in File.ReadAllLines(list))
            {
                string[] p = line.Split(' ');
                int x, y, w, h;
                if (p.Length < 4 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                    !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) || !int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) || w <= 0 || h <= 0) continue;
                Rectangle r = new Rectangle(x, y, w, h);
                if (!got.Contains(r)) got.Add(r);
            }
            if (got.Count == 0) { Forget(capture); return null; }
            Bitmap original = Load(img);
            try
            {
                bool same;
                using (Bitmap now = ShotStack.LoadFull(capture))
                using (Bitmap expected = TrackedData.DeepCopy(original))
                {
                    SensitiveCover.Paint(expected, got);
                    same = SensitiveCover.SamePixels(expected, now);
                }
                if (!same)
                {
                    original.Dispose();
                    Forget(capture);
                    return null;
                }
            }
            catch { original.Dispose(); throw; }
            boxes = got;
            return original;
        }

        static Bitmap Load(string file)
        {
            byte[] data = File.ReadAllBytes(file);
            using (MemoryStream ms = new MemoryStream(data))
            using (Image i = Image.FromStream(ms))
                return new Bitmap(i);
        }

        public static void Forget(string capture)
        {
            try { File.Delete(ListFile(capture)); } catch { }
            try { File.Delete(ImageFile(capture)); } catch { }
        }

        // A new capture is taking this name: an original still kept under it belongs to an earlier capture of the same
        // name, gone since (a file name template without the time reuses names).
        public static void DropStale(string capture)
        {
            if (capture == null) return;
            if (File.Exists(ListFile(capture)) || File.Exists(ImageFile(capture))) Forget(capture);
        }

        // Originals whose capture is gone from the temporary folder (and leftovers of an interrupted save), as soon as it
        // is gone: an uncovered original never outlives its capture. writing says whether a capture is still on its way
        // to the disk (its original stays).
        public static int Prune(string tempFolder, Predicate<string> writing)
        {
            int n = 0;
            try
            {
                if (!Directory.Exists(Dir)) return 0;
                foreach (string f in Directory.GetFiles(Dir))
                {
                    string capture = System.IO.Path.Combine(tempFolder, Stem(f) + ".png");
                    lock (pending) if (pending.Contains(capture)) continue;
                    if (File.Exists(capture) || (writing != null && writing(capture))) continue;
                    try { File.Delete(f); n++; }
                    catch (Exception ex) { ShotStack.Log("Datos sensibles, original " + System.IO.Path.GetFileName(f) + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { ShotStack.Log("Datos sensibles, originales: " + ex.Message); }
            return n;
        }

        // "Captura 2026-10-12 10.00.00" for its .png, .txt and their .part files.
        static string Stem(string file)
        {
            string name = System.IO.Path.GetFileName(file);
            if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
            return System.IO.Path.GetFileNameWithoutExtension(name);
        }
    }
}
