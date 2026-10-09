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
            Root.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (e.ClickCount == 1) try { DragMove(); } catch { } };
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
            head.Children.Add(Text("Grabar v\u00EDdeo y GIF", Ds.Title, 20, pal.Label, new Thickness(14, 0, 0, 0)));
            page.Children.Add(head);
            TextBlock p = Text("Para grabar, Stackshot usa FFmpeg " + Version + ", un programa libre y gratuito. Se descarga una sola vez (unos 110 MB) " +
                               "desde GitHub, se comprueba que es el original y se guarda solo para tu usuario.", Ds.Regular, 13, pal.Label2, new Thickness(0, 16, 0, 0));
            p.TextWrapping = TextWrapping.Wrap;
            p.LineHeight = 19;
            page.Children.Add(p);

            track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Ds.Brush(pal.Control), Margin = new Thickness(0, 22, 0, 0) };
            track.Visibility = busy || installing ? Visibility.Visible : Visibility.Collapsed;
            fill = new Border { CornerRadius = new CornerRadius(3), Background = new LinearGradientBrush(pal.Accent, pal.Purple, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            track.Child = fill;
            track.SizeChanged += delegate { ShowProgress(); };
            page.Children.Add(track);
            status = Text(statusText, Ds.Regular, 12, failed ? pal.Red : pal.Label3, new Thickness(0, 8, 0, 0));
            status.TextWrapping = TextWrapping.Wrap;
            page.Children.Add(status);

            Grid buttons = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            cancel = new MacButton("Cancelar", ButtonKind.Secondary, null, null, 32);
            cancel.HorizontalAlignment = HorizontalAlignment.Left;
            cancel.Click += delegate { Close(); };
            cancel.IsEnabled = !installing;
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            browse = new MacButton("Ya lo tengo\u2026", ButtonKind.Secondary, null, null, 32);
            browse.ToolTip = "Elegir un ffmpeg.exe que ya tengas en el equipo";
            browse.Click += Browse;
            browse.IsEnabled = !busy && !installing;
            download = new MacButton(failed ? "Reintentar" : "Descargar", ButtonKind.Primary, "save", null, 32);
            download.Margin = new Thickness(8, 0, 0, 0);
            download.Click += Start;
            download.IsEnabled = !busy && !installing;
            right.Children.Add(browse);
            right.Children.Add(download);
            buttons.Children.Add(cancel);
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
            status.Foreground = Ds.Brush(error ? Ds.Brushes.Red : Ds.Brushes.Label3);
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
                d.Title = "\u00BFD\u00F3nde est\u00E1 ffmpeg.exe?";
                if (d.ShowDialog(Win32) != WF.DialogResult.OK) return;
                settings.Ffmpeg = d.FileName;
                settings.Save();
                result = d.FileName;
                Close();
            }
        }

        void Start()
        {
            busy = true;
            failed = false;
            progress = 0;
            shownPermille = -1;
            shownMb = -1;
            download.IsEnabled = false;
            browse.IsEnabled = false;
            track.Visibility = Visibility.Visible;
            ShowProgress();
            SetStatus("Conectando\u2026", false);
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
                        SetStatus(mb + " MB" + (total > 0 ? " de " + (total / 1048576) + " MB" : ""), false);
                    }
                };
                web.DownloadFileCompleted += delegate(object o, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    if (e.Cancelled || IsDisposed) { TryDelete(zipPath); return; }
                    if (e.Error != null) { TryDelete(zipPath); Fail("No se pudo descargar: " + e.Error.Message); return; }
                    // Verifying and extracting takes a few seconds; closing is blocked meanwhile.
                    installing = true;
                    cancel.IsEnabled = false;
                    progress = 1;
                    ShowProgress();
                    SetStatus("Comprobando que es el original y preparando\u2026", false);
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
                if (actual != Sha256) error = "La descarga no coincide con la huella esperada; se ha descartado por seguridad.";
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
                    if (!found) error = "El paquete descargado no trae ffmpeg.exe.";
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
            download.Label = "Reintentar";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (web != null && !web.IsBusy) { web.Dispose(); web = null; }
        }
    }
}
