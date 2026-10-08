// Stackshot - Main window: home with the mascot, hotkeys and all settings, macOS-style.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Stackshot
{
    // A single owner-drawn window: sidebar on the left, current section on the right. Changes apply immediately (no
    // Save button). Closing it keeps Stackshot in the tray.
    // Performance: sidebar and content are drawn into two DIBs that are rebuilt only on change; each frame blits them
    // and draws the mascot and animations on top. No timer runs while hidden.
    public partial class HomeWindow : Form
    {
        const int LW = 980, LH = 660, Side = 240, Bar = 52;
        static readonly string[] PageIds = { "home", "keys", "general", "editor", "record", "mascot", "about" };
        static readonly string[] PageNames = { "Inicio", "Atajos", "General", "Fondo y editor", "Grabaci\u00F3n", "Mascota", "Acerca de" };
        static readonly string[] PageIcons = { "home", "keyboard", "gear", "photo", "video", "bot", "info" };

        readonly ShotStack owner;
        readonly Settings settings;
        float s = 1f;
        string page = "home";
        readonly List<Widget> items = new List<Widget>();     // current section (content coordinates)
        readonly List<Widget> nav = new List<Widget>();       // sidebar (window coordinates)
        Widget hot, pressed;
        Dib side, content, frame;   // frame: our own back buffer, so each repaint composes and copies only what changed
        bool sideDirty = true, contentDirty = true;
        int contentHeight;
        double scroll, scrollTarget;
        readonly Mascot mascot = new Mascot();
        readonly Timer timer = new Timer();
        readonly Tween pageIn = new Tween(1);
        Intro intro;
        HotkeyRow listening;
        TextBox nameBox;
        Rectangle nameRect;
        Point mouse = new Point(-1, -1);
        Point lastScreenMouse;
        bool active, mascotHot;
        int captionHot = -1, captionDown = -1;
        double nextTip, lastFrame;
        string bubbleText;
        RectangleF heroMascot;          // where the big mascot goes on Home (content coordinates)
        readonly Dictionary<int, Font> fonts = new Dictionary<int, Font>();
        Image logo;
        static readonly Random Rng = new Random();

        public HomeWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            Text = "Stackshot";
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Mac.Window;
            KeyPreview = true;
            ShowInTaskbar = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            logo = ShotStack.LoadResourceImage("logo.png");
            mascot.Look = MascotLook.From(settings);
            s = ShotStack.ScaleFor(Screen.FromPoint(Control.MousePosition));
            Size = new Size(P(LW), P(LH));
            Center(Screen.FromPoint(Control.MousePosition));
            timer.Interval = 15;
            timer.Tick += Tick;
            owner.Captured += OnCaptured;
            Updater.Changed += OnUpdaterChanged;
            Build();
        }

        int P(float v) { return (int)Math.Round(v * s); }
        // The real client size (DPI rounding or a monitor change can make it differ from the design size), so the layers
        // always cover the whole window.
        int ViewW { get { return Math.Max(1, Real.Width - P(Side)); } }
        int ViewH { get { return Real.Height; } }

        // The real client size from Windows. WinForms keeps its own figure, worked out for a normal frame, and keeps going
        // back to it; with no frame (WM_NCCALCSIZE) the real client is the whole window, so trusting WinForms left a black
        // band at the bottom and right.
        Size Real
        {
            get
            {
                Native.RECT rc;
                if (IsHandleCreated && Native.GetClientRect(Handle, out rc) && rc.Right > 0 && rc.Bottom > 0) return new Size(rc.Right, rc.Bottom);
                return new Size(P(LW), P(LH));
            }
        }

        void Center(Screen scr)
        {
            Rectangle wa = scr.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + Math.Max(0, (wa.Height - Height) / 2 - P(10)));
        }

        public void Present(string pageId, bool withIntro)
        {
            bool wasHidden = !Visible || WindowState == FormWindowState.Minimized;
            if (!Visible)
            {
                // If its monitor is gone, center it on the cursor's monitor.
                bool onScreen = false;
                foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(Bounds)) onScreen = true;
                if (!onScreen) Center(Screen.FromPoint(Control.MousePosition));
            }
            if (pageId != null && pageId != page) SetPage(pageId, false);
            if (withIntro)
            {
                if (intro != null) intro.Dispose();
                intro = new Intro(Anim.Now);
                intro.TargetSize = P(30);
                intro.Target = new PointF(P(20) + P(15), P(24) + P(15));
            }
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.ForceForeground(Handle);
            if (intro == null && wasHidden && settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(MascotTalk.Hello(settings));
            }
            nextTip = Anim.Now + 25000;
            timer.Start();
            Invalidate();
        }

        void HideToTray()
        {
            CancelListening();
            if (!settings.CloseToTray) { owner.Quit(); return; }
            Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { timer.Stop(); ReleaseLayers(); }
            else if (WindowState != FormWindowState.Minimized && !timer.Enabled) { lastFrame = 0; timer.Start(); } // however it was shown
            owner.HomeShown(Visible && WindowState != FormWindowState.Minimized);
        }

        // Minimized: stop the timer (0% CPU) until restored.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) timer.Stop();
            else if (Visible && !timer.Enabled) { lastFrame = 0; timer.Start(); Invalidate(); }
            // A different client size (DPI change, another monitor): lay everything out again for the new size.
            if (WindowState != FormWindowState.Minimized && side != null && (side.Height != ViewH || (content != null && content.Width != ViewW)))
            {
                Build();
                Invalidate();
            }
            if (owner != null) owner.HomeShown(Visible && WindowState != FormWindowState.Minimized);
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); active = true; Invalidate(); }
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); active = false; CancelListening(); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                owner.Captured -= OnCaptured;
                Updater.Changed -= OnUpdaterChanged;
                timer.Dispose();
                ReleaseLayers();
                if (intro != null) intro.Dispose();
                foreach (Font f in fonts.Values) f.Dispose();
                fonts.Clear();
                foreach (Bitmap b in previews.Values) b.Dispose();
                previews.Clear();
                if (logo != null) logo.Dispose();
            }
            base.Dispose(disposing);
        }

        void ReleaseLayers()
        {
            if (side != null) { side.Dispose(); side = null; }
            if (content != null) { content.Dispose(); content = null; }
            if (frame != null) { frame.Dispose(); frame = null; }
            sideDirty = contentDirty = true;
        }

        // No Windows title bar (the whole client area is ours), but keep the system shadow, corners and minimize
        // animation.
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0083: // WM_NCCALCSIZE
                    if (m.WParam != IntPtr.Zero) { m.Result = IntPtr.Zero; return; }
                    break;
                case 0x0084: // WM_NCHITTEST: the top strip drags the window
                {
                    Point p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                    if (p.Y >= 0 && p.Y < P(Bar) && CaptionAt(p) < 0 && HitTest(p) == null && intro == null) { m.Result = (IntPtr)2; return; }
                    m.Result = (IntPtr)1;
                    return;
                }
                case 0x02E0: // WM_DPICHANGED
                {
                    s = (m.WParam.ToInt64() & 0xFFFF) / 96f;
                    Native.RECT r = (Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                    foreach (Font f in fonts.Values) f.Dispose();
                    fonts.Clear();
                    Bounds = new Rectangle(r.Left, r.Top, P(LW), P(LH));
                    ReleaseLayers();
                    Build();
                    Invalidate();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = round
                int border = 58 | (58 << 8) | (62 << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch { }
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); // SWP_FRAMECHANGED
        }

        void SetPage(string id, bool animate)
        {
            if (Array.IndexOf(PageIds, id) < 0) id = "home";
            CancelListening();
            page = id;
            scroll = scrollTarget = 0;
            Build();
            if (animate) { pageIn.Set(0); pageIn.Go(1, 260, 0, Ease.OutCubic, null); }
            if (id == "home" && settings.MascotOn && animate)
            {
                mascot.PopIn();
                if (settings.MascotTalks && mascot.Bubble == null && Rng.Next(3) == 0) mascot.Say(Tip(), 4200);
            }
            Invalidate();
        }

        void Build()
        {
            hot = pressed = null;
            nav.Clear();
            int y = P(80);
            for (int i = 0; i < PageIds.Length; i++)
            {
                NavItem n = new NavItem(PageIds[i], PageNames[i], PageIcons[i]);
                n.R = new Rectangle(P(12), y, P(Side - 24), P(36));
                nav.Add(n);
                y += P(38);
            }
            MiniMascot mm = new MiniMascot();
            mm.R = new Rectangle(P(12), ViewH - P(118), P(Side - 24), P(104));
            nav.Add(mm);

            items.Clear();
            RemoveNameBox();
            switch (page)
            {
                case "keys": BuildKeys(); break;
                case "general": BuildGeneral(); break;
                case "editor": BuildEditor(); break;
                case "record": BuildRecord(); break;
                case "mascot": BuildMascot(); break;
                case "about": BuildAbout(); break;
                default: BuildHome(); break;
            }
            contentHeight = P(40);
            foreach (Widget w in items) contentHeight = Math.Max(contentHeight, w.R.Bottom + P(40));
            sideDirty = contentDirty = true;
            if (content != null && content.Height < Math.Max(ViewH, contentHeight)) { content.Dispose(); content = null; }
            PlaceNameBox();
        }

        void Tick(object sender, EventArgs e)
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            double now = Anim.Now;
            double dt = lastFrame == 0 ? 16 : Math.Min(60, now - lastFrame);
            lastFrame = now;

            bool repaintAll = false;
            if (intro != null)
            {
                repaintAll = true;
                if (intro.T(now) > Intro.RevealAt + 120 && !introGreeted)
                {
                    introGreeted = true;
                    if (settings.MascotOn)
                    {
                        mascot.PopIn();
                        if (settings.MascotTalks) mascot.Greet(Greeting() + " Soy " + settings.MascotName + ". \u00BFQu\u00E9 capturamos?");
                    }
                }
                if (intro.Done(now)) { intro.Dispose(); intro = null; introGreeted = false; sideDirty = true; }
            }

            // Smooth scrolling.
            double before = scroll;
            scrollTarget = Math.Max(0, Math.Min(Math.Max(0, contentHeight - ViewH), scrollTarget));
            scroll += (scrollTarget - scroll) * (1 - Math.Exp(-dt / 70.0));
            if (Math.Abs(scrollTarget - scroll) < 0.5) scroll = scrollTarget;
            if (scroll != before) { repaintAll = true; PlaceNameBox(); }

            pageIn.Step(now);
            if (pageIn.Running) repaintAll = true;
            foreach (Widget w in items) { w.Step(now); if (w.Running) DirtyContent(w.R); }
            foreach (Widget w in nav) { w.Step(now); if (w.Running) { sideDirty = true; repaintAll = true; } }

            // The mascot tracks the mouse even outside the window.
            Point sm = Control.MousePosition;
            bool moved = sm != lastScreenMouse;
            lastScreenMouse = sm;
            Point cm = PointToClient(sm);
            RectangleF oldBounds = settings.MascotOn ? mascot.PaintBounds : RectangleF.Empty;
            UpdateMascotBox();
            mascot.Step(now, cm, moved);
            if (settings.MascotOn && settings.MascotTalks && now > nextTip && intro == null)
            {
                nextTip = now + 38000 + Rng.NextDouble() * 20000;
                if (mascot.Bubble == null && !mascot.Sleeping) mascot.Say(Tip(), 5200);
            }

            // Adaptive frame rate: 60 fps while animating, 30 while the mouse moves (the mascot follows it), ~25 idle
            // with the mascot, 10 without it.
            if (moved) lastMove = now;
            bool lively = repaintAll || listening != null || (settings.MascotOn && mascot.Lively);
            bool looking = settings.MascotOn && now - lastMove < 600;
            int interval = lively ? 15 : looking ? 31 : settings.MascotOn ? 47 : 100; // multiples of the 15.6 ms system tick
            if (!active && !lively && !looking) interval = settings.MascotOn ? 50 : 200;
            if (timer.Interval != interval) timer.Interval = interval;

            if (repaintAll) { Invalidate(); return; }
            if (settings.MascotOn)
            {
                RectangleF r = RectangleF.Union(oldBounds, mascot.PaintBounds);
                Invalidate(Rectangle.Round(RectangleF.Inflate(r, 2, 2)));
                if (mascot.BubbleAlpha > 0 || bubbleText != null) Invalidate(BubbleRect(true));
                if (!lastBubble.IsEmpty) { Invalidate(lastBubble); if (bubbleText == null) lastBubble = Rectangle.Empty; }
            }
        }
        bool introGreeted;
        double lastMove;

        void UpdateMascotBox()
        {
            if (page == "home" || page == "mascot")
            {
                RectangleF b = heroMascot;
                b.Offset(P(Side), (float)(-scroll + ContentShift));
                mascot.Box = b;
            }
            else
            {
                Widget mm = nav[nav.Count - 1];
                float d = P(74);
                mascot.Box = new RectangleF(mm.R.X + P(10), mm.R.Y + P(14), d, d);
            }
        }

        float ContentShift { get { return (float)((1 - pageIn.Value) * P(12)); } }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Composes only the invalidated rectangle into our own back buffer and copies just that to the screen: a mascot
        // frame costs a few hundred pixels instead of the whole window, and nothing unpainted is ever shown.
        protected override void OnPaint(PaintEventArgs e)
        {
            Size real = Real;
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, new Rectangle(Point.Empty, real));
            if (clip.Width <= 0 || clip.Height <= 0) return;
            EnsureLayers();
            if (frame == null || frame.Width != real.Width || frame.Height != real.Height)
            {
                if (frame != null) frame.Dispose();
                frame = new Dib(real.Width, real.Height);
            }
            int sy = (int)Math.Round(scroll), shift = (int)Math.Round(ContentShift);
            Rectangle sr = Rectangle.Intersect(clip, new Rectangle(0, 0, side.Width, side.Height));
            if (!sr.IsEmpty) Native.BitBlt(frame.Dc, sr.X, sr.Y, sr.Width, sr.Height, side.Dc, sr.X, sr.Y, Native.SRCCOPY);
            Rectangle cr = Rectangle.Intersect(clip, new Rectangle(P(Side), shift, ViewW, ViewH - shift));
            if (!cr.IsEmpty) Native.BitBlt(frame.Dc, cr.X, cr.Y, cr.Width, cr.Height, content.Dc, cr.X - P(Side), cr.Y - shift + sy, Native.SRCCOPY);
            using (Graphics g = frame.DcGraphics())
            {
                g.SetClip(clip);
                PaintOverlays(g, shift);
            }
            Native.GdiFlush();
            IntPtr hdc = e.Graphics.GetHdc();
            try { Native.BitBlt(hdc, clip.X, clip.Y, clip.Width, clip.Height, frame.Dc, clip.X, clip.Y, Native.SRCCOPY); }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }

        void PaintOverlays(Graphics g, int shift)
        {
            // While a section slides in, the background shows above it.
            if (shift > 0) using (SolidBrush wb = new SolidBrush(Mac.Window)) g.FillRectangle(wb, P(Side), 0, ViewW, shift);

            Mac.Quality(g);
            if (pageIn.Value < 1 && timer.Enabled) // a stalled transition must never hide the page
            {
                using (SolidBrush b = new SolidBrush(Mac.Alpha(Mac.Window, 1 - pageIn.Value))) g.FillRectangle(b, P(Side), 0, ViewW, ViewH);
            }
            if (scroll > 1)
            {
                // Content fades under the top strip when scrolled, like a macOS toolbar.
                Rectangle fade = new Rectangle(P(Side), 0, ViewW, P(Bar));
                double k = Math.Min(1, scroll / P(30));
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(fade.X, fade.Y - 1, fade.Width, fade.Height + 2),
                                                                       Mac.Alpha(Mac.Window, k), Color.FromArgb(0, Mac.Window), 90f))
                    g.FillRectangle(b, fade);
            }
            PaintCaption(g);
            if (settings.MascotOn && (intro == null || intro.T(Anim.Now) > Intro.RevealAt))
            {
                Region old = g.Clip;
                if (page == "home" || page == "mascot") g.SetClip(new Rectangle(P(Side), 0, ViewW, ViewH), CombineMode.Intersect);
                mascot.Paint(g);
                g.Clip = old;
                old.Dispose();
                PaintBubble(g);
            }
            if (intro != null) intro.Paint(g, new Rectangle(Point.Empty, Real), Anim.Now, s);
        }

        void EnsureLayers()
        {
            if (side != null && side.Height != ViewH) { side.Dispose(); side = null; }
            if (side == null) { side = new Dib(P(Side), ViewH); sideDirty = true; }
            int ch = Math.Max(ViewH, contentHeight);
            if (content == null || content.Height < ch || content.Width != ViewW) { if (content != null) content.Dispose(); content = new Dib(ViewW, ch); contentDirty = true; }
            if (sideDirty)
            {
                using (Graphics g = side.DcGraphics()) { Mac.Quality(g); PaintSide(g); }
                sideDirty = false;
            }
            if (contentDirty)
            {
                using (Graphics g = content.DcGraphics())
                {
                    Mac.Quality(g);
                    g.Clear(Mac.Window);
                    foreach (Widget w in items) PaintWidget(g, w);
                }
                contentDirty = false;
                dirtyRects.Clear();
            }
            else if (dirtyRects.Count > 0)
            {
                // Only the parts that changed (a hovered tile, a toggle sliding): the rest of the page stays as is.
                if (dirtyRects.Count > 8)
                {
                    Rectangle all = dirtyRects[0];
                    foreach (Rectangle r in dirtyRects) all = Rectangle.Union(all, r);
                    dirtyRects.Clear();
                    dirtyRects.Add(all);
                }
                using (Graphics g = content.DcGraphics())
                using (SolidBrush bg = new SolidBrush(Mac.Window))
                {
                    Mac.Quality(g);
                    foreach (Rectangle r in dirtyRects)
                    {
                        g.SetClip(r);
                        g.FillRectangle(bg, r);
                        foreach (Widget w in items)
                            if (Rectangle.Inflate(w.R, P(16), P(16)).IntersectsWith(r)) PaintWidget(g, w);
                        g.ResetClip();
                    }
                }
                dirtyRects.Clear();
            }
        }

        void PaintWidget(Graphics g, Widget w)
        {
            // A failing widget must not break the rest of the section.
            GraphicsState st = g.Save();
            try { w.Paint(g, this); }
            catch (Exception ex) { ShotStack.Log("Dibujar " + w.GetType().Name + ": " + ex); }
            g.Restore(st);
        }

        readonly List<Rectangle> dirtyRects = new List<Rectangle>();

        // Marks part of the section (content coordinates) for repainting, shadows included.
        void DirtyContent(Rectangle r)
        {
            r.Inflate(P(14), P(14));
            dirtyRects.Add(r);
            Invalidate(new Rectangle(r.X + P(Side), r.Y - (int)Math.Round(scroll) + (int)Math.Round(ContentShift), r.Width, r.Height));
        }

        void PaintSide(Graphics g)
        {
            g.Clear(Mac.Sidebar);
            using (Pen p = new Pen(Color.FromArgb(44, 44, 48))) g.DrawLine(p, P(Side) - 1, 0, P(Side) - 1, ViewH);
            Rectangle lr = new Rectangle(P(20), P(24), P(30), P(30));
            if (intro == null || intro.T(Anim.Now) > Intro.Length - 200)
            {
                if (logo != null) g.DrawImage(logo, lr);
            }
            TextRenderer.DrawText(g, "Stackshot", F(15, 2), new Rectangle(P(58), P(22), P(170), P(20)), Mac.Text, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "Versi\u00F3n " + Installer.MyVersion.ToString(3), F(11.5f, 0), new Rectangle(P(58), P(42), P(170), P(16)), Mac.Text3, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            foreach (Widget w in nav) w.Paint(g, this);
        }

        // Windows 11 caption buttons: minimize and close (to tray). Fixed size, no maximize.
        Rectangle CaptionButton(int i) { return new Rectangle(Real.Width - P(46) * (2 - i), 0, P(46), P(32)); }
        Rectangle CaptionRect { get { return Rectangle.Union(CaptionButton(0), CaptionButton(1)); } }

        int CaptionAt(Point p)
        {
            for (int i = 0; i < 2; i++) if (CaptionButton(i).Contains(p)) return i;
            return -1;
        }

        void PaintCaption(Graphics g)
        {
            string[] glyphs = { "\uE921", "\uE8BB" };
            for (int i = 0; i < 2; i++)
            {
                Rectangle r = CaptionButton(i);
                bool h = captionHot == i, d = h && captionDown == i;
                Color fg = active ? Mac.Text : Mac.Text3;
                if (i == 1 && h)
                {
                    using (SolidBrush b = new SolidBrush(d ? Color.FromArgb(200, 196, 43, 28) : Color.FromArgb(196, 43, 28))) g.FillRectangle(b, r);
                    fg = Color.White;
                }
                else if (h)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(d ? 10 : 18, 255, 255, 255))) g.FillRectangle(b, r);
                }
                Font f = F(10, 3);
                TextRenderer.DrawText(g, glyphs[i], f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        // The bubble sits above the mascot, never over the text next to it: above the head on Home and Mascota,
        // above its card in the sidebar.
        bool MiniMode { get { return page != "home" && page != "mascot"; } }

        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        Rectangle lastBubble;          // area painted last time, so a shorter text never leaves leftovers behind
        string measuredText;
        int measuredW;
        SizeF measuredSize;
        int bubbleTail = 1;            // 1 points down at the mascot, -1 up, 0 none

        Rectangle BubbleRect(bool inflate)
        {
            string text = mascot.Bubble ?? bubbleText;
            if (text == null) return Rectangle.Empty;
            RectangleF b = mascot.Box;
            int maxW = MiniMode ? P(Side - 24) : P(214);
            // Measured once per text and width, not on every tick.
            if (text != measuredText || maxW != measuredW)
            {
                using (Graphics mg = Graphics.FromImage(measureBmp))
                {
                    mg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    measuredSize = mg.MeasureString(text, F(13, 0), maxW - P(28));
                }
                measuredText = text;
                measuredW = maxW;
            }
            SizeF ts = measuredSize;
            int w = (int)Math.Ceiling(ts.Width) + P(30), h = (int)Math.Ceiling(ts.Height) + P(20);
            Rectangle r;
            bubbleTail = 1;
            if (MiniMode) r = new Rectangle(P(12), nav[nav.Count - 1].R.Y - h - P(12), w, h);
            else
            {
                int left = (int)(b.X + b.Width / 2 - w / 2f), minX = P(Side) + P(14), maxX = P(Side) + P(40) + P(252) - w;
                // Above the head, leaving room for a hat if it wears one.
                int x = Math.Max(minX, Math.Min(maxX, left)), above = (int)(b.Y - h - P(10) - (mascot.Look.EffectiveHat(DateTime.Now) != 0 ? b.Height * 0.16f : 0)), below = (int)(b.Bottom - b.Height * 0.08f + P(10));
                // Never above the card the mascot stands in by more than a little: the page title and subtitle stay readable,
                // even if that means brushing the top of a hat.
                int ceiling = CardTop(b) - P(30), plain = (int)(b.Y - h - P(10));
                if (above < ceiling && ceiling <= plain) above = ceiling;
                // Above the head when it fits; below the mascot (tail up) when its head is scrolled out of view; and when the
                // mascot is out of view altogether, a plain note at the top of the page.
                if (above >= P(8)) { r = new Rectangle(x, above, w, h); bubbleTail = 1; }
                else if (b.Y + b.Height * 0.5f > P(8) && below + h < Real.Height - P(8)) { r = new Rectangle(x, below, w, h); bubbleTail = -1; }
                else { r = new Rectangle(P(Side) + (Real.Width - P(Side) - w) / 2, P(14), w, h); bubbleTail = 0; }
            }
            if (inflate) r.Inflate(P(14), P(14));
            return r;
        }

        // Top of the hero or stage card holding the mascot, in window coordinates (or 0 when there is none).
        int CardTop(RectangleF box)
        {
            PointF c = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
            int dy = -(int)Math.Round(scroll) + (int)Math.Round(ContentShift);
            foreach (Widget w in items)
            {
                if (!(w is Hero) && !(w is MascotStage)) continue;
                Rectangle r = new Rectangle(w.R.X + P(Side), w.R.Y + dy, w.R.Width, w.R.Height);
                if (r.Contains(Point.Round(c))) return r.Y;
            }
            return 0;
        }

        void PaintBubble(Graphics g)
        {
            if (mascot.Bubble != null) bubbleText = mascot.Bubble;
            if (bubbleText == null) return;
            double a = mascot.BubbleAlpha;
            if (a < 0.02) { if (mascot.Bubble == null) bubbleText = null; return; }
            Rectangle r = BubbleRect(false);
            lastBubble = Rectangle.Inflate(r, P(16), P(16));
            float pop = (float)(0.92 + 0.08 * a);
            GraphicsState st = g.Save();
            float tx = Math.Max(r.X + P(18), Math.Min(r.Right - P(18), mascot.Box.X + mascot.Box.Width / 2));
            float ox = tx, oy = bubbleTail < 0 ? r.Top : r.Bottom;
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(pop, pop);
            g.TranslateTransform(-ox, -oy);
            Color fill = Color.FromArgb((int)(250 * a), 62, 62, 68);
            using (GraphicsPath p = Theme.Round(r, P(14)))
            {
                RectangleF sh = r;
                sh.Offset(0, P(3));
                using (GraphicsPath sp = Theme.Round(sh, P(14)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * a), 0, 0, 0))) g.FillPath(b, sp);
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (bubbleTail != 0)
                {
                    float edge = bubbleTail > 0 ? r.Bottom - P(1) : r.Top + P(1), tip = bubbleTail > 0 ? r.Bottom + P(7) : r.Top - P(7);
                    PointF[] tail = { new PointF(tx - P(7), edge), new PointF(tx + P(1), tip), new PointF(tx + P(7), edge) };
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPolygon(b, tail);
                }
                using (Pen pen = new Pen(Color.FromArgb((int)(40 * a), 255, 255, 255))) g.DrawPath(pen, p);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(255 * a), Mac.Text)))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                RectangleF tr = new RectangleF(r.X + P(15), r.Y + P(10), r.Width - P(28), r.Height - P(16));
                g.DrawString(bubbleText, F(13, 0), tb, tr);
            }
            g.Restore(st);
        }

        Widget HitTest(Point p)
        {
            foreach (Widget w in nav) if (w.Interactive && w.R.Contains(p)) return w;
            if (p.X >= P(Side))
            {
                Point c = new Point(p.X - P(Side), p.Y + (int)Math.Round(scroll));
                for (int i = items.Count - 1; i >= 0; i--) if (items[i].Interactive && items[i].Hit(c)) return items[i];
            }
            return null;
        }

        Point ToContent(Point p) { return new Point(p.X - P(Side), p.Y + (int)Math.Round(scroll)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            if (intro != null) return;
            int ch = CaptionAt(e.Location);
            if (ch != captionHot) { captionHot = ch; Invalidate(CaptionRect); }
            bool lh = ch >= 0;
            bool mh = settings.MascotOn && mascot.Box.Contains(e.Location);
            if (mh != mascotHot) { mascotHot = mh; mascot.Hover(mh); }
            Widget h = HitTest(e.Location);
            if (h != hot)
            {
                TryOn(null);
                if (hot != null) hot.SetHot(false);
                hot = h;
                if (hot != null) hot.SetHot(true);
            }
            if (hot != null) hot.Move(this, IsNav(hot) ? e.Location : ToContent(e.Location));
            Cursor = ((hot != null && hot.Clickable) || mh) && !lh ? Cursors.Hand : Cursors.Default;
        }

        bool IsNav(Widget w) { return nav.Contains(w); }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            TryOn(null);
            if (hot != null) { hot.SetHot(false); hot = null; }
            if (captionHot >= 0) { captionHot = -1; captionDown = -1; Invalidate(CaptionRect); }
            if (mascotHot) { mascotHot = false; mascot.Hover(false); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (intro != null) { SkipIntro(); return; }
            if (e.Button != MouseButtons.Left) return;
            if (CaptionAt(e.Location) >= 0) { captionDown = CaptionAt(e.Location); Invalidate(CaptionRect); return; }
            if (settings.MascotOn && mascot.Box.Contains(e.Location))
            {
                if (page != "home" && page != "mascot") { SetPage("home", true); return; }
                mascot.Poke(PokeLines());
                return;
            }
            pressed = HitTest(e.Location);
            if (pressed != null) pressed.Down(this, IsNav(pressed) ? e.Location : ToContent(e.Location));
            if (listening != null && pressed != listening) CancelListening();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (captionDown >= 0)
            {
                int i = captionDown;
                captionDown = -1;
                Invalidate(CaptionRect);
                if (CaptionAt(e.Location) != i) return;
                if (i == 1) HideToTray();
                else WindowState = FormWindowState.Minimized;
                return;
            }
            Widget p = pressed;
            pressed = null;
            if (p == null) return;
            p.Up(this);
            if (HitTest(e.Location) == p) p.Click(this, IsNav(p) ? e.Location : ToContent(e.Location));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.X < P(Side)) return;
            scrollTarget -= e.Delta / 120.0 * P(72);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (intro != null && (keyData & Keys.KeyCode) != Keys.None) { SkipIntro(); return true; }
            if (listening != null)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return true;
                if (key == Keys.PrintScreen) return true; // arrives on key up (OnKeyUp)
                listening.Take(this, keyData);
                return true;
            }
            if (nameBox != null && nameBox.Focused) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Control | Keys.W:
                case Keys.Escape:
                    HideToTray();
                    return true;
                case Keys.Control | Keys.Tab:
                case Keys.Control | Keys.Shift | Keys.Tab:
                {
                    int i = Array.IndexOf(PageIds, page) + ((keyData & Keys.Shift) != 0 ? -1 : 1);
                    SetPage(PageIds[(i + PageIds.Length) % PageIds.Length], true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (listening != null && e.KeyCode == Keys.PrintScreen) listening.Take(this, e.KeyData);
        }

        void SkipIntro()
        {
            if (intro == null) return;
            intro.Dispose();
            intro = null;
            sideDirty = true;
            if (settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(Greeting() + " Soy " + settings.MascotName + ".");
            }
            Invalidate();
        }

        void CancelListening()
        {
            if (listening == null) return;
            listening.Stop(this);
        }

        static string Greeting() { return MascotTalk.Greeting(); }
        string[] PokeLines() { return MascotTalk.Pokes(settings); }
        string Tip() { return MascotTalk.Tip(settings); }

        void OnCaptured(string path)
        {
            if (!Visible || !settings.MascotOn) return;
            int level = MascotParts.Level(settings.MascotLove);
            bool up = level > MascotParts.Level(settings.MascotLove - 1);
            mascot.Celebrate(settings.MascotTalks ? (up ? MascotTalk.LevelUp(settings, level) : MascotTalk.Celebrate(settings)) : null);
            InvalidatePreviews();
        }

        void OnUpdaterChanged()
        {
            if (page == "about") Rebuild();
            else { sideDirty = true; contentDirty = true; Invalidate(); }
        }

        // ---- Mascot look: applying changes and cached picker previews.

        readonly Dictionary<string, Bitmap> previews = new Dictionary<string, Bitmap>();
        readonly Dictionary<string, Bitmap> shownInSlot = new Dictionary<string, Bitmap>();
        readonly HashSet<string> rendering = new HashSet<string>();
        MascotLook tryOn;

        // Called after any look change: the live mascot, the desktop pet and every preview follow.
        void LookChanged(MascotLook l, string reaction)
        {
            l.ApplyTo(settings);
            tryOn = null;
            mascot.Look = MascotLook.From(settings);
            InvalidatePreviews();
            owner.MascotChanged();
            if (reaction != null && settings.MascotTalks) mascot.Celebrate(reaction);
            else mascot.Celebrate(null);
            Changed();
        }

        void InvalidatePreviews()
        {
            contentDirty = true;
        }

        // Shows a look on the big mascot without saving it (hovering a picker); null goes back to the saved one.
        void TryOn(MascotLook l)
        {
            string key = l == null ? null : l.Key, cur = tryOn == null ? null : tryOn.Key;
            if (key == cur) return;
            tryOn = l;
            RectangleF before = mascot.PaintBounds;
            mascot.Look = l ?? MascotLook.From(settings);
            if (l != null && settings.MascotOn) mascot.Hop(0.45);
            Invalidate(Rectangle.Round(RectangleF.Union(before, mascot.PaintBounds)));
        }

        // A still render of the mascot wearing one variation. Missing ones are rendered on a worker thread (so
        // changing the look never freezes the window); meanwhile the slot keeps showing what it showed before.
        Bitmap Preview(MascotLook l, int px, string slot, Rectangle area)
        {
            string key = l.Key + "@" + px;
            Bitmap b;
            if (previews.TryGetValue(key, out b)) { shownInSlot[slot] = b; return b; }
            if (rendering.Add(key))
            {
                MascotLook look = l.Clone();
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Bitmap bmp = new Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    try
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            float d = px * 0.62f;
                            Mascot.RenderStill(g, new RectangleF((px - d) / 2, px * 0.22f, d, d), look);
                        }
                    }
                    catch (Exception ex) { ShotStack.Log("Vista previa: " + ex.Message); }
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            rendering.Remove(key);
                            if (IsDisposed) { bmp.Dispose(); return; }
                            TrimPreviews();
                            previews[key] = bmp;
                            DirtyContent(area);
                        });
                    }
                    catch { bmp.Dispose(); }
                });
            }
            return shownInSlot.TryGetValue(slot, out b) ? b : null;
        }

        // Keeps the cache bounded; bitmaps still on screen stay.
        void TrimPreviews()
        {
            if (previews.Count < 600) return;
            HashSet<Bitmap> keep = new HashSet<Bitmap>(shownInSlot.Values);
            foreach (KeyValuePair<string, Bitmap> kv in new List<KeyValuePair<string, Bitmap>>(previews))
            {
                if (keep.Contains(kv.Value)) continue;
                kv.Value.Dispose();
                previews.Remove(kv.Key);
            }
        }

        Font F(float px, int weight)
        {
            int key = (int)Math.Round(px * 100) * 4 + weight;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            string fam = weight == 3 ? Theme.IconFont : weight == 2 ? Fonts.DisplaySemibold : weight == 1 ? "Segoe UI Semibold" : Mac.TextFont;
            f = new Font(fam, Math.Max(1f, px * s), GraphicsUnit.Pixel);
            fonts[key] = f;
            return f;
        }

        static void Fill(Graphics g, Rectangle r, float radius, Color c)
        {
            using (GraphicsPath p = Theme.Round(r, radius))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        static void Txt(Graphics g, string t, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, t, f, r, c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | extra);
        }

        // Draws a combo as keycaps. Returns the width used.
        int Keycaps(Graphics g, string combo, int right, int cy, bool big, Color fg, Color bg)
        {
            string[] keys = combo.Split(new string[] { " + " }, StringSplitOptions.None);
            Font f = F(big ? 14 : 12, 1);
            int h = P(big ? 30 : 24), pad = P(big ? 10 : 8), gap = P(5);
            int total = 0;
            int[] ws = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ws[i] = Math.Max(h, TextRenderer.MeasureText(keys[i], f).Width + pad * 2 - P(6));
                total += ws[i] + (i > 0 ? gap : 0);
            }
            int x = right - total;
            for (int i = 0; i < keys.Length; i++)
            {
                Rectangle r = new Rectangle(x, cy - h / 2, ws[i], h);
                Rectangle sh = r;
                sh.Offset(0, P(2));
                Fill(g, sh, P(6), Color.FromArgb(20, 20, 22));
                Fill(g, r, P(6), bg);
                using (GraphicsPath p = Theme.Round(r, P(6)))
                using (Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawPath(pen, p);
                Txt(g, keys[i], f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                x += ws[i] + gap;
            }
            return total;
        }

        // Colored tile with a white icon, like iOS Settings.
        void IconTile(Graphics g, Rectangle r, string icon, Color a, Color b)
        {
            using (GraphicsPath p = Theme.Round(r, r.Width * 0.26f))
            using (LinearGradientBrush lb = new LinearGradientBrush(Rectangle.Inflate(r, 1, 1), a, b, 90f)) g.FillPath(lb, p);
            int m = (int)(r.Width * 0.2f);
            Icons.Draw(g, icon, Rectangle.Inflate(r, -m, -m), Color.White);
        }
    }
}
