// Stackshot - Editor canvas: draw, select, move, crop and undo.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Stackshot
{
    public class Canvas : Control
    {
        class Snapshot { public List<Shape> Shapes; public RectangleF Crop; }
        enum Drag { None, Draw, Move, Resize }

        public static readonly float[] Weights = { 0.6f, 1f, 1.6f };

        public Bitmap Img { get; private set; }
        public float Ui = 1f;
        public Tool Tool = Tool.Rect;
        public Color Color = Theme.Palette[0];
        public int Weight = 1;
        public RectangleF Crop;
        public event EventHandler Changed, StateChanged;
        List<Shape> shapes = new List<Shape>();
        readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();
        readonly float baseStroke, baseFont;
        Shape cur, selected, hover, orig, editing;
        Drag drag;
        int handle;
        bool committed, skipNextDown;
        PointF dragFrom;
        float k = 1f;
        PointF off;
        Bitmap view, dots;
        Size viewFor;
        RectangleF viewCrop;
        TextBox box;
        PointF boxAt;
        // Presentation backdrop: with BgOn, the capture is shown (and exported) on Bg's background.
        public Settings Bg;
        public bool BgOn;
        public bool Live;          // while a backdrop slider is dragged: fast scaling, high quality afterwards
        bool viewLive;
        Rectangle frameScreen;
        float radiusScreen;
        Bitmap bgView, roundView;
        string bgKey, roundKey;

        public Canvas(Bitmap img)
        {
            Img = img;
            Crop = new RectangleF(0, 0, img.Width, img.Height);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(19, 20, 28);
            int m = Math.Min(img.Width, img.Height);
            baseStroke = Math.Max(4f, m / 180f);
            baseFont = Math.Max(18f, m / 30f);
        }

        public bool Typing { get { return box != null; } }
        public bool HasSelection { get { return selected != null; } }
        public bool CanUndo { get { return undo.Count > 0 || box != null; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public Color ActiveColor { get { return selected != null ? selected.Color : Color; } }
        public int ActiveWeight { get { return selected != null ? selected.Weight : Weight; } }
        // Output size: the whole frame when the backdrop is on.
        public Size OutputSize
        {
            get
            {
                if (!BgOn || Bg == null) return OutputSizeRaw;
                Size frame;
                Rectangle inner;
                Backdrop.Measure(OutputSizeRaw, Bg, false, out frame, out inner);
                return frame;
            }
        }

        Size OutputSizeRaw { get { return new Size(Math.Max(1, (int)Math.Round(Crop.Width)), Math.Max(1, (int)Math.Round(Crop.Height))); } }

        int Pu(float v) { return (int)Math.Round(v * Ui); }
        float StrokeFor(int w) { return baseStroke * Weights[w]; }
        float FontFor(int w) { return baseFont * (0.75f + 0.3f * w); }

        void Fire(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        void Fit()
        {
            float pad = 28 * Ui;
            float aw = Math.Max(1f, Width - 2 * pad), ah = Math.Max(1f, Height - 2 * pad);
            if (BgOn && Bg != null)
            {
                // Fit the whole frame (backdrop included); the capture sits inside it.
                Size cs = OutputSizeRaw, frame;
                Rectangle inner;
                Backdrop.Measure(cs, Bg, false, out frame, out inner);
                k = Math.Min(ShotStack.MaxZoom(cs), Math.Min(aw / frame.Width, ah / frame.Height));
                float fx = (float)Math.Round((Width - frame.Width * k) / 2f), fy = (float)Math.Round((Height - frame.Height * k) / 2f);
                off = new PointF((float)Math.Round(fx + inner.X * k), (float)Math.Round(fy + inner.Y * k));
                frameScreen = new Rectangle((int)fx, (int)fy, Math.Max(1, (int)Math.Round(frame.Width * k)), Math.Max(1, (int)Math.Round(frame.Height * k)));
                radiusScreen = Backdrop.RadiusFor(cs, Bg) * k;
                return;
            }
            k = Math.Min(ShotStack.MaxZoom(new Size((int)Crop.Width, (int)Crop.Height)), Math.Min(aw / Crop.Width, ah / Crop.Height));
            off = new PointF((float)Math.Round((Width - Crop.Width * k) / 2f), (float)Math.Round((Height - Crop.Height * k) / 2f));
        }

        // Background and shadow at screen scale, rebuilt only when something changes.
        Bitmap BgView(Rectangle ir)
        {
            string key = Bg.BgPreset + "|" + Bg.BgPadding + "|" + Bg.BgRadius + "|" + Bg.BgShadow + "|" + Bg.BgRatio + "|" + frameScreen + "|" + ir;
            if (bgView != null && key == bgKey) return bgView;
            if (bgView != null) bgView.Dispose();
            Rectangle inner = new Rectangle(ir.X - frameScreen.X, ir.Y - frameScreen.Y, ir.Width, ir.Height);
            bgView = Backdrop.Background(frameScreen.Size, inner, Bg, (int)Math.Round(radiusScreen));
            bgKey = key;
            return bgView;
        }

        // Scaled capture with antialiased rounded corners.
        Bitmap RoundView(Bitmap v)
        {
            string key = v.GetHashCode() + "|" + v.Size + "|" + Math.Round(radiusScreen, 1);
            if (roundView != null && key == roundKey) return roundView;
            if (roundView != null) roundView.Dispose();
            roundView = Backdrop.Rounded(v, radiusScreen);
            roundKey = key;
            return roundView;
        }

        PointF ToImg(Point p, bool clamp)
        {
            float x = Crop.X + (p.X - off.X) / k, y = Crop.Y + (p.Y - off.Y) / k;
            if (!clamp) return new PointF(x, y);
            return new PointF(Math.Max(Crop.Left, Math.Min(Crop.Right, x)), Math.Max(Crop.Top, Math.Min(Crop.Bottom, y)));
        }

        Point ToScreen(PointF p)
        {
            return new Point((int)Math.Round(off.X + (p.X - Crop.X) * k), (int)Math.Round(off.Y + (p.Y - Crop.Y) * k));
        }

        Rectangle ScreenRect(RectangleF r)
        {
            Point a = ToScreen(r.Location), b = ToScreen(new PointF(r.Right, r.Bottom));
            return Rectangle.FromLTRB(a.X, a.Y, b.X, b.Y);
        }

        // Cached scaled image so painting while dragging is instant.
        Bitmap View()
        {
            Size want = new Size(Math.Max(1, (int)Math.Round(Crop.Width * k)), Math.Max(1, (int)Math.Round(Crop.Height * k)));
            if (view != null && viewFor == want && viewCrop == Crop && (viewLive == Live || Live)) return view;
            if (view != null) view.Dispose();
            view = new Bitmap(want.Width, want.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(view))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = (k == 1f || k == 2f) ? InterpolationMode.NearestNeighbor : Live ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(Img, new Rectangle(0, 0, want.Width, want.Height), Crop.X, Crop.Y, Crop.Width, Crop.Height, GraphicsUnit.Pixel, ia);
            }
            viewFor = want;
            viewLive = Live;
            viewCrop = Crop;
            return view;
        }

        // Subtle dot grid, rendered once per size.
        Bitmap Dots()
        {
            if (dots != null && dots.Size == ClientSize) return dots;
            if (dots != null) dots.Dispose();
            dots = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(dots))
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(34, 36, 50)))
            {
                g.Clear(BackColor);
                int step = Math.Max(12, Pu(20)), d = Math.Max(2, Pu(2));
                for (int yy = step / 2; yy < dots.Height; yy += step)
                    for (int xx = step / 2; xx < dots.Width; xx += step)
                        g.FillRectangle(dot, xx, yy, d, d);
            }
            return dots;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Fit();
            g.DrawImageUnscaled(Dots(), 0, 0);
            Bitmap v = View();
            Rectangle ir = new Rectangle((int)off.X, (int)off.Y, v.Width, v.Height);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool bg = BgOn && Bg != null;
            if (bg)
            {
                g.DrawImageUnscaled(BgView(ir), frameScreen.X, frameScreen.Y);
                g.DrawImageUnscaled(radiusScreen >= 0.5f ? RoundView(v) : v, ir.X, ir.Y);
            }
            else
            {
                for (int i = 1; i <= 6; i++) // soft shadow under the capture
                {
                    Rectangle sr = ir;
                    sr.Inflate(Pu(i * 1.6f), Pu(i * 1.6f));
                    sr.Offset(0, Pu(3));
                    using (GraphicsPath p = Theme.Round(sr, Pu(3 + i * 1.6f)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 0, 0, 0))) g.FillPath(b, p);
                }
                g.DrawImageUnscaled(v, ir.X, ir.Y);
                using (Pen p = new Pen(Theme.Border)) g.DrawRectangle(p, ir.X - 1, ir.Y - 1, ir.Width + 1, ir.Height + 1);
            }

            GraphicsState st = g.Save();
            if (bg && radiusScreen >= 0.5f)
            {
                using (GraphicsPath clip = Theme.Round(ir, radiusScreen)) g.SetClip(clip);
            }
            else g.SetClip(ir);
            g.TranslateTransform(off.X - Crop.X * k, off.Y - Crop.Y * k);
            g.ScaleTransform(k, k);
            foreach (Shape s in shapes)
            {
                if (s != editing) Painter.Draw(g, s, Img);
            }
            if (cur != null && cur.Kind != Tool.Crop) Painter.Draw(g, cur, Img);
            g.Restore(st);

            if (hover != null && hover != selected && drag == Drag.None && box == null) Outline(g, hover, false);
            if (selected != null && box == null) Outline(g, selected, true);
            if (cur != null && cur.Kind == Tool.Crop) CropOverlay(g, ir, cur.Box);
        }

        static PointF[] Handles(Shape s)
        {
            switch (s.Kind)
            {
                case Tool.Arrow:
                    return new PointF[] { s.A, s.B, s.Mid }; // the third handle (middle) bends the arrow
                case Tool.Text:
                case Tool.Counter:
                    return new PointF[0];
                default:
                    RectangleF b = s.Box;
                    return new PointF[] { new PointF(b.Left, b.Top), new PointF(b.Right, b.Top), new PointF(b.Right, b.Bottom), new PointF(b.Left, b.Bottom) };
            }
        }

        // Dashed outline: faint on hover, blue with handles when selected.
        void Outline(Graphics g, Shape s, bool strong)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = strong ? Theme.Accent : Color.FromArgb(150, Theme.Accent);
            using (Pen pen = new Pen(c, Math.Max(1f, 1.4f * Ui)))
            {
                pen.DashStyle = DashStyle.Dash;
                if (s.Kind == Tool.Arrow)
                {
                    PointF[] pts = s.Curve(32);
                    Point[] sp = new Point[pts.Length];
                    for (int i = 0; i < pts.Length; i++) sp[i] = ToScreen(pts[i]);
                    g.DrawLines(pen, sp);
                }
                else
                {
                    Rectangle r = ScreenRect(Painter.Bounds(s));
                    r.Inflate(Pu(4), Pu(4));
                    g.DrawRectangle(pen, r);
                }
            }
            if (!strong) return;
            int hs = Pu(5);
            PointF[] handles = Handles(s);
            for (int i = 0; i < handles.Length; i++)
            {
                Point p = ToScreen(handles[i]);
                Rectangle hr = new Rectangle(p.X - hs, p.Y - hs, hs * 2, hs * 2);
                // The bend handle is filled blue to tell it apart from the endpoints.
                bool bend = s.Kind == Tool.Arrow && i == 2;
                using (SolidBrush b = new SolidBrush(bend ? Theme.Accent : Color.White)) g.FillEllipse(b, hr);
                using (Pen pen = new Pen(bend ? Color.White : Theme.Accent, Math.Max(1.5f, 2f * Ui))) g.DrawEllipse(pen, hr);
            }
        }

        // Crop: dim outside, rule-of-thirds guides and resulting size inside.
        void CropOverlay(Graphics g, Rectangle ir, RectangleF crop)
        {
            Rectangle cr = ScreenRect(crop);
            cr.Intersect(ir);
            using (Region outside = new Region(ir))
            {
                outside.Exclude(cr);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.FillRegion(b, outside);
            }
            g.SmoothingMode = SmoothingMode.None;
            using (Pen thirds = new Pen(Color.FromArgb(90, 255, 255, 255)))
            {
                for (int i = 1; i < 3; i++)
                {
                    g.DrawLine(thirds, cr.X + cr.Width * i / 3, cr.Y, cr.X + cr.Width * i / 3, cr.Bottom);
                    g.DrawLine(thirds, cr.X, cr.Y + cr.Height * i / 3, cr.Right, cr.Y + cr.Height * i / 3);
                }
            }
            using (Pen pen = new Pen(Color.White, Math.Max(1f, 1.5f * Ui))) g.DrawRectangle(pen, cr);
            int L = Pu(14), t = Math.Max(2, Pu(3));
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, cr.X - t, cr.Y - t, L, t); g.FillRectangle(b, cr.X - t, cr.Y - t, t, L);
                g.FillRectangle(b, cr.Right - L + t, cr.Y - t, L, t); g.FillRectangle(b, cr.Right, cr.Y - t, t, L);
                g.FillRectangle(b, cr.X - t, cr.Bottom, L, t); g.FillRectangle(b, cr.X - t, cr.Bottom - L + t, t, L);
                g.FillRectangle(b, cr.Right - L + t, cr.Bottom, L, t); g.FillRectangle(b, cr.Right, cr.Bottom - L + t, t, L);
            }
            string label = (int)Math.Round(crop.Width) + " \u00D7 " + (int)Math.Round(crop.Height);
            using (Font f = new Font("Segoe UI Semibold", Pu(12), GraphicsUnit.Pixel))
            {
                Size ts = TextRenderer.MeasureText(label, f);
                Rectangle lr = new Rectangle(cr.X, Math.Max(ir.Y, cr.Y - ts.Height - Pu(10)), ts.Width + Pu(14), ts.Height + Pu(6));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = Theme.Round(lr, lr.Height / 2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, Theme.Dark))) g.FillPath(b, p);
                TextRenderer.DrawText(g, label, f, lr, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        Shape ShapeAt(Point screen)
        {
            if (!ScreenRect(Crop).Contains(screen)) return null;
            PointF q = ToImg(screen, false);
            float tol = 6f * Ui / k;
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                if (Hit(shapes[i], q, tol)) return shapes[i];
            }
            return null;
        }

        static bool Hit(Shape s, PointF q, float tol)
        {
            RectangleF b;
            switch (s.Kind)
            {
                case Tool.Rect:
                {
                    // Stroke only, so the user can draw inside a rectangle without grabbing it.
                    b = s.Box;
                    float m = s.Width / 2 + tol;
                    RectangleF outer = RectangleF.Inflate(b, m, m), inner = RectangleF.Inflate(b, -m, -m);
                    return outer.Contains(q) && !(inner.Width > 0 && inner.Height > 0 && inner.Contains(q));
                }
                case Tool.Ellipse:
                {
                    b = s.Box;
                    float rx = Math.Max(1f, b.Width / 2), ry = Math.Max(1f, b.Height / 2);
                    double nxv = (q.X - (b.X + rx)) / rx, nyv = (q.Y - (b.Y + ry)) / ry;
                    double d = Math.Sqrt(nxv * nxv + nyv * nyv);
                    return Math.Abs(d - 1) * Math.Min(rx, ry) <= s.Width / 2 + tol;
                }
                case Tool.Arrow:
                {
                    PointF[] pts = s.Curve(s.Curved ? 32 : 1);
                    float best = float.MaxValue;
                    for (int i = 1; i < pts.Length; i++) best = Math.Min(best, SegmentDistance(q, pts[i - 1], pts[i]));
                    return best <= s.Width * 1.3f + tol;
                }
                case Tool.Counter:
                {
                    float dx = q.X - s.A.X, dy = q.Y - s.A.Y;
                    return Math.Sqrt(dx * dx + dy * dy) <= Painter.CounterRadius(s) + tol;
                }
                default:
                    b = Painter.Bounds(s);
                    b.Inflate(tol, tol);
                    return b.Contains(q);
            }
        }

        static float SegmentDistance(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
            float t = len2 < 1e-6f ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            float cx = a.X + t * dx - p.X, cy = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        int HandleAt(Point screen)
        {
            if (selected == null) return -1;
            PointF[] hs = Handles(selected);
            int r = Pu(8);
            for (int i = hs.Length - 1; i >= 0; i--)
            {
                Point p = ToScreen(hs[i]);
                if (Math.Abs(p.X - screen.X) <= r && Math.Abs(p.Y - screen.Y) <= r) return i;
            }
            return -1;
        }

        void UpdateHover(Point p)
        {
            Shape h = null;
            Cursor c;
            int hh = HandleAt(p);
            if (hh >= 0) c = selected.Kind == Tool.Arrow ? Cursors.SizeAll : (hh == 0 || hh == 2 ? Cursors.SizeNWSE : Cursors.SizeNESW);
            else
            {
                if (Tool != Tool.Crop) h = ShapeAt(p);
                c = h != null ? Cursors.SizeAll : (Tool == Tool.Text ? Cursors.IBeam : Cursors.Cross);
            }
            if (h != hover) { hover = h; Invalidate(); }
            if (Cursor != c) Cursor = c;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (skipNextDown) { skipNextDown = false; return; }
            if (box != null) { CommitText(); return; }
            Focus();
            if (e.Button == MouseButtons.Right) { SelectShape(null); return; }
            if (e.Button != MouseButtons.Left) return;
            PointF p = ToImg(e.Location, true);
            int h = HandleAt(e.Location);
            if (h >= 0)
            {
                drag = Drag.Resize;
                handle = h;
                Begin(ToImg(e.Location, false));
                return;
            }
            if (Tool != Tool.Crop)
            {
                Shape hit = ShapeAt(e.Location);
                if (hit != null)
                {
                    SelectShape(hit);
                    if (e.Clicks >= 2 && hit.Kind == Tool.Text) { StartText(hit.A, hit); return; }
                    drag = Drag.Move;
                    Begin(ToImg(e.Location, false));
                    return;
                }
            }
            SelectShape(null);
            if (Tool == Tool.Text) { StartText(p, null); return; }
            if (Tool == Tool.Counter) { AddCounter(p); return; }
            cur = NewShape(Tool);
            cur.A = p;
            cur.B = p;
            drag = Drag.Draw;
        }

        Shape NewShape(Tool t)
        {
            Shape s = new Shape();
            s.Kind = t;
            s.Color = Color;
            s.Weight = Weight;
            s.Width = StrokeFor(Weight);
            s.FontPx = FontFor(Weight);
            return s;
        }

        void Begin(PointF p)
        {
            dragFrom = p;
            orig = selected.Clone();
            committed = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            switch (drag)
            {
                case Drag.Draw:
                {
                    PointF p = ToImg(e.Location, true);
                    if ((ModifierKeys & Keys.Shift) != 0) p = Constrain(cur.A, p, cur.Kind);
                    cur.B = p;
                    Invalidate();
                    return;
                }
                case Drag.Move:
                {
                    PointF p = ToImg(e.Location, false);
                    float dx = p.X - dragFrom.X, dy = p.Y - dragFrom.Y;
                    if (!committed)
                    {
                        if (Math.Abs(dx) * k < 2 && Math.Abs(dy) * k < 2) return;
                        Commit();
                        committed = true;
                    }
                    selected.Offset(orig, dx, dy);
                    Invalidate();
                    return;
                }
                case Drag.Resize:
                {
                    if (!committed) { Commit(); committed = true; }
                    // The middle handle bends the arrow (snaps back to straight near the line).
                    if (selected.Kind == Tool.Arrow && handle == 2) selected.SetMid(ToImg(e.Location, false), 8f * Ui / k);
                    else ResizeShape(selected, orig, handle, ToImg(e.Location, true), (ModifierKeys & Keys.Shift) != 0);
                    Invalidate();
                    return;
                }
            }
            UpdateHover(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Drag d = drag;
            drag = Drag.None;
            if (d == Drag.Draw && cur != null) FinishDraw();
            else if ((d == Drag.Move || d == Drag.Resize) && committed) Fire(Changed);
            orig = null;
            UpdateHover(e.Location);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null && drag == Drag.None) { hover = null; Invalidate(); }
        }

        void FinishDraw()
        {
            Shape s = cur;
            cur = null;
            float min = 4f / k;
            bool tiny = s.Kind == Tool.Arrow
                ? Math.Abs(s.B.X - s.A.X) < min && Math.Abs(s.B.Y - s.A.Y) < min
                : s.Box.Width < min || s.Box.Height < min;
            if (tiny) { s.DropCache(); Invalidate(); return; }
            if (s.Kind == Tool.Crop) { ApplyCrop(s.Box); return; }
            Commit();
            shapes.Add(s);
            Fire(Changed);
            // New arrows stay selected so the bend handle is visible.
            if (s.Kind == Tool.Arrow) SelectShape(s);
        }

        // Non-destructive crop: only the area is stored and Ctrl+Z restores it.
        void ApplyCrop(RectangleF r)
        {
            r = RectangleF.Intersect(r, Crop);
            Rectangle ri = Rectangle.Round(r);
            if (ri.Width < 8 || ri.Height < 8) { Invalidate(); return; }
            Commit();
            Crop = ri;
            Fire(Changed);
            Invalidate();
        }

        void AddCounter(PointF p)
        {
            int n = 1;
            foreach (Shape s in shapes)
            {
                if (s.Kind == Tool.Counter && s.Number >= n) n = s.Number + 1;
            }
            Shape c = NewShape(Tool.Counter);
            c.A = p;
            c.B = p;
            c.Number = n;
            Commit();
            shapes.Add(c);
            Fire(Changed);
            Invalidate();
        }

        // Shift: perfect square/circle or 45-degree arrow.
        static PointF Constrain(PointF a, PointF b, Tool t)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (t == Tool.Arrow)
            {
                double ang = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
                double len = Math.Sqrt(dx * dx + dy * dy);
                return new PointF(a.X + (float)(Math.Cos(ang) * len), a.Y + (float)(Math.Sin(ang) * len));
            }
            float d = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new PointF(a.X + Math.Sign(dx) * d, a.Y + Math.Sign(dy) * d);
        }

        static void ResizeShape(Shape s, Shape o, int h, PointF p, bool square)
        {
            if (s.Kind == Tool.Arrow)
            {
                if (h == 0) s.A = square ? Constrain(o.B, p, Tool.Arrow) : p;
                else s.B = square ? Constrain(o.A, p, Tool.Arrow) : p;
                if (o.Curved) s.C = Painter.Similar(o.C, o.A, o.B, s.A, s.B); // keep the curve's shape while resizing
                return;
            }
            RectangleF b = o.Box;
            PointF fixedPt = h == 0 ? new PointF(b.Right, b.Bottom) : h == 1 ? new PointF(b.Left, b.Bottom)
                           : h == 2 ? new PointF(b.Left, b.Top) : new PointF(b.Right, b.Top);
            s.A = fixedPt;
            s.B = square ? Constrain(fixedPt, p, Tool.Rect) : p;
        }

        Snapshot Take()
        {
            Snapshot sn = new Snapshot();
            sn.Shapes = new List<Shape>();
            foreach (Shape s in shapes) sn.Shapes.Add(s.Clone());
            sn.Crop = Crop;
            return sn;
        }

        // Snapshot taken before every change.
        void Commit()
        {
            undo.Add(Take());
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
        }

        void Restore(Snapshot sn)
        {
            foreach (Shape s in shapes) s.DropCache();
            shapes = sn.Shapes;
            Crop = sn.Crop;
            selected = null;
            hover = null;
            Fire(Changed);
            Fire(StateChanged);
            Invalidate();
        }

        public void Undo()
        {
            if (box != null) { CancelText(); return; }
            if (undo.Count == 0) return;
            redo.Add(Take());
            Snapshot sn = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Restore(sn);
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(Take());
            Snapshot sn = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Restore(sn);
        }

        public void SelectShape(Shape s)
        {
            if (selected == s) return;
            selected = s;
            Invalidate();
            Fire(StateChanged);
        }

        public void SetTool(Tool t)
        {
            CommitText();
            Tool = t;
            SelectShape(null);
            Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
            Fire(StateChanged);
        }

        // Applies to the selected shape if any, otherwise to the next ones.
        public void SetColor(Color c)
        {
            Color = c;
            if (selected != null && selected.Color.ToArgb() != c.ToArgb())
            {
                Commit();
                selected.Color = c;
                Fire(Changed);
                Invalidate();
            }
            Fire(StateChanged);
        }

        public void SetWeight(int w)
        {
            w = Math.Max(0, Math.Min(Weights.Length - 1, w));
            Weight = w;
            if (selected != null && selected.Weight != w)
            {
                Commit();
                selected.Weight = w;
                selected.Width = StrokeFor(w);
                selected.FontPx = FontFor(w);
                Fire(Changed);
                Invalidate();
            }
            Fire(StateChanged);
        }

        public void DeleteSelected()
        {
            if (selected == null) return;
            Commit();
            shapes.Remove(selected);
            selected.DropCache();
            selected = null;
            hover = null;
            Fire(Changed);
            Fire(StateChanged);
            Invalidate();
        }

        public void Nudge(int dx, int dy)
        {
            if (selected == null) return;
            Commit();
            selected.Offset(selected.Clone(), dx, dy);
            Fire(Changed);
            Invalidate();
        }

        public string HintText
        {
            get
            {
                if (box != null) return "Escribe y pulsa Enter \u00B7 May\u00FAs+Enter a\u00F1ade una l\u00EDnea \u00B7 Esc cancela";
                if (selected != null)
                {
                    string how = selected.Kind == Tool.Arrow ? "arr\u00E1strala para moverla; tira del punto azul del medio para curvarla"
                               : Handles(selected).Length > 0 ? "arr\u00E1strala para moverla o tira de sus puntos" : "arr\u00E1strala para moverla";
                    if (selected.Kind == Tool.Text) how += " (doble clic para cambiar el texto)";
                    return "Marca seleccionada: " + how + " \u00B7 Supr la borra \u00B7 el color y el grosor se le aplican";
                }
                switch (Tool)
                {
                    case Tool.Arrow: return "Arrastra para dibujar una flecha \u00B7 May\u00FAs la deja recta \u00B7 despu\u00E9s, tira del punto azul del medio para curvarla";
                    case Tool.Rect: return "Arrastra para enmarcar \u00B7 May\u00FAs hace un cuadrado \u00B7 pincha una marca para moverla";
                    case Tool.Ellipse: return "Arrastra para rodear \u00B7 May\u00FAs hace un c\u00EDrculo";
                    case Tool.Text: return "Haz clic donde quieras escribir";
                    case Tool.Counter: return "Cada clic pone el siguiente n\u00FAmero: 1, 2, 3\u2026";
                    case Tool.Highlight: return "Arrastra sobre lo que quieras resaltar";
                    case Tool.Pixelate: return "Arrastra sobre lo que quieras difuminar \u00b7 para contrase\u00f1as o datos sensibles, mejor Tapar (X)";
                    case Tool.Redact: return "Arrastra sobre lo que quieras tapar: un bloque s\u00f3lido que borra de verdad lo de debajo";
                    default: return "Arrastra el \u00E1rea que quieres conservar \u00B7 Ctrl+Z lo deshace";
                }
            }
        }

        void StartText(PointF at, Shape existing)
        {
            editing = existing;
            boxAt = existing != null ? existing.A : at;
            float fp = existing != null ? existing.FontPx : FontFor(Weight);
            Color c = existing != null ? existing.Color : Color;
            box = new TextBox();
            box.Multiline = true;
            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Segoe UI Semibold", Math.Max(9f, fp * k), GraphicsUnit.Pixel);
            box.BackColor = c;
            box.ForeColor = Painter.Contrast(c);
            int pad = (int)Math.Round(fp * 0.35f * k);
            Point pt = ToScreen(boxAt);
            box.Location = new Point(pt.X + pad, pt.Y + pad);
            if (existing != null) box.Text = existing.Text;
            box.KeyDown += BoxKeyDown;
            box.TextChanged += delegate { SizeBox(); };
            box.LostFocus += delegate
            {
                if (box == null) return;
                bool clickOnCanvas = (Control.MouseButtons & MouseButtons.Left) != 0 &&
                                     ClientRectangle.Contains(PointToClient(Control.MousePosition));
                CommitText();
                if (clickOnCanvas) skipNextDown = true;
            };
            SizeBox();
            Controls.Add(box);
            box.Focus();
            box.SelectionStart = box.Text.Length;
            Invalidate();
            Fire(StateChanged);
        }

        void SizeBox()
        {
            if (box == null) return;
            Size sz = TextRenderer.MeasureText(box.Text.Length > 0 ? box.Text + "  " : "Escribe aqu\u00ED", box.Font);
            box.Size = new Size(sz.Width + 8, sz.Height + 4);
        }

        void BoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; CommitText(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CancelText(); }
        }

        public void CommitText()
        {
            TextBox b = box;
            if (b == null) return;
            box = null;
            string t = b.Text.TrimEnd();
            Controls.Remove(b);
            b.Dispose();
            Focus();
            Shape ed = editing;
            editing = null;
            if (ed != null)
            {
                if (t != ed.Text)
                {
                    Commit();
                    if (t.Length == 0) { shapes.Remove(ed); selected = null; }
                    else ed.Text = t;
                    Fire(Changed);
                }
                Invalidate();
                Fire(StateChanged);
                return;
            }
            Fire(StateChanged);
            if (t.Length == 0) { Invalidate(); return; }
            Shape s = NewShape(Tool.Text);
            s.A = boxAt;
            s.B = boxAt;
            s.Text = t;
            Commit();
            shapes.Add(s);
            Fire(Changed);
            Invalidate();
        }

        void CancelText()
        {
            TextBox b = box;
            if (b == null) return;
            box = null;
            editing = null;
            Controls.Remove(b);
            b.Dispose();
            Focus();
            Invalidate();
            Fire(StateChanged);
        }

        // Final image: cropped, annotated and on the backdrop if enabled.
        public Bitmap Render()
        {
            Size o = OutputSizeRaw;
            Bitmap b = new Bitmap(o.Width, o.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.DrawImage(Img, new Rectangle(0, 0, b.Width, b.Height), Crop.X, Crop.Y, Crop.Width, Crop.Height, GraphicsUnit.Pixel);
                g.TranslateTransform(-Crop.X, -Crop.Y);
                foreach (Shape s in shapes) Painter.Draw(g, s, Img);
            }
            if (!BgOn || Bg == null) return b;
            using (b) return Backdrop.Compose(b, Bg);
        }

        // Only the annotations in that area, on transparent (to overlay on a video).
        public Bitmap RenderMarks(Rectangle area)
        {
            Bitmap b = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.TranslateTransform(-area.X, -area.Y);
                foreach (Shape s in shapes) Painter.Draw(g, s, Img);
            }
            return b;
        }

        public bool HasMarks { get { return shapes.Count > 0; } }

        public Rectangle CropRect { get { return Rectangle.Round(Crop); } }

        public void Release()
        {
            foreach (Shape s in shapes) s.DropCache();
            if (cur != null) cur.DropCache();
            if (view != null) view.Dispose();
            if (dots != null) dots.Dispose();
            if (bgView != null) bgView.Dispose();
            if (roundView != null) roundView.Dispose();
            Img.Dispose();
        }

        // Video editor: shows another frame of the same recording under the marks (same size, so nothing moves).
        public void ReplaceImage(Bitmap img)
        {
            if (img.Width != Img.Width || img.Height != Img.Height) { img.Dispose(); return; }
            Bitmap old = Img;
            Img = img;
            old.Dispose();
            if (view != null) { view.Dispose(); view = null; }
            if (roundView != null) { roundView.Dispose(); roundView = null; roundKey = null; }
            Invalidate();
        }
    }

}
