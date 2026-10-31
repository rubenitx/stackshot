// Stackshot - FFmpeg command line of a video export from the editor, as a pure function of the user's choices.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    // What one export does: source, crop, backdrop, kept ranges and options.
    internal sealed class ExportSpec
    {
        public string Source, Overlay, Output;     // Overlay: PNG with the backdrop and marks, or null
        public Rectangle Crop;
        public bool Backdrop;
        public Size Frame;                         // with a backdrop, the padded frame and where the video sits in it
        public Rectangle Inner;
        public List<double[]> Keep;                // null: the whole file
        public double Speed = 1;
        public int MaxHeight;
        public bool ToGif, SourceIsGif, HasAudio, RgbSource;
        public int Level;                          // export quality: 0 maximum, 1 high, 2 light, 3 and 4 a size limit (TargetBytes)
        public int AudioTrack;                     // the sound track being listened to (0 the first); joined clips: VideoExport.ClipTrack
        public double Seconds;                     // the output's length: what a size limit divides its bytes by
        public double Fit = 1;                     // share of that bitrate used: below 1 on a retry of a file that came out too big
        public double Fps = 60;
        // Several videos joined: Keep is then on the joined timeline and every piece is brought to the canvas of the first one.
        public List<VideoClip> Clips;
        public Size Canvas;
    }

    // Set from another thread to make the Run calls of the thread that owns it stop: the running FFmpeg is killed.
    internal sealed class ExportAbort
    {
        public volatile bool Set;
    }

    internal static class VideoExport
    {
        [ThreadStatic] internal static ExportAbort Abort;

        // RGB to YUV with the BT.709 matrix (what players assume for screen video) at full precision; swscale would
        // otherwise use BT.601 and leave the result untagged, so a Cine master or a GIF plays back with shifted colours.
        internal const string RgbToYuv = "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int+full_chroma_inp";

        // The 4:2:0 chroma that this conversion creates sits between the luma samples (swscale's way), said so in the
        // stream: without it H.264 means "left", and players that follow it would shift every colour edge half a pixel.
        internal const string CentredChroma = "scale=out_chroma_loc=center,";

        // Written in every MP4 instead of what the source said about itself (a master describes itself as lossless).
        internal const string ShareComment = "Stackshot: MP4 para compartir (H.264 y AAC, con p\u00E9rdida)";

        // Only when every colour comes from RGB here; a YUV source keeps the chroma it had (and its implicit location).
        internal static bool ChromaMadeHere(ExportSpec s)
        {
            if (s.ToGif) return false;
            if (s.Clips == null || s.Clips.Count < 2) return s.RgbSource;
            foreach (VideoClip c in s.Clips) if (!c.Rgb) return false;
            return true;
        }

        // Runs FFmpeg and waits up to timeoutMs (then kills it, returning -1). Stderr is read asynchronously so the
        // timeout works even if FFmpeg hangs; the process dies with Stackshot.
        internal static int Run(string ff, string args, int timeoutMs, out string stderr)
        {
            return Run(ff, args, timeoutMs, null, out stderr);
        }

        // Same, handing every line FFmpeg prints on stdout to onLine (on a background thread) when it is not null.
        internal static int Run(string ff, string args, int timeoutMs, Action<string> onLine, out string stderr)
        {
            StringBuilder sb = new StringBuilder();
            ExportAbort abort = Abort;
            if (abort != null && abort.Set) { stderr = ""; return -1; }
            using (Process p = new Process())
            {
                p.StartInfo.FileName = ff;
                p.StartInfo.Arguments = "-nostdin " + args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = onLine != null;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sb) { if (sb.Length < 16000) sb.AppendLine(e.Data); }
                };
                if (onLine != null)
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) onLine(e.Data); };
                // FFmpeg gone (an antivirus quarantined it, a custom path that no longer exists): a failure to report,
                // never an exception on the caller's worker thread.
                try { p.Start(); }
                catch (Exception ex) { stderr = I18n.T("No se pudo iniciar FFmpeg: ", "Could not start FFmpeg: ") + ex.Message; return -1; }
                ChildJob.Add(p);
                p.BeginErrorReadLine();
                if (onLine != null) p.BeginOutputReadLine();
                bool exited = false;
                for (int waited = 0; waited < timeoutMs && !(abort != null && abort.Set); )
                {
                    int slice = Math.Min(200, timeoutMs - waited);
                    if (p.WaitForExit(slice)) { exited = true; break; }
                    waited += slice;
                }
                if (!exited) { try { p.Kill(); } catch { } }
                p.WaitForExit(2000); // drain the remaining output
                lock (sb) stderr = sb.ToString();
                return exited ? p.ExitCode : -1;
            }
        }

        internal static string Seconds(double t)
        {
            return Math.Max(0, t).ToString("0.000", CultureInfo.InvariantCulture);
        }

        static string Number(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // The frame rate of an export at this speed: a speed-up keeps every source frame (30 fps at 2x plays at 60)
        // up to 60 fps, or the source rate when that is higher; a slow-down keeps the source rate (frames repeat).
        internal static double OutFps(double fps, double speed)
        {
            if (fps <= 0) return 0;
            if (speed <= 1) return fps;
            return Math.Min(fps * speed, Math.Max(fps, 60));
        }

        // A rate as FFmpeg should read it: the NTSC rates exactly (30000/1001), others as a decimal.
        internal static string Rate(double fps)
        {
            foreach (int n in new int[] { 24, 30, 48, 60, 120 })
                if (Math.Abs(fps - n * 1000.0 / 1001) < 0.006) return n * 1000 + "/1001";
            return Number(fps);
        }

        // The pixel format FFmpeg reports for the first video stream ("gbrp", "yuv420p", "bgra"...), or null.
        internal static string PixelFormat(string probe)
        {
            Match m = Regex.Match(probe ?? "", @"Stream #0:\d+[^\n]*?: Video: [^,\n]+, ([a-z0-9_]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        // Cine masters (gbrp), GIFs (pal8/bgra) and any other RGB video need an explicit conversion to YUV.
        internal static bool IsRgb(string path, string probe)
        {
            if (string.Equals(Path.GetExtension(path ?? ""), ".gif", StringComparison.OrdinalIgnoreCase)) return true;
            return IsRgbFormat(path, PixelFormat(probe));
        }

        // The same from a pixel format name already read (MediaInfo.PixelFormat).
        internal static bool IsRgbFormat(string path, string f)
        {
            if (string.Equals(Path.GetExtension(path ?? ""), ".gif", StringComparison.OrdinalIgnoreCase)) return true;
            if (f == null) return false;
            return f.StartsWith("gbr") || f.StartsWith("rgb") || f.StartsWith("bgr") || f.StartsWith("argb") || f.StartsWith("abgr") ||
                   f.StartsWith("0rgb") || f.StartsWith("0bgr") || f.StartsWith("x2rgb") || f.StartsWith("x2bgr") || f == "pal8";
        }

        // Only an MP4 (or a GIF exported as GIF) is ready to share as it is; anything else, such as a lossless Cine
        // master, gets a new file even with no edits.
        internal static bool NeedsTranscode(string path, bool toGif)
        {
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return toGif ? ext != ".gif" : ext != ".mp4";
        }

        // The output size after the optional shrink (never upscaled; H.264 needs even sizes).
        internal static Size OutputSize(ExportSpec s)
        {
            Size frame = s.Backdrop ? s.Frame : s.Crop.Size;
            if (s.MaxHeight > 0 && frame.Height > s.MaxHeight)
                frame = new Size(Math.Max(2, (int)Math.Round(frame.Width * (double)s.MaxHeight / frame.Height)) & ~1, s.MaxHeight);
            return frame;
        }

        // H.264 levels (Annex A) with the High profile's limits: level x10, macroblocks per second, macroblocks per frame,
        // bitrate (kbit/s) and buffer (kbit), both 1.25 times the Main ones.
        static readonly int[][] Levels = {
            new int[] { 41, 245760, 8192, 62500, 78125 },
            new int[] { 42, 522240, 8704, 62500, 78125 },
            new int[] { 50, 589824, 22080, 168750, 168750 },
            new int[] { 51, 983040, 36864, 300000, 300000 },
            new int[] { 52, 2073600, 36864, 300000, 300000 },
            // 8K, or 4K above 60 fps (levels 6.x, in H.264 since 2016): few devices play them, but the file says so.
            new int[] { 60, 4177920, 139264, 300000, 300000 },
            new int[] { 61, 8355840, 139264, 600000, 600000 },
            new int[] { 62, 16711680, 139264, 1000000, 1000000 } };

        // The lowest of those levels that holds the frame at this rate (x10: 41 ... 62), or 0 above 6.2. Never below 4.1,
        // which practically every phone, browser and TV decodes; a lower one would only lower the ceiling.
        internal static int H264Level(Size size, double fps)
        {
            long w = (size.Width + 15) / 16, h = (size.Height + 15) / 16, mbs = w * h;
            double rate = fps > 0 ? fps : 60;
            foreach (int[] l in Levels)
                if (mbs <= l[2] && w * w <= 8L * l[2] && h * h <= 8L * l[2] && mbs * rate <= l[1]) return l[0];
            return 0;
        }

        // The H.264 settings of every export: CRF by quality level (0 maximum, 1 high, 2 light), preset slow up to 2.4 MP
        // and medium above, and the level the frame needs. Given the level, x264 keeps the reference frames within it
        // (a 1080p "slow" would otherwise ask for 5.0), and the bitrate ceiling is that level's, at most 160 Mb/s, with the
        // buffer at most twice that: a file any decoder of the level must play, and fast scrolling text keeps its quality.
        // (A clip shorter than the buffer can average more than the ceiling: the decoder's buffer model allows it.)
        internal static string ShareCodec(int level, Size size, double fps)
        {
            return ShareCodec(level, size, fps, 0);
        }

        // videoKbps: the bitrate of a size-limited export (TargetBytes), encoded in two passes; 0 for the CRF levels.
        internal static string ShareCodec(int level, Size size, double fps, int videoKbps)
        {
            int crf = level <= 0 ? 10 : level == 1 ? 14 : 20;
            long pixels = (long)size.Width * size.Height;
            int lv = H264Level(size, fps), maxrate = 160000, bufsize = 300000;
            foreach (int[] l in Levels)
                if (l[0] == lv) { maxrate = Math.Min(l[3], 160000); bufsize = Math.Min(l[4], 2 * maxrate); }
            string lvArg = lv == 0 ? "" : " -level:v " + (lv / 10) + "." + (lv % 10);
            string head = "-c:v libx264 -preset " + (pixels > 2400000 ? "medium" : "slow");
            string tail = " -g " + Math.Max(2, (int)Math.Round((fps > 0 ? fps : 60) * 2));
            if (TargetBytes(level) > 0 && videoKbps > 0)
            {
                // A size to fit: the average bitrate over two passes, peaks allowed up to twice it (within the level). A
                // short clip could afford more than the level's ceiling: it gets the ceiling and comes out smaller.
                int kb = Math.Min(videoKbps, maxrate), peak = Math.Min(maxrate, kb * 2);
                return head + " -b:v " + kb + "k -profile:v high" + lvArg + tail + " -maxrate " + peak + "k -bufsize " + Math.Min(bufsize, peak * 2) + "k";
            }
            return head + " -crf " + crf + " -profile:v high" + lvArg + tail + " -maxrate " + maxrate + "k -bufsize " + bufsize + "k";
        }

        internal static string ShareAudioCodec(int level)
        {
            return "-c:a aac -b:a " + (level <= 0 ? "320k" : level == 1 ? "256k" : level == 2 ? "160k" : "128k") + " -ar 48000";
        }

        // The size limits of the "16 MB" and "25 MB" levels (WhatsApp and most mail): a little under, as each counts
        // megabytes its own way and the container adds a little.
        internal static long TargetBytes(int level)
        {
            return level == 3 ? 15500000L : level == 4 ? 24000000L : 0;
        }

        // The video bitrate (kbit/s) that fits the export in its size limit: what is left after the sound and 2 % for the
        // container, over the output's length (ExportSpec.Seconds). 0 when there is no limit or no length.
        internal static int TargetKbps(ExportSpec s)
        {
            long bytes = TargetBytes(s.Level);
            if (bytes <= 0 || s.Seconds <= 0 || s.ToGif) return 0;
            double audio = s.HasAudio ? 128000 : 0;
            double bps = (bytes * 8 * 0.98 / s.Seconds - audio) * s.Fit;
            return (int)Math.Max(1, Math.Floor(bps / 1000));
        }

        // A size-limited export that came out too big (x264's two passes can miss by several per cent on hard content):
        // the bitrate is scaled down by how much it missed, and a little more, for another second pass. False when the
        // file is within its limit (or has none).
        internal static bool Refit(ExportSpec s, string output)
        {
            long limit = TargetBytes(s.Level);
            if (limit <= 0 || s.ToGif) return false;
            long size;
            try { size = new FileInfo(output).Length; }
            catch (Exception) { return false; }
            if (size <= limit) return false;
            s.Fit = Math.Max(0.3, s.Fit * limit / size * 0.97);
            return true;
        }

        // Runs an export's passes (one, or two for a size limit) and, while a size-limited file came out too big, the
        // second pass again with a lower bitrate (twice at most). progress(pass, passes, line) gets FFmpeg's progress lines.
        // The exit code of the last run; the pass log is left for the caller to delete.
        internal static int RunPasses(string ff, ExportSpec s, List<string> passes, string log, int timeoutMs, Action<int, int, string> progress, out string stderr)
        {
            stderr = "";
            int code = 0;
            for (int i = 0; i < passes.Count && code == 0 && !(Abort != null && Abort.Set); i++)
                code = Run(ff, passes[i], timeoutMs, Progress(progress, i, passes.Count), out stderr);
            for (int tries = 0; tries < 2 && passes.Count == 2 && code == 0 && !(Abort != null && Abort.Set) && Refit(s, s.Output); tries++)
            {
                ShotStack.Log("Exportar: m\u00E1s grande que el l\u00EDmite, segunda pasada otra vez al " + (s.Fit * 100).ToString("0", CultureInfo.InvariantCulture) + " %");
                code = Run(ff, Arguments(s, 2, log), timeoutMs, Progress(progress, 1, 2), out stderr);
            }
            return code;
        }

        static Action<string> Progress(Action<int, int, string> progress, int pass, int count)
        {
            if (progress == null) return null;
            return delegate(string line) { progress(pass, count, line); };
        }

        // The longest video a size limit takes at TargetMinKbps, as a few words: "unos 7 min".
        internal static string MaxLength(int level, bool audio)
        {
            long bytes = TargetBytes(level);
            if (bytes <= 0) return "";
            double s = bytes * 8 * 0.98 / (TargetMinKbps * 1000.0 + (audio ? 128000 : 0));
            return s >= 90 ? I18n.Fmt("unos {0} min", "about {0} min", (int)Math.Floor(s / 60)) : I18n.Fmt("unos {0} s", "about {0} s", (int)Math.Floor(s));
        }

        // Too long for its size limit: under this bitrate (kbit/s) a screen video is a blur, so the export is refused.
        internal const int TargetMinKbps = 150;

        // The FFmpeg runs of an export: one, or for a size limit the two passes of x264 (log: their shared log file, which
        // the caller deletes afterwards with DeletePassLog).
        internal static List<string> Passes(ExportSpec s, string log)
        {
            List<string> r = new List<string>();
            if (TargetKbps(s) > 0) { r.Add(Arguments(s, 1, log)); r.Add(Arguments(s, 2, log)); }
            else r.Add(Arguments(s));
            return r;
        }

        internal static void DeletePassLog(string log)
        {
            if (string.IsNullOrEmpty(log)) return;
            foreach (string suffix in new string[] { "-0.log", "-0.log.mbtree", "-0.log.temp", "-0.log.mbtree.temp" })
                try { if (File.Exists(log + suffix)) File.Delete(log + suffix); } catch (Exception) { }
        }

        // The matrix name the scale filter takes for a clip's colour tag; an untagged one is read the way players do
        // (BT.709 from 720p or 1280 wide, BT.601 below).
        internal static string ClipMatrix(string tag, int width, int height)
        {
            switch (tag)
            {
                case "bt709": return "bt709";
                case "bt470bg":
                case "smpte170m": return "smpte170m";
                case "smpte240m": return "smpte240m";
                case "fcc": return "fcc";
                case "bt2020nc":
                case "bt2020c": return "bt2020";
            }
            return height >= 720 || width >= 1280 ? "bt709" : "smpte170m";
        }

        // The sound track of a joined clip: the chosen one (numbered as in the first clip) when the clip has it, else
        // its default one, the way the editor plays it.
        internal static int ClipTrack(VideoClip c, int track)
        {
            if (c == null || c.Info == null || c.Info.Audio.Count == 0) return 0;
            if (track >= 0 && track < c.Info.Audio.Count) return track;
            foreach (AudioTrackInfo a in c.Info.Audio) if (a.Default) return a.Index;
            return 0;
        }

        // A YUV clip that is not BT.709 limited range is converted before it is joined, as the joined file is tagged BT.709.
        static string ToBt709(VideoClip c)
        {
            if (c.Matrix == null || (c.Matrix == "bt709" && !c.FullRange)) return "";
            return "scale=in_color_matrix=" + c.Matrix + ":in_range=" + (c.FullRange ? "pc" : "tv") + ":out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int+full_chroma_inp,format=yuv444p,";
        }

        // One input per piece of the joined clips, each brought to the canvas of the first clip: RGB sources converted
        // to YUV with BT.709, scaled to fit, padded with black, square pixels, the canvas frame rate; the sound as stereo
        // 48 kHz float (a clip with no sound gets silence of the same length). Ends in concat.
        static string ClipInputs(ExportSpec s, List<ClipSegment> segs, bool aud, out string inputs)
        {
            StringBuilder pre = new StringBuilder(), args = new StringBuilder();
            Size c = s.Canvas.IsEmpty ? new Size(s.Clips[0].Width, s.Clips[0].Height) : s.Canvas;
            string rate = Rate(s.Fps > 0 ? s.Fps : 30);
            string fit = "scale=" + c.Width + ":" + c.Height + ":force_original_aspect_ratio=decrease:flags=lanczos,pad=" + c.Width + ":" + c.Height + ":(ow-iw)/2:(oh-ih)/2:black,setsar=1,fps=" + rate;
            for (int i = 0; i < segs.Count; i++)
            {
                ClipSegment g = segs[i];
                if (g.Whole) args.Append(Recorder.SafeInput + "-i " + Recorder.Quote(g.Clip.Path) + " ");
                else args.Append(PlayerArgs.Seek(g.A - Early(g.Clip.Path, g)) + "-to " + Seconds(g.B) + " " + Recorder.SafeInput + "-i " + Recorder.Quote(g.Clip.Path) + " ");
                double e = g.Whole ? 0 : Early(g.Clip.Path, g);
                string conv = (e > 0 ? Trim(e, g.B - g.A, false) : "") + (s.ToGif ? "" : g.Clip.Rgb ? RgbToYuv + ",format=yuv444p," : ToBt709(g.Clip));
                // The converted frames still carry the old tag until they are retagged, and a later stage would undo the conversion.
                string retag = conv.StartsWith("scale=in_color_matrix=", StringComparison.Ordinal) ? ",setparams=colorspace=bt709:range=tv" : "";
                pre.Append("[" + i + ":v:0]" + conv + fit + "," + (s.ToGif ? "format=rgb24" : "format=yuv444p") + retag + "[v" + i + "];");
                if (!aud) continue;
                if (g.Clip.HasAudio) pre.Append("[" + i + ":a:" + ClipTrack(g.Clip, s.AudioTrack) + "]" + (e > 0 ? Trim(e, g.B - g.A, true) : "") + "aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo[a" + i + "];");
                else pre.Append("anullsrc=r=48000:cl=stereo:d=" + Seconds(g.B - g.A) + ",aformat=sample_fmts=fltp:channel_layouts=stereo[a" + i + "];");
            }
            for (int i = 0; i < segs.Count; i++) pre.Append("[v" + i + "]" + (aud ? "[a" + i + "]" : ""));
            pre.Append("concat=n=" + segs.Count + ":v=1:a=" + (aud ? 1 : 0) + "[cv]" + (aud ? "[ca]" : "") + ";");
            inputs = args.ToString();
            return pre.ToString();
        }

        static double Early(string path, ClipSegment g) { return PlayerArgs.Early(path, g.A); }

        // Drops what an early seek (PlayerArgs.Early) decoded before the piece starts. The end is cut here too: after such a
        // seek the input -to counts from where the demuxer landed, so it can let a little more through.
        static string Trim(double early, double length, bool audio)
        {
            string span = "start=" + Seconds(early) + ":end=" + Seconds(early + length);
            return audio ? "atrim=" + span + ",asetpts=PTS-STARTPTS," : "trim=" + span + ",setpts=PTS-STARTPTS,";
        }

        // FFmpeg puts the (cropped, padded) video below and the image with backdrop, corners and marks on top.
        internal static string Arguments(ExportSpec s)
        {
            return Arguments(s, 0, null);
        }

        // pass 1 and 2: the two runs of a size-limited export (Passes); the first writes only x264's log, without sound.
        internal static string Arguments(ExportSpec s, int pass, string log)
        {
            // The editor never keeps a piece shorter than 0.05 s; FFmpeg would refuse an empty or reversed one (-to before -ss).
            if (s.Keep != null) foreach (double[] k in s.Keep) if (!(k[1] > k[0])) throw new ArgumentException("A kept piece must end after it starts.");
            Rectangle crop = s.Crop;
            bool clips = s.Clips != null && s.Clips.Count > 1;
            List<ClipSegment> segs = clips ? ClipSeq.Segments(s.Clips, s.Keep) : null;
            int pieces = clips ? segs.Count : s.Keep != null ? s.Keep.Count : 1;
            // A trimmed MPEG-TS/PS source seeks early, and its pieces go through concat to drop the extra frames.
            bool early = !clips && s.Keep != null && s.Keep.Exists(delegate(double[] k) { return PlayerArgs.Early(s.Source, k[0]) > 0; });
            // Joined pieces (cuts or several videos) come out of concat, with timestamps off the source grid.
            bool multi = clips || pieces > 1 || early;
            Size frame = s.Backdrop ? s.Frame : crop.Size;
            Size output = OutputSize(s);

            // Composited at full chroma resolution, so the backdrop's rounded corners and the marks keep clean edges;
            // the overlay image is converted with the same BT.709 matrix as the video.
            string vin = multi ? "[cv]" : "[0:v]";
            int topIn = multi ? pieces : 1;
            string toYuv = s.ToGif || clips ? "" : (s.RgbSource ? RgbToYuv + ",format=yuv444p," : "format=yuv444p,");
            string f = vin + toYuv + "crop=" + crop.Width + ":" + crop.Height + ":" + crop.X + ":" + crop.Y;
            if (s.Backdrop) f += ",pad=" + frame.Width + ":" + frame.Height + ":" + s.Inner.X + ":" + s.Inner.Y + ":black";
            if (s.Overlay != null)
            {
                if (s.ToGif) f += "[v];[v][" + topIn + ":v]overlay=0:0:format=auto";
                else f = "[" + topIn + ":v]scale=out_color_matrix=bt709:out_range=tv,format=yuva444p[top];" + f + "[v];[v][top]overlay=0:0:format=yuv444";
            }
            if (s.Speed != 1) f += ",setpts=PTS/" + Number(s.Speed);
            // A constant rate after a speed change or joined pieces (their timestamps no longer fall on the source grid).
            if (!s.ToGif && s.Fps > 0 && (s.Speed != 1 || multi)) f += ",fps=" + Rate(OutFps(s.Fps, s.Speed));
            if (output != frame) f += ",scale=" + output.Width + ":" + output.Height + ":flags=lanczos";

            string args = "-hide_banner -loglevel error -nostats -progress pipe:1 -y ";
            bool aud = s.HasAudio && !s.ToGif && pass != 1;
            // The sound being listened to in the player or the editor, not always the first track.
            string track = ":a:" + Math.Max(0, s.AudioTrack);
            string pre = "";
            if (clips)
            {
                string inputs;
                pre = ClipInputs(s, segs, aud, out inputs);
                args += inputs;
            }
            else if (s.Keep != null)
            {
                // One input per kept range (seeking each, so nothing is buffered), joined with concat.
                foreach (double[] k in s.Keep)
                    args += PlayerArgs.Seek(k[0] - PlayerArgs.Early(s.Source, k[0])) + "-to " + Seconds(k[1]) + " " + Recorder.SafeInput + "-i " + Recorder.Quote(s.Source) + " ";
                if (multi)
                {
                    string labels = "";
                    for (int i = 0; i < s.Keep.Count; i++)
                    {
                        double e = PlayerArgs.Early(s.Source, s.Keep[i][0]), len = s.Keep[i][1] - s.Keep[i][0];
                        if (e <= 0) { labels += "[" + i + ":v]" + (aud ? "[" + i + track + "]" : ""); continue; }
                        pre += "[" + i + ":v]" + Trim(e, len, false).TrimEnd(',') + "[t" + i + "];" + (aud ? "[" + i + track + "]" + Trim(e, len, true).TrimEnd(',') + "[u" + i + "];" : "");
                        labels += "[t" + i + "]" + (aud ? "[u" + i + "]" : "");
                    }
                    pre += labels;
                    pre += "concat=n=" + s.Keep.Count + ":v=1:a=" + (aud ? 1 : 0) + "[cv]" + (aud ? "[ca]" : "") + ";";
                }
            }
            else args += Recorder.SafeInput + "-i " + Recorder.Quote(s.Source);
            if (s.Overlay != null) args += " -i " + Recorder.Quote(s.Overlay);
            if (s.ToGif)
            {
                // Per-GIF palette and light dithering, at most 960 px wide; videos drop to 15 fps. The palette counts every
                // pixel: "diff" only trains on what changes, so a red clip turning green exported all red.
                int gw = Math.Min(output.Width, 960) & ~1;
                f += (s.SourceIsGif ? "" : ",fps=15") + ",scale=" + gw + ":-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=full[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle[o]";
                return args + " -filter_complex \"" + pre + f + "\" -map \"[o]\" -loop 0 " + Recorder.Quote(s.Output);
            }
            // Always tagged BT.709 with the sRGB transfer when the colours were converted here (joined videos always are);
            // YUV sources keep the tags they came with.
            f += (s.RgbSource || clips ? "," + Recorder.Tags : "") + "," + (ChromaMadeHere(s) ? CentredChroma : "") + "format=yuv420p[o]";
            // The sound follows the trim and the speed (atempo keeps the pitch).
            string audio = "-an";
            if (aud && s.Speed != 1)
            {
                f += ";" + (multi ? "[ca]" : "[0" + track + "]") + "atempo=" + Number(s.Speed) + "[ao]";
                audio = "-map \"[ao]\" " + ShareAudioCodec(s.Level) + " -ac 2";
            }
            else if (aud) audio = "-map " + (multi ? "\"[ca]\"" : "0" + track) + " " + ShareAudioCodec(s.Level) + " -ac 2";
            // Every frame keeps its time: FFmpeg's default for MP4 drops frames of a variable rate source (a phone video);
            // the rate only changes where a filter above says so. The master's description and track names stay with it.
            string video = " -filter_complex \"" + pre + f + "\" -map \"[o]\" -fps_mode passthrough " + ShareCodec(s.Level, output, OutFps(s.Fps, s.Speed), TargetKbps(s));
            if (pass == 1) return args + video + " -pass 1 -passlogfile " + Recorder.Quote(log) + " -an -f null NUL";
            return args + video + (pass == 2 ? " -pass 2 -passlogfile " + Recorder.Quote(log) : "") + " " + audio +
                   " -metadata comment=\"" + ShareComment + "\"" + (aud ? " -metadata:s:a:0 title=" : "") + " -movflags +faststart " + Recorder.Quote(s.Output);
        }
    }
}
