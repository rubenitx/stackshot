// Stackshot - Characters from Klei's games, drawn in their own hand-inked style instead of the mascot's round one.
// Don't Starve Together's Wilson and Willow follow the game's drawings: a spade-shaped head with a sharp chin, a brush
// line that swells on the shadow side, hair laid down in big tapering locks scratched with strokes along their flow,
// flat colours with a hard-edged shade, thin black limbs and pointed shoes. Wilson: a long face cut straight across at
// the hairline, his hair rising behind it in a big lock on the left, a curl in the middle and a hook on the right, tall
// dark eyes in olive shadows, a check-mark nose, white shirtsleeves and a pinstriped red vest. Willow: a dark bob with
// ragged bangs and two pigtails that hook upwards, blank staring eyes under heavy brows, a red blouse and a pinstriped
// black skirt, her lighter in one hand and Bernie in the other. And two Oxygen Not Included
// duplicants (a wide bean head, dot eyes, a grey suit with a yellow collar): Bumpo with short black hair and
// Ollmeister with a cloud of curls. Fan tributes, not affiliated with Klei Entertainment (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindWilson = 76, KindWillow = 77, KindDupe = 78, KindDupeCurly = 79, KindAbigail = 87;

        public static bool IsKlei(int kind) { return (kind >= KindWilson && kind <= KindDupeCurly) || kind == KindAbigail; }
        static bool IsDupe(int kind) { return kind == KindDupe || kind == KindDupeCurly; }
        public static bool IsStarve(int kind) { return kind == KindWilson || kind == KindWillow || kind == KindAbigail; }

        // The characters whose arms are drawn to their hands, also to the ones holding a prop.
        public static bool HasDrawnArms(int kind) { return (IsStarve(kind) && kind != KindAbigail) || IsToon(kind); }

        static readonly Color KInk = Color.FromArgb(34, 26, 24);
        internal static readonly Color DsInkC = Color.FromArgb(26, 20, 20);

        // ---- Don't Starve's drawing.

        // A fixed pseudo-random number in [-1, 1] for a seed, so the hand-drawn wobble stays put from frame to frame.
        static float DsJ(int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }

        // Points along a run of cubic curves (a start point, then three per curve, in units of D) from xy[from].
        static PointF[] DsSpine(float D, int per, float[] xy, int from)
        {
            int curves = ((xy.Length - from) / 2 - 1) / 3;
            PointF[] pts = new PointF[curves * per + 1];
            for (int c = 0; c < curves; c++)
            {
                int o = from + c * 6;
                PointF a = P(xy[o] * D, xy[o + 1] * D), b = P(xy[o + 2] * D, xy[o + 3] * D), cc = P(xy[o + 4] * D, xy[o + 5] * D), d = P(xy[o + 6] * D, xy[o + 7] * D);
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)per, u = 1 - t;
                    pts[c * per + i] = P(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * cc.X + t * t * t * d.X,
                                         u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * cc.Y + t * t * t * d.Y);
                }
                if (c == curves - 1) pts[curves * per] = d;
            }
            return pts;
        }

        static readonly Func<float, float> DsTaper = t => (float)Math.Pow(Math.Sin(Math.PI * t), 0.6);
        static readonly Func<float, float> DsLockW = t => (float)Math.Pow(1 - t, 0.75);

        // The outline of a brush stroke along a line of points, w wide where the profile (0-1 along it) is 1.
        static GraphicsPath DsRibbon(PointF[] pts, float w, Func<float, float> profile)
        {
            int n = pts.Length;
            PointF[] poly = new PointF[n * 2];
            for (int i = 0; i < n; i++)
            {
                PointF a = pts[Math.Max(0, i - 1)], b = pts[Math.Min(n - 1, i + 1)];
                float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-4f) { dx = 1; dy = 0; len = 1; }
                float hw = w * 0.5f * profile(i / (float)(n - 1));
                poly[i] = P(pts[i].X - dy / len * hw, pts[i].Y + dx / len * hw);
                poly[n * 2 - 1 - i] = P(pts[i].X + dy / len * hw, pts[i].Y - dx / len * hw);
            }
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(poly);
            return p;
        }

        // A brush stroke that swells in the middle and tapers to a point at both ends.
        internal static void DsStroke(Graphics g, Color c, float w, PointF[] pts)
        {
            using (GraphicsPath p = DsRibbon(pts, w, DsTaper)) FillSolid(g, p, c);
        }

        internal static void DsStroke(Graphics g, Color c, float w, float D, params float[] xy)
        {
            DsStroke(g, c, w, DsSpine(D, 10, xy, 0));
        }

        // The ink line round a shape, laid under its colour and shifted down and to the right: the colour trims its
        // inner half, so it swells on the shadow side and thins towards the light.
        static void DsUnder(Graphics g, GraphicsPath p, float D)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(D * 0.003f, D * 0.004f);
            Stroke(g, p, DsInkC, Math.Max(2f, D * 0.024f));
            g.Restore(st);
        }

        // Over the colour: a fine line on the edge and, on the big shapes, a loose pencil pass turned a fraction of a
        // degree.
        static void DsInk(Graphics g, GraphicsPath p, float D, bool pencil)
        {
            Stroke(g, p, DsInkC, Math.Max(1f, D * 0.009f));
            if (!pencil) return;
            RectangleF rb = p.GetBounds();
            float cx = rb.X + rb.Width / 2, cy = rb.Y + rb.Height / 2;
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(1.1f);
            g.ScaleTransform(1.012f, 0.994f);
            g.TranslateTransform(-cx, -cy);
            Stroke(g, p, Color.FromArgb(80, DsInkC), Math.Max(0.6f, D * 0.0045f));
            g.Restore(st);
        }

        // A flat colour and, on the side away from the light, a hard-edged shade: what the shape leaves uncovered
        // when moved up and to the left. Hatched with pencil strokes when hatch > 0.
        static void DsFill(Graphics g, GraphicsPath p, Color fill, Color shade, float D, float depth, int hatch)
        {
            FillSolid(g, p, fill);
            if (depth <= 0) return;
            RectangleF r = p.GetBounds();
            Region old = g.Clip;
            g.SetClip(p, CombineMode.Intersect);
            using (GraphicsPath lit = (GraphicsPath)p.Clone())
            using (Matrix m = new Matrix())
            {
                m.Translate(-Math.Min(depth * D, r.Width * 0.22f), -Math.Min(depth * D * 0.75f, r.Height * 0.18f));
                lit.Transform(m);
                g.SetClip(lit, CombineMode.Exclude);
            }
            using (SolidBrush b = new SolidBrush(shade)) g.FillRectangle(b, r);
            if (hatch > 0)
                using (Pen h = new Pen(Color.FromArgb(Math.Min(220, 70 * hatch), DsInkC), Math.Max(0.7f, D * 0.005f)))
                {
                    h.StartCap = h.EndCap = LineCap.Round;
                    for (float x = r.X; x < r.Right + r.Height; x += D * 0.016f) g.DrawLine(h, x, r.Bottom, x - r.Height * 0.55f, r.Y);
                }
            g.Clip = old;
            old.Dispose();
        }

        static void DsShape(Graphics g, GraphicsPath p, Color fill, Color shade, float D, int hatch)
        {
            DsUnder(g, p, D);
            DsFill(g, p, fill, shade, D, 0.03f, hatch);
            DsInk(g, p, D, false);
        }

        // A small part in flat colour, inked.
        static void DsFlat(Graphics g, GraphicsPath p, Color fill, float D)
        {
            DsUnder(g, p, D);
            FillSolid(g, p, fill);
            DsInk(g, p, D, false);
        }

        static Bitmap dsGrain;

        // The paper the drawings are coloured on: soft light and dark mottling, made once and tiled.
        static Bitmap DsGrain()
        {
            if (dsGrain != null) return dsGrain;
            const int n = 96, c = 6;
            Bitmap b = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Smoothly blended coarse noise that wraps round the tile, and a little fine grain on top.
                    float fx = x * c / (float)n, fy = y * c / (float)n;
                    int ix = (int)fx, iy = (int)fy;
                    float tx = fx - ix, ty = fy - iy;
                    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                    Func<int, int, float> v = (i, j) => DsJ((i % c) * 131 + (j % c) * 977 + 5);
                    float val = (v(ix, iy) * (1 - tx) + v(ix + 1, iy) * tx) * (1 - ty) + (v(ix, iy + 1) * (1 - tx) + v(ix + 1, iy + 1) * tx) * ty;
                    val = val * 0.8f + DsJ(x * 7919 + y * 104729) * 0.2f;
                    int a = Math.Min(255, (int)(Math.Abs(val) * 34));
                    b.SetPixel(x, y, val > 0 ? Color.FromArgb(a, 255, 252, 244) : Color.FromArgb(a, 96, 76, 56));
                }
            dsGrain = b;
            return b;
        }

        static readonly object dsGrainLock = new object();

        static void DsPaper(Graphics g, GraphicsPath p, float D)
        {
            // The brush copies the shared tile as it is made: one thread at a time (the stage, the desktop pet and the
            // workers drawing previews and looks ahead all draw paper), or GDI+ finds the tile in use.
            TextureBrush brush;
            lock (dsGrainLock) brush = new TextureBrush(DsGrain(), WrapMode.Tile);
            using (TextureBrush tb = brush)
            {
                float sc = D * 0.42f / 96f;
                tb.ScaleTransform(sc, sc);
                g.FillPath(tb, p);
            }
        }

        // A line along a lock at a fraction u (-1 to 1) of its half-width, from t0 to t1 of its length.
        static PointF[] DsAlong(PointF[] spine, float w, float u, float t0, float t1)
        {
            int n = spine.Length, m = 10;
            PointF[] r = new PointF[m];
            for (int i = 0; i < m; i++)
            {
                float t = t0 + (t1 - t0) * i / (m - 1), f = t * (n - 1);
                int a = Math.Min(n - 2, (int)f);
                float fr = f - a, dx = spine[a + 1].X - spine[a].X, dy = spine[a + 1].Y - spine[a].Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-4f) { dx = 1; dy = 0; len = 1; }
                float hw = w * 0.5f * DsLockW(t) * u;
                r[i] = P(spine[a].X + dx * fr - dy / len * hw, spine[a].Y + dy * fr + dx / len * hw);
            }
            return r;
        }

        // A lock of hair on its own: a broad stroke from its root to a sharp tip along a curve (k: width, then the
        // curve), inked, filled and scratched with strokes along it.
        static void DsLock(Graphics g, float D, float[] k, int seed)
        {
            using (GraphicsPath p = DsRibbon(DsSpine(D, 12, k, 1), k[0] * D, DsLockW))
            {
                DsUnder(g, p, D);
                FillSolid(g, p, DsHair);
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                DsStrands(g, D, k, seed, Color.FromArgb(180, 176, 170, 176), 2, 0.008f, 0.015f, 80);
                g.Clip = old;
                old.Dispose();
                DsInk(g, p, D, true);
            }
        }

        // A thin black limb: one ink stroke.
        static void DsStick(Graphics g, float D, float x1, float y1, float x2, float y2)
        {
            using (Pen p = new Pen(DsInkC, Math.Max(1.4f, 0.022f * D))) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, x1 * D, y1 * D, x2 * D, y2 * D); }
        }

        // A pointed black shoe, its toe turned out and curling up.
        static void DsShoe(Graphics g, float D, float x, float y, int s)
        {
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddBezier(P((x - s * 0.02f) * D, (y + 0.011f) * D), P((x - s * 0.026f) * D, (y - 0.012f) * D), P((x + s * 0.016f) * D, (y - 0.015f) * D), P((x + s * 0.05f) * D, (y - 0.008f) * D));
                sp.AddBezier(P((x + s * 0.05f) * D, (y - 0.008f) * D), P((x + s * 0.064f) * D, (y - 0.011f) * D), P((x + s * 0.076f) * D, (y - 0.018f) * D), P((x + s * 0.084f) * D, (y - 0.026f) * D));
                sp.AddBezier(P((x + s * 0.084f) * D, (y - 0.026f) * D), P((x + s * 0.08f) * D, (y + 0.003f) * D), P((x + s * 0.036f) * D, (y + 0.016f) * D), P((x - s * 0.02f) * D, (y + 0.011f) * D));
                sp.CloseFigure();
                DsFlat(g, sp, Color.FromArgb(44, 38, 42), D);
            }
        }

        // Where PaintHands puts each hand (in units of D), so the arms reach them.
        static PointF DsHand(MascotPose p, Geo geo, int side)
        {
            float hy = geo.HandY + (float)(Math.Sin(p.Now / 2600.0 * Math.PI * 2 + side * 0.9) * 0.02);
            float hx = side * geo.HandX;
            hy += HandSwing(p, side);
            if (side > 0 && p.Wave > 0) { hy -= (float)(0.2 * p.Wave); hx += (float)(0.03 * p.Wave + StarveWaveOut * p.Wave); }
            return P(hx, hy);
        }

        // A waving hand goes out this much further, clear of the big head and hair (PaintHands too).
        internal const float StarveWaveOut = 0.1f;

        // A small pale hand in ink (PaintHands).
        internal static void StarveHand(Graphics g, float D, int kind, Color skin)
        {
            if (IsToon(kind)) { ToonHand(g, D, kind); return; }
            float k = kind == KindWilson ? 0.84f : 1f;   // his are smaller, at the ends of thinner arms
            using (GraphicsPath hp = new GraphicsPath())
            {
                hp.AddEllipse(-D * 0.028f * k, -D * 0.025f * k, D * 0.056f * k, D * 0.05f * k);
                DsFlat(g, hp, skin, D);
            }
        }

        // The head (PaintBody): the ink under it, the pale skin and the paper; the fine ink goes on in BodyInk. Abigail's
        // sheet gets the pencil strokes that run down into its torn hem.
        static void StarveHead(Graphics g, int kind, GraphicsPath body, float D, Color c1, Color c2)
        {
            DsUnder(g, body, D);
            DsFill(g, body, c1, Mac.Mix(c1, c2, 0.3), D, 0.02f, 0);
            DsPaper(g, body, D);
            if (kind != KindAbigail) return;
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            foreach (float[] s in AbigailFolds)
                DsStroke(g, Color.FromArgb(150, 60, 56, 50), 0.008f * D, D, s[0], s[1], s[0] + 0.002f, s[1] + (s[2] - s[1]) * 0.35f, s[0] + 0.004f, s[1] + (s[2] - s[1]) * 0.7f, s[0] + 0.006f, s[2]);
            g.Clip = old;
            old.Dispose();
        }

        // The ink over the head (BodyInk); Abigail's flower sits on top of her outline.
        static void StarveInk(Graphics g, int kind, GraphicsPath body, float D)
        {
            DsInk(g, body, D, true);
            if (kind == KindAbigail) AbigailFlower(g, D, 0.13f, -0.425f, 0.2f, 52f, false, true);
        }

        // ---- Abigail, Wendy's twin sister, as a ghost: a cream sheet, round at the top and torn into points at the hem,
        // with big hollow eyes and her red flower.

        // Pencil strokes down her sheet: x, top, bottom (in units of D).
        static readonly float[][] AbigailFolds =
        {
            new float[] { -0.2f, 0.02f, 0.13f }, new float[] { -0.155f, 0.09f, 0.2f }, new float[] { -0.112f, 0.13f, 0.24f },
            new float[] { -0.06f, 0.18f, 0.29f }, new float[] { 0.032f, 0.17f, 0.29f }, new float[] { 0.09f, 0.13f, 0.25f },
            new float[] { 0.142f, 0.09f, 0.2f }, new float[] { 0.2f, -0.02f, 0.11f },
        };

        // The torn hem, right to left: points hanging down and the notches between them (x, y pairs, in units of D).
        static readonly float[] AbigailHem =
        {
            0.236f, 0.13f, 0.205f, 0.115f, 0.2f, 0.2f, 0.165f, 0.175f, 0.15f, 0.26f, 0.122f, 0.225f, 0.1f, 0.305f, 0.075f, 0.262f,
            0.05f, 0.345f, 0.03f, 0.3f, 0.004f, 0.392f, -0.02f, 0.318f, -0.045f, 0.35f, -0.058f, 0.272f, -0.085f, 0.31f, -0.1f, 0.23f,
            -0.13f, 0.26f, -0.145f, 0.19f, -0.18f, 0.21f, -0.188f, 0.145f, -0.215f, 0.15f,
        };

        // Her outline: the rounded top and sides as smooth runs, the hem as straight tears whose points sway a little.
        static GraphicsPath AbigailPath(float D, double now)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddCurve(new PointF[] { P(0, -0.505f * D), P(0.095f * D, -0.488f * D), P(0.16f * D, -0.445f * D), P(0.205f * D, -0.38f * D), P(0.228f * D, -0.3f * D),
                                      P(0.238f * D, -0.2f * D), P(0.238f * D, -0.1f * D), P(0.232f * D, -0.01f * D), P(0.222f * D, 0.06f * D) }, 0.5f);
            PointF[] hem = new PointF[AbigailHem.Length / 2];
            for (int i = 0; i < hem.Length; i++)
            {
                float sw = i % 2 == 0 ? (float)Math.Sin(now / 650.0 + i * 1.3) * 0.007f : 0;
                hem[i] = P((AbigailHem[i * 2] + sw) * D, AbigailHem[i * 2 + 1] * D);
            }
            p.AddLines(hem);
            p.AddCurve(new PointF[] { P(-0.212f * D, 0.09f * D), P(-0.228f * D, 0f), P(-0.236f * D, -0.1f * D), P(-0.234f * D, -0.2f * D), P(-0.222f * D, -0.3f * D),
                                      P(-0.195f * D, -0.38f * D), P(-0.15f * D, -0.448f * D), P(-0.085f * D, -0.49f * D), P(0, -0.505f * D) }, 0.5f);
            p.CloseFigure();
            return p;
        }

        // Behind her: the soft light she gives off in the game.
        static void AbigailBack(Graphics g, float D)
        {
            Glow(g, 0, -0.06f * D, 0.74f * D, 1.08f * D, Color.FromArgb(255, 252, 226), 45);
        }

        // Abigail's Flower as the game draws it: a tuft of long pointed red petals fanning out from one point, the back ones
        // darker, each with a line down its middle, inked. At (x, y) in units of D, s high, turned by angle degrees; with a
        // little grass at its foot when it stands on the ground.
        // Petals: the angle from upright, the length and the width (fractions of the flower's height), back row first. The
        // item is a full fan of them; the one on Abigail's head, a tuft of a few broad ones.
        static readonly float[] FlowerFan = { -72, 0.8f, 0.26f, -46, 0.95f, 0.28f, -20, 1f, 0.28f, 6, 1f, 0.28f, 32, 0.95f, 0.28f, 58, 0.88f, 0.26f, 80, 0.74f, 0.24f,
                                              -56, 0.62f, 0.3f, -26, 0.72f, 0.31f, 4, 0.76f, 0.32f, 34, 0.7f, 0.31f, 62, 0.6f, 0.29f };
        static readonly float[] FlowerTuft = { -40, 0.9f, 0.36f, -10, 1f, 0.38f, 20, 0.96f, 0.36f, 48, 0.8f, 0.32f, -24, 0.7f, 0.4f, 10, 0.74f, 0.4f };

        internal static void AbigailFlower(Graphics g, float D, float x, float y, float s, float angle, bool grass, bool tuft)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x * D, y * D);
            g.RotateTransform(angle);
            float[] petals = tuft ? FlowerTuft : FlowerFan;
            for (int i = 0; i < petals.Length / 3; i++)
            {
                bool back = i < (tuft ? 4 : 7);
                double a = petals[i * 3] * Math.PI / 180;
                float len = petals[i * 3 + 1] * s * D, w = petals[i * 3 + 2] * s * D;
                float dx = (float)Math.Sin(a) * len, dy = -(float)Math.Cos(a) * len, nx = -dy / len, ny = dx / len;
                using (GraphicsPath pp = new GraphicsPath())
                {
                    pp.AddBezier(0, 0, dx * 0.25f + nx * w, dy * 0.25f + ny * w, dx * 0.65f + nx * w * 0.75f, dy * 0.65f + ny * w * 0.75f, dx, dy);
                    pp.AddBezier(dx, dy, dx * 0.65f - nx * w * 0.75f, dy * 0.65f - ny * w * 0.75f, dx * 0.25f - nx * w, dy * 0.25f - ny * w, 0, 0);
                    pp.CloseFigure();
                    FillSolid(g, pp, back ? Color.FromArgb(150, 38, 40) : Color.FromArgb(208, 70, 62));
                    Line(g, back ? Color.FromArgb(110, 22, 26) : Color.FromArgb(150, 36, 38), Math.Max(0.6f, D * s * 0.04f), dx * 0.15f, dy * 0.15f, dx * 0.8f, dy * 0.8f);
                    Stroke(g, pp, DsInkC, Math.Max(0.9f, D * s * 0.065f));
                }
            }
            if (grass)
                using (Pen gr = new Pen(DsInkC, Math.Max(0.8f, D * s * 0.05f)))
                {
                    gr.StartCap = gr.EndCap = LineCap.Round;
                    for (int i = -3; i <= 3; i++) g.DrawLine(gr, i * 0.14f * s * D, 0.02f * s * D, i * 0.17f * s * D, -0.1f * s * D);
                }
            g.Restore(st);
        }

        // ---- The rest of Klei's drawing, for the duplicants.

        // The ink: a thick dark line, and a finer one just beside it so it reads as drawn by hand.
        static void KleiInk(Graphics g, GraphicsPath p, float D)
        {
            Stroke(g, p, Color.FromArgb(240, KInk), Math.Max(1.4f, D * 0.018f));
            GraphicsState st = g.Save();
            g.TranslateTransform(D * 0.004f, D * 0.003f);
            Stroke(g, p, Color.FromArgb(110, KInk), Math.Max(0.8f, D * 0.007f));
            g.Restore(st);
            // A third, rougher pass a hair off the other way: the sketchy double line of the game's drawings.
            st = g.Save();
            g.TranslateTransform(-D * 0.003f, -D * 0.0025f);
            Stroke(g, p, Color.FromArgb(70, KInk), Math.Max(0.6f, D * 0.005f));
            g.Restore(st);
            // And two loose pencil passes, turned a fraction of a degree, as Klei's drawings go over each outline.
            RectangleF rb = p.GetBounds();
            float ccx = rb.X + rb.Width / 2, ccy = rb.Y + rb.Height / 2;
            foreach (float a in new float[] { 0.9f, -0.7f })
            {
                st = g.Save();
                g.TranslateTransform(ccx, ccy);
                g.RotateTransform(a);
                g.ScaleTransform(1.012f, 0.994f);
                g.TranslateTransform(-ccx, -ccy);
                Stroke(g, p, Color.FromArgb(85, KInk), Math.Max(0.6f, D * 0.005f));
                g.Restore(st);
            }
        }

        static void KleiShape(Graphics g, GraphicsPath p, Color a, Color b, float D)
        {
            RectangleF r = p.GetBounds();
            Fill(g, p, r, a, b, 90f);
            // A pencil tone along the inside of the edge.
            Region old = g.Clip;
            g.SetClip(p, CombineMode.Intersect);
            using (Pen edge = new Pen(Color.FromArgb(50, Darker(b, 0.35)), D * 0.045f)) g.DrawPath(edge, p);
            g.Clip = old;
            old.Dispose();
            KleiInk(g, p, D);
        }

        // Hand-drawn hatching on the shadow side (the right, away from the light), clipped to the shape.
        static void KHatch(Graphics g, GraphicsPath p, float D, int alpha)
        {
            RectangleF r = p.GetBounds();
            if (r.Width < 2 || r.Height < 2) return;
            Region old = g.Clip;
            g.SetClip(p, CombineMode.Intersect);
            using (Pen h = new Pen(Color.FromArgb(alpha, KInk), Math.Max(0.8f, D * 0.006f)))
            {
                h.StartCap = h.EndCap = LineCap.Round;
                float step = 0.028f * D;
                for (float x = r.X + r.Width * 0.55f; x < r.Right + r.Height; x += step)
                    g.DrawLine(h, x, r.Bottom, x - r.Height * 0.6f, r.Y);
            }
            g.Clip = old;
            old.Dispose();
        }

        static GraphicsPath KCurve(float D, float tension, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2] * D, xy[i * 2 + 1] * D);
            GraphicsPath p = new GraphicsPath();
            p.AddClosedCurve(pts, tension);
            return p;
        }

        static GraphicsPath KPoly(float D, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2] * D, xy[i * 2 + 1] * D);
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        // A limb: a dark ink line with the colour inside it.
        static void KLimb(Graphics g, float D, Color c, float w, float x1, float y1, float x2, float y2)
        {
            using (Pen ink = new Pen(KInk, (w + 0.026f) * D))
            using (Pen fill = new Pen(c, w * D))
            {
                ink.StartCap = ink.EndCap = fill.StartCap = fill.EndCap = LineCap.Round;
                g.DrawLine(ink, x1 * D, y1 * D, x2 * D, y2 * D);
                g.DrawLine(fill, x1 * D, y1 * D, x2 * D, y2 * D);
            }
        }

        static void KShoe(Graphics g, float D, float x, float y, Color c)
        {
            RectangleF r = new RectangleF((x - 0.05f) * D, (y - 0.025f) * D, 0.1f * D, 0.05f * D);
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddEllipse(r);
                KleiShape(g, sp, Lighter(c, 0.1), c, D);
            }
        }

        static Geo KleiGeo(int kind)
        {
            if (IsDupe(kind)) return G(0.54f, -0.32f, -0.06f, 0.09f, 0.13f, 0.42f, 0.14f, 0.23f, 0.2f, 0.52f);
            // Wilson as the game has him: a long narrow face set high, the eyes close together, a small body with the
            // hands hanging at its sides.
            if (kind == KindWilson) return G(0.44f, -0.43f, -0.16f, 0.065f, 0.1f, 0.3f, 0.1f, 0.135f, 0.25f, 0.44f);
            // Abigail: a tall sheet, its eyes in the upper third.
            if (kind == KindAbigail) return G(0.476f, -0.505f, -0.27f, 0.088f, 0.1f, 0.3f, 0.39f, 0.2f, 0.15f, 0.4f);
            // Willow: an egg of a face under her bangs, big eyes close together, her hands at her sides.
            return G(0.44f, -0.38f, -0.11f, 0.095f, 0.12f, 0.3f, 0.13f, 0.18f, 0.22f, 0.44f);
        }

        // Where the face is across (units of D): Shin-chan's, drawn three quarters on, sits off to the right of his middle.
        public static float FaceX(int kind) { return IsShin(kind) ? 0.038f : 0; }

        // Where the mouth is across, for what goes to it and for a moustache over it (Snoopy's, side on, at the front of
        // his smile under his snout).
        public static float MouthX(int kind) { return IsShin(kind) ? 0.135f : kind == KindSnoopySd ? 0.19f : 0; }

        // Where its eye is across, for a hand shading it.
        public static float EyeX(int kind) { return kind == KindSnoopySd ? 0.05f : FaceX(kind); }

        // How far ahead of its middle a character holds a console, a book or a camera: side on, in front of it.
        public static float HoldAhead(int kind) { return kind == KindSnoopySd ? 0.13f : 0; }

        // Where the mouth is (in units of D), for props brought up to it.
        public static float MouthY(int kind, Geo geo)
        {
            if (IsToon(kind)) return geo.FaceY + ToonMouthDy(kind);
            if (!IsKlei(kind)) return geo.FaceY + 0.085f;
            return geo.FaceY + (IsDupe(kind) ? 0.11f : 0.15f);
        }

        // The head only; the body, arms and legs are drawn behind it.
        static GraphicsPath KleiPath(int kind, float D, double now)
        {
            if (kind == KindAbigail) return AbigailPath(D, now);
            if (IsDupe(kind))
                return KCurve(D, 0.5f, 0, -0.32f, 0.2f, -0.29f, 0.27f, -0.12f, 0.27f, 0.04f, 0.18f, 0.12f, 0, 0.14f, -0.18f, 0.12f, -0.27f, 0.04f, -0.27f, -0.12f, -0.2f, -0.29f);
            if (kind == KindWilson)
            {
                // Wilson's face as the game draws it: cut straight across at the hairline, which dips to a point in the
                // middle, with near-straight sides that turn in a sharp V to the chin.
                GraphicsPath w = new GraphicsPath();
                w.AddLine(P(-0.22f * D, -0.43f * D), P(0, -0.395f * D));
                w.AddLine(P(0, -0.395f * D), P(0.22f * D, -0.43f * D));
                w.AddBezier(P(0.22f * D, -0.43f * D), P(0.218f * D, -0.33f * D), P(0.205f * D, -0.2f * D), P(0.19f * D, -0.08f * D));
                w.AddBezier(P(0.19f * D, -0.08f * D), P(0.17f * D, -0.02f * D), P(0.08f * D, 0.06f * D), P(0, 0.1f * D));
                w.AddBezier(P(0, 0.1f * D), P(-0.08f * D, 0.06f * D), P(-0.17f * D, -0.02f * D), P(-0.19f * D, -0.08f * D));
                w.AddBezier(P(-0.19f * D, -0.08f * D), P(-0.205f * D, -0.2f * D), P(-0.218f * D, -0.33f * D), P(-0.22f * D, -0.43f * D));
                w.CloseFigure();
                return w;
            }
            // Willow's face as the game draws it: an egg, round under her bangs and narrowing to a pointed chin.
            GraphicsPath p = new GraphicsPath();
            p.AddBezier(P(0, 0.13f * D), P(0.06f * D, 0.1f * D), P(0.18f * D, 0.025f * D), P(0.212f * D, -0.06f * D));
            p.AddBezier(P(0.212f * D, -0.06f * D), P(0.232f * D, -0.13f * D), P(0.232f * D, -0.22f * D), P(0.2f * D, -0.3f * D));
            p.AddBezier(P(0.2f * D, -0.3f * D), P(0.165f * D, -0.36f * D), P(0.085f * D, -0.39f * D), P(0, -0.39f * D));
            p.AddBezier(P(0, -0.39f * D), P(-0.085f * D, -0.39f * D), P(-0.165f * D, -0.36f * D), P(-0.2f * D, -0.3f * D));
            p.AddBezier(P(-0.2f * D, -0.3f * D), P(-0.232f * D, -0.22f * D), P(-0.232f * D, -0.13f * D), P(-0.212f * D, -0.06f * D));
            p.AddBezier(P(-0.212f * D, -0.06f * D), P(-0.18f * D, 0.025f * D), P(-0.06f * D, 0.1f * D), P(0, 0.13f * D));
            p.CloseFigure();
            return p;
        }

        // Wilson's hair as the game draws it, rising from behind his face: on the left a big lock sweeping up to a point,
        // in the middle a lock that curls over to the left round a hole, on the right one that hooks over the same way,
        // and short locks down his sides to the ears. Its outline as smooth runs meeting in sharp points, clockwise from
        // behind his left ear.
        static readonly float[][] WilsonHairEdge =
        {
            new float[] { -0.19f, -0.24f, -0.235f, -0.25f, -0.262f, -0.29f },
            new float[] { -0.262f, -0.29f, -0.3f, -0.33f, -0.335f, -0.36f },
            new float[] { -0.335f, -0.36f, -0.3f, -0.385f, -0.28f, -0.405f },
            new float[] { -0.28f, -0.405f, -0.31f, -0.45f, -0.345f, -0.505f },
            new float[] { -0.345f, -0.505f, -0.3f, -0.52f, -0.275f, -0.535f },
            new float[] { -0.275f, -0.535f, -0.28f, -0.6f, -0.29f, -0.665f },
            new float[] { -0.29f, -0.665f, -0.24f, -0.66f, -0.215f, -0.655f },
            new float[] { -0.215f, -0.655f, -0.17f, -0.72f, -0.12f, -0.755f, -0.06f, -0.778f },
            new float[] { -0.06f, -0.778f, -0.1f, -0.735f, -0.135f, -0.675f, -0.142f, -0.6f, -0.13f, -0.535f, -0.11f, -0.505f },
            new float[] { -0.11f, -0.505f, -0.04f, -0.49f, 0.025f, -0.5f },
            new float[] { 0.025f, -0.5f, 0.036f, -0.56f, 0.04f, -0.625f, 0.03f, -0.675f, 0f, -0.695f, -0.035f, -0.688f, -0.058f, -0.664f },
            new float[] { -0.058f, -0.664f, -0.04f, -0.705f, 0.005f, -0.728f, 0.055f, -0.725f, 0.1f, -0.68f, 0.14f, -0.615f, 0.168f, -0.55f, 0.178f, -0.5f },
            new float[] { 0.178f, -0.5f, 0.22f, -0.49f, 0.262f, -0.5f },
            new float[] { 0.262f, -0.5f, 0.272f, -0.575f, 0.266f, -0.635f, 0.243f, -0.662f, 0.205f, -0.668f },
            new float[] { 0.205f, -0.668f, 0.25f, -0.69f, 0.3f, -0.705f, 0.33f, -0.71f },
            new float[] { 0.33f, -0.71f, 0.345f, -0.65f, 0.35f, -0.58f, 0.36f, -0.52f, 0.39f, -0.47f },
            new float[] { 0.39f, -0.47f, 0.345f, -0.45f, 0.32f, -0.42f },
            new float[] { 0.32f, -0.42f, 0.34f, -0.37f, 0.37f, -0.33f },
            new float[] { 0.37f, -0.33f, 0.31f, -0.32f, 0.285f, -0.3f },
            new float[] { 0.285f, -0.3f, 0.29f, -0.26f, 0.3f, -0.225f },
            new float[] { 0.3f, -0.225f, 0.26f, -0.215f, 0.22f, -0.2f },
            new float[] { 0.22f, -0.2f, 0f, -0.3f, -0.19f, -0.24f },
        };

        // How its strokes run: a spread at the root, then the curve from the root to the point they end in.
        static readonly float[][] WilsonHairFlow =
        {
            new float[] { 0.07f, -0.19f, -0.3f, -0.24f, -0.32f, -0.29f, -0.34f, -0.335f, -0.36f },
            new float[] { 0.1f, -0.18f, -0.37f, -0.24f, -0.41f, -0.3f, -0.46f, -0.345f, -0.505f },
            new float[] { 0.1f, -0.16f, -0.45f, -0.21f, -0.52f, -0.25f, -0.6f, -0.29f, -0.665f },
            new float[] { 0.2f, -0.1f, -0.42f, -0.17f, -0.52f, -0.17f, -0.68f, -0.065f, -0.775f },
            new float[] { 0.17f, 0.1f, -0.43f, 0.12f, -0.56f, 0.09f, -0.72f, -0.05f, -0.68f },
            new float[] { 0.13f, 0.3f, -0.43f, 0.33f, -0.56f, 0.33f, -0.7f, 0.21f, -0.67f },
            new float[] { 0.09f, 0.24f, -0.41f, 0.3f, -0.43f, 0.35f, -0.45f, 0.39f, -0.47f },
            new float[] { 0.08f, 0.22f, -0.34f, 0.27f, -0.33f, 0.32f, -0.33f, 0.37f, -0.33f },
            new float[] { 0.06f, 0.22f, -0.26f, 0.25f, -0.25f, 0.28f, -0.24f, 0.3f, -0.225f },
        };

        // A closed outline of smooth runs that meet in sharp points, each run starting where the last one ended.
        static GraphicsPath DsRuns(float D, float tension, float[][] runs)
        {
            GraphicsPath p = new GraphicsPath();
            foreach (float[] r in runs)
            {
                PointF[] pts = new PointF[r.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = P(r[i * 2] * D, r[i * 2 + 1] * D);
                p.AddCurve(pts, tension);
            }
            p.CloseFigure();
            return p;
        }

        static readonly Color DsHair = Color.FromArgb(42, 38, 42);

        // Hair strokes along a flow (spread, then the curve), converging on its point: black ones, and a few pale ones
        // where the paper shows through. Drawn clipped to the hair.
        static void DsStrands(Graphics g, float D, float[] k, int seed)
        {
            DsStrands(g, D, k, seed, Color.FromArgb(170, 176, 170, 176), 3, 0.007f, 0.018f, 44);
        }

        // Every so many strokes a pale one, of its own colour; widths of the pale and dark ones; density: strokes per
        // unit of spread.
        static void DsStrands(Graphics g, float D, float[] k, int seed, Color pale, int every, float paleW, float darkW, float density)
        {
            float w = k[0] * D;
            PointF[] spine = DsSpine(D, 12, k, 1);
            int n = Math.Max(4, (int)(k[0] * density));
            for (int i = 0; i < n; i++)
            {
                float u = -0.95f + 1.9f * (i + 0.5f + 0.35f * DsJ(seed + i)) / n;
                float t0 = 0.02f + 0.25f * (0.5f + 0.5f * DsJ(seed + 50 + i)), t1 = 0.6f + 0.38f * (0.5f + 0.5f * DsJ(seed + 90 + i));
                bool p = (i + seed) % every == 0;
                DsStroke(g, p ? pale : Color.FromArgb(240, 8, 6, 8), (p ? paleW : darkW) * D, DsAlong(spine, w, u, t0, t1));
            }
        }

        sealed class PartCache
        {
            public readonly System.Collections.Generic.Dictionary<long, CachedPart> Parts = new System.Collections.Generic.Dictionary<long, CachedPart>();
            public readonly long Budget;
            public long Pixels, Clock;
            public PartCache(long budget) { Budget = budget; }
        }

        sealed class CachedPart
        {
            public Bitmap Image;
            public long Used;
        }

        [ThreadStatic] static PartCache dsCache;

        // Parts that never move (hair, heads, clothes) are the costliest to draw, so each size of one is drawn once, at
        // twice the resolution, scaled down into a bitmap (per thread: GDI+ bitmaps can't be shared) and copied in from
        // then on. Each thread keeps the parts it used last within a budget of pixels, so the stage and the desktop pet
        // keep theirs while a sweep over the dock tries characters on; the dock's workers only need the one they draw.
        // u is the area a part covers, in units of D.
        internal static void DsCached(Graphics g, float D, int id, RectangleF u, Action<Graphics> draw)
        {
            PartCache c = dsCache;
            if (c == null) dsCache = c = NewPartCache();
            long key = (long)id * 1000000 + (int)Math.Round(D * 4);
            CachedPart p;
            if (!c.Parts.TryGetValue(key, out p))
            {
                Bitmap b;
                using (Bitmap big = new Bitmap(Math.Max(1, (int)Math.Ceiling(u.Width * D * 2)), Math.Max(1, (int)Math.Ceiling(u.Height * D * 2)), System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    using (Graphics bg = Graphics.FromImage(big))
                    {
                        bg.SmoothingMode = SmoothingMode.AntiAlias;
                        bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        bg.ScaleTransform(2, 2);
                        bg.TranslateTransform(-u.X * D, -u.Y * D);
                        draw(bg);
                    }
                    b = new Bitmap((big.Width + 1) / 2, (big.Height + 1) / 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (Graphics sg = Graphics.FromImage(b))
                    {
                        sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        sg.DrawImage(big, new RectangleF(0, 0, big.Width / 2f, big.Height / 2f));
                    }
                }
                long px = (long)b.Width * b.Height;
                while (c.Parts.Count > 0 && c.Pixels + px > c.Budget) DropOldestPart(c);
                p = new CachedPart { Image = b };
                c.Parts[key] = p;
                c.Pixels += px;
            }
            p.Used = ++c.Clock;
            g.DrawImage(p.Image, new RectangleF(u.X * D, u.Y * D, p.Image.Width, p.Image.Height));
        }

        // The UI thread (stage, desktop pet) keeps more than a worker that draws one preview at a time.
        static PartCache NewPartCache()
        {
            return new PartCache(System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA ? 2500000 : 400000);
        }

        // Runs draw on this thread with an empty cache of its own and hands back the parts it drew, now the caller's
        // (MascotWarm takes them to the UI thread).
        internal static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, Bitmap>> DrawParts(Action draw)
        {
            PartCache keep = dsCache, mine = new PartCache(long.MaxValue);
            dsCache = mine;
            try { draw(); }
            catch
            {
                foreach (CachedPart p in mine.Parts.Values) p.Image.Dispose();
                throw;
            }
            finally { dsCache = keep; }
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, Bitmap>> got = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, Bitmap>>(mine.Parts.Count);
            foreach (System.Collections.Generic.KeyValuePair<long, CachedPart> kv in mine.Parts) got.Add(new System.Collections.Generic.KeyValuePair<long, Bitmap>(kv.Key, kv.Value.Image));
            return got;
        }

        // Takes parts drawn on another thread into this thread's cache, as if drawn here just now (one it already has is
        // let go).
        internal static void AdoptParts(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, Bitmap>> parts)
        {
            if (parts == null || parts.Count == 0) return;
            PartCache c = dsCache;
            if (c == null) dsCache = c = NewPartCache();
            foreach (System.Collections.Generic.KeyValuePair<long, Bitmap> kv in parts)
            {
                if (c.Parts.ContainsKey(kv.Key)) { kv.Value.Dispose(); continue; }
                long px = (long)kv.Value.Width * kv.Value.Height;
                while (c.Parts.Count > 0 && c.Pixels + px > c.Budget) DropOldestPart(c);
                c.Parts[kv.Key] = new CachedPart { Image = kv.Value, Used = ++c.Clock };
                c.Pixels += px;
            }
        }

        // A worker about to end lets its parts go at once instead of leaving them to the finalizer.
        internal static void DropPartCache()
        {
            PartCache c = dsCache;
            dsCache = null;
            if (c == null) return;
            foreach (CachedPart p in c.Parts.Values) p.Image.Dispose();
            c.Parts.Clear();
        }

        static void DropOldestPart(PartCache c)
        {
            long oldest = 0, at = long.MaxValue;
            foreach (System.Collections.Generic.KeyValuePair<long, CachedPart> kv in c.Parts)
                if (kv.Value.Used < at) { at = kv.Value.Used; oldest = kv.Key; }
            CachedPart p = c.Parts[oldest];
            c.Parts.Remove(oldest);
            c.Pixels -= (long)p.Image.Width * p.Image.Height;
            p.Image.Dispose();
        }

        // Dark brown-black, streaked with light where the strokes leave the paper showing, as in the game.
        static void WilsonHair(Graphics g, float D)
        {
            DsCached(g, D, 1, new RectangleF(-0.37f, -0.82f, 0.8f, 0.66f), bg =>
            {
                using (GraphicsPath h = DsRuns(D, 0.5f, WilsonHairEdge))
                {
                    DsUnder(bg, h, D);
                    FillSolid(bg, h, Color.FromArgb(44, 34, 34));
                    bg.SetClip(h, CombineMode.Intersect);
                    for (int i = 0; i < WilsonHairFlow.Length; i++)
                        DsStrands(bg, D, WilsonHairFlow[i], 23 + i * 37, Color.FromArgb(220, 216, 200, 160), 2, 0.009f, 0.014f, 100);
                    bg.ResetClip();
                    DsInk(bg, h, D, true);
                }
            });
        }

        // ---- Behind the head: hair, then the body with its clothes, arms, legs and shoes.
        static void KleiBack(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            if (IsStarve(l.Kind)) { StarveBack(g, l, D, c1, p); return; }
            // A duplicant's neck and suit: light grey with a yellow collar and a dark belt; short legs.
            using (GraphicsPath nk = KPoly(D, -0.035f, 0.08f, 0.035f, 0.08f, 0.04f, 0.14f, -0.04f, 0.14f)) KleiShape(g, nk, c1, c2, D);
            float bob = (float)Math.Sin(p.Now / 2600.0 * Math.PI * 2) * 0.004f;
            Color suit = Color.FromArgb(206, 204, 194), pants = Color.FromArgb(74, 78, 90);
            foreach (int s in Sides)
            {
                PointF ft = Stride(p, s);
                KLimb(g, D, pants, 0.05f, s * 0.05f, 0.27f, s * 0.055f + ft.X, 0.32f + ft.Y);
                KShoe(g, D, s * 0.065f + ft.X, 0.34f + ft.Y, Color.FromArgb(44, 44, 52));
            }
            KLimb(g, D, suit, 0.05f, -0.1f, 0.15f, -0.2f, 0.19f + bob);
            KLimb(g, D, suit, 0.05f, 0.1f, 0.15f, 0.2f, 0.19f + bob);
            using (GraphicsPath sp = Theme.Round(new RectangleF(-0.13f * D, 0.11f * D, 0.26f * D, 0.17f * D), 0.05f * D)) { KleiShape(g, sp, suit, Color.FromArgb(166, 164, 156), D); KHatch(g, sp, D, 45); }
            Line(g, Color.FromArgb(70, 60, 50), D * 0.02f, -0.12f * D, 0.235f * D, 0.12f * D, 0.235f * D);
            using (GraphicsPath col = Theme.Round(new RectangleF(-0.08f * D, 0.105f * D, 0.16f * D, 0.04f * D), 0.02f * D)) KleiShape(g, col, Color.FromArgb(246, 204, 70), Color.FromArgb(214, 160, 30), D);
        }

        static void StarveBack(Graphics g, MascotLook l, float D, Color skin, MascotPose p)
        {
            Geo geo = KleiGeo(l.Kind);
            if (l.Kind == KindWilson) { WilsonHair(g, D); WilsonClothes(g, D, p, geo, skin); }
            else if (l.Kind == KindAbigail) AbigailBack(g, D);
            else { WillowHair(g, D, skin); WillowClothes(g, D, p, geo, skin); }
        }

        // An arm from the shoulder to the hand h (in units of D): Wilson's white shirtsleeve to the elbow, with its cuff,
        // and a thin black forearm; Willow's thin pale arm. Also drawn by the props, to the hands that hold them.
        internal static void StarveArm(Graphics g, float D, int kind, int side, PointF h, Color skin)
        {
            if (IsToon(kind)) { ToonArm(g, D, kind, side, h); return; }
            if (kind == KindWilson)
            {
                PointF s = P(side * 0.07f, 0.1f);
                PointF e = P(s.X + (h.X - s.X) * 0.58f + side * 0.01f, s.Y + (h.Y - s.Y) * 0.58f);
                DsStick(g, D, e.X, e.Y, h.X, h.Y);
                using (Pen ink = new Pen(DsInkC, Math.Max(2.4f, 0.052f * D)))
                using (Pen sl = new Pen(Color.FromArgb(238, 234, 224), Math.Max(1.4f, 0.034f * D)))
                {
                    ink.StartCap = ink.EndCap = sl.StartCap = sl.EndCap = LineCap.Round;
                    g.DrawLine(ink, s.X * D, s.Y * D, e.X * D, e.Y * D);
                    g.DrawLine(sl, s.X * D, s.Y * D, e.X * D, e.Y * D);
                }
                float dx = e.X - s.X, dy = e.Y - s.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001f)
                {
                    float cx = s.X + dx * 0.78f, cy = s.Y + dy * 0.78f, nx = -dy / len * 0.017f, ny = dx / len * 0.017f;
                    Line(g, DsInkC, Math.Max(0.8f, D * 0.007f), (cx - nx) * D, (cy - ny) * D, (cx + nx) * D, (cy + ny) * D);
                }
                return;
            }
            PointF[] ap = DsSpine(D, 6, new float[] { side * 0.085f, 0.13f, side * 0.11f, 0.15f, (h.X + side * 0.1f) / 2, h.Y + 0.005f, h.X, h.Y }, 0);
            using (GraphicsPath arm = DsRibbon(ap, 0.024f * D, t => 1f)) DsFlat(g, arm, skin, D);
        }

        static void WilsonClothes(Graphics g, float D, MascotPose p, Geo geo, Color skin)
        {
            Color shirt = Color.FromArgb(238, 234, 224), vest = Color.FromArgb(178, 66, 58), vestShade = Color.FromArgb(130, 40, 38);
            using (GraphicsPath nk = KPoly(D, -0.022f, 0.05f, 0.022f, 0.05f, 0.025f, 0.1f, -0.025f, 0.1f)) DsFlat(g, nk, skin, D);
            // Thin black legs close together, in pointed shoes with the toes turned out.
            foreach (int s in Sides)
            {
                PointF ft = Stride(p, s);
                using (Pen lg = new Pen(DsInkC, Math.Max(1.6f, 0.028f * D))) { lg.StartCap = lg.EndCap = LineCap.Round; g.DrawLine(lg, s * 0.024f * D, 0.235f * D, (s * 0.026f + ft.X) * D, (0.356f + ft.Y) * D); }
                DsShoe(g, D, s * 0.032f + ft.X, 0.364f + ft.Y, s);
            }
            // His arms hang from the shoulders to the hands; when he holds something, the prop draws them to it.
            if (!p.HideHands)
                foreach (int s in Sides) StarveArm(g, D, KindWilson, s, DsHand(p, geo, s), skin);
            using (GraphicsPath tr = Theme.Round(new RectangleF(-0.068f * D, 0.21f * D, 0.136f * D, 0.045f * D), 0.012f * D)) DsFlat(g, tr, Color.FromArgb(42, 36, 40), D);
            using (GraphicsPath sh = KPoly(D, -0.074f, 0.085f, 0.074f, 0.085f, 0.07f, 0.225f, -0.07f, 0.225f)) DsFlat(g, sh, shirt, D);
            // The vest: two pinstriped red panels with a V at the neck and a point at the bottom of each, buttons down one side.
            foreach (int s in Sides)
                using (GraphicsPath vp = KPoly(D, s * 0.078f, 0.09f, s * 0.03f, 0.088f, 0, 0.145f, 0, 0.222f, s * 0.028f, 0.24f, s * 0.074f, 0.226f))
                {
                    DsUnder(g, vp, D);
                    DsFill(g, vp, vest, vestShade, D, 0.02f, 0);
                    Region ov = g.Clip;
                    g.SetClip(vp, CombineMode.Intersect);
                    for (int i = 0; i < 4; i++)
                        Line(g, Color.FromArgb(170, 112, 30, 30), Math.Max(0.7f, D * 0.0055f), s * (0.012f + i * 0.017f) * D, 0.08f * D, s * (0.014f + i * 0.017f) * D, 0.25f * D);
                    g.Clip = ov;
                    ov.Dispose();
                    DsInk(g, vp, D, false);
                }
            for (int i = 0; i < 3; i++) Ellipse(g, Color.FromArgb(48, 26, 26), 0.006f * D, (0.158f + i * 0.02f) * D, 0.01f * D, 0.01f * D);
            foreach (int s in Sides)
                using (GraphicsPath cp = KPoly(D, s * 0.006f, 0.088f, s * 0.046f, 0.082f, s * 0.026f, 0.122f)) DsFlat(g, cp, shirt, D);
            // His round ears, beside his face at the height of his eyes, with the fold inside.
            foreach (int s in Sides)
            {
                using (GraphicsPath ep = new GraphicsPath())
                {
                    ep.AddBezier(P(s * 0.2f * D, -0.205f * D), P(s * 0.25f * D, -0.215f * D), P(s * 0.272f * D, -0.16f * D), P(s * 0.262f * D, -0.13f * D));
                    ep.AddBezier(P(s * 0.262f * D, -0.13f * D), P(s * 0.252f * D, -0.1f * D), P(s * 0.225f * D, -0.09f * D), P(s * 0.198f * D, -0.095f * D));
                    ep.CloseFigure();
                    DsFlat(g, ep, skin, D);
                }
                DsStroke(g, Color.FromArgb(190, DsInkC), 0.007f * D, D, s * 0.215f, -0.178f, s * 0.24f, -0.172f, s * 0.247f, -0.142f, s * 0.228f, -0.12f);
            }
        }

        static void WillowHair(Graphics g, float D, Color skin)
        {
            DsCached(g, D, 2, new RectangleF(-0.57f, -0.52f, 1.14f, 0.63f), bg =>
            {
                // The pigtails, from under the bob at her jaw: out and a little down, then up past her cheeks and hooking
                // back in at the top, their points turned down, as her portrait draws them.
                foreach (int s in Sides)
                    DsLock(bg, D, new float[] { 0.08f, s * 0.22f, 0.0f, s * 0.3f, 0.055f, s * 0.39f, 0.065f, s * 0.455f, 0.025f,
                                                s * 0.52f, -0.02f, s * 0.53f, -0.12f, s * 0.49f, -0.17f,
                                                s * 0.46f, -0.205f, s * 0.415f, -0.18f, s * 0.405f, -0.125f }, s > 0 ? 71 : 53);
                // The bob behind her face: a round dome falling close round it to her jaw, its ends cut ragged, the
                // strands running down from the crown.
                using (GraphicsPath b = KCurve(D, 0.35f, -0.2f, 0.03f, -0.24f, 0.02f, -0.255f, 0.045f, -0.268f, -0.04f, -0.272f, -0.15f, -0.258f, -0.28f,
                                               -0.205f, -0.4f, -0.11f, -0.465f, 0f, -0.48f, 0.11f, -0.465f, 0.205f, -0.4f, 0.258f, -0.28f, 0.272f, -0.15f,
                                               0.268f, -0.04f, 0.255f, 0.045f, 0.24f, 0.02f, 0.2f, 0.03f))
                {
                    DsUnder(bg, b, D);
                    FillSolid(bg, b, DsHair);
                    bg.SetClip(b, CombineMode.Intersect);
                    foreach (int s in Sides)
                    {
                        DsStrands(bg, D, new float[] { 0.16f, s * 0.08f, -0.47f, s * 0.2f, -0.44f, s * 0.27f, -0.25f, s * 0.26f, 0.04f }, s > 0 ? 31 : 67);
                        DsStrands(bg, D, new float[] { 0.1f, s * 0.02f, -0.48f, s * 0.12f, -0.45f, s * 0.22f, -0.32f, s * 0.24f, 0.03f }, s > 0 ? 13 : 89);
                    }
                    bg.ResetClip();
                    DsInk(bg, b, D, true);
                }
            });
            // Her small ears, peeping out beside her face.
            foreach (int s in Sides)
                using (GraphicsPath ep = KCurve(D, 0.5f, s * 0.21f, -0.15f, s * 0.255f, -0.155f, s * 0.27f, -0.11f, s * 0.252f, -0.07f, s * 0.212f, -0.065f))
                    DsFlat(g, ep, skin, D);
        }

        static void WillowClothes(Graphics g, float D, MascotPose p, Geo geo, Color skin)
        {
            // Her blouse is salmon pink in her portrait, buttoned down the front; the skirt black with pale pinstripes.
            Color top = Color.FromArgb(210, 124, 110), topShade = Color.FromArgb(168, 84, 76);
            Color skirt = Color.FromArgb(40, 36, 42), skirtShade = Color.FromArgb(22, 20, 24);
            using (GraphicsPath nk = KPoly(D, -0.022f, 0.08f, 0.022f, 0.08f, 0.025f, 0.13f, -0.025f, 0.13f)) DsFlat(g, nk, skin, D);
            foreach (int s in Sides)
            {
                PointF ft = Stride(p, s);
                DsStick(g, D, s * 0.03f, 0.3f, s * 0.034f + ft.X, 0.352f + ft.Y);
                DsShoe(g, D, s * 0.036f + ft.X, 0.36f + ft.Y, s);
            }
            // Thin pale arms from the shoulders to the hands (a prop she holds draws them itself), Bernie dangling from her
            // left hand and the lighter held up in her right one with its flame.
            if (!p.HideHands)
            {
                foreach (int s in Sides) StarveArm(g, D, KindWillow, s, DsHand(p, geo, s), skin);
                // Bernie never changes, so he is drawn once and moved along with her hand.
                PointF hb = DsHand(p, geo, -1);
                GraphicsState bs = g.Save();
                g.TranslateTransform(hb.X * D, hb.Y * D);
                DsCached(g, D, 4, new RectangleF(-0.08f, -0.03f, 0.19f, 0.29f), bg => Bernie(bg, D, P(0, 0)));
                g.Restore(bs);
                PointF h = DsHand(p, geo, 1);
                float fl = (float)Math.Sin(p.Now / 90.0) * 0.006f;
                using (GraphicsPath lt = Theme.Round(new RectangleF((h.X - 0.017f) * D, (h.Y - 0.075f) * D, 0.034f * D, 0.058f * D), 0.006f * D))
                    DsFlat(g, lt, Color.FromArgb(206, 202, 196), D);
                using (GraphicsPath fp = KCurve(D, 0.5f, h.X - 0.012f, h.Y - 0.078f, h.X + 0.016f, h.Y - 0.1f, h.X + fl, h.Y - 0.15f, h.X - 0.016f, h.Y - 0.1f))
                    DsFlat(g, fp, Color.FromArgb(255, 214, 96), D);
            }
            // The skirt: long and flared, black with pale pinstripes, its hem pleated.
            using (GraphicsPath sk = KPoly(D, -0.068f, 0.2f, 0.068f, 0.2f, 0.135f, 0.31f, 0.1f, 0.303f, 0.067f, 0.313f, 0.034f, 0.304f, 0f, 0.314f,
                                           -0.034f, 0.304f, -0.067f, 0.313f, -0.1f, 0.303f, -0.135f, 0.31f))
            {
                DsUnder(g, sk, D);
                DsFill(g, sk, skirt, skirtShade, D, 0.03f, 0);
                Region ov = g.Clip;
                g.SetClip(sk, CombineMode.Intersect);
                for (int i = -5; i <= 5; i++)
                    Line(g, Color.FromArgb(170, 210, 206, 214), Math.Max(0.7f, D * 0.0055f), i * 0.0125f * D, 0.2f * D, i * 0.026f * D, 0.315f * D);
                g.Clip = ov;
                ov.Dispose();
                DsInk(g, sk, D, false);
            }
            using (GraphicsPath tp = KPoly(D, -0.074f, 0.115f, 0.074f, 0.115f, 0.066f, 0.208f, -0.066f, 0.208f)) DsShape(g, tp, top, topShade, D, 1);
            foreach (int s in Sides)
                using (GraphicsPath sl = new GraphicsPath())
                {
                    sl.AddEllipse((s * 0.085f - 0.032f) * D, 0.112f * D, 0.064f * D, 0.052f * D);
                    DsFlat(g, sl, top, D);
                }
            // The small pointed collar, a shade darker, and the dark buttons down the front.
            foreach (int s in Sides)
                using (GraphicsPath cl = KPoly(D, s * 0.004f, 0.112f, s * 0.042f, 0.106f, s * 0.026f, 0.142f))
                    DsFlat(g, cl, topShade, D);
            for (int i = 0; i < 3; i++) Ellipse(g, Color.FromArgb(70, 34, 32), -0.006f * D, (0.15f + i * 0.018f) * D, 0.012f * D, 0.012f * D);
        }

        // Bernie, Willow's teddy bear, dangling by one ear from her hand (h, in units of D): a patched grey-brown bear
        // with a button eye and a stitched one, nearly down to her feet as in her portrait.
        static void Bernie(Graphics g, float D, PointF h)
        {
            GraphicsState bs = g.Save();
            g.TranslateTransform(h.X * D, h.Y * D);
            g.ScaleTransform(1.25f, 1.25f);
            g.TranslateTransform(-h.X * D, -h.Y * D);
            Color fur = Color.FromArgb(150, 136, 118), muzzle = Color.FromArgb(198, 186, 164);
            float x = h.X + 0.012f, y = h.Y;
            foreach (int s in Sides)
                using (GraphicsPath lg = new GraphicsPath())
                {
                    lg.AddEllipse((x + s * 0.024f - 0.018f) * D, (y + 0.15f) * D, 0.036f * D, 0.03f * D);
                    DsFlat(g, lg, fur, D);
                }
            using (GraphicsPath bd = new GraphicsPath())
            {
                bd.AddEllipse((x - 0.037f) * D, (y + 0.088f) * D, 0.074f * D, 0.078f * D);
                DsFlat(g, bd, fur, D);
            }
            Ellipse(g, muzzle, (x - 0.02f) * D, (y + 0.11f) * D, 0.03f * D, 0.03f * D);   // the patch on his tummy
            using (Pen st = new Pen(Color.FromArgb(200, DsInkC), Math.Max(0.7f, D * 0.004f)))
                for (int i = 0; i < 3; i++) g.DrawLine(st, (x - 0.022f + i * 0.013f) * D, (y + 0.108f) * D, (x - 0.018f + i * 0.013f) * D, (y + 0.143f) * D);
            foreach (int s in Sides)
                using (GraphicsPath er = new GraphicsPath())
                {
                    er.AddEllipse((x + s * 0.03f - 0.015f) * D, (y + 0.004f) * D, 0.03f * D, 0.03f * D);
                    DsFlat(g, er, fur, D);
                }
            using (GraphicsPath hd = new GraphicsPath())
            {
                hd.AddEllipse((x - 0.042f) * D, (y + 0.016f) * D, 0.084f * D, 0.074f * D);
                DsFlat(g, hd, fur, D);
            }
            Ellipse(g, muzzle, (x - 0.017f) * D, (y + 0.05f) * D, 0.034f * D, 0.026f * D);
            Ellipse(g, DsInkC, (x - 0.007f) * D, (y + 0.052f) * D, 0.014f * D, 0.009f * D);
            Ellipse(g, DsInkC, (x - 0.03f) * D, (y + 0.033f) * D, 0.014f * D, 0.014f * D);
            using (Pen xe = new Pen(DsInkC, Math.Max(0.8f, D * 0.005f)))
            {
                xe.StartCap = xe.EndCap = LineCap.Round;
                g.DrawLine(xe, (x + 0.013f) * D, (y + 0.032f) * D, (x + 0.027f) * D, (y + 0.045f) * D);
                g.DrawLine(xe, (x + 0.027f) * D, (y + 0.032f) * D, (x + 0.013f) * D, (y + 0.045f) * D);
            }
            g.Restore(bs);
        }

        // Willow's bangs: long strands from the top of her head down over the forehead to her brows, with the skin
        // showing between their points as in her portrait, drawn down from the crown to each point; and a few loose
        // ones sticking up from the crown.
        static void WillowBangs(Graphics g, float D)
        {
            float[] jag = { 0.232f, -0.16f, 0.205f, -0.27f, 0.178f, -0.215f, 0.148f, -0.3f, 0.118f, -0.222f, 0.088f, -0.305f, 0.055f, -0.228f,
                            0.025f, -0.31f, -0.006f, -0.232f, -0.036f, -0.305f, -0.068f, -0.226f, -0.098f, -0.3f, -0.128f, -0.22f, -0.158f, -0.295f,
                            -0.186f, -0.214f, -0.21f, -0.265f, -0.232f, -0.16f };
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddCurve(new PointF[] { P(-0.232f * D, -0.16f * D), P(-0.262f * D, -0.3f * D), P(-0.19f * D, -0.42f * D), P(0, -0.495f * D), P(0.19f * D, -0.42f * D), P(0.262f * D, -0.3f * D), P(0.232f * D, -0.16f * D) }, 0.5f);
                PointF[] pts = new PointF[jag.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = P(jag[i * 2] * D, jag[i * 2 + 1] * D);
                bp.AddLines(pts);
                bp.CloseFigure();
                DsUnder(g, bp, D);
                FillSolid(g, bp, DsHair);
                g.SetClip(bp, CombineMode.Intersect);
                for (int i = 0; i < pts.Length; i++)
                {
                    float x = jag[i * 2], y = jag[i * 2 + 1];
                    bool tip = i % 2 == 0 || i == pts.Length - 1;
                    DsStroke(g, tip ? Color.FromArgb(235, 10, 8, 10) : Color.FromArgb(150, 170, 164, 170), (tip ? 0.013f : 0.0065f) * D, D,
                             x * 0.3f, -0.49f, x * 0.7f, -0.45f, x * 0.95f, -0.36f, x, y + (tip ? 0.004f : -0.03f));
                }
                g.ResetClip();
                DsInk(g, bp, D, true);
            }
            DsStroke(g, DsInkC, 0.011f * D, D, -0.03f, -0.48f, -0.04f, -0.51f, -0.06f, -0.54f, -0.085f, -0.555f);
            DsStroke(g, DsInkC, 0.011f * D, D, 0.0f, -0.485f, 0.005f, -0.52f, 0.02f, -0.55f, 0.045f, -0.57f);
            DsStroke(g, DsInkC, 0.009f * D, D, 0.03f, -0.48f, 0.05f, -0.505f, 0.075f, -0.52f, 0.1f, -0.52f);
        }

        // ---- In front of the face: brows and nose, and Willow's bangs.
        static void KleiDetails(Graphics g, MascotLook l, Geo geo, float D, MascotPose p)
        {
            float fy = geo.FaceY * D, gap = geo.EyeGap * D, f = geo.FaceY;
            switch (l.Kind)
            {
                case KindWilson:
                    // Thick brows, lowered a little towards the middle, each with a short upward stroke at its inner end;
                    // his nose drawn as a check mark.
                    foreach (int s in Sides)
                    {
                        DsStroke(g, DsInkC, 0.028f * D, D, s * 0.158f, f - 0.088f, s * 0.12f, f - 0.088f, s * 0.075f, f - 0.076f, s * 0.035f, f - 0.058f);
                        DsStroke(g, DsInkC, 0.012f * D, D, s * 0.037f, f - 0.058f, s * 0.035f, f - 0.07f, s * 0.033f, f - 0.082f, s * 0.03f, f - 0.095f);
                    }
                    DsStroke(g, DsInkC, 0.012f * D, D, -0.034f, f + 0.086f, -0.028f, f + 0.098f, -0.022f, f + 0.11f, -0.016f, f + 0.122f);
                    DsStroke(g, DsInkC, 0.013f * D, D, -0.016f, f + 0.122f, -0.006f, f + 0.1f, 0.004f, f + 0.06f, 0.012f, f + 0.025f);
                    break;
                case KindWillow:
                {
                    DsCached(g, D, 3, new RectangleF(-0.3f, -0.6f, 0.6f, 0.47f), bg => WillowBangs(bg, D));
                    // Heavy brows straight over her eyes, a few strokes under them, and the thin hooked line of her nose.
                    foreach (int s in Sides)
                    {
                        DsStroke(g, DsInkC, 0.026f * D, D, s * 0.158f, f - 0.074f, s * 0.12f, f - 0.082f, s * 0.075f, f - 0.085f, s * 0.035f, f - 0.082f);
                        for (int i = 0; i < 3; i++)
                            DsStroke(g, Color.FromArgb(150, DsInkC), 0.007f * D, D, s * (0.07f + i * 0.02f), f + 0.085f + i * 0.004f, s * (0.08f + i * 0.02f), f + 0.083f + i * 0.004f,
                                     s * (0.09f + i * 0.02f), f + 0.081f + i * 0.004f, s * (0.1f + i * 0.02f), f + 0.079f + i * 0.004f);
                    }
                    DsStroke(g, DsInkC, 0.01f * D, D, 0.002f, f + 0.01f, -0.002f, f + 0.05f, -0.01f, f + 0.09f, -0.002f, f + 0.1f, 0.004f, f + 0.104f, 0.01f, f + 0.1f, 0.016f, f + 0.092f);
                    break;
                }
                case KindDupe:
                {
                    // Short black hair: drawn as a cap of small curls, ink first so the outline is only on the outside.
                    float[][] curls = DupeCurls(false);
                    foreach (float[] c in curls)
                        using (Pen ink = new Pen(KInk, D * 0.03f)) g.DrawEllipse(ink, (c[0] - c[2]) * D, (c[1] - c[2]) * D, c[2] * 2 * D, c[2] * 2 * D);
                    foreach (float[] c in curls) Ellipse(g, Color.FromArgb(40, 34, 36), (c[0] - c[2]) * D, (c[1] - c[2]) * D, c[2] * 2 * D, c[2] * 2 * D);
                    foreach (float[] c in curls) Ellipse(g, Color.FromArgb(90, 255, 255, 255), (c[0] - c[2] * 0.4f) * D, (c[1] - c[2] * 0.6f) * D, c[2] * 0.5f * D, c[2] * 0.35f * D);
                    DupeFace(g, fy, gap, D);
                    break;
                }
                case KindDupeCurly:
                {
                    // A big cloud of curls.
                    float[][] curls = DupeCurls(true);
                    foreach (float[] c in curls)
                        using (Pen ink = new Pen(KInk, D * 0.03f)) g.DrawEllipse(ink, (c[0] - c[2]) * D, (c[1] - c[2]) * D, c[2] * 2 * D, c[2] * 2 * D);
                    foreach (float[] c in curls)
                    {
                        RectangleF r = new RectangleF((c[0] - c[2]) * D, (c[1] - c[2]) * D, c[2] * 2 * D, c[2] * 2 * D);
                        using (GraphicsPath cp = new GraphicsPath())
                        {
                            cp.AddEllipse(r);
                            Fill(g, cp, r, Color.FromArgb(240, 178, 84), Color.FromArgb(196, 116, 42), 90f);
                        }
                    }
                    using (Pen sw = new Pen(Color.FromArgb(150, 150, 84, 30), Math.Max(1f, D * 0.008f)))
                        foreach (float[] c in curls) g.DrawArc(sw, (c[0] - c[2] * 0.55f) * D, (c[1] - c[2] * 0.55f) * D, c[2] * 1.1f * D, c[2] * 1.1f * D, 120, 200);
                    DupeFace(g, fy, gap, D);
                    break;
                }
            }
        }

        // Curls as circles (x, y, radius): a tight cap, or a full cloud with bangs.
        static float[][] DupeCurls(bool cloud)
        {
            System.Collections.Generic.List<float[]> c = new System.Collections.Generic.List<float[]>();
            int n = cloud ? 9 : 8;
            for (int i = 0; i < n; i++)
            {
                double a = (-170 + i * (160.0 / (n - 1))) * Math.PI / 180;
                float rr = cloud ? 0.29f : 0.25f;
                c.Add(new float[] { (float)Math.Cos(a) * rr, -0.1f + (float)Math.Sin(a) * rr * 0.9f, cloud ? 0.095f : 0.07f });
            }
            if (cloud)
            {
                c.Add(new float[] { -0.13f, -0.24f, 0.075f });
                c.Add(new float[] { 0.0f, -0.27f, 0.08f });
                c.Add(new float[] { 0.13f, -0.24f, 0.075f });
                c.Add(new float[] { 0.0f, -0.36f, 0.1f });
            }
            else
            {
                c.Add(new float[] { -0.09f, -0.26f, 0.065f });
                c.Add(new float[] { 0.05f, -0.27f, 0.065f });
                c.Add(new float[] { 0.0f, -0.32f, 0.08f });
            }
            return c.ToArray();
        }

        // A duplicant's brows and little nose.
        static void DupeFace(Graphics g, float fy, float gap, float D)
        {
            using (Pen b = new Pen(KInk, Math.Max(1f, D * 0.012f)))
            {
                b.StartCap = b.EndCap = LineCap.Round;
                foreach (int s in Sides) g.DrawArc(b, s * gap - 0.035f * D, fy - 0.085f * D, 0.07f * D, 0.04f * D, 200, 140);
                g.DrawArc(b, -0.015f * D, fy + 0.03f * D, 0.03f * D, 0.025f * D, 300, 200);
            }
        }

        static string[] KleiLines(int kind)
        {
            if (!I18n.IsSpanish) return KleiLinesEn(kind);
            switch (kind)
            {
                // Their own lines from the game, translated.
                case KindWilson: return new string[] { "\u00A1Tengo much\u00EDsima hambre!", "Se est\u00E1 haciendo tarde. Pronto oscurecer\u00E1.", "\u00A1Qu\u00E9 oscuro est\u00E1!",
                                                       "Esa presencia\u2026 \u00A1me resulta familiar! \u00BFHola?", "\u00A1\u00A1ABEEEEEEEEEEEEJAS!!", "\u00A1Qu\u00E9 fr\u00EDo!",
                                                       "Necesito\u2026 hielo\u2026 \u00A1o\u2026 sombra!", "\u00BFHas o\u00EDdo eso?", "\u00A1Ahora me siento muy listo!", "\u00A1Nunca dejes de aprender!",
                                                       "Mejor esto que la oscuridad.", "Yo solo venero a la ciencia.", "\u00A1Lo conquistar\u00E9 todo con el poder de mi MENTE!" };
                case KindWillow: return new string[] { "\u00A1Necesito comida!", "Se acerca la noche. \u00A1Necesito fuego!", "\u00A1\u00BFD\u00F3nde he puesto mi mechero?!",
                                                       "\u00A1No te tengo miedo!", "\u00A1Aaah! \u00A1Bichos que pican!", "\u00A1El fr\u00EDo! \u00A1Quema!", "\u00A1Mi preciosa luz se ha apagado!",
                                                       "\u00A1Mu\u00E9strate!", "\u00A1He hecho un fuego!", "\u00A1No puedo evitarlo!", "\u00A1Es mi mechero de la suerte!",
                                                       "\u00A1Mi amigo de la infancia: Bernie!", "\u00A1Ay\u00FAdame, Bernie!", "\u00A1ARDE! \u00A1ARDE M\u00C1S R\u00C1PIDO!", "Me gusta el fuego.",
                                                       "Podr\u00EDa quedarme horas mir\u00E1ndolo.", "Las cosas son mucho m\u00E1s bonitas cuando arden.", "No tengo tiempo que perder con flores." };
                // Abigail doesn't speak in the game, she only wails and whispers: her moans, and what the others say to her.
                case KindAbigail: return new string[] { "Uuuuuuuuuh\u2026", "Ooooooooh\u2026", "(susurros)\u2026", "Wendy: \u00AB\u00A1Abigail! \u00BFEst\u00E1s lista para jugar?\u00BB",
                                                        "Wendy: \u00ABNunca volveremos a separarnos.\u00BB", "Wendy: \u00AB\u00A1Juguemos, Abigail!\u00BB",
                                                        "Wendy: \u00ABAbigail siempre ha sido mi luz en la oscuridad\u2026\u00BB", "Wendy: \u00ABMe sent\u00EDa tan sola sin ti aqu\u00ED.\u00BB",
                                                        "Wendy: \u00AB\u00A1Ten cuidado, Abigail!\u00BB", "Wilson: \u00ABOooh, tiene un lacito mon\u00EDsimo.\u00BB", "Willow: \u00ABBueno, \u00BFy a ti qu\u00E9 te pas\u00F3?\u00BB" };
                default: return new string[] { "Necesito ox\u00EDgeno\u2026", "\u00BFQui\u00E9n ha dejado el slime aqu\u00ED?", "\u00A1Tarea completada!" };
            }
        }

        static string[] KleiLinesEn(int kind)
        {
            switch (kind)
            {
                case KindWilson: return new string[] { "I'm so very hungry!", "It's getting late. It will be dark soon.", "It's so dark!",
                                                       "That presence\u2026 it feels familiar! Hello?", "BEEEEEEEEEEEEES!!", "It's freezing!",
                                                       "I need\u2026 ice\u2026 or\u2026 shade!", "Did you hear that?", "I feel so smart now!", "Never stop learning!",
                                                       "Better this than the dark.", "I worship only science.", "I will conquer it all with the power of my MIND!" };
                case KindWillow: return new string[] { "I need food!", "Night is coming. I need fire!", "Where did I put my lighter?!",
                                                       "I'm not afraid of you!", "Aah! Stinging bugs!", "The cold! It burns!", "My precious light went out!",
                                                       "Show yourself!", "I made a fire!", "I can't help it!", "It's my lucky lighter!",
                                                       "My childhood friend: Bernie!", "Help me, Bernie!", "BURN! BURN FASTER!", "I like fire.",
                                                       "I could watch it for hours.", "Things are so much prettier when they burn.", "I have no time to waste on flowers." };
                case KindAbigail: return new string[] { "Uuuuuuuuuh\u2026", "Ooooooooh\u2026", "(whispers)\u2026", "Wendy: \u201CAbigail! Are you ready to play?\u201D",
                                                        "Wendy: \u201CWe'll never be apart again.\u201D", "Wendy: \u201CLet's play, Abigail!\u201D",
                                                        "Wendy: \u201CAbigail has always been my light in the dark\u2026\u201D", "Wendy: \u201CI was so lonely here without you.\u201D",
                                                        "Wendy: \u201CBe careful, Abigail!\u201D", "Wilson: \u201COooh, she has the cutest little bow.\u201D", "Willow: \u201CWell, what happened to you?\u201D" };
                default: return new string[] { "Need oxygen\u2026", "Who left this slime here?", "Errand complete!" };
            }
        }
    }

    public partial class Mascot
    {
        // Wilson's tired eyes (small dark pupils in reddish rings), Willow's blank staring ones, a duplicant's dots.
        bool KleiEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            int k = Look.Kind;
            float sk = surprised ? 1.12f : 1f;
            if (k == MascotParts.KindWilson)
            {
                // Tall dark eyes close together, in olive-brown shadows that spread out towards the sides of his face.
                float o = x < 0 ? -1 : 1;
                MascotParts.Ellipse(g, Color.FromArgb(95, 120, 96, 48), x + o * 0.016f * D - 0.045f * D, y - 0.034f * D, 0.09f * D, 0.08f * D);
                MascotParts.Ellipse(g, Color.FromArgb(60, 100, 76, 36), x + o * 0.024f * D - 0.032f * D, y - 0.008f * D, 0.064f * D, 0.05f * D);
                if (blink < 0.35f) { ClosedEye(g, x, y, 0.05f * D, D); return true; }
                float ew = 0.036f * D * sk, eh = 0.062f * D * blink * sk;
                MascotParts.Ellipse(g, Color.FromArgb(44, 32, 32), x - ew / 2, y - eh / 2, ew, eh);
                MascotParts.Ellipse(g, Color.FromArgb(14, 10, 10), x - ew * 0.36f, y - eh * 0.2f, ew * 0.72f, eh * 0.62f);
                return true;
            }
            if (k == MascotParts.KindWillow)
            {
                float w = 0.122f * D * sk, h = 0.122f * D * sk * blink;
                if (h < D * 0.02f) { ClosedEye(g, x, y, w * 0.8f, D); return true; }
                RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
                MascotParts.Ellipse(g, Color.FromArgb(250, 248, 242), r.X, r.Y, r.Width, r.Height);
                // Ringed in a few loose strokes of ink.
                using (Pen p1 = new Pen(MascotParts.DsInkC, Math.Max(1.2f, D * 0.012f)))
                using (Pen p2 = new Pen(Color.FromArgb(150, MascotParts.DsInkC), Math.Max(0.8f, D * 0.006f)))
                {
                    g.DrawEllipse(p1, r);
                    GraphicsState st = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform(x < 0 ? 14 : -11);
                    g.DrawEllipse(p2, -w * 0.535f, -h * 0.5f, w * 1.05f, h * 1.02f);
                    g.Restore(st);
                }
                return true;
            }
            if (k == MascotParts.KindAbigail)
            {
                // Big hollow rings of ink, grey shadow round their lower outer side, and three lashes on the upper outer one.
                float o = x < 0 ? -1 : 1, r = 0.057f * D * sk;
                MascotParts.Ellipse(g, Color.FromArgb(85, 96, 96, 104), x + o * 0.014f * D - r * 1.2f, y - r * 1.02f + 0.012f * D, r * 2.4f, r * 2.3f);
                using (Pen lash = new Pen(MascotParts.DsInkC, Math.Max(1f, 0.011f * D)))
                {
                    lash.StartCap = lash.EndCap = LineCap.Round;
                    for (int i = 0; i < 3; i++)
                    {
                        double a = (o < 0 ? 200 + i * 24 : 340 - i * 24) * Math.PI / 180;
                        float cx = (float)Math.Cos(a), cy = (float)Math.Sin(a);
                        g.DrawLine(lash, x + cx * r * 1.05f, y + cy * r * 1.05f * Math.Max(0.35f, blink), x + cx * r * 1.55f, y + cy * r * 1.55f * Math.Max(0.35f, blink) - 0.004f * D);
                    }
                }
                if (blink < 0.35f) { ClosedEye(g, x, y, r * 2, D); return true; }
                RectangleF er = new RectangleF(x - r, y - r * blink, r * 2, r * 2 * blink);
                MascotParts.Ellipse(g, Color.FromArgb(253, 251, 245), er.X, er.Y, er.Width, er.Height);
                using (Pen p = new Pen(MascotParts.DsInkC, Math.Max(1.4f, 0.017f * D))) g.DrawEllipse(p, er);
                return true;
            }
            if (k == MascotParts.KindDupe || k == MascotParts.KindDupeCurly)
            {
                float w = 0.045f * D * sk, h = 0.062f * D * sk * blink;
                if (h < D * 0.015f) { ClosedEye(g, x, y, w * 1.4f, D); return true; }
                MascotParts.Ellipse(g, Color.FromArgb(26, 22, 22), x - w / 2, y - h / 2, w, h);
                MascotParts.Ellipse(g, Color.FromArgb(230, 255, 255, 255), x - w * 0.3f, y - h * 0.34f, w * 0.38f, h * 0.28f);
                return true;
            }
            return false;
        }

        bool KleiMouth(Graphics g, float D, float mx, float fy, bool talking)
        {
            int k = Look.Kind;
            if (!MascotParts.IsKlei(k)) return false;
            if (k == MascotParts.KindAbigail) return true;   // she has no mouth
            bool open = talking || mood == Mood.Happy || mood == Mood.Love;
            bool dupe = k == MascotParts.KindDupe || k == MascotParts.KindDupeCurly;
            float my = fy + (dupe ? 0.11f : 0.15f) * D, w = (dupe ? 0.08f : 0.07f) * D;
            if (open)
            {
                OpenMouth(g, mx, my, w * (dupe ? 1f : 0.8f), 0.042f * D, D);
                return true;
            }
            if (k == MascotParts.KindWilson)
            {
                // A small dark pout: arched on top, almost flat underneath.
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddBezier(mx - 0.044f * D, my + 0.011f * D, mx - 0.024f * D, my - 0.02f * D, mx + 0.024f * D, my - 0.02f * D, mx + 0.044f * D, my + 0.011f * D);
                    m.AddBezier(mx + 0.044f * D, my + 0.011f * D, mx + 0.02f * D, my + 0.005f * D, mx - 0.02f * D, my + 0.005f * D, mx - 0.044f * D, my + 0.011f * D);
                    m.CloseFigure();
                    MascotParts.FillSolid(g, m, Color.FromArgb(52, 30, 30));
                    MascotParts.Stroke(g, m, MascotParts.DsInkC, Math.Max(0.8f, D * 0.006f));
                }
                return true;
            }
            if (k == MascotParts.KindWillow)
            {
                // Her small pursed lips.
                RectangleF r = new RectangleF(mx - 0.019f * D, my - 0.012f * D, 0.038f * D, 0.024f * D);
                MascotParts.Ellipse(g, Color.FromArgb(150, 58, 62), r.X, r.Y, r.Width, r.Height);
                using (Pen p = new Pen(MascotParts.DsInkC, Math.Max(1f, D * 0.007f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawEllipse(p, r);
                    g.DrawLine(p, r.X + r.Width * 0.15f, my, r.Right - r.Width * 0.15f, my);
                }
                return true;
            }
            using (Pen p = new Pen(Color.FromArgb(34, 26, 24), Math.Max(1.2f, D * 0.012f)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawArc(p, mx - w / 2, my - 0.03f * D, w, 0.05f * D, 20, 140);         // a little smile
            }
            return true;
        }
    }
}
