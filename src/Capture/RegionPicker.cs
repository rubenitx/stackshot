// Stackshot - Region picker: freezes the screen and lets the user drag an area or click a window.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Stackshot
{
    // One window covers every monitor with the frozen, dimmed screenshot. Hovering highlights the window underneath
    // (click = that window, or the whole monitor over the desktop); dragging selects an area. In all-in-one mode the
    // area stays on screen with handles to adjust it and a bar to choose what to do with it.
    // Drawing: a back buffer always holds what the window shows. Every change recomposes in it, from the two frozen
    // images, only what can differ between the old and the new state (the part of the area that changed and the bands
    // around both edges, labels and bars) and copies those rectangles to the window. Nothing is drawn incrementally, so
    // nothing can be left behind however fast the mouse moves.
    public partial class RegionPicker : Form
    {
        public enum Mode { Image, Video, Gif, Scroll, Text }

        // A monitor in client coordinates and its scale.
        class Mon { public Rectangle R; public float Scale; }

        // A monitor's usage hint, pre-composed over both frozen images (never part of the capture).
        class Hint { public Rectangle R, Zone; public Dib OnBright, OnDim; }

        readonly Dib bright, dimmed;   // plain and dimmed screen
        Dib back;                      // what the window shows
        readonly Rectangle vs;
        readonly List<Grabber.Win> wins;
        readonly Mode mode;
        readonly bool allInOne;
        readonly bool adjustFirst;   // "Ajustar antes": the area stays adjustable, with a live preview, until it is captured
        List<Mon> mons;
        readonly List<Hint> hints = new List<Hint>();
        Point cur = new Point(-10000, -10000), start;
        bool down, dragging, adjusting, interacted, nearHint;
        Rectangle sel, hover, windowSel;
        IntPtr hoverHandle;
        Rectangle result = Rectangle.Empty;
        IntPtr resultWindow = IntPtr.Zero;
        Bitmap crop;
        Mode chosen;
        Rectangle curMon = Rectangle.Empty;   // monitor under the cursor
        float curScale = 1f;                  // scale of the decorations: the monitor holding the lit area
        Font labelFont, barFont;
        Cursor shownCursor;

        // State currently in the back buffer (and on screen).
        Rectangle shownLit;
        List<Rectangle> shownParts = new List<Rectangle>();
        bool shownHint = true;

        // All-in-one adjusting: which handle (0-7 clockwise from top-left, 8 = move) is dragged, and the bar.
        int grab = -1, barHot = -1, barDown = -1;
        Point grabFrom;
        Rectangle grabSel;
        readonly List<BarItem> bar = new List<BarItem>();
        Rectangle barRect;
        float barScale;

        class BarItem { public Rectangle R; public string Icon, Label; public Mode Mode; public bool Primary, Close; public int TextW; }

        RegionPicker(Dib grab, Rectangle vs, List<Grabber.Win> wins, Mode mode) : this(grab, vs, wins, mode, false, false) { }

        RegionPicker(Dib grab, Rectangle vs, List<Grabber.Win> wins, Mode mode, bool allInOne, bool adjustFirst)
        {
            this.vs = vs;
            this.wins = wins;
            this.mode = mode;
            this.allInOne = allInOne && mode == Mode.Image;
            this.adjustFirst = adjustFirst && !this.allInOne && mode == Mode.Image;
            chosen = mode;
            bright = grab;
            // ~36% black: the screen at 64% over a new (black) buffer, in one pass.
            dimmed = new Dib(grab.Width, grab.Height);
            Native.AlphaBlend(dimmed.Dc, 0, 0, grab.Width, grab.Height, grab.Dc, 0, 0, grab.Width, grab.Height, 163 << 16);
            Native.GdiFlush();
            mons = Monitors(vs);
            BuildHints();
            Recompose(Changes()); // ready before the window shows: its first paint is a plain copy

            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Bounds = vs;
            Cursor = shownCursor = Cursors.Cross;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8; // WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        // Returns the chosen rectangle in screen coordinates (empty if cancelled), the frozen screen under it (its
        // screen rectangle in frozenAt) and the clicked window, if any.
        public static Rectangle Pick(Mode mode, out Bitmap frozen, out Rectangle frozenAt, out IntPtr window)
        {
            Mode chosen;
            return Pick(mode, false, out frozen, out frozenAt, out window, out chosen);
        }

        // allInOne: adjust the area afterwards and choose image, video, GIF, scrolling or text (returned in chosen). The
        // frozen image only covers the result, and only when an image or text was chosen; otherwise it is a 1 px placeholder.
        public static Rectangle Pick(Mode mode, bool allInOne, out Bitmap frozen, out Rectangle frozenAt, out IntPtr window, out Mode chosen)
        {
            return Pick(mode, allInOne, false, out frozen, out frozenAt, out window, out chosen);
        }

        // adjustFirst: an image area is not captured on release; it can be moved and resized, with a live preview of the
        // result, until Enter, a double click or Capturar.
        public static Rectangle Pick(Mode mode, bool allInOne, bool adjustFirst, out Bitmap frozen, out Rectangle frozenAt, out IntPtr window, out Mode chosen)
        {
            Stall.Mark("selector de \u00E1rea");
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Dib grab = Dib.FromScreen(virtualScreen);
            List<Grabber.Win> wins = Grabber.Windows();
            using (RegionPicker p = new RegionPicker(grab, virtualScreen, wins, mode, allInOne, adjustFirst))
            {
                p.ShowDialog();
                window = p.resultWindow;
                chosen = p.chosen;
                Rectangle res = p.result;
                if (p.crop != null)
                {
                    frozen = p.crop;
                    frozenAt = p.cropAt;
                }
                else
                {
                    frozen = new Bitmap(1, 1);
                    frozenAt = new Rectangle(res.Location, new Size(1, 1));
                }
                return res;
            }
        }

        Rectangle cropAt;

        // The whole frozen screen, in client coordinates.
        Rectangle Area { get { return new Rectangle(0, 0, bright.Width, bright.Height); } }

        float S { get { return curScale; } }
        int Pz(float v) { return (int)Math.Round(v * S); }
        Point ToScreen(Point p) { return new Point(p.X + vs.X, p.Y + vs.Y); }
        static Point Center(Rectangle r) { return new Point(r.X + r.Width / 2, r.Y + r.Height / 2); }

        // ---- Monitors, read once: asking Windows on every mouse move costs more than drawing.

        static List<Mon> Monitors(Rectangle vs)
        {
            List<Mon> l = new List<Mon>();
            foreach (Screen sc in Screen.AllScreens)
            {
                Mon m = new Mon();
                m.R = sc.Bounds;
                m.R.Offset(-vs.X, -vs.Y);
                m.Scale = ShotStack.ScaleFor(sc);
                l.Add(m);
            }
            return l;
        }

        // The monitor holding p, or the nearest one (the virtual screen can have gaps between monitors).
        Mon MonAt(Point p)
        {
            Mon best = null;
            long bestD = long.MaxValue;
            foreach (Mon m in mons)
            {
                if (m.R.Contains(p)) return m;
                long dx = Math.Max(0, Math.Max(m.R.Left - p.X, p.X - m.R.Right + 1));
                long dy = Math.Max(0, Math.Max(m.R.Top - p.Y, p.Y - m.R.Bottom + 1));
                if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = m; }
            }
            return best;
        }

        // The monitor holding most of r (labels and the bar stay on it).
        Rectangle MonitorOf(Rectangle r) { return MonAt(Center(r)).R; }

        void TrackMonitor() { curMon = MonAt(cur).R; }

        // Decorations take the scale of the monitor holding the lit area (or the cursor).
        void SyncScale()
        {
            Rectangle lit = Lit;
            float s = MonAt(lit.IsEmpty ? cur : Center(lit)).Scale;
            if (labelFont != null && s == curScale) return;
            curScale = s;
            if (labelFont != null) labelFont.Dispose();
            if (barFont != null) barFont.Dispose();
            labelFont = new Font("Segoe UI Semibold", (float)Math.Round(12 * s), GraphicsUnit.Pixel);
            barFont = new Font("Segoe UI Semibold", (float)Math.Round(12.5f * s), GraphicsUnit.Pixel);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.ForceForeground(Handle);
            Activate();
            Update();
            cur = PointToClient(Control.MousePosition);
            TrackMonitor();
            UpdateHover();
            nearHint = NearHint(cur);
            Present();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Windows asked for a repaint (first show, something passed over us): the back buffer already has it.
        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, Area);
            if (clip.Width <= 0 || clip.Height <= 0 || back == null) return; // null only once closed
            IntPtr hdc = e.Graphics.GetHdc();
            try { Native.BitBlt(hdc, clip.X, clip.Y, clip.Width, clip.Height, back.Dc, clip.X, clip.Y, Native.SRCCOPY); }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }

        // Puts the current state on screen now: recomposes what changed since the last state shown.
        void Present()
        {
            if (!IsHandleCreated) return;
            List<Rectangle> dirty = Recompose(Changes());
            if (dirty.Count == 0) return;
            IntPtr hdc = Native.GetDC(Handle);
            try
            {
                foreach (Rectangle r in dirty) Native.BitBlt(hdc, r.X, r.Y, r.Width, r.Height, back.Dc, r.X, r.Y, Native.SRCCOPY);
            }
            finally { Native.ReleaseDC(Handle, hdc); }
        }

        // What may differ between the state on screen and the current one; records the current one as shown.
        List<Rectangle> Changes()
        {
            SyncScale();
            if (adjusting) LayoutBar();
            LayoutPreview();
            Rectangle lit = Lit;
            List<Rectangle> parts = Parts();
            bool hint = HintOn;
            List<Rectangle> d = new List<Rectangle>();
            if (lit != shownLit)
            {
                Subtract(lit, shownLit, d);
                Subtract(shownLit, lit, d);
            }
            d.AddRange(shownParts);
            d.AddRange(parts);
            if (hint != shownHint) foreach (Hint h in hints) d.Add(h.R);
            shownLit = lit;
            shownParts = parts;
            shownHint = hint;
            return d;
        }

        // Recomposes those rectangles in the back buffer and returns them merged and clipped, ready to copy.
        List<Rectangle> Recompose(List<Rectangle> dirty)
        {
            if (back == null)
            {
                back = new Dib(bright.Width, bright.Height);
                dirty = new List<Rectangle> { Area };
            }
            Rectangle client = new Rectangle(0, 0, back.Width, back.Height);
            List<Rectangle> rs = new List<Rectangle>(dirty.Count);
            foreach (Rectangle r0 in dirty)
            {
                Rectangle r = Rectangle.Intersect(r0, client);
                if (r.Width > 0 && r.Height > 0) rs.Add(r);
            }
            Merge(rs);
            rs = Disjoint(rs);
            if (rs.Count == 0) return rs;
            foreach (Rectangle r in rs) Base(back, r, Point.Empty);
            Overlay(back, Point.Empty, rs);
            PaintTools(back, Point.Empty, rs);
            Native.GdiFlush();
            return rs;
        }

        // Dimmed screen, the lit area in bright and the hint over both, for r, into target at r - origin.
        void Base(Dib target, Rectangle r, Point origin)
        {
            int ox = origin.X, oy = origin.Y;
            Native.BitBlt(target.Dc, r.X - ox, r.Y - oy, r.Width, r.Height, dimmed.Dc, r.X, r.Y, Native.SRCCOPY);
            Rectangle lit = Rectangle.Intersect(Lit, r);
            if (lit.Width > 0 && lit.Height > 0)
                Native.BitBlt(target.Dc, lit.X - ox, lit.Y - oy, lit.Width, lit.Height, bright.Dc, lit.X, lit.Y, Native.SRCCOPY);
            if (!HintOn) return;
            foreach (Hint h in hints)
            {
                Rectangle a = Rectangle.Intersect(h.R, r);
                if (a.Width <= 0 || a.Height <= 0) continue;
                Native.BitBlt(target.Dc, a.X - ox, a.Y - oy, a.Width, a.Height, h.OnDim.Dc, a.X - h.R.X, a.Y - h.R.Y, Native.SRCCOPY);
                Rectangle b = Rectangle.Intersect(a, Lit);
                if (b.Width > 0 && b.Height > 0)
                    Native.BitBlt(target.Dc, b.X - ox, b.Y - oy, b.Width, b.Height, h.OnBright.Dc, b.X - h.R.X, b.Y - h.R.Y, Native.SRCCOPY);
            }
        }

        // The current state for r composed from scratch into buf (whose origin is r's top-left corner).
        void Compose(Dib buf, Rectangle r)
        {
            SyncScale();
            if (adjusting) LayoutBar();
            LayoutPreview();
            Base(buf, r, r.Location);
            Overlay(buf, r.Location, new List<Rectangle> { r });
            Native.GdiFlush();
        }

        // Edge, handles, size label and bar over the base, only inside rs (disjoint, screen coordinates; target's
        // top-left corner is origin). Handles and bar are pre-rendered sprites: while dragging they only get copied.
        void Overlay(Dib target, Point origin, List<Rectangle> rs)
        {
            Rectangle lit = Lit;
            if (lit.IsEmpty || rs.Count == 0) return;
            Rectangle bounds = rs[0];
            foreach (Rectangle r in rs) bounds = Rectangle.Union(bounds, r);
            using (Graphics g = Clipped(target, origin, rs)) PaintEdge(g, lit);
            if (adjusting)
            {
                Dib hs = HandleSprite();
                int c = hs.Width / 2;
                bool small = SmallSel;
                Point[] pts = Handles(sel);
                for (int i = 0; i < pts.Length; i++)
                {
                    if (small && i % 2 == 1) continue; // only the corners on tiny areas
                    Point h = HandleShown(pts[i]);
                    Blend(target, origin, rs, hs, new Point(h.X - c, h.Y - c));
                }
            }
            string text;
            Rectangle label = LabelRect(out text);
            if (text.Length > 0 && label.IntersectsWith(bounds))
                using (Graphics g = Clipped(target, origin, rs)) PaintLabel(g, label, text);
            if (adjusting && BarShadowRect().IntersectsWith(bounds)) Blend(target, origin, rs, BarSprite(), BarShadowRect().Location);
            if (PreviewOn && PreviewShadowRect().IntersectsWith(bounds))
            {
                Blend(target, origin, rs, PreviewSprite(), PreviewShadowRect().Location);
                if (previewCard.IntersectsWith(bounds)) PreviewText(target, origin, rs);
            }
        }

        static Graphics Clipped(Dib target, Point origin, List<Rectangle> rs)
        {
            Graphics g = target.Graphics();
            g.TranslateTransform(-origin.X, -origin.Y);
            if (rs.Count == 1) g.SetClip(rs[0]);
            else
            {
                using (Region clip = new Region(Rectangle.Empty))
                {
                    foreach (Rectangle r in rs) clip.Union(r);
                    g.Clip = clip;
                }
            }
            return g;
        }

        // Premultiplied sprite over target at screen point at, inside rs only.
        static void Blend(Dib target, Point origin, List<Rectangle> rs, Dib sprite, Point at)
        {
            Native.GdiFlush();
            Rectangle s = new Rectangle(at, new Size(sprite.Width, sprite.Height));
            foreach (Rectangle r in rs)
            {
                Rectangle a = Rectangle.Intersect(r, s);
                if (a.Width <= 0 || a.Height <= 0) continue;
                Native.AlphaBlend(target.Dc, a.X - origin.X, a.Y - origin.Y, a.Width, a.Height, sprite.Dc, a.X - at.X, a.Y - at.Y, a.Width, a.Height, (255 << 16) | (1 << 24)); // AC_SRC_ALPHA
            }
        }

        // Rectangles covering the same pixels without overlapping (sprites are blended once per pixel).
        static List<Rectangle> Disjoint(List<Rectangle> rs)
        {
            List<Rectangle> o = new List<Rectangle>(rs.Count);
            foreach (Rectangle r in rs)
            {
                List<Rectangle> pieces = new List<Rectangle> { r };
                foreach (Rectangle q in o)
                {
                    List<Rectangle> next = new List<Rectangle>();
                    foreach (Rectangle pc in pieces) Subtract(pc, q, next);
                    pieces = next;
                }
                o.AddRange(pieces);
            }
            return o;
        }

        // Bounds of everything drawn over the frozen screen: bands along the edge (with the handles), label and bar.
        List<Rectangle> Parts()
        {
            List<Rectangle> p = new List<Rectangle>(6);
            AddTools(p);
            Rectangle lit = Lit;
            if (lit.IsEmpty) return p;
            // Outside: the edge and half a handle; inside: a handle pushed in from the monitor edge.
            int m = adjusting ? Pz(9) / 2 + Pz(4) + 2 : 4, mi = adjusting ? Pz(9) + Pz(4) + 3 : 4;
            Rectangle o = Rectangle.Inflate(lit, m, m), i = Rectangle.Inflate(lit, -mi, -mi);
            if (i.Width <= 0 || i.Height <= 0) p.Add(o);
            else
            {
                p.Add(new Rectangle(o.X, o.Y, o.Width, i.Y - o.Y));
                p.Add(new Rectangle(o.X, i.Bottom, o.Width, o.Bottom - i.Bottom));
                p.Add(new Rectangle(o.X, i.Y, i.X - o.X, i.Height));
                p.Add(new Rectangle(i.Right, i.Y, o.Right - i.Right, i.Height));
            }
            string text;
            Rectangle label = LabelRect(out text);
            if (!label.IsEmpty) p.Add(Rectangle.Inflate(label, 2, 2));
            if (adjusting && bar.Count > 0) p.Add(BarShadowRect());
            if (PreviewOn) p.Add(PreviewShadowRect());
            return p;
        }

        Rectangle BarShadowRect() { return Rectangle.Inflate(barRect, Pz(14), Pz(14)); }

        // a minus b, as up to four rectangles.
        static void Subtract(Rectangle a, Rectangle b, List<Rectangle> o)
        {
            if (a.Width <= 0 || a.Height <= 0) return;
            Rectangle i = Rectangle.Intersect(a, b);
            if (i.Width <= 0 || i.Height <= 0) { o.Add(a); return; }
            if (i.Top > a.Top) o.Add(new Rectangle(a.X, a.Y, a.Width, i.Top - a.Top));
            if (i.Bottom < a.Bottom) o.Add(new Rectangle(a.X, i.Bottom, a.Width, a.Bottom - i.Bottom));
            if (i.Left > a.Left) o.Add(new Rectangle(a.X, i.Y, i.Left - a.Left, i.Height));
            if (i.Right < a.Right) o.Add(new Rectangle(i.Right, i.Y, a.Right - i.Right, i.Height));
        }

        static long Pixels(Rectangle r) { return r.Width <= 0 || r.Height <= 0 ? 0 : (long)r.Width * r.Height; }

        // Joins rectangles whose union wastes little, so a frame is a handful of copies.
        static void Merge(List<Rectangle> rs)
        {
            bool again = true;
            while (again)
            {
                again = false;
                for (int i = 0; i < rs.Count && !again; i++)
                {
                    for (int j = i + 1; j < rs.Count; j++)
                    {
                        Rectangle a = rs[i], b = rs[j], u = Rectangle.Union(a, b);
                        long covered = Pixels(a) + Pixels(b) - Pixels(Rectangle.Intersect(a, b));
                        if (Pixels(u) - covered > Math.Max(4096, covered / 4)) continue;
                        rs[i] = u;
                        rs.RemoveAt(j);
                        again = true;
                        break;
                    }
                }
            }
        }

        Rectangle Lit { get { return dragging || adjusting ? sel : hover; } }

        // 1 px white edge just outside the lit area, with a faint dark halo so it reads on light content too.
        static void PaintEdge(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.None;
            Frame(g, Rectangle.Inflate(r, 3, 3), Color.FromArgb(18, 0, 0, 0));
            Frame(g, Rectangle.Inflate(r, 2, 2), Color.FromArgb(56, 0, 0, 0));
            Frame(g, Rectangle.Inflate(r, 1, 1), Color.FromArgb(235, 255, 255, 255));
        }

        // 1 px ring along the inside of r.
        static void Frame(Graphics g, Rectangle r, Color c)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillRectangle(b, r.X, r.Y, r.Width, 1);
                g.FillRectangle(b, r.X, r.Bottom - 1, r.Width, 1);
                g.FillRectangle(b, r.X, r.Y + 1, 1, r.Height - 2);
                g.FillRectangle(b, r.Right - 1, r.Y + 1, 1, r.Height - 2);
            }
        }

        // Handle centers, clockwise from the top-left corner.
        static Point[] Handles(Rectangle r)
        {
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            return new Point[]
            {
                new Point(r.Left, r.Top), new Point(cx, r.Top), new Point(r.Right, r.Top), new Point(r.Right, cy),
                new Point(r.Right, r.Bottom), new Point(cx, r.Bottom), new Point(r.Left, r.Bottom), new Point(r.Left, cy)
            };
        }

        // Handles on a monitor edge move in just enough to be seen whole (full-screen areas), and only towards the
        // middle of the area. One in a gap between monitors stays put: nobody sees it there, and pulling it onto the
        // nearest monitor would leave it floating far from the area.
        Point HandleShown(Point p)
        {
            Point c = Center(sel), inner = new Point(p.X + Math.Sign(c.X - p.X), p.Y + Math.Sign(c.Y - p.Y)); // just inside the area
            foreach (Mon m in mons)
            {
                if (!m.R.Contains(inner)) continue;
                int k = Pz(9) / 2 + 1;
                int x = Math.Max(m.R.Left + k, Math.Min(m.R.Right - 1 - k, p.X)), y = Math.Max(m.R.Top + k, Math.Min(m.R.Bottom - 1 - k, p.Y));
                if (Math.Sign(x - p.X) != Math.Sign(c.X - p.X)) x = p.X; // an edge just past a monitor boundary
                if (Math.Sign(y - p.Y) != Math.Sign(c.Y - p.Y)) y = p.Y;
                if (Math.Abs(x - p.X) > Math.Abs(c.X - p.X)) x = c.X;    // a sliver: never past its middle
                if (Math.Abs(y - p.Y) > Math.Abs(c.Y - p.Y)) y = c.Y;
                return new Point(x, y);
            }
            return p;
        }

        bool SmallSel { get { return sel.Width < Pz(60) || sel.Height < Pz(60); } }

        // A round white handle with a blue ring and a soft shadow, centered in a premultiplied sprite.
        Dib HandleSprite()
        {
            if (handleSprite != null && handleFor == S) return handleSprite;
            if (handleSprite != null) handleSprite.Dispose();
            float d = Pz(9);
            int size = Pz(9) + 2 * (4 + Pz(1)), c = size / 2;
            Dib sp = new Dib(size, size, true);
            using (Graphics g = sp.Graphics())
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            using (Pen ring = new Pen(Ds.Gdi(Ds.Rgb(10, 132, 255)), Math.Max(1.2f, 1.4f * S)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF e = new RectangleF(c - d / 2, c - d / 2, d, d);
                RectangleF sh = e;
                sh.Inflate(1.5f, 1.5f);
                sh.Offset(0, 0.8f);
                g.FillEllipse(shade, sh);
                g.FillEllipse(Brushes.White, e);
                g.DrawEllipse(ring, e);
            }
            handleSprite = sp;
            handleFor = S;
            return sp;
        }

        Dib handleSprite, barSprite;
        float handleFor;
        string barSpriteFor;

        // The bar with its shadow, re-rendered only when its look changes (hover, press, scale).
        Dib BarSprite()
        {
            Rectangle sr = BarShadowRect();
            string key = S + "|" + barHot + "|" + barDown + "|" + barRect.Size;
            if (barSprite != null && key == barSpriteFor) return barSprite;
            if (barSprite != null) barSprite.Dispose();
            barSprite = new Dib(sr.Width, sr.Height, true);
            using (Graphics g = barSprite.Graphics())
            {
                g.TranslateTransform(-sr.X, -sr.Y);
                PaintBar(g);
            }
            barSpriteFor = key;
            return barSprite;
        }

        // ---- "Ajustar antes": a floating card beside the area with a live picture of what will be captured and its size.
        // It moves out of the way of the area, the bar and the size pill; with no room left on the monitor it hides.

        Rectangle previewCard, previewThumb;   // the card on screen; the picture inside it (relative to the card)
        Dib previewSprite, previewPic;
        string previewFor;

        bool PreviewOn { get { return adjustFirst && adjusting && !previewCard.IsEmpty; } }

        Rectangle PreviewShadowRect() { return Rectangle.Inflate(previewCard, Pz(14), Pz(14)); }

        void LayoutPreview()
        {
            previewCard = Rectangle.Empty;
            if (!adjustFirst || !adjusting || sel.Width < 1 || sel.Height < 1) return;
            int pad = Pz(8), foot = Pz(28);
            // Big areas shrink to fit; tiny ones grow (up to 4x) so their pixels can be checked.
            double k = Math.Min(Math.Min(Pz(232) / (double)sel.Width, Pz(150) / (double)sel.Height), 4);
            int tw = Math.Max(1, (int)Math.Round(sel.Width * k)), th = Math.Max(1, (int)Math.Round(sel.Height * k));
            int w = Math.Max(tw + 2 * pad, Pz(172)), h = pad + th + foot;
            previewThumb = new Rectangle((w - tw) / 2, pad, tw, th);
            Rectangle m = MonitorOf(sel);
            int gap = Pz(16), edge = Pz(10);
            string text;
            Rectangle label = LabelRect(out text);
            List<Rectangle> avoid = new List<Rectangle> { Rectangle.Inflate(sel, Pz(10), Pz(10)) };
            if (bar.Count > 0) avoid.Add(Rectangle.Inflate(barRect, Pz(8), Pz(8)));
            if (!label.IsEmpty) avoid.Add(Rectangle.Inflate(label, Pz(6), Pz(6)));
            Point[] at =
            {
                new Point(sel.Right + gap, sel.Top), new Point(sel.Left - gap - w, sel.Top),
                new Point(sel.Right - w, sel.Bottom + gap), new Point(sel.Right - w, sel.Top - gap - h),
                new Point(sel.Left, sel.Bottom + gap), new Point(sel.Left, sel.Top - gap - h),
                new Point(m.Right - w - edge, m.Top + edge), new Point(m.Left + edge, m.Top + edge),
                new Point(m.Right - w - edge, m.Bottom - h - edge), new Point(m.Left + edge, m.Bottom - h - edge)
            };
            foreach (Point p in at)
            {
                Rectangle r = new Rectangle(Math.Max(m.Left + edge, Math.Min(m.Right - w - edge, p.X)), Math.Max(m.Top + edge, Math.Min(m.Bottom - h - edge, p.Y)), w, h);
                if (!m.Contains(r)) continue;
                bool clear = true;
                foreach (Rectangle a in avoid) if (a.IntersectsWith(r)) { clear = false; break; }
                if (clear) { previewCard = r; return; }
            }
        }

        static readonly Color PreviewBack = Color.FromArgb(30, 30, 32);

        // Shadow, card and picture, premultiplied, rebuilt only when the area or its place changes.
        Dib PreviewSprite()
        {
            Rectangle sr = PreviewShadowRect();
            string key = S + "|" + sel + "|" + previewCard.Size;
            if (previewSprite != null && key == previewFor) return previewSprite;
            if (previewSprite != null) previewSprite.Dispose();
            Rectangle tr = previewThumb;
            // The picture: GDI's halftone scaling from the frozen screen (fast and smooth), whole pixels when enlarged.
            if (previewPic == null || previewPic.Width != tr.Width || previewPic.Height != tr.Height)
            {
                if (previewPic != null) previewPic.Dispose();
                previewPic = new Dib(tr.Width, tr.Height);
            }
            Native.SetStretchBltMode(previewPic.Dc, tr.Width < sel.Width ? 4 : 3);   // HALFTONE : COLORONCOLOR
            Native.SetBrushOrgEx(previewPic.Dc, 0, 0, IntPtr.Zero);
            Native.StretchBlt(previewPic.Dc, 0, 0, tr.Width, tr.Height, bright.Dc, sel.X, sel.Y, sel.Width, sel.Height, Native.SRCCOPY);
            Native.GdiFlush();
            previewSprite = new Dib(sr.Width, sr.Height, true);
            using (Graphics g = previewSprite.Graphics())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(-sr.X, -sr.Y);
                Rectangle c = previewCard;
                float radius = Pz(12);
                for (int i = Ds.Brushes.High ? 0 : 7; i >= 1; i--)
                {
                    RectangleF sh = c;
                    sh.Inflate(i * S, i * S);
                    sh.Offset(0, 4 * S);
                    using (GraphicsPath p = Theme.Round(sh, radius + i * S))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(10 * (8 - i) / 3, 0, 0, 0))) g.FillPath(b, p);
                }
                using (GraphicsPath p = Theme.Round(c, radius))
                using (SolidBrush bg = new SolidBrush(Ds.Gdi(Palette.HudBack(Ds.Rgb(PreviewBack.R, PreviewBack.G, PreviewBack.B))))) g.FillPath(bg, p);
                RectangleF hl = c;
                hl.Inflate(-0.5f, -0.5f);
                using (GraphicsPath p = Theme.Round(hl, radius - 0.5f))
                using (Pen line = new Pen(Ds.Gdi(Palette.HudLine), 1f)) g.DrawPath(line, p);
                Rectangle pic = new Rectangle(c.X + tr.X, c.Y + tr.Y, tr.Width, tr.Height);
                float pr = Math.Min(Pz(6), Math.Min(pic.Width, pic.Height) / 2f);
                using (GraphicsPath p = Theme.Round(pic, pr))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(p);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(previewPic.Bitmap, pic.X, pic.Y, pic.Width, pic.Height);
                    g.Restore(st);
                    using (Pen edge = new Pen(Color.FromArgb(40, 255, 255, 255), 1f)) g.DrawPath(edge, p);
                }
            }
            previewFor = key;
            return previewSprite;
        }

        // "Vista previa" and the size under the picture, with ClearType straight onto the opaque buffer.
        void PreviewText(Dib target, Point origin, List<Rectangle> rs)
        {
            Native.GdiFlush();
            using (Graphics g = target.DcGraphics())
            {
                g.TranslateTransform(-origin.X, -origin.Y);
                using (Region clip = new Region(Rectangle.Empty))
                {
                    foreach (Rectangle r in rs) clip.Union(r);
                    g.Clip = clip;
                }
                Rectangle c = previewCard;
                int pad = Pz(10), top = c.Y + previewThumb.Bottom;
                Rectangle line = new Rectangle(c.X + pad, top, c.Width - 2 * pad, c.Bottom - top);
                Color back = Ds.Gdi(Palette.HudBack(Ds.Rgb(PreviewBack.R, PreviewBack.G, PreviewBack.B)));
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
                TextRenderer.DrawText(g, I18n.T("Vista previa", "Preview"), labelFont, line, Over(Ds.Gdi(Palette.HudLabel2), back), f);
                TextRenderer.DrawText(g, sel.Width + " \u00D7 " + sel.Height + " px", labelFont, line, Over(Ds.Gdi(Palette.HudLabel), back), f | TextFormatFlags.Right);
            }
        }

        // GDI text has no transparency: a translucent colour mixed over the card.
        static Color Over(Color c, Color back)
        {
            double a = c.A / 255.0;
            return Color.FromArgb((int)Math.Round(c.R * a + back.R * (1 - a)), (int)Math.Round(c.G * a + back.G * (1 - a)), (int)Math.Round(c.B * a + back.B * (1 - a)));
        }

        // "W x H" pill: under the area, or inside its bottom edge when there is no room below. While adjusting it sits
        // above the area, or inside its top edge, wherever the bar is not.
        Rectangle LabelRect(out string text)
        {
            Rectangle t = Lit;
            text = t.IsEmpty ? "" : t.Width + " \u00D7 " + t.Height;
            if (text.Length == 0 || labelFont == null) return Rectangle.Empty;
            if (text != measuredText || labelFont != measuredFont)
            {
                measuredWidth = TextKit.Measure(text, labelFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextKit.Crisp).Width;
                measuredText = text;
                measuredFont = labelFont;
            }
            int w = measuredWidth + Pz(18), h = Pz(22), gap = Pz(8), edge = Pz(4);
            Rectangle m = MonitorOf(t);
            int x = Math.Max(m.Left + edge, Math.Min(m.Right - w - edge, t.X + (t.Width - w) / 2)), y;
            if (adjusting && bar.Count > 0)
            {
                Rectangle avoid = Rectangle.Inflate(barRect, Pz(4), Pz(4));
                // Last resort (a sliver against the bottom edge): stacked on the bar rather than hidden under it.
                int[] ys = { t.Top - h - gap, t.Top + gap, t.Bottom + gap, t.Bottom - h - gap, barRect.Top - h - gap, barRect.Bottom + gap };
                y = ys[1];
                foreach (int c in ys)
                {
                    if (c < m.Top + edge || c + h > m.Bottom - edge) continue;
                    if (new Rectangle(x, c, w, h).IntersectsWith(avoid)) continue;
                    y = c;
                    break;
                }
            }
            else
            {
                y = t.Bottom + gap;
                if (y + h > m.Bottom - edge)
                {
                    y = t.Bottom - h - gap;                                 // inside, along the bottom edge
                    if (t.Height < h + 2 * gap) y = t.Top - h - gap;        // too short: above
                }
            }
            y = Math.Max(m.Top + edge, Math.Min(m.Bottom - h - edge, y));
            return new Rectangle(x, y, w, h);
        }

        string measuredText;
        Font measuredFont;
        int measuredWidth;

        void PaintLabel(Graphics g, Rectangle r, string text)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Theme.Round(r, Pz(6)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(196, 18, 18, 20))) g.FillPath(b, p);
            TextKit.Draw(g, text, labelFont, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextKit.Crisp);
        }

        // ---- All-in-one bar

        void BuildBar()
        {
            bar.Clear();
            AddBar("area", I18n.T("Capturar", "Capture"), Mode.Image, true, false);
            if (adjustFirst)
            {
                AddBar("close", null, Mode.Image, false, true);
                barScale = 0;
                return;
            }
            AddBar("video", I18n.T("V\u00EDdeo", "Video"), Mode.Video, false, false);
            AddBar("gif", "GIF", Mode.Gif, false, false);
            AddBar("scroll", I18n.T("Desplazar", "Scroll"), Mode.Scroll, false, false);
            AddBar("textscan", I18n.T("Texto", "Text"), Mode.Text, false, false);
            AddBar("close", null, Mode.Image, false, true);
            barScale = 0;
        }

        void AddBar(string icon, string label, Mode m, bool primary, bool close)
        {
            BarItem b = new BarItem();
            b.Icon = icon; b.Label = label; b.Mode = m; b.Primary = primary; b.Close = close;
            bar.Add(b);
        }

        // Below the area, centered; above it or inside when there is no room.
        void LayoutBar()
        {
            if (barScale != S)
            {
                foreach (BarItem b in bar) b.TextW = b.Close ? 0 : TextKit.Measure(b.Label, barFont, Size.Empty, TextFormatFlags.SingleLine | TextKit.Crisp).Width;
                barScale = S;
            }
            int h = Pz(32), pad = Pz(5), gap = Pz(4), gi = Pz(16);
            int x = 0;
            foreach (BarItem b in bar)
            {
                int w = b.Close ? h : Pz(12) + gi + Pz(6) + b.TextW + Pz(10);
                b.R = new Rectangle(x, 0, w, h);
                x += w + gap;
            }
            int bw = x - gap + 2 * pad, bh = h + 2 * pad;
            Rectangle m = MonitorOf(sel);
            int bx = sel.X + (sel.Width - bw) / 2, by = sel.Bottom + Pz(12);
            if (by + bh > m.Bottom - Pz(8))
            {
                by = sel.Top - bh - Pz(12);
                if (by < m.Top + Pz(8)) by = sel.Bottom - bh - Pz(14);
            }
            bx = Math.Max(m.Left + Pz(8), Math.Min(m.Right - bw - Pz(8), bx));
            by = Math.Max(m.Top + Pz(8), Math.Min(m.Bottom - bh - Pz(8), by));
            barRect = new Rectangle(bx, by, bw, bh);
            foreach (BarItem b in bar) b.R.Offset(bx + pad, by + pad);
        }

        void PaintBar(Graphics g)
        {
            Rectangle r = barRect;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = r.Height / 2f;
            for (int i = Ds.Brushes.High ? 0 : 6; i >= 1; i--)
            {
                RectangleF sh = r;
                sh.Inflate(i * S, i * S);
                sh.Offset(0, 3 * S);
                using (GraphicsPath p = Theme.Round(sh, radius + i * S))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(9 * (7 - i) / 3, 0, 0, 0))) g.FillPath(b, p);
            }
            using (GraphicsPath p = Theme.Round(r, radius))
            using (SolidBrush bg = new SolidBrush(Ds.Gdi(Palette.HudBack(Ds.Argb(0.94, 30, 30, 32))))) g.FillPath(bg, p);
            RectangleF hl = r;
            hl.Inflate(-0.5f, -0.5f);
            using (GraphicsPath p = Theme.Round(hl, radius - 0.5f))
            using (Pen line = new Pen(Ds.Gdi(Palette.HudLine), 1f)) g.DrawPath(line, p);
            int gi = Pz(16);
            double stroke = Math.Max(1.4, 1.7 * S);
            for (int i = 0; i < bar.Count; i++)
            {
                BarItem b = bar[i];
                bool hot = i == barHot, press = hot && i == barDown;
                // The main button never lighter than the accent, so its white label keeps 4.5:1.
                Color fill = b.Primary ? Ds.Gdi(Ds.Brushes.Deeper(Ds.Brushes.Accent, press ? 0.80 : hot ? 0.90 : 1))
                           : hot ? Ds.Gdi(Palette.HudWash(Ds.Argb((press ? 70 : 46) / 255.0, 255, 255, 255))) : Color.Empty;
                if (fill != Color.Empty)
                    using (GraphicsPath p = Theme.Round(b.R, b.R.Height / 2f))
                    using (SolidBrush br = new SolidBrush(fill)) g.FillPath(br, p);
                System.Windows.Media.Color ink = b.Primary ? Palette.OnHudFill(Palette.HudLabel) : b.Close && !hot ? Palette.HudLabel2 : Palette.HudLabel;
                if (b.Close)
                {
                    g.DrawImage(Ink.GlyphBitmap("close", gi, ink, stroke), b.R.X + (b.R.Width - gi) / 2, b.R.Y + (b.R.Height - gi) / 2, gi, gi);
                    continue;
                }
                int ix = b.R.X + Pz(12);
                g.DrawImage(Ink.GlyphBitmap(b.Icon, gi, ink, stroke), ix, b.R.Y + (b.R.Height - gi) / 2, gi, gi);
                Rectangle tr = new Rectangle(ix + gi + Pz(6), b.R.Y, b.R.Right - ix - gi - Pz(6), b.R.Height);
                TextKit.Draw(g, b.Label, barFont, tr, Ds.Gdi(ink), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
        }

        int BarAt(Point p)
        {
            for (int i = 0; i < bar.Count; i++) if (bar[i].R.Contains(p)) return i;
            return -1;
        }

        // Starts adjusting r: handles and the bar appear.
        void Adjust(Rectangle r)
        {
            adjusting = true;
            interacted = true;
            dragging = false;
            down = false;
            sel = r;
            windowSel = resultWindow != IntPtr.Zero ? r : Rectangle.Empty;
            BuildBar();
            Present();
        }

        // ---- Usage hint at the top of each monitor. It hides once the user starts selecting, and while the cursor
        // is near it so whatever lies underneath can be seen.

        bool HintOn { get { return !interacted && !nearHint; } }

        bool NearHint(Point p)
        {
            foreach (Hint h in hints) if (h.Zone.Contains(p)) return true;
            return false;
        }

        void BuildHints()
        {
            foreach (Hint old in hints) { old.OnBright.Dispose(); old.OnDim.Dispose(); }
            hints.Clear();
            string text;
            if (I18n.IsSpanish)
            {
                text = mode == Mode.Image
                        ? (allInOne ? "Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Despu\u00E9s eliges qu\u00E9 hacer  \u00B7  C: color  \u00B7  May\u00FAs: medir  \u00B7  Esc: cancelar"
                           : adjustFirst ? "Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Despu\u00E9s la ajustas  \u00B7  C: color  \u00B7  May\u00FAs: medir  \u00B7  Esc: cancelar"
                                    : "Arrastra para capturar un \u00E1rea  \u00B7  Clic: ventana o pantalla entera  \u00B7  C: color  \u00B7  May\u00FAs: medir  \u00B7  Esc: cancelar")
                        : mode == Mode.Video ? "Grabar v\u00EDdeo  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : mode == Mode.Text ? "Copiar texto  \u00B7  Arrastra sobre el texto o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : mode == Mode.Gif ? "Grabar GIF  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : "Captura con desplazamiento  \u00B7  Arrastra sobre la parte que se mueve, sin la cabecera fija  \u00B7  Esc: cancelar";
            }
            else
            {
                text = mode == Mode.Image
                        ? (allInOne ? "Drag an area or click a window  \u00B7  Then choose what to do  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel"
                           : adjustFirst ? "Drag an area or click a window  \u00B7  Then adjust it  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel"
                                    : "Drag to capture an area  \u00B7  Click: window or full screen  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel")
                        : mode == Mode.Video ? "Record video  \u00B7  Drag an area or click a window  \u00B7  Esc: cancel"
                        : mode == Mode.Text ? "Copy text  \u00B7  Drag over text or click a window  \u00B7  Esc: cancel"
                        : mode == Mode.Gif ? "Record GIF  \u00B7  Drag an area or click a window  \u00B7  Esc: cancel"
                        : "Scrolling capture  \u00B7  Drag over the scrolling area, excluding fixed header  \u00B7  Esc: cancel";
            }
            string icon = mode == Mode.Image ? "area" : mode == Mode.Video ? "video" : mode == Mode.Gif ? "gif" : mode == Mode.Text ? "textscan" : "scroll";
            HintPart[] parts = SplitHint(text.Split(new string[] { "  \u00B7  " }, StringSplitOptions.None));
            Rectangle client = new Rectangle(0, 0, bright.Width, bright.Height);
            foreach (Mon mon in mons)
            {
                float s = mon.Scale;
                using (Font bold = new Font("Segoe UI Semibold", (float)Math.Round(13 * s), GraphicsUnit.Pixel))
                using (Font reg = new Font("Segoe UI", (float)Math.Round(13 * s), GraphicsUnit.Pixel))
                using (Font key = new Font("Segoe UI Semibold", (float)Math.Round(11.5 * s), GraphicsUnit.Pixel))
                {
                    HintFonts f = new HintFonts { Bold = bold, Reg = reg, Key = key };
                    Rectangle pill = HintPill(mon.R, s, parts, f);
                    int sh = (int)Math.Ceiling(9 * s) + 2;
                    Rectangle r = Rectangle.Intersect(Rectangle.FromLTRB(pill.Left - sh, pill.Top - sh, pill.Right + sh, pill.Bottom + sh + (int)Math.Ceiling(3 * s)), client);
                    if (r.Width <= 0 || r.Height <= 0) continue;
                    Hint h = new Hint();
                    h.R = r;
                    h.Zone = Rectangle.Inflate(r, (int)Math.Round(24 * s), (int)Math.Round(24 * s));
                    h.OnBright = HintPatch(bright, r, pill, s, icon, parts, f);
                    h.OnDim = HintPatch(dimmed, r, pill, s, icon, parts, f);
                    hints.Add(h);
                }
            }
        }

        // One piece of the hint: "C: color" is the key C and what it does; the first piece (the action) has no key.
        class HintPart { public string Key, Label; public int KeyW, LabelW; }
        class HintFonts { public Font Bold, Reg, Key; }

        static HintPart[] SplitHint(string[] parts)
        {
            HintPart[] r = new HintPart[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int c = i > 0 ? parts[i].IndexOf(": ", StringComparison.Ordinal) : -1;
                r[i] = c > 0 ? new HintPart { Key = parts[i].Substring(0, c), Label = parts[i].Substring(c + 2) } : new HintPart { Label = parts[i] };
            }
            return r;
        }

        // The whole hint (capsule, key caps, words) at `at` on any surface, for previews and tests; text joins its pieces
        // with "  ·  " as the picker's own hints do.
        internal static void PaintHintPreview(Graphics g, Point at, float s, string icon, string text)
        {
            HintPart[] parts = SplitHint(text.Split(new string[] { "  ·  " }, StringSplitOptions.None));
            using (Font bold = new Font("Segoe UI Semibold", (float)Math.Round(13 * s), GraphicsUnit.Pixel))
            using (Font reg = new Font("Segoe UI", (float)Math.Round(13 * s), GraphicsUnit.Pixel))
            using (Font key = new Font("Segoe UI Semibold", (float)Math.Round(11.5 * s), GraphicsUnit.Pixel))
            {
                HintFonts f = new HintFonts { Bold = bold, Reg = reg, Key = key };
                Rectangle pill = HintPill(new Rectangle(0, 0, 100000, 100000), s, parts, f);
                pill.Location = at;
                SmoothingMode sm = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PaintHint(g, pill, s, icon, parts);
                g.SmoothingMode = sm;
                HintText(g, Point.Empty, pill, s, parts, f);
            }
        }

        static Dib HintPatch(Dib under, Rectangle r, Rectangle pill, float s, string icon, HintPart[] parts, HintFonts f)
        {
            Dib d = new Dib(r.Width, r.Height);
            Native.BitBlt(d.Dc, 0, 0, r.Width, r.Height, under.Dc, r.X, r.Y, Native.SRCCOPY);
            using (Graphics g = d.Graphics())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TranslateTransform(-r.X, -r.Y);
                PaintHint(g, pill, s, icon, parts);
            }
            // The overlay is an opaque window and the capsule is solid under the text: the system's ClearType, as sharp
            // as any other text on screen.
            using (Graphics g = d.DcGraphics()) HintText(g, new Point(-r.X, -r.Y), pill, s, parts, f);
            Native.GdiFlush();
            return d;
        }

        const TextFormatFlags HintFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        // Spacing, in pixels at scale s.
        static int Hs(double v, float s) { return (int)Math.Round(v * s); }

        static Rectangle HintPill(Rectangle mon, float s, HintPart[] parts, HintFonts f)
        {
            int w = Hs(14, s) + Hs(16, s) + Hs(8, s) + Hs(16, s);
            for (int i = 0; i < parts.Length; i++)
            {
                HintPart p = parts[i];
                p.LabelW = TextRenderer.MeasureText(p.Label, i == 0 ? f.Bold : f.Reg, Size.Empty, HintFlags).Width;
                p.KeyW = p.Key == null ? 0 : TextRenderer.MeasureText(p.Key, f.Key, Size.Empty, HintFlags).Width + 2 * Hs(7, s);
                if (i > 0) w += i == 1 ? Hs(25, s) : Hs(16, s);
                if (p.Key != null) w += p.KeyW + Hs(6, s);
                w += p.LabelW;
            }
            int h = Hs(40, s);
            return new Rectangle(mon.X + (mon.Width - w) / 2, mon.Y + Hs(16, s), w, h);
        }

        // Where each key cap and label goes (the same for the capsule and for its text).
        static void HintLayout(Rectangle pill, float s, HintPart[] parts, out Rectangle[] keys, out Rectangle[] labels, out int divider)
        {
            keys = new Rectangle[parts.Length];
            labels = new Rectangle[parts.Length];
            divider = 0;
            int x = pill.X + Hs(14, s) + Hs(16, s) + Hs(8, s), cy = pill.Y + pill.Height / 2, kh = Hs(22, s);
            for (int i = 0; i < parts.Length; i++)
            {
                HintPart p = parts[i];
                if (i == 1) { divider = x + Hs(12, s); x += Hs(25, s); }
                else if (i > 1) x += Hs(16, s);
                if (p.Key != null)
                {
                    keys[i] = new Rectangle(x, cy - kh / 2, p.KeyW, kh);
                    x += p.KeyW + Hs(6, s);
                }
                labels[i] = new Rectangle(x, pill.Y, p.LabelW + 2, pill.Height);
                x += p.LabelW;
            }
        }

        // Esc cancels: its key is red; the rest are blue.
        static bool IsCancel(HintPart p) { return p.Key == "Esc"; }

        static Color KeyInk(HintPart p) { return Ds.Gdi(IsCancel(p) ? Palette.HudDanger : Palette.HudAccent); }

        // Dark HUD capsule: icon, then a hairline, then each shortcut as a coloured key cap (its text comes in HintText).
        static void PaintHint(Graphics g, Rectangle pill, float s, string icon, HintPart[] parts)
        {
            int gi = Hs(16, s), h = pill.Height;
            float radius = h / 2f;
            bool high = Ds.Brushes.High;
            for (int i = high ? 0 : 8; i >= 1; i--)
            {
                RectangleF sh = pill;
                sh.Inflate(i * s, i * s);
                sh.Offset(0, 3 * s);
                using (GraphicsPath p = Theme.Round(sh, radius + i * s))
                using (SolidBrush br = new SolidBrush(Color.FromArgb((int)(7 * (9 - i) / 4.0), 0, 0, 0))) g.FillPath(br, p);
            }
            using (GraphicsPath p = Theme.Round(pill, radius))
            using (SolidBrush bg = new SolidBrush(Ds.Gdi(Palette.Hud)))
                g.FillPath(bg, p);
            RectangleF hl = pill;
            hl.Inflate(-0.5f, -0.5f);
            using (GraphicsPath p = Theme.Round(hl, radius - 0.5f))
            using (Pen line = new Pen(Ds.Gdi(Palette.HudLine), 1f))
                g.DrawPath(line, p);
            int cy = pill.Y + h / 2;
            g.DrawImage(Ink.GlyphBitmap(icon, gi, Palette.HudLabel, Math.Max(1.4, 1.6 * s)), pill.X + Hs(14, s), cy - gi / 2, gi, gi);
            Rectangle[] keys, labels;
            int divider;
            HintLayout(pill, s, parts, out keys, out labels, out divider);
            if (divider > 0)
            {
                int dh = Hs(9, s);
                using (Pen line = new Pen(Ds.Gdi(Palette.HudFaint(Ds.Argb(0.22, 255, 255, 255))), Math.Max(1f, s)))
                    g.DrawLine(line, divider, cy - dh, divider, cy + dh);
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Key == null) continue;
                Color ink = KeyInk(parts[i]);
                RectangleF k = keys[i];
                float kr = Math.Max(4f, 6f * s);
                if (!high)
                    using (GraphicsPath p = Theme.Round(k, kr))
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(48, ink))) g.FillPath(fill, p);
                k.Inflate(-0.5f, -0.5f);
                using (GraphicsPath p = Theme.Round(k, kr - 0.5f))
                using (Pen edge = new Pen(Color.FromArgb(high ? 255 : 120, ink), 1f)) g.DrawPath(edge, p);
            }
        }

        // The words, with GDI (ClearType) straight into the patch: the action in white, keys in their colour, what they
        // do in a light grey that reads well on the capsule.
        static void HintText(Graphics g, Point offset, Rectangle pill, float s, HintPart[] parts, HintFonts f)
        {
            Rectangle[] keys, labels;
            int divider;
            HintLayout(pill, s, parts, out keys, out labels, out divider);
            Color white = Opaque(Ds.Gdi(Palette.HudLabel)), soft = Opaque(Ds.Gdi(Palette.HudFaint(Ds.Argb(0.8, 255, 255, 255))));
            for (int i = 0; i < parts.Length; i++)
            {
                HintPart p = parts[i];
                if (p.Key != null)
                {
                    Rectangle k = keys[i];
                    k.Offset(offset);
                    k.Offset(0, -Math.Max(0, Hs(0.5, s)));
                    TextRenderer.DrawText(g, p.Key, f.Key, k, Opaque(KeyInk(p)), HintFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                Rectangle l = labels[i];
                l.Offset(offset);
                TextRenderer.DrawText(g, p.Label, i == 0 ? f.Bold : f.Reg, l, i == 0 ? white : soft, HintFlags | TextFormatFlags.VerticalCenter);
            }
        }

        // GDI text has no transparency: a translucent colour is mixed with the capsule's dark first.
        static Color Opaque(Color c)
        {
            if (c.A == 255) return c;
            Color bg = Ds.Gdi(Palette.Hud);
            double a = c.A / 255.0;
            return Color.FromArgb((int)Math.Round(c.R * a + bg.R * (1 - a)), (int)Math.Round(c.G * a + bg.G * (1 - a)), (int)Math.Round(c.B * a + bg.B * (1 - a)));
        }

        // ---- Input

        // Window under the cursor (or its whole monitor over the desktop). Returns whether it changed.
        bool UpdateHover()
        {
            Point sp = ToScreen(cur);
            Rectangle r = Rectangle.Empty;
            IntPtr h = IntPtr.Zero;
            foreach (Grabber.Win w in wins)
            {
                if (w.Bounds.Contains(sp)) { r = w.Bounds; h = w.Handle; break; }
            }
            if (r.IsEmpty) { r = curMon; r.Offset(vs.X, vs.Y); } // over the desktop: whole monitor
            r.Intersect(vs);
            r.Offset(-vs.X, -vs.Y);
            if (r == hover && h == hoverHandle) return false;
            hover = r;
            hoverHandle = h;
            return true;
        }

        static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);
        }

        // While adjusting: corner (0, 2, 4, 6) or edge (1, 3, 5, 7) under p, anywhere along it; 8 = inside, to move the
        // area; -1 = outside.
        int HandleAt(Point p)
        {
            Rectangle r = sel;
            int reach = Math.Max(Pz(4), Math.Min(Pz(9), Math.Min(r.Width, r.Height) / 3));
            Point[] hs = Handles(r);
            for (int i = 0; i < 8; i += 2)
                if (Math.Abs(p.X - hs[i].X) <= reach && Math.Abs(p.Y - hs[i].Y) <= reach) return i;
            bool alongX = p.X >= r.Left - reach && p.X <= r.Right + reach, alongY = p.Y >= r.Top - reach && p.Y <= r.Bottom + reach;
            if (alongX && Math.Abs(p.Y - r.Top) <= reach) return 1;
            if (alongY && Math.Abs(p.X - r.Right) <= reach) return 3;
            if (alongX && Math.Abs(p.Y - r.Bottom) <= reach) return 5;
            if (alongY && Math.Abs(p.X - r.Left) <= reach) return 7;
            return r.Contains(p) ? 8 : -1;
        }

        static readonly Cursor[] HandleCursors =
        {
            Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE, Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE, Cursors.SizeAll
        };

        void SetCursor(Cursor c)
        {
            if (c == shownCursor) return;
            shownCursor = c;
            Cursor = c;
        }

        Rectangle Resized(int h, Point p)
        {
            int dx = p.X - grabFrom.X, dy = p.Y - grabFrom.Y;
            Rectangle r = grabSel;
            if (h == 8) { r.Offset(dx, dy); return Keep(r); }
            int l = r.Left, t = r.Top, rt = r.Right, b = r.Bottom;
            if (h == 0 || h == 6 || h == 7) l += dx;
            if (h == 2 || h == 3 || h == 4) rt += dx;
            if (h == 0 || h == 1 || h == 2) t += dy;
            if (h == 4 || h == 5 || h == 6) b += dy;
            return Inside(Rectangle.FromLTRB(Math.Min(l, rt - 1), Math.Min(t, b - 1), Math.Max(rt, l + 1), Math.Max(b, t + 1)));
        }

        // A moved area stays inside the virtual screen.
        Rectangle Keep(Rectangle r)
        {
            r.X = Math.Max(0, Math.Min(bright.Width - r.Width, r.X));
            r.Y = Math.Max(0, Math.Min(bright.Height - r.Height, r.Y));
            return r;
        }

        // A resized area is clipped to the virtual screen and keeps at least one pixel.
        Rectangle Inside(Rectangle r)
        {
            r = Rectangle.Intersect(r, Area);
            if (r.Width < 1) r.Width = 1;
            if (r.Height < 1) r.Height = 1;
            return r;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Location == cur) return;
            cur = e.Location;
            if (!curMon.Contains(cur)) TrackMonitor();
            if (measuring)
            {
                // The button went up where no release reached us (Alt+Tab, a system prompt): the measure ends there.
                if ((e.Button & MouseButtons.Left) == 0) { measuring = false; measured = true; }
                else MeasureTo(cur);
                Present();
                return;
            }
            if (adjusting)
            {
                if (grab >= 0)
                {
                    Rectangle r = Resized(grab, cur);
                    if (r != sel) { sel = r; Present(); }
                    return;
                }
                int bh = BarAt(cur);
                if (bh != barHot) { barHot = bh; Present(); }
                bool onBar = barRect.Contains(cur);
                int h = onBar ? -1 : HandleAt(cur);
                SetCursor(bh >= 0 ? Cursors.Hand : onBar ? Cursors.Default : h >= 0 ? HandleCursors[h] : Cursors.Cross);
                return;
            }
            bool changed = false;
            if (down && !dragging && (Math.Abs(cur.X - start.X) > Pz(4) || Math.Abs(cur.Y - start.Y) > Pz(4)))
            {
                dragging = true;
                interacted = true;
                changed = true;
            }
            if (dragging)
            {
                Rectangle r = Normalize(start, cur);
                if (r != sel) { sel = r; changed = true; }
            }
            else if (!down && UpdateHover()) changed = true;
            bool near = NearHint(cur);
            if (near != nearHint) { nearHint = near; changed = true; }
            if (loupe) changed = true;   // the loupe follows the pointer
            if (changed) Present();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                if (down || dragging || adjusting) Reset();
                else Cancel();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (!adjusting && ToolPress(e.Location)) return;
            if (adjusting)
            {
                barDown = BarAt(e.Location);
                if (barDown >= 0) { Present(); return; }
                if (barRect.Contains(e.Location)) return; // between its buttons: a near miss must not drop the area
                grab = HandleAt(e.Location);
                if (grab >= 0)
                {
                    grabFrom = e.Location;
                    grabSel = sel;
                    if (e.Clicks == 2 && grab == 8) { grab = -1; Choose(Mode.Image); }
                    return;
                }
                // Outside the area: start a new one (or take the window under the cursor if it is just a click).
                adjusting = false;
                bar.Clear();
                sel = Rectangle.Empty;
                resultWindow = IntPtr.Zero;
                cur = e.Location;
                TrackMonitor();
                UpdateHover();
                SetCursor(Cursors.Cross);
                Present();
            }
            down = true;
            start = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (measuring) { measuring = false; measured = true; Present(); return; }
            if (adjusting)
            {
                if (barDown >= 0)
                {
                    int i = barDown;
                    barDown = -1;
                    if (BarAt(e.Location) == i)
                    {
                        if (bar[i].Close) Cancel();
                        else Choose(bar[i].Mode);
                    }
                    else Present();
                    return;
                }
                grab = -1;
                return;
            }
            if (!down) return;
            Rectangle r;
            IntPtr w = IntPtr.Zero;
            if (dragging)
            {
                if (sel.Width < 4 || sel.Height < 4) { Reset(); return; }
                r = sel;
            }
            else { r = hover; w = hoverHandle; }
            if (allInOne || adjustFirst) { resultWindow = w; Adjust(r); return; }
            Accept(r, w);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ToolKey(e.KeyCode)) return;
            int step = e.Shift ? 10 : 1;
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    if (adjusting) Reset();
                    else Cancel();
                    break;
                case Keys.Enter:
                    if (adjusting) Choose(Mode.Image);
                    else Accept(dragging ? sel : hover, dragging ? IntPtr.Zero : hoverHandle);
                    break;
                case Keys.Left: Nudge(-step, 0, e.Control); break;
                case Keys.Right: Nudge(step, 0, e.Control); break;
                case Keys.Up: Nudge(0, -step, e.Control); break;
                case Keys.Down: Nudge(0, step, e.Control); break;
            }
        }

        // Arrow keys: move the cursor for pixel-precise selection, or while adjusting move the area (Ctrl: resize it).
        void Nudge(int dx, int dy, bool resize)
        {
            if (!adjusting)
            {
                Cursor.Position = new Point(Cursor.Position.X + dx, Cursor.Position.Y + dy);
                return;
            }
            Rectangle r = resize ? Inside(new Rectangle(sel.X, sel.Y, Math.Max(1, sel.Width + dx), Math.Max(1, sel.Height + dy)))
                                 : Keep(new Rectangle(sel.X + dx, sel.Y + dy, sel.Width, sel.Height));
            if (r == sel) return;
            sel = r;
            Present();
        }

        void Reset()
        {
            down = false;
            dragging = false;
            adjusting = false;
            grab = barHot = barDown = -1;
            bar.Clear();
            sel = Rectangle.Empty;
            resultWindow = IntPtr.Zero;
            SetCursor(Cursors.Cross);
            UpdateHover();
            Present();
        }

        // The area keeps the window's identity (name, rounded corners) only while it is exactly that window.
        void Choose(Mode m)
        {
            chosen = m;
            Accept(sel, sel == windowSel ? resultWindow : IntPtr.Zero);
        }

        void Accept(Rectangle r, IntPtr window)
        {
            if (r.Width < 1 || r.Height < 1) { Cancel(); return; }
            Rectangle local = Rectangle.Intersect(r, new Rectangle(0, 0, bright.Width, bright.Height));
            if ((chosen == Mode.Image || chosen == Mode.Text) && local.Width > 0 && local.Height > 0)
            {
                crop = Crop(bright, local);
                cropAt = local;
                cropAt.Offset(vs.X, vs.Y);
            }
            r.Offset(vs.X, vs.Y);
            result = r;
            resultWindow = window;
            DialogResult = DialogResult.OK;
            Close();
        }

        // Copy of part of the frozen screen.
        static Bitmap Crop(Dib d, Rectangle r)
        {
            Native.GdiFlush();
            Bitmap b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            BitmapData bd = b.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int y = 0; y < r.Height; y++)
                    Native.CopyMemory(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride),
                                      new IntPtr(d.Bits.ToInt64() + ((long)(r.Y + y) * d.Width + r.X) * 4), new UIntPtr((uint)(r.Width * 4)));
            }
            finally { b.UnlockBits(bd); }
            return b;
        }

        void Cancel()
        {
            result = Rectangle.Empty;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            bright.Dispose();
            dimmed.Dispose();
            if (back != null) { back.Dispose(); back = null; }
            foreach (Hint h in hints) { h.OnBright.Dispose(); h.OnDim.Dispose(); }
            hints.Clear();
            if (handleSprite != null) { handleSprite.Dispose(); handleSprite = null; }
            if (barSprite != null) { barSprite.Dispose(); barSprite = null; }
            if (previewSprite != null) { previewSprite.Dispose(); previewSprite = null; }
            if (previewPic != null) { previewPic.Dispose(); previewPic = null; }
            if (labelFont != null) labelFont.Dispose();
            if (barFont != null) barFont.Dispose();
        }
    }
}
