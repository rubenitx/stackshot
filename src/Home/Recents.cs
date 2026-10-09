// Stackshot - Recent captures for the main window: latest files, counts per day, folder size and cached thumbnails.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using D = System.Drawing;

namespace Stackshot
{
    // Reads the save folder and the temporary captures. Listing is cheap and done on demand (one listing serves a whole
    // page build); thumbnails and the folder size are computed on the thread pool and cached, so the window never waits
    // on the disk. Listing may be called from any thread; the rest from the UI thread.
    public static class Recents
    {
        static readonly string[] Kinds = { ".png", ".jpg", ".jpeg", ".gif", ".mp4" };
        static readonly Dictionary<string, BitmapSource> thumbs = new Dictionary<string, BitmapSource>();
        static readonly Dictionary<string, List<Action<BitmapSource>>> waiting = new Dictionary<string, List<Action<BitmapSource>>>();
        static readonly HashSet<string> unreadable = new HashSet<string>();   // files that gave no thumbnail (until they change)
        public static string TempFolder = Settings.TempDir;   // tools point it elsewhere

        static readonly object listLock = new object();
        static List<FileInfo> listed;
        static string listedFor;
        static int listedAt;
        const int ListFresh = 400;    // ms a listing is reused: Home asks several times while it builds

        static long folderBytes = -1;
        static string folderDir;
        static DateTime folderAt;
        static bool measuring;
        static readonly List<Action> folderWaiting = new List<Action>();

        public static List<FileInfo> Latest(Settings s, int n)
        {
            List<FileInfo> all = Files(s);
            all.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
            if (all.Count > n) all.RemoveRange(n, all.Count - n);
            return all;
        }

        // Captures per day for the last `days` days, oldest first (today last).
        public static int[] PerDay(Settings s, int days)
        {
            int[] r = new int[days];
            DateTime today = DateTime.Today;
            foreach (FileInfo f in Files(s))
            {
                int ago = (int)(today - f.LastWriteTime.Date).TotalDays;
                if (ago >= 0 && ago < days) r[days - 1 - ago]++;
            }
            return r;
        }

        // The next listing reads the disk again (a capture was just written).
        public static void Forget()
        {
            lock (listLock) listed = null;
        }

