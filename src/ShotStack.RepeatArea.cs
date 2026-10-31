// Stackshot - What a capture keeps from the moment it started (the pointer, the window in front), the name of its file,
// the last area chosen (captured again by "Repetir la última área") and where kept captures go (dated subfolders).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        // ---- The moment a capture starts

        // What a capture keeps from the moment it started: the program in front and its title (only when the file name
        // template uses them) and the mouse pointer (only with "Incluir el puntero"). Nothing is read when neither is wanted.
        public sealed class CaptureStart : IDisposable
        {
            public string App = "", Window = "";
            public PointerShot Pointer;

            public static CaptureStart Take(Settings s)
            {
                CaptureStart c = new CaptureStart();
                c.ReadForeground(s.FileNameTemplate);
                if (s.CaptureCursor)
                {
                    try { c.Pointer = PointerShot.Take(); }
                    catch (Exception ex) { Log("Puntero: " + ex.Message); }
                }
                return c;
            }

            // The program and the title of the window in front, as far as the template asks for them.
            internal void ReadForeground(string template)
            {
                bool app = NameTemplate.Uses(template, "{app}"), title = NameTemplate.Uses(template, "{ventana}");
                if (!app && !title) return;
                IntPtr h = Native.GetForegroundWindow();
                if (h == IntPtr.Zero) return;
                if (app) App = Grabber.ProcessName(h) ?? "";
                if (title) Window = WindowTitle(h);
            }

            // Into a capture whose top-left pixel sits at screen point origin (before a window's corners are rounded).
            public void DrawPointer(Bitmap shot, Point origin)
            {
                if (Pointer == null || shot == null) return;
                try { Pointer.DrawInto(shot, origin); }
                catch (Exception ex) { Log("Puntero: " + ex.Message); }
            }

            public void Dispose()
            {
                if (Pointer == null) return;
                Pointer.Dispose();
                Pointer = null;
            }
        }

        CaptureStart captureStart;

        // The first step of a capture the user started (hotkey, tray, Home); OnHotkey ends it when the capture is done.
        CaptureStart BeginCapture()
        {
            EndCapture();
            captureStart = CaptureStart.Take(settings);
            return captureStart;
        }

        void EndCapture()
        {
            if (captureStart == null) return;
            captureStart.Dispose();
            captureStart = null;
        }

        // Another process's caption is read without sending it a message, so a hung window can't hold this up.
        static string WindowTitle(IntPtr h)
        {
            try
            {
                StringBuilder sb = new StringBuilder(512);
                return GetWindowText(h, sb, sb.Capacity) > 0 ? sb.ToString() : "";
            }
            catch (Exception) { return ""; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);

        // ---- File names

        // When each capture of this session was taken, for its dated subfolder.
        readonly Dictionary<string, DateTime> takenAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        string NewCapturePath(string name, CaptureStart start)
        {
            DateTime now = DateTime.Now;
            string path = CapturePath(settings, folder, name, start, now);
            if (takenAt.Count > 2000) takenAt.Clear();
            takenAt[path] = now;
            return path;
        }

        // Where a new capture goes in the temporary folder. The default template keeps the names Stackshot always gave,
        // byte for byte; any other one is filled in, and its {n} goes past the numbers already used that day here and in
        // the folder kept captures go to. A capture not started by the user (scrolling, a video frame) takes the window in
        // front now.
        internal static string CapturePath(Settings s, string folder, string name, CaptureStart start, DateTime now)
        {
            string t = s.FileNameTemplate;
            if (NameTemplate.IsDefault(t)) return Unique(Path.Combine(folder, Clean(name) + " " + now.ToString("yyyy-MM-dd HH.mm.ss") + ".png"));
            NameTemplate.Values v = new NameTemplate.Values();
            v.Type = name;
            v.When = now;
            if (start == null)
            {
                start = new CaptureStart();
                start.ReadForeground(t);
            }
            v.App = start.App;
            // With "Tapar los datos sensibles" on, what the capture covers doesn't show in its name either.
            v.Window = CoversCaptures(s) ? Sensitive.Scrub(start.Window) : start.Window;
            if (!NameTemplate.UsesCounter(t)) return Unique(Path.Combine(folder, NameTemplate.Format(t, v, 1) + ".png"));
            string keep = SaveFolders.Target(s, now);
            return LocalFolder(keep) ? CountedPath(t, v, folder, keep) : CountedPathBounded(t, v, folder, keep);
        }

        // The first free name for {n} in folder, past the numbers used there and in keep (left out when null).
        static string CountedPath(string t, NameTemplate.Values v, string folder, string keep)
        {
            long n = NameTemplate.FirstCounter(t, v, ".png", folder, keep);
            string last = null;
            for (int tries = 0; ; tries++, n++)
            {
                string file = NameTemplate.Format(t, v, n) + ".png";
                string p = Path.Combine(folder, file);
                // A counter cut off with the end of a very long name changes nothing: " (2)" then, as without one.
                if (tries >= 1000 || file == last) return Unique(p);
                if (!Taken(p) && (keep == null || !File.Exists(Path.Combine(keep, file)))) return p;
                last = file;
            }
        }

        // A save folder on a network share may be slow or gone (a lost connection holds each call for many seconds): it is
        // read on a worker and waited for briefly. Without an answer the number only goes past those of the temporary
        // folder; a kept copy then gets " (2)" if its name is taken there.
        internal static string CountedPathBounded(string t, NameTemplate.Values v, string folder, string keep)
        {
            return FromSlowFolder(keep, delegate { return CountedPath(t, v, folder, keep); }) ?? CountedPath(t, v, folder, null);
        }

        const int SlowFolderWaitMs = 500;
        static readonly HashSet<string> slowFolderReads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // read() on a worker thread: its result, or null when it takes longer than SlowFolderWaitMs or a read of the same
        // folder is still waiting from before (that one is not asked again until it ends).
        internal static string FromSlowFolder(string dir, Func<string> read)
        {
            lock (slowFolderReads) if (!slowFolderReads.Add(dir ?? "")) return null;
            object gate = new object();
            bool finished = false;
            string result = null;
            System.Threading.Thread worker = new System.Threading.Thread(delegate()
            {
                string r = null;
                try { r = read(); }
                catch (Exception ex) { Log("Nombre de captura (" + dir + "): " + ex.Message); }
                finally
                {
                    lock (slowFolderReads) slowFolderReads.Remove(dir ?? "");
                    lock (gate)
                    {
                        result = r;
                        finished = true;
                        System.Threading.Monitor.PulseAll(gate);
                    }
                }
            });
            worker.IsBackground = true;
            worker.Name = "Stackshot carpeta de guardado";
            worker.Start();
            lock (gate)
            {
                if (!finished) System.Threading.Monitor.Wait(gate, SlowFolderWaitMs);
                if (finished) return result;
            }
            Log("Nombre de captura: " + dir + " no responde; el n\u00FAmero sigue solo a los de la carpeta temporal");
            return null;
        }

        // A folder on a local fixed disk, read at once; anything else (a share, a removable drive) is read with a limit.
        internal static bool LocalFolder(string dir)
        {
            try
            {
                string root = Path.GetPathRoot(dir);
                if (string.IsNullOrEmpty(root) || root.StartsWith("\\\\", StringComparison.Ordinal)) return false;
                return new DriveInfo(root).DriveType == DriveType.Fixed;
            }
            catch (Exception) { return false; }
        }

        // ---- Repetir la última área

        // The last area chosen in an area capture this session: where it was, the window it was (if one was clicked) and
        // the monitors it touched, with their bounds then.
        internal sealed class LastArea
        {
            public Rectangle Bounds;
            public IntPtr Window;
            public readonly List<KeyValuePair<string, Rectangle>> Monitors = new List<KeyValuePair<string, Rectangle>>();
        }

        LastArea lastArea;

        // Device name and bounds of every monitor now.
        static List<KeyValuePair<string, Rectangle>> CurrentMonitors()
        {
            List<KeyValuePair<string, Rectangle>> l = new List<KeyValuePair<string, Rectangle>>();
            foreach (Screen sc in Screen.AllScreens) l.Add(new KeyValuePair<string, Rectangle>(sc.DeviceName, sc.Bounds));
            return l;
        }

        internal static LastArea MakeArea(Rectangle r, IntPtr window, List<KeyValuePair<string, Rectangle>> monitors)
        {
            LastArea a = new LastArea();
            a.Bounds = r;
            a.Window = window;
            foreach (KeyValuePair<string, Rectangle> m in monitors)
                if (m.Value.IntersectsWith(r)) a.Monitors.Add(m);
            return a;
        }

        // Still the same place: every monitor it touched is there, under the same name and with the same bounds. A monitor
        // unplugged, moved or set to another resolution means the area may no longer show what it showed.
        internal static bool AreaFits(LastArea a, List<KeyValuePair<string, Rectangle>> monitors)
        {
            if (a == null || a.Bounds.Width < 1 || a.Bounds.Height < 1 || a.Monitors.Count == 0) return false;
            foreach (KeyValuePair<string, Rectangle> was in a.Monitors)
            {
                bool found = false;
                foreach (KeyValuePair<string, Rectangle> now in monitors)
                    if (now.Value == was.Value && string.Equals(now.Key, was.Key, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        void RememberArea(Rectangle r, IntPtr window)
        {
            lastArea = MakeArea(r, window, CurrentMonitors());
        }

        // The same screen rectangle again, at once. With no area yet, or one that no longer fits the monitors, the normal
        // picker opens instead.
        void CaptureRepeat()
        {
            LastArea a = lastArea;
            if (!AreaFits(a, CurrentMonitors())) { CaptureRegion(); return; }
            picking = true;
            CaptureStart start = BeginCapture();
            Rectangle r = a.Bounds;
            Bitmap shot = Grabber.Grab(r);
            start.DrawPointer(shot, r.Location);
            string name = I18n.T("Captura", "Capture");
            // The window clicked last time, still exactly there: its name and its rounded corners, as then.
            if (a.Window != IntPtr.Zero && Grabber.Bounds(a.Window) == r)
            {
                name = Grabber.ProcessName(a.Window) ?? name;
                if (WindowIsRounded(a.Window, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
            }
            SaveCapture(shot, name, start);
        }

        // ---- Captures

        // A capture that needs no start: the window in front when it is saved.
        public string SaveCapture(Bitmap shot, string name)
        {
            return SaveCapture(shot, name, null);
        }

        // ---- Where kept captures go

        // The save folder or, with dated subfolders, the one for the time the capture was taken. A capture kept before is
        // refreshed where its copy is, whatever Subcarpetas says now.
        string KeepFolderFor(string path)
        {
            string dest;
            if (path != null && kept.TryGetValue(path, out dest) && !string.IsNullOrEmpty(dest)) return Path.GetDirectoryName(dest);
            return settings.SaveSubfolder == 0 ? settings.SaveFolder : SaveFolders.Target(settings, CapturedAt(path));
        }

        // When a capture was taken: known for this session's; otherwise when its file was made (edits keep that), or now
        // for one not written yet.
        DateTime CapturedAt(string path)
        {
            DateTime t;
            if (path != null && takenAt.TryGetValue(path, out t)) return t;
            try
            {
                if (File.Exists(path))
                {
                    t = File.GetCreationTime(path);
                    if (t.Year >= 2000 && t <= DateTime.Now.AddDays(1)) return t;
                }
            }
            catch (Exception) { }
            return DateTime.Now;
        }

        // A file of that name in the save folder or any of its dated subfolders (listed once per caller, in dirs).
        bool KeptAnywhere(string name, ref List<string> dirs)
        {
            if (dirs == null) dirs = SaveFolders.All(settings);
            foreach (string d in dirs)
            {
                try { if (File.Exists(Path.Combine(d, name))) return true; }
                catch (Exception) { }
            }
            return false;
        }

        // A dated subfolder (2026-10, 2026\10) of that save folder.
        static bool InDatedFolder(string dir, string saveFolder)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(saveFolder)) return false;
            Settings probe = new Settings();
            probe.SaveFolder = saveFolder;
            return SaveFolders.Inside(probe, dir);
        }
    }
}
