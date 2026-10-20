// Stackshot - A text file of any size as lines: its encoding found from the bytes, the start of every line indexed in the
// background (a log of hundreds of MB opens at once), lines decoded only when shown, and the file followed as it grows.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Stackshot
{
    public sealed class TextFile : IDisposable
    {
        public const long WholeLimit = 24L * 1024 * 1024;   // up to here the text is kept whole (wrapping, JSON, tables)
        public const int LineCap = 20000;                     // characters of one line shown (a minified file is one line)

        public readonly string Path;
        public Encoding Enc { get; private set; }
        public string EncodingName { get; private set; }
        public string NewlineName { get; private set; }
        public long Length { get; private set; }
        public bool Done { get; private set; }               // the whole file is indexed

        readonly object gate = new object();
        readonly List<long> starts = new List<long>();       // byte offset of each line
        long indexed;                                         // bytes looked at so far
        int unit = 1;                                         // 2 for UTF-16
        bool bigEndian;
        int skip;                                             // BOM bytes
        FileStream fs;
        string whole;                                         // the text, when the file is small enough
        string[] lines;                                       // its lines
        Thread worker;
        volatile bool stop;
        volatile int gen;                                     // a reload starts a new one: the old worker stops
        int crlf, lf, cr;

        public event Action Grew;                             // more lines (or the file was replaced): on the indexing thread

        TextFile(string path) { Path = path; }

        // Opens and reads enough to show the start right away; the rest is indexed by a worker. Null with error when the
        // file can't be read.
        public static TextFile Open(string path, out string error)
        {
            error = null;
            TextFile t = new TextFile(path);
            try { t.Start(); }
            catch (Exception ex)
            {
                t.Dispose();
                error = ex is FileNotFoundException ? "No encuentro el archivo: " + path : ex is UnauthorizedAccessException ? "No tengo permiso para leer el archivo." : "No he podido leer el archivo: " + ex.Message;
                return null;
            }
            return t;
        }

        void Start()
        {
            fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.RandomAccess);
            Length = fs.Length;
            byte[] head = new byte[(int)Math.Min(Length, 64 * 1024)];
            ReadAt(0, head, head.Length);
            Detect(head);
            starts.Add(skip);
            indexed = skip;
            if (Length <= WholeLimit)
            {
                byte[] all = new byte[Length];
                ReadAt(0, all, (int)Length);
                whole = Enc.GetString(all, skip, all.Length - skip);
                SplitWhole();
                Done = true;
                return;
            }
            Index(Math.Min(Length, 4L * 1024 * 1024));
            int my = gen;
            worker = new Thread(delegate()
            {
                while (!stop && !Done && my == gen)
                {
                    long before = indexed;
                    Index(Math.Min(Length, indexed + 8L * 1024 * 1024));
                    // Nothing more could be read (the file got shorter, an odd byte of UTF-16 at its end): as far as it goes.
                    if (!Done && indexed == before) { Done = true; Newlines(); }
                    Fire();
                }
            });
            worker.IsBackground = true;
            worker.Priority = ThreadPriority.BelowNormal;
            worker.Name = "Stackshot: \u00EDndice de texto";
            worker.Start();
        }

        void Fire() { Action h = Grew; if (h != null) h(); }

        // ---- Encoding

        void Detect(byte[] b)
        {
            int n = b.Length;
            if (n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { Use(new UTF8Encoding(false), 3, 1, false, "UTF-8 con BOM"); return; }
            if (n >= 2 && b[0] == 0xFF && b[1] == 0xFE) { Use(new UnicodeEncoding(false, false), 2, 2, false, "UTF-16 LE"); return; }
            if (n >= 2 && b[0] == 0xFE && b[1] == 0xFF) { Use(new UnicodeEncoding(true, false), 2, 2, true, "UTF-16 BE"); return; }
            // No mark: UTF-16 shows as zero bytes on one side; otherwise UTF-8 if it decodes, else Windows-1252.
            int zeroEven = 0, zeroOdd = 0;
            for (int i = 0; i + 1 < Math.Min(n, 4096); i += 2) { if (b[i] == 0) zeroEven++; if (b[i + 1] == 0) zeroOdd++; }
            int pairs = Math.Max(1, Math.Min(n, 4096) / 2);
            if (zeroOdd > pairs * 0.3 && zeroEven < pairs * 0.05) { Use(new UnicodeEncoding(false, false), 0, 2, false, "UTF-16 LE"); return; }
            if (zeroEven > pairs * 0.3 && zeroOdd < pairs * 0.05) { Use(new UnicodeEncoding(true, false), 0, 2, true, "UTF-16 BE"); return; }
            if (Utf8(b)) { Use(new UTF8Encoding(false), 0, 1, false, "UTF-8"); return; }
            Use(Encoding.GetEncoding(1252), 0, 1, false, "Windows-1252");
        }

        void Use(Encoding e, int bom, int u, bool be, string name)
        {
            Enc = e;
            skip = bom;
            unit = u;
            bigEndian = be;
            EncodingName = name;
        }

        // Valid UTF-8, allowing the sample to end in the middle of a character.
        static bool Utf8(byte[] b)
        {
            int i = 0, n = b.Length;
            while (i < n)
            {
                byte c = b[i];
                int more = c < 0x80 ? 0 : (c & 0xE0) == 0xC0 ? 1 : (c & 0xF0) == 0xE0 ? 2 : (c & 0xF8) == 0xF0 ? 3 : -1;
                if (more < 0) return false;
                if (i + more >= n) return true;
                for (int k = 1; k <= more; k++) if ((b[i + k] & 0xC0) != 0x80) return false;
                i += more + 1;
            }
            return true;
        }

        // ---- Lines

        void SplitWhole()
        {
            List<string> o = new List<string>();
            int s = 0;
            for (int i = 0; i < whole.Length; i++)
            {
                char c = whole[i];
                if (c != '\n' && c != '\r') continue;
                o.Add(whole.Substring(s, i - s));
                if (c == '\r' && i + 1 < whole.Length && whole[i + 1] == '\n') { crlf++; i++; }
                else if (c == '\r') cr++;
                else lf++;
                s = i + 1;
            }
            o.Add(whole.Substring(s));
            lines = o.ToArray();
            Newlines();
        }

        void Newlines()
        {
            NewlineName = crlf + lf + cr == 0 ? "" : crlf >= lf && crlf >= cr ? (lf + cr > 0 ? "CRLF (mezclado)" : "CRLF") : lf >= cr ? (crlf + cr > 0 ? "LF (mezclado)" : "LF") : "CR";
        }

        // Finds the line breaks of bytes [indexed, to). For UTF-16 the units are looked at in pairs.
        void Index(long to)
        {
            const int Block = 1 << 20;
            byte[] buf = new byte[Block];
            long at = indexed;
            while (at < to && !stop)
            {
                int want = (int)Math.Min(Block, to - at);
                want -= want % unit;
                if (want <= 0) break;
                int got = ReadAt(at, buf, want);
                if (got <= 0) break;
                List<long> found = new List<long>();
                for (int i = 0; i + unit - 1 < got; i += unit)
                {
                    int ch = unit == 1 ? buf[i] : bigEndian ? (buf[i] << 8) | buf[i + 1] : buf[i] | (buf[i + 1] << 8);
                    if (ch == '\n') { found.Add(at + i + unit); if (Prev(buf, i, at) == '\r') crlf++; else lf++; }
                    else if (ch == '\r')
                    {
                        // A lone CR ends a line too; one followed by LF is handled at the LF.
                        int next = i + unit < got ? (unit == 1 ? buf[i + 1] : bigEndian ? (buf[i + unit] << 8) | buf[i + unit + 1] : buf[i + unit] | (buf[i + unit + 1] << 8)) : -1;
                        if (next != '\n' && next != -1) { found.Add(at + i + unit); cr++; }
                    }
                }
                lock (gate) { starts.AddRange(found); indexed = at + got; }
                at += got;
            }
            if (indexed >= Length) { Done = true; Newlines(); }
        }

        int Prev(byte[] buf, int i, long at)
        {
            if (i >= unit) return unit == 1 ? buf[i - 1] : bigEndian ? (buf[i - 2] << 8) | buf[i - 1] : buf[i - 2] | (buf[i - 1] << 8);
            if (at - unit < skip) return -1;
            byte[] one = new byte[unit];
            if (ReadAt(at - unit, one, unit) < unit) return -1;
            return unit == 1 ? one[0] : bigEndian ? (one[0] << 8) | one[1] : one[0] | (one[1] << 8);
        }

        int ReadAt(long pos, byte[] buf, int count)
        {
            lock (fs)
            {
                fs.Position = pos;
                int got = 0;
                while (got < count)
                {
                    int n = fs.Read(buf, got, count - got);
                    if (n <= 0) break;
                    got += n;
                }
                return got;
            }
        }

        public int Count
        {
            get
            {
                if (lines != null) return lines.Length;
                lock (gate) return Done ? starts.Count : Math.Max(1, starts.Count);
            }
        }

        public bool Whole { get { return whole != null; } }

        // The whole text (small files only), or null.
        public string Text { get { return whole; } }

        public string Line(int i)
        {
            if (lines != null) return i >= 0 && i < lines.Length ? lines[i] : "";
            long a, b;
            lock (gate)
            {
                if (i < 0 || i >= starts.Count) return "";
                a = starts[i];
                b = i + 1 < starts.Count ? starts[i + 1] : (Done ? Length : indexed);
            }
            int len = (int)Math.Min(b - a, (long)LineCap * 4);
            if (len <= 0) return "";
            byte[] buf = new byte[len];
            int got = ReadAt(a, buf, len);
            string s = Enc.GetString(buf, 0, got - got % unit);
            int end = s.Length;
            while (end > 0 && (s[end - 1] == '\n' || s[end - 1] == '\r')) end--;
            return end == s.Length ? s : s.Substring(0, end);
        }

        // ---- Following a file that grows (a log being written)

        // Looks again at the file: new lines are indexed (true when there are any); a file that got shorter was replaced
        // or rotated, and is read again from the start.
        public bool Refresh()
        {
            long now;
            try { now = new FileInfo(Path).Length; }
            catch { return false; }
            if (now == Length || (!Done && whole == null)) return false;
            if (now < Length || whole != null)
            {
                Reload();
                return true;
            }
            Length = now;
            Done = false;
            Index(now);
            return true;
        }

        void Reload()
        {
            gen++;
            if (worker != null) worker.Join(2000);
            lock (gate) { starts.Clear(); indexed = 0; }
            crlf = lf = cr = 0;
            whole = null;
            lines = null;
            Done = false;
            try { fs.Dispose(); } catch { }
            Start();
        }

        public void Dispose()
        {
            stop = true;
            try { if (fs != null) lock (fs) fs.Dispose(); } catch { }
        }
    }
}
