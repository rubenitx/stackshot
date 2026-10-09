// Stackshot - The reindeer, and the pieces for the tribute to Tony Tony Chopper: pink doctor's top hat with the white
// cross, blue nose and shorts.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindReindeer = 12, HatDoctor = 43, OutfitShorts = 36, FaceBlueNose = 19;
        static readonly Color Antler1 = Color.FromArgb(232, 196, 140), Antler2 = Color.FromArgb(170, 118, 66);

        // ---- Behind the body: antlers (they come out past a hat, as if through it) and small ears out to the sides.
        static void ReindeerBack(Graphics g, float D, Color c1, Color c2, MascotPose p)
        {
            foreach (int s in new int[] { -1, 1 })
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.15f * D, -0.25f * D);
                g.RotateTransform((float)(s * (s < 0 ? p.EarL : p.EarR) * 0.15));
                Antler(g, D, s, 0.052f, Antler2);
                Antler(g, D, s, 0.03f, Antler1);
                g.Restore(st);
            }
            foreach (int s in new int[] { -1, 1 })
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.34f * D, -0.14f * D);
                g.RotateTransform((float)(s * (24 + (s < 0 ? p.EarL : p.EarR) * 0.6)));
                RectangleF ear = new RectangleF(-0.09f * D, -0.045f * D, 0.18f * D, 0.09f * D);
                using (GraphicsPath ep = new GraphicsPath())
                {
                    ep.AddEllipse(ear);
                    Fill(g, ep, ear, c1, c2, 90f);
                    Stroke(g, ep, Color.FromArgb(40, 0, 0, 0), D * 0.008f);
                }
                Ellipse(g, Color.FromArgb(150, 255, 196, 196), s * 0.005f * D - 0.05f * D, -0.022f * D, 0.1f * D, 0.044f * D);
                g.Restore(st);
            }
        }

        // One antler: a beam curving up and out, with two tines; drawn twice (dark outline, light core) for volume.
        static void Antler(Graphics g, float D, int s, float width, Color c)
        {
            using (Pen pen = new Pen(c, D * width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                using (GraphicsPath beam = new GraphicsPath())
                {
                    beam.AddBezier(P(0, 0.02f * D), P(s * 0.05f * D, -0.08f * D), P(s * 0.15f * D, -0.12f * D), P(s * 0.27f * D, -0.24f * D));
                    g.DrawPath(pen, beam);
                }
                using (GraphicsPath tine = new GraphicsPath())
                {
                    tine.AddBezier(P(s * 0.08f * D, -0.08f * D), P(s * 0.07f * D, -0.14f * D), P(s * 0.08f * D, -0.19f * D), P(s * 0.11f * D, -0.24f * D));
                    g.DrawPath(pen, tine);
                }
                using (GraphicsPath tine = new GraphicsPath())
                {
                    tine.AddBezier(P(s * 0.17f * D, -0.14f * D), P(s * 0.17f * D, -0.2f * D), P(s * 0.19f * D, -0.25f * D), P(s * 0.21f * D, -0.3f * D));
                    g.DrawPath(pen, tine);
                }
            }
        }

        // ---- On the body: a lighter muzzle.
        static void ReindeerMuzzle(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            Ellipse(g, Color.FromArgb(120, 255, 236, 210), -0.13f * D, fy + 0.02f * D, 0.26f * D, 0.16f * D);
        }

        // ---- In front of the face: a dark, soft nose.
        static void ReindeerNose(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            RectangleF r = new RectangleF(-0.04f * D, fy + 0.025f * D, 0.08f * D, 0.055f * D);
            using (GraphicsPath n = new GraphicsPath())
            {
                n.AddEllipse(r);
                Fill(g, n, r, Color.FromArgb(96, 64, 52), Color.FromArgb(40, 26, 22), 90f);
            }
            Ellipse(g, Color.FromArgb(130, 255, 255, 255), -0.022f * D, fy + 0.032f * D, 0.024f * D, 0.013f * D);
        }

        // The big, glossy blue nose.
        static void BlueNose(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            RectangleF r = new RectangleF(-0.066f * D, fy - 0.004f * D, 0.132f * D, 0.1f * D);
            using (GraphicsPath n = new GraphicsPath())
            {
                n.AddEllipse(r);
                Fill(g, n, r, Color.FromArgb(110, 176, 255), Color.FromArgb(28, 78, 206), 90f);
                Stroke(g, n, Color.FromArgb(90, 10, 30, 90), D * 0.008f);
            }
            Ellipse(g, Color.FromArgb(220, 255, 255, 255), -0.04f * D, fy + 0.01f * D, 0.042f * D, 0.024f * D);
            Ellipse(g, Color.FromArgb(120, 255, 255, 255), 0.02f * D, fy + 0.05f * D, 0.016f * D, 0.011f * D);
        }

        // ---- The doctor's top hat: pink, a darker band and the white cross, tilted like an X.
        static void DoctorHat(Graphics g, float u)
        {
            Color p1 = Color.FromArgb(255, 140, 186), p2 = Color.FromArgb(226, 72, 134);
            RectangleF crown = new RectangleF(-0.33f * u, -0.78f * u, 0.66f * u, 0.74f * u);
            using (GraphicsPath c = new GraphicsPath())
            {
                // Slightly wider at the top, with a soft, rounded crown, as it is drawn in the series.
                c.AddBezier(P(-0.3f * u, -0.04f * u), P(-0.31f * u, -0.3f * u), P(-0.36f * u, -0.6f * u), P(-0.33f * u, -0.72f * u));
                c.AddBezier(P(-0.33f * u, -0.72f * u), P(-0.2f * u, -0.8f * u), P(0.2f * u, -0.8f * u), P(0.33f * u, -0.72f * u));
                c.AddBezier(P(0.33f * u, -0.72f * u), P(0.36f * u, -0.6f * u), P(0.31f * u, -0.3f * u), P(0.3f * u, -0.04f * u));
                c.CloseFigure();
                Fill(g, c, crown, p1, p2, 0f);
                Shine(g, c, crown, 70);
                Stroke(g, c, Color.FromArgb(70, 120, 20, 60), u * 0.014f);
            }
            RectangleF band = new RectangleF(-0.305f * u, -0.17f * u, 0.61f * u, 0.11f * u);
            using (GraphicsPath b = new GraphicsPath())
            {
                b.AddRectangle(band);
                Fill(g, b, band, Color.FromArgb(214, 60, 120), Color.FromArgb(170, 36, 92), 90f);
            }
            GraphicsState st = g.Save();
            g.TranslateTransform(0, -0.45f * u);
            g.RotateTransform(45);
            float arm = 0.17f * u, th = 0.08f * u;
            foreach (RectangleF bar in new RectangleF[] { new RectangleF(-arm, -th / 2, arm * 2, th), new RectangleF(-th / 2, -arm, th, arm * 2) })
            {
                RectangleF sh = bar;
                sh.Offset(0.01f * u, 0.012f * u);
                using (GraphicsPath sp = Theme.Round(sh, th * 0.3f)) FillSolid(g, sp, Color.FromArgb(50, 90, 10, 40));
                using (GraphicsPath bp = Theme.Round(bar, th * 0.3f)) FillSolid(g, bp, Color.FromArgb(252, 252, 255));
            }
            g.Restore(st);
            RectangleF brim = new RectangleF(-0.56f * u, -0.1f * u, 1.12f * u, 0.18f * u);
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddEllipse(brim);
                Fill(g, bp, brim, p1, Darker(p2, 0.12), 90f);
                Stroke(g, bp, Color.FromArgb(70, 120, 20, 60), u * 0.012f);
            }
        }

        // ---- Shorts on the lower body (clipped to it by the caller).
        static void Shorts(Graphics g, float D, Geo geo)
        {
            float top = geo.Bottom * D - 0.13f * D;
            RectangleF r = new RectangleF(-D, top, D * 2, 0.4f * D);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(r);
                Fill(g, p, r, Color.FromArgb(178, 62, 78), Color.FromArgb(112, 28, 44), 90f);
            }
            Line(g, Color.FromArgb(70, 40, 0, 10), D * 0.012f, -D, top, D, top);
            Line(g, Color.FromArgb(80, 40, 0, 10), D * 0.01f, 0, top + 0.05f * D, 0, top + 0.4f * D);
        }

        static bool ReindeerHat(Graphics g, int hat, float u)
        {
            if (hat != HatDoctor) return false;
            DoctorHat(g, u);
            return true;
        }

        static bool ReindeerOutfit(Graphics g, MascotLook l, float D, Geo geo)
        {
            if (l.Outfit != OutfitShorts) return false;
            Shorts(g, D, geo);
            return true;
        }

        static bool ReindeerFace(Graphics g, MascotLook l, float D, Geo geo)
        {
            if (l.Face != FaceBlueNose) return false;
            BlueNose(g, geo, D);
            return true;
        }
    }
}
