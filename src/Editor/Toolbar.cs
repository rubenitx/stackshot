// Stackshot - Editor toolbar (also the window's title bar) and hint strip.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using M = System.Windows.Media;

namespace Stackshot
{
    // Shared look of the editor chrome, read from the design palette at paint time so theme switches just repaint.
    public static class EdLook
    {
        static string text, semibold;

        public static Color C(M.Color c) { return Ds.Gdi(c); }

        // Area around the capture, so it floats on a calm, slightly darker surface.
        public static Color Surround { get { return Ds.Dark ? Color.FromArgb(22, 22, 24) : Color.FromArgb(233, 233, 235); } }

        public static string Text
        {
            get
            {
                if (text == null) text = Fonts.Has("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
                return text;
            }
        }

        public static string Semibold
        {
            get
            {
                if (semibold == null) semibold = Fonts.Has("Segoe UI Variable Text Semibold") ? "Segoe UI Variable Text Semibold" : "Segoe UI Semibold";
                return semibold;
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)Math.Round(a.A + (b.A - a.A) * t), (int)Math.Round(a.R + (b.R - a.R) * t),
                                  (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static void Fill(Graphics g, RectangleF r, float radius, Color c)
        {
            using (GraphicsPath p = Theme.Round(r, radius))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        // 1 px border just inside the shape.
        public static void Hairline(Graphics g, RectangleF r, float radius, Color c)
        {
            r.Inflate(-0.5f, -0.5f);
            using (GraphicsPath p = Theme.Round(r, Math.Max(0, radius - 0.5f)))
            using (Pen pen = new Pen(c, 1f)) g.DrawPath(pen, p);
        }

        // Line icon centered in r.
        public static void Icon(Graphics g, string name, RectangleF r, int px, M.Color color, float stroke)
        {
            if (name == null || px <= 0) return;
            Bitmap b = Ink.GlyphBitmap(name, px, color, stroke);
            g.DrawImage(b, new Rectangle((int)Math.Round(r.X + (r.Width - px) / 2f), (int)Math.Round(r.Y + (r.Height - px) / 2f), px, px));
        }

        public static M.Color White { get { return M.Color.FromRgb(255, 255, 255); } }
    }

    public class Bar : Control
    {
        public enum Kind { Tool, Swatch, Weight, Button, Gap, Caption }

        public class Item
        {
            public Kind Kind;
            public Tool Tool;
            public int Index;
            public Color Swatch;
            public string Glyph, Label, Alt, Tip;
            public Action Do;
            public bool On, Accent, Right, DragOut, ShowAlt;
            // Left out when the screen is too narrow even for the tight bar (reachable by shortcut or from the stack).
            public bool Optional;
            public bool Enabled = true;
            // Contextual options (Group > 0) can be hidden; the rest of the bar keeps its place.
            public bool Visible = true;
            public int Group;
            // Weights drawn as letter sizes (text and numbers).
            public bool Sized;
            public Rectangle R;
            internal float HotT, DownT;
            internal int Sig;
            internal bool Placed;
        }

        public readonly List<Item> Items = new List<Item>();
        public float S = 1f;
        public bool Maximized;
        // How much the bar gives up to fit the window: 0 everything with labels; 1 secondary buttons as icons; 2 tighter
        // spacing; 3 without the optional buttons (the narrowest window allowed); 4 the primary button as an icon too,
        // only for screens narrower than that.
        public int Level { get; private set; }
        public const int Levels = 5, MinLevel = 3;
        public bool Compact { get { return Level > 0; } }
        // Width that fits every item with its label.
        public int FullWidth { get; private set; }
        bool locked;

        // While exporting only the window buttons respond (the window can still be moved, minimized or closed).
        public bool Locked
        {
            get { return locked; }
            set { if (locked == value) return; locked = value; SetHot(-1); pressed = -1; Invalidate(); }
        }
        public event Action DragOut;
        int hot = -1, pressed = -1;
        Point downAt;
        bool dragFired;
        readonly Timer anim = new Timer();
        long lastStep;
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = EdLook.C(Ds.Brushes.Window);
            anim.Interval = 15;
            anim.Tick += delegate { Step(); };
        }

        int P(float v) { return (int)Math.Round(v * S); }

        public Item Add(Kind kind)
        {
            Item it = new Item();
            it.Kind = kind;
            Items.Add(it);
            return it;
        }

        bool IconOnly(Item it) { return it.Label == null || (Level >= 1 && !it.Accent) || Level >= 4; }

        bool Tight { get { return Level >= 2; } }

        bool Shown(Item it) { return it.Visible && !(it.Optional && Level >= 3); }

        int WidthOf(Item it)
        {
            switch (it.Kind)
            {
                case Kind.Tool: return Tight ? P(30) : P(32);
                case Kind.Swatch: return P(24);
                case Kind.Weight: return P(24);
                case Kind.Gap: return Tight ? P(7) : P(11);
                case Kind.Caption: return P(46);
                default:
                    if (IconOnly(it)) return Tight ? P(30) : P(32);
                    Font f = LabelFont(it);
                    int tw = TextKit.Measure(it.Label, f, Size.Empty, TextFormatFlags.NoPadding).Width;
                    if (it.Alt != null) tw = Math.Max(tw, TextKit.Measure(it.Alt, f, Size.Empty, TextFormatFlags.NoPadding).Width);
                    return P(12) + (HasIcon(it) ? P(15) + P(6) : 0) + tw + P(12);
            }
        }

        // Secondary actions are text-only to keep the bar compact; the primary one and toggles keep their icon.
        static bool HasIcon(Item it) { return it.Glyph != null && (!it.Right || it.Accent); }

        Font LabelFont(Item it) { return Fonts.Get(it.Accent ? EdLook.Semibold : EdLook.Text, P(13)); }

        // Tools as a centered group, actions on the right before the window buttons, at the first level that fits.
        // Returns the narrowest width the window should allow (MinLevel).
        public int LayoutItems()
        {
            int width = Math.Max(Width, 1), min = 0, pick = -1;
            for (int lv = 0; lv < Levels; lv++)
            {
                Level = lv;
                int need = Arrange();
                if (lv == 0) FullWidth = need;
                if (lv == MinLevel) min = need;
                if (pick < 0 && need <= width) pick = lv;
            }
            Level = pick < 0 ? Levels - 1 : pick;
            Arrange();
            Invalidate();
            return min;
        }

        int Arrange()
        {
            int width = Math.Max(Width, 1), m = P(12);
            int space = Tight ? P(1) : P(2), rightSpace = Tight ? P(2) : P(6), between = Tight ? P(8) : P(20);
            int capW = 0;
            foreach (Item it in Items) if (it.Kind == Kind.Caption) capW += WidthOf(it);
            int cx = width - capW;
            foreach (Item it in Items)
            {
                if (it.Kind != Kind.Caption) continue;
                it.R = new Rectangle(cx, 0, WidthOf(it), P(32));
                cx += it.R.Width;
            }

            int right = width - capW - P(10), rw = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                Item it = Items[i];
                if (!it.Right || it.Kind == Kind.Caption) continue;
                if (!Shown(it)) { it.R = Rectangle.Empty; continue; }
                int w = WidthOf(it), h = IconOnly(it) ? P(32) : P(28);
                if (rw > 0) { right -= rightSpace; rw += rightSpace; }
                right -= w;
                rw += w;
                it.R = new Rectangle(right, (Height - h) / 2, w, h);
            }

            // The left group is laid out with every option showing, so tools and actions never move; the visible
            // options are then packed into that slot.
            int lw = 0;
            foreach (Item it in Items)
            {
                if (it.Right || it.Kind == Kind.Caption) continue;
                if (lw > 0) lw += space;
                lw += WidthOf(it);
            }
            int start = (width - lw) / 2;
            start = Math.Min(start, (rw > 0 ? right : width - capW) - between - lw);
            start = Math.Max(m, start);
            int left = start, slot = -1;
            foreach (Item it in Items)
            {
                if (it.Right || it.Kind == Kind.Caption) continue;
                int w = WidthOf(it), h = it.Kind == Kind.Button && !IconOnly(it) ? P(28) : P(32);
                it.R = new Rectangle(left, (Height - h) / 2, w, h);
                if (it.Group > 0 && slot < 0) slot = left;
                left += w + space;
            }
            foreach (Item it in Items)
            {
                it.Placed = it.Visible;
                if (it.Group <= 0) continue;
                if (!it.Visible) { it.R = Rectangle.Empty; continue; }
                it.R.X = slot;
                slot += it.R.Width + space;
            }
            return m + lw + between + rw + P(10) + capW;
        }

        // Repaints only the items whose state changed since the last paint (relayout if options were shown or hidden).
        public void Sync()
        {
            bool relayout = false;
            foreach (Item it in Items) if (it.Visible != it.Placed) relayout = true;
            if (relayout) { LayoutItems(); return; }
            foreach (Item it in Items)
            {
                int sig = SigOf(it);
                if (sig != it.Sig) InvalidateItem(it);
            }
        }

        static int SigOf(Item it)
        {
            return (it.On ? 1 : 0) | (it.Enabled ? 2 : 0) | (it.ShowAlt ? 4 : 0) | (it.Sized ? 8 : 0) | (it.Visible ? 16 : 0) |
                   ((it.Tip ?? "").GetHashCode() << 5);
        }

        void InvalidateItem(Item it)
        {
            if (it.R.IsEmpty) return;
            Rectangle r = it.R;
            r.Inflate(P(4), P(4));
            Invalidate(r);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutItems();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Palette pal = Ds.Brushes;
            Rectangle clip = e.ClipRectangle;
            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Window))) g.FillRectangle(b, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(b, 0, Height - 1, Width, 1);
            for (int i = 0; i < Items.Count; i++)
            {
                Item it = Items[i];
                if (!it.Visible || it.R.IsEmpty) { it.Sig = SigOf(it); continue; }
                Rectangle r = it.R;
                r.Inflate(P(4), P(4));
                if (!r.IntersectsWith(clip)) continue;
                it.Sig = SigOf(it);
                PaintItem(g, pal, it);
            }
            if (locked)
            {
                int capX = Width;
                foreach (Item it in Items) if (it.Kind == Kind.Caption) capX = Math.Min(capX, it.R.X);
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, EdLook.C(pal.Window)))) g.FillRectangle(veil, 0, 0, capX, Height - 1);
            }
        }

        static float Smooth(float t) { return t * t * (3 - 2 * t); }

        static Color Alpha(Color c, float t) { return Color.FromArgb((int)Math.Round(c.A * Math.Max(0, Math.Min(1, t))), c.R, c.G, c.B); }

        // Quiet hover/press fill shared by tools, weights and icon buttons.
        static Color Wash(Palette pal, Item it)
        {
            float h = Smooth(it.HotT), d = Smooth(it.DownT);
            return EdLook.Mix(Alpha(EdLook.C(pal.Control), h), EdLook.C(pal.ControlHover), d);
        }

        void PaintItem(Graphics g, Palette pal, Item it)
        {
            Rectangle r = it.R;
            float stroke = 1.5f * S;
            float hotT = Smooth(it.HotT), downT = Smooth(it.DownT);
            switch (it.Kind)
            {
                case Kind.Gap:
                    using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator)))
                        g.FillRectangle(b, r.X + r.Width / 2, (Height - P(20)) / 2, 1, P(20));
                    return;
                case Kind.Caption:
                {
                    bool close = it.Index == 2;
                    M.Color fg = pal.Label;
                    if (hotT > 0 && Enabled)
                    {
                        if (close)
                        {
                            Color red = EdLook.Mix(Color.FromArgb(196, 43, 28), Color.FromArgb(200, 196, 43, 28), downT);
                            using (SolidBrush b = new SolidBrush(Alpha(red, hotT))) g.FillRectangle(b, r);
                            if (hotT > 0.5f) fg = EdLook.White;
                        }
                        else using (SolidBrush b = new SolidBrush(Wash(pal, it))) g.FillRectangle(b, r);
                    }
                    string name = it.Index == 0 ? "minus" : it.Index == 1 ? (Maximized ? "restore" : "maximize") : "close";
                    EdLook.Icon(g, name, r, P(16), fg, Math.Max(1f, 1.1f * S));
                    return;
                }
                case Kind.Tool:
                {
                    if (it.On) EdLook.Fill(g, r, P(7), EdLook.Mix(EdLook.C(Ds.WithAlpha(pal.Accent, 0.16)), EdLook.C(Ds.WithAlpha(pal.Accent, 0.26)), downT));
                    else if (hotT > 0) EdLook.Fill(g, r, P(7), Wash(pal, it));
                    EdLook.Icon(g, it.Glyph, r, P(18), it.On ? pal.Accent : pal.Label, stroke);
                    return;
                }
                case Kind.Swatch:
                {
                    float d = 18 * S * (1 + 0.07f * hotT - 0.06f * downT);
                    RectangleF c = new RectangleF(r.X + (r.Width - d) / 2f, r.Y + (r.Height - d) / 2f, d, d);
                    float ringT = it.On ? 1 : hotT;
                    if (ringT > 0)
                    {
                        float pw = Math.Max(1.5f, 2f * S), gap = 2f * S;
                        RectangleF ring = RectangleF.Inflate(c, gap + pw / 2, gap + pw / 2);
                        using (Pen p = new Pen(it.On ? EdLook.C(pal.Label) : Alpha(EdLook.C(pal.Label3), ringT), pw)) g.DrawEllipse(p, ring);
                    }
                    using (SolidBrush b = new SolidBrush(it.Swatch)) g.FillEllipse(b, c);
                    if (it.Swatch.GetBrightness() > 0.9f || (Ds.Dark && it.Swatch.GetBrightness() < 0.15f))
                        using (Pen p = new Pen(EdLook.C(pal.Hairline))) g.DrawEllipse(p, RectangleF.Inflate(c, -0.5f, -0.5f));
                    return;
                }
                case Kind.Weight:
                {
                    if (it.On) EdLook.Fill(g, r, P(7), EdLook.C(Ds.WithAlpha(pal.Accent, 0.16)));
                    else if (hotT > 0 || downT > 0) EdLook.Fill(g, r, P(7), Wash(pal, it));
                    Color ink = EdLook.C(it.On ? pal.Accent : pal.Label);
                    if (it.Sized)
                    {
                        // Text and numbers: the weight sets the size, shown as a small, medium and large letter.
                        float px = it.Index == 0 ? 10.5f : it.Index == 1 ? 13.5f : 17f;
                        TextKit.Draw(g, "A", Fonts.Get(EdLook.Semibold, px * S), r, ink,
                                     TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                        return;
                    }
                    float dd = it.Index == 0 ? 4 * S : it.Index == 1 ? 7 * S : 10.5f * S;
                    using (SolidBrush b = new SolidBrush(ink))
                        g.FillEllipse(b, r.X + (r.Width - dd) / 2f, r.Y + (r.Height - dd) / 2f, dd, dd);
                    return;
                }
                default:
                {
                    M.Color fg = it.Enabled ? pal.Label : pal.Label3;
                    if (IconOnly(it) && !it.Accent)
                    {
                        // Icon-only: a quiet button like the tools; the confirmation shows as a check.
                        if (it.On && it.Enabled) { EdLook.Fill(g, r, P(7), EdLook.C(Ds.WithAlpha(pal.Accent, 0.16))); fg = pal.Accent; }
                        else if (it.Enabled && (hotT > 0 || downT > 0)) EdLook.Fill(g, r, P(7), Wash(pal, it));
                        if (it.ShowAlt) { fg = pal.Accent; EdLook.Icon(g, "check", r, P(18), fg, 1.8f * S); }
                        else EdLook.Icon(g, it.Glyph, r, P(18), fg, stroke);
                        return;
                    }
                    float rad = P(7);
                    if (IconOnly(it) && it.Accent)
                    {
                        // The primary button on a very narrow screen: the accent fill with its icon only.
                        Color ac = EdLook.C(pal.Accent);
                        if (!it.Enabled) ac = Color.FromArgb(120, ac);
                        else ac = EdLook.Mix(EdLook.Mix(ac, Color.White, 0.1f * hotT), Color.Black, 0.15f * downT);
                        EdLook.Fill(g, RectangleF.Inflate(r, -P(1), -P(2)), rad, ac);
                        EdLook.Icon(g, it.ShowAlt ? "check" : it.Glyph, r, P(17), EdLook.White, 1.6f * S);
                        return;
                    }
                    if (it.Accent)
                    {
                        Color a = EdLook.C(pal.Accent);
                        if (!it.Enabled) a = Color.FromArgb(120, a);
                        else a = EdLook.Mix(EdLook.Mix(a, Color.White, 0.1f * hotT), Color.Black, 0.15f * downT);
                        EdLook.Fill(g, r, rad, a);
                        fg = EdLook.White;
                    }
                    else if (it.On && it.Enabled)
                    {
                        // Toggled on (backdrop).
                        EdLook.Fill(g, r, rad, EdLook.C(Ds.WithAlpha(pal.Accent, 0.16 + 0.06 * hotT + 0.06 * downT)));
                        fg = pal.Accent;
                    }
                    else
                    {
                        Color fill = EdLook.C(pal.Control);
                        if (it.Enabled) fill = EdLook.Mix(EdLook.Mix(fill, EdLook.C(pal.ControlHover), hotT), EdLook.C(pal.Hairline), downT);
                        EdLook.Fill(g, r, rad, fill);
                        EdLook.Hairline(g, r, rad, EdLook.C(Ds.WithAlpha(pal.Hairline, 0.7)));
                    }
                    // Icon and label centered as one group (the width also fits the confirmation text).
                    string text = it.ShowAlt ? it.Alt : it.Label;
                    int cw = TextKit.Measure(text, LabelFont(it), Size.Empty, TextFormatFlags.NoPadding).Width + (HasIcon(it) ? P(15) + P(6) : 0);
                    int x = r.X + Math.Max(P(10), (r.Width - cw) / 2);
                    if (HasIcon(it))
                    {
                        EdLook.Icon(g, it.Glyph, new RectangleF(x, r.Y, P(15), r.Height), P(15), fg, 1.4f * S);
                        x += P(15) + P(6);
                    }
                    TextKit.Draw(g, text, LabelFont(it), new Rectangle(x, r.Y, r.Right - x, r.Height), EdLook.C(fg),
                                 TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    return;
                }
            }
        }

        // Hover and press fade in and out; only the items that change are repainted and the timer stops when idle.
        void Step()
        {
            long now = clock.ElapsedMilliseconds;
            float dt = Math.Max(1, Math.Min(60, now - lastStep));
            lastStep = now;
            bool more = false;
            for (int i = 0; i < Items.Count; i++)
            {
                Item it = Items[i];
                float th = i == hot && it.Enabled ? 1 : 0, td = i == pressed && i == hot ? 1 : 0;
                float h = Approach(it.HotT, th, dt / (th > it.HotT ? 110f : 170f));
                float d = Approach(it.DownT, td, dt / (td > it.DownT ? 50f : 140f));
                if (h == it.HotT && d == it.DownT) continue;
                it.HotT = h;
                it.DownT = d;
                InvalidateItem(it);
                more = true;
            }
            if (!more) anim.Stop();
        }

        static float Approach(float v, float target, float step)
        {
            return v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
        }

        void Kick()
        {
            if (anim.Enabled) return;
            lastStep = clock.ElapsedMilliseconds - 15;
            anim.Start();
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Item it = Items[i];
                if (it.Kind != Kind.Gap && Shown(it) && (!locked || it.Kind == Kind.Caption) && it.R.Contains(p)) return i;
            }
            return -1;
        }

        // Empty toolbar areas move the window.
        public bool IsEmptyAt(Point p) { return HitTest(p) < 0; }

        void SetHot(int h)
        {
            if (h == hot) return;
            hot = h;
            Kick();
            bool usable = h >= 0 && Items[h].Enabled;
            Cursor = usable && Items[h].Kind != Kind.Caption ? (Items[h].DragOut ? Cursors.SizeAll : Cursors.Hand) : Cursors.Default;
            if (h >= 0 && Items[h].Tip != null && pressed < 0) EdTip.Schedule(this, Items[h].R, Items[h].Tip);
            else EdTip.Cancel();
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
                    EdTip.Cancel();
                    Kick();
                    if (DragOut != null) DragOut();
                    return;
                }
            }
            SetHot(HitTest(e.Location));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            EdTip.Cancel();
            if (e.Button != MouseButtons.Left || hot < 0 || !Items[hot].Enabled) return;
            pressed = hot;
            downAt = e.Location;
            dragFired = false;
            Kick();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int p = pressed;
            pressed = -1;
            Kick();
            if (p >= 0 && !dragFired && HitTest(e.Location) == p && Items[p].Enabled && Items[p].Do != null) Items[p].Do();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHot(-1);
        }

        // Capture lost while a button is held (a dialog, Alt+Tab): no press left behind.
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && pressed >= 0) { pressed = -1; Kick(); }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) EdTip.Cancel();
        }

        // For measurements: paints the bar (or the part in clip) into g.
        public void PaintTo(Graphics g, Rectangle clip) { OnPaint(new PaintEventArgs(g, clip)); }

        // For offscreen checks: forces an item's hover/press look.
        public void Pose(int index, float hotT, float downT)
        {
            Items[index].HotT = hotT;
            Items[index].DownT = downT;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EdTip.Cancel();
                anim.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Bottom strip: context help for the current tool or selection, and the output size.
    public class Hint : Control
    {
        public string LeftText = "", RightText = "";
        public double Progress = -1;   // 0-1: progress bar on top (video export); -1 = hidden
        public float S = 1f;

        public Hint()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = EdLook.C(Ds.Brushes.Window);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Palette pal = Ds.Brushes;
            g.Clear(EdLook.C(pal.Window));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush b = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(b, 0, 0, Width, 1);
            if (Progress >= 0)
            {
                int ph = Math.Max(2, (int)Math.Round(2 * S));
                using (SolidBrush b = new SolidBrush(EdLook.C(pal.Accent))) g.FillRectangle(b, 0, 0, (float)(Width * Math.Min(1, Progress)), ph);
            }
            int m = (int)Math.Round(14 * S);
            Font f = Fonts.Get(EdLook.Text, 12 * S);
            Color fg = EdLook.C(pal.Label2);
            Size rs = TextKit.Measure(RightText, f, Size.Empty, TextFormatFlags.NoPadding);
            Rectangle rr = new Rectangle(Width - rs.Width - m, 1, rs.Width + 1, Height - 1);
            TextKit.Draw(g, RightText, f, rr, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Rectangle lr = new Rectangle(m, 1, Math.Max(0, rr.X - 2 * m), Height - 1);
            TextKit.Draw(g, LeftText, f, lr, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                                  TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

}
