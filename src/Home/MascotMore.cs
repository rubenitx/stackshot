// Stackshot - More characters (penguin, panda, fox, frog) and the extra pieces for the second batch of costumes.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindPenguin = 8, KindPanda = 9, KindFox = 10, KindFrog = 11;
        static readonly Color PandaInk = Color.FromArgb(38, 36, 44);

        // ---- Behind the body.
        static void PaintBackMore(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            switch (l.Kind)
            {
                case KindPanda:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        float ex = s * 0.25f * D, ey = -0.25f * D - (float)((s < 0 ? p.EarL : p.EarR) * 0.0006 * D);
                        Ellipse(g, PandaInk, ex - 0.1f * D, ey - 0.1f * D, 0.2f * D, 0.2f * D);
                    }
                    break;
                case KindFox:
                    FoxTail(g, D, c1, c2, p.Now);
                    foreach (int s in new int[] { -1, 1 }) FoxEar(g, D, s, c1, c2, s < 0 ? p.EarL : p.EarR);
                    break;
                case KindReindeer:
                    ReindeerBack(g, D, c1, c2, p);
                    break;
                case KindFrog:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        RectangleF bulb = new RectangleF(s * 0.17f * D - 0.13f * D, -0.36f * D, 0.26f * D, 0.24f * D);
                        using (GraphicsPath bp = new GraphicsPath())
                        {
                            bp.AddEllipse(bulb);
                            Fill(g, bp, bulb, Lighter(c1, 0.1), c1, 70f);
                        }
                    }
                    break;
            }
        }

        static void FoxEar(Graphics g, float D, int s, Color c1, Color c2, double twitch)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.2f * D, -0.23f * D);
            g.RotateTransform((float)(s * (12 + twitch)));
            using (GraphicsPath ear = Poly(P(-0.13f * D, 0.07f * D), P(s * 0.02f * D, -0.27f * D), P(0.13f * D, 0.07f * D)))
                Fill(g, ear, new RectangleF(-0.13f * D, -0.27f * D, 0.26f * D, 0.34f * D), c1, c2, 70f);
            using (GraphicsPath tip = Poly(P(-0.045f * D, -0.15f * D), P(s * 0.02f * D, -0.27f * D), P(0.06f * D, -0.15f * D)))
                FillSolid(g, tip, PandaInk);
            using (GraphicsPath inner = Poly(P(-0.06f * D, 0.04f * D), P(s * 0.01f * D, -0.13f * D), P(0.06f * D, 0.04f * D)))
                FillSolid(g, inner, Color.FromArgb(220, 255, 244, 230));
            g.Restore(st);
        }

        static void FoxTail(Graphics g, float D, Color c1, Color c2, double now)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0.24f * D, 0.2f * D);
            g.RotateTransform((float)(-40 + Math.Sin(now / 420.0) * 10));
            RectangleF t = new RectangleF(0, -0.09f * D, 0.36f * D, 0.18f * D);
            using (GraphicsPath tp = new GraphicsPath())
            {
                tp.AddEllipse(t);
                Fill(g, tp, t, c1, c2, 0f);
            }
            Ellipse(g, Color.FromArgb(255, 252, 245), 0.24f * D, -0.065f * D, 0.13f * D, 0.13f * D);
            g.Restore(st);
        }

        // ---- On the body, before the face.
        static void PaintBodyMore(Graphics g, MascotLook l, Geo geo, float D, GraphicsPath body)
        {
            float fy = geo.FaceY * D;
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            switch (l.Kind)
            {
                case KindPenguin:
                    Ellipse(g, Color.FromArgb(250, 250, 252), -0.25f * D, fy - 0.12f * D, 0.5f * D, 0.62f * D);
                    break;
                case KindPanda:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        GraphicsState st = g.Save();
                        g.TranslateTransform(s * geo.EyeGap * D, fy + 0.01f * D);
                        g.RotateTransform(s * -25);
                        Ellipse(g, PandaInk, -0.07f * D, -0.085f * D, 0.14f * D, 0.17f * D);
                        g.Restore(st);
                    }
                    break;
                case KindFox:
                    Ellipse(g, Color.FromArgb(240, 255, 248, 238), -0.2f * D, fy + 0.02f * D, 0.4f * D, 0.4f * D);
                    break;
            }
            g.Clip = old;
            old.Dispose();
        }

        // ---- In front of the face.
        static void PaintDetailsMore(Graphics g, MascotLook l, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            switch (l.Kind)
            {
                case KindPenguin:
                    using (GraphicsPath beak = Poly(P(-0.05f * D, fy + 0.05f * D), P(0.05f * D, fy + 0.05f * D), P(0, fy + 0.11f * D)))
                        Fill(g, beak, new RectangleF(-0.05f * D, fy + 0.05f * D, 0.1f * D, 0.06f * D), Color.FromArgb(255, 190, 60), Color.FromArgb(235, 120, 20), 90f);
                    foreach (int s in new int[] { -1, 1 })
                        Ellipse(g, Color.FromArgb(250, 150, 40), s * 0.11f * D - 0.07f * D, geo.Bottom * D - 0.03f * D, 0.14f * D, 0.06f * D);
                    break;
                case KindPanda:
                case KindFox:
                    Ellipse(g, PandaInk, -0.03f * D, fy + 0.055f * D, 0.06f * D, 0.04f * D);
                    break;
                case KindReindeer:
                    ReindeerNose(g, geo, D);
                    break;
            }
        }

        // ---- Extra costume pieces (second batch).
        public const int HatBlackHair = 26, HatBlondNinja = 27;

        static bool PaintMoreHat(Graphics g, int hat, float u, double now)
        {
            if (hat == HatBlackHair) { Hair(g, u, 5, now); return true; }
            if (hat == HatBlondNinja) { Hair(g, u, 6, now); NinjaBand(g, u, now); return true; }
            return PaintExtraHat(g, hat, u, now);
        }

        static bool MoreOutfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 15: Top(g, D, y, geo, Color.FromArgb(240, 60, 60), Color.FromArgb(180, 20, 30)); return true;
                case 16: ConsoleButtons(g, D, geo); return true;
                case 17: BellyMarks(g, D, geo); return true;
                case 18:
                    Top(g, D, y, geo, Color.FromArgb(255, 150, 40), Color.FromArgb(230, 100, 10));
                    Panels(g, D, y, w, geo, Color.FromArgb(40, 60, 140), Color.FromArgb(20, 30, 90), false);
                    return true;
            }
            return ExtraOutfit(g, l, D, y, w, geo);
        }

        // A plain shirt over the lower body.
        static void Top(Graphics g, float D, float y, Geo geo, Color a, Color b)
        {
            RectangleF r = new RectangleF(-D, y - 0.03f * D, D * 2, geo.Bottom * D - y + 0.1f * D);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(r);
                Fill(g, p, r, a, b, 90f);
            }
            Line(g, Color.FromArgb(50, 0, 0, 0), D * 0.01f, -D, y - 0.03f * D, D, y - 0.03f * D);
        }

        static void ConsoleButtons(Graphics g, float D, Geo geo)
        {
            float y = geo.Bottom * D - 0.1f * D;
            Color pad = Color.FromArgb(250, 210, 50);
            RectangleF h = new RectangleF(-0.26f * D, y - 0.018f * D, 0.12f * D, 0.036f * D), v = new RectangleF(-0.218f * D, y - 0.06f * D, 0.036f * D, 0.12f * D);
            using (GraphicsPath p = Theme.Round(h, 0.01f * D)) FillSolid(g, p, pad);
            using (GraphicsPath p = Theme.Round(v, 0.01f * D)) FillSolid(g, p, pad);
            Ellipse(g, Color.FromArgb(240, 60, 70), 0.13f * D, y - 0.05f * D, 0.07f * D, 0.07f * D);
            Ellipse(g, Color.FromArgb(60, 200, 110), 0.2f * D, y - 0.01f * D, 0.05f * D, 0.05f * D);
            Ellipse(g, Color.FromArgb(70, 120, 240), 0.06f * D, y + 0.0f * D, 0.05f * D, 0.05f * D);
        }

        static void BellyMarks(Graphics g, float D, Geo geo)
        {
            Ellipse(g, Color.FromArgb(235, 240, 232, 214), -0.22f * D, 0.0f * D, 0.44f * D, 0.4f * D);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.1f * D, y = 0.07f * D;
                using (GraphicsPath c = Poly(P(x - 0.035f * D, y), P(x, y - 0.03f * D), P(x + 0.035f * D, y), P(x, y - 0.012f * D)))
                    FillSolid(g, c, Color.FromArgb(110, 100, 90));
            }
        }

        static bool PaintMoreFace(Graphics g, MascotLook l, float D, Geo geo)
        {
            float fy = geo.FaceY * D, gap = geo.EyeGap * D;
            switch (l.Face)
            {
                case 9: // thick eyebrows
                    foreach (int s in new int[] { -1, 1 })
                    {
                        RectangleF b = new RectangleF(s * gap - 0.065f * D, fy - 0.13f * D, 0.13f * D, 0.05f * D);
                        using (GraphicsPath p = Theme.Round(b, 0.025f * D)) FillSolid(g, p, Color.FromArgb(24, 22, 30));
                    }
                    return true;
                case 10: // red cheeks
                    foreach (int s in new int[] { -1, 1 })
                        Ellipse(g, Color.FromArgb(235, 70, 60), s * (gap + 0.08f * D) - 0.06f * D, fy + 0.04f * D, 0.12f * D, 0.11f * D);
                    return true;
                case 11: // whisker marks
                    foreach (int s in new int[] { -1, 1 })
                        for (int i = -1; i <= 1; i++)
                            Line(g, Color.FromArgb(150, 60, 40, 30), D * 0.012f, s * (gap + 0.03f * D), fy + 0.07f * D + i * 0.025f * D, s * (gap + 0.13f * D), fy + 0.065f * D + i * 0.03f * D);
                    return true;
            }
            return ExtraFace(g, l, D, geo);
        }
    }
}
