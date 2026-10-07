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

        // Writes a new file with the contents only, like any installer: alternate data streams are not carried over,
        // so the installed copy needs no stream manipulation afterwards.
        static void CopyContents(string from, string to)
        {
            string tmp = to + ".new";
            File.WriteAllBytes(tmp, File.ReadAllBytes(from));
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(tmp, to);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        // Shortcuts and uninstall entry pointing to the installed copy.
        public static void Register(bool startup)
        {
            if (ManagedByMsi) return; // the MSI handles this
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

        // When running from the install folder, recreate missing shortcuts.
        public static void Repair()
        {
            if (!RunningInstalled || ManagedByMsi) return;
            try
            {
                if (!File.Exists(MenuLink) || Registry.CurrentUser.OpenSubKey(UninstallKey) == null) Register(StartupEnabled);
                else if (StartupEnabled) SetStartup(true); // older shortcuts lacked --background
            }
            catch (Exception ex) { ShotStack.Log("Reparar instalaci\u00F3n: " + ex.Message); }
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

        // Ask the running instance to show its window.
        public static void SignalShow()
        {
            try { System.Threading.EventWaitHandle.OpenExisting(ShowEvent).Set(); }
            catch { }
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

        public static void FreePrintScreen()
        {
            try { Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord); }
            catch (Exception ex) { ShotStack.Log("Impr Pant: " + ex.Message); }
        }
    }

    // Welcome window (first run of the downloaded .exe): the basics to get started. Everything else lives in the main
    // window.
    public class SetupWindow : DarkForm
    {
        readonly Settings settings;
        readonly Toggle startup, printScreen, sound, copy;
        readonly Label folderLabel;
        readonly Image logo;
        string folder;

        public bool StartWithWindows { get { return startup.Checked; } }

        // Returns false if closed without installing.
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
            int y = 256;

            // Grouped card like macOS Settings: toggles and the folder, separated by hairlines.
            int top = y;
            startup = AddToggleRow(x, ref y, w, "Iniciar con Windows", "Se abre solo, en segundo plano, al encender el equipo.", true);
            printScreen = null;
            if (Installer.SnippingOwnsPrintScreen)
                printScreen = AddToggleRow(x, ref y, w, "Usar la tecla Impr Pant", "Windows la usa para Recortes; Stackshot se la queda solo en tu usuario.", true);
            sound = AddToggleRow(x, ref y, w, "Sonido al capturar", "Un peque\u00F1o clic de c\u00E1mara.", s.Sound);
            copy = AddToggleRow(x, ref y, w, "Copiar cada captura", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla.", s.CopyToClipboard);
            separators.Add(y - 9);
            AddLabel("Carpeta de capturas", x, y, w - 116, 14, Theme.Fg, true, ContentAlignment.TopLeft);
            folderLabel = AddLabel("", x, y + 21, w - 116, 12, Theme.Muted, false, ContentAlignment.TopLeft);
            folderLabel.AutoEllipsis = true;
            folderLabel.Height = P(20);
            ShowFolder();
            Pill change = MakePill("Cambiar\u2026", false, x + w - 96, y + 3, 96, 32);
            change.Font = new Font("Segoe UI Semibold", P(13), GraphicsUnit.Pixel);
            change.Click += delegate { PickFolder(); };
            y += 40;
            cards.Add(new Rectangle(x - 16, top - 14, w + 32, y - top + 28));
            y += 48;

            // The essentials as key caps, so there's nothing else to read.
            top = y;
            string[][] caps = { Hotkeys.Display(s.HotRegion).Split('+'), Hotkeys.Display(s.HotVideo).Split('+'), new string[] { "Clic" } };
            string[] what = { "Capturar un \u00E1rea o una ventana", "Grabar la pantalla", "en la miniatura para editarla" };
            for (int i = 0; i < caps.Length; i++)
            {
                if (i > 0) separators.Add(y - 7);
                keys.Add(new KeyValuePair<Point, string[]>(new Point(x, y), caps[i]));
                AddLabel(what[i], x + 196, y + 3, w - 196, 13, Theme.Fg2, false, ContentAlignment.TopLeft).Height = P(20);
                y += 34;
            }
            cards.Add(new Rectangle(x - 16, top - 12, w + 32, y - top + 14));
            y += 28;

            Pill ok = MakePill("Instalar y empezar", true, 286, y, 198, 42);
            ok.Click += delegate { Save(); };
            Pill cancel = MakePill("Ahora no", false, 36, y, 120, 42);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            AcceptButton = null;
            // Height follows the content (one more row when Windows owns Print Screen).
            ClientSize = new Size(ClientSize.Width, P(y + 42 + 30));
            Rectangle wa = Grabber.CurrentScreen().WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + Math.Max(0, (wa.Height - Height) / 2));
        }

        readonly List<Rectangle> cards = new List<Rectangle>();
        readonly List<int> separators = new List<int>();
        readonly List<KeyValuePair<Point, string[]>> keys = new List<KeyValuePair<Point, string[]>>();
        int toggleRows;

        Pill MakePill(string text, bool accent, int x, int y, int w, int h)
        {
            Pill p = new Pill(text, accent);
            p.Font = new Font("Segoe UI Semibold", P(14), GraphicsUnit.Pixel);
            p.Bounds = new Rectangle(P(x), P(y), P(w), P(h));
            Controls.Add(p);
            p.BringToFront(); // above any label behind it
            return p;
        }

        // Row with title, description and a toggle on the right.
        Toggle AddToggleRow(int x, ref int y, int w, string title, string desc, bool value)
        {
            AddLabel(title, x, y, w - 70, 14, Theme.Fg, true, ContentAlignment.TopLeft);
            Label d = AddLabel(desc, x, y + 21, w - 70, 12, Theme.Muted, false, ContentAlignment.TopLeft);
            Toggle t = new Toggle(value);
            t.Bounds = new Rectangle(P(x + w - 46), P(y + 8), P(46), P(26));
            Controls.Add(t);
            if (toggleRows++ > 0) separators.Add(y - 9);
            y += Math.Max(56, 21 + (int)Math.Round(d.Height / s) + 18);
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
            // Soft halo behind the logo.
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
            foreach (Rectangle c in cards)
            {
                Rectangle r = new Rectangle(P(c.X), P(c.Y), P(c.Width), P(c.Height));
                using (GraphicsPath p = Theme.Round(r, P(12)))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 41))) g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(14, 255, 255, 255))) g.DrawPath(pen, p);
                }
            }
            using (Pen hair = new Pen(Color.FromArgb(52, 52, 56), Math.Max(1f, s)))
                foreach (int sy in separators) g.DrawLine(hair, P(36), P(sy), P(36 + 448), P(sy));
            Font kf = Fonts.Get("Segoe UI Semibold", P(12));
            foreach (KeyValuePair<Point, string[]> k in keys)
            {
                int kx = P(k.Key.X);
                foreach (string raw in k.Value)
                {
                    string cap = raw.Trim();
                    int kw = Math.Max(P(26), TextRenderer.MeasureText(cap, kf).Width + P(8));
                    Rectangle kr = new Rectangle(kx, P(k.Key.Y), kw, P(24));
                    using (GraphicsPath p = Theme.Round(new Rectangle(kr.X, kr.Y + P(2), kr.Width, kr.Height), P(6)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 24, 26))) g.FillPath(b, p); // key edge
                    using (GraphicsPath p = Theme.Round(kr, P(6)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(62, 62, 66))) g.FillPath(b, p);
                    TextRenderer.DrawText(g, cap, kf, kr, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    kx += kw + P(5);
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
