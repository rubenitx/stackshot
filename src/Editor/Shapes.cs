// Stackshot - Marcas del editor y cómo se dibujan.
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
    // ---------------------------------------------------------------- Editor rápido

    public enum Tool { Arrow, Rect, Ellipse, Text, Counter, Highlight, Pixelate, Crop }

    // Una marca, en coordenadas de la imagen original.
    public class Shape
    {
        public Tool Kind;
        public Color Color;
        public int Weight = 1;      // 0 fino, 1 medio, 2 grueso
        public float Width;         // grosor del trazo
        public float FontPx;        // texto y números
        public PointF A, B;
        public PointF C;            // flechas curvas: punto de control de la curva (de A a B)
        public bool Curved;
        public string Text;
        public int Number;
        public Bitmap Cache;        // pixelado ya calculado: solo lo tiene la marca viva, nunca las copias del historial
        public RectangleF CacheBox;

        public RectangleF Box
        {
            get
            {
                RectangleF b = RectangleF.FromLTRB(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y), Math.Max(A.X, B.X), Math.Max(A.Y, B.Y));
                if (Kind != Tool.Arrow || !Curved) return b;
                foreach (PointF p in Curve(24)) b = RectangleF.Union(b, new RectangleF(p.X, p.Y, 0.01f, 0.01f));
                return b;
            }
        }

        // Punto de la flecha a mitad de camino: de ahí se tira para curvarla.
        public PointF Mid
        {
            get
            {
                if (!Curved) return new PointF((A.X + B.X) / 2, (A.Y + B.Y) / 2);
                return new PointF(0.25f * A.X + 0.5f * C.X + 0.25f * B.X, 0.25f * A.Y + 0.5f * C.Y + 0.25f * B.Y);
            }
        }

        // Curva la flecha para que pase por p. Si p queda casi en la recta, vuelve a ser recta.
        public void SetMid(PointF p, float snap)
        {
            PointF m = new PointF((A.X + B.X) / 2, (A.Y + B.Y) / 2);
            float dx = p.X - m.X, dy = p.Y - m.Y;
            if (dx * dx + dy * dy < snap * snap) { Curved = false; return; }
            Curved = true;
            C = new PointF(2 * p.X - m.X, 2 * p.Y - m.Y);
        }

        public void Offset(Shape from, float dx, float dy)
        {
            A = new PointF(from.A.X + dx, from.A.Y + dy);
            B = new PointF(from.B.X + dx, from.B.Y + dy);
            C = new PointF(from.C.X + dx, from.C.Y + dy);
        }

        // Puntos a lo largo de la flecha (curva de Bézier cuadrática).
        public PointF[] Curve(int n)
        {
            PointF[] pts = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                pts[i] = Curved
                    ? new PointF(u * u * A.X + 2 * u * t * C.X + t * t * B.X, u * u * A.Y + 2 * u * t * C.Y + t * t * B.Y)
                    : new PointF(A.X + (B.X - A.X) * t, A.Y + (B.Y - A.Y) * t);
            }
            return pts;
        }

        public Shape Clone()
        {
            Shape c = (Shape)MemberwiseClone();
            c.Cache = null;
            return c;
        }

        public void DropCache()
        {
            if (Cache != null) { Cache.Dispose(); Cache = null; }
        }
    }

    public static class Painter
    {
        static readonly Graphics measure = Graphics.FromImage(new Bitmap(1, 1));

        public static Color Contrast(Color c)
        {
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 150 ? Color.Black : Color.White;
        }

        static StringFormat TextFormat()
        {
            StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            return sf;
        }

        // La caja de un texto con su fondo, en coordenadas de la imagen.
        public static RectangleF TextBounds(Shape s)
        {
            using (Font f = new Font("Segoe UI Semibold", s.FontPx, GraphicsUnit.Pixel))
            using (StringFormat sf = TextFormat())
            {
                SizeF sz = measure.MeasureString(s.Text ?? "", f, PointF.Empty, sf);
                float pad = s.FontPx * 0.35f;
                return new RectangleF(s.A.X, s.A.Y, sz.Width + pad * 2, sz.Height + pad * 2);
            }
        }

        public static float CounterRadius(Shape s) { return s.FontPx * 0.8f; }

        // Lo que ocupa una marca: para seleccionarla y dibujar su contorno.
        public static RectangleF Bounds(Shape s)
        {
            RectangleF b;
            switch (s.Kind)
            {
                case Tool.Text:
                    return TextBounds(s);
                case Tool.Counter:
                    float r = CounterRadius(s);
                    return new RectangleF(s.A.X - r, s.A.Y - r, 2 * r, 2 * r);
                case Tool.Arrow:
                    b = s.Box;
                    b.Inflate(s.Width * 2.5f, s.Width * 2.5f);
                    return b;
                default:
                    b = s.Box;
                    b.Inflate(s.Width / 2, s.Width / 2);
                    return b;
            }
        }

        // Flecha afilada, como las de CleanShot: cola fina, cuerpo que se ensancha y punta ancha.
        public static GraphicsPath ArrowPath(PointF a, PointF b, float w)
        {
            GraphicsPath p = new GraphicsPath();
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) { p.AddEllipse(a.X - w / 2, a.Y - w / 2, w, w); return p; }
            float ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
            float head = Math.Min(len * 0.5f, w * 4.4f), headW = Math.Min(head * 0.58f, w * 2.5f);
            float neck = Math.Min(headW * 0.5f, w * 0.62f), tail = w * 0.22f;
            PointF c = new PointF(b.X - ux * head, b.Y - uy * head);
            p.AddPolygon(new PointF[]
            {
                new PointF(a.X + nx * tail, a.Y + ny * tail),
                new PointF(c.X + nx * neck, c.Y + ny * neck),
                new PointF(c.X + nx * headW, c.Y + ny * headW),
                b,
                new PointF(c.X - nx * headW, c.Y - ny * headW),
                new PointF(c.X - nx * neck, c.Y - ny * neck),
                new PointF(a.X - nx * tail, a.Y - ny * tail)
            });
            return p;
        }

        // La misma flecha afilada, pero siguiendo una curva: el cuerpo se ensancha a lo largo de ella y la punta
        // mira hacia donde llega la curva.
        public static GraphicsPath ArrowPath(Shape s)
        {
            if (!s.Curved) return ArrowPath(s.A, s.B, s.Width);
            const int n = 48;
            PointF[] pts = s.Curve(n);
            float[] len = new float[n + 1];
            for (int i = 1; i <= n; i++)
            {
                float dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y;
                len[i] = len[i - 1] + (float)Math.Sqrt(dx * dx + dy * dy);
            }
            float total = len[n], w = s.Width;
            if (total < 1) return ArrowPath(s.A, s.B, w);
            float head = Math.Min(total * 0.5f, w * 4.4f), headW = Math.Min(head * 0.58f, w * 2.5f);
            float neck = Math.Min(headW * 0.5f, w * 0.62f), tail = w * 0.22f, baseLen = total - head;
            int k = 0;
            while (k < n - 1 && len[k + 1] < baseLen) k++;
            float seg = Math.Max(1e-4f, len[k + 1] - len[k]), f = (baseLen - len[k]) / seg;
            PointF bp = new PointF(pts[k].X + (pts[k + 1].X - pts[k].X) * f, pts[k].Y + (pts[k + 1].Y - pts[k].Y) * f);
            float hx = s.B.X - bp.X, hy = s.B.Y - bp.Y, hl = (float)Math.Max(1e-4, Math.Sqrt(hx * hx + hy * hy));
            float hnx = -hy / hl, hny = hx / hl;
            System.Collections.Generic.List<PointF> left = new System.Collections.Generic.List<PointF>(), right = new System.Collections.Generic.List<PointF>();
            for (int i = 0; i <= k; i++)
            {
                PointF a = pts[Math.Max(0, i - 1)], b = pts[Math.Min(n, i + 1)];
                float tx = b.X - a.X, ty = b.Y - a.Y, tl = (float)Math.Max(1e-4, Math.Sqrt(tx * tx + ty * ty));
                float nx = -ty / tl, ny = tx / tl, wi = tail + (neck - tail) * Math.Min(1, len[i] / Math.Max(1e-4f, baseLen));
                left.Add(new PointF(pts[i].X + nx * wi, pts[i].Y + ny * wi));
                right.Add(new PointF(pts[i].X - nx * wi, pts[i].Y - ny * wi));
            }
            left.Add(new PointF(bp.X + hnx * neck, bp.Y + hny * neck));
            left.Add(new PointF(bp.X + hnx * headW, bp.Y + hny * headW));
            left.Add(s.B);
            left.Add(new PointF(bp.X - hnx * headW, bp.Y - hny * headW));
            left.Add(new PointF(bp.X - hnx * neck, bp.Y - hny * neck));
            for (int i = right.Count - 1; i >= 0; i--) left.Add(right[i]);
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(left.ToArray());
            return p;
        }

        // Lleva p de la recta (a0, b0) a la recta (a1, b1): al estirar una flecha curva, la curva conserva su forma.
        public static PointF Similar(PointF p, PointF a0, PointF b0, PointF a1, PointF b1)
        {
            float dx0 = b0.X - a0.X, dy0 = b0.Y - a0.Y, dx1 = b1.X - a1.X, dy1 = b1.Y - a1.Y, den = dx0 * dx0 + dy0 * dy0;
            if (den < 1e-6f) return p;
            float rr = (dx1 * dx0 + dy1 * dy0) / den, ri = (dy1 * dx0 - dx1 * dy0) / den;
            float px = p.X - a0.X, py = p.Y - a0.Y;
            return new PointF(a1.X + px * rr - py * ri, a1.Y + px * ri + py * rr);
        }

        public static void Draw(Graphics g, Shape s, Bitmap img)
        {
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = s.Box;
            float sh = Math.Max(1f, s.Width * 0.45f);
            Color shadow = Color.FromArgb(80, 0, 0, 0);
            switch (s.Kind)
            {
                case Tool.Rect:
                    using (GraphicsPath p = Theme.Round(r, s.Width * 1.1f))
                    using (Pen ps = new Pen(shadow, s.Width))
                    using (Pen pen = new Pen(s.Color, s.Width))
                    {
                        ps.LineJoin = LineJoin.Round;
                        pen.LineJoin = LineJoin.Round;
                        g.TranslateTransform(sh, sh);
                        g.DrawPath(ps, p);
                        g.TranslateTransform(-sh, -sh);
                        g.DrawPath(pen, p);
                    }
                    break;
                case Tool.Ellipse:
                    using (Pen ps = new Pen(shadow, s.Width))
                    using (Pen pen = new Pen(s.Color, s.Width))
                    {
                        g.DrawEllipse(ps, r.X + sh, r.Y + sh, r.Width, r.Height);
                        g.DrawEllipse(pen, r.X, r.Y, r.Width, r.Height);
                    }
                    break;
                case Tool.Arrow:
                    using (GraphicsPath p = ArrowPath(s))
                    using (SolidBrush bs = new SolidBrush(shadow))
                    using (SolidBrush b = new SolidBrush(s.Color))
                    {
                        g.TranslateTransform(sh, sh);
                        g.FillPath(bs, p);
                        g.TranslateTransform(-sh, -sh);
                        g.FillPath(b, p);
                    }
                    break;
                case Tool.Highlight:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(95, s.Color))) g.FillRectangle(b, r);
                    break;
                case Tool.Pixelate:
                    Rectangle ri = Rectangle.Round(r);
                    ri.Intersect(new Rectangle(0, 0, img.Width, img.Height));
                    if (ri.Width < 2 || ri.Height < 2) break;
                    RectangleF rf = ri;
                    if (s.Cache == null || s.CacheBox != rf)
                    {
                        s.DropCache();
                        s.Cache = Pixelate(img, ri);
                        s.CacheBox = rf;
                    }
                    if (s.Cache != null)
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(s.Cache, ri);
                    }
                    break;
                case Tool.Text:
                    using (Font f = new Font("Segoe UI Semibold", s.FontPx, GraphicsUnit.Pixel))
                    using (StringFormat sf = TextFormat())
                    {
                        SizeF sz = g.MeasureString(s.Text, f, PointF.Empty, sf);
                        float pad = s.FontPx * 0.35f;
                        RectangleF box = new RectangleF(s.A.X, s.A.Y, sz.Width + pad * 2, sz.Height + pad * 2);
                        RectangleF shb = box;
                        shb.Offset(sh, sh);
                        using (GraphicsPath p = Theme.Round(shb, pad))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillPath(b, p);
                        using (GraphicsPath p = Theme.Round(box, pad))
                        using (SolidBrush b = new SolidBrush(s.Color)) g.FillPath(b, p);
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        using (SolidBrush t = new SolidBrush(Contrast(s.Color))) g.DrawString(s.Text, f, t, s.A.X + pad, s.A.Y + pad, sf);
                    }
                    break;
                case Tool.Counter:
                    float rad = CounterRadius(s);
                    RectangleF c = new RectangleF(s.A.X - rad, s.A.Y - rad, 2 * rad, 2 * rad);
                    using (SolidBrush bs = new SolidBrush(shadow)) g.FillEllipse(bs, c.X + sh, c.Y + sh, c.Width, c.Height);
                    using (SolidBrush b = new SolidBrush(s.Color)) g.FillEllipse(b, c);
                    Color ink = Contrast(s.Color);
                    using (Pen ring = new Pen(Color.FromArgb(220, ink == Color.White ? Color.White : Color.FromArgb(40, 40, 50)), Math.Max(1.5f, rad * 0.12f)))
                        g.DrawEllipse(ring, c.X, c.Y, c.Width, c.Height);
                    using (Font f = new Font("Segoe UI", rad * (s.Number >= 10 ? 0.8f : 1.05f), FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    using (SolidBrush t = new SolidBrush(ink))
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        RectangleF tc = c;
                        tc.Offset(0, rad * 0.04f);
                        g.DrawString(s.Number.ToString(), f, t, tc, sf);
                    }
                    break;
            }
            g.Restore(state);
        }

        public static Bitmap Pixelate(Bitmap src, Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            if (r.Width < 2 || r.Height < 2) return null;
            int block = Math.Max(8, Math.Min(src.Width, src.Height) / 60);
            int sw = Math.Max(1, r.Width / block), sh = Math.Max(1, r.Height / block);
            using (Bitmap small = new Bitmap(sw, sh))
            {
                using (Graphics g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(src, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
                }
                Bitmap patch = new Bitmap(r.Width, r.Height);
                using (Graphics g = Graphics.FromImage(patch))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(small, new Rectangle(0, 0, r.Width, r.Height));
                }
                return patch;
            }
        }
    }

    // Lienzo: la captura encajada en la ventana y las marcas encima. Una marca ya hecha se puede pinchar para
    // moverla, tirar de sus puntos para cambiarla, borrarla con Supr o cambiarle el color y el grosor.
}
