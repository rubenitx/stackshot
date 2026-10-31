// Stackshot - Crayon Shin-chan drawn like the 3D models of his games, as the anime has him nearly facing us: his head
// narrow on top and wide at the cheeks, the near cheek bulging out under his hair, the black hair a cap over the top
// and down his far side; the thick bushy eyebrows, the round black eyes close together with a thin line over each, and
// the little mouth low by his chin with his tongue out; the red long-sleeved T-shirt, yellow shorts, white socks and
// yellow shoes, everything lit softly and rounded. And Shin-chan dressed up, as his official figures have him: as
// Ultrahero (the sky blue mask with its big yellow goggles, the fin and the horns; the green top under the white
// collar, the white belt with the yellow A, red shorts, blue legs, red gloves and boots), as Kantam Robot (a green
// helmet with a window for his face and the great V on its brow, the armour, the big fists) and as Waniyama, the
// crocodile of his Chocobi biscuits (the hot pink suit all of one piece, its hood with the big sleepy eyes on top). He
// turns to face the way he walks. Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindShinSd = 106, KindKantamSd = 108, KindUltraSd = 109, KindWaniSd = 111;

        public static bool IsShin(int kind) { return kind == KindShinSd || kind == KindKantamSd || kind == KindUltraSd || kind == KindWaniSd; }

        // Where his eyes are (x, y for each, units of D): the face and the goggles over them both go by them.
        internal static readonly float[] ShinEyes = { -0.03f, -0.365f, 0.107f, -0.377f };

        internal static readonly Color ShSkin = Color.FromArgb(252, 210, 176), ShSkinShade = Color.FromArgb(228, 168, 132);
        static readonly Color ShRed = Color.FromArgb(220, 40, 46), ShRedShade = Color.FromArgb(160, 20, 30);
        static readonly Color ShHair = Color.FromArgb(40, 36, 44);
        static readonly Color UhSky = Color.FromArgb(76, 168, 236), UhSkyShade = Color.FromArgb(40, 116, 196), UhGreen = Color.FromArgb(44, 160, 90), UhGreenShade = Color.FromArgb(24, 112, 58);
        static readonly Color UhRed = Color.FromArgb(224, 40, 44), UhRedShade = Color.FromArgb(160, 22, 30), UhYellow = Color.FromArgb(252, 214, 44);
        static readonly Color KtGreen = Color.FromArgb(56, 146, 86), KtGreenShade = Color.FromArgb(30, 92, 52), KtOrange = Color.FromArgb(244, 126, 42), KtWhite = Color.FromArgb(240, 244, 238);
        static readonly Color WnPink = Color.FromArgb(232, 38, 122), WnPinkShade = Color.FromArgb(170, 18, 86);

        // His head as the anime draws it, turned a little to his left (our right): narrow on top, wide at the cheeks, the
        // round near cheek bulging out under his hair, the chin low on the side he faces.
        static GraphicsPath ShinHead(float D)
        {
            return KCurve(D, 0.5f, 0.03f, -0.597f, 0.115f, -0.58f, 0.172f, -0.545f, 0.205f, -0.5f, 0.225f, -0.46f, 0.265f, -0.418f, 0.303f, -0.37f,
                          0.322f, -0.31f, 0.317f, -0.252f, 0.287f, -0.2f, 0.232f, -0.166f, 0.162f, -0.152f, 0.075f, -0.162f, -0.04f, -0.181f, -0.15f, -0.203f,
                          -0.228f, -0.216f, -0.278f, -0.235f, -0.302f, -0.275f, -0.298f, -0.33f, -0.282f, -0.372f, -0.255f, -0.396f, -0.228f, -0.405f,
                          -0.222f, -0.44f, -0.208f, -0.485f, -0.175f, -0.533f, -0.12f, -0.567f, -0.05f, -0.589f);
        }

        // His hair: a cap over the top of his head, its edge high across his forehead, down his far side to a point on
        // top of the cheek and ending at the near side over his ear.
        static readonly float[] ShinHairEdge =
        {
            -0.182f, -0.362f, -0.17f, -0.405f, -0.155f, -0.455f, -0.135f, -0.502f, -0.105f, -0.536f, -0.05f, -0.551f, 0.03f, -0.556f, 0.11f, -0.554f,
            0.16f, -0.54f, 0.192f, -0.516f, 0.218f, -0.485f, 0.27f, -0.48f, 0.3f, -0.62f, 0.1f, -0.7f, -0.2f, -0.68f, -0.34f, -0.5f, -0.34f, -0.42f,
            -0.26f, -0.412f, -0.215f, -0.392f
        };

        // The window a helmet or hood leaves for his face (centre and radii).
        static RectangleF ShinWindow(float D, float grow)
        {
            float cx = 0.045f, cy = -0.355f, rx = 0.235f + grow, ry = 0.198f + grow;
            return new RectangleF((cx - rx) * D, (cy - ry) * D, rx * 2 * D, ry * 2 * D);
        }

        // The head, kept: the face, then his hair or what he wears on his head dressed up.
        static void ShinHeadDraw(Graphics g, int k, GraphicsPath head, float D)
        {
            if (k == KindUltraSd)
            {
                // The mask's horns, up and out at the top of his head, and the thin red fin standing on top of it.
                VPoly(g, k, D, UhSky, UhSkyShade, 0, 0.145f, -0.55f, 0.335f, -0.68f, 0.235f, -0.46f);
                VPoly(g, k, D, UhSky, UhSkyShade, 0, -0.115f, -0.55f, -0.305f, -0.68f, -0.205f, -0.46f);
                VBlob(g, k, D, UhRed, UhRedShade, 0, 0.45f, 0, -0.55f, 0.008f, -0.66f, 0.035f, -0.74f, 0.068f, -0.778f, 0.074f, -0.72f, 0.064f, -0.65f, 0.07f, -0.55f);
            }
            VFill(g, k, head, ShSkin, ShSkinShade, D, 0);
            Region old = g.Clip;
            switch (k)
            {
                case KindShinSd:
                    g.SetClip(head, CombineMode.Intersect);
                    using (GraphicsPath h = KCurve(D, 0.35f, ShinHairEdge))
                    {
                        Stroke(g, h, TnInk(k), TnW(D, k) * 2);
                        SoftPaint(g, h, ShHair, Color.FromArgb(14, 12, 16), D, 26);
                    }
                    g.Clip = old;
                    break;
                case KindUltraSd:
                {
                    // The mask: over his head and down past his eyes, coming to a point over his nose; on it the big yellow
                    // goggles round his eyes, wider on the outside, meeting over his nose.
                    g.SetClip(head, CombineMode.Intersect);
                    using (GraphicsPath mask = KPoly(D, -0.45f, -0.75f, 0.45f, -0.75f, 0.45f, -0.29f, 0.25f, -0.314f, 0.18f, -0.326f, 0.12f, -0.322f, 0.075f, -0.298f,
                                                       0.04f, -0.252f, 0.005f, -0.294f, -0.05f, -0.314f, -0.11f, -0.316f, -0.2f, -0.302f, -0.45f, -0.28f))
                    {
                        Stroke(g, mask, TnInk(k), TnW(D, k) * 2);
                        SoftPaint(g, mask, UhSky, UhSkyShade, D);
                    }
                    g.Clip = old;
                    for (int i = 0; i < 2; i++)
                    {
                        float cx = ShinEyes[i * 2], cy = ShinEyes[i * 2 + 1], m = i == 0 ? -1 : 1;
                        VBlob(g, k, D, UhYellow, Color.FromArgb(226, 176, 20), 0, 0.5f, cx + m * 0.085f, cy - 0.03f, cx + m * 0.066f, cy - 0.072f, cx + m * 0.01f, cy - 0.085f,
                              cx - m * 0.045f, cy - 0.07f, cx - m * 0.07f, cy - 0.025f, cx - m * 0.055f, cy + 0.03f, cx, cy + 0.05f, cx + m * 0.058f, cy + 0.038f);
                    }
                    break;
                }
                case KindKantamSd:
                case KindWaniSd:
                {
                    // A helmet (the robot's) or a hood (the crocodile's) over his whole head, a window for his face.
                    bool robot = k == KindKantamSd;
                    Color c = robot ? KtGreen : WnPink, cs = robot ? KtGreenShade : WnPinkShade;
                    using (GraphicsPath cover = robot ? KCurve(D, 0.5f, 0.03f, -0.665f, 0.2f, -0.635f, 0.315f, -0.54f, 0.355f, -0.4f, 0.345f, -0.27f, 0.285f, -0.165f,
                                                               0.14f, -0.118f, -0.08f, -0.118f, -0.245f, -0.162f, -0.328f, -0.27f, -0.345f, -0.4f, -0.3f, -0.54f, -0.16f, -0.635f)
                                                      : KCurve(D, 0.5f, 0.02f, -0.665f, 0.19f, -0.64f, 0.31f, -0.545f, 0.36f, -0.4f, 0.352f, -0.27f, 0.305f, -0.165f,
                                                               0.17f, -0.098f, 0.02f, -0.082f, -0.13f, -0.098f, -0.27f, -0.165f, -0.33f, -0.27f, -0.345f, -0.4f, -0.3f, -0.545f, -0.16f, -0.64f))
                        VFill(g, k, cover, c, cs, D, 0);
                    using (GraphicsPath win = new GraphicsPath())
                    {
                        win.AddEllipse(ShinWindow(D, 0));
                        g.SetClip(win, CombineMode.Intersect);
                        g.SetClip(head, CombineMode.Intersect);
                        SoftPaint(g, head, ShSkin, ShSkinShade, D);
                        if (!robot) Glow(g, 0.045f * D, -0.54f * D, 0.44f * D, 0.12f * D, Color.Black, 64);   // the hood's shade on his forehead
                        g.Clip = old;
                        if (robot)
                        {
                            // The helmet's hard rim round his face.
                            using (Pen rim = new Pen(Darker(cs, 0.15), Math.Max(1.2f, 0.022f * D))) g.DrawEllipse(rim, ShinWindow(D, 0));
                            using (Pen rim = new Pen(Lighter(c, 0.25), Math.Max(0.8f, 0.007f * D))) g.DrawEllipse(rim, ShinWindow(D, 0.016f));
                        }
                        else
                        {
                            // The hood's soft edge round his face, a fold catching the light.
                            using (Pen fold = new Pen(Color.FromArgb(150, Lighter(c, 0.3)), Math.Max(1f, 0.014f * D))) g.DrawEllipse(fold, ShinWindow(D, 0.014f));
                            using (Pen edge = new Pen(TnInk(k), TnW(D, k) * 2)) g.DrawEllipse(edge, ShinWindow(D, 0));
                        }
                    }
                    if (robot)
                    {
                        // The dark visor across the brow with the robot's yellow eyes, the orange discs over the ears, and
                        // the great V rising from the brow.
                        using (GraphicsPath visor = Theme.Round(new RectangleF(-0.13f * D, -0.605f * D, 0.34f * D, 0.05f * D), 0.016f * D)) VFill(g, k, visor, Color.FromArgb(26, 58, 36), Color.Empty, D, 0);
                        foreach (int s in Sides)
                            using (GraphicsPath eye = KPoly(D, 0.04f + s * 0.03f, -0.585f, 0.04f + s * 0.12f, -0.598f, 0.04f + s * 0.115f, -0.572f, 0.04f + s * 0.035f, -0.568f))
                                FillSolid(g, eye, Color.FromArgb(255, 224, 70));
                        VOval(g, k, D, KtOrange, Color.FromArgb(196, 84, 20), 0, -0.322f, -0.37f, 0.085f, 0.11f);
                        VOval(g, k, D, KtOrange, Color.FromArgb(196, 84, 20), 0, 0.345f, -0.37f, 0.07f, 0.105f);
                        VPoly(g, k, D, KtGreen, KtGreenShade, 0, 0.01f, -0.605f, 0.05f, -0.63f, -0.21f, -0.88f, -0.26f, -0.865f);
                        VPoly(g, k, D, KtGreen, KtGreenShade, 0, 0.03f, -0.63f, 0.07f, -0.605f, 0.35f, -0.86f, 0.3f, -0.88f);
                        VOval(g, k, D, Color.FromArgb(252, 214, 50), Color.FromArgb(220, 170, 20), 0, 0.04f, -0.618f, 0.05f, 0.04f);
                    }
                    else
                        for (int i = 0; i < 2; i++)
                        {
                            // The crocodile's big sleepy eyes on top of the hood: white, looking aside, the lids slanting
                            // down to the middle.
                            float ex = i == 0 ? -0.07f : 0.115f, ey = -0.645f, r = 0.085f;
                            float yl = ey + (i == 0 ? -0.38f : 0.02f) * r, yr = ey + (i == 0 ? 0.02f : -0.38f) * r;   // the lid's edge, left and right
                            using (GraphicsPath ball = new GraphicsPath())
                            {
                                ball.AddEllipse((ex - r) * D, (ey - r) * D, r * 2 * D, r * 2 * D);
                                Stroke(g, ball, TnInk(k), TnW(D, k) * 2);
                                SoftPaint(g, ball, Color.FromArgb(252, 252, 250), Color.FromArgb(200, 204, 216), D);
                                g.SetClip(ball, CombineMode.Intersect);
                                Ellipse(g, Color.FromArgb(52, 48, 58), (ex + r * 0.04f) * D, (ey - r * 0.06f) * D, r * 0.72f * D, r * 0.72f * D);
                                Ellipse(g, Color.FromArgb(220, 255, 255, 255), (ex + r * 0.39f) * D, (ey + r * 0.09f) * D, r * 0.22f * D, r * 0.22f * D);
                                PointF[] lid = { P((ex - r - 0.01f) * D, (ey - r - 0.01f) * D), P((ex + r + 0.01f) * D, (ey - r - 0.01f) * D), P((ex + r + 0.01f) * D, yr * D), P((ex - r - 0.01f) * D, yl * D) };
                                using (SolidBrush lb = new SolidBrush(WnPink)) g.FillPolygon(lb, lid);
                                g.Clip = old;
                            }
                            TnLine(g, TnInk(k), 0.011f, D, ex - r * 0.97f, yl, ex + r * 0.97f, yr);
                        }
                    break;
                }
            }
            old.Dispose();
        }

        // Where a foot is, his front to the right: stepping as he walks, swinging in the air.
        static PointF ShinStep(MascotPose p, int side)
        {
            PointF s = SnoopyStep(p, side);
            return new PointF(s.X * 0.45f, s.Y);
        }

        // Behind his head: his legs and shoes, his clothes (kept), the soft shade of his arms on him, and his arms.
        static void ShinBody(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            foreach (int s in Sides)
            {
                PointF st = ShinStep(p, s), f = P(s * 0.1f + st.X, 0.35f + st.Y);
                switch (k)
                {
                    case KindShinSd:
                        VLimb(g, k, D, ShSkin, 0.056f, P(s * 0.1f, 0.19f), P(f.X, f.Y - 0.04f), 0);
                        VRound(g, k, D, Color.FromArgb(252, 252, 250), Color.FromArgb(206, 210, 222), 0, f.X - 0.033f, f.Y - 0.06f, 0.066f, 0.064f, 0.018f);
                        ShinShoe(g, k, D, s, f, Color.FromArgb(248, 206, 56));
                        break;
                    case KindUltraSd:
                        VLimb(g, k, D, UhSky, 0.074f, P(s * 0.1f, 0.15f), P(f.X, f.Y - 0.02f), 0);
                        VRound(g, k, D, UhRed, UhRedShade, 0, f.X - 0.046f, f.Y - 0.088f, 0.092f, 0.098f, 0.026f);
                        VRound(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(206, 210, 222), 0, f.X - 0.05f, f.Y - 0.096f, 0.1f, 0.026f, 0.01f);
                        ShinShoe(g, k, D, s, f, UhRed);
                        break;
                    case KindKantamSd:
                    {
                        // The robot's legs: thick, an orange joint at the knee, the big dark boots with a pale band.
                        PointF hip = P(s * 0.1f, 0.15f), knee = Lerp(hip, f, 0.45f);
                        VLimb(g, k, D, KtGreen, 0.084f, hip, P(f.X, f.Y - 0.02f), 0);
                        VOval(g, k, D, KtOrange, Color.FromArgb(196, 84, 20), 0, knee.X, knee.Y, 0.05f, 0.044f);
                        VRound(g, k, D, KtGreenShade, Color.FromArgb(20, 64, 36), 0, f.X - 0.058f, f.Y - 0.045f, 0.116f, 0.092f, 0.03f);
                        VRound(g, k, D, KtWhite, Color.FromArgb(200, 210, 204), 0, f.X - 0.056f, f.Y - 0.05f, 0.112f, 0.022f, 0.009f);
                        break;
                    }
                    default:
                        // The crocodile suit's short thick legs and big round feet.
                        VOval(g, k, D, WnPink, WnPinkShade, 0, f.X + s * 0.014f, f.Y + 0.016f, 0.122f, 0.072f);
                        VLimb(g, k, D, WnPink, 0.1f, P(s * 0.1f, 0.17f), P(f.X, f.Y - 0.006f), 0);
                        break;
                }
            }
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.27f, -0.25f, 0.54f, 0.51f), bg => ShinClothes(bg, k, D));
            if (!p.HideHands)
            {
                foreach (int s in Sides)
                {
                    // The soft shade each arm throws on him.
                    PointF h = DsHand(p, geo, s), a = P(s * 0.2f, -0.14f);
                    using (Pen sh = new Pen(Color.FromArgb(46, 0, 0, 0), 0.085f * D))
                    {
                        sh.StartCap = sh.EndCap = LineCap.Round;
                        g.DrawLine(sh, (a.X + 0.012f) * D, (a.Y + 0.02f) * D, (h.X + 0.012f) * D, (h.Y + 0.02f) * D);
                    }
                }
                foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            }
        }

        // A shoe under the sock or boot at f, its toe out to the side a little (the same shoe mirrored on each foot).
        static void ShinShoe(Graphics g, int k, float D, int s, PointF f, Color c)
        {
            float[] o = { -0.036f, -0.008f, 0.03f, -0.012f, 0.07f, 0.014f, 0.066f, 0.044f, -0.012f, 0.05f, -0.046f, 0.032f };
            float[] xy = new float[o.Length];
            for (int i = 0; i < o.Length; i += 2) { xy[i] = f.X + s * o[i]; xy[i + 1] = f.Y + o[i + 1]; }
            VBlob(g, k, D, c, Mac.Mix(c, Color.Black, 0.22), 0, 0.45f, xy);
        }

        // His clothes, kept: shorts with the shade of his top on them, then the top with the shade of his head across it;
        // the crocodile's suit all of one piece, round in the belly.
        static void ShinClothes(Graphics g, int k, float D)
        {
            if (k == KindWaniSd)
            {
                using (GraphicsPath suit = KCurve(D, 0.45f, -0.17f, -0.226f, 0.17f, -0.2f, 0.215f, -0.1f, 0.24f, 0.02f, 0.245f, 0.12f, 0.2f, 0.2f, 0.1f, 0.232f,
                                                  0, 0.215f, -0.1f, 0.232f, -0.2f, 0.2f, -0.245f, 0.12f, -0.24f, 0.02f, -0.215f, -0.1f))
                {
                    VFill(g, k, suit, WnPink, WnPinkShade, D, 0);
                    Region old = g.Clip;
                    g.SetClip(suit, CombineMode.Intersect);
                    Glow(g, 0, -0.19f * D, 0.62f * D, 0.16f * D, Color.Black, 70);
                    g.Clip = old;
                    old.Dispose();
                }
                return;
            }
            Color lo, loShade, top, topShade;
            switch (k)
            {
                case KindUltraSd: lo = UhRed; loShade = UhRedShade; top = UhGreen; topShade = UhGreenShade; break;
                case KindKantamSd: lo = KtGreen; loShade = KtGreenShade; top = KtGreen; topShade = KtGreenShade; break;
                default: lo = Color.FromArgb(252, 236, 100); loShade = Color.FromArgb(220, 190, 50); top = ShRed; topShade = ShRedShade; break;
            }
            float bottom = k == KindUltraSd ? 0.185f : 0.21f;
            using (GraphicsPath s = KCurve(D, 0.25f, -0.208f, 0.05f, 0.208f, 0.05f, 0.226f, bottom - 0.03f, 0.2f, bottom, 0.03f, bottom, 0, bottom - 0.04f, -0.03f, bottom,
                                           -0.2f, bottom, -0.226f, bottom - 0.03f))
            {
                VFill(g, k, s, lo, loShade, D, 0);
                Region old = g.Clip;
                g.SetClip(s, CombineMode.Intersect);
                Glow(g, 0, 0.065f * D, 0.5f * D, 0.07f * D, Color.Black, 70);
                g.Clip = old;
                old.Dispose();
            }
            using (GraphicsPath t = KCurve(D, 0.4f, -0.17f, -0.226f, 0.17f, -0.2f, 0.205f, -0.1f, 0.224f, 0.0f, 0.226f, 0.06f, 0.212f, 0.09f, 0, 0.1f, -0.212f, 0.09f,
                                           -0.226f, 0.06f, -0.224f, 0.0f, -0.21f, -0.1f))
            {
                Stroke(g, t, TnInk(k), TnW(D, k) * 2);
                SoftPaint(g, t, top, topShade, D, 22);   // a faint sheen: the top of it is in the shade of his head
                Region old = g.Clip;
                g.SetClip(t, CombineMode.Intersect);
                Glow(g, 0, -0.19f * D, 0.62f * D, 0.16f * D, Color.Black, 80);
                g.Clip = old;
                old.Dispose();
            }
            if (k == KindUltraSd)
            {
                // The white collar over his shoulders, down in a V to his chest; the white belt and the yellow A on it.
                foreach (int s in Sides)
                    VPoly(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(206, 210, 222), 0, s * 0.215f, -0.19f, s * 0.11f, -0.216f, s * 0.012f, -0.042f,
                          s * 0.05f, -0.026f, s * 0.226f, -0.11f);
                VRound(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(206, 210, 222), 0, -0.222f, 0.03f, 0.444f, 0.05f, 0.014f);
                VOval(g, k, D, UhYellow, Color.FromArgb(226, 176, 20), 0, 0, 0.055f, 0.082f, 0.082f);
                TnLine(g, UhGreenShade, 0.009f, D, -0.02f, 0.078f, 0, 0.032f, 0.02f, 0.078f);
                TnLine(g, UhGreenShade, 0.008f, D, -0.011f, 0.062f, 0.011f, 0.062f);
            }
            else if (k == KindKantamSd)
            {
                // The armour's white chest panel with its vents, the orange lights, and the white ribbed waist.
                VRound(g, k, D, KtWhite, Color.FromArgb(200, 210, 204), 0, -0.1f, -0.12f, 0.2f, 0.085f, 0.014f);
                for (int i = 0; i < 3; i++) TnLine(g, Color.FromArgb(150, 70, 90, 80), 0.008f, D, -0.06f, -0.1f + i * 0.022f, 0.06f, -0.1f + i * 0.022f);
                foreach (int s in Sides) VOval(g, k, D, KtOrange, Color.FromArgb(196, 84, 20), 0, s * 0.158f, -0.08f, 0.055f, 0.055f);
                using (GraphicsPath w = Theme.Round(new RectangleF(-0.214f * D, 0.03f * D, 0.428f * D, 0.055f * D), 0.014f * D))
                {
                    VFill(g, k, w, KtWhite, Color.FromArgb(200, 210, 204), D, 0);
                    TnLine(g, Color.FromArgb(150, 120, 140, 130), 0.005f, D, -0.2f, 0.057f, 0.2f, 0.057f);
                }
            }
        }

        static void ShinArm(Graphics g, float D, int k, int side, PointF h)
        {
            PointF a = P(side * 0.2f, -0.14f);
            switch (k)
            {
                case KindUltraSd:
                    VLimb(g, k, D, UhSky, 0.064f, a, h, side * 0.03f);
                    VLimb(g, k, D, UhRed, 0.072f, Lerp(a, h, 0.5f), h, 0);
                    PointF c = Lerp(a, h, 0.52f);
                    TnLine(g, Color.FromArgb(250, 250, 248), 0.014f, D, c.X - 0.032f, c.Y, c.X + 0.032f, c.Y);
                    break;
                case KindKantamSd:
                    VOval(g, k, D, KtOrange, Color.FromArgb(196, 84, 20), 0, a.X, a.Y + 0.012f, 0.12f, 0.12f);
                    VLimb(g, k, D, KtGreen, 0.072f, a, h, side * 0.03f);
                    break;
                case KindWaniSd: VLimb(g, k, D, WnPink, 0.08f, a, h, side * 0.03f); break;
                default: VLimb(g, k, D, ShRed, 0.078f, a, Lerp(a, h, 0.84f), side * 0.03f); break;   // the long sleeve, down to his hand
            }
        }

        static void ShinHand(Graphics g, float D, int k)
        {
            switch (k)
            {
                case KindUltraSd: VOval(g, k, D, UhRed, UhRedShade, 0, 0, 0, 0.078f, 0.072f); break;
                case KindKantamSd:
                    VRound(g, k, D, KtGreen, KtGreenShade, 0, -0.058f, -0.052f, 0.116f, 0.104f, 0.032f);
                    for (int i = 0; i < 3; i++) TnLine(g, Color.FromArgb(150, 18, 60, 34), 0.007f, D, -0.034f + i * 0.034f, 0.0f, -0.034f + i * 0.034f, 0.045f);
                    break;
                case KindWaniSd: VOval(g, k, D, WnPink, WnPinkShade, 0, 0, 0, 0.08f, 0.074f); break;
                default: VOval(g, k, D, ShSkin, ShSkinShade, 0, 0, 0, 0.066f, 0.062f); break;
            }
        }

        static string[] ShinLines(int k)
        {
            if (!I18n.IsSpanish)
                switch (k)
                {
                    case KindUltraSd: return new string[] { "Ha ha ha ha! I'm Action Kamen!", "Action Beam!", "Justice always wins!", "Action Kamen never loses." };
                    case KindKantamSd: return new string[] { "Kantam Robo, move out!", "Rocket punch!", "Beep, beep\u2026 I'm a robot.", "Final attack!" };
                    case KindWaniSd: return new string[] { "I love Chocobi cookies!", "Nom, nom\u2026 Chocobi.", "I'm a crocodile!", "Grrr\u2026 I'm gonna eat you!" };
                    default: return new string[] { "Do you like green peppers?", "Ha ha ha ha! I'm Action Kamen!", "Mom, I'm hungry!", "Chocobi cookies are the best.", "Shiro, come here!" };
                }
            switch (k)
            {
                case KindUltraSd: return new string[] { "\u00A1Ja, ja, ja, ja! \u00A1Soy Ultrah\u00E9roe!", "\u00A1Rayo de acci\u00F3n!", "\u00A1La justicia siempre triunfa!", "Ultrah\u00E9roe nunca pierde." };
                case KindKantamSd: return new string[] { "\u00A1Robot Kantam, en marcha!", "\u00A1Pu\u00F1o cohete!", "Bip, bip\u2026 soy un robot.", "\u00A1Ataque final!" };
                case KindWaniSd: return new string[] { "\u00A1Me encantan las galletas Chocobi!", "\u00D1am, \u00F1am\u2026 Chocobi.", "\u00A1Soy un cocodrilo!", "Grrr\u2026 \u00A1que te como!" };
                default: return new string[] { "\u00BFTe gustan los pimientos?", "\u00A1Ja, ja, ja, ja! \u00A1Soy Ultrah\u00E9roe!", "\u00A1Mam\u00E1, tengo hambre!", "Las galletas Chocobi son lo mejor.", "\u00A1Nevado, ven aqu\u00ED!" };
            }
        }
    }

    public partial class Mascot
    {
        // Shin-chan's face as the anime draws it: round black eyes close together towards his front, a white shine in each
        // and a thin line arching over it; the thick bushy eyebrows high under his hair (as Ultrahero, frowning on top of
        // his goggles); the little mouth low by his chin, his tongue out.
        bool ShinFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 30), dark = Color.FromArgb(120, 44, 48);
            float lw = Math.Max(1.1f, D * 0.013f), dx = ex * 0.45f, dy = (ey - geo.FaceY * D) * 0.6f;
            float[] eyes = MascotParts.ShinEyes;
            bool surprised = mood == Mood.Surprised, masked = Look.Kind == MascotParts.KindUltraSd;
            // The line over each eye, from the eye's centre: round over its top from its outer side.
            float[][] lids = { new float[] { -0.098f, 0.0f, -0.072f, -0.062f, -0.022f, -0.086f, 0.03f, -0.058f },
                               new float[] { -0.045f, -0.028f, -0.005f, -0.072f, 0.045f, -0.08f, 0.083f, -0.038f } };
            // His eyebrows, thick and bushy, along these points.
            float[][] brows = masked ? new float[][] { new float[] { -0.125f, -0.46f, -0.085f, -0.467f, -0.04f, -0.455f, 0.0f, -0.432f },
                                                       new float[] { 0.075f, -0.446f, 0.115f, -0.47f, 0.16f, -0.477f, 0.198f, -0.46f } }
                                     : new float[][] { new float[] { -0.118f, -0.476f, -0.088f, -0.503f, -0.04f, -0.506f, 0.0f, -0.488f },
                                                       new float[] { 0.072f, -0.492f, 0.104f, -0.514f, 0.145f, -0.511f, 0.176f, -0.488f } };
            using (Pen p = new Pen(ink, lw))
            using (Pen lid = new Pen(Color.FromArgb(100, 60, 50), Math.Max(0.7f, D * 0.0065f)))
            {
                p.StartCap = p.EndCap = lid.StartCap = lid.EndCap = LineCap.Round;
                for (int i = 0; i < 2; i++)
                {
                    float x = eyes[i * 2] * D + dx, y = eyes[i * 2 + 1] * D + dy, r = (surprised ? 0.056f : 0.05f) * D;
                    if (!masked)
                    {
                        PointF[] arc = new PointF[4];
                        for (int j = 0; j < 4; j++) arc[j] = new PointF(x + lids[i][j * 2] * D, y + (lids[i][j * 2 + 1] - (surprised ? 0.012f : 0)) * D);
                        g.DrawCurve(lid, arc, 0.5f);
                    }
                    Mood m = mood == Mood.Wink && i == 1 ? Mood.Sleep : mood;
                    if (m == Mood.Happy || m == Mood.Wink) { g.DrawArc(p, x - r, y - r * 0.5f, r * 2, r * 1.5f, 200, 140); continue; }
                    if (m == Mood.Sleep || m == Mood.Yawn) { g.DrawArc(p, x - r, y - r * 0.8f, r * 2, r * 1.2f, 20, 140); continue; }
                    if (m == Mood.Love) { using (GraphicsPath hp = MascotParts.Heart(x, y, r * 2.4f)) MascotParts.FillSolid(g, hp, LoveColor); continue; }
                    if (m == Mood.Dizzy) { DizzyEye(g, p, x, y, r * 1.1f, i); continue; }
                    float h = r * 1.04f * (surprised ? 1 : blink);
                    if (h < r * 0.3f) { g.DrawLine(p, x - r, y, x + r, y); continue; }
                    MascotParts.Ellipse(g, ink, x - r, y - h, r * 2, h * 2);
                    if (h > r * 0.6f) MascotParts.Ellipse(g, Color.FromArgb(250, 255, 255, 255), x - r * 0.26f, y - h * 0.34f, r * 0.6f, r * 0.6f);
                }
                // The eyebrows, raised when he is surprised.
                float lift = surprised ? -0.026f * D : 0;
                foreach (float[] b in brows)
                {
                    PointF[] pts = new PointF[4];
                    for (int j = 0; j < 4; j++) pts[j] = new PointF(b[j * 2] * D + dx, b[j * 2 + 1] * D + lift);
                    ShinBrow(g, pts, D);
                }
                if (mood == Mood.Love || mood == Mood.Happy)
                    foreach (float[] c in new float[][] { new float[] { -0.225f, -0.27f }, new float[] { 0.25f, -0.255f } })
                        MascotParts.Glow(g, c[0] * D, c[1] * D, 0.09f * D, 0.045f * D, Color.FromArgb(255, 120, 130), 110);
                // The mouth; opened wide it grows up, clear of his chin.
                float mx = 0.135f * D + dx * 0.6f, my = -0.198f * D;
                bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy, smile = mood == Mood.Happy || mood == Mood.Love;
                float open = smile ? 0.07f : surprised ? 0.06f : mood == Mood.Yawn ? 0.075f : mood == Mood.Sleep ? 0 : 0.042f;
                if (talking && mood == Mood.Idle) open *= (float)(0.55 + 0.45 * Math.Abs(Math.Sin(now / 110.0)));
                if (open <= 0)
                {
                    g.DrawArc(p, mx - 0.02f * D, my - 0.01f * D, 0.04f * D, 0.02f * D, 20, 140);
                    return true;
                }
                my -= Math.Max(0, open - 0.042f) * 0.5f * D;
                float w = open * D * (smile ? 1.15f : 0.9f), hh = open * D;
                using (GraphicsPath m = new GraphicsPath())
                {
                    if (smile) { m.AddArc(mx - w / 2, my - hh * 0.6f, w, hh * 1.6f, 0, 180); m.CloseFigure(); }
                    else m.AddEllipse(mx - w / 2, my - hh / 2, w, hh);
                    MascotParts.FillSolid(g, m, dark);
                    Region old = g.Clip;
                    g.SetClip(m, CombineMode.Intersect);
                    MascotParts.Ellipse(g, Color.FromArgb(236, 112, 120), mx - w * 0.3f, my + hh * 0.12f, w * 0.6f, hh * 0.6f);
                    g.Clip = old;
                    old.Dispose();
                    MascotParts.Stroke(g, m, ink, lw * 0.9f);
                }
                if (mood == Mood.Idle && !talking)
                    using (GraphicsPath t = new GraphicsPath())
                    {
                        // His tongue out over his lip.
                        t.AddEllipse(mx - w * 0.36f, my - hh * 0.1f, w * 0.72f, hh);
                        MascotParts.FillSolid(g, t, Color.FromArgb(222, 104, 114));
                        MascotParts.Stroke(g, t, ink, lw * 0.6f);
                    }
            }
            return true;
        }

        // A thick bushy eyebrow along four points: fuller in the middle (half its thickness at each point, units of D),
        // rounded at the ends, a soft sheen on top.
        static void ShinBrow(Graphics g, PointF[] c, float D, float[] half = null)
        {
            if (half == null) half = new float[] { 0.012f, 0.02f, 0.02f, 0.012f };
            PointF[] edge = new PointF[c.Length * 2];
            for (int i = 0; i < c.Length; i++)
            {
                PointF a = c[Math.Max(0, i - 1)], b = c[Math.Min(c.Length - 1, i + 1)];
                float tx = b.X - a.X, ty = b.Y - a.Y, len = (float)Math.Sqrt(tx * tx + ty * ty), nx = -ty / len * half[i] * D, ny = tx / len * half[i] * D;
                edge[i] = new PointF(c[i].X - nx, c[i].Y - ny);
                edge[edge.Length - 1 - i] = new PointF(c[i].X + nx, c[i].Y + ny);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddClosedCurve(edge, 0.5f);
                MascotParts.FillSolid(g, p, Color.FromArgb(28, 24, 28));
                RectangleF r = p.GetBounds();
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                MascotParts.Glow(g, r.X + r.Width * 0.45f, r.Y + r.Height * 0.22f, r.Width * 0.6f, r.Height * 0.45f, Color.White, 46);
                g.Clip = old;
                old.Dispose();
            }
        }
    }
}
