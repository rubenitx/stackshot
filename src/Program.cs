// Stackshot - Arranque: instalación, actualización, bienvenida y parámetros.
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
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace Stackshot
{
    // Stackshot.exe                    la primera vez, bienvenida e instalación; después, arranca la pila
    //            --install [--startup] [--folder <ruta>] [--no-start]   instala sin preguntar (despliegues)
    //            --uninstall [--quiet] desinstala (lo usa Configuración > Aplicaciones)
    //            --restart             pide a la que esté en marcha que se cierre y ocupa su sitio
    //            --portable            funciona desde donde esté, sin instalarse
    //            --edit <imagen>       (o arrastrar una imagen sobre el .exe) abre solo el editor
    //            --test                no se oculta de las capturas, no toca el portapapeles ni los atajos
    //            --background          arranca sin enseñar la ventana (el acceso de Inicio de Windows lo usa)
    //            --home                con --test, abre la ventana igualmente
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
            // A partir de aquí, las DLL de Windows solo se cargan de System32 (LOAD_LIBRARY_SEARCH_SYSTEM32): una DLL con el
            // mismo nombre puesta junto al .exe (en Descargas) ya no se usa. Las que .NET carga antes de llegar aquí no
            // dependen de nosotros; por eso, además, el .exe se instala en su propia carpeta.
            try { Native.SetDefaultDllDirectories(0x800); } catch { }
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // PER_MONITOR_AWARE_V2 (el manifiesto ya lo pide)
            Directory.CreateDirectory(Settings.DataDir);
            ShotStack.LogPath = Settings.LogFile;
            Theme.Init();
            ShotStack.AppIcon = ShotStack.LoadAppIcon();
            Application.EnableVisualStyles();
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

            // Una imagen o un vídeo arrastrado sobre el .exe (o --edit): solo el editor.
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
            Settings.ReadOnly = test; // las pruebas nunca tocan los ajustes de verdad
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
                if (Installer.IsInstalled && s.FirstRunDone)
                {
                    // Ya está instalado: si este es más nuevo, se actualiza; si no, se abre el instalado.
                    if (Installer.MyVersion > Installer.InstalledVersion)
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
                // Copiado a mano a la carpeta de instalación, sin pasar por la bienvenida.
                bool startup;
                if (!SetupWindow.Welcome(s, out startup)) return 0;
                Installer.Register(startup);
            }

            System.Threading.Mutex mutex = new System.Threading.Mutex(false, Installer.MutexName);
            bool mine;
            try { mine = mutex.WaitOne(0); }
            catch (System.Threading.AbandonedMutexException) { mine = true; }
            if (!mine)
            {
                if (!Has("--restart") && !test)
                {
                    // Ya hay una en marcha: que enseñe su ventana (salvo al arrancar con Windows).
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
