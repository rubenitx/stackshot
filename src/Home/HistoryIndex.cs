// Stackshot - The capture history and its text index: the text of every image capture, read on this computer with the
// OCR that comes with Windows (Ocr.Read), so the Historial page finds a capture by what it said.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace Stackshot
{
    public enum HistoryKind { Image, Video, Gif }

    // A capture in the history: a file in the temporary folder, or in the save folder and its dated subfolders.
    public sealed class HistoryItem
    {
        public string Path, Name;
        public long Size, Ticks;            // length and last write (UTC ticks): with the path, what the index goes by
        public DateTime When;               // last write, local time
        public HistoryKind Kind;
        internal HistoryIndex.Entry Entry;  // its text, once read (images only)
        string foldName;

        public string Text { get { HistoryIndex.Entry e = Entry; return e != null ? e.Text : null; } }

        internal string FoldName
        {
            get
            {
                string f = foldName;
                if (f == null) foldName = f = HistoryIndex.Fold(Name ?? "");
                return f;
            }
        }

        internal string FoldText { get { HistoryIndex.Entry e = Entry; return e != null ? e.FoldText : null; } }
    }

    // A piece of a capture's text (or of its name) around the first match, and every match in it as (start, length).
    public sealed class HistorySnippet
    {
        public string Text = "";
        public bool FromName;
        public readonly List<KeyValuePair<int, int>> Marks = new List<KeyValuePair<int, int>>();
    }

    // The index lives in Settings.DataDir\historial\indice.gz: gzipped UTF-8, one line per image (path, size, last write,
    // state, text), always written whole to a temporary file first (AtomicFile); a file that can't be read back is rebuilt.
    // One thread at below-normal priority reads one image at a time, new captures first and then newest first, resting as
    // long as each read took; with nothing to read it waits on an event, with no timer. It pauses while a recording or a
    // scrolling capture runs and stops when the setting is turned off, which deletes the index.
    public static class HistoryIndex
    {
        internal sealed class Entry
        {
            public readonly string Path;
            public readonly long Size, Ticks;
            public readonly byte State;
            public readonly string Text;     // only when State is Read ("" for an image without text)
            string fold;

            public Entry(string path, long size, long ticks, byte state, string text)
            {
                Path = path;
                Size = size;
                Ticks = ticks;
                State = state;
                Text = text;
            }

            public bool Same(long size, long ticks) { return Size == size && Ticks == ticks; }

            // Folded once, by the first search that needs it.
            public string FoldText
            {
                get
                {
                    string f = fold;
                    if (f == null && Text != null) fold = f = Fold(Text);
                    return f;
                }
            }
        }

        internal const byte Read = 1, TooBig = 2, Failed = 3;   // Failed is never saved: it is tried again at the next start
        const string Header = "stackshot-historial 1";
        const string FileName = "indice.gz";
        static readonly string[] Images = { ".png", ".jpg", ".jpeg" };
        static readonly string[] Kinds = { ".png", ".jpg", ".jpeg", ".gif", ".mp4", ".mkv" };
        static readonly string[] GroupNamesEs = { "Hoy", "Ayer", "Esta semana", "Este mes", "Anteriores" };
        static readonly string[] GroupNamesEn = { "Today", "Yesterday", "This week", "This month", "Earlier" };
        public static string[] GroupNames { get { return I18n.IsSpanish ? GroupNamesEs : GroupNamesEn; } }

        public static string Dir = System.IO.Path.Combine(Settings.DataDir, "historial");   // tests point both elsewhere
        public static string TempFolder = Settings.TempDir;
        public static int Limit = 5000;                // newest captures in the history, and so in the index
        public static long MaxPixels = 40000000;       // bigger images are listed but not read
        public const int MaxText = 4000;               // characters kept per image
        const long MaxBytes = 400L << 20;              // a file this big is not even opened
        internal static int StartDelay = 20000;        // ms after startup before the first pass (opening the page starts it)
        internal static int FreshDelay = 4000;         // a new capture waits this long: the stack and the editor go first
        // The folders are listed again at least this often, even while new captures keep waking the thread: the text of a
        // deleted capture doesn't stay in the index for long.
        internal static int IdleRescan = 10 * 60 * 1000;
        const int SaveEvery = 30000;
        const int DeleteAttempts = 8;

        internal delegate string Recognizer(Bitmap image, out string error);
        internal static Recognizer Reader = Ocr.Read;  // tests read without the OCR
        internal static Func<bool> Busy = delegate { return Recorder.Recording || ScrollCapture.Active; };

        // Progress, a pause, new texts. Raised on the index thread (or on whoever deleted the index).
        public static event Action Changed;

        static readonly object gate = new object(), loadLock = new object(), fileLock = new object();
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> queue = new List<string>();
        static readonly List<KeyValuePair<string, DateTime>> fresh = new List<KeyValuePair<string, DateTime>>();   // and when each may be read
        static readonly AutoResetEvent wake = new AutoResetEvent(false);
        static Thread worker;
        static Settings live;
        static bool attached, exitHooked, dirty, enabled, scanDue = true;
        // Written under gate, read by the page without it (the UI thread never waits for the reading thread).
        static volatile bool loaded, paused, idle, unavailable, deleting;
        static volatile int version, passDone, passTotal;
        static int generation, passes;
        static DateTime lastSave = DateTime.UtcNow, attachedAt = DateTime.UtcNow;
        static readonly Stopwatch sinceScan = new Stopwatch();   // under gate; not running until the first scan

        static string IndexFile { get { return System.IO.Path.Combine(Dir, FileName); } }

        // There is an index on disk (General offers to delete it only then).
        public static bool Exists
        {
            get
            {
                if (deleting) return false;
                try { return File.Exists(IndexFile); }
                catch (Exception) { return false; }
            }
        }

        public static long Bytes
        {
            get
            {
                try
                {
                    FileInfo f = new FileInfo(IndexFile);
                    return f.Exists ? f.Length : 0;
                }
                catch (Exception) { return 0; }
            }
        }

        public static bool Unavailable { get { return unavailable; } }
        public static bool Paused { get { return paused; } }
        public static bool Loaded { get { return loaded; } }
        public static bool Running { get { lock (gate) return worker != null && enabled; } }
        internal static bool Idle { get { return idle; } }
        internal static bool Deleting { get { return deleting; } }
        internal static int Count { get { lock (gate) return entries.Count; } }
        internal static int Passes { get { lock (gate) return passes; } }   // times the thread ran out of work

        // Bumps whenever a text is added, changed or dropped.
        public static int Version { get { return version; } }

        // The current pass: images read, of how many (0 of 0 between passes).
        public static void Progress(out int done, out int total)
        {
            total = passTotal;
            done = Math.Min(passDone, total);
        }

        // ---- Life cycle

        // Called once by the stack. --test never builds or deletes the real index.
        public static void Attach(ShotStack owner, Settings s)
        {
            if (attached || ShotStack.Test || s == null) return;
            attached = true;
            attachedAt = DateTime.UtcNow;
            if (owner != null) owner.Captured += Captured;
            if (!exitHooked)
            {
                exitHooked = true;
                // What was read since the last save is kept when Stackshot closes (the second one finds nothing left to save).
                try
                {
                    System.Windows.Forms.Application.ApplicationExit += delegate { Flush(); };
                    AppDomain.CurrentDomain.ProcessExit += delegate { Flush(); };
                }
                catch (Exception) { }
            }
            Apply(s);
        }

        // At startup and when the setting changes: on starts reading, off stops and deletes the index.
        public static void Apply(Settings s)
        {
            if (s == null) return;
            lock (gate) live = s;
            if (!attached) return;
            if (s.HistorySearch) { Start(); return; }
            bool had;
            lock (gate) had = enabled || entries.Count > 0;
            Stop();
            if (had || Exists) Delete();
        }

        static void Start()
        {
            lock (gate)
            {
                enabled = true;
                scanDue = true;
                if (worker == null)
                {
                    unavailable = false;
                    Thread t = new Thread(Run);
                    t.IsBackground = true;
                    t.Priority = ThreadPriority.BelowNormal;
                    t.Name = "Historial";
                    t.SetApartmentState(ApartmentState.MTA);   // what the OCR's WinRT calls want
                    worker = t;
                    t.Start();
                    return;
                }
            }
            wake.Set();
        }

        static void Stop()
        {
            lock (gate)
            {
                enabled = false;
                queue.Clear();
                fresh.Clear();
                passDone = passTotal = 0;
                paused = false;
            }
            wake.Set();
        }

        // Forgets every text and deletes the file. While the setting is on, reading starts again from the beginning.
        public static void Delete()
        {
            lock (gate)
            {
                generation++;
                entries.Clear();
                queue.Clear();
                fresh.Clear();
                dirty = false;
                loaded = true;
                scanDue = true;
                passDone = passTotal = 0;
                version++;
                deleting = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (fileLock)
                {
                    try
                    {
                        // An antivirus or the search indexer may hold the file a moment after a save: tried again shortly.
                        for (int attempt = 1; ; attempt++)
                        {
                            try { DeleteFiles(); break; }
                            catch (Exception ex)
                            {
                                if (attempt >= DeleteAttempts) { ShotStack.Log("Historial: no se pudo borrar el \u00EDndice: " + ex.Message); break; }
                                Thread.Sleep(250);
                            }
                        }
                    }
                    finally { deleting = false; }
                }
                Raise();
            });
            wake.Set();
            Raise();
        }

        static void DeleteFiles()
        {
            if (!Directory.Exists(Dir)) return;
            foreach (string f in Directory.GetFiles(Dir))
            {
                string n = System.IO.Path.GetFileName(f);
                if (n == FileName || n.StartsWith(FileName + ".~", StringComparison.OrdinalIgnoreCase) || n.StartsWith("~", StringComparison.Ordinal)) File.Delete(f);
            }
            if (Directory.GetFileSystemEntries(Dir).Length == 0) Directory.Delete(Dir);
        }

        // The page opened or came back: the folders are listed again (a capture may have been kept, edited or deleted).
        public static void RequestScan()
        {
            lock (gate)
            {
                scanDue = true;
                if (worker == null) return;
            }
            wake.Set();
        }

        // A capture was just taken: it is read before anything else, a few seconds later.
        static void Captured(string path)
        {
            if (string.IsNullOrEmpty(path) || !IsImage(path)) return;
            lock (gate)
            {
                if (!enabled || unavailable) return;   // without OCR nothing would ever take it off the list
                int at = queue.FindIndex(delegate(string q) { return string.Equals(q, path, StringComparison.OrdinalIgnoreCase); });
                if (at >= 0) queue.RemoveAt(at);
                else if (fresh.RemoveAll(delegate(KeyValuePair<string, DateTime> f) { return string.Equals(f.Key, path, StringComparison.OrdinalIgnoreCase); }) == 0) passTotal++;
                fresh.Add(new KeyValuePair<string, DateTime>(path, DateTime.UtcNow.AddMilliseconds(FreshDelay)));
            }
            wake.Set();
        }

        internal static void NotifyCaptured(string path) { Captured(path); }

        // Keeps what was read since the last save (Stackshot is closing).
        internal static void Flush()
        {
            try { Save(); }
            catch (Exception ex) { ShotStack.Log("Historial: " + ex.Message); }
        }

        // ---- The reading thread

        static void Run()
        {
            // Right after startup Stackshot has better things to do; turned on later, it starts at once.
            double delay = StartDelay - (DateTime.UtcNow - attachedAt).TotalMilliseconds;
            if (delay > 0) wake.WaitOne((int)delay);
            while (true)
            {
                lock (gate)
                {
                    if (!enabled || worker != Thread.CurrentThread)
                    {
                        if (worker == Thread.CurrentThread) worker = null;
                        idle = false;
                        paused = false;
                        return;
                    }
                }
                try { Step(); }
                catch (Exception ex)
                {
                    // Whatever it was, the thread stays and tries again in a while.
                    ShotStack.Log("Historial: " + ex);
                    lock (gate)
                    {
                        idle = false;
                        scanDue = true;
                    }
                    wake.WaitOne(60000);
                }
            }
        }

        // One turn of the thread: a pause, a scan and one image, or waiting for work.
        static void Step()
        {
            bool scan;
            lock (gate)
            {
                scan = scanDue || !sinceScan.IsRunning || sinceScan.ElapsedMilliseconds >= IdleRescan;
                scanDue = false;
            }
            if (Busy())
            {
                lock (gate) scanDue |= scan;
                SetPaused(true);
                wake.WaitOne(2000);
                return;
            }
            SetPaused(false);
            EnsureLoaded();
            if (scan) Scan();
            string next = null;
            int wait = -1;
            lock (gate)
            {
                if (!unavailable && enabled)   // turned off during the scan: nothing more is read
                {
                    if (fresh.Count > 0)
                    {
                        double ms = (fresh[0].Value - DateTime.UtcNow).TotalMilliseconds;
                        if (ms <= 0) { next = fresh[0].Key; fresh.RemoveAt(0); }
                        else wait = (int)Math.Ceiling(ms);
                    }
                    else if (queue.Count > 0) { next = queue[0]; queue.RemoveAt(0); }
                }
                if (next == null && wait < 0) passDone = passTotal = 0;
            }
            if (next == null)
            {
                Save();
                if (wait < 0)
                {
                    lock (gate)
                    {
                        idle = true;
                        passes++;
                    }
                }
                Raise();
                int rescan;
                lock (gate) rescan = (int)Math.Max(1, IdleRescan - sinceScan.ElapsedMilliseconds);
                bool woke = wake.WaitOne(wait >= 0 ? Math.Min(wait, rescan) : rescan);
                lock (gate)
                {
                    idle = false;
                    if (!woke && wait < 0) scanDue = true;
                }
                return;
            }
            Stopwatch sw = Stopwatch.StartNew();
            ReadOne(next);
            if ((DateTime.UtcNow - lastSave).TotalMilliseconds >= SaveEvery) Save();
            // The OCR is heavy and the computer is in use: at least half of the time is left to everything else.
            wake.WaitOne((int)Math.Max(60, Math.Min(2000, sw.ElapsedMilliseconds)));
        }

        static void SetPaused(bool on)
        {
            lock (gate)
            {
                if (paused == on) return;
                paused = on;
            }
            Raise();
        }

        static void Raise()
        {
            Action h = Changed;
            if (h == null) return;
            try { h(); }
            catch (Exception ex) { ShotStack.Log("Historial: " + ex.Message); }
        }

        // One more image of the pass dealt with (under gate).
        static void Done()
        {
            passDone++;
            if (passDone > passTotal) passTotal = passDone;
        }

        // Lists the folders and lines up what needs reading: new and changed images, newest first. The kept copy of a
        // capture already read gets its text. Texts of files that are gone, moved, past the cap or listed under another
        // copy are dropped, except in a folder that could not be listed (a drive that isn't there right now).
        static void Scan()
        {
            Settings s;
            lock (gate)
            {
                s = live;
                sinceScan.Restart();
            }
            if (s == null) return;
            HashSet<string> failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<HistoryItem> found = List(s, failed);
            lock (gate)
            {
                HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Entry> twins = null;
                bool changed = false;
                queue.Clear();
                foreach (HistoryItem it in found)
                {
                    if (it.Kind != HistoryKind.Image) continue;
                    keep.Add(it.Path);
                    Entry e;
                    if (entries.TryGetValue(it.Path, out e) && e.Same(it.Size, it.Ticks)) continue;
                    if (twins == null) twins = Twins();
                    Entry twin;
                    if (twins.TryGetValue(TwinKey(it.Name, it.Size), out twin) && !string.Equals(twin.Path, it.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        entries[it.Path] = new Entry(it.Path, it.Size, it.Ticks, twin.State, twin.Text);
                        changed = true;
                        continue;
                    }
                    if (e != null) { entries.Remove(it.Path); changed = true; }
                    if (!InFresh(it.Path)) queue.Add(it.Path);
                }
                List<string> drop = new List<string>();
                foreach (Entry e in entries.Values)
                    if (!keep.Contains(e.Path) && !InFresh(e.Path) && !Under(e.Path, failed)) drop.Add(e.Path);
                foreach (string p in drop) entries.Remove(p);
                if (changed || drop.Count > 0)
                {
                    dirty = true;
                    version++;
                }
                passTotal = passDone + queue.Count + fresh.Count;
            }
            Raise();
        }

        static bool InFresh(string path)
        {
            foreach (KeyValuePair<string, DateTime> f in fresh)
                if (string.Equals(f.Key, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool Under(string path, HashSet<string> failed)
        {
            if (failed.Count == 0) return false;
            string dir;
            try { dir = System.IO.Path.GetDirectoryName(path); }
            catch (Exception) { return false; }
            if (string.IsNullOrEmpty(dir)) return false;
            dir = dir.TrimEnd('\\');
            foreach (string f in failed)
                if (string.Equals(dir, f, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(f.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Reads one image: its size first (a huge one is only listed), then its text. A file another program holds is left
        // for the next pass; one that is not an image, or that the OCR can't read, is tried again at the next start.
        static void ReadOne(string path)
        {
            ShotStack.WaitWritten(path);   // a capture just taken may still be on its way to the disk
            long size = 0, ticks = 0;
            bool exists;
            try
            {
                FileInfo fi = new FileInfo(path);
                exists = fi.Exists;
                if (exists)
                {
                    size = fi.Length;
                    ticks = fi.LastWriteTimeUtc.Ticks;
                }
            }
            catch (Exception) { exists = false; }
            int gen;
            lock (gate)
            {
                gen = generation;
                Entry e;
                if (!exists || size <= 0)
                {
                    if (entries.Remove(path))
                    {
                        dirty = true;
                        version++;
                    }
                    Done();
                    return;
                }
                if (entries.TryGetValue(path, out e) && e.Same(size, ticks)) { Done(); return; }
            }
            byte state = Failed;
            string text = null;
            try
            {
                if (size > MaxBytes) state = TooBig;
                else
                {
                    byte[] data;
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        data = new byte[fs.Length];
                        int got = 0;
                        while (got < data.Length)
                        {
                            int n = fs.Read(data, got, data.Length - got);
                            if (n <= 0) break;
                            got += n;
                        }
                    }
                    using (MemoryStream ms = new MemoryStream(data))
                    using (Image img = Image.FromStream(ms, false, false))
                    {
                        if ((long)img.Width * img.Height > MaxPixels) state = TooBig;
                        else
                        {
                            string error;
                            using (Bitmap b = new Bitmap(img)) text = Reader(b, out error);
                            if (text != null)
                            {
                                text = Clean(text);
                                state = Read;
                            }
                            else if (error == Ocr.Unavailable)
                            {
                                // Nothing to read with: the pass stops, and the history is searched by name only.
                                lock (gate) unavailable = true;
                                ShotStack.Log("Historial: este Windows no tiene OCR; solo se busca por el nombre");
                                Raise();
                                return;
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                lock (gate) Done();
                Raise();
                return;
            }
            catch (UnauthorizedAccessException)
            {
                lock (gate) Done();
                Raise();
                return;
            }
            catch (Exception ex) { ShotStack.Log("Historial: " + System.IO.Path.GetFileName(path) + ": " + ex.Message); }
            lock (gate)
            {
                if (gen != generation || !enabled) return;
                entries[path] = new Entry(path, size, ticks, state, text);
                if (state != Failed) dirty = true;
                version++;
                Done();
            }
            Raise();
        }

        static string Clean(string t)
        {
            return Cut(t.Replace("\r\n", "\n").Replace('\r', '\n').Trim());
        }

        static string Cut(string t)
        {
            if (t == null || t.Length <= MaxText) return t;
            int n = MaxText;
            if (char.IsHighSurrogate(t[n - 1])) n--;   // never half a character
            return t.Substring(0, n);
        }

        // ---- Listing

        static bool IsImage(string path)
        {
            string ext;
            try { ext = System.IO.Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            return Array.IndexOf(Images, ext) >= 0;
        }

        static HistoryKind KindOf(string ext)
        {
            if (ext == ".gif") return HistoryKind.Gif;
            if (ext == ".mp4" || ext == ".mkv") return HistoryKind.Video;
            return HistoryKind.Image;
        }

        static string Folder(string d)
        {
            string full = System.IO.Path.GetFullPath(d);
            return full.Length > 3 ? full.TrimEnd('\\') : full;
        }

        // Every capture in the folders, newest first, at most Limit. A kept capture that is still in the temporary folder
        // too (same name and size) is listed once, from the save folder. Folders that could not be listed go to failed.
        internal static List<HistoryItem> List(Settings s, HashSet<string> failed)
        {
            List<string> dirs = SaveFolders.All(s);
            if (!string.IsNullOrEmpty(TempFolder)) dirs.Add(TempFolder);
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase), names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<HistoryItem> r = new List<HistoryItem>();
            foreach (string d in dirs)
            {
                string full;
                try { full = Folder(d); }
                catch (Exception) { continue; }
                if (!done.Add(full)) continue;
                try
                {
                    if (!Directory.Exists(full))
                    {
                        // Not made yet, or on a drive that isn't there now: only the second keeps its texts.
                        string root = System.IO.Path.GetPathRoot(full);
                        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) failed.Add(full);
                        continue;
                    }
                    foreach (FileInfo f in new DirectoryInfo(full).EnumerateFiles())
                    {
                        HistoryItem it = new HistoryItem();
                        string ext;
                        try
                        {
                            it.Name = f.Name;
                            if (it.Name.StartsWith("~", StringComparison.Ordinal)) continue;
                            ext = f.Extension.ToLowerInvariant();
                            if (Array.IndexOf(Kinds, ext) < 0) continue;
                            it.Path = f.FullName;
                            it.Size = f.Length;
                            DateTime utc = f.LastWriteTimeUtc;
                            it.Ticks = utc.Ticks;
                            it.When = utc.ToLocalTime();
                        }
                        catch (Exception) { continue; }   // one odd file (a name too long) leaves the rest listed
                        if (it.Size <= 0 || !names.Add(it.Name + "|" + it.Size.ToString(CultureInfo.InvariantCulture))) continue;
                        it.Kind = KindOf(ext);
                        r.Add(it);
                    }
                }
                catch (Exception ex)
                {
                    failed.Add(full);
                    ShotStack.Log("Historial: " + full + ": " + ex.Message);
                }
            }
            r.Sort(delegate(HistoryItem a, HistoryItem b)
            {
                int c = b.Ticks.CompareTo(a.Ticks);
                return c != 0 ? c : string.CompareOrdinal(a.Path, b.Path);
            });
            if (r.Count > Limit) r.RemoveRange(Limit, r.Count - Limit);
            return r;
        }

        // The history for the page: the listing, with the texts already in memory (Load brings the rest). Blocks: never
        // on the UI thread.
        public static List<HistoryItem> Catalog(Settings s)
        {
            List<HistoryItem> r = List(s, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (s.HistorySearch && Loaded) Fill(r);
            return r;
        }

        // Reads the index from disk when it isn't in memory yet. Blocks: never on the UI thread.
        public static void Load() { EnsureLoaded(); }

        // Gives each image its current text: an edited capture has none until it is read again.
        public static void Fill(List<HistoryItem> items)
        {
            lock (gate)
            {
                Dictionary<string, Entry> twins = null;
                foreach (HistoryItem it in items)
                {
                    if (it.Kind != HistoryKind.Image) continue;
                    Entry e;
                    if (entries.TryGetValue(it.Path, out e) && e.Same(it.Size, it.Ticks))
                    {
                        it.Entry = e.Text != null ? e : null;
                        continue;
                    }
                    if (twins == null) twins = Twins();
                    Entry t;
                    it.Entry = twins.TryGetValue(TwinKey(it.Name, it.Size), out t) && t.Text != null ? t : null;
                }
            }
        }

        // The entries by file name and size (under gate): a kept copy and its temporary original are the same capture.
        static Dictionary<string, Entry> Twins()
        {
            Dictionary<string, Entry> d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry e in entries.Values)
                if (e.State != Failed) d[TwinKey(System.IO.Path.GetFileName(e.Path), e.Size)] = e;
            return d;
        }

        static string TwinKey(string name, long size) { return name + "|" + size.ToString(CultureInfo.InvariantCulture); }

        // ---- The file

        static void EnsureLoaded()
        {
            lock (gate) { if (loaded) return; }
            lock (loadLock)
            {
                int gen;
                lock (gate)
                {
                    if (loaded) return;
                    gen = generation;
                }
                bool broken, plain;
                List<Entry> read = LoadFile(IndexFile, out broken, out plain);
                lock (gate)
                {
                    if (loaded || gen != generation) return;
                    foreach (Entry e in read) if (!entries.ContainsKey(e.Path)) entries[e.Path] = e;
                    loaded = true;
                    if (broken || plain) dirty = true;   // written again, clean and sealed, at the next save
                    version++;
                }
                if (attached) CleanLeftovers();
            }
        }

        // Temporary files of a save that never finished (Stackshot was killed while writing).
        static void CleanLeftovers()
        {
            lock (fileLock)
            {
                try
                {
                    if (!Directory.Exists(Dir)) return;
                    foreach (string f in Directory.GetFiles(Dir, FileName + ".~*.tmp")) File.Delete(f);
                }
                catch (Exception) { }
            }
        }

        // The entries of an index file. A damaged one gives what could be read before the damage, one from another
        // version nothing; broken is true in both cases.
        internal static List<Entry> LoadFile(string path, out bool broken)
        {
            bool plain;
            return LoadFile(path, out broken, out plain);
        }

        // The text of every capture is private: the file is sealed with the user's Windows key (DPAPI), so another
        // account, or a copy of the file taken elsewhere, can't read it. plain: an older unsealed file, read as it is
        // and sealed at the next save.
        static readonly byte[] SealMark = Encoding.ASCII.GetBytes("SSHI1\n");
        static readonly byte[] SealEntropy = Encoding.ASCII.GetBytes("Stackshot.HistoryIndex.v1");

        static byte[] Seal(byte[] gz)
        {
            byte[] sealedData = System.Security.Cryptography.ProtectedData.Protect(gz, SealEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            byte[] r = new byte[SealMark.Length + sealedData.Length];
            Buffer.BlockCopy(SealMark, 0, r, 0, SealMark.Length);
            Buffer.BlockCopy(sealedData, 0, r, SealMark.Length, sealedData.Length);
            return r;
        }

        static bool IsSealed(byte[] b)
        {
            if (b.Length < SealMark.Length) return false;
            for (int i = 0; i < SealMark.Length; i++) if (b[i] != SealMark[i]) return false;
            return true;
        }

        internal static List<Entry> LoadFile(string path, out bool broken, out bool plain)
        {
            broken = false;
            plain = false;
            List<Entry> r = new List<Entry>();
            try
            {
                if (!File.Exists(path)) return r;
                byte[] bytes = File.ReadAllBytes(path);
                if (IsSealed(bytes))
                {
                    byte[] body = new byte[bytes.Length - SealMark.Length];
                    Buffer.BlockCopy(bytes, SealMark.Length, body, 0, body.Length);
                    try { bytes = System.Security.Cryptography.ProtectedData.Unprotect(body, SealEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser); }
                    catch (System.Security.Cryptography.CryptographicException ex)
                    {
                        broken = true;
                        ShotStack.Log("Historial: el \u00EDndice no se puede abrir en esta cuenta y se vuelve a crear (" + ex.Message + ")");
                        return r;
                    }
                }
                else plain = bytes.Length > 0;
                using (MemoryStream fs = new MemoryStream(bytes, false))
                using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                using (StreamReader rd = new StreamReader(gz, new UTF8Encoding(false, false)))
                {
                    string head = rd.ReadLine();
                    if (head != Header)
                    {
                        broken = true;
                        ShotStack.Log(head == null ? "Historial: el \u00EDndice estaba vac\u00EDo y se vuelve a crear" : "Historial: el \u00EDndice no es de esta versi\u00F3n y se vuelve a crear");
                        return r;
                    }
                    string line;
                    while ((line = rd.ReadLine()) != null)
                    {
                        Entry e = Parse(line);
                        if (e != null) r.Add(e);
                        else broken = true;
                    }
                }
            }
            catch (Exception ex)
            {
                broken = true;
                ShotStack.Log("Historial: el \u00EDndice estaba da\u00F1ado y se vuelve a crear (" + ex.Message + ")");
            }
            return r;
        }

        static Entry Parse(string line)
        {
            int a = line.IndexOf('\t');
            int b = a < 0 ? -1 : line.IndexOf('\t', a + 1);
            int c = b < 0 ? -1 : line.IndexOf('\t', b + 1);
            int d = c < 0 ? -1 : line.IndexOf('\t', c + 1);
            if (d < 0 || a == 0) return null;
            string path = line.Substring(0, a);
            long size, ticks;
            byte state;
            if (!long.TryParse(line.Substring(a + 1, b - a - 1), NumberStyles.None, CultureInfo.InvariantCulture, out size)) return null;
            if (!long.TryParse(line.Substring(b + 1, c - b - 1), NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) return null;
            if (!byte.TryParse(line.Substring(c + 1, d - c - 1), NumberStyles.None, CultureInfo.InvariantCulture, out state)) return null;
            if ((state != Read && state != TooBig) || ticks > DateTime.MaxValue.Ticks) return null;
            try
            {
                if (path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0 || !System.IO.Path.IsPathRooted(path)) return null;
            }
            catch (ArgumentException) { return null; }
            return new Entry(path, size, ticks, state, state == Read ? Cut(Unescape(line, d + 1)) : null);
        }

        static void Escape(StringBuilder sb, string s)
        {
            foreach (char ch in s)
            {
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch != '\r') sb.Append(ch);
            }
        }

        static string Unescape(string line, int from)
        {
            StringBuilder sb = new StringBuilder(Math.Max(0, line.Length - from));
            for (int i = from; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch != '\\' || i + 1 >= line.Length) { sb.Append(ch); continue; }
                char n = line[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
            }
            return sb.ToString();
        }

        static byte[] Serialize(List<Entry> list)
        {
            StringBuilder sb = new StringBuilder(64 + list.Count * 200);
            sb.Append(Header).Append('\n');
            foreach (Entry e in list)
            {
                if ((e.State != Read && e.State != TooBig) || e.Path.IndexOf('\t') >= 0 || e.Path.IndexOf('\n') >= 0) continue;
                sb.Append(e.Path).Append('\t').Append(e.Size.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(e.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(e.State.ToString(CultureInfo.InvariantCulture)).Append('\t');
                if (e.Text != null) Escape(sb, e.Text);
                sb.Append('\n');
            }
            byte[] raw = new UTF8Encoding(false).GetBytes(sb.ToString());
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true)) gz.Write(raw, 0, raw.Length);
                return Seal(ms.ToArray());
            }
        }

        // Writes the whole index when something changed. A delete that came meanwhile wins: nothing is written back.
        static void Save()
        {
            DropGoneTemp();
            List<Entry> snap;
            int gen;
            lock (gate)
            {
                if (!dirty || !enabled || !loaded) return;
                snap = new List<Entry>(entries.Values);
                gen = generation;
                dirty = false;
                lastSave = DateTime.UtcNow;
            }
            try
            {
                byte[] data = Serialize(snap);
                lock (fileLock)
                {
                    lock (gate) { if (gen != generation || !enabled) return; }
                    Directory.CreateDirectory(Dir);
                    AtomicFile.Write(IndexFile, data);
                }
            }
            catch (Exception ex)
            {
                ShotStack.Log("Historial: no se pudo guardar el \u00EDndice: " + ex.Message);
                lock (gate) { if (gen == generation) dirty = true; }
            }
        }

        // The texts of temporary captures that are gone (swept, or Borrar recientes) are dropped before every save, so they
        // never reach the file: not even at exit, right after the last sweep. One listing of a local folder.
        static void DropGoneTemp()
        {
            string temp;
            try { temp = string.IsNullOrEmpty(TempFolder) ? null : Folder(TempFolder); }
            catch (Exception) { return; }
            if (temp == null) return;
            List<Entry> mine = null;
            lock (gate)
            {
                if (!enabled || !loaded) return;
                foreach (Entry e in entries.Values)
                {
                    string dir;
                    try { dir = System.IO.Path.GetDirectoryName(e.Path); }
                    catch (Exception) { continue; }
                    if (dir == null || !string.Equals(dir.TrimEnd('\\'), temp, StringComparison.OrdinalIgnoreCase)) continue;
                    if (mine == null) mine = new List<Entry>();
                    mine.Add(e);
                }
            }
            if (mine == null) return;
            HashSet<string> there = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Directory.Exists(temp)) foreach (string f in Directory.GetFiles(temp)) there.Add(System.IO.Path.GetFileName(f));
            }
            catch (Exception) { return; }
            lock (gate)
            {
                bool changed = false;
                foreach (Entry e in mine)
                {
                    Entry now;
                    if (there.Contains(System.IO.Path.GetFileName(e.Path)) || InFresh(e.Path) || !entries.TryGetValue(e.Path, out now) || now != e) continue;
                    entries.Remove(e.Path);
                    changed = true;
                }
                if (changed)
                {
                    dirty = true;
                    version++;
                }
            }
        }

        // ---- Searching

        static readonly char[] folds = new char[65536];   // 0 until worked out
        static readonly char[] Blanks = { ' ', '\t', '\n', '\r' };

        // Lower case without accents, one character for one: a position in the folded text is the same position in the
        // original, so a match found in one is highlighted in the other.
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            char[] o = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i], f;
                if (c < 128) f = c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
                else
                {
                    f = folds[c];
                    if (f == '\0') folds[c] = f = FoldChar(c);
                }
                if (f == c) continue;
                if (o == null) o = s.ToCharArray();
                o[i] = f;
            }
            return o == null ? s : new string(o);
        }

        static char FoldChar(char c)
        {
            if (char.IsSurrogate(c)) return c;
            if (char.IsWhiteSpace(c)) return ' ';
            if ((c >= '\u2010' && c <= '\u2015') || c == '\u2212') return '-';
            if (c == '\u2018' || c == '\u2019' || c == '\u201A' || c == '\u201B') return '\'';
            if (c == '\u201C' || c == '\u201D' || c == '\u201E') return '"';
            char l = char.ToLowerInvariant(c);
            try
            {
                string d = l.ToString().Normalize(NormalizationForm.FormD);
                if (d.Length > 1 && !char.IsSurrogate(d[0]))
                {
                    bool marks = true;
                    for (int j = 1; j < d.Length; j++)
                        if (CharUnicodeInfo.GetUnicodeCategory(d[j]) != UnicodeCategory.NonSpacingMark) marks = false;
                    if (marks) l = d[0];
                }
            }
            catch (ArgumentException) { }
            return l == '\0' ? c : l;
        }

        // The words of a search, folded. Every one must be found, in the text or in the name.
        public static string[] Words(string query)
        {
            if (string.IsNullOrEmpty(query)) return new string[0];
            List<string> r = new List<string>();
            foreach (string w in Fold(query).Split(Blanks, StringSplitOptions.RemoveEmptyEntries))
                if (!r.Contains(w)) r.Add(w);
            return r.ToArray();
        }

        // filter: 0 everything, 1 images, 2 videos, 3 GIFs. The order is kept (newest first).
        public static List<HistoryItem> Search(List<HistoryItem> items, string query, int filter)
        {
            string[] words = Words(query);
            List<HistoryItem> r = new List<HistoryItem>();
            foreach (HistoryItem it in items)
            {
                if ((filter == 1 && it.Kind != HistoryKind.Image) || (filter == 2 && it.Kind != HistoryKind.Video) || (filter == 3 && it.Kind != HistoryKind.Gif)) continue;
                if (words.Length > 0 && !Matches(it, words)) continue;
                r.Add(it);
            }
            return r;
        }

        public static bool Matches(HistoryItem it, string[] words)
        {
            string n = it.FoldName, t = it.FoldText;
            foreach (string w in words)
                if (n.IndexOf(w, StringComparison.Ordinal) < 0 && (t == null || t.IndexOf(w, StringComparison.Ordinal) < 0)) return false;
            return true;
        }

        // About room characters of the text around the first match, from the start of a word, with lines joined and blanks
        // collapsed; or the name, when only the name matches. Every match is marked.
        public static HistorySnippet Snippet(HistoryItem it, string[] words, int room)
        {
            HistorySnippet sn = new HistorySnippet();
            string text = it.Text, fold = it.FoldText;
            int at = -1;
            if (fold != null && text != null && fold.Length == text.Length)
                foreach (string w in words)
                {
                    int p = fold.IndexOf(w, StringComparison.Ordinal);
                    if (p >= 0 && (at < 0 || p < at)) at = p;
                }
            if (at < 0)
            {
                sn.FromName = true;
                sn.Text = it.Name ?? "";
                Mark(sn, it.FoldName, 0, sn.Text.Length, words, null, sn.Text.Length);
                return sn;
            }
            int start = Math.Max(0, at - room / 4);
            int back = start;
            while (back > 0 && back > start - 16 && !char.IsWhiteSpace(text[back - 1])) back--;
            if (back == 0 || char.IsWhiteSpace(text[back - 1])) start = back;
            while (start < at && char.IsWhiteSpace(text[start])) start++;
            int end = Math.Min(text.Length, start + room);
            int fwd = end;
            while (fwd < text.Length && fwd < end + 16 && !char.IsWhiteSpace(text[fwd])) fwd++;
            if (fwd == text.Length || char.IsWhiteSpace(text[fwd])) end = fwd;
            StringBuilder sb = new StringBuilder(end - start + 2);
            int[] map = new int[end - start + 1];
            if (start > 0) sb.Append('\u2026');
            for (int i = start; i < end; i++)
            {
                map[i - start] = sb.Length;
                char ch = text[i];
                if (!char.IsWhiteSpace(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
            }
            map[end - start] = sb.Length;
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            int body = sb.Length;
            if (end < text.Length) sb.Append('\u2026');
            sn.Text = sb.ToString();
            Mark(sn, fold, start, end, words, map, body);
            return sn;
        }

        static void Mark(HistorySnippet sn, string fold, int start, int end, string[] words, int[] map, int limit)
        {
            List<KeyValuePair<int, int>> raw = new List<KeyValuePair<int, int>>();
            foreach (string w in words)
            {
                if (w.Length == 0 || start >= fold.Length) continue;
                int p = fold.IndexOf(w, start, StringComparison.Ordinal);
                while (p >= 0 && p < end)
                {
                    int q = Math.Min(end, p + w.Length);
                    int s0 = map == null ? p : map[p - start], s1 = map == null ? q : map[q - start];
                    s0 = Math.Min(s0, limit);
                    s1 = Math.Min(s1, limit);
                    if (s1 > s0) raw.Add(new KeyValuePair<int, int>(s0, s1 - s0));
                    if (p + w.Length >= fold.Length) break;
                    p = fold.IndexOf(w, p + w.Length, StringComparison.Ordinal);
                }
            }
            raw.Sort(delegate(KeyValuePair<int, int> a, KeyValuePair<int, int> b) { return a.Key.CompareTo(b.Key); });
            foreach (KeyValuePair<int, int> m in raw)
            {
                int n = sn.Marks.Count;
                if (n > 0 && m.Key <= sn.Marks[n - 1].Key + sn.Marks[n - 1].Value)
                {
                    KeyValuePair<int, int> last = sn.Marks[n - 1];
                    int stop = Math.Max(last.Key + last.Value, m.Key + m.Value);
                    sn.Marks[n - 1] = new KeyValuePair<int, int>(last.Key, stop - last.Key);
                }
                else sn.Marks.Add(m);
            }
        }

        // 0 today, 1 yesterday, 2 earlier this week (from Monday), 3 earlier this month, 4 before.
        public static int GroupOf(DateTime when, DateTime now)
        {
            DateTime today = now.Date, d = when.Date;
            if (d >= today) return 0;
            if (d == today.AddDays(-1)) return 1;
            if (d >= today.AddDays(-(((int)today.DayOfWeek + 6) % 7))) return 2;
            if (d >= new DateTime(today.Year, today.Month, 1)) return 3;
            return 4;
        }

        // ---- Tests

        internal static void ResetForTests()
        {
            Thread t;
            lock (gate)
            {
                enabled = false;
                t = worker;
            }
            wake.Set();
            if (t != null) t.Join(15000);
            while (deleting) Thread.Sleep(10);
            lock (gate)
            {
                worker = null;
                entries.Clear();
                queue.Clear();
                fresh.Clear();
                loaded = dirty = attached = paused = idle = unavailable = false;
                scanDue = true;
                passDone = passTotal = passes = 0;
                generation++;
                version++;
                live = null;
                sinceScan.Reset();
            }
        }

        internal static Dictionary<string, string> TextsForTests()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            lock (gate) foreach (Entry e in entries.Values) d[e.Path] = e.State == Read ? e.Text : e.State == TooBig ? "<grande>" : "<fallo>";
            return d;
        }
    }
}
