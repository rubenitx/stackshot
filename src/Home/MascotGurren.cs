// Stackshot - Gurren Lagann's Kamina as an anime chibi, like the ninjas (MascotToons): his blue hair spiking up and
// back, the pointed red shades over his eyes and his big toothy grin; bare-chested under the red cape with its tall
// collar, the blue flame tattoo, the gold cord across his chest, the white wrap and belt round his middle, dark baggy
// trousers, and his katana slung over his shoulder. Fan tribute, not affiliated with its owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindKaminaSd = 105;

        static readonly Color KmHair = Color.FromArgb(66, 92, 214), KmHairShade = Color.FromArgb(34, 46, 140), KmHairLight = Color.FromArgb(150, 190, 255);
        static readonly Color KmRed = Color.FromArgb(214, 40, 40), KmRedShade = Color.FromArgb(150, 20, 26);

        // His hair behind his head, swept up and back in big spikes; the few that fall over his forehead.
        static readonly float[] KmSpikes =
        {
            -0.255f, -0.1f, -0.42f, -0.22f, -0.3f, -0.27f, -0.46f, -0.42f, -0.26f, -0.4f, -0.37f, -0.62f, -0.14f, -0.46f, -0.13f, -0.71f,
            0, -0.48f, 0.12f, -0.73f, 0.12f, -0.46f, 0.35f, -0.65f, 0.26f, -0.4f, 0.49f, -0.47f, 0.3f, -0.26f, 0.45f, -0.18f, 0.255f, -0.08f
        };
        static readonly float[] KmFringe =
        {
            -0.28f, -0.25f, -0.15f, -0.39f, 0.05f, -0.41f, 0.22f, -0.37f, 0.29f, -0.25f, 0.24f, -0.2f, 0.17f, -0.28f, 0.09f, -0.17f, 0.02f, -0.29f,
            -0.06f, -0.18f, -0.13f, -0.29f, -0.21f, -0.19f, -0.25f, -0.27f
        };

        static void KmHead(Graphics g, GraphicsPath face, float D)
        {
            int k = KindKaminaSd;
            VFill(g, k, face, NjSkin, NjSkinShade, D, 0);
            using (GraphicsPath f = TnSpikes(D, 0.04f, KmFringe)) SlayerHair(g, k, f, D, KmHair, KmHairShade);
        }

        // Behind: the cape and the katana over his shoulder with his hair and ears (kept), the legs stepping as he walks,
        // the arms, and over them his clothes (kept).
        static void KmBack(Graphics g, float D, MascotPose p, Geo geo)
        {
            int k = KindKaminaSd;
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.55f, -0.8f, 1.1f, 1.18f), bg => KmBehind(bg, D));
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.052f, 0.322f);
                VLimb(g, k, D, Color.FromArgb(70, 76, 96), 0.066f, P(s * 0.048f, 0.235f), f, 0);
                VOval(g, k, D, Color.FromArgb(60, 42, 34), Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.022f, 0.082f, 0.042f);
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.2f, -0.06f, 0.4f, 0.36f), bg => KmClothes(bg, D));
        }

        static void KmBehind(Graphics g, float D)
        {
            int k = KindKaminaSd;
            // The red cape hanging behind him, flaring out to his knees.
            VBlob(g, k, D, KmRed, KmRedShade, 30, 0.45f, -0.1f, 0.06f, 0.1f, 0.06f, 0.2f, 0.18f, 0.27f, 0.34f, 0.12f, 0.31f, 0, 0.34f, -0.12f, 0.31f, -0.27f, 0.34f, -0.2f, 0.18f);
            // The katana slung over his shoulder: the dark sheath down behind him, the guard and the wrapped hilt up past his
            // shoulder.
            VLimb(g, k, D, Color.FromArgb(44, 36, 40), 0.03f, P(0.2f, -0.12f), P(-0.32f, 0.3f), 0);
            VRound(g, k, D, Color.FromArgb(214, 176, 70), Color.Empty, 40, 0.19f, -0.15f, 0.05f, 0.05f, 0.01f);
            VLimb(g, k, D, Color.FromArgb(234, 230, 222), 0.03f, P(0.24f, -0.16f), P(0.36f, -0.27f), 0);
            for (int i = 0; i < 3; i++)
            {
                PointF a = Lerp(P(0.24f, -0.16f), P(0.36f, -0.27f), 0.2f + i * 0.28f);
                TnLine(g, Color.FromArgb(60, 40, 40), 0.006f, D, a.X - 0.012f, a.Y - 0.012f, a.X + 0.012f, a.Y + 0.012f);
            }
            SpikyHair(g, k, D, KmSpikes, KmHair, KmHairShade, KmHairLight, -0.25f);
            foreach (int s in Sides)
            {
                VOval(g, k, D, NjSkin, NjSkinShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, 170, 104, 84), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
        }

        // His bare chest with the blue flame tattoo and the gold cord across it, the tall collar of his cape, the white wrap
        // round his middle and the belt.
        static void KmClothes(Graphics g, float D)
        {
            int k = KindKaminaSd;
            VRound(g, k, D, Color.FromArgb(70, 76, 96), Color.FromArgb(48, 52, 70), 30, -0.105f, 0.2f, 0.21f, 0.08f, 0.025f);
            VRound(g, k, D, NjSkin, NjSkinShade, 30, -0.105f, 0.07f, 0.21f, 0.15f, 0.045f);
            using (GraphicsPath flame = KPoly(D, -0.085f, 0.095f, -0.06f, 0.08f, -0.05f, 0.1f, -0.03f, 0.085f, -0.035f, 0.115f, -0.015f, 0.11f, -0.04f, 0.14f, -0.075f, 0.13f))
                FillSolid(g, flame, Color.FromArgb(200, 40, 70, 200));
            TnLine(g, Color.FromArgb(120, 190, 120, 96), 0.006f, D, 0, 0.13f, 0, 0.17f);
            TnLine(g, Color.FromArgb(250, 200, 60), 0.012f, D, -0.1f, 0.085f, -0.04f, 0.13f, 0.04f, 0.13f, 0.1f, 0.085f);
            TnLine(g, Color.FromArgb(214, 160, 30), 0.009f, D, -0.1f, 0.1f, -0.03f, 0.155f, 0.03f, 0.155f, 0.1f, 0.1f);
            using (GraphicsPath wrap = Theme.Round(new RectangleF(-0.108f * D, 0.17f * D, 0.216f * D, 0.045f * D), 0.012f * D))
            {
                VFill(g, k, wrap, Color.FromArgb(246, 246, 240), Color.FromArgb(210, 210, 206), D, 0);
                TnLine(g, Color.FromArgb(120, 170, 170, 170), 0.005f, D, -0.1f, 0.192f, 0.1f, 0.192f);
            }
            VRound(g, k, D, Color.FromArgb(60, 48, 56), Color.Empty, 0, -0.106f, 0.212f, 0.212f, 0.018f, 0.006f);
            VRound(g, k, D, Color.FromArgb(200, 200, 210), Color.Empty, 40, -0.02f, 0.208f, 0.04f, 0.026f, 0.006f);
            // The cape's tall collar, standing up either side of his neck.
            foreach (int s in Sides) VPoly(g, k, D, KmRed, KmRedShade, 40, s * 0.06f, 0.075f, s * 0.1f, -0.02f, s * 0.16f, 0.02f, s * 0.13f, 0.1f);
        }

        static void KmArm(Graphics g, float D, int side, PointF h)
        {
            int k = KindKaminaSd;
            PointF a = P(side * 0.1f, 0.115f);
            VLimb(g, k, D, NjSkin, 0.05f, a, h, side * 0.03f);
            VLimb(g, k, D, Color.FromArgb(60, 48, 56), 0.056f, Lerp(a, h, 0.78f), Lerp(a, h, 0.9f), 0);
            VLimb(g, k, D, KmRed, 0.066f, a, Lerp(a, h, 0.2f), 0);
        }

        // The pointed red shades over his eyes, turning with his face: a lens each side, its outer end a sharp point out
        // past his face, a glint across each.
        static void KmShades(Graphics g, float D, float f)
        {
            int k = KindKaminaSd;
            foreach (int s in Sides)
                using (GraphicsPath lens = KPoly(D, s * 0.035f, f - 0.045f, s * 0.3f, f - 0.08f, s * 0.2f, f - 0.015f, s * 0.095f, f + 0.045f, s * 0.04f, f + 0.005f))
                {
                    Stroke(g, lens, TnInk(k), TnW(D) * 2);
                    RectangleF r = lens.GetBounds();
                    using (LinearGradientBrush b = new LinearGradientBrush(RectangleF.Inflate(r, 0, 1), Color.FromArgb(215, 255, 120, 70), Color.FromArgb(225, 214, 30, 40), 90f)) g.FillPath(b, lens);
                    TnLine(g, Color.FromArgb(200, 255, 236, 220), 0.008f, D, s * 0.08f, f - 0.035f, s * 0.2f, f - 0.055f);
                }
            TnLine(g, TnInk(k), 0.012f, D, -0.04f, f - 0.042f, 0, f - 0.05f, 0.04f, f - 0.042f);
        }

        static string[] KmLines()
        {
            if (!I18n.IsSpanish)
                return new string[] { "Just who the hell do you think I am?!", "Your drill is the drill that will pierce the heavens!", "Believe in the you who believes in yourself!",
                                      "Kick logic to the curb and do the impossible!" };
            return new string[] { "\u00BF\u00A1Qui\u00E9n demonios te crees que soy!?", "\u00A1Tu taladro es el que perforar\u00E1 los cielos!", "\u00A1Cree en el t\u00FA que cree en ti!",
                                  "\u00A1Manda la l\u00F3gica a paseo y haz lo imposible!" };
        }
    }
}
