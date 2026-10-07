// Stackshot - Camera bubble for screen recordings.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    // Loom-style round or square bubble with the webcam, placed inside the recorded area so it ends up in the video
    // (it is the one Stackshot window that is not excluded from capture). FFmpeg reads the camera through DirectShow,
    // crops it square, mirrors it and pipes raw BGRA; a background thread keeps only the latest frame. Drag to move,
    // double-click to switch round/square, right-click to change the size.
    public class WebcamBubble : Form
    {
        static readonly int[] Sizes = { 150, 210, 280 };
        const int Frame = 360; // camera pixels per side, scaled down to the bubble

        readonly Settings settings;
        readonly string ffmpeg;
        readonly Rectangle area;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly object gate = new object();
        readonly StringBuilder errors = new StringBuilder();
        Process proc;
        byte[] latest;
        bool fresh;
        volatile bool closing;
        Bitmap frame;
        Dib dib;
        float s;
        int D, M, W, wx, wy;
        string status = "Conectando la c\u00E1mara\u2026";
        bool down, dragged, live;
        Point grab, downAt;

        public WebcamBubble(Settings settings, string ffmpeg, Rectangle area)
        {
            this.settings = settings;
            this.ffmpeg = ffmpeg;
            this.area = area;
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            s = ShotStack.ScaleFor(Screen.FromRectangle(area));
            M = P(16);
            Measure();
            // Bottom-left corner of the recording, like Loom.
            wx = area.X + P(24) - M;
            wy = area.Bottom - P(24) - D - M;
            Bounds = new Rectangle(wx, wy, W, W);
            timer.Interval = 31;
            timer.Tick += delegate { if (fresh) Render(); };
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // The bubble never takes more than a third of the recorded area's short side.
        void Measure()
        {
            D = Math.Max(P(72), Math.Min(P(Sizes[Math.Max(0, Math.Min(2, settings.WebcamSize))]), Math.Min(area.Width, area.Height) / 3));
            W = D + 2 * M;
            if (dib != null) dib.Dispose();
            dib = new Dib(W, W, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000080 | 0x00000008 | 0x08000000; // WS_EX_LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message msg)
        {
            if (msg.Msg == 0x0021) { msg.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref msg);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
            timer.Start();
            Thread t = new Thread(Run);
            t.IsBackground = true;
            t.Start();
        }

        // DirectShow video devices as FFmpeg lists them. Handles both the old layout (a "video devices" section) and
        // the newer one ("Name" (video) on each line).
        public static List<string> ListDevices(string ffmpeg)
        {
            List<string> list = new List<string>();
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -list_devices true -f dshow -i dummy";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;
                    // Read asynchronously with a time limit: a stuck DirectShow driver must not leave the bubble waiting forever.
                    StringBuilder sb = new StringBuilder();
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.Start();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } }
                    p.WaitForExit(1000);
                    string text;
                    lock (sb) text = sb.ToString();
                    bool videoSection = false;
                    foreach (string raw in text.Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.Contains("DirectShow video devices")) { videoSection = true; continue; }
                        if (line.Contains("DirectShow audio devices")) { videoSection = false; continue; }
                        if (line.Contains("Alternative name")) continue;
                        int a = line.IndexOf('"'), b = line.LastIndexOf('"');
                        if (a < 0 || b <= a + 1) continue;
                        bool video = line.EndsWith("(video)") || (videoSection && !line.EndsWith("(audio)"));
                        if (video) list.Add(line.Substring(a + 1, b - a - 1));
                    }
                }
            }
            catch (Exception ex) { ShotStack.Log("C\u00E1mara: " + ex.Message); }
            return list;
        }

        // Background thread: find the camera, start FFmpeg and hand over frames until closed.
        void Run()
        {
            string device = settings.WebcamDevice;
            if (string.IsNullOrEmpty(device))
            {
                List<string> found = ListDevices(ffmpeg);
                device = found.Count > 0 ? found[0] : null;
            }
            if (closing) return;
            if (device == null) { Fail("No se encuentra ninguna c\u00E1mara"); return; }
            device = device.Replace("\"", "").TrimEnd('\\'); // a trailing backslash would escape the closing quote
            int n = Frame * Frame * 4;
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = ffmpeg;
                p.StartInfo.Arguments = "-hide_banner -loglevel error -f dshow -rtbufsize 64M -i video=\"" + device + "\"" +
                                        " -vf \"crop=min(iw\\,ih):min(iw\\,ih),scale=" + Frame + ":" + Frame + ":flags=bicubic,hflip\"" +
                                        " -pix_fmt bgra -f rawvideo -";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                {
                    if (e.Data != null) lock (errors) { if (errors.Length < 2000) errors.AppendLine(e.Data); }
                };
                p.Start();
                p.BeginErrorReadLine();
                lock (gate)
                {
                    if (closing) { try { p.Kill(); } catch { } p.Dispose(); return; }
                    proc = p;
                }
                Stream st = p.StandardOutput.BaseStream;
                byte[] buf = new byte[n];
                while (!closing)
                {
                    int got = 0;
                    while (got < n)
                    {
                        int r = st.Read(buf, got, n - got);
                        if (r <= 0) break;
                        got += r;
                    }
                    if (got < n) break;
                    lock (gate)
                    {
                        byte[] spare = latest;
                        latest = buf;
                        fresh = true;
                        buf = spare ?? new byte[n];
                    }
                }
            }
            catch (Exception ex)
            {
                if (!closing) ShotStack.Log("C\u00E1mara: " + ex.Message);
            }
            if (!closing)
            {
                string err;
                lock (errors) err = errors.ToString().Trim();
                if (err.Length > 0) ShotStack.Log("C\u00E1mara: " + (err.Length > 300 ? err.Substring(err.Length - 300) : err));
                Fail(live ? "La c\u00E1mara se ha desconectado" : "No se puede abrir la c\u00E1mara");
            }
        }

        // Shows the reason for a few seconds, then gets out of the way.
        void Fail(string why)
        {
            if (closing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (closing) return;
                    live = false;
                    status = why;
                    Render();
                    System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                    t.Interval = 3500;
                    t.Tick += delegate { t.Dispose(); if (!closing) Close(); };
                    t.Start();
                });
            }
            catch { }
        }

        void Render()
        {
            lock (gate)
            {
                if (fresh && latest != null)
                {
                    if (frame == null) frame = new Bitmap(Frame, Frame, PixelFormat.Format32bppRgb);
                    BitmapData bd = frame.LockBits(new Rectangle(0, 0, Frame, Frame), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                    try { Marshal.Copy(latest, 0, bd.Scan0, latest.Length); }
                    finally { frame.UnlockBits(bd); }
                    fresh = false;
                    live = true;
                }
            }
            bool round = settings.Webcam != 2;
            RectangleF r = new RectangleF(M, M, D, D);
            using (Graphics g = dib.Graphics())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // Soft shadow: a few stacked, growing silhouettes.
                for (int i = 6; i >= 1; i--)
                {
                    RectangleF sh = r;
                    sh.Inflate(i * M / 9f, i * M / 9f);
                    sh.Offset(0, P(3));
                    using (GraphicsPath p = Shape(sh, round))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(9, 0, 0, 0))) g.FillPath(b, p);
                }
                using (GraphicsPath p = Shape(r, round))
                {
                    if (live && frame != null)
                    {
                        using (TextureBrush tb = new TextureBrush(frame, WrapMode.Clamp))
                        {
                            tb.TranslateTransform(r.X, r.Y);
                            tb.ScaleTransform(D / (float)Frame, D / (float)Frame);
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.FillPath(tb, p);
                        }
                    }
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(44, 44, 48))) g.FillPath(b, p);
                        using (Font gf = new Font(Theme.IconFont, D * 0.16f, GraphicsUnit.Pixel))
                        using (SolidBrush ib = new SolidBrush(Color.FromArgb(152, 152, 159)))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            g.DrawString("\uE714", gf, ib, new RectangleF(r.X, r.Y + D * 0.22f, D, D * 0.25f), sf); // video camera
                            using (Font tf = new Font("Segoe UI", Math.Max(P(10), D * 0.075f), GraphicsUnit.Pixel))
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(235, 235, 240)))
                            {
                                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                                g.DrawString(status, tf, tb, new RectangleF(r.X + D * 0.14f, r.Y + D * 0.48f, D * 0.72f, D * 0.3f), sf);
                            }
                        }
                    }
                    using (Pen edge = new Pen(Color.FromArgb(60, 255, 255, 255), Math.Max(1f, 1.5f * s))) g.DrawPath(edge, p);
                }
            }
            Native.GdiFlush();
            if (IsHandleCreated) Native.Present(Handle, dib.Dc, wx, wy, W, W);
        }

        GraphicsPath Shape(RectangleF r, bool round)
        {
            if (!round) return Theme.Round(r, r.Width * 0.2f);
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        // Keeps the bubble fully inside the recorded area.
        void Clamp()
        {
            wx = Math.Max(area.X - M, Math.Min(area.Right - D - M, wx));
            wy = Math.Max(area.Y - M, Math.Min(area.Bottom - D - M, wy));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            down = true;
            dragged = false;
            downAt = Cursor.Position;
            grab = new Point(downAt.X - wx, downAt.Y - wy);
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!down) { Cursor = Cursors.Hand; return; }
            Point c = Cursor.Position;
            if (!dragged && Math.Abs(c.X - downAt.X) + Math.Abs(c.Y - downAt.Y) < P(4)) return;
            dragged = true;
            wx = c.X - grab.X;
            wy = c.Y - grab.Y;
            Clamp();
            Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            down = false;
            Cursor = Cursors.Hand;
            if (e.Button != MouseButtons.Right) return;
            // Cycle the size, keeping the bubble's center where it was.
            int cx = wx + W / 2, cy = wy + W / 2;
            settings.WebcamSize = (settings.WebcamSize + 1) % Sizes.Length;
            Measure();
            wx = cx - W / 2;
            wy = cy - W / 2;
            Clamp();
            Render();
            settings.Save();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            settings.Webcam = settings.Webcam == 2 ? 1 : 2;
            Render();
            settings.Save();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closing = true;
            timer.Dispose();
            Process p;
            lock (gate) { p = proc; proc = null; }
            if (p != null)
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                try { p.Dispose(); } catch { }
            }
            if (frame != null) frame.Dispose();
            if (dib != null) dib.Dispose();
            base.OnFormClosed(e);
        }
    }
}
