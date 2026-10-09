// Stackshot - Camera bubble for screen recordings.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using WM = System.Windows.Media;

namespace Stackshot
{
    // A DirectShow camera as FFmpeg lists it.
    public class CameraInfo
    {
        public string Name;     // friendly name, may repeat
        public string AltName;  // unique "@device_..." name, null when FFmpeg does not print it
        public string Label;    // Name, or "Name (2)" when several cameras share it; valid as settings.WebcamDevice

        // The -i argument. The unique name is immune to repeated names, accents, quotes and ':' (which dshow splits on).
        public string Input
        {
            get
            {
                string id = !string.IsNullOrEmpty(AltName) ? AltName : Name.Replace("\"", "");
                int tail = 0;
                while (tail < id.Length && id[id.Length - 1 - tail] == '\\') tail++;
                return "video=\"" + id + new string('\\', tail) + "\""; // doubled so they do not escape the closing quote
            }
        }

        public override string ToString() { return Label; }
    }

    // One capture format a camera offers.
    public class CameraMode
    {
        public string PixelFormat;  // raw formats (yuyv422, nv12...)
        public string Codec;        // compressed formats (mjpeg...)
        public int Width, Height;
        public double MinFps, MaxFps;
        public string MaxFpsText;   // as FFmpeg printed it: dshow compares frame intervals, so it goes back verbatim

        public bool Raw { get { return PixelFormat != null; } }

        // About 30 fps is plenty for the bubble.
        public string Rate { get { return MaxFps <= 31 ? MaxFpsText : MinFps <= 30 ? "30" : MaxFpsText; } }

        // Fixed high-rate modes are thinned to 30 fps before scaling.
        public bool Thin { get { return MaxFps > 31 && MinFps > 30; } }

        public string Args
        {
            get
            {
                return "-video_size " + Width + "x" + Height + " -framerate " + Rate +
                       (Raw ? " -pixel_format " + PixelFormat : " -vcodec " + Codec + " -threads 1");
            }
        }

        public override string ToString() { return Width + "x" + Height + " " + (Raw ? PixelFormat : Codec) + " a " + Rate + " fps"; }
    }

    // Camera discovery through FFmpeg: device list, capture formats (probed once per camera and session) and the capture command.
    public static class Webcam
    {
        static readonly object cacheLock = new object();
        static readonly Dictionary<string, CameraMode> modes = new Dictionary<string, CameraMode>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> broken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Regex OptionLine = new Regex(@"(pixel_format|vcodec)=(\S+)\s+min s=(\d+)x(\d+) fps=(\S+)\s+max s=(\d+)x(\d+) fps=(\S+)",
                                                     RegexOptions.CultureInvariant);

        // Labels of the connected cameras, valid as settings.WebcamDevice. Takes about a second: call it off the UI thread.
        public static List<string> ListDevices(string ffmpeg)
        {
            List<string> list = new List<string>();
            foreach (CameraInfo c in ListCameras(ffmpeg)) list.Add(c.Label);
            return list;
        }

        public static List<CameraInfo> ListCameras(string ffmpeg)
        {
            string text = RunFfmpeg(ffmpeg, "-hide_banner -nostdin -list_devices true -f dshow -i dummy", 6000);
            return text == null ? new List<CameraInfo>() : ParseDevices(text);
        }

