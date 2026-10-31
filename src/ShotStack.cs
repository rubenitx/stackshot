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
    public partial class ShotStack : ApplicationContext
    {
        public static string LogPath;
        public static Icon AppIcon;
        public static bool Test;           // --test: leaves clipboard and hotkeys alone
        public static ShotStack Running;    // the one this process runs (windows without an owner reach it here)
        const int MaxCards = 20;           // the rest are reachable with the mouse wheel
        const double FollowDelay = 350;    // ms the cursor must stay on another monitor before the stack follows it
        static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
        static readonly string[] MediaExts = { ".gif", ".mp4", ".webm", ".mkv", ".mov", ".avi" };
        // Every extension the player offers to open (FFmpeg decides in the end: any other one is probed when opened by hand).
        public static readonly string[] VideoExtensions = { ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".asf", ".flv", ".f4v", ".mpg", ".mpeg",
                                                            ".ts", ".mts", ".m2ts", ".m2t", ".m2v", ".m1v", ".mpv", ".mpe", ".qt", ".dv", ".ogm", ".trp", ".tp", ".wtv", ".dvr-ms",
                                                            ".3gp", ".3g2", ".3gpp", ".ismv", ".ogv", ".vob", ".mxf", ".divx", ".rm", ".rmvb", ".gif" };
        readonly List<PlayerWindow> players = new List<PlayerWindow>();

        readonly Settings settings;
        readonly string folder;
        readonly Dictionary<string, string> kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Timer sweepTimer = new Timer();
        readonly System.Threading.EventWaitHandle quitEvent;
        TrackedData clip;
        bool exiting, picking;
        public bool Exiting { get { return exiting; } }
        readonly List<Card> cards = new List<Card>();
        readonly Dictionary<string, Editor> editors = new Dictionary<string, Editor>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> refresh = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly Control sync = new Control();
        readonly FileSystemWatcher fsw;
        readonly Timer poll = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Hotkeys hotkeys;
        readonly Timer follow = new Timer();
        string anchorDevice, followCandidate;
        double followSince;
        readonly Chip upChip, downChip;
        int scroll, wheelAcc, pageSize = 1;    // scroll: how many of the newest cards are hidden above

        public ShotStack(Settings settings, bool justInstalled, bool showHome) : this(settings, justInstalled, showHome, null) { }

        // startPage: the main window opens on that section, without the launch animation (after an update: Acerca de).
        public ShotStack(Settings settings, bool justInstalled, bool showHome, string startPage)
        {
            this.settings = settings;
            folder = Settings.TempDir;
            Running = this;
            sync.CreateControl();
            IntPtr forceHandle = sync.Handle;
            if (!Test) Stall.Start(sync);
            Directory.CreateDirectory(folder);
            upChip = new Chip(this, -1);
            downChip = new Chip(this, 1);
            Ds.Changed += delegate { Ui(Restyle); };

            // Watch the temp folder to refresh a card when its file changes (the editor rewrites it) and drop it if the
            // file is deleted or moved.
            fsw = new FileSystemWatcher(folder);
            fsw.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            fsw.SynchronizingObject = sync;
            fsw.Changed += OnChanged;
            fsw.Renamed += OnRenamed;
            fsw.Deleted += OnDeleted;
            fsw.Error += OnWatcherError;
            fsw.EnableRaisingEvents = true;

            poll.Interval = 150;
            poll.Tick += Poll;
            follow.Interval = 100;
            follow.Tick += FollowTick;

            panel = BuildMenu();
            tray.Icon = TrayIcon();
            tray.Text = Test ? "Stackshot (prueba)" : "Stackshot";
            // Either button opens the tray panel; clicking the icon again closes it.
            tray.MouseClick += delegate(object o, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) Panel().Toggle();
            };
            // Double click opens the main window straight away (restored and in front), closing the panel the first click opened.
            tray.MouseDoubleClick += delegate(object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                Panel().CloseNow();
                ShowHome("home", false);
            };
            // Pointing at the icon lists the recent captures in the background, so the panel opens with them ready.
            tray.MouseMove += delegate { Panel().Warm(); };
            tray.Disposed += delegate { panel.Shutdown(); };
            // Built quietly a moment after startup, so even the first click opens it at once.
            Timer warmPanel = new Timer();
            warmPanel.Interval = 2500;
            warmPanel.Tick += delegate { warmPanel.Dispose(); if (!exiting) panel.Prewarm(); };
            warmPanel.Start();
            tray.Visible = true;
            if (!Test) closeListener = new CloseListener(BlockReason, SessionEndAsked, SessionEnding);

            hotkeys = new Hotkeys();
            hotkeys.Pressed += OnHotkey;
            RegisterHotkeys(true);

            // A folder whose new XML files open in the viewer (SOAP messages); off unless turned on.
            xmlWatch = new XmlWatch(sync, OpenArrivedXml);
            xmlWatch.Apply(settings);
            // Turned on with no folder (an earlier version had a default one): said once per start, instead of silence.
            if (settings.XmlWatch && XmlWatch.FolderFor(settings) == null && !Test)
                After(6000, delegate { if (!exiting) NotifyPage(I18n.T("Elige la carpeta de los XML", "Choose the XML folder"), I18n.T("La vigilancia de XML est\u00E1 activada pero no tiene carpeta. El\u00EDgela en General > Avanzado.", "XML monitoring is on, but has no folder. Pick one in General > Advanced."), "general"); });
            // Spanish kept from before on a Windows in another language: English is offered once.
            if (!Test) After(8000, delegate { if (!exiting) OfferEnglish(); });

            // Another instance (an update, the uninstaller) can ask this one to quit cleanly.
            if (!Test)
            {
                try
                {
                    quitEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.QuitEvent);
                    // Not one-shot: if the user keeps Stackshot open (Cancelar on unsaved changes), a later request is heard.
                    quitWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(quitEvent,
                        delegate { Ui(delegate { Log("Cierre pedido por otra copia"); TryQuit(QuitKind.OtherCopy); }); }, null, -1, false);
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
                // A .md or .xml opened with Stackshot while it runs arrives here (the path is in a small file).
                try
                {
                    openEvent = OpenHandle ?? new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.OpenEvent);
                    OpenHandle = null;
                    openWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(openEvent,
                        delegate { Ui(OpenRequested); }, null, -1, false);
                }
                catch (Exception ex) { Log("Evento de abrir: " + ex.Message); }
            }

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            HistoryIndex.Attach(this, settings);
            Log("Stackshot " + Installer.MyVersion.ToString(3) + " en marcha (" + Installer.ExePath + ")");

            // Test mode cleans nothing: the temp folder belongs to the real install. Otherwise the sweep runs only when
            // something can have become old enough (no timer at all while the folder holds nothing to clean).
            sweepTimer.Tick += delegate { sweepTimer.Stop(); Sweep(); };
            string startFile = OpenAtStart;
            OpenAtStart = null;
            // Started to open a document: it comes first, before the cleanup, the main window and the mascot.
            if (startFile != null) sync.BeginInvoke((Action)delegate { if (!exiting) OpenRequested(startFile); });
            if (!Test)
            {
                // The document is read on another thread and shown from a later message: the cleanup waits a few
                // seconds instead, so nothing on this thread stands between the file and the screen.
                if (startFile != null) After(4000, delegate { RescueOrphans(); Sweep(); });
                else
                {
                    RescueOrphans();
                    Sweep();
                }
            }

            Backdrop.Prewarm();
            if (startPage != null) sync.BeginInvoke((Action)delegate { ShowHome(startPage, false); });
            else if ((justInstalled && startFile == null) || showHome) sync.BeginInvoke((Action)delegate { ShowHome("home", true); });
            // Installed by opening a document: the document first, the main window right after.
            else if (justInstalled) After(2500, delegate { ShowHome("home", true); });
            else
            {
                // Build the main window quietly a little after startup, so the first time it opens it is instant. It
                // takes the interface about a second, so it waits for a moment the user isn't typing or moving the
                // mouse (2 minutes at most).
                Timer warm = new Timer();
                warm.Interval = 6000;
                DateTime warmBy = DateTime.UtcNow.AddMinutes(2);
                warm.Tick += delegate
                {
                    if (!exiting && home == null && Native.IdleMs() < 3000 && DateTime.UtcNow < warmBy) { warm.Interval = 1000; return; }
                    warm.Dispose();
                    if (exiting || home != null) return;
                    Stall.Mark("preparar la ventana principal");
                    try
                    {
                        HomeWindow warming = new HomeWindow(this, settings);
                        home = warming;
                        home.Closed += delegate { home = null; };
                        home.Prewarm();
                        // Once it has rendered (and hidden itself), make sure it isn't left showing off screen.
                        EventHandler rendered = null;
                        rendered = delegate
                        {
                            warming.ContentRendered -= rendered;
                            sync.BeginInvoke((Action)delegate { if (home == warming && !exiting) HideIfOffScreen(warming); });
                        };
                        warming.ContentRendered += rendered;
                    }
                    catch (Exception ex) { Log("Preparar ventana: " + ex.Message); }
                };
                warm.Start();
            }
            if (startFile != null) After(1500, MascotChanged);
            else sync.BeginInvoke((Action)MascotChanged);
            // What other copies asked for while this one was starting (startFile itself was already sent).
            if (startFile == null) sync.BeginInvoke((Action)delegate { if (!exiting) OpenRequested(null); });
            Updater.Start(this);
            StartWellness();
            StartNews(justInstalled);
            StartReportWatch();
            StartHealth();
            StartAssist();
        }

        // Something to say to the user right now: the mascot's bubble, or a notice when it is not on screen.
        public void Told(string text)
        {
            if (PetShown) pet.FocusSay(text);
            else Notify("Stackshot", text);
        }

        // The desktop mascot is out and can be seen (not hidden by a full-screen app): it speaks, and no notice says the same.
        bool PetShown { get { return pet != null && pet.Visible && !pet.Behind; } }

        enum Teller { Pet, Wait, Notice }

        // Who tells something that can wait a little: the desktop mascot once it is free. While it is busy (a walk, a
        // menu, a question of its own) the thing waits, but only for patience ms; then a notice says it. Below a
        // full-screen app it keeps waiting, as a notice would.
        Teller TellerFor(ref double waitingSince, double patience)
        {
            if (pet == null) { waitingSince = 0; return Teller.Notice; }
            if (pet.CanRemind) { waitingSince = 0; return Teller.Pet; }
            if (pet.Behind) return Teller.Wait;
            double now = Anim.Now;
            if (waitingSince <= 0) waitingSince = now;
            if (now - waitingSince < patience) return Teller.Wait;
            waitingSince = 0;
            return Teller.Notice;
        }

        // Runs once on this thread after ms, unless Stackshot is closing by then.
        void After(int ms, Action act)
        {
            Timer t = new Timer { Interval = ms };
            t.Tick += delegate { t.Dispose(); if (!exiting) act(); };
            t.Start();
        }

        System.Threading.EventWaitHandle showEvent, openEvent;
        System.Threading.RegisteredWaitHandle quitWait, showWait, openWait;

        // The file the command line named (set before the window is built), opened once the stack is up.
        public static string OpenAtStart;

        // Made by Program as soon as it holds the lock; the window takes it over when it is built.
        public static System.Threading.EventWaitHandle OpenHandle;

        void OpenRequested() { OpenRequested(null); }

        System.Windows.Forms.Timer openRetry;
        int openTries;

        // Every video opens in its own player window. The viewer shows one document at a time: the first one opens and the
        // rest are announced.
        void OpenRequested(string first)
        {
            List<string> all = new List<string>(), commands = new List<string>();
            if (first != null) all.Add(first);
            if (!Test)
            {
                foreach (string p in Installer.TakeOpenRequests(commands))
                    if (!all.Exists(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) all.Add(p);
                // The request file was held for a moment (an antivirus or indexer): taken again shortly, not left waiting
                // for the next double click (for up to about 6 s).
                if (!Installer.OpenPending) openTries = 0;
                else if (openTries++ < 20)
                {
                    if (openRetry == null)
                    {
                        openRetry = new System.Windows.Forms.Timer { Interval = 300 };
                        openRetry.Tick += delegate { openRetry.Stop(); if (!exiting) OpenRequested(null); };
                    }
                    openRetry.Stop();
                    openRetry.Start();
                }
            }
            RunCommands(commands);
            List<string> docs = all.FindAll(x => Installer.ViewerFile(x));
            foreach (string p in all)
                if (!Installer.ViewerFile(p))
                {
                    try { OpenPlayer(p); }
                    catch (Exception ex) { Log("Abrir " + p + ": " + ex.Message); }
                }
            if (docs.Count == 0) return;
            OpenViewerFile(docs[0]);
            if (docs.Count > 1)
                Notify("Stackshot", string.Format(I18n.T("Se abre {0}. {1} {2} (el visor muestra uno cada vez).", "Opening {0}. {1} {2} (the viewer shows one at a time)."), Path.GetFileName(docs[0]), docs.Count - 1, docs.Count == 2 ? I18n.T("archivo m\u00E1s", "more file") : I18n.T("archivos m\u00E1s", "more files")));
        }

        void OpenViewerFile(string path)
        {
            try { MarkdownWindow.OpenFile(this, path); }
            catch (Exception ex) { Log("Abrir " + path + ": " + ex.Message); }
        }

        XmlWatch xmlWatch;

        // A new XML in the watched folder: in the viewer and in front. Unsaved changes there are not replaced: the file is
        // only announced, and the tray's latest-XML entry brings it later.
        void OpenArrivedXml(string path)
        {
            if (exiting) return;
            CheckArrivedFault(path);
            if (MarkdownWindow.OpenArrived(this, path)) return;
            TellArrivedXml(path);
        }

        internal static string NoXmlFolder { get { return I18n.T("Todav\u00EDa no hay ninguna carpeta de XML vigilada: el\u00EDgela en Ajustes > General > Avanzado.", "No XML folder is monitored yet: choose it in Settings > General > Advanced."); } }

        // The tray's latest-XML entry: the newest file in the watched folder.
        void OpenLatestXml()
        {
            string dir = XmlWatch.FolderFor(settings);
            if (dir == null) { Notify("Stackshot", NoXmlFolder); return; }
            string last = XmlWatch.Latest(dir);
            if (last == null) { Notify("Stackshot", I18n.Fmt("No hay ning\u00FAn XML en {0}.", "There is no XML in {0}.", dir)); return; }
            OpenViewerFile(last);
        }
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

        // One point per capture; reaching a new level is celebrated.
        void AddLove()
        {
            string up = LoveUp();
            if (pet != null) pet.Celebrate(up);
        }

        // One friendship point, saved; what it says when that reaches a new level, or null.
        string LoveUp()
        {
            int before = MascotParts.Level(settings.MascotLove);
            settings.MascotLove++;
            settings.Save();
            int after = MascotParts.Level(settings.MascotLove);
            return after > before ? MascotTalk.LevelUp(settings, after) : null;
        }

        TrayPanel panel;

        // The tray panel, made again should anything ever have closed it for good.
        TrayPanel Panel()
        {
            if (panel.IsDisposed && !exiting) panel = BuildMenu();
            return panel;
        }

        // The tray panel and what its entries do; they run once it has faded out.
        TrayPanel BuildMenu()
        {
            TrayPanel p = new TrayPanel(settings);
            p.HasCards = delegate { return cards.Count > 0; };
            p.OpenCapture = delegate(string path) { OpenMedia(path); };
            p.Command = delegate(string cmd)
            {
                switch (cmd)
                {
                    case "home": ShowHome("home", false); break;
                    case "folder": OpenFolder(); break;
                    case "openvideo": OpenVideoDialog(); break;
                    case "lastxml": OpenLatestXml(); break;
                    case "closeall": CloseAll(); break;
                    case "settings": ShowHome("general", false); break;
                    case "pause": Recorder.TogglePause(); break;
                    case "quit": TryQuit(QuitKind.User); break;
                    // A capture: the panel has already blinked, faded out and hidden (~210 ms after the click, and it is
                    // excluded from captures); a few frames more so the window that got the focus back has repainted.
                    default: Delay(60, delegate { OnHotkey(cmd); }); break;
                }
            };
            return p;
        }

        void RegisterHotkeys(bool report)
        {
            if (Test) return;
            hotkeys.Clear();
            // Failures sorted by cause: another program holds it, Stackshot already registered it for an action (written
            // twice in a settings file edited by hand), or it is not a combination at all. Each combination is told once.
            List<string> failed = new List<string>(), twice = new List<string>(), unreadable = new List<string>(), mine = new List<string>();
            foreach (string action in Settings.Actions)
            {
                List<string> bad = hotkeys.Register(action, settings.HotkeysFor(action));
                List<string> combos = Hotkeys.Split(settings.HotkeysFor(action));
                // The same text twice in one action: the first that registered works, the rest failed.
                Dictionary<string, int> works = new Dictionary<string, int>();
                foreach (string c in combos) works[c] = (works.ContainsKey(c) ? works[c] : 0) + 1;
                foreach (string c in bad) works[c]--;
                foreach (string c in combos)
                {
                    string combo = c;
                    if (works[combo] > 0) { works[combo]--; mine.Add(combo); continue; }
                    uint mods;
                    Keys key;
                    string shown = Hotkeys.Display(combo);
                    if (!Hotkeys.TryParse(combo, out mods, out key)) { if (!unreadable.Contains("\u00AB" + combo.Trim() + "\u00BB")) unreadable.Add("\u00AB" + combo.Trim() + "\u00BB"); }
                    else if (mine.Exists(delegate(string o) { return Hotkeys.Same(o, combo); })) { if (!twice.Contains(shown)) twice.Add(shown); }
                    else if (!failed.Contains(shown)) failed.Add(shown);
                }
            }
            if (failed.Count + twice.Count + unreadable.Count == 0) return;
            Log("Atajos sin registrar: ocupados " + string.Join(", ", failed.ToArray()) + "; repetidos " + string.Join(", ", twice.ToArray()) +
                "; no v\u00E1lidos " + string.Join(", ", unreadable.ToArray()));
            if (!report) return;
            List<string> why = new List<string>();
            if (failed.Count > 0) why.Add(string.Join(", ", failed.ToArray()) + " " + I18n.T("los usa otro programa (\u00BFOneDrive, ShareX, Lightshot, Greenshot\u2026?).", "is used by another program (OneDrive, ShareX, Lightshot, Greenshot\u2026?)."));
            if (twice.Count > 0) why.Add(string.Join(", ", twice.ToArray()) + " " + I18n.T("est\u00E1 puesto dos veces en los atajos de Stackshot: solo cuenta la primera.", "is set twice in Stackshot shortcuts: only the first counts."));
            if (unreadable.Count > 0) why.Add(string.Join(", ", unreadable.ToArray()) + " " + I18n.T("no es una combinaci\u00F3n de teclas v\u00E1lida.", "is not a valid key combination."));
            Notify(failed.Count > 0 ? I18n.T("Algunos atajos est\u00E1n ocupados", "Some shortcuts are occupied") : I18n.T("Algunos atajos no funcionan", "Some shortcuts do not work"), string.Join(" ", why.ToArray()),
                   NoticeKind.Warning, I18n.T("Elegir otros atajos", "Choose other shortcuts"), ChooseOtherKeys);
        }

        // A short notice: Stackshot's own toast by the clock (ShotStack.Toast.cs).
        public void Notify(string title, string text)
        {
            Notify(title, text, null);
        }

        // page: clicking the notice opens that section of the main window ("record", "keys"...).
        public void NotifyPage(string title, string text, string page)
        {
            Post(PageNotice(title, text, page));
        }

        // folder: clicking the notice opens it in Explorer.
        public void Notify(string title, string text, string folder)
        {
            Post(FolderNotice(title, text, folder));
        }

        void OnHotkey(string action)
        {
            if (picking) return;
            try
            {
                switch (action)
                {
                    case "region": CaptureRegion(); break;
                    case "repeat": CaptureRepeat(); break;
                    case "delayed": CaptureDelayed(); break;
                    case "screen": CaptureScreen(); break;
                    case "window": CaptureWindow(); break;
                    case "scroll": picking = !ScrollCapture.Active; ScrollCapture.Toggle(this, settings); break;
                    case "text": CaptureText(); break;
                    case "video": picking = !Recorder.Recording; Recorder.Toggle(this, settings, false); break;
                    case "markdown": MarkdownWindow.Open(this, settings); break;
                    case "gif": picking = !Recorder.Recording; Recorder.Toggle(this, settings, true); break;
                    case "pause": Recorder.TogglePause(); break;
                }
            }
            catch (Exception ex)
            {
                // Never a silent failure: the details go to the log, the user hears it didn't work.
                Log("Captura (" + action + "): " + ex);
                if (!exiting)
                    Notify(action == "markdown" ? I18n.T("No se pudo abrir el visor", "Could not open viewer") : I18n.T("No se pudo capturar", "Could not capture"),
                           ex is OutOfMemoryException ? I18n.T("No hay memoria suficiente para una imagen tan grande. Prueba con un \u00E1rea m\u00E1s peque\u00F1a.", "Not enough memory for such a large image. Try a smaller area.")
                                                      : I18n.T("Ha fallado algo inesperado. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.", "An unexpected error occurred. Please try again; if it recurs, check the Stackshot log for details."));
            }
            finally { picking = false; EndCapture(); }
        }

        public Settings Settings { get { return settings; } }

        // Opens (or brings to front) the main window on that section. intro: play the launch animation.
        public void ShowHome(string page, bool intro)
        {
            Stall.Mark("abrir la ventana principal");
            if (home == null || home.IsDisposed)
            {
                home = new HomeWindow(this, settings);
                home.Closed += delegate { home = null; };
            }
            else HideIfOffScreen(home); // still warming up off screen (or left there): it comes back centered
            home.Present(page, intro && settings.ShowIntro);
        }
        HomeWindow home;

        // The main window warms up parked far off screen. With display scaling above ~110% Windows clamps that spot, so
        // the window could stay "shown" out of sight, with its clock running, and later open there. Hidden instead.
        static void HideIfOffScreen(HomeWindow w)
        {
            try
            {
                if (w.IsDisposed || !w.IsVisible || w.WindowState == System.Windows.WindowState.Minimized) return;
                IntPtr h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                Native.RECT r;
                if (h == IntPtr.Zero || !Native.GetWindowRect(h, out r)) return;
                Rectangle px = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                foreach (Screen s in Screen.AllScreens) if (s.Bounds.IntersectsWith(px)) return;
                w.Hide();
            }
            catch (Exception ex) { Log("Ventana fuera de pantalla: " + ex.Message); }
        }

        // While a hotkey field is recording, global hotkeys must not swallow the keys.
        public void SuspendHotkeys() { if (!Test) hotkeys.Clear(); }
        public void ResumeHotkeys() { RegisterHotkeys(true); }

        // Action requested from the window (already hidden so it isn't captured).
        public void Run(string action)
        {
            Delay(220, delegate { OnHotkey(action == "video" && Recorder.Recording ? "video" : action); });
        }

        // Closing the main window with "Seguir en la bandeja" off.
        public void Quit() { TryQuit(QuitKind.User); }

        // ---- Quitting: one path for every way out.

        // Session: Windows asked to end the session and was told to wait; the user may still cancel the sign-out.
        public enum QuitKind { User, OtherCopy, Session, Windows }

        // Another copy (an update, the uninstaller) doesn't ask: the recording is saved first. A sign-out asks only
        // while the take is still going (a recording already saving has nothing to decide), since the user may cancel
        // the sign-out and want to keep recording.
        public static bool AsksAboutRecording(QuitKind kind, bool stillRecording)
        {
            if (kind == QuitKind.User) return true;
            if (kind == QuitKind.Session) return stillRecording;
            return false;
        }

        bool quitApproved, quitting;
        System.Threading.EventWaitHandle savingFlag;

        // The tray's Salir, closing Home, another copy (update, install, uninstall): asks about what would be lost,
        // saves the recording, then exits. Stays open if the user cancels.
        void TryQuit(QuitKind kind)
        {
            if (exiting || quitting) return;
            if (PrepareQuit(kind)) ExitThread();
        }

        // Runs the checks of QuitSteps against the real recorder, editors and documents. While it runs, another copy
        // waiting for this one to close (Installer.QuitRunning) keeps waiting instead of giving up after 10 s.
        bool PrepareQuit(QuitKind kind)
        {
            if (quitApproved) return true;
            quitting = true;
            HoldSavingFlag(true);
            try
            {
                QuitSteps steps = new QuitSteps();
                steps.Interactive = kind != QuitKind.Windows;
                steps.Recording = delegate { return Recorder.Busy; };
                steps.AskRecording = delegate
                {
                    if (!AsksAboutRecording(kind, Recorder.Recording)) return true;
                    string text = kind == QuitKind.Session
                        ? I18n.T("Windows quiere cerrar la sesi\u00F3n y hay una grabaci\u00F3n en marcha.\n\nStackshot la detiene y la guarda en tu carpeta antes de salir.",
                                 "Windows wants to sign out and a recording is in progress.\n\nStackshot will stop and save it to your folder before exiting.")
                        : I18n.T("Hay una grabaci\u00F3n en marcha.\n\nStackshot la detiene y la guarda en tu carpeta antes de salir.",
                                 "A recording is in progress.\n\nStackshot will stop and save it to your folder before exiting.");
                    return Dialogs.Ask(null, text, MessageBoxIcon.Question, new string[] { I18n.T("Detener y salir", "Stop and exit"), I18n.T("Seguir grabando", "Keep recording") }, 1, 1) == 0;
                };
                steps.ConfirmEditors = Editor.ConfirmCloseAll;
                steps.ConfirmDocuments = MarkdownWindow.ConfirmCloseAll;
                steps.SaveRecording = delegate { SaveRecordingForExit(kind == QuitKind.Windows ? 25000 : 300000); };
                steps.WaitCopies = delegate { WaitCopies(kind == QuitKind.Windows ? 20000 : 120000); };
                bool ok = steps.Run();
                if (ok) quitApproved = true;
                else Log("Salida cancelada (" + steps.CancelledBy + ")");
                return ok;
            }
            finally
            {
                quitting = false;
                if (!quitApproved) HoldSavingFlag(false);
            }
        }

        void HoldSavingFlag(bool on)
        {
            if (Test) return;
            try
            {
                if (on && savingFlag == null) savingFlag = new System.Threading.EventWaitHandle(true, System.Threading.EventResetMode.ManualReset, Installer.SavingEvent);
                else if (!on && savingFlag != null) { savingFlag.Dispose(); savingFlag = null; }
            }
            catch (Exception ex) { Log("Aviso de guardado: " + ex.Message); }
        }

        // Stops the recording as if Stop had been pressed and waits for it. Its card can no longer be shown, so what it
        // produced goes into the save folder: the recorder moves it there; anything it left in the temp folder is moved
        // here, so a finished recording is never left to the sweep.
        void SaveRecordingForExit(int timeoutMs)
        {
            HashSet<string> before = MediaIn(folder);
            bool done = Recorder.Shutdown(timeoutMs);
            if (!done) Log("Salida: la grabaci\u00F3n no termin\u00F3 a tiempo; sus archivos de trabajo se recuperan al volver a abrir");
            foreach (string f in MediaIn(folder))
            {
                if (before.Contains(f)) continue;
                try
                {
                    string keepFolder = KeepFolderFor(f);
                    Directory.CreateDirectory(keepFolder);
                    string dest = Unique(Path.Combine(keepFolder, Path.GetFileName(f)));
                    File.Move(f, dest);
                    CopyPoster(f, dest);
                    Log("Grabaci\u00F3n guardada al salir: " + dest);
                }
                catch (Exception ex) { Log("Guardar grabaci\u00F3n al salir " + f + ": " + ex.Message); }
            }
        }

        static HashSet<string> MediaIn(string dir)
        {
            HashSet<string> r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                    if (IsMediaFile(f) && !Path.GetFileName(f).StartsWith("~", StringComparison.Ordinal)) r.Add(f);
            }
            catch (Exception ex) { Log("Salida: " + ex.Message); }
            return r;
        }

        // ---- Windows signing out or shutting down (CloseListener).

        // Why Windows should wait, or null when nothing would be lost.
        string BlockReason()
        {
            if (exiting) return null;
            bool rec = false, docs = false;
            try { rec = Recorder.Busy; } catch { }
            try { docs = Editor.AnyUnsaved || MarkdownWindow.AnyUnsaved; } catch { }
            bool live = false;
            try { live = Recorder.Recording; } catch { }
            if (live) return I18n.T("Hay una grabaci\u00F3n en marcha en Stackshot", "A recording is in progress in Stackshot");
            if (rec) return I18n.T("Stackshot est\u00E1 guardando una grabaci\u00F3n\u2026", "Stackshot is saving a recording\u2026");
            if (docs) return I18n.T("Hay cambios sin guardar en Stackshot", "There are unsaved changes in Stackshot");
            return null;
        }

        // Windows asked and was told to wait: the questions are asked (a take still going too, as the user may cancel
        // the sign-out and keep recording); a recording already saving goes on. When everything is settled Stackshot
        // closes and Windows can go on. If the user picks "sign out anyway" first, SessionEnding saves.
        void SessionEndAsked()
        {
            if (exiting || quitting) return;
            sync.BeginInvoke((Action)delegate { TryQuit(QuitKind.Session); });
        }

        // The session ends now (the user chose to close anyway): no questions, save what can be saved in time. Windows
        // may end the process as soon as this returns.
        void SessionEnding()
        {
            if (exiting) return;
            if (quitting)
            {
                // Arrived while a question about unsaved changes is on screen: nobody will answer it.
                if (Recorder.Busy) SaveRecordingForExit(25000);
                return;
            }
            PrepareQuit(QuitKind.Windows);
            ExitThread();
        }

        public void ApplySettings()
        {
            Ds.Apply(settings.Appearance);
            if (settings.FollowMouse && cards.Count > 0) follow.Start();
            if (xmlWatch != null) xmlWatch.Apply(settings);
            settings.Save();
        }

        void CaptureRegion()
        {
            picking = true;
            CaptureStart start = BeginCapture(); // the pointer as the screen freezes, before the picker shows its crosshair
            Bitmap frozen;
            Rectangle vs;
            IntPtr window;
            RegionPicker.Mode chosen;
            Rectangle r = RegionPicker.Pick(RegionPicker.Mode.Image, settings.AllInOne, settings.AdjustFirst && !settings.AllInOne, out frozen, out vs, out window, out chosen);
            using (frozen)
            {
                if (r.IsEmpty) return;
                // All-in-one: the bar can turn the area into a recording, a scrolling capture or copied text.
                if (chosen == RegionPicker.Mode.Video || chosen == RegionPicker.Mode.Gif) { Recorder.Start(this, settings, r, chosen == RegionPicker.Mode.Gif); return; }
                if (chosen == RegionPicker.Mode.Scroll) { ScrollCapture.Start(this, r); return; }
                if (chosen == RegionPicker.Mode.Text) { CopyAreaText(frozen, r, vs); return; }
                Rectangle src = new Rectangle(r.X - vs.X, r.Y - vs.Y, r.Width, r.Height);
                src.Intersect(new Rectangle(0, 0, frozen.Width, frozen.Height));
                if (src.Width < 1 || src.Height < 1) return;
                Bitmap shot = frozen.Clone(src, PixelFormat.Format32bppArgb);
                start.DrawPointer(shot, new Point(src.X + vs.X, src.Y + vs.Y));
                string name = I18n.T("Captura", "Capture");
                if (window != IntPtr.Zero)
                {
                    name = Grabber.ProcessName(window) ?? name;
                    if (WindowIsRounded(window, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
                }
                RememberArea(r, window);
                SaveCapture(shot, name, start);
            }
        }

        // Copy text from the screen: an area is chosen as for a capture, and the text in it goes to the clipboard. No card
        // and no file: only the text is wanted.
        void CaptureText()
        {
            picking = true;
            Bitmap frozen;
            Rectangle vs;
            IntPtr window;
            RegionPicker.Mode chosen;
            Rectangle r = RegionPicker.Pick(RegionPicker.Mode.Text, false, out frozen, out vs, out window, out chosen);
            using (frozen) if (!r.IsEmpty) CopyAreaText(frozen, r, vs);
        }

        // The text in an area of the frozen screen (frozenAt: where that image sits on the screen).
        void CopyAreaText(Bitmap frozen, Rectangle r, Rectangle frozenAt)
        {
            Rectangle src = new Rectangle(r.X - frozenAt.X, r.Y - frozenAt.Y, r.Width, r.Height);
            src.Intersect(new Rectangle(0, 0, frozen.Width, frozen.Height));
            if (src.Width < 1 || src.Height < 1) return;
            CopyText(frozen.Clone(src, PixelFormat.Format32bppArgb), true, null);
        }

        // Reads the text in the image with Windows' OCR (on a worker thread) and copies it. done(ok, message) gets a few
        // words for the caller to show; without it the outcome is a notice.
        public void CopyText(Bitmap image, Action<bool, string> done)
        {
            CopyText(image, false, done);
        }

        // owned: the image is handed over (decoded or rendered for this) and read without another copy.
        public void CopyText(Bitmap image, bool owned, Action<bool, string> done)
        {
            Ocr.ReadAsync(image, owned, sync, delegate(string text, string error)
            {
                if (exiting) return;
                bool ok = false;
                string title, message;
                if (text == null) { title = I18n.T("No se ha podido leer el texto", "Could not read text"); message = error; }
                else if (text.Trim().Length == 0) { title = I18n.T("No hay texto", "No text found"); message = I18n.T("No se ha encontrado texto en la imagen.", "No text found in the image."); }
                else
                {
                    try
                    {
                        CopyPlainText(text);
                        ok = true;
                        title = I18n.T("Texto copiado", "Text copied");
                        message = Ocr.Describe(text);
                    }
                    catch (Exception ex)
                    {
                        Log("Copiar texto: " + ex.Message);
                        title = I18n.T("No se pudo copiar", "Could not copy");
                        message = I18n.T("Otro programa est\u00E1 usando el portapapeles. Vuelve a intentarlo.", "Another program is using the clipboard. Please try again.");
                    }
                }
                if (done != null) done(ok, ok ? I18n.T("Texto copiado", "Text copied") : title);
                if (done == null || !ok || text.IndexOf('\n') >= 0 || text.Length > 40) Notify(title, message);
            });
        }

        // Plain text to the clipboard, kept out of the cloud clipboard like every other copy.
        public static void CopyPlainText(string text)
        {
            DataObject d = new DataObject();
            d.SetText(text.Replace("\n", Environment.NewLine), TextDataFormat.UnicodeText);
            KeepOffCloud(d);
            Clipboard.SetDataObject(d, true, ClipTries, ClipWaitMs);
        }

        void CaptureScreen()
        {
            CaptureStart start = BeginCapture();
            Screen scr = Grabber.CurrentScreen();
            Bitmap shot = Grabber.Grab(scr.Bounds);
            start.DrawPointer(shot, scr.Bounds.Location);
            SaveCapture(shot, I18n.T("Pantalla", "Screen"), start);
        }

        void CaptureWindow()
        {
            CaptureStart start = BeginCapture();
            IntPtr h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero) { CaptureScreen(); return; }
            Rectangle r = Grabber.Bounds(h);
            r.Intersect(SystemInformation.VirtualScreen);
            if (r.Width < 2 || r.Height < 2) { CaptureScreen(); return; }
            Bitmap shot = Grabber.Grab(r);
            start.DrawPointer(shot, r.Location);
            if (WindowIsRounded(h, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
            SaveCapture(shot, Grabber.ProcessName(h) ?? I18n.T("Ventana", "Window"), start);
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
        public string SaveCapture(Bitmap shot, string name, CaptureStart start)
        {
            if (settings.Sound) Shutter.Play();
            Directory.CreateDirectory(folder);
            string path = NewCapturePath(name, start);
            Size size = shot.Size;
            Bitmap preview = Preview(shot, 600);
            Bitmap forClipboard = settings.CopyToClipboard && !Test ? TrackedData.DeepCopy(shot) : null;
            bool covering = CoverBeforeShowing(shot, path, ref preview, ref forClipboard);
            if (!covering) BeginWrite(path, shot, delegate(Exception ex)
            {
                Ui(delegate
                {
                    // The card may already be gone (pasted, closed) while the PNG was still being written: then nothing
                    // can use the image any more.
                    if (Find(path) == null)
                    {
                        DropUnsaved(path);
                        Notify(I18n.T("No se pudo guardar la captura", "Could not save capture"), Explain(ex, path));
                    }
                    else Notify(I18n.T("No se pudo guardar la captura", "Could not save capture"), Explain(ex, path) + I18n.T(" La miniatura sigue en la pila: puedes copiarla o guardarla en tu carpeta.", " The thumbnail remains in the stack: you can copy it or save it to your folder."));
                });
            });
            PlanSweep(TempLife + TimeSpan.FromSeconds(5));
            if (!AddCard(path, preview, size))
            {
                if (forClipboard != null) forClipboard.Dispose();
                DropUnsaved(path);
                return null;
            }
            if (forClipboard != null)
            {
                try { CopyTracked(path, forClipboard, false, true); }
                catch (Exception ex) { Log("Portapapeles: " + ex.Message); }
            }
            Log("Captura: " + Path.GetFileName(path) + " (" + size.Width + "x" + size.Height + ")");
            SaveIfAll(path);
            if (!Test) { AddLove(); CountCapture(); }
            if (Captured != null) Captured(path);
            return path;
        }

        // With "Guardar todas" on, a new capture or recording is copied to the save folder right away; a failure leaves
        // it in the stack, still temporary, and says why.
        void SaveIfAll(string path)
        {
            if (!settings.SaveAll || Test) return;
            KeepAsync(path, delegate(string saved, string why)
            {
                if (saved == null) Notify(I18n.T("No se pudo guardar en tu carpeta", "Could not save to your folder"), why + I18n.T(" La miniatura sigue en la pila: puedes volver a pulsar Guardar.", " The thumbnail remains in the stack: you can click Save again."));
            }, true);
        }

        // For the main window (the mascot celebrates each capture).
        public event Action<string> Captured;

        // Files being written in the background, and save-folder names taken by copies still running (both under the
        // writing lock).
        static readonly HashSet<string> writing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Captures whose PNG could not be written (disk full, a blocked folder): the image stays in memory so the card
        // can still be copied, dragged, edited or saved into the save folder.
        static readonly Dictionary<string, Bitmap> unsaved = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        // Write under a temp name and rename when done, so nobody reads a half-written PNG and the folder watcher
        // doesn't mistake it for an edit. failed runs on the worker thread.
        internal static void BeginWrite(string path, Bitmap bmp, Action<Exception> failed)
        {
            lock (writing) writing.Add(path);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string part = path + ".part";
                Exception error = null;
                try
                {
                    bmp.Save(part, ImageFormat.Png);
                    File.Move(part, path);
                }
                catch (Exception ex)
                {
                    error = ex;
                    Log("Guardar " + path + ": " + ex.Message);
                    try { if (File.Exists(part)) File.Delete(part); } catch { }
                }
                finally
                {
                    if (error != null) lock (unsaved) unsaved[path] = bmp;
                    else bmp.Dispose();
                    lock (writing)
                    {
                        writing.Remove(path);
                        System.Threading.Monitor.PulseAll(writing);
                    }
                }
                if (error != null && failed != null)
                {
                    try { failed(error); } catch { }
                }
            });
        }

        // A copy of the in-memory image of a capture that couldn't be written (the caller disposes it), or null.
        internal static Bitmap Unsaved(string path)
        {
            if (path == null) return null;
            lock (unsaved)
            {
                Bitmap b;
                return unsaved.TryGetValue(path, out b) ? TrackedData.DeepCopy(b) : null;
            }
        }

        // Its card is gone: the in-memory image goes too.
        internal static void DropUnsaved(string path)
        {
            if (path == null) return;
            lock (unsaved)
            {
                Bitmap b;
                if (!unsaved.TryGetValue(path, out b)) return;
                unsaved.Remove(path);
                b.Dispose();
            }
        }

        // Waits (up to 10 s) until a fresh capture's PNG is on disk.
        internal static bool IsWriting(string path)
        {
            if (path == null) return false;
            lock (writing) return writing.Contains(path);
        }

        // Another writer (the editor saving in the background) announces a file the same way BeginWrite does.
        internal static void MarkWriting(string path)
        {
            lock (writing) writing.Add(path);
        }

        internal static void DoneWriting(string path)
        {
            lock (writing)
            {
                writing.Remove(path);
                System.Threading.Monitor.PulseAll(writing);
            }
        }

        public static void WaitWritten(string path)
        {
            if (path == null) return;
            WaitForCheck(path);
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

        // Where an export of a file goes: next to it when it is already in the save folder (the export is kept like its
        // source; in the temp folder it would be swept away an hour after its card goes), else with the unsaved captures.
        public static string ExportFolder(string source, string saveFolder)
        {
            string dir = null;
            try { dir = Path.GetDirectoryName(source); } catch (Exception) { }
            return SameFolder(dir, saveFolder) ? saveFolder : InDatedFolder(dir, saveFolder) ? dir : Settings.TempDir;
        }

        public string ExportFolder(string source) { return ExportFolder(source, settings.SaveFolder); }

        // Finished recording or export: add it to the stack and put the file on the clipboard (to paste into a chat). One
        // written straight into the save folder (an export of a saved video) shows as saved.
        public void AddRecording(string path)
        {
            PlanSweep(TempLife + TimeSpan.FromSeconds(5));
            if (!AddCard(path)) return;
            if (SaveFolders.Inside(settings, Path.GetDirectoryName(path))) KeptInPlace(path);
            else if (!IsCine(path)) SaveIfAll(path); // a Cine master (GB) is kept only by hand
            // A Cine master (GB of lossless video most apps can't play) is never pasted into a chat by accident.
            if (settings.CopyToClipboard && !Test && !IsCine(path))
            {
                try { CopyFiles(path); }
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
            lock (writing) return writing.Contains(p) || reserved.Contains(p);
        }

        // A free name held until Unreserve: FFmpeg creates the file only once it starts, so two exports of one video
        // started together would otherwise both pick the same name and write over each other.
        public static string Reserve(string path)
        {
            lock (writing)
            {
                string p = Unique(path);
                reserved.Add(p);
                return p;
            }
        }

        public static void Unreserve(string path)
        {
            if (path == null) return;
            lock (writing) reserved.Remove(path);
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

        // Copies the capture to the save folder (or refreshes the copy if already saved), on the calling thread. The
        // stack and Home use KeepAsync; this stays for the editor, which saves small images it has just written.
        public string Keep(string path)
        {
            if (SaveFolders.Inside(settings, Path.GetDirectoryName(path))) return KeptInPlace(path);
            WaitWritten(path);
            string keepFolder = KeepFolderFor(path);
            Directory.CreateDirectory(keepFolder);
            string dest;
            if (!kept.TryGetValue(path, out dest))
            {
                dest = Unique(Path.Combine(keepFolder, Path.GetFileName(path)));
                kept[path] = dest;
            }
            // A background copy of an older version is still running: copying now would be overwritten by it when it
            // renames its .part, so that copy repeats itself with the new content instead.
            if (copyRuns.Repeat(path))
            {
                Log("Guardar: " + Path.GetFileName(path) + " se copia de nuevo al terminar la copia en curso");
                return dest;
            }
            Bitmap pending = Unsaved(path);
            if (pending != null && !File.Exists(path)) using (pending) WritePng(pending, dest);
            else
            {
                if (pending != null) pending.Dispose();
                File.Copy(path, dest, true);
            }
            CopyPoster(path, dest);
            Card c = Find(path);
            if (c != null) c.MarkSaved(dest);
            Log("Guardada: " + dest);
            return dest;
        }

        // Already in the save folder (opened from Home or the tray's recent list): nothing to copy.
        string KeptInPlace(string path) { return KeptInPlace(path, false); }

        string KeptInPlace(string path, bool quiet)
        {
            kept[path] = path;
            Card c = Find(path);
            if (c != null) c.MarkSaved(path, quiet);
            return path;
        }

        readonly HashSet<string> copying = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly CopyRuns copyRuns = new CopyRuns();
        int copiesRunning;
        readonly System.Threading.ManualResetEvent copiesIdle = new System.Threading.ManualResetEvent(true);

        public bool IsSaving(string path) { return copying.Contains(path); }

        // Saves a copy in the save folder off the UI thread (a multi-GB recording, a slow or synced folder): the card
        // shows the progress, a second press meanwhile does nothing. done runs on the UI thread with the saved path, or
        // with null and what went wrong in plain words.
        public void KeepAsync(string path, Action<string, string> done) { KeepAsync(path, done, false); }

        // quiet: saved by "Guardar todas", not by the user: the card shows it is saved without celebrating it.
        public void KeepAsync(string path, Action<string, string> done, bool quiet)
        {
            if (copying.Contains(path)) return;
            if (SaveFolders.Inside(settings, Path.GetDirectoryName(path)))
            {
                KeptInPlace(path, quiet);
                if (done != null) done(path, null);
                return;
            }
            string dest;
            bool fresh = !kept.TryGetValue(path, out dest);
            string keepFolder = KeepFolderFor(path);
            try
            {
                if (fresh) dest = Unique(Path.Combine(keepFolder, Path.GetFileName(path)));
            }
            catch (Exception ex)
            {
                string why = Explain(ex, keepFolder);
                if (done != null) done(null, why);
                return;
            }
            if (fresh) kept[path] = dest;
            lock (writing) reserved.Add(dest);
            copying.Add(path);
            if (System.Threading.Interlocked.Increment(ref copiesRunning) == 1) copiesIdle.Reset();
            Card card = Find(path);
            if (card != null) card.Saving(0);
            Bitmap pending = Unsaved(path);
            string target = dest;
            copyRuns.Begin(path);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    Directory.CreateDirectory(keepFolder);
                    WaitWritten(path);
                    while (true)
                    {
                        if (pending != null && !File.Exists(path)) WritePng(pending, target);
                        else
                        {
                            int shown = -1;
                            CopyWithProgress(path, target, delegate(double f)
                            {
                                int pct = (int)(f * 100);
                                if (pct == shown) return;
                                shown = pct;
                                Ui(delegate { Card c = Find(path); if (c != null) c.Saving(f); });
                            });
                        }
                        if (!copyRuns.Again(path)) break;
                        Log("Guardar: copiando de nuevo " + Path.GetFileName(path) + " (ha cambiado)");
                    }
                }
                catch (Exception ex)
                {
                    Log("Guardar " + path + ": " + ex.Message);
                    error = Explain(ex, target);
                }
                finally
                {
                    copyRuns.End(path);
                    if (pending != null) pending.Dispose();
                    lock (writing) reserved.Remove(target);
                    if (System.Threading.Interlocked.Decrement(ref copiesRunning) == 0) copiesIdle.Set();
                }
                Ui(delegate
                {
                    copying.Remove(path);
                    Card c = Find(path);
                    if (error != null)
                    {
                        if (fresh) kept.Remove(path);
                        if (c != null) c.SaveFailed();
                        if (done != null) done(null, error);
                        return;
                    }
                    CopyPoster(path, target);
                    if (c != null) c.MarkSaved(target, quiet);
                    Log("Guardada: " + target);
                    if (done != null) done(target, null);
                });
            });
        }

        // Copies through a ".part" file renamed at the end, so the save folder never holds half a file under the real
        // name; an older copy of the same capture (saved again after an edit) is replaced only then.
        public static void CopyWithProgress(string src, string dest, Action<double> progress)
        {
            string part = dest + ".part";
            try
            {
                using (FileStream from = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan))
                using (FileStream to = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    long total = Math.Max(1, from.Length), copied = 0;
                    byte[] buf = new byte[1 << 20];
                    int n, last = Environment.TickCount;
                    while ((n = from.Read(buf, 0, buf.Length)) > 0)
                    {
                        to.Write(buf, 0, n);
                        copied += n;
                        if (progress != null && unchecked(Environment.TickCount - last) >= 100)
                        {
                            last = Environment.TickCount;
                            progress(Math.Min(1.0, copied / (double)total));
                        }
                    }
                }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(part, dest);
                if (progress != null) progress(1.0);
            }
            catch
            {
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                throw;
            }
        }

        static void WritePng(Bitmap b, string dest)
        {
            string part = dest + ".part";
            try
            {
                b.Save(part, ImageFormat.Png);
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(part, dest);
            }
            catch
            {
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                throw;
            }
        }

        // Before exiting: copies still running finish (or the time runs out).
        void WaitCopies(int timeoutMs)
        {
            WaitChecks(timeoutMs);
            if (System.Threading.Volatile.Read(ref copiesRunning) == 0) return;
            Log("Salida: esperando a que terminen de guardarse " + copiesRunning + " archivos");
            if (!copiesIdle.WaitOne(timeoutMs)) Log("Salida: alguna copia no termin\u00F3 a tiempo");
        }

        // The same folder however it is written (trailing slash, short 8.3 names, relative parts).
        public static bool SameFolder(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        }

        // What went wrong with a file, in words the user can act on (the raw text stays in the log).
        public static string Explain(Exception ex, string path) { return Explain(ex, path, true); }

        // The same for reading or opening a file (starting its program): a denied access is not about the folder's
        // write permissions there.
        public static string ExplainOpen(Exception ex, string path) { return Explain(ex, path, false); }

        static string Explain(Exception ex, string path, bool writing)
        {
            string where = "";
            try { if (!string.IsNullOrEmpty(path)) where = Path.GetDirectoryName(path); } catch { }
            int code = ex == null ? 0 : ex.HResult & 0xFFFF;
            System.ComponentModel.Win32Exception w = ex as System.ComponentModel.Win32Exception;
            if (w != null) code = w.NativeErrorCode;
            if (code == 1260 || code == 225 || code == 4551)
                return I18n.T("Windows o el antivirus lo ha bloqueado.", "Windows or the antivirus blocked it.");
            if (code == 1155)
                return I18n.T("Windows no tiene ninguna aplicaci\u00F3n para abrir este tipo de archivo.", "Windows has no application to open this type of file.");
            if ((ex is UnauthorizedAccessException || code == 5) && !writing)
                return I18n.T("Windows no deja abrir el archivo (por sus permisos o por el antivirus).", "Windows cannot open the file (due to permissions or antivirus).");
            if (ex is UnauthorizedAccessException || code == 5)
                return I18n.Fmt("Windows no deja escribir en {0}. Comprueba los permisos de la carpeta o si \u00ABAcceso controlado a carpetas\u00BB la protege.", "Windows cannot write to {0}. Check folder permissions or if Controlled Folder Access is protecting it.", Show(where));
            if (code == 112 || code == 39)
                return I18n.Fmt("No queda espacio en el disco de {0}. Libera espacio y vuelve a intentarlo.", "No space left on the disk of {0}. Free up space and try again.", Show(where));
            if (code == 32 || code == 33)
                return I18n.T("Otro programa est\u00E1 usando el archivo. Ci\u00E9rralo y vuelve a intentarlo.", "Another program is using the file. Close it and try again.");
            if (ex is DirectoryNotFoundException || ex is DriveNotFoundException || code == 3 || code == 21 || code == 53 || code == 67)
                return I18n.Fmt("No se encuentra la carpeta {0} (\u00BFuna unidad desconectada o de red?).", "Cannot find folder {0} (a disconnected or network drive?).", Show(where));
            if (ex is FileNotFoundException || code == 2)
                return I18n.T("El archivo ya no existe.", "The file no longer exists.");
            if (ex is PathTooLongException || code == 206)
                return I18n.T("La ruta es demasiado larga para Windows. Elige una carpeta con una ruta m\u00E1s corta.", "The path is too long for Windows. Choose a folder with a shorter path.");
            if (ex is OutOfMemoryException)
                return I18n.T("No hay memoria suficiente. Cierra algo y vuelve a intentarlo.", "Not enough memory. Close something and try again.");
            if (!writing)
                return I18n.T("Ha fallado al abrir el archivo. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.", "Failed to open the file. Try again; if it recurs, the Stackshot log has the details.");
            return I18n.T("Ha fallado al leer o escribir el archivo. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.", "Failed to read or write the file. Try again; if it recurs, the Stackshot log has the details.");
        }

        static string Show(string dir)
        {
            return string.IsNullOrEmpty(dir) ? I18n.T("la carpeta", "the folder") : "\u00AB" + dir + "\u00BB";
        }

        public bool IsKept(string path)
        {
            return kept.ContainsKey(path);
        }

        readonly DateTime started = DateTime.Now;
        static readonly TimeSpan[] KeepSpans = { TimeSpan.FromHours(1), TimeSpan.FromDays(3650), TimeSpan.FromDays(1), TimeSpan.FromDays(7), TimeSpan.FromDays(30) };
        // How long a temporary capture lives (Settings.TempKeep). "Until closed" lives out the session; the exit sweep removes it.
        TimeSpan TempLife { get { return KeepSpans[Math.Max(0, Math.Min(KeepSpans.Length - 1, settings.TempKeep))]; } }
        bool sweepAll;
        DateTime sweepDue = DateTime.MaxValue;

        // Permanently deletes temp captures older than one hour that are no longer on screen.
        // Only inside %LOCALAPPDATA%\Stackshot\temp, never anywhere else. Then plans the next sweep for when the next
        // file comes of age (or stops if there is nothing left to clean).
        public void Sweep()
        {
            if (Test || exiting) return;
            DateTime now = DateTime.Now, limit = now - TempLife, next = DateTime.MaxValue;
            if (sweepAll) limit = DateTime.MaxValue;
            else if (settings.TempKeep == 1) limit = started;
            bool held = false;  // something old enough that can't go yet (on screen, open, locked)
            // One listing that already carries each file's date, instead of asking the disk again for every file.
            FileInfo[] files;
            try { files = new DirectoryInfo(folder).GetFiles(); }
            catch (Exception ex)
            {
                if (Directory.Exists(folder)) Log("Limpieza: " + ex.Message);
                return;
            }
            int n = 0;
            foreach (FileInfo fi in files)
            {
                string f = fi.FullName;
                string ext = Path.GetExtension(f).ToLowerInvariant();
                // Also ".png.part" files left by an interrupted write. Work files of a recording are never deleted here:
                // one that outlives its recording is moved to recuperado at the next start.
                if (WorkFiles.IsWorkFile(Path.GetFileName(f))) continue;
                if (Array.IndexOf(ImageExts, ext) < 0 && Array.IndexOf(MediaExts, ext) < 0 && ext != ".part" && ext != ".md") continue;
                try
                {
                    DateTime written = fi.LastWriteTime;
                    if (written > limit)
                    {
                        if (written + TempLife < next) next = written + TempLife;
                        continue;
                    }
                    // Old enough, but still on screen, open in the editor or part of a long recording under way: looked at
                    // again a little later.
                    if (Find(f) != null || (ext == ".md" && Find(Path.ChangeExtension(f, ".png")) != null) || editors.ContainsKey(f) || Playing(f) || copying.Contains(f) || (Recorder.Recording && Path.GetFileName(f).StartsWith("~", StringComparison.Ordinal)))
                    {
                        held = true;
                        continue;
                    }
                    File.Delete(f);
                    kept.Remove(f);
                    n++;
                }
                catch (Exception ex) { held = true; Log("Limpieza de " + f + ": " + ex.Message); }
            }
            if (n > 0) Log("Limpieza: " + n + " capturas temporales borradas");
            // Only files from here on (posters, covered originals, XML attachments; the save folders may be on a
            // network): on a worker thread, except the cleanup at exit, which must finish before the app goes.
            int keepXml = sweepAll ? -1 : settings.TempKeep;
            Action prune = delegate
            {
                PrunePosters();
                PruneOriginals();
                XmlAttachment.Prune(keepXml);
            };
            if (exiting || sweepAll) prune();
            else System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { prune(); } catch (Exception ex) { Log("Limpieza: " + ex.Message); } });
            sweepTimer.Stop();
            sweepDue = DateTime.MaxValue;
            TimeSpan wait = next != DateTime.MaxValue ? next - now + TimeSpan.FromSeconds(5) : TimeSpan.MaxValue;
            if (held && wait > TimeSpan.FromMinutes(15)) wait = TimeSpan.FromMinutes(15);
            if (wait != TimeSpan.MaxValue) PlanSweep(wait);
        }

        // A poster whose recording is in neither the temp folder nor the save folder any more (after a day, so a file
        // being moved isn't caught in between).
        void PrunePosters()
        {
            try
            {
                if (!Directory.Exists(PosterDir)) return;
                List<string> saved = null;
                foreach (string p in Directory.GetFiles(PosterDir, "*.png"))
                {
                    if (File.GetLastWriteTime(p) > DateTime.Now.AddDays(-1)) continue;
                    string name = Path.GetFileNameWithoutExtension(p);
                    if (File.Exists(Path.Combine(folder, name)) || KeptAnywhere(name, ref saved)) continue;
                    File.Delete(p);
                }
            }
            catch (Exception ex) { Log("Portadas: " + ex.Message); }
        }

        // ---- Recordings interrupted by a crash, a kill or a power cut.

        public static string RecoveredDir { get { return Path.Combine(Settings.DataDir, "recuperado"); } }

        // Work files left by a recording that never finished are moved, never deleted, to recuperado\<time>, and the user
        // is told once where they are.
        void RescueOrphans()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { SweepWorkTemp(); });
            List<WorkFiles.Rescued> moved = WorkFiles.Rescue(folder, RecoveredDir, TimeSpan.FromSeconds(10), true);
            if (moved.Count == 0) return;
            WorkFiles.Rescued last = moved[moved.Count - 1];
            // Spanish as it always was; English with the month spelled out (3/11 reads differently in the US).
            string when = last.Started == DateTime.MinValue ? "" : I18n.IsSpanish
                ? " (la de las " + last.Started.ToString("HH:mm") + (last.Started.Date != DateTime.Today ? " del " + last.Started.ToString("d/M") : "") + ")"
                : " (the one from " + last.Started.ToString("HH:mm") + (last.Started.Date != DateTime.Today ? " on " + last.Started.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture) : "") + ")";
            string text = (moved.Count == 1 ? I18n.Fmt("Stackshot se cerr\u00F3 mientras grababa{0}. Lo grabado est\u00E1 a salvo", "Stackshot closed while recording{0}. The recording is safe", when) : I18n.Fmt("Stackshot se cerr\u00F3 durante {0} grabaciones. Lo grabado est\u00E1 a salvo", "Stackshot closed during {0} recordings. The recordings are safe", moved.Count)) +
                          " " + I18n.T("en la carpeta \u00ABrecuperado\u00BB.", "in the \u201Crecuperado\u201D folder.");
            string dir = moved.Count == 1 ? last.Folder : RecoveredDir;
            sync.BeginInvoke((Action)delegate { if (!exiting) Notify(I18n.T("Grabaci\u00F3n recuperada", "Recording recovered"), text, dir); });
        }

        // The editor and the player write their work files (first frame, overlay, two-pass logs, strip, GIF palette)
        // to Windows' temp folder and delete them when done; a crash or a forced close leaves them behind. A day later
        // nothing can still be using them.
        static readonly string[] WorkTempPrefixes = { "stackshot-frame-", "stackshot-top-", "stackshot-2pass-", "stackshot-strip-", "stackshot-palette-" };

        internal static void SweepWorkTemp()
        {
            DateTime limit = DateTime.Now.AddDays(-1);
            string temp = Path.GetTempPath();
            foreach (string prefix in WorkTempPrefixes)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(temp, prefix + "*"))
                        try { if (File.GetLastWriteTime(f) < limit) File.Delete(f); } catch (Exception) { }
                    foreach (string d in Directory.GetDirectories(temp, prefix + "*"))
                        try { if (Directory.GetLastWriteTime(d) < limit) Directory.Delete(d, true); } catch (Exception) { }
                }
                catch (Exception ex) { Log("Limpieza de temporales de trabajo: " + ex.Message); }
            }
        }

        void OpenExplorer(string dir)
        {
            try { Process.Start(Native.Explorer, "\"" + dir + "\""); }
            catch (Exception ex) { Log("Carpeta: " + ex.Message); Notify(I18n.T("No se pudo abrir la carpeta", "Could not open folder"), ExplainOpen(ex, dir)); }
        }

        // Deletes every temporary capture that is not on screen or open (the save folder is never touched).
        public void ClearTemp()
        {
            sweepAll = true;
            try { Sweep(); }
            finally { sweepAll = false; }
            Recents.Forget();
        }

        // Makes sure a sweep runs within that time (an earlier one already planned stays).
        void PlanSweep(TimeSpan after)
        {
            if (Test || exiting) return;
            double ms = Math.Max(30000, Math.Min(Math.Min(TempLife.TotalMilliseconds, 3 * 86400000.0) + 60000, after.TotalMilliseconds));
            DateTime due = DateTime.Now.AddMilliseconds(ms);
            if (sweepTimer.Enabled && sweepDue <= due) return;
            sweepTimer.Stop();
            sweepTimer.Interval = (int)ms;
            sweepTimer.Start();
            sweepDue = due;
        }

        // The temp folder can vanish under the watcher (cleaned by hand, a disk tool): make it again and keep watching,
        // so cards still follow their files. An error that comes straight back is not retried in a loop: a few quick
        // re-arms, then it waits for the next capture.
        void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Exception ex = e.GetException();
            if (exiting || ex is InternalBufferOverflowException) { Log("Vigilante: " + (ex != null ? ex.Message : "?")); return; }
            DateTime now = DateTime.UtcNow;
            watcherErrors = now - watcherErrorAt < TimeSpan.FromSeconds(10) ? watcherErrors + 1 : 1;
            watcherErrorAt = now;
            Log("Vigilante: " + (ex != null ? ex.Message : "?") + (watcherErrors > 3 ? " (se deja de vigilar)" : ""));
            try
            {
                fsw.EnableRaisingEvents = false;
                if (watcherErrors > 3) return;
                Directory.CreateDirectory(folder);
                fsw.EnableRaisingEvents = true;
            }
            catch (Exception again) { Log("Vigilante, reinicio: " + again.Message); }
        }
        int watcherErrors;
        DateTime watcherErrorAt;

        // Small captures open at 2x so they are easy to annotate; normal ones at most at 100%.
        public static float MaxZoom(Size img)
        {
            return img.Width < 600 && img.Height < 400 ? 2f : 1f;
        }

        public static bool IsMediaFile(string path)
        {
            return Array.IndexOf(MediaExts, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        public static bool IsVideoFile(string path)
        {
            return !string.IsNullOrEmpty(path) && Array.IndexOf(VideoExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        // "Abrir vídeo": the common video types first, then anything.
        public static string VideoFilter
        {
            get
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(I18n.T("V\u00EDdeos|", "Videos|"));
                for (int i = 0; i < VideoExtensions.Length; i++) sb.Append(i > 0 ? ";*" : "*").Append(VideoExtensions[i]);
                return sb.Append("|").Append(I18n.T("Todos los archivos", "All files")).Append("|*.*").ToString();
            }
        }

        internal enum OpenRoute { Editor, Player, Windows }

        // Where a capture opens from a card, a recent or the tray: images in the editor; videos in Stackshot's player (Cine
        // masters always, as Windows can't decode them) or, when that is switched off, in Windows' default app.
        internal static OpenRoute RouteFor(string path, bool playerOpensVideos)
        {
            if (string.IsNullOrEmpty(path) || (!IsMediaFile(path) && !IsVideoFile(path))) return OpenRoute.Editor;
            return IsCineMaster(path) || playerOpensVideos ? OpenRoute.Player : OpenRoute.Windows;
        }

        // Only what the editor really opens: images, markdown, an interrupted write. Anything else may be a video FFmpeg reads.
        internal static bool IsEditorFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(ImageExts, ext) >= 0 || ext == ".md" || ext == ".part" || IsMediaFile(path) || IsVideoFile(path);
        }

        // A card, a recent or the tray: the route decides. An extension nobody listed is probed first.
        public void OpenMedia(string path)
        {
            if (RouteFor(path, settings.PlayerOpensVideos) == OpenRoute.Editor && !IsEditorFile(path) && File.Exists(path))
            {
                OpenUnknown(path);
                return;
            }
            OpenRouted(path, RouteFor(path, settings.PlayerOpensVideos));
        }

        void OpenRouted(string path, OpenRoute route)
        {
            switch (route)
            {
                case OpenRoute.Editor: OpenEditor(path); break;
                case OpenRoute.Player: OpenPlayerWhenReady(path); break;
                default: OpenWithWindows(path); break;
            }
        }

        void OpenWithWindows(string path)
        {
            try { Process.Start(path); }
            catch (Exception ex) { Log("Abrir: " + ex.Message); Notify(I18n.T("No se pudo abrir", "Could not open"), Path.GetFileName(path) + ": " + ExplainOpen(ex, path)); }
        }

        // FFmpeg decides: a file it can play follows the video route, anything else opens in Windows.
        void OpenUnknown(string path)
        {
            bool player = settings.PlayerOpensVideos;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string ff = Recorder.FindFfmpeg(settings);
                bool video = ff != null && MediaInfo.Probe(ff, path).CanPlay;
                Ui(delegate { OpenRouted(path, !video ? OpenRoute.Windows : player ? OpenRoute.Player : OpenRoute.Windows); });
            });
        }

        // A fresh recording may still be closing its file: the window opens once it is written.
        public void OpenPlayerWhenReady(string path)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                WaitWritten(path);
                Ui(delegate { OpenPlayer(path); });
            });
        }

        bool Playing(string path)
        {
            foreach (PlayerWindow w in players)
                if (!w.IsDisposed && string.Equals(w.FilePath, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // One window per file; opening it again brings it up.
        public void OpenPlayer(string path)
        {
            foreach (PlayerWindow w in players)
                if (!w.IsDisposed && string.Equals(w.FilePath, path, StringComparison.OrdinalIgnoreCase)) { w.BringUp(); return; }
            string ff = Recorder.FindFfmpeg(settings);
            if (ff == null) ff = FfmpegSetup.Run(settings);
            if (ff == null) return;
            // FFmpeg reads the file first, off the UI thread (a network drive or a damaged file can take long); the
            // window then finds the answer ready.
            if (MediaInfo.IsCached(path)) { OpenPlayerNow(path, ff); return; }
            if (!preparing.Add("p|" + path)) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                MediaInfo.ProbeCached(ff, path);
                Ui(delegate { preparing.Remove("p|" + path); if (!exiting && !Playing(path)) OpenPlayerNow(path, ff); });
            });
        }

        void OpenPlayerNow(string path, string ff)
        {
            PlayerWindow win;
            try { win = new PlayerWindow(this, settings, ff, path, null); }
            catch (Exception ex)
            {
                Log("Reproductor: " + ex.Message);
                Notify(I18n.T("No se pudo abrir", "Could not open"), Path.GetFileName(path));
                return;
            }
            players.Add(win);
            win.FormClosed += delegate { players.Remove(win); };
            win.Show();
            win.BringUp();
        }

        // The "Abrir vídeo…" dialog.
        public void OpenVideoDialog()
        {
            using (System.Windows.Forms.OpenFileDialog d = new System.Windows.Forms.OpenFileDialog())
            {
                d.Title = I18n.T("Abrir v\u00EDdeo", "Open video");
                d.Filter = VideoFilter;
                // "Todos los archivos" can pick an image: it goes where a card would send it.
                if (d.ShowDialog() != DialogResult.OK) return;
                if (IsVideoFile(d.FileName) || !IsEditorFile(d.FileName)) OpenPlayer(d.FileName);
                else OpenEditor(d.FileName);
            }
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
            // An atomic save (File.Replace) renames the old file away and puts the new one under the same name: the
            // capture is still there, only its content changed.
            if (File.Exists(e.OldFullPath)) { c.ReloadAsync(Relayout); return; }
            if (File.Exists(e.FullPath))
            {
                TrackedData td = clip;
                if (td != null && string.Equals(td.FilePath, e.OldFullPath, StringComparison.OrdinalIgnoreCase)) td.FilePath = e.FullPath;
                c.FilePath = e.FullPath;
                c.ReloadAsync(Relayout);
            }
            else Remove(c);
        }

        void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Card c = Find(e.FullPath);
            if (c != null && !File.Exists(e.FullPath)) Remove(c);
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

        // An edited card reloads once its writer has closed the file. A file whose card is gone (deleted, or renamed and
        // followed under its new name) or that stays held open for minutes is dropped, so the poll never ticks on idly.
        void Poll(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            foreach (string p in new List<string>(refresh.Keys))
            {
                double since = (now - refresh[p]).TotalMilliseconds;
                Card c = Find(p);
                if (c == null || since > RefreshGiveUpMs) { refresh.Remove(p); continue; }
                if (since < 300 || ClosedSize(p) <= 0) continue;
                refresh.Remove(p);
                c.ReloadAsync(Relayout);
            }
            if (refresh.Count == 0) poll.Stop();
        }
        const double RefreshGiveUpMs = 2 * 60 * 1000;

        bool AddCard(string p)
        {
            return AddCard(p, null, Size.Empty);
        }

        bool AddCard(string p, Bitmap preview, Size orig)
        {
            Card c = new Card(this, p);
            if (preview != null) c.UsePreview(preview, orig);
            else if (!c.Reload()) { c.Dispose(); Log("No se pudo leer " + p); return false; }
            // The folder watcher gave up after repeated errors: a new card is a good moment to try again.
            if (!fsw.EnableRaisingEvents && !exiting)
            {
                try { fsw.EnableRaisingEvents = true; }
                catch (Exception ex) { Log("Vigilante: " + ex.Message); }
            }
            // A new capture brings a tucked stack back out.
            if (tucked) Untuck();
            peeked = false;   // a new capture waits the full time again
            // The card appears on the monitor where the capture was taken; the stack follows it there.
            anchorDevice = MonitorName(Control.MousePosition);
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
            TuckAwake();
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

        // Another app holding the clipboard (Win+V history reading a copy, a clipboard tool) keeps it for milliseconds:
        // a few short tries, never a second of frozen interface.
        const int ClipTries = 6, ClipWaitMs = 40;

        // Tracked copy: when another app pastes it, its card counts as used and is removed.
        // sameAsFile: the image is exactly the PNG file, so the clipboard PNG is read from disk without re-encoding.
        public void CopyTracked(string path, Bitmap image, bool fromCapture, bool sameAsFile)
        {
            TrackedData td = new TrackedData(path, image, fromCapture, sameAsFile && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase), OnPasted);
            try { Clipboard.SetDataObject(td, false, ClipTries, ClipWaitMs); }
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
                else Log("Pegada sin miniatura en la pila (" + cards.Count + " miniaturas)");
            });
        }

        public void RemoveByPath(string path)
        {
            Card c = Find(path);
            if (c != null) Remove(c);
        }

        // False when there was nothing to open (the file could not be read).
        public bool OpenEditor(string path)
        {
            Stall.Mark("abrir el editor");
            Editor ed;
            if (editors.TryGetValue(path, out ed) && !ed.IsDisposed) { ed.BringUp(); return true; }
            if (IsMediaFile(path) || IsVideoFile(path))
            {
                // Video or GIF: presentation mode (annotations and backdrop over the whole recording). FFmpeg reads it
                // off the UI thread, so a slow drive or a damaged file never freezes the stack; the window follows.
                if (preparing.Contains("e|" + path)) return true;
                string ff = Recorder.FindFfmpeg(settings);
                if (ff == null) ff = FfmpegSetup.Run(settings);
                if (ff == null) return false;
                preparing.Add("e|" + path);
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Editor.VideoPrep v = Editor.PrepareVideo(ff, path);
                    Ui(delegate
                    {
                        preparing.Remove("e|" + path);
                        if (exiting) { Editor.FromPrepared(null, path, ff, new Editor.VideoPrep { Frame = v.Frame, Failure = "", Quiet = true }); return; }
                        Editor e = Editor.FromPrepared(this, path, ff, v);
                        if (e != null) ShowEditor(path, e);
                    });
                });
                return true;
            }
            if (OpenWhenChecked(path)) return true;
            Bitmap img;
            try { img = LoadFull(path); }
            catch (Exception ex) { Log("Editor: no se pudo abrir " + path + ": " + ex.Message); return false; }
            ShowEditor(path, new Editor(this, path, img));
            return true;
        }

        readonly HashSet<string> preparing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ShowEditor(string path, Editor ed)
        {
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
            // Nothing to follow with no cards, one monitor or the option off: the timer stops until that changes (a new
            // card, a monitor plugged in, the setting turned on).
            if (cards.Count == 0 || !settings.FollowMouse || Screen.AllScreens.Length < 2) { follow.Stop(); followCandidate = null; return; }
            if (Control.MouseButtons != MouseButtons.None || Busy())
            {
                followCandidate = null;
                return;
            }
            string dev = MonitorName(Control.MousePosition);
            if (dev == anchorDevice) { followCandidate = null; return; }
            double now = Anim.Now;
            if (dev != followCandidate) { followCandidate = dev; followSince = now; return; }
            if (now - followSince < FollowDelay) return;
            followCandidate = null;
            // Tucked, it goes along still tucked: its tab leaves this monitor and comes back on the other once the cards are in.
            if (tucked) { HideTab(); tabDelay = 560; }
            anchorDevice = dev;
            Relayout();
        }

        // The device name of the monitor under a point (Screen.FromPoint(p).DeviceName), cheap enough for every timer
        // tick or mouse move: building a Screen opens a device context (a quarter of a millisecond with several monitors),
        // so the last answer is kept per monitor handle until the displays change (WinForms then lists them anew).
        public static string MonitorName(Point p)
        {
            Native.POINT pt;
            pt.X = p.X;
            pt.Y = p.Y;
            IntPtr mon = Native.MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
            Screen[] all = Screen.AllScreens;
            MonitorSeen seen = lastMonitor;
            if (seen != null && mon != IntPtr.Zero && seen.Handle == mon && seen.All == all) return seen.Name;
            seen = new MonitorSeen(mon, all, Screen.FromPoint(p).DeviceName);
            lastMonitor = seen;
            return seen.Name;
        }

        sealed class MonitorSeen
        {
            public readonly IntPtr Handle;
            public readonly Screen[] All;
            public readonly string Name;
            public MonitorSeen(IntPtr h, Screen[] all, string name) { Handle = h; All = all; Name = name; }
        }
        static MonitorSeen lastMonitor;

        bool Busy()
        {
            foreach (Card c in cards)
            {
                if (c.Busy) return true;
            }
            return false;
        }

        // Light/dark switched: floating surfaces repaint with the new palette.
        void Restyle()
        {
            foreach (Card c in cards) c.Restyle();
            upChip.Restyle();
            downChip.Restyle();
        }

        // Bottom-left: the first capture stays at the bottom and newer ones stack above. When they don't all fit, the
        // newest are shown with a pill counting the hidden ones; the wheel (or a click on the pill) scrolls. Up to 20
        // cards; beyond that the oldest leaves (its file stays in the temp folder).
        void Relayout()
        {
            ToastsFollowStack();
            while (cards.Count > MaxCards)
            {
                Card oldest = cards[0];
                cards.RemoveAt(0);
                oldest.Dismiss(Card.Exit.Slide, 0);
            }
            int n = cards.Count;
            if (n == 0) { HideChips(); tucked = false; HideTab(); return; }
            Screen scr = AnchorScreen();
            Rectangle wa = scr.WorkingArea;
            float s = ScaleFor(scr);
            int margin = (int)Math.Round(16 * s), gap = (int)Math.Round(10 * s), chipH = (int)Math.Round(26 * s);
            int width = (int)Math.Round(256 * s), left = wa.Left + margin, avail = wa.Height - 2 * margin;

            scroll = Math.Max(0, Math.Min(n - 1 - FitUp(s, avail, gap, chipH), scroll));
            int top = n - 1 - scroll, bottom = FitDown(top, s, avail, gap, chipH);
            pageSize = Math.Max(1, top - bottom);

            int y = wa.Bottom - margin;
            if (bottom > 0)
            {
                downChip.Set(new Rectangle(left, y - chipH, width, chipH), s, I18n.Fmt(bottom == 1 ? "{0} anterior" : "{0} anteriores", bottom == 1 ? "{0} older" : "{0} older", bottom), true);
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
            if (hidden > 0) upChip.Set(new Rectangle(left, y - chipH, width, chipH), s, I18n.Fmt(hidden == 1 ? "{0} m\u00E1s reciente" : "{0} m\u00E1s recientes", hidden == 1 ? "{0} newer" : "{0} newer", hidden), true);
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
            // Tucked: the pills stay away until the cards come back out, and the tab follows the cards.
            if (tucked) { HideChips(); PlaceTab(tabDelay); tabDelay = 0; }
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
            if (tucked) { Untuck(); return; }
            int before = scroll;
            scroll = Math.Max(0, scroll + d);
            if (scroll != before) Relayout();
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            sync.BeginInvoke((Action)delegate
            {
                if (exiting) return;
                if (tucked) Untuck();
                Relayout();
                if (cards.Count > 0 && settings.FollowMouse) follow.Start();
                if (pet != null) pet.ScreensChanged();
            });
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
            catch (Exception ex)
            {
                Log("Carpeta: " + ex.Message);
                Notify(I18n.T("No se pudo abrir la carpeta", "Could not open folder"), ExplainOpen(ex, settings.SaveFolder));
            }
        }

        // Every exit ends here. One that didn't come through TryQuit (anything calling ExitThread directly) still asks
        // about unsaved work and saves the recording first, and stays open if the user cancels.
        protected override void ExitThreadCore()
        {
            if (exiting || quitting) return;
            if (!PrepareQuit(QuitKind.OtherCopy)) return;
            // The tracked clipboard object lives in this process: flush it to the clipboard before exiting.
            exiting = true;
            Editor.WaitBackgroundWrites();   // edits still being written land before the temp files are swept
            if (settings.TempKeep == 1) { exiting = false; sweepAll = true; try { Sweep(); } catch { } exiting = true; }
            Recorder.Stop();
            ScrollCapture.Stop();
            if (home != null && !home.IsDisposed) home.Shutdown();
            if (showWait != null) showWait.Unregister(null);
            if (openWait != null) openWait.Unregister(null);
            if (openEvent != null) openEvent.Dispose();
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
            xmlWatch.Dispose();
            poll.Stop();
            foreach (Card c in new List<Card>(cards)) c.Close();
            cards.Clear();
            if (toasts != null) toasts.CloseAll();
            if (pet != null) { pet.Close(); pet = null; }
            StickyNotes.CloseAll();
            Updater.Stop();
            upChip.Close();
            downChip.Close();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
            // Held until here so the copy taking over waits for this one to finish closing.
            HoldSavingFlag(false);
        }

        public static Bitmap LoadFull(string path)
        {
            WaitWritten(path);
            Bitmap pending = Unsaved(path);
            if (pending != null)
            {
                if (!File.Exists(path)) return pending;
                pending.Dispose();
            }
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
            KeepOffCloud(d);
            Clipboard.SetDataObject(d, true, ClipTries, ClipWaitMs);
        }

        // Files to the clipboard (videos, GIFs), kept out of the cloud clipboard like every other copy.
        public static void CopyFiles(string path)
        {
            StringCollection sc = new StringCollection();
            sc.Add(path);
            DataObject d = new DataObject();
            d.SetFileDropList(sc);
            KeepOffCloud(d);
            Clipboard.SetDataObject(d, true, ClipTries, ClipWaitMs);
        }

        // Screenshots often hold private data: never synced to other devices through Windows' cloud clipboard.
        public static void KeepOffCloud(DataObject d)
        {
            d.SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
        }

        // A Cine master: Windows' player can't decode it, so Stackshot's editor opens it.
        public static bool IsCine(string path)
        {
            return path != null && string.Equals(Path.GetExtension(path), Cine.Extension, StringComparison.OrdinalIgnoreCase);
        }

        // A master by name ("Cine <stamp>.mkv"): any other .mkv is an ordinary video.
        public static bool IsCineMaster(string path)
        {
            return IsCine(path) && Path.GetFileName(path).StartsWith("Cine ", StringComparison.OrdinalIgnoreCase);
        }

        // Windows has no codec for Cine masters (H.264 RGB), so Explorer can't draw them: the recorder keeps a small
        // image of its own for each one. It is also written to disk by file name, so the saved copy and later sessions
        // still have it.
        static readonly Dictionary<string, Bitmap> posters = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        public static string PosterDir = Path.Combine(Settings.DataDir, "posters");   // tests point it elsewhere

        public static void SetPoster(string path, Bitmap b)
        {
            if (path == null || b == null) return;
            Remember(path, new Bitmap(b));
            if (Test) return;
            try
            {
                Directory.CreateDirectory(PosterDir);
                b.Save(PosterFile(path), ImageFormat.Png);
            }
            catch (Exception ex) { Log("Portada de " + path + ": " + ex.Message); }
        }

        static void Remember(string path, Bitmap copy)
        {
            lock (posters)
            {
                Bitmap old;
                if (posters.TryGetValue(path, out old)) old.Dispose();
                else if (posters.Count >= 32)
                {
                    foreach (string k in posters.Keys) { posters[k].Dispose(); posters.Remove(k); break; }
                }
                posters[path] = copy;
            }
        }

        static string PosterFile(string path) { return Path.Combine(PosterDir, Path.GetFileName(path) + ".png"); }

        // A copy of the poster (the caller disposes it), or null.
        public static Bitmap Poster(string path)
        {
            if (path == null) return null;
            lock (posters)
            {
                Bitmap b;
                if (posters.TryGetValue(path, out b)) return new Bitmap(b);
            }
            if (!IsCine(path)) return null;
            try
            {
                string f = PosterFile(path);
                if (!File.Exists(f)) return null;
                Bitmap loaded;
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(f)))
                using (Image img = Image.FromStream(ms))
                    loaded = new Bitmap(img);
                Remember(path, new Bitmap(loaded));
                return loaded;
            }
            catch (Exception ex) { Log("Portada de " + path + ": " + ex.Message); return null; }
        }

        // The saved copy of a recording gets the same poster.
        static void CopyPoster(string from, string to)
        {
            if (!IsCine(from) || string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
            using (Bitmap b = Poster(from))
                if (b != null) SetPoster(to, b);
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

        // Called from several threads (and processes: an update runs next to the copy it replaces). Past 1 MB the log
        // rotates to stackshot.log.old, so it never grows unbounded; a rotation that can't happen now (the old file
        // held open) never stops the logging. A line that meets the file busy is retried briefly.
        public static void Log(string msg)
        {
            string path = LogPath;
            if (path == null) return;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine;
            // The interface thread never waits for the disk: its lines go to a writer thread (FlushLog at exit and on a
            // crash). Other threads write as before.
            if (Application.MessageLoop)
            {
                lock (logQueue)
                {
                    logQueue.Add(line);
                    if (logWriter == null)
                    {
                        logWriter = new System.Threading.Thread(LogWriter) { IsBackground = true, Name = "Stackshot log", Priority = System.Threading.ThreadPriority.BelowNormal };
                        logWriter.Start();
                    }
                }
                logWake.Set();
                return;
            }
            FlushLog();   // keeps the order: what the interface thread queued comes first
            WriteLog(path, line);
        }

        static readonly List<string> logQueue = new List<string>();
        static readonly System.Threading.AutoResetEvent logWake = new System.Threading.AutoResetEvent(false);
        static System.Threading.Thread logWriter;

        static void LogWriter()
        {
            while (true)
            {
                logWake.WaitOne();
                FlushLog();
            }
        }

        // Writes the lines still queued, now.
        public static void FlushLog()
        {
            string text;
            lock (logQueue)
            {
                if (logQueue.Count == 0) return;
                text = string.Concat(logQueue.ToArray());
                logQueue.Clear();
            }
            string path = LogPath;
            if (path != null) WriteLog(path, text);
        }

        static void WriteLog(string path, string line)
        {
            lock (logLock)
            {
                try
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxLogBytes)
                    {
                        string old = path + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                }
                catch { }
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { File.AppendAllText(path, line); return; }
                    catch (DirectoryNotFoundException) { return; } // folder gone (uninstalled, cleaned by hand): waiting won't help
                    catch (PathTooLongException) { return; }
                    catch (IOException) { System.Threading.Thread.Sleep(15); }
                    catch { return; }
                }
            }
        }
    }
}
