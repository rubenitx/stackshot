// Stackshot - Ajustes: %LOCALAPPDATA%\Stackshot\settings.ini (texto plano, clave = valor).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
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

        // Las capturas que guardas con 💾 (las demás son temporales y se borran solas).
        public string SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Stackshot");
        public bool Sound = true;
        public bool CopyToClipboard = true;
        public bool FollowMouse = true;
        public bool FirstRunDone;
        // Atajos: una o varias combinaciones por acción, separadas por comas (p. ej. "PrintScreen, Ctrl+Shift+Alt+4").
        public string HotRegion = "PrintScreen";
        public string HotScreen = "Ctrl+PrintScreen";
        public string HotWindow = "Alt+PrintScreen";
        public string HotVideo = "Shift+PrintScreen";
        public string HotGif = "Ctrl+Shift+PrintScreen";
        public int VideoFps = 30;
        public int GifFps = 15;
        public string Ffmpeg = "";      // ruta a ffmpeg.exe si no se quiere usar el que descarga Stackshot

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
                        case "video": s.HotVideo = v; break;
                        case "gif": s.HotGif = v; break;
                        case "videofps": s.VideoFps = Int(v, s.VideoFps, 5, 60); break;
                        case "giffps": s.GifFps = Int(v, s.GifFps, 5, 30); break;
                        case "ffmpeg": s.Ffmpeg = v; break;
                    }
                }
            }
            catch (Exception ex) { ShotStack.Log("Ajustes: " + ex.Message); }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                StringBuilder b = new StringBuilder();
                b.AppendLine("# Ajustes de Stackshot. Se pueden cambiar desde el icono de la bandeja > Ajustes.");
                b.AppendLine("SaveFolder = " + SaveFolder);
                b.AppendLine("Sound = " + (Sound ? "1" : "0"));
                b.AppendLine("CopyToClipboard = " + (CopyToClipboard ? "1" : "0"));
                b.AppendLine("FollowMouse = " + (FollowMouse ? "1" : "0"));
                b.AppendLine("FirstRunDone = " + (FirstRunDone ? "1" : "0"));
                b.AppendLine("# Atajos (varios separados por comas). Ejemplos: PrintScreen, Ctrl+Shift+S, Alt+F1");
                b.AppendLine("Region = " + HotRegion);
                b.AppendLine("Screen = " + HotScreen);
                b.AppendLine("Window = " + HotWindow);
                b.AppendLine("Video = " + HotVideo);
                b.AppendLine("Gif = " + HotGif);
                b.AppendLine("VideoFps = " + VideoFps);
                b.AppendLine("GifFps = " + GifFps);
                b.AppendLine("# Ruta a ffmpeg.exe (vac\u00EDo = el que descarga Stackshot la primera vez que grabas)");
                b.AppendLine("Ffmpeg = " + Ffmpeg);
                File.WriteAllText(FilePath, b.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { ShotStack.Log("Guardar ajustes: " + ex.Message); }
        }

        // Atajos de cada acción, en el orden en que salen en los menús.
        public static readonly string[] Actions = { "region", "screen", "window", "video", "gif" };

        public string HotkeysFor(string action)
        {
            switch (action)
            {
                case "region": return HotRegion;
                case "screen": return HotScreen;
                case "window": return HotWindow;
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
