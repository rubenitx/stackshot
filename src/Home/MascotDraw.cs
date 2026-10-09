// Stackshot - Mascot drawing: character bodies, hats, outfits and face accessories, all vector, at any size.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Animated values the body parts need. The graphics origin is the body center; sizes are fractions of D.
    public class MascotPose
    {
        public double Now, EarL, EarR, Antenna, Pulse, Wave, WaveRot;
        public bool Sleeping, HideHands;
    }

    public static partial class MascotParts
    {
        static readonly Color Ink = Color.FromArgb(36, 32, 56);
        static readonly Color Pink = Color.FromArgb(255, 150, 185);
        static readonly Color Gold1 = Color.FromArgb(255, 226, 92), Gold2 = Color.FromArgb(214, 140, 10);

        // ---- Behind the body: tails, wings, ears, capes, antenna.
        public static void PaintBack(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            Geo geo = GeoFor(l.Kind);
            if (l.Outfit == 4) Cape(g, Color.FromArgb(40, 36, 52), Color.FromArgb(14, 12, 20), geo, D, p.Now);
            else if (l.Outfit == 5 || l.Outfit == OutfitRedCoat) Cape(g, Color.FromArgb(239, 68, 68), Color.FromArgb(153, 27, 27), geo, D, p.Now);
            else if (l.Outfit == OutfitScoutCape) Cape(g, Color.FromArgb(52, 120, 80), Color.FromArgb(20, 60, 40), geo, D, p.Now);
            else if (l.Outfit == OutfitBaldHero) Cape(g, Color.FromArgb(252, 252, 255), Color.FromArgb(206, 210, 224), geo, D, p.Now);
            else if (l.Outfit == OutfitBat) Cape(g, Color.FromArgb(44, 44, 56), Color.FromArgb(12, 12, 18), geo, D, p.Now);
            else if (l.Outfit == OutfitSteel) Cape(g, Color.FromArgb(235, 50, 50), Color.FromArgb(150, 20, 24), geo, D, p.Now);
            switch (l.Kind)
            {
                case 0:
                    foreach (int side in new int[] { -1, 1 })
                    {
                        RectangleF ear = new RectangleF(side < 0 ? -0.36f * D - D * 0.05f : 0.36f * D - D * 0.02f, -D * 0.1f, D * 0.07f, D * 0.2f);
                        using (GraphicsPath ep = Theme.Round(ear, D * 0.035f)) Fill(g, ep, ear, Darker(c2, 0.25), Darker(c2, 0.5), 90f);
                    }
                    Antenna(g, D, geo, c1, c2, p);
                    break;
                case 1:
                    Tail(g, D, c2, p.Now, false);
                    foreach (int side in new int[] { -1, 1 }) CatEar(g, D, side, c1, c2, side < 0 ? p.EarL : p.EarR);
                    break;
                case 2:
                    foreach (int side in new int[] { -1, 1 }) BunnyEar(g, D, side, c1, c2, side < 0 ? p.EarL : p.EarR);
                    break;
                case KindDog: DogTail(g, D, c2, p.Now); break;
                default: PaintBackMore(g, l, D, c1, c2, p); break;
                case 5:
                    Tail(g, D, c2, p.Now, true);
                    foreach (int side in new int[] { -1, 1 }) Wing(g, D, side, c2, p.Now, p.Sleeping);
                    foreach (int side in new int[] { -1, 1 }) Horn(g, D, side);
                    break;
            }
        }

        static void Antenna(Graphics g, float D, Geo geo, Color c1, Color c2, MascotPose p)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0, geo.Top * D + D * 0.01f);
            g.RotateTransform((float)p.Antenna);
            float len = D * 0.15f;
            Line(g, Darker(c2, 0.3), Math.Max(1.2f, D * 0.022f), 0, 0, 0, -len);
            float r = D * 0.042f, gr = r * (2.6f + (float)p.Pulse * 0.6f);
            Color glow = Lighter(c1, 0.35);
            Glow(g, 0, -len, gr * 2, gr * 2, glow, (int)(90 + 70 * p.Pulse));
            Ellipse(g, Lighter(c1, 0.55), -r, -len - r, r * 2, r * 2);
            Ellipse(g, Color.FromArgb(200, 255, 255, 255), -r * 0.45f, -len - r * 0.6f, r * 0.6f, r * 0.6f);
            g.Restore(st);
        }

        static void CatEar(Graphics g, float D, int s, Color c1, Color c2, double twitch)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.19f * D, -0.24f * D);
            g.RotateTransform((float)(s * (8 + twitch)));
            using (GraphicsPath ear = Poly(P(-0.12f * D, 0.06f * D), P(s * 0.01f * D, -0.2f * D), P(0.12f * D, 0.06f * D)))
            {
                Fill(g, ear, new RectangleF(-0.12f * D, -0.2f * D, 0.24f * D, 0.26f * D), c1, c2, 70f);
                Stroke(g, ear, Mac.Mix(c1, c2, 0.5), D * 0.03f);
            }
            using (GraphicsPath inner = Poly(P(-0.06f * D, 0.03f * D), P(s * 0.008f * D, -0.12f * D), P(0.06f * D, 0.03f * D)))
                FillSolid(g, inner, Color.FromArgb(200, Pink));
            g.Restore(st);
        }

        static void BunnyEar(Graphics g, float D, int s, Color c1, Color c2, double flop)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.12f * D, -0.22f * D);
            g.RotateTransform((float)(s * (10 + flop)));
            RectangleF ear = new RectangleF(-0.065f * D, -0.42f * D, 0.13f * D, 0.46f * D);
            using (GraphicsPath ep = new GraphicsPath())
            {
                ep.AddEllipse(ear);
                Fill(g, ep, ear, c1, c2, 80f);
            }
            RectangleF inner = new RectangleF(-0.035f * D, -0.36f * D, 0.07f * D, 0.32f * D);
            Ellipse(g, Color.FromArgb(190, Pink), inner.X, inner.Y, inner.Width, inner.Height);
            g.Restore(st);
        }

        static void Horn(Graphics g, float D, int s)
        {
            using (GraphicsPath h = new GraphicsPath())
            {
                h.AddBezier(P(s * 0.08f * D, -0.25f * D), P(s * 0.1f * D, -0.36f * D), P(s * 0.18f * D, -0.42f * D), P(s * 0.25f * D, -0.44f * D));
                h.AddBezier(P(s * 0.25f * D, -0.44f * D), P(s * 0.22f * D, -0.36f * D), P(s * 0.22f * D, -0.3f * D), P(s * 0.2f * D, -0.24f * D));
                h.CloseFigure();
                Fill(g, h, new RectangleF(-0.26f * D, -0.45f * D, 0.52f * D, 0.22f * D), Color.FromArgb(255, 244, 214), Color.FromArgb(210, 170, 110), 90f);
            }
        }

        static void Wing(Graphics g, float D, int s, Color c2, double now, bool sleeping)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.28f * D, -0.02f * D);
            g.RotateTransform((float)(s * (sleeping ? -6 : Math.Sin(now / 170.0) * 14 - 4)));
            // Bat-like wing: a bony top edge and a membrane with two scallops.
            PointF tip = P(s * 0.3f * D, -0.24f * D), f1 = P(s * 0.3f * D, -0.04f * D), f2 = P(s * 0.17f * D, -0.01f * D), root = P(s * 0.03f * D, 0.05f * D);
            using (GraphicsPath w = new GraphicsPath())
            {
                w.AddBezier(P(0, -0.02f * D), P(s * 0.08f * D, -0.16f * D), P(s * 0.2f * D, -0.24f * D), tip);
                w.AddLine(tip, f1);
                w.AddBezier(f1, P(s * 0.26f * D, -0.08f * D), P(s * 0.2f * D, -0.07f * D), f2);
                w.AddBezier(f2, P(s * 0.12f * D, -0.05f * D), P(s * 0.06f * D, -0.03f * D), root);
                w.CloseFigure();
                Fill(g, w, new RectangleF(-0.31f * D, -0.25f * D, 0.62f * D, 0.31f * D), Lighter(c2, 0.3), Darker(c2, 0.05), 90f);
                Stroke(g, w, Darker(c2, 0.35), D * 0.012f);
            }
            Line(g, Darker(c2, 0.3), D * 0.01f, 0, -0.02f * D, f1.X, f1.Y);
            Line(g, Darker(c2, 0.3), D * 0.01f, 0, -0.02f * D, f2.X, f2.Y);
            g.Restore(st);
        }

        static void Tail(Graphics g, float D, Color c2, double now, bool dragon)
        {
            float sway = (float)Math.Sin(now / 520.0) * 0.05f * D;
            using (GraphicsPath t = new GraphicsPath())
            {
                t.AddBezier(P(0.22f * D, 0.24f * D), P(0.46f * D, 0.26f * D), P(0.52f * D, 0.02f * D), P(0.42f * D + sway, -0.1f * D));
                using (Pen p = new Pen(dragon ? Darker(c2, 0.05) : c2, D * (dragon ? 0.085f : 0.07f)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = dragon ? LineCap.Triangle : LineCap.Round;
                    g.DrawPath(p, t);
                }
            }
            if (dragon)
            {
                float tx = 0.42f * D + sway, ty = -0.1f * D;
                using (GraphicsPath sp = Poly(P(tx - 0.05f * D, ty + 0.02f * D), P(tx + 0.02f * D, ty - 0.1f * D), P(tx + 0.06f * D, ty + 0.03f * D)))
                    FillSolid(g, sp, Darker(c2, 0.25));
            }
            else Ellipse(g, Lighter(c2, 0.35), 0.42f * D + sway - 0.035f * D, -0.135f * D, 0.07f * D, 0.07f * D);
        }

        static void Cape(Graphics g, Color ca, Color cb, Geo geo, float D, double now)
        {
            float y0 = geo.NeckY * D - 0.04f * D, w0 = geo.NeckW * D * 0.5f;
            float flow = (float)Math.Sin(now / 600.0) * 0.03f * D;
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddLine(P(-w0, y0), P(w0, y0));
                c.AddBezier(P(w0, y0), P(w0 + 0.12f * D, y0 + 0.14f * D), P(0.36f * D + flow, geo.Bottom * D + 0.05f * D), P(0.4f * D + flow, geo.Bottom * D + 0.1f * D));
                c.AddBezier(P(0.4f * D + flow, geo.Bottom * D + 0.1f * D), P(0.15f * D, geo.Bottom * D + 0.04f * D), P(-0.15f * D, geo.Bottom * D + 0.04f * D), P(-0.4f * D + flow, geo.Bottom * D + 0.1f * D));
                c.AddBezier(P(-0.4f * D + flow, geo.Bottom * D + 0.1f * D), P(-0.36f * D + flow, geo.Bottom * D + 0.05f * D), P(-w0 - 0.12f * D, y0 + 0.14f * D), P(-w0, y0));
                c.CloseFigure();
                RectangleF r = new RectangleF(-0.42f * D, y0, 0.84f * D, geo.Bottom * D + 0.12f * D - y0);
                Fill(g, c, r, ca, cb, 90f);
            }
        }

        // ---- The body itself. Returns the face plate to clip the eyes to (the robot's visor), or null.
        public static GraphicsPath PaintBody(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            Geo geo = GeoFor(l.Kind);
            float W = geo.HeadW * D, top = geo.Top * D, bottom = geo.Bottom * D;
            if (l.Kind == 0)
            {
                float H = bottom - top;
                RectangleF head = new RectangleF(-W / 2, top, W, H);
                using (GraphicsPath hp = Theme.Round(head, H * 0.42f))
                {
                    Fill(g, hp, head, c1, c2, 55f);
                    Shine(g, hp, head, 110);
                    Stroke(g, hp, Color.FromArgb(70, 255, 255, 255), Math.Max(1f, D * 0.008f));
                }
                float vw = W * 0.8f, vh = H * 0.6f;
                RectangleF visor = new RectangleF(-vw / 2, -vh / 2 - D * 0.012f, vw, vh);
                GraphicsPath vp = Theme.Round(visor, vh * 0.42f);
                Fill(g, vp, visor, Color.FromArgb(14, 16, 30), Color.FromArgb(24, 28, 50), 90f);
                Region old = g.Clip;
                g.SetClip(vp, CombineMode.Intersect);
                using (GraphicsPath glass = Poly(P(-vw * 0.2f, -vh), P(vw * 0.02f, -vh), P(-vw * 0.3f, vh), P(-vw * 0.52f, vh)))
                    FillSolid(g, glass, Color.FromArgb(12, 255, 255, 255));
                g.Clip = old;
                old.Dispose();
                return vp;
            }
            GraphicsPath body = BodyPath(l.Kind, geo, D, p.Now);
            RectangleF b = body.GetBounds();
            if (l.Kind == 3)
            {
                Glow(g, 0, (top + bottom) / 2, W * 1.5f, (bottom - top) * 1.4f, Lighter(c1, 0.3), 60);
                Fill(g, body, b, Color.FromArgb(238, Lighter(c1, 0.2)), Color.FromArgb(238, c2), 70f);
            }
            else Fill(g, body, b, c1, c2, 55f);
            if (l.Kind == 1 || l.Kind == 2 || l.Kind == 5 || l.Kind == KindDog || l.Kind == KindReindeer)
            {
                Region old = g.Clip;
                g.SetClip(body, CombineMode.Intersect);
                Color belly = l.Kind == 5 ? Color.FromArgb(170, 255, 240, 200) : Color.FromArgb(120, 255, 255, 255);
                Ellipse(g, belly, -0.17f * D, 0.06f * D, 0.34f * D, 0.3f * D);
                g.Clip = old;
                old.Dispose();
            }
            if (l.Kind == 4)
            {
                // Bubbles inside the jelly.
                Ellipse(g, Color.FromArgb(70, 255, 255, 255), 0.18f * D, 0.12f * D, 0.06f * D, 0.06f * D);
                Ellipse(g, Color.FromArgb(55, 255, 255, 255), 0.25f * D, 0.04f * D, 0.035f * D, 0.035f * D);
            }
            if (l.Kind == KindDog) DogMuzzle(g, geo, D);
            if (l.Kind == KindReindeer) ReindeerMuzzle(g, geo, D);
            if (l.Kind >= KindPenguin && l.Kind <= KindFox) PaintBodyMore(g, l, geo, D, body);
            Shine(g, body, b, l.Kind == 4 ? 150 : 105);
            Stroke(g, body, Color.FromArgb(60, 255, 255, 255), Math.Max(1f, D * 0.008f));
            body.Dispose();
            return null;
        }

        static GraphicsPath BodyPath(int kind, Geo geo, float D, double now)
        {
            float W = geo.HeadW * D, top = geo.Top * D, bottom = geo.Bottom * D;
            if (kind == 3)
            {
                GraphicsPath p = new GraphicsPath();
                float r = W / 2;
                p.AddArc(-r, top, W, W, 180, 180);
                int n = 24;
                PointF[] hem = new PointF[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float x = r - W * i / n;
                    double wave = Math.Sin(i / (double)n * Math.PI * 6 + now / 260.0);
                    hem[i] = P(x, bottom - 0.03f * D + (float)(wave * 0.03 * D));
                }
                p.AddLine(P(r, top + r), hem[0]);
                p.AddLines(hem);
                p.CloseFigure();
                return p;
            }
            if (kind == 4)
            {
                GraphicsPath p = Squircle(new RectangleF(-W / 2, -0.21f * D, W, bottom + 0.21f * D), 2.3);
                p.FillMode = FillMode.Winding;
                p.StartFigure();
                p.AddBezier(P(-0.13f * D, -0.17f * D), P(-0.06f * D, -0.24f * D), P(-0.03f * D, -0.33f * D), P(0.04f * D, -0.33f * D));
                p.AddBezier(P(0.04f * D, -0.33f * D), P(0.01f * D, -0.27f * D), P(0.08f * D, -0.22f * D), P(0.14f * D, -0.17f * D));
                p.CloseFigure();
                return p;
            }
            return Squircle(new RectangleF(-W / 2, top, W, bottom - top), kind == 2 ? 2.2 : 2.5);
        }

        // Small details in front of the face (whiskers, nostrils).
        public static void PaintDetails(Graphics g, MascotLook l, float D, Color c2, MascotPose p)
        {
            Geo geo = GeoFor(l.Kind);
            float fy = geo.FaceY * D;
            if (l.Kind == KindDog) { DogEars(g, D, c2, p); DogNose(g, geo, D); }
            PaintDetailsMore(g, l, geo, D);
            if (l.Kind == 1)
            {
                foreach (int s in new int[] { -1, 1 })
                    for (int i = -1; i <= 1; i++)
                        Line(g, Color.FromArgb(110, Ink), D * 0.008f, s * 0.2f * D, fy + 0.09f * D + i * 0.02f * D, s * 0.32f * D, fy + 0.08f * D + i * 0.035f * D);
            }
            else if (l.Kind == 5)
            {
                Ellipse(g, Color.FromArgb(110, Ink), -0.035f * D, fy + 0.075f * D, 0.015f * D, 0.012f * D);
                Ellipse(g, Color.FromArgb(110, Ink), 0.02f * D, fy + 0.075f * D, 0.015f * D, 0.012f * D);
            }
        }

        // ---- Hands and paws; the right one waves (wave 0-1).
        public static void PaintHands(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            if (p.HideHands) return;
            Geo geo = GeoFor(l.Kind);
            foreach (int side in new int[] { -1, 1 })
            {
                double phase = side * 0.9;
                float hy = geo.HandY * D + (float)(Math.Sin(p.Now / 2600.0 * Math.PI * 2 + phase) * D * 0.02);
                float hx = side * geo.HandX * D;
                float rot = 0;
                if (side > 0 && p.Wave > 0)
                {
                    hy -= (float)(D * 0.2 * p.Wave);
                    hx += (float)(D * 0.03 * p.Wave);
                    rot = (float)p.WaveRot;
                }
                GraphicsState st = g.Save();
                g.TranslateTransform(hx, hy);
                g.RotateTransform(rot);
                if (l.Kind == 0)
                {
                    RectangleF hand = new RectangleF(-D * 0.05f, -D * 0.06f, D * 0.1f, D * 0.12f);
                    using (GraphicsPath hp = Theme.Round(hand, D * 0.05f))
                    {
                        Fill(g, hp, hand, c1, c2, 60f);
                        Stroke(g, hp, Color.FromArgb(60, 255, 255, 255), Math.Max(1f, D * 0.006f));
                    }
                }
                else
                {
                    float w = l.Kind == 3 ? 0.1f : 0.105f, h = l.Kind == 3 ? 0.13f : 0.09f;
                    RectangleF paw = new RectangleF(-w * D / 2, -h * D / 2, w * D, h * D);
                    if (l.Kind == 3) g.RotateTransform(side * 25);
                    using (GraphicsPath pp = new GraphicsPath())
                    {
                        pp.AddEllipse(paw);
                        Fill(g, pp, paw, Mac.Mix(c1, c2, 0.4), c2, 70f);
                        Stroke(g, pp, Color.FromArgb(50, 255, 255, 255), Math.Max(1f, D * 0.006f));
                    }
                }
                g.Restore(st);
            }
        }

        // ---- Outfits in front of the body.
        public static void PaintOutfit(Graphics g, MascotLook l, float D, double now)
        {
            if (l.Outfit == 0) return;
            Geo geo = GeoFor(l.Kind);
            float y = geo.NeckY * D, w = geo.NeckW * D;
            switch (l.Outfit)
            {
                case 1: Scarf(g, D, y, w, now); break;
                case 2: BowTie(g, D, y, Color.FromArgb(225, 29, 72), Color.FromArgb(150, 15, 50)); break;
                case 3: Tie(g, D, y); break;
                case 4:
                    foreach (int s in new int[] { -1, 1 })
                        using (GraphicsPath c = Poly(P(s * 0.04f * D, y - 0.02f * D), P(s * w * 0.55f, y - 0.16f * D), P(s * w * 0.5f, y + 0.05f * D)))
                        {
                            Fill(g, c, new RectangleF(-w * 0.6f, y - 0.17f * D, w * 1.2f, 0.24f * D), Color.FromArgb(220, 38, 38), Color.FromArgb(110, 10, 20), 90f);
                            Stroke(g, c, Color.FromArgb(20, 14, 24), D * 0.012f);
                        }
                    break;
                case 5:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        float cx = s * w * 0.32f;
                        Ellipse(g, Gold2, cx - 0.035f * D, y - 0.035f * D, 0.07f * D, 0.07f * D);
                        Ellipse(g, Gold1, cx - 0.025f * D, y - 0.03f * D, 0.05f * D, 0.05f * D);
                    }
                    break;
                case 6: Bell(g, D, y, w); break;
                case 7: Chain(g, D, y, w); break;
                default: PaintAnimeOutfit(g, l, D, y, w); break;
            }
        }

        static void Scarf(Graphics g, float D, float y, float w, double now)
        {
            RectangleF band = new RectangleF(-w * 0.52f, y - 0.05f * D, w * 1.04f, 0.1f * D);
            float sway = (float)Math.Sin(now / 480.0) * 4;
            GraphicsState st = g.Save();
            g.TranslateTransform(-w * 0.24f, y + 0.02f * D);
            g.RotateTransform(8 + sway);
            RectangleF tail = new RectangleF(-0.045f * D, 0, 0.09f * D, 0.2f * D);
            using (GraphicsPath tp = Theme.Round(tail, 0.02f * D)) Striped(g, tp, tail, true);
            for (int i = 0; i < 4; i++) Line(g, Color.FromArgb(220, 38, 38), D * 0.012f, -0.03f * D + i * 0.02f * D, 0.2f * D, -0.03f * D + i * 0.02f * D, 0.235f * D);
            g.Restore(st);
            using (GraphicsPath bp = Theme.Round(band, 0.05f * D)) Striped(g, bp, band, false);
        }

        static void Striped(Graphics g, GraphicsPath p, RectangleF r, bool horizontal)
        {
            FillSolid(g, p, Color.FromArgb(225, 45, 55));
            Region old = g.Clip;
            g.SetClip(p, CombineMode.Intersect);
            float step = Math.Max(2f, (horizontal ? r.Height : r.Width) / 5f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 245, 250)))
            {
                for (float t = 0; t < (horizontal ? r.Height : r.Width); t += step * 2)
                {
                    if (horizontal) g.FillRectangle(b, r.X, r.Y + t + step, r.Width, step);
                    else g.FillRectangle(b, r.X + t + step, r.Y, step, r.Height);
                }
            }
            g.Clip = old;
            old.Dispose();
            Stroke(g, p, Color.FromArgb(70, 0, 0, 0), Math.Max(0.8f, r.Height * 0.06f));
        }

        static void BowTie(Graphics g, float D, float y, Color a, Color b)
        {
            float s = 0.09f * D;
            using (GraphicsPath l = Poly(P(0, y), P(-s * 1.2f, y - s * 0.65f), P(-s * 1.2f, y + s * 0.65f)))
            using (GraphicsPath r = Poly(P(0, y), P(s * 1.2f, y - s * 0.65f), P(s * 1.2f, y + s * 0.65f)))
            {
                RectangleF box = new RectangleF(-s * 1.2f, y - s * 0.65f, s * 2.4f, s * 1.3f);
                Fill(g, l, box, a, b, 90f);
                Fill(g, r, box, a, b, 90f);
                Stroke(g, l, Darker(b, 0.3), D * 0.01f);
                Stroke(g, r, Darker(b, 0.3), D * 0.01f);
            }
            RectangleF knot = new RectangleF(-s * 0.32f, y - s * 0.36f, s * 0.64f, s * 0.72f);
            using (GraphicsPath k = Theme.Round(knot, s * 0.18f)) Fill(g, k, knot, Lighter(a, 0.15), b, 90f);
        }

        static void Tie(Graphics g, float D, float y)
        {
            Color a = Color.FromArgb(59, 130, 246), b = Color.FromArgb(30, 64, 175);
            using (GraphicsPath knot = Poly(P(-0.04f * D, y - 0.035f * D), P(0.04f * D, y - 0.035f * D), P(0.028f * D, y + 0.02f * D), P(-0.028f * D, y + 0.02f * D)))
                Fill(g, knot, new RectangleF(-0.04f * D, y - 0.04f * D, 0.08f * D, 0.07f * D), a, b, 90f);
            using (GraphicsPath tie = Poly(P(-0.026f * D, y + 0.02f * D), P(0.026f * D, y + 0.02f * D), P(0.05f * D, y + 0.15f * D), P(0, y + 0.2f * D), P(-0.05f * D, y + 0.15f * D)))
            {
                RectangleF r = new RectangleF(-0.05f * D, y + 0.02f * D, 0.1f * D, 0.18f * D);
                Fill(g, tie, r, a, b, 90f);
                Region old = g.Clip;
                g.SetClip(tie, CombineMode.Intersect);
                for (int i = 0; i < 5; i++) Line(g, Color.FromArgb(120, 255, 255, 255), D * 0.01f, -0.06f * D, y + 0.05f * D + i * 0.04f * D, 0.06f * D, y + 0.02f * D + i * 0.04f * D);
                g.Clip = old;
                old.Dispose();
            }
        }

        static void Bell(Graphics g, float D, float y, float w)
        {
            RectangleF band = new RectangleF(-w * 0.48f, y - 0.025f * D, w * 0.96f, 0.05f * D);
            using (GraphicsPath bp = Theme.Round(band, 0.025f * D)) Fill(g, bp, band, Color.FromArgb(244, 63, 94), Color.FromArgb(190, 18, 60), 90f);
            float r = 0.05f * D, cy = y + 0.05f * D;
            using (GraphicsPath bell = new GraphicsPath())
            {
                bell.AddEllipse(-r, cy - r, r * 2, r * 2);
                Fill(g, bell, new RectangleF(-r, cy - r, r * 2, r * 2), Gold1, Gold2, 60f);
            }
            Line(g, Color.FromArgb(150, 120, 70, 0), D * 0.01f, 0, cy, 0, cy + r * 0.8f);
            Ellipse(g, Color.FromArgb(150, 120, 70, 0), -r * 0.25f, cy - r * 0.05f, r * 0.5f, r * 0.3f);
            Ellipse(g, Color.FromArgb(200, 255, 255, 255), -r * 0.6f, cy - r * 0.65f, r * 0.45f, r * 0.35f);
        }

        static void Chain(Graphics g, float D, float y, float w)
        {
            int n = 13;
            for (int i = 0; i <= n; i++)
            {
                double t = i / (double)n;
                float x = (float)((t - 0.5) * w * 0.85);
                float cy = y - 0.03f * D + (float)(Math.Sin(t * Math.PI) * 0.1 * D);
                RectangleF link = new RectangleF(x - 0.022f * D, cy - 0.014f * D, 0.044f * D, 0.028f * D);
                using (GraphicsPath lp = new GraphicsPath())
                {
                    lp.AddEllipse(link);
                    Stroke(g, lp, i % 2 == 0 ? Gold2 : Gold1, D * 0.012f);
                }
            }
            float py = y + 0.1f * D, r = 0.055f * D;
            using (GraphicsPath med = new GraphicsPath())
            {
                med.AddEllipse(-r, py - r * 0.6f, r * 2, r * 2);
                Fill(g, med, new RectangleF(-r, py - r * 0.6f, r * 2, r * 2), Gold1, Gold2, 60f);
                Stroke(g, med, Darker(Gold2, 0.2), D * 0.008f);
            }
            Font f = Fonts.Get("Segoe UI Black", (float)Math.Round(Math.Max(4f, r * 1.4f))); // cached: drawn every frame
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 90, 40, 0)))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("S", f, b, new RectangleF(-r, py - r * 0.6f, r * 2, r * 2), sf);
            }
        }

        // ---- Face accessories, over the eyes.
        public static void PaintFaceAccessory(Graphics g, MascotLook l, float D)
        {
            if (l.Face == 0) return;
            Geo geo = GeoFor(l.Kind);
            float fy = geo.FaceY * D, gap = geo.EyeGap * D;
            // The robot wears them over a dark visor: frames and dark pieces switch to light tones to stay visible.
            bool visor = l.Kind == 0;
            Color frame = visor ? Color.FromArgb(225, 214, 222, 240) : Color.FromArgb(20, 20, 28);
            Color dark = visor ? Color.FromArgb(70, 72, 88) : Color.FromArgb(24, 24, 30);
            switch (l.Face)
            {
                case 8: Blindfold(g, D, geo); break;
                default: PaintMoreFace(g, l, D, geo); break;
                case 1:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        RectangleF lens = new RectangleF(s * gap - 0.085f * D, fy - 0.05f * D, 0.17f * D, 0.105f * D);
                        using (GraphicsPath lp = Theme.Round(lens, 0.045f * D))
                        {
                            Fill(g, lp, lens, Color.FromArgb(250, 40, 44, 60), Color.FromArgb(250, 10, 10, 18), 90f);
                            Region old = g.Clip;
                            g.SetClip(lp, CombineMode.Intersect);
                            Line(g, Color.FromArgb(70, 255, 255, 255), D * 0.018f, lens.X + lens.Width * 0.2f, lens.Bottom, lens.X + lens.Width * 0.6f, lens.Y);
                            g.Clip = old;
                            old.Dispose();
                            if (visor) Stroke(g, lp, frame, D * 0.012f);
                        }
                    }
                    Line(g, frame, D * 0.02f, -gap + 0.08f * D, fy - 0.03f * D, gap - 0.08f * D, fy - 0.03f * D);
                    break;
                case 2:
                {
                    float px = 0.026f * D;
                    string[] rows = { "1111111111111", "0111110111110", "0110010110010", "0011100011100" };
                    using (SolidBrush b = new SolidBrush(visor ? Color.FromArgb(235, 238, 250) : Color.FromArgb(14, 14, 18)))
                    using (SolidBrush w = new SolidBrush(visor ? Color.FromArgb(40, 44, 70) : Color.White))
                    {
                        float x0 = -6.5f * px, y0 = fy - 1.6f * px;
                        for (int r = 0; r < rows.Length; r++)
                            for (int c = 0; c < rows[r].Length; c++)
                                if (rows[r][c] == '1') g.FillRectangle(b, x0 + c * px, y0 + r * px, px + 0.5f, px + 0.5f);
                        g.FillRectangle(w, x0 + 2 * px, y0 + px, px, px);
                        g.FillRectangle(w, x0 + 8 * px, y0 + px, px, px);
                    }
                    break;
                }
                case 3:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        float r = 0.072f * D;
                        Ellipse(g, Color.FromArgb(40, 180, 220, 255), s * gap - r, fy - r, r * 2, r * 2);
                        using (GraphicsPath c = new GraphicsPath())
                        {
                            c.AddEllipse(s * gap - r, fy - r, r * 2, r * 2);
                            Stroke(g, c, Color.FromArgb(120, 72, 30), D * 0.016f);
                        }
                    }
                    Line(g, Color.FromArgb(120, 72, 30), D * 0.014f, -gap + 0.07f * D, fy - 0.01f * D, gap - 0.07f * D, fy - 0.01f * D);
                    break;
                case 4:
                {
                    float r = 0.07f * D;
                    Ellipse(g, Color.FromArgb(45, 200, 230, 255), gap - r, fy - r, r * 2, r * 2);
                    using (GraphicsPath c = new GraphicsPath())
                    {
                        c.AddEllipse(gap - r, fy - r, r * 2, r * 2);
                        Stroke(g, c, Gold2, D * 0.016f);
                    }
                    using (GraphicsPath chain = new GraphicsPath())
                    {
                        chain.AddBezier(P(gap + r * 0.7f, fy + r * 0.7f), P(gap + r * 1.2f, fy + 0.15f * D), P(gap + 0.02f * D, fy + 0.2f * D), P(gap - 0.03f * D, fy + 0.26f * D));
                        Stroke(g, chain, Gold1, D * 0.007f);
                    }
                    break;
                }
                case 5:
                    using (GraphicsPath m = new GraphicsPath(FillMode.Alternate))
                    {
                        m.AddBezier(P(-gap - 0.13f * D, fy - 0.02f * D), P(-gap - 0.1f * D, fy - 0.11f * D), P(gap + 0.1f * D, fy - 0.11f * D), P(gap + 0.13f * D, fy - 0.02f * D));
                        m.AddBezier(P(gap + 0.13f * D, fy - 0.02f * D), P(gap + 0.12f * D, fy + 0.08f * D), P(0.03f * D, fy + 0.07f * D), P(0, fy + 0.03f * D));
                        m.AddBezier(P(0, fy + 0.03f * D), P(-0.03f * D, fy + 0.07f * D), P(-gap - 0.12f * D, fy + 0.08f * D), P(-gap - 0.13f * D, fy - 0.02f * D));
                        m.CloseFigure();
                        m.AddEllipse(-gap - 0.045f * D, fy - 0.04f * D, 0.09f * D, 0.075f * D);
                        m.AddEllipse(gap - 0.045f * D, fy - 0.04f * D, 0.09f * D, 0.075f * D);
                        Fill(g, m, new RectangleF(-gap - 0.13f * D, fy - 0.11f * D, gap * 2 + 0.26f * D, 0.19f * D), Color.FromArgb(124, 58, 237), Color.FromArgb(46, 16, 101), 90f);
                    }
                    break;
                case 6:
                    Line(g, dark, D * 0.014f, -0.3f * D, fy - 0.14f * D, 0.3f * D, fy + 0.04f * D);
                    using (GraphicsPath patch = new GraphicsPath())
                    {
                        patch.AddEllipse(-gap - 0.065f * D, fy - 0.06f * D, 0.13f * D, 0.115f * D);
                        FillSolid(g, patch, dark);
                        if (visor) Stroke(g, patch, frame, D * 0.01f);
                    }
                    break;
                case 7:
                    foreach (int s in new int[] { -1, 1 })
                        using (GraphicsPath m = new GraphicsPath())
                        {
                            float y = fy + 0.1f * D;
                            m.AddBezier(P(0, y), P(s * 0.05f * D, y - 0.04f * D), P(s * 0.12f * D, y + 0.01f * D), P(s * 0.15f * D, y - 0.03f * D));
                            m.AddBezier(P(s * 0.15f * D, y - 0.03f * D), P(s * 0.13f * D, y + 0.05f * D), P(s * 0.05f * D, y + 0.04f * D), P(0, y + 0.02f * D));
                            m.CloseFigure();
                            FillSolid(g, m, visor ? Color.FromArgb(205, 160, 115) : Color.FromArgb(70, 40, 20));
                        }
                    break;
            }
        }

        // ---- Hats, anchored at the top of the head. u is the hat width unit.
        public static void PaintHat(Graphics g, MascotLook l, int hat, float D, double now)
        {
            if (hat == 0) return;
            Geo geo = GeoFor(l.Kind);
            float u = geo.HatW * D;
            GraphicsState st = g.Save();
            g.TranslateTransform(0, geo.Top * D + 0.02f * D);
            switch (hat)
            {
                case 1: Cap(g, u); break;
                case 2: Beanie(g, u); break;
                case 3: Witch(g, u); break;
                case 4: Pumpkin(g, u, now); break;
                case 5: Horns(g, u); break;
                case 6: TopHat(g, u); break;
                case 7: Party(g, u); break;
                case 8: Cowboy(g, u); break;
                case 9: Headphones(g, u, geo, D); break;
                case 10: g.TranslateTransform(u * 0.3f, u * 0.02f); g.RotateTransform(-14); BowTie(g, u * 1.25f, 0, Color.FromArgb(244, 114, 182), Color.FromArgb(190, 24, 93)); break;
                case 11: Santa(g, u); break;
                case 12: Sprout(g, u, now); break;
                case 13: Crown(g, u); break;
                case 14: Halo(g, u, now); break;
                case 15: Beret(g, u); break;
                case 16: Chef(g, u); break;
                case 17: Flowers(g, u, now); break;
                default: PaintAnimeHat(g, hat, u, now); break;
            }
            g.Restore(st);
        }

        static void Cap(Graphics g, float u)
        {
            RectangleF dome = new RectangleF(-0.44f * u, -0.38f * u, 0.88f * u, 0.7f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(dome, 180, 180);
                p.CloseFigure();
                Fill(g, p, new RectangleF(dome.X, dome.Y, dome.Width, dome.Height / 2), Color.FromArgb(248, 82, 82), Color.FromArgb(185, 28, 28), 90f);
                Shine(g, p, new RectangleF(dome.X, dome.Y, dome.Width, dome.Height / 2), 90);
            }
            Line(g, Color.FromArgb(90, 0, 0, 0), u * 0.012f, 0, -0.38f * u, 0, -0.03f * u);
            RectangleF brim = new RectangleF(0.05f * u, -0.06f * u, 0.58f * u, 0.11f * u);
            using (GraphicsPath b = new GraphicsPath())
            {
                b.AddEllipse(brim);
                Fill(g, b, brim, Color.FromArgb(220, 38, 38), Color.FromArgb(127, 29, 29), 90f);
            }
            Ellipse(g, Color.FromArgb(127, 29, 29), -0.035f * u, -0.41f * u, 0.07f * u, 0.05f * u);
        }

        static void Beanie(Graphics g, float u)
        {
            RectangleF dome = new RectangleF(-0.46f * u, -0.46f * u, 0.92f * u, 0.86f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(dome, 180, 180);
                p.CloseFigure();
                RectangleF r = new RectangleF(dome.X, dome.Y, dome.Width, dome.Height / 2);
                Fill(g, p, r, Color.FromArgb(129, 140, 248), Color.FromArgb(67, 56, 202), 90f);
                Shine(g, p, r, 70);
            }
            RectangleF band = new RectangleF(-0.49f * u, -0.08f * u, 0.98f * u, 0.17f * u);
            using (GraphicsPath b = Theme.Round(band, 0.08f * u))
            {
                Fill(g, b, band, Color.FromArgb(165, 180, 252), Color.FromArgb(99, 102, 241), 90f);
                Region old = g.Clip;
                g.SetClip(b, CombineMode.Intersect);
                for (float x = band.X + 0.06f * u; x < band.Right; x += 0.08f * u)
                    Line(g, Color.FromArgb(60, 30, 27, 75), u * 0.014f, x, band.Y, x, band.Bottom);
                g.Clip = old;
                old.Dispose();
            }
            float r2 = 0.11f * u;
            Ellipse(g, Color.FromArgb(240, 242, 255), -r2, -0.46f * u - r2 * 1.3f, r2 * 2, r2 * 2);
            Ellipse(g, Color.FromArgb(200, 210, 230), -r2 * 0.4f, -0.46f * u - r2 * 0.6f, r2 * 0.9f, r2 * 0.7f);
        }

        static void Witch(Graphics g, float u)
        {
            RectangleF brim = new RectangleF(-0.66f * u, -0.1f * u, 1.32f * u, 0.22f * u);
            using (GraphicsPath b = new GraphicsPath())
            {
                b.AddEllipse(brim);
                Fill(g, b, brim, Color.FromArgb(76, 29, 149), Color.FromArgb(30, 10, 60), 90f);
            }
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddLine(P(-0.32f * u, 0), P(0.32f * u, 0));
                c.AddBezier(P(0.32f * u, 0), P(0.25f * u, -0.35f * u), P(0.2f * u, -0.62f * u), P(0.42f * u, -0.86f * u));
                c.AddBezier(P(0.42f * u, -0.86f * u), P(0.1f * u, -0.78f * u), P(-0.12f * u, -0.55f * u), P(-0.32f * u, 0));
                c.CloseFigure();
                RectangleF r = new RectangleF(-0.33f * u, -0.87f * u, 0.76f * u, 0.88f * u);
                Fill(g, c, r, Color.FromArgb(109, 40, 217), Color.FromArgb(46, 16, 101), 30f);
                Shine(g, c, r, 60);
            }
            RectangleF band = new RectangleF(-0.31f * u, -0.13f * u, 0.62f * u, 0.11f * u);
            using (GraphicsPath bp = Theme.Round(band, 0.03f * u)) FillSolid(g, bp, Color.FromArgb(249, 115, 22));
            RectangleF buckle = new RectangleF(-0.06f * u, -0.145f * u, 0.12f * u, 0.14f * u);
            using (GraphicsPath k = Theme.Round(buckle, 0.02f * u)) Stroke(g, k, Gold1, u * 0.025f);
            using (GraphicsPath s = Star(0.14f * u, -0.42f * u, 0.06f * u, 0.45f)) FillSolid(g, s, Color.FromArgb(253, 224, 71));
        }

        static void Pumpkin(Graphics g, float u, double now)
        {
            RectangleF body = new RectangleF(-0.42f * u, -0.5f * u, 0.84f * u, 0.56f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(body);
                Fill(g, p, body, Color.FromArgb(253, 160, 50), Color.FromArgb(220, 80, 10), 90f);
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (Pen rib = new Pen(Color.FromArgb(90, 150, 50, 0), u * 0.02f))
                {
                    g.DrawEllipse(rib, -0.25f * u, body.Y, 0.5f * u, body.Height);
                    g.DrawEllipse(rib, -0.1f * u, body.Y, 0.2f * u, body.Height);
                }
                g.Clip = old;
                old.Dispose();
                Shine(g, p, body, 80);
            }
            // Jack-o'-lantern face with a flickering candle glow.
            double flick = 0.75 + 0.25 * Math.Sin(now / 90.0) * Math.Sin(now / 37.0);
            Color glow = Color.FromArgb((int)(255 * flick), 255, 214, 90);
            float cy = -0.24f * u;
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath e = Poly(P(s * 0.17f * u, cy - 0.07f * u), P(s * 0.09f * u, cy + 0.03f * u), P(s * 0.24f * u, cy + 0.03f * u)))
                    FillSolid(g, e, glow);
            using (GraphicsPath m = Poly(P(-0.2f * u, cy + 0.09f * u), P(-0.12f * u, cy + 0.13f * u), P(-0.06f * u, cy + 0.09f * u), P(0, cy + 0.14f * u),
                                         P(0.06f * u, cy + 0.09f * u), P(0.12f * u, cy + 0.13f * u), P(0.2f * u, cy + 0.09f * u), P(0.12f * u, cy + 0.19f * u), P(-0.12f * u, cy + 0.19f * u)))
                FillSolid(g, m, glow);
            RectangleF stem = new RectangleF(-0.045f * u, -0.62f * u, 0.09f * u, 0.15f * u);
            using (GraphicsPath sp = Theme.Round(stem, 0.03f * u)) Fill(g, sp, stem, Color.FromArgb(120, 82, 40), Color.FromArgb(70, 45, 20), 0f);
            GraphicsState st = g.Save();
            g.TranslateTransform(0.07f * u, -0.56f * u);
            g.RotateTransform(-25);
            Ellipse(g, Color.FromArgb(34, 197, 94), 0, -0.04f * u, 0.16f * u, 0.08f * u);
            g.Restore(st);
        }

        static void Horns(Graphics g, float u)
        {
            foreach (int s in new int[] { -1, 1 })
                using (GraphicsPath h = new GraphicsPath())
                {
                    h.AddBezier(P(s * 0.18f * u, 0.02f * u), P(s * 0.22f * u, -0.18f * u), P(s * 0.38f * u, -0.3f * u), P(s * 0.5f * u, -0.38f * u));
                    h.AddBezier(P(s * 0.5f * u, -0.38f * u), P(s * 0.44f * u, -0.22f * u), P(s * 0.42f * u, -0.08f * u), P(s * 0.36f * u, 0.04f * u));
                    h.CloseFigure();
                    RectangleF r = new RectangleF(-0.52f * u, -0.4f * u, 1.04f * u, 0.45f * u);
                    Fill(g, h, r, Color.FromArgb(248, 82, 82), Color.FromArgb(127, 29, 29), 90f);
                    Stroke(g, h, Color.FromArgb(90, 60, 10, 10), u * 0.012f);
                }
        }

        static void TopHat(Graphics g, float u)
        {
            RectangleF cyl = new RectangleF(-0.3f * u, -0.66f * u, 0.6f * u, 0.62f * u);
            using (GraphicsPath c = Theme.Round(cyl, 0.05f * u))
            {
                Fill(g, c, cyl, Color.FromArgb(55, 55, 66), Color.FromArgb(14, 14, 20), 0f);
                Shine(g, c, cyl, 50);
            }
            RectangleF band = new RectangleF(-0.3f * u, -0.2f * u, 0.6f * u, 0.11f * u);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 38, 38))) g.FillRectangle(b, band);
            RectangleF brim = new RectangleF(-0.52f * u, -0.08f * u, 1.04f * u, 0.16f * u);
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddEllipse(brim);
                Fill(g, bp, brim, Color.FromArgb(50, 50, 60), Color.FromArgb(10, 10, 14), 90f);
            }
        }

        static void Party(Graphics g, float u)
        {
            using (GraphicsPath c = Poly(P(-0.28f * u, 0), P(0.28f * u, 0), P(0.06f * u, -0.78f * u)))
            {
                FillSolid(g, c, Color.FromArgb(236, 72, 153));
                Region old = g.Clip;
                g.SetClip(c, CombineMode.Intersect);
                for (int i = -4; i < 8; i++)
                    Line(g, Color.FromArgb(250, 204, 21), u * 0.07f, -0.4f * u, -i * 0.16f * u, 0.4f * u, -i * 0.16f * u - 0.3f * u);
                g.Clip = old;
                old.Dispose();
                Shine(g, c, new RectangleF(-0.28f * u, -0.78f * u, 0.56f * u, 0.78f * u), 70);
            }
            Ellipse(g, Color.FromArgb(56, 189, 248), 0.06f * u - 0.07f * u, -0.85f * u, 0.14f * u, 0.14f * u);
            Color[] conf = { Color.FromArgb(250, 204, 21), Color.FromArgb(34, 197, 94), Color.FromArgb(56, 189, 248) };
            for (int i = 0; i < 6; i++)
            {
                float x = (float)Math.Cos(i * 1.9) * 0.48f * u, y = -0.2f * u - (float)Math.Abs(Math.Sin(i * 2.3)) * 0.5f * u;
                Ellipse(g, conf[i % 3], x, y, 0.04f * u, 0.04f * u);
            }
        }

        static void Cowboy(Graphics g, float u)
        {
            Color a = Color.FromArgb(180, 104, 50), b = Color.FromArgb(110, 58, 20);
            using (GraphicsPath brim = new GraphicsPath())
            {
                brim.AddBezier(P(-0.7f * u, -0.18f * u), P(-0.5f * u, 0.12f * u), P(0.5f * u, 0.12f * u), P(0.7f * u, -0.18f * u));
                brim.AddBezier(P(0.7f * u, -0.18f * u), P(0.5f * u, 0.02f * u), P(-0.5f * u, 0.02f * u), P(-0.7f * u, -0.18f * u));
                brim.CloseFigure();
                Fill(g, brim, new RectangleF(-0.7f * u, -0.18f * u, 1.4f * u, 0.3f * u), a, b, 90f);
            }
            using (GraphicsPath crown = new GraphicsPath())
            {
                crown.AddLine(P(-0.3f * u, 0), P(-0.34f * u, -0.42f * u));
                crown.AddBezier(P(-0.34f * u, -0.42f * u), P(-0.15f * u, -0.52f * u), P(-0.05f * u, -0.38f * u), P(0, -0.44f * u));
                crown.AddBezier(P(0, -0.44f * u), P(0.05f * u, -0.38f * u), P(0.15f * u, -0.52f * u), P(0.34f * u, -0.42f * u));
                crown.AddLine(P(0.34f * u, -0.42f * u), P(0.3f * u, 0));
                crown.CloseFigure();
                RectangleF r = new RectangleF(-0.34f * u, -0.52f * u, 0.68f * u, 0.52f * u);
                Fill(g, crown, r, a, b, 90f);
                Shine(g, crown, r, 60);
            }
            RectangleF band = new RectangleF(-0.31f * u, -0.12f * u, 0.62f * u, 0.08f * u);
            using (SolidBrush br = new SolidBrush(Color.FromArgb(60, 30, 10))) g.FillRectangle(br, band);
            using (GraphicsPath s = Star(0, -0.08f * u, 0.05f * u, 0.45f)) FillSolid(g, s, Gold1);
        }

        static void Headphones(Graphics g, float u, Geo geo, float D)
        {
            float cupY = (geo.FaceY - geo.Top) * D - 0.02f * D;
            float half = geo.HeadW * D / 2 + 0.01f * D;
            using (Pen band = new Pen(Color.FromArgb(40, 40, 52), u * 0.07f))
            {
                band.StartCap = LineCap.Round; band.EndCap = LineCap.Round;
                g.DrawArc(band, -half, -0.18f * u, half * 2, cupY * 2 + 0.36f * u, 180, 180);
            }
            foreach (int s in new int[] { -1, 1 })
            {
                RectangleF cup = new RectangleF(s * half - 0.08f * u, cupY - 0.14f * u, 0.16f * u, 0.28f * u);
                using (GraphicsPath c = Theme.Round(cup, 0.07f * u))
                {
                    Fill(g, c, cup, Color.FromArgb(70, 70, 86), Color.FromArgb(24, 24, 32), 0f);
                    Stroke(g, c, Color.FromArgb(56, 189, 248), u * 0.02f);
                }
            }
        }

        static void Santa(Graphics g, float u)
        {
            using (GraphicsPath c = new GraphicsPath())
            {
                c.AddLine(P(-0.44f * u, 0), P(0.44f * u, 0));
                c.AddBezier(P(0.44f * u, 0), P(0.4f * u, -0.4f * u), P(0.2f * u, -0.6f * u), P(0.56f * u, -0.42f * u));
                c.AddBezier(P(0.56f * u, -0.42f * u), P(0.1f * u, -0.72f * u), P(-0.38f * u, -0.4f * u), P(-0.44f * u, 0));
                c.CloseFigure();
                RectangleF r = new RectangleF(-0.44f * u, -0.7f * u, 1f * u, 0.7f * u);
                Fill(g, c, r, Color.FromArgb(239, 68, 68), Color.FromArgb(153, 27, 27), 70f);
                Shine(g, c, r, 60);
            }
            RectangleF trim = new RectangleF(-0.5f * u, -0.1f * u, 1f * u, 0.18f * u);
            using (GraphicsPath t = Theme.Round(trim, 0.09f * u)) Fill(g, t, trim, Color.White, Color.FromArgb(215, 222, 235), 90f);
            Ellipse(g, Color.White, 0.5f * u, -0.5f * u, 0.15f * u, 0.15f * u);
        }

        static void Sprout(Graphics g, float u, double now)
        {
            float wig = (float)Math.Sin(now / 400.0) * 8;
            GraphicsState st = g.Save();
            g.RotateTransform(wig * 0.4f);
            using (GraphicsPath stem = new GraphicsPath())
            {
                stem.AddBezier(P(0, 0.04f * u), P(0.02f * u, -0.1f * u), P(-0.02f * u, -0.2f * u), P(0, -0.3f * u));
                Stroke(g, stem, Color.FromArgb(22, 163, 74), u * 0.045f);
            }
            foreach (int s in new int[] { -1, 1 })
            {
                GraphicsState ls = g.Save();
                g.TranslateTransform(0, -0.3f * u);
                g.RotateTransform(s * (35 + wig));
                RectangleF leaf = new RectangleF(s < 0 ? -0.24f * u : 0, -0.06f * u, 0.24f * u, 0.12f * u);
                using (GraphicsPath lp = new GraphicsPath())
                {
                    lp.AddEllipse(leaf);
                    Fill(g, lp, leaf, Color.FromArgb(134, 239, 172), Color.FromArgb(22, 163, 74), 90f);
                }
                g.Restore(ls);
            }
            g.Restore(st);
        }

        static void Crown(Graphics g, float u)
        {
            using (GraphicsPath c = Poly(P(-0.34f * u, 0.02f * u), P(-0.38f * u, -0.34f * u), P(-0.17f * u, -0.16f * u), P(0, -0.42f * u),
                                         P(0.17f * u, -0.16f * u), P(0.38f * u, -0.34f * u), P(0.34f * u, 0.02f * u)))
            {
                RectangleF r = new RectangleF(-0.38f * u, -0.42f * u, 0.76f * u, 0.44f * u);
                Fill(g, c, r, Gold1, Gold2, 90f);
                Shine(g, c, r, 90);
                Stroke(g, c, Darker(Gold2, 0.25), u * 0.015f);
            }
            float br = 0.045f * u;
            foreach (PointF tip in new PointF[] { P(-0.38f * u, -0.34f * u), P(0, -0.42f * u), P(0.38f * u, -0.34f * u) })
                Ellipse(g, Gold1, tip.X - br, tip.Y - br, br * 2, br * 2);
            Ellipse(g, Color.FromArgb(239, 68, 68), -0.05f * u, -0.14f * u, 0.1f * u, 0.1f * u);
            Ellipse(g, Color.FromArgb(59, 130, 246), -0.24f * u, -0.1f * u, 0.07f * u, 0.07f * u);
            Ellipse(g, Color.FromArgb(34, 197, 94), 0.17f * u, -0.1f * u, 0.07f * u, 0.07f * u);
        }

        static void Halo(Graphics g, float u, double now)
        {
            float bob = (float)Math.Sin(now / 700.0) * 0.03f * u;
            RectangleF ring = new RectangleF(-0.34f * u, -0.36f * u + bob, 0.68f * u, 0.16f * u);
            Glow(g, 0, ring.Y + ring.Height / 2, ring.Width * 1.6f, ring.Height * 3.2f, Color.FromArgb(253, 230, 138), 120);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(ring);
                Stroke(g, p, Gold1, u * 0.06f);
            }
        }

        static void Beret(Graphics g, float u)
        {
            g.RotateTransform(-10);
            RectangleF r = new RectangleF(-0.52f * u, -0.3f * u, 1.04f * u, 0.36f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(r);
                Fill(g, p, r, Color.FromArgb(239, 68, 68), Color.FromArgb(153, 27, 27), 90f);
                Shine(g, p, r, 80);
            }
            RectangleF band = new RectangleF(-0.36f * u, -0.02f * u, 0.72f * u, 0.09f * u);
            using (GraphicsPath b = Theme.Round(band, 0.045f * u)) FillSolid(g, b, Color.FromArgb(127, 29, 29));
            Line(g, Color.FromArgb(127, 29, 29), u * 0.035f, 0.02f * u, -0.29f * u, 0.05f * u, -0.38f * u);
        }

        static void Chef(Graphics g, float u)
        {
            Color white = Color.FromArgb(252, 252, 255), shade = Color.FromArgb(214, 220, 232);
            RectangleF band = new RectangleF(-0.34f * u, -0.2f * u, 0.68f * u, 0.22f * u);
            // Puffy top: overlapping circles, then the band in front.
            float[][] puffs = { new float[] { -0.27f, -0.42f, 0.2f }, new float[] { 0.27f, -0.42f, 0.2f }, new float[] { 0, -0.55f, 0.25f } };
            foreach (float[] c in puffs)
            {
                RectangleF r = new RectangleF((c[0] - c[2]) * u, (c[1] - c[2]) * u, c[2] * 2 * u, c[2] * 2 * u);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(r);
                    Fill(g, p, r, white, shade, 90f);
                }
            }
            using (GraphicsPath b = Theme.Round(band, 0.05f * u))
            {
                Fill(g, b, band, white, shade, 90f);
                Stroke(g, b, Color.FromArgb(40, 0, 0, 0), u * 0.012f);
            }
            for (int i = -1; i <= 1; i++) Line(g, Color.FromArgb(30, 0, 0, 0), u * 0.012f, i * 0.14f * u, band.Y + 0.03f * u, i * 0.14f * u, band.Bottom - 0.03f * u);
        }

        static void Flowers(Graphics g, float u, double now)
        {
            Color[] petals = { Color.FromArgb(244, 114, 182), Color.FromArgb(253, 224, 71), Color.FromArgb(167, 139, 250), Color.FromArgb(251, 146, 60), Color.FromArgb(96, 165, 250) };
            using (GraphicsPath vine = new GraphicsPath())
            {
                vine.AddArc(new RectangleF(-0.46f * u, -0.16f * u, 0.92f * u, 0.3f * u), 190, 160);
                using (Pen p = new Pen(Color.FromArgb(34, 160, 80), u * 0.04f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, vine); }
            }
            for (int i = 0; i < 5; i++)
            {
                double a = Math.PI * (1.12 + i * 0.19);
                float x = (float)Math.Cos(a) * 0.44f * u, y = (float)Math.Sin(a) * 0.14f * u - 0.01f * u;
                float r = (i == 2 ? 0.1f : 0.08f) * u;
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)(i * 31 + Math.Sin(now / 900.0 + i) * 6));
                for (int k = 0; k < 5; k++)
                {
                    g.RotateTransform(72);
                    Ellipse(g, petals[i], -r * 0.38f, -r * 1.05f, r * 0.76f, r * 0.9f);
                }
                g.Restore(st);
                Ellipse(g, Color.FromArgb(255, 251, 235), x - r * 0.32f, y - r * 0.32f, r * 0.64f, r * 0.64f);
            }
        }
    }
}
