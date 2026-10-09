// Stackshot - Per-user install (no admin), welcome window and setup.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Stackshot
{
    // Installs to %LOCALAPPDATA%\Programs\Stackshot like any per-user app: Start menu shortcut, optional startup and an
    // entry in Settings > Apps.
    public static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Stackshot";
        public const string QuitEvent = "Local\\Stackshot.Quit";
        public const string MutexName = "Local\\Stackshot";
        public const string ShowEvent = "Local\\Stackshot.Show";
        public const string OpenEvent = "Local\\Stackshot.Open";

        public static string StartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Stackshot.lnk"); } }
        public static string MenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Stackshot.lnk"); } }
        public static string ExePath { get { return Path.GetFullPath(Application.ExecutablePath); } }

        public static bool RunningInstalled
        {
            get { return string.Equals(ExePath, Path.GetFullPath(Settings.InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsInstalled { get { return File.Exists(Settings.InstalledExe); } }

        // Installed by the MSI (tools\Stackshot.wxs, usually deployed by IT): Windows Installer owns shortcuts, the
        // Apps entry, upgrades and uninstall.
        const string MsiKey = @"Software\Stackshot";

        public static bool ManagedByMsi
        {
            get
            {
                Guid code;
                return MsiValue("Installer") == "msi" && Guid.TryParse(MsiValue("ProductCode") ?? "", out code);
            }
        }

        static string MsiValue(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(MsiKey))
                    return k == null ? null : k.GetValue(name) as string;
            }
            catch { return null; }
        }
        public static bool StartupEnabled { get { return File.Exists(StartupLink); } }
        public static Version MyVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        public static Version InstalledVersion
        {
            get
            {
                try { return new Version(FileVersionInfo.GetVersionInfo(Settings.InstalledExe).FileVersion); }
                catch { return new Version(0, 0); }
            }
        }

        public static void Install(bool startup)
        {
            Directory.CreateDirectory(Settings.InstallDir);
            if (!RunningInstalled)
            {
                QuitRunning();
                Exception last = null;
                for (int i = 0; i < 25; i++)
                {
                    try { CopyContents(ExePath, Settings.InstalledExe); last = null; break; }
                    catch (Exception ex) { last = ex; System.Threading.Thread.Sleep(200); }
                }
                if (last != null) throw last;
            }
            Register(startup);
            ShotStack.Log("Instalado " + MyVersion + " en " + Settings.InstallDir);
        }

        // Writes a new file with the contents only, like any installer (alternate data streams are not carried over). The
        // old file goes only once nothing runs from it (until then deleting fails and nothing has changed); if the new one
        // can't be renamed into place right after (a scanner holding it), its bytes are written there directly, so there
        // is never a moment without an installed Stackshot.
        static void CopyContents(string from, string to)
        {
            byte[] data = File.ReadAllBytes(from);
            string tmp = to + ".new";
            File.WriteAllBytes(tmp, data);
            try
            {
                if (File.Exists(to)) File.Delete(to);
                try { File.Move(tmp, to); }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                    File.WriteAllBytes(to, data);
                }
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } // a leftover is overwritten next time
            }
        }

        // Shortcuts and uninstall entry pointing to the installed copy.
        public static void Register(bool startup)
        {
            if (ManagedByMsi) return; // the MSI handles this
            string exe = Settings.InstalledExe;
            // A shortcut the shell refuses (a locked-down profile) is logged, never a reason for the install to fail.
            try { CreateShortcut(MenuLink, exe, "Stackshot: capturas de pantalla, v\u00EDdeo y GIF", ""); }
            catch (Exception ex) { ShotStack.Log("Acceso directo del men\u00FA Inicio: " + ex.Message); }
            SetStartup(startup);
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", "Stackshot");
                    k.SetValue("DisplayVersion", MyVersion.ToString(3));
                    k.SetValue("Publisher", "rubenitx");
                    k.SetValue("DisplayIcon", exe + ",0");
                    k.SetValue("InstallLocation", Settings.InstallDir);
                    k.SetValue("UninstallString", "\"" + exe + "\" --uninstall");
                    k.SetValue("QuietUninstallString", "\"" + exe + "\" --uninstall --quiet");
                    k.SetValue("URLInfoAbout", Program.RepoUrl);
                    k.SetValue("HelpLink", Program.RepoUrl);
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { ShotStack.Log("Registro de desinstalaci\u00F3n: " + ex.Message); }
        }

        // When running from the install folder, recreate missing shortcuts and refresh the Apps entry of a copy replaced
        // by hand. Cheap when all is in place (no shortcut is rewritten on every start).
        public static void Repair()
        {
            if (!RunningInstalled || ManagedByMsi) return;
            try
            {
                string shown;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    shown = k == null ? null : k.GetValue("DisplayVersion") as string;
                if (!File.Exists(MenuLink) || shown != MyVersion.ToString(3)) Register(StartupEnabled);
                else if (StartupEnabled && !LinkHas(StartupLink, "--background")) SetStartup(true); // older shortcuts lacked it
            }
            catch (Exception ex) { ShotStack.Log("Reparar instalaci\u00F3n: " + ex.Message); }
        }

        // Whether a shortcut's arguments contain that text (a .lnk stores them as UTF-16), without COM.
        static bool LinkHas(string lnk, string text)
        {
            try
            {
                byte[] data = File.ReadAllBytes(lnk), find = System.Text.Encoding.Unicode.GetBytes(text);
                for (int i = 0; i + find.Length <= data.Length; i++)
                {
                    int j = 0;
                    while (j < find.Length && data[i + j] == find[j]) j++;
                    if (j == find.Length) return true;
                }
            }
            catch { }
            return false;
        }

        // The startup shortcut starts in the background (no window).
        public static void SetStartup(bool on)
        {
            try
            {
                if (on) CreateShortcut(StartupLink, IsInstalled ? Settings.InstalledExe : ExePath, "Stackshot", "--background");
                else if (File.Exists(StartupLink)) File.Delete(StartupLink);
            }
            catch (Exception ex) { ShotStack.Log("Arranque con Windows: " + ex.Message); }
        }

        static void CreateShortcut(string lnk, string target, string description, string arguments)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object link = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type lt = link.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { arguments });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { target + ",0" });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                Marshal.ReleaseComObject(link);
            }
            finally { Marshal.ReleaseComObject(shell); }
        }

        // A Stackshot is running (it listens for the quit request; test runs don't).
        public static bool Running
        {
            get
            {
                try
                {
                    System.Threading.EventWaitHandle e;
                    if (!System.Threading.EventWaitHandle.TryOpenExisting(QuitEvent, out e)) return false;
                    e.Dispose();
                    return true;
                }
                catch { return false; }
            }
        }

        // Ask the running instance to show its window.
        public static void SignalShow()
        {
            try { System.Threading.EventWaitHandle.OpenExisting(ShowEvent).Set(); }
            catch { }
        }

        static string OpenFile { get { return Path.Combine(Settings.DataDir, "open.txt"); } }

        // Ask the running instance to open a .md or .xml in the viewer. The request is written first (with its time), so it
        // is found even when the running copy is still starting; the event is then looked for up to waitMs.
        // False only when nothing could be written.
        public static bool SendOpen(string path, int waitMs)
        {
            try
            {
                Directory.CreateDirectory(Settings.DataDir);
                byte[] line = System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks + "\t" + path + "\n");
                using (FileStream f = new FileStream(OpenFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) f.Write(line, 0, line.Length);
            }
            catch { return false; }
            DateTime end = DateTime.UtcNow.AddMilliseconds(waitMs);
            while (true)
            {
                try
                {
                    using (System.Threading.EventWaitHandle e = System.Threading.EventWaitHandle.OpenExisting(OpenEvent)) { e.Set(); }
                    break;
                }
                catch { }
                if (DateTime.UtcNow >= end) break;
                System.Threading.Thread.Sleep(100);
            }
            return true;
        }

        // The paths waiting for the running instance (taken by renaming the file, so nothing is read twice). Requests
        // older than two minutes, or for files that are gone, are ignored.
        public static List<string> TakeOpenRequests()
        {
            List<string> r = new List<string>();
            for (int round = 0; round < 5 && File.Exists(OpenFile); round++)
            {
                string tmp = Path.Combine(Settings.DataDir, "open." + Guid.NewGuid().ToString("N") + ".txt");
                bool moved = false;
                for (int i = 0; i < 3 && !moved; i++)
                {
                    try { File.Move(OpenFile, tmp); moved = true; }
                    catch (FileNotFoundException) { break; }
                    catch (IOException) { System.Threading.Thread.Sleep(50); }
                    catch { break; }
                }
                if (!moved) break;
                try
                {
                    foreach (string l in File.ReadAllLines(tmp, System.Text.Encoding.UTF8))
                    {
                        string p = l.Trim();
                        if (p.Length == 0) continue;
                        int tab = p.IndexOf('\t');
                        long ticks;
                        if (tab > 0 && long.TryParse(p.Substring(0, tab), out ticks))
                        {
                            p = p.Substring(tab + 1).Trim();
                            if (ticks > DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute || DateTime.UtcNow.Ticks - ticks > 2 * TimeSpan.TicksPerMinute) continue;
                        }
                        if (ViewerFile(p) && File.Exists(p) && !r.Exists(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) r.Add(p);
                    }
                }
                catch { }
                try { File.Delete(tmp); } catch { }
            }
            return r;
        }

        // A file Stackshot opens in its viewer (Markdown or XML).
        public static bool ViewerFile(string path)
        {
            try
            {
                string e = Path.GetExtension(path).ToLowerInvariant();
                return e == ".md" || e == ".markdown" || e == ".mdown" || e == ".mkd" || XmlDoc.IsXmlPath(path);
            }
            catch { return false; }
        }

        // Ask the running instance to quit (keeping the clipboard) and wait for it.
        public static void QuitRunning()
        {
            try { System.Threading.EventWaitHandle.OpenExisting(QuitEvent).Set(); }
            catch { return; }
            DateTime until = DateTime.Now.AddSeconds(10);
            while (DateTime.Now < until)
            {
                using (System.Threading.Mutex m = new System.Threading.Mutex(false, MutexName))
                {
                    bool free;
                    try { free = m.WaitOne(0); }
                    catch (System.Threading.AbandonedMutexException) { free = true; }
                    if (free) { m.ReleaseMutex(); return; }
                }
                System.Threading.Thread.Sleep(150);
            }
        }

        public static void Uninstall(bool quiet)
        {
            if (ManagedByMsi)
            {
                // Windows Installer uninstalls it (closes Stackshot, removes shortcuts, files and data).
                try { Process.Start(Native.System32("msiexec.exe"), "/x " + Guid.Parse(MsiValue("ProductCode")).ToString("B") + (quiet ? " /qn" : "")); }
                catch (Exception ex) { ShotStack.Log("Desinstalar (MSI): " + ex.Message); }
                return;
            }
            Settings s = Settings.Load();
            if (!quiet && MessageBox.Show("\u00BFDesinstalar Stackshot?\n\nLas capturas que hayas guardado en " + s.SaveFolder + " no se borran.",
                                          "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            QuitRunning();
            try { if (File.Exists(StartupLink)) File.Delete(StartupLink); } catch { }
            try { if (File.Exists(MenuLink)) File.Delete(MenuLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { if (Directory.Exists(Settings.DataDir)) Directory.Delete(Settings.DataDir, true); } catch { }
            // A running .exe can't delete itself, so a hidden cmd removes the folder once this process exits.
            try
            {
                // Retries every second for up to 30 s.
                string dir = Settings.InstallDir.TrimEnd('\\');
                ProcessStartInfo psi = new ProcessStartInfo(Native.System32("cmd.exe"),
                    "/d /c for /l %i in (1,1,30) do (ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"" + dir + "\" 2>nul & if not exist \"" + dir + "\" exit)");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
            if (!quiet) MessageBox.Show("Stackshot se ha desinstalado. \u00A1Gracias por probarlo!", "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Windows 11 maps Print Screen to Snipping Tool. Stackshot can take it over (current user only).
        public static bool SnippingOwnsPrintScreen
        {
            get
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", null);
                if (v is int) return (int)v != 0;
                return Environment.OSVersion.Version.Build >= 22000; // on by default on Windows 11
            }
        }

        // Windows 11 gives Print Screen to the Snipping Tool by default; this hands it back (current user only) and tells
        // the shell the keyboard setting changed, so it applies right away where Windows supports it.
        public static void FreePrintScreen()
        {
            try { Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord); }
            catch (Exception ex) { ShotStack.Log("Impr Pant: " + ex.Message); return; }
            try
            {
                UIntPtr result;
                SendMessageTimeout((IntPtr)0xFFFF, 0x001A, UIntPtr.Zero, "Keyboard", 0x0002, 1000, out result); // WM_SETTINGCHANGE, SMTO_ABORTIFHUNG
            }
            catch { }
            ShotStack.Log("Impr Pant liberada de Recortes");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, UIntPtr wParam, string lParam, int flags, int timeout, out UIntPtr result);
    }
}
