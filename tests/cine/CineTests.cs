// Independent tests of the lossless Cine mode, written against the approved spec (not the implementation).
// Suites run by default: regression hash args pool roundtrip negative audio failures reliability.
// Named explicitly only: stress bench realcapture hunt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Stackshot.Tests
{
    static partial class CineTests
    {
        sealed class TestFailed : Exception { public TestFailed(string message) : base(message) { } }
        sealed class TestSkipped : Exception { public TestSkipped(string message) : base(message) { } }

        sealed class Outcome
        {
            public string Suite, Name, Status, Detail;
            public double Seconds;
            public Dictionary<string, object> Data;
        }

        static readonly List<Outcome> outcomes = new List<Outcome>();
        static readonly string[] defaultSuites = { "regression", "hash", "args", "pool", "roundtrip", "negative", "audio", "failures", "reliability", "saturation", "w6record" };
        static string currentDetail;
        static Dictionary<string, object> currentData;
        static string testsDir, repoDir, workRoot, ffmpeg;

        [STAThread]
        static int Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("STACKSHOT_CINE_DIR");
            testsDir = string.IsNullOrEmpty(env) ? @"C:\Projects\stackshot\tests\cine" : env;
            repoDir = Path.GetDirectoryName(Path.GetDirectoryName(testsDir));
            ffmpeg = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Stackshot", "ffmpeg", "ffmpeg.exe");
            // Per process: two runs at the same time must not delete each other's files.
            workRoot = Path.Combine(Path.GetTempPath(), "stackshot-cine-tests", "work-" + Process.GetCurrentProcess().Id);
            Settings.ReadOnly = true;
            I18n.Apply("es");   // the suites check the Spanish texts, whatever language this Windows is in
            Directory.CreateDirectory(workRoot);

            List<string> suites = args.Length > 0 ? new List<string>(args) : new List<string>(defaultSuites);
            foreach (string suite in suites) RunSuite(suite);
            DeleteDir(workRoot);

            int passes = 0, fails = 0, skips = 0;
            foreach (Outcome o in outcomes)
            {
                if (o.Status == "PASS") passes++;
                else if (o.Status == "FAIL") fails++;
                else skips++;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string dir = Path.Combine(testsDir, "results", stamp);
            Directory.CreateDirectory(dir);
            WriteMarkdown(Path.Combine(dir, "results.md"), stamp, suites, passes, fails, skips);
            WriteJson(Path.Combine(dir, "results.json"), stamp, suites, passes, fails, skips);
            Console.WriteLine("TOTAL " + passes + " PASS, " + fails + " FAIL, " + skips + " SKIP -> " + dir);
            return fails;
        }

        static void RunSuite(string suite)
        {
            switch (suite.ToLowerInvariant())
            {
                case "regression": Regression(); break;
                case "hash": HashSuite(); break;
                case "args": ArgsSuite(); break;
                case "pool": PoolSuite(); break;
                case "roundtrip": RoundtripSuite(); break;
                case "negative": NegativeSuite(); break;
                case "audio": AudioSuite(); break;
                case "failures": FailuresSuite(); break;
                case "reliability": ReliabilitySuite(); break;
                case "stress": StressSuite(); break;
                case "bench": BenchSuite(); break;
                case "realcapture": RealCaptureSuite(); break;
                case "hunt": HuntSuite(); break;
                case "hunt2cine": Hunt2CineSuite(); break;
                case "saturation": SaturationSuite(); break;
                case "h3cine": H3CineSuite(); break;
                case "h4cine": H4CineSuite(); break;
                case "w6record": W6RecordSuite(); break;
                default: Test("main", suite, () => { throw new TestFailed("unknown suite"); }); break;
            }
        }

        // Runs one test. The body reports detail with Note/Add, fails with Check, and skips with Skip.
        static void Test(string suite, string name, Action body)
        {
            currentDetail = "";
            currentData = new Dictionary<string, object>();
            string status = "PASS", text;
            Stopwatch clock = Stopwatch.StartNew();
            try
            {
                body();
                text = currentDetail;
            }
            catch (TestSkipped s)
            {
                status = "SKIP";
                text = s.Message;
            }
            catch (TestFailed f)
            {
                status = "FAIL";
                text = f.Message + (currentDetail.Length > 0 ? " | " + currentDetail : "");
            }
            catch (Exception e)
            {
                status = "FAIL";
                string top = (e.StackTrace ?? "").Split('\n')[0].Trim();
                text = e.GetType().Name + ": " + e.Message + " @ " + top + (currentDetail.Length > 0 ? " | " + currentDetail : "");
            }
            clock.Stop();
            Outcome o = new Outcome();
            o.Suite = suite;
            o.Name = name;
            o.Status = status;
            o.Detail = OneLine(text);
            o.Seconds = clock.Elapsed.TotalSeconds;
            o.Data = currentData;
            outcomes.Add(o);
            Console.WriteLine(status + " " + suite + "/" + name + " " + o.Detail + " [" + o.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s]");
        }

        static void Check(bool ok, string message) { if (!ok) throw new TestFailed(message); }
        static void Skip(string why) { throw new TestSkipped(why); }
        static void Note(string text) { currentDetail = text; }
        static void Add(string text) { currentDetail = currentDetail.Length == 0 ? text : currentDetail + "; " + text; }
        static void Put(string key, object value) { currentData[key] = value; }
        static void NeedFfmpeg() { if (!File.Exists(ffmpeg)) Skip("no hay ffmpeg en " + ffmpeg); }
        static string OneLine(string s) { return s.Replace("\r", " ").Replace("\n", " | "); }
        static string Q(string path) { return "\"" + path + "\""; }
        static string Inv(double d) { return d.ToString("0.###", CultureInfo.InvariantCulture); }

        static string WorkDir(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
            string dir = Path.Combine(workRoot, sb.ToString() + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void DeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        static void DeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ---- processes ----

        // Runs exe with stdin closed; stdout goes to consume (or is discarded); returns the exit code and stderr.
        static int RunProcess(string exe, string args, Action<Stream> consume, out string stderr)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            StringBuilder err = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) err.AppendLine(e.Data); };
                p.Start();
                p.StandardInput.Close();
                p.BeginErrorReadLine();
                if (consume != null) consume(p.StandardOutput.BaseStream);
                else p.StandardOutput.BaseStream.CopyTo(Stream.Null);
                p.WaitForExit();
                stderr = err.ToString();
                return p.ExitCode;
            }
        }

        static int Ffmpeg(string args, out string stderr)
        {
            return RunProcess(ffmpeg, args, null, out stderr);
        }

        static List<string> FfmpegLines(string args, out string stderr, out int exit)
        {
            List<string> lines = new List<string>();
            exit = RunProcess(ffmpeg, args, s =>
            {
                using (StreamReader r = new StreamReader(s, Encoding.ASCII))
                {
                    string line;
                    while ((line = r.ReadLine()) != null) lines.Add(line);
                }
            }, out stderr);
            return lines;
        }

        static string Sha256Stream(Stream s, out long bytes)
        {
            byte[] buf = new byte[1 << 20];
            long total = 0;
            using (SHA256 sha = SHA256.Create())
            {
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    sha.TransformBlock(buf, 0, n, null, 0);
                    total += n;
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                bytes = total;
                return HexBytes(sha.Hash);
            }
        }

        static string Sha256File(string path)
        {
            long bytes;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return Sha256Stream(fs, out bytes);
        }

        static string HexBytes(byte[] b)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        // Child processes of parent with the given image name (Toolhelp, so no System.Management reference is needed).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);

        static List<int> ChildPids(int parent, string imageName)
        {
            List<int> pids = new List<int>();
            IntPtr snap = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
            if (snap == new IntPtr(-1)) return pids;
            try
            {
                ProcessEntry e = new ProcessEntry();
                e.dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry));
                bool more = Process32FirstW(snap, ref e);
                while (more)
                {
                    if (e.th32ParentProcessID == (uint)parent && string.Equals(e.szExeFile, imageName, StringComparison.OrdinalIgnoreCase))
                        pids.Add((int)e.th32ProcessID);
                    more = Process32NextW(snap, ref e);
                }
            }
            finally { CloseHandle(snap); }
            return pids;
        }

        // ---- reference hash (written from the spec text) ----

        // px: canonical pixels B | G<<8 | R<<16, row-major.
        static ulong RefHash(int width, int height, int[] px)
        {
            unchecked
            {
                int n = width * height;
                ulong h = 0x9E3779B97F4A7C15UL ^ (((ulong)width << 32) | (uint)height);
                for (int i = 0; i < n; i += 2)
                {
                    ulong lo = (uint)px[i];
                    ulong hi = i + 1 < n ? (uint)px[i + 1] : 0UL;
                    ulong word = lo | (hi << 32);
                    h ^= word * 0xFF51AFD7ED558CCDUL;
                    h = ((h << 31) | (h >> 33)) * 0xC4CEB9FE1A85EC53UL;
                }
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                return h;
            }
        }

        static int[] CanonFromBgr0(byte[] b, int pixels)
        {
            int[] px = new int[pixels];
            for (int i = 0; i < pixels; i++) px[i] = b[4 * i] | (b[4 * i + 1] << 8) | (b[4 * i + 2] << 16);
            return px;
        }

        static byte[] RandomBytes(Random rnd, int n)
        {
            byte[] b = new byte[n];
            rnd.NextBytes(b);
            return b;
        }

        static void HashSuite()
        {
            Test("hash", "x-byte-ignored", () =>
            {
                Random rnd = new Random(1);
                byte[] a = RandomBytes(rnd, 13 * 7 * 4);
                byte[] b = (byte[])a.Clone();
                for (int i = 3; i < b.Length; i += 4) b[i] = (byte)rnd.Next(256);
                Check(CineHash.Bgr0(a, 13, 7) == CineHash.Bgr0(b, 13, 7), "el byte X cambia el hash");
            });

            Test("hash", "any-bgr-byte-changes-hash", () =>
            {
                int w = 37, h = 9, n = w * h;
                Random rnd = new Random(2);
                byte[] a = RandomBytes(rnd, n * 4);
                ulong baseHash = CineHash.Bgr0(a, w, h);
                int[] pixels = { 0, 1, 2, 17, 100, 332, n - 1 };
                int changes = 0;
                foreach (int p in pixels)
                    for (int c = 0; c < 3; c++)
                    {
                        byte[] b = (byte[])a.Clone();
                        b[4 * p + c] ^= 0x01;
                        Check(CineHash.Bgr0(b, w, h) != baseHash, "cambiar el byte " + c + " del pixel " + p + " no cambia el hash");
                        changes++;
                    }
                Note(changes + " cambios de un byte detectados");
            });

            Test("hash", "swapped-pixels-change-hash", () =>
            {
                int w = 16, h = 4, n = w * h;
                Random rnd = new Random(3);
                byte[] a = RandomBytes(rnd, n * 4);
                ulong baseHash = CineHash.Bgr0(a, w, h);
                int[][] pairs = { new[] { 0, 1 }, new[] { 0, n - 1 }, new[] { 5, 6 }, new[] { 2, 3 } };
                foreach (int[] pr in pairs)
                {
                    byte[] b = (byte[])a.Clone();
                    for (int c = 0; c < 4; c++)
                    {
                        byte t = b[4 * pr[0] + c];
                        b[4 * pr[0] + c] = b[4 * pr[1] + c];
                        b[4 * pr[1] + c] = t;
                    }
                    Check(CineHash.Bgr0(b, w, h) != baseHash, "intercambiar los pixeles " + pr[0] + " y " + pr[1] + " no cambia el hash");
                }
            });

            Test("hash", "dimensions-are-hashed", () =>
            {
                byte[] a = RandomBytes(new Random(4), 16 * 3 * 4);
                ulong h1 = CineHash.Bgr0(a, 16, 3), h2 = CineHash.Bgr0(a, 8, 6), h3 = CineHash.Bgr0(a, 3, 16), h4 = CineHash.Bgr0(a, 48, 1);
                Check(h1 != h2 && h1 != h3 && h1 != h4 && h2 != h3 && h2 != h4 && h3 != h4, "dos dimensiones distintas dan el mismo hash");
            });

            Test("hash", "bgr0-ptr-array-gbrp-agree", () =>
            {
                int[][] sizes = { new[] { 15, 3 }, new[] { 1920, 1080 }, new[] { 1, 1 }, new[] { 7, 5 }, new[] { 33, 17 }, new[] { 640, 360 } };
                Random rnd = new Random(5);
                foreach (int[] sz in sizes)
                {
                    int w = sz[0], h = sz[1], n = w * h;
                    byte[] bgr = RandomBytes(rnd, n * 4);
                    byte[] planes = new byte[n * 3];
                    for (int i = 0; i < n; i++)
                    {
                        planes[i] = bgr[4 * i + 1];           // G plane first
                        planes[n + i] = bgr[4 * i];           // then B
                        planes[2 * n + i] = bgr[4 * i + 2];   // then R
                    }
                    ulong expected = RefHash(w, h, CanonFromBgr0(bgr, n));
                    IntPtr ptr = Marshal.AllocHGlobal(bgr.Length);
                    try
                    {
                        Marshal.Copy(bgr, 0, ptr, bgr.Length);
                        long[] scratch = new long[(n * 4 + 7) / 8];
                        ulong fromPtr = CineHash.Bgr0(ptr, w, h, ref scratch);
                        ulong fromArray = CineHash.Bgr0(bgr, w, h);
                        ulong fromGbrp = CineHash.Gbrp(planes, w, h);
                        Check(fromPtr == expected, w + "x" + h + ": Bgr0(IntPtr) difiere de la referencia");
                        Check(fromArray == expected, w + "x" + h + ": Bgr0(byte[]) difiere de la referencia");
                        Check(fromGbrp == expected, w + "x" + h + ": Gbrp difiere de la referencia");
                    }
                    finally { Marshal.FreeHGlobal(ptr); }
                }
                Note(sizes.Length + " tamanos, incluidos 15x3 y 1920x1080");
            });

            Test("hash", "matches-reference-on-random-data", () =>
            {
                Random rnd = new Random(6);
                for (int t = 0; t < 60; t++)
                {
                    int w = rnd.Next(1, 71), h = rnd.Next(1, 71), n = w * h;
                    byte[] bgr = RandomBytes(rnd, n * 4);
                    ulong expected = RefHash(w, h, CanonFromBgr0(bgr, n));
                    Check(CineHash.Bgr0(bgr, w, h) == expected, "caso " + t + " (" + w + "x" + h + ") difiere de la referencia");
                }
                Note("60 casos aleatorios");
            });

            Test("hash", "hex-format", () =>
            {
                Check(CineHash.Hex(0x0123456789ABCDEFUL) == "0123456789abcdef", "Hex no es minusculas o no tiene el orden correcto");
                Check(CineHash.Hex(0UL) == "0000000000000000", "Hex(0)");
                Check(CineHash.Hex(ulong.MaxValue) == "ffffffffffffffff", "Hex(max)");
                Random rnd = new Random(7);
                for (int i = 0; i < 1000; i++)
                {
                    ulong v = ((ulong)(uint)rnd.Next() << 32) | (uint)rnd.Next();
                    string s = CineHash.Hex(v);
                    Check(s.Length == 16 && System.Text.RegularExpressions.Regex.IsMatch(s, "^[0-9a-f]{16}$"), "Hex mal formado: " + s);
                }
            });
        }

        // ---- args ----

        static void ArgsSuite()
        {
            Test("args", "constants", () =>
            {
                Check(Cine.Extension == ".mkv", "Extension");
                Check(Cine.InputPixelFormat == "bgr0" && Cine.StoredPixelFormat == "gbrp", "formatos");
                Check(Cine.RealtimePixelRate == 1920L * 1200 * 60, "RealtimePixelRate");
                Check(Cine.MinFreeBytes == 2L << 30, "MinFreeBytes");
                Check(Cine.ReserveBytes == 512L << 20, "ReserveBytes");
            });

            Test("args", "live-args", () =>
            {
                int[][] sizes = { new[] { 1920, 1080 }, new[] { 3840, 2160 }, new[] { 1280, 720 } };
                foreach (int[] sz in sizes)
                {
                    string a = Cine.LiveArgs(sz[0], sz[1], 60, 1, @"C:\tmp\v.nut");
                    Check(a.Contains("libx264rgb"), "falta libx264rgb: " + a);
                    Check(a.Contains("-qp 0"), "falta -qp 0: " + a);
                    Check(a.Contains("-pix_fmt bgr0"), "falta -pix_fmt bgr0: " + a);
                    Check(a.Contains("-f nut"), "falta -f nut: " + a);
                    Check(a.Contains("-video_size " + sz[0] + "x" + sz[1]), "falta el tamano: " + a);
                    Check(a.Contains("-framerate 60/1"), "falta el framerate: " + a);
                    foreach (string bad in new[] { "-vf", "scale", "format=", "yuv", "crf", "aac", "nv12" })
                        Check(!a.Contains(bad), "no debe contener '" + bad + "': " + a);
                    Check(CountOf(a, "-pix_fmt") == 1, "debe haber un solo -pix_fmt (el de entrada): " + a);
                }
                string nd = Cine.LiveArgs(1920, 1080, 60000, 1001, @"C:\tmp\v.nut");
                Check(nd.Contains("-framerate 60000/1001"), "framerate NTSC: " + nd);
                Check(nd.Contains("-g 120"), "el GOP de 60000/1001 deberia ser 120: " + nd);
            });

            Test("args", "master-none", () =>
            {
                string a = Cine.MasterArgs(@"C:\t\v.nut", null, null, "", @"C:\t\m.mkv");
                CheckMasterCommon(a);
                Check(!a.Contains("f32le") && !a.Contains("amix") && !a.Contains("-map 1:a"), "sin audio no debe haber pistas: " + a);
                Check(a.Contains("-f matroska"), "falta -f matroska");
            });

            Test("args", "master-sys", () =>
            {
                string a = Cine.MasterArgs(@"C:\t\v.nut", @"C:\t\sys.f32", null, "", @"C:\t\m.mkv");
                CheckMasterCommon(a);
                Check(a.Contains("Sonido del equipo (original)"), "falta el titulo del sonido del equipo: " + a);
                Check(a.Contains("-c:a copy"), "la pista original debe copiarse: " + a);
                Check(!a.Contains("amix"), "con una sola fuente no hay mezcla: " + a);
                Check(a.Contains("-f matroska"), "falta -f matroska");
            });

            Test("args", "master-mic", () =>
            {
                string a = Cine.MasterArgs(@"C:\t\v.nut", null, @"C:\t\mic.f32", ",volume=3.0dB", @"C:\t\m.mkv");
                CheckMasterCommon(a);
                Check(a.Contains("Micr\u00F3fono (original)"), "falta el titulo del micro: " + a);
                Check(a.Contains("-c:a copy"), "la pista original debe copiarse: " + a);
                Check(!a.Contains("amix"), "con una sola fuente no hay mezcla: " + a);
                Check(a.Contains("-f matroska"), "falta -f matroska");
            });

            Test("args", "master-both", () =>
            {
                string a = Cine.MasterArgs(@"C:\t\v.nut", @"C:\t\sys.f32", @"C:\t\mic.f32", ",volume=3.0dB", @"C:\t\m.mkv");
                CheckMasterCommon(a);
                Check(a.Contains("amix"), "falta la mezcla amix: " + a);
                Check(a.Contains("alimiter"), "falta el limitador: " + a);
                Check(a.Contains("highpass=f=70"), "falta el filtro de voz: " + a);
                Check(a.Contains(",volume=3.0dB"), "falta la ganancia del micro: " + a);
                Check(a.Contains("Mezcla (equipo + micr\u00F3fono)"), "falta el titulo de la mezcla: " + a);
                Check(a.Contains("Sonido del equipo (original)") && a.Contains("Micr\u00F3fono (original)"), "faltan los originales: " + a);
                Check(a.Contains("-c:a:0 pcm_f32le"), "la mezcla debe ser pcm_f32le: " + a);
                Check(a.Contains("-c:a:1 copy") && a.Contains("-c:a:2 copy"), "originales sin copia: " + a);
                Check(a.Contains("-f matroska"), "falta -f matroska");
            });

            Test("args", "probe-and-decode-args", () =>
            {
                string m = @"C:\t\m.mkv";
                Check(Cine.ProbeArgs(m).Contains("-map 0 -c copy -f framecrc -"), "ProbeArgs: " + Cine.ProbeArgs(m));
                string vd = Cine.VideoDecodeArgs(m);
                // Every decoded frame exactly once (no frame-rate fitting) and no conversion: the stream already is gbrp.
                Check(vd.Contains("-map 0:v:0") && vd.Contains("-fps_mode passthrough") && vd.EndsWith("-f rawvideo -pix_fmt gbrp -") &&
                      !vd.Contains("scale") && !vd.Contains("-vf") && !vd.Contains("-r "), "VideoDecodeArgs: " + vd);
                Check(Cine.AudioDecodeArgs(m, 2).Contains("-map 0:a:2 -c:a pcm_f32le -f f32le -"), "AudioDecodeArgs: " + Cine.AudioDecodeArgs(m, 2));
            });

            Test("args", "gop", () =>
            {
                Check(Cine.Gop(60, 1) == 120, "Gop(60/1)");
                Check(Cine.Gop(60000, 1001) == 120, "Gop(60000/1001)");
                Check(Cine.Gop(30, 1) == 60, "Gop(30/1)");
                Check(Cine.Gop(1, 1) == 2, "Gop(1/1) debe ser 2 (minimo)");
                Check(Cine.Gop(1, 10) == 2, "Gop(1/10) debe ser 2 (minimo)");
                Check(Cine.Gop(240, 1) == 480, "Gop(240/1)");
                Check(Cine.Gop(24, 1) == 48, "Gop(24/1)");
                int[][] cases = { new[] { 60, 1 }, new[] { 60000, 1001 }, new[] { 25, 1 }, new[] { 7, 3 }, new[] { 1, 1 }, new[] { 90, 1 } };
                foreach (int[] c in cases)
                {
                    int expected = Math.Max(2, (int)Math.Round(2.0 * c[0] / c[1]));
                    Check(Cine.Gop(c[0], c[1]) == expected, "Gop(" + c[0] + "/" + c[1] + ") = " + Cine.Gop(c[0], c[1]) + ", esperado " + expected);
                }
            });

            Test("args", "audio-frames-for", () =>
            {
                Check(Cine.AudioFramesFor(60, 60, 1) == 48000, "60 fotogramas a 60 fps = 1 s");
                Check(Cine.AudioFramesFor(1, 30, 1) == 1600, "1 fotograma a 30 fps");
                int[][] cases = { new[] { 60, 60, 1 }, new[] { 1000, 60000, 1001 }, new[] { 77, 30, 1 }, new[] { 3, 7, 3 } };
                foreach (int[] c in cases)
                {
                    long expected = (long)Math.Round(c[0] * (double)c[2] / c[1] * 48000);
                    Check(Cine.AudioFramesFor(c[0], c[1], c[2]) == expected, "frames=" + c[0] + " rate=" + c[1] + "/" + c[2]);
                }
            });

            Test("args", "pool-frames-clamp", () =>
            {
                long gib = 1L << 30;
                long[][] cases = {
                    new[] { 1920L * 1080 * 4, 16 * gib },
                    new[] { 1920L * 1080 * 4, gib },
                    new[] { 1920L * 1080 * 4, 0 },
                    new[] { 3840L * 2160 * 4, 64 * gib },
                    new[] { 16, 64 * gib },
                    new[] { 1, 64 * gib },
                    new[] { 4L << 20, 4 * gib },
                    new[] { 2560L * 1440 * 4, 3 * gib },
                };
                // PoolFrames now takes the budget; the normal memory level (PoolBudget 0) must keep the original rule
                // exactly: min(2 GiB, avail/4), between 8 and 600 frames.
                foreach (long[] c in cases)
                {
                    long cap = Math.Min(2L << 30, c[1] / 4);
                    long expected = Math.Max(8, Math.Min(600, cap / c[0]));
                    int got = Cine.PoolFrames(c[0], Cine.PoolBudget(0, c[1]));
                    Check(got == expected, "PoolFrames(" + c[0] + ", PoolBudget(0, " + c[1] + ")) = " + got + ", esperado " + expected);
                }
                Check(Cine.PoolFrames(1920L * 1080 * 4, Cine.PoolBudget(0, gib)) == 32, "1 GiB disponible: avail/4 = 256 MiB / 8,3 MB = 32");
                Check(Cine.PoolFrames(4L << 20, Cine.PoolBudget(0, 4 * gib)) == 256, "4 GiB disponibles, fotograma de 4 MiB: 1 GiB / 4 MiB = 256");
                Check(Cine.PoolFrames(16, Cine.PoolBudget(0, 64 * gib)) == 600, "fotogramas pequenos: tope 600");
                Check(Cine.PoolFrames(33177600L, Cine.PoolBudget(0, 0)) == 8, "sin memoria: minimo 8");
            });

            Test("args", "encoder-and-disk", () =>
            {
                NeedFfmpeg();
                Check(Cine.EncoderAvailable(ffmpeg), "libx264rgb no disponible en " + ffmpeg);
                long free = Cine.FreeBytes(Path.GetTempPath());
                Check(free > 0, "FreeBytes(TEMP) = " + free);
                Note("libx264rgb disponible; TEMP libre " + (free >> 20) + " MiB");
            });
        }

        static int CountOf(string s, string part)
        {
            int n = 0, at = 0;
            while ((at = s.IndexOf(part, at, StringComparison.Ordinal)) >= 0) { n++; at += part.Length; }
            return n;
        }

        static void CheckMasterCommon(string a)
        {
            Check(a.Contains("-c:v copy"), "el video debe copiarse: " + a);
            foreach (string bad in new[] { "aac", "libx264", "crf", "scale" })
                Check(!a.Contains(bad), "el master no debe re-codificar (" + bad + "): " + a);
        }

        // ---- pool ----

        static void PoolSuite()
        {
            Test("pool", "take-until-zero-give-back-dispose", () =>
            {
                FramePool pool = new FramePool(4096, 5);
                Check(pool.Capacity == 5, "Capacity = " + pool.Capacity);
                List<IntPtr> taken = new List<IntPtr>();
                for (int i = 0; i < 5; i++)
                {
                    IntPtr p = pool.TryTake();
                    Check(p != IntPtr.Zero, "TryTake " + i + " devolvio cero antes de llegar a la capacidad");
                    taken.Add(p);
                }
                Check(pool.InUse == 5, "InUse = " + pool.InUse + " con 5 en uso");
                Check(pool.TryTake() == IntPtr.Zero, "TryTake en capacidad debe devolver cero");
                Check(pool.TryTake() == IntPtr.Zero, "segundo TryTake en capacidad debe devolver cero");
                pool.Give(taken[0]);
                Check(pool.InUse == 4, "InUse tras Give = " + pool.InUse);
                IntPtr again = pool.TryTake();
                Check(again != IntPtr.Zero, "TryTake tras Give debe funcionar");
                Check(pool.InUse == 5, "InUse tras reutilizar = " + pool.InUse);
                for (int i = 1; i < taken.Count; i++) pool.Give(taken[i]);
                pool.Give(again);
                Check(pool.InUse == 0, "InUse tras devolver todo = " + pool.InUse);
                pool.Dispose();
                pool.Dispose();
            });
        }

        // ---- regression: the new argument builders reproduce the golden file ----

        static void Regression()
        {
            Test("regression", "profile-constants", () =>
            {
                Check(Recorder.Standard == 0 && Recorder.High == 2 && Recorder.Maximum == 1 && Recorder.Cinema == 3,
                      "Standard/High/Maximum/Cinema = " + Recorder.Standard + "/" + Recorder.High + "/" + Recorder.Maximum + "/" + Recorder.Cinema);
            });

            Test("regression", "baseline-profiles", () =>
            {
                string path = Path.Combine(testsDir, "baseline-profiles.txt");
                Check(File.Exists(path), "no existe " + path);
                string golden = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
                List<string> generated = ScenarioRecords();
                string text = string.Join("\n", generated.ToArray()) + "\n";
                if (text == golden) { Note(generated.Count + " escenarios identicos al original"); return; }

                // Cinema is no longer a recording codec: only the share export uses it, at crf 14 instead of the old crf 10.
                // Exactly the FinalCodec q=3 entries are exempt, and their new values are asserted here.
                List<string> expected = SplitRecords(golden.TrimEnd('\n').Split('\n'));
                int exempt = 0;
                for (int i = 0; i < expected.Count && i < generated.Count; i++)
                {
                    if (!expected[i].StartsWith("FinalCodec q=" + Recorder.Cinema + " ", StringComparison.Ordinal)) continue;
                    Check(expected[i].Contains(" -crf 10 "), "el original de '" + OneLine(expected[i]) + "' ya no lleva -crf 10");
                    string want = expected[i].Replace(" -crf 10 ", " -crf 14 ");
                    Check(generated[i] == want, "FinalCodec q=3 esperado [" + OneLine(want) + "] obtenido [" + OneLine(generated[i]) + "]");
                    expected[i] = generated[i];
                    exempt++;
                }
                Check(exempt == 6, "se esperaban 6 entradas FinalCodec q=3 exentas, hay " + exempt);
                int differing = 0;
                List<string> first = new List<string>();
                for (int i = 0; i < Math.Max(expected.Count, generated.Count); i++)
                {
                    string e = i < expected.Count ? expected[i] : "(falta)";
                    string g = i < generated.Count ? generated[i] : "(sobra)";
                    if (e == g) continue;
                    differing++;
                    if (first.Count < 5) first.Add("#" + i + " esperado [" + OneLine(e) + "] obtenido [" + OneLine(g) + "]");
                }
                if (differing == 0 && expected.Count == generated.Count)
                {
                    Note(generated.Count + " escenarios identicos al original salvo " + exempt + " de FinalCodec q=3 (crf 14, comprobados)");
                    return;
                }
                Check(false, differing + " escenarios distintos de " + expected.Count + " (generados " + generated.Count + "). Primeros: " +
                      string.Join(" / ", first.ToArray()));
            });
        }

        static List<string> SplitRecords(string[] lines)
        {
            List<string> records = new List<string>();
            StringBuilder cur = null;
            foreach (string line in lines)
            {
                if (line.Length > 0 && line[0] != ' ')
                {
                    if (cur != null) records.Add(cur.ToString());
                    cur = new StringBuilder(line);
                }
                else if (cur != null) cur.Append("\n").Append(line);
            }
            if (cur != null) records.Add(cur.ToString());
            return records;
        }

        // The scenarios of tests/cine/LegacyBaseline.cs (Lines()), built with the new Recorder functions.
        static List<string> ScenarioRecords()
        {
            int[][] sizes = { new[] { 1280, 720 }, new[] { 1920, 1080 }, new[] { 1920, 1200 }, new[] { 2560, 1440 }, new[] { 3840, 2160 }, new[] { 1000, 2000 } };
            string[] hws = { null, "h264_nvenc", "h264_qsv", "h264_amf" };
            int[][] rates = { new[] { 60, 1 }, new[] { 60000, 1001 }, new[] { 30, 1 }, new[] { 24, 1 } };
            string[] gains = { "", ",volume=3.0dB" };
            const string temp = "T", output = "O.mp4";
            List<string> records = new List<string>();
            foreach (int quality in new[] { Recorder.Standard, Recorder.High, Recorder.Maximum, -1 })
                foreach (int videoFps in quality == Recorder.Standard ? new[] { 24, 30, 60 } : new[] { 60 })
                    foreach (int[] sz in sizes)
                        foreach (bool gpuNv12 in new[] { false, true })
                            foreach (string hw in hws)
                                foreach (int[] rate in rates)
                                    for (int audioMask = 0; audioMask < 4; audioMask++)
                                        foreach (string gain in gains)
                                        {
                                            bool gif = quality < 0;
                                            if (gif && audioMask != 0) continue;
                                            if ((audioMask & 2) == 0 && gain != "") continue;
                                            int q = gif ? Recorder.Standard : quality;
                                            int w = sz[0], h = sz[1];
                                            bool audio = audioMask != 0;
                                            int target = gif ? Math.Max(10, 15 * 2) : q == Recorder.Standard ? videoFps : 60;
                                            bool large = !gif && (long)w * h * target > 1920L * 1200 * 60;
                                            bool nv12 = large && gpuNv12 && w % 2 == 0 && h % 2 == 0;
                                            string outName = gif ? "O.gif" : output;
                                            LivePlan plan = Recorder.PlanLive(q, gif, videoFps, 15, w, h, nv12, large, hw, audio,
                                                                              rate[0], rate[1], outName, temp);
                                            string fin;
                                            if (gif) fin = Recorder.GifArgs(w, 15, plan.Video, "O.gif");
                                            else if (plan.Direct) fin = "-";
                                            else
                                                fin = Recorder.FinishArgs(q, plan.Lossless, nv12, w, h, rate[0], rate[1], plan.Video,
                                                                          (audioMask & 1) != 0 ? temp + " equipo.f32" : null,
                                                                          (audioMask & 2) != 0 ? temp + " micro.f32" : null, gain, output);
                                            string key = (gif ? "gif" : q == Recorder.Standard ? "standard" : q == Recorder.High ? "high" : "maximum") +
                                                         " fps=" + videoFps + " " + w + "x" + h + " gpuNv12=" + gpuNv12 + " hw=" + (hw ?? "none") +
                                                         " rate=" + rate[0] + "/" + rate[1] + " audio=" + audioMask + " gain=" + (gain == "" ? "none" : "3dB");
                                            records.Add(key + "\n  live: " + plan.Args + "\n  final: " + fin);
                                        }
            foreach (int quality in new[] { Recorder.Standard, Recorder.High, Recorder.Maximum, Recorder.Cinema })
                foreach (long px in new long[] { 1920L * 1080, 2400001L })
                    foreach (double fps in new[] { 30.0, 59.94, 60.0 })
                        records.Add("FinalCodec q=" + quality + " px=" + px + " fps=" + fps.ToString(CultureInfo.InvariantCulture) +
                                    "\n  " + Recorder.FinalCodec(quality, px, fps));
            foreach (int quality in new[] { Recorder.Standard, Recorder.High, Recorder.Maximum, Recorder.Cinema })
                records.Add("AudioCodec q=" + quality + "\n  " + Recorder.AudioCodec(quality));
            return records;
        }

        // ---- results ----

        static void WriteMarkdown(string path, string stamp, List<string> suites, int passes, int fails, int skips)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Cine tests ").Append(stamp).Append("\n\n");
            sb.Append("Suites: ").Append(string.Join(", ", suites.ToArray())).Append("\n\n");
            sb.Append("PASS ").Append(passes).Append(" - FAIL ").Append(fails).Append(" - SKIP ").Append(skips).Append("\n\n");
            sb.Append("| Result | Test | Detail | s |\n|---|---|---|---|\n");
            foreach (Outcome o in outcomes)
                sb.Append("| ").Append(o.Status).Append(" | ").Append(o.Suite).Append('/').Append(o.Name).Append(" | ")
                  .Append(o.Detail.Replace("|", "/")).Append(" | ").Append(o.Seconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(" |\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        static void WriteJson(string path, string stamp, List<string> suites, int passes, int fails, int skips)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            foreach (Outcome o in outcomes)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["suite"] = o.Suite;
                d["name"] = o.Name;
                d["status"] = o.Status;
                d["detail"] = o.Detail;
                d["seconds"] = o.Seconds;
                d["data"] = o.Data;
                list.Add(d);
            }
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["stamp"] = stamp;
            root["suites"] = suites;
            root["pass"] = passes;
            root["fail"] = fails;
            root["skip"] = skips;
            root["tests"] = list;
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            File.WriteAllText(path, js.Serialize(root), new UTF8Encoding(false));
        }
    }
}
