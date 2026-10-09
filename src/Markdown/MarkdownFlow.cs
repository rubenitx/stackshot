// Stackshot - Markdown as a read-only FlowDocument: the same look as MarkdownView, but text can be selected across blocks and copied.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    public sealed class MarkdownFlow
    {
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");

        public readonly RichTextBox Box;
        readonly Dictionary<string, Block> anchors = new Dictionary<string, Block>();
        readonly Dictionary<string, int> slugs = new Dictionary<string, int>();
        readonly Palette pal = Ds.Brushes;
        readonly Brush ink, faint, accent, codeBack, line, done;
        readonly double side;

        public event Action<MouseButtonEventArgs, List<MenuEntry>> Menu;

        // side: the least padding at each side of the page.
        public MarkdownFlow(double side)
        {
            this.side = side;
            ink = Ds.Brush(pal.Label);
            faint = Ds.Brush(pal.Label2);
            accent = Ds.Brush(pal.Accent);
            done = Ds.Brush(Ds.WithAlpha(pal.Label, 0.55));
            codeBack = Ds.Brush(pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0));
            line = Ds.Brush(pal.Hairline);
            Box = new RichTextBox
            {
                IsReadOnly = true, IsDocumentEnabled = true, IsReadOnlyCaretVisible = false, BorderThickness = new Thickness(0),
                Background = Brushes.Transparent, Foreground = ink, FontFamily = Ds.Text, FontSize = 14.5, FocusVisualStyle = null,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                SelectionBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.4)), Resources = HomeWindow.ScrollStyle(), UndoLimit = 0,
                SpellCheck = { IsEnabled = false }, Padding = new Thickness(0)
            };
            Box.IsInactiveSelectionHighlightEnabled = true;
            Box.SizeChanged += delegate { Pad(); };
            Box.ContextMenuOpening += delegate(object o, ContextMenuEventArgs e) { e.Handled = true; };
            Box.PreviewMouseRightButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; Box.Focus(); };
            Box.PreviewMouseRightButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (Menu == null) return;
                List<MenuEntry> m = new List<MenuEntry>();
                MenuEntry copy = MenuEntry.Item("Copiar", "copy", false, delegate { Box.Copy(); });
                copy.Enabled = !Box.Selection.IsEmpty;
                m.Add(copy);
                m.Add(MenuEntry.Item("Seleccionar todo", "check", false, delegate { Box.Focus(); Box.SelectAll(); }));
                Menu(e, m);
            };
            // Mouse wheel moves the page by a comfortable step (the box scrolls by lines otherwise).
            Box.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e)
            {
                ScrollViewer sv = Scroller;
                if (sv == null) return;
                e.Handled = true;
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            };
            Box.Document = new FlowDocument();
        }

        public ScrollViewer Scroller
        {
            get
            {
                if (Box.Template == null) return null;
                Box.ApplyTemplate();
                return Box.Template.FindName("PART_ContentHost", Box) as ScrollViewer;
            }
        }

        void Pad()
        {
            double w = Box.ActualWidth;
            double p = Math.Max(side, (w - MarkdownView.PageWidth) / 2);
            Thickness t = new Thickness(p, 18, p, 44);
            if (Box.Document != null && Box.Document.PagePadding != t) Box.Document.PagePadding = t;
        }

        public void Set(List<MdBlock> blocks)
        {
            anchors.Clear();
            slugs.Clear();
            FlowDocument d = new FlowDocument { FontFamily = Ds.Text, FontSize = 14.5, Foreground = ink, TextAlignment = TextAlignment.Left, ColumnWidth = 100000, PagePadding = new Thickness(side, 18, side, 44) };
            Fill(d.Blocks, blocks, 0, 14);
            Box.Document = d;
            Pad();
        }

        public bool Scroll(string slug)
        {
            Block b;
            if (slug.StartsWith("#", StringComparison.Ordinal)) slug = slug.Substring(1);
            if (!anchors.TryGetValue(slug.ToLowerInvariant(), out b)) return false;
            b.BringIntoView();
            return true;
        }

        // ---- Blocks

        void Fill(BlockCollection into, List<MdBlock> blocks, int depth, double gap)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                Block e = Make(blocks[i], depth);
                if (e == null) continue;
                bool heading = blocks[i].Kind == MdKind.Heading;
                double top = into.Count == 0 ? 0 : heading ? gap + 8 : 0;
                e.Margin = new Thickness(0, top, 0, i == blocks.Count - 1 && depth > 0 ? 0 : heading ? gap * 0.55 : gap);
                into.Add(e);
            }
        }

        Block Make(MdBlock b, int depth)
        {
            switch (b.Kind)
            {
                case MdKind.Heading: return Heading(b);
                case MdKind.Paragraph: return Para(b.Text, 14.5, ink, FontWeights.Normal);
                case MdKind.Code: return Code(b);
                case MdKind.Quote: return Quote(b, depth);
                case MdKind.List: return Items(b, depth);
                case MdKind.Table: return TableBlock(b);
                default: return new BlockUIContainer(new System.Windows.Controls.Border { Height = 1, Background = line, Margin = new Thickness(0, 6, 0, 6) });
            }
        }

        Block Heading(MdBlock b)
        {
            double[] sizes = { 29, 23, 19, 16.5, 15, 14 };
            int lv = Math.Max(1, Math.Min(6, b.Level));
            Paragraph p = Para(b.Text, sizes[lv - 1], lv == 6 ? faint : ink, FontWeights.SemiBold);
            p.FontFamily = Ds.Display;
            p.LineHeight = Math.Round(sizes[lv - 1] * 1.3);
            if (lv <= 2) { p.BorderBrush = line; p.BorderThickness = new Thickness(0, 0, 0, 1); p.Padding = new Thickness(0, 0, 0, 7); }
            string slug = MarkdownTools.Slug(b.Text);
            int n;
            if (slugs.TryGetValue(slug, out n)) { slugs[slug] = n + 1; slug += "-" + n; } else slugs[slug] = 1;
            anchors[slug] = p;
            return p;
        }

        Block Code(MdBlock b)
        {
            Paragraph p = new Paragraph { FontFamily = Mono, FontSize = 12.8, Foreground = ink, LineHeight = 19, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Background = codeBack, BorderBrush = line, BorderThickness = new Thickness(1), Padding = new Thickness(14, 11, 14, 12) };
            string[] ls = b.Text.Replace("\r", "").Split('\n');
            for (int i = 0; i < ls.Length; i++)
            {
                if (i > 0) p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(ls[i]));
            }
            if (!string.IsNullOrEmpty(b.Lang)) p.ToolTip = b.Lang;
            return p;
        }

        Block Quote(MdBlock b, int depth)
        {
            // A one-cell table gives the bar on the left and keeps the blocks inside it selectable.
            Table t = new Table { CellSpacing = 0 };
            t.Columns.Add(new TableColumn());
            TableRowGroup g = new TableRowGroup();
            TableRow r = new TableRow();
            TableCell c = new TableCell { BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.7)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 1, 0, 1) };
            Fill(c.Blocks, b.Children, depth + 1, 8);
            r.Cells.Add(c);
            g.Rows.Add(r);
            t.RowGroups.Add(g);
            return t;
        }

        Block Items(MdBlock b, int depth)
        {
            bool tasks = false;
            foreach (MdItem it in b.Items) if (it.Checked.HasValue) tasks = true;
            List list = new List { MarkerOffset = 6, Padding = new Thickness(b.Ordered && !tasks ? 28 : 20, 0, 0, 0) };
            if (tasks) list.MarkerStyle = TextMarkerStyle.None;
            else if (b.Ordered) { list.MarkerStyle = TextMarkerStyle.Decimal; list.StartIndex = b.Level > 0 ? b.Level : 1; }
            else list.MarkerStyle = depth % 3 == 0 ? TextMarkerStyle.Disc : depth % 3 == 1 ? TextMarkerStyle.Circle : TextMarkerStyle.Square;
            for (int i = 0; i < b.Items.Count; i++)
            {
                MdItem it = b.Items[i];
                ListItem li = new ListItem { Margin = new Thickness(0, 0, 0, i == b.Items.Count - 1 ? 0 : 4) };
                Fill(li.Blocks, it.Blocks, depth + 1, 6);
                if (tasks)
                {
                    string mark = it.Checked.HasValue ? (it.Checked.Value ? "\u2611 " : "\u2610 ") : "\u2022 ";
                    Paragraph first = li.Blocks.FirstBlock as Paragraph;
                    if (first == null) { first = new Paragraph(); li.Blocks.InsertBefore(li.Blocks.FirstBlock, first); }
                    first.TextIndent = -20;
                    Run m = new Run(mark) { Foreground = it.Checked == true ? accent : faint };
                    if (first.Inlines.Count > 0) first.Inlines.InsertBefore(first.Inlines.FirstInline, m); else first.Inlines.Add(m);
                }
                if (it.Checked == true) li.Foreground = done;
                list.ListItems.Add(li);
            }
            return list;
        }


        Block TableBlock(MdBlock b)
        {
            int cols = b.Head.Count;
            Table t = new Table { CellSpacing = 0 };
            for (int c = 0; c < cols; c++)
            {
                int w = 4;
                w = Math.Max(w, MarkdownDoc.Plain(b.Head[c]).Length);
                foreach (List<string> r in b.Rows) w = Math.Max(w, MarkdownDoc.Plain(r[c]).Length);
                t.Columns.Add(new TableColumn { Width = new GridLength(Math.Min(w, 36) + 4, GridUnitType.Star) });
            }
            TableRowGroup g = new TableRowGroup();
            for (int r = 0; r <= b.Rows.Count; r++)
            {
                TableRow row = new TableRow();
                for (int c = 0; c < cols; c++)
                {
                    bool head = r == 0;
                    Paragraph p = Para(head ? b.Head[c] : b.Rows[r - 1][c], 13.5, ink, head ? FontWeights.SemiBold : FontWeights.Normal);
                    p.TextAlignment = b.Align[c] == 1 ? TextAlignment.Center : b.Align[c] == 2 ? TextAlignment.Right : TextAlignment.Left;
                    TableCell cell = new TableCell(p) { Padding = new Thickness(11, 7, 11, 8), Background = head ? codeBack : null, BorderBrush = line, BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1) };
                    row.Cells.Add(cell);
                }
                g.Rows.Add(row);
            }
            t.RowGroups.Add(g);
            return t;
        }

        // ---- Text

        Paragraph Para(string src, double size, Brush fg, FontWeight weight)
        {
            Paragraph p = new Paragraph { FontSize = size, Foreground = fg, FontWeight = weight, LineHeight = Math.Round(size * 1.52), LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0) };
            foreach (MdSpan s in MarkdownDoc.Inline(src))
            {
                if (s.Break) { p.Inlines.Add(new LineBreak()); continue; }
                Run r = new Run(s.Text);
                if (s.Bold) r.FontWeight = FontWeights.Bold;
                if (s.Italic) r.FontStyle = FontStyles.Italic;
                if (s.Strike) r.TextDecorations = TextDecorations.Strikethrough;
                if (s.Code)
                {
                    r.FontFamily = Mono;
                    r.FontSize = size * 0.9;
                    r.Background = codeBack;
                    r.Text = "\u2009" + s.Text + "\u2009";
                }
                if (s.Href != null && MarkdownView.IsSafe(s.Href))
                {
                    Hyperlink h = new Hyperlink(r) { Foreground = accent, TextDecorations = null, Cursor = Cursors.Hand };
                    string href = s.Href;
                    h.Click += delegate { Open(href); };
                    h.ToolTip = href;
                    p.Inlines.Add(h);
                }
                else p.Inlines.Add(r);
            }
            return p;
        }

        void Open(string href)
        {
            try
            {
                if (href.StartsWith("#", StringComparison.Ordinal)) Scroll(href);
                else Process.Start(href);
            }
            catch (Exception ex) { ShotStack.Log("Markdown enlace: " + ex.Message); }
        }
    }
}
