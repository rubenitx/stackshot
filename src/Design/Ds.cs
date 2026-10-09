// Stackshot - Design tokens: one palette (light and dark), radii, type and motion.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using Microsoft.Win32;
using M = System.Windows.Media;

namespace Stackshot
{
    // Single source of truth for how Stackshot looks. Colors follow the macOS system palette; the mode follows Windows
    // unless the user fixes it in Settings.
    public static class Ds
    {
        public static bool Dark = true;
        static int mode;
        static bool hooked;
        public static event Action Changed;

        // 0 follow Windows, 1 light, 2 dark.
        public static void Apply(int appearance)
        {
            mode = appearance;
            if (!hooked)
            {
                hooked = true;
                try { SystemEvents.UserPreferenceChanged += delegate { if (mode == 0) Refresh(); }; } catch { }
            }
            Refresh();
        }

        static void Refresh()
        {
            bool d = mode == 2 || (mode == 0 && !SystemLight());
            if (d == Dark) return;
            Dark = d;
            Brushes = new Palette(d);
            Action h = Changed;
            if (h != null) h();
        }

        static bool SystemLight()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return !(v is int) || (int)v != 0;
            }
            catch { return false; }
        }

        public static Palette Brushes = new Palette(true);

        // Radii
        public const double RSmall = 6, RControl = 8, RPanel = 10, RCard = 12, RLarge = 20;

        // Type
        public static readonly M.FontFamily Text = Family("Segoe UI Variable Text", "Segoe UI");
        public static readonly M.FontFamily Display = Family("Segoe UI Variable Display", "Segoe UI");
        public static readonly M.Typeface Regular = new M.Typeface(Text, System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
        public static readonly M.Typeface Medium = new M.Typeface(Text, System.Windows.FontStyles.Normal, System.Windows.FontWeights.Medium, System.Windows.FontStretches.Normal);
        public static readonly M.Typeface Semibold = new M.Typeface(Text, System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal);
        public static readonly M.Typeface Title = new M.Typeface(Display, System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal);

        static M.FontFamily Family(string name, string fallback)
        {
            foreach (M.FontFamily f in M.Fonts.SystemFontFamilies)
                if (string.Equals(f.Source, name, StringComparison.OrdinalIgnoreCase)) return f;
            return new M.FontFamily(fallback);
        }

        public static M.Color Rgb(byte r, byte g, byte b) { return M.Color.FromRgb(r, g, b); }
        public static M.Color Argb(double a, byte r, byte g, byte b) { return M.Color.FromArgb((byte)Math.Round(a * 255), r, g, b); }
        public static M.Color WithAlpha(M.Color c, double a) { return M.Color.FromArgb((byte)Math.Round(c.A * Math.Max(0, Math.Min(1, a))), c.R, c.G, c.B); }

        public static M.SolidColorBrush Brush(M.Color c)
        {
            M.SolidColorBrush b = new M.SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static System.Drawing.Color Gdi(M.Color c) { return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B); }
    }

    public sealed class Palette
    {
        public readonly bool Dark;
        public readonly M.Color Label, Label2, Label3, Window, Sidebar, Group, Control, ControlHover, Separator, Hairline,
                                Accent, Red, Green, Orange, Yellow, Purple, Pink, Teal, Thumb, Shadow;

        // Always-dark heads-up surfaces (overlays, toolbars on captures), as on macOS.
        public static readonly M.Color Hud = Ds.Argb(0.86, 28, 28, 30), HudHover = Ds.Argb(0.18, 255, 255, 255),
                                       HudLabel = Ds.Rgb(255, 255, 255), HudLabel2 = Ds.Argb(0.6, 255, 255, 255),
                                       HudLine = Ds.Argb(0.12, 255, 255, 255);

        public Palette(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Label = Ds.Argb(0.92, 255, 255, 255); Label2 = Ds.Argb(0.55, 255, 255, 255); Label3 = Ds.Argb(0.28, 255, 255, 255);
                Window = Ds.Rgb(30, 30, 30); Sidebar = Ds.Rgb(37, 37, 38); Group = Ds.Argb(0.05, 255, 255, 255);
                Control = Ds.Argb(0.10, 255, 255, 255); ControlHover = Ds.Argb(0.16, 255, 255, 255);
                Separator = Ds.Argb(0.10, 255, 255, 255); Hairline = Ds.Argb(0.14, 255, 255, 255);
                Accent = Ds.Rgb(10, 132, 255); Red = Ds.Rgb(255, 69, 58); Green = Ds.Rgb(48, 209, 88); Orange = Ds.Rgb(255, 159, 10);
                Yellow = Ds.Rgb(255, 214, 10); Purple = Ds.Rgb(191, 90, 242); Pink = Ds.Rgb(255, 55, 95); Teal = Ds.Rgb(100, 210, 255);
                Thumb = Ds.Rgb(44, 44, 46); Shadow = Ds.Argb(0.45, 0, 0, 0);
            }
            else
            {
                Label = Ds.Argb(0.88, 0, 0, 0); Label2 = Ds.Argb(0.50, 0, 0, 0); Label3 = Ds.Argb(0.26, 0, 0, 0);
                Window = Ds.Rgb(246, 246, 246); Sidebar = Ds.Rgb(232, 232, 234); Group = Ds.Rgb(255, 255, 255);
                Control = Ds.Argb(0.06, 0, 0, 0); ControlHover = Ds.Argb(0.10, 0, 0, 0);
                Separator = Ds.Argb(0.09, 0, 0, 0); Hairline = Ds.Argb(0.12, 0, 0, 0);
                Accent = Ds.Rgb(0, 122, 255); Red = Ds.Rgb(255, 59, 48); Green = Ds.Rgb(52, 199, 89); Orange = Ds.Rgb(255, 149, 0);
                Yellow = Ds.Rgb(255, 204, 0); Purple = Ds.Rgb(175, 82, 222); Pink = Ds.Rgb(255, 45, 85); Teal = Ds.Rgb(90, 200, 250);
                Thumb = Ds.Rgb(242, 242, 247); Shadow = Ds.Argb(0.28, 0, 0, 0);
            }
        }
    }
}
