// Stackshot - Demon Slayer's Tanjiro and Nezuko as anime chibis with the funny faces the anime gives them: Tanjiro's dot
// eyes and big open smile (wide white eyes with tiny pupils and a mouthful of teeth when startled), his dark red hair, the
// scar on his forehead and the hanafuda earrings, the black uniform under the green and black checked haori; Nezuko's
// dot eyes, raised brows and blush over the bamboo in her mouth, her long black hair turning orange at the ends, the
// pink ribbon, the pink kimono with its hemp leaf pattern under the dark haori, the checked sash. Fan tributes, not
// affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindTanjiroSd = 101, KindNezukoSd = 102;

        static readonly Color TjHair = Color.FromArgb(128, 34, 32), TjHairShade = Color.FromArgb(60, 14, 16), TjHairLight = Color.FromArgb(222, 104, 84);
        static readonly Color NzHair = Color.FromArgb(40, 30, 36), NzHairShade = Color.FromArgb(18, 12, 16), NzPink = Color.FromArgb(250, 172, 192);

        // Tanjiro's hair behind his head, spiking up and back.
        static readonly float[] TanjiroHair =
        {
            -0.255f, -0.12f, -0.38f, -0.3f, -0.27f, -0.33f, -0.37f, -0.5f, -0.19f, -0.44f, -0.22f, -0.62f, -0.07f, -0.48f, 0, -0.65f,
            0.08f, -0.48f, 0.2f, -0.6f, 0.2f, -0.44f, 0.36f, -0.5f, 0.28f, -0.34f, 0.42f, -0.3f, 0.3f, -0.2f, 0.36f, -0.08f, 0.255f, -0.1f
        };
        // His fringe over his brow, short over the scar.
        static readonly float[] TanjiroFringe =
        {
            -0.3f, -0.28f, -0.15f, -0.4f, 0.05f, -0.42f, 0.22f, -0.38f, 0.31f, -0.27f, 0.29f, -0.1f, 0.24f, -0.25f, 0.17f, -0.28f, 0.05f, -0.27f,
            -0.02f, -0.15f, -0.08f, -0.24f, -0.15f, -0.13f, -0.21f, -0.22f, -0.27f, -0.08f, -0.28f, -0.2f
        };
        // Nezuko's fringe, cut just over her brows, and the long locks either side of her face.
        static readonly float[] NezukoFringe =
        {
            -0.3f, -0.26f, -0.15f, -0.4f, 0.05f, -0.42f, 0.22f, -0.38f, 0.31f, -0.26f, 0.27f, -0.14f, 0.2f, -0.2f, 0.14f, -0.15f, 0.07f, -0.21f,
            0, -0.16f, -0.07f, -0.21f, -0.14f, -0.15f, -0.2f, -0.2f, -0.27f, -0.14f
        };
        static readonly float[] NezukoSideLock = { -0.27f, -0.2f, -0.22f, -0.12f, -0.21f, 0.0f, -0.23f, 0.12f, -0.26f, 0.06f, -0.29f, 0.14f, -0.3f, 0.0f, -0.3f, -0.12f };

        // The head, drawn once and kept: the face; Tanjiro's scar and fringe; Nezuko's fringe, locks and ribbon.
        static void SlayerHead(Graphics g, int k, GraphicsPath face, float D)
        {
            VFill(g, k, face, NjSkin, NjSkinShade, D, 0);
            if (k == KindTanjiroSd)
            {
                // The scar on his forehead, like a dark red flame.
                VBlob(g, k, D, Color.FromArgb(176, 62, 52), Color.Empty, 0, 0.45f, 0.06f, -0.2f, 0.09f, -0.27f, 0.12f, -0.24f, 0.16f, -0.29f, 0.17f, -0.22f, 0.14f, -0.18f, 0.1f, -0.19f);
                using (GraphicsPath f = TnSpikes(D, 0.04f, TanjiroFringe)) SlayerHair(g, k, f, D, TjHair, TjHairShade);
            }
            else
            {
                foreach (int s in Sides)
                    using (GraphicsPath l = KCurve(D, 0.4f, s > 0 ? Mirror(NezukoSideLock) : NezukoSideLock)) SlayerHair(g, k, l, D, NzHair, NzHairShade);
                using (GraphicsPath f = TnSpikes(D, 0.04f, NezukoFringe)) SlayerHair(g, k, f, D, NzHair, NzHairShade);
                // The pink ribbon at the side of her head.
                foreach (int s in Sides)
                    VBlob(g, k, D, NzPink, Color.FromArgb(226, 120, 150), 60, 0.4f, 0.22f, -0.34f, 0.22f + s * 0.07f, -0.39f, 0.22f + s * 0.085f, -0.33f, 0.22f + s * 0.06f, -0.29f);
                VOval(g, k, D, Color.FromArgb(236, 120, 150), Color.Empty, 0, 0.22f, -0.335f, 0.035f, 0.035f);
            }
        }

        // A piece of hair in front: outlined where it falls over the face only (its top lies over the hair behind), shaded,
        // with a couple of light strands.
        static void SlayerHair(Graphics g, int k, GraphicsPath p, float D, Color hair, Color shade)
        {
            Region old = g.Clip;
            g.SetClip(new RectangleF(-D, -0.33f * D, 2 * D, 2 * D), CombineMode.Intersect);
            Stroke(g, p, TnInk(k), TnW(D) * 2);
            g.Clip = old;
            VPaint(g, p, hair, shade, D, 30);
            old.Dispose();
        }

        // Behind: the hair with the ears and Tanjiro's earrings (kept), the legs stepping as they walk, the arms, and over
        // them the clothes (kept).
        static void SlayerBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.5f, -0.7f, 1.0f, 1.05f), bg => SlayerHairBack(bg, k, D));
            Color leg = k == KindTanjiroSd ? Color.FromArgb(40, 40, 48) : NjSkin;
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.05f, 0.322f);
                VLimb(g, k, D, leg, k == KindTanjiroSd ? 0.062f : 0.044f, P(s * 0.046f, 0.235f), f, 0);
                if (k == KindTanjiroSd) VRound(g, k, D, Color.FromArgb(244, 244, 240), Color.FromArgb(206, 206, 210), 0, f.X - 0.034f, f.Y - 0.045f, 0.068f, 0.05f, 0.016f);   // his leg wraps
                VOval(g, k, D, Color.FromArgb(96, 66, 46), Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.022f, 0.08f, 0.04f);
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.2f, -0.02f, 0.4f, 0.34f), bg => SlayerClothes(bg, k, D));
        }

        static void SlayerHairBack(Graphics g, int k, float D)
        {
            if (k == KindTanjiroSd)
            {
                SpikyHair(g, k, D, TanjiroHair, TjHair, TjHairShade, TjHairLight, -0.25f);
                foreach (int s in Sides)
                {
                    VOval(g, k, D, NjSkin, NjSkinShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                    // The hanafuda earring: a white card with the red sun.
                    VRound(g, k, D, Color.FromArgb(250, 250, 246), Color.FromArgb(214, 214, 220), 0, s * 0.272f - 0.018f, 0.0f, 0.036f, 0.07f, 0.006f);
                    Ellipse(g, Color.FromArgb(220, 52, 44), (s * 0.272f - 0.011f) * D, 0.016f * D, 0.022f * D, 0.022f * D);
                    TnLine(g, Color.FromArgb(70, 70, 80), 0.004f, D, s * 0.272f - 0.012f, 0.052f, s * 0.272f + 0.012f, 0.052f);
                }
                return;
            }
            // Nezuko's long hair down her back, black turning to flame orange at the ends.
            using (GraphicsPath h = KCurve(D, 0.5f, 0, -0.41f, 0.22f, -0.38f, 0.32f, -0.22f, 0.34f, 0, 0.34f, 0.16f, 0.32f, 0.28f, 0.24f, 0.24f, 0.18f, 0.3f,
                                           0.1f, 0.24f, 0, 0.28f, -0.1f, 0.24f, -0.18f, 0.3f, -0.24f, 0.24f, -0.32f, 0.28f, -0.34f, 0.16f, -0.34f, 0, -0.32f, -0.22f, -0.22f, -0.38f))
            {
                Stroke(g, h, TnInk(k), TnW(D) * 2);
                RectangleF r = h.GetBounds();
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), NzHair, Color.FromArgb(236, 112, 44), 90f))
                {
                    ColorBlend cb = new ColorBlend(4);
                    cb.Colors = new Color[] { Color.FromArgb(70, 56, 64), NzHair, NzHair, Color.FromArgb(236, 112, 44) };
                    cb.Positions = new float[] { 0, 0.3f, 0.66f, 1 };
                    b.InterpolationColors = cb;
                    g.FillPath(b, h);
                }
            }
        }

        static void SlayerClothes(Graphics g, int k, float D)
        {
            if (k == KindTanjiroSd)
            {
                // The black uniform with its white belt, and the haori over it, its fronts in green and black checks.
                VRound(g, k, D, Color.FromArgb(40, 40, 50), Color.FromArgb(24, 24, 32), 40, -0.11f, 0.07f, 0.22f, 0.165f, 0.045f);
                VRound(g, k, D, Color.FromArgb(244, 244, 240), Color.Empty, 0, -0.106f, 0.2f, 0.212f, 0.02f, 0.008f);
                foreach (int s in Sides)
                    using (GraphicsPath panel = KPoly(D, s * 0.03f, 0.075f, s * 0.115f, 0.08f, s * 0.12f, 0.235f, s * 0.045f, 0.235f))
                    {
                        Stroke(g, panel, TnInk(k), TnW(D) * 2);
                        FillSolid(g, panel, Color.FromArgb(34, 34, 40));
                        Region old = g.Clip;
                        g.SetClip(panel, CombineMode.Intersect);
                        using (SolidBrush green = new SolidBrush(Color.FromArgb(46, 150, 110)))
                            for (int i = 0; i < 8; i++)
                                for (int j = 0; j < 5; j++)
                                    if ((i + j) % 2 == 0) g.FillRectangle(green, (s > 0 ? 0.03f + j * 0.022f : -0.14f + j * 0.022f) * D, (0.07f + i * 0.022f) * D, 0.022f * D, 0.022f * D);
                        g.Clip = old;
                        old.Dispose();
                    }
                return;
            }
            // Nezuko: the pink kimono with its hemp leaf pattern, the dark haori open over it, the red and white checked sash.
            using (GraphicsPath t = Theme.Round(new RectangleF(-0.11f * D, 0.07f * D, 0.22f * D, 0.2f * D), 0.045f * D))
            {
                VFill(g, k, t, NzPink, Color.FromArgb(226, 140, 164), D, 40);
                Region old = g.Clip;
                g.SetClip(t, CombineMode.Intersect);
                using (Pen leaf = new Pen(Color.FromArgb(150, 255, 236, 242), Math.Max(0.7f, D * 0.005f)))
                    for (int i = -2; i <= 2; i++)
                    {
                        float cx = i * 0.05f * D, cy = (0.13f + (i & 1) * 0.03f) * D, r = 0.03f * D;
                        for (int a = 0; a < 6; a++)
                        {
                            double an = a * Math.PI / 3;
                            g.DrawLine(leaf, cx, cy, cx + (float)Math.Cos(an) * r, cy + (float)Math.Sin(an) * r);
                        }
                    }
                g.Clip = old;
                old.Dispose();
            }
            foreach (int s in Sides) VPoly(g, k, D, Color.FromArgb(62, 44, 40), Color.FromArgb(40, 28, 26), 0, s * 0.045f, 0.07f, s * 0.115f, 0.08f, s * 0.12f, 0.24f, s * 0.075f, 0.24f);
            using (GraphicsPath sash = Theme.Round(new RectangleF(-0.112f * D, 0.165f * D, 0.224f * D, 0.04f * D), 0.01f * D))
            {
                VFill(g, k, sash, Color.FromArgb(214, 60, 52), Color.Empty, D, 0);
                Region old = g.Clip;
                g.SetClip(sash, CombineMode.Intersect);
                using (SolidBrush w = new SolidBrush(Color.FromArgb(250, 246, 240)))
                    for (int i = 0; i < 12; i++)
                        for (int j = 0; j < 2; j++)
                            if ((i + j) % 2 == 0) g.FillRectangle(w, (-0.112f + i * 0.02f) * D, (0.165f + j * 0.02f) * D, 0.02f * D, 0.02f * D);
                g.Clip = old;
                old.Dispose();
            }
        }

        static void SlayerArm(Graphics g, float D, int k, int side, PointF h)
        {
            PointF a = P(side * 0.1f, 0.115f);
            if (k == KindTanjiroSd)
            {
                VLimb(g, k, D, Color.FromArgb(46, 150, 110), 0.06f, a, h, side * 0.03f);
                TnLine(g, Color.FromArgb(34, 34, 40), 0.02f, D, Lerp(a, h, 0.3f).X, Lerp(a, h, 0.3f).Y, Lerp(a, h, 0.62f).X, Lerp(a, h, 0.62f).Y);
            }
            else VLimb(g, k, D, NzPink, 0.058f, a, h, side * 0.03f);
        }

        // Nezuko's brows (raised, as in her funny faces) and blush; the bamboo goes with her face (NezukoFace).
        static void SlayerDetails(Graphics g, int k, float D, float f)
        {
            if (k != KindNezukoSd) return;
            foreach (int s in Sides)
            {
                TnLine(g, Color.FromArgb(60, 40, 40), 0.008f, D, s * 0.06f, f - 0.1f, s * 0.1f, f - 0.118f, s * 0.145f, f - 0.108f);
                Glow(g, s * 0.16f * D, (f + 0.06f) * D, 0.08f * D, 0.036f * D, Color.FromArgb(255, 130, 150), 90);
            }
        }

        static string[] SlayerLines(int k)
        {
            if (!I18n.IsSpanish)
            {
                if (k == KindTanjiroSd)
                    return new string[] { "Water Breathing, First Form!", "Nezuko, don't worry.", "I smell a demon\u2026", "I've got a really hard head!", "I'll give it everything!" };
                return new string[] { "Mmm!", "Mmm-mmh.", "Mm-mm!", "Mmm\u2026 \u266A" };
            }
            if (k == KindTanjiroSd)
                return new string[] { "\u00A1Respiraci\u00F3n del agua, primera postura!", "Nezuko, no te preocupes.", "Huele a demonio\u2026", "\u00A1Tengo la cabeza muy dura!", "\u00A1Lo dar\u00E9 todo!" };
            return new string[] { "\u00A1Mmm!", "Mmm-mmh.", "\u00A1Mm-mm!", "Mmm\u2026 \u266A" };
        }
    }

    public partial class Mascot
    {
        // A black dot of an eye with a tiny shine, r across; a short line when it shuts.
        static void SlayerDot(Graphics g, Pen p, float x, float y, float D, float r, float blink)
        {
            if (blink < 0.5f) { g.DrawLine(p, x - r * 1.1f, y, x + r * 1.1f, y); return; }
            MascotParts.Ellipse(g, Color.FromArgb(30, 22, 24), x - r, y - r * blink, r * 2, r * 2 * blink);
            MascotParts.Ellipse(g, Color.FromArgb(230, 255, 255, 255), x - r * 0.55f, y - r * 0.6f, r * 0.55f, r * 0.55f);
        }

        // The eyes of every mood that shut or change: happy arcs, sleepy lines, hearts, spirals. False for the others.
        bool SlayerMoodEye(Graphics g, Pen p, float x, float y, float D, Mood m, int i, float size)
        {
            switch (m)
            {
                case Mood.Happy:
                case Mood.Wink: g.DrawArc(p, x - size, y - size * 0.5f, size * 2, size * 1.5f, 200, 140); return true;
                case Mood.Sleep:
                case Mood.Yawn: g.DrawArc(p, x - size, y - size * 0.8f, size * 2, size * 1.1f, 20, 140); return true;
                case Mood.Love:
                    using (GraphicsPath hp = MascotParts.Heart(x, y, size * 2.4f)) MascotParts.FillSolid(g, hp, LoveColor);
                    return true;
                case Mood.Dizzy: DizzyEye(g, p, x, y, size * 1.3f, i); return true;
                default: return false;
            }
        }

        // Tanjiro: dot eyes over a big open smile; startled, wide white eyes with tiny pupils, a mouthful of teeth and a
        // drop of sweat.
        bool TanjiroFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            Color ink = Color.FromArgb(46, 30, 28), dark = Color.FromArgb(120, 36, 44);
            float gap = geo.EyeGap * D, lw = Math.Max(1.1f, D * 0.012f);
            bool shocked = mood == Mood.Surprised;
            using (Pen p = new Pen(ink, lw))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0 ? -gap : gap) + ex, y = ey;
                    Mood m = mood == Mood.Wink && i == 1 ? Mood.Sleep : mood;
                    if (shocked)
                    {
                        float r = 0.068f * D;
                        MascotParts.Ellipse(g, ink, x - r, y - r, r * 2, r * 2);
                        MascotParts.Ellipse(g, Color.White, x - r + lw, y - r + lw, r * 2 - lw * 2, r * 2 - lw * 2);
                        MascotParts.Ellipse(g, ink, x - 0.011f * D, y - 0.011f * D, 0.022f * D, 0.022f * D);
                        continue;
                    }
                    if (!SlayerMoodEye(g, p, x, y, D, m, i, 0.03f * D)) SlayerDot(g, p, x, y, D, 0.03f * D, blink);
                }
                float mx = ex * 0.6f, my = geo.FaceY * D + 0.09f * D;
                bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy;
                if (shocked)
                {
                    // Wide open, the top and bottom teeth showing.
                    RectangleF r = new RectangleF(mx - 0.05f * D, my - 0.012f * D, 0.1f * D, 0.05f * D);
                    using (GraphicsPath m = Theme.Round(r, 0.018f * D))
                    {
                        MascotParts.FillSolid(g, m, dark);
                        Region old = g.Clip;
                        g.SetClip(m, CombineMode.Intersect);
                        using (SolidBrush w = new SolidBrush(Color.White))
                        {
                            g.FillRectangle(w, r.X, r.Y, r.Width, r.Height * 0.3f);
                            g.FillRectangle(w, r.X, r.Bottom - r.Height * 0.28f, r.Width, r.Height * 0.28f);
                        }
                        g.Clip = old;
                        old.Dispose();
                        MascotParts.Stroke(g, m, ink, lw);
                    }
                    using (GraphicsPath drop = new GraphicsPath())
                    {
                        float dx = 0.2f * D, dy = geo.FaceY * D - 0.13f * D, dr = 0.016f * D;
                        drop.AddArc(dx - dr, dy, dr * 2, dr * 2, 0, 180);
                        drop.AddLine(dx - dr, dy + dr, dx, dy - dr * 1.6f);
                        drop.CloseFigure();
                        MascotParts.FillSolid(g, drop, Color.FromArgb(200, 170, 220, 255));
                        MascotParts.Stroke(g, drop, Color.FromArgb(160, 60, 120, 200), Math.Max(0.8f, D * 0.005f));
                    }
                }
                else if (mood == Mood.Sleep) g.DrawArc(p, mx - 0.02f * D, my - 0.01f * D, 0.04f * D, 0.02f * D, 20, 140);
                else if (mood == Mood.Yawn || mood == Mood.Dizzy)
                {
                    float r = (mood == Mood.Yawn ? 0.03f : 0.016f) * D;
                    MascotParts.Ellipse(g, dark, mx - r * 0.8f, my - r * 0.5f, r * 1.6f, r * 2);
                }
                else
                {
                    // His big open smile, the tongue in it; it moves as he talks.
                    float w = 0.07f * D, h = (mood == Mood.Happy || mood == Mood.Love ? 0.06f : 0.05f) * D;
                    if (talking && mood == Mood.Idle) h *= (float)(0.55 + 0.45 * Math.Abs(Math.Sin(now / 110.0)));
                    using (GraphicsPath m = new GraphicsPath())
                    {
                        m.AddArc(mx - w / 2, my - h, w, h * 2, 0, 180);
                        m.CloseFigure();
                        MascotParts.FillSolid(g, m, dark);
                        Region old = g.Clip;
                        g.SetClip(m, CombineMode.Intersect);
                        MascotParts.Ellipse(g, Color.FromArgb(240, 116, 126), mx - w * 0.32f, my + h * 0.35f, w * 0.64f, h * 0.8f);
                        g.Clip = old;
                        old.Dispose();
                        MascotParts.Stroke(g, m, ink, lw);
                    }
                }
            }
            return true;
        }

        // Nezuko: small dot eyes, and the bamboo across her mouth, its cord running back to her ears; it bobs as she
        // "talks".
        bool NezukoFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            Color ink = Color.FromArgb(46, 30, 28);
            float gap = geo.EyeGap * D, lw = Math.Max(1f, D * 0.011f);
            using (Pen p = new Pen(ink, lw))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0 ? -gap : gap) + ex, y = ey;
                    Mood m = mood == Mood.Wink && i == 1 ? Mood.Sleep : mood;
                    if (!SlayerMoodEye(g, p, x, y, D, m, i, 0.026f * D)) SlayerDot(g, p, x, y, D, (m == Mood.Surprised ? 0.03f : 0.022f) * D, blink);
                }
            }
            bool talking = Bubble != null && mood != Mood.Sleep;
            float bob = talking ? (float)Math.Sin(now / 90.0) * 0.006f * D : 0;
            float mx = ex * 0.6f, my = geo.FaceY * D + 0.085f * D + bob;
            using (Pen cord = new Pen(Color.FromArgb(150, 60, 40), Math.Max(0.8f, D * 0.007f)))
            {
                g.DrawLine(cord, mx - 0.11f * D, my, -0.25f * D, my - 0.03f * D);
                g.DrawLine(cord, mx + 0.11f * D, my, 0.25f * D, my - 0.03f * D);
            }
            RectangleF b = new RectangleF(mx - 0.115f * D, my - 0.024f * D, 0.23f * D, 0.048f * D);
            using (GraphicsPath bp = Theme.Round(b, 0.02f * D))
            {
                MascotParts.Stroke(g, bp, ink, lw * 2);
                using (LinearGradientBrush br = new LinearGradientBrush(RectangleF.Inflate(b, 0, 1), Color.FromArgb(176, 214, 110), Color.FromArgb(96, 150, 52), 90f)) g.FillPath(br, bp);
            }
            // Its nodes, and a shine along it.
            using (Pen node = new Pen(Color.FromArgb(70, 110, 40), Math.Max(0.8f, D * 0.008f)))
                foreach (float t in new float[] { -0.075f, 0.08f }) g.DrawLine(node, mx + t * D, b.Y + 1, mx + t * D, b.Bottom - 1);
            MascotParts.Line(g, Color.FromArgb(150, 255, 255, 230), Math.Max(0.8f, D * 0.006f), b.X + 0.02f * D, b.Y + b.Height * 0.3f, b.Right - 0.03f * D, b.Y + b.Height * 0.3f);
            return true;
        }
    }
}
