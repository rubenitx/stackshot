// Stackshot - The mascot living on the desktop, just above the taskbar.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    // A per-pixel-alpha layered window (UpdateLayeredWindow): only the mascot is visible and clickable, the rest lets
    // clicks through. It wanders along the bottom of the screen, looks at the mouse, naps when nobody is around, can
    // be dragged and dropped (it falls back down) and hides during full-screen apps. A click opens a comic-style
    // speech bubble with things it can do. Excluded from captures and screen sharing. Frame rate adapts: ~30 fps while
    // moving or with the bubble open, ~20 idle, ~8 asleep.
    public class PetWindow : Form
    {
        enum State { Idle, Walk, Drag, Fall }

        class Choice
        {
            public string Icon, Text;
            public Action Do;
            public RectangleF R;
        }

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public int cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        static readonly Color ComicInk = Color.FromArgb(24, 22, 34);

        readonly ShotStack owner;
        readonly Settings settings;
        readonly Mascot m = new Mascot();
        readonly Timer timer = new Timer();
        readonly Random rnd = new Random();
        Dib dib;
        float s = 1f;
        int W, H, D;
        Rectangle wa;
        double bx, by, vy;            // mascot box top-left in screen coordinates; vertical speed while falling
        double targetX, nextIdea, lastHop, lastCheck, lastFrame, lastIdleLine;
        int dir = 1, wx, wy;
        State state = State.Idle;
        Point grab, downAt;
        bool down, homeShown, blocked, asleepForAway;
        Point lastMouse;
        string bubble;
        // Comic menu
        List<Choice> menu;
        string menuTitle;
        double menuAt, menuSeen;
        int menuHot = -1;
        RectangleF menuRect;

        public PetWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            m.Look = MascotLook.From(settings);
            Screen scr = Screen.FromPoint(Control.MousePosition);
            Place(scr, scr.WorkingArea.Left + scr.WorkingArea.Width * settings.MascotSpot / 1000.0); // where it was last time
            by = Ground;
            timer.Interval = 33;
            timer.Tick += delegate
            {
                try { Tick(); }
                catch (Exception ex)
                {
                    // A drawing bug must not spam the log or eat CPU every frame: stop and leave a trace.
                    timer.Stop();
                    ShotStack.Log("Mascota en el escritorio: " + ex);
                }
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000080 | 0x00000008 | 0x08000000; // WS_EX_LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message msg)
        {
            if (msg.Msg == 0x0021) { msg.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref msg);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ShotStack.Test) Native.SetWindowDisplayAffinity(Handle, 0x11); // WDA_EXCLUDEFROMCAPTURE
        }

        // Sizes follow the DPI of the monitor it is on. The window leaves room above the mascot for the menu.
        void Place(Screen scr, double centerX)
        {
            wa = scr.WorkingArea;
            float ns = ShotStack.ScaleFor(scr);
            if (dib == null || ns != s)
            {
                s = ns;
                D = (int)Math.Round(84 * s);
                W = (int)Math.Round(340 * s);
                H = (int)Math.Round(D * 1.02 + 360 * s);
                if (dib != null) dib.Dispose();
                dib = new Dib(W, H, true);
            }
            bx = Math.Max(wa.Left, Math.Min(wa.Right - D, centerX - D / 2.0));
            targetX = bx;
        }

        double Ground { get { return (perch != IntPtr.Zero ? perchRect.Top : wa.Bottom) - D * 0.92; } }

        // ---- Standing on windows: it hops onto the foreground window, walks along its top edge and rides along when the
        // window moves. If the window goes away (closed, minimized, maximized), it drops back to the taskbar.
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
        static readonly uint MyPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr perch;
        Native.RECT perchRect;
        double vx;

        // A window it can stand on: visible, not ours, not maximized or cloaked, wide enough and with room above it.
        bool PerchFor(IntPtr h, out Native.RECT r)
        {
            r = new Native.RECT();
            if (h == IntPtr.Zero || h == Handle || !IsWindowVisible(h) || Native.IsIconic(h) || IsZoomed(h)) return false;
            uint pid;
            Native.GetWindowPid(h, out pid);
            if (pid == MyPid) return false;
            int cloaked;
            if (Native.DwmGetInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false; // DWMWA_CLOAKED
            if (Native.DwmGetRect(h, 9, out r, 16) != 0) return false;                         // DWMWA_EXTENDED_FRAME_BOUNDS
            return r.Right - r.Left > D * 2.5 && r.Top - D > wa.Top && r.Top < wa.Bottom - D * 1.5 && r.Left < wa.Right - D && r.Right > wa.Left + D;
        }

        // Every frame: ride along with the window, or fall if it is gone.
        void FollowPerch()
        {
            if (perch == IntPtr.Zero || state == State.Drag) return;
            Native.RECT r;
            // The window went to another monitor: move to that monitor too.
            Native.RECT at;
            if (Native.DwmGetRect(perch, 9, out at, 16) == 0)
            {
                Screen sc = Screen.FromRectangle(Rectangle.FromLTRB(at.Left, at.Top, at.Right, at.Bottom));
                if (sc.WorkingArea != wa) Place(sc, bx + D / 2.0);
            }
            if (!PerchFor(perch, out r))
            {
                perch = IntPtr.Zero;
                Place(Screen.FromPoint(new Point((int)(bx + D / 2), (int)by)), bx + D / 2.0);
                if (state != State.Fall) { state = State.Fall; vy = 0; vx = 0; m.Say(settings.MascotTalks ? "\u00A1Aaaah!" : null, 1200); }
                return;
            }
            int dx = r.Left - perchRect.Left, dy = r.Top - perchRect.Top;
            perchRect = r;
            if ((dx != 0 || dy != 0) && state != State.Fall)
            {
                bx += dx; by += dy; targetX += dx;
                bx = Math.Max(r.Left, Math.Min(r.Right - D, bx));
            }
        }

        // Jumps in an arc to a spot on top of the window.
        void ClimbOnto(IntPtr h, Native.RECT r)
        {
            perch = h;
            perchRect = r;
            double g = 2600 * s, rise = Math.Max(0, by - Ground) + 40 * s;
            vy = -Math.Sqrt(2 * g * rise);
            double flight = -vy / g + Math.Sqrt(2 * 40 * s / g);
            double tx = Math.Max(r.Left + D * 0.1, Math.Min(r.Right - D * 1.1, bx + (rnd.NextDouble() - 0.5) * D * 3));
            vx = (tx - bx) / flight;
            state = State.Fall;
            m.WakeUp();
            m.Hop(1.4);
        }

        // ---- Going home: waves goodbye, gets off any window, walks out through the nearest edge and switches itself off.
        int clicks;
        double lastClick, menuDue;
        bool leaving;

        void GoHome()
        {
            CloseMenu();
            leaving = true;
            m.WakeUp();
            if (state == State.Walk) state = State.Idle; // the current stroll ends here; the walk out starts after the wave
            m.Greet(settings.MascotTalks ? "\u00A1Me voy a casa! \u00A1Hasta luego!" : null);
            nextIdea = Anim.Now + 1300; // wave first
            if (perch != IntPtr.Zero) HopDown();
        }

        void WalkOff()
        {
            if (Anim.Now < nextIdea) return;
            bool left = bx + D / 2.0 < wa.Left + wa.Width / 2.0;
            targetX = left ? wa.Left - D * 1.4 : wa.Right + D * 0.4;
            dir = left ? -1 : 1;
            state = State.Walk;
        }

        void Gone()
        {
            leaving = false;
            settings.MascotDesktop = false;
            owner.MascotChanged();
            owner.ApplySettings();
        }

        // Remembers where it stands (per monitor width), so it comes back there after a restart or a new session. Walks
        // only save once a minute; drops save right away.
        double lastSpotSave;
        void RememberSpot(bool now)
        {
            if (perch != IntPtr.Zero || wa.Width <= 0) return;
            settings.MascotSpot = Math.Max(0, Math.Min(1000, (int)Math.Round((bx + D / 2.0 - wa.Left) * 1000.0 / wa.Width)));
            if (!now && Anim.Now - lastSpotSave < 60000) return;
            lastSpotSave = Anim.Now;
            settings.Save();
        }

        // Let go: settle on the monitor under the mouse, on a window if dropped over one, and remember the spot.
        void EndDrag()
        {
            down = false;
            Screen scr = Screen.FromPoint(Control.MousePosition);
            Place(scr, bx + D / 2.0);
            RememberSpot(true);
            // Dropped over a window: it lands on that window instead of the taskbar.
            Native.POINT pt;
            pt.X = (int)(bx + D / 2);
            pt.Y = (int)(by + D + 6 * s);
            IntPtr under = GetAncestor(WindowFromPoint(pt), 2); // GA_ROOT
            Native.RECT pr;
            if (settings.MascotClimb && PerchFor(under, out pr) && pr.Top > by + D * 0.5) { perch = under; perchRect = pr; }
            state = State.Fall;
            vy = 0;
        }

        void HopDown()
        {
            perch = IntPtr.Zero;
            vy = -520 * s;
            vx = 0;
            state = State.Fall;
            m.Hop(1.0);
        }

        public void ShowPet()
        {
            m.PopIn();
            nextIdea = Anim.Now + 3000;
            Show();
            timer.Start();
            if (settings.MascotTalks) m.Say(MascotTalk.Hello(settings) + " Haz clic en m\u00ED.", 4200);
        }

        public void LookChanged()
        {
            m.Look = MascotLook.From(settings);
            m.Celebrate(null);
        }

        public void Celebrate(string text)
        {
            if (state == State.Drag) return;
            CloseMenu();
            m.Celebrate(settings.MascotTalks ? text ?? MascotTalk.Celebrate(settings) : null);
        }

        // The desktop mascot stays out even with the main window open: it is switched on from there, and hiding it
        // then made it look broken.
        public void SetHomeShown(bool shown)
        {
            homeShown = shown;
        }

        void UpdateVisibility()
        {
            bool want = !blocked;
            if (want && !Visible) { Show(); m.PopIn(); }
            else if (!want && Visible) { CloseMenu(); Hide(); }
            if (want && !timer.Enabled) timer.Start();
        }

        static int IdleMs()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            return unchecked(Environment.TickCount - (int)li.dwTime);
        }

        // Full-screen apps, games and presentations: hide until they end.
        bool FullScreenBusy()
        {
            int q;
            // Games (QUNS_RUNNING_D3D_FULL_SCREEN, 3) and presentations (QUNS_PRESENTATION_MODE, 4) always hide it. QUNS_BUSY (2)
            // means some app is full screen on any monitor: only hide when it covers the monitor the mascot is on.
            try
            {
                if (SHQueryUserNotificationState(out q) != 0) return false;
                if (q == 3 || q == 4) return true;
                if (q != 2) return false;
                IntPtr fg = Native.GetForegroundWindow();
                Native.RECT r;
                if (fg == IntPtr.Zero || !Native.GetWindowRect(fg, out r)) return false;
                Rectangle mon = Screen.FromRectangle(wa).Bounds;
                return r.Left <= mon.Left && r.Top <= mon.Top && r.Right >= mon.Right && r.Bottom >= mon.Bottom;
            }
            catch { }
            return false;
        }

        void Tick()
        {
            double now = Anim.Now;
            double dt = lastFrame == 0 ? 0.033 : Math.Min(0.1, (now - lastFrame) / 1000.0);
            lastFrame = now;

            if (now - lastCheck > 1500)
            {
                lastCheck = now;
                bool b = FullScreenBusy();
                if (b != blocked) { blocked = b; UpdateVisibility(); }
                // Asleep while the user is away; awake again on return.
                int idle = IdleMs();
                if (idle > 60000 && !asleepForAway && state != State.Drag) { asleepForAway = true; CloseMenu(); m.SleepNow(); state = State.Idle; }
                else if (idle < 1500 && asleepForAway) { asleepForAway = false; m.WakeUp(); m.Hop(1.2); }
            }
            if (!Visible) { timer.Interval = 500; return; }

            Point mouse = Control.MousePosition;
            bool near = Math.Abs(mouse.X - (bx + D / 2.0)) < 360 * s && Math.Abs(mouse.Y - (by + D / 2.0)) < 300 * s;
            bool moved = near && mouse != lastMouse;
            lastMouse = mouse;

            if (menu != null)
            {
                // Close the bubble when the user clicks somewhere else or ignores it for a while.
                bool inside = menuRect.Contains(mouse.X - wx, mouse.Y - wy) || new RectangleF((float)bx, (float)by, D, D).Contains(mouse);
                if (inside) menuSeen = now;
                if ((Control.MouseButtons != MouseButtons.None && !inside) || now - menuSeen > 9000) CloseMenu();
            }

            FollowPerch();
            if (menuDue > 0 && now >= menuDue) { menuDue = 0; clicks = 0; OpenMenu(); }
            if (clicks > 0 && now - lastClick > 400 && menuDue == 0) clicks = 0;
            switch (state)
            {
                case State.Drag:
                    // The button went up without a mouse-up reaching us (Alt+Tab, a system dialog): treat it as a drop.
                    if ((Control.MouseButtons & MouseButtons.Left) == 0) { EndDrag(); break; }
                    bx = mouse.X - grab.X;
                    by = mouse.Y - grab.Y;
                    break;
                case State.Fall:
                    vy += 2600 * s * dt;
                    by += vy * dt;
                    bx += vx * dt;
                    if (vy >= 0 && by >= Ground)
                    {
                        by = Ground;
                        vx = 0;
                        if (perch != IntPtr.Zero) bx = Math.Max(perchRect.Left, Math.Min(perchRect.Right - D, bx));
                        m.Land(Math.Min(1, vy / (900 * s)));
                        vy = 0;
                        state = State.Idle;
                        nextIdea = now + 2500;
                    }
                    break;
                case State.Walk:
                {
                    double step = (leaving ? 160 : 75) * s * dt;
                    if (Math.Abs(targetX - bx) <= step)
                    {
                        bx = targetX;
                        state = State.Idle;
                        nextIdea = now + 4000 + rnd.NextDouble() * 6000;
                        if (leaving) { BeginInvoke((Action)Gone); return; }
                        RememberSpot(false);
                    }
                    else bx += Math.Sign(targetX - bx) * step;
                    if (now - lastHop > 420) { lastHop = now; m.Hop(0.7); }
                    break;
                }
                default:
                    if (leaving) { WalkOff(); break; }
                    if (menu == null && !m.Sleeping && now > nextIdea) Idea(now);
                    if (m.Sleeping && !asleepForAway && now > nextIdea + 120000) { m.WakeUp(); Idea(now); }
                    break;
            }
            m.Facing = state == State.Walk ? dir * 0.8 : 0;
            m.Dangling = state == State.Drag;
            m.ShowShadow = state != State.Drag && state != State.Fall;

            PlaceWindow();
            m.Step(now, new PointF(mouse.X - wx, mouse.Y - wy), moved);
            Render(now);

            // The system timer ticks every 15.6 ms: 16 = ~64 fps, 31 = ~32 fps, 63 = ~16 fps (a gentle float), 250 asleep.
            int interval = state == State.Drag || state == State.Fall ? 16 : state != State.Idle || menu != null || m.Lively ? 31 : m.Sleeping ? 250 : 63;
            if (asleepForAway && state == State.Idle) interval = 500; // nobody is looking (away or locked)
            if (timer.Interval != interval) timer.Interval = interval;
        }

        // What to do next while idle: wander somewhere, wave, or just float.
        void Idea(double now)
        {
            double r = rnd.NextDouble();
            nextIdea = now + 5000 + rnd.NextDouble() * 9000;
            if (settings.MascotClimb && rnd.NextDouble() < 0.22)
            {
                Native.RECT pr;
                if (perch != IntPtr.Zero) { HopDown(); return; }
                IntPtr fg = Native.GetForegroundWindow();
                if (PerchFor(fg, out pr)) { ClimbOnto(fg, pr); return; }
            }
            if (r < 0.45)
            {
                double range = Math.Min(wa.Width - D, 520 * s);
                double lo = perch != IntPtr.Zero ? perchRect.Left : wa.Left, hi = perch != IntPtr.Zero ? perchRect.Right - D : wa.Right - D;
                targetX = Math.Max(lo, Math.Min(hi, bx + (rnd.NextDouble() * 2 - 1) * range));
                if (Math.Abs(targetX - bx) > 30 * s) { dir = Math.Sign(targetX - bx); state = State.Walk; m.WakeUp(); }
            }
            else if (r < 0.55 && settings.MascotTalks && now - lastIdleLine > 90000)
            {
                lastIdleLine = now;
                m.Greet(MascotTalk.Idle(settings));
            }
        }

        void Render(double now)
        {
            using (Graphics g = dib.Graphics())
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                // Only the mascot area changes unless a bubble or the menu is (or was) up.
                RectangleF pb = m.PaintBounds;
                bool full = menu != null || bubble != null || m.Bubble != null || lastFull;
                lastFull = menu != null || bubble != null || m.Bubble != null;
                if (!full && !lastPaint.IsEmpty)
                {
                    RectangleF clip = RectangleF.Union(pb, lastPaint);
                    clip.Inflate(2, 2);
                    g.SetClip(clip);
                }
                lastPaint = pb;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                m.Paint(g);
                if (menu != null) PaintMenu(g, now);
                else PaintBubble(g);
            }
            Native.GdiFlush();
            // Without a bubble or the menu, only the mascot's own box goes to the screen (a fraction of the window).
            Rectangle crop = new Rectangle(0, 0, W, H);
            if (!lastFull && menu == null && bubble == null && m.Bubble == null)
            {
                RectangleF pb = m.PaintBounds;
                pb.Inflate(4, 4);
                crop = Rectangle.Intersect(Rectangle.Round(pb), crop);
                if (crop.Width < 1 || crop.Height < 1) crop = new Rectangle(0, 0, W, H);
            }
            presentAt = crop.Location;
            Native.Present(Handle, dib.Dc, wx + crop.X, wy + crop.Y, crop.Width, crop.Height, crop.X, crop.Y);
        }

        RectangleF lastPaint;
        Point presentAt;             // top-left of the part of the surface currently shown
        string measuredFor;
        SizeF measured;
        bool lastFull = true;

        // The window stays inside the monitor; near an edge the mascot moves off-center inside it, so the bubble and
        // the menu always fit on screen.
        void PlaceWindow()
        {
            float boxY = H - D * 1.02f;
            int x = (int)Math.Round(bx - (W - D) / 2.0);
            if (W <= wa.Width && state != State.Drag) x = Math.Max(wa.Left, Math.Min(wa.Right - W, x)); // free while dragged across monitors
            wx = x;
            wy = (int)Math.Round(by - boxY);
            m.Box = new RectangleF((float)(bx - wx), boxY, D, D);
        }

        float MidX { get { return m.Box.X + D / 2f; } }

        // While dragging, the window follows every mouse move right away (the timer only animates the mascot).
        void FollowMouse()
        {
            Point mouse = Control.MousePosition;
            bx = mouse.X - grab.X;
            by = mouse.Y - grab.Y;
            PlaceWindow();
            Native.SetWindowPos(Handle, IntPtr.Zero, wx + presentAt.X, wy + presentAt.Y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0200); // NOSIZE | NOZORDER | NOACTIVATE | NOOWNERZORDER
        }

        void PaintBubble(Graphics g)
        {
            if (m.Bubble != null) bubble = m.Bubble;
            if (bubble == null) return;
            double a = m.BubbleAlpha;
            if (a < 0.02) { if (m.Bubble == null) bubble = null; return; }
            Font f = Fonts.Get(Mac.TextFont, 13 * s);
            // Measured once per text, not every frame.
            if (bubble != measuredFor) { measured = g.MeasureString(bubble, f, (int)(W - 60 * s)); measuredFor = bubble; }
            SizeF ts = measured;
            float w = ts.Width + 26 * s, h = ts.Height + 16 * s;
            float x = ClampX(MidX - w / 2, w), y = m.Box.Y - D * 0.38f - h;
            RectangleF r = new RectangleF(x, y, w, h);
            using (GraphicsPath p = Theme.Round(r, 12 * s))
            {
                RectangleF sh = r;
                sh.Offset(0, 3 * s);
                using (GraphicsPath sp = Theme.Round(sh, 12 * s)) MascotParts.FillSolid(g, sp, Color.FromArgb((int)(60 * a), 0, 0, 0));
                MascotParts.FillSolid(g, p, Color.FromArgb((int)(245 * a), 52, 52, 60));
                PointF[] tail = { new PointF(MidX - 7 * s, r.Bottom - 1), new PointF(MidX, r.Bottom + 7 * s), new PointF(MidX + 7 * s, r.Bottom - 1) };
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(245 * a), 52, 52, 60))) g.FillPolygon(b, tail);
                MascotParts.Stroke(g, p, Color.FromArgb((int)(40 * a), 255, 255, 255), 1);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(255 * a), Mac.Text)))
                g.DrawString(bubble, f, tb, new RectangleF(x + 13 * s, y + 8 * s, w - 24 * s, h - 12 * s));
        }

        // Keeps a bubble of width w inside the monitor, even when the mascot stands at an edge.
        float ClampX(float x, float w)
        {
            float minX = wa.Left - wx + 6 * s, maxX = wa.Right - wx - w - 6 * s;
            return Math.Max(Math.Max(4 * s, minX), Math.Min(Math.Min(W - w - 4 * s, maxX), x));
        }

        // ---- Comic menu: a white speech bubble with a bold outline, a hand-written question and the choices.

        void OpenMenu()
        {
            m.WakeUp();
            m.Bubble = null;
            bubble = null;
            m.Hop(0.9);
            menu = new List<Choice>();
            Add("area", "Capturar un \u00E1rea", delegate { owner.Run("region"); });
            Add("video", Recorder.Recording ? "Parar la grabaci\u00F3n" : "Grabar un v\u00EDdeo", delegate { owner.Run("video"); });
            Add("folder", "Abrir mis capturas", delegate { owner.OpenFolder(); });
            Add("brush", "Cambiarme de ropa", delegate { owner.ShowHome("mascot", false); });
            Add("sparkle", "\u00A1Haz un truco!", delegate
            {
                int trick = rnd.Next(3);
                if (trick == 0) m.Dance();
                else if (trick == 1) m.Twirl();
                else m.Poke(settings.MascotTalks ? MascotTalk.Pokes(settings) : null);
            });
            Add("bot", m.Sleeping ? "Despierta" : "Echa una siesta", delegate { if (m.Sleeping) { m.WakeUp(); m.Hop(1.2); } else { m.SleepNow(); state = State.Idle; } });
            Add("logo", "Abrir Stackshot", delegate { owner.ShowHome("home", false); });
            Add("home", "Vete a casa", GoHome);
            switch (settings.MascotPersonality)
            {
                case 1: menuTitle = "\u00BFEn qu\u00E9 te ayudo?"; break;
                case 2: menuTitle = "\u00BFQu\u00E9 liamos, jefe?"; break;
                default: menuTitle = "\u00A1Hola! \u00BFQu\u00E9 hacemos?"; break;
            }
            menuAt = menuSeen = Anim.Now;
            menuHot = -1;
        }

        void Add(string icon, string text, Action act)
        {
            Choice c = new Choice();
            c.Icon = icon; c.Text = text; c.Do = act;
            menu.Add(c);
        }

        void CloseMenu()
        {
            menu = null;
            menuHot = -1;
        }

        void PaintMenu(Graphics g, double now)
        {
            Font title = Fonts.Get("Segoe Print", 15 * s), item = Fonts.Get("Segoe UI Semibold", 13 * s);
            float rowH = 30 * s, pad = 14 * s, w = 236 * s;
            float h = pad + 34 * s + menu.Count * rowH + pad * 0.6f;
            float x = ClampX(MidX - w / 2, w), y = m.Box.Y - D * 0.32f - h;
            menuRect = new RectangleF(x, y, w, h);

            // Pops out of the mascot: a quick spring in scale around the tail, slightly tilted like a comic panel.
            double t = (now - menuAt) / 1000.0, sc = t >= 1 ? 1 : 1 - Math.Exp(-14 * t) * Math.Cos(18 * t);
            GraphicsState st = g.Save();
            float ox = MidX, oy = m.Box.Y - D * 0.2f;
            g.TranslateTransform(ox, oy);
            g.ScaleTransform((float)(0.6 + 0.4 * sc), (float)(0.6 + 0.4 * sc));
            g.RotateTransform(-1.5f);
            g.TranslateTransform(-ox, -oy);

            float tailX = Math.Max(x + 26 * s, Math.Min(x + w - 26 * s, MidX));
            using (GraphicsPath p = Theme.Round(menuRect, 18 * s))
            using (GraphicsPath tail = new GraphicsPath())
            {
                tail.AddPolygon(new PointF[] { new PointF(tailX - 14 * s, y + h - 2), new PointF(MidX + 4 * s, m.Box.Y - D * 0.16f), new PointF(tailX + 10 * s, y + h - 2) });
                // Hard offset shadow: the comic look.
                GraphicsState ss = g.Save();
                g.TranslateTransform(4 * s, 5 * s);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) { g.FillPath(sh, p); g.FillPath(sh, tail); }
                g.Restore(ss);
                using (Pen outline = new Pen(ComicInk, 3 * s)) { outline.LineJoin = LineJoin.Round; g.DrawPath(outline, p); g.DrawPath(outline, tail); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 253, 246))) { g.FillPath(b, p); g.FillPath(b, tail); }
                // Halftone dots in a corner, a classic comic texture.
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush dots = new SolidBrush(Color.FromArgb(28, 255, 190, 40)))
                    for (float dy = 0; dy < 60 * s; dy += 7 * s)
                        for (float dx = 0; dx < 90 * s - dy; dx += 7 * s)
                            g.FillEllipse(dots, x + w - dx - 8 * s, y + dy + 4 * s, 3.2f * s, 3.2f * s);
                g.Clip = old;
                old.Dispose();
            }
            using (SolidBrush ink = new SolidBrush(ComicInk))
                g.DrawString(menuTitle, title, ink, x + pad, y + pad * 0.55f);
            float ry = y + pad + 34 * s;
            for (int i = 0; i < menu.Count; i++)
            {
                Choice c = menu[i];
                c.R = new RectangleF(x + 8 * s, ry, w - 16 * s, rowH);
                if (i == menuHot)
                    using (GraphicsPath hp = Theme.Round(RectangleF.Inflate(c.R, 0, -2 * s), 9 * s))
                    {
                        MascotParts.FillSolid(g, hp, Color.FromArgb(255, 255, 214, 10));
                        using (Pen pen = new Pen(ComicInk, 2 * s)) g.DrawPath(pen, hp);
                    }
                Icons.Draw(g, c.Icon, new RectangleF(c.R.X + 8 * s, c.R.Y + (rowH - 18 * s) / 2, 18 * s, 18 * s), ComicInk);
                using (SolidBrush ink = new SolidBrush(ComicInk))
                using (StringFormat sf = new StringFormat())
                {
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(c.Text, item, ink, new RectangleF(c.R.X + 34 * s, c.R.Y, c.R.Width - 36 * s, c.R.Height), sf);
                }
                ry += rowH;
            }
            g.Restore(st);
        }

        int ChoiceAt(Point client)
        {
            if (menu == null) return -1;
            for (int i = 0; i < menu.Count; i++) if (menu[i].R.Contains(client)) return i;
            return -1;
        }

        // ---- Mouse: click opens the menu, drag moves it, choices run on release.

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); m.Hover(true); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); m.Hover(false); menuHot = -1; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
            if (ChoiceAt(e.Location) >= 0 || (menu != null && menuRect.Contains(e.Location))) return;
            if (e.Button != MouseButtons.Left) return;
            down = true;
            downAt = Control.MousePosition;
            grab = new Point(downAt.X - (int)bx, downAt.Y - (int)by);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = ChoiceAt(e.Location);
            if (hot != menuHot) menuHot = hot;
            Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
            if (state == State.Drag) { FollowMouse(); return; }
            if (!down) return;
            Point p = Control.MousePosition;
            if (Math.Abs(p.X - downAt.X) > 5 || Math.Abs(p.Y - downAt.Y) > 5)
            {
                CloseMenu();
                state = State.Drag;
                menuDue = 0;
                perch = IntPtr.Zero;
                leaving = false;
                m.WakeUp();
                if (settings.MascotTalks) m.Say("\u00A1Uoooh!", 1500);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int pick = ChoiceAt(e.Location);
            if (pick >= 0)
            {
                Action act = menu[pick].Do;
                CloseMenu();
                BeginInvoke(act);
                return;
            }
            if (menu != null && menuRect.Contains(e.Location)) return;
            bool wasDown = down;
            down = false;
            if (state == State.Drag) { EndDrag(); return; }
            if (e.Button == MouseButtons.Right) { if (menu != null) CloseMenu(); else OpenMenu(); return; }
            if (!wasDown) return;
            // One click opens the menu (after a short wait, in case more follow); quick extra clicks are pokes instead,
            // and five in a row make it dizzy. A click while the menu is open just closes it.
            double now = Anim.Now;
            if (menu != null && clicks == 0) { CloseMenu(); return; }
            clicks = now - lastClick < 380 ? clicks + 1 : 1;
            lastClick = now;
            if (clicks == 1) menuDue = now + 260;
            else
            {
                menuDue = 0;
                if (menu != null) CloseMenu();
                m.Poke(settings.MascotTalks ? MascotTalk.Pokes(settings) : null);
            }
        }

        public void ScreensChanged()
        {
            Screen scr = Screen.FromPoint(new Point((int)(bx + D / 2.0), (int)(by + D / 2.0)));
            Place(scr, bx + D / 2.0);
            by = Ground;
            state = State.Idle;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            timer.Dispose();
            if (dib != null) dib.Dispose();
        }
    }
}
