// Stackshot - Third batch of costume pieces: helmets, a spark crown, beards, armor and a space suit.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Archetypes only (spartan, space marine, knight, astronaut, wizard...): no names, logos or emblems of real works or brands.
    public static partial class MascotParts
    {
        public const int HatMarine = 28, HatKnight = 29, HatAstronaut = 30, HatSpark = 31;

        static bool PaintExtraHat(Graphics g, int hat, float u, double now)
        {
            switch (hat)
            {
                case HatMarine: MarineHelmet(g, u); return true;
                case HatKnight: KnightHelmet(g, u, now); return true;
                case HatAstronaut: SpaceBubble(g, u); return true;
                case HatSpark: Spark(g, u, now); return true;
            }
            return PaintCastHat(g, hat, u, now);
        }

        static void MarineHelmet(Graphics g, float u)
        {
            RectangleF r = new RectangleF(-0.7f * u, -0.14f * u, 1.4f * u, 1.04f * u);
            using (GraphicsPath p = Theme.Round(r, 0.46f * u))
            {
                Fill(g, p, r, Color.FromArgb(96, 140, 76), Color.FromArgb(40, 66, 34), 90f);
                Shine(g, p, r, 90);
                Stroke(g, p, Color.FromArgb(90, 0, 0, 0), u * 0.016f);
            }
            RectangleF v = new RectangleF(-0.52f * u, 0.26f * u, 1.04f * u, 0.26f * u);
            using (GraphicsPath p = Theme.Round(v, 0.1f * u))
            {
                Fill(g, p, v, Color.FromArgb(255, 214, 110), Color.FromArgb(196, 116, 20), 90f);
                Shine(g, p, v, 150);
            }
            foreach (int s in new int[] { -1, 1 })
                for (int i = 0; i < 3; i++)
                    Line(g, Color.FromArgb(120, 20, 30, 16), u * 0.025f, s * 0.42f * u, 0.64f * u + i * 0.07f * u, s * 0.22f * u, 0.64f * u + i * 0.07f * u);
        }

        static void KnightHelmet(Graphics g, float u, double now)
        {
            float sway = (float)Math.Sin(now / 500.0) * 0.03f * u;
            using (GraphicsPath plume = new GraphicsPath())
            {
                plume.AddBezier(P(0, -0.1f * u), P(0.1f * u, -0.45f * u), P(0.4f * u + sway, -0.45f * u), P(0.55f * u + sway, -0.2f * u));
                using (Pen p = new Pen(Color.FromArgb(220, 40, 50), u * 0.13f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, plume); }
            }
            RectangleF r = new RectangleF(-0.68f * u, -0.12f * u, 1.36f * u, 1.0f * u);
            using (GraphicsPath p = Theme.Round(r, 0.5f * u))
            {
                Fill(g, p, r, Color.FromArgb(232, 236, 244), Color.FromArgb(130, 138, 156), 90f);
                Shine(g, p, r, 130);
                Stroke(g, p, Color.FromArgb(90, 40, 44, 56), u * 0.016f);
            }
            using (SolidBrush slit = new SolidBrush(Color.FromArgb(28, 30, 40)))
            {
                using (GraphicsPath h = Theme.Round(new RectangleF(-0.46f * u, 0.36f * u, 0.92f * u, 0.09f * u), 0.04f * u)) g.FillPath(slit, h);
                using (GraphicsPath v = Theme.Round(new RectangleF(-0.045f * u, 0.36f * u, 0.09f * u, 0.38f * u), 0.04f * u)) g.FillPath(slit, v);
            }
            for (int i = -2; i <= 2; i++)
                if (i != 0) Ellipse(g, Color.FromArgb(80, 40, 44, 56), i * 0.12f * u - 0.015f * u, 0.6f * u, 0.03f * u, 0.03f * u);
        }

        static void SpaceBubble(Graphics g, float u)
        {
            RectangleF r = new RectangleF(-0.8f * u, -0.24f * u, 1.6f * u, 1.42f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(r);
                FillSolid(g, p, Color.FromArgb(34, 200, 230, 255));
                Stroke(g, p, Color.FromArgb(170, 235, 245, 255), u * 0.03f);
            }
            using (Pen hi = new Pen(Color.FromArgb(150, 255, 255, 255), u * 0.05f))
            {
                hi.StartCap = LineCap.Round; hi.EndCap = LineCap.Round;
                g.DrawArc(hi, r.X + u * 0.12f, r.Y + u * 0.12f, r.Width - u * 0.24f, r.Height - u * 0.24f, 200, 50);
            }
            Ellipse(g, Color.FromArgb(160, 255, 255, 255), 0.3f * u, -0.04f * u, 0.12f * u, 0.07f * u);
        }

        // A warm starburst floating over the head, slowly turning.
        static void Spark(Graphics g, float u, double now)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0, -0.2f * u);
            g.RotateTransform((float)(now / 40.0 % 360));
            Color c = Color.FromArgb(222, 120, 86);
            Glow(g, 0, 0, u * 0.9f, u * 0.9f, Color.FromArgb(255, 170, 130), 70);
            int n = 10;
            using (Pen p = new Pen(c, u * 0.075f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                for (int i = 0; i < n; i++)
                {
                    double a = Math.PI * 2 * i / n;
                    float len = (i % 2 == 0 ? 0.26f : 0.18f) * u;
                    g.DrawLine(p, (float)Math.Cos(a) * 0.04f * u, (float)Math.Sin(a) * 0.04f * u, (float)Math.Cos(a) * len, (float)Math.Sin(a) * len);
                }
            }
            g.Restore(st);
        }

        static bool ExtraOutfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 19: // leather strap and a shoulder guard
                    Line(g, Color.FromArgb(120, 72, 40), D * 0.06f, -w * 0.5f, y - 0.04f * D, w * 0.5f, geo.Bottom * D);
                    Ellipse(g, Color.FromArgb(150, 150, 160), -w * 0.62f, y - 0.1f * D, 0.24f * D, 0.16f * D);
                    Ellipse(g, Color.FromArgb(200, 205, 215), -w * 0.58f, y - 0.09f * D, 0.12f * D, 0.06f * D);
                    return true;
                case 20: // armored chest plate
                    Top(g, D, y, geo, Color.FromArgb(96, 140, 76), Color.FromArgb(40, 66, 34));
                    Line(g, Color.FromArgb(110, 20, 30, 16), D * 0.02f, 0, y, 0, geo.Bottom * D);
                    foreach (int s in new int[] { -1, 1 }) Line(g, Color.FromArgb(90, 20, 30, 16), D * 0.016f, s * 0.08f * D, y + 0.07f * D, s * 0.22f * D, y + 0.07f * D);
                    return true;
                case 21: // space suit
                    Top(g, D, y, geo, Color.FromArgb(248, 248, 252), Color.FromArgb(196, 202, 216));
                    using (GraphicsPath p = Theme.Round(new RectangleF(0.07f * D, y + 0.03f * D, 0.12f * D, 0.08f * D), 0.015f * D)) FillSolid(g, p, Color.FromArgb(70, 110, 230));
                    Ellipse(g, Color.FromArgb(240, 80, 70), -0.17f * D, y + 0.04f * D, 0.05f * D, 0.05f * D);
                    return true;
            }
            return CastOutfit(g, l, D, y, w, geo);
        }

        static bool ExtraFace(Graphics g, MascotLook l, float D, Geo geo)
        {
            float fy = geo.FaceY * D, gap = geo.EyeGap * D;
            Color beard = Color.FromArgb(52, 40, 34);
            switch (l.Face)
            {
                case 12: // red stripe over one eye and a dark beard
                    using (GraphicsPath s = new GraphicsPath())
                    {
                        s.AddBezier(P(-gap - 0.01f * D, fy - 0.24f * D), P(-gap + 0.02f * D, fy - 0.1f * D), P(-gap - 0.03f * D, fy + 0.05f * D), P(-gap - 0.02f * D, fy + 0.16f * D));
                        using (Pen p = new Pen(Color.FromArgb(190, 200, 30, 30), D * 0.045f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, s); }
                    }
                    using (GraphicsPath b = new GraphicsPath())
                    {
                        b.AddBezier(P(-0.12f * D, fy + 0.07f * D), P(-0.12f * D, fy + 0.22f * D), P(0.12f * D, fy + 0.22f * D), P(0.12f * D, fy + 0.07f * D));
                        b.AddBezier(P(0.12f * D, fy + 0.07f * D), P(0.07f * D, fy + 0.15f * D), P(-0.07f * D, fy + 0.15f * D), P(-0.12f * D, fy + 0.07f * D));
                        b.CloseFigure();
                        FillSolid(g, b, beard);
                    }
                    Line(g, beard, D * 0.025f, -0.06f * D, fy + 0.075f * D, 0.06f * D, fy + 0.075f * D);
                    return true;
                case 13: // long white wizard beard
                    using (GraphicsPath b = new GraphicsPath())
                    {
                        b.AddBezier(P(-0.16f * D, fy + 0.07f * D), P(-0.16f * D, fy + 0.25f * D), P(-0.03f * D, fy + 0.34f * D), P(0, fy + 0.36f * D));
                        b.AddBezier(P(0, fy + 0.36f * D), P(0.03f * D, fy + 0.34f * D), P(0.16f * D, fy + 0.25f * D), P(0.16f * D, fy + 0.07f * D));
                        b.CloseFigure();
                        Fill(g, b, new RectangleF(-0.16f * D, fy + 0.07f * D, 0.32f * D, 0.29f * D), Color.FromArgb(252, 252, 255), Color.FromArgb(200, 206, 220), 90f);
                    }
                    foreach (int s in new int[] { -1, 1 })
                        using (GraphicsPath m = new GraphicsPath())
                        {
                            m.AddBezier(P(0, fy + 0.08f * D), P(s * 0.06f * D, fy + 0.04f * D), P(s * 0.13f * D, fy + 0.08f * D), P(s * 0.15f * D, fy + 0.04f * D));
                            m.AddBezier(P(s * 0.15f * D, fy + 0.04f * D), P(s * 0.13f * D, fy + 0.12f * D), P(s * 0.05f * D, fy + 0.11f * D), P(0, fy + 0.1f * D));
                            m.CloseFigure();
                            FillSolid(g, m, Color.FromArgb(236, 238, 246));
                        }
                    return true;
                case 14: // command prompt where the mouth goes
                    Font f = Fonts.Get("Consolas", (float)Math.Round(Math.Max(5f, 0.09f * D))); // cached: drawn every frame
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 240, 160)))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(">_", f, b, new RectangleF(-0.12f * D, fy + 0.05f * D, 0.24f * D, 0.12f * D), sf);
                    }
                    return true;
            }
            return CastFace(g, l, D, geo);
        }

    }
}
