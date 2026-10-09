// Stackshot - Drawing kit: line icons, text, shapes and per-pixel surfaces rendered with WPF.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using D = System.Drawing;
using DI = System.Drawing.Imaging;

namespace Stackshot
{
    // SF Symbols-style line icons on a 24x24 grid. Names ending in '!' are filled.
    public static partial class Glyph
    {
        static readonly Dictionary<string, Geometry> cache = new Dictionary<string, Geometry>();

        public static bool Has(string name) { return data.ContainsKey(name); }

        static Geometry Get(string name)
        {
            Geometry g;
            if (cache.TryGetValue(name, out g)) return g;
            string d;
            if (!data.TryGetValue(name, out d)) return null;
            g = Geometry.Parse(d);
            g.Freeze();
            cache[name] = g;
            return g;
        }

        // Draws the icon centered in a size x size box at (x, y); stroke is in output pixels.
        public static void Draw(DrawingContext dc, string name, double x, double y, double size, Color color, double stroke)
        {
            Geometry g = Get(name);
            if (g == null) return;
            double k = size / 24.0;
            dc.PushTransform(new MatrixTransform(k, 0, 0, k, x, y));
            Brush b = Ds.Brush(color);
            if (name.EndsWith("!")) dc.DrawGeometry(b, null, g);
            else
            {
                Pen p = new Pen(b, stroke / k);
                p.StartLineCap = p.EndLineCap = PenLineCap.Round;
                p.LineJoin = PenLineJoin.Round;
                p.Freeze();
                dc.DrawGeometry(null, p, g);
            }
            dc.Pop();
        }
    }

    public static class Ink
    {
        // Device pixels per DIP of the window the text is drawn in; the main window keeps it current.
        public static double PerDip = 1.0;

        public static FormattedText Text(string s, Typeface face, double px, Color color)
        {
            return new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, Math.Max(1, px),
                                     Ds.Brush(color), null, px <= 15 ? TextFormattingMode.Display : TextFormattingMode.Ideal, PerDip);
        }

