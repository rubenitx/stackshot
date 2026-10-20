// Stackshot - Markdown and XML editor: open, paste or drop a document, read it, edit and save it, or turn it into a capture.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
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
    // Created on first use and gone when closed: nothing runs while it is not open (its timers are one-shot).
    public sealed partial class MarkdownWindow : Sheet
    {
        static MarkdownWindow open;
        // Tests answer the save dialog here (suggested name in, chosen path out; null cancels).
        internal static Func<string, string> PickPath { get; set; }
        static readonly XmlFormat format = new XmlFormat();
        static readonly FindOptions findOptions = new FindOptions();

        readonly ShotStack owner;
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        readonly DispatcherTimer fade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        readonly DispatcherTimerLite searchLater, syncLater, checkLater;
        string text, name, status, savedText, approvedText;
        DocFile file;
        bool editing, statusBad, dragging, compact, dirtyShown, reveal, quiet;
        volatile bool unsaved;
        int token;
        double split = 1, fullWidth;
        StackPanel tools;
        ColumnDefinition editCol, viewCol;
        Grid page;
        Border previewHost, findHost, bannerHost;
        MarkdownEditor editor;
        TextBlock statusLabel, errorLabel;
        Rectangle dash;
        Ellipse saveDot;
        MarkdownFlow flow;
        MacSegmented mode;
        FileSystemWatcher watcher;
        // XML: the parsed tree (for the text it was parsed from), the tree on screen, the search box and the last error.
        bool xml;
        XmlModel model;
        XmlModel prepared;                     // the model whose escaped XML and fault the parse already worked out
        string preparedCode, preparedText;
        string parsedText, xmlError, selPath;
        int errLine, errCol;
        XmlTreeView tree;
        SearchField search;
        string query = "";
        FrameworkElement proxy;
        // Find: the bar, its query, the matches in the editor (or the view) and the current one.
        FindBar findBar;
        bool findOpen, findReplace;
        string findQuery = "";
        List<Hit> hits = new List<Hit>();
        int hitIndex = -1;
        string hitsFor;
        // A banner about the file on disk: 0 none, 1 changed outside, 2 deleted.
        int banner;

        public static void Open(ShotStack owner, Settings settings)
        {
            Show(owner);
        }

        public static bool IsOpen { get { return open != null; } }

        // Any document with changes that are not saved (from another thread: as of the last edit).
        public static bool AnyUnsaved
        {
            get
            {
                MarkdownWindow w = open;
                if (w == null) return false;
                return w.Dispatcher.CheckAccess() ? w.Dirty : w.unsaved;
            }
        }

        // Asks Guardar / No guardar / Cancelar for an unsaved document (saving when asked). False when the user cancels;
        // true when everything can close. A later close of the same text does not ask again.
        public static bool ConfirmCloseAll()
        {
            MarkdownWindow w = open;
            if (w == null || !w.Dirty) return true;
            try
            {
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
            }
            catch { }
            if (!w.ConfirmDiscard()) return false;
            w.approvedText = w.text;
            return true;
        }

        // A file named on the command line, or sent by another copy of Stackshot.
        public static void OpenFile(ShotStack owner, string path)
        {
            // Asking about unsaved changes needs the window in front; otherwise it only appears once the file is read.
            MarkdownWindow w = open != null && open.Dirty ? Show(owner) : Pending(owner);
            w.LoadFile(path);
        }

        // A file that just appeared in the watched XML folder: opened and brought in front of whatever the user is in.
        // False (nothing done) when the open document has unsaved changes.
        public static bool OpenArrived(ShotStack owner, string path)
        {
            if (open != null && open.Dirty) return false;
            Pending(owner).LoadFile(path);
            return true;
        }

        // Text made elsewhere (the summary of a PDF's marks), opened as a document that is not a file yet.
        public static void OpenText(ShotStack owner, string text, string label)
        {
            if (open != null && open.Dirty)
            {
                Show(owner);
                if (!open.ConfirmDiscard()) return;
            }
            Pending(owner).LoadText(text, label, null);
        }

        // The window to load a file into, shown (or brought to the front) only when the document is ready: a cold
        // open never flashes the empty start screen, and an open window doesn't show its old document first.
        static MarkdownWindow Pending(ShotStack owner)
        {
            if (open == null) open = new MarkdownWindow(owner);
            open.reveal = true;
            return open;
        }

        void Reveal()
        {
            if (!reveal) return;
            reveal = false;
            if (!IsVisible) { quiet = true; Show(); }
            Front();
        }

        // Windows won't hand the foreground to a background process; toggling Topmost still raises the window over the rest.
        void Front()
        {
            try
            {
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Topmost = true;
                Topmost = false;
                Activate();
            }
            catch (Exception ex) { ShotStack.Log("Visor al frente: " + ex.Message); }
        }

        // [ and ]: the XML written before or after this one in the same folder.
        void Neighbour(int step)
        {
            string next = XmlWatch.Sibling(file.Path, step);
            if (next == null) { Say(step < 0 ? "No hay XML anteriores en la carpeta." : "No hay XML posteriores en la carpeta.", false); return; }
            LoadFile(next);
        }

        static MarkdownWindow Show(ShotStack owner)
        {
            if (open != null)
            {
                open.reveal = false;
                if (!open.IsVisible) open.Show();
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
            Icon = AppIcon;
            AllowDrop = true;
            searchLater = new DispatcherTimerLite(180, ApplySearch);
            syncLater = new DispatcherTimerLite(250, SyncTree);
            checkLater = new DispatcherTimerLite(300, CheckDisk);
            debounce.Tick += delegate { debounce.Stop(); Preview(false); RefreshHits(false); Stats(); };
            fade.Tick += delegate { fade.Stop(); status = null; statusBad = false; Stats(); };
            PreviewKeyDown += OnKey;
            PreviewTextInput += OnTyped;
            PreviewDragEnter += OnDrag;
            PreviewDragOver += OnDrag;
            PreviewDragLeave += delegate { if (!Inside()) Glow(false); };
            PreviewMouseMove += delegate { if (dragging) Glow(false); };
            SizeChanged += delegate { CheckCompact(); };
            PreviewDrop += OnDrop;
            Activated += delegate { if (file != null && file.Path != null) checkLater.Restart(); };
            Closed += delegate
            {
                debounce.Stop(); fade.Stop(); searchLater.Stop(); syncLater.Stop(); checkLater.Stop();
                token++; parseToken++; formatToken++;
                Unwatch();
                if (open == this) open = null;
            };
            Build();
            Loaded += delegate { if (quiet) quiet = false; else Enter(Root, 0); };
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

        protected override bool Snaps { get { return true; } }

        protected override void ThemeChanged()
        {
            if (editor != null) editor.SetPalette(Ds.Brushes);
            Build();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel || ShuttingDown) return;
            if (text != null && approvedText != null && approvedText == Current()) return;
            if (!ConfirmDiscard()) e.Cancel = true;
        }

        // ---- Document state

        internal MarkdownEditor Editor { get { return editor; } }
        internal string Text { get { return Current(); } }
        internal DocFile Doc { get { return file; } }
        internal bool Editing { get { return editing; } }
        internal int Banner { get { return banner; } }
        internal XmlTreeView Tree { get { return tree; } }

        string Current()
        {
            if (editor != null && text != null) text = editor.Text;
            return text;
        }

        internal bool Dirty { get { return Current() != null && text != savedText; } }

        // The title and the dot on Guardar follow the dirty state.
        void MarkDirty() { MarkDirty(false); }

        void MarkDirty(bool force)
        {
            bool d = Dirty;
            unsaved = d;
            if (saveDot != null) saveDot.Visibility = d ? Visibility.Visible : Visibility.Hidden;
            if (d == dirtyShown && !force) return;
            dirtyShown = d;
            SetTitle();
            Stats();
        }

        // Guardar / No guardar / Cancelar when there are unsaved changes. True when the caller can go on.
        internal bool ConfirmDiscard()
        {
            if (!Dirty) return true;
            int r = DocAlert.Ask(this, "\u00BFGuardar los cambios en \u00AB" + (name ?? "Sin t\u00EDtulo") + "\u00BB?", "Si no los guardas, se perder\u00E1n.",
                                 new string[] { "Guardar", "No guardar", "Cancelar" }, 0, 2);
            if (r == 0) return Save(false);
            return r == 1;
        }

        // ---- Layout

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            Root.Children.Add(page);
            KeepCaptionsOnTop();
            if (editor != null && editor.Parent is Panel) ((Panel)editor.Parent).Children.Remove(editor);
            flow = null; tree = null; search = null; previewHost = null; dash = null; errorLabel = null; findBar = null; saveDot = null;
            proxy = new Border { Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            Grid.SetRowSpan(proxy, 6);
            page.Children.Add(proxy);

            Border bar = new Border { Background = Brushes.Transparent };
            bar.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else if (e.ClickCount == 1) try { DragMove(); } catch { } };
            page.Children.Add(bar);
            bool has = text != null;
            tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 104, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            tools.Visibility = has ? Visibility.Visible : Visibility.Hidden;
            mode = new MacSegmented(new string[] { "Vista", "Editar" }, editing ? 1 : 0);
            mode.Changed += delegate(int i) { SetEditing(i == 1); };
            compact = false;
            FillTools();
            tools.Measure(new Size(double.PositiveInfinity, 54));
            fullWidth = tools.DesiredSize.Width;
            if (Narrow()) { compact = true; FillTools(); }
            page.Children.Add(tools);

            if (has && editing && !xml) page.Children.Add(FormatBar());
            if (has && xml) page.Children.Add(NewHeaderHost()); else headerHost = null;
            findHost = new Border();
            Grid.SetRow(findHost, 2);
            page.Children.Add(findHost);
            bannerHost = new Border();
            Grid.SetRow(bannerHost, 3);
            page.Children.Add(bannerHost);
            ShowBanner(banner);

            Grid body = new Grid { Margin = new Thickness(0) };
            Grid.SetRow(body, 4);
            page.Children.Add(OutlineWrap(body));
            if (has) BuildDocument(body); else BuildZone(body);
            if (has && findOpen) ShowFind(findReplace, false);

            Grid foot = new Grid { Margin = new Thickness(20, 0, 16, 0) };
            Grid.SetRow(foot, 5);
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusLabel = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            foot.Children.Add(statusLabel);
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(acts, 1);
            if (has)
            {
                MacButton fresh = new MacButton("Nuevo", ButtonKind.Plain, "plus", null, 30);
                fresh.ToolTip = "Nuevo (Ctrl+N)";
                fresh.Click += delegate { New(); };
                acts.Children.Add(fresh);
                DocIconButton more = new DocIconButton("more", "M\u00E1s opciones", 30) { Margin = new Thickness(6, 0, 0, 0) };
                more.Click += delegate { MoreMenu(more); };
                acts.Children.Add(more);
                Grid saveBox = new Grid { Margin = new Thickness(6, 0, 0, 0) };
                MacButton save = new MacButton("Guardar", ButtonKind.Secondary, "save", null, 30);
                save.ToolTip = "Guardar (Ctrl+S)";
                save.Click += delegate { Save(false); };
                saveBox.Children.Add(save);
                saveDot = new Ellipse { Width = 7, Height = 7, Fill = Ds.Brush(pal.Accent), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -2, 0), IsHitTestVisible = false, Visibility = Dirty ? Visibility.Visible : Visibility.Hidden };
                System.Windows.Automation.AutomationProperties.SetName(save, "Guardar");
                saveBox.Children.Add(saveDot);
                acts.Children.Add(saveBox);
                MacButton done = new MacButton("Listo", ButtonKind.Primary, "check", null, 30) { Margin = new Thickness(8, 0, 0, 0) };
                done.ToolTip = "Crear la captura y cerrar (Ctrl+Intro)";
                done.Click += Done;
                acts.Children.Add(done);
            }
            foot.Children.Add(acts);
            page.Children.Add(foot);
            Stats();
            AccessNames();
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
                    tools.Children.Add(Tool("Formatear", "sparkle", delegate { Reformat(true); }, "Formatear (Ctrl+May\u00FAs+F)"));
                    DocIconButton opts = new DocIconButton("down", "Opciones de formato", 30) { Margin = new Thickness(2, 0, 0, 0) };
                    opts.Click += delegate { FormatMenu(opts); };
                    tools.Children.Add(opts);
                    tools.Children.Add(Tool("Compactar", "minus", delegate { Reformat(false); }, null));
                }
                else
                {
                    tools.Children.Add(Tool("Expandir todo", "plus", delegate { ExpandXml(true); }, null));
                    tools.Children.Add(Tool("Contraer todo", "minus", delegate { ExpandXml(false); }, null));
                    if (search == null)
                    {
                        search = new SearchField(Ds.Brushes, "Buscar o filtrar");
                        search.Text = query;
                        search.Changed += OnSearch;
                        search.Step += StepSearch;
                        search.FilterChanged += delegate { searchLater.Stop(); ApplySearch(); };
                    }
                    tools.Children.Add(search);
                }
                tools.Children.Add(copyTextTool = Tool("Copiar XML", "copy", CopyMarkdown, null));
            }
            else
            {
                tools.Children.Add(OutlineTool());
                tools.Children.Add(tidyTool = Tool("Ordenar", "sparkle", Tidy, null));
                tools.Children.Add(indexTool = Tool("Insertar \u00EDndice", "plus", Index, null));
                tools.Children.Add(copyTextTool = Tool("Copiar Markdown", "copy", CopyMarkdown, null));
            }
            tools.Children.Add(copyImageTool = Tool("Copiar como imagen", "photo", CopyImage, null));
        }

        // The toolbar buttons that confirm on themselves (the status line also says it).
        MacButton copyTextTool, copyImageTool, tidyTool, indexTool;

        static void Confirm(MacButton b, string word)
        {
            if (b != null && b.IsLoaded) b.Confirm(word);
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

        MacButton Tool(string label, string icon, Action act, string tip)
        {
            MacButton b = new MacButton(compact ? "" : label, ButtonKind.Secondary, icon, null, 30) { Margin = new Thickness(8, 0, 0, 0) };
            if (compact || tip != null) { b.ToolTip = tip ?? label; System.Windows.Automation.AutomationProperties.SetName(b, label); }
            b.Click += act;
            return b;
        }

        // Formatting for Markdown, under the toolbar while editing.
        FrameworkElement FormatBar()
        {
            Palette pal = Ds.Brushes;
            Border b = new Border { BorderBrush = Ds.Brush(pal.Separator), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 5, 14, 5) };
            Grid.SetRow(b, 1);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel s = new StackPanel { Orientation = Orientation.Horizontal };
            Fmt(s, "text:B", "Negrita (Ctrl+B)", delegate { Wrap("**", "texto"); });
            Fmt(s, "text:I", "Cursiva (Ctrl+I)", delegate { Wrap("*", "texto"); });
            Fmt(s, "text:S", "Tachado (Ctrl+May\u00FAs+X)", delegate { Wrap("~~", "texto"); });
            Sep(s);
            Fmt(s, "text:H", "T\u00EDtulo (Ctrl+1 a Ctrl+6)", delegate { HeadingCycle(); });
            Sep(s);
            Fmt(s, "md-list", "Lista (Ctrl+May\u00FAs+8)", delegate { Prefix(LineKind.Bullet); });
            Fmt(s, "md-numlist", "Lista numerada (Ctrl+May\u00FAs+7)", delegate { Prefix(LineKind.Numbered); });
            Fmt(s, "md-task", "Lista de tareas", delegate { Prefix(LineKind.Task); });
            Fmt(s, "md-quote", "Cita", delegate { Prefix(LineKind.Quote); });
            Sep(s);
            Fmt(s, "md-link", "Enlace (Ctrl+K)", delegate { Command(delegate(string t, int a, int l) { return EditCommands.Link(t, a, l); }); });
            Fmt(s, "md-code", "C\u00F3digo (Ctrl+May\u00FAs+C)", delegate { Wrap("`", "c\u00F3digo"); });
            Fmt(s, "md-codeblock", "Bloque de c\u00F3digo", delegate { Command(delegate(string t, int a, int l) { return EditCommands.CodeBlock(t, a, l); }); });
            DocIconButton table = Fmt(s, "md-table", "Tabla (dentro de una: filas y columnas)", null);
            table.Click += delegate { TableButton(table); };
            Fmt(s, "md-rule", "Separador", delegate { Command(delegate(string t, int a, int l) { return EditCommands.Rule(t, a); }); });
            g.Children.Add(s);
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(right, 1);
            Fmt(right, "undo", "Deshacer (Ctrl+Z)", delegate { if (editor != null) { editor.Box.Undo(); editor.Box.Focus(); } });
            Fmt(right, "redo", "Rehacer (Ctrl+Y)", delegate { if (editor != null) { editor.Box.Redo(); editor.Box.Focus(); } });
            g.Children.Add(right);
            b.Child = g;
            return b;
        }

        static DocIconButton Fmt(Panel p, string icon, string tip, Action act)
        {
            DocIconButton b = new DocIconButton(icon, tip, 28) { Margin = new Thickness(1, 0, 1, 0) };
            if (act != null) b.Click += act;
            p.Children.Add(b);
            return b;
        }

        // Outside a table the button inserts one; inside, it offers to add or remove a row or a column there.
        void TableButton(FrameworkElement anchor)
        {
            if (!editing || editor == null || xml) return;
            if (!EditCommands.InTable(editor.Text, editor.Box.SelectionStart))
            {
                Command(delegate(string t, int a, int l) { return EditCommands.Table(t, a, 3, 2); });
                return;
            }
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Insertar fila debajo", null, false, delegate { TableOp(EditCommands.TableOp.RowBelow); }));
            m.Add(MenuEntry.Item("Insertar columna a la derecha", null, false, delegate { TableOp(EditCommands.TableOp.ColumnRight); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Eliminar fila", null, false, delegate { TableOp(EditCommands.TableOp.DeleteRow); }));
            m.Add(MenuEntry.Item("Eliminar columna", null, false, delegate { TableOp(EditCommands.TableOp.DeleteColumn); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Alinear la tabla", null, false, delegate { TableOp(EditCommands.TableOp.Align); }));
            MacMenu.Show(anchor, m);
        }

        void TableOp(EditCommands.TableOp op)
        {
            Command(delegate(string t, int a, int l)
            {
                TextEdit ed = EditCommands.Table(t, a, op);
                if (ed.None && op == EditCommands.TableOp.DeleteRow) Say("La fila de t\u00EDtulos no se puede eliminar.", false);
                return ed;
            });
        }

        static void Sep(Panel p)
        {
            p.Children.Add(new Border { Width = 1, Height = 16, Margin = new Thickness(6, 0, 6, 0), Background = Ds.Brush(Ds.Brushes.Separator), VerticalAlignment = VerticalAlignment.Center });
        }

        void MoreMenu(FrameworkElement anchor)
        {
            bool onDisk = file != null && file.Path != null;
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Abrir\u2026", "folder", false, delegate { Browse(); }));
            m.Add(MenuEntry.Item("Guardar como\u2026", "save", false, delegate { Save(true); }));
            m.Add(MenuEntry.Line());
            MenuEntry reload = MenuEntry.Item("Recargar desde el disco", "update", false, delegate { Reload(true); });
            reload.Enabled = onDisk;
            m.Add(reload);
            MenuEntry show = MenuEntry.Item("Mostrar en la carpeta", "folder", false, delegate { ShowInFolder(); });
            show.Enabled = onDisk;
            m.Add(show);
            MenuEntry copy = MenuEntry.Item("Copiar la ruta", "copy", false, delegate { if (Retry(delegate { Clipboard.SetText(file.Path); })) Say("Ruta copiada.", false); });
            copy.Enabled = onDisk;
            m.Add(copy);
            if (!xml)
            {
                m.Add(MenuEntry.Line());
                m.Add(MenuEntry.Item("Copiar como HTML", "copy", false, delegate { CopyHtml(); }));
                m.Add(MenuEntry.Item("Exportar HTML\u2026", "share", false, delegate { ExportHtml(); }));
                m.Add(PdfEntry());
            }
            else XmlEntries(m, onDisk);
            MacMenu.Show(anchor, m);
        }

        // For an XML: the files around it in its folder and, with the folder watch on, the latest ones that arrived there.
        void XmlEntries(List<MenuEntry> m, bool onDisk)
        {
            m.Add(MenuEntry.Line());
            MenuEntry prev = MenuEntry.Item("XML anterior de la carpeta   [", "back", false, delegate { Neighbour(-1); });
            prev.Enabled = onDisk;
            m.Add(prev);
            MenuEntry next = MenuEntry.Item("XML siguiente de la carpeta   ]", "right", false, delegate { Neighbour(1); });
            next.Enabled = onDisk;
            m.Add(next);
            XmlExtraEntries(m, onDisk);
            Settings s = owner != null ? owner.Settings : null;
            if (s == null || !s.XmlWatch) return;
            List<XmlRecent> recent = XmlWatch.Recent(XmlWatch.FolderFor(s), 8);
            if (recent.Count == 0) return;
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Title("XML recientes"));
            foreach (XmlRecent r in recent)
            {
                string p = r.Path;
                bool current = onDisk && string.Equals(p, file.Path, StringComparison.OrdinalIgnoreCase);
                string when = r.Written.ToString(r.Written.Date == DateTime.Today ? "HH:mm:ss" : "dd/MM HH:mm");
                MenuEntry e = MenuEntry.Item(when + "   " + (r.Operation.Length > 0 ? r.Operation : System.IO.Path.GetFileName(p)), null, current, delegate { LoadFile(p); });
                e.Detail = System.IO.Path.GetFileName(p);
                m.Add(e);
            }
        }

        void CopyHtml()
        {
            if (Current() == null) return;
            try
            {
                string body = MdHtml.Body(text, HtmlImages(null));
                DataObject d = new DataObject();
                d.SetData(DataFormats.Html, MdHtml.Clipboard(body));
                d.SetData(DataFormats.UnicodeText, text);
                if (Retry(delegate { Clipboard.SetDataObject(d, true); })) Say("HTML copiado.", false);
                else Say("No he podido usar el portapapeles.", true);
            }
            catch (Exception ex) { ShotStack.Log("Copiar HTML: " + ex.Message); Say("No he podido copiar el HTML.", true); }
        }

        // A standalone page next to the document (or where the user says), always UTF-8.
        internal bool ExportHtml()
        {
            if (Current() == null) return false;
            string baseName = name != null ? System.IO.Path.GetFileNameWithoutExtension(name) : Safe(MarkdownTools.Title(text) ?? "");
            if (baseName.Length == 0) baseName = "documento";
            string target = null;
            Func<string, string> pick = PickPath;
            if (pick != null) target = pick(baseName + ".html");
            else
            {
                try
                {
                    using (System.Windows.Forms.SaveFileDialog d = new System.Windows.Forms.SaveFileDialog())
                    {
                        d.Title = "Exportar HTML";
                        d.Filter = "P\u00E1gina web|*.html;*.htm|Todos los archivos|*.*";
                        d.FileName = baseName + ".html";
                        d.DefaultExt = "html";
                        d.AddExtension = true;
                        d.OverwritePrompt = true;
                        d.InitialDirectory = file != null && file.Path != null ? System.IO.Path.GetDirectoryName(file.Path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK) target = d.FileName;
                    }
                }
                catch (Exception ex) { ShotStack.Log("Exportar HTML: " + ex.Message); }
            }
            if (target == null) return false;
            string err;
            string title = MarkdownTools.Title(text) ?? baseName;
            if (!DocFile.Write(target, new UTF8Encoding(false).GetBytes(MdHtml.Page(text, title, HtmlImages(target))), out err)) { Say(err, true); return false; }
            Say("Exportado: " + System.IO.Path.GetFileName(target) + ".", false);
            return true;
        }

        void FormatMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title("Sangr\u00EDa"));
            m.Add(MenuEntry.Item("2 espacios", null, format.Indent == "  ", delegate { format.Indent = "  "; }));
            m.Add(MenuEntry.Item("4 espacios", null, format.Indent == "    ", delegate { format.Indent = "    "; }));
            m.Add(MenuEntry.Item("Tabulador", null, format.Indent == "\t", delegate { format.Indent = "\t"; }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Atributos en l\u00EDneas nuevas", null, format.AttrLines, delegate { format.AttrLines = !format.AttrLines; }));
            MacMenu.Show(anchor, m);
        }

        void BuildZone(Grid body)
        {
            Palette pal = Ds.Brushes;
            Grid zone = new Grid { Width = 560, Height = 316, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5) };
            zone.RenderTransform = new ScaleTransform(1, 1);
            dash = new Rectangle { RadiusX = 20, RadiusY = 20, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection(new double[] { 5, 5 }), Fill = Ds.Brush(pal.Group), Stroke = Ds.Brush(pal.Label3) };
            zone.Children.Add(dash);
            StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            IconTile tile = new IconTile("markdown", 56, Ds.Rgb(140, 150, 255), Ds.Rgb(88, 86, 214)) { HorizontalAlignment = HorizontalAlignment.Center };
            s.Children.Add(tile);
            s.Children.Add(new TextBlock { Text = "Pega o arrastra un .md, .xml o texto", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 20, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = "Ctrl+V pega desde el portapapeles. Admite .md y .txt de hasta 2 MB, y .xml de hasta 8 MB.", FontSize = 13, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
            MacButton browse = new MacButton("Abrir archivo\u2026", ButtonKind.Secondary, "folder", null, 30);
            browse.ToolTip = "Abrir (Ctrl+O)";
            browse.Click += Browse;
            MacButton blank = new MacButton("Documento en blanco", ButtonKind.Secondary, "edit", null, 30) { Margin = new Thickness(8, 0, 0, 0) };
            blank.Click += Blank;
            buttons.Children.Add(browse);
            buttons.Children.Add(blank);
            s.Children.Add(buttons);
            errorLabel = new TextBlock { FontSize = 13, Foreground = Ds.Brush(pal.Error), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(24, 14, 24, 0), MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
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
                // One editor per document: switching modes or themes keeps its undo history, caret and scroll.
                if (editor == null) editor = NewEditor();
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
                tree.Picked += OnPicked;
                tree.CopyRequest += delegate(string t, string what)
                {
                    if (Retry(delegate { Clipboard.SetText(t); })) Say(what, false);
                    else Say("No he podido usar el portapapeles.", true);
                };
                tree.Menu += ShowMenu;
                WireTree();
            }
            else
            {
                flow = new MarkdownFlow(editing ? 28 : 40);
                flow.Menu += ShowMenu;
                flow.Copied += delegate(string what) { Say(what, false); };
                previewHost.Child = flow.Box;
            }
            Preview(true);
            if (editing) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { if (editor != null && !(findBar != null && findBar.HasFocus)) editor.Box.Focus(); }));
            else Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                if (findBar != null && findBar.HasFocus) return;
                if (flow != null) flow.Box.Focus();
                else if (tree != null && model != null) tree.List.Focus();
            }));
        }

        MarkdownEditor NewEditor()
        {
            MarkdownEditor ed = new MarkdownEditor(Ds.Brushes, text, xml);
            ed.Edited += delegate
            {
                text = ed.Text;
                approvedText = null;
                debounce.Stop();
                debounce.Interval = TimeSpan.FromMilliseconds(text.Length > 200000 ? 600 : 180);
                debounce.Start();
                MarkDirty();
            };
            ed.Scrolled += delegate { if (!xml) SyncPreview(); };
            ed.CaretMoved += delegate { if (xml && ed.Box.IsKeyboardFocused) syncLater.Restart(); };
            ed.Menu += ShowMenu;
            ed.Copied += delegate(string what) { Say(what, false); };
            HookMarkdownEditor(ed);
            return ed;
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
            ShowBlocks();
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
                if (editing && editor != null) SyncPreview(); else if (sv != null) sv.ScrollToVerticalOffset(off);
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
        Action parseDone;

        // One parse at a time off the UI thread; text typed meanwhile gets one more pass when it ends, and a document that
        // was replaced drops the result.
        void ParseXml(Action done)
        {
            if (parsedText == Current() && !parsing) { done(); return; }
            parseDone = done;
            if (parsing) return;
            StartParse();
        }

        void StartParse()
        {
            string snap = Current();
            int my = ++parseToken;
            parsing = true;
           
            System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                XmlModel m = null; string err = null; int l = 0, c = 0;
                try { XmlDoc.Parse(snap, delegate { return System.Threading.Volatile.Read(ref parseToken) != my; }, out m, out err, out l, out c); }
                catch (Exception ex) { ShotStack.Log("XML leer: " + ex.Message); err = "No he podido leer el XML: " + ex.Message; }
                // Escaped XML and the SOAP fault are worked out here too: on a big document they are whole-tree passes.
                bool ready = false;
                string fc = null, ft = null;
                if (m != null && System.Threading.Volatile.Read(ref parseToken) == my)
                {
                    try
                    {
                        XmlDoc.ExpandInner(m);
                        if (!XmlDoc.Fault(m, out fc, out ft)) fc = ft = null;
                        ready = true;
                    }
                    catch (Exception ex) { ShotStack.Log("XML preparar: " + ex.Message); }
                }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != parseToken) return;
                        parsing = false;
                        if (Current() != snap) { StartParse(); return; }
                        parsedText = snap; model = m; xmlError = err; errLine = l; errCol = c;
                        prepared = ready ? m : null; preparedCode = fc; preparedText = ft;
                        if (model != null) selPath = null;
                        Action d = parseDone;
                        parseDone = null;
                        if (d != null) d();
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
            if (parsedText != Current() || parsing)
            {
                // Keep the tree on screen while an edit is being read again; show the wait only when there is none.
                if (previewHost.Child != tree && !(previewHost.Child is StackPanel)) previewHost.Child = LoadingPanel("Leyendo el XML\u2026");
                ParseXml(delegate
                {
                    if (autoEdit)
                    {
                        autoEdit = false;
                        // A document that does not parse opens in the editor, with the error beside it.
                        if (model == null && !editing) { editing = true; Build(); return; }
                    }
                    if (previewHost != null) PreviewXml();
                });
                Stats();
                return;
            }
            if (editor != null) editor.SetError(model == null && errLine > 0 ? EditCommands.Offset(text, errLine, errCol) : -1);
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
                // XML escaped inside a field opens as a branch, and a SOAP fault gets a banner on top.
                if (model == prepared) { faultCode = preparedCode; faultText = preparedText; }
                else
                {
                    XmlDoc.ExpandInner(model);
                    if (!XmlDoc.Fault(model, out faultCode, out faultText)) faultCode = faultText = null;
                }
                ShowBanner(banner);
                tree.Load(model);
                ReapplySearch();
            }
            previewHost.Child = tree;
            ShowCompareView();
            Stats();
        }

        FrameworkElement ErrorPanel()
        {
            Palette pal = Ds.Brushes;
            StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 480, Margin = new Thickness(28) };
            s.Children.Add(new GlyphView("warning", 30, pal.Red) { HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = "El XML no est\u00E1 bien formado", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 17, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = xmlError, FontSize = 13, Foreground = Ds.Brush(pal.Error), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            s.Children.Add(new TextBlock { Text = editing ? "Corr\u00EDgelo en el editor: esta vista se actualiza sola." : "Pasa a Editar para corregirlo.", FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            if (editing && editor != null && errLine > 0)
            {
                MacButton go = new MacButton("Ir al error", ButtonKind.Secondary, null, "F8", 30) { Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                go.Click += GoToError;
                s.Children.Add(go);
            }
            return s;
        }

        void GoToError()
        {
            if (editor == null || errLine <= 0 || model != null) return;
            editor.GoTo(errLine, errCol);
        }

        // ---- Tree and text together (XML while editing)

        void OnPicked(XItem n)
        {
            if (!editing || editor == null || n == null || n.Line <= 0 || parsedText != Current() || tree == null || !tree.List.IsKeyboardFocusWithin) return;
            // An element's position is its name: the caret goes to its '<'.
            int col = n.Kind == XKind.Element ? n.Col - 1 : n.Kind == XKind.Comment ? n.Col - 4 : n.Kind == XKind.CData ? n.Col - 9 : n.Kind == XKind.Pi ? n.Col - 2 : n.Col;
            int i = EditCommands.Offset(text, n.Line, Math.Max(1, col));
            // The tag's name stays highlighted while the tree keeps the focus.
            editor.Select(i, n.Kind == XKind.Element && i < text.Length && text[i] == '<' ? 1 + n.Name.Length : 0);
        }

        void SyncTree()
        {
            if (!xml || !editing || editor == null || tree == null || model == null || parsedText != Current() || !editor.Box.IsKeyboardFocused) return;
            int line, col;
            EditCommands.LineCol(text, editor.Box.CaretIndex, out line, out col);
            XItem n = XmlTreeView.At(model, line, col);
            if (n != null) tree.Reveal(n);
        }

        // ---- XML tree search (after a short pause; Enter applies at once). It takes the user to each match in the
        // whole document (Enter / Shift+Enter, F3); the funnel switches to showing only the matches.

        void OnSearch(string q)
        {
            query = q.Trim();
            if (query.Length == 0) { searchLater.Stop(); ApplySearch(); }
            else searchLater.Restart();
        }

        bool SearchFilters { get { return search != null && search.Filtering; } }

        void ApplySearch()
        {
            Stall.Mark("buscar en el XML");
            if (tree == null || model == null) return;
            if (SearchFilters) tree.Filter(query);
            else if (tree.Find(query) > 0) tree.GoHit(1);
            SearchCount();
            Stats();
        }

        // Applies the query to a freshly loaded tree without moving the view.
        void ReapplySearch()
        {
            if (query.Length == 0 || tree == null) return;
            if (SearchFilters) tree.Filter(query);
            else tree.Find(query);
            SearchCount();
        }

        void StepSearch(int dir)
        {
            if (tree == null || model == null || query.Length == 0 || SearchFilters) return;
            if (searchLater.Pending) { searchLater.Flush(); return; }   // still typing: this is the first match
            tree.GoHit(dir);
            SearchCount();
        }

        void SearchCount()
        {
            if (search == null || tree == null) return;
            search.SetCount(SearchFilters ? -1 : tree.HitIndex, SearchFilters ? tree.Matches : tree.HitCount);
        }

        // ---- Status

        void Stats()
        {
            if (statusLabel == null) return;
            Palette pal = Ds.Brushes;
            statusLabel.Foreground = Ds.Brush(statusBad ? pal.Error : status != null ? pal.Label : pal.Label2);
            // A message stands out from the usual counts while it lasts.
            statusLabel.FontWeight = status != null ? FontWeights.SemiBold : FontWeights.Normal;
            if (status != null) { statusLabel.Text = status; return; }
            if (text == null) { statusLabel.Text = "Ver, editar y convertir Markdown o XML en una captura"; return; }
            string head = name != null ? name : "Sin t\u00EDtulo";
            string tail = (file != null && file.Path != null ? "  \u00B7  " + file.EncodingName + "  \u00B7  " + file.NewlineName : "") + "  \u00B7  " + (Dirty ? "sin guardar" : file != null && file.Path != null ? "guardado" : "sin cambios");
            if (xml)
            {
                if (parsing && parsedText != text) { statusLabel.Text = head + "  \u00B7  leyendo\u2026"; return; }
                if (model == null && parsedText == text)
                {
                    statusLabel.Foreground = Ds.Brush(pal.Error);
                    statusLabel.Text = head + "  \u00B7  " + (xmlError ?? "XML con errores");
                    return;
                }
                if (CompareShown()) { statusLabel.Text = CompareStatus(); return; }
                if (selPath != null && !editing) { statusLabel.Text = selPath; return; }
                string extra = tree != null && tree.Filtering ? "  \u00B7  " + tree.Matches.ToString("N0") + (tree.Matches == 1 ? " coincidencia" : " coincidencias") : "";
                int n = model != null ? model.Count : 0;
                string ok = editing && model != null ? "  \u00B7  bien formado" : "";
                statusLabel.Text = head + "  \u00B7  " + n.ToString("N0") + (n == 1 ? " nodo" : " nodos") + (model != null && model.Truncated ? " (los primeros)" : "") + extra + ok + tail;
                return;
            }
            int w = MarkdownTools.Words(text);
            statusLabel.Text = head + "  \u00B7  " + w.ToString("N0") + (w == 1 ? " palabra" : " palabras") + tail;
        }

        void Say(string msg, bool bad)
        {
            status = msg;
            statusBad = bad;
            Stats();
            fade.Stop();
            fade.Start();
        }

        void SetTitle()
        {
            string t = name != null ? name + " \u2014 " + (xml ? "XML" : "Markdown") : text != null ? "Sin t\u00EDtulo \u2014 " + (xml ? "XML" : "Markdown") : "Markdown y XML";
            Title = (text != null && Dirty ? "\u25CF " : "") + t;
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

        // A new document on screen. doc is how it is stored (null for pasted text).
        void LoadText(string t, string label, DocFile doc)
        {
            token++;
            Cursor = null;
            string raw = t;
            t = t == null ? null : MarkdownTools.Normalize(t);
            bool isXml = t != null && (label != null ? XmlDoc.IsXmlPath(label) : XmlDoc.Sniff(t));
            string err = isXml ? XmlDoc.CheckText(t) : MarkdownTools.Validate(t);
            if (err != null) { status = null; Fail(err); return; }
            Start(t, label, doc ?? DocFile.FromText(raw, isXml), isXml, false);
            // A document that does not parse opens in the editor, with the error beside it.
            autoEdit = xml;
            Build();
            if (reveal) Reveal(); else Enter(page, 0);
            if (file != null && file.Lossy) Say("Hay bytes que no son " + file.EncodingName + " v\u00E1lido: se muestran como \u00AB\uFFFD\u00BB.", true);
        }

        void Start(string t, string label, DocFile doc, bool isXml, bool edit)
        {
            text = t;
            savedText = t;
            approvedText = null;
            name = label;
            file = doc;
            editing = edit;
            status = null;
            xml = isXml;
            editor = null;
            parsedText = null; model = null; prepared = null; selPath = null; query = ""; search = null;
            hits = new List<Hit>(); hitIndex = -1; hitsFor = null;
            parseToken++; formatToken++; parsing = false; formatting = false; autoEdit = false; parseDone = null;
            banner = 0;
            faultCode = faultText = null;
            dirtyShown = false;
            unsaved = false;
            Watch();
            SetTitle();
        }

        // Files are read off the UI thread: a WSL share can take a moment to wake up.
        void LoadSpecs(List<string> specs)
        {
            if (!ConfirmDiscard()) return;
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
                DocFile doc = null;
                string err = null;
                try { doc = MarkdownTools.LoadDoc(specs, out err); }
                catch (Exception ex) { ShotStack.Log("Markdown cargar: " + ex.Message); err = "No he podido leer el archivo: " + first; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (token != my || open != this) return;
                        Cursor = null;
                        status = null;
                        if (doc == null) { Stats(); Fail(err ?? "No he podido leer el archivo."); }
                        else LoadText(doc.Text, System.IO.Path.GetFileName(doc.Path), doc);
                        Reveal();
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
            LoadParsed(specs, plain, empty);
        }

        void LoadParsed(List<string> specs, string plain, string empty)
        {
            if (specs.Count > 0) { LoadSpecs(specs); return; }
            if (plain != null) { if (ConfirmDiscard()) LoadText(plain, null, null); return; }
            Fail(empty);
        }

        void LoadFile(string path)
        {
            LoadSpecs(new List<string> { path });
        }

        // Nuevo: back to the start, after asking about unsaved changes.
        void New()
        {
            if (!ConfirmDiscard()) return;
            Reset();
        }

        void Reset()
        {
            token++;
            Cursor = null;
            text = null; name = null; savedText = null; approvedText = null; file = null; editing = false; status = null; editor = null;
            xml = false; model = null; prepared = null; parsedText = null; selPath = null; query = ""; search = null;
            parseToken++; formatToken++; parsing = false; formatting = false; autoEdit = false; parseDone = null;
            findOpen = false; banner = 0; dirtyShown = false; unsaved = false; faultCode = faultText = null;
            Unwatch();
            SetTitle();
            Build();
            Enter(page, 0);
        }

        // An empty Markdown document, straight into the editor.
        void Blank()
        {
            if (!ConfirmDiscard()) return;
            token++;
            Start("", null, DocFile.FromText("", false), false, true);
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
                    if (file != null && file.Path != null) d.InitialDirectory = System.IO.Path.GetDirectoryName(file.Path);
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

        // ---- Saving

        // Guardar writes back to the file (pasted text asks where); Guardar como always asks. False when nothing was saved.
        internal bool Save(bool asNew)
        {
            if (Current() == null) return false;
            if (file == null) file = DocFile.FromText(text, xml);
            string target = asNew ? null : file.Path;
            if (target == null)
            {
                target = AskPath();
                if (target == null) return false;
            }
            else if (file.Check() == DiskState.Modified)
            {
                int r = DocAlert.Ask(this, "\u00AB" + name + "\u00BB ha cambiado fuera de Stackshot", "Si guardas, tu versi\u00F3n sustituye a la del disco.", new string[] { "Sobrescribir", "Cancelar" }, 1, 1);
                if (r != 0) return false;
            }
            string err;
            bool ok;
            lock (file) ok = file.Save(text, target, out err);
            if (!ok && file.Lossy)
            {
                bool over = file.Path != null && string.Equals(System.IO.Path.GetFullPath(target), file.Path, StringComparison.OrdinalIgnoreCase);
                int r = DocAlert.Ask(this, err, "Al guardar, esos bytes se sustituyen por \u00AB\uFFFD\u00BB" + (over ? " y el original se pierde. Para conservarlo, usa \u00ABGuardar como\u2026\u00BB." : "."),
                                     new string[] { "Guardar igualmente", "Cancelar" }, 1, 1);
                if (r != 0) return false;
                lock (file) { file.AcceptLoss(); ok = file.Save(text, target, out err); if (!ok) file.Lossy = true; }
            }
            if (!ok && err != null && err.StartsWith("Hay caracteres que no caben", StringComparison.Ordinal))
            {
                int r = DocAlert.Ask(this, err, "Puedo guardarlo en UTF-8, que admite cualquier car\u00E1cter.", new string[] { "Guardar en UTF-8", "Cancelar" }, 0, 1);
                if (r != 0) return false;
                string t2;
                lock (file) t2 = file.ToUtf8(text);
                // The declaration now says UTF-8: the text on screen says so too, as one undoable edit.
                if (t2 != text) { if (editor != null) editor.SetText(t2); text = t2; }
                lock (file) ok = file.Save(text, target, out err);
            }
            if (!ok) { Say(err ?? "No he podido guardar.", true); return false; }
            savedText = text;
            approvedText = null;
            name = System.IO.Path.GetFileName(file.Path);
            banner = 0;
            ShowBanner(0);
            Watch();
            PicturesFolderChanged();
            MarkDirty(true);
            Say("Guardado a las " + DateTime.Now.ToString("HH:mm") + ".", false);
            return true;
        }

        string AskPath()
        {
            string ext = xml ? ".xml" : ".md";
            string suggested = name;
            if (suggested == null)
            {
                string title = xml ? null : MarkdownTools.Title(text);
                suggested = title != null ? Safe(title) : "";
                if (suggested.Length == 0) suggested = "documento";
                suggested += ext;
            }
            Func<string, string> pick = PickPath;
            if (pick != null) return pick(suggested);
            try
            {
                using (System.Windows.Forms.SaveFileDialog d = new System.Windows.Forms.SaveFileDialog())
                {
                    d.Title = "Guardar como";
                    d.Filter = xml ? "XML|*.xml|Todos los archivos|*.*" : "Markdown|*.md|Texto|*.txt|Todos los archivos|*.*";
                    d.FileName = suggested;
                    d.AddExtension = true;
                    d.DefaultExt = ext.Substring(1);
                    d.OverwritePrompt = true;
                    d.InitialDirectory = file != null && file.Path != null ? System.IO.Path.GetDirectoryName(file.Path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK ? d.FileName : null;
                }
            }
            catch (Exception ex) { ShotStack.Log("Guardar como: " + ex.Message); Say("No he podido abrir el di\u00E1logo de guardar.", true); return null; }
        }

        static string Safe(string title)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in title.Trim())
            {
                if (Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0) continue;
                sb.Append(c);
                if (sb.Length >= 60) break;
            }
            return sb.ToString().Trim().TrimEnd('.');
        }

        void ShowInFolder()
        {
            if (file == null || file.Path == null) return;
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file.Path + "\""); }
            catch (Exception ex) { ShotStack.Log("Mostrar en la carpeta: " + ex.Message); Say("No he podido abrir la carpeta.", true); }
        }

        // ---- Changes on disk

        void Watch()
        {
            Unwatch();
            if (file == null || file.Path == null) return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(file.Path);
                FileSystemWatcher w = new FileSystemWatcher(dir, System.IO.Path.GetFileName(file.Path));
                w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                FileSystemEventHandler h = delegate { Poke(); };
                w.Changed += h; w.Created += h; w.Deleted += h;
                w.Renamed += delegate { Poke(); };
                w.EnableRaisingEvents = true;
                watcher = w;
            }
            catch (Exception ex) { ShotStack.Log("Vigilar archivo: " + ex.Message); }
        }

        void Poke()
        {
            try { Dispatcher.BeginInvoke(new Action(delegate { checkLater.Restart(); })); } catch { }
        }

        void Unwatch()
        {
            if (watcher == null) return;
            try { watcher.EnableRaisingEvents = false; watcher.Dispose(); } catch { }
            watcher = null;
        }

        // Checked off the UI thread (a network share can be slow to answer).
        void CheckDisk()
        {
            DocFile f = file;
            if (f == null || f.Path == null || text == null) return;
            int my = token;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                DiskState st;
                lock (f) st = f.Check();
                try { Dispatcher.BeginInvoke(new Action(delegate { if (my == token && f == file) OnDisk(st); })); } catch { }
            });
        }

        void OnDisk(DiskState st)
        {
            if (st == DiskState.Unchanged) { if (banner == 2) { banner = 0; ShowBanner(0); } return; }
            if (st == DiskState.Deleted) { if (banner != 2) { banner = 2; ShowBanner(2); } return; }
            if (!Dirty) { Reload(false); return; }
            if (banner != 1) { banner = 1; ShowBanner(1); }
        }

        // Reads the file again; asked: from the menu (unsaved changes are confirmed first).
        void Reload(bool asked)
        {
            if (file == null || file.Path == null) return;
            if (asked && Dirty)
            {
                int r = DocAlert.Ask(this, "\u00BFRecargar \u00AB" + name + "\u00BB desde el disco?", "Se perder\u00E1n los cambios que no has guardado.", new string[] { "Recargar", "Cancelar" }, 1, 1);
                if (r != 0) return;
            }
            string path = file.Path;
            int my = token;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err;
                DocFile d = DocFile.Load(path, out err);
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != token || open != this) return;
                        if (d == null) { Say(err ?? "No he podido leer el archivo.", true); return; }
                        ApplyReload(d, asked);
                    }));
                }
                catch { }
            });
        }

        void ApplyReload(DocFile d, bool asked)
        {
            file = d;
            savedText = d.Text;
            approvedText = null;
            banner = 0;
            ShowBanner(0);
            // In the editor the reload is one undoable edit, so the caret and the scroll stay where they were.
            if (editor != null) editor.SetText(d.Text);
            text = d.Text;
            parsedText = null;
            MarkDirty(true);
            Preview(false);
            Watch();
            Say(asked ? "Recargado desde el disco." : "Recargado: el archivo cambi\u00F3 fuera de Stackshot.", false);
        }

        string faultCode, faultText;

        // A SOAP fault in the open XML: its code and message where they are seen first.
        FrameworkElement FaultBanner()
        {
            if (!xml || faultCode == null || model == null) return null;
            if (FaultInHeader()) return null;
            Palette pal = Ds.Brushes;
            Border b = new Border { Background = Ds.Brush(pal.High ? pal.Window : Ds.WithAlpha(pal.Red, pal.Dark ? 0.16 : 0.10)), BorderBrush = Ds.Brush(pal.High ? pal.Hairline : Ds.WithAlpha(pal.Red, 0.35)), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(18, 7, 14, 7) };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new GlyphView("warning", 16, pal.Red) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            TextBlock msg = new TextBlock { FontSize = 13, Foreground = Ds.Brush(pal.Label), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            msg.Inlines.Add(new System.Windows.Documents.Run("SOAP Fault") { FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Error) });
            if (faultCode.Length > 0) msg.Inlines.Add(new System.Windows.Documents.Run("   " + faultCode) { Foreground = Ds.Brush(pal.Label2) });
            if (faultText.Length > 0) msg.Inlines.Add(new System.Windows.Documents.Run("   " + faultText));
            msg.ToolTip = (faultCode + "\n" + faultText).Trim();
            row.Children.Add(msg);
            b.Child = row;
            return b;
        }

        void ShowBanner(int kind)
        {
            ShowHeader();
            if (bannerHost == null) return;
            if (kind == 0) { bannerHost.Child = FaultBanner(); return; }
            Palette pal = Ds.Brushes;
            Border b = new Border { Background = Ds.Brush(pal.High ? pal.Window : Ds.WithAlpha(pal.Orange, pal.Dark ? 0.16 : 0.12)), BorderBrush = Ds.Brush(pal.High ? pal.Hairline : Ds.WithAlpha(pal.Orange, 0.35)), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(18, 7, 14, 7) };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new GlyphView("warning", 16, pal.Orange) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            TextBlock msg = new TextBlock { FontSize = 13, Foreground = Ds.Brush(pal.Label), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            msg.Text = kind == 1 ? "El archivo ha cambiado en el disco y aqu\u00ED tienes cambios sin guardar." : "El archivo ya no existe en el disco. Gu\u00E1rdalo para no perder el texto.";
            Grid.SetColumn(msg, 1);
            g.Children.Add(msg);
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(acts, 2);
            if (kind == 1)
            {
                MacButton reload = new MacButton("Recargar", ButtonKind.Secondary, null, null, 26);
                reload.Click += delegate { Reload(false); };
                MacButton keep = new MacButton("Mantener mis cambios", ButtonKind.Secondary, null, null, 26) { Margin = new Thickness(6, 0, 0, 0) };
                keep.Click += delegate
                {
                    // The version on disk is now known: saving will overwrite it without asking again.
                    if (file != null) lock (file) file.Acknowledge();
                    banner = 0;
                    ShowBanner(0);
                };
                acts.Children.Add(reload);
                acts.Children.Add(keep);
            }
            else
            {
                MacButton save = new MacButton("Guardar", ButtonKind.Secondary, "save", null, 26);
                save.Click += delegate { if (file != null) lock (file) file.Acknowledge(); Save(false); };
                MacButton saveAs = new MacButton("Guardar como\u2026", ButtonKind.Secondary, null, null, 26) { Margin = new Thickness(6, 0, 0, 0) };
                saveAs.Click += delegate { Save(true); };
                acts.Children.Add(save);
                acts.Children.Add(saveAs);
            }
            g.Children.Add(acts);
            b.Child = g;
            bannerHost.Child = b;
        }

        // ---- Find and replace

        // Ctrl+F (and Ctrl+H with replace): in the editor, or in the Markdown view (find only). The XML tree has its own filter.
        void OpenFind(bool replace)
        {
            if (text == null) return;
            if (xml && !editing)
            {
                if (search != null) { search.Box.Focus(); search.Box.SelectAll(); }
                return;
            }
            if (replace && !editing) SetEditing(true);
            string sel = editor != null && editing && editor.Box.SelectionLength > 0 && editor.Box.SelectionLength < 200 ? editor.Box.SelectedText : null;
            if (sel != null && sel.IndexOf('\n') < 0) findQuery = sel;
            findOpen = true;
            findReplace = replace || (findReplace && editing);
            ShowFind(findReplace, true);
        }

        void ShowFind(bool replace, bool focus)
        {
            if (findHost == null) return;
            if (findBar == null)
            {
                findBar = new FindBar(Ds.Brushes, editing);
                findBar.SetOptions(findOptions);
                findBar.Query.Text = findQuery;
                findBar.Changed += delegate { findQuery = findBar.Query.Text; RefreshHits(true); };
                findBar.Next += delegate { FindStep(false); };
                findBar.Prev += delegate { FindStep(true); };
                findBar.Replace += ReplaceOne;
                findBar.ReplaceAll += ReplaceAll;
                findBar.Closed += CloseFind;
                findHost.Child = findBar;
                if (focus) findBar.Enter();
            }
            findBar.Replacing = replace && editing;
            if (focus) Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate { if (findBar != null) findBar.FocusQuery(); }));
            RefreshHits(false);
        }

        void CloseFind()
        {
            findOpen = false;
            if (findHost != null) findHost.Child = null;
            findBar = null;
            hits = new List<Hit>();
            hitIndex = -1;
            hitsFor = null;
            if (editor != null) { editor.SetHits(null, -1); if (editing) editor.Box.Focus(); }
            else if (flow != null) flow.Box.Focus();
        }

        string FindText()
        {
            if (editing && editor != null) return Current();
            if (!xml && flow != null) return flow.FlatText();
            return null;
        }

        // Matches again for the query (typed: move to the first one from the caret).
        void RefreshHits(bool typed)
        {
            if (findBar == null) return;
            string t = FindText();
            if (t == null) return;
            FindOptions o = findBar.Options;
            findOptions.Case = o.Case; findOptions.Word = o.Word; findOptions.Regex = o.Regex;
            string err = TextSearch.Error(findQuery, o);
            bool partial = false;
            string key = findQuery + "\u0001" + o.Case + o.Word + o.Regex;
            if (err != null) hits = new List<Hit>();
            else if (typed || hitsFor != key || editing) hits = TextSearch.All(t, findQuery, o, TextSearch.MaxHits, out partial);
            hitsFor = key;
            int from = editing && editor != null ? editor.Box.SelectionStart : 0;
            if (typed) hitIndex = TextSearch.Next(hits, from, false);
            else if (hitIndex >= hits.Count) hitIndex = hits.Count - 1;
            if (hits.Count == 0) hitIndex = -1;
            if (editing && editor != null) editor.SetHits(hits, hitIndex);
            if (typed && hitIndex >= 0) Reach(hitIndex);
            findBar.Show(hitIndex, hits.Count, err, partial);
        }

        void Reach(int i)
        {
            Hit h = hits[i];
            if (editing && editor != null) { editor.Select(h.Start, h.Length); editor.SetHits(hits, i); }
            else if (flow != null) flow.Select(h.Start, h.Length);
        }

        void FindStep(bool back)
        {
            if (findBar == null) { OpenFind(false); return; }
            findBar.Flush();
            if (hits.Count == 0) { RefreshHits(true); if (hits.Count == 0) return; }
            int pos;
            if (editing && editor != null)
            {
                int s = editor.Box.SelectionStart, l = editor.Box.SelectionLength;
                pos = back ? s : (hitIndex >= 0 && hitIndex < hits.Count && hits[hitIndex].Start == s && hits[hitIndex].Length == l ? s + Math.Max(1, l) : s);
            }
            else pos = hitIndex < 0 ? 0 : back ? hits[hitIndex].Start : hits[hitIndex].Start + 1;
            hitIndex = TextSearch.Next(hits, pos, back);
            Reach(hitIndex);
            findBar.Show(hitIndex, hits.Count, null, false);
        }

        void ReplaceOne()
        {
            if (!editing || editor == null || findBar == null) return;
            findBar.Flush();
            int s = editor.Box.SelectionStart, l = editor.Box.SelectionLength;
            int i = hits.FindIndex(delegate(Hit h) { return h.Start == s && h.Length == l; });
            if (i < 0) { FindStep(false); return; }
            string with = TextSearch.Replacement(Current(), hits[i], findQuery, findBar.With.Text, findBar.Options);
            editor.Apply(TextEdit.Make(s, l, with, s + with.Length, 0));
            text = editor.Text;
            RefreshHits(false);
            hitIndex = TextSearch.Next(hits, s + with.Length, false);
            if (hitIndex >= 0) Reach(hitIndex);
            findBar.Show(hitIndex, hits.Count, null, false);
        }

        void ReplaceAll()
        {
            if (!editing || editor == null || findBar == null) return;
            findBar.Flush();
            int n;
            bool slow;
            string t = TextSearch.ReplaceAll(Current(), findQuery, findBar.With.Text, findBar.Options, out n, out slow);
            if (slow) { Say("La expresi\u00F3n tarda demasiado: no he reemplazado nada.", true); return; }
            if (n == 0) { Say("No hay nada que reemplazar.", false); return; }
            // One edit, one undo step.
            editor.SetText(t);
            text = editor.Text;
            RefreshHits(false);
            Say(n == 1 ? "1 reemplazo." : n.ToString("N0") + " reemplazos.", false);
        }

        // ---- Input

        // [ and ]: the XML before or after this one in its folder (messages one after another), outside a field.
        // Read as typed text: on a Spanish keyboard they come with AltGr.
        void OnTyped(object sender, TextCompositionEventArgs e)
        {
            if ((e.Text != "[" && e.Text != "]") || !xml || Keyboard.FocusedElement is TextBox || file == null || file.Path == null) return;
            e.Handled = true;
            Neighbour(e.Text == "[" ? -1 : 1);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            ModifierKeys mods = e.KeyboardDevice.Modifiers;
            bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0, alt = (mods & ModifierKeys.Alt) != 0;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (CompareKey(key, ctrl, shift, alt)) { e.Handled = true; return; }
            if (key == Key.Escape)
            {
                // The bar's fields close it themselves; anywhere else the open bar is the first thing Esc closes.
                if (findBar != null && findBar.HasFocus && Keyboard.FocusedElement is TextBox) return;
                if (findBar != null) { e.Handled = true; CloseFind(); return; }
                e.Handled = true;
                if (search != null && Keyboard.FocusedElement == search.Box)
                {
                    if (search.Text.Length > 0) search.Text = ""; else { Keyboard.ClearFocus(); Focus(); }
                    return;
                }
                // In the editor the first Esc only leaves it, so a stray press does not close the window.
                if (Keyboard.FocusedElement is TextBox) { Keyboard.ClearFocus(); Focus(); if (Dirty) Say("Pulsa Esc otra vez para cerrar.", false); }
                else Close();
                return;
            }
            if (key == Key.F3 && xml && !editing && search != null && query.Length > 0 && !SearchFilters && !ctrl) { e.Handled = true; StepSearch(shift ? -1 : 1); return; }
            if (key == Key.F3 && text != null && !ctrl) { e.Handled = true; FindStep(shift); return; }
            if (key == Key.F8 && xml && editing) { e.Handled = true; GoToError(); return; }
            // Enter, Shift+Enter and Ctrl+Enter belong to the find bar's fields (Ctrl+Enter there is Reemplazar todo, not Listo).
            if (key == Key.Enter && findBar != null && e.OriginalSource is TextBox && findBar.IsAncestorOf((DependencyObject)e.OriginalSource)) return;
            if (key == Key.Enter && search != null && Keyboard.FocusedElement == search.Box)
            {
                e.Handled = true;
                if (searchLater.Pending || SearchFilters) searchLater.Flush();
                else StepSearch(shift ? -1 : 1);
                return;
            }
            if (!ctrl || alt) return;
            bool inEditor = editing && editor != null && editor.Box.IsKeyboardFocused;

            if (key == Key.S) { e.Handled = true; if (text != null) Save(shift); return; }
            if (key == Key.O && !shift) { e.Handled = true; Browse(); return; }
            if (key == Key.N && !shift) { e.Handled = true; if (text != null) New(); return; }
            if (key == Key.F && shift && xml && editing) { e.Handled = true; Reformat(true); return; }
            if (key == Key.F && !shift) { e.Handled = true; OpenFind(false); return; }
            if (key == Key.H && !shift) { e.Handled = true; OpenFind(true); return; }
            if (key == Key.V && !shift && !(Keyboard.FocusedElement is TextBox))
            {
                e.Handled = true;
                Paste();
                return;
            }
            if (key == Key.E && !shift && text != null) { e.Handled = true; SetEditing(!editing); return; }
            if (key == Key.Enter && text != null) { e.Handled = true; Done(); return; }
            if (!inEditor || xml) return;

            // Markdown formatting in the editor.
            if (key == Key.B && !shift) { e.Handled = true; Wrap("**", "texto"); }
            else if (key == Key.I && !shift) { e.Handled = true; Wrap("*", "texto"); }
            else if (key == Key.X && shift) { e.Handled = true; Wrap("~~", "texto"); }
            else if (key == Key.C && shift) { e.Handled = true; Wrap("`", "c\u00F3digo"); }
            else if (key == Key.K && !shift) { e.Handled = true; Command(delegate(string t, int a, int l) { return EditCommands.Link(t, a, l); }); }
            else if ((key == Key.D8 || key == Key.NumPad8) && shift) { e.Handled = true; Prefix(LineKind.Bullet); }
            else if ((key == Key.D7 || key == Key.NumPad7) && shift) { e.Handled = true; Prefix(LineKind.Numbered); }
            else if (!shift && key >= Key.D0 && key <= Key.D6) { e.Handled = true; int lv = key - Key.D0; Command(delegate(string t, int a, int l) { return EditCommands.Heading(t, a, l, lv); }); }
        }

        delegate TextEdit EditFn(string text, int start, int length);

        void Command(EditFn f)
        {
            if (!editing || editor == null || xml) return;
            TextEdit ed = f(editor.Text, editor.Box.SelectionStart, editor.Box.SelectionLength);
            if (!ed.None) editor.Apply(ed);
            editor.Box.Focus();
        }

        void Wrap(string mark, string placeholder) { Command(delegate(string t, int a, int l) { return EditCommands.Wrap(t, a, l, mark, placeholder); }); }
        void Prefix(LineKind k) { Command(delegate(string t, int a, int l) { return EditCommands.Prefix(t, a, l, k); }); }
        void HeadingCycle() { Command(delegate(string t, int a, int l) { return EditCommands.Heading(t, a, l, EditCommands.NextHeading(t, a)); }); }

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
            if (DropImageFiles(e)) return;
            if (!Takes(e.Data)) return;
            e.Handled = true;
            try
            {
                // Read what was dropped now, act on it after the drop returns: an alert about unsaved changes must not
                // keep Explorer waiting in the middle of the drag.
                string plain;
                List<string> specs = MarkdownTools.Specs(e.Data, out plain);
                Dispatcher.BeginInvoke(new Action(delegate { LoadParsed(specs, plain, "No he encontrado ning\u00FAn texto ni archivo .md o .xml en lo que has soltado."); }));
            }
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

        internal void SetEditing(bool on)
        {
            if (on == editing || text == null) return;
            Current();
            editing = on;
            bool fresh = on && editor == null;
            // A big document takes a moment to lay out in the editor: say so before it starts.
            if (fresh && text.Length > MarkdownEditor.NiceFontLimit)
            {
                Say("Abriendo el editor\u2026", false);
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { if (editing && text != null) { Build(); status = null; Stats(); } }));
                if (mode != null) mode.Selected = 1;
                return;
            }
            Build();
            if (on && editor != null && fresh)
            {
                editor.Opacity = 0;
                editor.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            }
        }

        // Ordenar, Insertar indice, Formatear: in the editor as one undoable edit.
        void Replace(string t, string message)
        {
            if (editor != null) editor.SetText(t);
            text = t;
            approvedText = null;
            MarkDirty();
            Preview(false);
            Say(message, false);
        }

        void Tidy()
        {
            if (xml || Current() == null) return;
            string t = MarkdownTools.Tidy(text);
            if (t.TrimEnd('\n') == text.TrimEnd('\n')) { Say("Ya estaba ordenado.", false); Confirm(tidyTool, "Ya estaba"); return; }
            Replace(t, "Documento ordenado.");
            Confirm(tidyTool, "Ordenado");
        }

        void Index()
        {
            if (xml || Current() == null) return;
            string t = MarkdownTools.Toc(text);
            if (t == null) { Say("No hay t\u00EDtulos suficientes para un \u00EDndice.", true); return; }
            Replace(t, "\u00CDndice insertado.");
            Confirm(indexTool, "Insertado");
        }

        // Formats or compacts the XML (text next to markup and xml:space="preserve" stay as they are).
        void Reformat(bool indent)
        {
            if (!xml || Current() == null) return;
            if (formatting) { Say("Ya estoy formateando\u2026", false); return; }
            string snap = text;
            int my = ++formatToken;
            formatting = true;
            XmlFormat f = new XmlFormat { Indent = format.Indent, AttrLines = format.AttrLines };
            Say(indent ? "Formateando\u2026" : "Compactando\u2026", false);
            System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                string e = null, r = null;
                try { r = XmlDoc.Format(snap, indent, f, out e); }
                catch (Exception ex) { ShotStack.Log("XML formatear: " + ex.Message); e = "No he podido leer el XML: " + ex.Message; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != formatToken) return;
                        formatting = false;
                        if (Current() != snap) { Say("El texto cambi\u00F3 mientras formateaba: vuelve a pulsar " + (indent ? "Formatear" : "Compactar") + ".", false); return; }
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
            if (!xml) return MarkdownView.Render(Current(), out err);
            if (CompareShown()) return CompareImage(out err);
            if (parsing || parsedText != Current()) { err = "Un momento: todav\u00EDa estoy leyendo el XML."; return null; }
            if (model == null) { err = "Corrige el XML antes de crear la imagen."; return null; }
            if (tree == null) { err = "Pasa a Vista para crear la imagen del XML."; return null; }
            return XmlTreeView.Render(tree.Rows, tree.Look, out err);
        }

        // No picture of the document: one too long for an image can still go out whole as a PDF.
        void NoImage(string err)
        {
            if (xml || err != MarkdownView.TooLong) { Say(err, true); return; }
            Cursor = null;
            if (DocAlert.Ask(this, "Demasiado largo para una imagen",
                             "Como imagen medir\u00EDa m\u00E1s de 16.000 p\u00EDxeles de alto y no se podr\u00EDa ver bien. En PDF va entero, repartido en p\u00E1ginas.",
                             new string[] { "Guardar como PDF", "Cancelar" }, 0, 1) == 0)
                ExportPdf();
        }

        void CopyMarkdown()
        {
            if (Retry(delegate { Clipboard.SetText(Current()); })) { Say(xml ? "XML copiado." : "Markdown copiado.", false); Confirm(copyTextTool, "Copiado"); }
            else Say("No he podido usar el portapapeles.", true);
        }

        void CopyImage()
        {
            string err;
            Cursor = Cursors.Wait;
            try
            {
                BitmapSource img = RenderDoc(out err);
                if (img == null) { NoImage(err); return; }
                // Like captures, never synced to other devices through the cloud clipboard.
                DataObject d = new DataObject();
                d.SetImage(img);
                d.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
                if (Retry(delegate { Clipboard.SetDataObject(d, true); })) { Say("Imagen copiada.", false); Confirm(copyImageTool, "Copiada"); }
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

        // The copy saved next to the capture, in the document's own encoding (UTF-8 when its characters do not fit).
        internal static byte[] Companion(string text, bool xml, DocFile like)
        {
            string warn;
            byte[] b = (like ?? DocFile.FromText(text, xml)).Encode(text, out warn);
            if (b != null) return b;
            DocFile f = DocFile.FromText(text, xml);
            string t = f.ToUtf8(text);
            return f.Encode(t, out warn) ?? new UTF8Encoding(false).GetBytes(t);
        }

        void Done()
        {
            if (Current() == null) return;
            // A file with unsaved edits asks before the capture exists, so Cancelar leaves nothing in the stack.
            bool asked = false;
            if (file != null && file.Path != null && Dirty)
            {
                if (!ConfirmDiscard()) return;
                asked = true;
            }
            string err;
            Cursor = Cursors.Wait;
            try
            {
                BitmapSource img = RenderDoc(out err);
                if (img == null) { NoImage(err); return; }
                string path = owner.SaveCapture(Ink.ToGdi(img), xml ? "XML" : "Markdown");
                if (path == null) { Say("No he podido a\u00F1adir la captura.", true); return; }
                try { System.IO.File.WriteAllBytes(System.IO.Path.ChangeExtension(path, xml ? ".xml" : ".md"), Companion(text, xml, file)); }
                catch (Exception ex) { ShotStack.Log("Markdown guardar: " + ex.Message); }
                // Text that is not a file is kept in the copy next to the capture.
                if (file == null || file.Path == null) savedText = text;
                if (asked) approvedText = text;
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
