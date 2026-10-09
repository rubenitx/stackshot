// Stackshot - Markdown and XML viewer: paste or drop a document, read it, tidy it and turn it into a capture.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Stackshot
{
    // Created on first use and gone when closed: nothing runs while it is not open (its timers are one-shot).
    public sealed class MarkdownWindow : Sheet
    {
        static MarkdownWindow open;

        readonly ShotStack owner;
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        readonly DispatcherTimer fade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        string text, name, status;
        bool editing, modified, statusBad, dragging, compact;
        int token;
        double split = 1, fullWidth;
        StackPanel tools;
        ColumnDefinition editCol, viewCol;
        Grid page;
        Border previewHost;
        MarkdownEditor editor;
        TextBlock statusLabel, errorLabel;
        Rectangle dash;
        MarkdownFlow flow;
        MacSegmented mode;
        // XML: the parsed tree (for the text it was parsed from), the tree on screen, the search box and the last error.
        bool xml;
        XmlModel model;
        string parsedText, xmlError, selPath;
        int errLine, errCol;
        XmlTreeView tree;
        SearchField search;
        string query = "";
        FrameworkElement proxy;

        public static void Open(ShotStack owner, Settings settings)
        {
            Show(owner);
        }

        // A file named on the command line, or sent by another copy of Stackshot.
        public static void OpenFile(ShotStack owner, string path)
        {
            MarkdownWindow w = Show(owner);
            w.LoadFile(path);
        }

        static MarkdownWindow Show(ShotStack owner)
        {
            if (open != null)
            {
                if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
                open.Activate();
                return open;
            }
            open = new MarkdownWindow(owner);
            open.Show();
            open.Activate();
            return open;
        }

        MarkdownWindow(ShotStack owner)
        {
            this.owner = owner;
            Title = "Markdown y XML";
            Width = 1000;
            Height = 720;
            MinWidth = 720;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Icon = AppIcon;
            AllowDrop = true;
            debounce.Tick += delegate { debounce.Stop(); Preview(false); Stats(); };
            fade.Tick += delegate { fade.Stop(); status = null; statusBad = false; Stats(); };
            PreviewKeyDown += OnKey;
            PreviewDragEnter += OnDrag;
            PreviewDragOver += OnDrag;
            PreviewDragLeave += delegate { if (!Inside()) Glow(false); };
            PreviewMouseMove += delegate { if (dragging) Glow(false); };
            SizeChanged += delegate { CheckCompact(); };
            PreviewDrop += OnDrop;
            Closed += delegate { debounce.Stop(); fade.Stop(); token++; parseToken++; formatToken++; if (editor != null) editor.Stop(); open = null; };
            Build();
            Loaded += delegate { Enter(Root, 0); };
        }

        // Resizing is done by hit-testing the edges ourselves: a resizable frame makes DWM paint its own caption buttons over ours.
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

        protected override void ThemeChanged() { Build(); }

        // ---- Layout

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            page.RowDefinitions.Add(new RowDefinition());
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            Root.Children.Add(page);
            KeepCaptionsOnTop();
            if (editor != null) editor.Stop();
            editor = null; flow = null; tree = null; search = null; previewHost = null; dash = null; errorLabel = null;
            proxy = new Border { Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            Grid.SetRowSpan(proxy, 3);
            page.Children.Add(proxy);

            Border bar = new Border { Background = Brushes.Transparent };
            bar.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else if (e.ClickCount == 1) try { DragMove(); } catch { } };
            page.Children.Add(bar);
            bool has = text != null;
            tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 104, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tools, 0);
            tools.Visibility = has ? Visibility.Visible : Visibility.Hidden;
            mode = new MacSegmented(new string[] { "Vista", "Editar" }, editing ? 1 : 0);
            mode.Changed += delegate(int i) { SetEditing(i == 1); };
            compact = false;
            FillTools();
            tools.Measure(new Size(double.PositiveInfinity, 54));
            fullWidth = tools.DesiredSize.Width;
            if (Narrow()) { compact = true; FillTools(); }
            page.Children.Add(tools);

            Grid body = new Grid { Margin = new Thickness(0) };
            Grid.SetRow(body, 1);
            page.Children.Add(body);
            if (has) BuildDocument(body); else BuildZone(body);

            Grid foot = new Grid { Margin = new Thickness(20, 0, 20, 0) };
            Grid.SetRow(foot, 2);
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusLabel = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            foot.Children.Add(statusLabel);
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(acts, 1);
            if (has)
            {
                MacButton fresh = new MacButton("Nuevo", ButtonKind.Plain, "plus", null, 30);
                fresh.Click += delegate { Reset(); };
                MacButton done = new MacButton("Listo", ButtonKind.Primary, "check", null, 30) { Margin = new Thickness(8, 0, 0, 0) };
                done.Click += Done;
                acts.Children.Add(fresh);
                acts.Children.Add(done);
            }
            foot.Children.Add(acts);
            page.Children.Add(foot);
            Stats();
        }

        // Labels collapse to icons when the window is too narrow for them (the caption buttons stay clear on the right).
        void FillTools()
        {
            if (tools == null) return;
            tools.Children.Clear();
            tools.Children.Add(mode);
            if (xml)
            {
                if (editing)
                {
                    tools.Children.Add(Tool("Formatear", "sparkle", delegate { Reformat(true); }));
                    tools.Children.Add(Tool("Compactar", "minus", delegate { Reformat(false); }));
                }
                else
                {
                    tools.Children.Add(Tool("Expandir todo", "plus", delegate { if (tree != null) tree.ExpandAll(); }));
                    tools.Children.Add(Tool("Contraer todo", "minus", delegate { if (tree != null) tree.CollapseAll(); }));
                    if (search == null) { search = new SearchField(Ds.Brushes, "Buscar o filtrar"); search.Text = query; search.Changed += OnSearch; }
                    tools.Children.Add(search);
                }
                tools.Children.Add(Tool("Copiar XML", "copy", CopyMarkdown));
            }
            else
            {
                tools.Children.Add(Tool("Ordenar", "sparkle", Tidy));
                tools.Children.Add(Tool("Insertar \u00EDndice", "plus", Index));
                tools.Children.Add(Tool("Copiar Markdown", "copy", CopyMarkdown));
            }
            tools.Children.Add(Tool("Copiar como imagen", "photo", CopyImage));
        }

        bool Narrow()
        {
            double w = ActualWidth > 0 ? ActualWidth : Width;
            return fullWidth > 0 && w < fullWidth + 16 + 104 + 8;
        }

        void CheckCompact()
        {
            if (tools == null || fullWidth <= 0 || Narrow() == compact) return;
            compact = !compact;
            FillTools();
        }

        MacButton Tool(string label, string icon, Action act)
        {
            MacButton b = new MacButton(compact ? "" : label, ButtonKind.Secondary, icon, null, 30) { Margin = new Thickness(8, 0, 0, 0) };
            if (compact) { b.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(b, label); }
            b.Click += act;
            return b;
        }

        void BuildZone(Grid body)
        {
            Palette pal = Ds.Brushes;
            Grid zone = new Grid { Width = 560, Height = 300, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5) };
            zone.RenderTransform = new ScaleTransform(1, 1);
            dash = new Rectangle { RadiusX = 20, RadiusY = 20, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection(new double[] { 5, 5 }), Fill = Ds.Brush(pal.Group), Stroke = Ds.Brush(pal.Label3) };
            zone.Children.Add(dash);
            StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            IconTile tile = new IconTile("markdown", 56, Ds.Rgb(140, 150, 255), Ds.Rgb(88, 86, 214)) { HorizontalAlignment = HorizontalAlignment.Center };
            s.Children.Add(tile);
            s.Children.Add(new TextBlock { Text = "Pega o arrastra un .md, .xml o texto", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 20, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = "Ctrl+V pega desde el portapapeles. Admite .md y .txt de hasta 2 MB, y .xml de hasta 8 MB.", FontSize = 13, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            MacButton browse = new MacButton("Abrir archivo\u2026", ButtonKind.Secondary, "folder", null, 30) { Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            browse.Click += Browse;
            s.Children.Add(browse);
            errorLabel = new TextBlock { FontSize = 13, Foreground = Ds.Brush(pal.Red), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(24, 14, 24, 0), MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
            s.Children.Add(errorLabel);
            zone.Children.Add(s);
            body.Children.Add(zone);
        }

        void BuildDocument(Grid body)
        {
            Palette pal = Ds.Brushes;
            editCol = new ColumnDefinition { Width = editing ? new GridLength(split, GridUnitType.Star) : new GridLength(0), MinWidth = editing ? 240 : 0 };
            ColumnDefinition bar = new ColumnDefinition { Width = editing ? GridLength.Auto : new GridLength(0) };
            viewCol = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = editing ? 240 : 0 };
            body.ColumnDefinitions.Add(editCol);
            body.ColumnDefinitions.Add(bar);
            body.ColumnDefinitions.Add(viewCol);
            if (editing)
            {
                editor = new MarkdownEditor(pal, text, xml);
                editor.Edited += delegate
                {
                    text = editor.Text;
                    debounce.Stop();
                    debounce.Start();
                    if (!modified) { modified = true; Stats(); }
                };
                if (!xml) editor.Scrolled += SyncPreview;
                body.Children.Add(editor);
                Border rule = new Border { Width = 1, Background = Ds.Brush(pal.Separator), HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
                Grid.SetColumn(rule, 1);
                body.Children.Add(rule);
                GridSplitter gs = new GridSplitter { Width = 9, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Background = Brushes.Transparent, ShowsPreview = false, Cursor = Cursors.SizeWE };
                Grid.SetColumn(gs, 1);
                gs.MouseEnter += delegate { rule.Background = Ds.Brush(pal.Accent); rule.Width = 2; };
                gs.MouseLeave += delegate { if (!gs.IsMouseCaptured) { rule.Background = Ds.Brush(pal.Separator); rule.Width = 1; } };
                gs.DragCompleted += delegate
                {
                    if (viewCol.ActualWidth > 0 && editCol.ActualWidth > 0) split = editCol.ActualWidth / viewCol.ActualWidth;
                    rule.Background = Ds.Brush(pal.Separator);
                    rule.Width = 1;
                };
                body.Children.Add(gs);
            }
            previewHost = new Border();
            Grid.SetColumn(previewHost, 2);
            body.Children.Add(previewHost);
            treeFor = null;
            if (xml)
            {
                tree = new XmlTreeView(pal);
                tree.PathChanged += delegate(string p) { selPath = p; Stats(); };
                tree.CopyRequest += delegate(string t, string what)
                {
                    if (Retry(delegate { Clipboard.SetText(t); })) Say(what, false);
                    else Say("No he podido usar el portapapeles.", true);
                };
                tree.Menu += ShowMenu;
            }
            else
            {
                flow = new MarkdownFlow(editing ? 28 : 40);
                flow.Menu += ShowMenu;
                previewHost.Child = flow.Box;
            }
            Preview(true);
            if (editing) editor.Box.Focus();
            else Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                if (flow != null) flow.Box.Focus();
                else if (tree != null && model != null) tree.List.Focus();
            }));
        }

        // The pop-up menu opens where the mouse is: a one-pixel anchor is moved there first.
        void ShowMenu(MouseButtonEventArgs e, List<MenuEntry> entries)
        {
            if (proxy == null || page == null) return;
            Point p = e.GetPosition(page);
            proxy.Margin = new Thickness(p.X, p.Y, 0, 0);
            MacMenu.Show(proxy, entries);
        }

        // ---- Preview

        void Preview(bool animate)
        {
            if (previewHost == null || text == null) return;
            if (xml) { PreviewXml(); return; }
            ScrollViewer sv = flow.Scroller;
            double off = sv != null ? sv.VerticalOffset : 0;
            flow.Set(MarkdownDoc.Parse(text));
            if (animate)
            {
                flow.Box.Opacity = 0;
                DoubleAnimation a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
                a.Completed += delegate { if (flow != null) { flow.Box.BeginAnimation(UIElement.OpacityProperty, null); flow.Box.Opacity = 1; } };
                flow.Box.BeginAnimation(UIElement.OpacityProperty, a);
            }
            else
            {
                flow.Box.UpdateLayout();
                if (editor != null) SyncPreview(); else if (sv != null) sv.ScrollToVerticalOffset(off);
            }
        }

        // The preview follows the editor by proportion, so a long document stays roughly side by side.
        void SyncPreview()
        {
            ScrollViewer sv = flow != null ? flow.Scroller : null;
            if (sv == null || editor == null) return;
            double max = editor.ScrollMax;
            sv.ScrollToVerticalOffset(max <= 0 ? 0 : sv.ScrollableHeight * Math.Min(1, editor.Offset / max));
        }

        // ---- XML preview

        XmlModel treeFor;

        int parseToken, formatToken;
        bool parsing, formatting, autoEdit;

        // Parses off the UI thread; a result for text that has changed since (or a document that was replaced) is dropped.
        void ParseXml(Action done)
        {
            if (parsedText == text && !parsing) { done(); return; }
            string snap = text;
            int my = ++parseToken;
            parsing = true;
            parsedText = null; model = null; selPath = null;
            System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                XmlModel m = null; string err = null; int l = 0, c = 0;
                try { XmlDoc.Parse(snap, out m, out err, out l, out c); }
                catch (Exception ex) { ShotStack.Log("XML leer: " + ex.Message); err = "No he podido leer el XML: " + ex.Message; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != parseToken || text != snap) return;
                        parsing = false;
                        parsedText = snap; model = m; xmlError = err; errLine = l; errCol = c;
                        done();
                    }));
                }
                catch { }
            });
        }

        FrameworkElement LoadingPanel(string msg)
        {
            return new TextBlock { Text = msg, FontSize = 13, Foreground = Ds.Brush(Ds.Brushes.Label2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        }

        void PreviewXml()
        {
            if (parsedText != text || parsing)
            {
                // Keep the tree on screen while an edit is being read again; show the wait only when there is none.
                if (previewHost.Child != tree) previewHost.Child = LoadingPanel("Leyendo el XML\u2026");
                ParseXml(delegate
                {
                    if (autoEdit)
                    {
                        autoEdit = false;
                        // A document that does not parse opens in the editor, with the error beside it.
                        if (model == null && !editing) { editing = true; Build(); return; }
                    }
                    PreviewXml();
                });
                Stats();
                return;
            }
            if (model == null)
            {
                previewHost.Child = ErrorPanel();
                treeFor = null;
                Stats();
                return;
            }
            if (treeFor != model)
            {
                treeFor = model;
                tree.Load(model);
                if (query.Length > 0) tree.Filter(query);
            }
            previewHost.Child = tree;
            Stats();
        }

        FrameworkElement ErrorPanel()
        {
            Palette pal = Ds.Brushes;
            StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 480, Margin = new Thickness(28) };
            s.Children.Add(new GlyphView("warning", 30, pal.Red) { HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = "El XML no est\u00E1 bien formado", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 17, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = xmlError, FontSize = 13, Foreground = Ds.Brush(pal.Red), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            s.Children.Add(new TextBlock { Text = editing ? "Corr\u00EDgelo en el editor: esta vista se actualiza sola." : "Pasa a Editar para corregirlo.", FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            if (editing && editor != null && errLine > 0)
            {
                MacButton go = new MacButton("Ir al error", ButtonKind.Secondary, null, null, 30) { Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                go.Click += delegate { if (editor != null) editor.GoTo(errLine, errCol); };
                s.Children.Add(go);
            }
            return s;
        }

        void OnSearch(string q)
        {
            query = q.Trim();
            if (tree == null || model == null) return;
            tree.Filter(query);
            Stats();
        }

        void Stats()
        {
            if (statusLabel == null) return;
            Palette pal = Ds.Brushes;
            statusLabel.Foreground = Ds.Brush(statusBad ? pal.Red : pal.Label2);
            if (status != null) { statusLabel.Text = status; return; }
            if (text == null) { statusLabel.Text = "Ver, ordenar y convertir Markdown o XML en una captura"; return; }
            if (xml)
            {
                string head = name != null ? name : "Sin t\u00EDtulo";
                if (parsing) { statusLabel.Text = head + "  \u00B7  leyendo\u2026"; return; }
                if (model == null && parsedText == text) { statusLabel.Foreground = Ds.Brush(pal.Red); statusLabel.Text = head + "  \u00B7  XML con errores"; return; }
                if (selPath != null) { statusLabel.Text = selPath; return; }
                string extra = tree != null && tree.Filtering ? "  \u00B7  " + tree.Matches.ToString("N0") + (tree.Matches == 1 ? " coincidencia" : " coincidencias") : "";
                int n = model != null ? model.Count : 0;
                statusLabel.Text = head + "  \u00B7  " + n.ToString("N0") + (n == 1 ? " nodo" : " nodos") + (model != null && model.Truncated ? " (los primeros)" : "") + extra + "  \u00B7  " + (modified ? "editado" : "sin cambios");
                return;
            }
            int w = MarkdownTools.Words(text);
            statusLabel.Text = (name != null ? name : "Sin t\u00EDtulo") + "  \u00B7  " + w.ToString("N0") + (w == 1 ? " palabra" : " palabras") + "  \u00B7  " + (modified ? "editado" : "sin cambios");
        }

        void Say(string msg, bool bad)
        {
            status = msg;
            statusBad = bad;
            Stats();
            fade.Stop();
            fade.Start();
        }

        // ---- Loading

        void Fail(string msg)
        {
            if (text != null) { Say(msg, true); return; }
            if (errorLabel == null) return;
            errorLabel.Text = msg;
            errorLabel.Visibility = Visibility.Visible;
            errorLabel.Opacity = 0;
            errorLabel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            // A short sideways shake on the card.
            FrameworkElement card = errorLabel.Parent as FrameworkElement;
            card = card != null ? card.Parent as FrameworkElement : null;
            if (card != null)
            {
                TranslateTransform tt = new TranslateTransform();
                card.RenderTransform = tt;
                DoubleAnimationUsingKeyFrames k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(320) };
                double[] xs = { -9, 8, -5, 3, 0 };
                for (int i = 0; i < xs.Length; i++) k.KeyFrames.Add(new LinearDoubleKeyFrame(xs[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(64 * (i + 1)))));
                k.Completed += delegate { tt.BeginAnimation(TranslateTransform.XProperty, null); };
                tt.BeginAnimation(TranslateTransform.XProperty, k);
            }
        }

        void SetTitle() { Title = name != null ? name + " \u2014 " + (xml ? "XML" : "Markdown") : "Markdown y XML"; }

        void LoadText(string t, string label)
        {
            token++;
            Cursor = null;
            t = t == null ? null : MarkdownTools.Normalize(t);
            bool isXml = t != null && (label != null ? XmlDoc.IsXmlPath(label) : XmlDoc.Sniff(t));
            string err = isXml ? XmlDoc.CheckText(t) : MarkdownTools.Validate(t);
            if (err != null) { status = null; Fail(err); return; }
            text = t;
            name = label;
            modified = false;
            editing = false;
            status = null;
            xml = isXml;
            parsedText = null; model = null; selPath = null; query = ""; search = null;
            // A document that does not parse opens in the editor, with the error beside it.
            parseToken++; formatToken++; parsing = false; formatting = false;
            autoEdit = xml;
            SetTitle();
            Build();
            Enter(page, 0);
        }

        // Files are read off the UI thread: a WSL share can take a moment to wake up.
        void LoadSpecs(List<string> specs)
        {
            int my = ++token;
            string first = specs[0];
            int cut = first.LastIndexOfAny(new char[] { '/', '\\' });
            fade.Stop();
            status = "Cargando " + first.Substring(cut + 1) + "\u2026";
            statusBad = false;
            if (errorLabel != null) errorLabel.Visibility = Visibility.Collapsed;
            Cursor = Cursors.AppStarting;
            Stats();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string path = null, err = null, t = null;
                try { t = MarkdownTools.LoadSpecs(specs, out path, out err); }
                catch (Exception ex) { ShotStack.Log("Markdown cargar: " + ex.Message); err = "No he podido leer el archivo: " + first; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (token != my || open != this) return;
                        Cursor = null;
                        status = null;
                        if (t == null) { Stats(); Fail(err ?? "No he podido leer el archivo."); }
                        else LoadText(t, System.IO.Path.GetFileName(path));
                    }));
                }
                catch { }
            });
        }

        // Files, paths and URIs first (Explorer, Cursor, VS Code); plain text is a document.
        void LoadData(System.Windows.IDataObject d, string empty)
        {
            string plain;
            List<string> specs = MarkdownTools.Specs(d, out plain);
            if (specs.Count > 0) { LoadSpecs(specs); return; }
            if (plain != null) { LoadText(plain, null); return; }
            Fail(empty);
        }

        void LoadFile(string path)
        {
            LoadSpecs(new List<string> { path });
        }

        void Reset()
        {
            token++;
            Cursor = null;
            text = null; name = null; modified = false; editing = false; status = null;
            xml = false; model = null; parsedText = null; selPath = null; query = ""; search = null;
            parseToken++; formatToken++; parsing = false; formatting = false; autoEdit = false;
            SetTitle();
            Build();
            Enter(page, 0);
        }

        void Browse()
        {
            try
            {
                using (System.Windows.Forms.OpenFileDialog d = new System.Windows.Forms.OpenFileDialog())
                {
                    d.Title = "Abrir un documento Markdown o XML";
                    d.Filter = "Markdown, XML y texto|*.md;*.markdown;*.mdown;*.mkd;*.xml;*.xsd;*.config;*.svg;*.xaml;*.txt|Todos los archivos|*.*";
                    if (d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK) LoadFile(d.FileName);
                }
            }
            catch (Exception ex) { ShotStack.Log("Markdown abrir: " + ex.Message); Fail("No he podido abrir el selector de archivos."); }
        }


        void Paste()
        {
            try { LoadData(Clipboard.GetDataObject(), "En el portapapeles no hay texto ni un archivo .md o .xml."); }
            catch (Exception ex) { ShotStack.Log("Markdown pegar: " + ex.Message); Fail("No he podido leer el portapapeles."); }
        }

        // ---- Input

        void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (search != null && Keyboard.FocusedElement == search.Box)
                {
                    if (search.Text.Length > 0) search.Text = ""; else { Keyboard.ClearFocus(); Focus(); }
                    return;
                }
                // In the editor the first Esc only leaves it, so a stray press does not throw the edits away.
                if (Keyboard.FocusedElement is TextBox) { Keyboard.ClearFocus(); Focus(); if (modified) Say("Pulsa Esc otra vez para cerrar sin guardar.", false); }
                else Close();
                return;
            }

            if (!ctrl) return;
            if (e.Key == Key.V && !(Keyboard.FocusedElement is TextBox))
            {
                e.Handled = true;
                if (modified && text != null) Say("Hay cambios sin guardar: pulsa Nuevo antes de pegar otro documento.", true);
                else Paste();
            }

            else if (e.Key == Key.F && xml && !editing && search != null) { e.Handled = true; search.Box.Focus(); search.Box.SelectAll(); }
            else if (e.Key == Key.E && text != null) { e.Handled = true; SetEditing(!editing); }
            else if (e.Key == Key.Enter && text != null) { e.Handled = true; Done(); }
        }

        bool Inside()
        {
            try
            {
                System.Drawing.Point m = System.Windows.Forms.Control.MousePosition;
                Point p = PointFromScreen(new Point(m.X, m.Y));
                return p.X >= 0 && p.Y >= 0 && p.X < ActualWidth && p.Y < ActualHeight;
            }
            catch { return false; }
        }

        // A drop is ours when it carries files or paths, or text with no document open (otherwise the editor takes the text).
        bool Takes(IDataObject d)
        {
            if (MarkdownTools.HasFiles(d)) return true;
            return text == null && d != null && (d.GetDataPresent(DataFormats.UnicodeText) || d.GetDataPresent(DataFormats.Text));
        }

        void OnDrag(object sender, DragEventArgs e)
        {
            if (!Takes(e.Data)) return;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            Glow(true);
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            Glow(false);
            if (!Takes(e.Data)) return;
            e.Handled = true;
            try { LoadData(e.Data, "No he encontrado ning\u00FAn texto ni archivo .md o .xml en lo que has soltado."); }
            catch (Exception ex) { ShotStack.Log("Markdown soltar: " + ex.Message); Fail("No he podido leer lo que has soltado."); }
        }

        void Glow(bool on)
        {
            if (dragging == on || dash == null) return;
            dragging = on;
            Palette pal = Ds.Brushes;
            dash.Stroke = Ds.Brush(on ? pal.Accent : pal.Label3);
            dash.Fill = Ds.Brush(on ? Ds.WithAlpha(pal.Accent, 0.10) : pal.Group);
            FrameworkElement zone = dash.Parent as FrameworkElement;
            ScaleTransform st = zone != null ? zone.RenderTransform as ScaleTransform : null;
            if (st == null) return;
            DoubleAnimation a = new DoubleAnimation(on ? 1.02 : 1, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        // ---- Actions

        void SetEditing(bool on)
        {
            if (on == editing || text == null) return;
            editing = on;
            Build();
            if (on && editor != null)
            {
                editor.Opacity = 0;
                editor.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            }
        }

        void Replace(string t, string message)
        {
            text = t;
            modified = true;
            if (editor != null) editor.SetText(t);
            Preview(false);
            Say(message, false);
        }

        void Tidy()
        {
            if (xml) return;
            string t = MarkdownTools.Tidy(text);
            if (t.TrimEnd('\n') == text.TrimEnd('\n')) { Say("Ya estaba ordenado.", false); return; }
            Replace(t, "Documento ordenado.");
        }

        void Index()
        {
            if (xml) return;
            string t = MarkdownTools.Toc(text);
            if (t == null) { Say("No hay t\u00EDtulos suficientes para un \u00EDndice.", true); return; }
            Replace(t, "\u00CDndice insertado.");
        }

        // Formats or compacts the XML (text next to markup stays as it is).
        void Reformat(bool indent)
        {
            if (!xml || text == null) return;
            if (formatting) { Say("Ya estoy formateando\u2026", false); return; }
            string snap = text;
            int my = ++formatToken;
            formatting = true;
            Say(indent ? "Formateando\u2026" : "Compactando\u2026", false);
            System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                string e = null, r = null;
                try { r = XmlDoc.Format(snap, indent, out e); }
                catch (Exception ex) { ShotStack.Log("XML formatear: " + ex.Message); e = "No he podido leer el XML: " + ex.Message; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != formatToken) return;
                        formatting = false;
                        if (text != snap) return;
                        ApplyFormat(r, e, indent);
                    }));
                }
                catch { }
            });
        }

        void ApplyFormat(string t, string err, bool indent)
        {
            if (t == null) { Say(err, true); return; }
            if (t.TrimEnd('\n') == text.TrimEnd('\n')) { Say(indent ? "Ya estaba formateado." : "Ya estaba compacto.", false); return; }
            Replace(t, indent ? "XML formateado." : "XML compactado.");
        }

        BitmapSource RenderDoc(out string err)
        {
            err = null;
            if (!xml) return MarkdownView.Render(text, out err);
            if (parsing || parsedText != text) { err = "Un momento: todav\u00EDa estoy leyendo el XML."; return null; }
            if (model == null || tree == null) { err = "Corrige el XML antes de crear la imagen."; return null; }
            return XmlTreeView.Render(tree.Rows, tree.Look, out err);
        }

        void CopyMarkdown()
        {
            if (Retry(delegate { Clipboard.SetText(text); })) Say(xml ? "XML copiado." : "Markdown copiado.", false);
            else Say("No he podido usar el portapapeles.", true);
        }

        void CopyImage()
        {
            string err;
            Cursor = Cursors.Wait;
            try
            {
                System.Windows.Media.Imaging.BitmapSource img = RenderDoc(out err);
                if (img == null) { Say(err, true); return; }
                if (Retry(delegate { Clipboard.SetImage(img); })) Say("Imagen copiada.", false);
                else Say("No he podido usar el portapapeles.", true);
            }
            catch (Exception ex) { ShotStack.Log("Markdown imagen: " + ex.Message); Say("No he podido crear la imagen.", true); }
            finally { Cursor = null; }
        }

        static bool Retry(Action act)
        {
            for (int i = 0; i < 5; i++)
            {
                try { act(); return true; }
                catch (System.Runtime.InteropServices.ExternalException) { System.Threading.Thread.Sleep(40); }
            }
            return false;
        }


        void Done()
        {
            if (text == null) return;
            string err;
            Cursor = Cursors.Wait;
            try
            {
                System.Windows.Media.Imaging.BitmapSource img = RenderDoc(out err);
                if (img == null) { Say(err, true); return; }
                string path = owner.SaveCapture(Ink.ToGdi(img), xml ? "XML" : "Markdown");
                if (path == null) { Say("No he podido a\u00F1adir la captura.", true); return; }
                try { File.WriteAllText(System.IO.Path.ChangeExtension(path, xml ? ".xml" : ".md"), text, new UTF8Encoding(false)); }
                catch (Exception ex) { ShotStack.Log("Markdown guardar: " + ex.Message); }
                Close();
            }
            catch (Exception ex) { ShotStack.Log("Markdown listo: " + ex.Message); Say("No he podido crear la captura.", true); }
            finally { Cursor = null; }
        }

        // ---- Motion

        static void Enter(UIElement e, int delay)
        {
            ScaleTransform st = new ScaleTransform(0.975, 0.975);
            TranslateTransform tt = new TranslateTransform(0, 12);
            TransformGroup g = new TransformGroup();
            g.Children.Add(st);
            g.Children.Add(tt);
            FrameworkElement f = e as FrameworkElement;
            if (f != null && f.RenderTransformOrigin == new Point(0, 0)) f.RenderTransformOrigin = new Point(0.5, 0.4);
            e.RenderTransform = g;
            e.Opacity = 0;
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            TimeSpan begin = TimeSpan.FromMilliseconds(delay), len = TimeSpan.FromMilliseconds(320);
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, len) { BeginTime = begin, EasingFunction = ease };
            fadeIn.Completed += delegate { e.BeginAnimation(UIElement.OpacityProperty, null); e.Opacity = 1; e.RenderTransform = null; };
            e.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.975, 1, len) { BeginTime = begin, EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.975, 1, len) { BeginTime = begin, EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, len) { BeginTime = begin, EasingFunction = ease });
        }
    }
}
