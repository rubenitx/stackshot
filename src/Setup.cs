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
        // Exists while a closing Stackshot is still asking about unsaved work or saving a recording.
        public const string SavingEvent = "Local\\Stackshot.Saving";

        // Tests point the current user's registry somewhere else.
        internal static RegistryKey HkcuOverride { get; set; }
        static RegistryKey Hkcu { get { return HkcuOverride ?? Registry.CurrentUser; } }

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
            SweepParked();
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
            try { CreateShortcut(MenuLink, exe, I18n.T("Stackshot: capturas de pantalla, v\u00EDdeo y GIF", "Stackshot: screenshots, video and GIF"), ""); }
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

        // Ask the running instance to open a file (OpensHere: a .md or .xml in the viewer, a video in the player). The request
        // is written first (with its time), so it is found even when the running copy is still starting; the event is then
        // looked for up to waitMs.
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

        // Requests are still waiting (the file could not be taken: an antivirus or indexer held it for a moment).
        public static bool OpenPending { get { try { return File.Exists(OpenFile); } catch { return false; } } }

        // The paths waiting for the running instance (taken by renaming the file, so nothing is read twice). Requests
        // older than two minutes, or for files that are gone, are ignored.
        public static List<string> TakeOpenRequests() { return TakeOpenRequests(null); }

        // The same; the commands among the requests (CliCommand) go to commands, or are dropped when it is null.
        public static List<string> TakeOpenRequests(List<string> commands)
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
                        // Trimmed as Windows itself trims a path: a trailing space is never part of a name it can open.
                        string p = l.Trim();
                        if (p.Length == 0) continue;
                        int tab = p.IndexOf('\t');
                        long ticks;
                        if (tab > 0 && long.TryParse(p.Substring(0, tab), out ticks))
                        {
                            p = p.Substring(tab + 1).Trim();
                            if (ticks > DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute || DateTime.UtcNow.Ticks - ticks > 2 * TimeSpan.TicksPerMinute) continue;
                        }
                        else ticks = 0;
                        if (CliCommand.IsRequest(p)) { if (commands != null) CliCommand.Take(commands, p, ticks); continue; }
                        // No File.Exists here (UI thread; a sleeping WSL share can stall it): the viewer reports a missing file.
                        if (OpensHere(p) && !r.Exists(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) r.Add(p);
                    }
                }
                catch { }
                try { File.Delete(tmp); } catch { }
            }
            return r;
        }

        // A file given on the command line (double click, "Open with") that the running Stackshot opens: a document in the
        // viewer or a video in the player (a Cine master .mkv, which Windows' own player cannot decode). A GIF is still an
        // image to edit.
        public static bool OpensHere(string path)
        {
            if (ViewerFile(path)) return true;
            try { return ShotStack.IsVideoFile(path) && !string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
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

        // Ask the running instance to quit (keeping the clipboard) and wait for it: 10 s, or as long as it says it is
        // still saving a recording or waiting for the user to answer about unsaved work (up to 10 minutes).
        public static void QuitRunning()
        {
            try { System.Threading.EventWaitHandle.OpenExisting(QuitEvent).Set(); }
            catch { return; }
            DateTime start = DateTime.Now, until = start.AddSeconds(10), limit = start.AddMinutes(10);
            while (DateTime.Now < until)
            {
                using (System.Threading.Mutex m = new System.Threading.Mutex(false, MutexName))
                {
                    bool free;
                    try { free = m.WaitOne(0); }
                    catch (System.Threading.AbandonedMutexException) { free = true; }
                    if (free) { m.ReleaseMutex(); return; }
                }
                if (StillSaving()) until = DateTime.Now.AddSeconds(10) < limit ? DateTime.Now.AddSeconds(10) : limit;
                System.Threading.Thread.Sleep(150);
            }
        }

        static bool StillSaving()
        {
            try
            {
                System.Threading.EventWaitHandle e;
                if (!System.Threading.EventWaitHandle.TryOpenExisting(SavingEvent, out e)) return false;
                e.Dispose();
                return true;
            }
            catch { return false; }
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
            long recovered = WorkFiles.Bytes(ShotStack.RecoveredDir);
            string keepNote = recovered > 0 ? I18n.Fmt("\n\nLas grabaciones recuperadas ({0}) se quedan en {1}.", "\n\nRecovered recordings ({0}) remain in {1}.", Recents.Size(recovered), ShotStack.RecoveredDir) : "";
            if (!quiet && MessageBox.Show(I18n.Fmt("\u00BFDesinstalar Stackshot?\n\nLas capturas que hayas guardado en {0} no se borran.{1}", "Uninstall Stackshot?\n\nCaptures saved in {0} will not be deleted.{1}", s.SaveFolder, keepNote),
                                          "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            QuitRunning();
            if (Running)
            {
                // The user kept it open (unsaved changes) or it is still saving: nothing is removed under it.
                ShotStack.Log("Desinstalar: Stackshot sigue abierto");
                if (!quiet) MessageBox.Show(I18n.T("Stackshot sigue abierto: hay cambios sin guardar o una grabaci\u00F3n guard\u00E1ndose.\n\nCi\u00E9rralo y vuelve a desinstalar.",
                                                   "Stackshot is still open: there are unsaved changes or a recording is being saved.\n\nClose it and uninstall again."),
                                            "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool printBack = CleanShell();
            // A recording interrupted earlier is kept with the other recovered ones before the temp folder goes.
            WorkFiles.Rescue(Settings.TempDir, ShotStack.RecoveredDir, TimeSpan.Zero, true);
            try { if (File.Exists(StartupLink)) File.Delete(StartupLink); } catch { }
            try { if (File.Exists(MenuLink)) File.Delete(MenuLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            bool keptRecovered = DeleteData(Settings.DataDir, s.SaveFolder);
            RemoveInstallDir();
            if (quiet) return;
            string done = I18n.T("Stackshot se ha desinstalado. \u00A1Gracias por probarlo!", "Stackshot has been uninstalled. Thank you for trying it!");
            if (printBack) done += I18n.T("\n\nLa tecla Impr Pant vuelve a abrir Recortes de Windows.", "\n\nThe Print Screen key opens Windows Snipping Tool again.");
            if (keptRecovered) done += I18n.Fmt("\n\nLas grabaciones recuperadas siguen en {0}.", "\n\nRecovered recordings remain in {0}.", ShotStack.RecoveredDir);
            MessageBox.Show(done, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        const string ParkedPrefix = "Stackshot-desinstalado-";

        // A running .exe can't be deleted but can be moved: it goes to the temp folder under a name Windows won't run, and
        // the folder goes at once (no helper process left behind, which security software rightly distrusts). Windows'
        // temp cleanup, or the next install, removes the parked file. Should the move fail, the folder goes at next sign-in.
        static void RemoveInstallDir()
        {
            string dir = Settings.InstallDir.TrimEnd('\\');
            try
            {
                string self = Path.GetFullPath(ExePath);
                if (string.Equals(Path.GetDirectoryName(self), dir, StringComparison.OrdinalIgnoreCase))
                    File.Move(self, Path.Combine(Path.GetTempPath(), ParkedPrefix + Guid.NewGuid().ToString("N") + ".old"));
            }
            catch (Exception ex) { ShotStack.Log("Desinstalar, apartar el ejecutable: " + ex.Message); }
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception ex) { ShotStack.Log("Desinstalar, carpeta: " + ex.Message); }
            if (!Directory.Exists(dir)) return;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce"))
                    k.SetValue("StackshotCleanup", "\"" + Native.System32("cmd.exe") + "\" /d /c rmdir /s /q \"" + dir + "\"");
            }
            catch (Exception ex) { ShotStack.Log("Desinstalar, limpieza al iniciar sesi\u00F3n: " + ex.Message); }
        }

        // Executables parked by an earlier uninstall.
        static void SweepParked()
        {
            try
            {
                foreach (string f in Directory.GetFiles(Path.GetTempPath(), ParkedPrefix + "*.old"))
                    try { File.Delete(f); } catch (Exception ex) { ShotStack.Log("Instalar, resto de desinstalaci\u00F3n: " + ex.Message); }
            }
            catch (Exception ex) { ShotStack.Log("Instalar, resto de desinstalaci\u00F3n: " + ex.Message); }
        }

        // Deletes Stackshot's data folder except what can't be made again: recordings in recuperado\ and, should the save
        // folder ever live inside it, the saved captures. True when recuperado\ was kept.
        internal static bool DeleteData(string dataDir, string saveFolder)
        {
            if (!Directory.Exists(dataDir)) return false;
            string recovered = Path.Combine(dataDir, "recuperado");
            bool keep = WorkFiles.Bytes(recovered) > 0;
            if (!string.IsNullOrEmpty(saveFolder) && ShotStack.SameFolder(dataDir, saveFolder))
            {
                // The data folder is also where captures are saved: only what Stackshot itself keeps here goes.
                foreach (string d in SafeList(delegate { return Directory.GetDirectories(dataDir); }))
                {
                    string name = Path.GetFileName(d);
                    bool own = Array.Exists(OwnFolders, delegate(string o) { return string.Equals(o, name, StringComparison.OrdinalIgnoreCase); }) ||
                               (!keep && string.Equals(name, "recuperado", StringComparison.OrdinalIgnoreCase));
                    if (!own) continue;
                    try { Directory.Delete(d, true); } catch (Exception ex) { ShotStack.Log("Desinstalar, " + d + ": " + ex.Message); }
                }
                foreach (string f in SafeList(delegate { return Directory.GetFiles(dataDir); }))
                {
                    string name = Path.GetFileName(f);
                    bool own = name.StartsWith("settings.ini", StringComparison.OrdinalIgnoreCase) || name.StartsWith("stackshot.log", StringComparison.OrdinalIgnoreCase) ||
                               (name.StartsWith("open.", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
                    if (!own) continue;
                    try { File.Delete(f); } catch { }
                }
                return keep;
            }
            foreach (string d in SafeList(delegate { return Directory.GetDirectories(dataDir); }))
            {
                if (keep && ShotStack.SameFolder(d, recovered)) continue;
                if (Contains(d, saveFolder)) continue;
                try { Directory.Delete(d, true); } catch (Exception ex) { ShotStack.Log("Desinstalar, " + d + ": " + ex.Message); }
            }
            foreach (string f in SafeList(delegate { return Directory.GetFiles(dataDir); }))
            {
                try { File.Delete(f); } catch { }
            }
            if (!keep && !Contains(dataDir, saveFolder)) { try { Directory.Delete(dataDir, true); } catch { } }
            return keep;
        }

        // The folders Stackshot creates inside its data folder (recuperado\ is handled apart).
        static readonly string[] OwnFolders = { "temp", "ffmpeg", "cine", "Fondos", "posters", "update", "originales", "historial" };

        static string[] SafeList(Func<string[]> list)
        {
            try { return list(); }
            catch { return new string[0]; }
        }

        // Whether folder is dir itself or somewhere inside it.
        static bool Contains(string dir, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            try
            {
                string d = Path.GetFullPath(dir).TrimEnd('\\') + "\\", f = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
                return f.StartsWith(d, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Puts back what Stackshot changed in Windows for this user: the .md/.xml/.mkv associations and, if Stackshot took
        // it, the Print Screen key for Snipping Tool. Safe to run when nothing was changed (also `--cleanup-shell`, for the
        // MSI's uninstall). True when Print Screen was given back.
        public static bool CleanShell()
        {
            FileAssoc.Disable(FileAssoc.Markdown);
            FileAssoc.Disable(FileAssoc.Xml);
            FileAssoc.Disable(FileAssoc.Video);
            bool back = RestorePrintScreen();
            try
            {
                bool empty;
                using (RegistryKey k = Hkcu.OpenSubKey(MsiKey))
                    empty = k != null && k.SubKeyCount == 0 && k.ValueCount == 0;
                if (empty && !Settings.ReadOnly) Hkcu.DeleteSubKey(MsiKey, false);
            }
            catch { }
            return back;
        }

        const string Keyboard = @"Control Panel\Keyboard", PrintValue = "PrintScreenKeyForSnippingEnabled", FreedFlag = "FreedPrintScreen";

        // FreedFlag remembers what the value was before Stackshot set it to 0: 1, or "none" when it wasn't there.
        internal static bool RestorePrintScreen()
        {
            if (Settings.ReadOnly) return false;
            try
            {
                string before;
                using (RegistryKey k = Hkcu.OpenSubKey(MsiKey))
                    before = k == null ? null : Convert.ToString(k.GetValue(FreedFlag));
                if (string.IsNullOrEmpty(before)) return false;
                using (RegistryKey kb = Hkcu.CreateSubKey(Keyboard))
                {
                    object now = kb.GetValue(PrintValue);
                    // Changed back by the user meanwhile: theirs to keep.
                    if (now is int && (int)now != 0) before = null;
                    else if (before == "none") kb.DeleteValue(PrintValue, false);
                    else kb.SetValue(PrintValue, 1, RegistryValueKind.DWord);
                }
                using (RegistryKey k = Hkcu.OpenSubKey(MsiKey, true))
                    if (k != null) k.DeleteValue(FreedFlag, false);
                if (before == null) return false;
                KeyboardChanged();
                ShotStack.Log("Impr Pant devuelta a Recortes");
                return true;
            }
            catch (Exception ex) { ShotStack.Log("Devolver Impr Pant: " + ex.Message); return false; }
        }

        static void KeyboardChanged()
        {
            if (HkcuOverride != null) return;
            try
            {
                UIntPtr result;
                SendMessageTimeout((IntPtr)0xFFFF, 0x001A, UIntPtr.Zero, "Keyboard", 0x0002, 1000, out result); // WM_SETTINGCHANGE, SMTO_ABORTIFHUNG
            }
            catch { }
        }

        // Windows 11 maps Print Screen to Snipping Tool. Stackshot can take it over (current user only).
        public static bool SnippingOwnsPrintScreen
        {
            get
            {
                object v = null;
                try
                {
                    using (RegistryKey k = Hkcu.OpenSubKey(Keyboard))
                        if (k != null) v = k.GetValue(PrintValue);
                }
                catch { }
                if (v is int) return (int)v != 0;
                return Environment.OSVersion.Version.Build >= 22000; // on by default on Windows 11
            }
        }

        // Windows 11 gives Print Screen to the Snipping Tool by default; this hands it back (current user only) and tells
        // the shell the keyboard setting changed, so it applies right away where Windows supports it. What it was before
        // is remembered, so uninstalling gives it back.
        public static void FreePrintScreen()
        {
            try
            {
                using (RegistryKey kb = Hkcu.CreateSubKey(Keyboard))
                {
                    object was = kb.GetValue(PrintValue);
                    if (!(was is int) || (int)was != 0)
                        using (RegistryKey k = Hkcu.CreateSubKey(MsiKey))
                            if (k.GetValue(FreedFlag) == null) k.SetValue(FreedFlag, was is int ? "1" : "none");
                    kb.SetValue(PrintValue, 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { ShotStack.Log("Impr Pant: " + ex.Message); return; }
            KeyboardChanged();
            ShotStack.Log("Impr Pant liberada de Recortes");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, UIntPtr wParam, string lParam, int flags, int timeout, out UIntPtr result);
    }
}
