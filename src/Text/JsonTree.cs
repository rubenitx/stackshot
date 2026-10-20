// Stackshot - A JSON document as a tree that folds: keys, values coloured by type, sizes of what is folded, search that
// opens the branches leading to each match. Only the rows in view are drawn, so a document of any size scrolls smoothly.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    public sealed class JsonTree : FrameworkElement
    {
        readonly JsonNode root;
        readonly HashSet<JsonNode> open = new HashSet<JsonNode>();
        readonly List<JsonNode> rows = new List<JsonNode>();
        readonly List<int> depth = new List<int>();
        readonly HashSet<JsonNode> hits = new HashSet<JsonNode>();
        readonly List<JsonNode> hitList = new List<JsonNode>();
        int hitAt = -1;
        string query = "";
        JsonNode selected;
        double oy;
        const double RowH = 24, Indent = 16, Pad = 8;

        public event Action<JsonNode> Picked, Opened, Menu;

        public JsonTree(JsonNode root)
        {
            this.root = root;
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            open.Add(root);
            // The first level open, and a small document all of it.
            if (root.Children != null) foreach (JsonNode c in root.Children) if (Size(c) < 40) open.Add(c);
            if (JsonDoc.Count(root) < 300) ExpandAll(true);
            else Flatten();
            selected = root;
        }

        static int Size(JsonNode n) { return n.Children == null ? 0 : n.Children.Count; }

        public JsonNode Selected { get { return selected; } }
        public int Matches { get { return hitList.Count; } }

        void Flatten()
        {
            rows.Clear();
            depth.Clear();
            Stack<KeyValuePair<JsonNode, int>> todo = new Stack<KeyValuePair<JsonNode, int>>();
            todo.Push(new KeyValuePair<JsonNode, int>(root, 0));
            while (todo.Count > 0)
            {
                KeyValuePair<JsonNode, int> x = todo.Pop();
                rows.Add(x.Key);
                depth.Add(x.Value);
                if (x.Key.Children == null || !open.Contains(x.Key)) continue;
                for (int i = x.Key.Children.Count - 1; i >= 0; i--) todo.Push(new KeyValuePair<JsonNode, int>(x.Key.Children[i], x.Value + 1));
            }
            Clamp();
            InvalidateVisual();
        }

        public void ExpandAll(bool on)
        {
            open.Clear();
            open.Add(root);
            if (on)
            {
                Stack<JsonNode> s = new Stack<JsonNode>();
                s.Push(root);
                while (s.Count > 0 && open.Count < 200000)
                {
                    JsonNode n = s.Pop();
                    if (n.Children == null) continue;
                    open.Add(n);
                    foreach (JsonNode c in n.Children) s.Push(c);
                }
            }
            Flatten();
        }

        // Keys and values that contain the text (case and accents aside); their branches open.
        public void Filter(string q)
        {
            query = PdfPageText.Fold(q ?? "").Trim();
            hits.Clear();
            hitList.Clear();
            hitAt = -1;
            if (query.Length > 0)
            {
                Stack<JsonNode> s = new Stack<JsonNode>();
                s.Push(root);
                List<JsonNode> order = new List<JsonNode>();
                while (s.Count > 0)
                {
                    JsonNode n = s.Pop();
                    order.Add(n);
                    if (n.Children != null) for (int i = n.Children.Count - 1; i >= 0; i--) s.Push(n.Children[i]);
                }
                foreach (JsonNode n in order)
                {
                    bool k = n.Key != null && PdfPageText.Fold(n.Key).Contains(query), v = n.Value != null && PdfPageText.Fold(n.Value).Contains(query);
                    if (!k && !v) continue;
                    hits.Add(n);
                    hitList.Add(n);
                    for (JsonNode p = n.Parent; p != null; p = p.Parent) open.Add(p);
                    if (hitList.Count >= 20000) break;
                }
            }
            Flatten();
            if (hitList.Count > 0) Step(1);
        }

        public void Step(int d)
        {
            if (hitList.Count == 0) return;
            hitAt = hitAt < 0 ? 0 : (hitAt + d + hitList.Count) % hitList.Count;
            Pick(hitList[hitAt]);
        }

        void Pick(JsonNode n)
        {
            selected = n;
            int i = rows.IndexOf(n);
            if (i >= 0)
            {
                double vis = ActualHeight / RowH;
                if (i < oy + 1 || i > oy + vis - 2) oy = i - vis / 3;
                Clamp();
            }
            InvalidateVisual();
            Action<JsonNode> h = Picked;
            if (h != null) h(n);
        }

        void Clamp() { oy = Math.Max(0, Math.Min(Math.Max(0, rows.Count - ActualHeight / RowH + 1), oy)); }

        void Toggle(JsonNode n)
        {
            if (n.Children == null || n.Children.Count == 0) return;
            if (open.Contains(n) && n != root) open.Remove(n); else open.Add(n);
            Flatten();
        }

        // ---- Drawing

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            dc.DrawRectangle(Ds.Brush(pal.Dark ? Color.FromRgb(30, 30, 32) : Color.FromRgb(252, 252, 253)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            int first = (int)Math.Floor(oy), n = (int)Math.Ceiling(ActualHeight / RowH) + 1;
            double y0 = Pad - (oy - first) * RowH;
            Color keyC = pal.Dark ? Color.FromRgb(156, 220, 254) : Color.FromRgb(4, 81, 165), strC = pal.Dark ? Color.FromRgb(206, 145, 120) : Color.FromRgb(163, 21, 21),
                  numC = pal.Dark ? Color.FromRgb(181, 206, 168) : Color.FromRgb(9, 134, 88), wordC = pal.Dark ? Color.FromRgb(86, 156, 214) : Color.FromRgb(0, 0, 255);
            for (int i = 0; i < n && first + i < rows.Count; i++)
            {
                JsonNode node = rows[first + i];
                double y = y0 + i * RowH, x = Pad + depth[first + i] * Indent;
                if (node == selected) dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, IsKeyboardFocusWithin ? (pal.Dark ? 0.4 : 0.22) : 0.12)), null, new Rect(4, y + 1, ActualWidth - 8, RowH - 2), 5, 5);
                else if (hits.Contains(node)) dc.DrawRoundedRectangle(Ds.Brush(Color.FromArgb(70, 255, 214, 10)), null, new Rect(4, y + 1, ActualWidth - 8, RowH - 2), 5, 5);
                bool box = node.Children != null && node.Children.Count > 0;
                if (box) Glyph.Draw(dc, open.Contains(node) ? "down" : "right", x - 2, y + 5, 14, pal.Label2, 1.8);
                x += 14;
                string label = node.Key != null ? node.Key : node.Index >= 0 ? node.Index.ToString(CultureInfo.InvariantCulture) : node == root ? "JSON" : "";
                FormattedText k = Ink.Text(label, node.Key != null ? Ds.Semibold : Ds.Regular, 12.5, node.Key != null ? keyC : pal.Label3);
                dc.DrawText(k, new Point(Math.Round(x), Math.Round(y + (RowH - k.Height) / 2)));
                x += k.WidthIncludingTrailingWhitespace;
                FormattedText colon = Ink.Text(":  ", Ds.Regular, 12.5, pal.Label3);
                dc.DrawText(colon, new Point(Math.Round(x), Math.Round(y + (RowH - colon.Height) / 2)));
                x += colon.WidthIncludingTrailingWhitespace;
                string value = box && open.Contains(node) ? (node.Kind == JsonKind.Object ? "{ " + node.Children.Count + " }" : "[ " + node.Children.Count + " ]") : JsonDoc.Summary(node);
                Color vc = node.Kind == JsonKind.String ? strC : node.Kind == JsonKind.Number ? numC : node.Kind == JsonKind.Object || node.Kind == JsonKind.Array ? pal.Label3 : wordC;
                FormattedText v = Ink.Text(value, Ds.Regular, 12.5, vc);
                v.MaxTextWidth = Math.Max(10, ActualWidth - x - 12);
                v.MaxLineCount = 1;
                v.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(v, new Point(Math.Round(x), Math.Round(y + (RowH - v.Height) / 2)));
            }
        }

        // ---- Input

        int RowAt(Point p) { return (int)Math.Floor(oy + (p.Y - Pad) / RowH); }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            int i = RowAt(e.GetPosition(this));
            if (i < 0 || i >= rows.Count) return;
            JsonNode n = rows[i];
            double x = Pad + depth[i] * Indent;
            if (e.ClickCount == 2) { Action<JsonNode> h = Opened; if (h != null) h(n); e.Handled = true; return; }
            if (e.GetPosition(this).X < x + 16) Toggle(n);
            Pick(n);
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            int i = RowAt(e.GetPosition(this));
            if (i >= 0 && i < rows.Count) Pick(rows[i]);
            Action<JsonNode> h = Menu;
            if (h != null && selected != null) h(selected);
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            oy -= e.Delta / 120.0 * 3;
            Clamp();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); Clamp(); }

        public bool HandleKey(KeyEventArgs e)
        {
            int i = selected == null ? -1 : rows.IndexOf(selected);
            switch (e.Key)
            {
                case Key.Down: if (i + 1 < rows.Count) Pick(rows[i + 1]); return true;
                case Key.Up: if (i > 0) Pick(rows[i - 1]); return true;
                case Key.Right:
                    if (selected != null && selected.Children != null && selected.Children.Count > 0)
                    {
                        if (!open.Contains(selected)) Toggle(selected); else Pick(selected.Children[0]);
                    }
                    return true;
                case Key.Left:
                    if (selected != null && open.Contains(selected) && selected != root) Toggle(selected);
                    else if (selected != null && selected.Parent != null) Pick(selected.Parent);
                    return true;
                case Key.Space: if (selected != null) Toggle(selected); return true;
                case Key.Enter: if (selected != null) { Action<JsonNode> h = Opened; if (h != null) h(selected); } return true;
                case Key.Home: if (rows.Count > 0) Pick(rows[0]); return true;
                case Key.End: if (rows.Count > 0) Pick(rows[rows.Count - 1]); return true;
                case Key.PageDown: if (rows.Count > 0) Pick(rows[Math.Min(rows.Count - 1, i + (int)(ActualHeight / RowH))]); return true;
                case Key.PageUp: if (rows.Count > 0) Pick(rows[Math.Max(0, i - (int)(ActualHeight / RowH))]); return true;
            }
            return false;
        }
    }
}
