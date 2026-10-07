// Stackshot - Anime-inspired costume pieces: hair, hoods, headbands, capes and vests, plus the dog and chibi bodies.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Generic archetypes (adventurer, ninja, alchemist, scout...) in the mascot's own style: no names, logos or emblems.
    public static partial class MascotParts
    {
        public const int KindDog = 6, KindChibi = 7;
        public const int HairFirst = 18, HatHood = 23, HatNinja = 24, HatStraw = 25;
        public const int OutfitScoutCape = 8, OutfitRedCoat = 9;

        // Fan tributes: the characters' own names and where each comes from (shown on hover). Not affiliated with or
        // endorsed by their owners; see the README. Ready-made costumes below: kind, color, eyes, hat, outfit, face.
        public static readonly string[] AnimeNames = { "Finn", "Jake", "Pakkun", "Edward Elric", "Eren Jaeger", "Luffy", "Goku", "Tanjiro", "Gojo",
                                                       "Shin-chan", "BMO", "Pikachu", "Doraemon", "Naruto", "Totoro", "Kratos", "Doom Slayer", "Claude",
                                                       "Codex", "Gon", "Killua", "Hisoka", "Sasuke", "Kakashi", "Zoro", "Vegeta", "Saitama", "Levi",
                                                       "Nezuko", "Spider-Man", "Batman", "Flash", "Superman", "Iron Man",
                                                       "Caballero", "Astronauta", "Mago sabio", "Superh\u00E9roe" };
        public static readonly string[] AnimeInspiration = { "Hora de aventuras", "Hora de aventuras", "Naruto", "Fullmetal Alchemist", "Ataque a los titanes",
                                                             "One Piece", "Dragon Ball", "Kimetsu no Yaiba", "Jujutsu Kaisen", "Crayon Shin-chan",
                                                             "Hora de aventuras", "Pok\u00E9mon", "Doraemon", "Naruto", "Mi vecino Totoro", "God of War", "Doom",
                                                             "Anthropic", "OpenAI", "Hunter x Hunter", "Hunter x Hunter", "Hunter x Hunter", "Naruto", "Naruto",
                                                             "One Piece", "Dragon Ball", "One Punch Man", "Ataque a los titanes", "Kimetsu no Yaiba",
                                                             "Marvel", "DC", "DC", "DC", "Marvel", null, null, null, null };
        static readonly int[,] Anime =
        {
            { 7, 17, 2, 23, 13, 0 },
            { 6, 5, 2, 0, 0, 0 },
            { 6, 18, 0, 24, 10, 0 },
            { 7, 17, 3, 18, 9, 0 },
            { 7, 17, 4, 19, 8, 0 },
            { 7, 17, 1, 25, 14, 0 },
            { 7, 17, 3, 20, 11, 0 },
            { 7, 17, 1, 22, 12, 0 },
            { 7, 17, 0, 21, 0, 8 },
            { 7, 17, 2, 26, 15, 9 },
            { 0, 19, 2, 0, 16, 0 },
            { 2, 5, 2, 0, 0, 10 },
            { 1, 4, 2, 0, 6, 0 },
            { 7, 17, 1, 27, 18, 11 },
            { 2, 7, 2, 12, 17, 0 },
            { 7, 20, 4, 0, 19, 12 },
            { 7, 15, 0, 28, 20, 0 },
            { 4, 21, 1, 31, 0, 0 },
            { 0, 22, 0, 0, 0, 14 },
            { 7, 17, 2, 32, 22, 0 },
            { 7, 17, 4, 21, 23, 0 },
            { 7, 17, 4, 33, 24, 15 },
            { 7, 17, 4, 34, 25, 0 },
            { 7, 17, 4, 38, 30, 17 },
            { 7, 17, 4, 35, 26, 0 },
            { 7, 17, 4, 36, 27, 0 },
            { 7, 17, 2, 0, 28, 0 },
            { 7, 17, 4, 26, 8, 0 },
            { 7, 17, 1, 37, 29, 16 },
            { 7, 13, 0, 0, 31, 18 },
            { 7, 17, 4, 39, 32, 0 },
            { 7, 17, 0, 40, 33, 0 },
            { 7, 17, 0, 41, 34, 0 },
            { 7, 17, 0, 42, 35, 0 },
            { 7, 17, 0, 29, 0, 0 },
            { 7, 17, 1, 30, 21, 0 },
            { 7, 17, 0, 3, 0, 13 },
            { 7, 17, 4, 0, 5, 5 }
        };

        public static void ApplyAnime(MascotLook l, int i)
        {
            l.Kind = Anime[i, 0]; l.Color = Anime[i, 1]; l.Eyes = Anime[i, 2];
            l.Hat = Anime[i, 3]; l.Outfit = Anime[i, 4]; l.Face = Anime[i, 5];
        }

        // ---- Dog: wagging tail behind, floppy ears and nose in front, muzzle on the body.
        static void DogTail(Graphics g, float D, Color c2, double now)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0.27f * D, 0.2f * D);
            g.RotateTransform((float)(-35 + Math.Sin(now / 95.0) * 22));
            using (GraphicsPath t = new GraphicsPath())
            {
                t.AddBezier(P(0, 0), P(0.06f * D, -0.04f * D), P(0.12f * D, -0.1f * D), P(0.13f * D, -0.17f * D));
                using (Pen p = new Pen(c2, D * 0.065f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, t); }
            }
            g.Restore(st);
        }

        static void DogEars(Graphics g, float D, Color c2, MascotPose p)
        {
            foreach (int s in new int[] { -1, 1 })
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.26f * D, -0.25f * D);
                g.RotateTransform((float)(s * (14 + (s < 0 ? p.EarL : p.EarR) * 0.5)));
                using (GraphicsPath e = new GraphicsPath())
                {
                    e.AddBezier(P(-0.06f * D, 0), P(-0.1f * D, 0.12f * D), P(-0.06f * D, 0.26f * D), P(0.01f * D, 0.27f * D));
                    e.AddBezier(P(0.01f * D, 0.27f * D), P(0.08f * D, 0.27f * D), P(0.08f * D, 0.1f * D), P(0.05f * D, 0));
                    e.CloseFigure();
                    RectangleF r = new RectangleF(-0.1f * D, 0, 0.18f * D, 0.27f * D);
                    Fill(g, e, r, Darker(c2, 0.15), Darker(c2, 0.4), 90f);
                    Stroke(g, e, Color.FromArgb(40, 0, 0, 0), D * 0.008f);
                }
                g.Restore(st);
            }
        }

        static void DogMuzzle(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            Ellipse(g, Color.FromArgb(150, 255, 248, 235), -0.12f * D, fy + 0.035f * D, 0.24f * D, 0.15f * D);
        }

        static void DogNose(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            using (GraphicsPath n = new GraphicsPath())
            {
                RectangleF r = new RectangleF(-0.04f * D, fy + 0.045f * D, 0.08f * D, 0.05f * D);
                n.AddEllipse(r);
                Fill(g, n, r, Color.FromArgb(70, 60, 80), Color.FromArgb(20, 18, 28), 90f);
            }
            Ellipse(g, Color.FromArgb(150, 255, 255, 255), -0.022f * D, fy + 0.052f * D, 0.022f * D, 0.012f * D);
        }

        // ---- Hair: one shape with a style per character. Origin at the top of the head, u = hat unit.
        // Hair styles: colors, how far the spikes stick out, how many, and how low the bangs come (fixed tables, not per frame).
        static readonly Color[,] HairTones =
        {
            { Color.FromArgb(255, 222, 110), Color.FromArgb(214, 150, 30) },   // blond
            { Color.FromArgb(120, 80, 52), Color.FromArgb(62, 38, 24) },       // brown
            { Color.FromArgb(58, 56, 72), Color.FromArgb(16, 16, 24) },        // black, spiky
            { Color.FromArgb(250, 250, 255), Color.FromArgb(190, 200, 225) },  // white
            { Color.FromArgb(150, 50, 60), Color.FromArgb(70, 18, 28) },       // dark red
            { Color.FromArgb(52, 50, 60), Color.FromArgb(14, 14, 20) },         // black, smooth
            { Color.FromArgb(255, 222, 90), Color.FromArgb(225, 150, 20) },      // blond, spiky
            { Color.FromArgb(46, 74, 54), Color.FromArgb(10, 22, 14) },       // dark green-black, tall spikes
            { Color.FromArgb(240, 96, 128), Color.FromArgb(150, 30, 72) },    // magenta, slicked up
            { Color.FromArgb(62, 66, 100), Color.FromArgb(16, 18, 36) },      // dark navy, spiky back
            { Color.FromArgb(116, 206, 124), Color.FromArgb(38, 120, 60) },   // green, short
            { Color.FromArgb(58, 56, 72), Color.FromArgb(16, 16, 24) },       // black, tall flame
            { Color.FromArgb(58, 42, 56), Color.FromArgb(16, 10, 18) },       // black, long
            { Color.FromArgb(222, 226, 236), Color.FromArgb(150, 156, 172) }  // silver, slanted spikes
        };
        static readonly float[] HairSpike = { 0.12f, 0.04f, 0.5f, 0.3f, 0.06f, 0.02f, 0.42f, 0.8f, 0.45f, 0.38f, 0.08f, 1.15f, 0.03f, 0.55f };
        static readonly int[] HairSpikes = { 7, 9, 7, 8, 9, 10, 8, 6, 6, 7, 10, 5, 10, 6 };
        static readonly float[] HairBangs = { 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.3f, 0.18f, 0.34f, 0.24f, 0.16f, 0.32f, 0.3f };

        static void Hair(Graphics g, float u, int style, double now)
        {
            Color[,] tones = HairTones;
            float[] spike = HairSpike;
            int[] spikes = HairSpikes;
            float cx = 0, cy = 0.4f * u, rx = 0.64f * u, ry = 0.46f * u;
            int n = spikes[style] * 2;
            PointF[] top = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI + Math.PI * i / n;
                float k = 1 + (i % 2 == 1 ? spike[style] * (float)Math.Pow(Math.Sin(Math.PI * i / n), 1.5) : 0);
                top[i] = new PointF(cx + (float)Math.Cos(a) * rx * (i % 2 == 1 ? 1 + spike[style] * 0.3f : 1), cy + (float)Math.Sin(a) * ry * k);
            }
            using (GraphicsPath h = new GraphicsPath())
            {
                float side = style == 12 ? 1.05f * u : 0.5f * u; // long hair falls past the cheeks
                h.AddLine(P(-0.66f * u, side), top[0]);
                h.AddLines(top);
                h.AddLine(top[n], P(0.66f * u, side));
                // Bangs: points hanging over the forehead.
                int b = style == 2 ? 3 : 5;
                for (int i = 0; i <= b * 2; i++)
                {
                    float x = 0.62f * u - 1.24f * u * i / (b * 2);
                    float y = i % 2 == 1 ? HairBangs[style] * u : (i == 0 || i == b * 2 ? side : Math.Min(0.16f, HairBangs[style] * 0.5f) * u);
                    if (style == 0 && i == b) y = 0.12f * u; // center part
                    h.AddLine(P(x, y), P(x, y));
                }
                h.CloseFigure();
                RectangleF r = new RectangleF(-0.68f * u, -0.3f * u, 1.36f * u, 0.82f * u);
                Fill(g, h, r, tones[style, 0], tones[style, 1], 70f);
                Shine(g, h, r, style == 3 ? 60 : 90);
                Stroke(g, h, Color.FromArgb(50, 0, 0, 0), u * 0.014f);
            }
            if (style == 0)
            {
                // A curly strand on top that bounces.
                float sway = (float)Math.Sin(now / 400.0) * 0.03f * u;
                using (GraphicsPath a = new GraphicsPath())
                {
                    a.AddBezier(P(0, -0.04f * u), P(0.02f * u, -0.2f * u), P(0.16f * u + sway, -0.24f * u), P(0.12f * u + sway, -0.14f * u));
                    using (Pen p = new Pen(tones[0, 1], u * 0.05f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, a); }
                }
            }
        }

        static void Hood(Graphics g, float u)
        {
            Color a = Color.FromArgb(255, 255, 255), b = Color.FromArgb(206, 214, 230);
            foreach (int s in new int[] { -1, 1 }) Ellipse(g, b, s * 0.32f * u - 0.12f * u, -0.3f * u, 0.24f * u, 0.24f * u);
            using (GraphicsPath h = new GraphicsPath(FillMode.Alternate))
            {
                h.AddArc(-0.68f * u, -0.2f * u, 1.36f * u, 1.1f * u, 180, 180);
                h.AddLine(P(0.68f * u, 0.35f * u), P(0.6f * u, 0.95f * u));
                h.AddLine(P(0.6f * u, 0.95f * u), P(-0.6f * u, 0.95f * u));
                h.CloseFigure();
                h.AddEllipse(-0.5f * u, 0.08f * u, 1.0f * u, 0.95f * u); // the face opening
                RectangleF r = new RectangleF(-0.68f * u, -0.2f * u, 1.36f * u, 1.15f * u);
                Fill(g, h, r, a, b, 90f);
                Stroke(g, h, Color.FromArgb(45, 0, 0, 0), u * 0.014f);
            }
        }

        static void NinjaBand(Graphics g, float u, double now)
        {
            float y = 0.2f * u;
            float flow = (float)Math.Sin(now / 300.0) * 3;
            GraphicsState st = g.Save();
            g.TranslateTransform(0.6f * u, y + 0.04f * u);
            foreach (float rot in new float[] { 20 + flow, 40 + flow })
            {
                GraphicsState s2 = g.Save();
                g.RotateTransform(rot);
                RectangleF tail = new RectangleF(0, -0.04f * u, 0.32f * u, 0.08f * u);
                using (GraphicsPath tp = Theme.Round(tail, 0.03f * u)) FillSolid(g, tp, Color.FromArgb(30, 44, 90));
                g.Restore(s2);
            }
            g.Restore(st);
            RectangleF band = new RectangleF(-0.66f * u, y - 0.07f * u, 1.32f * u, 0.15f * u);
            using (GraphicsPath bp = Theme.Round(band, 0.06f * u)) Fill(g, bp, band, Color.FromArgb(52, 72, 140), Color.FromArgb(28, 40, 86), 90f);
            RectangleF plate = new RectangleF(-0.26f * u, y - 0.09f * u, 0.52f * u, 0.19f * u);
            using (GraphicsPath pp = Theme.Round(plate, 0.04f * u))
            {
                Fill(g, pp, plate, Color.FromArgb(232, 236, 244), Color.FromArgb(150, 158, 175), 90f);
                Shine(g, pp, plate, 120);
                Stroke(g, pp, Color.FromArgb(90, 60, 66, 80), u * 0.012f);
            }
            foreach (int s in new int[] { -1, 1 })
                foreach (int t in new int[] { -1, 1 })
                    Ellipse(g, Color.FromArgb(120, 70, 76, 92), s * 0.21f * u - 0.012f * u, y + t * 0.05f * u - 0.012f * u, 0.024f * u, 0.024f * u);
            for (int i = -1; i <= 1; i++) Line(g, Color.FromArgb(110, 70, 76, 92), u * 0.014f, -0.1f * u, y + i * 0.03f * u, 0.1f * u, y + i * 0.03f * u);
        }

        static void StrawHat(Graphics g, float u)
        {
            Color s1 = Color.FromArgb(250, 220, 140), s2 = Color.FromArgb(205, 160, 70);
            RectangleF brim = new RectangleF(-0.8f * u, -0.08f * u, 1.6f * u, 0.3f * u);
            using (GraphicsPath b = new GraphicsPath())
            {
                b.AddEllipse(brim);
                Fill(g, b, brim, s1, s2, 90f);
                Stroke(g, b, Color.FromArgb(70, 120, 80, 20), u * 0.012f);
            }
            RectangleF dome = new RectangleF(-0.44f * u, -0.44f * u, 0.88f * u, 0.7f * u);
            using (GraphicsPath d = new GraphicsPath())
            {
                d.AddArc(dome, 180, 180);
                d.CloseFigure();
                RectangleF r = new RectangleF(dome.X, dome.Y, dome.Width, dome.Height / 2);
                Fill(g, d, r, s1, s2, 90f);
                Region old = g.Clip;
                g.SetClip(d, CombineMode.Intersect);
                for (float yy = dome.Y + 0.06f * u; yy < 0; yy += 0.06f * u) Line(g, Color.FromArgb(40, 120, 80, 20), u * 0.01f, dome.X, yy, dome.Right, yy);
                g.Clip = old;
                old.Dispose();
                Shine(g, d, r, 80);
            }
            RectangleF band = new RectangleF(-0.44f * u, -0.1f * u, 0.88f * u, 0.1f * u);
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddRectangle(band);
                Fill(g, bp, band, Color.FromArgb(235, 50, 50), Color.FromArgb(160, 20, 30), 90f);
            }
        }

        // ---- Outfits.
        static void Straps(Graphics g, float D, float y, float w, Color c)
        {
            foreach (int s in new int[] { -1, 1 })
                Line(g, c, D * 0.035f, s * w * 0.32f, y - 0.03f * D, s * w * 0.18f, y + 0.2f * D);
            Line(g, c, D * 0.035f, -w * 0.3f, y + 0.12f * D, w * 0.3f, y + 0.12f * D);
        }

        static void Panels(Graphics g, float D, float y, float w, Geo geo, Color a, Color b, bool checker)
        {
            foreach (int s in new int[] { -1, 1 })
            {
                using (GraphicsPath p = Poly(P(s * 0.05f * D, y - 0.03f * D), P(s * w * 0.56f, y - 0.06f * D), P(s * w * 0.6f, geo.Bottom * D), P(s * 0.13f * D, geo.Bottom * D)))
                {
                    RectangleF r = new RectangleF(-w * 0.6f, y - 0.06f * D, w * 1.2f, geo.Bottom * D - y + 0.06f * D);
                    if (!checker) Fill(g, p, r, a, b, 90f);
                    else
                    {
                        FillSolid(g, p, b);
                        Region old = g.Clip;
                        g.SetClip(p, CombineMode.Intersect);
                        float q = Math.Max(2f, 0.05f * D);
                        using (SolidBrush br = new SolidBrush(a))
                            for (float yy = r.Y; yy < r.Bottom; yy += q)
                                for (float xx = r.X + ((int)((yy - r.Y) / q) % 2) * q; xx < r.Right; xx += q * 2) g.FillRectangle(br, xx, yy, q, q);
                        g.Clip = old;
                        old.Dispose();
                    }
                    Stroke(g, p, Color.FromArgb(60, 0, 0, 0), D * 0.01f);
                }
            }
        }

        static void GiCollar(Graphics g, float D, float y, float w)
        {
            Color blue = Color.FromArgb(37, 70, 200);
            using (GraphicsPath v = Poly(P(-0.14f * D, y - 0.04f * D), P(0, y + 0.1f * D), P(0.14f * D, y - 0.04f * D), P(0.08f * D, y - 0.04f * D), P(0, y + 0.04f * D), P(-0.08f * D, y - 0.04f * D)))
                FillSolid(g, v, blue);
            RectangleF belt = new RectangleF(-w * 0.5f, y + 0.13f * D, w, 0.06f * D);
            using (GraphicsPath bp = Theme.Round(belt, 0.03f * D)) Fill(g, bp, belt, Color.FromArgb(70, 110, 240), blue, 90f);
            Ellipse(g, Color.FromArgb(30, 50, 160), -0.035f * D, y + 0.125f * D, 0.07f * D, 0.07f * D);
        }

        static void CoatCollar(Graphics g, float D, float y, float w)
        {
            // Black shirt under the open coat.
            RectangleF r = new RectangleF(-0.16f * D, y - 0.06f * D, 0.32f * D, 0.4f * D);
            using (GraphicsPath p = new GraphicsPath()) { p.AddRectangle(r); Fill(g, p, r, Color.FromArgb(44, 42, 52), Color.FromArgb(16, 14, 20), 90f); }
        }

        static void Blindfold(Graphics g, float D, Geo geo)
        {
            float fy = geo.FaceY * D;
            RectangleF band = new RectangleF(-geo.HeadW * D * 0.52f, fy - 0.06f * D, geo.HeadW * D * 1.04f, 0.12f * D);
            using (GraphicsPath p = Theme.Round(band, 0.03f * D)) Fill(g, p, band, Color.FromArgb(46, 46, 56), Color.FromArgb(14, 14, 20), 90f);
        }

        // Called from the main switches.
        static bool PaintAnimeHat(Graphics g, int hat, float u, double now)
        {
            if (hat >= HairFirst && hat < HairFirst + 5) { Hair(g, u, hat - HairFirst, now); return true; }
            if (hat == HatHood) { Hood(g, u); return true; }
            if (hat == HatNinja) { NinjaBand(g, u, now); return true; }
            if (hat == HatStraw) { StrawHat(g, u); return true; }
            if (PaintMoreHat(g, hat, u, now)) return true;
            return false;
        }

        static bool PaintAnimeOutfit(Graphics g, MascotLook l, float D, float y, float w)
        {
            if (l.Outfit < 8) return false;
            Geo geo = GeoFor(l.Kind);
            // Clothes stay inside the body silhouette.
            GraphicsPath body = l.Kind == 0 ? Theme.Round(new RectangleF(-geo.HeadW * D / 2, geo.Top * D, geo.HeadW * D, (geo.Bottom - geo.Top) * D), (geo.Bottom - geo.Top) * D * 0.42f)
                                            : BodyPath(l.Kind, geo, D, 0);
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            try { return Outfit(g, l, D, y, w, geo); }
            finally { g.Clip = old; old.Dispose(); body.Dispose(); }
        }

        static bool Outfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 8: Straps(g, D, y, w, Color.FromArgb(120, 74, 40)); return true;
                case 9: CoatCollar(g, D, y, w); Panels(g, D, y, w, geo, Color.FromArgb(225, 45, 45), Color.FromArgb(140, 16, 24), false); return true;
                case 10: Panels(g, D, y, w, geo, Color.FromArgb(70, 100, 190), Color.FromArgb(32, 50, 120), false); return true;
                case 11: GiCollar(g, D, y, w); return true;
                case 12: Panels(g, D, y, w, geo, Color.FromArgb(20, 20, 24), Color.FromArgb(22, 140, 100), true); return true;
                case 13: Straps(g, D, y, w, Color.FromArgb(34, 160, 80)); return true;
                case 14: Panels(g, D, y, w, geo, Color.FromArgb(240, 60, 60), Color.FromArgb(170, 20, 30), false); return true;
                default: return MoreOutfit(g, l, D, y, w, geo);
            }
        }
    }
}
