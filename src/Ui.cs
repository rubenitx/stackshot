// Stackshot - Fonts and GDI+ text drawing (TextKit) for the surfaces drawn with GDI+.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Fonts
    {
        static string display, displaySemibold;

        // Semibold variant for titles (Windows truncates its family name to 31 characters).
        public static string DisplaySemibold
        {
            get
            {
                if (displaySemibold != null) return displaySemibold;
                displaySemibold = "Segoe UI Semibold";
                if (Has("Segoe UI Variable Display Semib")) displaySemibold = "Segoe UI Variable Display Semib";
                return displaySemibold;
            }
        }

        // Segoe UI Variable on Windows 11; Segoe UI on Windows 10.
        public static string Display
        {
            get
            {
                if (display != null) return display;
                display = "Segoe UI";
                if (Has("Segoe UI Variable Display")) display = "Segoe UI Variable Display";
                return display;
            }
        }

        // family -> size in hundredths of a pixel -> font (no string building on every draw call).
        static readonly Dictionary<string, Dictionary<int, Font>> cache = new Dictionary<string, Dictionary<int, Font>>();

        // Shared fonts for owner-drawn chrome repainted every animation frame. Never dispose the result: callers share
        // it, and the set stays small (a few sizes per DPI scale).
        // Checks one family directly instead of enumerating every installed font (slow at startup).
        public static bool Has(string family)
        {
            try { using (FontFamily f = new FontFamily(family)) return true; }
            catch (ArgumentException) { return false; }
        }

        public static Font Get(string family, float px)
        {
            int key = (int)Math.Round(px * 100);
            lock (cache)
            {
                Dictionary<int, Font> sizes;
                if (!cache.TryGetValue(family, out sizes)) cache[family] = sizes = new Dictionary<int, Font>();
                Font f;
                if (!sizes.TryGetValue(key, out f)) sizes[key] = f = new Font(family, Math.Max(1f, px), GraphicsUnit.Pixel);
                return f;
            }
        }
    }

    // UI text through GDI+ with unhinted, fractional glyph positions, the way macOS and DirectWrite lay it out. GDI
    // (TextRenderer) snaps stems and advances to whole pixels, which makes small text look heavy and cramped. Same
    // signatures as TextRenderer; text is always measured with the same settings it is drawn with.
    public static class TextKit
    {
        static readonly Dictionary<int, StringFormat> formats = new Dictionary<int, StringFormat>();
        [ThreadStatic] static Graphics measurer;

        const TextFormatFlags Relevant = TextFormatFlags.HorizontalCenter | TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                                         TextFormatFlags.Bottom | TextFormatFlags.WordBreak | TextFormatFlags.SingleLine |
                                         TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis;

        static StringFormat Format(TextFormatFlags f)
        {
            int key = (int)(f & Relevant);
            lock (formats)
            {
                StringFormat sf;
                if (formats.TryGetValue(key, out sf)) return sf;
                sf = (StringFormat)StringFormat.GenericTypographic.Clone();
                StringFormatFlags ff = sf.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;
                if ((f & TextFormatFlags.WordBreak) == 0 || (f & TextFormatFlags.SingleLine) != 0) ff |= StringFormatFlags.NoWrap;
                else ff |= StringFormatFlags.LineLimit;
                sf.FormatFlags = ff;
                sf.Alignment = (f & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center
                             : (f & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
                sf.LineAlignment = (f & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center
                                 : (f & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
                sf.Trimming = (f & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter
                            : (f & TextFormatFlags.WordEllipsis) != 0 ? StringTrimming.EllipsisWord
                            : (f & TextFormatFlags.PathEllipsis) != 0 ? StringTrimming.EllipsisPath : StringTrimming.None;
                sf.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
                formats[key] = sf;
                return sf;
            }
        }

        // TextRenderer's default glyph overhang padding, so layouts sized for it keep their spacing.
        static void Margins(Font font, TextFormatFlags f, out int left, out int right)
        {
            left = right = 0;
            if ((f & TextFormatFlags.NoPadding) != 0) return;
            float o = font.Height / 6f;
            bool wide = (f & TextFormatFlags.LeftAndRightPadding) != 0;
            left = (int)Math.Ceiling(wide ? 2 * o : o);
            right = (int)Math.Ceiling(o * (wide ? 2.5f : 1.5f));
        }

        // Extra flag for overlay text on layered surfaces: hinted, grid-fitted glyphs that stay sharp instead of fractional ones.
        public const TextFormatFlags Crisp = TextFormatFlags.NoClipping;

        static string Clean(string text, TextFormatFlags f)
        {
            if (text == null) return "";
            return (f & TextFormatFlags.SingleLine) != 0 ? text.Replace("\r", "").Replace('\n', ' ') : text;
        }

        public static void Draw(Graphics g, string text, Font font, Rectangle bounds, Color color)
        {
            Draw(g, text, font, bounds, color, TextFormatFlags.Default);
        }

        public static void Draw(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            text = Clean(text, flags);
            if (text.Length == 0 || bounds.Width <= 0 || color.A == 0) return;
            int l, r;
            Margins(font, flags, out l, out r);
            RectangleF box = new RectangleF(bounds.X + l, bounds.Y, Math.Max(1, bounds.Width - l - r), Math.Max(1, bounds.Height));
            Paint(g, text, font, box, color, Format(flags), (flags & Crisp) != 0);
        }

        public static void Draw(Graphics g, string text, Font font, Point at, Color color)
        {
            Draw(g, text, font, at, color, TextFormatFlags.Default);
        }

        public static void Draw(Graphics g, string text, Font font, Point at, Color color, TextFormatFlags flags)
        {
            text = Clean(text, flags);
            if (text.Length == 0 || color.A == 0) return;
            int l, r;
            Margins(font, flags, out l, out r);
            Size sz = Measure(text, font, Size.Empty, flags | TextFormatFlags.NoPadding);
            Paint(g, text, font, new RectangleF(at.X + l, at.Y, sz.Width + 1, sz.Height + 1), color, Format(TextFormatFlags.NoPadding), (flags & Crisp) != 0);
        }

        static void Paint(Graphics g, string text, Font font, RectangleF box, Color color, StringFormat sf, bool crisp)
        {
            System.Drawing.Text.TextRenderingHint hint = g.TextRenderingHint;
            CompositingMode mode = g.CompositingMode;
            // Opaque surfaces ask for ClearType (hinted, sharp on a solid background); layered ones use the Crisp flag; the rest unhinted.
            g.TextRenderingHint = crisp ? System.Drawing.Text.TextRenderingHint.AntiAliasGridFit
                : hint == System.Drawing.Text.TextRenderingHint.ClearTypeGridFit ? hint : System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.CompositingMode = CompositingMode.SourceOver;
            using (SolidBrush b = new SolidBrush(color)) g.DrawString(text, font, b, box, sf);
            g.TextRenderingHint = hint;
            g.CompositingMode = mode;
        }

        public static Size Measure(string text, Font font)
        {
            return Measure(text, font, Size.Empty, TextFormatFlags.Default);
        }

        public static Size Measure(Graphics g, string text, Font font)
        {
            return Measure(text, font, Size.Empty, TextFormatFlags.Default);
        }

        public static Size Measure(Graphics g, string text, Font font, Size proposed, TextFormatFlags flags)
        {
            return Measure(text, font, proposed, flags);
        }

        public static Size Measure(string text, Font font, Size proposed, TextFormatFlags flags)
        {
            text = Clean(text, flags);
            if (text.Length == 0) return Size.Empty;
            int l, r;
            Margins(font, flags, out l, out r);
            if (measurer == null) measurer = Graphics.FromImage(new Bitmap(1, 1));
            measurer.TextRenderingHint = (flags & Crisp) != 0 ? System.Drawing.Text.TextRenderingHint.AntiAliasGridFit : System.Drawing.Text.TextRenderingHint.AntiAlias;
            bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0 && proposed.Width > 0;
            float width = wrap ? Math.Max(1, proposed.Width - l - r) : 100000f;
            SizeF s = measurer.MeasureString(text, font, new SizeF(width, 100000f), Format(wrap ? TextFormatFlags.WordBreak : TextFormatFlags.Default));
            return new Size((int)Math.Ceiling(s.Width) + l + r, (int)Math.Ceiling(s.Height));
        }
    }

}
