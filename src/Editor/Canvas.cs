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
    public partial class Canvas : Control
    {
        class Snapshot { public List<Shape> Shapes; public RectangleF Crop; public object Extra; }

        // State kept outside the canvas that undo also covers (a video's trim and cuts): taken with every step and given
        // back on undo and redo.
        public Func<object> SaveExtra;
        public Action<object> LoadExtra;
        enum Drag { None, Draw, Move, Resize }

        public static readonly float[] Weights = { 0.6f, 1f, 1.6f };

        public Bitmap Img { get; private set; }
        public float Ui = 1f;
        public Tool Tool = Tool.Rect;
        public Color Color = Theme.Palette[0];
        public int Weight = 1;
        public RectangleF Crop;
        public event EventHandler Changed, StateChanged;
        public event Action DrawStart;     // a mark (or text) is about to be made: the editor stops the playback
        List<Shape> shapes = new List<Shape>();
        readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();
        readonly float baseStroke, baseFont;
        Shape cur, selected, hover, orig, editing;
        Drag drag;
        int handle;
        bool committed, skipNextDown;
        PointF dragFrom, grab;
        float k = 1f;
        PointF off;
        Bitmap view;
        Size viewFor;
        RectangleF viewCrop;
        TextBox box;
        PointF boxAt;
        // Presentation backdrop: with BgOn, the capture is shown (and exported) on Bg's background.
        public Settings Bg;
        public bool BgOn;
        // While a backdrop slider is dragged: fast scaling, high quality afterwards.
        public bool Live
        {
            get { return slidingBg; }
            set { if (slidingBg && !value) SlideDone(); slidingBg = value; }
        }
        bool slidingBg;
        bool viewLive;
        Rectangle frameScreen;
        float radiusScreen, innerRadiusScreen;
        Padding insetPx;                // around the capture, in its own pixels (an inset in its edge colour)
        Backdrop.ShotInfo info;         // what the backdrop takes from the capture (never for a video)
        // A video: its frames change, so the backdrop takes nothing from them (no inset, no blurred backdrop).
        public bool Video;
        Bitmap bgFill, roundView;
        Backdrop.ShadowPiece[] bgShadow;
        string fillKey, fillPreset, shadowKey, roundKey;
        bool fillLive;

        // Playback: the newest decoded frame is drawn instead of Img (at the size it was decoded at) until the exact frame
        // replaces Img. Written from the player's thread, drawn from the UI thread.
        readonly object liveLock = new object();
        Bitmap live;
        bool liveOn;
        int livePending;

        public Canvas(Bitmap img)
        {
            Img = img;
            Crop = new RectangleF(0, 0, img.Width, img.Height);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = EdLook.Surround;
            int m = Math.Min(img.Width, img.Height);
            baseStroke = Math.Max(4f, m / 180f);
            baseFont = Math.Max(18f, m / 30f);
        }

        public bool Typing { get { return box != null; } }
        public bool HasSelection { get { return selected != null; } }
        // A drag in progress (drawing, moving, resizing or panning): editor shortcuts wait until it ends.
        public bool Busy { get { return drag != Drag.None || panning; } }
        public bool IsCropped { get { return Crop != new RectangleF(0, 0, Img.Width, Img.Height); } }
        public bool CanUndo { get { return undo.Count > 0 || box != null; } }
        // Not while typing: the marks would be swapped under the text box (and an edited mark lost).
        public bool CanRedo { get { return redo.Count > 0 && box == null; } }
        public Color ActiveColor { get { return selected != null ? selected.Color : Color; } }
        public int ActiveWeight { get { return selected != null ? selected.Weight : Weight; } }
        // What the color and weight options apply to: the selected mark, or the next one drawn with the tool.
        public Tool ActiveKind { get { return selected != null ? selected.Kind : Tool; } }
        // Output size: the whole frame when the backdrop is on.
        public Size OutputSize
        {
            get
            {
                if (!BgOn || Bg == null) return OutputSizeRaw;
                Size frame;
                Rectangle card, inner;
                Backdrop.ShotInfo i = Info();
                Backdrop.Measure(OutputSizeRaw, Bg, false, i != null ? i.Inset : Padding.Empty, out frame, out card, out inner);
                return frame;
            }
        }

        // The capture shrunk and blurred, for the swatches of the blurred backdrop: the same one the backdrop uses, made
        // once per crop.
        public Bitmap BlurThumb()
        {
            Backdrop.ShotInfo i = Info();
            return i == null ? null : Backdrop.BlurOf(i, Img, Rectangle.Round(Crop));
        }

        // Read again only when the crop or what is asked of it changes; the preview and the saved image share it.
        Backdrop.ShotInfo Info()
        {
            if (Video || Bg == null) return null;
            Backdrop.ShotInfo n = Backdrop.Study(Img, Rectangle.Round(Crop), Bg, info);
            if (info != null && n != info) info.Dispose();
            info = n;
            return info;
        }

        Size OutputSizeRaw { get { return new Size(Math.Max(1, (int)Math.Round(Crop.Width)), Math.Max(1, (int)Math.Round(Crop.Height))); } }

        int Pu(float v) { return (int)Math.Round(v * Ui); }
        float StrokeFor(int w) { return baseStroke * Weights[w]; }
        float FontFor(int w) { return baseFont * (0.75f + 0.3f * w); }

        void Fire(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        // Zoom on top of "fit to window" (1 = fit) and how far the view is panned from the centre, in screen pixels.
        float zoom = 1f;
        PointF pan;
        bool zoomReady;
        // The scaled view is cached as one bitmap up to DirectPixels; beyond that only the visible part is drawn. With the
        // backdrop on, the whole frame is cached, so zoom stops at MaxFramePixels.
        const double DirectPixels = 6e6, MaxFramePixels = 12e6;

        void Fit()
        {
            float pad = 28 * Ui;
            // Room below for the zoom control, so it doesn't cover the capture when fitted.
            float padB = ShowPill ? Math.Max(pad, PillRoom * Ui) : pad;
            float aw = Math.Max(1f, Width - 2 * pad), ah = Math.Max(1f, Height - pad - padB);
            bool bg = BgOn && Bg != null;
            Size cs = OutputSizeRaw, frame = cs;
            Rectangle inner = new Rectangle(Point.Empty, cs), card = inner;
            insetPx = Padding.Empty;
            if (bg)
            {
                Backdrop.ShotInfo i = Info();
                if (i != null) insetPx = i.Inset;
                Backdrop.Measure(cs, Bg, false, insetPx, out frame, out card, out inner); // the whole frame fits; the capture sits inside it
            }
            float fit = Math.Min(ShotStack.MaxZoom(cs), Math.Min(aw / frame.Width, ah / frame.Height));
            if (!zoomReady && Width > 1 && Height > 1 && IsHandleCreated)
            {
                // Tall captures (scrolling ones) open fitted to the width and scrolled to the top, so they can be read.
                zoomReady = true;
                float byWidth = Math.Min(1f, aw / frame.Width);
                if (frame.Height > frame.Width * 2.2f && byWidth > fit * 1.3f) { zoom = byWidth / fit; pan = new PointF(0, float.MaxValue); BeginInvoke((Action)delegate { Fire(StateChanged); }); }
            }
            k = fit * zoom;
            if (bg && (double)frame.Width * k * frame.Height * k > MaxFramePixels) { k = (float)Math.Sqrt(MaxFramePixels / ((double)frame.Width * frame.Height)); zoom = k / fit; }
            float cw = frame.Width * k, ch = frame.Height * k;
            float mx = Math.Max(0, (cw - Width) / 2 + pad), my = Math.Max(0, (ch - Height) / 2 + pad);
            pan = new PointF(Math.Max(-mx, Math.Min(mx, pan.X)), Math.Max(-my, Math.Min(my, pan.Y)));
            float fx = (float)Math.Round((Width - cw) / 2f + pan.X), fy = (float)Math.Round((Height - (padB - pad) - ch) / 2f + pan.Y);
            off = new PointF((float)Math.Round(fx + inner.X * k), (float)Math.Round(fy + inner.Y * k));
            if (bg)
            {
                frameScreen = new Rectangle((int)fx, (int)fy, Math.Max(1, (int)Math.Round(cw)), Math.Max(1, (int)Math.Round(ch)));
                int radius = Backdrop.RadiusFor(card.Size, Bg);
                radiusScreen = radius * k;
                innerRadiusScreen = Backdrop.InnerRadius(radius, insetPx, cs) * k;
            }
        }

        // The card around the capture on screen: the capture itself without an inset.
        Rectangle CardScreen(Rectangle ir)
        {
            if (insetPx == Padding.Empty) return ir;
            return Rectangle.FromLTRB(ir.X - (int)Math.Round(insetPx.Left * k), ir.Y - (int)Math.Round(insetPx.Top * k),
                                      ir.Right + (int)Math.Round(insetPx.Right * k), ir.Bottom + (int)Math.Round(insetPx.Bottom * k));
        }

        // The window moved to a monitor with another scale (Ui changed).
        public void Rescaled()
        {
            pillFitW = -1;
            pillHot = pillDown = -1;
            Fit();
            PlaceBox();
            Invalidate();
        }

        public int ZoomPercent { get { Fit(); return (int)Math.Round(k * 100); } }
        public bool Zoomed { get { return Math.Abs(zoom - 1f) > 0.01f; } }

        // Zooms by a factor keeping the image point under p (screen) where it is.
        public void ZoomAt(Point p, float factor)
        {
            Fit();
            PointF ip = ToImg(p, false);
            zoom = Math.Max(0.25f, Math.Min(16f, zoom * factor));
            Fit();
            Point now = ToScreen(ip);
            pan = new PointF(pan.X + p.X - now.X, pan.Y + p.Y - now.Y);
            ZoomChanged();
        }

        public void ZoomBy(float factor) { ZoomAt(new Point(Width / 2, Height / 2), factor); }
        public void ZoomFit() { zoom = 1f; pan = PointF.Empty; ZoomChanged(); }
        public void ZoomActual() { Fit(); ZoomBy(1f / k); }

        void ZoomChanged()
        {
            Fit();
            Invalidate();
            Fire(StateChanged);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (box != null) return;
            if ((ModifierKeys & Keys.Control) != 0) { ZoomAt(e.Location, (float)Math.Pow(1.15, e.Delta / 120.0)); return; }
            float step = e.Delta / 120f * 90 * Ui;
            if ((ModifierKeys & Keys.Shift) != 0) pan.X += step; else pan.Y += step;
            Fit();
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            // WM_MOUSEHWHEEL: touchpads and tilt wheels scroll sideways.
            if (m.Msg == 0x020E)
            {
                if (box == null)
                {
                    int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                    pan.X -= delta / 120f * 90 * Ui;
                    Fit();
                    Invalidate();
                }
                m.Result = (IntPtr)1;
                return;
            }
            // While one button drags, the others are ignored: WinForms would release the capture on their button-up,
            // which drops the gesture.
            MouseButtons active = panning ? panButton : drag != Drag.None ? MouseButtons.Left : MouseButtons.None;
            if (active != MouseButtons.None)
            {
                MouseButtons b = m.Msg >= 0x201 && m.Msg <= 0x203 ? MouseButtons.Left : m.Msg >= 0x204 && m.Msg <= 0x206 ? MouseButtons.Right
                               : m.Msg >= 0x207 && m.Msg <= 0x209 ? MouseButtons.Middle : m.Msg >= 0x20B && m.Msg <= 0x20D ? MouseButtons.XButton1
                               : MouseButtons.None;
                if (b != MouseButtons.None && b != active)
                {
                    m.Result = b == MouseButtons.XButton1 ? (IntPtr)1 : IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        // The middle button, or the left one while Space is held, drags the view around.
        bool panning, spacePan;
        MouseButtons panButton;
        Point panFrom;
        PointF panStart;

        // Shift pressed or let go mid-drag: the square/45-degree constraint follows at once, without moving the mouse.
        void ShiftChanged()
        {
            if (drag != Drag.Draw && drag != Drag.Resize) return;
            Point p = PointToClient(MousePosition);
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, p.X, p.Y, 0));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.ShiftKey) { ShiftChanged(); return; }
            if (e.KeyCode != Keys.Space || box != null) return;
            e.Handled = e.SuppressKeyPress = true;
            if (spacePan || drag != Drag.None) return;
            spacePan = true;
            if (hover != null) { InvalidateOutline(hover); hover = null; }
            Cursor = Cursors.SizeAll;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.ShiftKey) { ShiftChanged(); return; }
            if (e.KeyCode != Keys.Space || !spacePan) return;
            spacePan = false;
            if (!panning) UpdateHover(PointToClient(MousePosition));
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            spacePan = false;
        }

        // Background and shadow at screen scale, as two layers rebuilt only when what they depend on changes: the
        // background by the frame's size, the shadow by the capture's size and corners (dragging "Esquinas" never
        // repaints the background). Not keyed by screen position, so panning and scrolling reuse them. While a slider
        // is dragged (Live) a new one is made the fast way and a sharp one replaces it on release.
        void DrawBackdrop(Graphics g, Rectangle ir)
        {
            // What the card will cover is not painted under it: most of the frame, and of the shadow's scaled pictures.
            GraphicsState under = g.Save();
            ExcludeCard(g, ir);
            DrawBackdropLayers(g, ir);
            g.Restore(under);
        }

        // Leaves out of the clip the part of the card that is opaque whatever its corners: the card less its four corner
        // squares. An inset's border is always opaque; without one, only a capture with no transparent pixel covers it.
        public static bool PaintUnderCard;   // tests: paint everything, to compare

        void ExcludeCard(Graphics g, Rectangle ir)
        {
            if (PaintUnderCard || (insetPx == Padding.Empty && !Opaque())) return;
            // Two pixels in from every edge: an antialiased card or a stretched capture is not quite opaque on its border.
            Rectangle card = Rectangle.Inflate(CardScreen(ir), -2, -2);
            int r = (int)Math.Ceiling(Math.Max(radiusScreen, innerRadiusScreen)) + 1;
            if (card.Width <= 2 * r || card.Height <= 2 * r) return;
            g.SetClip(Rectangle.FromLTRB(card.X + r, card.Y, card.Right - r, card.Bottom), CombineMode.Exclude);
            g.SetClip(Rectangle.FromLTRB(card.X, card.Y + r, card.Right, card.Bottom - r), CombineMode.Exclude);
        }

        Bitmap opaqueFor;
        bool opaque;

        // Whether the capture has no transparent pixel (read once per picture, stopping at the first one).
        bool Opaque()
        {
            if (opaqueFor == Img) return opaque;
            opaqueFor = Img;
            opaque = true;
            if (!Image.IsAlphaPixelFormat(Img.PixelFormat)) return opaque;
            const int Band = 256;
            int[] row = new int[Img.Width];
            for (int y0 = 0; y0 < Img.Height && opaque; y0 += Band)
            {
                int bh = Math.Min(Band, Img.Height - y0);
                BitmapData d = Img.LockBits(new Rectangle(0, y0, Img.Width, bh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < bh && opaque; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, row.Length);
                        for (int x = 0; x < row.Length; x++) if ((uint)row[x] < 0xFF000000u) { opaque = false; break; }
                    }
                }
                finally { Img.UnlockBits(d); }
            }
            return opaque;
        }

        void DrawBackdropLayers(Graphics g, Rectangle ir)
        {
            string preset = Backdrop.FillKey(Bg, info), fk = preset + "|" + frameScreen.Size;
            if (Live && bgFill != null && fillPreset == preset && fk != fillKey)
            {
                // "Margen" being dragged: the background already made, stretched to the new frame (a step is a few pixels).
                // A gradient, a plain colour or a blur is smooth: stretched a few percent, the fastest scaling looks the
                // same; a picture keeps bilinear.
                InterpolationMode im = g.InterpolationMode;
                PixelOffsetMode pm = g.PixelOffsetMode;
                bool picture = Bg.BgKind == 0 && !Backdrop.IsGradient(Bg.BgPreset);
                g.InterpolationMode = picture ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bgFill, frameScreen);
                g.InterpolationMode = im;
                g.PixelOffsetMode = pm;
            }
            else
            {
                if (bgFill == null || fk != fillKey || (fillLive && !Live))
                {
                    if (bgFill != null) bgFill.Dispose();
                    bgFill = Backdrop.Fill(frameScreen.Size, Bg, Live, info);
                    fillKey = fk;
                    fillPreset = preset;
                    fillLive = Live;
                }
                g.DrawImageUnscaled(bgFill, frameScreen.X, frameScreen.Y);
            }
            Probe(ref slideFill);
            int level = Backdrop.ShadowLevel(Bg);
            if (level == 0) return;
            int radius = (int)Math.Round(radiusScreen);
            Rectangle card = CardScreen(ir);
            string sk = card.Size + "|" + radius + "|" + level;
            if (bgShadow == null || sk != shadowKey)
            {
                DropShadow();
                bgShadow = Backdrop.ShadowPieces(new Rectangle(Point.Empty, card.Size), radius, level);
                shadowKey = sk;
            }
            // Within the frame, as when it was drawn on the background itself; upscaled straight to the screen.
            GraphicsState st = g.Save();
            g.SetClip(frameScreen, CombineMode.Intersect);
            foreach (Backdrop.ShadowPiece p in bgShadow) p.Draw(g, card.Location, Live);
            g.Restore(st);
            Probe(ref slideShadow);
        }

        // A backdrop setting changed. While "Esquinas" is dragged the frame keeps its place and size: only inside it
        // changes, so the floating shadow around it and the rest of the canvas aren't painted again.
        public void BackdropChanged()
        {
            Rectangle before = frameScreen;
            Fit();
            if (Live && BgOn && Bg != null && before == frameScreen && before.Width > 1) Invalidate(before);
            else Invalidate();
        }

        void DropShadow()
        {
            if (bgShadow != null) foreach (Backdrop.ShadowPiece p in bgShadow) p.Dispose();
            bgShadow = null;
        }

        // While a backdrop slider is dragged, what each repaint costs, written to the log on release: the place to look
        // if dragging ever feels slow.
        long slideAt;
        double slideFill, slideShadow, slideView, slideMarks, slideTotal, slideWorst;
        int slideFrames;

        void Probe(ref double into)
        {
            if (!Live) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            into += (now - slideAt) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            slideAt = now;
        }

        void SlideDone()
        {
            if (slideFrames == 0) return;
            int n = slideFrames;
            ShotStack.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Fondo: {0} repintados al arrastrar, media {1:0.0} ms, peor {2:0.0} ms (relleno {3:0.0}, sombra {4:0.0}, captura {5:0.0}, marcas {6:0.0}) a {7}x{8}",
                n, slideTotal / n, slideWorst, slideFill / n, slideShadow / n, slideView / n, slideMarks / n, frameScreen.Width, frameScreen.Height));
            slideFrames = 0;
            slideFill = slideShadow = slideView = slideMarks = slideTotal = slideWorst = 0;
        }

        // Scaled capture with antialiased rounded corners; drawn into the same bitmap while the size stays.
        Bitmap RoundView(Bitmap v, float radius)
        {
            // While a slider is dragged, corners less than a pixel apart (rounding as the margin changes) reuse the ones
            // made; the exact ones come on release.
            if (Live && roundView != null && roundFor == v && roundView.Size == v.Size && Math.Abs(radius - roundRadius) < 1f) return roundView;
            string key = v.GetHashCode() + "|" + v.Size + "|" + Math.Round(radius, 1);
            if (roundView != null && key == roundKey) return roundView;
            Bitmap made = Backdrop.Rounded(v, radius, roundView);
            if (roundView != null && made != roundView) roundView.Dispose();
            roundView = made;
            roundKey = key;
            roundFor = v;
            roundRadius = radius;
            return roundView;
        }

        Bitmap roundFor;
        float roundRadius;

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

        // Very large zooms: draw only the part of the capture that is on screen, straight from the original.
        void DrawVisible(Graphics g, Rectangle ir)
        {
            Rectangle vis = Rectangle.Intersect(ir, ClientRectangle);
            if (vis.Width <= 0 || vis.Height <= 0) return;
            float sx = Crop.X + (vis.X - off.X) / k, sy = Crop.Y + (vis.Y - off.Y) / k;
            InterpolationMode im = g.InterpolationMode;
            PixelOffsetMode pm = g.PixelOffsetMode;
            g.InterpolationMode = k >= 2f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(Img, vis, sx, sy, vis.Width / k, vis.Height / k, GraphicsUnit.Pixel, ia);
            }
            g.InterpolationMode = im;
            g.PixelOffsetMode = pm;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            long paintAt = System.Diagnostics.Stopwatch.GetTimestamp();
            slideAt = paintAt;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Fit();
            g.Clear(EdLook.Surround);
            bool bg = BgOn && Bg != null;
            int vw = Math.Max(1, (int)Math.Round(Crop.Width * k)), vh = Math.Max(1, (int)Math.Round(Crop.Height * k));
            bool direct = !bg && (double)vw * vh > DirectPixels;
            bool playing = LiveOn;
            // "Margen" being dragged changes the scale a little on every step: the capture already scaled is stretched
            // meanwhile, and scaled properly from the original on release.
            bool stale = bg && Live && view != null && viewCrop == Crop;
            Bitmap v = direct || playing ? null : stale ? view : View();
            Probe(ref slideView);
            Rectangle ir = new Rectangle((int)off.X, (int)off.Y, vw, vh);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (bg)
            {
                // The shadow lies outside the frame: a repaint inside it (dragging a mark) never reaches it.
                if (!frameScreen.Contains(e.ClipRectangle)) FloatShadow(g, frameScreen);
                Probe(ref slideShadow);
                DrawBackdrop(g, ir);
                if (insetPx != Padding.Empty && info != null)
                {
                    // Inside the frame: rounding each side of the inset on its own can reach a pixel past it.
                    GraphicsState cs = g.Save();
                    g.SetClip(frameScreen, CombineMode.Intersect);
                    Backdrop.FillCard(g, CardScreen(ir), radiusScreen, info.Edge);
                    g.Restore(cs);
                }
                if (!(playing && DrawLive(g, ir, true)))
                {
                    if (v == null) v = View();
                    if (v.Width == ir.Width && v.Height == ir.Height) g.DrawImageUnscaled(innerRadiusScreen >= 0.5f ? RoundView(v, innerRadiusScreen) : v, ir.X, ir.Y);
                    else
                    {
                        float r = innerRadiusScreen * v.Width / (float)ir.Width;   // the corners as they will look once stretched
                        InterpolationMode im = g.InterpolationMode;
                        PixelOffsetMode pm = g.PixelOffsetMode;
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(r >= 0.5f ? RoundView(v, r) : v, ir);
                        g.InterpolationMode = im;
                        g.PixelOffsetMode = pm;
                    }
                }
                Probe(ref slideView);
            }
            else
            {
                if (!ir.Contains(e.ClipRectangle)) FloatShadow(g, ir);
                if (!(playing && DrawLive(g, ir, false)))
                {
                    if (v == null && !direct) v = View();
                    if (v != null) g.DrawImageUnscaled(v, ir.X, ir.Y);
                    else DrawVisible(g, ir);
                }
                using (Pen p = new Pen(EdLook.C(Ds.Brushes.Hairline))) g.DrawRectangle(p, ir.X - 1, ir.Y - 1, ir.Width + 1, ir.Height + 1);
            }

            GraphicsState st = g.Save();
            if (bg && innerRadiusScreen >= 0.5f)
            {
                using (GraphicsPath clip = Theme.Round(ir, innerRadiusScreen)) g.SetClip(clip);
            }
            else g.SetClip(ir);
            g.TranslateTransform(off.X - Crop.X * k, off.Y - Crop.Y * k);
            g.ScaleTransform(k, k);
            Painter.DrawMarks(g, shapes, cur != null && cur.Kind != Tool.Crop ? cur : null, editing, Img, Crop);
            g.Restore(st);
            Probe(ref slideMarks);
            if (Live)
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - paintAt) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                slideTotal += ms;
                slideWorst = Math.Max(slideWorst, ms);
                slideFrames++;
            }
            if (box != null)
            {
                // The view moved under the box (a pan, the backdrop toggled or resized): it follows on this paint.
                if (BoxStale()) PlaceBox();
                BoxLabel(g);
            }

            if (hover != null && hover != selected && drag == Drag.None && box == null) Outline(g, hover, false);
            if (selected != null && box == null) Outline(g, selected, true);
            if (cur != null && cur.Kind == Tool.Crop) CropOverlay(g, ir, cur.Box);
            // While a video exports the canvas is locked, and so is the zoom: the pill steps aside.
            if (ShowPill && Enabled && e.ClipRectangle.IntersectsWith(PillArea)) DrawPill(g);
        }

        bool LiveOn { get { lock (liveLock) return liveOn && live != null; } }

        // The playback frame over the image rectangle: the cropped part, scaled to the screen (rounded when the backdrop is on).
        bool DrawLive(Graphics g, Rectangle ir, bool rounded)
        {
            lock (liveLock)
            {
                if (!liveOn || live == null) return false;
                float sx = live.Width / (float)Img.Width, sy = live.Height / (float)Img.Height;
                GraphicsState st = g.Save();
                if (rounded && innerRadiusScreen >= 0.5f)
                    using (GraphicsPath clip = Theme.Round(ir, innerRadiusScreen)) g.SetClip(clip, CombineMode.Intersect);
                g.InterpolationMode = k == 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(live, ir, Crop.X * sx, Crop.Y * sy, Crop.Width * sx, Crop.Height * sy, GraphicsUnit.Pixel, ia);
                }
                g.Restore(st);
                return true;
            }
        }

        // Any thread: copies a BGRA frame of the video (decoded at any size) and repaints the image area.
        public void ShowPlayFrame(byte[] bgra, int w, int h)
        {
            lock (liveLock)
            {
                if (live == null || live.Width != w || live.Height != h)
                {
                    if (live != null) live.Dispose();
                    live = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                BitmapData d = live.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    if (d.Stride == w * 4) System.Runtime.InteropServices.Marshal.Copy(bgra, 0, d.Scan0, w * h * 4);
                    else
                        for (int y = 0; y < h; y++)
                            System.Runtime.InteropServices.Marshal.Copy(bgra, y * w * 4, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w * 4);
                }
                finally { live.UnlockBits(d); }
                liveOn = true;
            }
            if (System.Threading.Interlocked.Exchange(ref livePending, 1) != 0 || !IsHandleCreated) return;
            try { BeginInvoke((Action)LiveRepaint); }
            catch (InvalidOperationException) { livePending = 0; }
        }

        void LiveRepaint()
        {
            livePending = 0;
            if (IsDisposed) return;
            Fit();
            Rectangle ir = new Rectangle((int)off.X, (int)off.Y, Math.Max(1, (int)Math.Round(Crop.Width * k)), Math.Max(1, (int)Math.Round(Crop.Height * k)));
            ir.Inflate(2, 2);
            Invalidate(ir);
        }

        // Back to the exact image (the frame the editor asked for has arrived, or playback is over).
        public void EndPlayFrame()
        {
            lock (liveLock) liveOn = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { pillHot = pillDown = -1; EdTip.Cancel(); }
            Invalidate();
        }

        // Soft shadow so the capture (or the backdrop frame) floats on the surround; only the part outside r is seen.
        void FloatShadow(Graphics g, Rectangle r)
        {
            if (Ds.Brushes.High) return;   // no shadows in a contrast theme
            int sa = Ds.Dark ? 22 : 9;
            // Only the ring around r is filled: blending seven full-size layers under the capture was most of the paint time.
            Region old = g.Clip;
            SmoothingMode sm = g.SmoothingMode;
            // While a slider is dragged these faint rings need no antialiasing (it is put back on release).
            if (Live) g.SmoothingMode = SmoothingMode.HighSpeed;
            g.SetClip(r, CombineMode.Exclude);
            for (int i = 1; i <= 7; i++)
            {
                Rectangle sr = r;
                sr.Inflate(Pu(i * 1.8f), Pu(i * 1.8f));
                sr.Offset(0, Pu(4));
                using (GraphicsPath p = Theme.Round(sr, Pu(3 + i * 1.8f)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(sa, 0, 0, 0))) g.FillPath(b, p);
            }
            g.SmoothingMode = sm;
            g.Clip = old;
            old.Dispose();
        }

        // Floating zoom control at the bottom right: out, percentage (click: actual size), in, and fit.
        static readonly string[] PillTipsEs = { "Alejar  (Ctrl+\u2212)", "Tama\u00F1o real  (Ctrl+1)", "Acercar  (Ctrl++)", "Ajustar a la ventana  (Ctrl+0)" };
        static readonly string[] PillTipsEn = { "Zoom out  (Ctrl+\u2212)", "Actual size  (Ctrl+1)", "Zoom in  (Ctrl++)", "Fit to window  (Ctrl+0)" };
        static string[] PillTips { get { return I18n.IsSpanish ? PillTipsEs : PillTipsEn; } }
        static string PillFitText { get { return I18n.T("Ajustar", "Fit"); } }
        int pillHot = -1, pillDown = -1;

        public const float PillRoom = 58;

        bool ShowPill { get { return Width >= Pu(320) && Height >= Pu(150); } }

        Font PillFont { get { return Fonts.Get(EdLook.Text, 12 * Ui); } }

        // 0 minus, 1 percentage, 2 plus, 3 fit; the last rectangle is the whole capsule.
        int pillFitW = -1;
        bool pillFitLang = true;

        Rectangle[] PillParts()
        {
            int h = Pu(30), bw = Pu(30), pw = Pu(50);
            if (pillFitW < 0 || pillFitLang != I18n.IsSpanish)
            {
                pillFitLang = I18n.IsSpanish;
                pillFitW = TextKit.Measure(PillFitText, PillFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            }
            int fw = pillFitW + Pu(26);
            int w = Pu(3) + bw + pw + bw + Pu(5) + fw + Pu(3);
            Rectangle all = new Rectangle(Width - Pu(16) - w, Height - Pu(16) - h, w, h);
            int x = all.X + Pu(3);
            Rectangle[] r = new Rectangle[5];
            r[0] = new Rectangle(x, all.Y, bw, h); x += bw;
            r[1] = new Rectangle(x, all.Y, pw, h); x += pw;
            r[2] = new Rectangle(x, all.Y, bw, h); x += bw + Pu(5);
            r[3] = new Rectangle(x, all.Y, fw, h);
            r[4] = all;
            return r;
        }

        Rectangle PillArea
        {
            get
            {
                Rectangle a = PillParts()[4];
                a.Inflate(Pu(10), Pu(10));
                return a;
            }
        }

        int PillAt(Point p)
        {
            if (!ShowPill || drag != Drag.None || panning) return -1;
            Rectangle[] r = PillParts();
            if (!r[4].Contains(p)) return -1;
            for (int i = 0; i < 4; i++) if (r[i].Contains(p)) return i;
            return 4; // the capsule's padding: inert, but still not the canvas
        }

        void DrawPill(Graphics g)
        {
            Rectangle[] r = PillParts();
            Rectangle all = r[4];
            float rad = all.Height / 2f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = Ds.Brushes.High ? 4 : 1; i <= 3; i++)
            {
                RectangleF sr = all;
                sr.Inflate(i * 1.5f * Ui, i * 1.5f * Ui);
                sr.Offset(0, 2 * Ui);
                EdLook.Fill(g, sr, rad + i * 1.5f * Ui, Color.FromArgb(Ds.Dark ? 30 : 14, 0, 0, 0));
            }
            EdLook.Fill(g, all, rad, EdLook.C(Palette.Hud));
            EdLook.Hairline(g, all, rad, EdLook.C(Palette.HudLine));
            for (int i = 0; i < 4; i++)
            {
                if (i != pillHot && i != pillDown) continue;
                RectangleF hr = RectangleF.Inflate(r[i], -Pu(1), -Pu(3));
                Color c = EdLook.C(Palette.HudHover);
                if (i == pillDown && i == pillHot) c = Color.FromArgb(Math.Min(255, c.A * 2), c);
                EdLook.Fill(g, hr, hr.Height / 2f, c);
            }
            float stroke = Math.Max(1.2f, 1.6f * Ui);
            EdLook.Icon(g, "minus", r[0], Pu(14), Palette.HudLabel, stroke);
            EdLook.Icon(g, "plus", r[2], Pu(14), Palette.HudLabel, stroke);
            TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            TextKit.Draw(g, (int)Math.Round(k * 100) + " %", Fonts.Get(EdLook.Semibold, 12 * Ui), r[1], EdLook.C(Palette.HudLabel), center);
            using (SolidBrush b = new SolidBrush(EdLook.C(Palette.HudLine))) g.FillRectangle(b, r[3].X - Pu(3), all.Y + Pu(8), Math.Max(1, Pu(1)), all.Height - Pu(16));
            TextKit.Draw(g, PillFitText, PillFont, r[3], EdLook.C(Zoomed ? Palette.HudLabel : Palette.HudLabel2), center);
        }

        void SetPillHot(int part)
        {
            if (part == pillHot) return;
            pillHot = part;
            Invalidate(PillArea);
            if (part >= 0 && part < 4) EdTip.Schedule(this, PillParts()[part], PillTips[part], true);
            else EdTip.Cancel();
        }

        void PillClick(int part)
        {
            if (part == 0) ZoomBy(0.8f);
            else if (part == 1) ZoomActual();
            else if (part == 2) ZoomBy(1.25f);
            else if (part == 3) ZoomFit();
        }

        static PointF[] Handles(Shape s)
        {
            switch (s.Kind)
            {
                case Tool.Arrow:
                case Tool.Line:
                    return new PointF[] { s.A, s.B, s.Mid }; // the third handle (middle) bends the arrow
                case Tool.Text:
                case Tool.Counter:
                    return new PointF[0];
                default:
                    RectangleF b = s.Box;
                    return new PointF[] { new PointF(b.Left, b.Top), new PointF(b.Right, b.Top), new PointF(b.Right, b.Bottom), new PointF(b.Left, b.Bottom) };
            }
        }

        // Selection box on screen: just outside the mark, so the handles sit on it and never cover the stroke.
        Rectangle OutlineRect(Shape s)
        {
            Rectangle r = ScreenRect(Painter.Bounds(s));
            r.Inflate(Pu(4), Pu(4));
            return r;
        }

        // Handle centers on screen (box marks: the corners of the selection box).
        Point[] ScreenHandles(Shape s)
        {
            PointF[] hs = Handles(s);
            Point[] r = new Point[hs.Length];
            if (Shape.IsLinear(s.Kind) || hs.Length != 4)
            {
                for (int i = 0; i < hs.Length; i++) r[i] = ToScreen(hs[i]);
                return r;
            }
            Rectangle o = OutlineRect(s);
            r[0] = new Point(o.Left, o.Top);
            r[1] = new Point(o.Right, o.Top);
            r[2] = new Point(o.Right, o.Bottom);
            r[3] = new Point(o.Left, o.Bottom);
            return r;
        }

        // Dashed outline: faint on hover, blue with handles when selected (a selected arrow shows only its handles).
        void Outline(Graphics g, Shape s, bool strong)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = EdLook.C(Ds.Brushes.Accent);
            Color c = strong ? accent : Color.FromArgb(150, accent);
            using (Pen pen = new Pen(c, Math.Max(1f, 1.25f * Ui)))
            {
                pen.DashStyle = DashStyle.Dash;
                if (Shape.IsLinear(s.Kind))
                {
                    if (!strong)
                    {
                        PointF[] pts = s.Curve(32);
                        Point[] sp = new Point[pts.Length];
                        for (int i = 0; i < pts.Length; i++) sp[i] = ToScreen(pts[i]);
                        g.DrawLines(pen, sp);
                    }
                }
                else
                {
                    g.SmoothingMode = SmoothingMode.None; // crisp 1 px dashes
                    g.DrawRectangle(pen, OutlineRect(s));
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                }
            }
            if (!strong) return;
            float hs = 5f * Ui;
            Point[] handles = ScreenHandles(s);
            for (int i = 0; i < handles.Length; i++)
            {
                Point p = handles[i];
                RectangleF hr = new RectangleF(p.X - hs, p.Y - hs, hs * 2, hs * 2);
                // The bend handle is filled blue to tell it apart from the endpoints.
                bool bend = Shape.IsLinear(s.Kind) && i == 2;
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(Ds.Dark ? 70 : 45, 0, 0, 0)))
                    g.FillEllipse(sh, RectangleF.Inflate(new RectangleF(hr.X, hr.Y + Math.Max(1f, Ui), hr.Width, hr.Height), 0.5f * Ui, 0.5f * Ui));
                using (SolidBrush b = new SolidBrush(bend ? accent : Color.White)) g.FillEllipse(b, hr);
                float pw = Math.Max(1.5f, 1.75f * Ui);
                using (Pen pen = new Pen(bend ? Color.White : accent, pw)) g.DrawEllipse(pen, hr);
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
            Font f = Fonts.Get(EdLook.Semibold, 12 * Ui);
            Size ts = TextKit.Measure(label, f);
            Rectangle lr = new Rectangle(cr.X, Math.Max(ir.Y, cr.Y - ts.Height - Pu(10)), ts.Width + Pu(14), ts.Height + Pu(6));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Theme.Round(lr, lr.Height / 2f))
            using (SolidBrush b = new SolidBrush(EdLook.C(Palette.Hud))) g.FillPath(b, p);
            TextKit.Draw(g, label, f, lr, EdLook.C(Palette.HudLabel), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        // While typing, the label's rounded, padded background is drawn around the text box, as the mark will look.
        Rectangle BoxLabelRect()
        {
            int pad = (int)Math.Round(BoxFont * 0.35f * k);
            return new Rectangle(box.Left - pad, box.Top - pad, box.Width - boxSlack + 2 * pad, box.Height + 2 * pad);
        }

        void BoxLabel(Graphics g)
        {
            Rectangle r = BoxLabelRect();
            float rad = (float)Math.Round(BoxFont * 0.35f * k);
            float sh = Math.Max(1f, StrokeFor(editing != null ? editing.Weight : Weight) * 0.45f * k);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF shb = r;
            shb.Offset(sh, sh);
            EdLook.Fill(g, shb, rad, Color.FromArgb(60, 0, 0, 0));
            EdLook.Fill(g, r, rad, BoxColor);
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
                case Tool.Line:
                {
                    PointF[] pts = s.Curve(s.Curved ? 32 : 1);
                    float best = float.MaxValue;
                    for (int i = 1; i < pts.Length; i++) best = Math.Min(best, SegmentDistance(q, pts[i - 1], pts[i]));
                    return best <= s.Width * 1.3f + tol;
                }
                case Tool.Spotlight:
                {
                    // Only its edge: marks can be drawn inside the lit area without grabbing it.
                    b = s.Box;
                    float m = 2 * tol;
                    RectangleF outer = RectangleF.Inflate(b, m, m), inner = RectangleF.Inflate(b, -m, -m);
                    return outer.Contains(q) && !(inner.Width > 0 && inner.Height > 0 && inner.Contains(q));
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
            Point[] hs = ScreenHandles(selected);
            int r = Pu(8);
            for (int i = hs.Length - 1; i >= 0; i--)
            {
                Point p = hs[i];
                if (Math.Abs(p.X - screen.X) <= r && Math.Abs(p.Y - screen.Y) <= r) return i;
            }
            return -1;
        }

        void UpdateHover(Point p)
        {
            if (spacePan || panning) { if (Cursor != Cursors.SizeAll) Cursor = Cursors.SizeAll; return; }
            Shape h = null;
            Cursor c;
            int hh = HandleAt(p);
            if (hh >= 0) c = Shape.IsLinear(selected.Kind) ? Cursors.SizeAll : (hh == 0 || hh == 2 ? Cursors.SizeNWSE : Cursors.SizeNESW);
            else
            {
                if (Tool != Tool.Crop) h = ShapeAt(p);
                c = h != null ? Cursors.SizeAll : (Tool == Tool.Text ? Cursors.IBeam : Cursors.Cross);
            }
            if (h != hover)
            {
                InvalidateOutline(hover);
                hover = h;
                InvalidateOutline(hover);
            }
            if (Cursor != c) Cursor = c;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (panning) return;
            if (drag == Drag.None && (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && spacePan && box == null)))
            {
                panning = true;
                panButton = e.Button;
                panFrom = e.Location;
                panStart = pan;
                EdTip.Cancel();
                Cursor = Cursors.SizeAll;
                return;
            }
            if (drag != Drag.None) return; // a second button while dragging
            if (skipNextDown) { skipNextDown = false; return; }
            Fit(); // an undo or crop may not have been painted yet
            int part = e.Button == MouseButtons.Left && box == null ? PillAt(e.Location) : -1;
            if (part >= 0)
            {
                EdTip.Cancel();
                pillDown = part;
                Invalidate(PillArea);
                return;
            }
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
                // Where the mark's point is relative to the grab, so it doesn't jump to the cursor.
                PointF hp = Handles(selected)[h], at = ToImg(e.Location, false);
                grab = new PointF(hp.X - at.X, hp.Y - at.Y);
                Begin(at);
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
            // Text and numbers go where the click is, so a click beside the capture only clears the selection.
            if ((Tool == Tool.Text || Tool == Tool.Counter) && !ScreenRect(Crop).Contains(e.Location)) return;
            if (Tool == Tool.Text) { StartText(p, null); return; }
            if (Tool == Tool.Counter) { AddCounter(p); return; }
            cur = NewShape(Tool);
            cur.A = p;
            cur.B = p;
            drag = Drag.Draw;
        }

        Shape NewShape(Tool t)
        {
            Action started = DrawStart;
            if (started != null) started();
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
            redoBeforeDrag = null;
        }

        // The undo step a move or reshape takes when it starts; the redo history it clears comes back if Esc cancels it.
        List<Snapshot> redoBeforeDrag;

        void CommitDrag()
        {
            redoBeforeDrag = redo.Count > 0 ? new List<Snapshot>(redo) : null;
            Commit();
            committed = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (panning)
            {
                pan = new PointF(panStart.X + e.X - panFrom.X, panStart.Y + e.Y - panFrom.Y);
                Fit();
                if (box != null) PlaceBox(); // a middle-button pan while typing: the box goes along
                Invalidate();
                return;
            }
            if (pillDown >= 0) { SetPillHot(PillAt(e.Location) == pillDown ? pillDown : -1); return; }
            if ((drag == Drag.Move || drag == Drag.Resize) && (selected == null || orig == null)) drag = Drag.None;
            if (spacePan && drag == Drag.None) { SetPillHot(-1); return; }
            if (drag == Drag.None)
            {
                int part = box == null ? PillAt(e.Location) : -1;
                SetPillHot(part);
                if (part >= 0)
                {
                    if (hover != null) { InvalidateOutline(hover); hover = null; }
                    if (Cursor != Cursors.Hand) Cursor = Cursors.Hand;
                    return;
                }
            }
            switch (drag)
            {
                case Drag.Draw:
                {
                    PointF p = ToImg(e.Location, true);
                    if ((ModifierKeys & Keys.Shift) != 0) p = Constrain(cur.A, p, cur.Kind);
                    if (cur.Kind == Tool.Crop) { cur.B = p; Invalidate(); return; } // the dimming covers everything
                    InvalidateMark(cur);
                    cur.B = p;
                    InvalidateMark(cur);
                    return;
                }
                case Drag.Move:
                {
                    PointF p = ToImg(e.Location, false);
                    float dx = p.X - dragFrom.X, dy = p.Y - dragFrom.Y;
                    if (!committed)
                    {
                        if (Math.Abs(dx) * k < 2 && Math.Abs(dy) * k < 2) return;
                        CommitDrag();
                    }
                    InvalidateMark(selected);
                    selected.Offset(orig, dx, dy);
                    InvalidateMark(selected);
                    return;
                }
                case Drag.Resize:
                {
                    if (!committed) CommitDrag();
                    InvalidateMark(selected);
                    // The middle handle bends the arrow (snaps back to straight near the line).
                    PointF to = ToImg(e.Location, false);
                    to = new PointF(to.X + grab.X, to.Y + grab.Y);
                    if (Shape.IsLinear(selected.Kind) && handle == 2) selected.SetMid(to, 8f * Ui / k);
                    else
                    {
                        to = new PointF(Math.Max(Crop.Left, Math.Min(Crop.Right, to.X)), Math.Max(Crop.Top, Math.Min(Crop.Bottom, to.Y)));
                        ResizeShape(selected, orig, handle, to, (ModifierKeys & Keys.Shift) != 0);
                    }
                    InvalidateMark(selected);
                    return;
                }
            }
            UpdateHover(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (panning)
            {
                if (e.Button != panButton) return;
                panning = false;
                if (spacePan) Cursor = Cursors.SizeAll;
                else UpdateHover(e.Location);
                return;
            }
            if (pillDown >= 0)
            {
                int part = pillDown;
                pillDown = -1;
                Invalidate(PillArea);
                if (e.Button == MouseButtons.Left && PillAt(e.Location) == part) PillClick(part);
                return;
            }
            if (e.Button != MouseButtons.Left && drag != Drag.None) return; // another button let go mid-drag
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
            if (hover != null && drag == Drag.None) { InvalidateOutline(hover); hover = null; }
            SetPillHot(-1);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Capture lost mid-drag (a dialog, Alt+Tab): the gesture is dropped instead of being left half done.
            if (!Capture && (drag != Drag.None || panning || pillDown >= 0)) CancelGesture();
        }

        // Esc during a drag: a mark being moved or reshaped goes back to where it was; one being drawn is dropped.
        public void CancelGesture()
        {
            panning = false;
            if (pillDown >= 0) { pillDown = -1; Invalidate(PillArea); }
            if (drag == Drag.Draw && cur != null) { cur.DropCache(); cur = null; }
            else if ((drag == Drag.Move || drag == Drag.Resize) && selected != null && orig != null && committed)
            {
                selected.A = orig.A;
                selected.B = orig.B;
                selected.C = orig.C;
                selected.Curved = orig.Curved;
                undo.RemoveAt(undo.Count - 1); // the snapshot taken when the drag began
                if (redoBeforeDrag != null) redo.AddRange(redoBeforeDrag);
                version++;
            }
            redoBeforeDrag = null;
            drag = Drag.None;
            orig = null;
            committed = false;
            Invalidate();
            if (IsHandleCreated) UpdateHover(PointToClient(MousePosition));
            Fire(StateChanged);
        }

        void InvalidateOutline(Shape s)
        {
            if (s == null) return;
            Rectangle r = OutlineRect(s);
            r.Inflate(Pu(10), Pu(10));
            Invalidate(r);
        }

        // Whether any spotlight dims the capture now (the same test as Painter.DrawSpotlights), and what the last repaint
        // knew about it.
        bool spotLit;

        bool SpotlightsLit()
        {
            foreach (Shape x in shapes) if (x.Kind == Tool.Spotlight && x.Box.Width >= 1 && x.Box.Height >= 1) return true;
            return cur != null && cur.Kind == Tool.Spotlight && cur.Box.Width >= 1 && cur.Box.Height >= 1;
        }

        // The mark with its outline, handles and drop shadow: dragging repaints only what moved.
        void InvalidateMark(Shape s)
        {
            if (s == null) return;
            // A spotlight too: the dimming is the same everywhere but in its hole, so only where the hole was and is changes
            // (callers invalidate before and after the change). Except when the dimming itself comes or goes (the first
            // spotlight drawn, the last one shrunk to nothing): then it covers, or leaves, the whole capture.
            if (s.Kind == Tool.Spotlight)
            {
                bool lit = SpotlightsLit();
                if (lit != spotLit) { spotLit = lit; Invalidate(); return; }
            }
            Rectangle r = OutlineRect(s);
            int m = Pu(10) + (int)Math.Ceiling(Math.Max(1f, s.Width * 0.45f) * k) + 2;
            r.Inflate(m, m);
            Invalidate(r);
        }

        void FinishDraw()
        {
            Shape s = cur;
            cur = null;
            float min = 4f / k;
            bool tiny = Shape.IsLinear(s.Kind)
                ? Math.Abs(s.B.X - s.A.X) < min && Math.Abs(s.B.Y - s.A.Y) < min
                : s.Box.Width < min || s.Box.Height < min;
            if (tiny) { s.DropCache(); Invalidate(); return; }
            if (s.Kind == Tool.Crop) { ApplyCrop(s.Box); return; }
            Commit();
            shapes.Add(s);
            Fire(Changed);
            // New arrows stay selected so the bend handle is visible.
            if (Shape.IsLinear(s.Kind)) SelectShape(s);
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
            // A crop is a one-off: back to the tool in use before it, so the next drag draws instead of cropping again.
            SetTool(toolBeforeCrop);
        }

        void AddCounter(PointF p)
        {
            Shape c = NewShape(Tool.Counter);
            c.A = p;
            c.B = p;
            c.Number = NextNumber();
            Commit();
            shapes.Add(c);
            Fire(Changed);
            Invalidate();
        }

        // Shift: perfect square/circle or 45-degree arrow.
        static PointF Constrain(PointF a, PointF b, Tool t)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (Shape.IsLinear(t))
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
            if (Shape.IsLinear(s.Kind))
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
            if (SaveExtra != null) sn.Extra = SaveExtra();
            return sn;
        }

        // An undo step for a change made outside the canvas (the timeline), taken just before it.
        public void Checkpoint()
        {
            if (box != null) return;
            Commit();
            Fire(StateChanged);
        }

        // Snapshot taken before every change.
        void Commit()
        {
            undo.Add(Take());
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
            version++;
        }

        // Bumped by every undo step and every undo or redo, so repeated nudges can share one step.
        int version, nudgedAt = -1;
        Shape nudged;

        void Restore(Snapshot sn)
        {
            foreach (Shape s in shapes) s.DropCache();
            shapes = sn.Shapes;
            Crop = sn.Crop;
            if (LoadExtra != null && sn.Extra != null) LoadExtra(sn.Extra);
            selected = null;
            hover = null;
            if (drag == Drag.Move || drag == Drag.Resize) { drag = Drag.None; orig = null; }
            version++;
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
            if (!CanRedo) return;
            undo.Add(Take());
            Snapshot sn = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Restore(sn);
        }

        public void SelectShape(Shape s)
        {
            if (selected == s) return;
            if (drag == Drag.Move || drag == Drag.Resize) { drag = Drag.None; orig = null; }
            selected = s;
            Invalidate();
            Fire(StateChanged);
        }

        Tool toolBeforeCrop = Tool.Rect;

        public void SetTool(Tool t)
        {
            CommitText();
            if (t == Tool.Crop && Tool != Tool.Crop) toolBeforeCrop = Tool;
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
            PlaceBox(); // the text being typed takes it at once
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
            PlaceBox();
            Fire(StateChanged);
        }

        public void DeleteSelected()
        {
            if (selected == null) return;
            if (drag == Drag.Move || drag == Drag.Resize) { drag = Drag.None; orig = null; }
            Commit();
            shapes.Remove(selected);
            selected.DropCache();
            // Steps stay 1, 2, 3 after one is removed; the same undo step brings back the old numbers.
            if (selected.Kind == Tool.Counter) Renumber(shapes);
            selected = null;
            hover = null;
            Fire(Changed);
            Fire(StateChanged);
            Invalidate();
        }

        // Arrow keys: holding one down (or tapping it several times) is a single undo step.
        public void Nudge(int dx, int dy)
        {
            if (selected == null) return;
            if (nudged != selected || nudgedAt != version) Commit();
            nudged = selected;
            nudgedAt = version;
            InvalidateMark(selected);
            selected.Offset(selected.Clone(), dx, dy);
            InvalidateMark(selected);
            Fire(Changed);
        }

        // Ctrl+D: a copy of the selected mark, just below and to the right, selected so it can be moved at once.
        // A copied number takes the next one.
        public void DuplicateSelected()
        {
            if (selected == null || box != null) return;
            Commit();
            Shape c = selected.Clone();
            float d = (float)Math.Round(Math.Max(1f, Pu(14) / k));
            c.Offset(selected, d, d);
            if (c.Kind == Tool.Counter) c.Number = NextNumber();
            shapes.Add(c);
            SelectShape(c);
            Fire(Changed);
            Invalidate();
        }

        // Counters numbered 1..n in their current order (ties keep the order they were placed in).
        internal static void Renumber(List<Shape> list)
        {
            List<Shape> counters = list.FindAll(delegate(Shape s) { return s.Kind == Tool.Counter; });
            List<int> order = new List<int>();
            for (int i = 0; i < counters.Count; i++) order.Add(i);
            order.Sort(delegate(int a, int b)
            {
                int c = counters[a].Number.CompareTo(counters[b].Number);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (int i = 0; i < order.Count; i++) counters[order[i]].Number = i + 1;
        }

        int NextNumber()
        {
            int n = 1;
            foreach (Shape s in shapes)
            {
                if (s.Kind == Tool.Counter && s.Number >= n) n = s.Number + 1;
            }
            return n;
        }

        public string HintText
        {
            get
            {
                if (box != null) return I18n.T("Escribe y pulsa Enter \u00B7 May\u00FAs+Enter a\u00F1ade una l\u00EDnea \u00B7 Esc cancela", "Type and press Enter \u00B7 Shift+Enter adds a line \u00B7 Esc cancels");
                if (selected != null)
                {
                    if (selected.Kind == Tool.Spotlight) return I18n.T("Foco seleccionado: arr\u00E1stralo por el borde o tira de sus puntos \u00B7 Supr lo quita \u00B7 Ctrl+D a\u00F1ade otro", "Spotlight selected: drag by border or pull points \u00B7 Del removes \u00B7 Ctrl+D adds another");
                    string how = Shape.IsLinear(selected.Kind) ? I18n.T("arr\u00E1strala; tira del punto azul del medio para curvarla", "drag it; pull the blue midpoint to curve")
                               : selected.Kind == Tool.Text ? I18n.T("arr\u00E1strala; doble clic cambia el texto", "drag it; double-click edits text")
                               : Handles(selected).Length > 0 ? I18n.T("arr\u00E1strala o tira de sus puntos", "drag it or pull points") : I18n.T("arr\u00E1strala para moverla", "drag to move");
                    return I18n.Fmt("Marca seleccionada: {0} \u00B7 Supr la borra \u00B7 Ctrl+D la duplica \u00B7 el color y el grosor se le aplican", "Selected mark: {0} \u00B7 Del deletes \u00B7 Ctrl+D duplicates \u00B7 color and thickness apply", how);
                }
                switch (Tool)
                {
                    case Tool.Arrow: return I18n.T("Arrastra para dibujar una flecha \u00B7 May\u00FAs la deja recta \u00B7 despu\u00E9s, tira del punto azul del medio para curvarla", "Drag to draw an arrow \u00B7 Shift draws straight \u00B7 then drag the blue midpoint to curve");
                    case Tool.Line: return I18n.T("Arrastra para dibujar una l\u00EDnea \u00B7 May\u00FAs la deja recta o a 45\u00B0 \u00B7 despu\u00E9s, tira del punto azul del medio para curvarla", "Drag to draw a line \u00B7 Shift snaps to straight or 45\u00B0 \u00B7 then drag the blue midpoint to curve");
                    case Tool.Spotlight: return I18n.T("Arrastra sobre lo importante: el resto se oscurece \u00B7 puedes poner varios focos", "Drag over what matters: the rest is dimmed \u00B7 you can add multiple spotlights");
                    case Tool.Rect: return I18n.T("Arrastra para enmarcar \u00B7 May\u00FAs hace un cuadrado \u00B7 pincha una marca para moverla", "Drag to frame \u00B7 Shift makes a square \u00B7 click a mark to move it");
                    case Tool.Ellipse: return I18n.T("Arrastra para rodear \u00B7 May\u00FAs hace un c\u00EDrculo", "Drag to circle \u00B7 Shift makes a circle");
                    case Tool.Text: return I18n.T("Haz clic donde quieras escribir", "Click where you want to type");
                    case Tool.Counter: return I18n.T("Cada clic pone el siguiente n\u00FAmero: 1, 2, 3\u2026", "Each click adds the next number: 1, 2, 3\u2026");
                    case Tool.Highlight: return I18n.T("Arrastra sobre lo que quieras resaltar", "Drag over what you want to highlight");
                    case Tool.Pixelate: return I18n.T("Arrastra sobre lo que quieras pixelar \u00b7 para contrase\u00f1as o datos sensibles, mejor Tapar (X)", "Drag over what you want to pixelate \u00b7 for passwords or sensitive data, use Redact (X)");
                    case Tool.Redact: return I18n.T("Arrastra sobre lo que quieras tapar: un bloque s\u00f3lido que borra de verdad lo de debajo", "Drag over what you want to redact: a solid block that permanently erases what is underneath");
                    default: return I18n.T("Arrastra el \u00E1rea que quieres conservar \u00B7 Ctrl+Z lo deshace", "Drag the area you want to keep \u00B7 Ctrl+Z undoes");
                }
            }
        }

        // Size and color of the text being typed: the mark's own when editing one, otherwise the current options.
        float BoxFont { get { return editing != null ? editing.FontPx : FontFor(Weight); } }
        Color BoxColor { get { return editing != null ? editing.Color : Color; } }
        Font boxFont;
        int boxSlack;
        static Graphics measureDc;

        void StartText(PointF at, Shape existing)
        {
            Action started = DrawStart;
            if (started != null) started();
            editing = existing;
            boxAt = existing != null ? existing.A : at;
            box = new TextBox();
            box.Multiline = true;
            box.WordWrap = false; // the box grows with the text; wrapping would make it jump while typing
            box.BorderStyle = BorderStyle.None;
            box.HandleCreated += delegate(object o, EventArgs ea) { NoMargins((TextBox)o); };
            if (existing != null) box.Text = existing.Text;
            PlaceBox();
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
            Controls.Add(box);
            box.Focus();
            box.SelectionStart = box.Text.Length;
            Invalidate();
            Fire(StateChanged);
        }

        // Font, colors and position of the text box for the current zoom (also after a resize or an option change).
        void PlaceBox()
        {
            if (box == null) return;
            float px = Math.Max(9f, BoxFont * k);
            if (boxFont == null || Math.Abs(boxFont.Size - px) > 0.01f)
            {
                Font old = boxFont;
                boxFont = new Font("Segoe UI Semibold", px, GraphicsUnit.Pixel);
                box.Font = boxFont;
                if (old != null) old.Dispose();
                NoMargins(box);
            }
            Color c = BoxColor;
            box.BackColor = c;
            box.ForeColor = Painter.Contrast(c);
            box.Location = BoxLocation();
            SizeBox();
            Invalidate();
        }

        Point BoxLocation()
        {
            int pad = (int)Math.Round(BoxFont * 0.35f * k);
            Point pt = ToScreen(boxAt);
            return new Point(pt.X + pad, pt.Y + pad);
        }

        bool BoxStale()
        {
            return box.Location != BoxLocation() || boxFont == null || Math.Abs(boxFont.Size - Math.Max(9f, BoxFont * k)) > 0.01f;
        }

        // Text drawn from the box's very edge, so it sits where the finished mark will draw it.
        static void NoMargins(TextBox b)
        {
            if (b.IsHandleCreated) Native.SendMessage(b.Handle, 0xD3, (IntPtr)3, IntPtr.Zero); // EM_SETMARGINS: left and right 0
        }

        // As big as the text (measured the way the TextBox draws it, with GDI) plus room for the caret, which stays
        // inside the label's padding: the label looks as it will once committed.
        void SizeBox()
        {
            if (box == null) return;
            Rectangle before = BoxLabelRect();
            string t = box.Text.Length > 0 ? box.Text : I18n.T("Escribe aqu\u00ED", "Type here");
            if (t.EndsWith("\n")) t += " "; // a new, still empty line counts too
            // Measured on a DC like the box's own: TextRenderer's default measuring DC comes out about 10% wider.
            if (measureDc == null) measureDc = Graphics.FromImage(new Bitmap(1, 1));
            Size sz = TextRenderer.MeasureText(measureDc, t, box.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            boxSlack = Math.Max(2, (int)Math.Round(box.Font.Size * 0.25f));
            box.Size = new Size(sz.Width + boxSlack, sz.Height);
            Rectangle after = BoxLabelRect();
            int m = Pu(8);
            Invalidate(Rectangle.Inflate(Rectangle.Union(before, after), m, m));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (box == null) return;
            Fit();
            PlaceBox();
        }

        void DropBox(TextBox b)
        {
            Controls.Remove(b);
            b.Dispose();
            if (boxFont != null) { boxFont.Dispose(); boxFont = null; }
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
            DropBox(b);
            Focus();
            Shape ed = editing;
            editing = null;
            if (ed != null)
            {
                if (t != ed.Text)
                {
                    Commit();
                    if (t.Length == 0)
                    {
                        // Emptied: the mark goes, and so does its hover outline (the pointer is usually still on it).
                        shapes.Remove(ed);
                        selected = null;
                        if (hover == ed) hover = null;
                    }
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
            DropBox(b);
            Focus();
            Invalidate();
            Fire(StateChanged);
        }

        // Final image: cropped, annotated and on the backdrop if enabled.
        public Bitmap Render()
        {
            Size o = OutputSizeRaw;
            Rectangle area = Rectangle.Round(Crop);
            bool copy = area == Crop && area.Size == o && area.X >= 0 && area.Y >= 0 && area.Right <= Img.Width && area.Bottom <= Img.Height;
            Bitmap b = copy ? CopyArea(Img, area) : new Bitmap(o.Width, o.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                if (!copy) g.DrawImage(Img, new Rectangle(0, 0, b.Width, b.Height), Crop.X, Crop.Y, Crop.Width, Crop.Height, GraphicsUnit.Pixel);
                g.TranslateTransform(-Crop.X, -Crop.Y);
                Painter.DrawMarks(g, shapes, null, null, Img, Crop);
            }
            if (!BgOn || Bg == null) return b;
            using (b) return Backdrop.Compose(b, Bg, Info());
        }

        // The capture (or the cropped part) copied row by row: the same pixels as DrawImage, several times faster.
        static Bitmap CopyArea(Bitmap src, Rectangle r)
        {
            Bitmap b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            BitmapData s = src.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData d = b.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                UIntPtr row = new UIntPtr((uint)(r.Width * 4));
                for (int y = 0; y < r.Height; y++)
                    Native.CopyMemory(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), new IntPtr(s.Scan0.ToInt64() + (long)y * s.Stride), row);
            }
            finally
            {
                src.UnlockBits(s);
                b.UnlockBits(d);
            }
            return b;
        }

        // Only the annotations in that area, on transparent (to overlay on a video).
        public Bitmap RenderMarks(Rectangle area)
        {
            Bitmap b = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.TranslateTransform(-area.X, -area.Y);
                Painter.DrawMarks(g, shapes, null, null, Img, area);
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
            if (bgFill != null) bgFill.Dispose();
            DropShadow();
            if (info != null) info.Dispose();
            if (roundView != null) roundView.Dispose();
            lock (liveLock)
            {
                liveOn = false;
                if (live != null) live.Dispose();
                live = null;
            }
            Img.Dispose();
        }

        // Video editor: shows another frame of the same recording under the marks (same size, so nothing moves).
        public void ReplaceImage(Bitmap img)
        {
            if (img.Width != Img.Width || img.Height != Img.Height) { img.Dispose(); return; }
            Bitmap old = Img;
            Img = img;
            old.Dispose();
            // A pixelated mark caches the mosaic of the picture it was drawn on.
            foreach (Shape s in shapes) s.DropCache();
            if (cur != null) cur.DropCache();
            if (view != null) { view.Dispose(); view = null; }
            if (roundView != null) { roundView.Dispose(); roundView = null; roundKey = null; }
            lock (liveLock) liveOn = false;
            Invalidate();
        }
    }

}
