// Stackshot - Five more tributes, round and soft like the rest: Stitch (big ears that twitch, a light chest and eye
// patches, a big nose; goes wild with two extra arms), Kirby (a pink ball on red feet; inhales with a swirl and puffs
// up), Grogu (long ears out to the sides, a little robe; lifts a stone with the Force and gets tired), Eevee (big
// pointed ears, a cream ruff and a fluffy tail; rolls with stars) and Charmander (cream belly, a tail whose flame never
// goes out; a small burst of flame). Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindStitch = 19, KindKirby = 20, KindGrogu = 21, KindEevee = 22, KindCharmander = 23, KindPikachu = 24;
        public const int ColorIndigo = 26, ColorSage = 27, ColorElectric = 28;
        public const int PowerCrazy = 7, PowerInhale = 8, PowerForce = 9, PowerRoll = 10, PowerFlame = 11, PowerThunder = 12;

        public static bool IsTribute(int kind) { return kind >= KindStitch && kind <= KindPikachu; }
        public static bool HasPower(int kind) { return IsDragon(kind) || IsTribute(kind) || ArcadeHasPower(kind); }

        static Color TributeIris(int kind)
        {
            switch (kind)
            {
                case KindStitch: return Color.FromArgb(32, 34, 60);
                case KindKirby: return Color.FromArgb(40, 64, 158);
                case KindGrogu: return Color.FromArgb(70, 46, 30);
                case KindEevee: return Color.FromArgb(110, 64, 30);
                case KindCharmander: return Color.FromArgb(48, 118, 176);
                case KindPikachu: return Color.FromArgb(22, 20, 24);
                default: return Color.FromArgb(136, 214, 76);
            }
        }

        // How each character's eyes are (sizes in units of D): its iris colour, or all dark; the pupil's share of the
        // eye; whether a dragon's pupil narrows to a slit; and an eyelid line where the original has one.
        public struct EyeSpec
        {
            public float W, H, Pupil;
            public Color Iris, LidColor;
            public bool Dark, Slit, Lid;
        }

        public static EyeSpec EyeFor(int kind)
        {
            EyeSpec e = new EyeSpec { W = 0.125f, H = 0.14f, Pupil = 0.56f, Iris = DragonIris(kind), Slit = IsDragon(kind) };
            switch (kind)
            {
                case KindPikachu: e.W = 0.1f; e.H = 0.118f; e.Dark = true; break;
                case KindStitch: e.W = 0.13f; e.H = 0.152f; e.Dark = true; break;
                case KindGrogu: e.W = 0.138f; e.H = 0.136f; e.Pupil = 0.7f; e.Lid = true; e.LidColor = Color.FromArgb(78, 58, 40); break;
                case KindEevee: e.W = 0.115f; e.H = 0.14f; e.Pupil = 0.58f; e.Lid = true; e.LidColor = Color.FromArgb(62, 36, 22); break;
                case KindCharmander: e.W = 0.115f; e.H = 0.138f; e.Pupil = 0.5f; break;
                case KindGronckle: e.W = 0.112f; e.H = 0.122f; e.Pupil = 0.5f; break;
                case KindBulbasaur: e.W = 0.115f; e.H = 0.13f; e.Pupil = 0.48f; e.Iris = Color.FromArgb(204, 44, 60); break;
                case KindSquirtle: e.W = 0.118f; e.H = 0.14f; e.Pupil = 0.52f; e.Iris = Color.FromArgb(150, 52, 44); break;
                case KindJigglypuff: e.W = 0.15f; e.H = 0.16f; e.Pupil = 0.46f; e.Iris = Color.FromArgb(40, 150, 180); break;
                case KindGengar: e.W = 0.13f; e.H = 0.1f; e.Pupil = 0.36f; e.Iris = Color.FromArgb(236, 50, 70); e.Lid = true; e.LidColor = Color.FromArgb(52, 24, 74); break;
                case KindMew: e.W = 0.135f; e.H = 0.155f; e.Pupil = 0.54f; e.Iris = Color.FromArgb(52, 120, 214); break;
                case KindNightFury:
                case KindLightFury: e.W = 0.13f; e.H = 0.146f; e.Pupil = 0.6f; break;
            }
            if (IsExtra(kind)) ExtraEyeSpec(kind, ref e);
            // Pokémon eyes the way their official plush and chibi art draws them: nearly all pupil, a thin ring of colour,
            // big catchlights. A thin iris with a small pupil made them stare.
            if (IsPokemonLike(kind) && !e.Dark) e.Pupil = Math.Max(e.Pupil, 0.76f);
            return e;
        }

        static string TributePowerName(int kind)
        {
            if (ArcadeHasPower(kind)) return ArcadePowerName(kind);
            switch (kind)
            {
                case KindStitch: return I18n.T("Hazte el loco", "Go wild");
                case KindKirby: return I18n.T("Inhala", "Inhale");
                case KindGrogu: return I18n.T("Usa la Fuerza", "Use the Force");
                case KindEevee: return I18n.T("Rueda de alegr\u00EDa", "Joy roll");
                case KindPikachu: return I18n.T("Impactrueno", "Thunder Shock");
                default: return I18n.T("Echa una llamarada", "Breathe fire");
            }
        }

        static string TributePowerHint(int kind)
        {
            if (ArcadeHasPower(kind)) return ArcadePowerHint(kind);
            switch (kind)
            {
                case KindStitch: return I18n.T("Gira, saca dos brazos m\u00E1s y suelta chispas", "Spins, pops out two extra arms and throws sparks");
                case KindKirby: return I18n.T("Aspira con un remolino y se hincha", "Sucks in with a whirlwind and puffs up");
                case KindGrogu: return I18n.T("Levanta una piedra sin tocarla", "Lifts a rock without touching it");
                case KindEevee: return I18n.T("Una voltereta llena de estrellas", "A somersault full of stars");
                case KindPikachu: return I18n.T("Se carga los mofletes y suelta rayos", "Charges its cheeks and lets out lightning");
                default: return I18n.T("Una peque\u00F1a llamarada, y la cola arde m\u00E1s", "A little burst of flame, and the tail burns brighter");
            }
        }

        static string TributePowerLine(int power)
        {
            if (power >= PowerChomp) return ArcadePowerLine(power);
            switch (power)
            {
                case PowerCrazy: return I18n.T("\u00A1Ohana!", "Ohana!");
                case PowerInhale: return I18n.T("\u00A1Poyo!", "Poyo!");
                case PowerForce: return "Mmm\u2026";
                case PowerRoll: return I18n.T("\u00A1Eevee!", "Eevee!");
                case PowerThunder: return I18n.T("\u00A1Pika pika!", "Pika pika!");
                default: return I18n.T("\u00A1Char!", "Char!");
            }
        }

        // ---- Behind the body.
        static void TributeBack(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            switch (l.Kind)
            {
                case KindStitch:
                    if (p.Fx > 0.01) StitchArms(g, D, c1, c2, p.Fx, p.Now);
                    foreach (int s in Sides)
                        SoftEar(g, D, s * 0.23f * D, -0.2f * D, (float)(s * (70 + (s < 0 ? p.EarL : p.EarR) * 1.2)), 0.32f, 0.62f,
                                Lighter(c1, 0.05), c2, Color.FromArgb(220, 236, 150, 196));
                    break;
                case KindKirby:
                    // Red oval feet tucked under the ball, toes turned a little out; they step in turn while it walks.
                    foreach (int s in Sides)
                    {
                        float step = Math.Abs(p.Facing) > 0.2 ? (float)Math.Max(0, Math.Sin(p.Now / 95.0 + (s < 0 ? 0 : Math.PI))) * 0.03f * D : 0;
                        GraphicsState fs = g.Save();
                        g.TranslateTransform(s * 0.15f * D, 0.29f * D - step);
                        g.RotateTransform(s * 14);
                        RectangleF f = new RectangleF(-0.105f * D, -0.06f * D, 0.21f * D, 0.12f * D);
                        using (GraphicsPath fp = new GraphicsPath())
                        {
                            fp.AddEllipse(f);
                            Fill(g, fp, f, Color.FromArgb(246, 70, 92), Color.FromArgb(184, 22, 52), 90f);
                            Stroke(g, fp, Color.FromArgb(200, 120, 10, 34), D * 0.011f);
                        }
                        Ellipse(g, Color.FromArgb(90, 255, 255, 255), f.X + f.Width * 0.2f, f.Y + f.Height * 0.15f, f.Width * 0.32f, f.Height * 0.28f);
                        g.Restore(fs);
                    }
                    break;
                case KindGrogu:
                    // Long ears reaching out to the sides and a little up, pink inside.
                    foreach (int s in Sides)
                        SoftEar(g, D, s * 0.26f * D, -0.12f * D, (float)(s * (80 + (s < 0 ? p.EarL : p.EarR) * 0.5)), 0.44f, 0.46f,
                                Lighter(c1, 0.06), c2, Color.FromArgb(215, 236, 176, 172));
                    break;
                case KindEevee:
                    { GraphicsState ts = TailSide(g, p); EeveeTail(g, D, c1, c2, p.Now, p.Fx); g.Restore(ts); }
                    // Big ears opening out to the sides, dark brown inside.
                    foreach (int s in Sides)
                        SoftEar(g, D, s * 0.19f * D, -0.24f * D, (float)(s * (36 + (s < 0 ? p.EarL : p.EarR))), 0.34f, 0.6f,
                                Lighter(c1, 0.05), c2, Color.FromArgb(225, 92, 58, 34));
                    break;
                case KindCharmander:
                    { GraphicsState ts = TailSide(g, p); CharTail(g, D, c1, c2, p.Now, p.Fx); g.Restore(ts); }
                    foreach (int s in Sides) Foot(g, D, s, Lighter(c1, 0.05), c2, p);
                    break;
                case KindPikachu:
                    if (p.Fx > 0.01) Glow(g, 0, -0.02f * D, 1.3f * D, 1.2f * D, Color.FromArgb(255, 226, 70), (int)(130 * p.Fx));
                    { GraphicsState ts = TailSide(g, p); PikaTail(g, D, c1, c2, p.Now); g.Restore(ts); }
                    foreach (int s in Sides) PikaEar(g, D, s, c1, c2, s < 0 ? p.EarL : p.EarR);
                    foreach (int s in Sides) Foot(g, D, s, Lighter(c1, 0.05), c2, p);
                    break;
            }
        }

        static readonly Color PikaBlack = Color.FromArgb(34, 30, 32), PikaBrown = Color.FromArgb(150, 92, 40);

        // Pikachu's ears: long, pointed, standing up and a little out, black at the tip.
        static void PikaEar(Graphics g, float D, int s, Color c1, Color c2, double twitch)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.15f * D, -0.24f * D);
            g.RotateTransform((float)(s * (20 + twitch)));
            float L = 0.4f * D, w = 0.15f * D;
            using (GraphicsPath e = new GraphicsPath())
            {
                // Wide at the base, tapering to a soft point.
                e.AddBezier(P(-w * 0.5f, 0.02f * D), P(-w * 0.62f, -L * 0.5f), P(-w * 0.22f, -L * 0.9f), P(0, -L));
                e.AddBezier(P(0, -L), P(w * 0.22f, -L * 0.9f), P(w * 0.62f, -L * 0.5f), P(w * 0.5f, 0.02f * D));
                e.CloseFigure();
                Fill(g, e, new RectangleF(-w, -L, w * 2, L + 0.03f * D), Lighter(c1, 0.08), c2, 80f);
                Region old = g.Clip;
                g.SetClip(e, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(PikaBlack)) g.FillRectangle(b, -w, -L - 1, w * 2, L * 0.3f + 1);
                g.Clip = old;
                old.Dispose();
                Stroke(g, e, Color.FromArgb(190, Darker(c2, 0.5)), D * 0.011f);
            }
            g.Restore(st);
        }

        // The lightning-bolt tail behind it on the right, brown where it meets the body.
        static void PikaTail(Graphics g, float D, Color c1, Color c2, double now)
        {
            GraphicsState st = g.Save();
            // A gentle sway around where it leaves the body.
            g.TranslateTransform(0.2f * D, 0.24f * D);
            g.RotateTransform((float)(Math.Sin(now / 640.0) * 4));
            g.TranslateTransform(-0.2f * D, -0.24f * D);
            PointF[] bolt =
            {
                P(0.17f * D, 0.21f * D), P(0.28f * D, 0.11f * D), P(0.24f * D, 0.05f * D), P(0.37f * D, -0.04f * D), P(0.32f * D, -0.1f * D), P(0.48f * D, -0.27f * D),
                P(0.61f * D, -0.12f * D), P(0.44f * D, 0.0f), P(0.48f * D, 0.06f * D), P(0.35f * D, 0.14f * D), P(0.39f * D, 0.19f * D), P(0.23f * D, 0.28f * D)
            };
            using (GraphicsPath t = new GraphicsPath())
            {
                t.AddPolygon(bolt);
                RectangleF r = new RectangleF(0.16f * D, -0.28f * D, 0.46f * D, 0.57f * D);
                Fill(g, t, r, Lighter(c1, 0.1), c2, 60f);
                Region old = g.Clip;
                g.SetClip(t, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(PikaBrown)) g.FillRectangle(b, 0.14f * D, 0.17f * D, 0.3f * D, 0.14f * D);
                g.Clip = old;
                old.Dispose();
                Stroke(g, t, Color.FromArgb(90, Darker(c2, 0.3)), D * 0.008f);
            }
            g.Restore(st);
        }

        // Tails trail behind the walk: on the left while it walks right (mirrored), on the right otherwise.
        static GraphicsState TailSide(Graphics g, MascotPose p)
        {
            GraphicsState st = g.Save();
            if (p.TailLeft) g.ScaleTransform(-1, 1);
            return st;
        }

        // A small round foot peeking out under the body; while it walks, the feet step in turn.
        static void Foot(Graphics g, float D, int s, Color a, Color b, MascotPose p)
        {
            float step = Math.Abs(p.Facing) > 0.2 ? (float)Math.Max(0, Math.Sin(p.Now / 95.0 + (s < 0 ? 0 : Math.PI))) * 0.03f * D : 0;
            RectangleF f = new RectangleF(s * 0.15f * D - 0.085f * D, 0.255f * D - step, 0.17f * D, 0.095f * D);
            using (GraphicsPath fp = new GraphicsPath())
            {
                fp.AddEllipse(f);
                Fill(g, fp, f, a, b, 90f);
                Stroke(g, fp, Color.FromArgb(190, Darker(b, 0.5)), D * 0.011f);
            }
        }

        // A soft ear: a leaf with a round end and a lighter (or darker) inside, rising from its base at x, y.
        static void SoftEar(Graphics g, float D, float x, float y, float rot, float len, float width, Color a, Color b, Color inner)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rot);
            float L = len * D, w = len * width * D;
            using (GraphicsPath e = Leaf(w, L, D))
            {
                Fill(g, e, new RectangleF(-w, -L, w * 2, L + 0.03f * D), a, b, 80f);
                Stroke(g, e, Color.FromArgb(b.A == 0 ? 0 : 190, Darker(b, 0.5)), D * 0.011f);
            }
            g.TranslateTransform(0, -L * 0.12f);
            using (GraphicsPath i = Leaf(w * 0.55f, L * 0.68f, D)) FillSolid(g, i, inner);
            g.Restore(st);
        }

        static GraphicsPath Leaf(float w, float L, float D)
        {
            GraphicsPath e = new GraphicsPath();
            e.AddBezier(P(-w * 0.5f, 0.02f * D), P(-w * 0.75f, -L * 0.55f), P(-w * 0.42f, -L), P(0, -L));
            e.AddBezier(P(0, -L), P(w * 0.42f, -L), P(w * 0.75f, -L * 0.55f), P(w * 0.5f, 0.02f * D));
            e.CloseFigure();
            return e;
        }

        // Stitch gone wild: two extra little arms waving at his sides.
        static void StitchArms(Graphics g, float D, Color c1, Color c2, double k, double now)
        {
            foreach (int s in Sides)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.34f * D, 0.02f * D);
                g.RotateTransform((float)(s * (40 + Math.Sin(now / 70.0 + s) * 30)));
                RectangleF arm = new RectangleF(-0.04f * D, -0.13f * D * (float)k, 0.08f * D, 0.13f * D * (float)k);
                using (GraphicsPath ap = new GraphicsPath())
                {
                    ap.AddEllipse(arm);
                    Fill(g, ap, arm, Mac.Mix(c1, c2, 0.4), c2, 70f);
                }
                g.Restore(st);
            }
        }

        // A big fluffy tail behind on the right with a cream tip; it wags faster while rolling.
        static void EeveeTail(Graphics g, float D, Color c1, Color c2, double now, double fx)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0.24f * D, 0.16f * D);
            g.RotateTransform((float)(-46 + Math.Sin(now / (fx > 0 ? 120.0 : 520.0)) * (fx > 0 ? 16 : 8)));
            RectangleF t = new RectangleF(0, -0.12f * D, 0.38f * D, 0.24f * D);
            using (GraphicsPath tp = new GraphicsPath())
            {
                tp.AddEllipse(t);
                Fill(g, tp, t, Lighter(c1, 0.08), c2, 0f);
                Region old = g.Clip;
                g.SetClip(tp, CombineMode.Intersect);
                Ellipse(g, Color.FromArgb(250, 246, 232, 206), 0.25f * D, -0.12f * D, 0.2f * D, 0.24f * D);
                g.Clip = old;
                old.Dispose();
            }
            g.Restore(st);
        }

        // Charmander's tail with the flame at its tip, always flickering; bigger while it breathes fire.
        static void CharTail(Graphics g, float D, Color c1, Color c2, double now, double fx)
        {
            float angle;
            PointF tip = TaperTail(g, D, Lighter(c1, 0.05), c2, now, 0.1f, 0.05f, out angle);
            float flick = (float)(1 + 0.08 * Math.Sin(now / 60.0) + 0.05 * Math.Sin(now / 23.0));
            float size = 0.1f * D * flick * (float)(1 + 0.7 * fx);
            Glow(g, tip.X, tip.Y - size * 0.3f, size * 3.2f, size * 3.2f, Color.FromArgb(255, 140, 40), (int)(150 + 60 * fx));
            Flame(g, tip.X, tip.Y, size);
        }

        // A teardrop of flame standing on (x, y): orange outside, yellow inside.
        static void Flame(Graphics g, float x, float y, float size)
        {
            using (GraphicsPath f = Drop(x, y, size))
                Fill(g, f, new RectangleF(x - size * 0.6f, y - size * 1.6f, size * 1.2f, size * 1.7f), Color.FromArgb(255, 96, 24), Color.FromArgb(255, 186, 40), 90f);
            using (GraphicsPath f = Drop(x, y - size * 0.05f, size * 0.55f))
                Fill(g, f, new RectangleF(x - size * 0.4f, y - size, size * 0.8f, size), Color.FromArgb(255, 236, 120), Color.FromArgb(255, 255, 214), 90f);
        }

        static GraphicsPath Drop(float x, float y, float s)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddBezier(P(x, y - s * 1.5f), P(x + s * 0.2f, y - s * 0.9f), P(x + s * 0.62f, y - s * 0.6f), P(x + s * 0.5f, y - s * 0.12f));
            p.AddBezier(P(x + s * 0.5f, y - s * 0.12f), P(x + s * 0.4f, y + s * 0.18f), P(x - s * 0.4f, y + s * 0.18f), P(x - s * 0.5f, y - s * 0.12f));
            p.AddBezier(P(x - s * 0.5f, y - s * 0.12f), P(x - s * 0.62f, y - s * 0.6f), P(x - s * 0.2f, y - s * 0.9f), P(x, y - s * 1.5f));
            p.CloseFigure();
            return p;
        }

        // ---- On the body: Stitch's chest and eye patches, Grogu's robe, Charmander's belly.
        static void TributeBody(Graphics g, MascotLook l, float D, GraphicsPath body, Color c1, Color c2)
        {
            Geo geo = GeoFor(l.Kind);
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            switch (l.Kind)
            {
                case KindStitch:
                {
                    Color light = Lighter(c1, 0.42);
                    Ellipse(g, Color.FromArgb(235, light), -0.16f * D, 0.08f * D, 0.32f * D, 0.26f * D);
                    float fy = geo.FaceY * D, gap = geo.EyeGap * D;
                    foreach (int s in Sides) Ellipse(g, Color.FromArgb(150, Lighter(c1, 0.3)), s * gap - 0.1f * D, fy - 0.1f * D, 0.2f * D, 0.19f * D);
                    break;
                }
                case KindGrogu:
                {
                    float top = geo.NeckY * D - 0.02f * D;
                    RectangleF robe = new RectangleF(-0.5f * D, top, D, 0.5f * D);
                    using (GraphicsPath rp = new GraphicsPath())
                    {
                        rp.AddRectangle(robe);
                        Fill(g, rp, robe, Color.FromArgb(214, 188, 148), Color.FromArgb(160, 128, 92), 90f);
                    }
                    // The collar folding into a V.
                    Line(g, Color.FromArgb(120, 112, 84, 56), D * 0.014f, -0.17f * D, top + 0.01f * D, 0, top + 0.1f * D);
                    Line(g, Color.FromArgb(120, 112, 84, 56), D * 0.014f, 0.17f * D, top + 0.01f * D, 0, top + 0.1f * D);
                    Line(g, Color.FromArgb(60, 112, 84, 56), D * 0.01f, -0.12f * D, top + 0.14f * D, -0.14f * D, top + 0.24f * D);
                    Line(g, Color.FromArgb(60, 112, 84, 56), D * 0.01f, 0.12f * D, top + 0.14f * D, 0.14f * D, top + 0.24f * D);
                    break;
                }
                case KindCharmander:
                {
                    // A cream belly rising in an oval from under the chin, a touch darker at the bottom.
                    RectangleF belly = new RectangleF(-0.17f * D, 0.06f * D, 0.34f * D, 0.31f * D);
                    using (GraphicsPath bp = new GraphicsPath())
                    {
                        bp.AddEllipse(belly);
                        Fill(g, bp, belly, Color.FromArgb(255, 240, 182), Color.FromArgb(246, 204, 122), 90f);
                    }
                    break;
                }
            }
            g.Clip = old;
            old.Dispose();
        }

        // ---- In front of the face: Stitch's big nose, Eevee's ruff and nose, Charmander's nostrils.
        static void TributeDetails(Graphics g, MascotLook l, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            switch (l.Kind)
            {
                case KindStitch:
                {
                    RectangleF n = new RectangleF(-0.04f * D, fy + 0.035f * D, 0.08f * D, 0.05f * D);
                    using (GraphicsPath np = new GraphicsPath())
                    {
                        np.AddEllipse(n);
                        Fill(g, np, n, Color.FromArgb(70, 60, 110), Color.FromArgb(30, 26, 56), 90f);
                    }
                    Ellipse(g, Color.FromArgb(150, 255, 255, 255), n.X + n.Width * 0.22f, n.Y + n.Height * 0.15f, n.Width * 0.25f, n.Height * 0.3f);
                    break;
                }
                case KindEevee:
                {
                    // The fluffy mane on its chest, in one piece: a soft curve on top following the body, and below a fringe
                    // of many small rounded tufts, longest in the middle, the outer ones sweeping out past the sides. A soft
                    // shadow under it sets it on the body instead of floating over it.
                    Color cream1 = Color.FromArgb(253, 247, 230), cream2 = Color.FromArgb(232, 212, 176);
                    Glow(g, 0, 0.25f * D, 0.82f * D, 0.26f * D, Color.FromArgb(90, 58, 30), 70);
                    using (GraphicsPath mane = new GraphicsPath())
                    {
                        mane.AddBezier(P(-0.37f * D, 0.11f * D), P(-0.2f * D, 0.2f * D), P(0.2f * D, 0.2f * D), P(0.37f * D, 0.11f * D));
                        const int tufts = 9;
                        PointF[] fringe = new PointF[tufts * 2 - 1];
                        for (int k = 0; k < tufts; k++)
                        {
                            float x = 0.38f - k * (0.76f / (tufts - 1));
                            float tip = 0.22f + 0.12f * (1 - (x / 0.42f) * (x / 0.42f));
                            float out1 = Math.Abs(x) > 0.3f ? Math.Sign(x) * 0.04f : 0;
                            fringe[k * 2] = P((x + out1) * D, tip * D);
                            if (k < tufts - 1)
                            {
                                float xv = x - 0.38f / (tufts - 1), yv = 0.22f + 0.12f * (1 - (xv / 0.42f) * (xv / 0.42f)) - 0.045f;
                                fringe[k * 2 + 1] = P(xv * D, yv * D);
                            }
                        }
                        // The fringe runs right to left from the top curve's end back to its start.
                        PointF[] run = new PointF[tufts * 2 + 1];
                        run[0] = P(0.37f * D, 0.11f * D);
                        for (int k = 0; k < tufts * 2 - 1; k++) run[k + 1] = fringe[k];
                        run[tufts * 2] = P(-0.37f * D, 0.11f * D);
                        mane.AddCurve(run, 0.55f);
                        mane.CloseFigure();
                        RectangleF mb = new RectangleF(-0.44f * D, 0.11f * D, 0.88f * D, 0.24f * D);
                        Fill(g, mane, mb, cream1, cream2, 90f);
                        Stroke(g, mane, Color.FromArgb(46, 150, 118, 80), D * 0.006f);
                    }
                    // Short strands along the tufts so it reads as fur.
                    Color strand = Color.FromArgb(60, 176, 146, 104);
                    for (int k = 0; k < 5; k++)
                    {
                        float x = (-0.24f + k * 0.12f) * D, y0 = 0.21f * D, y1 = (0.27f + 0.03f * (1 - Math.Abs(k - 2) / 2f)) * D;
                        Line(g, strand, D * 0.006f, x, y0, x + (k - 2) * 0.012f * D, y1);
                    }
                    Ellipse(g, Color.FromArgb(200, 70, 44, 30), -0.016f * D, fy + 0.055f * D, 0.032f * D, 0.022f * D);
                    break;
                }
                case KindCharmander:
                    Ellipse(g, Color.FromArgb(110, Ink), -0.035f * D, fy + 0.06f * D, 0.014f * D, 0.011f * D);
                    Ellipse(g, Color.FromArgb(110, Ink), 0.021f * D, fy + 0.06f * D, 0.014f * D, 0.011f * D);
                    break;
                case KindKirby:
                {
                    // Its pink blush: two soft ovals under and just outside the eyes.
                    float gap = geo.EyeGap * D;
                    foreach (int s in Sides)
                    {
                        float cx = s * (gap + 0.085f * D), cy = fy + 0.085f * D;
                        Glow(g, cx, cy, 0.12f * D, 0.065f * D, Color.FromArgb(250, 100, 140), 120);
                        Ellipse(g, Color.FromArgb(150, 246, 104, 146), cx - 0.04f * D, cy - 0.02f * D, 0.08f * D, 0.04f * D);
                    }
                    break;
                }
                case KindPikachu:
                {
                    // Round red cheeks out on the sides of the face, and a tiny nose.
                    float gap = geo.EyeGap * D, r = 0.058f * D;
                    foreach (int s in Sides)
                    {
                        float cx = s * (gap + 0.11f * D), cy = fy + 0.075f * D;
                        RectangleF ch = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                        using (GraphicsPath cp = new GraphicsPath())
                        {
                            cp.AddEllipse(ch);
                            Fill(g, cp, ch, Color.FromArgb(246, 88, 72), Color.FromArgb(214, 44, 40), 90f);
                        }
                        Ellipse(g, Color.FromArgb(110, 255, 255, 255), cx - r * 0.5f, cy - r * 0.6f, r * 0.55f, r * 0.4f);
                    }
                    Ellipse(g, Color.FromArgb(220, PikaBlack), -0.012f * D, fy + 0.05f * D, 0.024f * D, 0.014f * D);
                    break;
                }
            }
        }
    }

    // The tributes' tricks, on the mascot itself.
    public partial class Mascot
    {
        int TributePower()
        {
            if (MascotParts.ArcadeHasPower(Look.Kind)) return ArcadePower();
            switch (Look.Kind)
            {
                case MascotParts.KindStitch: return MascotParts.PowerCrazy;
                case MascotParts.KindKirby: return MascotParts.PowerInhale;
                case MascotParts.KindGrogu: return MascotParts.PowerForce;
                case MascotParts.KindEevee: return MascotParts.PowerRoll;
                case MascotParts.KindCharmander: return MascotParts.PowerFlame;
                case MascotParts.KindPikachu: return MascotParts.PowerThunder;
                default: return 0;
            }
        }

        void TributeStep(double t, double my)
        {
            double dir = Facing < -0.2 ? -1 : 1;
            switch (power)
            {
                case MascotParts.PowerCrazy:
                    // A wild spin with extra arms and sparks.
                    if (powerStage == 0) { powerStage = 1; spinV += 900; jumpV -= 1.6; SetMood(Mood.Happy, 1800); }
                    if (t < 1500 && now - lastEmit > 90) { lastEmit = now; Sparkles(0, -0.05, 3); }
                    if (t > 1900) powerStart = -1;
                    break;
                case MascotParts.PowerInhale:
                    // A swirl of air into its open mouth, then it puffs up.
                    if (powerStage == 0) { powerStage = 1; SetMood(Mood.Surprised, 1300); }
                    if (t < 1200 && now - lastEmit > 70)
                    {
                        lastEmit = now;
                        Emit('w', 0.8 * dir, my - 0.08 + (rnd.NextDouble() - 0.5) * 0.32, -1.15 * dir, (rnd.NextDouble() - 0.5) * 0.1, 640, 0.06 + rnd.NextDouble() * 0.03);
                    }
                    if (powerStage == 1 && t >= 1250)
                    {
                        powerStage = 2;
                        squashX = 1.32;
                        squashY = 1.22;
                        jumpV -= 0.8;
                        SetMood(Mood.Happy, 1200);
                    }
                    if (t > 2400 && powerStage >= 2) powerStart = -1;
                    break;
                case MascotParts.PowerForce:
                    // A hand up, eyes closed in focus, a stone rising beside it; then a yawn, it was tiring.
                    if (powerStage == 0)
                    {
                        powerStage = 1;
                        waveUntil = now + 2000;
                        SetMood(Mood.Happy, 1900);
                        Emit('k', 0.42 * dir, 0.34, 0, -0.18, 2000, 0.075);
                    }
                    else if (powerStage == 1 && t >= 2050) { powerStage = 2; SetMood(Mood.Yawn, 1500); }
                    if (t > 3600 && powerStage >= 2) powerStart = -1;
                    break;
                case MascotParts.PowerRoll:
                    // A happy roll with a burst of stars.
                    if (powerStage == 0)
                    {
                        powerStage = 1;
                        spinV -= 1250;
                        jumpV -= 2.0;
                        SetMood(Mood.Happy, 1600);
                        for (int i = 0; i < 8; i++) Spawn('s', 0, -0.1, 0.7);
                    }
                    if (t > 1600) powerStart = -1;
                    break;
                case MascotParts.PowerThunder:
                    // Cheeks charged, then bolts crackling up and out all around.
                    if (powerStage == 0) { powerStage = 1; SetMood(Mood.Happy, 1400); squashX = 0.9; squashY = 1.12; jumpV -= 1.2; }
                    if (t >= 180 && t < 950 && now - lastEmit > 70)
                    {
                        lastEmit = now;
                        double a = (-160 + rnd.NextDouble() * 140) * Math.PI / 180, v = 0.9 + rnd.NextDouble() * 0.4;
                        Emit('j', Math.Cos(a) * 0.25, -0.05 + Math.Sin(a) * 0.2, Math.Cos(a) * v, Math.Sin(a) * v, 380, 0.08 + rnd.NextDouble() * 0.03);
                    }
                    if (t > 1500) powerStart = -1;
                    break;
                case MascotParts.PowerFlame:
                    // A short burst of flame from the mouth while the tail's flame grows.
                    if (powerStage == 0) { powerStage = 1; SetMood(Mood.Happy, 1300); }
                    if (t >= 250 && t < 950 && now - lastEmit > 30)
                    {
                        lastEmit = now;
                        for (int i = 0; i < 2; i++)
                        {
                            double a = (-30 + rnd.NextDouble() * 30) * Math.PI / 180, v = 0.9 + rnd.NextDouble() * 0.4;
                            Emit('f', 0.05 * dir, my, Math.Cos(a) * v * dir, Math.Sin(a) * v, 520, 0.035 + rnd.NextDouble() * 0.02);
                        }
                    }
                    if (t > 1500) powerStart = -1;
                    break;
                default:
                    if (!ArcadeStep(t)) powerStart = -1;
                    break;
            }
        }

        // How strong the trick's own drawing is now (Stitch's arms, Charmander's tail flame, Eevee's wagging).
        double TributeFx()
        {
            if (powerStart < 0) return 0;
            if (power >= MascotParts.PowerChomp) return ArcadeFx();
            double t = now - powerStart;
            if (power == MascotParts.PowerCrazy) return t < 1500 ? Math.Min(1, t / 200.0) : Math.Max(0, 1 - (t - 1500) / 300.0);
            if (power == MascotParts.PowerFlame) return t < 950 ? Math.Min(1, t / 250.0) : Math.Max(0, 1 - (t - 950) / 400.0);
            if (power == MascotParts.PowerRoll) return 1;
            if (power == MascotParts.PowerThunder) return t < 1000 ? Math.Min(1, t / 150.0) : Math.Max(0, 1 - (t - 1000) / 300.0);
            return 0;
        }

        static bool IsTributeParticle(char k) { return k == 'w' || k == 'k' || k == 'f' || k == 'j'; }

        static bool TributeMove(Particle p, double s)
        {
            if (ArcadeMove(p, s)) return true;
            switch (p.Kind)
            {
                case 'j':
                case 'w':
                case 'k':
                    p.X += p.Vx * s;
                    p.Y += p.Vy * s;
                    return true;
                case 'f':
                    Decay(ref p.X, ref p.Vx, 0.94, s);
                    Decay(ref p.Y, ref p.Vy, 0.94, s);
                    p.Y -= 0.12 * s;   // flame rises a little
                    return true;
            }
            return false;
        }

        static bool PaintTributeParticle(Graphics g, char kind, float s, int a)
        {
            switch (kind)
            {
                case 'j':
                {
                    // A little bolt of lightning, along its way.
                    PointF[] z = { new PointF(-s, 0), new PointF(-s * 0.3f, -s * 0.28f), new PointF(-s * 0.05f, s * 0.12f), new PointF(s * 0.4f, -s * 0.22f), new PointF(s, 0) };
                    MascotParts.Glow(g, 0, 0, s * 2.6f, s * 1.4f, Color.FromArgb(255, 220, 60), a / 2);
                    using (Pen outer = new Pen(Color.FromArgb(a, 255, 214, 40), Math.Max(1.5f, s * 0.2f)))
                    using (Pen core = new Pen(Color.FromArgb(a, 255, 255, 230), Math.Max(0.8f, s * 0.07f)))
                    {
                        outer.LineJoin = core.LineJoin = LineJoin.Round;
                        outer.StartCap = outer.EndCap = LineCap.Round;
                        g.DrawLines(outer, z);
                        g.DrawLines(core, z);
                    }
                    return true;
                }
                case 'w':
                    using (Pen pen = new Pen(Color.FromArgb(a * 3 / 4, 255, 255, 255), Math.Max(1f, s * 0.16f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, -s, -s * 0.6f, s * 2, s * 1.2f, 200, 200);
                    }
                    return true;
                case 'k':
                    MascotParts.Glow(g, 0, 0, s * 3.4f, s * 3.4f, Color.FromArgb(190, 225, 255), a / 2);
                    using (GraphicsPath rp = new GraphicsPath())
                    {
                        rp.AddPolygon(new PointF[] { new PointF(-s * 0.5f, -s * 0.15f), new PointF(-s * 0.1f, -s * 0.48f), new PointF(s * 0.45f, -s * 0.3f),
                                                     new PointF(s * 0.5f, s * 0.2f), new PointF(0, s * 0.45f), new PointF(-s * 0.45f, s * 0.3f) });
                        MascotParts.FillSolid(g, rp, Color.FromArgb(a, 150, 140, 128));
                        MascotParts.Stroke(g, rp, Color.FromArgb(a, 100, 92, 84), Math.Max(0.8f, s * 0.08f));
                    }
                    return true;
                case 'f':
                    MascotParts.Glow(g, 0, 0, s * 2.8f, s * 2.8f, Color.FromArgb(255, 120, 30), a * 3 / 4);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 150, 40), -s * 0.5f, -s * 0.45f, s, s * 0.9f);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 232, 120), -s * 0.22f, -s * 0.22f, s * 0.44f, s * 0.44f);
                    return true;
            }
            return false;
        }
    }
}
