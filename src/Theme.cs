// Stackshot - Colores (Tokyo Night) y utilidades de dibujo.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(22, 22, 30);
        public static readonly Color Bg2 = Color.FromArgb(15, 15, 20);
        public static readonly Color Dark = Color.FromArgb(26, 27, 38);
        public static readonly Color Button = Color.FromArgb(36, 40, 59);
        public static readonly Color ButtonHover = Color.FromArgb(52, 59, 88);
        public static readonly Color Border = Color.FromArgb(59, 66, 97);
        public static readonly Color Fg = Color.FromArgb(192, 202, 245);
        public static readonly Color Fg2 = Color.FromArgb(169, 177, 214);
        public static readonly Color Muted = Color.FromArgb(115, 124, 165);
        public static readonly Color Accent = Color.FromArgb(122, 162, 247);
        public static readonly Color Purple = Color.FromArgb(187, 154, 247);
        public static readonly Color Red = Color.FromArgb(247, 118, 142);
        public static readonly Color Green = Color.FromArgb(158, 206, 106);
        // Colores para marcar: fuertes, que se vean sobre cualquier captura.
        public static readonly Color[] Palette =
        {
            Color.FromArgb(255, 59, 48), Color.FromArgb(255, 204, 0), Color.FromArgb(52, 199, 89),
            Color.FromArgb(10, 132, 255), Color.White
        };
        public static readonly string[] PaletteNames = { "Rojo", "Amarillo", "Verde", "Azul", "Blanco" };
        public static string IconFont = "Segoe Fluent Icons";

        public static void Init()
        {
            using (InstalledFontCollection fc = new InstalledFontCollection())
            {
                foreach (FontFamily f in fc.Families)
                {
                    if (f.Name == IconFont) return;
                }
            }
            IconFont = "Segoe MDL2 Assets";
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
