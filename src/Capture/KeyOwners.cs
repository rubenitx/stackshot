// Stackshot - Who else answers a shortcut: Windows' own capture keys, and the capture apps that are open (with the keys
// their settings give them, when those can be read).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    public static class KeyOwners
    {
        // A capture shortcut of Windows itself. Windows never gives these up to RegisterHotKey, so Stackshot takes them on
        // the way in (KeyGrab) once the user puts one on an action.
        public sealed class Reserved
        {
            public readonly string Combo, Owner, Does;
            public Reserved(string combo, string owner, string does) { Combo = combo; Owner = owner; Does = does; }
        }

        public const string Snipping = "Recortes de Windows", GameBar = "Xbox Game Bar";

        static readonly Reserved[] Windows =
        {
            new Reserved("Win+Shift+S", Snipping, "recortar un \u00E1rea"),
            new Reserved("Win+Shift+R", Snipping, "grabar la pantalla"),
            new Reserved("Win+PrintScreen", "Windows", "guardar la pantalla en Im\u00E1genes"),
            new Reserved("Win+Alt+PrintScreen", GameBar, "capturar la ventana"),
            new Reserved("Win+Alt+R", GameBar, "grabar la ventana"),
            new Reserved("Win+Alt+G", GameBar, "grabar lo \u00FAltimo"),
            new Reserved("Win+G", GameBar, "abrir la Game Bar")
        };

        // The three that "Usar los atajos de Windows" puts on Stackshot's actions, as Windows uses them.
        public static readonly string[][] WindowsSet =
        {
            new string[] { "Win+Shift+S", "region" },
            new string[] { "Win+Shift+R", "video" },
            new string[] { "Win+PrintScreen", "screen" }
        };

        // The Windows shortcut a combo is, or null. Print Screen alone only while Windows gives it to the Snipping Tool.
        public static Reserved OfWindows(string combo)
        {
            if (string.IsNullOrEmpty(combo)) return null;
            foreach (Reserved r in Windows) if (Hotkeys.Same(r.Combo, combo)) return r;
            if (Hotkeys.Same(combo, "PrintScreen") && (Installer.SnippingOwnsPrintScreen || Installer.FreedPrintScreen))
                return new Reserved("PrintScreen", Snipping, "recortar un \u00E1rea");
            return null;
        }

        // ---- Capture apps

        // A capture app Stackshot knows: its process names, its usual keys and where its own settings say otherwise.
        sealed class Known
        {
            public string Name;
            public string[] Processes, Keys;
            public Func<List<string>> Read;      // the keys its settings give it, or null to use the usual ones
            public bool Maybe;                   // takes Print Screen only when one of its options is on
        }

        static readonly Known[] Apps =
        {
            new Known { Name = "ShareX", Processes = new[] { "ShareX" }, Keys = new[] { "PrintScreen", "Ctrl+PrintScreen", "Alt+PrintScreen", "Shift+PrintScreen", "Ctrl+Shift+PrintScreen" }, Read = ShareXKeys },
            new Known { Name = "Greenshot", Processes = new[] { "Greenshot" }, Keys = new[] { "PrintScreen", "Alt+PrintScreen", "Ctrl+PrintScreen", "Shift+PrintScreen", "Ctrl+Shift+PrintScreen" }, Read = GreenshotKeys },
            new Known { Name = "Lightshot", Processes = new[] { "Lightshot" }, Keys = new[] { "PrintScreen", "Shift+PrintScreen" } },
            new Known { Name = "Snagit", Processes = new[] { "Snagit32", "SnagitCapture" }, Keys = new[] { "PrintScreen", "Ctrl+Shift+C" } },
            new Known { Name = "PicPick", Processes = new[] { "picpick" }, Keys = new[] { "PrintScreen", "Ctrl+PrintScreen", "Shift+PrintScreen", "Ctrl+Shift+PrintScreen", "Alt+PrintScreen" } },
            new Known { Name = "Snipaste", Processes = new[] { "Snipaste" }, Keys = new[] { "F1", "F3" } },
            new Known { Name = "Flameshot", Processes = new[] { "flameshot" }, Keys = new[] { "PrintScreen" } },
            new Known { Name = "FastStone Capture", Processes = new[] { "FSCapture" }, Keys = new[] { "PrintScreen", "Alt+PrintScreen", "Ctrl+PrintScreen", "Shift+PrintScreen", "Ctrl+Shift+PrintScreen" } },
            new Known { Name = "Screenpresso", Processes = new[] { "Screenpresso" }, Keys = new[] { "PrintScreen", "Ctrl+PrintScreen", "Shift+PrintScreen" } },
            new Known { Name = "Gyazo", Processes = new[] { "GyStation", "Gyazowin" }, Keys = new[] { "Ctrl+Shift+C" } },
            new Known { Name = "Monosnap", Processes = new[] { "Monosnap" }, Keys = new[] { "Alt+Shift+5", "Alt+Shift+6" } },
            new Known { Name = "PowerToys", Processes = new[] { "PowerToys" }, Keys = new[] { "Win+Shift+T", "Win+Shift+C", "Win+Ctrl+Shift+T" } },
            new Known { Name = "NVIDIA", Processes = new[] { "NVIDIA Share", "NVIDIA Overlay" }, Keys = new[] { "Alt+F1", "Alt+F9", "Alt+F10", "Alt+Z" } },
            new Known { Name = "AMD Radeon", Processes = new[] { "RadeonSoftware" }, Keys = new[] { "Ctrl+Shift+I", "Ctrl+Shift+E", "Ctrl+Shift+R" } },
            new Known { Name = "OneDrive", Processes = new[] { "OneDrive" }, Keys = new[] { "PrintScreen" }, Maybe = true },
            new Known { Name = "Dropbox", Processes = new[] { "Dropbox" }, Keys = new[] { "PrintScreen", "Ctrl+PrintScreen", "Alt+PrintScreen" }, Maybe = true }
        };

        // A capture app that is open now, the keys it most likely holds and where its .exe is (for its icon).
        public sealed class App
        {
            public string Name, Exe;
            public List<string> Keys = new List<string>();
            public List<int> Pids = new List<int>();
            public bool Maybe, FromSettings;
        }

        // Tests replace what is running and what the apps' settings say.
        internal static Func<string, int[]> FindProcesses { get; set; }
        internal static Func<string, List<string>> ReadKeys { get; set; }

        // The known capture apps that are open now. Lists the processes: call it off the UI thread.
        public static List<App> Running()
        {
            List<App> list = new List<App>();
            int me;
            using (Process self = Process.GetCurrentProcess()) me = self.Id;
            Dictionary<string, List<int>> open = FindProcesses == null ? Snapshot() : null;
            foreach (Known k in Apps)
            {
                App a = null;
                foreach (string name in k.Processes)
                {
                    foreach (int pid in Pids(name, open))
                    {
                        if (pid == me) continue;
                        if (a == null) a = new App { Name = k.Name, Maybe = k.Maybe };
                        a.Pids.Add(pid);
                        if (a.Exe == null && FindProcesses == null) a.Exe = ExeOf(pid);
                    }
                }
                if (a == null) continue;
                List<string> keys = null;
                try { keys = ReadKeys != null ? ReadKeys(k.Name) : k.Read != null ? k.Read() : null; }
                catch (Exception ex) { ShotStack.Log("Atajos de " + k.Name + ": " + ex.Message); }
                a.FromSettings = keys != null;
                foreach (string c in keys ?? new List<string>(k.Keys))
                    if (!a.Keys.Exists(delegate(string o) { return Hotkeys.Same(o, c); })) a.Keys.Add(Hotkeys.Normalize(c));
                list.Add(a);
            }
            return list;
        }

        static int[] Pids(string name, Dictionary<string, List<int>> open)
        {
            if (FindProcesses != null) return FindProcesses(name) ?? new int[0];
            List<int> l;
            return open != null && open.TryGetValue(name, out l) ? l.ToArray() : new int[0];
        }

        // Every process by name, listed once (asking name by name lists them all each time).
        static Dictionary<string, List<int>> Snapshot()
        {
            Dictionary<string, List<int>> o = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            Process[] ps;
            try { ps = Process.GetProcesses(); }
            catch { return o; }
            foreach (Process p in ps)
            {
                try
                {
                    List<int> l;
                    if (!o.TryGetValue(p.ProcessName, out l)) o[p.ProcessName] = l = new List<int>();
                    l.Add(p.Id);
                }
                catch { }
                finally { p.Dispose(); }
            }
            return o;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        // Without reading the other process' memory: limited query access is enough for its path.
        static string ExeOf(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(1024);
                int n = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString(0, n) : null;
            }
            finally { CloseHandle(h); }
        }

        // Who most likely holds a combo Stackshot could not register: "ShareX", "ShareX o Greenshot", or null. Apps that
        // only take Print Screen with an option on (OneDrive, Dropbox) are named only when nothing surer is.
        public static string Holder(string combo, List<App> running)
        {
            if (running == null) return null;
            List<string> sure = new List<string>(), maybe = new List<string>();
            foreach (App a in running)
                if (a.Keys.Exists(delegate(string k) { return Hotkeys.Same(k, combo); })) (a.Maybe ? maybe : sure).Add(a.Name);
            List<string> names = sure.Count > 0 ? sure : maybe;
            if (names.Count == 0) return null;
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " o " + names[names.Count - 1];
        }

        // ---- Their settings

        // ShareX keeps its shortcuts in Documents\ShareX\HotkeysConfig.json: "Hotkey": "PrintScreen, Control" (a .NET Keys
        // value) with "Win" apart.
        static List<string> ShareXKeys()
        {
            string doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = Path.Combine(Path.Combine(doc, "ShareX"), "HotkeysConfig.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 2 * 1024 * 1024) return null;
            string json = File.ReadAllText(path);
            List<string> o = new List<string>();
            foreach (Match m in Regex.Matches(json, "\"Hotkey\"\\s*:\\s*\"([^\"]*)\"(?:\\s*,\\s*\"Win\"\\s*:\\s*(true|false))?"))
            {
                Keys k;
                string v = m.Groups[1].Value;
                if (v.Length == 0 || v == "None" || !Enum.TryParse(v, out k)) continue;
                if ((k & Keys.KeyCode) == Keys.None) continue;
                o.Add(Hotkeys.ToSetting(k, m.Groups[2].Value == "true"));
            }
            return o;
        }

        // Greenshot: %APPDATA%\Greenshot\Greenshot.ini, lines such as "RegionHotkey=Ctrl + PrintScreen".
        static List<string> GreenshotKeys()
        {
            string path = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Greenshot"), "Greenshot.ini");
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return null;
            List<string> o = new List<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                Match m = Regex.Match(line, @"^\s*\w+Hotkey\s*=\s*(.+?)\s*$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                uint mods;
                Keys key;
                string v = m.Groups[1].Value;
                if (Hotkeys.TryParse(v, out mods, out key)) o.Add(Hotkeys.Normalize(v));
            }
            return o;
        }

        // ---- Closing one

        // Asks each of its windows to close, the way its own menu would; true once none of its processes is left.
        public static bool CloseGently(App a, int waitMs)
        {
            foreach (int pid in a.Pids)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid))
                    {
                        p.CloseMainWindow();
                        foreach (IntPtr w in WindowsOf(pid)) PostMessage(w, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                    }
                }
                catch { }
            }
            return WaitGone(a, waitMs);
        }

        // Ends it as Task Manager does: anything it had unsaved is lost (asked before).
        public static bool CloseNow(App a, out string error)
        {
            error = null;
            foreach (int pid in a.Pids)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid))
                        if (!p.HasExited) p.Kill();
                }
                catch (ArgumentException) { }  // already gone
                catch (Exception ex)
                {
                    // Running as administrator: a normal app may not close it.
                    error = ex is System.ComponentModel.Win32Exception ? a.Name + " est\u00E1 abierto como administrador: ci\u00E9rralo desde su icono." : ex.Message;
                }
            }
            return WaitGone(a, 2000) && error == null;
        }

        static bool WaitGone(App a, int ms)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
            while (true)
            {
                bool any = false;
                foreach (int pid in a.Pids)
                {
                    try { using (Process p = Process.GetProcessById(pid)) if (!p.HasExited) any = true; }
                    catch { }
                }
                if (!any) return true;
                if (DateTime.UtcNow >= end) return false;
                Thread.Sleep(100);
            }
        }

        delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        static List<IntPtr> WindowsOf(int pid)
        {
            List<IntPtr> o = new List<IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                Native.GetWindowPid(h, out p);
                if (p == pid && Native.IsWindowVisible(h)) o.Add(h);
                return true;
            }, IntPtr.Zero);
            return o;
        }

        // Windows' list of apps that start with it: where the user turns one off for good.
        public static void OpenStartupApps()
        {
            if (Settings.ReadOnly) return;
            try { Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }); }
            catch (Exception ex) { ShotStack.Log("Aplicaciones de inicio: " + ex.Message); }
        }
    }
}
