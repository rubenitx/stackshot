// Hunt tests of the app shell (Haiku tester): settings round trip, update rules, recents, FFmpeg pins, tracked clipboard
// data and the uninstall file deletion. Only private folders, never the registry, the network or the real data folder.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot.Tests
{
    static class AppTestsHunt
    {
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;

        static string Folder(string name)
        {
            string d = Path.Combine(Path.GetTempPath(), "stackshot-app-hunt", Guid.NewGuid().ToString("N"), name);
            Directory.CreateDirectory(d);
            return d;
        }

        static void Fail(string what) { throw new InvalidOperationException(what); }

        static void Same<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                Fail(what + ": expected <" + expected + ">, got <" + actual + ">");
        }

        static string Sha256Hex(byte[] data)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        // ---- Settings

        static Dictionary<string, object> AllValues()
        {
            Dictionary<string, object> v = new Dictionary<string, object>();
            v["SaveFolder"] = @"D:\Capturas Stackshot\2026";
            v["Sound"] = false; v["CopyToClipboard"] = false; v["FollowMouse"] = false; v["StackTuck"] = 10; v["FirstRunDone"] = true;
            v["HotRegion"] = "Ctrl+Shift+F1, PrintScreen"; v["HotScreen"] = "Ctrl+F2"; v["HotWindow"] = "Alt+F3";
            v["HotScroll"] = "Ctrl+Alt+F4"; v["HotVideo"] = "Shift+F5"; v["HotGif"] = "Ctrl+Shift+F6"; v["HotMarkdown"] = "Ctrl+Shift+Alt+F7"; v["HotText"] = "Ctrl+Alt+F8";
            v["HotRepeat"] = "Ctrl+Shift+Alt+F9"; v["HotDelayed"] = "Ctrl+Shift+Alt+F11"; v["CaptureDelay"] = 10;
            v["VideoFps"] = 60; v["GifFps"] = 25; v["VideoQuality"] = 3;
            v["RecordSystemAudio"] = true; v["RecordMic"] = true;
            v["MicDevice"] = "{0.0.1.00000000}.{abc-123}"; v["SystemAudioDevice"] = "{0.0.0.00000000}.{xyz}";
            v["Webcam"] = 2; v["WebcamSize"] = 2; v["WebcamDevice"] = "C\u00E1mara HD, USB";
            v["Ffmpeg"] = @"C:\Herramientas\ffmpeg\bin\ffmpeg.exe";
            v["BgAuto"] = true; v["BgPreset"] = 7; v["BgPadding"] = 33; v["BgRadius"] = 12; v["BgShadow"] = false; v["BgRatio"] = "4:3";
            v["BgKind"] = 1; v["BgColor"] = 0x12AB34; v["BgBlur"] = 2; v["BgShadowSize"] = 77; v["BgInset"] = 15; v["BgBalance"] = true; v["BgAlign"] = 6; v["BgStyles"] = "1,0,1223476,2,33,12,0,77,15,1,6,4:3;0,3,16777215,0,50,40,1,50,0,0,4,auto";
            v["Appearance"] = 2; v["AllInOne"] = true; v["AdjustFirst"] = true; v["ShowRecent"] = false; v["TempKeep"] = 4; v["HomeStyle"] = 2;
            v["CloseToTray"] = false; v["Profile"] = 0; v["ShowIntro"] = false; v["Language"] = "en"; v["LanguageOffered"] = true;
            v["MascotOn"] = false; v["MascotName"] = "\u00D1u Pixel"; v["MascotColor"] = 5; v["MascotTalks"] = false;
            v["MascotKind"] = 3; v["MascotHat"] = 4; v["MascotOutfit"] = 2; v["MascotFace"] = 1; v["MascotEyes"] = 1;
            v["MascotPersonality"] = 1; v["MascotSeasonal"] = false; v["MascotDesktop"] = true; v["MascotClimb"] = false;
            v["MascotAllScreens"] = false; v["MascotSpot"] = 400; v["MascotLove"] = 1234; v["MascotFavorites"] = "0:1,2:3";
            v["MascotLooks"] = "a|b"; v["MascotAccessory"] = 3; v["MascotEnergy"] = 2; v["MascotChatter"] = 0; v["MascotAlone"] = 9;
            v["MascotSleepIdle"] = false; v["MascotRhythm"] = false;
            v["CheckUpdates"] = false; v["LastUpdateCheck"] = "2026-10-09T10:00:00.0000000Z"; v["AutoUpdate"] = true; v["UpdateNotified"] = "v2.0.1";
            v["CineFps"] = 30; v["CinePeakMemory"] = 1; v["RecordSetup"] = false; v["RecordCountdown"] = 10;
            v["PlayerOpensVideos"] = false; v["PlayerScaling"] = 1; v["PlayerFill"] = true; v["PlayerVolume"] = 35; v["PlayerLoop"] = true; v["ExportQuality"] = 2;
            v["XmlWatch"] = true; v["XmlWatchFolder"] = @"D:\Mensajes SOAP\entrada";
            v["CaptureCursor"] = true; v["FileNameTemplate"] = "{app} - {ventana} {fecha}"; v["SaveSubfolder"] = 2; v["SensitiveAuto"] = true; v["HistorySearch"] = false;
            v["RecordClicks"] = true; v["RecordKeys"] = true;
            v["SaveAll"] = false; v["MascotHideFull"] = false; v["NewsSeen"] = "2.1.0"; v["ScrollGuides"] = 2;
            v["WellOn"] = false; v["WellWater"] = 90; v["WellEyes"] = 20; v["WellMove"] = 45; v["WellBreathe"] = 120; v["WellToasts"] = true;
            v["WellDay"] = "2026-10-14"; v["WellDone"] = "1,2,3,4";
            v["FocusWork"] = 50; v["FocusShort"] = 10; v["FocusLong"] = 30; v["FocusRounds"] = 3; v["FocusAuto"] = true; v["FocusSound"] = false;
            v["FocusNotices"] = false; v["FocusDay"] = "2026-10-14"; v["FocusBlocks"] = 7; v["FocusMinutes"] = 175;
            v["WaterGoal"] = 10; v["SummaryHour"] = 17; v["SummaryMinute"] = 30; v["StepsGoal"] = 8000; v["SummaryParts"] = 13; v["SummaryDay"] = "2026-10-13";
            v["XmlFaultAlert"] = false; v["FitbitAsked"] = "no"; v["HealthTalk"] = false; v["HealthSaid"] = "2026-10-14|a,b";
            v["FitbitLast"] = "5000|8000|420|30|2|638000000000000000"; v["ShortSleepDay"] = "2026-10-12"; v["StatsDay"] = "2026-10-14"; v["StatsCaptures"] = 12;
            return v;
        }

        static string WriteSettings(Settings s, string path)
        {
            MethodInfo write = typeof(Settings).GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance);
            write.Invoke(s, new object[] { path });
            return path;
        }

        public static void SettingsRoundTrip()
        {
            Dictionary<string, object> v = AllValues();
            Settings s = new Settings();
            List<FieldInfo> fields = new List<FieldInfo>(typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance));
            foreach (FieldInfo f in fields)
                if (!v.ContainsKey(f.Name)) Fail("no test value for Settings." + f.Name + " (new field: add it to AllValues)");
            foreach (FieldInfo f in fields) f.SetValue(s, v[f.Name]);

            string path = Path.Combine(Folder("rt"), "settings.ini");
            WriteSettings(s, path);
            Settings back = Settings.Load(path);
            foreach (FieldInfo f in fields)
                Same(f.GetValue(s), f.GetValue(back), "round trip of " + f.Name);

            // Every key is written once.
            string[] lines = File.ReadAllLines(path);
            Dictionary<string, int> keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in lines)
            {
                if (l.StartsWith("#") || l.IndexOf('=') <= 0) continue;
                string k = l.Substring(0, l.IndexOf('=')).Trim();
                int n;
                keys.TryGetValue(k, out n);
                keys[k] = n + 1;
            }
            foreach (KeyValuePair<string, int> kv in keys)
                Same(1, kv.Value, "key written once: " + kv.Key);
        }

        // Writing again over a file keeps the previous one as .bak and a broken file falls back to it.
        public static void SettingsBackupFallback()
        {
            string dir = Folder("bak");
            string path = Path.Combine(dir, "settings.ini");
            Settings a = new Settings();
            a.Sound = false;
            a.VideoFps = 24;
            WriteSettings(a, path);
            Settings b = new Settings();
            b.Sound = true;
            WriteSettings(b, path);
            Same(true, Settings.Load(path).Sound, "current file read");
            Same(false, Settings.Load(path + ".bak").Sound, "backup holds the previous file");
            File.WriteAllText(path, "esto no es un fichero de ajustes");
            Settings fallback = Settings.Load(path);
            Same(false, fallback.Sound, "broken file falls back to the backup");
            Same(24, fallback.VideoFps, "broken file falls back to the backup (values)");
        }

        public static void SettingsMissingAndGarbage()
        {
            string dir = Folder("missing");
            Settings none = Settings.Load(Path.Combine(dir, "nope.ini"));
            Settings def = new Settings();
            Same(def.SaveFolder, none.SaveFolder, "missing file: default save folder");
            Same(def.VideoFps, none.VideoFps, "missing file: default fps");
            string path = Path.Combine(dir, "empty.ini");
            File.WriteAllText(path, "");
            Same(def.Sound, Settings.Load(path).Sound, "empty file: defaults");
            File.WriteAllText(path, "# solo comentarios\n; otro\n\nclave sin igual\n= sin clave\nFoo = 1\n");
            Same(def.Sound, Settings.Load(path).Sound, "no known key: defaults");
        }

        // Each value is read as the program clamps or rejects it.
        public static void SettingsParseRules()
        {
            string dir = Folder("parse");
            string path = Path.Combine(dir, "settings.ini");
            File.WriteAllText(path,
                "Sound = maybe\n" +
                "  SAVEFOLDER  =  relative\\carpeta  \n" +
                "VideoFps = 999\n" +
                "GifFps = 2\n" +
                "VideoQuality = 9\n" +
                "TempKeep = -4\n" +
                "Appearance = 7\n" +
                "PlayerVolume = 250\n" +
                "BgRatio = 5:4\n" +
                "RecordCountdown = 7\n" +
                "CineFps = 25\n" +
                "MascotName = 12345678901234567890\n" +
                "Mascot = 0\n" +
                "CheckUpdates = si\n" +
                "Ffmpeg = \"C:\\a b\\ffmpeg.exe\"\n" +
                "Video = Shift+F9\n" +
                "CineFps = 50\n");
            Settings s = Settings.Load(path);
            Settings def = new Settings();
            Same(def.Sound, s.Sound, "Sound = maybe keeps the default");
            Same(def.SaveFolder, s.SaveFolder, "relative save folder keeps the default");
            Same(60, s.VideoFps, "VideoFps clamps to 60");
            Same(5, s.GifFps, "GifFps clamps to 5");
            Same(3, s.VideoQuality, "VideoQuality clamps to 3");
            Same(0, s.TempKeep, "TempKeep clamps to 0");
            Same(2, s.Appearance, "Appearance clamps to 2");
            Same(100, s.PlayerVolume, "PlayerVolume clamps to 100");
            Same("auto", s.BgRatio, "BgRatio 5:4 is refused");
            Same(5, s.RecordCountdown, "RecordCountdown 7 becomes 5");
            Same(60, s.CineFps, "CineFps 50 becomes 60 (last line wins)");
            Same(16, s.MascotName.Length, "MascotName is cut at 16 characters");
            Same(false, s.MascotOn, "Mascot = 0 turns the mascot off");
            Same(true, s.CheckUpdates, "CheckUpdates = si is yes");
            Same(@"C:\a b\ffmpeg.exe", s.Ffmpeg, "quoted Ffmpeg path is unquoted");
            Same("Shift+F9", s.HotVideo, "Video key reads the video hotkey");
        }

        public static void SettingsCountdownAndHotkeys()
        {
            Same(0, Settings.Countdown(0), "0 -> 0");
            Same(0, Settings.Countdown(1), "1 -> 0");
            Same(3, Settings.Countdown(2), "2 -> 3");
            Same(3, Settings.Countdown(4), "4 -> 3");
            Same(5, Settings.Countdown(5), "5 -> 5");
            Same(5, Settings.Countdown(7), "7 -> 5");
            Same(10, Settings.Countdown(8), "8 -> 10");
            Same(10, Settings.Countdown(60), "60 -> 10");
            Settings s = new Settings();
            foreach (string action in Settings.Actions)
            {
                s.SetHotkeys(action, "Ctrl+" + action);
                Same("Ctrl+" + action, s.HotkeysFor(action), "hotkey round trip " + action);
            }
        }

        // ---- Updater (pure rules; nothing is downloaded)

        static Updater.Release ParseRelease(string json)
        {
            MethodInfo parse = typeof(Updater).GetMethod("Parse", Priv);
            return (Updater.Release)parse.Invoke(null, new object[] { json });
        }

        const string Repo = "https://github.com/rubenitx/stackshot/";

        public static void UpdaterParseGood()
        {
            Updater.Release r = ParseRelease("{\"tag_name\":\"v2.0.1\",\"html_url\":\"" + Repo + "releases/tag/v2.0.1\"," +
                "\"assets\":[{\"name\":\"Stackshot.exe\",\"browser_download_url\":\"" + Repo + "releases/download/v2.0.1/Stackshot.exe\",\"digest\":\"sha256:ABCDEF\"}," +
                "{\"name\":\"Stackshot.exe.sig\",\"browser_download_url\":\"" + Repo + "releases/download/v2.0.1/Stackshot.exe.sig\"}]," +
                "\"body\":\"### Cambios\\n- algo\"}");
            Check(r != null, "release parsed");
            Same("v2.0.1", r.Tag, "tag");
            Same("2.0.1", r.Version.ToString(3), "version");
            Same("abcdef", r.Sha256, "digest lowercased without prefix");
            Check(r.ExeUrl != null && r.SigUrl != null, "both assets found");
            Same(0, r.Notes.Count, "notes stop at ### heading");
        }

        static void Check(bool ok, string what) { if (!ok) Fail(what); }

        public static void UpdaterParseEdges()
        {
            Check(ParseRelease("{\"tag_name\":\"v2.0\"}").Version.Equals(new Version(2, 0, 0)), "v2.0 is 2.0.0");
            Check(ParseRelease("{\"tag_name\":\"V3.1.4\"}").Version.Equals(new Version(3, 1, 4)), "uppercase V accepted");
            Check(ParseRelease("{\"tag_name\":\"v2.0.1-rc1\"}") == null, "pre-release tag is not offered");
            Check(ParseRelease("{\"tag_name\":\"v2\"}") == null, "single-number tag refused");
            Check(ParseRelease("{\"html_url\":\"" + Repo + "x\"}") == null, "missing tag: no release");
            Check(ParseRelease("{\"tag_name\":\"\"}") == null, "empty tag: no release");
            Updater.Release foreign = ParseRelease("{\"tag_name\":\"v2.0.1\",\"html_url\":\"https://evil.example/x\"}");
            Same(Program.RepoUrl + "/releases", foreign.Page, "foreign page replaced by the repo releases page");
            Updater.Release hosts = ParseRelease("{\"tag_name\":\"v2.0.1\",\"assets\":[{\"name\":\"Stackshot.exe\"," +
                "\"browser_download_url\":\"https://evil.example/Stackshot.exe\",\"digest\":\"sha256:00\"}]}");
            Check(hosts.ExeUrl == null && hosts.Sha256 == null, "asset from another host ignored");
            Updater.Release sha1 = ParseRelease("{\"tag_name\":\"v2.0.1\",\"assets\":[{\"name\":\"Stackshot.exe\"," +
                "\"browser_download_url\":\"" + Repo + "releases/download/v2.0.1/Stackshot.exe\",\"digest\":\"sha1:00\"}]}");
            Check(sha1.ExeUrl != null && sha1.Sha256 == null, "non-sha256 digest is not used");
            Updater.Release notAray = ParseRelease("{\"tag_name\":\"v2.0.1\",\"assets\":{\"a\":1}}");
            Check(notAray.ExeUrl == null, "assets as an object: no asset, no crash");
            Updater.Release noBody = ParseRelease("{\"tag_name\":\"v2.0.1\"}");
            Same(0, noBody.Notes.Count, "no body, no notes");
        }

        public static void UpdaterNotes()
        {
            Updater.Release r = ParseRelease("{\"tag_name\":\"v2.0.1\",\"body\":\"- **Captura** nueva. M\u00E1s detalles aqu\u00ED\\n" +
                "- `c\u00F3digo` simple\\n- <b>html</b> tag\\nTexto suelto\\n- uno\\n- dos\\n- tres\\n- cuatro\"}");
            Same(5, r.Notes.Count, "at most five bullets");
            Same("Captura nueva.", r.Notes[0], "bold removed, first sentence only");
            Same("c\u00F3digo simple", r.Notes[1], "backticks removed");
            Same("html tag", r.Notes[2], "html removed");
        }

        public static void UpdaterDelays()
        {
            MethodInfo next = typeof(Updater).GetMethod("NextDelay");
            Func<int, TimeSpan> nd = delegate(int n) { return (TimeSpan)next.Invoke(null, new object[] { n }); };
            Same(180.0, nd(0).TotalMinutes, "no failures: every three hours");
            Same(180.0, nd(-3).TotalMinutes, "negative failures: every three hours");
            Same(15.0, nd(1).TotalMinutes, "one failure: 15 minutes");
            Same(30.0, nd(2).TotalMinutes, "two failures: 30 minutes");
            Same(60.0, nd(3).TotalMinutes, "three failures: 60 minutes");
            Same(180.0, nd(5).TotalMinutes, "five failures: capped at three hours");
            Same(180.0, nd(500).TotalMinutes, "many failures: still capped");

            DateTime now = DateTime.UtcNow;
            Same(false, Updater.DueOnWake(now, now.AddMinutes(-29), 0), "woke after 29 minutes: not due");
            Same(true, Updater.DueOnWake(now, now.AddMinutes(-30), 0), "woke after 30 minutes: due");
            Same(true, Updater.DueOnWake(now, now.AddMinutes(-1), 1), "a failed check: due at once");
            Same(true, Updater.DueOnWake(now, DateTime.MinValue, 0), "never checked: due");

            Same(false, Updater.ShouldAnnounce(null, null), "no release: nothing to announce");
            Updater.Release r = new Updater.Release { Tag = "v2.0.1" };
            Same(true, Updater.ShouldAnnounce(r, null), "new release announced");
            Same(false, Updater.ShouldAnnounce(r, "v2.0.1"), "same tag not announced twice");
            Same(true, Updater.ShouldAnnounce(r, "v2.0.0"), "other tag announced");
            Same(true, Updater.ShouldAnnounce(r, "V2.0.1"), "tag compare is case sensitive (documented)");

            Same("--install --updated", Updater.InstallArguments(false, false), "args: automatic, not at startup");
            Same("--install --startup --updated", Updater.InstallArguments(false, true), "args: startup");
            Same("--install --startup --updated --home", Updater.InstallArguments(true, true), "args: manual at startup");
        }

        public static void UpdaterLastChecked()
        {
            Settings s = new Settings();
            Same(DateTime.MinValue, Updater.LastChecked(null), "no settings: never");
            Same(DateTime.MinValue, Updater.LastChecked(s), "empty value: never");
            s.LastUpdateCheck = "no es una fecha";
            Same(DateTime.MinValue, Updater.LastChecked(s), "garbage value: never");
            DateTime utc = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
            s.LastUpdateCheck = utc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            Same(utc, Updater.LastChecked(s), "round trip of a UTC stamp");
        }

        public static void UpdaterVerify()
        {
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            string good = Sha256Hex(data);
            Check(Updater.Verify(data, new byte[10], null) != null, "no digest: refused");
            Check(Updater.Verify(data, new byte[10], "00" + good.Substring(2)) != null, "wrong digest: refused");
            Check(Updater.Verify(data, null, good) != null, "digest ok, no signature: refused");
            Check(Updater.Verify(data, new byte[10], good) != null, "digest ok, short garbage signature: refused");
            Check(Updater.Verify(data, new byte[256], good) != null, "digest ok, zero signature: refused");
            Check(Updater.Verify(new byte[0], null, Sha256Hex(new byte[0])) != null, "empty file without signature: refused");
        }

        // ---- Recents

        static void Stamp(string path, int bytes, DateTime written)
        {
            File.WriteAllBytes(path, new byte[bytes]);
            File.SetLastWriteTime(path, written);
        }

        static Settings RecentsFor(string save, string temp)
        {
            Recents.TempFolder = temp;
            Recents.Forget();
            Settings s = new Settings();
            s.SaveFolder = save;
            return s;
        }

        static HashSet<string> Names(List<FileInfo> list)
        {
            HashSet<string> r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo f in list) r.Add(f.Name);
            return r;
        }

        public static void RecentsMissingFolders()
        {
            string root = Folder("rec-missing");
            Settings s = RecentsFor(Path.Combine(root, "no-existe"), Path.Combine(root, "tampoco"));
            Same(0, Recents.Latest(s, 10).Count, "missing folders: nothing listed");
            int[] days = Recents.PerDay(s, 7);
            Same(7, days.Length, "per day has one bucket per day");
            foreach (int d in days) Same(0, d, "per day empty");
            Settings nul = RecentsFor(null, null);
            Same(0, Recents.Latest(nul, 10).Count, "null folders: nothing listed");
        }

        public static void RecentsKinds()
        {
            string save = Folder("rec-kinds");
            string temp = Folder("rec-kinds-temp");
            DateTime t = DateTime.Now.AddMinutes(-5);
            string[] names = { "a.png", "b.mkv", "c.ts", "d.wmv", "~grabando e.mkv", "f.MP4", "g.txt", "h.gif", "i.jpeg", "j.mov", "k.webm", "l.jpg", "m.md" };
            foreach (string n in names) Stamp(Path.Combine(save, n), 10, t);
            Settings s = RecentsFor(save, temp);
            HashSet<string> got = Names(Recents.Latest(s, 100));
            Check(got.Contains("a.png") && got.Contains("b.mkv") && got.Contains("f.MP4") && got.Contains("h.gif") &&
                  got.Contains("i.jpeg") && got.Contains("l.jpg"), "capture kinds listed (case-insensitive extension)");
            Check(!got.Contains("~grabando e.mkv"), "~ work files skipped");
            Check(!got.Contains("g.txt") && !got.Contains("m.md"), "other files skipped");
            Same(6, got.Count, "exactly the capture kinds (png jpg jpeg gif mp4 mkv)");
        }

        public static void RecentsDuplicateAndLimit()
        {
            string save = Folder("rec-dup");
            string temp = Folder("rec-dup-temp");
            DateTime now = DateTime.Now;
            Stamp(Path.Combine(save, "same.png"), 50, now.AddMinutes(-1));
            Stamp(Path.Combine(temp, "same.png"), 50, now.AddMinutes(-1));
            Stamp(Path.Combine(save, "newest.png"), 10, now);
            Stamp(Path.Combine(save, "old1.png"), 10, now.AddHours(-1));
            Stamp(Path.Combine(temp, "old2.png"), 10, now.AddHours(-2));
            Settings s = RecentsFor(save, temp);
            List<FileInfo> all = Recents.Latest(s, 100);
            Same(4, all.Count, "a kept capture in both folders is listed once");
            List<FileInfo> two = Recents.Latest(s, 2);
            Same(2, two.Count, "limit respected");
            Same("newest.png", two[0].Name, "newest first");
            Same("same.png", two[1].Name, "second newest next");
        }

        public static void RecentsPerDay()
        {
            string save = Folder("rec-day");
            string temp = Folder("rec-day-temp");
            DateTime today = DateTime.Today.AddHours(12);
            Stamp(Path.Combine(save, "hoy.png"), 1, today);
            Stamp(Path.Combine(save, "hoy2.png"), 1, today.AddMinutes(-30));
            Stamp(Path.Combine(save, "ayer.png"), 1, today.AddDays(-1));
            Stamp(Path.Combine(save, "hace7.png"), 1, today.AddDays(-6));
            Stamp(Path.Combine(save, "hace8.png"), 1, today.AddDays(-7));
            Stamp(Path.Combine(save, "futuro.png"), 1, today.AddDays(2));
            Settings s = RecentsFor(save, temp);
            int[] d = Recents.PerDay(s, 7);
            Same(2, d[6], "today has two");
            Same(1, d[5], "yesterday has one");
            Same(1, d[0], "six days ago is in the last seven");
            int sum = 0;
            foreach (int n in d) sum += n;
            Same(4, sum, "eight days ago and tomorrow are not counted");
        }

        public static void RecentsRenamedAndLocked()
        {
            string save = Folder("rec-rename");
            string temp = Folder("rec-rename-temp");
            string a = Path.Combine(save, "antes.png");
            Stamp(a, 10, DateTime.Now);
            Settings s = RecentsFor(save, temp);
            Same(1, Recents.Latest(s, 10).Count, "before rename");
            File.Move(a, Path.Combine(save, "despues.png"));
            Recents.Forget();
            List<FileInfo> after = Recents.Latest(s, 10);
            Same(1, after.Count, "after rename: one file");
            Same("despues.png", after[0].Name, "after rename: new name listed");

            string locked = Path.Combine(temp, "bloqueado.mkv");
            Stamp(locked, 10, DateTime.Now);
            using (FileStream hold = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Recents.Forget();
                HashSet<string> names = Names(Recents.Latest(s, 10));
                Check(names.Contains("bloqueado.mkv"), "a file open by another program is still listed");
            }
            File.Delete(Path.Combine(save, "despues.png"));
            Recents.Forget();
            Check(!Names(Recents.Latest(s, 10)).Contains("despues.png"), "a deleted file disappears after Forget");
        }

        static string Dec(double x) { return x.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture); }

        // Sizes shown next to the folder: each boundary.
        public static void RecentsSizeTexts()
        {
            Same("\u2026", Recents.Size(-1), "unknown");
            Same("Vac\u00EDa", Recents.Size(0), "empty");
            Same("1 KB", Recents.Size(1), "one byte: 1 KB");
            Same("1 KB", Recents.Size(1023), "1023 bytes: 1 KB");
            Same("10 MB", Recents.Size(10L * 1048576), "10 MB");
            Same(Dec(5) + " MB", Recents.Size(5L * 1048576), "5 MB shows one decimal");
            Same(Dec(1) + " GB", Recents.Size(1024L * 1048576), "1 GB");
            Same(Dec(2) + " GB", Recents.Size(2L * 1073741824), "2 GB");
        }

        // Known wrong texts, reproduced: values just under a unit boundary round up to "1024 KB" / "1024 MB".
        public static void RecentsSizeBoundary()
        {
            Same(Dec(1) + " MB", Recents.Size(1048575), "1 MB minus one byte should read 1.0 MB, not 1024 KB");
        }

        public static void RecentsSizeGigaBoundary()
        {
            Same(Dec(1) + " GB", Recents.Size(1073741823), "1 GB minus one byte should read 1.0 GB, not 1024 MB");
        }

        public static void RecentsThumbMissing()
        {
            string temp = Folder("rec-thumb");
            Recents.TempFolder = temp;
            bool called = false;
            Recents.Thumb(Path.Combine(temp, "no-existe.png"), 200, delegate { called = true; });
            Check(!called, "a missing file gives no thumbnail and no callback on the test thread");
        }

        // ---- FFmpeg pins (constants only; nothing is downloaded)

        static string FfConst(string name)
        {
            return (string)typeof(FfmpegSetup).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        public static void FfmpegPins()
        {
            string version = FfConst("Version"), zip = FfConst("Zip"), url = FfConst("Url"), sha = FfConst("Sha256");
            Same(zip, url.Substring(url.LastIndexOf('/') + 1), "zip name is the last part of the URL");
            Check(url.StartsWith("https://github.com/GyanD/codexffmpeg/releases/download/" + version + "/", StringComparison.Ordinal),
                  "URL is the release of the pinned version");
            Check(zip.Contains(version), "zip name contains the version");
            Same(64, sha.Length, "SHA-256 has 64 characters");
            foreach (char c in sha) Check((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), "SHA-256 is lowercase hex");
        }

        // ---- Tracked clipboard data

        public static void TrackedCountsAsPaste()
        {
            Same(false, TrackedData.CountsAsPaste(null, 5000), "no format: not a paste");
            Same(false, TrackedData.CountsAsPaste("CanIncludeInClipboardHistory", 5000), "Can* flags are not a paste");
            Same(false, TrackedData.CountsAsPaste("CanUploadToCloudClipboard", 5000), "cloud flag is not a paste");
            Same(false, TrackedData.CountsAsPaste("Bitmap", 500), "reads in the first second are the system");
            Same(false, TrackedData.CountsAsPaste("PNG", 1000), "exactly one second is not yet a paste");
            Same(true, TrackedData.CountsAsPaste("PNG", 1001), "after a second it is a paste");
        }

        public static void TrackedDeepCopy()
        {
            Bitmap src = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.FromArgb(255, 1, 2, 3));
            src.SetPixel(2, 1, Color.FromArgb(128, 9, 8, 7));
            Bitmap dst = TrackedData.DeepCopy(src);
            try
            {
                Same(PixelFormat.Format32bppArgb, dst.PixelFormat, "same 32 bit format");
                Same(Color.FromArgb(255, 1, 2, 3).ToArgb(), dst.GetPixel(0, 0).ToArgb(), "pixel copied");
                Same(Color.FromArgb(128, 9, 8, 7).ToArgb(), dst.GetPixel(2, 1).ToArgb(), "alpha pixel copied");
                src.SetPixel(0, 0, Color.Black);
                Same(Color.FromArgb(255, 1, 2, 3).ToArgb(), dst.GetPixel(0, 0).ToArgb(), "copy does not share pixels");
            }
            finally { src.Dispose(); dst.Dispose(); }
        }

        public static void TrackedDeepCopyOtherFormats()
        {
            Bitmap rgb = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            rgb.SetPixel(1, 1, Color.FromArgb(10, 20, 30));
            Bitmap copy = TrackedData.DeepCopy(rgb);
            try
            {
                Same(PixelFormat.Format32bppArgb, copy.PixelFormat, "24 bit copied as 32 bit ARGB");
                Same(4, copy.Width, "width kept");
                Same(Color.FromArgb(255, 10, 20, 30).ToArgb(), copy.GetPixel(1, 1).ToArgb(), "colour kept");
            }
            finally { rgb.Dispose(); copy.Dispose(); }

            Bitmap indexed = new Bitmap(5, 3, PixelFormat.Format8bppIndexed);
            Bitmap ic = TrackedData.DeepCopy(indexed);
            try { Same(PixelFormat.Format32bppArgb, ic.PixelFormat, "8 bit indexed copied as 32 bit"); Same(3, ic.Height, "height kept"); }
            finally { indexed.Dispose(); ic.Dispose(); }
        }

        public static void TrackedPasteCountedOnce()
        {
            string dir = Folder("tracked");
            Bitmap img = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            int used = 0;
            TrackedData d = new TrackedData(Path.Combine(dir, "captura.png"), img, true, false, delegate { used++; });
            try
            {
                Thread.Sleep(1200);
                d.GetData("CanIncludeInClipboardHistory", false);
                Same(0, used, "a Can* read is not a paste");
                d.GetData(DataFormats.Bitmap, true);
                d.GetData(DataFormats.Bitmap, true);
                Same(1, used, "paste counted once");
            }
            finally { d.Release(); }
        }

        // The file is not there (it was deleted before the clipboard asked): no hang, an empty PNG.
        public static void TrackedPngFromMissingFile()
        {
            string dir = Folder("tracked-missing");
            Bitmap img = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            TrackedData d = new TrackedData(Path.Combine(dir, "borrada.png"), img, false, true, null);
            try
            {
                DateTime start = DateTime.Now;
                object png = d.GetData("PNG", false);
                Check((DateTime.Now - start).TotalSeconds < 5, "reading PNG did not wait for ten seconds");
                Check(png != null, "PNG stream returned");
            }
            finally { d.Release(); }
        }

        public static void TrackedReleaseEarly()
        {
            string dir = Folder("tracked-release");
            Bitmap img = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            TrackedData.TestPngDelayMs = 300;
            try
            {
                TrackedData d = new TrackedData(Path.Combine(dir, "x.png"), img, true, false, null);
                d.Release();
                d.Release();
                Thread.Sleep(600);
            }
            finally { TrackedData.TestPngDelayMs = 0; }
        }

        // ---- Setup: deleting the data folder (private folders only)

        public static void SetupSaveFolderIsDataFolder()
        {
            string data = Folder("uninstall-data");
            string saved = Path.Combine(data, "Capturas");
            Directory.CreateDirectory(saved);
            File.WriteAllText(Path.Combine(saved, "mine.png"), "x");
            File.WriteAllText(Path.Combine(data, "loose.png"), "x");
            File.WriteAllText(Path.Combine(data, "settings.ini"), "x");
            MethodInfo del = typeof(Installer).GetMethod("DeleteData", Priv);
            del.Invoke(null, new object[] { data, data });
            Check(File.Exists(Path.Combine(saved, "mine.png")), "a capture in a subfolder of the save folder kept");
        }

        public static void SetupSaveFolderIsDataFolderLoose()
        {
            string data = Folder("uninstall-loose");
            File.WriteAllText(Path.Combine(data, "loose.png"), "x");
            File.WriteAllText(Path.Combine(data, "settings.ini"), "x");
            MethodInfo del = typeof(Installer).GetMethod("DeleteData", Priv);
            del.Invoke(null, new object[] { data, data });
            Check(File.Exists(Path.Combine(data, "loose.png")), "a capture at the top of the save folder kept");
        }

        // The save folder inside a sibling of the data folder: the data folder goes.
        public static void SetupSaveFolderOutside()
        {
            string root = Folder("uninstall-outside");
            string data = Path.Combine(root, "Stackshot");
            string pictures = Path.Combine(root, "Pictures", "Stackshot");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(Path.Combine(data, "temp"));
            Directory.CreateDirectory(pictures);
            File.WriteAllText(Path.Combine(pictures, "keep.png"), "x");
            MethodInfo del = typeof(Installer).GetMethod("DeleteData", Priv);
            bool kept = (bool)del.Invoke(null, new object[] { data, pictures });
            Check(!kept, "nothing recovered");
            Check(!Directory.Exists(data), "data folder gone");
            Check(File.Exists(Path.Combine(pictures, "keep.png")), "saved capture outside kept");
        }

        // The downloaded exe must report a newer version than this build and the published one: this test exe is not.
        public static void UpdaterMatchesRefusesOther()
        {
            MethodInfo matches = typeof(Updater).GetMethod("Matches", Priv);
            Updater.Release r = new Updater.Release { Version = new Version(2, 0, 1), Tag = "v2.0.1" };
            string msg = (string)matches.Invoke(null, new object[] { Installer.ExePath, r });
            Check(msg != null, "this build is not a newer published version: refused");
        }

        public static void AssocCommand()
        {
            string cmd = FileAssoc.Command;
            Check(cmd.StartsWith("\"" + Installer.ExePath + "\" "), "command starts with the quoted exe path");
            Check(cmd.EndsWith(" \"%1\""), "command passes the file as %1 in quotes");
            Same(".md", FileAssoc.Markdown.Exts[0], "markdown extension first");
            Same("Stackshot.Markdown", FileAssoc.Markdown.ProgId, "markdown progid");
        }

        // ---- Text checks

        public static void TextsAccentsAndWords()
        {
            Same("Abrir v\u00EDdeo\u2026", "Abrir v\u00EDdeo\u2026", "ellipsis");
            Check(ShotStack.VideoFilter.StartsWith("V\u00EDdeos|", StringComparison.Ordinal), "video filter title");
            Check(ShotStack.VideoFilter.EndsWith("|Todos los archivos|*.*", StringComparison.Ordinal), "video filter catch-all");
            Check(ShotStack.VideoFilter.Contains("*.mkv") && ShotStack.VideoFilter.Contains("*.ts"), "video filter has mkv and ts");
            Check(ShotStack.IsVideoFile("clip.TS") && ShotStack.IsVideoFile("clip.wmv") && ShotStack.IsVideoFile("clip.mkv"), "ts, wmv, mkv are videos");
            Check(!ShotStack.IsVideoFile("nota.md") && !ShotStack.IsVideoFile(null), "md and null are not videos");
            Check(ShotStack.IsMediaFile("x.gif") && !ShotStack.IsMediaFile("x.ts"), "media kinds for captures");
        }

        public static void SuiteSettings()
        {
            AppTests.Test("hunt-settings", "every key round trips", SettingsRoundTrip);
            AppTests.Test("hunt-settings", "backup kept and used when the file breaks", SettingsBackupFallback);
            AppTests.Test("hunt-settings", "missing and garbage files give the defaults", SettingsMissingAndGarbage);
            AppTests.Test("hunt-settings", "values clamped or refused as documented", SettingsParseRules);
            AppTests.Test("hunt-settings", "countdown rounding and hotkey mapping", SettingsCountdownAndHotkeys);
        }

        public static void SuiteUpdater()
        {
            AppTests.Test("hunt-updater", "good release parsed", UpdaterParseGood);
            AppTests.Test("hunt-updater", "malformed and foreign release data", UpdaterParseEdges);
            AppTests.Test("hunt-updater", "notes: five bullets, markdown removed", UpdaterNotes);
            AppTests.Test("hunt-updater", "delays, wake rules, announce once, install arguments", UpdaterDelays);
            AppTests.Test("hunt-updater", "last check stamp", UpdaterLastChecked);
            AppTests.Test("hunt-updater", "verify refuses bad digest and bad signature", UpdaterVerify);
            AppTests.Test("hunt-updater", "a build that is not newer is refused by the file check", UpdaterMatchesRefusesOther);
        }

        public static void SuiteRecents()
        {
            AppTests.Test("hunt-recents", "missing and null folders", RecentsMissingFolders);
            AppTests.Test("hunt-recents", "only capture kinds listed", RecentsKinds);
            AppTests.Test("hunt-recents", "duplicates in two folders, newest first, limit", RecentsDuplicateAndLimit);
            AppTests.Test("hunt-recents", "per day buckets", RecentsPerDay);
            AppTests.Test("hunt-recents", "renamed, locked and deleted files", RecentsRenamedAndLocked);
            AppTests.Test("hunt-recents", "size texts", RecentsSizeTexts);
            AppTests.Test("hunt-recents", "size one byte under 1 MB", RecentsSizeBoundary);
            AppTests.Test("hunt-recents", "size one byte under 1 GB", RecentsSizeGigaBoundary);
            AppTests.Test("hunt-recents", "thumbnail of a missing file", RecentsThumbMissing);
        }

        public static void SuiteFfmpeg()
        {
            AppTests.Test("hunt-ffmpeg", "pinned version, URL, zip name and hash shape", FfmpegPins);
        }

        public static void SuiteTracked()
        {
            AppTests.Test("hunt-tracked", "which reads count as a paste", TrackedCountsAsPaste);
            AppTests.Test("hunt-tracked", "deep copy of 32 bit", TrackedDeepCopy);
            AppTests.Test("hunt-tracked", "deep copy of other formats", TrackedDeepCopyOtherFormats);
            AppTests.Test("hunt-tracked", "paste counted once", TrackedPasteCountedOnce);
            AppTests.Test("hunt-tracked", "PNG from a missing file does not hang", TrackedPngFromMissingFile);
            AppTests.Test("hunt-tracked", "release before the PNG is ready", TrackedReleaseEarly);
        }

        public static void SuiteSetup()
        {
            AppTests.Test("hunt-setup", "save folder equal to the data folder survives uninstall", SetupSaveFolderIsDataFolder);
            AppTests.Test("hunt-setup", "capture at the top of the save folder survives uninstall", SetupSaveFolderIsDataFolderLoose);
            AppTests.Test("hunt-setup", "save folder outside the data folder survives uninstall", SetupSaveFolderOutside);
            AppTests.Test("hunt-setup", "video filter and kind helpers", TextsAccentsAndWords);
            AppTests.Test("hunt-setup", "file association command line", AssocCommand);
        }
    }
}
