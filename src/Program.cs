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
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace Stackshot
{
    // Stackshot.exe            first run: welcome and install; afterwards, starts the stack
    //   --install [--startup] [--folder <path>] [--no-start]   silent install (deployments)
    //   --install --updated [--home]   in-app update: the new copy installs itself and starts again with --updated
    //   --updated              started by an update: quietly, without the window (--home: on Acerca de)
    //   --update-failed <ver>  started again after the update to <ver> failed (it is then offered by hand)
    //   --uninstall [--quiet]  uninstall (used by Settings > Apps)
    //   --restart              ask the running instance to quit and take its place
    //   --portable             run in place without installing
    //   --edit <file>          editor only (same as dropping a file on the .exe)
    //   <file.md|.xml>         opens the viewer on that file (in the running Stackshot when there is one); --open <file> is the same
    //   --test                 not excluded from capture; leaves clipboard, hotkeys and the single-instance lock alone
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
            // WPF windows follow per-monitor DPI changes (the default for apps that declare .NET 4.6.2 or later).
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
            // From here on, Windows DLLs load only from System32 (LOAD_LIBRARY_SEARCH_SYSTEM32), so a same-named DLL
            // next to the .exe (e.g. in Downloads) is ignored. DLLs .NET loads earlier are out of our control, which is
            // why the .exe is installed into its own folder.
            try { Native.SetDefaultDllDirectories(0x800); } catch { }
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (also in the manifest)
            Directory.CreateDirectory(Settings.DataDir);
            if (ShotStack.LogPath == null) ShotStack.LogPath = Settings.LogFile; // tooling may point it elsewhere, before or after
            Theme.Init();
            ShotStack.AppIcon = ShotStack.LoadAppIcon();
            Application.EnableVisualStyles();
            // One reusable back buffer for every double-buffered window (the default only caches tiny ones).
            System.Drawing.BufferedGraphicsManager.Current.MaximumBuffer = new System.Drawing.Size(1400, 1000);
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

            bool test = Has("--test"), portable = Has("--portable") || test, updated = Has("--updated");
            Settings.ReadOnly = test; // tests never write the real settings

            // An image or video dropped on the .exe (or --edit): editor only.
            string edit = Value("--edit");
            if (edit == null && args.Length == 1 && File.Exists(args[0]) && ShotStack.IsEditable(args[0])) edit = args[0];
            if (edit != null)
            {
                Ds.Apply(Settings.Load().Appearance); // light or dark as chosen in the app, not only as Windows says
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

            // A Markdown or XML file (double click, "Open with", the command line): the viewer opens it, in the running
            // Stackshot when there is one.
            string view = null;
            if (args.Length == 1 && File.Exists(args[0]) && Installer.ViewerFile(args[0])) view = Path.GetFullPath(args[0]);
            else if (Value("--open") != null && File.Exists(Value("--open")) && Installer.ViewerFile(Value("--open"))) view = Path.GetFullPath(Value("--open"));
            if (view != null) ShotStack.OpenAtStart = view;

            Settings s = Settings.Load();
            Ds.Apply(s.Appearance);

            if (Has("--install"))
            {
                string folder = Value("--folder");
                if (folder != null)
                {
                    try { s.SaveFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder)); }
                    catch (Exception ex) { ShotStack.Log("Instalar, carpeta " + folder + ": " + ex.Message); folder = null; }
                }
                // Written only when something changes: an update runs next to the copy it replaces, which may be saving.
                // An in-app update comes from a copy past its welcome, so a FirstRunDone that reads false there is a
                // misread file, never a reason to write (the defaults) over it.
                if (folder != null || (!s.FirstRunDone && !updated))
                {
                    s.FirstRunDone = true;
                    s.Save();
                }
                string home = Has("--home") ? " --home" : "";
                bool wasRunning = !updated && Installer.Running; // older versions update with a plain --install
                try { Installer.Install(Has("--startup")); }
                catch (Exception ex)
                {
                    ShotStack.Log("Instalar " + Installer.MyVersion.ToString(3) + ": " + ex);
                    // The install has already asked the running copy to close: open it again (taking over from it if it
                    // is still on its way out), so Stackshot never just disappears. After an in-app update it is also
                    // told this version could not be installed (it is offered by hand from then on).
                    if (updated) Start(Settings.InstalledExe, "--updated --restart --update-failed " + Installer.MyVersion.ToString(3) + home);
                    else if (wasRunning && Installer.IsInstalled) Start(Settings.InstalledExe, "--background --restart");
                    return 1;
                }
                if (!Has("--no-start")) Start(Settings.InstalledExe, updated ? "--updated" + home : "--welcome-done");
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
                        if (!TryInstall(Installer.StartupEnabled)) return 1;
                    }
                    Start(Settings.InstalledExe, view != null ? "\"" + view + "\"" : null);
                    return 0;
                }
                bool startup;
                if (!SetupWindow.Welcome(s, out startup)) return 0;
                if (!TryInstall(startup)) return 1;
                Start(Settings.InstalledExe, "--welcome-done");
                return 0;
            }

            // One Stackshot at a time (test runs stay out of it, so they never keep the real one from starting).
            System.Threading.Mutex mutex = null;
            bool mine = false;
            if (!test)
            {
                mutex = new System.Threading.Mutex(false, Installer.MutexName);
                try { mine = mutex.WaitOne(0); }
                catch (System.Threading.AbandonedMutexException) { mine = true; }
                if (!mine)
                {
                    if (!Has("--restart"))
                    {
                        // Already running: ask it to show its window (unless starting with Windows or after an update).
                        // The request is written first, so the running copy finds it even while it is still starting.
                        if (view != null) { if (!Installer.SendOpen(view, 3000)) Installer.SignalShow(); }
                        else if (!Has("--background") && !updated) Installer.SignalShow();
                        return 0;
                    }
                    Installer.QuitRunning();
                    try { mine = mutex.WaitOne(10000); }
                    catch (System.Threading.AbandonedMutexException) { mine = true; }
                    if (!mine) return 1;
                    // Read again: the copy it replaced may have saved something on its way out.
                    s = Settings.Load();
                    Ds.Apply(s.Appearance);
                }
            }
            // Created as soon as the lock is ours, so a second copy launched right after finds it.
            if (mine)
            {
                try { ShotStack.OpenHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.OpenEvent); }
                catch (Exception ex) { ShotStack.Log("Evento de abrir: " + ex.Message); }
            }
            try
            {
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
                        // Copied into the install folder by hand, without the welcome (once: the lock is already held).
                        bool startup;
                        if (!SetupWindow.Welcome(s, out startup)) return 0;
                        Installer.Register(startup);
                    }
                }
                if (!portable) { Installer.Repair(); FileAssoc.Repair(); }
                ShotStack.Test = test;
                FloatWindow.ExcludeFromCapture = !test;
                Card.ForceHover = false;
                Version failed;
                if (Version.TryParse(Value("--update-failed") ?? "", out failed)) Updater.FailedBefore(failed);
                // After an update it comes back quietly; one asked for by hand reopens on Acerca de, with the new version.
                bool show = (!Has("--background") && !updated && !test && view == null) || Has("--home");
                Application.Run(new ShotStack(s, Has("--welcome-done"), show, updated && Has("--home") ? "about" : null));
                ShotStack.Log("Cerrada");
            }
            catch (Exception ex) { ShotStack.Log("ERROR " + ex); }
            finally
            {
                if (mine) { try { mutex.ReleaseMutex(); } catch { } }
            }
            return 0;
        }

        // Installs this copy; a failure is explained instead of crashing, and a copy it had already closed is reopened.
        static bool TryInstall(bool startup)
        {
            try
            {
                Installer.Install(startup);
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Instalar " + Installer.MyVersion.ToString(3) + ": " + ex);
                MessageBox.Show("No se pudo instalar Stackshot:\n" + ex.Message + "\n\nCierra lo que pueda estar us\u00E1ndolo y vuelve a intentarlo.",
                                "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (Installer.IsInstalled) Start(Settings.InstalledExe, "--background");
                return false;
            }
        }

        static void Start(string exe, string arguments)
        {
            try
            {
                if (arguments == null) Process.Start(exe);
                else Process.Start(exe, arguments);
            }
            catch (Exception ex) { ShotStack.Log("Abrir " + exe + ": " + ex.Message); }
        }
    }
}
