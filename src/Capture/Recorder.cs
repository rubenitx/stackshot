// Stackshot - MP4/GIF screen recording with sound, through FFmpeg.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using WI = System.Windows.Media.Imaging;
using WM = System.Windows.Media;

namespace Stackshot
{
    // Stackshot grabs the frames itself (Desktop Duplication, physical pixels, any DPI, with cursor) and the sound
    // (WASAPI), both on the same clock, and pipes raw frames to FFmpeg, which only encodes. Stackshot windows are excluded
    // from capture.
    public static class Recorder
    {
        // Quality values as stored in settings.ini (the menu shows them in another order).
        public const int Standard = 0, Maximum = 1, High = 2, Cinema = 3;

        // Screen content is sRGB: BT.709 video tagged with the sRGB transfer, so players show the original colors.
        public const string Tags = "setparams=color_primaries=bt709:color_trc=iec61966-2-1:colorspace=bt709:range=tv";

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
            Begin(owner, s, ffmpeg, r, gif);
        }

        // Records an area already chosen (the all-in-one picker).
        public static void Start(ShotStack owner, Settings s, Rectangle r, bool gif)
        {
            if (current != null) { current.Busy(); return; } // one at a time: the bar shakes to say so
            string ffmpeg = FindFfmpeg(s);
            if (ffmpeg == null) ffmpeg = FfmpegSetup.Run(s);
            if (ffmpeg == null) return;
            Begin(owner, s, ffmpeg, r, gif);
        }

