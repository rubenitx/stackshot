// Stackshot - Backdrop strip in the editor: presets, padding, corners, shadow and aspect ratio.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Stackshot
{
    // Owner-drawn. Changed fires on every change (live preview); Committed fires on mouse release (persist settings).
    public class BgPanel : Control
    {
        enum Kind { Swatch, Slider, Shadow, Ratio }

        class Item
        {
            public Kind Kind;
            public int Index;          // swatch: -1 = none; slider: 0 padding, 1 radius; ratio: 0-3
            public string Label;
            public Rectangle R;        // hit area
            public Rectangle Track;    // slider track
        }

        static readonly string[] Ratios = { "auto", "16:9", "4:3", "1:1" };
        static readonly string[] RatioNames = { "Auto", "16:9", "4:3", "1:1" };

        public float S = 1f;
        public bool On;
        public Settings Bg;
        public event Action Changed, Committed;
        public bool Sliding { get { return sliding >= 0; } }
        readonly List<Item> items = new List<Item>();
        int hot = -1, sliding = -1;
        bool twoRows;

        public BgPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = EdLook.C(Ds.Brushes.Window);
        }

        int P(float v) { return (int)Math.Round(v * S); }

        Font Small() { return Fonts.Get(EdLook.Text, 11.5f * S); }
        Font Regular() { return Fonts.Get(EdLook.Text, P(12)); }
        Font Semibold() { return Fonts.Get(EdLook.Semibold, P(12)); }

        // Height needed for this width: one row if everything fits, otherwise two.
        public int HeightFor(int width)
        {
            Arrange(width);
            return twoRows ? P(104) : P(58);
        }

        void Arrange(int width)
        {
            items.Clear();
            int sw = P(28), gap = P(6), m = P(12);
            int swatches = (Backdrop.Count + 1) * (sw + gap) - gap;
            int controls = P(120) * 2 + P(60) + 4 * P(44) + 3 * P(22);
            twoRows = m + swatches + P(22) + controls + m > width;
            int rowH = P(58);
            int x = m, y = (rowH - sw) / 2;
            for (int i = -1; i < Backdrop.Count; i++)
            {
                Item it = new Item();
                it.Kind = Kind.Swatch;
                it.Index = i;
                it.R = new Rectangle(x, y, sw, sw);
                items.Add(it);
                x += sw + gap;
            }
            int cy = 0;
            if (twoRows) { x = m; cy = P(46); }
            else x += P(22) - gap;
            for (int i = 0; i < 2; i++)
            {
                Item it = new Item();
                it.Kind = Kind.Slider;
                it.Index = i;
                it.Label = i == 0 ? "Margen" : "Esquinas";
                it.R = new Rectangle(x, cy + P(6), P(120), rowH - P(12));
                it.Track = new Rectangle(x + P(6), cy + P(38), P(108), P(4));
                items.Add(it);
                x += P(120) + P(22);
            }
            Item shadow = new Item();
            shadow.Kind = Kind.Shadow;
            shadow.Label = "Sombra";
            shadow.R = new Rectangle(x, cy + P(6), P(60), rowH - P(12));
            items.Add(shadow);
            x += P(60) + P(22);
            for (int i = 0; i < Ratios.Length; i++)
            {
                Item it = new Item();
                it.Kind = Kind.Ratio;
                it.Index = i;
                it.Label = RatioNames[i];
                it.R = new Rectangle(x + i * P(44), cy + P(26), P(44), P(26));
                items.Add(it);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Arrange(Width);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Palette pal = Ds.Brushes;
            g.Clear(EdLook.C(pal.Window));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(b, 0, Height - 1, Width, 1);
            for (int i = 0; i < items.Count; i++) PaintItem(g, pal, items[i], i == hot);
            // Locked while a video exports.
            if (!Enabled) using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, EdLook.C(pal.Window)))) g.FillRectangle(veil, 0, 0, Width, Height - 1);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { EndSlide(); hot = -1; }
            Invalidate();
        }

        // A slide also ends when the strip loses the mouse mid-drag (Esc or B hide it, a dialog, Alt+Tab): otherwise the
        // knob would keep following the pointer and the preview would stay in its fast, low-quality mode.
        void EndSlide()
        {
            if (sliding < 0) return;
            sliding = -1;
            Invalidate();
            Fire(Committed);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) EndSlide();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { EndSlide(); SetHot(-1); }
        }

        // White round knob with a soft shadow, as on macOS sliders and switches.
        void Knob(Graphics g, RectangleF k)
        {
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(Ds.Dark ? 60 : 26, 0, 0, 0)))
            {
                g.FillEllipse(sh, RectangleF.Inflate(new RectangleF(k.X, k.Y + 1.2f * S, k.Width, k.Height), 1.2f * S, 1.2f * S));
                g.FillEllipse(sh, k.X, k.Y + 0.6f * S, k.Width, k.Height);
            }
            g.FillEllipse(Brushes.White, k);
            using (Pen p = new Pen(Color.FromArgb(Ds.Dark ? 0 : 30, 0, 0, 0))) g.DrawEllipse(p, RectangleF.Inflate(k, -0.5f, -0.5f));
        }

        void PaintItem(Graphics g, Palette pal, Item it, bool isHot)
        {
            Color label = EdLook.C(On ? pal.Label2 : pal.Label3);
            switch (it.Kind)
            {
                case Kind.Swatch:
                {
                    bool sel = it.Index < 0 ? !On : On && Bg.BgPreset == it.Index;
                    Rectangle r = it.R;
                    float rad = P(7);
                    using (GraphicsPath p = Theme.Round(r, rad))
                    {
                        if (it.Index < 0)
                        {
                            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Control))) g.FillPath(b, p);
                            using (Pen pen = new Pen(EdLook.C(pal.Label3), Math.Max(1.2f, 1.5f * S)))
                            {
                                pen.StartCap = pen.EndCap = LineCap.Round;
                                g.DrawLine(pen, r.X + P(9), r.Bottom - P(9), r.Right - P(9), r.Y + P(9));
                            }
                        }
                        else
                        {
                            Region old = g.Clip;
                            g.SetClip(p, CombineMode.Intersect);
                            Backdrop.PaintSwatch(g, r, it.Index);
                            g.Clip = old;
                            old.Dispose();
                        }
                    }
                    EdLook.Hairline(g, r, rad, EdLook.C(Ds.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.1, 0, 0, 0)));
                    if (sel || isHot)
                    {
                        float pw = Math.Max(1.5f, 2f * S), gap = 2f * S;
                        RectangleF ring = RectangleF.Inflate(r, gap + pw / 2, gap + pw / 2);
                        using (GraphicsPath p = Theme.Round(ring, rad + gap + pw / 2))
                        using (Pen pen = new Pen(EdLook.C(sel ? pal.Accent : pal.Hairline), pw)) g.DrawPath(pen, p);
                    }
                    return;
                }
                case Kind.Slider:
                {
                    int value = it.Index == 0 ? Bg.BgPadding : Bg.BgRadius;
                    Font f = Small();
                    TextKit.Draw(g, it.Label, f, new Point(it.R.X + P(4), it.R.Y + P(4)), label, TextFormatFlags.NoPadding);
                    string v = value.ToString();
                    Size vs = TextKit.Measure(v, f, Size.Empty, TextFormatFlags.NoPadding);
                    TextKit.Draw(g, v, f, new Point(it.R.Right - P(4) - vs.Width, it.R.Y + P(4)), EdLook.C(pal.Label3), TextFormatFlags.NoPadding);
                    Rectangle t = it.Track;
                    using (GraphicsPath p = Theme.Round(t, t.Height / 2f))
                    using (SolidBrush b = new SolidBrush(EdLook.C(Ds.Dark ? pal.ControlHover : pal.ControlHover))) g.FillPath(b, p);
                    float fx = t.X + t.Width * value / 100f;
                    if (fx > t.X + 1)
                    {
                        using (GraphicsPath p = Theme.Round(new RectangleF(t.X, t.Y, fx - t.X, t.Height), t.Height / 2f))
                        using (SolidBrush b = new SolidBrush(EdLook.C(On ? pal.Accent : pal.Label3))) g.FillPath(b, p);
                    }
                    float d = 16 * S * (isHot || sliding >= 0 && items[sliding] == it ? 1.08f : 1f);
                    Knob(g, new RectangleF(fx - d / 2, t.Y + t.Height / 2f - d / 2, d, d));
                    return;
                }
                case Kind.Shadow:
                {
                    TextKit.Draw(g, it.Label, Small(), new Point(it.R.X + P(4), it.R.Y + P(4)), label, TextFormatFlags.NoPadding);
                    RectangleF track = new RectangleF(it.R.X + P(4), it.R.Y + P(24), P(34), P(20));
                    bool v = Bg.BgShadow;
                    Color tc = v ? EdLook.C(On ? pal.Accent : pal.Label3) : EdLook.C(isHot ? pal.Hairline : pal.ControlHover);
                    EdLook.Fill(g, track, track.Height / 2f, tc);
                    float d = track.Height - 2 * 2f * S, x = v ? track.Right - 2f * S - d : track.X + 2f * S;
                    Knob(g, new RectangleF(x, track.Y + 2f * S, d, d));
                    return;
                }
                default:
                {
                    if (it.Index == 0)
                    {
                        TextKit.Draw(g, "Proporci\u00F3n", Small(), new Point(it.R.X + P(4), it.R.Y - P(16)), label, TextFormatFlags.NoPadding);
                        Rectangle all = new Rectangle(it.R.X, it.R.Y, it.R.Width * Ratios.Length, it.R.Height);
                        EdLook.Fill(g, all, P(7), EdLook.C(pal.Control));
                    }
                    bool sel = Bg.BgRatio == Ratios[it.Index];
                    RectangleF r = RectangleF.Inflate(it.R, -2f * S, -2f * S);
                    if (sel)
                    {
                        // Raised segment: white on light, lighter gray on dark.
                        if (!Ds.Dark) EdLook.Fill(g, new RectangleF(r.X, r.Y + 0.75f * S, r.Width, r.Height), P(5), Color.FromArgb(22, 0, 0, 0));
                        EdLook.Fill(g, r, P(5), Ds.Dark ? Color.FromArgb(On ? 92 : 64, 255, 255, 255) : Color.FromArgb(On ? 255 : 200, 255, 255, 255));
                    }
                    else if (isHot) EdLook.Fill(g, r, P(5), EdLook.C(pal.Control));
                    TextKit.Draw(g, it.Label, sel ? Semibold() : Regular(), it.R, EdLook.C(sel && On ? pal.Label : pal.Label2),
                                 TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    return;
                }
            }
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (!it.R.Contains(p)) continue;
                // A slider answers on its track, not on its label (a click there would jump the value).
                if (it.Kind == Kind.Slider && p.Y < it.Track.Y - P(12)) return -1;
                return i;
            }
            return -1;
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            InvalidateItem(hot);
            hot = h;
            InvalidateItem(hot);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h >= 0 && items[h].Kind == Kind.Swatch) EdTip.Schedule(this, items[h].R, items[h].Index < 0 ? "Sin fondo" : Backdrop.Name(items[h].Index));
            else EdTip.Cancel();
        }

        void InvalidateItem(int i)
        {
            if (i < 0 || i >= items.Count) return;
            Rectangle r = items[i].R;
            r.Inflate(P(6), P(6));
            Invalidate(r);
        }

        void Fire(Action a) { if (a != null) a(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || hot < 0) return;
            Item it = items[hot];
            EdTip.Cancel();
            switch (it.Kind)
            {
                case Kind.Swatch:
                    if (it.Index < 0) On = false;
                    else { On = true; Bg.BgPreset = it.Index; }
                    break;
                case Kind.Slider:
                {
                    bool wasOn = On;
                    sliding = hot;
                    On = true;
                    if (!Slide(e.X) && !wasOn) Fire(Changed); // the backdrop comes on even if the value stays
                    Invalidate();
                    return; // Committed on mouse up
                }
                case Kind.Shadow:
                    Bg.BgShadow = !Bg.BgShadow;
                    On = true;
                    break;
                case Kind.Ratio:
                    Bg.BgRatio = Ratios[it.Index];
                    On = true;
                    break;
            }
            Invalidate();
            Fire(Changed);
            Fire(Committed);
        }

        // Returns whether the value changed.
        bool Slide(int x)
        {
            Item it = items[sliding];
            int v = (int)Math.Round(Math.Max(0, Math.Min(100, (x - it.Track.X) * 100.0 / Math.Max(1, it.Track.Width))));
            if (it.Index == 0)
            {
                if (Bg.BgPadding == v) return false;
                Bg.BgPadding = v;
            }
            else
            {
                if (Bg.BgRadius == v) return false;
                Bg.BgRadius = v;
            }
            Invalidate();
            Fire(Changed);
            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (sliding >= 0) { Slide(e.X); return; }
            SetHot(HitTest(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndSlide();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (sliding < 0) SetHot(-1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EdTip.Cancel();
            }
            base.Dispose(disposing);
        }
    }
}
