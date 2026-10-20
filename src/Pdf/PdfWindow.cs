// Stackshot - The PDF viewer, in the spirit of Preview: the pages with a sidebar of thumbnails, marks and search
// results, marking tools, search through the text, and every page ready to become a capture, an image, a print.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Stackshot
{
    // One window per document; opening the same file again brings its window up. Nothing runs while none is open.
    public sealed partial class PdfWindow : Sheet
    {
        static readonly List<PdfWindow> open = new List<PdfWindow>();
        // Kept for the session: the tool colours the user last chose.
        static Color lastMark = PdfMarks.Colors[0], lastInk = PdfMarks.Colors[5];

        readonly ShotStack owner;
        string path, cacheDir, password;
        PdfEngine engine;
        PdfReader reader;
        PdfMarks marks;
        readonly PdfView view = new PdfView();
        readonly DispatcherTimer saveLater = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        int token;
        bool loading;
        FileSystemWatcher watcher;

        Grid page;
        Border sideHost, bannerHost, sayHost, pagePill;
        ColumnDefinition sideCol;
        TextBlock titleLabel, subLabel, zoomLabel, sayLabel, pageLabel;
        readonly Dictionary<PdfTool, DocIconButton> toolButtons = new Dictionary<PdfTool, DocIconButton>();
        SearchField search;
        ColorWell well;
        bool sideOpen = true;
        int sideMode;                     // 0 pages, 1 marks, 2 search results
        readonly DispatcherTimer sayFade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        readonly DispatcherTimer pillFade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };

        // Tests answer dialogs and pick paths here.
        internal static Func<string, string> PickPath { get; set; }
        internal static Func<string> AskPassword { get; set; }

        public static bool IsPdf(string path)
        {
            try { return string.Equals(System.IO.Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        public static IList<PdfWindow> Windows { get { return open.AsReadOnly(); } }

        // Stackshot is closing: every window keeps its marks and where it was (nothing to ask: marks save themselves).
        public static void CloseAll()
        {
            foreach (PdfWindow w in new List<PdfWindow>(open))
            {
                try { w.Close(); }
                catch (Exception ex) { ShotStack.Log("PDF, cerrar: " + ex.Message); }
            }
        }

        // A PDF named on the command line, sent by another copy or opened from the app.
        public static PdfWindow OpenFile(ShotStack owner, string path)
        {
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch { full = path; }
            foreach (PdfWindow w in open)
                if (string.Equals(w.path, full, StringComparison.OrdinalIgnoreCase)) { w.RevealNow(); w.Front(); return w; }
            PdfWindow n = new PdfWindow(owner);
            n.reveal = true;
            open.Add(n);
            n.Load(full, null);
            // Shown with the document in it; a slow file (a sleeping network drive) shows it with its progress meanwhile.
            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            t.Tick += delegate { t.Stop(); n.RevealNow(); };
            t.Start();
            return n;
        }

        bool reveal;

        void RevealNow()
        {
            if (!reveal) return;
            reveal = false;
            if (!IsVisible) Show();
            Front();
        }

        PdfWindow(ShotStack owner)
        {
            this.owner = owner;
            Title = "PDF";
            System.Drawing.Rectangle work = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            double k = 1;
            try { using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96.0; } catch { }
            Width = Math.Min(1180, work.Width / Math.Max(1, k) * 0.8);
            Height = Math.Min(900, work.Height / Math.Max(1, k) * 0.88);
            MinWidth = 640;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = AppIcon;
            AllowDrop = true;
            PreviewKeyDown += OnKey;
            PreviewDragOver += OnDrag;
            PreviewDrop += OnDrop;
            saveLater.Tick += delegate { saveLater.Stop(); SaveMarks(); };
            sayFade.Tick += delegate { sayFade.Stop(); Fade(sayHost, false); };
            pillFade.Tick += delegate { pillFade.Stop(); Fade(pagePill, false); };
            view.ViewChanged += OnViewChanged;
            view.SelectionChanged += OnSelection;
            view.NoteOpen += OpenNote;
            view.TextWanted += delegate(int p) { if (reader != null) { reader.Want(p); Say("Leyendo el texto de la p\u00E1gina\u2026", false); } };
            view.Mark = lastMark;
            view.Ink = lastInk;
            // The floating bar and the note are windows of their own: they leave with the window's focus (never over
            // another app) and follow it when it moves.
            Deactivated += delegate
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                {
                    if (IsActive || (note != null && note.IsKeyboardFocusWithin)) return;
                    CloseNote();
                    CloseBar();
                }));
            };
            Activated += delegate { if (bar == null && note == null && (view.HasText || view.HasZone || view.SelectedMark != null)) ShowBar(); };
            LocationChanged += delegate { Nudge(bar); Nudge(note); };
            StateChanged += delegate { if (WindowState == WindowState.Minimized) { CloseNote(); CloseBar(); } };
            Closed += delegate
            {
                token++;
                saveLater.Stop();
                Unwatch();
                SaveState();
                SaveMarks();
                CloseBar();
                CloseNote();
                view.Detach();
                if (reader != null) reader.Dispose();
                if (engine != null) { PdfEngine gone = engine; System.Threading.ThreadPool.QueueUserWorkItem(delegate { gone.Dispose(); }); }
                open.Remove(this);
            };
            Build();
        }

        // Resizing is done by hit-testing the edges ourselves, as the other Stackshot windows do.
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            System.Windows.Interop.HwndSource src = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
            if (src != null) src.AddHook(EdgeHit);
        }

        IntPtr EdgeHit(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg != 0x84 || WindowState != WindowState.Normal) return IntPtr.Zero;   // WM_NCHITTEST
            int x = (short)((long)lp & 0xFFFF), y = (short)(((long)lp >> 16) & 0xFFFF);
            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return IntPtr.Zero;
            int b = (int)Math.Round(6 * Ink.Scale(this));
            bool l = x < r.Left + b, rt = x >= r.Right - b, t = y < r.Top + b, bt = y >= r.Bottom - b;
            int hit = t ? (l ? 13 : rt ? 14 : 12) : bt ? (l ? 16 : rt ? 17 : 15) : l ? 10 : rt ? 11 : 0;
            if (hit == 0) return IntPtr.Zero;
            handled = true;
            return new IntPtr(hit);
        }

        protected override bool Snaps { get { return true; } }

        protected override void ThemeChanged() { Build(); }

        void Front()
        {
            try
            {
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Topmost = true;
                Topmost = false;
                Activate();
            }
            catch { }
        }

        internal PdfView View { get { return view; } }
        internal PdfMarks Marks { get { return marks; } }
        internal PdfReader Reader { get { return reader; } }
        internal PdfEngine Engine { get { return engine; } }
        internal string FilePath { get { return path; } }
        internal bool Loading { get { return loading; } }
        internal string CacheDir { get { return cacheDir; } }

        // ---- Loading

        void Load(string file, string pass)
        {
            int my = ++token;
            loading = true;
            path = file;
            password = pass;
            SetTitle();
            Say("Abriendo " + System.IO.Path.GetFileName(file) + "\u2026", false, true);
            Dispatcher ui = Dispatcher;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error, key = null;
                PdfEngine e = PdfEngine.Open(file, pass, out error);
                if (e != null) key = Fingerprint(file);
                ui.BeginInvoke(new Action(delegate
                {
                    if (my != token) { if (e != null) e.Dispose(); return; }
                    loading = false;
                    if (e == null) { Failed(file, error); return; }
                    Opened(e, key);
                }));
            });
        }

        void Failed(string file, string error)
        {
            RevealNow();
            if (error == PdfEngine.NeedsPassword)
            {
                string pass = AskPassword != null ? AskPassword() : PdfPassword.Ask(this, System.IO.Path.GetFileName(file), password != null);
                if (pass != null) { Load(file, pass); return; }
                error = "Este PDF tiene contrase\u00F1a.";
            }
            ShotStack.Log("PDF " + file + ": " + error);
            Say(error, true, true);
            ShowEmpty(error);
        }

        // The document's identity: the hash of its bytes, so its marks and text follow it when it is moved or renamed.
        static string Fingerprint(string file)
        {
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] h = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < 16; i++) sb.Append(h[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception ex) { ShotStack.Log("PDF, huella: " + ex.Message); return null; }
        }

        public static string CacheRoot { get { return System.IO.Path.Combine(Settings.DataDir, "pdf"); } }

        void Opened(PdfEngine e, string key)
        {
            if (engine != null) { PdfEngine gone = engine; System.Threading.ThreadPool.QueueUserWorkItem(delegate { gone.Dispose(); }); }
            if (reader != null) reader.Dispose();
            engine = e;
            cacheDir = key == null ? null : System.IO.Path.Combine(CacheRoot, key);
            marks = PdfMarks.Load(cacheDir == null ? null : System.IO.Path.Combine(cacheDir, "marks.json"));
            marks.Dirty = false;
            marks.SinceExport = marks.All.Count > 0;
            marks.Changed += OnMarksChanged;
            reader = new PdfReader(e, cacheDir == null ? null : System.IO.Path.Combine(cacheDir, "text.json"), Dispatcher);
            reader.Read += OnTextRead;
            thumbs = new Dictionary<int, BitmapSource>();
            view.Thumbs = thumbs;
            view.Attach(e, reader, marks);
            RestoreState();
            Build();
            SetTitle();
            CloseSay();
            Watch();
            // The page shown is read first; the rest follow quietly, so search and selection find the text ready.
            reader.Want(view.CurrentPage);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Prune(); });
            // Sticky notes inside the PDF (Acrobat's or anyone's): Windows' engine leaves them out, so they are shown here.
            string file = path;
            PdfMarks shown = marks;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<PdfMark> notes = null;
                try { if (new FileInfo(file).Length <= PdfEngine.MemoryLimit) notes = PdfAnnotWriter.ReadNotes(File.ReadAllBytes(file)); }
                catch (Exception ex) { ShotStack.Log("PDF, notas: " + ex.Message); }
                if (notes == null || notes.Count == 0) return;
                Dispatcher.BeginInvoke(new Action(delegate { if (marks == shown) marks.FromFile(notes); }));
            });
            RevealNow();
        }

        // Old documents' folders go once there are many (most recently used kept).
        static void Prune()
        {
            try
            {
                DirectoryInfo root = new DirectoryInfo(CacheRoot);
                if (!root.Exists) return;
                List<DirectoryInfo> dirs = new List<DirectoryInfo>(root.GetDirectories());
                if (dirs.Count <= 150) return;
                dirs.Sort(delegate(DirectoryInfo a, DirectoryInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                for (int i = 150; i < dirs.Count; i++)
                {
                    // Marks are the user's work: a folder with marks is kept whatever its age.
                    if (File.Exists(System.IO.Path.Combine(dirs[i].FullName, "marks.json"))) continue;
                    try { dirs[i].Delete(true); } catch { }
                }
            }
            catch { }
        }

        void SetTitle()
        {
            string name = path == null ? "PDF" : System.IO.Path.GetFileName(path);
            Title = name;
            if (titleLabel != null) titleLabel.Text = name;
            SyncSub();
        }

        void SyncSub()
        {
            if (subLabel == null) return;
            if (engine == null) { subLabel.Text = loading ? "Abriendo\u2026" : ""; return; }
            int p = view.CurrentPage;
            subLabel.Text = "P\u00E1gina " + (p + 1).ToString("N0") + " de " + engine.Pages.ToString("N0");
            if (pageLabel != null) pageLabel.Text = (p + 1).ToString("N0") + " de " + engine.Pages.ToString("N0");
        }

        // ---- State kept per document: where the user was and how it was shown

        void RestoreState()
        {
            if (cacheDir == null) return;
            try
            {
                string f = System.IO.Path.Combine(cacheDir, "state.json");
                if (!File.Exists(f)) return;
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>;
                if (d == null) return;
                int fit = d.ContainsKey("fit") ? Convert.ToInt32(d["fit"], CultureInfo.InvariantCulture) : 3;
                double zoom = d.ContainsKey("zoom") ? Convert.ToDouble(d["zoom"], CultureInfo.InvariantCulture) : 1;
                int pg = d.ContainsKey("page") ? Convert.ToInt32(d["page"], CultureInfo.InvariantCulture) : 0;
                if (d.ContainsKey("sidebar")) sideOpen = Convert.ToInt32(d["sidebar"], CultureInfo.InvariantCulture) != 0;
                // Once laid out (the zoom to fit needs the size of the window).
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
                {
                    if (engine == null) return;
                    if (fit == 0) view.SetZoom(zoom, null); else if (fit != 3) view.SetFit(fit);
                    view.GoTo(pg, false);
                }));
            }
            catch (Exception ex) { ShotStack.Log("PDF, estado: " + ex.Message); }
        }

        void SaveState()
        {
            if (cacheDir == null || engine == null || Settings.ReadOnly && cacheDir.StartsWith(Settings.DataDir, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                Directory.CreateDirectory(cacheDir);
                string json = "{\"page\":" + view.CurrentPage + ",\"zoom\":" + view.Zoom.ToString("0.###", CultureInfo.InvariantCulture) +
                              ",\"fit\":" + view.Fit + ",\"sidebar\":" + (sideOpen ? 1 : 0) + ",\"name\":" + PdfReader.Json(System.IO.Path.GetFileName(path)) + "}";
                AtomicFile.Write(System.IO.Path.Combine(cacheDir, "state.json"), new UTF8Encoding(false).GetBytes(json));
            }
            catch (Exception ex) { ShotStack.Log("PDF, guardar estado: " + ex.Message); }
        }

        void OnMarksChanged()
        {
            view.InvalidateVisual();
            saveLater.Stop();
            saveLater.Start();
            if (sideMode == 1) FillSide();
            ShowBarSoon();
        }

        void SaveMarks()
        {
            if (marks == null || !marks.Dirty || cacheDir == null) return;
            if (Settings.ReadOnly && cacheDir.StartsWith(Settings.DataDir, StringComparison.OrdinalIgnoreCase)) return;
            marks.Save(System.IO.Path.Combine(cacheDir, "marks.json"));
        }

        void OnTextRead(int p)
        {
            if (p == view.CurrentPage || (view.selPage < 0 && p == view.zonePage)) CloseSay();
            view.InvalidateVisual();
            if (searchQuery.Length > 0) SearchPage(p);
        }

        // ---- Layout

        void Build()
        {
            Palette pal = Ds.Brushes;
            CloseBar();
            if (page != null) Root.Children.Remove(page);
            Root.Background = Ds.Brush(pal.Window);
            page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            Root.Children.Add(page);
            KeepCaptionsOnTop();

            page.Children.Add(Toolbar(pal));
            bannerHost = new Border();
            Grid.SetRow(bannerHost, 1);
            page.Children.Add(bannerHost);

            Grid body = new Grid();
            Grid.SetRow(body, 2);
            sideCol = new ColumnDefinition { Width = new GridLength(sideOpen && engine != null ? SideWidth : 0) };
            body.ColumnDefinitions.Add(sideCol);
            body.ColumnDefinitions.Add(new ColumnDefinition());
            sideHost = new Border { Background = Ds.Brush(pal.Sidebar), BorderBrush = Ds.Brush(pal.Separator), BorderThickness = new Thickness(0, 0, 1, 0), ClipToBounds = true };
            body.Children.Add(sideHost);
            if (view.Parent is Panel) ((Panel)view.Parent).Children.Remove(view);
            Grid main = new Grid();
            Grid.SetColumn(main, 1);
            main.Children.Add(view);
            pagePill = Pill(pal, out pageLabel);
            pagePill.VerticalAlignment = VerticalAlignment.Bottom;
            pagePill.Margin = new Thickness(0, 0, 0, 18);
            pagePill.Opacity = 0;
            main.Children.Add(pagePill);
            sayHost = Pill(pal, out sayLabel);
            sayHost.VerticalAlignment = VerticalAlignment.Top;
            sayHost.Margin = new Thickness(0, 14, 0, 0);
            sayHost.Opacity = 0;
            main.Children.Add(sayHost);
            body.Children.Add(main);
            page.Children.Add(body);
            proxy = new Border { Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            Grid.SetRowSpan(proxy, 3);
            page.Children.Add(proxy);
            FillSide();
            SyncTools();
            SyncSub();
            OnViewChanged();
            if (engine == null && !loading) ShowEmpty(null);
        }

        const double SideWidth = 200;

        static Border Pill(Palette pal, out TextBlock label)
        {
            label = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(Color.FromRgb(255, 255, 255)), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 520 };
            return new Border
            {
                Background = Ds.Brush(Ds.Argb(0.78, 28, 28, 30)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false,
                Child = label
            };
        }

        FrameworkElement Toolbar(Palette pal)
        {
            Grid bar = new Grid { Background = Brushes.Transparent };
            bar.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
            {
                if (e.OriginalSource != bar && !(e.OriginalSource is TextBlock)) return;
                if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else try { DragMove(); } catch { }
            };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) }); // the caption buttons

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
            DocIconButton side = new DocIconButton("sidebar", "Barra lateral (Ctrl+Alt+S)", 30) { On = sideOpen && engine != null };
            side.IsEnabled = engine != null;
            side.Click += ToggleSide;
            left.Children.Add(side);
            StackPanel names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), MaxWidth = 300 };
            titleLabel = new TextBlock { FontFamily = Ds.Text, FontWeight = FontWeights.SemiBold, FontSize = 13.5, Foreground = Ds.Brush(pal.Label), TextTrimming = TextTrimming.CharacterEllipsis };
            subLabel = new TextBlock { FontSize = 11.5, Foreground = Ds.Brush(pal.Label2), TextTrimming = TextTrimming.CharacterEllipsis };
            names.Children.Add(titleLabel);
            names.Children.Add(subLabel);
            left.Children.Add(names);
            bar.Children.Add(left);

            // The marking tools, centred like Preview's markup bar.
            StackPanel tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tools, 1);
            toolButtons.Clear();
            AddTool(tools, PdfTool.Select, "textcursor", "Seleccionar texto (V)");
            AddTool(tools, PdfTool.Highlight, "highlight", "Resaltar (H)");
            AddTool(tools, PdfTool.Underline, "underline", "Subrayar (U)");
            AddTool(tools, PdfTool.Strike, "strike", "Tachar (K)");
            AddTool(tools, PdfTool.Note, "sticky", "Nota (N)");
            AddTool(tools, PdfTool.Ink, "edit", "L\u00E1piz (P)");
            AddTool(tools, PdfTool.Zone, "area", "Zona: capturarla, copiarla o leer su texto (R)");
            well = new ColorWell(view.Tool == PdfTool.Ink ? view.Ink : view.Mark) { Margin = new Thickness(6, 0, 0, 0) };
            well.Click += delegate { ColorMenu(well); };
            tools.Children.Add(well);
            bar.Children.Add(tools);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
            Grid.SetColumn(right, 2);
            DocIconButton zout = new DocIconButton("zoomout", "Alejar (Ctrl+\u2212)", 30);
            zout.Click += delegate { view.ZoomStep(-1); };
            DocIconButton zin = new DocIconButton("zoomin", "Acercar (Ctrl++)", 30);
            zin.Click += delegate { view.ZoomStep(1); };
            Border zoomBox = new Border { Width = 56, Height = 30, CornerRadius = new CornerRadius(7), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Tama\u00F1o" };
            zoomLabel = new TextBlock { FontSize = 12.5, Foreground = Ds.Brush(pal.Label), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            zoomBox.Child = zoomLabel;
            zoomBox.MouseEnter += delegate { zoomBox.Background = Ds.Brush(pal.ControlHover); };
            zoomBox.MouseLeave += delegate { zoomBox.Background = Brushes.Transparent; };
            zoomBox.MouseLeftButtonUp += delegate { ZoomMenu(zoomBox); };
            foreach (FrameworkElement f in new FrameworkElement[] { zout, zoomBox, zin }) { f.IsEnabled = engine != null; right.Children.Add(f); }
            search = new SearchField(pal, "Buscar") { Width = 200 };
            search.HideFilter();
            search.Text = searchQuery;
            search.Changed += delegate(string q) { searchLater.Restart(); };
            search.Step += delegate(int d) { StepHit(d); };
            search.IsEnabled = engine != null;
            right.Children.Add(search);
            DocIconButton share = new DocIconButton("camera", "Capturar, copiar y exportar", 30) { Margin = new Thickness(6, 0, 0, 0) };
            share.IsEnabled = engine != null;
            share.Click += delegate { ShareMenu(share); };
            right.Children.Add(share);
            DocIconButton more = new DocIconButton("more", "M\u00E1s opciones", 30);
            more.Click += delegate { MoreMenu(more); };
            right.Children.Add(more);
            bar.Children.Add(right);
            SetTitle();
            return bar;
        }

        void AddTool(Panel p, PdfTool t, string icon, string tip)
        {
            DocIconButton b = new DocIconButton(icon, tip, 30) { On = view.Tool == t, Margin = new Thickness(1, 0, 1, 0) };
            b.IsEnabled = engine != null;
            b.Click += delegate { SetTool(view.Tool == t && t != PdfTool.Select ? PdfTool.Select : t); };
            toolButtons[t] = b;
            p.Children.Add(b);
        }

        internal void SetTool(PdfTool t)
        {
            // A marking tool picked with words selected marks them at once, as in Preview.
            if ((t == PdfTool.Highlight || t == PdfTool.Underline || t == PdfTool.Strike) && view.HasText)
            {
                view.MarkWords(PdfView.KindFor(t));
                return;
            }
            view.Tool = t;
            SyncTools();
            if (t != PdfTool.Select && t != PdfTool.Note && reader != null) reader.Want(view.CurrentPage);
        }

        void SyncTools()
        {
            foreach (KeyValuePair<PdfTool, DocIconButton> kv in toolButtons)
            {
                kv.Value.On = kv.Key == view.Tool;
                kv.Value.InvalidateVisual();
            }
            if (well != null) well.Color = view.Tool == PdfTool.Ink ? view.Ink : view.Mark;
        }

        void ToggleSide()
        {
            if (engine == null) return;
            sideOpen = !sideOpen;
            if (sideCol != null)
            {
                GridLengthAnimationless(sideCol, sideOpen ? SideWidth : 0);
            }
            Build();
        }

        static void GridLengthAnimationless(ColumnDefinition c, double w) { c.Width = new GridLength(w); }

        void OnViewChanged()
        {
            if (zoomLabel != null) zoomLabel.Text = Math.Round(view.Zoom * 100).ToString(CultureInfo.CurrentCulture) + " %";
            int before = lastPage;
            SyncSub();
            if (engine == null) return;
            int p = view.CurrentPage;
            if (p != before)
            {
                lastPage = p;
                if (reader != null) reader.Want(p);
                if (thumbStrip != null) thumbStrip.Current = p;
                ShowPill();
            }
            PlaceBar();
        }

        int lastPage = -1;

        void ShowPill()
        {
            if (pagePill == null || engine == null || engine.Pages < 2) return;
            Fade(pagePill, true);
            pillFade.Stop();
            pillFade.Start();
        }

        static void Fade(UIElement e, bool on)
        {
            if (e == null) return;
            DoubleAnimation a = new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(on ? 120 : 260));
            e.BeginAnimation(OpacityProperty, a);
        }

        // A short message over the pages; sticky ones stay until replaced.
        internal void Say(string text, bool bad) { Say(text, bad, false); }

        void Say(string text, bool bad, bool sticky)
        {
            if (sayLabel == null) return;
            sayLabel.Text = text;
            sayHost.Background = Ds.Brush(bad ? Ds.Argb(0.9, 200, 50, 40) : Ds.Argb(0.78, 28, 28, 30));
            Fade(sayHost, true);
            sayFade.Stop();
            if (!sticky) sayFade.Start();
            lastSay = text;
        }

        internal string lastSay;

        void CloseSay() { sayFade.Stop(); Fade(sayHost, false); }

        void ShowEmpty(string error)
        {
            Palette pal = Ds.Brushes;
            if (page == null) return;
            StackPanel s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            s.Children.Add(new GlyphView("doc", 46, pal.Label3, 1.4) { HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = error == null ? "Suelta aqu\u00ED un PDF" : "No se ha podido abrir", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 20, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = error ?? "O \u00E1brelo con Ctrl+O.", FontSize = 13, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, TextAlignment = TextAlignment.Center });
            MacButton b = new MacButton("Abrir un PDF\u2026", ButtonKind.Secondary) { Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            b.Click += Browse;
            s.Children.Add(b);
            Grid.SetRow(s, 2);
            page.Children.Add(s);
        }

        void Browse()
        {
            using (System.Windows.Forms.OpenFileDialog d = new System.Windows.Forms.OpenFileDialog())
            {
                d.Title = "Abrir PDF";
                d.Filter = "PDF|*.pdf|Todos los archivos|*.*";
                if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                if (engine == null) Load(d.FileName, null);
                else OpenFile(owner, d.FileName);
            }
        }

        // ---- The file on disk

        void Watch()
        {
            Unwatch();
            try
            {
                watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(path));
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                FileSystemEventHandler on = delegate { Dispatcher.BeginInvoke(new Action(ChangedOnDisk)); };
                watcher.Changed += on;
                watcher.Deleted += on;
                watcher.Renamed += delegate { Dispatcher.BeginInvoke(new Action(ChangedOnDisk)); };
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) { ShotStack.Log("PDF, vigilar: " + ex.Message); }
        }

        void Unwatch()
        {
            if (watcher == null) return;
            try { watcher.EnableRaisingEvents = false; watcher.Dispose(); } catch { }
            watcher = null;
        }

        // Saved again by another program (or by Stackshot, writing the marks in): a banner offers to show it again.
        internal bool ignoreDisk;

        void ChangedOnDisk()
        {
            if (ignoreDisk || bannerHost == null || engine == null) return;
            Palette pal = Ds.Brushes;
            bool gone = !File.Exists(path);
            Grid g = new Grid { Margin = new Thickness(12, 0, 12, 8) };
            Border b = new Border { CornerRadius = new CornerRadius(8), Background = Ds.Brush(Ds.WithAlpha(pal.Orange, pal.Dark ? 0.2 : 0.14)), Padding = new Thickness(12, 7, 8, 7) };
            DockPanel d = new DockPanel();
            if (!gone)
            {
                MacButton reload = new MacButton("Volver a cargar", ButtonKind.Secondary);
                reload.Click += delegate { bannerHost.Child = null; Reload(); };
                DockPanel.SetDock(reload, Dock.Right);
                d.Children.Add(reload);
            }
            d.Children.Add(new TextBlock { Text = gone ? "El archivo ya no est\u00E1 en su carpeta: sigues viendo la copia que se abri\u00F3." : "El archivo ha cambiado en el disco.", VerticalAlignment = VerticalAlignment.Center, Foreground = Ds.Brush(pal.Label), FontSize = 12.5 });
            b.Child = d;
            g.Children.Add(b);
            bannerHost.Child = g;
        }

        internal void Reload()
        {
            SaveMarks();
            SaveState();
            Load(path, password);
        }

        // ---- Keys

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) { if (e.Key == Key.Escape && search != null && search.Box.IsKeyboardFocused) { view.Focus(); e.Handled = true; } return; }
            Key k = e.Key == Key.System ? e.SystemKey : e.Key;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            if (ctrl && k == Key.W) { Close(); e.Handled = true; return; }
            if (ctrl && k == Key.O) { Browse(); e.Handled = true; return; }
            if (engine == null) return;
            if (ctrl && k == Key.F) { search.Box.Focus(); search.Box.SelectAll(); e.Handled = true; return; }
            if (k == Key.F3) { StepHit(shift ? -1 : 1); e.Handled = true; return; }
            if (ctrl && k == Key.P) { Print(); e.Handled = true; return; }
            if (ctrl && k == Key.C) { Copy(); e.Handled = true; return; }
            if (ctrl && k == Key.A) { SelectPage(); e.Handled = true; return; }
            if (ctrl && !shift && k == Key.Z) { if (marks.Undo()) Say("Deshecho", false); e.Handled = true; return; }
            if (ctrl && (k == Key.Y || (shift && k == Key.Z))) { if (marks.Redo()) Say("Rehecho", false); e.Handled = true; return; }
            if (ctrl && (k == Key.OemPlus || k == Key.Add)) { view.ZoomStep(1); e.Handled = true; return; }
            if (ctrl && (k == Key.OemMinus || k == Key.Subtract)) { view.ZoomStep(-1); e.Handled = true; return; }
            if (ctrl && (k == Key.D0 || k == Key.NumPad0)) { view.SetFit(1); e.Handled = true; return; }
            if (ctrl && (k == Key.D1 || k == Key.NumPad1)) { view.SetZoom(1, null); e.Handled = true; return; }
            if (ctrl && alt && k == Key.S) { ToggleSide(); e.Handled = true; return; }
            if (ctrl && shift && k == Key.S) { ExportPdf(); e.Handled = true; return; }
            if (ctrl && k == Key.G) { GoToPrompt(); e.Handled = true; return; }
            if ((k == Key.Delete || k == Key.Back) && view.SelectedMark != null) { DeleteSelected(); e.Handled = true; return; }
            if (k == Key.Escape)
            {
                if (view.HasText || view.HasZone || view.SelectedMark != null) view.ClearSelection(true);
                else if (view.Tool != PdfTool.Select) SetTool(PdfTool.Select);
                else if (searchQuery.Length > 0) search.Text = "";
                e.Handled = true;
                return;
            }
            if (!ctrl && !alt)
            {
                PdfTool t;
                if (ToolKey(k, out t)) { SetTool(t); e.Handled = true; return; }
            }
            if (view.HandleKey(e)) e.Handled = true;
        }

        static bool ToolKey(Key k, out PdfTool t)
        {
            switch (k)
            {
                case Key.V: t = PdfTool.Select; return true;
                case Key.H: t = PdfTool.Highlight; return true;
                case Key.U: t = PdfTool.Underline; return true;
                case Key.K: t = PdfTool.Strike; return true;
                case Key.N: t = PdfTool.Note; return true;
                case Key.P: t = PdfTool.Ink; return true;
                case Key.R: t = PdfTool.Zone; return true;
            }
            t = PdfTool.Select;
            return false;
        }

        // Ctrl+A: the words of the page shown (once its text is read).
        void SelectPage()
        {
            int p = view.CurrentPage;
            PdfPageText t = reader.Get(p);
            if (t == null) { reader.Want(p); Say("Leyendo el texto de la p\u00E1gina\u2026", false); return; }
            if (t.Empty) { Say("Esta p\u00E1gina no tiene texto que se pueda leer.", false); return; }
            view.SelectWords(p, 0, t.Words.Count - 1);
        }

        void OnDrag(object sender, DragEventArgs e)
        {
            e.Effects = PdfIn(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            string f = PdfIn(e.Data);
            if (f == null) return;
            e.Handled = true;
            if (engine == null) Load(f, null);
            else OpenFile(owner, f);
        }

        static string PdfIn(IDataObject d)
        {
            try
            {
                string[] files = d.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return null;
                foreach (string f in files) if (IsPdf(f) && File.Exists(f)) return f;
            }
            catch { }
            return null;
        }
    }

    // The colour of the tool in use: a round swatch, as in Preview's markup bar.
    public sealed class ColorWell : Drawn
    {
        Color color;
        public event Action Click;

        public ColorWell(Color c)
        {
            color = c;
            Width = Height = 30;
            ToolTip = "Color";
            System.Windows.Automation.AutomationProperties.SetName(this, "Color");
            AcceptFocus();
        }

        public Color Color { get { return color; } set { color = value; InvalidateVisual(); } }

        protected override void Clicked(Point p) { Action h = Click; if (h != null) h(); }
        protected override bool EnterActivates { get { return true; } }
        protected override double RingRadius { get { return 15; } }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double w = ActualWidth, h = ActualHeight;
            if (Hot > 0 || Down > 0) dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.ControlHover, Math.Min(1, Hot + Down * 0.6))), null, new Rect(0, 0, w, h), 6, 6);
            Point c = new Point(w / 2, h / 2);
            dc.DrawEllipse(Ds.Brush(color), new Pen(Ds.Brush(pal.Dark ? Ds.Argb(0.5, 255, 255, 255) : Ds.Argb(0.25, 0, 0, 0)), 1), c, 8, 8);
        }
    }
}
