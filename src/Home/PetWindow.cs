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
    // speech bubble with things it can do. Excluded from captures and screen sharing. Frame rate adapts (MascotRate): vsync-paced
    // (60 fps or the display rate) only while it walks, falls or is dragged, 32 with the bubble open or the mouse moving near
    // it, 16 idle or asleep, 2 with the screen off or locked. The animation runs on real time at any rate.
    public class PetWindow : Form
    {
        enum State { Idle, Walk, Drag, Fall }

        class Choice
        {
            public string Icon, Text, Detail;
            public Action Do;
            public bool Sep, Header;       // Header: a line of text at the top, not clickable
            public RectangleF R;
            public float TextW, DetailW, HeaderH;   // measured once, at the first paint
        }
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        // Keeps the mascot above windows that appear later (other always-on-top windows, the taskbar). Checked every
        // second and a half with one cheap call, and only touches the z-order when something really is above it.
        public static bool KeepOnTop = true;

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
        bool down, homeShown, asleepForAway;
        Point lastMouse;
        // Motion: a vsync pacer thread drives frames only while moving; the timer covers the slow cases.
        System.Threading.Thread pacer;
        readonly System.Threading.ManualResetEvent fastGo = new System.Threading.ManualResetEvent(false);
        int queued;
        volatile bool closing;
        bool fast, thrown;
        int bounces;
        double lastWalkPaint;
        float walkPaintX;
        const double WalkPaintMs = 26;
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
        bool menuArmed;
        RectangleF menuRect;
        List<Choice> menuGone;         // the menu just closed, fading out (drawn only, never clicked)
        RectangleF menuGoneRect;
        double menuGoneAt;
        const double MenuInMs = 170, MenuOutMs = 130;
        // Healthy breaks: the reminder on show (its kind, or -1) and the guided break in progress.
        int menuWell = -1, session = -1, sessionReps;
        bool menuSticky;            // the menu stays a minute and ignores clicks elsewhere (news after an update)
        bool menuFocus;             // the menu is a focus-mode question
        int sessionSerial;
        bool sessionStretch;

        public PetWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            // Efficiency mode would make it tick slowly while another app has focus (Power logs a refusal).
            Power.OptOutOfThrottling();
            Power.OptOutThisThread();
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            m.Look = MascotLook.From(settings);
            Screen scr = Screen.FromPoint(Control.MousePosition);
            Place(scr, scr.WorkingArea.Left + scr.WorkingArea.Width * settings.MascotSpot / 1000.0); // where it was last time
            by = Ground;
            timer.Interval = MascotRate.Look;
            timer.Tick += delegate { SafeTick(false); };
            // Files and text can be dropped on it (only where it is drawn: the rest of the window lets everything through).
            AllowDrop = true;
        }

        void SafeTick(bool fromPacer)
        {
            try { Tick(fromPacer); }
            catch (Exception ex)
            {
                // A drawing bug must not spam the log or eat CPU every frame, nor leave the mascot frozen for good: it
                // pauses, drops the menu (what is most likely to fail), and comes back a second later.
                timer.Stop();
                SetFast(false);
                // Failures far apart are glitches, not a broken mascot: only a run of them (a minute apart at most) counts
                // towards giving up.
                if (unchecked(Environment.TickCount - lastFailure) > 60000) tickFailures = 0;
                lastFailure = Environment.TickCount;
                if (++tickFailures <= 3) ShotStack.Log("Mascota en el escritorio: " + ex);
                try { CloseMenu(); } catch (Exception) { }
                if (IsDisposed || tickFailures > 50) return;
                Timer again = new Timer { Interval = 1000 };
                again.Tick += delegate { again.Stop(); again.Dispose(); if (!IsDisposed) timer.Start(); };
                again.Start();
            }
        }

        int tickFailures, lastFailure;

        // Motion runs on the display refresh (DwmFlush) with a 1 ms system timer, only while it moves.
        void SetFast(bool on)
        {
            if (on == fast) return;
            fast = on;
            // The process never has focus, so it gets no foreground boost: under load (builds) frames would slip while it
            // moves. Both frame threads run above normal only then.
            System.Threading.Thread ui = System.Threading.Thread.CurrentThread;
            if (on)
            {
                timeBeginPeriod(1);
                if (pacer == null)
                {
                    pacer = new System.Threading.Thread(Pace);
                    pacer.IsBackground = true;
                    pacer.Name = "Stackshot pet frames";
                    pacer.Priority = System.Threading.ThreadPriority.AboveNormal;   // it only waits and posts
                    pacer.Start();
                }
                fastGo.Set();
                try { uiPriority = ui.Priority; if (uiPriority < System.Threading.ThreadPriority.AboveNormal) ui.Priority = System.Threading.ThreadPriority.AboveNormal; }
                catch { }
            }
            else
            {
                fastGo.Reset();
                timeEndPeriod(1);
                try { ui.Priority = uiPriority; } catch { }
            }
        }
        System.Threading.ThreadPriority uiPriority = System.Threading.ThreadPriority.Normal;

        void Pace()
        {
            Power.OptOutThisThread();
            double last = 0, clockUntil = 0;
            while (!closing)
            {
                fastGo.WaitOne();
                if (closing) break;
                double t0 = Anim.Now;
                if (t0 < clockUntil) System.Threading.Thread.Sleep(Math.Max(1, 16 - (int)(t0 - last)));
                else if (DwmFlush() != 0) System.Threading.Thread.Sleep(8);
                // DwmFlush waits for the next composition, which may not come while nothing on screen changes (a slow
                // walk moves the window every other frame): pace by the clock for a moment instead.
                else if (Anim.Now - t0 > 25) clockUntil = Anim.Now + 500;
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
            MascotScreen.Ensure();
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
                // Room above it for the longest menu (focus mode on: its line and choices on top of the rest); only what
                // is drawn is ever repainted or sent to the screen.
                H = (int)Math.Round(D * 1.02 + 800 * s);
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
                if (sc.DeviceName == cur.DeviceName || Busy(sc)) continue;
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
            if (dest.WorkingArea != wa && !Busy(dest) && Math.Sign(targetX - bx) == side)
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
            m.Greet(settings.MascotTalks ? (m.CanVent ? I18n.T("Me voy por la alcantarilla\u2026 \u00A1que nadie me vea!", "Off through the vent\u2026 don't let anyone see me!") : I18n.T("\u00A1Me voy a casa! \u00A1Hasta luego!", "Heading home! See you later!")) : null);
            nextIdea = Anim.Now + 1300; // wave first
            if (perch != IntPtr.Zero) HopDown();
        }

        // The crewmate leaves through a vent instead, right where it stands.
        double ventDone;

        void WalkOff()
        {
            if (Anim.Now < nextIdea) return;
            if (m.CanVent)
            {
                if (ventDone == 0) { m.Vent(); ventDone = Anim.Now + Mascot.VentTime; }
                else if (Anim.Now >= ventDone) { ventDone = 0; BeginInvoke((Action)Gone); }
                return;
            }
            bool left = bx + D / 2.0 < wa.Left + wa.Width / 2.0;
            strollDest = null;
            targetX = left ? wa.Left - D * 1.4 : wa.Right + D * 0.4;
            dir = left ? -1 : 1;
            state = State.Walk;
        }

        void Gone()
        {
            leaving = false;
            ventDone = 0;
            m.EndVent();
            settings.MascotDesktop = false;
            owner.MascotChanged();
            owner.ApplySettings();
        }

        // Remembers where it stands (per monitor width), so it comes back there after a restart or a new session. Walks
        // only save once a minute; drops save two seconds after the last one. The file is written off the UI thread, so
        // a slow disk or an antivirus scan never makes it hitch as it lands.
        double lastSpotSave, spotDue;
        void RememberSpot(bool now)
        {
            if (perch != IntPtr.Zero || wa.Width <= 0) return;
            settings.MascotSpot = Math.Max(0, Math.Min(1000, (int)Math.Round((bx + D / 2.0 - wa.Left) * 1000.0 / wa.Width)));
            if (now) { spotDue = Anim.Now + 2000; return; }
            if (Anim.Now - lastSpotSave < 60000) return;
            SaveSpot();
        }

        void SaveSpot()
        {
            lastSpotSave = Anim.Now;
            spotDue = 0;
            Settings st = settings;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { st.Save(); });
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
            double sp = Math.Sqrt(tvx * tvx + tvy * tvy);
            if (sp < 160 * s) { tvx = 0; tvy = 0; return; }
            // A normal toss keeps the hand's speed; a harder one goes harder still (each px/s past the knee counts 1.7),
            // up to a ceiling that keeps it on the monitor's walls and ceiling.
            double knee = 1200 * s, cap = 7000 * s, boosted = sp <= knee ? sp : Math.Min(cap, knee + (sp - knee) * 1.7);
            tvx *= boosted / sp;
            tvy *= boosted / sp;
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
            if (settings.MascotTalks) m.Say(MascotTalk.Hello(settings) + I18n.T(" Haz clic en m\u00ED.", " Click me."), 4200);
        }

        // Neither a new look nor a capture interrupts a break in progress.
        public void LookChanged()
        {
            MascotLook l = MascotLook.From(settings);
            lookDue = l;
            // A look it hasn't drawn yet is drawn first on a worker (MascotWarm): changing clothes from the main window then
            // doesn't stall that window's frames, which run on this same thread.
            if (MascotWarm.Ahead(l, D, delegate(Action a) { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }, delegate { PutOn(l); })) return;
            PutOn(l);
        }

        MascotLook lookDue;   // the newest look asked for, until it goes on

        void PutOn(MascotLook l)
        {
            if (lookDue != l || IsDisposed) return;
            lookDue = null;
            m.Look = l;
            if (session < 0) m.Celebrate(null);
        }

        public void Celebrate(string text)
        {
            if (state == State.Drag || session >= 0) return;
            CloseMenu();
            m.Celebrate(settings.MascotTalks ? text ?? MascotTalk.Celebrate(settings) : null);
        }

        // ---- Healthy breaks. The owner decides when (Wellness); here the mascot asks, and does the break with you.

        // Free to bring up a reminder now: on screen, standing still, no menu, nothing being dragged or done.
        public bool CanRemind
        {
            get { return Visible && !behind && !leaving && state == State.Idle && menu == null && session < 0 && !down && clicks == 0; }
        }

        public void Remind(int kind, string text)
        {
            m.WakeUp();
            m.Bubble = null;
            bubble = null;
            m.Perform(MascotCmd.Wave);
            m.Bubble = null;
            menu = new List<Choice>();
            Header(text);
            string go = kind == WellKind.Water ? I18n.T("Bebo un vaso contigo", "I'll drink a glass with you") : kind == WellKind.Eyes ? I18n.T("Empezar (20 s)", "Start (20 s)") :
                        kind == WellKind.Move ? (owner.MoveIsStretch ? I18n.T("Estirarnos juntos", "Let's stretch together") : I18n.T("Hacer 8 sentadillas", "Do 8 squats")) : I18n.T("Respirar 1 minuto", "Breathe for 1 minute");
            int k = kind;
            Add(WellKind.Glyphs[kind], go, null, delegate { StartSession(k); });
            Add("check", I18n.T("Ya lo he hecho", "Already done"), null, delegate { Finish(k, true); });
            Add("clock", I18n.T("Recu\u00E9rdamelo en 10 min", "Remind me in 10 min"), null, delegate { owner.WellLater(k, 600000); m.Say(MascotTalk.WellLater(settings), 3200); });
            Add("close", I18n.T("Hoy no", "Not today"), null, delegate { owner.WellLater(k, -1); m.Say(MascotTalk.WellToday(settings, k), 3200); });
            menuAt = menuSeen = Anim.Now;
            menuGone = null;
            Halt();
            menuArmed = false;
            menuRect = RectangleF.Empty;
            menuHot = -1;
            menuWell = kind;
            nextIdea = Anim.Now + 8000;
        }

        // From its menu: every break, any time, with today's count on top.
        void OpenBreaks()
        {
            m.WakeUp();
            m.Hop(0.6);
            menu = new List<Choice>();
            Header(Wellness.Summary(settings));
            for (int i = 0; i < WellKind.Count; i++)
            {
                int k = i;
                if (k == WellKind.Water && !owner.WaterOn) continue;
                string text = k == WellKind.Water ? I18n.T("Bebe agua conmigo", "Drink water with me") : k == WellKind.Eyes ? I18n.T("Descansa la vista (20 s)", "Rest your eyes (20 s)") :
                              k == WellKind.Move ? (owner.MoveIsStretch ? I18n.T("Estira la espalda", "Stretch your back") : I18n.T("Estira las piernas", "Stretch your legs")) : I18n.T("Respira conmigo (1 min)", "Breathe with me (1 min)");
                Add(WellKind.Glyphs[k], text, null, delegate { StartSession(k); });
            }
            AddSep();
            Add("gear", I18n.T("Ajustar los avisos", "Adjust reminders"), null, delegate { owner.ShowHome("mascotset", false); });
            menuAt = menuSeen = Anim.Now;
            menuGone = null;
            Halt();
            menuArmed = false;
            menuRect = RectangleF.Empty;
            menuHot = -1;
        }

        // Right after an update: it says the version is here and offers what it brings (asked once, by the owner).
        public void AnnounceNews(string version, string headline, Action open)
        {
            m.WakeUp();
            m.Hop(0.8);
            m.Bubble = null;
            bubble = null;
            menu = new List<Choice>();
            Header(I18n.Fmt("\u00A1Estrenamos Stackshot {0}! \u00BFTe cuento qu\u00E9 trae?", "Stackshot {0} is here! Want to hear what's new?", version) + (headline.Length > 0 ? I18n.Fmt(" Por ejemplo: \u00AB{0}\u00BB.", " For example: \u201C{0}\u201D.", headline) : ""));
            Add("sparkle", I18n.T("Ver las novedades", "See what's new"), null, open);
            Add("clock", I18n.T("Luego", "Later"), null, delegate { m.Say(I18n.T("Cuando quieras: Inicio, Acerca de, Novedades.", "Whenever you like: Home, About, What's new."), 3600); });
            menuAt = menuSeen = Anim.Now;
            menuGone = null;
            Halt();
            // Like a reminder, it stays while you work (clicks elsewhere don't close it) and goes after a minute.
            menuSticky = true;
            menuArmed = false;
            menuRect = RectangleF.Empty;
            menuHot = -1;
            nextIdea = Anim.Now + 8000;
        }

        // ---- Focus mode: the questions at each change of phase, what it says, and the clock above its head.

        public void FocusSay(string text)
        {
            if (menu != null && menuFocus) CloseMenu();
            m.WakeUp();
            m.Say(text, 3800);
        }

        // A block is done: its break (with the healthy break that is due, if any), another block, or the end.
        public void FocusAskWork(string levelUp)
        {
            FocusClock f = owner.FocusState;
            string today = owner.FocusToday();
            m.Perform(MascotCmd.Cheer);
            FocusPrompt(I18n.Fmt("\u00A1Bloque {0} de {1} hecho!", "Block {0} of {1} done!", f.Block, f.Rounds) + (today != null ? " " + today + "." : "") + (levelUp != null ? " " + levelUp : ""));
            AddFocusChoices(f);
        }

        // A break is over: the next block, a few more minutes, or the end.
        public void FocusAskBreak()
        {
            FocusClock f = owner.FocusState;
            int next = f.Block >= f.Rounds || f.LongBreak ? 1 : f.Block + 1;
            m.Perform(MascotCmd.Wave);
            FocusPrompt(I18n.Fmt("Fin del descanso. \u00BFSeguimos con el bloque {0} de {1}?", "Break's over. Shall we go on with block {0} of {1}?", next, f.Rounds));
            AddFocusChoices(f);
        }

        // Back after being away in the middle of a block (the clock stopped when they left).
        public void FocusAskBack(string text)
        {
            m.Hop(1.0);
            FocusPrompt(text);
            AddFocusChoices(owner.FocusState);
        }

        void FocusPrompt(string header)
        {
            m.WakeUp();
            m.Bubble = null;
            bubble = null;
            menu = new List<Choice>();
            Header(header);
            menuAt = menuSeen = Anim.Now;
            menuGone = null;
            Halt();
            // It stays while you work (clicks elsewhere don't close it) and goes after a minute; the clock above its
            // head then says it is waiting, and a click on it brings the same choices back.
            menuSticky = menuFocus = true;
            menuArmed = false;
            menuRect = RectangleF.Empty;
            menuHot = -1;
            nextIdea = Anim.Now + 8000;
        }

        // ---- Health: what it tells about your day and your watch, in a card that waits while you work.

        public void HealthTell(string text, bool cheer)
        {
            if (cheer) m.Perform(MascotCmd.Cheer);
            FocusPrompt(text);
            menuFocus = false;
            Add("check", I18n.T("Gracias", "Thanks"), null, delegate { });
        }

        public void HealthAskMove(string text)
        {
            FocusPrompt(text);
            menuFocus = false;
            Add(WellKind.Glyphs[WellKind.Move], I18n.T("Estiramos las piernas", "Let's stretch our legs"), null, delegate { StartSession(WellKind.Move); });
            Add("clock", I18n.T("Luego", "Later"), null, delegate { m.Say(I18n.T("Vale, luego nos movemos.", "OK, we'll move later."), 2600); });
        }

        // Close to the day's goal: a real walk (the watch is read again when you are back) or not now.
        public void HealthAskWalk(string text)
        {
            FocusPrompt(text);
            menuFocus = false;
            Add("walk", I18n.T("Voy a dar un paseo", "I'll go for a walk"), null, delegate
            {
                owner.WalkStarted();
                m.Perform(MascotCmd.Cheer);
                m.Say(I18n.T("\u00A1Genial! Cuando vuelvas te digo cu\u00E1ntos pasos has dado.", "Great! When you're back I'll tell you how many steps you took."), 3600);
            });
            Add("clock", I18n.T("Ahora no", "Not now"), null, delegate { m.Say(I18n.T("Vale. Si luego te apetece, aqu\u00ED estoy.", "OK. If you feel like it later, I'm here."), 2600); });
        }

        static readonly string[] OpeningEs = { "\u00A1Marchando!", "\u00A1Te lo abro!", "\u00A1Ah\u00ED va!", "\u00A1Hecho!" };
        static readonly string[] OpeningEn = { "Coming right up!", "Opening it!", "Here it goes!", "Done!" };
        static string[] Opening { get { return I18n.IsSpanish ? OpeningEs : OpeningEn; } }

        void OpenLink(QuickLink l)
        {
            string why = owner.OpenLink(l);
            if (why != null) { m.Say(why, 3600); return; }
            if (settings.MascotTalks) m.Say(Opening[rnd.Next(Opening.Length)] + I18n.Fmt(" \u00AB{0}\u00BB", " \u201C{0}\u201D", l.Name), 2200);
        }

        // "Mi actividad": the watch is read now and the mascot tells how the day goes.
        void TellActivity()
        {
            m.Say(I18n.T("Miro tu reloj\u2026", "Checking your watch\u2026"), 2200);
            Action tell = delegate
            {
                if (IsDisposed || !Visible) return;
                GoogleHealth.Today w = owner.Watch;
                HealthTell(owner.ActivityLine(), w != null && !w.Empty && w.Steps >= w.StepsGoal);
            };
            // A reading already under way: told when it ends.
            if (owner.WatchReading)
            {
                Action once = null;
                once = delegate { owner.WatchRead -= once; tell(); };
                owner.WatchRead += once;
                return;
            }
            owner.ReadWatch(delegate { tell(); }, true);
        }

        // "Haz un truco": only real tricks (no props, no just talking), never one of the last two again.
        static readonly int[] TrickSet = { MascotCmd.Juggle, MascotCmd.Dance, MascotCmd.Jump, MascotCmd.Twirl, MascotCmd.Flip, MascotCmd.Cheer, MascotCmd.Bubbles };
        int lastTrick = -1, lastTrick2 = -1;

        void DoTrick()
        {
            // A dragon often shows its own power instead.
            if (m.HasPower && rnd.NextDouble() < 0.4) { m.WakeUp(); m.Power(); return; }
            int t;
            do t = TrickSet[rnd.Next(TrickSet.Length)]; while (t == lastTrick || t == lastTrick2);
            lastTrick2 = lastTrick;
            lastTrick = t;
            m.WakeUp();
            m.Perform(t);
        }

        // ---- Assistant: reminders when they are due, a SOAP fault that arrived, and things dropped on it.

        public void ReminderCard(string text, Action<int> later)
        {
            m.Perform(MascotCmd.Wave);
            FocusPrompt(I18n.T("Recordatorio: ", "Reminder: ") + text);
            menuFocus = false;
            Add("check", I18n.T("Hecho", "Done"), null, delegate { m.Hop(0.8); });
            Add("clock", I18n.T("Recu\u00E9rdamelo en 10 min", "Remind me in 10 min"), null, delegate { later(10); m.Say(I18n.T("Vale, en 10 minutos.", "OK, in 10 minutes."), 2600); });
            Add("clock", I18n.T("En 1 hora", "In 1 hour"), null, delegate { later(60); m.Say(I18n.T("Vale, dentro de una hora.", "OK, in an hour."), 2600); });
        }

        public void FaultCard(string text, Action open)
        {
            m.Hop(1.0);
            FocusPrompt(text);
            menuFocus = false;
            Add("open", I18n.T("Abrir el XML", "Open the XML"), null, open);
            Add("check", I18n.T("Vale", "OK"), null, delegate { });
        }

        static bool Droppable(IDataObject d)
        {
            if (d == null) return false;
            if (d.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = d.GetData(DataFormats.FileDrop) as string[];
                if (files != null) foreach (string f in files) if (ShotStack.CanTake(f)) return true;
                return false;
            }
            return d.GetDataPresent(DataFormats.UnicodeText) || d.GetDataPresent(DataFormats.Text);
        }

        bool dragHinted;

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = Droppable(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
            if (e.Effect == DragDropEffects.None || dragHinted) return;
            dragHinted = true;
            m.WakeUp();
            m.Hop(0.7);
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            string[] dropped = files ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;
            bool folder = dropped != null && dropped.Length > 0 && System.IO.Directory.Exists(dropped[0]);
            m.Say(folder ? I18n.T("\u00A1Su\u00E9ltala y la guardo en mis accesos r\u00E1pidos!", "Drop it and I'll save it in my shortcuts!") : files ? I18n.T("\u00A1Su\u00E9ltalo aqu\u00ED y te lo abro!", "Drop it here and I'll open it!") : I18n.T("\u00A1Su\u00E9ltalo y lo apunto en tus notas!", "Drop it and I'll add it to your notes!"), 2000);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            e.Effect = Droppable(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            dragHinted = false;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            dragHinted = false;
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    // Opened once this drop is over (Explorer waits for it to return).
                    if (files != null) BeginInvoke((Action)delegate
                    {
                        string said = owner.OpenDropped(files);
                        if (said != null) { m.Perform(MascotCmd.Jump); m.Say(said, 2600); }
                    });
                    return;
                }
                string text = (e.Data.GetData(DataFormats.UnicodeText) ?? e.Data.GetData(DataFormats.Text)) as string;
                if (!string.IsNullOrEmpty(text) && owner.AddNote(text)) { m.Perform(MascotCmd.Read); m.Say(I18n.T("Apuntado en tus notas.", "Added to your notes."), 2600); }
            }
            catch (Exception ex) { ShotStack.Log("Soltar en la mascota: " + ex.Message); }
        }

        // Once: whether there is a watch to connect (asked again three days after "Luego").
        public void WatchOffer(string text, Action connect, Action never)
        {
            m.Perform(MascotCmd.Wave);
            FocusPrompt(text);
            menuFocus = false;
            Add("google", I18n.T("Conectar mi reloj", "Connect my watch"), null, delegate { connect(); m.Say(I18n.T("Te abro el inicio de sesi\u00F3n en el navegador.", "Opening the sign-in page in your browser."), 3600); });
            Add("clock", I18n.T("Luego", "Later"), null, delegate { m.Say(I18n.T("Vale, otro d\u00EDa te lo vuelvo a preguntar.", "OK, I'll ask you again another day."), 3000); });
            Add("close", I18n.T("No tengo reloj", "I don't have a watch"), null, delegate { never(); m.Say(I18n.T("\u00A1Entendido! Sin reloj tambi\u00E9n te acompa\u00F1o.", "Got it! I'll keep you company without a watch too."), 3000); });
        }

        void DrinkOne()
        {
            string up;
            int n = owner.WaterPlusOne(out up);
            m.Perform(MascotCmd.Water);
            m.Say(MascotTalk.WellCount(WellKind.Water, n, settings.WaterGoal) + (up != null ? " " + up : ""), 3600);
        }

        void TellDay()
        {
            string day = owner.DaySummary();
            HealthTell(day != null ? day : I18n.T("Todav\u00EDa no ha pasado nada que contar hoy. \u00A1Vamos all\u00E1!", "Nothing to tell about today yet. Let's go!"), false);
        }

        // What can be done now, for the questions and the top of its menu.
        void AddFocusChoices(FocusClock f)
        {
            if (f.Asking && f.Now == FocusClock.Phase.Work)
            {
                bool longBreak = f.Block >= f.Rounds;
                Add("clock", (longBreak ? I18n.Fmt("Descanso largo de {0}", "{0} long break", FocusClock.Minutes(settings.FocusLong)) : I18n.Fmt("Descanso de {0}", "{0} break", FocusClock.Minutes(settings.FocusShort))), null, owner.FocusBreak);
                // The healthy break that is due fits in the break: started together.
                int well = owner.WellDueKind();
                if (well >= 0) Add(WellKind.Glyphs[well], I18n.Fmt("{0} conmigo", "{0} with me", WellKind.Names[well]), null, delegate { owner.FocusBreak(); int k = well; BeginInvoke((Action)delegate { StartSession(k); }); });
                Add("play", I18n.T("Otro bloque sin descanso", "Another block, no break"), null, delegate { owner.FocusWork(false); });
                Add("close", I18n.T("Terminar por hoy", "Done for today"), null, owner.FocusStop);
            }
            else if (f.Asking)
            {
                Add("play", I18n.T("Empezar el bloque", "Start the block"), null, delegate { owner.FocusWork(false); });
                Add("clock", I18n.T("5 minutos m\u00E1s de descanso", "5 more minutes of break"), null, delegate { owner.FocusMore(5); });
                Add("close", I18n.T("Terminar por hoy", "Done for today"), null, owner.FocusStop);
            }
            else if (f.Paused)
            {
                Add("play!", f.Now == FocusClock.Phase.Work ? I18n.T("Seguir donde lo dej\u00E9", "Pick up where I left off") : I18n.T("Seguir con el descanso", "Go on with the break"), null, owner.FocusResume);
                if (f.Now == FocusClock.Phase.Work) Add("update", I18n.T("Empezar el bloque de nuevo", "Restart the block"), null, delegate { owner.FocusWork(true); });
                Add("close", I18n.T("Terminar la concentraci\u00F3n", "End focus"), null, owner.FocusStop);
            }
            else
            {
                Add("pause", I18n.T("Pausar", "Pause"), null, owner.FocusPause);
                if (f.Now == FocusClock.Phase.Break) Add("play", I18n.T("Saltar el descanso", "Skip the break"), null, delegate { owner.FocusWork(false); });
                Add("close", I18n.T("Terminar la concentraci\u00F3n", "End focus"), null, owner.FocusStop);
            }
        }

        // While a block runs it stays where it is and reads now and then, without talking.
        void FocusIdea(double now)
        {
            nextIdea = now + 35000 + rnd.NextDouble() * 25000;
            if (state == State.Idle && session < 0) m.Perform(MascotCmd.Read);
        }

        // The clock above its head while focus mode is on; the bubble and the menu go over it.
        void PillParts(FocusClock f, DateTime now, out string time, out string label, out Color tone)
        {
            if (f.Asking)
            {
                time = "";
                label = f.Now == FocusClock.Phase.Work ? I18n.T("\u00A1Bloque hecho! T\u00F3came", "Block done! Tap me") : I18n.T("\u00BFSeguimos? T\u00F3came", "Shall we go on? Tap me");
                tone = Color.FromArgb(10, 132, 255);
                return;
            }
            time = FocusClock.Clock(f.Left(now));
            label = f.Paused ? I18n.T("En pausa", "Paused") : f.Now == FocusClock.Phase.Break ? (f.LongBreak ? I18n.T("Descanso largo", "Long break") : I18n.T("Descanso", "Break")) : I18n.T("Concentraci\u00F3n", "Focus");
            tone = f.Paused ? Color.FromArgb(160, 160, 170) : f.Now == FocusClock.Phase.Break ? Color.FromArgb(52, 199, 89) : Color.FromArgb(255, 159, 10);
        }

        string pillLabelFor;
        float pillLabelW, pillTimeW, pillMeasuredAt;
        const TextFormatFlags PillFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        RectangleF PillLayout(Graphics g, string time, string label)
        {
            // Measured as the system draws them (PillText).
            if (label != pillLabelFor || s != pillMeasuredAt)
            {
                pillLabelFor = label;
                pillLabelW = TextRenderer.MeasureText(label, Fonts.Get(Mac.TextFont, 12 * s), Size.Empty, PillFlags).Width;
            }
            // The time takes the width of its widest digits, so the pill doesn't twitch every second.
            if (pillTimeW == 0 || s != pillMeasuredAt) pillTimeW = TextRenderer.MeasureText("00:00", Fonts.Get(Fonts.DisplaySemibold, 14 * s), Size.Empty, PillFlags).Width;
            pillMeasuredAt = s;
            float tw = time.Length == 0 ? 0 : (time.Length > 5 ? pillTimeW * 1.4f : pillTimeW) + 7 * s;
            float w = 10 * s + 16 * s + 8 * s + tw + pillLabelW + 13 * s, h = 30 * s;
            float x = ClampX(MidX - w / 2, w), y = m.Box.Y - D * 0.38f - h;
            return new RectangleF((float)Math.Round(x), (float)Math.Round(y), (float)Math.Ceiling(w), (float)Math.Ceiling(h));
        }

        void PaintPill(Graphics g)
        {
            FocusClock f = owner.FocusState;
            DateTime now = DateTime.UtcNow;
            string time, label;
            Color tone;
            PillParts(f, now, out time, out label, out tone);
            RectangleF r = PillLayout(g, time, label);
            using (GraphicsPath p = Theme.Round(r, r.Height / 2))
            {
                RectangleF sh = r;
                sh.Offset(0, 3 * s);
                using (GraphicsPath sp = Theme.Round(sh, r.Height / 2)) MascotParts.FillSolid(g, sp, Color.FromArgb(60, 0, 0, 0));
                MascotParts.FillSolid(g, p, Color.FromArgb(245, 52, 52, 60));
                MascotParts.Stroke(g, p, Color.FromArgb(40, 255, 255, 255), 1);
            }
            // A ring that fills as the phase goes by; two bars inside it while paused, a dot while it waits.
            float rs = 16 * s;
            RectangleF ring = new RectangleF(r.X + 10 * s, r.Y + (r.Height - rs) / 2, rs, rs);
            using (Pen track = new Pen(Color.FromArgb(70, 255, 255, 255), 2.6f * s)) g.DrawEllipse(track, ring);
            if (f.Asking)
            {
                float d = 7 * s;
                using (SolidBrush dot = new SolidBrush(tone)) g.FillEllipse(dot, ring.X + (rs - d) / 2, ring.Y + (rs - d) / 2, d, d);
            }
            else
            {
                float sweep = (float)(360 * f.Progress(now));
                if (sweep > 0.5f)
                    using (Pen arc = new Pen(tone, 2.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(arc, ring, -90, sweep);
                if (f.Paused)
                    using (SolidBrush bars = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                    {
                        g.FillRectangle(bars, ring.X + rs * 0.34f, ring.Y + rs * 0.3f, rs * 0.11f, rs * 0.4f);
                        g.FillRectangle(bars, ring.X + rs * 0.55f, ring.Y + rs * 0.3f, rs * 0.11f, rs * 0.4f);
                    }
            }
            // Its time and label, drawn sharp once a second (when the time changes) and copied in between.
            Bitmap text = PillText(time, label, f.Asking, r, ring.Right + 8 * s);
            CompositingMode mode = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(text, new Rectangle(pillTextAt.X + (int)r.X, pillTextAt.Y + (int)r.Y, text.Width, text.Height), 0, 0, text.Width, text.Height, GraphicsUnit.Pixel);
            g.CompositingMode = mode;
        }

        Bitmap pillText;
        string pillTextFor;
        Point pillTextAt;   // where it goes, from the pill's top-left

        Bitmap PillText(string time, string label, bool asking, RectangleF r, float left)
        {
            string key = time + "|" + label + "|" + asking + "|" + s + "|" + r.Size + "|" + (left - r.X);
            if (pillText != null && pillTextFor == key) return pillText;
            Font tf = Fonts.Get(Fonts.DisplaySemibold, 14 * s), lf = Fonts.Get(Mac.TextFont, 12 * s);
            // Only round the text, inside the pill's round ends.
            int lineH = Math.Max(TextRenderer.MeasureText("0", tf, Size.Empty, PillFlags).Height, TextRenderer.MeasureText("0", lf, Size.Empty, PillFlags).Height) + 2;
            int x0 = (int)Math.Round(left - r.X) - 1, y0 = (int)Math.Round((r.Height - lineH) / 2), x1 = (int)Math.Round(r.Width - 13 * s) + 2;
            Bitmap b = new Bitmap(Math.Max(1, x1 - x0), Math.Max(1, lineH), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
                SharpText(g, new Rectangle(0, 0, b.Width, b.Height), CardOnShadow, delegate(Graphics lg, Rectangle lr, Color under)
                {
                    int x = 1;
                    if (time.Length > 0)
                    {
                        TextRenderer.DrawText(lg, time, tf, new Rectangle(x, 0, lr.Width - x, lr.Height), Over(Color.White, under), PillFlags | TextFormatFlags.VerticalCenter);
                        x += (int)Math.Round((time.Length > 5 ? pillTimeW * 1.4f : pillTimeW) + 7 * s);
                    }
                    TextRenderer.DrawText(lg, label, lf, new Rectangle(x, 0, Math.Max(1, lr.Width - x), lr.Height), Over(asking ? Color.White : Color.FromArgb(190, 255, 255, 255), under),
                                          PillFlags | TextFormatFlags.VerticalCenter);
                });
            if (pillText != null) pillText.Dispose();
            pillText = b;
            pillTextFor = key;
            pillTextAt = new Point(x0, y0);
            return b;
        }

        void Header(string text)
        {
            Choice c = new Choice();
            c.Header = true;
            c.Text = text;
            menu.Add(c);
        }

        void StartSession(int kind)
        {
            CloseMenu();
            session = kind;
            sessionReps = 0;
            sessionStretch = kind == WellKind.Move && owner.MoveIsStretch;
            m.Guided = true;
            StartRep();
            nextIdea = double.MaxValue;
        }

        // One run of the activity (the back stretch is three), with its line in the bubble from the first frame.
        void StartRep()
        {
            m.Perform(SessionCmd());
            sessionSerial = m.ActivitySerial;
            m.Say(SessionLine(0), 1500);
        }

        // Cut short from outside (hidden by a full-screen app, the screen locked): it doesn't count.
        void StopSession()
        {
            if (session < 0) return;
            m.WakeUp();
        }

        int SessionCmd()
        {
            switch (session)
            {
                case WellKind.Water: return MascotCmd.Water;
                case WellKind.Eyes: return MascotCmd.EyeRest;
                case WellKind.Move: return sessionStretch ? MascotCmd.Stretch : MascotCmd.Legs;
                default: return MascotCmd.Breathe;
            }
        }

        // What the bubble says while you do it; the break ends with the activity (or when something interrupts it).
        void SessionTick(double now)
        {
            if (m.Activity != MascotCmd.PropOf(SessionCmd()) || m.ActivitySerial != sessionSerial)
            {
                // The mascot knows whether it got to the end or something cut it short (a click, a drag, being woken).
                bool whole = m.Completed(sessionSerial);
                if (whole && sessionStretch && sessionReps < 2 && state == State.Idle && !m.Dangling) { sessionReps++; StartRep(); return; }
                int k = session;
                session = -1;
                m.Guided = false;
                nextIdea = now + 6000;
                if (whole) Finish(k, false);
                else { owner.WellLater(k, 600000); m.Say(I18n.T("Lo dejamos para dentro de 10 minutos.", "Let's leave it for 10 minutes."), 2600); }
                return;
            }
            // Said again every frame, so a line that lasts longer than the bubble never blinks.
            m.Say(SessionLine(m.ActivityMs), 1500);
        }

        string SessionLine(double t)
        {
            string line;
            switch (session)
            {
                case WellKind.Water: line = t < 2500 ? I18n.T("\u00A1Un vaso de agua! Bebe conmigo.", "A glass of water! Drink with me.") : I18n.T("Sorbo a sorbo\u2026", "Sip by sip\u2026"); break;
                case WellKind.Eyes:
                {
                    int left = (int)Math.Ceiling(Math.Max(0, 20000 - t) / 1000);
                    line = left > 0 ? I18n.T("Mira algo lejano, a m\u00E1s de 6 metros\u2026 ", "Look at something far away, over 20 feet\u2026 ") + left : I18n.T("\u00A1Listo!", "Done!");
                    break;
                }
                case WellKind.Move:
                    line = sessionStretch ? I18n.T("Brazos arriba y estira la espalda\u2026 ", "Arms up and stretch your back\u2026 ") + (sessionReps + 1) + I18n.T(" de 3", " of 3")
                                          : I18n.T("Sentadilla ", "Squat ") + Math.Min(8, (int)(Math.Max(0, t - 200) / MascotProps.SquatMs) + 1) + I18n.T(" de 8", " of 8");
                    break;
                default:
                {
                    int n = Math.Min(6, (int)(Math.Max(0, t - 200) / MascotProps.BreathMs) + 1);
                    line = (MascotProps.Inhaling(t) ? I18n.T("Inspira despacio por la nariz\u2026 ", "Breathe in slowly through your nose\u2026 ") : I18n.T("Suelta el aire poco a poco\u2026 ", "Let the air out little by little\u2026 ")) + n + I18n.T(" de 6", " of 6");
                    break;
                }
            }
            return line;
        }

        // A break done (with the mascot, or on your own): counted for today, a little celebration, and the news if
        // the friendship went up a level with it.
        void Finish(int kind, bool alone)
        {
            string levelUp;
            int today = owner.WellDone(kind, out levelUp);
            if (alone) m.Perform(MascotCmd.Cheer);
            m.Celebrate(null);
            // Always said, chatty or not: it is the answer to what the user chose.
            string line = settings.MascotTalks ? MascotTalk.WellDone(settings, kind, today) : MascotTalk.WellCount(kind, today, settings.WaterGoal);
            m.Say(line + (levelUp != null ? " " + levelUp : ""), 4600);
        }

        // The desktop mascot stays out even with the main window open: it is switched on from there, and hiding it
        // then made it look broken.
        public void SetHomeShown(bool shown)
        {
            homeShown = shown;
        }

        static int IdleMs() { return Native.IdleMs(); }

        // The monitor a full-screen app fills now (a game, a presentation, a remote desktop or a virtual machine), or null;
        // always null when asked to stay in front.
        Screen BusyScreen()
        {
            if (!settings.MascotHideFull) { fullWin = IntPtr.Zero; return null; }
            int q;
            // QUNS_BUSY (2) and QUNS_RUNNING_D3D_FULL_SCREEN (3; virtual machines and remote desktops often say so too):
            // the foreground window must cover its whole monitor. QUNS_PRESENTATION_MODE (4): the monitor it is shown on.
            try
            {
                IntPtr fg = Native.GetForegroundWindow();
                if (SHQueryUserNotificationState(out q) == 0 && q >= 2 && q <= 4 && fg != IntPtr.Zero)
                {
                    if (q == 4) { fullWin = fg; return Screen.FromHandle(fg); }
                    if (Fills(fg)) { fullWin = fg; return Screen.FromHandle(fg); }
                }
                // Windows only says so while that app is in front: a click on another monitor leaves it filling its own,
                // so the window found last still counts while it is there, shown and full screen.
                if (fullWin != IntPtr.Zero && IsWindowVisible(fullWin) && !Native.IsIconic(fullWin) && Fills(fullWin)) return Screen.FromHandle(fullWin);
            }
            catch { }
            fullWin = IntPtr.Zero;
            return null;
        }

        IntPtr fullWin;               // the full-screen window found last

        static bool Fills(IntPtr w)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(w, out r)) return false;
            Rectangle b = Screen.FromHandle(w).Bounds;
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }

        string busyName;              // the monitor a full-screen app fills, or null
        bool behind;                  // below that app: its only monitor is taken

        // Below a full-screen app it is not "on show": what it would say goes to a notice, reminders wait.
        public bool Behind { get { return behind; } }

        bool Busy(Screen sc) { return busyName != null && sc.DeviceName == busyName; }

        // Every second and a half: a full-screen app on its monitor sends it to another one that is free (where it pops
        // in), or, with none, behind that app until it ends.
        void CheckFullScreen()
        {
            Screen busy = BusyScreen();
            string name = busy != null ? busy.DeviceName : null;
            bool changed = name != busyName;
            busyName = name;
            bool here = busy != null && cur != null && cur.DeviceName == busy.DeviceName;
            if (!here) { SetBehind(false, changed); return; }
            if (state == State.Drag) return;
            Screen free = null;
            foreach (Screen sc in Screen.AllScreens)
                if (sc.DeviceName != busy.DeviceName && (free == null || sc.Primary)) free = sc;
            if (free == null) { SetBehind(true, changed); return; }
            SetBehind(false, false);
            CloseMenu();
            perch = IntPtr.Zero;
            leaving = false;
            ventDone = 0;
            m.EndVent();
            slide = vx = vy = 0;
            Rectangle fw = free.WorkingArea;
            Place(free, fw.Left + fw.Width * Math.Max(0.05, Math.Min(0.95, settings.MascotSpot / 1000.0)));
            by = Ground;
            targetX = bx;
            state = State.Idle;
            nextIdea = Anim.Now + 4000;
            m.PopIn();
            ShotStack.Log("Mascota: se cambia de pantalla por una pantalla completa");
        }

        void SetBehind(bool on, bool log)
        {
            if (on == behind) return;
            behind = on;
            if (on)
            {
                CloseMenu();
                // Out of the always-on-top band and right under the app that fills the screen.
                Native.SetWindowPos(Handle, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // NOTOPMOST
                Native.SetWindowPos(Handle, fullWin != IntPtr.Zero ? fullWin : Native.GetForegroundWindow(), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                ShotStack.Log("Mascota: se queda detr\u00E1s de una pantalla completa");
            }
            else
            {
                Native.SetWindowPos(Handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // TOPMOST again
                ShotStack.Log("Mascota: vuelve delante");
            }
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
            if (spotDue > 0 && now >= spotDue) SaveSpot();

            if (now - lastCheck > 1500)
            {
                lastCheck = now;
                CheckFullScreen();
                if (Visible && state != State.Drag)
                {
                    // Work area changed (taskbar resized or auto-hidden): follow it. Then make sure it is still on top.
                    Screen cur = Screen.FromPoint(new Point((int)(bx + D / 2.0), (int)(by + D / 2.0)));
                    if (cur.WorkingArea != wa) Place(cur, bx + D / 2.0);
                    if (KeepOnTop && !behind && ForeignAbove())
                        Native.SetWindowPos(Handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // TOPMOST, NOSIZE | NOMOVE | NOACTIVATE
                }
                // Naps while the user is away (if it may doze off at all), only once it stands still.
                bool atRest = state == State.Idle && menu == null && !leaving && slide == 0 && clicks == 0 && !down && session < 0;
                if (MascotRate.AwayNap(IdleMs(), settings.MascotSleepIdle, atRest, asleepForAway)) { asleepForAway = true; m.SleepNow(); }
            }
            if (MascotRate.ScreenOff) MascotScreen.Check();
            // Frozen only in a real nap: woken meanwhile (a reminder, the news, a XML with a fault) it keeps drawing.
            bool napping = asleepForAway && m.Sleeping && state == State.Idle && menu == null && m.Bubble == null && !leaving;
            if (!Visible || MascotRate.ScreenOff || (napping && MascotRate.Frozen(IdleMs(), true))) { StopSession(); SetFast(false); SetInterval(MascotRate.Hidden); return; }

            Point mouse = Control.MousePosition;
            bool near = Math.Abs(mouse.X - (bx + D / 2.0)) < 360 * s && Math.Abs(mouse.Y - (by + D / 2.0)) < 300 * s;
            bool moved = near && mouse != lastMouse;
            // Only a move that turns its eyes keeps the faster rate; past their limit the picture stays the same.
            if (moved && m.GazeMoves(new PointF(lastMouse.X - wx, lastMouse.Y - wy), new PointF(mouse.X - wx, mouse.Y - wy))) lastNear = now;
            lastMouse = mouse;
            // Back from being away: wakes on the first input, not at the next check.
            if (asleepForAway && MascotRate.AwayWake(true, IdleMs(), moved)) { asleepForAway = false; m.WakeUp(); m.Hop(1.2); }

            if (menu != null)
            {
                // Close the bubble when the user clicks somewhere else or ignores it for a while.
                bool inside = menuRect.Contains(mouse.X - wx, mouse.Y - wy) || new RectangleF((float)bx, (float)by, D, D).Contains(mouse);
                if (inside) menuSeen = now;
                // Only a click that starts after the menu is up closes it (not a button already held when it appeared).
                // A reminder ignores clicks elsewhere, since you are working, and stays a minute (it is answered later
                // anyway if nobody does); the quick menu stays 9 s.
                bool pressed = Control.MouseButtons != MouseButtons.None;
                if (!pressed) menuArmed = true;
                bool sticky = menuWell >= 0 || menuSticky;
                if ((pressed && menuArmed && !inside && !sticky) || now - menuSeen > (sticky ? 60000 : 9000)) CloseMenu();
            }

            FollowPerch();
            CheckGround();
            m.Busy = state != State.Idle || menu != null || leaving || clicks > 0;
            // While a block runs it keeps you company quietly: no chatter, no dances, only reading now and then.
            bool focusing = owner.FocusState.Working;
            m.Talks = settings.MascotTalks && !focusing;
            m.Energy = focusing ? 0 : settings.MascotEnergy;
            m.Chatter = settings.MascotChatter;
            m.Alone = focusing ? 0 : settings.MascotAlone;
            m.SleepIdle = settings.MascotSleepIdle;
            m.Rhythm = settings.MascotRhythm;
            MascotParts.NightCaps = settings.MascotRhythm;
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
                    if ((menu != null || menuDue > 0) && !leaving) { Halt(); break; }
                    // At most a 30 fps frame's worth: after a hiccup it carries on from where it was instead of leaping ahead.
                    double step = (leaving ? 160 : Math.Abs(targetX - bx) > 600 * s ? 150 : 75) * s * Math.Min(dt, 1 / 30.0);
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
                        bx += slide * Math.Min(dt, 1 / 30.0);
                        slide *= Math.Exp(-7 * dt);
                        if (bx < lo || bx > hi) { bx = Math.Max(lo, Math.Min(hi, bx)); slide = 0; }
                        targetX = bx;
                        if (Math.Abs(slide) <= 6 * s) { slide = 0; RememberSpot(true); }
                    }
                    else slide = 0;
                    if (leaving) { WalkOff(); break; }
                    if (session >= 0) SessionTick(now);
                    else if (menu == null && !m.Sleeping && now > nextIdea) { if (focusing) FocusIdea(now); else Idea(now); }
                    if (m.Sleeping && !asleepForAway && now > nextIdea + 120000) { m.WakeUp(); Idea(now); }
                    break;
            }
            // Leans (and stretches a little) toward where it is going, held by the mouse or in the air after a toss.
            double lean = state == State.Walk ? dir * 0.8 : state == State.Drag ? dragVx / (650 * s) : state == State.Fall && thrown ? vx / (1100 * s) : 0;
            double pull = state == State.Drag ? Math.Sqrt(dragVx * dragVx + dragVy * dragVy) / (2000 * s) : state == State.Fall ? Math.Abs(vy) / (Terminal * s) * 0.6 : 0;
            m.Facing = Math.Max(-1.5, Math.Min(1.5, lean));
            m.Walking = state == State.Walk;
            m.Airborne = state == State.Drag || state == State.Fall;
            dragPull += (Math.Min(1, pull) - dragPull) * (1 - Math.Exp(-dt / 0.08));
            m.Pull = dragPull;
            m.Dangling = state == State.Drag;
            m.ShowShadow = state != State.Drag && state != State.Fall;

            PlaceWindow();
            m.Step(now, new PointF(mouse.X - wx, mouse.Y - wy), moved);
            // While it only walks, the picture is redrawn at about 30 fps whatever the display's rate (every other frame at
            // 60 Hz, every fourth at 144) and the window still moves every frame. Against a screen edge the window stops and
            // the mascot walks inside it: then every frame is drawn, or it would stand still and jump.
            bool still = state == State.Walk && fast && menu == null && bubble == null && m.Bubble == null && !lastFull && now - lastWalkPaint < WalkPaintMs &&
                         Math.Abs(m.Box.X - walkPaintX) < 1;
            if (still) MoveOnly();
            else { Render(now); lastWalkPaint = now; walkPaintX = m.Box.X; }

            // The menu opening or closing runs at the display's rate too, so it glides instead of stepping.
            bool menuMoving = (menu != null && now - menuAt < MenuInMs + 200) || (menuGone != null && now - menuGoneAt < MenuOutMs + 40);
            bool wantFast = state == State.Drag || state == State.Fall || state == State.Walk || slide != 0 || menuMoving;
            SetFast(wantFast);
            // Napping while the user is away nobody watches it: a slow clock (it wakes on the first input all the same).
            SetInterval(asleepForAway && !wantFast && menu == null ? MascotRate.Background : MascotRate.Pet(false, wantFast, menu != null, m.Lively, now - lastNear < MascotRate.NearMs));
        }

        double lastNear = -1e9;

        void SetInterval(int ms)
        {
            if (timer.Interval != ms) timer.Interval = ms;
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
                    if (ow != wa && !Busy(os))
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
                // Only what changes is cleared and painted: the mascot and its bubble or menu, where they are now and
                // where they were last frame (a reminder card or a minute-long breathing bubble never repaints the
                // whole window).
                RectangleF pb = m.PaintBounds, ui = UiRect(g);
                if (!lastPaint.IsEmpty)
                {
                    RectangleF clip = RectangleF.Union(pb, lastPaint);
                    if (!ui.IsEmpty) clip = RectangleF.Union(clip, ui);
                    if (!lastUi.IsEmpty) clip = RectangleF.Union(clip, lastUi);
                    clip.Inflate(2, 2);
                    g.SetClip(clip);
                }
                lastPaint = pb;
                lastUi = ui;
                lastFull = !ui.IsEmpty;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                m.Paint(g);
                // What it just drew is in this thread's part cache from now on (MascotWarm doesn't draw it again).
                if (m.Look != seenLook || D != seenSize) { seenLook = m.Look; seenSize = D; MascotWarm.Seen(seenLook, D); }
                if (menu != null) PaintMenu(g, now, menu, menuRect, menuHot, false);
                else if (menuGone != null) PaintMenu(g, now, menuGone, menuGoneRect, -1, true);
                else if (bubble != null && m.BubbleAlpha >= 0.02) PaintBubble(g);
                else if (owner.FocusState.On) PaintPill(g);
            }
            Native.GdiFlush();
            // Only the mascot's box, and the bubble or menu if any, goes to the screen (a fraction of the window).
            RectangleF show = lastUi.IsEmpty ? lastPaint : RectangleF.Union(lastPaint, lastUi);
            show.Inflate(4, 4);
            Rectangle crop = Rectangle.Intersect(Rectangle.Round(show), new Rectangle(0, 0, W, H));
            if (crop.Width < 1 || crop.Height < 1) crop = new Rectangle(0, 0, W, H);
            presentAt = crop.Location;
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            shownX = wx + crop.X; shownY = wy + crop.Y;
            Native.Present(Handle, dib.Dc, shownX, shownY, crop.Width, crop.Height, crop.X, crop.Y);
            if (Probe != null) { double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency; Probe((t1 - t0) * f, (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * f, crop.Width * crop.Height); }
        }

        // Optional frame timing hook (render ms, present ms, pixels sent); null in normal use.
        public static Action<double, double, int> Probe;

        RectangleF lastPaint, lastUi;
        MascotLook seenLook;         // the look last drawn, and its size: already in the part cache
        int seenSize;
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

        // Where the bubble or the menu goes this frame, with its shadow and arrow (Empty: none). Worked out before
        // painting, so only that area is redrawn.
        RectangleF UiRect(Graphics g)
        {
            if (menu != null)
            {
                RectangleF r = MenuLayout(g);
                return RectangleF.FromLTRB(r.Left - 15 * s, r.Top - 15 * s, r.Right + 15 * s, r.Bottom + 22 * s);
            }
            if (menuGone != null)
            {
                if (Anim.Now - menuGoneAt < MenuOutMs)
                {
                    RectangleF r = menuGoneRect;
                    return RectangleF.FromLTRB(r.Left - 15 * s, r.Top - 15 * s, r.Right + 15 * s, r.Bottom + 22 * s);
                }
                menuGone = null;
            }
            if (m.Bubble != null) bubble = m.Bubble;
            if (bubble != null && m.BubbleAlpha < 0.02 && m.Bubble == null) bubble = null;
            if (bubble == null || m.BubbleAlpha < 0.02)
            {
                // No bubble: the focus clock, if focus mode is on.
                if (!owner.FocusState.On) return RectangleF.Empty;
                FocusClock f = owner.FocusState;
                string time, label;
                Color tone;
                PillParts(f, DateTime.UtcNow, out time, out label, out tone);
                RectangleF pr = PillLayout(g, time, label);
                return RectangleF.FromLTRB(pr.Left - 2, pr.Top - 2, pr.Right + 2, pr.Bottom + 6 * s);
            }
            RectangleF b = BubbleLayout(g);
            return RectangleF.FromLTRB(b.Left - 2, b.Top - 2, b.Right + 2, b.Bottom + 10 * s);
        }

        RectangleF BubbleLayout(Graphics g)
        {
            Font f = Fonts.Get(Mac.TextFont, 13 * s);
            // Measured once per text, not every frame, as the system draws it (see BubbleCard).
            if (bubble != measuredFor)
            {
                Size sz = TextRenderer.MeasureText(bubble, f, new Size((int)(W - 60 * s), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                measured = new SizeF(sz.Width, sz.Height);
                measuredFor = bubble;
            }
            float w = measured.Width + 26 * s, h = measured.Height + 16 * s;
            float x = ClampX(MidX - w / 2, w), y = m.Box.Y - D * 0.38f - h;
            return new RectangleF((float)Math.Round(x), (float)Math.Round(y), (float)Math.Ceiling(w), (float)Math.Ceiling(h));
        }

        void PaintBubble(Graphics g)
        {
            if (bubble == null) return;
            double a = Math.Max(0, Math.Min(1, m.BubbleAlpha));
            if (a < 0.02) return;
            RectangleF r = BubbleLayout(g);
            Bitmap card = BubbleCard(bubble, r, MidX);
            Rectangle at = new Rectangle((int)r.X - CardPad, (int)r.Y - CardPad, card.Width, card.Height);
            if (a >= 0.999) g.DrawImage(card, at, 0, 0, card.Width, card.Height, GraphicsUnit.Pixel);
            else
                using (System.Drawing.Imaging.ImageAttributes fade = new System.Drawing.Imaging.ImageAttributes())
                {
                    fade.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)a });
                    g.DrawImage(card, at, 0, 0, card.Width, card.Height, GraphicsUnit.Pixel, fade);
                }
        }

        // The speech bubble as one picture with its text drawn sharp (SharpText); redrawn only when what it says, its
        // size or where its tail points change, and copied as it is on every other frame.
        Bitmap bubbleCard;
        string bubbleCardFor;
        SizeF bubbleCardSize;
        float bubbleCardTail, bubbleCardScale;

        Bitmap BubbleCard(string text, RectangleF r, float midX)
        {
            float tail = midX - r.X;
            if (bubbleCard != null && bubbleCardFor == text && bubbleCardSize == r.Size && bubbleCardTail == tail && bubbleCardScale == s) return bubbleCard;
            int pad = CardPad;
            Bitmap card = new Bitmap((int)Math.Ceiling(r.Width) + pad * 2, (int)Math.Ceiling(r.Height) + pad * 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            Color fill = Color.FromArgb(245, 52, 52, 60);
            using (Graphics g = Graphics.FromImage(card))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF b = new RectangleF(pad, pad, r.Width, r.Height);
                using (GraphicsPath p = Theme.Round(b, 12 * s))
                {
                    RectangleF sh = b;
                    sh.Offset(0, 3 * s);
                    using (GraphicsPath sp = Theme.Round(sh, 12 * s)) MascotParts.FillSolid(g, sp, Color.FromArgb(60, 0, 0, 0));
                    MascotParts.FillSolid(g, p, fill);
                    float tx = pad + tail;
                    PointF[] tp = { new PointF(tx - 7 * s, b.Bottom - 1), new PointF(tx, b.Bottom + 7 * s), new PointF(tx + 7 * s, b.Bottom - 1) };
                    using (SolidBrush tb = new SolidBrush(fill)) g.FillPolygon(tb, tp);
                    MascotParts.Stroke(g, p, Color.FromArgb(40, 255, 255, 255), 1);
                }
                Rectangle tr = new Rectangle(pad + (int)Math.Round(13 * s), pad + (int)Math.Round(8 * s), (int)Math.Ceiling(r.Width - 24 * s), (int)Math.Ceiling(r.Height - 12 * s));
                Font f = Fonts.Get(Mac.TextFont, 13 * s);
                SharpText(g, tr, CardOnShadow, delegate(Graphics lg, Rectangle lr, Color under)
                {
                    TextRenderer.DrawText(lg, text, f, lr, Over(Mac.Text, under), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                });
            }
            if (bubbleCard != null) bubbleCard.Dispose();
            bubbleCard = card;
            bubbleCardFor = text; bubbleCardSize = r.Size; bubbleCardTail = tail; bubbleCardScale = s;
            return card;
        }

        // The dark card of the bubble and the focus clock (245, 52, 52, 60) as it comes out over the soft shadow under it,
        // premultiplied as GDI+ blends them: what the text's layer has to match.
        static readonly Color CardOnShadow = Color.FromArgb(247, 52, 52, 60);

        // Text drawn by the system, sharp (ClearType, as in its own menus), on a card of colour bg at bg's opacity. ClearType
        // needs a solid background: draw puts the text on an opaque layer of the card's colour (lr: the layer, under: its
        // colour), which is then copied over the card at the card's opacity, so the two match exactly. r must lie on the
        // card's flat colour, clear of its rounded corners.
        static void SharpText(Graphics g, Rectangle r, Color bg, Action<Graphics, Rectangle, Color> draw)
        {
            if (r.Width < 1 || r.Height < 1) return;
            Color solid = Color.FromArgb(255, bg);
            using (Bitmap layer = new Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            {
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    lg.Clear(solid);
                    lg.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // the quality TextRenderer asks the system for
                    draw(lg, new Rectangle(0, 0, r.Width, r.Height), solid);
                }
                CompositingMode mode = g.CompositingMode;
                g.CompositingMode = CompositingMode.SourceCopy;
                using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = bg.A / 255f });
                    g.DrawImage(layer, r, 0, 0, r.Width, r.Height, GraphicsUnit.Pixel, ia);
                }
                g.CompositingMode = mode;
            }
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
            menu = new List<Choice>();
            // Focus mode on: where it stands and what can be done, first.
            FocusClock f = owner.FocusState;
            if (f.On)
            {
                Header(owner.FocusLine());
                AddFocusChoices(f);
                AddSep();
            }
            // Short on purpose: what gets used, in four groups (capture, links, notes and well-being, the app).
            Add("area", I18n.T("Capturar un \u00E1rea", "Capture an area"), Keys(settings.HotRegion), delegate { owner.Run("region"); });
            Add("video", Recorder.Recording ? I18n.T("Parar la grabaci\u00F3n", "Stop recording") : I18n.T("Grabar un v\u00EDdeo", "Record a video"), null, delegate { owner.Run("video"); });
            Add("textscan", I18n.T("Copiar texto", "Copy text"), Keys(settings.HotText), delegate { owner.Run("text"); });
            Add("folder", I18n.T("Abrir mis capturas", "Open my captures"), null, delegate { owner.OpenFolder(); });
            AddSep();
            // Quick links: each one opens with a word from it; the last row adds or tidies them.
            // A few fit above the mascot (fewer with the focus rows on top); the rest open from their window.
            List<QuickLink> links = owner.Links;
            int room = f.On ? 2 : 3;
            for (int i = 0; i < links.Count && i < room; i++)
            {
                QuickLink it = links[i];
                Add(it.Icon, it.Name, null, delegate { OpenLink(it); });
            }
            Add("plus", links.Count == 0 ? I18n.T("A\u00F1adir un acceso r\u00E1pido\u2026", "Add a quick link\u2026") : I18n.T("Accesos r\u00E1pidos\u2026", "Quick links\u2026"), links.Count > room ? (links.Count - room) + I18n.T(" m\u00E1s", " more") : null,
                delegate { LinksWindow.Show(owner); });
            AddSep();
            // What it helps with: reminders, notes, focus, breaks and the day.
            int pending = owner.Reminders.Count;
            Add("bell", I18n.T("Recu\u00E9rdame algo\u2026", "Remind me of something\u2026"), pending > 0 ? pending + (pending == 1 ? I18n.T(" pendiente", " pending") : I18n.T(" pendientes", " pending")) : null, delegate { QuickWindow.Remind(owner); });
            Add("memo", I18n.T("Apunta algo\u2026", "Note something\u2026"), null, delegate { QuickWindow.Note(owner); });
            if (!f.On) Add("clock", I18n.T("Concentrarme ", "Focus ") + FocusClock.Minutes(settings.FocusWork), null, owner.FocusStart);
            // Only what is turned on: no breaks or water rows for someone who doesn't use them.
            if (settings.WellOn) Add("drop", I18n.T("Una pausa conmigo", "Take a break with me"), null, OpenBreaks);
            if (owner.WaterOn)
            {
                int water = owner.WaterToday();
                Add("drop", I18n.T("+1 vaso de agua", "+1 glass"), settings.WaterGoal > 0 ? water + I18n.T(" de ", " of ") + settings.WaterGoal : water + I18n.T(" hoy", " today"), DrinkOne);
            }
            Add("heart", I18n.T("\u00BFQu\u00E9 tal el d\u00EDa?", "How's your day going?"), null, TellDay);
            if (GoogleHealth.Connected && !f.On)
            {
                GoogleHealth.Today w = owner.Watch;
                Add("walk", I18n.T("Mi actividad", "My activity"), w != null && !w.Empty ? w.Steps.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo(I18n.IsSpanish ? "es-ES" : "en-US")) + I18n.T(" pasos", " steps") : null, TellActivity);
            }
            // Not for someone who said they have no watch (it stays in Ajustes > Salud y actividad).
            if (GoogleHealth.Available && !GoogleHealth.Connected && !GoogleHealth.Connecting && settings.FitbitAsked != "no")
                Add("google", I18n.T("Conectar mi reloj", "Connect my watch"), null, delegate { owner.ConnectWatch(null); m.Say(I18n.T("Te abro el inicio de sesi\u00F3n en el navegador.", "Opening the sign-in page in your browser."), 3600); });
            AddSep();
            Add("brush", I18n.T("Cambiarme de ropa", "Change my outfit"), null, delegate { owner.ShowHome("mascot", false); });
            Add("logo", I18n.T("Abrir Stackshot", "Open Stackshot"), null, delegate { owner.ShowHome("home", false); });
            Add("home", I18n.T("Vete a casa", "Go home"), null, GoHome);
            menuAt = menuSeen = Anim.Now;
            menuGone = null;
            Halt();
            menuArmed = false;
            menuRect = RectangleF.Empty;
            menuHot = -1;
        }

        // The first shortcut of an action, or nothing when it has none.
        static string Keys(string combos) { return Hotkeys.Split(combos).Count > 0 ? Hotkeys.Display(combos) : null; }

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

        // Stands still where it is: a press or a menu stops a walk, so the menu never travels across the screen with it.
        // It stays put a moment after the menu closes too.
        void Halt()
        {
            if (state == State.Walk && !leaving)
            {
                state = State.Idle;
                targetX = bx;
                if (!leaving) RememberSpot(false);
            }
            slide = 0;
            nextIdea = Math.Max(nextIdea, Anim.Now + 5000);
        }

        void CloseMenu()
        {
            if (menu != null && !menuRect.IsEmpty)
            {
                menuGone = menu;
                menuGoneRect = menuRect;
                menuGoneAt = Anim.Now;
                nextIdea = Math.Max(nextIdea, Anim.Now + 3500);
            }
            menu = null;
            menuHot = -1;
            menuWell = -1;
            menuSticky = menuFocus = false;
        }

        // The menu's card (without the arrow): its size from what it holds, above the mascot and inside the monitor.
        bool menuDown;   // the menu had to come down over the mascot (no room above it)

        RectangleF MenuLayout(Graphics g)
        {
            Font item = Fonts.Get(Mac.TextFont, 13 * s), small = Fonts.Get(Mac.TextFont, 12 * s);
            float rowH = 28 * s, sepH = 9 * s, pad = 5 * s, w = 238 * s, arrow = 7 * s;
            float h = pad * 2;
            bool header = false;
            foreach (Choice ch in menu)
            {
                if (ch.Header) { header = true; continue; }
                h += ch.Sep ? sepH : rowH;
                if (ch.Sep) continue;
                // Wide enough for every label and its shortcut side by side, within the window (measured as the system
                // draws them, see PaintRows).
                if (ch.TextW == 0) ch.TextW = TextRenderer.MeasureText(ch.Text, item, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                if (ch.Detail != null && ch.DetailW == 0) ch.DetailW = TextRenderer.MeasureText(ch.Detail, small, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                w = Math.Max(w, (float)Math.Ceiling(33 * s + ch.TextW + (ch.Detail != null ? 16 * s + ch.DetailW : 0) + 12 * s + pad * 2));
            }
            if (header) w = Math.Max(w, 270 * s);
            w = Math.Min(w, W - 36 * s);   // its margins and the arrow's room stay inside the window
            // Header text wraps to the width; measured once.
            foreach (Choice ch in menu)
                if (ch.Header)
                {
                    if (ch.HeaderH == 0)
                        ch.HeaderH = (float)Math.Ceiling(TextRenderer.MeasureText(ch.Text, item, new Size((int)(w - pad * 2 - 20 * s), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 14 * s);
                    h += ch.HeaderH;
                }
            float x = ClampX(MidX - w / 2 - 14 * s, w + 28 * s) + 14 * s, y = m.Box.Y - D * 0.22f - arrow - h;
            // Sitting on a window near the top of the screen there is no room above it: the menu comes down to stay whole
            // on screen, over the mascot, and loses the arrow that would point at nothing.
            float top = wa.Top - wy + 6 * s;
            menuDown = y < top;
            if (menuDown) y = top;   // its top edge always on screen; the window is tall enough below
            // On whole pixels, so its text, border and separators are sharp at any scale (125 %, 150 %...).
            menuRect = new RectangleF((float)Math.Round(x), (float)Math.Round(y), (float)Math.Round(w), (float)Math.Ceiling(h));
            return menuRect;
        }

        // list and box: the open menu, or the one fading out after it closed (away).
        void PaintMenu(Graphics g, double now, List<Choice> list, RectangleF box, int hotRow, bool away)
        {
            float arrow = 7 * s;
            float x = box.X, y = box.Y, w = box.Width, h = box.Height;

            // Opening: it fades in and grows up out of the arrow, eased out. Closing: a quick fade and a slight shrink.
            double t = (now - (away ? menuGoneAt : menuAt)) / 1000.0, a, sc, rise;
            // A click can stamp the opening or closing a hair after this frame's clock: no time before it, so no
            // opacity outside 0..1 (a negative one made GDI+ throw, and the mascot stopped).
            t = Math.Max(0, t);
            if (away)
            {
                a = Math.Max(0, 1 - t / (MenuOutMs / 1000.0));
                sc = 0.97 + 0.03 * a;
                rise = 0;
            }
            else
            {
                double k = Math.Min(1, t / (MenuInMs / 1000.0)), p = 1 - Math.Exp(-t * 16);
                a = 1 - Math.Pow(1 - k, 3);
                sc = 0.94 + 0.06 * p;
                rise = 8 * s * (1 - p);
            }
            float tailX = Math.Max(x + 22 * s, Math.Min(x + w - 22 * s, MidX));
            Bitmap card = MenuCard(list, box, hotRow, tailX);
            Rectangle at = new Rectangle((int)Math.Round(x) - CardPad, (int)Math.Round(y) - CardPad, card.Width, card.Height);
            GraphicsState st = g.Save();
            // Moved only while it settles: once there, it is copied pixel for pixel, with no transform left over that
            // would soften the text.
            if (sc < 0.997 || rise > 0.05)
            {
                float oy = y + h + arrow;
                g.TranslateTransform(tailX, oy + (float)rise);
                g.ScaleTransform((float)sc, (float)sc);
                g.TranslateTransform(-tailX, -oy);
            }
            if (a >= 0.999) g.DrawImage(card, at, 0, 0, card.Width, card.Height, GraphicsUnit.Pixel);
            else
                using (System.Drawing.Imaging.ImageAttributes fade = new System.Drawing.Imaging.ImageAttributes())
                {
                    fade.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)a });
                    g.DrawImage(card, at, 0, 0, card.Width, card.Height, GraphicsUnit.Pixel, fade);
                }
            g.Restore(st);
        }

        // The menu as one picture: its shadow, card, arrow and rows. Its text is drawn by the system as in its own menus
        // (ClearType, sharp), which needs a solid background: the rows go on an opaque layer first, then onto the card.
        // Redrawn only when the row under the mouse, the choices, the place or the theme change; a frame just copies it.
        Bitmap menuCard;
        List<Choice> cardFor;
        int cardHot = -2;
        RectangleF cardBox;
        float cardTail, cardScale;
        bool cardDown;
        Palette cardPalette;

        // Room around the card for its shadow and arrow.
        int CardPad { get { return (int)Math.Ceiling(14 * s); } }

        Bitmap MenuCard(List<Choice> list, RectangleF box, int hotRow, float tailX)
        {
            Palette pal = Ds.Brushes;
            if (menuCard != null && cardFor == list && cardHot == hotRow && cardBox == box && cardTail == tailX && cardDown == menuDown && cardPalette == pal && cardScale == s)
                return menuCard;
            bool dark = pal.Dark;
            Font item = Fonts.Get(Mac.TextFont, 13 * s), small = Fonts.Get(Mac.TextFont, 12 * s);
            float rowH = 28 * s, sepH = 9 * s, pad = 5 * s, arrow = 7 * s;
            float x = (float)Math.Round(box.X), y = (float)Math.Round(box.Y), w = box.Width, h = box.Height;
            int cp0 = CardPad;
            Bitmap card = new Bitmap((int)Math.Ceiling(w) + cp0 * 2, (int)Math.Ceiling(h) + cp0 + (int)Math.Ceiling(22 * s), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            Color bg = pal.High ? Ds.Gdi(pal.Window) : dark ? Color.FromArgb(44, 44, 47) : Color.FromArgb(250, 250, 251);
            Color border = pal.High ? Ds.Gdi(pal.Hairline) : dark ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(31, 0, 0, 0);
            bg = Color.FromArgb(255, bg);
            using (Graphics g = Graphics.FromImage(card))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(cp0 - x, cp0 - y);
                // Soft shadow: stacked rounded rectangles that fade out.
                for (int i = pal.High ? 0 : 8; i >= 1; i--)
                {
                    RectangleF sr = box;
                    sr.Inflate(i * 1.5f * s, i * 1.5f * s);
                    sr.Offset(0, 5 * s);
                    double f = 1 - i / 9.0;
                    using (GraphicsPath sp = Theme.Round(sr, 10 * s + i * 1.5f * s))
                        MascotParts.FillSolid(g, sp, Color.FromArgb((int)((dark ? 15 : 8) * f * f + 0.5), 0, 0, 0));
                }
                using (GraphicsPath cp = Theme.Round(box, 10 * s))
                using (GraphicsPath tp = new GraphicsPath())
                {
                    MascotParts.FillSolid(g, cp, bg);
                    if (!menuDown)
                    {
                        tp.AddPolygon(new PointF[] { new PointF(tailX - 7 * s, y + h - 0.5f), new PointF(tailX, y + h + arrow), new PointF(tailX + 7 * s, y + h - 0.5f) });
                        MascotParts.FillSolid(g, tp, bg);
                    }
                    MascotParts.Stroke(g, cp, border, 1);
                    // The arrow's two sides, drawn over the card border where they meet.
                    if (!menuDown)
                    using (Pen pen = new Pen(border, 1))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawLines(pen, new PointF[] { new PointF(tailX - 7 * s, y + h), new PointF(tailX, y + h + arrow), new PointF(tailX + 7 * s, y + h) });
                    }
                    if (!menuDown) using (SolidBrush cover = new SolidBrush(bg)) g.FillRectangle(cover, tailX - 6.2f * s, y + h - 1.5f, 12.4f * s, 2.2f);
                }

                // The rows' layer: inside the card's rounded corners and clear of its border.
                int lx = (int)Math.Floor(x + pad) - 1, ly = (int)Math.Floor(y + pad) - 1;
                int lw = (int)Math.Ceiling(x + w - pad) + 1 - lx, lh = (int)Math.Ceiling(y + h - pad) + 1 - ly;
                using (Bitmap layer = new Bitmap(Math.Max(1, lw), Math.Max(1, lh), System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    using (Graphics lg = Graphics.FromImage(layer))
                    {
                        lg.Clear(bg);
                        lg.SmoothingMode = SmoothingMode.AntiAlias;
                        lg.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // the quality TextRenderer asks the system for
                        lg.TranslateTransform(-lx, -ly);
                        PaintRows(lg, list, x, y, w, hotRow, bg, lx, ly, item, small, rowH, sepH, pad);
                    }
                    g.DrawImage(layer, new Rectangle(lx, ly, layer.Width, layer.Height), 0, 0, layer.Width, layer.Height, GraphicsUnit.Pixel);
                }
            }
            if (menuCard != null) menuCard.Dispose();
            menuCard = card;
            cardFor = list; cardHot = hotRow; cardBox = box; cardTail = tailX; cardDown = menuDown; cardPalette = pal; cardScale = s;
            return card;
        }

        // The rows on the opaque layer (g is translated to the window's surface; ox, oy: where the layer sits on it, for
        // the text, which the system draws in the layer's own pixels).
        void PaintRows(Graphics g, List<Choice> list, float x, float y, float w, int hotRow, Color bg, int ox, int oy, Font item, Font small, float rowH, float sepH, float pad)
        {
            Palette pal = Ds.Brushes;
            Color label = Ds.Gdi(pal.Label), label2 = Ds.Gdi(pal.Label2), accent = Color.FromArgb(255, Ds.Gdi(pal.Accent));
            const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            float ry = y + pad;
            for (int i = 0; i < list.Count; i++)
            {
                Choice c = list[i];
                if (c.Header)
                {
                    c.R = RectangleF.Empty;
                    Rectangle hr = new Rectangle((int)Math.Round(x + pad + 10 * s) - ox, (int)Math.Round(ry + 6 * s) - oy, (int)(w - pad * 2 - 20 * s), (int)Math.Ceiling(c.HeaderH - 8 * s));
                    TextRenderer.DrawText(g, c.Text, item, hr, Over(label, bg), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    ry += c.HeaderH;
                    float hy = (float)Math.Round(ry - 2 * s);
                    using (Pen sp = new Pen(Ds.Gdi(pal.Separator), 1)) g.DrawLine(sp, x + 10 * s, hy, x + w - 10 * s, hy);
                    continue;
                }
                if (c.Sep)
                {
                    float sy = (float)Math.Round(ry + sepH / 2);
                    using (Pen sp = new Pen(Ds.Gdi(pal.Separator), 1)) g.DrawLine(sp, x + 10 * s, sy, x + w - 10 * s, sy);
                    c.R = RectangleF.Empty;
                    ry += sepH;
                    continue;
                }
                // Rows on whole pixels too: 9 px separators at 125 % would leave the rows below on half pixels.
                c.R = new RectangleF((float)Math.Round(x + pad), (float)Math.Round(ry), (float)Math.Round(w - pad * 2), (float)Math.Round(rowH));
                bool hot = i == hotRow;
                Color under = hot ? accent : bg;
                if (hot)
                    using (GraphicsPath hp = Theme.Round(c.R, 6 * s)) MascotParts.FillSolid(g, hp, accent);
                Color fg = hot ? Ds.Gdi(pal.OnAccent) : label;
                Icons.Draw(g, c.Icon, new RectangleF(c.R.X + 8 * s, c.R.Y + (rowH - 17 * s) / 2, 17 * s, 17 * s), hot ? fg : label2);
                // The label never runs under the shortcut: a very long one ends in "…".
                float labelW = c.R.Width - 36 * s - (c.Detail != null ? c.DetailW + 16 * s : 0);
                Rectangle lr = new Rectangle((int)Math.Round(c.R.X + 33 * s) - ox, (int)c.R.Y - oy, (int)Math.Max(10 * s, labelW), (int)c.R.Height);
                // White on the accent: ClearType's coloured edges would fringe on a saturated colour, so this row's text is
                // hinted greyscale (still sharp on its whole pixels); the others stay ClearType.
                if (hot) g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                TextRenderer.DrawText(g, c.Text, item, lr, Over(fg, under), Line | TextFormatFlags.EndEllipsis);
                if (c.Detail != null)
                {
                    Rectangle dr = new Rectangle((int)c.R.X - ox, (int)c.R.Y - oy, (int)(c.R.Width - 10 * s), (int)c.R.Height);
                    TextRenderer.DrawText(g, c.Detail, small, dr, Over(hot ? fg : label2, under), Line | TextFormatFlags.Right);
                }
                if (hot) g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                ry += rowH;
            }
        }

        // The system draws text in a solid colour: a translucent one (the secondary labels) is mixed over its background first.
        static Color Over(Color fg, Color bg)
        {
            double k = fg.A / 255.0;
            return Color.FromArgb(255, (int)Math.Round(bg.R + (fg.R - bg.R) * k), (int)Math.Round(bg.G + (fg.G - bg.G) * k), (int)Math.Round(bg.B + (fg.B - bg.B) * k));
        }

        // Choices and the menu are laid out on the whole drawing surface, but the window shown on screen is only the part of
        // it that holds something (it starts at presentAt): a mouse position in the window is moved onto the surface first.
        Point OnSurface(Point client) { return new Point(client.X + presentAt.X, client.Y + presentAt.Y); }

        int ChoiceAt(Point client)
        {
            if (menu == null) return -1;
            Point p = OnSurface(client);
            for (int i = 0; i < menu.Count; i++) if (menu[i].R.Contains(p)) return i;
            return -1;
        }

        bool InMenu(Point client) { return menu != null && menuRect.Contains(OnSurface(client)); }

        // ---- Mouse: click opens the menu, drag moves it, choices run on release.

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); m.Hover(true); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); m.Hover(false); menuHot = -1; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
            if (ChoiceAt(e.Location) >= 0 || InMenu(e.Location)) return;
            if (e.Button != MouseButtons.Left) return;
            down = true;
            Capture = true;
            Halt();
            smN = 0;
            downAt = Control.MousePosition;
            grab = new Point(downAt.X - (int)bx, downAt.Y - (int)by);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            MascotRate.ScreenSeen();   // the mouse is over it: the screen is on, whatever was missed
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
                ventDone = 0;
                m.EndVent();
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
            if (InMenu(e.Location)) return;
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
            if (clicks == 1) menuDue = now + 220;
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
            if (spotDue > 0) { spotDue = 0; settings.Save(); }
            if (dib != null) dib.Dispose();
            if (menuCard != null) { menuCard.Dispose(); menuCard = null; }
            if (bubbleCard != null) { bubbleCard.Dispose(); bubbleCard = null; }
            if (pillText != null) { pillText.Dispose(); pillText = null; }
        }
    }
}
