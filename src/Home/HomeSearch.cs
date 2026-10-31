// Stackshot - Settings search: a field at the top of the sidebar (Ctrl+F), the results over the pane grouped by page with
// the words found marked, and opening one goes to its page, brings its row to the middle and makes it glow a moment.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Stackshot
{
    public partial class HomeWindow
    {
        const double SearchBand = 30;          // what the field adds to the top of the sidebar
        const int SearchMaxShown = 50, SearchFirstRows = 8, SearchMoreRows = 8;

        // Whether the page glides and glows (Windows' animation effects); tests turn it off to read where things end up.
        internal static Func<bool> HomeMotion = delegate { return SystemParameters.ClientAreaAnimation; };

        TextBox searchBox;
        Border searchClear;
        TextBlock searchHint;
        string searchText = "";
        DispatcherTimer searchDebounce;
        Grid searchView;                       // over the pane while the results show
        ScrollViewer searchScroller;
        StackPanel searchList;
        readonly List<SearchHit> searchHits = new List<SearchHit>();
        readonly List<Border> searchRows = new List<Border>();
        List<SearchHit> searchPending;         // the results to show while some are still to be drawn
        int searchPendingLeft, searchDrawGen;  // results past the most shown; the drawing under way
        StackPanel searchCardList;             // the card the next result goes in
        int searchSel = -1;
        string searchFrom;                     // the page under the results
        string searchShownQuery;
        Palette searchShownPalette;
        bool searchKeysHooked;
        Border searchGlow;                     // the glow on the row just opened
        AnimationTimeline glide;               // the page gliding to a row or a section

        internal TextBox SearchBox { get { return searchBox; } }
        internal bool SearchShowing { get { return searchView != null && searchView.Visibility == Visibility.Visible; } }
        internal List<SearchHit> SearchResults { get { return new List<SearchHit>(searchHits); } }
        internal int SearchSelected { get { return searchSel; } }
        internal FrameworkElement SearchGlowView { get { return searchGlow; } }
        internal string SearchPage { get { return page; } }

        // ---- The field

        FrameworkElement SearchField()
        {
            Palette pal = Ds.Brushes;
            bool hadFocus = searchBox != null && searchBox.IsKeyboardFocusWithin;
            Color rest = pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.62, 255, 255, 255);
            Color line = pal.Dark ? Ds.Argb(0.09, 255, 255, 255) : Ds.Argb(0.09, 0, 0, 0);
            Grid g = new Grid { Height = 28, Margin = new Thickness(10, 0, 10, 6), VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.IBeam };
            Border frame = new Border { CornerRadius = new CornerRadius(7), Background = Ds.Brush(rest), BorderBrush = Ds.Brush(line), BorderThickness = new Thickness(1) };
            g.Children.Add(frame);
            GlyphView icon = new GlyphView("search", 13, pal.Label2, 1.7) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0) };
            g.Children.Add(icon);
            TextBlock hint = Label(I18n.T("Buscar ajustes", "Search settings"), Ds.Regular, 12.5, pal.Label3, new Thickness(28, 0, 0, 1));
            hint.IsHitTestVisible = false;
            hint.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(hint);
            TextBox box = new TextBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(26, 0, 24, 1),
                FontFamily = Ds.Text, FontSize = 12.5, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(pal.Accent),
                MaxLength = 80, VerticalContentAlignment = VerticalAlignment.Center, Template = DockFieldTemplate(), Text = searchText
            };
            AutomationProperties.SetName(box, I18n.T("Buscar ajustes", "Search settings"));
            AutomationProperties.SetHelpText(box, "Ctrl+F");
            g.Children.Add(box);
            Border clear = new TapBorder
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Arrow, Background = Ds.Brush(pal.Dark ? Ds.Argb(0.32, 255, 255, 255) : Ds.Argb(0.28, 0, 0, 0)),
                ToolTip = I18n.T("Borrar la b\u00FAsqueda", "Clear search"), Label = I18n.T("Borrar la b\u00FAsqueda", "Clear search"), Do = delegate { box.Text = ""; box.Focus(); }
            };
            clear.Child = new GlyphView("close", 9, pal.Dark ? Ds.Rgb(44, 44, 48) : Colors.White, 2.2) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            clear.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; };
            clear.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; box.Text = ""; box.Focus(); };
            g.Children.Add(clear);
            g.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
            {
                if (box.IsKeyboardFocusWithin) return;
                box.Focus();
                box.CaretIndex = box.Text.Length;
                e.Handled = true;
            };
            box.TextChanged += delegate { SearchTyped(); };
            box.GotKeyboardFocus += delegate
            {
                frame.BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.85));
                frame.BorderThickness = new Thickness(1.5);
                icon.Color = pal.Accent;
                // The index is made on first use, while the first word is still being typed.
                SearchIndexSoon();
                if (box.Text.Trim().Length > 0 && !SearchShowing) SearchRun();
            };
            box.LostKeyboardFocus += delegate
            {
                frame.BorderBrush = Ds.Brush(line);
                frame.BorderThickness = new Thickness(1);
                icon.Color = pal.Label2;
            };
            box.PreviewKeyDown += SearchFieldKey;
            Grid.SetRow(g, 0);
            searchBox = box;
            searchHint = hint;
            searchClear = clear;
            SearchFieldSync();
            // Rebuilt with the theme while it was being used: the new one takes over.
            if (hadFocus) Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate { box.Focus(); box.CaretIndex = box.Text.Length; });
            return g;
        }

        void SearchFieldSync()
        {
            if (searchBox == null) return;
            bool any = searchBox.Text.Length > 0;
            searchHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            searchClear.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        void SearchTyped()
        {
            searchText = searchBox.Text;
            SearchFieldSync();
            if (searchText.Trim().Length == 0)
            {
                if (searchDebounce != null) searchDebounce.Stop();
                SearchHide();
                return;
            }
            SearchIndexSoon();
            if (searchDebounce == null)
            {
                searchDebounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
                searchDebounce.Tick += delegate { SearchRun(); };
            }
            searchDebounce.Stop();
            searchDebounce.Start();
        }

        // Down and Up choose a result, Enter opens it; Esc empties the field (back to the page) and then leaves it.
        void SearchFieldKey(object o, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Up:
                    if (!SearchShowing) return;
                    e.Handled = true;
                    SearchMove(e.Key == Key.Down ? 1 : -1);
                    break;
                case Key.Enter:
                    e.Handled = true;
                    if (searchDebounce != null && searchDebounce.IsEnabled) SearchRun();
                    if (SearchShowing && searchSel >= 0) SearchOpen(searchSel);
                    break;
                case Key.Escape:
                    e.Handled = true;
                    if (searchBox.Text.Length > 0) searchBox.Text = "";
                    else { SearchHide(); Focus(); }
                    break;
            }
        }

        void HookSearchKeys()
        {
            if (searchKeysHooked) return;
            searchKeysHooked = true;
            // After the window's own handler, which keeps the keys while a hotkey is being recorded.
            PreviewKeyDown += SearchKeys;
            scroller.PreviewMouseWheel += delegate { StopGlide(); };
            scroller.PreviewMouseDown += delegate { StopGlide(); };
        }

        void SearchKeys(object sender, KeyEventArgs e)
        {
            if (listening != null || searchBox == null) return;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != Key.F || e.KeyboardDevice.Modifiers != ModifierKeys.Control || SearchCtrlFStays()) return;
            e.Handled = true;
            searchBox.Focus();
            searchBox.SelectAll();
        }

        // Ctrl+F stays with a page that has a search of its own: Historial, and the mascot's wardrobe.
        internal bool SearchCtrlFStays()
        {
            if (SearchShowing) return false;
            return (page == "history" && historyBox != null && HistoryLive) || (page == "mascot" && dockSearch != null);
        }

        // ---- Results

        // Runs the search now (the debounce, a finished index, a test).
        void SearchRun()
        {
            if (searchDebounce != null) searchDebounce.Stop();
            string q = searchText.Trim();
            if (q.Length == 0 || searchBox == null) { SearchHide(); return; }
            List<SearchEntry> all = SearchEntries();
            List<SearchHit> hits = all != null ? SettingsSearch.Find(all, q) : new List<SearchHit>();
            SearchShow();
            SearchRender(q, hits, searchIndex != null);
        }

        // Types into the field and shows the results at once, with the index built first if needed (tests).
        internal void SearchFor(string text)
        {
            if (!SearchIndexReady) SearchIndexNow(null);
            searchBox.Text = text;
            SearchRun();
            SearchDrawRows(int.MaxValue);
        }

        void SearchShow()
        {
            EnsureSearchView();
            if (SearchShowing) return;
            searchFrom = page;
            StopGlide();
            searchView.Visibility = Visibility.Visible;
            // Hidden, not collapsed: the page keeps its layout and scroll for when the search ends.
            scroller.Visibility = Visibility.Hidden;
            SearchToolbar();
            searchScroller.ScrollToVerticalOffset(0);
            ToolbarShade();
        }

        // While the results show, the toolbar names them (a sub-page's back button waits).
        void SearchToolbar()
        {
            toolbarTitle.Text = I18n.T("Resultados", "Results");
            toolbarTitle.Margin = new Thickness(40, 0, 0, 0);
            foreach (UIElement c in toolbarRow.Children) if (c != toolbarTitle) c.Visibility = Visibility.Collapsed;
        }

        internal void SearchHide()
        {
            if (!SearchShowing) return;
            searchView.Visibility = Visibility.Collapsed;
            scroller.Visibility = Visibility.Visible;
            SearchClearRows();
            toolbarTitle.Text = PageTitle(page);
            BuildToolbar();
            ToolbarShade();
        }

        // Empty, with any drawing still under way called off.
        void SearchClearRows()
        {
            searchDrawGen++;
            searchPending = null;
            searchCardList = null;
            searchList.Children.Clear();
            searchHits.Clear();
            searchRows.Clear();
            searchSel = -1;
        }

        double PaneOffset() { return SearchShowing ? searchScroller.VerticalOffset : scroller.VerticalOffset; }

        void EnsureSearchView()
        {
            if (searchView == null)
            {
                searchView = new Grid { Visibility = Visibility.Collapsed };
                searchScroller = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Focusable = false, PanningMode = PanningMode.VerticalOnly, Resources = ScrollStyle()
                };
                searchList = new StackPanel { Margin = new Thickness(40, 0, 40, 40), MaxWidth = 660, HorizontalAlignment = HorizontalAlignment.Stretch };
                searchScroller.Content = searchList;
                searchScroller.ScrollChanged += delegate { ToolbarShade(); };
                searchView.Children.Add(searchScroller);
                pane.Children.Insert(pane.Children.IndexOf(toolbar), searchView);
            }
            searchView.Background = Ds.Brush(Ds.Brushes.Window);
        }

        void SearchRender(string q, List<SearchHit> hits, bool complete)
        {
            Palette pal = Ds.Brushes;
            searchShownPalette = pal;
            searchView.Background = Ds.Brush(pal.Window);
            SearchClearRows();
            // Other words start at the top; the same ones drawn again (a theme) stay where they were.
            if (q != searchShownQuery) searchScroller.ScrollToVerticalOffset(0);
            searchShownQuery = q;
            searchList.Children.Add(Label(I18n.T("Resultados", "Results"), Ds.Title, 26, pal.Label, new Thickness(0, 58, 0, 0)));
            string quoted = I18n.IsSpanish ? "\u00AB" + q + "\u00BB" : "\u201C" + q + "\u201D";
            if (hits.Count == 0)
            {
                searchList.Children.Add(complete ? SearchNothing(quoted) : SearchWaiting());
                return;
            }
            string countMsg = I18n.IsSpanish
                ? (hits.Count == 1 ? "1 ajuste coincide con " + quoted + "." : hits.Count + " ajustes coinciden con " + quoted + ".")
                : (hits.Count == 1 ? "1 setting matches " + quoted + "." : hits.Count + " settings match " + quoted + ".");
            TextBlock count = Paragraph(countMsg, 13, pal.Label2);
            count.Margin = new Thickness(0, 6, 0, 0);
            searchList.Children.Add(count);
            searchList.Children.Add(new Border { Height = 24 });
            searchPending = hits.GetRange(0, Math.Min(hits.Count, SearchMaxShown));
            searchPendingLeft = hits.Count - searchPending.Count;
            searchCardList = null;
            searchSel = 0;
            SearchDrawRows(SearchFirstRows);
        }

        // Draws the next n results: the first screenful at once, the rest a few at a time below input, so a first letter
        // that matches most settings never holds up the next one.
        void SearchDrawRows(int n)
        {
            if (searchPending == null) return;
            for (int k = 0; k < n && searchHits.Count < searchPending.Count; k++)
            {
                int i = searchHits.Count;
                SearchHit h = searchPending[i];
                if (searchCardList == null || h.Entry.Page != searchHits[i - 1].Entry.Page)
                {
                    searchList.Children.Add(SearchCaption(h.Entry));
                    searchCardList = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
                    searchList.Children.Add(SearchCard(searchCardList));
                }
                Border row = SearchRow(h, i);
                searchCardList.Children.Add(row);
                searchRows.Add(row);
                searchHits.Add(h);
            }
            SearchPaintRows();
            if (searchHits.Count < searchPending.Count)
            {
                int gen = searchDrawGen;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate { if (gen == searchDrawGen && SearchShowing && !IsDisposed) SearchDrawRows(SearchMoreRows); });
                return;
            }
            if (searchPendingLeft > 0)
            {
                string moreMsg = I18n.IsSpanish ? ("Y " + searchPendingLeft + " m\u00E1s: a\u00F1ade otra palabra para afinar.") : ("And " + searchPendingLeft + " more: add another word to refine.");
                TextBlock more = Paragraph(moreMsg, 12, Ds.Brushes.Label2);
                more.Margin = new Thickness(8, -12, 8, 0);
                searchList.Children.Add(more);
            }
            searchPending = null;
        }

        FrameworkElement SearchNothing(string quoted)
        {
            Palette pal = Ds.Brushes;
            StackPanel p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 54, 0, 0), MaxWidth = 420 };
            p.Children.Add(new GlyphView("search", 34, pal.Label3, 1.6) { HorizontalAlignment = HorizontalAlignment.Center });
            string titleMsg = I18n.IsSpanish ? ("No hay ajustes que coincidan con " + quoted) : ("No settings match " + quoted);
            TextBlock t = Label(titleMsg, Ds.Semibold, 15, pal.Label, new Thickness(0, 14, 0, 0));
            t.TextWrapping = TextWrapping.Wrap;
            t.TextAlignment = TextAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(t);
            string helpMsg = I18n.IsSpanish ? "Prueba con otras palabras o con menos letras, como \u00ABcarpeta\u00BB, \u00ABsonido\u00BB o \u00ABatajo\u00BB." : "Try different words or fewer letters, such as \u201Cfolder\u201D, \u201Csound\u201D or \u201Cshortcut\u201D.";
            TextBlock h = Paragraph(helpMsg, 12.5, pal.Label2);
            h.TextAlignment = TextAlignment.Center;
            h.Margin = new Thickness(0, 6, 0, 0);
            p.Children.Add(h);
            return p;
        }

        // The first search while the index is still being made (a moment).
        FrameworkElement SearchWaiting()
        {
            TextBlock t = Paragraph(I18n.T("Buscando\u2026", "Searching\u2026"), 13, Ds.Brushes.Label2);
            t.Margin = new Thickness(0, 6, 0, 0);
            return t;
        }

        FrameworkElement SearchCaption(SearchEntry e)
        {
            int pi = Array.IndexOf(PageIds, e.Page == "mascotset" ? "mascot" : e.Page);
            StackPanel cap = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 8) };
            if (pi >= 0) cap.Children.Add(new IconTile(PageIcons[pi], 16, W(PageColors[pi]), W(Darker(PageColors[pi]))) { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
            cap.Children.Add(Label(e.PageName, Ds.Semibold, 12.5, Ds.Brushes.Label2));
            return cap;
        }

        // The rounded card of a group of results, as a settings group.
        static FrameworkElement SearchCard(StackPanel list)
        {
            Palette pal = Ds.Brushes;
            Border card = new Border
            {
                CornerRadius = new CornerRadius(10), Background = Ds.Brush(pal.Group), BorderThickness = new Thickness(1), Child = list,
                BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.07, 0, 0, 0))
            };
            Grid holder = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            if (!pal.Dark) holder.Children.Add(new SoftShadow(10, 3, 0.5, 0.06));
            holder.Children.Add(card);
            return holder;
        }

        // A result: the row's icon, its title and description with the words found marked, and where it lives.
        Border SearchRow(SearchHit h, int i)
        {
            Palette pal = Ds.Brushes;
            SearchEntry e = h.Entry;
            Border b = new TapBorder { CornerRadius = new CornerRadius(7), Margin = new Thickness(5, 1, 5, 1), Padding = new Thickness(9, 7, 10, 8), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                                       Label = e.Title, Help = e.Group != null ? e.PageName + ", " + e.Group : e.PageName, Role = Access.Link, Do = delegate { SearchOpen(i); } };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // Rows drawn without an icon of their own (the backdrop style) take their page's.
            string icon = e.Icon;
            Color c1 = e.C1, c2 = e.C2;
            int pi = Array.IndexOf(PageIds, e.Page == "mascotset" ? "mascot" : e.Page);
            if (icon == null) icon = pi >= 0 ? PageIcons[pi] : "gear";
            if (c1.A == 0 && pi >= 0) { c1 = W(PageColors[pi]); c2 = W(Darker(PageColors[pi])); }
            g.Children.Add(new IconTile(icon, 28, c1, c2) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) });
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            TextBlock title = Marked(e.Title, h.TitleMarks, 13.5, pal.Label);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(title);
            if (e.Sub != null)
            {
                TextBlock sub = Marked(e.Sub, h.SubMarks, 12, pal.Label2);
                sub.TextWrapping = TextWrapping.Wrap;
                sub.TextTrimming = TextTrimming.CharacterEllipsis;
                sub.LineHeight = 16;
                sub.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                sub.MaxHeight = 32;
                sub.Margin = new Thickness(0, 2, 0, 0);
                text.Children.Add(sub);
            }
            TextBlock where = Label(e.Group != null ? e.PageName + "\u00A0\u203A\u00A0" + e.Group : e.PageName, Ds.Regular, 11.5, pal.Label2, new Thickness(0, 3, 0, 0));
            where.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(where);
            g.Children.Add(text);
            GlyphView go = new GlyphView("right", 12, pal.Label3, 1.6) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(go, 2);
            g.Children.Add(go);
            b.Child = g;
            b.MouseEnter += delegate { SearchPaintRow(i, true); };
            b.MouseLeave += delegate { SearchPaintRow(i, false); };
            b.MouseLeftButtonUp += delegate { SearchOpen(i); };
            return b;
        }

        // The text with the marked parts on yellow, as the history search shows them.
        static TextBlock Marked(string text, List<KeyValuePair<int, int>> marks, double size, Color color)
        {
            Palette pal = Ds.Brushes;
            TextBlock t = new TextBlock { FontFamily = Ds.Regular.FontFamily, FontWeight = Ds.Regular.Weight, FontSize = size, Foreground = Ds.Brush(color), VerticalAlignment = VerticalAlignment.Center };
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            Brush mark = Ds.Brush(Ds.WithAlpha(pal.Yellow, pal.Dark ? 0.40 : 0.55)), ink = Ds.Brush(pal.Label);
            int pos = 0;
            foreach (KeyValuePair<int, int> m in marks)
            {
                if (m.Key < pos || m.Value <= 0 || m.Key + m.Value > text.Length) continue;
                if (m.Key > pos) t.Inlines.Add(new Run(text.Substring(pos, m.Key - pos)));
                t.Inlines.Add(new Run(text.Substring(m.Key, m.Value)) { Background = mark, Foreground = ink });
                pos = m.Key + m.Value;
            }
            if (pos < text.Length) t.Inlines.Add(new Run(text.Substring(pos)));
            return t;
        }

        void SearchPaintRow(int i, bool hot)
        {
            if (i < 0 || i >= searchRows.Count) return;
            Palette pal = Ds.Brushes;
            searchRows[i].Background = i == searchSel ? Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.28 : 0.14)) : hot ? Ds.Brush(pal.Control) : Brushes.Transparent;
        }

        void SearchPaintRows()
        {
            for (int i = 0; i < searchRows.Count; i++) SearchPaintRow(i, searchRows[i].IsMouseOver);
        }

        void SearchMove(int d)
        {
            if (searchRows.Count == 0) return;
            // Down past what is drawn so far: the next ones now.
            if (d > 0 && searchSel >= searchRows.Count - 1) SearchDrawRows(SearchMoreRows);
            searchSel = Math.Max(0, Math.Min(searchRows.Count - 1, searchSel < 0 ? 0 : searchSel + d));
            SearchPaintRows();
            if (searchSel == 0) { searchScroller.ScrollToVerticalOffset(0); return; }
            // The chosen result stays in view, under the toolbar.
            FrameworkElement row = searchRows[searchSel];
            if (row.ActualHeight <= 0) searchScroller.UpdateLayout();
            try
            {
                Point p = row.TranslatePoint(new Point(0, 0), searchList);
                double top = p.Y - 8, bottom = p.Y + row.ActualHeight + 8, off = searchScroller.VerticalOffset, view = searchScroller.ViewportHeight;
                if (top < off + Bar) searchScroller.ScrollToVerticalOffset(Math.Max(0, top - Bar));
                else if (bottom > off + view) searchScroller.ScrollToVerticalOffset(bottom - view);
            }
            catch (InvalidOperationException) { }
        }

        // ---- Opening a result

        // Goes to the row's page, glides it to the middle of the view and makes it glow. The field keeps the words, so
        // clicking it again brings the results back.
        internal void SearchOpen(int i)
        {
            if (i < 0 || i >= searchHits.Count) return;
            SearchEntry e = searchHits[i].Entry;
            SearchHide();
            Focus();
            bool fresh = true;
            if (e.Page != page) SetPage(e.Page, true);
            else fresh = false;
            FrameworkElement at = SearchLiveView(e, true);
            // The page on show was built before what it shows changed (a quality chosen, a row gone): built again.
            if (at == null && !fresh) { Rebuild(); at = SearchLiveView(e, true); }
            if (at == null) at = SearchLiveView(e, false);
            if (at == null) return;
            UpdateLayout();
            SearchScrollTo(at);
            SearchShine(at);
        }

        // The same setting on the page as built now: by its place, by its title as the row says it now (a folder row
        // whose folder changed) or, unless exact, its group. Folded away since (another quality chosen, a row gone), the
        // closest box around it that shows.
        FrameworkElement SearchLiveView(SearchEntry e, bool exact)
        {
            if (liveBody != body) return null;
            FrameworkElement at = null;
            foreach (SearchEntry m in liveMarks) if (m.Key == e.Key && m.View != null) { at = m.View; break; }
            if (at == null)
                foreach (SearchEntry m in liveMarks)
                {
                    Row r = m.View as Row;
                    string now = r != null && r.Title != null ? r.Title.Trim() : m.Title;
                    if (m.View != null && m.Group == e.Group && now == e.Title) { at = m.View; break; }
                }
            if (at == null && !exact && e.Group != null)
                foreach (SectionMark s in liveSections)
                {
                    if (s.Title != e.Group) continue;
                    // A group's caption: its card, right below it.
                    int i = body.Children.IndexOf(s.Anchor);
                    at = s.Anchor is TextBlock && i >= 0 && i + 1 < body.Children.Count ? body.Children[i + 1] as FrameworkElement : s.Anchor;
                    break;
                }
            if (at == null) return null;
            FrameworkElement shown = SearchShown(at);
            return exact && shown != at ? null : shown;
        }

        // The element, or the nearest one around it that is not collapsed or hidden.
        FrameworkElement SearchShown(FrameworkElement at)
        {
            List<FrameworkElement> up = new List<FrameworkElement>();
            int hidden = -1;
            for (DependencyObject d = at; d != null && d != pageHost; d = VisualTreeHelper.GetParent(d))
            {
                FrameworkElement f = d as FrameworkElement;
                if (f == null) continue;
                if (f.Visibility != Visibility.Visible) hidden = up.Count;
                up.Add(f);
            }
            if (hidden < 0) return at;
            return hidden + 1 < up.Count ? up[hidden + 1] : null;
        }

        // The corners of what glows: a rounded box, or a tile drawn as a panel over one.
        static double SearchCorner(FrameworkElement at)
        {
            Border b = at as Border;
            Panel p = at as Panel;
            if (b == null && p != null && p.Children.Count > 0) b = p.Children[0] as Border;
            return b != null ? b.CornerRadius.TopLeft : 9;
        }

        void SearchScrollTo(FrameworkElement at)
        {
            Point p;
            try { p = at.TranslatePoint(new Point(0, 0), pageHost); }
            catch (InvalidOperationException) { return; }
            double top = Bar + (sectionMarks != null ? SectionPinH : 0), view = scroller.ViewportHeight;
            if (view <= top) return;
            double to = p.Y + at.ActualHeight / 2 - (top + view) / 2;
            if (at.ActualHeight > view - top - 24) to = p.Y - top - 12;   // taller than the view: its top under the bars
            GlideTo(Math.Max(0, Math.Min(scroller.ScrollableHeight, to)));
        }

        // A soft accent glow over the row that fades by itself.
        void SearchShine(FrameworkElement at)
        {
            if (searchGlow != null) { pageHost.Children.Remove(searchGlow); searchGlow = null; }
            Point p;
            try { p = at.TranslatePoint(new Point(0, 0), pageHost); }
            catch (InvalidOperationException) { return; }
            Palette pal = Ds.Brushes;
            bool row = at is Row;
            Rect r = new Rect(p.X, p.Y, Math.Max(0, at.ActualWidth), Math.Max(0, at.ActualHeight));
            r.Inflate(row ? 8 : 3, row ? 0 : 3);    // a row sits 14 px inside its card
            double radius = row ? 8 : SearchCorner(at) + 3;
            Border glow = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(r.X, r.Y, 0, 0),
                Width = r.Width, Height = r.Height, CornerRadius = new CornerRadius(radius), IsHitTestVisible = false, BorderThickness = new Thickness(1.5),
                Background = Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.22 : 0.13)), BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.55 : 0.45))
            };
            pageHost.Children.Add(glow);
            searchGlow = glow;
            Action gone = delegate
            {
                if (searchGlow == glow) searchGlow = null;
                pageHost.Children.Remove(glow);
            };
            if (!HomeMotion()) { Later(1200, gone); return; }
            DoubleAnimationUsingKeyFrames a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1250) };
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250)), new CubicEase { EasingMode = EasingMode.EaseInOut }));
            a.Completed += delegate { gone(); };
            glow.Opacity = 0;
            glow.BeginAnimation(OpacityProperty, a);
        }

        // ---- The page gliding to an offset

        static readonly DependencyProperty PageGlideProperty = DependencyProperty.RegisterAttached("PageGlide", typeof(double), typeof(HomeWindow),
            new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs a)
            {
                ScrollViewer s = d as ScrollViewer;
                if (s != null) s.ScrollToVerticalOffset((double)a.NewValue);
            }));

        void GlideTo(double to)
        {
            StopGlide();
            double from = scroller.VerticalOffset;
            if (!HomeMotion() || Math.Abs(to - from) < 1) { scroller.ScrollToVerticalOffset(to); return; }
            scroller.SetValue(PageGlideProperty, from);
            DoubleAnimation a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(Math.Max(240, Math.Min(560, 220 + Math.Abs(to - from) * 0.15))));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
            a.Completed += delegate
            {
                if (glide != a) return;
                glide = null;
                scroller.SetValue(PageGlideProperty, to);
                scroller.BeginAnimation(PageGlideProperty, null);
                ToolbarShade();
            };
            glide = a;
            scroller.BeginAnimation(PageGlideProperty, a);
        }

        // The wheel, a click or another glide takes over where the page is.
        void StopGlide()
        {
            if (glide == null) return;
            glide = null;
            scroller.SetValue(PageGlideProperty, scroller.VerticalOffset);
            scroller.BeginAnimation(PageGlideProperty, null);
        }

        // ---- After every page build

        void PageBuilt()
        {
            // The builders ran again, maybe seeing something new (a row that comes and goes): the index is made again
            // at the next search.
            searchIndexStale = true;
            HookSearchKeys();
            SectionStrip();
            if (!SearchShowing) return;
            // Went to another page: the page shows. Rebuilt in place (a theme, news about an update): the results stay.
            if (page != searchFrom) { SearchHide(); return; }
            SearchToolbar();
            // A new theme: drawn again now, and once more when the index has the icons' new colours.
            if (searchShownPalette != Ds.Brushes) { SearchIndexSoon(); SearchRun(); }
        }

        // Esc while the results show and the field has lost the focus: they close as from the field, instead of the
        // window's own Esc tucking the window away.
        bool SearchEscape()
        {
            if (!SearchShowing || searchBox == null) return false;
            searchBox.Text = "";
            return true;
        }
    }
}
