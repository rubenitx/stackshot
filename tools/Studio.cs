// Stackshot - Screenshot studio: builds a fake desktop on one monitor and photographs each part of Stackshot for the
// README (docs\*.png), so images never show real content and can be regenerated. Built and run by
// tools\make-screenshots.ps1.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Studio
    {
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        static string docs, tmp;
        static Screen target;
        static Form backdrop;
        static Bitmap scene;
        static ShotStack stack;
        static List<string> crops = new List<string>();

        [STAThread]
        public static void Main(string[] args)
        {
            docs = args[0];
            Directory.CreateDirectory(docs);
            tmp = Path.Combine(Path.GetTempPath(), "stackshot-studio");
            Directory.CreateDirectory(tmp);
            Program.Init();
            ShotStack.Test = true;
            FloatWindow.ExcludeFromCapture = false;
            Backdrop.NoWallpaper = true; // the "Your desktop" swatch must not show the real wallpaper
            Settings.ReadOnly = true; // never write the settings of whoever generates the images
            // The monitor without the mouse, to stay out of the way.
            target = Screen.PrimaryScreen;
            foreach (Screen s in Screen.AllScreens) if (!s.Bounds.Contains(Control.MousePosition)) { target = s; break; }

            scene = Desktop(target.Bounds.Size);
            backdrop = new Form();
            backdrop.FormBorderStyle = FormBorderStyle.None;
            backdrop.StartPosition = FormStartPosition.Manual;
            backdrop.Bounds = target.Bounds;
            backdrop.TopMost = true;
            backdrop.ShowInTaskbar = false;
            backdrop.BackgroundImage = scene;
            backdrop.BackgroundImageLayout = ImageLayout.None;
            backdrop.Show();

            settings = new Settings();
            settings.FollowMouse = false;
            settings.Sound = false;
            stack = new ShotStack(settings, false, false);

            List<KeyValuePair<int, Action>> steps = new List<KeyValuePair<int, Action>>();
            int t = 600;
            steps.Add(Step(t, Cards)); t += 2200;
            steps.Add(Step(t, delegate { Grab(new Rectangle(target.WorkingArea.Left, target.WorkingArea.Bottom - 760, 980, 760), "stack.png"); HoverTop(); })); t += 900;
            steps.Add(Step(t, delegate { Grab(new Rectangle(target.WorkingArea.Left, target.WorkingArea.Bottom - 760, 980, 760), "stack-hover.png"); CloseCards(); })); t += 900;
            steps.Add(Step(t, Region)); t += 400;
            steps.Add(Step(t, Recording)); t += 2600;
            steps.Add(Step(t, delegate { GrabRecording(); })); t += 600;
            steps.Add(Step(t, Editor)); t += 1600;
            steps.Add(Step(t, delegate { GrabWindow(editor, "editor.png", 40); typeof(Editor).GetMethod("ToggleBgPanel", NP).Invoke(editor, null); })); t += 1300;
            steps.Add(Step(t, delegate { GrabWindow(editor, "backdrop.png", 40); typeof(Editor).GetField("closeWithoutAsking", NP).SetValue(editor, true); editor.Close(); })); t += 600;
            steps.Add(Step(t, delegate { ShowSetup(true); })); t += 1200;
            steps.Add(Step(t, delegate { GrabWindow(setup, "welcome.png", 40); setup.Close(); })); t += 400;
            steps.Add(Step(t, ShowHome)); t += 2200;
            steps.Add(Step(t, delegate { GrabWindow(home, "app.png", 40); typeof(HomeWindow).GetMethod("SetPage", NP).Invoke(home, new object[] { "general", true }); })); t += 1000;
            steps.Add(Step(t, delegate { GrabWindow(home, "settings.png", 40); home.Dispose(); })); t += 400;
            steps.Add(Step(t, Hero)); t += 300;
            steps.Add(Step(t, delegate { stack.ExitThread(); backdrop.Close(); Application.ExitThread(); }));

            Timer timer = new Timer();
            timer.Interval = 30;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            int next = 0;
            timer.Tick += delegate
            {
                while (next < steps.Count && sw.ElapsedMilliseconds >= steps[next].Key)
                {
                    try { steps[next].Value(); }
                    catch (Exception ex) { File.AppendAllText(Path.Combine(docs, "studio-errores.txt"), ex + Environment.NewLine); }
                    next++;
                }
            };
            timer.Start();
            Application.Run(stack);
        }

        static KeyValuePair<int, Action> Step(int ms, Action a) { return new KeyValuePair<int, Action>(ms, a); }

        static void Cards()
        {
            // Four "captures" cropped from the fake desktop.
            Rectangle[] parts = { new Rectangle(560, 210, 640, 360), new Rectangle(1240, 230, 420, 300), new Rectangle(520, 600, 760, 330), new Rectangle(330, 180, 420, 520) };
            foreach (Rectangle p in parts)
            {
                Rectangle r = Rectangle.Intersect(p, new Rectangle(Point.Empty, scene.Size));
                string f = Path.Combine(tmp, "Captura " + crops.Count + ".png");
                using (Bitmap b = scene.Clone(r, PixelFormat.Format32bppArgb)) b.Save(f, ImageFormat.Png);
                crops.Add(f);
                stack.GetType().GetMethod("AddCard", NP, null, new Type[] { typeof(string) }, null).Invoke(stack, new object[] { f });
                stack.GetType().GetField("anchorDevice", NP).SetValue(stack, target.DeviceName);
                stack.GetType().GetMethod("Relayout", NP).Invoke(stack, null);
            }
        }

        static void HoverTop()
        {
            List<Card> cards = (List<Card>)stack.GetType().GetField("cards", NP).GetValue(stack);
            Card top = cards[cards.Count - 1];
            typeof(Card).GetField("hover", NP).SetValue(top, true);
            typeof(Card).GetMethod("UpdateHover", NP).Invoke(top, null);
            typeof(Card).GetMethod("SetHot", NP).Invoke(top, new object[] { 2 });
        }

        static void CloseCards()
        {
            stack.CloseAll();
        }

        // The region picker is rendered off-screen (without covering yours) over the fake desktop.
        static void Region()
        {
            Rectangle vs = target.Bounds;
            Dib frozen = new Dib(vs.Width, vs.Height);
            using (Graphics g = frozen.Graphics()) g.DrawImageUnscaled(scene, 0, 0);
            ConstructorInfo ci = typeof(RegionPicker).GetConstructors(NP)[0];
            RegionPicker p = (RegionPicker)ci.Invoke(new object[] { frozen, vs, new List<Grabber.Win>(), RegionPicker.Mode.Image });
            Point start = new Point(600, 250), cur = new Point(1180, 590);
            typeof(RegionPicker).GetField("down", NP).SetValue(p, true);
            typeof(RegionPicker).GetField("dragging", NP).SetValue(p, true);
            typeof(RegionPicker).GetField("start", NP).SetValue(p, start);
            typeof(RegionPicker).GetField("cur", NP).SetValue(p, cur);
            typeof(RegionPicker).GetMethod("TrackMonitor", NP).Invoke(p, null);
            typeof(RegionPicker).GetField("sel", NP).SetValue(p, Rectangle.FromLTRB(start.X, start.Y, cur.X + 1, cur.Y + 1));
            // The same composition as on screen, in one pass.
            using (Dib outD = new Dib(vs.Width, vs.Height))
            {
                typeof(RegionPicker).GetMethod("Compose", NP).Invoke(p, new object[] { outD, new Rectangle(0, 0, vs.Width, vs.Height) });
                using (Bitmap outB = outD.ToBitmap()) outB.Save(Path.Combine(docs, "region.png"), ImageFormat.Png);
            }
            p.Dispose();
            frozen.Dispose();
        }

        static RecordBar bar;
        static FrameEdge[] edges;

        static void Recording()
        {
            Rectangle wa = target.WorkingArea;
            Rectangle area = new Rectangle(wa.Left + 520, wa.Top + 200, 820, 460);
            Session s = new Session(null, new Settings(), "ffmpeg.exe", area, false);
            typeof(Session).GetField("clock", NP).GetValue(s).GetType().GetMethod("Start").Invoke(typeof(Session).GetField("clock", NP).GetValue(s), null);
            edges = FrameEdge.Around(area);
            bar = new RecordBar(s, area);
        }

        static void GrabRecording()
        {
            Rectangle wa = target.WorkingArea;
            Grab(new Rectangle(wa.Left + 420, wa.Top + 120, 1020, 660), "recording.png");
            bar.Close();
            foreach (FrameEdge e in edges) e.Close();
        }

        static Editor editor;

        static void Editor()
        {
            Rectangle src = new Rectangle(500, 170, 1100, 640);
            string f = Path.Combine(tmp, "editor.png");
            using (Bitmap b = scene.Clone(src, PixelFormat.Format32bppArgb)) b.Save(f, ImageFormat.Png);
            editor = new Editor(null, f, ShotStack.LoadFull(f));
            editor.TopMost = true;
            editor.StartPosition = FormStartPosition.Manual;
            editor.Location = new Point(target.WorkingArea.Left + (target.WorkingArea.Width - editor.Width) / 2, target.WorkingArea.Top + (target.WorkingArea.Height - editor.Height) / 2);
            editor.Show();
            Canvas cv = (Canvas)typeof(Editor).GetField("canvas", NP).GetValue(editor);
            List<Shape> shapes = (List<Shape>)typeof(Canvas).GetField("shapes", NP).GetValue(cv);
            Color[] P = Theme.Palette;
            Shape arrow = S(Tool.Arrow, P[0], 170, 560, 470, 330, 1);
            arrow.SetMid(new PointF(250, 380), 1);
            shapes.Add(arrow);
            shapes.Add(S(Tool.Rect, P[0], 480, 270, 860, 400, 1));
            Shape n1 = S(Tool.Counter, P[3], 520, 120, 520, 120, 1); n1.Number = 1; shapes.Add(n1);
            Shape n2 = S(Tool.Counter, P[3], 900, 335, 900, 335, 1); n2.Number = 2; shapes.Add(n2);
            Shape tx = S(Tool.Text, P[0], 70, 580, 70, 580, 1); tx.Text = "Aqu\u00ED falla el login"; shapes.Add(tx);
            shapes.Add(S(Tool.Highlight, P[1], 470, 455, 860, 495, 1));
            shapes.Add(S(Tool.Pixelate, P[0], 40, 110, 230, 160, 1));
            cv.SelectShape(arrow);
            cv.Invalidate();
        }

        static Shape S(Tool k, Color c, float ax, float ay, float bx, float by, int weight)
        {
            Shape s = new Shape();
            s.Kind = k; s.Color = c; s.Weight = weight; s.Width = 6; s.FontPx = 26;
            s.A = new PointF(ax, ay); s.B = new PointF(bx, by);
            return s;
        }

        static Form setup;

        // Main window with the mascot waving (no launch animation).
        static HomeWindow home;
        static Settings settings;
        static void ShowHome()
        {
            home = new HomeWindow(stack, settings);
            home.TopMost = true;
            Rectangle wa = target.WorkingArea;
            home.Location = new Point(wa.Left + (wa.Width - home.Width) / 2, wa.Top + (wa.Height - home.Height) / 2);
            home.Present("home", false);
        }

        static void ShowSetup(bool welcome)
        {
            ConstructorInfo ci = typeof(SetupWindow).GetConstructors(NP)[0];
            setup = (Form)ci.Invoke(new object[] { new Settings() });
            setup.TopMost = true;
            setup.Location = new Point(target.WorkingArea.Left + (target.WorkingArea.Width - setup.Width) / 2, target.WorkingArea.Top + (target.WorkingArea.Height - setup.Height) / 2);
            setup.Show();
        }

        static void Grab(Rectangle r, string name)
        {
            r.Intersect(target.Bounds);
            using (Bitmap b = Grabber.Grab(r)) b.Save(Path.Combine(docs, name), ImageFormat.Png);
        }

        static void GrabWindow(Form w, string name, int margin)
        {
            Rectangle r = w.Bounds;
            r.Inflate(margin, margin);
            Grab(r, name);
        }

        // Social preview banner (1280x640): logo, name, tagline and the stack.
        static void Hero()
        {
            using (Bitmap b = new Bitmap(1280, 640, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (LinearGradientBrush bg = new LinearGradientBrush(new Point(0, 0), new Point(1280, 640), Color.FromArgb(14, 14, 30), Color.FromArgb(30, 20, 64)))
                    g.FillRectangle(bg, 0, 0, 1280, 640);
                Glow(g, new RectangleF(-200, -260, 900, 760), Color.FromArgb(70, 139, 92, 246));
                Glow(g, new RectangleF(700, 200, 800, 700), Color.FromArgb(60, 20, 184, 230));
                string logoPath = Path.Combine(Path.GetDirectoryName(docs), "assets", "logo-512.png");
                if (File.Exists(logoPath)) using (Image logo = Image.FromFile(logoPath)) g.DrawImage(logo, 70, 70, 190, 190);
                using (Font f = new Font(Fonts.DisplaySemibold, 92, GraphicsUnit.Pixel))
                    g.DrawString("Stackshot", f, Brushes.White, 66, 268);
                using (Font f = new Font(Fonts.Display, 30, GraphicsUnit.Pixel))
                using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 210, 220, 255)))
                    g.DrawString("Capture, annotate and share\nin seconds.", f, br, 72, 392);
                using (Font f = new Font("Segoe UI Semibold", 19, GraphicsUnit.Pixel))
                using (SolidBrush br = new SolidBrush(Color.FromArgb(150, 170, 190, 240)))
                    g.DrawString("Free  \u00B7  Open source (MIT)  \u00B7  Windows 10 and 11", f, br, 74, 514);
                // Editor on the right; the stack in its corner in front (as it really looks).
                string editorPath = Path.Combine(docs, "editor.png"), stackPath = Path.Combine(docs, "stack-hover.png");
                if (File.Exists(editorPath))
                    using (Image ed = Image.FromFile(editorPath))
                    {
                        Rectangle src = new Rectangle(40, 40, ed.Width - 80, ed.Height - 80); // without the backdrop margin
                        float k = 620f / src.Width;
                        Framed(g, ed, src, new RectangleF(576, 84, src.Width * k, src.Height * k), 12);
                    }
                if (File.Exists(stackPath))
                    using (Image st = Image.FromFile(stackPath))
                    {
                        // The top three cards (the first one with its buttons visible).
                        Rectangle src = new Rectangle(16, 104, 240, 494);
                        float k = 0.78f;
                        RectangleF dst = new RectangleF(1280 - 34 - src.Width * k, 640 - 34 - src.Height * k, src.Width * k, src.Height * k);
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                        using (GraphicsPath ps = Theme.Round(new RectangleF(dst.X + 4, dst.Y + 14, dst.Width, dst.Height), 14)) g.FillPath(sb, ps);
                        g.DrawImage(st, dst, src, GraphicsUnit.Pixel);
                    }
                b.Save(Path.Combine(docs, "hero.png"), ImageFormat.Png);
            }
        }

        // Image with rounded corners, shadow and rim light.
        static void Framed(Graphics g, Image img, Rectangle src, RectangleF dst, float radius)
        {
            using (GraphicsPath p = Theme.Round(dst, radius))
            {
                RectangleF sh = dst;
                sh.Offset(0, 18);
                using (GraphicsPath ps = Theme.Round(sh, radius))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(sb, ps);
                Region old = g.Clip;
                g.SetClip(p);
                g.DrawImage(img, dst, src, GraphicsUnit.Pixel);
                g.Clip = old;
                using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.5f)) g.DrawPath(pen, p);
            }
        }

        static void Glow(Graphics g, RectangleF r, Color c)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(r);
                using (PathGradientBrush pb = new PathGradientBrush(p))
                {
                    pb.CenterColor = c;
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillEllipse(pb, r);
                }
            }
        }

        static Bitmap Desktop(Size size)
        {
            Bitmap b = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (LinearGradientBrush bg = new LinearGradientBrush(new Point(0, 0), new Point(size.Width, size.Height), Color.FromArgb(40, 24, 96), Color.FromArgb(10, 90, 130)))
                    g.FillRectangle(bg, 0, 0, size.Width, size.Height);
                Glow(g, new RectangleF(-300, -200, 1200, 900), Color.FromArgb(120, 168, 85, 247));
                Glow(g, new RectangleF(size.Width - 900, size.Height - 700, 1200, 900), Color.FromArgb(110, 34, 211, 238));
                Glow(g, new RectangleF(size.Width / 2 - 400, -300, 900, 700), Color.FromArgb(70, 99, 102, 241));

                // A fake chat app.
                Rectangle win = new Rectangle(320, 150, 1360, 800);
                using (GraphicsPath sh = Theme.Round(new RectangleF(win.X, win.Y + 18, win.Width, win.Height), 14))
                using (SolidBrush s = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(s, sh);
                using (GraphicsPath p = Theme.Round(win, 14))
                using (SolidBrush s = new SolidBrush(Color.FromArgb(26, 27, 38))) g.FillPath(s, p);
                Rectangle title = new Rectangle(win.X, win.Y, win.Width, 46);
                using (GraphicsPath p = Theme.Round(new RectangleF(title.X, title.Y, title.Width, title.Height + 14), 14))
                using (SolidBrush s = new SolidBrush(Color.FromArgb(22, 22, 30))) g.FillPath(s, p);
                using (Font f = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel)) g.DrawString("Equipo \u00B7 Soporte", f, new SolidBrush(Color.FromArgb(192, 202, 245)), win.X + 20, win.Y + 13);
                // Sidebar.
                Rectangle side = new Rectangle(win.X, win.Y + 46, 260, win.Height - 46);
                g.FillRectangle(new SolidBrush(Color.FromArgb(22, 22, 30)), side);
                string[] names = { "Laura", "Marcos", "Ana", "Dise\u00F1o", "Backend", "Pruebas" };
                Color[] av = { Color.FromArgb(247, 118, 142), Color.FromArgb(158, 206, 106), Color.FromArgb(122, 162, 247), Color.FromArgb(224, 175, 104), Color.FromArgb(187, 154, 247), Color.FromArgb(125, 207, 255) };
                for (int i = 0; i < names.Length; i++)
                {
                    int y = side.Y + 24 + i * 62;
                    if (i == 0) using (GraphicsPath p = Theme.Round(new RectangleF(side.X + 10, y - 8, side.Width - 20, 52), 10)) g.FillPath(new SolidBrush(Color.FromArgb(41, 46, 66)), p);
                    g.FillEllipse(new SolidBrush(av[i]), side.X + 22, y, 36, 36);
                    using (Font f = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel)) g.DrawString(names[i], f, new SolidBrush(Color.FromArgb(192, 202, 245)), side.X + 70, y + 1);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(60, 66, 97)), side.X + 70, y + 24, 110 - i * 9, 6);
                }
                // Messages.
                int mx = side.Right + 40, my = win.Y + 80;
                Bubble(g, mx, my, "Laura", "\u00BFPuedes mirar por qu\u00E9 falla el inicio de sesi\u00F3n?", false);
                Bubble(g, mx + 330, my + 110, "T\u00FA", "Claro, te paso una captura con lo que veo", true);
                // A chart panel.
                Rectangle chart = new Rectangle(mx, my + 230, 620, 300);
                using (GraphicsPath p = Theme.Round(chart, 12)) g.FillPath(new SolidBrush(Color.FromArgb(36, 40, 59)), p);
                using (Font f = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel)) g.DrawString("Errores de inicio de sesi\u00F3n (\u00FAltimos 7 d\u00EDas)", f, new SolidBrush(Color.FromArgb(192, 202, 245)), chart.X + 20, chart.Y + 16);
                int[] vals = { 40, 55, 38, 120, 210, 90, 60 };
                for (int i = 0; i < vals.Length; i++)
                {
                    RectangleF bar = new RectangleF(chart.X + 40 + i * 80, chart.Bottom - 30 - vals[i], 44, vals[i]);
                    using (GraphicsPath p = Theme.Round(bar, 6))
                    using (LinearGradientBrush br = new LinearGradientBrush(new PointF(0, bar.Top), new PointF(0, bar.Bottom), i == 4 ? Color.FromArgb(247, 118, 142) : Color.FromArgb(122, 162, 247), i == 4 ? Color.FromArgb(200, 80, 110) : Color.FromArgb(90, 120, 220)))
                        g.FillPath(br, p);
                }
                // A code block with the error.
                Rectangle code = new Rectangle(mx + 650, my + 230, 380, 300);
                using (GraphicsPath p = Theme.Round(code, 12)) g.FillPath(new SolidBrush(Color.FromArgb(22, 22, 30)), p);
                string[] lines = { "POST /api/login", "status: 500", "error: token expirado", "  at auth.verify()", "  at session.start()" };
                Color[] lc = { Color.FromArgb(125, 207, 255), Color.FromArgb(247, 118, 142), Color.FromArgb(224, 175, 104), Color.FromArgb(86, 95, 137), Color.FromArgb(86, 95, 137) };
                using (Font f = new Font("Consolas", 16, GraphicsUnit.Pixel))
                    for (int i = 0; i < lines.Length; i++) g.DrawString(lines[i], f, new SolidBrush(lc[i]), code.X + 22, code.Y + 24 + i * 30);
                // Message box.
                Rectangle input = new Rectangle(mx, win.Bottom - 74, win.Right - mx - 30, 48);
                using (GraphicsPath p = Theme.Round(input, 24)) g.FillPath(new SolidBrush(Color.FromArgb(36, 40, 59)), p);
                using (Font f = new Font("Segoe UI", 15, GraphicsUnit.Pixel)) g.DrawString("Escribe un mensaje\u2026", f, new SolidBrush(Color.FromArgb(86, 95, 137)), input.X + 22, input.Y + 14);
            }
            return b;
        }

        static void Bubble(Graphics g, int x, int y, string who, string text, bool mine)
        {
            using (Font f = new Font("Segoe UI", 16, GraphicsUnit.Pixel))
            using (Font fw = new Font("Segoe UI Semibold", 13, GraphicsUnit.Pixel))
            {
                SizeF sz = g.MeasureString(text, f);
                RectangleF r = new RectangleF(x, y + 20, sz.Width + 32, sz.Height + 22);
                g.DrawString(who, fw, new SolidBrush(Color.FromArgb(115, 124, 165)), x + 4, y);
                using (GraphicsPath p = Theme.Round(r, 16))
                    g.FillPath(new SolidBrush(mine ? Color.FromArgb(122, 162, 247) : Color.FromArgb(41, 46, 66)), p);
                g.DrawString(text, f, new SolidBrush(mine ? Color.FromArgb(26, 27, 38) : Color.FromArgb(192, 202, 245)), r.X + 16, r.Y + 11);
            }
        }
    }
}
