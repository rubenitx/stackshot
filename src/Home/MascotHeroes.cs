// Stackshot - Superhero tribute pieces: masks, cowls, helmets and suits (drawn from scratch, no logos).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int OutfitBat = 32, OutfitSteel = 34;

        static bool PaintHeroHat(Graphics g, int hat, float u, double now)
        {
            switch (hat)
            {
                case 39: BatCowl(g, u); return true;
                case 40: SpeedCowl(g, u); return true;
                case 41: Hair(g, u, 5, now); Curl(g, u); return true;
                case 42: IronHelmet(g, u, now); return true;
            }
            return false;
        }

        // Black cowl with pointed ears; leaves the lower face uncovered.
        static void BatCowl(Graphics g, float u)
        {
            Color a = Color.FromArgb(58, 60, 74), b = Color.FromArgb(18, 18, 26);
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath ear = Poly(P(s * 0.22f * u, 0), P(s * 0.4f * u, -0.36f * u), P(s * 0.48f * u, 0.02f * u)))
                    FillSolid(g, ear, b);
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddArc(-0.68f * u, -0.12f * u, 1.36f * u, 1.0f * u, 180, 180);
                c.AddLine(P(0.68f * u, 0.38f * u), P(0.5f * u, 0.62f * u));
                c.AddBezier(P(0.5f * u, 0.62f * u), P(0.2f * u, 0.5f * u), P(-0.2f * u, 0.5f * u), P(-0.5f * u, 0.62f * u));
                c.CloseFigure();
                RectangleF r = new RectangleF(-0.68f * u, -0.12f * u, 1.36f * u, 0.74f * u);
                Fill(g, c, r, a, b, 90f);
                Shine(g, c, r, 70);
            }
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath eye = Poly(P(s * 0.08f * u, 0.4f * u), P(s * 0.3f * u, 0.34f * u), P(s * 0.26f * u, 0.46f * u)))
                    FillSolid(g, eye, Color.FromArgb(245, 245, 250));
        }

        // Red cowl with small golden lightning wings.
        static void SpeedCowl(Graphics g, float u)
        {
            RectangleF r = new RectangleF(-0.68f * u, -0.12f * u, 1.36f * u, 0.76f * u);
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddArc(-0.68f * u, -0.12f * u, 1.36f * u, 1.0f * u, 180, 180);
                c.AddLine(P(0.68f * u, 0.38f * u), P(0.5f * u, 0.64f * u));
                c.AddBezier(P(0.5f * u, 0.64f * u), P(0.2f * u, 0.52f * u), P(-0.2f * u, 0.52f * u), P(-0.5f * u, 0.64f * u));
                c.CloseFigure();
                Fill(g, c, r, Color.FromArgb(240, 60, 50), Color.FromArgb(160, 20, 24), 90f);
                Shine(g, c, r, 90);
            }
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath w = Poly(P(s * 0.62f * u, 0.24f * u), P(s * 0.86f * u, 0.1f * u), P(s * 0.74f * u, 0.22f * u), P(s * 0.92f * u, 0.16f * u), P(s * 0.64f * u, 0.38f * u)))
                    Fill(g, w, new RectangleF(-0.92f * u, 0.1f * u, 1.84f * u, 0.28f * u), Color.FromArgb(255, 226, 90), Color.FromArgb(220, 150, 20), 90f);
        }

        // The famous forehead curl over slicked black hair.
        static void Curl(Graphics g, float u)
        {
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddBezier(P(0.02f * u, 0.12f * u), P(0.12f * u, 0.14f * u), P(0.1f * u, 0.3f * u), P(-0.02f * u, 0.28f * u));
                using (Pen p = new Pen(Color.FromArgb(20, 20, 30), u * 0.05f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, c); }
            }
        }

        // Red armored helmet with a golden faceplate and glowing eyes.
        static void IronHelmet(Graphics g, float u, double now)
        {
            RectangleF r = new RectangleF(-0.68f * u, -0.12f * u, 1.36f * u, 1.02f * u);
            using (GraphicsPath p = Theme.Round(r, 0.46f * u))
            {
                Fill(g, p, r, Color.FromArgb(220, 44, 44), Color.FromArgb(130, 14, 20), 90f);
                Shine(g, p, r, 110);
            }
            RectangleF f = new RectangleF(-0.46f * u, 0.18f * u, 0.92f * u, 0.66f * u);
            using (GraphicsPath p = Theme.Round(f, 0.24f * u))
            {
                Fill(g, p, f, Color.FromArgb(255, 216, 110), Color.FromArgb(206, 140, 30), 90f);
                Stroke(g, p, Color.FromArgb(90, 120, 70, 10), u * 0.014f);
            }
            double glow = 0.75 + 0.25 * Math.Sin(now / 300.0);
            foreach (int s in new int[] { -1, 1 })
            {
                Glow(g, s * 0.2f * u, 0.4f * u, 0.36f * u, 0.2f * u, Color.FromArgb(150, 230, 255), (int)(130 * glow));
                using (GraphicsPath e = Theme.Round(new RectangleF(s * 0.2f * u - 0.12f * u, 0.37f * u, 0.24f * u, 0.07f * u), 0.03f * u))
                    FillSolid(g, e, Color.FromArgb(235, 250, 255));
            }
            Line(g, Color.FromArgb(120, 120, 70, 10), u * 0.016f, -0.16f * u, 0.66f * u, 0.16f * u, 0.66f * u);
        }

        static bool HeroOutfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 31: // red and blue web suit with a small spider
                    Top(g, D, y, geo, Color.FromArgb(225, 40, 50), Color.FromArgb(150, 16, 26));
                    foreach (int s in new int[] { -1, 1 })
                        using (GraphicsPath side = Poly(P(s * 0.2f * D, y), P(s * D, y), P(s * D, geo.Bottom * D + D), P(s * 0.26f * D, geo.Bottom * D + D)))
                            FillSolid(g, side, Color.FromArgb(40, 80, 190));
                    Ellipse(g, Color.FromArgb(20, 20, 26), -0.025f * D, y + 0.06f * D, 0.05f * D, 0.06f * D);
                    for (int i = -1; i <= 1; i += 2)
                        for (int k = 0; k < 3; k++)
                            Line(g, Color.FromArgb(20, 20, 26), D * 0.007f, i * 0.02f * D, y + 0.08f * D + k * 0.015f, i * 0.07f * D, y + 0.05f * D + k * 0.03f * D);
                    return true;
                case OutfitBat: // grey suit with a yellow utility belt
                    Top(g, D, y, geo, Color.FromArgb(120, 124, 136), Color.FromArgb(70, 72, 84));
                    using (GraphicsPath bp = Theme.Round(new RectangleF(-D * 0.5f, y + 0.14f * D, D, 0.06f * D), 0.02f * D)) FillSolid(g, bp, Color.FromArgb(240, 196, 60));
                    return true;
                case 33: // red suit with a lightning bolt
                    Top(g, D, y, geo, Color.FromArgb(235, 50, 44), Color.FromArgb(160, 20, 22));
                    Ellipse(g, Color.FromArgb(252, 240, 220), -0.07f * D, y + 0.02f * D, 0.14f * D, 0.14f * D);
                    using (GraphicsPath bolt = Poly(P(0.02f * D, y + 0.03f * D), P(-0.04f * D, y + 0.1f * D), P(0, y + 0.1f * D), P(-0.02f * D, y + 0.15f * D), P(0.05f * D, y + 0.07f * D), P(0.01f * D, y + 0.07f * D)))
                        FillSolid(g, bolt, Color.FromArgb(250, 200, 30));
                    return true;
                case OutfitSteel: // blue suit with a plain diamond crest (the red cape is drawn behind)
                    Top(g, D, y, geo, Color.FromArgb(50, 96, 220), Color.FromArgb(24, 50, 150));
                    using (GraphicsPath dia = Poly(P(-0.07f * D, y + 0.04f * D), P(0.07f * D, y + 0.04f * D), P(0.09f * D, y + 0.07f * D), P(0, y + 0.16f * D), P(-0.09f * D, y + 0.07f * D)))
                    {
                        Fill(g, dia, new RectangleF(-0.09f * D, y + 0.04f * D, 0.18f * D, 0.12f * D), Color.FromArgb(255, 220, 70), Color.FromArgb(230, 160, 20), 90f);
                        Stroke(g, dia, Color.FromArgb(200, 30, 30), D * 0.012f);
                    }
                    return true;
                case 35: // red armor with a glowing chest light
                    Top(g, D, y, geo, Color.FromArgb(215, 40, 40), Color.FromArgb(120, 12, 18));
                    Glow(g, 0, y + 0.08f * D, 0.16f * D, 0.16f * D, Color.FromArgb(150, 230, 255), 160);
                    Ellipse(g, Color.FromArgb(230, 250, 255), -0.03f * D, y + 0.05f * D, 0.06f * D, 0.06f * D);
                    return true;
            }
            return false;
        }

        static bool HeroFace(Graphics g, MascotLook l, float D, Geo geo)
        {
            if (l.Face != 18) return false;
            // Full red mask with a web pattern and big white lenses.
            float fy = geo.FaceY * D, gap = geo.EyeGap * D, hw = geo.HeadW * D / 2;
            RectangleF r = new RectangleF(-hw * 0.96f, geo.Top * D + 0.02f * D, hw * 1.92f, (geo.Bottom - geo.Top) * D * 0.78f);
            using (GraphicsPath m = Theme.Round(r, r.Width * 0.42f))
            {
                Fill(g, m, r, Color.FromArgb(232, 44, 52), Color.FromArgb(150, 16, 26), 90f);
                Region old = g.Clip;
                g.SetClip(m, CombineMode.Intersect);
                using (Pen web = new Pen(Color.FromArgb(110, 20, 10, 16), Math.Max(0.8f, D * 0.006f)))
                {
                    for (int i = -3; i <= 3; i++) g.DrawLine(web, 0, fy + 0.02f * D, i * 0.16f * D, r.Y - 0.1f * D);
                    for (int k = 1; k <= 3; k++) g.DrawEllipse(web, -k * 0.1f * D, fy + 0.02f * D - k * 0.1f * D, k * 0.2f * D, k * 0.2f * D);
                }
                g.Clip = old;
                old.Dispose();
            }
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath lens = Poly(P(s * 0.03f * D, fy + 0.03f * D), P(s * (gap + 0.09f * D), fy - 0.09f * D), P(s * (gap + 0.06f * D), fy + 0.05f * D)))
                {
                    FillSolid(g, lens, Color.FromArgb(250, 250, 252));
                    Stroke(g, lens, Color.FromArgb(20, 20, 26), D * 0.014f);
                }
            return true;
        }
    }
}