        // Text for per-pixel surfaces: they are rendered 1:1 at 96 dpi in device pixels, so one DIP is one pixel.
        public static FormattedText Px(string s, Typeface face, double px, Color color)
        {
            return new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, Math.Max(1, px),
                                     Ds.Brush(color), null, px <= 15 ? TextFormattingMode.Display : TextFormattingMode.Ideal, 1.0);
        }

        // Text centered in a box, nudged to whole pixels so it stays crisp.
        public static void Center(DrawingContext dc, FormattedText t, Rect r)
        {
            double k = t.PixelsPerDip;
            dc.DrawText(t, new Point(Math.Round((r.X + (r.Width - t.WidthIncludingTrailingWhitespace) / 2) * k) / k,
                                     Math.Round((r.Y + (r.Height - t.Height) / 2) * k) / k));
        }

        public static void Round(DrawingContext dc, Color fill, Rect r, double radius)
        {
            dc.DrawRoundedRectangle(Ds.Brush(fill), null, r, radius, radius);
        }

        // Hairline inside the edge, like the 1 px borders of macOS panels.
        public static void Hairline(DrawingContext dc, Color c, Rect r, double radius)
        {
            Pen p = new Pen(Ds.Brush(c), 1);
            p.Freeze();
            r.Inflate(-0.5, -0.5);
            dc.DrawRoundedRectangle(null, p, r, Math.Max(0, radius - 0.5), Math.Max(0, radius - 0.5));
        }

        public static Rect Inset(Rect r, double d) { r.Inflate(-d, -d); return r.Width < 0 || r.Height < 0 ? new Rect(r.X, r.Y, 0, 0) : r; }

        // Device-pixel snapping for owner-drawn lines, so 1 px edges stay crisp at 125% and 150%.
        public static double Scale(Visual v)
        {
            try { return Math.Max(0.5, VisualTreeHelper.GetDpi(v).DpiScaleY); }
            catch { return 1; }
        }

        // A hairline thickness in DIP that covers whole device pixels (1 px up to 150%, 2 px from 175%).
        public static double Hair(Visual v)
        {
            double k = Scale(v);
            return Math.Max(1, Math.Floor(k + 0.25)) / k;
        }

        // A length of about dip DIPs rounded to whole device pixels (at least one).
        public static double Snap(Visual v, double dip)
        {
            double k = Scale(v);
            return Math.Max(1, Math.Round(dip * k, MidpointRounding.AwayFromZero)) / k;
        }

        // A coordinate moved onto the nearest device-pixel boundary.
        public static double Align(Visual v, double dip)
        {
            double k = Scale(v);
            return Math.Round(dip * k) / k;
        }

        public static BitmapSource FromGdi(D.Bitmap b)
        {
            D.Rectangle r = new D.Rectangle(0, 0, b.Width, b.Height);
            DI.BitmapData bd = b.LockBits(r, DI.ImageLockMode.ReadOnly, DI.PixelFormat.Format32bppPArgb);
            try
            {
                BitmapSource s = BitmapSource.Create(b.Width, b.Height, 96, 96, PixelFormats.Pbgra32, null, bd.Scan0, bd.Stride * b.Height, bd.Stride);
                s.Freeze();
                return s;
            }
            finally { b.UnlockBits(bd); }
        }

        static readonly Dictionary<string, D.Bitmap> glyphs = new Dictionary<string, D.Bitmap>();

        // A line icon as a cached GDI+ bitmap, for WinForms surfaces (menus, bars).
        public static D.Bitmap GlyphBitmap(string name, int px, Color color, double stroke)
        {
            string key = name + "|" + px + "|" + color + "|" + stroke;
            D.Bitmap b;
            if (glyphs.TryGetValue(key, out b)) return b;
            if (glyphs.Count > 300) { foreach (D.Bitmap old in glyphs.Values) old.Dispose(); glyphs.Clear(); }
            b = ToGdi(Render(px, px, delegate(DrawingContext dc) { Glyph.Draw(dc, name, 0, 0, px, color, stroke); }, null));
            glyphs[key] = b;
            return b;
        }

        public static D.Bitmap ToGdi(BitmapSource s)
        {
            D.Bitmap b = new D.Bitmap(s.PixelWidth, s.PixelHeight, DI.PixelFormat.Format32bppPArgb);
            DI.BitmapData bd = b.LockBits(new D.Rectangle(0, 0, b.Width, b.Height), DI.ImageLockMode.WriteOnly, DI.PixelFormat.Format32bppPArgb);
            try { s.CopyPixels(new Int32Rect(0, 0, b.Width, b.Height), bd.Scan0, bd.Stride * b.Height, bd.Stride); }
            finally { b.UnlockBits(bd); }
            return b;
        }

        public static RenderTargetBitmap Render(int w, int h, Action<DrawingContext> paint, Effect effect)
        {
            DrawingVisual v = new DrawingVisual();
            if (effect != null) v.Effect = effect;
            TextOptions.SetTextRenderingMode(v, TextRenderingMode.Grayscale);
            RenderOptions.SetBitmapScalingMode(v, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = v.RenderOpen()) paint(dc);
            RenderTargetBitmap rtb = new RenderTargetBitmap(Math.Max(1, w), Math.Max(1, h), 96, 96, PixelFormats.Pbgra32);
            rtb.Render(v);
            rtb.Freeze();
            return rtb;
        }

        // Soft drop shadow of a rounded rectangle, pre-rendered once per size.
        public static BitmapSource Shadow(int w, int h, Rect body, double radius, double blur, Color color)
        {
            BlurEffect fx = new BlurEffect();
            fx.Radius = blur;
            fx.KernelType = KernelType.Gaussian;
            fx.RenderingBias = RenderingBias.Quality;
            return Render(w, h, delegate(DrawingContext dc) { dc.DrawRoundedRectangle(Ds.Brush(color), null, body, radius, radius); }, fx);
        }

        public static BitmapSource Blurred(BitmapSource src, double radius)
        {
            BlurEffect fx = new BlurEffect();
            fx.Radius = radius;
            fx.KernelType = KernelType.Gaussian;
            fx.RenderingBias = RenderingBias.Quality;
            int w = src.PixelWidth, h = src.PixelHeight;
            // Edge pixels extended so the blur doesn't fade into transparency at the borders.
            int m = (int)Math.Ceiling(radius);
            RenderTargetBitmap big = Render(w + 2 * m, h + 2 * m, delegate(DrawingContext dc)
            {
                dc.DrawImage(src, new Rect(-m, -m, w + 2 * m + 2 * m, h + 2 * m + 2 * m));
                dc.DrawImage(src, new Rect(m, m, w, h));
            }, fx);
            CroppedBitmap c = new CroppedBitmap(big, new Int32Rect(m, m, w, h));
            c.Freeze();
            return c;
        }
    }

    // A premultiplied pixel buffer shown with UpdateLayeredWindow: real per-pixel alpha (soft shadows, rounded edges)
    // painted by WPF in software, which for small surfaces is faster than starting a GPU device.
    public sealed class Surface : IDisposable
    {
        Dib dib;

        public int Width { get { return dib == null ? 0 : dib.Width; } }
        public int Height { get { return dib == null ? 0 : dib.Height; } }

        public void Paint(int w, int h, Action<DrawingContext> paint)
        {
            if (dib == null || dib.Width != w || dib.Height != h)
            {
                if (dib != null) dib.Dispose();
                dib = new Dib(w, h, true);
            }
            RenderTargetBitmap rtb = Ink.Render(w, h, paint, null);
            Native.GdiFlush();
            rtb.CopyPixels(new Int32Rect(0, 0, w, h), dib.Bits, w * 4 * h, w * 4);
        }

        public void Present(IntPtr hwnd, byte alpha)
        {
            if (dib != null) Native.PresentHere(hwnd, dib.Dc, dib.Width, dib.Height, alpha);
        }

        public D.Bitmap Snapshot(D.Rectangle part)
        {
            D.Bitmap b = new D.Bitmap(part.Width, part.Height, DI.PixelFormat.Format32bppPArgb);
            using (D.Graphics g = D.Graphics.FromImage(b)) g.DrawImage(dib.Bitmap, new D.Rectangle(0, 0, part.Width, part.Height), part, D.GraphicsUnit.Pixel);
            return b;
        }

        public void Dispose()
        {
            if (dib != null) { dib.Dispose(); dib = null; }
        }
    }
}
