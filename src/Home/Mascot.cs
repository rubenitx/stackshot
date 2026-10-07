// Stackshot - La mascota: un robotito flotante con visera y ojos de luz que sigue al ratón y reacciona.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Todo se dibuja a mano (sin imágenes) y con física sencilla: flota, parpadea, sigue al ratón con la mirada
    // (aunque esté fuera de la ventana), se aplasta y salta al tocarla, saluda con la mano, se duerme si nadie le
    // hace caso y se marea si se le hace demasiado caso. La ventana le da el tiempo y el ratón en cada fotograma.
    public class Mascot
    {
        public enum Mood { Idle, Happy, Surprised, Sleep, Dizzy, Wink, Love }

        // Colores del cuerpo (claro, oscuro) y nombre de cada uno.
        public static readonly Color[,] Bodies =
        {
            { Color.FromArgb(150, 110, 255), Color.FromArgb(40, 150, 245) },   // Aurora (la marca)
            { Color.FromArgb(52, 211, 153), Color.FromArgb(6, 152, 200) },     // Menta
            { Color.FromArgb(255, 128, 140), Color.FromArgb(245, 140, 30) },   // Coral
            { Color.FromArgb(196, 160, 255), Color.FromArgb(236, 72, 153) },   // Lavanda
            { Color.FromArgb(96, 165, 250), Color.FromArgb(37, 70, 235) },     // Océano
            { Color.FromArgb(250, 204, 21), Color.FromArgb(249, 115, 22) },    // Sol
            { Color.FromArgb(190, 242, 100), Color.FromArgb(22, 163, 74) },    // Lima
            { Color.FromArgb(160, 166, 180), Color.FromArgb(70, 76, 92) }      // Grafito
        };
        public static readonly string[] BodyNames = { "Aurora", "Menta", "Coral", "Lavanda", "Oc\u00E9ano", "Sol", "Lima", "Grafito" };

        static readonly Color EyeCore = Color.FromArgb(236, 254, 255);
        static readonly Color EyeGlow = Color.FromArgb(103, 232, 249);
        static readonly Color LoveColor = Color.FromArgb(255, 92, 140);

        class Particle
        {
            public char Kind;          // h = corazón, s = estrella, z = zeta, p = chispa
            public double X, Y, Vx, Vy, Age, Life, Rot, Vr, Size;
        }

        public int Hue = 0;
        public string Bubble;          // lo que dice ahora mismo (null = nada)
        public double BubbleAlpha;     // para que el bocadillo entre y salga con fundido
        public RectangleF Box;         // dónde se dibuja, en coordenadas de la ventana (lo pone quien la pinta)

        readonly Random rnd = new Random();
        readonly List<Particle> parts = new List<Particle>();
        Mood mood = Mood.Idle;
        double now, last, moodUntil, bubbleUntil, nextBlink, blinkStart = -1, waveUntil, lastPoke, lastSeen, lastSpark;
        double lookX, lookY, tilt, tiltV, antenna, antennaV, squashX = 1, squashXV, squashY = 1, squashYV, jump, jumpV;
        double spin, spinV, appear = 1, pulse;
        int pokes;
        bool hovering;

        public Mascot()
        {
            nextBlink = 1800;
        }

        public Mood Current { get { return mood; } }
        public bool Sleeping { get { return mood == Mood.Sleep; } }

        // Haciendo algo más que flotar (la ventana sube los fotogramas solo cuando hace falta).
        public bool Lively
        {
            get
            {
                return parts.Count > 0 || (mood != Mood.Idle && mood != Mood.Sleep) || blinkStart >= 0 || now < waveUntil ||
                       Math.Abs(jumpV) > 0.15 || Math.Abs(squashXV) > 0.15 || Math.Abs(squashYV) > 0.15 || Math.Abs(antennaV) > 60 ||
                       appear < 0.99 || (BubbleAlpha > 0.02 && BubbleAlpha < 0.98);
            }
        }

        // ------------------------------------------------------------ Reacciones

        public void Say(string text, double ms)
        {
            now = Anim.Now;
            Bubble = text;
            bubbleUntil = now + ms;
        }

        void SetMood(Mood m, double ms)
        {
            now = Anim.Now;
            mood = m;
            moodUntil = now + ms;
        }

        // Aparece de golpe con un rebote (al abrir la ventana).
        public void PopIn()
        {
            appear = 0.01;
            squashX = 0.6; squashY = 1.35;
            jumpV = -2.2;
        }

        public void Greet(string text)
        {
            now = Anim.Now;
            Wake();
            SetMood(Mood.Happy, 2200);
            waveUntil = now + 1800;
            jumpV -= 1.2;
            if (text != null) Say(text, 4200);
        }

        public void Celebrate(string text)
        {
            now = Anim.Now;
            Wake();
            SetMood(Mood.Surprised, 420);
            squashX = 1.18; squashY = 0.84;
            jumpV -= 1.8;
            for (int i = 0; i < 10; i++) Spawn('p', 0, -0.05, 0.9);
            if (text != null) Say(text, 2600);
            pendingHappy = true;
        }
        bool pendingHappy;

        // Un clic encima: cada vez una cosa distinta; muchos seguidos, se marea.
        public void Poke(string[] lines)
        {
            now = Anim.Now;
            Wake();
            pokes = now - lastPoke < 1200 ? pokes + 1 : 1;
            lastPoke = now;
            squashX = 1.22; squashY = 0.8;
            antennaV += 900;
            if (pokes >= 5)
            {
                pokes = 0;
                SetMood(Mood.Dizzy, 2600);
                spinV = 900;
                for (int i = 0; i < 6; i++) Spawn('s', 0, -0.32, 0.6);
                Say("\u00A1Qu\u00E9 mareo! \u00BFPor qu\u00E9 hay tres ratones?", 2600);
                return;
            }
            switch (rnd.Next(4))
            {
                case 0:
                    jumpV -= 2.6;
                    SetMood(Mood.Happy, 1400);
                    break;
                case 1:
                    SetMood(Mood.Love, 1800);
                    for (int i = 0; i < 5; i++) Spawn('h', 0, -0.2, 0.7);
                    break;
                case 2:
                    SetMood(Mood.Wink, 1200);
                    tiltV += 140;
                    break;
                default:
                    SetMood(Mood.Surprised, 700);
                    jumpV -= 1.4;
                    break;
            }
            if (lines != null && lines.Length > 0) Say(lines[rnd.Next(lines.Length)], 2600);
        }

        public void Hover(bool on)
        {
            if (on == hovering) return;
            hovering = on;
            if (on) { Wake(); antennaV += 260; }
        }

        void Wake()
        {
            now = Anim.Now;
            lastSeen = now;
            if (mood == Mood.Sleep) { mood = Mood.Idle; squashY = 1.12; jumpV -= 1; }
        }

        void Spawn(char kind, double x, double y, double speed)
        {
            Particle p = new Particle();
            p.Kind = kind;
            double a = -Math.PI / 2 + (rnd.NextDouble() - 0.5) * (kind == 'p' ? Math.PI * 2 : 1.6);
            double v = speed * (0.55 + rnd.NextDouble() * 0.6);
            p.X = x + (rnd.NextDouble() - 0.5) * 0.2;
            p.Y = y;
            p.Vx = Math.Cos(a) * v;
            p.Vy = Math.Sin(a) * v;
            p.Life = kind == 'z' ? 2200 : kind == 'p' ? 700 : 1300;
            p.Rot = rnd.NextDouble() * 360;
            p.Vr = (rnd.NextDouble() - 0.5) * 360;
            p.Size = kind == 'p' ? 0.035 + rnd.NextDouble() * 0.03 : 0.07 + rnd.NextDouble() * 0.04;
            parts.Add(p);
        }

        // ------------------------------------------------------------ Física

        // Avanza la animación. mouse: posición del ratón en coordenadas de la ventana; moved: si se ha movido.
        // Devuelve true si algo cambia y hay que repintar (casi siempre: flota).
        public void Step(double t, PointF mouse, bool moved)
        {
            now = t;
            double dt = Math.Min(0.05, Math.Max(0.001, (t - last) / 1000.0));
            if (last == 0) dt = 0.016;
            last = t;
            if (lastSeen == 0) lastSeen = t;
            if (moved) lastSeen = t;

            if (mood != Mood.Idle && mood != Mood.Sleep && now > moodUntil) mood = Mood.Idle;
            if (pendingHappy && mood == Mood.Idle) { pendingHappy = false; SetMood(Mood.Happy, 1500); }
            if (mood == Mood.Idle && now - lastSeen > 45000) { mood = Mood.Sleep; Bubble = null; }
            if (Bubble != null && now > bubbleUntil) Bubble = null;
            BubbleAlpha += ((Bubble != null ? 1 : 0) - BubbleAlpha) * (1 - Math.Exp(-dt / 0.09));
            if (Bubble == null && BubbleAlpha < 0.02) BubbleAlpha = 0;

            // Mirada: hacia el ratón, esté donde esté.
            float d = Math.Max(1, Box.Width);
            double cx = Box.X + d / 2, cy = Box.Y + d * 0.45;
            double tx = Math.Max(-1, Math.Min(1, (mouse.X - cx) / (d * 2.2)));
            double ty = Math.Max(-1, Math.Min(1, (mouse.Y - cy) / (d * 2.2)));
            if (mood == Mood.Sleep || mood == Mood.Dizzy) { tx = 0; ty = 0.3; }
            double follow = 1 - Math.Exp(-dt / 0.07);
            lookX += (tx - lookX) * follow;
            lookY += (ty - lookY) * follow;

            // Inclinación hacia donde mira; la antena se balancea con retraso.
            Anim.Spring(ref tilt, ref tiltV, lookX * 7, 90, 0.5, dt);
            Anim.Spring(ref antenna, ref antennaV, -tilt * 1.6, 60, 0.12, dt);
            Anim.Spring(ref squashX, ref squashXV, 1, 380, 0.28, dt);
            Anim.Spring(ref squashY, ref squashYV, 1, 380, 0.28, dt);
            Anim.Spring(ref jump, ref jumpV, 0, 120, 0.4, dt);
            if (mood == Mood.Dizzy) spin += spinV * dt; else Anim.Spring(ref spin, ref spinV, Math.Round(spin / 360) * 360, 60, 0.6, dt);
            appear += (1 - appear) * (1 - Math.Exp(-dt / 0.12));
            pulse = 0.5 + 0.5 * Math.Sin(now / 520.0);

            // Parpadeo cada pocos segundos (a veces doble).
            if (blinkStart < 0 && now > nextBlink)
            {
                blinkStart = now;
                nextBlink = now + 2200 + rnd.NextDouble() * 3600;
                if (rnd.NextDouble() < 0.2) nextBlink = now + 260;
            }
            if (blinkStart >= 0 && now - blinkStart > 150) blinkStart = -1;

            if (mood == Mood.Sleep && now - lastSpark > 1300) { lastSpark = now; Spawn('z', 0.18, -0.3, 0.16); }
            if (mood == Mood.Love && now - lastSpark > 380) { lastSpark = now; Spawn('h', 0, -0.25, 0.45); }
            if (mood == Mood.Dizzy && now - lastSpark > 500) { lastSpark = now; Spawn('s', 0, -0.36, 0.3); }

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Particle p = parts[i];
                p.Age += dt * 1000;
                if (p.Age > p.Life) { parts.RemoveAt(i); continue; }
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (p.Kind == 'p') { p.Vx *= 0.92; p.Vy *= 0.92; }
                else if (p.Kind == 'z') p.X += Math.Sin(p.Age / 300.0) * 0.002;
                else p.Vy += 0.12 * dt;
                p.Rot += p.Vr * dt;
            }
        }

        // ------------------------------------------------------------ Dibujo

        public void Paint(Graphics g)
        {
            RectangleF box = Box;
            float D = box.Width;
            if (D < 4) return;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Color c1 = Bodies[Math.Max(0, Math.Min(Bodies.GetLength(0) - 1, Hue)), 0];
            Color c2 = Bodies[Math.Max(0, Math.Min(Bodies.GetLength(0) - 1, Hue)), 1];

            double speed = mood == Mood.Sleep ? 4200.0 : 2600.0;
            float bob = (float)(Math.Sin(now / speed * Math.PI * 2) * D * 0.022);
            float jy = (float)(jump * D * 0.1);
            float cx = box.X + D / 2, cy = box.Y + D * 0.46f + bob + jy;
            float W = D * 0.72f, H = D * 0.58f;
            float scale = (float)Math.Max(0.01, appear);

            // Sombra en el suelo: más pequeña y tenue cuanto más alto está.
            float lift = Math.Max(0, -(bob + jy)) / D;
            float sw = D * 0.5f * (1 - lift * 1.6f) * scale, sh = D * 0.075f * (1 - lift * 1.2f) * scale;
            RectangleF shadow = new RectangleF(cx - sw / 2, box.Y + D * 0.9f - sh / 2, sw, sh);
            if (sw > 1 && sh > 1)
            {
                using (GraphicsPath sp = new GraphicsPath())
                {
                    sp.AddEllipse(shadow);
                    using (PathGradientBrush pb = new PathGradientBrush(sp))
                    {
                        pb.CenterColor = System.Drawing.Color.FromArgb((int)(110 * (1 - lift)), 0, 0, 0);
                        pb.SurroundColors = new Color[] { System.Drawing.Color.FromArgb(0, 0, 0, 0) };
                        g.FillEllipse(pb, shadow);
                    }
                }
            }

            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(tilt + spin % 360));
            g.ScaleTransform((float)squashX * scale, (float)squashY * scale);

            PaintHands(g, D, W, H, c1, c2);
            PaintAntenna(g, D, H, c1, c2);

            // Orejeras a los lados.
            foreach (int side in new int[] { -1, 1 })
            {
                RectangleF ear = new RectangleF(side < 0 ? -W / 2 - D * 0.05f : W / 2 - D * 0.02f, -D * 0.1f, D * 0.07f, D * 0.2f);
                using (GraphicsPath ep = Theme.Round(ear, D * 0.035f))
                using (LinearGradientBrush eb = new LinearGradientBrush(ear, Mac.Mix(c2, System.Drawing.Color.Black, 0.25), Mac.Mix(c2, System.Drawing.Color.Black, 0.5), 90f))
                    g.FillPath(eb, ep);
            }

            // La cabeza: degradado del color elegido, brillo arriba y un filo de luz.
            RectangleF head = new RectangleF(-W / 2, -H / 2, W, H);
            using (GraphicsPath hp = Theme.Round(head, H * 0.42f))
            {
                using (LinearGradientBrush hb = new LinearGradientBrush(RectangleF.Inflate(head, 1, 1), c1, c2, 55f)) g.FillPath(hb, hp);
                Region old = g.Clip;
                g.SetClip(hp, CombineMode.Intersect);
                RectangleF shine = new RectangleF(-W * 0.46f, -H * 0.62f, W * 0.8f, H * 0.6f);
                using (GraphicsPath shp = new GraphicsPath())
                {
                    shp.AddEllipse(shine);
                    using (PathGradientBrush pb = new PathGradientBrush(shp))
                    {
                        pb.CenterColor = System.Drawing.Color.FromArgb(110, 255, 255, 255);
                        pb.SurroundColors = new Color[] { System.Drawing.Color.FromArgb(0, 255, 255, 255) };
                        g.FillEllipse(pb, shine);
                    }
                }
                RectangleF bottom = new RectangleF(-W / 2, H * 0.1f, W, H * 0.42f);
                using (LinearGradientBrush bb = new LinearGradientBrush(RectangleF.Inflate(bottom, 0, 1), System.Drawing.Color.FromArgb(0, 0, 0, 0), System.Drawing.Color.FromArgb(60, 0, 0, 0), 90f))
                    g.FillRectangle(bb, bottom);
                g.Clip = old;
                old.Dispose();
                using (Pen rim = new Pen(System.Drawing.Color.FromArgb(70, 255, 255, 255), Math.Max(1f, D * 0.008f))) g.DrawPath(rim, hp);
            }

            // Visera oscura con un reflejo de cristal.
            float vw = W * 0.8f, vh = H * 0.6f;
            RectangleF visor = new RectangleF(-vw / 2, -vh / 2 - D * 0.012f, vw, vh);
            using (GraphicsPath vp = Theme.Round(visor, vh * 0.42f))
            {
                using (LinearGradientBrush vb = new LinearGradientBrush(RectangleF.Inflate(visor, 1, 1), System.Drawing.Color.FromArgb(14, 16, 30), System.Drawing.Color.FromArgb(24, 28, 50), 90f))
                    g.FillPath(vb, vp);
                Region old = g.Clip;
                g.SetClip(vp, CombineMode.Intersect);
                using (GraphicsPath glass = new GraphicsPath())
                {
                    glass.AddPolygon(new PointF[] { new PointF(-vw * 0.2f, -vh), new PointF(vw * 0.02f, -vh), new PointF(-vw * 0.3f, vh), new PointF(-vw * 0.52f, vh) });
                    using (SolidBrush gb = new SolidBrush(System.Drawing.Color.FromArgb(12, 255, 255, 255))) g.FillPath(gb, glass);
                }
                PaintFace(g, D, vw, vh, visor);
                g.Clip = old;
                old.Dispose();
                using (Pen vr = new Pen(System.Drawing.Color.FromArgb(40, 255, 255, 255), Math.Max(1f, D * 0.006f))) g.DrawPath(vr, vp);
            }
            if (mood == Mood.Happy || mood == Mood.Love || mood == Mood.Wink)
            {
                foreach (int side in new int[] { -1, 1 })
                {
                    RectangleF ch = new RectangleF(side * W * 0.31f - D * 0.04f, H * 0.2f, D * 0.08f, D * 0.04f);
                    using (SolidBrush cb = new SolidBrush(System.Drawing.Color.FromArgb(120, 255, 120, 160))) g.FillEllipse(cb, ch);
                }
            }
            g.Restore(st);
            PaintParticles(g, box);
        }

        void PaintAntenna(Graphics g, float D, float H, Color c1, Color c2)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0, -H / 2 + D * 0.01f);
            g.RotateTransform((float)antenna);
            float len = D * 0.15f;
            using (Pen p = new Pen(Mac.Mix(c2, System.Drawing.Color.Black, 0.3), Math.Max(1.2f, D * 0.022f)))
            {
                p.StartCap = LineCap.Round;
                g.DrawLine(p, 0, 0, 0, -len);
            }
            float r = D * 0.042f, gr = r * (2.6f + (float)pulse * 0.6f);
            Color glow = Mac.Mix(c1, System.Drawing.Color.White, 0.35);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(-gr, -len - gr, gr * 2, gr * 2);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = System.Drawing.Color.FromArgb((int)(90 + 70 * pulse), glow);
                    pb.SurroundColors = new Color[] { System.Drawing.Color.FromArgb(0, glow) };
                    g.FillEllipse(pb, -gr, -len - gr, gr * 2, gr * 2);
                }
            }
            using (SolidBrush b = new SolidBrush(Mac.Mix(c1, System.Drawing.Color.White, 0.55))) g.FillEllipse(b, -r, -len - r, r * 2, r * 2);
            using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, -r * 0.45f, -len - r * 0.6f, r * 0.6f, r * 0.6f);
            g.Restore(st);
        }

        // Dos manitas que flotan a los lados; la derecha saluda.
        void PaintHands(Graphics g, float D, float W, float H, Color c1, Color c2)
        {
            foreach (int side in new int[] { -1, 1 })
            {
                double phase = side * 0.9;
                float hy = H * 0.42f + (float)(Math.Sin(now / 2600.0 * Math.PI * 2 + phase) * D * 0.025);
                float hx = side * (W / 2 + D * 0.085f);
                float rot = 0;
                if (side > 0 && now < waveUntil)
                {
                    double w = (waveUntil - now) / 1800.0;
                    hy -= (float)(D * 0.22 * Math.Min(1, w * 3));
                    rot = (float)(Math.Sin(now / 90.0) * 28);
                }
                GraphicsState st = g.Save();
                g.TranslateTransform(hx, hy);
                g.RotateTransform(rot);
                RectangleF hand = new RectangleF(-D * 0.05f, -D * 0.06f, D * 0.1f, D * 0.12f);
                using (GraphicsPath hp = Theme.Round(hand, D * 0.05f))
                using (LinearGradientBrush hb = new LinearGradientBrush(RectangleF.Inflate(hand, 1, 1), c1, c2, 60f))
                {
                    g.FillPath(hb, hp);
                    using (Pen rim = new Pen(System.Drawing.Color.FromArgb(60, 255, 255, 255), Math.Max(1f, D * 0.006f))) g.DrawPath(rim, hp);
                }
                g.Restore(st);
            }
        }

        // Los ojos (y la boca, si toca) según el humor.
        void PaintFace(Graphics g, float D, float vw, float vh, RectangleF visor)
        {
            float ex = (float)(lookX * vw * 0.13), ey = (float)(lookY * vh * 0.16) - D * 0.006f;
            float ew = D * 0.072f, eh = D * 0.145f, gap = vw * 0.2f;
            float blink = 1;
            if (blinkStart >= 0)
            {
                double p = (now - blinkStart) / 150.0;
                blink = (float)Math.Max(0.1, 1 - Math.Sin(Math.PI * Math.Min(1, p)) * 0.95);
            }
            Color core = mood == Mood.Love ? Mac.Mix(LoveColor, System.Drawing.Color.White, 0.25) : EyeCore;
            Color glow = mood == Mood.Love ? LoveColor : EyeGlow;
            float stroke = Math.Max(1.5f, D * 0.03f);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -gap : gap) + ex, y = ey;
                Glow(g, x, y, ew * 2.6f, eh * 1.5f, glow, mood == Mood.Sleep ? 40 : 95);
                Mood m = mood;
                if (m == Mood.Wink && i == 1) m = Mood.Sleep;
                switch (m)
                {
                    case Mood.Happy:
                    case Mood.Wink:
                        using (Pen p = new Pen(core, stroke))
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                            g.DrawArc(p, x - ew * 0.9f, y - eh * 0.15f, ew * 1.8f, eh * 0.75f, 200, 140);
                        }
                        break;
                    case Mood.Sleep:
                        using (Pen p = new Pen(Mac.Alpha(core, mood == Mood.Sleep ? 0.75 : 1), stroke))
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                            g.DrawArc(p, x - ew * 0.85f, y - eh * 0.3f, ew * 1.7f, eh * 0.5f, 20, 140);
                        }
                        break;
                    case Mood.Surprised:
                    {
                        float r = ew * 0.95f;
                        using (SolidBrush b = new SolidBrush(core)) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                        break;
                    }
                    case Mood.Dizzy:
                        using (Pen p = new Pen(core, stroke * 0.8f))
                        using (GraphicsPath sp = new GraphicsPath())
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                            List<PointF> pts = new List<PointF>();
                            double rot = now / 120.0 * (i == 0 ? 1 : -1);
                            for (int k = 0; k <= 30; k++)
                            {
                                double a = rot + k * 0.42, rr = ew * 0.12 + k * ew * 0.03;
                                pts.Add(new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr)));
                            }
                            sp.AddCurve(pts.ToArray());
                            g.DrawPath(p, sp);
                        }
                        break;
                    case Mood.Love:
                        using (GraphicsPath hp = Heart(x, y, ew * 2.1f))
                        using (SolidBrush b = new SolidBrush(core)) g.FillPath(b, hp);
                        break;
                    default:
                    {
                        float h = eh * blink;
                        using (GraphicsPath ep = Theme.Round(new RectangleF(x - ew / 2, y - h / 2, ew, h), ew / 2))
                        using (SolidBrush b = new SolidBrush(core)) g.FillPath(b, ep);
                        break;
                    }
                }
            }
            if (mood == Mood.Happy || mood == Mood.Love || (Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy))
            {
                // Sonrisita (y, mientras habla, la boca se mueve).
                float mw = D * 0.08f, mh = D * 0.045f;
                if (Bubble != null && mood == Mood.Idle) mh *= (float)(0.5 + 0.5 * Math.Abs(Math.Sin(now / 110.0)));
                using (Pen p = new Pen(Mac.Alpha(EyeCore, 0.9), Math.Max(1.2f, D * 0.018f)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, ex * 0.6f - mw / 2, ey + eh * 0.42f, mw, mh, 20, 140);
                }
            }
            else if (mood == Mood.Surprised || mood == Mood.Dizzy)
            {
                float r = D * 0.022f;
                using (SolidBrush b = new SolidBrush(Mac.Alpha(EyeCore, 0.85))) g.FillEllipse(b, ex * 0.6f - r, ey + eh * 0.55f, r * 2, r * 2.4f);
            }
        }

        static void Glow(Graphics g, float x, float y, float w, float h, Color c, int alpha)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
                gp.AddEllipse(r);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = System.Drawing.Color.FromArgb(alpha, c);
                    pb.SurroundColors = new Color[] { System.Drawing.Color.FromArgb(0, c) };
                    g.FillEllipse(pb, r);
                }
            }
        }

        static GraphicsPath Heart(float x, float y, float size)
        {
            GraphicsPath p = new GraphicsPath();
            float s = size / 2;
            p.AddBezier(x, y + s * 0.85f, x - s * 1.25f, y - s * 0.05f, x - s * 0.55f, y - s * 0.95f, x, y - s * 0.3f);
            p.AddBezier(x, y - s * 0.3f, x + s * 0.55f, y - s * 0.95f, x + s * 1.25f, y - s * 0.05f, x, y + s * 0.85f);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath Star(float x, float y, float r)
        {
            GraphicsPath p = new GraphicsPath();
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : r * 0.45;
                pts[i] = new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr));
            }
            p.AddPolygon(pts);
            return p;
        }

        void PaintParticles(Graphics g, RectangleF box)
        {
            float D = box.Width;
            float cx = box.X + D / 2, cy = box.Y + D * 0.46f;
            foreach (Particle p in parts)
            {
                double life = p.Age / p.Life;
                int a = (int)(255 * Math.Min(1, Math.Min(life * 6, (1 - life) * 2.5)));
                if (a <= 0) continue;
                float x = cx + (float)(p.X * D), y = cy + (float)(p.Y * D), s = (float)(p.Size * D);
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)p.Rot * (p.Kind == 'z' ? 0.1f : 1));
                switch (p.Kind)
                {
                    case 'h':
                        using (GraphicsPath hp = Heart(0, 0, s))
                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(a, LoveColor))) g.FillPath(b, hp);
                        break;
                    case 's':
                        using (GraphicsPath sp = Star(0, 0, s * 0.6f))
                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(a, 255, 214, 10))) g.FillPath(b, sp);
                        break;
                    case 'z':
                        using (Font f = new Font("Segoe UI", Math.Max(6f, s * 1.2f), FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(a * 3 / 4, 200, 210, 255)))
                            g.DrawString("z", f, b, -s * 0.4f, -s * 0.7f);
                        break;
                    default:
                    {
                        Color c = Bodies[Math.Max(0, Math.Min(Bodies.GetLength(0) - 1, Hue)), (int)(p.Rot) % 2 == 0 ? 0 : 1];
                        using (GraphicsPath sp = Star(0, 0, s))
                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(a, Mac.Mix(c, System.Drawing.Color.White, 0.4)))) g.FillPath(b, sp);
                        break;
                    }
                }
                g.Restore(st);
            }
        }

        // Zona que puede ocupar al dibujarse (con manos, antena, partículas y saltos), para repintar solo eso.
        public RectangleF PaintBounds
        {
            get
            {
                RectangleF r = Box;
                r.Inflate(Box.Width * 0.45f, Box.Width * 0.6f);
                return r;
            }
        }
    }
}
