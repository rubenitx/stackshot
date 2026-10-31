// Stackshot - Nevado (Shiro), Shin-chan's little dog, drawn flat as his games show him: three quarters, a big round
// cotton ball of a head, wider than tall, the tiny dot eyes far apart with a little line over each, the black smudge of
// a nose low at the front, one floppy ear flying back and the other folded over, the blue collar, and under it all a
// small fluffy body on four stubby legs. He turns to face the way he walks. Fan tribute, not affiliated with its owners
// (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindNevadoSd = 107;

        static readonly Color NvWhite = Color.FromArgb(252, 252, 252), NvShade = Color.FromArgb(206, 212, 226), NvFar = Color.FromArgb(232, 236, 244);

        // He is drawn a little narrower and smaller than his measurements, from the ground up, so he stays small and slim.
        const float NvKx = 0.88f, NvKy = 0.95f;
        internal static void NvScale(Graphics g, float D)
        {
            g.TranslateTransform(0, 0.4f * D);
            g.ScaleTransform(NvKx, NvKy);
            g.TranslateTransform(0, -0.4f * D);
        }

        // A fluffy shape: soft bumps all round an ellipse (centre and radii in units of D).
        static GraphicsPath Fluff(float D, float cx, float cy, float rx, float ry, int bumps, float depth)
        {
            PointF[] pts = new PointF[bumps * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = Math.PI * 2 * i / pts.Length - Math.PI / 2;
                float r = i % 2 == 0 ? 1 : 1 - depth;
                pts[i] = P((cx + (float)Math.Cos(a) * rx * r) * D, (cy + (float)Math.Sin(a) * ry * r) * D);
            }
            GraphicsPath p = new GraphicsPath();
            p.AddClosedCurve(pts, 0.6f);
            return p;
        }

        // His head, facing right: a big soft ball, flatter on top, a little fuller low at the front.
        static GraphicsPath NevadoHead(float D)
        {
            return KCurve(D, 0.5f, 0.015f, -0.225f, 0.17f, -0.215f, 0.3f, -0.15f, 0.35f, 0.02f, 0.3f, 0.17f, 0.15f, 0.235f, 0, 0.24f,
                          -0.16f, 0.225f, -0.28f, 0.16f, -0.315f, 0, -0.27f, -0.15f, -0.13f, -0.215f);
        }

        // The head, kept: the ear flying back behind it, the ball of his head, and the other ear folded over at the front.
        static void NevadoHeadDraw(Graphics g, GraphicsPath head, float D)
        {
            int k = KindNevadoSd;
            GraphicsState st = g.Save();
            NvScale(g, D);
            VBlob(g, k, D, NvWhite, NvShade, 0, 0.5f, -0.2f, -0.2f, -0.32f, -0.215f, -0.47f, -0.2f, -0.535f, -0.15f, -0.5f, -0.1f, -0.38f, -0.115f, -0.25f, -0.1f);
            VFill(g, k, head, NvWhite, NvShade, D, 0);
            VBlob(g, k, D, NvWhite, NvShade, 0, 0.5f, 0.2f, -0.19f, 0.3f, -0.205f, 0.36f, -0.15f, 0.355f, -0.085f, 0.31f, -0.09f, 0.27f, -0.14f);
            g.Restore(st);
        }

        // Where a leg's paw is (his front to the right): trotting, one front leg with the other back one; paddling in the air.
        static PointF NevadoStep(MascotPose p, int leg)
        {
            PointF s = SnoopyStep(p, leg == 0 || leg == 3 ? 1 : -1);
            return new PointF(s.X * 0.6f, s.Y * 0.7f);
        }

        // Behind his head: the far legs, his little tail wagging, his small fluffy body and the collar (kept), the near legs.
        static void NevadoBody(Graphics g, float D, MascotPose p)
        {
            int k = KindNevadoSd;
            GraphicsState all = g.Save();
            NvScale(g, D);
            float[] legX = { 0.06f, -0.17f, 0.16f, -0.25f };   // near front, near back, far front, far back
            for (int leg = 2; leg < 4; leg++) NevadoLeg(g, D, legX[leg], NevadoStep(p, leg), true);
            float wag = (float)(Math.Sin(p.Now / (p.Walk > 0.3 ? 110.0 : 240.0)) * (p.Sleeping ? 2 : 16));
            GraphicsState st = g.Save();
            g.TranslateTransform(-0.3f * D, 0.21f * D);
            g.RotateTransform(-wag);
            using (GraphicsPath tail = Fluff(D, -0.03f, -0.05f, 0.05f, 0.065f, 7, 0.08f)) VFill(g, k, tail, NvWhite, NvShade, D, 0);
            g.Restore(st);
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.36f, 0.14f, 0.62f, 0.22f), bg => NevadoTorso(bg, D));
            for (int leg = 0; leg < 2; leg++) NevadoLeg(g, D, legX[leg], NevadoStep(p, leg), false);
            g.Restore(all);
        }

        static void NevadoTorso(Graphics g, float D)
        {
            int k = KindNevadoSd;
            using (GraphicsPath b = KCurve(D, 0.5f, -0.28f, 0.18f, -0.1f, 0.2f, 0.12f, 0.2f, 0.22f, 0.24f, 0.2f, 0.31f, 0, 0.33f, -0.22f, 0.32f, -0.32f, 0.26f))
                VFill(g, k, b, NvWhite, NvShade, D, 0);
            using (GraphicsPath collar = TnTube(D, 0.036f, P(-0.13f, 0.238f), P(-0.05f, 0.268f), P(0.05f, 0.268f), P(0.12f, 0.238f))) VFill(g, k, collar, Color.FromArgb(70, 146, 232), Color.Empty, D, 0);
        }

        // A short stubby leg down to its round paw; the far ones a touch greyer.
        static void NevadoLeg(Graphics g, float D, float x, PointF step, bool far)
        {
            int k = KindNevadoSd;
            Color c = far ? NvFar : NvWhite;
            PointF f = P(x + step.X, 0.375f + step.Y);
            VLimb(g, k, D, c, 0.066f, P(x, 0.28f), P(f.X, f.Y - 0.01f), 0);
            VOval(g, k, D, c, Color.Empty, 0, f.X + 0.008f, f.Y + 0.002f, 0.08f, 0.046f);
        }

        static string[] NevadoLines()
        {
            if (!I18n.IsSpanish) return new string[] { "Woof!", "Woof, woof!", "Grrr\u2026", "Awooo\u2026" };
            return new string[] { "\u00A1Guau!", "\u00A1Guau, guau!", "Grrr\u2026", "Auuu\u2026" };
        }
    }

    public partial class Mascot
    {
        // Nevado's face three quarters: two tiny dot eyes far apart with a little line over each, the black smudge of his
        // nose low between them at the front, and a little pink tongue when he barks or is happy.
        bool NevadoFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            GraphicsState fs = g.Save();
            MascotParts.NvScale(g, D);
            Color ink = Color.FromArgb(30, 26, 30);
            float lw = Math.Max(1f, D * 0.011f), dx = ex * 0.3f, dy = (ey - geo.FaceY * D) * 0.5f;
            float[] eyes = { -0.06f, 0.03f, 0.295f, 0.015f };
            bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy;
            using (Pen p = new Pen(ink, lw))
            using (Pen thin = new Pen(ink, Math.Max(0.8f, D * 0.007f)))
            {
                p.StartCap = p.EndCap = thin.StartCap = thin.EndCap = LineCap.Round;
                for (int i = 0; i < 2; i++)
                {
                    float x = eyes[i * 2] * D + dx, y = eyes[i * 2 + 1] * D + dy, r = 0.024f * D * (mood == Mood.Surprised ? 1.3f : 1f);
                    Mood m = mood == Mood.Wink && i == 1 ? Mood.Sleep : mood;
                    switch (m)
                    {
                        case Mood.Happy:
                        case Mood.Wink: g.DrawArc(p, x - r * 1.5f, y - r * 0.6f, r * 3, r * 2, 200, 140); break;
                        case Mood.Sleep:
                        case Mood.Yawn: g.DrawArc(p, x - r * 1.5f, y - r, r * 3, r * 1.5f, 20, 140); break;
                        case Mood.Love: using (GraphicsPath hp = MascotParts.Heart(x, y, r * 3.6f)) MascotParts.FillSolid(g, hp, LoveColor); break;
                        case Mood.Dizzy: DizzyEye(g, p, x, y, r * 1.7f, i); break;
                        default:
                            if (blink < 0.5f) g.DrawLine(p, x - r * 1.2f, y, x + r * 1.2f, y);
                            else MascotParts.Ellipse(g, ink, x - r * 0.85f, y - r * 1.15f * blink, r * 1.7f, r * 2.3f * blink);
                            break;
                    }
                    // The little line over each eye.
                    float bw = (i == 0 ? 0.075f : 0.03f) * D, lift = mood == Mood.Surprised ? -0.02f * D : 0;
                    g.DrawArc(thin, x - bw, y - (i == 0 ? 0.13f : 0.075f) * D + lift, bw * 2, (i == 0 ? 0.12f : 0.06f) * D, 200, 140);
                }
            }
            // His nose: a black smudge, a little wavy, low at the front.
            float nx = 0.205f * D, ny = 0.108f * D;
            using (GraphicsPath n = new GraphicsPath())
            {
                n.AddBezier(new PointF(nx - 0.08f * D, ny - 0.012f * D), new PointF(nx - 0.03f * D, ny - 0.03f * D), new PointF(nx + 0.03f * D, ny - 0.026f * D), new PointF(nx + 0.08f * D, ny - 0.01f * D));
                n.AddBezier(new PointF(nx + 0.08f * D, ny - 0.01f * D), new PointF(nx + 0.07f * D, ny + 0.026f * D), new PointF(nx - 0.06f * D, ny + 0.03f * D), new PointF(nx - 0.08f * D, ny - 0.012f * D));
                n.CloseFigure();
                MascotParts.FillSolid(g, n, ink);
            }
            if (talking || mood == Mood.Happy || mood == Mood.Yawn || mood == Mood.Love)
            {
                float hh = (mood == Mood.Yawn ? 0.06f : 0.04f) * D;
                if (talking && mood == Mood.Idle) hh *= (float)(0.5 + 0.5 * Math.Abs(Math.Sin(now / 110.0)));
                MascotParts.Ellipse(g, Color.FromArgb(236, 110, 120), nx - 0.03f * D, ny + 0.025f * D, 0.05f * D, hh);
            }
            g.Restore(fs);
            return true;
        }
    }
}
