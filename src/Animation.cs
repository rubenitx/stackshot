// Stackshot - Easing curves, tweens and the shared animation clock.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Ease
    {
        public static double Linear(double t) { return t; }
        public static double OutCubic(double t) { double u = 1 - t; return 1 - u * u * u; }
        public static double InCubic(double t) { return t * t * t; }
        // Overshoots slightly before settling.
        public static double OutBack(double t) { double u = t - 1; return 1 + 2.70158 * u * u * u + 1.70158 * u * u; }
    }

    // Animates a value to a target over a duration (optional delay) and calls back when done.
    public class Tween
    {
        public double Value;
        public bool Running;
        double from, to, start, dur;
        Func<double, double> curve;
        Action done;

        public Tween(double value) { Value = value; }

        public double Target { get { return Running ? to : Value; } }

        public void Go(double target, double ms, double delay, Func<double, double> ease, Action onDone)
        {
            from = Value;
            to = target;
            start = Anim.Now + delay;
            dur = Math.Max(1, ms);
            curve = ease;
            done = onDone;
            Running = true;
        }

        public void Set(double value)
        {
            Value = value;
            Running = false;
            done = null;
        }

        public void Step(double now)
        {
            if (!Running) return;
            double t = (now - start) / dur;
            if (t < 0) return;
            if (t >= 1)
            {
                Value = to;
                Running = false;
                Action d = done;
                done = null;
                if (d != null) d();
                return;
            }
            Value = from + (to - from) * curve(t);
        }
    }

    // One clock drives every floating window, in step with the display: a background thread waits for each frame of
    // the desktop compositor and has the UI thread take one step, so every frame shows exactly one (a plain timer
    // drifts against the refresh and shows two steps in some frames and none in others). Steps never queue up behind
    // a busy UI thread, and nothing runs while nothing moves. A 10 ms timer takes over if the compositor can't be
    // waited on.
    public static class Anim
    {
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly List<FloatWindow> active = new List<FloatWindow>();
        static readonly AutoResetEvent resume = new AutoResetEvent(false);
        static Control sync;           // takes the frames to the UI thread
        static Thread pacer;
        static volatile bool running, noCompositor;
        static int posted;             // a step is waiting for the UI thread
        static System.Windows.Forms.Timer timer; // fallback without the compositor

        [DllImport("dwmapi.dll")] static extern int DwmFlush();

        public static double Now { get { return clock.Elapsed.TotalMilliseconds; } }

        public static void Wake(FloatWindow w)
        {
            if (active.Contains(w)) return;
            w.LastStep = Now;
            active.Add(w);
            Start();
        }

        static void Start()
        {
            if (active.Count == 0) return;
            if (noCompositor)
            {
                if (timer == null)
                {
                    timer = new System.Windows.Forms.Timer();
                    timer.Interval = 10;
                    timer.Tick += delegate { Tick(); };
                }
                if (!timer.Enabled) timer.Start();
                return;
            }
            if (running) return;
            if (sync == null)
            {
                sync = new Control();
                GC.KeepAlive(sync.Handle);
            }
            running = true;
            if (pacer == null)
            {
                pacer = new Thread(Pace);
                pacer.IsBackground = true;
                pacer.Name = "Stackshot frames";
                pacer.Start();
            }
            resume.Set();
        }

        // Frame thread: sleeps until woken, then posts one step per composed frame while anything moves.
        static void Pace()
        {
            Action step = Tick, fallback = Start;
            double last = 0;
            while (true)
            {
                resume.WaitOne();
                while (running)
                {
                    if (DwmFlush() != 0)
                    {
                        noCompositor = true;
                        running = false;
                        try { sync.BeginInvoke(fallback); } catch (InvalidOperationException) { }
                        break;
                    }
                    // Never spin, even if a driver returns at once while the UI thread is busy (240 Hz still fits).
                    if (Now - last < 3) Thread.Sleep(3);
                    last = Now;
                    if (Interlocked.CompareExchange(ref posted, 1, 0) != 0) continue; // the last step hasn't run yet
                    try { sync.BeginInvoke(step); }
                    catch (InvalidOperationException) { posted = 0; running = false; } // shutting down
                }
            }
        }

        static void Tick()
        {
            Interlocked.Exchange(ref posted, 0);
            double now = Now;
            foreach (FloatWindow w in active.ToArray())
            {
                bool more = false;
                if (!w.IsDisposed)
                {
                    try { more = w.Step(now); }
                    catch (Exception ex) { ShotStack.Log("Animaci\u00F3n: " + ex.Message); }
                }
                if (!more) active.Remove(w);
            }
            if (active.Count == 0)
            {
                running = false;
                if (timer != null) timer.Stop();
            }
        }

        // Damped spring: k is stiffness; z = 1 is critically damped, below 1 bounces.
        public static void Spring(ref double x, ref double v, double target, double k, double z, double dt)
        {
            double c = 2 * z * Math.Sqrt(k);
            while (dt > 1e-6)
            {
                double h = Math.Min(dt, 0.004);
                v += (k * (target - x) - c * v) * h;
                x += v * h;
                dt -= h;
            }
        }
    }

}
