// Stackshot - Curvas, interpolaciones y el temporizador de animaciones.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    // ---------------------------------------------------------------- Animaciones

    // Curvas de animación: t va de 0 a 1.
    public static class Ease
    {
        public static double Linear(double t) { return t; }
        public static double OutCubic(double t) { double u = 1 - t; return 1 - u * u * u; }
        public static double InCubic(double t) { return t * t * t; }
        // Se pasa un poco y vuelve: para lo que aparece de golpe (avisos, el check de guardada).
        public static double OutBack(double t) { double u = t - 1; return 1 + 2.70158 * u * u * u + 1.70158 * u * u; }
    }

    // Un valor que va de donde esté hasta otro en un tiempo dado (con retraso opcional) y avisa al llegar.
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

    // Un único temporizador para todas las ventanas flotantes. Solo corre mientras algo se mueve (en reposo,
    // 0 % de CPU) y entonces pide al sistema el temporizador de 1 ms para que vaya fluido.
    public static class Anim
    {
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly List<FloatWindow> active = new List<FloatWindow>();
        static Timer timer;

        public static double Now { get { return clock.Elapsed.TotalMilliseconds; } }

        public static void Wake(FloatWindow w)
        {
            if (active.Contains(w)) return;
            w.LastStep = Now;
            active.Add(w);
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 10;
                timer.Tick += Tick;
            }
            if (!timer.Enabled)
            {
                Native.timeBeginPeriod(1);
                timer.Start();
            }
        }

        static void Tick(object sender, EventArgs e)
        {
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
                timer.Stop();
                Native.timeEndPeriod(1);
            }
        }

        // Muelle amortiguado: k es la rigidez; z = 1 llega sin pasarse y por debajo de 1 rebota un poco.
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

    // Arrastrar un fichero a otra aplicación igual que desde el Explorador, con la miniatura pegada al cursor.
}
