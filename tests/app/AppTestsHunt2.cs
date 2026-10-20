// Hunt round 2 (Haiku tester, label h2-open): how files reach Stackshot (open requests in open.txt, OpensHere, which kinds
// open where), the per-user associations of .md, .xml and .mkv on a throwaway registry key, and the player's settings.
// open.txt is only ever read, moved or written in a private data folder (STACKSHOT_TEST_DATA), never the real one.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Stackshot.Tests
{
    static class AppTestsHunt2
    {
        const string SuiteName = "hunt2-open";
        const string CapKey = @"Software\Stackshot\Capabilities";
        const string RegApps = @"Software\RegisteredApplications";
        const string UserChoice = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.mkv\UserChoice";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly long Second = TimeSpan.TicksPerSecond, Minute = TimeSpan.TicksPerMinute;

        public static void Suite()
        {
            AppTests.Test(SuiteName, "open requests: fresh taken, stale and future dropped", OpenFreshStaleFuture);
            AppTests.Test(SuiteName, "open requests: 2 min old and 1 min ahead limits", OpenLimits);
            AppTests.Test(SuiteName, "open requests: same path in other case taken once", OpenCaseDuplicates);
            AppTests.Test(SuiteName, "open requests: CRLF line endings", OpenCrlf);
            AppTests.Test(SuiteName, "open requests: accents, emoji, spaces", OpenUnicode);
            AppTests.Test(SuiteName, "open requests: path longer than 260 characters", OpenLongPath);
            AppTests.Test(SuiteName, "open requests: md, mkv, MKV, png, gif, double and missing extensions", OpenMixedKinds);
            AppTests.Test(SuiteName, "open requests: blank lines and a line with only a number", OpenBlankLines);
            AppTests.Test(SuiteName, "open requests: a line without a timestamp is taken", OpenNoTimestamp);
            AppTests.Test(SuiteName, "open requests: a BOM at the start of open.txt", OpenBom);
            AppTests.Test(SuiteName, "open requests: a line whose time is not a number is not a path", OpenNonNumericTime);
            AppTests.Test(SuiteName, "open requests: a quoted path is ignored, not opened as another file", OpenQuoted);
            AppTests.Test(SuiteName, "open requests: a path with a trailing space comes back as written", OpenTrailingSpace);
            AppTests.Test(SuiteName, "open requests: a request is taken once", OpenTakenOnce);
            AppTests.Test(SuiteName, "open requests: 10 MB of old lines plus one fresh", OpenHuge);
            AppTests.Test(SuiteName, "open requests: binary garbage before a valid request", OpenBinary);
            AppTests.Test(SuiteName, "open requests: open.txt locked by another handle, nothing lost", OpenLockedNotLost);
            AppTests.Test(SuiteName, "open requests: a 400 ms lock does not stall the request", OpenLockRetried);

            AppTests.Test(SuiteName, "kinds: video, markdown and xml open here; any case, UNC", OpensHereKinds);
            AppTests.Test(SuiteName, "kinds: gif, images, double and trailing dots or spaces do not", OpensHereNot);
            AppTests.Test(SuiteName, "kinds: every VideoExtensions entry opens here except gif", EveryVideoExtensionOpensHere);
            AppTests.Test(SuiteName, "kinds: editor list (images, GIF, media) is IsEditable", EditableKinds);
            AppTests.Test(SuiteName, "kinds: what a file given alone to the exe does", ProgramDecision);
            AppTests.Test(SuiteName, "kinds: RouteFor sends masters and videos to the right place", RouteKinds);

            AppTests.Test(SuiteName, "assoc: video alone on, offered in Open with, off removes all of it", VideoAlone);
            AppTests.Test(SuiteName, "assoc: video off leaves no empty Software\\Stackshot behind", VideoAloneNoLeftovers);
            AppTests.Test(SuiteName, "assoc: all three, last one out takes the registration", AllThreeLastOneOut);
            AppTests.Test(SuiteName, "assoc: toggling video five times, off twice", Toggling);
            AppTests.Test(SuiteName, "assoc: Repair rewrites a command pointing to another exe", RepairStaleCommand);
            AppTests.Test(SuiteName, "assoc: Repair rewrites our ProgID even when its capability entry is gone", RepairWithoutCapability);
            AppTests.Test(SuiteName, "assoc: another app's OpenWithProgids, default and subkeys survive", OtherAppsKept);
            AppTests.Test(SuiteName, "assoc: ReadOnly writes nothing", ReadOnlyWritesNothing);
            AppTests.Test(SuiteName, "assoc: IsDefault reads UserChoice (case, other app, missing)", UserChoiceReading);
            AppTests.Test(SuiteName, "assoc: enable and disable never change the user's UserChoice", UserChoiceUntouched);
            AppTests.Test(SuiteName, "assoc: CleanShell takes video too, twice in a row", CleanShellTakesVideo);

            AppTests.Test(SuiteName, "settings: PlayerFill written and read back", PlayerFillRoundTrip);
            AppTests.Test(SuiteName, "settings: PlayerFill garbage (yes, 2, empty, 1, true, si)", PlayerFillGarbage);
            AppTests.Test(SuiteName, "settings: PlayerVolume garbage clamps or falls back", PlayerVolumeGarbage);
            AppTests.Test(SuiteName, "settings: PlayerScaling, ExportQuality, PlayerLoop, PlayerOpensVideos garbage", PlayerOtherGarbage);
        }

        // ---- helpers

        static string OpenFile { get { return Path.Combine(Settings.DataDir, "open.txt"); } }

        // True only when Settings.DataDir is not the real Stackshot data folder (or inside it).
        static bool PrivateData()
        {
            string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrEmpty(local)) return false;
            string data = Path.GetFullPath(Settings.DataDir).TrimEnd('\\') + "\\";
            string real = Path.GetFullPath(Path.Combine(local, "Stackshot")).TrimEnd('\\') + "\\";
            return !data.StartsWith(real, StringComparison.OrdinalIgnoreCase);
        }

        static bool Private(string test)
        {
            if (PrivateData()) return true;
            Console.WriteLine("  (" + test + " skipped: Settings.DataDir is the real data folder)");
            return false;
        }

        // Takes the requests only from a private folder: TakeOpenRequests renames the real open.txt away.
        static List<string> Take()
        {
            if (!PrivateData()) throw new InvalidOperationException("refused: the real data folder");
            return Installer.TakeOpenRequests();
        }

        static void ClearOpen()
        {
            if (!PrivateData()) throw new InvalidOperationException("refused: the real data folder");
            Directory.CreateDirectory(Settings.DataDir);
            foreach (string f in Directory.GetFiles(Settings.DataDir, "open*.txt")) File.Delete(f);
        }

        static void WriteOpen(string text)
        {
            ClearOpen();
            File.WriteAllBytes(OpenFile, Utf8.GetBytes(text));
        }

        static void WriteOpenBytes(byte[] bytes)
        {
            ClearOpen();
            File.WriteAllBytes(OpenFile, bytes);
        }

        // The line SendOpen writes: "<UtcNow.Ticks>\t<path>\n".
        static string Req(long ticks, string path) { return ticks + "\t" + path + "\n"; }

        static string Show(List<string> l) { return "[" + string.Join(" | ", l.ToArray()) + "]"; }

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static void Same<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(what + ": expected <" + expected + ">, got <" + actual + ">");
        }

        static bool Key(RegistryKey root, string path)
        {
            using (RegistryKey k = root.OpenSubKey(path)) return k != null;
        }

        static object Val(RegistryKey root, string path, string name)
        {
            using (RegistryKey k = root.OpenSubKey(path)) return k == null ? null : k.GetValue(name);
        }

        static void SetVal(RegistryKey root, string path, string name, string value)
        {
            using (RegistryKey k = root.CreateSubKey(path)) k.SetValue(name, value);
        }

        // A throwaway HKCU key used as the current user's registry (FileAssoc and Installer), removed afterwards.
        sealed class Hive : IDisposable
        {
            public readonly string Name = @"Software\StackshotTests\h2-" + Guid.NewGuid().ToString("N");
            public readonly RegistryKey Root;

            public Hive()
            {
                Root = Registry.CurrentUser.CreateSubKey(Name);
                FileAssoc.HkcuOverride = Root;
                Installer.HkcuOverride = Root;
                Settings.ReadOnly = false;
            }

            public void Dispose()
            {
                Settings.ReadOnly = true;
                FileAssoc.HkcuOverride = null;
                Installer.HkcuOverride = null;
                Root.Close();
                try { Registry.CurrentUser.DeleteSubKeyTree(Name, false); } catch { }
                try
                {
                    bool empty;
                    using (RegistryKey t = Registry.CurrentUser.OpenSubKey(@"Software\StackshotTests"))
                        empty = t != null && t.SubKeyCount == 0 && t.ValueCount == 0;
                    if (empty) Registry.CurrentUser.DeleteSubKey(@"Software\StackshotTests", false);
                }
                catch { }
            }
        }

        // ---- open requests (TakeOpenRequests)

        static void OpenFreshStaleFuture()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\fresco.md") + Req(now - 3 * Minute, @"C:\v\viejo.mkv") + Req(now + 5 * Minute, @"C:\v\futuro.mkv"));
            List<string> got = Take();
            Same(1, got.Count, "taken " + Show(got));
            Same(@"C:\v\fresco.md", got[0], "the fresh one");
            Check(!File.Exists(OpenFile), "open.txt was taken away");
        }

        static void OpenLimits()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now - 100 * Second, @"C:\v\hace100s.md") + Req(now - 130 * Second, @"C:\v\hace130s.md") +
                      Req(now + 50 * Second, @"C:\v\dentro50s.md") + Req(now + 70 * Second, @"C:\v\dentro70s.md"));
            List<string> got = Take();
            Check(got.Contains(@"C:\v\hace100s.md") && got.Contains(@"C:\v\dentro50s.md"), "inside the limits: " + Show(got));
            Check(!got.Contains(@"C:\v\hace130s.md") && !got.Contains(@"C:\v\dentro70s.md"), "outside the limits: " + Show(got));
        }

        static void OpenCaseDuplicates()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\V\Nota.MD") + Req(now, @"c:\v\NOTA.md") + Req(now, @"C:\v\nota.md"));
            List<string> got = Take();
            Same(1, got.Count, "count " + Show(got));
            Same(@"C:\V\Nota.MD", got[0], "first spelling kept");
        }

        static void OpenCrlf()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(now + "\t" + @"C:\v\uno.md" + "\r\n" + now + "\t" + @"C:\v\dos.mkv" + "\r\n");
            List<string> got = Take();
            Same(2, got.Count, "count " + Show(got));
            Same(@"C:\v\uno.md", got[0], "first, no CR left");
            Same(@"C:\v\dos.mkv", got[1], "second, no CR left");
        }

        static void OpenUnicode()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            string emoji = "C:\\v\\Captura \u00F1and\u00FA \uD83C\uDFAC 2026.mkv";
            string accents = "C:\\v\\caf\u00E9 \u00C0 \u00FCber.md";
            WriteOpen(Req(now, emoji) + Req(now, accents));
            List<string> got = Take();
            Same(2, got.Count, "count " + Show(got));
            Same(emoji, got[0], "emoji and accents");
            Same(accents, got[1], "accents");
        }

        static void OpenLongPath()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            StringBuilder sb = new StringBuilder(@"C:\v\");
            for (int i = 0; i < 30; i++) sb.Append("carpeta-de-prueba-larga-" + i + @"\");
            sb.Append("clip largo.mkv");
            string p = sb.ToString();
            Check(p.Length > 600, "the path is long: " + p.Length);
            WriteOpen(Req(now, p));
            List<string> got = Take();
            Same(1, got.Count, "count " + Show(got));
            Same(p, got[0], "whole long path");
        }

        static void OpenMixedKinds()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\a.md") + Req(now, @"C:\v\b.mkv") + Req(now, @"C:\v\C.MKV") + Req(now, @"C:\v\d.png") +
                      Req(now, @"C:\v\e.gif") + Req(now, @"C:\v\F.Md") + Req(now, @"C:\v\noext") + Req(now, @"C:\v\g.mkv.png") +
                      Req(now, @"C:\v\h.png.mkv") + Req(now, @"C:\v\i.GIF") + Req(now, @"\\srv\share\j.MKV"));
            List<string> got = Take();
            string want = string.Join(" | ", new string[] { @"C:\v\a.md", @"C:\v\b.mkv", @"C:\v\C.MKV", @"C:\v\F.Md", @"C:\v\h.png.mkv", @"\\srv\share\j.MKV" });
            Same(want, string.Join(" | ", got.ToArray()), "kinds taken");
        }

        static void OpenBlankLines()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen("\n   \n\t\n123\n\r\n" + Req(now, @"C:\v\bueno.md") + "   \n");
            List<string> got = Take();
            Same(1, got.Count, "only the valid one: " + Show(got));
            Same(@"C:\v\bueno.md", got[0], "the valid one");
        }

        static void OpenNoTimestamp()
        {
            if (!Private("open requests")) return;
            WriteOpen(@"C:\v\sin-hora.md" + "\n");
            List<string> got = Take();
            Same(1, got.Count, "count " + Show(got));
            Same(@"C:\v\sin-hora.md", got[0], "taken without a time");
        }

        static void OpenBom()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] line = Utf8.GetBytes(Req(now, @"C:\v\con-bom.md"));
            byte[] all = new byte[bom.Length + line.Length];
            Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
            Buffer.BlockCopy(line, 0, all, bom.Length, line.Length);
            WriteOpenBytes(all);
            List<string> got = Take();
            Same(1, got.Count, "count " + Show(got));
            Same(@"C:\v\con-bom.md", got[0], "first request after the BOM");
        }

        // A line whose first field is not a number is not "time<TAB>path": it is kept whole as a path.
        static void OpenNonNumericTime()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen("abc\t" + @"C:\v\x.md" + "\n" + Req(now, @"C:\v\ok.md"));
            List<string> got = Take();
            Same(@"C:\v\ok.md", string.Join(" | ", got.ToArray()), "only the valid request: " + Show(got));
        }

        static void OpenQuoted()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, "\"C:\\v\\citado.md\"") + Req(now, @"C:\v\ok.md"));
            List<string> got = Take();
            Same(@"C:\v\ok.md", string.Join(" | ", got.ToArray()), "only the unquoted one: " + Show(got));
        }

        // Observed: the line is trimmed, so "a.md " (a name only WSL can create) comes back as "a.md" (another file).
        static void OpenTrailingSpace()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\espacio.md "));
            List<string> got = Take();
            Same(1, got.Count, "count " + Show(got));
            // Windows drops a trailing space from any path it opens, so the request names the file Windows would open.
            Same(@"C:\v\espacio.md", got[0], "trimmed as Windows trims it");
        }

        static void OpenTakenOnce()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\una.md"));
            Same(1, Take().Count, "first take");
            Same(0, Take().Count, "second take");
            Check(!File.Exists(OpenFile), "nothing left behind");
        }

        static void OpenHuge()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            ClearOpen();
            using (StreamWriter w = new StreamWriter(OpenFile, false, Utf8))
            {
                for (int i = 0; i < 400000; i++) w.Write("1\tC:\\v\\viejo-" + i + ".mkv\n");
                w.Write(Req(now, @"C:\v\nuevo.mkv"));
            }
            Console.WriteLine("  (open.txt " + new FileInfo(OpenFile).Length + " bytes)");
            Stopwatch sw = Stopwatch.StartNew();
            List<string> got = Take();
            sw.Stop();
            Console.WriteLine("  (took " + sw.ElapsedMilliseconds + " ms)");
            Same(@"C:\v\nuevo.mkv", string.Join(" | ", got.ToArray()), "only the fresh one");
            Check(sw.ElapsedMilliseconds < 15000, "too slow: " + sw.ElapsedMilliseconds + " ms");
        }

        static void OpenBinary()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            Random rnd = new Random(1234);
            byte[] junk = new byte[200000];
            rnd.NextBytes(junk);
            byte[] tail = Utf8.GetBytes("\n" + Req(now, @"C:\v\tras-basura.md"));
            byte[] all = new byte[junk.Length + tail.Length];
            Buffer.BlockCopy(junk, 0, all, 0, junk.Length);
            Buffer.BlockCopy(tail, 0, all, junk.Length, tail.Length);
            WriteOpenBytes(all);
            List<string> got = Take();
            Check(got.Contains(@"C:\v\tras-basura.md"), "valid request after the garbage: " + Show(got));
            Same(1, got.Count, "only the valid one: " + Show(got));
        }

        static void OpenLockedNotLost()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\retenido.md"));
            using (FileStream held = new FileStream(OpenFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                List<string> during = Take();
                Same(0, during.Count, "taken while locked: " + Show(during));
                Check(File.Exists(OpenFile), "still in open.txt while locked");
            }
            List<string> after = Take();
            Same(1, after.Count, "taken once free: " + Show(after));
        }

        // Found by this test: the take gave up after three tries (about 150 ms) and nothing retried it, so a 400 ms lock
        // (a scanner) left the double-clicked file waiting for the next request. Now the UI thread still never waits out
        // the lock, but the request shows as pending and the running copy takes it again (ShotStack.OpenRequested retries
        // every 300 ms while Installer.OpenPending), as the second take here does.
        static void OpenLockRetried()
        {
            if (!Private("open requests")) return;
            long now = DateTime.UtcNow.Ticks;
            WriteOpen(Req(now, @"C:\v\retenido-400.md"));
            FileStream held = new FileStream(OpenFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            Thread release = new Thread(delegate() { Thread.Sleep(400); held.Dispose(); });
            release.Start();
            List<string> got = Take();
            bool pending = Installer.OpenPending;
            release.Join();
            Console.WriteLine("  (during a 400 ms lock: " + Show(got) + "; pending: " + pending + ")");
            Check(got.Count == 1 || pending, "neither taken nor left pending: " + Show(got));
            if (got.Count == 0)
            {
                List<string> again = Take();
                Same(1, again.Count, "taken once the lock is gone: " + Show(again));
                Check(!Installer.OpenPending, "nothing left pending");
            }
        }

        // ---- which files open where (Installer.OpensHere, ShotStack.IsEditable)

        static void OpensHereKinds()
        {
            string[] open = {
                @"C:\v\a.mkv", @"C:\v\A.MKV", @"C:\v\b.Mkv", @"C:\v\c.mp4", @"C:\v\d.webm", @"C:\v\e.MOV", @"C:\v\f.ts",
                @"C:\v\g.m2ts", @"C:\v\h.avi", @"C:\v\i.wmv", @"\\srv\share\clip.mkv", @"C:\v\Cine 2026-10-11 17.03.26.mkv",
                @"C:\v\a.png.mkv", @"C:\v\j.md", @"C:\v\K.MD", @"C:\v\l.markdown", @"C:\v\m.mkd", @"C:\v\n.xml", @"C:\v\o.XSD",
                @"C:\v\p.svg", "C:\\v\\Documento \u00F1.md", @"C:\v\b.png", @"C:\v\c.JPG", @"C:\v\d.txt", @"C:\v\a.mkv.png", @"C:\v\e.PDF" };
            foreach (string p in open) Check(Installer.OpensHere(p), "should open here: " + p);
        }

        static void OpensHereNot()
        {
            string[] alone = {
                @"C:\v\a.gif", @"C:\v\A.GIF", @"C:\v\noext", @"C:\v\a.docx",
                @"C:\v\a.mkv.exe", @"C:\v\a.mkv.", @"C:\v\a.mkv ", "", null };
            foreach (string p in alone) Check(!Installer.OpensHere(p), "should not open here: " + p);
        }

        static void EveryVideoExtensionOpensHere()
        {
            foreach (string ext in ShotStack.VideoExtensions)
                Same(ext != ".gif", Installer.OpensHere(@"C:\v\x" + ext), "OpensHere of x" + ext);
        }

        static string[] ShotStackList(string name)
        {
            FieldInfo f = typeof(ShotStack).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Check(f != null, "no field " + name);
            return (string[])f.GetValue(null);
        }

        static void EditableKinds()
        {
            foreach (string ext in ShotStackList("ImageExts")) Check(ShotStack.IsEditable("x" + ext), "image editable: " + ext);
            foreach (string ext in ShotStackList("MediaExts")) Check(ShotStack.IsEditable("x" + ext), "media editable: " + ext);
            foreach (string p in new string[] { "x.md", "x.xml", "x.txt", "x.m4v", "x.wmv", "x.ts", "noext" })
                Check(!ShotStack.IsEditable(p), "not editable: " + p);
        }

        // Program.Main with one file: the editor when it is editable and not for the viewer or player.
        static void ProgramDecision()
        {
            foreach (string p in new string[] { @"C:\v\a.gif", @"C:\v\A.GIF", @"C:\v\b.png", @"C:\v\c.jpeg", @"C:\v\d.tif" })
                Check(ShotStack.IsEditable(p) && !Installer.OpensHere(p), "editor: " + p);
            foreach (string p in new string[] { @"C:\v\a.mkv", @"C:\v\b.MP4", @"C:\v\c.webm", @"C:\v\d.mov", @"C:\v\e.avi", @"C:\v\f.m4v", @"C:\v\g.wmv" })
                Check(Installer.OpensHere(p) && !(ShotStack.IsEditable(p) && !Installer.OpensHere(p)), "player: " + p);
            foreach (string p in new string[] { @"C:\v\a.md", @"C:\v\b.xml" })
                Check(Installer.OpensHere(p) && !ShotStack.IsEditable(p), "viewer: " + p);
        }

        static void RouteKinds()
        {
            Same(ShotStack.OpenRoute.Windows, ShotStack.RouteFor(@"C:\v\a.mkv", false), "mkv with the player off");
            Same(ShotStack.OpenRoute.Player, ShotStack.RouteFor(@"C:\v\a.mkv", true), "mkv with the player on");
            Same(ShotStack.OpenRoute.Player, ShotStack.RouteFor(@"C:\v\Cine 2026-10-11 17.03.26.mkv", false), "master always in the player");
            Same(ShotStack.OpenRoute.Player, ShotStack.RouteFor(@"C:\v\cine 2026-10-11 17.03.26.MKV", false), "master, other case");
            Same(ShotStack.OpenRoute.Windows, ShotStack.RouteFor(@"C:\v\a.gif", false), "gif with the player off");
            Same(ShotStack.OpenRoute.Editor, ShotStack.RouteFor(@"C:\v\a.png", true), "png");
            Same(ShotStack.OpenRoute.Editor, ShotStack.RouteFor("", true), "no path");
        }

        // ---- associations on the throwaway key (FileAssoc)

        static void VideoAlone()
        {
            using (Hive h = new Hive())
            {
                Check(FileAssoc.Enable(FileAssoc.Video), "enable returned false");
                Check(FileAssoc.Registered(FileAssoc.Video), "not registered");
                Same<object>(FileAssoc.Command, Val(h.Root, @"Software\Classes\Stackshot.Video\shell\open\command", ""), "open command");
                Check(FileAssoc.Command.StartsWith("\"") && FileAssoc.Command.EndsWith("\" \"%1\""), "command shape: " + FileAssoc.Command);
                Same<object>("", Val(h.Root, @"Software\Classes\.mkv\OpenWithProgids", "Stackshot.Video"), "open with");
                Same<object>("Stackshot.Video", Val(h.Root, CapKey + @"\FileAssociations", ".mkv"), "capability");
                Same<object>(CapKey, Val(h.Root, RegApps, "Stackshot"), "registered application");
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "default without the user's choice");
                FileAssoc.Disable(FileAssoc.Video);
                Check(!FileAssoc.Registered(FileAssoc.Video), "still registered");
                Check(!Key(h.Root, @"Software\Classes\Stackshot.Video"), "progid left");
                Check(!Key(h.Root, @"Software\Classes\.mkv"), ".mkv key left (we created it, it is empty)");
                Check(!Key(h.Root, CapKey), "capabilities left");
                Same<object>(null, Val(h.Root, RegApps, "Stackshot"), "registered application left");
            }
        }

        // Observed: Disable removes Capabilities but not the Software\Stackshot key above it (CleanShell removes that one).
        static void VideoAloneNoLeftovers()
        {
            using (Hive h = new Hive())
            {
                FileAssoc.Enable(FileAssoc.Video);
                FileAssoc.Disable(FileAssoc.Video);
                Check(!Key(h.Root, @"Software\Stackshot"), "empty Software\\Stackshot left behind");
            }
        }

        static void AllThreeLastOneOut()
        {
            using (Hive h = new Hive())
            {
                Check(FileAssoc.Enable(FileAssoc.Markdown) && FileAssoc.Enable(FileAssoc.Xml) && FileAssoc.Enable(FileAssoc.Video), "enable all");
                FileAssoc.Disable(FileAssoc.Xml);
                Check(Val(h.Root, RegApps, "Stackshot") != null, "registration gone after xml off");
                Check(Val(h.Root, CapKey + @"\FileAssociations", ".xml") == null, "xml capability still there");
                Check(Val(h.Root, CapKey + @"\FileAssociations", ".md") != null && Val(h.Root, CapKey + @"\FileAssociations", ".mkv") != null, "md and mkv capabilities lost");
                FileAssoc.Disable(FileAssoc.Video);
                Check(Val(h.Root, RegApps, "Stackshot") != null && Key(h.Root, CapKey), "registration gone with md still on");
                Check(Val(h.Root, CapKey + @"\FileAssociations", ".mkv") == null, "mkv capability still there");
                FileAssoc.Disable(FileAssoc.Markdown);
                Check(!Key(h.Root, CapKey), "capabilities left with nothing on");
                Check(Val(h.Root, RegApps, "Stackshot") == null, "registration left with nothing on");
                Check(FileAssoc.Enable(FileAssoc.Xml), "enable xml again");
                Check(Val(h.Root, CapKey + @"\FileAssociations", ".md") == null && Val(h.Root, CapKey + @"\FileAssociations", ".mkv") == null, "only xml comes back");
                Same<object>("Stackshot.Xml", Val(h.Root, CapKey + @"\FileAssociations", ".xml"), "xml capability back");
            }
        }

        static void Toggling()
        {
            using (Hive h = new Hive())
            {
                for (int i = 0; i < 5; i++)
                {
                    Check(FileAssoc.Enable(FileAssoc.Video), "enable #" + i);
                    Check(FileAssoc.Registered(FileAssoc.Video), "registered #" + i);
                    FileAssoc.Disable(FileAssoc.Video);
                    FileAssoc.Disable(FileAssoc.Video);
                    Check(!FileAssoc.Registered(FileAssoc.Video), "gone #" + i);
                }
                Check(!Key(h.Root, @"Software\Classes\.mkv"), "a .mkv key left after toggling");
                Check(!Key(h.Root, CapKey) && Val(h.Root, RegApps, "Stackshot") == null, "registration left after toggling");
                Check(FileAssoc.Enable(FileAssoc.Video) && FileAssoc.Enable(FileAssoc.Video), "enable twice");
                RegistryKey w = h.Root.OpenSubKey(@"Software\Classes\.mkv\OpenWithProgids");
                Same(1, w == null ? -1 : w.ValueCount, "one open-with entry after enabling twice");
                if (w != null) w.Close();
            }
        }

        // Observed: the stale command is left alone when our capability entry is gone (Repair only rewrites what passes
        // CapabilityHas). The ProgID is ours by name, so the command could be fixed anyway.
        static void RepairStaleCommand()
        {
            using (Hive h = new Hive())
            {
                FileAssoc.Enable(FileAssoc.Video);
                SetVal(h.Root, @"Software\Classes\Stackshot.Video\shell\open\command", "", "\"C:\\Users\\viejo\\Stackshot.exe\" \"%1\"");
                FileAssoc.Repair();
                Same<object>(FileAssoc.Command, Val(h.Root, @"Software\Classes\Stackshot.Video\shell\open\command", ""), "command rewritten to this copy");
            }
        }

        static void RepairWithoutCapability()
        {
            using (Hive h = new Hive())
            {
                FileAssoc.Enable(FileAssoc.Video);
                SetVal(h.Root, @"Software\Classes\Stackshot.Video\shell\open\command", "", "\"C:\\Users\\viejo\\Stackshot.exe\" \"%1\"");
                using (RegistryKey fa = h.Root.OpenSubKey(CapKey + @"\FileAssociations", true)) fa.DeleteValue(".mkv", false);
                FileAssoc.Repair();
                Same<object>(FileAssoc.Command, Val(h.Root, @"Software\Classes\Stackshot.Video\shell\open\command", ""), "command of our ProgID rewritten");
            }
        }

        static void OtherAppsKept()
        {
            using (Hive h = new Hive())
            {
                SetVal(h.Root, @"Software\Classes\.mkv\OpenWithProgids", "VLC.mkv", "");
                SetVal(h.Root, @"Software\Classes\.mkv", "", "VLC.mkv");
                SetVal(h.Root, @"Software\Classes\.mkv\OpenWithList\vlc.exe", "", "");
                SetVal(h.Root, @"Software\Classes\.mkv", "PerceivedType", "video");
                SetVal(h.Root, @"Software\Classes\VLC.mkv\shell\open\command", "", "vlc.exe \"%1\"");
                FileAssoc.Enable(FileAssoc.Video);
                FileAssoc.Disable(FileAssoc.Video);
                Same<object>("", Val(h.Root, @"Software\Classes\.mkv\OpenWithProgids", "VLC.mkv"), "other app's open-with entry");
                Same<object>(null, Val(h.Root, @"Software\Classes\.mkv\OpenWithProgids", "Stackshot.Video"), "our open-with entry");
                Same<object>("VLC.mkv", Val(h.Root, @"Software\Classes\.mkv", ""), "the extension's default");
                Same<object>("video", Val(h.Root, @"Software\Classes\.mkv", "PerceivedType"), "PerceivedType");
                Check(Key(h.Root, @"Software\Classes\.mkv\OpenWithList\vlc.exe"), "OpenWithList of another app");
                Check(Key(h.Root, @"Software\Classes\VLC.mkv\shell\open\command"), "another app's ProgID");
            }
        }

        static void ReadOnlyWritesNothing()
        {
            using (Hive h = new Hive())
            {
                Settings.ReadOnly = true;
                Check(!FileAssoc.Enable(FileAssoc.Video), "enable with ReadOnly returned true");
                Check(!Key(h.Root, @"Software\Classes\Stackshot.Video") && !Key(h.Root, @"Software\Classes\.mkv"), "keys written under ReadOnly");
                Check(!Key(h.Root, CapKey) && Val(h.Root, RegApps, "Stackshot") == null, "registration written under ReadOnly");
            }
        }

        static void UserChoiceReading()
        {
            using (Hive h = new Hive())
            {
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "no choice: not default");
                SetVal(h.Root, UserChoice, "ProgId", "Stackshot.Video");
                Check(FileAssoc.IsDefault(FileAssoc.Video), "Stackshot.Video chosen: default");
                SetVal(h.Root, UserChoice, "ProgId", "stackshot.video");
                Check(FileAssoc.IsDefault(FileAssoc.Video), "other case: default");
                SetVal(h.Root, UserChoice, "ProgId", "VLC.mkv");
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "VLC chosen: not default");
                SetVal(h.Root, UserChoice, "ProgId", "Stackshot.Video.Old");
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "a similar name: not default");
                using (RegistryKey k = h.Root.OpenSubKey(UserChoice, true)) k.DeleteValue("ProgId");
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "no ProgId value: not default");
                using (RegistryKey k = h.Root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.mkv", true)) k.DeleteSubKeyTree("UserChoice");
                Check(!FileAssoc.IsDefault(FileAssoc.Video), "no UserChoice key: not default");
            }
        }

        static void UserChoiceUntouched()
        {
            using (Hive h = new Hive())
            {
                SetVal(h.Root, UserChoice, "ProgId", "Stackshot.Video");
                FileAssoc.Enable(FileAssoc.Video);
                FileAssoc.Disable(FileAssoc.Video);
                Same<object>("Stackshot.Video", Val(h.Root, UserChoice, "ProgId"), "the user's choice as it was");
            }
        }

        static void CleanShellTakesVideo()
        {
            using (Hive h = new Hive())
            {
                Check(FileAssoc.Enable(FileAssoc.Markdown) && FileAssoc.Enable(FileAssoc.Xml) && FileAssoc.Enable(FileAssoc.Video), "enable all");
                Installer.CleanShell();
                Check(!FileAssoc.Registered(FileAssoc.Markdown) && !FileAssoc.Registered(FileAssoc.Xml) && !FileAssoc.Registered(FileAssoc.Video), "still registered");
                Check(!Key(h.Root, @"Software\Classes\.mkv") && !Key(h.Root, @"Software\Classes\.md"), "extension keys left");
                Check(!Key(h.Root, @"Software\Stackshot"), "Software\\Stackshot left");
                Installer.CleanShell();
                Check(Val(h.Root, RegApps, "Stackshot") == null, "registration left after the second run");
            }
        }

        // ---- player settings (Settings.Parse / Write)

        static void WriteSettings(Settings s, string path)
        {
            MethodInfo write = typeof(Settings).GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance);
            write.Invoke(s, new object[] { path });
        }

        static Settings Loaded(string text)
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-hunt2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.ini");
            File.WriteAllText(path, text, Utf8);
            return Settings.Load(path);
        }

        static void PlayerFillRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-hunt2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.ini");
            Settings s = new Settings();
            s.PlayerFill = true;
            WriteSettings(s, path);
            Check(File.ReadAllText(path).Contains("PlayerFill = 1"), "written as 1");
            Same(true, Settings.Load(path).PlayerFill, "true comes back");
            s.PlayerFill = false;
            WriteSettings(s, path);
            Check(File.ReadAllText(path).Contains("PlayerFill = 0"), "written as 0");
            Same(false, Settings.Load(path).PlayerFill, "false comes back");
        }

        static void PlayerFillGarbage()
        {
            Same(true, Loaded("PlayerFill = yes\n").PlayerFill, "yes");
            Same(true, Loaded("PlayerFill = 1\n").PlayerFill, "1");
            Same(true, Loaded("PlayerFill = TRUE\n").PlayerFill, "TRUE");
            Same(true, Loaded("PlayerFill = si\n").PlayerFill, "si");
            Same(false, Loaded("PlayerFill = 2\n").PlayerFill, "2 is not a value: default");
            Same(false, Loaded("PlayerFill =\n").PlayerFill, "empty: default");
            Same(false, Loaded("PlayerFill = 0\n").PlayerFill, "0");
            Same(false, Loaded("PlayerFill = no\n").PlayerFill, "no");
        }

        static void PlayerVolumeGarbage()
        {
            Same(100, Loaded("PlayerVolume = yes\n").PlayerVolume, "yes: default");
            Same(2, Loaded("PlayerVolume = 2\n").PlayerVolume, "2 is a volume");
            Same(100, Loaded("PlayerVolume =\n").PlayerVolume, "empty: default");
            Same(0, Loaded("PlayerVolume = -5\n").PlayerVolume, "negative: 0");
            Same(100, Loaded("PlayerVolume = 1e3\n").PlayerVolume, "exponent: default");
            Same(100, Loaded("PlayerVolume = 99999999999999999999\n").PlayerVolume, "huge: 100");
            Same(0, Loaded("PlayerVolume = -99999999999999999999\n").PlayerVolume, "huge negative: 0");
            Same(50, Loaded("PlayerVolume = +50\n").PlayerVolume, "plus sign");
            Same(100, Loaded("PlayerVolume = 250\n").PlayerVolume, "over the range: 100");
        }

        static void PlayerOtherGarbage()
        {
            Same(1, Loaded("PlayerScaling = 2\n").PlayerScaling, "scaling 2 clamps to 1");
            Same(0, Loaded("PlayerScaling = yes\n").PlayerScaling, "scaling yes: default");
            Same(2, Loaded("ExportQuality = 2\n").ExportQuality, "quality 2");
            Same(3, Loaded("ExportQuality = 3\n").ExportQuality, "quality 3 (16 MB)");
            Same(4, Loaded("ExportQuality = 9\n").ExportQuality, "quality 9 clamps to 4 (25 MB)");
            Same(0, Loaded("ExportQuality = yes\n").ExportQuality, "quality yes: default");
            Same(true, Loaded("PlayerLoop = yes\n").PlayerLoop, "loop yes");
            Same(false, Loaded("PlayerLoop = 2\n").PlayerLoop, "loop 2: default");
            Same(true, Loaded("PlayerOpensVideos =\n").PlayerOpensVideos, "opens videos, empty: default on");
            Same(false, Loaded("PlayerOpensVideos = 0\n").PlayerOpensVideos, "opens videos off");
        }
    }
}
