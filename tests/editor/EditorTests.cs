// Tests of the editor's export, frame strip and saves. Headless: no window is shown and the screen is never captured.
// Suites: args export strip atomic keep tools player playerwindow share windows clips (all by default), and sharebench
// (only when named: a benchmark of the share export, see EditorTestsShareBench.cs).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Stackshot.Tests
{
    static partial class EditorTests
    {
        sealed class TestFailed : Exception { public TestFailed(string message) : base(message) { } }
        sealed class TestSkipped : Exception { public TestSkipped(string message) : base(message) { } }

        sealed class Outcome
        {
            public string Suite, Name, Status, Detail;
            public double Seconds;
        }

        static readonly List<Outcome> outcomes = new List<Outcome>();
        static readonly string[] defaultSuites = { "args", "export", "strip", "atomic", "keep", "tools", "player", "playerwindow", "share", "windows", "clips", "huntexport", "huntplayer", "hunttools", "playerplus", "w6media" };
        static string currentDetail;
        static string testsDir, workRoot, ffmpeg;

        [STAThread]
        static int Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("STACKSHOT_EDITOR_DIR");
            testsDir = string.IsNullOrEmpty(env) ? @"C:\Projects\stackshot\tests\editor" : env;
            ffmpeg = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Stackshot", "ffmpeg", "ffmpeg.exe");
            workRoot = Path.Combine(Path.GetTempPath(), "stackshot-editor-tests", "work");
            Settings.ReadOnly = true;
            I18n.Apply("es");   // the suites check the Spanish texts, whatever language this Windows is in
            // Never the real temp folder of the app.
            AtomicFile.WatchedFolder = Path.Combine(workRoot, "watched-none");
            Directory.CreateDirectory(workRoot);

            List<string> suites = args.Length > 0 ? new List<string>(args) : new List<string>(defaultSuites);
            foreach (string suite in suites) RunSuite(suite);
            try { Directory.Delete(workRoot, true); } catch { }

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
            WriteResults(dir, stamp, passes, fails, skips);
            Console.WriteLine("TOTAL " + passes + " PASS, " + fails + " FAIL, " + skips + " SKIP -> " + dir);
            return fails;
        }

        static void RunSuite(string suite)
        {
            switch (suite.ToLowerInvariant())
            {
                case "args": ArgsSuite(); break;
                case "export": ExportSuite(); break;
                case "strip": StripSuite(); break;
                case "atomic": AtomicSuite(); break;
                case "keep": KeepSuite(); break;
                case "tools": ToolsSuite(); break;
                case "player": PlayerSuite(); break;
                case "playerwindow": PlayerWindowSuite(); break;
                case "share": ShareSuite(); break;
                case "windows": WindowsSuite(); break;
                case "clips": ClipsSuite(); break;
                case "huntexport": HuntSuite(); break;
                case "huntplayer": HpSuite(); break;
                case "hunttools": HuntToolsSuite(); break;
                case "hunt2player": Hunt2PlayerSuite(); break;
                case "playerplus": PlayerPlusSuite(); break;
                case "h3export": H3ExportSuite(); break;
                case "h3player": H3pSuite(); break;
                case "h4editor": H4EditorSuite(); break;
                case "w6media": W6MediaSuite(); break;
                case "hunt2export": Hunt2ExportSuite(); break;
                case "hunt2editor": Hunt2EditorSuite(); break;
                case "sharebench": ShareBenchSuite(); break;
                default: Test("main", suite, delegate { throw new TestFailed("unknown suite"); }); break;
            }
        }

        static void Test(string suite, string name, Action body)
        {
            currentDetail = "";
            string status = "PASS", text;
            Stopwatch clock = Stopwatch.StartNew();
            try
            {
                body();
                text = currentDetail;
            }
            catch (TestSkipped s) { status = "SKIP"; text = s.Message; }
            catch (TestFailed f) { status = "FAIL"; text = f.Message + (currentDetail.Length > 0 ? " | " + currentDetail : ""); }
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
            o.Detail = (text ?? "").Replace("\r", " ").Replace("\n", " | ");
            o.Seconds = clock.Elapsed.TotalSeconds;
            outcomes.Add(o);
            Console.WriteLine(status + " " + suite + "/" + name + " " + o.Detail + " [" + o.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s]");
        }

        static void Check(bool ok, string message) { if (!ok) throw new TestFailed(message); }
        static void Skip(string why) { throw new TestSkipped(why); }
        static void Add(string text) { currentDetail = currentDetail.Length == 0 ? text : currentDetail + "; " + text; }
        static void NeedFfmpeg() { if (!File.Exists(ffmpeg)) Skip("no hay ffmpeg en " + ffmpeg); }
        static string Q(string path) { return "\"" + path + "\""; }

        static string WorkDir(string name)
        {
            string dir = Path.Combine(workRoot, name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // FFmpeg with stdout and stderr captured (an export prints -progress on stdout).
        static int Ff(string args, out string stderr, int timeoutMs)
        {
            StringBuilder err = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = ffmpeg;
                p.StartInfo.Arguments = "-nostdin " + args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
                p.OutputDataReceived += delegate { };
                p.Start();
                ChildJob.Add(p);
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                bool done = p.WaitForExit(timeoutMs);
                if (!done) { try { p.Kill(); } catch { } }
                p.WaitForExit(3000);
                lock (err) stderr = err.ToString();
                return done ? p.ExitCode : -1;
            }
        }

        static void FfOk(string args, int timeoutMs)
        {
            string err;
            int code = Ff(args, out err, timeoutMs);
            if (code != 0) throw new TestFailed("ffmpeg " + code + ": " + Tail(err));
        }

        static string Tail(string t)
        {
            t = (t ?? "").Trim();
            return t.Length > 400 ? t.Substring(t.Length - 400) : t;
        }

        static string Probe(string file)
        {
            string err;
            Ff("-hide_banner -i " + Q(file), out err, 30000);
            return err;
        }

        static double DurationOf(string probe)
        {
            Match m = Regex.Match(probe, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return -1;
            return int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        }

        // One pixel of the first frame, decoded the way players decode tagged HD video (BT.709, limited range for YUV).
        static int[] PixelAt(string file, int x, int y, bool yuv)
        {
            string raw = file + "." + x + "x" + y + ".raw";
            string conv = yuv ? "scale=in_color_matrix=bt709:in_range=tv:flags=accurate_rnd+full_chroma_int," : "";
            FfOk("-hide_banner -loglevel error -y -i " + Q(file) + " -vf \"crop=2:2:" + (x & ~1) + ":" + (y & ~1) + "," + conv + "format=rgb24\" -frames:v 1 -f rawvideo " + Q(raw), 60000);
            byte[] b = File.ReadAllBytes(raw);
            File.Delete(raw);
            Check(b.Length >= 3, "no pixel decoded from " + Path.GetFileName(file));
            return new int[] { b[0], b[1], b[2] };
        }

        static string Rgb(int[] c) { return "(" + c[0] + "," + c[1] + "," + c[2] + ")"; }

        static int Diff(int[] a, int[] b)
        {
            return Math.Max(Math.Abs(a[0] - b[0]), Math.Max(Math.Abs(a[1] - b[1]), Math.Abs(a[2] - b[2])));
        }

        static void WriteResults(string dir, string stamp, int passes, int fails, int skips)
        {
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Editor tests " + stamp);
            md.AppendLine();
            md.AppendLine(passes + " PASS, " + fails + " FAIL, " + skips + " SKIP");
            md.AppendLine();
            md.AppendLine("| Suite | Test | Result | Seconds | Detail |");
            md.AppendLine("|---|---|---|---|---|");
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            foreach (Outcome o in outcomes)
            {
                md.AppendLine("| " + o.Suite + " | " + o.Name + " | " + o.Status + " | " + o.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " | " + o.Detail.Replace("|", "/") + " |");
                Dictionary<string, object> r = new Dictionary<string, object>();
                r["suite"] = o.Suite;
                r["name"] = o.Name;
                r["status"] = o.Status;
                r["seconds"] = Math.Round(o.Seconds, 2);
                r["detail"] = o.Detail;
                rows.Add(r);
            }
            File.WriteAllText(Path.Combine(dir, "results.md"), md.ToString());
            Dictionary<string, object> all = new Dictionary<string, object>();
            all["stamp"] = stamp;
            all["pass"] = passes;
            all["fail"] = fails;
            all["skip"] = skips;
            all["tests"] = rows;
            File.WriteAllText(Path.Combine(dir, "results.json"), new JavaScriptSerializer().Serialize(all));
        }
    }
}
