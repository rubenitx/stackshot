// Stackshot - Hunter x Hunter's Gon, Killua and Hisoka as anime chibis, like the ninjas (MascotToons), as the 2011
// anime draws them: Gon's black hair spiking up with its dark green tips, his green jacket with the orange line and
// collar, shorts, belt and boots; Killua's messy white hair falling over his brow, his white top over dark blue sleeves,
// white shorts; Hisoka's magenta hair slicked back and up like flames, the star and the teardrop on his cheeks, his pale
// top with a heart and a spade, the pink ring round his neck. Fan tributes, not affiliated with their owners (see the
// README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindGonSd = 98, KindKilluaSd = 99, KindHisokaSd = 100;

        static void HunterHairColors(int kind, out Color hair, out Color shade, out Color light)
        {
            switch (kind)
            {
                case KindGonSd: hair = Color.FromArgb(52, 58, 56); shade = Color.FromArgb(24, 28, 28); light = Color.FromArgb(128, 148, 136); break;
                case KindKilluaSd: hair = Color.FromArgb(246, 246, 252); shade = Color.FromArgb(186, 188, 218); light = Color.FromArgb(255, 255, 255); break;
                default: hair = Color.FromArgb(232, 66, 116); shade = Color.FromArgb(132, 44, 120); light = Color.FromArgb(255, 170, 196); break;
            }
        }

        // Behind the head, from one side over the top to the other, notches and spike tips in turn. Gon's: long spikes
        // fanning up and out, the tallest in the middle.
        static readonly float[] GonHair =
        {
            -0.255f, -0.12f, -0.42f, -0.34f, -0.28f, -0.33f, -0.41f, -0.6f, -0.21f, -0.45f, -0.27f, -0.79f, -0.08f, -0.5f, -0.06f, -0.87f,
            0.06f, -0.52f, 0.17f, -0.83f, 0.18f, -0.48f, 0.38f, -0.7f, 0.28f, -0.38f, 0.48f, -0.46f, 0.3f, -0.24f, 0.43f, -0.2f, 0.26f, -0.1f
        };
        // Killua's: a messy mop of short spikes all round.
        static readonly float[] KilluaHair =
        {
            -0.255f, 0, -0.38f, -0.05f, -0.3f, -0.16f, -0.44f, -0.25f, -0.31f, -0.3f, -0.42f, -0.46f, -0.24f, -0.42f, -0.28f, -0.6f,
            -0.12f, -0.48f, -0.06f, -0.64f, 0.04f, -0.5f, 0.16f, -0.63f, 0.18f, -0.46f, 0.34f, -0.56f, 0.29f, -0.38f, 0.45f, -0.4f,
            0.31f, -0.25f, 0.44f, -0.17f, 0.3f, -0.1f, 0.39f, 0, 0.255f, 0
        };
        // Hisoka's: slicked back off his forehead and up in tall points, like flames.
        static readonly float[] HisokaHair =
        {
            -0.255f, -0.12f, -0.44f, -0.3f, -0.3f, -0.34f, -0.47f, -0.56f, -0.24f, -0.46f, -0.31f, -0.75f, -0.1f, -0.52f, -0.04f, -0.81f,
            0.06f, -0.52f, 0.23f, -0.77f, 0.2f, -0.47f, 0.45f, -0.6f, 0.3f, -0.36f, 0.49f, -0.34f, 0.3f, -0.22f, 0.41f, -0.12f, 0.25f, -0.1f
        };
        // Killua's bangs over his brow, their points down to his eyes (the top, over his hair, is not outlined).
        static readonly float[] KilluaBangs =
        {
            -0.3f, -0.28f, -0.2f, -0.4f, 0, -0.44f, 0.2f, -0.4f, 0.31f, -0.27f, 0.29f, -0.05f, 0.22f, -0.19f, 0.15f, -0.1f, 0.09f, -0.2f,
            0.02f, -0.11f, -0.05f, -0.21f, -0.11f, -0.11f, -0.17f, -0.2f, -0.25f, -0.07f, -0.27f, -0.2f
        };

        // In front of the top of the head, so the hair starts at the hairline and not behind a bare head. Gon's: spikes
        // rising from his forehead, shorter than the ones behind, and a pointed sideburn in front of each ear (from the
        // hairline on the left, up and over, down to the right, and back along the hairline; tips at the odd points).
        static readonly float[] GonCap =
        {
            -0.2f, -0.17f, -0.285f, -0.05f, -0.25f, -0.2f, -0.37f, -0.36f, -0.22f, -0.37f, -0.25f, -0.56f, -0.09f, -0.42f, -0.05f, -0.62f,
            0.06f, -0.43f, 0.17f, -0.58f, 0.19f, -0.38f, 0.37f, -0.4f, 0.25f, -0.22f, 0.285f, -0.05f, 0.2f, -0.17f, 0.1f, -0.235f, 0, -0.255f, -0.1f, -0.235f
        };
        // Hisoka's: slicked back from a hairline coming to a point in the middle of his forehead.
        static readonly float[] HisokaCap =
        {
            -0.27f, -0.13f, -0.31f, -0.3f, -0.2f, -0.44f, 0, -0.48f, 0.2f, -0.44f, 0.31f, -0.3f, 0.27f, -0.13f, 0.22f, -0.21f, 0.12f, -0.26f,
            0.045f, -0.27f, 0, -0.225f, -0.045f, -0.27f, -0.12f, -0.26f, -0.22f, -0.21f
        };

        // The face and the hair over the top of it (kept with the head).
        static void HunterHead(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, NjSkin, NjSkinShade, D, 0);
            Color hair, shade, light;
            HunterHairColors(k, out hair, out shade, out light);
            if (k == KindGonSd)
                using (GraphicsPath c = TnSpikes(D, 0.06f, GonCap))
                {
                    Stroke(g, c, TnInk(k), TnW(D) * 2);
                    VPaint(g, c, hair, shade, D, 20);
                    Region old = g.Clip;
                    g.SetClip(c, CombineMode.Intersect);
                    using (SolidBrush tip = new SolidBrush(Color.FromArgb(44, 110, 66)))
                    using (Pen streak = new Pen(Color.FromArgb(170, light), Math.Max(0.8f, D * 0.008f)))
                    {
                        streak.StartCap = streak.EndCap = LineCap.Round;
                        for (int i = 3; i <= 11; i += 2)
                        {
                            PointF a = P(GonCap[i * 2 - 2] * D, GonCap[i * 2 - 1] * D), t = P(GonCap[i * 2] * D, GonCap[i * 2 + 1] * D), b = P(GonCap[i * 2 + 2] * D, GonCap[i * 2 + 3] * D);
                            g.FillPolygon(tip, new PointF[] { t, Lerp(t, a, 0.3f), Lerp(t, b, 0.3f) });
                            PointF mid = Lerp(a, b, 0.5f);
                            g.DrawLine(streak, Lerp(mid, t, 0.25f), Lerp(mid, t, 0.62f));
                        }
                    }
                    g.Clip = old;
                    old.Dispose();
                }
            else if (k == KindHisokaSd)
                using (GraphicsPath c = TnSpikes(D, 0.04f, HisokaCap))
                {
                    SlayerHair(g, k, c, D, hair, shade);
                    // Combed back: strands from his hairline up over his head.
                    Region old = g.Clip;
                    g.SetClip(c, CombineMode.Intersect);
                    foreach (float x in new float[] { -0.2f, -0.1f, 0.02f, 0.12f, 0.21f })
                        TnLine(g, Color.FromArgb(150, shade), 0.007f, D, x, -0.22f, x * 1.12f, -0.34f, x * 1.25f, -0.48f);
                    TnLine(g, Color.FromArgb(170, light), 0.008f, D, -0.06f, -0.3f, -0.09f, -0.44f);
                    g.Clip = old;
                    old.Dispose();
                }
        }

        // Behind: the hair with the ears (kept), the legs stepping as they walk, the arms, and over them the clothes (kept).
        static void HunterBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.6f, -0.92f, 1.2f, 1.0f), bg => HunterHairBack(bg, k, D));
            Color leg = k == KindHisokaSd ? Color.FromArgb(240, 240, 246) : NjSkin;
            Color shoe = k == KindGonSd ? Color.FromArgb(34, 40, 36) : k == KindKilluaSd ? Color.FromArgb(70, 60, 120) : Color.FromArgb(70, 46, 96);
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.05f, 0.322f);
                VLimb(g, k, D, leg, k == KindHisokaSd ? 0.06f : 0.044f, P(s * 0.046f, 0.235f), f, 0);
                if (k == KindGonSd) VRound(g, k, D, Color.FromArgb(52, 124, 66), Color.FromArgb(36, 92, 48), 0, f.X - 0.034f, f.Y - 0.052f, 0.068f, 0.062f, 0.02f);   // his boots
                VOval(g, k, D, shoe, Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.022f, 0.08f, 0.042f);
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.2f, -0.02f, 0.4f, 0.34f), bg => HunterClothes(bg, k, D));
        }

        static void HunterHairBack(Graphics g, int k, float D)
        {
            Color hair, shade, light;
            HunterHairColors(k, out hair, out shade, out light);
            float[] xy = k == KindGonSd ? GonHair : k == KindKilluaSd ? KilluaHair : HisokaHair;
            SpikyHair(g, k, D, xy, hair, shade, light, -0.25f);
            if (k == KindGonSd)
                using (GraphicsPath h = TnSpikes(D, 0.07f, xy))
                {
                    // The dark green at the tips of his spikes.
                    Region old = g.Clip;
                    g.SetClip(h, CombineMode.Intersect);
                    using (SolidBrush tip = new SolidBrush(Color.FromArgb(44, 110, 66)))
                        for (int i = 1; i < xy.Length / 2 - 1; i += 2)
                        {
                            PointF a = P(xy[i * 2 - 2] * D, xy[i * 2 - 1] * D), t = P(xy[i * 2] * D, xy[i * 2 + 1] * D), c = P(xy[i * 2 + 2] * D, xy[i * 2 + 3] * D);
                            g.FillPolygon(tip, new PointF[] { t, Lerp(t, a, 0.3f), Lerp(t, c, 0.3f) });
                        }
                    g.Clip = old;
                    old.Dispose();
                }
            foreach (int s in Sides)
            {
                VOval(g, k, D, NjSkin, NjSkinShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, 170, 104, 84), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
        }

        // Killua's bangs, in front of his face over the tops of his eyes (drawn once and kept).
        static void KilluaFront(Graphics g, float D)
        {
            int k = KindKilluaSd;
            Color hair, shade, light;
            HunterHairColors(k, out hair, out shade, out light);
            using (GraphicsPath b = TnSpikes(D, 0.04f, KilluaBangs))
            {
                Region old = g.Clip;
                g.SetClip(new RectangleF(-D, -0.33f * D, 2 * D, D), CombineMode.Intersect);
                Stroke(g, b, TnInk(k), TnW(D) * 2);
                g.Clip = old;
                VPaint(g, b, hair, shade, D, 40);
                g.SetClip(b, CombineMode.Intersect);
                foreach (float x in new float[] { -0.2f, -0.06f, 0.08f, 0.21f })
                    TnLine(g, Color.FromArgb(120, 150, 152, 190), 0.006f, D, x * 0.8f, -0.36f, x, -0.24f, x + 0.012f, -0.16f);
                g.Clip = old;
                old.Dispose();
            }
        }

        static void HunterClothes(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindGonSd:
                {
                    // Green shorts with a dark belt and its buckle; the green jacket over them, the orange line down its
                    // front and its high collar, edged in orange.
                    Color green = Color.FromArgb(58, 150, 72), greenShade = Color.FromArgb(38, 112, 52), orange = Color.FromArgb(240, 116, 40);
                    VRound(g, k, D, Color.FromArgb(46, 120, 58), Color.FromArgb(32, 92, 42), 30, -0.1f, 0.2f, 0.2f, 0.08f, 0.025f);
                    TnLine(g, TnInk(k), 0.006f, D, 0, 0.235f, 0, 0.278f);
                    VRound(g, k, D, green, greenShade, 50, -0.108f, 0.07f, 0.216f, 0.15f, 0.045f);
                    VRound(g, k, D, Color.FromArgb(44, 44, 48), Color.Empty, 0, -0.104f, 0.202f, 0.208f, 0.024f, 0.01f);
                    VRound(g, k, D, Color.FromArgb(214, 218, 226), Color.Empty, 40, -0.022f, 0.2f, 0.044f, 0.028f, 0.006f);
                    TnLine(g, orange, 0.013f, D, 0, 0.1f, 0, 0.198f);
                    foreach (int s in Sides)
                    {
                        VPoly(g, k, D, Color.FromArgb(30, 84, 42), Color.Empty, 0, s * 0.012f, 0.105f, s * 0.05f, 0.04f, s * 0.12f, 0.055f, s * 0.11f, 0.12f);
                        TnLine(g, orange, 0.008f, D, s * 0.016f, 0.1f, s * 0.052f, 0.048f, s * 0.112f, 0.06f);
                    }
                    break;
                }
                case KindKilluaSd:
                {
                    // White shorts, and his loose white top with the dark blue neck of the shirt under it.
                    VRound(g, k, D, Color.FromArgb(242, 242, 248), Color.FromArgb(206, 208, 222), 30, -0.1f, 0.205f, 0.2f, 0.07f, 0.025f);
                    TnLine(g, TnInk(k), 0.006f, D, 0, 0.235f, 0, 0.272f);
                    VRound(g, k, D, Color.FromArgb(250, 250, 252), Color.FromArgb(212, 214, 230), 50, -0.112f, 0.07f, 0.224f, 0.17f, 0.05f);
                    VBlob(g, k, D, Color.FromArgb(44, 52, 110), Color.FromArgb(28, 34, 80), 0, 0.5f, -0.055f, 0.064f, 0.055f, 0.064f, 0.042f, 0.1f, 0, 0.112f, -0.042f, 0.1f);
                    break;
                }
                default:
                {
                    // White trousers, a purple sash, the pale blue top with a heart and a spade, the pink ring at his neck.
                    Color suit = Color.FromArgb(124, 70, 176);
                    VRound(g, k, D, Color.FromArgb(240, 240, 246), Color.FromArgb(204, 206, 222), 30, -0.1f, 0.2f, 0.2f, 0.075f, 0.025f);
                    VRound(g, k, D, Color.FromArgb(222, 242, 248), Color.FromArgb(178, 214, 228), 50, -0.11f, 0.07f, 0.22f, 0.15f, 0.045f);
                    VRound(g, k, D, Color.FromArgb(116, 66, 170), Color.FromArgb(84, 44, 130), 30, -0.108f, 0.196f, 0.216f, 0.034f, 0.012f);
                    using (GraphicsPath h = Heart(-0.05f * D, 0.152f * D, 0.05f * D)) FillSolid(g, h, suit);
                    using (GraphicsPath sp = Heart(0.05f * D, 0.142f * D, 0.05f * D))
                    using (Matrix m = new Matrix(1, 0, 0, -1, 0, 2 * 0.142f * D))
                    {
                        sp.Transform(m);
                        FillSolid(g, sp, suit);
                    }
                    using (GraphicsPath stem = KPoly(D, 0.05f, 0.15f, 0.062f, 0.17f, 0.038f, 0.17f)) FillSolid(g, stem, suit);
                    VRound(g, k, D, Color.FromArgb(244, 120, 150), Color.FromArgb(206, 80, 116), 40, -0.07f, 0.056f, 0.14f, 0.036f, 0.016f);
                    TnLine(g, Color.FromArgb(170, 196, 66, 100), 0.005f, D, -0.06f, 0.074f, 0.06f, 0.074f);
                    break;
                }
            }
        }

        static void HunterArm(Graphics g, float D, int k, int side, PointF h)
        {
            PointF a = P(side * 0.1f, 0.115f);
            switch (k)
            {
                case KindGonSd: VLimb(g, k, D, Color.FromArgb(58, 150, 72), 0.056f, a, h, side * 0.03f); break;
                case KindKilluaSd: VLimb(g, k, D, Color.FromArgb(44, 52, 110), 0.054f, a, h, side * 0.03f); break;
                default:
                    VLimb(g, k, D, NjSkin, 0.046f, a, h, side * 0.03f);
                    VLimb(g, k, D, Color.FromArgb(222, 242, 248), 0.06f, a, Lerp(a, h, 0.3f), 0);
                    break;
            }
        }

        // Brows, Gon's blush, and Hisoka's star and teardrop.
        static void HunterDetails(Graphics g, int k, float D, float f)
        {
            foreach (int s in Sides)
            {
                if (k == KindGonSd)
                {
                    TnLine(g, Color.FromArgb(46, 36, 30), 0.017f, D, s * 0.05f, f - 0.11f, s * 0.1f, f - 0.124f, s * 0.155f, f - 0.114f);
                    Glow(g, s * 0.16f * D, (f + 0.065f) * D, 0.08f * D, 0.036f * D, Color.FromArgb(255, 130, 120), 60);
                }
                else if (k == KindHisokaSd)
                    TnLine(g, Color.FromArgb(160, 44, 96), 0.009f, D, s * 0.05f, f - 0.085f, s * 0.11f, f - 0.112f, s * 0.165f, f - 0.1f);
            }
            if (k != KindHisokaSd) return;
            using (GraphicsPath st = Star(0.165f * D, (f + 0.08f) * D, 0.03f * D, 0.45f)) FillSolid(g, st, Color.FromArgb(232, 44, 72));
            using (GraphicsPath tear = new GraphicsPath())
            {
                float x = -0.165f * D, y = (f + 0.085f) * D, r = 0.014f * D;
                tear.AddArc(x - r, y - r, r * 2, r * 2, 0, 180);
                tear.AddLine(x - r, y, x, y - r * 2.6f);
                tear.CloseFigure();
                FillSolid(g, tear, Color.FromArgb(70, 150, 230));
            }
        }

        static string[] HunterLines(int k)
        {
            if (!I18n.IsSpanish)
                switch (k)
                {
                    case KindGonSd: return new string[] { "Come on, Killua!", "Rock, paper\u2026 scissors!", "I'm going to find my dad!", "I'm Gon Freecss." };
                    case KindKilluaSd: return new string[] { "My name is Killua.", "Any chocolate left?", "Gon, you're my best friend.", "Godspeed!" };
                    default: return new string[] { "I'll save that one for later.", "What a delicious fruit!", "Bungee Gum has the properties of both rubber and gum \u2665", "Ripen a little more\u2026 \u2660" };
                }
            switch (k)
            {
                case KindGonSd: return new string[] { "\u00A1Vamos, Killua!", "\u00A1Piedra, papel\u2026 \u00A1tijera!", "\u00A1Voy a encontrar a mi padre!", "Soy Gon Freecss." };
                case KindKilluaSd: return new string[] { "Me llamo Killua.", "\u00BFQueda chocolate?", "Gon, eres mi mejor amigo.", "\u00A1Godspeed!" };
                default: return new string[] { "Esa me la guardo.", "\u00A1Qu\u00E9 fruta tan deliciosa!", "El chicle el\u00E1stico tiene las propiedades del chicle y de la goma \u2665", "Madura un poco m\u00E1s\u2026 \u2660" };
            }
        }
    }
}
