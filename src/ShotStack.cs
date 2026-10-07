// Stackshot - La pila: captura, miniaturas, portapapeles, limpieza y el icono de la bandeja.
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
    // Recibe los atajos, captura y apila una miniatura por cada captura (abajo a la izquierda, como CleanShot X).
    // Solo se conserva lo que se guarda con 💾: lo demás vive en una carpeta temporal y se borra solo.
    public class ShotStack : ApplicationContext
    {
        public static string LogPath;
        public static Icon AppIcon;
        public static bool Test;           // --test: no toca el portapapeles ni los atajos
        const int MaxCards = 20;           // las que no caben en pantalla se ven con la rueda del ratón
        const double FollowDelay = 350;    // ms que el ratón tiene que quedarse en otra pantalla para llevarse la pila
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
        int scroll, wheelAcc, pageSize = 1;    // scroll: cuántas de las más recientes quedan ocultas por arriba

        public ShotStack(Settings settings, bool justInstalled, bool showHome)
        {
            this.settings = settings;
            folder = Settings.TempDir;
            sync.CreateControl();
            IntPtr forceHandle = sync.Handle;
            Directory.CreateDirectory(folder);
            upChip = new Chip(this, -1);
            downChip = new Chip(this, 1);

            // La carpeta temporal se vigila para refrescar una miniatura si su fichero cambia (el editor la reescribe)
            // y para quitarla si alguien lo borra o lo mueve.
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
                // Clic normal en el icono: el mismo menú que con el derecho.
                if (e.Button != MouseButtons.Left) return;
                MethodInfo show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (show != null) show.Invoke(tray, null);
            };
            tray.Visible = true;
            if (!Test) closeListener = new CloseListener(delegate { sync.BeginInvoke((Action)ExitThread); });

            hotkeys = new Hotkeys();
            hotkeys.Pressed += OnHotkey;
            RegisterHotkeys(true);

            // Otra copia (una actualización, el desinstalador) puede pedirle que se cierre bien.
            if (!Test)
            {
                try
                {
                    quitEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.QuitEvent);
                    System.Threading.ThreadPool.RegisterWaitForSingleObject(quitEvent,
                        delegate { sync.BeginInvoke((Action)delegate { Log("Cierre pedido por otra copia"); ExitThread(); }); }, null, -1, true);
                }
                catch (Exception ex) { Log("Evento de salida: " + ex.Message); }
                // Abrir Stackshot otra vez (menú Inicio, el .exe) con esta ya en marcha: se enseña la ventana.
                try
                {
                    showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.ShowEvent);
                    WaitShow();
                }
                catch (Exception ex) { Log("Evento de mostrar: " + ex.Message); }
            }

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            Log("Stackshot " + Installer.MyVersion.ToString(3) + " en marcha (" + Installer.ExePath + ")");

            // En modo prueba no se limpia nada: la carpeta temporal es la de la copia de verdad.
            if (!Test)
            {
                sweepTimer.Interval = 5 * 60 * 1000;
                sweepTimer.Tick += delegate { Sweep(); };
                sweepTimer.Start();
                Sweep();
            }

            Backdrop.Prewarm();
            if (justInstalled || showHome) sync.BeginInvoke((Action)delegate { ShowHome("home", true); });
        }

        System.Threading.EventWaitHandle showEvent;
        CloseListener closeListener;

        void WaitShow()
        {
            System.Threading.ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
            {
                if (exiting) return;
                sync.BeginInvoke((Action)delegate { ShowHome("home", true); });
                WaitShow();
            }, null, -1, true);
        }

        // ------------------------------------------------------------ Bandeja y atajos

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
            // Se espera a que el menú se cierre del todo, para que no salga en la captura.
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
            tray.BalloonTipTitle = "Algunos atajos est\u00E1n ocupados";
            tray.BalloonTipText = string.Join(", ", failed.ToArray()) + " los usa otro programa (\u00BFRecortes, ShareX, Lightshot\u2026?). " +
                                  "Ci\u00E9rralo o elige otros atajos en Ajustes.";
            tray.BalloonTipIcon = ToolTipIcon.Warning;
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

        // ------------------------------------------------------------ Ventana principal

        public Settings Settings { get { return settings; } }

        // Abre (o trae al frente) la ventana principal en esa sección. intro: con la animación de bienvenida.
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

        // Mientras se elige un atajo en la ventana, que los globales no se coman las teclas.
        public void SuspendHotkeys() { if (!Test) hotkeys.Clear(); }
        public void ResumeHotkeys() { RegisterHotkeys(true); }

        // Una acción pedida desde la ventana (que ya se ha escondido para no salir en la captura).
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

        // ------------------------------------------------------------ Capturar

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

        // En Windows 11 las ventanas normales (no maximizadas) tienen esquinas redondeadas.
        static bool WindowIsRounded(IntPtr h, Rectangle r)
        {
            if (Environment.OSVersion.Version.Build < 22000) return false;
            Screen scr = Screen.FromRectangle(r);
            return r != scr.Bounds && r != scr.WorkingArea;
        }

        // Guarda la captura en la carpeta temporal, la copia (si está activado) y apila su miniatura.
        // Para que se sienta instantánea: el sonido va primero, la miniatura sale de la imagen en memoria y el
        // PNG (lo más lento, cientos de ms en pantallas grandes) se escribe en otro hilo. Quien necesite el
        // fichero antes de tiempo espera con WaitWritten.
        public void SaveCapture(Bitmap shot, string name)
        {
            if (settings.Sound) Shutter.Play();
            Directory.CreateDirectory(folder);
            string path = Unique(Path.Combine(folder, Clean(name) + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png"));
            Size size = shot.Size;
            Bitmap preview = Preview(shot, 600);
            Bitmap forClipboard = settings.CopyToClipboard && !Test ? new Bitmap(shot) : null;
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
            if (Captured != null) Captured(path);
        }

        // Para la ventana principal (la mascota celebra cada captura).
        public event Action<string> Captured;

        // Ficheros que se están escribiendo en segundo plano.
        static readonly HashSet<string> writing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Se escribe con otro nombre y se renombra al acabar: así nadie lee un PNG a medias y el vigilante de la
        // carpeta no lo confunde con una edición.
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

        // Espera (como mucho 10 s) a que el PNG de una captura recién hecha esté en el disco.
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

        // Una grabación terminada: a la pila y, como fichero, al portapapeles (para pegarla en un chat).
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

        // Ocupado en el disco o reservado por una captura que aún se está escribiendo.
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

        // Ejecuta algo en el hilo de la interfaz (desde la grabación, que va en otro hilo).
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

        // ------------------------------------------------------------ Guardar y limpiar

        // Copia la captura a la carpeta de guardadas (o actualiza la copia si ya estaba guardada).
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

        // Borra (sin Papelera) las capturas temporales de más de una hora que ya no están en pantalla.
        // Solo dentro de %LOCALAPPDATA%\Stackshot\temp: nunca en otra carpeta.
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
                // También los ".png.part" que dejaría un cierre a mitad de escritura.
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

        // Las capturas pequeñas se ven al doble para poder marcarlas; las normales, como mucho a tamaño real.
        public static float MaxZoom(Size img)
        {
            return img.Width < 600 && img.Height < 400 ? 2f : 1f;
        }

        public static bool IsMediaFile(string path)
        {
            return Array.IndexOf(MediaExts, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        // Lo que el editor sabe abrir: imágenes, vídeos y GIF.
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

        // Tamaño si el fichero ya está cerrado por quien lo escribe; -1 si sigue abierto.
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

        // ------------------------------------------------------------ Miniaturas

        bool AddCard(string p)
        {
            return AddCard(p, null, Size.Empty);
        }

        bool AddCard(string p, Bitmap preview, Size orig)
        {
            Card c = new Card(this, p);
            if (preview != null) c.UsePreview(preview, orig);
            else if (!c.Reload()) { c.Dispose(); Log("No se pudo leer " + p); return false; }
            // La captura sale en la pantalla donde se ha hecho; si la pila estaba en otra, se viene con ella.
            anchorDevice = Screen.FromPoint(Control.MousePosition).DeviceName;
            followCandidate = null;
            cards.Add(c);
            scroll = 0; // una captura nueva siempre se ve: la pila vuelve a lo más reciente
            Relayout();
            if (!c.Parked)
            {
                c.Show();
                Native.SetWindowPos(c.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // TOPMOST sin activar
            }
            follow.Start();
            return true;
        }

        public void Remove(Card c)
        {
            Remove(c, Card.Exit.Slide);
        }

        // La miniatura empieza a irse ya; las de encima bajan a ocupar su hueco mientras se desvanece.
        public void Remove(Card c, Card.Exit how)
        {
            if (!c.IsDisposed) c.Dismiss(how, 0);
            sync.BeginInvoke((Action)delegate
            {
                if (cards.Remove(c)) Relayout();
            });
        }

        // Copia vigilada: al pegarla en otra aplicación, su miniatura se da por usada y se quita.
        // sameAsFile: la imagen es exactamente la del fichero (PNG): el PNG del portapapeles se lee de él, sin recodificar.
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
                // Vídeo o GIF: modo presentación (marcas y fondo sobre toda la grabación).
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

        // Se van en cascada, de arriba abajo.
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
            // Esa pantalla ya no está (portátil desenchufado, etc.): a la del ratón.
            Screen m = Screen.FromPoint(Control.MousePosition);
            anchorDevice = m.DeviceName;
            return m;
        }

        // La pila sigue al ratón: si se queda un momento en otra pantalla, las miniaturas se van con él.
        // No se mueve mientras se pulsa, se desliza o se arrastra algo (ni siquiera fuera de la pila).
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

        // Abajo a la izquierda: la primera captura se queda abajo y las siguientes se apilan encima, en orden.
        // Si no caben todas, se ven las más recientes y una pastilla abajo dice cuántas hay debajo; con la rueda
        // del ratón (o un clic en la pastilla) la pila se desplaza para verlas. Hay sitio para 20: después se va
        // la más antigua (el fichero se queda en la carpeta temporal).
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

            // Las que no caben se aparcan por su lado: las anteriores por abajo y las más recientes por arriba.
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

        // Con la de arriba del todo en top, hasta qué índice caben hacia abajo (reservando sitio para las pastillas).
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

        // Con la más antigua abajo del todo, hasta qué índice caben: marca hasta dónde se puede desplazar.
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

        // Rueda del ratón sobre la pila: hacia abajo enseña las anteriores; hacia arriba, las más recientes.
        public void Wheel(int delta)
        {
            wheelAcc += delta;
            int steps = 0;
            while (wheelAcc >= 120) { wheelAcc -= 120; steps--; }
            while (wheelAcc <= -120) { wheelAcc += 120; steps++; }
            if (steps != 0) ScrollBy(steps);
        }

        // Clic en una pastilla: una página entera hacia ese lado.
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
            sync.BeginInvoke((Action)Relayout);
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
                Process.Start("explorer.exe", "\"" + settings.SaveFolder + "\"");
            }
            catch (Exception ex) { Log("Carpeta: " + ex.Message); }
        }

        protected override void ExitThreadCore()
        {
            // La copia vigilada vive en este proceso: dejarla fija en el portapapeles antes de salir.
            exiting = true;
            Recorder.Stop();
            ScrollCapture.Stop();
            if (home != null && !home.IsDisposed) home.Dispose();
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
            upChip.Close();
            downChip.Close();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        // ------------------------------------------------------------ Utilidades

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

        // Copia reducida (como mucho maxDim de lado) para las miniaturas.
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

        // Imagen al portapapeles como bitmap y como PNG (lo que prefieren los chats web).
        public static void CopyImage(Bitmap bmp)
        {
            MemoryStream png = new MemoryStream();
            bmp.Save(png, ImageFormat.Png);
            DataObject d = new DataObject();
            d.SetData(DataFormats.Bitmap, true, bmp);
            d.SetData("PNG", false, png);
            Clipboard.SetDataObject(d, true, 10, 100);
        }

        // Miniatura del Explorador (vídeos y GIF).
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

        // Recursos metidos dentro del .exe (el icono y el logo).
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

        public static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine); }
            catch { }
        }
    }
}
