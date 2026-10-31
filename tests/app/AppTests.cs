// Headless tests of the app shell: safe exit order, orphaned recordings, background save, clipboard timing, clean
// uninstall, pinned image size. No window, no screen, no real clipboard; the registry is a throwaway key.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace Stackshot.Tests
{
    static class AppTests
    {
        sealed class TestFailed : Exception { public TestFailed(string message) : base(message) { } }

        static int passes, fails;
        static string work;
        static readonly string[] defaultSuites = { "quit", "orphans", "keep", "explain", "clipboard", "uninstall", "pin", "posters",
                                                       "hunt-settings", "hunt-updater", "hunt-recents", "hunt-ffmpeg", "hunt-tracked", "hunt-setup", "hunt-ui", "text", "xmlwatch",
                                                       "w6capture", "w6sensitive", "w6history", "w6cli", "w6search", "w6a11y", "w6toast", "w6contrast", "w6e2e", "w6home", "scroll", "backdrop", "news", "i18n" };

        [STAThread]
        static int Main(string[] args)
        {
            Settings.ReadOnly = true;
            ShotStack.LogPath = null;
            I18n.Apply("es");
            work = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            List<string> suites = args.Length > 0 ? new List<string>(args) : new List<string>(defaultSuites);
            foreach (string s in suites) Suite(s);
            try { Directory.Delete(work, true); } catch { }
            Console.WriteLine("TOTAL " + passes + " PASS, " + fails + " FAIL");
            return fails;
        }

        static void Suite(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "quit": QuitSuite(); break;
                case "orphans": OrphanSuite(); break;
                case "keep": KeepSuite(); break;
                case "explain": ExplainSuite(); break;
                case "clipboard": ClipboardSuite(); break;
                case "uninstall": UninstallSuite(); break;
                case "pin": PinSuite(); break;
                case "posters": PosterSuite(); break;
                case "hunt-settings": AppTestsHunt.SuiteSettings(); break;
                case "hunt-updater": AppTestsHunt.SuiteUpdater(); break;
                case "hunt-recents": AppTestsHunt.SuiteRecents(); break;
                case "hunt-ffmpeg": AppTestsHunt.SuiteFfmpeg(); break;
                case "hunt-tracked": AppTestsHunt.SuiteTracked(); break;
                case "hunt-setup": AppTestsHunt.SuiteSetup(); break;
                case "hunt-ui": AppTestsHuntUi.Suite(); break;
                case "hunt2-open": AppTestsHunt2.Suite(); break;
                case "text": AppTestsText.Suite(); break;
                case "h3text": AppTestsH3Text.Suite(); break;
                case "h3ux": AppTestsH3Ux.Suite(); break;
                case "h4app": AppTestsH4.Suite(); break;
                case "xmlwatch": AppTestsXml.Suite(); break;
                case "w6capture": AppTestsW6Capture.Suite(); break;
                case "w6sensitive": AppTestsW6Sensitive.Suite(); break;
                case "w6history": AppTestsW6History.Suite(); break;
                case "w6cli": AppTestsW6Cli.Suite(); break;
                case "w6search": AppTestsW6Search.Suite(); break;
                case "w6a11y": AppTestsW6A11y.Suite(); break;
                case "w6toast": AppTestsW6Toast.Suite(); break;
                case "w6contrast": AppTestsW6Contrast.Suite(); break;
                case "w6e2e": AppTestsW6E2e.Suite(); break;
                case "w6home": AppTestsW6Home.Suite(); break;
                case "w6perf": AppTestsW6Perf.Suite(); break;
                case "scroll": AppTestsScroll.Suite(); break;
                case "backdrop": AppTestsBackdrop.Suite(); break;
                case "news": AppTestsNews.Suite(); break;
                case "i18n": I18nSuite(); break;
                default: Test(name, "unknown suite", delegate { throw new TestFailed("no such suite"); }); break;
            }
        }

        internal static void Test(string suite, string name, Action body)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                body();
                passes++;
                Console.WriteLine("PASS " + suite + "/" + name + " (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (Exception ex)
            {
                fails++;
                Console.WriteLine("FAIL " + suite + "/" + name + ": " + (ex is TestFailed ? ex.Message : ex.ToString()));
            }
        }

        static void Check(bool ok, string what) { if (!ok) throw new TestFailed(what); }

        static void Eq<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new TestFailed(what + ": expected <" + expected + ">, got <" + actual + ">");
        }

        static string Dir(string name)
        {
            string d = Path.Combine(work, name);
            Directory.CreateDirectory(d);
            return d;
        }

        static void Touch(string path, int bytes, DateTime written)
        {
            File.WriteAllBytes(path, new byte[bytes]);
            File.SetLastWriteTime(path, written);
        }

        static void Box(List<byte> to, string type, int payload)
        {
            int size = payload < 0 ? 0 : 8 + payload; // negative: size 0, runs to the end
            to.Add((byte)(size >> 24)); to.Add((byte)(size >> 16)); to.Add((byte)(size >> 8)); to.Add((byte)size);
            foreach (char c in type) to.Add((byte)c);
            for (int i = 0; i < Math.Max(0, payload); i++) to.Add(0x11);
        }

        // A small MP4 as FFmpeg lays it out: ftyp, then moov before mdat (faststart) or after it, or no moov at all
        // with the mdat still open.
        static byte[] Mp4(bool ftyp, bool moovFirst, bool moovLast)
        {
            List<byte> b = new List<byte>();
            if (ftyp) Box(b, "ftyp", 24);
            if (moovFirst) Box(b, "moov", 40);
            Box(b, "free", 0);
            if (moovFirst || moovLast) Box(b, "mdat", 300);
            else { Box(b, "mdat", -1); for (int i = 0; i < 300; i++) b.Add(0x22); }
            if (moovLast) Box(b, "moov", 40);
            return b.ToArray();
        }

        // Runs FFmpeg hidden; killAfterMs >= 0 kills it then (a crash mid-recording). Exit code, or -1 when killed.
        static int Ffmpeg(string exe, string args, int killAfterMs)
        {
            ProcessStartInfo si = new ProcessStartInfo(exe, args);
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            using (Process p = Process.Start(si))
            {
                if (killAfterMs >= 0 && !p.WaitForExit(killAfterMs))
                {
                    try { p.Kill(); } catch { }
                    p.WaitForExit(10000);
                    return -1;
                }
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } return -2; }
                return p.ExitCode;
            }
        }

        static byte[] Mp4Large()
        {
            List<byte> b = new List<byte>();
            Box(b, "ftyp", 24);
            long size = 16 + 200;
            b.AddRange(new byte[] { 0, 0, 0, 1 });
            foreach (char c in "mdat") b.Add((byte)c);
            for (int i = 7; i >= 0; i--) b.Add((byte)(size >> (8 * i)));
            for (int i = 0; i < 200; i++) b.Add(0x33);
            Box(b, "moov", 40);
            return b.ToArray();
        }

        // ---- quit: the order of a safe exit

        static QuitSteps Steps(List<string> log, bool recording, bool askOk, bool editorsOk, bool docsOk)
        {
            QuitSteps q = new QuitSteps();
            q.Recording = delegate { log.Add("recording?"); return recording; };
            q.AskRecording = delegate { log.Add("ask"); return askOk; };
            q.ConfirmEditors = delegate { log.Add("editors"); return editorsOk; };
            q.ConfirmDocuments = delegate { log.Add("documents"); return docsOk; };
            q.SaveRecording = delegate { log.Add("save"); };
            q.WaitCopies = delegate { log.Add("copies"); };
            return q;
        }

        static string Calls(List<string> log) { return string.Join(",", log.FindAll(delegate(string s) { return s != "recording?"; }).ToArray()); }

        static void QuitSuite()
        {
            Test("quit", "questions first, then the recording is saved, then copies", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, true, true, true, true);
                Check(q.Run(), "should quit");
                Eq("ask,editors,documents,save,copies", Calls(log), "order");
            });
            Test("quit", "keeping the recording going cancels before anything is stopped", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, true, false, true, true);
                Check(!q.Run(), "should stay");
                Eq("ask", Calls(log), "calls");
                Eq("grabaci\u00F3n", q.CancelledBy, "cancelled by");
            });
            Test("quit", "Cancelar in an editor stops the quit and saves nothing", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, true, true, false, true);
                Check(!q.Run(), "should stay");
                Eq("ask,editors", Calls(log), "calls");
            });
            Test("quit", "Cancelar on a document stops the quit", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, false, true, true, false);
                Check(!q.Run(), "should stay");
                Eq("editors,documents", Calls(log), "calls (no recording question without a recording)");
            });
            Test("quit", "no recording: nothing to ask or save about it", delegate
            {
                List<string> log = new List<string>();
                Check(Steps(log, false, true, true, true).Run(), "should quit");
                Eq("editors,documents,copies", Calls(log), "calls");
            });
            Test("quit", "Windows ending the session: no questions, the recording is still saved", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, true, false, false, false);
                q.Interactive = false;
                Check(q.Run(), "must always quit");
                Eq("save,copies", Calls(log), "calls");
            });
            Test("quit", "a question that throws never traps the user", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, false, true, true, true);
                q.ConfirmEditors = delegate { throw new InvalidOperationException("broken"); };
                Check(q.Run(), "should quit");
            });
            Test("quit", "who is asked about a recording before it is stopped", delegate
            {
                Check(ShotStack.AsksAboutRecording(ShotStack.QuitKind.User, true), "the user quitting");
                Check(ShotStack.AsksAboutRecording(ShotStack.QuitKind.Session, true), "a sign-out the user may still cancel");
                Check(!ShotStack.AsksAboutRecording(ShotStack.QuitKind.Session, false), "sign-out while it is only saving");
                Check(!ShotStack.AsksAboutRecording(ShotStack.QuitKind.OtherCopy, true), "an update or the uninstaller");
                Check(!ShotStack.AsksAboutRecording(ShotStack.QuitKind.Windows, true), "the session ending now");
            });
            Test("quit", "a failing save still lets the copies finish", delegate
            {
                List<string> log = new List<string>();
                QuitSteps q = Steps(log, true, true, true, true);
                q.SaveRecording = delegate { log.Add("save"); throw new IOException("disk"); };
                Check(q.Run(), "should quit");
                Eq("ask,editors,documents,save,copies", Calls(log), "order");
            });
        }

        // ---- orphans: work files of a recording that never finished

        static void OrphanSuite()
        {
            Test("orphans", "names", delegate
            {
                Check(WorkFiles.IsWorkFile("~grabando 2026-10-09 14.00.00.nut"), "~grabando");
                Check(WorkFiles.IsWorkFile("~cine 2026-10-09 14.00.00.mkv"), "~cine");
                Check(!WorkFiles.IsWorkFile("V\u00EDdeo 2026-10-09 14.00.00.mp4"), "output is not a work file");
                Check(!WorkFiles.IsWorkFile("~grabandoX.nut"), "prefix needs the space");
                Eq("2026-10-09 14.00.00", WorkFiles.StampOf("~grabando 2026-10-09 14.00.00 equipo.f32"), "stamp");
                Eq<string>(null, WorkFiles.StampOf("~grabando sin fecha.nut"), "no stamp");
            });
            Test("orphans", "moved to recuperado by recording, nothing deleted, live and recent files stay", delegate
            {
                string temp = Dir("orph-temp"), rec = Path.Combine(work, "orph-rec");
                DateTime old = DateTime.Now.AddHours(-3);
                Touch(Path.Combine(temp, "~grabando 2026-10-09 14.00.00.nut"), 1000, old);
                Touch(Path.Combine(temp, "~grabando 2026-10-09 14.00.00 equipo.f32"), 300, old);
                Touch(Path.Combine(temp, "~cine 2026-10-09 14.00.00.mkv"), 700, old);
                Touch(Path.Combine(temp, "Cine 2026-10-09 14.00.00.mkv"), 50, old);
                Touch(Path.Combine(temp, "~grabando 2026-10-09 15.30.00.nut"), 10, old);
                Touch(Path.Combine(temp, "Captura 2026-10-09 14.00.00.png"), 10, old);       // a capture, not ours
                Touch(Path.Combine(temp, "V\u00EDdeo 2026-10-08 10.00.00.mp4"), 10, old);    // finished long ago
                Touch(Path.Combine(temp, "~grabando 2026-10-10 09.00.00.nut"), 10, DateTime.Now); // still being written
                string live = Path.Combine(temp, "~grabando 2026-10-09 16.00.00.nut");
                Touch(live, 10, old);
                List<WorkFiles.Rescued> got;
                using (new FileStream(live, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) // a live FFmpeg
                    got = WorkFiles.Rescue(temp, rec, TimeSpan.FromSeconds(10), true);
                Eq(2, got.Count, "groups");
                Eq("2026-10-09 14.00.00", got[0].Stamp, "first group");
                Eq(4, got[0].Files.Count, "files of the first recording (with its output)");
                Eq(2050L, got[0].Bytes, "bytes");
                Eq(new DateTime(2026, 10, 9, 14, 0, 0), got[0].Started, "start time");
                Check(File.Exists(Path.Combine(rec, @"2026-10-09 14.00.00\~cine 2026-10-09 14.00.00.mkv")), "master moved");
                Check(File.Exists(Path.Combine(rec, @"2026-10-09 15.30.00\~grabando 2026-10-09 15.30.00.nut")), "second recording moved");
                Check(File.Exists(Path.Combine(temp, "Captura 2026-10-09 14.00.00.png")), "capture untouched");
                Check(File.Exists(Path.Combine(temp, "V\u00EDdeo 2026-10-08 10.00.00.mp4")), "unrelated video untouched");
                Check(File.Exists(Path.Combine(temp, "~grabando 2026-10-10 09.00.00.nut")), "recent work file stays");
                Check(File.Exists(live), "file in use stays");
                int total = Directory.GetFiles(temp).Length + Directory.GetFiles(rec, "*", SearchOption.AllDirectories).Length;
                Eq(9, total, "files conserved (nothing deleted)");
                Eq(2060L, WorkFiles.Bytes(rec), "recovered size");
            });
            Test("orphans", "during a session the outputs stay (a card may show them)", delegate
            {
                string temp = Dir("orph-temp2"), rec = Path.Combine(work, "orph-rec2");
                DateTime old = DateTime.Now.AddHours(-3);
                Touch(Path.Combine(temp, "~grabando 2026-10-09 14.00.00.nut"), 10, old);
                Touch(Path.Combine(temp, "V\u00EDdeo 2026-10-09 14.00.00.mp4"), 10, old);
                List<WorkFiles.Rescued> got = WorkFiles.Rescue(temp, rec, TimeSpan.FromSeconds(10), false);
                Eq(1, got.Count, "groups");
                Eq(1, got[0].Files.Count, "only the work file");
                Check(File.Exists(Path.Combine(temp, "V\u00EDdeo 2026-10-09 14.00.00.mp4")), "output stays");
            });
            Test("orphans", "a name already in recuperado is never overwritten", delegate
            {
                string temp = Dir("orph-temp3"), rec = Dir("orph-rec3");
                Directory.CreateDirectory(Path.Combine(rec, "2026-10-09 14.00.00"));
                File.WriteAllText(Path.Combine(rec, @"2026-10-09 14.00.00\~grabando 2026-10-09 14.00.00.nut"), "older");
                Touch(Path.Combine(temp, "~grabando 2026-10-09 14.00.00.nut"), 10, DateTime.Now.AddHours(-1));
                WorkFiles.Rescue(temp, rec, TimeSpan.FromSeconds(10), true);
                Eq("older", File.ReadAllText(Path.Combine(rec, @"2026-10-09 14.00.00\~grabando 2026-10-09 14.00.00.nut")), "older copy kept");
                Check(File.Exists(Path.Combine(rec, @"2026-10-09 14.00.00\~grabando 2026-10-09 14.00.00 (2).nut")), "new one beside it");
            });
            Test("orphans", "an MP4 without its index is an interrupted recording", delegate
            {
                string d = Dir("orph-mp4");
                string open = Path.Combine(d, "open.mp4"), done = Path.Combine(d, "done.mp4"), late = Path.Combine(d, "late.mp4");
                string big = Path.Combine(d, "big.mp4"), junk = Path.Combine(d, "junk.mp4"), empty = Path.Combine(d, "empty.mp4");
                File.WriteAllBytes(open, Mp4(true, false, false));
                File.WriteAllBytes(done, Mp4(true, true, false));
                File.WriteAllBytes(late, Mp4(true, false, true));
                File.WriteAllBytes(big, Mp4Large());
                File.WriteAllBytes(junk, new byte[100]);
                File.WriteAllBytes(empty, new byte[0]);
                Check(WorkFiles.UnfinishedMp4(open), "mdat that runs to the end, no moov");
                Check(!WorkFiles.UnfinishedMp4(done), "faststart: moov before mdat");
                Check(!WorkFiles.UnfinishedMp4(late), "moov after mdat");
                Check(!WorkFiles.UnfinishedMp4(big), "64-bit mdat size, then moov");
                Check(!WorkFiles.UnfinishedMp4(junk), "not an MP4");
                Check(!WorkFiles.UnfinishedMp4(empty), "empty file");
                Check(!WorkFiles.UnfinishedMp4(Path.Combine(d, "nope.mp4")), "missing file");
            });
            Test("orphans", "real FFmpeg MP4s: finished ones are left, a killed one is unfinished", delegate
            {
                string ff = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", @"Stackshot\ffmpeg\ffmpeg.exe");
                if (!File.Exists(ff)) { Console.WriteLine("  (sin FFmpeg: se omite)"); return; }
                string d = Dir("orph-ff");
                string fast = Path.Combine(d, "fast.mp4"), plain = Path.Combine(d, "plain.mp4"), killed = Path.Combine(d, "killed.mp4");
                const string src = "-hide_banner -loglevel error -y -f lavfi -i testsrc=size=320x240:rate=30 -c:v libx264 -preset ultrafast -pix_fmt yuv420p ";
                Eq(0, Ffmpeg(ff, src + "-t 1 -movflags +faststart \"" + fast + "\"", -1), "faststart encode");
                Eq(0, Ffmpeg(ff, src + "-t 1 \"" + plain + "\"", -1), "plain encode");
                Ffmpeg(ff, "-re " + src + "-t 60 -movflags +faststart \"" + killed + "\"", 2500);
                Check(!WorkFiles.UnfinishedMp4(fast), "faststart file is finished");
                Check(!WorkFiles.UnfinishedMp4(plain), "moov at the end is finished");
                Check(File.Exists(killed) && new FileInfo(killed).Length > 0, "killed file has data");
                Check(WorkFiles.UnfinishedMp4(killed), "killed recording is unfinished");
            });
            Test("orphans", "a sound-less recording written straight to its MP4 is rescued at startup", delegate
            {
                string temp = Dir("orph-temp4"), rec = Path.Combine(work, "orph-rec4");
                DateTime old = DateTime.Now.AddHours(-3);
                string cut = Path.Combine(temp, "V\u00EDdeo 2026-10-09 14.00.00.mp4");
                string fine = Path.Combine(temp, "V\u00EDdeo 2026-10-09 15.00.00.mp4");
                string fresh = Path.Combine(temp, "V\u00EDdeo 2026-10-09 16.00.00.mp4");
                string gif = Path.Combine(temp, "GIF 2026-10-09 17.00.00.gif");
                File.WriteAllBytes(cut, Mp4(true, false, false)); File.SetLastWriteTime(cut, old);
                File.WriteAllBytes(fine, Mp4(true, true, false)); File.SetLastWriteTime(fine, old);
                File.WriteAllBytes(fresh, Mp4(true, false, false));
                Touch(gif, 10, old);
                List<WorkFiles.Rescued> during = WorkFiles.Rescue(temp, rec, TimeSpan.FromSeconds(10), false);
                Eq(0, during.Count, "never during a session");
                List<WorkFiles.Rescued> got = WorkFiles.Rescue(temp, rec, TimeSpan.FromSeconds(10), true);
                Eq(1, got.Count, "groups");
                Eq("2026-10-09 14.00.00", got[0].Stamp, "the interrupted one");
                Check(File.Exists(Path.Combine(rec, "2026-10-09 14.00.00", "V\u00EDdeo 2026-10-09 14.00.00.mp4")), "moved to recuperado");
                Check(File.Exists(fine), "a finished video stays");
                Check(File.Exists(fresh), "a recent one stays");
                Check(File.Exists(gif), "a GIF stays");
            });
            Test("orphans", "a missing temp folder is fine", delegate
            {
                Eq(0, WorkFiles.Rescue(Path.Combine(work, "nope"), Path.Combine(work, "nope-rec"), TimeSpan.Zero, true).Count, "nothing");
                Eq(0L, WorkFiles.Bytes(Path.Combine(work, "nope-rec")), "no folder, no bytes");
            });
        }

        // ---- keep: the copy Guardar makes in the background

        static void KeepSuite()
        {
            Test("keep", "copy with progress, through .part, identical bytes, off the calling thread", delegate
            {
                string d = Dir("keep");
                string src = Path.Combine(d, "big.mp4"), dest = Path.Combine(d, "saved", "big.mp4");
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                byte[] data = new byte[24 << 20];
                new Random(7).NextBytes(data);
                File.WriteAllBytes(src, data);
                List<double> seen = new List<double>();
                Exception error = null;
                int caller = Thread.CurrentThread.ManagedThreadId, worker = -1;
                ManualResetEvent done = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    worker = Thread.CurrentThread.ManagedThreadId;
                    try { ShotStack.CopyWithProgress(src, dest, delegate(double f) { lock (seen) seen.Add(f); }); }
                    catch (Exception ex) { error = ex; }
                    done.Set();
                });
                Check(done.WaitOne(60000), "copy finished");
                if (error != null) throw error;
                Check(worker != caller, "ran on another thread");
                Check(seen.Count > 0 && seen[seen.Count - 1] == 1.0, "progress reaches 100%");
                for (int i = 1; i < seen.Count; i++) Check(seen[i] >= seen[i - 1], "progress never goes back");
                Check(!File.Exists(dest + ".part"), "no .part left");
                Check(Same(src, dest), "same bytes");
            });
            Test("keep", "saving again replaces the older copy", delegate
            {
                string d = Dir("keep2");
                string src = Path.Combine(d, "a.png"), dest = Path.Combine(d, "b.png");
                File.WriteAllText(src, "new");
                File.WriteAllText(dest, "old and longer");
                ShotStack.CopyWithProgress(src, dest, null);
                Eq("new", File.ReadAllText(dest), "replaced");
            });
            Test("keep", "a failed copy leaves nothing half written", delegate
            {
                string d = Dir("keep3");
                string src = Path.Combine(d, "a.png");
                File.WriteAllText(src, "x");
                string dest = Path.Combine(d, "b.png");
                File.WriteAllText(dest, "keep me");
                bool threw = false;
                using (new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.None)) // held by another program
                {
                    try { ShotStack.CopyWithProgress(src, dest, null); }
                    catch (IOException) { threw = true; }
                }
                Check(threw, "reports the failure");
                Check(!File.Exists(dest + ".part"), "no .part left");
                Eq("keep me", File.ReadAllText(dest), "older copy intact");
            });
            Test("keep", "same folder however it is written", delegate
            {
                string d = Dir("keep4");
                Check(ShotStack.SameFolder(d, d + "\\"), "trailing slash");
                Check(ShotStack.SameFolder(d, Path.Combine(d, "sub", "..")), "relative part");
                Check(!ShotStack.SameFolder(d, Path.Combine(d, "sub")), "subfolder is another folder");
                Check(!ShotStack.SameFolder(null, d), "null");
            });
            Test("keep", "a save during a running copy makes that copy repeat instead of racing it", delegate
            {
                CopyRuns runs = new CopyRuns();
                string p = @"C:\t\Captura.png";
                Check(!runs.Repeat(p), "nothing running: the caller copies");
                runs.Begin(p);
                Check(runs.Repeat(@"C:\t\Otra.png") == false, "another path is not affected");
                Check(runs.Repeat(p.ToUpperInvariant()), "running: it repeats (any case)");
                Check(runs.Again(p), "the copy goes once more");
                Check(runs.Repeat(p), "a second change during the repeat");
                Check(runs.Again(p), "and once more");
                Check(!runs.Again(p), "then it is done");
                Check(!runs.Repeat(p), "done: the caller copies again");
            });
            Test("keep", "a failed copy forgets the pending repeat", delegate
            {
                CopyRuns runs = new CopyRuns();
                string p = @"C:\t\Captura.png";
                runs.Begin(p);
                Check(runs.Repeat(p), "repeat asked");
                runs.End(p);
                Check(!runs.Repeat(p), "nothing running any more");
                runs.Begin(p);
                Check(!runs.Again(p), "a new copy starts clean");
            });
            Test("keep", "repeats race-free between threads: the last change is always copied", delegate
            {
                // A copy thread doing passes while another thread keeps asking for repeats: after both stop, no
                // request made before the copy ended is lost.
                CopyRuns runs = new CopyRuns();
                string p = @"C:\t\Carrera.png";
                int version = 0, copied = -1;
                for (int round = 0; round < 200; round++)
                {
                    runs.Begin(p);
                    ManualResetEvent finished = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        do { Thread.VolatileWrite(ref copied, Thread.VolatileRead(ref version)); } while (runs.Again(p));
                        finished.Set();
                    });
                    for (int i = 0; i < 5; i++)
                    {
                        Interlocked.Increment(ref version);
                        // When the copy already ended, the caller copies by itself.
                        if (!runs.Repeat(p)) Thread.VolatileWrite(ref copied, Thread.VolatileRead(ref version));
                    }
                    Check(finished.WaitOne(10000), "copy thread ended");
                    Eq(Thread.VolatileRead(ref version), Thread.VolatileRead(ref copied), "last version copied (round " + round + ")");
                }
            });
            Test("keep", "a capture that can't be written stays in memory only until dropped", delegate
            {
                string path = Path.Combine(work, "no-such-folder", "Captura.png");
                ManualResetEvent failed = new ManualResetEvent(false);
                ShotStack.BeginWrite(path, new Bitmap(64, 32), delegate(Exception ex) { failed.Set(); });
                Check(failed.WaitOne(10000), "failure reported");
                using (Bitmap kept = ShotStack.Unsaved(path)) Check(kept != null && kept.Width == 64, "image kept for its card");
                ShotStack.DropUnsaved(path);
                Check(ShotStack.Unsaved(path) == null, "dropped once nothing can use it");
            });
            Test("keep", "Cine masters are recognised", delegate
            {
                Check(ShotStack.IsCine(@"C:\x\Cine 2026-10-09 14.00.00.mkv"), "mkv");
                Check(ShotStack.IsCine(@"C:\x\CINE.MKV"), "case");
                Check(!ShotStack.IsCine(@"C:\x\V\u00EDdeo.mp4"), "mp4");
                Check(!ShotStack.IsCine(null), "null");
            });
        }

        static bool Same(string a, string b)
        {
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        // ---- explain: plain Spanish instead of raw exception text

        static void ExplainSuite()
        {
            Test("explain", "common causes", delegate
            {
                string p = @"D:\Fotos\a.png";
                Check(ShotStack.Explain(new IOException("x", unchecked((int)0x80070070)), p).StartsWith("No queda espacio"), "disk full");
                Check(ShotStack.Explain(new IOException("x", unchecked((int)0x80070020)), p).StartsWith("Otro programa"), "in use");
                Check(ShotStack.Explain(new UnauthorizedAccessException("x"), p).StartsWith("Windows no deja escribir"), "denied");
                Check(ShotStack.Explain(new DirectoryNotFoundException("x"), p).StartsWith("No se encuentra la carpeta"), "missing folder");
                Check(ShotStack.Explain(new System.ComponentModel.Win32Exception(1260), p).Contains("bloqueado"), "blocked by policy");
                Check(ShotStack.Explain(new System.ComponentModel.Win32Exception(1155), p).Contains("ninguna aplicaci\u00F3n"), "no app");
                Check(ShotStack.Explain(new PathTooLongException("x"), p).Contains("demasiado larga"), "long path");
                Check(ShotStack.Explain(new IOException("x", unchecked((int)0x80070070)), p).Contains("\u00ABD:\\Fotos\u00BB"), "names the folder");
            });
            Test("explain", "opening or reading is not about writing to the folder", delegate
            {
                string p = @"D:\Fotos\a.png";
                string denied = ShotStack.ExplainOpen(new UnauthorizedAccessException("x"), p);
                Check(denied.StartsWith("Windows no deja abrir") && !denied.Contains("escribir") && !denied.Contains("Acceso controlado"), "denied: " + denied);
                string start = ShotStack.ExplainOpen(new System.ComponentModel.Win32Exception(5), p);
                Check(start.StartsWith("Windows no deja abrir"), "Process.Start denied: " + start);
                Check(ShotStack.ExplainOpen(new System.ComponentModel.Win32Exception(2), p).StartsWith("El archivo ya no existe"), "missing file");
                Check(ShotStack.ExplainOpen(new System.ComponentModel.Win32Exception(1155), p).Contains("ninguna aplicaci\u00F3n"), "no app");
                string generic = ShotStack.ExplainOpen(new System.Runtime.InteropServices.ExternalException("x"), p);
                Check(!generic.Contains("escribir"), "generic: " + generic);
                Check(ShotStack.Explain(new UnauthorizedAccessException("x"), p).Contains("Acceso controlado"), "writes keep the folder advice");
            });
            Test("explain", "never the raw message", delegate
            {
                string raw = "Error gen\u00E9rico en GDI+ 0xDEAD";
                string got = ShotStack.Explain(new System.Runtime.InteropServices.ExternalException(raw), @"C:\a.png");
                Check(!got.Contains("0xDEAD") && got.Length > 0, "generic text: " + got);
                Check(!string.IsNullOrEmpty(ScrollSession.FailureText(true)) && ScrollSession.FailureText(true).Contains("memoria"), "scroll: too big");
                Check(!string.IsNullOrEmpty(ScrollSession.FailureText(false)), "scroll: other");
            });
        }

        // ---- clipboard: what counts as a paste, and copies kept off the cloud clipboard

        static void ClipboardSuite()
        {
            Test("clipboard", "timing rule", delegate
            {
                Check(!TrackedData.CountsAsPaste("PNG", 200), "early read is a clipboard tool");
                Check(TrackedData.CountsAsPaste("PNG", 1500), "late read is a paste");
                Check(!TrackedData.CountsAsPaste("CanIncludeInClipboardHistory", 5000), "Windows asking about history");
                Check(!TrackedData.CountsAsPaste(null, 5000), "no format");
            });
            Test("clipboard", "a clipboard manager waiting for a slow PNG is not a paste", delegate
            {
                int used = 0;
                TrackedData.TestPngDelayMs = 1500;
                Bitmap img = new Bitmap(64, 64);
                TrackedData td = null;
                try
                {
                    td = new TrackedData(Path.Combine(work, "none.png"), img, true, false, delegate { Interlocked.Increment(ref used); });
                    Stopwatch sw = Stopwatch.StartNew();
                    object png = td.GetData("PNG", false);   // asked at once, answered after the slow PNG
                    Check(png != null, "PNG delivered");
                    Check(sw.ElapsedMilliseconds >= 1000, "it really waited (" + sw.ElapsedMilliseconds + " ms)");
                    Eq(0, used, "not counted as pasted");
                    td.GetData("CanUploadToCloudClipboard", false);
                    Eq(0, used, "flags never count");
                    td.GetData("PNG", false);                 // a real paste, well after the copy
                    Eq(1, used, "counted once pasted");
                    td.GetData("PNG", false);
                    Eq(1, used, "only once");
                }
                finally
                {
                    TrackedData.TestPngDelayMs = 0;
                    if (td != null) td.Release(); else img.Dispose();
                }
            });
            Test("clipboard", "copies carry the no-cloud flag", delegate
            {
                System.Windows.Forms.DataObject d = new System.Windows.Forms.DataObject();
                ShotStack.KeepOffCloud(d);
                MemoryStream ms = d.GetData("CanUploadToCloudClipboard") as MemoryStream;
                Check(ms != null, "flag present");
                Eq(0, BitConverter.ToInt32(ms.ToArray(), 0), "value 0");
                Bitmap img = new Bitmap(8, 8);
                TrackedData td = new TrackedData(Path.Combine(work, "none.png"), img, false, false, null);
                try { Check(td.GetData("CanUploadToCloudClipboard", false) != null, "tracked copies too"); }
                finally { td.Release(); }
            });
        }

        // ---- uninstall: Windows put back as it was, recovered recordings kept

        static void UninstallSuite()
        {
            string rootName = @"Software\StackshotTests\app-" + Guid.NewGuid().ToString("N");
            RegistryKey root = Registry.CurrentUser.CreateSubKey(rootName);
            FileAssoc.HkcuOverride = root;
            Installer.HkcuOverride = root;
            Settings.ReadOnly = false;
            try
            {
                Test("uninstall", "associations removed, Print Screen given back (value was absent)", delegate
                {
                    Check(FileAssoc.Enable(FileAssoc.Markdown) && FileAssoc.Enable(FileAssoc.Xml) && FileAssoc.Enable(FileAssoc.Video), "enabled");
                    Check(Key(root, @"Software\Classes\Stackshot.Markdown\shell\open\command"), "markdown registered");
                    Check(Key(root, @"Software\Classes\Stackshot.Video\shell\open\command"), "mkv registered");
                    Installer.FreePrintScreen();
                    Eq<object>(0, Value(root, @"Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled"), "freed");
                    Eq<object>("none", Value(root, @"Software\Stackshot", "FreedPrintScreen"), "remembered it was absent");
                    Check(Installer.CleanShell(), "print screen given back");
                    Check(!Key(root, @"Software\Classes\Stackshot.Markdown"), "markdown progid gone");
                    Check(!Key(root, @"Software\Classes\Stackshot.Xml"), "xml progid gone");
                    Check(!Key(root, @"Software\Classes\Stackshot.Video"), "mkv progid gone");
                    Check(!Key(root, @"Software\Classes\.md\OpenWithProgids"), "open-with entry gone");
                    Check(!Key(root, @"Software\Classes\.mkv"), "mkv open-with entry gone");
                    Check(!Key(root, @"Software\Stackshot"), "Software\\Stackshot gone (empty)");
                    Eq<object>(null, Value(root, @"Software\RegisteredApplications", "Stackshot"), "registered application gone");
                    Eq<object>(null, Value(root, @"Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled"), "value absent again");
                });
                Test("uninstall", "Print Screen back to 1 when it was 1", delegate
                {
                    using (RegistryKey kb = root.CreateSubKey(@"Control Panel\Keyboard")) kb.SetValue("PrintScreenKeyForSnippingEnabled", 1, RegistryValueKind.DWord);
                    Installer.FreePrintScreen();
                    Installer.FreePrintScreen(); // twice: still remembers the original
                    Check(Installer.CleanShell(), "given back");
                    Eq<object>(1, Value(root, @"Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled"), "back to 1");
                });
                Test("uninstall", "never freed by Stackshot: left alone", delegate
                {
                    using (RegistryKey kb = root.CreateSubKey(@"Control Panel\Keyboard")) kb.SetValue("PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord);
                    Check(!Installer.CleanShell(), "nothing given back");
                    Eq<object>(0, Value(root, @"Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled"), "the user's 0 stays");
                });
                Test("uninstall", "changed back by the user meanwhile: theirs", delegate
                {
                    using (RegistryKey kb = root.CreateSubKey(@"Control Panel\Keyboard")) kb.SetValue("PrintScreenKeyForSnippingEnabled", 1, RegistryValueKind.DWord);
                    Installer.FreePrintScreen();
                    using (RegistryKey kb = root.CreateSubKey(@"Control Panel\Keyboard")) kb.SetValue("PrintScreenKeyForSnippingEnabled", 1, RegistryValueKind.DWord);
                    Check(!Installer.CleanShell(), "nothing to give back");
                    Eq<object>(null, Value(root, @"Software\Stackshot", "FreedPrintScreen"), "flag cleared");
                });
                Test("uninstall", "an association of another app is untouched", delegate
                {
                    using (RegistryKey w = root.CreateSubKey(@"Software\Classes\.md\OpenWithProgids")) w.SetValue("Other.Editor", "", RegistryValueKind.String);
                    FileAssoc.Enable(FileAssoc.Markdown);
                    Installer.CleanShell();
                    Eq<object>("", Value(root, @"Software\Classes\.md\OpenWithProgids", "Other.Editor"), "other app kept");
                });
                Test("uninstall", "mkv association: offered for .mkv, the player opens it, removed on its own", delegate
                {
                    using (RegistryKey w = root.CreateSubKey(@"Software\Classes\.mkv\OpenWithProgids")) w.SetValue("VLC.mkv", "", RegistryValueKind.String);
                    using (RegistryKey d = root.CreateSubKey(@"Software\Classes\.mkv")) d.SetValue("", "VLC.mkv");
                    Check(FileAssoc.Enable(FileAssoc.Video) && FileAssoc.Enable(FileAssoc.Markdown), "enabled");
                    Check(FileAssoc.Registered(FileAssoc.Video), "registered");
                    Eq<object>(FileAssoc.Command, Value(root, @"Software\Classes\Stackshot.Video\shell\open\command", ""), "open command: this exe with the file");
                    Eq<object>("V\u00EDdeo MKV", Value(root, @"Software\Classes\Stackshot.Video", ""), "file type name");
                    Eq<object>("", Value(root, @"Software\Classes\.mkv\OpenWithProgids", "Stackshot.Video"), "offered in Open with");
                    Eq<object>("Stackshot.Video", Value(root, @"Software\Stackshot\Capabilities\FileAssociations", ".mkv"), "in Windows' default apps");
                    Eq<object>("VLC.mkv", Value(root, @"Software\Classes\.mkv", ""), "the extension's default is never changed");
                    Check(!FileAssoc.IsDefault(FileAssoc.Video), "not the default until the user picks it");
                    // Off for .mkv only: the other association and the app's registration stay.
                    FileAssoc.Disable(FileAssoc.Video);
                    Check(!FileAssoc.Registered(FileAssoc.Video) && !Key(root, @"Software\Classes\Stackshot.Video"), "mkv progid gone");
                    Eq<object>(null, Value(root, @"Software\Classes\.mkv\OpenWithProgids", "Stackshot.Video"), "open-with entry gone");
                    Eq<object>("", Value(root, @"Software\Classes\.mkv\OpenWithProgids", "VLC.mkv"), "other app kept");
                    Eq<object>(null, Value(root, @"Software\Stackshot\Capabilities\FileAssociations", ".mkv"), "capability gone");
                    Check(FileAssoc.Registered(FileAssoc.Markdown) && Value(root, @"Software\RegisteredApplications", "Stackshot") != null, "markdown and the app registration stay");
                    Installer.CleanShell();
                    Eq<object>(null, Value(root, @"Software\RegisteredApplications", "Stackshot"), "the last one out takes the registration");
                    Eq<object>("VLC.mkv", Value(root, @"Software\Classes\.mkv", ""), "the extension's default still untouched");
                });
                Test("uninstall", "files that the running Stackshot opens", delegate
                {
                    foreach (string p in new string[] { @"C:\v\Cine 2026-10-11 17.03.26.mkv", @"C:\v\A.MKV", @"C:\v\b.mp4", @"C:\v\c.webm", @"C:\v\n.md", @"C:\v\x.xml" })
                        Check(Installer.OpensHere(p), p + " should open in the running Stackshot");
                    foreach (string p in new string[] { @"C:\v\a.gif", @"C:\v\a.png", @"C:\v\a.txt", @"C:\v\noext", "" })
                        Check(!Installer.OpensHere(p), p + " should not");
                    // What a file given alone to the exe does: a video plays, an image or a GIF is edited.
                    Check(ShotStack.IsEditable(@"C:\v\a.mkv") && Installer.OpensHere(@"C:\v\a.mkv"), "an .mkv goes to the player, not the editor");
                    Check(ShotStack.IsEditable(@"C:\v\a.gif") && !Installer.OpensHere(@"C:\v\a.gif"), "a GIF still goes to the editor");
                    Check(ShotStack.IsEditable(@"C:\v\a.png") && !Installer.OpensHere(@"C:\v\a.png"), "an image still goes to the editor");
                    // A request a second copy writes (SendOpen's line, without signalling the running Stackshot, which may be
                    // the user's) is taken: only with a private data folder (STACKSHOT_TEST_DATA).
                    string real = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Stackshot");
                    if (!string.Equals(Path.GetFullPath(Settings.DataDir).TrimEnd('\\'), real.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        string mkv = Path.Combine(work, "Cine pedido.mkv"), md = Path.Combine(work, "nota.md"), png = Path.Combine(work, "foto.png");
                        Installer.TakeOpenRequests();
                        Directory.CreateDirectory(Settings.DataDir);
                        string lines = "";
                        foreach (string p in new string[] { mkv, md, png }) lines += DateTime.UtcNow.Ticks + "\t" + p + "\n";
                        File.AppendAllText(Path.Combine(Settings.DataDir, "open.txt"), lines, new System.Text.UTF8Encoding(false));
                        List<string> got = Installer.TakeOpenRequests();
                        Check(got.Count == 2 && got.Contains(mkv) && got.Contains(md), "taken: " + string.Join(", ", got.ToArray()));
                    }
                    else Console.WriteLine("  (open requests not checked: STACKSHOT_TEST_DATA is not set)");
                });
            }
            finally
            {
                Settings.ReadOnly = true;
                FileAssoc.HkcuOverride = null;
                Installer.HkcuOverride = null;
                root.Close();
                try { Registry.CurrentUser.DeleteSubKeyTree(rootName, false); } catch { }
                try
                {
                    bool empty;
                    using (RegistryKey t = Registry.CurrentUser.OpenSubKey(@"Software\StackshotTests")) empty = t != null && t.SubKeyCount == 0 && t.ValueCount == 0;
                    if (empty) Registry.CurrentUser.DeleteSubKey(@"Software\StackshotTests", false);
                }
                catch { }
            }
            Test("uninstall", "data folder removed except recovered recordings", delegate
            {
                string data = Dir("data1");
                File.WriteAllText(Path.Combine(data, "settings.ini"), "x");
                Directory.CreateDirectory(Path.Combine(data, "temp"));
                File.WriteAllText(Path.Combine(data, @"temp\a.png"), "x");
                Directory.CreateDirectory(Path.Combine(data, "ffmpeg"));
                Directory.CreateDirectory(Path.Combine(data, @"recuperado\2026-10-09 14.00.00"));
                File.WriteAllText(Path.Combine(data, @"recuperado\2026-10-09 14.00.00\~grabando.nut"), "video");
                Check(Installer.DeleteData(data, Path.Combine(work, "Pictures")), "recovered kept");
                Check(File.Exists(Path.Combine(data, @"recuperado\2026-10-09 14.00.00\~grabando.nut")), "recording still there");
                Eq(0, Directory.GetFiles(data).Length, "loose files gone");
                Eq(1, Directory.GetDirectories(data).Length, "only recuperado left");
            });
            Test("uninstall", "nothing recovered: the whole folder goes", delegate
            {
                string data = Dir("data2");
                File.WriteAllText(Path.Combine(data, "settings.ini"), "x");
                Directory.CreateDirectory(Path.Combine(data, "recuperado"));
                Check(!Installer.DeleteData(data, Path.Combine(work, "Pictures")), "nothing kept");
                Check(!Directory.Exists(data), "folder gone");
            });
            Test("uninstall", "a save folder inside the data folder is never deleted", delegate
            {
                string data = Dir("data3");
                string saved = Path.Combine(data, "Capturas");
                Directory.CreateDirectory(saved);
                File.WriteAllText(Path.Combine(saved, "mine.png"), "x");
                File.WriteAllText(Path.Combine(data, "settings.ini"), "x");
                Installer.DeleteData(data, saved);
                Check(File.Exists(Path.Combine(saved, "mine.png")), "saved capture kept");
                Check(!File.Exists(Path.Combine(data, "settings.ini")), "settings gone");
            });
        }

        static bool Key(RegistryKey root, string path)
        {
            using (RegistryKey k = root.OpenSubKey(path)) return k != null;
        }

        static object Value(RegistryKey root, string path, string name)
        {
            using (RegistryKey k = root.OpenSubKey(path)) return k == null ? null : k.GetValue(name);
        }

        // ---- pin: proportions kept for very tall or wide captures

        static void PinSuite()
        {
            Size wa = new Size(1920, 1040);
            Test("pin", "a very tall capture keeps its aspect ratio", delegate
            {
                float min;
                Size img = new Size(800, 30000);
                Size s = PinWindow.ScaledSize(img, PinWindow.FitZoom(img, wa, out min));
                Ratio(img, s);
                Size smallest = PinWindow.ScaledSize(img, min);
                Ratio(img, smallest);
            });
            Test("pin", "a very wide strip keeps its aspect ratio", delegate
            {
                float min;
                Size img = new Size(40000, 300);
                Size s = PinWindow.ScaledSize(img, PinWindow.FitZoom(img, wa, out min));
                Ratio(img, s);
            });
            Test("pin", "tall or wide captures fit the work area, and so does the smallest zoom", delegate
            {
                foreach (Size img in new Size[] { new Size(800, 30000), new Size(600, 25000), new Size(800, 100000), new Size(40000, 300), new Size(5000, 20) })
                {
                    float min;
                    float z = PinWindow.FitZoom(img, wa, out min);
                    Size s = PinWindow.ScaledSize(img, z);
                    Check(s.Width <= wa.Width && s.Height <= wa.Height, "fits " + img + " -> " + s);
                    Check(min <= z, "can zoom out from the start " + img);
                    Size smallest = PinWindow.ScaledSize(img, min);
                    Check(smallest.Width <= wa.Width && smallest.Height <= wa.Height, "smallest fits " + img + " -> " + smallest);
                }
            });
            Test("pin", "a tall capture that fits stays at least the smallest usable window", delegate
            {
                float min;
                Size img = new Size(50, 1200);
                Size s = PinWindow.ScaledSize(img, PinWindow.FitZoom(img, wa, out min));
                Check(s.Width >= 40, "usable width " + s);
                Check(s.Height <= wa.Height, "fits " + s);
            });
            Test("pin", "normal captures: 70% of the screen at most, never upscaled", delegate
            {
                float min;
                Eq(1f, PinWindow.FitZoom(new Size(1000, 500), wa, out min), "small fits at 100%");
                Size s = PinWindow.ScaledSize(new Size(3840, 2160), PinWindow.FitZoom(new Size(3840, 2160), wa, out min));
                Check(s.Width <= 1920 * 0.7 + 1 && s.Height <= 1040 * 0.7 + 1, "fits " + s);
            });
            Test("pin", "a tiny capture grows evenly to the smallest window", delegate
            {
                float min;
                Size s = PinWindow.ScaledSize(new Size(10, 10), PinWindow.FitZoom(new Size(10, 10), wa, out min));
                Eq(s.Width, s.Height, "square stays square");
                Check(s.Width >= 40, "big enough " + s);
            });
        }

        static void Ratio(Size img, Size s)
        {
            double a = img.Width / (double)img.Height, b = s.Width / (double)s.Height;
            // Rounding to whole pixels: within one pixel on the short side.
            double tol = Math.Max(1.0 / Math.Min(s.Width, s.Height), 0.001);
            Check(Math.Abs(a / b - 1) <= tol + 1e-9, "aspect " + img + " -> " + s);
        }

        // ---- posters: a Cine master's picture follows its saved copy and survives a restart

        static void PosterSuite()
        {
            Test("posters", "kept on disk by file name", delegate
            {
                string before = ShotStack.PosterDir;
                ShotStack.PosterDir = Dir("posters");
                try
                {
                    using (Bitmap b = new Bitmap(32, 18))
                    {
                        using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(255, 10, 200, 30));
                        ShotStack.SetPoster(@"C:\temp\Cine 2026-10-09 14.00.00.mkv", b);
                    }
                    Check(File.Exists(Path.Combine(ShotStack.PosterDir, "Cine 2026-10-09 14.00.00.mkv.png")), "written");
                    using (Bitmap again = ShotStack.Poster(@"D:\Saved\Cine 2026-10-09 14.00.00.mkv"))
                    {
                        Check(again != null, "found for the saved copy");
                        Eq(32, again.Width, "same picture");
                        Eq(200, (int)again.GetPixel(5, 5).G, "same pixels");
                    }
                    Check(ShotStack.Poster(@"D:\Saved\Otro.mkv") == null, "none for another file");
                    Check(ShotStack.Poster(@"D:\Saved\V\u00EDdeo.mp4") == null, "only Cine masters");
                }
                finally { ShotStack.PosterDir = before; }
            });
        }

        static void I18nSuite()
        {
            Test("i18n", "switching languages and dynamic updates", delegate
            {
                try
                {
                    I18n.Apply("en");
                    Check(!I18n.IsSpanish, "en mode is not spanish");
                    Check(I18n.T("Captura", "Capture") == "Capture", "en text");
                    Check(I18n.Copy == "Copy", "en copy");
                    Check(I18n.Cancel == "Cancel", "en cancel");
                    Check(I18n.Fmt("{0} archivos", "{0} files", 5) == "5 files", "en fmt");
                    Check(Hotkeys.KeyName(System.Windows.Forms.Keys.Space) == "Space", "en space key");
                    Check(WellKind.Names[0] == "Drink water", "en wellkind");
                    Check(HistoryIndex.GroupNames[0] == "Today", "en history group");

                    I18n.Apply("es");
                    Check(I18n.IsSpanish, "es mode is spanish");
                    Check(I18n.T("Captura", "Capture") == "Captura", "es text");
                    Check(I18n.Copy == "Copiar", "es copy");
                    Check(I18n.Cancel == "Cancelar", "es cancel");
                    Check(I18n.Fmt("{0} archivos", "{0} files", 5) == "5 archivos", "es fmt");
                    Check(Hotkeys.KeyName(System.Windows.Forms.Keys.Space) == "Espacio", "es space key");
                    Check(WellKind.Names[0] == "Beber agua", "es wellkind");
                    Check(HistoryIndex.GroupNames[0] == "Hoy", "es history group");
                }
                finally
                {
                    I18n.Apply("es");
                }
            });
            AppTestsI18n.Suite();
        }
    }
}
