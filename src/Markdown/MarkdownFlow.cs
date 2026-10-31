// Stackshot - Markdown as a read-only FlowDocument: the same look as MarkdownView, but text can be selected across blocks and copied.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Text;
using System.Windows.Media;

namespace Stackshot
{
    public sealed partial class MarkdownFlow
    {
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");

        public readonly RichTextBox Box;
        readonly Dictionary<string, Block> anchors = new Dictionary<string, Block>();
        readonly Dictionary<string, int> slugs = new Dictionary<string, int>();
        readonly Palette pal = Ds.Brushes;
        readonly Brush ink, faint, accent, codeBack, line, done;
        readonly double side;

        public event Action<MouseButtonEventArgs, List<MenuEntry>> Menu;
        public event Action<string> Copied;     // copied from its menu: what to tell the user

        // side: the least padding at each side of the page.
        public MarkdownFlow(double side)
        {
            this.side = side;
            ink = Ds.Brush(pal.Label);
            faint = Ds.Brush(pal.Label2);
            accent = Ds.Brush(pal.Link);   // links and ticked boxes are text on the page
            done = Ds.Brush(pal.High ? pal.Label3 : Ds.WithAlpha(pal.Label, pal.Dark ? 0.55 : 0.70));
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
                MenuEntry copy = MenuEntry.Item(I18n.T("Copiar", "Copy"), "copy", false, delegate { Box.Copy(); if (Copied != null) Copied(I18n.T("Copiado.", "Copied.")); });
                copy.Enabled = !Box.Selection.IsEmpty;
                m.Add(copy);
                m.Add(MenuEntry.Item(I18n.T("Seleccionar todo", "Select all"), "check", false, delegate { Box.Focus(); Box.SelectAll(); }));
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

        // What is on screen: one element (or none) per top-level block, and each block's signature.
        readonly List<Block> shown = new List<Block>();
        readonly List<string> keys = new List<string>();
        FlowDocument doc;

        // While editing only the blocks that changed are rebuilt: the document keeps the rest, so WPF lays out only them
        // (a full rebuild of 5,000 lines took over a second on every pause in typing).
        public void Set(List<MdBlock> blocks)
        {
            flat = null;
            List<string> next = new List<string>(blocks.Count);
            foreach (MdBlock b in blocks) next.Add(Key(b));
            if (doc == null || Box.Document != doc)
            {
                anchors.Clear();
                slugs.Clear();
                shown.Clear();
                keys.Clear();
                doc = new FlowDocument { FontFamily = Ds.Text, FontSize = 14.5, Foreground = ink, TextAlignment = TextAlignment.Left, ColumnWidth = 100000, PagePadding = new Thickness(side, 18, side, 44) };
                for (int i = 0; i < blocks.Count; i++) { Block e = Make(blocks[i], 0); shown.Add(e); if (e != null) doc.Blocks.Add(e); }
                keys.AddRange(next);
                Space(blocks);
                Box.Document = doc;
                Pad();
                return;
            }
            int max = Math.Min(keys.Count, next.Count), p = 0;
            while (p < max && keys[p] == next[p]) p++;
            int s = 0;
            while (s < max - p && keys[keys.Count - 1 - s] == next[next.Count - 1 - s]) s++;
            if (p == keys.Count && p == next.Count) return;
            // The first element kept after the change, to insert before.
            Block after = null;
            for (int i = keys.Count - s; i < keys.Count && after == null; i++) after = shown[i];
            for (int i = p; i < keys.Count - s; i++) if (shown[i] != null) doc.Blocks.Remove(shown[i]);
            List<Block> made = new List<Block>();
            for (int i = p; i < next.Count - s; i++)
            {
                Block e = Make(blocks[i], 0);
                made.Add(e);
                if (e == null) continue;
                if (after != null) doc.Blocks.InsertBefore(after, e); else doc.Blocks.Add(e);
            }
            shown.RemoveRange(p, keys.Count - s - p);
            shown.InsertRange(p, made);
            keys.Clear();
            keys.AddRange(next);
            Space(blocks);
        }

        // Spacing between top-level blocks, and the heading anchors in order (setting an unchanged margin costs nothing).
        void Space(List<MdBlock> blocks)
        {
            const double gap = 14;
            anchors.Clear();
            slugs.Clear();
            bool first = true;
            for (int i = 0; i < blocks.Count && i < shown.Count; i++)
            {
                Block e = shown[i];
                if (e == null) continue;
                bool heading = blocks[i].Kind == MdKind.Heading;
                Thickness m = new Thickness(0, first ? 0 : heading ? gap + 8 : 0, 0, heading ? gap * 0.55 : gap);
                if (e.Margin != m) e.Margin = m;
                first = false;
                if (heading)
                {
                    string slug = MarkdownTools.Slug(blocks[i].Text);
                    int n;
                    if (slugs.TryGetValue(slug, out n)) { slugs[slug] = n + 1; slug += "-" + n; } else slugs[slug] = 1;
                    anchors[slug] = e;
                }
            }
        }

        static string Key(MdBlock b)
        {
            StringBuilder sb = new StringBuilder();
            Key(b, sb);
            return sb.ToString();
        }

        static void Key(MdBlock b, StringBuilder sb)
        {
            sb.Append((int)b.Kind).Append('\u0001').Append(b.Level).Append('\u0001').Append(b.Lang).Append('\u0001').Append(b.Ordered ? '1' : '0').Append('\u0001').Append(b.Text).Append('\u0002');
            if (b.Children != null) foreach (MdBlock c in b.Children) { sb.Append('\u0003'); Key(c, sb); }
            if (b.Items != null)
                foreach (MdItem it in b.Items)
                {
                    sb.Append('\u0004').Append(it.Checked.HasValue ? (it.Checked.Value ? 'x' : 'o') : '-');
                    foreach (MdBlock c in it.Blocks) { sb.Append('\u0003'); Key(c, sb); }
                }
            if (b.Head != null)
            {
                sb.Append('\u0005').Append(string.Join("\u0001", b.Head.ToArray()));
                foreach (List<string> r in b.Rows) sb.Append('\u0006').Append(string.Join("\u0001", r.ToArray()));
                foreach (int a in b.Align) sb.Append(a);
            }
        }

        // ---- Find: the document as plain text, with where each piece of text starts, so a match can be selected.

        string flat;
        readonly List<int> runAt = new List<int>();
        readonly List<TextPointer> runPtr = new List<TextPointer>();

        public string FlatText()
        {
            if (flat != null) return flat;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            runAt.Clear(); runPtr.Clear();
            TextPointer p = Box.Document.ContentStart;
            while (p != null)
            {
                TextPointerContext c = p.GetPointerContext(LogicalDirection.Forward);
                if (c == TextPointerContext.Text)
                {
                    string s = p.GetTextInRun(LogicalDirection.Forward);
                    runAt.Add(sb.Length);
                    runPtr.Add(p);
                    sb.Append(s);
                }
                else if (c == TextPointerContext.ElementEnd && p.Parent is Paragraph) sb.Append('\n');
                else if (c == TextPointerContext.ElementStart && p.GetAdjacentElement(LogicalDirection.Forward) is LineBreak) sb.Append('\n');
                p = p.GetNextContextPosition(LogicalDirection.Forward);
            }
            flat = sb.ToString();
            return flat;
        }

        TextPointer At(int offset)
        {
            int lo = 0, hi = runAt.Count - 1, k = -1;
            while (lo <= hi) { int mid = (lo + hi) / 2; if (runAt[mid] <= offset) { k = mid; lo = mid + 1; } else hi = mid - 1; }
            if (k < 0) return null;
            return runPtr[k].GetPositionAtOffset(offset - runAt[k], LogicalDirection.Forward);
        }

        // Selects a stretch of FlatText and scrolls it into view.
        public void Select(int start, int length)
        {
            FlatText();
            TextPointer a = At(start), b = At(start + length);
            if (a == null || b == null) return;
            Box.Selection.Select(a, b);
            Rect r = a.GetCharacterRect(LogicalDirection.Forward);
            ScrollViewer sv = Scroller;
            if (sv == null || r.IsEmpty) return;
            if (r.Top < 0 || r.Bottom > sv.ViewportHeight) sv.ScrollToVerticalOffset(Math.Max(0, sv.VerticalOffset + r.Top - sv.ViewportHeight / 3));
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

        // BlockCollection.Count walks the whole collection, so emptiness is tracked here (a big document was quadratic).
        void Fill(BlockCollection into, List<MdBlock> blocks, int depth, double gap)
        {
            bool empty = into.FirstBlock == null;
            for (int i = 0; i < blocks.Count; i++)
            {
                Block e = Make(blocks[i], depth);
                if (e == null) continue;
                bool heading = blocks[i].Kind == MdKind.Heading;
                double top = empty ? 0 : heading ? gap + 8 : 0;
                e.Margin = new Thickness(0, top, 0, i == blocks.Count - 1 && depth > 0 ? 0 : heading ? gap * 0.55 : gap);
                into.Add(e);
                empty = false;
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
                if (s.Image && Picture(p, s)) continue;
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
                else if (MarkdownView.IsSafe(href)) Process.Start(href);
            }
            catch (Exception ex) { ShotStack.Log("Markdown enlace: " + ex.Message); }
        }
    }
}
