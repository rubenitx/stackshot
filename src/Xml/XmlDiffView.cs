// Stackshot - The compare view: the merged tree of two XML documents (XmlDiff) as a virtualized list like the XML tree,
// each line marked added (green), removed (red, struck through), changed (amber, old -> new) or unchanged (dimmed). By
// default only the differences show, with the elements that lead to them; F7 steps through them. Read-only.
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
    // The syntax colours of the tree plus the marks of the comparison, taken once from the palette.
    public sealed class XmlDiffLook
    {
        public readonly XmlLook Syntax;
        public readonly Brush AddBack, DelBack, ChgBack, AddInk, DelInk, ChgInk;
        public readonly bool Dark;

        public XmlDiffLook(Palette pal)
        {
            Syntax = new XmlLook(pal);
            Dark = pal.Dark;
            AddBack = Ds.Brush(Ds.WithAlpha(pal.Green, Dark ? 0.17 : 0.15));
            DelBack = Ds.Brush(Ds.WithAlpha(pal.Red, Dark ? 0.17 : 0.11));
            ChgBack = Ds.Brush(Ds.WithAlpha(pal.Orange, Dark ? 0.17 : 0.14));
            AddInk = Ds.Brush(Dark ? Ds.Rgb(110, 222, 142) : pal.Pictures ? Ds.Rgb(18, 122, 52) : Ds.Rgb(16, 112, 48));
            DelInk = Ds.Brush(Dark ? Ds.Rgb(255, 128, 118) : Ds.Rgb(196, 36, 28));
            ChgInk = Ds.Brush(Dark ? Ds.Rgb(255, 192, 84) : pal.Pictures ? Ds.Rgb(170, 98, 0) : Ds.Rgb(150, 82, 0));   // 4.5:1 on the row tints; pictures keep the old ones
            if (!pal.High) return;
            // Contrast theme: the signs (+, -, ~) and the strike-through tell the changes apart; no tinted rows.
            AddBack = DelBack = ChgBack = Brushes.Transparent;
            AddInk = Ds.Brush(pal.Link);
            DelInk = Ds.Brush(pal.Label3);
            ChgInk = Ds.Brush(pal.Accent);
        }
    }

    public sealed class DiffRow
    {
        public DiffNode Node;
        public int Depth, Gap;          // Gap > 0: one line standing for that many unchanged siblings left out
        public bool Close, Collapsed;
        public XmlDiffLook Look;

        public bool Collapsible { get { return Gap == 0 && !Close && DiffRows.HasRows(Node); } }
        public bool Dim { get { return Gap == 0 && Node.Kind == DiffKind.Same && !Node.Inside; } }
        // The mark the line carries: a closing tag shows it only for an added or removed element.
        public DiffKind Mark { get { return Gap > 0 ? DiffKind.Same : Close && Node.Kind == DiffKind.Changed ? DiffKind.Same : Node.Kind; } }
    }

    public struct DiffPiece
    {
        public string Text;
        public Seg Kind;
        public int Mark;        // 0 as is, 1 the newer value, 2 the older value (struck through), 3 the arrow between them
        public DiffPiece(string t, Seg k, int mark) { Text = t; Kind = k; Mark = mark; }
    }

    // What each line says, as pieces, and the lines of a merged tree.
    public static class DiffRows
    {
        const int Shown = 300;
        static readonly Regex WsRx = new Regex(@"\s+", RegexOptions.Compiled);

        static bool IsText(XItem x) { return x.Kind == XKind.Text || x.Kind == XKind.CData; }

        // An element whose content fits on its own line (nothing, one piece of text, or a text change).
        public static bool Inline(DiffNode d)
        {
            // An unchanged branch not opened yet answers from its document node, without making its children.
            if (d.Lazy) { XItem x = d.Item; return x.Kind != XKind.Element || x.Kids.Count == 0 || (x.Kids.Count == 1 && IsText(x.Kids[0])); }
            if (d.Item.Kind != XKind.Element || d.TextChanged || d.Kids.Count == 0) return true;
            return d.Kids.Count == 1 && IsText(d.Kids[0].Item) && (d.Kids[0].Kind == DiffKind.Same || d.Kids[0].Kind == d.Kind);
        }

        public static bool HasRows(DiffNode d) { return d.Item.Kind == XKind.Element && !Inline(d); }

        static void Value(List<DiffPiece> o, string v, Seg kind, int mark, bool full, bool attr, bool raw)
        {
            v = v ?? "";
            if (!full)
            {
                int total = v.Length;
                bool cut = total > Shown * 4;
                if (cut) v = v.Substring(0, Shown * 4);
                v = WsRx.Replace(v, " ").Trim();
                if (cut && v.Length <= Shown) { o.Add(new DiffPiece(raw ? v : XmlDoc.Esc(v, attr), kind, mark)); o.Add(new DiffPiece("\u2026", Seg.Hint, 0)); return; }
                if (v.Length > Shown)
                {
                    string rest = (cut ? total - Shown : v.Length - Shown).ToString("N0");
                    o.Add(new DiffPiece(raw ? v.Substring(0, Shown) : XmlDoc.Esc(v.Substring(0, Shown), attr), kind, mark));
                    o.Add(new DiffPiece("\u2026 (+" + rest + ")", Seg.Hint, 0));
                    return;
                }
            }
            else v = v.Trim();
            o.Add(new DiffPiece(raw ? v : XmlDoc.Esc(v, attr), kind, mark));
        }

        // Old value struck through, an arrow, the new one (either side may be missing).
        static void Change(List<DiffPiece> o, string old, string now, Seg kind, bool full, bool attr, bool raw)
        {
            if (old != null) Value(o, old, kind, 2, full, attr, raw);
            if (old != null && now != null) o.Add(new DiffPiece(" \u2192 ", Seg.Hint, 3));
            if (now != null) Value(o, now, kind, 1, full, attr, raw);
        }

        static void Name(List<DiffPiece> o, XItem n)
        {
            if (n.Prefix.Length > 0 && n.Name.StartsWith(n.Prefix + ":", StringComparison.Ordinal))
            {
                o.Add(new DiffPiece(n.Prefix + ":", Seg.Ns, 0));
                o.Add(new DiffPiece(n.Local, Seg.Name, 0));
            }
            else o.Add(new DiffPiece(n.Name, Seg.Name, 0));
        }

        static void Attrs(List<DiffPiece> o, DiffNode d, bool full)
        {
            if (d.Attrs != null)
            {
                foreach (AttrDiff a in d.Attrs)
                {
                    int m = a.Kind == DiffKind.Added ? 1 : a.Kind == DiffKind.Removed ? 2 : 0;
                    o.Add(new DiffPiece(" ", Seg.Punct, 0));
                    bool ns = a.Ns || a.Name == "xmlns" || a.Name.StartsWith("xmlns:", StringComparison.Ordinal);
                    o.Add(new DiffPiece(a.Name, ns ? Seg.Ns : Seg.AttrName, m));
                    o.Add(new DiffPiece("=\"", Seg.Punct, m));
                    if (a.Kind == DiffKind.Changed) Change(o, a.Old, a.New, Seg.AttrValue, full, true, false);
                    else Value(o, a.Kind == DiffKind.Removed ? a.Old : a.New, Seg.AttrValue, m, full, true, false);
                    o.Add(new DiffPiece("\"", Seg.Punct, m));
                }
                return;
            }
            XItem x = d.Item;
            if (x.Attrs == null) return;
            foreach (KeyValuePair<string, string> a in x.Attrs)
            {
                o.Add(new DiffPiece(" ", Seg.Punct, 0));
                bool ns = a.Key == "xmlns" || a.Key.StartsWith("xmlns:", StringComparison.Ordinal);
                o.Add(new DiffPiece(a.Key, ns ? Seg.Ns : Seg.AttrName, 0));
                o.Add(new DiffPiece("=\"", Seg.Punct, 0));
                Value(o, a.Value, Seg.AttrValue, 0, full, true, false);
                o.Add(new DiffPiece("\"", Seg.Punct, 0));
            }
        }

        static string Hidden(int n) { return "\u22EF " + n.ToString("N0") + (n == 1 ? I18n.T(" nodo sin cambios", " unchanged node") : I18n.T(" nodos sin cambios", " unchanged nodes")); }

        public static List<DiffPiece> Pieces(DiffRow r, bool full)
        {
            List<DiffPiece> o = new List<DiffPiece>();
            if (r.Gap > 0) { o.Add(new DiffPiece(Hidden(r.Gap), Seg.Hint, 0)); return o; }
            DiffNode d = r.Node;
            XItem n = d.Item;
            string oldV = d.TextChanged ? d.TextOld : null, newV = d.TextChanged ? d.TextNew : n.Value;
            switch (n.Kind)
            {
                case XKind.Pi:
                    o.Add(new DiffPiece("<?", Seg.Punct, 0));
                    o.Add(new DiffPiece(n.Name, Seg.Pi, 0));
                    o.Add(new DiffPiece(" ", Seg.Punct, 0));
                    if (d.TextChanged) Change(o, oldV, newV, Seg.Pi, full, false, true); else Value(o, n.Value, Seg.Pi, 0, full, false, true);
                    o.Add(new DiffPiece("?>", Seg.Punct, 0));
                    break;
                case XKind.Comment:
                    o.Add(new DiffPiece("<!-- ", Seg.Punct, 0));
                    if (d.TextChanged) Change(o, oldV, newV, Seg.Comment, full, false, true); else Value(o, n.Value, Seg.Comment, 0, full, false, true);
                    o.Add(new DiffPiece(" -->", Seg.Punct, 0));
                    break;
                case XKind.CData:
                    o.Add(new DiffPiece("<![CDATA[", Seg.Punct, 0));
                    if (d.TextChanged) Change(o, oldV, newV, Seg.CData, full, false, true); else Value(o, n.Value, Seg.CData, 0, full, false, true);
                    o.Add(new DiffPiece("]]>", Seg.Punct, 0));
                    break;
                case XKind.Text:
                    if (d.TextChanged) Change(o, oldV, newV, Seg.Text, full, false, false); else Value(o, n.Value, Seg.Text, 0, full, false, false);
                    break;
                case XKind.Element:
                    if (r.Close)
                    {
                        o.Add(new DiffPiece("</", Seg.Punct, 0));
                        Name(o, n);
                        o.Add(new DiffPiece(">", Seg.Punct, 0));
                        break;
                    }
                    o.Add(new DiffPiece("<", Seg.Punct, 0));
                    Name(o, n);
                    Attrs(o, d, full);
                    if (!HasRows(d))
                    {
                        if (d.TextChanged || d.Kids.Count == 1)
                        {
                            o.Add(new DiffPiece(">", Seg.Punct, 0));
                            if (d.TextChanged) Change(o, d.TextOld, d.TextNew, Seg.Text, full, false, false);
                            else
                            {
                                XItem t = d.Kids[0].Item;
                                Value(o, t.Value, t.Kind == XKind.CData ? Seg.CData : Seg.Text, 0, full, false, t.Kind == XKind.CData);
                            }
                            o.Add(new DiffPiece("</", Seg.Punct, 0)); Name(o, n); o.Add(new DiffPiece(">", Seg.Punct, 0));
                        }
                        else if (n.Empty) o.Add(new DiffPiece(" />", Seg.Punct, 0));
                        else { o.Add(new DiffPiece("></", Seg.Punct, 0)); Name(o, n); o.Add(new DiffPiece(">", Seg.Punct, 0)); }
                    }
                    else if (r.Collapsed)
                    {
                        o.Add(new DiffPiece(">", Seg.Punct, 0));
                        o.Add(new DiffPiece(" \u2026 ", Seg.Hint, 0));
                        o.Add(new DiffPiece("</", Seg.Punct, 0)); Name(o, n); o.Add(new DiffPiece(">", Seg.Punct, 0));
                        o.Add(new DiffPiece("  " + d.Kids.Count.ToString("N0") + (d.Kids.Count == 1 ? I18n.T(" nodo", " node") : I18n.T(" nodos", " nodes")) + (n.Inner ? I18n.T(" \u00B7 XML interno", " \u00B7 inner XML") : ""), Seg.Hint, 0));
                    }
                    else
                    {
                        o.Add(new DiffPiece(">", Seg.Punct, 0));
                        if (n.Inner) o.Add(new DiffPiece(I18n.T("   XML interno", "   inner XML"), Seg.Hint, 0));
                    }
                    break;
                default:
                    Value(o, n.Value, Seg.Pi, 0, full, false, true);
                    break;
            }
            return o;
        }

        public static string Sign(DiffKind k) { return k == DiffKind.Added ? "+" : k == DiffKind.Removed ? "\u2212" : k == DiffKind.Changed ? "~" : ""; }

        // One line as plain text, with its mark first (+, -, ~), for copying.
        public static string Line(DiffRow r)
        {
            DiffKind k = r.Mark;
            StringBuilder b = new StringBuilder(k == DiffKind.Added ? "+ " : k == DiffKind.Removed ? "- " : k == DiffKind.Changed ? "~ " : "  ");
            b.Append(' ', r.Depth * 2);
            foreach (DiffPiece p in Pieces(r, true)) b.Append(p.Text);
            return b.ToString();
        }

        static bool Visible(DiffNode d) { return d.Differs || d.Inside; }

        sealed class Frame { public List<DiffNode> List; public int I, Depth; public DiffNode Closer; }

        // The lines for the merged tree as its nodes are expanded; onlyDiffs leaves out what has no difference in it, one
        // hint line for each run of unchanged siblings.
        public static List<DiffRow> Walk(List<DiffNode> top, bool onlyDiffs, XmlDiffLook look)
        {
            List<DiffRow> rows = new List<DiffRow>();
            Stack<Frame> st = new Stack<Frame>();
            st.Push(new Frame { List = top });
            while (st.Count > 0)
            {
                Frame f = st.Peek();
                if (onlyDiffs)
                {
                    int hidden = 0;
                    while (f.I < f.List.Count && !Visible(f.List[f.I])) { hidden++; f.I++; }
                    if (hidden > 0) rows.Add(new DiffRow { Node = f.List[f.I - 1], Depth = f.Depth, Gap = hidden, Look = look });
                }
                if (f.I >= f.List.Count)
                {
                    st.Pop();
                    if (f.Closer != null) rows.Add(new DiffRow { Node = f.Closer, Depth = f.Depth - 1, Close = true, Look = look });
                    continue;
                }
                DiffNode n = f.List[f.I++];
                DiffRow r = new DiffRow { Node = n, Depth = f.Depth, Look = look };
                rows.Add(r);
                if (!HasRows(n)) continue;
                if (n.Expanded) st.Push(new Frame { List = n.Kids, Depth = f.Depth + 1, Closer = n });
                else r.Collapsed = true;
            }
            return rows;
        }
    }

    // One line of the compare view. Reused by the list while scrolling, so it fills itself whenever its row changes.
    public sealed class DiffRowView : Grid
    {
        const double Lead = 16;
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");
        static readonly Geometry Down = Tri(-4, -2.5, 4, -2.5, 0, 3), Right = Tri(-2.5, -4, 3, 0, -2.5, 4);
        readonly TextBlock text = new TextBlock(), sign = new TextBlock();
        readonly System.Windows.Shapes.Path arrow = new System.Windows.Shapes.Path { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        DiffRow row;

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

        public DiffRowView()
        {
            Background = Brushes.Transparent;
            text.FontFamily = Mono;
            text.FontSize = 12.8;
            text.TextWrapping = TextWrapping.Wrap;
            text.LineHeight = 20;
            text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            sign.FontFamily = Mono;
            sign.FontSize = 12.8;
            sign.FontWeight = FontWeights.Bold;
            sign.LineHeight = 20;
            sign.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            sign.Margin = new Thickness(5, 0, 0, 0);
            sign.HorizontalAlignment = HorizontalAlignment.Left;
            sign.IsHitTestVisible = false;
            Children.Add(text);
            Children.Add(sign);
            Children.Add(arrow);
            DataContextChanged += delegate { Fill(DataContext as DiffRow); };
        }

        public DiffRowView(DiffRow r) : this() { Fill(r); }

        public DiffRow Row { get { return row; } }

        public static double ArrowEnd(DiffRow r) { return Lead + r.Depth * XmlRowView.Step + XmlRowView.Gutter - 2; }

        void Fill(DiffRow r)
        {
            row = r;
            text.Inlines.Clear();
            arrow.Visibility = Visibility.Collapsed;
            sign.Text = "";
            Background = Brushes.Transparent;
            text.Opacity = 1;
            text.TextDecorations = null;
            text.FontStyle = FontStyles.Normal;
            if (r == null) return;
            XmlDiffLook k = r.Look;
            text.Padding = new Thickness(Lead + r.Depth * XmlRowView.Step + XmlRowView.Gutter, 0, 14, 0);
            DiffKind mark = r.Mark;
            if (mark == DiffKind.Added) { Background = k.AddBack; sign.Foreground = k.AddInk; }
            else if (mark == DiffKind.Removed) { Background = k.DelBack; sign.Foreground = k.DelInk; text.TextDecorations = TextDecorations.Strikethrough; }
            else if (mark == DiffKind.Changed) { Background = k.ChgBack; sign.Foreground = k.ChgInk; }
            sign.Text = DiffRows.Sign(mark);
            if (r.Dim) text.Opacity = 0.5;
            if (r.Gap > 0) text.FontStyle = FontStyles.Italic;
            if (r.Collapsible)
            {
                arrow.Data = r.Collapsed ? Right : Down;
                arrow.Fill = k.Syntax.Hint;
                arrow.Margin = new Thickness(Lead + r.Depth * XmlRowView.Step + 10, 10, 0, 0);
                arrow.Visibility = Visibility.Visible;
            }
            foreach (DiffPiece p in DiffRows.Pieces(r, false))
            {
                System.Windows.Documents.Run run = new System.Windows.Documents.Run(p.Text);
                if (p.Mark == 1) run.Foreground = k.AddInk;
                else if (p.Mark == 2) { run.Foreground = k.DelInk; run.TextDecorations = TextDecorations.Strikethrough; }
                else if (p.Mark == 3) run.Foreground = k.Syntax.Hint;
                else run.Foreground = k.Syntax.For(p.Kind);
                if (p.Mark == 1 && mark == DiffKind.Changed) run.FontWeight = FontWeights.SemiBold;
                text.Inlines.Add(run);
            }
        }
    }

    public sealed partial class XmlDiffView : Grid
    {
        readonly ListBox list = new XmlLineList();
        readonly XmlDiffLook look;
        readonly DiffResult result;
        readonly FrameworkElement empty;
        List<DiffRow> rows = new List<DiffRow>();
        bool only, quiet;

        public event Action Moved;      // the line selected changed (and with it the position among the differences)
        public event Action<MouseButtonEventArgs, List<MenuEntry>> Menu;
        public event Action<string, string> CopyRequest;

        public XmlDiffView(Palette pal, DiffResult r, bool onlyDiffs)
        {
            look = new XmlDiffLook(pal);
            result = r;
            only = onlyDiffs;
            string acc = Color.FromArgb(pal.Dark ? (byte)0x55 : (byte)0x40, pal.Accent.R, pal.Accent.G, pal.Accent.B).ToString();
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
            list.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(DiffRowView)) };
            list.Padding = new Thickness(0, 8, 0, 36);
            Children.Add(list);

            StackPanel e = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460, Margin = new Thickness(28), IsHitTestVisible = false };
            e.Children.Add(new GlyphView("check", 30, pal.Green) { HorizontalAlignment = HorizontalAlignment.Center });
            e.Children.Add(new TextBlock { Text = I18n.T("Los dos XML son iguales", "The two XML files are the same"), FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 17, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            e.Children.Add(new TextBlock { Text = I18n.T("No cuentan los espacios en blanco, el orden de los atributos ni los prefijos de los espacios de nombres.", "Whitespace, attribute order and namespace prefixes don't count."), FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            empty = e;
            Children.Add(empty);

            Defaults();
            list.SelectionChanged += delegate { if (!quiet && Moved != null) Moved(); };
            list.PreviewMouseLeftButtonDown += OnDown;
            list.PreviewMouseRightButtonDown += OnRightDown;
            list.PreviewMouseRightButtonUp += OnRightUp;
            list.PreviewKeyDown += OnKey;
            Reflow(null);
        }

        public ListBox List { get { return list; } }
        public List<DiffRow> Rows { get { return rows; } }
        public XmlDiffLook Look { get { return look; } }
        public DiffResult Result { get { return result; } }
        public bool Empty { get { return empty.Visibility == Visibility.Visible; } }

        public bool OnlyDiffs
        {
            get { return only; }
            set { if (only == value) return; only = value; Reflow(Selected()); }
        }

        // Open where the differences are; big added or removed branches and, in a big document, the unchanged ones start
        // closed (one click opens them).
        void Defaults()
        {
            // Once per comparison: a view made again (a new theme) keeps what the user opened and closed.
            if (result.Laid) return;
            result.Laid = true;
            bool small = result.Nodes <= 1500;
            Stack<DiffNode> st = new Stack<DiffNode>();
            foreach (DiffNode t in result.Top) st.Push(t);
            while (st.Count > 0)
            {
                DiffNode d = st.Pop();
                if (d.Inside) d.Expanded = true;
                else if (d.Kind == DiffKind.Added || d.Kind == DiffKind.Removed) d.Expanded = !d.Stop || d.Size <= 200;
                else if (d.Kind == DiffKind.Same) d.Expanded = d.Unfold = small;
                else d.Expanded = true;
                // Only what is made: an unchanged branch makes its children (open or not, as said here) when shown.
                foreach (DiffNode k in d.Made) st.Push(k);
            }
        }

        DiffNode Selected()
        {
            DiffRow r = list.SelectedItem as DiffRow;
            return r != null && r.Gap == 0 ? r.Node : null;
        }

        public DiffNode SelectedNode { get { return Selected(); } }

        void Reflow(DiffNode select)
        {
            ScrollViewer sv = Scroller();
            double off = sv != null ? sv.VerticalOffset : 0;
            quiet = true;
            try
            {
                // Two equal documents: the message, not a lone line of unchanged nodes.
                rows = only && result.Total == 0 ? new List<DiffRow>() : DiffRows.Walk(result.Top, only, look);
                list.ItemsSource = rows;
                empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                int i = Line(select);
                if (i >= 0) list.SelectedIndex = i;
                if (sv != null) { list.UpdateLayout(); sv.ScrollToVerticalOffset(off); }
                if (i >= 0) list.ScrollIntoView(rows[i]);
            }
            finally { quiet = false; }
            if (Moved != null) Moved();
        }

        // The line of a node, or of the nearest node holding it that has one (closed or filtered away); -1 for none.
        int Line(DiffNode n)
        {
            if (n == null) return -1;
            Dictionary<DiffNode, int> chain = new Dictionary<DiffNode, int>();
            for (DiffNode a = n; a != null; a = a.Parent) chain[a] = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                DiffRow r = rows[i];
                if (r.Gap == 0 && !r.Close && chain.ContainsKey(r.Node) && chain[r.Node] < 0) chain[r.Node] = i;
            }
            for (DiffNode a = n; a != null; a = a.Parent) if (chain[a] >= 0) return chain[a];
            return -1;
        }

        ScrollViewer Scroller()
        {
            if (!list.IsLoaded) return null;
            Decorator d = VisualTreeHelper.GetChildrenCount(list) > 0 ? VisualTreeHelper.GetChild(list, 0) as Decorator : null;
            return d != null ? d.Child as ScrollViewer : null;
        }

        // ---- Stepping through the differences

        // Which difference is selected, 0-based (-1 when the selected line is not one).
        public int Current
        {
            get
            {
                DiffNode d = Selected();
                if (d == null || !d.Stop) return -1;
                int i = Find(d.Order);
                return i < result.Stops.Count && result.Stops[i] == d ? i : -1;
            }
        }

        // First stop at or after a document position.
        int Find(int order)
        {
            List<DiffNode> s = result.Stops;
            int lo = 0, hi = s.Count;
            while (lo < hi) { int mid = (lo + hi) / 2; if (s[mid].Order < order) lo = mid + 1; else hi = mid; }
            return lo;
        }

        // Next (dir 1) or previous (dir -1) difference from the selected line; past either end it wraps. 0: there are
        // none; 1: moved; 2: moved and wrapped.
        public int Step(int dir)
        {
            List<DiffNode> s = result.Stops;
            if (s.Count == 0) return 0;
            DiffNode cur = Selected();
            int i;
            bool wrapped = false;
            if (dir > 0)
            {
                i = cur == null ? 0 : Find(cur.Order + 1);
                if (i >= s.Count) { i = 0; wrapped = cur != null; }
            }
            else
            {
                i = cur == null ? s.Count - 1 : Find(cur.Order) - 1;
                if (i < 0) { i = s.Count - 1; wrapped = cur != null; }
            }
            Reveal(s[i]);
            return wrapped ? 2 : 1;
        }

        // Opens what holds the node and selects its line.
        public void Reveal(DiffNode n)
        {
            for (DiffNode a = n.Parent; a != null; a = a.Parent) a.Expanded = true;
            Reflow(n);
            list.Focus();
        }

        // ---- Expand, collapse

        // Opening everything makes every node; closing touches only the ones made (the rest start closed).
        void SetAll(bool open)
        {
            Stack<KeyValuePair<DiffNode, int>> st = new Stack<KeyValuePair<DiffNode, int>>();
            foreach (DiffNode t in result.Top) st.Push(new KeyValuePair<DiffNode, int>(t, 0));
            while (st.Count > 0)
            {
                KeyValuePair<DiffNode, int> p = st.Pop();
                p.Key.Expanded = open || p.Value == 0;
                p.Key.Unfold = open;
                foreach (DiffNode k in open ? p.Key.Kids : p.Key.Made) st.Push(new KeyValuePair<DiffNode, int>(k, p.Value + 1));
            }
        }

        public void ExpandAll() { SetAll(true); Reflow(Selected()); }
        public void CollapseAll() { SetAll(false); Reflow(Selected()); }

        void Toggle(int i)
        {
            if (i < 0 || i >= rows.Count || !rows[i].Collapsible) return;
            DiffNode n = rows[i].Node;
            n.Expanded = !n.Expanded;
            Reflow(n);
        }

        DiffRowView RowAt(MouseEventArgs e, out Point inRow)
        {
            inRow = new Point();
            HitTestResult h = VisualTreeHelper.HitTest(list, e.GetPosition(list));
            DependencyObject d = h == null ? null : h.VisualHit;
            while (d != null && !(d is DiffRowView)) d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : null;
            DiffRowView v = d as DiffRowView;
            if (v != null) inRow = e.GetPosition(v);
            return v;
        }

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            Point p;
            DiffRowView v = RowAt(e, out p);
            if (v == null || v.Row == null || !v.Row.Collapsible) return;
            if (p.X >= DiffRowView.ArrowEnd(v.Row) && e.ClickCount != 2) return;
            e.Handled = true;
            Toggle(rows.IndexOf(v.Row));
        }

        void OnRightDown(object sender, MouseButtonEventArgs e)
        {
            Point p;
            DiffRowView v = RowAt(e, out p);
            if (v != null && v.Row != null && !list.SelectedItems.Contains(v.Row)) { list.SelectedItems.Clear(); list.SelectedItem = v.Row; }
            e.Handled = true;
        }

        void OnRightUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Menu == null) return;
            List<MenuEntry> m = new List<MenuEntry>();
            MenuEntry copy = MenuEntry.Item(I18n.T("Copiar", "Copy"), "copy", false, delegate { CopySelection(); });
            copy.Enabled = list.SelectedItems.Count > 0;
            m.Add(copy);
            m.Add(MenuEntry.Line());
            MenuEntry next = MenuEntry.Item(I18n.T("Siguiente diferencia   F7", "Next difference   F7"), "down", false, delegate { Step(1); });
            MenuEntry prev = MenuEntry.Item(I18n.T("Diferencia anterior   May\u00FAs+F7", "Previous difference   Shift+F7"), "up", false, delegate { Step(-1); });
            next.Enabled = prev.Enabled = result.Stops.Count > 0;
            m.Add(next);
            m.Add(prev);
            DiffRow r = list.SelectedItem as DiffRow;
            if (r != null && r.Collapsible)
            {
                m.Add(MenuEntry.Line());
                DiffRow sel = r;
                m.Add(MenuEntry.Item(r.Collapsed ? I18n.T("Expandir", "Expand") : I18n.T("Contraer", "Collapse"), r.Collapsed ? "plus" : "minus", false, delegate { Toggle(rows.IndexOf(sel)); }));
            }
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item(I18n.T("Seleccionar todo", "Select all"), "check", false, delegate { list.Focus(); list.SelectAll(); }));
            Menu(e, m);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl && e.Key == Key.C) { e.Handled = true; CopySelection(); return; }
            DiffRow r = list.SelectedItem as DiffRow;
            if (r == null || ctrl || !r.Collapsible) return;
            if (e.Key == Key.Right && r.Collapsed || e.Key == Key.Left && !r.Collapsed) { e.Handled = true; Toggle(rows.IndexOf(r)); }
        }

        // ---- Copying

        // The selected lines in the order shown, each with its mark (+, -, ~).
        public string SelectionText()
        {
            HashSet<DiffRow> chosen = new HashSet<DiffRow>();
            foreach (object o in list.SelectedItems) chosen.Add((DiffRow)o);
            StringBuilder s = new StringBuilder();
            foreach (DiffRow r in rows)
            {
                if (!chosen.Contains(r)) continue;
                if (s.Length > XmlTreeView.MaxCopy) break;
                s.Append(DiffRows.Line(r)).Append("\r\n");
            }
            return s.ToString().TrimEnd('\r', '\n');
        }

        public void CopySelection()
        {
            string t = SelectionText();
            int n = list.SelectedItems.Count;
            if (t.Length > 0 && CopyRequest != null) CopyRequest(t, n == 1 ? I18n.T("Copiado.", "Copied.") : I18n.Fmt("Copiadas {0} l\u00EDneas.", "{0} lines copied.", n.ToString("N0")));
        }

        // ---- Picture

        // The lines as shown (at most 700) on the window colour, 2x when it fits.
        public static BitmapSource Render(List<DiffRow> rows, XmlDiffLook look, out string error)
        {
            error = null;
            if (rows.Count == 0) { error = I18n.T("No hay diferencias que mostrar.", "No differences to display."); return null; }
            look = new XmlDiffLook(Palette.Export());   // the colours exports have always had, whatever the theme
            const double W = 1000;
            int n = Math.Min(rows.Count, 700);
            Border page = null;
            while (true)
            {
                StackPanel sp = new StackPanel { Width = W };
                for (int i = 0; i < n; i++)
                {
                    DiffRow r = rows[i];
                    sp.Children.Add(new DiffRowView(new DiffRow { Node = r.Node, Depth = r.Depth, Gap = r.Gap, Close = r.Close, Collapsed = r.Collapsed, Look = look }));
                }
                if (n < rows.Count)
                    sp.Children.Add(new TextBlock { Text = I18n.Fmt("\u2026 y {0} l\u00EDneas m\u00E1s", "\u2026 and {0} more lines", (rows.Count - n).ToString("N0")), Foreground = look.Syntax.Hint, FontSize = 12.5, Margin = new Thickness(XmlRowView.Gutter, 8, 0, 0) });
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
            if (h > 16000) { error = I18n.T("La comparaci\u00F3n es demasiado larga para una sola imagen.", "The comparison is too long for a single image."); return null; }
            double scale = h * 2 <= 16000 ? 2 : 1;
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(page);
            rtb.Freeze();
            return rtb;
        }
    }
}
