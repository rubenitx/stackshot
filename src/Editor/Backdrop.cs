// Stackshot - Fondos de presentación (como CleanShot X): la captura sobre un degradado, con margen, esquinas
// redondeadas y sombra. Sirve para imágenes (Compose) y para preparar las capas de un vídeo.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Stackshot
{
    // Escalas de los ajustes:
    //   BgPadding 0-100 (por defecto 50): margen = BgPadding % de max(20 % del lado medio de la imagen, 80 px).
    //   BgRadius  0-100 (por defecto 40): radio  = BgRadius % de max(5 % del lado medio de la imagen, 28 px).
    // "Lado medio" = raíz de ancho × alto: así una captura pequeña y una grande quedan con las mismas proporciones.
    public static class Backdrop
    {
        class Preset
        {
            public string Name;
            public Color[] Stops;
            public float Angle;
            public bool Wallpaper;
            public Preset(string name, float angle, params Color[] stops) { Name = name; Angle = angle; Stops = stops; }
        }

        static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        static readonly Preset[] presets = Build();

        static Preset[] Build()
        {
            Preset desk = new Preset("Tu escritorio", 45, C(0x4F7BFF), C(0x8B5CF6));
            desk.Wallpaper = true;
            return new Preset[]
            {
                new Preset("Stackshot", 45, C(0x8B5CF6), C(0x4F7BFF), C(0x14B8E6)),
                new Preset("Atardecer", 45, C(0xFF5F6D), C(0xFF9A5A), C(0xFFC371)),
                new Preset("Menta", 45, C(0x0BA360), C(0x3CBA92), C(0x8EE4AF)),
                new Preset("Oc\u00E9ano", 45, C(0x2E3192), C(0x1C8DD8), C(0x1BFFFF)),
                new Preset("Melocot\u00F3n", 45, C(0xFFD3A5), C(0xFDA085), C(0xFD6585)),
                new Preset("Lavanda", 45, C(0xA18CD1), C(0xC9A7EB), C(0xFBC2EB)),
                new Preset("Rosa", 45, C(0xF857A6), C(0xFF5858)),
                new Preset("Aurora", 60, C(0x00C9A7), C(0x4D8AF0), C(0x845EC2)),
                new Preset("Noche", 45, C(0x0F2027), C(0x203A43), C(0x2C5364)),
                new Preset("Grafito", 90, C(0x3A3D45), C(0x1F2126)),
                new Preset("Claro", 90, C(0xF5F5F7), C(0xE6E6EB)),
                new Preset("Oscuro", 90, C(0x2A2A30), C(0x161619)),
                desk
            };
        }

        public static int Count { get { return presets.Length; } }

        public static string Name(int i)
        {
            return presets[Clamp(i)].Name;
        }

        static int Clamp(int i) { return Math.Max(0, Math.Min(presets.Length - 1, i)); }

        // ------------------------------------------------------------ Medidas

        static double Unit(Size content) { return Math.Sqrt(Math.Max(1.0, (double)content.Width * content.Height)); }

        public static int PaddingFor(Size content, Settings s)
        {
            return (int)Math.Round(Math.Max(0, Math.Min(100, s.BgPadding)) / 100.0 * Math.Max(0.2 * Unit(content), 80));
        }

        public static int RadiusFor(Size content, Settings s)
        {
            int r = (int)Math.Round(Math.Max(0, Math.Min(100, s.BgRadius)) / 100.0 * Math.Max(0.05 * Unit(content), 28));
            return Math.Min(r, Math.Min(content.Width, content.Height) / 2);
        }

        // Tamaño del lienzo y sitio de la imagen dentro. even: medidas pares (lo pide H.264).
        public static void Measure(Size content, Settings s, bool even, out Size frame, out Rectangle inner)
        {
            int pad = PaddingFor(content, s);
            int w = content.Width + 2 * pad, h = content.Height + 2 * pad;
            double ratio = 0;
            switch (s.BgRatio)
            {
                case "16:9": ratio = 16.0 / 9; break;
                case "4:3": ratio = 4.0 / 3; break;
                case "1:1": ratio = 1; break;
            }
            if (ratio > 0)
            {
                if (w / (double)h < ratio) w = (int)Math.Ceiling(h * ratio);
                else h = (int)Math.Ceiling(w / ratio);
            }
            if (even) { w += w & 1; h += h & 1; }
            frame = new Size(w, h);
            int x = (w - content.Width) / 2, y = (h - content.Height) / 2;
            if (even) { x &= ~1; y &= ~1; }
            inner = new Rectangle(x, y, content.Width, content.Height);
        }

        // ------------------------------------------------------------ Dibujo

        // La muestra de un fondo, para los selectores.
        public static void PaintSwatch(Graphics g, Rectangle r, int preset)
        {
            Paint(g, r, Clamp(preset), true);
        }

        static void Paint(Graphics g, Rectangle r, int preset, bool small)
        {
            if (r.Width < 1 || r.Height < 1) return;
            Preset p = presets[preset];
            if (p.Wallpaper)
            {
                Bitmap wp = Wallpaper();
                if (wp != null)
                {
                    // Cubre todo el lienzo; como la imagen es diminuta, al ampliarla queda desenfocada.
                    float k = Math.Max(r.Width / (float)wp.Width, r.Height / (float)wp.Height);
                    float w = wp.Width * k, h = wp.Height * k;
                    InterpolationMode im = g.InterpolationMode;
                    PixelOffsetMode pm = g.PixelOffsetMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(wp, new Rectangle((int)(r.X + (r.Width - w) / 2), (int)(r.Y + (r.Height - h) / 2), (int)Math.Ceiling(w), (int)Math.Ceiling(h)),
                                    0, 0, wp.Width, wp.Height, GraphicsUnit.Pixel, ia);
                    }
                    g.InterpolationMode = im;
                    g.PixelOffsetMode = pm;
                    return;
                }
            }
            RectangleF rf = new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
            using (LinearGradientBrush b = new LinearGradientBrush(rf, p.Stops[0], p.Stops[p.Stops.Length - 1], p.Angle, true))
            {
                if (p.Stops.Length > 2)
                {
                    ColorBlend cb = new ColorBlend(p.Stops.Length);
                    for (int i = 0; i < p.Stops.Length; i++) cb.Positions[i] = i / (float)(p.Stops.Length - 1);
                    cb.Colors = p.Stops;
                    b.InterpolationColors = cb;
                }
                g.FillRectangle(b, r);
            }
            if (small) return;
            // Un brillo suave arriba a la izquierda, para que no parezca un degradado plano.
            RectangleF glow = new RectangleF(r.X - r.Width * 0.25f, r.Y - r.Height * 0.45f, r.Width * 0.9f, r.Height * 0.9f);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(glow);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(46, 255, 255, 255);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillEllipse(pb, glow);
                }
            }
        }

        // Fondo y sombra, sin la imagen: para la vista previa del editor y para los vídeos.
        public static Bitmap Background(Size frame, Rectangle inner, Settings s, int radius)
        {
            Bitmap b = new Bitmap(Math.Max(1, frame.Width), Math.Max(1, frame.Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Paint(g, new Rectangle(0, 0, b.Width, b.Height), Clamp(s.BgPreset), false);
                if (s.BgShadow) Shadow(g, inner, radius);
            }
            return b;
        }

        // La captura con su fondo, lista para copiar o guardar.
        public static Bitmap Compose(Bitmap content, Settings s)
        {
            Size frame;
            Rectangle inner;
            Measure(content.Size, s, false, out frame, out inner);
            int radius = RadiusFor(content.Size, s);
            Bitmap b = Background(frame, inner, s, radius);
            using (Bitmap round = Rounded(content, radius))
            using (Graphics g = Graphics.FromImage(b))
                g.DrawImage(round, inner.X, inner.Y, round.Width, round.Height);
            return b;
        }

        // Copia con las esquinas redondeadas y suavizadas: solo se tocan los píxeles de las cuatro esquinas, así que
        // es casi tan rápido como copiar la imagen (rellenar un trazado con la imagen como textura es mucho más lento).
        public static Bitmap Rounded(Bitmap src, float radius)
        {
            Bitmap b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            int r = (int)Math.Ceiling(Math.Min(radius, Math.Min(src.Width, src.Height) / 2f));
            if (r < 1) return b;
            BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int[] row = new int[r];
                for (int corner = 0; corner < 4; corner++)
                {
                    int x0 = corner % 2 == 0 ? 0 : b.Width - r, y0 = corner < 2 ? 0 : b.Height - r;
                    for (int y = y0; y < y0 + r; y++)
                    {
                        IntPtr p = new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride + x0 * 4);
                        Marshal.Copy(p, row, 0, r);
                        for (int i = 0; i < r; i++)
                        {
                            float m = Coverage(x0 + i, y, b.Width, b.Height, radius);
                            if (m >= 1) continue;
                            int c = row[i];
                            // Premultiplicado: los cuatro canales se escalan igual.
                            row[i] = ((int)(((c >> 24) & 255) * m + 0.5f) << 24) | ((int)(((c >> 16) & 255) * m + 0.5f) << 16) |
                                     ((int)(((c >> 8) & 255) * m + 0.5f) << 8) | (int)((c & 255) * m + 0.5f);
                        }
                        Marshal.Copy(row, 0, p, r);
                    }
                }
            }
            finally { b.UnlockBits(bd); }
            return b;
        }

        // Dos sombras como en macOS: una amplia y suave que separa la ventana del fondo y otra corta que la asienta.
        static void Shadow(Graphics g, Rectangle inner, int radius)
        {
            double u = Unit(inner.Size);
            float wide = (float)Math.Max(6, u * 0.045), tight = (float)Math.Max(2, u * 0.012);
            Blurred(g, inner, radius, wide, wide * 0.42f, 0.42f);
            Blurred(g, inner, radius, tight, tight * 0.5f, 0.30f);
        }

        // Sombra difuminada de verdad: se dibuja a escala reducida, se desenfoca (tres cajas ≈ gaussiana) y se amplía.
        static void Blurred(Graphics g, Rectangle inner, int radius, float blur, float offY, float alpha)
        {
            float d = Math.Max(3f, blur / 3.5f);         // reducción: el desenfoque se hace en pequeño
            int margin = (int)Math.Ceiling(blur * 2.2f);
            Rectangle area = Rectangle.Inflate(inner, margin, margin);
            int sw = Math.Max(4, (int)Math.Ceiling(area.Width / d)), sh = Math.Max(4, (int)Math.Ceiling(area.Height / d));
            using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
            {
                using (Graphics sg = Graphics.FromImage(small))
                {
                    sg.SmoothingMode = SmoothingMode.AntiAlias;
                    RectangleF r = new RectangleF(margin / d, margin / d, inner.Width / d, inner.Height / d);
                    using (GraphicsPath p = Theme.Round(r, radius / d))
                    using (SolidBrush br = new SolidBrush(Color.Black)) sg.FillPath(br, p);
                }
                BlurAlpha(small, Math.Max(1, (int)Math.Round(blur / d * 0.55f)), alpha);
                // El borde de la miniatura es transparente (el margen lo cubre): basta un DrawImage normal, que es rápido.
                InterpolationMode im = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear; // al ampliar es suave y, en GDI+, más rápido que el normal
                g.DrawImage(small, new Rectangle(area.X, (int)Math.Round(area.Y + offY), area.Width, area.Height));
                g.InterpolationMode = im;
            }
        }

        // Desenfoca el canal alfa con tres pasadas de caja (horizontal y vertical) y lo atenúa; el color queda negro.
        static void BlurAlpha(Bitmap b, int r, float alpha)
        {
            int w = b.Width, h = b.Height;
            BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int[] px = new int[w * h];
                Marshal.Copy(bd.Scan0, px, 0, px.Length);
                float[] a = new float[w * h], t = new float[w * h];
                for (int i = 0; i < px.Length; i++) a[i] = (px[i] >> 24) & 255;
                for (int pass = 0; pass < 3; pass++)
                {
                    Box(a, t, w, h, r, true);
                    Box(t, a, w, h, r, false);
                }
                for (int i = 0; i < px.Length; i++) px[i] = (int)Math.Max(0, Math.Min(255, a[i] * alpha + 0.5f)) << 24;
                Marshal.Copy(px, 0, bd.Scan0, px.Length);
            }
            finally { b.UnlockBits(bd); }
        }

        static void Box(float[] src, float[] dst, int w, int h, int r, bool horizontal)
        {
            int n = horizontal ? w : h, lines = horizontal ? h : w;
            float inv = 1f / (2 * r + 1);
            for (int l = 0; l < lines; l++)
            {
                int start = horizontal ? l * w : l, step = horizontal ? 1 : w;
                float sum = 0;
                for (int i = -r; i <= r; i++) sum += src[start + Math.Max(0, Math.Min(n - 1, i)) * step];
                for (int i = 0; i < n; i++)
                {
                    dst[start + i * step] = sum * inv;
                    int add = Math.Min(n - 1, i + r + 1), sub = Math.Max(0, i - r);
                    sum += src[start + add * step] - src[start + sub * step];
                }
            }
        }

        // ------------------------------------------------------------ Vídeo

        // La capa de encima para un vídeo: el fondo con un hueco de esquinas redondeadas donde va el vídeo y las
        // marcas dentro de ese hueco. FFmpeg pone el vídeo debajo y esta imagen encima. overlay: las marcas
        // (tamaño del vídeo recortado, transparente) o null.
        public static Bitmap VideoTop(Size frame, Rectangle inner, Settings s, int radius, Bitmap overlay)
        {
            Bitmap bg = Background(frame, inner, s, radius);
            Bitmap top = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(top))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(bg, 0, 0);
            }
            bg.Dispose();
            Bitmap marks = null;
            if (overlay != null)
            {
                marks = new Bitmap(inner.Width, inner.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(marks)) g.DrawImageUnscaled(overlay, 0, 0);
            }
            BitmapData td = top.LockBits(new Rectangle(0, 0, top.Width, top.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            BitmapData md = marks != null ? marks.LockBits(new Rectangle(0, 0, marks.Width, marks.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb) : null;
            try
            {
                int[] row = new int[inner.Width], mrow = new int[inner.Width];
                for (int y = 0; y < inner.Height; y++)
                {
                    IntPtr tp = new IntPtr(td.Scan0.ToInt64() + (long)(inner.Y + y) * td.Stride + inner.X * 4);
                    Marshal.Copy(tp, row, 0, row.Length);
                    if (md != null) Marshal.Copy(new IntPtr(md.Scan0.ToInt64() + (long)y * md.Stride), mrow, 0, mrow.Length);
                    for (int x = 0; x < inner.Width; x++)
                    {
                        float m = Coverage(x, y, inner.Width, inner.Height, radius);
                        int bgc = row[x], mk = md != null ? mrow[x] : 0;
                        float ba = ((bgc >> 24) & 255) / 255f * (1 - m), ma = ((mk >> 24) & 255) / 255f * m;
                        float oa = ba + ma;
                        if (oa <= 0.0001f) { row[x] = 0; continue; }
                        int r = (int)((((bgc >> 16) & 255) * ba + ((mk >> 16) & 255) * ma) / oa + 0.5f);
                        int gg = (int)((((bgc >> 8) & 255) * ba + ((mk >> 8) & 255) * ma) / oa + 0.5f);
                        int bb = (int)(((bgc & 255) * ba + (mk & 255) * ma) / oa + 0.5f);
                        row[x] = ((int)(Math.Min(1f, oa) * 255 + 0.5f) << 24) | (Math.Min(255, r) << 16) | (Math.Min(255, gg) << 8) | Math.Min(255, bb);
                    }
                    Marshal.Copy(row, 0, tp, row.Length);
                }
            }
            finally
            {
                top.UnlockBits(td);
                if (md != null) marks.UnlockBits(md);
                if (marks != null) marks.Dispose();
            }
            return top;
        }

        // Cuánto del píxel (x, y) cae dentro del rectángulo redondeado (0 fuera, 1 dentro, con borde suave).
        static float Coverage(int x, int y, int w, int h, float r)
        {
            if (r < 1) return 1;
            float px = x + 0.5f, py = y + 0.5f, cx, cy;
            if (px < r) cx = r; else if (px > w - r) cx = w - r; else return 1;
            if (py < r) cy = r; else if (py > h - r) cy = h - r; else return 1;
            float dx = px - cx, dy = py - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            return Math.Max(0, Math.Min(1, r - dist + 0.5f));
        }

        // ------------------------------------------------------------ Fondo de escritorio

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action, int param, StringBuilder value, int winIni);

        static Bitmap wallpaper;
        static string wallpaperFor;
        static DateTime wallpaperStamp;
        static readonly object wallpaperLock = new object();
        static DateTime wallpaperChecked;

        // Leer el fondo de escritorio la primera vez cuesta unos cientos de ms: se hace en otro hilo al arrancar,
        // para que abrir el editor o los ajustes no se note.
        public static void Prewarm()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Wallpaper(); });
        }

        // Para las imágenes del README (tools\Studio.cs): nunca el fondo de escritorio de quien las genera.
        public static bool NoWallpaper;

        static Bitmap Wallpaper()
        {
            if (NoWallpaper) return null;
            lock (wallpaperLock) return LoadWallpaper();
        }

        // El fondo de escritorio en miniatura (112 px): ampliado queda desenfocado, como un cristal esmerilado.
        static Bitmap LoadWallpaper()
        {
            try
            {
                // Se pinta muchas veces seguidas (una por muestra): preguntar a Windows y al disco, como mucho cada 2 s.
                if (wallpaper != null && (DateTime.UtcNow - wallpaperChecked).TotalSeconds < 2) return wallpaper;
                wallpaperChecked = DateTime.UtcNow;
                StringBuilder sb = new StringBuilder(520);
                if (!SystemParametersInfo(0x73, sb.Capacity, sb, 0)) return null; // SPI_GETDESKWALLPAPER
                string path = sb.ToString();
                if (path.Length == 0 || !File.Exists(path)) return null;
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (wallpaper != null && path == wallpaperFor && stamp == wallpaperStamp) return wallpaper;
                Bitmap small;
                using (Bitmap full = ShotStack.LoadFull(path))
                {
                    int w = 112, h = Math.Max(1, (int)Math.Round(112.0 * full.Height / full.Width));
                    small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(full, 0, 0, w, h);
                    }
                }
                if (wallpaper != null) wallpaper.Dispose();
                wallpaper = small;
                wallpaperFor = path;
                wallpaperStamp = stamp;
                return wallpaper;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Fondo de escritorio: " + ex.Message);
                return null;
            }
        }
    }
}
