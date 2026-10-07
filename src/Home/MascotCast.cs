// Stackshot - Fourth batch of costume pieces: more hair styles, outfits and face paint for the character tributes.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int OutfitBaldHero = 28;

        static bool PaintCastHat(Graphics g, int hat, float u, double now)
        {
            switch (hat)
            {
                case 32: Hair(g, u, 7, now); return true;                          // tall dark spikes
                case 33: Hair(g, u, 8, now); return true;                          // magenta, slicked up
                case 34: Hair(g, u, 9, now); NinjaBand(g, u, now); return true;    // dark navy + headband
                case 35: Hair(g, u, 10, now); return true;                         // short green
                case 36: Hair(g, u, 11, now); return true;                         // tall black flame
                case 37: Hair(g, u, 12, now); return true;                         // long black
                case 38: Hair(g, u, 13, now); NinjaBand(g, u, now); return true;   // silver + headband
            }
            return PaintHeroHat(g, hat, u, now);
        }

        static bool CastOutfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 22: // green jacket over a dark shirt
                    Top(g, D, y, geo, Color.FromArgb(70, 180, 90), Color.FromArgb(24, 110, 50));
                    using (GraphicsPath v = Poly(P(-0.1f * D, y - 0.03f * D), P(0, y + 0.1f * D), P(0.1f * D, y - 0.03f * D)))
                        FillSolid(g, v, Color.FromArgb(40, 44, 52));
                    return true;
                case 23: // purple shirt with a white collar
                    Top(g, D, y, geo, Color.FromArgb(120, 92, 190), Color.FromArgb(70, 50, 130));
                    Line(g, Color.FromArgb(240, 240, 248), D * 0.035f, -0.16f * D, y - 0.02f * D, 0.16f * D, y - 0.02f * D);
                    return true;
                case 24: // clown top with card suits
                    Top(g, D, y, geo, Color.FromArgb(246, 244, 250), Color.FromArgb(200, 196, 214));
                    Font f = Fonts.Get("Segoe UI Symbol", (float)Math.Round(Math.Max(5f, 0.09f * D))); // cached: drawn every frame
                    {
                        using (SolidBrush r = new SolidBrush(Color.FromArgb(220, 40, 70))) g.DrawString("\u2665", f, r, -0.2f * D, y + 0.02f * D);
                        using (SolidBrush k = new SolidBrush(Color.FromArgb(40, 30, 60))) g.DrawString("\u2660", f, k, 0.08f * D, y + 0.02f * D);
                    }
                    return true;
                case 25: // navy shirt with a high collar
                    Top(g, D, y, geo, Color.FromArgb(52, 60, 110), Color.FromArgb(24, 28, 64));
                    Line(g, Color.FromArgb(30, 34, 70), D * 0.05f, -0.18f * D, y - 0.03f * D, 0.18f * D, y - 0.03f * D);
                    return true;
                case 26: // white top with a green waist sash
                    Top(g, D, y, geo, Color.FromArgb(246, 246, 250), Color.FromArgb(206, 210, 222));
                    using (GraphicsPath s = Theme.Round(new RectangleF(-D * 0.5f, y + 0.12f * D, D, 0.07f * D), 0.02f * D))
                        Fill(g, s, new RectangleF(-D * 0.5f, y + 0.12f * D, D, 0.07f * D), Color.FromArgb(80, 180, 90), Color.FromArgb(30, 110, 50), 90f);
                    return true;
                case 27: // white chest armor with yellow shoulders
                    Top(g, D, y, geo, Color.FromArgb(248, 248, 252), Color.FromArgb(200, 204, 216));
                    foreach (int s in new int[] { -1, 1 })
                        Ellipse(g, Color.FromArgb(250, 210, 70), s * w * 0.42f - 0.08f * D, y - 0.06f * D, 0.16f * D, 0.09f * D);
                    Line(g, Color.FromArgb(60, 70, 90), D * 0.04f, -D * 0.5f, geo.Bottom * D - 0.03f * D, D * 0.5f, geo.Bottom * D - 0.03f * D);
                    return true;
                case OutfitBaldHero: // yellow suit with a zip (the white cape is drawn behind)
                    Top(g, D, y, geo, Color.FromArgb(255, 220, 60), Color.FromArgb(230, 170, 20));
                    Line(g, Color.FromArgb(180, 130, 20), D * 0.012f, 0, y, 0, geo.Bottom * D);
                    Ellipse(g, Color.FromArgb(220, 40, 40), -0.035f * D, y + 0.12f * D, 0.07f * D, 0.07f * D);
                    return true;
                case 29: // pink kimono with a hemp-leaf pattern hint
                    Top(g, D, y, geo, Color.FromArgb(250, 170, 200), Color.FromArgb(220, 110, 150));
                    for (int i = -2; i <= 2; i++)
                        using (GraphicsPath st = Star(i * 0.1f * D, y + 0.1f * D, 0.025f * D, 0.4f)) FillSolid(g, st, Color.FromArgb(120, 255, 255, 255));
                    using (GraphicsPath o = Theme.Round(new RectangleF(-D * 0.5f, y + 0.16f * D, D, 0.06f * D), 0.02f * D)) FillSolid(g, o, Color.FromArgb(60, 30, 40));
                    return true;
                case 30: // green flak vest
                    Panels(g, D, y, w, geo, Color.FromArgb(110, 140, 80), Color.FromArgb(56, 80, 40), false);
                    return true;
            }
            return HeroOutfit(g, l, D, y, w, geo);
        }

        static bool CastFace(Graphics g, MascotLook l, float D, Geo geo)
        {
            float fy = geo.FaceY * D, gap = geo.EyeGap * D;
            switch (l.Face)
            {
                case 15: // star and teardrop face paint
                    using (GraphicsPath s = Star(-gap - 0.02f * D, fy + 0.1f * D, 0.035f * D, 0.45f)) FillSolid(g, s, Color.FromArgb(200, 120, 90, 200));
                    using (GraphicsPath t = new GraphicsPath())
                    {
                        float x = gap + 0.02f * D, ty = fy + 0.08f * D;
                        t.AddBezier(P(x, ty), P(x - 0.025f * D, ty + 0.04f * D), P(x + 0.025f * D, ty + 0.04f * D), P(x, ty));
                        t.AddEllipse(x - 0.02f * D, ty + 0.02f * D, 0.04f * D, 0.04f * D);
                        FillSolid(g, t, Color.FromArgb(200, 70, 160, 220));
                    }
                    return true;
                case 16: // bamboo muzzle
                {
                    RectangleF r = new RectangleF(-0.17f * D, fy + 0.065f * D, 0.34f * D, 0.06f * D);
                    using (GraphicsPath p = Theme.Round(r, 0.03f * D)) Fill(g, p, r, Color.FromArgb(150, 200, 90), Color.FromArgb(80, 130, 40), 90f);
                    Line(g, Color.FromArgb(90, 40, 70, 20), D * 0.01f, 0.06f * D, r.Y, 0.06f * D, r.Bottom);
                    Line(g, Color.FromArgb(120, 140, 60, 40), D * 0.012f, -gap - 0.08f * D, fy + 0.095f * D, r.X, fy + 0.095f * D);
                    Line(g, Color.FromArgb(120, 140, 60, 40), D * 0.012f, r.Right, fy + 0.095f * D, gap + 0.08f * D, fy + 0.095f * D);
                    return true;
                }
                case 17: // dark mask over the lower face
                {
                    RectangleF r = new RectangleF(-geo.HeadW * D * 0.46f, fy + 0.05f * D, geo.HeadW * D * 0.92f, 0.2f * D);
                    using (GraphicsPath p = Theme.Round(r, 0.08f * D)) Fill(g, p, r, Color.FromArgb(52, 58, 84), Color.FromArgb(26, 30, 50), 90f);
                    return true;
                }
            }
            return HeroFace(g, l, D, geo);
        }
    }
}
