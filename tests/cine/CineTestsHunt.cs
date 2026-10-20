// Hunt tests by an independent tester: the take options window (RecordSetup) and the countdown window built headless,
// Settings parsing and writing on private copies only, disk estimate edges, and the live encode plans of the
// Estandar/Alta/Maxima qualities and GIF. Run with: run-tests.ps1 hunt. A failure is a product bug unless its detail says
// that the test itself is wrong.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Stackshot.Tests
{
    static partial class CineTests
    {
        static Rectangle HuntArea() { return new Rectangle(40, 300, 640, 360); }

        static FieldInfo HuntFieldInfo(Type t, string name)
        {
            FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            Check(f != null, "no encuentro el campo " + name);
            return f;
        }

        static object HuntGet(object o, string name) { return HuntFieldInfo(o.GetType(), name).GetValue(o); }

        static void HuntSet(object o, string name, object value) { HuntFieldInfo(o.GetType(), name).SetValue(o, value); }

        static object HuntCall(object o, string name, params object[] args)
        {
            MethodInfo m = o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Check(m != null, "no encuentro el metodo " + name);
            try { return m.Invoke(o, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        static object HuntCallStatic(Type t, string name, params object[] args)
        {
            MethodInfo m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Check(m != null, "no encuentro el metodo estatico " + name);
            try { return m.Invoke(null, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        static RecordSetup HuntSetup(Settings s, bool gif)
        {
            ConstructorInfo c = typeof(RecordSetup).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new Type[] { typeof(Settings), typeof(Rectangle), typeof(bool) }, null);
            Check(c != null, "no encuentro el constructor de RecordSetup");
            try { return (RecordSetup)c.Invoke(new object[] { s, HuntArea(), gif }); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        static void HuntIni(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        static Settings HuntLoadText(string dir, string text)
        {
            string path = Path.Combine(dir, "t" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".ini");
            HuntIni(path, text);
            return Settings.Load(path);
        }

        static void HuntSuite()
        {
            Test("hunt", "settings-every-field-roundtrips", SettingsRoundtrip);
            Test("hunt", "settings-garbage-never-throws", SettingsGarbage);
            Test("hunt", "settings-clamps-and-defaults", SettingsClamps);
            Test("hunt", "settings-overflow-clamps-like-other-values", SettingsOverflow);
            Test("hunt", "settings-bom-crlf-tabs-and-accents", SettingsBomCrlf);
            Test("hunt", "settings-utf16-file", SettingsUtf16);
            Test("hunt", "settings-ansi-file-accented-path", SettingsAnsi);
            Test("hunt", "settings-duplicate-keys-last-wins", SettingsDuplicates);
            Test("hunt", "settings-write-blocks-line-injection", SettingsInjection);
            Test("hunt", "settings-mascot-name-keeps-valid-text", SettingsSurrogate);
            Test("hunt", "settings-save-folder-forms", SettingsFolders);
            Test("hunt", "settings-missing-keys-keep-defaults", SettingsMissing);
            Test("hunt", "settings-zeroed-file-uses-backup", SettingsBackup);
            Test("hunt", "setup-constructor-writes-nothing", SetupNothing);
            Test("hunt", "setup-accept-keeps-saved-choices", SetupKeeps);
            Test("hunt", "setup-accept-each-quality", SetupEachQuality);
            Test("hunt", "setup-cine-fps-choice-is-stored", SetupCineFps);
            Test("hunt", "setup-gif-accept-touches-only-gif-keys", SetupGif);
            Test("hunt", "setup-accept-is-idempotent", SetupTwice);
            Test("hunt", "setup-countdown-choice-stored", SetupCountdown);
            Test("hunt", "setup-video-fps-shown-equals-recorded", SetupVideoFpsShown);
            Test("hunt", "setup-gif-fps-shown-equals-recorded", SetupGifFpsShown);
            Test("hunt", "countdown-done-after-its-seconds", CountdownDone);
            Test("hunt", "countdown-minimum-one-second", CountdownMinimum);
            Test("hunt", "countdown-centered-on-area", CountdownCentered);
            Test("hunt", "countdown-never-activates", CountdownNoActivate);
            Test("hunt", "countdown-cancel-fires-once", CountdownCancel);
            Test("hunt", "countdown-first-frame-shows-full-count", CountdownFirstFrame);
            Test("hunt", "disk-estimate-order-by-quality", DiskOrder);
            Test("hunt", "disk-estimate-no-room-and-negative-free", DiskNoRoom);
            Test("hunt", "disk-minutes-left-edges", DiskMinutesLeft);
            Test("hunt", "disk-texts-edges", DiskTexts);
            Test("hunt", "gif-target-fps-and-args", GifArgsCheck);
            Test("hunt", "standard-no-sound-writes-mp4-directly", StandardDirect);
            Test("hunt", "maxima-small-area-live-is-lossless", MaximaSmall);
            Test("hunt", "maxima-large-area-text-is-honest", MaximaLarge);
        }

        // ---- Settings (private copies only; Settings.ReadOnly is set, Save never writes) ----

        static Dictionary<string, object> HuntValues()
        {
            Dictionary<string, object> v = new Dictionary<string, object>();
            v["SaveFolder"] = "C:\\Fotos \u00D1and\u00FA\\Stackshot \u65E5\u672C";
            v["Sound"] = false; v["CopyToClipboard"] = false; v["FollowMouse"] = false; v["StackTuck"] = 3; v["FirstRunDone"] = true;
            v["HotRegion"] = "Ctrl+Shift+S, F1"; v["HotScreen"] = "Ctrl+Alt+S"; v["HotWindow"] = "Alt+F2";
            v["HotScroll"] = "Ctrl+Alt+F3"; v["HotVideo"] = "Shift+F4"; v["HotGif"] = "Ctrl+F5"; v["HotMarkdown"] = "Ctrl+Shift+Alt+N"; v["HotText"] = "Ctrl+Alt+F9";
            v["HotRepeat"] = "Ctrl+Shift+Alt+F10"; v["HotDelayed"] = "Ctrl+Shift+Alt+F12"; v["CaptureDelay"] = 5; v["KeysFirst"] = "Alt+F9";
            v["VideoFps"] = 24; v["GifFps"] = 20; v["VideoQuality"] = 3; v["RecordSystemAudio"] = true; v["RecordMic"] = true;
            v["MicDevice"] = "{0.0.1.00000000}.{ab-cd}"; v["SystemAudioDevice"] = "{0.0.0.00000000}.{xy}";
            v["Webcam"] = 2; v["WebcamSize"] = 2; v["WebcamDevice"] = "C\u00E1mara HD"; v["Ffmpeg"] = "C:\\Tools\\ffmpeg.exe";
            v["BgAuto"] = true; v["BgPreset"] = 3; v["BgPadding"] = 20; v["BgRadius"] = 7; v["BgShadow"] = false; v["BgRatio"] = "4:3";
            v["BgKind"] = 2; v["BgColor"] = 0xFEDCBA; v["BgBlur"] = 1; v["BgShadowSize"] = 12; v["BgInset"] = 40; v["BgBalance"] = true; v["BgAlign"] = 2; v["BgStyles"] = "2,0,16702650,1,20,7,0,12,40,1,2,4:3";
            v["Appearance"] = 2; v["AllInOne"] = true; v["AdjustFirst"] = true; v["ShowRecent"] = false; v["TempKeep"] = 4; v["HomeStyle"] = 2;
            v["CloseToTray"] = false; v["Profile"] = 0; v["ShowIntro"] = false; v["MascotOn"] = false; v["MascotName"] = "\u00D1u";
            v["MascotColor"] = 5; v["MascotTalks"] = false; v["MascotKind"] = 2; v["MascotHat"] = 3; v["MascotOutfit"] = 4;
            v["MascotFace"] = 1; v["MascotEyes"] = 2; v["MascotPersonality"] = 1; v["MascotSeasonal"] = false; v["MascotDesktop"] = true;
            v["MascotClimb"] = false; v["MascotAllScreens"] = false; v["MascotSpot"] = 300; v["MascotLove"] = 4242;
            v["MascotFavorites"] = "1:2,3:4"; v["MascotLooks"] = "abc|def"; v["MascotAccessory"] = 5; v["MascotEnergy"] = 0;
            v["MascotChatter"] = 2; v["MascotAlone"] = 9; v["MascotSleepIdle"] = false; v["MascotRhythm"] = false; v["CheckUpdates"] = false;
            v["LastUpdateCheck"] = "2026-10-09"; v["AutoUpdate"] = true; v["UpdateNotified"] = "v2.0.0";
            v["CineFps"] = 30; v["CinePeakMemory"] = 1; v["RecordSetup"] = false; v["RecordCountdown"] = 10;
            v["PlayerOpensVideos"] = false; v["PlayerScaling"] = 1; v["PlayerVolume"] = 42; v["PlayerLoop"] = true; v["ExportQuality"] = 4; v["PlayerFill"] = true;
            v["XmlWatch"] = true; v["XmlWatchFolder"] = @"E:\xml vigilados";
            v["CaptureCursor"] = true; v["FileNameTemplate"] = "{tipo}_{fecha}_{n}"; v["SaveSubfolder"] = 1; v["SensitiveAuto"] = true; v["HistorySearch"] = false;
            v["RecordClicks"] = true; v["RecordKeys"] = true;
            v["SaveAll"] = false; v["MascotHideFull"] = false; v["NewsSeen"] = "2.0.0"; v["ScrollGuides"] = 3;
            v["WellOn"] = false; v["WellWater"] = 45; v["WellEyes"] = 60; v["WellMove"] = 20; v["WellBreathe"] = 30; v["WellToasts"] = true;
            v["WellDay"] = "2026-10-13"; v["WellDone"] = "4,3,2,1";
            v["FocusWork"] = 40; v["FocusShort"] = 7; v["FocusLong"] = 20; v["FocusRounds"] = 6; v["FocusAuto"] = true; v["FocusSound"] = false;
            v["FocusNotices"] = false; v["FocusDay"] = "2026-10-13"; v["FocusBlocks"] = 3; v["FocusMinutes"] = 90;
            v["WaterGoal"] = 6; v["SummaryHour"] = 19; v["SummaryMinute"] = 45; v["StepsGoal"] = 12000; v["SummaryParts"] = 5; v["SummaryDay"] = "2026-10-12";
            v["XmlFaultAlert"] = false; v["FitbitAsked"] = "2026-10-20"; v["HealthTalk"] = false; v["HealthSaid"] = "2026-10-13|x";
            v["FitbitLast"] = "1|2|3|4|5|6"; v["ShortSleepDay"] = "2026-10-11"; v["StatsDay"] = "2026-10-13"; v["StatsCaptures"] = 99;
            return v;
        }

        static void SettingsRoundtrip()
        {
            string dir = WorkDir("hunt-roundtrip");
            try
            {
                Dictionary<string, object> v = HuntValues();
                Settings s = new Settings();
                List<string> untested = new List<string>();
                int set = 0;
                foreach (FieldInfo f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!v.ContainsKey(f.Name)) { untested.Add(f.Name); continue; }
                    f.SetValue(s, v[f.Name]);
                    set++;
                }
                Check(untested.Count == 0, "sin valor de prueba: " + string.Join(", ", untested.ToArray()));
                string path = Path.Combine(dir, "settings.ini");
                MethodInfo write = typeof(Settings).GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance);
                Check(write != null, "no encuentro Settings.Write");
                write.Invoke(s, new object[] { path });
                Settings r = Settings.Load(path);
                List<string> diff = new List<string>();
                foreach (FieldInfo f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object a = f.GetValue(s), b = f.GetValue(r);
                    if (!object.Equals(a, b)) diff.Add(f.Name + " " + a + " -> " + b);
                }
                Check(diff.Count == 0, "no vuelven igual: " + string.Join("; ", diff.ToArray()));
                Note(set + " campos ida y vuelta");
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsGarbage()
        {
            string dir = WorkDir("hunt-garbage");
            try
            {
                Random rnd = new Random(20261010);
                string file = Path.Combine(dir, "g.ini");
                for (int i = 0; i < 300; i++)
                {
                    byte[] bytes = new byte[rnd.Next(0, 3000)];
                    rnd.NextBytes(bytes);
                    File.WriteAllBytes(file, bytes);
                    Settings s = Settings.Load(file);
                    Check(s != null && s.VideoFps >= 5 && s.VideoFps <= 60 && s.GifFps >= 5 && s.GifFps <= 30 && s.VideoQuality >= 0 && s.VideoQuality <= 3,
                          "valor fuera de rango con basura " + i);
                }
                string[] hostile = { "VideoFps = ", "VideoFps = =", "=", "==", "VideoFps", "\t\t", "# VideoFps = 5", "; GifFps = 6", "VideoFps=5=6", "SaveFolder = \"", "SaveFolder = \"\"", "Ffmpeg = \"" };
                foreach (string line in hostile)
                {
                    Settings s = HuntLoadText(dir, line + "\r\n");
                    Check(s.VideoFps == 30 && s.GifFps == 15, "linea rara cambia valores: [" + line + "] -> " + s.VideoFps + "/" + s.GifFps);
                }
                Note("300 ficheros aleatorios y " + hostile.Length + " lineas raras sin excepciones");
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsClamps()
        {
            string dir = WorkDir("hunt-clamps");
            try
            {
                Settings d = new Settings();
                string[,] table = {
                    { "VideoFps = 1000", "VideoFps", "60" }, { "VideoFps = -1", "VideoFps", "5" }, { "VideoFps = 0", "VideoFps", "5" }, { "VideoFps = 45", "VideoFps", "45" },
                    { "GifFps = 0", "GifFps", "5" }, { "GifFps = 31", "GifFps", "30" },
                    { "VideoQuality = 4", "VideoQuality", "3" }, { "VideoQuality = -2", "VideoQuality", "0" },
                    { "Webcam = 5", "Webcam", "2" }, { "WebcamSize = -1", "WebcamSize", "0" },
                    { "Appearance = 9", "Appearance", "2" }, { "TempKeep = 9", "TempKeep", "4" }, { "HomeStyle = 7", "HomeStyle", "2" },
                    { "Profile = 5", "Profile", "2" }, { "BgPreset = 150", "BgPreset", "99" }, { "BgPadding = 101", "BgPadding", "100" },
                    { "BgRadius = -1", "BgRadius", "0" }, { "BgRatio = 2:1", "BgRatio", "auto" }, { "BgRatio = 16:9 ", "BgRatio", "16:9" },
                    { "PlayerVolume = -5", "PlayerVolume", "0" }, { "PlayerVolume = 150", "PlayerVolume", "100" },
                    { "PlayerScaling = 2", "PlayerScaling", "1" }, { "ExportQuality = 9", "ExportQuality", "4" },
                    { "CinePeakMemory = 2", "CinePeakMemory", "1" },
                    { "CineFps = 0", "CineFps", "30" }, { "CineFps = -100", "CineFps", "30" }, { "CineFps = 44", "CineFps", "30" },
                    { "CineFps = 45", "CineFps", "60" }, { "CineFps = 1000", "CineFps", "60" }, { "CineFps = 5000", "CineFps", "60" },
                    { "RecordCountdown = -4", "RecordCountdown", "0" }, { "RecordCountdown = 4", "RecordCountdown", "3" },
                    { "RecordCountdown = 7", "RecordCountdown", "5" }, { "RecordCountdown = 8", "RecordCountdown", "10" },
                    { "RecordCountdown = 60", "RecordCountdown", "10" },
                    { "MascotSpot = 5000", "MascotSpot", "1000" }, { "MascotLove = -3", "MascotLove", "0" },
                    { "MascotEnergy = 3", "MascotEnergy", "2" }, { "MascotAlone = 99", "MascotAlone", "15" },
                    { "Sound = no", "Sound", "False" }, { "Sound = maybe", "Sound", "True" }, { "Sound = 2", "Sound", "True" },
                    { "PlayerLoop = TRUE", "PlayerLoop", "True" }, { "RecordSetup = 0", "RecordSetup", "False" },
                    { "MascotName = ", "MascotName", "Pixel" }, { "MascotName = 0123456789ABCDEFXYZ", "MascotName", "0123456789ABCDEF" },
                    { "Ffmpeg = \"C:\\a b\\ffmpeg.exe\"", "Ffmpeg", "C:\\a b\\ffmpeg.exe" },
                    { "SaveFolder = \"relativa\"", "SaveFolder", d.SaveFolder },
                };
                List<string> problems = new List<string>();
                for (int i = 0; i < table.GetLength(0); i++)
                {
                    Settings s = HuntLoadText(dir, table[i, 0] + "\r\n");
                    object got = HuntGet(s, table[i, 1]);
                    string want = table[i, 2];
                    if (!object.Equals(got.ToString(), want)) problems.Add(table[i, 0] + " -> " + got + " (esperado " + want + ")");
                }
                Check(problems.Count == 0, string.Join("; ", problems.ToArray()));
                Note(table.GetLength(0) + " lineas comprobadas");
            }
            finally { DeleteDir(dir); }
        }

        // A huge number that does not fit in an int: the other out-of-range values are clamped to the limit, so this should be too.
        static void SettingsOverflow()
        {
            string dir = WorkDir("hunt-overflow");
            try
            {
                string[] lines = { "GifFps = 99999999999", "VideoFps = 4294967296" };
                string[] fields = { "GifFps", "VideoFps" };
                string[] want = { "30", "60" };
                List<string> problems = new List<string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    Settings s = HuntLoadText(dir, lines[i] + "\r\n");
                    object got = HuntGet(s, fields[i]);
                    if (!object.Equals(got.ToString(), want[i])) problems.Add(lines[i] + " -> " + got + " (esperado " + want[i] + ", como 1000 -> limite)");
                }
                Check(problems.Count == 0, string.Join("; ", problems.ToArray()));
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsBomCrlf()
        {
            string dir = WorkDir("hunt-bom");
            try
            {
                string text = "VideoFps = 45\r\nSaveFolder = C:\\Fotos A\u00F1o \u00F1\r\n\r\n  # comentario\r\nMascotName =  Ana \r\nGifFps\t=\t12\r\n";
                byte[] bom = new UTF8Encoding(true).GetPreamble();
                byte[] body = new UTF8Encoding(false).GetBytes(text);
                byte[] all = new byte[bom.Length + body.Length];
                Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
                Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
                string path = Path.Combine(dir, "bom.ini");
                File.WriteAllBytes(path, all);
                Settings s = Settings.Load(path);
                Check(s.VideoFps == 45, "primera linea con BOM: VideoFps " + s.VideoFps);
                Check(s.SaveFolder == "C:\\Fotos A\u00F1o \u00F1", "ruta con acentos: " + s.SaveFolder);
                Check(s.MascotName == "Ana", "valor con espacios: [" + s.MascotName + "]");
                Check(s.GifFps == 12, "separador tabulador: GifFps " + s.GifFps);
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsUtf16()
        {
            string dir = WorkDir("hunt-utf16");
            try
            {
                string path = Path.Combine(dir, "u16.ini");
                File.WriteAllText(path, "VideoFps = 45\r\nRecordCountdown = 5\r\n", Encoding.Unicode);
                Settings s = Settings.Load(path);
                Check(s.VideoFps == 45 && s.RecordCountdown == 5, "UTF-16 con BOM: " + s.VideoFps + "/" + s.RecordCountdown);
            }
            finally { DeleteDir(dir); }
        }

        // A settings.ini saved as ANSI (Windows-1252) by an older editor or by hand: the accented path must survive.
        static void SettingsAnsi()
        {
            string dir = WorkDir("hunt-ansi");
            try
            {
                string path = Path.Combine(dir, "ansi.ini");
                byte[] bytes = Encoding.GetEncoding(1252).GetBytes("VideoFps = 45\r\nSaveFolder = C:\\Fotos A\u00F1o\r\n");
                File.WriteAllBytes(path, bytes);
                Settings s = Settings.Load(path);
                Check(s.VideoFps == 45, "VideoFps " + s.VideoFps);
                Check(s.SaveFolder == "C:\\Fotos A\u00F1o", "ruta leida: [" + s.SaveFolder + "] (ANSI se lee como UTF-8)");
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsDuplicates()
        {
            Settings s = HuntLoadText(WorkDir("hunt-dup"), "VideoFps = 24\r\nVideoFps = 60\r\nRecordCountdown = 3\r\nRecordCountdown = 0\r\n");
            Check(s.VideoFps == 60 && s.RecordCountdown == 0, "duplicados: " + s.VideoFps + "/" + s.RecordCountdown);
        }

        static void SettingsInjection()
        {
            string dir = WorkDir("hunt-inject");
            try
            {
                Settings s = new Settings();
                s.SaveFolder = "C:\\Fotos\r\nFfmpeg = C:\\evil\\ffmpeg.exe";
                s.MascotName = "Ana\nSound = 0";
                s.Ffmpeg = "";
                string path = Path.Combine(dir, "inj.ini");
                MethodInfo write = typeof(Settings).GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance);
                write.Invoke(s, new object[] { path });
                Settings r = Settings.Load(path);
                Check(r.Ffmpeg == "", "una ruta con salto de linea inyecta Ffmpeg: " + r.Ffmpeg);
                Check(r.Sound == true, "un salto de linea en el nombre cambia Sound");
            }
            finally { DeleteDir(dir); }
        }

        // The name is cut to 16 characters on load; a cut between the two halves of an emoji leaves half of it.
        static void SettingsSurrogate()
        {
            string dir = WorkDir("hunt-surrogate");
            try
            {
                string name = "ABCDEFGHIJKLMNO\uD83D\uDE00X";
                string path = Path.Combine(dir, "sur.ini");
                HuntIni(path, "MascotName = " + name + "\r\n");
                Settings s = Settings.Load(path);
                char last = s.MascotName[s.MascotName.Length - 1];
                Check(!char.IsHighSurrogate(last), "el nombre termina en una mitad de emoji: [" + s.MascotName + "] (" + s.MascotName.Length + " caracteres)");
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsFolders()
        {
            string dir = WorkDir("hunt-folders");
            try
            {
                Settings d = new Settings();
                string profile = Environment.GetEnvironmentVariable("USERPROFILE");
                Check(HuntLoadText(dir, "SaveFolder = \"%USERPROFILE%\\Pics\"\r\n").SaveFolder == Path.Combine(profile, "Pics"), "entre comillas con variable");
                Check(HuntLoadText(dir, "SaveFolder = Pics\r\n").SaveFolder == d.SaveFolder, "ruta relativa debe volver al predeterminado");
                Check(HuntLoadText(dir, "SaveFolder = C:Pics\r\n").SaveFolder == d.SaveFolder, "C:Pics debe volver al predeterminado");
                Check(HuntLoadText(dir, "SaveFolder = \\\\server\\share\\Fotos\r\n").SaveFolder == "\\\\server\\share\\Fotos", "UNC");
                Check(HuntLoadText(dir, "SaveFolder = C:\\Fotos\\..\\Otra\r\n").SaveFolder == "C:\\Otra", "normaliza ..");
                Check(HuntLoadText(dir, "SaveFolder = C:\\bad<name>\r\n").SaveFolder == d.SaveFolder, "caracteres invalidos deben volver al predeterminado");
                Check(HuntLoadText(dir, "SaveFolder = \r\n").SaveFolder == d.SaveFolder, "valor vacio");
            }
            finally { DeleteDir(dir); }
        }

        static void SettingsMissing()
        {
            Settings loaded = HuntLoadText(WorkDir("hunt-missing"), "VideoFps = 45\r\n");
            Settings d = new Settings();
            Check(loaded.VideoFps == 45, "VideoFps");
            Check(loaded.GifFps == d.GifFps && loaded.Sound == d.Sound && loaded.MascotName == d.MascotName && loaded.RecordCountdown == d.RecordCountdown,
                  "claves ausentes no quedan en su valor por defecto");
        }

        static void SettingsBackup()
        {
            string dir = WorkDir("hunt-backup");
            try
            {
                string path = Path.Combine(dir, "settings.ini");
                File.WriteAllBytes(path, new byte[512]);
                HuntIni(path + ".bak", "VideoFps = 45\r\nRecordCountdown = 10\r\n");
                Settings s = Settings.Load(path);
                Check(s.VideoFps == 45 && s.RecordCountdown == 10, "no usa la copia .bak: " + s.VideoFps + "/" + s.RecordCountdown);
            }
            finally { DeleteDir(dir); }
        }

        // ---- RecordSetup (the take options window, never shown; Accept only changes the settings object) ----

        static void SetupNothing()
        {
            Settings s = new Settings();
            s.VideoQuality = Recorder.Cinema; s.RecordSetup = true;
            RecordSetup w = HuntSetup(s, false);
            Check(s.VideoQuality == Recorder.Cinema && s.RecordSetup, "el constructor cambia los ajustes");
            HuntCall(w, "Close");
        }

        static void SetupKeeps()
        {
            Settings s = new Settings();
            s.VideoQuality = Recorder.High; s.VideoFps = 60; s.CineFps = 30; s.RecordSystemAudio = true; s.RecordMic = false;
            s.Webcam = 1; s.RecordCountdown = 5; s.RecordSetup = true; s.GifFps = 20;
            RecordSetup w = HuntSetup(s, false);
            HuntCall(w, "Accept");
            Check(s.VideoQuality == Recorder.High && s.VideoFps == 60 && s.CineFps == 30, "calidad/fps: " + s.VideoQuality + "/" + s.VideoFps + "/" + s.CineFps);
            Check(s.RecordSystemAudio && !s.RecordMic && s.Webcam == 1, "sonido/microfono/camara");
            Check(s.RecordCountdown == 5 && s.RecordSetup && s.GifFps == 20, "cuenta atras/preguntar/GIF");
        }

        static void SetupEachQuality()
        {
            int[] qualities = { Recorder.Standard, Recorder.High, Recorder.Maximum, Recorder.Cinema };
            foreach (int q in qualities)
            {
                Settings s = new Settings();
                s.VideoQuality = q; s.VideoFps = 24; s.CineFps = 30;
                RecordSetup w = HuntSetup(s, false);
                Check(HuntGet(w, "quality").Equals(q), "calidad mostrada " + HuntGet(w, "quality") + " para " + q);
                HuntCall(w, "Accept");
                Check(s.VideoQuality == q, "calidad guardada " + s.VideoQuality + " para " + q);
            }
        }

        static void SetupCineFps()
        {
            Settings s = new Settings();
            s.VideoQuality = Recorder.Cinema; s.CineFps = 30;
            RecordSetup w = HuntSetup(s, false);
            HuntCall(w, "Accept");
            Check(s.CineFps == 30 && s.VideoQuality == Recorder.Cinema, "Cine a 30 no se guarda: " + s.CineFps);
            Settings t = new Settings();
            t.VideoQuality = Recorder.Cinema; t.CineFps = 60;
            RecordSetup w2 = HuntSetup(t, false);
            HuntSet(w2, "cineFps", 30);
            HuntCall(w2, "Accept");
            Check(t.CineFps == 30, "el cambio a 30 fps de Cine no se guarda: " + t.CineFps);
        }

        static void SetupGif()
        {
            Settings s = new Settings();
            s.VideoQuality = Recorder.Cinema; s.Webcam = 2; s.RecordSystemAudio = true; s.RecordMic = true; s.GifFps = 15;
            RecordSetup w = HuntSetup(s, true);
            HuntSet(w, "gifFps", 20);
            HuntSet(w, "sys", false);
            HuntCall(w, "Accept");
            Check(s.GifFps == 20, "GIF no guarda sus fps: " + s.GifFps);
            Check(s.VideoQuality == Recorder.Cinema && s.Webcam == 2 && s.RecordSystemAudio && s.RecordMic, "un GIF cambia los ajustes de video");
        }

        static void SetupTwice()
        {
            Settings s = new Settings();
            s.VideoQuality = Recorder.Standard;
            RecordSetup w = HuntSetup(s, false);
            HuntSet(w, "quality", Recorder.Maximum);
            HuntCall(w, "Accept");
            HuntSet(w, "quality", Recorder.Cinema);
            HuntCall(w, "Accept");
            Check(s.VideoQuality == Recorder.Maximum, "un segundo Accept cambia la calidad: " + s.VideoQuality);
        }

        static void SetupCountdown()
        {
            int[] given = { 0, 3, 5, 7, 10, 60 }, want = { 0, 3, 5, 5, 10, 10 };
            for (int i = 0; i < given.Length; i++)
            {
                Settings s = new Settings();
                s.RecordCountdown = given[i];
                RecordSetup w = HuntSetup(s, false);
                HuntCall(w, "Accept");
                Check(s.RecordCountdown == want[i], "cuenta atras " + given[i] + " guarda " + s.RecordCountdown);
            }
        }

        // The frame rate choice that is shown must be the one the recording uses. Settings accept any value from 5 to 60,
        // while the window offers 24, 30 and 60 (the nearest one is shown).
        static void SetupVideoFpsShown()
        {
            int[] stored = { 5, 15, 24, 30, 45, 60 };
            int[] offered = { 24, 30, 60 };
            List<string> bad = new List<string>();
            foreach (int f in stored)
            {
                Settings s = new Settings();
                s.VideoQuality = Recorder.Standard; s.VideoFps = f;
                RecordSetup w = HuntSetup(s, false);
                int shown = offered[(int)HuntCallStatic(typeof(RecordSetup), "Nearest", offered, f)];
                HuntCall(w, "Accept");
                if (s.VideoFps != shown) bad.Add("guardado " + f + ", se muestra " + shown + " y se graba a " + s.VideoFps);
            }
            Check(bad.Count == 0, string.Join("; ", bad.ToArray()));
        }

        static void SetupGifFpsShown()
        {
            int[] stored = { 5, 12, 15, 30 };
            int[] offered = { 10, 15, 20, 25 };
            List<string> bad = new List<string>();
            foreach (int f in stored)
            {
                Settings s = new Settings();
                s.GifFps = f;
                RecordSetup w = HuntSetup(s, true);
                int shown = offered[(int)HuntCallStatic(typeof(RecordSetup), "Nearest", offered, f)];
                HuntCall(w, "Accept");
                if (s.GifFps != shown) bad.Add("guardado " + f + ", se muestra " + shown + " y se graba a " + s.GifFps);
            }
            Check(bad.Count == 0, string.Join("; ", bad.ToArray()));
        }

        // ---- CountdownWindow (never shown: its timer never ticks, so Done and Cancel are driven by hand) ----

        static CountdownWindow HuntCountdown(int seconds)
        {
            return new CountdownWindow(HuntArea(), seconds);
        }

        static void CountdownDone()
        {
            CountdownWindow c = HuntCountdown(1);
            try
            {
                Check(!c.Done, "Done antes de empezar");
                System.Threading.Thread.Sleep(1100);
                Check(c.Done, "no esta Done al pasar su segundo");
            }
            finally { c.Dispose(); }
        }

        static void CountdownMinimum()
        {
            CountdownWindow c = HuntCountdown(0);
            try
            {
                Check(HuntGet(c, "seconds").Equals(1), "0 segundos debe contar como 1: " + HuntGet(c, "seconds"));
                Check(!c.Done, "Done al empezar con 0 segundos");
            }
            finally { c.Dispose(); }
        }

        static void CountdownCentered()
        {
            CountdownWindow c = HuntCountdown(3);
            try
            {
                Rectangle a = HuntArea();
                int cx = c.Left + c.Width / 2, cy = c.Top + c.Height / 2;
                int ax = a.Left + a.Width / 2, ay = a.Top + a.Height / 2;
                Check(Math.Abs(cx - ax) <= 1 && Math.Abs(cy - ay) <= 1, "centro " + cx + "," + cy + " frente a " + ax + "," + ay);
                Check(c.Width == c.Height && c.Width > 0, "no es cuadrado: " + c.Width + "x" + c.Height);
            }
            finally { c.Dispose(); }
        }

        static void CountdownNoActivate()
        {
            CountdownWindow c = HuntCountdown(3);
            try
            {
                System.Windows.Forms.CreateParams cp = (System.Windows.Forms.CreateParams)HuntProperty(c, "CreateParams");
                Check((cp.ExStyle & 0x08000000) != 0, "falta WS_EX_NOACTIVATE");
                Check((bool)HuntProperty(c, "ShowWithoutActivation"), "ShowWithoutActivation no es verdadero");
                Message m = Message.Create(IntPtr.Zero, 0x0021, IntPtr.Zero, IntPtr.Zero);
                object[] args = { m };
                MethodInfo wp = typeof(CountdownWindow).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance);
                wp.Invoke(c, args);
                Check(((Message)args[0]).Result == (IntPtr)3, "un clic no devuelve MA_NOACTIVATE");
            }
            finally { c.Dispose(); }
        }

        static object HuntProperty(object o, string name)
        {
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Check(p != null, "no encuentro la propiedad " + name);
            return p.GetValue(o, null);
        }

        static void CountdownCancel()
        {
            CountdownWindow c = HuntCountdown(3);
            try
            {
                int fired = 0;
                c.Cancelled += delegate { fired++; };
                HuntCall(c, "Cancel");
                HuntCall(c, "Cancel");
                Check(fired == 1, "Cancelled se dispara " + fired + " veces");
            }
            finally { c.Dispose(); }
        }

        static void CountdownFirstFrame()
        {
            CountdownWindow c = HuntCountdown(3);
            try
            {
                int side = c.Width;
                Bitmap before = HuntPaint(c, side);
                HuntCall(c, "Step");
                Bitmap after = HuntPaint(c, side);
                HuntSet(c, "shown", 1);
                Bitmap one = HuntPaint(c, side);
                // Compare the middle of the window, where the digit is, and not the ring or the caption.
                long a = HuntRegionHash(before, side), b = HuntRegionHash(after, side), o = HuntRegionHash(one, side);
                before.Dispose();
                after.Dispose();
                one.Dispose();
                Note("primer dibujo " + (a == o ? "es el del 1" : "no es el del 1") + "; tras el primer tic " + (b == o ? "es el del 1" : "no es el del 1"));
                Check(a == b, "el primer dibujo (antes del primer tic) no muestra la misma cifra que el primer tic (3)");
            }
            finally { c.Dispose(); }
        }

        static Bitmap HuntPaint(CountdownWindow c, int side)
        {
            Bitmap bmp = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                HuntCall(c, "OnPaint", new PaintEventArgs(g, new Rectangle(0, 0, side, side)));
            }
            return bmp;
        }

        static long HuntRegionHash(Bitmap bmp, int side)
        {
            long h = 17;
            int x0 = side * 3 / 10, x1 = side * 7 / 10, y0 = side * 2 / 10, y1 = side * 6 / 10;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    h = h * 31 + bmp.GetPixel(x, y).ToArgb();
            return h;
        }

        // ---- disk estimate ----

        static void DiskOrder()
        {
            long gib = 1L << 30, free = 200 * gib;
            DiskBudget.Estimate std = DiskBudget.For(Recorder.Standard, false, 1920, 1080, 60, 1, free);
            DiskBudget.Estimate high = DiskBudget.For(Recorder.High, false, 1920, 1080, 60, 1, free);
            DiskBudget.Estimate max = DiskBudget.For(Recorder.Maximum, false, 1920, 1080, 60, 1, free);
            DiskBudget.Estimate cine = DiskBudget.For(Recorder.Cinema, false, 1920, 1080, 60, 1, free);
            Check(std.TypicalPerSecond < high.TypicalPerSecond && high.TypicalPerSecond < max.TypicalPerSecond && max.TypicalPerSecond < cine.TypicalPerSecond,
                  "tasas tipicas no crecen: " + std.TypicalPerSecond + " " + high.TypicalPerSecond + " " + max.TypicalPerSecond + " " + cine.TypicalPerSecond);
            Check(std.TypicalMinutes > high.TypicalMinutes && high.TypicalMinutes > max.TypicalMinutes && max.TypicalMinutes > cine.TypicalMinutes && cine.TypicalMinutes > 0,
                  "minutos no decrecen");
            Check(std.HeavyMinutes <= std.TypicalMinutes && cine.HeavyMinutes <= cine.TypicalMinutes, "con movimiento caben mas minutos que en lo normal");
        }

        static void DiskNoRoom()
        {
            foreach (long free in new long[] { 0, -5, 1000 })
            {
                DiskBudget.Estimate e = DiskBudget.For(Recorder.Maximum, false, 1280, 720, 60, 1, free);
                Check(!double.IsNaN(e.TypicalMinutes) && !double.IsNaN(e.HeavyMinutes) && e.TypicalMinutes >= 0 && e.HeavyMinutes >= 0,
                      "minutos raros con " + free + " bytes libres: " + e.TypicalMinutes + " / " + e.HeavyMinutes);
                if (free <= 0) Check(e.TypicalMinutes == 0 && e.HeavyMinutes == 0, "sin espacio debe dar 0 minutos");
            }
            DiskBudget.Estimate g = DiskBudget.For(Recorder.Standard, true, 1280, 720, 10, 0, 50L << 30);
            Check(g.TypicalMinutes > 0 && !double.IsNaN(g.TypicalMinutes), "estimacion de GIF");
        }

        static void DiskMinutesLeft()
        {
            Check(DiskBudget.MinutesLeft(0, 0, 1000) == 0, "sin espacio: 0 minutos");
            Check(DiskBudget.MinutesLeft(5, 10, 1000) == 0, "por debajo de lo necesario: 0 minutos");
            Check(double.IsPositiveInfinity(DiskBudget.MinutesLeft(100, 0, 1)), "sin ritmo debe ser infinito");
            Check(Math.Abs(DiskBudget.MinutesLeft(60000000L + 1000, 1000, 1000) - 1000) < 1e-6, "ritmo normal");
        }

        static void DiskTexts()
        {
            Check(DiskBudget.Range(0, 0) == "menos de 1 min", "Range(0,0): " + DiskBudget.Range(0, 0));
            Check(DiskBudget.Range(1, 1) == "unos 1 min", "Range(1,1): " + DiskBudget.Range(1, 1));
            Check(DiskBudget.Range(59.5, 60.5) == "entre 59 min y 60 min", "Range(59.5,60.5): " + DiskBudget.Range(59.5, 60.5));
            Check(DiskBudget.Chip(-3) == "~0 min de disco", "Chip(-3): " + DiskBudget.Chip(-3));
            Check(DiskBudget.Chip(2.99) == "~2 min de disco", "Chip(2.99): " + DiskBudget.Chip(2.99));
            Check(DiskBudget.Gb(0) == "0,0 GB", "Gb(0): " + DiskBudget.Gb(0));
        }

        // ---- argument plans ----

        static void GifArgsCheck()
        {
            Settings s = new Settings();
            s.GifFps = 5;
            Check(Recorder.TargetFps(s, true, 60) == 10, "GIF a 5 fps graba a " + Recorder.TargetFps(s, true, 60));
            s.GifFps = 30;
            Check(Recorder.TargetFps(s, true, 60) == 60, "GIF a 30 fps graba a " + Recorder.TargetFps(s, true, 60));
            string a = Recorder.GifArgs(1001, 15, "in.nut", "out.gif");
            Check(a.Contains("scale=960:-2") && a.Contains("fps=15,"), "GIF 1001 px: " + a);
            string b = Recorder.GifArgs(301, 15, "in.nut", "out.gif");
            Check(b.Contains("scale=300:-2"), "GIF 301 px: " + b);
        }

        static void StandardDirect()
        {
            LivePlan p = Recorder.PlanLive(Recorder.Standard, false, 30, 15, 640, 360, false, false, null, false, 30, 1, "out.mp4", "tmp");
            Check(p.Direct && p.Video == "out.mp4" && !p.Lossless, "Estandar sin sonido debe escribir el MP4 directo: " + p.Video + " " + p.Direct);
        }

        static void MaximaSmall()
        {
            LivePlan p = Recorder.PlanLive(Recorder.Maximum, false, 60, 15, 1280, 720, false, false, null, false, 60, 1, "out.mp4", "tmp");
            Check(p.Lossless && p.Enc.Contains("libx264rgb") && p.Enc.Contains("-qp 0"), "Maxima pequena no es sin perdidas: " + p.Enc);
        }

        // An area above 1920x1200 at 60 fps goes through NV12 on the GPU (Session.Start opens the source with wantNv12 =
        // large), so the live encode is 4:2:0 CRF 8. The options window must not call that lossless.
        static void MaximaLarge()
        {
            LivePlan p = Recorder.PlanLive(Recorder.Maximum, false, 60, 15, 2560, 1440, true, true, null, false, 60, 1, "out.mp4", "tmp");
            Check(!p.Enc.Contains("libx264rgb"), "the large-area plan is expected to be 4:2:0: " + p.Enc.Trim());
            Check(Recorder.LargeArea(2560, 1440, 60) && !Recorder.LargeArea(1920, 1200, 60), "LargeArea threshold");
            string big = RecordSetup.QualityNote(Recorder.Maximum, new Rectangle(0, 0, 2560, 1440));
            string small = RecordSetup.QualityNote(Recorder.Maximum, new Rectangle(0, 0, 1280, 720));
            Check(big.IndexOf("no es sin p\u00E9rdidas", StringComparison.Ordinal) >= 0 && big.IndexOf("Sin p\u00E9rdidas", StringComparison.Ordinal) < 0, "large area note must not promise lossless: " + big);
            Check(small.StartsWith("Sin p\u00E9rdidas", StringComparison.Ordinal), "small area note: " + small);
        }
    }
}
