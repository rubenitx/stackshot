// Stackshot - Barra de herramientas y franja de ayuda del editor.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public class Bar : Control
    {
        public enum Kind { Tool, Swatch, Weight, Button, Gap }

        public class Item
        {
            public Kind Kind;
            public Tool Tool;
            public int Index;
            public Color Swatch;
            public string Glyph, Label, Alt, Tip;
            public Action Do;
            public bool On, Accent, Right, DragOut, ShowAlt;
            public bool Enabled = true;
            public Rectangle R;
        }

        public readonly List<Item> Items = new List<Item>();
        public float S = 1f;
        public event Action DragOut;
        int hot = -1, pressed = -1;
        Point downAt;
        bool dragFired;
        readonly ToolTip tips = new ToolTip();
        readonly Timer tipTimer = new Timer();

        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Dark;
            tipTimer.Interval = 450;
            tipTimer.Tick += delegate
            {
                tipTimer.Stop();
                if (hot < 0 || Items[hot].Tip == null) return;
                tips.Show(Items[hot].Tip, this, Items[hot].R.X, Height + P(2), 4000);
            };
        }

        int P(float v) { return (int)Math.Round(v * S); }

        public Item Add(Kind kind)
        {
            Item it = new Item();
            it.Kind = kind;
            Items.Add(it);
            return it;
        }

        int WidthOf(Item it)
        {
            switch (it.Kind)
            {
                case Kind.Tool: return P(36);
                case Kind.Swatch: return P(26);
                case Kind.Weight: return P(30);
                case Kind.Gap: return P(17);
                default:
                    if (it.Label == null) return P(36);
                    using (Font f = LabelFont())
                    {
                        int tw = TextRenderer.MeasureText(it.Label, f).Width;
                        if (it.Alt != null) tw = Math.Max(tw, TextRenderer.MeasureText(it.Alt, f).Width);
                        return P(12) + P(18) + P(7) + tw + P(10);
                    }
            }
        }

        Font LabelFont() { return new Font("Segoe UI Semibold", P(13), GraphicsUnit.Pixel); }

        // Coloca los botones: los normales desde la izquierda y los de acción pegados a la derecha.
        // Devuelve el ancho mínimo para que quepa todo.
        public int LayoutItems()
        {
            int left = P(10), right = Math.Max(Width, 1) - P(10), used = P(20);
            foreach (Item it in Items)
            {
                if (it.Right) continue;
                int w = WidthOf(it), h = it.Kind == Kind.Swatch ? P(26) : P(36);
                it.R = new Rectangle(left, (Height - h) / 2, w, h);
                left += w + P(2);
                used += w + P(2);
            }
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                Item it = Items[i];
                if (!it.Right) continue;
                int w = WidthOf(it), h = P(36);
                right -= w;
                it.R = new Rectangle(right, (Height - h) / 2, w, h);
                right -= P(6);
                used += w + P(6);
            }
            Invalidate();
            return used + P(24);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutItems();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            for (int i = 0; i < Items.Count; i++) PaintItem(g, Items[i], i == hot, i == pressed && i == hot);
        }

        static void Fill(Graphics g, Rectangle r, float radius, Color c)
        {
            using (GraphicsPath p = Theme.Round(r, radius))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        static void Glyph(Graphics g, string glyph, Rectangle r, Color c, int px)
        {
            using (Font f = new Font(Theme.IconFont, Math.Max(1, px), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                                         TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        void PaintItem(Graphics g, Item it, bool isHot, bool down)
        {
            Rectangle r = it.R;
            switch (it.Kind)
            {
                case Kind.Gap:
                    using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, r.X + r.Width / 2, r.Y + P(8), r.X + r.Width / 2, r.Bottom - P(8));
                    return;
                case Kind.Tool:
                {
                    if (it.On) Fill(g, r, P(8), Theme.Accent);
                    else if (isHot) Fill(g, r, P(8), down ? Theme.Border : Theme.ButtonHover);
                    Color fg = it.On ? Theme.Dark : Theme.Fg;
                    if (it.Glyph != null) Glyph(g, it.Glyph, r, fg, P(17));
                    else ToolIcon(g, it.Tool, r, fg, S);
                    return;
                }
                case Kind.Swatch:
                {
                    int d = P(18);
                    Rectangle c = new Rectangle(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
                    if (it.On || isHot)
                    {
                        Rectangle ring = Rectangle.Inflate(c, P(4), P(4));
                        using (Pen p = new Pen(it.On ? Theme.Fg : Theme.Border, Math.Max(1.5f, 2f * S))) g.DrawEllipse(p, ring);
                    }
                    using (SolidBrush b = new SolidBrush(it.Swatch)) g.FillEllipse(b, c);
                    if (it.Swatch.GetBrightness() > 0.9f) using (Pen p = new Pen(Theme.Border)) g.DrawEllipse(p, c);
                    return;
                }
                case Kind.Weight:
                {
                    if (it.On) Fill(g, r, P(8), Theme.ButtonHover);
                    else if (isHot) Fill(g, r, P(8), Theme.Button);
                    int d = it.Index == 0 ? P(5) : it.Index == 1 ? P(8) : P(12);
                    using (SolidBrush b = new SolidBrush(it.On ? Theme.Fg : Theme.Fg2))
                        g.FillEllipse(b, r.X + (r.Width - d) / 2f, r.Y + (r.Height - d) / 2f, d, d);
                    return;
                }
                default:
                {
                    Color bg = it.Accent ? (isHot ? Theme.Purple : Theme.Accent) : (isHot ? Theme.ButtonHover : Theme.Button);
                    if (down) bg = it.Accent ? Theme.Fg2 : Theme.Border;
                    if (!it.Enabled) bg = Theme.Button;
                    Fill(g, r, P(8), bg);
                    Color fg = it.Accent ? Theme.Dark : it.Enabled ? Theme.Fg : Theme.Muted;
                    if (it.On && !it.Accent && it.Enabled) { Fill(g, r, P(8), isHot ? Theme.Border : Theme.ButtonHover); fg = Theme.Accent; } // activado (el fondo)
                    if (it.Label == null) { Glyph(g, it.Glyph, r, fg, P(16)); return; }
                    Glyph(g, it.Glyph, new Rectangle(r.X + P(12), r.Y, P(18), r.Height), fg, P(15));
                    using (Font f = LabelFont())
                        TextRenderer.DrawText(g, it.ShowAlt ? it.Alt : it.Label, f, new Rectangle(r.X + P(37), r.Y, r.Width - P(41), r.Height), fg,
                                              TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    return;
                }
            }
        }

        // Iconos que no están en la fuente de Windows: la flecha afilada y el número.
        static void ToolIcon(Graphics g, Tool t, Rectangle r, Color c, float s)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            if (t == Tool.Arrow)
            {
                using (GraphicsPath p = Painter.ArrowPath(new PointF(cx - 7 * s, cy + 7 * s), new PointF(cx + 7.5f * s, cy - 7.5f * s), 2.3f * s))
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
                return;
            }
            float rad = 8.5f * s;
            using (Pen p = new Pen(c, 1.6f * s)) g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
            using (Font f = new Font("Segoe UI Semibold", 11f * s, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "1", f, new Rectangle((int)(cx - rad), (int)(cy - rad), (int)(rad * 2), (int)(rad * 2)), c,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Kind != Kind.Gap && Items[i].R.Contains(p)) return i;
            }
            return -1;
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            hot = h;
            Invalidate();
            bool usable = h >= 0 && Items[h].Enabled;
            Cursor = usable ? (Items[h].DragOut ? Cursors.SizeAll : Cursors.Hand) : Cursors.Default;
            tips.Hide(this);
            tipTimer.Stop();
            if (h >= 0 && Items[h].Tip != null) tipTimer.Start();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressed >= 0 && Items[pressed].DragOut && !dragFired && (e.Button & MouseButtons.Left) != 0)
            {
                Size d = SystemInformation.DragSize;
                if (Math.Abs(e.X - downAt.X) > d.Width || Math.Abs(e.Y - downAt.Y) > d.Height)
                {
                    dragFired = true;
                    pressed = -1;
                    tips.Hide(this);
                    Invalidate();
                    if (DragOut != null) DragOut();
                    return;
                }
            }
            SetHot(HitTest(e.Location));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || hot < 0 || !Items[hot].Enabled) return;
            pressed = hot;
            downAt = e.Location;
            dragFired = false;
            tips.Hide(this);
            tipTimer.Stop();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int p = pressed;
            pressed = -1;
            Invalidate();
            if (p >= 0 && !dragFired && HitTest(e.Location) == p && Items[p].Enabled && Items[p].Do != null) Items[p].Do();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHot(-1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tipTimer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Franja de abajo: qué se puede hacer ahora mismo (cambia con la herramienta y la selección) y el tamaño final.
    public class Hint : Control
    {
        public string LeftText = "", RightText = "";
        public double Progress = -1;   // 0-1: barra de progreso arriba (exportando un vídeo); -1 = sin barra
        public float S = 1f;

        public Hint()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Dark;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, 0, Width, 0);
            if (Progress >= 0)
            {
                int ph = Math.Max(2, (int)Math.Round(3 * S));
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), ph), Theme.Accent, Theme.Purple, 0f))
                    g.FillRectangle(b, 0, 0, (float)(Width * Math.Min(1, Progress)), ph);
            }
            int m = (int)Math.Round(12 * S);
            using (Font f = new Font("Segoe UI", 12 * S, GraphicsUnit.Pixel))
            {
                Size rs = TextRenderer.MeasureText(RightText, f);
                Rectangle rr = new Rectangle(Width - rs.Width - m, 0, rs.Width, Height);
                TextRenderer.DrawText(g, RightText, f, rr, Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                Rectangle lr = new Rectangle(m, 0, Math.Max(0, rr.X - 2 * m), Height);
                TextRenderer.DrawText(g, LeftText, f, lr, Theme.Fg2, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                                                      TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    // Ventana del editor: barra de herramientas arriba, lienzo en medio y ayuda abajo.
}
