// Stackshot - Options of a take shown after choosing the area (quality, frame rate, sound, camera, countdown), and the
// countdown itself.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using D = System.Drawing;
using WF = System.Windows.Forms;

namespace Stackshot
{
    // Enter records, Esc cancels. What is chosen here is saved, as if changed on the Grabacion page.
    public sealed class RecordSetup : Sheet
    {
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        // Shown order (Estandar, Alta, Maxima, Cine) to Recorder's quality numbers.
        internal static readonly int[] QualityOrder = { Recorder.Standard, Recorder.High, Recorder.Maximum, Recorder.Cinema };
        internal static readonly int[] Countdowns = { 0, 3, 5, 10 };
        // The frame rates offered; a stored value outside them is snapped so what is shown is what gets recorded.
        internal static readonly int[] VideoFpsOptions = { 24, 30, 60 }, GifFpsOptions = { 10, 15, 20, 25 };
        static readonly string[] QualityNotesEs =
        {
            "Archivos peque\u00F1os, para enviar en segundos.",
            "60 fps con mucho detalle; se guarda al momento.",
            "Sin p\u00E9rdidas al grabar y MP4 de muy alta calidad al terminar; tarda un poco en guardar.",
            "M\u00E1ster sin p\u00E9rdidas comprobado fotograma a fotograma (.mkv); ocupa mucho."
        };
        static readonly string[] QualityNotesEn =
        {
            "Small files, ready to send in seconds.",
            "60 fps with rich detail; saves instantly.",
            "Lossless while recording and very high quality MP4 when finished; takes a moment to save.",
            "Lossless master verified frame by frame (.mkv); takes up significant space."
        };
        static string[] QualityNotes { get { return I18n.IsSpanish ? QualityNotesEs : QualityNotesEn; } }

        // Maxima is not lossless on areas above 1920x1200 at 60 fps: they go through NV12 (4:2:0) on the graphics card.
        internal static string QualityNote(int quality, D.Rectangle area)
        {
            if (quality == Recorder.Maximum && Recorder.LargeArea(area.Width, area.Height, 60))
                return I18n.T("Calidad muy alta al grabar (en un \u00E1rea tan grande no es sin p\u00E9rdidas) y MP4 de muy alta calidad al terminar; tarda un poco en guardar.",
                              "Very high quality while recording (lossless not supported on an area this large) and very high quality MP4 when finished; takes a moment to save.");
            return QualityNotes[Array.IndexOf(QualityOrder, quality)];
        }

        readonly Settings settings;
        readonly D.Rectangle area;
        readonly bool gif;
        // The choices, applied to the settings only when Grabar is pressed.
        int quality, videoFps, cineFps, gifFps, camera, countdown;
        bool sys, mic, ask;
        StackPanel page;
        bool accepted;
        MicCheck micCheck;      // the microphone's level while it is on, kept across rebuilds of the page
        TextBlock micLabel;

        // False when cancelled.
        public static bool Ask(Settings s, D.Rectangle area, bool gif)
        {
            RecordSetup w = new RecordSetup(s, area, gif);
            w.ShowDialog();
            return w.accepted;
        }

