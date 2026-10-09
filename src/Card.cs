// Stackshot - A thumbnail in the floating stack.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // A stack thumbnail, drawn like a Quick Access Overlay: rounded image with a soft shadow; on hover the capture blurs
    // and dims behind Copy / Save and round corner buttons. It slides in from the edge, springs into place, can be
    // swiped away or dragged from anywhere (buttons included) into other apps.
    public class Card : FloatWindow
    {
        public enum Exit { Slide, Swipe, Drop }

        public static bool ForceHover = false;

        const double FlashMs = 1150;

        class Btn { public Rectangle R; public string Glyph; public string Label; public Action Do; public bool Corner; public bool Close; }

        public string FilePath;
        public string SavedPath;   // copy in the save folder, if the user kept it
        public readonly bool IsMedia;
        public string Device;      // monitor it is on
        public bool Leaving;       // removed from the stack, only the exit animation is left
        readonly ShotStack owner;
        readonly List<Btn> btns = new List<Btn>();
        Bitmap preview;
        MI.BitmapSource face, shadow;
        Rectangle faceRect;
        Size shadowFor;
        Size origSize = new Size(16, 9);
        float nextScale = 1f;
        Rectangle home;
        bool hover, pressed, gestureDecided, swiping, dragging, migrating, parked, fadeByPos, closeAfterDrag;
        int hot = -1, prevHot = -1, pressBtn = -1, pressShown = -1, swipeStartX;
        Point pressPt;
        double swipeV, swipeLastX, swipeLastT;
        string flash;
        bool flashGood;
        readonly Tween dim = new Tween(1), hoverT = new Tween(0), hotT = new Tween(1), pressT = new Tween(0),
                       flashT = new Tween(0), badgeT = new Tween(1), wait = new Tween(0);

        public Card(ShotStack owner, string path)
        {
            this.owner = owner;
            FilePath = path;
            IsMedia = ShotStack.IsMediaFile(path);
        }

        protected override bool PerPixel { get { return true; } }

        // Pressed, swiping or dragging: the stack must not move it to another monitor.
        public bool Busy { get { return pressed || swiping || dragging; } }

        // Hidden because there are more thumbnails than fit.
        public bool Parked { get { return parked; } }

        public Size WantedFor(float scale)
        {
            int w = (int)Math.Round(256 * scale);
            int h = (int)Math.Round(w * (double)origSize.Height / Math.Max(1, origSize.Width));
            return new Size(w, Math.Max((int)Math.Round(110 * scale), Math.Min((int)Math.Round(200 * scale), h)));
        }

        protected override bool HoldX { get { return swiping; } }

        protected override void Settled()
        {
            if (!swiping && !Leaving) fadeByPos = false;
        }

        // Fades while swiped towards the edge; releasing past 30% of its width dismisses it.
        protected override double AlphaFactor()
        {
            double f = dim.Value;
            if (fadeByPos)
            {
                double d = (home.X - x) / Math.Max(1.0, body.Width * 0.6);
                f *= 1 - 0.85 * Math.Max(0, Math.Min(1, d));
            }
            return f;
        }

        void Fit(Size size)
        {
            Pad = P(26);
            SetSize(size);
            BuildButtons();
            Redraw();
        }

        // Called by the stack with its slot. New cards slide in; when the stack changes monitor they fade out here and
        // in there; cards that don't fit are parked (slide away, fade out and hide until they fit again).
        public void Place(Rectangle r, string device, float scale, int order, bool visible)
        {
            home = r;
            if (Leaving) return;
            if (Device == null)
            {
                Device = device;
                s = scale;
                Fit(r.Size);
                if (visible) SlideIn();
                else { parked = true; JumpTo(r.X, r.Y); }
                return;
            }
            if (!visible)
            {
                bool sameScreen = device == Device;
                Device = device;
                nextScale = scale;
                if (parked) return;
                parked = true;
                migrating = false;
                if (!Visible) return;
                if (sameScreen) MoveTo(r.X, r.Y, 320, 0.9, 0); // drift towards its side while fading
                alpha.Go(0, 170, 0, Ease.OutCubic, Park);
                Anim.Wake(this);
                return;
            }
            if (parked)
            {
                // Back in view: enter from the side it left.
                parked = false;
                bool fromAbove = y < r.Y;
                Device = device;
                s = scale;
                Fit(r.Size);
                if (!Visible)
                {
                    alpha.Set(0);
                    ApplyAlpha();
                    JumpTo(r.X, r.Y + (fromAbove ? -P(48) : P(48)));
                }
                ShowQuiet();
                alpha.Go(1, 200, order * 18, Ease.OutCubic, null);
                MoveTo(r.X, r.Y, 320, 0.8, order * 18);
                return;
            }
            if (device != Device)
            {
                Device = device;
                nextScale = scale;
                if (!migrating)
                {
                    migrating = true;
                    double delay = order * 28;
                    MoveTo(x - P(28), y, 300, 1, delay);
                    alpha.Go(0, 130, delay, Ease.InCubic, Arrive);
                    Anim.Wake(this);
                }
                return;
            }
            if (migrating) { nextScale = scale; return; } // Arrive will use the new slot
            if (scale != s || r.Size != body) { s = scale; Fit(r.Size); }
            MoveTo(r.X, r.Y, 320, 0.8, 0);
        }

        void SlideIn()
        {
            JumpTo(home.X - P(56), home.Y);
            alpha.Set(0);
            ApplyAlpha();
            alpha.Go(1, 220, 0, Ease.OutCubic, null);
            MoveTo(home.X, home.Y, 260, 0.68, 0);
        }

        // Faded out on the old monitor: jump to the new one and slide in.
        void Arrive()
        {
            migrating = false;
            if (Leaving || IsDisposed) return;
            s = nextScale;
            Fit(home.Size);
            SlideIn();
        }

        // Fully parked: hide and free the window's pixels. The small pre-scaled face stays, so scrolling back to it
        // never decodes the file again.
        void Park()
        {
            if (!parked || Leaving) return;
            moving = false;
            Hide();
            if (preview != null) { preview.Dispose(); preview = null; }
            ReleaseSurface();
        }

        // Leaves the stack with an animation and closes when done.
        public void Dismiss(Exit how, double delay)
        {
            if (Leaving) return;
            Leaving = true;
            migrating = false;
            pressed = false;
            hover = false;
            if (!IsHandleCreated || !Visible || parked) { Close(); return; }
            SetHot(-1);
            UpdateHover();
            switch (how)
            {
                case Exit.Swipe: // follow the swipe towards the edge
                    fadeByPos = true;
                    MoveTo(x - P(160), y, 140, 1, 0);
                    alpha.Go(0, 180, 0, Ease.OutCubic, Finish);
                    break;
                case Exit.Drop: // dropped into another app: fade out in place
                    alpha.Go(0, 170, 0, Ease.OutCubic, Finish);
                    break;
                default:
                    MoveTo(x - P(44), y, 300, 1, delay);
                    alpha.Go(0, 190, delay, Ease.InCubic, Finish);
                    break;
            }
            Anim.Wake(this);
        }

        void Finish()
        {
            if (dragging) { closeAfterDrag = true; return; }
            Close();
        }

        // Pasted: show a brief label, then leave.
        public void Used(string text, Action then)
        {
            if (Leaving) return;
            if (parked) { then(); return; }
            Flash(text, true);
            wait.Set(0);
            wait.Go(1, 560, 0, Ease.Linear, then);
            Anim.Wake(this);
        }

        protected override bool StepExtra(double now)
        {
            bool repaint = hoverT.Running || hotT.Running || pressT.Running || flashT.Running || badgeT.Running;
            dim.Step(now);
            hoverT.Step(now);
            hotT.Step(now);
            pressT.Step(now);
            flashT.Step(now);
            badgeT.Step(now);
            wait.Step(now);
            if (IsDisposed) return false;
            if (repaint) Redraw();
            return dim.Running || hoverT.Running || hotT.Running || pressT.Running || flashT.Running || badgeT.Running || wait.Running;
        }

        // Validates the file and records its real size. The preview is released once the thumbnail face is rendered, so
        // each card holds little more than what is visible.
        public bool Reload()
        {
            Bitmap p = LoadPreview();
            if (p == null && !IsMedia) return false;
            ReleaseImages();
            preview = p;
            Redraw();
            return true;
        }

        // Fresh capture: the preview comes from memory while the PNG is still being written.
        public void UsePreview(Bitmap p, Size orig)
        {
            ReleaseImages();
            preview = p;
            origSize = orig;
            Redraw();
        }

        Bitmap LoadPreview()
        {
            try
            {
                Size orig;
                if (IsMedia)
                {
                    Bitmap t = ShotStack.ShellThumb(FilePath, 512);
                    if (t != null) origSize = t.Size;
                    return t;
                }
                Bitmap b = ShotStack.LoadPreview(FilePath, 600, out orig);
                origSize = orig;
                return b;
            }
            catch (Exception ex)
            {
                ShotStack.Log("No se pudo leer " + FilePath + ": " + ex.Message);
                return null;
            }
        }

        void ReleaseImages()
        {
            if (preview != null) { preview.Dispose(); preview = null; }
            face = null;
            loadingFor = Rectangle.Empty;
        }

        // Close top-left, Pin (or Open for videos) top-right, Edit bottom-left; Copy and Save in the middle.
        void BuildButtons()
        {
            btns.Clear();
            int W = body.Width, H = body.Height;
            int cs = P(26), m = P(8);
            Corner(new Rectangle(m, m, cs, cs), "close", "Cerrar", delegate { owner.Remove(this); }).Close = true;
            if (IsMedia) Corner(new Rectangle(W - m - cs, m, cs, cs), "play!", "Abrir", OpenFile);
            else Corner(new Rectangle(W - m - cs, m, cs, cs), "pin", "Fijar en pantalla", Pin);
            Corner(new Rectangle(m, H - m - cs, cs, cs), "edit", "Editar", Edit);
            if (SavedPath != null) Corner(new Rectangle(W - m - cs, H - m - cs, cs, cs), "folder", "Mostrar en la carpeta", ShowInFolder);

            List<Btn> pills = new List<Btn>();
            pills.Add(Pill("Copiar", Copy));
            pills.Add(SavedPath == null ? Pill("Guardar", Keep) : Pill("Abrir", IsMedia ? (Action)OpenFile : (Action)Edit));
            int ph = P(28), gap = P(8), total = 0;
            foreach (Btn b in pills) { b.R.Width = Measure(b.Label) + P(26); total += b.R.Width; }
            total += gap * (pills.Count - 1);
            int left = (W - total) / 2, top = (H - ph) / 2;
            foreach (Btn b in pills)
            {
                b.R = new Rectangle(left, top, b.R.Width, ph);
                left += b.R.Width + gap;
                btns.Add(b);
            }
        }

        Btn Corner(Rectangle r, string glyph, string label, Action act)
        {
            Btn b = new Btn();
            b.R = r; b.Glyph = glyph; b.Label = label; b.Do = act; b.Corner = true;
            btns.Add(b);
            return b;
        }

        static Btn Pill(string label, Action act)
        {
            Btn b = new Btn();
            b.Label = label; b.Do = act;
            return b;
        }

        int Measure(string text)
        {
            return (int)Math.Ceiling(Ink.Px(text, Ds.Semibold, P(13), Palette.HudLabel).WidthIncludingTrailingWhitespace);
        }

        Point Local(Point p) { return new Point(p.X - Pad, p.Y - Pad); }

        bool Inside(Point local) { return local.X >= 0 && local.Y >= 0 && local.X < body.Width && local.Y < body.Height; }

        int HitTest(Point local)
        {
            if (hoverT.Target <= 0 && !ForceHover) return -1;
            for (int i = 0; i < btns.Count; i++) if (btns[i].R.Contains(local)) return i;
            return -1;
        }

        // Fit the image without upscaling beyond its real size.
        Rectangle ImageRect(Rectangle box)
        {
            double f = Math.Max((double)box.Width / origSize.Width, (double)box.Height / origSize.Height);
            double fit = Math.Min((double)box.Width / origSize.Width, (double)box.Height / origSize.Height);
            // Fill the card unless that would crop more than a sliver; very tall or wide captures are letterboxed.
            if (f / fit > 1.12) f = fit;
            int w = Math.Max(1, (int)Math.Round(origSize.Width * f));
            int h = Math.Max(1, (int)Math.Round(origSize.Height * f));
            return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        // Preview pre-scaled to the card size once, so each frame is a 1:1 copy. When the size changes (a monitor with
        // another scale) the preview is read again off the UI thread, and meanwhile the old face is drawn scaled.
        MI.BitmapSource Face()
        {
            if (body.Width <= 0 || body.Height <= 0) return null;
            Rectangle ir = ImageRect(new Rectangle(Point.Empty, body));
            if (face != null && faceRect == ir) return face;
            if (preview == null)
            {
                if (face != null && !IsMedia) { LoadAsync(ir); return face; }
                preview = LoadPreview();
            }
            if (preview == null) return null;
            using (Bitmap scaled = new Bitmap(ir.Width, ir.Height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                using (ImageAttributes ia = new ImageAttributes())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    ia.SetWrapMode(WrapMode.TileFlipXY); // avoids dark edges when downscaling
                    g.DrawImage(preview, new Rectangle(0, 0, ir.Width, ir.Height), 0, 0, preview.Width, preview.Height, GraphicsUnit.Pixel, ia);
                }
                face = Ink.FromGdi(scaled);
            }
            faceRect = ir;
            preview.Dispose();
            preview = null;
            return face;
        }

        Rectangle loadingFor = Rectangle.Empty;

        void LoadAsync(Rectangle forRect)
        {
            if (loadingFor == forRect || !IsHandleCreated) return; // already asked (or it failed) for this size
            loadingFor = forRect;
            string path = FilePath;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap b = null;
                Size orig = Size.Empty;
                try { b = ShotStack.LoadPreview(path, 600, out orig); }
                catch (Exception ex) { ShotStack.Log("No se pudo leer " + path + ": " + ex.Message); }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (b == null) return; // failed: not asked again for this size
                        if (IsDisposed || path != FilePath || preview != null) { b.Dispose(); return; }
                        loadingFor = Rectangle.Empty;
                        // Back at the size of the face it already has (the monitor changed again meanwhile).
                        if (face != null && faceRect == ImageRect(new Rectangle(Point.Empty, body))) { b.Dispose(); return; }
                        preview = b;
                        origSize = orig;
                        if (!parked) Redraw(); // a parked card uses it when it comes back
                    });
                }
                catch { if (b != null) b.Dispose(); } // closed meanwhile
            });
        }

        MI.BitmapSource Shadow(int w, int h)
        {
            if (shadow != null && shadowFor == new Size(w, h)) return shadow;
            W.Rect b = new W.Rect(Pad, Pad + P(6), body.Width, body.Height);
            shadow = Ink.Shadow(w, h, b, P((float)Ds.RCard), P(18), Ds.Argb(Ds.Dark ? 0.55 : 0.32, 0, 0, 0));
            shadowFor = new Size(w, h);
            return shadow;
        }

        // Pieces of one small pre-blurred shadow, shared by every card of a scale and theme.
        static readonly Dictionary<string, MI.BitmapSource[]> shadowTiles = new Dictionary<string, MI.BitmapSource[]>();

        // The soft shadow as a nine-slice of that tile: a new card, size or monitor costs no blur.
        void PaintShadow(M.DrawingContext dc, int w, int h)
        {
            double r = P((float)Ds.RCard);
            int blur = P(18), off = P(6), c = (int)Math.Ceiling(r) + blur + off + 2, t = 2 * Pad + 2 * c;
            if (body.Width < 2 * c || body.Height < 2 * c) { dc.DrawImage(Shadow(w, h), new W.Rect(0, 0, w, h)); return; }
            int half = t / 2;
            int[] sx = { 0, half - 1, half + 1 }, sw = { half - 1, 2, t - half - 1 };
            string key = t + "|" + r + "|" + Ds.Dark;
            MI.BitmapSource[] parts;
            if (!shadowTiles.TryGetValue(key, out parts))
            {
                MI.BitmapSource tile = Ink.Shadow(t, t, new W.Rect(Pad, Pad + off, 2 * c, 2 * c), r, blur, Ds.Argb(Ds.Dark ? 0.55 : 0.32, 0, 0, 0));
                parts = new MI.BitmapSource[9];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        MI.CroppedBitmap part = new MI.CroppedBitmap(tile, new W.Int32Rect(sx[i], sx[j], sw[i], sw[j]));
                        part.Freeze();
                        parts[i * 3 + j] = part;
                    }
                shadowTiles[key] = parts;
            }
            int[] dx = { 0, half - 1, w - (t - half - 1) }, dw = { half - 1, w - t + 2, t - half - 1 };
            int[] dy = { 0, half - 1, h - (t - half - 1) }, dh = { half - 1, h - t + 2, t - half - 1 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (i != 1 || j != 1) // the middle is under the card
                        dc.DrawImage(parts[i * 3 + j], new W.Rect(dx[i], dy[j], dw[i], dh[j]));
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            if (body.Width <= 0 || body.Height <= 0) return;
            PaintShadow(dc, w, h);
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            PaintBody(dc, ForceHover ? 1 : hoverT.Value);
            dc.Pop();
        }

        void PaintBody(M.DrawingContext dc, double h)
        {
            Palette pal = Ds.Brushes;
            double R = P((float)Ds.RCard);
            W.Rect b = new W.Rect(0, 0, body.Width, body.Height);
            dc.PushClip(new M.RectangleGeometry(b, R, R));
            Ink.Round(dc, pal.Thumb, b, 0);
            MI.BitmapSource f = Face();
            if (f != null)
            {
                Rectangle ir = ImageRect(new Rectangle(Point.Empty, body));
                W.Rect fr = new W.Rect(ir.X, ir.Y, ir.Width, ir.Height);
                dc.DrawImage(f, fr);
            }
            else
            {
                M.FormattedText t = Ink.Px(Path.GetFileName(FilePath), Ds.Medium, P(12), pal.Label2);
                t.MaxTextWidth = Math.Max(1, body.Width - P(24));
                t.MaxLineCount = 1;
                t.Trimming = W.TextTrimming.CharacterEllipsis;
                Ink.Center(dc, t, b);
            }
            // Hover keeps the capture visible: a light veil, a little darker at the edges where the corner buttons sit.
            if (h > 0.004)
            {
                Ink.Round(dc, Ds.Argb(0.22 * h, 0, 0, 0), b, 0);
                M.RadialGradientBrush vig = new M.RadialGradientBrush(Ds.Argb(0, 0, 0, 0), Ds.Argb(0.22 * h, 0, 0, 0));
                vig.RadiusX = vig.RadiusY = 0.75;
                vig.Freeze();
                dc.DrawRectangle(vig, null, b);
            }
            dc.Pop();
            Ink.Hairline(dc, pal.Dark ? Ds.Argb(0.16, 255, 255, 255) : Ds.Argb(0.14, 0, 0, 0), b, R);

            double rest = 1 - h;
            if (rest > 0.004)
            {
                dc.PushOpacity(rest);
                PaintBadges(dc, pal);
                dc.Pop();
            }
            if (h > 0.004) PaintButtons(dc, h);
            PaintFlash(dc, b, R);
        }

        // Resting state: play button and format on videos, green check once saved.
        void PaintBadges(M.DrawingContext dc, Palette pal)
        {
            if (IsMedia)
            {
                double d = P(40);
                W.Rect c = new W.Rect((body.Width - d) / 2, (body.Height - d) / 2, d, d);
                dc.DrawEllipse(Ds.Brush(Ds.Argb(0.42, 0, 0, 0)), null, new W.Point(c.X + d / 2, c.Y + d / 2), d / 2, d / 2);
                Glyph.Draw(dc, "play!", c.X + P(10) + P(1), c.Y + P(10), P(20), Palette.HudLabel, 0);
                string ext = Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
                M.FormattedText t = Ink.Px(ext, Ds.Semibold, P(10.5f), Palette.HudLabel);
                double lw = t.WidthIncludingTrailingWhitespace + P(12), lh = P(18);
                W.Rect lr = new W.Rect(P(8), body.Height - P(8) - lh, lw, lh);
                Ink.Round(dc, Ds.Argb(0.48, 0, 0, 0), lr, lh / 2);
                Ink.Center(dc, t, lr);
            }
            if (SavedPath != null)
            {
                double pop = badgeT.Value, d = P(20) * pop;
                if (d > 2)
                {
                    double cx = body.Width - P(8) - P(10), cy = body.Height - P(8) - P(10);
                    dc.DrawEllipse(Ds.Brush(pal.Green), new M.Pen(Ds.Brush(Ds.Argb(0.9, 255, 255, 255)), P(1.5f)), new W.Point(cx, cy), d / 2, d / 2);
                    double gs = P(13) * pop;
                    Glyph.Draw(dc, "check", cx - gs / 2, cy - gs / 2, gs, Palette.HudLabel, P(2.2f) * pop);
                }
            }
        }

        double HotAmount(int i)
        {
            double t = hotT.Value;
            return (i == hot ? t : 0) + (i == prevHot ? 1 - t : 0);
        }

        void PaintButtons(M.DrawingContext dc, double h)
        {
            Palette pal = Ds.Brushes;
            double lift = (1 - h) * P(6);
            dc.PushOpacity(h);
            for (int i = 0; i < btns.Count; i++)
            {
                Btn b = btns[i];
                double hotA = HotAmount(i), press = i == pressShown ? pressT.Value : 0;
                double k = (b.Corner ? 0.82 + 0.18 * h : 0.94 + 0.06 * h) * (1 - 0.06 * press);
                W.Rect r = new W.Rect(b.R.X, b.R.Y + (b.Corner ? 0 : lift), b.R.Width, b.R.Height);
                W.Point c = new W.Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                dc.PushTransform(new M.ScaleTransform(k, k, c.X, c.Y));
                // White controls with dark ink, as in CleanShot's overlay: readable on any capture.
                M.Color bg = b.Close ? Mix(Ds.Argb(0.94, 255, 255, 255), pal.Red, hotA) : Mix(Ds.Argb(0.94, 255, 255, 255), Ds.Rgb(255, 255, 255), hotA);
                M.Color ink = b.Close && hotA > 0.5 ? Palette.HudLabel : Ds.Rgb(29, 29, 31);
                W.Rect sh = r;
                sh.Offset(0, P(1));
                if (b.Corner)
                {
                    dc.DrawEllipse(Ds.Brush(Ds.Argb(0.25, 0, 0, 0)), null, new W.Point(c.X, c.Y + P(1)), r.Width / 2 + 0.5, r.Height / 2 + 0.5);
                    dc.DrawEllipse(Ds.Brush(bg), null, c, r.Width / 2, r.Height / 2);
                    double gs = P(14);
                    Glyph.Draw(dc, b.Glyph, c.X - gs / 2 + (b.Glyph == "play!" ? P(1) : 0), c.Y - gs / 2, gs, ink, P(1.8f));
                }
                else
                {
                    Ink.Round(dc, Ds.Argb(0.22, 0, 0, 0), sh, r.Height / 2);
                    Ink.Round(dc, bg, r, r.Height / 2);
                    Ink.Center(dc, Ink.Px(b.Label, Ds.Semibold, P(13), ink), r);
                }
                dc.Pop();
            }
            PaintTip(dc);
            dc.Pop();
        }

        // Name of the hovered corner button, next to it.
        void PaintTip(M.DrawingContext dc)
        {
            int i = hot >= 0 ? hot : prevHot;
            if (i < 0 || i >= btns.Count || !btns[i].Corner) return;
            double a = i == hot ? hotT.Value : 1 - hotT.Value;
            if (a < 0.02) return;
            Btn b = btns[i];
            M.FormattedText t = Ink.Px(b.Label, Ds.Semibold, P(11.5f), Palette.HudLabel);
            double w = t.WidthIncludingTrailingWhitespace + P(14), hh = P(22);
            bool right = b.R.X < body.Width / 2;
            double left = right ? b.R.Right + P(6) - (1 - a) * P(4) : b.R.X - P(6) - w + (1 - a) * P(4);
            left = Math.Max(P(4), Math.Min(body.Width - P(4) - w, left));
            W.Rect r = new W.Rect(left, b.R.Y + (b.R.Height - hh) / 2, w, hh);
            dc.PushOpacity(a);
            Ink.Round(dc, Palette.Hud, r, hh / 2);
            Ink.Center(dc, t, r);
            dc.Pop();
        }

        // Status label (copied, saved, pasted): pops in, holds, then fades.
        void PaintFlash(M.DrawingContext dc, W.Rect b, double R)
        {
            if (flash == null) return;
            double ms = flashT.Value * FlashMs, sc, a;
            if (ms < 240) { double t = ms / 240; sc = 0.7 + 0.3 * Ease.OutBack(t); a = Ease.OutCubic(t); }
            else if (ms < FlashMs - 260) { sc = 1; a = 1; }
            else { double t = (ms - (FlashMs - 260)) / 260; sc = 1 - 0.06 * t; a = 1 - t; }
            if (a <= 0.01) return;
            dc.PushClip(new M.RectangleGeometry(b, R, R));
            Ink.Round(dc, Ds.Argb(0.30 * a, 0, 0, 0), b, 0);
            dc.Pop();
            M.FormattedText t2 = Ink.Px(flash, Ds.Semibold, P(13), Palette.HudLabel);
            double gs = P(15), w = t2.WidthIncludingTrailingWhitespace + gs + P(8) + P(28), hh = P(32);
            W.Rect r = new W.Rect((b.Width - w) / 2, (b.Height - hh) / 2, w, hh);
            W.Point c = new W.Point(r.X + w / 2, r.Y + hh / 2);
            dc.PushOpacity(a);
            dc.PushTransform(new M.ScaleTransform(sc, sc, c.X, c.Y));
            Ink.Round(dc, Palette.Hud, r, hh / 2);
            Ink.Hairline(dc, Palette.HudLine, r, hh / 2);
            Palette pal = Ds.Brushes;
            Glyph.Draw(dc, flashGood ? "check" : "close", r.X + P(14), r.Y + (hh - gs) / 2, gs, flashGood ? pal.Green : pal.Red, P(2.2f));
            dc.DrawText(t2, new W.Point(Math.Round(r.X + P(14) + gs + P(8)), Math.Round(r.Y + (hh - t2.Height) / 2)));
            dc.Pop();
            dc.Pop();
        }

        static M.Color Mix(M.Color a, M.Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return M.Color.FromArgb((byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
                                    (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        // Buttons appear after a short delay so they don't flicker when the mouse passes by.
        void UpdateHover()
        {
            double want = hover && !swiping && !dragging && !Leaving ? 1 : 0;
            if (hoverT.Target == want) return;
            if (want > 0) hoverT.Go(1, 170, hoverT.Value > 0.05 ? 0 : 60, Ease.OutCubic, null);
            else hoverT.Go(0, 180, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            prevHot = hot;
            hot = h;
            hotT.Set(0);
            hotT.Go(1, 120, 0, Ease.OutCubic, null);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Anim.Wake(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Leaving) owner.Wheel(e.Delta);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point p = Local(e.Location);
            if (Leaving || e.Button != MouseButtons.Left || !Inside(p)) return;
            pressed = true;
            FileDrag.Warm(FilePath);
            gestureDecided = false;
            swiping = false;
            pressPt = p;
            pressBtn = HitTest(p);
            if (pressBtn >= 0)
            {
                pressShown = pressBtn;
                pressT.Go(1, 90, 0, Ease.OutCubic, null);
                Anim.Wake(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Leaving) return;
            if (swiping) { SwipeMove(); return; }
            Point p = Local(e.Location);
            bool inside = Inside(p);
            if (inside != hover && !pressed) { hover = inside; UpdateHover(); if (inside) FileDrag.Warm(FilePath); }
            if (pressed && !gestureDecided && (e.Button & MouseButtons.Left) != 0)
            {
                int dx = p.X - pressPt.X, dy = p.Y - pressPt.Y;
                Size d = SystemInformation.DragSize;
                if (Math.Abs(dx) > d.Width || Math.Abs(dy) > d.Height)
                {
                    // Wherever it was pressed (even on a button), movement means a gesture, not a click.
                    gestureDecided = true;
                    pressBtn = -1;
                    if (pressT.Target > 0) pressT.Go(0, 120, 0, Ease.OutCubic, null);
                    // Left: swipe to dismiss, like CleanShot.
                    if (dx < 0 && Math.Abs(dx) >= Math.Abs(dy)) { StartSwipe(dx); return; }
                    // Any other direction: drag the file to another app.
                    pressed = false;
                    StartDrag();
                    return;
                }
            }
            if (!pressed) SetHot(inside ? HitTest(p) : -1);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (swiping || Inside(Local(PointToClient(Control.MousePosition)))) return;
            hover = false;
            SetHot(-1);
            UpdateHover();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (Leaving) return;
            if (swiping) { EndSwipe(); return; }
            Point p = Local(e.Location);
            if (pressShown >= 0 && pressT.Target > 0) { pressT.Go(0, 160, 0, Ease.OutCubic, null); Anim.Wake(this); }
            if ((e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) && Inside(p)) { owner.Remove(this); return; }
            if (e.Button != MouseButtons.Left || !pressed) return;
            pressed = false;
            int down = pressBtn;
            pressBtn = -1;
            Action act = null;
            if (down >= 0) { if (HitTest(p) == down) act = btns[down].Do; }
            else if (Inside(p)) act = IsMedia ? (Action)OpenFile : (Action)Edit;
            if (act != null) BeginInvoke(act);
            hover = Inside(p);
            UpdateHover();
            SetHot(hover ? HitTest(p) : -1);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Mouse capture lost mid-swipe (Alt+Tab...): spring back.
            BeginInvoke((Action)delegate
            {
                if (!swiping || IsDisposed) return;
                swiping = false;
                pressed = false;
                MoveTo(home.X, home.Y, 420, 0.62, 0);
                UpdateHover();
            });
        }

        void StartSwipe(int dx)
        {
            swiping = true;
            fadeByPos = true;
            swipeStartX = Control.MousePosition.X - dx;
            swipeV = 0;
            swipeLastX = x;
            swipeLastT = Anim.Now;
            vx = 0;
            Cursor = Cursors.Default;
            SetHot(-1);
            UpdateHover();
            SwipeMove();
        }

        void SwipeMove()
        {
            Point m = Control.MousePosition;
            // Well inside another monitor, the user is carrying the file there rather than dismissing it (a quick flick
            // that just crosses the edge still dismisses).
            if (Screen.FromPoint(new Point(m.X + P(100), m.Y)).DeviceName != Device)
            {
                swiping = false;
                pressed = false;
                MoveTo(home.X, home.Y, 420, 0.75, 0);
                StartDrag();
                return;
            }
            int dx = m.X - swipeStartX;
            // Rubber-band resistance to the right.
            double nx = home.X + (dx < 0 ? dx : Math.Min(P(14), dx * 0.2));
            double now = Anim.Now, dt = now - swipeLastT;
            if (dt >= 4)
            {
                swipeV = 0.6 * ((nx - swipeLastX) / (dt / 1000.0)) + 0.4 * swipeV;
                swipeLastX = nx;
                swipeLastT = now;
            }
            x = nx;
            vx = 0;
            ApplyPos();
            ApplyAlpha();
        }

        void EndSwipe()
        {
            swiping = false;
            pressed = false;
            double gone = home.X - x;
            if (Anim.Now - swipeLastT > 90) swipeV = 0; // stopped before releasing
            // Dismissed past 30% or when flung towards the edge.
            if (gone > body.Width * 0.3 || (swipeV < -700 && gone > P(12)))
            {
                vx = Math.Min(swipeV, -500);
                owner.Remove(this, Exit.Swipe);
                return;
            }
            MoveTo(home.X, home.Y, 420, 0.62, 0); // spring back with a small bounce
            UpdateHover();
        }

        // While dragging, the ghost follows the cursor and the card dims.
        void StartDrag()
        {
            ShotStack.WaitWritten(FilePath);
            if (!File.Exists(FilePath)) return;
            dragging = true;
            SetHot(-1);
            UpdateHover();
            dim.Go(0.35, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
            DragDropEffects result = DragDropEffects.None;
            try
            {
                Point grab = pressPt;
                Bitmap ghost;
                int w = Math.Max(1, body.Width), h = Math.Max(1, body.Height);
                MI.RenderTargetBitmap flat = Ink.Render(w, h, delegate(M.DrawingContext dc)
                {
                    Ink.Round(dc, Ds.Brushes.Thumb, new W.Rect(0, 0, w, h), 0);
                    MI.BitmapSource f = Face();
                    Rectangle ir = ImageRect(new Rectangle(0, 0, w, h));
                    if (f != null) dc.DrawImage(f, new W.Rect(ir.X, ir.Y, ir.Width, ir.Height));
                }, null);
                using (Bitmap b = Ink.ToGdi(flat)) ghost = FileDrag.Ghost(b, P(320), P(10), ref grab);
                result = FileDrag.Run(this, FilePath, ghost, grab);
            }
            catch (Exception ex) { ShotStack.Log("Arrastrar: " + ex.Message); }
            dragging = false;
            if (IsDisposed) return;
            if (closeAfterDrag) { Close(); return; }
            // Dropped into another app: done, remove it.
            if (result != DragDropEffects.None) { owner.Remove(this, Exit.Drop); return; }
            dim.Go(1, 220, 0, Ease.OutCubic, null);
            hover = Inside(Local(PointToClient(Control.MousePosition)));
            UpdateHover();
            Anim.Wake(this);
        }

        void Copy()
        {
            if (IsMedia)
            {
                try
                {
                    StringCollection sc = new StringCollection();
                    sc.Add(FilePath);
                    Clipboard.SetFileDropList(sc);
                    Flash("Copiado", true);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Copiar: " + ex.Message);
                    Flash("No se pudo copiar", false);
                }
                return;
            }
            string path = FilePath;
            WithFull(path, delegate(Bitmap img, string error)
            {
                try
                {
                    if (img == null) throw new IOException(error);
                    owner.CopyTracked(path, img, false, true);
                    if (!IsDisposed) Flash("Copiado", true);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Copiar: " + ex.Message);
                    if (!IsDisposed) Flash("No se pudo copiar", false);
                }
            });
        }

        // Decodes the whole capture off the UI thread (a large PNG takes a while), then continues on it.
        void WithFull(string path, Action<Bitmap, string> then)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap img = null;
                string error = null;
                try { img = ShotStack.LoadFull(path); }
                catch (Exception ex) { error = ex.Message; }
                owner.Ui(delegate { then(img, error); });
            });
        }

        void Edit() { owner.OpenEditor(FilePath); }

        void Pin()
        {
            WithFull(FilePath, delegate(Bitmap img, string error)
            {
                if (img == null) { ShotStack.Log("Fijar: " + error); return; }
                try { PinWindow.Open(img); }
                catch (Exception ex) { img.Dispose(); ShotStack.Log("Fijar: " + ex.Message); }
            });
            owner.Remove(this);
        }

        void OpenFile()
        {
            ShotStack.WaitWritten(FilePath);
            try { Process.Start(FilePath); }
            catch (Exception ex) { ShotStack.Log("Abrir: " + ex.Message); }
        }

        void ShowInFolder()
        {
            ShotStack.WaitWritten(FilePath);
            try { Process.Start(Native.Explorer, "/select,\"" + (SavedPath ?? FilePath) + "\""); }
            catch (Exception ex) { ShotStack.Log("Carpeta: " + ex.Message); }
        }

        void Keep()
        {
            try { owner.Keep(FilePath); }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                Flash("No se pudo guardar", false);
            }
        }

        public void MarkSaved(string dest)
        {
            bool first = SavedPath == null;
            SavedPath = dest;
            BuildButtons();
            if (first)
            {
                badgeT.Set(0);
                badgeT.Go(1, 420, 0, Ease.OutBack, null);
                Flash("Guardada", true);
            }
            else Redraw();
        }

        void Flash(string text, bool good)
        {
            flash = text;
            flashGood = good;
            flashT.Set(0);
            flashT.Go(1, FlashMs, 0, Ease.Linear, delegate { flash = null; Redraw(); });
            Anim.Wake(this);
        }

        // Theme switched: shadow and colors are rebuilt on the next frame.
        public void Restyle()
        {
            shadow = null;
            Redraw();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            ReleaseImages();
        }
    }

}
