// Stackshot - El logo, dibujado en código: así se puede regenerar a cualquier tamaño sin programas de diseño.
// MIT License - https://github.com/rubenitx/stackshot
//
// Una "squircle" (superelipse, la forma de los iconos de Apple) con un degradado violeta-azul-cian y un brillo
// arriba; dentro, tres tarjetas en abanico (la pila de capturas) y, en la de delante, las esquinas de un visor.
// Se dibuja a 4-8 veces el tamaño final y se reduce con calidad alta; las sombras se difuminan de verdad.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class StackshotLogo
{
    // style 0: logo (con margen y sombra, para la web)  ·  1: icono a sangre  ·  2: icono pequeño (16-32 px)
    public static Bitmap Render(int size, int style)
    {
        int ss = size <= 64 ? 8 : size <= 256 ? 4 : 2;
        int S = size * ss;
        float k = S / 1024f;
        float half = style == 0 ? 404 : style == 1 ? 488 : 500;
        float cardScale = style == 0 ? 1.0f : style == 1 ? 1.18f : 1.42f;
        Bitmap big = new Bitmap(S, S, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            using (GraphicsPath body = Squircle(512 * k, 512 * k, half * k, 5))
            {
                if (style == 0) Shadow(g, body, S, 0, 26 * k, 30 * k, Color.FromArgb(82, 0, 0, 0));

                // Fondo: degradado en diagonal.
                using (LinearGradientBrush bg = new LinearGradientBrush(new PointF(0, 0), new PointF(S, S), Color.White, Color.White))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { Hex("8B5CF6"), Hex("4F7BFF"), Hex("14B8E6") };
                    cb.Positions = new float[] { 0f, 0.52f, 1f };
                    bg.InterpolationColors = cb;
                    g.FillPath(bg, body);
                }

                // Brillo arriba a la izquierda.
                GraphicsState st = g.Save();
                g.SetClip(body);
                using (GraphicsPath glow = new GraphicsPath())
                {
                    RectangleF gr = new RectangleF(S * 0.28f - S * 0.85f, S * 0.12f - S * 0.85f, S * 1.7f, S * 1.7f);
                    glow.AddEllipse(gr);
                    using (PathGradientBrush pb = new PathGradientBrush(glow))
                    {
                        pb.CenterPoint = new PointF(S * 0.28f, S * 0.12f);
                        ColorBlend cb = new ColorBlend();
                        cb.Colors = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(15, 255, 255, 255), Color.FromArgb(88, 255, 255, 255) };
                        cb.Positions = new float[] { 0f, 0.45f, 1f };
                        pb.InterpolationColors = cb;
                        g.FillPath(pb, glow);
                    }
                }

                Cards(g, S, k * cardScale, style != 2, style == 2);
                g.Restore(st);

                // Filo de luz en el borde (más claro arriba): da volumen, como el cristal.
                if (style != 2)
                {
                    using (LinearGradientBrush rim = new LinearGradientBrush(new PointF(0, 0), new PointF(0, S), Color.White, Color.White))
                    {
                        ColorBlend cb = new ColorBlend();
                        cb.Colors = new Color[] { Color.FromArgb(140, 255, 255, 255), Color.FromArgb(20, 255, 255, 255), Color.FromArgb(46, 255, 255, 255) };
                        cb.Positions = new float[] { 0f, 0.35f, 1f };
                        rim.InterpolationColors = cb;
                        using (Pen p = new Pen(rim, 6 * k)) g.DrawPath(p, body);
                    }
                }
            }
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

    // Las tres tarjetas en abanico (la de atrás, translúcida; la de delante, blanca y con sombra) y el visor.
    static void Cards(Graphics g, int S, float k, bool brackets, bool small)
    {
        float c = S / 2f, m = c - 20 * k, w = 470 * k, h = 336 * k, r = 50 * k; // m: centro vertical de la pila, algo por encima del centro
        DrawCard(g, S, c - 26 * k, m - 64 * k, -13, w, h, r, Color.FromArgb(small ? 110 : 77, 255, 255, 255), Color.Empty, 0, 0);
        DrawCard(g, S, c - 6 * k, m - 14 * k, -6, w, h, r, Color.FromArgb(small ? 175 : 148, 255, 255, 255), Color.FromArgb(56, 27, 22, 80), 10 * k, 16 * k);
        DrawCard(g, S, c + 14 * k, m + 40 * k, 0, w, h, r, Color.White, Color.FromArgb(107, 27, 22, 80), 22 * k, 26 * k);
        if (!brackets) return;
        float fx = c + 14 * k - w / 2, fy = m + 40 * k - h / 2, inset = 64 * k, arm = 74 * k;
        float L = fx + inset, T = fy + inset, R = fx + w - inset, B = fy + h - inset;
        using (LinearGradientBrush ink = new LinearGradientBrush(new PointF(fx, fy), new PointF(fx + w, fy + h), Hex("7C5CFF"), Hex("1FA8F0")))
        using (Pen pen = new Pen(ink, 30 * k))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, new PointF[] { new PointF(L, T + arm), new PointF(L, T), new PointF(L + arm, T) });
            g.DrawLines(pen, new PointF[] { new PointF(R - arm, T), new PointF(R, T), new PointF(R, T + arm) });
            g.DrawLines(pen, new PointF[] { new PointF(R, B - arm), new PointF(R, B), new PointF(R - arm, B) });
            g.DrawLines(pen, new PointF[] { new PointF(L + arm, B), new PointF(L, B), new PointF(L, B - arm) });
            float d = 44 * k;
            g.FillEllipse(ink, c + 14 * k - d / 2, m + 40 * k - d / 2, d, d);
        }
    }

    static void DrawCard(Graphics g, int S, float cx, float cy, float angle, float w, float h, float r, Color fill, Color shadow, float dy, float blur)
    {
        using (GraphicsPath p = Round(new RectangleF(cx - w / 2, cy - h / 2, w, h), r))
        using (Matrix m = new Matrix())
        {
            m.RotateAt(angle, new PointF(cx, cy));
            p.Transform(m);
            if (shadow.A > 0) Shadow(g, p, S, 0, dy, blur, shadow);
            if (fill == Color.White)
            {
                RectangleF b = p.GetBounds();
                using (LinearGradientBrush br = new LinearGradientBrush(new PointF(0, b.Top), new PointF(0, b.Bottom), Color.White, Hex("EEF1FF")))
                    g.FillPath(br, p);
            }
            else using (SolidBrush br = new SolidBrush(fill)) g.FillPath(br, p);
        }
    }

    // Sombra suave de verdad: la forma se pinta en una máscara pequeña, se difumina (tres pasadas de caja ≈ gaussiana)
    // y se pinta ampliada y teñida debajo de la forma.
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

    // Superelipse |x|^n + |y|^n = 1: esquinas de curvatura continua.
    static GraphicsPath Squircle(float cx, float cy, float half, double n)
    {
        PointF[] pts = new PointF[720];
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

    static Color Hex(string h)
    {
        return Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
    }
}
