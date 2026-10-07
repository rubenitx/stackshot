// Stackshot - Mascot reel for the README (docs\mascot.gif): the mascot trying on styles and costumes, rendered
// offscreen in real time and encoded with GifWriter. Built and run by tools\make-reel.ps1.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;

namespace Stackshot
{
    public static class Reel
    {
        const int W = 640, H = 320, FrameMs = 60, ShotMs = 1250;

        [STAThread]
        public static void Main(string[] args)
        {
            Program.Init();
            if (args.Length > 1 && args[1] == "grid") { Grid(args[0]); return; }
            if (args.Length > 1 && args[1] == "videos") { Bitmap hero = Promo.Tour(args[0]); Promo.Run(args[2], hero); if (hero != null) hero.Dispose(); return; }
            Mascot m = new Mascot();
            m.Look = new MascotLook();
            m.Look.Seasonal = false;
            m.Box = new RectangleF(64, 62, 200, 200);
            // Looks to show: a few quick styles, then the costumes.
            int[][] looks = { new int[] { 0, 0 }, new int[] { 0, 2 }, new int[] { 1, 0 }, new int[] { 1, 1 }, new int[] { 1, 3 }, new int[] { 1, 5 }, new int[] { 1, 6 },
                              new int[] { 1, 11 }, new int[] { 1, 13 }, new int[] { 1, 15 }, new int[] { 1, 17 }, new int[] { 1, 19 }, new int[] { 1, 22 }, new int[] { 1, 25 },
                              new int[] { 1, 29 }, new int[] { 1, 30 }, new int[] { 1, 33 } };
            using (Bitmap frame = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (GifWriter gif = new GifWriter(args[0], W, H, 0))
            {
                for (int k = 0; k < looks.Length; k++)
                {
                    string name;
                    if (looks[k][0] == 0) { MascotParts.ApplyStyle(m.Look, looks[k][1]); name = Promo.En(MascotParts.StyleNames[looks[k][1]]); }
                    else { MascotParts.ApplyAnime(m.Look, looks[k][1]); name = Promo.En(MascotParts.AnimeNames[looks[k][1]]); }
                    if (k % 3 == 0) m.Dance(); else if (k % 3 == 1) m.Twirl(); else m.Celebrate(null);
                    string tag = looks[k][0] == 0 ? "Styles" : "Characters";
                    double start = Anim.Now;
                    while (Anim.Now - start < ShotMs)
                    {
                        double t0 = Anim.Now;
                        m.Step(t0, new PointF(520, 150), true);
                        Paint(frame, m, name, tag, (t0 - start) / 260.0);
                        gif.AddFrame(frame, FrameMs);
                        int wait = FrameMs - (int)(Anim.Now - t0);
                        if (wait > 0) Thread.Sleep(wait);
                    }
                }
            }
        }

        // Static sheet with every character costume, its name and where it comes from (docs/characters.png).
        static void Grid(string path)
        {
            int cols = 6, cw = 150, ch = 178, n = MascotParts.AnimeNames.Length, rows = (n + cols - 1) / cols;
            using (Bitmap b = new Bitmap(cols * cw + 32, rows * ch + 32))
            using (Graphics g = Graphics.FromImage(b))
            using (Font fn = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel))
            using (Font fs = new Font("Segoe UI", 12, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.FromArgb(28, 28, 30));
                for (int i = 0; i < n; i++)
                {
                    int x = 16 + (i % cols) * cw, y = 16 + (i / cols) * ch;
                    Rectangle card = new Rectangle(x + 6, y + 6, cw - 12, ch - 12);
                    using (GraphicsPath p = Theme.Round(card, 16))
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(40, 40, 44))) g.FillPath(cb, p);
                    MascotLook l = new MascotLook();
                    l.Seasonal = false;
                    MascotParts.ApplyAnime(l, i);
                    Mascot.RenderStill(g, new RectangleF(x + 30, y + 26, 90, 90), l);
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 247)))
                        g.DrawString(Promo.En(MascotParts.AnimeNames[i]), fn, tb, new RectangleF(x + 8, y + 124, cw - 16, 20), sf);
                    string from = Promo.En(MascotParts.AnimeInspiration[i] ?? "Original");
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(152, 152, 159)))
                        g.DrawString(from, fs, sb, new RectangleF(x + 8, y + 146, cw - 16, 18), sf);
                }
                b.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        static void Paint(Bitmap b, Mascot m, string name, string tag, double fadeIn)
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.FromArgb(28, 28, 30));
                Rectangle card = new Rectangle(16, 16, W - 32, H - 32);
                using (GraphicsPath p = Theme.Round(card, 22))
                using (LinearGradientBrush lb = new LinearGradientBrush(card, Color.FromArgb(44, 40, 62), Color.FromArgb(30, 30, 34), 35f))
                    g.FillPath(lb, p);
                MascotParts.Glow(g, 164, 155, 340, 270, Color.FromArgb(120, 110, 255), 50);
                m.Paint(g);
                int a = (int)(255 * Math.Max(0, Math.Min(1, fadeIn)));
                using (Font ft = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(a * 160 / 255, 235, 235, 245)))
                    g.DrawString(tag.ToUpperInvariant(), ft, tb, 330, 104);
                using (Font fn = new Font(Fonts.DisplaySemibold, 36, GraphicsUnit.Pixel))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(a, 245, 245, 247)))
                    g.DrawString(name, fn, nb, 326, 126);
                using (Font fs = new Font("Segoe UI", 15, GraphicsUnit.Pixel))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(a * 150 / 255, 200, 200, 210)))
                    g.DrawString("Stackshot \u00B7 your capture sidekick", fs, sb, 330, 182);
            }
        }
    }
}
