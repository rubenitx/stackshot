// Stackshot - Five dragons from How to Train Your Dragon, each with its own build and power: Toothless (Night Fury: ear
// plates, big wings, the red prosthetic tail fin, plasma blasts), the Light Fury (white, plasma or a vanishing
// shimmer), Stormfly (Deadly Nadder: spiked crest, nose horn, tail spines, spines or magnesium fire), Hookfang
// (Monstrous Nightmare: horns, jaw spines, clawed wings, sets himself on fire) and Meatlug (Gronckle: round, buzzing
// little wings, club tail, eats a rock and spits lava). Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int KindNightFury = 14, KindLightFury = 15, KindNadder = 16, KindNightmare = 17, KindGronckle = 18;
        public const int ColorEmber = 25, EyesDragon = 7;
        // The powers, as Mascot.Power picks them.
        public const int PowerPlasma = 1, PowerVanish = 2, PowerSpines = 3, PowerMagnesium = 4, PowerFire = 5, PowerLava = 6;

        static readonly Color NadderGold = Color.FromArgb(252, 206, 70), NadderTip = Color.FromArgb(236, 100, 38);
        static readonly Color Bone = Color.FromArgb(246, 236, 214), BoneDeep = Color.FromArgb(190, 164, 124);
        static readonly Color Prosthetic = Color.FromArgb(208, 40, 44), ProstheticDeep = Color.FromArgb(128, 18, 24);
        static readonly Color PearlA = Color.FromArgb(252, 253, 255), PearlB = Color.FromArgb(198, 210, 234), PearlLine = Color.FromArgb(150, 168, 204);
        static readonly Color PearlWing = Color.FromArgb(206, 214, 244);   // the Light Fury's wings: pearl with a hint of lilac

        public static bool IsDragon(int kind) { return kind >= KindNightFury && kind <= KindGronckle; }

        // Eye colour of each dragon (also used when another kind wears the dragon eyes).
        public static Color DragonIris(int kind)
        {
            switch (kind)
            {
                case KindLightFury: return Color.FromArgb(96, 182, 252);
                case KindNadder: return Color.FromArgb(250, 212, 72);
                case KindNightmare: return Color.FromArgb(255, 194, 44);
                case KindGronckle: return Color.FromArgb(238, 168, 60);
                case KindNightFury: return Color.FromArgb(136, 214, 76);
                default: return TributeIris(kind);
            }
        }

        // The power's name in the menus, a hint, and what the dragon says doing it.
        public static string PowerName(int kind)
        {
            switch (kind)
            {
                case KindNightFury: return I18n.T("Dispara plasma", "Fire plasma");
                case KindLightFury: return I18n.T("Plasma o camuflaje", "Plasma or camouflage");
                case KindNadder: return I18n.T("P\u00FAas o fuego", "Spines or fire");
                case KindNightmare: return I18n.T("Pr\u00E9ndete fuego", "Burst into flames");
                case KindGronckle: return I18n.T("C\u00F3mete una roca", "Eat a rock");
                default: return TributePowerName(kind);
            }
        }

        public static string PowerHint(int kind)
        {
            switch (kind)
            {
                case KindNightFury: return I18n.T("Un disparo de plasma azul", "A blue plasma blast");
                case KindLightFury: return I18n.T("Plasma, o se vuelve invisible un momento", "Plasma, or turns invisible for a moment");
                case KindNadder: return I18n.T("Lanza p\u00FAas de la cola o escupe fuego de magnesio", "Throws tail spines or spits magnesium fire");
                case KindNightmare: return I18n.T("Se cubre de llamas y se apaga", "Covers itself in flames, then puts them out");
                case KindGronckle: return I18n.T("Se come una roca y escupe lava", "Eats a rock and spits lava");
                default: return TributePowerHint(kind);
            }
        }

        public static string PowerLine(int power)
        {
            switch (power)
            {
                case PowerPlasma: return I18n.T("\u00A1Disparo de plasma!", "Plasma blast!");
                case PowerVanish: return I18n.T("\u00BFMe ves? \u00BFY ahora?", "See me? How about now?");
                case PowerSpines: return I18n.T("\u00A1P\u00FAas al aire!", "Spines away!");
                case PowerMagnesium: return I18n.T("\u00A1Fuego de magnesio!", "Magnesium fire!");
                case PowerFire: return I18n.T("\u00A1Me enciendo!", "I'm on fire!");
                case PowerLava: return I18n.T("\u00D1am, una roca\u2026 \u00A1y lava!", "Yum, a rock\u2026 and lava!");
                default: return TributePowerLine(power);
            }
        }

        // ---- Behind the body.
        static void DragonBack(Graphics g, MascotLook l, float D, Color c1, Color c2, MascotPose p)
        {
            if (p.Fire > 0.01) BodyFire(g, D, p.Now, p.Fire);
            switch (l.Kind)
            {
                case KindNightFury:
                case KindLightFury:
                {
                    bool light = l.Kind == KindLightFury;
                    { GraphicsState ts = TailSide(g, p); FuryTail(g, D, c1, c2, p.Now, light); g.Restore(ts); }
                    Color m1 = light ? PearlA : Lighter(c2, 0.22), m2 = light ? PearlWing : Darker(c2, 0.05), edge = light ? PearlLine : Darker(c2, 0.4);
                    foreach (int s in Sides) DragonWing(g, D, s, m1, m2, edge, Color.FromArgb(150, edge), p.Now, p.Sleeping, 0.8f, 320, 8, false);
                    foreach (int s in Sides) FuryEars(g, D, s, c1, c2, s < 0 ? p.EarL : p.EarR, light);
                    break;
                }
                case KindNadder:
                    { GraphicsState ts = TailSide(g, p); NadderTail(g, D, c1, c2, p.Now); g.Restore(ts); }
                    foreach (int s in Sides) DragonWing(g, D, s, Lighter(c1, 0.15), c2, NadderGold, Color.FromArgb(140, Darker(c2, 0.3)), p.Now, p.Sleeping, 0.78f, 280, 10, false);
                    NadderCrest(g, D, p);
                    break;
                case KindNightmare:
                    { GraphicsState ts = TailSide(g, p); NightmareTail(g, D, c1, c2, p.Now); g.Restore(ts); }
                    foreach (int s in Sides) DragonWing(g, D, s, Lighter(c1, 0.08), Darker(c2, 0.05), Darker(c2, 0.4), Color.FromArgb(150, Darker(c2, 0.4)), p.Now, p.Sleeping, 0.85f, 300, 9, true);
                    foreach (int s in Sides) NightmareHorn(g, D, s, s < 0 ? p.EarL : p.EarR);
                    foreach (int s in Sides) JawSpines(g, D, s, c2);
                    break;
                case KindGronckle:
                    { GraphicsState ts = TailSide(g, p); GronckleTail(g, D, c1, c2, p.Now); g.Restore(ts); }
                    foreach (int s in Sides) GronckleWing(g, D, s, c2, p.Now, p.Sleeping);
                    GronckleKnobs(g, D, c1, c2);
                    break;
            }
        }

        // A bat-like wing out of the shoulder: bony leading edge, a membrane with scallops between its fingers, a slow
        // flap (folded down while asleep). span scales it; claws adds the Nightmare's hooks at the joint and the tip.
        static void DragonWing(Graphics g, float D, int s, Color m1, Color m2, Color edge, Color bone, double now, bool sleeping,
                               float span, double period, double amp, bool claws)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.27f * D, -0.03f * D);
            g.RotateTransform((float)(s * (sleeping ? -12 : Math.Sin(now / period) * amp - 2)));
            g.ScaleTransform(span, span);
            PointF tip = P(s * 0.4f * D, -0.3f * D), f1 = P(s * 0.42f * D, -0.06f * D), f2 = P(s * 0.27f * D, 0), f3 = P(s * 0.13f * D, 0.04f * D), root = P(s * 0.02f * D, 0.06f * D);
            using (GraphicsPath w = new GraphicsPath())
            {
                w.AddBezier(P(0, -0.03f * D), P(s * 0.1f * D, -0.2f * D), P(s * 0.26f * D, -0.31f * D), tip);
                // A rounded wingtip: softer, friendlier than a point.
                w.AddBezier(tip, P(s * 0.45f * D, -0.29f * D), P(s * 0.46f * D, -0.14f * D), f1);
                w.AddBezier(f1, P(s * 0.37f * D, -0.08f * D), P(s * 0.31f * D, -0.05f * D), f2);
                w.AddBezier(f2, P(s * 0.23f * D, -0.02f * D), P(s * 0.17f * D, 0), f3);
                w.AddBezier(f3, P(s * 0.1f * D, 0.02f * D), P(s * 0.05f * D, 0.04f * D), root);
                w.CloseFigure();
                Fill(g, w, new RectangleF(-0.43f * D, -0.32f * D, 0.86f * D, 0.39f * D), m1, m2, 90f);
                Stroke(g, w, edge, D * 0.012f);
            }
            Line(g, bone, D * 0.011f, 0, -0.03f * D, f1.X, f1.Y);
            Line(g, bone, D * 0.01f, 0, -0.03f * D, f2.X, f2.Y);
            Line(g, bone, D * 0.009f, 0, -0.03f * D, f3.X, f3.Y);
            if (claws)
            {
                foreach (PointF c in new PointF[] { tip, P(s * 0.2f * D, -0.26f * D) })
                    using (GraphicsPath h = Poly(P(c.X - s * 0.012f * D, c.Y + 0.01f * D), P(c.X + s * 0.035f * D, c.Y - 0.045f * D), P(c.X + s * 0.016f * D, c.Y + 0.012f * D)))
                        FillSolid(g, h, Bone);
            }
            g.Restore(st);
        }

        // Toothless's ear plates: a big one on top of each side of the head and a smaller one under it; they perk up and
        // twitch with the ears. The Light Fury's are smaller and sleeker.
        // Toothless's ear plates: rounded flaps falling out to the sides of the head, a big one and a smaller one under it;
        // they twitch with the ears. The Light Fury's are smaller and lie further back.
        static void FuryEars(Graphics g, float D, int s, Color c1, Color c2, double twitch, bool light)
        {
            EarPlate(g, D, s * 0.15f * D, -0.25f * D, (float)(s * ((light ? 64 : 52) + twitch * 1.1)), light ? 0.15f : 0.2f, c1, c2, light);
            EarPlate(g, D, s * 0.25f * D, -0.14f * D, (float)(s * ((light ? 92 : 84) + twitch * 0.6)), light ? 0.1f : 0.13f, c1, c2, light);
        }

        static void EarPlate(Graphics g, float D, float x, float y, float rot, float len, Color c1, Color c2, bool light)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rot);
            float L = len * D, w = len * 0.62f * D;
            using (GraphicsPath e = new GraphicsPath())
            {
                // A soft leaf with a round end.
                e.AddBezier(P(-w * 0.5f, 0.02f * D), P(-w * 0.72f, -L * 0.55f), P(-w * 0.42f, -L), P(0, -L));
                e.AddBezier(P(0, -L), P(w * 0.42f, -L), P(w * 0.72f, -L * 0.55f), P(w * 0.5f, 0.02f * D));
                e.CloseFigure();
                Fill(g, e, new RectangleF(-w, -L, w * 2, L + 0.03f * D), light ? PearlA : Lighter(c1, 0.12), light ? PearlB : c2, 80f);
                Stroke(g, e, light ? PearlLine : Darker(c2, 0.3), D * 0.008f);
            }
            // A lighter groove down the middle, as on the films' ears.
            Line(g, light ? Color.FromArgb(120, 170, 186, 220) : Color.FromArgb(70, 255, 255, 255), D * 0.012f, 0, -L * 0.15f, 0, -L * 0.75f);
            g.Restore(st);
        }

        // A tail that leaves low from behind the body and sweeps out to the side, thinner towards its tip (never curling
        // back towards the body). Returns the tip and the tail's direction there, in degrees.
        static PointF TaperTail(Graphics g, float D, Color a, Color b, double now, float w0, float w1, out float angle)
        {
            float sway = (float)Math.Sin(now / 600.0) * 0.03f * D;
            PointF p0 = P(0.06f * D, 0.27f * D), p1 = P(0.32f * D, 0.4f * D), p2 = P(0.5f * D, 0.34f * D + sway * 0.4f), p3 = P(0.6f * D, 0.17f * D + sway);
            const int n = 18;
            PointF[] left = new PointF[n + 1], right = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                PointF q = Bez(p0, p1, p2, p3, t), d = BezD(p0, p1, p2, p3, t);
                float len = (float)Math.Max(0.001, Math.Sqrt(d.X * d.X + d.Y * d.Y)), half = (w0 + (w1 - w0) * t) * D / 2;
                float nx = -d.Y / len, ny = d.X / len;
                left[i] = P(q.X + nx * half, q.Y + ny * half);
                right[n - i] = P(q.X - nx * half, q.Y - ny * half);
            }
            using (GraphicsPath tp = new GraphicsPath())
            {
                tp.AddLines(left);
                tp.AddLines(right);
                tp.CloseFigure();
                Fill(g, tp, tp.GetBounds(), a, b, 70f);
                Stroke(g, tp, Color.FromArgb(190, Darker(b, 0.5)), D * 0.011f);
            }
            PointF dd = BezD(p0, p1, p2, p3, 1);
            angle = (float)(Math.Atan2(dd.Y, dd.X) * 180 / Math.PI);
            return p3;
        }

        static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t)
        {
            float u = 1 - t;
            return P(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X, u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
        }

        static PointF BezD(PointF a, PointF b, PointF c, PointF d, float t)
        {
            float u = 1 - t;
            return P(3 * u * u * (b.X - a.X) + 6 * u * t * (c.X - b.X) + 3 * t * t * (d.X - c.X), 3 * u * u * (b.Y - a.Y) + 6 * u * t * (c.Y - b.Y) + 3 * t * t * (d.Y - c.Y));
        }

        // The Furies' tail with its two fins open at the tip, one to each side. Toothless's left one (on our right) is the
        // red prosthetic Hiccup made him, with its white mark.
        static void FuryTail(Graphics g, float D, Color c1, Color c2, double now, bool light)
        {
            float angle;
            PointF end = TaperTail(g, D, light ? PearlA : Lighter(c1, 0.05), light ? PearlB : c2, now, 0.11f, 0.045f, out angle);
            GraphicsState st = g.Save();
            g.TranslateTransform(end.X, end.Y);
            g.RotateTransform((float)(angle + 90 + Math.Sin(now / 420.0) * 5));
            g.ScaleTransform(1.25f, 1.25f);
            TailFin(g, D, -1, light ? PearlA : Lighter(c2, 0.2), light ? PearlB : Darker(c2, 0.05), false);
            TailFin(g, D, 1, light ? PearlA : Prosthetic, light ? PearlB : ProstheticDeep, !light);
            g.Restore(st);
        }

        static void TailFin(Graphics g, float D, int s, Color a, Color b, bool prosthetic)
        {
            using (GraphicsPath f = new GraphicsPath())
            {
                f.AddBezier(P(0, 0), P(s * 0.05f * D, -0.02f * D), P(s * 0.11f * D, -0.07f * D), P(s * 0.12f * D, -0.13f * D));
                f.AddBezier(P(s * 0.12f * D, -0.13f * D), P(s * 0.08f * D, -0.1f * D), P(s * 0.05f * D, -0.1f * D), P(s * 0.035f * D, -0.115f * D));
                f.AddBezier(P(s * 0.035f * D, -0.115f * D), P(s * 0.025f * D, -0.07f * D), P(s * 0.01f * D, -0.03f * D), P(0, 0));
                f.CloseFigure();
                Fill(g, f, new RectangleF(Math.Min(0, s * 0.13f * D), -0.14f * D, 0.13f * D, 0.15f * D), a, b, 70f);
                Stroke(g, f, Darker(b, 0.35), D * 0.008f);
            }
            if (!prosthetic) return;
            float cx = s * 0.072f * D, cy = -0.08f * D, r = 0.016f * D;
            Ellipse(g, Color.FromArgb(245, 250, 250, 250), cx - r, cy - r, r * 2, r * 1.8f);
            Ellipse(g, ProstheticDeep, cx - r * 0.6f, cy - r * 0.25f, r * 0.45f, r * 0.45f);
            Ellipse(g, ProstheticDeep, cx + r * 0.15f, cy - r * 0.25f, r * 0.45f, r * 0.45f);
        }

        // Stormfly's crown: a fan of spikes around the top of the head, gold at the root and orange at the tip; the
        // outer ones are the horns at the sides. They lift a little with the ears.
        // Stormfly's crown: a few soft, rounded spikes on top of the head, gold at the root and orange at the tip, like a
        // bird's crest. They lift a little with the ears.
        static void NadderCrest(Graphics g, float D, MascotPose p)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = (i - 2) / 2f;
                float len = (0.15f - Math.Abs(t) * 0.035f) * D, w = 0.04f * D;
                GraphicsState st = g.Save();
                g.TranslateTransform(t * 0.11f * D, -0.26f * D);
                g.RotateTransform((float)(t * 44 + (t < 0 ? p.EarL : p.EarR) * 0.6));
                Petal(g, D, w, len, NadderTip, NadderGold);
                g.Restore(st);
            }
        }

        // A spike with a rounded end, rising from its base at the origin.
        static void Petal(Graphics g, float D, float w, float len, Color tip, Color root)
        {
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddBezier(P(-w, 0.02f * D), P(-w * 1.05f, -len * 0.5f), P(-w * 0.55f, -len), P(0, -len));
                sp.AddBezier(P(0, -len), P(w * 0.55f, -len), P(w * 1.05f, -len * 0.5f), P(w, 0.02f * D));
                sp.CloseFigure();
                Fill(g, sp, new RectangleF(-w, -len, w * 2, len + 0.03f * D), tip, root, 90f);
                Stroke(g, sp, Color.FromArgb(90, 150, 70, 20), D * 0.006f);
            }
        }

        // A tail ending in a small cluster of rounded spines, the ones it throws.
        static void NadderTail(Graphics g, float D, Color c1, Color c2, double now)
        {
            float angle;
            PointF end = TaperTail(g, D, Lighter(c1, 0.05), c2, now, 0.1f, 0.045f, out angle);
            for (int i = 0; i < 3; i++)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(end.X, end.Y);
                g.RotateTransform((float)(angle + 90 + (i - 1) * 34));
                Petal(g, D, 0.024f * D, (0.08f + (i == 1 ? 0.02f : 0)) * D, NadderTip, NadderGold);
                g.Restore(st);
            }
        }

        // Hookfang's horns: long, sweeping back and out, bone-coloured with darker tips.
        static void NightmareHorn(Graphics g, float D, int s, double twitch)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(s * 0.15f * D, -0.26f * D);
            g.RotateTransform((float)(s * (24 + twitch * 0.4)));
            g.ScaleTransform(0.72f, 0.72f);
            using (GraphicsPath h = new GraphicsPath())
            {
                // Rounded at the tip: a horn, not a blade.
                h.AddBezier(P(-s * 0.035f * D, 0.02f * D), P(-s * 0.01f * D, -0.12f * D), P(s * 0.07f * D, -0.22f * D), P(s * 0.17f * D, -0.25f * D));
                h.AddBezier(P(s * 0.17f * D, -0.25f * D), P(s * 0.22f * D, -0.25f * D), P(s * 0.2f * D, -0.2f * D), P(s * 0.15f * D, -0.18f * D));
                h.AddBezier(P(s * 0.15f * D, -0.18f * D), P(s * 0.08f * D, -0.13f * D), P(s * 0.05f * D, -0.07f * D), P(s * 0.04f * D, 0.02f * D));
                h.CloseFigure();
                Fill(g, h, new RectangleF(-0.05f * D, -0.26f * D, 0.26f * D, 0.29f * D), Bone, BoneDeep, 60f);
                Stroke(g, h, Color.FromArgb(150, 120, 90, 60), D * 0.007f);
            }
            g.Restore(st);
        }

        // Spines along the jaw, pointing out and down.
        static void JawSpines(Graphics g, float D, int s, Color c2)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = s * (0.31f + i * 0.01f) * D, y = (0.03f + i * 0.08f) * D;
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(s * (72 + i * 18));
                Petal(g, D, 0.024f * D, 0.065f * D, Darker(c2, 0.3), c2);
                g.Restore(st);
            }
        }

        // A long, thin tail with a spade-like spike.
        static void NightmareTail(Graphics g, float D, Color c1, Color c2, double now)
        {
            float angle;
            PointF end = TaperTail(g, D, Lighter(c1, 0.05), c2, now, 0.09f, 0.035f, out angle);
            GraphicsState st = g.Save();
            g.TranslateTransform(end.X, end.Y);
            g.RotateTransform(angle + 90);
            Petal(g, D, 0.035f * D, 0.08f * D, Darker(c2, 0.35), c2);
            g.Restore(st);
        }

        // Meatlug's wings: tiny, buzzing like a bumblebee's, so they show as a blur.
        static void GronckleWing(Graphics g, float D, int s, Color c2, double now, bool sleeping)
        {
            double beat = sleeping ? 0.15 : Math.Sin(now / 26.0);
            for (int k = 0; k < (sleeping ? 1 : 2); k++)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.24f * D, -0.17f * D);
                g.RotateTransform((float)(s * (-35 + (k == 0 ? beat : -beat) * 24)));
                RectangleF w = new RectangleF(0, -0.05f * D, s * 0.2f * D, 0.1f * D);
                if (s < 0) w = new RectangleF(-0.2f * D, -0.05f * D, 0.2f * D, 0.1f * D);
                using (GraphicsPath wp = new GraphicsPath())
                {
                    wp.AddEllipse(w);
                    FillSolid(g, wp, Color.FromArgb(sleeping ? 230 : 120, Lighter(c2, 0.35)));
                    Stroke(g, wp, Color.FromArgb(sleeping ? 200 : 110, Darker(c2, 0.3)), D * 0.007f);
                }
                g.Restore(st);
            }
        }

        // The rocky knobs on top of its head.
        static void GronckleKnobs(Graphics g, float D, Color c1, Color c2)
        {
            float[] xs = { -0.2f, -0.08f, 0.06f, 0.18f };
            for (int i = 0; i < xs.Length; i++)
            {
                float r = (i == 1 || i == 2 ? 0.055f : 0.045f) * D, x = xs[i] * D, y = -0.255f * D - (i == 1 || i == 2 ? 0.015f * D : 0);
                using (GraphicsPath k = new GraphicsPath())
                {
                    RectangleF kr = new RectangleF(x - r, y - r, r * 2, r * 2);
                    k.AddEllipse(kr);
                    Fill(g, k, kr, Lighter(c1, 0.15), c2, 70f);
                }
            }
        }

        // A short tail ending in a knobby club.
        static void GronckleTail(Graphics g, float D, Color c1, Color c2, double now)
        {
            float sway = (float)Math.Sin(now / 640.0) * 0.03f * D;
            PointF end = P(0.42f * D + sway, 0.1f * D);
            using (GraphicsPath t = new GraphicsPath())
            {
                t.AddBezier(P(0.2f * D, 0.24f * D), P(0.32f * D, 0.27f * D), P(0.4f * D, 0.2f * D), end);
                using (Pen pen = new Pen(c2, D * 0.07f)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawPath(pen, t); }
            }
            float r = 0.075f * D;
            RectangleF club = new RectangleF(end.X - r, end.Y - r, r * 2, r * 2);
            using (GraphicsPath cp = new GraphicsPath())
            {
                cp.AddEllipse(club);
                Fill(g, cp, club, Lighter(c1, 0.1), Darker(c2, 0.1), 60f);
            }
            for (int i = 0; i < 5; i++)
            {
                double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                float kx = end.X + (float)Math.Cos(a) * r * 0.9f, ky = end.Y + (float)Math.Sin(a) * r * 0.9f, kr = r * 0.32f;
                Ellipse(g, Darker(c2, 0.05), kx - kr, ky - kr, kr * 2, kr * 2);
            }
        }

        // Hookfang on fire: tongues of flame all around the top and sides, flickering, behind everything else.
        static void BodyFire(Graphics g, float D, double now, double k)
        {
            Glow(g, 0, -0.04f * D, 1.15f * D, 1.05f * D, Color.FromArgb(255, 120, 30), (int)(130 * k));
            for (int i = 0; i < 15; i++)
            {
                double a = -Math.PI / 2 + (i - 7) / 7.0 * Math.PI * 0.62;
                double flick = 0.72 + 0.22 * Math.Sin(now / 70.0 + i * 1.7) + 0.08 * Math.Sin(now / 37.0 + i);
                float bx = (float)Math.Cos(a) * 0.33f * D, by = (float)Math.Sin(a) * 0.3f * D + 0.02f * D;
                float len = (float)(0.22 * flick * k) * D, w = 0.065f * D;
                GraphicsState st = g.Save();
                g.TranslateTransform(bx, by);
                g.RotateTransform((float)(a * 180 / Math.PI + 90));
                using (GraphicsPath f = new GraphicsPath())
                {
                    f.AddBezier(P(-w, 0), P(-w, -len * 0.5f), P(-w * 0.2f, -len * 0.7f), P(0, -len));
                    f.AddBezier(P(0, -len), P(w * 0.3f, -len * 0.6f), P(w, -len * 0.45f), P(w, 0));
                    f.CloseFigure();
                    if (len > 1) Fill(g, f, new RectangleF(-w, -len, w * 2, len), Color.FromArgb((int)(210 * k), 255, 84, 18), Color.FromArgb((int)(240 * k), 255, 216, 84), 90f);
                }
                g.Restore(st);
            }
        }

        // ---- On the body: bellies, the Gronckle's warts.
        static void DragonBody(Graphics g, MascotLook l, float D, GraphicsPath body, Color c1)
        {
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            switch (l.Kind)
            {
                case KindNadder:
                    Ellipse(g, Color.FromArgb(235, 255, 238, 186), -0.17f * D, 0.07f * D, 0.34f * D, 0.3f * D);
                    break;
                case KindNightmare:
                    Ellipse(g, Color.FromArgb(150, 255, 166, 60), -0.16f * D, 0.08f * D, 0.32f * D, 0.28f * D);
                    for (int i = 0; i < 3; i++) Line(g, Color.FromArgb(90, 150, 40, 20), D * 0.01f, -0.1f * D, (0.15f + i * 0.055f) * D, 0.1f * D, (0.15f + i * 0.055f) * D);
                    break;
                case KindGronckle:
                    Ellipse(g, Color.FromArgb(150, 236, 204, 150), -0.19f * D, 0.08f * D, 0.38f * D, 0.3f * D);
                    float[,] warts = { { -0.27f, -0.12f }, { 0.25f, -0.14f }, { -0.3f, 0.06f }, { 0.29f, 0.04f }, { -0.15f, -0.2f }, { 0.12f, -0.21f } };
                    for (int i = 0; i < warts.GetLength(0); i++)
                    {
                        float r = 0.022f * D;
                        Ellipse(g, Color.FromArgb(150, Lighter(c1, 0.3)), warts[i, 0] * D - r, warts[i, 1] * D - r, r * 2, r * 2);
                    }
                    break;
            }
            g.Clip = old;
            old.Dispose();
        }

        // ---- In front of the face: nostrils, Stormfly's nose horn, Meatlug's tusks, and the glow of a power charging.
        static void DragonDetails(Graphics g, MascotLook l, Geo geo, float D, MascotPose p)
        {
            float fy = geo.FaceY * D;
            Color nose = l.Kind == KindNightFury ? Color.FromArgb(150, 10, 10, 14) : Color.FromArgb(110, Ink);
            Ellipse(g, nose, -0.04f * D, fy + 0.06f * D, 0.016f * D, 0.012f * D);
            Ellipse(g, nose, 0.024f * D, fy + 0.06f * D, 0.016f * D, 0.012f * D);
            if (l.Kind == KindNadder)
            {
                // A little horn on the snout, just above the nostrils.
                GraphicsState st = g.Save();
                g.TranslateTransform(0, fy + 0.058f * D);
                Petal(g, D, 0.015f * D, 0.04f * D, BoneDeep, Bone);
                g.Restore(st);
            }
            if (l.Kind == KindGronckle)
                foreach (int s in Sides)
                    using (GraphicsPath t = Poly(P(s * 0.06f * D, fy + 0.115f * D), P(s * 0.075f * D, fy + 0.07f * D), P(s * 0.088f * D, fy + 0.115f * D)))
                        FillSolid(g, t, Bone);
        }

        // The mouth lights up while a power charges: blue plasma, white magnesium or orange lava.
        public static void PaintDragonFront(Graphics g, MascotLook l, float D, MascotPose p)
        {
            if (p.Charge <= 0.01) return;
            Geo geo = GeoFor(l.Kind);
            float my = geo.FaceY * D + 0.09f * D;
            Color c = p.Power == PowerMagnesium ? Color.FromArgb(220, 240, 255) : p.Power == PowerLava ? Color.FromArgb(255, 140, 30) : Color.FromArgb(120, 150, 255);
            float r = (float)(0.1 + 0.12 * p.Charge) * D;
            Glow(g, 0, my, r * 2, r * 1.6f, c, (int)(200 * p.Charge));
            Glow(g, 0, my, r * 0.8f, r * 0.6f, Color.White, (int)(220 * p.Charge));
        }
    }

    // The dragon's power and its eyes, on the mascot itself.
    public partial class Mascot
    {
        double powerStart = -1, hideUntil, fireUntil, fireFrom, focusUntil, lastEmit;
        int power, powerStage;

        public bool HasPower { get { return MascotParts.HasPower(Look.Kind); } }

        bool PowerLively { get { return powerStart >= 0 || now < fireUntil || now < hideUntil || now < flashUntil || ventStart >= 0; } }

        // Starts the dragon's own power (each has one, two for the Light Fury and Stormfly).
        public void Power()
        {
            if (!HasPower) return;
            now = Clock();
            Wake();
            Stop(false);
            mood = Mood.Idle;
            switch (Look.Kind)
            {
                case MascotParts.KindNightFury: power = MascotParts.PowerPlasma; break;
                case MascotParts.KindLightFury: power = rnd.NextDouble() < 0.5 ? MascotParts.PowerPlasma : MascotParts.PowerVanish; break;
                case MascotParts.KindNadder: power = rnd.NextDouble() < 0.5 ? MascotParts.PowerSpines : MascotParts.PowerMagnesium; break;
                case MascotParts.KindNightmare: power = MascotParts.PowerFire; break;
                case MascotParts.KindGronckle: power = MascotParts.PowerLava; break;
                default: power = TributePower(); break;
            }
            if (power == 0) return;
            hideUntil = 0;   // a power asked again starts clean
            fireUntil = 0;
            powerStart = now;
            powerStage = 0;
            lastEmit = 0;
            focusUntil = now + 1500;
            if (power == MascotParts.PowerFire)
            {
                fireFrom = now;
                fireUntil = now + 2800;
                jumpV -= 1.2;
                SetMood(Mood.Happy, 2800);
            }
            if (Talks) Say(MascotParts.PowerLine(power), 2600);
        }

        double MouthY { get { return MascotParts.GeoFor(Look.Kind).FaceY + 0.09; } }

        // Called each step, after the particles moved.
        void DragonStep()
        {
            if (now < hideUntil) appear = 0.02;
            if (powerStart < 0) return;
            double t = now - powerStart, my = MouthY;
            switch (power)
            {
                case MascotParts.PowerPlasma:
                    // Charge, the shot with a recoil, and a burst where it ends.
                    if (powerStage == 0 && t >= 380)
                    {
                        powerStage = 1;
                        double dir = Facing < -0.2 ? -1 : 1;
                        Emit('P', 0.02 * dir, my, 0.55 * dir, -1.3, 620, 0.075);
                        jumpV += 0.9;
                        tiltV -= 70 * dir;
                    }
                    else if (powerStage == 1 && t >= 380 + 600)
                    {
                        powerStage = 2;
                        double dir = Facing < -0.2 ? -1 : 1, bx = 0.02 * dir + 0.55 * dir * 0.6, by = my - 1.3 * 0.6;
                        for (int i = 0; i < 14; i++)
                        {
                            double a = rnd.NextDouble() * Math.PI * 2, v = 0.35 + rnd.NextDouble() * 0.35;
                            Emit('q', bx, by, Math.Cos(a) * v, Math.Sin(a) * v, 520, 0.022 + rnd.NextDouble() * 0.018);
                        }
                    }
                    if (t > 1700 && powerStage >= 2) powerStart = -1;
                    break;
                case MascotParts.PowerVanish:
                    // A shimmer, gone for a moment, then back in a sparkle.
                    if (powerStage == 0 && t >= 260)
                    {
                        powerStage = 1;
                        Sparkles(0, -0.05, 16);
                        hideUntil = now + 900;
                    }
                    else if (powerStage == 1 && t >= 260 + 900)
                    {
                        powerStage = 2;
                        Sparkles(0, -0.05, 12);
                        SetMood(Mood.Wink, 1200);
                    }
                    if (t > 1900 && powerStage >= 2) powerStart = -1;
                    break;
                case MascotParts.PowerSpines:
                    // A flick of the tail and a fan of spines into the air.
                    if (powerStage == 0 && t >= 300)
                    {
                        powerStage = 1;
                        double side = tailLeft ? -1 : 1;   // from wherever its tail is
                        tiltV += 140 * side;
                        for (int i = 0; i < 6; i++)
                        {
                            double a = (-80 + i * 14) * Math.PI / 180, v = 1.0 + rnd.NextDouble() * 0.3;
                            Emit('S', 0.48 * side, -0.06, Math.Cos(a) * v * side, Math.Sin(a) * v, 900, 0.07);
                        }
                    }
                    if (t > 1500) powerStart = -1;
                    break;
                case MascotParts.PowerMagnesium:
                    // A spray of white-hot sparks from the mouth.
                    if (t >= 320 && t < 1150 && now - lastEmit > 24)
                    {
                        lastEmit = now;
                        double dir = Facing < -0.2 ? -1 : 1;
                        for (int i = 0; i < 2; i++)
                        {
                            double a = (-60 + rnd.NextDouble() * 34) * Math.PI / 180, v = 1.1 + rnd.NextDouble() * 0.5;
                            Emit('m', 0.03 * dir, my, Math.Cos(a) * v * dir, Math.Sin(a) * v, 440, 0.024 + rnd.NextDouble() * 0.016);
                        }
                    }
                    if (t > 1600) powerStart = -1;
                    break;
                case MascotParts.PowerFire:
                    // Embers rise off it while it burns.
                    if (now < fireUntil && now - lastEmit > 70)
                    {
                        lastEmit = now;
                        Emit('e', (rnd.NextDouble() - 0.5) * 0.7, -0.1 + (rnd.NextDouble() - 0.5) * 0.4, (rnd.NextDouble() - 0.5) * 0.12, -0.35 - rnd.NextDouble() * 0.3, 900, 0.02 + rnd.NextDouble() * 0.016);
                    }
                    if (now >= fireUntil + 200) powerStart = -1;
                    break;
                case MascotParts.PowerLava:
                    // A rock flies into its mouth, it chews, then spits lava.
                    if (powerStage == 0)
                    {
                        powerStage = 1;
                        Emit('r', -0.6, my - 0.15, 1.2, 0.3, 500, 0.07);
                    }
                    else if (powerStage == 1 && t >= 600) { powerStage = 2; jumpV -= 0.6; }
                    else if (powerStage == 2 && t >= 850) { powerStage = 3; jumpV -= 0.6; }
                    else if (powerStage == 3 && t >= 1100) { powerStage = 4; jumpV -= 0.6; }
                    else if (powerStage == 4 && t >= 1350)
                    {
                        powerStage = 5;
                        double dir = Facing < -0.2 ? -1 : 1;
                        for (int i = 0; i < 3; i++) Emit('L', 0.04 * dir, my, (0.75 + rnd.NextDouble() * 0.25) * dir, -0.85 + rnd.NextDouble() * 0.2, 1100, 0.045 + rnd.NextDouble() * 0.02);
                        jumpV += 0.6;
                        SetMood(Mood.Happy, 1400);
                    }
                    if (t > 2600 && powerStage >= 5) powerStart = -1;
                    break;
                default:
                    TributeStep(t, my);
                    break;
            }
        }

        void Sparkles(double x, double y, int n)
        {
            for (int i = 0; i < n; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, v = 0.3 + rnd.NextDouble() * 0.45;
                Emit('q', x + Math.Cos(a) * 0.12, y + Math.Sin(a) * 0.12, Math.Cos(a) * v, Math.Sin(a) * v, 620, 0.02 + rnd.NextDouble() * 0.02);
            }
        }

        void Emit(char kind, double x, double y, double vx, double vy, double life, double size)
        {
            Particle p = new Particle();
            p.Kind = kind;
            p.X = x; p.Y = y; p.Vx = vx; p.Vy = vy;
            p.Born = now; p.Life = life; p.Size = size;
            p.Rot = Math.Atan2(vy, vx) * 180 / Math.PI;
            p.Tint = rnd.Next(4);
            parts.Add(p);
        }

        // How a dragon particle moves: false when it isn't one.
        static bool DragonMove(Particle p, double s)
        {
            if (TributeMove(p, s)) return true;
            switch (p.Kind)
            {
                case 'P':
                case 'r':
                    p.X += p.Vx * s;
                    p.Y += p.Vy * s;
                    return true;
                case 'q':
                case 'm':
                    Decay(ref p.X, ref p.Vx, 0.93, s);
                    Decay(ref p.Y, ref p.Vy, 0.93, s);
                    return true;
                case 'e':
                    p.X += p.Vx * s;
                    p.Y += p.Vy * s;
                    return true;
                case 'S':
                case 'L':
                    p.X += p.Vx * s;
                    p.Y += p.Vy * s + 0.7 * s * s;
                    p.Vy += 1.4 * s;
                    p.Rot = Math.Atan2(p.Vy, p.Vx) * 180 / Math.PI;
                    return true;
            }
            return false;
        }

        // Fire, charge and power for the drawing.
        void DragonPose()
        {
            double fire = 0;
            if (now < fireUntil) fire = Math.Min(1, (now - fireFrom) / 300.0) * Math.Min(1, (fireUntil - now) / 400.0);
            pose.Fire = fire;
            pose.Power = power;
            double charge = 0;
            if (powerStart >= 0)
            {
                double t = now - powerStart;
                if (power == MascotParts.PowerPlasma && powerStage == 0) charge = t / 380.0;
                else if (power == MascotParts.PowerMagnesium && t < 1150) charge = Math.Min(1, t / 320.0);
                else if (power == MascotParts.PowerLava && t >= 1150 && powerStage < 5) charge = (t - 1150) / 200.0;
            }
            pose.Charge = Math.Max(0, Math.Min(1, charge));
            pose.Fx = TributeFx();
        }

        static bool IsDragonParticle(char k) { return k == 'P' || k == 'q' || k == 'S' || k == 'm' || k == 'e' || k == 'L' || k == 'r' || IsTributeParticle(k); }

        // Drawn at the particle, turned along its travel.
        static void PaintDragonParticle(Graphics g, char kind, float s, int a)
        {
            if (PaintTributeParticle(g, kind, s, a)) return;
            switch (kind)
            {
                case 'P':
                {
                    // The plasma bolt: a violet halo, a cyan core and a short trail.
                    using (GraphicsPath tr = new GraphicsPath())
                    {
                        tr.AddPolygon(new PointF[] { new PointF(-s * 3.2f, 0), new PointF(0, -s * 0.5f), new PointF(0, s * 0.5f) });
                        using (LinearGradientBrush tb = new LinearGradientBrush(new RectangleF(-s * 3.3f, -s, s * 3.4f, s * 2), Color.FromArgb(0, 120, 120, 255), Color.FromArgb(a * 3 / 4, 150, 170, 255), 0f))
                            g.FillPath(tb, tr);
                    }
                    MascotParts.Glow(g, 0, 0, s * 3.2f, s * 3.2f, Color.FromArgb(150, 110, 255), a * 3 / 4);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 130, 220, 255), -s * 0.6f, -s * 0.6f, s * 1.2f, s * 1.2f);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 255, 255), -s * 0.32f, -s * 0.32f, s * 0.64f, s * 0.64f);
                    break;
                }
                case 'q':
                    MascotParts.Glow(g, 0, 0, s * 2.6f, s * 2.6f, Color.FromArgb(140, 160, 255), a * 2 / 3);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 210, 230, 255), -s * 0.35f, -s * 0.35f, s * 0.7f, s * 0.7f);
                    break;
                case 'm':
                    MascotParts.Glow(g, 0, 0, s * 2.8f, s * 2.8f, Color.FromArgb(190, 225, 255), a * 2 / 3);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 255, 255), -s * 0.38f, -s * 0.38f, s * 0.76f, s * 0.76f);
                    break;
                case 'e':
                    MascotParts.Glow(g, 0, 0, s * 2.4f, s * 2.4f, Color.FromArgb(255, 120, 30), a * 2 / 3);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 210, 90), -s * 0.3f, -s * 0.3f, s * 0.6f, s * 0.6f);
                    break;
                case 'S':
                    using (GraphicsPath sp = new GraphicsPath())
                    {
                        sp.AddPolygon(new PointF[] { new PointF(s * 0.6f, 0), new PointF(-s * 0.5f, -s * 0.16f), new PointF(-s * 0.5f, s * 0.16f) });
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-s * 0.6f, -s * 0.2f, s * 1.3f, s * 0.4f), Color.FromArgb(a, 252, 206, 70), Color.FromArgb(a, 236, 100, 38), 0f))
                            g.FillPath(b, sp);
                    }
                    break;
                case 'L':
                    MascotParts.Glow(g, 0, 0, s * 2.6f, s * 2.6f, Color.FromArgb(255, 110, 20), a * 3 / 4);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 120, 24), -s * 0.55f, -s * 0.48f, s * 1.1f, s * 0.96f);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 214, 90), -s * 0.25f, -s * 0.3f, s * 0.45f, s * 0.4f);
                    break;
                default:
                    using (GraphicsPath rp = new GraphicsPath())
                    {
                        rp.AddPolygon(new PointF[] { new PointF(-s * 0.5f, -s * 0.2f), new PointF(-s * 0.15f, -s * 0.5f), new PointF(s * 0.4f, -s * 0.35f),
                                                     new PointF(s * 0.5f, s * 0.15f), new PointF(s * 0.1f, s * 0.48f), new PointF(-s * 0.42f, s * 0.3f) });
                        MascotParts.FillSolid(g, rp, Color.FromArgb(a, 128, 120, 112));
                        MascotParts.Stroke(g, rp, Color.FromArgb(a, 84, 78, 72), Math.Max(0.8f, s * 0.08f));
                    }
                    break;
            }
        }

        // Kirby's eyes: tall ovals, black at the top turning blue at the bottom, with a white shine high up.
        void KirbyEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            float k = surprised ? 1.1f : 1f, w = D * 0.075f * k, h = D * 0.16f * k * blink;
            if (h < D * 0.02f)
            {
                MascotParts.Line(g, Color.FromArgb(230, 20, 20, 34), Math.Max(1.2f, D * 0.02f), x - w * 0.6f, y, x + w * 0.6f, y);
                return;
            }
            RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
            using (GraphicsPath ep = Ellipse(r))
            using (LinearGradientBrush b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.FromArgb(14, 14, 26), Color.FromArgb(44, 92, 214), 90f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(14, 14, 26), Color.FromArgb(16, 18, 40), Color.FromArgb(52, 104, 226) };
                cb.Positions = new float[] { 0f, 0.55f, 1f };
                b.InterpolationColors = cb;
                g.FillPath(b, ep);
            }
            if (blink < 0.5) return;
            MascotParts.Ellipse(g, Color.FromArgb(250, 255, 255, 255), x - w * 0.32f, y - h * 0.4f, w * 0.64f, h * 0.36f);
        }

        // Each character's own eyes (MascotParts.EyeFor), drawn the way the originals are: an iris darker at the top, as
        // under the lid, and lighter below (or all dark, like Pikachu's and Stitch's), a big round pupil that a dragon
        // narrows to a slit while it uses its power, a soft rim instead of a hard outline, an eyelid line where the
        // character has one, and two catchlights placed the same on both eyes, the big one high on the side the light
        // comes from (the top left, as on the body's shine).
        void DragonEye(Graphics g, float x, float y, float D, float blink, bool surprised)
        {
            if (Look.Kind == MascotParts.KindKirby) { KirbyEye(g, x, y, D, blink, surprised); return; }
            MascotParts.EyeSpec e = MascotParts.EyeFor(Look.Kind);
            float k = surprised ? 1.1f : 1f, w = D * e.W * k, h = D * e.H * k * blink;
            Color ink = e.Dark ? Color.FromArgb(18, 16, 22) : MascotParts.Darker(e.Iris, 0.72);
            if (h < D * 0.02f)
            {
                MascotParts.Line(g, Color.FromArgb(235, e.Lid ? e.LidColor : ink), Math.Max(1.2f, D * 0.02f), x - w * 0.52f, y, x + w * 0.52f, y);
                return;
            }
            RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
            using (GraphicsPath ep = Ellipse(r))
            {
                if (e.Dark) MascotParts.Fill(g, ep, r, Color.FromArgb(10, 10, 14), Color.FromArgb(52, 50, 66), 90f);
                else MascotParts.Fill(g, ep, r, MascotParts.Darker(e.Iris, 0.42), MascotParts.Lighter(e.Iris, 0.22), 90f);
                if (!e.Dark)
                {
                    // A lighter glow low in the iris, then the pupil over the middle.
                    Region old = g.Clip;
                    g.SetClip(ep, CombineMode.Intersect);
                    MascotParts.Ellipse(g, Color.FromArgb(110, MascotParts.Lighter(e.Iris, 0.55)), x - w * 0.34f, y + h * 0.08f, w * 0.68f, h * 0.4f);
                    g.Clip = old;
                    old.Dispose();
                    bool slit = e.Slit && now < focusUntil && !surprised;
                    float grow = surprised ? 0.06f : 0;
                    float pw = slit ? w * 0.17f : w * (e.Pupil + grow), ph = slit ? h * 0.82f : h * (e.Pupil + 0.08f + grow);
                    MascotParts.Ellipse(g, Color.FromArgb(250, 10, 10, 14), x - pw / 2, y - ph / 2 + h * 0.03f, pw, ph);
                }
                MascotParts.Stroke(g, ep, Color.FromArgb(150, ink), Math.Max(0.8f, D * 0.006f));
            }
            if (e.Lid)
                using (Pen lid = new Pen(e.LidColor, Math.Max(1.2f, D * 0.014f)))
                {
                    lid.StartCap = LineCap.Round;
                    lid.EndCap = LineCap.Round;
                    g.DrawArc(lid, r.X - w * 0.07f, r.Y - h * 0.01f, w * 1.14f, h * 0.92f, 196, 148);
                }
            if (blink < 0.5) return;
            MascotParts.Ellipse(g, Color.FromArgb(248, 255, 255, 255), x - w * 0.36f, y - h * 0.38f, w * 0.42f, h * 0.34f);
            MascotParts.Ellipse(g, Color.FromArgb(200, 255, 255, 255), x + w * 0.12f, y + h * 0.12f, w * 0.16f, w * 0.16f);
        }
    }
}
