// Stackshot - Cine recording: lossless H.264 RGB video and PCM float sound in Matroska, checked frame by frame.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace Stackshot
{
    // The pixel contract: Stackshot pipes bgr0 (B, G, R and an ignored byte; desktop alpha means nothing). Every frame
    // decoded from the master must have the (R, G, B) of the frame piped to FFmpeg, in the same number and order.
    // The master is H.264 "High 4:4:4 Predictive" with qp 0, stored as gbrp, and the sound is PCM float copied
    // bit for bit. Matroska rather than MP4: Windows' own decoder only reads 4:2:0 H.264, so an .mp4 would look
    // compatible and not play.
    public static class Cine
    {
        public const string Extension = ".mkv";
        public const string InputPixelFormat = "bgr0";
        public const string StoredPixelFormat = "gbrp";
        public const long RealtimePixelRate = 1920L * 1200 * 60;   // up to this the CPU encodes lossless in real time
        public const long MinFreeBytes = 2L << 30;                    // needed to start
        public const long ReserveBytes = 512L << 20;                  // kept free while recording, on top of the remux copy

        internal const string TitleMix = "Mezcla (equipo + micr\u00F3fono)";
        internal const string TitleSystem = "Sonido del equipo (original)";
        internal const string TitleMic = "Micr\u00F3fono (original)";
        const string Comment = "Stackshot Cine: v\u00EDdeo H.264 RGB sin p\u00E9rdidas y sonido PCM de 32 bits";
        const string AudioIn = " -f f32le -sample_rate 48000 -ch_layout stereo -i ";

        // The track titles stay in the file as they are (container metadata); this is how they read on screen.
        public static string TrackName(string title)
        {
            if (I18n.IsSpanish || title == null) return title;
            if (title == TitleMix) return "Mix (computer + microphone)";
            if (title == TitleSystem) return "Computer sound (original)";
            if (title == TitleMic) return "Microphone (original)";
            return title;
        }

        public static int Gop(int fpsNum, int fpsDen)
        {
            return Math.Max(2, (int)Math.Round(2.0 * fpsNum / fpsDen));
        }

        public static string LiveArgs(int width, int height, int fpsNum, int fpsDen, string nutPath)
        {
            return "-hide_banner -loglevel error -y -f rawvideo -pix_fmt " + InputPixelFormat + " -video_size " + width + "x" + height +
                   " -framerate " + fpsNum + "/" + fpsDen + " -i - -map 0:v -c:v libx264rgb -preset ultrafast -qp 0 -g " + Gop(fpsNum, fpsDen) +
                   " -x264-params colorprim=bt709:transfer=iec61966-2-1:colormatrix=GBR:range=pc -f nut " + Recorder.Quote(nutPath);
        }

        // Nothing is re-encoded except the optional mix of both sources; the originals are copied as they are.
        public static string MasterArgs(string nutPath, string sysPath, string micPath, string voiceGain, string outPath)
        {
            string args = "-i " + Recorder.Quote(nutPath);
            int next = 1, sysIn = -1, micIn = -1;
            if (sysPath != null) { args += AudioIn + Recorder.Quote(sysPath); sysIn = next++; }
            if (micPath != null) { args += AudioIn + Recorder.Quote(micPath); micIn = next++; }
            if (sysIn >= 0 && micIn >= 0)
            {
                args += " -filter_complex \"[" + micIn + ":a]highpass=f=70" + voiceGain + "[m];[" + sysIn + ":a][m]amix=inputs=2:duration=first:dropout_transition=0:normalize=0," +
                        "alimiter=limit=0.891:attack=5:release=60:level=0:latency=1[a]\"";
                args += " -map 0:v -c:v copy -map \"[a]\" -c:a:0 pcm_f32le -map " + sysIn + ":a -c:a:1 copy -map " + micIn + ":a -c:a:2 copy";
                args += " -metadata:s:a:0 title=\"" + TitleMix + "\" -metadata:s:a:1 title=\"" + TitleSystem + "\" -metadata:s:a:2 title=\"" + TitleMic + "\"";
                args += " -disposition:a:0 default -disposition:a:1 0 -disposition:a:2 0";
            }
            else if (sysIn >= 0 || micIn >= 0)
            {
                args += " -map 0:v -c:v copy -map " + (sysIn >= 0 ? sysIn : micIn) + ":a -c:a copy";
                args += " -metadata:s:a:0 title=\"" + (sysIn >= 0 ? TitleSystem : TitleMic) + "\" -disposition:a:0 default";
            }
            else args += " -map 0:v -c:v copy";
            return args + " -metadata comment=\"" + Comment + "\" -f matroska " + Recorder.Quote(outPath);
        }

        public static string ProbeArgs(string master)
        {
            return "-hide_banner -nostdin -i " + Recorder.Quote(master) + " -map 0 -c copy -f framecrc -";
        }

        public static string VideoDecodeArgs(string master)
        {
            // passthrough: every decoded frame once, never duplicated or dropped to fit a guessed frame rate.
            return "-hide_banner -nostdin -loglevel error -i " + Recorder.Quote(master) + " -map 0:v:0 -an -fps_mode passthrough -f rawvideo -pix_fmt " + StoredPixelFormat + " -";
        }

        public static string AudioDecodeArgs(string master, int k)
        {
            return "-hide_banner -nostdin -loglevel error -i " + Recorder.Quote(master) + " -map 0:a:" + k + " -c:a pcm_f32le -f f32le -";
        }

        // The sound is cut to the length of the video, in whole 48 kHz frames.
        public static long AudioFramesFor(long videoFrames, int fpsNum, int fpsDen)
        {
            return (long)Math.Round(videoFrames * (double)fpsDen / fpsNum * AudioCapture.Rate);
        }

        public const long PeakMemoryNormal = 2L << 30, PeakMemoryHigh = 8L << 30;

        // Memory for frames waiting for the encoder (Settings.CinePeakMemory): normal, up to a quarter of the free memory
        // and 2 GiB; high, up to half of it and 8 GiB. Only what a motion peak needs is ever allocated.
        public static long PoolBudget(int level, long availablePhysical)
        {
            long avail = Math.Max(0, availablePhysical);
            return level >= 1 ? Math.Min(PeakMemoryHigh, avail / 2) : Math.Min(PeakMemoryNormal, avail / 4);
        }

        // Frames that fit in the budget. The cap (600 frames, 10 s at 60 fps, per 2 GiB) grows with the budget, so small
        // areas get a longer queue only when more memory was offered.
        public static int PoolFrames(long frameBytes, long budget)
        {
            if (frameBytes <= 0) return 8;
            budget = Math.Max(0, budget);
            long cap = Math.Max(600, 600 * budget / PeakMemoryNormal);
            return (int)Math.Max(8, Math.Min(cap, budget / frameBytes));
        }

        // How many seconds of motion peak the queue absorbs at that size and rate (for the settings page).
        public static double PeakSeconds(int level, long frameBytes, long availablePhysical, int fps)
        {
            return PoolFrames(frameBytes, PoolBudget(level, availablePhysical)) / (double)Math.Max(1, fps);
        }

        static readonly object probeLock = new object();
        static readonly HashSet<string> encoderOk = new HashSet<string>();

        // FFmpeg builds without libx264rgb exist: ask once (only a yes is remembered).
        public static bool EncoderAvailable(string ffmpeg)
        {
            if (string.IsNullOrEmpty(ffmpeg)) return false;
            lock (probeLock) if (encoderOk.Contains(ffmpeg)) return true;
            string o, e;
            int code = Capture(ffmpeg, "-hide_banner -nostdin -h encoder=libx264rgb", 15000, out o, out e);
            bool ok = code == 0 && (o + e).Contains(InputPixelFormat);
            if (ok) lock (probeLock) encoderOk.Add(ffmpeg);
            return ok;
        }

        // Free space of the drive that holds the folder; -1 if it can't be told (network paths, for instance).
        public static long FreeBytes(string folder)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace; }
            catch { return -1; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus m);

        internal static long AvailablePhysical()
        {
            try
            {
                MemoryStatus m = new MemoryStatus();
                m.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
                if (GlobalMemoryStatusEx(ref m)) return (long)m.AvailPhys;
            }
            catch { }
            return 2L << 30;
        }

        // Runs FFmpeg to the end (or the timeout), handing each line of stdout to `line` as it arrives.
        internal static int CaptureLines(string ffmpeg, string args, int timeoutMs, Action<string> line, out string stderr)
        {
            return CaptureLines(ffmpeg, args, timeoutMs, line, null, out stderr);
        }

        internal static int CaptureLines(string ffmpeg, string args, int timeoutMs, Action<string> line, PassWatch watch, out string stderr)
        {
            StringBuilder se = new StringBuilder();
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) line(e.Data); };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (se) { if (se.Length < 200000) se.AppendLine(e.Data); } };
                    p.Start();
                    ChildJob.Add(p);
                    if (watch != null) watch.Attach(p, 0);
                    try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited) { try { p.Kill(); } catch { } }
                    if (p.WaitForExit(10000)) p.WaitForExit();
                    if (watch != null) watch.Detach(p);
                    lock (se) stderr = se.ToString();
                    return exited ? p.ExitCode : -1;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return -1;
            }
        }

        // Runs FFmpeg to the end (or the timeout) keeping both outputs; used for short answers, never for streams.
        internal static int Capture(string ffmpeg, string args, int timeoutMs, out string stdout, out string stderr)
        {
            StringBuilder so = new StringBuilder(), se = new StringBuilder();
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (so) { if (so.Length < 4000000) so.AppendLine(e.Data); } };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (se) { if (se.Length < 200000) se.AppendLine(e.Data); } };
                    p.Start();
                    ChildJob.Add(p);
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited) { try { p.Kill(); } catch { } }
                    if (p.WaitForExit(10000)) p.WaitForExit();
                    lock (so) stdout = so.ToString();
                    lock (se) stderr = se.ToString();
                    return exited ? p.ExitCode : -1;
                }
            }
            catch (Exception ex)
            {
                stdout = "";
                stderr = ex.Message;
                return -1;
            }
        }

        // Before recording, on the UI thread: can this PC do it? false: nothing starts. fps: 60 or 30 (Settings.CineFps;
        // at 60 an area above the real-time rate asks first).
        public static bool Preflight(ShotStack owner, Settings s, string ffmpeg, Rectangle r, out int fps)
        {
            fps = s.CineFps == 30 ? 30 : 60;
            if (!EncoderAvailable(ffmpeg))
            {
                Dialogs.Show(I18n.T("Esta versi\u00F3n de FFmpeg no incluye el codificador sin p\u00E9rdidas (libx264rgb), que la calidad Cine necesita.\n\nActual\u00EDzalo desde los ajustes de Stackshot o elige otra calidad.",
                                    "This version of FFmpeg does not include the lossless encoder (libx264rgb), required for Cinema quality.\n\nUpdate it from Stackshot settings or choose another quality."),
                             "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            long free = FreeBytes(Settings.TempDir);
            if (free >= 0 && free < MinFreeBytes)
            {
                Dialogs.Show(I18n.Fmt("No hay espacio suficiente en el disco para grabar sin p\u00E9rdidas: quedan {0} GB y hacen falta al menos {1} GB (un minuto ocupa desde unos MB hasta varios GB seg\u00FAn el movimiento).",
                                      "Not enough disk space for lossless recording: {0} GB left, need at least {1} GB (a minute takes from several MBs up to multiple GBs depending on motion).",
                                      (free / 1048576.0 / 1024).ToString("0.0", CultureInfo.InvariantCulture), (MinFreeBytes >> 30)),
                             "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (fps == 60 && (long)r.Width * r.Height * 60 > RealtimePixelRate)
            {
                DialogResult d = Dialogs.Show(I18n.Fmt("El \u00E1rea de {0} \u00D7 {1} es demasiado grande para codificar sin p\u00E9rdidas a 60 fps en la mayor\u00EDa de los equipos.\n\nS\u00ED: grabar a 30 fps, sin p\u00E9rdidas.\nNo: probar a 60 fps (si el equipo no puede seguir, la grabaci\u00F3n se detiene y se conserva lo grabado).\nCancelar: no grabar.",
                                                       "The area of {0} \u00D7 {1} is too large to encode losslessly at 60 fps on most computers.\n\nYes: record at 30 fps, lossless.\nNo: try at 60 fps (if the PC cannot keep up, recording stops and saved portion is kept).\nCancel: do not record.",
                                                       r.Width, r.Height),
                                              "Stackshot", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (d == DialogResult.Cancel) return false;
                fps = d == DialogResult.Yes ? 30 : 60;
            }
            return true;
        }
    }

    // 64-bit hash of a frame's B, G, R values. Canonical pixel p = B | G << 8 | R << 16 (the X byte counts as 0), row
    // major, two pixels per little-endian 64-bit word (the even pixel in the low half); an odd last pixel fills the
    // low half only.
    public static class CineHash
    {
        const ulong Seed = 0x9E3779B97F4A7C15, M1 = 0xFF51AFD7ED558CCD, M2 = 0xC4CEB9FE1A85EC53;
        const ulong BgrPair = 0x00FFFFFF00FFFFFF, BgrOne = 0x00FFFFFF;

        static ulong Begin(int width, int height)
        {
            return Seed ^ (((ulong)width << 32) | (uint)height);
        }

        static ulong Step(ulong h, ulong w)
        {
            unchecked
            {
                h ^= w * M1;
                return ((h << 31) | (h >> 33)) * M2;
            }
        }

        static ulong End(ulong h)
        {
            unchecked
            {
                h ^= h >> 33;
                h *= M1;
                return h ^ (h >> 33);
            }
        }

        // The words are two raw bgr0 pixels each, so one mask gives the canonical word.
        static ulong Words(long[] raw, long pixels, int width, int height)
        {
            ulong h = Begin(width, height);
            int words = (int)(pixels / 2);
            for (int i = 0; i < words; i++) h = Step(h, (ulong)raw[i] & BgrPair);
            if ((pixels & 1) != 0) h = Step(h, (ulong)raw[words] & BgrOne);
            return End(h);
        }

        // data: width * height * 4 bytes. scratch keeps a copy of the frame (grown if needed) so nothing is allocated per frame.
        public static ulong Bgr0(IntPtr data, int width, int height, ref long[] scratch)
        {
            long pixels = (long)width * height;
            int words = (int)(pixels / 2), need = words + (int)(pixels & 1);
            if (scratch == null || scratch.Length < need) scratch = new long[need];
            if (words > 0) Marshal.Copy(data, scratch, 0, words);
            if ((pixels & 1) != 0) scratch[words] = (uint)Marshal.ReadInt32(data, words * 8);
            return Words(scratch, pixels, width, height);
        }

        public static ulong Bgr0(byte[] data, int width, int height)
        {
            long pixels = (long)width * height;
            int words = (int)(pixels / 2);
            long[] raw = new long[words + (int)(pixels & 1)];
            Buffer.BlockCopy(data, 0, raw, 0, words * 8);
            if ((pixels & 1) != 0) raw[words] = BitConverter.ToUInt32(data, words * 8);
            return Words(raw, pixels, width, height);
        }

        // ffmpeg's rawvideo gbrp: the G plane, then B, then R.
        public static ulong Gbrp(byte[] planes, int width, int height)
        {
            int n = width * height, g = 0, b = n, r = 2 * n, i = 0;
            ulong h = Begin(width, height);
            for (; i + 1 < n; i += 2)
            {
                ulong p0 = planes[b + i] | ((ulong)planes[g + i] << 8) | ((ulong)planes[r + i] << 16);
                ulong p1 = planes[b + i + 1] | ((ulong)planes[g + i + 1] << 8) | ((ulong)planes[r + i + 1] << 16);
                h = Step(h, p0 | (p1 << 32));
            }
            if (i < n) h = Step(h, planes[b + i] | ((ulong)planes[g + i] << 8) | ((ulong)planes[r + i] << 16));
            return End(h);
        }

        public static string Hex(ulong h)
        {
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    // The live encoder process, with the hash of every frame it was given. The frame is copied once into a pinned buffer
    // that is both hashed and written, so the hash is of exactly the bytes FFmpeg received.
    public sealed class CineSink : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeFileHandle h, IntPtr buffer, int count, out int written, IntPtr overlapped);

        readonly string ffmpeg, nut;
        readonly int width, height, fpsNum, fpsDen, frameBytes;
        readonly List<ulong> hashes = new List<ulong>();
        readonly StringBuilder errors = new StringBuilder();
        readonly object sync = new object();
        Process proc;
        Stream pipeStream;
        SafeFileHandle pipe;
        long[] scratch;
        GCHandle pin;
        IntPtr pinned;
        byte[] fallback;
        ulong lastHash;
        bool haveLast, failed, disposed;
        int frames;

        public CineSink(string ffmpeg, int width, int height, int fpsNum, int fpsDen, string nutPath)
        {
            this.ffmpeg = ffmpeg;
            this.width = width;
            this.height = height;
            this.fpsNum = fpsNum;
            this.fpsDen = fpsDen;
            nut = nutPath;
            frameBytes = checked(width * height * 4);
        }

        public int Frames { get { lock (sync) return frames; } }
        public List<ulong> Hashes { get { return hashes; } }
        public bool Failed { get { lock (sync) return failed; } }

        public string ErrorTail
        {
            get
            {
                lock (errors) return CineMaster.LastLines(errors.ToString().Trim(), 300);
            }
        }

        public void Start()
        {
            scratch = new long[(frameBytes + 7) / 8];
            pin = GCHandle.Alloc(scratch, GCHandleType.Pinned);
            pinned = pin.AddrOfPinnedObject();
            try
            {
                // Process writes the console encoding's preamble (the UTF-8 BOM, say) into the pipe when it starts; FFmpeg
                // is told to skip those bytes. If the guess was wrong, once more with the real length.
                int skip = 0;
                try { skip = Console.InputEncoding.GetPreamble().Length; }
                catch { }
                for (int attempt = 0; ; attempt++)
                {
                    Process p = Launch(skip);
                    int actual = p.StandardInput.Encoding.GetPreamble().Length;
                    if (actual == skip || attempt > 0) { Attach(p); return; }
                    try { p.Kill(); } catch { }
                    p.Dispose();
                    skip = actual;
                }
            }
            catch
            {
                if (pin.IsAllocated) pin.Free();
                scratch = null;
                throw;
            }
        }

        Process Launch(int skip)
        {
            Process p = new Process();
            string args = Cine.LiveArgs(width, height, fpsNum, fpsDen, nut);
            if (skip > 0) args = args.Replace(" -i - ", " -skip_initial_bytes " + skip + " -i - ");
            p.StartInfo.FileName = ffmpeg;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardError = true;
            p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (errors)
                {
                    errors.AppendLine(e.Data);
                    if (errors.Length > 8192) errors.Remove(0, errors.Length - 8192);
                }
            };
            try
            {
                p.Start();
            }
            catch
            {
                p.Dispose();
                throw;
            }
            // The live encoder must keep up with the screen: never parked as background work (normal priority, so the
            // app being recorded keeps its own pace).
            Power.OptOutOfThrottling(p);
            p.BeginErrorReadLine();
            return p;
        }

        void Attach(Process p)
        {
            proc = p;
            pipeStream = p.StandardInput.BaseStream;
            FileStream fs = pipeStream as FileStream;
            pipe = fs != null ? fs.SafeFileHandle : null;
        }

        // The frame goes to FFmpeg whole; false once the pipe has failed (the frame is then not counted).
        public bool Write(IntPtr bgr0)
        {
            lock (sync)
            {
                if (failed || proc == null || disposed) return false;
                ulong h = CineHash.Bgr0(bgr0, width, height, ref scratch);
                if (!Send()) { failed = true; return false; }
                lastHash = h;
                haveLast = true;
                hashes.Add(h);
                frames++;
                return true;
            }
        }

        // The previous frame again: same bytes, same hash.
        public bool Repeat()
        {
            lock (sync)
            {
                if (failed || proc == null || disposed || !haveLast) return false;
                if (!Send()) { failed = true; return false; }
                hashes.Add(lastHash);
                frames++;
                return true;
            }
        }

        bool Send()
        {
            try
            {
                if (pipe != null)
                {
                    int off = 0;
                    while (off < frameBytes)
                    {
                        int done;
                        if (!WriteFile(pipe, new IntPtr(pinned.ToInt64() + off), frameBytes - off, out done, IntPtr.Zero) || done <= 0) return false;
                        off += done;
                    }
                    return true;
                }
                if (fallback == null) fallback = new byte[frameBytes];
                Buffer.BlockCopy(scratch, 0, fallback, 0, frameBytes);
                pipeStream.Write(fallback, 0, frameBytes);
                pipeStream.Flush();
                return true;
            }
            catch { return false; }
        }

        // Ends the video: closes the pipe and waits for FFmpeg to finish the file. False (and killed) on timeout.
        public bool Close(int timeoutMs, out int exitCode)
        {
            exitCode = -1;
            if (proc == null) return false;
            bool got = Monitor.TryEnter(sync, Math.Max(1, timeoutMs));
            try { pipeStream.Close(); }
            catch { }
            finally { if (got) Monitor.Exit(sync); }
            if (!got) { Kill(); return false; }
            try
            {
                if (!proc.WaitForExit(timeoutMs)) { Kill(); proc.WaitForExit(3000); return false; }
                proc.WaitForExit();
                exitCode = proc.ExitCode;
                return true;
            }
            catch { return false; }
        }

        public void Kill()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); }
            catch { }
        }

        public void Dispose()
        {
            Kill();
            if (!Monitor.TryEnter(sync, 3000)) return;
            try
            {
                if (disposed) return;
                disposed = true;
                if (proc != null) { try { proc.Dispose(); } catch { } }
                if (pin.IsAllocated) pin.Free();
                scratch = null;
            }
            finally { Monitor.Exit(sync); }
        }
    }

    public sealed class CineAudioTrack
    {
        public string Title, Path, Sha256;
        public bool Original;
        public long Frames;
    }

    public static class CineMaster
    {
        // The end of FFmpeg's error text, at most max characters, starting at a line (not in the middle of one).
        internal static string LastLines(string t, int max)
        {
            if (t == null || t.Length <= max) return t;
            string tail = t.Substring(t.Length - max);
            int nl = tail.IndexOf('\n');
            return nl >= 0 && nl < tail.Length - 1 ? tail.Substring(nl + 1).Trim() : tail.Trim();
        }

        public static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
                return Hex(sha.ComputeHash(fs));
        }

        internal static string Hex(byte[] b)
        {
            StringBuilder sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // Writes outPath; null on success, else why not. progress: 0..1 of `seconds` (may be null).
        public static string Build(string ffmpeg, string nutPath, string sysPath, string micPath, string voiceGain, string outPath, double seconds, Action<double> progress)
        {
            using (PassWatch watch = new PassWatch(PassWatch.DefaultStallMs))
                return Build(ffmpeg, nutPath, sysPath, micPath, voiceGain, outPath, seconds, progress, watch);
        }

        // watch: kills the pass if it stops making progress or the user cancels.
        internal static string Build(string ffmpeg, string nutPath, string sysPath, string micPath, string voiceGain, string outPath, double seconds, Action<double> progress,
                                     PassWatch watch)
        {
            StringBuilder err = new StringBuilder();
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = "-hide_banner -nostdin -loglevel error -nostats -progress pipe:1 -y " + Cine.MasterArgs(nutPath, sysPath, micPath, voiceGain ?? "", outPath);
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        watch.Ping();
                        if (progress == null || !e.Data.StartsWith("out_time_us=", StringComparison.Ordinal) || seconds <= 0) return;
                        long us;
                        if (long.TryParse(e.Data.Substring(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out us))
                            progress(Math.Max(0, Math.Min(1, us / 1e6 / seconds)));
                    };
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        lock (err) { if (err.Length < 4000) err.AppendLine(e.Data); }
                    };
                    p.Start();
                    ChildJob.Add(p);
                    watch.Attach(p, PassWatch.LimitFor(seconds));
                    try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    int code = PassWatch.WaitExit(p, watch);
                    watch.Detach(p);
                    if (watch.Cancelled) { TryDelete(outPath); return PassWatch.CancelledText; }
                    if (watch.Stalled) { TryDelete(outPath); return PassWatch.StalledText; }
                    if (code != 0)
                    {
                        string t;
                        lock (err) t = err.ToString().Trim();
                        TryDelete(outPath);
                        return t.Length > 0 ? LastLines(t, 300) : I18n.Fmt("FFmpeg termin\u00F3 con el c\u00F3digo {0}.", "FFmpeg exited with code {0}.", code);
                    }
                }
            }
            catch (Exception ex) { return ex.Message; }
            try
            {
                if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0) return I18n.T("FFmpeg no cre\u00F3 el archivo.", "FFmpeg did not create the file.");
            }
            catch (Exception ex) { return ex.Message; }
            if (progress != null) progress(1);
            return null;
        }

        static void TryDelete(string f)
        {
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }

        // The audio tracks of the master, in file order. The mix (both sources) comes first and is the default one.
        public static List<CineAudioTrack> Tracks(string sysPath, string micPath, long frames)
        {
            List<CineAudioTrack> list = new List<CineAudioTrack>();
            if (sysPath != null && micPath != null) list.Add(new CineAudioTrack { Title = Cine.TitleMix, Original = false, Frames = frames });
            if (sysPath != null) list.Add(new CineAudioTrack { Title = Cine.TitleSystem, Path = sysPath, Sha256 = Sha256File(sysPath), Original = true, Frames = frames });
            if (micPath != null) list.Add(new CineAudioTrack { Title = Cine.TitleMic, Path = micPath, Sha256 = Sha256File(micPath), Original = true, Frames = frames });
            return list;
        }
    }

    public sealed class CineExpect
    {
        public int Width, Height, FpsNum, FpsDen;
        public List<ulong> FrameHashes = new List<ulong>();
        public List<CineAudioTrack> Audio = new List<CineAudioTrack>();
    }

    public sealed class CineCheck
    {
        public string Name;
        public bool Passed;
        public string Detail;
    }

    public sealed class CineVerification
    {
        public bool Passed;
        public List<CineCheck> Checks = new List<CineCheck>();
        public long FramesDecoded, FramesMatched, FirstMismatch = -1;
        public string Container, Codec, Profile, PixelFormat, FrameRate;
        public int Width, Height;
        public double Seconds;
        public Bitmap Poster;   // small image of frame min(60, last), or null
    }

    // Opens the master the way any player would and compares it with what was recorded: structure and timing from a
    // packet listing, every video frame by hash, every audio track by byte count (and the originals by SHA-256).
    public static class CineVerifier
    {
        public static CineVerification Verify(string ffmpeg, string master, CineExpect expect, Action<double> progress)
        {
            using (PassWatch watch = new PassWatch(PassWatch.DefaultStallMs))
                return Verify(ffmpeg, master, expect, progress, watch);
        }

        // watch: kills a pass that stops making progress, or every pass once the user cancels (the checks then fail).
        internal static CineVerification Verify(string ffmpeg, string master, CineExpect expect, Action<double> progress, PassWatch watch)
        {
            CineVerification v = new CineVerification();
            v.Width = expect.Width;
            v.Height = expect.Height;
            try
            {
                Probe(ffmpeg, master, expect, v, watch);
                if (progress != null) progress(0.02);
                if (!watch.Cancelled && !watch.Stalled) DecodeVideo(ffmpeg, master, expect, v, progress, watch);
                if (!watch.Cancelled && !watch.Stalled) DecodeAudio(ffmpeg, master, expect, v, progress, watch);
                if (watch.Cancelled) Add(v, I18n.T("Comprobaci\u00F3n", "Check"), false, I18n.T("Cancelada antes de terminar.", "Cancelled before finishing."));
                else if (watch.Stalled) Add(v, I18n.T("Comprobaci\u00F3n", "Check"), false, PassWatch.StalledText);
            }
            catch (Exception ex)
            {
                Add(v, I18n.T("Comprobaci\u00F3n", "Check"), false, I18n.T("No se pudo completar: ", "Could not finish: ") + ex.Message);
            }
            bool all = v.Checks.Count > 0;
            foreach (CineCheck c in v.Checks) if (!c.Passed) all = false;
            v.Passed = all;
            if (progress != null) progress(1);
            return v;
        }

        static void Add(CineVerification v, string name, bool ok, string detail)
        {
            CineCheck c = new CineCheck();
            c.Name = name;
            c.Passed = ok;
            c.Detail = detail;
            v.Checks.Add(c);
        }

        static readonly Regex TimeBase = new Regex(@"^#tb (\d+): (\d+)/(\d+)", RegexOptions.Compiled);

        // Structure and timing, from FFmpeg's own listing of the file.
        static void Probe(string ffmpeg, string master, CineExpect expect, CineVerification v, PassWatch watch)
        {
            // The packet listing, read line by line as it comes (an hour of video is hundreds of thousands of lines):
            // "#tb n: a/b" headers, then "stream, dts, pts, duration, size, crc" per packet.
            Dictionary<int, double> tb = new Dictionary<int, double>();
            Dictionary<int, List<long[]>> packets = new Dictionary<int, List<long[]>>();
            string stderr;
            int code = Cine.CaptureLines(ffmpeg, Cine.ProbeArgs(master), 4 * 3600 * 1000, delegate(string line)
            {
                watch.Ping();
                if (line.Length == 0) return;
                if (line[0] == '#')
                {
                    Match m = TimeBase.Match(line);
                    if (m.Success) tb[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    return;
                }
                if (line[0] < '0' || line[0] > '9') return;
                string[] f = line.Split(',');
                long stream, pts, dur;
                if (f.Length < 5 || !long.TryParse(f[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stream) ||
                    !long.TryParse(f[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pts) || !long.TryParse(f[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out dur)) return;
                List<long[]> l;
                if (!packets.TryGetValue((int)stream, out l)) packets[(int)stream] = l = new List<long[]>();
                l.Add(new long[] { pts, dur });
            }, watch, out stderr);
            Add(v, I18n.T("Lectura del archivo", "Reading the file"), code == 0, code == 0 ? I18n.T("FFmpeg lo lee entero sin errores.", "FFmpeg reads all of it without errors.")
                                                                                       : I18n.Fmt("FFmpeg termin\u00F3 con el c\u00F3digo {0}.", "FFmpeg exited with code {0}.", code));

            int inStart = stderr.IndexOf("Input #0,", StringComparison.Ordinal);
            int inEnd = stderr.IndexOf("Output #0,", StringComparison.Ordinal);
            string input = inStart < 0 ? "" : (inEnd > inStart ? stderr.Substring(inStart, inEnd - inStart) : stderr.Substring(inStart));
            bool mkv = input.StartsWith("Input #0, matroska,webm", StringComparison.Ordinal);
            v.Container = mkv ? "Matroska" : null;
            Add(v, I18n.T("Contenedor", "Container"), mkv, mkv ? "Matroska (.mkv)." : I18n.T("No es un archivo Matroska.", "Not a Matroska file."));

            List<string> videos = new List<string>(), audios = new List<string>();
            foreach (Match m in Regex.Matches(input, @"^\s*Stream #0:\d+[^\r\n]*?: (Video|Audio): ([^\r\n]*)", RegexOptions.Multiline))
                (m.Groups[1].Value == "Video" ? videos : audios).Add(m.Groups[2].Value);

            bool one = videos.Count == 1;
            string vline = one ? videos[0] : "";
            bool profile = vline.StartsWith("h264 (High 4:4:4 Predictive)", StringComparison.Ordinal);
            bool pix = vline.Contains("gbrp(pc");
            bool size = Regex.IsMatch(vline, @"\b" + expect.Width + "x" + expect.Height + @"\b");
            v.Codec = profile ? "h264" : null;
            v.Profile = profile ? "High 4:4:4 Predictive" : null;
            v.PixelFormat = pix ? "gbrp" : null;
            Match fr = Regex.Match(vline, @"([0-9.]+) fps");
            v.FrameRate = fr.Success ? fr.Groups[1].Value + " fps" : null;
            Add(v, I18n.T("V\u00EDdeo H.264 RGB", "H.264 RGB video"), one && profile && pix && size,
                one ? (profile ? I18n.T("Perfil High 4:4:4 Predictive", "High 4:4:4 Predictive profile") : I18n.T("El perfil no es High 4:4:4 Predictive", "The profile is not High 4:4:4 Predictive")) + ", " +
                      (pix ? I18n.T("RGB completo (gbrp)", "full RGB (gbrp)") : I18n.T("no es RGB completo", "not full RGB")) + ", " +
                      (size ? expect.Width + " \u00D7 " + expect.Height : I18n.T("tama\u00F1o distinto del grabado", "size differs from the recording")) + "."
                    : I18n.Fmt("Se esperaba un \u00FAnico flujo de v\u00EDdeo y hay {0}.", "Expected a single video stream and there are {0}.", videos.Count));

            bool audioOk = audios.Count == expect.Audio.Count;
            foreach (string a in audios) if (!a.StartsWith("pcm_f32le", StringComparison.Ordinal) || !a.Contains("48000 Hz") || !a.Contains("stereo")) audioOk = false;
            if (expect.Audio.Count > 0 || audios.Count > 0)
                Add(v, I18n.T("Pistas de sonido", "Sound tracks"), audioOk,
                    audioOk ? (I18n.IsSpanish ? audios.Count + (audios.Count == 1 ? " pista" : " pistas") + " PCM de 32 bits, 48 kHz, est\u00E9reo."
                                              : audios.Count + (audios.Count == 1 ? " track" : " tracks") + " of 32-bit PCM, 48 kHz, stereo.")
                            : I18n.Fmt("Se esperaban {0} pistas PCM de 32 bits a 48 kHz est\u00E9reo y hay {1} (o con otro formato).",
                                       "Expected {0} tracks of 32-bit PCM at 48 kHz stereo and there are {1} (or in another format).", expect.Audio.Count, audios.Count));

            // Stream 0 is the video and 1.. the sound, in the order of the file.
            List<long[]> vp;
            packets.TryGetValue(0, out vp);
            long count = vp == null ? 0 : vp.Count;
            Add(v, I18n.T("N\u00FAmero de fotogramas", "Frame count"), count == expect.FrameHashes.Count,
                I18n.Fmt("{0} en el archivo, {1} enviados al codificador.", "{0} in the file, {1} sent to the encoder.", count, expect.FrameHashes.Count));

            double t0 = tb.ContainsKey(0) ? tb[0] : 0.001;
            string timing = null;
            if (vp != null && tb.ContainsKey(0))
            {
                double step = expect.FpsDen / (double)expect.FpsNum;
                for (int i = 0; i < vp.Count; i++)
                {
                    if (i > 0 && vp[i][0] <= vp[i - 1][0]) { timing = I18n.Fmt("El fotograma {0} no es posterior al anterior.", "Frame {0} does not come after the one before.", i); break; }
                    double off = Math.Abs(vp[i][0] * t0 - i * step);
                    if (off > 0.001 + t0 + 1e-9)
                    {
                        timing = I18n.Fmt("El fotograma {0} est\u00E1 {1} ms fuera de su instante.", "Frame {0} is {1} ms off its time.", i, (off * 1000).ToString("0.0", CultureInfo.InvariantCulture));
                        break;
                    }
                }
            }
            else timing = I18n.T("El archivo no tiene lista de fotogramas.", "The file has no frame list.");
            Add(v, I18n.T("Tiempos de los fotogramas", "Frame timing"), timing == null,
                timing ?? I18n.T("Cada fotograma en su instante (a menos de 1 ms), siempre hacia delante.", "Every frame at its time (within 1 ms), always moving forward."));
            if (vp != null && vp.Count > 0) v.Seconds = (vp[vp.Count - 1][0] * t0) + expect.FpsDen / (double)expect.FpsNum;

            for (int k = 0; k < expect.Audio.Count; k++)
            {
                CineAudioTrack t = expect.Audio[k];
                List<long[]> ap;
                bool has = packets.TryGetValue(k + 1, out ap) && tb.ContainsKey(k + 1);
                // The span from the first packet to the end of the last one: summing the packets' own durations would
                // read short, as Matroska keeps each of them in whole milliseconds.
                double want = t.Frames / (double)AudioCapture.Rate;
                double got = has && ap.Count > 0 ? (ap[ap.Count - 1][0] - ap[0][0] + ap[ap.Count - 1][1]) * tb[k + 1] : 0;
                bool ok = has && ap.Count > 0 && Math.Abs(got - want) <= 0.001 + 2 * tb[k + 1];
                Add(v, I18n.T("Duraci\u00F3n del sonido: ", "Sound duration: ") + Cine.TrackName(t.Title), ok,
                    has ? I18n.Fmt("{0} s en el archivo, {1} s grabados.", "{0} s in the file, {1} s recorded.",
                                   got.ToString("0.000", CultureInfo.InvariantCulture), want.ToString("0.000", CultureInfo.InvariantCulture))
                        : I18n.T("No hay paquetes de esta pista.", "This track has no packets."));
            }
        }

        // Every video frame, in order, as FFmpeg's decoder produces it. One thread reads the pipe while this one hashes
        // the frame before, so decoding and hashing overlap (same hash, same order).
        static void DecodeVideo(string ffmpeg, string master, CineExpect expect, CineVerification v, Action<double> progress, PassWatch watch)
        {
            int w = expect.Width, h = expect.Height, size = checked(w * h * 3);
            int total = expect.FrameHashes.Count, posterAt = Math.Max(0, Math.Min(60, total - 1));
            long decoded = 0, matched = 0, first = -1;
            StringBuilder err = new StringBuilder();
            int code;
            using (Process p = Spawn(ffmpeg, Cine.VideoDecodeArgs(master), err, watch))
            {
                BlockingCollection<byte[]> full = new BlockingCollection<byte[]>(), free = new BlockingCollection<byte[]>();
                for (int i = 0; i < 3; i++) free.Add(new byte[size]);
                Stream s = p.StandardOutput.BaseStream;
                Exception readError = null;
                Thread reader = new Thread(delegate()
                {
                    try
                    {
                        byte[] b;
                        while (free.TryTake(out b, Timeout.Infinite) && ReadFully(s, b, size)) full.Add(b);
                    }
                    catch (Exception ex) { readError = ex; }
                    finally { full.CompleteAdding(); }
                });
                reader.IsBackground = true;
                reader.Start();
                try
                {
                    foreach (byte[] buf in full.GetConsumingEnumerable())
                    {
                        watch.Ping();
                        if (decoded < total)
                        {
                            if (CineHash.Gbrp(buf, w, h) == expect.FrameHashes[(int)decoded]) matched++;
                            else if (first < 0) first = decoded;
                            if (decoded == posterAt) v.Poster = Poster(buf, w, h);
                        }
                        decoded++;
                        free.Add(buf);
                        if (progress != null && (decoded & 15) == 0 && total > 0) progress(0.02 + 0.78 * Math.Min(1.0, decoded / (double)total));
                    }
                    code = PassWatch.WaitExit(p, watch);
                }
                finally
                {
                    End(p);
                    free.CompleteAdding();
                    reader.Join(10000);
                    watch.Detach(p);
                }
                if (readError != null) throw new IOException(readError.Message, readError);
            }
            v.FramesDecoded = decoded;
            v.FramesMatched = matched;
            v.FirstMismatch = first;
            if (first < 0 && decoded != total) v.FirstMismatch = Math.Min(decoded, total);
            bool ok = matched == total && decoded == total && code == 0;
            string detail;
            if (ok) detail = I18n.Fmt("{0} de {1} fotogramas id\u00E9nticos (cada p\u00EDxel).", "{0} of {1} frames identical (every pixel).", matched, total);
            else if (first >= 0) detail = I18n.Fmt("El fotograma {0} no coincide; {1} de {2} id\u00E9nticos.", "Frame {0} does not match; {1} of {2} identical.", first, matched, total);
            else if (decoded != total) detail = I18n.Fmt("Se han decodificado {0} fotogramas y se grabaron {1}.", "{0} frames were decoded and {1} were recorded.", decoded, total);
            else detail = I18n.Fmt("FFmpeg termin\u00F3 con el c\u00F3digo {0}.", "FFmpeg exited with code {0}.", code);
            Add(v, I18n.T("Fotogramas id\u00E9nticos a los capturados", "Frames identical to the captured ones"), ok, detail);
        }

        // Each audio track as the decoder gives it: its length, and for the originals the bits themselves.
        static void DecodeAudio(string ffmpeg, string master, CineExpect expect, CineVerification v, Action<double> progress, PassWatch watch)
        {
            for (int k = 0; k < expect.Audio.Count && !watch.Cancelled && !watch.Stalled; k++)
            {
                CineAudioTrack t = expect.Audio[k];
                long bytes = 0;
                string hash;
                int code;
                StringBuilder err = new StringBuilder();
                using (Process p = Spawn(ffmpeg, Cine.AudioDecodeArgs(master, k), err, watch))
                using (SHA256 sha = SHA256.Create())
                {
                    try
                    {
                        Stream s = p.StandardOutput.BaseStream;
                        byte[] chunk = new byte[1 << 20];
                        int n;
                        while ((n = s.Read(chunk, 0, chunk.Length)) > 0)
                        {
                            watch.Ping();
                            sha.TransformBlock(chunk, 0, n, null, 0);
                            bytes += n;
                        }
                        sha.TransformFinalBlock(chunk, 0, 0);
                        hash = CineMaster.Hex(sha.Hash);
                        code = PassWatch.WaitExit(p, watch);
                    }
                    finally { End(p); watch.Detach(p); }
                }
                long want = t.Frames * 8;
                Add(v, I18n.T("Longitud del sonido: ", "Sound length: ") + Cine.TrackName(t.Title), bytes == want && code == 0,
                    bytes == want ? I18n.Fmt("{0} muestras, las grabadas.", "{0} samples, as recorded.", t.Frames)
                                  : I18n.Fmt("{0} muestras en el archivo y {1} grabadas.", "{0} samples in the file and {1} recorded.", bytes / 8, t.Frames));
                if (t.Original)
                {
                    bool same = string.Equals(hash, t.Sha256, StringComparison.OrdinalIgnoreCase);
                    Add(v, I18n.T("Sonido original id\u00E9ntico: ", "Original sound identical: ") + Cine.TrackName(t.Title), same,
                        same ? I18n.T("Los bits son exactamente los grabados (SHA-256).", "The bits are exactly the recorded ones (SHA-256).")
                             : I18n.T("Los bits no coinciden con lo grabado.", "The bits do not match the recording."));
                }
                if (progress != null) progress(0.8 + 0.2 * (k + 1) / (double)Math.Max(1, expect.Audio.Count));
            }
        }

        // FFmpeg with its stdout read by the caller; stderr is collected so a full pipe can't stop it.
        static Process Spawn(string ffmpeg, string args, StringBuilder err, PassWatch watch)
        {
            Process p = new Process();
            p.StartInfo.FileName = ffmpeg;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) lock (err) { if (err.Length < 4000) err.AppendLine(e.Data); } };
            p.Start();
            ChildJob.Add(p);
            watch.Attach(p, 0);
            try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            p.BeginErrorReadLine();
            return p;
        }

        // A reader that stopped early would leave FFmpeg blocked on a full pipe.
        static void End(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        static bool ReadFully(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        // A box-filtered copy of a gbrp frame, at most 512 px on its long side.
        static Bitmap Poster(byte[] planes, int w, int h)
        {
            int k = Math.Max(1, (Math.Max(w, h) + 511) / 512), pw = Math.Max(1, w / k), ph = Math.Max(1, h / k), n = w * h;
            Bitmap bmp = new Bitmap(pw, ph, PixelFormat.Format24bppRgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, pw, ph), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[Math.Abs(bd.Stride)];
                for (int y = 0; y < ph; y++)
                {
                    for (int x = 0; x < pw; x++)
                    {
                        int sg = 0, sb = 0, sr = 0, c = 0;
                        for (int j = 0; j < k && y * k + j < h; j++)
                            for (int i = 0; i < k && x * k + i < w; i++)
                            {
                                int o = (y * k + j) * w + x * k + i;
                                sg += planes[o]; sb += planes[n + o]; sr += planes[2 * n + o];
                                c++;
                            }
                        row[x * 3] = (byte)(sb / c);
                        row[x * 3 + 1] = (byte)(sg / c);
                        row[x * 3 + 2] = (byte)(sr / c);
                    }
                    Marshal.Copy(row, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), pw * 3);
                }
            }
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }
    }

    // What happened in one Cine recording, in numbers: filled while recording and saving, shown to the user when
    // something was lost, and saved next to the other reports.
    public sealed class CineReport
    {
        public string File, Started, StopReason, CaptureApi, Encoder;
        public string Error;      // why there is no master (what the user was told), or null
        public string Rescued;    // the recuperado folder holding what was recorded when the master could not be made
        public int Width, Height, FpsNum, FpsDen;
        public double RefreshHz;
        public bool Hdr, Gdi, AudioConverted;
        public long SourcePresents, Coalesced, ImagesReceived, ImagesUnused, HistoryMisses, Protected, LostEvents;
        public double LostMs, MaxLateMs, RecordSeconds, MuxSeconds, VerifySeconds;
        public long FramesEmitted, FramesWritten, FramesRepeatedFailed, FramesBlack, QueuePeak, PoolFrames, Discarded;
        public long FileBytes;
        public int Pauses;        // times the recording was paused (what happened meanwhile is not in the file)
        public long SoundJoins;   // resumes where the sound fades across the cut (10 ms not as the device gave them)
        public long OverlayFrames; // frames with clicks or keys drawn on them
        public List<CineAudioTrack> Audio;
        public List<string> AudioNotes;
        public string Saturation;  // a source clipped while recording: one line for the notice, or null
        public CineVerification Verification;

        // Anything that makes the frames differ from the screen, or the sound from what the device gave, is told in full.
        public bool CaptureClean
        {
            get
            {
                return HistoryMisses == 0 && FramesRepeatedFailed == 0 && FramesBlack == 0 && Discarded == 0 && StopReason == null &&
                       !Hdr && !Gdi && LostEvents == 0 && !AudioConverted && SoundJoins == 0 && OverlayFrames == 0;
            }
        }
        public bool Passed { get { return Verification != null && Verification.Passed; } }

        static string N(double d, string f) { return d.ToString(f, CultureInfo.InvariantCulture); }

        public string Summary()
        {
            if (!I18n.IsSpanish) return SummaryEn();
            StringBuilder sb = new StringBuilder();
            double fps = FpsDen > 0 ? FpsNum / (double)FpsDen : 0;
            sb.AppendLine("Cine: " + Width + " \u00D7 " + Height + " a " + N(fps, "0.##") + " fps, " + N(RecordSeconds, "0.0") + " s, " + FramesWritten + " fotogramas.");
            if (!string.IsNullOrEmpty(File)) sb.AppendLine("Archivo: " + System.IO.Path.GetFileName(File) + (FileBytes > 0 ? " (" + N(FileBytes / 1048576.0, "0.0") + " MB)" : ""));
            if (StopReason != null) sb.AppendLine("La grabaci\u00F3n se detuvo antes de tiempo: " + StopReason + ". Se ha guardado lo grabado hasta ese momento.");
            if (Pauses > 0) sb.AppendLine("- Se paus\u00F3 " + (Pauses == 1 ? "una vez" : Pauses + " veces") + ": lo que pas\u00F3 en " + (Pauses == 1 ? "la pausa" : "las pausas") + " no est\u00E1 en el archivo.");
            if (OverlayFrames > 0) sb.AppendLine("- " + (OverlayFrames == 1 ? "1 fotograma lleva" : OverlayFrames + " fotogramas llevan") + " dibujados los clics o las teclas pulsadas (Grabaci\u00F3n > Clics y teclas).");
            if (HistoryMisses > 0) sb.AppendLine("- " + HistoryMisses + " fotogramas pudieron tomar una imagen de pantalla equivocada (el equipo fue demasiado lento).");
            if (FramesRepeatedFailed > 0) sb.AppendLine("- " + FramesRepeatedFailed + " fotogramas repiten el anterior porque no se pudo leer la pantalla.");
            if (FramesBlack > 0) sb.AppendLine("- " + FramesBlack + " fotogramas negros al principio porque no se pudo leer la pantalla.");
            if (Discarded > 0) sb.AppendLine("- " + Discarded + " fotogramas tomados no llegaron a escribirse.");
            if (LostEvents > 0) sb.AppendLine("- La captura de pantalla se interrumpi\u00F3 " + LostEvents + (LostEvents == 1 ? " vez" : " veces") + " (" + N(LostMs, "0") + " ms sin imagen nueva).");
            // Windows raises this for any window hidden from captures anywhere on the monitor (Stackshot's own too).
            if (Protected > 0) sb.AppendLine("- En " + Protected + " im\u00E1genes Windows ocult\u00F3 ventanas protegidas o excluidas de la captura en esa pantalla (tambi\u00E9n las de Stackshot); si alguna estaba dentro del \u00E1rea, no aparece o sale en negro.");
            if (Gdi) sb.AppendLine("- Se ha usado la captura de reserva (GDI): no ve contenido acelerado por hardware.");
            if (Hdr) sb.AppendLine("- La pantalla est\u00E1 en HDR: Windows entrega la imagen convertida a SDR.");
            if (RefreshHz > 0 && fps > 0 && RefreshHz > fps * 1.02) sb.AppendLine("- El monitor va a " + N(RefreshHz, "0.#") + " Hz y se graba a " + N(fps, "0.##") + " fps: " + ImagesUnused + " im\u00E1genes de pantalla no aparecen en ning\u00FAn fotograma.");
            if (AudioNotes != null) foreach (string a in AudioNotes) sb.AppendLine("- " + a);
            if (!string.IsNullOrEmpty(Error)) sb.AppendLine("No se ha creado el m\u00E1ster: " + Error.Replace("\r", "").Replace("\n\n", " ").Replace("\n", " "));
            if (!string.IsNullOrEmpty(Rescued)) sb.AppendLine("Lo grabado est\u00E1 en: " + Rescued);
            if (Verification == null) sb.AppendLine("Comprobaci\u00F3n: no se ha hecho.");
            else if (Verification.Passed) sb.AppendLine("Comprobaci\u00F3n: correcta, " + Verification.FramesMatched + " fotogramas id\u00E9nticos a los enviados al codificador.");
            else
            {
                sb.AppendLine("Comprobaci\u00F3n: HA FALLADO.");
                foreach (CineCheck c in Verification.Checks) if (!c.Passed) sb.AppendLine("- " + c.Name + ": " + c.Detail);
            }
            return sb.ToString().TrimEnd();
        }

        string SummaryEn()
        {
            StringBuilder sb = new StringBuilder();
            double fps = FpsDen > 0 ? FpsNum / (double)FpsDen : 0;
            sb.AppendLine("Cinema: " + Width + " \u00D7 " + Height + " at " + N(fps, "0.##") + " fps, " + N(RecordSeconds, "0.0") + " s, " + FramesWritten + " frames.");
            if (!string.IsNullOrEmpty(File)) sb.AppendLine("File: " + System.IO.Path.GetFileName(File) + (FileBytes > 0 ? " (" + N(FileBytes / 1048576.0, "0.0") + " MB)" : ""));
            if (StopReason != null) sb.AppendLine("The recording stopped early: " + StopReason + ". What was recorded up to then has been saved.");
            if (Pauses > 0) sb.AppendLine("- Paused " + (Pauses == 1 ? "once" : Pauses + " times") + ": what happened during " + (Pauses == 1 ? "the pause" : "the pauses") + " is not in the file.");
            if (OverlayFrames > 0) sb.AppendLine("- " + (OverlayFrames == 1 ? "1 frame has" : OverlayFrames + " frames have") + " clicks or pressed keys drawn on them (Recording > Clicks and keys).");
            if (HistoryMisses > 0) sb.AppendLine("- " + HistoryMisses + " frames may have taken the wrong screen image (the computer was too slow).");
            if (FramesRepeatedFailed > 0) sb.AppendLine("- " + FramesRepeatedFailed + " frames repeat the one before because the screen could not be read.");
            if (FramesBlack > 0) sb.AppendLine("- " + FramesBlack + " black frames at the start because the screen could not be read.");
            if (Discarded > 0) sb.AppendLine("- " + Discarded + " captured frames were never written.");
            if (LostEvents > 0) sb.AppendLine("- Screen capture was interrupted " + LostEvents + (LostEvents == 1 ? " time" : " times") + " (" + N(LostMs, "0") + " ms with no new image).");
            if (Protected > 0) sb.AppendLine("- In " + Protected + " images Windows hid protected windows or windows excluded from capture on that screen (Stackshot's too); any inside the area is missing or black.");
            if (Gdi) sb.AppendLine("- The fallback capture (GDI) was used: it does not see hardware-accelerated content.");
            if (Hdr) sb.AppendLine("- The screen is in HDR: Windows hands over the image converted to SDR.");
            if (RefreshHz > 0 && fps > 0 && RefreshHz > fps * 1.02) sb.AppendLine("- The monitor runs at " + N(RefreshHz, "0.#") + " Hz and recording is at " + N(fps, "0.##") + " fps: " + ImagesUnused + " screen images are not in any frame.");
            if (AudioNotes != null) foreach (string a in AudioNotes) sb.AppendLine("- " + a);
            if (!string.IsNullOrEmpty(Error)) sb.AppendLine("The master was not created: " + Error.Replace("\r", "").Replace("\n\n", " ").Replace("\n", " "));
            if (!string.IsNullOrEmpty(Rescued)) sb.AppendLine("What was recorded is in: " + Rescued);
            if (Verification == null) sb.AppendLine("Check: not done.");
            else if (Verification.Passed) sb.AppendLine("Check: passed, " + Verification.FramesMatched + " frames identical to the ones sent to the encoder.");
            else
            {
                sb.AppendLine("Check: FAILED.");
                foreach (CineCheck c in Verification.Checks) if (!c.Passed) sb.AppendLine("- " + c.Name + ": " + c.Detail);
            }
            return sb.ToString().TrimEnd();
        }

        public string ToJson()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["file"] = File;
            d["started"] = Started;
            d["stopReason"] = StopReason;
            d["error"] = Error;
            d["rescued"] = Rescued;
            d["captureApi"] = CaptureApi;
            d["encoder"] = Encoder;
            d["width"] = Width;
            d["height"] = Height;
            d["fpsNum"] = FpsNum;
            d["fpsDen"] = FpsDen;
            d["refreshHz"] = RefreshHz;
            d["hdr"] = Hdr;
            d["gdi"] = Gdi;
            d["audioConverted"] = AudioConverted;
            d["sourcePresents"] = SourcePresents;
            d["coalesced"] = Coalesced;
            d["imagesReceived"] = ImagesReceived;
            d["imagesUnused"] = ImagesUnused;
            d["historyMisses"] = HistoryMisses;
            d["protectedContent"] = Protected;
            d["lostEvents"] = LostEvents;
            d["lostMs"] = LostMs;
            d["maxLateMs"] = MaxLateMs;
            d["recordSeconds"] = RecordSeconds;
            d["muxSeconds"] = MuxSeconds;
            d["verifySeconds"] = VerifySeconds;
            d["framesEmitted"] = FramesEmitted;
            d["framesWritten"] = FramesWritten;
            d["framesRepeatedFailed"] = FramesRepeatedFailed;
            d["framesBlack"] = FramesBlack;
            d["queuePeak"] = QueuePeak;
            d["poolFrames"] = PoolFrames;
            d["discarded"] = Discarded;
            d["pauses"] = Pauses;
            d["soundJoins"] = SoundJoins;
            d["overlayFrames"] = OverlayFrames;
            d["fileBytes"] = FileBytes;
            d["captureClean"] = CaptureClean;
            d["passed"] = Passed;
            List<object> tracks = new List<object>();
            if (Audio != null)
                foreach (CineAudioTrack t in Audio)
                {
                    Dictionary<string, object> a = new Dictionary<string, object>();
                    a["title"] = t.Title;
                    a["original"] = t.Original;
                    a["frames"] = t.Frames;
                    a["sha256"] = t.Sha256;
                    tracks.Add(a);
                }
            d["audio"] = tracks;
            d["audioNotes"] = AudioNotes ?? new List<string>();
            d["saturation"] = Saturation;
            if (Verification != null)
            {
                Dictionary<string, object> v = new Dictionary<string, object>();
                v["passed"] = Verification.Passed;
                v["framesDecoded"] = Verification.FramesDecoded;
                v["framesMatched"] = Verification.FramesMatched;
                v["firstMismatch"] = Verification.FirstMismatch;
                v["container"] = Verification.Container;
                v["codec"] = Verification.Codec;
                v["profile"] = Verification.Profile;
                v["pixelFormat"] = Verification.PixelFormat;
                v["frameRate"] = Verification.FrameRate;
                v["seconds"] = Verification.Seconds;
                List<object> checks = new List<object>();
                foreach (CineCheck c in Verification.Checks)
                {
                    Dictionary<string, object> k = new Dictionary<string, object>();
                    k["name"] = c.Name;
                    k["passed"] = c.Passed;
                    k["detail"] = c.Detail;
                    checks.Add(k);
                }
                v["checks"] = checks;
                d["verification"] = v;
            }
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            return js.Serialize(d);
        }

        // <dir>\<name of the video>.json
        public string Save(string dir)
        {
            Directory.CreateDirectory(dir);
            string name = System.IO.Path.GetFileNameWithoutExtension(File ?? "");
            if (name.Length == 0) name = "cine";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string path = System.IO.Path.Combine(dir, name + ".json");
            System.IO.File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
            return path;
        }
    }
}
