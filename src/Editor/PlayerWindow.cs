// Stackshot - Player window: opens any video FFmpeg can read, with the same engine as the editor (Playback), a bar that
// hides while playing, speed, loop, frame steps, audio tracks, fullscreen and an info panel.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using M = System.Windows.Media;

namespace Stackshot
{
    // A window that plays one video. The bar and the keys are drawn and handled here; Playback does the decoding.
    internal sealed partial class PlayerWindow : Form
    {
        sealed class Btn
        {
            public string Id, Glyph, Text, Tip;
            public Rectangle R;
            public bool On, Pill;
        }

        enum Drag { None, Seek, Volume }

        const int HideMs = 2500;

        readonly ShotStack owner;
        readonly Settings settings;
        readonly string ffmpeg;
        readonly Func<ISoundOut> makeSound;
        float s;
        string path;
        MediaInfo info;
        Playback play;
        List<string[]> infoRows = new List<string[]>();
        double markIn = -1, markOut = -1;   // the piece to export (E and S on the bar), -1 when not set
        bool grabbing;

        // The picture on screen, written by the engine's threads.
        Bitmap frame;
        bool haveFrame, closed;
        readonly object frameLock = new object();
        int paintPending;
        Size decode;

        double volume, speed = 1;
        bool muted, loop, infoOn, menuOpen, fullscreen, cursorHidden;
        string message, flash, exportText;
        int flashUntil;

        readonly List<Btn> btns = new List<Btn>();
        Rectangle bar, seek, vol;
        string hot, pressed;
        Drag drag;
        double scrub;
        bool wasPlaying;
        readonly Stopwatch moved = Stopwatch.StartNew(), stillGap = Stopwatch.StartNew();
        float alpha = 1f;
        Point hoverAt = new Point(-1, -1);
        FormBorderStyle prevStyle;
        Rectangle prevBounds;
        FormWindowState prevState;

        readonly Timer tick = new Timer(), resizeTimer = new Timer(), saveTimer = new Timer();
        System.Threading.Thread exportThread;
        ExportAbort exportAbort;
        string exportOut;

        // For the tests: called with every picture the window takes (playing or still), on the thread that made it.
        Action<PlayerFrame> frameHook;
        internal Action<PlayerFrame> FrameHook { get { return frameHook; } set { frameHook = value; } }

        internal PlayerWindow(ShotStack owner, Settings settings, string ffmpeg, string path, Func<ISoundOut> sound)
        {
            this.owner = owner;
            this.settings = settings;
            this.ffmpeg = ffmpeg;
            makeSound = sound ?? delegate { return new WaveOutSink(); };
            s = ShotStack.ScaleFor(Screen.FromPoint(Control.MousePosition));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            StartPosition = FormStartPosition.Manual;
            AllowDrop = true;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            MinimumSize = new Size(P(520), P(380));
            volume = Math.Max(0, Math.Min(100, settings.PlayerVolume)) / 100.0;
            loop = settings.PlayerLoop;
            tick.Interval = 33;
            tick.Tick += OnTick;
            resizeTimer.Interval = 200;
            resizeTimer.Tick += delegate { resizeTimer.Stop(); Redecode(true); };
            saveTimer.Interval = 500;
            saveTimer.Tick += delegate { saveTimer.Stop(); settings.Save(); };
            Open(path);
            tick.Start();
        }

        public string FilePath { get { return path; } }
        internal Playback Engine { get { return play; } }
        internal MediaInfo Media { get { return info; } }
        internal string Message { get { return message; } }
        internal Size DecodeNow { get { return decode; } }

        int P(float v) { return (int)Math.Round(v * s); }

        public void BringUp()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.ForceForeground(Handle);
        }

        // ---- opening ----

        // Reads the file and sizes the window for it; nothing plays until BeginPlay.
        void Open(string file)
        {
            RememberPosition();
            DisposePlayback();
            path = file;
            message = null;
            flash = null;
            infoOn = false;
            speed = 1;
            markIn = markOut = -1;
            lock (frameLock) haveFrame = false;
            Text = Path.GetFileName(file) + " \u2013 Stackshot";
            Cursor.Current = Cursors.WaitCursor;
            try { info = MediaInfo.ProbeCached(ffmpeg, file); }
            finally { Cursor.Current = Cursors.Default; }
            long bytes = 0;
            try { bytes = new FileInfo(file).Length; }
            catch (Exception) { }
            if (!info.CanPlay)
            {
                message = !File.Exists(file) ? "No se encuentra el archivo." : "No se puede reproducir este archivo: FFmpeg no encuentra un v\u00EDdeo que mostrar.";
                if (ClientSize.Width < P(560)) ClientSize = new Size(P(640), P(360));
                infoRows = new List<string[]>();
                Invalidate();
                return;
            }
            infoRows = PlayerKit.Describe(info, file, bytes, PlayerKit.IsMaster(info) ? PlayerKit.ReadCineReport(file) : null);
            Size video = info.Display;
            if (!fullscreen && WindowState == FormWindowState.Normal)
            {
                Rectangle work = (IsHandleCreated ? Screen.FromControl(this) : Screen.FromPoint(Control.MousePosition)).WorkingArea;
                // At its real size whenever the window fits on the screen with its frame (not a share of the screen).
                ClientSize = PlayerKit.WindowFor(video, work, P(560), new Size(Width - ClientSize.Width, Height - ClientSize.Height), settings.PlayerFill);
                Location = new Point(work.Left + Math.Max(0, (work.Width - Width) / 2), work.Top + Math.Max(0, (work.Height - Height) / 2));
            }
            decode = PlayerKit.DecodeSize(video, ClientSize);
            try
            {
                Playback p = new Playback(ffmpeg, file, info, makeSound());
                p.Scaling = settings.PlayerScaling;
                p.Repeat = loop;
                p.Volume = muted ? 0 : volume;
                foreach (AudioTrackInfo a in info.Audio) if (a.Default) { p.Track = a.Index; break; }
                p.SetDecodeSize(decode.Width, decode.Height);
                p.FrameReady += OnFrame;
                p.StillReady += OnStill;
                p.Ended += delegate { OnUi(delegate { Wake(); }); };
                p.Error += delegate(string m) { OnUi(delegate { message = m; Wake(); }); };
                p.Notice += delegate(string m) { OnUi(delegate { Flash(m); }); };
                play = p;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: " + ex.Message);
                message = I18n.T("No se pudo iniciar la reproducci\u00F3n.", "Could not start playback.");
            }
            Invalidate();
        }

        // Starts from the beginning.
        internal void BeginPlay()
        {
            if (play != null && info != null && info.CanPlay)
            {
                // A long video starts where it was left last time (Inicio goes back to the beginning).
                double at = PlayerKit.ResumeAt(path, info.Duration);
                play.Start(at, info.Duration);
                if (at > 0) Flash(I18n.Fmt("Sigue donde lo dejaste ({0}) \u00B7 Inicio vuelve al principio", "Resume from where you left off ({0}) \u00B7 Home returns to start", PlayerKit.Clock(at)));
            }
            Wake();
        }

        // Where this video was left, for next time (only long ones; forgotten when watched to the end).
        void RememberPosition()
        {
            if (play == null || info == null || !info.CanPlay || path == null) return;
            try { PlayerKit.ResumeSave(path, play.Position, info.Duration); }
            catch (Exception ex) { ShotStack.Log("Reproductor: posici\u00F3n " + ex.Message); }
        }

