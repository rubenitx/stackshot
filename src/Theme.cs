// Stackshot - Colors (Tokyo Night) and drawing helpers.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Stackshot
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(28, 28, 30);
        public static readonly Color Bg2 = Color.FromArgb(18, 18, 20);
        public static readonly Color Dark = Color.FromArgb(34, 34, 37);
        public static readonly Color Button = Color.FromArgb(48, 48, 52);
        public static readonly Color ButtonHover = Color.FromArgb(62, 62, 67);
        public static readonly Color Border = Color.FromArgb(58, 58, 62);
        public static readonly Color Fg = Color.FromArgb(245, 245, 247);
        public static readonly Color Fg2 = Color.FromArgb(210, 210, 215);
        public static readonly Color Muted = Color.FromArgb(152, 152, 159);
        public static readonly Color Accent = Color.FromArgb(10, 132, 255);
        public static readonly Color AccentHover = Color.FromArgb(64, 156, 255);
        public static readonly Color AccentDown = Color.FromArgb(0, 104, 214);
        public static readonly Color Purple = Color.FromArgb(94, 92, 230);
        public static readonly Color Red = Color.FromArgb(255, 69, 58);
        public static readonly Color Green = Color.FromArgb(48, 209, 88);
        // Annotation colors: saturated so they stand out on any capture.
        public static readonly Color[] Palette =
        {
            Color.FromArgb(255, 59, 48), Color.FromArgb(255, 204, 0), Color.FromArgb(52, 199, 89),
            Color.FromArgb(10, 132, 255), Color.White
        };
        public static readonly string[] PaletteNames = { "Rojo", "Amarillo", "Verde", "Azul", "Blanco" };
        public static string IconFont = "Segoe Fluent Icons";

        public static void Init()
        {
            if (!Fonts.Has(IconFont)) IconFont = "Segoe MDL2 Assets";
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

}
