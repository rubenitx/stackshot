// Stackshot - Entry point: install, update, welcome and command-line options.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Stackshot")]
[assembly: AssemblyProduct("Stackshot")]
[assembly: AssemblyDescription("Capturas de pantalla, v\u00EDdeo y GIF para Windows, con miniaturas flotantes y editor r\u00E1pido")]
[assembly: AssemblyCompany("rubenitx")]
[assembly: AssemblyCopyright("Copyright \u00A9 2026 rubenitx \u00B7 MIT License")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace Stackshot
{
    // Stackshot.exe            first run: welcome and install; afterwards, starts the stack
    //   --install [--startup] [--folder <path>] [--no-start]   silent install (deployments)
    //   --uninstall [--quiet]  uninstall (used by Settings > Apps)
    //   --restart              ask the running instance to quit and take its place
    //   --portable             run in place without installing
    //   --edit <file>          editor only (same as dropping a file on the .exe)
    //   --test                 not excluded from capture; leaves clipboard and hotkeys alone
    //   --background           start without showing the window (startup shortcut)
    //   --home                 with --test, show the window anyway
    public static class Program
    {
        public const string RepoUrl = "https://github.com/rubenitx/stackshot";

        static string[] args;

        static bool Has(string name)
        {
            foreach (string a in args) if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string Value(string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static void Init()
        {
            // From here on, Windows DLLs load only from System32 (LOAD_LIBRARY_SEARCH_SYSTEM32), so a same-named DLL
            // next to the .exe (e.g. in Downloads) is ignored. DLLs .NET loads earlier are out of our control, which is
            // why the .exe is installed into its own folder.
            try { Native.SetDefaultDllDirectories(0x800); } catch { }
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (also in the manifest)
            Directory.CreateDirectory(Settings.DataDir);
            ShotStack.LogPath = Settings.LogFile;
            Theme.Init();
            ShotStack.AppIcon = ShotStack.LoadAppIcon();
            Application.EnableVisualStyles();            // One reusable back buffer for every double-buffered window (the default only caches tiny ones).            BufferedGraphicsManager.Current.MaximumBuffer = new System.Drawing.Size(1400, 1000);
            try { Application.SetCompatibleTextRenderingDefault(false); } catch { }
            try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }
            Application.ThreadException += delegate(object o, System.Threading.ThreadExceptionEventArgs e) { ShotStack.Log("Error: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object o, UnhandledExceptionEventArgs e) { ShotStack.Log("Error grave: " + e.ExceptionObject); };
        }

        [STAThread]
        public static int Main(string[] arguments)
        {
            args = arguments;
            Init();

            if (Has("--uninstall")) { Installer.Uninstall(Has("--quiet")); return 0; }

            // An image or video dropped on the .exe (or --edit): editor only.
            string edit = Value("--edit");
            if (edit == null && args.Length == 1 && File.Exists(args[0]) && ShotStack.IsEditable(args[0])) edit = args[0];
            if (edit != null)
            {
                try
                {
                    if (ShotStack.IsMediaFile(edit))
                    {
                        Editor v = Editor.ForVideo(null, Path.GetFullPath(edit));
                        if (v != null) Application.Run(v);
                    }
                    else Application.Run(new Editor(null, Path.GetFullPath(edit), ShotStack.LoadFull(edit)));
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Editar " + edit + ": " + ex);
                    MessageBox.Show("No se pudo abrir " + Path.GetFileName(edit) + ":\n" + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return 0;
            }

            bool test = Has("--test"), portable = Has("--portable") || test;
            Settings.ReadOnly = test; // tests never write the real settings
            Settings s = Settings.Load();

            if (Has("--install"))
            {
                string folder = Value("--folder");
                if (folder != null) s.SaveFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder));
                s.FirstRunDone = true;
                s.Save();
                Installer.Install(Has("--startup"));
                if (!Has("--no-start")) Process.Start(Settings.InstalledExe, "--welcome-done");
                return 0;
            }

            if (!portable && !Installer.RunningInstalled)
            {
                if (Installer.IsInstalled && (s.FirstRunDone || Installer.ManagedByMsi))
                {
                    // Already installed: update if this build is newer, otherwise open the installed one.
                    // MSI installs are updated through the MSI, never overwritten here.
                    if (Installer.MyVersion > Installer.InstalledVersion && Installer.ManagedByMsi)
                    {
                        MessageBox.Show("Este equipo tiene Stackshot " + Installer.InstalledVersion.ToString(3) + " instalado con el paquete MSI.\n\n" +
                                        "Para pasar a la " + Installer.MyVersion.ToString(3) + ", instala Stackshot.msi de esa versi\u00F3n (o p\u00EDdeselo a inform\u00E1tica).",
                                        "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (Installer.MyVersion > Installer.InstalledVersion)
                    {
                        if (MessageBox.Show("\u00BFActualizar Stackshot de la versi\u00F3n " + Installer.InstalledVersion.ToString(3) + " a la " +
                                            Installer.MyVersion.ToString(3) + "?", "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                        Installer.Install(Installer.StartupEnabled);
                    }
                    Process.Start(Settings.InstalledExe);
                    return 0;
                }
                bool startup;
                if (!SetupWindow.Welcome(s, out startup)) return 0;
                Installer.Install(startup);
                Process.Start(Settings.InstalledExe, "--welcome-done");
                return 0;
            }

            if (!portable && !s.FirstRunDone)
            {
                if (Installer.ManagedByMsi)
                {
                    // Installed by the MSI: shortcuts and startup are already set; skip the welcome.
                    s.FirstRunDone = true;
                    s.Save();
                }
                else
                {
                    // Copied into the install folder by hand, without the welcome.
                    bool startup;
                    if (!SetupWindow.Welcome(s, out startup)) return 0;
                    Installer.Register(startup);
                }
            }

            System.Threading.Mutex mutex = new System.Threading.Mutex(false, Installer.MutexName);
            bool mine;
            try { mine = mutex.WaitOne(0); }
            catch (System.Threading.AbandonedMutexException) { mine = true; }
            if (!mine)
            {
                if (!Has("--restart") && !test)
                {
                    // Already running: ask it to show its window (unless starting with Windows).
                    if (!Has("--background")) Installer.SignalShow();
                    return 0;
                }
                if (!test)
                {
                    Installer.QuitRunning();
                    try { mine = mutex.WaitOne(10000); }
                    catch (System.Threading.AbandonedMutexException) { mine = true; }
                    if (!mine) return 1;
                }
            }
            try
            {
                if (!portable) Installer.Repair();
                ShotStack.Test = test;
                FloatWindow.ExcludeFromCapture = !test;
                Card.ForceHover = false;
                Application.Run(new ShotStack(s, Has("--welcome-done"), (!Has("--background") && !test) || Has("--home")));
                ShotStack.Log("Cerrada");
            }
            catch (Exception ex) { ShotStack.Log("ERROR " + ex); }
            finally
            {
                if (mine) { try { mutex.ReleaseMutex(); } catch { } }
            }
            return 0;
        }
    }
}
