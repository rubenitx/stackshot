// Stackshot - Scroll pills shown when there are more captures than fit.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Stackshot
{
    public class Chip : FloatWindow
    {
        readonly ShotStack owner;
        readonly int dir;          // -1 = top (newer), +1 = bottom (older)
        string label = "";
        bool wanted, hover;
        readonly Tween hoverT = new Tween(0);

        public Chip(ShotStack owner, int dir)
        {
            this.owner = owner;
            this.dir = dir;
            BackColor = Theme.Dark;
            Cursor = Cursors.Hand;
        }

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
            if (text != label) { label = text; Invalidate(); }
            SetSize(r.Size);
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

        protected override bool StepExtra(double now)
        {
            bool repaint = hoverT.Running;
            hoverT.Step(now);
            SetBorder(Mix(Theme.Border, Theme.Accent, hoverT.Value));
            if (repaint) Invalidate();
            return hoverT.Running;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Mix(Theme.Dark, Theme.ButtonHover, hoverT.Value));
            Rectangle r = ClientRectangle;
            Font f = Fonts.Get("Segoe UI Semibold", P(12));
            Size ts = TextRenderer.MeasureText(label, f);
            int gw = P(16), total = gw + P(6) + ts.Width;
            int left = (r.Width - total) / 2;
            DrawGlyph(g, dir < 0 ? "\uE70E" : "\uE70D", new Rectangle(left, 0, gw, r.Height), Theme.Accent, P(11));
            TextRenderer.DrawText(g, label, f, new Rectangle(left + gw + P(6), 0, ts.Width + P(2), r.Height), Theme.Fg,
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            hoverT.Go(1, 120, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            hoverT.Go(0, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && hover) owner.Page(dir);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            owner.Wheel(e.Delta);
        }
    }

}
