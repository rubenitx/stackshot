// Stackshot - Franja de "Fondo" del editor: muestras de fondos, margen, esquinas, sombra y proporción.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Stackshot
{
    // Se dibuja a mano, como la barra. Cada cambio avisa al momento (Changed, para la vista previa en vivo) y al
    // soltar el ratón (Committed, para guardarlo en los ajustes).
    public class BgPanel : Control
    {
        enum Kind { Swatch, Slider, Shadow, Ratio }

        class Item
        {
            public Kind Kind;
            public int Index;          // muestra: -1 = sin fondo; deslizador: 0 margen, 1 esquinas; proporción: 0-3
            public string Label;
            public Rectangle R;        // zona que responde al ratón
            public Rectangle Track;    // deslizadores: la pista
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
        readonly ToolTip tips = new ToolTip();
        readonly Timer tipTimer = new Timer();
        bool twoRows;

        public BgPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Dark;
            tipTimer.Interval = 350;
            tipTimer.Tick += delegate
            {
                tipTimer.Stop();
                if (hot < 0 || items[hot].Kind != Kind.Swatch) return;
                Item it = items[hot];
                tips.Show(it.Index < 0 ? "Sin fondo" : Backdrop.Name(it.Index), this, it.R.X, it.R.Bottom + P(4), 2500);
            };
        }

        int P(float v) { return (int)Math.Round(v * S); }

        Font Small() { return new Font("Segoe UI", P(11), GraphicsUnit.Pixel); }
        Font Semibold() { return new Font("Segoe UI Semibold", P(12), GraphicsUnit.Pixel); }

        // Alto que necesita para este ancho: una fila si cabe todo y, si no, dos (fondos arriba, ajustes abajo).
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

        // ------------------------------------------------------------ Dibujo

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            for (int i = 0; i < items.Count; i++) PaintItem(g, items[i], i == hot);
        }

        void PaintItem(Graphics g, Item it, bool isHot)
        {
            switch (it.Kind)
            {
                case Kind.Swatch:
                {
                    bool sel = it.Index < 0 ? !On : On && Bg.BgPreset == it.Index;
                    Rectangle r = it.R;
                    using (GraphicsPath p = Theme.Round(r, P(8)))
                    {
                        if (it.Index < 0)
                        {
                            using (SolidBrush b = new SolidBrush(Theme.Button)) g.FillPath(b, p);
                            using (Pen pen = new Pen(Theme.Muted, Math.Max(1.5f, 1.6f * S)))
                                g.DrawLine(pen, r.X + P(8), r.Bottom - P(8), r.Right - P(8), r.Y + P(8));
                        }
                        else
                        {
                            Region old = g.Clip;
                            g.SetClip(p, CombineMode.Intersect);
                            Backdrop.PaintSwatch(g, r, it.Index);
                            g.Clip = old;
                            old.Dispose();
                            using (Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawPath(pen, p);
                        }
                    }
                    if (sel || isHot)
                    {
                        Rectangle ring = Rectangle.Inflate(r, P(3), P(3));
                        using (GraphicsPath p = Theme.Round(ring, P(10)))
                        using (Pen pen = new Pen(sel ? Theme.Accent : Theme.Border, Math.Max(1.5f, 2f * S))) g.DrawPath(pen, p);
                    }
                    return;
                }
                case Kind.Slider:
                {
                    int value = it.Index == 0 ? Bg.BgPadding : Bg.BgRadius;
                    Color fg = On ? Theme.Fg2 : Theme.Muted;
                    using (Font f = Small())
                    {
                        TextRenderer.DrawText(g, it.Label, f, new Point(it.R.X + P(4), it.R.Y + P(4)), fg, TextFormatFlags.NoPadding);
                        string v = value.ToString();
                        Size vs = TextRenderer.MeasureText(v, f, Size.Empty, TextFormatFlags.NoPadding);
                        TextRenderer.DrawText(g, v, f, new Point(it.R.Right - P(4) - vs.Width, it.R.Y + P(4)), Theme.Muted, TextFormatFlags.NoPadding);
                    }
                    Rectangle t = it.Track;
                    using (GraphicsPath p = Theme.Round(t, t.Height / 2f))
                    using (SolidBrush b = new SolidBrush(Theme.Button)) g.FillPath(b, p);
                    float fx = t.X + t.Width * value / 100f;
                    if (fx > t.X + 1)
                    {
                        using (GraphicsPath p = Theme.Round(new RectangleF(t.X, t.Y, fx - t.X, t.Height), t.Height / 2f))
                        using (SolidBrush b = new SolidBrush(On ? Theme.Accent : Theme.Border)) g.FillPath(b, p);
                    }
                    float d = P(isHot || sliding >= 0 && items[sliding] == it ? 16 : 14);
                    RectangleF knob = new RectangleF(fx - d / 2, t.Y + t.Height / 2f - d / 2, d, d);
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(sh, knob.X, knob.Y + P(1), d, d);
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, knob);
                    return;
                }
                case Kind.Shadow:
                {
                    using (Font f = Small())
                        TextRenderer.DrawText(g, it.Label, f, new Point(it.R.X + P(4), it.R.Y + P(4)), On ? Theme.Fg2 : Theme.Muted, TextFormatFlags.NoPadding);
                    RectangleF track = new RectangleF(it.R.X + P(4), it.R.Y + P(22), P(38), P(22));
                    bool v = Bg.BgShadow;
                    using (GraphicsPath p = Theme.Round(track, track.Height / 2f))
                    using (SolidBrush b = new SolidBrush(v ? (On ? Theme.Accent : Theme.Border) : isHot ? Theme.ButtonHover : Theme.Button)) g.FillPath(b, p);
                    float d = track.Height - P(6), x = v ? track.Right - P(3) - d : track.X + P(3);
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x, track.Y + P(3), d, d);
                    return;
                }
                default:
                {
                    if (it.Index == 0)
                    {
                        using (Font f = Small())
                            TextRenderer.DrawText(g, "Proporci\u00F3n", f, new Point(it.R.X + P(4), it.R.Y - P(20)), On ? Theme.Fg2 : Theme.Muted, TextFormatFlags.NoPadding);
                        Rectangle all = new Rectangle(it.R.X, it.R.Y, it.R.Width * Ratios.Length, it.R.Height);
                        using (GraphicsPath p = Theme.Round(all, P(8)))
                        using (SolidBrush b = new SolidBrush(Theme.Button)) g.FillPath(b, p);
                    }
                    bool sel = Bg.BgRatio == Ratios[it.Index];
                    Rectangle r = Rectangle.Inflate(it.R, -P(2), -P(2));
                    if (sel || isHot)
                    {
                        using (GraphicsPath p = Theme.Round(r, P(6)))
                        using (SolidBrush b = new SolidBrush(sel ? (On ? Theme.Accent : Theme.Border) : Theme.ButtonHover)) g.FillPath(b, p);
                    }
                    using (Font f = Semibold())
                        TextRenderer.DrawText(g, it.Label, f, it.R, sel && On ? Theme.Dark : Theme.Fg,
                                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    return;
                }
            }
        }

        // ------------------------------------------------------------ Ratón

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].R.Contains(p)) return i;
            }
            return -1;
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            hot = h;
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            tips.Hide(this);
            tipTimer.Stop();
            if (h >= 0 && items[h].Kind == Kind.Swatch) tipTimer.Start();
            Invalidate();
        }

        void Fire(Action a) { if (a != null) a(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || hot < 0) return;
            Item it = items[hot];
            tips.Hide(this);
            switch (it.Kind)
            {
                case Kind.Swatch:
                    if (it.Index < 0) On = false;
                    else { On = true; Bg.BgPreset = it.Index; }
                    break;
                case Kind.Slider:
                    sliding = hot;
                    On = true;
                    Slide(e.X);
                    Invalidate();
                    return; // Committed al soltar
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

        void Slide(int x)
        {
            Item it = items[sliding];
            int v = (int)Math.Round(Math.Max(0, Math.Min(100, (x - it.Track.X) * 100.0 / Math.Max(1, it.Track.Width))));
            if (it.Index == 0)
            {
                if (Bg.BgPadding == v) return;
                Bg.BgPadding = v;
            }
            else
            {
                if (Bg.BgRadius == v) return;
                Bg.BgRadius = v;
            }
            Invalidate();
            Fire(Changed);
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
            if (sliding < 0) return;
            sliding = -1;
            Invalidate();
            Fire(Committed);
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
                tipTimer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