        // Video devices from "-list_devices". Old layout: "DirectShow video devices" / "audio devices" sections with bare "Name"
        // lines. Newer one: "Name" (video), (audio), (video, audio) or (none) on each line. Either may be followed by
        // Alternative name "@device_...".
        public static List<CameraInfo> ParseDevices(string text)
        {
            List<CameraInfo> list = new List<CameraInfo>();
            int section = 0;        // 0 unknown, 1 video, 2 audio
            bool audioSeen = false;
            CameraInfo cur = null;
            int kind = 0;           // of cur: 1 video, 2 audio, 3 decide by its alternative name
            bool curAfterAudio = false;
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = StripPrefix(raw);
                if (line.Length == 0) continue;
                bool videoHeader = line.StartsWith("DirectShow video devices", StringComparison.OrdinalIgnoreCase);
                bool audioHeader = line.StartsWith("DirectShow audio devices", StringComparison.OrdinalIgnoreCase);
                if (videoHeader || audioHeader || line[0] == '"')
                {
                    Keep(list, cur, kind, curAfterAudio);
                    cur = null;
                }
                if (videoHeader || audioHeader)
                {
                    section = videoHeader ? 1 : 2;
                    if (audioHeader) audioSeen = true;
                    continue;
                }
                if (line.StartsWith("Alternative name", StringComparison.OrdinalIgnoreCase))
                {
                    int a = line.IndexOf('"'), b = line.LastIndexOf('"');
                    if (cur != null && a >= 0 && b > a + 1) cur.AltName = line.Substring(a + 1, b - a - 1);
                    continue;
                }
                if (line[0] != '"') continue;
                int end = line.LastIndexOf('"');
                if (end <= 1) continue;
                cur = new CameraInfo();
                cur.Name = line.Substring(1, end - 1);
                curAfterAudio = audioSeen;
                string type = line.Substring(end + 1).Trim().ToLowerInvariant();
                if (type.Length == 0) kind = section == 0 ? 3 : section;
                else if (type.Contains("video")) kind = 1;
                else if (type.Contains("audio")) { kind = 2; audioSeen = true; }
                else kind = 3;
            }
            Keep(list, cur, kind, curAfterAudio);
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CameraInfo c in list)
            {
                int n;
                seen.TryGetValue(c.Name, out n);
                seen[c.Name] = ++n;
                c.Label = n == 1 ? c.Name : c.Name + " (" + n + ")";
            }
            return list;
        }

        static void Keep(List<CameraInfo> list, CameraInfo c, int kind, bool afterAudio)
        {
            if (c == null || c.Name.Length == 0) return;
            bool video = kind == 1 || (kind == 3 && (c.AltName != null ? VideoMoniker(c.AltName) : !afterAudio));
            if (!video) return;
            // A device in both categories shows up twice with the same unique name.
            if (c.AltName != null)
                foreach (CameraInfo o in list) if (string.Equals(o.AltName, c.AltName, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(c);
        }

        // Unique names carry the DirectShow category: video input {860BB310...}, WDM capture devices (@device_pnp_), audio {33D9A762...}.
        static bool VideoMoniker(string alt)
        {
            string a = alt.ToLowerInvariant();
            if (a.Contains("33d9a762") || a.Contains("6994ad04")) return false;
            return a.StartsWith("@device_pnp_") || a.Contains("860bb310") || a.Contains("65e8773d") || a.Contains("6994ad05");
        }

        // Drops FFmpeg's "[dshow @ 0000...]" / "[in#0 @ 0000...]" context prefix.
        static string StripPrefix(string raw)
        {
            string line = raw.Trim();
            if (line.StartsWith("["))
            {
                int close = line.IndexOf(']');
                if (close > 0 && line.Substring(0, close).Contains(" @ ")) line = line.Substring(close + 1).Trim();
            }
            return line;
        }

        // The camera a saved setting refers to: unique name, label, name; null when it is not connected (or the setting is empty).
        public static CameraInfo Find(List<CameraInfo> cams, string setting)
        {
            if (string.IsNullOrEmpty(setting) || cams == null) return null;
            string s = setting.Trim();
            foreach (CameraInfo c in cams) if (c.AltName != null && string.Equals(c.AltName, s, StringComparison.OrdinalIgnoreCase)) return c;
            foreach (CameraInfo c in cams) if (c.Label == s) return c;
            foreach (CameraInfo c in cams) if (c.Name == s) return c;
            foreach (CameraInfo c in cams) if (string.Equals(c.Label, s, StringComparison.OrdinalIgnoreCase)) return c;
            string bare = Bare(s);
            foreach (CameraInfo c in cams) if (string.Equals(Bare(c.Name), bare, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        static string Bare(string s) { return s.Replace("\"", "").Trim().TrimEnd('\\'); }

        // Cameras to try, in order: the chosen one, then the rest as FFmpeg lists them, those that failed this session last.
        public static List<CameraInfo> Order(List<CameraInfo> cams, string setting)
        {
            CameraInfo chosen = Find(cams, setting);
            List<CameraInfo> good = new List<CameraInfo>(), bad = new List<CameraInfo>();
            if (chosen != null) good.Add(chosen);
            lock (cacheLock)
                foreach (CameraInfo c in cams)
                    if (c != chosen) (broken.Contains(Key(c)) ? bad : good).Add(c);
            good.AddRange(bad);
            return good;
        }

        internal static void MarkBroken(CameraInfo cam, bool failed)
        {
            lock (cacheLock) { if (failed) broken.Add(Key(cam)); else broken.Remove(Key(cam)); }
        }

        static string Key(CameraInfo cam) { return cam.AltName ?? ("name:" + cam.Name); }

        // Capture mode for a camera, probed with -list_options the first time; null = use the camera's default format.
        public static CameraMode ModeFor(string ffmpeg, CameraInfo cam)
        {
            string key = Key(cam);
            CameraMode mode;
            lock (cacheLock) if (modes.TryGetValue(key, out mode)) return mode;
            string text = RunFfmpeg(ffmpeg, "-hide_banner -nostdin -list_options true -f dshow -i " + cam.Input, 10000);
            if (text == null) return null;
            List<CameraMode> all = ParseOptions(text);
            mode = Pick(all);
            if (mode != null) ShotStack.Log("C\u00E1mara: \u00AB" + cam.Label + "\u00BB ofrece " + all.Count + " formatos; se usa " + mode);
            else ShotStack.Log("C\u00E1mara: no se pueden leer los formatos de \u00AB" + cam.Label + "\u00BB; se usa el suyo por defecto (" + Tail(text, 200) + ")");
            // A probe that could not even open the device (busy, still waking up) is repeated next time.
            if (mode != null || text.IndexOf("device options", StringComparison.OrdinalIgnoreCase) >= 0)
                lock (cacheLock) modes[key] = mode;
            return mode;
        }

        // The probed mode did not open but the default format did: stop asking for it this session.
        internal static void ForgetMode(CameraInfo cam)
        {
            lock (cacheLock) modes[Key(cam)] = null;
        }

        // Formats from "-list_options", e.g. "pixel_format=yuyv422  min s=640x480 fps=5 max s=640x480 fps=30" or
        // "vcodec=mjpeg  min s=1280x720 fps=30 max s=1280x720 fps=30 (pc, bt470bg/bt709/unknown, center)".
        public static List<CameraMode> ParseOptions(string text)
        {
            List<CameraMode> list = new List<CameraMode>();
            foreach (Match m in OptionLine.Matches(text ?? ""))
            {
                int w0, h0, w1, h1;
                double lo, hi;
                if (!int.TryParse(m.Groups[3].Value, out w0) || !int.TryParse(m.Groups[4].Value, out h0) ||
                    !int.TryParse(m.Groups[6].Value, out w1) || !int.TryParse(m.Groups[7].Value, out h1)) continue;
                if (!Fps(m.Groups[5].Value, out lo) || !Fps(m.Groups[8].Value, out hi)) continue;
                string loText = m.Groups[5].Value, hiText = m.Groups[8].Value;
                if (lo > hi) { double t = lo; lo = hi; hi = t; hiText = loText; }
                bool raw = m.Groups[1].Value == "pixel_format";
                string fmt = m.Groups[2].Value;
                AddMode(list, raw, fmt, w1, h1, lo, hi, hiText);
                if (w0 != w1 || h0 != h1) AddMode(list, raw, fmt, w0, h0, lo, hi, hiText);
            }
            return list;
        }

        static void AddMode(List<CameraMode> list, bool raw, string fmt, int w, int h, double lo, double hi, string hiText)
        {
            if (w < 16 || h < 16 || w > 16384 || h > 16384) return;
            CameraMode m = new CameraMode();
            if (raw) m.PixelFormat = fmt; else m.Codec = fmt;
            m.Width = w;
            m.Height = h;
            m.MinFps = lo;
            m.MaxFps = hi;
            m.MaxFpsText = hiText;
            list.Add(m);
        }

        static bool Fps(string s, out double v)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0.1 && v <= 240;
        }

        // Smooth (>= 24 fps), at most 1280x720 and sharp enough for the bubble; then raw over MJPEG (no decoding), then the
        // smallest mode with a short side of 480+ to keep CPU low. Other codecs (H.264...) are left to the default format.
        public static CameraMode Pick(List<CameraMode> list)
        {
            CameraMode best = null;
            long[] bestRank = null;
            foreach (CameraMode m in list)
            {
                if (!m.Raw && !string.Equals(m.Codec, "mjpeg", StringComparison.OrdinalIgnoreCase)) continue;
                long[] r = Rank(m);
                if (bestRank == null || Compare(r, bestRank) > 0) { best = m; bestRank = r; }
            }
            return best;
        }

        static long[] Rank(CameraMode m)
        {
            long area = (long)m.Width * m.Height;
            int shortSide = Math.Min(m.Width, m.Height);
            bool fits = m.Width <= 1280 && m.Height <= 1280 && area <= 1280L * 720;
            bool sharp = shortSide >= 360;
            long size = fits && sharp ? (shortSide >= 480 ? (1L << 40) - area : area) : fits ? area : (1L << 40) - area;
            long rate = m.MaxFps >= 29 && m.MaxFps <= 31 ? 2 : m.MaxFps > 31 ? 1 : 0;
            return new long[] { m.MaxFps >= 23.9 ? 1 : 0, fits && sharp ? 1 : 0, m.Raw ? 1 : 0, size, rate };
        }

        static int Compare(long[] a, long[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return a[i] > b[i] ? 1 : -1;
            return 0;
        }

        // The bubble's FFmpeg command: the camera cropped square, mirrored and scaled to side x side, as raw BGRA on stdout.
        public static string CaptureArgs(CameraInfo cam, CameraMode mode, int side)
        {
            string thin = mode != null && mode.Thin ? "fps=30," : "";
            return "-hide_banner -nostdin -loglevel error -f dshow -rtbufsize 64M " + (mode != null ? mode.Args + " " : "") + "-i " + cam.Input +
                   " -vf \"" + thin + "crop=min(iw\\,ih):min(iw\\,ih),scale=" + side + ":" + side + ":flags=bicubic,hflip\"" +
                   " -pix_fmt bgra -f rawvideo -";
        }

        // Runs FFmpeg with a time limit (a stuck DirectShow driver must not block forever) and returns its stderr, read as UTF-8
        // because device names can have accents; null if it could not start.
        static string RunFfmpeg(string ffmpeg, string args, int ms)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    StringBuilder sb = new StringBuilder();
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.Start();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(ms)) { try { p.Kill(); } catch { } }
                    if (p.WaitForExit(1000)) p.WaitForExit();
                    lock (sb) return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                ShotStack.Log("C\u00E1mara: " + ex.Message);
                return null;
            }
        }

        internal static string Tail(string text, int max)
        {
            string t = (text ?? "").Trim().Replace("\r", "").Replace('\n', ' ');
            return t.Length > max ? "\u2026" + t.Substring(t.Length - max) : t;
        }
    }

    // Loom-style round or square bubble with the webcam, placed inside the recorded area so it ends up in the video
    // (it is the one Stackshot window that is not excluded from capture). FFmpeg reads the camera through DirectShow,
    // crops it square, mirrors it and pipes raw BGRA; a background thread keeps only the latest frame. Drag to move (it
    // settles on the corners' margins and is remembered for the next recording), double-click to switch round/square,
    // right-click to change the size. It fades in when it appears and out when the recording stops.
    public class WebcamBubble : Form
    {
        static readonly int[] Sizes = { 150, 210, 280 };
        const double FadeInMs = 220, FadeOutMs = 160;
        internal static int OpenTimeoutMs = 12000;  // to the first frame: some USB cameras take seconds to wake up
        internal static int StallTimeoutMs = 5000;  // without frames once live: the camera is gone
        internal static int ReconnectMs = 10000;    // how long to keep looking for a lost camera

        enum Outcome { Closed, Failed, Lost }

        // Where the user left the bubble in this run: the nearest sides of the area and the distance to them (at 100 %).
        static bool placed, fromRight, fromBottom;
        static float offX, offY;

        readonly Settings settings;
        readonly string ffmpeg;
        readonly Rectangle area;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly object gate = new object();
        Process proc;
        byte[] latest;              // newest camera frame (BGRA, latestSide square), handed over under gate
        int latestSide;
        bool fresh;
        volatile bool closing;
        byte[] image;               // the frame on screen, and its side
        int imageSide;
        Dib dib;
        float s;
        int D, M, W, wx, wy, measured;
        string status = "Conectando la c\u00E1mara\u2026";
        bool down, dragged, live, dismissing;
        volatile bool gaveUp;
        double shownAt = double.MaxValue, dismissAt, dismissFrom = 1;
        Point grab, downAt;
        byte[] shade, still;        // cached pixels: the shadow alone, and the whole bubble without camera image
        string shadeKey, stillKey, maskKey;
        byte[] mask, rim, work;     // shape coverage and rim alpha (D x D), and the frame being composed
        Bitmap scaleFrom;           // only when the bubble changed size after the camera started
        Dib scaleTo;
        byte[] scaled;

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
            Place();
            Bounds = new Rectangle(wx, wy, W, W);
            timer.Interval = 31;
            timer.Tick += delegate { Tick(); };
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // Camera pixels per side: FFmpeg delivers the bubble's own size, so a frame reaches the screen unscaled.
        int FrameSide() { return Math.Max(64, Math.Min(1080, D)); }

        // The bubble never takes more than a third of the recorded area's short side.
        void Measure()
        {
            measured = settings.WebcamSize;
            D = Math.Max(P(72), Math.Min(P(Sizes[Math.Max(0, Math.Min(2, measured))]), Math.Min(area.Width, area.Height) / 3));
            W = D + 2 * M;
            if (dib != null) dib.Dispose();
            dib = new Dib(W, W, true);
        }

        // Bottom-left corner of the recording, like Loom, or where it was left last time.
        void Place()
        {
            int ox = P(placed ? offX : 24), oy = P(placed ? offY : 24);
            wx = placed && fromRight ? area.Right - ox - D - M : area.X + ox - M;
            wy = !placed || fromBottom ? area.Bottom - oy - D - M : area.Y + oy - M;
            Clamp();
        }

        void Remember()
        {
            int left = wx + M - area.X, right = area.Right - (wx + M + D), top = wy + M - area.Y, bottom = area.Bottom - (wy + M + D);
            fromRight = right < left;
            fromBottom = bottom <= top;
            offX = Math.Max(0, fromRight ? right : left) / s;
            offY = Math.Max(0, fromBottom ? bottom : top) / s;
            placed = true;
        }

        // A new size keeps the bubble's center where it was.
        void Rescale()
        {
            int cx = wx + W / 2, cy = wy + W / 2;
            Measure();
            wx = cx - W / 2;
            wy = cy - W / 2;
            Clamp();
            Render();
        }

        void Tick()
        {
            if (IsDisposed) return;
            if (dismissing && Anim.Now - dismissAt >= FadeOutMs) { Close(); return; }
            if (settings.WebcamSize != measured && !dismissing) { Rescale(); return; } // changed in Settings meanwhile
            if (fresh || dismissing || Anim.Now - shownAt < FadeInMs + 40) Render();
        }

        // Recording stopped: the camera is let go at once and the bubble fades out.
        // Live: a real camera frame is on screen. GaveUp: the camera could not be opened (the bubble says why).
        public bool Live { get { return live && !dismissing; } }
        public bool GaveUp { get { return gaveUp; } }

        public void Dismiss()
        {
            if (IsDisposed || dismissing) return;
            if (!IsHandleCreated || !Visible) { Close(); return; }
            closing = true;
            dismissFrom = Visibility(); // stopped while still fading in: fade out from there, never back to opaque
            dismissing = true;
            dismissAt = Anim.Now;
            Process p;
            lock (gate) { p = proc; proc = null; }
            if (p != null) Kill(p);
            timer.Start();
        }

        double Visibility()
        {
            double now = Anim.Now;
            double a = dismissing ? dismissFrom * (1 - Ease.OutCubic(Math.Min(1, (now - dismissAt) / FadeOutMs)))
                                  : Ease.OutCubic(Math.Max(0, Math.Min(1, (now - shownAt) / FadeInMs)));
            return Math.Max(0, Math.Min(1, a));
        }

        byte FadeAlpha() { return (byte)Math.Round(Visibility() * 255); }

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
            shownAt = Anim.Now;
            Render();
            timer.Start();
            Thread t = new Thread(Run);
            t.IsBackground = true;
            t.Start();
        }

        public static List<string> ListDevices(string ffmpeg) { return Webcam.ListDevices(ffmpeg); }

        void Run()
        {
            try { Feed(); }
            catch (Exception ex)
            {
                if (closing) return;
                ShotStack.Log("C\u00E1mara: " + ex.Message);
                Fail("No se puede abrir la c\u00E1mara");
            }
        }

        // Background thread: find the camera, start FFmpeg and hand over frames until closed. The chosen camera goes first;
        // if it is missing or does not open, the others are tried in turn.
        void Feed()
        {
            List<CameraInfo> cams = Webcam.ListCameras(ffmpeg);
            if (cams.Count == 0 && !closing)
            {
                Pause(1500); // a camera that is waking up or was just plugged in may not be listed yet
                cams = Webcam.ListCameras(ffmpeg);
            }
            if (closing) return;
            if (cams.Count == 0)
            {
                ShotStack.Log("C\u00E1mara: FFmpeg no encuentra ninguna c\u00E1mara");
                Fail("No se encuentra ninguna c\u00E1mara");
                return;
            }
            string wanted = settings.WebcamDevice;
            List<CameraInfo> order = Webcam.Order(cams, wanted);
            if (!string.IsNullOrEmpty(wanted) && Webcam.Find(cams, wanted) == null)
                ShotStack.Log("C\u00E1mara: \u00AB" + wanted + "\u00BB no est\u00E1 conectada; se prueba con \u00AB" + order[0].Label + "\u00BB");
            foreach (CameraInfo cam in order)
            {
                string why;
                Outcome o = Open(cam, out why);
                if (o == Outcome.Closed) return;
                if (o == Outcome.Lost) { Reconnect(cam, why); return; }
            }
            if (!closing) Fail("No se puede abrir la c\u00E1mara");
        }

        // The probed mode first, then the camera's default format (what older versions always used).
        Outcome Open(CameraInfo cam, out string why)
        {
            CameraMode mode = Webcam.ModeFor(ffmpeg, cam);
            bool started;
            if (closing) { why = null; return Outcome.Closed; }
            if (mode != null)
            {
                Outcome o = Pump(cam, mode, out why, out started);
                if (o != Outcome.Failed) return o;
                ShotStack.Log("C\u00E1mara: \u00AB" + cam.Label + "\u00BB no abre en " + mode + " (" + why + "); se prueba con su formato por defecto");
            }
            Outcome d = Pump(cam, null, out why, out started);
            if (d == Outcome.Failed)
            {
                Webcam.MarkBroken(cam, true);
                ShotStack.Log("C\u00E1mara: no se puede abrir \u00AB" + cam.Label + "\u00BB: " + why);
            }
            else if (mode != null && started) Webcam.ForgetMode(cam);
            return d;
        }

        // Runs one FFmpeg capture until the bubble closes or frames stop. A watchdog kills FFmpeg when the first frame is late
        // or frames stall, which DirectShow does not always report. When the bubble grows past the frames' size, the camera
        // is opened again at the new size (the last frame stays meanwhile), so it is never upscaled for the rest of the
        // recording; a smaller bubble just scales the frames down.
        Outcome Pump(CameraInfo cam, CameraMode mode, out string why, out bool started)
        {
            why = null;
            started = false;
            while (true)
            {
                bool grown;
                Outcome o = PumpOnce(cam, mode, ref why, ref started, out grown);
                if (!grown) return o;
                ShotStack.Log("C\u00E1mara: \u00AB" + cam.Label + "\u00BB se abre de nuevo a " + FrameSide() + " px");
            }
        }

        Outcome PumpOnce(CameraInfo cam, CameraMode mode, ref string why, ref bool started, out bool grown)
        {
            grown = false;
            int side = FrameSide(), n = side * side * 4;
            StringBuilder errors = new StringBuilder();
            Process p = new Process();
            p.StartInfo.FileName = ffmpeg;
            p.StartInfo.Arguments = Webcam.CaptureArgs(cam, mode, side);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                if (e.Data != null) lock (errors) { if (errors.Length < 2000) errors.AppendLine(e.Data); }
            };
            try { p.Start(); }
            catch (Exception ex) { p.Dispose(); why = ex.Message; return Outcome.Failed; }
            p.BeginErrorReadLine();
            lock (gate)
            {
                if (closing) { Kill(p); p.Dispose(); return Outcome.Closed; }
                proc = p;
            }
            long begin = Stopwatch.GetTimestamp();
            long last = 0;   // time of the latest frame, 0 until the first
            int late = 0;
            System.Threading.Timer dog = new System.Threading.Timer(delegate
            {
                long t = Interlocked.Read(ref last);
                long ms = (Stopwatch.GetTimestamp() - (t == 0 ? begin : t)) * 1000 / Stopwatch.Frequency;
                if (ms > (t == 0 ? OpenTimeoutMs : StallTimeoutMs)) { Interlocked.Exchange(ref late, 1); Kill(p); }
            }, null, 500, 500);
            try
            {
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
                    Interlocked.Exchange(ref last, Stopwatch.GetTimestamp());
                    if (!started)
                    {
                        started = true;
                        Webcam.MarkBroken(cam, false);
                        ShotStack.Log("C\u00E1mara: \u00AB" + cam.Label + "\u00BB \u00B7 " + (mode != null ? mode.ToString() : "formato por defecto"));
                    }
                    lock (gate)
                    {
                        byte[] spare = latest != null && latest.Length == n ? latest : null;
                        latest = buf;
                        latestSide = side;
                        fresh = true;
                        buf = spare ?? new byte[n];
                    }
                    if (FrameSide() > side) { grown = true; break; }
                }
            }
            catch (Exception ex) { why = ex.Message; }
            dog.Dispose();
            lock (gate) { if (proc == p) proc = null; }
            Kill(p);
            try { if (p.WaitForExit(2000)) p.WaitForExit(); } catch { }
            try { p.Dispose(); } catch { }
            if (closing) return Outcome.Closed;
            if (grown) return Outcome.Lost;
            string err;
            lock (errors) err = errors.ToString();
            if (Volatile.Read(ref late) != 0)
                why = started ? "sin imagen durante " + StallTimeoutMs / 1000 + " s" : "no responde en " + OpenTimeoutMs / 1000 + " s";
            else if (err.Trim().Length > 0) why = Webcam.Tail(err, 300);
            else if (why == null) why = "FFmpeg se ha cerrado";
            return started ? Outcome.Lost : Outcome.Failed;
        }

        // The camera stopped mid-recording (unplugged, asleep, taken by another app): look for it again for a while.
        void Reconnect(CameraInfo cam, string why)
        {
            ShotStack.Log("C\u00E1mara: se ha perdido \u00AB" + cam.Label + "\u00BB (" + why + "); reconectando");
            Stopwatch gone = Stopwatch.StartNew();
            while (!closing && gone.ElapsedMilliseconds < ReconnectMs)
            {
                Status("Reconectando la c\u00E1mara\u2026");
                Pause(1000);
                if (closing) return;
                List<CameraInfo> cams = Webcam.ListCameras(ffmpeg);
                CameraInfo again = Webcam.Find(cams, cam.AltName ?? cam.Label);
                if (again == null || closing) continue;
                string w;
                Outcome o = Open(again, out w);
                if (o == Outcome.Closed) return;
                if (o == Outcome.Lost)
                {
                    cam = again;
                    gone.Restart();
                    ShotStack.Log("C\u00E1mara: se ha vuelto a perder \u00AB" + cam.Label + "\u00BB (" + w + "); reconectando");
                }
            }
            if (closing) return;
            ShotStack.Log("C\u00E1mara: \u00AB" + cam.Label + "\u00BB no ha vuelto");
            Fail("La c\u00E1mara se ha desconectado");
        }

        void Pause(int ms)
        {
            for (int i = 0; i < ms && !closing; i += 100) Thread.Sleep(100);
        }

        static void Kill(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        // Shows a message in the bubble while it keeps trying.
        void Status(string text)
        {
            if (closing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (closing) return;
                    lock (gate) fresh = false;
                    live = false;
                    status = text;
                    Render();
                });
            }
            catch { }
        }

        // Shows the reason for a few seconds, then gets out of the way.
        void Fail(string why)
        {
            gaveUp = true;
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
                    t.Tick += delegate { t.Dispose(); if (!closing) Dismiss(); };
                    t.Start();
                });
            }
            catch { }
        }

        // Live: the cached shadow with the camera image laid into the shape through a cached coverage mask, plus a faint
        // rim. Without an image: the cached placeholder. Either way one premultiplied buffer goes to the screen.
        void Render()
        {
            if (IsDisposed || dib == null) return;
            lock (gate)
            {
                if (fresh && latest != null)
                {
                    if (image == null || image.Length != latest.Length) image = new byte[latest.Length];
                    Buffer.BlockCopy(latest, 0, image, 0, latest.Length);
                    imageSide = latestSide;
                    fresh = false;
                    live = true;
                }
            }
            bool round = settings.Webcam != 2;
            Native.GdiFlush();
            if (live && image != null) Compose(round);
            else
            {
                byte[] bg = Still(round);
                Marshal.Copy(bg, 0, dib.Bits, bg.Length);
            }
            if (IsHandleCreated) Present();
        }

        // About 0.3 ms for a medium bubble: straight byte work, no GDI+ texture fill (which cost several ms per frame).
        void Compose(bool round)
        {
            byte[] bg = Shade(round);
            Masks(round);
            byte[] src = imageSide == D ? image : Scaled();
            if (work == null || work.Length != bg.Length) work = new byte[bg.Length];
            Buffer.BlockCopy(bg, 0, work, 0, bg.Length);
            for (int y = 0; y < D; y++)
            {
                int i = y * D, o = ((y + M) * W + M) * 4;
                for (int x = 0; x < D; x++, i++, o += 4)
                {
                    int m = mask[i];
                    if (m == 0) continue;
                    int c = i * 4;
                    if (m == 255)
                    {
                        work[o] = src[c];
                        work[o + 1] = src[c + 1];
                        work[o + 2] = src[c + 2];
                        work[o + 3] = 255;
                    }
                    else
                    {
                        int k = 255 - m;
                        work[o] = (byte)((src[c] * m + work[o] * k + 127) / 255);
                        work[o + 1] = (byte)((src[c + 1] * m + work[o + 1] * k + 127) / 255);
                        work[o + 2] = (byte)((src[c + 2] * m + work[o + 2] * k + 127) / 255);
                        work[o + 3] = (byte)((255 * m + work[o + 3] * k + 127) / 255);
                    }
                    int r = rim[i];
                    if (r != 0)
                    {
                        int k = 255 - r;
                        work[o] = (byte)(r + (work[o] * k + 127) / 255);
                        work[o + 1] = (byte)(r + (work[o + 1] * k + 127) / 255);
                        work[o + 2] = (byte)(r + (work[o + 2] * k + 127) / 255);
                        work[o + 3] = (byte)(r + (work[o + 3] * k + 127) / 255);
                    }
                }
            }
            Marshal.Copy(work, 0, dib.Bits, work.Length);
        }

        // Antialiased coverage of the shape and of its rim (white, inside the edge), once per size and shape.
        void Masks(bool round)
        {
            string key = D + "|" + round;
            if (mask != null && maskKey == key) return;
            mask = Coverage(round, false);
            rim = Coverage(round, true);
            maskKey = key;
        }

        byte[] Coverage(bool round, bool edge)
        {
            byte[] a = new byte[D * D];
            using (Dib d = new Dib(D, D, true))
            {
                using (Graphics g = d.Graphics())
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    RectangleF r = new RectangleF(0, 0, D, D);
                    if (edge)
                    {
                        float w = Math.Max(1f, 1.2f * s);
                        r.Inflate(-w / 2, -w / 2);
                        using (GraphicsPath p = Shape(r, round))
                        using (Pen pen = new Pen(Color.FromArgb(64, 255, 255, 255), w)) g.DrawPath(pen, p);
                    }
                    else
                        using (GraphicsPath p = Shape(r, round))
                        using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                }
                Native.GdiFlush();
                byte[] px = new byte[D * D * 4];
                Marshal.Copy(d.Bits, px, 0, px.Length);
                for (int i = 0; i < a.Length; i++) a[i] = px[i * 4 + 3];
            }
            return a;
        }

        // The bubble changed size after the camera started (its frames keep the old size): scale this one.
        byte[] Scaled()
        {
            if (scaleFrom == null || scaleFrom.Width != imageSide)
            {
                if (scaleFrom != null) scaleFrom.Dispose();
                scaleFrom = new Bitmap(imageSide, imageSide, PixelFormat.Format32bppRgb);
            }
            BitmapData bd = scaleFrom.LockBits(new Rectangle(0, 0, imageSide, imageSide), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try { Marshal.Copy(image, 0, bd.Scan0, Math.Min(image.Length, imageSide * imageSide * 4)); }
            finally { scaleFrom.UnlockBits(bd); }
            if (scaleTo == null || scaleTo.Width != D)
            {
                if (scaleTo != null) scaleTo.Dispose();
                scaleTo = new Dib(D, D);
            }
            using (Graphics g = scaleTo.Graphics())
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY); // no dark seam at the edges
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(scaleFrom, new Rectangle(0, 0, D, D), 0, 0, imageSide, imageSide, GraphicsUnit.Pixel, ia);
            }
            Native.GdiFlush();
            if (scaled == null || scaled.Length != D * D * 4) scaled = new byte[D * D * 4];
            Marshal.Copy(scaleTo.Bits, scaled, 0, scaled.Length);
            return scaled;
        }

        // Soft shadow under the shape, blurred once per size and shape.
        byte[] Shade(bool round)
        {
            string key = W + "|" + round;
            if (shade != null && shadeKey == key) return shade;
            shade = Pixels(ShadowImage(round));
            shadeKey = key;
            return shade;
        }

        WM.Imaging.BitmapSource ShadowImage(bool round)
        {
            return Ink.Shadow(W, W, new System.Windows.Rect(M, M + P(3), D, D), round ? D / 2.0 : D * 0.2, P(12), Ds.Argb(0.34, 0, 0, 0));
        }

        // The bubble while there is no camera image: a dark tile with a camera icon and the status.
        byte[] Still(bool round)
        {
            string key = W + "|" + round + "|" + status;
            if (still != null && stillKey == key) return still;
            WM.Imaging.BitmapSource shadow = ShadowImage(round);
            still = Pixels(Ink.Render(W, W, delegate(WM.DrawingContext dc)
            {
                dc.DrawImage(shadow, new System.Windows.Rect(0, 0, W, W));
                System.Windows.Rect r = new System.Windows.Rect(M, M, D, D);
                WM.Geometry shape = round ? (WM.Geometry)new WM.EllipseGeometry(r) : new WM.RectangleGeometry(r, D * 0.2, D * 0.2);
                dc.DrawGeometry(new WM.LinearGradientBrush(Ds.Rgb(60, 60, 66), Ds.Rgb(36, 36, 40), 90), null, shape);
                // Trying ("...") shows the camera; giving up shows it struck through. Icon and status are centered
                // together; a bubble too small to read the status shows a larger icon alone.
                WM.FormattedText t = StatusText(D * 0.72);
                double gs = Math.Round(D * (t != null ? 0.2 : 0.3)), gap = Math.Round(D * 0.08);
                double top = Math.Round(M + (D - gs - (t != null ? gap + t.Height : 0)) / 2);
                bool trying = status.EndsWith("\u2026");
                Glyph.Draw(dc, trying ? "video" : "videooff", M + (D - gs) / 2, top, gs, Ds.Argb(0.55, 255, 255, 255), Math.Max(1.5, gs / 15));
                if (t != null) dc.DrawText(t, new System.Windows.Point(M + D * 0.14, top + gs + gap));
                WM.Pen rim = new WM.Pen(Ds.Brush(Ds.Argb(0.2, 255, 255, 255)), Math.Max(1, 1.2 * s));
                rim.Freeze();
                r.Inflate(-0.5, -0.5);
                dc.DrawGeometry(null, rim, round ? (WM.Geometry)new WM.EllipseGeometry(r) : new WM.RectangleGeometry(r, D * 0.2 - 0.5, D * 0.2 - 0.5));
            }, null));
            stillKey = key;
            return still;
        }

        // The status as large as it fits: in two lines, else three, never cutting a word. Null when it would not be
        // readable (the bubble is tiny), since it is also in the video.
        WM.FormattedText StatusText(double width)
        {
            for (int lines = 2; lines <= 3; lines++)
                for (double px = Math.Max(P(11), D * 0.07); px >= P(9); px -= 0.5)
                {
                    WM.FormattedText t = Ink.Px(status, Ds.Medium, px, Ds.Argb(0.9, 255, 255, 255));
                    t.MaxTextWidth = width;
                    t.TextAlignment = System.Windows.TextAlignment.Center;
                    double line = Ink.Px("X", Ds.Medium, px, Ds.Argb(0.9, 255, 255, 255)).Height;
                    if (t.MinWidth <= width && t.Height <= line * lines + 0.5) return t;
                }
            return null;
        }

        static byte[] Pixels(WM.Imaging.BitmapSource b)
        {
            byte[] a = new byte[b.PixelWidth * b.PixelHeight * 4];
            b.CopyPixels(a, b.PixelWidth * 4, 0);
            return a;
        }

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int Cx, Cy; }
        [StructLayout(LayoutKind.Sequential)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT dst, ref SIZE size, IntPtr hdcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);

        // The buffer at (wx, wy) with the current fade.
        void Present()
        {
            POINT dst; dst.X = wx; dst.Y = wy;
            POINT src; src.X = 0; src.Y = 0;
            SIZE size; size.Cx = W; size.Cy = W;
            BLENDFUNCTION bf = new BLENDFUNCTION();
            bf.SourceConstantAlpha = FadeAlpha();
            bf.AlphaFormat = 1; // AC_SRC_ALPHA
            UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, dib.Dc, ref src, 0, ref bf, 2); // ULW_ALPHA
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

        // Near the corners' margins the bubble settles on them, so it lines up like Loom's.
        void Snap()
        {
            int g = P(24), near = P(10);
            int left = area.X + g - M, right = area.Right - g - D - M, top = area.Y + g - M, bottom = area.Bottom - g - D - M;
            if (Math.Abs(wx - left) < near) wx = left;
            else if (Math.Abs(wx - right) < near) wx = right;
            if (Math.Abs(wy - top) < near) wy = top;
            else if (Math.Abs(wy - bottom) < near) wy = bottom;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || dismissing) return;
            down = true;
            dragged = false;
            downAt = PointToScreen(e.Location);
            grab = new Point(downAt.X - wx, downAt.Y - wy);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = Cursors.SizeAll;
            if (!down || dismissing) return;
            Point c = PointToScreen(e.Location);
            if (!dragged && Math.Abs(c.X - downAt.X) + Math.Abs(c.Y - downAt.Y) < P(4)) return;
            dragged = true;
            wx = c.X - grab.X;
            wy = c.Y - grab.Y;
            Snap();
            Clamp();
            Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool moved = down && dragged;
            down = false;
            if (dismissing) return;
            if (moved) Remember();
            if (e.Button != MouseButtons.Right) return;
            settings.WebcamSize = (settings.WebcamSize + 1) % Sizes.Length;
            Rescale();
            Remember();
            settings.Save();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || dismissing) return;
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
            if (scaleFrom != null) scaleFrom.Dispose();
            if (scaleTo != null) scaleTo.Dispose();
            if (dib != null) { dib.Dispose(); dib = null; }
            base.OnFormClosed(e);
        }
    }
}
