// Stackshot - More heroes as anime chibis, drawn like the others in MascotPersonas. The Avengers: Captain America (the
// blue helmet with its white A and little wings, the star over the red and white stripes, the round shield), Thor (long
// golden hair and beard, the dark armour with its silver discs, the red cape, Mjolnir crackling in his hand), the Hulk
// (green and broad, black messy hair, a scowl and clenched teeth, torn purple shorts), Black Widow (red hair, the black
// suit, the hourglass on her belt, her bracelets), Hawkeye (short fair hair, the black and purple vest, bare arms, the bow
// and the quiver), Black Panther (the black mask with its ears, silver lines and white eyes, the silver necklace), Doctor
// Strange (hair swept back and grey at the temples, the goatee, the blue tunic, the red cloak's high collar, the Eye of
// Agamotto and a spell turning at his hand), Captain Marvel (blonde, the red and blue suit with the gold star, glowing
// hands) and the Scarlet Witch (long auburn hair, the red crown and coat, red magic at her hands). And three titans from
// Attack on Titan: the Colossal Titan (no skin: red muscle, a lipless grin of teeth, eyes deep in their sockets, steam),
// the Attack Titan (long dark hair, glowing green eyes with the lines under them, the bare jaw) and the Armored Titan
// (white plates over his face and body, short blond hair, glowing eyes). Fan tributes, not affiliated with their owners
// (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindCapSd = 128, KindThorSd = 129, KindHulkSd = 130, KindWidowSd = 131, KindHawkSd = 132, KindPantherSd = 133,
                         KindStrangeSd = 134, KindCarolSd = 135, KindWandaSd = 136, KindColossalSd = 137, KindAttackSd = 138, KindArmoredSd = 139;
        public const int KindSmileTitanSd = 159, KindBoboTitanSd = 160;
        static readonly Color TtSkin = Color.FromArgb(244, 206, 182), TtSkinShade = Color.FromArgb(214, 166, 142), TtHair = Color.FromArgb(236, 208, 130), TtHairShade = Color.FromArgb(196, 160, 82);

        static readonly Color CaBlue = Color.FromArgb(44, 74, 156), CaBlueShade = Color.FromArgb(26, 46, 108), CaRed = Color.FromArgb(204, 36, 46), CaRedShade = Color.FromArgb(142, 20, 30);
        static readonly Color CaWhite = Color.FromArgb(248, 248, 252), CaWhiteShade = Color.FromArgb(202, 206, 218), CaBrown = Color.FromArgb(122, 80, 46);
        static readonly Color ThHair = Color.FromArgb(240, 200, 104), ThHairShade = Color.FromArgb(198, 148, 56), ThBeard = Color.FromArgb(206, 156, 72);
        static readonly Color ThArmor = Color.FromArgb(64, 68, 84), ThArmorShade = Color.FromArgb(36, 38, 50), ThSilver = Color.FromArgb(214, 218, 230), ThSilverShade = Color.FromArgb(150, 156, 174);
        static readonly Color HkSkin = Color.FromArgb(122, 182, 76), HkSkinShade = Color.FromArgb(80, 132, 44), HkHair = Color.FromArgb(34, 36, 34), HkHairShade = Color.FromArgb(14, 16, 14);
        static readonly Color HkPurple = Color.FromArgb(114, 62, 156), HkPurpleShade = Color.FromArgb(74, 36, 108);
        static readonly Color BwHair = Color.FromArgb(200, 72, 44), BwHairShade = Color.FromArgb(142, 40, 24), BwSuit = Color.FromArgb(40, 40, 50), BwSuitShade = Color.FromArgb(20, 20, 26);
        static readonly Color HeHair = Color.FromArgb(156, 116, 66), HeHairShade = Color.FromArgb(106, 74, 38), HePurple = Color.FromArgb(104, 58, 150), HePurpleShade = Color.FromArgb(66, 32, 102);
        static readonly Color BpBlack = Color.FromArgb(40, 40, 52), BpBlackShade = Color.FromArgb(18, 18, 26), BpSilver = Color.FromArgb(206, 210, 224);
        static readonly Color DrHair = Color.FromArgb(52, 40, 36), DrHairShade = Color.FromArgb(28, 20, 18), DrGrey = Color.FromArgb(178, 176, 180), DrBlue = Color.FromArgb(38, 66, 120), DrBlueShade = Color.FromArgb(22, 40, 78);
        static readonly Color DrRed = Color.FromArgb(194, 30, 40), DrRedShade = Color.FromArgb(130, 14, 24), DrGold = Color.FromArgb(236, 190, 74), DrTan = Color.FromArgb(180, 136, 88);
        static readonly Color CmHair = Color.FromArgb(242, 208, 126), CmHairShade = Color.FromArgb(198, 152, 72), CmBlue = Color.FromArgb(40, 70, 148), CmBlueShade = Color.FromArgb(24, 44, 100);
        static readonly Color SwHair = Color.FromArgb(160, 66, 48), SwHairShade = Color.FromArgb(110, 38, 26), SwRed = Color.FromArgb(176, 26, 42), SwRedShade = Color.FromArgb(116, 12, 26);
        static readonly Color CtMuscle = Color.FromArgb(212, 74, 62), CtMuscleShade = Color.FromArgb(152, 42, 36), CtFiber = Color.FromArgb(118, 26, 24);
        static readonly Color AtSkin = Color.FromArgb(238, 200, 172), AtSkinShade = Color.FromArgb(208, 164, 136), AtHair = Color.FromArgb(56, 40, 34), AtHairShade = Color.FromArgb(32, 22, 18);
        static readonly Color ArPlate = Color.FromArgb(238, 230, 208), ArPlateShade = Color.FromArgb(192, 180, 152), ArHair = Color.FromArgb(232, 202, 122), ArMuscle = Color.FromArgb(194, 78, 68);

        // Hair outlines for TnSpikes / SpikyHair (points alternating notch and tip, clockwise).
        static readonly float[] HulkFringe =
        {
            -0.29f, -0.24f, -0.12f, -0.41f, 0.08f, -0.42f, 0.28f, -0.31f, 0.285f, -0.17f, 0.22f, -0.25f, 0.17f, -0.17f, 0.11f, -0.27f, 0.05f, -0.19f,
            0, -0.28f, -0.06f, -0.2f, -0.12f, -0.28f, -0.18f, -0.19f, -0.235f, -0.26f, -0.285f, -0.17f
        };
        static readonly float[] HulkHair =
        {
            -0.25f, -0.06f, -0.31f, -0.04f, -0.3f, -0.13f, -0.35f, -0.19f, -0.3f, -0.26f, -0.34f, -0.33f, -0.26f, -0.37f, -0.27f, -0.45f, -0.14f, -0.43f,
            -0.08f, -0.5f, 0, -0.45f, 0.09f, -0.5f, 0.15f, -0.43f, 0.27f, -0.45f, 0.26f, -0.37f, 0.34f, -0.33f, 0.3f, -0.26f, 0.35f, -0.19f, 0.3f, -0.13f,
            0.31f, -0.04f, 0.25f, -0.06f
        };
        static readonly float[] HawkFringe =
        {
            -0.285f, -0.25f, -0.12f, -0.415f, 0.08f, -0.42f, 0.285f, -0.3f, 0.275f, -0.2f, 0.2f, -0.27f, 0.13f, -0.235f, 0.07f, -0.3f, 0, -0.26f,
            -0.07f, -0.31f, -0.13f, -0.25f, -0.2f, -0.29f, -0.27f, -0.2f
        };
        // Short hair standing up round the back of the head.
        static readonly float[] ShortHairBack =
        {
            -0.25f, -0.12f, -0.31f, -0.16f, -0.29f, -0.24f, -0.33f, -0.3f, -0.25f, -0.35f, -0.25f, -0.43f, -0.13f, -0.41f, -0.07f, -0.47f, 0, -0.43f,
            0.08f, -0.47f, 0.14f, -0.41f, 0.25f, -0.43f, 0.25f, -0.35f, 0.33f, -0.3f, 0.29f, -0.24f, 0.31f, -0.16f, 0.25f, -0.12f
        };
        static readonly float[] AttackFringe =
        {
            -0.3f, -0.22f, -0.13f, -0.41f, 0.08f, -0.42f, 0.29f, -0.3f, 0.3f, -0.05f, 0.24f, -0.17f, 0.2f, -0.06f, 0.14f, -0.2f, 0.08f, -0.1f,
            0.02f, -0.23f, -0.04f, -0.11f, -0.1f, -0.22f, -0.16f, -0.09f, -0.22f, -0.19f, -0.29f, -0.04f
        };
        static readonly float[] AttackHair =
        {
            -0.24f, 0.06f, -0.33f, 0.16f, -0.32f, 0.02f, -0.38f, -0.06f, -0.32f, -0.16f, -0.37f, -0.27f, -0.27f, -0.33f, -0.28f, -0.44f, -0.14f, -0.42f,
            -0.08f, -0.5f, 0, -0.45f, 0.09f, -0.5f, 0.15f, -0.42f, 0.28f, -0.44f, 0.27f, -0.33f, 0.37f, -0.27f, 0.32f, -0.16f, 0.38f, -0.06f, 0.32f, 0.02f,
            0.33f, 0.16f, 0.24f, 0.06f
        };

        // ---- Layout

        static bool AvFrame(int k, out float top, out float bottom)
        {
            bottom = 0.16f;
            switch (k)
            {
                case KindCapSd: top = -0.46f; break;
                case KindHawkSd:
                case KindArmoredSd: top = -0.48f; break;
                case KindColossalSd: top = -0.5f; break;
                case KindHulkSd:
                case KindWandaSd: top = -0.53f; break;
                case KindSmileTitanSd: top = -0.52f; break;
                default: top = -0.5f; break;
            }
            return true;
        }

        // ---- The head, kept: the face (or mask, or armour) and what goes over it.

        static void AvHead(Graphics g, int k, GraphicsPath face, float D)
        {
            switch (k)
            {
                case KindPantherSd: PantherMask(g, k, face, D); return;
                case KindColossalSd: ColossalFace(g, k, face, D); return;
                case KindArmoredSd: ArmoredFace(g, k, face, D); return;
                case KindSmileTitanSd: SmileTitanFace(g, k, face, D); return;
                case KindBoboTitanSd: BoboTitanFace(g, k, face, D); return;
            }
            VFill(g, k, face, k == KindHulkSd ? HkSkin : k == KindAttackSd ? AtSkin : NjSkin, k == KindHulkSd ? HkSkinShade : k == KindAttackSd ? AtSkinShade : NjSkinShade, D, 0);
            switch (k)
            {
                case KindCapSd: CapHelmet(g, k, face, D); break;
                case KindThorSd:
                    // A short golden beard round his jaw under the mouth, then his long hair parted in the middle and falling
                    // past his face.
                    VBlob(g, k, D, ThBeard, Darker(ThBeard, 0.25), 0, 0.45f, -0.232f, 0.0f, -0.2f, 0.045f, -0.12f, 0.072f, -0.045f, 0.088f, 0, 0.092f, 0.045f, 0.088f,
                          0.12f, 0.072f, 0.2f, 0.045f, 0.232f, 0.0f, 0.205f, 0.07f, 0.13f, 0.11f, 0, 0.128f, -0.13f, 0.11f, -0.205f, 0.07f);
                    TnLine(g, Darker(ThBeard, 0.08), 0.012f, D, -0.045f, 0.047f, 0, 0.042f, 0.045f, 0.047f);
                    using (GraphicsPath h = KCurve(D, 0.4f, 0, -0.47f, 0.2f, -0.44f, 0.288f, -0.32f, 0.292f, -0.14f, 0.284f, 0.01f, 0.266f, 0.085f, 0.238f, 0.035f, 0.226f, -0.07f,
                                                   0.2f, -0.17f, 0.13f, -0.24f, 0.035f, -0.265f, 0, -0.245f, -0.035f, -0.265f, -0.13f, -0.24f, -0.2f, -0.17f, -0.226f, -0.07f,
                                                   -0.238f, 0.035f, -0.266f, 0.085f, -0.284f, 0.01f, -0.292f, -0.14f, -0.288f, -0.32f, -0.2f, -0.44f))
                        SlayerHair(g, k, h, D, ThHair, ThHairShade);
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(150, 176, 126, 40), 0.007f, D, s * 0.04f, -0.42f, s * 0.12f, -0.33f, s * 0.2f, -0.2f);
                        TnLine(g, Color.FromArgb(130, 176, 126, 40), 0.006f, D, s * 0.26f, -0.2f, s * 0.262f, -0.05f, s * 0.255f, 0.04f);
                    }
                    break;
                case KindHulkSd:
                    using (GraphicsPath f = TnSpikes(D, 0.04f, HulkFringe)) SlayerHair(g, k, f, D, HkHair, HkHairShade);
                    break;
                case KindWidowSd:
                    // Red hair parted on one side and swept across her brow, falling in waves past her cheeks.
                    using (GraphicsPath h = KCurve(D, 0.42f, 0, -0.47f, 0.2f, -0.44f, 0.295f, -0.32f, 0.305f, -0.14f, 0.29f, 0.04f, 0.255f, 0.085f, 0.235f, -0.04f, 0.225f, -0.16f,
                                                   0.17f, -0.25f, 0.1f, -0.29f, 0.0f, -0.26f, -0.1f, -0.22f, -0.18f, -0.16f, -0.22f, -0.08f, -0.238f, 0.04f, -0.262f, 0.09f,
                                                   -0.295f, 0.03f, -0.305f, -0.14f, -0.295f, -0.32f, -0.2f, -0.44f))
                        SlayerHair(g, k, h, D, BwHair, BwHairShade);
                    TnLine(g, Color.FromArgb(150, 236, 130, 90), 0.008f, D, 0.08f, -0.4f, 0.0f, -0.33f, -0.12f, -0.26f);
                    break;
                case KindHawkSd:
                    using (GraphicsPath f = TnSpikes(D, 0.035f, HawkFringe)) SlayerHair(g, k, f, D, HeHair, HeHairShade);
                    break;
                case KindStrangeSd:
                {
                    // Dark hair swept back from a high brow, grey at the temples; the moustache and the little beard.
                    using (GraphicsPath h = KCurve(D, 0.42f, 0, -0.475f, 0.2f, -0.445f, 0.29f, -0.33f, 0.3f, -0.17f, 0.27f, -0.12f, 0.24f, -0.2f, 0.18f, -0.25f, 0.08f, -0.275f,
                                                   0, -0.255f, -0.08f, -0.275f, -0.18f, -0.25f, -0.24f, -0.2f, -0.27f, -0.12f, -0.3f, -0.17f, -0.29f, -0.33f, -0.2f, -0.445f))
                    {
                        SlayerHair(g, k, h, D, DrHair, DrHairShade);
                        Region old = g.Clip;
                        g.SetClip(h, CombineMode.Intersect);
                        foreach (int s in Sides) Glow(g, s * 0.27f * D, -0.17f * D, 0.16f * D, 0.2f * D, DrGrey, 230);
                        using (Pen st = new Pen(Color.FromArgb(120, 140, 120, 110), Math.Max(0.8f, D * 0.006f)))
                            for (int i = -2; i <= 2; i++) g.DrawBezier(st, P(i * 0.05f * D, -0.27f * D), P(i * 0.06f * D, -0.33f * D), P(i * 0.08f * D, -0.39f * D), P(i * 0.11f * D, -0.44f * D));
                        g.Clip = old;
                        old.Dispose();
                    }
                    VBlob(g, k, D, DrHair, DrHairShade, 0, 0.5f, -0.07f, 0.05f, -0.035f, 0.036f, 0, 0.04f, 0.035f, 0.036f, 0.07f, 0.05f, 0.04f, 0.052f, 0, 0.05f, -0.04f, 0.052f);
                    VBlob(g, k, D, Mac.Mix(DrHair, DrGrey, 0.3), DrHairShade, 0, 0.5f, -0.032f, 0.084f, 0.032f, 0.084f, 0.026f, 0.118f, 0, 0.13f, -0.026f, 0.118f);
                    break;
                }
                case KindCarolSd:
                    // Blonde hair to her shoulders, parted on one side.
                    using (GraphicsPath h = KCurve(D, 0.42f, 0, -0.47f, 0.2f, -0.44f, 0.295f, -0.32f, 0.305f, -0.14f, 0.295f, 0.04f, 0.262f, 0.1f, 0.235f, -0.03f, 0.222f, -0.15f,
                                                   0.16f, -0.24f, 0.06f, -0.285f, -0.04f, -0.25f, -0.13f, -0.22f, -0.2f, -0.16f, -0.228f, -0.05f, -0.24f, 0.06f, -0.268f, 0.1f,
                                                   -0.297f, 0.03f, -0.305f, -0.14f, -0.295f, -0.32f, -0.2f, -0.44f))
                        SlayerHair(g, k, h, D, CmHair, CmHairShade);
                    break;
                case KindWandaSd:
                    // Long auburn hair parted in the middle, then the red crown over her brow with its two horns.
                    using (GraphicsPath h = KCurve(D, 0.4f, 0, -0.47f, 0.2f, -0.44f, 0.295f, -0.32f, 0.305f, -0.14f, 0.295f, 0.04f, 0.27f, 0.12f, 0.235f, 0.04f, 0.222f, -0.08f,
                                                   0.19f, -0.18f, 0.12f, -0.25f, 0.035f, -0.27f, 0, -0.25f, -0.035f, -0.27f, -0.12f, -0.25f, -0.19f, -0.18f, -0.222f, -0.08f,
                                                   -0.235f, 0.04f, -0.27f, 0.12f, -0.295f, 0.04f, -0.305f, -0.14f, -0.295f, -0.32f, -0.2f, -0.44f))
                        SlayerHair(g, k, h, D, SwHair, SwHairShade);
                    foreach (int s in Sides)
                        VBlob(g, k, D, SwRed, SwRedShade, 0, 0.35f, s * 0.05f, -0.255f, s * 0.12f, -0.29f, s * 0.16f, -0.36f, s * 0.175f, -0.47f, s * 0.2f, -0.36f, s * 0.2f, -0.27f, s * 0.15f, -0.235f);
                    VBlob(g, k, D, SwRed, SwRedShade, 30, 0.3f, -0.17f, -0.24f, -0.06f, -0.27f, 0, -0.3f, 0.06f, -0.27f, 0.17f, -0.24f, 0.08f, -0.225f, 0, -0.17f, -0.08f, -0.225f);
                    break;
                case KindAttackSd:
                {
                    // The lines that run back from under his eyes, the bare jaw with its teeth and the ridges up the cheeks,
                    // then his long dark hair hanging over his brow.
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(200, 120, 70, 60), 0.011f, D, s * 0.06f, 0.03f, s * 0.13f, 0.04f, s * 0.2f, 0.01f, s * 0.24f, -0.03f);
                        TnLine(g, Color.FromArgb(170, 140, 84, 70), 0.009f, D, s * 0.1f, 0.075f, s * 0.16f, 0.06f, s * 0.215f, 0.035f);
                    }
                    AttackTeeth(g, k, D, 0.095f, 0.048f, 0.034f);
                    using (GraphicsPath f = TnSpikes(D, 0.04f, AttackFringe)) SlayerHair(g, k, f, D, AtHair, AtHairShade);
                    break;
                }
            }
        }

        // Captain America's helmet: over his head and down the sides of his face to the jaw, the face bare from the brow
        // down; the white A on his forehead and a small white wing at each temple.
        static void CapHelmet(Graphics g, int k, GraphicsPath face, float D)
        {
            Region old = g.Clip;
            g.SetClip(face, CombineMode.Intersect);
            using (GraphicsPath c = KCurve(D, 0.4f, -0.42f, -0.5f, 0.42f, -0.5f, 0.42f, 0.13f, 0.228f, 0.13f, 0.216f, 0.0f, 0.19f, -0.1f, 0.12f, -0.142f, 0, -0.152f,
                                           -0.12f, -0.142f, -0.19f, -0.1f, -0.216f, 0.0f, -0.228f, 0.13f, -0.42f, 0.13f))
            {
                Stroke(g, c, TnInk(k), TnW(D) * 2);
                VPaint(g, c, CaBlue, CaBlueShade, D, 40);
            }
            g.Clip = old;
            old.Dispose();
            TnLine(g, Color.FromArgb(230, 22, 30, 70), 0.028f, D, -0.038f, -0.198f, 0, -0.292f, 0.038f, -0.198f);
            TnLine(g, CaWhite, 0.017f, D, -0.038f, -0.198f, 0, -0.292f, 0.038f, -0.198f);
            TnLine(g, CaWhite, 0.014f, D, -0.019f, -0.234f, 0.019f, -0.234f);
            foreach (int s in Sides)
            {
                VBlob(g, k, D, CaWhite, CaWhiteShade, 0, 0.3f, s * 0.205f, -0.15f, s * 0.215f, -0.23f, s * 0.245f, -0.3f, s * 0.262f, -0.25f, s * 0.295f, -0.29f,
                      s * 0.3f, -0.225f, s * 0.33f, -0.235f, s * 0.31f, -0.165f);
                TnLine(g, Color.FromArgb(130, 120, 130, 150), 0.006f, D, s * 0.225f, -0.18f, s * 0.262f, -0.25f);
                TnLine(g, Color.FromArgb(130, 120, 130, 150), 0.006f, D, s * 0.235f, -0.17f, s * 0.295f, -0.22f);
            }
        }

        // Black Panther's mask: black over the whole head, silver lines framing his eyes and running down his face.
        static void PantherMask(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, BpBlack, BpBlackShade, D, 50);
            Color line = Color.FromArgb(210, BpSilver);
            TnLine(g, line, 0.008f, D, 0, -0.35f, 0, -0.13f);
            foreach (int s in Sides)
            {
                TnLine(g, line, 0.008f, D, s * 0.03f, -0.13f, s * 0.11f, -0.135f, s * 0.2f, -0.1f, s * 0.24f, -0.03f);
                TnLine(g, line, 0.007f, D, s * 0.03f, -0.1f, s * 0.05f, 0.0f, s * 0.1f, 0.06f, s * 0.15f, 0.085f);
                TnLine(g, Color.FromArgb(120, BpSilver), 0.006f, D, s * 0.06f, -0.3f, s * 0.13f, -0.22f, s * 0.2f, -0.18f);
            }
            Glow(g, -0.1f * D, -0.25f * D, 0.2f * D, 0.08f * D, Color.White, 50);
        }

        // ---- Behind: cape, cloak or quiver (kept), hair and ears (kept), legs, arms, clothes (kept), what is in their hands.

        static void AvBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            RectangleF area;
            if (AvCapeArea(k, out area)) DsCached(g, D, TnId(k, 0), area, bg => AvCape(bg, k, D));
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.5f, -0.72f, 1f, 0.98f), bg => AvHairBack(bg, k, D));
            foreach (int s in Sides) AvLeg(g, k, D, s, Foot(p, s, s * (k == KindHulkSd ? 0.06f : 0.05f), 0.322f));
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.24f, -0.02f, 0.48f, 0.36f), bg => AvClothes(bg, k, D));
            if (!p.HideHands) AvProps(g, k, D, p, geo);
        }

        static bool AvCapeArea(int k, out RectangleF r)
        {
            switch (k)
            {
                case KindThorSd: r = new RectangleF(-0.3f, 0, 0.6f, 0.42f); return true;
                case KindStrangeSd: r = new RectangleF(-0.37f, -0.3f, 0.74f, 0.68f); return true;
                case KindHawkSd: r = new RectangleF(0.0f, -0.24f, 0.38f, 0.56f); return true;
                default: r = RectangleF.Empty; return false;
            }
        }

        static void AvCape(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindThorSd:
                    VBlob(g, k, D, CaRed, CaRedShade, 0, 0.45f, -0.11f, 0.06f, 0.11f, 0.06f, 0.18f, 0.12f, 0.23f, 0.32f, 0.12f, 0.35f, 0, 0.32f, -0.12f, 0.35f, -0.23f, 0.32f, -0.18f, 0.12f);
                    break;
                case KindStrangeSd:
                    // The Cloak of Levitation: its high stiff collar rising behind his head, the red falling to his feet.
                    VBlob(g, k, D, DrRed, DrRedShade, 0, 0.35f, -0.12f, 0.06f, -0.19f, -0.07f, -0.27f, -0.24f, -0.33f, -0.12f, -0.31f, 0.03f, -0.25f, 0.13f, -0.23f, 0.31f,
                          -0.12f, 0.35f, 0, 0.32f, 0.12f, 0.35f, 0.23f, 0.31f, 0.25f, 0.13f, 0.31f, 0.03f, 0.33f, -0.12f, 0.27f, -0.24f, 0.19f, -0.07f, 0.12f, 0.06f);
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(120, 90, 8, 16), 0.008f, D, s * 0.2f, -0.08f, s * 0.26f, -0.2f);
                    break;
                case KindHawkSd:
                    // The quiver across his back, its arrows' fletchings over his right shoulder.
                    foreach (float[] a in new float[][] { new float[] { 0.2f, -0.16f }, new float[] { 0.245f, -0.13f }, new float[] { 0.28f, -0.1f } })
                    {
                        TnLine(g, Color.FromArgb(70, 60, 56), 0.01f, D, a[0] - 0.05f, a[1] + 0.12f, a[0], a[1]);
                        VBlob(g, k, D, HePurple, HePurpleShade, 0, 0.4f, a[0] - 0.012f, a[1] + 0.02f, a[0] - 0.02f, a[1] - 0.03f, a[0] + 0.004f, a[1] - 0.045f, a[0] + 0.022f, a[1] - 0.012f, a[0] + 0.01f, a[1] + 0.024f);
                    }
                    using (GraphicsPath q = KCurve(D, 0.3f, 0.07f, 0.26f, 0.12f, 0.28f, 0.29f, -0.04f, 0.25f, -0.07f))
                    {
                        Stroke(g, q, TnInk(k), TnW(D) * 2);
                        VPaint(g, q, Color.FromArgb(118, 82, 50), Color.FromArgb(82, 54, 30), D, 20);
                    }
                    break;
            }
        }

        static void AvHairBack(Graphics g, int k, float D)
        {
            bool ears = true;
            switch (k)
            {
                case KindCapSd: ears = false; break;
                case KindThorSd:
                    // His long hair down behind his shoulders, ending in locks.
                    VBlob(g, k, D, ThHair, ThHairShade, 30, 0.4f, 0, -0.47f, 0.19f, -0.44f, 0.29f, -0.34f, 0.315f, -0.16f, 0.315f, 0.02f, 0.3f, 0.14f, 0.31f, 0.22f, 0.255f, 0.17f,
                          0.22f, 0.21f, 0.18f, 0.14f, 0.12f, 0.1f, 0, 0.08f, -0.12f, 0.1f, -0.18f, 0.14f, -0.22f, 0.21f, -0.255f, 0.17f, -0.31f, 0.22f, -0.3f, 0.14f,
                          -0.315f, 0.02f, -0.315f, -0.16f, -0.29f, -0.34f, -0.19f, -0.44f);
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(140, 176, 126, 40), 0.007f, D, s * 0.29f, -0.1f, s * 0.29f, 0.08f, s * 0.3f, 0.2f);
                        TnLine(g, Color.FromArgb(120, 176, 126, 40), 0.006f, D, s * 0.2f, 0.06f, s * 0.22f, 0.16f);
                    }
                    ears = false;
                    break;
                case KindHulkSd: SpikyHair(g, k, D, HulkHair, HkHair, HkHairShade, Color.FromArgb(96, 100, 96), -0.25f); break;
                case KindWidowSd:
                    VBlob(g, k, D, BwHair, BwHairShade, 30, 0.5f, 0, -0.47f, 0.2f, -0.44f, 0.3f, -0.32f, 0.325f, -0.15f, 0.335f, 0.0f, 0.315f, 0.1f, 0.26f, 0.14f, 0.2f, 0.09f,
                          0, 0.05f, -0.2f, 0.09f, -0.26f, 0.14f, -0.315f, 0.1f, -0.335f, 0.0f, -0.325f, -0.15f, -0.3f, -0.32f, -0.2f, -0.44f);
                    ears = false;
                    break;
                case KindHawkSd:
                case KindStrangeSd:
                {
                    Color hair = k == KindHawkSd ? HeHair : DrHair;
                    using (GraphicsPath h = KCurve(D, 0.5f, -0.29f, -0.1f, -0.29f, -0.3f, -0.17f, -0.43f, 0, -0.46f, 0.17f, -0.43f, 0.29f, -0.3f, 0.29f, -0.1f, 0, -0.12f))
                        SlayerHair(g, k, h, D, hair, Darker(hair, 0.4));
                    break;
                }
                case KindPantherSd:
                    // The mask's two small ears.
                    foreach (int s in Sides) VPoly(g, k, D, BpBlack, BpBlackShade, 0, s * 0.1f, -0.32f, s * 0.2f, -0.455f, s * 0.245f, -0.26f);
                    ears = false;
                    break;
                case KindCarolSd:
                    VBlob(g, k, D, CmHair, CmHairShade, 30, 0.5f, 0, -0.47f, 0.2f, -0.44f, 0.3f, -0.32f, 0.325f, -0.15f, 0.335f, 0.02f, 0.32f, 0.12f, 0.27f, 0.16f, 0.21f, 0.1f,
                          0, 0.06f, -0.21f, 0.1f, -0.27f, 0.16f, -0.32f, 0.12f, -0.335f, 0.02f, -0.325f, -0.15f, -0.3f, -0.32f, -0.2f, -0.44f);
                    ears = false;
                    break;
                case KindWandaSd:
                    VBlob(g, k, D, SwHair, SwHairShade, 30, 0.5f, 0, -0.47f, 0.2f, -0.44f, 0.31f, -0.32f, 0.34f, -0.12f, 0.35f, 0.06f, 0.33f, 0.2f, 0.27f, 0.25f, 0.22f, 0.19f,
                          0.15f, 0.22f, 0, 0.12f, -0.15f, 0.22f, -0.22f, 0.19f, -0.27f, 0.25f, -0.33f, 0.2f, -0.35f, 0.06f, -0.34f, -0.12f, -0.31f, -0.32f, -0.2f, -0.44f);
                    ears = false;
                    break;
                case KindColossalSd:
                    // Steam rising off his shoulders and his head.
                    foreach (float[] s in new float[][] { new float[] { -0.24f, 0.02f, 0.13f }, new float[] { -0.29f, -0.07f, 0.1f }, new float[] { 0.25f, 0.0f, 0.14f },
                                                          new float[] { 0.3f, -0.09f, 0.1f }, new float[] { -0.12f, -0.4f, 0.13f }, new float[] { 0.1f, -0.43f, 0.11f } })
                        Glow(g, s[0] * D, s[1] * D, s[2] * D, s[2] * 0.85f * D, Color.White, 215);
                    foreach (int s in Sides) VOval(g, k, D, CtMuscle, CtMuscleShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(160, CtFiber), 0.006f, D, s * 0.25f, -0.07f, s * 0.27f, -0.03f, s * 0.255f, 0.0f);
                    return;
                case KindAttackSd: SpikyHair(g, k, D, AttackHair, AtHair, AtHairShade, Color.FromArgb(120, 96, 84), -0.25f); ears = false; break;
                case KindSmileTitanSd:
                    // Its blond hair down to its shoulders.
                    VBlob(g, k, D, TtHair, TtHairShade, 30, 0.5f, 0, -0.47f, 0.2f, -0.44f, 0.3f, -0.32f, 0.33f, -0.15f, 0.34f, 0.02f, 0.32f, 0.12f, 0.27f, 0.16f, 0.21f, 0.1f,
                          0, 0.06f, -0.21f, 0.1f, -0.27f, 0.16f, -0.32f, 0.12f, -0.34f, 0.02f, -0.33f, -0.15f, -0.3f, -0.32f, -0.2f, -0.44f);
                    ears = false;
                    break;
                case KindBoboTitanSd:
                    // Big ears sticking out.
                    foreach (int s in Sides)
                    {
                        VOval(g, k, D, TtSkin, TtSkinShade, 0, s * 0.27f, -0.045f, 0.1f, 0.13f);
                        TnLine(g, Color.FromArgb(150, 176, 110, 96), 0.008f, D, s * 0.262f, -0.08f, s * 0.285f, -0.045f, s * 0.265f, -0.01f);
                    }
                    return;
                case KindArmoredSd:
                    SpikyHair(g, k, D, ShortHairBack, ArHair, Darker(ArHair, 0.35), Color.FromArgb(255, 236, 180), -0.25f);
                    ears = false;
                    break;
            }
            if (!ears) return;
            Color skin = k == KindHulkSd ? HkSkin : NjSkin, shade = k == KindHulkSd ? HkSkinShade : NjSkinShade;
            foreach (int s in Sides)
            {
                VOval(g, k, D, skin, shade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, k == KindHulkSd ? Color.FromArgb(60, 100, 30) : Color.FromArgb(170, 104, 84)), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
        }

        static void AvLeg(Graphics g, int k, float D, int s, PointF f)
        {
            PointF hip = P(s * (k == KindHulkSd ? 0.056f : 0.046f), 0.235f);
            Color leg, boot;
            bool tall = true, bare = false;
            float w = 0.058f;
            switch (k)
            {
                case KindCapSd: leg = CaBlue; boot = CaRed; break;
                case KindThorSd: leg = ThArmor; boot = Color.FromArgb(96, 66, 44); break;
                case KindHulkSd: leg = HkSkin; boot = HkSkin; w = 0.068f; bare = true; break;
                case KindWidowSd: leg = BwSuit; boot = Color.FromArgb(26, 26, 32); break;
                case KindHawkSd: leg = Color.FromArgb(44, 44, 54); boot = Color.FromArgb(28, 28, 34); break;
                case KindPantherSd: leg = BpBlack; boot = BpBlack; break;
                case KindStrangeSd: leg = DrBlueShade; boot = Color.FromArgb(70, 48, 34); break;
                case KindCarolSd: leg = CmBlue; boot = CaRed; break;
                case KindWandaSd: leg = Color.FromArgb(36, 30, 36); boot = SwRedShade; break;
                case KindColossalSd: leg = CtMuscle; boot = CtMuscle; bare = true; break;
                case KindAttackSd: leg = AtSkin; boot = AtSkin; bare = true; break;
                case KindSmileTitanSd: leg = TtSkin; boot = TtSkin; bare = true; break;
                case KindBoboTitanSd: leg = TtSkin; boot = TtSkin; bare = true; w = 0.068f; break;
                default: leg = ArPlate; boot = ArPlate; break;
            }
            VLimb(g, k, D, leg, w, hip, tall && !bare ? P(f.X, f.Y - 0.03f) : f, 0);
            if (k == KindCarolSd) VLimb(g, k, D, CaRed, 0.016f, P(hip.X + s * 0.02f, hip.Y + 0.01f), P(f.X + s * 0.02f, f.Y - 0.04f), 0);
            if (k == KindColossalSd) TnLine(g, Color.FromArgb(150, CtFiber), 0.006f, D, hip.X + s * 0.012f, hip.Y + 0.01f, f.X + s * 0.01f, f.Y - 0.02f);
            if (k == KindArmoredSd) VOval(g, k, D, ArPlate, ArPlateShade, 0, f.X, f.Y - 0.05f, 0.07f, 0.05f);   // a knee plate
            if (bare)
            {
                Color skin = k == KindHulkSd ? HkSkin : k == KindColossalSd ? CtMuscle : k == KindArmoredSd ? ArPlate : k == KindSmileTitanSd || k == KindBoboTitanSd ? TtSkin : AtSkin;
                VOval(g, k, D, skin, Color.Empty, 0, f.X + s * 0.01f, f.Y + 0.02f, k == KindHulkSd ? 0.094f : 0.08f, 0.046f);
                return;
            }
            VRound(g, k, D, boot, Darker(boot, 0.3), 0, f.X - 0.034f, f.Y - 0.052f, 0.068f, 0.07f, 0.02f);
            VOval(g, k, D, boot, Color.Empty, 0, f.X + s * 0.008f, f.Y + 0.022f, 0.086f, 0.044f);
        }

        static void AvClothes(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindCapSd:
                {
                    // The blue suit, the white star on his chest, the red and white stripes under it, the brown belt.
                    using (GraphicsPath t = Theme.Round(new RectangleF(-0.106f * D, 0.07f * D, 0.212f * D, 0.18f * D), 0.045f * D))
                    {
                        VFill(g, k, t, CaBlue, CaBlueShade, D, 40);
                        Region old = g.Clip;
                        g.SetClip(t, CombineMode.Intersect);
                        for (int i = 0; i < 6; i++)
                            using (SolidBrush b = new SolidBrush(i % 2 == 0 ? CaRed : CaWhite)) g.FillRectangle(b, (-0.075f + i * 0.025f) * D, 0.158f * D, 0.025f * D, 0.045f * D);
                        g.Clip = old;
                        old.Dispose();
                    }
                    using (GraphicsPath s = Star(D, 0, 0.113f, 0.042f, 0.018f)) VFill(g, k, s, CaWhite, CaWhiteShade, D, 0);
                    VRound(g, k, D, CaBrown, Color.Empty, 0, -0.108f, 0.2f, 0.216f, 0.026f, 0.01f);
                    VRound(g, k, D, Color.FromArgb(214, 218, 228), Color.Empty, 0, -0.018f, 0.2f, 0.036f, 0.026f, 0.006f);
                    break;
                }
                case KindThorSd:
                    // The dark armour with the six silver discs, the belt.
                    VRound(g, k, D, ThArmor, ThArmorShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    foreach (int s in Sides)
                        for (int i = 0; i < 3; i++)
                        {
                            VOval(g, k, D, ThSilver, ThSilverShade, 0, s * 0.052f, 0.102f + i * 0.042f, 0.036f, 0.036f);
                            Ellipse(g, Color.FromArgb(120, 90, 96, 110), (s * 0.052f - 0.008f) * D, (0.094f + i * 0.042f) * D, 0.016f * D, 0.016f * D);
                        }
                    VRound(g, k, D, Color.FromArgb(52, 40, 34), Color.Empty, 0, -0.108f, 0.205f, 0.216f, 0.024f, 0.01f);
                    break;
                case KindHulkSd:
                    // His broad bare chest, and the torn purple shorts.
                    VRound(g, k, D, HkSkin, HkSkinShade, 40, -0.128f, 0.065f, 0.256f, 0.17f, 0.05f);
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(150, 54, 96, 28), 0.008f, D, s * 0.01f, 0.1f, s * 0.05f, 0.135f, s * 0.1f, 0.12f);
                        TnLine(g, Color.FromArgb(110, 54, 96, 28), 0.007f, D, s * 0.03f, 0.16f, s * 0.035f, 0.185f);
                    }
                    TnLine(g, Color.FromArgb(110, 54, 96, 28), 0.007f, D, 0, 0.145f, 0, 0.19f);
                    VPoly(g, k, D, HkPurple, HkPurpleShade, 30, -0.135f, 0.19f, 0.135f, 0.19f, 0.145f, 0.272f, 0.105f, 0.292f, 0.075f, 0.266f, 0.035f, 0.296f, 0, 0.272f,
                          -0.035f, 0.296f, -0.075f, 0.266f, -0.105f, 0.292f, -0.145f, 0.272f);
                    break;
                case KindWidowSd:
                    // The black suit with its zip, the grey belt and the red hourglass on its buckle.
                    VRound(g, k, D, BwSuit, BwSuitShade, 40, -0.1f, 0.07f, 0.2f, 0.18f, 0.045f);
                    TnLine(g, Color.FromArgb(170, 170, 176, 190), 0.006f, D, 0, 0.08f, 0, 0.19f);
                    VRound(g, k, D, Color.FromArgb(104, 108, 122), Color.Empty, 0, -0.102f, 0.192f, 0.204f, 0.024f, 0.01f);
                    VOval(g, k, D, Color.FromArgb(30, 30, 36), Color.Empty, 0, 0, 0.204f, 0.04f, 0.036f);
                    using (GraphicsPath h = KPoly(D, -0.012f, 0.192f, 0.012f, 0.192f, -0.012f, 0.216f, 0.012f, 0.216f)) FillSolid(g, h, Color.FromArgb(226, 40, 46));
                    break;
                case KindHawkSd:
                    // The black vest with the purple V, the strap of his quiver across it.
                    VRound(g, k, D, Color.FromArgb(44, 44, 54), Color.FromArgb(24, 24, 30), 40, -0.1f, 0.07f, 0.2f, 0.18f, 0.045f);
                    VPoly(g, k, D, HePurple, HePurpleShade, 0, -0.085f, 0.075f, -0.05f, 0.075f, 0, 0.13f, 0.05f, 0.075f, 0.085f, 0.075f, 0, 0.165f);
                    TnLine(g, Color.FromArgb(118, 82, 50), 0.016f, D, -0.09f, 0.21f, 0.0f, 0.13f, 0.095f, 0.075f);
                    VRound(g, k, D, Color.FromArgb(30, 30, 36), Color.Empty, 0, -0.102f, 0.2f, 0.204f, 0.024f, 0.01f);
                    break;
                case KindPantherSd:
                {
                    // The black suit and the silver necklace of claws across his chest, faint silver lines on the suit.
                    VRound(g, k, D, BpBlack, BpBlackShade, 50, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    for (int i = -4; i <= 4; i++)
                    {
                        float t = i / 4f, x = t * 0.088f, y = 0.078f + (1 - t * t) * 0.045f;
                        using (GraphicsPath c = KPoly(D, x - 0.01f, y - 0.004f, x + 0.01f, y - 0.004f, x, y + 0.022f)) FillSolid(g, c, BpSilver);
                    }
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(90, BpSilver), 0.005f, D, s * 0.03f, 0.15f, s * 0.07f, 0.17f, s * 0.08f, 0.23f);
                    TnLine(g, Color.FromArgb(110, 150, 110, 230), 0.006f, D, -0.06f, 0.2f, 0, 0.215f, 0.06f, 0.2f);
                    break;
                }
                case KindStrangeSd:
                    // The blue tunic wrapped over, the tan sash across it, the Eye of Agamotto at his throat.
                    VRound(g, k, D, DrBlue, DrBlueShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    TnLine(g, Color.FromArgb(150, 16, 30, 60), 0.007f, D, -0.06f, 0.075f, 0.03f, 0.2f);
                    VPoly(g, k, D, DrTan, Darker(DrTan, 0.3), 0, -0.106f, 0.18f, 0.106f, 0.17f, 0.106f, 0.205f, -0.106f, 0.215f);
                    VOval(g, k, D, DrGold, Darker(DrGold, 0.3), 0, 0, 0.098f, 0.05f, 0.05f);
                    VOval(g, k, D, Color.FromArgb(110, 230, 140), Color.FromArgb(40, 150, 70), 0, 0, 0.098f, 0.024f, 0.016f);
                    break;
                case KindCarolSd:
                {
                    // The blue middle of her suit, red at the sides, the gold star on her chest, the gold collar.
                    using (GraphicsPath t = Theme.Round(new RectangleF(-0.1f * D, 0.07f * D, 0.2f * D, 0.18f * D), 0.045f * D))
                    {
                        VFill(g, k, t, CmBlue, CmBlueShade, D, 40);
                        Region old = g.Clip;
                        g.SetClip(t, CombineMode.Intersect);
                        foreach (int s in Sides)
                            using (GraphicsPath r = KPoly(D, s * 0.055f, 0.06f, s * 0.12f, 0.06f, s * 0.12f, 0.26f, s * 0.045f, 0.26f))
                            {
                                Stroke(g, r, TnInk(k), TnW(D) * 1.6f);
                                VPaint(g, r, CaRed, CaRedShade, D, 30);
                            }
                        g.Clip = old;
                        old.Dispose();
                    }
                    PointF[] st = new PointF[16];
                    for (int i = 0; i < 16; i++)
                    {
                        double a = -Math.PI / 2 + Math.PI * i / 8;
                        float r = i % 2 == 0 ? 0.04f : 0.017f;
                        st[i] = P((float)Math.Cos(a) * r * D, (0.125f + (float)Math.Sin(a) * r) * D);
                    }
                    using (GraphicsPath s8 = new GraphicsPath())
                    {
                        s8.AddPolygon(st);
                        VFill(g, k, s8, Color.FromArgb(250, 210, 80), Color.FromArgb(214, 160, 30), D, 0);
                    }
                    TnLine(g, Color.FromArgb(240, 200, 70), 0.012f, D, -0.06f, 0.076f, 0, 0.09f, 0.06f, 0.076f);
                    break;
                }
                case KindWandaSd:
                    // The red coat open over a darker bodice, its tails flaring to her knees.
                    VRound(g, k, D, Color.FromArgb(70, 18, 30), Color.FromArgb(40, 8, 16), 30, -0.1f, 0.07f, 0.2f, 0.17f, 0.045f);
                    TnLine(g, Color.FromArgb(150, 200, 60, 70), 0.006f, D, -0.03f, 0.1f, 0.03f, 0.13f, -0.03f, 0.16f, 0.03f, 0.19f);
                    foreach (int s in Sides)
                        VPoly(g, k, D, SwRed, SwRedShade, 30, s * 0.035f, 0.07f, s * 0.118f, 0.072f, s * 0.15f, 0.2f, s * 0.168f, 0.31f, s * 0.07f, 0.29f, s * 0.05f, 0.16f);
                    break;
                case KindColossalSd:
                    // No skin: red muscle, the ribs and the bands of muscle showing.
                    VRound(g, k, D, CtMuscle, CtMuscleShade, 30, -0.112f, 0.07f, 0.224f, 0.2f, 0.05f);
                    foreach (int s in Sides)
                        for (int i = 0; i < 3; i++) TnLine(g, Color.FromArgb(170, CtFiber), 0.007f, D, s * 0.02f, 0.105f + i * 0.035f, s * 0.06f, 0.095f + i * 0.035f, s * 0.1f, 0.11f + i * 0.035f);
                    TnLine(g, Color.FromArgb(170, CtFiber), 0.008f, D, 0, 0.09f, 0, 0.25f);
                    break;
                case KindAttackSd:
                    // A lean bare body, the muscles drawn in.
                    VRound(g, k, D, AtSkin, AtSkinShade, 30, -0.106f, 0.07f, 0.212f, 0.2f, 0.045f);
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(140, 170, 110, 90), 0.007f, D, s * 0.01f, 0.1f, s * 0.05f, 0.13f, s * 0.09f, 0.115f);
                        TnLine(g, Color.FromArgb(110, 170, 110, 90), 0.006f, D, s * 0.03f, 0.16f, s * 0.032f, 0.2f);
                    }
                    TnLine(g, Color.FromArgb(110, 170, 110, 90), 0.006f, D, 0, 0.14f, 0, 0.22f);
                    break;
                case KindSmileTitanSd:
                    // A plain bare body, its navel.
                    VRound(g, k, D, TtSkin, TtSkinShade, 30, -0.104f, 0.07f, 0.208f, 0.2f, 0.045f);
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(110, 176, 120, 100), 0.006f, D, s * 0.012f, 0.1f, s * 0.05f, 0.125f, s * 0.085f, 0.112f);
                    Ellipse(g, Color.FromArgb(150, 170, 110, 96), -0.008f * D, 0.19f * D, 0.016f * D, 0.012f * D);
                    break;
                case KindBoboTitanSd:
                    // A round pot belly with its navel.
                    VRound(g, k, D, TtSkin, TtSkinShade, 30, -0.11f, 0.07f, 0.22f, 0.2f, 0.05f);
                    VOval(g, k, D, TtSkin, TtSkinShade, 0, 0, 0.18f, 0.25f, 0.2f);
                    Ellipse(g, Color.FromArgb(150, 170, 110, 96), -0.009f * D, 0.2f * D, 0.018f * D, 0.014f * D);
                    break;
                default:
                    // The Armored Titan: white plates over his chest and belly, the red muscle showing in their seams.
                    VRound(g, k, D, ArMuscle, Darker(ArMuscle, 0.3), 0, -0.11f, 0.07f, 0.22f, 0.2f, 0.045f);
                    foreach (int s in Sides) VRound(g, k, D, ArPlate, ArPlateShade, 40, s > 0 ? 0.006f : -0.102f, 0.078f, 0.096f, 0.07f, 0.024f);
                    for (int i = 0; i < 2; i++)
                        foreach (int s in Sides) VRound(g, k, D, ArPlate, ArPlateShade, 30, s > 0 ? 0.008f : -0.07f, 0.155f + i * 0.048f, 0.062f, 0.042f, 0.014f);
                    break;
            }
        }

        static void AvArm(Graphics g, float D, int k, int side, PointF h)
        {
            PointF a = P(side * (k == KindHulkSd ? 0.122f : 0.1f), k == KindHulkSd ? 0.108f : 0.115f);
            switch (k)
            {
                case KindCapSd:
                    VLimb(g, k, D, CaBlue, 0.056f, a, h, side * 0.03f);
                    VLimb(g, k, D, CaRed, 0.062f, Lerp(a, h, 0.74f), Lerp(a, h, 0.92f), 0);
                    return;
                case KindThorSd:
                    VLimb(g, k, D, NjSkin, 0.048f, a, h, side * 0.03f);
                    VLimb(g, k, D, ThArmor, 0.058f, a, Lerp(a, h, 0.62f), side * 0.03f);
                    VLimb(g, k, D, ThSilver, 0.06f, Lerp(a, h, 0.64f), Lerp(a, h, 0.84f), 0);
                    return;
                case KindHulkSd: VLimb(g, k, D, HkSkin, 0.074f, a, h, side * 0.035f); return;
                case KindWidowSd:
                    VLimb(g, k, D, BwSuit, 0.052f, a, h, side * 0.03f);
                    VLimb(g, k, D, Color.FromArgb(150, 156, 172), 0.058f, Lerp(a, h, 0.76f), Lerp(a, h, 0.88f), 0);
                    return;
                case KindHawkSd:
                    VLimb(g, k, D, NjSkin, 0.05f, a, h, side * 0.03f);
                    if (side < 0) VLimb(g, k, D, Color.FromArgb(40, 40, 50), 0.058f, Lerp(a, h, 0.5f), Lerp(a, h, 0.86f), 0);   // his arm guard
                    return;
                case KindPantherSd: VLimb(g, k, D, BpBlack, 0.054f, a, h, side * 0.03f); return;
                case KindStrangeSd:
                    VLimb(g, k, D, DrBlue, 0.056f, a, h, side * 0.03f);
                    VLimb(g, k, D, DrTan, 0.058f, Lerp(a, h, 0.62f), Lerp(a, h, 0.86f), 0);
                    return;
                case KindCarolSd: VLimb(g, k, D, CaRed, 0.054f, a, h, side * 0.03f); return;
                case KindWandaSd:
                    VLimb(g, k, D, NjSkin, 0.046f, a, h, side * 0.03f);
                    VLimb(g, k, D, SwRed, 0.058f, a, Lerp(a, h, 0.82f), side * 0.03f);
                    return;
                case KindColossalSd:
                    VLimb(g, k, D, CtMuscle, 0.056f, a, h, side * 0.03f);
                    TnLine(g, Color.FromArgb(150, CtFiber), 0.006f, D, Lerp(a, h, 0.15f).X, Lerp(a, h, 0.15f).Y, Lerp(a, h, 0.8f).X, Lerp(a, h, 0.8f).Y);
                    return;
                case KindAttackSd: VLimb(g, k, D, AtSkin, 0.054f, a, h, side * 0.03f); return;
                case KindSmileTitanSd: VLimb(g, k, D, TtSkin, 0.052f, a, h, side * 0.03f); return;
                case KindBoboTitanSd: VLimb(g, k, D, TtSkin, 0.064f, a, h, side * 0.03f); return;
                default:
                    VLimb(g, k, D, ArMuscle, 0.054f, a, h, side * 0.03f);
                    VLimb(g, k, D, ArPlate, 0.06f, a, Lerp(a, h, 0.45f), side * 0.03f);
                    VLimb(g, k, D, ArPlate, 0.058f, Lerp(a, h, 0.55f), Lerp(a, h, 0.85f), 0);
                    return;
            }
        }

        static void AvHand(Graphics g, float D, int k)
        {
            Color c;
            float w = 0.058f, h = 0.054f;
            switch (k)
            {
                case KindCapSd:
                case KindCarolSd: c = CaRed; break;
                case KindHulkSd: c = HkSkin; w = 0.08f; h = 0.072f; break;
                case KindPantherSd: c = BpBlack; break;
                case KindColossalSd: c = CtMuscle; break;
                case KindAttackSd: c = AtSkin; break;
                case KindSmileTitanSd: c = TtSkin; break;
                case KindBoboTitanSd: c = TtSkin; w = 0.066f; h = 0.06f; break;
                case KindArmoredSd: c = ArPlate; break;
                default: c = NjSkin; break;
            }
            // Captain Marvel's hands glow with her power; the Scarlet Witch's with her red magic.
            if (k == KindCarolSd) Glow(g, 0, 0, 0.15f * D, 0.15f * D, Color.FromArgb(255, 214, 110), 150);
            if (k == KindWandaSd) Glow(g, 0, 0, 0.16f * D, 0.16f * D, Color.FromArgb(240, 40, 90), 150);
            VOval(g, k, D, c, Color.Empty, 0, 0, 0, w, h);
        }

        // What they hold: the shield on Cap's arm, Mjolnir in Thor's hand, Hawkeye's bow, the spell turning at Doctor
        // Strange's hand.
        static void AvProps(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            switch (k)
            {
                case KindCapSd:
                {
                    PointF h = DsHand(p, geo, -1);
                    GraphicsState st = g.Save();
                    g.TranslateTransform((h.X - 0.025f) * D, (h.Y - 0.015f) * D);
                    DsCached(g, D, TnId(k, 7), new RectangleF(-0.13f, -0.13f, 0.26f, 0.26f), bg =>
                    {
                        VOval(bg, k, D, CaRed, CaRedShade, 0, 0, 0, 0.21f, 0.21f);
                        Ellipse(bg, CaWhite, -0.081f * D, -0.081f * D, 0.162f * D, 0.162f * D);
                        Ellipse(bg, CaRed, -0.06f * D, -0.06f * D, 0.12f * D, 0.12f * D);
                        Ellipse(bg, CaBlue, -0.038f * D, -0.038f * D, 0.076f * D, 0.076f * D);
                        using (GraphicsPath s = Star(D, 0, 0.002f, 0.032f, 0.013f)) FillSolid(bg, s, CaWhite);
                        Glow(bg, -0.035f * D, -0.045f * D, 0.09f * D, 0.06f * D, Color.White, 90);
                    });
                    g.Restore(st);
                    break;
                }
                case KindThorSd:
                {
                    PointF h = DsHand(p, geo, 1);
                    GraphicsState st = g.Save();
                    g.TranslateTransform(h.X * D, h.Y * D);
                    // Mjolnir hanging from his hand by its short handle, the head down, a little lightning about it.
                    DsCached(g, D, TnId(k, 7), new RectangleF(-0.1f, -0.06f, 0.24f, 0.24f), bg =>
                    {
                        VLimb(bg, k, D, Color.FromArgb(120, 78, 46), 0.024f, P(0.0f, -0.03f), P(0.012f, 0.085f), 0);
                        TnLine(bg, Color.FromArgb(150, 70, 44, 26), 0.006f, D, -0.002f, -0.01f, 0.006f, 0.02f, 0.002f, 0.05f, 0.009f, 0.075f);
                        VRound(bg, k, D, Color.FromArgb(172, 176, 190), Color.FromArgb(112, 116, 132), 40, -0.043f, 0.075f, 0.11f, 0.072f, 0.014f);
                        TnLine(bg, Color.FromArgb(150, 90, 94, 110), 0.006f, D, -0.03f, 0.111f, 0.055f, 0.111f);
                        TnLine(bg, Color.FromArgb(160, 130, 210, 255), 0.007f, D, 0.075f, 0.06f, 0.095f, 0.09f, 0.08f, 0.1f, 0.105f, 0.135f);
                        Glow(bg, 0.09f * D, 0.1f * D, 0.08f * D, 0.1f * D, Color.FromArgb(150, 210, 255), 110);
                    });
                    g.Restore(st);
                    break;
                }
                case KindHawkSd:
                {
                    // The bow upright in his near hand, bowed out away from him, the string on his side.
                    PointF h = DsHand(p, geo, -1);
                    PointF top = P(h.X + 0.006f, h.Y - 0.13f), bot = P(h.X + 0.006f, h.Y + 0.12f);
                    TnLine(g, Color.FromArgb(200, 210, 210, 220), 0.004f, D, top.X, top.Y, bot.X, bot.Y);
                    using (Pen o = new Pen(TnInk(k), Math.Max(1f, D * 0.026f)))
                    using (Pen b = new Pen(HePurple, Math.Max(1f, D * 0.014f)))
                    {
                        o.StartCap = o.EndCap = b.StartCap = b.EndCap = LineCap.Round;
                        PointF c1 = P((h.X - 0.075f) * D, (h.Y - 0.1f) * D), c2 = P((h.X - 0.075f) * D, (h.Y + 0.09f) * D);
                        g.DrawBezier(o, P(top.X * D, top.Y * D), c1, c2, P(bot.X * D, bot.Y * D));
                        g.DrawBezier(b, P(top.X * D, top.Y * D), c1, c2, P(bot.X * D, bot.Y * D));
                    }
                    break;
                }
                case KindStrangeSd:
                {
                    // An orange spell circle turning round his near hand.
                    PointF h = DsHand(p, geo, 1);
                    float cx = h.X * D, cy = h.Y * D, r = 0.07f * D;
                    Glow(g, cx, cy, r * 3.2f, r * 3.2f, Color.FromArgb(255, 150, 40), 90);
                    using (Pen ring = new Pen(Color.FromArgb(230, 255, 170, 60), Math.Max(1f, D * 0.007f)))
                    using (Pen fine = new Pen(Color.FromArgb(200, 255, 200, 110), Math.Max(0.8f, D * 0.004f)))
                    {
                        g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
                        g.DrawEllipse(fine, cx - r * 0.72f, cy - r * 0.72f, r * 1.44f, r * 1.44f);
                        double turn = p.Now / 900.0;
                        for (int i = 0; i < 12; i++)
                        {
                            double a = turn + Math.PI * 2 * i / 12;
                            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                            g.DrawLine(fine, cx + ca * r * 0.74f, cy + sa * r * 0.74f, cx + ca * r * 0.96f, cy + sa * r * 0.96f);
                        }
                        PointF[] sq = new PointF[4];
                        for (int i = 0; i < 4; i++)
                        {
                            double a = -turn * 1.3 + Math.PI / 2 * i;
                            sq[i] = new PointF(cx + (float)Math.Cos(a) * r * 0.7f, cy + (float)Math.Sin(a) * r * 0.7f);
                        }
                        g.DrawPolygon(fine, sq);
                    }
                    break;
                }
            }
        }

        // Brows and marks (f: the eyes' height).
        static void AvDetails(Graphics g, int k, float D, float f)
        {
            switch (k)
            {
                case KindThorSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 170, 118, 44), 0.014f, D, s * 0.05f, f - 0.088f, s * 0.11f, f - 0.105f, s * 0.165f, f - 0.098f);
                    break;
                case KindHulkSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(250, 30, 50, 20), 0.026f, D, s * 0.035f, f - 0.05f, s * 0.1f, f - 0.085f, s * 0.175f, f - 0.1f);
                    break;
                case KindWidowSd:
                case KindCarolSd:
                case KindWandaSd:
                {
                    Color b = k == KindWidowSd ? Color.FromArgb(230, 150, 50, 30) : k == KindCarolSd ? Color.FromArgb(230, 176, 128, 60) : Color.FromArgb(230, 110, 40, 30);
                    foreach (int s in Sides) TnLine(g, b, 0.01f, D, s * 0.06f, f - 0.098f, s * 0.115f, f - 0.112f, s * 0.165f, f - 0.1f);
                    break;
                }
                case KindHawkSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 110, 80, 46), 0.013f, D, s * 0.045f, f - 0.075f, s * 0.11f, f - 0.092f, s * 0.165f, f - 0.088f);
                    break;
                case KindStrangeSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 40, 30, 28), 0.013f, D, s * 0.05f, f - 0.09f, s * 0.11f, f - 0.115f, s * 0.168f, f - 0.1f);
                    break;
                case KindAttackSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(250, 50, 30, 24), 0.02f, D, s * 0.04f, f - 0.06f, s * 0.1f, f - 0.09f, s * 0.17f, f - 0.1f);
                    break;
            }
        }

        static string[] AvLines(int k)
        {
            if (!I18n.IsSpanish) return AvLinesEn(k);
            switch (k)
            {
                case KindCapSd: return new string[] { "\u00A1Vengadores, reun\u00EDos!", "Puedo seguir as\u00ED todo el d\u00EDa.", "No me gustan los abusones.", "\u00A1Ese lenguaje!" };
                case KindThorSd: return new string[] { "\u00A1Por Asgard!", "Soy el dios del trueno.", "\u00A1Otra!", "\u00A1Traedme a Thanos!" };
                case KindHulkSd: return new string[] { "\u00A1Hulk aplasta!", "\u00A1GRRRR!", "Mi secreto: siempre estoy enfadado.", "Hulk tiene hambre." };
                case KindWidowSd: return new string[] { "Tengo un historial en rojo y quiero borrarlo.", "No soy de las que se rinden.", "Viuda Negra, en posici\u00F3n.", "\u00BFMe necesitabas? Aqu\u00ED estoy." };
                case KindHawkSd: return new string[] { "Nunca fallo.", "Tengo una flecha para eso.", "La ciudad vuela y yo con un arco.", "Ojo de Halc\u00F3n, en posici\u00F3n." };
                case KindPantherSd: return new string[] { "\u00A1Wakanda por siempre!", "Soy el rey de Wakanda.", "En tiempos de crisis, el sabio tiende puentes.", "No es la primera vez que me dan por muerto." };
                case KindStrangeSd: return new string[] { "Dormammu, he venido a negociar.", "Uno entre catorce millones.", "El tiempo es relativo.", "Hechicero Supremo, a tu servicio." };
                case KindCarolSd: return new string[] { "\u00A1M\u00E1s alto, m\u00E1s lejos, m\u00E1s r\u00E1pido!", "No tengo nada que demostrarte.", "Soy Carol Danvers.", "\u00BFNecesitas ayuda? Ya llegu\u00E9." };
                case KindWandaSd: return new string[] { "Esto es lo que yo quiero.", "No puedes controlarme.", "Soy la Bruja Escarlata.", "Magia del caos." };
                case KindColossalSd: return new string[] { "\u2026", "\u00A1GROOOAAAR!", "Fsssshhh\u2026", "El Muro ya no os protege." };
                case KindSmileTitanSd: return new string[] { "Hola\u2026 jijiji.", "\u00BFTienes hambre? Yo s\u00ED.", "Qu\u00E9 buen d\u00EDa para pasear.", "*sonr\u00EDe sin parar*" };
                case KindBoboTitanSd: return new string[] { "\u00BFEh?", "Uaaah\u2026", "\u00BFHas visto a Eren?", "Mmm\u2026 \u00BFqu\u00E9 estaba haciendo?" };
                case KindAttackSd: return new string[] { "\u00A1RAAAAAH!", "\u00A1Os exterminar\u00E9!", "Sigue avanzando.", "Luchar\u00E9." };
                default: return new string[] { "\u2026", "\u00A1Embestida!", "Soy un guerrero.", "No hay vuelta atr\u00E1s." };
            }
        }

        static string[] AvLinesEn(int k)
        {
            switch (k)
            {
                case KindCapSd: return new string[] { "Avengers, assemble!", "I can do this all day.", "I don't like bullies.", "Language!" };
                case KindThorSd: return new string[] { "For Asgard!", "I am the god of thunder.", "Another!", "Bring me Thanos!" };
                case KindHulkSd: return new string[] { "Hulk smash!", "GRRRR!", "That's my secret: I'm always angry.", "Hulk is hungry." };
                case KindWidowSd: return new string[] { "I've got red in my ledger, and I'd like to wipe it out.", "I'm not one to give up.", "Black Widow, in position.", "You needed me? Here I am." };
                case KindHawkSd: return new string[] { "I never miss.", "I've got an arrow for that.", "The city is flying and I've got a bow.", "Hawkeye, in position." };
                case KindPantherSd: return new string[] { "Wakanda forever!", "I am the king of Wakanda.", "In times of crisis, the wise build bridges.", "It's not the first time I've been left for dead." };
                case KindStrangeSd: return new string[] { "Dormammu, I've come to bargain.", "One in fourteen million.", "Time is relative.", "Sorcerer Supreme, at your service." };
                case KindCarolSd: return new string[] { "Higher, further, faster!", "I have nothing to prove to you.", "I'm Carol Danvers.", "Need a hand? I'm here." };
                case KindWandaSd: return new string[] { "This is what I want.", "You can't control me.", "I'm the Scarlet Witch.", "Chaos magic." };
                case KindColossalSd: return new string[] { "\u2026", "GROOOAAAR!", "Fsssshhh\u2026", "The Wall won't protect you anymore." };
                case KindSmileTitanSd: return new string[] { "Hello\u2026 hehehe.", "Hungry? I am.", "What a nice day for a walk.", "*smiles nonstop*" };
                case KindBoboTitanSd: return new string[] { "Huh?", "Uaaah\u2026", "Have you seen Eren?", "Hmm\u2026 what was I doing?" };
                case KindAttackSd: return new string[] { "RAAAAAH!", "I'll wipe you all out!", "Keep moving forward.", "I'll fight." };
                default: return new string[] { "\u2026", "Charge!", "I am a warrior.", "There's no turning back." };
            }
        }

        // ---- The titans' faces.

        // The Colossal Titan: no skin, the red muscle running in bands, deep dark sockets under a heavy brow, the nose a
        // hole, and the lipless grin: two rows of teeth across his face.
        static void ColossalFace(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, CtMuscle, CtMuscleShade, D, 30);
            Region old = g.Clip;
            g.SetClip(face, CombineMode.Intersect);
            using (Pen fiber = new Pen(Color.FromArgb(130, CtFiber), Math.Max(0.8f, D * 0.006f)))
            {
                fiber.StartCap = fiber.EndCap = LineCap.Round;
                // Bands fanning up over the skull from the brow, the temples' fans, and the cheeks' muscle running down to
                // the jaw.
                for (int i = -3; i <= 3; i++)
                    g.DrawBezier(fiber, P(i * 0.028f * D, -0.17f * D), P(i * 0.034f * D, -0.24f * D), P(i * 0.052f * D, -0.31f * D), P(i * 0.075f * D, -0.37f * D));
                foreach (int s in Sides)
                {
                    for (int i = 0; i < 3; i++)
                        g.DrawBezier(fiber, P(s * (0.19f + i * 0.02f) * D, -0.33f * D), P(s * (0.23f + i * 0.015f) * D, -0.26f * D), P(s * (0.245f + i * 0.01f) * D, -0.18f * D), P(s * (0.22f + i * 0.012f) * D, -0.11f * D));
                    for (int i = 0; i < 4; i++)
                        g.DrawBezier(fiber, P(s * (0.205f + i * 0.012f) * D, -0.02f * D), P(s * (0.2f + i * 0.01f) * D, 0.02f * D), P(s * (0.185f + i * 0.008f) * D, 0.06f * D), P(s * (0.16f + i * 0.01f) * D, 0.095f * D));
                }
            }
            foreach (int s in Sides)
            {
                VBlob(g, k, D, Lighter(CtMuscle, 0.08), CtMuscleShade, 0, 0.45f, s * 0.04f, -0.12f, s * 0.11f, -0.15f, s * 0.19f, -0.13f, s * 0.21f, -0.09f, s * 0.12f, -0.105f, s * 0.05f, -0.09f);
                Ellipse(g, Color.FromArgb(60, 14, 12), (s * 0.112f - 0.06f) * D, (-0.09f) * D, 0.12f * D, 0.1f * D);
            }
            using (GraphicsPath n = KPoly(D, -0.018f, -0.03f, 0.018f, -0.03f, 0.008f, 0.0f, -0.008f, 0.0f)) FillSolid(g, n, Color.FromArgb(80, 16, 14));
            g.Clip = old;
            old.Dispose();
            AttackTeeth(g, k, D, 0.17f, 0.05f, 0.044f);
        }

        // A lipless jaw: the gums and two rows of teeth closed together, w wide, at height y, each row h tall.
        static void AttackTeeth(Graphics g, int k, float D, float w, float y, float h)
        {
            RectangleF r = new RectangleF(-w * D, (y - h) * D, w * 2 * D, h * 2 * D);
            using (GraphicsPath m = Theme.Round(r, h * 0.5f * D))
            {
                Stroke(g, m, TnInk(k), TnW(D) * 2);
                FillSolid(g, m, Color.FromArgb(150, 40, 40));
                Region old = g.Clip;
                g.SetClip(m, CombineMode.Intersect);
                using (SolidBrush tooth = new SolidBrush(Color.FromArgb(250, 246, 236)))
                {
                    int n = Math.Max(4, (int)Math.Round(w * 2 / 0.034f));
                    float tw = w * 2 * D / n;
                    for (int i = 0; i < n; i++)
                    {
                        float x = r.X + i * tw;
                        g.FillRectangle(tooth, x + tw * 0.08f, y * D - h * 0.82f * D, tw * 0.84f, h * 0.8f * D);
                        g.FillRectangle(tooth, x + tw * 0.08f, y * D + h * 0.04f * D, tw * 0.84f, h * 0.76f * D);
                    }
                }
                using (Pen gap = new Pen(Color.FromArgb(120, 90, 70, 60), Math.Max(0.6f, D * 0.004f))) g.DrawLine(gap, r.X, y * D, r.Right, y * D);
                g.Clip = old;
                old.Dispose();
            }
        }

        // The Smiling Titan: pale skin, a fringe of blond hair, and the grin stretched across its whole face, its lips
        // closed, curling up at the corners.
        static void SmileTitanFace(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, TtSkin, TtSkinShade, D, 0);
            using (Pen lip = new Pen(Color.FromArgb(150, 196, 120, 112), Math.Max(1f, D * 0.02f)))
            using (Pen line = new Pen(TnInk(k), Math.Max(1f, D * 0.011f)))
            {
                lip.StartCap = lip.EndCap = line.StartCap = line.EndCap = LineCap.Round;
                g.DrawBezier(lip, -0.19f * D, 0.012f * D, -0.09f * D, 0.098f * D, 0.09f * D, 0.098f * D, 0.19f * D, 0.012f * D);
                g.DrawBezier(line, -0.21f * D, -0.004f * D, -0.1f * D, 0.08f * D, 0.1f * D, 0.08f * D, 0.21f * D, -0.004f * D);
                foreach (int s in Sides) g.DrawArc(line, (s > 0 ? 0.2f : -0.232f) * D, -0.028f * D, 0.032f * D, 0.036f * D, s > 0 ? 90 : -10, 100);
            }
            foreach (int s in Sides) Glow(g, s * 0.17f * D, 0.02f * D, 0.08f * D, 0.05f * D, Color.FromArgb(240, 130, 120), 70);
            using (GraphicsPath h = KCurve(D, 0.42f, 0, -0.47f, 0.2f, -0.44f, 0.295f, -0.32f, 0.305f, -0.14f, 0.29f, 0.04f, 0.255f, 0.085f, 0.235f, -0.04f, 0.225f, -0.16f,
                                           0.17f, -0.25f, 0.1f, -0.29f, 0.0f, -0.26f, -0.1f, -0.22f, -0.18f, -0.16f, -0.22f, -0.08f, -0.238f, 0.04f, -0.262f, 0.09f,
                                           -0.295f, 0.03f, -0.305f, -0.14f, -0.295f, -0.32f, -0.2f, -0.44f))
                SlayerHair(g, k, h, D, TtHair, TtHairShade);
        }

        // The bald, dopey titan: a few hairs on top, a big round nose, and a wide open grin showing its big square teeth.
        static void BoboTitanFace(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, TtSkin, TtSkinShade, D, 30);
            foreach (float x in new float[] { -0.04f, 0.0f, 0.045f }) TnLine(g, Color.FromArgb(200, 96, 74, 54), 0.006f, D, x, -0.355f, x + 0.012f, -0.4f, x + 0.03f, -0.41f);
            using (GraphicsPath m = new GraphicsPath())
            {
                m.AddBezier(-0.14f * D, 0.02f * D, -0.07f * D, 0.035f * D, 0.07f * D, 0.035f * D, 0.14f * D, 0.02f * D);
                m.AddBezier(0.14f * D, 0.02f * D, 0.11f * D, 0.105f * D, -0.11f * D, 0.105f * D, -0.14f * D, 0.02f * D);
                m.CloseFigure();
                Stroke(g, m, TnInk(k), TnW(D) * 2);
                FillSolid(g, m, Color.FromArgb(120, 36, 40));
                Region old = g.Clip;
                g.SetClip(m, CombineMode.Intersect);
                Ellipse(g, Color.FromArgb(240, 120, 130), -0.06f * D, 0.06f * D, 0.12f * D, 0.06f * D);
                using (SolidBrush t = new SolidBrush(Color.FromArgb(250, 248, 238)))
                    for (int i = 0; i < 5; i++) g.FillRectangle(t, (-0.1f + i * 0.04f + 0.002f) * D, 0.018f * D, 0.036f * D, 0.034f * D);
                g.Clip = old;
                old.Dispose();
            }
            VOval(g, k, D, Darker(TtSkin, 0.04), TtSkinShade, 0, 0, -0.008f, 0.075f, 0.058f);
        }

        // The Armored Titan: white plates over the face, the brow plate, the plates down the cheeks and the nose, dark
        // slits for the eyes, and the teeth bare between the plates of the jaw.
        static void ArmoredFace(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, ArPlate, ArPlateShade, D, 40);
            Region old = g.Clip;
            g.SetClip(face, CombineMode.Intersect);
            Color seam = Color.FromArgb(170, 150, 120, 96);
            TnLine(g, seam, 0.008f, D, -0.3f, -0.13f, -0.1f, -0.15f, 0, -0.14f, 0.1f, -0.15f, 0.3f, -0.13f);
            foreach (int s in Sides)
            {
                TnLine(g, seam, 0.008f, D, s * 0.16f, -0.13f, s * 0.15f, 0.0f, s * 0.13f, 0.1f);
                using (GraphicsPath slot = KCurve(D, 0.45f, s * 0.045f, -0.06f, s * 0.11f, -0.1f, s * 0.19f, -0.08f, s * 0.2f, -0.01f, s * 0.12f, 0.01f, s * 0.05f, -0.01f))
                    FillSolid(g, slot, Color.FromArgb(60, 40, 36));
            }
            TnLine(g, seam, 0.008f, D, -0.022f, -0.14f, -0.026f, 0.02f);
            TnLine(g, seam, 0.008f, D, 0.022f, -0.14f, 0.026f, 0.02f);
            g.Clip = old;
            old.Dispose();
            AttackTeeth(g, k, D, 0.1f, 0.06f, 0.03f);
            using (GraphicsPath f = TnSpikes(D, 0.035f, ArmoredFringe)) SlayerHair(g, k, f, D, ArHair, Darker(ArHair, 0.35));
        }

        // The Armored Titan's short blond hair over the top of his armoured head.
        static readonly float[] ArmoredFringe =
        {
            -0.275f, -0.21f, -0.12f, -0.39f, 0.08f, -0.4f, 0.275f, -0.24f, 0.245f, -0.19f, 0.18f, -0.25f, 0.11f, -0.215f, 0.04f, -0.27f, -0.03f, -0.225f,
            -0.1f, -0.28f, -0.17f, -0.225f, -0.23f, -0.265f
        };
    }

    public partial class Mascot
    {
        // Their eyes: Cap's, Thor's and Captain Marvel's blue, the Hulk's angry green, the Widow's and the Witch's green,
        // Hawkeye's grey, Strange's blue-green; the Panther's white lenses; the titans': the Colossal's small pale lights
        // deep in their sockets, the Attack Titan's glowing green, the Armored's white glow in the slits.
        bool AvEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            float b = surprised ? 1.15f : blink;
            switch (Look.Kind)
            {
                case MascotParts.KindCapSd:
                case MascotParts.KindThorSd:
                case MascotParts.KindCarolSd: return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.14f, Color.FromArgb(24, 60, 150), Color.FromArgb(96, 156, 244), 0.88f);
                case MascotParts.KindHulkSd: return NinjaEye(g, x, y, D, blink, surprised, 0.11f, 0.11f, Color.FromArgb(30, 100, 36), Color.FromArgb(120, 214, 90), 0.62f);
                case MascotParts.KindWidowSd:
                case MascotParts.KindWandaSd: return NinjaEye(g, x, y, D, blink, surprised, 0.12f, 0.14f, Color.FromArgb(30, 100, 70), Color.FromArgb(110, 196, 140), 0.86f);
                case MascotParts.KindHawkSd: return NinjaEye(g, x, y, D, blink, surprised, 0.112f, 0.12f, Color.FromArgb(56, 74, 104), Color.FromArgb(140, 160, 192), 0.72f);
                case MascotParts.KindStrangeSd: return NinjaEye(g, x, y, D, blink, surprised, 0.112f, 0.12f, Color.FromArgb(36, 90, 100), Color.FromArgb(110, 184, 196), 0.74f);
                case MascotParts.KindPantherSd: LensEye(g, x, y + 0.006f * D, D, b, 0.12f, 0.06f, 18, Color.FromArgb(250, 250, 252), 0.012f, Color.Empty); return true;
                case MascotParts.KindColossalSd:
                {
                    if (b < 0.3f) return true;
                    MascotParts.Glow(g, x, y, 0.07f * D, 0.06f * D, Color.FromArgb(255, 236, 160), 170);
                    MascotParts.Ellipse(g, Color.FromArgb(255, 250, 220), x - 0.016f * D, y - 0.014f * D * b, 0.032f * D, 0.028f * D * b);
                    return true;
                }
                case MascotParts.KindAttackSd:
                    MascotParts.Glow(g, x, y, 0.16f * D, 0.13f * D, Color.FromArgb(120, 240, 170), 70);
                    return NinjaEye(g, x, y, D, blink, surprised, 0.11f, 0.11f, Color.FromArgb(20, 110, 70), Color.FromArgb(110, 240, 170), 0.66f);
                case MascotParts.KindSmileTitanSd:
                {
                    // Its eyes squeezed shut into happy curves over the grin, open only when something surprises it.
                    if (surprised) return DotEye(g, x, y, D, 0.026f * D, 0.032f * D, 1);
                    using (Pen p = new Pen(Color.FromArgb(46, 30, 28), Math.Max(1.1f, D * 0.012f)))
                    {
                        p.StartCap = p.EndCap = LineCap.Round;
                        g.DrawArc(p, x - 0.045f * D, y - 0.01f * D, 0.09f * D, 0.05f * D, 200, 140);
                    }
                    return true;
                }
                case MascotParts.KindBoboTitanSd:
                {
                    // Big round white eyes with tiny pupils looking two ways at once.
                    float sk = surprised ? 1.15f : 1f, w = 0.13f * D * sk, h = 0.13f * D * sk * (surprised ? 1 : blink);
                    if (h < 0.018f * D) { MascotParts.Line(g, Color.FromArgb(46, 30, 28), Math.Max(1f, D * 0.011f), x - w * 0.45f, y, x + w * 0.45f, y); return true; }
                    MascotParts.Ellipse(g, Color.White, x - w / 2, y - h / 2, w, h);
                    using (Pen p = new Pen(Color.FromArgb(46, 30, 28), Math.Max(1f, D * 0.009f))) g.DrawEllipse(p, x - w / 2, y - h / 2, w, h);
                    float px = x < 0 ? -0.028f : 0.03f, py = x < 0 ? -0.026f : 0.022f;
                    MascotParts.Ellipse(g, Color.FromArgb(22, 18, 22), x + px * D - 0.013f * D, y + py * D - 0.013f * D, 0.026f * D, 0.026f * D);
                    return true;
                }
                case MascotParts.KindArmoredSd: LensEye(g, x, y - 0.03f * D, D, b, 0.08f, 0.036f, 6, Color.FromArgb(255, 246, 214), 0, Color.FromArgb(255, 220, 140)); return true;
                default: return true;
            }
        }

        // Their mouths, shut: the Hulk's clenched teeth (bared and roaring when open), the Widow's and the Witch's red lips,
        // Hawkeye's flat line, Captain Marvel's smirk. None under the Panther's mask, and the titans' teeth are on their
        // faces. False: the usual one.
        bool AvMouth(Graphics g, float D, float mx, float my, Color ink, bool open)
        {
            int k = Look.Kind;
            if (k == MascotParts.KindPantherSd || k == MascotParts.KindColossalSd || k == MascotParts.KindAttackSd || k == MascotParts.KindArmoredSd || k == MascotParts.KindSmileTitanSd || k == MascotParts.KindBoboTitanSd) return true;
            if (k == MascotParts.KindHulkSd)
            {
                float w = 0.12f * D, h = (open ? 0.07f : 0.04f) * D;
                RectangleF r = new RectangleF(mx - w / 2, my - h / 2 + 0.006f * D, w, h);
                using (GraphicsPath m = Theme.Round(r, h * 0.4f))
                {
                    MascotParts.FillSolid(g, m, open ? Color.FromArgb(90, 24, 30) : Color.FromArgb(250, 248, 236));
                    if (open)
                        using (SolidBrush t = new SolidBrush(Color.FromArgb(250, 248, 236))) g.FillRectangle(t, r.X + w * 0.1f, r.Y, w * 0.8f, h * 0.28f);
                    else
                        using (Pen tl = new Pen(Color.FromArgb(150, 90, 100, 80), Math.Max(0.7f, D * 0.004f)))
                        {
                            g.DrawLine(tl, r.X, r.Y + h / 2, r.Right, r.Y + h / 2);
                            for (int i = 1; i < 5; i++) g.DrawLine(tl, r.X + w * i / 5, r.Y, r.X + w * i / 5, r.Bottom);
                        }
                    MascotParts.Stroke(g, m, Color.FromArgb(220, 30, 40, 20), Math.Max(1f, D * 0.01f));
                }
                return true;
            }
            if (open) return false;
            Color lip = k == MascotParts.KindWidowSd || k == MascotParts.KindWandaSd ? Color.FromArgb(200, 50, 60) : ink;
            using (Pen p = new Pen(lip, Math.Max(1.1f, D * 0.012f)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                switch (k)
                {
                    case MascotParts.KindWidowSd:
                    case MascotParts.KindWandaSd: g.DrawArc(p, mx - 0.03f * D, my - 0.02f * D, 0.06f * D, 0.036f * D, 20, 140); return true;
                    case MascotParts.KindHawkSd: g.DrawLine(p, mx - 0.022f * D, my + 0.004f * D, mx + 0.022f * D, my + 0.004f * D); return true;
                    case MascotParts.KindCarolSd: g.DrawBezier(p, mx - 0.045f * D, my - 0.006f * D, mx - 0.015f * D, my + 0.014f * D, mx + 0.025f * D, my + 0.01f * D, mx + 0.05f * D, my - 0.012f * D); return true;
                }
            }
            return false;
        }
    }
}
