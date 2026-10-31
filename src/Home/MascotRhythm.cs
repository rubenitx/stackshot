// Stackshot - The mascot follows the time of day: a coffee now and then in the morning, its nightcap and yawns late at
// night, a little party on Friday afternoons; and a camera flash at each capture. Only within what it may do alone, and
// all of it off with "Según la hora del día".
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public partial class Mascot
    {
        public bool Rhythm = true;     // follows the time of day (Mascota > Ajustes > Comportamiento)
        bool still;                    // a still render for pickers: shows the look itself
        bool tailLeft;                 // the side its tail trails on, kept from the last walk
        bool faceLeft;                 // the way it last walked, for characters that turn to face it
        bool flipped;                  // drawn mirrored this frame (so looking around stays the right way)
        double flashUntil;
        const double FlashMs = 380;

        // Sometimes, instead of a random pick, what suits the hour. True when it did something.
        bool RhythmFidget()
        {
            DateTime d = DateTime.Now;
            int h = d.Hour;
            if (h >= 7 && h < 10 && (Alone & MascotCmd.AloneProps) != 0 && lastAlone != MascotCmd.Coffee && rnd.NextDouble() < 0.35)
            {
                Remember(MascotCmd.Coffee);
                Run(MascotCmd.Coffee, false);
                if (Talks && Chatter > 0) Say(rnd.NextDouble() < 0.5 ? I18n.T("Un cafecito para empezar.", "A little coffee to get going.") : I18n.T("\u00A1Buenos d\u00EDas! Primero, caf\u00E9.", "Good morning! Coffee first."), 2600);
                return true;
            }
            if ((h >= 22 || h < 6) && (Alone & MascotCmd.AloneSmall) != 0 && lastAlone != AloneWink && rnd.NextDouble() < 0.35)
            {
                Remember(AloneWink);
                SetMood(Mood.Yawn, 1500);
                if (Talks && Chatter > 0 && rnd.NextDouble() < 0.4) Say(I18n.T("Qu\u00E9 tarde es ya\u2026", "It's getting so late\u2026"), 2400);
                return true;
            }
            if (d.DayOfWeek == DayOfWeek.Friday && h >= 15 && h < 21 && (Alone & MascotCmd.AloneSmall) != 0 && lastAlone != MascotCmd.Cheer && rnd.NextDouble() < 0.15)
            {
                Remember(MascotCmd.Cheer);
                Run(MascotCmd.Cheer, false);
                if (Talks && Chatter > 0) Say(I18n.T("\u00A1Viernes!", "Friday!"), 2200);
                return true;
            }
            return false;
        }

        void Remember(int cmd)
        {
            prevAlone = lastAlone;
            lastAlone = cmd;
        }

        // The flash of a capture: a soft white burst over it that fades in a moment.
        void PaintFlash(Graphics g, RectangleF box)
        {
            if (now >= flashUntil) return;
            double k = (flashUntil - now) / FlashMs;
            float D = box.Width, cx = box.X + D / 2, cy = box.Y + D * 0.42f;
            MascotParts.Glow(g, cx, cy, D * 1.3f, D * 1.15f, Color.White, (int)(170 * k * k));
        }
    }

    public static partial class MascotParts
    {
        public const int HatNightcap = 45;
        public static bool NightCaps = true;   // the owners keep it in step with Settings.MascotRhythm

        // A soft nightcap flopping to one side, with a white band and a pompom that sways.
        static void NightCap(Graphics g, float u, double now)
        {
            float sway = (float)Math.Sin(now / 900.0) * 0.03f * u;
            PointF tip = P(0.5f * u + sway, -0.34f * u);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(P(-0.42f * u, -0.02f * u), P(-0.36f * u, -0.44f * u), P(0.1f * u, -0.62f * u), tip);
                p.AddBezier(tip, P(0.26f * u, -0.34f * u), P(0.4f * u, -0.14f * u), P(0.42f * u, -0.02f * u));
                p.CloseFigure();
                RectangleF r = new RectangleF(-0.45f * u, -0.62f * u, 0.97f * u, 0.62f * u);
                Fill(g, p, r, Color.FromArgb(132, 150, 236), Color.FromArgb(74, 88, 186), 60f);
                Shine(g, p, r, 80);
            }
            // Little stars on it.
            foreach (PointF s in new PointF[] { P(-0.18f * u, -0.2f * u), P(0.06f * u, -0.38f * u), P(0.2f * u, -0.18f * u) })
                using (GraphicsPath sp = Star(s.X, s.Y, 0.035f * u, 0.45f)) FillSolid(g, sp, Color.FromArgb(230, 255, 236, 140));
            RectangleF band = new RectangleF(-0.47f * u, -0.09f * u, 0.94f * u, 0.14f * u);
            using (GraphicsPath bp = Theme.Round(band, 0.07f * u)) Fill(g, bp, band, Color.FromArgb(252, 252, 255), Color.FromArgb(212, 216, 234), 90f);
            float r2 = 0.085f * u;
            Ellipse(g, Color.FromArgb(252, 252, 255), tip.X - r2, tip.Y - r2, r2 * 2, r2 * 2);
            Ellipse(g, Color.FromArgb(120, 200, 205, 225), tip.X - r2 * 0.3f, tip.Y, r2 * 1.1f, r2 * 0.9f);
        }
    }
}
