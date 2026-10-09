// Stackshot - Colors for the GDI+ surfaces (bridged to the design palette) and drawing helpers.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Stackshot
{
    // Opaque System.Drawing colors that follow the light/dark palette in Ds, for code drawn with GDI+.
    public static class Theme
    {
        static Color Pick(int dr, int dg, int db, int lr, int lg, int lb) { return Ds.Dark ? Color.FromArgb(dr, dg, db) : Color.FromArgb(lr, lg, lb); }

        public static Color Bg { get { return Pick(30, 30, 30, 246, 246, 246); } }
        public static Color Bg2 { get { return Pick(22, 22, 24, 236, 236, 238); } }
        public static Color Dark { get { return Pick(37, 37, 39, 240, 240, 242); } }
        public static Color Button { get { return Pick(58, 58, 62, 228, 228, 232); } }
        public static Color ButtonHover { get { return Pick(72, 72, 77, 216, 216, 221); } }
        public static Color Border { get { return Pick(58, 58, 62, 212, 212, 216); } }
        public static Color Fg { get { return Pick(245, 245, 247, 29, 29, 31); } }
        public static Color Fg2 { get { return Pick(210, 210, 215, 72, 72, 78); } }
        public static Color Muted { get { return Pick(152, 152, 159, 128, 128, 134); } }
        public static Color Accent { get { return Ds.Gdi(Ds.Brushes.Accent); } }
        public static Color AccentHover { get { return Pick(64, 156, 255, 40, 140, 255); } }
        public static Color AccentDown { get { return Pick(0, 104, 214, 0, 100, 210); } }
        public static Color Purple { get { return Ds.Gdi(Ds.Brushes.Purple); } }
        public static Color Red { get { return Ds.Gdi(Ds.Brushes.Red); } }
        public static Color Green { get { return Ds.Gdi(Ds.Brushes.Green); } }
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