        static void Begin(ShotStack owner, Settings s, string ffmpeg, Rectangle r, bool gif)
        {
            // H.264 needs even dimensions.
            r.Width = Math.Max(16, r.Width & ~1);
            r.Height = Math.Max(16, r.Height & ~1);
            Session session = new Session(owner, s, ffmpeg, r, gif);
            current = session;
            try { session.Start(); }
            catch (Exception ex)
            {
                // A start that failed half way must not leave Stackshot believing it records (every later press would
                // only "stop" it).
                ShotStack.Log("Grabaci\u00F3n: " + ex);
                if (!session.Abandon()) return;
                if (current == session) current = null;
                MessageBox.Show("No se pudo empezar a grabar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

        // H.264 settings for finished files, by quality: the slower presets only where they pay off.
        public static string FinalCodec(int quality, long pixels, double fps)
        {
            string gop = " -g " + Math.Max(2, (int)Math.Round(fps * 2));
            if (quality == Cinema) return "-c:v libx264 -preset " + (pixels > 2400000 ? "medium" : "slow") + " -crf 10 -profile:v high" + gop;
            if (quality == Maximum) return "-c:v libx264 -preset " + (pixels > 2400000 ? "medium" : "slow") + " -crf 14 -profile:v high" + gop;
            if (quality == High) return "-c:v libx264 -preset medium -crf 16 -profile:v high" + gop;
            return "-c:v libx264 -preset medium -crf 20 -profile:v high" + gop;
        }

        public static string AudioCodec(int quality)
        {
            return "-c:a aac -b:a " + (quality == Cinema ? "320k" : quality == Maximum ? "256k" : quality == High ? "192k" : "160k") + " -ar 48000";
        }

        static string hardware;
        static bool probed;

        // The graphics card's H.264 encoder (NVIDIA, Intel or AMD), probed once, for areas too large to encode on the
        // CPU in real time. level: 0 near lossless, 1 high, 2 standard. Null if there is none.
        public static string HardwareEncoder(string ffmpeg, int level)
        {
            if (!probed)
            {
                probed = true;
                foreach (string c in new string[] { "h264_nvenc", "h264_qsv", "h264_amf" })
                {
                    string err;
                    if (Run(ffmpeg, "-hide_banner -loglevel error -f lavfi -i color=black:s=640x360:r=30 -frames:v 10 -pix_fmt nv12 -c:v " + c + " -f null -", 15000, out err) == 0)
                    {
                        hardware = c;
                        break;
                    }
                }
                ShotStack.Log("Codificador por hardware: " + (hardware ?? "ninguno"));
            }
            if (hardware == null) return null;
            int q = level == 0 ? 12 : level == 1 ? 18 : 24;
            if (hardware == "h264_nvenc") return "-c:v h264_nvenc -preset p5 -rc constqp -qp " + q;
            if (hardware == "h264_qsv") return "-c:v h264_qsv -preset medium -global_quality " + q + " -look_ahead 0";
            return "-c:v h264_amf -quality quality -rc cqp -qp_i " + q + " -qp_p " + q;
        }

        // Runs FFmpeg to the end (or the timeout); stderr is read asynchronously so a stuck process can't block.
        public static int Run(string ffmpeg, string args, int timeoutMs, out string stderr)
        {
            StringBuilder sb = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = ffmpeg;
                p.StartInfo.Arguments = "-nostdin " + args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sb) { if (sb.Length < 32000) sb.AppendLine(e.Data); }
                };
                p.Start();
                p.BeginErrorReadLine();
                bool exited = p.WaitForExit(timeoutMs);
                if (!exited) { try { p.Kill(); } catch { } }
                p.WaitForExit(2000);
                lock (sb) stderr = sb.ToString();
                return exited ? p.ExitCode : -1;
            }
        }
    }

    // Frame buffers handed from the capture thread to the writer; allocated on demand up to a limit.
    sealed class FramePool : IDisposable
    {
        readonly int bytes, max;
        readonly Stack<IntPtr> free = new Stack<IntPtr>();
        readonly List<IntPtr> all = new List<IntPtr>();

        public FramePool(int bytes, int max)
        {
            this.bytes = bytes;
            this.max = Math.Max(3, max);
        }

        // Blocks while every buffer is in use; IntPtr.Zero if abort() says so meanwhile.
        public IntPtr Take(Func<bool> abort)
        {
            lock (free)
            {
                while (free.Count == 0 && all.Count >= max)
                {
                    if (abort()) return IntPtr.Zero;
                    Monitor.Wait(free, 50);
                }
                if (free.Count > 0) return free.Pop();
                IntPtr p = Marshal.AllocHGlobal(bytes);
                all.Add(p);
                return p;
            }
        }

        public void Give(IntPtr p)
        {
            lock (free) { free.Push(p); Monitor.Pulse(free); }
        }

        public void Dispose()
        {
            lock (free)
            {
                foreach (IntPtr p in all) Marshal.FreeHGlobal(p);
                all.Clear();
                free.Clear();
            }
        }
    }

    class Session
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeFileHandle h, IntPtr buffer, int count, out int written, IntPtr overlapped);

        readonly ShotStack owner;
        readonly string ffmpeg;
        readonly Rectangle area;
        readonly Settings settings;
        readonly bool gif;
        readonly int quality, gifFps, cameraShape;
        readonly Stopwatch clock = new Stopwatch();
        readonly StringBuilder errors = new StringBuilder();
        readonly List<string> notes = new List<string>();
        int fpsNum = 30, fpsDen = 1;
        string output, video, sysPath, micPath, enc;
        bool lossless, direct;
        ScreenSource source;
        AudioCapture sys, mic;
        Process proc;
        Stream pipeStream;
        SafeFileHandle pipe;
        Thread capture, writer;
        FramePool pool;
        BlockingCollection<IntPtr> queue;
        long t0, t1, frames;
        double maxLate;                   // how far behind the screen frames were made (ms), for the log
        volatile bool stopping, cancelled, writeFailed;
        bool fineTimer;                   // the 1 ms timer resolution is raised for this recording
        string error;
        RecordBar bar;
        WebcamBubble webcam;
        FrameEdge[] edges;

        public Session(ShotStack owner, Settings s, string ffmpeg, Rectangle area, bool gif)
        {
            this.owner = owner;
            this.ffmpeg = ffmpeg;
            this.area = area;
            this.gif = gif;
            quality = gif ? Recorder.Standard : s.VideoQuality;
            gifFps = s.GifFps;
            cameraShape = gif ? 0 : s.Webcam;
            settings = s;
        }

        public TimeSpan Elapsed { get { return clock.Elapsed; } }
        public bool Gif { get { return gif; } }
        public AudioCapture SystemAudio { get { return sys; } }
        public AudioCapture Microphone { get { return mic; } }

        public void Start()
        {
            Directory.CreateDirectory(Settings.TempDir);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
            output = ShotStack.Unique(Path.Combine(Settings.TempDir, (gif ? "GIF " : "V\u00EDdeo ") + stamp + (gif ? ".gif" : ".mp4")));
            // Work files start with "~" and never show up as captures.
            string temp = Path.Combine(Settings.TempDir, "~grabando " + stamp);
            int target = gif ? Math.Max(10, gifFps * 2) : quality == Recorder.Standard ? settings.VideoFps : 60;
            lossless = gif || quality == Recorder.Maximum || quality == Recorder.Cinema;
            // Up to 1920x1200 at 60 fps the CPU encodes losslessly in real time. Larger areas are converted to 4:2:0 on
            // the GPU (2.7x less data) and encoded by the graphics card when it can.
            bool large = !gif && (long)area.Width * area.Height * target > 1920L * 1200 * 60;
            source = ScreenSource.Open(area, large, cameraShape > 0);
            ChooseRate(target);
            // The camera opens while the rest is prepared; nothing is recorded until it shows live video.
            if (cameraShape > 0)
            {
                webcam = new WebcamBubble(settings, ffmpeg, area);
                webcam.Show();
            }

            if (!gif && settings.RecordSystemAudio) sys = StartAudio(true, settings.SystemAudioDevice, sysPath = temp + " equipo.f32");
            if (!gif && settings.RecordMic) mic = StartAudio(false, settings.MicDevice, micPath = temp + " micro.f32");
            bool audio = sys != null || mic != null;

            string rate = fpsNum + "/" + fpsDen;
            string gop = " -g " + Math.Max(2, (int)Math.Round(2.0 * fpsNum / fpsDen));
            string input = "-f rawvideo -pix_fmt " + (source.Nv12 ? "nv12" : "bgra") + " -video_size " + area.Width + "x" + area.Height +
                           " -framerate " + rate + " -i - ";
            // enc is a field (logged when recording starts)
            if (lossless && !source.Nv12)
            {
                // Lossless RGB: exactly what was on screen; the color conversion waits for the final encode.
                enc = "-c:v libx264rgb -preset ultrafast -qp 0";
                video = temp + ".nut";
            }
            else
            {
                string vf = source.Nv12 ? Recorder.Tags : "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p," + Recorder.Tags;
                int level = lossless ? 0 : quality == Recorder.High ? 1 : 2;
                string hw = large ? Recorder.HardwareEncoder(ffmpeg, level) : null;
                if (hw == null)
                    hw = "-c:v libx264 -preset " + (large ? "ultrafast" : "veryfast") + " -crf " + (level == 0 ? 8 : level == 1 ? 16 : 23);
                enc = "-vf \"" + vf + "\" " + hw + gop;
                direct = !lossless && !audio;
                video = direct ? output : temp + ".nut"; // NUT keeps exact frame times (Matroska rounds them to 1 ms)
                if (direct) enc += " -movflags +faststart";
            }
            try
            {
                proc = StartFfmpeg("-hide_banner -loglevel error -y " + input + enc + " " + Recorder.Quote(video), true);
            }
            catch (Exception ex)
            {
                ShotStack.Log("FFmpeg: " + ex.Message);
                MessageBox.Show("No se pudo arrancar FFmpeg: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (webcam != null) { webcam.Close(); webcam = null; }
                long none = 0;
                if (sys != null) sys.Finish(none);
                if (mic != null) mic.Finish(none);
                TryDelete(sysPath);
                TryDelete(micPath);
                source.Dispose();
                Recorder.Ended(this);
                return;
            }
            pipeStream = proc.StandardInput.BaseStream;
            FileStream fs = pipeStream as FileStream;
            pipe = fs != null ? fs.SafeFileHandle : null;
            pool = new FramePool(source.FrameBytes, (int)Math.Max(4, Math.Min(32, (256L << 20) / source.FrameBytes)));
            queue = new BlockingCollection<IntPtr>();

            edges = FrameEdge.Around(area);
            bar = new RecordBar(this, area);
            bar.Prepare();
            preparing = true;
            prepClock = Stopwatch.StartNew();
            escSeenUp = false;
            prep = new System.Windows.Forms.Timer();
            prep.Interval = 30;
            prep.Tick += delegate { PrepTick(); };
            prep.Start();
        }

        const int CameraWaitMs = 6000;
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        static bool OurForeground()
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }
        bool preparing, escSeenUp;
        Stopwatch prepClock;
        System.Windows.Forms.Timer prep;
        string cameraNote;

        // Waits for every device (today the camera; audio, FFmpeg and the screen are open by now), then starts.
        void PrepTick()
        {
            if (!preparing) return;
            try
            {
                bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0 && OurForeground();
                if (!esc) escSeenUp = true;
                else if (escSeenUp) { CancelPrep(); return; }
                if (webcam != null)
                {
                    bool late = prepClock.ElapsedMilliseconds > CameraWaitMs;
                    if (webcam.GaveUp || late)
                    {
                        cameraNote = webcam.GaveUp ? "No se ha podido abrir la c\u00E1mara." : "La c\u00E1mara no ha respondido a tiempo.";
                        ShotStack.Log("C00E1mara: " + cameraNote + " Se graba sin ella.");
                        webcam.Dismiss();
                        webcam = null;
                    }
                    else if (!webcam.Live) return;
                }
                preparing = false;
                prep.Stop();
                Go();
            }
            catch (Exception ex)
            {
                preparing = false;
                prep.Stop();
                ShotStack.Log("Grabaci\u00F3n: " + ex);
                if (Abandon()) Recorder.Ended(this);
                MessageBox.Show("No se pudo empezar a grabar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void CancelPrep()
        {
            preparing = false;
            prep.Stop();
            ShotStack.Log("Grabaci\u00F3n cancelada antes de empezar");
            if (Abandon()) Recorder.Ended(this);
        }

        // Everything is ready: from here sound and image share one clock.
        void Go()
        {
            string rate = fpsNum + "/" + fpsDen;
            bar.Ready();
            Native.timeBeginPeriod(1);
            fineTimer = true;
            t0 = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 20;
            if (sys != null) sys.Begin(t0);
            if (mic != null) mic.Begin(t0);
            clock.Start();
            writer = new Thread(WriteLoop);
            writer.IsBackground = true;
            writer.Priority = ThreadPriority.AboveNormal;
            writer.Start();
            capture = new Thread(CaptureLoop);
            capture.IsBackground = true;
            capture.Priority = ThreadPriority.Highest;
            capture.Start();
            ShotStack.Log("Grabando " + (gif ? "GIF" : "v\u00EDdeo") + " " + area.Width + "x" + area.Height + " a " + rate + " fps \u00B7 " + source.Description +
                          " \u00B7 " + enc + (sys != null ? " \u00B7 sonido: " + sys.DeviceName : "") + (mic != null ? " \u00B7 micr\u00F3fono: " + mic.DeviceName : ""));
            if (notes.Count > 0) owner.Notify("Grabando sin parte del sonido", string.Join("\n", notes.ToArray()));
            if (cameraNote != null) owner.Notify("Grabando sin c\u00E1mara", cameraNote);
        }

        AudioCapture StartAudio(bool computer, string device, string path)
        {
            AudioCapture a = new AudioCapture(computer, device, path);
            if (a.Start()) return a;
            notes.Add(a.Error);
            ShotStack.Log("Sonido: " + a.Error);
            TryDelete(path);
            return null;
        }

        // On a 59.94 Hz panel, 59.94 (or 29.97) fps, so the video stays in step with the screen. Only the standard rates
        // are used: an odd one (a monitor reporting 60.001 Hz) confuses some editors, and the phase tracking absorbs the
        // tiny difference.
        void ChooseRate(int target)
        {
            fpsNum = target;
            fpsDen = 1;
            if (!source.Gpu || (target != 60 && target != 30)) return;
            double hz = source.RefreshNum / (double)source.RefreshDen;
            if (Math.Abs(hz - 60000 / 1001.0) < 0.02) { fpsNum = target * 1000; fpsDen = 1001; }
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
            if (preparing) { CancelPrep(); return; }
            if (stopping)
            {
                // Asked again while saving (the record shortcut, say): the bar shakes to say it is still busy.
                Busy();
                return;
            }
            t1 = Stopwatch.GetTimestamp();
            stopping = true;
            clock.Stop();
            // Discarding has nothing to wait for: the bar goes at once.
            if (bar != null) { if (cancelled) bar.Finish(false); else bar.Saving(); }
            if (webcam != null) { webcam.Dismiss(); webcam = null; }
            // The frame only marks what is being recorded: it goes now, while the bar keeps showing the saving.
            if (edges != null) { foreach (FrameEdge e in edges) e.Dismiss(); edges = null; }
        }

        public void Cancel()
        {
            cancelled = true;
            Stop();
        }

        // Another recording was asked for while this one runs or is being saved.
        public void Busy()
        {
            if (bar != null && !cancelled) bar.Nudge();
        }

        // Start threw before the recording was running: let go of whatever it had opened. False once the threads run
        // (the recording then goes on and ends as usual).
        internal bool Abandon()
        {
            if (writer != null) return false;
            if (prep != null) { prep.Stop(); prep.Dispose(); prep = null; }
            stopping = cancelled = true;
            if (proc != null)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
                try { proc.Dispose(); } catch { }
            }
            try { if (sys != null) sys.Finish(0); } catch { }
            try { if (mic != null) mic.Finish(0); } catch { }
            try { if (source != null) source.Dispose(); } catch { }
            if (pool != null) pool.Dispose();
            if (bar != null && !bar.IsDisposed) bar.Close();
            if (webcam != null && !webcam.IsDisposed) webcam.Close();
            if (edges != null) foreach (FrameEdge e in edges) if (!e.IsDisposed) e.Close();
            if (video != null && video != output) TryDelete(video);
            TryDelete(output);
            TryDelete(sysPath);
            TryDelete(micPath);
            EndPeriod();
            return true;
        }

        void EndPeriod()
        {
            if (!fineTimer) return;
            fineTimer = false;
            Native.timeEndPeriod(1);
        }

        // Video frame n shows the latest image presented before Cut(n). The compositor stamps each image when it
        // presents it, which scatters several milliseconds (unevenly) after the monitor's refresh. With the monitor's own
        // rate, the cut goes in the quietest part of the refresh interval, learned from the recent present times: each
        // frame then gets exactly one refresh and motion is perfectly even. Without a known refresh, halfway through the
        // frame's interval.
        double Cut(long n, double period, double cutPhase, bool locked)
        {
            return locked ? t0 + cutPhase + n * period : t0 + (n + 0.5) * period;
        }

        static double Wrap(double v, double m) { v %= m; return v < 0 ? v + m : v; }

        // Middle of the quietest quarter of the refresh interval (circular histogram of present times).
        static double Quietest(double[] hist, double display, double current)
        {
            int bins = hist.Length, w = bins / 4, best = 0;
            double bestSum = double.MaxValue;
            for (int s = 0; s < bins; s++)
            {
                double sum = 0;
                for (int j = 0; j < w; j++) sum += hist[(s + j) % bins];
                if (sum < bestSum) { bestSum = sum; best = s; }
            }
            // Only move for a clearly quieter place, so the cut doesn't wander between two similar ones.
            int cur = (int)(current / display * bins) - w / 2;
            double curSum = 0;
            for (int j = 0; j < w; j++) curSum += hist[((cur + j) % bins + bins) % bins];
            if (bestSum > curSum * 0.7) return current;
            return Wrap((best + w / 2.0) / bins * display, display);
        }

        // Takes every new screen image as it comes and does nothing else, so none is ever merged with the next. It runs
        // until the last frame is made (`acquired`), which can be a little after the user stops.
        volatile bool acquired;

        void AcquireLoop()
        {
            try
            {
                while (!acquired && !source.Broken)
                {
                    long present;
                    if (source.Wait(16, out present)) source.Take();
                }
            }
            catch (Exception ex) { ShotStack.Log("Grabaci\u00F3n (im\u00E1genes): " + ex.Message); }
        }

        void CaptureLoop()
        {
            long freq = Stopwatch.Frequency;
            double period = freq * (double)fpsDen / fpsNum;
            double display = source.Gpu ? freq * (double)source.RefreshDen / source.RefreshNum : period;
            bool locked = source.Gpu && display <= period * 1.02;
            // A frame is made a refresh after its cut: the compositor sometimes hands an image over late, and it must
            // still land in its own frame.
            long margin = freq * 3 / 1000 + (locked ? (long)display : 0);
            double[] hist = new double[32];
            int seen = 0;
            // Until there is a history, assume presents gather just after the refresh of the first image.
            double cutPhase = source.LastPresent != 0 ? Wrap(source.LastPresent - t0 + display * 0.6, display) : display / 2;
            Thread acquire = null;
            if (source.Gpu)
            {
                acquire = new Thread(AcquireLoop);
                acquire.IsBackground = true;
                acquire.Priority = ThreadPriority.Highest;
                acquire.Start();
            }
            long n = 0;
            try
            {
                while (!stopping)
                {
                    SampleCursor();
                    if (locked)
                        foreach (long present in source.NewPresents())
                        {
                            double ph = Wrap(present - t0, display);
                            for (int i = 0; i < hist.Length; i++) hist[i] *= 0.995; // about the last three seconds
                            hist[Math.Min(hist.Length - 1, (int)(ph / display * hist.Length))] += 1;
                            seen++;
                            if (seen == 1) cutPhase = Wrap(ph + display * 0.6, display);
                            else if (seen >= 20 && seen % 5 == 0) cutPhase = Quietest(hist, display, cutPhase);
                        }
                    long now = Stopwatch.GetTimestamp();
                    while (Cut(n, period, cutPhase, locked) + margin <= now && !stopping)
                    {
                        double late = (now - Cut(n, period, cutPhase, locked)) * 1000.0 / freq;
                        if (late > maxLate) maxLate = late;
                        if (!Emit(Cut(n, period, cutPhase, locked), locked ? display / 2 : 0)) break;
                        n++;
                    }
                    // Short sleeps keep the cursor history dense (a few ms apart).
                    int ms = (int)Math.Max(1, Math.Min(4, (Cut(n, period, cutPhase, locked) + margin - Stopwatch.GetTimestamp()) * 1000 / freq));
                    Thread.Sleep(ms);
                }
                // Up to the moment the user stopped.
                while (t0 + n * period < t1 && !cancelled && !writeFailed)
                {
                    if (!Emit(Cut(n, period, cutPhase, locked), locked ? display / 2 : 0)) break;
                    n++;
                }
                acquired = true;
                if (acquire != null) acquire.Join(2000);
                Drain(source.Pending);
            }
            catch (Exception ex)
            {
                if (!cancelled && error == null) error = "La captura de pantalla ha fallado: " + ex.Message;
                ShotStack.Log("Grabaci\u00F3n: " + ex);
            }
            acquired = true;
            if (acquire != null) acquire.Join(2000);
            frames = n;
            queue.CompleteAdding();
            Finish();
        }

        // One video frame: the image on screen at its cut, with the cursor of that moment (`back` before the cut, when
        // the image itself appeared).
        bool Emit(double cut, double back)
        {
            source.Submit(CursorAt(cut - back), source.Pick(cut));
            // Read one frame behind, so the GPU has finished it and nothing waits.
            return source.Pending < 2 || Drain(1);
        }

        // The cursor of the last moments, so each frame gets the cursor of its own instant (frames are written a little
        // after they were on screen).
        readonly long[] cursorTime = new long[128];
        readonly Native.CURSORINFO[] cursorShot = new Native.CURSORINFO[128];
        int cursorCount;

        void SampleCursor()
        {
            int i = cursorCount % cursorShot.Length;
            cursorShot[i] = Grabber.CursorNow();
            cursorTime[i] = Stopwatch.GetTimestamp();
            cursorCount++;
        }

        // Between two samples the position is interpolated, so movement stays smooth.
        Native.CURSORINFO CursorAt(double when)
        {
            if (!source.Gpu || cursorCount == 0) return Grabber.CursorNow();
            int size = cursorShot.Length, available = Math.Min(cursorCount, size);
            int after = (cursorCount - 1) % size;
            for (int k = 1; k < available; k++)
            {
                int before = (cursorCount - 1 - k) % size;
                if (cursorTime[before] > when) { after = before; continue; }
                Native.CURSORINFO a = cursorShot[before], b = cursorShot[after];
                double span = cursorTime[after] - cursorTime[before];
                if (span <= 0 || a.hCursor != b.hCursor || (a.flags & 1) != (b.flags & 1)) return a;
                double f = Math.Max(0, Math.Min(1, (when - cursorTime[before]) / span));
                a.ptScreenPos.X = (int)Math.Round(a.ptScreenPos.X + (b.ptScreenPos.X - a.ptScreenPos.X) * f);
                a.ptScreenPos.Y = (int)Math.Round(a.ptScreenPos.Y + (b.ptScreenPos.Y - a.ptScreenPos.Y) * f);
                return a;
            }
            return cursorShot[after];
        }

        bool Drain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                IntPtr buf = pool.Take(delegate { return cancelled || writeFailed; });
                if (buf == IntPtr.Zero) return false;
                if (source.Read(buf)) queue.Add(buf);
                else
                {
                    pool.Give(buf);
                    queue.Add(IntPtr.Zero); // repeat the previous frame
                }
            }
            return true;
        }

        void WriteLoop()
        {
            int size = source.FrameBytes;
            IntPtr held = IntPtr.Zero, black = IntPtr.Zero;
            try
            {
                foreach (IntPtr f in queue.GetConsumingEnumerable())
                {
                    IntPtr data = f;
                    if (data == IntPtr.Zero)
                    {
                        if (held == IntPtr.Zero && black == IntPtr.Zero) black = Black(size);
                        data = held != IntPtr.Zero ? held : black;
                    }
                    if (!writeFailed && !WriteFrame(data, size))
                    {
                        writeFailed = true;
                        if (!cancelled && error == null) error = "FFmpeg se ha cerrado durante la grabaci\u00F3n. " + Tail();
                        // Ends the recording if it still runs; a stop already under way must not read as a second press.
                        owner.Ui(delegate { if (!stopping) Stop(); });
                    }
                    if (f != IntPtr.Zero)
                    {
                        if (held != IntPtr.Zero) pool.Give(held);
                        held = f;
                    }
                }
            }
            catch (Exception ex) { ShotStack.Log("Grabaci\u00F3n: " + ex.Message); }
            finally
            {
                if (held != IntPtr.Zero) pool.Give(held);
                if (black != IntPtr.Zero) Marshal.FreeHGlobal(black);
                try { pipeStream.Close(); } catch { }
            }
        }

        IntPtr Black(int size)
        {
            IntPtr p = Marshal.AllocHGlobal(size);
            byte[] b = new byte[size];
            if (source.Nv12)
            {
                int luma = area.Width * area.Height;
                for (int i = 0; i < size; i++) b[i] = (byte)(i < luma ? 16 : 128);
            }
            Marshal.Copy(b, 0, p, size);
            return p;
        }

        bool WriteFrame(IntPtr data, int size)
        {
            try
            {
                if (pipe == null)
                {
                    byte[] b = new byte[size];
                    Marshal.Copy(data, b, 0, size);
                    pipeStream.Write(b, 0, size);
                    return true;
                }
                int off = 0;
                while (off < size)
                {
                    int done;
                    if (!WriteFile(pipe, new IntPtr(data.ToInt64() + off), size - off, out done, IntPtr.Zero) || done <= 0) return false;
                    off += done;
                }
                return true;
            }
            catch { return false; }
        }

        // On the capture thread. Whatever goes wrong while saving, the bar and the recording state still end.
        void Finish()
        {
            try { Complete(); }
            catch (Exception ex)
            {
                if (!cancelled && error == null) error = "No se pudo guardar la grabaci\u00F3n: " + ex.Message;
                ShotStack.Log("Grabaci\u00F3n: " + ex);
                // Whatever it had not released yet (each of these can be released twice).
                try { if (!proc.HasExited) proc.Kill(); } catch { }
                try { proc.Dispose(); } catch { }
                try { source.Dispose(); } catch { }
                pool.Dispose();
                TryDelete(output);
                if (video != output) TryDelete(video);
                TryDelete(sysPath);
                TryDelete(micPath);
            }
            owner.Ui(Done);
        }

        // Waits for FFmpeg, then makes the final file (GIF, or video with sound and the final encode).
        void Complete()
        {
            // A cancelled or stuck FFmpeg is killed, so the writer's pending write fails instead of waiting forever.
            if (cancelled) { try { proc.Kill(); } catch { } }
            if (writer != null && !writer.Join(120000))
            {
                try { proc.Kill(); } catch { }
                writer.Join(5000);
                if (error == null && !cancelled) error = "FFmpeg ha dejado de responder.";
            }
            if (cancelled)
            {
                try { if (!proc.WaitForExit(3000)) proc.Kill(); } catch { }
            }
            else if (!proc.WaitForExit(120000))
            {
                try { proc.Kill(); } catch { }
                if (error == null) error = "FFmpeg no termin\u00F3 de guardar el v\u00EDdeo.";
            }
            if (error == null && !cancelled && proc.ExitCode != 0) error = "FFmpeg no pudo guardar el v\u00EDdeo. " + Tail();
            double seconds = frames * (double)fpsDen / fpsNum;
            long samples = (long)Math.Round(seconds * AudioCapture.Rate);
            if (sys != null) sys.Finish(samples);
            if (mic != null) mic.Finish(samples);
            if (source.Gpu) ShotStack.Log("Grabaci\u00F3n: " + frames + " fotogramas, " + source.Presents + " im\u00E1genes nuevas de la pantalla, retraso m\u00E1ximo " + (int)maxLate + " ms");
            try { source.Dispose(); } catch { }
            pool.Dispose();
            if (error == null && !cancelled && gif) MakeGif(seconds);
            else if (error == null && !cancelled && !direct) MakeVideo(seconds);
            if (cancelled || error != null) TryDelete(output);
            if (video != output) TryDelete(video);
            TryDelete(sysPath);
            TryDelete(micPath);
            try { proc.Dispose(); } catch { }
        }

        void MakeGif(double seconds)
        {
            // Per-GIF palette with light dithering: accurate colors, small files.
            int w = Math.Min(area.Width, 960) & ~1;
            Encode("-i " + Recorder.Quote(video) + " -vf \"fps=" + gifFps + ",scale=" + w + ":-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];" +
                   "[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle\" -loop 0 " + Recorder.Quote(output),
                   "Creando el GIF\u2026", seconds, "No se pudo crear el GIF.");
        }

        // The finished MP4: sound added (voice brought to a steady level, mixed without clipping) and, for the lossless
        // qualities, the final H.264 encode with exact BT.709 colors. Cinema doubles the resolution (up to 4K) with
        // nearest-neighbor pixels, so 4:2:0 keeps every pixel's color: text and thin colored lines look exactly as on
        // screen, also when platforms re-encode it.
        void MakeVideo(double seconds)
        {
            double fps = fpsNum / (double)fpsDen;
            string args = "-i " + Recorder.Quote(video);
            int next = 1, sysIn = -1, micIn = -1;
            if (sys != null) { args += " -f f32le -sample_rate 48000 -ch_layout stereo -i " + Recorder.Quote(sysPath); sysIn = next++; }
            if (mic != null) { args += " -f f32le -sample_rate 48000 -ch_layout stereo -i " + Recorder.Quote(micPath); micIn = next++; }
            List<string> graph = new List<string>();
            string vmap = "0:v", vcodec = "-c:v copy", amap = null;
            if (lossless)
            {
                long pixels = (long)area.Width * area.Height;
                string chain;
                if (source.Nv12) chain = Recorder.Tags;
                else if (quality == Recorder.Cinema && ((area.Width * 2 + 15) / 16) * ((area.Height * 2 + 15) / 16) <= 36864)
                {
                    // H.264 level 5.2 holds up to about 4096x2304 at 60 fps.
                    chain = "scale=iw*2:ih*2:flags=neighbor+accurate_rnd+full_chroma_int+full_chroma_inp:out_color_matrix=bt709:out_range=tv,format=yuv420p," + Recorder.Tags;
                    pixels *= 4;
                }
                else chain = "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int+full_chroma_inp,format=yuv420p," + Recorder.Tags;
                graph.Add("[0:v]" + chain + "[v]");
                vmap = "[v]";
                vcodec = Recorder.FinalCodec(quality, pixels, fps);
            }
            string voice = micIn >= 0 ? "[" + micIn + ":a]highpass=f=70" + VoiceGain() : null;
            const string limiter = "alimiter=limit=0.891:attack=5:release=60:level=0:latency=1";
            if (sysIn >= 0 && micIn >= 0)
            {
                graph.Add(voice + "[m];[" + sysIn + ":a][m]amix=inputs=2:duration=first:dropout_transition=0:normalize=0," + limiter + "[a]");
                amap = "[a]";
            }
            else if (micIn >= 0)
            {
                graph.Add(voice + "," + limiter + "[a]");
                amap = "[a]";
            }
            else if (sysIn >= 0) amap = sysIn + ":a";
            if (graph.Count > 0) args += " -filter_complex \"" + string.Join(";", graph.ToArray()) + "\"";
            args += " -map \"" + vmap + "\" " + vcodec;
            if (amap != null) args += " -map \"" + amap + "\" " + Recorder.AudioCodec(quality);
            args += " -movflags +faststart " + Recorder.Quote(output);
            string label = quality == Recorder.Cinema ? "Guardando en calidad cine\u2026" : quality == Recorder.Maximum ? "Guardando en m\u00E1xima calidad\u2026" : "A\u00F1adiendo el sonido\u2026";
            Encode(args, label, seconds, "No se pudo guardar el v\u00EDdeo.");
        }

        // A fixed gain that brings the voice to about -16 LUFS (EBU R128), the level of good voice-overs: no compression
        // or pumping, just the right volume. Silence and near silence are left alone.
        string VoiceGain()
        {
            string err;
            Recorder.Run(ffmpeg, "-hide_banner -nostats -f f32le -sample_rate 48000 -ch_layout stereo -i " + Recorder.Quote(micPath) +
                                 " -af highpass=f=70,loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json -f null -", 300000, out err);
            Match m = Regex.Match(err ?? "", "\"input_i\"\\s*:\\s*\"(-?[0-9.]+)\"");
            double i;
            if (!m.Success || !double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out i) || i < -50 || i > 0) return "";
            double gain = Math.Max(-6, Math.Min(15, -16 - i));
            ShotStack.Log("Micr\u00F3fono: " + i.ToString("0.0", CultureInfo.InvariantCulture) + " LUFS, ganancia " + gain.ToString("0.0", CultureInfo.InvariantCulture) + " dB");
            return Math.Abs(gain) < 0.5 ? "" : ",volume=" + gain.ToString("0.0", CultureInfo.InvariantCulture) + "dB";
        }

        // Runs a final FFmpeg pass at low priority (the computer stays responsive), with its progress on the bar.
        void Encode(string args, string label, double seconds, string failure)
        {
            lock (errors) errors.Length = 0;
            owner.Ui(delegate { if (bar != null) bar.Converting(label, 0); });
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -nostdin -loglevel error -nostats -progress pipe:1 -y " + args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    int shown = -1;
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        if (e.Data == null || !e.Data.StartsWith("out_time_us=") || seconds <= 0) return;
                        long us;
                        if (!long.TryParse(e.Data.Substring(12), out us)) return;
                        int pct = (int)Math.Max(0, Math.Min(99, us / 1e6 / seconds * 100));
                        if (pct == shown) return;
                        shown = pct;
                        owner.Ui(delegate { if (bar != null) bar.Converting(label, pct / 100.0); });
                    };
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        lock (errors) { if (errors.Length < 4000) errors.AppendLine(e.Data); }
                    };
                    p.Start();
                    try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    if (p.ExitCode != 0) error = failure + " " + Tail();
                }
            }
            catch (Exception ex) { error = failure + " " + ex.Message; }
        }

        void Done()
        {
            EndPeriod();
            bool saved = error == null && !cancelled && File.Exists(output);
            // A short check when the file is ready; the bar closes itself after its fade.
            if (bar != null) bar.Finish(saved);
            if (webcam != null) { webcam.Close(); webcam = null; }
            if (edges != null) { foreach (FrameEdge e in edges) e.Close(); edges = null; }
            Recorder.Ended(this);
            if (error != null)
            {
                ShotStack.Log("Grabaci\u00F3n: " + error);
                MessageBox.Show(error, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (saved)
            {
                ShotStack.Log("Grabaci\u00F3n lista: " + Path.GetFileName(output) + " (" + frames + " fotogramas, " + (new FileInfo(output).Length / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB)");
                owner.AddRecording(output);
            }
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
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    // Floating HUD while recording: time, the sound being recorded (live meters; click a source to mute it), Stop and
    // Discard. On Stop it narrows into a pill with the saving progress, confirms with a check and fades out. A dark
    // capsule with a soft shadow, excluded from the capture; its empty parts drag it out of the way.
    class RecordBar : FloatWindow
    {
        enum Phase { Rec, Busy, Progress, Done, Gone }

        const string DiscardHint = "\u00BFDescartar?";
        const double ArmMs = 3000;          // a first click on Discard asks for a second one within this time
        const double QuickDiscardMs = 3000; // shorter recordings are discarded at once
        static readonly WM.Color Red = Ds.Rgb(255, 59, 48), Accent = Ds.Rgb(10, 132, 255), Green = Ds.Rgb(48, 209, 88);

        readonly Session session;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        readonly bool hasSys, hasMic;
        readonly float[] meter = new float[2];
        readonly Tween cap = new Tween(0);     // capsule width; the window may be wider (transparent, click-through)
        readonly Tween fade = new Tween(1);    // opacity of the content shown
        static RecordBar previous;             // the last bar, which may still be saying "Guardado"
        // phase, label and progress say what the recording is doing; the face is what the capsule shows, which follows
        // them through a short fade (and keeps the recording controls while a discarded bar fades out).
        Phase phase = Phase.Rec, face = Phase.Rec;
        string label, faceLabel, shown;
        double progress = -1, faceProgress = -1, armedAt = -1, nudgeAt = -1;
        int hot = -1, pressed = -1, spin, faceW;
        bool dragging, longClock;
        Point dragFrom;
        double dragX, dragY;
        // Recording layout, in capsule coordinates.
        int recW, timeX, srcX, stopX, stopW, discardX;
        WI.BitmapSource shadowL, shadowM, shadowR;
        WM.FormattedText[] clockGlyphs;          // 0-9 and the colon
        double digitW;
        int shadowCore;

        public RecordBar(Session session, Rectangle area)
        {
            this.session = session;
            hasSys = session.SystemAudio != null;
            hasMic = session.Microphone != null;
            // A new recording started while the last bar still confirms its file: that one gets out of the way.
            if (previous != null && !previous.IsDisposed && previous.phase == Phase.Done) previous.StepAside();
            previous = this;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            Pad = P(22);
            Plan();
            cap.Set(recW);
            Size sz = new Size(recW, P(46));
            Rectangle wa = scr.WorkingArea;
            int x0 = area.X + (area.Width - sz.Width) / 2;
            int y0 = area.Bottom + P(14);
            if (y0 + sz.Height > wa.Bottom) y0 = area.Top - sz.Height - P(14);
            // Full-screen area: inside it (it is excluded from the video anyway), above the taskbar.
            if (y0 < wa.Top) y0 = Math.Min(area.Bottom, wa.Bottom) - sz.Height - P(18);
            x0 = Math.Max(wa.Left + P(8), Math.Min(wa.Right - sz.Width - P(8), x0));
            SetSize(sz);
            JumpTo(x0, y0 + P(10));
            ShowQuiet();
            alpha.Go(1, 200, 0, Ease.OutCubic, null);
            MoveTo(x0, y0, 320, 0.78, 0);
            tick.Tick += delegate { Tick(); };
            Retime();
        }

        protected override bool PerPixel { get { return true; } }

        // Devices are being opened: nothing is recorded yet.
        public void Prepare()
        {
            if (phase == Phase.Rec && !IsDisposed) Become(Phase.Busy, "Preparando2026", -1);
        }

        // Everything is ready and recording starts: back to the recording controls.
        public void Ready()
        {
            if (phase != Phase.Busy || IsDisposed) return;
            phase = Phase.Rec;
            label = null;
            hot = pressed = -1;
            Morph(recW);
            fade.Go(0, fade.Value > 0.01 ? 90 : 1, 0, Ease.OutCubic, Swap);
            Retime();
            Anim.Wake(this);
        }

        // Recording stopped: saving starts.
        public void Saving()
        {
            if (phase == Phase.Rec && !IsDisposed) Become(Phase.Busy, "Guardando\u2026", -1);
        }

        // A final pass with its progress; fraction < 0: unknown.
        public void Converting(string text, double fraction)
        {
            if (phase == Phase.Done || phase == Phase.Gone || IsDisposed) return;
            if (phase == Phase.Progress && text == label && (fraction < 0) == (progress < 0))
            {
                progress = fraction;
                if (face == Phase.Progress && faceLabel == text) faceProgress = fraction;
                Repaint();
                return;
            }
            Become(Phase.Progress, text, fraction);
        }

        // The end: a short check when the file was saved, then it fades out and closes (at once when discarded or failed).
        public void Finish(bool saved)
        {
            if (phase == Phase.Done || phase == Phase.Gone || IsDisposed) return;
            double wait = 0;
            if (saved) { Become(Phase.Done, "Guardado", -1); wait = 900; }
            else phase = Phase.Gone;
            tick.Stop();
            dragging = false;
            Cursor = Cursors.Default;
            alpha.Go(0, saved ? 240 : 160, wait, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            if (!moving) MoveTo(tx, ty + P(6), 260, 1, wait);
            Anim.Wake(this);
        }

        // Out of the way at once (a new recording is starting).
        void StepAside()
        {
            phase = Phase.Gone;
            tick.Stop();
            alpha.Go(0, 120, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            Anim.Wake(this);
        }

        // Another recording was asked for while this one runs or is being saved: a short shake says "busy".
        public void Nudge()
        {
            if (phase == Phase.Gone || IsDisposed || dragging) return;
            nudgeAt = Anim.Now;
            Anim.Wake(this);
        }

        void Become(Phase p, string text, double fraction)
        {
            phase = p;
            label = text;
            progress = fraction;
            hot = pressed = -1;
            armedAt = -1;
            if (!dragging) Cursor = Cursors.Default;
            Morph(PillWidth());
            // The old content fades out quickly while the capsule changes shape, then the new one fades in.
            fade.Go(0, fade.Value > 0.01 ? 90 : 1, 0, Ease.OutCubic, Swap);
            Retime();
            Anim.Wake(this);
        }

        void Swap()
        {
            if (phase == Phase.Gone || IsDisposed) return;
            face = phase;
            faceLabel = label;
            faceProgress = progress;
            faceW = PillWidth();
            fade.Go(1, 200, 0, Ease.OutCubic, null);
        }

        // The timer only runs while something on the bar changes by itself: the meters and the clock, or the spinner.
        void Retime()
        {
            if (IsDisposed) { tick.Stop(); return; }
            bool spinning = phase == Phase.Busy || (phase == Phase.Progress && progress < 0);
            if (phase == Phase.Rec) tick.Interval = Metering() ? 50 : NextSecond();
            else if (spinning) tick.Interval = 90;
            if (phase == Phase.Rec || spinning) tick.Start(); else tick.Stop();
        }

        bool Metering() { return (hasSys && !session.SystemAudio.Muted) || (hasMic && !session.Microphone.Muted); }

        // Without live meters only the clock changes (and the discard question expires): wake just after that.
        int NextSecond()
        {
            double ms = 1000 - session.Elapsed.TotalMilliseconds % 1000 + 15;
            if (armedAt >= 0) ms = Math.Min(ms, ArmMs - (Anim.Now - armedAt) + 15);
            return (int)Math.Max(20, Math.Min(1015, ms));
        }

        void Tick()
        {
            if (IsDisposed) return;
            if (phase == Phase.Rec)
            {
                if (hasSys) meter[0] = Math.Max(session.SystemAudio.Muted ? 0 : session.SystemAudio.Level, meter[0] * 0.8f);
                if (hasMic) meter[1] = Math.Max(session.Microphone.Muted ? 0 : session.Microphone.Level, meter[1] * 0.8f);
                if (armedAt >= 0 && Anim.Now - armedAt > ArmMs) armedAt = -1;
                if (!longClock && session.Elapsed.TotalHours >= 1)
                {
                    longClock = true;
                    Plan();
                    Morph(recW);
                    Anim.Wake(this);
                }
                if (!Metering()) Retime();
            }
            else spin++;
            Repaint();
        }

        // Repaints only when something visible changed (a still bar costs one paint per second).
        void Repaint()
        {
            if (Signature() != shown) Redraw();
        }

        string Signature()
        {
            if (face == Phase.Rec)
                return "r" + Clock(session.Elapsed) + "|" + Bars(0) + Bars(1) + "|" + hot + "|" + pressed + "|" + (armedAt >= 0) +
                       (hasSys && session.SystemAudio.Muted) + (hasMic && session.Microphone.Muted);
            return face + "|" + faceLabel + "|" + (int)Math.Round(faceProgress * 100) + "|" + spin;
        }

        static string Clock(TimeSpan t)
        {
            int h = (int)t.TotalHours;
            string ms = t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            return h > 0 ? h + ":" + ms : ms;
        }

        // Every digit takes the widest one's room, like tabular figures: the time never jitters as it counts.
        WM.FormattedText ClockGlyph(char c)
        {
            if (clockGlyphs == null)
            {
                clockGlyphs = new WM.FormattedText[11];
                for (int i = 0; i < 11; i++)
                {
                    clockGlyphs[i] = Ink.Px(i < 10 ? ((char)('0' + i)).ToString() : ":", Ds.Semibold, P(14), Palette.HudLabel);
                    if (i < 10) digitW = Math.Max(digitW, clockGlyphs[i].WidthIncludingTrailingWhitespace);
                }
            }
            return clockGlyphs[c >= '0' && c <= '9' ? c - '0' : 10];
        }

        double ClockCell(char c) { WM.FormattedText g = ClockGlyph(c); return c >= '0' && c <= '9' ? digitW : g.WidthIncludingTrailingWhitespace; }

        double ClockWidth(string text)
        {
            double w = 0;
            foreach (char c in text) w += ClockCell(c);
            return w;
        }

        void DrawClock(WM.DrawingContext dc, string text, double x, double cy)
        {
            foreach (char c in text)
            {
                WM.FormattedText g = ClockGlyph(c);
                double cell = ClockCell(c);
                dc.DrawText(g, new System.Windows.Point(x + (cell - g.WidthIncludingTrailingWhitespace) / 2, Math.Round(cy - g.Height / 2)));
                x += cell;
            }
        }

        // Lit bars of a source's meter (0-3), from -50 dB to 0 dB.
        int Bars(int i)
        {
            if (i == 0 ? !hasSys || session.SystemAudio.Muted : !hasMic || session.Microphone.Muted) return 0;
            float level = meter[i];
            double db = level > 0.0001 ? 20 * Math.Log10(level) : -100;
            double v = Math.Max(0, Math.Min(1, (db + 50) / 50));
            int n = 0;
            for (int k = 0; k < 3; k++) if (v > (k + 0.5) / 3.5) n++;
            return n;
        }

        // ---- layout ----

        void Plan()
        {
            double clockW = ClockWidth(longClock ? "0:00:00" : "00:00");
            WM.FormattedText hint = Ink.Px(DiscardHint, Ds.Semibold, P(13), Palette.HudLabel);
            WM.FormattedText stop = Ink.Px("Detener", Ds.Semibold, P(13), Palette.HudLabel);
            timeX = P(34);
            srcX = timeX + (int)Math.Ceiling(clockW) + P(10);
            int left = srcX + (hasSys ? P(44) : 0) + (hasMic ? P(44) : 0);
            // While Discard is armed its question takes the place of the dot, the time and the sources.
            stopX = Math.Max(left, P(14) + (int)Math.Ceiling(hint.WidthIncludingTrailingWhitespace) + P(8));
            stopW = P(30) + (int)Math.Ceiling(stop.WidthIncludingTrailingWhitespace) + P(16);
            discardX = stopX + stopW + P(4);
            recW = discardX + P(30) + P(8);
        }

        Rectangle StopRect() { return new Rectangle(stopX, P(8), stopW, body.Height - P(16)); }
        Rectangle DiscardRect() { return new Rectangle(discardX, (body.Height - P(30)) / 2, P(30), P(30)); }
        Rectangle SourceRect(int i) { return new Rectangle(srcX + i * P(44), P(7), P(40), body.Height - P(14)); }

        int PillIcon { get { return P(42); } }

        int PillWidth()
        {
            WM.FormattedText t = Ink.Px(label ?? "", Ds.Semibold, P(13), Palette.HudLabel);
            int w = PillIcon + (int)Math.Ceiling(t.WidthIncludingTrailingWhitespace) + P(18);
            if (phase == Phase.Progress && progress >= 0) w += P(12) + PercentWidth();
            return Math.Max(P(120), w);
        }

        int PercentWidth() { return (int)Math.Ceiling(Ink.Px("100 %", Ds.Medium, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace); }

        int CapLeft() { return (int)Math.Round((body.Width - Math.Round(cap.Value)) / 2); }

        // The capsule glides to a new width around its center; the window only grows (in one move) when it must.
        void Morph(int width)
        {
            if (width > body.Width && IsHandleCreated)
            {
                int grow = width - body.Width;
                grow += grow & 1;
                body = new Size(body.Width + grow, body.Height);
                // Centered on the old capsule, but never past the edges of the screen.
                Rectangle wa = Screen.FromHandle(Handle).WorkingArea;
                double nx = Math.Max(wa.Left + P(8), Math.Min(wa.Right - body.Width - P(8), tx - grow / 2));
                x += nx - tx;
                tx = nx;
                Native.SetWindowPos(Handle, IntPtr.Zero, (int)Math.Round(x) - Pad, (int)Math.Round(y) - Pad, body.Width + 2 * Pad, body.Height + 2 * Pad,
                                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                ApplyPos();
            }
            cap.Go(width, 360, 0, Ease.OutCubic, null);
        }

        protected override bool StepExtra(double now)
        {
            bool repaint = cap.Running || fade.Running;
            cap.Step(now);
            fade.Step(now);
            bool shaking = false;
            if (nudgeAt >= 0)
            {
                double t = (now - nudgeAt) / 420.0;
                if (t >= 1 || moving || dragging) { nudgeAt = -1; if (!moving && !dragging) x = tx; }
                else { x = tx + Math.Sin(t * Math.PI * 6) * (1 - t) * P(7); shaking = true; }
                if (!moving) ApplyPos();
            }
            if (repaint && !IsDisposed) Redraw();
            return cap.Running || fade.Running || shaking;
        }

        // ---- painting ----

        static System.Windows.Rect R(Rectangle r) { return new System.Windows.Rect(r.X, r.Y, r.Width, r.Height); }

        // One soft shadow rendered once; its middle column stretches to any capsule width.
        void Shadow(WM.DrawingContext dc, double left, double width, int h)
        {
            if (shadowL == null)
            {
                shadowCore = P(64);
                int w = shadowCore + 2 * Pad, half = Pad + shadowCore / 2;
                WI.BitmapSource sh = Ink.Shadow(w, h, new System.Windows.Rect(Pad, Pad + P(5), shadowCore, body.Height), P(14), P(16), Ds.Argb(0.45, 0, 0, 0));
                shadowL = Crop(sh, 0, half - 2, h);
                shadowM = Crop(sh, half - 2, 4, h);
                shadowR = Crop(sh, half + 2, w - half - 2, h);
            }
            double lx = left - Pad, rx = left + width - shadowCore / 2 + 2;
            dc.DrawImage(shadowL, new System.Windows.Rect(lx, 0, shadowL.PixelWidth, h));
            dc.DrawImage(shadowR, new System.Windows.Rect(rx, 0, shadowR.PixelWidth, h));
            double mx = lx + shadowL.PixelWidth;
            if (rx > mx) dc.DrawImage(shadowM, new System.Windows.Rect(mx, 0, rx - mx, h));
        }

        static WI.BitmapSource Crop(WI.BitmapSource src, int x0, int w, int h)
        {
            WI.CroppedBitmap c = new WI.CroppedBitmap(src, new System.Windows.Int32Rect(x0, 0, w, h));
            c.Freeze();
            return c;
        }

        protected override void PaintSurface(WM.DrawingContext dc, int w, int h)
        {
            shown = Signature();
            double cw = Math.Round(cap.Value), cx = CapLeft();
            Shadow(dc, Pad + cx, cw, h);
            dc.PushTransform(new WM.TranslateTransform(Pad + cx, Pad));
            System.Windows.Rect b = new System.Windows.Rect(0, 0, cw, body.Height);
            Ink.Round(dc, Ds.Argb(0.93, 30, 30, 32), b, P(14));
            Ink.Hairline(dc, Palette.HudLine, b, P(14));
            dc.PushClip(new WM.RectangleGeometry(b, P(14), P(14)));
            dc.PushOpacity(Math.Max(0, Math.Min(1, fade.Value)));
            // The content keeps its final layout, centered: the capsule's edges reveal or hide it while it changes size.
            int fw = face == Phase.Rec ? recW : faceW;
            dc.PushTransform(new WM.TranslateTransform(Math.Round((cw - fw) / 2), 0));
            if (face == Phase.Rec) PaintRec(dc);
            else PaintPill(dc, fw);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        void PaintRec(WM.DrawingContext dc)
        {
            double cy = body.Height / 2.0;
            WM.Color white = Palette.HudLabel, dim = Palette.HudLabel2;
            bool armed = armedAt >= 0;
            if (armed)
            {
                WM.FormattedText ht = Ink.Px(DiscardHint, Ds.Semibold, P(13), Ds.Rgb(255, 138, 128));
                dc.DrawText(ht, new System.Windows.Point(P(14), Math.Round(cy - ht.Height / 2)));
            }
            else
            {
                dc.DrawEllipse(Ds.Brush(Red), null, new System.Windows.Point(P(20), cy), 4.5 * s, 4.5 * s);
                DrawClock(dc, Clock(session.Elapsed), timeX, cy);
                if (hasSys) PaintSource(dc, SourceRect(0), 0, session.SystemAudio.Muted ? "soundoff" : "sound", session.SystemAudio.Muted, hot == 3);
                if (hasMic) PaintSource(dc, SourceRect(hasSys ? 1 : 0), 1, session.Microphone.Muted ? "micoff" : "mic", session.Microphone.Muted, hot == 2);
            }
            System.Windows.Rect sr = R(StopRect());
            Ink.Round(dc, hot == 0 && pressed == 0 ? Ds.Rgb(222, 46, 36) : hot == 0 ? Ds.Rgb(255, 92, 82) : Red, sr, sr.Height / 2);
            double sq = 9 * s;
            Ink.Round(dc, white, new System.Windows.Rect(Math.Round(sr.X + P(14)), Math.Round(cy - sq / 2), sq, sq), 2 * s);
            WM.FormattedText st = Ink.Px("Detener", Ds.Semibold, P(13), white);
            dc.DrawText(st, new System.Windows.Point(Math.Round(sr.X + P(30)), Math.Round(cy - st.Height / 2)));
            System.Windows.Rect dr = R(DiscardRect());
            System.Windows.Point dcp = new System.Windows.Point(dr.X + dr.Width / 2, cy);
            if (armed) dc.DrawEllipse(Ds.Brush(hot == 1 && pressed == 1 ? Ds.Rgb(222, 46, 36) : hot == 1 ? Ds.Rgb(255, 92, 82) : Red), null, dcp, dr.Width / 2, dr.Width / 2);
            else if (hot == 1) dc.DrawEllipse(Ds.Brush(pressed == 1 ? Ds.Argb(0.26, 255, 255, 255) : Palette.HudHover), null, dcp, dr.Width / 2, dr.Width / 2);
            double gs = 15 * s;
            Glyph.Draw(dc, "trash", dcp.X - gs / 2, cy - gs / 2, gs, armed || hot == 1 ? white : dim, Math.Max(1.4, 1.6 * s));
        }

        // An audio source: its icon and three bars that light up with the level; red and silent while muted.
        void PaintSource(WM.DrawingContext dc, Rectangle r, int i, string icon, bool muted, bool hover)
        {
            if (hover) Ink.Round(dc, pressed == (i == 0 ? 3 : 2) ? Ds.Argb(0.26, 255, 255, 255) : Palette.HudHover, R(r), P(8));
            double gs = 17 * s, cy = r.Y + r.Height / 2.0;
            Glyph.Draw(dc, icon, r.X + P(4), cy - gs / 2, gs, muted ? Ds.Rgb(255, 105, 97) : Palette.HudLabel, Math.Max(1.4, 1.6 * s));
            int lit = Bars(i);
            for (int k = 0; k < 3; k++)
            {
                double bh = P(4 + k * 4), bx = r.X + P(25) + k * 4.5 * s, by = cy + P(7) - bh;
                Ink.Round(dc, k < lit ? Green : Ds.Argb(muted ? 0.12 : 0.22, 255, 255, 255), new System.Windows.Rect(bx, by, 2.5 * s, bh), 1.2 * s);
            }
        }

        void PaintPill(WM.DrawingContext dc, double cw)
        {
            double cy = body.Height / 2.0, ix = P(23), r = 8.5 * s, th = Math.Max(1.5, 2 * s);
            WM.Color white = Palette.HudLabel;
            bool ring = face == Phase.Progress && faceProgress >= 0;
            if (face == Phase.Done)
            {
                dc.DrawEllipse(Ds.Brush(Green), null, new System.Windows.Point(ix, cy), r + 1, r + 1);
                double gs = 13 * s;
                Glyph.Draw(dc, "check", ix - gs / 2, cy - gs / 2, gs, white, Math.Max(1.8, 2.2 * s));
            }
            else if (ring) Ring(dc, ix, cy, r, th, faceProgress);
            else Spinner(dc, ix, cy, th);
            double right = cw - P(18);
            if (ring)
            {
                WM.FormattedText pt = Ink.Px((int)Math.Round(faceProgress * 100) + " %", Ds.Medium, P(12), Palette.HudLabel2);
                dc.DrawText(pt, new System.Windows.Point(Math.Round(right - pt.WidthIncludingTrailingWhitespace), Math.Round(cy - pt.Height / 2)));
                right -= PercentWidth() + P(12);
            }
            WM.FormattedText lt = Ink.Px(faceLabel, Ds.Semibold, P(13), white);
            lt.MaxTextWidth = Math.Max(1, right - PillIcon);
            lt.MaxLineCount = 1;
            lt.Trimming = System.Windows.TextTrimming.CharacterEllipsis;
            dc.DrawText(lt, new System.Windows.Point(PillIcon, Math.Round(cy - lt.Height / 2)));
        }

        // Determinate progress: a thin ring that fills clockwise from the top.
        static void Ring(WM.DrawingContext dc, double cx, double cy, double r, double th, double f)
        {
            WM.Pen track = new WM.Pen(Ds.Brush(Ds.Argb(0.2, 255, 255, 255)), th);
            track.Freeze();
            dc.DrawEllipse(null, track, new System.Windows.Point(cx, cy), r, r);
            f = Math.Max(0, Math.Min(1, f));
            if (f <= 0.005) return;
            WM.Pen pen = new WM.Pen(Ds.Brush(Accent), th);
            pen.StartLineCap = pen.EndLineCap = WM.PenLineCap.Round;
            pen.Freeze();
            if (f >= 0.995) { dc.DrawEllipse(null, pen, new System.Windows.Point(cx, cy), r, r); return; }
            double a = f * 2 * Math.PI;
            WM.StreamGeometry g = new WM.StreamGeometry();
            using (WM.StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new System.Windows.Point(cx, cy - r), false, false);
                c.ArcTo(new System.Windows.Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a)), new System.Windows.Size(r, r), 0, a > Math.PI,
                        WM.SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }

        // Unknown progress: the macOS spinner, eight spokes fading behind the leading one.
        void Spinner(WM.DrawingContext dc, double cx, double cy, double th)
        {
            const int n = 8;
            double r0 = 4 * s, r1 = 8.5 * s;
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n - Math.PI / 2;
                int age = ((spin - i) % n + n) % n;
                WM.Pen p = new WM.Pen(Ds.Brush(Ds.WithAlpha(Palette.HudLabel, Math.Max(0.2, 1 - age * 0.13))), th);
                p.StartLineCap = p.EndLineCap = WM.PenLineCap.Round;
                p.Freeze();
                dc.DrawLine(p, new System.Windows.Point(cx + Math.Cos(a) * r0, cy + Math.Sin(a) * r0), new System.Windows.Point(cx + Math.Cos(a) * r1, cy + Math.Sin(a) * r1));
            }
        }

        // ---- mouse ----

        Point Local(Point p) { return new Point(p.X - Pad, p.Y - Pad); }

        bool OnCapsule(Point p)
        {
            int cx = CapLeft();
            return p.X >= cx && p.X < cx + Math.Round(cap.Value) && p.Y >= 0 && p.Y < body.Height;
        }

        // 0 Stop, 1 Discard, 2 microphone, 3 computer sound; -1 none.
        int HitTest(Point p)
        {
            if (phase != Phase.Rec || face != Phase.Rec || cap.Running) return -1;
            Point q = new Point(p.X - CapLeft(), p.Y);
            if (StopRect().Contains(q)) return 0;
            if (DiscardRect().Contains(q)) return 1;
            if (armedAt >= 0) return -1;
            if (hasMic && SourceRect(hasSys ? 1 : 0).Contains(q)) return 2;
            if (hasSys && SourceRect(0).Contains(q)) return 3;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // Once it says "Guardado" (or is going) it is only leaving: nothing to press or drag.
            if (e.Button != MouseButtons.Left || phase == Phase.Gone || phase == Phase.Done) return;
            Point p = Local(e.Location);
            pressed = HitTest(p);
            if (pressed >= 0) { Repaint(); return; }
            if (!OnCapsule(p)) return;
            dragging = true;
            dragFrom = PointToScreen(e.Location);
            dragX = moving ? tx : x;
            dragY = moving ? ty : y;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) { Drag(PointToScreen(e.Location)); return; }
            int h = HitTest(Local(e.Location));
            if (h != hot)
            {
                hot = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                Repaint();
            }
        }

        // Moves the bar with the mouse, keeping the capsule inside the screen's working area.
        void Drag(Point c)
        {
            if (phase == Phase.Done || phase == Phase.Gone) return;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;
            int cx = CapLeft(), cw = (int)Math.Round(cap.Value);
            double nx = Math.Max(wa.Left - cx, Math.Min(wa.Right - cx - cw, dragX + c.X - dragFrom.X));
            double ny = Math.Max(wa.Top, Math.Min(wa.Bottom - body.Height, dragY + c.Y - dragFrom.Y));
            x = tx = nx;
            y = ty = ny;
            vx = vy = 0;
            moving = false;
            nudgeAt = -1;
            ApplyPos();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            Point p = Local(e.Location);
            if (dragging)
            {
                dragging = false;
                hot = HitTest(p);
                Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
                Repaint();
                return;
            }
            int down = pressed;
            pressed = -1;
            if (down >= 0 && HitTest(p) == down) Act(down);
            if (!IsDisposed) Repaint();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture) return;
            dragging = false;
            if (pressed >= 0) { pressed = -1; Repaint(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1 && !dragging) { hot = -1; Repaint(); }
        }

        void Act(int what)
        {
            if (phase != Phase.Rec) return;
            if (what == 0) session.Stop();
            else if (what == 1)
            {
                // A long recording is only thrown away on a second click.
                if (armedAt >= 0 || session.Elapsed.TotalMilliseconds < QuickDiscardMs) session.Cancel();
                else { armedAt = Anim.Now; Retime(); Repaint(); }
            }
            else if (what == 2) { session.Microphone.Muted = !session.Microphone.Muted; meter[1] = 0; Retime(); Repaint(); }
            else if (what == 3) { session.SystemAudio.Muted = !session.SystemAudio.Muted; meter[0] = 0; Retime(); Repaint(); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            tick.Dispose();
            if (previous == this) previous = null;
        }
    }

    // Quiet frame around the recorded area: a fine translucent white line with white corner marks, on click-through
    // strips that never appear in the video. A faint dark edge keeps both visible on white content. A side on the edge
    // of its screen gets no strip: the screen's edge already marks it, and the strip would show on the next monitor.
    class FrameEdge : FloatWindow
    {
        readonly int side;     // 0 top, 1 bottom, 2 left, 3 right
        readonly int t, mark, k;
        readonly int lead, trail; // top and bottom strips: how far they reach past the area on the left and the right

        FrameEdge(Rectangle r, int side, int t, int mark, int k, int lead, int trail)
        {
            this.side = side;
            this.t = t;
            this.mark = mark;
            this.k = k;
            this.lead = lead;
            this.trail = trail;
            SetSize(r.Size);
            JumpTo(r.X, r.Y);
            ShowQuiet();
            alpha.Go(1, 180, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override bool PerPixel { get { return true; } }
        protected override bool Rounded { get { return false; } }

        // Recording stopped: a quick fade, then gone (saving goes on without it).
        public void Dismiss()
        {
            if (IsDisposed) return;
            alpha.Go(0, 120, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            Anim.Wake(this);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: clicks pass through
                return cp;
            }
        }

        protected override void PaintSurface(WM.DrawingContext dc, int w, int h)
        {
            WM.Brush line = Ds.Brush(Ds.Argb(0.6, 255, 255, 255)), halo = Ds.Brush(Ds.Argb(0.3, 0, 0, 0));
            WM.Brush white = Ds.Brush(Ds.Rgb(255, 255, 255)), edge = Ds.Brush(Ds.Argb(0.34, 0, 0, 0));
            // The line hugs the recorded area; the strip is t wide so the corner marks (k thick, o from the outside) fit.
            int o = t - k;
            if (side < 2)
            {
                bool top = side == 0;
                double ly = top ? t - 1 : 0, my = top ? o : 0, ey = top ? o - 1 : 0;
                dc.DrawRectangle(halo, null, new System.Windows.Rect(0, top ? ly - 1 : ly + 1, w, 1));
                dc.DrawRectangle(line, null, new System.Windows.Rect(lead, ly, w - lead - trail, 1));
                // Each mark covers the corner square when there is a side strip to meet, else it starts at the area.
                int a0 = Math.Max(0, lead - k), a1 = lead + mark, b0 = w - trail - mark, b1 = Math.Min(w, w - trail + k);
                int e0 = Math.Max(0, a0 - 1), e1 = Math.Min(w, b1 + 1);
                dc.DrawRectangle(edge, null, new System.Windows.Rect(e0, ey, a1 + 1 - e0, k + 1));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(b0 - 1, ey, e1 - b0 + 1, k + 1));
                dc.DrawRectangle(white, null, new System.Windows.Rect(a0, my, a1 - a0, k));
                dc.DrawRectangle(white, null, new System.Windows.Rect(b0, my, b1 - b0, k));
            }
            else
            {
                bool left = side == 2;
                double lx = left ? t - 1 : 0, mx = left ? o : 0, ex = left ? o - 1 : 0;
                dc.DrawRectangle(halo, null, new System.Windows.Rect(left ? lx - 1 : lx + 1, 0, 1, h));
                dc.DrawRectangle(line, null, new System.Windows.Rect(lx, 0, 1, h));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(ex, 0, k + 1, mark + 1));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(ex, h - mark - 1, k + 1, mark + 1));
                dc.DrawRectangle(white, null, new System.Windows.Rect(mx, 0, k, mark));
                dc.DrawRectangle(white, null, new System.Windows.Rect(mx, h - mark, k, mark));
            }
        }

        public static FrameEdge[] Around(Rectangle a)
        {
            float s = ShotStack.ScaleFor(Screen.FromRectangle(a));
            int t = Math.Max(4, (int)Math.Round(4 * s)), mark = (int)Math.Round(18 * s), k = Math.Max(3, Math.Min(t - 1, (int)Math.Round(3 * s)));
            bool top = !OnScreenEdge(a, 0), bottom = !OnScreenEdge(a, 1), left = !OnScreenEdge(a, 2), right = !OnScreenEdge(a, 3);
            int l = left ? t : 0, r = right ? t : 0;
            List<FrameEdge> strips = new List<FrameEdge>();
            if (top) strips.Add(new FrameEdge(new Rectangle(a.X - l, a.Y - t, a.Width + l + r, t), 0, t, mark, k, l, r));
            if (bottom) strips.Add(new FrameEdge(new Rectangle(a.X - l, a.Bottom, a.Width + l + r, t), 1, t, mark, k, l, r));
            if (left) strips.Add(new FrameEdge(new Rectangle(a.X - t, a.Y, t, a.Height), 2, t, mark, k, 0, 0));
            if (right) strips.Add(new FrameEdge(new Rectangle(a.Right, a.Y, t, a.Height), 3, t, mark, k, 0, 0));
            return strips.ToArray();
        }

        // Whether that side of the area lies on (or past) the edge of the screen its middle is on.
        static bool OnScreenEdge(Rectangle a, int side)
        {
            Point p = side == 0 ? new Point(a.X + a.Width / 2, a.Y) : side == 1 ? new Point(a.X + a.Width / 2, a.Bottom - 1)
                    : side == 2 ? new Point(a.X, a.Y + a.Height / 2) : new Point(a.Right - 1, a.Y + a.Height / 2);
            Rectangle b = Screen.FromPoint(p).Bounds;
            if (!b.Contains(p)) return false;
            return side == 0 ? a.Top <= b.Top : side == 1 ? a.Bottom >= b.Bottom : side == 2 ? a.Left <= b.Left : a.Right >= b.Right;
        }
    }
}
