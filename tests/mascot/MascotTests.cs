// Mascot timing and frame-rate rules, all offscreen: a fake clock drives Mascot.Step, and the CPU probe renders into
// memory or into a WPF window placed off the desktop.
// Suites run by default: timing rules doze. On request: cpu (idle CPU probe; prints numbers, fails only if the new
// rates cost more than the old ones).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot.Tests
{
    static class MascotTests
    {
        sealed class TestFailed : Exception { public TestFailed(string message) : base(message) { } }

        static int passes, fails;
        static readonly string[] defaultSuites = { "timing", "rules", "doze" };
        // Tick-aligned intervals (what the timers really give), the nominal ones the hosts ask for, and slow ones.
        static readonly double[] Intervals = { 15.625, 16, 31, 31.25, 62, 62.5, 93.75, 100, 125, 250, 500 };

        [STAThread]
        static int Main(string[] args)
        {
            Settings.ReadOnly = true;
            I18n.Apply("es");   // the suites check the Spanish texts, whatever language this Windows is in
            List<string> suites = args.Length > 0 ? new List<string>(args) : new List<string>(defaultSuites);
            foreach (string s in suites)
            {
                switch (s.ToLowerInvariant())
                {
                    case "timing": Timing(); break;
                    case "rules": Rules(); break;
                    case "doze": Doze(); break;
                    case "cpu": Cpu(); break;
                    default: Test("main", s, delegate { throw new TestFailed("unknown suite"); }); break;
                }
            }
            Console.WriteLine("TOTAL " + passes + " PASS, " + fails + " FAIL");
            return fails;
        }

        static string detail;

        static void Test(string suite, string name, Action body)
        {
            detail = "";
            string status = "PASS", text;
            try { body(); text = detail; }
            catch (TestFailed f) { status = "FAIL"; text = f.Message + (detail.Length > 0 ? " | " + detail : ""); }
            catch (Exception e) { status = "FAIL"; text = e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(); }
            if (status == "PASS") passes++; else fails++;
            Console.WriteLine(status + "  " + suite + "/" + name + (text.Length > 0 ? "  " + text : ""));
        }

        static void Check(bool ok, string message) { if (!ok) throw new TestFailed(message); }
        static void Note(string s) { detail += (detail.Length > 0 ? "; " : "") + s; }
        static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        // ---- A mascot on a fake clock, with nothing happening on its own.

        sealed class Rig
        {
            public double T = 10000;
            public readonly Mascot M = new Mascot();

            public Rig()
            {
                M.TestClock = delegate { return T; };
                M.Alone = 0;
                M.SleepIdle = false;
                M.Box = new RectangleF(0, 0, 100, 100);
                Step();
            }

            public void Step() { M.Step(T, new PointF(50, 45), false); }

            public void Advance(double interval) { T += interval; Step(); }

            public void Run(double interval, double ms)
            {
                double end = T + ms;
                while (T + interval <= end + 1e-9) Advance(interval);
            }
        }

        // ---- timing: the same real time at every frame interval

        static void Timing()
        {
            foreach (double iv in Intervals)
            {
                double interval = iv;
                Test("timing", "zzz-lifetime@" + F(interval), delegate
                {
                    Rig r = new Rig();
                    r.Run(interval, 2000);
                    r.M.SleepNow();
                    int guard = 0;
                    while (r.M.CountParticles('z') == 0 && guard++ < 10000) r.Advance(interval);
                    Check(r.M.CountParticles('z') == 1, "no z appeared");
                    double born = r.T;
                    r.M.WakeUp();   // no more z: follow this one alone
                    double lastAge = 0, lastT = born;
                    while (r.M.CountParticles('z') > 0 && guard++ < 20000)
                    {
                        r.Advance(interval);
                        double x, y, age;
                        if (r.M.CountParticles('z') == 0 || !r.M.TestParticle(0, out x, out y, out age)) break;
                        double real = r.T - born;
                        Check(Math.Abs(age - real) <= real * 0.05 + 1e-6, "age " + F(age) + " ms after " + F(real) + " ms of real time");
                        lastAge = age; lastT = r.T;
                    }
                    double gone = r.T - born;
                    Check(gone >= 2200 - 1e-6 && gone <= 2200 + interval + 1e-6, "lived " + F(gone) + " ms (2200 expected, one frame of slack)");
                    Note("lived " + F(gone) + " ms, last age " + F(lastAge) + " at " + F(lastT - born));
                });

                Test("timing", "zzz-path@" + F(interval), delegate
                {
                    Rig r = new Rig();
                    r.Run(31.25, 500);
                    int i = r.M.AddTestParticle('z', 0, -0.2);
                    double born = r.T, worst = 0;
                    for (int n = 0; n < 4000 && r.T - born + interval < 2200; n++)
                    {
                        r.Advance(interval);
                        double x, y, age;
                        Check(r.M.TestParticle(i, out x, out y, out age), "particle gone early");
                        double a = r.T - born, ey = -0.2 * a / 1000.0, ex = 0.002 * 60 / 1000.0 * 300 * (1 - Math.Cos(a / 300));
                        worst = Math.Max(worst, Math.Abs(y - ey) / Math.Abs(ey));
                        Check(Math.Abs(y - ey) <= Math.Abs(ey) * 0.05 + 1e-9, "rise " + F(y) + " vs " + F(ey) + " at " + F(a) + " ms");
                        Check(Math.Abs(x - ex) <= 0.002 + 1e-9, "sway " + F(x) + " vs " + F(ex) + " at " + F(a) + " ms");
                    }
                    Note("worst rise error " + F(worst * 100) + "%");
                });

                Test("timing", "spark-path@" + F(interval), delegate
                {
                    Rig r = new Rig();
                    int i = r.M.AddTestParticle('p', 0.9, -0.6);
                    double born = r.T, rate = -60 * Math.Log(0.92), worst = 0;
                    while (r.T - born + interval < 700)
                    {
                        r.Advance(interval);
                        double x, y, age;
                        Check(r.M.TestParticle(i, out x, out y, out age), "spark gone early");
                        double a = (r.T - born) / 1000.0, ex = 0.9 * (1 - Math.Exp(-rate * a)) / rate;
                        worst = Math.Max(worst, Math.Abs(x - ex) / ex);
                        Check(Math.Abs(x - ex) <= ex * 0.05, "spark at " + F(x) + " vs " + F(ex) + " after " + F(a * 1000) + " ms");
                    }
                    r.Run(interval, 700 + interval);
                    Check(r.M.CountParticles('p') == 0, "spark outlived its 700 ms");
                    Note("worst error " + F(worst * 100) + "%");
                });
            }

            // The hop spring: same position at the same real time, whatever the frame interval.
            double[] reference = SpringTrace(1, 3000);
            double peak = 0;
            foreach (double v in reference) peak = Math.Max(peak, Math.Abs(v));
            foreach (double iv in Intervals)
            {
                double interval = iv;
                Test("timing", "spring@" + F(interval), delegate
                {
                    Rig r = new Rig();
                    r.Run(15.625, 3000);
                    double t0 = r.T, worst = 0;
                    r.M.Hop(1);
                    while (r.T - t0 + interval <= 2999)
                    {
                        r.Advance(interval);
                        double a = r.T - t0;
                        int k = (int)Math.Floor(a);
                        double want = reference[k] + (reference[Math.Min(k + 1, reference.Length - 1)] - reference[k]) * (a - k);
                        double err = Math.Abs(r.M.TestJump - want);
                        worst = Math.Max(worst, err);
                        Check(err <= peak * 0.05, "jump " + F(r.M.TestJump) + " vs " + F(want) + " at " + F(a) + " ms (peak " + F(peak) + ")");
                    }
                    Note("worst " + F(worst / peak * 100) + "% of the peak");
                });
            }

            Test("timing", "nap-not-lively", delegate
            {
                // Asleep, only its slow z's move: the hosts can tick at the idle rate.
                Rig r = new Rig();
                r.M.SleepNow();
                int lively = 0, maxZ = 0;
                for (int n = 0; n < 192; n++)
                {
                    r.Advance(62.5);
                    if (n >= 48 && r.M.Lively) lively++;
                    maxZ = Math.Max(maxZ, r.M.CountParticles('z'));
                }
                Check(r.M.Sleeping, "woke up");
                Check(lively == 0, lively + " lively frames during a quiet nap");
                Check(maxZ <= 2, maxZ + " z on screen at once (2 at most)");
                Note("max z " + maxZ);
            });

            Test("timing", "zzz-count@500", delegate
            {
                // The reported symptom: 14-15 z piled up at 2 fps. Now at most 2 whatever the rate.
                Rig r = new Rig();
                r.M.SleepNow();
                int maxZ = 0;
                for (int n = 0; n < 40; n++) { r.Advance(500); maxZ = Math.Max(maxZ, r.M.CountParticles('z')); }
                Check(maxZ <= 2, maxZ + " z at once");
                Note("max z " + maxZ);
            });

            Test("timing", "resume-after-pause", delegate
            {
                // A long gap (window hidden for minutes) does not fast-forward a pop-in.
                Rig r = new Rig();
                r.Run(31.25, 1000);
                r.T += 600000;
                r.M.PopIn();
                r.Step();
                Check(r.M.Lively, "the pop-in was over in the first frame");
            });
        }

        // Hop(1) from rest, sampled every millisecond by stepping 1 ms at a time.
        static double[] SpringTrace(double interval, int ms)
        {
            Rig r = new Rig();
            r.Run(15.625, 3000);
            double t0 = r.T;
            r.M.Hop(1);
            double[] trace = new double[ms + 1];
            trace[0] = r.M.TestJump;
            for (int i = 1; i <= ms; i++) { r.T = t0 + i * interval; r.Step(); trace[i] = r.M.TestJump; }
            return trace;
        }

        // ---- rules: pure decisions of the hosts

        static void Rules()
        {
            Test("rules", "away-nap", delegate
            {
                Check(!MascotRate.AwayNap(60001, true, true, false), "naps after one minute (the old rule)");
                Check(!MascotRate.AwayNap(299999, true, true, false), "naps before five minutes");
                Check(MascotRate.AwayNap(300001, true, true, false), "no nap after five minutes away");
                Check(!MascotRate.AwayNap(900000, false, true, false), "naps although 'Se duerme sola' is off");
                Check(!MascotRate.AwayNap(900000, true, false, false), "naps in the middle of a walk, fall, drag or menu");
                Check(!MascotRate.AwayNap(900000, true, true, true), "naps again while already napping");
            });
            Test("rules", "away-wake", delegate
            {
                Check(MascotRate.AwayWake(true, 100, false), "input does not wake it");
                Check(MascotRate.AwayWake(true, 400000, true), "the mouse moving near it does not wake it");
                Check(!MascotRate.AwayWake(true, 400000, false), "wakes with nobody there");
                Check(!MascotRate.AwayWake(false, 100, true), "wakes when it was not napping");
            });
            Test("rules", "long-away-freezes", delegate
            {
                Check(!MascotRate.Frozen(599999, true), "frozen before ten minutes away");
                Check(MascotRate.Frozen(600001, true), "still drawing the nap after ten minutes away");
                Check(!MascotRate.Frozen(3600000, false), "frozen while awake (it only freezes a nap)");
                Check(!MascotRate.Frozen(400, true), "frozen with the user back");
                Check(MascotRate.Hidden <= 500, "the frozen clock checks for the user's return less than twice a second");
            });
            Test("rules", "pet-interval", delegate
            {
                Check(MascotRate.Pet(true, true, true, true, true) == MascotRate.Hidden, "screen off must tick at the hidden rate");
                Check(MascotRate.Pet(false, true, false, false, false) == MascotRate.Look, "moving: the timer backs up the pacer at 32 fps");
                Check(MascotRate.Pet(false, false, true, false, false) == MascotRate.Look, "menu open");
                Check(MascotRate.Pet(false, false, false, true, false) == MascotRate.Look, "lively");
                Check(MascotRate.Pet(false, false, false, false, true) == MascotRate.Look, "mouse moving near it");
                Check(MascotRate.Pet(false, false, false, false, false) == MascotRate.Idle, "idle or asleep");
                Check(MascotRate.Idle <= 62.5 && MascotRate.Hidden == 500, "away with the screen on must stay at 16 fps, 2 fps only when off");
            });
            Test("rules", "home-interval", delegate
            {
                Check(MascotRate.Home(false, false, true, true, false, true, false, true, 0) == 0, "hidden window keeps ticking");
                Check(MascotRate.Home(true, true, true, true, false, true, false, true, 0) == MascotRate.Hidden, "cloaked: only a slow check");
                Check(MascotRate.Home(true, false, true, true, false, false, false, true, 0) == MascotRate.Anim, "animating: full rate");
                Check(MascotRate.Home(true, false, true, true, true, false, false, true, 0) == MascotRate.Look, "a page entering: half rate");
                Check(MascotRate.Home(true, false, true, true, false, false, false, false, 0) == MascotRate.Look, "animating in the background: half rate");
                Check(MascotRate.Home(true, false, false, false, false, false, false, true, 0) == 0, "nothing to draw: stop");
                Check(MascotRate.Home(true, false, true, false, false, true, false, false, 0) == MascotRate.Look, "following the mouse");
                Check(MascotRate.Home(true, false, true, false, false, false, false, true, 1000) == MascotRate.Look, "active, just stirred");
                Check(MascotRate.Home(true, false, true, false, false, false, false, true, 20000) == MascotRate.Idle, "active, settled");
                Check(MascotRate.Home(true, false, true, false, false, false, false, false, 1000) == MascotRate.Idle, "background");
                Check(MascotRate.Home(true, false, true, false, false, false, false, false, 20000) == MascotRate.Background, "background, settled");
                Check(MascotRate.Home(true, false, true, false, false, false, true, true, 1000) == MascotRate.Look, "napping, active");
                Check(MascotRate.Home(true, false, true, false, false, false, true, false, 1000) == MascotRate.Idle, "napping, background");
            });
            Test("rules", "tick-aligned", delegate
            {
                // Timers fire on whole 15.625 ms ticks: each timer interval must sit just under a tick multiple.
                int[] timerRates = { MascotRate.Look, MascotRate.Idle, MascotRate.Background, MascotRate.Hidden };
                foreach (int v in timerRates)
                {
                    double ticks = Math.Ceiling(v / 15.625), slack = ticks * 15.625 - v;
                    Check(slack < 1.0, v + " ms really fires every " + F(ticks * 15.625) + " ms");
                }
                Check(MascotRate.Anim < 20 && MascotRate.Look >= 20, "only the full rate may ride the render loop");
            });
            Test("rules", "screen-state", delegate
            {
                MascotRate.ScreenSeen();
                Check(!MascotRate.ScreenOff, "starts on");
                MascotRate.SetDisplayOff(true);
                Check(MascotRate.ScreenOff, "display off");
                MascotRate.SetDisplayOff(false);
                MascotRate.SetLocked(true);
                Check(MascotRate.ScreenOff, "locked");
                MascotRate.ScreenSeen();
                Check(!MascotRate.ScreenOff, "input over the mascot proves the screen is on");
            });
            Test("rules", "screen-recovers", delegate
            {
                // A missed "on" or "unlock" must not freeze the mascots: input after the report brings them back, but not
                // the keys that locked it (Win+L lands just before the lock notification).
                Check(!MascotRate.BackFromOff(false, 0, 5000), "screen on: nothing to recover");
                Check(!MascotRate.BackFromOff(true, 1000, 900), "input before the report");
                Check(!MascotRate.BackFromOff(true, 1000, 1000 + MascotRate.OffGraceMs), "the key that locked it");
                Check(MascotRate.BackFromOff(true, 1000, 1000 + MascotRate.OffGraceMs + 1), "input after the report");
                Check(MascotRate.BackFromOff(true, int.MaxValue - 100, unchecked(int.MaxValue + 2000)), "tick count wraps");
                MascotRate.ScreenSeen();
                int t0 = Environment.TickCount;
                MascotRate.SetLocked(true);
                Check(!MascotRate.InputSinceOff(t0 - 10), "lock: older input ignored");
                Check(MascotRate.InputSinceOff(unchecked(t0 + 5000)), "lock: later input seen");
                System.Threading.Thread.Sleep(40);
                MascotRate.SetDisplayOff(true);   // already unseen: the first report's time stands
                Check(MascotRate.InputSinceOff(unchecked(t0 + 5000)), "display off while locked keeps the lock time");
                MascotRate.ScreenSeen();
                Check(!MascotRate.InputSinceOff(unchecked(t0 + 5000)), "seen: nothing to recover");
            });
            Test("rules", "screen-watch", delegate
            {
                // The session's display, not the console's (Remote Desktop), and a watcher that works without the pet.
                System.Reflection.FieldInfo f = typeof(MascotScreen).GetField("DisplayStatus",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Check(f != null && ((Guid)f.GetValue(null)) == new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5"),
                      "must watch GUID_SESSION_DISPLAY_STATUS");
                MascotScreen.Ensure();
                MascotScreen.Ensure();
                Check(MascotScreen.Watching, "hidden watcher window created");
                MascotRate.ScreenSeen();
                MascotScreen.Check();
                Check(!MascotRate.ScreenOff, "check on a seen screen changes nothing");
            });
        }

        // ---- doze: no napping while busy (walking, falling, menu)

        static void Doze()
        {
            Test("doze", "not-while-busy", delegate
            {
                Rig r = new Rig();
                r.M.SleepIdle = true;
                r.M.Look.Personality = 1;   // "Tranquilo": dozes off after 25 s
                r.M.Busy = true;
                for (int n = 0; n < 1920; n++)
                {
                    r.Advance(31.25);
                    Check(!r.M.Sleeping && r.M.Current != Mascot.Mood.Yawn, "dozed off while walking, at " + F(n * 31.25) + " ms");
                }
                r.M.Busy = false;
                for (int n = 0; n < 156; n++)
                {
                    r.Advance(31.25);
                    Check(!r.M.Sleeping, "fell asleep " + F(n * 31.25) + " ms after a walk");
                }
                r.Run(31.25, 30000);
                Check(r.M.Sleeping, "never dozed off once left alone");
            });
            Test("doze", "natural-nap", delegate
            {
                Rig r = new Rig();
                r.M.SleepIdle = true;
                r.M.Look.Personality = 1;
                r.Run(62.5, 26000);
                Check(r.M.Sleeping, "did not doze off after 26 s alone");
            });
            Test("doze", "sleep-idle-off", delegate
            {
                Rig r = new Rig();
                r.M.SleepIdle = false;
                r.Run(250, 600000);
                Check(!r.M.Sleeping, "dozed off with 'Se duerme sola' off");
            });
        }

        // ---- cpu: idle CPU probe, offscreen

        static void Cpu()
        {
            MascotLook look = new MascotLook();
            look.Kind = Math.Min(4, MascotParts.Kinds.Length - 1);
            look.Color = Math.Min(14, MascotParts.ColorNames.Length - 1);
            look.Hat = Math.Min(30, MascotParts.HatNames.Length - 1);
            look.Outfit = Math.Min(31, MascotParts.OutfitNames.Length - 1);
            look.Face = Math.Min(7, MascotParts.FaceNames.Length - 1);
            int secs = 4, reps = 3;
            // Process CPU cycles (QueryProcessCycleTime, exact, unlike the 15.6 ms CPU-time quantum) in millions per
            // second; each scenario is repeated, alternated with the others, and the median kept, because other work
            // on the machine shifts single runs.

            Test("cpu", "desktop-pet", delegate
            {
                PetPhase(look, false, 31, 2);
                PetPhase(look, true, 31, 2);
                List<double> oldIdle = new List<double>(), newIdle = new List<double>(), oldNap = new List<double>(), newNap = new List<double>();
                for (int rep = 0; rep < reps; rep++)
                {
                    oldIdle.Add(PetPhase(look, false, 63, secs));                 // before: idle at 63 ms (fires every 75)
                    newIdle.Add(PetPhase(look, false, MascotRate.Idle, secs));
                    oldNap.Add(PetPhase(look, true, 31, secs));                   // before: the z's kept a nap "lively" (32 fps)
                    newNap.Add(PetPhase(look, true, MascotRate.Idle, secs));
                }
                Note("idle " + F(Median(oldIdle)) + " -> " + F(Median(newIdle)) + " Mcycles/s");
                Note("asleep " + F(Median(oldNap)) + " -> " + F(Median(newNap)) + " Mcycles/s");
                Check(Median(newNap) < Median(oldNap), "a nap costs more than before");
                Check(Median(newIdle) <= Median(oldIdle) * 1.5, "idle costs much more than before");
            });

            Test("cpu", "home-window", delegate
            {
                // The main window's per-frame work (step, GDI+ paint of the 150 px mascot layer, diff against the last
                // frame), in memory: an offscreen WPF window throttles its render thread and caps the rate at ~9 fps, so
                // it can't stand in for the real one. Not counted: the WPF render loop the old 30 fps clock kept running
                // at the monitor's refresh rate, which the new timer no longer wakes.
                HomePhase(look, 31, 2);
                List<double> oldActive = new List<double>(), newActive = new List<double>(), newSettled = new List<double>(),
                             oldBack = new List<double>(), newBack = new List<double>();
                for (int rep = 0; rep < reps; rep++)
                {
                    oldActive.Add(HomePhase(look, 31, secs));                     // before: ~30 fps on the render loop
                    newActive.Add(HomePhase(look, MascotRate.Look, secs));
                    newSettled.Add(HomePhase(look, MascotRate.Idle, secs));
                    oldBack.Add(HomePhase(look, 100, secs));                      // before: 100 ms (fires every 109)
                    newBack.Add(HomePhase(look, MascotRate.Background, secs));
                }
                Note("active idle " + F(Median(oldActive)) + " -> " + F(Median(newActive)) + " Mcycles/s, " + F(Median(newSettled)) + " after 15 s");
                Note("background " + F(Median(oldBack)) + " -> " + F(Median(newBack)) + " Mcycles/s");
                Check(Median(newSettled) < Median(oldActive), "a settled active window costs more than before");
                Check(Median(newBack) < Median(oldBack) * 1.1, "the background window costs more than before");
            });
        }

        static double PetPhase(MascotLook look, bool asleep, int ms, int secs)
        {
            float s = 1.25f;
            int D = (int)Math.Round(84 * s), W = (int)Math.Round(340 * s), H = (int)Math.Round(D * 1.02 + 360 * s);
            Mascot m = new Mascot();
            m.Look = look; m.Alone = 0; m.SleepIdle = false;
            m.Box = new RectangleF((W - D) / 2f, H - D * 1.02f, D, D);
            if (asleep) m.SleepNow();
            RectangleF lastPaint = RectangleF.Empty;
            int frames = 0;
            using (Dib dib = new Dib(W, H, true))
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
            {
                timer.Interval = ms;
                timer.Tick += delegate
                {
                    m.Step(Anim.Now, new PointF(W / 2f, H - D), false);
                    using (Graphics g = dib.Graphics())
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        RectangleF pb = m.PaintBounds;
                        if (!lastPaint.IsEmpty) { RectangleF clip = RectangleF.Union(pb, lastPaint); clip.Inflate(2, 2); g.SetClip(clip); }
                        lastPaint = pb;
                        g.Clear(System.Drawing.Color.Transparent);
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        m.Paint(g);
                    }
                    frames++;
                };
                double cpu = Measure(secs, delegate { timer.Start(); }, delegate { timer.Stop(); });
                Console.WriteLine("      pet " + (asleep ? "asleep" : "idle") + " @" + ms + " ms: " + F(frames / (double)secs) + " fps, " + F(cpu) + " Mcycles/s");
                return cpu;
            }
        }

        static int lastChanged;   // pixels that differed in the last frame (the diff GdiLayer does before uploading)

        // Like GdiLayer.Refresh for the main window's mascot slot (box 150 at 100%: a 255 x 263 layer).
        static double HomePhase(MascotLook look, int ms, int secs)
        {
            const int box = 150, W = 255, H = 263;
            Mascot m = new Mascot();
            m.Look = look; m.Alone = 0; m.SleepIdle = false;
            m.Box = new RectangleF((W - box) / 2f, H - box - box * 0.06f, box, box);
            int[] cur = new int[W * H], prev = new int[W * H];
            int frames = 0;
            using (Dib dib = new Dib(W, H, true))
            {
                DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
                timer.Interval = TimeSpan.FromMilliseconds(ms);
                timer.Tick += delegate
                {
                    m.Step(Anim.Now, new PointF(W / 2f, H / 2f), false);
                    using (Graphics g = dib.Graphics())
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        m.Paint(g);
                    }
                    System.Runtime.InteropServices.Marshal.Copy(dib.Bits, cur, 0, W * H);
                    int changed = 0;
                    for (int i = 0; i < cur.Length; i++) if (cur[i] != prev[i]) changed++;
                    int[] t = prev; prev = cur; cur = t;
                    frames++;
                    lastChanged = changed;
                };
                double cpu = Measure(secs, delegate { timer.Start(); }, delegate { timer.Stop(); });
                Console.WriteLine("      home @" + ms + " ms: " + F(frames / (double)secs) + " fps, " + F(cpu) + " Mcycles/s");
                return cpu;
            }
        }

        // Runs the message loop for secs seconds and returns the process CPU cycles in millions per second.
        static double Measure(int secs, Action start, Action stop)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer end = new DispatcherTimer(DispatcherPriority.Normal);
            end.Interval = TimeSpan.FromSeconds(secs);
            end.Tick += delegate { end.Stop(); frame.Continue = false; };
            ulong c0, c1;
            QueryProcessCycleTime(GetCurrentProcess(), out c0);
            Stopwatch sw = Stopwatch.StartNew();
            start();
            end.Start();
            Dispatcher.PushFrame(frame);
            stop();
            QueryProcessCycleTime(GetCurrentProcess(), out c1);
            return (c1 - c0) / 1e6 / sw.Elapsed.TotalSeconds;
        }

        static double Median(List<double> v)
        {
            List<double> s = new List<double>(v);
            s.Sort();
            return s[s.Count / 2];
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryProcessCycleTime(IntPtr process, out ulong cycles);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    }
}
