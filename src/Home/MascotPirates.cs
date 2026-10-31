// Stackshot - One Piece's Luffy and Zoro as anime chibis, like the ninjas (MascotToons). Luffy: the straw hat with its
// red band over his messy black hair, the scar under his eye and his big toothy grin, the red shirt open over the X scar
// on his chest with its yellow buttons, the yellow sash, blue shorts and sandals. Zoro: short green hair, the three gold
// earrings, a stern frown, the white shirt, the green haramaki, black trousers and boots, the black bandana on his arm
// and his three swords at his hip. Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindLuffySd = 103, KindZoroSd = 104;

        static readonly Color LfRed = Color.FromArgb(222, 46, 44), LfRedShade = Color.FromArgb(170, 26, 30);
        static readonly Color ZrGreen = Color.FromArgb(112, 190, 110), ZrGreenShade = Color.FromArgb(60, 130, 70), ZrGreenLight = Color.FromArgb(196, 242, 184);

        // Luffy's hair showing under his hat, messy spikes out at the sides; Zoro's, short and spiky all over.
        static readonly float[] LuffyHair =
        {
            -0.255f, -0.05f, -0.34f, -0.02f, -0.3f, -0.12f, -0.38f, -0.16f, -0.3f, -0.22f, -0.36f, -0.3f, -0.24f, -0.32f, -0.2f, -0.42f,
            0, -0.4f, 0.2f, -0.42f, 0.24f, -0.32f, 0.36f, -0.3f, 0.3f, -0.22f, 0.38f, -0.16f, 0.3f, -0.12f, 0.34f, -0.02f, 0.255f, -0.05f
        };
        static readonly float[] ZoroHair =
        {
            -0.255f, -0.12f, -0.31f, -0.2f, -0.27f, -0.27f, -0.33f, -0.34f, -0.22f, -0.36f, -0.22f, -0.45f, -0.1f, -0.42f, -0.04f, -0.5f,
            0.05f, -0.43f, 0.15f, -0.49f, 0.18f, -0.4f, 0.3f, -0.42f, 0.27f, -0.32f, 0.36f, -0.3f, 0.3f, -0.2f, 0.33f, -0.12f, 0.255f, -0.1f
        };
        // Their fringes: Luffy's spiky one under the brim of his hat, Zoro's short one at the top of his forehead.
        static readonly float[] LuffyFringe =
        {
            -0.3f, -0.3f, -0.1f, -0.38f, 0.1f, -0.38f, 0.3f, -0.3f, 0.27f, -0.12f, 0.2f, -0.24f, 0.13f, -0.14f, 0.07f, -0.25f, 0, -0.15f,
            -0.06f, -0.25f, -0.13f, -0.14f, -0.2f, -0.24f, -0.27f, -0.12f, -0.29f, -0.22f
        };
        static readonly float[] ZoroFringe =
        {
            -0.27f, -0.24f, -0.15f, -0.38f, 0.05f, -0.4f, 0.22f, -0.36f, 0.28f, -0.24f, 0.24f, -0.2f, 0.17f, -0.27f, 0.1f, -0.21f, 0.03f, -0.28f,
            -0.04f, -0.21f, -0.11f, -0.28f, -0.18f, -0.21f, -0.24f, -0.27f
        };

        // The head, kept: the face, and Zoro's fringe (Luffy's goes in front, under his hat).
        static void PirateHead(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, NjSkin, NjSkinShade, D, 0);
            if (k == KindZoroSd)
                using (GraphicsPath f = TnSpikes(D, 0.04f, ZoroFringe)) SlayerHair(g, k, f, D, ZrGreen, ZrGreenShade);
        }

        // In front of Luffy's face (kept): his fringe and the straw hat over it.
        static void LuffyFront(Graphics g, float D)
        {
            int k = KindLuffySd;
            using (GraphicsPath f = TnSpikes(D, 0.04f, LuffyFringe)) SlayerHair(g, k, f, D, Color.FromArgb(36, 34, 40), Color.FromArgb(14, 12, 16));
            const float cy = -0.3f, rx = 0.42f, ry = 0.065f, lip = 0.022f;
            Color straw = Color.FromArgb(246, 210, 110), strawShade = Color.FromArgb(214, 168, 70);
            using (GraphicsPath brim = new GraphicsPath())
            {
                brim.AddArc(-rx * D, (cy - ry) * D, 2 * rx * D, 2 * ry * D, 180, 180);
                brim.AddArc(-rx * D, (cy - ry + lip) * D, 2 * rx * D, 2 * ry * D, 0, 180);
                brim.CloseFigure();
                VFill(g, k, brim, Color.FromArgb(228, 186, 86), strawShade, D, 0);
            }
            using (GraphicsPath top = new GraphicsPath())
            {
                top.AddEllipse(-rx * D, (cy - ry) * D, 2 * rx * D, 2 * ry * D);
                VPaint(g, top, straw, Color.Empty, D, 40);
                using (Pen weave = new Pen(Color.FromArgb(60, 150, 104, 30), Math.Max(0.6f, D * 0.004f)))
                    for (int i = 1; i < 4; i++) g.DrawEllipse(weave, -rx * D * i / 4, (cy - ry * i / 4) * D, 2 * rx * D * i / 4, 2 * ry * D * i / 4);
            }
            using (GraphicsPath crown = new GraphicsPath())
            {
                crown.AddArc(-0.24f * D, (cy - 0.045f) * D, 0.48f * D, 0.09f * D, 180, -180);
                crown.AddBezier(P(0.24f * D, cy * D), P(0.25f * D, -0.46f * D), P(0.16f * D, -0.585f * D), P(0, -0.585f * D));
                crown.AddBezier(P(0, -0.585f * D), P(-0.16f * D, -0.585f * D), P(-0.25f * D, -0.46f * D), P(-0.24f * D, cy * D));
                crown.CloseFigure();
                VFill(g, k, crown, straw, strawShade, D, 70);
                // The red band round it.
                Region old = g.Clip;
                g.SetClip(crown, CombineMode.Intersect);
                using (GraphicsPath band = new GraphicsPath())
                {
                    band.AddArc(-0.26f * D, -0.43f * D, 0.52f * D, 0.08f * D, 180, -180);
                    band.AddArc(-0.26f * D, (cy - 0.04f) * D, 0.52f * D, 0.09f * D, 0, 180);
                    band.CloseFigure();
                    Stroke(g, band, TnInk(k), TnW(D) * 2);
                    VPaint(g, band, LfRed, LfRedShade, D, 0);
                }
                g.Clip = old;
                old.Dispose();
            }
        }

        // Behind: the hair with the ears (Zoro's earrings, his swords) kept, the legs stepping as they walk, the arms, and
        // over them the clothes (kept).
        static void PirateBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.45f, -0.55f, 0.9f, 0.95f), bg => PirateHairBack(bg, k, D));
            bool luffy = k == KindLuffySd;
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.05f, 0.322f);
                VLimb(g, k, D, luffy ? NjSkin : Color.FromArgb(40, 40, 48), luffy ? 0.044f : 0.062f, P(s * 0.046f, 0.235f), f, 0);
                if (luffy)
                {
                    // Straw sandals, a strap over the foot.
                    VOval(g, k, D, Color.FromArgb(200, 150, 80), Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.026f, 0.08f, 0.032f);
                    TnLine(g, Color.FromArgb(150, 90, 40), 0.012f, D, f.X - 0.026f, f.Y + 0.012f, f.X + 0.026f, f.Y + 0.012f);
                }
                else
                {
                    VRound(g, k, D, Color.FromArgb(30, 30, 36), Color.Empty, 0, f.X - 0.036f, f.Y - 0.04f, 0.072f, 0.05f, 0.018f);
                    VOval(g, k, D, Color.FromArgb(30, 30, 36), Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.022f, 0.08f, 0.042f);
                }
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.2f, -0.02f, 0.42f, 0.34f), bg => PirateClothes(bg, k, D));
        }

        static void PirateHairBack(Graphics g, int k, float D)
        {
            if (k == KindZoroSd)
            {
                // His three swords at his left hip, behind him: the sheaths running down and out.
                Color[] sheath = { Color.FromArgb(30, 30, 36), Color.FromArgb(120, 30, 40), Color.FromArgb(40, 36, 60) };
                for (int i = 0; i < 3; i++)
                    VLimb(g, k, D, sheath[i], 0.026f, P(0.07f + i * 0.02f, 0.14f), P(0.2f + i * 0.035f, 0.36f - i * 0.02f), 0);
                SpikyHair(g, k, D, ZoroHair, ZrGreen, ZrGreenShade, ZrGreenLight, -0.25f);
            }
            else SpikyHair(g, k, D, LuffyHair, Color.FromArgb(36, 34, 40), Color.FromArgb(14, 12, 16), Color.FromArgb(110, 110, 130), -0.2f);
            foreach (int s in Sides)
            {
                VOval(g, k, D, NjSkin, NjSkinShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, 170, 104, 84), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
            if (k == KindZoroSd)
                for (int i = 0; i < 3; i++)   // his three gold earrings
                    VOval(g, k, D, Color.FromArgb(250, 204, 70), Color.FromArgb(206, 150, 30), 90, 0.262f + i * 0.012f, 0.012f + i * 0.004f, 0.014f, 0.03f);
        }

        static void PirateClothes(Graphics g, int k, float D)
        {
            if (k == KindLuffySd)
            {
                // Blue shorts, rolled up; his bare chest with the X scar; the red shirt open over it with its yellow buttons;
                // the yellow sash.
                VRound(g, k, D, Color.FromArgb(62, 104, 196), Color.FromArgb(40, 72, 150), 30, -0.1f, 0.2f, 0.2f, 0.08f, 0.025f);
                VRound(g, k, D, Color.FromArgb(110, 150, 226), Color.Empty, 0, -0.1f, 0.262f, 0.2f, 0.02f, 0.008f);
                TnLine(g, TnInk(k), 0.006f, D, 0, 0.23f, 0, 0.262f);
                VRound(g, k, D, NjSkin, NjSkinShade, 30, -0.105f, 0.07f, 0.21f, 0.15f, 0.045f);
                TnLine(g, Color.FromArgb(200, 196, 110, 100), 0.008f, D, -0.04f, 0.1f, 0.04f, 0.17f);
                TnLine(g, Color.FromArgb(200, 196, 110, 100), 0.008f, D, 0.04f, 0.1f, -0.04f, 0.17f);
                foreach (int s in Sides) VPoly(g, k, D, LfRed, LfRedShade, 30, s * 0.035f, 0.07f, s * 0.11f, 0.075f, s * 0.115f, 0.205f, s * 0.06f, 0.205f);
                for (int i = 0; i < 3; i++) VOval(g, k, D, Color.FromArgb(250, 208, 60), Color.Empty, 60, -0.058f, 0.105f + i * 0.035f, 0.016f, 0.016f);
                VRound(g, k, D, Color.FromArgb(248, 204, 64), Color.FromArgb(214, 164, 30), 40, -0.108f, 0.19f, 0.216f, 0.032f, 0.012f);
                return;
            }
            // Zoro: his white shirt, the green haramaki round his middle, and his swords' hilts above it.
            VRound(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(214, 216, 222), 40, -0.11f, 0.07f, 0.22f, 0.15f, 0.045f);
            TnLine(g, Color.FromArgb(150, 160, 160, 170), 0.006f, D, -0.03f, 0.075f, 0, 0.1f, 0.03f, 0.075f);
            using (GraphicsPath h = Theme.Round(new RectangleF(-0.112f * D, 0.165f * D, 0.224f * D, 0.065f * D), 0.02f * D))
            {
                VFill(g, k, h, Color.FromArgb(70, 150, 80), Color.FromArgb(48, 112, 58), D, 30);
                Region old = g.Clip;
                g.SetClip(h, CombineMode.Intersect);
                for (int i = 1; i < 3; i++) TnLine(g, Color.FromArgb(120, 30, 80, 40), 0.006f, D, -0.12f, 0.165f + i * 0.022f, 0.12f, 0.165f + i * 0.022f);
                g.Clip = old;
                old.Dispose();
            }
            Color[] hilt = { Color.FromArgb(244, 244, 240), Color.FromArgb(170, 36, 48), Color.FromArgb(34, 34, 40) };
            for (int i = 0; i < 3; i++)
            {
                PointF a = P(0.075f + i * 0.02f, 0.2f), b = P(0.04f + i * 0.02f, 0.12f);
                VLimb(g, k, D, hilt[i], 0.022f, a, b, 0);
                VRound(g, k, D, Color.FromArgb(220, 180, 60), Color.Empty, 0, a.X - 0.018f, a.Y - 0.01f, 0.036f, 0.014f, 0.004f);
            }
        }

        static void PirateArm(Graphics g, float D, int k, int side, PointF h)
        {
            PointF a = P(side * 0.1f, 0.115f);
            VLimb(g, k, D, NjSkin, 0.048f, a, h, side * 0.03f);
            if (k == KindLuffySd) VLimb(g, k, D, LfRed, 0.062f, a, Lerp(a, h, 0.36f), 0);
            else
            {
                VLimb(g, k, D, Color.FromArgb(250, 250, 248), 0.06f, a, Lerp(a, h, 0.3f), 0);
                if (side > 0) VLimb(g, k, D, Color.FromArgb(34, 34, 40), 0.056f, Lerp(a, h, 0.36f), Lerp(a, h, 0.48f), 0);   // the bandana on his arm
            }
        }

        // Luffy's scar under his eye, two stitches across it; Zoro's frowning brows.
        static void PirateDetails(Graphics g, int k, float D, float f)
        {
            if (k == KindLuffySd)
            {
                Color scar = Color.FromArgb(200, 150, 80, 70);
                TnLine(g, scar, 0.007f, D, 0.09f, f + 0.1f, 0.13f, f + 0.108f, 0.17f, f + 0.096f);
                for (int i = 0; i < 2; i++) TnLine(g, scar, 0.006f, D, 0.117f + i * 0.03f, f + 0.091f, 0.112f + i * 0.03f, f + 0.119f);
                return;
            }
            foreach (int s in Sides) TnLine(g, Color.FromArgb(46, 100, 54), 0.017f, D, s * 0.05f, f - 0.085f, s * 0.1f, f - 0.105f, s * 0.162f, f - 0.118f);
        }

        static string[] PirateLines(int k)
        {
            if (!I18n.IsSpanish)
            {
                if (k == KindLuffySd)
                    return new string[] { "I'm gonna be King of the Pirates!", "I'm hungry! Meat!", "Gomu Gomu no\u2026!", "Shishishi!", "Join my crew!" };
                return new string[] { "Nothing happened here.", "Where am I? I got lost again.", "Santoryu!", "I'll be the world's greatest swordsman.", "Time for a nap." };
            }
            if (k == KindLuffySd)
                return new string[] { "\u00A1Voy a ser el Rey de los Piratas!", "\u00A1Tengo hambre! \u00A1Carne!", "\u00A1Gomu Gomu no\u2026!", "\u00A1Shishishi!", "\u00A1\u00DAnete a mi tripulaci\u00F3n!" };
            return new string[] { "Aqu\u00ED no ha pasado nada.", "\u00BFD\u00F3nde estoy? Me he vuelto a perder.", "\u00A1Santoryu!", "Ser\u00E9 el mejor espadach\u00EDn del mundo.", "Me echo una siesta." };
        }
    }
}