        // A copy of the current listing; the disk is read again once it is a moment old or the folders changed.
        static List<FileInfo> Files(Settings s)
        {
            string key = s.SaveFolder + "|" + TempFolder;
            lock (listLock)
            {
                if (listed != null && listedFor == key && unchecked(Environment.TickCount - listedAt) < ListFresh) return new List<FileInfo>(listed);
            }
            List<FileInfo> r = new List<FileInfo>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in new string[] { s.SaveFolder, TempFolder })
            {
                try
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                    foreach (FileInfo f in new DirectoryInfo(dir).GetFiles())
                    {
                        if (f.Name.StartsWith("~", StringComparison.Ordinal) || Array.IndexOf(Kinds, f.Extension.ToLowerInvariant()) < 0) continue;
                        if (!names.Add(f.Name + "|" + f.Length)) continue; // a kept capture also sits in the temp folder
                        r.Add(f);
                    }
                }
                catch (Exception ex) { ShotStack.Log("Recientes: " + ex.Message); }
            }
            lock (listLock)
            {
                listed = r;
                listedFor = key;
                listedAt = Environment.TickCount;
            }
            return new List<FileInfo>(r);
        }

        // Bytes in the save folder; -1 until first measured. Re-measured at most once a minute, or at once for another
        // folder. Every caller waiting on a measurement hears when it lands (a page rebuilt meanwhile included).
        public static long FolderBytes(Settings s, Action changed)
        {
            return FolderBytes(s.SaveFolder ?? "", changed);
        }

        static long FolderBytes(string dir, Action changed)
        {
            bool other = !string.Equals(dir, folderDir, StringComparison.OrdinalIgnoreCase);
            if (other) { folderDir = dir; folderBytes = -1; }
            if (folderBytes < 0 || (DateTime.Now - folderAt).TotalSeconds > 60)
            {
                if (changed != null) folderWaiting.Add(changed);
                if (!measuring)
                {
                    measuring = true;
                    folderAt = DateTime.Now;
                    System.Windows.Threading.Dispatcher ui = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        long total = Measure(dir);
                        ui.BeginInvoke((Action)delegate
                        {
                            measuring = false;
                            List<Action> tell = new List<Action>(folderWaiting);
                            folderWaiting.Clear();
                            if (!string.Equals(dir, folderDir, StringComparison.OrdinalIgnoreCase))
                            {
                                // The folder changed while measuring: measure the new one for the same callers.
                                folderBytes = -1;
                                foreach (Action a in tell) FolderBytes(folderDir, a);
                                return;
                            }
                            bool news = folderBytes != total;
                            folderBytes = total;
                            folderAt = DateTime.Now;
                            if (news) foreach (Action a in tell) a();
                        });
                    });
                }
            }
            return folderBytes;
        }

        // Bytes under a folder. A subfolder that can't be read (Documents keeps protected junctions, a path may be too
        // long) is left out instead of losing the whole total; the depth is bounded so a link loop ends.
        static long Measure(string root)
        {
            long total = 0;
            if (string.IsNullOrEmpty(root)) return 0;
            Stack<KeyValuePair<string, int>> todo = new Stack<KeyValuePair<string, int>>();
            todo.Push(new KeyValuePair<string, int>(root, 0));
            while (todo.Count > 0)
            {
                KeyValuePair<string, int> d = todo.Pop();
                try
                {
                    DirectoryInfo di = new DirectoryInfo(d.Key);
                    foreach (FileInfo f in di.EnumerateFiles()) total += f.Length;
                    if (d.Value < 24) foreach (DirectoryInfo sub in di.EnumerateDirectories()) todo.Push(new KeyValuePair<string, int>(sub.FullName, d.Value + 1));
                }
                catch (Exception) { }
            }
            return total;
        }

        public static string Size(long bytes)
        {
            if (bytes < 0) return "\u2026";
            if (bytes == 0) return "Vac\u00EDa";
            if (bytes < 1048576) return Math.Max(1, (int)Math.Round(bytes / 1024.0)) + " KB";
            double mb = bytes / 1048576.0;
            return mb >= 1024 ? (mb / 1024).ToString("0.0") + " GB" : mb.ToString(mb < 10 ? "0.0" : "0") + " MB";
        }

        // Thumbnail of a capture at px (device pixels) on its long side; ready is called on the UI thread.
        public static void Thumb(string path, int px, Action<BitmapSource> ready)
        {
            string key;
            try { key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|" + px; }
            catch { return; }
            BitmapSource b;
            if (thumbs.TryGetValue(key, out b)) { ready(b); return; }
            if (unreadable.Contains(key)) return;
            List<Action<BitmapSource>> list;
            if (waiting.TryGetValue(key, out list)) { list.Add(ready); return; }
            waiting[key] = new List<Action<BitmapSource>> { ready };
            System.Windows.Threading.Dispatcher ui = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ThreadPool.QueueUserWorkItem(delegate
            {
                D.Bitmap bmp = null;
                try
                {
                    if (ShotStack.IsMediaFile(path)) bmp = ShotStack.ShellThumb(path, px);
                    else { D.Size orig; bmp = ShotStack.LoadPreview(path, px, out orig); }
                }
                catch (Exception ex) { ShotStack.Log("Miniatura: " + ex.Message); }
                ui.BeginInvoke((Action)delegate
                {
                    BitmapSource src = null;
                    if (bmp != null) { try { src = Ink.FromGdi(bmp); } catch { } bmp.Dispose(); }
                    if (thumbs.Count > 200) thumbs.Clear();
                    if (src != null) thumbs[key] = src;
                    else
                    {
                        // Don't decode a broken file on every rebuild; a new version of it gets a new key.
                        if (unreadable.Count > 500) unreadable.Clear();
                        unreadable.Add(key);
                    }
                    List<Action<BitmapSource>> ws;
                    if (!waiting.TryGetValue(key, out ws)) return;
                    waiting.Remove(key);
                    if (src != null) foreach (Action<BitmapSource> w in ws) w(src);
                });
            });
        }
    }
}
