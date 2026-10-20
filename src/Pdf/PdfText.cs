// Stackshot - The text of each PDF page, read with Windows' own OCR (Windows does not hand out the text of a PDF): words
// and where they sit, kept in memory and in the document's folder of the cache, so a PDF opened again finds it ready.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

namespace Stackshot
{
    // A word read from a page. Box is where it sits on the page as shown: fractions of its width and height, from the
    // top left corner. Words come in reading order; Line numbers the lines from the top.
    public sealed class PdfWord
    {
        public string Text;
        public Rect Box;
        public int Line;
    }

    public sealed class PdfPageText
    {
        public readonly List<PdfWord> Words;

        public PdfPageText(List<PdfWord> words) { Words = words ?? new List<PdfWord>(); }

        public bool Empty { get { return Words.Count == 0; } }

        // The words first..last as text: spaces within a line, a line break between lines.
        public string Between(int first, int last)
        {
            if (first > last) { int t = first; first = last; last = t; }
            first = Math.Max(0, first);
            last = Math.Min(Words.Count - 1, last);
            StringBuilder sb = new StringBuilder();
            for (int i = first; i <= last; i++)
            {
                if (i > first) sb.Append(Words[i].Line != Words[i - 1].Line ? "\n" : " ");
                sb.Append(Words[i].Text);
            }
            return sb.ToString();
        }

        public string All { get { return Words.Count == 0 ? "" : Between(0, Words.Count - 1); } }

        // The word under p (page fractions), or -1. near: otherwise the closest word on the closest line, for a drag
        // that starts or ends between words.
        public int Hit(Point p, bool near)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int i = 0; i < Words.Count; i++)
            {
                Rect b = Words[i].Box;
                // A little slack around each word, so the gap between two words or lines still counts.
                double padX = b.Height * 0.25, padY = b.Height * 0.2;
                if (p.X >= b.Left - padX && p.X <= b.Right + padX && p.Y >= b.Top - padY && p.Y <= b.Bottom + padY) return i;
                if (!near) continue;
                double dy = p.Y < b.Top ? b.Top - p.Y : p.Y > b.Bottom ? p.Y - b.Bottom : 0;
                double dx = p.X < b.Left ? b.Left - p.X : p.X > b.Right ? p.X - b.Right : 0;
                double d = dy * 4 + dx;   // the line matters more than the place along it
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // One box per line covered by words first..last (fractions of the page).
        public List<Rect> LineBoxes(int first, int last)
        {
            if (first > last) { int t = first; first = last; last = t; }
            List<Rect> o = new List<Rect>();
            Rect cur = Rect.Empty;
            int line = -1;
            for (int i = Math.Max(0, first); i <= last && i < Words.Count; i++)
            {
                PdfWord w = Words[i];
                if (w.Line != line && !cur.IsEmpty) { o.Add(cur); cur = Rect.Empty; }
                line = w.Line;
                cur = cur.IsEmpty ? w.Box : Rect.Union(cur, w.Box);
            }
            if (!cur.IsEmpty) o.Add(cur);
            return o;
        }

        // The words a box covers (their centres inside it), first and last, or false.
        public bool Inside(Rect box, out int first, out int last)
        {
            first = -1; last = -1;
            for (int i = 0; i < Words.Count; i++)
            {
                Rect b = Words[i].Box;
                if (!box.Contains(new Point(b.X + b.Width / 2, b.Y + b.Height / 2))) continue;
                if (first < 0) first = i;
                last = i;
            }
            return first >= 0;
        }

        // Where each match of query starts and ends (word indices), ignoring case and accents.
        public List<int[]> Find(string query)
        {
            List<int[]> o = new List<int[]>();
            string q = Fold(query == null ? "" : query.Trim());
            q = string.Join(" ", q.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            if (q.Length == 0 || Words.Count == 0) return o;
            StringBuilder sb = new StringBuilder();
            List<int> owner = new List<int>();
            for (int i = 0; i < Words.Count; i++)
            {
                if (i > 0) { sb.Append(' '); owner.Add(i); }
                string f = Fold(Words[i].Text);
                sb.Append(f);
                for (int k = 0; k < f.Length; k++) owner.Add(i);
            }
            string s = sb.ToString();
            int at = 0;
            while (at <= s.Length - q.Length)
            {
                int k = s.IndexOf(q, at, StringComparison.Ordinal);
                if (k < 0) break;
                int a = owner[k], b = owner[Math.Min(owner.Count - 1, k + q.Length - 1)];
                if (s[k] == ' ') a = owner[k];
                o.Add(new int[] { a, b });
                at = k + Math.Max(1, q.Length);
            }
            return o;
        }

        // Lower case without accents ("Camión" finds "camion"), one character for one where it can.
        public static string Fold(string s)
        {
            StringBuilder o = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                string d = c.ToString().Normalize(NormalizationForm.FormD);
                foreach (char x in d)
                    if (CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark) o.Append(char.ToLowerInvariant(x));
            }
            return o.ToString();
        }
    }

