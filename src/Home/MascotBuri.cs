// Stackshot - Buriburizaemon, the Brave Little Pig of Crayon Shin-chan, as his vinyl figure has him: a big pink head,
// wider than tall, with a pointed ear standing up at each top corner; the thick black eyebrows slanting down to the
// middle, small black eyes and the wide snout with its two big nostrils over his mouth; bare-chested and pot-bellied,
// his hands on his hips, the baggy purple trousers down over his feet and the yellow guard of his sword at his hip. He
// turns to face the way he walks. Fan tribute, not affiliated with its owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindPiggySd = 110;

        // Some are drawn a little smaller than their measurements, all of them from the ground up: the Cerdito's big head
        // stood too tall next to the others, above all as the desktop pet.
        public static float KindScale(int kind) { return kind == KindPiggySd ? 0.84f : 1f; }
        public const float KindScaleGround = 0.344f;

        static readonly Color BrPink = Color.FromArgb(250, 196, 184), BrPinkShade = Color.FromArgb(226, 150, 140), BrSnout = Color.FromArgb(247, 186, 174);
        static readonly Color BrPurple = Color.FromArgb(132, 104, 196), BrPurpleShade = Color.FromArgb(88, 62, 150), BrNostril = Color.FromArgb(104, 56, 50);
        static readonly Color BrGold = Color.FromArgb(250, 204, 44), BrGoldShade = Color.FromArgb(214, 158, 20);

        // His head from the front: a little domed across the top between his ears, the cheeks round and wide, in to the
        // chin under his snout.
        static GraphicsPath BuriHead(float D)
        {
            return KCurve(D, 0.5f, 0, -0.612f, 0.182f, -0.594f, 0.276f, -0.548f, 0.326f, -0.482f, 0.339f, -0.404f, 0.333f, -0.32f, 0.3f, -0.268f,
                          0.235f, -0.243f, 0.155f, -0.197f, 0.09f, -0.172f, 0, -0.165f, -0.09f, -0.172f, -0.155f, -0.197f, -0.235f, -0.243f,
                          -0.3f, -0.268f, -0.333f, -0.32f, -0.339f, -0.404f, -0.326f, -0.482f, -0.276f, -0.548f, -0.182f, -0.594f);
        }

        // The head, kept: his ears behind it, the head, and the snout with its two deep nostrils.
        static void BuriHeadDraw(Graphics g, GraphicsPath head, float D)
        {
            int k = KindPiggySd;
            foreach (int s in Sides)
                using (GraphicsPath ear = KCurve(D, 0.3f, s * 0.318f, -0.47f, s * 0.298f, -0.708f, s * 0.15f, -0.592f))
                {
                    // A big pointed ear standing up at each top corner, a little hollow inside.
                    VFill(g, k, ear, BrPink, BrPinkShade, D, 0);
                    Region old = g.Clip;
                    g.SetClip(ear, CombineMode.Intersect);
                    using (GraphicsPath hollow = KCurve(D, 0.3f, s * 0.285f, -0.52f, s * 0.287f, -0.664f, s * 0.19f, -0.588f)) FillSolid(g, hollow, Color.FromArgb(64, 190, 100, 96));
                    g.Clip = old;
                    old.Dispose();
                }
            VFill(g, k, head, BrPink, BrPinkShade, D, 0);
            // The snout: wide, rising to a bridge between his eyes, its lower edge in two lobes over his mouth.
            using (GraphicsPath sn = KCurve(D, 0.5f, 0, -0.39f, 0.07f, -0.381f, 0.138f, -0.356f, 0.19f, -0.316f, 0.211f, -0.27f, 0.2f, -0.234f,
                                            0.165f, -0.212f, 0.12f, -0.205f, 0.07f, -0.214f, 0.03f, -0.232f, 0, -0.24f, -0.03f, -0.232f, -0.07f, -0.214f,
                                            -0.12f, -0.205f, -0.165f, -0.212f, -0.2f, -0.234f, -0.211f, -0.27f, -0.19f, -0.316f, -0.138f, -0.356f, -0.07f, -0.381f))
                VFill(g, k, sn, BrSnout, BrPinkShade, D, 0);
            foreach (int s in Sides)
                using (GraphicsPath n = new GraphicsPath())
                {
                    // A nostril: dark and deep, the light catching its lower inside.
                    n.AddEllipse((s * 0.118f - 0.041f) * D, -0.33f * D, 0.082f * D, 0.076f * D);
                    Stroke(g, n, TnInk(k), TnW(D, k) * 2);
                    FillSolid(g, n, BrNostril);
                    Region old = g.Clip;
                    g.SetClip(n, CombineMode.Intersect);
                    Glow(g, s * 0.118f * D, -0.262f * D, 0.072f * D, 0.044f * D, Color.FromArgb(222, 150, 130), 130);
                    g.Clip = old;
                    old.Dispose();
                }
        }

        // Where a foot is, his front to the right: stepping as he walks, swinging in the air.
        static PointF BuriStep(MascotPose p, int side)
        {
            PointF s = SnoopyStep(p, side);
            return new PointF(s.X * 0.4f, s.Y);
        }

        // Behind his head: his bare body with the outline of his trousers (kept), his trouser legs down over his feet,
        // then the top of his trousers over the legs' tops (kept), so hips and legs make one baggy piece; and his arms.
        static void BuriBody(Graphics g, float D, MascotPose p, Geo geo)
        {
            int k = KindPiggySd;
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.3f, -0.24f, 0.6f, 0.5f), bg => BuriTorso(bg, D));
            foreach (int s in Sides)
            {
                PointF st = BuriStep(p, s), f = P(s * 0.18f + st.X, 0.36f + st.Y);
                VOval(g, k, D, BrPurple, BrPurpleShade, 0, f.X + s * 0.014f, f.Y + 0.006f, 0.14f, 0.078f);
                VLimb(g, k, D, BrPurple, 0.14f, P(s * 0.13f, 0.14f), P(f.X, f.Y - 0.022f), 0);
            }
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.3f, 0, 0.6f, 0.26f), bg => BuriHips(bg, D));
            if (!p.HideHands) foreach (int s in Sides) BuriArm(g, D, s, DsHand(p, geo, s));
        }

        // The top of his baggy trousers: from his waist down to where his legs part.
        static GraphicsPath BuriTrousers(float D)
        {
            return KCurve(D, 0.4f, -0.245f, 0.035f, 0, 0.03f, 0.245f, 0.035f, 0.262f, 0.12f, 0.255f, 0.2f, 0.19f, 0.226f, 0.07f, 0.214f, 0, 0.19f,
                          -0.07f, 0.214f, -0.19f, 0.226f, -0.255f, 0.2f, -0.262f, 0.12f);
        }

        static void BuriHips(Graphics g, float D)
        {
            using (GraphicsPath t = BuriTrousers(D))
            {
                SoftPaint(g, t, BrPurple, BrPurpleShade, D);
                Region old = g.Clip;
                g.SetClip(t, CombineMode.Intersect);
                Glow(g, 0, 0.03f * D, 0.56f * D, 0.06f * D, Color.Black, 60);   // the shade of his belly
                g.Clip = old;
                old.Dispose();
            }
        }

        // His bare body, round in the belly, in the shade of his head; and the outline of his trousers, its inside painted
        // over the legs' tops later.
        static void BuriTorso(Graphics g, float D)
        {
            int k = KindPiggySd;
            using (GraphicsPath b = KCurve(D, 0.45f, -0.17f, -0.215f, 0.17f, -0.215f, 0.222f, -0.13f, 0.245f, -0.03f, 0.242f, 0.05f, 0, 0.075f,
                                           -0.242f, 0.05f, -0.245f, -0.03f, -0.222f, -0.13f))
            {
                VFill(g, k, b, BrPink, BrPinkShade, D, 0);
                Region old = g.Clip;
                g.SetClip(b, CombineMode.Intersect);
                Glow(g, 0, -0.2f * D, 0.6f * D, 0.14f * D, Color.Black, 70);
                g.Clip = old;
                old.Dispose();
            }
            foreach (int s in Sides) Ellipse(g, Color.FromArgb(120, 66, 60), (s * 0.128f - 0.007f) * D, -0.083f * D, 0.014f * D, 0.014f * D);
            using (GraphicsPath t = BuriTrousers(D)) Stroke(g, t, TnInk(k), TnW(D, k) * 2);
        }

        // An arm from his shoulder to his hand, bent at the elbow out to the side: with his hand on his hip, he stands
        // akimbo.
        static void BuriArm(Graphics g, float D, int side, PointF h)
        {
            PointF a = P(side * 0.205f, -0.13f), e = Lerp(a, h, 0.5f);
            float dx = h.X - a.X, dy = h.Y - a.Y, d = (float)Math.Sqrt(dx * dx + dy * dy), L = 0.15f;
            if (d > 0.001f && d < L * 2)
            {
                float off = (float)Math.Sqrt(L * L - d * d / 4), nx = -dy / d, ny = dx / d;
                if (nx * side < 0) { nx = -nx; ny = -ny; }
                e = P(e.X + nx * off, e.Y + ny * off);
            }
            // One smooth limb through the elbow: its control point puts the middle of the curve there.
            PointF q = P((e.X - 0.125f * (a.X + h.X)) / 0.75f * D, (e.Y - 0.125f * (a.Y + h.Y)) / 0.75f * D);
            SoftLimb(g, KindPiggySd, D, BrPink, 0.08f, P(a.X * D, a.Y * D), q, q, P(h.X * D, h.Y * D));
        }

        static void BuriHand(Graphics g, float D) { VOval(g, KindPiggySd, D, BrPink, BrPinkShade, 0, 0, 0, 0.076f, 0.07f); }

        // The hilt of his sword at his hip, in front of his hand: the round yellow guard, and the red grip with its gold
        // pommel up out of it.
        internal static void BuriSword(Graphics g, float D)
        {
            int k = KindPiggySd;
            VOval(g, k, D, BrGold, BrGoldShade, 0, 0.27f, 0.072f, 0.112f, 0.1f);
            VOval(g, k, D, Color.FromArgb(196, 44, 56), Color.Empty, 0, 0.27f, 0.072f, 0.044f, 0.04f);
            VLimb(g, k, D, Color.FromArgb(214, 52, 62), 0.032f, P(0.272f, 0.066f), P(0.316f, 0.004f), 0);
            VOval(g, k, D, BrGold, BrGoldShade, 0, 0.32f, -0.004f, 0.038f, 0.042f);
        }

        static string[] BuriLines()
        {
            if (!I18n.IsSpanish)
                return new string[] { "I'm Buriburizaemon, the savior!", "En garde!", "Oink, oink.", "Got anything to eat?",
                                      "I always side with whoever's strongest." };
            return new string[] { "\u00A1Soy el Cerdito Valiente, el salvador!", "\u00A1En guardia!", "Oink, oink.", "\u00BFHay algo de comer?",
                                  "Yo siempre me pongo del lado del m\u00E1s fuerte." };
        }
    }

    public partial class Mascot
    {
        // Buriburizaemon's face: small black eyes under the thick eyebrows slanting down to the middle (raised when he is
        // surprised), and his mouth under the snout, open when he talks, laughs, gasps or yawns.
        bool BuriFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 30), dark = Color.FromArgb(120, 44, 48);
            float lw = Math.Max(1.1f, D * 0.012f), dx = ex * 0.4f, dy = (ey - geo.FaceY * D) * 0.5f;
            bool surprised = mood == Mood.Surprised;
            using (Pen p = new Pen(ink, lw))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 0.172f * D + dx, y = -0.425f * D + dy, r = (surprised ? 0.037f : 0.03f) * D;
                    Mood m = mood == Mood.Wink && s > 0 ? Mood.Sleep : mood;
                    if (m == Mood.Happy || m == Mood.Wink) { g.DrawArc(p, x - r * 1.2f, y - r * 0.6f, r * 2.4f, r * 1.8f, 200, 140); continue; }
                    if (m == Mood.Sleep || m == Mood.Yawn) { g.DrawArc(p, x - r * 1.2f, y - r, r * 2.4f, r * 1.4f, 20, 140); continue; }
                    if (m == Mood.Love) { using (GraphicsPath hp = MascotParts.Heart(x, y, r * 3.4f)) MascotParts.FillSolid(g, hp, LoveColor); continue; }
                    if (m == Mood.Dizzy) { DizzyEye(g, p, x, y, r * 1.5f, s < 0 ? 0 : 1); continue; }
                    float h = r * 1.05f * (surprised ? 1 : blink);
                    if (h < r * 0.3f) { g.DrawLine(p, x - r * 1.2f, y, x + r * 1.2f, y); continue; }
                    MascotParts.Ellipse(g, ink, x - r, y - h, r * 2, h * 2);
                    if (h > r * 0.6f) MascotParts.Ellipse(g, Color.FromArgb(235, 255, 255, 255), x - r * 0.55f, y - h * 0.62f, r * 0.55f, r * 0.55f);
                }
                // The eyebrows, slanting down to the middle; surprised, they go up, the inner ends most.
                float lift = surprised ? -0.03f * D : 0;
                for (int s = -1; s <= 1; s += 2)
                {
                    PointF[] pts = { new PointF(s * 0.235f * D + dx, -0.524f * D + lift), new PointF(s * 0.19f * D + dx, -0.512f * D + lift * 1.1f),
                                     new PointF(s * 0.14f * D + dx, -0.492f * D + lift * 1.3f), new PointF(s * 0.092f * D + dx, -0.462f * D + lift * 1.5f) };
                    ShinBrow(g, pts, D, new float[] { 0.017f, 0.021f, 0.021f, 0.017f });
                }
                if (mood == Mood.Love || mood == Mood.Happy)
                    for (int s = -1; s <= 1; s += 2) MascotParts.Glow(g, s * 0.27f * D, -0.3f * D, 0.1f * D, 0.05f * D, Color.FromArgb(255, 120, 130), 110);
                // The mouth under the snout: a wide smile, or round when he talks, gasps or yawns.
                bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy, smile = mood == Mood.Happy || mood == Mood.Love;
                float open = surprised ? 0.044f : mood == Mood.Yawn ? 0.05f : 0;
                if (talking && mood == Mood.Idle) open = 0.04f * (float)(0.4 + 0.6 * Math.Abs(Math.Sin(now / 110.0)));
                if (!smile && open <= 0) return true;
                float mx = dx * 0.5f, w = (smile ? 0.11f : 0.074f) * D;
                using (GraphicsPath mp = new GraphicsPath())
                {
                    if (smile) { mp.AddArc(mx - w / 2, -0.256f * D, w, 0.084f * D, 0, 180); mp.CloseFigure(); }
                    else mp.AddEllipse(mx - w / 2, -0.196f * D - open * D / 2, w, open * D);
                    MascotParts.FillSolid(g, mp, dark);
                    RectangleF mr = mp.GetBounds();
                    Region old = g.Clip;
                    g.SetClip(mp, CombineMode.Intersect);
                    MascotParts.Ellipse(g, Color.FromArgb(236, 112, 120), mx - w * 0.3f, mr.Bottom - mr.Height * 0.5f, w * 0.6f, mr.Height * 0.6f);
                    g.Clip = old;
                    old.Dispose();
                    MascotParts.Stroke(g, mp, ink, lw * 0.9f);
                }
            }
            return true;
        }
    }
}
