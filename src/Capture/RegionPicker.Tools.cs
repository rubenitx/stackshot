// Stackshot - Two helpers in the region picker: a colour picker (C shows a loupe of the pixels under the pointer; a
// click or Enter copies its #RRGGBB) and a ruler (dragging with Shift measures in screen pixels instead of selecting).
// Both read the frozen screen the picker already holds and are recomposed only where they are, like the rest.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class RegionPicker
    {
        const int LoupeCells = 9;
        bool loupe, measuring, measured;
        Point mFrom, mTo;

        int Cell { get { return Math.Max(8, Pz(12)); } }

        // What the tools cover now, for the rectangles to recompose.
        void AddTools(List<Rectangle> p)
        {
            if (loupe) p.Add(Rectangle.Inflate(LoupeRect(), 3, 3));
            if (measuring || measured) p.Add(RulerRect());
        }

        void PaintTools(Dib target, Point origin, List<Rectangle> rs)
        {
            if ((!loupe && !measuring && !measured) || rs.Count == 0) return;
            using (Graphics g = Clipped(target, origin, rs))
            {
                if (measuring || measured) PaintRuler(g);
                if (loupe) PaintLoupe(g);
            }
        }

        // C: the loupe on or off. Enter with it on: copy. Esc: first the loupe or the ruler go, then the picker.
        bool ToolKey(Keys k)
        {
            if (adjusting) return false;
            if (k == Keys.C && (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == 0) { loupe = !loupe; interacted = true; Present(); return true; }
            if (k == Keys.Enter && loupe) { CopyColor(); return true; }
            if (k == Keys.Escape && (loupe || measured))
            {
                loupe = false;
                measured = false;
                Present();
                return true;
            }
            return false;
        }

        // A press: with the loupe, it copies the colour; with Shift, it starts measuring. Either way no area is begun.
        bool ToolPress(Point at)
        {
            if (loupe) { CopyColor(); return true; }
            if (measured) { measured = false; Present(); }
            if ((Control.ModifierKeys & Keys.Shift) == 0) return false;
            measuring = true;
            interacted = true;
            mFrom = mTo = at;
            Present();
            return true;
        }

        // Close to straight, it snaps level or plumb.
        void MeasureTo(Point p)
        {
            int snap = Pz(8);
            if (Math.Abs(p.X - mFrom.X) < snap) p.X = mFrom.X;
            else if (Math.Abs(p.Y - mFrom.Y) < snap) p.Y = mFrom.Y;
            mTo = p;
        }

        Color PixelAt(int x, int y)
        {
            x = Math.Max(0, Math.Min(bright.Width - 1, x));
            y = Math.Max(0, Math.Min(bright.Height - 1, y));
            int v = Marshal.ReadInt32(bright.Bits, (y * bright.Width + x) * 4);
            return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        static string Hex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        // Copies the colour under the pointer and closes the picker; a notice says which.
        void CopyColor()
        {
            string hex = Hex(PixelAt(cur.X, cur.Y));
            bool ok = false;
            for (int i = 0; i < 5 && !ok; i++)
            {
                try { Clipboard.SetText(hex); ok = true; }
                catch (ExternalException) { System.Threading.Thread.Sleep(30); }
            }
            Cancel();
            if (ShotStack.Running != null)
                ShotStack.Running.Notify(ok ? I18n.T("Color copiado", "Color copied") : I18n.T("No se pudo copiar el color", "Could not copy color"), ok ? (hex + I18n.T(" est\u00E1 listo para pegar.", " is ready to paste.")) : I18n.T("El portapapeles est\u00E1 ocupado: vuelve a intentarlo.", "Clipboard is busy: please try again."));
        }

        // ---- Loupe: a rounded dark card beside the pointer with the 9 x 9 pixels around it and the colour's code.

        Rectangle LoupeRect()
        {
            int box = Cell * LoupeCells, pad = Pz(6), w = box + pad * 2, h = box + pad * 2 + Pz(24);
            Rectangle m = curMon.IsEmpty ? Area : curMon;
            int x = cur.X + Pz(22), y = cur.Y + Pz(22);
            if (x + w > m.Right - Pz(4)) x = cur.X - Pz(22) - w;
            if (y + h > m.Bottom - Pz(4)) y = cur.Y - Pz(22) - h;
            return new Rectangle(x, y, w, h);
        }

        void PaintLoupe(Graphics g)
        {
            Rectangle r = LoupeRect();
            int pad = Pz(6), cell = Cell, box = cell * LoupeCells, half = LoupeCells / 2;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath bg = Theme.Round(r, Pz(12)))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(238, 24, 24, 26))) g.FillPath(b, bg);
                using (Pen edge = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) g.DrawPath(edge, bg);
            }
            Rectangle grid = new Rectangle(r.X + pad, r.Y + pad, box, box);
            using (GraphicsPath gp = Theme.Round(grid, Pz(8)))
            {
                Region keep = g.Clip;
                g.SetClip(gp, CombineMode.Intersect);
                g.SmoothingMode = SmoothingMode.None;
                using (SolidBrush pb = new SolidBrush(Color.Black))
                    for (int j = 0; j < LoupeCells; j++)
                        for (int i = 0; i < LoupeCells; i++)
                        {
                            pb.Color = PixelAt(cur.X + i - half, cur.Y + j - half);
                            g.FillRectangle(pb, grid.X + i * cell, grid.Y + j * cell, cell, cell);
                        }
                using (Pen line = new Pen(Color.FromArgb(46, 0, 0, 0), 1))
                    for (int i = 1; i < LoupeCells; i++)
                    {
                        g.DrawLine(line, grid.X + i * cell, grid.Y, grid.X + i * cell, grid.Bottom);
                        g.DrawLine(line, grid.X, grid.Y + i * cell, grid.Right, grid.Y + i * cell);
                    }
                g.Clip = keep;
                keep.Dispose();
            }
            Rectangle mid = new Rectangle(grid.X + half * cell, grid.Y + half * cell, cell, cell);
            using (Pen dark = new Pen(Color.FromArgb(210, 0, 0, 0), 3)) g.DrawRectangle(dark, mid);
            using (Pen white = new Pen(Color.White, 1)) g.DrawRectangle(white, mid);

            Color c = PixelAt(cur.X, cur.Y);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle sw = new Rectangle(grid.X + Pz(2), grid.Bottom + Pz(7), Pz(12), Pz(12));
            using (GraphicsPath sp = Theme.Round(sw, Pz(3)))
            {
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, sp);
                using (Pen edge = new Pen(Color.FromArgb(120, 255, 255, 255), 1)) g.DrawPath(edge, sp);
            }
            if (labelFont == null) return;
            Rectangle text = new Rectangle(sw.Right + Pz(7), grid.Bottom + Pz(2), grid.Right - sw.Right - Pz(7), Pz(22));
            TextKit.Draw(g, Hex(c), labelFont, text, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextKit.Crisp);
        }

        // ---- Ruler: a white line with a soft dark halo, ticks at both ends and its length in a pill.

        string RulerText()
        {
            int dx = Math.Abs(mTo.X - mFrom.X), dy = Math.Abs(mTo.Y - mFrom.Y);
            int len = (int)Math.Round(Math.Sqrt((double)dx * dx + (double)dy * dy));
            return len + " px  \u00B7  " + dx + " \u00D7 " + dy;
        }

        Rectangle RulerLabel()
        {
            if (labelFont == null) return Rectangle.Empty;
            string t = RulerText();
            int w = TextKit.Measure(t, labelFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextKit.Crisp).Width + Pz(18), h = Pz(22);
            Rectangle m = curMon.IsEmpty ? Area : curMon;
            int x = (mFrom.X + mTo.X) / 2 - w / 2, y = Math.Max(mFrom.Y, mTo.Y) + Pz(12);
            if (y + h > m.Bottom - Pz(4)) y = Math.Min(mFrom.Y, mTo.Y) - Pz(12) - h;
            x = Math.Max(m.Left + Pz(4), Math.Min(m.Right - w - Pz(4), x));
            return new Rectangle(x, y, w, h);
        }

        Rectangle RulerRect()
        {
            Rectangle r = Rectangle.FromLTRB(Math.Min(mFrom.X, mTo.X), Math.Min(mFrom.Y, mTo.Y), Math.Max(mFrom.X, mTo.X) + 1, Math.Max(mFrom.Y, mTo.Y) + 1);
            r = Rectangle.Inflate(r, Pz(12), Pz(12));
            Rectangle label = RulerLabel();
            return label.IsEmpty ? r : Rectangle.Union(r, Rectangle.Inflate(label, 2, 2));
        }

        void PaintRuler(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float dx = mTo.X - mFrom.X, dy = mTo.Y - mFrom.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
            float nx = len > 0 ? -dy / len : 0, ny = len > 0 ? dx / len : 1, tick = Pz(7);
            using (Pen halo = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(3f, Pz(4))))
            using (Pen line = new Pen(Color.White, Math.Max(1.5f, Pz(2))))
            {
                halo.StartCap = halo.EndCap = LineCap.Round;
                line.StartCap = line.EndCap = LineCap.Round;
                foreach (Pen p in new Pen[] { halo, line })
                {
                    g.DrawLine(p, mFrom, mTo);
                    g.DrawLine(p, mFrom.X - nx * tick, mFrom.Y - ny * tick, mFrom.X + nx * tick, mFrom.Y + ny * tick);
                    g.DrawLine(p, mTo.X - nx * tick, mTo.Y - ny * tick, mTo.X + nx * tick, mTo.Y + ny * tick);
                }
            }
            Rectangle label = RulerLabel();
            if (!label.IsEmpty) PaintLabel(g, label, RulerText());
        }
    }
}