        void DisposePlayback()
        {
            Playback p = play;
            play = null;
            if (p != null) p.Dispose();
        }

        void OnUi(Action a)
        {
            try { BeginInvoke((Action)delegate { if (!IsDisposed) a(); }); }
            catch (InvalidOperationException) { }
        }

        // Back to a visible bar and a fresh paint.
        void Wake()
        {
            moved.Restart();
            Invalidate();
        }

        bool Playing { get { return play != null && play.State == PlayState.Playing; } }

        double Pos
        {
            get
            {
                if (drag == Drag.Seek) return scrub;
                return play != null ? play.Position : 0;
            }
        }

        double Duration { get { return info != null ? info.Duration : 0; } }

        // ---- pictures ----

        void OnFrame(PlayerFrame f)
        {
            Put(f.Data, f.Width, f.Height);
            Action<PlayerFrame> h = frameHook;
            if (h != null) h(f);
        }

        void OnStill(PlayerFrame f)
        {
            Playback p = play;
            if (p == null || p.State == PlayState.Playing) return;
            Put(f.Data, f.Width, f.Height);
            Action<PlayerFrame> h = frameHook;
            if (h != null) h(f);
        }

        void Put(byte[] data, int w, int h)
        {
            lock (frameLock)
            {
                if (closed) return;
                if (frame == null || frame.Width != w || frame.Height != h)
                {
                    if (frame != null) frame.Dispose();
                    frame = new Bitmap(w, h, PixelFormat.Format32bppRgb);
                }
                BitmapData d = frame.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try
                {
                    if (d.Stride == w * 4) System.Runtime.InteropServices.Marshal.Copy(data, 0, d.Scan0, w * h * 4);
                    else
                        for (int y = 0; y < h; y++)
                            System.Runtime.InteropServices.Marshal.Copy(data, y * w * 4, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w * 4);
                }
                finally { frame.UnlockBits(d); }
                haveFrame = true;
            }
            if (System.Threading.Interlocked.Exchange(ref paintPending, 1) != 0 || !IsHandleCreated) return;
            try { BeginInvoke((Action)RepaintVideo); }
            catch (InvalidOperationException) { paintPending = 0; }
        }

        void RepaintVideo()
        {
            paintPending = 0;
            if (IsDisposed) return;
            Invalidate(VideoRect());
        }

        Rectangle VideoRect()
        {
            return info != null && info.CanPlay ? PlayerKit.Picture(info.Display, ClientSize, settings.PlayerFill) : Rectangle.Empty;
        }

        // The window got another size: the pictures are decoded again at the new one (debounced by the caller).
        void Redecode(bool sizeChanged)
        {
            if (play == null || info == null || !info.CanPlay || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            Size want = PlayerKit.DecodeSize(info.Display, ClientSize);
            if (sizeChanged && want == decode) return;
            decode = want;
            play.SetDecodeSize(want.Width, want.Height);
            if (Playing) play.Seek(play.Position);
            else play.Still(play.Position);
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        // ---- commands ----

        void Toggle()
        {
            if (play == null || info == null || !info.CanPlay) return;
            bool atEnd = Duration > 0 && play.Position >= Duration - 1.5 / info.Fps;
            switch (play.State)
            {
                case PlayState.Playing: play.Pause(); break;
                case PlayState.Paused:
                    if (atEnd) play.Start(0, info.Duration); else play.Resume();
                    break;
                default: play.Start(atEnd ? 0 : play.Position, info.Duration); break;
            }
            Wake();
        }

        // Moves to t (the picture follows at once when paused); playing carries on from there.
        void SeekTo(double t)
        {
            if (play == null || info == null || !info.CanPlay) return;
            if (Duration > 0) t = Math.Min(t, Duration - 0.5 / info.Fps);
            t = Math.Max(0, t);
            play.Seek(t);
            if (!Playing) play.Still(t);
            Wake();
        }

        void Skip(double dt)
        {
            SeekTo(Pos + dt);
        }

        // One frame forward (1) or back (-1), exact: the picture is decoded on its own at that frame.
        internal void Step(int dir)
        {
            if (play == null || info == null || !info.CanPlay) return;
            if (Playing) play.Pause();
            double t = PlayerKit.StepTime(play.Position, info.Fps, Duration, dir);
            play.Seek(t);
            play.Still(t);
            Wake();
        }

        void SetVolume(double v, bool say)
        {
            volume = Math.Max(0, Math.Min(1, Math.Round(v * 100) / 100.0));
            if (volume > 0) muted = false;
            ApplyVolume();
            if (say) Flash(I18n.Fmt("Volumen {0} %", "Volume {0} %", (int)Math.Round(volume * 100)));
            settings.PlayerVolume = (int)Math.Round(volume * 100);
            Remember();
            Invalidate(BarArea());
        }

        void ToggleMute()
        {
            muted = !muted;
            ApplyVolume();
            Flash(muted ? I18n.T("Sin sonido", "Muted") : I18n.Fmt("Volumen {0} %", "Volume {0} %", (int)Math.Round(volume * 100)));
            Invalidate(BarArea());
        }

        void ApplyVolume()
        {
            if (play != null) play.Volume = muted ? 0 : volume;
        }

        void SetSpeed(double v)
        {
            speed = v;
            if (play != null) play.Speed = v;
            Flash(I18n.Fmt("Velocidad {0}", "Speed {0}", PlayerKit.SpeedText(v)));
            Invalidate(BarArea());
        }

        void ToggleLoop()
        {
            loop = !loop;
            if (play != null) play.Repeat = loop;
            settings.PlayerLoop = loop;
            Remember();
            Flash(loop ? I18n.T("Repetir: s\u00ED", "Loop: on") : I18n.T("Repetir: no", "Loop: off"));
            Invalidate(BarArea());
        }

        void SetScaling(int v)
        {
            settings.PlayerScaling = v;
            Remember();
            if (play == null) return;
            play.Scaling = v;
            Redecode(false);
        }

        // Real size (never enlarged: the sharpest) or enlarged to fill the window; the decoded size does not change.
        void SetFill(bool v)
        {
            settings.PlayerFill = v;
            Remember();
            Invalidate();
        }

        internal Rectangle PictureRect { get { return VideoRect(); } }

        void ToggleInfo()
        {
            infoOn = !infoOn;
            Invalidate();
        }

        void Remember()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        void Flash(string text)
        {
            flash = text;
            flashUntil = Environment.TickCount + 1800;
            Invalidate();
        }

        void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                prevState = WindowState;
                prevStyle = FormBorderStyle;
                prevBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                fullscreen = true;
                if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                fullscreen = false;
                FormBorderStyle = prevStyle;
                Bounds = prevBounds;
                WindowState = prevState;
            }
            Invalidate();
        }

        void Reveal()
        {
            string p = path;
            try { Process.Start(Native.Explorer, "/select,\"" + p + "\""); }
            catch (Exception ex) { ShotStack.Log("Reproductor: carpeta " + ex.Message); Flash(I18n.T("No se pudo abrir la carpeta", "Could not open folder")); }
        }

        bool CanEdit { get { return owner != null && info != null && info.CanPlay && Array.IndexOf(PlayerKit.EditorFormats, Path.GetExtension(path ?? "").ToLowerInvariant()) >= 0; } }

        // A whole MP4 is already shareable; a piece of one is not.
        bool CanExport
        {
            get
            {
                return info != null && info.CanPlay && exportThread == null && (HasRange || VideoExport.TargetBytes(settings.ExportQuality) > 0 ||
                       !string.Equals(Path.GetExtension(path ?? ""), ".mp4", StringComparison.OrdinalIgnoreCase));
            }
        }