    // Reads the pages on a thread of its own: the engine draws each one for the OCR, which then reads it. The page asked
    // for most recently goes first; answers come on the UI thread through Read.
    public sealed class PdfReader : IDisposable
    {
        const int Version = 1;
        readonly PdfEngine engine;
        readonly string cache;
        readonly Dispatcher ui;
        readonly PdfPageText[] pages;
        readonly object gate = new object();
        readonly List<int> wanted = new List<int>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread thread;
        volatile bool stop;
        int sinceSave;

        public event Action<int> Read;
        public string Error { get; private set; }     // the OCR is missing from this Windows: no text, and why

        public PdfReader(PdfEngine engine, string cacheFile, Dispatcher ui)
        {
            this.engine = engine;
            cache = cacheFile;
            this.ui = ui;
            pages = new PdfPageText[engine.Pages];
            LoadCache();
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Name = "Stackshot: texto del PDF";
            lock (live) live.Add(this);
            thread.Start();
        }

        // The text of a page, or null while it has not been read.
        public PdfPageText Get(int page)
        {
            if (page < 0 || page >= pages.Length) return null;
            return Volatile.Read(ref pages[page]);
        }

        public int Count { get { return pages.Length; } }

        public int ReadCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pages.Length; i++) if (Volatile.Read(ref pages[i]) != null) n++;
                return n;
            }
        }

        // Read this page next (pages asked for later go before it).
        public void Want(int page)
        {
            if (page < 0 || page >= pages.Length || Get(page) != null || Error != null) return;
            lock (gate)
            {
                wanted.Remove(page);
                wanted.Add(page);
            }
            wake.Set();
        }

        // Every page from this one on, then the ones before (a search through the whole document).
        public void WantAll(int from)
        {
            if (Error != null) return;
            lock (gate)
            {
                List<int> order = new List<int>();
                for (int i = pages.Length - 1; i >= 0; i--)
                {
                    int p = (from + pages.Length - 1 - i) % pages.Length;   // newest last: from goes first
                    if (Get(p) == null && !wanted.Contains(p)) order.Add(p);
                }
                // Insert at the front so pages asked for one by one still win.
                wanted.InsertRange(0, order);
            }
            wake.Set();
        }

        public bool Busy { get { lock (gate) return wanted.Count > 0; } }

        void Loop()
        {
            while (!stop)
            {
                int page = -1;
                lock (gate)
                {
                    while (wanted.Count > 0 && page < 0)
                    {
                        int p = wanted[wanted.Count - 1];
                        wanted.RemoveAt(wanted.Count - 1);
                        if (Get(p) == null) page = p;
                    }
                }
                if (page < 0) { SaveCache(false); wake.WaitOne(1000); continue; }
                PdfPageText t = ReadPage(page);
                if (stop) break;
                if (t == null) continue;
                Volatile.Write(ref pages[page], t);
                if (++sinceSave >= 4) SaveCache(true);
                Action<int> h = Read;
                if (h != null)
                {
                    try { ui.BeginInvoke(new Action(delegate { h(page); })); }
                    catch { }
                }
            }
            SaveCache(false);
            lock (live) live.Remove(this);
        }

        // About 150 dpi: what the OCR reads best on document text (it doubles small images itself).
        PdfPageText ReadPage(int page)
        {
            Size s = engine.Sizes[page];
            double k = Math.Min(1.6, 2400 / Math.Max(s.Width, s.Height));
            int w = Math.Max(1, (int)Math.Round(s.Width * k)), h = Math.Max(1, (int)Math.Round(s.Height * k));
            System.Drawing.Bitmap b = engine.RenderGdi(page, w, h, 60000);
            if (b == null) return null;
            // Fractions of the image as it came, whatever size the engine made it.
            w = b.Width;
            h = b.Height;
            List<Ocr.Word> words;
            string error;
            using (b) words = Ocr.ReadWords(b, out error);
            if (words == null)
            {
                if (error == Ocr.Unavailable) { Error = error; lock (gate) wanted.Clear(); }
                return null;
            }
            List<PdfWord> o = new List<PdfWord>();
            foreach (Ocr.Word x in words)
            {
                string text = (x.Text ?? "").Trim();
                if (text.Length == 0) continue;
                o.Add(new PdfWord { Text = text, Line = x.Line, Box = new Rect(x.Box.X / w, x.Box.Y / h, x.Box.Width / w, x.Box.Height / h) });
            }
            return new PdfPageText(o);
        }

        // ---- Cache: {"v":1,"pages":{"3":[["word",x,y,w,h,line],...]}}

        void LoadCache()
        {
            if (cache == null) return;
            try
            {
                if (!File.Exists(cache) || new FileInfo(cache).Length > 64L * 1024 * 1024) return;
                JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                Dictionary<string, object> root = js.DeserializeObject(File.ReadAllText(cache, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("v") || Convert.ToInt32(root["v"]) != Version) return;
                Dictionary<string, object> ps = root["pages"] as Dictionary<string, object>;
                if (ps == null) return;
                foreach (KeyValuePair<string, object> kv in ps)
                {
                    int p;
                    if (!int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out p) || p < 0 || p >= pages.Length) continue;
                    object[] list = kv.Value as object[];
                    if (list == null) continue;
                    List<PdfWord> words = new List<PdfWord>();
                    foreach (object o in list)
                    {
                        object[] a = o as object[];
                        if (a == null || a.Length < 6) continue;
                        words.Add(new PdfWord
                        {
                            Text = Convert.ToString(a[0], CultureInfo.InvariantCulture),
                            Box = new Rect(Num(a[1]), Num(a[2]), Math.Max(0, Num(a[3])), Math.Max(0, Num(a[4]))),
                            Line = Convert.ToInt32(a[5], CultureInfo.InvariantCulture)
                        });
                    }
                    pages[p] = new PdfPageText(words);
                }
            }
            catch (Exception ex) { ShotStack.Log("PDF, texto guardado: " + ex.Message); }
        }

        static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }

        void SaveCache(bool force)
        {
            if (cache == null || (sinceSave == 0 && !force) || Settings.ReadOnly && cache.StartsWith(Settings.DataDir, StringComparison.OrdinalIgnoreCase)) return;
            sinceSave = 0;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"v\":").Append(Version).Append(",\"pages\":{");
                bool first = true;
                for (int i = 0; i < pages.Length; i++)
                {
                    PdfPageText t = Get(i);
                    if (t == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(i).Append("\":[");
                    for (int k = 0; k < t.Words.Count; k++)
                    {
                        PdfWord w = t.Words[k];
                        if (k > 0) sb.Append(',');
                        sb.Append('[').Append(Json(w.Text)).Append(',').Append(D(w.Box.X)).Append(',').Append(D(w.Box.Y)).Append(',')
                          .Append(D(w.Box.Width)).Append(',').Append(D(w.Box.Height)).Append(',').Append(w.Line).Append(']');
                    }
                    sb.Append(']');
                }
                sb.Append("}}");
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                AtomicFile.Write(cache, new UTF8Encoding(false).GetBytes(sb.ToString()));
            }
            catch (Exception ex) { ShotStack.Log("PDF, guardar texto: " + ex.Message); }
        }

        static string D(double v) { return v.ToString("0.#####", CultureInfo.InvariantCulture); }

        internal static string Json(string s)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        public void Dispose()
        {
            stop = true;
            lock (gate) wanted.Clear();
            wake.Set();
        }

        static readonly List<PdfReader> live = new List<PdfReader>();

        // At the end of the process: every reader stopped and waited for (one may be in the middle of the OCR).
        public static void StopAll(int waitMs)
        {
            List<PdfReader> all;
            lock (live) all = new List<PdfReader>(live);
            DateTime end = DateTime.UtcNow.AddMilliseconds(waitMs);
            foreach (PdfReader r in all) r.Dispose();
            foreach (PdfReader r in all)
            {
                int left = (int)Math.Max(0, (end - DateTime.UtcNow).TotalMilliseconds);
                try { r.thread.Join(left); } catch { }
            }
        }
    }
}
