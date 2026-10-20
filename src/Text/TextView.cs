// Stackshot - Lines of text of any number, drawn only where they are seen: line numbers, colours by kind (log levels,
// JSON, settings files), search matches, character selection, wrapping for files that fit in memory, smooth scrolling.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public struct TextSpan
    {
        public int Start, Length;
        public Color Color;
        public bool Bold;
        public TextSpan(int start, int length, Color color, bool bold) { Start = start; Length = length; Color = color; Bold = bold; }
    }

    // What the view shows: rows (lines of the file, or only some of them while filtering), each with its line number.
    public sealed class TextRows
    {
        public Func<int> Count;
        public Func<int, string> Text;
        public Func<int, int> Number;     // 0-based line of the file
        public Func<int, Color?> Band;    // a background for the whole row (an error in a log), or null
    }

    public sealed class TextView : FrameworkElement
    {
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");
        const double Pad = 10;

        TextRows rows;
        public Func<string, List<TextSpan>> Colors;   // colours of one line, or null for none
        double fontSize = 13, lineH, charW;
        Typeface face, bold;
        double oy, ox;            // scroll: rows (fractional) and DIPs
        double targetY;
        bool gliding;
        bool wrap;
        List<int[]> visual;       // wrapping: for each visual row, [row, start column]
        int wrapCols;
        int maxLen = 1;
        public string Query = "";
        public int[] Current;     // the match shown: row, column, length
        int selRowA = -1, selColA, selRowB = -1, selColB;
        bool dragging;
        public int CaretRow = -1;

        public event Action Moved;        // scrolled, selection or caret changed
        public event Action<MouseButtonEventArgs> Menu;

        public TextView()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Cursor = Cursors.IBeam;
            SetFont(13);
            SizeChanged += delegate { if (wrap) Rewrap(); Clamp(); InvalidateVisual(); };
        }

        public void SetFont(double size)
        {
            fontSize = Math.Max(8, Math.Min(32, size));
            face = new Typeface(Mono, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            bold = new Typeface(Mono, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            FormattedText probe = Ink.Text("MMMMMMMMMM", face, fontSize, Colors0);
            charW = probe.WidthIncludingTrailingWhitespace / 10;
            lineH = Math.Ceiling(fontSize * 1.5);
            if (wrap) Rewrap();
            Clamp();
            InvalidateVisual();
        }

        public double FontSizeNow { get { return fontSize; } }
        static readonly Color Colors0 = System.Windows.Media.Colors.Black;

        public void Show(TextRows r)
        {
            rows = r;
            maxLen = 1;
            ClearSelection();
            if (wrap) Rewrap();
            Clamp();
            InvalidateVisual();
        }

        // The rows changed (more lines indexed, a filter): the view keeps its place, or stays at the end when it was there.
        public void Refresh(bool keepEnd)
        {
            bool atEnd = keepEnd && AtEnd;
            if (wrap) Rewrap();
            if (atEnd) oy = Math.Max(0, VisualCount - Visible);
            Clamp();
            InvalidateVisual();
            Tell();
        }

        public bool Wrap
        {
            get { return wrap; }
            set { wrap = value; visual = null; if (wrap) Rewrap(); ox = 0; Clamp(); InvalidateVisual(); }
        }

        int RowCount { get { return rows == null ? 0 : rows.Count(); } }
        int VisualCount { get { return wrap && visual != null ? visual.Count : RowCount; } }
        double Visible { get { return Math.Max(1, (ActualHeight - Pad) / lineH); } }
        public bool AtEnd { get { return oy >= VisualCount - Visible - 0.5; } }
        double Gutter { get { return Math.Max(3, Digits(LastNumber)) * charW + 22; } }

        int LastNumber
        {
            get
            {
                int n = RowCount;
                return n == 0 ? 1 : rows.Number(n - 1) + 1;
            }
        }

        static int Digits(int n) { int d = 1; while (n >= 10) { n /= 10; d++; } return d; }

        void Rewrap()
        {
            visual = new List<int[]>();
            int cols = Math.Max(20, (int)((ActualWidth - Gutter - Pad * 2) / Math.Max(1, charW)));
            wrapCols = cols;
            int n = RowCount;
            for (int r = 0; r < n; r++)
            {
                string t = rows.Text(r);
                int len = Math.Min(t.Length, TextFile.LineCap);
                if (len <= cols) { visual.Add(new int[] { r, 0 }); continue; }
                // Break at the last space within the width when there is one, as prose wraps.
                int at = 0;
                while (at < len)
                {
                    visual.Add(new int[] { r, at });
                    int end = Math.Min(len, at + cols);
                    if (end < len)
                    {
                        int sp = t.LastIndexOf(' ', end - 1, Math.Max(1, end - at));
                        if (sp > at + cols / 3) end = sp + 1;
                    }
                    at = end;
                }
            }
        }

        // ---- Positions

        void VisualToRow(int v, out int row, out int col)
        {
            if (wrap && visual != null && v >= 0 && v < visual.Count) { row = visual[v][0]; col = visual[v][1]; return; }
            row = v; col = 0;
        }

        int RowToVisual(int row, int col)
        {
            if (!wrap || visual == null) return row;
            int lo = 0, hi = visual.Count - 1, pick = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int[] x = visual[mid];
                if (x[0] < row || (x[0] == row && x[1] <= col)) { pick = mid; lo = mid + 1; } else hi = mid - 1;
            }
            return pick;
        }

        // The row and column under a point of the view.
        void At(Point p, out int row, out int col)
        {
            int v = (int)Math.Floor(oy + (p.Y - Pad / 2) / lineH);
            v = Math.Max(0, Math.Min(VisualCount - 1, v));
            int start;
            VisualToRow(v, out row, out start);
            col = start + Math.Max(0, (int)Math.Round((p.X - Gutter - Pad + ox) / charW));
            if (row >= 0 && row < RowCount)
            {
                int len = rows.Text(row).Length;
                int end = len;
                if (wrap && v + 1 < VisualCount) { int r2, c2; VisualToRow(v + 1, out r2, out c2); if (r2 == row) end = c2; }
                col = Math.Min(col, end);
            }
        }

        public void ScrollToRow(int row, bool smooth)
        {
            int v = RowToVisual(row, 0);
            if (v >= oy + 1 && v < oy + Visible - 2) return;
            ScrollTo(v - Visible / 3, smooth);
        }

        public void ScrollTo(double v, bool smooth)
        {
            targetY = Math.Max(0, Math.Min(Math.Max(0, VisualCount - Visible + 1), v));
            if (!smooth) { StopGlide(); oy = targetY; InvalidateVisual(); Tell(); return; }
            if (!gliding) { gliding = true; CompositionTarget.Rendering += Glide; }
        }

        public void ToEnd() { ScrollTo(VisualCount, false); }

        void Glide(object sender, EventArgs e)
        {
            double d = targetY - oy;
            if (Math.Abs(d) < 0.02) { oy = targetY; StopGlide(); }
            else oy += d * 0.3;
            InvalidateVisual();
            Tell();
        }

        void StopGlide()
        {
            if (!gliding) return;
            gliding = false;
            CompositionTarget.Rendering -= Glide;
        }

        void Clamp()
        {
            oy = Math.Max(0, Math.Min(Math.Max(0, VisualCount - Visible + 1), oy));
            double wide = wrap ? 0 : Math.Max(0, maxLen * charW + Gutter + Pad * 2 - ActualWidth);
            ox = Math.Max(0, Math.Min(wide, ox));
        }

        public int TopRow { get { int r, c; VisualToRow((int)oy, out r, out c); return r; } }

        void Tell() { Action h = Moved; if (h != null) h(); }

        // ---- Drawing

        readonly Dictionary<int, FormattedText> cache = new Dictionary<int, FormattedText>();
        int cacheFor = -1;

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            dc.DrawRectangle(Ds.Brush(pal.Dark ? Color.FromRgb(30, 30, 32) : Color.FromRgb(252, 252, 253)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (rows == null) return;
            double gw = Gutter;
            dc.DrawRectangle(Ds.Brush(pal.Dark ? Color.FromRgb(36, 36, 38) : Color.FromRgb(244, 244, 246)), null, new Rect(0, 0, gw - 6, ActualHeight));
            int first = (int)Math.Floor(oy), count = (int)Math.Ceiling(Visible) + 1, total = VisualCount;
            double y0 = Pad / 2 - (oy - first) * lineH;
            string q = PdfPageText.Fold(Query ?? "").Trim();
            dc.PushClip(new RectangleGeometry(new Rect(gw, 0, Math.Max(0, ActualWidth - gw), ActualHeight)));
            for (int i = 0; i < count && first + i < total; i++)
            {
                int v = first + i, row, start;
                VisualToRow(v, out row, out start);
                if (row < 0 || row >= RowCount) continue;
                double y = y0 + i * lineH;
                string full = rows.Text(row);
                if (full.Length > maxLen) maxLen = Math.Min(full.Length, TextFile.LineCap);
                int end = full.Length;
                if (wrap && v + 1 < total) { int r2, c2; VisualToRow(v + 1, out r2, out c2); if (r2 == row) end = c2; }
                end = Math.Min(end, Math.Min(full.Length, TextFile.LineCap));
                if (start > end) start = end;
                string part = full.Substring(start, end - start);
                Color? band = rows.Band != null ? rows.Band(row) : null;
                if (band.HasValue) dc.DrawRectangle(Ds.Brush(band.Value), null, new Rect(gw - 6, y, ActualWidth, lineH));
                if (row == CaretRow) dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.14 : 0.07)), null, new Rect(gw - 6, y, ActualWidth, lineH));
                double x = gw + Pad - ox;
                DrawSelection(dc, row, start, end, x, y, pal);
                if (q.Length > 0) DrawMatches(dc, full, row, start, end, q, x, y);
                FormattedText ft = Line(v, part, full, start, pal);
                dc.DrawText(ft, new Point(Math.Round(x), Math.Round(y + (lineH - ft.Height) / 2)));
            }
            dc.Pop();
            // Line numbers on top of the gutter (only on the first visual row of a line).
            for (int i = 0; i < count && first + i < total; i++)
            {
                int v = first + i, row, start;
                VisualToRow(v, out row, out start);
                if (start != 0 || row < 0 || row >= RowCount) continue;
                double y = y0 + i * lineH;
                FormattedText n = Ink.Text((rows.Number(row) + 1).ToString(CultureInfo.InvariantCulture), face, fontSize * 0.88, row == CaretRow ? pal.Label : pal.Label3);
                dc.DrawText(n, new Point(Math.Round(gw - 14 - n.Width), Math.Round(y + (lineH - n.Height) / 2)));
            }
            DrawBar(dc, pal);
        }

        FormattedText Line(int v, string part, string full, int start, Palette pal)
        {
            int key = v * 31 + part.GetHashCode();
            if (cacheFor != (pal.Dark ? 1 : 0) + (int)(fontSize * 10)) { cache.Clear(); cacheFor = (pal.Dark ? 1 : 0) + (int)(fontSize * 10); }
            FormattedText ft;
            if (cache.TryGetValue(key, out ft)) return ft;
            ft = new FormattedText(part, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, fontSize, Ds.Brush(pal.Label), Ink.PerDip);
            if (Colors != null)
            {
                List<TextSpan> spans = Colors(full);
                if (spans != null)
                    foreach (TextSpan s in spans)
                    {
                        int a = Math.Max(s.Start, start) - start, b = Math.Min(s.Start + s.Length, start + part.Length) - start;
                        if (b <= a) continue;
                        ft.SetForegroundBrush(Ds.Brush(s.Color), a, b - a);
                        if (s.Bold) ft.SetFontWeight(FontWeights.SemiBold, a, b - a);
                    }
            }
            if (cache.Count > 400) cache.Clear();
            cache[key] = ft;
            return ft;
        }

        void DrawMatches(DrawingContext dc, string full, int row, int start, int end, string q, double x, double y)
        {
            string f = full.Length == FoldedLength(full) ? PdfPageText.Fold(full) : null;
            if (f == null) return;   // folding changed the length (rare characters): no matches drawn on this line
            int at = f.IndexOf(q, start, StringComparison.Ordinal);
            while (at >= 0 && at < end)
            {
                int a = Math.Max(at, start), b = Math.Min(at + q.Length, end);
                bool now = Current != null && Current[0] == row && Current[1] == at;
                Color c = now ? Color.FromRgb(255, 170, 60) : Color.FromRgb(255, 222, 96);
                dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(c, now ? 0.85 : 0.6)), null, new Rect(x + (a - start) * charW - 1, y + 1, (b - a) * charW + 2, lineH - 2), 3, 3);
                at = f.IndexOf(q, at + Math.Max(1, q.Length), StringComparison.Ordinal);
            }
        }

        static int FoldedLength(string s) { return PdfPageText.Fold(s).Length; }

        void DrawSelection(DrawingContext dc, int row, int start, int end, double x, double y, Palette pal)
        {
            if (selRowA < 0) return;
            int r0 = selRowA, c0 = selColA, r1 = selRowB, c1 = selColB;
            if (r1 < r0 || (r1 == r0 && c1 < c0)) { int t = r0; r0 = r1; r1 = t; t = c0; c0 = c1; c1 = t; }
            if (row < r0 || row > r1) return;
            int a = row == r0 ? c0 : 0, b = row == r1 ? c1 : int.MaxValue;
            a = Math.Max(a, start);
            b = Math.Min(b, end + (row < r1 ? 1 : 0));
            if (b <= a && !(row < r1 && end == start)) return;
            dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.42 : 0.26)), null, new Rect(x + (a - start) * charW, y, Math.Max(charW * 0.6, (b - a) * charW), lineH));
        }

        // ---- Scroll bar (thin, as on macOS)

        double BarLength() { int n = VisualCount; return n <= Visible ? 0 : Math.Max(30, ActualHeight * Visible / n); }

        Rect BarRect()
        {
            double len = BarLength();
            if (len <= 0) return Rect.Empty;
            double max = Math.Max(1, VisualCount - Visible + 1), y = oy / max * (ActualHeight - len);
            return new Rect(ActualWidth - 10, y + 2, 7, len - 4);
        }

        bool barHot;

        void DrawBar(DrawingContext dc, Palette pal)
        {
            Rect r = BarRect();
            if (r.IsEmpty) return;
            Color c = pal.Dark ? Color.FromArgb((byte)(barHot ? 150 : 80), 255, 255, 255) : Color.FromArgb((byte)(barHot ? 120 : 60), 0, 0, 0);
            dc.DrawRoundedRectangle(Ds.Brush(c), null, r, 3.5, 3.5);
        }

        // ---- Selection

        public bool HasSelection { get { return selRowA >= 0 && (selRowA != selRowB || selColA != selColB); } }

        public void ClearSelection() { selRowA = selRowB = -1; InvalidateVisual(); }

        public void SelectRow(int row)
        {
            if (row < 0 || row >= RowCount) return;
            selRowA = row; selColA = 0; selRowB = row; selColB = rows.Text(row).Length;
            CaretRow = row;
            InvalidateVisual();
            Tell();
        }

        public void SelectAll()
        {
            int n = RowCount;
            if (n == 0) return;
            selRowA = 0; selColA = 0; selRowB = n - 1; selColB = rows.Text(n - 1).Length;
            InvalidateVisual();
            Tell();
        }

        // What is selected, lines joined with CRLF (as Windows pastes them); the caret's line when nothing is.
        public string SelectedText
        {
            get
            {
                if (!HasSelection) return CaretRow >= 0 && CaretRow < RowCount ? rows.Text(CaretRow) : "";
                int r0 = selRowA, c0 = selColA, r1 = selRowB, c1 = selColB;
                if (r1 < r0 || (r1 == r0 && c1 < c0)) { int t = r0; r0 = r1; r1 = t; t = c0; c0 = c1; c1 = t; }
                StringBuilder sb = new StringBuilder();
                for (int r = r0; r <= r1 && sb.Length < 50 * 1024 * 1024; r++)
                {
                    string t = rows.Text(r);
                    int a = r == r0 ? Math.Min(c0, t.Length) : 0, b = r == r1 ? Math.Min(c1, t.Length) : t.Length;
                    if (r > r0) sb.Append("\r\n");
                    if (b > a) sb.Append(t, a, b - a);
                }
                return sb.ToString();
            }
        }

        // ---- Mouse and keys

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point p = e.GetPosition(this);
            if (BarRect().Contains(p) || (p.X > ActualWidth - 14 && BarLength() > 0)) { barDrag = true; barFrom = p.Y; barOy = oy; CaptureMouse(); e.Handled = true; return; }
            int row, col;
            At(p, out row, out col);
            if (p.X < Gutter - 6) { SelectRow(row); e.Handled = true; return; }
            if (e.ClickCount == 2) { Word(row, col); e.Handled = true; return; }
            if (e.ClickCount >= 3) { SelectRow(row); e.Handled = true; return; }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && selRowA >= 0) { selRowB = row; selColB = col; }
            else { selRowA = selRowB = row; selColA = selColB = col; }
            CaretRow = row;
            dragging = true;
            CaptureMouse();
            InvalidateVisual();
            Tell();
            e.Handled = true;
        }

        bool barDrag;
        double barFrom, barOy;

        void Word(int row, int col)
        {
            string t = rows.Text(row);
            if (t.Length == 0) return;
            int a = Math.Min(col, t.Length - 1), b = a;
            Func<char, bool> w = delegate(char c) { return char.IsLetterOrDigit(c) || c == '_'; };
            if (!w(t[a]) && a > 0 && w(t[a - 1])) a = b = a - 1;
            if (!w(t[a])) { selRowA = selRowB = row; selColA = a; selColB = a + 1; }
            else
            {
                while (a > 0 && w(t[a - 1])) a--;
                while (b < t.Length && w(t[b])) b++;
                selRowA = selRowB = row; selColA = a; selColB = b;
            }
            CaretRow = row;
            InvalidateVisual();
            Tell();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            bool hot = p.X > ActualWidth - 14 && BarLength() > 0;
            if (hot != barHot) { barHot = hot; Cursor = hot ? Cursors.Arrow : p.X < Gutter - 6 ? Cursors.Arrow : Cursors.IBeam; InvalidateVisual(); }
            else if (!dragging && !barDrag) Cursor = p.X < Gutter - 6 ? Cursors.Arrow : Cursors.IBeam;
            if (barDrag)
            {
                double len = BarLength(), track = Math.Max(1, ActualHeight - len);
                oy = barOy + (p.Y - barFrom) / track * Math.Max(1, VisualCount - Visible + 1);
                Clamp();
                InvalidateVisual();
                Tell();
                return;
            }
            if (!dragging) return;
            if (p.Y < 0) ScrollTo(oy - 1, false);
            else if (p.Y > ActualHeight) ScrollTo(oy + 1, false);
            int row, col;
            At(p, out row, out col);
            selRowB = row;
            selColB = col;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            dragging = false;
            barDrag = false;
            ReleaseMouseCapture();
            Tell();
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            Action<MouseButtonEventArgs> h = Menu;
            if (h != null) h(e);
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { SetFont(fontSize + (e.Delta > 0 ? 1 : -1)); e.Handled = true; return; }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) { ox -= e.Delta * 0.6; Clamp(); InvalidateVisual(); e.Handled = true; return; }
            bool notch = e.Delta % 120 == 0;
            ScrollTo((gliding ? targetY : oy) - e.Delta / 120.0 * 3, notch);
            e.Handled = true;
        }

        public bool HandleKey(KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            double page = Math.Max(1, Visible - 2);
            switch (e.Key)
            {
                case Key.Down: Caret(1, ctrl); return true;
                case Key.Up: Caret(-1, ctrl); return true;
                case Key.PageDown: ScrollTo(oy + page, true); CaretRow = Math.Min(RowCount - 1, CaretRow + (int)page); return true;
                case Key.PageUp: ScrollTo(oy - page, true); CaretRow = Math.Max(0, CaretRow - (int)page); return true;
                case Key.Home: if (ctrl) { ScrollTo(0, false); CaretRow = 0; } else { ox = 0; InvalidateVisual(); } return true;
                case Key.End: if (ctrl) { ToEnd(); CaretRow = RowCount - 1; } return true;
                case Key.Left: if (!wrap) { ox = Math.Max(0, ox - charW * 8); InvalidateVisual(); } return true;
                case Key.Right: if (!wrap) { ox += charW * 8; Clamp(); InvalidateVisual(); } return true;
            }
            return false;
        }

        void Caret(int d, bool scrollOnly)
        {
            if (scrollOnly) { ScrollTo(oy + d, false); return; }
            CaretRow = Math.Max(0, Math.Min(RowCount - 1, (CaretRow < 0 ? TopRow : CaretRow) + d));
            ScrollToRow(CaretRow, false);
            InvalidateVisual();
            Tell();
        }

        public void Detach() { StopGlide(); rows = null; cache.Clear(); }
    }
}