        // ---- the piece to export: E marks where it starts, S where it ends (both at the frame on screen) ----

        bool HasRange { get { return markIn >= 0 && markOut > markIn + 0.05; } }

        double FrameNow()
        {
            if (play == null || info == null || info.Fps <= 0) return 0;
            return PlayerKit.FrameAt(play.Position, info.Fps) / info.Fps;
        }

        void MarkIn()
        {
            if (info == null || !info.CanPlay) return;
            markIn = FrameNow();
            if (markOut >= 0 && markOut <= markIn + 0.05) markOut = -1;
            Flash(markOut < 0 ? I18n.Fmt("Inicio del tramo en {0} \u00B7 S marca el final", "In-point at {0} \u00B7 S sets out-point", PlayerKit.Clock(markIn)) : I18n.Fmt("Tramo {0} \u2013 {1}", "Clip {0} \u2013 {1}", PlayerKit.Clock(markIn), PlayerKit.Clock(markOut)));
        }

        void MarkOut()
        {
            if (info == null || !info.CanPlay) return;
            // The frame on screen is the last one in.
            double end = Math.Min(Duration, FrameNow() + 1 / info.Fps);
            if (markIn < 0) markIn = 0;
            if (end <= markIn + 0.05) { Flash(I18n.Fmt("El final tiene que ir despu\u00E9s del inicio ({0})", "Out-point must be after in-point ({0})", PlayerKit.Clock(markIn))); return; }
            markOut = end;
            Flash(I18n.Fmt("Tramo {0} \u2013 {1} \u00B7 Exportar lo crea en MP4; G, en GIF", "Clip {0} \u2013 {1} \u00B7 Export creates MP4; G creates GIF", PlayerKit.Clock(markIn), PlayerKit.Clock(markOut)));
        }

        void ClearRange()
        {
            markIn = markOut = -1;
            Flash(I18n.T("Tramo quitado", "Clip cleared"));
        }

        // ---- the frame on screen as a capture (C) ----

        // At its real size (non-square pixels made square), decoded the way the player shows it. With the stack it becomes
        // a capture like any screenshot (copy, edit, save, pin); on its own the player saves it next to the video.
        void GrabFrame()
        {
            if (info == null || !info.CanPlay || grabbing) return;
            if (play != null && Playing) play.Pause();
            double t = FrameNow();
            Size d = info.Display;
            bool scaled = d.Width != info.Width || d.Height != info.Height;
            string ff = ffmpeg, src = path;
            int nw = info.Width, nh = info.Height;
            double step = 1 / info.Fps;
            grabbing = true;
            System.Threading.Thread th = new System.Threading.Thread(delegate()
            {
                // At the very end a seek to the last frame's time can land past it and give nothing: half a frame and
                // then a frame earlier find it.
                byte[] px = null;
                foreach (double back in new double[] { 0, step / 2, step })
                {
                    if (t - back < 0) break;
                    px = PlayerKit.RunBytes(ff, PlayerArgs.Still(src, t - back, scaled ? d.Width : 0, scaled ? d.Height : 0, nw, nh, 0), d.Width * d.Height * 4, 30000);
                    if (px != null) break;
                }
                OnUi(delegate
                {
                    grabbing = false;
                    if (px == null) { Flash(I18n.T("No se pudo capturar el fotograma.", "Could not capture frame.")); return; }
                    Bitmap bmp = PlayerKit.FromBgra(px, d.Width, d.Height);
                    string name = Path.GetFileNameWithoutExtension(src);
                    if (owner != null)
                    {
                        if (owner.SaveCapture(bmp, (I18n.IsSpanish ? "Fotograma " : "Frame ") + name) != null) Flash(I18n.Fmt("Fotograma de {0} en la pila", "Frame at {0} added to stack", PlayerKit.Clock(t)));
                        return;
                    }
                    try
                    {
                        string png = ShotStack.Unique(Path.Combine(Path.GetDirectoryName(src), name + I18n.T(" fotograma ", " frame ") + PlayerKit.Clock(t).Replace(':', '.') + ".png"));
                        bmp.Save(png, ImageFormat.Png);
                        Flash(I18n.Fmt("Fotograma guardado: {0}", "Frame saved: {0}", Path.GetFileName(png)));
                    }
                    catch (Exception ex) { Flash(I18n.T("No se pudo guardar el fotograma: ", "Could not save frame: ") + ex.Message); }
                    finally { bmp.Dispose(); }
                });
            });
            th.IsBackground = true;
            th.Start();
        }

        void Edit()
        {
            if (!CanEdit) return;
            string p = path;
            if (play != null && Playing) play.Pause();
            if (owner.OpenEditor(p)) Close();
        }

