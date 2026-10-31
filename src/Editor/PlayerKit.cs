// Stackshot - The player's arithmetic: window and decode sizes, times, frame steps and the info panel's text.
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
    // The parts of the player that need no window: sizes, times, frame arithmetic and the text of the info panel.
    internal static class PlayerKit
    {
        public static readonly double[] Speeds = { 0.25, 0.5, 0.75, 1, 1.25, 1.5, 2 };
        // The editor reads the same containers as the player.
        public static readonly string[] EditorFormats = ShotStack.VideoExtensions;
        public static string CineDir = Path.Combine(Settings.DataDir, "cine");   // tests point it elsewhere

        static string Dec(double v, string f) { return v.ToString(f, CultureInfo.InvariantCulture).Replace('.', I18n.IsSpanish ? ',' : '.'); }

        // 1:07 or 1:02:03.
        public static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            int t = (int)Math.Floor(seconds);
            int h = t / 3600, m = t / 60 % 60, s = t % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m + ":" + s.ToString("00");
        }

        public static string SpeedText(double v) { return Dec(v, "0.##") + "\u00D7"; }

        // The frame on screen at a position: the last one that starts at or before it.
        public static int FrameAt(double pos, double fps) { return (int)Math.Floor(Math.Max(0, pos) * fps + 1e-3); }

        public static int LastFrame(double duration, double fps)
        {
            if (duration <= 0 || fps <= 0) return int.MaxValue;
            return Math.Max(0, (int)Math.Ceiling(duration * fps - 1e-3) - 1);
        }

        // Where a step of one frame (dir -1 or 1) from pos lands: the start of the next or previous frame.
        public static double StepTime(double pos, double fps, double duration, int dir)
        {
            int n = FrameAt(pos, fps) + (dir < 0 ? -1 : 1);
            n = Math.Max(0, Math.Min(LastFrame(duration, fps), n));
            return n / fps;
        }

        // The video scaled to sit inside the box, centred.
        public static Rectangle Letterbox(Size video, Size box)
        {
            if (video.Width <= 0 || video.Height <= 0 || box.Width <= 0 || box.Height <= 0) return Rectangle.Empty;
            double k = Math.Min(box.Width / (double)video.Width, box.Height / (double)video.Height);
            int w = Math.Max(1, (int)Math.Round(video.Width * k)), h = Math.Max(1, (int)Math.Round(video.Height * k));
            return new Rectangle((box.Width - w) / 2, (box.Height - h) / 2, w, h);
        }

        // The size to decode at for a window: what the picture takes inside the box, even, never above native. Empty means native.
        public static Size DecodeSize(Size video, Size box)
        {
            Rectangle r = Letterbox(video, box);
            if (r.Width >= video.Width || r.Height >= video.Height || r.IsEmpty) return Size.Empty;
            return new Size(Math.Max(2, r.Width & ~1), Math.Max(2, r.Height & ~1));
        }

        // Where the picture goes in the box: the video itself, centred, when it fits (one video pixel per screen pixel, the
        // sharpest), else scaled down to fit. With fill it is also enlarged to fill the box.
        public static Rectangle Picture(Size video, Size box, bool fill)
        {
            Rectangle r = Letterbox(video, box);
            if (fill || r.IsEmpty || (r.Width <= video.Width && r.Height <= video.Height)) return r;
            return new Rectangle((box.Width - video.Width) / 2, (box.Height - video.Height) / 2, video.Width, video.Height);
        }

        // How big the picture is next to the video itself, for the info panel. grow: the window could still get bigger.
        public static string ScaleNote(Size video, Rectangle picture, bool grow)
        {
            if (video.Width <= 0 || video.Height <= 0 || picture.Width <= 0) return "";
            if (picture.Width == video.Width && picture.Height == video.Height) return I18n.T("100 % (tama\u00F1o real: un p\u00EDxel del v\u00EDdeo, un p\u00EDxel de la pantalla)", "100 % (actual size: 1 video pixel = 1 screen pixel)");
            double pc = picture.Width * 100.0 / video.Width;
            if (pc > 100) return Math.Max(101, (int)Math.Ceiling(pc)) + I18n.T(" % (ampliado para llenar la ventana)", " % (scaled to fill window)");
            return Math.Min(99, (int)Math.Floor(pc)) + " % (" + (grow ? I18n.T("la ventana es m\u00E1s peque\u00F1a que el v\u00EDdeo: maxim\u00EDzala o pulsa F", "window is smaller than video: maximize or press F") : I18n.T("el v\u00EDdeo es m\u00E1s grande que la pantalla", "video is larger than screen")) + ")";
        }

        // The client size of a window for a video: the video at its real size when it fits the work area with the window's
        // frame around it (frame: the outer size minus the client size), else the largest that fits; at least minW wide
        // (the bar needs it) when the work area allows.
        public static Size WindowFor(Size video, Rectangle work, int minW, Size frame) { return WindowFor(video, work, minW, frame, true); }

        // fill: the picture is enlarged to fill the window, so a narrow video's window keeps its shape; at real size it
        // only gets as wide as the bar needs (and at least 16:9 of that tall), with no black above and below.
        public static Size WindowFor(Size video, Rectangle work, int minW, Size frame, bool fill)
        {
            if (video.Width <= 0 || video.Height <= 0) return new Size(Math.Max(minW, 640), 360);
            double aw = Math.Max(1, work.Width - frame.Width), ah = Math.Max(1, work.Height - frame.Height);
            double k = Math.Min(1.0, Math.Min(aw / video.Width, ah / video.Height));
            double w = video.Width * k, h = video.Height * k;
            if (w < minW)
            {
                if (fill)
                {
                    // Never past what the work area allows.
                    double up = Math.Min(minW / w, Math.Min(aw / w, ah / h));
                    w *= up;
                    h *= up;
                }
                else
                {
                    w = Math.Min(minW, aw);
                    h = Math.Max(h, Math.Min(minW * 9.0 / 16, ah));
                }
            }
            return new Size((int)Math.Min(aw, Math.Round(w)), (int)Math.Min(ah, Math.Round(h)));
        }

        public static Size WindowFor(Size video, Rectangle work, int minW) { return WindowFor(video, work, minW, Size.Empty); }

        static string Bytes(long b)
        {
            if (b < 1024 * 1024) return Dec(b / 1024.0, "0.#") + " KB";
            if (b < 1024L * 1024 * 1024) return Dec(b / 1048576.0, "0.#") + " MB";
            return Dec(b / 1073741824.0, "0.##") + " GB";
        }

        static string Container(string raw)
        {
            Match m = Regex.Match(raw ?? "", @"Input #0, ([^,\n]+(?:,[a-z0-9_]+)*), from ");
            if (!m.Success) return null;
            string f = m.Groups[1].Value;
            if (f.IndexOf("matroska", StringComparison.Ordinal) >= 0) return "Matroska / WebM";
            if (f.StartsWith("mov", StringComparison.Ordinal)) return "MP4 / MOV";
            if (f == "avi") return "AVI";
            if (f == "asf") return "ASF (WMV)";
            if (f == "flv") return "FLV";
            if (f == "mpegts") return "MPEG-TS";
            if (f == "mpeg") return "MPEG-PS";
            if (f == "gif") return "GIF";
            if (f == "ogg") return "Ogg";
            return f;
        }

        // Where the Cine report of a master is kept.
        public static string CineReportPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? "");
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return Path.Combine(CineDir, (name.Length == 0 ? "cine" : name) + ".json");
        }

        public static string ReadCineReport(string path)
        {
            try
            {
                string f = CineReportPath(path);
                return File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : null;
            }
            catch (Exception) { return null; }
        }

        // A master says so in its description and is RGB (gbrp). Earlier MP4 exports copied that description, and a lossy
        // YUV file must never be shown as lossless.
        public static bool IsMaster(MediaInfo m)
        {
            return m != null && (m.Raw ?? "").IndexOf("Stackshot Cine", StringComparison.Ordinal) >= 0 &&
                   m.PixelFormat != null && m.PixelFormat.StartsWith("gbr", StringComparison.Ordinal);
        }

        // What the player's export of a whole video must be. A master has a constant rate by construction, so its rate and
        // frames are checked too; a phone video can have a variable rate, so only its size, sound, length and level are.
        internal static ShareExpect ExportExpect(MediaInfo info, ExportSpec spec)
        {
            // A piece (the marks of the bar): worked out like the editor's trims, frames counted when the rate is constant.
            if (spec.Keep != null)
            {
                double f;
                bool constant = Editor.ConstantRate(info.Raw, out f) || IsMaster(info);
                return Editor.ExpectFor(spec, info.Duration, f > 0 ? f : info.Fps, constant);
            }
            ShareExpect e = new ShareExpect();
            e.Width = spec.Crop.Width;
            e.Height = spec.Crop.Height;
            e.Seconds = info.Duration;
            e.HasAudio = spec.HasAudio;
            e.RgbSource = spec.RgbSource;
            e.CentredChroma = VideoExport.ChromaMadeHere(spec);
            e.Level = VideoExport.H264Level(spec.Crop.Size, spec.Fps);
            if (IsMaster(info) && info.Fps > 0) e.Fps = Editor.ExactFps(info.Fps);
            return e;
        }

        // FFmpeg's raw output (a decoded picture), or null when it is not exactly the expected number of bytes.
        internal static byte[] RunBytes(string ff, string args, int expected, int timeoutMs)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ff;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.ErrorDataReceived += delegate { };
                    p.Start();
                    ChildJob.Add(p);
                    p.BeginErrorReadLine();
                    MemoryStream ms = new MemoryStream(Math.Max(0, expected));
                    System.Threading.Thread reader = new System.Threading.Thread(delegate() { try { p.StandardOutput.BaseStream.CopyTo(ms); } catch (Exception) { } });
                    reader.IsBackground = true;
                    reader.Start();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch (Exception) { } }
                    reader.Join(5000);
                    byte[] b = ms.ToArray();
                    return p.HasExited && p.ExitCode == 0 && b.Length == expected ? b : null;
                }
            }
            catch (Exception ex) { ShotStack.Log("Reproductor: fotograma " + ex.Message); return null; }
        }

        // A bitmap from bgra rows, top to bottom.
        internal static Bitmap FromBgra(byte[] px, int w, int h)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(px, y * w * 4, bd.Scan0 + y * bd.Stride, w * 4);
            }
            finally { b.UnlockBits(bd); }
            return b;
        }

        // ---- where each long video was left ("seguir donde lo dejaste") ----

        // One line per video: last use (UTC ticks), file size, write time (ticks), seconds and the path. The size and time
        // tell an edited or replaced file from the one that was being watched. Tests point it elsewhere.
        public static string ResumeFile = Path.Combine(Settings.DataDir, "posiciones.txt");
        public const double ResumeMinLength = 120;   // shorter videos always start from the beginning
        const int ResumeKeep = 200;

        static string ResumeKey(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                return fi.Length + "\t" + fi.LastWriteTimeUtc.Ticks + "\t" + fi.FullName;
            }
            catch (Exception) { return null; }
        }

        static List<string[]> ResumeRead()
        {
            List<string[]> r = new List<string[]>();
            try
            {
                if (!File.Exists(ResumeFile)) return r;
                foreach (string l in File.ReadAllLines(ResumeFile, Encoding.UTF8))
                {
                    string[] f = l.Split(new char[] { '\t' }, 5);
                    if (f.Length == 5) r.Add(f);
                }
            }
            catch (Exception) { }
            return r;
        }

        // Where to start a video: where it was left, or 0 (never for a short one, nor in its last seconds).
        public static double ResumeAt(string path, double duration)
        {
            if (duration < ResumeMinLength) return 0;
            string key = ResumeKey(path);
            if (key == null) return 0;
            foreach (string[] f in ResumeRead())
            {
                // Windows paths ignore case: "Clip.mkv" and "CLIP.MKV" are the same file.
                if (!string.Equals(f[1] + "\t" + f[2] + "\t" + f[4], key, StringComparison.OrdinalIgnoreCase)) continue;
                double s;
                if (double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out s) && s >= 10 && s <= duration - 15) return s;
            }
            return 0;
        }

        // Remembers where a video was left; one watched to (nearly) the end is forgotten. A video only glanced at (under
        // 10 s, or opened and closed again) keeps the spot it had: that was not where it was left.
        public static void ResumeSave(string path, double pos, double duration)
        {
            if (Settings.ReadOnly && ResumeFile == Path.Combine(Settings.DataDir, "posiciones.txt")) return;
            string key = ResumeKey(path);
            if (key == null) return;
            bool keep = duration >= ResumeMinLength && pos >= 10 && pos <= duration - 15;
            bool ended = duration >= ResumeMinLength && pos > duration - 15;
            if (!keep && !ended) return;
            List<string[]> all = ResumeRead();
            all.RemoveAll(f => string.Equals(f[1] + "\t" + f[2] + "\t" + f[4], key, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(f[4], key.Substring(key.LastIndexOf('\t') + 1), StringComparison.OrdinalIgnoreCase));
            string[] k = key.Split(new char[] { '\t' }, 3);
            if (keep) all.Add(new string[] { DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture), k[0], k[1], pos.ToString("0.###", CultureInfo.InvariantCulture), k[2] });
            all.Sort((a, b) => string.CompareOrdinal(b[0].PadLeft(20, '0'), a[0].PadLeft(20, '0')));
            if (all.Count > ResumeKeep) all.RemoveRange(ResumeKeep, all.Count - ResumeKeep);
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (string[] f in all) sb.Append(string.Join("\t", f)).Append('\n');
                Directory.CreateDirectory(Path.GetDirectoryName(ResumeFile));
                AtomicFile.Write(ResumeFile, Encoding.UTF8.GetBytes(sb.ToString()));
            }
            catch (Exception ex) { ShotStack.Log("Reproductor: posiciones " + ex.Message); }
        }

        // Label and value pairs of the info panel.
        public static List<string[]> Describe(MediaInfo m, string path, long fileBytes, string cineJson)
        {
            List<string[]> r = new List<string[]>();
            string raw = m.Raw ?? "";
            r.Add(new string[] { I18n.T("Archivo", "File"), Path.GetFileName(path ?? "") });
            string cont = Container(raw);
            if (cont != null) r.Add(new string[] { I18n.T("Contenedor", "Container"), cont });
            Match v = Regex.Match(raw, @"Stream #\d+:\d+[^\n]*?: Video: ([^\s,]+)(?: \(([^)]*)\))?");
            if (v.Success) r.Add(new string[] { I18n.T("C\u00F3dec de v\u00EDdeo", "Video codec"), v.Groups[1].Value + (v.Groups[2].Success ? " (" + v.Groups[2].Value + ")" : "") });
            if (m.PixelFormat != null) r.Add(new string[] { I18n.T("Formato de p\u00EDxel", "Pixel format"), m.PixelFormat });
            Size seen = m.Display;
            if (m.HasVideo) r.Add(new string[] { I18n.T("Tama\u00F1o", "Size"), m.Width + " \u00D7 " + m.Height + (seen.Width == m.Width && seen.Height == m.Height ? "" :
                                                  I18n.Fmt(" (p\u00EDxeles no cuadrados {0}:{1}, se ve a {2} \u00D7 {3})", " (non-square pixels {0}:{1}, displayed as {2} \u00D7 {3})", m.SarNum, m.SarDen, seen.Width, seen.Height)) });
            if (m.Fps > 0) r.Add(new string[] { I18n.T("Fotogramas por segundo", "Frames per second"), Dec(m.Fps, "0.##") });
            if (m.Duration > 0) r.Add(new string[] { I18n.T("Duraci\u00F3n", "Duration"), Clock(m.Duration) });
            MatchCollection au = Regex.Matches(raw, @"Stream #\d+:\d+[^\n]*?: Audio: ([^\s,]+)(?: \(([^)]*)\))?[^,\n]*, (\d+) Hz, ([^,\n]+)");
            if (m.Audio.Count == 0) r.Add(new string[] { "Audio", I18n.T("Sin sonido", "No audio") });
            for (int i = 0; i < m.Audio.Count; i++)
            {
                string title = string.IsNullOrEmpty(m.Audio[i].Title) ? I18n.Fmt("Pista {0}", "Track {0}", i + 1) : Cine.TrackName(m.Audio[i].Title);
                string detail = "";
                if (i < au.Count)
                {
                    string layout = au[i].Groups[4].Value.Trim();
                    if (layout == "stereo") layout = I18n.T("est\u00E9reo", "stereo");
                    detail = au[i].Groups[1].Value + " \u00B7 " + layout + " \u00B7 " + au[i].Groups[3].Value + " Hz";
                }
                r.Add(new string[] { m.Audio.Count > 1 ? "Audio " + (i + 1) : "Audio", title + (detail.Length > 0 ? " \u00B7 " + detail : "") });
            }
            if (fileBytes > 0) r.Add(new string[] { I18n.T("Tama\u00F1o del archivo", "File size"), Bytes(fileBytes) });
            bool passed = false, known = false;
            long matched = 0;
            if (!string.IsNullOrEmpty(cineJson))
            {
                try
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    js.MaxJsonLength = int.MaxValue;
                    Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(cineJson);
                    object p;
                    if (d != null && d.TryGetValue("passed", out p) && p is bool)
                    {
                        known = true;
                        passed = (bool)p;
                        object ver;
                        Dictionary<string, object> vd;
                        object fm;
                        if (d.TryGetValue("verification", out ver) && (vd = ver as Dictionary<string, object>) != null && vd.TryGetValue("framesMatched", out fm))
                            matched = Convert.ToInt64(fm, CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception) { known = false; }
            }
            if (IsMaster(m) || known) r.Add(new string[] { I18n.T("M\u00E1ster sin p\u00E9rdidas", "Lossless master"), I18n.T("S\u00ED: cada p\u00EDxel y el sonido, tal como los entreg\u00F3 Windows", "Yes: every pixel and audio exactly as delivered by Windows") });
            if (known) r.Add(new string[] { I18n.T("Comprobaci\u00F3n", "Verification"), passed ? I18n.T("Correcta", "Passed") + (matched > 0 ? I18n.Fmt(": {0} fotogramas id\u00E9nticos", ": {0} identical frames", matched) : "") : I18n.T("No superada", "Failed") });
            return r;
        }
    }
}
