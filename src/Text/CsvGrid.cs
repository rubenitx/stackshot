// Stackshot - A CSV or TSV file as a table: the header stays on top, columns as wide as their content, numbers on the
// right, sorting by a column, a filter, and copying a cell, a row, a column or the whole table for Excel. Only the cells
// in view are drawn.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    public sealed class CsvGrid : FrameworkElement
    {
        readonly CsvTable t;
        readonly double[] widths;
        readonly bool[] numeric;
        List<int> order;              // data rows shown, in order (filtered, sorted)
        int sortCol = -1;
        bool sortDesc;
        string query = "";
        double oy, ox;
        int selRow = -1, selCol = -1;
        const double RowH = 26, HeadH = 30, NumW = 52, CellPad = 10;

        public event Action Picked;
        public event Action Menu;

        public CsvGrid(CsvTable table)
        {
            t = table;
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            widths = new double[t.Columns];
            numeric = new bool[t.Columns];
            Measure0();
            order = AllRows();
        }

        int First { get { return t.HasHeader ? 1 : 0; } }

        List<int> AllRows()
        {
            List<int> o = new List<int>();
            for (int i = First; i < t.Rows.Count; i++) o.Add(i);
            return o;
        }

        // Widths from the header and the first rows (between 56 and 380 DIPs); a column of numbers aligns them right.
        void Measure0()
        {
            int sample = Math.Min(t.Rows.Count, 400);
            int[] nums = new int[t.Columns], filled = new int[t.Columns];
            for (int c = 0; c < t.Columns; c++)
            {
                double w = 56;
                for (int r = 0; r < sample; r++)
                {
                    string s = t.Rows[r][c];
                    if (s.Length == 0) continue;
                    w = Math.Max(w, Math.Min(380, Ink.Text(s.Length > 60 ? s.Substring(0, 60) : s, r == 0 && t.HasHeader ? Ds.Semibold : Ds.Regular, 12.5, Colors.Black).WidthIncludingTrailingWhitespace + CellPad * 2 + (r == 0 ? 14 : 0)));
                    if (r >= First) { filled[c]++; if (CsvTable.IsNumber(s)) nums[c]++; }
                }
                widths[c] = Math.Ceiling(w);
                numeric[c] = filled[c] > 0 && nums[c] >= filled[c] * 0.8;
            }
        }

        public int Shown { get { return order.Count; } }

        // "Fila 12 · Importe: 1.234,50" for the status line, or null.
        public string Cell
        {
            get
            {
                if (selRow < 0 || selRow >= order.Count) return null;
                int r = order[selRow];
                string head = selCol >= 0 && t.HasHeader ? t.Rows[0][selCol] : selCol >= 0 ? "Columna " + (selCol + 1) : null;
                return "Fila " + (r - First + 1).ToString("N0") + (head != null ? " \u00B7 " + head : "");
            }
        }

        // Rows with a cell containing the text (case and accents aside).
        public void Filter(string q)
        {
            query = PdfPageText.Fold(q ?? "").Trim();
            Sort(sortCol, sortDesc, false);
            if (query.Length > 0)
            {
                List<int> o = new List<int>();
                foreach (int r in order)
                {
                    foreach (string s in t.Rows[r]) if (s.Length >= query.Length && PdfPageText.Fold(s).Contains(query)) { o.Add(r); break; }
                }
                order = o;
            }
            selRow = order.Count > 0 ? 0 : -1;
            oy = 0;
            InvalidateVisual();
            Tell();
        }

        void Sort(int col, bool desc, bool refilter)
        {
            sortCol = col;
            sortDesc = desc;
            if (col < 0) order = AllRows();
            else
            {
                List<int> o = CsvTable.SortedOrder(t, col, desc);
                order = o;
            }
            if (refilter && query.Length > 0) { string q = query; query = ""; Filter(q); return; }
            InvalidateVisual();
        }

        void Tell() { Action h = Picked; if (h != null) h(); }

        // ---- Text for the clipboard (tab-separated: Excel pastes it into cells)

        static string Tsv(string s)
        {
            if (s.IndexOf('\t') < 0 && s.IndexOf('\n') < 0 && s.IndexOf('\r') < 0 && s.IndexOf('"') < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public string SelectionText()
        {
            if (selRow < 0 || selRow >= order.Count) return "";
            if (selCol < 0) return RowText();
            return t.Rows[order[selRow]][selCol];
        }

        public string RowText()
        {
            if (selRow < 0 || selRow >= order.Count) return "";
            string[] r = t.Rows[order[selRow]];
            StringBuilder sb = new StringBuilder();
            for (int c = 0; c < r.Length; c++) { if (c > 0) sb.Append('\t'); sb.Append(Tsv(r[c])); }
            return sb.ToString();
        }

        public string ColumnText()
        {
            if (selCol < 0) return "";
            StringBuilder sb = new StringBuilder();
            if (t.HasHeader) sb.Append(Tsv(t.Rows[0][selCol])).Append("\r\n");
            foreach (int r in order) sb.Append(Tsv(t.Rows[r][selCol])).Append("\r\n");
            return sb.ToString();
        }

        public string AllText()
        {
            StringBuilder sb = new StringBuilder();
            List<int> rows = new List<int>();
            if (t.HasHeader) rows.Add(0);
            rows.AddRange(order);
            foreach (int r in rows)
            {
                string[] x = t.Rows[r];
                for (int c = 0; c < x.Length; c++) { if (c > 0) sb.Append('\t'); sb.Append(Tsv(x[c])); }
                sb.Append("\r\n");
                if (sb.Length > 64 * 1024 * 1024) break;
            }
            return sb.ToString();
        }

        // ---- Geometry

        double ColX(int c)
        {
            double x = NumW;
            for (int i = 0; i < c; i++) x += widths[i];
            return x;
        }

        double TotalW { get { return ColX(t.Columns); } }
        double Visible { get { return Math.Max(1, (ActualHeight - HeadH) / RowH); } }

        void Clamp()
        {
            oy = Math.Max(0, Math.Min(Math.Max(0, order.Count - Visible + 1), oy));
            ox = Math.Max(0, Math.Min(Math.Max(0, TotalW - ActualWidth + 8), ox));
        }

        void At(Point p, out int row, out int col)
        {
            row = p.Y < HeadH ? -1 : (int)Math.Floor(oy + (p.Y - HeadH) / RowH);
            col = -1;
            if (p.X < NumW) return;
            double x = p.X + ox;
            for (int c = 0; c < t.Columns; c++) if (x >= ColX(c) && x < ColX(c + 1)) { col = c; break; }
        }

        // ---- Drawing

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            Color bg = pal.Dark ? Color.FromRgb(30, 30, 32) : Color.FromRgb(252, 252, 253), alt = pal.Dark ? Color.FromRgb(36, 36, 39) : Color.FromRgb(245, 246, 248);
            dc.DrawRectangle(Ds.Brush(bg), null, new Rect(0, 0, ActualWidth, ActualHeight));
            Pen grid = new Pen(Ds.Brush(pal.Separator), 1);
            grid.Freeze();
            int first = (int)Math.Floor(oy), n = (int)Math.Ceiling(Visible) + 1;
            double y0 = HeadH - (oy - first) * RowH;
            int c0 = 0;
            while (c0 < t.Columns - 1 && ColX(c0 + 1) - ox < NumW) c0++;
            dc.PushClip(new RectangleGeometry(new Rect(0, HeadH, ActualWidth, Math.Max(0, ActualHeight - HeadH))));
            for (int i = 0; i < n && first + i < order.Count; i++)
            {
                int vr = first + i, r = order[vr];
                double y = y0 + i * RowH;
                if (vr % 2 == 1) dc.DrawRectangle(Ds.Brush(alt), null, new Rect(0, y, ActualWidth, RowH));
                if (vr == selRow) dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, selCol < 0 ? 0.22 : 0.08)), null, new Rect(0, y, ActualWidth, RowH));
                for (int c = c0; c < t.Columns; c++)
                {
                    double x = ColX(c) - ox;
                    if (x > ActualWidth) break;
                    if (vr == selRow && c == selCol) dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.45 : 0.28)), null, new Rect(x, y, widths[c], RowH));
                    Text(dc, t.Rows[r][c], x, y, widths[c], RowH, numeric[c] && CsvTable.IsNumber(t.Rows[r][c]), Ds.Regular, pal.Label, query);
                }
                FormattedText num = Ink.Text((r - First + 1).ToString("N0"), Ds.Regular, 11, pal.Label3);
                dc.DrawRectangle(Ds.Brush(bg), null, new Rect(0, y, NumW - 1, RowH));
                dc.DrawText(num, new Point(Math.Round(NumW - 10 - num.Width), Math.Round(y + (RowH - num.Height) / 2)));
            }
            dc.Pop();
            // The header, on top of the rows.
            dc.DrawRectangle(Ds.Brush(pal.Dark ? Color.FromRgb(40, 40, 43) : Color.FromRgb(238, 239, 242)), null, new Rect(0, 0, ActualWidth, HeadH));
            for (int c = c0; c < t.Columns; c++)
            {
                double x = ColX(c) - ox;
                if (x > ActualWidth) break;
                string h = t.HasHeader ? t.Rows[0][c] : ColumnName(c);
                Text(dc, h, x, 0, widths[c] - (c == sortCol ? 14 : 0), HeadH, numeric[c], Ds.Semibold, pal.Label, null);
                if (c == sortCol) Glyph.Draw(dc, sortDesc ? "down" : "up", x + widths[c] - 20, 8, 13, pal.Accent, 1.8);
                dc.DrawLine(grid, new Point(Math.Round(x + widths[c]) + 0.5, 6), new Point(Math.Round(x + widths[c]) + 0.5, HeadH - 6));
            }
            dc.DrawRectangle(Ds.Brush(pal.Dark ? Color.FromRgb(40, 40, 43) : Color.FromRgb(238, 239, 242)), null, new Rect(0, 0, NumW - 1, HeadH));
            dc.DrawLine(grid, new Point(0, HeadH - 0.5), new Point(ActualWidth, HeadH - 0.5));
            dc.DrawLine(grid, new Point(NumW - 0.5, 0), new Point(NumW - 0.5, ActualHeight));
        }

        // A1-style names for a table without a header: A, B, ... Z, AA...
        static string ColumnName(int c)
        {
            string s = "";
            c++;
            while (c > 0) { int m = (c - 1) % 26; s = (char)('A' + m) + s; c = (c - 1) / 26; }
            return s;
        }

        static void Text(DrawingContext dc, string s, double x, double y, double w, double h, bool right, Typeface face, Color c, string q)
        {
            if (string.IsNullOrEmpty(s) || w <= CellPad * 2) return;
            string shown = s.Length > 300 ? s.Substring(0, 300) : s;
            shown = shown.Replace("\r\n", " \u21B5 ").Replace('\n', ' ').Replace('\r', ' ');
            FormattedText ft = Ink.Text(shown, face, 12.5, c);
            ft.MaxTextWidth = Math.Max(1, w - CellPad * 2);
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;
            if (!string.IsNullOrEmpty(q) && PdfPageText.Fold(shown).Contains(q))
                dc.DrawRoundedRectangle(Ds.Brush(Color.FromArgb(60, 255, 214, 10)), null, new Rect(x + 3, y + 3, w - 6, h - 6), 4, 4);
            double tx = right ? x + w - CellPad - Math.Min(ft.WidthIncludingTrailingWhitespace, w - CellPad * 2) : x + CellPad;
            dc.DrawText(ft, new Point(Math.Round(tx), Math.Round(y + (h - ft.Height) / 2)));
        }

        // ---- Input

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            int row, col;
            At(e.GetPosition(this), out row, out col);
            if (row < 0 && col >= 0)
            {
                // The header sorts: up, down, back to the file's order.
                if (sortCol != col) Sort(col, false, true);
                else if (!sortDesc) Sort(col, true, true);
                else Sort(-1, false, true);
                e.Handled = true;
                return;
            }
            if (row < 0 || row >= order.Count) return;
            selRow = row;
            selCol = col;
            InvalidateVisual();
            Tell();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            int row, col;
            At(e.GetPosition(this), out row, out col);
            if (row >= 0 && row < order.Count) { selRow = row; selCol = col; InvalidateVisual(); Tell(); }
            Action h = Menu;
            if (h != null) h();
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) ox -= e.Delta * 0.8;
            else oy -= e.Delta / 120.0 * 3;
            Clamp();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); Clamp(); }

        public bool HandleKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down: Move(1, 0); return true;
                case Key.Up: Move(-1, 0); return true;
                case Key.Right: Move(0, 1); return true;
                case Key.Left: Move(0, -1); return true;
                case Key.PageDown: Move((int)Visible - 1, 0); return true;
                case Key.PageUp: Move(-(int)Visible + 1, 0); return true;
                case Key.Home: selRow = 0; oy = 0; Clamp(); InvalidateVisual(); Tell(); return true;
                case Key.End: selRow = order.Count - 1; oy = order.Count; Clamp(); InvalidateVisual(); Tell(); return true;
            }
            return false;
        }

        void Move(int dr, int dc)
        {
            if (order.Count == 0) return;
            selRow = Math.Max(0, Math.Min(order.Count - 1, (selRow < 0 ? 0 : selRow) + dr));
            selCol = Math.Max(0, Math.Min(t.Columns - 1, (selCol < 0 ? 0 : selCol) + dc));
            if (selRow < oy) oy = selRow;
            else if (selRow > oy + Visible - 2) oy = selRow - Visible + 2;
            double x0 = ColX(selCol), x1 = ColX(selCol + 1);
            if (x0 - ox < NumW) ox = x0 - NumW;
            else if (x1 - ox > ActualWidth) ox = x1 - ActualWidth + 8;
            Clamp();
            InvalidateVisual();
            Tell();
        }
    }
}