        void OpenDialog()
        {
            if (exportThread != null) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Abrir v\u00EDdeo";
                d.Filter = ShotStack.VideoFilter;
                if (d.ShowDialog(this) == DialogResult.OK) OpenAndPlay(d.FileName);
            }
        }

        void OpenAndPlay(string file)
        {
            if (exportThread != null || probing) return;
            if (MediaInfo.IsCached(file)) { Open(file); BeginPlay(); return; }
            // FFmpeg reads it first off the UI thread: the current video keeps playing meanwhile.
            probing = true;
            UseWaitCursor = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                MediaInfo.ProbeCached(ffmpeg, file);
                OnUi(delegate
                {
                    probing = false;
                    UseWaitCursor = false;
                    if (IsDisposed || exportThread != null) return;
                    Open(file);
                    BeginPlay();
                });
            });
        }

        bool probing;

        // ---- export to MP4 ----

        // A plain, shareable MP4 of the whole video: the same profile as a Cine master's export, checked before it is offered.
        void ExportMp4()
        {
            if (!CanExport) return;
            // Next to a saved video (kept like it), else with the temporary captures.
            string dir = owner != null ? owner.ExportFolder(path) : Path.GetDirectoryName(path);
            string output;
            try
            {
                Directory.CreateDirectory(dir);
                output = ShotStack.Reserve(Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + (HasRange ? " (tramo).mp4" : " (para compartir).mp4")));
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: exportar " + ex.Message);
                Flash(I18n.T("No se pudo preparar la exportaci\u00F3n.", "Could not prepare export."));
                return;
            }
            if (play != null && Playing) play.Pause();
            // The whole picture, even-sized for 4:2:0 (never larger than the source: a crop past it fails).
            Rectangle crop = new Rectangle(0, 0, Math.Max(2, info.Width & ~1), Math.Max(2, info.Height & ~1));
            ExportSpec spec = new ExportSpec();
            spec.Source = path;
            spec.Output = output;
            spec.Crop = crop;
            spec.Frame = crop.Size;
            spec.Inner = new Rectangle(Point.Empty, crop.Size);
            spec.SourceIsGif = string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
            spec.HasAudio = info.Audio.Count > 0;
            spec.RgbSource = VideoExport.IsRgb(path, info.Raw);
            spec.Level = settings.ExportQuality;
            spec.Fps = info.Fps;
            // The track being listened to goes into the MP4.
            int track = -1;
            if (play != null) track = play.Track;
            else foreach (AudioTrackInfo a in info.Audio) if (a.Default) { track = a.Index; break; }
            spec.AudioTrack = track >= 0 && track < info.Audio.Count ? track : 0;
            // The piece between the marks, or the whole video.
            if (HasRange) spec.Keep = new List<double[]> { new double[] { markIn, markOut } };
            double dur = HasRange ? markOut - markIn : info.Duration;
            spec.Seconds = dur;
            if (VideoExport.TargetBytes(spec.Level) > 0 && dur <= 0)
            {
                ShotStack.Unreserve(output);
                Alert("No se sabe cu\u00E1nto dura este v\u00EDdeo, as\u00ED que no se puede ajustar a " + Timeline.Qualities[spec.Level] + ". Marca un tramo (E y S) o elige otra calidad en M\u00E1s opciones > Calidad al exportar.");
                return;
            }
            if (VideoExport.TargetBytes(spec.Level) > 0 && VideoExport.TargetKbps(spec) < VideoExport.TargetMinKbps)
            {
                ShotStack.Unreserve(output);
                Alert("Es demasiado largo para caber en " + Timeline.Qualities[spec.Level] + " con buena calidad (caben " + VideoExport.MaxLength(spec.Level, spec.HasAudio) + "). Marca un tramo m\u00E1s corto (E y S) o elige otra calidad en M\u00E1s opciones > Calidad al exportar.");
                return;
            }
            string log = Path.Combine(Path.GetTempPath(), "stackshot-2pass-" + Guid.NewGuid().ToString("N"));
            List<string> passes = VideoExport.Passes(spec, log);
            ShareExpect expect = PlayerKit.ExportExpect(info, spec);
            expect.MaxBytes = VideoExport.TargetBytes(spec.Level);
            string ff = ffmpeg;
            ExportAbort ab = new ExportAbort();
            exportAbort = ab;
            exportOut = output;
            exportText = "Creando el MP4\u2026 0 %  \u00B7  Esc cancela";
            Invalidate();
            ShotStack.Log("Reproductor: exportando " + Path.GetFileName(output));
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                VideoExport.Abort = ab;
                string err = "";
                int code = 0;
                int timeout = (int)Math.Min(6 * 3600 * 1000.0, Math.Max(120000, dur * 20000));
                // Two passes for a size limit: each one is half of the bar (a second pass redone to fit runs it again).
                code = VideoExport.RunPasses(ff, spec, passes, log, timeout, delegate(int done, int count, string line)
                {
                    if (dur <= 0 || !line.StartsWith("out_time=", StringComparison.Ordinal)) return;
                    TimeSpan ts;
                    if (!TimeSpan.TryParse(line.Substring(9).Trim(), CultureInfo.InvariantCulture, out ts)) return;
                    int pc = (int)Math.Round((done + Math.Max(0, Math.Min(1, ts.TotalSeconds / dur))) / count * 100);
                    OnUi(delegate { if (exportAbort == ab) { exportText = "Creando el MP4\u2026 " + pc + " %  \u00B7  Esc cancela"; Invalidate(); } });
                }, out err);
                VideoExport.DeletePassLog(log);
                ShareReport rep = null;
                if (!ab.Set && code == 0 && File.Exists(output))
                {
                    OnUi(delegate { if (exportAbort == ab) { exportText = "Comprobando el v\u00EDdeo\u2026  \u00B7  Esc cancela"; Invalidate(); } });
                    rep = ShareCheck.Validate(ff, output, expect);
                }
                OnUi(delegate { ExportDone(ab, output, code, err, rep); });
            });
            t.IsBackground = true;
            exportThread = t;
            t.Start();
        }

        void ExportDone(ExportAbort ab, string output, int code, string err, ShareReport rep)
        {
            ShotStack.Unreserve(output);
            if (exportAbort != ab) return;
            exportAbort = null;
            exportThread = null;
            exportOut = null;
            exportText = null;
            Invalidate();
            if (ab.Set) { try { if (File.Exists(output)) File.Delete(output); } catch (Exception) { } return; }
            if (code != 0 || !File.Exists(output))
            {
                ShotStack.Log("Reproductor: FFmpeg " + code + ": " + err);
                try { if (File.Exists(output)) File.Delete(output); } catch (Exception) { }
                Alert(I18n.T("No se pudo crear el MP4.\n\n", "Could not create MP4.\n\n") + Tail(err));
                return;
            }
            ShotStack.Log("Reproductor: comprobaci\u00F3n " + (rep != null && rep.Passed ? "correcta" : "FALLIDA") + " de " + Path.GetFileName(output) + "\n" + rep);
            if (rep != null && rep.Passed)
            {
                exportNote = "ok";
                if (owner != null)
                {
                    owner.AddRecording(output);
                    Flash(SaveFolders.Inside(settings, Path.GetDirectoryName(output)) ? I18n.T("MP4 listo y guardado junto al original", "MP4 ready and saved next to original") : I18n.T("MP4 listo: est\u00E1 en la pila", "MP4 ready: added to stack"));
                }
                else Flash(I18n.T("MP4 listo", "MP4 ready"));
                return;
            }
            // Not a normal result: kept under another name so it is never taken for a good export.
            string kept = output;
            try
            {
                kept = ShotStack.Unique(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + " (sin validar)" + Path.GetExtension(output)));
                File.Move(output, kept);
            }
            catch (Exception ex) { kept = output; ShotStack.Log("Reproductor: no se pudo renombrar: " + ex.Message); }
            Alert((rep != null ? rep.Summary : I18n.T("No se pudo comprobar el v\u00EDdeo.", "Could not verify video.")) + I18n.Fmt("\n\nEl archivo no se ha a\u00F1adido a la pila. Queda en:\n{0}", "\n\nThe file was not added to stack. Stored in:\n{0}", kept));
        }

        // A message the user must read; tests set Quiet so nothing blocks.
        internal static bool Quiet { get; set; }
        string exportNote;
        internal string ExportNote { get { return exportNote; } }

        void Alert(string text)
        {
            exportNote = text;
            if (Quiet) Flash(text.Replace("\n", " "));
            else Dialogs.Show(this, text, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        static string Tail(string t)
        {
            t = (t ?? "").Trim();
            return t.Length > 300 ? t.Substring(t.Length - 300) : t;
        }

        void CancelExport()
        {
            ExportAbort ab = exportAbort;
            if (ab == null) return;
            ab.Set = true;
            exportAbort = null;
            System.Threading.Thread t = exportThread;
            exportThread = null;
            exportText = null;
            try { if (t != null) t.Join(3000); } catch (Exception) { }
            // The partial file goes; the thread's own answer is ignored (it is no longer the current export).
            string partial = exportOut;
            exportOut = null;
            try { if (partial != null && File.Exists(partial)) File.Delete(partial); } catch (Exception) { }
            ShotStack.Unreserve(partial);
            Invalidate();
        }

        // ---- keys ----

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0;
            if (ctrl)
            {
                if (k == Keys.O) { OpenDialog(); return true; }
                if (k == Keys.C) { GrabFrame(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if ((keyData & Keys.Alt) != 0) return base.ProcessCmdKey(ref msg, keyData);
            switch (k)
            {
                case Keys.Space:
                case Keys.K: Toggle(); return true;
                case Keys.Left: Skip(-5); return true;
                case Keys.Right: Skip(5); return true;
                case Keys.J: Skip(-10); return true;
                case Keys.L: Skip(10); return true;
                case Keys.Oemcomma: Step(-1); return true;
                case Keys.OemPeriod: Step(1); return true;
                case Keys.Up: SetVolume(volume + 0.05, true); return true;
                case Keys.Down: SetVolume(volume - 0.05, true); return true;
                case Keys.M: ToggleMute(); return true;
                case Keys.F:
                case Keys.F11: ToggleFullscreen(); return true;
                case Keys.I: ToggleInfo(); return true;
                case Keys.C: GrabFrame(); return true;
                case Keys.E: MarkIn(); return true;
                case Keys.S: MarkOut(); return true;
                case Keys.G: ExportGif(); return true;
                case Keys.Home: SeekTo(0); return true;
                case Keys.End:
                    if (info != null && info.CanPlay)
                    {
                        if (Playing) play.Pause();
                        SeekTo(PlayerKit.LastFrame(Duration, info.Fps) / info.Fps);
                    }
                    return true;
                case Keys.Escape:
                    if (exportAbort != null) { CancelExport(); return true; }
                    if (fullscreen) { ToggleFullscreen(); return true; }
                    if (markIn >= 0 || markOut >= 0) { ClearRange(); return true; }
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---- mouse ----

        Rectangle BarArea()
        {
            Rectangle r = bar;
            r.Inflate(P(10), P(44));
            return r;
        }

        bool BarShown { get { return alpha > 0.25f; } }

        string HitBar(Point p)
        {
            if (!BarShown || !bar.Contains(p)) return null;
            Rectangle sk = seek;
            sk.Inflate(P(4), P(6));
            if (sk.Contains(p)) return "seek";
            if (!vol.IsEmpty)
            {
                Rectangle vr = vol;
                vr.Inflate(P(4), P(4));
                if (vr.Contains(p)) return "vol";
            }
            foreach (Btn b in btns) if (b.R.Contains(p)) return b.Id;
            return "bar";
        }

        Btn Find(string id)
        {
            foreach (Btn b in btns) if (b.Id == id) return b;
            return null;
        }

        double SeekFromX(int x)
        {
            if (seek.Width <= 0) return 0;
            double f = Math.Max(0, Math.Min(1, (x - seek.X) / (double)seek.Width));
            return f * Duration;
        }

        void VolumeFromX(int x)
        {
            if (vol.Width <= 0) return;
            SetVolume(Math.Max(0, Math.Min(1, (x - vol.X) / (double)vol.Width)), false);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            moved.Restart();
            Focus();
            if (e.Button == MouseButtons.Right) { ShowMenu(e.Location, null); return; }
            if (e.Button != MouseButtons.Left) return;
            string hit = HitBar(e.Location);
            if (hit == null)
            {
                // A double click is fullscreen; the first click of it already toggled playback, which is put back.
                if (e.Clicks >= 2) { ToggleFullscreen(); Toggle(); }
                else Toggle();
                return;
            }
            if (hit == "seek" && Duration > 0 && play != null)
            {
                drag = Drag.Seek;
                wasPlaying = Playing;
                if (wasPlaying) play.Pause();
                scrub = SeekFromX(e.X);
                Scrub();
                Capture = true;
            }
            else if (hit == "vol") { drag = Drag.Volume; VolumeFromX(e.X); Capture = true; }
            else pressed = hit;
        }

        // Moves while dragging the bar: the position follows at once, the picture is asked for at most every 60 ms.
        void Scrub()
        {
            if (info == null) return;
            double t = Math.Min(scrub, Math.Max(0, Duration - 0.5 / info.Fps));
            play.Seek(t);
            if (stillGap.ElapsedMilliseconds >= 60) { stillGap.Restart(); play.Still(t); }
            Invalidate(BarArea());
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Location != hoverAt) moved.Restart();
            hoverAt = e.Location;
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
            if (drag == Drag.Seek) { scrub = SeekFromX(e.X); Scrub(); return; }
            if (drag == Drag.Volume) { VolumeFromX(e.X); return; }
            string h = HitBar(e.Location);
            if (h != hot)
            {
                hot = h;
                Cursor = h != null && h != "bar" ? Cursors.Hand : Cursors.Default;
                Btn b = h != null ? Find(h) : null;
                if (b != null && b.Tip != null) EdTip.Schedule(this, b.R, b.Tip, true);
                else EdTip.Cancel();
                Invalidate(BarArea());
            }
            else if (h == "seek") Invalidate(BarArea());
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (drag == Drag.Seek)
            {
                drag = Drag.None;
                Capture = false;
                SeekTo(scrub);
                if (wasPlaying && play != null) play.Resume();
                wasPlaying = false;
                Wake();
                return;
            }
            if (drag == Drag.Volume) { drag = Drag.None; Capture = false; return; }
            string p = pressed;
            pressed = null;
            if (p != null && e.Button == MouseButtons.Left && HitBar(e.Location) == p) Act(p);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hot = null;
            hoverAt = new Point(-1, -1);
            EdTip.Cancel();
            Invalidate(BarArea());
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (info != null && info.Audio.Count > 0) SetVolume(volume + 0.05 * Math.Sign(e.Delta), true);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            string f = files[0];
            if (Directory.Exists(f) || !File.Exists(f)) { Flash(I18n.T("No se puede abrir eso.", "Cannot open that.")); return; }
            // An extension FFmpeg is not known to read is probed first, so a stray file does not replace what is playing.
            if (ShotStack.IsVideoFile(f)) { OpenAndPlay(f); return; }
            if (probing) return;
            probing = true;
            UseWaitCursor = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool playable = MediaInfo.ProbeCached(ffmpeg, f).CanPlay;
                OnUi(delegate
                {
                    probing = false;
                    UseWaitCursor = false;
                    if (!playable) { Flash(I18n.T("Ese archivo no es un v\u00EDdeo que se pueda abrir.", "That file is not a playable video.")); return; }
                    OpenAndPlay(f);
                });
            });
        }

        void Act(string id)
        {
            switch (id)
            {
                case "play": Toggle(); break;
                case "mute": ToggleMute(); break;
                case "full": ToggleFullscreen(); break;
                case "tracks": ShowTracks(); break;
                case "speed": ShowSpeeds(); break;
                case "loop": ToggleLoop(); break;
                case "menu": ShowMenu(Point.Empty, Find("menu")); break;
                case "info": ToggleInfo(); break;
                case "folder": Reveal(); break;
                case "edit": Edit(); break;
                case "export": ExportMp4(); break;
                case "gif": ExportGif(); break;
            }
        }

        // ---- menus ----

        void OpenMenu(ContextMenuStrip m, Point at)
        {
            menuOpen = true;
            m.Closed += delegate { menuOpen = false; OnUi(delegate { m.Dispose(); Wake(); }); };
            m.Show(this, at, ToolStripDropDownDirection.AboveRight);
        }

        void ShowTracks()
        {
            Btn b = Find("tracks");
            if (b == null || info == null) return;
            ContextMenuStrip m = TrayMenu.Create();
            foreach (AudioTrackInfo a in info.Audio)
            {
                int idx = a.Index;
                ToolStripMenuItem it = TrayMenu.Item(string.IsNullOrEmpty(a.Title) ? I18n.Fmt("Pista {0}", "Track {0}", idx + 1) : a.Title, null, delegate { if (play != null) play.Track = idx; });
                it.Checked = play != null && play.Track == idx;
                m.Items.Add(it);
            }
            OpenMenu(m, new Point(b.R.X, b.R.Y - P(6)));
        }

        void ShowSpeeds()
        {
            Btn b = Find("speed");
            if (b == null) return;
            ContextMenuStrip m = TrayMenu.Create();
            foreach (double v in PlayerKit.Speeds)
            {
                double sp = v;
                ToolStripMenuItem it = TrayMenu.Item(PlayerKit.SpeedText(sp) + (sp == 1 ? "  (normal)" : ""), null, delegate { SetSpeed(sp); });
                it.Checked = Math.Abs(speed - sp) < 1e-9;
                m.Items.Add(it);
            }
            OpenMenu(m, new Point(b.R.X, b.R.Y - P(6)));
        }

        // Everything the bar has no room for, and the settings of the player.
        void ShowMenu(Point at, Btn b)
        {
            if (info == null) return;
            ContextMenuStrip m = TrayMenu.Create();
            if (CanEdit) m.Items.Add(TrayMenu.Item(I18n.T("Editar", "Edit"), "edit", delegate { Edit(); }));
            if (CanExport) m.Items.Add(TrayMenu.Item(HasRange ? I18n.T("Exportar el tramo", "Export clip") : I18n.T("Exportar MP4", "Export MP4"), "share", delegate { ExportMp4(); }));
            if (info.CanPlay && exportThread == null) m.Items.Add(GifMenuItem());
            m.Items.Add(TrayMenu.Item((I18n.IsSpanish ? "Calidad al exportar: " : "Export quality: ") + QualityName(settings.ExportQuality) + "\u2026", null, delegate { OnUi(delegate { ShowQuality(at, b); }); }));
            m.Items.Add(TrayMenu.Item(I18n.T("Capturar este fotograma", "Capture this frame"), "photo", "C", delegate { GrabFrame(); }));
            m.Items.Add(TrayMenu.Item(I18n.T("Marcar el inicio del tramo", "Set in-point"), null, "E", delegate { MarkIn(); }));
            m.Items.Add(TrayMenu.Item(I18n.T("Marcar el final del tramo", "Set out-point"), null, "S", delegate { MarkOut(); }));
            if (markIn >= 0 || markOut >= 0) m.Items.Add(TrayMenu.Item(I18n.T("Quitar el tramo", "Clear range"), null, "Esc", delegate { ClearRange(); }));
            m.Items.Add(TrayMenu.Item(I18n.T("Mostrar en carpeta", "Show in folder"), "folder", delegate { Reveal(); }));
            m.Items.Add(TrayMenu.Item(I18n.T("Abrir otro v\u00EDdeo\u2026", "Open another video\u2026"), "video", "Ctrl+O", delegate { OpenDialog(); }));
            m.Items.Add(TrayMenu.Separator());
            ToolStripMenuItem inf = TrayMenu.Item(I18n.T("Informaci\u00F3n", "Information"), "info", "I", delegate { ToggleInfo(); });
            inf.Checked = infoOn;
            m.Items.Add(inf);
            ToolStripMenuItem lp = TrayMenu.Item(I18n.T("Repetir al terminar", "Loop playback"), "update", delegate { ToggleLoop(); });
            lp.Checked = loop;
            m.Items.Add(lp);
            m.Items.Add(TrayMenu.Separator());
            ToolStripMenuItem best = TrayMenu.Item(I18n.T("Escalado: mejor calidad", "Scaling: best quality"), null, delegate { SetScaling(0); });
            best.Checked = settings.PlayerScaling == 0;
            m.Items.Add(best);
            ToolStripMenuItem fast = TrayMenu.Item(I18n.T("Escalado: m\u00E1s r\u00E1pido", "Scaling: faster"), null, delegate { SetScaling(1); });
            fast.Checked = settings.PlayerScaling != 0;
            m.Items.Add(fast);
            ToolStripMenuItem real = TrayMenu.Item(I18n.T("Imagen: tama\u00F1o real (la m\u00E1s n\u00EDtida)", "Image: 100% scale (sharpest)"), null, delegate { SetFill(false); });
            real.Checked = !settings.PlayerFill;
            m.Items.Add(real);
            ToolStripMenuItem fill = TrayMenu.Item(I18n.T("Imagen: llenar la ventana", "Image: fit window"), null, delegate { SetFill(true); });
            fill.Checked = settings.PlayerFill;
            m.Items.Add(fill);
            ToolStripMenuItem opens = TrayMenu.Item(I18n.T("Abrir los v\u00EDdeos en Stackshot", "Open videos in Stackshot"), null, delegate
            {
                settings.PlayerOpensVideos = !settings.PlayerOpensVideos;
                Remember();
                Flash(settings.PlayerOpensVideos ? I18n.T("Los v\u00EDdeos se abrir\u00E1n en Stackshot", "Videos will open in Stackshot") : I18n.T("Los v\u00EDdeos se abrir\u00E1n en el reproductor de Windows", "Videos will open in Windows player"));
            });
            opens.Checked = settings.PlayerOpensVideos;
            m.Items.Add(opens);
            if (b != null) OpenMenu(m, new Point(b.R.X, b.R.Y - P(6)));
            else { menuOpen = true; m.Closed += delegate { menuOpen = false; OnUi(delegate { m.Dispose(); Wake(); }); }; m.Show(this, at); }
        }

        static string QualityName(int q)
        {
            q = Math.Max(0, Math.Min(Timeline.Qualities.Length - 1, q));
            return Timeline.Qualities[q] + (q == 3 ? " (WhatsApp)" : q == 4 ? (I18n.IsSpanish ? " (correo)" : " (email)") : "");
        }

        // The export quality, shared with the editor; 16 MB and 25 MB fit the file in that size.
        void ShowQuality(Point at, Btn b)
        {
            ContextMenuStrip m = TrayMenu.Create();
            for (int i = 0; i < Timeline.Qualities.Length; i++)
            {
                int q = i;
                ToolStripMenuItem it = TrayMenu.Item(QualityName(q), null, delegate { settings.ExportQuality = q; Remember(); Flash((I18n.IsSpanish ? "Calidad al exportar: " : "Export quality: ") + QualityName(q)); });
                it.Checked = settings.ExportQuality == q;
                m.Items.Add(it);
            }
            if (b != null) OpenMenu(m, new Point(b.R.X, b.R.Y - P(6)));
            else { menuOpen = true; m.Closed += delegate { menuOpen = false; OnUi(delegate { m.Dispose(); Wake(); }); }; m.Show(this, at); }
        }

        // ---- time ----

        void OnTick(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            bool playing = Playing;
            bool want = WantBar();
            float target = want ? 1f : 0f;
            bool fading = false;
            if (Math.Abs(alpha - target) > 0.001f)
            {
                alpha += (target - alpha) * 0.4f;
                if (Math.Abs(alpha - target) < 0.04f) alpha = target;
                fading = true;
            }
            bool hide = playing && !want && Bounds.Contains(Control.MousePosition) && Form.ActiveForm == this;
            if (hide && !cursorHidden) { Cursor.Hide(); cursorHidden = true; }
            else if (!hide && cursorHidden) { Cursor.Show(); cursorHidden = false; }
            if (flash != null && Environment.TickCount - flashUntil > 0) { flash = null; Invalidate(); }
            if ((playing || fading) && alpha > 0.02f) Invalidate(BarArea());
            else if (fading) Invalidate(BarArea());
        }

        bool WantBar()
        {
            if (play == null || message != null || exportAbort != null) return true;
            if (!Playing || drag != Drag.None || menuOpen) return true;
            if (moved.ElapsedMilliseconds < HideMs) return true;
            return BarShown && bar.Contains(PointToClient(Control.MousePosition));
        }

        // ---- painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            Rectangle vr = VideoRect();
            lock (frameLock)
            {
                if (frame != null && haveFrame && !vr.IsEmpty)
                {
                    g.InterpolationMode = frame.Width == vr.Width && frame.Height == vr.Height ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(frame, vr);
                    g.CompositingMode = CompositingMode.SourceOver;
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Default;
            LayoutBar(g);
            if (message != null) PaintCard(g, message, ClientSize.Height / 2, true);
            string top = exportText ?? flash;
            if (top != null) PaintCard(g, top, P(34), false);
            if (infoOn && info != null && info.CanPlay) PaintInfo(g);
            if (info != null && info.CanPlay) PaintBar(g);
        }

        Font Face(bool semi, float px) { return Fonts.Get(semi ? EdLook.Semibold : EdLook.Text, px * s); }

        static Color Hud(int a, int v) { return ChromeWhite(a, v, 1); }

        // A rounded dark label centred at y, with text in white.
        void PaintCard(Graphics g, string text, int y, bool wrap)
        {
            Font f = Face(false, 13);
            int maxW = Math.Min(ClientSize.Width - P(40), P(560));
            Size sz = TextKit.Measure(g, text, f, new Size(maxW - P(32), 0), wrap ? TextFormatFlags.WordBreak | TextFormatFlags.NoPadding : TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int w = Math.Min(maxW, sz.Width + P(32)), h = sz.Height + P(20);
            Rectangle r = new Rectangle((ClientSize.Width - w) / 2, y - (wrap ? h / 2 : 0), w, h);
            EdLook.Fill(g, r, P(10), ChromeBack(214, 30, 30, 32, 1));
            EdLook.Hairline(g, r, P(10), Hud(31, 255));
            TextFormatFlags fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | (wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
            TextKit.Draw(g, text, f, Rectangle.Inflate(r, -P(16), -P(8)), Hud(255, 255), fl);
        }

        void PaintInfo(Graphics g)
        {
            Font lf = Face(false, 12), vf = Face(false, 12), tf = Face(true, 13);
            int w = Math.Min(ClientSize.Width - P(32), P(520)), rowH = P(21), pad = P(16);
            List<string[]> rows = new List<string[]>(infoRows);
            // Right under the video's size: how big it is shown now.
            string scale = PlayerKit.ScaleNote(info.Display, VideoRect(), !fullscreen && WindowState != FormWindowState.Maximized);
            // The rows were made when the video opened, in the language of that moment.
            int at = rows.FindIndex(x => x[0] == "Tama\u00F1o" || x[0] == "Size");
            if (scale.Length > 0) rows.Insert(at >= 0 ? at + 1 : rows.Count, new string[] { I18n.T("En pantalla", "On screen"), scale });
            if (play != null) rows.Add(new string[] { I18n.T("Reproducci\u00F3n", "Playback"), I18n.Fmt("{0} fotogramas mostrados, {1} omitidos", "{0} frames shown, {1} dropped", play.FramesShown, play.FramesDropped) });
            int h = pad * 2 + P(26) + rows.Count * rowH;
            int floor = (bar.IsEmpty ? ClientSize.Height : bar.Y) - P(12);
            Rectangle r = new Rectangle(P(16), P(16), w, Math.Max(P(60), Math.Min(h, floor - P(16))));
            EdLook.Fill(g, r, P(12), ChromeBack(222, 28, 28, 30, 1));
            EdLook.Hairline(g, r, P(12), Hud(31, 255));
            TextKit.Draw(g, I18n.T("Informaci\u00F3n", "Information"), tf, new Rectangle(r.X + pad, r.Y + pad - P(2), r.Width - pad * 2, P(22)), Hud(255, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int y = r.Y + pad + P(26), lw = P(150);
            foreach (string[] row in rows)
            {
                if (y + rowH > r.Bottom) break;
                TextKit.Draw(g, row[0], lf, new Rectangle(r.X + pad, y, lw, rowH), Hud(150, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextKit.Draw(g, row[1], vf, new Rectangle(r.X + pad + lw, y, r.Width - pad * 2 - lw, rowH), Hud(255, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                y += rowH;
            }
        }

        // The bar's rectangles and buttons for this size (the same for what is drawn and what is clicked).
        void LayoutBar(Graphics g)
        {
            btns.Clear();
            if (info == null || !info.CanPlay) { bar = Rectangle.Empty; seek = vol = Rectangle.Empty; return; }
            int W = ClientSize.Width, H = ClientSize.Height;
            int bw = Math.Min(W - P(24), P(920));
            if (bw < P(300)) bw = Math.Max(P(100), W - P(8));
            int bh = P(80);
            bar = new Rectangle((W - bw) / 2, H - bh - P(14), bw, bh);
            int pad = P(16), timeW = P(62);
            seek = new Rectangle(bar.X + pad + timeW + P(8), bar.Y + P(11), Math.Max(P(20), bar.Width - 2 * pad - 2 * timeW - P(16)), P(18));
            int y2 = bar.Y + P(40), h2 = P(32), x = bar.X + pad;
            AddBtn("play", Playing ? "pause" : "play!", null, Playing ? I18n.T("Pausa  (Espacio)", "Pause  (Space)") : I18n.T("Reproducir  (Espacio)", "Play  (Space)"), new Rectangle(x, y2, P(36), h2), false, false);
            x += P(40);
            vol = Rectangle.Empty;
            if (info.Audio.Count > 0)
            {
                AddBtn("mute", muted || volume == 0 ? "soundoff" : "sound", null, muted ? I18n.T("Activar el sonido  (M)", "Unmute  (M)") : I18n.T("Silenciar  (M)", "Mute  (M)"), new Rectangle(x, y2, P(34), h2), false, false);
                x += P(36);
                vol = new Rectangle(x, y2 + P(4), P(84), h2 - P(8));
                x += P(84) + P(10);
            }
            int left = x + P(8), rx = bar.Right - pad, gap = P(4);
            Font pf = Face(true, 12);
            List<Btn> right = new List<Btn>();
            right.Add(Make("full", fullscreen ? "restore" : "maximize", null, I18n.T("Pantalla completa  (F)", "Full screen  (F)"), P(34), false, false));
            if (info.Audio.Count > 1) right.Add(Make("tracks", null, "Audio", I18n.T("Pista de audio", "Audio track"), Math.Max(P(52), TextKit.Measure(g, "Audio", pf, Size.Empty, TextFormatFlags.NoPadding).Width + P(24)), false, true));
            right.Add(Make("speed", null, PlayerKit.SpeedText(speed), I18n.T("Velocidad", "Speed"), Math.Max(P(48), TextKit.Measure(g, PlayerKit.SpeedText(speed), pf, Size.Empty, TextFormatFlags.NoPadding).Width + P(20)), speed != 1, true));
            right.Add(Make("loop", "update", null, I18n.T("Repetir al terminar", "Loop playback"), P(34), loop, false));
            right.Add(Make("menu", "gear", null, I18n.T("M\u00E1s opciones", "More options"), P(34), false, false));
            right.Add(Make("info", "info", null, I18n.T("Informaci\u00F3n  (I)", "Information  (I)"), P(34), infoOn, false));
            right.Add(Make("folder", "folder", null, I18n.T("Mostrar en carpeta", "Show in folder"), P(34), false, false));
            if (CanEdit) right.Add(Make("edit", null, I18n.T("Editar", "Edit"), I18n.T("Abrir en el editor", "Open in editor"), TextKit.Measure(g, I18n.T("Editar", "Edit"), pf, Size.Empty, TextFormatFlags.NoPadding).Width + P(26), false, true));
            string exportLabel = HasRange ? I18n.T("Exportar tramo", "Export clip") : I18n.T("Exportar MP4", "Export MP4");
            if (CanExport) right.Add(Make("export", null, exportLabel, (HasRange ? I18n.T("Crear un MP4 con el tramo marcado (E y S)", "Create MP4 with marked range (E and S)") : I18n.T("Crear un MP4 para compartir", "Create shareable MP4")) + " \u00B7 " + I18n.T("calidad ", "quality ") + QualityName(settings.ExportQuality), TextKit.Measure(g, exportLabel, pf, Size.Empty, TextFormatFlags.NoPadding).Width + P(26), false, true));
            if (CanGif) right.Add(Make("gif", null, "GIF", GifTip(), TextKit.Measure(g, "GIF", pf, Size.Empty, TextFormatFlags.NoPadding).Width + P(26), false, true));
            foreach (Btn b in right)
            {
                int w = b.R.Width;
                if (rx - w < left) break;
                rx -= w;
                b.R = new Rectangle(rx, y2, w, h2);
                btns.Add(b);
                rx -= gap;
            }
        }

        static Btn Make(string id, string glyph, string text, string tip, int w, bool on, bool pill)
        {
            Btn b = new Btn();
            b.Id = id; b.Glyph = glyph; b.Text = text; b.Tip = tip; b.On = on; b.Pill = pill;
            b.R = new Rectangle(0, 0, w, 0);
            return b;
        }

        void AddBtn(string id, string glyph, string text, string tip, Rectangle r, bool on, bool pill)
        {
            Btn b = Make(id, glyph, text, tip, r.Width, on, pill);
            b.R = r;
            btns.Add(b);
        }

        void PaintBar(Graphics g)
        {
            if (alpha <= 0.02f) return;
            float a = (float)Math.Round(alpha * 8) / 8f;
            if (a <= 0) return;
            Func<int, int, Color> W = delegate(int al, int v) { return ChromeWhite(al, v, a); };
            EdLook.Fill(g, bar, P(14), ChromeBack(214, 30, 30, 32, a));
            EdLook.Hairline(g, bar, P(14), W(31, 255));
            int pad = P(16), timeW = P(62);
            Font tf = Face(false, 12);
            double pos = Pos, dur = Duration;
            TextKit.Draw(g, PlayerKit.Clock(pos), tf, new Rectangle(bar.X + pad, seek.Y, timeW, seek.Height), W(235, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            TextKit.Draw(g, dur > 0 ? PlayerKit.Clock(dur) : "", tf, new Rectangle(bar.Right - pad - timeW, seek.Y, timeW, seek.Height), W(150, 255), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            Color accent = EdLook.C(Ds.Brushes.Accent);
            Color acc = Color.FromArgb((int)Math.Round(255 * a), accent);
            // Seek track.
            bool hotSeek = hot == "seek" || drag == Drag.Seek;
            int th = hotSeek ? P(6) : P(4), cy = seek.Y + seek.Height / 2;
            Rectangle track = new Rectangle(seek.X, cy - th / 2, seek.Width, th);
            EdLook.Fill(g, track, th / 2f, W(64, 255));
            // The marked piece: a lighter band under the progress, with a tick at each end.
            if (dur > 0 && (markIn >= 0 || markOut >= 0))
            {
                double ma = Math.Max(0, markIn), mb2 = markOut > 0 ? markOut : dur;
                int ax = seek.X + (int)Math.Round(Math.Min(1, ma / dur) * seek.Width), bx = seek.X + (int)Math.Round(Math.Min(1, mb2 / dur) * seek.Width);
                if (bx > ax) EdLook.Fill(g, new Rectangle(ax, cy - th / 2 - P(2), bx - ax, th + P(4)), P(2), ChromeMark(140));
                using (SolidBrush mb = new SolidBrush(ChromeMark(255)))
                {
                    if (markIn >= 0) g.FillRectangle(mb, ax - P(1), cy - P(7), P(2), P(14));
                    if (markOut > 0) g.FillRectangle(mb, bx - P(1), cy - P(7), P(2), P(14));
                }
            }
            double frac = dur > 0 ? Math.Max(0, Math.Min(1, pos / dur)) : 0;
            int px = seek.X + (int)Math.Round(frac * seek.Width);
            if (px > seek.X) EdLook.Fill(g, new Rectangle(seek.X, cy - th / 2, px - seek.X, th), th / 2f, acc);
            if (hotSeek) using (SolidBrush kb = new SolidBrush(W(255, 255))) g.FillEllipse(kb, px - P(6), cy - P(6), P(12), P(12));
            if (hot == "seek" && drag == Drag.None && dur > 0 && hoverAt.X >= 0)
            {
                string tip = PlayerKit.Clock(SeekFromX(hoverAt.X));
                Size ts = TextKit.Measure(g, tip, tf, Size.Empty, TextFormatFlags.NoPadding);
                int tw = ts.Width + P(16);
                Rectangle tr = new Rectangle(Math.Max(bar.X, Math.Min(bar.Right - tw, hoverAt.X - tw / 2)), bar.Y - P(32), tw, P(24));
                EdLook.Fill(g, tr, P(7), ChromeBack(224, 30, 30, 32, a));
                if (HighChrome) EdLook.Hairline(g, tr, P(7), W(255, 255));
                TextKit.Draw(g, tip, tf, tr, W(255, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
            // Volume slider.
            if (!vol.IsEmpty)
            {
                bool hv = hot == "vol" || drag == Drag.Volume;
                int vh = hv ? P(6) : P(4), vy = vol.Y + vol.Height / 2;
                EdLook.Fill(g, new Rectangle(vol.X, vy - vh / 2, vol.Width, vh), vh / 2f, W(64, 255));
                double vf = muted ? 0 : volume;
                int vx = vol.X + (int)Math.Round(vf * vol.Width);
                if (vx > vol.X) EdLook.Fill(g, new Rectangle(vol.X, vy - vh / 2, vx - vol.X, vh), vh / 2f, W(235, 255));
                if (hv) using (SolidBrush kb = new SolidBrush(W(255, 255))) g.FillEllipse(kb, vx - P(5), vy - P(5), P(10), P(10));
            }
            foreach (Btn b in btns)
            {
                bool over = hot == b.Id;
                if (over || b.Pill) ChromeButton(g, b.R, over, W);
                Color wi = W(235, 255);
                M.Color ink = b.On ? ChromeOn(a) : M.Color.FromArgb(wi.A, wi.R, wi.G, wi.B);
                if (b.Glyph != null) EdLook.Icon(g, b.Glyph, b.R, P(17), ink, Math.Max(1.5f, 1.9f * s));
                else TextKit.Draw(g, b.Text, Face(true, 12), b.R, Color.FromArgb(ink.A, ink.R, ink.G, ink.B), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        // ---- frame ----

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);  // DWMWA_WINDOW_CORNER_PREFERENCE: round
            }
            catch (Exception) { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Focus();
            BeginPlay();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (exportAbort != null) CancelExport();
            RememberPosition();
            if (saveTimer.Enabled) { saveTimer.Stop(); settings.Save(); }
            if (fullscreen) { fullscreen = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Stop();
                resizeTimer.Stop();
                saveTimer.Stop();
                ExportAbort ab = exportAbort;
                if (ab != null) ab.Set = true;
                if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
                EdTip.Cancel();
                DisposePlayback();
                lock (frameLock)
                {
                    closed = true;
                    if (frame != null) { frame.Dispose(); frame = null; }
                }
                tick.Dispose();
                resizeTimer.Dispose();
                saveTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
