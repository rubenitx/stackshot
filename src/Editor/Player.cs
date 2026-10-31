// Stackshot - Built-in video player of the editor: FFmpeg decodes picture and sound, winmm plays the sound, which is the clock.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Stackshot
{
    internal sealed class AudioTrackInfo
    {
        public int Index;
        public string Title;
        public bool Default;
    }

    // What FFmpeg says about a file ("ffmpeg -i" stderr), as plain data.
    internal sealed class MediaInfo
    {
        public double Duration, Fps;
        public int Width, Height;
        public int SarNum = 1, SarDen = 1;   // the shape of one pixel ("[SAR 4:3 DAR 16:9]" of an HDV camera); 1:1 square
        public string PixelFormat;
        public string ColorMatrix;   // as tagged ("bt709", "bt470bg", "smpte170m"...), null when the file does not say
        public bool FullRange;
        public string Raw;   // the text it was read from (the output side of the command left out)
        public readonly List<AudioTrackInfo> Audio = new List<AudioTrackInfo>();

        public bool HasVideo { get { return Width > 0 && Height > 0; } }

        // The size the picture is meant to be seen at: the stored one with non-square pixels made square, by widening
        // (or, for tall pixels, heightening) so no stored pixel is lost. 1440x1080 at SAR 4:3 is seen at 1920x1080.
        public System.Drawing.Size Display
        {
            get
            {
                if (SarNum <= 0 || SarDen <= 0 || SarNum == SarDen) return new System.Drawing.Size(Width, Height);
                if (SarNum > SarDen) return new System.Drawing.Size((int)Math.Round(Width * (double)SarNum / SarDen), Height);
                return new System.Drawing.Size(Width, (int)Math.Round(Height * (double)SarDen / SarNum));
            }
        }
        public bool CanPlay { get { return HasVideo && Fps > 0; } }

        // Probes kept a while (same file, size and time), so the window opened after a background probe doesn't
        // ask FFmpeg again on the UI thread.
        static readonly Dictionary<string, KeyValuePair<DateTime, MediaInfo>> recent = new Dictionary<string, KeyValuePair<DateTime, MediaInfo>>(StringComparer.OrdinalIgnoreCase);

        static string CacheKey(string path)
        {
            try { FileInfo f = new FileInfo(path); return f.Exists ? path + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks : null; }
            catch (Exception) { return null; }
        }

        // A failed probe (FFmpeg stuck, a drive gone) is only kept long enough for the window it opens, so trying the same
        // file again asks FFmpeg again.
        static bool Fresh(KeyValuePair<DateTime, MediaInfo> hit)
        {
            return DateTime.UtcNow - hit.Key < (hit.Value.HasVideo ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(5));
        }

        internal static bool IsCached(string path)
        {
            string key = CacheKey(path);
            KeyValuePair<DateTime, MediaInfo> hit;
            lock (recent) return key != null && recent.TryGetValue(key, out hit) && Fresh(hit);
        }

        internal static MediaInfo ProbeCached(string ffmpeg, string path)
        {
            string key = CacheKey(path);
            KeyValuePair<DateTime, MediaInfo> hit;
            if (key != null) lock (recent) if (recent.TryGetValue(key, out hit) && Fresh(hit)) return hit.Value;
            MediaInfo m = Probe(ffmpeg, path);
            if (key != null) lock (recent) { if (recent.Count > 32) recent.Clear(); recent[key] = new KeyValuePair<DateTime, MediaInfo>(DateTime.UtcNow, m); }
            return m;
        }

        // Asks FFmpeg about a file. Its text is UTF-8 (the track titles carry accents), so it is read as such.
        internal static MediaInfo Probe(string ffmpeg, string path)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -nostdin " + PlayerArgs.Whitelist + "-i " + Recorder.Quote(path);
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.StandardErrorEncoding = new UTF8Encoding(false);
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        lock (sb) { if (sb.Length < 32000) sb.AppendLine(e.Data); }
                    };
                    p.Start();
                    ChildJob.Add(p);
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } }
                    p.WaitForExit(2000);
                }
                MediaInfo m;
                lock (sb) m = Parse(sb.ToString());
                if (m.Duration <= 0 && m.HasVideo) m.Duration = ScanDuration(ffmpeg, path);
                if (m.HasVideo && PlayerArgs.NoIndex(path)) PlayerArgs.LearnKeyGap(ffmpeg, path);
                return m;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: sondeo " + ex.Message);
                return new MediaInfo();
            }
        }

        static readonly Dictionary<string, double> scanned = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // A header without a duration (WebM recorded in a browser, an interrupted MKV): the video packets are read, not
        // decoded, to find where it ends. Remembered per file, size and time; 0 when it can't be found.
        internal static double ScanDuration(string ffmpeg, string path)
        {
            string key;
            try { FileInfo f = new FileInfo(path); key = path + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks; }
            catch { return 0; }
            lock (scanned) { double known; if (scanned.TryGetValue(key, out known)) return known; }
            double end = 0;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -nostdin -loglevel error -nostats " + PlayerArgs.Whitelist + "-i " + Recorder.Quote(path) +
                                            " -map 0:v:0 -c copy -f null -progress pipe:1 -";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        long us;
                        if (e.Data != null && e.Data.StartsWith("out_time_us=", StringComparison.Ordinal) &&
                            long.TryParse(e.Data.Substring(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out us) && us > 0)
                            lock (scanned) end = Math.Max(end, us / 1e6);
                    };
                    p.ErrorDataReceived += delegate { };
                    p.Start();
                    ChildJob.Add(p);
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } }
                    p.WaitForExit(2000);
                }
            }
            catch (Exception ex) { ShotStack.Log("Reproductor: duraci\u00F3n " + ex.Message); }
            lock (scanned) { scanned[key] = end; return end; }
        }

        static readonly Regex StreamLine = new Regex(@"^\s*Stream #\d+:\d+(?:\[[^\]]*\])?(?:\([^)]*\))?(?:\[[^\]]*\])?: (Video|Audio|Subtitle|Data|Attachment):\s*(.*)$");

        public static MediaInfo Parse(string text)
        {
            MediaInfo m = new MediaInfo();
            if (string.IsNullOrEmpty(text)) return m;
            text = text.Replace("\r", "");
            // The output side of a command that also writes a file repeats the stream lines.
            int outAt = text.IndexOf("\nOutput #", StringComparison.Ordinal);
            if (outAt >= 0) text = text.Substring(0, outAt);
            m.Raw = text;
            Match d = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (d.Success)
                m.Duration = int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 + int.Parse(d.Groups[2].Value, CultureInfo.InvariantCulture) * 60 +
                             double.Parse(d.Groups[3].Value, CultureInfo.InvariantCulture);
            AudioTrackInfo cur = null;
            bool chosen = false;
            foreach (string line in text.Split('\n'))
            {
                Match s = StreamLine.Match(line);
                if (s.Success)
                {
                    cur = null;
                    chosen = false;
                    string rest = s.Groups[2].Value;
                    if (s.Groups[1].Value == "Video")
                    {
                        if (!m.HasVideo && rest.IndexOf("attached pic", StringComparison.Ordinal) < 0) { ParseVideo(m, rest); chosen = m.HasVideo; }
                    }
                    else if (s.Groups[1].Value == "Audio")
                    {
                        cur = new AudioTrackInfo();
                        cur.Index = m.Audio.Count;
                        cur.Default = Regex.IsMatch(rest, @"\(default\)\s*$");
                        m.Audio.Add(cur);
                    }
                    continue;
                }
                Match t = Regex.Match(line, @"^\s+title\s*:\s*(.*?)\s*$");
                if (t.Success && cur != null && cur.Title == null) cur.Title = t.Groups[1].Value;
                // A phone video stored sideways: FFmpeg rotates it on decode, so the picture has the swapped size.
                Match r = Regex.Match(line, @"display ?matrix: rotation of (-?[0-9.]+) degrees", RegexOptions.IgnoreCase);
                double deg;
                if (r.Success && chosen && double.TryParse(r.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out deg) &&
                    ((int)Math.Round(Math.Abs(deg) / 90)) % 2 == 1)
                {
                    int w = m.Width;
                    m.Width = m.Height;
                    m.Height = w;
                    // Turned on its side, a wide pixel becomes a tall one.
                    int sar = m.SarNum;
                    m.SarNum = m.SarDen;
                    m.SarDen = sar;
                    chosen = false;
                }
            }
            return m;
        }

        // "h264 (High 4:4:4 Predictive), gbrp(pc, gbr/bt709/iec61966-2-1, progressive), 1730x786, 30 fps, 30 tbr, 1k tbn"
        static void ParseVideo(MediaInfo m, string rest)
        {
            List<string> parts = new List<string>();
            StringBuilder sb = new StringBuilder();
            int depth = 0;
            foreach (char c in rest)
            {
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth = Math.Max(0, depth - 1);
                if (c == ',' && depth == 0) { parts.Add(sb.ToString().Trim()); sb.Length = 0; }
                else sb.Append(c);
            }
            parts.Add(sb.ToString().Trim());
            if (parts.Count > 1)
            {
                string f = parts[1];
                int p = f.IndexOf('(');
                m.PixelFormat = (p >= 0 ? f.Substring(0, p) : f).Trim();
                // "yuv420p(tv, bt470bg/bt470bg/smpte170m, progressive)": range, then colour tags when there are any, then the field order.
                Match tags = Regex.Match(f, @"\(([^)]*)\)");
                if (tags.Success)
                {
                    string[] t = tags.Groups[1].Value.Split(new string[] { ", " }, StringSplitOptions.None);
                    m.FullRange = t[0] == "pc";
                    if (t.Length >= 3) m.ColorMatrix = t[1].Split('/')[0];
                }
                if (m.PixelFormat.StartsWith("yuvj", StringComparison.Ordinal)) m.FullRange = true;
            }
            double tbr = 0;
            foreach (string part in parts)
            {
                Match sz = Regex.Match(part, @"^(\d+)x(\d+)");
                if (sz.Success && m.Width == 0)
                {
                    m.Width = int.Parse(sz.Groups[1].Value, CultureInfo.InvariantCulture);
                    m.Height = int.Parse(sz.Groups[2].Value, CultureInfo.InvariantCulture);
                    Match sar = Regex.Match(part, @"\[SAR (\d+):(\d+) DAR");
                    int sn, sd;
                    if (sar.Success && int.TryParse(sar.Groups[1].Value, out sn) && int.TryParse(sar.Groups[2].Value, out sd) && sn > 0 && sd > 0 && sn <= 64 * sd && sd <= 64 * sn)
                    {
                        m.SarNum = sn;
                        m.SarDen = sd;
                    }
                    continue;
                }
                Match fp = Regex.Match(part, @"^([0-9.]+) fps$");
                if (fp.Success && m.Fps == 0) { double v; if (double.TryParse(fp.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) m.Fps = v; continue; }
                Match tb = Regex.Match(part, @"^([0-9.]+) tbr$");
                if (tb.Success) { double v; if (double.TryParse(tb.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) tbr = v; }
            }
            if (m.Fps <= 0) m.Fps = tbr;
        }
    }

    // FFmpeg command lines of the player, as pure functions.
    internal static class PlayerArgs
    {
        // The player, the editor and the export read the same containers.
        internal const string Whitelist = Recorder.SafeInput;

        static string Sec(double t) { return Math.Max(0, t).ToString("0.000", CultureInfo.InvariantCulture); }

        // No -ss at the very start: some demuxers (FLV) cannot "seek" to 0 and then write nothing.
        internal static string Seek(double t) { return t >= 0.0005 ? "-ss " + Sec(t) + " " : ""; }

        // MPEG-TS and MPEG-PS files have no index: a seek lands on any packet and the frames up to the next keyframe are
        // lost. Those inputs seek this much earlier and the decoded frames before t are dropped afterwards.
        internal const double Preroll = 3;
        static readonly string[] NoIndexExts = { ".ts", ".mts", ".m2ts", ".m2t", ".trp", ".tp", ".mpg", ".mpeg", ".mpe", ".m1v", ".m2v", ".mpv", ".vob" };

        internal static bool NoIndex(string src)
        {
            string ext = (System.IO.Path.GetExtension(src ?? "") ?? "").ToLowerInvariant();
            return Array.IndexOf(NoIndexExts, ext) >= 0;
        }

        // A file whose keyframes are further apart than Preroll (x264's default for a TS is one every 250 frames, over
        // 8 s at 30 fps) seeks back past its longest gap instead, once LearnKeyGap has measured it.
        internal static double Early(string src, double t)
        {
            if (!NoIndex(src)) return 0;
            double gap;
            lock (keyGaps) if (!keyGaps.TryGetValue(GapKey(src), out gap)) gap = 0;
            return Math.Min(Math.Max(0, t), Math.Max(Preroll, Math.Min(60, gap + 0.5)));
        }

        static readonly Dictionary<string, double> keyGaps = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        static string GapKey(string src)
        {
            try { System.IO.FileInfo f = new System.IO.FileInfo(src); return src + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks; }
            catch (Exception) { return src ?? ""; }
        }

        // The longest gap between keyframes in the first two minutes, read by decoding only the keyframes (fast).
        // Remembered per file, size and time.
        internal static void LearnKeyGap(string ffmpeg, string src)
        {
            string key = GapKey(src);
            lock (keyGaps) if (keyGaps.ContainsKey(key)) return;
            double gap = 0, last = -1;
            object gate = new object();
            try
            {
                using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -nostdin -nostats -skip_frame nokey " + Whitelist + "-i " + Recorder.Quote(src) +
                                            " -t 120 -map 0:v:0 -vf showinfo -an -f null -";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.ErrorDataReceived += delegate(object o, System.Diagnostics.DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        int i = e.Data.IndexOf(" pts_time:", StringComparison.Ordinal);
                        if (i < 0) return;
                        int j = i + 10, k = j;
                        while (k < e.Data.Length && e.Data[k] != ' ') k++;
                        double t;
                        if (!double.TryParse(e.Data.Substring(j, k - j), NumberStyles.Float, CultureInfo.InvariantCulture, out t)) return;
                        lock (gate)
                        {
                            gap = Math.Max(gap, last < 0 ? t : t - last);
                            last = t;
                        }
                    };
                    p.Start();
                    ChildJob.Add(p);
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } }
                    p.WaitForExit(2000);
                }
            }
            catch (Exception ex) { ShotStack.Log("Reproductor: fotogramas clave " + ex.Message); }
            lock (keyGaps) keyGaps[key] = gap;
        }

        // The input part of a command that starts reading src at t.
        internal static string Input(string src, double t)
        {
            double e = Early(src, t);
            return Seek(t - e) + Whitelist + "-i " + Recorder.Quote(src) + (e >= 0.0005 ? " -ss " + Sec(e) : "");
        }

        // Frame i of a run that starts at t is the one shown at t + i / fps; the picture is scaled to w x h unless that is the native size.
        internal static string Video(string src, double t, int w, int h, double fps, int nativeW, int nativeH)
        {
            return Video(src, t, w, h, fps, nativeW, nativeH, 1, 1, Recorder.SafeInput);
        }

        // Scaling 0 is Lanczos, anything else bilinear. At a speed other than 1 the picture is retimed before the constant
        // frame rate, so frame i of the run shows the media at t + i * speed / fps.
        internal static string Video(string src, double t, int w, int h, double fps, int nativeW, int nativeH, double speed, int scaling)
        {
            return Video(src, t, w, h, fps, nativeW, nativeH, speed, scaling, Whitelist);
        }

        static string Video(string src, double t, int w, int h, double fps, int nativeW, int nativeH, double speed, int scaling, string wl)
        {
            string a = "-hide_banner -loglevel error -nostdin " + Input(src, t) + " -map 0:v:0 -an -sn -dn";
            string vf = Scale(w, h, nativeW, nativeH, scaling);
            if (Math.Abs(speed - 1) > 1e-9) vf += (vf.Length > 0 ? "," : "") + "setpts=PTS/" + Num(speed);
            if (vf.Length > 0) a += " -vf " + vf;
            return a + " -fps_mode cfr -r " + Num(fps) + " -f rawvideo -pix_fmt bgra -";
        }

        // One picture, for a paused window: the frame at t (never upscaled, like the video).
        internal static string Still(string src, double t, int w, int h, int nativeW, int nativeH, int scaling)
        {
            string a = "-hide_banner -loglevel error -nostdin " + Input(src, t) + " -map 0:v:0 -an -sn -dn";
            string vf = Scale(w, h, nativeW, nativeH, scaling);
            if (vf.Length > 0) a += " -vf " + vf;
            return a + " -frames:v 1 -f rawvideo -pix_fmt bgra -";
        }

        internal static string Audio(string src, double t, int track)
        {
            return Audio(src, t, track, 1, Recorder.SafeInput);
        }

        internal static string Audio(string src, double t, int track, double speed)
        {
            return Audio(src, t, track, speed, Whitelist);
        }

        static string Audio(string src, double t, int track, double speed, string wl)
        {
            string tempo = Tempo(speed);
            return "-hide_banner -loglevel error -nostdin " + Input(src, t) + " -map 0:a:" + track +
                   " -vn -sn -dn" + (tempo.Length > 0 ? " -af " + tempo : "") + " -ac 2 -ar 48000 -f f32le -";
        }

        static string Num(double v) { return v.ToString("0.######", CultureInfo.InvariantCulture); }

        static string Scale(int w, int h, int nativeW, int nativeH, int scaling)
        {
            if (w <= 0 || h <= 0 || (w == nativeW && h == nativeH)) return "";
            return "scale=" + w + ":" + h + ":flags=" + (scaling == 0 ? "lanczos" : "bilinear");
        }

        // atempo keeps the pitch; each stage takes 0.5 to 2, so 0.25 is two stages of 0.5.
        internal static string Tempo(double speed)
        {
            if (speed <= 0 || Math.Abs(speed - 1) < 1e-9) return "";
            List<string> st = new List<string>();
            while (speed < 0.5 - 1e-9) { st.Add("atempo=0.5"); speed /= 0.5; }
            while (speed > 2 + 1e-9) { st.Add("atempo=2"); speed /= 2; }
            if (Math.Abs(speed - 1) > 1e-9) st.Add("atempo=" + Num(speed));
            return string.Join(",", st.ToArray());
        }
    }

    // Where the sound goes. The position is counted in sample frames played since the last Reset.
    internal interface ISoundOut : IDisposable
    {
        bool Open(int rate, int channels);
        // Blocks while the device queue is full; false when a Reset (or Dispose) cancelled it.
        bool Write(float[] samples, int count);
        long Position { get; }
        void Pause();
        void Resume();
        void Reset();
    }

    // waveOut with 32-bit float samples: four buffers of 50 ms, the device signals an event when one is free.
    internal sealed class WaveOutSink : ISoundOut
    {
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hwo, int device, IntPtr format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutPause(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutRestart(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutGetPosition(IntPtr hwo, ref MmTime time, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct WaveHdr
        {
            public IntPtr lpData;
            public uint dwBufferLength, dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags, dwLoops;
            public IntPtr lpNext, reserved;
        }

        // MMTIME is packed on 1 byte: a 4-byte type and an 8-byte union, whose first field is the value.
        [StructLayout(LayoutKind.Explicit, Size = 12)]
        struct MmTime
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(4)] public uint value;
        }

        sealed class Buf { public IntPtr Hdr, Data; }

        const int CallbackEvent = 0x00050000, WhdrInQueue = 0x10, TimeMs = 1, TimeSamples = 2, TimeBytes = 4;
        const int Buffers = 4, BufferFrames = 2400;

        readonly object gate = new object();
        IntPtr hwo;
        AutoResetEvent evt;
        Buf[] bufs;
        int next, rate, channels, epoch, hdrSize, flagsAt, lengthAt;
        long lastPosition;

        public bool Open(int rate, int channels)
        {
            lock (gate)
            {
                if (hwo != IntPtr.Zero) return true;
                this.rate = rate;
                this.channels = channels;
                AutoResetEvent e = new AutoResetEvent(false);
                IntPtr fmt = Marshal.AllocHGlobal(40);
                int rc;
                try
                {
                    IntPtr cb = e.SafeWaitHandle.DangerousGetHandle();
                    Format(fmt, false);
                    rc = waveOutOpen(out hwo, -1, fmt, cb, IntPtr.Zero, CallbackEvent);
                    if (rc != 0)
                    {
                        Format(fmt, true);
                        rc = waveOutOpen(out hwo, -1, fmt, cb, IntPtr.Zero, CallbackEvent);
                    }
                }
                finally { Marshal.FreeHGlobal(fmt); }
                if (rc != 0)
                {
                    hwo = IntPtr.Zero;
                    e.Dispose();
                    ShotStack.Log("Reproductor: waveOutOpen " + rc);
                    return false;
                }
                evt = e;
                hdrSize = Marshal.SizeOf(typeof(WaveHdr));
                flagsAt = (int)Marshal.OffsetOf(typeof(WaveHdr), "dwFlags");
                lengthAt = (int)Marshal.OffsetOf(typeof(WaveHdr), "dwBufferLength");
                bufs = new Buf[Buffers];
                for (int i = 0; i < Buffers; i++)
                {
                    Buf b = new Buf();
                    b.Data = Marshal.AllocHGlobal(BufferFrames * channels * 4);
                    b.Hdr = Marshal.AllocHGlobal(hdrSize);
                    WaveHdr h = new WaveHdr();
                    h.lpData = b.Data;
                    h.dwBufferLength = (uint)(BufferFrames * channels * 4);
                    Marshal.StructureToPtr(h, b.Hdr, false);
                    waveOutPrepareHeader(hwo, b.Hdr, hdrSize);
                    bufs[i] = b;
                }
                waveOutPause(hwo);
                return true;
            }
        }

        // WAVE_FORMAT_IEEE_FLOAT, or WAVE_FORMAT_EXTENSIBLE with the float sub-format for drivers that insist.
        void Format(IntPtr p, bool extensible)
        {
            Marshal.WriteInt16(p, 0, (short)(extensible ? unchecked((short)0xFFFE) : 3));
            Marshal.WriteInt16(p, 2, (short)channels);
            Marshal.WriteInt32(p, 4, rate);
            Marshal.WriteInt32(p, 8, rate * channels * 4);
            Marshal.WriteInt16(p, 12, (short)(channels * 4));
            Marshal.WriteInt16(p, 14, 32);
            Marshal.WriteInt16(p, 16, (short)(extensible ? 22 : 0));
            if (!extensible) return;
            Marshal.WriteInt16(p, 18, 32);
            Marshal.WriteInt32(p, 20, channels == 2 ? 3 : 1);
            byte[] sub = { 3, 0, 0, 0, 0, 0, 0x10, 0, 0x80, 0, 0, 0xAA, 0, 0x38, 0x9B, 0x71 };
            Marshal.Copy(sub, 0, new IntPtr(p.ToInt64() + 24), 16);
        }

        public bool Write(float[] samples, int count)
        {
            int ep;
            AutoResetEvent e;
            lock (gate)
            {
                if (hwo == IntPtr.Zero) return false;
                ep = epoch;
                e = evt;
            }
            int per = BufferFrames * channels, done = 0;
            while (done < count)
            {
                Buf b = null;
                while (b == null)
                {
                    lock (gate)
                    {
                        if (hwo == IntPtr.Zero || epoch != ep) return false;
                        for (int i = 0; i < bufs.Length; i++)
                        {
                            Buf c = bufs[(next + i) % bufs.Length];
                            if ((Marshal.ReadInt32(c.Hdr, flagsAt) & WhdrInQueue) == 0) { b = c; next = (next + i + 1) % bufs.Length; break; }
                        }
                    }
                    if (b == null) e.WaitOne(20);
                }
                int n = Math.Min(count - done, per);
                lock (gate)
                {
                    if (hwo == IntPtr.Zero || epoch != ep) return false;
                    Marshal.Copy(samples, done, b.Data, n);
                    Marshal.WriteInt32(b.Hdr, lengthAt, n * 4);
                    if (waveOutWrite(hwo, b.Hdr, hdrSize) != 0) return false;
                }
                done += n;
            }
            return true;
        }

        public long Position
        {
            get
            {
                lock (gate)
                {
                    if (hwo == IntPtr.Zero) return 0;
                    MmTime t = new MmTime();
                    t.type = TimeSamples;
                    if (waveOutGetPosition(hwo, ref t, 12) != 0) return lastPosition;
                    long v = t.value;
                    if (t.type == TimeBytes) v /= channels * 4;
                    else if (t.type == TimeMs) v = v * rate / 1000;
                    else if (t.type != TimeSamples) return lastPosition;
                    lastPosition = v;
                    return v;
                }
            }
        }

        public void Pause() { lock (gate) { if (hwo != IntPtr.Zero) waveOutPause(hwo); } }
        public void Resume() { lock (gate) { if (hwo != IntPtr.Zero) waveOutRestart(hwo); } }

        // Everything queued is dropped, the position goes back to zero and the device stays paused.
        public void Reset()
        {
            lock (gate)
            {
                epoch++;
                if (hwo == IntPtr.Zero) return;
                waveOutReset(hwo);
                waveOutPause(hwo);
                lastPosition = 0;
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                epoch++;
                if (hwo == IntPtr.Zero) return;
                waveOutReset(hwo);
                foreach (Buf b in bufs)
                {
                    waveOutUnprepareHeader(hwo, b.Hdr, hdrSize);
                    Marshal.FreeHGlobal(b.Hdr);
                    Marshal.FreeHGlobal(b.Data);
                }
                waveOutClose(hwo);
                hwo = IntPtr.Zero;
                evt.Dispose();
                evt = null;
            }
        }
    }

    // One decoded picture, BGRA. The buffer belongs to the player: it is valid only while FrameReady runs.
    internal sealed class PlayerFrame
    {
        public byte[] Data;
        public int Width, Height, Number;
        public double Time;
    }

    internal enum PlayState { Stopped, Playing, Paused, Ended }

    internal sealed class PlayerChild
    {
        Process p;
        readonly StringBuilder err = new StringBuilder();
        public int Id;

        public Stream Out { get { return p.StandardOutput.BaseStream; } }

        public static PlayerChild Start(string exe, string args)
        {
            PlayerChild c = new PlayerChild();
            Process pr = new Process();
            pr.StartInfo.FileName = exe;
            pr.StartInfo.Arguments = args;
            pr.StartInfo.UseShellExecute = false;
            pr.StartInfo.CreateNoWindow = true;
            pr.StartInfo.RedirectStandardOutput = true;
            pr.StartInfo.RedirectStandardError = true;
            pr.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (c.err) { if (c.err.Length < 2000) c.err.AppendLine(e.Data); }
            };
            pr.Start();
            ChildJob.Add(pr);
            Power.OptOutOfThrottling(pr);
            pr.BeginErrorReadLine();
            c.p = pr;
            c.Id = pr.Id;
            return c;
        }

        public string Tail
        {
            get
            {
                lock (err)
                {
                    string t = err.ToString().Trim();
                    return t.Length > 300 ? t.Substring(t.Length - 300) : t;
                }
            }
        }

        public bool Alive { get { try { return !p.HasExited; } catch { return false; } } }
        public void Kill() { try { if (!p.HasExited) p.Kill(); } catch { } }

        public int Exit()
        {
            try { if (p.WaitForExit(2000)) return p.ExitCode; }
            catch { }
            return -1;
        }

        public void Dispose() { try { p.Dispose(); } catch { } }
    }

    // Plays a video: FFmpeg decodes the picture to BGRA and the sound to float samples, the sound card plays the sound
    // and its position is the clock the pictures follow (the Stopwatch is the clock when there is no sound to follow).
    // One engine thread owns the processes and the clock; every public member is safe to call from any thread and
    // returns at once (commands are applied by the engine, the latest seek wins). After Seek, Pause, Stop or Dispose
    // returns, no FrameReady from before it starts any more.
    internal sealed class Playback : IDisposable
    {
        const int Rate = 48000, Channels = 2, PrimeFrames = 4800, ChunkFrames = 2400, PoolSize = 4;
        const double LateFrames = 1.5, PrimeTimeout = 20;

        sealed class VideoRun
        {
            public PlayerChild Proc;
            public Thread Thread;
            public readonly object Sync = new object();
            public readonly List<PlayerFrame> Ready = new List<PlayerFrame>();
            public readonly List<PlayerFrame> Free = new List<PlayerFrame>();
            public double T0, Fps, Speed = 1;
            public int Frames, ExitCode;
            public bool Eof;
            public volatile bool Cancel;

            public void Stop()
            {
                Cancel = true;
                lock (Sync) Monitor.PulseAll(Sync);
                if (Proc != null) Proc.Kill();
            }
        }

        sealed class AudioRun
        {
            public PlayerChild Proc;
            public Thread Thread;
            public volatile bool Cancel, Ready, Eof;
            long total;

            public long Total { get { return Interlocked.Read(ref total); } set { Interlocked.Exchange(ref total, value); } }

            public void Stop()
            {
                Cancel = true;
                if (Proc != null) Proc.Kill();
            }
        }

        readonly string ffmpeg, src;
        readonly MediaInfo info;
        readonly ISoundOut sink;
        readonly object gate = new object(), clockLock = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false), stopAck = new AutoResetEvent(false);
        readonly List<int> pids = new List<int>();
        readonly List<PlayerChild> children = new List<PlayerChild>();
        Thread engine;
        bool disposed;

        // Commands, under gate.
        bool seekPending, stopPending, audioPending, wantPlay;
        double seekTarget, endT;
        int decodeW, decodeH, track;
        volatile PlayState state = PlayState.Stopped;
        double volume = 1, speed = 1, loopStart;
        int scaling = 1;
        bool loop;

        // Engine only.
        VideoRun vrun;
        AudioRun arun;
        bool runsAlive, priming, primeNeedsVideo, periodOn, sinkTried, sinkOk, noticed;
        double runSpeed = 1;
        int runScaling = 1;
        readonly Stopwatch primeWatch = new Stopwatch();
        PlayerFrame[] pool;
        int poolW, poolH;

        // The clock, under clockLock.
        bool clockRunning, clockOverride;
        double origin, heldPos, drainWall = -1, clockSpeed = 1;
        AudioRun clockAudio;
        readonly Stopwatch wall = new Stopwatch();

        long decoded, shown, dropped, audioQueued, loops;
        readonly Stopwatch loopWatch = new Stopwatch();
        int stillReq;
        PlayerChild stillProc;
        readonly object stillLock = new object();

        public event Action<PlayerFrame> FrameReady;
        // The picture asked for with Still, from any thread; its buffer is the receiver's to keep.
        public event Action<PlayerFrame> StillReady;
        public event Action Ended;
        public event Action<string> Error, Notice;
        // Playback ended at, or jumps to, the time this returns for the current position: itself to play on, a later
        // time to skip what is between, positive infinity to end. Called on the engine thread.
        public Func<double, double> Skip;

        public Playback(string ffmpeg, string src, MediaInfo info) : this(ffmpeg, src, info, new WaveOutSink()) { }

        public Playback(string ffmpeg, string src, MediaInfo info, ISoundOut sink)
        {
            this.ffmpeg = ffmpeg;
            this.src = src;
            this.info = info;
            this.sink = sink;
            endT = info.Duration > 0 ? info.Duration : double.MaxValue;
        }

        public MediaInfo Info { get { return info; } }
        public PlayState State { get { return state; } }
        public long FramesDecoded { get { return Interlocked.Read(ref decoded); } }
        public long FramesShown { get { return Interlocked.Read(ref shown); } }
        public long FramesDropped { get { return Interlocked.Read(ref dropped); } }
        public long AudioFramesQueued { get { return Interlocked.Read(ref audioQueued); } }
        public long Loops { get { return Interlocked.Read(ref loops); } }

        // At the end, playing carries on from LoopStart instead of ending.
        public bool Repeat
        {
            get { lock (gate) return loop; }
            set { lock (gate) loop = value; }
        }

        public double LoopStart
        {
            get { lock (gate) return loopStart; }
            set { lock (gate) loopStart = Math.Max(0, value); }
        }

        // 0 is Lanczos, 1 bilinear. Takes effect at the next start or seek.
        public int Scaling
        {
            get { lock (gate) return scaling; }
            set { lock (gate) scaling = value; }
        }

        // Media seconds per second, 0.25 to 4. Playing or paused, the pictures and the sound start again at the same place.
        public double Speed
        {
            get { lock (gate) return speed; }
            set
            {
                value = Math.Max(0.25, Math.Min(4, value));
                lock (gate)
                {
                    if (Math.Abs(speed - value) < 1e-9) return;
                    speed = value;
                    if (disposed || engine == null || (state != PlayState.Playing && state != PlayState.Paused)) return;
                    QueueSeek(Position);
                }
                wake.Set();
            }
        }

        // Every FFmpeg this player ever started, for the tests.
        public int[] ProcessIds() { lock (pids) return pids.ToArray(); }

        public double EndTime
        {
            get { lock (gate) return endT; }
            set { lock (gate) endT = value > 0 ? value : double.MaxValue; }
        }

        public double Volume
        {
            get { lock (gate) return volume; }
            set
            {
                value = Math.Max(0, Math.Min(1, value));
                lock (gate)
                {
                    // Silence stops decoding the sound; coming back from it restarts it.
                    if ((value == 0) != (volume == 0)) audioPending = true;
                    volume = value;
                }
                wake.Set();
            }
        }

        public int Track
        {
            get { lock (gate) return track; }
            set
            {
                lock (gate) { if (track == value) return; track = value; audioPending = true; }
                wake.Set();
            }
        }

        // The size pictures are decoded at (even, never above native); 0 x 0 is native. Takes effect at the next start or seek.
        public void SetDecodeSize(int w, int h)
        {
            lock (gate)
            {
                if (w <= 0 || h <= 0 || (w >= info.Width && h >= info.Height)) { decodeW = decodeH = 0; return; }
                decodeW = Math.Max(2, Math.Min(info.Width, w) & ~1);
                decodeH = Math.Max(2, Math.Min(info.Height, h) & ~1);
            }
        }

        // The playhead of a run that begins at t: the frame grid the pictures are counted on.
        public double Align(double t) { return Math.Round(Math.Max(0, t) * info.Fps) / info.Fps; }

        public double Position
        {
            get { lock (clockLock) return ClockLocked(); }
        }

        double ClockLocked()
        {
            if (clockOverride || !clockRunning) return heldPos;
            AudioRun a = clockAudio;
            if (a == null) return origin + clockSpeed * wall.Elapsed.TotalSeconds;
            long played = sink.Position;
            long total = a.Total;
            if (a.Eof && played >= total)
            {
                // The sound ran out before the picture did: the Stopwatch carries on from where it stopped.
                double now = wall.Elapsed.TotalSeconds;
                if (drainWall < 0) drainWall = now;
                return origin + clockSpeed * (total / (double)Rate + (now - drainWall));
            }
            return origin + clockSpeed * (played / (double)Rate);
        }

        public void Start(double t, double end)
        {
            lock (gate)
            {
                if (disposed) return;
                endT = end > 0 ? end : (info.Duration > 0 ? info.Duration : double.MaxValue);
                decoded = shown = dropped = audioQueued = 0;
                QueueSeek(t);
                wantPlay = true;
                state = PlayState.Playing;
                EnsureEngine();
            }
            wake.Set();
        }

        // Playing: carries on from t. Otherwise the position moves and stays paused.
        public void Seek(double t)
        {
            lock (gate)
            {
                if (disposed) return;
                QueueSeek(t);
                if (state != PlayState.Playing) state = PlayState.Paused;
                EnsureEngine();
            }
            wake.Set();
        }

        void QueueSeek(double t)
        {
            seekPending = true;
            seekTarget = double.IsNaN(t) ? 0 : Math.Max(0, t);
            lock (clockLock)
            {
                heldPos = seekTarget;
                clockOverride = true;
            }
        }

        public void Pause()
        {
            lock (gate)
            {
                if (disposed || state != PlayState.Playing) return;
                wantPlay = false;
                state = PlayState.Paused;
            }
            wake.Set();
        }

        public void Resume()
        {
            lock (gate)
            {
                if (disposed || state != PlayState.Paused) return;
                wantPlay = true;
                state = PlayState.Playing;
                EnsureEngine();
            }
            wake.Set();
        }

        // Kills what is running and waits for it; the position stays where it was.
        public void Stop()
        {
            bool wait;
            lock (gate)
            {
                if (disposed) return;
                stopPending = true;
                seekPending = false;
                wantPlay = false;
                state = PlayState.Stopped;
                wait = engine != null && engine != Thread.CurrentThread;
            }
            wake.Set();
            if (wait) stopAck.WaitOne(3000);
        }

        void EnsureEngine()
        {
            if (engine != null) return;
            engine = new Thread(Loop);
            engine.IsBackground = true;
            engine.Name = "Stackshot player";
            engine.Start();
        }

        public void Dispose()
        {
            Thread t;
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                t = engine;
            }
            // Killed first: the readers see the end of their pipe and the engine has little left to wait for.
            lock (stillLock) lock (children) foreach (PlayerChild c in children) c.Kill();
            wake.Set();
            if (t != null && t != Thread.CurrentThread) t.Join(1500);
            try { sink.Dispose(); } catch { }
            lock (children) foreach (PlayerChild c in children) c.Kill();
        }

        // ---- engine ----

        void Loop()
        {
            Power.OptOutThisThread();
            try
            {
                while (true)
                {
                    bool doStop, doSeek, doAudio, play;
                    double target;
                    lock (gate)
                    {
                        if (disposed) break;
                        doStop = stopPending;
                        doSeek = seekPending;
                        doAudio = audioPending;
                        target = seekTarget;
                        play = wantPlay;
                        stopPending = seekPending = audioPending = false;
                    }
                    if (doStop)
                    {
                        FreezeClock();
                        StopRuns();
                        lock (clockLock) clockOverride = false;
                        stopAck.Set();
                        doSeek = doAudio = false;
                    }
                    if (doSeek) ApplySeek(target);
                    else if (doAudio) ApplyAudio();
                    // Ending or failing while applying a command clears the wish to play.
                    lock (gate) play = wantPlay;
                    Reconcile(play);
                    wake.WaitOne(WaitMs());
                }
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: " + ex);
                try { Fail(I18n.T("Error al reproducir: ", "Playback error: ") + ex.Message); } catch { }
            }
            finally
            {
                try { StopRuns(); } catch { }
                stopAck.Set();
            }
        }

        int WaitMs()
        {
            if (!runsAlive) return Timeout.Infinite;
            if (priming) return 10;
            return clockRunning ? 4 : Timeout.Infinite;
        }

        void Reconcile(bool play)
        {
            if (play)
            {
                if (!runsAlive) BeginRuns();
                else if (!clockRunning && !priming) ResumeClock();
                if (runsAlive && priming) CheckPrime();
            }
            else if (clockRunning) FreezeClock();
            if (runsAlive && clockRunning) Present();
        }

        void ApplySeek(double t)
        {
            FreezeClock();
            StopRuns();
            double end;
            lock (gate) end = endT;
            lock (clockLock)
            {
                heldPos = Math.Min(t, end);
                clockOverride = false;
            }
            if (t >= end - 1e-6 || (info.Duration > 0 && t >= info.Duration + 1e-6)) FinishNow();
        }

        void StartPeriod() { if (!periodOn) { Native.timeBeginPeriod(1); periodOn = true; } }
        void EndPeriod() { if (periodOn) { Native.timeEndPeriod(1); periodOn = false; } }

        bool EnsureSink()
        {
            if (sinkTried) return sinkOk;
            sinkTried = true;
            try { sinkOk = sink != null && sink.Open(Rate, Channels); }
            catch (Exception ex) { ShotStack.Log("Reproductor: sonido " + ex.Message); sinkOk = false; }
            if (!sinkOk && !noticed)
            {
                noticed = true;
                Action<string> n = Notice;
                if (n != null) n(I18n.T("Sin salida de sonido", "No sound output"));
            }
            return sinkOk;
        }

        bool WantAudio()
        {
            double v;
            int tr;
            lock (gate) { v = volume; tr = track; }
            return info.Audio.Count > 0 && tr >= 0 && tr < info.Audio.Count && v > 0 && EnsureSink();
        }

        // Starts both decoders at the (aligned) position and waits for them in the priming phase.
        void BeginRuns()
        {
            double t0;
            int w, h;
            lock (clockLock) t0 = heldPos;
            double sp;
            int sc;
            lock (gate) { w = decodeW; h = decodeH; sp = speed; sc = scaling; }
            runSpeed = sp;
            runScaling = sc;
            t0 = Align(t0);
            if (w == 0) { w = info.Width; h = info.Height; }
            try
            {
                vrun = StartVideo(t0, w, h);
                arun = WantAudio() ? StartAudio(t0) : null;
            }
            catch (Exception ex)
            {
                Fail(I18n.T("No se pudo iniciar FFmpeg: ", "Could not start FFmpeg: ") + ex.Message);
                return;
            }
            lock (clockLock)
            {
                origin = t0;
                clockSpeed = runSpeed;
                heldPos = t0;
                clockRunning = false;
                clockOverride = false;
                clockAudio = arun;
                drainWall = -1;
                wall.Reset();
            }
            runsAlive = true;
            priming = true;
            primeNeedsVideo = true;
            primeWatch.Restart();
            StartPeriod();
        }

        VideoRun StartVideo(double t0, int w, int h)
        {
            VideoRun v = new VideoRun();
            v.T0 = t0;
            v.Fps = info.Fps;
            v.Speed = runSpeed;
            // A quarter of a frame before the first one, so the seek never lands past it on rounded timestamps.
            double ss = Math.Max(0, t0 - 0.25 / info.Fps);
            v.Proc = Spawn(PlayerArgs.Video(src, ss, w, h, info.Fps, info.Width, info.Height, runSpeed, runScaling));
            int bytes = w * h * 4;
            if (pool == null || poolW != w || poolH != h)
            {
                pool = new PlayerFrame[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    PlayerFrame f = new PlayerFrame();
                    f.Data = new byte[bytes];
                    f.Width = w;
                    f.Height = h;
                    pool[i] = f;
                }
                poolW = w;
                poolH = h;
            }
            v.Free.AddRange(pool);
            v.Thread = new Thread(delegate() { VideoLoop(v, bytes); });
            v.Thread.IsBackground = true;
            v.Thread.Name = "Stackshot video";
            v.Thread.Start();
            return v;
        }

        AudioRun StartAudio(double t0)
        {
            AudioRun a = new AudioRun();
            int tr;
            lock (gate) tr = track;
            a.Proc = Spawn(PlayerArgs.Audio(src, t0, tr, runSpeed));
            a.Thread = new Thread(delegate() { AudioLoop(a); });
            a.Thread.IsBackground = true;
            a.Thread.Name = "Stackshot audio";
            a.Thread.Start();
            return a;
        }

        PlayerChild Spawn(string args)
        {
            PlayerChild c = PlayerChild.Start(ffmpeg, args);
            lock (pids) pids.Add(c.Id);
            lock (children) { children.RemoveAll(delegate(PlayerChild x) { return !x.Alive; }); children.Add(c); }
            return c;
        }

        static int ReadFull(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) break;
                got += n;
            }
            return got;
        }

        void VideoLoop(VideoRun v, int bytes)
        {
            try
            {
                Stream s = v.Proc.Out;
                while (!v.Cancel)
                {
                    PlayerFrame f;
                    lock (v.Sync)
                    {
                        while (!v.Cancel && v.Free.Count == 0) Monitor.Wait(v.Sync);
                        if (v.Cancel) break;
                        f = v.Free[v.Free.Count - 1];
                        v.Free.RemoveAt(v.Free.Count - 1);
                    }
                    int n = ReadFull(s, f.Data, bytes);
                    if (n < bytes) { lock (v.Sync) v.Free.Add(f); break; }
                    lock (v.Sync)
                    {
                        f.Number = v.Frames;
                        f.Time = v.T0 + v.Frames * v.Speed / v.Fps;
                        v.Frames++;
                        v.Ready.Add(f);
                    }
                    Interlocked.Increment(ref decoded);
                    wake.Set();
                }
            }
            catch (Exception) { }
            finally
            {
                int code = v.Cancel ? 0 : v.Proc.Exit();
                lock (v.Sync)
                {
                    v.ExitCode = code;
                    v.Eof = true;
                }
                wake.Set();
            }
        }

        void AudioLoop(AudioRun a)
        {
            long queued = 0;
            try
            {
                Stream s = a.Proc.Out;
                byte[] buf = new byte[ChunkFrames * Channels * 4];
                float[] samples = new float[ChunkFrames * Channels];
                while (!a.Cancel)
                {
                    int n = ReadFull(s, buf, buf.Length);
                    int frames = n / (Channels * 4);
                    if (frames <= 0) break;
                    Buffer.BlockCopy(buf, 0, samples, 0, frames * Channels * 4);
                    double v;
                    lock (gate) v = volume;
                    if (v != 1) { float g = (float)v; for (int i = 0; i < frames * Channels; i++) samples[i] *= g; }
                    if (!sink.Write(samples, frames * Channels)) break;
                    queued += frames;
                    a.Total = queued;
                    Interlocked.Add(ref audioQueued, frames);
                    if (!a.Ready && queued >= PrimeFrames) { a.Ready = true; wake.Set(); }
                    if (n < buf.Length) break;
                }
            }
            catch (Exception) { }
            finally
            {
                a.Total = queued;
                a.Eof = true;
                a.Ready = true;
                wake.Set();
            }
        }

        void CheckPrime()
        {
            VideoRun v = vrun;
            AudioRun a = arun;
            bool vOk = true, aOk = a == null || a.Ready;
            if (primeNeedsVideo && v != null) lock (v.Sync) vOk = v.Ready.Count > 0 || v.Eof;
            if (vOk && aOk)
            {
                if (v != null && primeNeedsVideo && CheckVideoFailed(v)) return;
                StartClock();
            }
            else if (primeWatch.Elapsed.TotalSeconds > PrimeTimeout) Fail(I18n.T("No se pudo reproducir: FFmpeg no responde.", "Could not play: FFmpeg is not responding."));
        }

        // A decoder that ended with an error before giving a single picture.
        bool CheckVideoFailed(VideoRun v)
        {
            bool failed;
            int code;
            lock (v.Sync) { failed = v.Eof && v.Frames == 0 && v.ExitCode != 0; code = v.ExitCode; }
            if (!failed) return false;
            string tail = v.Proc.Tail;
            ShotStack.Log("Reproductor: FFmpeg " + code + ": " + tail);
            Fail(I18n.T("No se pudo reproducir el v\u00EDdeo.", "Could not play the video.") + (tail.Length > 0 ? "\n\n" + tail : ""));
            return true;
        }

        void StartClock()
        {
            lock (clockLock)
            {
                wall.Restart();
                clockRunning = true;
            }
            if (clockAudio != null) sink.Resume();
            priming = false;
            primeNeedsVideo = false;
        }

        void ResumeClock()
        {
            if (clockAudio != null) sink.Resume();
            lock (clockLock)
            {
                wall.Start();
                clockRunning = true;
            }
        }

        void FreezeClock()
        {
            bool audio;
            lock (clockLock) { if (!clockRunning) return; audio = clockAudio != null; }
            if (audio) sink.Pause();
            lock (clockLock)
            {
                heldPos = ClockLocked();
                clockRunning = false;
                wall.Stop();
            }
        }

        // The sound changed (another track, or silence on or off): the decoder of the sound restarts at the clock.
        void ApplyAudio()
        {
            if (!runsAlive) return;
            FreezeClock();
            AudioRun old = arun;
            arun = null;
            lock (clockLock) clockAudio = null;
            if (old != null) { old.Stop(); }
            if (sinkOk) sink.Reset();
            JoinAudio(old);
            double p;
            lock (clockLock) p = heldPos;
            try { arun = WantAudio() ? StartAudio(p) : null; }
            catch (Exception ex) { ShotStack.Log("Reproductor: sonido " + ex.Message); arun = null; }
            lock (clockLock)
            {
                origin = p;
                clockRunning = false;
                clockAudio = arun;
                drainWall = -1;
                wall.Reset();
            }
            priming = true;
            primeNeedsVideo = false;
            primeWatch.Restart();
        }

        void JoinAudio(AudioRun a)
        {
            if (a == null) return;
            if (a.Thread != null) a.Thread.Join(1500);
            if (a.Proc != null) a.Proc.Dispose();
        }

        void StopRuns()
        {
            VideoRun v = vrun;
            AudioRun a = arun;
            vrun = null;
            arun = null;
            runsAlive = priming = false;
            lock (clockLock)
            {
                clockRunning = false;
                clockAudio = null;
            }
            if (v != null) v.Stop();
            if (a != null) a.Stop();
            if (sinkOk) sink.Reset();
            if (v != null)
            {
                bool joined = v.Thread == null || v.Thread.Join(1500);
                if (!joined) pool = null;
                if (v.Proc != null) v.Proc.Dispose();
            }
            JoinAudio(a);
            EndPeriod();
        }

        void FinishNow()
        {
            FreezeClock();
            double end;
            lock (gate) end = endT;
            lock (clockLock) heldPos = Math.Min(heldPos, end);
            StopRuns();
            bool raise;
            lock (gate)
            {
                raise = state == PlayState.Playing;
                if (raise) state = PlayState.Ended;
                wantPlay = false;
            }
            if (raise) { Action e = Ended; if (e != null) e(); }
        }

        // With Repeat on, the end is a seek back to LoopStart (unless that already happened a moment ago: a file with nothing to show).
        void EndOrLoop()
        {
            bool again;
            double to;
            lock (gate)
            {
                again = loop && wantPlay && state == PlayState.Playing && !stopPending && !seekPending;
                to = loopStart;
            }
            if (again && loopWatch.IsRunning && loopWatch.ElapsedMilliseconds < 200)
            {
                // A run that showed something may be as short as the start-up of FFmpeg; only an empty one stops the loop.
                VideoRun v = vrun;
                bool shown = false;
                if (v != null) lock (v.Sync) shown = v.Frames > 0;
                if (!shown) again = false;
            }
            if (!again) { FinishNow(); return; }
            loopWatch.Restart();
            Interlocked.Increment(ref loops);
            FreezeClock();
            StopRuns();
            lock (gate) QueueSeek(to);
            wake.Set();
        }

        // The picture at t, decoded on its own (the position is not touched): for a paused window and for stepping by frames.
        // Only the latest request is answered, and nothing arrives after Dispose.
        public void Still(double t)
        {
            int w, h, sc, req;
            lock (gate)
            {
                if (disposed) return;
                w = decodeW; h = decodeH; sc = scaling;
                req = Interlocked.Increment(ref stillReq);
            }
            if (w == 0) { w = info.Width; h = info.Height; }
            lock (stillLock) { if (stillProc != null) stillProc.Kill(); }
            ThreadPool.QueueUserWorkItem(delegate { StillWork(req, Math.Max(0, t - 0.25 / info.Fps), w, h, sc); });
        }

        void StillWork(int req, double ss, int w, int h, int sc)
        {
            PlayerChild c = null;
            try
            {
                // A time past the last picture gives nothing: the pictures before it are tried (three frames back at most).
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    double at = Math.Max(0, ss - attempt / info.Fps);
                    lock (stillLock)
                    {
                        lock (gate) { if (disposed || req != stillReq) return; }
                        c = Spawn(PlayerArgs.Still(src, at, w, h, info.Width, info.Height, sc));
                        stillProc = c;
                    }
                    PlayerFrame f = new PlayerFrame();
                    f.Width = w;
                    f.Height = h;
                    f.Data = new byte[w * h * 4];
                    int n = ReadFull(c.Out, f.Data, f.Data.Length);
                    c.Exit();
                    bool current;
                    lock (gate) current = !disposed && req == stillReq;
                    if (!current) return;
                    if (n == f.Data.Length)
                    {
                        Action<PlayerFrame> h2 = StillReady;
                        if (h2 != null) h2(f);
                        return;
                    }
                    c.Kill();
                    lock (stillLock) { if (stillProc == c) stillProc = null; }
                    c.Dispose();
                    c = null;
                    if (at <= 0) return;
                }
            }
            catch (Exception ex) { ShotStack.Log("Reproductor: fotograma " + ex.Message); }
            finally
            {
                if (c != null)
                {
                    c.Kill();
                    lock (stillLock) { if (stillProc == c) stillProc = null; }
                    c.Dispose();
                }
            }
        }

        void Fail(string message)
        {
            StopRuns();
            lock (gate)
            {
                state = PlayState.Stopped;
                wantPlay = false;
            }
            Action<string> e = Error;
            if (e != null) e(message);
        }

        // One engine tick with the clock running: skip, end, and show the picture that is due.
        void Present()
        {
            VideoRun v = vrun;
            if (v == null) return;
            double pos = Position, end;
            lock (gate) end = endT;
            Func<double, double> skip = Skip;
            if (skip != null)
            {
                double to = pos;
                try { to = skip(pos); } catch (Exception) { }
                if (double.IsPositiveInfinity(to)) { EndOrLoop(); return; }
                if (to > pos + 0.002)
                {
                    // A boundary between two frames: the first frame at or after it, or the run would align back before it.
                    if (Align(to) < to - 0.002) to = Math.Ceiling(to * v.Fps - 1e-6) / v.Fps;
                    lock (gate) QueueSeek(to);
                    return;
                }
            }
            if (pos >= end) { EndOrLoop(); return; }
            double frameDur = v.Speed / v.Fps;
            PlayerFrame f = null;
            lock (v.Sync)
            {
                while (v.Ready.Count > 0)
                {
                    PlayerFrame head = v.Ready[0];
                    if (head.Time > pos + 1e-4 || head.Time >= end) break;
                    // Late by more than a frame and a half with a newer one already due: that one is skipped.
                    if (v.Ready.Count > 1 && v.Ready[1].Time <= pos + 1e-4 && pos - head.Time > LateFrames * frameDur)
                    {
                        v.Ready.RemoveAt(0);
                        v.Free.Add(head);
                        Interlocked.Increment(ref dropped);
                        Monitor.PulseAll(v.Sync);
                        continue;
                    }
                    f = head;
                    v.Ready.RemoveAt(0);
                    break;
                }
            }
            if (f != null)
            {
                Deliver(f);
                lock (v.Sync)
                {
                    v.Free.Add(f);
                    Monitor.PulseAll(v.Sync);
                }
                return;
            }
            bool done, failed;
            double lastEnd;
            lock (v.Sync)
            {
                done = v.Eof && v.Ready.Count == 0;
                failed = done && v.Frames == 0 && v.ExitCode != 0;
                lastEnd = v.T0 + v.Frames * frameDur;
            }
            if (failed) { CheckVideoFailed(v); return; }
            if (done && pos >= lastEnd - 1e-4) EndOrLoop();
        }

        // Under the gate, so a command that returned is never followed by a picture from before it.
        void Deliver(PlayerFrame f)
        {
            lock (gate)
            {
                if (disposed || stopPending || seekPending || !wantPlay) return;
                Action<PlayerFrame> h = FrameReady;
                Interlocked.Increment(ref shown);
                if (h == null) return;
                try { h(f); }
                catch (Exception ex) { ShotStack.Log("Reproductor: " + ex.Message); }
            }
        }
    }
}
