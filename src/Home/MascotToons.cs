// Stackshot - Characters redrawn as toys in their own series' look: a bold outline, a soft gradient, a crisp shade on
// the side away from the light and a little shine, like vinyl figures. Adventure Time's Finn at the model sheet's
// proportions (the white bear hat over a column of blue shirt and dark blue shorts, long thin arms and legs, sock cuffs,
// black shoes, the green backpack at his shoulders), Jake as his figure (an egg of a dog, deep-set eyes, the arched
// muzzle with its jowls, long thin arms and legs) and BMO (the teal console, a face on its screen, thin legs); Naruto's
// Pakkun (Kakashi's pug, his head wrapped in the blue cloth of his forehead protector, a big dark muzzle and heavy lids)
// and Naruto, Sasuke and Kakashi as anime chibis, their hair in spiky locks and the leaf village's forehead protector on
// their brows. Also the walk: the feet of every character drawn with legs step in turn and its hands swing while the
// desktop pet walks. Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindFinn = 88, KindJake = 89, KindBmoAt = 90, KindPakkun = 91, KindNarutoSd = 92, KindSasukeSd = 93, KindKakashiSd = 94;

        public static bool IsToon(int kind) { return (kind >= KindFinn && kind <= KindWaniSd) || IsPersona(kind) || IsSoftFig(kind); }
        static bool IsAdventure(int kind) { return kind >= KindFinn && kind <= KindBmoAt; }

        // Kakashi's left eye is under his forehead protector, and no mouth shows through his mask.
        public static bool HidesRightEye(int kind) { return kind == KindKakashiSd; }
        public static bool NoMouth(int kind) { return kind == KindAbigail || kind == KindKakashiSd || kind == KindSpiderSd || kind == KindIronSd || kind == KindDoomSd || kind == KindKnightSd || kind == KindPantherSd || kind == KindColossalSd || kind == KindAttackSd || kind == KindArmoredSd || kind == KindSmileTitanSd || kind == KindBoboTitanSd; }
        // Their muzzles carry the mouth while it is shut.
        public static bool MuzzleMouth(int kind) { return kind == KindJake || kind == KindPakkun; }
        // The chibis' arms are short: stretching, their hands go up beside their heads, not far over them.
        public static bool ShortArms(int kind) { return IsToon(kind) && kind != KindFinn && kind != KindJake && kind != KindBmoAt && kind != KindPakkun; }

        // How much bigger they show in the small views (the home's mascot, the sidebar's), room allowing: Finn tall and
        // slim, Jake and BMO smaller than the box.
        public static double PreviewZoom(int kind)
        {
            switch (kind)
            {
                case KindFinn: return 1.2;
                case KindJake: return 1.4;
                case KindBmoAt: return 1.45;
                default: return 1;
            }
        }

        // For a still preview (the dock's tiles), the ones drawn to their series' proportions show as a bust, head to waist,
        // like a profile picture: its top and bottom (units of D from the point the mascot is drawn around).
        public static bool PreviewFrame(int kind, out float top, out float bottom)
        {
            if (IsPersona(kind)) return PersonaFrame(kind, out top, out bottom);
            switch (kind)
            {
                case KindFinn: top = -0.73f; bottom = -0.06f; return true;
                case KindJake: top = -0.53f; bottom = 0.1f; return true;
                case KindBmoAt: top = -0.39f; bottom = 0.22f; return true;
                case KindPakkun: top = -0.38f; bottom = 0.22f; return true;
                case KindNarutoSd: top = -0.7f; bottom = 0.16f; return true;
                case KindSasukeSd: top = -0.67f; bottom = 0.16f; return true;
                case KindKakashiSd: top = -0.74f; bottom = 0.16f; return true;
                case KindCreeper: top = -0.5f; bottom = 0.3f; return true;
                case KindCrewmate: top = -0.5f; bottom = 0.28f; return true;
                case KindWilson: top = -0.68f; bottom = 0.15f; return true;
                case KindWillow: top = -0.55f; bottom = 0.15f; return true;
                case KindAbigail: top = -0.62f; bottom = 0.15f; return true;
                case KindGonSd: top = -0.89f; bottom = 0.16f; return true;
                case KindKilluaSd: top = -0.66f; bottom = 0.16f; return true;
                case KindHisokaSd: top = -0.83f; bottom = 0.16f; return true;
                case KindTanjiroSd: top = -0.68f; bottom = 0.16f; return true;
                case KindNezukoSd: top = -0.47f; bottom = 0.16f; return true;
                case KindLuffySd: top = -0.62f; bottom = 0.16f; return true;
                case KindZoroSd: top = -0.52f; bottom = 0.16f; return true;
                case KindKaminaSd: top = -0.76f; bottom = 0.16f; return true;
                case KindShinSd: top = -0.64f; bottom = 0.12f; return true;
                case KindKantamSd: top = -0.9f; bottom = 0.12f; return true;
                case KindUltraSd: top = -0.8f; bottom = 0.12f; return true;
                case KindPiggySd: top = -0.558f; bottom = 0.156f; return true;
                case KindWaniSd: top = -0.75f; bottom = 0.12f; return true;
                default: top = bottom = 0; return false;
            }
        }

        // Where a still preview px square draws the mascot's box: a bust filling the tile for the ones above, the whole
        // character as before for the rest.
        public static RectangleF PreviewBox(int kind, float px)
        {
            float top, bottom;
            if (!PreviewFrame(kind, out top, out bottom)) return new RectangleF(px * 0.19f, px * 0.22f, px * 0.62f, px * 0.62f);
            float d = px * 0.94f / (bottom - top);
            return new RectangleF((px - d) / 2, px * 0.5f - d * (0.46f + (top + bottom) / 2), d, d);
        }

        // How high they reach (units of D, from the point the mascot is drawn around; the top of its box, at -0.46, for
        // most): higher, Finn's hat ears, the ninjas' hair and Jake's head; lower, BMO. What goes over the head (a speech
        // bubble) keeps above it.
        public static float HeadTop(int kind)
        {
            if (IsPersona(kind)) return PersonaHeadTop(kind);
            if (IsSoftFig(kind)) return PkTop(kind);
            switch (kind)
            {
                case KindFinn: return -0.715f;
                case KindNarutoSd: return -0.68f;
                case KindSasukeSd: return -0.65f;
                case KindKakashiSd: return -0.72f;
                case KindJake: return -0.515f;
                case KindBmoAt: return -0.375f;
                case KindChopper: return -0.655f;
                case KindKeroSd: return -0.465f;
                case KindSnoopySd: return -0.5f;
                case KindGonSd: return -0.87f;
                case KindKilluaSd: return -0.64f;
                case KindHisokaSd: return -0.81f;
                case KindTanjiroSd: return -0.66f;
                case KindNezukoSd: return -0.45f;
                case KindLuffySd: return -0.6f;
                case KindZoroSd: return -0.5f;
                case KindKaminaSd: return -0.74f;
                case KindShinSd: return -0.625f;
                case KindNevadoSd: return -0.21f;
                case KindKantamSd: return -0.89f;
                case KindUltraSd: return -0.785f;
                case KindPiggySd: return -0.545f;
                case KindWaniSd: return -0.735f;
                default: return -0.46f;
            }
        }

        // ---- Walking: while the desktop pet walks, each foot in turn lifts and swings forward, then sets down and slides
        // back as the body moves on over it; the hands swing a little in time.

        // Where a foot is, from where it stands (units of D).
        public static PointF Stride(MascotPose p, int side)
        {
            float x = 0, y = 0;
            if (p.Walk >= 0.01)
            {
                double ph = p.Step + (side > 0 ? Math.PI : 0);
                float dir = p.Facing > 0.1 ? 1 : p.Facing < -0.1 ? -1 : 0;
                x = (float)(-Math.Cos(ph) * 0.022 * p.Walk) * dir;
                y = (float)(-Math.Max(0, Math.Sin(ph)) * 0.045 * p.Walk);
            }
            if (p.Air >= 0.01)
            {
                // In the air the legs move by themselves, trailing behind the way it is going: held up by the mouse they
                // hang a little lower and swing loosely; falling or thrown they pedal in turn, quick, drawn up a little.
                double ph = p.Now / (p.Held ? 300.0 : 80.0) + (side > 0 ? Math.PI : 0);
                float a = (float)p.Air, k = (float)Math.Sin(ph), trail = -0.04f * (float)Math.Max(-1, Math.Min(1, p.Facing));
                if (p.Held) { x += (k * 0.022f + trail) * a; y += (0.014f - Math.Max(0, k) * 0.012f) * a; }
                else { x += ((float)Math.Cos(ph) * 0.03f + trail) * a; y += (-0.02f - Math.Max(0, k) * 0.06f) * a; }
            }
            return new PointF(x, y);
        }

        public static float HandSwing(MascotPose p, int side)
        {
            float y = p.Walk < 0.01 ? 0 : (float)(Math.Sin(p.Step + (side > 0 ? 0 : Math.PI)) * 0.025 * p.Walk);
            // Falling, the hands go up and flap; held up, they hang a little.
            if (p.Air >= 0.01) y += (float)((p.Held ? 0.02 : -0.1 - Math.Sin(p.Now / 70.0 + side) * 0.04) * p.Air);
            return y;
        }

        static PointF Foot(MascotPose p, int side, float x, float y)
        {
            PointF s = Stride(p, side);
            return P(x + s.X, y + s.Y);
        }

        static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

        // ---- The look: the outline laid under each shape, so only its outer half shows and parts drawn over each other
        // keep their edge; a soft gradient, a crisp shade where the shape moved up and to the left no longer covers it,
        // and a soft shine at its top left.

        static readonly Color AtInk = Color.FromArgb(30, 26, 34), AnInk = Color.FromArgb(46, 30, 28);
        static readonly Color NjSkin = Color.FromArgb(255, 226, 202), NjSkinShade = Color.FromArgb(246, 196, 166);
        static Color TnInk(int kind) { return IsAdventure(kind) || kind == KindSnoopySd || (kind >= KindShinSd && kind <= KindWaniSd) || IsSoftFig(kind) ? AtInk : AnInk; }
        static float TnW(float D) { return Math.Max(1.25f, D * 0.0135f); }
        // Finn is drawn at the show's proportions, smaller than the rest, so his outline is a little finer; Shin-chan's
        // world, drawn like the 3D models of his games, finer still.
        static float TnW(float D, int kind) { return kind == KindFinn ? Math.Max(1.1f, D * 0.0108f) : TnFigure(kind) ? Math.Max(1f, D * 0.0075f) : TnW(D); }
        static bool TnFigure(int kind) { return (kind >= KindShinSd && kind <= KindWaniSd) || IsSoftFig(kind); }

        // The 3D model look: each shape lit softly from the top left, rounding off darker towards its edges with no hard
        // bands, and a soft shine where the light falls.
        static void SoftPaint(Graphics g, GraphicsPath p, Color c, Color shade, float D, int glow = 60)
        {
            RectangleF r = p.GetBounds();
            if (r.Width < 0.5f || r.Height < 0.5f) return;
            Color edge = shade.IsEmpty ? Darker(c, 0.16) : Mac.Mix(c, shade, 0.8);
            using (PathGradientBrush pb = new PathGradientBrush(p))
            {
                pb.CenterPoint = new PointF(r.X + r.Width * 0.4f, r.Y + r.Height * 0.34f);
                pb.CenterColor = Lighter(c, 0.08);
                pb.SurroundColors = new Color[] { edge };
                pb.FocusScales = new PointF(0.45f, 0.45f);
                g.FillPath(pb, p);
            }
            if (r.Width < 0.07f * D || r.Height < 0.06f * D) return;
            Region old = g.Clip;
            g.SetClip(p, CombineMode.Intersect);
            Glow(g, r.X + r.Width * 0.34f, r.Y + r.Height * 0.25f, r.Width * 0.42f, r.Height * 0.26f, Color.White, glow);
            g.Clip = old;
            old.Dispose();
        }

        // A limb in the 3D model look: a tube, darker at its sides, lit along the side towards the light, with a shine.
        static void SoftLimb(Graphics g, int kind, float D, Color c, float w, PointF a, PointF c1, PointF c2, PointF b)
        {
            float W = w * D, o = W * 0.12f;
            using (Pen ink = new Pen(TnInk(kind), W + TnW(D, kind) * 2))
            using (Pen body = new Pen(Mac.Mix(c, Darker(c, 0.3), 0.55), W))
            using (Pen lit = new Pen(c, W * 0.68f))
            using (Pen shine = new Pen(Color.FromArgb(150, Lighter(c, 0.3)), W * 0.22f))
            {
                ink.StartCap = ink.EndCap = body.StartCap = body.EndCap = lit.StartCap = lit.EndCap = shine.StartCap = shine.EndCap = LineCap.Round;
                g.DrawBezier(ink, a, c1, c2, b);
                g.DrawBezier(body, a, c1, c2, b);
                g.DrawBezier(lit, Nudge(a, -o), Nudge(c1, -o), Nudge(c2, -o), Nudge(b, -o));
                g.DrawBezier(shine, Nudge(a, -o * 2.4f), Nudge(c1, -o * 2.4f), Nudge(c2, -o * 2.4f), Nudge(b, -o * 2.4f));
            }
        }

        static PointF Nudge(PointF p, float d) { return new PointF(p.X + d, p.Y + d); }

        // The id each kept piece of a character is cached under (DsCached), one apart from every other character's: part 1
        // behind it, 2 its head, 3 its clothes, 4 in front of its face, 5 and 6 its eyes.
        internal static int TnId(int kind, int part) { return 200 + kind * 8 + part; }

        static void VPaint(Graphics g, GraphicsPath p, Color c, Color shade, float D, int gloss)
        {
            RectangleF r = p.GetBounds();
            if (r.Width < 0.5f || r.Height < 0.5f) return;
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Lighter(c, 0.12), c, 90f))
                g.FillPath(b, p);
            if (shade.IsEmpty && gloss <= 0) return;
            Region old = g.Clip;
            if (!shade.IsEmpty)
            {
                g.SetClip(p, CombineMode.Intersect);
                using (GraphicsPath lit = (GraphicsPath)p.Clone())
                using (Matrix m = new Matrix())
                {
                    m.Translate(-Math.Min(0.028f * D, r.Width * 0.18f), -Math.Min(0.022f * D, r.Height * 0.16f));
                    lit.Transform(m);
                    g.SetClip(lit, CombineMode.Exclude);
                }
                using (SolidBrush sb = new SolidBrush(shade)) g.FillRectangle(sb, r);
                g.Clip = old;
            }
            if (gloss > 0)
            {
                g.SetClip(p, CombineMode.Intersect);
                Glow(g, r.X + r.Width * 0.34f, r.Y + r.Height * 0.24f, r.Width * 0.52f, r.Height * 0.34f, Color.White, gloss);
                g.Clip = old;
            }
            old.Dispose();
        }

        static void VFill(Graphics g, int kind, GraphicsPath p, Color c, Color shade, float D, int gloss)
        {
            Stroke(g, p, TnInk(kind), TnW(D, kind) * 2);
            if (TnFigure(kind)) SoftPaint(g, p, c, shade, D);
            else VPaint(g, p, c, shade, D, gloss);
        }

        static void VOval(Graphics g, int kind, float D, Color c, Color shade, int gloss, float cx, float cy, float w, float h)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse((cx - w / 2) * D, (cy - h / 2) * D, w * D, h * D);
                VFill(g, kind, p, c, shade, D, gloss);
            }
        }

        static void VRound(Graphics g, int kind, float D, Color c, Color shade, int gloss, float x, float y, float w, float h, float r)
        {
            using (GraphicsPath p = Theme.Round(new RectangleF(x * D, y * D, w * D, h * D), r * D)) VFill(g, kind, p, c, shade, D, gloss);
        }

        static void VPoly(Graphics g, int kind, float D, Color c, Color shade, int gloss, params float[] xy)
        {
            using (GraphicsPath p = KPoly(D, xy)) VFill(g, kind, p, c, shade, D, gloss);
        }

        static void VBlob(Graphics g, int kind, float D, Color c, Color shade, int gloss, float tension, params float[] xy)
        {
            using (GraphicsPath p = KCurve(D, tension, xy)) VFill(g, kind, p, c, shade, D, gloss);
        }

        // A soft limb from a to b (units of D), bowed a little, in colour inside its outline.
        static void VLimb(Graphics g, int kind, float D, Color c, float w, PointF a, PointF b, float bow)
        {
            if (!Finite(a) || !Finite(b)) return;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            // A hand right at the shoulder: a curve with all its points in one place, which GDI+ refuses to draw.
            if (Math.Abs(dx) + Math.Abs(dy) < 0.002f) return;
            PointF c1 = P((a.X + dx * 0.33f + dy * bow) * D, (a.Y + dy * 0.33f - dx * bow) * D), c2 = P((a.X + dx * 0.66f + dy * bow) * D, (a.Y + dy * 0.66f - dx * bow) * D);
            if (TnFigure(kind)) { SoftLimb(g, kind, D, c, w, P(a.X * D, a.Y * D), c1, c2, P(b.X * D, b.Y * D)); return; }
            using (Pen ink = new Pen(TnInk(kind), w * D + TnW(D, kind) * 2))
            using (Pen fill = new Pen(c, w * D))
            {
                ink.StartCap = ink.EndCap = fill.StartCap = fill.EndCap = LineCap.Round;
                try
                {
                    g.DrawBezier(ink, a.X * D, a.Y * D, c1.X, c1.Y, c2.X, c2.Y, b.X * D, b.Y * D);
                    g.DrawBezier(fill, a.X * D, a.Y * D, c1.X, c1.Y, c2.X, c2.Y, b.X * D, b.Y * D);
                }
                catch (OutOfMemoryException) { }   // GDI+'s answer to a curve it can't stroke: this frame goes without that limb
            }
        }

        static bool Finite(PointF p) { return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y); }

        // A line through points (units of D).
        static void TnLine(Graphics g, Color c, float w, float D, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2] * D, xy[i * 2 + 1] * D);
            using (Pen p = new Pen(c, Math.Max(0.8f, w * D)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                if (pts.Length == 2) g.DrawLine(p, pts[0], pts[1]);
                else g.DrawCurve(p, pts, 0.5f);
            }
        }

        // A spiky outline: points alternating between notches and spike tips, joined by edges bowed out a little (for
        // points running clockwise) that meet in sharp corners; closed.
        static GraphicsPath TnSpikes(float D, float bow, float[] xy)
        {
            int n = xy.Length / 2;
            GraphicsPath p = new GraphicsPath();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                PointF a = P(xy[i * 2] * D, xy[i * 2 + 1] * D), b = P(xy[j * 2] * D, xy[j * 2 + 1] * D);
                float dx = b.X - a.X, dy = b.Y - a.Y;
                p.AddCurve(new PointF[] { a, P((a.X + b.X) / 2 + dy * bow, (a.Y + b.Y) / 2 - dx * bow), b }, 0.5f);
            }
            p.CloseFigure();
            return p;
        }

        // The same outline mirrored left to right, its points reversed so it still runs clockwise.
        static float[] Mirror(float[] xy)
        {
            int n = xy.Length / 2;
            float[] m = new float[xy.Length];
            for (int i = 0; i < n; i++)
            {
                m[i * 2] = -xy[(n - 1 - i) * 2];
                m[i * 2 + 1] = xy[(n - 1 - i) * 2 + 1];
            }
            return m;
        }

        // ---- Layout of each one.

        static Geo ToonGeo(int kind)
        {
            if (IsSoftFig(kind)) return PkGeo(kind);
            switch (kind)
            {
                case KindFinn: return G(0.3f, -0.658f, -0.558f, 0.0574f, -0.425f, 0.3f, 0.42f, 0.2375f, 0.02f, 0.3f);
                case KindJake: return G(0.52f, -0.5f, -0.3064f, 0.127f, -0.12f, 0.4f, 0.17f, 0.335f, 0.07f, 0.4f);
                case KindBmoAt: return G(0.4f, -0.36f, -0.215f, 0.062f, 0.1f, 0.3f, 0.2f, 0.295f, 0.04f, 0.38f);
                case KindPakkun: return G(0.57f, -0.34f, -0.085f, 0.11f, 0.12f, 0.3f, 0.155f, 0.19f, 0.25f, 0.5f);
                // Chopper and Luffy: a hat picked for them sits on the top of their own.
                case KindChopper: return G(0.41f, -0.648f, -0.12f, 0.105f, -0.01f, 0.16f, 0.29f, 0.2f, 0.2f, 0.42f);
                case KindLuffySd: return G(0.52f, -0.585f, -0.04f, 0.112f, 0.1f, 0.3f, 0.1f, 0.17f, 0.23f, 0.38f);
                case KindKeroSd: return G(0.45f, -0.412f, -0.195f, 0.175f, 0.01f, 0.22f, 0.33f, 0.18f, 0.17f, 0.44f);
                case KindSnoopySd: return G(0.62f, -0.5f, -0.302f, 0.05f, -0.04f, 0.12f, 0.34f, 0.115f, 0.18f, 0.5f);
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: return G(0.56f, -0.6f, -0.37f, 0.07f, -0.17f, 0.3f, 0.21f, 0.238f, 0.068f, 0.52f);
                case KindPiggySd: return G(0.68f, -0.6f, -0.425f, 0.172f, -0.17f, 0.3f, 0.21f, 0.235f, 0.03f, 0.56f);
                case KindNevadoSd: return G(0.58f, -0.2f, 0.039f, 0.16f, 0.21f, 0.28f, 0.33f, 0.09f, 0.3f, 0.46f);
                default: return G(0.52f, -0.36f, -0.04f, 0.112f, 0.1f, 0.3f, 0.1f, 0.17f, 0.23f, 0.52f);
            }
        }

        internal static float ToonMouthDy(int kind)
        {
            if (IsSoftFig(kind)) return PkMouthDy(kind);
            switch (kind)
            {
                case KindFinn: return 0.022f;
                case KindJake: return 0.135f;
                case KindBmoAt: return 0.05f;
                case KindPakkun: return 0.175f;
                case KindChopper: return 0.066f;
                case KindKeroSd: return 0.04f;
                case KindSnoopySd: return 0.15f;
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: return 0.172f;
                case KindPiggySd: return 0.225f;
                case KindNevadoSd: return 0.126f;
                default: return 0.1f;
            }
        }

        // The main shape, drawn over the body: Finn's hat, Jake's whole bean, BMO's box, Pakkun's and the ninjas' heads.
        static GraphicsPath ToonPath(int kind, float D)
        {
            if (IsSoftFig(kind)) return PkPath(kind, D);
            switch (kind)
            {
                case KindFinn:
                {
                    // His bear hat as the model sheet draws it: a little wider than it is tall, as wide as the rest of him,
                    // with its straight sides running up into the two small rounded ears at its top corners.
                    GraphicsPath p = new GraphicsPath();
                    float l = -0.15f * D, r = 0.15f * D, t = -0.658f * D, b = -0.425f * D, ew = 0.054f * D, et = -0.7f * D, rb = 0.012f * D;
                    p.AddLine(l, b - rb, l, et + ew / 2);
                    p.AddArc(l, et, ew, ew, 180, 180);
                    p.AddLine(l + ew, et + ew / 2, l + ew, t);
                    p.AddBezier(P(l + ew, t), P(l + ew + 0.05f * D, t - 0.006f * D), P(r - ew - 0.05f * D, t - 0.006f * D), P(r - ew, t));
                    p.AddLine(r - ew, t, r - ew, et + ew / 2);
                    p.AddArc(r - ew, et, ew, ew, 180, 180);
                    p.AddLine(r, et + ew / 2, r, b - rb);
                    p.AddArc(r - rb * 2, b - rb * 2, rb * 2, rb * 2, 0, 90);
                    p.AddArc(l, b - rb * 2, rb * 2, rb * 2, 90, 90);
                    p.CloseFigure();
                    return p;
                }
                case KindJake:
                    // An egg, as the figure is: round on top, widest a little above the middle, rounding in to the bottom.
                    return KCurve(D, 0.5f, 0, -0.5f, 0.098f, -0.4799f, 0.151f, -0.4547f, 0.196f, -0.417f, 0.2288f, -0.3668f, 0.2514f, -0.2913f, 0.2604f, -0.21f,
                                  0.2615f, -0.13f, 0.2564f, -0.04f, 0.2464f, 0.0103f, 0.2238f, 0.0606f, 0.2011f, 0.0983f, 0.1785f, 0.1235f, 0.1332f, 0.1486f, 0.07f, 0.166f,
                                  0, 0.172f, -0.07f, 0.166f, -0.1332f, 0.1486f, -0.1785f, 0.1235f, -0.2011f, 0.0983f, -0.2238f, 0.0606f, -0.2464f, 0.0103f, -0.2564f, -0.04f,
                                  -0.2615f, -0.13f, -0.2604f, -0.21f, -0.2514f, -0.2913f, -0.2288f, -0.3668f, -0.196f, -0.417f, -0.151f, -0.4547f, -0.098f, -0.4799f);
                case KindBmoAt:
                    return Theme.Round(new RectangleF(-0.2f * D, -0.36f * D, 0.4f * D, 0.56f * D), 0.065f * D);
                case KindPakkun:
                    // A round pug's head, its cheeks heavy at the bottom.
                    return KCurve(D, 0.5f, 0, -0.34f, 0.17f, -0.32f, 0.26f, -0.22f, 0.285f, -0.08f, 0.275f, 0.04f, 0.21f, 0.125f, 0.1f, 0.155f, 0, 0.16f,
                                  -0.1f, 0.155f, -0.21f, 0.125f, -0.275f, 0.04f, -0.285f, -0.08f, -0.26f, -0.22f, -0.17f, -0.32f);
                case KindChopper: return ChopperHead(D);
                case KindKeroSd: return KeroHead(D);
                case KindSnoopySd: return SnoopyHead(D);
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: return ShinHead(D);
                case KindPiggySd: return BuriHead(D);
                case KindNevadoSd: return NevadoHead(D);
                default:
                {
                    // A chibi's face: full round cheeks and a small soft chin.
                    GraphicsPath p = new GraphicsPath();
                    p.AddBezier(P(0, 0.1f * D), P(0.13f * D, 0.1f * D), P(0.235f * D, 0.045f * D), P(0.255f * D, -0.05f * D));
                    p.AddBezier(P(0.255f * D, -0.05f * D), P(0.275f * D, -0.16f * D), P(0.25f * D, -0.36f * D), P(0, -0.36f * D));
                    p.AddBezier(P(0, -0.36f * D), P(-0.25f * D, -0.36f * D), P(-0.275f * D, -0.16f * D), P(-0.255f * D, -0.05f * D));
                    p.AddBezier(P(-0.255f * D, -0.05f * D), P(-0.235f * D, 0.045f * D), P(-0.13f * D, 0.1f * D), P(0, 0.1f * D));
                    p.CloseFigure();
                    return p;
                }
            }
        }

        // The main shape and everything fixed on it (PaintBody): Finn's face in his hat, Jake's ears, BMO's screen and
        // buttons, Pakkun's hood, the ninjas' locks, forehead protectors and Kakashi's mask. None of it moves, so it is
        // drawn once and kept.
        static void ToonHead(Graphics g, int kind, GraphicsPath body, float D)
        {
            RectangleF u;
            if (IsSoftFig(kind)) { DsCached(g, D, TnId(kind, 2), PkHeadArea(kind), bg => DrawHead(bg, kind, D)); return; }
            switch (kind)
            {
                case KindFinn: u = new RectangleF(-0.17f, -0.715f, 0.34f, 0.305f); break;
                case KindJake: u = new RectangleF(-0.28f, -0.52f, 0.56f, 0.71f); break;
                case KindBmoAt: u = new RectangleF(-0.22f, -0.38f, 0.44f, 0.6f); break;
                case KindPakkun: u = new RectangleF(-0.31f, -0.36f, 0.62f, 0.55f); break;
                case KindChopper: u = new RectangleF(-0.23f, -0.33f, 0.46f, 0.35f); break;
                case KindKeroSd: u = new RectangleF(-0.3f, -0.48f, 0.6f, 0.52f); break;
                case KindSnoopySd: u = new RectangleF(-0.31f, -0.52f, 0.68f, 0.48f); break;
                case KindGonSd: u = new RectangleF(-0.42f, -0.66f, 0.84f, 0.8f); break;
                case KindHisokaSd: u = new RectangleF(-0.36f, -0.52f, 0.72f, 0.66f); break;
                case KindShinSd: u = new RectangleF(-0.36f, -0.66f, 0.72f, 0.53f); break;
                case KindKantamSd: u = new RectangleF(-0.4f, -0.92f, 0.8f, 0.82f); break;
                case KindUltraSd: u = new RectangleF(-0.4f, -0.82f, 0.8f, 0.69f); break;
                case KindPiggySd: u = new RectangleF(-0.37f, -0.74f, 0.74f, 0.6f); break;
                case KindWaniSd: u = new RectangleF(-0.39f, -0.75f, 0.78f, 0.69f); break;
                case KindNevadoSd: u = new RectangleF(-0.52f, -0.24f, 0.88f, 0.54f); break;
                default: u = IsPersona(kind) ? PersonaHeadArea(kind) : new RectangleF(-0.34f, -0.42f, 0.68f, 0.56f); break;
            }
            int id = TnId(kind, 2);
            if (kind == KindWizardSd && WornHat != 0) id += 50000;   // his hat off, for the one picked for him
            DsCached(g, D, id, u, bg => DrawHead(bg, kind, D));
        }

        // Wearing a hat of their own: Chopper's, Luffy's straw hat, the wizard's. A hat picked for them goes on top of
        // Chopper's and Luffy's and in place of the wizard's; the seasonal and night ones leave all three be.
        public static bool OwnHat(int kind) { return kind == KindChopper || kind == KindLuffySd || kind == KindWizardSd; }

        // The hat worn while a character is being painted (Mascot.Paint sets it), for the ones that take theirs off for it.
        [ThreadStatic] internal static int WornHat;

        static void DrawHead(Graphics g, int kind, float D)
        {
            using (GraphicsPath body = ToonPath(kind, D))
                if (IsPersona(kind)) PersonaHead(g, kind, body, D);
                else if (IsSoftFig(kind)) PkHeadDraw(g, kind, body, D);
                else switch (kind)
                {
                    case KindFinn:
                        VFill(g, kind, body, Color.FromArgb(252, 252, 250), Color.FromArgb(216, 224, 234), D, 90);
                        using (GraphicsPath f = FinnFace(D)) VFill(g, kind, f, FinnSkin, Color.FromArgb(242, 204, 190), D, 0);
                        break;
                    case KindJake:
                        VFill(g, kind, body, JakeFur, JakeShade, D, 90);
                        break;
                    case KindBmoAt:
                        VFill(g, kind, body, Color.FromArgb(104, 204, 182), Color.FromArgb(78, 176, 154), D, 80);
                        BmoFront(g, D);
                        break;
                    case KindPakkun:
                        VFill(g, kind, body, Color.FromArgb(218, 158, 94), Color.FromArgb(188, 126, 66), D, 50);
                        PakkunHood(g, D);
                        break;
                    case KindChopper: ChopperFace(g, body, D); break;
                    case KindKeroSd:
                        foreach (int s in Sides) KeroEarSd(g, D, s);
                        VFill(g, kind, body, KeFur, KeFurShade, D, 80);
                        break;
                    case KindSnoopySd: VFill(g, kind, body, SnWhite, SnShade, D, 90); break;
                    case KindGonSd:
                    case KindKilluaSd:
                    case KindHisokaSd: HunterHead(g, kind, body, D); break;
                    case KindTanjiroSd:
                    case KindNezukoSd: SlayerHead(g, kind, body, D); break;
                    case KindLuffySd:
                    case KindZoroSd: PirateHead(g, kind, body, D); break;
                    case KindKaminaSd: KmHead(g, body, D); break;
                    case KindShinSd:
                    case KindKantamSd:
                    case KindUltraSd:
                    case KindWaniSd: ShinHeadDraw(g, kind, body, D); break;
                    case KindPiggySd: BuriHeadDraw(g, body, D); break;
                    case KindNevadoSd: NevadoHeadDraw(g, body, D); break;
                    default:
                    {
                        // The face stops under the forehead protector: above it, the hair behind shows.
                        float cy, angle;
                        BandAt(kind, out cy, out angle);
                        Region old = g.Clip;
                        ClipBelowBand(g, D, 0, cy, angle);
                        VFill(g, kind, body, NjSkin, NjSkinShade, D, 0);
                        g.Clip = old;
                        old.Dispose();
                        NinjaFront(g, kind, D);
                        break;
                    }
                }
        }

        // The show's colours.
        static readonly Color FinnSkin = Color.FromArgb(255, 213, 194), FinnShirt = Color.FromArgb(14, 156, 208), FinnShirtShade = Color.FromArgb(4, 122, 174);
        internal static readonly Color JakeFur = Color.FromArgb(255, 190, 32), JakeShade = Color.FromArgb(230, 146, 12);

        // The opening of his hat: an oval his face fills, three quarters as wide as the hat.
        static GraphicsPath FinnFace(float D)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(-0.11f * D, -0.62f * D, 0.22f * D, 0.158f * D);
            return p;
        }

        // BMO's front as the show draws it: the big pale screen with its glare; under it the cartridge slot and a small dark
        // blue button; the yellow cross on the left; the blue triangle and the green button on the right with the big red
        // one below them; and two short slots at the bottom.
        static void BmoFront(Graphics g, float D)
        {
            int k = KindBmoAt;
            Color dark = Color.FromArgb(34, 46, 78);
            using (GraphicsPath scr = Theme.Round(new RectangleF(-0.162f * D, -0.321f * D, 0.324f * D, 0.258f * D), 0.03f * D))
            {
                VFill(g, k, scr, Color.FromArgb(230, 250, 238), Color.FromArgb(204, 240, 220), D, 0);
                Region old = g.Clip;
                g.SetClip(scr, CombineMode.Intersect);
                using (SolidBrush w = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                    g.FillPolygon(w, new PointF[] { P(-0.165f * D, -0.325f * D), P(-0.05f * D, -0.325f * D), P(-0.165f * D, -0.19f * D) });
                g.Clip = old;
                old.Dispose();
            }
            VRound(g, k, D, dark, Color.Empty, 0, -0.128f, -0.034f, 0.144f, 0.022f, 0.01f);
            VOval(g, k, D, dark, Color.Empty, 60, 0.125f, -0.036f, 0.03f, 0.03f);
            using (GraphicsPath cross = new GraphicsPath())
            {
                float cx = -0.105f, cy = 0.072f, a = 0.036f, b = 0.013f;
                cross.AddPolygon(new PointF[] { P((cx - b) * D, (cy - a) * D), P((cx + b) * D, (cy - a) * D), P((cx + b) * D, (cy - b) * D), P((cx + a) * D, (cy - b) * D),
                                                P((cx + a) * D, (cy + b) * D), P((cx + b) * D, (cy + b) * D), P((cx + b) * D, (cy + a) * D), P((cx - b) * D, (cy + a) * D),
                                                P((cx - b) * D, (cy + b) * D), P((cx - a) * D, (cy + b) * D), P((cx - a) * D, (cy - b) * D), P((cx - b) * D, (cy - b) * D) });
                VFill(g, k, cross, Color.FromArgb(250, 212, 46), Color.FromArgb(226, 172, 20), D, 90);
            }
            VPoly(g, k, D, Color.FromArgb(80, 176, 236), Color.FromArgb(52, 136, 206), 0, 0.04f, 0.058f, 0.08f, 0.058f, 0.06f, 0.022f);
            VOval(g, k, D, Color.FromArgb(92, 200, 96), Color.FromArgb(58, 160, 64), 110, 0.127f, 0.045f, 0.04f, 0.04f);
            VOval(g, k, D, Color.FromArgb(236, 56, 60), Color.FromArgb(194, 30, 40), 140, 0.085f, 0.112f, 0.08f, 0.08f);
            VRound(g, k, D, dark, Color.Empty, 0, -0.118f, 0.145f, 0.042f, 0.016f, 0.008f);
            VRound(g, k, D, dark, Color.Empty, 0, -0.046f, 0.145f, 0.042f, 0.016f, 0.008f);
        }

        // Pakkun's head in the blue cloth of his forehead protector, down over his brow and the sides of his head, and its
        // steel plate.
        static void PakkunHood(Graphics g, float D)
        {
            int k = KindPakkun;
            using (GraphicsPath head = ToonPath(k, D))
            using (GraphicsPath hood = new GraphicsPath())
            {
                hood.AddCurve(new PointF[] { P(-0.33f * D, 0.03f * D), P(-0.245f * D, -0.125f * D), P(-0.12f * D, -0.168f * D), P(0, -0.174f * D),
                                             P(0.12f * D, -0.168f * D), P(0.245f * D, -0.125f * D), P(0.33f * D, 0.03f * D) }, 0.5f);
                hood.AddLine(P(0.33f * D, 0.03f * D), P(0.33f * D, -0.5f * D));
                hood.AddLine(P(0.33f * D, -0.5f * D), P(-0.33f * D, -0.5f * D));
                hood.CloseFigure();
                Region old = g.Clip;
                g.SetClip(head, CombineMode.Intersect);
                Stroke(g, hood, TnInk(k), TnW(D) * 2);
                VPaint(g, hood, Color.FromArgb(52, 86, 176), Color.FromArgb(34, 58, 132), D, 60);
                // A couple of folds in the cloth at the sides.
                foreach (int s in Sides) TnLine(g, Color.FromArgb(140, 24, 40, 96), 0.007f, D, s * 0.2f, -0.27f, s * 0.235f, -0.2f, s * 0.255f, -0.14f);
                g.Clip = old;
                old.Dispose();
            }
            Plate(g, k, D, 0, -0.222f, 0, 0.27f, 0.088f);
        }

        // ---- The forehead protector of the leaf village.

        // The band's shape in its own frame (scaled by D): long enough to cross the whole face, sagging a little in the
        // middle as it wraps round the brow; or, with above, everything above its top edge (where the hair goes).
        static GraphicsPath BandEdge(float D, float h, bool above)
        {
            float hw = 0.42f, sag = 0.02f * 1.33f;
            GraphicsPath p = new GraphicsPath();
            p.AddBezier(P(-hw * D, -h / 2 * D), P(-hw * 0.4f * D, (-h / 2 + sag) * D), P(hw * 0.4f * D, (-h / 2 + sag) * D), P(hw * D, -h / 2 * D));
            if (above)
            {
                p.AddLine(P(hw * D, -h / 2 * D), P(hw * D, -1.2f * D));
                p.AddLine(P(hw * D, -1.2f * D), P(-hw * D, -1.2f * D));
            }
            else
            {
                p.AddLine(P(hw * D, -h / 2 * D), P(hw * D, h / 2 * D));
                p.AddBezier(P(hw * D, h / 2 * D), P(hw * 0.4f * D, (h / 2 + sag) * D), P(-hw * 0.4f * D, (h / 2 + sag) * D), P(-hw * D, h / 2 * D));
            }
            p.CloseFigure();
            return p;
        }

        // The blue band round the head (centred at (cx, cy), turned by angle, h deep), clipped to the face.
        static void Band(Graphics g, int kind, GraphicsPath face, float D, float cx, float cy, float angle, float h)
        {
            Region old = g.Clip;
            g.SetClip(face, CombineMode.Intersect);
            GraphicsState st = g.Save();
            g.TranslateTransform(cx * D, cy * D);
            g.RotateTransform(angle);
            using (GraphicsPath band = BandEdge(D, h, false))
            {
                Stroke(g, band, TnInk(kind), TnW(D) * 2);
                VPaint(g, band, Color.FromArgb(50, 82, 172), Color.FromArgb(32, 54, 126), D, 0);
            }
            g.Restore(st);
            g.Clip = old;
            old.Dispose();
        }

        // Its steel plate at (cx, cy), turned by angle, pw wide and ph tall: brushed steel with a shine, two rivets at each
        // end and the leaf engraved in the middle.
        static void Plate(Graphics g, int kind, float D, float cx, float cy, float angle, float pw, float ph)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(cx * D, cy * D);
            g.RotateTransform(angle);
            RectangleF pr = new RectangleF(-pw / 2 * D, -ph / 2 * D, pw * D, ph * D);
            using (GraphicsPath plate = Theme.Round(pr, 0.016f * D))
            {
                Stroke(g, plate, TnInk(kind), TnW(D) * 2);
                using (LinearGradientBrush b = new LinearGradientBrush(RectangleF.Inflate(pr, 0, 1), Color.FromArgb(246, 248, 252), Color.FromArgb(150, 160, 180), 90f))
                    g.FillPath(b, plate);
                Region old = g.Clip;
                g.SetClip(plate, CombineMode.Intersect);
                using (SolidBrush w = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                    g.FillPolygon(w, new PointF[] { P(pr.X + pr.Width * 0.07f, pr.Y), P(pr.X + pr.Width * 0.17f, pr.Y), P(pr.X + pr.Width * 0.1f, pr.Bottom), P(pr.X, pr.Bottom) });
                g.Clip = old;
                old.Dispose();
            }
            Color etch = Color.FromArgb(70, 78, 98);
            foreach (int s in Sides)
                for (int i = 0; i < 2; i++)
                    Ellipse(g, etch, (s * (pw / 2 - 0.022f) - 0.0065f) * D, (-ph * 0.27f + i * ph * 0.54f - 0.0065f) * D, 0.013f * D, 0.013f * D);
            LeafMark(g, D, 0, 0.004f, ph * 0.3f, etch);
            g.Restore(st);
        }

        // The leaf village's mark: a swirl opening at the lower left, the leaf's point at its upper left and the stem below.
        static void LeafMark(Graphics g, float D, float cx, float cy, float r, Color c)
        {
            float R = r * D, x = cx * D, y = cy * D;
            using (Pen pen = new Pen(c, Math.Max(0.8f, R * 0.24f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawArc(pen, x - R, y - R, 2 * R, 2 * R, -150, 300);
                g.DrawArc(pen, x - R * 0.5f, y - R * 0.44f, R, R, 160, 250);
                g.DrawLine(pen, x - R * 0.78f, y + R * 0.62f, x - R * 1.3f, y + R * 1.12f);
            }
            using (SolidBrush b = new SolidBrush(c))
                g.FillPolygon(b, new PointF[] { P(x - R * 0.98f, y - R * 0.26f), P(x - R * 1.6f, y - R * 1.4f), P(x - R * 0.26f, y - R * 0.98f) });
        }

        // Leaves out everything above the line through the middle of the forehead protector at (cx, cy), turned by angle:
        // the face stops there, under the band, and the hair behind shows above it.
        static void ClipBelowBand(Graphics g, float D, float cx, float cy, float angle)
        {
            Matrix baseM = g.Transform;
            g.TranslateTransform(cx * D, cy * D);
            g.RotateTransform(angle);
            using (GraphicsPath above = BandEdge(D, 0, true)) g.SetClip(above, CombineMode.Exclude);
            g.Transform = baseM;
            baseM.Dispose();
        }

        // ---- Hair.

        static void NinjaHairColors(int kind, out Color hair, out Color shade, out Color light)
        {
            switch (kind)
            {
                case KindNarutoSd: hair = Color.FromArgb(255, 212, 60); shade = Color.FromArgb(240, 158, 30); light = Color.FromArgb(255, 248, 200); break;
                case KindSasukeSd: hair = Color.FromArgb(48, 54, 92); shade = Color.FromArgb(26, 28, 50); light = Color.FromArgb(118, 136, 200); break;
                default: hair = Color.FromArgb(236, 239, 246); shade = Color.FromArgb(176, 186, 208); light = Color.FromArgb(255, 255, 255); break;
            }
        }

        // From one side of the head over the top to the other: notches and spike tips in turn (closed behind the head).
        // Naruto's: a few big, uneven tufts above his forehead protector, out to the sides and up, as Kishimoto draws them.
        static readonly float[] NarutoHair =
        {
            -0.255f, -0.17f, -0.36f, -0.25f, -0.28f, -0.31f, -0.39f, -0.43f, -0.24f, -0.42f, -0.25f, -0.6f, -0.12f, -0.49f, -0.03f, -0.66f,
            0.04f, -0.5f, 0.17f, -0.62f, 0.18f, -0.46f, 0.36f, -0.5f, 0.28f, -0.36f, 0.42f, -0.3f, 0.29f, -0.24f, 0.34f, -0.17f, 0.255f, -0.16f
        };
        // Sasuke's: a full mop spiking up and out at the back.
        static readonly float[] SasukeHair =
        {
            -0.24f, 0.02f, -0.31f, -0.07f, -0.29f, -0.17f, -0.37f, -0.3f, -0.27f, -0.36f, -0.31f, -0.5f, -0.17f, -0.46f, -0.12f, -0.6f,
            -0.03f, -0.49f, 0.08f, -0.63f, 0.1f, -0.48f, 0.25f, -0.6f, 0.22f, -0.42f, 0.39f, -0.47f, 0.29f, -0.33f, 0.4f, -0.27f,
            0.3f, -0.18f, 0.33f, -0.07f, 0.245f, 0.02f
        };
        // Kakashi's: swept up and over to his left like a flame in the wind.
        static readonly float[] KakashiHair =
        {
            -0.25f, -0.1f, -0.3f, -0.25f, -0.22f, -0.3f, -0.24f, -0.47f, -0.12f, -0.42f, -0.06f, -0.64f, 0.02f, -0.47f, 0.18f, -0.7f,
            0.15f, -0.48f, 0.4f, -0.64f, 0.27f, -0.42f, 0.55f, -0.47f, 0.33f, -0.32f, 0.57f, -0.27f, 0.33f, -0.2f, 0.48f, -0.1f,
            0.28f, -0.1f, 0.36f, 0.0f, 0.255f, -0.02f
        };
        // In front, at the left side of the face (mirrored for the right): Naruto's short spiky locks under the band, down
        // to his eyes; Sasuke's long bangs, from the top of his head down over the ends of the band to his chin, framing his
        // face and leaving the plate clear.
        static readonly float[] NarutoSideLock = { -0.268f, -0.19f, -0.285f, -0.12f, -0.264f, -0.04f, -0.246f, -0.1f, -0.218f, -0.07f, -0.208f, -0.13f, -0.212f, -0.19f };
        static readonly float[] SasukeSideLock =
        {
            -0.12f, -0.41f, -0.21f, -0.35f, -0.28f, -0.24f, -0.295f, -0.1f, -0.272f, 0.02f, -0.245f, 0.1f, -0.228f, 0.02f, -0.205f, 0.05f,
            -0.205f, -0.06f, -0.19f, -0.18f, -0.155f, -0.3f
        };

        // A mass of spiky hair: each spike shaded down its side away from the light, with a light streak down the other,
        // a soft shine over the top and a short line in from each notch towards the head (centred at (0, hy)).
        static void SpikyHair(Graphics g, int kind, float D, float[] xy, Color hair, Color shade, Color light, float hy)
        {
            using (GraphicsPath h = TnSpikes(D, 0.07f, xy))
            {
                Stroke(g, h, TnInk(kind), TnW(D) * 2);
                RectangleF r = h.GetBounds();
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Lighter(hair, 0.16), hair, 90f)) g.FillPath(b, h);
                Region old = g.Clip;
                g.SetClip(h, CombineMode.Intersect);
                int n = xy.Length / 2;
                PointF hc = P(0, hy * D);
                using (SolidBrush sb = new SolidBrush(shade))
                using (Pen lp = new Pen(Color.FromArgb(200, light), Math.Max(0.8f, D * 0.008f)))
                using (Pen np = new Pen(Color.FromArgb(150, TnInk(kind)), Math.Max(0.8f, D * 0.007f)))
                {
                    lp.StartCap = lp.EndCap = np.StartCap = np.EndCap = LineCap.Round;
                    for (int i = 1; i < n - 1; i += 2)
                    {
                        PointF a = P(xy[i * 2 - 2] * D, xy[i * 2 - 1] * D), t = P(xy[i * 2] * D, xy[i * 2 + 1] * D), c = P(xy[i * 2 + 2] * D, xy[i * 2 + 3] * D);
                        bool cFar = c.X + c.Y > a.X + a.Y;
                        PointF far = cFar ? c : a, near = cFar ? a : c, mid = Lerp(a, c, 0.5f);
                        g.FillPolygon(sb, new PointF[] { t, far, Lerp(far, mid, 0.55f), Lerp(t, mid, 0.42f) });
                        g.DrawLine(lp, Lerp(Lerp(t, near, 0.22f), mid, 0.12f), Lerp(Lerp(t, near, 0.5f), mid, 0.5f));
                    }
                    for (int i = 2; i < n - 1; i += 2)
                    {
                        PointF v = P(xy[i * 2] * D, xy[i * 2 + 1] * D);
                        g.DrawLine(np, v, Lerp(v, hc, 0.38f));
                    }
                }
                g.Clip = old;
                old.Dispose();
            }
        }

        // A lock of hair in front of the face, outlined all round, with its shade and a light streak.
        static void SideLock(Graphics g, int kind, float D, float[] xy, Color hair, Color shade, Color light)
        {
            using (GraphicsPath p = TnSpikes(D, -0.05f, xy))
            {
                Stroke(g, p, TnInk(kind), TnW(D) * 2);
                VPaint(g, p, hair, shade, D, 0);
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                RectangleF r = p.GetBounds();
                TnLine(g, Color.FromArgb(200, light), 0.008f, D, (r.X + r.Width * 0.45f) / D, (r.Y + r.Height * 0.12f) / D, (r.X + r.Width * 0.4f) / D, (r.Y + r.Height * 0.6f) / D);
                g.Clip = old;
                old.Dispose();
            }
        }

        // Where each one's forehead protector sits: its centre's height and its tilt (Kakashi's pulled down over his eye).
        static void BandAt(int k, out float cy, out float angle)
        {
            if (k == KindKakashiSd) { cy = -0.105f; angle = 28.2f; }
            else { cy = -0.21f; angle = 0; }
        }

        // The ninjas' heads, fixed parts: Naruto's locks under his forehead protector, Sasuke's long bangs over the ends of
        // his; Kakashi's mask and his protector pulled down over his left eye.
        static void NinjaFront(Graphics g, int k, float D)
        {
            Color hair, shade, light;
            NinjaHairColors(k, out hair, out shade, out light);
            float cy, angle;
            BandAt(k, out cy, out angle);
            using (GraphicsPath face = ToonPath(k, D))
            {
                if (k == KindKakashiSd)
                {
                    KakashiMask(g, face, D);
                    Band(g, k, face, D, 0, cy, angle, 0.095f);
                    Plate(g, k, D, 0.035f, -0.086f, angle, 0.25f, 0.104f);
                    return;
                }
                const float h = 0.085f;
                if (k == KindNarutoSd)
                {
                    SideLock(g, k, D, NarutoSideLock, hair, shade, light);
                    SideLock(g, k, D, Mirror(NarutoSideLock), hair, shade, light);
                }
                Band(g, k, face, D, 0, cy, 0, h);
                Plate(g, k, D, 0, cy - 0.004f, 0, 0.28f, 0.094f);
                if (k == KindSasukeSd)
                {
                    SideLock(g, k, D, SasukeSideLock, hair, shade, light);
                    SideLock(g, k, D, Mirror(SasukeSideLock), hair, shade, light);
                }
            }
        }

        // Kakashi's mask: from just under his eyes over his nose, mouth and neck, the bridge of his nose showing through.
        static void KakashiMask(Graphics g, GraphicsPath face, float D)
        {
            int k = KindKakashiSd;
            Region old = g.Clip;
            g.SetClip(face, CombineMode.Intersect);
            using (GraphicsPath m = new GraphicsPath())
            {
                m.AddCurve(new PointF[] { P(-0.32f * D, 0.04f * D), P(-0.2f * D, 0.05f * D), P(-0.1f * D, 0.047f * D), P(-0.03f * D, 0.022f * D), P(0, 0.01f * D),
                                          P(0.03f * D, 0.022f * D), P(0.1f * D, 0.042f * D), P(0.2f * D, 0.04f * D), P(0.32f * D, 0.03f * D) }, 0.5f);
                m.AddLine(P(0.32f * D, 0.03f * D), P(0.32f * D, 0.4f * D));
                m.AddLine(P(0.32f * D, 0.4f * D), P(-0.32f * D, 0.4f * D));
                m.CloseFigure();
                Stroke(g, m, TnInk(k), TnW(D) * 2);
                VPaint(g, m, Color.FromArgb(50, 62, 108), Color.FromArgb(32, 40, 76), D, 0);
            }
            TnLine(g, Color.FromArgb(80, 255, 255, 255), 0.008f, D, 0.006f, 0.02f, 0.01f, 0.055f);
            g.Clip = old;
            old.Dispose();
        }

        // ---- Behind the main shape: hair, bodies, arms, legs, tails.
        static void ToonBack(Graphics g, MascotLook l, float D, MascotPose p)
        {
            int k = l.Kind;
            Geo geo = ToonGeo(k);
            if (IsPersona(k)) { PersonaBack(g, k, D, p, geo); return; }
            if (IsSoftFig(k)) { PkBack(g, k, D, p, geo); return; }
            switch (k)
            {
                case KindFinn: FinnBody(g, D, p, geo); break;
                case KindJake:
                {
                    // His little tail low at the back, then his long thin legs with their rounded ends, stepping as he walks,
                    // and his long arms.
                    VBlob(g, k, D, JakeFur, JakeShade, 0, 0.45f, -0.22f, 0.1f, -0.29f, 0.06f, -0.345f, 0.0f, -0.33f, 0.07f, -0.28f, 0.13f);
                    foreach (int s in Sides)
                    {
                        PointF f = Foot(p, s, s * 0.147f, 0.39f);
                        VLimb(g, k, D, JakeFur, 0.03f, P(s * 0.147f, 0.11f), f, 0);
                        VOval(g, k, D, JakeFur, Color.Empty, 0, f.X, f.Y, 0.04f, 0.03f);
                        if (!p.HideHands) ToonArm(g, D, k, s, DsHand(p, geo, s));
                    }
                    break;
                }
                case KindBmoAt:
                {
                    Color teal = Color.FromArgb(104, 204, 182);
                    // The side of the box, so it reads as a block rather than a card; then his thin legs with their rounded
                    // ends, stepping as BMO walks, and his arms.
                    DsCached(g, D, TnId(k, 1), new RectangleF(-0.2f, -0.36f, 0.45f, 0.6f), bg => VRound(bg, KindBmoAt, D, Color.FromArgb(62, 150, 132), Color.Empty, 0, -0.174f, -0.34f, 0.4f, 0.56f, 0.065f));
                    foreach (int s in Sides)
                    {
                        PointF f = Foot(p, s, s * 0.085f, 0.335f);
                        VLimb(g, k, D, teal, 0.03f, P(s * 0.08f, 0.19f), f, 0);
                        if (!p.HideHands) ToonArm(g, D, k, s, DsHand(p, geo, s));
                    }
                    break;
                }
                case KindPakkun: PakkunBody(g, D, p, geo); break;
                case KindChopper: ChopperBody(g, D, p, geo); break;
                case KindKeroSd: KeroBody(g, D, p, geo); break;
                case KindSnoopySd: SnoopyBody(g, D, p, geo); break;
                case KindGonSd:
                case KindKilluaSd:
                case KindHisokaSd: HunterBack(g, k, D, p, geo); break;
                case KindTanjiroSd:
                case KindNezukoSd: SlayerBack(g, k, D, p, geo); break;
                case KindLuffySd:
                case KindZoroSd: PirateBack(g, k, D, p, geo); break;
                case KindKaminaSd: KmBack(g, D, p, geo); break;
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: ShinBody(g, k, D, p, geo); break;
                case KindPiggySd: BuriBody(g, D, p, geo); break;
                case KindNevadoSd: NevadoBody(g, D, p); break;
                default: NinjaBack(g, k, D, p, geo); break;
            }
        }

        // Finn as the model sheet has him: under the hat a column just as wide, his blue shirt down to the shorts and their
        // two short legs; long thin legs with the white cuffs of his socks and black shoes pointing out; long thin arms. The
        // backpack showing at his shoulders (kept), his legs stepping as he walks, his arms, and over them his clothes (kept).
        static void FinnBody(Graphics g, float D, MascotPose p, Geo geo)
        {
            int k = KindFinn;
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.21f, -0.48f, 0.42f, 0.25f), bg => FinnBag(bg, D));
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.1375f, 0.295f);
                VLimb(g, k, D, FinnSkin, 0.02f, P(s * 0.1375f, 0.05f), f, 0);
                FinnShoe(g, D, s, f);
                VRound(g, k, D, Color.FromArgb(250, 252, 252), Color.FromArgb(206, 222, 230), 0, f.X - 0.045f, f.Y - 0.023f, 0.09f, 0.046f, 0.023f);
                // The leg going down into the cuff.
                VLimb(g, k, D, FinnSkin, 0.02f, P(f.X, f.Y - 0.034f), P(f.X, f.Y - 0.008f), 0);
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.18f, -0.46f, 0.36f, 0.55f), bg => FinnClothes(bg, D));
        }

        // A black shoe under the sock's cuff at f, its toe pointing out and down, with a soft shine along its top.
        static void FinnShoe(Graphics g, float D, int s, PointF f)
        {
            float[] xy = { -0.02f, 0.006f, 0.02f, 0.006f, 0.024f, 0.04f, 0.05f, 0.058f, 0.078f, 0.075f, 0.09f, 0.092f, 0.078f, 0.104f, 0.04f, 0.1f, 0.0f, 0.094f, -0.022f, 0.08f, -0.026f, 0.045f };
            for (int i = 0; i < xy.Length; i += 2) { xy[i] = f.X + s * xy[i]; xy[i + 1] += f.Y; }
            VBlob(g, KindFinn, D, Color.FromArgb(34, 34, 36), Color.Empty, 0, 0.4f, xy);
            TnLine(g, Color.FromArgb(120, 170, 170, 140), 0.007f, D, f.X + s * 0.03f, f.Y + 0.054f, f.X + s * 0.056f, f.Y + 0.07f, f.X + s * 0.074f, f.Y + 0.084f);
        }

        // His green backpack behind his shoulders, a little wider than he is: the flap's light green over the bag's darker one.
        static void FinnBag(Graphics g, float D)
        {
            int k = KindFinn;
            using (GraphicsPath bag = Theme.Round(new RectangleF(-0.2f * D, -0.465f * D, 0.4f * D, 0.21f * D), 0.055f * D))
            {
                VFill(g, k, bag, Color.FromArgb(80, 166, 39), Color.FromArgb(62, 136, 28), D, 0);
                Region old = g.Clip;
                g.SetClip(bag, CombineMode.Intersect);
                using (SolidBrush fl = new SolidBrush(Color.FromArgb(160, 226, 129))) g.FillRectangle(fl, -0.21f * D, -0.47f * D, 0.42f * D, 0.08f * D);
                TnLine(g, TnInk(k), 0.007f, D, -0.21f, -0.39f, 0.21f, -0.39f);
                g.Clip = old;
                old.Dispose();
            }
        }

        // His blue shirt, a column as wide as the hat down to its hem, over his dark blue shorts and their two short legs;
        // on it the backpack's straps, down from his shoulders and round his sides to it.
        static void FinnClothes(Graphics g, float D)
        {
            int k = KindFinn;
            VPoly(g, k, D, Color.FromArgb(10, 102, 204), Color.FromArgb(4, 76, 166), 30, -0.155f, -0.125f, 0.155f, -0.125f, 0.155f, 0.071f, 0.092f, 0.071f, 0.092f, -0.046f,
                  -0.092f, -0.046f, -0.092f, 0.071f, -0.155f, 0.071f);
            using (GraphicsPath t = Theme.Round(new RectangleF(-0.155f * D, -0.44f * D, 0.31f * D, 0.328f * D), 0.012f * D))
            {
                VFill(g, k, t, FinnShirt, FinnShirtShade, D, 50);
                Region old = g.Clip;
                g.SetClip(t, CombineMode.Intersect);
                foreach (int s in Sides) VLimb(g, k, D, Color.FromArgb(98, 182, 58), 0.02f, P(s * 0.08f, -0.46f), P(s * 0.19f, -0.2f), -s * 0.08f);
                g.Clip = old;
                old.Dispose();
            }
        }

        // Pakkun: his ears and tail (kept), his hind legs stepping as he walks, his round body in the blue cloth tied round
        // his neck (kept), and his front legs, the right one bandaged.
        static void PakkunBody(Graphics g, float D, MascotPose p, Geo geo)
        {
            int k = KindPakkun;
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.37f, -0.27f, 0.74f, 0.56f), bg => PakkunBehind(bg, D));
            Color fur = Color.FromArgb(218, 158, 94);
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.08f, 0.33f);
                VLimb(g, k, D, fur, 0.058f, P(s * 0.075f, 0.27f), f, 0);
                VOval(g, k, D, fur, Color.Empty, 0, f.X + s * 0.008f, f.Y + 0.014f, 0.08f, 0.048f);
                for (int i = 0; i < 2; i++) TnLine(g, Color.FromArgb(150, 110, 70, 40), 0.006f, D, f.X + s * (-0.006f + i * 0.02f), f.Y + 0.02f, f.X + s * (-0.006f + i * 0.02f), f.Y + 0.035f);
            }
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.17f, 0.11f, 0.34f, 0.26f), bg => PakkunTorso(bg, D));
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
        }

        static void PakkunBehind(Graphics g, float D)
        {
            int k = KindPakkun;
            foreach (int s in Sides)
                VBlob(g, k, D, Color.FromArgb(120, 76, 46), Color.FromArgb(92, 56, 32), 0, 0.5f, s * 0.2f, -0.24f, s * 0.31f, -0.22f, s * 0.345f, -0.12f, s * 0.33f, 0.0f, s * 0.29f, 0.04f, s * 0.26f, -0.06f);
            using (Pen ink = new Pen(TnInk(k), 0.05f * D + TnW(D) * 2))
            using (Pen fill = new Pen(Color.FromArgb(218, 158, 94), 0.05f * D))
            {
                ink.StartCap = ink.EndCap = fill.StartCap = fill.EndCap = LineCap.Round;
                g.DrawArc(ink, 0.12f * D, 0.12f * D, 0.11f * D, 0.11f * D, 150, 270);
                g.DrawArc(fill, 0.12f * D, 0.12f * D, 0.11f * D, 0.11f * D, 150, 270);
            }
        }

        static void PakkunTorso(Graphics g, float D)
        {
            int k = KindPakkun;
            Color blue = Color.FromArgb(52, 86, 176), blueShade = Color.FromArgb(34, 58, 132);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddEllipse(-0.14f * D, 0.13f * D, 0.28f * D, 0.21f * D);
                VFill(g, k, body, Color.FromArgb(218, 158, 94), Color.FromArgb(188, 126, 66), D, 40);
                Region old = g.Clip;
                g.SetClip(body, CombineMode.Intersect);
                using (GraphicsPath cloth = new GraphicsPath())
                {
                    cloth.AddLine(P(-0.2f * D, 0.1f * D), P(0.2f * D, 0.1f * D));
                    cloth.AddLine(P(0.2f * D, 0.1f * D), P(0.2f * D, 0.2f * D));
                    cloth.AddBezier(P(0.2f * D, 0.2f * D), P(0.1f * D, 0.235f * D), P(-0.1f * D, 0.235f * D), P(-0.2f * D, 0.2f * D));
                    cloth.CloseFigure();
                    Stroke(g, cloth, TnInk(k), TnW(D) * 2);
                    VPaint(g, cloth, blue, blueShade, D, 40);
                }
                g.Clip = old;
                old.Dispose();
            }
            VBlob(g, k, D, blue, blueShade, 30, 0.5f, -0.035f, 0.15f, 0.035f, 0.15f, 0.04f, 0.19f, 0, 0.2f, -0.04f, 0.19f);
            foreach (int s in Sides) VPoly(g, k, D, blue, blueShade, 0, s * 0.015f, 0.19f, s * 0.05f, 0.25f, s * 0.025f, 0.255f, s * 0.004f, 0.2f);
        }

        // The ninjas: the hair behind the head with the ears (drawn once and kept), the legs stepping as they walk, the arms,
        // and over them the clothes (drawn once and kept).
        static void NinjaBack(Graphics g, int k, float D, MascotPose p, Geo geo)
        {
            DsCached(g, D, TnId(k, 1), new RectangleF(-0.6f, -0.75f, 1.2f, 0.9f), bg => NinjaHairBack(bg, k, D));
            Color leg, legShade, shoe;
            switch (k)
            {
                case KindNarutoSd: leg = Color.FromArgb(250, 142, 38); legShade = Color.FromArgb(224, 104, 24); shoe = Color.FromArgb(58, 94, 196); break;
                case KindSasukeSd: leg = NjSkin; legShade = NjSkinShade; shoe = Color.FromArgb(44, 56, 110); break;
                default: leg = Color.FromArgb(44, 56, 100); legShade = Color.FromArgb(28, 36, 70); shoe = Color.FromArgb(34, 40, 66); break;
            }
            foreach (int s in Sides)
            {
                PointF f = Foot(p, s, s * 0.05f, 0.322f);
                VLimb(g, k, D, leg, k == KindSasukeSd ? 0.044f : 0.062f, P(s * 0.046f, 0.235f), f, 0);
                if (k == KindSasukeSd) VRound(g, k, D, Color.FromArgb(244, 244, 240), Color.Empty, 0, f.X - 0.028f, f.Y - 0.042f, 0.056f, 0.034f, 0.012f);
                VOval(g, k, D, shoe, Color.Empty, 0, f.X + s * 0.006f, f.Y + 0.022f, 0.08f, 0.042f);
            }
            if (!p.HideHands) foreach (int s in Sides) ToonArm(g, D, k, s, DsHand(p, geo, s));
            DsCached(g, D, TnId(k, 3), new RectangleF(-0.2f, -0.02f, 0.4f, 0.34f), bg => NinjaClothes(bg, k, D));
        }

        static void NinjaHairBack(Graphics g, int k, float D)
        {
            Color hair, shade, light;
            NinjaHairColors(k, out hair, out shade, out light);
            SpikyHair(g, k, D, k == KindNarutoSd ? NarutoHair : k == KindSasukeSd ? SasukeHair : KakashiHair, hair, shade, light, -0.25f);
            foreach (int s in Sides)
            {
                VOval(g, k, D, NjSkin, NjSkinShade, 0, s * 0.262f, -0.035f, 0.07f, 0.09f);
                TnLine(g, Color.FromArgb(150, 170, 104, 84), 0.007f, D, s * 0.255f, -0.06f, s * 0.272f, -0.035f, s * 0.258f, -0.012f);
            }
        }

        static void NinjaClothes(Graphics g, int k, float D)
        {
            switch (k)
            {
                case KindNarutoSd:
                {
                    // His orange tracksuit jacket, dark blue across the shoulders, the white collar standing up round his
                    // neck and the zip with its ring.
                    Color orange = Color.FromArgb(250, 142, 38), orangeShade = Color.FromArgb(224, 104, 24), navy = Color.FromArgb(40, 54, 112);
                    using (GraphicsPath t = Theme.Round(new RectangleF(-0.11f * D, 0.07f * D, 0.22f * D, 0.19f * D), 0.045f * D))
                    {
                        VFill(g, k, t, orange, orangeShade, D, 50);
                        Region old = g.Clip;
                        g.SetClip(t, CombineMode.Intersect);
                        using (GraphicsPath y = KPoly(D, -0.13f, 0.04f, 0.13f, 0.04f, 0.13f, 0.125f, 0.045f, 0.142f, -0.045f, 0.142f, -0.13f, 0.125f))
                        {
                            Stroke(g, y, TnInk(k), TnW(D) * 2);
                            VPaint(g, y, navy, Darker(navy, 0.3), D, 30);
                        }
                        g.Clip = old;
                        old.Dispose();
                    }
                    foreach (int s in Sides)
                        VPoly(g, k, D, Color.FromArgb(250, 250, 248), Color.FromArgb(212, 216, 226), 0, s * 0.004f, 0.08f, s * 0.072f, 0.06f, s * 0.066f, 0.114f, s * 0.008f, 0.132f);
                    TnLine(g, TnInk(k), 0.006f, D, 0, 0.132f, 0, 0.258f);
                    using (Pen r = new Pen(TnInk(k), Math.Max(1.2f, D * 0.015f))) g.DrawEllipse(r, 0.008f * D, 0.148f * D, 0.026f * D, 0.032f * D);
                    using (Pen r = new Pen(Color.White, Math.Max(0.8f, D * 0.007f))) g.DrawEllipse(r, 0.008f * D, 0.148f * D, 0.026f * D, 0.032f * D);
                    break;
                }
                case KindSasukeSd:
                {
                    // White shorts under a dark blue shirt, its high collar flaring up beside his chin.
                    Color navy = Color.FromArgb(44, 56, 110), navyShade = Color.FromArgb(28, 36, 76);
                    VRound(g, k, D, Color.FromArgb(240, 238, 230), Color.FromArgb(204, 200, 190), 30, -0.1f, 0.205f, 0.2f, 0.075f, 0.025f);
                    TnLine(g, TnInk(k), 0.006f, D, 0, 0.235f, 0, 0.275f);
                    VRound(g, k, D, navy, navyShade, 50, -0.106f, 0.07f, 0.212f, 0.16f, 0.045f);
                    foreach (int s in Sides) VPoly(g, k, D, navy, navyShade, 0, s * 0.03f, 0.1f, s * 0.055f, 0.035f, s * 0.13f, 0.05f, s * 0.12f, 0.125f);
                    break;
                }
                default:
                {
                    // His green flak jacket with its scroll pockets and thick collar, a red swirl on each shoulder.
                    Color green = Color.FromArgb(98, 134, 74), greenShade = Color.FromArgb(70, 100, 50), pocket = Color.FromArgb(120, 158, 92);
                    VRound(g, k, D, green, greenShade, 50, -0.112f, 0.07f, 0.224f, 0.19f, 0.045f);
                    foreach (int s in Sides)
                    {
                        VRound(g, k, D, pocket, greenShade, 0, s * 0.058f - 0.036f, 0.13f, 0.072f, 0.064f, 0.012f);
                        TnLine(g, TnInk(k), 0.006f, D, s * 0.058f, 0.132f, s * 0.058f, 0.192f);
                    }
                    VBlob(g, k, D, green, greenShade, 30, 0.5f, -0.115f, 0.075f, 0, 0.058f, 0.115f, 0.075f, 0.105f, 0.115f, 0, 0.125f, -0.105f, 0.115f);
                    using (Pen sw = new Pen(Color.FromArgb(214, 50, 44), Math.Max(1f, D * 0.008f)))
                        foreach (int s in Sides)
                        {
                            g.DrawArc(sw, (s * 0.126f - 0.018f) * D, 0.1f * D, 0.036f * D, 0.036f * D, 0, 300);
                            g.DrawArc(sw, (s * 0.126f - 0.008f) * D, 0.11f * D, 0.016f * D, 0.016f * D, 180, 270);
                        }
                    break;
                }
            }
        }

        // ---- Arms (also drawn by the props to the hands that hold them) and hands.
        internal static void ToonArm(Graphics g, float D, int kind, int side, PointF h)
        {
            if (IsPersona(kind)) { PersonaArm(g, D, kind, side, h); return; }
            if (IsSoftFig(kind)) { PkArm(g, D, kind, side, h); return; }
            switch (kind)
            {
                case KindFinn:
                {
                    // A long thin arm, bowed out a little, and the short sleeve at its top.
                    PointF a = P(side * 0.15f, -0.415f);
                    VLimb(g, kind, D, FinnSkin, 0.02f, a, h, side * 0.035f);
                    VLimb(g, kind, D, FinnShirt, 0.04f, a, Lerp(a, h, 0.27f), side * 0.02f);
                    break;
                }
                case KindJake: VLimb(g, kind, D, JakeFur, 0.022f, P(side * 0.245f, -0.19f), h, side * 0.06f); break;
                case KindBmoAt: VLimb(g, kind, D, Color.FromArgb(104, 204, 182), 0.026f, P(side * 0.19f, -0.02f), h, side * 0.04f); break;
                case KindPakkun:
                {
                    PointF a = P(side * 0.095f, 0.17f);
                    VLimb(g, kind, D, Color.FromArgb(218, 158, 94), 0.058f, a, h, 0);
                    if (side < 0)
                    {
                        // The bandage round his right foreleg.
                        PointF b0 = Lerp(a, h, 0.45f), b1 = Lerp(a, h, 0.78f);
                        VLimb(g, kind, D, Color.FromArgb(250, 250, 246), 0.062f, b0, b1, 0);
                        TnLine(g, Color.FromArgb(150, 160, 160, 170), 0.006f, D, Lerp(b0, b1, 0.5f).X - 0.028f, Lerp(b0, b1, 0.5f).Y - 0.006f, Lerp(b0, b1, 0.5f).X + 0.028f, Lerp(b0, b1, 0.5f).Y + 0.006f);
                    }
                    break;
                }
                case KindChopper: VLimb(g, kind, D, ChFur, 0.042f, P(side * 0.1f, 0.022f), h, side * 0.03f); break;
                case KindKeroSd: VLimb(g, kind, D, KeFur, 0.058f, P(side * 0.1f, 0.05f), h, side * 0.02f); break;
                case KindSnoopySd: VLimb(g, kind, D, SnWhite, 0.042f, P(side * 0.04f, 0.045f), h, side * 0.04f); break;
                case KindGonSd:
                case KindKilluaSd:
                case KindHisokaSd: HunterArm(g, D, kind, side, h); break;
                case KindTanjiroSd:
                case KindNezukoSd: SlayerArm(g, D, kind, side, h); break;
                case KindLuffySd:
                case KindZoroSd: PirateArm(g, D, kind, side, h); break;
                case KindKaminaSd: KmArm(g, D, side, h); break;
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: ShinArm(g, D, kind, side, h); break;
                case KindPiggySd: BuriArm(g, D, side, h); break;
                case KindNevadoSd: break;
                case KindNarutoSd: VLimb(g, kind, D, Color.FromArgb(250, 142, 38), 0.056f, P(side * 0.1f, 0.115f), h, side * 0.03f); break;
                case KindSasukeSd:
                {
                    PointF a = P(side * 0.1f, 0.115f);
                    VLimb(g, kind, D, NjSkin, 0.042f, a, h, side * 0.03f);
                    VLimb(g, kind, D, Color.FromArgb(244, 244, 240), 0.05f, Lerp(a, h, 0.55f), Lerp(a, h, 0.86f), 0);   // his arm warmer
                    VLimb(g, kind, D, Color.FromArgb(44, 56, 110), 0.06f, a, Lerp(a, h, 0.3f), 0);                        // the short sleeve
                    break;
                }
                default: VLimb(g, kind, D, Color.FromArgb(44, 56, 100), 0.056f, P(side * 0.1f, 0.115f), h, side * 0.03f); break;
            }
        }

        internal static void ToonHand(Graphics g, float D, int kind)
        {
            if (IsPersona(kind)) { PersonaHand(g, D, kind); return; }
            if (IsSoftFig(kind)) { PkHand(g, D, kind); return; }
            switch (kind)
            {
                case KindFinn: VOval(g, kind, D, FinnSkin, Color.Empty, 0, 0, 0, 0.034f, 0.038f); break;
                case KindJake: VOval(g, kind, D, JakeFur, Color.Empty, 0, 0, 0, 0.04f, 0.036f); break;
                case KindBmoAt: VOval(g, kind, D, Color.FromArgb(104, 204, 182), Color.Empty, 0, 0, 0, 0.038f, 0.036f); break;
                case KindPakkun:
                    VOval(g, kind, D, Color.FromArgb(218, 158, 94), Color.Empty, 0, 0, 0.004f, 0.07f, 0.056f);
                    for (int i = -1; i <= 1; i += 2) TnLine(g, Color.FromArgb(150, 110, 70, 40), 0.006f, D, i * 0.012f, 0.018f, i * 0.012f, 0.03f);
                    break;
                case KindChopper:
                    // His wrist, and the small hoof he has for a hand.
                    VOval(g, kind, D, ChFur, Color.Empty, 0, 0, -0.012f, 0.052f, 0.046f);
                    ChopperHoof(g, D, 0, 0.018f, 0.68f);
                    break;
                case KindKeroSd: VOval(g, kind, D, KeFur, Color.Empty, 0, 0, 0, 0.062f, 0.058f); break;
                case KindSnoopySd: VOval(g, kind, D, SnWhite, Color.Empty, 0, 0, 0, 0.05f, 0.046f); break;
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: ShinHand(g, D, kind); break;
                case KindPiggySd: BuriHand(g, D); break;
                case KindNevadoSd: break;
                case KindKakashiSd: VOval(g, kind, D, Color.FromArgb(44, 56, 100), Color.Empty, 0, 0, 0, 0.056f, 0.052f); break;
                default: VOval(g, kind, D, NjSkin, Color.Empty, 0, 0, 0, 0.056f, 0.052f); break;
            }
        }

        // ---- In front of the face, turning with it.
        static void ToonDetails(Graphics g, MascotLook l, Geo geo, float D, MascotPose p)
        {
            int k = l.Kind;
            float f = geo.FaceY;
            if (IsPersona(k)) { PersonaDetails(g, k, D, f); return; }
            if (IsSoftFig(k)) { PkDetails(g, k, D, f); return; }
            switch (k)
            {
                case KindBmoAt:
                    foreach (int s in Sides) Glow(g, s * 0.1f * D, (f + 0.05f) * D, 0.05f * D, 0.026f * D, Color.FromArgb(255, 120, 150), 70);
                    break;
                case KindJake:
                    // His muzzle as the figure has it, over the inner side of his eyes: an arch rising between them, the two
                    // jowls hanging from it apart, and the big black nose with its shine where they part.
                    VBlob(g, k, D, JakeFur, JakeShade, 40, 0.5f, 0, f - 0.0302f, 0.0302f, f - 0.0258f, 0.0553f, f - 0.0151f, 0.0773f, f + 0.0025f, 0.093f, f + 0.0244f,
                          0.1043f, f + 0.0465f, 0.11f, f + 0.0684f, 0.1087f, f + 0.0967f, 0.1006f, f + 0.1187f, 0.0836f, f + 0.1312f, 0.0647f, f + 0.1331f, 0.0459f, f + 0.1219f,
                          0.0333f, f + 0.1061f, 0.0207f, f + 0.0892f, 0.0126f, f + 0.0804f, -0.0126f, f + 0.0804f, -0.0207f, f + 0.0892f, -0.0333f, f + 0.1061f,
                          -0.0459f, f + 0.1219f, -0.0647f, f + 0.1331f, -0.0836f, f + 0.1312f, -0.1006f, f + 0.1187f, -0.1087f, f + 0.0967f, -0.11f, f + 0.0684f,
                          -0.1043f, f + 0.0465f, -0.093f, f + 0.0244f, -0.0773f, f + 0.0025f, -0.0553f, f - 0.0151f, -0.0302f, f - 0.0258f);
                    VOval(g, k, D, Color.FromArgb(34, 30, 36), Color.Empty, 0, 0, f + 0.0578f, 0.0804f, 0.0603f);
                    Ellipse(g, Color.FromArgb(200, 255, 255, 255), -0.029f * D, (f + 0.037f) * D, 0.024f * D, 0.013f * D);
                    break;
                case KindPakkun:
                {
                    // The big dark muzzle over the lower half of his face, his black nose, his frown, the wrinkles over his
                    // eyes and the bags under them.
                    Color crease = Color.FromArgb(160, 130, 80, 44);
                    VBlob(g, k, D, Color.FromArgb(98, 64, 44), Color.FromArgb(76, 48, 32), 40, 0.45f, 0, f + 0.032f, 0.12f, f + 0.04f, 0.172f, f + 0.095f, 0.168f, f + 0.17f,
                          0.1f, f + 0.222f, 0, f + 0.232f, -0.1f, f + 0.222f, -0.168f, f + 0.17f, -0.172f, f + 0.095f, -0.12f, f + 0.04f);
                    VBlob(g, k, D, Color.FromArgb(26, 20, 20), Color.Empty, 80, 0.5f, -0.044f, f + 0.05f, 0.044f, f + 0.05f, 0.036f, f + 0.082f, 0, f + 0.094f, -0.036f, f + 0.082f);
                    TnLine(g, Color.FromArgb(36, 22, 16), 0.009f, D, 0, f + 0.094f, 0, f + 0.15f);
                    TnLine(g, Color.FromArgb(36, 22, 16), 0.012f, D, -0.078f, f + 0.198f, -0.04f, f + 0.17f, 0, f + 0.162f, 0.04f, f + 0.17f, 0.078f, f + 0.198f);
                    foreach (int s in Sides)
                    {
                        TnLine(g, crease, 0.008f, D, s * 0.06f, f - 0.052f, s * 0.11f, f - 0.064f, s * 0.158f, f - 0.054f);
                        TnLine(g, crease, 0.007f, D, s * 0.072f, f + 0.042f, s * 0.11f, f + 0.05f, s * 0.15f, f + 0.04f);
                        TnLine(g, crease, 0.007f, D, s * 0.012f, f - 0.07f, s * 0.03f, f - 0.05f);
                    }
                    break;
                }
                case KindNarutoSd:
                    // Short brows under the band, a little blush and the three whisker marks on each cheek.
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(220, 176, 112, 30), 0.012f, D, s * 0.06f, f - 0.098f, s * 0.11f, f - 0.108f, s * 0.152f, f - 0.1f);
                        Glow(g, s * 0.16f * D, (f + 0.065f) * D, 0.085f * D, 0.04f * D, Color.FromArgb(255, 130, 130), 80);
                        for (int i = 0; i < 3; i++)
                            TnLine(g, Color.FromArgb(210, 110, 64, 48), 0.0065f, D, s * 0.135f, f + 0.045f + i * 0.022f, s * 0.21f, f + 0.038f + i * 0.026f);
                    }
                    break;
                case KindSasukeSd:
                    foreach (int s in Sides)
                    {
                        TnLine(g, Color.FromArgb(230, 36, 36, 56), 0.013f, D, s * 0.045f, f - 0.086f, s * 0.1f, f - 0.1f, s * 0.152f, f - 0.106f);
                        Glow(g, s * 0.16f * D, (f + 0.065f) * D, 0.08f * D, 0.036f * D, Color.FromArgb(255, 140, 140), 50);
                    }
                    break;
                case KindChopper: ChopperNose(g, D, f); break;
                case KindKeroSd: KeroNose(g, D, f); break;
                case KindGonSd:
                case KindKilluaSd:
                case KindHisokaSd: HunterDetails(g, k, D, f); break;
                case KindTanjiroSd:
                case KindNezukoSd: SlayerDetails(g, k, D, f); break;
                case KindLuffySd:
                case KindZoroSd: PirateDetails(g, k, D, f); break;
                case KindKaminaSd: KmShades(g, D, f); break;
                case KindKakashiSd:
                    // One grey brow over his visible eye, relaxed.
                    TnLine(g, Color.FromArgb(220, 130, 136, 156), 0.011f, D, -0.05f, f - 0.06f, -0.1f, f - 0.068f, -0.158f, f - 0.062f);
                    break;
            }
        }

        static string[] ToonLines(int kind)
        {
            if (IsPersona(kind)) return PersonaLines(kind);
            if (IsSoftFig(kind)) return PkLines(kind);
            if (!I18n.IsSpanish)
            {
                string[] en = ToonLinesEn(kind);
                if (en != null) return en;
            }
            switch (kind)
            {
                case KindFinn: return new string[] { "\u00A1Matem\u00E1tico!", "\u00A1Algebraico!", "\u00A1Es la hora de aventuras!", "\u00A1Vamos, Jake!", "Soy Finn, el humano." };
                case KindJake: return new string[] { "\u00A1Hora de aventuras!", "\u00A1T\u00EDo, eso ha sido matem\u00E1tico!", "Ser malo en algo es el primer paso para ser algo bueno en ello.", "Soy Jake, el perro." };
                case KindBmoAt: return new string[] { "\u00BFQui\u00E9n quiere jugar a videojuegos?", "\u00A1BMO est\u00E1 aqu\u00ED!", "BMO te quiere." };
                case KindPakkun: return new string[] { "Yo.", "Mi olfato nunca falla.", "\u00BFOtra vez me llamas, Kakashi?" };
                case KindNarutoSd: return new string[] { "\u00A1Dattebayo!", "\u00A1Ser\u00E9 el pr\u00F3ximo Hokage!", "\u00A1Nunca retiro mi palabra! \u00A1Ese es mi camino ninja!", "\u00A1Kage Bunshin no Jutsu!", "\u00A1Me apetece un ramen!" };
                case KindSasukeSd: return new string[] { "Hmph.", "Eres molesta.", "Lo que tengo no es un sue\u00F1o, porque lo har\u00E9 realidad.", "\u00A1Chidori!" };
                case KindChopper: return new string[] { "\u00A1Que me llames mono no me alegra nada, idiota!", "\u00A1Un m\u00E9dico! \u00A1Que alguien llame a un m\u00E9dico! \u2026Ah, que el m\u00E9dico soy yo.",
                                                        "\u00A1No soy un mapache, soy un reno!", "\u00A1Algod\u00F3n de az\u00FAcar!", "Soy Tony Tony Chopper, el m\u00E9dico de los Sombrero de Paja." };
                case KindKeroSd: return new string[] { "\u00A1D\u00E9jaselo a Kero-chan!", "Soy Kerberos, la bestia guardiana del sello.", "\u00BFQueda pudin?", "\u00A1Sakura, a por esa carta!",
                                                       "\u00BFEchamos una partida a la consola?" };
                case KindGonSd:
                case KindKilluaSd:
                case KindHisokaSd: return HunterLines(kind);
                case KindTanjiroSd:
                case KindNezukoSd: return SlayerLines(kind);
                case KindLuffySd:
                case KindZoroSd: return PirateLines(kind);
                case KindKaminaSd: return KmLines();
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd: return ShinLines(kind);
                case KindPiggySd: return BuriLines();
                case KindNevadoSd: return NevadoLines();
                case KindSnoopySd: return new string[] { "\u00A1Hora de bailar!", "Necesito mi caseta\u2026", "\u00A1Maldito Bar\u00F3n Rojo!", "\u00BFYa es la hora de cenar?",
                                                         "\u00A1Woodstock, ven!" };
                default: return new string[] { "Yo.", "Mi primera impresi\u00F3n de vosotros\u2026 os odio.", "Los que rompen las reglas son escoria, pero los que abandonan a sus amigos son peor que escoria.",
                                               "Perd\u00F3n por el retraso: me perd\u00ED en el camino de la vida." };
            }
        }

        // The English lines of the ones ToonLines writes itself; null for those it hands to another file (each of
        // those answers in both languages).
        static string[] ToonLinesEn(int kind)
        {
            switch (kind)
            {
                case KindFinn: return new string[] { "Mathematical!", "Algebraic!", "It's adventure time!", "Come on, Jake!", "I'm Finn the Human." };
                case KindJake: return new string[] { "Adventure time!", "Dude, that was mathematical!", "Sucking at something is the first step to being sorta good at something.", "I'm Jake the Dog." };
                case KindBmoAt: return new string[] { "Who wants to play video games?", "BMO is here!", "BMO loves you." };
                case KindPakkun: return new string[] { "Yo.", "My nose never fails.", "Summoning me again, Kakashi?" };
                case KindNarutoSd: return new string[] { "Dattebayo!", "I'm gonna be the next Hokage!", "I never go back on my word! That's my ninja way!", "Kage Bunshin no Jutsu!", "I could go for some ramen!" };
                case KindSasukeSd: return new string[] { "Hmph.", "You're annoying.", "What I have is not a dream, because I will make it a reality.", "Chidori!" };
                case KindChopper: return new string[] { "Calling me cute won't make me happy, you jerk!", "A doctor! Somebody call a doctor! \u2026Oh wait, I'm the doctor.",
                                                        "I'm not a raccoon, I'm a reindeer!", "Cotton candy!", "I'm Tony Tony Chopper, the Straw Hats' doctor." };
                case KindKeroSd: return new string[] { "Leave it to Kero-chan!", "I'm Cerberus, guardian beast of the seal.", "Any pudding left?", "Sakura, go get that card!",
                                                       "Wanna play a video game?" };
                case KindGonSd:
                case KindKilluaSd:
                case KindHisokaSd:
                case KindTanjiroSd:
                case KindNezukoSd:
                case KindLuffySd:
                case KindZoroSd:
                case KindKaminaSd:
                case KindShinSd:
                case KindKantamSd:
                case KindUltraSd:
                case KindWaniSd:
                case KindPiggySd:
                case KindNevadoSd: return null;
                case KindSnoopySd: return new string[] { "Time to dance!", "I need my doghouse\u2026", "Curse you, Red Baron!", "Is it suppertime yet?",
                                                         "Woodstock, come here!" };
                default: return new string[] { "Yo.", "My first impression of you guys\u2026 I hate you.", "Those who break the rules are scum, but those who abandon their friends are worse than scum.",
                                               "Sorry I'm late: I got lost on the path of life." };
            }
        }
    }

    public partial class Mascot
    {
        bool ToonEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            float sk = surprised ? 1.15f : 1f;
            if (MascotParts.IsPersona(Look.Kind)) return PersonaEye(g, x, y, D, blink, surprised);
            if (MascotParts.IsSoftFig(Look.Kind)) return PkEye(g, x, y, D, blink, surprised);
            switch (Look.Kind)
            {
                case MascotParts.KindFinn: FinnDot(g, x, y, D, sk, blink); return true;
                case MascotParts.KindBmoAt: return DotEye(g, x, y, D, 0.028f * D * sk, 0.042f * D * sk, blink);
                case MascotParts.KindJake: JakeEye(g, x, y, D, sk, blink); return true;
                case MascotParts.KindChopper: ChopperEye(g, x, y, D, sk, blink, surprised ? 0.5f : 0.8f); return true;
                case MascotParts.KindKeroSd: return DotEye(g, x, y, D, 0.025f * D * sk, 0.033f * D * sk, blink);
                case MascotParts.KindSnoopySd: return DotEye(g, x, y, D, 0.022f * D * sk, 0.062f * D * sk, blink);
                case MascotParts.KindGonSd: return NinjaEye(g, x, y, D, blink, surprised, 0.12f, 0.15f, Color.FromArgb(110, 58, 18), Color.FromArgb(236, 164, 60), 1f);
                case MascotParts.KindKilluaSd: return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.13f, Color.FromArgb(34, 62, 150), Color.FromArgb(132, 188, 250), 0.84f);
                case MascotParts.KindHisokaSd: return NinjaEye(g, x, y, D, blink, surprised, 0.12f, 0.11f, Color.FromArgb(168, 108, 20), Color.FromArgb(252, 214, 90), 0.55f);
                case MascotParts.KindTanjiroSd:
                case MascotParts.KindNezukoSd: return DotEye(g, x, y, D, 0.05f * D * sk, 0.05f * D * sk, blink);
                case MascotParts.KindLuffySd: return NinjaEye(g, x, y, D, blink, surprised, 0.12f, 0.15f, Color.FromArgb(24, 22, 28), Color.FromArgb(70, 66, 80), 1f);
                case MascotParts.KindZoroSd: return NinjaEye(g, x, y, D, blink, surprised, 0.115f, 0.12f, Color.FromArgb(24, 26, 30), Color.FromArgb(90, 96, 100), 0.72f);
                case MascotParts.KindKaminaSd: return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.12f, Color.FromArgb(40, 30, 30), Color.FromArgb(110, 80, 60), 0.7f);
                case MascotParts.KindShinSd: return DotEye(g, x, y, D, 0.1f * D * sk, 0.108f * D * sk, blink);
                case MascotParts.KindNevadoSd:
                case MascotParts.KindKantamSd:
                case MascotParts.KindUltraSd:
                case MascotParts.KindPiggySd:
                case MascotParts.KindWaniSd: return DotEye(g, x, y, D, 0.03f * D * sk, 0.034f * D * sk, blink);
                case MascotParts.KindPakkun:
                {
                    // Sleepy eyes: a small black pupil under a heavy lid that hides the top half of the eye.
                    Color ink = Color.FromArgb(46, 30, 28);
                    float w = 0.094f * D * sk, h = 0.07f * D * sk;
                    if (blink < 0.4f) { MascotParts.Line(g, ink, Math.Max(1f, D * 0.013f), x - w / 2, y, x + w / 2, y); return true; }
                    RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
                    using (GraphicsPath ep = new GraphicsPath())
                    {
                        ep.AddEllipse(r);
                        MascotParts.FillSolid(g, ep, Color.FromArgb(252, 250, 244));
                        Region old = g.Clip;
                        g.SetClip(ep, CombineMode.Intersect);
                        MascotParts.Ellipse(g, Color.FromArgb(22, 16, 16), x - 0.019f * D, y - 0.008f * D, 0.038f * D, 0.038f * D);
                        MascotParts.Ellipse(g, Color.FromArgb(230, 255, 255, 255), x - 0.012f * D, y - 0.002f * D, 0.012f * D, 0.012f * D);
                        if (!surprised) using (SolidBrush lidB = new SolidBrush(Color.FromArgb(214, 152, 88))) g.FillRectangle(lidB, r.X - 1, r.Y - 1, r.Width + 2, h * 0.5f + 1);
                        g.Clip = old;
                        old.Dispose();
                        using (Pen p = new Pen(ink, Math.Max(1f, D * 0.011f))) g.DrawPath(p, ep);
                    }
                    if (!surprised) using (Pen p = new Pen(ink, Math.Max(1.2f, D * 0.016f))) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, x - w * 0.56f, y, x + w * 0.56f, y); }
                    return true;
                }
                case MascotParts.KindNarutoSd:
                    return NinjaEye(g, x, y, D, blink, surprised, 0.118f, 0.15f, Color.FromArgb(24, 88, 196), Color.FromArgb(120, 200, 255), 1f);
                case MascotParts.KindSasukeSd:
                    return NinjaEye(g, x, y, D, blink, surprised, 0.115f, 0.135f, Color.FromArgb(18, 18, 28), Color.FromArgb(86, 86, 108), 0.84f);
                default:
                    if (x > 0) return true;   // under the forehead protector
                    return NinjaEye(g, x, y, D, blink, surprised, 0.115f, 0.14f, Color.FromArgb(30, 30, 38), Color.FromArgb(100, 100, 116), 0.5f);
            }
        }

        // A ninja's eye (w and h in units of D): while simply open it never changes, so it is drawn once and kept; drawn
        // afresh while it blinks or goes wide.
        bool NinjaEye(Graphics g, float x, float y, float D, float blink, bool surprised, float w, float h, Color top, Color bottom, float open)
        {
            int side = x < 0 ? -1 : 1;
            if (surprised || blink < 0.999f)
            {
                float sk = surprised ? 1.15f : 1f;
                return AnimeEye(g, x, y, D, w * D * sk, h * D * sk, top, bottom, surprised ? Math.Max(open, 0.95f) : open, blink, side);
            }
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            MascotParts.DsCached(g, D, MascotParts.TnId(Look.Kind, side > 0 ? 6 : 5), new RectangleF(-0.1f, -0.1f, 0.2f, 0.19f),
                                 bg => AnimeEye(bg, 0, 0, D, w * D, h * D, top, bottom, open, 1, side));
            g.Restore(st);
            return true;
        }

        // Jake's eye as the figure has it: a deep round socket, dark, with a light lip round it, and in it the white eyeball
        // sitting a little low and out, so the dark shows at the inner top, turning to look about; to blink, the lid comes
        // down over it.
        void JakeEye(Graphics g, float x, float y, float D, float sk, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 34);
            float rs = 0.0817f * D * sk, o = x < 0 ? -1 : 1, lx = (float)lookX * (flipped ? -1 : 1), ly = (float)lookY;
            RectangleF sr = new RectangleF(x - rs, y - rs, rs * 2, rs * 2);
            using (Pen lip = new Pen(Color.FromArgb(210, 255, 230, 150), Math.Max(1f, D * 0.007f))) g.DrawEllipse(lip, RectangleF.Inflate(sr, D * 0.0035f, D * 0.0035f));
            MascotParts.Ellipse(g, Color.FromArgb(26, 22, 28), sr.X, sr.Y, sr.Width, sr.Height);
            float rb = rs * 0.89f, bx = x + o * rs * 0.04f + lx * rs * 0.08f, by = y + rs * 0.05f + ly * rs * 0.06f;
            using (GraphicsPath ball = new GraphicsPath())
            {
                ball.AddEllipse(bx - rb, by - rb, rb * 2, rb * 2);
                using (PathGradientBrush pb = new PathGradientBrush(ball))
                {
                    pb.CenterPoint = new PointF(bx - rb * 0.25f, by - rb * 0.3f);
                    pb.CenterColor = Color.White;
                    pb.SurroundColors = new Color[] { Color.FromArgb(196, 204, 216) };
                    g.FillPath(pb, ball);
                }
            }
            MascotParts.Ellipse(g, Color.FromArgb(235, 255, 255, 255), bx - rb * 0.55f, by - rb * 0.62f, rb * 0.42f, rb * 0.3f);
            if (blink >= 0.999f) return;
            // The lid, down to where the blink has it, its edge a dark line.
            float lidY = sr.Y + sr.Height * (1 - blink);
            Region old = g.Clip;
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddEllipse(RectangleF.Inflate(sr, D * 0.002f, D * 0.002f));
                g.SetClip(sp, CombineMode.Intersect);
            }
            using (SolidBrush lid = new SolidBrush(MascotParts.JakeFur)) g.FillRectangle(lid, sr.X - 2, sr.Y - 2, sr.Width + 4, lidY - sr.Y + 2);
            g.Clip = old;
            old.Dispose();
            float dy = lidY - y, half = (float)Math.Sqrt(Math.Max(0, rs * rs - dy * dy));
            using (Pen lp = new Pen(ink, Math.Max(1f, D * 0.012f))) { lp.StartCap = lp.EndCap = LineCap.Round; g.DrawLine(lp, x - half, lidY, x + half, lidY); }
        }

        // Finn's eye: a small black dot, as the show draws it (no shine), a line when it shuts.
        static void FinnDot(Graphics g, float x, float y, float D, float sk, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 34);
            float w = 0.02f * D * sk, h = 0.029f * D * sk * blink;
            if (h < D * 0.009f) { MascotParts.Line(g, ink, Math.Max(1f, D * 0.0085f), x - w * 0.75f, y, x + w * 0.75f, y); return; }
            MascotParts.Ellipse(g, ink, x - w / 2, y - h / 2, w, h);
        }

        // Finn's whole face, every mood: at his size the shared eyes and mouths would crowd it.
        bool FinnFace(Graphics g, float D, MascotParts.Geo geo, float ex, float ey, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 34);
            float gap = geo.EyeGap * D, lw = Math.Max(1f, D * 0.0085f);
            using (Pen p = new Pen(ink, lw))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0 ? -gap : gap) + ex, y = ey;
                    Mood m = mood == Mood.Wink && i == 1 ? Mood.Sleep : mood;
                    switch (m)
                    {
                        case Mood.Happy:
                        case Mood.Wink:
                            g.DrawArc(p, x - 0.019f * D, y - 0.01f * D, 0.038f * D, 0.03f * D, 200, 140);
                            break;
                        case Mood.Sleep:
                        case Mood.Yawn:
                            g.DrawArc(p, x - 0.018f * D, y - 0.016f * D, 0.036f * D, 0.024f * D, 20, 140);
                            break;
                        case Mood.Love:
                            using (GraphicsPath hp = MascotParts.Heart(x, y, D * 0.05f)) MascotParts.FillSolid(g, hp, LoveColor);
                            break;
                        case Mood.Dizzy:
                        {
                            PointF[] pts = new PointF[24];
                            double rot = now / 120.0 * (i == 0 ? 1 : -1);
                            for (int k = 0; k < pts.Length; k++)
                            {
                                double a = rot + k * 0.5, rr = D * (0.002 + k * 0.0011);
                                pts[k] = new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr));
                            }
                            g.DrawCurve(p, pts);
                            break;
                        }
                        default:
                            FinnDot(g, x, y, D, m == Mood.Surprised ? 1.3f : 1f, blink);
                            break;
                    }
                }
                // The mouth.
                float mx = ex * 0.6f, my = geo.FaceY * D + MascotParts.ToonMouthDy(MascotParts.KindFinn) * D;
                Color dark = Color.FromArgb(118, 40, 44);
                bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy;
                if (mood == Mood.Yawn)
                {
                    double o = Math.Sin(Math.Min(1, (moodUntil - now) / 1900.0) * Math.PI);
                    float r = D * (0.012f + 0.016f * (float)o);
                    MascotParts.Ellipse(g, dark, mx - r * 0.8f, my - r * 0.3f, r * 1.6f, r * 2);
                }
                else if (mood == Mood.Surprised || mood == Mood.Dizzy)
                {
                    float r = D * 0.011f;
                    MascotParts.Ellipse(g, dark, mx - r, my - r * 0.2f, r * 2, r * 2.3f);
                }
                else if (talking || mood == Mood.Happy || mood == Mood.Love)
                {
                    // Open: a D with his top teeth along it and the tongue at the bottom.
                    float w = 0.058f * D, h = 0.034f * D;
                    if (talking && mood == Mood.Idle) h *= (float)(0.5 + 0.5 * Math.Abs(Math.Sin(now / 110.0)));
                    using (GraphicsPath m = new GraphicsPath())
                    {
                        m.AddArc(mx - w / 2, my - h * 0.6f, w, h * 1.6f, 0, 180);
                        m.CloseFigure();
                        MascotParts.FillSolid(g, m, dark);
                        Region old = g.Clip;
                        g.SetClip(m, CombineMode.Intersect);
                        MascotParts.Ellipse(g, Color.White, mx - w / 2, my - h * 0.25f, w, h * 0.7f);
                        MascotParts.Ellipse(g, Color.FromArgb(240, 116, 126), mx - w * 0.3f, my + h * 0.42f, w * 0.6f, h * 0.6f);
                        g.Clip = old;
                        old.Dispose();
                        MascotParts.Stroke(g, m, ink, lw);
                    }
                }
                else if (mood == Mood.Sleep) MascotParts.Line(g, ink, lw, mx - 0.012f * D, my + 0.008f * D, mx + 0.012f * D, my + 0.008f * D);
                else g.DrawArc(p, mx - 0.036f * D, my - 0.026f * D, 0.072f * D, 0.042f * D, 25, 130);   // his small smile
            }
            return true;
        }

        // A little black eye, glossy.
        static bool DotEye(Graphics g, float x, float y, float D, float w, float h, float blink)
        {
            Color ink = Color.FromArgb(30, 26, 34);
            if (h * blink < D * 0.012f) { MascotParts.Line(g, ink, Math.Max(1f, D * 0.011f), x - w * 0.7f, y, x + w * 0.7f, y); return true; }
            h *= blink;
            MascotParts.Ellipse(g, ink, x - w / 2, y - h / 2, w, h);
            if (blink > 0.6f) MascotParts.Ellipse(g, Color.FromArgb(235, 255, 255, 255), x - w * 0.02f, y - h * 0.36f, w * 0.42f, w * 0.42f);
            return true;
        }

        // An anime eye at (x, y), w wide and h tall: arched on top, rounder below, the iris dark at the top and light at the
        // bottom, the pupil, two shines, the shadow of the upper lid, the thick upper lash line with its flick at the outer
        // corner and a short lower one; open is how much of it the upper lid leaves showing (1 wide awake, 0.5 half asleep).
        static bool AnimeEye(Graphics g, float x, float y, float D, float w, float h, Color irisTop, Color irisBottom, float open, float blink, int side)
        {
            Color ink = Color.FromArgb(46, 30, 28);
            float o = side, lw = Math.Max(1.3f, D * 0.018f);
            if (blink < 0.35f)
            {
                using (Pen p = new Pen(ink, lw)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawArc(p, x - w / 2, y - h * 0.2f, w, h * 0.4f, 15, 150); }
                return true;
            }
            PointF ci = new PointF(x - o * w * 0.5f, y - h * 0.06f), co = new PointF(x + o * w * 0.5f, y - h * 0.16f);
            PointF t1 = new PointF(x - o * w * 0.46f, y - h * 0.56f), t2 = new PointF(x + o * w * 0.34f, y - h * 0.62f);
            // A lid only a little lowered keeps the eye's arch (a cool, narrowed look); a heavy one is nearly level.
            float shut = Math.Max(1 - open, 1 - blink);
            bool arched = shut <= 0.25f;
            float drop = arched ? shut * h * 0.92f : 0;
            PointF ai = new PointF(ci.X, ci.Y + drop), a1 = new PointF(t1.X, t1.Y + drop), a2 = new PointF(t2.X, t2.Y + drop), ao = new PointF(co.X, co.Y + drop);
            using (GraphicsPath eye = new GraphicsPath())
            {
                eye.AddBezier(ci, t1, t2, co);
                eye.AddBezier(co, new PointF(x + o * w * 0.56f, y + h * 0.44f), new PointF(x - o * w * 0.44f, y + h * 0.6f), ci);
                eye.CloseFigure();
                float top = y - h * 0.46f, lid = arched ? top + drop * 0.6f : top + (y + h * 0.46f - top) * shut;
                Region old = g.Clip;
                g.SetClip(eye, CombineMode.Intersect);
                if (arched)
                    using (GraphicsPath below = new GraphicsPath())
                    {
                        below.AddBezier(ai, a1, a2, ao);
                        below.AddLine(ao, new PointF(ao.X, y + h));
                        below.AddLine(new PointF(ao.X, y + h), new PointF(ai.X, y + h));
                        below.CloseFigure();
                        g.SetClip(below, CombineMode.Intersect);
                    }
                else g.SetClip(new RectangleF(x - w, lid, w * 2, h * 2), CombineMode.Intersect);
                MascotParts.FillSolid(g, eye, Color.FromArgb(253, 253, 251));
                RectangleF ir = new RectangleF(x - w * 0.33f, y - h * 0.36f, w * 0.66f, h * 0.84f);
                using (GraphicsPath ip = new GraphicsPath())
                {
                    ip.AddEllipse(ir);
                    using (LinearGradientBrush b = new LinearGradientBrush(RectangleF.Inflate(ir, 0, 1), irisTop, irisBottom, 90f)) g.FillPath(b, ip);
                    using (Pen rp = new Pen(MascotParts.Darker(irisTop, 0.3), Math.Max(0.8f, D * 0.006f))) g.DrawPath(rp, ip);
                }
                MascotParts.Ellipse(g, MascotParts.Darker(irisTop, 0.45), x - w * 0.14f, y - h * 0.1f, w * 0.28f, h * 0.36f);
                // The lid's shadow over the top of the eye.
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(45, 60, 40, 60))) g.FillRectangle(sh, x - w, lid, w * 2, h * 0.14f);
                MascotParts.Ellipse(g, Color.FromArgb(250, 255, 255, 255), x - w * 0.26f, y - h * 0.3f, w * 0.3f, h * 0.24f);
                MascotParts.Ellipse(g, Color.FromArgb(220, 255, 255, 255), x + w * 0.06f, y + h * 0.16f, w * 0.13f, w * 0.13f);
                g.Clip = old;
                old.Dispose();
                using (Pen p = new Pen(ink, lw))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    PointF outer;
                    if (arched)
                    {
                        using (GraphicsPath topLine = new GraphicsPath())
                        {
                            topLine.AddBezier(ai, a1, a2, ao);
                            g.DrawPath(p, topLine);
                        }
                        outer = ao;
                    }
                    else
                    {
                        // A heavy lid: a nearly level line, its outer end a little higher.
                        outer = new PointF(x + o * w * 0.52f, lid - h * 0.015f);
                        g.DrawLine(p, x - o * w * 0.52f, lid + h * 0.015f, outer.X, outer.Y);
                    }
                    g.DrawLine(p, outer.X, outer.Y, outer.X + o * w * 0.16f, outer.Y - h * 0.1f);
                }
                using (Pen p = new Pen(Color.FromArgb(160, ink), Math.Max(0.8f, D * 0.007f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawBezier(p, new PointF(x + o * w * 0.08f, y + h * 0.44f), new PointF(x + o * w * 0.3f, y + h * 0.42f), new PointF(x + o * w * 0.45f, y + h * 0.3f), new PointF(x + o * w * 0.5f, y + h * 0.16f));
                }
            }
            return true;
        }

        bool ToonMouth(Graphics g, float D, float mx, float fy, bool talking)
        {
            int k = Look.Kind;
            if (!MascotParts.IsToon(k)) return false;
            if (k == MascotParts.KindKakashiSd) return true;
            float my = fy + MascotParts.ToonMouthDy(k) * D;
            bool open = talking || mood == Mood.Happy || mood == Mood.Love;
            bool adv = k <= MascotParts.KindBmoAt;
            Color ink = adv ? Color.FromArgb(30, 26, 34) : Color.FromArgb(46, 30, 28);
            float w = (k == MascotParts.KindSasukeSd ? 0.045f : k == MascotParts.KindPakkun || k == MascotParts.KindJake ? 0.065f : k == MascotParts.KindBmoAt ? 0.07f : 0.09f) * D;
            if (MascotParts.IsPersona(k) && PersonaMouth(g, D, mx, my, ink, open)) return true;
            if (MascotParts.IsSoftFig(k) && PkMouth(g, D, mx, my, open, talking)) return true;
            if (!open)
            {
                if (k == MascotParts.KindLuffySd || k == MascotParts.KindKaminaSd)
                {
                    // The big toothy grin.
                    float gw = 0.13f * D, gh = 0.055f * D;
                    using (GraphicsPath m = new GraphicsPath())
                    {
                        m.AddArc(mx - gw / 2, my - gh * 0.8f, gw, gh * 1.8f, 0, 180);
                        m.CloseFigure();
                        MascotParts.FillSolid(g, m, Color.White);
                        using (Pen tp = new Pen(Color.FromArgb(150, 46, 30, 28), Math.Max(0.8f, D * 0.006f))) g.DrawLine(tp, mx - gw * 0.45f, my + gh * 0.45f, mx + gw * 0.45f, my + gh * 0.45f);
                        MascotParts.Stroke(g, m, ink, Math.Max(1f, D * 0.012f));
                    }
                    return true;
                }
                if (k == MascotParts.KindJake || k == MascotParts.KindPakkun) return true;   // their muzzles carry the mouth
                using (Pen p = new Pen(ink, Math.Max(1.1f, D * 0.012f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    if (k == MascotParts.KindSasukeSd) g.DrawLine(p, mx - w / 2, my, mx + w / 2, my + D * 0.003f);
                    else if (k == MascotParts.KindNarutoSd || k == MascotParts.KindGonSd) g.DrawArc(p, mx - 0.06f * D, my - 0.045f * D, 0.12f * D, 0.07f * D, 18, 144);   // a wide, sure grin
                    else if (k == MascotParts.KindKilluaSd)
                    {
                        // His cat's mouth.
                        g.DrawArc(p, mx - 0.03f * D, my - 0.014f * D, 0.03f * D, 0.024f * D, 10, 160);
                        g.DrawArc(p, mx, my - 0.014f * D, 0.03f * D, 0.024f * D, 10, 160);
                    }
                    else if (k == MascotParts.KindHisokaSd) g.DrawBezier(p, mx - 0.045f * D, my + 0.004f * D, mx - 0.01f * D, my + 0.018f * D, mx + 0.03f * D, my + 0.006f * D, mx + 0.05f * D, my - 0.016f * D);   // his smirk
                    else if (k == MascotParts.KindZoroSd) g.DrawArc(p, mx - 0.03f * D, my - 0.004f * D, 0.06f * D, 0.03f * D, 205, 130);   // his frown
                    else g.DrawArc(p, mx - w / 2, my - D * 0.03f, w, D * 0.05f, 25, 130);
                }
                return true;
            }
            // Open: a wide D with a row of teeth along the top and the tongue at the bottom.
            float h = (k == MascotParts.KindSasukeSd ? 0.032f : k == MascotParts.KindJake || k == MascotParts.KindPakkun ? 0.04f : 0.055f) * D;
            if (k == MascotParts.KindJake || k == MascotParts.KindPakkun) my += 0.012f * D;
            using (GraphicsPath m = new GraphicsPath())
            {
                m.AddArc(mx - w / 2, my - h * 0.6f, w, h * 1.6f, 0, 180);
                m.CloseFigure();
                MascotParts.FillSolid(g, m, Color.FromArgb(118, 40, 44));
                Region old = g.Clip;
                g.SetClip(m, CombineMode.Intersect);
                MascotParts.Ellipse(g, Color.FromArgb(240, 116, 126), mx - w * 0.3f, my + h * 0.38f, w * 0.6f, h * 0.6f);
                // The top teeth along the mouth's straight top edge (it runs at my + h * 0.2).
                if (k != MascotParts.KindJake && k != MascotParts.KindPakkun && k != MascotParts.KindBmoAt) MascotParts.Ellipse(g, Color.White, mx - w / 2, my - h * 0.25f, w, h * 0.7f);
                g.Clip = old;
                old.Dispose();
                MascotParts.Stroke(g, m, ink, Math.Max(1f, D * 0.011f));
            }
            return true;
        }
    }
}
