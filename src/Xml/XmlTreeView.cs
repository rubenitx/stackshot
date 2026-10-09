// Stackshot - XML tree: a virtualized list of colored rows (collapsible, filterable, selectable) and its picture export.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public enum Seg { Punct, Name, AttrName, AttrValue, Text, Comment, CData, Pi, Ns, Hint }

    // Colors of the syntax, taken once from the palette; Query is what the filter looks for.
    public sealed class XmlLook
    {
        public readonly Brush Ink, Punct, Name, AttrName, AttrValue, Comment, CData, Pi, Ns, Hint, Match;
        public readonly Color Accent;
        public readonly bool Dark;
        public string Query = "";

        public XmlLook(Palette pal)
        {
            Dark = pal.Dark;
            Accent = pal.Accent;
            Ink = Ds.Brush(pal.Label);
            Punct = Ds.Brush(pal.Label3);
            Name = Ds.Brush(Dark ? Ds.Rgb(120, 190, 255) : Ds.Rgb(0, 92, 190));
            AttrName = Ds.Brush(Dark ? Ds.Rgb(238, 166, 120) : Ds.Rgb(176, 78, 22));
            AttrValue = Ds.Brush(Dark ? Ds.Rgb(130, 214, 160) : Ds.Rgb(18, 124, 66));
            Comment = Ds.Brush(pal.Label3);
            CData = Ds.Brush(Dark ? Ds.Rgb(204, 176, 255) : Ds.Rgb(112, 70, 190));
            Pi = Ds.Brush(Dark ? Ds.Rgb(240, 140, 190) : Ds.Rgb(180, 40, 110));
            Ns = Ds.Brush(Dark ? Ds.Rgb(100, 210, 220) : Ds.Rgb(0, 120, 140));
            Hint = Ds.Brush(pal.Label2);
            Match = Ds.Brush(Dark ? Ds.Argb(0.40, 255, 214, 10) : Ds.Argb(0.55, 255, 214, 10));
        }

        public Brush For(Seg s)
        {
            switch (s)
            {
                case Seg.Punct: return Punct;
                case Seg.Name: return Name;
                case Seg.AttrName: return AttrName;
                case Seg.AttrValue: return AttrValue;
                case Seg.Comment: return Comment;
                case Seg.CData: return CData;
                case Seg.Pi: return Pi;
                case Seg.Ns: return Ns;
                case Seg.Hint: return Hint;
                default: return Ink;
            }
        }
    }

    public sealed class XRow
    {
        public XItem Node;
        public int Depth;
        public bool Close, Collapsed, Filtered;
        public XmlLook Look;

        public bool Collapsible { get { return !Close && !Filtered && Node.Kind == XKind.Element && !Node.Inline; } }
    }

    public struct Piece
    {
        public string Text;
        public Seg Kind;
        public Piece(string t, Seg k) { Text = t; Kind = k; }
    }

    // One line of the tree. Reused by the list while scrolling, so it fills itself whenever its row changes.
    public sealed class XmlRowView : Grid
    {
        public const double Step = 16, Gutter = 24;
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");
        static readonly Geometry Down = Tri(-4, -2.5, 4, -2.5, 0, 3), Right = Tri(-2.5, -4, 3, 0, -2.5, 4);
        readonly TextBlock text = new TextBlock();
        readonly System.Windows.Shapes.Path arrow = new System.Windows.Shapes.Path { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        XRow row;

        static Geometry Tri(double ax, double ay, double bx, double by, double cx, double cy)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(ax, ay), true, true);
                c.LineTo(new Point(bx, by), false, false);
                c.LineTo(new Point(cx, cy), false, false);
            }
            g.Freeze();
            return g;
        }

        public XmlRowView()
        {
            Background = Brushes.Transparent;
            text.FontFamily = Mono;
            text.FontSize = 12.8;
            text.TextWrapping = TextWrapping.Wrap;
            text.LineHeight = 20;
            text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            Children.Add(text);
            Children.Add(arrow);
            DataContextChanged += delegate { Fill(DataContext as XRow); };
        }

        public XmlRowView(XRow r) : this() { Fill(r); }

        public XRow Row { get { return row; } }

        void Fill(XRow r)
        {
            row = r;
            text.Inlines.Clear();
            arrow.Visibility = Visibility.Collapsed;
            if (r == null) return;
            text.Padding = new Thickness(r.Depth * Step + Gutter, 0, 14, 0);
            if (r.Collapsible)
            {
                arrow.Data = r.Collapsed ? Right : Down;
                arrow.Fill = r.Look.Hint;
                arrow.Margin = new Thickness(r.Depth * Step + 10, 10, 0, 0);
                arrow.Visibility = Visibility.Visible;
            }
            string q = r.Look.Query;
            foreach (Piece p in XmlRows.Pieces(r, false))
            {
                Brush b = r.Look.For(p.Kind);
                if (q.Length == 0 || p.Kind == Seg.Punct) { text.Inlines.Add(new System.Windows.Documents.Run(p.Text) { Foreground = b }); continue; }
                int at = 0;
                while (at < p.Text.Length)
                {
                    int i = p.Text.IndexOf(q, at, StringComparison.OrdinalIgnoreCase);
                    if (i < 0) { text.Inlines.Add(new System.Windows.Documents.Run(p.Text.Substring(at)) { Foreground = b }); break; }
                    if (i > at) text.Inlines.Add(new System.Windows.Documents.Run(p.Text.Substring(at, i - at)) { Foreground = b });
                    text.Inlines.Add(new System.Windows.Documents.Run(p.Text.Substring(i, q.Length)) { Foreground = b, Background = r.Look.Match });
                    at = i + q.Length;
                }
            }
        }
    }

    // What each row says, as colored pieces (full: nothing shortened, for copying).
    public static class XmlRows
    {
        const int Shown = 300;
        static readonly Regex WsRx = new Regex(@"\s+", RegexOptions.Compiled);

        static void Value(List<Piece> o, string v, Seg kind, bool full, bool attr, bool raw = false)
        {
            if (!full)
            {
                // Cut first: a huge value is never run through the regex whole.
                int total = v.Length;
                bool cut = total > Shown * 4;
                if (cut) v = v.Substring(0, Shown * 4);
                v = WsRx.Replace(v, " ").Trim();
                if (cut && v.Length <= Shown) { o.Add(new Piece(raw ? v : XmlDoc.Esc(v, attr), kind)); o.Add(new Piece("\u2026", Seg.Hint)); return; }
                if (v.Length > Shown) { string rest = (cut ? total - Shown : v.Length - Shown).ToString("N0"); o.Add(new Piece(raw ? v.Substring(0, Shown) : XmlDoc.Esc(v.Substring(0, Shown), attr), kind)); o.Add(new Piece("\u2026 (+" + rest + ")", Seg.Hint)); return; }
            }
            else v = v.Trim();
            o.Add(new Piece(raw ? v : XmlDoc.Esc(v, attr), kind));
        }

        static void Name(List<Piece> o, XItem n)
        {
            if (n.Prefix.Length > 0 && n.Name.StartsWith(n.Prefix + ":", StringComparison.Ordinal))
            {
                o.Add(new Piece(n.Prefix + ":", Seg.Ns));
                o.Add(new Piece(n.Local, Seg.Name));
            }
            else o.Add(new Piece(n.Name, Seg.Name));
        }

        public static List<Piece> Pieces(XRow r, bool full)
        {
            List<Piece> o = new List<Piece>();
            XItem n = r.Node;
            switch (n.Kind)
            {
                case XKind.Decl:
                    o.Add(new Piece("<?xml ", Seg.Pi));
                    o.Add(new Piece(n.Value, Seg.Pi));
                    o.Add(new Piece("?>", Seg.Pi));
                    break;
                case XKind.Pi:
                    o.Add(new Piece("<?", Seg.Punct));
                    o.Add(new Piece(n.Name, Seg.Pi));
                    if (n.Value.Length > 0) { o.Add(new Piece(" ", Seg.Punct)); Value(o, n.Value, Seg.Pi, full, false, true); }
                    o.Add(new Piece("?>", Seg.Punct));
                    break;
                case XKind.Comment:
                    o.Add(new Piece("<!-- ", Seg.Punct));
                    Value(o, n.Value, Seg.Comment, full, false, true);
                    o.Add(new Piece(" -->", Seg.Punct));
                    break;
                case XKind.CData:
                    o.Add(new Piece("<![CDATA[", Seg.Punct));
                    Value(o, n.Value, Seg.CData, full, false, true);
                    o.Add(new Piece("]]>", Seg.Punct));
                    break;
                case XKind.Text:
                    Value(o, n.Value, Seg.Text, full, false);
                    break;
                default:
                    if (r.Close)
                    {
                        o.Add(new Piece("</", Seg.Punct));
                        Name(o, n);
                        o.Add(new Piece(">", Seg.Punct));
                        break;
                    }
                    o.Add(new Piece("<", Seg.Punct));
                    Name(o, n);
                    if (n.Attrs != null)
                        foreach (KeyValuePair<string, string> a in n.Attrs)
                        {
                            o.Add(new Piece(" ", Seg.Punct));
                            bool ns = a.Key == "xmlns" || a.Key.StartsWith("xmlns:", StringComparison.Ordinal);
                            o.Add(new Piece(a.Key, ns ? Seg.Ns : Seg.AttrName));
                            o.Add(new Piece("=\"", Seg.Punct));
                            Value(o, a.Value, Seg.AttrValue, full, true);
                            o.Add(new Piece("\"", Seg.Punct));
                        }
                    if (n.Kids.Count == 0)
                    {
                        if (n.Empty) o.Add(new Piece(" />", Seg.Punct));
                        else { o.Add(new Piece("></", Seg.Punct)); Name(o, n); o.Add(new Piece(">", Seg.Punct)); }
                    }
                    else if (n.Inline)
                    {
                        o.Add(new Piece(">", Seg.Punct));
                        Value(o, n.Kids[0].Value, Seg.Text, full, false);
                        o.Add(new Piece("</", Seg.Punct)); Name(o, n); o.Add(new Piece(">", Seg.Punct));
                    }
                    else if (r.Collapsed)
                    {
                        o.Add(new Piece(">", Seg.Punct));
                        o.Add(new Piece(" \u2026 ", Seg.Hint));
                        o.Add(new Piece("</", Seg.Punct)); Name(o, n); o.Add(new Piece(">", Seg.Punct));
                        o.Add(new Piece("  " + n.Kids.Count + (n.Kids.Count == 1 ? " nodo" : " nodos"), Seg.Hint));
                    }
                    else o.Add(new Piece(">", Seg.Punct));
                    break;
            }
            return o;
        }

        public static string Line(XRow r)
        {
            StringBuilder b = new StringBuilder(new string(' ', r.Depth * 2));
            foreach (Piece p in Pieces(r, true)) b.Append(p.Text);
            return b.ToString();
        }

        sealed class Frame { public List<XItem> List; public int I, Depth; public XItem Closer; }

        // The rows for these nodes: expanded as they are marked, or all of them (force), or only what the filter keeps.
        public static List<XRow> Walk(List<XItem> start, int depth, bool force, HashSet<XItem> keep, XmlLook look, int max)
        {
            List<XRow> rows = new List<XRow>();
            Stack<Frame> st = new Stack<Frame>();
            st.Push(new Frame { List = start, Depth = depth });
            while (st.Count > 0 && rows.Count < max)
            {
                Frame f = st.Peek();
                if (f.I >= f.List.Count)
                {
                    st.Pop();
                    if (f.Closer != null) rows.Add(new XRow { Node = f.Closer, Depth = f.Depth - 1, Close = true, Look = look, Filtered = keep != null });
                    continue;
                }
                XItem n = f.List[f.I++];
                if (keep != null && !keep.Contains(n)) continue;
                XRow r = new XRow { Node = n, Depth = f.Depth, Look = look, Filtered = keep != null };
                rows.Add(r);
                if (n.Kind != XKind.Element || n.Inline) continue;
                bool show = force;
                if (!show && keep != null) { foreach (XItem k in n.Kids) if (keep.Contains(k)) { show = true; break; } }
                else if (!show) show = n.Expanded;
                if (show) st.Push(new Frame { List = n.Kids, Depth = f.Depth + 1, Closer = n });
                else r.Collapsed = true;
            }
            return rows;
        }
    }

    // A small search box for the toolbar.
    public sealed class SearchField : Border
    {
        public readonly TextBox Box;
        readonly TextBlock hint;
        public event Action<string> Changed;

        public SearchField(Palette pal, string placeholder)
        {
            Height = 30;
            Width = 190;
            CornerRadius = new CornerRadius(8);
            Margin = new Thickness(8, 0, 0, 0);
            Background = Ds.Brush(pal.Dark ? Ds.Argb(0.10, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0));
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            GlyphView icon = new GlyphView("search", 15, pal.Label2, 1.6) { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(icon);
            Box = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label), FontSize = 13, Padding = new Thickness(6, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, SpellCheck = { IsEnabled = false } };
            Grid.SetColumn(Box, 1);
            hint = new TextBlock { Text = placeholder, FontSize = 13, Foreground = Ds.Brush(pal.Label3), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };
            Grid.SetColumn(hint, 1);
            g.Children.Add(Box);
            g.Children.Add(hint);
            Child = g;
            Box.TextChanged += delegate
            {
                hint.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (Changed != null) Changed(Box.Text);
            };
        }

        public string Text
        {
            get { return Box.Text; }
            set { Box.Text = value ?? ""; }
        }
    }

    public sealed class XmlTreeView : Grid
    {
        readonly ListBox list = new ListBox();
        readonly XmlLook look;
        XmlModel model;
        HashSet<XItem> keep;
        List<XRow> rows = new List<XRow>();
        int matches;

        public event Action<string> PathChanged;
        public event Action<string, string> CopyRequest;
        public event Action<MouseButtonEventArgs, List<MenuEntry>> Menu;

        public XmlTreeView(Palette pal)
        {
            look = new XmlLook(pal);
            string acc = Color.FromArgb(pal.Dark ? (byte)0x55 : (byte)0x40, look.Accent.R, look.Accent.G, look.Accent.B).ToString();
            string xaml =
                "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Style TargetType='ListBoxItem'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'>" +
                "<Border x:Name='b' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#14808080'/></Trigger>" +
                "<Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='" + acc + "'/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></ResourceDictionary>";
            ResourceDictionary rd = new ResourceDictionary();
            rd.MergedDictionaries.Add(HomeWindow.ScrollStyle());
            rd.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml));
            list.Resources = rd;
            list.BorderThickness = new Thickness(0);
            list.Background = Brushes.Transparent;
            list.SelectionMode = SelectionMode.Extended;
            list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
            VirtualizingStackPanel.SetIsVirtualizing(list, true);
            VirtualizingStackPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
            list.FocusVisualStyle = null;
            list.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(XmlRowView)) };
            list.Padding = new Thickness(0, 8, 0, 36);
            Children.Add(list);

            list.SelectionChanged += delegate
            {
                if (PathChanged == null) return;
                XRow r = list.SelectedItem as XRow;
                PathChanged(r == null || list.SelectedItems.Count != 1 ? null : XmlDoc.XPath(r.Node));
            };
            list.PreviewMouseLeftButtonDown += OnDown;
            list.PreviewMouseRightButtonDown += OnRightDown;
            list.PreviewMouseRightButtonUp += OnRightUp;
            list.PreviewKeyDown += OnKey;
        }

        public ListBox List { get { return list; } }
        public int Matches { get { return matches; } }
        public bool Filtering { get { return keep != null; } }
        public List<XRow> Rows { get { return rows; } }
        public XmlLook Look { get { return look; } }
        public XmlModel Model { get { return model; } }

        public void Load(XmlModel m)
        {
            model = m;
            keep = null;
            look.Query = "";
            matches = 0;
            // Small documents open fully; large ones, two levels deep.
            Stack<XItem> st = new Stack<XItem>();
            Stack<int> depth = new Stack<int>();
            foreach (XItem t in m.Top) { st.Push(t); depth.Push(0); }
            bool all = m.Count <= 1500;
            while (st.Count > 0)
            {
                XItem n = st.Pop();
                int d = depth.Pop();
                if (n.Kind != XKind.Element) continue;
                n.Expanded = all || d < 2;
                foreach (XItem k in n.Kids) { st.Push(k); depth.Push(d + 1); }
            }
            Reflow(false, null);
        }

        void Reflow(bool keepScroll, XRow select)
        {
            ScrollViewer sv = Scroller();
            double off = keepScroll && sv != null ? sv.VerticalOffset : 0;
            rows = XmlRows.Walk(model.Top, 0, false, keep, look, int.MaxValue);
            list.ItemsSource = rows;
            if (select != null)
            {
                int i = rows.FindIndex(delegate(XRow r) { return r.Node == select.Node && r.Close == select.Close; });
                if (i >= 0) list.SelectedIndex = i;
            }
            if (keepScroll && sv != null) { list.UpdateLayout(); sv.ScrollToVerticalOffset(off); }
        }

        ScrollViewer Scroller()
        {
            if (!list.IsLoaded) return null;
            Decorator d = VisualTreeHelper.GetChildrenCount(list) > 0 ? VisualTreeHelper.GetChild(list, 0) as Decorator : null;
            return d != null ? d.Child as ScrollViewer : null;
        }

        // ---- Expand, collapse, filter

        void SetAll(bool open)
        {
            if (model == null) return;
            Stack<XItem> st = new Stack<XItem>();
            Stack<int> depth = new Stack<int>();
            foreach (XItem t in model.Top) { st.Push(t); depth.Push(0); }
            while (st.Count > 0)
            {
                XItem n = st.Pop();
                int d = depth.Pop();
                if (n.Kind != XKind.Element) continue;
                n.Expanded = open || d == 0;
                foreach (XItem k in n.Kids) { st.Push(k); depth.Push(d + 1); }
            }
        }

        public void ExpandAll() { SetAll(true); Reflow(false, null); }
        public void CollapseAll() { SetAll(false); Reflow(false, null); }

        static bool Hit(XItem n, string q)
        {
            if (n.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 && n.Kind != XKind.Text) return true;
            if (n.Kind != XKind.Element) return n.Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (n.Attrs != null)
                foreach (KeyValuePair<string, string> a in n.Attrs)
                    if (a.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || a.Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return n.Inline && n.Kids.Count == 1 && n.Kids[0].Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Shows only the nodes that contain the text, with the elements that lead to them. Returns how many matched.
        public int Filter(string q)
        {
            if (model == null) return 0;
            q = (q ?? "").Trim();
            look.Query = q;
            if (q.Length == 0) { keep = null; matches = 0; Reflow(false, null); return 0; }
            HashSet<XItem> k = new HashSet<XItem>();
            Stack<XItem> st = new Stack<XItem>();
            foreach (XItem t in model.Top) st.Push(t);
            int n = 0;
            while (st.Count > 0)
            {
                XItem x = st.Pop();
                // The text inside an inline element is shown on its own row: it is not a row itself.
                bool hidden = x.Parent != null && x.Parent.Inline;
                if (!hidden && Hit(x, q))
                {
                    n++;
                    for (XItem a = x; a != null && k.Add(a); a = a.Parent) { }
                }
                if (!x.Inline) foreach (XItem c in x.Kids) st.Push(c);
            }
            keep = k;
            matches = n;
            Reflow(false, null);
            return n;
        }

        // ---- Toggling

        void Toggle(int i)
        {
            if (i < 0 || i >= rows.Count) return;
            XRow r = rows[i];
            if (!r.Collapsible) return;
            r.Node.Expanded = !r.Node.Expanded;
            Reflow(true, new XRow { Node = r.Node, Close = false });
        }

        XmlRowView RowAt(MouseEventArgs e, out Point inRow)
        {
            inRow = new Point();
            HitTestResult h = VisualTreeHelper.HitTest(list, e.GetPosition(list));
            DependencyObject d = h == null ? null : h.VisualHit;
            while (d != null && !(d is XmlRowView)) d = d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : null;
            XmlRowView v = d as XmlRowView;
            if (v != null) inRow = e.GetPosition(v);
            return v;
        }

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            Point p;
            XmlRowView v = RowAt(e, out p);
            if (v == null || v.Row == null || !v.Row.Collapsible) return;
            bool onArrow = p.X < v.Row.Depth * XmlRowView.Step + XmlRowView.Gutter - 2;
            if (!onArrow && e.ClickCount != 2) return;
            e.Handled = true;
            Toggle(rows.IndexOf(v.Row));
        }

        void OnRightDown(object sender, MouseButtonEventArgs e)
        {
            Point p;
            XmlRowView v = RowAt(e, out p);
            if (v != null && v.Row != null && !list.SelectedItems.Contains(v.Row)) { list.SelectedItems.Clear(); list.SelectedItem = v.Row; }
            e.Handled = true;
        }

        void OnRightUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Menu == null) return;
            XRow r = list.SelectedItem as XRow;
            List<MenuEntry> m = new List<MenuEntry>();
            bool any = list.SelectedItems.Count > 0;
            m.Add(MenuEntry.Item("Copiar", "copy", false, delegate { CopySelection(); }));
            m[0].Enabled = any;
            MenuEntry path = MenuEntry.Item("Copiar ruta (XPath)", "copy", false, delegate { CopyPath(); });
            path.Enabled = r != null;
            m.Add(path);
            if (r != null && !r.Close && r.Node.Kind != XKind.Decl)
            {
                string v = r.Node.Kind == XKind.Element ? (r.Node.Inline && r.Node.Kids.Count == 1 ? r.Node.Kids[0].Value : null) : r.Node.Value;
                if (v != null) m.Add(MenuEntry.Item("Copiar valor", "copy", false, delegate { Ask(v, "Valor copiado."); }));
            }
            if (r != null && r.Collapsible && !Filtering)
            {
                m.Add(MenuEntry.Line());
                XRow sel = r;
                m.Add(MenuEntry.Item(r.Collapsed ? "Expandir" : "Contraer", r.Collapsed ? "plus" : "minus", false, delegate { Toggle(rows.IndexOf(sel)); }));
            }
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Seleccionar todo", "check", false, delegate { list.Focus(); list.SelectAll(); }));
            Menu(e, m);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (ctrl && e.Key == Key.C) { e.Handled = true; if (shift) CopyPath(); else CopySelection(); return; }
            XRow r = list.SelectedItem as XRow;
            if (r == null || ctrl || !r.Collapsible) return;
            if (e.Key == Key.Right && r.Collapsed || e.Key == Key.Left && !r.Collapsed) { e.Handled = true; Toggle(rows.IndexOf(r)); }
        }

        // ---- Copying

        void Ask(string text, string what) { if (CopyRequest != null) CopyRequest(text, what); }

        public void CopyPath()
        {
            XRow r = list.SelectedItem as XRow;
            if (r != null) Ask(XmlDoc.XPath(r.Node), "Ruta copiada.");
        }

        public string SelectionText()
        {
            List<XRow> sel = new List<XRow>();
            foreach (object o in list.SelectedItems) sel.Add((XRow)o);
            sel.Sort(delegate(XRow a, XRow b) { return rows.IndexOf(a).CompareTo(rows.IndexOf(b)); });
            StringBuilder s = new StringBuilder();
            foreach (XRow r in sel)
            {
                // A collapsed element copies everything inside it.
                if (r.Collapsed && !r.Close)
                    foreach (XRow x in XmlRows.Walk(new List<XItem> { r.Node }, r.Depth, true, null, look, int.MaxValue)) s.Append(XmlRows.Line(x)).Append("\r\n");
                else s.Append(XmlRows.Line(r)).Append("\r\n");
            }
            return s.ToString().TrimEnd('\r', '\n');
        }

        public void CopySelection()
        {
            string t = SelectionText();
            if (t.Length > 0) Ask(t, list.SelectedItems.Count == 1 ? "Copiado." : "Copiadas " + list.SelectedItems.Count + " l\u00EDneas.");
        }

        // ---- Picture

        // The rows as shown (at most `max`) on the window color, 2x when it fits.
        public static BitmapSource Render(List<XRow> rows, XmlLook look, out string error)
        {
            error = null;
            if (rows.Count == 0) { error = "No hay nada que mostrar."; return null; }
            const double W = 1000;
            int n = Math.Min(rows.Count, 700);
            Border page = null;
            while (true)
            {
                StackPanel sp = new StackPanel { Width = W };
                for (int i = 0; i < n; i++) sp.Children.Add(new XmlRowView(rows[i]));
                if (n < rows.Count)
                    sp.Children.Add(new TextBlock { Text = "\u2026 y " + (rows.Count - n).ToString("N0") + " l\u00EDneas m\u00E1s (contrae lo que no necesites para verlas)", Foreground = look.Hint, FontSize = 12.5, Margin = new Thickness(XmlRowView.Gutter, 8, 0, 0) });
                page = new Border { Background = Ds.Brush(look.Dark ? Ds.Rgb(30, 30, 32) : Colors.White), Padding = new Thickness(28, 26, 28, 28), Width = W + 56, UseLayoutRounding = true, Child = sp };
                TextOptions.SetTextFormattingMode(page, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(page, TextRenderingMode.Grayscale);
                page.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, Ds.Text);
                page.Measure(new Size(page.Width, double.PositiveInfinity));
                if (page.DesiredSize.Height <= 16000 || n <= 20) break;
                n = (int)(n * 0.8);
            }
            page.Arrange(new Rect(0, 0, page.DesiredSize.Width, page.DesiredSize.Height));
            page.UpdateLayout();
            int w = (int)Math.Ceiling(page.ActualWidth), h = (int)Math.Ceiling(page.ActualHeight);
            if (h > 16000) { error = "El XML es demasiado largo para una sola imagen."; return null; }
            double scale = h * 2 <= 16000 ? 2 : 1;
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(page);
            rtb.Freeze();
            return rtb;
        }
    }
}
