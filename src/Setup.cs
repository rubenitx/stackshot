// Stackshot - Instalación por usuario (sin administrador), bienvenida y ajustes.
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
    // Se instala en %LOCALAPPDATA%\Programs\Stackshot, como cualquier programa por usuario: acceso en el menú Inicio,
    // arranque con Windows opcional y entrada en Configuración > Aplicaciones para desinstalarlo.
    public static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Stackshot";
        public const string QuitEvent = "Local\\Stackshot.Quit";
        public const string MutexName = "Local\\Stackshot";
        public const string ShowEvent = "Local\\Stackshot.Show";

        public static string StartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Stackshot.lnk"); } }
        public static string MenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Stackshot.lnk"); } }
        public static string ExePath { get { return Path.GetFullPath(Application.ExecutablePath); } }

        public static bool RunningInstalled
        {
            get { return string.Equals(ExePath, Path.GetFullPath(Settings.InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsInstalled { get { return File.Exists(Settings.InstalledExe); } }
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
                    try { File.Copy(ExePath, Settings.InstalledExe, true); last = null; break; }
                    catch (Exception ex) { last = ex; System.Threading.Thread.Sleep(200); }
                }
                if (last != null) throw last;
                // Quien lo ha descargado ya ha dado permiso al abrirlo: la copia instalada no vuelve a preguntar.
                Native.DeleteFile(Settings.InstalledExe + ":Zone.Identifier");
            }
            Register(startup);
            ShotStack.Log("Instalado " + MyVersion + " en " + Settings.InstallDir);
        }

        // Accesos directos y entrada de desinstalación, apuntando a la copia instalada.
        public static void Register(bool startup)
        {
            string exe = Settings.InstalledExe;
            CreateShortcut(MenuLink, exe, "Stackshot: capturas de pantalla, v\u00EDdeo y GIF", "");
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

        // Al arrancar desde la carpeta de instalación: si alguien borró un acceso directo, se rehace.
        public static void Repair()
        {
            if (!RunningInstalled) return;
            try
            {
                if (!File.Exists(MenuLink) || Registry.CurrentUser.OpenSubKey(UninstallKey) == null) Register(StartupEnabled);
                else if (StartupEnabled) SetStartup(true); // los accesos de versiones anteriores no llevaban --background
            }
            catch (Exception ex) { ShotStack.Log("Reparar instalaci\u00F3n: " + ex.Message); }
        }

        // El acceso de Inicio arranca en segundo plano (sin enseñar la ventana).
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

        // Pide a la que esté en marcha que enseñe su ventana.
        public static void SignalShow()
        {
            try { System.Threading.EventWaitHandle.OpenExisting(ShowEvent).Set(); }
            catch { }
        }

        // Pide a la que esté en marcha que se cierre (deja el portapapeles en su sitio) y espera a que lo haga.
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
            Settings s = Settings.Load();
            if (!quiet && MessageBox.Show("\u00BFDesinstalar Stackshot?\n\nLas capturas que hayas guardado en " + s.SaveFolder + " no se borran.",
                                          "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            QuitRunning();
            try { if (File.Exists(StartupLink)) File.Delete(StartupLink); } catch { }
            try { if (File.Exists(MenuLink)) File.Delete(MenuLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { if (Directory.Exists(Settings.DataDir)) Directory.Delete(Settings.DataDir, true); } catch { }
            // El propio .exe no puede borrarse mientras corre: lo borra una consola oculta un par de segundos después.
            try
            {
                // Lo reintenta cada segundo durante medio minuto, en cuanto este proceso termine.
                string dir = Settings.InstallDir.TrimEnd('\\');
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/d /c for /l %i in (1,1,30) do (ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"" + dir + "\" 2>nul & if not exist \"" + dir + "\" exit)");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
            if (!quiet) MessageBox.Show("Stackshot se ha desinstalado. \u00A1Gracias por probarlo!", "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Windows 11 usa Impr Pant para abrir Recortes. Stackshot puede quedársela (solo en este usuario).
        public static bool SnippingOwnsPrintScreen
        {
            get
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", null);
                if (v is int) return (int)v != 0;
                return Environment.OSVersion.Version.Build >= 22000; // en Windows 11 viene activado de serie
            }
        }

        public static void FreePrintScreen()
        {
            try { Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord); }
            catch (Exception ex) { ShotStack.Log("Impr Pant: " + ex.Message); }
        }
    }

    // Bienvenida (la primera vez que se abre el .exe descargado): lo básico para empezar e instalar. El resto de
    // ajustes está en la ventana principal.
    public class SetupWindow : DarkForm
    {
        readonly Settings settings;
        readonly Toggle startup, printScreen, sound, copy;
        readonly Label folderLabel;
        readonly Image logo;
        string folder;

        public bool StartWithWindows { get { return startup.Checked; } }

        // Devuelve false si se cierra sin instalar.
        public static bool Welcome(Settings s, out bool startWithWindows)
        {
            using (SetupWindow w = new SetupWindow(s))
            {
                bool ok = w.ShowDialog() == DialogResult.OK;
                startWithWindows = w.StartWithWindows;
                return ok;
            }
        }

        SetupWindow(Settings s) : base(520, 668)
        {
            settings = s;
            folder = s.SaveFolder;
            logo = ShotStack.LoadResourceImage("logo.png");
            int x = 36, w = 448;
            Label t = AddLabel("Bienvenido a Stackshot", 0, 140, 520, 26, Theme.Fg, true, ContentAlignment.TopCenter);
            t.Font = new Font(Fonts.DisplaySemibold, P(27), GraphicsUnit.Pixel);
            t.Height = P(40);
            AddLabel("Captura, marca y comparte en segundos.\nPulsa Impr Pant y listo.", 40, 186, 440, 14, Theme.Fg2, false, ContentAlignment.TopCenter);
            int y = 252;

            startup = AddToggleRow(x, ref y, w, "Iniciar con Windows", "Stackshot se abre solo (en segundo plano) al encender el equipo.", true);
            printScreen = null;
            if (Installer.SnippingOwnsPrintScreen)
                printScreen = AddToggleRow(x, ref y, w, "Usar la tecla Impr Pant", "Windows la usa para Recortes; Stackshot se la queda solo en tu usuario.", true);
            sound = AddToggleRow(x, ref y, w, "Sonido al capturar", "Un peque\u00F1o clic de c\u00E1mara.", s.Sound);
            copy = AddToggleRow(x, ref y, w, "Copiar cada captura", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla.", s.CopyToClipboard);

            y += 6;
            AddLabel("Tus capturas guardadas", x, y, w - 116, 14, Theme.Fg, true, ContentAlignment.TopLeft);
            folderLabel = AddLabel("", x, y + 22, w - 116, 12, Theme.Muted, false, ContentAlignment.TopLeft);
            folderLabel.AutoEllipsis = true;
            folderLabel.Height = P(20);
            ShowFolder();
            Pill change = MakePill("Cambiar\u2026", false, x + w - 104, y + 4, 104, 34);
            change.Click += delegate { PickFolder(); };
            y += 68;

            // Lo básico, para empezar sin leer nada más.
            string[,] keys = { { Hotkeys.Display(s.HotRegion), "Capturar un \u00E1rea (o clic en una ventana)" },
                               { Hotkeys.Display(s.HotVideo), "Grabar v\u00EDdeo" },
                               { "Clic en la miniatura", "Editar: flechas, recuadros, n\u00FAmeros, texto\u2026" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                Label k = AddLabel(keys[i, 0], x, y, 170, 12, Theme.Accent, true, ContentAlignment.TopLeft);
                k.Height = P(18);
                Label d = AddLabel(keys[i, 1], x + 176, y, w - 176, 12, Theme.Fg2, false, ContentAlignment.TopLeft);
                d.Height = P(18);
                y += 26;
            }

            Pill ok = MakePill("Instalar y empezar", true, 286, 598, 198, 42);
            ok.Click += delegate { Save(); };
            Pill cancel = MakePill("Ahora no", false, 36, 598, 120, 42);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            AcceptButton = null;
        }

        Pill MakePill(string text, bool accent, int x, int y, int w, int h)
        {
            Pill p = new Pill(text, accent);
            p.Font = new Font("Segoe UI Semibold", P(14), GraphicsUnit.Pixel);
            p.Bounds = new Rectangle(P(x), P(y), P(w), P(h));
            Controls.Add(p);
            p.BringToFront(); // por encima de cualquier etiqueta que pase por detrás
            return p;
        }

        // Fila con título, explicación y un interruptor a la derecha.
        Toggle AddToggleRow(int x, ref int y, int w, string title, string desc, bool value)
        {
            AddLabel(title, x, y, w - 70, 14, Theme.Fg, true, ContentAlignment.TopLeft);
            Label d = AddLabel(desc, x, y + 21, w - 70, 12, Theme.Muted, false, ContentAlignment.TopLeft);
            Toggle t = new Toggle(value);
            t.Bounds = new Rectangle(P(x + w - 46), P(y + 8), P(46), P(26));
            Controls.Add(t);
            y += Math.Max(54, 21 + (int)Math.Round(d.Height / s) + 14);
            return t;
        }

        void ShowFolder()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            folderLabel.Text = folder.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + folder.Substring(home.Length) : folder;
        }

        void PickFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "\u00BFD\u00F3nde quieres guardar las capturas que conserves?";
                d.ShowNewFolderButton = true;
                try { Directory.CreateDirectory(folder); d.SelectedPath = folder; } catch { }
                if (d.ShowDialog(this) == DialogResult.OK) { folder = d.SelectedPath; ShowFolder(); }
            }
        }

        void Save()
        {
            settings.SaveFolder = folder;
            settings.Sound = sound.Checked;
            settings.CopyToClipboard = copy.Checked;
            settings.FirstRunDone = true;
            settings.Save();
            if (printScreen != null && printScreen.Checked) Installer.FreePrintScreen();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // Un halo suave detrás del logo.
            Rectangle halo = new Rectangle(ClientSize.Width / 2 - P(150), P(-40), P(300), P(220));
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(halo);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(70, 110, 120, 255);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, Theme.Bg) };
                    g.FillEllipse(pb, halo);
                }
            }
            int d = P(96);
            Rectangle lr = new Rectangle((ClientSize.Width - d) / 2, P(30), d, d);
            if (logo != null) g.DrawImage(logo, lr);
            else if (ShotStack.AppIcon != null) g.DrawIcon(ShotStack.AppIcon, lr);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (logo != null) logo.Dispose();
        }
    }
}
