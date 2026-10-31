// Stackshot - Presentation backdrops: the capture on a gradient with padding, rounded corners and shadow. Used for
// images (Compose) and video overlays.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    // Setting scales:
    // BgPadding 0-100 (default 50): padding = BgPadding% of max(20% of the mean side, 80 px).
    // BgRadius 0-100 (default 40): radius = BgRadius% of max(5% of the mean side, 28 px).
    // Mean side = sqrt(width x height), so small and large captures keep the same proportions.
    public static class Backdrop
    {
        class Preset
        {
            public string Name;
            public Color[] Stops;
            public float Angle;
            public bool Wallpaper;
            public string File;      // the user's own image (in CustomDir)
            public Preset(string name, float angle, params Color[] stops) { Name = name; Angle = angle; Stops = stops; }
        }

        static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        static Preset[] presets = Build();

        static Preset[] Build()
        {
            Preset desk = new Preset("Tu escritorio", 45, C(0x4F7BFF), C(0x8B5CF6));
            desk.Wallpaper = true;
            Preset[] builtIn =
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
            List<Preset> all = new List<Preset>(builtIn);
            int n = 1;
            foreach (string f in CustomFiles()) { Preset p = new Preset("Tu imagen " + n++, 0, C(0x3A3D45), C(0x1F2126)); p.File = f; all.Add(p); }
            return all.ToArray();
        }

        public static int Count { get { return presets.Length; } }

        public static string Name(int i)
        {
            int idx = Clamp(i);
            string n = presets[idx].Name;
            if (I18n.IsSpanish) return n;
            switch (idx)
            {
                case 0: return "Stackshot";
                case 1: return "Sunset";
                case 2: return "Mint";
                case 3: return "Ocean";
                case 4: return "Peach";
                case 5: return "Lavender";
                case 6: return "Rose";
                case 7: return "Aurora";
                case 8: return "Night";
                case 9: return "Graphite";
                case 10: return "Light";
                case 11: return "Dark";
                case 12: return "Your desktop";
            }
            if (n != null && n.StartsWith("Tu imagen "))
                return "Your image " + n.Substring("Tu imagen ".Length);
            return n;
        }

        static int Clamp(int i) { return Math.Max(0, Math.Min(presets.Length - 1, i)); }

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

        // Canvas size and image placement. even: even dimensions (required by H.264).
        public static void Measure(Size content, Settings s, bool even, out Size frame, out Rectangle inner)
        {
            Rectangle card;
            Measure(content, s, even, Padding.Empty, out frame, out card, out inner);
        }

        // With an inset: the card (capture plus its border in the edge colour) carries the corners and the shadow, and
        // the capture sits inside it. Alignment other than centred puts the card against those edges of the frame.
        public static void Measure(Size content, Settings s, bool even, Padding inset, out Size frame, out Rectangle card, out Rectangle inner)
        {
            Size cs = new Size(content.Width + inset.Horizontal, content.Height + inset.Vertical);
            int pad = PaddingFor(cs, s);
            int w = cs.Width + 2 * pad, h = cs.Height + 2 * pad;
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
            int col = AlignOf(s) % 3, row = AlignOf(s) / 3;
            int x = col == 0 ? 0 : col == 2 ? w - cs.Width : (w - cs.Width) / 2;
            int y = row == 0 ? 0 : row == 2 ? h - cs.Height : (h - cs.Height) / 2;
            if (even) { x &= ~1; y &= ~1; }
            card = new Rectangle(x, y, cs.Width, cs.Height);
            inner = new Rectangle(x + inset.Left, y + inset.Top, content.Width, content.Height);
        }

        static int AlignOf(Settings s) { return s.BgAlign >= 0 && s.BgAlign <= 8 ? s.BgAlign : 4; }

        // The shadow's strength, 0 when it is off.
        public static int ShadowLevel(Settings s) { return s.BgShadow ? Math.Max(0, Math.Min(100, s.BgShadowSize)) : 0; }

        // ---- What a backdrop takes from the capture itself: the inset around it (in its edge colour) and, for the
        // blurred backdrop, a small blurred copy. Made once per capture and crop; the editor's preview and the saved
        // image use the same one, so they look the same.
        public sealed class ShotInfo : IDisposable
        {
            public Padding Inset;
            public Color Edge = Color.White;
            public Bitmap Blur;      // small, already blurred; null unless the blurred backdrop is chosen
            public string Key = "";  // what it was made from, for the caches that depend on it
            internal string Base;    // the capture and the part of it read
            internal int[] Margins;  // its own margins in the edge colour, read once

            public void Dispose() { if (Blur != null) Blur.Dispose(); Blur = null; }
        }

        public static int InsetFor(Size content, Settings s)
        {
            return (int)Math.Round(Math.Max(0, Math.Min(100, s.BgInset)) / 100.0 * Math.Max(0.08 * Unit(content), 32));
        }

        // area: the part of img that is the capture (the crop). Never null.
        public static ShotInfo Study(Bitmap img, Rectangle area, Settings s) { return Study(img, area, s, null); }

        // last: the one made before; for the same capture and part it is updated and returned (what was read from the
        // pixels is kept), otherwise a new one is made and last is left to the caller.
        public static ShotInfo Study(Bitmap img, Rectangle area, Settings s, ShotInfo last)
        {
            area.Intersect(new Rectangle(0, 0, img.Width, img.Height));
            string from = img.GetHashCode() + "|" + area;
            ShotInfo l = last != null && last.Base == from ? last : new ShotInfo { Base = from };
            if (area.Width < 1 || area.Height < 1) return l;
            if ((s.BgInset > 0 || s.BgBalance) && l.Margins == null)
            {
                int[] read = new int[4];
                l.Edge = EdgeOf(img, area, read);
                l.Margins = read;
            }
            int[] m = s.BgBalance && l.Margins != null ? l.Margins : new int[4];
            int u = InsetFor(area.Size, s), most = Math.Max(Math.Max(m[0], m[1]), Math.Max(m[2], m[3]));
            // Balanced: each side gets what it lacks to match the widest of the capture's own margins.
            l.Inset = new Padding(u + most - m[0], u + most - m[1], u + most - m[2], u + most - m[3]);
            if (s.BgKind == 2) BlurOf(l, img, area);
            l.Key = from + "|" + l.Inset + "|" + l.Edge.ToArgb();
            return l;
        }

        // The small blurred copy of the part read, made the first time it is asked for (the sidebar's swatches or the
        // blurred backdrop itself).
        public static Bitmap BlurOf(ShotInfo info, Bitmap img, Rectangle area)
        {
            if (info.Blur != null) return info.Blur;
            area.Intersect(new Rectangle(0, 0, img.Width, img.Height));
            if (area.Width < 1 || area.Height < 1) return null;
            info.Blur = BlurSmall(img, area);
            return info.Blur;
        }

        // The most common colour along the capture's border, and (margins: left, top, right, bottom) how many lines
        // from each edge are of that colour alone, at most a quarter of the side.
        static Color EdgeOf(Bitmap img, Rectangle a, int[] margins)
        {
            BitmapData bd = img.LockBits(a, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int w = a.Width, h = a.Height;
                long scan = bd.Scan0.ToInt64(), stride = bd.Stride;
                Func<int, int, int> at = delegate(int x, int y) { return Marshal.ReadInt32(new IntPtr(scan + y * stride + x * 4L)); };
                Dictionary<int, int[]> buckets = new Dictionary<int, int[]>();
                int step = Math.Max(1, (2 * (w + h)) / 1600);
                Action<int> add = delegate(int c)
                {
                    int key = ((c >> 20) & 0xF) << 8 | ((c >> 12) & 0xF) << 4 | ((c >> 4) & 0xF);
                    int[] b;
                    if (!buckets.TryGetValue(key, out b)) buckets[key] = b = new int[4];
                    b[0]++; b[1] += (c >> 16) & 255; b[2] += (c >> 8) & 255; b[3] += c & 255;
                };
                for (int x = 0; x < w; x += step) { add(at(x, 0)); add(at(x, h - 1)); }
                for (int y = 0; y < h; y += step) { add(at(0, y)); add(at(w - 1, y)); }
                int[] best = null;
                foreach (int[] b in buckets.Values) if (best == null || b[0] > best[0]) best = b;
                Color edge = Color.FromArgb(best[1] / best[0], best[2] / best[0], best[3] / best[0]);
                if (margins == null) return edge;
                int er = edge.R, eg = edge.G, eb = edge.B;
                Func<int, bool> near = delegate(int c)
                {
                    return Math.Abs(((c >> 16) & 255) - er) <= 12 && Math.Abs(((c >> 8) & 255) - eg) <= 12 && Math.Abs((c & 255) - eb) <= 12;
                };
                // A line counts while nearly all of it (sampled) is the edge colour: antialiasing or one stray pixel
                // doesn't end it.
                Func<bool, int, bool> plain = delegate(bool column, int i)
                {
                    int n = column ? h : w, s = Math.Max(1, n / 400), all = 0, bad = 0;
                    for (int j = 0; j < n; j += s) { all++; if (!near(column ? at(i, j) : at(j, i))) bad++; }
                    return bad * 100 <= all;
                };
                int mw = w / 4, mh = h / 4;
                while (margins[0] < mw && plain(true, margins[0])) margins[0]++;
                while (margins[2] < mw && plain(true, w - 1 - margins[2])) margins[2]++;
                while (margins[1] < mh && plain(false, margins[1])) margins[1]++;
                while (margins[3] < mh && plain(false, h - 1 - margins[3])) margins[3]++;
                return edge;
            }
            finally { img.UnlockBits(bd); }
        }

        // The capture shrunk to a few dozen pixels and softened: upscaled to cover the frame it reads as frosted glass.
        // Each small pixel is the mean of its block, read from at most about a million samples and a band of rows at a
        // time, so a very tall capture costs no more than an ordinary one.
        public static Bitmap BlurSmall(Bitmap img, Rectangle a)
        {
            double k = 48.0 / Math.Max(a.Width, a.Height);
            int w = Math.Max(2, (int)Math.Round(a.Width * k)), h = Math.Max(2, (int)Math.Round(a.Height * k));
            int step = Math.Max(1, (int)Math.Sqrt((double)a.Width * a.Height / 1000000.0));
            double[] sum = new double[w * h * 3];
            int[] count = new int[w * h];
            int[] row = new int[a.Width];
            const int Band = 256;
            for (int y0 = 0; y0 < a.Height; y0 += Band)
            {
                int bh = Math.Min(Band, a.Height - y0);
                BitmapData src = img.LockBits(new Rectangle(a.X, a.Y + y0, a.Width, bh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    long scan = src.Scan0.ToInt64();
                    for (int y = y0 + (step - y0 % step) % step; y < y0 + bh; y += step)
                    {
                        Marshal.Copy(new IntPtr(scan + (long)(y - y0) * src.Stride), row, 0, a.Width);
                        int oy = Math.Min(h - 1, (int)((long)y * h / a.Height));
                        for (int x = 0; x < a.Width; x += step)
                        {
                            int c = row[x], i = oy * w + Math.Min(w - 1, (int)((long)x * w / a.Width));
                            sum[i * 3] += (c >> 16) & 255;
                            sum[i * 3 + 1] += (c >> 8) & 255;
                            sum[i * 3 + 2] += c & 255;
                            count[i]++;
                        }
                    }
                }
                finally { img.UnlockBits(src); }
            }
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int[] px = new int[w * h];
                float[][] ch = { new float[px.Length], new float[px.Length], new float[px.Length] };
                float[] t = new float[px.Length];
                double[] all = new double[3];
                long seen = 0;
                for (int i = 0; i < px.Length; i++) { all[0] += sum[i * 3]; all[1] += sum[i * 3 + 1]; all[2] += sum[i * 3 + 2]; seen += count[i]; }
                for (int i = 0; i < px.Length; i++)
                    for (int c = 0; c < 3; c++)
                        ch[c][i] = (float)(count[i] > 0 ? sum[i * 3 + c] / count[i] : all[c] / Math.Max(1, seen));   // a block no sample fell in: the mean
                foreach (float[] c in ch)
                    for (int pass = 0; pass < 2; pass++) { Box(c, t, w, h, 2, true); Box(t, c, w, h, 2, false); }
                for (int i = 0; i < px.Length; i++)
                    px[i] = (255 << 24) | ((int)Math.Min(255, ch[0][i] + 0.5f) << 16) | ((int)Math.Min(255, ch[1][i] + 0.5f) << 8) | (int)Math.Min(255, ch[2][i] + 0.5f);
                Marshal.Copy(px, 0, bd.Scan0, px.Length);
            }
            finally { b.UnlockBits(bd); }
            return b;
        }

        // ---- The user's own backgrounds: copies in %LOCALAPPDATA%\Stackshot\Fondos, at most 3840 px on the long side.
        public static readonly string CustomDir = Path.Combine(Settings.DataDir, "Fondos");
        static readonly object customLock = new object();
        static readonly Dictionary<string, Bitmap> thumbs = new Dictionary<string, Bitmap>();
        static string fullFor;
        static Bitmap full;

        static string[] CustomFiles()
        {
            try
            {
                if (!Directory.Exists(CustomDir)) return new string[0];
                List<string> files = new List<string>(Directory.GetFiles(CustomDir, "*.png"));
                files.Sort(delegate(string a, string b) { return File.GetCreationTimeUtc(a).CompareTo(File.GetCreationTimeUtc(b)); });
                return files.ToArray();
            }
            catch { return new string[0]; }
        }

        public const int MaxCustom = 6; // they share the editor's single row of swatches
        public static int CustomCount { get { int n = 0; foreach (Preset p in presets) if (p.File != null) n++; return n; } }

        public static bool IsCustom(int i) { return i >= 0 && i < presets.Length && presets[i].File != null; }
        public static bool IsWallpaper(int i) { return i >= 0 && i < presets.Length && presets[i].Wallpaper; }
        public static bool IsGradient(int i) { return i >= 0 && i < presets.Length && !presets[i].Wallpaper && presets[i].File == null; }

        // Copies an image in as a new background and returns its index (or -1 if it is not a readable image).
        public static int AddCustom(string source)
        {
            try
            {
                Directory.CreateDirectory(CustomDir);
                string dest = Path.Combine(CustomDir, Guid.NewGuid().ToString("N") + ".png");
                using (Bitmap src = ShotStack.LoadFull(source))
                {
                    double k = Math.Min(1.0, 3840.0 / Math.Max(src.Width, src.Height));
                    int w = Math.Max(1, (int)Math.Round(src.Width * k)), h = Math.Max(1, (int)Math.Round(src.Height * k));
                    using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(src, 0, 0, w, h);
                        }
                        b.Save(dest, ImageFormat.Png);
                    }
                }
                presets = Build();
                for (int i = 0; i < presets.Length; i++) if (presets[i].File == dest) return i;
            }
            catch (Exception ex) { ShotStack.Log("Fondo propio: " + ex.Message); }
            return -1;
        }

        public static void RemoveCustom(int i)
        {
            if (!IsCustom(i)) return;
            string f = presets[i].File;
            lock (customLock)
            {
                Bitmap t;
                if (thumbs.TryGetValue(f, out t)) { t.Dispose(); thumbs.Remove(f); }
                if (fullFor == f && full != null) { full.Dispose(); full = null; fullFor = null; }
            }
            try { File.Delete(f); } catch (Exception ex) { ShotStack.Log("Fondo propio: " + ex.Message); }
            presets = Build();
        }

        // Small copy for swatches; the full image (only the current one is kept) for the real render.
        static Bitmap Custom(string f, bool small)
        {
            lock (customLock)
            {
                try
                {
                    Bitmap b;
                    if (small && thumbs.TryGetValue(f, out b)) return b;
                    if (!small && fullFor == f && full != null) return full;
                    using (Bitmap src = ShotStack.LoadFull(f))
                    {
                        if (!small)
                        {
                            if (full != null) full.Dispose();
                            full = new Bitmap(src);
                            fullFor = f;
                            return full;
                        }
                        int w = 160, h = Math.Max(1, (int)Math.Round(160.0 * src.Height / src.Width));
                        b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(src, 0, 0, w, h);
                        }
                        thumbs[f] = b;
                        return b;
                    }
                }
                catch (Exception ex) { ShotStack.Log("Fondo propio: " + ex.Message); return null; }
            }
        }

        // Scales an image to cover r, centered.
        static void Cover(Graphics g, Rectangle r, Bitmap img)
        {
            float k = Math.Max(r.Width / (float)img.Width, r.Height / (float)img.Height);
            float w = img.Width * k, h = img.Height * k;
            InterpolationMode im = g.InterpolationMode;
            PixelOffsetMode pm = g.PixelOffsetMode;
            g.InterpolationMode = quick ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(img, new Rectangle((int)(r.X + (r.Width - w) / 2), (int)(r.Y + (r.Height - h) / 2), (int)Math.Ceiling(w), (int)Math.Ceiling(h)),
                            0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
            g.InterpolationMode = im;
            g.PixelOffsetMode = pm;
        }

        // Swatch for the pickers.
        public static void PaintSwatch(Graphics g, Rectangle r, int preset)
        {
            Paint(g, r, Clamp(preset), true);
        }

        static void Paint(Graphics g, Rectangle r, int preset, bool small)
        {
            if (r.Width < 1 || r.Height < 1) return;
            Preset p = presets[preset];
            if (p.File != null)
            {
                Bitmap img = Custom(p.File, small);
                if (img != null) { Cover(g, r, img); return; }
            }
            if (p.Wallpaper)
            {
                Bitmap wp = Wallpaper();
                if (wp != null)
                {
                    // Cover the whole canvas; the tiny image blurs when upscaled.
                    float k = Math.Max(r.Width / (float)wp.Width, r.Height / (float)wp.Height);
                    float w = wp.Width * k, h = wp.Height * k;
                    InterpolationMode im = g.InterpolationMode;
                    PixelOffsetMode pm = g.PixelOffsetMode;
                    g.InterpolationMode = quick ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
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
            // Soft top-left glow so the gradient doesn't look flat.
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

        // Background and shadow without the image (editor preview and videos).
        public static Bitmap Background(Size frame, Rectangle inner, Settings s, int radius) { return Background(frame, inner, s, radius, null); }

        // card: what carries the shadow. info: what the capture gives (null for a video: a blurred backdrop is drawn
        // as the gradient chosen).
        public static Bitmap Background(Size frame, Rectangle card, Settings s, int radius, ShotInfo info)
        {
            Bitmap b = new Bitmap(Math.Max(1, frame.Width), Math.Max(1, frame.Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PaintFor(g, new Rectangle(0, 0, b.Width, b.Height), s, info);
                int level = ShadowLevel(s);
                if (level > 0) Shadow(g, card, radius, level);
            }
            return b;
        }

        // The editor's preview draws the background and the shadow as separate layers, so dragging the corner slider
        // never repaints the background, and a slider being dragged scales images the fast way (quick).
        [ThreadStatic] static bool quick;

        public static Bitmap Fill(Size frame, Settings s, bool fast) { return Fill(frame, s, fast, null); }

        public static Bitmap Fill(Size frame, Settings s, bool fast, ShotInfo info)
        {
            Bitmap b = new Bitmap(Math.Max(1, frame.Width), Math.Max(1, frame.Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                quick = fast;
                try { PaintFor(g, new Rectangle(0, 0, b.Width, b.Height), s, info); }
                finally { quick = false; }
            }
            return b;
        }

        // The backdrop the settings choose: a plain colour, the capture blurred (when there is one), or a gradient or
        // a picture.
        static void PaintFor(Graphics g, Rectangle r, Settings s, ShotInfo info)
        {
            if (s.BgKind == 1)
            {
                using (SolidBrush b = new SolidBrush(PlainColor(s))) g.FillRectangle(b, r);
                return;
            }
            if (s.BgKind == 2 && info != null && info.Blur != null) { PaintBlur(g, r, info.Blur, s.BgBlur); return; }
            Paint(g, r, Clamp(s.BgPreset), false);
        }

        // What PaintFor draws, as the key of a cache of it.
        public static string FillKey(Settings s, ShotInfo info)
        {
            if (s.BgKind == 1) return "c" + (s.BgColor & 0xFFFFFF);
            if (s.BgKind == 2 && info != null && info.Blur != null) return "b" + s.BgBlur + "|" + info.Base;
            return s.BgPreset + "|" + Name(s.BgPreset);
        }

        public static Color PlainColor(Settings s)
        {
            int c = s.BgColor & 0xFFFFFF;
            return Color.FromArgb(255, (c >> 16) & 255, (c >> 8) & 255, c & 255);
        }

        // The blurred capture over r; variant 1 darker, 2 lighter.
        public static void PaintBlur(Graphics g, Rectangle r, Bitmap small, int variant)
        {
            Cover(g, r, small);
            if (variant == 1) using (SolidBrush b = new SolidBrush(Color.FromArgb(96, 0, 0, 0))) g.FillRectangle(b, r);
            else if (variant == 2) using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 255, 255, 255))) g.FillRectangle(b, r);
        }

        public static Bitmap Compose(Bitmap content, Settings s)
        {
            using (ShotInfo info = Study(content, new Rectangle(Point.Empty, content.Size), s)) return Compose(content, s, info);
        }

        // info: from Study (the editor passes the one it made from the capture without the marks, as its preview uses).
        public static Bitmap Compose(Bitmap content, Settings s, ShotInfo info)
        {
            Padding inset = info != null ? info.Inset : Padding.Empty;
            Size frame;
            Rectangle card, inner;
            Measure(content.Size, s, false, inset, out frame, out card, out inner);
            int radius = RadiusFor(card.Size, s);
            Bitmap b = Background(frame, card, s, radius, info);
            using (Graphics g = Graphics.FromImage(b))
            {
                if (inset != Padding.Empty) FillCard(g, card, radius, info.Edge);
                using (Bitmap round = Rounded(content, InnerRadius(radius, inset, content.Size)))
                    g.DrawImage(round, inner.X, inner.Y, round.Width, round.Height);
            }
            return b;
        }

        // The border of an inset: the card in the capture's edge colour, under the capture.
        public static void FillCard(Graphics g, RectangleF card, float radius, Color edge)
        {
            SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(edge))
            {
                if (radius < 0.5f) g.FillRectangle(b, card);
                else using (GraphicsPath p = Theme.Round(card, radius)) g.FillPath(b, p);
            }
            g.SmoothingMode = sm;
        }

        // The capture's own corners inside a card: the card's, less the narrowest side of the inset, never more than
        // half the capture's shorter side.
        public static float InnerRadius(float radius, Padding inset, Size content)
        {
            float r = Math.Max(0, radius - Math.Min(Math.Min(inset.Left, inset.Right), Math.Min(inset.Top, inset.Bottom)));
            return Math.Min(r, Math.Min(content.Width, content.Height) / 2);
        }

        // Copy with antialiased rounded corners. Only the corner pixels are touched, which is far faster than filling a
        // path with a texture brush.
        public static Bitmap Rounded(Bitmap src, float radius) { return Rounded(src, radius, null); }

        // reuse: a bitmap of the same size to draw into (the preview while a slider moves), or null for a new one.
        public static Bitmap Rounded(Bitmap src, float radius, Bitmap reuse)
        {
            Bitmap b = reuse != null && reuse.Size == src.Size && reuse.PixelFormat == PixelFormat.Format32bppPArgb
                ? reuse : new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
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
                            // Premultiplied: scale all four channels.
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

        // Two macOS-style shadows: a wide soft one and a tight one that grounds the window.
        static void Shadow(Graphics g, Rectangle inner, int radius, int level)
        {
            foreach (ShadowPiece p in ShadowPieces(inner, radius, level))
                using (p) p.Draw(g, Point.Empty);
        }

        // Two macOS-style shadows, each a small blurred picture and where it goes once upscaled (Area). The editor keeps
        // them for a capture's size and corners and draws them straight to the screen.
        public sealed class ShadowPiece : IDisposable
        {
            public Bitmap Small;
            public Rectangle Area;

            public void Draw(Graphics g, Point offset) { Draw(g, offset, false); }

            // fast: plain bilinear, for a slider being dragged (on a blur the difference doesn't show).
            public void Draw(Graphics g, Point offset, bool fast)
            {
                // The bitmap edge is transparent (the margin covers it), so a plain DrawImage is enough.
                InterpolationMode im = g.InterpolationMode;
                g.InterpolationMode = fast ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear; // smooth and faster than the default when upscaling in GDI+
                g.DrawImage(Small, new Rectangle(Area.X + offset.X, Area.Y + offset.Y, Area.Width, Area.Height));
                g.InterpolationMode = im;
            }

            public void Dispose() { if (Small != null) Small.Dispose(); Small = null; }
        }

        public static ShadowPiece[] ShadowPieces(Rectangle inner, int radius) { return ShadowPieces(inner, radius, 50); }

        // level 1-100: 50 is the shadow as it always was; less is closer and lighter, more wider and darker.
        public static ShadowPiece[] ShadowPieces(Rectangle inner, int radius, int level)
        {
            double u = Unit(inner.Size), f = 0.5 + level / 100.0, a = 0.25 + 1.5 * level / 100.0;
            float wide = (float)Math.Max(6, u * 0.045 * f), tight = (float)Math.Max(2, u * 0.012 * f);
            return new ShadowPiece[] { Blurred(inner, radius, wide, wide * 0.42f, (float)Math.Min(0.9, 0.42 * a)),
                                       Blurred(inner, radius, tight, tight * 0.5f, (float)Math.Min(0.75, 0.30 * a)) };
        }

        // Real blurred shadow: drawn downscaled, box-blurred three times (~gaussian), to be upscaled over Area.
        static ShadowPiece Blurred(Rectangle inner, int radius, float blur, float offY, float alpha)
        {
            float d = Math.Max(3f, blur / 3.5f);         // blur at reduced size
            int margin = (int)Math.Ceiling(blur * 2.2f);
            Rectangle area = Rectangle.Inflate(inner, margin, margin);
            int sw = Math.Max(4, (int)Math.Ceiling(area.Width / d)), sh = Math.Max(4, (int)Math.Ceiling(area.Height / d));
            Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
            using (Graphics sg = Graphics.FromImage(small))
            {
                sg.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF r = new RectangleF(margin / d, margin / d, inner.Width / d, inner.Height / d);
                using (GraphicsPath p = Theme.Round(r, radius / d))
                using (SolidBrush br = new SolidBrush(Color.Black)) sg.FillPath(br, p);
            }
            BlurAlpha(small, Math.Max(1, (int)Math.Round(blur / d * 0.55f)), alpha);
            return new ShadowPiece { Small = small, Area = new Rectangle(area.X, (int)Math.Round(area.Y + offY), area.Width, area.Height) };
        }

        // Three box-blur passes on the alpha channel, scaled by alpha; color stays black.
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

        // Top layer for a video: background with a rounded hole for the video, plus the annotations inside it. FFmpeg
        // puts the video below and this image on top. overlay: annotations sized to the cropped video, or null.
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

        // Coverage of pixel (x, y) inside the rounded rectangle (0 outside, 1 inside, antialiased edge).
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action, int param, StringBuilder value, int winIni);

        static Bitmap wallpaper;
        static string wallpaperFor;
        static DateTime wallpaperStamp;
        static readonly object wallpaperLock = new object();
        static DateTime wallpaperChecked;

        // Loading the wallpaper takes a few hundred ms the first time, so it is prewarmed on a worker thread at
        // startup.
        public static void Prewarm()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Wallpaper(); });
        }

        // For README images (tools\Studio.cs): never use the wallpaper of whoever generates them.
        public static bool NoWallpaper;

        static Bitmap Wallpaper()
        {
            if (NoWallpaper) return null;
            lock (wallpaperLock) return LoadWallpaper();
        }

        // Wallpaper downscaled to 112 px; upscaled it looks like frosted glass.
        static Bitmap LoadWallpaper()
        {
            try
            {
                // Painted once per swatch in a row: hit Windows and the disk at most every 2 s.
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
