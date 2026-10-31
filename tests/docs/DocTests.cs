// Tests of the Markdown and XML editor: byte-faithful files, the editing commands, find and replace, the tokenizers,
// performance budgets and offscreen renders of the window. Nothing is shown on the desktop: windows are parked far off
// screen and drawn to bitmaps; alerts and the save dialog are answered here; the real settings are never written.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Threading;

namespace Stackshot.Tests
{
    static partial class DocTests
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
        static readonly string[] defaultSuites = { "io", "tools", "commands", "find", "syntax", "perf", "render", "editor", "window", "lists", "xmlinner", "w6xml", "w6md" };
        static string currentDetail;
        static Dictionary<string, object> currentData;
        static string testsDir, workRoot, resultDir;

        [STAThread]
        static int Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("STACKSHOT_DOCS_DIR");
            testsDir = string.IsNullOrEmpty(env) ? @"C:\Projects\stackshot\tests\docs" : env;
            workRoot = Path.Combine(Path.GetTempPath(), "stackshot-doc-tests", "work");
            Settings.ReadOnly = true;
            I18n.Apply("es");   // the suites check the Spanish texts, whatever language this Windows is in
            ShotStack.LogPath = null;   // set once the results folder exists
            Directory.CreateDirectory(workRoot);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            resultDir = Path.Combine(testsDir, "results", stamp);
            Directory.CreateDirectory(resultDir);
            ShotStack.LogPath = Path.Combine(resultDir, "stackshot.log");

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
            WriteMarkdown(Path.Combine(resultDir, "results.md"), stamp, suites, passes, fails, skips);
            WriteJson(Path.Combine(resultDir, "results.json"), stamp, suites, passes, fails, skips);
            Console.WriteLine("TOTAL " + passes + " PASS, " + fails + " FAIL, " + skips + " SKIP -> " + resultDir);
            return fails;
        }

        static void RunSuite(string suite)
        {
            switch (suite.ToLowerInvariant())
            {
                case "io": IoSuite(); break;
                case "tools": ToolsSuite(); break;
                case "commands": CommandsSuite(); break;
                case "find": FindSuite(); break;
                case "syntax": SyntaxSuite(); break;
                case "perf": PerfSuite(); break;
                case "render": RenderSuite(); break;
                case "editor": EditorSuite(); break;
                case "window": WindowSuite(); break;
                case "hunt": HuntSuite(); break;
                case "hunt2docs": Hunt2DocsSuite(); break;
                case "lists": ListsSuite(); break;
                case "h3docs": H3DocsSuite(); break;
                case "h4docs": H4DocsSuite(); break;
                case "xmlinner": XmlInnerSuite(); break;
                case "w6xml": W6XmlSuite(); break;
                case "w6md": W6MdSuite(); break;
                default: Test("main", suite, delegate { throw new TestFailed("unknown suite"); }); break;
            }
        }

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
            catch (TestSkipped s) { status = "SKIP"; text = s.Message; }
            catch (TestFailed f) { status = "FAIL"; text = f.Message + (currentDetail.Length > 0 ? " | " + currentDetail : ""); }
            catch (Exception e)
            {
                status = "FAIL";
                string top = (e.StackTrace ?? "").Split('\n')[0].Trim();
                text = e.GetType().Name + ": " + e.Message + " @ " + top + (currentDetail.Length > 0 ? " | " + currentDetail : "");
            }
            clock.Stop();
            Outcome o = new Outcome { Suite = suite, Name = name, Status = status, Detail = OneLine(text), Seconds = clock.Elapsed.TotalSeconds, Data = currentData };
            outcomes.Add(o);
            Console.WriteLine(status + " " + suite + "/" + name + " " + o.Detail + " [" + o.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s]");
        }

        static void Check(bool ok, string message) { if (!ok) throw new TestFailed(message); }
        static void Same(string expected, string actual, string what)
        {
            if (expected == actual) return;
            int i = 0;
            while (i < expected.Length && i < actual.Length && expected[i] == actual[i]) i++;
            throw new TestFailed(what + ": differs at " + i + ": expected \"" + Show(expected, i) + "\" got \"" + Show(actual, i) + "\"");
        }
        static string Show(string s, int at)
        {
            int a = Math.Max(0, at - 12);
            string part = s.Substring(a, Math.Min(s.Length - a, 40));
            return part.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
        static void Skip(string why) { throw new TestSkipped(why); }
        static void Note(string text) { currentDetail = text; }
        static void Add(string text) { currentDetail = currentDetail.Length == 0 ? text : currentDetail + "; " + text; }
        static void Put(string key, object value) { currentData[key] = value; }
        static string OneLine(string s) { return s.Replace("\r", " ").Replace("\n", " | "); }
        static string Ms(double ms) { return ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms"; }

        // A budget check that records the measure either way.
        static void Budget(string what, double ms, double limit)
        {
            Put(what + "_ms", Math.Round(ms, 1));
            Add(what + " " + Ms(ms) + " (max " + limit + ")");
            Check(ms <= limit, what + " took " + Ms(ms) + ", budget " + limit + " ms");
        }

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

        // Runs the dispatcher until it is idle (layout and render done).
        static void Idle()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
        }

        static void Pump(int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            Idle();
        }

        // Pumps until the condition holds (or the time runs out); returns whether it held.
        static bool Until(Func<bool> cond, int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (cond()) return true;
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
            Idle();
            return cond();
        }

        static void WriteMarkdown(string path, string stamp, List<string> suites, int passes, int fails, int skips)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Document tests ").Append(stamp).Append("\n\n");
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
