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
    // speech bubble with things it can do. Excluded from captures and screen sharing. Frame rate adapts: vsync-paced (60 fps or the
    // display rate) only while it walks, falls or is dragged, ~30 with the bubble open, ~16 idle, ~4 asleep.
    public class PetWindow : Form
    {
        enum State { Idle, Walk, Drag, Fall }

        class Choice
        {
            public string Icon, Text, Detail;
            public Action Do;
            public bool Sep;
            public RectangleF R;
        }

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public int cbSize; public uint dwTime; }
        [StructLayout(LayoutKind.Sequential)] struct PowerThrottling { public uint Version, ControlMask, StateMask; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("kernel32.dll")] static extern bool SetProcessInformation(IntPtr process, int cls, ref PowerThrottling info, int size);
        [DllImport("kernel32.dll")] static extern bool SetThreadInformation(IntPtr thread, int cls, ref PowerThrottling info, int size);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        // Keeps the mascot above windows that appear later (other always-on-top windows, the taskbar). Checked every
        // second and a half with one cheap call, and only touches the z-order when something really is above it.
        public static bool KeepOnTop = true;

        // Windows 11 "efficiency mode" (EcoQoS) and timer throttling make a background process tick slowly: the mascot
        // would stutter while another app has focus. Opts this process and the UI thread out of both.
        static bool unthrottled;
        public static void OptOutOfThrottling()
        {
            if (unthrottled) return;
            unthrottled = true;
            try
            {
                PowerThrottling st = new PowerThrottling();
                st.Version = 1;
                st.ControlMask = 1 | 4;   // PROCESS_POWER_THROTTLING_EXECUTION_SPEED | IGNORE_TIMER_RESOLUTION
                st.StateMask = 0;         // bit cleared: throttling off
                SetProcessInformation(GetCurrentProcess(), 4, ref st, Marshal.SizeOf(typeof(PowerThrottling)));
                // Threads only accept the execution-speed bit; ThreadPowerThrottling is class 3.
                PowerThrottling th = new PowerThrottling();
                th.Version = 1;
                th.ControlMask = 1;
                SetThreadInformation(GetCurrentThread(), 3, ref th, Marshal.SizeOf(typeof(PowerThrottling)));
            }
            catch { }
        }

        readonly ShotStack owner;
        readonly Settings settings;
        readonly Mascot m = new Mascot();
        readonly Timer timer = new Timer();
        readonly Random rnd = new Random();
        Dib dib;
        float s = 1f;
        int W, H, D;
        Rectangle wa;
        Screen cur;                   // the monitor wa belongs to
        double bx, by, vy;            // mascot box top-left in screen coordinates; vertical speed while falling
        Screen strollDest;
        double targetX, nextIdea, lastHop, lastCheck, lastFrame, lastIdleLine;
        int dir = 1, wx, wy;
        State state = State.Idle;
        Point grab, downAt;
        bool down, homeShown, blocked, asleepForAway;
        Point lastMouse;
        // Motion: a vsync pacer thread drives frames only while moving; the timer covers the slow cases.
        System.Threading.Thread pacer;
        readonly System.Threading.ManualResetEvent fastGo = new System.Threading.ManualResetEvent(false);
        int queued;
        volatile bool closing;
        bool fast, thrown;
        int bounces, walkFrame;
        double slide;                  // leftover ground speed after a landing
        double dragVx, dragVy, dragPull, dragLastT;
        Point dragLast;
        readonly double[] smT = new double[24];
        readonly Point[] smP = new Point[24];
        int smN;
        const double Gravity = 1800, Terminal = 1500, Restitution = 0.35;
        string bubble;
        // Quick menu
        List<Choice> menu;
        double menuAt, menuSeen, nextTalk;
        int menuHot = -1;
        RectangleF menuRect;

        public PetWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            OptOutOfThrottling();
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
            timer.Tick += delegate { SafeTick(false); };
        }

        void SafeTick(bool fromPacer)
        {
            try { Tick(fromPacer); }
            catch (Exception ex)
            {
                // A drawing bug must not spam the log or eat CPU every frame: stop and leave a trace.
                timer.Stop();
                SetFast(false);
                ShotStack.Log("Mascota en el escritorio: " + ex);
            }
        }

        // Motion runs on the display refresh (DwmFlush) with a 1 ms system timer, only while it moves.
        void SetFast(bool on)
        {
            if (on == fast) return;
            fast = on;
            if (on)
            {
                timeBeginPeriod(1);
                if (pacer == null)
                {
                    pacer = new System.Threading.Thread(Pace);
                    pacer.IsBackground = true;
                    pacer.Name = "Stackshot pet frames";
                    pacer.Start();
                }
                fastGo.Set();
            }
            else { fastGo.Reset(); timeEndPeriod(1); }
        }

        void Pace()
        {
            double last = 0;
            while (!closing)
            {
                fastGo.WaitOne();
                if (closing) break;
                if (DwmFlush() != 0) System.Threading.Thread.Sleep(8);
                else if (Anim.Now - last < 3) System.Threading.Thread.Sleep(3);
                last = Anim.Now;
                if (!fast) continue;
                if (System.Threading.Interlocked.CompareExchange(ref queued, 1, 0) != 0) continue;
                try { BeginInvoke((Action)delegate { queued = 0; if (fast && !closing) SafeTick(true); }); }
                catch (InvalidOperationException) { queued = 0; System.Threading.Thread.Sleep(50); }
            }
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
        void Place(Screen scr, double centerX) { Place(scr, centerX, false); }

        // keep: crossing between monitors, so the position is not clamped into the new area and the walk target survives.
        void Place(Screen scr, double centerX, bool keep)
        {
            int oldD = D;
            cur = scr;
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
            if (keep) { bx = centerX - D / 2.0; targetX += (oldD - D) / 2.0; }
            else
            {
                bx = Math.Max(wa.Left, Math.Min(wa.Right - D, centerX - D / 2.0));
                targetX = bx;
            }
        }

        // Free to wander onto other monitors (not while going home or standing on a window).
        bool Roam { get { return settings.MascotAllScreens && !leaving && perch == IntPtr.Zero; } }

        // The monitor touching the current one on that side (-1 left, 1 right), with some vertical overlap; null if none.
        Screen Neighbor(int side)
        {
            Rectangle a = cur.Bounds;
            foreach (Screen sc in Screen.AllScreens)
            {
                if (sc.DeviceName == cur.DeviceName) continue;
                Rectangle b = sc.Bounds;
                bool touch = side > 0 ? Math.Abs(b.Left - a.Right) <= 8 : Math.Abs(b.Right - a.Left) <= 8;
                if (touch && b.Top < a.Bottom && b.Bottom > a.Top) return sc;
            }
            return null;
        }

        // Its center went past the edge of the floor: step onto the neighbor, or, with nothing touching, hop to the monitor it was
        // heading for, or turn around.
        void Cross(int side)
        {
            Screen nb = Neighbor(side);
            if (nb != null)
            {
                Place(nb, bx + D / 2.0, true);
                double g = Ground;
                if (g < by - 2) { by = g; m.Hop(0.9); }                         // the floor is higher over there: hops up
                else if (g > by + 2) { state = State.Fall; vy = 0; vx = dir * 75 * s; }   // lower: drops down
                return;
            }
            Screen dest = strollDest ?? Screen.FromPoint(new Point((int)(targetX + D / 2.0), (int)(by + D / 2.0)));
            strollDest = null;
            if (dest.WorkingArea != wa && Math.Sign(targetX - bx) == side)
            {
                Place(dest, targetX + D / 2.0);
                by = Ground;
                m.Hop(1.6);
                state = State.Idle;
                nextIdea = Anim.Now + 2500;
                return;
            }
            bx = Math.Max(wa.Left, Math.Min(wa.Right - D, bx));
            targetX = Math.Max(wa.Left, Math.Min(wa.Right - D, bx - side * D * 2.0));
            dir = -side;
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
            if (!PerchFor(perch, out r) || Covered(r))
            {
                // Closed, minimized, maximized or buried under another window: let go and drop with gravity.
                perch = IntPtr.Zero;
                Place(Screen.FromPoint(new Point((int)(bx + D / 2), (int)by)), bx + D / 2.0);
                if (state != State.Fall) { state = State.Fall; vy = 0; vx = 0; if (settings.MascotTalks) m.Say(MascotTalk.Fall(settings), 1400); }
                return;
            }
            int dx = r.Left - perchRect.Left, dy = r.Top - perchRect.Top;
            perchRect = r;
            if (state != State.Fall)
            {
                // Rides along every frame, and stays inside the window's top edge if it shrinks or slides away.
                bx += dx; by += dy; targetX += dx;
                bx = Math.Max(r.Left, Math.Min(r.Right - D, bx));
                targetX = Math.Max(r.Left, Math.Min(r.Right - D, targetX));
                if (dy < -10 * s) m.Hop(0.5);
            }
        }

        // Checked now and then (not every frame): is another window covering the spot under its feet?
        bool Covered(Native.RECT r)
        {
            double now = Anim.Now;
            if (now - lastCover < 1200) return false;
            lastCover = now;
            Native.POINT pt;
            pt.X = (int)(bx + D / 2);
            pt.Y = r.Top + (int)(14 * s);
            IntPtr top = GetAncestor(WindowFromPoint(pt), 2);
            if (top == IntPtr.Zero || top == perch || top == Handle) return false;
            uint pid;
            Native.GetWindowPid(top, out pid);
            return pid != MyPid;
        }
        double lastCover;

        // Never frozen in the air or sunk into a window: if the ground is not where it stands (the window it was on
        // moved in a way that was missed, the taskbar changed size), it drops, or hops out if it ended up inside.
        void CheckGround()
        {
            if (state != State.Idle && state != State.Walk) return;
            double gap = Ground - by;
            if (Math.Abs(gap) < 1.5) return;
            if (gap > 0) { vy = 0; }
            else { vy = -Math.Sqrt(2 * Gravity * s * -gap) - 200 * s; m.Hop(0.8); }
            vx = 0;
            state = State.Fall;
        }

        // Jumps in an arc to a spot on top of the window.
        void ClimbOnto(IntPtr h, Native.RECT r)
        {
            perch = h;
            perchRect = r;
            double g = Gravity * s, rise = Math.Max(0, by - Ground) + 40 * s;
            vy = -Math.Sqrt(2 * g * rise);
            double flight = -vy / g + Math.Sqrt(2 * 40 * s / g);
            double tx = Math.Max(r.Left + D * 0.1, Math.Min(r.Right - D * 1.1, bx + (rnd.NextDouble() - 0.5) * D * 3));
            vx = (tx - bx) / flight;
            state = State.Fall;
            m.WakeUp();
            m.Hop(1.4);
            if (settings.MascotTalks && rnd.NextDouble() < 0.3) m.Say(MascotTalk.Climb(settings), 2400);
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
            strollDest = null;
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
            // Tossed: keeps the speed of the last ~80 ms of the drag, then floats down with air drag.
            double tvx, tvy;
            ThrowSpeed(out tvx, out tvy);
            vx = tvx; vy = tvy;
            thrown = true;
            bounces = 0;
            slide = 0;
            Capture = false;
            if (Math.Abs(tvx) + Math.Abs(tvy) > 400 * s) m.Wobble(0.6, true);
        }

        void Sample(Point p)
        {
            double t = Anim.Now;
            if (smN > 0 && t - smT[(smN - 1) % smT.Length] < 2) return;
            smT[smN % smT.Length] = t; smP[smN % smT.Length] = p; smN++;
        }

        // Velocity of the cursor over the last ~80 ms (px/s), including how it moves right now.
        void ThrowSpeed(out double tvx, out double tvy)
        {
            tvx = tvy = 0;
            Point cur = Control.MousePosition;
            double now = Anim.Now;
            double t0 = now, x0 = cur.X, y0 = cur.Y;
            for (int i = smN - 1; i >= 0 && i > smN - smT.Length; i--)
            {
                int k = i % smT.Length;
                if (now - smT[k] > 80) break;
                t0 = smT[k]; x0 = smP[k].X; y0 = smP[k].Y;
            }
            double dt = (now - t0) / 1000.0;
            if (dt < 0.016) return;
            tvx = (cur.X - x0) / dt;
            tvy = (cur.Y - y0) / dt;
            double sp = Math.Sqrt(tvx * tvx + tvy * tvy), cap = 3200 * s;
            if (sp > cap) { tvx *= cap / sp; tvy *= cap / sp; }
            if (sp < 160 * s) { tvx = 0; tvy = 0; }
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);

        // The window right above it, if it should go back over it: never over the app's own windows (capture overlay,
        // toasts) nor over popup menus and tooltips of other apps.
        bool ForeignAbove()
        {
            IntPtr above = GetWindow(Handle, 3); // GW_HWNDPREV
            if (above == IntPtr.Zero || !IsWindowVisible(above)) return false;
            uint pid;
            Native.GetWindowPid(above, out pid);
            if (pid == MyPid) return false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            GetClassName(above, sb, 64);
            string c = sb.ToString();
            return c != "#32768" && c != "tooltips_class32" && c != "SysShadow";
        }

        void Tick(bool fromPacer)
        {
            double now = Anim.Now;
            // The timer also fires while the vsync pacer runs: skip it if a frame was just drawn.
            if (!fromPacer && fast && lastFrame != 0 && now - lastFrame < 10) return;
            double dt = lastFrame == 0 ? 0.033 : Math.Min(0.1, (now - lastFrame) / 1000.0);
            lastFrame = now;

            if (now - lastCheck > 1500)
            {
                lastCheck = now;
                bool b = FullScreenBusy();
                if (b != blocked) { blocked = b; UpdateVisibility(); }
                if (Visible && state != State.Drag)
                {
                    // Work area changed (taskbar resized or auto-hidden): follow it. Then make sure it is still on top.
                    Screen cur = Screen.FromPoint(new Point((int)(bx + D / 2.0), (int)(by + D / 2.0)));
                    if (cur.WorkingArea != wa) Place(cur, bx + D / 2.0);
                    if (KeepOnTop && ForeignAbove())
                        Native.SetWindowPos(Handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // TOPMOST, NOSIZE | NOMOVE | NOACTIVATE
                }
                // Asleep while the user is away; awake again on return.
                int idle = IdleMs();
                if (idle > 60000 && !asleepForAway && state != State.Drag) { asleepForAway = true; CloseMenu(); m.SleepNow(); state = State.Idle; }
                else if (idle < 1500 && asleepForAway) { asleepForAway = false; m.WakeUp(); m.Hop(1.2); }
            }
            if (!Visible) { SetFast(false); timer.Interval = 500; return; }

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
            CheckGround();
            m.Busy = state != State.Idle || menu != null || leaving || clicks > 0;
            m.Talks = settings.MascotTalks;
            m.Energy = settings.MascotEnergy;
            m.Chatter = settings.MascotChatter;
            m.Alone = settings.MascotAlone;
            m.SleepIdle = settings.MascotSleepIdle;
            if (menuDue > 0 && now >= menuDue) { menuDue = 0; clicks = 0; OpenMenu(); }
            if (clicks > 0 && now - lastClick > 400 && menuDue == 0) clicks = 0;
            switch (state)
            {
                case State.Drag:
                    // The button went up without a mouse-up reaching us (Alt+Tab, a system dialog): treat it as a drop.
                    if ((Control.MouseButtons & MouseButtons.Left) == 0) { EndDrag(); break; }
                    Sample(mouse);
                    bx = mouse.X - grab.X;
                    by = mouse.Y - grab.Y;
                    if (now > dragLastT)
                    {
                        double sec = (now - dragLastT) / 1000.0, k = 1 - Math.Exp(-sec / 0.06);
                        dragVx += ((mouse.X - dragLast.X) / sec - dragVx) * k;
                        dragVy += ((mouse.Y - dragLast.Y) / sec - dragVy) * k;
                        dragLast = mouse; dragLastT = now;
                    }
                    break;
                case State.Fall:
                    vy = Math.Min(Terminal * s, vy + Gravity * s * dt);
                    if (thrown) { vx *= Math.Exp(-0.8 * dt); vy *= Math.Exp(-0.25 * dt); }
                    by += vy * dt;
                    bx += vx * dt;
                    if (settings.MascotAllScreens && perch == IntPtr.Zero && (bx + D / 2.0 < wa.Left || bx + D / 2.0 > wa.Right))
                    {
                        Screen sc = Screen.FromPoint(new Point((int)(bx + D / 2.0), (int)(by + D / 2.0)));
                        if (sc.WorkingArea != wa) Place(sc, bx + D / 2.0, true);   // lands on the floor of the monitor it is over
                    }
                    if (thrown)
                    {
                        // Walls and ceiling of the monitor it was thrown on (a neighbor takes it in above): a soft rebound.
                        if (bx + D / 2.0 < wa.Left || bx + D / 2.0 > wa.Right)
                        {
                            bx = Math.Max(wa.Left, Math.Min(wa.Right - D, bx));
                            vx = -vx * 0.4;
                        }
                        if (by < wa.Top) { by = wa.Top; vy = Math.Abs(vy) * 0.3; }
                    }
                    if (vy >= 0 && by >= Ground)
                    {
                        double hit = vy;
                        by = Ground;
                        if (perch != IntPtr.Zero) bx = Math.Max(perchRect.Left, Math.Min(perchRect.Right - D, bx));
                        double impact = Math.Min(1, hit / (1100 * s));
                        if (hit * Restitution > 150 * s && bounces < 2)
                        {
                            // A soft bounce: squashes, leaves the floor again with a third of the speed.
                            m.Land(impact * (bounces == 0 ? 1 : 0.6));
                            vy = -hit * Restitution;
                            vx *= 0.75;
                            bounces++;
                        }
                        else
                        {
                            m.Land(impact * (bounces == 0 ? 1 : 0.5));
                            if (thrown && hit > 500 * s) m.Wobble(Math.Min(1, hit / (1300 * s)), hit > 900 * s);
                            if (hit > 1100 * s && settings.MascotTalks && rnd.NextDouble() < 0.35) m.Say(MascotTalk.Land(settings), 2000);
                            slide = thrown ? vx * 0.6 : 0;
                            vx = 0; vy = 0;
                            thrown = false; bounces = 0;
                            state = State.Idle;
                            nextIdea = now + 2500;
                        }
                    }
                    break;
                case State.Walk:
                {
                    double step = (leaving ? 160 : Math.Abs(targetX - bx) > 600 * s ? 150 : 75) * s * dt;
                    if (Math.Abs(targetX - bx) <= step)
                    {
                        bx = targetX;
                        state = State.Idle;
                        nextIdea = now + 4000 + rnd.NextDouble() * 6000;
                        if (leaving) { BeginInvoke((Action)Gone); return; }
                        RememberSpot(false);
                    }
                    else bx += Math.Sign(targetX - bx) * step;
                    if (Roam && (bx + D / 2.0 < wa.Left || bx + D / 2.0 > wa.Right)) Cross(bx + D / 2.0 < wa.Left ? -1 : 1);
                    if (now - lastHop > 420) { lastHop = now; m.Hop(0.7); }
                    break;
                }
                default:
                    if (Math.Abs(slide) > 6 * s)
                    {
                        // Skids a little after a toss, then stops.
                        double lo = perch != IntPtr.Zero ? perchRect.Left : wa.Left, hi = perch != IntPtr.Zero ? perchRect.Right - D : wa.Right - D;
                        bx += slide * dt;
                        slide *= Math.Exp(-7 * dt);
                        if (bx < lo || bx > hi) { bx = Math.Max(lo, Math.Min(hi, bx)); slide = 0; }
                        targetX = bx;
                        if (Math.Abs(slide) <= 6 * s) { slide = 0; RememberSpot(true); }
                    }
                    else slide = 0;
                    if (leaving) { WalkOff(); break; }
                    if (menu == null && !m.Sleeping && now > nextIdea) Idea(now);
                    if (m.Sleeping && !asleepForAway && now > nextIdea + 120000) { m.WakeUp(); Idea(now); }
                    break;
            }
            // Leans (and stretches a little) toward where it is going, held by the mouse or in the air after a toss.
            double lean = state == State.Walk ? dir * 0.8 : state == State.Drag ? dragVx / (650 * s) : state == State.Fall && thrown ? vx / (1100 * s) : 0;
            double pull = state == State.Drag ? Math.Sqrt(dragVx * dragVx + dragVy * dragVy) / (2000 * s) : state == State.Fall ? Math.Abs(vy) / (Terminal * s) * 0.6 : 0;
            m.Facing = Math.Max(-1.5, Math.Min(1.5, lean));
            dragPull += (Math.Min(1, pull) - dragPull) * (1 - Math.Exp(-dt / 0.08));
            m.Pull = dragPull;
            m.Dangling = state == State.Drag;
            m.ShowShadow = state != State.Drag && state != State.Fall;

            PlaceWindow();
            m.Step(now, new PointF(mouse.X - wx, mouse.Y - wy), moved);
            // While it only walks, the picture is redrawn every other frame (30 fps) and the window still moves every frame.
            bool still = state == State.Walk && fast && menu == null && bubble == null && m.Bubble == null && !lastFull && (++walkFrame & 1) == 1;
            if (still) MoveOnly(); else Render(now);

            // The system timer ticks every 15.6 ms: 16 = ~64 fps, 31 = ~32 fps, 63 = ~16 fps (a gentle float), 250 asleep.
            bool wantFast = state == State.Drag || state == State.Fall || state == State.Walk || slide != 0;
            SetFast(wantFast);
            int interval = wantFast ? 33 : menu != null || m.Lively ? 31 : m.Sleeping ? 250 : 63;
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
                Screen[] all = Screen.AllScreens;
                if (Roam && all.Length > 1 && rnd.NextDouble() < 0.35)
                {
                    // A stroll to another monitor.
                    Screen os = all[rnd.Next(all.Length)];
                    Rectangle ow = os.WorkingArea;
                    if (ow != wa)
                    {
                        targetX = ow.Left + rnd.NextDouble() * Math.Max(1, ow.Width - D);
                        strollDest = os;
                        dir = Math.Sign(targetX - bx);
                        state = State.Walk;
                        m.WakeUp();
                        return;
                    }
                }
                double range = Math.Min(wa.Width - D, 520 * s);
                double lo = perch != IntPtr.Zero ? perchRect.Left : wa.Left, hi = perch != IntPtr.Zero ? perchRect.Right - D : wa.Right - D;
                strollDest = null;
                targetX = Math.Max(lo, Math.Min(hi, bx + (rnd.NextDouble() * 2 - 1) * range));
                if (Math.Abs(targetX - bx) > 30 * s) { dir = Math.Sign(targetX - bx); state = State.Walk; m.WakeUp(); }
            }
            else if (r < 0.6 && settings.MascotTalks && now - lastIdleLine > nextTalk)
            {
                lastIdleLine = now;
                nextTalk = (45000 + rnd.NextDouble() * 75000) * (settings.MascotChatter == 0 ? 3.0 : settings.MascotChatter == 2 ? 0.5 : 1.0);
                m.Greet(MascotTalk.Idle(settings));
            }
        }

        void Render(double now)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
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
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            shownX = wx + crop.X; shownY = wy + crop.Y;
            Native.Present(Handle, dib.Dc, shownX, shownY, crop.Width, crop.Height, crop.X, crop.Y);
            if (Probe != null) { double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency; Probe((t1 - t0) * f, (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * f, crop.Width * crop.Height); }
        }

        // Optional frame timing hook (render ms, present ms, pixels sent); null in normal use.
        public static Action<double, double, int> Probe;

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
            Sample(mouse);
            bx = mouse.X - grab.X;
            by = mouse.Y - grab.Y;
            PlaceWindow();
            MoveOnly();
        }

        // Same picture, new place: the window moves by whole pixels without repainting anything.
        void MoveOnly()
        {
            int x = wx + presentAt.X, y = wy + presentAt.Y;
            if (x == shownX && y == shownY) return;
            shownX = x; shownY = y;
            Native.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0200); // NOSIZE | NOZORDER | NOACTIVATE | NOOWNERZORDER
        }
        int shownX = int.MinValue, shownY = int.MinValue;

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
            x = (float)Math.Round(x); y = (float)Math.Round(y); w = (float)Math.Ceiling(w); h = (float)Math.Ceiling(h);
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
                { g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.DrawString(bubble, f, tb, new RectangleF((float)Math.Round(x + 13 * s), (float)Math.Round(y + 8 * s), (float)Math.Ceiling(w - 24 * s), (float)Math.Ceiling(h - 12 * s))); }
        }

        // Keeps a bubble of width w inside the monitor, even when the mascot stands at an edge.
        float ClampX(float x, float w)
        {
            float minX = wa.Left - wx + 6 * s, maxX = wa.Right - wx - w - 6 * s;
            return Math.Max(Math.Max(4 * s, minX), Math.Min(Math.Min(W - w - 4 * s, maxX), x));
        }

        // ---- Quick menu: the same rounded, minimal look as the app's menus (Ds palette, hairline border, soft shadow,
        // line icons, accent highlight), light or dark, with a small arrow pointing at the mascot.

        void OpenMenu()
        {
            m.WakeUp();
            m.Bubble = null;
            bubble = null;
            m.Hop(0.9);
            menu = new List<Choice>();
            Add("area", "Capturar un \u00E1rea", Hotkeys.Display(settings.HotRegion), delegate { owner.Run("region"); });
            Add("video", Recorder.Recording ? "Parar la grabaci\u00F3n" : "Grabar un v\u00EDdeo", null, delegate { owner.Run("video"); });
            Add("folder", "Abrir mis capturas", null, delegate { owner.OpenFolder(); });
            AddSep();
            Add("brush", "Cambiarme de ropa", null, delegate { owner.ShowHome("mascot", false); });
            Add("play", "Ponte a jugar", null, delegate { m.Perform(MascotCmd.Play); });
            Add("sparkle", "Haz un truco", null, delegate
            {
                int trick = rnd.Next(MascotCmd.Count + 1);
                if (trick == MascotCmd.Count) m.Poke(settings.MascotTalks ? MascotTalk.Pokes(settings) : null);
                else m.Perform(trick == MascotCmd.Sleep ? MascotCmd.Dance : trick);
            });
            Add("bot", m.Sleeping ? "Despierta" : "Echa una siesta", null, delegate { if (m.Sleeping) { m.WakeUp(); m.Hop(1.2); } else { m.SleepNow(); state = State.Idle; } });
            AddSep();
            Add("logo", "Abrir Stackshot", null, delegate { owner.ShowHome("home", false); });
            Add("home", "Vete a casa", null, GoHome);
            menuAt = menuSeen = Anim.Now;
            menuHot = -1;
        }

        void Add(string icon, string text, string detail, Action act)
        {
            Choice c = new Choice();
            c.Icon = icon; c.Text = text; c.Detail = detail; c.Do = act;
            menu.Add(c);
        }

        void AddSep()
        {
            Choice c = new Choice();
            c.Sep = true;
            menu.Add(c);
        }

        void CloseMenu()
        {
            menu = null;
            menuHot = -1;
        }

        static Color Fade(Color c, double k) { return Color.FromArgb((int)Math.Round(c.A * Math.Max(0, Math.Min(1, k))), c); }

        void PaintMenu(Graphics g, double now)
        {
            Palette pal = Ds.Brushes;
            bool dark = pal.Dark;
            Font item = Fonts.Get(Mac.TextFont, 13 * s), small = Fonts.Get(Mac.TextFont, 12 * s);
            float rowH = 28 * s, sepH = 9 * s, pad = 5 * s, w = 238 * s, arrow = 7 * s;
            float h = pad * 2;
            foreach (Choice ch in menu) h += ch.Sep ? sepH : rowH;
            float x = ClampX(MidX - w / 2 - 14 * s, w + 28 * s) + 14 * s, y = m.Box.Y - D * 0.22f - arrow - h;
            menuRect = new RectangleF(x, y, w, h);

            // Quick ease-out: a fade with a slight settle in size, anchored at the arrow.
            double t = (now - menuAt) / 1000.0, a = Math.Min(1, t / 0.1), p = 1 - Math.Exp(-t * 20);
            float tailX = Math.Max(x + 22 * s, Math.Min(x + w - 22 * s, MidX));
            GraphicsState st = g.Save();
            float oy = y + h + arrow;
            g.TranslateTransform(tailX, oy);
            float sc = (float)(0.95 + 0.05 * Math.Min(1, p));
            g.ScaleTransform(sc, sc);
            g.TranslateTransform(-tailX, -oy);

            Color bg = dark ? Color.FromArgb(44, 44, 47) : Color.FromArgb(250, 250, 251);
            Color border = dark ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(31, 0, 0, 0);
            // Soft shadow: stacked rounded rectangles that fade out.
            for (int i = 8; i >= 1; i--)
            {
                RectangleF sr = menuRect;
                sr.Inflate(i * 1.5f * s, i * 1.5f * s);
                sr.Offset(0, 5 * s);
                double f = 1 - i / 9.0;
                using (GraphicsPath sp = Theme.Round(sr, 10 * s + i * 1.5f * s))
                    MascotParts.FillSolid(g, sp, Color.FromArgb((int)((dark ? 15 : 8) * f * f * a + 0.5), 0, 0, 0));
            }
            using (GraphicsPath cp = Theme.Round(menuRect, 10 * s))
            using (GraphicsPath tp = new GraphicsPath())
            {
                MascotParts.FillSolid(g, cp, Fade(bg, a));
                tp.AddPolygon(new PointF[] { new PointF(tailX - 7 * s, y + h - 0.5f), new PointF(tailX, y + h + arrow), new PointF(tailX + 7 * s, y + h - 0.5f) });
                MascotParts.FillSolid(g, tp, Fade(bg, a));
                MascotParts.Stroke(g, cp, Fade(border, a), 1);
                // The arrow's two sides, drawn over the card border where they meet.
                using (Pen pen = new Pen(Fade(border, a), 1))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[] { new PointF(tailX - 7 * s, y + h), new PointF(tailX, y + h + arrow), new PointF(tailX + 7 * s, y + h) });
                }
                using (SolidBrush cover = new SolidBrush(Fade(bg, a))) g.FillRectangle(cover, tailX - 6.2f * s, y + h - 1.5f, 12.4f * s, 2.2f);
            }

            Color label = Ds.Gdi(pal.Label), label2 = Ds.Gdi(pal.Label2);
            float ry = y + pad;
            for (int i = 0; i < menu.Count; i++)
            {
                Choice c = menu[i];
                if (c.Sep)
                {
                    using (Pen sp = new Pen(Fade(Ds.Gdi(pal.Separator), a), 1)) g.DrawLine(sp, x + 10 * s, ry + sepH / 2, x + w - 10 * s, ry + sepH / 2);
                    c.R = RectangleF.Empty;
                    ry += sepH;
                    continue;
                }
                c.R = new RectangleF(x + pad, ry, w - pad * 2, rowH);
                bool hot = i == menuHot;
                if (hot)
                    using (GraphicsPath hp = Theme.Round(c.R, 6 * s)) MascotParts.FillSolid(g, hp, Fade(Ds.Gdi(pal.Accent), a));
                Color fg = hot ? Color.White : label;
                Icons.Draw(g, c.Icon, new RectangleF(c.R.X + 8 * s, c.R.Y + (rowH - 17 * s) / 2, 17 * s, 17 * s), Fade(hot ? Color.White : label2, a));
                using (SolidBrush ink = new SolidBrush(Fade(fg, a)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(c.Text, item, ink, new RectangleF(c.R.X + 33 * s, c.R.Y, c.R.Width - 36 * s, c.R.Height), sf);
                    if (c.Detail != null)
                    {
                        sf.Alignment = StringAlignment.Far;
                        using (SolidBrush dk = new SolidBrush(Fade(hot ? Color.FromArgb(200, 255, 255, 255) : label2, a)))
                            g.DrawString(c.Detail, small, dk, new RectangleF(c.R.X, c.R.Y, c.R.Width - 10 * s, c.R.Height), sf);
                    }
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
            Capture = true;
            smN = 0;
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
                thrown = false; slide = 0;
                dragLast = Control.MousePosition; dragLastT = Anim.Now; dragVx = dragVy = dragPull = 0;
                menuDue = 0;
                perch = IntPtr.Zero;
                leaving = false;
                m.WakeUp();
                if (settings.MascotTalks) m.Say(MascotTalk.Drag(settings), 1700);
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
            if (state != State.Drag) Capture = false;
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
            perch = IntPtr.Zero;   // the window it stood on may have gone with the monitor
            Screen scr = Screen.FromPoint(new Point((int)(bx + D / 2.0), (int)(by + D / 2.0)));
            Place(scr, bx + D / 2.0);
            by = Ground;
            state = State.Idle;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            closing = true;
            SetFast(false);
            fastGo.Set();
            timer.Dispose();
            if (dib != null) dib.Dispose();
        }
    }
}
