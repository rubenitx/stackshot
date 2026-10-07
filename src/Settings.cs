// Stackshot - Settings in %LOCALAPPDATA%\Stackshot\settings.ini (plain key = value).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Text;

namespace Stackshot
{
    public class Settings
    {
        static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static readonly string DataDir = Path.Combine(LocalAppData, "Stackshot");
        public static readonly string TempDir = Path.Combine(DataDir, "temp");
        public static readonly string FilePath = Path.Combine(DataDir, "settings.ini");
        public static readonly string LogFile = Path.Combine(DataDir, "stackshot.log");
        public static readonly string FfmpegDir = Path.Combine(DataDir, "ffmpeg");
        public static readonly string InstallDir = Path.Combine(LocalAppData, "Programs", "Stackshot");
        public static readonly string InstalledExe = Path.Combine(InstallDir, "Stackshot.exe");

        // Where kept captures go (everything else is temporary and cleaned up).
        public string SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Stackshot");
        public bool Sound = true;
        public bool CopyToClipboard = true;
        public bool FollowMouse = true;
        public bool FirstRunDone;
        // Hotkeys: one or more combos per action, comma-separated (e.g. "PrintScreen, Ctrl+Shift+Alt+4").
        public string HotRegion = "PrintScreen";
        public string HotScreen = "Ctrl+PrintScreen";
        public string HotWindow = "Alt+PrintScreen";
        public string HotScroll = "Ctrl+Alt+PrintScreen";
        public string HotVideo = "Shift+PrintScreen";
        public string HotGif = "Ctrl+Shift+PrintScreen";
        public int VideoFps = 30;
        public int GifFps = 15;
        public int VideoQuality = 0;    // 0 standard, 1 maximum (60 fps, lossless capture, slow encode), 2 high (60 fps, crisp)
        public int Webcam = 0;          // camera bubble while recording video: 0 off, 1 round, 2 square
        public int WebcamSize = 1;      // 0 small, 1 medium, 2 large
        public string WebcamDevice = ""; // DirectShow name; empty = first camera
        public string Ffmpeg = "";      // custom ffmpeg.exe instead of the downloaded one
        // Editor presentation backdrop. BgPadding and BgRadius range 0-100 (see Backdrop).
        public bool BgAuto = false;     // open the editor with the backdrop applied
        public int BgPreset = 0;
        public int BgPadding = 50;
        public int BgRadius = 40;
        public bool BgShadow = true;
        public string BgRatio = "auto"; // auto, 16:9, 4:3 or 1:1
        // Main window and mascot.
        public bool CloseToTray = true;     // closing the window keeps Stackshot in the tray
        public int Profile = 1;          // 0 performance, 1 balanced, 2 everything (see HomeWindow.ApplyProfile)
        public bool ShowIntro = true;       // launch animation
        public bool MascotOn = true;
        public string MascotName = "Pixel";
        public int MascotColor = 0;
        public bool MascotTalks = true;     // speech bubbles with tips and greetings
        // Mascot look; indices into the MascotParts catalogs.
        public int MascotKind = 0;
        public int MascotHat = 0;
        public int MascotOutfit = 0;
        public int MascotFace = 0;
        public int MascotEyes = 0;
        public int MascotPersonality = 0;
        public bool MascotSeasonal = true;  // seasonal costume when no hat is chosen
        public bool MascotDesktop = false;  // also live on the desktop, above the taskbar
        public bool MascotClimb = true;     // the desktop mascot hops onto the foreground window
        public int MascotSpot = 720;        // where the desktop mascot stands, in thousandths of its monitor width
        public int MascotLove = 0;          // friendship points (one per capture)
        // Updates.
        public bool CheckUpdates = true;
        public string LastUpdateCheck = "";

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant(), v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "savefolder": if (v.Length > 0) s.SaveFolder = Environment.ExpandEnvironmentVariables(v); break;
                        case "sound": s.Sound = Bool(v, s.Sound); break;
                        case "copytoclipboard": s.CopyToClipboard = Bool(v, s.CopyToClipboard); break;
                        case "followmouse": s.FollowMouse = Bool(v, s.FollowMouse); break;
                        case "firstrundone": s.FirstRunDone = Bool(v, s.FirstRunDone); break;
                        case "region": s.HotRegion = v; break;
                        case "screen": s.HotScreen = v; break;
                        case "window": s.HotWindow = v; break;
                        case "scroll": s.HotScroll = v; break;
                        case "closetotray": s.CloseToTray = Bool(v, s.CloseToTray); break;
                        case "profile": s.Profile = Int(v, s.Profile, 0, 2); break;
                        case "showintro": s.ShowIntro = Bool(v, s.ShowIntro); break;
                        case "mascot": s.MascotOn = Bool(v, s.MascotOn); break;
                        case "mascotname": if (v.Length > 0) s.MascotName = v.Length > 16 ? v.Substring(0, 16) : v; break;
                        case "mascotcolor": s.MascotColor = Int(v, s.MascotColor, 0, 99); break;
                        case "mascottalks": s.MascotTalks = Bool(v, s.MascotTalks); break;
                        case "mascotkind": s.MascotKind = Int(v, s.MascotKind, 0, 99); break;
                        case "mascothat": s.MascotHat = Int(v, s.MascotHat, 0, 99); break;
                        case "mascotoutfit": s.MascotOutfit = Int(v, s.MascotOutfit, 0, 99); break;
                        case "mascotface": s.MascotFace = Int(v, s.MascotFace, 0, 99); break;
                        case "mascoteyes": s.MascotEyes = Int(v, s.MascotEyes, 0, MascotParts.EyeNames.Length - 1); break;
                        case "mascotpersonality": s.MascotPersonality = Int(v, s.MascotPersonality, 0, MascotParts.Personalities.Length - 1); break;
                        case "mascotseasonal": s.MascotSeasonal = Bool(v, s.MascotSeasonal); break;
                        case "mascotdesktop": s.MascotDesktop = Bool(v, s.MascotDesktop); break;
                        case "mascotclimb": s.MascotClimb = Bool(v, s.MascotClimb); break;
                        case "mascotspot": s.MascotSpot = Int(v, s.MascotSpot, 0, 1000); break;
                        case "mascotlove": s.MascotLove = Int(v, s.MascotLove, 0, 1000000); break;
                        case "checkupdates": s.CheckUpdates = Bool(v, s.CheckUpdates); break;
                        case "lastupdatecheck": s.LastUpdateCheck = v; break;
                        case "video": s.HotVideo = v; break;
                        case "gif": s.HotGif = v; break;
                        case "videofps": s.VideoFps = Int(v, s.VideoFps, 5, 60); break;
                        case "giffps": s.GifFps = Int(v, s.GifFps, 5, 30); break;
                        case "videoquality": s.VideoQuality = Int(v, s.VideoQuality, 0, 2); break;
                        case "webcam": s.Webcam = Int(v, s.Webcam, 0, 2); break;
                        case "webcamsize": s.WebcamSize = Int(v, s.WebcamSize, 0, 2); break;
                        case "webcamdevice": s.WebcamDevice = v; break;
                        case "ffmpeg": s.Ffmpeg = v; break;
                        case "bgauto": s.BgAuto = Bool(v, s.BgAuto); break;
                        case "bgpreset": s.BgPreset = Int(v, s.BgPreset, 0, 99); break;
                        case "bgpadding": s.BgPadding = Int(v, s.BgPadding, 0, 100); break;
                        case "bgradius": s.BgRadius = Int(v, s.BgRadius, 0, 100); break;
                        case "bgshadow": s.BgShadow = Bool(v, s.BgShadow); break;
                        case "bgratio": if (v == "auto" || v == "16:9" || v == "4:3" || v == "1:1") s.BgRatio = v; break;
                    }
                }
            }
            catch (Exception ex) { ShotStack.Log("Ajustes: " + ex.Message); }
            return s;
        }

        // With --test (and in tooling) settings are read but never written.
        public static bool ReadOnly;

        public void Save()
        {
            if (ReadOnly) return;
            try
            {
                Directory.CreateDirectory(DataDir);
                StringBuilder b = new StringBuilder();
                b.AppendLine("# Ajustes de Stackshot. Se pueden cambiar desde el icono de la bandeja > Ajustes.");
                b.AppendLine("SaveFolder = " + OneLine(SaveFolder));
                b.AppendLine("Sound = " + (Sound ? "1" : "0"));
                b.AppendLine("CopyToClipboard = " + (CopyToClipboard ? "1" : "0"));
                b.AppendLine("FollowMouse = " + (FollowMouse ? "1" : "0"));
                b.AppendLine("FirstRunDone = " + (FirstRunDone ? "1" : "0"));
                b.AppendLine("# Atajos (varios separados por comas). Ejemplos: PrintScreen, Ctrl+Shift+S, Alt+F1");
                b.AppendLine("Region = " + OneLine(HotRegion));
                b.AppendLine("Screen = " + OneLine(HotScreen));
                b.AppendLine("Window = " + OneLine(HotWindow));
                b.AppendLine("Scroll = " + OneLine(HotScroll));
                b.AppendLine("Video = " + OneLine(HotVideo));
                b.AppendLine("Gif = " + OneLine(HotGif));
                b.AppendLine("VideoFps = " + VideoFps);
                b.AppendLine("GifFps = " + GifFps);
                b.AppendLine("Profile = " + Profile);
                b.AppendLine("VideoQuality = " + VideoQuality);
                b.AppendLine("Webcam = " + Webcam);
                b.AppendLine("WebcamSize = " + WebcamSize);
                b.AppendLine("WebcamDevice = " + OneLine(WebcamDevice));
                b.AppendLine("# Ruta a ffmpeg.exe (vac\u00EDo = el que descarga Stackshot la primera vez que grabas)");
                b.AppendLine("Ffmpeg = " + OneLine(Ffmpeg));
                b.AppendLine("# Fondo de presentaci\u00F3n del editor (margen y esquinas de 0 a 100; proporci\u00F3n: auto, 16:9, 4:3 o 1:1)");
                b.AppendLine("BgAuto = " + (BgAuto ? "1" : "0"));
                b.AppendLine("BgPreset = " + BgPreset);
                b.AppendLine("BgPadding = " + BgPadding);
                b.AppendLine("BgRadius = " + BgRadius);
                b.AppendLine("BgShadow = " + (BgShadow ? "1" : "0"));
                b.AppendLine("BgRatio = " + OneLine(BgRatio));
                b.AppendLine("CloseToTray = " + (CloseToTray ? "1" : "0"));
                b.AppendLine("ShowIntro = " + (ShowIntro ? "1" : "0"));
                b.AppendLine("Mascot = " + (MascotOn ? "1" : "0"));
                b.AppendLine("MascotName = " + OneLine(MascotName));
                b.AppendLine("MascotColor = " + MascotColor);
                b.AppendLine("MascotTalks = " + (MascotTalks ? "1" : "0"));
                b.AppendLine("MascotKind = " + MascotKind);
                b.AppendLine("MascotHat = " + MascotHat);
                b.AppendLine("MascotOutfit = " + MascotOutfit);
                b.AppendLine("MascotFace = " + MascotFace);
                b.AppendLine("MascotEyes = " + MascotEyes);
                b.AppendLine("MascotPersonality = " + MascotPersonality);
                b.AppendLine("MascotSeasonal = " + (MascotSeasonal ? "1" : "0"));
                b.AppendLine("MascotDesktop = " + (MascotDesktop ? "1" : "0"));
                b.AppendLine("MascotClimb = " + (MascotClimb ? "1" : "0"));
                b.AppendLine("MascotSpot = " + MascotSpot);
                b.AppendLine("MascotLove = " + MascotLove);
                b.AppendLine("CheckUpdates = " + (CheckUpdates ? "1" : "0"));
                b.AppendLine("LastUpdateCheck = " + OneLine(LastUpdateCheck));
                // Write to a temp file and swap it in, so a crash mid-write never leaves an empty file.
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, b.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { ShotStack.Log("Guardar ajustes: " + ex.Message); }
        }

        // A line break in a value would inject extra keys into the file (e.g. a different Ffmpeg path).
        static string OneLine(string v)
        {
            return (v ?? "").Replace("\r", "").Replace("\n", "");
        }

        // Hotkey actions, in menu order.
        public static readonly string[] Actions = { "region", "screen", "window", "scroll", "video", "gif" };

        public string HotkeysFor(string action)
        {
            switch (action)
            {
                case "region": return HotRegion;
                case "screen": return HotScreen;
                case "window": return HotWindow;
                case "scroll": return HotScroll;
                case "video": return HotVideo;
                default: return HotGif;
            }
        }

        public void SetHotkeys(string action, string value)
        {
            switch (action)
            {
                case "region": HotRegion = value; break;
                case "screen": HotScreen = value; break;
                case "window": HotWindow = value; break;
                case "scroll": HotScroll = value; break;
                case "video": HotVideo = value; break;
                default: HotGif = value; break;
            }
        }

        public Settings Clone()
        {
            return (Settings)MemberwiseClone();
        }

        static bool Bool(string v, bool def)
        {
            v = v.ToLowerInvariant();
            if (v == "1" || v == "true" || v == "si" || v == "yes") return true;
            if (v == "0" || v == "false" || v == "no") return false;
            return def;
        }

        static int Int(string v, int def, int min, int max)
        {
            int n;
            return int.TryParse(v, out n) ? Math.Max(min, Math.Min(max, n)) : def;
        }
    }
}
