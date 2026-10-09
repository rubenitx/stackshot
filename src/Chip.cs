// Stackshot - Scroll pills shown when there are more captures than fit.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    public class Chip : FloatWindow
    {
        readonly ShotStack owner;
        readonly int dir;          // -1 = top (newer), +1 = bottom (older)
        string label = "";
        bool wanted, pressed;
        readonly Tween hoverT = new Tween(0), pressT = new Tween(0);
        MI.BitmapSource shadow;
        Size shadowFor;

        public Chip(ShotStack owner, int dir)
        {
            this.owner = owner;
            this.dir = dir;
            Cursor = Cursors.Hand;
        }

        protected override bool PerPixel { get { return true; } }

        public void Set(Rectangle r, float scale, string text, bool visible)
        {
            if (!visible)
            {
                if (!wanted) return;
                wanted = false;
                alpha.Go(0, 150, 0, Ease.OutCubic, delegate { if (!wanted) Hide(); });
                Anim.Wake(this);
                return;
            }
            s = scale;
            Pad = P(14);
            bool changed = text != label || r.Size != body;
            label = text;
            SetSize(r.Size);
            if (changed) Redraw();
            // When the stack changes monitor, the pill fades out and reappears there instead of flying across.
            if (wanted && Visible && Math.Abs(r.X - x) > r.Width * 2)
            {
                alpha.Set(0);
                ApplyAlpha();
                JumpTo(r.X, r.Y - dir * P(10));
                alpha.Go(1, 180, 160, Ease.OutCubic, null);
            }
            if (!wanted)
            {
                wanted = true;
                if (!Visible) { alpha.Set(0); ApplyAlpha(); JumpTo(r.X, r.Y - dir * P(10)); }
                ShowQuiet();
                alpha.Go(1, 180, 60, Ease.OutCubic, null);
            }
            MoveTo(r.X, r.Y, 320, 0.8, 0);
        }

        public void Restyle()
        {
            shadow = null;
            Redraw();
        }

        protected override bool StepExtra(double now)
        {
            bool repaint = hoverT.Running || pressT.Running;
            hoverT.Step(now);
            pressT.Step(now);
            if (repaint) Redraw();
            return hoverT.Running || pressT.Running;
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            W.Rect b = new W.Rect(Pad, Pad, body.Width, body.Height);
            double R = b.Height / 2;
            if (shadow == null || shadowFor != new Size(w, h))
            {
                W.Rect sb = b;
                sb.Offset(0, P(3));
                shadow = Ink.Shadow(w, h, sb, R, P(10), Ds.Argb(Ds.Dark ? 0.5 : 0.25, 0, 0, 0));
                shadowFor = new Size(w, h);
            }
            dc.DrawImage(shadow, new W.Rect(0, 0, w, h));
            Palette pal = Ds.Brushes;
            M.Color bg = pal.Dark ? Ds.Rgb(44, 44, 46) : Ds.Rgb(255, 255, 255);
            M.Color over = pal.Dark ? Ds.Rgb(58, 58, 60) : Ds.Rgb(242, 242, 247);
            M.Color down = pal.Dark ? Ds.Rgb(72, 72, 74) : Ds.Rgb(229, 229, 234);
            Ink.Round(dc, Blend(Blend(bg, over, hoverT.Value), down, pressT.Value), b, R);
            Ink.Hairline(dc, pal.Hairline, b, R);
            M.FormattedText ft = Ink.Px(label, Ds.Semibold, P(12), pal.Label);
            double gs = P(14), total = gs + P(4) + ft.WidthIncludingTrailingWhitespace;
            double left = b.X + (b.Width - total) / 2;
            Glyph.Draw(dc, dir < 0 ? "up" : "down", left, b.Y + (b.Height - gs) / 2, gs, pal.Accent, P(2));
            dc.DrawText(ft, new W.Point(Math.Round(left + gs + P(4)), Math.Round(b.Y + (b.Height - ft.Height) / 2)));
        }

        static M.Color Blend(M.Color a, M.Color b, double t)
        {
            return M.Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hoverT.Go(1, 120, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverT.Go(0, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = true;
            pressT.Go(1, 70, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        // Pages when released over the pill (it can appear under a still cursor, so no hover state is needed).
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!pressed) return;
            pressed = false;
            pressT.Go(0, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
            if (e.Button == MouseButtons.Left && new Rectangle(Pad, Pad, body.Width, body.Height).Contains(e.Location)) owner.Page(dir);
        }

        // Hidden mid-press or mid-hover, no button-up or leave will follow: it must not come back pressed or lit.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible || (!pressed && pressT.Value == 0 && hoverT.Value == 0)) return;
            pressed = false;
            pressT.Set(0);
            hoverT.Set(0);
            Redraw();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            owner.Wheel(e.Delta);
        }
    }

}
