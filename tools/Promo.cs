// Stackshot - README videos: docs\promo.gif (a short promotional piece) and docs\demo.gif (a tour of the real main
// window). Everything is rendered offscreen with read-only settings; the main window is photographed with PrintWindow.
// Built and run by tools\make-reel.ps1.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Promo
    {
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        // English labels for the README images (the app itself is in Spanish).
        static readonly Dictionary<string, string> en = new Dictionary<string, string>
        {
            { "Hora de aventuras", "Adventure Time" }, { "Ataque a los titanes", "Attack on Titan" }, { "Kimetsu no Yaiba", "Demon Slayer" },
            { "Mi vecino Totoro", "My Neighbor Totoro" }, { "Caballero", "Knight" }, { "Astronauta", "Astronaut" },
            { "Mago sabio", "Wise wizard" }, { "Superh\u00E9roe", "Superhero" }, { "Cl\u00E1sico", "Classic" }, { "Chulo", "Cool" },
            { "Vampiro", "Vampire" }, { "Dragoncito", "Little dragon" }
        };

        public static string En(string s)
        {
            string t;
            return s != null && en.TryGetValue(s, out t) ? t : s;
        }

        static double Clamp(double v) { return Math.Max(0, Math.Min(1, v)); }
        static double Out(double t) { t = Clamp(t); return 1 - Math.Pow(1 - t, 3); }

        static void Pace(double start, int frameMs)
        {
            int wait = frameMs - (int)(Anim.Now - start);
            if (wait > 0) Thread.Sleep(wait);
            Application.DoEvents();
        }

        // ---- Tour: the real main window, page by page, then dressing the mascot up.
        public static Bitmap Tour(string path)
        {
            Settings.ReadOnly = true;
            ShotStack.Test = true;
            Backdrop.NoWallpaper = true;
            Settings st = new Settings();
            st.ShowIntro = false;
            st.MascotLove = 42;
            ShotStack stack = new ShotStack(st, false, false);
            HomeWindow w = new HomeWindow(stack, st);
            w.StartPosition = FormStartPosition.Manual;
            w.Location = new Point(-6000, 0);
            w.Show();
            Type ht = typeof(HomeWindow);
            Bitmap hero = null;
            int gw = 760, gh = (int)Math.Round(760.0 * w.Height / w.Width) & ~1;
            using (Bitmap shot = new Bitmap(w.Width, w.Height))
            using (Bitmap frame = new Bitmap(gw, gh))
            using (GifWriter gif = new GifWriter(path, gw, gh, 0))
            {
                Action<double> film = delegate(double ms)
                {
                    double end = Anim.Now + ms;
                    while (Anim.Now < end)
                    {
                        double t0 = Anim.Now;
                        Application.DoEvents();
                        using (Graphics g = Graphics.FromImage(shot))
                        {
                            IntPtr hdc = g.GetHdc();
                            PrintWindow(w.Handle, hdc, 2);
                            g.ReleaseHdc(hdc);
                        }
                        using (Graphics g = Graphics.FromImage(frame))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(shot, 0, 0, gw, gh);
                        }
                        gif.AddFrame(frame, 70);
                        Pace(t0, 70);
                    }
                };
                Action<string> page = delegate(string id) { ht.GetMethod("SetPage", NP).Invoke(w, new object[] { id, true }); };
                Action<double> scrollTo = delegate(double y) { ht.GetField("scrollTarget", NP).SetValue(w, y); };
                Action<int> dress = delegate(int i)
                {
                    MascotLook l = MascotLook.From(st);
                    MascotParts.ApplyAnime(l, i);
                    ht.GetMethod("LookChanged", NP).Invoke(w, new object[] { l, "\u00A1Hoy soy " + MascotParts.AnimeNames[i] + "!" });
                };
                film(2600);
                page("general"); film(1500);
                page("editor"); film(1500);
                page("record"); film(1500);
                page("mascot"); film(1600);
                int contentH = (int)ht.GetField("contentHeight", NP).GetValue(w);
                scrollTo(Math.Min(contentH - 600, 760)); film(1300);
                dress(6); film(900);
                dress(29); film(900);
                scrollTo(0); film(1500);
                dress(33); film(1400);
                page("home"); film(2400);
                hero = new Bitmap(shot);
            }
            w.Close();
            stack.Quit();
            return hero;
        }

        // ---- Promo: five short scenes, about 17 seconds.
        public static void Run(string path, Bitmap appShot)
        {
            const int W = 720, H = 405, F = 50;
            Mascot m = new Mascot();
            m.Look = new MascotLook();
            m.Look.Seasonal = false;
            int[] parade = { 6, 29, 11, 30, 13, 33, 5, 15, 19, 22 };
            string[] features = { "Floating thumbnails", "Scrolling capture", "Editor & backdrops", "Video, GIF & webcam", "A desktop sidekick", "In-app updates" };
            Color[] chip = { Color.FromArgb(79, 123, 255), Color.FromArgb(20, 184, 230), Color.FromArgb(139, 92, 246), Color.FromArgb(244, 63, 94), Color.FromArgb(52, 199, 89), Color.FromArgb(255, 159, 10) };
            using (Bitmap b = new Bitmap(W, H))
            using (GifWriter gif = new GifWriter(path, W, H, 0))
            using (Font huge = new Font(Fonts.DisplaySemibold, 52, GraphicsUnit.Pixel))
            using (Font big = new Font(Fonts.DisplaySemibold, 34, GraphicsUnit.Pixel))
            using (Font mid = new Font("Segoe UI", 18, GraphicsUnit.Pixel))
            using (Font chipFont = new Font("Segoe UI Semibold", 17, GraphicsUnit.Pixel))
            using (StringFormat center = new StringFormat())
            {
                center.Alignment = StringAlignment.Center;
                center.LineAlignment = StringAlignment.Center;
                double[] lengths = { 2600, 3200, 5000, 3800, 2800 };
                for (int scene = 0; scene < lengths.Length; scene++)
                {
                    double start = Anim.Now;
                    int next = 0;
                    if (scene == 2) { m.Box = new RectangleF(70, 92, 220, 220); m.Dance(); }
                    while (Anim.Now - start < lengths[scene])
                    {
                        double t0 = Anim.Now, t = t0 - start;
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            Background(g, W, H, t0);
                            switch (scene)
                            {
                                case 0:
                                {
                                    float d = 150;
                                    LogoArt.Paint(g, new RectangleF((W - d) / 2, 64, d, d), Out(t / 600), Out((t - 250) / 600), Out((t - 450) / 600), Out((t - 650) / 600), false);
                                    Text(g, "Stackshot", huge, new RectangleF(0, 228, W, 60), 245, Out((t - 700) / 500), center);
                                    Text(g, "Capture, annotate and share in seconds.", mid, new RectangleF(0, 292, W, 30), 200, Out((t - 1000) / 500), center);
                                    break;
                                }
                                case 1:
                                {
                                    double k = Out(t / 700);
                                    Text(g, "Everything, one shortcut away", big, new RectangleF(0, 22, W, 48), 245, k, center);
                                    if (appShot != null)
                                    {
                                        float sw = 560, sh = sw * appShot.Height / appShot.Width;
                                        float z = (float)(0.92 + 0.08 * k + t / 60000.0);
                                        RectangleF r = new RectangleF((W - sw * z) / 2, 84 + (float)((1 - k) * 60), sw * z, sh * z);
                                        using (GraphicsPath p = Theme.Round(r, 14))
                                        {
                                            MascotParts.Glow(g, r.X + r.Width / 2, r.Y + r.Height / 2, r.Width * 1.2f, r.Height * 1.2f, Color.FromArgb(120, 110, 255), (int)(60 * k));
                                            Region old = g.Clip;
                                            g.SetClip(p, CombineMode.Intersect);
                                            g.DrawImage(appShot, r);
                                            g.Clip = old;
                                            old.Dispose();
                                        }
                                    }
                                    break;
                                }
                                case 2:
                                {
                                    int i = Math.Min(parade.Length - 1, (int)(t / (lengths[2] / parade.Length)));
                                    if (i >= next)
                                    {
                                        MascotParts.ApplyAnime(m.Look, parade[i]);
                                        if (i % 2 == 0) m.Twirl(); else m.Celebrate(null);
                                        next = i + 1;
                                    }
                                    m.Step(t0, new PointF(500, 160), true);
                                    MascotParts.Glow(g, 180, 200, 360, 300, Color.FromArgb(120, 110, 255), 60);
                                    m.Paint(g);
                                    double k = Out(t / 600);
                                    Text(g, "Make it yours.", huge, new RectangleF(330, 96, 380, 70), 245, k, null);
                                    Text(g, "38 characters, 12 species,", mid, new RectangleF(334, 176, 380, 28), 210, Out((t - 300) / 600), null);
                                    Text(g, "endless styles.", mid, new RectangleF(334, 202, 380, 28), 210, Out((t - 300) / 600), null);
                                    Text(g, MascotParts.AnimeNames[parade[i]], big, new RectangleF(334, 252, 380, 46), 250, 1, null);
                                    Text(g, En(MascotParts.AnimeInspiration[parade[i]] ?? "Original"), mid, new RectangleF(336, 298, 380, 28), 150, 1, null);
                                    break;
                                }
                                case 3:
                                {
                                    Text(g, "Built for the way you work", big, new RectangleF(0, 30, W, 48), 245, Out(t / 600), center);
                                    for (int i = 0; i < features.Length; i++)
                                    {
                                        double k = Out((t - 250 - i * 220) / 500);
                                        if (k <= 0) continue;
                                        int col = i % 2, row = i / 2;
                                        RectangleF r = new RectangleF(70 + col * 300 + (float)((1 - k) * 30), 110 + row * 82, 280, 62);
                                        using (GraphicsPath p = Theme.Round(r, 16))
                                        using (SolidBrush cb = new SolidBrush(Color.FromArgb((int)(230 * k), 44, 44, 50))) g.FillPath(cb, p);
                                        using (GraphicsPath dot = Theme.Round(new RectangleF(r.X + 16, r.Y + 17, 28, 28), 8))
                                        using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(255 * k), chip[i]))) g.FillPath(db, dot);
                                        Text(g, features[i], chipFont, new RectangleF(r.X + 58, r.Y, r.Width - 64, r.Height), 240, k, null, true);
                                    }
                                    break;
                                }
                                default:
                                {
                                    float d = 96;
                                    LogoArt.PaintFull(g, new RectangleF((W - d) / 2, 70, d, d), false);
                                    Text(g, "Free and open source for Windows", big, new RectangleF(0, 186, W, 48), 245, Out(t / 500), center);
                                    Text(g, "Download at github.com/rubenitx/stackshot", mid, new RectangleF(0, 240, W, 30), 200, Out((t - 300) / 500), center);
                                    break;
                                }
                            }
                        }
                        gif.AddFrame(b, F);
                        Pace(t0, F);
                    }
                }
            }
        }

        static void Background(Graphics g, int w, int h, double now)
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(32, 28, 52), Color.FromArgb(18, 18, 22), 35f))
                g.FillRectangle(lb, 0, 0, w, h);
            // A still glow: anything moving behind every frame would make the GIF several times larger.
            MascotParts.Glow(g, w * 0.5f, h * 0.2f, w * 0.9f, h * 0.9f, Color.FromArgb(100, 90, 255), 34);
        }

        static void Text(Graphics g, string s, Font f, RectangleF r, int alpha, double k, StringFormat sf, bool middle = false)
        {
            if (k <= 0) return;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(alpha * Clamp(k)), 245, 245, 247)))
            {
                RectangleF rr = r;
                rr.Offset(0, (float)((1 - Clamp(k)) * 12));
                if (sf != null) g.DrawString(s, f, b, rr, sf);
                else if (middle) using (StringFormat m = new StringFormat()) { m.LineAlignment = StringAlignment.Center; g.DrawString(s, f, b, rr, m); }
                else g.DrawString(s, f, b, rr);
            }
        }
    }
}
