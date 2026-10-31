// Stackshot - The editor's backdrop sidebar: gradients, pictures, the capture blurred and plain colours, then margin,
// inset, shadow, corners, alignment and proportion.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Stackshot
{
    // Owner-drawn and scrolled with the wheel. Changed fires on every change (live preview); Committed fires on mouse
    // release (persist settings).
    public partial class BgPanel : Control
    {
        enum Kind { None, Header, More, Swatch, Add, Blur, Plain, Picker, Slider, Check, Align, Ratio, Styles, Keep }

        class Item
        {
            public Kind Kind;
            public int Index;          // swatch: preset; blur: variant; plain: RGB; slider: 0 margin, 1 corners, 2 shadow, 3 inset; align: 0-8
            public string Label;
            public Rectangle R;        // hit area, in content coordinates (before scrolling)
            public Rectangle Track;    // slider track
        }

        public const float Wide = 252;     // width at 100 %

        static readonly string[] Ratios = { "auto", "16:9", "4:3", "1:1" };
        static readonly string[] RatioNames = { "Auto", "16:9", "4:3", "1:1" };
        static readonly int[] Plains =
        {
            0x000000, 0xFFFFFF, 0xE5383B, 0xF3722C, 0xF9C74F, 0x2B9348, 0x1D72E8, 0x7B2CBF, 0xE84393,
            0x2C2C2E, 0xE5E5EA, 0xF6A6A1, 0xF9C58D, 0xFBE3A0, 0xA8E6B4, 0xA9CBF5, 0xD3B8F2
        };
        static readonly string[] PlainNamesEs =
        {
            "Negro", "Blanco", "Rojo", "Naranja", "Amarillo", "Verde", "Azul", "Morado", "Rosa",
            "Gris oscuro", "Gris claro", "Rojo suave", "Naranja suave", "Amarillo suave", "Verde suave", "Azul suave", "Morado suave"
        };
        static readonly string[] PlainNamesEn =
        {
            "Black", "White", "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Pink",
            "Dark gray", "Light gray", "Soft red", "Soft orange", "Soft yellow", "Soft green", "Soft blue", "Soft purple"
        };
        static string[] PlainNames { get { return I18n.IsSpanish ? PlainNamesEs : PlainNamesEn; } }
        static readonly string[] BlurNamesEs = { "Desenfocada", "Desenfocada y oscura", "Desenfocada y clara" };
        static readonly string[] BlurNamesEn = { "Blurred", "Blurred & dark", "Blurred & light" };
        static string[] BlurNames { get { return I18n.IsSpanish ? BlurNamesEs : BlurNamesEn; } }
        static readonly string[] AlignNamesEs =
        {
            "Arriba a la izquierda", "Arriba", "Arriba a la derecha", "A la izquierda", "Centrada", "A la derecha",
            "Abajo a la izquierda", "Abajo", "Abajo a la derecha"
        };
        static readonly string[] AlignNamesEn =
        {
            "Top left", "Top", "Top right", "Left", "Centered", "Right",
            "Bottom left", "Bottom", "Bottom right"
        };
        static string[] AlignNames { get { return I18n.IsSpanish ? AlignNamesEs : AlignNamesEn; } }
        static readonly string[] SliderNamesEs = { "Margen", "Esquinas", "Sombra", "Inserci\u00F3n" };
        static readonly string[] SliderNamesEn = { "Padding", "Corners", "Shadow", "Border inset" };
        static string[] SliderNames { get { return I18n.IsSpanish ? SliderNamesEs : SliderNamesEn; } }
        static bool allGradients = true;   // "Ver menos" folds them to one row, for the rest of the session

        public float S = 1f;
        public bool On;
        public Settings Bg;
        // A video: nothing is taken from its frames, so no blurred backdrop and no inset.
        public bool Video;
        // The capture shrunk and blurred, for the blurred swatches (the editor makes it once).
        public Func<Bitmap> BlurThumb;
        public event Action Changed, Committed;
        public bool Sliding { get { return sliding >= 0; } }
        readonly List<Item> items = new List<Item>();
        int hot = -1, sliding = -1, scroll, contentH;
        int barGrab = -1;   // the scroll thumb being dragged: where it was grabbed

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

        // The kind of backdrop shown as chosen: a video draws a blurred one as its gradient.
        int KindNow { get { return Video && Bg.BgKind == 2 ? 0 : Bg.BgKind; } }

        int ShadowNow { get { return Backdrop.ShadowLevel(Bg); } }

        int ValueOf(Item it)
        {
            switch (it.Index)
            {
                case 0: return Bg.BgPadding;
                case 1: return Bg.BgRadius;
                case 2: return ShadowNow;
                default: return Bg.BgInset;
            }
        }

        // Everything placed again (the width, the scale or what is listed changed).
        public void Relayout()
        {
            hot = -1;   // the list may be shorter now
            Arrange(Width);
            ClampScroll();
            Invalidate();
        }

        Item Add(Kind kind, int index, string label, Rectangle r)
        {
            Item it = new Item();
            it.Kind = kind;
            it.Index = index;
            it.Label = label;
            it.R = r;
            items.Add(it);
            return it;
        }

        void Arrange(int width)
        {
            items.Clear();
            if (Bg == null) return;
            int m = P(14), w = Math.Max(P(120), width - 2 * m), y = P(14);
            // Saved styles: a menu to bring one back and a button that keeps the one on screen.
            Add(Kind.Styles, 0, I18n.T("Estilos guardados", "Saved styles"), new Rectangle(m, y, w - P(38), P(28)));
            Add(Kind.Keep, 0, I18n.T("Guardar este estilo", "Save this style"), new Rectangle(m + w - P(30), y, P(30), P(28)));
            y += P(28) + P(10);
            Add(Kind.None, 0, I18n.T("Ninguno", "None"), new Rectangle(m, y, w, P(30)));
            y += P(30) + P(18);

            // Gradients, five to a row.
            int gap = P(8), sw = (w - 4 * gap) / 5;
            Add(Kind.Header, 0, I18n.T("Degradados", "Gradients"), new Rectangle(m, y, w, P(16)));
            Add(Kind.More, 0, allGradients ? I18n.T("Ver menos", "Show less") : I18n.T("Ver m\u00E1s", "Show more"), new Rectangle(m + w - P(84), y - P(3), P(84), P(22)));
            y += P(26);
            int col = 0, shown = 0;
            for (int i = 0; i < Backdrop.Count; i++)
            {
                if (!Backdrop.IsGradient(i)) continue;
                if (!allGradients && shown == 5) break;
                Add(Kind.Swatch, i, Backdrop.Name(i), new Rectangle(m + col * (sw + gap), y, sw, sw));
                shown++;
                if (++col == 5) { col = 0; y += sw + gap; }
            }
            if (col > 0) y += sw + gap;
            y += P(10);

            // The desktop's wallpaper and the user's own pictures, then the button that adds one.
            Add(Kind.Header, 1, I18n.T("Fondos de pantalla", "Wallpapers"), new Rectangle(m, y, w, P(16)));
            y += P(26);
            col = 0;
            for (int i = 0; i < Backdrop.Count; i++)
            {
                if (Backdrop.IsGradient(i)) continue;
                Add(Kind.Swatch, i, Backdrop.Name(i), new Rectangle(m + col * (sw + gap), y, sw, sw));
                if (++col == 5) { col = 0; y += sw + gap; }
            }
            if (Backdrop.CustomCount < Backdrop.MaxCustom)
            {
                Add(Kind.Add, 0, I18n.T("A\u00F1adir una imagen\u2026", "Add an image\u2026"), new Rectangle(m + col * (sw + gap), y, sw, sw));
                col++;
            }
            if (col > 0) y += sw + gap;
            y += P(10);

            if (!Video)
            {
                Add(Kind.Header, 2, I18n.T("Desenfocado", "Blur"), new Rectangle(m, y, w, P(16)));
                y += P(26);
                for (int i = 0; i < 3; i++) Add(Kind.Blur, i, BlurNames[i], new Rectangle(m + i * (sw + gap), y, sw, sw));
                y += sw + gap + P(10);
            }

            // Plain colours: two rows of nine, the last one opens the colour picker.
            Add(Kind.Header, 3, I18n.T("Color liso", "Solid color"), new Rectangle(m, y, w, P(16)));
            y += P(26);
            int d = P(19), dg = (w - 9 * d) / 8;
            for (int i = 0; i < 18; i++)
            {
                Rectangle r = new Rectangle(m + (i % 9) * (d + dg), y + (i / 9) * (d + P(9)), d, d);
                if (i < Plains.Length) Add(Kind.Plain, Plains[i], PlainNames[i], r);
                else Add(Kind.Picker, 0, I18n.T("Otro color\u2026", "Other color\u2026"), r);
            }
            y += 2 * d + P(9) + P(14);
            Add(Kind.Header, -1, null, new Rectangle(m, y, w, 1));   // a dividing line
            y += P(16);

            int half = (w - P(18)) / 2, right = m + w - half;
            Slider(0, m, y, w);
            y += P(48);
            if (!Video)
            {
                Slider(3, m, y, half);
                Add(Kind.Check, 0, I18n.T("Auto-equilibrar", "Auto-balance"), new Rectangle(right, y + P(16), half, P(24)));
                y += P(48);
            }
            Slider(2, m, y, half);
            Slider(1, right, y, half);
            y += P(52);

            Add(Kind.Header, 4, I18n.T("Alineaci\u00F3n", "Alignment"), new Rectangle(m, y, half, P(16)));
            Add(Kind.Header, 5, I18n.T("Proporci\u00F3n", "Aspect ratio"), new Rectangle(right, y, half, P(16)));
            y += P(24);
            int cw = P(24), ch = P(18), cg = P(7);
            for (int i = 0; i < 9; i++) Add(Kind.Align, i, AlignNames[i], new Rectangle(m + (i % 3) * (cw + cg), y + (i / 3) * (ch + cg), cw, ch));
            Add(Kind.Ratio, 0, I18n.T("Proporci\u00F3n", "Aspect ratio"), new Rectangle(right, y, half, P(28)));
            y += 3 * ch + 2 * cg + P(18);
            contentH = y;
        }

        void Slider(int index, int x, int y, int w)
        {
            Item it = Add(Kind.Slider, index, SliderNames[index], new Rectangle(x, y, w, P(40)));
            it.Track = new Rectangle(x + P(8), y + P(28), w - P(16), P(4));
        }

        void ClampScroll()
        {
            scroll = Math.Max(0, Math.Min(scroll, contentH - Height));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Relayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Palette pal = Ds.Brushes;
            g.Clear(EdLook.C(pal.Window));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            if (items.Count == 0) Arrange(Width);
            GraphicsState st = g.Save();
            g.TranslateTransform(0, -scroll);
            Rectangle seen = new Rectangle(e.ClipRectangle.X, e.ClipRectangle.Y + scroll, e.ClipRectangle.Width, e.ClipRectangle.Height);
            for (int i = 0; i < items.Count; i++)
                if (Rectangle.Inflate(items[i].R, P(8), P(8)).IntersectsWith(seen)) PaintItem(g, pal, items[i], i == hot);
            g.Restore(st);
            PaintBar(g, pal);
            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(b, Width - 1, 0, 1, Height);
            // Locked while a video exports.
            if (!Enabled) using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, EdLook.C(pal.Window)))) g.FillRectangle(veil, 0, 0, Width - 1, Height);
        }

        Rectangle BarThumb()
        {
            if (contentH <= Height || Height <= 0) return Rectangle.Empty;
            int track = Height - P(8), len = Math.Max(P(28), track * Height / contentH);
            int top = P(4) + (track - len) * scroll / Math.Max(1, contentH - Height);
            return new Rectangle(Width - P(8), top, P(4), len);
        }

        void PaintBar(Graphics g, Palette pal)
        {
            Rectangle t = BarThumb();
            if (t.IsEmpty) return;
            EdLook.Fill(g, t, t.Width / 2f, EdLook.C(barGrab >= 0 ? pal.Label3 : pal.Hairline));
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { EndSlide(); hot = -1; }
            Invalidate();
        }

        // A slide also ends when the panel loses the mouse mid-drag (Esc or B hide it, a dialog, Alt+Tab): otherwise the
        // knob would keep following the pointer and the preview would stay in its fast, low-quality mode.
        void EndSlide()
        {
            barGrab = -1;
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
            if (Ds.Brushes.High) { EdHc.Knob(g, k); return; }
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(Ds.Dark ? 60 : 26, 0, 0, 0)))
            {
                g.FillEllipse(sh, RectangleF.Inflate(new RectangleF(k.X, k.Y + 1.2f * S, k.Width, k.Height), 1.2f * S, 1.2f * S));
                g.FillEllipse(sh, k.X, k.Y + 0.6f * S, k.Width, k.Height);
            }
            g.FillEllipse(Brushes.White, k);
            using (Pen p = new Pen(Color.FromArgb(Ds.Dark ? 0 : 30, 0, 0, 0))) g.DrawEllipse(p, RectangleF.Inflate(k, -0.5f, -0.5f));
        }

        // The accent ring of what is chosen (or a soft one under the pointer).
        void Ring(Graphics g, Palette pal, RectangleF r, float radius, bool sel, bool isHot, bool round)
        {
            if (!sel && !isHot) return;
            float pw = Math.Max(1.5f, 2f * S), gap = 2f * S;
            RectangleF ring = RectangleF.Inflate(r, gap + pw / 2, gap + pw / 2);
            using (Pen pen = new Pen(EdLook.C(sel ? pal.Accent : pal.Hairline), pw))
            {
                if (round) g.DrawEllipse(pen, ring);
                else using (GraphicsPath p = Theme.Round(ring, radius + gap + pw / 2)) g.DrawPath(pen, p);
            }
        }

        Color Edge(Palette pal) { return EdLook.C(pal.High ? pal.Hairline : Ds.Dark ? Ds.Argb(0.16, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)); }

        void PaintItem(Graphics g, Palette pal, Item it, bool isHot)
        {
            Color label = EdLook.C(pal.Label2);
            float rad = P(8);
            switch (it.Kind)
            {
                case Kind.None:
                {
                    bool sel = !On;
                    Color ink = EdLook.C(pal.Label);
                    if (pal.High) ink = EdHc.Segment(g, it.R, P(7), sel, isHot, true);
                    else
                    {
                        EdLook.Fill(g, it.R, P(7), EdLook.C(isHot ? pal.ControlHover : pal.Control));
                        if (sel)
                            using (GraphicsPath p = Theme.Round(RectangleF.Inflate(it.R, -1f * S, -1f * S), P(7) - S))
                            using (Pen pen = new Pen(EdLook.C(pal.Accent), Math.Max(1.5f, 2f * S))) g.DrawPath(pen, p);
                    }
                    TextKit.Draw(g, it.Label, sel ? Semibold() : Regular(), it.R, ink,
                                 TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    return;
                }
                case Kind.Header:
                    if (it.Label == null)
                    {
                        using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(b, it.R.X, it.R.Y, it.R.Width, Math.Max(1, P(1)));
                        return;
                    }
                    TextKit.Draw(g, it.Label, Small(), new Point(it.R.X, it.R.Y), label, TextFormatFlags.NoPadding);
                    return;
                case Kind.More:
                {
                    Font f = Small();
                    Size ts = TextKit.Measure(it.Label, f, Size.Empty, TextFormatFlags.NoPadding);
                    int cx = it.R.Right - P(6), cy = it.R.Y + it.R.Height / 2;
                    Color ink = EdLook.C(isHot ? pal.Label : pal.Label3);
                    TextKit.Draw(g, it.Label, f, new Point(cx - P(12) - ts.Width, cy - ts.Height / 2), ink, TextFormatFlags.NoPadding);
                    using (Pen pen = new Pen(ink, Math.Max(1.2f, 1.4f * S)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        float h = 2.5f * S, wv = 4f * S;
                        if (allGradients) g.DrawLines(pen, new PointF[] { new PointF(cx - wv, cy + h / 2), new PointF(cx - wv / 2, cy - h / 2), new PointF(cx, cy + h / 2) });
                        else g.DrawLines(pen, new PointF[] { new PointF(cx - wv, cy - h / 2), new PointF(cx - wv / 2, cy + h / 2), new PointF(cx, cy - h / 2) });
                    }
                    return;
                }
                case Kind.Swatch:
                case Kind.Blur:
                {
                    bool sel = On && (it.Kind == Kind.Swatch ? KindNow == 0 && Bg.BgPreset == it.Index : KindNow == 2 && Bg.BgBlur == it.Index);
                    using (GraphicsPath p = Theme.Round(it.R, rad))
                    {
                        Region old = g.Clip;
                        g.SetClip(p, CombineMode.Intersect);
                        Bitmap thumb = it.Kind == Kind.Blur && BlurThumb != null ? BlurThumb() : null;
                        if (it.Kind == Kind.Swatch) Backdrop.PaintSwatch(g, it.R, it.Index);
                        else if (thumb != null) Backdrop.PaintBlur(g, it.R, thumb, it.Index);
                        else using (SolidBrush b = new SolidBrush(EdLook.C(pal.Control))) g.FillRectangle(b, it.R);
                        g.Clip = old;
                        old.Dispose();
                    }
                    EdLook.Hairline(g, it.R, rad, Edge(pal));
                    Ring(g, pal, it.R, rad, sel, isHot, false);
                    return;
                }
                case Kind.Add:
                {
                    using (GraphicsPath p = Theme.Round(RectangleF.Inflate(it.R, -0.5f, -0.5f), rad))
                    using (Pen pen = new Pen(EdLook.C(isHot ? pal.Label2 : pal.Label3), Math.Max(1f, 1.2f * S)))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawPath(pen, p);
                    }
                    using (Pen pen = new Pen(EdLook.C(isHot ? pal.Label : pal.Label2), Math.Max(1.4f, 1.6f * S)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        float cx = it.R.X + it.R.Width / 2f, cy = it.R.Y + it.R.Height / 2f, a = 5.5f * S;
                        g.DrawLine(pen, cx - a, cy, cx + a, cy);
                        g.DrawLine(pen, cx, cy - a, cx, cy + a);
                    }
                    return;
                }
                case Kind.Plain:
                {
                    bool sel = On && KindNow == 1 && (Bg.BgColor & 0xFFFFFF) == it.Index;
                    Color c = Color.FromArgb(255, (it.Index >> 16) & 255, (it.Index >> 8) & 255, it.Index & 255);
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, it.R);
                    using (Pen pen = new Pen(Edge(pal))) g.DrawEllipse(pen, RectangleF.Inflate(it.R, -0.5f, -0.5f));
                    Ring(g, pal, it.R, 0, sel, isHot, true);
                    return;
                }
                case Kind.Picker:
                {
                    bool own = On && KindNow == 1 && Array.IndexOf(Plains, Bg.BgColor & 0xFFFFFF) < 0;
                    // A colour wheel; the colour picked shows in its middle.
                    for (int i = 0; i < 12; i++)
                        using (SolidBrush b = new SolidBrush(Hue(i * 30))) g.FillPie(b, it.R.X, it.R.Y, it.R.Width, it.R.Height, i * 30 - 90, 31);
                    RectangleF mid = RectangleF.Inflate(it.R, -it.R.Width * 0.24f, -it.R.Height * 0.24f);
                    Color inner = own ? Backdrop.PlainColor(Bg) : Color.White;
                    using (SolidBrush b = new SolidBrush(inner)) g.FillEllipse(b, mid);
                    Ring(g, pal, it.R, 0, own, isHot, true);
                    return;
                }
                case Kind.Slider:
                {
                    int value = ValueOf(it);
                    Font f = Small();
                    TextKit.Draw(g, it.Label, f, new Point(it.R.X, it.R.Y + P(2)), label, TextFormatFlags.NoPadding);
                    string v = value.ToString();
                    Size vs = TextKit.Measure(v, f, Size.Empty, TextFormatFlags.NoPadding);
                    TextKit.Draw(g, v, f, new Point(it.R.Right - vs.Width, it.R.Y + P(2)), EdLook.C(pal.Label3), TextFormatFlags.NoPadding);
                    Rectangle t = it.Track;
                    EdLook.Fill(g, t, t.Height / 2f, EdLook.C(pal.ControlHover));
                    if (pal.High) EdHc.Track(g, t, t.Height / 2f);
                    float fx = t.X + t.Width * value / 100f;
                    if (fx > t.X + 1) EdLook.Fill(g, new RectangleF(t.X, t.Y, fx - t.X, t.Height), t.Height / 2f, EdLook.C(On ? pal.Accent : pal.Label3));
                    float dk = 16 * S * (isHot || sliding >= 0 && items[sliding] == it ? 1.08f : 1f);
                    Knob(g, new RectangleF(fx - dk / 2, t.Y + t.Height / 2f - dk / 2, dk, dk));
                    return;
                }
                case Kind.Check:
                {
                    bool v = Bg.BgBalance;
                    RectangleF box = new RectangleF(it.R.X, it.R.Y + (it.R.Height - P(16)) / 2f, P(16), P(16));
                    if (pal.High) EdHc.Segment(g, box, P(4), v, isHot, true);
                    else if (v) EdLook.Fill(g, box, P(4), EdLook.C(On ? pal.Accent : pal.Label3));
                    else
                    {
                        EdLook.Fill(g, box, P(4), EdLook.C(isHot ? pal.ControlHover : pal.Control));
                        EdLook.Hairline(g, box, P(4), Edge(pal));
                    }
                    if (v)
                        using (Pen pen = new Pen(pal.High ? EdLook.C(pal.OnAccent) : Color.White, Math.Max(1.5f, 1.8f * S)))
                        {
                            pen.StartCap = pen.EndCap = LineCap.Round;
                            pen.LineJoin = LineJoin.Round;
                            g.DrawLines(pen, new PointF[] { new PointF(box.X + 4f * S, box.Y + 8.3f * S), new PointF(box.X + 6.8f * S, box.Y + 11f * S), new PointF(box.X + 12f * S, box.Y + 5.2f * S) });
                        }
                    Font f = Small();
                    TextKit.Draw(g, it.Label, f, new Point((int)box.Right + P(8), it.R.Y + (it.R.Height - f.Height) / 2), EdLook.C(pal.Label), TextFormatFlags.NoPadding);
                    return;
                }
                case Kind.Align:
                {
                    bool sel = Bg.BgAlign == it.Index;
                    RectangleF r = RectangleF.Inflate(it.R, -0.5f, -0.5f);
                    Color line = EdLook.C(sel ? (On ? pal.Accent : pal.Label2) : isHot ? pal.Label2 : pal.Label3);
                    if (sel) EdLook.Fill(g, r, P(4), EdLook.C(pal.Control));
                    using (GraphicsPath p = Theme.Round(r, P(4)))
                    using (Pen pen = new Pen(line, Math.Max(1f, (sel ? 1.6f : 1.1f) * S))) g.DrawPath(pen, p);
                    // The small block shows where the capture goes.
                    float bw = it.R.Width * 0.42f, bh = it.R.Height * 0.4f, inset = 3.5f * S;
                    int c = it.Index % 3, rr = it.Index / 3;
                    float bx = c == 0 ? r.X + inset : c == 2 ? r.Right - inset - bw : r.X + (r.Width - bw) / 2;
                    float by = rr == 0 ? r.Y + inset : rr == 2 ? r.Bottom - inset - bh : r.Y + (r.Height - bh) / 2;
                    EdLook.Fill(g, new RectangleF(bx, by, bw, bh), 1.5f * S, line);
                    return;
                }
                case Kind.Keep:
                {
                    Color ink = EdLook.C(isHot ? pal.Label : pal.Label2);
                    if (pal.High) ink = EdHc.Segment(g, it.R, P(6), false, isHot, true);
                    else EdLook.Fill(g, it.R, P(6), EdLook.C(isHot ? pal.ControlHover : pal.Control));
                    using (Pen pen = new Pen(ink, Math.Max(1.4f, 1.6f * S)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        float cx = it.R.X + it.R.Width / 2f, cy = it.R.Y + it.R.Height / 2f, a = 5f * S;
                        g.DrawLine(pen, cx - a, cy, cx + a, cy);
                        g.DrawLine(pen, cx, cy - a, cx, cy + a);
                    }
                    return;
                }
                default:
                {
                    // The proportion, or the saved styles: a pop-up button.
                    Color ink = EdLook.C(pal.Label);
                    if (pal.High) ink = EdHc.Segment(g, it.R, P(6), false, isHot, true);
                    else EdLook.Fill(g, it.R, P(6), EdLook.C(isHot ? pal.ControlHover : pal.Control));
                    string text;
                    if (it.Kind == Kind.Styles)
                    {
                        int n = Styles.Count(Bg);
                        text = n == 0 ? I18n.T("Estilos guardados\u2026", "Saved styles\u2026") : I18n.Fmt("Estilos guardados ({0})", "Saved styles ({0})", n);
                    }
                    else text = RatioNames[Math.Max(0, Array.IndexOf(Ratios, Bg.BgRatio))];
                    Rectangle tr = new Rectangle(it.R.X + P(10), it.R.Y, it.R.Width - P(30), it.R.Height);
                    TextKit.Draw(g, text, it.Kind == Kind.Styles ? Regular() : Semibold(), tr, ink,
                                 TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    using (Pen pen = new Pen(ink, Math.Max(1.2f, 1.5f * S)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        float cx = it.R.Right - P(14), cy = it.R.Y + it.R.Height / 2f, a = 3.5f * S;
                        g.DrawLines(pen, new PointF[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
                    }
                    return;
                }
            }
        }

        static Color Hue(float h)
        {
            float x = 1 - Math.Abs(h / 60f % 2 - 1);
            float r = 0, gr = 0, b = 0;
            if (h < 60) { r = 1; gr = x; }
            else if (h < 120) { r = x; gr = 1; }
            else if (h < 180) { gr = 1; b = x; }
            else if (h < 240) { gr = x; b = 1; }
            else if (h < 300) { r = x; b = 1; }
            else { r = 1; b = x; }
            return Color.FromArgb(255, (int)(r * 235 + 20), (int)(gr * 235 + 20), (int)(b * 235 + 20));
        }

        int HitTest(Point p)
        {
            Point c = new Point(p.X, p.Y + scroll);
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.Kind == Kind.Header) continue;
                Rectangle r = it.R;
                if (it.Kind == Kind.Slider) r = Rectangle.FromLTRB(it.R.X, it.Track.Y - P(12), it.R.Right, it.Track.Bottom + P(12));
                if (r.Contains(c)) return i;
            }
            return -1;
        }

        static string TipFor(Item it)
        {
            switch (it.Kind)
            {
                case Kind.Swatch: case Kind.Blur: case Kind.Plain: case Kind.Picker: case Kind.Add: case Kind.Align: case Kind.Keep: return it.Label;
            }
            return null;
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            InvalidateItem(hot);
            hot = h;
            InvalidateItem(hot);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            string tip = h >= 0 ? TipFor(items[h]) : null;
            if (tip != null) EdTip.Schedule(this, Shown(items[h].R), tip);
            else EdTip.Cancel();
        }

        Rectangle Shown(Rectangle r) { r.Offset(0, -scroll); return r; }

        void InvalidateItem(int i)
        {
            if (i < 0 || i >= items.Count) return;
            Rectangle r = Shown(items[i].R);
            r.Inflate(P(6), P(6));
            Invalidate(r);
        }

        void Fire(Action a) { if (a != null) a(); }

        // A choice made (a click, or a reader's default action): applied, previewed and remembered.
        void Pick(Item it)
        {
            switch (it.Kind)
            {
                case Kind.None: On = false; break;
                case Kind.Swatch: On = true; Bg.BgKind = 0; Bg.BgPreset = it.Index; break;
                case Kind.Blur: On = true; Bg.BgKind = 2; Bg.BgBlur = it.Index; break;
                case Kind.Plain: On = true; Bg.BgKind = 1; Bg.BgColor = it.Index; break;
                case Kind.Picker: if (!PickColor()) return; break;
                case Kind.Check: On = true; Bg.BgBalance = !Bg.BgBalance; break;
                case Kind.Align: On = true; Bg.BgAlign = it.Index; break;
                case Kind.More: allGradients = !allGradients; Relayout(); return;
                case Kind.Add: AddPicture(); return;
                case Kind.Ratio: RatioMenu(it); return;
                case Kind.Styles: StylesMenu(it); return;
                case Kind.Keep: KeepStyle(); return;
                default: return;
            }
            Invalidate();
            Fire(Changed);
            Fire(Committed);
        }

        void SetRatio(int i)
        {
            Bg.BgRatio = Ratios[i];
            On = true;
            Invalidate();
            Fire(Changed);
            Fire(Committed);
        }

        void RatioMenu(Item it)
        {
            ContextMenuStrip m = TrayMenu.Create();
            for (int i = 0; i < Ratios.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem mi = TrayMenu.Item(RatioNames[i], null, delegate { SetRatio(idx); });
                mi.Checked = Bg.BgRatio == Ratios[i];
                m.Items.Add(mi);
            }
            DisposeOnClose(m);
            Rectangle r = Shown(it.R);
            m.Show(this, new Point(r.X, r.Bottom + P(4)));
        }

        // A menu is let go once it closed and its click ran; at once if the panel is already gone.
        void DisposeOnClose(ContextMenuStrip m)
        {
            m.Closed += delegate
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed) { BeginInvoke((Action)delegate { m.Dispose(); }); return; }
                }
                catch (InvalidOperationException) { }
                m.Dispose();
            };
        }

        bool PickColor()
        {
            using (ColorDialog d = new ColorDialog())
            {
                d.FullOpen = true;
                d.Color = Backdrop.PlainColor(Bg);
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return false;
                On = true;
                Bg.BgKind = 1;
                Bg.BgColor = d.Color.ToArgb() & 0xFFFFFF;
                return true;
            }
        }

        void AddPicture()
        {
            if (Backdrop.CustomCount >= Backdrop.MaxCustom) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = I18n.T("Elige una imagen para usar de fondo", "Choose an image to use as background");
                d.Filter = I18n.T("Im\u00E1genes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                int i;
                Cursor = Cursors.WaitCursor;
                try { i = Backdrop.AddCustom(d.FileName); }
                finally { Cursor = Cursors.Default; }
                if (i < 0)
                {
                    Dialogs.Show(FindForm(), I18n.T("No se ha podido abrir esa imagen.", "Could not open that image."), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                On = true;
                Bg.BgKind = 0;
                Bg.BgPreset = i;
            }
            Relayout();
            Fire(Changed);
            Fire(Committed);
        }

        // A picture of the user's own, from its menu: gone from the list and from disk.
        void RemovePicture(int i)
        {
            if (!Backdrop.IsCustom(i)) return;
            Backdrop.RemoveCustom(i);
            if (Bg.BgPreset == i) Bg.BgPreset = 0;
            else if (Bg.BgPreset > i) Bg.BgPreset--;   // the ones after it moved up
            Styles.PictureRemoved(Bg, i);
            Relayout();
            Fire(Changed);
            Fire(Committed);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle bar = BarThumb();
            if (e.Button == MouseButtons.Left && !bar.IsEmpty && e.X >= Width - P(12))
            {
                if (!bar.Contains(e.Location)) { scroll += (e.Y < bar.Y ? -1 : 1) * Height * 9 / 10; ClampScroll(); Invalidate(); }
                else barGrab = e.Y - bar.Y;
                return;
            }
            if (hot < 0 || hot >= items.Count) return;
            Item it = items[hot];
            EdTip.Cancel();
            if (e.Button == MouseButtons.Right)
            {
                if (it.Kind != Kind.Swatch || !Backdrop.IsCustom(it.Index)) return;
                int idx = it.Index;
                ContextMenuStrip m = TrayMenu.Create();
                m.Items.Add(TrayMenu.Item(I18n.T("Quitar este fondo", "Remove this background"), "trash", delegate { RemovePicture(idx); }));
                DisposeOnClose(m);
                m.Show(this, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (it.Kind == Kind.Slider)
            {
                bool wasOn = On;
                sliding = hot;
                KeepShadow();
                On = true;
                if (!Slide(e.X) && !wasOn) Fire(Changed); // the backdrop comes on even if the value stays
                Invalidate();
                return; // Committed on mouse up
            }
            Pick(it);
        }

        // Sets a slider's value; returns whether it changed.
        bool SetValue(Item it, int v)
        {
            v = Math.Max(0, Math.Min(100, v));
            switch (it.Index)
            {
                case 0: if (Bg.BgPadding == v) return false; Bg.BgPadding = v; break;
                case 1: if (Bg.BgRadius == v) return false; Bg.BgRadius = v; break;
                case 2:
                    if (ShadowNow == v) return false;
                    // At 0 the shadow is off and keeps the size it had before the drag, for when it is turned on again.
                    Bg.BgShadow = v > 0;
                    Bg.BgShadowSize = v > 0 ? v : shadowKept;
                    break;
                default: if (Bg.BgInset == v) return false; Bg.BgInset = v; break;
            }
            return true;
        }

        int shadowKept = 50;

        // The shadow's size before a change, unless it was barely there.
        void KeepShadow()
        {
            shadowKept = Bg.BgShadowSize >= 10 ? Bg.BgShadowSize : 50;
        }

        bool Slide(int x)
        {
            Item it = items[sliding];
            if (!SetValue(it, (int)Math.Round((x - it.Track.X) * 100.0 / Math.Max(1, it.Track.Width)))) return false;
            // Only this slider (with room for its knob and shadow): the swatches, some of them pictures, stay as they are.
            Invalidate(Rectangle.Inflate(Shown(it.R), P(16), P(8)));
            Fire(Changed);
            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (sliding >= 0) { Slide(e.X); return; }
            if (barGrab >= 0)
            {
                Rectangle bar = BarThumb();
                int track = Height - P(8) - bar.Height;
                scroll = (int)Math.Round((e.Y - barGrab - P(4)) * (double)(contentH - Height) / Math.Max(1, track));
                ClampScroll();
                Invalidate();
                return;
            }
            SetHot(HitTest(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (barGrab >= 0) { barGrab = -1; Invalidate(); }
            EndSlide();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (sliding >= 0 || contentH <= Height) return;
            scroll -= e.Delta * P(56) / 120;
            ClampScroll();
            EdTip.Cancel();
            Invalidate();
            SetHot(HitTest(e.Location));
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
