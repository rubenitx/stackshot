// Stackshot - The logo, as one parametric drawing: used by the app (launch animation, sidebar) and by
// tools\make-logo.ps1 to render the icon and PNGs. Depends only on System.Drawing.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // A deep violet-to-blue squircle; inside, a frosted card tilted behind (the stack) and two thick viewfinder
    // corners on the diagonal around a glowing lens (the shot). Each part has a 0-1 progress so the launch animation
    // can grow it from a single dot: the lens splits into the corners, the tile and the card appear behind.
    public static class LogoArt
    {
        public static readonly Color Bg1 = Color.FromArgb(124, 58, 237), Bg2 = Color.FromArgb(67, 56, 202), Bg3 = Color.FromArgb(14, 165, 233);
        public static readonly Color Lens1 = Color.FromArgb(103, 232, 249), Lens2 = Color.FromArgb(196, 181, 253);

        public static void PaintFull(Graphics g, RectangleF box, bool small)
        {
            Paint(g, box, 1, 1, 1, 1, small);
        }

        // tile, glass, frame, lens: progress of each part (0 hidden, 1 final; values above 1 overshoot).
        public static void Paint(Graphics g, RectangleF box, double tile, double glass, double frame, double lens, bool small)
        {
            float k = box.Width / 1024f, cx = box.X + box.Width / 2, cy = box.Y + box.Height / 2;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (tile > 0.001)
            {
                float half = 488 * k * (float)(0.82 + 0.18 * tile);
                int a = (int)(255 * Math.Min(1, tile * 1.6));
                using (GraphicsPath body = Squircle(cx, cy, half, 5))
                {
                    RectangleF b = new RectangleF(cx - half, cy - half, half * 2, half * 2);
                    using (LinearGradientBrush bg = new LinearGradientBrush(new PointF(b.Left, b.Top), new PointF(b.Right, b.Bottom), Color.White, Color.White))
                    {
                        ColorBlend cb = new ColorBlend();
                        cb.Colors = new Color[] { Color.FromArgb(a, Bg1), Color.FromArgb(a, Bg2), Color.FromArgb(a, Bg3) };
                        cb.Positions = new float[] { 0f, 0.55f, 1f };
                        bg.InterpolationColors = cb;
                        g.FillPath(bg, body);
                    }
                    Region old = g.Clip;
                    g.SetClip(body, CombineMode.Intersect);
                    Glow(g, b.Left + b.Width * 0.22f, b.Top + b.Height * 0.12f, b.Width * 1.1f, Color.FromArgb(a * 60 / 255, 255, 255, 255));
                    Glow(g, b.Right - b.Width * 0.1f, b.Bottom - b.Height * 0.05f, b.Width * 0.9f, Color.FromArgb(a * 90 / 255, Lens1));
                    g.Clip = old;
                    old.Dispose();
                    if (!small)
                        using (LinearGradientBrush rim = new LinearGradientBrush(new PointF(0, b.Top), new PointF(0, b.Bottom), Color.White, Color.White))
                        {
                            ColorBlend cb = new ColorBlend();
                            cb.Colors = new Color[] { Color.FromArgb(a * 120 / 255, 255, 255, 255), Color.FromArgb(a * 14 / 255, 255, 255, 255), Color.FromArgb(a * 40 / 255, 255, 255, 255) };
                            cb.Positions = new float[] { 0f, 0.4f, 1f };
                            rim.InterpolationColors = cb;
                            using (Pen p = new Pen(rim, Math.Max(1f, 7 * k))) g.DrawPath(p, body);
                        }
                }
            }

            if (glass > 0.001 && !small)
            {
                GraphicsState gs = g.Save();
                g.TranslateTransform(cx + 26 * k, cy - 34 * k + (float)(70 * k * (1 - glass)));
                g.RotateTransform((float)(-9 * Math.Min(1, glass)));
                RectangleF card = new RectangleF(-300 * k, -214 * k, 600 * k, 428 * k);
                int a = (int)(255 * Math.Min(1, glass));
                using (GraphicsPath p = Round(card, 74 * k))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new PointF(0, card.Top), new PointF(0, card.Bottom), Color.FromArgb(a * 60 / 255, 255, 255, 255), Color.FromArgb(a * 18 / 255, 255, 255, 255)))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(a * 70 / 255, 255, 255, 255), Math.Max(1f, 5 * k))) g.DrawPath(pen, p);
                }
                g.Restore(gs);
            }

            if (frame > 0.001)
            {
                // The corners travel from the lens to their place; the arms grow as they go.
                float e = (float)frame;
                float arm = 196 * k * Math.Min(1f, 0.25f + 0.75f * e), wPen = (small ? 104 : 80) * k;
                float off = 238 * k * e;
                PointF tl = new PointF(cx - off, cy - off * 0.78f), br = new PointF(cx + off, cy + off * 0.78f);
                int a = (int)(255 * Math.Min(1, frame * 3));
                using (Pen pen = new Pen(Color.FromArgb(a, 255, 255, 255), wPen))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    if (!small)
                        using (Pen shade = new Pen(Color.FromArgb(a * 50 / 255, 20, 10, 60), wPen))
                        {
                            shade.StartCap = LineCap.Round; shade.EndCap = LineCap.Round; shade.LineJoin = LineJoin.Round;
                            g.DrawLines(shade, Corner(tl, arm, 1, 10 * k));
                            g.DrawLines(shade, Corner(br, arm, -1, 10 * k));
                        }
                    g.DrawLines(pen, Corner(tl, arm, 1, 0));
                    g.DrawLines(pen, Corner(br, arm, -1, 0));
                }
            }

            if (lens > 0.001)
            {
                float r = (small ? 78 : 64) * k * (float)lens;
                if (!small) Glow(g, cx, cy, r * 6, Color.FromArgb((int)(120 * Math.Min(1, lens)), Lens1));
                using (GraphicsPath p = new GraphicsPath())
                {
                    RectangleF d = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                    p.AddEllipse(d);
                    using (LinearGradientBrush b = new LinearGradientBrush(RectangleF.Inflate(d, 1, 1), Color.White, Lens1, 60f)) g.FillPath(b, p);
                    if (!small) using (Pen ring = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1f, 8 * k))) g.DrawPath(ring, p);
                }
                if (!small) Glow(g, cx - r * 0.35f, cy - r * 0.38f, r * 0.9f, Color.FromArgb(230, 255, 255, 255));
            }
            g.Restore(st);
        }

        // An L-shaped viewfinder corner; dir 1 opens to the bottom-right (top-left corner), -1 the opposite.
        static PointF[] Corner(PointF c, float arm, int dir, float dy)
        {
            return new PointF[] { new PointF(c.X, c.Y + dir * arm + dy), new PointF(c.X, c.Y + dy), new PointF(c.X + dir * arm, c.Y + dy) };
        }

        public static void Glow(Graphics g, float x, float y, float d, Color c)
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

        // Superellipse |x|^n + |y|^n = 1: continuous-curvature corners, the shape of app icons.
        public static GraphicsPath Squircle(float cx, float cy, float half, double n)
        {
            PointF[] pts = new PointF[240];
            for (int i = 0; i < pts.Length; i++)
            {
                double t = i * Math.PI * 2 / pts.Length, c = Math.Cos(t), s = Math.Sin(t);
                pts[i] = new PointF(cx + half * (float)(Math.Sign(c) * Math.Pow(Math.Abs(c), 2 / n)),
                                    cy + half * (float)(Math.Sign(s) * Math.Pow(Math.Abs(s), 2 / n)));
            }
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
