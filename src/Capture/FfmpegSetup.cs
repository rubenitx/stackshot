// Stackshot - Downloads FFmpeg on first use.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WF = System.Windows.Forms;

namespace Stackshot
{
    // Security: pinned version from a fixed URL, verified against the SHA-256 below (it matches the digest GitHub
    // publishes). Nothing is installed on mismatch. Update Version, Url and Sha256 together.
    // Enter downloads, Esc cancels (and stops a download in progress).
    public class FfmpegSetup : Sheet
    {
        const string Version = "9.0.2";
        const string Zip = "ffmpeg-9.0.2-essentials_build.zip";
        const string Url = "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip";
        const string Sha256 = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";
        readonly Settings settings;
        MacButton download, browse, cancel;
        Border track, fill;
        TextBlock status;
        StackPanel page;
        WebClient web;
        string result, zipPath;
        bool installing, busy, failed;
        // What the window shows, so a theme change can rebuild it as it was.
        string statusText = "";
        double progress;
        int shownPermille = -1;
        long shownMb = -1;

        static FfmpegSetup open;

        // s: the running settings (a custom ffmpeg.exe path is stored there). One dialog at a time: asked again while it
        // is up (the record shortcut pressed while the one opened from Home waits), it comes forward and this call
        // returns null; two downloads would write the same file.
        public static string Run(Settings s)
        {
            if (open != null)
            {
                if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
                open.Activate();
                return null;
            }
            FfmpegSetup f = new FfmpegSetup(s);
            open = f;
            try { f.ShowDialog(); }
            finally { open = null; }
            return f.result;
        }

