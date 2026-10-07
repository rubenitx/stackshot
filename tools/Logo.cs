// Stackshot - Renders the logo (src\Home\LogoArt.cs) to PNG and icon sizes, supersampled, with a real soft shadow.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Stackshot;

public static class StackshotLogo
{
    // style 0: logo (with margin and shadow, for the web) | 1: full-bleed icon | 2: small icon (16-32 px)
    public static Bitmap Render(int size, int style)
    {
        int ss = size <= 64 ? 8 : size <= 256 ? 4 : 2;
        int S = size * ss;
        float half = style == 0 ? 404 : style == 1 ? 488 : 500;   // squircle half-size on a 1024 canvas
        float w = S * half / 488f;
        RectangleF box = new RectangleF((S - w) / 2, (S - w) / 2, w, w);
        Bitmap big = new Bitmap(S, S, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            if (style == 0)
                using (GraphicsPath body = LogoArt.Squircle(S / 2f, S / 2f, half * S / 1024f, 5))
                    Shadow(g, body, S, 0, 26 * S / 1024f, 30 * S / 1024f, Color.FromArgb(90, 20, 8, 60));
            LogoArt.PaintFull(g, box, style == 2);
        }
        Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(big, new Rectangle(0, 0, size, size));
        }
        big.Dispose();
        return result;
    }

    // Real soft shadow: the shape is painted into a small mask, blurred (three box passes ~ gaussian) and drawn
    // upscaled and tinted below the shape.
    static void Shadow(Graphics g, GraphicsPath shape, int S, float dx, float dy, float sigma, Color color)
    {
        const int down = 4;
        int n = Math.Max(8, S / down);
        float scale = n / (float)S;
        Bitmap mask = new Bitmap(n, n, PixelFormat.Format32bppArgb);
        using (Graphics mg = Graphics.FromImage(mask))
        using (GraphicsPath p = (GraphicsPath)shape.Clone())
        using (Matrix m = new Matrix())
        {
            mg.SmoothingMode = SmoothingMode.AntiAlias;
            m.Scale(scale, scale);
            m.Translate(dx, dy);
            p.Transform(m);
            using (SolidBrush b = new SolidBrush(Color.White)) mg.FillPath(b, p);
        }
        BitmapData bd = mask.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        byte[] px = new byte[n * n * 4];
        Marshal.Copy(bd.Scan0, px, 0, px.Length);
        float[] a = new float[n * n];
        for (int i = 0; i < a.Length; i++) a[i] = px[i * 4 + 3];
        int radius = Math.Max(1, (int)Math.Round(sigma * scale));
        for (int pass = 0; pass < 3; pass++) { BoxH(a, n, radius); BoxV(a, n, radius); }
        for (int i = 0; i < a.Length; i++)
        {
            byte al = (byte)Math.Max(0, Math.Min(255, a[i] * color.A / 255f));
            px[i * 4] = color.B; px[i * 4 + 1] = color.G; px[i * 4 + 2] = color.R; px[i * 4 + 3] = al;
        }
        Marshal.Copy(px, 0, bd.Scan0, px.Length);
        mask.UnlockBits(bd);
        InterpolationMode im = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(mask, new Rectangle(0, 0, S, S));
        g.InterpolationMode = im;
        mask.Dispose();
    }

    static void BoxH(float[] a, int n, int r)
    {
        float[] row = new float[n];
        float inv = 1f / (2 * r + 1);
        for (int y = 0; y < n; y++)
        {
            int o = y * n;
            float sum = 0;
            for (int x = -r; x <= r; x++) sum += a[o + Math.Max(0, Math.Min(n - 1, x))];
            for (int x = 0; x < n; x++)
            {
                row[x] = sum * inv;
                sum += a[o + Math.Min(n - 1, x + r + 1)] - a[o + Math.Max(0, x - r)];
            }
            Array.Copy(row, 0, a, o, n);
        }
    }

    static void BoxV(float[] a, int n, int r)
    {
        float[] col = new float[n];
        float inv = 1f / (2 * r + 1);
        for (int x = 0; x < n; x++)
        {
            float sum = 0;
            for (int y = -r; y <= r; y++) sum += a[Math.Max(0, Math.Min(n - 1, y)) * n + x];
            for (int y = 0; y < n; y++)
            {
                col[y] = sum * inv;
                sum += a[Math.Min(n - 1, y + r + 1) * n + x] - a[Math.Max(0, y - r) * n + x];
            }
            for (int y = 0; y < n; y++) a[y * n + x] = col[y];
        }
    }
}