        RecordSetup(Settings s, D.Rectangle area, bool gif)
        {
            settings = s;
            this.area = area;
            this.gif = gif;
            quality = Array.IndexOf(QualityOrder, s.VideoQuality) >= 0 ? s.VideoQuality : Recorder.Standard;
            videoFps = VideoFpsOptions[Nearest(VideoFpsOptions, s.VideoFps)];
            cineFps = s.CineFps == 30 ? 30 : 60;
            gifFps = GifFpsOptions[Nearest(GifFpsOptions, s.GifFps)];
            camera = s.Webcam;
            countdown = Settings.Countdown(s.RecordCountdown);
            sys = s.RecordSystemAudio;
            mic = s.RecordMic;
            ask = s.RecordSetup;
            Title = "Stackshot";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowActivated = true;
            Icon = AppIcon;
            DragAnywhere();
            Build();
            Access.Dialog(this, null);
            KeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Handled || e.IsRepeat) return;
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter) { e.Handled = true; Accept(); }
            };
            // Over the middle of the chosen area, inside its monitor.
            Loaded += delegate { Place(); };
            // The recording opens the microphone right after: the test lets go of it first.
            Closed += delegate { if (micCheck != null) micCheck.Halt(); };
        }

        protected override void ThemeChanged() { Build(); }

        void Place()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                RECT r;
                if (h == IntPtr.Zero || !GetWindowRect(h, out r)) return;
                int w = r.Right - r.Left, ht = r.Bottom - r.Top;
                D.Rectangle wa = WF.Screen.FromRectangle(area).WorkingArea;
                int x = area.X + (area.Width - w) / 2, y = area.Y + (area.Height - ht) / 2;
                x = Math.Max(wa.Left + 8, Math.Min(wa.Right - w - 8, x));
                y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - ht - 8, y));
                SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); // SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
            }
            catch (Exception ex) { ShotStack.Log("Opciones de grabaci\u00F3n: " + ex.Message); }
        }

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new StackPanel { Margin = new Thickness(28, 26, 28, 24) };
            Root.Children.Add(page);
            KeepCaptionsOnTop();

            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(gif ? new IconTile("gif", 40, Ds.Rgb(191, 90, 242), Ds.Rgb(140, 60, 220)) : new IconTile("video", 40, Ds.Rgb(255, 85, 100), Ds.Rgb(230, 40, 40)));
            StackPanel titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Text(gif ? I18n.T("Grabar GIF", "Record GIF") : I18n.T("Grabar v\u00EDdeo", "Record video"), Ds.Title, 20, pal.Label));
            titles.Children.Add(Text(I18n.T("\u00C1rea de ", "Area ") + area.Width + " \u00D7 " + area.Height + " px", Ds.Regular, 12.5, pal.Label2));
            head.Children.Add(titles);
            page.Children.Add(head);

            Grid rows = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            page.Children.Add(rows);

            if (!gif)
            {
                MacSegmented q = new MacSegmented(new string[] { I18n.T("Est\u00E1ndar", "Standard"), I18n.T("Alta", "High"), I18n.T("M\u00E1xima", "Maximum"), I18n.T("Cine", "Cinema") }, Array.IndexOf(QualityOrder, quality));
                q.Changed += delegate(int i) { quality = QualityOrder[i]; Build(); };
                Row(rows, I18n.T("Calidad", "Quality"), q, QualityNote(quality, area));
                if (quality == Recorder.Standard)
                {
                    MacSegmented f = new MacSegmented(new string[] { "24", "30", "60" }, Nearest(VideoFpsOptions, videoFps));
                    f.Changed += delegate(int i) { videoFps = VideoFpsOptions[i]; };
                    Row(rows, I18n.T("Fotogramas", "Frame rate"), f, I18n.T("por segundo", "per second"));
                }
                else if (quality == Recorder.Cinema)
                {
                    MacSegmented f = new MacSegmented(new string[] { "30", "60" }, cineFps == 30 ? 0 : 1);
                    f.Changed += delegate(int i) { cineFps = i == 0 ? 30 : 60; };
                    Row(rows, I18n.T("Fotogramas", "Frame rate"), f, I18n.T("por segundo", "per second"));
                }
                else Row(rows, I18n.T("Fotogramas", "Frame rate"), Text(I18n.T("60 por segundo", "60 per second"), Ds.Regular, 13, pal.Label2), I18n.T("Alta y M\u00E1xima graban siempre a 60, para que el movimiento sea fluido.", "High and Maximum always record at 60 fps for smooth motion."));

                MacSwitch ss = new MacSwitch(sys);
                ss.Toggled += delegate(bool on) { sys = on; };
                Row(rows, I18n.T("Sonido del equipo", "System audio"), ss, null);
                MacSwitch ms = new MacSwitch(mic);
                ms.Toggled += delegate(bool on) { mic = on; MicLevel(); };
                Row(rows, I18n.T("Micr\u00F3fono", "Microphone"), ms, null);
                MicRow(rows);
                MacSegmented cam = new MacSegmented(new string[] { I18n.T("No", "None"), I18n.T("Redonda", "Circle"), I18n.T("Cuadrada", "Square") }, Math.Max(0, Math.Min(2, camera)));
                cam.Changed += delegate(int i) { camera = i; };
                Row(rows, I18n.T("C\u00E1mara", "Webcam"), cam, null);
            }
            else
            {
                MacSegmented f = new MacSegmented(new string[] { "10", "15", "20", "25" }, Nearest(GifFpsOptions, gifFps));
                f.Changed += delegate(int i) { gifFps = GifFpsOptions[i]; };
                Row(rows, I18n.T("Fotogramas", "Frame rate"), f, I18n.T("por segundo", "per second"));
            }
            MacSegmented cd = new MacSegmented(new string[] { I18n.T("No", "None"), "3 s", "5 s", "10 s" }, Math.Max(0, Array.IndexOf(Countdowns, countdown)));
            cd.Changed += delegate(int i) { countdown = Countdowns[i]; };
            Row(rows, I18n.T("Cuenta atr\u00E1s", "Countdown"), cd, null);

            Grid foot = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            StackPanel again = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            MacSwitch askSw = new MacSwitch(ask) { VerticalAlignment = VerticalAlignment.Center };
            askSw.Toggled += delegate(bool on) { ask = on; };
            again.Children.Add(askSw);
            TextBlock al = Text(I18n.T("Preguntar siempre", "Always ask"), Ds.Regular, 12.5, pal.Label2);
            al.Margin = new Thickness(8, 0, 0, 0);
            again.Children.Add(al);
            again.ToolTip = I18n.T("Si lo apagas, se graba directamente con estas opciones (se vuelve a activar en Grabaci\u00F3n).", "If turned off, recording starts immediately with these settings (re-enable in Recording).");
            Access.Describe(askSw, al.Text, (string)again.ToolTip);
            foot.Children.Add(again);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            MacButton go = new MacButton(I18n.T("Grabar", "Record"), ButtonKind.Primary, gif ? "gif" : "video", null, 32);
            go.Click += Accept;
            MacButton cancel = new MacButton(I18n.Cancel, ButtonKind.Secondary, null, null, 32);
            cancel.Margin = new Thickness(8, 0, 0, 0);
            cancel.Click += delegate { Close(); };
            buttons.Children.Add(go);
            buttons.Children.Add(cancel);
            foot.Children.Add(buttons);
            page.Children.Add(foot);
        }

        TextBlock Row(Grid rows, string label, UIElement control, string note)
        {
            Palette pal = Ds.Brushes;
            int r = rows.RowDefinitions.Count;
            rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock l = Text(label, Ds.Regular, 13, pal.Label);
            l.Margin = new Thickness(0, 7, 16, 7);
            Grid.SetRow(l, r);
            rows.Children.Add(l);
            FrameworkElement c = (FrameworkElement)control;
            Access.Describe(c, label, note);
            c.HorizontalAlignment = HorizontalAlignment.Right;
            c.VerticalAlignment = VerticalAlignment.Center;
            c.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(c, r);
            Grid.SetColumn(c, 1);
            rows.Children.Add(c);
            if (note == null) return l;
            rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock n = Text(note, Ds.Regular, 12, pal.Label3);
            n.TextWrapping = TextWrapping.Wrap;
            n.TextAlignment = TextAlignment.Right;
            n.Margin = new Thickness(0, 0, 0, 6);
            Grid.SetRow(n, r + 1);
            Grid.SetColumnSpan(n, 2);
            rows.Children.Add(n);
            return l;
        }

        // The microphone's level under its switch, shown while it is on; the same meter after a rebuild (quality, theme).
        void MicRow(Grid rows)
        {
            if (micCheck == null) micCheck = new MicCheck(delegate { return settings.MicDevice; }, false) { Width = 240, AlignRight = true };
            Panel old = micCheck.Parent as Panel;
            if (old != null) old.Children.Remove(micCheck);
            micLabel = Row(rows, I18n.T("Nivel de entrada", "Input level"), micCheck, null);
            MicLevel();
        }

        void MicLevel()
        {
            if (micCheck == null) return;
            Visibility v = mic ? Visibility.Visible : Visibility.Collapsed;
            if (micLabel != null) micLabel.Visibility = v;
            micCheck.Visibility = v;
            micCheck.On = mic;
        }

        static int Nearest(int[] opts, int v)
        {
            int best = 0;
            for (int i = 1; i < opts.Length; i++) if (Math.Abs(opts[i] - v) < Math.Abs(opts[best] - v)) best = i;
            return best;
        }

        static TextBlock Text(string s, Typeface face, double size, Color c)
        {
            return new TextBlock { Text = s, FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Foreground = Ds.Brush(c), VerticalAlignment = VerticalAlignment.Center };
        }

        void Accept()
        {
            if (accepted) return;
            accepted = true;
            if (!gif)
            {
                settings.VideoQuality = quality;
                settings.VideoFps = videoFps;
                settings.CineFps = cineFps;
                settings.RecordSystemAudio = sys;
                settings.RecordMic = mic;
                settings.Webcam = camera;
            }
            else settings.GifFps = gifFps;
            settings.RecordCountdown = countdown;
            settings.RecordSetup = ask;
            try { settings.Save(); } catch (Exception ex) { ShotStack.Log("Opciones de grabaci\u00F3n: " + ex.Message); }
            Close();
        }
    }

    // The 3-2-1 in the middle of the area before recording starts. Excluded from capture and never takes the focus;
    // a click (or Esc) cancels the take.
    internal sealed class CountdownWindow : WF.Form
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);

        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly int seconds;
        readonly WF.Timer timer = new WF.Timer();
        readonly float s;
        int shown = -1;
        bool escUp;
        public event Action Cancelled;

        public CountdownWindow(D.Rectangle area, int seconds)
        {
            this.seconds = Math.Max(1, seconds);
            s = ShotStack.ScaleFor(WF.Screen.FromRectangle(area));
            Text = "Stackshot";
            FormBorderStyle = WF.FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = WF.FormStartPosition.Manual;
            TopMost = true;
            BackColor = Ds.Gdi(Palette.HudBack(Ds.Rgb(28, 28, 30)));
            DoubleBuffered = true;
            int side = (int)Math.Round(150 * s);
            Bounds = new D.Rectangle(area.X + (area.Width - side) / 2, area.Y + (area.Height - side) / 2, side, side);
            Opacity = Ds.Brushes.High ? 1 : 0.92;
            Cursor = WF.Cursors.Hand;
            timer.Interval = 30;
            timer.Tick += delegate { Step(); };
            timer.Start();
        }

        public bool Done { get { return clock.Elapsed.TotalSeconds >= seconds; } }

        int Remaining { get { return Math.Max(1, seconds - (int)Math.Floor(clock.Elapsed.TotalSeconds)); } }

        void Step()
        {
            bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (!esc) escUp = true;
            else if (escUp) { Cancel(); return; }
            if (Done) { timer.Stop(); Hide(); return; }
            int n = Remaining;
            if (n != shown) shown = n;
            Invalidate(); // the ring moves every tick
        }

        void Cancel()
        {
            timer.Stop();
            Action c = Cancelled;
            Cancelled = null;
            if (c != null) c();
        }

        protected override WF.CreateParams CreateParams
        {
            get
            {
                WF.CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref WF.Message m)
        {
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // DWMWCP_ROUND
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
            }
            catch { }
            if (FloatWindow.ExcludeFromCapture) Native.SetWindowDisplayAffinity(Handle, 0x11); // WDA_EXCLUDEFROMCAPTURE
        }

        protected override void OnMouseUp(WF.MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == WF.MouseButtons.Left) Cancel();
        }

        protected override void OnPaint(WF.PaintEventArgs e)
        {
            D.Graphics g = e.Graphics;
            g.SmoothingMode = D.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = D.Text.TextRenderingHint.AntiAliasGridFit;
            int side = ClientSize.Width;
            // The ring empties over each second.
            double frac = clock.Elapsed.TotalSeconds - Math.Floor(clock.Elapsed.TotalSeconds);
            float pad = 14 * s, w = Math.Max(2f, 4f * s);
            D.RectangleF ring = new D.RectangleF(pad, pad, side - 2 * pad, side - 2 * pad);
            using (D.Pen back = new D.Pen(Ds.Gdi(Palette.HudFaint(Ds.Argb(60 / 255.0, 255, 255, 255))), w)) g.DrawEllipse(back, ring);
            using (D.Pen front = new D.Pen(Ds.Gdi(Palette.HudInk(Ds.Rgb(10, 132, 255))), w))
            {
                front.StartCap = front.EndCap = D.Drawing2D.LineCap.Round;
                float sweep = (float)(360 * (1 - frac));
                if (sweep > 0.5f) g.DrawArc(front, ring, -90, sweep);
            }
            using (D.StringFormat sf = new D.StringFormat { Alignment = D.StringAlignment.Center, LineAlignment = D.StringAlignment.Center })
            using (D.SolidBrush hudInk = new D.SolidBrush(Ds.Gdi(Palette.HudLabel)))
            {
                D.Font big = Fonts.Get(Fonts.DisplaySemibold, 64 * s);
                g.DrawString((shown > 0 ? shown : Remaining).ToString(), big, hudInk, new D.RectangleF(0, -6 * s, side, side), sf);
                D.Font small = Fonts.Get(Fonts.Display, 11 * s);
                using (D.SolidBrush b = new D.SolidBrush(Ds.Gdi(Palette.HudInk(Ds.Argb(170 / 255.0, 255, 255, 255)))))
                    g.DrawString(I18n.T("Esc: cancelar", "Esc: cancel"), small, b, new D.RectangleF(0, side * 0.68f, side, 20 * s), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
