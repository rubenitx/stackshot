// Stackshot - Props for the mascot's idle activities and sleep variants, drawn in the mascot's own soft style.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Everything is drawn in units of D (the body center is the origin) with a handful of cheap shapes, so an activity
    // costs about as much as the mascot itself.
    public static class MascotProps
    {
        public const int None = 0, Coffee = 1, Juggle = 2, Console = 3, Stretch = 4, Read = 5, Photo = 6;
        public const int Count = 6;

        public static double Duration(int act)
        {
            switch (act)
            {
                case Coffee: return 8400;
                case Juggle: return 7000;
                case Console: return 9000;
                case Stretch: return 3600;
                case Read: return 9500;
                default: return 6200;
            }
        }

        // 0..1 bump over a stretch.
        public static double StretchAmt(double t)
        {
            double p = Math.Max(0, Math.Min(1, (t - 300) / 2600.0));
            return Math.Pow(Math.Sin(p * Math.PI), 0.5);
        }

        static readonly Color Cream = Color.FromArgb(250, 246, 238);
        static readonly Color PageEdge = Color.FromArgb(214, 206, 190);

        static void Fill(Graphics g, Color c, float x, float y, float w, float h, float r)
        {
            using (GraphicsPath p = Theme.Round(new RectangleF(x, y, w, h), r))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        static void Grad(Graphics g, Color a, Color b, float x, float y, float w, float h, float r)
        {
            RectangleF rc = new RectangleF(x, y, w, h);
            using (GraphicsPath p = Theme.Round(rc, r))
            using (LinearGradientBrush br = new LinearGradientBrush(RectangleF.Inflate(rc, 0.01f, 0.01f), a, b, 90f)) g.FillPath(br, p);
        }

        static void Seg(Graphics g, Color c, float w, float x1, float y1, float x2, float y2)
        {
            using (Pen p = new Pen(c, w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, x1, y1, x2, y2); }
        }

        static void Dot(Graphics g, Color c, float x, float y, float r)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
        }

        static void Hand(Graphics g, Color c1, Color c2, float x, float y)
        {
            Color c = Mac.Mix(c1, c2, 0.3);
            Dot(g, c, x, y, 0.05f);
            using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255), 0.007f)) g.DrawEllipse(p, x - 0.05f, y - 0.05f, 0.1f, 0.1f);
        }

        static Color A(Color c, double k) { return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(c.A * k))), c); }

        // t: ms since the activity began; k: 0..1 fade in and out.
        public static void Paint(Graphics g, int act, float D, double t, double k, Color c1, Color c2)
        {
            if (act == None || k <= 0.01) return;
            GraphicsState st = g.Save();
            g.ScaleTransform(D, D);
            float e = (float)(0.6 + 0.4 * k);
            g.TranslateTransform(0, 0.2f);
            g.ScaleTransform(e, e);
            g.TranslateTransform(0, -0.2f);
            switch (act)
            {
                case Coffee: PaintCoffee(g, t, k, c1, c2); break;
                case Juggle: PaintJuggle(g, t, c1, c2); break;
                case Console: PaintConsole(g, t, c1, c2); break;
                case Stretch: PaintStretch(g, t, c1, c2); break;
                case Read: PaintRead(g, t, c1, c2); break;
                case Photo: PaintPhoto(g, t, c1, c2); break;
            }
            g.Restore(st);
        }

        static void PaintCoffee(Graphics g, double t, double k, Color c1, Color c2)
        {
            double cyc = (t % 4200) / 4200.0;
            float lift = (float)Math.Max(0, Math.Min(1, (0.5 - 0.5 * Math.Cos(cyc * Math.PI * 2)) * 1.7));
            float cx = 0.27f - 0.12f * lift, cy = 0.19f - 0.2f * lift;
            Hand(g, c1, c2, -0.3f, 0.22f);
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-16 * lift);
            g.ScaleTransform(1.35f, 1.35f);
            Seg(g, Cream, 0.022f, 0.06f, -0.02f, 0.085f, 0.0f);
            Seg(g, Cream, 0.022f, 0.085f, 0.0f, 0.058f, 0.03f);
            Grad(g, Cream, Color.FromArgb(222, 214, 200), -0.06f, -0.055f, 0.12f, 0.11f, 0.028f);
            Fill(g, Color.FromArgb(120, 76, 52), -0.05f, -0.052f, 0.1f, 0.02f, 0.01f);
            Fill(g, Color.FromArgb(70, c2.R, c2.G, c2.B), -0.06f, 0.0f, 0.12f, 0.012f, 0.006f);
            Hand(g, c1, c2, -0.05f, 0.02f);
            g.Restore(st);
            for (int i = 0; i < 3; i++)
            {
                double u = ((t / 1900.0) + i / 3.0) % 1;
                float x = cx + (i - 1) * 0.035f + (float)Math.Sin(u * 7 + i) * 0.018f, y = cy - 0.08f - (float)u * 0.2f;
                Seg(g, A(Color.White, 0.55 * (1 - u) * k), 0.014f, x, y, x + (float)Math.Sin(u * 7 + i + 1) * 0.02f, y - 0.045f);
            }
        }

        static readonly Color[] Balls = { Color.FromArgb(255, 92, 140), Color.FromArgb(255, 214, 10), Color.FromArgb(100, 210, 255) };

        static void PaintJuggle(Graphics g, double t, Color c1, Color c2)
        {
            float dip = (float)Math.Sin(t / 1000.0 * Math.PI * 2) * 0.012f;
            Hand(g, c1, c2, -0.3f, 0.17f + dip);
            Hand(g, c1, c2, 0.3f, 0.17f - dip);
            for (int i = 0; i < 3; i++)
            {
                double T = t / 1000.0 + i / 3.0, u = T - Math.Floor(T);
                bool right = (((int)Math.Floor(T)) & 1) == 0;
                float x0 = right ? -0.3f : 0.3f, x1 = -x0;
                float x = x0 + (x1 - x0) * (float)u, y = 0.14f - 0.62f * (float)(4 * u * (1 - u));
                Dot(g, Balls[i], x, y, 0.05f);
                Dot(g, Color.FromArgb(150, 255, 255, 255), x - 0.016f, y - 0.016f, 0.014f);
            }
        }

        static void PaintConsole(Graphics g, double t, Color c1, Color c2)
        {
            float bob = (float)Math.Sin(t / 700.0) * 0.006f;
            g.TranslateTransform(0, bob);
            Color shell = Mac.Mix(c1, Color.White, 0.55);
            Grad(g, shell, Mac.Mix(shell, c2, 0.35), -0.2f, 0.1f, 0.4f, 0.21f, 0.05f);
            Fill(g, Color.FromArgb(24, 30, 46), -0.115f, 0.122f, 0.23f, 0.15f, 0.014f);
            // The game: a block jumping over scrolling obstacles.
            double u = (t / 1500.0) % 1;
            float ox = 0.085f - 0.19f * (float)u, jh = 0;
            if (ox > -0.06f && ox < 0.0f) jh = 0.055f * (float)Math.Sin(Math.PI * (0.0 - ox) / 0.06);
            Color lcd = Color.FromArgb(150, 232, 170);
            Fill(g, A(lcd, 0.5), -0.1f, 0.255f, 0.2f, 0.008f, 0.003f);
            Fill(g, lcd, -0.075f, 0.226f - jh, 0.03f, 0.03f, 0.006f);
            if (ox > -0.1f) Fill(g, lcd, ox, 0.232f, 0.018f, 0.024f, 0.003f);
            Fill(g, A(lcd, 0.8), -0.09f, 0.14f, 0.012f, 0.012f, 0.002f);
            // D-pad and buttons.
            Color btn = Color.FromArgb(60, 60, 80);
            Fill(g, btn, -0.175f, 0.2f, 0.05f, 0.014f, 0.005f);
            Fill(g, btn, -0.157f, 0.182f, 0.014f, 0.05f, 0.005f);
            bool press = (int)(t / 380) % 3 == 0;
            Dot(g, press ? Color.FromArgb(255, 92, 140) : Color.FromArgb(205, 70, 110), 0.16f, 0.195f, 0.016f);
            Dot(g, !press ? Color.FromArgb(255, 214, 10) : Color.FromArgb(205, 170, 10), 0.13f, 0.225f, 0.016f);
            Hand(g, c1, c2, -0.2f, 0.285f);
            Hand(g, c1, c2, 0.2f, 0.285f);
        }

        static void PaintStretch(Graphics g, double t, Color c1, Color c2)
        {
            float a = (float)StretchAmt(t);
            Color arm = Mac.Mix(c1, c2, 0.25);
            foreach (int s in new int[] { -1, 1 })
            {
                float hx = s * (0.3f + 0.06f * a), hy = 0.21f - 0.74f * a;
                Seg(g, arm, 0.075f, s * 0.26f, 0.1f, hx, hy);
                Hand(g, c1, c2, hx, hy);
            }
        }

        static void PaintRead(Graphics g, double t, Color c1, Color c2)
        {
            Fill(g, MascotParts.Darker(c2, 0.35), -0.215f, 0.125f, 0.43f, 0.19f, 0.02f);
            PointF[] l = { new PointF(-0.2f, 0.12f), new PointF(-0.008f, 0.14f), new PointF(-0.008f, 0.3f), new PointF(-0.2f, 0.28f) };
            PointF[] r = { new PointF(0.008f, 0.14f), new PointF(0.2f, 0.12f), new PointF(0.2f, 0.28f), new PointF(0.008f, 0.3f) };
            using (SolidBrush b = new SolidBrush(Cream)) { g.FillPolygon(b, l); g.FillPolygon(b, r); }
            using (Pen p = new Pen(PageEdge, 0.006f)) { g.DrawPolygon(p, l); g.DrawPolygon(p, r); }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.16f + i * 0.03f;
                Seg(g, Color.FromArgb(110, 90, 80, 110), 0.008f, -0.17f, y, -0.04f - (i == 3 ? 0.05f : 0), y + 0.004f);
                Seg(g, Color.FromArgb(110, 90, 80, 110), 0.008f, 0.04f, y + 0.004f, 0.17f - (i == 2 ? 0.06f : 0), y);
            }
            // A page turns over every few seconds.
            double ft = (t % 3400) / 520.0;
            if (ft < 1)
            {
                float x = 0.19f * (float)Math.Cos(ft * Math.PI), lift = 0.03f * (float)Math.Sin(ft * Math.PI);
                PointF[] f = { new PointF(0.008f, 0.14f), new PointF(x, 0.12f - lift), new PointF(x, 0.28f - lift), new PointF(0.008f, 0.3f) };
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 252, 244))) g.FillPolygon(b, f);
                using (Pen p = new Pen(PageEdge, 0.006f)) g.DrawPolygon(p, f);
            }
            Hand(g, c1, c2, -0.2f, 0.29f);
            Hand(g, c1, c2, 0.2f, 0.29f);
        }

        static void PaintPhoto(Graphics g, double t, Color c1, Color c2)
        {
            double cyc = (t % 2600) / 2600.0;
            float shake = cyc > 0.7 && cyc < 0.78 ? 0.006f : 0;
            g.TranslateTransform(shake, 0.04f);
            Grad(g, Color.FromArgb(86, 90, 108), Color.FromArgb(48, 50, 66), -0.1f, -0.03f, 0.2f, 0.12f, 0.025f);
            Fill(g, Color.FromArgb(205, 208, 220), -0.1f, -0.03f, 0.2f, 0.022f, 0.011f);
            Fill(g, Color.FromArgb(255, 238, 160), 0.05f, -0.045f, 0.04f, 0.022f, 0.006f);
            Dot(g, Color.FromArgb(20, 22, 34), 0.0f, 0.03f, 0.047f);
            Dot(g, Color.FromArgb(52, 78, 140), 0.0f, 0.03f, 0.033f);
            Dot(g, Color.FromArgb(190, 255, 255, 255), -0.012f, 0.018f, 0.009f);
            Hand(g, c1, c2, -0.115f, 0.06f);
            Hand(g, c1, c2, 0.115f, 0.06f);
            double fl = (cyc - 0.72) / 0.14;
            if (fl > 0 && fl < 1)
            {
                double a = 1 - fl;
                for (int i = 0; i < 3; i++)
                {
                    float r = (0.06f + 0.16f * (float)fl) * (1 - i * 0.28f);
                    Dot(g, Color.FromArgb((int)(150 * a * (0.5 + i * 0.25)), 255, 255, 235), 0.07f, -0.035f, r);
                }
            }
        }

        // Sleep variants (the owner picks one each time it falls asleep): 1 blanket, 2 snore bubble, 3 nodding off, 4 both.
        public static void PaintSleep(Graphics g, int variant, float D, double now, Color c1, Color c2)
        {
            bool blanket = variant == 1 || variant == 4, bubble = variant == 2 || variant == 4;
            if (!blanket && !bubble) return;
            GraphicsState st = g.Save();
            g.ScaleTransform(D, D);
            if (blanket)
            {
                float by = 0.13f + (float)Math.Sin(now / 900.0) * 0.006f;
                Color a = Mac.Mix(c1, c2, 0.6), b = MascotParts.Darker(a, 0.25);
                RectangleF rc = new RectangleF(-0.38f, by, 0.76f, 0.27f);
                using (GraphicsPath p = Theme.Round(rc, 0.07f))
                {
                    using (LinearGradientBrush br = new LinearGradientBrush(RectangleF.Inflate(rc, 0.01f, 0.01f), a, b, 90f)) g.FillPath(br, p);
                    Region old = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    for (int i = 0; i < 6; i++) Seg(g, Color.FromArgb(46, 255, 255, 255), 0.03f, -0.4f + i * 0.17f, by + 0.3f, -0.2f + i * 0.17f, by - 0.04f);
                    g.Clip = old;
                    old.Dispose();
                    using (Pen pn = new Pen(Color.FromArgb(70, 255, 255, 255), 0.008f)) g.DrawPath(pn, p);
                }
            }
            if (bubble)
            {
                double cyc = (now % 4200) / 4200.0;
                float r = cyc < 0.8 ? 0.012f + 0.05f * (float)(0.5 - 0.5 * Math.Cos(cyc / 0.8 * Math.PI)) : 0.012f;
                float x = 0.075f + r * 0.5f, y = 0.045f;
                Dot(g, Color.FromArgb(70, 200, 225, 255), x, y, r);
                using (Pen pn = new Pen(Color.FromArgb(150, 235, 245, 255), 0.007f)) g.DrawEllipse(pn, x - r, y - r, r * 2, r * 2);
                Dot(g, Color.FromArgb(170, 255, 255, 255), x - r * 0.35f, y - r * 0.35f, r * 0.18f);
                if (cyc > 0.8 && cyc < 0.86)
                    using (Pen pn = new Pen(Color.FromArgb(120, 235, 245, 255), 0.007f)) { float pr = 0.06f + (float)(cyc - 0.8) * 1.2f; g.DrawEllipse(pn, x - pr, y - pr, pr * 2, pr * 2); }
            }
            g.Restore(st);
        }
    }
}
