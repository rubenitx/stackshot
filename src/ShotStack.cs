// Stackshot - The stack: capture, thumbnails, clipboard, cleanup and tray icon.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Stackshot
{
    // Handles hotkeys, captures and stacks one thumbnail per capture (bottom-left, like CleanShot X). Only what the
    // user saves is kept; everything else lives in a temp folder and is cleaned up.
    public class ShotStack : ApplicationContext
    {
        public static string LogPath;
        public static Icon AppIcon;
        public static bool Test;           // --test: leaves clipboard and hotkeys alone
        const int MaxCards = 20;           // the rest are reachable with the mouse wheel
        const double FollowDelay = 350;    // ms the cursor must stay on another monitor before the stack follows it
        static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
        static readonly string[] MediaExts = { ".gif", ".mp4", ".webm", ".mkv", ".mov", ".avi" };

        readonly Settings settings;
        readonly string folder;
        readonly Dictionary<string, string> kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Timer sweepTimer = new Timer();
        readonly System.Threading.EventWaitHandle quitEvent;
        TrackedData clip;
        bool exiting, picking;
        readonly List<Card> cards = new List<Card>();
        readonly Dictionary<string, Editor> editors = new Dictionary<string, Editor>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> refresh = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly Control sync = new Control();
        readonly FileSystemWatcher fsw;
        readonly Timer poll = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Hotkeys hotkeys;
        readonly Timer follow = new Timer();
        readonly Dictionary<string, ToolStripMenuItem> actionItems = new Dictionary<string, ToolStripMenuItem>();
        string anchorDevice, followCandidate;
        double followSince;
        readonly Chip upChip, downChip;
        int scroll, wheelAcc, pageSize = 1;    // scroll: how many of the newest cards are hidden above

        public ShotStack(Settings settings, bool justInstalled, bool showHome)
        {
            this.settings = settings;
            folder = Settings.TempDir;
            sync.CreateControl();
            IntPtr forceHandle = sync.Handle;
            Directory.CreateDirectory(folder);
            upChip = new Chip(this, -1);
            downChip = new Chip(this, 1);

            // Watch the temp folder to refresh a card when its file changes (the editor rewrites it) and drop it if the
            // file is deleted or moved.
            fsw = new FileSystemWatcher(folder);
            fsw.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            fsw.SynchronizingObject = sync;
            fsw.Changed += OnChanged;
            fsw.Renamed += OnRenamed;
            fsw.Deleted += OnDeleted;
            fsw.Error += delegate(object o, ErrorEventArgs e) { Log("Vigilante: " + e.GetException().Message); };
            fsw.EnableRaisingEvents = true;

            poll.Interval = 150;
            poll.Tick += Poll;
            follow.Interval = 100;
            follow.Tick += FollowTick;

            tray.ContextMenuStrip = BuildMenu();
            tray.Icon = TrayIcon();
            tray.Text = Test ? "Stackshot (prueba)" : "Stackshot";
            tray.MouseClick += delegate(object o, MouseEventArgs e)
            {
                // Left click on the tray icon opens the same menu as right click.
                if (e.Button != MouseButtons.Left) return;
                MethodInfo show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (show != null) show.Invoke(tray, null);
            };
            // Double click opens the main window straight away (restored and in front), closing the menu the first click opened.
            tray.MouseDoubleClick += delegate(object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Close();
                ShowHome("home", false);
            };
            tray.BalloonTipClicked += delegate { if (updateBalloon) ShowHome("about", false); };
            tray.BalloonTipClosed += delegate { updateBalloon = false; };
            tray.Visible = true;
            if (!Test) closeListener = new CloseListener(delegate { sync.BeginInvoke((Action)ExitThread); });

            hotkeys = new Hotkeys();
            hotkeys.Pressed += OnHotkey;
            RegisterHotkeys(true);

            // Another instance (an update, the uninstaller) can ask this one to quit cleanly.
            if (!Test)
            {
                try
                {
                    quitEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.QuitEvent);
                    quitWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(quitEvent,
                        delegate { Ui(delegate { Log("Cierre pedido por otra copia"); ExitThread(); }); }, null, -1, true);
                }
                catch (Exception ex) { Log("Evento de salida: " + ex.Message); }
                // Launching Stackshot again (Start menu, the .exe) while running shows the window.
                try
                {
                    showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.ShowEvent);
                    showWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(showEvent,
                        delegate { Ui(delegate { ShowHome("home", true); }); }, null, -1, false);
                }
                catch (Exception ex) { Log("Evento de mostrar: " + ex.Message); }
            }

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            Log("Stackshot " + Installer.MyVersion.ToString(3) + " en marcha (" + Installer.ExePath + ")");

            // Test mode cleans nothing: the temp folder belongs to the real install.
            if (!Test)
            {
                sweepTimer.Interval = 5 * 60 * 1000;
                sweepTimer.Tick += delegate { Sweep(); };
                sweepTimer.Start();
                Sweep();
            }

            Backdrop.Prewarm();
            if (justInstalled || showHome) sync.BeginInvoke((Action)delegate { ShowHome("home", true); });
            sync.BeginInvoke((Action)MascotChanged);
            Updater.Start(this);
        }

        System.Threading.EventWaitHandle showEvent;
        System.Threading.RegisteredWaitHandle quitWait, showWait;
        CloseListener closeListener;
        PetWindow pet;
        bool homeShown;

        // ---- Mascot on the desktop and friendship points.

        // Look, visibility or desktop toggle changed: create, update or remove the desktop pet.
        public void MascotChanged()
        {
            if (exiting) return;
            bool want = settings.MascotDesktop && settings.MascotOn;
            if (want && pet == null)
            {
                pet = new PetWindow(this, settings);
                pet.ShowPet();
                pet.SetHomeShown(homeShown);
            }
            else if (!want && pet != null)
            {
                pet.Close();
                pet = null;
            }
            else if (pet != null) pet.LookChanged();
        }

        public void HomeShown(bool shown)
        {
            homeShown = shown;
            if (pet != null) pet.SetHomeShown(shown);
        }

        bool updateBalloon;

        public void NotifyUpdate(Updater.Release r)
        {
            updateBalloon = true;
            tray.BalloonTipTitle = "Stackshot " + r.Version.ToString(3) + " disponible";
            tray.BalloonTipText = Installer.ManagedByMsi ? "P\u00EDdesela a inform\u00E1tica o instala el nuevo Stackshot.msi."
                                                         : "Haz clic aqu\u00ED para ver las novedades y actualizar sin salir de la app.";
            tray.BalloonTipIcon = ToolTipIcon.Info;
            tray.ShowBalloonTip(8000);
        }

        // One point per capture; reaching a new level is celebrated.
        void AddLove()
        {
            int before = MascotParts.Level(settings.MascotLove);
            settings.MascotLove++;
            settings.Save();
            int after = MascotParts.Level(settings.MascotLove);
            if (pet != null) pet.Celebrate(after > before ? MascotTalk.LevelUp(settings, after) : null);
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = TrayMenu.Create();
            menu.Items.Add(TrayMenu.Item("Abrir Stackshot", "app", delegate { ShowHome("home", false); }));
            menu.Items.Add(TrayMenu.Separator());
            AddAction(menu, "region", "Capturar un \u00E1rea", "area");
            AddAction(menu, "screen", "Capturar la pantalla", "screen");
            AddAction(menu, "window", "Capturar la ventana activa", "window");
            AddAction(menu, "scroll", "Captura con desplazamiento", "scroll");
            menu.Items.Add(TrayMenu.Separator());
            AddAction(menu, "video", "Grabar v\u00EDdeo", "video");
            AddAction(menu, "gif", "Grabar GIF", "gif");
            menu.Items.Add(TrayMenu.Separator());
            menu.Items.Add(TrayMenu.Item("Cerrar todas las miniaturas", "stack", delegate { CloseAll(); }));
            menu.Items.Add(TrayMenu.Item("Abrir capturas guardadas", "folder", delegate { OpenFolder(); }));
            menu.Items.Add(TrayMenu.Item("Ajustes\u2026", "gear", delegate { ShowHome("general", false); }));
            menu.Items.Add(TrayMenu.Separator());
            menu.Items.Add(TrayMenu.Item("Salir", "power", delegate { ExitThread(); }));
            menu.Opening += delegate
            {
                bool rec = Recorder.Recording;
                actionItems["video"].Text = rec ? "Detener la grabaci\u00F3n" : "Grabar v\u00EDdeo";
                actionItems["video"].Tag = rec ? "stop" : "video";
                actionItems["gif"].Visible = !rec;
                actionItems["scroll"].Text = ScrollCapture.Active ? "Terminar la captura con desplazamiento" : "Captura con desplazamiento";
                foreach (KeyValuePair<string, ToolStripMenuItem> kv in actionItems)
                    kv.Value.ShortcutKeyDisplayString = Hotkeys.Split(settings.HotkeysFor(kv.Key)).Count > 0 ? Hotkeys.Display(settings.HotkeysFor(kv.Key)) : "";
            };
            return menu;
        }

        void AddAction(ContextMenuStrip menu, string action, string text, string icon)
        {
            ToolStripMenuItem item = TrayMenu.Item(text, icon, null);
            // Wait until the menu is fully closed so it doesn't appear in the capture.
            item.Click += delegate { Delay(160, delegate { OnHotkey(action == "video" && Recorder.Recording ? "video" : action); }); };
            actionItems[action] = item;
            menu.Items.Add(item);
        }

        void RegisterHotkeys(bool report)
        {
            if (Test) return;
            hotkeys.Clear();
            List<string> failed = new List<string>();
            foreach (string action in Settings.Actions)
            {
                foreach (string f in hotkeys.Register(action, settings.HotkeysFor(action))) failed.Add(Hotkeys.Display(f));
            }
            if (failed.Count == 0) return;
            Log("Atajos ocupados por otro programa: " + string.Join(", ", failed.ToArray()));
            if (!report) return;
            updateBalloon = false;
            tray.BalloonTipTitle = "Algunos atajos est\u00E1n ocupados";
            tray.BalloonTipText = string.Join(", ", failed.ToArray()) + " los usa otro programa (\u00BFOneDrive, ShareX, Lightshot, Greenshot\u2026?). " +
                                  "Ci\u00E9rralo o elige otros atajos en Ajustes.";
            tray.BalloonTipIcon = ToolTipIcon.Warning;
            tray.ShowBalloonTip(8000);
        }

        // A short notice from the tray icon.
        public void Notify(string title, string text)
        {
            updateBalloon = false;
            tray.BalloonTipTitle = title;
            tray.BalloonTipText = text;
            tray.BalloonTipIcon = ToolTipIcon.Info;
            tray.ShowBalloonTip(8000);
        }

        void OnHotkey(string action)
        {
            if (picking) return;
            try
            {
                switch (action)
                {
                    case "region": CaptureRegion(); break;
                    case "screen": CaptureScreen(); break;
                    case "window": CaptureWindow(); break;
                    case "scroll": picking = !ScrollCapture.Active; ScrollCapture.Toggle(this, settings); break;
                    case "video": picking = !Recorder.Recording; Recorder.Toggle(this, settings, false); break;
                    case "gif": picking = !Recorder.Recording; Recorder.Toggle(this, settings, true); break;
                }
            }
            catch (Exception ex) { Log("Captura (" + action + "): " + ex); }
            finally { picking = false; }
        }

        public Settings Settings { get { return settings; } }

        // Opens (or brings to front) the main window on that section. intro: play the launch animation.
        public void ShowHome(string page, bool intro)
        {
            if (home == null || home.IsDisposed)
            {
                home = new HomeWindow(this, settings);
                home.FormClosed += delegate { home = null; };
            }
            home.Present(page, intro && settings.ShowIntro);
        }
        HomeWindow home;

        // While a hotkey field is recording, global hotkeys must not swallow the keys.
        public void SuspendHotkeys() { if (!Test) hotkeys.Clear(); }
        public void ResumeHotkeys() { RegisterHotkeys(true); }

        // Action requested from the window (already hidden so it isn't captured).
        public void Run(string action)
        {
            Delay(220, delegate { OnHotkey(action == "video" && Recorder.Recording ? "video" : action); });
        }

        public void Quit() { ExitThread(); }

        public void ApplySettings()
        {
            if (settings.FollowMouse && cards.Count > 0) follow.Start();
            settings.Save();
        }

        void CaptureRegion()
        {
            picking = true;
            Bitmap frozen;
            Rectangle vs;
            IntPtr window;
            Rectangle r = RegionPicker.Pick(RegionPicker.Mode.Image, out frozen, out vs, out window);
            using (frozen)
            {
                if (r.IsEmpty) return;
                Rectangle src = new Rectangle(r.X - vs.X, r.Y - vs.Y, r.Width, r.Height);
                src.Intersect(new Rectangle(0, 0, frozen.Width, frozen.Height));
                if (src.Width < 1 || src.Height < 1) return;
                Bitmap shot = frozen.Clone(src, PixelFormat.Format32bppArgb);
                string name = "Captura";
                if (window != IntPtr.Zero)
                {
                    name = Grabber.ProcessName(window) ?? name;
                    if (WindowIsRounded(window, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
                }
                SaveCapture(shot, name);
            }
        }

        void CaptureScreen()
        {
            Screen scr = Grabber.CurrentScreen();
            SaveCapture(Grabber.Grab(scr.Bounds), "Pantalla");
        }

        void CaptureWindow()
        {
            IntPtr h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero) { CaptureScreen(); return; }
            Rectangle r = Grabber.Bounds(h);
            r.Intersect(SystemInformation.VirtualScreen);
            if (r.Width < 2 || r.Height < 2) { CaptureScreen(); return; }
            Bitmap shot = Grabber.Grab(r);
            if (WindowIsRounded(h, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
            SaveCapture(shot, Grabber.ProcessName(h) ?? "Ventana");
        }

        // On Windows 11, normal (non-maximized) windows have rounded corners.
        static bool WindowIsRounded(IntPtr h, Rectangle r)
        {
            if (Environment.OSVersion.Version.Build < 22000) return false;
            Screen scr = Screen.FromRectangle(r);
            return r != scr.Bounds && r != scr.WorkingArea;
        }

        // Stores the capture in the temp folder, copies it (if enabled) and adds a card.
        // To feel instant: the sound plays first, the card uses the in-memory image and the PNG (the slowest part,
        // hundreds of ms on large screens) is written on a worker thread. Callers that need the file wait with
        // WaitWritten.
        public void SaveCapture(Bitmap shot, string name)
        {
            if (settings.Sound) Shutter.Play();
            Directory.CreateDirectory(folder);
            string path = Unique(Path.Combine(folder, Clean(name) + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png"));
            Size size = shot.Size;
            Bitmap preview = Preview(shot, 600);
            Bitmap forClipboard = settings.CopyToClipboard && !Test ? TrackedData.DeepCopy(shot) : null;
            BeginWrite(path, shot);
            if (!AddCard(path, preview, size))
            {
                if (forClipboard != null) forClipboard.Dispose();
                return;
            }
            if (forClipboard != null)
            {
                try { CopyTracked(path, forClipboard, false, true); }
                catch (Exception ex) { Log("Portapapeles: " + ex.Message); }
            }
            Log("Captura: " + Path.GetFileName(path) + " (" + size.Width + "x" + size.Height + ")");
            if (!Test) AddLove();
            if (Captured != null) Captured(path);
        }

        // For the main window (the mascot celebrates each capture).
        public event Action<string> Captured;

        // Files being written in the background.
        static readonly HashSet<string> writing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Write under a temp name and rename when done, so nobody reads a half-written PNG and the folder watcher
        // doesn't mistake it for an edit.
        static void BeginWrite(string path, Bitmap bmp)
        {
            lock (writing) writing.Add(path);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string part = path + ".part";
                try
                {
                    bmp.Save(part, ImageFormat.Png);
                    File.Move(part, path);
                }
                catch (Exception ex)
                {
                    Log("Guardar " + path + ": " + ex.Message);
                    try { if (File.Exists(part)) File.Delete(part); } catch { }
                }
                finally
                {
                    bmp.Dispose();
                    lock (writing)
                    {
                        writing.Remove(path);
                        System.Threading.Monitor.PulseAll(writing);
                    }
                }
            });
        }

        // Waits (up to 10 s) until a fresh capture's PNG is on disk.
        public static void WaitWritten(string path)
        {
            if (path == null) return;
            DateTime until = DateTime.Now.AddSeconds(10);
            lock (writing)
            {
                while (writing.Contains(path))
                {
                    int left = (int)(until - DateTime.Now).TotalMilliseconds;
                    if (left <= 0) break;
                    System.Threading.Monitor.Wait(writing, left);
                }
            }
        }

        // Finished recording: add it to the stack and put the file on the clipboard (to paste into a chat).
        public void AddRecording(string path)
        {
            if (!AddCard(path)) return;
            if (settings.CopyToClipboard && !Test)
            {
                try
                {
                    StringCollection sc = new StringCollection();
                    sc.Add(path);
                    Clipboard.SetFileDropList(sc);
                }
                catch (Exception ex) { Log("Portapapeles: " + ex.Message); }
            }
            Log("Grabaci\u00F3n: " + Path.GetFileName(path));
        }

        static string Clean(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length > 40 ? name.Substring(0, 40) : name;
        }

        public static string Unique(string path)
        {
            if (!Taken(path)) return path;
            string dir = Path.GetDirectoryName(path), stem = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
            for (int n = 2; ; n++)
            {
                string p = Path.Combine(dir, stem + " (" + n + ")" + ext);
                if (!Taken(p)) return p;
            }
        }

        // Exists on disk or is reserved by a capture still being written.
        static bool Taken(string p)
        {
            if (File.Exists(p)) return true;
            lock (writing) return writing.Contains(p);
        }

        public void Pin(string path)
        {
            try { PinWindow.Open(LoadFull(path)); }
            catch (Exception ex) { Log("Fijar: " + ex.Message); }
        }

        // Runs on the UI thread (recording runs on another thread).
        public void Ui(Action a)
        {
            if (exiting) return;
            try { sync.BeginInvoke(a); }
            catch (Exception ex) { Log("Ui: " + ex.Message); }
        }

        void Delay(int ms, Action a)
        {
            Timer t = new Timer();
            t.Interval = ms;
            t.Tick += delegate { t.Stop(); t.Dispose(); a(); };
            t.Start();
        }

        // Copies the capture to the save folder (or refreshes the copy if already saved).
        public string Keep(string path)
        {
            WaitWritten(path);
            string keepFolder = settings.SaveFolder;
            Directory.CreateDirectory(keepFolder);
            string dest;
            if (!kept.TryGetValue(path, out dest))
            {
                dest = Unique(Path.Combine(keepFolder, Path.GetFileName(path)));
                kept[path] = dest;
            }
            File.Copy(path, dest, true);
            Card c = Find(path);
            if (c != null) c.MarkSaved(dest);
            Log("Guardada: " + dest);
            return dest;
        }

        public bool IsKept(string path)
        {
            return kept.ContainsKey(path);
        }

        // Permanently deletes temp captures older than one hour that are no longer on screen.
        // Only inside %LOCALAPPDATA%\Stackshot\temp, never anywhere else.
        public void Sweep()
        {
            DateTime limit = DateTime.Now.AddHours(-1);
            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch (Exception ex) { Log("Limpieza: " + ex.Message); return; }
            int n = 0;
            foreach (string f in files)
            {
                if (Find(f) != null || editors.ContainsKey(f)) continue;
                string ext = Path.GetExtension(f).ToLowerInvariant();
                // Also ".png.part" files left by an interrupted write.
                if (Array.IndexOf(ImageExts, ext) < 0 && Array.IndexOf(MediaExts, ext) < 0 && ext != ".part") continue;
                try
                {
                    if (File.GetLastWriteTime(f) > limit) continue;
                    File.Delete(f);
                    kept.Remove(f);
                    n++;
                }
                catch (Exception ex) { Log("Limpieza de " + f + ": " + ex.Message); }
            }
            if (n > 0) Log("Limpieza: " + n + " capturas temporales borradas");
        }

        // Small captures open at 2x so they are easy to annotate; normal ones at most at 100%.
        public static float MaxZoom(Size img)
        {
            return img.Width < 600 && img.Height < 400 ? 2f : 1f;
        }

        public static bool IsMediaFile(string path)
        {
            return Array.IndexOf(MediaExts, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        // What the editor can open: images, videos and GIFs.
        public static bool IsEditable(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(ImageExts, ext) >= 0 || Array.IndexOf(MediaExts, ext) >= 0;
        }

        Card Find(string path)
        {
            foreach (Card c in cards)
            {
                if (string.Equals(c.FilePath, path, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Find(e.FullPath) == null) return;
            refresh[e.FullPath] = DateTime.Now;
            poll.Start();
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            Card c = Find(e.OldFullPath);
            if (c == null) return;
            if (File.Exists(e.FullPath)) { c.FilePath = e.FullPath; c.Reload(); }
            else Remove(c);
        }

        void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Card c = Find(e.FullPath);
            if (c != null) Remove(c);
        }

        // File size once its writer has closed it; -1 while it is still open.
        static long ClosedSize(string p)
        {
            try
            {
                using (FileStream f = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read)) return f.Length;
            }
            catch { return -1; }
        }

        void Poll(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            foreach (string p in new List<string>(refresh.Keys))
            {
                if ((now - refresh[p]).TotalMilliseconds < 300 || ClosedSize(p) <= 0) continue;
                refresh.Remove(p);
                Card c = Find(p);
                if (c != null && c.Reload()) Relayout();
            }
            if (refresh.Count == 0) poll.Stop();
        }

        bool AddCard(string p)
        {
            return AddCard(p, null, Size.Empty);
        }

        bool AddCard(string p, Bitmap preview, Size orig)
        {
            Card c = new Card(this, p);
            if (preview != null) c.UsePreview(preview, orig);
            else if (!c.Reload()) { c.Dispose(); Log("No se pudo leer " + p); return false; }
            // The card appears on the monitor where the capture was taken; the stack follows it there.
            anchorDevice = Screen.FromPoint(Control.MousePosition).DeviceName;
            followCandidate = null;
            cards.Add(c);
            scroll = 0; // a new capture is always visible: scroll back to the newest
            Relayout();
            if (!c.Parked)
            {
                c.Show();
                Native.SetWindowPos(c.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // HWND_TOPMOST, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
            }
            follow.Start();
            return true;
        }

        public void Remove(Card c)
        {
            Remove(c, Card.Exit.Slide);
        }

        // The card starts leaving right away; the ones above slide down into its slot while it fades.
        public void Remove(Card c, Card.Exit how)
        {
            if (!c.IsDisposed) c.Dismiss(how, 0);
            sync.BeginInvoke((Action)delegate
            {
                if (cards.Remove(c)) Relayout();
            });
        }

        // Tracked copy: when another app pastes it, its card counts as used and is removed.
        // sameAsFile: the image is exactly the PNG file, so the clipboard PNG is read from disk without re-encoding.
        public void CopyTracked(string path, Bitmap image, bool fromCapture, bool sameAsFile)
        {
            TrackedData td = new TrackedData(path, image, fromCapture, sameAsFile && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase), OnPasted);
            try { Clipboard.SetDataObject(td, false, 10, 100); }
            catch { td.Release(); throw; }
            TrackedData old = clip;
            clip = td;
            if (old != null) old.Release();
        }

        void OnPasted(TrackedData td)
        {
            if (exiting) return;
            sync.BeginInvoke((Action)delegate
            {
                Log("Pegada: " + Path.GetFileName(td.FilePath));
                Card c = Find(td.FilePath);
                if (c != null) c.Used("Pegada", delegate { Remove(c); });
            });
        }

        public void RemoveByPath(string path)
        {
            Card c = Find(path);
            if (c != null) Remove(c);
        }

        public void OpenEditor(string path)
        {
            Editor ed;
            if (editors.TryGetValue(path, out ed) && !ed.IsDisposed) { ed.BringUp(); return; }
            if (IsMediaFile(path))
            {
                // Video or GIF: presentation mode (annotations and backdrop over the whole recording).
                ed = Editor.ForVideo(this, path);
                if (ed == null) return;
            }
            else
            {
                Bitmap img;
                try { img = LoadFull(path); }
                catch (Exception ex) { Log("Editor: no se pudo abrir " + path + ": " + ex.Message); return; }
                ed = new Editor(this, path, img);
            }
            editors[path] = ed;
            ed.FormClosed += delegate { editors.Remove(path); };
            ed.Show();
            ed.BringUp();
        }

        // Cascade out, top to bottom.
        public void CloseAll()
        {
            int n = 0;
            for (int i = cards.Count - 1; i >= 0; i--) cards[i].Dismiss(Card.Exit.Slide, cards[i].Parked ? 0 : 35 * n++);
            cards.Clear();
            scroll = 0;
            HideChips();
        }

        Screen AnchorScreen()
        {
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.DeviceName == anchorDevice) return s;
            }
            // That monitor is gone (laptop undocked...): use the cursor's monitor.
            Screen m = Screen.FromPoint(Control.MousePosition);
            anchorDevice = m.DeviceName;
            return m;
        }

        // The stack follows the mouse: if it rests on another monitor, the cards move there.
        // It never moves while something is pressed, swiped or dragged.
        void FollowTick(object sender, EventArgs e)
        {
            if (cards.Count == 0) { follow.Stop(); followCandidate = null; return; }
            if (!settings.FollowMouse || Screen.AllScreens.Length < 2 || Control.MouseButtons != MouseButtons.None || Busy())
            {
                followCandidate = null;
                return;
            }
            string dev = Screen.FromPoint(Control.MousePosition).DeviceName;
            if (dev == anchorDevice) { followCandidate = null; return; }
            double now = Anim.Now;
            if (dev != followCandidate) { followCandidate = dev; followSince = now; return; }
            if (now - followSince < FollowDelay) return;
            followCandidate = null;
            anchorDevice = dev;
            Relayout();
        }

        bool Busy()
        {
            foreach (Card c in cards)
            {
                if (c.Busy) return true;
            }
            return false;
        }

        // Bottom-left: the first capture stays at the bottom and newer ones stack above. When they don't all fit, the
        // newest are shown with a pill counting the hidden ones; the wheel (or a click on the pill) scrolls. Up to 20
        // cards; beyond that the oldest leaves (its file stays in the temp folder).
        void Relayout()
        {
            while (cards.Count > MaxCards)
            {
                Card oldest = cards[0];
                cards.RemoveAt(0);
                oldest.Dismiss(Card.Exit.Slide, 0);
            }
            int n = cards.Count;
            if (n == 0) { HideChips(); return; }
            Screen scr = AnchorScreen();
            Rectangle wa = scr.WorkingArea;
            float s = ScaleFor(scr);
            int margin = (int)Math.Round(16 * s), gap = (int)Math.Round(10 * s), chipH = (int)Math.Round(26 * s);
            int width = (int)Math.Round(240 * s), left = wa.Left + margin, avail = wa.Height - 2 * margin;

            scroll = Math.Max(0, Math.Min(n - 1 - FitUp(s, avail, gap, chipH), scroll));
            int top = n - 1 - scroll, bottom = FitDown(top, s, avail, gap, chipH);
            pageSize = Math.Max(1, top - bottom);

            int y = wa.Bottom - margin;
            if (bottom > 0)
            {
                downChip.Set(new Rectangle(left, y - chipH, width, chipH), s, bottom + (bottom == 1 ? " anterior" : " anteriores"), true);
                y -= chipH + gap;
            }
            else downChip.Set(Rectangle.Empty, s, "", false);
            int below = y + gap, order = 0;
            for (int i = bottom; i <= top; i++)
            {
                Size sz = cards[i].WantedFor(s);
                y -= sz.Height;
                cards[i].Place(new Rectangle(left, y, sz.Width, sz.Height), scr.DeviceName, s, order++, true);
                y -= gap;
            }
            int hidden = n - 1 - top;
            if (hidden > 0) upChip.Set(new Rectangle(left, y - chipH, width, chipH), s, hidden + (hidden == 1 ? " m\u00E1s reciente" : " m\u00E1s recientes"), true);
            else upChip.Set(Rectangle.Empty, s, "", false);

            // Cards that don't fit are parked on their side: older ones below, newer ones above.
            for (int i = bottom - 1; i >= 0; i--)
            {
                Size sz = cards[i].WantedFor(s);
                cards[i].Place(new Rectangle(left, below, sz.Width, sz.Height), scr.DeviceName, s, 0, false);
                below += sz.Height + gap;
            }
            int above = y - (hidden > 0 ? chipH + gap : 0);
            for (int i = top + 1; i < n; i++)
            {
                Size sz = cards[i].WantedFor(s);
                above -= sz.Height;
                cards[i].Place(new Rectangle(left, above, sz.Width, sz.Height), scr.DeviceName, s, 0, false);
                above -= gap;
            }
        }

        // With the top card at index top, the lowest index that still fits (leaving room for the pills).
        int FitDown(int top, float s, int avail, int gap, int chip)
        {
            int used = top < cards.Count - 1 ? chip + gap : 0, bottom = top;
            for (int i = top; i >= 0; i--)
            {
                int need = used + (i == top ? 0 : gap) + cards[i].WantedFor(s).Height;
                int reserve = i > 0 ? gap + chip : 0;
                if (i != top && need + reserve > avail) break;
                used = need;
                bottom = i;
            }
            return bottom;
        }

        // With the oldest at the bottom, the highest index that fits: the scroll limit.
        int FitUp(float s, int avail, int gap, int chip)
        {
            int used = 0, top = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int need = used + (i == 0 ? 0 : gap) + cards[i].WantedFor(s).Height;
                int reserve = i < cards.Count - 1 ? gap + chip : 0;
                if (i != 0 && need + reserve > avail) break;
                used = need;
                top = i;
            }
            return top;
        }

        void HideChips()
        {
            upChip.Set(Rectangle.Empty, 1f, "", false);
            downChip.Set(Rectangle.Empty, 1f, "", false);
        }

        // Wheel over the stack: down shows older cards, up shows newer ones.
        public void Wheel(int delta)
        {
            wheelAcc += delta;
            int steps = 0;
            while (wheelAcc >= 120) { wheelAcc -= 120; steps--; }
            while (wheelAcc <= -120) { wheelAcc += 120; steps++; }
            if (steps != 0) ScrollBy(steps);
        }

        // Clicking a pill scrolls a whole page that way.
        public void Page(int dir)
        {
            ScrollBy(dir * pageSize);
        }

        void ScrollBy(int d)
        {
            int before = scroll;
            scroll = Math.Max(0, scroll + d);
            if (scroll != before) Relayout();
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            sync.BeginInvoke((Action)delegate { Relayout(); if (pet != null) pet.ScreensChanged(); });
        }

        public static float ScaleFor(Screen scr)
        {
            try
            {
                Native.POINT pt;
                pt.X = scr.Bounds.Left + scr.Bounds.Width / 2;
                pt.Y = scr.Bounds.Top + scr.Bounds.Height / 2;
                uint dx, dy;
                if (Native.GetDpiForMonitor(Native.MonitorFromPoint(pt, 2), 0, out dx, out dy) == 0 && dx > 0) return dx / 96f;
            }
            catch { }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }

        public void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(settings.SaveFolder);
                Process.Start(Native.Explorer, "\"" + settings.SaveFolder + "\"");
            }
            catch (Exception ex) { Log("Carpeta: " + ex.Message); }
        }

        protected override void ExitThreadCore()
        {
            // The tracked clipboard object lives in this process: flush it to the clipboard before exiting.
            exiting = true;
            Recorder.Stop();
            ScrollCapture.Stop();
            if (home != null && !home.IsDisposed) home.Dispose();
            if (showWait != null) showWait.Unregister(null);
            if (quitWait != null) quitWait.Unregister(null);
            if (showEvent != null) showEvent.Dispose();
            if (clip != null) { try { Native.OleFlushClipboard(); } catch { } }
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            sweepTimer.Stop();
            follow.Stop();
            hotkeys.Dispose();
            if (closeListener != null) closeListener.Dispose();
            if (quitEvent != null) quitEvent.Dispose();
            fsw.EnableRaisingEvents = false;
            fsw.Dispose();
            poll.Stop();
            foreach (Card c in new List<Card>(cards)) c.Close();
            cards.Clear();
            if (pet != null) { pet.Close(); pet = null; }
            Updater.Stop();
            upChip.Close();
            downChip.Close();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        public static Bitmap LoadFull(string path)
        {
            WaitWritten(path);
            byte[] data = File.ReadAllBytes(path);
            using (MemoryStream ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
                return new Bitmap(img);
        }

        public static Bitmap LoadPreview(string path, int maxDim, out Size orig)
        {
            using (Bitmap full = LoadFull(path))
            {
                orig = full.Size;
                return Preview(full, maxDim);
            }
        }

        // Downscaled copy (at most maxDim per side) for thumbnails.
        public static Bitmap Preview(Bitmap full, int maxDim)
        {
            double k = Math.Min(1.0, (double)maxDim / Math.Max(full.Width, full.Height));
            int w = Math.Max(1, (int)Math.Round(full.Width * k));
            int h = Math.Max(1, (int)Math.Round(full.Height * k));
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(full, 0, 0, w, h);
            }
            return b;
        }

        // Image to the clipboard as bitmap and PNG (preferred by web chats).
        public static void CopyImage(Bitmap bmp)
        {
            MemoryStream png = new MemoryStream();
            bmp.Save(png, ImageFormat.Png);
            DataObject d = new DataObject();
            d.SetData(DataFormats.Bitmap, true, bmp);
            d.SetData("PNG", false, png);
            Clipboard.SetDataObject(d, true, 10, 100);
        }

        // Explorer thumbnail (videos and GIFs).
        public static Bitmap ShellThumb(string path, int px)
        {
            IShellItemImageFactory f = null;
            try
            {
                Native.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out f);
                Native.SIZE sz;
                sz.cx = px;
                sz.cy = px;
                IntPtr hbm;
                if (f.GetImage(sz, 0, out hbm) != 0 || hbm == IntPtr.Zero) return null;
                try { return Image.FromHbitmap(hbm); }
                finally { Native.DeleteObject(hbm); }
            }
            catch (Exception ex)
            {
                Log("Miniatura de " + path + ": " + ex.Message);
                return null;
            }
            finally
            {
                if (f != null) Marshal.ReleaseComObject(f);
            }
        }

        // Resources embedded in the .exe (icon and logo).
        public static Stream Resource(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        public static Image LoadResourceImage(string name)
        {
            try
            {
                using (Stream s = Resource(name))
                {
                    if (s == null) return null;
                    using (Image img = Image.FromStream(s)) return new Bitmap(img);
                }
            }
            catch { return null; }
        }

        public static Icon LoadAppIcon()
        {
            try
            {
                using (Stream s = Resource("stackshot.ico"))
                    if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        static Icon TrayIcon()
        {
            try
            {
                using (Stream s = Resource("stackshot.ico"))
                    if (s != null) return new Icon(s, SystemInformation.SmallIconSize);
            }
            catch { }
            return AppIcon ?? SystemIcons.Application;
        }

        const long MaxLogBytes = 1024 * 1024;
        static readonly object logLock = new object();

        // Called from several threads. Past 1 MB the log rotates to stackshot.log.old, so it never grows unbounded.
        public static void Log(string msg)
        {
            try
            {
                lock (logLock)
                {
                    FileInfo fi = new FileInfo(LogPath);
                    if (fi.Exists && fi.Length > MaxLogBytes)
                    {
                        string old = LogPath + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(LogPath, old);
                    }
                    File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
