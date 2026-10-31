// Stackshot - Historial: every capture and recording, newest first in groups of days, found by its name or by the text in
// it (read by HistoryIndex). The grid only builds the rows near what the page shows.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Stackshot
{
    public partial class HomeWindow
    {
        static readonly string[] HistoryFiltersEs = { "Todo", "Capturas", "V\u00EDdeos", "GIF" };
        static readonly string[] HistoryFiltersEn = { "All", "Captures", "Videos", "GIF" };
        static string[] HistoryFilters { get { return I18n.IsSpanish ? HistoryFiltersEs : HistoryFiltersEn; } }
        static readonly CultureInfo HistoryEsCulture = new CultureInfo("es-ES");
        static CultureInfo HistoryEs { get { return I18n.IsSpanish ? HistoryEsCulture : CultureInfo.GetCultureInfo("en-US"); } }

        string historyQuery = "";
        int historyFilter;
        List<HistoryItem> historyAll;            // the last listing (null until the first one arrives)
        List<HistoryItem> historyShown;          // what the search and the filter leave of it
        string[] historyWords = new string[0];
        StackPanel historyBody;                  // the body the page was built into: any rebuild replaces it
        HistoryGrid historyGrid;
        TextBox historyBox;
        TextBlock historyCount, historyStatusText;
        FrameworkElement historyStatus;
        Grid historyStatusMeter;
        StackPanel historyNotice;
        readonly List<Border> historyChipViews = new List<Border>();
        DispatcherTimer historyDebounce, historyRefill;
        int historyLoads, historySearches, historyVersion;
        bool historyHooked, historyStale;
        volatile bool historyPulsing;

        // The page on show is the one built here.
        bool HistoryLive { get { return page == "history" && historyBody != null && historyBody == body; } }

        void BuildHistory()
        {
            HookHistory();
            body.MaxWidth = 700;
            historyBody = body;
            Header(I18n.T("Historial", "History"), I18n.T("Tus capturas y grabaciones, de la m\u00E1s reciente a la m\u00E1s antigua.", "Your captures and recordings, newest first."));
            if (!settings.HistorySearch) Add(HistoryOffCard());
            Add(HistorySearchField());
            Add(HistoryChipsRow());
            historyStatus = HistoryStatusLine();
            Add(historyStatus);
            historyNotice = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 36, 0, 36), Visibility = Visibility.Collapsed };
            Add(historyNotice);
            historyGrid = new HistoryGrid(this);
            Add(historyGrid);
            HistoryPaintStatus();
            // A rebuild (the theme changed) shows what was there at once; the listing then brings it up to date. Texts read
            // before the search was turned off go with it: neither shown nor searched again.
            if (!settings.HistorySearch && historyAll != null) foreach (HistoryItem it in historyAll) it.Entry = null;
            if (historyShown != null && historyAll != null && (settings.HistorySearch || historyWords.Length == 0)) HistoryShow(historyShown, historyWords, false);
            HistoryLoad(false);
        }

        // Ctrl+F, new captures, the window coming back and the index's progress, hooked once for the window's life.
        void HookHistory()
        {
            if (historyHooked) return;
            historyHooked = true;
            Action pulse = delegate
            {
                if (historyPulsing) return;
                historyPulsing = true;
                try { Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)HistoryPulse); }
                catch (Exception) { historyPulsing = false; }
            };
            HistoryIndex.Changed += pulse;
            Action<string> captured = delegate(string path)
            {
                if (!HistoryLive) return;
                Dispatcher ui = Dispatcher;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ShotStack.WaitWritten(path);   // the PNG is still being written in the background
                    ui.BeginInvoke((Action)delegate
                    {
                        if (!HistoryLive) return;
                        if (IsVisible) HistoryLoad(false);
                        else historyStale = true;
                    });
                });
            };
            owner.Captured += captured;
            IsVisibleChanged += delegate
            {
                if (!HistoryLive)
                {
                    // Hidden on another page: the last listing is let go (it is made again in a moment when needed).
                    if (!IsVisible) { historyAll = historyShown = null; historyGrid = null; }
                    return;
                }
                if (!IsVisible) { historyStale = true; return; }
                if (Left < -30000 || !historyStale) return;
                historyStale = false;
                HistoryLoad(false);
            };
            PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (!HistoryLive || historyBox == null || e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control) return;
                e.Handled = true;
                historyBox.Focus();
                historyBox.SelectAll();
                HistoryScrollToResults();   // scrolled down the list, the field comes back into view under the toolbar
            };
            scroller.ScrollChanged += delegate { if (historyGrid != null && HistoryLive) historyGrid.Scrolled(); };
            Closed += delegate
            {
                HistoryIndex.Changed -= pulse;
                owner.Captured -= captured;
            };
        }

        // ---- Page pieces

        FrameworkElement HistorySearchField()
        {
            Palette pal = Ds.Brushes;
            Color line = pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.10, 255, 255, 255) : Ds.Argb(0.10, 0, 0, 0);
            Grid holder = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            if (!pal.Dark && !pal.High) holder.Children.Add(new SoftShadow(10, 3, 0.5, 0.06));
            Grid g = new Grid { Height = 36 };
            Border frame = new Border { CornerRadius = new CornerRadius(10), Background = Ds.Brush(pal.Group), BorderBrush = Ds.Brush(line), BorderThickness = new Thickness(1) };
            g.Children.Add(frame);
            GlyphView icon = new GlyphView("search", 15, pal.Label2, 1.7) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            g.Children.Add(icon);
            string searchHintText = settings.HistorySearch
                ? I18n.T("Buscar por texto o nombre", "Search by text or name")
                : I18n.T("Buscar por nombre", "Search by name");
            TextBlock hint = Label(searchHintText, Ds.Regular, 13, pal.Label3, new Thickness(36, 0, 0, 1));
            hint.IsHitTestVisible = false;
            hint.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(hint);
            TextBlock keys = Label("Ctrl+F", Ds.Regular, 11.5, pal.Label3, new Thickness(0, 0, 13, 1));
            keys.IsHitTestVisible = false;
            keys.HorizontalAlignment = HorizontalAlignment.Right;
            g.Children.Add(keys);
            TextBox box = new TextBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(34, 0, 34, 1),
                FontFamily = Ds.Text, FontSize = 13, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(pal.Accent),
                MaxLength = 120, VerticalContentAlignment = VerticalAlignment.Center, Template = DockFieldTemplate(), Text = historyQuery
            };
            g.Children.Add(box);
            Access.Describe(box, hint.Text, null);
            g.Cursor = Cursors.IBeam;
            g.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (!box.IsKeyboardFocusWithin) { box.Focus(); e.Handled = true; } };
            Border clear = new TapBorder
            {
                Do = delegate { box.Text = ""; box.Focus(); },
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 11, 0), Cursor = Cursors.Arrow, Background = Ds.Brush(pal.High ? pal.Label : pal.Dark ? Ds.Argb(0.32, 255, 255, 255) : Ds.Argb(0.28, 0, 0, 0)),
                ToolTip = I18n.T("Borrar la b\u00FAsqueda", "Clear search")
            };
            clear.Child = new GlyphView("close", 10, pal.High ? pal.Window : pal.Dark ? Ds.Rgb(44, 44, 48) : Colors.White, 2.2) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            clear.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; };
            clear.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; box.Text = ""; box.Focus(); };
            g.Children.Add(clear);
            Action sync = delegate
            {
                bool any = box.Text.Length > 0;
                hint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
                keys.Visibility = any || box.IsKeyboardFocusWithin ? Visibility.Collapsed : Visibility.Visible;
                clear.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            };
            sync();
            box.TextChanged += delegate { sync(); HistoryTyped(box.Text); };
            box.GotKeyboardFocus += delegate
            {
                frame.BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.85));
                frame.BorderThickness = new Thickness(1.5);
                icon.Color = pal.Link;
                sync();
            };
            box.LostKeyboardFocus += delegate
            {
                frame.BorderBrush = Ds.Brush(line);
                frame.BorderThickness = new Thickness(1);
                icon.Color = pal.Label2;
                sync();
            };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    // As in Spotlight: Enter opens the first result.
                    e.Handled = true;
                    if (historyShown != null && historyShown.Count > 0) HistoryOpen(historyShown[0], 0);
                    return;
                }
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                if (box.Text.Length > 0) box.Text = "";
                else Focus();
            };
            historyBox = box;
            holder.Children.Add(g);
            return holder;
        }

        void HistoryTyped(string text)
        {
            if (text == historyQuery) return;
            historyQuery = text;
            if (historyDebounce == null)
            {
                historyDebounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(140) };
                historyDebounce.Tick += delegate
                {
                    historyDebounce.Stop();
                    if (HistoryLive) HistorySearchNow(true, false);
                };
            }
            historyDebounce.Stop();
            historyDebounce.Start();
        }

        FrameworkElement HistoryChipsRow()
        {
            Palette pal = Ds.Brushes;
            Grid row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            StackPanel chips = new TapPanel { Orientation = Orientation.Horizontal, Interactive = false, Role = Access.Group, Label = I18n.T("Mostrar", "Show") };
            historyChipViews.Clear();
            for (int i = 0; i < HistoryFilters.Length; i++)
            {
                int k = i;
                Border c = new TapBorder { CornerRadius = new CornerRadius(13), Height = 26, Padding = new Thickness(12, 0, 12, 1), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                                           BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = HcPaint.Edge,
                                           Label = HistoryFilters[i], Role = Access.Radio, On = delegate { return k == historyFilter; }, Do = delegate { HistoryPick(k); } };
                c.Child = Label(HistoryFilters[i], Ds.Medium, 12.5, pal.Label);
                c.MouseEnter += delegate { if (k != historyFilter) c.Background = Ds.Brush(pal.ControlHover); };
                c.MouseLeave += delegate { HistoryPaintChip(c, k); };
                c.MouseLeftButtonUp += delegate { HistoryPick(k); };
                historyChipViews.Add(c);
                HistoryPaintChip(c, k);
                chips.Children.Add(c);
            }
            row.Children.Add(chips);
            historyCount = Label("", Ds.Regular, 12, pal.Label2);
            historyCount.HorizontalAlignment = HorizontalAlignment.Right;
            row.Children.Add(historyCount);
            return row;
        }

        void HistoryPaintChip(Border c, int k)
        {
            Palette pal = Ds.Brushes;
            bool on = k == historyFilter;
            c.Background = Ds.Brush(on ? pal.Accent : pal.Control);
            TextBlock t = c.Child as TextBlock;
            if (t != null) t.Foreground = on ? Ds.Brush(pal.OnAccent) : Ds.Brush(pal.Label);
        }

        void HistoryPick(int k)
        {
            if (k == historyFilter) return;
            historyFilter = k;
            for (int i = 0; i < historyChipViews.Count; i++) HistoryPaintChip(historyChipViews[i], i);
            HistorySearchNow(true, false);
        }

        // "Leyendo el texto de las capturas... 120 de 300", with a thin meter; hidden when there is nothing to say.
        FrameworkElement HistoryStatusLine()
        {
            Palette pal = Ds.Brushes;
            DockPanel p = new DockPanel { LastChildFill = false, Margin = new Thickness(2, -2, 0, 14), Visibility = Visibility.Collapsed };
            GlyphView icon = new GlyphView("textscan", 14, pal.Link, 1.6) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 7, 0) };
            DockPanel.SetDock(icon, Dock.Left);
            p.Children.Add(icon);
            historyStatusText = Paragraph("", 12, pal.Label2);
            DockPanel.SetDock(historyStatusText, Dock.Left);
            p.Children.Add(historyStatusText);
            Grid meter = new Grid { Width = 80, Height = 4, Margin = new Thickness(10, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(meter, Dock.Left);
            meter.ColumnDefinitions.Add(new ColumnDefinition());
            meter.ColumnDefinitions.Add(new ColumnDefinition());
            Border track = new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.Control) };
            Grid.SetColumnSpan(track, 2);
            meter.Children.Add(track);
            meter.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.Accent) });
            historyStatusMeter = meter;
            p.Children.Add(meter);
            return p;
        }

        bool HistoryReading
        {
            get
            {
                int done, total;
                HistoryIndex.Progress(out done, out total);
                return settings.HistorySearch && !HistoryIndex.Unavailable && total > 0 && done < total;
            }
        }

        void HistoryPaintStatus()
        {
            if (historyStatus == null || historyStatusText == null) return;
            string text = null;
            double share = -1;
            if (settings.HistorySearch)
            {
                if (HistoryIndex.Unavailable) text = I18n.T("Este Windows no tiene el reconocimiento de texto (OCR): de momento se busca solo por el nombre.", "This Windows does not have text recognition (OCR): searching by name only for now.");
                else
                {
                    int done, total;
                    HistoryIndex.Progress(out done, out total);
                    // A single new capture is read in a moment: no line for it.
                    if (total > 1 && done < total)
                    {
                        text = I18n.IsSpanish
                            ? ("Leyendo el texto de las capturas\u2026 " + done.ToString("N0", HistoryEs) + " de " + total.ToString("N0", HistoryEs))
                            : ("Reading capture text\u2026 " + done.ToString("N0", CultureInfo.InvariantCulture) + " of " + total.ToString("N0", CultureInfo.InvariantCulture));
                        if (HistoryIndex.Paused) text += Recorder.Recording
                            ? I18n.T(" \u00B7 en pausa mientras grabas", " \u00B7 paused while recording")
                            : I18n.T(" \u00B7 en pausa mientras capturas", " \u00B7 paused while capturing");
                        share = done / (double)total;
                    }
                }
            }
            historyStatus.Visibility = text != null ? Visibility.Visible : Visibility.Collapsed;
            if (text == null) return;
            historyStatusText.Text = text;
            historyStatusMeter.Visibility = share >= 0 ? Visibility.Visible : Visibility.Collapsed;
            if (share < 0) return;
            historyStatusMeter.ColumnDefinitions[0].Width = new GridLength(Math.Max(0.0001, share), GridUnitType.Star);
            historyStatusMeter.ColumnDefinitions[1].Width = new GridLength(Math.Max(0.0001, 1 - share), GridUnitType.Star);
        }

        // With the setting off: what it would do, and a link that turns it on (the same switch as in General > Recientes).
        FrameworkElement HistoryOffCard()
        {
            Palette pal = Ds.Brushes;
            Grid holder = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            if (!pal.Dark && !pal.High) holder.Children.Add(new SoftShadow(10, 3, 0.5, 0.06));
            Border card = new Border
            {
                CornerRadius = new CornerRadius(10), Background = Ds.Brush(pal.Group), Padding = new Thickness(14, 12, 16, 13), BorderThickness = new Thickness(1),
                BorderBrush = Ds.Brush(pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.07, 0, 0, 0))
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            IconTile tile = new IconTile("search", 28, W(Mac.Indigo), W(Mac.Blue)) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 12, 0) };
            g.Children.Add(tile);
            StackPanel text = new StackPanel();
            Grid.SetColumn(text, 1);
            text.Children.Add(Label(I18n.T("La b\u00FAsqueda por texto est\u00E1 desactivada", "Text search is turned off"), Ds.Regular, 13.5, pal.Label));
            TextBlock p = Paragraph(I18n.T("Stackshot puede leer el texto de cada captura en tu equipo, sin que nada salga de \u00E9l, para que la encuentres por lo que pon\u00EDa. Ahora solo se busca por el nombre. ",
                                           "Stackshot can read text from each capture on your device, without anything leaving your computer, so you can find captures by their content. Currently searching by name only. "), 12, pal.Label2);
            p.Margin = new Thickness(0, 3, 0, 0);
            if (Policy.SearchForced) p.Inlines.Add(new Run(Policy.Managed + "."));   // an IT policy keeps it off: no way to turn it on here
            else
            {
                p.Inlines.Add(InlineLink(I18n.T("Activar la b\u00FAsqueda por texto", "Turn on text search"), HistoryTurnOn));
                p.Inlines.Add(new Run(I18n.T(" (tambi\u00E9n est\u00E1 en General\u00A0\u203A\u00A0Recientes).", " (also in General\u00A0\u203A\u00A0Recents).")));
            }
            text.Children.Add(p);
            g.Children.Add(text);
            card.Child = g;
            holder.Children.Add(card);
            return holder;
        }

        void HistoryTurnOn()
        {
            if (Policy.SearchForced) return;
            settings.HistorySearch = true;
            HistoryIndex.Apply(settings);
            Changed();
            Rebuild();
        }

        // The empty states: no captures at all, nothing of a kind, or nothing that matches.
        void HistoryPaintNotice(int shown)
        {
            if (historyNotice == null) return;
            Palette pal = Ds.Brushes;
            historyNotice.Children.Clear();
            string title = null, sub = null, icon = "search";
            if (historyAll != null && historyAll.Count == 0)
            {
                icon = "photo";
                title = I18n.T("Todav\u00EDa no hay capturas", "No captures yet");
                sub = I18n.T("Las capturas y grabaciones que hagas aparecer\u00E1n aqu\u00ED, ordenadas por d\u00EDas.", "Captures and recordings you make will appear here, grouped by day.");
            }
            else if (historyAll != null && shown == 0)
            {
                if (historyWords.Length > 0)
                {
                    title = I18n.IsSpanish ? ("Nada coincide con \u00AB" + historyQuery.Trim() + "\u00BB") : ("Nothing matches \u201C" + historyQuery.Trim() + "\u201D");
                    sub = !settings.HistorySearch ? I18n.T("Solo se busca por el nombre: la b\u00FAsqueda por texto est\u00E1 desactivada.", "Searching by name only: text search is disabled.")
                        : HistoryReading ? I18n.T("Todav\u00EDa se est\u00E1 leyendo el texto de algunas capturas: prueba otra vez dentro de un momento.", "Still reading text from some captures: try again in a moment.")
                        : historyFilter != 0 ? I18n.T("Prueba con otras palabras o con \u00ABTodo\u00BB.", "Try different words or switch to \u201CAll\u201D.")
                        : I18n.T("Prueba con otras palabras: se busca en el texto de cada captura y en su nombre.", "Try different words: searching in both text and file names.");
                }
                else
                {
                    icon = historyFilter == 2 ? "video" : historyFilter == 3 ? "gif" : "photo";
                    title = historyFilter == 2 ? I18n.T("No hay v\u00EDdeos", "No videos") : historyFilter == 3 ? I18n.T("No hay GIF", "No GIFs") : I18n.T("No hay capturas", "No captures");
                    sub = I18n.T("Prueba con \u00ABTodo\u00BB.", "Try \u201CAll\u201D.");
                }
            }
            historyNotice.Visibility = title != null ? Visibility.Visible : Visibility.Collapsed;
            if (title == null) return;
            historyNotice.Children.Add(new GlyphView(icon, 28, pal.Label3, 1.6) { HorizontalAlignment = HorizontalAlignment.Center });
            TextBlock t = Label(title, Ds.Semibold, 14, pal.Label, new Thickness(0, 10, 0, 0));
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.MaxWidth = 460;
            historyNotice.Children.Add(t);
            TextBlock s = Paragraph(sub, 12.5, pal.Label2);
            s.TextAlignment = TextAlignment.Center;
            s.HorizontalAlignment = HorizontalAlignment.Center;
            s.MaxWidth = 400;
            s.Margin = new Thickness(0, 4, 0, 0);
            historyNotice.Children.Add(s);
        }

        // ---- Listing and searching (off the UI thread; the page only shows the result)

        // The page's own background thread, started once: listing and searching never queue behind thumbnails on the thread
        // pool, and asking for a job costs the UI thread nothing. With nothing to do it waits, without a timer.
        static readonly Queue<ThreadStart> historyJobs = new Queue<ThreadStart>();
        static Thread historyRunner;

        static void HistoryWork(ThreadStart job)
        {
            lock (historyJobs)
            {
                historyJobs.Enqueue(job);
                if (historyRunner != null) { Monitor.Pulse(historyJobs); return; }
                historyRunner = new Thread(HistoryRun);
                historyRunner.IsBackground = true;
                historyRunner.Name = "Historial (p\u00E1gina)";
                historyRunner.Start();
            }
        }

        static void HistoryRun()
        {
            while (true)
            {
                ThreadStart job;
                lock (historyJobs)
                {
                    while (historyJobs.Count == 0) Monitor.Wait(historyJobs);
                    job = historyJobs.Dequeue();
                }
                try { job(); }
                catch (Exception ex) { ShotStack.Log("Historial: " + ex.Message); }
            }
        }

        void HistoryLoad(bool fromTop)
        {
            int gen = ++historyLoads;
            Settings s = settings;
            string q = historyQuery;
            int f = historyFilter;
            Dispatcher ui = Dispatcher;
            HistoryIndex.RequestScan();
            HistoryWork(delegate
            {
                int v1 = HistoryIndex.Version;
                List<HistoryItem> all, first;
                try
                {
                    all = HistoryIndex.Catalog(s);
                    first = HistoryIndex.Search(all, q, f);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Historial: " + ex.Message);
                    all = new List<HistoryItem>();
                    first = all;
                }
                ui.BeginInvoke((Action)delegate { HistoryLoaded(gen, all, first, q, f, v1, fromTop); });
                // The index wasn't in memory yet (the page opened right after startup): its texts follow.
                if (!s.HistorySearch || HistoryIndex.Loaded) return;
                int v2;
                List<HistoryItem> second;
                try
                {
                    HistoryIndex.Load();
                    v2 = HistoryIndex.Version;
                    HistoryIndex.Fill(all);
                    second = HistoryIndex.Search(all, q, f);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Historial: " + ex.Message);
                    return;
                }
                ui.BeginInvoke((Action)delegate { HistoryLoaded(gen, all, second, q, f, v2, false); });
            });
        }

        void HistoryLoaded(int gen, List<HistoryItem> all, List<HistoryItem> shown, string q, int f, int version, bool fromTop)
        {
            if (gen != historyLoads || !HistoryLive) return;
            historyAll = all;
            historyVersion = version;
            if (q != historyQuery || f != historyFilter) { HistorySearchNow(fromTop, false); return; }
            HistoryShow(shown, HistoryIndex.Words(q), fromTop);
        }

        // refill: new texts arrived; they are attached first, and only a search typed needs showing again.
        void HistorySearchNow(bool fromTop, bool refill)
        {
            if (historyAll == null) return;
            List<HistoryItem> all = historyAll;
            string q = historyQuery;
            int f = historyFilter;
            bool fill = refill && settings.HistorySearch, show = !refill || HistoryIndex.Words(q).Length > 0;
            int gen = show ? ++historySearches : historySearches;   // only a search that shows something outdates the others
            int version = HistoryIndex.Version;
            Dispatcher ui = Dispatcher;
            HistoryWork(delegate
            {
                List<HistoryItem> shown = null;
                try
                {
                    if (fill) HistoryIndex.Fill(all);
                    if (show) shown = HistoryIndex.Search(all, q, f);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Historial: " + ex.Message);
                    return;
                }
                ui.BeginInvoke((Action)delegate
                {
                    if (all != historyAll || !HistoryLive) return;
                    if (fill) historyVersion = version;
                    if (shown == null || gen != historySearches) return;
                    HistoryShow(shown, HistoryIndex.Words(q), fromTop);
                });
            });
        }

        void HistoryShow(List<HistoryItem> shown, string[] words, bool fromTop)
        {
            historyShown = shown;
            historyWords = words;
            int n = shown.Count;
            if (historyCount != null)
            {
                if (historyAll == null || historyAll.Count == 0 || n == 0) historyCount.Text = "";
                else if (words.Length > 0)
                {
                    historyCount.Text = I18n.IsSpanish
                        ? (n == 1 ? "1 resultado" : n.ToString("N0", HistoryEs) + " resultados")
                        : (n == 1 ? "1 result" : n.ToString("N0", CultureInfo.InvariantCulture) + " results");
                }
                else
                {
                    historyCount.Text = I18n.IsSpanish
                        ? (n == 1 ? "1 elemento" : n.ToString("N0", HistoryEs) + " elementos")
                        : (n == 1 ? "1 item" : n.ToString("N0", CultureInfo.InvariantCulture) + " items");
                }
            }
            HistoryPaintNotice(n);
            if (historyGrid != null) historyGrid.Set(shown, words);
            if (fromTop) HistoryScrollToResults();
        }

        // After a new search the results start from the top, with the search box still in view.
        void HistoryScrollToResults()
        {
            if (historyBox == null) return;
            try
            {
                double y = historyBox.TransformToAncestor(pageHost).Transform(new Point(0, 0)).Y - Bar - 8;
                if (scroller.VerticalOffset > y) scroller.ScrollToVerticalOffset(Math.Max(0, y));
            }
            catch (InvalidOperationException) { }
        }

        void HistoryPulse()
        {
            historyPulsing = false;
            if (!HistoryLive) return;
            HistoryPaintStatus();
            if (historyAll == null || !settings.HistorySearch || HistoryIndex.Version == historyVersion) return;
            // New texts: attached, and a search typed runs again; at most every second and a half while reading goes on.
            if (historyRefill == null)
            {
                historyRefill = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1500) };
                historyRefill.Tick += delegate
                {
                    historyRefill.Stop();
                    if (HistoryLive) HistorySearchNow(false, true);
                };
            }
            if (!historyRefill.IsEnabled) historyRefill.Start();
        }

        // ---- One capture

        static string HistoryTime(DateTime t, int group)
        {
            string hm = t.ToString("HH:mm", HistoryEs);
            if (group <= 1) return hm;
            if (group == 2)
            {
                string day = t.ToString("dddd", HistoryEs);
                return char.ToUpper(day[0], HistoryEs) + day.Substring(1) + ", " + hm;
            }
            if (t.Year == DateTime.Now.Year) return t.ToString(I18n.IsSpanish ? "d MMM" : "MMM d", HistoryEs).Replace(".", "") + ", " + hm;
            return t.ToString(I18n.IsSpanish ? "d MMM yyyy" : "MMM d, yyyy", HistoryEs).Replace(".", "");
        }

        sealed class HistoryTile : TapPanel
        {
            public HistoryItem Item;
            public Border Img;
            public bool Asked;      // its thumbnail was asked for
            public bool Gone;       // its row was let go (scrolled far away, or a new list)
        }

        // Thumbnails are decoded from the full image on the thread pool: a grid of them asked for at once would decode a
        // dozen 4K captures side by side. They go a few at a time, top to bottom, skipping tiles already let go.
        readonly Queue<KeyValuePair<HistoryTile, int>> historyThumbs = new Queue<KeyValuePair<HistoryTile, int>>();
        int historyThumbsBusy;
        const int HistoryThumbsAtOnce = 3;

        void HistoryThumb(HistoryTile t, int px)
        {
            t.Asked = true;
            historyThumbs.Enqueue(new KeyValuePair<HistoryTile, int>(t, px));
            HistoryThumbNext();
        }

        void HistoryThumbNext()
        {
            if (!HistoryLive) historyThumbs.Clear();
            while (historyThumbsBusy < HistoryThumbsAtOnce && historyThumbs.Count > 0)
            {
                KeyValuePair<HistoryTile, int> job = historyThumbs.Dequeue();
                HistoryTile t = job.Key;
                if (t.Gone) continue;
                Border img = t.Img;
                bool finished = false;
                DispatcherTimer patience = null;
                Action finish = delegate
                {
                    if (finished) return;
                    finished = true;
                    if (patience != null) patience.Stop();
                    historyThumbsBusy--;
                    HistoryThumbNext();
                };
                historyThumbsBusy++;
                Recents.Thumb(t.Item.Path, t.Item.Ticks, job.Value, delegate(BitmapSource src)
                {
                    ImageBrush ib = new ImageBrush(src) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
                    RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.HighQuality);
                    img.Background = ib;
                    finish();
                });
                if (finished) continue;
                // A file that gives no thumbnail never answers: its turn ends after a while.
                patience = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
                patience.Tick += delegate { finish(); };
                patience.Start();
            }
        }

        // The thumbnail, its time and, while searching, the text around the match with every match marked.
        FrameworkElement HistoryCell(HistoryItem it, double h, string[] words, int group)
        {
            Palette pal = Ds.Brushes;
            HistoryTile t = new HistoryTile { Item = it, Cursor = Cursors.Hand, Background = Brushes.Transparent, Label = it.Name, Help = HistoryTime(it.When, group), Radius = 8,
                                              Role = Access.ListItem, Do = delegate { HistoryOpen(it, 0); } };
            Grid pic = new Grid { Height = h };
            Border img = new Border { CornerRadius = new CornerRadius(8), Background = Ds.Brush(pal.Thumb), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1) };
            pic.Children.Add(img);
            t.Img = img;
            if (it.Kind != HistoryKind.Image)
            {
                Border tag = new Border
                {
                    CornerRadius = new CornerRadius(7), Background = Ds.Brush(Palette.HudBack(Ds.Argb(0.55, 0, 0, 0))), Padding = new Thickness(5, 1, 5, 2),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5), IsHitTestVisible = false
                };
                tag.Child = Label(Path.GetExtension(it.Name).TrimStart('.').ToUpperInvariant(), Ds.Semibold, 9.5, Palette.HudLabel);
                pic.Children.Add(tag);
            }
            Border ring = new Border { CornerRadius = new CornerRadius(8), BorderBrush = Ds.Brush(pal.Accent), BorderThickness = new Thickness(2), Opacity = 0, IsHitTestVisible = false };
            pic.Children.Add(ring);
            t.Children.Add(pic);
            TextBlock when = Label(HistoryTime(it.When, group), Ds.Regular, 11.5, pal.Label2, new Thickness(2, 6, 2, 0));
            when.TextTrimming = TextTrimming.CharacterEllipsis;
            t.Children.Add(when);
            if (words.Length > 0)
            {
                HistorySnippet sn = HistoryIndex.Snippet(it, words, 90);
                TextBlock s = new TextBlock
                {
                    FontFamily = Ds.Text, FontSize = 11.5, Foreground = Ds.Brush(sn.FromName ? pal.Label2 : pal.Label), TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 15.5, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, MaxHeight = 31,
                    Margin = new Thickness(2, 2, 2, 0)
                };
                TextOptions.SetTextFormattingMode(s, TextFormattingMode.Display);
                Brush mark = Ds.Brush(pal.High ? pal.Accent : Ds.WithAlpha(pal.Yellow, pal.Dark ? 0.40 : 0.55));
                Brush ink = Ds.Brush(pal.High ? pal.OnAccent : pal.Label);
                int pos = 0;
                foreach (KeyValuePair<int, int> m in sn.Marks)
                {
                    if (m.Key > pos) s.Inlines.Add(new Run(sn.Text.Substring(pos, m.Key - pos)));
                    s.Inlines.Add(new Run(sn.Text.Substring(m.Key, m.Value)) { Background = mark, Foreground = ink });
                    pos = m.Key + m.Value;
                }
                if (pos < sn.Text.Length) s.Inlines.Add(new Run(sn.Text.Substring(pos)));
                t.Children.Add(s);
            }
            t.ToolTip = it.Name + "\n" + it.When.ToString(I18n.IsSpanish ? "dddd, d 'de' MMMM 'de' yyyy, HH:mm" : "dddd, MMMM d, yyyy, HH:mm", HistoryEs);
            ToolTipService.SetInitialShowDelay(t, 700);
            bool pressed = false;
            t.MouseEnter += delegate { HomeGlide(ring, OpacityProperty, 1, 120, null); };
            t.MouseLeave += delegate { pressed = false; HomeGlide(ring, OpacityProperty, 0, 200, null); };
            t.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { pressed = true; e.Handled = true; };
            t.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                if (!pressed) return;
                pressed = false;
                e.Handled = true;
                HistoryOpen(it, 0);
            };
            t.MouseRightButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                HistoryMenu(it, pic, e.GetPosition(pic));
            };
            t.State.Menu = delegate { HistoryMenu(it, pic, new Point(pic.ActualWidth / 2, pic.ActualHeight / 2)); };
            return t;
        }

        // Right click: the menu opens where the mouse is (a one-pixel anchor is put there for it).
        void HistoryMenu(HistoryItem it, Grid pic, Point at)
        {
            Border anchor = new Border { Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(at.X, at.Y, 0, 0), IsHitTestVisible = false };
            pic.Children.Add(anchor);
            pic.UpdateLayout();
            bool image = it.Kind == HistoryKind.Image;
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item(I18n.Open, "open", false, delegate { HistoryOpen(it, image ? 1 : 0); }));
            m.Add(MenuEntry.Item(I18n.Copy, "copy", false, delegate { HistoryCopy(it); }));
            m.Add(MenuEntry.Item(I18n.T("Mostrar en la carpeta", "Show in folder"), "folder", false, delegate { HistoryReveal(it); }));
            if (image) m.Add(MenuEntry.Item(I18n.T("Abrir en el editor", "Open in editor"), "edit", false, delegate { HistoryOpen(it, 2); }));
            Popup pop = MacMenu.Show(anchor, m);
            pop.Closed += delegate { pic.Children.Remove(anchor); };
        }

        // how: 0 as a click does (images in the editor, videos as the player setting says), 1 in Windows' own app (images),
        // 2 in the editor.
        void HistoryOpen(HistoryItem it, int how)
        {
            if (!File.Exists(it.Path)) { HistoryMissing(it); return; }
            if (how == 1)
            {
                try { Process.Start(new ProcessStartInfo(it.Path) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    ShotStack.Log("Historial: abrir " + it.Path + ": " + ex.Message);
                    owner.Notify(I18n.T("No se pudo abrir", "Could not open"), it.Name + ": " + ShotStack.ExplainOpen(ex, it.Path));
                }
                return;
            }
            if (how == 2)
            {
                if (!owner.OpenEditor(it.Path)) owner.Notify(I18n.T("No se pudo abrir", "Could not open"), it.Name + I18n.T(" no se puede abrir en el editor.", " cannot be opened in the editor."));
                return;
            }
            OpenCapture(it.Path);
        }

        void HistoryCopy(HistoryItem it)
        {
            if (!File.Exists(it.Path)) { HistoryMissing(it); return; }
            CopyFile(it.Path, null);
        }

        void HistoryReveal(HistoryItem it)
        {
            if (!File.Exists(it.Path)) { HistoryMissing(it); return; }
            try { Process.Start(Native.Explorer, "/select,\"" + it.Path + "\""); }
            catch (Exception ex)
            {
                ShotStack.Log("Historial: mostrar " + it.Path + ": " + ex.Message);
                owner.Notify(I18n.T("No se pudo abrir la carpeta", "Could not open folder"), it.Name + ": " + ShotStack.ExplainOpen(ex, it.Path));
            }
        }

        // Deleted or moved since the list was made: said once, and the list catches up.
        void HistoryMissing(HistoryItem it)
        {
            owner.Notify(I18n.T("No se encuentra la captura", "Capture not found"), it.Name + I18n.T(" ya no est\u00E1 en su carpeta: se ha movido o borrado.", " is no longer in its folder: moved or deleted."));
            HistoryLoad(false);
        }

        // ---- General > Recientes

        // The row that deletes the text index, only while there is one.
        Row HistoryIndexRowIfAny()
        {
            if (ShotStack.Test || !HistoryIndex.Exists) return null;
            return new HistoryIndexRow(ClearHistoryIndex);
        }

        void ClearHistoryIndex()
        {
            string again = settings.HistorySearch ? I18n.T(" Mientras la b\u00FAsqueda siga activada, Stackshot volver\u00E1 a leerlas poco a poco.", " While search remains on, Stackshot will gradually re-read them.") : "";
            if (DocAlert.Ask(this, I18n.T("\u00BFBorrar el \u00EDndice de texto?", "Delete text index?"), I18n.T("Tus capturas no se tocan.", "Your captures are untouched.") + again, new string[] { I18n.T("Borrar el \u00EDndice", "Delete index"), I18n.Cancel }, 0, 1) != 0) return;
            HistoryIndex.Delete();
            SyncRows();
        }

        public class HistoryIndexRow : ButtonRow
        {
            public HistoryIndexRow(Action act) : base(I18n.T("Borrar el \u00EDndice de texto", "Delete text index"), null, "trash", Mac.Red, Mac.Orange, I18n.T("Borrar", "Delete"), act)
            {
                SubNow = delegate { return I18n.T("Lo que Stackshot ha le\u00EDdo en tus capturas (", "What Stackshot has indexed from your captures (") + Recents.Size(HistoryIndex.Bytes) + I18n.T("). Se vuelve a crear solo mientras la b\u00FAsqueda est\u00E9 activada.", "). Rebuilt automatically while search is on."); };
            }

            // Gone once deleted, with the separator above it.
            public override void Sync(bool animate)
            {
                base.Sync(animate);
                Visibility v = HistoryIndex.Exists ? Visibility.Visible : Visibility.Collapsed;
                if (Visibility == v) return;
                Visibility = v;
                Panel list = Parent as Panel;
                int i = list != null ? list.Children.IndexOf(this) : -1;
                if (i > 0) list.Children[i - 1].Visibility = v;
            }
        }

        // ---- The grid

        // The results in rows of thumbnails under a title per group of days. Every row's height is known, so the whole list
        // is laid out without building it and only the rows near what the page's scroller shows exist. While scrolling,
        // thumbnails are asked for once it rests, so a fling doesn't decode hundreds of images nobody sees.
        sealed class HistoryGrid : Panel
        {
            const double Gap = 12, MinCell = 148, HeadH = 40, Overscan = 500;

            protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() { return new RolePeer(this, Access.List, I18n.T("Capturas", "Captures")); }

            sealed class Line
            {
                public int Group, Count;
                public List<HistoryItem> Items;    // null: the group's title
            }

            readonly HomeWindow w;
            readonly List<Line> lines = new List<Line>();
            readonly List<double> tops = new List<double>();
            readonly Dictionary<int, FrameworkElement> live = new Dictionary<int, FrameworkElement>();
            readonly DispatcherTimer settle;
            List<HistoryItem> items = new List<HistoryItem>();
            string[] words = new string[0];
            double width = -1, cell, thumb, rowH, total;
            int cols = 4;
            bool eager, narrow;     // narrow: this pass builds only the rows on screen; those around them follow at once
            DateTime now = DateTime.Now, settleFrom;

            public HistoryGrid(HomeWindow w)
            {
                this.w = w;
                settle = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(90) };
                settle.Tick += delegate
                {
                    settle.Stop();
                    if (w.historyGrid == this && w.HistoryLive) LoadThumbs();
                };
            }

            public int LiveRows { get { return live.Count; } }

            public void Set(List<HistoryItem> list, string[] terms)
            {
                items = list ?? new List<HistoryItem>();
                words = terms ?? new string[0];
                now = DateTime.Now;
                Forget();
                if (width > 0) Lay();
                eager = true;   // not a scroll: thumbnails already decoded show at once
                narrow = true;
                InvalidateMeasure();
            }

            public void Scrolled() { InvalidateMeasure(); }

            void Forget()
            {
                foreach (FrameworkElement row in live.Values) Let(row);
                InternalChildren.Clear();
                live.Clear();
            }

            // A row leaves: its thumbnails still waiting are not decoded.
            static void Let(FrameworkElement row)
            {
                Grid g = row as Grid;
                if (g == null) return;
                foreach (UIElement c in g.Children)
                {
                    HistoryTile t = c as HistoryTile;
                    if (t != null) t.Gone = true;
                }
            }

            void Lay()
            {
                cols = Math.Max(2, (int)((width + Gap) / (MinCell + Gap)));
                cell = Math.Floor((width - Gap * (cols - 1)) / cols);
                thumb = Math.Round(cell * 10 / 16);
                rowH = thumb + 6 + 17 + (words.Length > 0 ? 33 : 0) + Gap + 4;
                lines.Clear();
                tops.Clear();
                double y = 0;
                int i = 0;
                while (i < items.Count)
                {
                    int g = HistoryIndex.GroupOf(items[i].When, now), j = i;
                    while (j < items.Count && HistoryIndex.GroupOf(items[j].When, now) == g) j++;
                    Line head = new Line();
                    head.Group = g;
                    head.Count = j - i;
                    lines.Add(head);
                    tops.Add(y);
                    y += HeadH;
                    for (int k = i; k < j; k += cols)
                    {
                        Line row = new Line();
                        row.Group = g;
                        row.Items = items.GetRange(k, Math.Min(cols, j - k));
                        lines.Add(row);
                        tops.Add(y);
                        y += rowH;
                    }
                    i = j;
                }
                total = y;
            }

            double HeightOf(int i) { return lines[i].Items == null ? HeadH : rowH; }

            // How many live lines come before line i.
            int Before(int i)
            {
                int n = 0;
                foreach (int k in live.Keys) if (k < i) n++;
                return n;
            }

            protected override Size MeasureOverride(Size avail)
            {
                double wd = double.IsInfinity(avail.Width) || avail.Width <= 0 ? 640 : avail.Width;
                if (Math.Abs(wd - width) > 0.5)
                {
                    width = wd;
                    Forget();
                    Lay();
                    narrow = true;
                }
                Realize();
                foreach (KeyValuePair<int, FrameworkElement> kv in live) kv.Value.Measure(new Size(width, HeightOf(kv.Key)));
                return new Size(wd, total);
            }

            protected override Size ArrangeOverride(Size final)
            {
                foreach (KeyValuePair<int, FrameworkElement> kv in live) kv.Value.Arrange(new Rect(0, tops[kv.Key], width, HeightOf(kv.Key)));
                return final;
            }

            // The part of the list the page shows, in this panel's coordinates, with room around it.
            void Range(out double lo, out double hi)
            {
                ScrollViewer sv = w.scroller;
                double vh = sv.ViewportHeight;
                if (double.IsNaN(vh) || vh <= 0) vh = sv.ActualHeight > 0 ? sv.ActualHeight : 760;
                double top = 0;
                try { if (sv.IsAncestorOf(this)) top = -TransformToAncestor(sv).Transform(new Point(0, 0)).Y; }
                catch (InvalidOperationException) { }
                double room = narrow ? 0 : Overscan;
                lo = top - room;
                hi = top + vh + room;
            }

            // The line at y.
            int At(double y)
            {
                int a = 0, b = tops.Count - 1;
                while (a < b)
                {
                    int m = (a + b + 1) / 2;
                    if (tops[m] <= y) a = m;
                    else b = m - 1;
                }
                return a;
            }

            void Realize()
            {
                double lo, hi;
                Range(out lo, out hi);
                int first = 0, last = -1;
                if (lines.Count > 0 && hi > 0 && lo < total)
                {
                    first = At(Math.Max(0, lo));
                    last = At(Math.Min(total, hi));
                }
                List<int> gone = new List<int>();
                foreach (int i in live.Keys) if (i < first || i > last) gone.Add(i);
                foreach (int i in gone)
                {
                    Let(live[i]);
                    InternalChildren.Remove(live[i]);
                    live.Remove(i);
                }
                bool added = false;
                for (int i = first; i <= last; i++)
                {
                    if (live.ContainsKey(i)) continue;
                    FrameworkElement e = Make(lines[i]);
                    live[i] = e;
                    InternalChildren.Insert(Before(i), e);   // in reading order, which is also the order of Tab
                    added = true;
                }
                if (narrow)
                {
                    narrow = false;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)InvalidateMeasure);
                }
                if (eager)
                {
                    eager = false;
                    settle.Stop();
                    LoadThumbs();
                }
                else if (added)
                {
                    // Once scrolling rests; a long scroll still gets its thumbnails every 400 ms or so.
                    if (!settle.IsEnabled) settleFrom = DateTime.UtcNow;
                    else if ((DateTime.UtcNow - settleFrom).TotalMilliseconds > 400) return;
                    settle.Stop();
                    settle.Start();
                }
            }

            FrameworkElement Make(Line l)
            {
                Palette pal = Ds.Brushes;
                if (l.Items == null)
                {
                    StackPanel h = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 10) };
                    h.Children.Add(Label(HistoryIndex.GroupNames[l.Group], Ds.Semibold, 13.5, pal.Label));
                    h.Children.Add(Label(l.Count.ToString("N0", HistoryEs), Ds.Regular, 12, pal.Label3, new Thickness(8, 1, 0, 0)));
                    return h;
                }
                Grid row = new Grid();
                for (int c = 0; c < cols; c++)
                {
                    if (c > 0) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Gap) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
                }
                for (int c = 0; c < l.Items.Count; c++)
                {
                    FrameworkElement v = w.HistoryCell(l.Items[c], thumb, words, l.Group);
                    v.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(v, c * 2);
                    row.Children.Add(v);
                }
                return row;
            }

            void LoadThumbs()
            {
                double k = 1.25;
                try { k = VisualTreeHelper.GetDpi(this).DpiScaleX; }
                catch (Exception) { }
                int px = (int)Math.Ceiling(cell * k * 1.25);
                List<int> order = new List<int>(live.Keys);
                order.Sort();
                foreach (int i in order)
                {
                    Grid g = live[i] as Grid;
                    if (g == null) continue;
                    foreach (UIElement c in g.Children)
                    {
                        HistoryTile t = c as HistoryTile;
                        if (t != null && !t.Asked) w.HistoryThumb(t, px);
                    }
                }
            }
        }
    }
}