        FfmpegSetup(Settings s)
        {
            settings = s;
            Title = "Stackshot";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Icon = AppIcon;
            // Controls handle their own presses; anything else that reaches the root moves the window.
            DragAnywhere();
            Build();
            KeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Handled || e.IsRepeat) return;
                if (e.Key == Key.Escape) { e.Handled = true; if (!installing) Close(); }
                else if (e.Key == Key.Enter) { e.Handled = true; if (!busy && !installing) Start(); }
            };
            Closing += delegate(object o, System.ComponentModel.CancelEventArgs e)
            {
                if (installing) { e.Cancel = true; return; }
                if (web != null && web.IsBusy) web.CancelAsync();
            };
        }

        protected override void ThemeChanged() { Build(); }

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new StackPanel { Margin = new Thickness(30, 30, 30, 26) };
            Root.Children.Add(page);
            KeepCaptionsOnTop();
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new IconTile("video", 40, Ds.Rgb(255, 85, 100), Ds.Rgb(230, 40, 40)));
            head.Children.Add(Text(I18n.T("Grabar v\u00EDdeo y GIF", "Record video and GIF"), Ds.Title, 20, pal.Label, new Thickness(14, 0, 0, 0)));
            page.Children.Add(head);
            TextBlock p = Text(I18n.Fmt("Para grabar y reproducir v\u00EDdeo, Stackshot usa FFmpeg {0}, un programa libre y gratuito con su propia licencia (GPL). Se descarga una sola vez (unos 110 MB) ",
                                        "To record and play video, Stackshot uses FFmpeg {0}, a free open-source program under its own license (GPL). It is downloaded once (about 110 MB) ", Version) +
                               I18n.T("desde GitHub, se comprueba que es el original y se guarda solo para tu usuario.", "from GitHub, verified to be the original, and saved only for your user account."), Ds.Regular, 13, pal.Label2, new Thickness(0, 16, 0, 0));
            p.TextWrapping = TextWrapping.Wrap;
            p.LineHeight = 19;
            page.Children.Add(p);

            track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Ds.Brush(pal.Control), Margin = new Thickness(0, 22, 0, 0) };
            track.Visibility = busy || installing ? Visibility.Visible : Visibility.Collapsed;
            fill = new Border { CornerRadius = new CornerRadius(3), Background = new LinearGradientBrush(pal.Accent, pal.Purple, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            track.Child = fill;
            track.SizeChanged += delegate { ShowProgress(); };
            page.Children.Add(track);
            status = Text(statusText, Ds.Regular, 12, failed ? pal.Error : pal.Label3, new Thickness(0, 8, 0, 0));
            status.TextWrapping = TextWrapping.Wrap;
            page.Children.Add(status);

            Grid buttons = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            download = new MacButton(failed ? I18n.T("Reintentar", "Retry") : I18n.T("Descargar", "Download"), ButtonKind.Primary, "save", null, 32);
            download.Click += Start;
            download.IsEnabled = !busy && !installing;
            browse = new MacButton(I18n.T("Ya lo tengo\u2026", "I already have it\u2026"), ButtonKind.Secondary, null, null, 32);
            browse.Margin = new Thickness(8, 0, 0, 0);
            browse.ToolTip = I18n.T("Elegir un ffmpeg.exe que ya tengas en el equipo", "Choose an ffmpeg.exe you already have on your PC");
            browse.Click += Browse;
            browse.IsEnabled = !busy && !installing;
            cancel = new MacButton(I18n.Cancel, ButtonKind.Secondary, null, null, 32);
            cancel.Margin = new Thickness(8, 0, 0, 0);
            cancel.Click += delegate { Close(); };
            cancel.IsEnabled = !installing;
            right.Children.Add(download);
            right.Children.Add(browse);
            right.Children.Add(cancel);
            buttons.Children.Add(right);
            page.Children.Add(buttons);
        }

        static TextBlock Text(string s, Typeface face, double size, Color c, Thickness m)
        {
            return new TextBlock { Text = s, FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Foreground = Ds.Brush(c), Margin = m, VerticalAlignment = VerticalAlignment.Center };
        }

        void SetStatus(string text, bool error)
        {
            statusText = text;
            failed = error;
            status.Text = text;
            status.Foreground = Ds.Brush(error ? Ds.Brushes.Error : Ds.Brushes.Label3);
        }

        void ShowProgress()
        {
            double w = track.ActualWidth > 0 ? track.ActualWidth : 420;
            fill.Width = Math.Max(6, w * Math.Max(0, Math.Min(1, progress)));
        }

        void Browse()
        {
            using (WF.OpenFileDialog d = new WF.OpenFileDialog())
            {
                d.Filter = "ffmpeg.exe|ffmpeg.exe";
                d.Title = I18n.T("\u00BFD\u00F3nde est\u00E1 ffmpeg.exe?", "Where is ffmpeg.exe?");
                if (d.ShowDialog(Win32) != WF.DialogResult.OK) return;
                string chosen = d.FileName;
                // Asked off the window's thread: an exe that never answers must not freeze it.
                browse.IsEnabled = download.IsEnabled = false;
                SetStatus(I18n.Fmt("Comprobando {0}\u2026", "Checking {0}\u2026", System.IO.Path.GetFileName(chosen)), false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool ok = AnswersAsFfmpeg(chosen);
                    try
                    {
                        Dispatcher.BeginInvoke((Action)delegate
                        {
                            if (!IsVisible) return;
                            browse.IsEnabled = download.IsEnabled = !busy && !installing;
                            if (!ok) { SetStatus(I18n.T("Ese archivo no responde como FFmpeg. Elige el ffmpeg.exe de una versi\u00F3n completa.", "That file does not respond as FFmpeg. Choose the ffmpeg.exe of a full version."), true); return; }
                            settings.Ffmpeg = chosen;
                            settings.Save();
                            result = chosen;
                            Close();
                        });
                    }
                    catch (Exception ex) { ShotStack.Log("FFmpeg elegido: " + ex.Message); }
                });
            }
        }

        // "ffmpeg -version" answers with its version line within a few seconds.
        static bool AnswersAsFfmpeg(string exe)
        {
            try
            {
                using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                {
                    p.StartInfo.FileName = exe;
                    p.StartInfo.Arguments = "-hide_banner -version";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    System.Threading.Tasks.Task<string> first = p.StandardOutput.ReadLineAsync();
                    bool answered = first.Wait(5000);
                    if (!p.WaitForExit(answered ? 3000 : 0)) { try { p.Kill(); } catch { } }
                    return answered && first.Result != null && first.Result.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                ShotStack.Log("FFmpeg elegido: " + ex.Message);
                return false;
            }
        }

        // A download that stops receiving anything for this long is given up (a proxy that holds the connection open).
        const int StallSeconds = 30;
        DateTime lastBytes;
        bool stalled;
        System.Windows.Threading.DispatcherTimer watch;

        void Start()
        {
            try
            {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Settings.DataDir)));
                if (drive.IsReady && drive.AvailableFreeSpace < 400L * 1048576)
                {
                    SetStatus(I18n.Fmt("Hacen falta unos 400 MB libres en {0} para descargar y preparar FFmpeg.", "Need about 400 MB free on {0} to download and prepare FFmpeg.", drive.Name), true);
                    return;
                }
            }
            catch (Exception ex) { ShotStack.Log("FFmpeg, espacio libre: " + ex.Message); }
            busy = true;
            stalled = false;
            lastBytes = DateTime.UtcNow;
            if (watch == null)
            {
                watch = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                watch.Tick += delegate
                {
                    if (!busy || installing || web == null || !web.IsBusy) return;
                    if ((DateTime.UtcNow - lastBytes).TotalSeconds < StallSeconds) return;
                    stalled = true;
                    web.CancelAsync();
                };
            }
            watch.Start();
            failed = false;
            progress = 0;
            shownPermille = -1;
            shownMb = -1;
            download.IsEnabled = false;
            browse.IsEnabled = false;
            track.Visibility = Visibility.Visible;
            ShowProgress();
            SetStatus(I18n.T("Conectando\u2026", "Connecting\u2026"), false);
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Directory.CreateDirectory(Settings.FfmpegDir);
                zipPath = Path.Combine(Settings.FfmpegDir, Zip);
                if (web != null) web.Dispose();
                web = new WebClient();
                web.Headers[HttpRequestHeader.UserAgent] = "Stackshot";
                if (web.Proxy != null) web.Proxy.Credentials = CredentialCache.DefaultCredentials; // corporate proxies
                web.DownloadProgressChanged += delegate(object o, DownloadProgressChangedEventArgs e)
                {
                    // Raised for every few KB read; the window only changes when a visible number does.
                    lastBytes = DateTime.UtcNow;
                    long total = e.TotalBytesToReceive, mb = e.BytesReceived / 1048576;
                    int permille = total > 0 ? (int)(e.BytesReceived * 1000 / total) : 0;
                    if (permille != shownPermille)
                    {
                        shownPermille = permille;
                        progress = permille / 1000.0;
                        ShowProgress();
                    }
                    if (mb != shownMb)
                    {
                        shownMb = mb;
                        SetStatus(total > 0 ? I18n.Fmt("{0} MB de {1} MB", "{0} MB of {1} MB", mb, total / 1048576) : mb + " MB", false);
                    }
                };
                web.DownloadFileCompleted += delegate(object o, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    if (watch != null) watch.Stop();
                    if (e.Cancelled && stalled && !IsDisposed) { TryDelete(zipPath); Fail(I18n.Fmt("La descarga se ha quedado parada m\u00E1s de {0} s. Comprueba la conexi\u00F3n y vuelve a intentarlo.", "The download stalled for more than {0} s. Check your connection and try again.", StallSeconds)); return; }
                    if (e.Cancelled || IsDisposed) { TryDelete(zipPath); return; }
                    if (e.Error != null) { TryDelete(zipPath); Fail(DownloadProblem(e.Error)); return; }
                    // Verifying and extracting takes a few seconds; closing is blocked meanwhile.
                    installing = true;
                    cancel.IsEnabled = false;
                    progress = 1;
                    ShowProgress();
                    SetStatus(I18n.T("Comprobando que es el original y preparando\u2026", "Verifying authenticity and setting up\u2026"), false);
                    ThreadPool.QueueUserWorkItem(delegate { Install(); });
                };
                web.DownloadFileAsync(new Uri(Url), zipPath);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // Verifies the pinned SHA-256 and extracts only ffmpeg.exe.
        void Install()
        {
            string error = null, exe = Path.Combine(Settings.FfmpegDir, "ffmpeg.exe");
            try
            {
                string actual;
                using (FileStream fs = File.OpenRead(zipPath))
                using (SHA256 sha = SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (actual != Sha256) error = I18n.T("El archivo descargado no es el original y se ha descartado por seguridad. Vuelve a intentarlo.", "The downloaded file is not original and was discarded for security. Try again.");
                else
                {
                    bool found = false;
                    string tmp = exe + ".part";
                    using (ZipArchive z = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in z.Entries)
                        {
                            if (!entry.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) continue;
                            entry.ExtractToFile(tmp, true);
                            found = true;
                            break;
                        }
                    }
                    if (!found) error = I18n.T("El paquete descargado no trae ffmpeg.exe.", "The downloaded package does not contain ffmpeg.exe.");
                    else
                    {
                        if (File.Exists(exe)) File.Delete(exe);
                        File.Move(tmp, exe);
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; }
            TryDelete(zipPath);
            TryDelete(exe + ".part");
            Action done = delegate
            {
                installing = false;
                cancel.IsEnabled = true;
                if (error != null) { Fail(error); return; }
                ShotStack.Log("FFmpeg " + Version + " listo en " + exe);
                result = exe;
                Close();
            };
            try { if (!IsDisposed) Dispatcher.BeginInvoke(done); }
            catch (Exception ex) { ShotStack.Log("FFmpeg: " + ex.Message); }
        }

        // A download error in plain words; the technical text stays in the log.
        static string DownloadProblem(Exception ex)
        {
            ShotStack.Log("FFmpeg, descarga: " + ex);
            WebException w = ex as WebException;
            if (w != null)
            {
                switch (w.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                        return I18n.T("No hay conexi\u00F3n con el servidor de descarga. Comprueba Internet o pregunta si tu red lo permite.", "No connection to download server. Check Internet or ask if your network allows it.");
                    case WebExceptionStatus.Timeout:
                        return I18n.T("La descarga ha tardado demasiado. Vuelve a intentarlo.", "The download timed out. Try again.");
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.SecureChannelFailure:
                        return I18n.T("No se pudo establecer una conexi\u00F3n segura (quiz\u00E1 un proxy de la empresa). Puedes descargar FFmpeg t\u00FA y elegirlo con \u00ABYa lo tengo\u2026\u00BB.", "Could not establish a secure connection (perhaps a corporate proxy). You can download FFmpeg yourself and pick it with \u201CI already have it\u2026\u201D.");
                }
                HttpWebResponse r = w.Response as HttpWebResponse;
                if (r != null && (int)r.StatusCode == 407) return I18n.T("El proxy de tu red pide usuario y contrase\u00F1a. Puedes descargar FFmpeg t\u00FA y elegirlo con \u00ABYa lo tengo\u2026\u00BB.", "Your network proxy requires username and password. You can download FFmpeg yourself and pick it with \u201CI already have it\u2026\u201D.");
            }
            if (ex.InnerException is IOException || ex is IOException) return I18n.T("No se pudo guardar la descarga: quiz\u00E1 no quede espacio en el disco.", "Could not save download: disk might be full.");
            return I18n.T("No se pudo descargar FFmpeg. Vuelve a intentarlo.", "Could not download FFmpeg. Try again.");
        }

        static void TryDelete(string f)
        {
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }

        void Fail(string message)
        {
            ShotStack.Log("FFmpeg: " + message);
            busy = false;
            SetStatus(message, true);
            track.Visibility = Visibility.Collapsed;
            download.IsEnabled = true;
            browse.IsEnabled = true;
            download.Label = I18n.T("Reintentar", "Retry");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (web != null && !web.IsBusy) { web.Dispose(); web = null; }
        }
    }
}
