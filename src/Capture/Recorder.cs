// Stackshot - MP4/GIF screen recording through FFmpeg.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    // Stackshot grabs the frames itself (physical pixels, any DPI, with cursor) and pipes raw BGRA to FFmpeg, which
    // only encodes. Stackshot windows are excluded from capture.
    public static class Recorder
    {
        static Session current;

        public static bool Recording { get { return current != null; } }

        // Only the user's explicit choice or the verified download: never whatever ffmpeg.exe happens to be on PATH
        // (a user-writable PATH entry could shadow it).
        public static string FindFfmpeg(Settings s)
        {
            if (!string.IsNullOrEmpty(s.Ffmpeg) && File.Exists(s.Ffmpeg)) return s.Ffmpeg;
            string mine = Path.Combine(Settings.FfmpegDir, "ffmpeg.exe");
            return File.Exists(mine) ? mine : null;
        }

        public static void Toggle(ShotStack owner, Settings s, bool gif)
        {
            if (current != null) { current.Stop(); return; }
            string ffmpeg = FindFfmpeg(s);
            if (ffmpeg == null) ffmpeg = FfmpegSetup.Run(s);
            if (ffmpeg == null) return;
            Bitmap frozen;
            Rectangle vsr;
            IntPtr win;
            Rectangle r = RegionPicker.Pick(gif ? RegionPicker.Mode.Gif : RegionPicker.Mode.Video, out frozen, out vsr, out win);
            frozen.Dispose();
            if (r.IsEmpty) return;
            // H.264 needs even dimensions.
            r.Width = Math.Max(16, r.Width & ~1);
            r.Height = Math.Max(16, r.Height & ~1);
            current = new Session(owner, s, ffmpeg, r, gif);
            current.Start();
        }

        public static void Stop()
        {
            if (current != null) current.Stop();
        }

        internal static void Ended(Session s)
        {
            if (current == s) current = null;
        }

        // Inputs the user opens: only real video containers, never playlists or concat lists that could pull other files.
        internal const string SafeInput = "-format_whitelist mov,mp4,m4a,3gp,3g2,mj2,matroska,webm,gif,avi ";

        internal static string Quote(string path) { return "\"" + path + "\""; }
    }

    class Session
    {
        readonly ShotStack owner;
        readonly string ffmpeg;
        readonly Rectangle area;
        readonly Settings settings;
        readonly bool gif, max, high;
        readonly int fps, gifFps;
        readonly Stopwatch clock = new Stopwatch();
        readonly StringBuilder errors = new StringBuilder();
        string output, video;
        Process proc;
        Thread worker;
        volatile bool stopping, cancelled;
        string error;
        RecordBar bar;
        WebcamBubble webcam;
        readonly int cameraShape;
        FrameEdge[] edges;

        public Session(ShotStack owner, Settings s, string ffmpeg, Rectangle area, bool gif)
        {
            this.owner = owner;
            this.ffmpeg = ffmpeg;
            this.area = area;
            this.gif = gif;
            max = !gif && s.VideoQuality == 1;
            high = !gif && s.VideoQuality == 2;
            fps = gif ? Math.Max(10, s.GifFps * 2) : max || high ? 60 : s.VideoFps;
            gifFps = s.GifFps;
            cameraShape = gif ? 0 : s.Webcam;
            settings = s;
        }

        public TimeSpan Elapsed { get { return clock.Elapsed; } }
        public bool Gif { get { return gif; } }

        public void Start()
        {
            Directory.CreateDirectory(Settings.TempDir);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
            output = ShotStack.Unique(Path.Combine(Settings.TempDir, (gif ? "GIF " : "V\u00EDdeo ") + stamp + (gif ? ".gif" : ".mp4")));
            // GIFs and maximum-quality videos are first recorded to a hidden "~" file and encoded afterwards: capturing
            // losslessly with the fastest preset keeps up at 60 fps, and the slow, high-quality encode runs once at the end.
            video = gif || max ? Path.Combine(Settings.TempDir, "~grabando " + stamp + ".mkv") : output;
            string enc = gif ? "-c:v libx264 -preset ultrafast -crf 10 -pix_fmt yuv444p "
                       : max ? "-c:v libx264 -preset ultrafast -qp 0 -pix_fmt yuv444p "
                       : high ? "-c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p -movflags +faststart "
                             : "-c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p -movflags +faststart ";
            string args = "-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -video_size " + area.Width + "x" + area.Height +
                          " -framerate " + fps + " -i - " + enc + Recorder.Quote(video);
            try
            {
                proc = StartFfmpeg(args, true);
            }
            catch (Exception ex)
            {
                ShotStack.Log("FFmpeg: " + ex.Message);
                MessageBox.Show("No se pudo arrancar FFmpeg: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Recorder.Ended(this);
                return;
            }
            edges = FrameEdge.Around(area);
            bar = new RecordBar(this, area);
            if (cameraShape > 0)
            {
                webcam = new WebcamBubble(settings, ffmpeg, area);
                webcam.Show();
            }
            Native.timeBeginPeriod(1);
            clock.Start();
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Priority = ThreadPriority.AboveNormal;
            worker.Start();
            ShotStack.Log("Grabando " + (gif ? "GIF" : "v\u00EDdeo") + " " + area.Width + "x" + area.Height + " a " + fps + " fps");
        }

        Process StartFfmpeg(string args, bool input)
        {
            Process p = new Process();
            p.StartInfo.FileName = ffmpeg;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = input;
            p.StartInfo.RedirectStandardError = true;
            p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (errors) { if (errors.Length < 4000) errors.AppendLine(e.Data); }
            };
            p.Start();
            p.BeginErrorReadLine();
            return p;
        }

        public void Stop()
        {
            if (stopping) return;
            stopping = true;
            clock.Stop();
            if (bar != null) bar.Saving();
            if (webcam != null) { webcam.Close(); webcam = null; }
        }

        public void Cancel()
        {
            cancelled = true;
            Stop();
        }

        // Capture thread: grabs at a fixed rate and pipes frames to FFmpeg. Late frames are duplicated so the video
        // keeps real-time duration.
        void Loop()
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), bits, dib, old;
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = area.Width;
            bi.biHeight = -area.Height; // top-down, as FFmpeg expects
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            dib = Native.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = Native.SelectObject(mem, dib);
            byte[] buf = new byte[area.Width * area.Height * 4];
            Stream input = proc.StandardInput.BaseStream;
            double interval = 1000.0 / fps;
            // CAPTUREBLT so the camera bubble (a layered window) is part of the video.
            int rop = Native.SRCCOPY | (cameraShape > 0 ? Native.CAPTUREBLT : 0);
            long frame = 0;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                while (!stopping)
                {
                    double now = sw.Elapsed.TotalMilliseconds, due = frame * interval;
                    if (now < due)
                    {
                        Thread.Sleep(Math.Max(1, (int)(due - now)));
                        continue;
                    }
                    Native.BitBlt(mem, 0, 0, area.Width, area.Height, screen, area.X, area.Y, rop);
                    Grabber.DrawCursor(mem, area.X, area.Y);
                    Marshal.Copy(bits, buf, 0, buf.Length);
                    int count = 1 + (int)Math.Floor((now - due) / interval);
                    for (int i = 0; i < count && !stopping; i++)
                    {
                        input.Write(buf, 0, buf.Length);
                        frame++;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancelled) error = "FFmpeg se ha cerrado durante la grabaci\u00F3n. " + Tail();
                ShotStack.Log("Grabaci\u00F3n: " + ex.Message + " " + Tail());
            }
            finally
            {
                Native.SelectObject(mem, old);
                Native.DeleteObject(dib);
                Native.DeleteDC(mem);
                Native.ReleaseDC(IntPtr.Zero, screen);
                try { input.Close(); } catch { }
            }
            Finish();
        }

        // Still on the capture thread: wait for FFmpeg to finalize the file and convert to GIF if needed.
        void Finish()
        {
            if (cancelled)
            {
                try { if (!proc.WaitForExit(3000)) proc.Kill(); } catch { }
            }
            else if (!proc.WaitForExit(60000))
            {
                try { proc.Kill(); } catch { }
                if (error == null) error = "FFmpeg no termin\u00F3 de guardar el v\u00EDdeo.";
            }
            if (error == null && !cancelled && proc.ExitCode != 0) error = "FFmpeg no pudo guardar el v\u00EDdeo. " + Tail();
            if (error == null && !cancelled && gif)
            {
                owner.Ui(delegate { if (bar != null) bar.Converting("Creando el GIF\u2026"); });
                // Per-GIF palette with light dithering: accurate colors, small files.
                int w = Math.Min(area.Width, 960) & ~1;
                lock (errors) errors.Length = 0;
                string args = "-hide_banner -loglevel error -y -i " + Recorder.Quote(video) + " -vf \"fps=" + gifFps + ",scale=" + w +
                              ":-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle\" -loop 0 " +
                              Recorder.Quote(output);
                try
                {
                    using (Process conv = StartFfmpeg(args, false))
                    {
                        conv.WaitForExit();
                        if (conv.ExitCode != 0) error = "No se pudo crear el GIF. " + Tail();
                    }
                }
                catch (Exception ex) { error = "No se pudo crear el GIF: " + ex.Message; }
                TryDelete(video);
            }
            if (error == null && !cancelled && max)
            {
                owner.Ui(delegate { if (bar != null) bar.Converting("Guardando en m\u00E1xima calidad\u2026"); });
                lock (errors) errors.Length = 0;
                // Lossless 4:4:4 capture -> very high quality 4:2:0 H.264 that every player and browser can open.
                string args = "-hide_banner -loglevel error -y -i " + Recorder.Quote(video) +
                              " -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -movflags +faststart " + Recorder.Quote(output);
                try
                {
                    using (Process conv = StartFfmpeg(args, false))
                    {
                        conv.WaitForExit();
                        if (conv.ExitCode != 0) error = "No se pudo guardar el v\u00EDdeo. " + Tail();
                    }
                }
                catch (Exception ex) { error = "No se pudo guardar el v\u00EDdeo: " + ex.Message; }
                TryDelete(video);
            }
            if (cancelled || error != null)
            {
                TryDelete(video);
                TryDelete(output);
            }
            try { proc.Dispose(); } catch { }
            owner.Ui(Done);
        }

        void Done()
        {
            Native.timeEndPeriod(1);
            if (bar != null) bar.Close();
            if (webcam != null) { webcam.Close(); webcam = null; }
            if (edges != null) foreach (FrameEdge e in edges) e.Close();
            Recorder.Ended(this);
            if (error != null)
            {
                ShotStack.Log("Grabaci\u00F3n: " + error);
                MessageBox.Show(error, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!cancelled && File.Exists(output)) owner.AddRecording(output);
        }

        string Tail()
        {
            lock (errors)
            {
                string t = errors.ToString().Trim();
                return t.Length > 300 ? t.Substring(t.Length - 300) : t;
            }
        }

        static void TryDelete(string f)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    // Floating bar while recording. Excluded from the capture.
    class RecordBar : FloatWindow
    {
        readonly Session session;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        string state = "rec";     // rec | saving | convert
        int hot = -1;
        double pulse;

        public RecordBar(Session session, Rectangle area)
        {
            this.session = session;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            BackColor = Theme.Dark;
            Size sz = new Size(P(248), P(44));
            Rectangle wa = scr.WorkingArea;
            int x = area.X + (area.Width - sz.Width) / 2;
            int y = area.Bottom + P(12);
            if (y + sz.Height > wa.Bottom) y = area.Top - sz.Height - P(12);
            if (y < wa.Top) y = area.Bottom - sz.Height - P(16); // full-screen area: place it inside (it is excluded from the video anyway)
            x = Math.Max(wa.Left + P(8), Math.Min(wa.Right - sz.Width - P(8), x));
            SetSize(sz);
            JumpTo(x, y + P(8));
            ShowQuiet();
            alpha.Go(1, 180, 0, Ease.OutCubic, null);
            MoveTo(x, y, 320, 0.8, 0);
            tick.Interval = 50;
            tick.Tick += delegate { pulse += 0.05; Invalidate(); };
            tick.Start();
        }

        public void Saving() { state = "saving"; hot = -1; Invalidate(); }
        public void Converting(string label) { state = "convert"; convertLabel = label; Invalidate(); }
        string convertLabel;

        Rectangle StopRect() { return new Rectangle(ClientSize.Width - P(44) - P(96), P(7), P(96), ClientSize.Height - P(14)); }
        Rectangle CancelRect() { return new Rectangle(ClientSize.Width - P(40), P(8), P(30), ClientSize.Height - P(16)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Dark);
            int cy = ClientSize.Height / 2;
            if (state == "rec")
            {
                double a = 0.55 + 0.45 * Math.Cos(pulse * Math.PI);
                int d = P(12);
                using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(70 * a), 255, 59, 48))) g.FillEllipse(glow, P(14) - P(4), cy - d / 2 - P(4), d + P(8), d + P(8));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 59, 48))) g.FillEllipse(b, P(14), cy - d / 2, d, d);
                TimeSpan t = session.Elapsed;
                string time = ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00");
                TextRenderer.DrawText(g, time, Fonts.Get("Segoe UI Semibold", P(14)), new Rectangle(P(34), 0, P(60), ClientSize.Height), Theme.Fg,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                Rectangle sr = StopRect();
                using (GraphicsPath p = Theme.Round(sr, sr.Height / 2f))
                using (SolidBrush b = new SolidBrush(hot == 0 ? Theme.AccentHover : Theme.Accent)) g.FillPath(b, p);
                int sq = P(9);
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, sr.X + P(14), cy - sq / 2, sq, sq);
                TextRenderer.DrawText(g, "Detener", Fonts.Get("Segoe UI Semibold", P(13)), new Rectangle(sr.X + P(28), sr.Y, sr.Width - P(30), sr.Height),
                                      Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                Rectangle cr = CancelRect();
                if (hot == 1) using (SolidBrush b = new SolidBrush(Theme.ButtonHover)) g.FillEllipse(b, cr);
                DrawGlyph(g, "\uE711", cr, hot == 1 ? Theme.Fg : Theme.Muted, P(11));
            }
            else
            {
                string text = state == "convert" ? convertLabel : "Guardando\u2026";
                int dots = (int)(pulse * 2) % 4;
                TextRenderer.DrawText(g, text, Fonts.Get("Segoe UI Semibold", P(13)), ClientRectangle, Theme.Fg,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                for (int i = 0; i < 3; i++)
                {
                    int d = P(5);
                    using (SolidBrush b = new SolidBrush(i == dots % 3 ? Theme.Accent : Theme.Border))
                        g.FillEllipse(b, ClientSize.Width - P(40) + i * P(9), cy - d / 2, d, d);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = state != "rec" ? -1 : StopRect().Contains(e.Location) ? 0 : CancelRect().Contains(e.Location) ? 1 : -1;
            if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || state != "rec") return;
            if (StopRect().Contains(e.Location)) session.Stop();
            else if (CancelRect().Contains(e.Location)) session.Cancel();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            tick.Dispose();
        }
    }

    // Red frame around the recorded area: four click-through strips, excluded from the video.
    class FrameEdge : FloatWindow
    {
        FrameEdge(Rectangle r)
        {
            BackColor = Color.FromArgb(255, 59, 48);
            SetSize(r.Size);
            JumpTo(r.X, r.Y);
            alpha.Set(0.9);
            ShowQuiet();
            ApplyAlpha();
        }

        protected override bool Rounded { get { return false; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: clicks pass through
                return cp;
            }
        }

        public static FrameEdge[] Around(Rectangle a)
        {
            int t = Math.Max(2, (int)Math.Round(2 * ShotStack.ScaleFor(Screen.FromRectangle(a))));
            return new FrameEdge[]
            {
                new FrameEdge(new Rectangle(a.X - t, a.Y - t, a.Width + 2 * t, t)),
                new FrameEdge(new Rectangle(a.X - t, a.Bottom, a.Width + 2 * t, t)),
                new FrameEdge(new Rectangle(a.X - t, a.Y, t, a.Height)),
                new FrameEdge(new Rectangle(a.Right, a.Y, t, a.Height))
            };
        }
    }

    // Downloads FFmpeg on first use.
    // Security: pinned version from a fixed URL, verified against the SHA-256 below (it matches the digest GitHub
    // publishes). Nothing is installed on mismatch. Update Version, Url and Sha256 together.
    public class FfmpegSetup : DarkForm
    {
        const string Version = "9.0.2";
        const string Zip = "ffmpeg-9.0.2-essentials_build.zip";
        const string Url = "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip";
        const string Sha256 = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";
        readonly Settings settings;
        readonly Pill download, browse, cancel;
        readonly Progress bar;
        readonly Label status;
        WebClient web;
        string result, zipPath;
        bool installing;

        // s: the running settings (a custom ffmpeg.exe path is stored there).
        public static string Run(Settings s)
        {
            using (FfmpegSetup f = new FfmpegSetup(s))
            {
                f.ShowDialog();
                return f.result;
            }
        }

        FfmpegSetup(Settings s) : base(460, 268)
        {
            settings = s;
            AddLabel("Grabar v\u00EDdeo y GIF", 28, 28, 400, 20, Theme.Fg, true, ContentAlignment.TopLeft);
            AddLabel("Para grabar, Stackshot usa FFmpeg " + Version + ", un programa libre y gratuito. Se descarga una sola vez (unos 110 MB) " +
                     "desde GitHub, se comprueba su firma y se guarda solo para tu usuario.", 28, 64, 404, 13, Theme.Fg2, false, ContentAlignment.TopLeft);
            bar = new Progress();
            bar.Bounds = new Rectangle(P(28), P(140), P(404), P(6));
            bar.Visible = false;
            Controls.Add(bar);
            status = AddLabel("", 28, 154, 404, 12, Theme.Muted, false, ContentAlignment.TopLeft);
            download = new Pill("Descargar", true);
            download.Font = new Font("Segoe UI Semibold", P(13), GraphicsUnit.Pixel);
            download.Bounds = new Rectangle(P(312), P(204), P(120), P(36));
            download.Click += delegate { Start(); };
            browse = new Pill("Ya lo tengo\u2026", false);
            browse.Font = download.Font;
            browse.Bounds = new Rectangle(P(180), P(204), P(124), P(36));
            browse.Click += delegate { Browse(); };
            cancel = new Pill("Cancelar", false);
            cancel.Font = download.Font;
            cancel.Bounds = new Rectangle(P(28), P(204), P(100), P(36));
            cancel.Click += delegate { Close(); };
            Controls.Add(download);
            Controls.Add(browse);
            Controls.Add(cancel);
        }

        void Browse()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "ffmpeg.exe|ffmpeg.exe";
                d.Title = "\u00BFD\u00F3nde est\u00E1 ffmpeg.exe?";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                settings.Ffmpeg = d.FileName;
                settings.Save();
                result = d.FileName;
                Close();
            }
        }

        void Start()
        {
            download.Enabled = false;
            browse.Enabled = false;
            bar.Visible = true;
            status.ForeColor = Theme.Muted;
            status.Text = "Conectando\u2026";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Directory.CreateDirectory(Settings.FfmpegDir);
                zipPath = Path.Combine(Settings.FfmpegDir, Zip);
                web = new WebClient();
                web.Headers[HttpRequestHeader.UserAgent] = "Stackshot";
                if (web.Proxy != null) web.Proxy.Credentials = CredentialCache.DefaultCredentials; // corporate proxies
                web.DownloadProgressChanged += delegate(object o, DownloadProgressChangedEventArgs e)
                {
                    if (e.TotalBytesToReceive > 0) bar.Value = e.BytesReceived / (double)e.TotalBytesToReceive;
                    status.Text = (e.BytesReceived / 1048576) + " MB" + (e.TotalBytesToReceive > 0 ? " de " + (e.TotalBytesToReceive / 1048576) + " MB" : "");
                };
                web.DownloadFileCompleted += delegate(object o, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    if (e.Cancelled || IsDisposed) { TryDelete(zipPath); return; }
                    if (e.Error != null) { TryDelete(zipPath); Fail("No se pudo descargar: " + e.Error.Message); return; }
                    // Verifying and extracting takes a few seconds; closing is blocked meanwhile.
                    installing = true;
                    cancel.Enabled = false;
                    status.Text = "Comprobando la firma y preparando\u2026";
                    ThreadPool.QueueUserWorkItem(delegate { Install(); });
                };
                web.DownloadFileAsync(new Uri(Url), zipPath);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // Verifies the pinned SHA-256 and extracts only ffmpeg.exe.
        void Install()
        {
            string error = null, exe = Path.Combine(Settings.FfmpegDir, "ffmpeg.exe");
            try
            {
                string actual;
                using (FileStream fs = File.OpenRead(zipPath))
                using (SHA256 sha = SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (actual != Sha256) error = "La descarga no coincide con la firma esperada; se ha descartado por seguridad.";
                else
                {
                    bool found = false;
                    string tmp = exe + ".part";
                    using (ZipArchive z = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in z.Entries)
                        {
                            if (!entry.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) continue;
                            entry.ExtractToFile(tmp, true);
                            found = true;
                            break;
                        }
                    }
                    if (!found) error = "El paquete descargado no trae ffmpeg.exe.";
                    else
                    {
                        if (File.Exists(exe)) File.Delete(exe);
                        File.Move(tmp, exe);
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; }
            TryDelete(zipPath);
            TryDelete(exe + ".part");
            Action done = delegate
            {
                installing = false;
                cancel.Enabled = true;
                if (error != null) { Fail(error); return; }
                ShotStack.Log("FFmpeg " + Version + " listo en " + exe);
                result = exe;
                Close();
            };
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(done); }
            catch (Exception ex) { ShotStack.Log("FFmpeg: " + ex.Message); }
        }

        static void TryDelete(string f)
        {
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }

        void Fail(string message)
        {
            ShotStack.Log("FFmpeg: " + message);
            status.Text = message;
            status.ForeColor = Theme.Red;
            bar.Visible = false;
            download.Enabled = true;
            browse.Enabled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (installing) { e.Cancel = true; return; }
            base.OnFormClosing(e);
            if (web != null && web.IsBusy) web.CancelAsync();
        }
    }
}
