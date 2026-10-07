// Stackshot - Animación de bienvenida al abrir la ventana: el logo se monta en el aire, dispara y vuela a su sitio.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Stackshot
{
    // Unos dos segundos, por fases:
    //   1. Fondo oscuro con auroras de los colores de la marca.
    //   2. Las tres tarjetas del logo llegan volando y girando desde abajo, cada una con su muelle.
    //   3. El cuadrado del logo florece detrás y aparecen las esquinas del visor.
    //   4. ¡Clic!: destello, onda y una lluvia de chispas; el nombre aparece debajo con un brillo que lo recorre.
    //   5. El logo se encoge y vuela hasta su sitio en la barra lateral mientras el fondo se desvanece.
    // Un clic o una tecla la saltan.
    public class Intro
    {
        public const double Length = 2150;
        public const double RevealAt = 1580;   // a partir de aquí se ve la ventana debajo

        class Spark { public double X, Y, Vx, Vy, Size, Life; public Color C; }

        readonly double start;
        readonly List<Spark> sparks = new List<Spark>();
        readonly Random rnd = new Random();
        Bitmap aurora;
        bool burst;
        public PointF Target;          // centro del logo de la barra lateral (adonde vuela)
        public float TargetSize;       // y su tamaño

        public Intro(double now)
        {
            start = now;
        }

        public double T(double now) { return now - start; }
        public bool Done(double now) { return now - start >= Length; }

        static double Clamp(double v) { return Math.Max(0, Math.Min(1, v)); }
        static double Phase(double t, double a, double b) { return Clamp((t - a) / (b - a)); }
        static double OutCubic(double t) { double u = 1 - t; return 1 - u * u * u; }
        static double InOutCubic(double t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; }
        static double OutBack(double t, double k) { double u = t - 1; return 1 + (k + 1) * u * u * u + k * u * u; }
        // Muelle ya resuelto: llega con un par de rebotes.
        static double Spring(double t) { return t >= 1 ? 1 : 1 - Math.Exp(-6.5 * t) * Math.Cos(10.5 * t); }

        public void Paint(Graphics g, Rectangle client, double now, float s)
        {
            double t = now - start;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            // Al final todo se desvanece y deja ver la ventana.
            double fade = 1 - InOutCubic(Phase(t, RevealAt, Length - 120));
            if (fade <= 0.001 && t > RevealAt) return;
            int bgA = (int)(255 * fade);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(bgA, 8, 8, 13))) g.FillRectangle(bg, client);
            PaintAurora(g, client, t, fade, s);

            float cx = client.Width / 2f, cy = client.Height / 2f - 34 * s;
            float L = 168 * s;

            // Vuelo final hacia la barra lateral.
            double fly = InOutCubic(Phase(t, RevealAt - 60, Length - 160));
            float k = (float)(1 + (TargetSize / L - 1) * fly);
            float lx = (float)(cx + (Target.X - cx) * fly), ly = (float)(cy + (Target.Y - cy) * fly);

            GraphicsState st = g.Save();
            g.TranslateTransform(lx, ly);
            g.ScaleTransform(k, k);

            // El cuadrado del logo florece detrás de las tarjetas.
            double bloom = Phase(t, 640, 1120);
            if (bloom > 0)
            {
                float bs = (float)(L * (0.35 + 0.65 * OutBack(bloom, 2.2)));
                PaintGlow(g, 0, 0, bs * 1.9f, Color.FromArgb((int)(120 * Clamp(bloom * 2) * (1 - fly * 0.7)), Mac.Brand2));
                RectangleF sq = new RectangleF(-bs / 2, -bs / 2, bs, bs);
                using (GraphicsPath p = Squircle(sq))
                {
                    using (LinearGradientBrush b = Mac.BrandBrush(sq)) g.FillPath(b, p);
                    RectangleF shine = new RectangleF(sq.X - bs * 0.1f, sq.Y - bs * 0.25f, bs * 0.9f, bs * 0.7f);
                    Region old = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    PaintGlow(g, shine.X + shine.Width / 2, shine.Y + shine.Height / 2, shine.Width, Color.FromArgb(70, 255, 255, 255));
                    g.Clip = old;
                    old.Dispose();
                    using (Pen rim = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, 1.4f * s))) g.DrawPath(rim, p);
                }
            }

            // Las tres tarjetas: llegan desde abajo y desde los lados, girando, y se colocan en abanico.
            float cw = L * 0.6f, ch = L * 0.44f;
            float[] endRot = { -13f, -6f, 0f };
            PointF[] endPos = { new PointF(-L * 0.04f, -L * 0.12f), new PointF(-L * 0.01f, -L * 0.03f), new PointF(L * 0.02f, L * 0.07f) };
            PointF[] from = { new PointF(-360 * s, 260 * s), new PointF(40 * s, 380 * s), new PointF(380 * s, 240 * s) };
            float[] fromRot = { -70f, 40f, 85f };
            for (int i = 0; i < 3; i++)
            {
                double p = Phase(t, 120 + i * 110, 820 + i * 110);
                if (p <= 0) continue;
                double e = Spring(p);
                float x = (float)(from[i].X + (endPos[i].X - from[i].X) * e);
                float y = (float)(from[i].Y + (endPos[i].Y - from[i].Y) * e);
                float rot = (float)(fromRot[i] + (endRot[i] - fromRot[i]) * e);
                float sc = (float)(0.5 + 0.5 * Clamp(p * 1.6));
                int a = (int)(255 * Clamp(p * 3));
                GraphicsState cs = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(rot);
                g.ScaleTransform(sc, sc);
                RectangleF card = new RectangleF(-cw / 2, -ch / 2, cw, ch);
                RectangleF shadow = card;
                shadow.Offset(0, 6 * s);
                using (GraphicsPath sp = Theme.Round(shadow, 9 * s))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(a / 4, 10, 10, 40))) g.FillPath(sb, sp);
                using (GraphicsPath cp = Theme.Round(card, 9 * s))
                {
                    Color top = Color.FromArgb(a, 255, 255, 255), bottom = Color.FromArgb(a, 226, 232, 255);
                    using (LinearGradientBrush cb = new LinearGradientBrush(RectangleF.Inflate(card, 1, 1), top, bottom, 90f)) g.FillPath(cb, cp);
                }
                if (i == 2) PaintViewfinder(g, card, t, s);
                g.Restore(cs);
            }
            g.Restore(st);

            // ¡Clic! Destello, onda y chispas.
            if (t >= 1180 && !burst)
            {
                burst = true;
                Color[] cs = { Mac.Brand1, Mac.Brand2, Mac.Brand3, Color.White, Mac.Pink };
                for (int i = 0; i < 46; i++)
                {
                    Spark sp = new Spark();
                    double a = rnd.NextDouble() * Math.PI * 2, v = (240 + rnd.NextDouble() * 520) * s;
                    sp.X = cx; sp.Y = cy;
                    sp.Vx = Math.Cos(a) * v; sp.Vy = Math.Sin(a) * v;
                    sp.Size = (2 + rnd.NextDouble() * 4.5) * s;
                    sp.Life = 600 + rnd.NextDouble() * 500;
                    sp.C = cs[rnd.Next(cs.Length)];
                    sparks.Add(sp);
                }
            }
            double flash = Phase(t, 1180, 1560);
            if (flash > 0 && flash < 1)
            {
                float maxR = (float)Math.Sqrt(client.Width * client.Width + client.Height * client.Height);
                float r = (float)(L * 0.5 + maxR * OutCubic(flash));
                int fa = (int)(150 * (1 - flash) * (1 - flash));
                PaintGlow(g, cx, cy, r * 2, Color.FromArgb(fa, 255, 255, 255));
                using (Pen ring = new Pen(Color.FromArgb((int)(180 * (1 - flash)), 200, 220, 255), (float)(3 * s * (1 - flash) + 0.5)))
                    g.DrawEllipse(ring, cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f);
            }
            if (sparks.Count > 0)
            {
                double st0 = t - 1180;
                foreach (Spark sp in sparks)
                {
                    double life = st0 / sp.Life;
                    if (life >= 1) continue;
                    double d = st0 / 1000.0;
                    double drag = (1 - Math.Exp(-2.8 * d)) / 2.8;
                    float x = (float)(sp.X + sp.Vx * drag), y = (float)(sp.Y + sp.Vy * drag + 90 * s * d * d);
                    float sz = (float)(sp.Size * (1 - life * 0.6));
                    int a = (int)(255 * (1 - life) * fade);
                    PaintGlow(g, x, y, sz * 5, Color.FromArgb(a / 3, sp.C));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, Mac.Mix(sp.C, Color.White, 0.4)))) g.FillEllipse(b, x - sz / 2, y - sz / 2, sz, sz);
                }
            }

            // El nombre, con un brillo que lo recorre de izquierda a derecha.
            double name = Phase(t, 1200, 1500) * (1 - Phase(t, RevealAt - 80, RevealAt + 180));
            if (name > 0)
            {
                using (Font f = new Font(Fonts.DisplaySemibold, 40 * s, GraphicsUnit.Pixel))
                using (Font f2 = new Font(Mac.TextFont, 15 * s, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    float ny = cy + L * 0.62f + (float)(14 * s * (1 - OutCubic(name)));
                    RectangleF nr = new RectangleF(0, ny, client.Width, 60 * s);
                    SizeF ns = g.MeasureString("Stackshot", f);
                    float shine = (float)(Phase(t, 1300, 1750));
                    float sx = client.Width / 2f - ns.Width / 2 + (ns.Width + 200 * s) * shine - 100 * s;
                    using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(sx - 90 * s, ny, 180 * s, 50 * s), Color.White, Color.White, 0f))
                    {
                        ColorBlend cb = new ColorBlend();
                        int a = (int)(255 * name);
                        cb.Colors = new Color[] { Color.FromArgb(a, 236, 238, 255), Color.FromArgb(a, 160, 210, 255), Color.FromArgb(a, 236, 238, 255) };
                        cb.Positions = new float[] { 0f, 0.5f, 1f };
                        lb.InterpolationColors = cb;
                        lb.WrapMode = WrapMode.TileFlipX;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.DrawString("Stackshot", f, lb, nr, sf);
                    }
                    double tag = Phase(t, 1320, 1600) * (1 - Phase(t, RevealAt - 80, RevealAt + 180));
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(170 * tag), 200, 205, 225)))
                        g.DrawString("Captura, marca y comparte en segundos", f2, tb, new RectangleF(0, ny + 54 * s, client.Width, 30 * s), sf);
                }
            }
        }

        // Esquinas del visor sobre la tarjeta de delante y el punto del centro, que aparecen con un pequeño zoom.
        static void PaintViewfinder(Graphics g, RectangleF card, double t, float s)
        {
            double p = Phase(t, 900, 1180);
            if (p <= 0) return;
            float k = (float)(1.35 - 0.35 * OutBack(p, 1.6));
            int a = (int)(255 * Clamp(p * 2));
            float w = card.Width * 0.62f * k, h = card.Height * 0.6f * k, arm = Math.Min(w, h) * 0.3f;
            RectangleF r = new RectangleF(-w / 2, -h / 2, w, h);
            using (LinearGradientBrush lb = new LinearGradientBrush(RectangleF.Inflate(r, 2, 2), Color.FromArgb(a, 124, 92, 255), Color.FromArgb(a, 31, 168, 240), 45f))
            using (Pen p2 = new Pen(lb, Math.Max(1.5f, 3.4f * s)))
            {
                p2.StartCap = LineCap.Round; p2.EndCap = LineCap.Round; p2.LineJoin = LineJoin.Round;
                g.DrawLines(p2, new PointF[] { new PointF(r.Left, r.Top + arm), new PointF(r.Left, r.Top), new PointF(r.Left + arm, r.Top) });
                g.DrawLines(p2, new PointF[] { new PointF(r.Right - arm, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + arm) });
                g.DrawLines(p2, new PointF[] { new PointF(r.Right, r.Bottom - arm), new PointF(r.Right, r.Bottom), new PointF(r.Right - arm, r.Bottom) });
                g.DrawLines(p2, new PointF[] { new PointF(r.Left + arm, r.Bottom), new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Bottom - arm) });
                float d = Math.Min(w, h) * 0.18f;
                g.FillEllipse(lb, -d / 2, -d / 2, d, d);
            }
        }

        // Auroras: tres manchas de luz de colores que giran despacio. Se pintan una vez y luego solo se mueven.
        void PaintAurora(Graphics g, Rectangle client, double t, double fade, float s)
        {
            if (aurora == null)
            {
                int w = Math.Max(1, client.Width / 3), h = Math.Max(1, client.Height / 3);
                aurora = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics ag = Graphics.FromImage(aurora))
                {
                    ag.SmoothingMode = SmoothingMode.AntiAlias;
                    PaintGlow(ag, w * 0.3f, h * 0.35f, w * 0.75f, Color.FromArgb(150, Mac.Brand1));
                    PaintGlow(ag, w * 0.72f, h * 0.62f, w * 0.7f, Color.FromArgb(130, Mac.Brand3));
                    PaintGlow(ag, w * 0.55f, h * 0.25f, w * 0.5f, Color.FromArgb(110, Mac.Brand2));
                }
            }
            double a = Clamp(t / 500.0) * fade * 0.85;
            if (a <= 0.01) return;
            GraphicsState st = g.Save();
            float cx = client.Width / 2f, cy = client.Height / 2f;
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(t / 40.0));
            float sc = (float)(1.25 + 0.1 * Math.Sin(t / 600.0));
            g.ScaleTransform(sc, sc);
            using (ImageAttributes ia = new ImageAttributes())
            {
                ColorMatrix cm = new ColorMatrix();
                cm.Matrix33 = (float)a;
                ia.SetColorMatrix(cm);
                // Cuadrado de lado la diagonal: al girar sigue cubriendo las esquinas.
                int dd = (int)(Math.Sqrt(client.Width * (double)client.Width + client.Height * (double)client.Height) * 1.05);
                Rectangle dst = new Rectangle(-dd / 2, -dd / 2, dd, dd);
                g.DrawImage(aurora, dst, 0, 0, aurora.Width, aurora.Height, GraphicsUnit.Pixel, ia);
            }
            g.Restore(st);
        }

        public static void PaintGlow(Graphics g, float x, float y, float d, Color c)
        {
            if (d < 1 || c.A == 0) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                RectangleF r = new RectangleF(x - d / 2, y - d / 2, d, d);
                p.AddEllipse(r);
                using (PathGradientBrush b = new PathGradientBrush(p))
                {
                    b.CenterColor = c;
                    b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillEllipse(b, r);
                }
            }
        }

        // Superelipse (n = 5) como la del icono.
        public static GraphicsPath Squircle(RectangleF r)
        {
            GraphicsPath p = new GraphicsPath();
            const int n = 72;
            PointF[] pts = new PointF[n];
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, ax = r.Width / 2, ay = r.Height / 2;
            for (int i = 0; i < n; i++)
            {
                double a = i * Math.PI * 2 / n, c = Math.Cos(a), sn = Math.Sin(a);
                pts[i] = new PointF((float)(cx + ax * Math.Sign(c) * Math.Pow(Math.Abs(c), 0.4)), (float)(cy + ay * Math.Sign(sn) * Math.Pow(Math.Abs(sn), 0.4)));
            }
            p.AddPolygon(pts);
            return p;
        }

        public void Dispose()
        {
            if (aurora != null) { aurora.Dispose(); aurora = null; }
        }
    }
}
