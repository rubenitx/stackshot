// Stackshot - The characters that were only a head, made whole as anime chibis like the ninjas (MascotToons). Edward
// Elric (golden hair parted in the middle with its long side locks, the antenna on top and the braid behind, the red coat
// open over the black jacket, white gloves); Eren Jaeger and Levi Ackerman in the Survey Corps uniform (the short light
// brown jacket with the wings, white shirt, leather straps, white trousers, tall boots, the gear at the hips and the green
// cloak; Eren's shaggy brown hair and angry green eyes, Levi's black hair parted over the undercut, narrow grey eyes and
// white cravat); Gojo Satoru (white hair standing up over the black blindfold, the high-collared black uniform);
// Saitama (bald and shining, dot eyes, the yellow suit, white cape, red gloves and boots); Kratos (ashen skin, the red
// tattoo over his eye, the full beard, bare chest with the leather strap, the red skirt, bracers); the Doom Slayer (the
// green helmet with its gold visor, the armour); Spider-Man (the red mask with its web and big white lenses, blue sides
// and legs, the spider on his chest); Batman (the cowl with its ears and white slits, grey suit, the bat on the yellow
// oval, the belt, black cape); the Flash (the red cowl with the lightning at the ears, the bolt on his chest, yellow
// boots); Superman (black hair with the curl, the shield on the blue suit, red cape, trunks and boots); Iron Man (the
// red helmet with the gold faceplate and glowing eyes, the light in his chest). And the app's own: the knight (steel
// helmet with the T of its visor and the eyes shining in it, the red plume, the red tabard with its cross), the
// astronaut (the glass helmet over a child's face, the white suit with its panel and backpack), the wise wizard (the tall
// purple hat with its star, the long white beard, the robe and the staff with its light) and the superhero (the purple
// mask, the gold star on the purple suit, the red cape). Fan tributes, not affiliated with their owners (see the
// README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindEdwardSd = 112, KindErenSd = 113, KindLeviSd = 114, KindGojoSd = 115, KindSaitamaSd = 116, KindKratosSd = 117,
                         KindDoomSd = 118, KindSpiderSd = 119, KindBatSd = 120, KindFlashSd = 121, KindSuperSd = 122, KindIronSd = 123,
                         KindKnightSd = 124, KindAstroSd = 125, KindWizardSd = 126, KindHeroSd = 127;
        public const int PersonaFirst = 112, PersonaLast = 139;

        public static bool IsPersona(int kind) { return (kind >= PersonaFirst && kind <= PersonaLast) || kind == KindSmileTitanSd || kind == KindBoboTitanSd; }

        // Their faces are covered by a mask or a helmet (no ears, no hair).
        static bool PersonaMasked(int k) { return k == KindDoomSd || k == KindSpiderSd || k == KindIronSd || k == KindKnightSd; }

        static readonly Color EdGold = Color.FromArgb(240, 196, 76), EdGoldShade = Color.FromArgb(196, 140, 34), EdGoldLight = Color.FromArgb(255, 238, 168);
        static readonly Color EdRed = Color.FromArgb(198, 40, 40), EdRedShade = Color.FromArgb(146, 22, 26), EdBlack = Color.FromArgb(42, 40, 48), EdBlackShade = Color.FromArgb(24, 22, 28);
        static readonly Color ScBrown = Color.FromArgb(198, 160, 106), ScBrownShade = Color.FromArgb(158, 118, 70), ScStrap = Color.FromArgb(100, 64, 40);
        static readonly Color ScWhite = Color.FromArgb(246, 244, 238), ScWhiteShade = Color.FromArgb(206, 204, 198), ScBoot = Color.FromArgb(108, 70, 42);
        static readonly Color ScCloak = Color.FromArgb(52, 104, 74), ScCloakShade = Color.FromArgb(32, 72, 50);
        static readonly Color ErHair = Color.FromArgb(92, 62, 42), ErHairShade = Color.FromArgb(58, 38, 26), ErHairLight = Color.FromArgb(160, 120, 86);
        static readonly Color LvHair = Color.FromArgb(34, 34, 40), LvHairShade = Color.FromArgb(14, 14, 18);
        static readonly Color GjWhite = Color.FromArgb(244, 246, 252), GjWhiteShade = Color.FromArgb(188, 198, 222), GjBlack = Color.FromArgb(34, 38, 54), GjBlackShade = Color.FromArgb(18, 20, 30);
        static readonly Color StYellow = Color.FromArgb(250, 214, 52), StYellowShade = Color.FromArgb(212, 166, 28), HeroRed = Color.FromArgb(216, 34, 42), HeroRedShade = Color.FromArgb(158, 18, 28);
        static readonly Color KrSkin = Color.FromArgb(230, 228, 224), KrSkinShade = Color.FromArgb(194, 190, 186), KrRed = Color.FromArgb(196, 36, 40), KrBeard = Color.FromArgb(86, 60, 42);
        static readonly Color DmGreen = Color.FromArgb(100, 118, 72), DmGreenShade = Color.FromArgb(66, 80, 46), DmDark = Color.FromArgb(62, 64, 62);
        static readonly Color SpBlue = Color.FromArgb(40, 72, 172), SpBlueShade = Color.FromArgb(24, 46, 120);
        static readonly Color BtGrey = Color.FromArgb(124, 126, 138), BtGreyShade = Color.FromArgb(86, 88, 100), BtCowl = Color.FromArgb(42, 44, 56), BtCowlShade = Color.FromArgb(22, 24, 32);
        static readonly Color SmBlue = Color.FromArgb(40, 94, 206), SmBlueShade = Color.FromArgb(24, 62, 152), SmHair = Color.FromArgb(28, 30, 44), SmHairShade = Color.FromArgb(10, 12, 20);
        static readonly Color ImRed = Color.FromArgb(194, 30, 38), ImRedShade = Color.FromArgb(132, 14, 22), ImGold = Color.FromArgb(232, 182, 62), ImGoldShade = Color.FromArgb(180, 124, 26);
        static readonly Color KnSteel = Color.FromArgb(206, 210, 220), KnSteelShade = Color.FromArgb(140, 146, 162), KnRed = Color.FromArgb(196, 36, 44), KnRedShade = Color.FromArgb(140, 20, 28);
        static readonly Color AsWhite = Color.FromArgb(244, 246, 250), AsWhiteShade = Color.FromArgb(194, 200, 214), AsHair = Color.FromArgb(112, 76, 50);
        static readonly Color WzPurple = Color.FromArgb(114, 72, 188), WzPurpleShade = Color.FromArgb(74, 44, 132), WzBeard = Color.FromArgb(248, 248, 246), WzBeardShade = Color.FromArgb(200, 202, 212);
        static readonly Color HrPurple = Color.FromArgb(104, 70, 196), HrPurpleShade = Color.FromArgb(66, 42, 140), HrHair = Color.FromArgb(40, 34, 30), HrHairShade = Color.FromArgb(18, 14, 12);

        // ---- Hair: the mass behind the head (points alternating notch and tip, clockwise) and the fringe over the face.

        static readonly float[] ErenHair =
        {
            -0.255f, 0.02f, -0.3f, 0.06f, -0.29f, -0.06f, -0.335f, -0.1f, -0.3f, -0.2f, -0.34f, -0.27f, -0.26f, -0.33f, -0.27f, -0.42f,
            -0.14f, -0.41f, -0.08f, -0.47f, 0, -0.43f, 0.09f, -0.47f, 0.15f, -0.41f, 0.27f, -0.42f, 0.26f, -0.33f, 0.34f, -0.27f,
            0.3f, -0.2f, 0.335f, -0.1f, 0.29f, -0.06f, 0.3f, 0.06f, 0.255f, 0.02f
        };
        static readonly float[] ErenFringe =
        {
            -0.29f, -0.26f, -0.12f, -0.385f, 0.08f, -0.39f, 0.28f, -0.3f, 0.285f, -0.1f, 0.22f, -0.2f, 0.17f, -0.1f, 0.11f, -0.22f,
            0.05f, -0.115f, 0, -0.24f, -0.06f, -0.13f, -0.12f, -0.23f, -0.18f, -0.115f, -0.24f, -0.22f, -0.285f, -0.1f
        };
        static readonly float[] EdwardHair =
        {
            -0.25f, 0.05f, -0.32f, 0.1f, -0.3f, -0.05f, -0.37f, -0.1f, -0.31f, -0.2f, -0.38f, -0.29f, -0.27f, -0.32f, -0.29f, -0.45f,
            -0.15f, -0.41f, -0.08f, -0.5f, 0, -0.43f, 0.08f, -0.5f, 0.15f, -0.41f, 0.29f, -0.45f, 0.27f, -0.32f, 0.38f, -0.29f,
            0.31f, -0.2f, 0.37f, -0.1f, 0.3f, -0.05f, 0.32f, 0.1f, 0.25f, 0.05f
        };
        // Edward's fringe in one piece: parted in the middle (a short V of brow between the two front locks) and falling to
        // both sides down to the long locks beside his cheeks.
        static readonly float[] EdwardFringe =
        {
            -0.015f, -0.395f, -0.16f, -0.375f, -0.27f, -0.29f, -0.305f, -0.13f, -0.3f, 0.07f, -0.25f, -0.09f, -0.205f, -0.2f, -0.14f, -0.12f,
            -0.1f, -0.25f, -0.05f, -0.17f, -0.024f, -0.29f, 0, -0.318f, 0.024f, -0.29f, 0.05f, -0.17f, 0.1f, -0.25f, 0.14f, -0.12f, 0.205f, -0.2f,
            0.25f, -0.09f, 0.3f, 0.07f, 0.305f, -0.13f, 0.27f, -0.29f, 0.16f, -0.375f, 0.015f, -0.395f
        };
        static readonly float[] GojoHair =
        {
            -0.26f, -0.04f, -0.33f, -0.1f, -0.28f, -0.2f, -0.41f, -0.33f, -0.26f, -0.36f, -0.31f, -0.56f, -0.14f, -0.45f, -0.1f, -0.65f,
            0, -0.48f, 0.1f, -0.67f, 0.14f, -0.47f, 0.31f, -0.59f, 0.25f, -0.38f, 0.41f, -0.35f, 0.28f, -0.22f, 0.33f, -0.1f, 0.26f, -0.04f
        };
        // Gojo's hair over the top of his head down to the blindfold, its ends tucked in under it.
        static readonly float[] GojoFringe =
        {
            -0.295f, -0.12f, -0.28f, -0.3f, -0.17f, -0.41f, 0, -0.44f, 0.17f, -0.41f, 0.28f, -0.3f, 0.295f, -0.12f, 0.21f, -0.17f, 0.12f, -0.12f,
            0.03f, -0.18f, -0.06f, -0.12f, -0.15f, -0.18f, -0.23f, -0.12f
        };

        // ---- Hooks from the toons (MascotToons): where each part goes for these.

        static bool PersonaFrame(int k, out float top, out float bottom)
        {
            if (k >= KindCapSd) return AvFrame(k, out top, out bottom);
            bottom = 0.16f;
            switch (k)
            {
                case KindEdwardSd: top = -0.68f; break;
                case KindErenSd: top = -0.55f; break;
                case KindLeviSd: top = -0.5f; break;
                case KindGojoSd: top = -0.7f; break;
                case KindBatSd: top = -0.6f; break;
                case KindFlashSd: top = -0.5f; break;
                case KindKnightSd: top = -0.62f; break;
                case KindAstroSd: top = -0.56f; break;
                case KindWizardSd: top = -0.95f; break;
                case KindHeroSd:
                case KindSuperSd: top = -0.5f; break;
                default: top = -0.46f; break;
            }
            return true;
        }

        static float PersonaHeadTop(int k)
        {
            float top, bottom;
            PersonaFrame(k, out top, out bottom);
            return top + 0.02f;
        }

        // The head's kept area: the face and what is drawn over it (the wizard's hat and beard reach far).
        static RectangleF PersonaHeadArea(int k)
        {
            return k == KindWizardSd ? new RectangleF(-0.48f, -0.98f, 0.96f, 1.32f) : new RectangleF(-0.4f, -0.62f, 0.8f, 0.78f);
        }

        // The head, kept: the face (or the mask, the helmet) and the fringe, hat or beard over it.
        static void PersonaHead(Graphics g, int k, GraphicsPath face, float D)
        {
            if (k >= KindCapSd) { AvHead(g, k, face, D); return; }
            switch (k)
            {
                case KindSpiderSd:
                {
                    // The red mask, its web: lines out from between the eyes and rings round them.
                    VFill(g, k, face, HeroRed, HeroRedShade, D, 0);
                    Region old = g.Clip;
                    g.SetClip(face, CombineMode.Intersect);
                    using (Pen web = new Pen(Color.FromArgb(150, 40, 10, 14), Math.Max(0.8f, D * 0.006f)))
                    {
                        PointF c = P(0, -0.07f * D);
                        for (int i = 0; i < 12; i++)
                        {
                            double a = Math.PI * 2 * i / 12 + 0.26;
                            g.DrawLine(web, c, P(c.X + (float)Math.Cos(a) * 0.5f * D, c.Y + (float)Math.Sin(a) * 0.5f * D));
                        }
                        for (int r = 1; r < 4; r++) g.DrawEllipse(web, -0.1f * r * D, c.Y - 0.09f * r * D, 0.2f * r * D, 0.18f * r * D);
                    }
                    g.Clip = old;
                    old.Dispose();
                    return;
                }
                case KindIronSd:
                    // The red helmet and its gold faceplate, with the line of the mouth.
                    VFill(g, k, face, ImRed, ImRedShade, D, 0);
                    VBlob(g, k, D, ImGold, ImGoldShade, 0, 0.35f, -0.2f, -0.25f, 0, -0.29f, 0.2f, -0.25f, 0.215f, -0.06f, 0.17f, 0.05f, 0.08f, 0.09f, 0, 0.096f,
                          -0.08f, 0.09f, -0.17f, 0.05f, -0.215f, -0.06f);
                    TnLine(g, Color.FromArgb(160, 120, 70, 10), 0.008f, D, -0.06f, 0.056f, 0.06f, 0.056f);
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(120, 120, 70, 10), 0.006f, D, s * 0.11f, 0.06f, s * 0.15f, 0.0f);
                    return;
                case KindDoomSd:
                    // The green helmet: the gold visor across it, a ridge over the top and the plates at the cheeks.
                    VFill(g, k, face, DmGreen, DmGreenShade, D, 0);
                    TnLine(g, Color.FromArgb(150, 40, 50, 30), 0.012f, D, 0, -0.355f, 0, -0.2f);
                    foreach (int s in Sides) VPoly(g, k, D, DmDark, Darker(DmDark, 0.3), 0, s * 0.08f, 0.03f, s * 0.24f, -0.02f, s * 0.2f, 0.07f, s * 0.08f, 0.095f);
                    using (GraphicsPath v = Theme.Round(new RectangleF(-0.215f * D, -0.17f * D, 0.43f * D, 0.17f * D), 0.07f * D))
                    {
                        Stroke(g, v, TnInk(k), TnW(D) * 2);
                        using (LinearGradientBrush b = new LinearGradientBrush(v.GetBounds(), Color.FromArgb(252, 214, 96), Color.FromArgb(196, 108, 22), 90f)) g.FillPath(b, v);
                        Region old = g.Clip;
                        g.SetClip(v, CombineMode.Intersect);
                        using (Pen shine = new Pen(Color.FromArgb(130, 255, 255, 255), 0.03f * D)) g.DrawLine(shine, -0.12f * D, 0.02f * D, -0.02f * D, -0.2f * D);
                        g.Clip = old;
                        old.Dispose();
                    }
                    return;
                case KindKnightSd:
                    // The steel helm: the T of its visor, dark, a ridge over the top and rivets at the sides.
                    VFill(g, k, face, KnSteel, KnSteelShade, D, 0);
                    TnLine(g, Color.FromArgb(150, 90, 96, 112), 0.012f, D, 0, -0.355f, 0, -0.12f);
                    using (GraphicsPath slit = Theme.Round(new RectangleF(-0.2f * D, -0.072f * D, 0.4f * D, 0.064f * D), 0.024f * D)) FillSolid(g, slit, Color.FromArgb(28, 30, 38));
                    using (GraphicsPath slit = Theme.Round(new RectangleF(-0.024f * D, -0.03f * D, 0.048f * D, 0.13f * D), 0.016f * D)) FillSolid(g, slit, Color.FromArgb(28, 30, 38));
                    foreach (int s in Sides)
                        for (int i = 0; i < 3; i++) VOval(g, k, D, Color.FromArgb(236, 238, 244), Color.Empty, 0, s * 0.21f, -0.2f + i * 0.08f, 0.022f, 0.022f);
                    return;
            }
            VFill(g, k, face, k == KindKratosSd ? KrSkin : NjSkin, k == KindKratosSd ? KrSkinShade : NjSkinShade, D, 0);
            switch (k)
            {
                case KindErenSd:
                    using (GraphicsPath f = TnSpikes(D, 0.04f, ErenFringe)) SlayerHair(g, k, f, D, ErHair, ErHairShade);
                    break;
                case KindEdwardSd:
                    using (GraphicsPath f = TnSpikes(D, 0.035f, EdwardFringe)) SlayerHair(g, k, f, D, EdGold, EdGoldShade);
                    break;
                case KindGojoSd:
                    using (GraphicsPath f = TnSpikes(D, 0.03f, GojoFringe)) SlayerHair(g, k, f, D, GjWhite, GjWhiteShade);
                    break;
                case KindLeviSd:
                    // His black hair in one piece: parted in the middle (a short V of brow), falling to both sides in curtains.
                    using (GraphicsPath f = KCurve(D, 0.4f, 0, -0.474f, 0.16f, -0.45f, 0.27f, -0.33f, 0.298f, -0.15f, 0.278f, -0.06f, 0.24f, -0.15f, 0.17f, -0.19f, 0.1f, -0.16f,
                                                   0.06f, -0.235f, 0.026f, -0.285f, 0, -0.305f, -0.026f, -0.285f, -0.06f, -0.235f, -0.1f, -0.16f, -0.17f, -0.19f, -0.24f, -0.15f,
                                                   -0.278f, -0.06f, -0.298f, -0.15f, -0.27f, -0.33f, -0.16f, -0.45f))
                        SlayerHair(g, k, f, D, LvHair, LvHairShade);
                    break;
                case KindSaitamaSd:
                    // The shine on his bald head.
                    Glow(g, -0.08f * D, -0.27f * D, 0.17f * D, 0.1f * D, Color.White, 150);
                    break;
                case KindSuperSd:
                    // Black hair combed back from a high brow, and the curl falling on his forehead.
                    using (GraphicsPath f = KCurve(D, 0.45f, -0.29f, -0.19f, -0.26f, -0.33f, -0.15f, -0.41f, 0.02f, -0.43f, 0.18f, -0.4f, 0.28f, -0.31f, 0.3f, -0.17f,
                                                   0.24f, -0.27f, 0.12f, -0.3f, 0, -0.285f, -0.12f, -0.3f, -0.22f, -0.26f))
                        SlayerHair(g, k, f, D, SmHair, SmHairShade);
                    VBlob(g, k, D, SmHair, SmHairShade, 0, 0.5f, 0.0f, -0.3f, 0.04f, -0.27f, 0.025f, -0.22f, -0.005f, -0.205f, 0.012f, -0.235f, 0.004f, -0.265f, -0.02f, -0.285f);
                    break;
                case KindHeroSd:
                    // Short black hair swept up at the front.
                    using (GraphicsPath f = KCurve(D, 0.45f, -0.29f, -0.18f, -0.27f, -0.33f, -0.16f, -0.42f, 0, -0.45f, 0.17f, -0.43f, 0.27f, -0.33f, 0.3f, -0.18f,
                                                   0.22f, -0.28f, 0.1f, -0.31f, -0.02f, -0.27f, -0.14f, -0.3f, -0.23f, -0.25f))
                        SlayerHair(g, k, f, D, HrHair, HrHairShade);
                    break;
                case KindAstroSd:
                    // A short brown fringe.
                    using (GraphicsPath f = KCurve(D, 0.45f, -0.27f, -0.2f, -0.23f, -0.33f, -0.12f, -0.4f, 0, -0.42f, 0.12f, -0.4f, 0.23f, -0.33f, 0.27f, -0.2f,
                                                   0.18f, -0.26f, 0.08f, -0.23f, 0, -0.28f, -0.08f, -0.23f, -0.18f, -0.26f))
                        SlayerHair(g, k, f, D, AsHair, Darker(AsHair, 0.35));
                    break;
                case KindWizardSd:
                {
                    // The long white beard and moustache, then his tall purple hat: its brim, the cone bending over at the
                    // tip, the gold band and a star.
                    VBlob(g, k, D, WzBeard, WzBeardShade, 0, 0.45f, -0.262f, -0.06f, -0.18f, 0.0f, -0.08f, 0.04f, 0, 0.03f, 0.08f, 0.04f, 0.18f, 0.0f, 0.262f, -0.06f,
                          0.25f, 0.08f, 0.16f, 0.2f, 0.05f, 0.26f, 0, 0.29f, -0.05f, 0.26f, -0.16f, 0.2f, -0.25f, 0.08f);
                    VBlob(g, k, D, WzBeard, WzBeardShade, 0, 0.45f, -0.12f, 0.05f, -0.06f, 0.02f, 0, 0.035f, 0.06f, 0.02f, 0.12f, 0.05f, 0.08f, 0.075f, 0, 0.06f, -0.08f, 0.075f);
                    if (WornHat != 0)
                    {
                        // His hat off for the one picked for him: his white hair, combed back.
                        using (GraphicsPath f = KCurve(D, 0.45f, -0.27f, -0.2f, -0.24f, -0.32f, -0.13f, -0.39f, 0, -0.41f, 0.13f, -0.39f, 0.24f, -0.32f, 0.27f, -0.2f,
                                                       0.2f, -0.26f, 0.1f, -0.29f, 0, -0.3f, -0.1f, -0.29f, -0.2f, -0.26f))
                            SlayerHair(g, k, f, D, WzBeard, WzBeardShade);
                        break;
                    }
                    VOval(g, k, D, WzPurple, WzPurpleShade, 0, 0, -0.3f, 0.72f, 0.14f);
                    using (GraphicsPath cone = KCurve(D, 0.45f, -0.25f, -0.31f, -0.17f, -0.5f, -0.05f, -0.68f, 0.06f, -0.81f, 0.17f, -0.92f, 0.13f, -0.78f, 0.11f, -0.62f, 0.2f, -0.45f, 0.25f, -0.31f))
                    {
                        VFill(g, k, cone, WzPurple, WzPurpleShade, D, 0);
                        Region old = g.Clip;
                        g.SetClip(cone, CombineMode.Intersect);
                        using (GraphicsPath band = KPoly(D, -0.4f, -0.4f, 0.4f, -0.4f, 0.4f, -0.33f, -0.4f, -0.33f))
                        {
                            Stroke(g, band, TnInk(k), TnW(D) * 2);
                            VPaint(g, band, Color.FromArgb(246, 196, 60), Color.FromArgb(206, 150, 30), D, 30);
                        }
                        g.Clip = old;
                        old.Dispose();
                    }
                    using (GraphicsPath star = Star(D, 0.01f, -0.53f, 0.05f, 0.022f)) VFill(g, k, star, Color.FromArgb(252, 222, 80), Color.FromArgb(220, 170, 30), D, 0);
                    break;
                }
                case KindKratosSd:
                {
                    // The red tattoo down over his left eye, then the full beard round his jaw.
                    Region old = g.Clip;
                    g.SetClip(face, CombineMode.Intersect);
                    using (GraphicsPath t = KPoly(D, 0.07f, -0.4f, 0.15f, -0.4f, 0.17f, -0.2f, 0.16f, -0.02f, 0.11f, 0.02f, 0.1f, -0.2f))
                    {
                        Stroke(g, t, Color.FromArgb(120, 80, 10, 10), D * 0.008f);
                        FillSolid(g, t, KrRed);
                    }
                    g.Clip = old;
                    old.Dispose();
                    VBlob(g, k, D, KrBeard, Darker(KrBeard, 0.3), 0, 0.45f, -0.262f, -0.04f, -0.2f, 0.01f, -0.1f, 0.03f, 0, 0.02f, 0.1f, 0.03f, 0.2f, 0.01f, 0.262f, -0.04f,
                          0.25f, 0.06f, 0.16f, 0.15f, 0, 0.19f, -0.16f, 0.15f, -0.25f, 0.06f);
                    break;
                }
                case KindBatSd:
                case KindFlashSd:
                {
                    // The cowl over his head and down past his eyes to the nose, leaving his mouth and chin bare.
                    bool bat = k == KindBatSd;
                    Region old = g.Clip;
                    g.SetClip(face, CombineMode.Intersect);
                    using (GraphicsPath c = KCurve(D, 0.45f, -0.4f, -0.5f, 0.4f, -0.5f, 0.4f, 0, 0.27f, 0.035f, 0.17f, 0.045f, 0.08f, 0.03f, 0, 0.0f, -0.08f, 0.03f,
                                                   -0.17f, 0.045f, -0.27f, 0.035f, -0.4f, 0))
                    {
                        Stroke(g, c, TnInk(k), TnW(D) * 2);
                        if (bat) VPaint(g, c, BtCowl, BtCowlShade, D, 40);
                        else VPaint(g, c, HeroRed, HeroRedShade, D, 40);
                    }
                    g.Clip = old;
                    old.Dispose();
                    break;
                }
            }
        }

        // A five-pointed star (centre, outer and inner radius, units of D).
        static GraphicsPath Star(float D, float cx, float cy, float ro, float ri)
        {
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / 5;
                float r = i % 2 == 0 ? ro : ri;
                pts[i] = P((cx + (float)Math.Cos(a) * r) * D, (cy + (float)Math.Sin(a) * r) * D);
            }
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        // Behind: the cape, cloak or backpack (kept), the hair with the ears (kept), the legs stepping as they walk, the
        // arms, the wizard's staff in his hand, and over them the clothes (kept).
        static void PersonaBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            if (k >= KindCapSd) { AvBack(g, k, D, p, geo); return; }
            if (k == KindErenSd || k == KindLeviSd || k == KindSaitamaSd || k == KindBatSd || k == KindSuperSd || k == KindHeroSd || k == KindAstroSd)
                DsCached(g, D, TnId(k, 0), new RectangleF(-0.3f, 0, 0.6f, 0.42f), bg => PersonaCape(bg, k, D));
            else if (k == KindEdwardSd) DsCached(g, D, TnId(k, 0), new RectangleF(-0.34f, -0.1f, 0.3f, 0.44f), bg => EdwardBraid(bg, k, D));
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.5f, -0.72f, 1f, 0.9f), bg => PersonaHairBack(bg, k, D));
            foreach (int s in Sides) PersonaLeg(g, k, D, s, Foot(p, s, s * 0.05f, 0.322f));
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.22f, -0.02f, 0.44f, 0.36f), bg => PersonaClothes(bg, k, D));
            if (k == KindWizardSd && !p.HideHands)
            {
                // His staff, held in his near hand and leaning out past his head, the light at its top.
                PointF h = DsHand(p, geo, 1), top = P(h.X + 0.15f, h.Y - 0.4f);
                VLimb(g, k, D, Color.FromArgb(138, 94, 54), 0.026f, P(h.X - 0.03f, h.Y + 0.09f), top, 0);
                GraphicsState st = g.Save();
                g.TranslateTransform(top.X * D, (top.Y - 0.02f) * D);
                DsCached(g, D, TnId(k, 7), new RectangleF(-0.07f, -0.07f, 0.14f, 0.14f), bg =>
                {
                    Glow(bg, 0, 0, 0.11f * D, 0.11f * D, Color.FromArgb(140, 210, 255), 170);
                    VOval(bg, k, D, Color.FromArgb(200, 238, 255), Color.FromArgb(110, 180, 230), 0, 0, 0, 0.052f, 0.052f);
                });
                g.Restore(st);
            }
        }

        // A cape or cloak hanging behind, its edges showing past the arms; the astronaut's backpack.
        static void PersonaCape(Graphics g, int k, float D)
        {
            if (k == KindAstroSd)
            {
                VRound(g, k, D, AsWhite, AsWhiteShade, 0, -0.15f, 0.04f, 0.3f, 0.21f, 0.04f);
                return;
            }
            Color c, cs;
            switch (k)
            {
                case KindSaitamaSd: c = Color.FromArgb(250, 250, 248); cs = Color.FromArgb(206, 208, 214); break;
                case KindBatSd: c = BtCowl; cs = BtCowlShade; break;
                case KindSuperSd:
                case KindHeroSd: c = HeroRed; cs = HeroRedShade; break;
                default: c = ScCloak; cs = ScCloakShade; break;
            }
            VBlob(g, k, D, c, cs, 0, 0.45f, -0.1f, 0.06f, 0.1f, 0.06f, 0.17f, 0.12f, 0.21f, 0.3f, 0.12f, 0.33f, 0, 0.3f, -0.12f, 0.33f, -0.21f, 0.3f, -0.17f, 0.12f);
        }

        // Edward's braid down his back, swinging out behind his shoulder: woven lobes leaning in turn, the black tie and the
        // tuft at its end. The coat and the arm come over it.
        static void EdwardBraid(Graphics g, int k, float D)
        {
            PointF a = P(-0.1f, -0.05f), b = P(-0.2f, -0.02f), c = P(-0.255f, 0.09f), d = P(-0.25f, 0.21f);
            const int n = 6;
            for (int i = 0; i < n; i++)
            {
                float t = 0.1f + i * 0.15f;
                PointF q = Bez(a, b, c, d, t), dd = BezD(a, b, c, d, t);
                float ang = (float)(Math.Atan2(dd.Y, dd.X) * 180 / Math.PI) - 90 + (i % 2 == 0 ? 26 : -26);
                GraphicsState st = g.Save();
                g.TranslateTransform(q.X * D, q.Y * D);
                g.RotateTransform(ang);
                VOval(g, k, D, EdGold, EdGoldShade, 0, 0, 0, 0.058f - i * 0.0035f, 0.066f - i * 0.002f);
                g.Restore(st);
            }
            PointF e = Bez(a, b, c, d, 0.98f);
            VBlob(g, k, D, EdGold, EdGoldShade, 0, 0.5f, e.X - 0.022f, e.Y + 0.006f, e.X + 0.022f, e.Y + 0.006f, e.X + 0.014f, e.Y + 0.05f, e.X + 0.002f, e.Y + 0.078f,
                  e.X - 0.014f, e.Y + 0.05f);
            VOval(g, k, D, Color.FromArgb(44, 40, 48), Color.Empty, 0, e.X, e.Y, 0.042f, 0.024f);
        }

        static void PersonaHairBack(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindErenSd: SpikyHair(g, k, D, ErenHair, ErHair, ErHairShade, ErHairLight, -0.25f); break;
                case KindEdwardSd:
                    // The hair, then the antenna standing up from the top (the braid hangs behind everything, apart).
                    SpikyHair(g, k, D, EdwardHair, EdGold, EdGoldShade, EdGoldLight, -0.25f);
                    VBlob(g, k, D, EdGold, EdGoldShade, 0, 0.35f, -0.035f, -0.43f, 0.02f, -0.52f, 0.075f, -0.655f, 0.05f, -0.52f, 0.045f, -0.43f);
                    break;
                case KindLeviSd:
                    // Sleek black hair, short at the back over the shaved sides.
                    VBlob(g, k, D, Color.FromArgb(70, 70, 80), Color.Empty, 0, 0.5f, -0.27f, -0.02f, -0.29f, -0.2f, 0.29f, -0.2f, 0.27f, -0.02f, 0, 0.04f);
                    using (GraphicsPath h = KCurve(D, 0.5f, -0.3f, -0.18f, -0.27f, -0.33f, -0.16f, -0.45f, 0, -0.475f, 0.16f, -0.45f, 0.27f, -0.33f, 0.3f, -0.18f, 0, -0.12f))
                        SlayerHair(g, k, h, D, LvHair, LvHairShade);
                    break;
                case KindGojoSd: SpikyHair(g, k, D, GojoHair, GjWhite, GjWhiteShade, Color.White, -0.25f); break;
                case KindSuperSd:
                case KindHeroSd:
                case KindAstroSd:
                {
                    Color hair = k == KindSuperSd ? SmHair : k == KindHeroSd ? HrHair : AsHair;
                    using (GraphicsPath h = KCurve(D, 0.5f, -0.29f, -0.1f, -0.29f, -0.3f, -0.17f, -0.43f, 0, -0.46f, 0.17f, -0.43f, 0.29f, -0.3f, 0.29f, -0.1f, 0, -0.12f))
                        SlayerHair(g, k, h, D, hair, Darker(hair, 0.4));
                    break;
                }
                case KindBatSd:
                    // The cowl's two pointed ears.
                    foreach (int s in Sides) VPoly(g, k, D, BtCowl, BtCowlShade, 0, s * 0.07f, -0.3f, s * 0.2f, -0.58f, s * 0.24f, -0.27f);
                    break;
                case KindFlashSd:
                    // The lightning bolts at the sides of his cowl.
                    foreach (int s in Sides)
                        VPoly(g, k, D, StYellow, StYellowShade, 0, s * 0.24f, -0.2f, s * 0.31f, -0.27f, s * 0.29f, -0.22f, s * 0.38f, -0.33f, s * 0.31f, -0.16f, s * 0.33f, -0.2f, s * 0.255f, -0.1f);
                    break;
                case KindKnightSd:
                    // The red plume standing up from the crest of his helm and curling back.
                    VBlob(g, k, D, KnRed, KnRedShade, 0, 0.45f, -0.04f, -0.33f, 0.0f, -0.46f, 0.08f, -0.56f, 0.2f, -0.6f, 0.3f, -0.55f, 0.24f, -0.5f, 0.13f, -0.46f, 0.06f, -0.36f);
                    break;
                case KindWizardSd:
                    // His long white hair falling behind to his shoulders.
                    VBlob(g, k, D, WzBeard, WzBeardShade, 0, 0.45f, -0.3f, -0.25f, -0.33f, 0.0f, -0.28f, 0.15f, 0, 0.12f, 0.28f, 0.15f, 0.33f, 0.0f, 0.3f, -0.25f, 0, -0.3f);
                    break;
            }
            if (PersonaMasked(k) || k == KindBatSd || k == KindFlashSd || k == KindWizardSd) return;
            Color skin = k == KindKratosSd ? KrSkin : NjSkin, shade = k == KindKratosSd ? KrSkinShade : NjSkinShade;
            foreach (int s in Sides)
            {
                VOval(g, k, D, skin, shade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, 170, 104, 84), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
        }

        // A leg from the hip down to its foot at f, in the trousers or tights, and the shoe or boot.
        static void PersonaLeg(Graphics g, int k, float D, int s, PointF f)
        {
            PointF hip = P(s * 0.046f, 0.235f);
            Color leg, boot;
            bool tall = true;
            switch (k)
            {
                case KindErenSd:
                case KindLeviSd: leg = ScWhite; boot = ScBoot; break;
                case KindEdwardSd: leg = EdBlack; boot = Color.FromArgb(30, 28, 34); tall = false; break;
                case KindGojoSd: leg = GjBlack; boot = Color.FromArgb(24, 24, 30); tall = false; break;
                case KindSaitamaSd: leg = StYellow; boot = HeroRed; break;
                case KindKratosSd: leg = KrSkin; boot = Color.FromArgb(110, 76, 50); break;
                case KindDoomSd: leg = DmDark; boot = Color.FromArgb(44, 46, 44); break;
                case KindSpiderSd: leg = SpBlue; boot = HeroRed; break;
                case KindBatSd: leg = BtGrey; boot = BtCowl; break;
                case KindFlashSd: leg = HeroRed; boot = StYellow; break;
                case KindSuperSd: leg = SmBlue; boot = HeroRed; break;
                case KindIronSd: leg = ImGold; boot = ImRed; break;
                case KindKnightSd: leg = Color.FromArgb(150, 154, 166); boot = KnSteel; break;
                case KindAstroSd: leg = AsWhite; boot = Color.FromArgb(214, 220, 232); break;
                case KindWizardSd: leg = WzPurple; boot = Color.FromArgb(120, 80, 50); tall = false; break;
                default: leg = HrPurple; boot = HeroRed; break;
            }
            VLimb(g, k, D, leg, 0.058f, hip, tall ? P(f.X, f.Y - 0.03f) : f, 0);
            if (tall) VRound(g, k, D, boot, Darker(boot, 0.3), 0, f.X - 0.034f, f.Y - 0.052f, 0.068f, 0.07f, 0.02f);
            if (k == KindDoomSd) VOval(g, k, D, DmGreen, DmGreenShade, 0, f.X, f.Y - 0.075f, 0.064f, 0.05f);   // a knee plate
            VOval(g, k, D, boot, Color.Empty, 0, f.X + s * 0.008f, f.Y + 0.022f, 0.086f, 0.044f);
        }

        static void PersonaClothes(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindErenSd:
                case KindLeviSd:
                {
                    // The white shirt under the short light brown jacket, the leather straps over both, the gear at each
                    // hip and the wings on the jacket's breast.
                    VRound(g, k, D, ScWhite, ScWhiteShade, 30, -0.1f, 0.07f, 0.2f, 0.17f, 0.04f);
                    foreach (int s in Sides)
                    {
                        VRound(g, k, D, Color.FromArgb(150, 154, 162), Color.FromArgb(104, 108, 118), 30, s * 0.132f - 0.03f, 0.17f, 0.06f, 0.075f, 0.016f);
                        VPoly(g, k, D, ScBrown, ScBrownShade, 30, s * 0.022f, 0.072f, s * 0.112f, 0.07f, s * 0.118f, 0.19f, s * 0.04f, 0.19f, s * 0.03f, 0.13f);
                        TnLine(g, ScStrap, 0.013f, D, s * 0.062f, 0.075f, s * 0.072f, 0.19f, s * 0.05f, 0.255f);
                    }
                    TnLine(g, ScStrap, 0.016f, D, -0.1f, 0.205f, 0.1f, 0.205f);
                    VRound(g, k, D, Color.FromArgb(214, 196, 150), Color.Empty, 0, -0.016f, 0.196f, 0.032f, 0.02f, 0.005f);
                    // The wings of freedom: a small shield, the blue wing over the white one.
                    VPoly(g, k, D, Color.FromArgb(250, 250, 248), Color.Empty, 0, -0.098f, 0.11f, -0.058f, 0.11f, -0.058f, 0.14f, -0.078f, 0.155f, -0.098f, 0.14f);
                    using (GraphicsPath w = KPoly(D, -0.078f, 0.113f, -0.06f, 0.113f, -0.06f, 0.139f, -0.078f, 0.152f)) FillSolid(g, w, Color.FromArgb(60, 96, 170));
                    if (k == KindLeviSd)
                    {
                        // His white cravat at the throat.
                        VBlob(g, k, D, ScWhite, ScWhiteShade, 0, 0.5f, -0.042f, 0.072f, 0.042f, 0.072f, 0.034f, 0.11f, 0.014f, 0.15f, -0.014f, 0.15f, -0.034f, 0.11f);
                        TnLine(g, Color.FromArgb(140, 150, 150, 160), 0.006f, D, 0, 0.09f, 0, 0.14f);
                    }
                    break;
                }
                case KindEdwardSd:
                    // The black jacket with its white trim, and the red coat open over it, flaring to his knees.
                    VRound(g, k, D, EdBlack, EdBlackShade, 30, -0.1f, 0.07f, 0.2f, 0.17f, 0.04f);
                    TnLine(g, Color.FromArgb(232, 232, 236), 0.008f, D, -0.03f, 0.075f, 0, 0.1f, 0.03f, 0.075f);
                    TnLine(g, Color.FromArgb(232, 232, 236), 0.006f, D, 0, 0.1f, 0, 0.24f);
                    foreach (int s in Sides)
                        VPoly(g, k, D, EdRed, EdRedShade, 30, s * 0.035f, 0.07f, s * 0.118f, 0.072f, s * 0.15f, 0.2f, s * 0.165f, 0.305f, s * 0.07f, 0.29f, s * 0.045f, 0.16f);
                    break;
                case KindGojoSd:
                    // The black uniform: the high collar up to his chin, the zip down the front.
                    VRound(g, k, D, GjBlack, GjBlackShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, GjBlack, GjBlackShade, 40, -0.075f, 0.05f, 0.15f, 0.06f, 0.025f);
                    TnLine(g, Color.FromArgb(150, 160, 170, 196), 0.006f, D, 0, 0.06f, 0, 0.24f);
                    break;
                case KindSaitamaSd:
                    // The yellow suit with its zip, the black belt, and the cape's two black clasps at his collar.
                    VRound(g, k, D, StYellow, StYellowShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    TnLine(g, Color.FromArgb(150, 170, 120, 20), 0.006f, D, 0, 0.08f, 0, 0.19f);
                    VRound(g, k, D, Color.FromArgb(40, 40, 44), Color.Empty, 0, -0.108f, 0.19f, 0.216f, 0.032f, 0.01f);
                    VOval(g, k, D, Color.FromArgb(40, 40, 44), Color.Empty, 0, 0, 0.206f, 0.04f, 0.04f);
                    foreach (int s in Sides) VOval(g, k, D, Color.FromArgb(36, 36, 40), Color.Empty, 0, s * 0.05f, 0.085f, 0.036f, 0.036f);
                    break;
                case KindKratosSd:
                    // His bare ashen chest with the red tattoo down the side, the leather strap across it, and the red skirt
                    // under the belt.
                    VRound(g, k, D, KrSkin, KrSkinShade, 30, -0.106f, 0.07f, 0.212f, 0.17f, 0.045f);
                    using (GraphicsPath t = KPoly(D, 0.03f, 0.07f, 0.075f, 0.07f, 0.06f, 0.24f, 0.015f, 0.24f)) FillSolid(g, t, KrRed);
                    TnLine(g, Color.FromArgb(110, 76, 50), 0.022f, D, -0.09f, 0.08f, 0, 0.15f, 0.09f, 0.225f);
                    VPoly(g, k, D, Color.FromArgb(150, 34, 34), Color.FromArgb(104, 20, 22), 30, -0.115f, 0.2f, 0.115f, 0.2f, 0.135f, 0.29f, 0.04f, 0.27f, 0, 0.3f, -0.04f, 0.27f, -0.135f, 0.29f);
                    VRound(g, k, D, Color.FromArgb(110, 76, 50), Color.Empty, 0, -0.112f, 0.19f, 0.224f, 0.03f, 0.01f);
                    break;
                case KindDoomSd:
                    // The green armour: the chest plate, the darker plates under it and the belt with its pouches.
                    VRound(g, k, D, DmDark, Darker(DmDark, 0.3), 30, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VBlob(g, k, D, DmGreen, DmGreenShade, 30, 0.4f, -0.1f, 0.075f, 0.1f, 0.075f, 0.105f, 0.15f, 0.05f, 0.18f, -0.05f, 0.18f, -0.105f, 0.15f);
                    VRound(g, k, D, Color.FromArgb(52, 50, 44), Color.Empty, 0, -0.108f, 0.2f, 0.216f, 0.032f, 0.01f);
                    foreach (int s in Sides) VRound(g, k, D, Color.FromArgb(84, 90, 62), Color.Empty, 0, s * 0.06f - 0.022f, 0.196f, 0.044f, 0.04f, 0.008f);
                    break;
                case KindSpiderSd:
                {
                    // The red middle of his suit with blue sides, the black spider on his chest.
                    using (GraphicsPath t = Theme.Round(new RectangleF(-0.106f * D, 0.07f * D, 0.212f * D, 0.19f * D), 0.045f * D))
                    {
                        VFill(g, k, t, SpBlue, SpBlueShade, D, 40);
                        Region old = g.Clip;
                        g.SetClip(t, CombineMode.Intersect);
                        using (GraphicsPath m = KPoly(D, -0.075f, 0.05f, 0.075f, 0.05f, 0.05f, 0.27f, -0.05f, 0.27f))
                        {
                            Stroke(g, m, TnInk(k), TnW(D) * 2);
                            VPaint(g, m, HeroRed, HeroRedShade, D, 30);
                        }
                        g.Clip = old;
                        old.Dispose();
                    }
                    Ellipse(g, Color.FromArgb(24, 20, 24), -0.012f * D, 0.115f * D, 0.024f * D, 0.04f * D);
                    using (Pen leg = new Pen(Color.FromArgb(24, 20, 24), Math.Max(0.8f, D * 0.006f)))
                        foreach (int s in Sides)
                            for (int i = 0; i < 4; i++)
                                g.DrawLines(leg, new PointF[] { P(s * 0.008f * D, (0.125f + i * 0.008f) * D), P(s * 0.03f * D, (0.11f + i * 0.014f) * D), P(s * 0.036f * D, (0.12f + i * 0.02f) * D) });
                    break;
                }
                case KindBatSd:
                    // The grey suit, the black bat on its yellow oval, the yellow belt and the dark trunks.
                    VRound(g, k, D, BtGrey, BtGreyShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VOval(g, k, D, StYellow, StYellowShade, 0, 0, 0.13f, 0.12f, 0.065f);
                    using (GraphicsPath bat = KPoly(D, -0.05f, 0.122f, -0.025f, 0.12f, -0.012f, 0.108f, -0.006f, 0.118f, 0.006f, 0.118f, 0.012f, 0.108f, 0.025f, 0.12f,
                                                  0.05f, 0.122f, 0.04f, 0.135f, 0.018f, 0.138f, 0, 0.152f, -0.018f, 0.138f, -0.04f, 0.135f))
                        FillSolid(g, bat, Color.FromArgb(26, 26, 32));
                    VRound(g, k, D, BtCowl, Color.Empty, 0, -0.1f, 0.215f, 0.2f, 0.05f, 0.016f);
                    VRound(g, k, D, StYellow, StYellowShade, 0, -0.108f, 0.19f, 0.216f, 0.03f, 0.01f);
                    break;
                case KindFlashSd:
                    // The red suit, the lightning bolt on a white circle on his chest, the yellow belt.
                    VRound(g, k, D, HeroRed, HeroRedShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VOval(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(206, 210, 222), 0, 0, 0.13f, 0.075f, 0.075f);
                    using (GraphicsPath bolt = KPoly(D, 0.012f, 0.1f, -0.022f, 0.135f, -0.002f, 0.135f, -0.014f, 0.162f, 0.022f, 0.124f, 0.002f, 0.124f))
                        FillSolid(g, bolt, Color.FromArgb(240, 190, 30));
                    VRound(g, k, D, StYellow, StYellowShade, 0, -0.108f, 0.2f, 0.216f, 0.026f, 0.01f);
                    break;
                case KindSuperSd:
                    // The blue suit, the shield on his chest (red edge, yellow inside, the red S), the yellow belt over the
                    // red trunks.
                    VRound(g, k, D, SmBlue, SmBlueShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, HeroRed, HeroRedShade, 0, -0.1f, 0.205f, 0.2f, 0.055f, 0.018f);
                    VRound(g, k, D, StYellow, StYellowShade, 0, -0.108f, 0.195f, 0.216f, 0.022f, 0.008f);
                    VPoly(g, k, D, HeroRed, HeroRedShade, 0, -0.062f, 0.098f, 0.062f, 0.098f, 0.078f, 0.115f, 0, 0.185f, -0.078f, 0.115f);
                    using (GraphicsPath y = KPoly(D, -0.046f, 0.108f, 0.046f, 0.108f, 0.058f, 0.118f, 0, 0.17f, -0.058f, 0.118f)) FillSolid(g, y, StYellow);
                    TnLine(g, HeroRed, 0.012f, D, 0.032f, 0.118f, -0.02f, 0.12f, 0.016f, 0.14f, -0.024f, 0.152f);
                    break;
                case KindIronSd:
                    // The red chest with the gold plates under it and the light in the middle of his chest.
                    VRound(g, k, D, ImRed, ImRedShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, ImGold, ImGoldShade, 30, -0.07f, 0.17f, 0.14f, 0.075f, 0.02f);
                    TnLine(g, Color.FromArgb(150, 120, 70, 10), 0.006f, D, -0.07f, 0.207f, 0.07f, 0.207f);
                    Glow(g, 0, 0.12f * D, 0.09f * D, 0.09f * D, Color.FromArgb(150, 230, 255), 200);
                    VOval(g, k, D, Color.FromArgb(214, 248, 255), Color.FromArgb(120, 200, 230), 0, 0, 0.12f, 0.045f, 0.045f);
                    break;
                case KindKnightSd:
                    // The steel breastplate, the red tabard over it with its gold cross, the belt.
                    VRound(g, k, D, KnSteel, KnSteelShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, KnRed, KnRedShade, 30, -0.07f, 0.085f, 0.14f, 0.2f, 0.02f);
                    TnLine(g, Color.FromArgb(246, 200, 64), 0.016f, D, 0, 0.11f, 0, 0.19f);
                    TnLine(g, Color.FromArgb(246, 200, 64), 0.016f, D, -0.035f, 0.14f, 0.035f, 0.14f);
                    VRound(g, k, D, Color.FromArgb(110, 76, 50), Color.Empty, 0, -0.108f, 0.2f, 0.216f, 0.026f, 0.01f);
                    break;
                case KindAstroSd:
                    // The white suit, the ring of the helmet's collar, the panel on his chest with its buttons.
                    VRound(g, k, D, AsWhite, AsWhiteShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, Color.FromArgb(206, 212, 226), Color.FromArgb(160, 166, 184), 0, -0.09f, 0.055f, 0.18f, 0.035f, 0.016f);
                    VRound(g, k, D, Color.FromArgb(196, 202, 218), Color.FromArgb(150, 156, 176), 0, -0.05f, 0.115f, 0.1f, 0.065f, 0.012f);
                    Color[] knob = { Color.FromArgb(226, 60, 60), Color.FromArgb(70, 140, 236), Color.FromArgb(250, 204, 60) };
                    for (int i = 0; i < 3; i++) Ellipse(g, knob[i], (-0.035f + i * 0.026f) * D, 0.135f * D, 0.018f * D, 0.018f * D);
                    break;
                case KindWizardSd:
                    // The long purple robe flaring to his feet, its rope belt, a few stars.
                    VBlob(g, k, D, WzPurple, WzPurpleShade, 30, 0.35f, -0.1f, 0.07f, 0.1f, 0.07f, 0.14f, 0.2f, 0.165f, 0.32f, 0, 0.33f, -0.165f, 0.32f, -0.14f, 0.2f);
                    TnLine(g, Color.FromArgb(240, 222, 170), 0.014f, D, -0.12f, 0.2f, 0, 0.21f, 0.12f, 0.2f);
                    foreach (float[] st in new float[][] { new float[] { -0.08f, 0.26f }, new float[] { 0.09f, 0.28f }, new float[] { 0.05f, 0.12f } })
                        using (GraphicsPath s = Star(D, st[0], st[1], 0.018f, 0.008f)) FillSolid(g, s, Color.FromArgb(252, 222, 80));
                    break;
                default:
                {
                    // The superhero: the purple suit with the gold star on his chest, the yellow belt over red trunks.
                    VRound(g, k, D, HrPurple, HrPurpleShade, 40, -0.106f, 0.07f, 0.212f, 0.18f, 0.045f);
                    VRound(g, k, D, HeroRed, HeroRedShade, 0, -0.1f, 0.205f, 0.2f, 0.055f, 0.018f);
                    VRound(g, k, D, StYellow, StYellowShade, 0, -0.108f, 0.195f, 0.216f, 0.022f, 0.008f);
                    using (GraphicsPath s = Star(D, 0, 0.135f, 0.045f, 0.019f)) VFill(g, k, s, Color.FromArgb(252, 214, 60), Color.FromArgb(214, 160, 26), D, 0);
                    break;
                }
            }
        }

        static void PersonaArm(Graphics g, float D, int k, int side, PointF h)
        {
            if (k >= KindCapSd) { AvArm(g, D, k, side, h); return; }
            PointF a = P(side * 0.1f, 0.115f);
            Color sleeve, skin = k == KindKratosSd ? KrSkin : NjSkin;
            float reach = 0.82f;
            switch (k)
            {
                case KindEdwardSd: VLimb(g, k, D, EdRed, 0.062f, a, Lerp(a, h, 0.86f), side * 0.03f); return;
                case KindWizardSd:
                    VLimb(g, k, D, skin, 0.046f, a, h, side * 0.03f);
                    VLimb(g, k, D, WzPurple, 0.066f, a, Lerp(a, h, 0.84f), side * 0.03f);
                    return;
                case KindErenSd:
                case KindLeviSd: sleeve = ScBrown; break;
                case KindGojoSd: sleeve = GjBlack; break;
                case KindSaitamaSd: sleeve = StYellow; break;
                case KindKratosSd:
                    // Bare arms, a bracer on each forearm.
                    VLimb(g, k, D, KrSkin, 0.05f, a, h, side * 0.03f);
                    VLimb(g, k, D, Color.FromArgb(170, 172, 178), 0.058f, Lerp(a, h, 0.55f), Lerp(a, h, 0.85f), 0);
                    return;
                case KindDoomSd:
                    VLimb(g, k, D, DmDark, 0.054f, a, h, side * 0.03f);
                    VLimb(g, k, D, DmGreen, 0.064f, a, Lerp(a, h, 0.45f), side * 0.03f);
                    return;
                case KindKnightSd:
                    VLimb(g, k, D, KnSteel, 0.056f, a, h, side * 0.03f);
                    VOval(g, k, D, KnSteel, KnSteelShade, 0, a.X + side * 0.012f, a.Y + 0.004f, 0.074f, 0.064f);   // the pauldron
                    return;
                case KindSpiderSd:
                case KindFlashSd: sleeve = HeroRed; reach = 1; break;
                case KindBatSd: sleeve = BtGrey; reach = 1; break;
                case KindSuperSd: sleeve = SmBlue; break;
                case KindIronSd: sleeve = ImRed; reach = 1; break;
                case KindAstroSd: sleeve = AsWhite; reach = 1; break;
                default: sleeve = HrPurple; reach = 1; break;
            }
            if (reach < 1) VLimb(g, k, D, skin, 0.046f, a, h, side * 0.03f);
            VLimb(g, k, D, sleeve, 0.056f, a, Lerp(a, h, reach), side * 0.03f);
        }

        static void PersonaHand(Graphics g, float D, int k)
        {
            if (k >= KindCapSd) { AvHand(g, D, k); return; }
            Color c;
            switch (k)
            {
                case KindEdwardSd:
                case KindAstroSd: c = Color.FromArgb(250, 250, 248); break;   // white gloves
                case KindSaitamaSd:
                case KindSpiderSd:
                case KindFlashSd:
                case KindIronSd:
                case KindHeroSd: c = HeroRed; break;
                case KindBatSd: c = BtCowl; break;
                case KindDoomSd: c = Color.FromArgb(52, 54, 52); break;
                case KindKratosSd: c = KrSkin; break;
                case KindKnightSd: c = KnSteel; break;
                default: c = NjSkin; break;
            }
            VOval(g, k, D, c, Color.Empty, 0, 0, 0, 0.058f, 0.054f);
        }

        // Brows and marks (f: the eyes' height): the superhero's mask goes round his eyes, open over them.
        static void PersonaDetails(Graphics g, int k, float D, float f)
        {
            if (k >= KindCapSd) { AvDetails(g, k, D, f); return; }
            switch (k)
            {
                case KindErenSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 70, 46, 32), 0.016f, D, s * 0.045f, f - 0.082f, s * 0.1f, f - 0.105f, s * 0.165f, f - 0.125f);
                    break;
                case KindEdwardSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 196, 140, 34), 0.014f, D, s * 0.045f, f - 0.085f, s * 0.1f, f - 0.108f, s * 0.162f, f - 0.122f);
                    break;
                case KindLeviSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 30, 30, 36), 0.011f, D, s * 0.05f, f - 0.075f, s * 0.11f, f - 0.083f, s * 0.165f, f - 0.088f);
                    break;
                case KindSuperSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(240, 26, 28, 40), 0.014f, D, s * 0.05f, f - 0.09f, s * 0.11f, f - 0.112f, s * 0.165f, f - 0.105f);
                    break;
                case KindAstroSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(220, 96, 64, 42), 0.01f, D, s * 0.07f, f - 0.1f, s * 0.12f, f - 0.112f, s * 0.165f, f - 0.104f);
                    break;
                case KindWizardSd:
                    // Big bushy white brows.
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(120, 150, 150, 166), 0.03f, D, s * 0.045f, f - 0.07f, s * 0.11f, f - 0.1f, s * 0.19f, f - 0.085f);
                        TnLine(g, WzBeard, 0.024f, D, s * 0.045f, f - 0.072f, s * 0.11f, f - 0.102f, s * 0.19f, f - 0.087f);
                    }
                    break;
                case KindHeroSd:
                {
                    // His brows and the mask, drawn once and kept (they move with his face).
                    DsCached(g, D, TnId(k, 7), new RectangleF(-0.3f, f - 0.15f, 0.6f, 0.26f), bg =>
                    {
                        foreach (int s in Sides) TnLine(bg, Color.FromArgb(240, 40, 34, 30), 0.014f, D, s * 0.05f, f - 0.105f, s * 0.11f, f - 0.125f, s * 0.165f, f - 0.118f);
                        using (GraphicsPath m = KCurve(D, 0.45f, -0.25f, f - 0.06f, -0.16f, f - 0.105f, 0, f - 0.075f, 0.16f, f - 0.105f, 0.25f, f - 0.06f, 0.23f, f + 0.05f,
                                                       0.12f, f + 0.075f, 0, f + 0.035f, -0.12f, f + 0.075f, -0.23f, f + 0.05f))
                        {
                            foreach (int s in Sides) m.AddEllipse((s * 0.112f - 0.064f) * D, (f - 0.07f) * D, 0.128f * D, 0.148f * D);
                            m.FillMode = FillMode.Alternate;
                            Stroke(bg, m, TnInk(k), TnW(D) * 1.6f);
                            FillSolid(bg, m, Color.FromArgb(118, 70, 196));
                        }
                    });
                    break;
                }
                case KindKratosSd:
                    // Heavy angry brows, and the scar over his right eye.
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(250, 60, 40, 30), 0.02f, D, s * 0.04f, f - 0.06f, s * 0.1f, f - 0.088f, s * 0.168f, f - 0.105f);
                    TnLine(g, Color.FromArgb(200, 150, 100, 96), 0.008f, D, -0.13f, f - 0.13f, -0.118f, f - 0.06f);
                    break;
                case KindSaitamaSd:
                    foreach (int s in Sides) TnLine(g, Color.FromArgb(160, 150, 110, 80), 0.008f, D, s * 0.08f, f - 0.07f, s * 0.14f, f - 0.072f);
                    break;
            }
        }

        // In front of the face, not turning with it: Gojo's blindfold, wound round his head under the hair; the
        // astronaut's glass helmet, with its shine.
        static void PersonaFront(Graphics g, int k, float D)
        {
            if (k == KindGojoSd)
            {
                using (GraphicsPath b = KCurve(D, 0.45f, -0.3f, -0.128f, 0, -0.142f, 0.3f, -0.128f, 0.305f, 0, 0, 0.012f, -0.305f, 0))
                {
                    Stroke(g, b, TnInk(k), TnW(D) * 2);
                    VPaint(g, b, Color.FromArgb(32, 32, 40), Color.FromArgb(16, 16, 20), D, 30);
                }
                return;
            }
            if (k != KindAstroSd) return;
            RectangleF r = new RectangleF(-0.37f * D, -0.5f * D, 0.74f * D, 0.6f * D);
            using (GraphicsPath glass = new GraphicsPath())
            {
                glass.AddEllipse(r);
                FillSolid(g, glass, Color.FromArgb(34, 170, 210, 255));
                Region old = g.Clip;
                g.SetClip(glass, CombineMode.Intersect);
                using (Pen shine = new Pen(Color.FromArgb(170, 255, 255, 255), 0.03f * D))
                {
                    shine.StartCap = shine.EndCap = LineCap.Round;
                    g.DrawArc(shine, r.X + 0.05f * D, r.Y + 0.04f * D, r.Width - 0.1f * D, r.Height - 0.08f * D, 200, 50);
                }
                Ellipse(g, Color.FromArgb(150, 255, 255, 255), 0.2f * D, -0.38f * D, 0.035f * D, 0.035f * D);
                g.Clip = old;
                old.Dispose();
                Stroke(g, glass, Color.FromArgb(200, 226, 236, 250), Math.Max(1.2f, D * 0.012f));
                Stroke(g, glass, Color.FromArgb(120, 60, 70, 90), Math.Max(0.8f, D * 0.005f));
            }
        }

        static bool HasPersonaFront(int k) { return k == KindGojoSd || k == KindAstroSd; }

        static RectangleF PersonaFrontArea(int k)
        {
            return k == KindAstroSd ? new RectangleF(-0.42f, -0.55f, 0.84f, 0.7f) : new RectangleF(-0.34f, -0.2f, 0.68f, 0.26f);
        }

        static string[] PersonaLines(int k)
        {
            if (k >= KindCapSd) return AvLines(k);
            if (!I18n.IsSpanish) return PersonaLinesEn(k);
            switch (k)
            {
                case KindEdwardSd: return new string[] { "\u00A1No me llames enano!", "\u00A1Soy el Alquimista de Acero!", "Para obtener algo, hay que dar algo a cambio.", "\u00A1Al, vamos!" };
                case KindErenSd: return new string[] { "\u00A1Los exterminar\u00E9 a todos!", "Sigue avanzando.", "\u00A1Entregad vuestros corazones!", "Si no luchas, no puedes ganar." };
                case KindLeviSd: return new string[] { "Tch.", "Esto sigue sucio.", "Elige lo que menos vayas a lamentar.", "Nadie sabe c\u00F3mo acabar\u00E1 esto." };
                case KindGojoSd: return new string[] { "Tranquilo, soy el m\u00E1s fuerte.", "Expansi\u00F3n de dominio: Vac\u00EDo Infinito.", "\u00BFPreocupado? Estoy yo aqu\u00ED.", "Esto se pone divertido." };
                case KindSaitamaSd: return new string[] { "Ok.", "Vaya, otra vez de un pu\u00F1etazo.", "Hoy hay ofertas en el s\u00FAper.", "Soy un h\u00E9roe por afici\u00F3n." };
                case KindKratosSd: return new string[] { "\u00A1Chico!", "No seas blando.", "Mant\u00E9n la calma.", "Lo haremos juntos." };
                case KindDoomSd: return new string[] { "\u2026", "Despedazar y destrozar.", "Ellos tienen miedo. Y deber\u00EDan.", "Hasta que no quede ninguno." };
                case KindSpiderSd: return new string[] { "Un gran poder conlleva una gran responsabilidad.", "\u00A1Tu amigo y vecino Spider-Man!", "\u00A1Mi sentido ar\u00E1cnido me avisa!", "\u00A1Thwip!" };
                case KindBatSd: return new string[] { "Soy Batman.", "Soy la noche.", "La justicia no descansa.", "Gotham me necesita." };
                case KindFlashSd: return new string[] { "\u00A1Soy el hombre m\u00E1s r\u00E1pido del mundo!", "Llego tarde\u2026 otra vez.", "\u00A1Corre, Barry, corre!", "\u00BFHas parpadeado? Ya he vuelto." };
                case KindSuperSd: return new string[] { "\u00A1Arriba, arriba y lejos!", "Esto es trabajo para Superman.", "La esperanza nos hace fuertes.", "\u00BFEs un p\u00E1jaro? \u00BFEs un avi\u00F3n?" };
                case KindIronSd: return new string[] { "Yo soy Iron Man.", "Genio, millonario, playboy, fil\u00E1ntropo.", "JARVIS, \u00BFest\u00E1s ah\u00ED?", "Te quiero tres mil." };
                case KindKnightSd: return new string[] { "\u00A1Por el honor!", "\u00A1En guardia!", "Esta armadura pesa un mont\u00F3n.", "Juro proteger este escritorio." };
                case KindAstroSd: return new string[] { "Houston, tenemos un problema.", "Un peque\u00F1o paso para m\u00ED\u2026", "\u00A1Rumbo a las estrellas!", "Aqu\u00ED no hay gravedad." };
                case KindWizardSd: return new string[] { "Un mago nunca llega tarde.", "Abracadabra.", "La sabidur\u00EDa llega con los a\u00F1os.", "\u00BFD\u00F3nde habr\u00E9 dejado mi varita?" };
                default: return new string[] { "\u00A1A salvar el d\u00EDa!", "\u00A1Que no cunda el p\u00E1nico!", "Mi superpoder: capturar la pantalla.", "\u00A1Justicia para todos!" };
            }
        }

        static string[] PersonaLinesEn(int k)
        {
            switch (k)
            {
                case KindEdwardSd: return new string[] { "Don't call me short!", "I'm the Fullmetal Alchemist!", "To obtain something, something of equal value must be lost.", "Al, let's go!" };
                case KindErenSd: return new string[] { "I'll wipe out every last one of them!", "Keep moving forward.", "Dedicate your hearts!", "If you don't fight, you can't win." };
                case KindLeviSd: return new string[] { "Tch.", "This is still filthy.", "Choose whatever you'll regret the least.", "No one knows how this will turn out." };
                case KindGojoSd: return new string[] { "Relax, I'm the strongest.", "Domain Expansion: Infinite Void.", "Worried? I'm here.", "Now this is getting fun." };
                case KindSaitamaSd: return new string[] { "OK.", "Huh, one punch again.", "There's a sale at the supermarket today.", "I'm a hero for fun." };
                case KindKratosSd: return new string[] { "Boy!", "Do not be soft.", "Stay calm.", "We will do this together." };
                case KindDoomSd: return new string[] { "\u2026", "Rip and tear.", "They're afraid. And they should be.", "Until none are left." };
                case KindSpiderSd: return new string[] { "With great power comes great responsibility.", "Your friendly neighborhood Spider-Man!", "My spider-sense is tingling!", "Thwip!" };
                case KindBatSd: return new string[] { "I'm Batman.", "I am the night.", "Justice never rests.", "Gotham needs me." };
                case KindFlashSd: return new string[] { "I'm the fastest man alive!", "Late\u2026 again.", "Run, Barry, run!", "Did you blink? I'm already back." };
                case KindSuperSd: return new string[] { "Up, up and away!", "This is a job for Superman.", "Hope makes us strong.", "Is it a bird? Is it a plane?" };
                case KindIronSd: return new string[] { "I am Iron Man.", "Genius, billionaire, playboy, philanthropist.", "JARVIS, you there?", "I love you 3000." };
                case KindKnightSd: return new string[] { "For honor!", "En garde!", "This armor weighs a ton.", "I swear to protect this desktop." };
                case KindAstroSd: return new string[] { "Houston, we have a problem.", "One small step for me\u2026", "To the stars!", "There's no gravity up here." };
                case KindWizardSd: return new string[] { "A wizard is never late.", "Abracadabra.", "Wisdom comes with age.", "Now where did I leave my wand?" };
                default: return new string[] { "Saving the day!", "Don't panic!", "My superpower: capturing the screen.", "Justice for all!" };
            }
        }
    }

    public partial class Mascot
    {
        // Their eyes: Edward's golden and wide, Eren's angry green, Levi's narrow and grey, Superman's and the superhero's
        // blue, the astronaut's big brown ones, the wizard's kind narrow blue ones, Kratos's narrow amber, Saitama's two dots;
        // the masks' lenses (Spider-Man's big white ones, the bat's slits, the Flash's white ones, Iron Man's glowing slits,
        // the knight's lights in his visor); none under Gojo's blindfold or behind the Doom Slayer's visor.
        bool PersonaEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            if (Look.Kind >= MascotParts.KindCapSd) return AvEye(g, x, y, D, blink, surprised);
            float b = surprised ? 1.15f : blink;
            switch (Look.Kind)
            {
                case MascotParts.KindEdwardSd: return NinjaEye(g, x, y, D, blink, surprised, 0.122f, 0.15f, Color.FromArgb(150, 92, 12), Color.FromArgb(252, 202, 74), 0.95f);
                case MascotParts.KindErenSd: return NinjaEye(g, x, y, D, blink, surprised, 0.12f, 0.14f, Color.FromArgb(24, 92, 72), Color.FromArgb(92, 206, 166), 0.84f);
                case MascotParts.KindLeviSd: return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.1f, Color.FromArgb(40, 46, 60), Color.FromArgb(126, 136, 158), 0.52f);
                case MascotParts.KindSuperSd:
                case MascotParts.KindHeroSd: return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.14f, Color.FromArgb(24, 60, 150), Color.FromArgb(96, 156, 244), 0.88f);
                case MascotParts.KindAstroSd: return NinjaEye(g, x, y, D, blink, surprised, 0.124f, 0.155f, Color.FromArgb(84, 52, 30), Color.FromArgb(180, 128, 72), 1f);
                case MascotParts.KindWizardSd: return NinjaEye(g, x, y, D, blink, surprised, 0.1f, 0.09f, Color.FromArgb(40, 80, 150), Color.FromArgb(112, 162, 230), 0.6f);
                case MascotParts.KindKratosSd: return NinjaEye(g, x, y, D, blink, surprised, 0.11f, 0.11f, Color.FromArgb(120, 60, 20), Color.FromArgb(226, 156, 64), 0.6f);
                case MascotParts.KindSaitamaSd: return DotEye(g, x, y, D, 0.026f * D, 0.032f * D, blink);
                case MascotParts.KindSpiderSd: LensEye(g, x, y + 0.01f * D, D, b, 0.15f, 0.17f, 24, Color.FromArgb(250, 250, 252), 0.02f, Color.Empty); return true;
                case MascotParts.KindBatSd: LensEye(g, x, y + 0.01f * D, D, b, 0.11f, 0.05f, 18, Color.FromArgb(250, 250, 252), 0.012f, Color.Empty); return true;
                case MascotParts.KindFlashSd: LensEye(g, x, y + 0.01f * D, D, b, 0.11f, 0.075f, 12, Color.FromArgb(250, 250, 252), 0.012f, Color.Empty); return true;
                case MascotParts.KindIronSd: LensEye(g, x, y + 0.02f * D, D, b, 0.1f, 0.032f, 14, Color.FromArgb(232, 252, 255), 0.008f, Color.FromArgb(120, 220, 255)); return true;
                case MascotParts.KindKnightSd: LensEye(g, x * 0.9f, y, D, b, 0.06f, 0.03f, 0, Color.FromArgb(255, 246, 214), 0, Color.FromArgb(255, 214, 130)); return true;
                default: return true;
            }
        }

        // A mask's lens over an eye: an almond w by h (units of D), its outer corner tilted up by slant degrees, filled and
        // ringed in ink (a glow round it when lit); blinking squeezes it to a line.
        void LensEye(Graphics g, float x, float y, float D, float blink, float w, float h, float slant, Color fill, float rim, Color glow)
        {
            int side = x < 0 ? -1 : 1;
            float hw = w * D / 2, hh = Math.Max(0.06f, Math.Min(1.2f, blink)) * h * D / 2;
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-side * slant);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(new PointF(-hw, 0), new PointF(-hw * 0.5f, -hh * 1.3f), new PointF(hw * 0.5f, -hh * 1.3f), new PointF(hw, 0));
                p.AddBezier(new PointF(hw, 0), new PointF(hw * 0.5f, hh * 1.3f), new PointF(-hw * 0.5f, hh * 1.3f), new PointF(-hw, 0));
                p.CloseFigure();
                if (!glow.IsEmpty) MascotParts.Glow(g, 0, 0, hw * 3.2f, Math.Max(hh, D * 0.02f) * 5, glow, 160);
                MascotParts.FillSolid(g, p, fill);
                if (rim > 0) MascotParts.Stroke(g, p, Color.FromArgb(28, 22, 26), Math.Max(1f, rim * D));
            }
            g.Restore(st);
        }

        // Their mouths: none under a mask or helmet; shut, each its own (Edward's and the Flash's sure grin, Eren's, the
        // bat's and Kratos's frowns, Levi's and Saitama's flat line, Gojo's smirk, the wizard's hidden under his
        // moustache, a little one when he talks). False: the usual one.
        bool PersonaMouth(Graphics g, float D, float mx, float my, Color ink, bool open)
        {
            if (Look.Kind >= MascotParts.KindCapSd) return AvMouth(g, D, mx, my, ink, open);
            int k = Look.Kind;
            if (k == MascotParts.KindSpiderSd || k == MascotParts.KindIronSd || k == MascotParts.KindDoomSd || k == MascotParts.KindKnightSd) return true;
            if (k == MascotParts.KindWizardSd)
            {
                if (open) MascotParts.Ellipse(g, Color.FromArgb(110, 40, 44), mx - 0.025f * D, my + 0.012f * D, 0.05f * D, 0.032f * D);
                return true;
            }
            if (open) return false;
            using (Pen p = new Pen(ink, Math.Max(1.1f, D * 0.012f)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                switch (k)
                {
                    case MascotParts.KindEdwardSd:
                    case MascotParts.KindFlashSd: g.DrawArc(p, mx - 0.06f * D, my - 0.045f * D, 0.12f * D, 0.07f * D, 18, 144); return true;
                    case MascotParts.KindErenSd:
                    case MascotParts.KindBatSd:
                    case MascotParts.KindKratosSd: g.DrawArc(p, mx - 0.032f * D, my - 0.002f * D, 0.064f * D, 0.03f * D, 205, 130); return true;
                    case MascotParts.KindLeviSd:
                    case MascotParts.KindSaitamaSd: g.DrawLine(p, mx - 0.022f * D, my + 0.004f * D, mx + 0.022f * D, my + 0.004f * D); return true;
                    case MascotParts.KindGojoSd: g.DrawBezier(p, mx - 0.05f * D, my - 0.01f * D, mx - 0.02f * D, my + 0.016f * D, mx + 0.03f * D, my + 0.012f * D, mx + 0.055f * D, my - 0.014f * D); return true;
                }
            }
            return false;
        }
    }
}
