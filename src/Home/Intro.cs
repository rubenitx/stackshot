// Stackshot - Launch animation: the logo grows out of a single lens, clicks like a shutter and flies into the sidebar.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Stackshot
{
    // One continuous shape, every frame computed from time (no timers or tweens), closed-form springs with a slight
    // overshoot and no particle effects:
    //   0.08 s  a lens dot springs in and charges (anticipation)
    //   0.47 s  it splits into the two viewfinder corners, which travel outwards
    //   0.60 s  the tile and the frosted card bloom behind
    //   1.00 s  shutter: a short flash inside the tile and a tactile squash
    //   1.02 s  the name rises letter by letter, then the tagline
    //   1.45 s  the logo flies into the sidebar with motion blur while the backdrop fades
    // Any click or key skips it.
    public class Intro
    {
        public const double Length = 1900;
        public const double RevealAt = 1450;   // from here on the window shows through

        const string Name = "Stackshot", Tagline = "Captura, marca y comparte en segundos";

        readonly double start;
        Bitmap still;                  // the finished logo, for the flight
        public PointF Target;          // sidebar logo center (flight target)
        public float TargetSize;       // and its size

        public Intro(double now)
        {
            start = now;
        }

        public double T(double now) { return now - start; }
        public bool Done(double now) { return now - start >= Length; }

        static double Clamp(double v) { return Math.Max(0, Math.Min(1, v)); }
        static double Phase(double t, double a, double b) { return Clamp((t - a) / (b - a)); }
        static double OutCubic(double t) { double u = 1 - t; return 1 - u * u * u; }
        static double InOutCubic(double t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; }

        // Underdamped spring released at time a: 0 before, settles at 1 with a slight overshoot (~6%).
        static double Spring(double t, double a, double freq)
        {
            double x = (t - a) / 1000.0;
            if (x <= 0) return 0;
            double w = 2 * Math.PI * freq, z = 0.66, wd = w * Math.Sqrt(1 - z * z);
            return 1 - Math.Exp(-z * w * x) * (Math.Cos(wd * x) + z * w / wd * Math.Sin(wd * x));
        }

        public void Paint(Graphics g, Rectangle client, double now, float s)
        {
            double t = now - start;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            double fade = 1 - InOutCubic(Phase(t, RevealAt, Length - 80));
            if (fade <= 0.001 && t > RevealAt) return;
            using (SolidBrush bg = new SolidBrush(Color.FromArgb((int)(255 * fade), 9, 8, 15))) g.FillRectangle(bg, client);

            float L = 196 * s;
            float cx = client.Width / 2f, cy = client.Height / 2f - 40 * s;
            LogoArt.Glow(g, cx, cy, L * 4.2f, Color.FromArgb((int)(60 * Phase(t, 0, 500) * fade), LogoArt.Bg1));
            LogoArt.Glow(g, cx + L * 0.5f, cy + L * 0.4f, L * 3.2f, Color.FromArgb((int)(36 * Phase(t, 300, 900) * fade), LogoArt.Bg3));

            double fly = InOutCubic(Phase(t, RevealAt - 40, Length - 120));
            if (fly <= 0)
            {
                double lens = Spring(t, 80, 2.4) * (1 - 0.18 * Math.Sin(Math.PI * Phase(t, 380, 560)));
                double frame = Spring(t, 470, 2.0);
                double tile = Spring(t, 600, 1.8);
                double glass = Spring(t, 700, 1.7);
                // Shutter squash: a quick dip in scale that springs back.
                double click = t < 1000 ? 1 : 1 - 0.045 * Math.Exp(-(t - 1000) / 70.0) * Math.Cos((t - 1000) / 45.0);
                float size = (float)(L * click);
                RectangleF box = new RectangleF(cx - size / 2, cy - size / 2, size, size);
                LogoArt.Paint(g, box, tile, glass, frame, lens, false);
                double flash = Phase(t, 1000, 1200);
                if (flash > 0 && flash < 1)
                    using (GraphicsPath p = LogoArt.Squircle(cx, cy, size * 488 / 1024f, 5))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(80 * (1 - flash) * (1 - flash)), 255, 255, 255))) g.FillPath(b, p);
            }
            else PaintFlight(g, t, fly, cx, cy, L);

            PaintName(g, client, t, cx, cy + L * 0.62f, s);
        }

        // The finished logo flies to the sidebar; a few fading copies along the path give it motion blur.
        void PaintFlight(Graphics g, double t, double fly, float cx, float cy, float L)
        {
            if (still == null)
            {
                int px = (int)Math.Ceiling(L);
                still = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
                using (Graphics sg = Graphics.FromImage(still)) LogoArt.PaintFull(sg, new RectangleF(0, 0, px, px), false);
            }
            for (int i = 3; i >= 0; i--)
            {
                double f = i == 0 ? fly : InOutCubic(Phase(t - i * 9, RevealAt - 40, Length - 120));
                float k = (float)(1 + (TargetSize / L - 1) * f);
                float x = (float)(cx + (Target.X - cx) * f), y = (float)(cy + (Target.Y - cy) * f), w = L * k;
                RectangleF dst = new RectangleF(x - w / 2, y - w / 2, w, w);
                if (i == 0) { g.DrawImage(still, dst); continue; }
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix33 = 0.11f;
                    ia.SetColorMatrix(cm);
                    g.DrawImage(still, Rectangle.Round(dst), 0, 0, still.Width, still.Height, GraphicsUnit.Pixel, ia);
                }
            }
        }

        // Each letter rises from a mask with a short trail; the tagline follows.
        static void PaintName(Graphics g, Rectangle client, double t, float cx, float top, float s)
        {
            double leave = Phase(t, RevealAt - 120, RevealAt + 160);
            if (t < 1020 || leave >= 1) return;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Font f = Fonts.Get(Fonts.DisplaySemibold, 44 * s);
            using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                float total = g.MeasureString(Name, f, PointF.Empty, sf).Width, x = cx - total / 2;
                float lh = f.GetHeight(g);
                GraphicsState st = g.Save();
                g.SetClip(new RectangleF(0, top - 4 * s, client.Width, lh + 10 * s));
                for (int i = 0; i < Name.Length; i++)
                {
                    string ch = Name[i].ToString();
                    float w = g.MeasureString(ch, f, PointF.Empty, sf).Width;
                    double p = Spring(t, 1020 + i * 30, 2.6);
                    float dy = (float)((1 - p) * lh * 0.9);
                    int a = (int)(255 * Clamp(p * 1.4) * (1 - leave));
                    if (a > 0)
                    {
                        if (p < 0.9)
                            using (SolidBrush trail = new SolidBrush(Color.FromArgb(a / 4, 190, 200, 255)))
                                g.DrawString(ch, f, trail, x, top + dy + 8 * s, sf);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 244, 244, 252))) g.DrawString(ch, f, b, x, top + dy, sf);
                    }
                    x += w;
                }
                g.Restore(st);
                double tag = OutCubic(Phase(t, 1180, 1460)) * (1 - leave);
                if (tag > 0)
                {
                    Font f2 = Fonts.Get(Mac.TextFont, 15 * s);
                    float tw = g.MeasureString(Tagline, f2, PointF.Empty, sf).Width;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(175 * tag), 196, 200, 222)))
                        g.DrawString(Tagline, f2, b, cx - tw / 2, top + lh + 12 * s + (float)((1 - tag) * 8 * s), sf);
                }
            }
        }

        public static void PaintGlow(Graphics g, float x, float y, float d, Color c)
        {
            LogoArt.Glow(g, x, y, d, c);
        }

        public void Dispose()
        {
            if (still != null) { still.Dispose(); still = null; }
        }
    }
}
