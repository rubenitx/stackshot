// Stackshot - Main window and tray menu style: macOS-like colors and line icons.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Stackshot
{
    // macOS dark mode colors (neutral backgrounds, system blue) and the brand gradient.
    public static class Mac
    {
        public static readonly Color Window = Color.FromArgb(28, 28, 30);
        public static readonly Color Sidebar = Color.FromArgb(21, 21, 23);
        public static readonly Color Card = Color.FromArgb(39, 39, 42);
        public static readonly Color CardHover = Color.FromArgb(50, 50, 54);
        public static readonly Color Control = Color.FromArgb(58, 58, 62);
        public static readonly Color Separator = Color.FromArgb(52, 52, 56);
        public static readonly Color Menu = Color.FromArgb(36, 36, 39);
        public static readonly Color Text = Color.FromArgb(245, 245, 247);
        public static readonly Color Text2 = Color.FromArgb(152, 152, 159);
        public static readonly Color Text3 = Color.FromArgb(99, 99, 104);
        public static readonly Color Blue = Color.FromArgb(10, 132, 255);
        public static readonly Color Green = Color.FromArgb(48, 209, 88);
        public static readonly Color Red = Color.FromArgb(255, 69, 58);
        public static readonly Color Orange = Color.FromArgb(255, 159, 10);
        public static readonly Color Yellow = Color.FromArgb(255, 214, 10);
        public static readonly Color Purple = Color.FromArgb(191, 90, 242);
        public static readonly Color Pink = Color.FromArgb(255, 55, 95);
        public static readonly Color Teal = Color.FromArgb(100, 210, 255);
        public static readonly Color Indigo = Color.FromArgb(94, 92, 230);
        // Logo gradient: violet, blue and cyan.
        public static readonly Color Brand1 = Color.FromArgb(139, 92, 246);
        public static readonly Color Brand2 = Color.FromArgb(79, 123, 255);
        public static readonly Color Brand3 = Color.FromArgb(20, 184, 230);

        static string text;

        // Segoe UI Variable Text on Windows 11; Segoe UI on Windows 10.
        public static string TextFont
        {
            get
            {
                if (text != null) return text;
                text = "Segoe UI";
                if (Fonts.Has("Segoe UI Variable Text")) text = "Segoe UI Variable Text";
                return text;
            }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                                  (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, double a)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, (int)Math.Round(255 * a))), c);
        }

        // Brand gradient brush, diagonal over r.
        public static LinearGradientBrush BrandBrush(RectangleF r)
        {
            LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), Brand1, Brand3, 45f);
            ColorBlend cb = new ColorBlend();
            cb.Colors = new Color[] { Brand1, Brand2, Brand3 };
            cb.Positions = new float[] { 0f, 0.5f, 1f };
            b.InterpolationColors = cb;
            return b;
        }

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
    }

    // Hand-drawn line icons on a 24x24 grid (SF Symbols style). Crisp at any scale and independent of installed icon
    // fonts.
    public static class Icons
    {
        public static Bitmap Render(string name, int px, Color c)
        {
            Bitmap b = new Bitmap(Math.Max(1, px), Math.Max(1, px), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                Mac.Quality(g);
                Draw(g, name, new RectangleF(0, 0, px, px), c);
            }
            return b;
        }

        public static void Draw(Graphics g, string name, RectangleF r, Color c)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            float k = r.Width / 24f;
            g.ScaleTransform(k, k);
            float w = Math.Max(1.55f, 1.25f / k + 0.9f); // slightly thinner stroke at larger sizes
            using (Pen p = new Pen(c, w))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                Paint(g, name, p, b);
            }
            g.Restore(st);
        }

        static void RoundRect(Graphics g, Pen p, float x1, float y1, float x2, float y2, float r)
        {
            using (GraphicsPath gp = Theme.Round(RectangleF.FromLTRB(x1, y1, x2, y2), r)) g.DrawPath(p, gp);
        }

        static void Dot(Graphics g, Brush b, float x, float y, float r)
        {
            g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
        }

        static void Paint(Graphics g, string name, Pen p, SolidBrush b)
        {
            switch (name)
            {
                case "area":
                {
                    float a = 5.2f;
                    g.DrawLines(p, new PointF[] { new PointF(4, 4 + a), new PointF(4, 4), new PointF(4 + a, 4) });
                    g.DrawLines(p, new PointF[] { new PointF(20 - a, 4), new PointF(20, 4), new PointF(20, 4 + a) });
                    g.DrawLines(p, new PointF[] { new PointF(20, 20 - a), new PointF(20, 20), new PointF(20 - a, 20) });
                    g.DrawLines(p, new PointF[] { new PointF(4 + a, 20), new PointF(4, 20), new PointF(4, 20 - a) });
                    g.DrawLine(p, 12, 9.5f, 12, 14.5f);
                    g.DrawLine(p, 9.5f, 12, 14.5f, 12);
                    break;
                }
                case "screen":
                    RoundRect(g, p, 2.5f, 4, 21.5f, 16.5f, 2.2f);
                    g.DrawLine(p, 12, 16.5f, 12, 19.5f);
                    g.DrawLine(p, 8, 20, 16, 20);
                    break;
                case "window":
                    RoundRect(g, p, 2.5f, 4.5f, 21.5f, 19.5f, 2.6f);
                    g.DrawLine(p, 2.5f, 9, 21.5f, 9);
                    Dot(g, b, 5.6f, 6.8f, 0.85f);
                    Dot(g, b, 8.2f, 6.8f, 0.85f);
                    Dot(g, b, 10.8f, 6.8f, 0.85f);
                    break;
                case "scroll":
                    RoundRect(g, p, 3.5f, 2.5f, 15, 21.5f, 2.4f);
                    g.DrawLine(p, 6.8f, 7.5f, 11.8f, 7.5f);
                    g.DrawLine(p, 6.8f, 11.5f, 11.8f, 11.5f);
                    g.DrawLine(p, 6.8f, 15.5f, 10, 15.5f);
                    g.DrawLine(p, 19.5f, 5.5f, 19.5f, 18.5f);
                    g.DrawLines(p, new PointF[] { new PointF(17, 16), new PointF(19.5f, 18.6f), new PointF(22, 16) });
                    break;
                case "video":
                    RoundRect(g, p, 2, 6, 15.5f, 18, 2.6f);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddLines(new PointF[] { new PointF(15.5f, 10.3f), new PointF(21.5f, 7.2f), new PointF(21.5f, 16.8f), new PointF(15.5f, 13.7f) });
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    break;
                case "camera":
                    g.DrawEllipse(p, 5, 2.5f, 14, 14);
                    g.DrawEllipse(p, 9.5f, 7, 5, 5);
                    g.DrawLine(p, 12, 16.5f, 12, 20.5f);
                    g.DrawLine(p, 7.5f, 21, 16.5f, 21);
                    break;
                case "gif":
                    RoundRect(g, p, 2, 5, 22, 19, 3.2f);
                    using (Font f = new Font("Segoe UI", 7.6f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("GIF", f, b, new RectangleF(2, 5.4f, 20, 14), sf);
                    }
                    break;
                case "stop":
                    using (GraphicsPath gp = Theme.Round(RectangleF.FromLTRB(6.5f, 6.5f, 17.5f, 17.5f), 2.4f)) g.FillPath(b, gp);
                    break;
                case "folder":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddLine(3, 17.5f, 3, 6.8f);
                        gp.AddBezier(3, 6.8f, 3, 5.6f, 3.6f, 5, 4.8f, 5);
                        gp.AddLine(4.8f, 5, 9, 5);
                        gp.AddLine(9, 5, 11.2f, 7.4f);
                        gp.AddLine(11.2f, 7.4f, 19.2f, 7.4f);
                        gp.AddBezier(19.2f, 7.4f, 20.4f, 7.4f, 21, 8, 21, 9.2f);
                        gp.AddLine(21, 9.2f, 21, 17.5f);
                        gp.AddBezier(21, 17.5f, 21, 18.7f, 20.4f, 19.3f, 19.2f, 19.3f);
                        gp.AddLine(19.2f, 19.3f, 4.8f, 19.3f);
                        gp.AddBezier(4.8f, 19.3f, 3.6f, 19.3f, 3, 18.7f, 3, 17.5f);
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    g.DrawLine(p, 3, 10.4f, 21, 10.4f);
                    break;
                case "gear":
                {
                    List<PointF> pts = new List<PointF>();
                    for (int i = 0; i < 8; i++)
                    {
                        double a0 = i * Math.PI / 4;
                        double[] da = { -0.34, -0.2, 0.2, 0.34 };
                        double[] rr = { 6.6, 9.4, 9.4, 6.6 };
                        for (int j = 0; j < 4; j++)
                            pts.Add(new PointF((float)(12 + Math.Cos(a0 + da[j]) * rr[j]), (float)(12 + Math.Sin(a0 + da[j]) * rr[j])));
                    }
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddPolygon(pts.ToArray());
                        g.DrawPath(p, gp);
                    }
                    g.DrawEllipse(p, 9, 9, 6, 6);
                    break;
                }
                case "close":
                    g.DrawLine(p, 6.5f, 6.5f, 17.5f, 17.5f);
                    g.DrawLine(p, 17.5f, 6.5f, 6.5f, 17.5f);
                    break;
                case "stack":
                    RoundRect(g, p, 7.5f, 3, 20.5f, 12, 2.2f);
                    RoundRect(g, p, 3.5f, 9, 17.5f, 20.5f, 2.4f);
                    break;
                case "keyboard":
                    RoundRect(g, p, 2, 6, 22, 18.5f, 2.6f);
                    for (int i = 0; i < 5; i++) Dot(g, b, 5.8f + i * 3.1f, 10, 0.9f);
                    for (int i = 0; i < 4; i++) Dot(g, b, 7.3f + i * 3.1f, 12.8f, 0.9f);
                    g.DrawLine(p, 8, 15.6f, 16, 15.6f);
                    break;
                case "photo":
                    RoundRect(g, p, 2.5f, 4.5f, 21.5f, 19.5f, 2.8f);
                    Dot(g, b, 8, 9.2f, 1.6f);
                    g.DrawLines(p, new PointF[] { new PointF(2.8f, 17.5f), new PointF(9, 12.5f), new PointF(13, 16), new PointF(16, 13.5f), new PointF(21.3f, 17.8f) });
                    break;
                case "bot":
                    RoundRect(g, p, 3.5f, 7.5f, 20.5f, 20, 4.4f);
                    g.DrawLine(p, 12, 7.5f, 12, 4.6f);
                    Dot(g, b, 12, 3.4f, 1.3f);
                    using (GraphicsPath gp = Theme.Round(new RectangleF(8.2f, 11.2f, 2.2f, 4.4f), 1.1f)) g.FillPath(b, gp);
                    using (GraphicsPath gp = Theme.Round(new RectangleF(13.6f, 11.2f, 2.2f, 4.4f), 1.1f)) g.FillPath(b, gp);
                    break;
                case "info":
                    g.DrawEllipse(p, 2.8f, 2.8f, 18.4f, 18.4f);
                    g.DrawLine(p, 12, 11, 12, 16.5f);
                    Dot(g, b, 12, 7.8f, 1.15f);
                    break;
                case "power":
                    g.DrawArc(p, 4, 4.5f, 16, 16, -50, 280);
                    g.DrawLine(p, 12, 2.8f, 12, 11);
                    break;
                case "home":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddLines(new PointF[] { new PointF(4, 10.5f), new PointF(12, 3.8f), new PointF(20, 10.5f), new PointF(20, 19.5f), new PointF(4, 19.5f) });
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    g.DrawLine(p, 10, 19.5f, 10, 14.5f);
                    g.DrawLine(p, 10, 14.5f, 14, 14.5f);
                    g.DrawLine(p, 14, 14.5f, 14, 19.5f);
                    break;
                case "open":
                    g.DrawLines(p, new PointF[] { new PointF(10.5f, 4.5f), new PointF(5.5f, 4.5f), new PointF(4, 6), new PointF(4, 18.5f), new PointF(5.5f, 20), new PointF(18, 20), new PointF(19.5f, 18.5f), new PointF(19.5f, 13.5f) });
                    g.DrawLine(p, 12.5f, 11.5f, 20, 4);
                    g.DrawLines(p, new PointF[] { new PointF(14.5f, 4), new PointF(20, 4), new PointF(20, 9.5f) });
                    break;
                case "brush":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddLines(new PointF[] { new PointF(15.5f, 4.5f), new PointF(19.5f, 8.5f), new PointF(9, 19), new PointF(4, 20), new PointF(5, 15) });
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    g.DrawLine(p, 13, 7, 17, 11);
                    break;
                case "sparkle":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddBezier(12, 3, 12.6f, 8.6f, 15.4f, 11.4f, 21, 12);
                        gp.AddBezier(21, 12, 15.4f, 12.6f, 12.6f, 15.4f, 12, 21);
                        gp.AddBezier(12, 21, 11.4f, 15.4f, 8.6f, 12.6f, 3, 12);
                        gp.AddBezier(3, 12, 8.6f, 11.4f, 11.4f, 8.6f, 12, 3);
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    break;
                case "sound":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddLines(new PointF[] { new PointF(3, 9.5f), new PointF(7, 9.5f), new PointF(12, 5), new PointF(12, 19), new PointF(7, 14.5f), new PointF(3, 14.5f) });
                        gp.CloseFigure();
                        g.DrawPath(p, gp);
                    }
                    g.DrawArc(p, 11.5f, 8.5f, 6, 7, -55, 110);
                    g.DrawArc(p, 11, 5, 10.5f, 14, -55, 110);
                    break;
                case "mic":
                case "micoff":
                    using (GraphicsPath gp = Theme.Round(RectangleF.FromLTRB(8.5f, 2.5f, 15.5f, 14.5f), 3.5f)) g.DrawPath(p, gp);
                    g.DrawArc(p, 5, 6.5f, 14, 11, 0, 180);
                    g.DrawLine(p, 12, 17.5f, 12, 21);
                    g.DrawLine(p, 8.5f, 21, 15.5f, 21);
                    if (name == "micoff") g.DrawLine(p, 4, 3.5f, 20, 19.5f);
                    break;
                case "check":
                    g.DrawLines(p, new PointF[] { new PointF(5, 12.5f), new PointF(10, 17.5f), new PointF(19.5f, 6.5f) });
                    break;
                case "film":
                    RoundRect(g, p, 3, 3.5f, 21, 20.5f, 2.6f);
                    g.DrawLine(p, 7.5f, 3.5f, 7.5f, 20.5f);
                    g.DrawLine(p, 16.5f, 3.5f, 16.5f, 20.5f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 7.5f + i * 4.5f;
                        g.DrawLine(p, 3, y, 7.5f, y);
                        g.DrawLine(p, 16.5f, y, 21, y);
                    }
                    break;
                case "copy":
                    g.DrawLines(p, new PointF[] { new PointF(4, 15.5f), new PointF(4, 6), new PointF(6, 4), new PointF(15.5f, 4) });
                    RoundRect(g, p, 8, 8, 20.5f, 20.5f, 2.6f);
                    break;
                case "logo":
                    // Small logo: the two viewfinder corners around the lens.
                    g.DrawLines(p, new PointF[] { new PointF(4, 11.5f), new PointF(4, 5), new PointF(10.5f, 5) });
                    g.DrawLines(p, new PointF[] { new PointF(20, 12.5f), new PointF(20, 19), new PointF(13.5f, 19) });
                    Dot(g, b, 12, 12, 2.4f);
                    break;
                default:
                    g.DrawEllipse(p, 4, 4, 16, 16);
                    break;
            }
        }
    }
}
