// Stackshot - The microphone test on screen: a bar with the level, the loudest peak held for a moment and "Saturando"
// while it clips, or a quiet line when there is nothing to measure.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    // The device is open only while the control is switched on (On) and on screen: in a window that is shown and not
    // minimized. Leaving the screen waits a moment before letting go, so a page rebuilt around it keeps it open. With a
    // button (the Grabacion page) the test is started by hand and ends by itself when the control leaves the screen;
    // without one (the options before recording) it follows On and comes back with the window.
    internal sealed class MicCheck : StackPanel
    {
        // OpeningMs: a laptop's built-in microphone takes about 0.6 s to deliver; the line is for slower ones (Bluetooth), so a
        // normal open never flashes it for a frame.
        internal const int GraceMs = 250, TickMs = 33, IdleTickMs = 250, OpeningMs = 1000;
        static readonly Stopwatch Clock = Stopwatch.StartNew();

        readonly MicMonitor monitor;
        readonly MicBallistics ball = new MicBallistics();
        readonly MicBar bar = new MicBar();
        readonly TextBlock note = new TextBlock(), hint = new TextBlock();
        readonly Grid row = new Grid(), cell = new Grid { Height = 18 };
        readonly MacButton button;
        readonly DispatcherTimer timer = new DispatcherTimer(), grace = new DispatcherTimer();
        Window window;
        bool on;
        double since;           // when the test started, for the line about opening it
        string noteText, barTip;

        // The button was pressed, or the test ended by itself; carries On.
        public event Action<bool> Switched;

        // device: the chosen microphone's id (empty: Windows' default), read when the device opens.
        public MicCheck(Func<string> device, bool withButton)
        {
            monitor = new MicMonitor(device);
            if (withButton)
            {
                button = new MacButton(I18n.T("Probar micr\u00F3fono", "Test microphone"), ButtonKind.Secondary, "mic", null, 28);
                button.HorizontalAlignment = HorizontalAlignment.Left;
                button.Click += delegate { On = !on; Notify(); };
                Children.Add(button);
                row.Margin = new Thickness(0, 10, 0, 0);
                row.Visibility = Visibility.Collapsed;
            }
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.VerticalAlignment = VerticalAlignment.Center;
            cell.Children.Add(bar);
            row.Children.Add(cell);
            note.VerticalAlignment = VerticalAlignment.Center;
            note.TextTrimming = TextTrimming.CharacterEllipsis;
            note.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(note, 2);
            row.Children.Add(note);
            System.Windows.Automation.AutomationProperties.SetName(bar, I18n.T("Nivel del micr\u00F3fono", "Microphone level"));
            hint.Text = I18n.T("Saturando", "Clipping");
            hint.Margin = new Thickness(10, 0, 0, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Visibility = Visibility.Hidden;
            Grid.SetColumn(hint, 1);
            row.Children.Add(hint);
            Children.Add(row);
            Restyle();
            timer.Interval = TimeSpan.FromMilliseconds(TickMs);
            timer.Tick += delegate { Tick(); };
            grace.Interval = TimeSpan.FromMilliseconds(GraceMs);
            grace.Tick += delegate { GraceOver(); };
            IsVisibleChanged += delegate { Update(); };
            Loaded += delegate { Restyle(); Update(); };
            Unloaded += delegate { Update(); };
        }

        static double Now { get { return Clock.Elapsed.TotalMilliseconds; } }

        public bool On
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                ShowOn();
                if (on) Update();
                else End(false);
            }
        }

        // Lets go of the device now (the window is closing). With a button, the test is over.
        public void Halt() { End(button != null); }

        // For a meter at the right edge (the options' controls): "Saturando" before the bar, the line to the right.
        public bool AlignRight
        {
            get { return Grid.GetColumn(cell) == 1; }
            set
            {
                row.ColumnDefinitions[0].Width = value ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
                row.ColumnDefinitions[1].Width = value ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
                Grid.SetColumn(cell, value ? 1 : 0);
                Grid.SetColumn(hint, value ? 0 : 1);
                hint.Margin = value ? new Thickness(0, 0, 10, 0) : new Thickness(10, 0, 0, 0);
                note.TextAlignment = value ? TextAlignment.Right : TextAlignment.Left;
            }
        }

        internal MicMonitor Monitor { get { return monitor; } }
        internal MacButton Button { get { return button; } }
        internal bool Active { get { return timer.IsEnabled; } }
        internal string NoteText { get { return noteText; } }
        internal bool Saturating { get { return hint.Visibility == Visibility.Visible; } }
        internal double BarPeak { get { return bar.Peak; } }

        // Whether the device should be open.
        internal static bool Wanted(bool on, bool visible, bool windowShown, WindowState state)
        {
            return on && visible && windowShown && state != WindowState.Minimized;
        }

        // The line instead of the bar, or null when the bar says it all.
        internal static string Words(MicState state, double sinceMs)
        {
            switch (state)
            {
                case MicState.Opening: return sinceMs >= OpeningMs ? I18n.T("Abriendo el micr\u00F3fono\u2026", "Opening microphone\u2026") : null;
                case MicState.NoDevice: return I18n.T("No hay micr\u00F3fono", "No microphone");
                case MicState.NotResponding: return I18n.T("El micr\u00F3fono no responde", "Microphone not responding");
                case MicState.Denied: return I18n.T("Windows no deja usar el micr\u00F3fono", "Windows blocked microphone access");
                case MicState.Recording: return I18n.T("Se est\u00E1 grabando", "Currently recording");
            }
            return null;
        }

        void Restyle()
        {
            Palette pal = Ds.Brushes;
            note.FontFamily = Ds.Text;
            note.FontSize = 12;
            note.Foreground = Ds.Brush(pal.Label2);
            hint.FontFamily = Ds.Semibold.FontFamily;
            hint.FontWeight = Ds.Semibold.Weight;
            hint.FontSize = 11.5;
            hint.Foreground = Ds.Brush(pal.Error);
            bar.InvalidateVisual();
        }

        void ShowOn()
        {
            if (button == null) return;
            button.Label = on ? I18n.T("Dejar de probar", "Stop testing") : I18n.T("Probar micr\u00F3fono", "Test microphone");
            row.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        void Notify()
        {
            Action<bool> h = Switched;
            if (h != null) h(on);
        }

        // The window it sits in, followed for minimizing.
        Window Host()
        {
            Window w = Window.GetWindow(this);
            if (w != window)
            {
                if (window != null) window.StateChanged -= OnWindowState;
                window = w;
                if (window != null) window.StateChanged += OnWindowState;
            }
            return w;
        }

        void OnWindowState(object sender, EventArgs e) { Update(); }

        bool WantedNow()
        {
            Window w = Host();
            return Wanted(on, IsVisible, w != null && w.IsVisible, w != null ? w.WindowState : WindowState.Normal);
        }

        void Update()
        {
            if (WantedNow())
            {
                grace.Stop();
                if (!timer.IsEnabled) Begin();
            }
            else if (timer.IsEnabled && !grace.IsEnabled) grace.Start();
        }

        void GraceOver()
        {
            grace.Stop();
            if (!WantedNow()) End(button != null);
        }

        void Begin()
        {
            since = Now;
            ball.Reset();
            monitor.Start();
            timer.Interval = TimeSpan.FromMilliseconds(monitor.Running ? TickMs : IdleTickMs);
            timer.Start();
            Tick();
        }

        void End(bool switchOff)
        {
            timer.Stop();
            grace.Stop();
            monitor.Stop();
            ball.Reset();
            bar.Show(MicScale.Floor, MicScale.Floor, MicScale.Floor, false);
            hint.Visibility = Visibility.Hidden;
            SetNote(null, MicState.Off);
            SetBarTip(null);
            if (switchOff && on)
            {
                on = false;
                ShowOn();
                Notify();
            }
        }

        // About 30 times a second while the device is open, 4 while it waits (a recording, a problem).
        void Tick()
        {
            double now = Now;
            if (monitor.Yield()) ball.Reset();
            else if (!monitor.Running && monitor.State == MicState.Recording && !grace.IsEnabled && !MicMonitor.IsRecording() && WantedNow())
            {
                // The recording is over: the test carries on (unless the control is on its way off the screen).
                since = now;
                monitor.Start();
            }
            MicState st = monitor.State;
            ball.Step(monitor.Take(), now);
            bool live = st == MicState.Live, clip = live && ball.Clipping(now);
            SetNote(Words(st, now - since), st);
            bar.Show(live ? ball.Peak : MicScale.Floor, live ? ball.Rms : MicScale.Floor, live ? ball.Hold(now) : MicScale.Floor, clip);
            Visibility hv = clip ? Visibility.Visible : Visibility.Hidden;
            if (hint.Visibility != hv) hint.Visibility = hv;
            string name = monitor.DeviceName;
            SetBarTip(live && name.Length > 0 ? I18n.T("Micr\u00F3fono: ", "Microphone: ") + name : null);
            int ms = monitor.Running ? TickMs : IdleTickMs;
            if (Math.Abs(timer.Interval.TotalMilliseconds - ms) > 0.5) timer.Interval = TimeSpan.FromMilliseconds(ms);
        }

        void SetNote(string text, MicState st)
        {
            if (text == noteText) return;
            noteText = text;
            note.Text = text ?? "";
            note.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
            bar.Visibility = text == null ? Visibility.Visible : Visibility.Hidden;
            if (st == MicState.Denied) note.ToolTip = I18n.T("Act\u00EDvalo en Configuraci\u00F3n > Privacidad y seguridad > Micr\u00F3fono.", "Enable it in Settings > Privacy & security > Microphone.");
            else if (st == MicState.Recording) note.ToolTip = I18n.T("Mientras grabas, el nivel de cada sonido sale en la barra de grabaci\u00F3n.", "While recording, audio levels are shown on the recording bar.");
            else note.ToolTip = text;
        }

        void SetBarTip(string tip)
        {
            if (tip == barTip) return;
            barTip = tip;
            bar.ToolTip = tip;
        }
    }

    // The bar itself: the RMS solid and the peak lighter over a track tinted from -6 dBFS, and a tick at the held peak.
    // Repaints only when what it shows moves.
    internal sealed class MicBar : FrameworkElement
    {
        double peak = MicScale.Floor, rms = MicScale.Floor, hold = MicScale.Floor;
        bool clip;

        public MicBar()
        {
            Height = 8;
            SnapsToDevicePixels = true;
        }

        internal double Peak { get { return peak; } }

        // Readers get it as a level from 0 to 100 (where the bar is filled), read when they ask.
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() { return new LevelPeer(this, delegate { return MicScale.Position(rms) * 100; }); }

        public void Show(double p, double r, double h, bool c)
        {
            if (Math.Abs(p - peak) < 0.05 && Math.Abs(r - rms) < 0.05 && Math.Abs(h - hold) < 0.05 && c == clip) return;
            peak = p;
            rms = r;
            hold = h;
            clip = c;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double w = ActualWidth, h = ActualHeight;
            if (w < 4 || h < 2) return;
            double rad = h / 2, x6 = Math.Round(MicScale.Position(MicScale.Warn) * w);
            Rect track = new Rect(0, 0, w, h);
            dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Argb(0.08, 0, 0, 0)), null, track, rad, rad);
            dc.PushClip(new RectangleGeometry(track, rad, rad));
            dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Yellow, pal.Dark ? 0.16 : 0.22)), null, new Rect(x6, 0, w - x6, h));
            Fill(dc, pal, MicScale.Position(peak) * w, x6, h, 0.45);
            Fill(dc, pal, MicScale.Position(rms) * w, x6, h, 1);
            if (hold > MicScale.Floor + 0.5)
            {
                int zone = MicScale.Zone(hold, clip);
                double hx = Math.Max(0, Math.Min(w - 2, MicScale.Position(hold) * w - 1));
                dc.DrawRectangle(Ds.Brush(zone == 2 ? pal.Red : zone == 1 ? pal.Yellow : pal.Green), null, new Rect(hx, 0, 2, h));
            }
            dc.Pop();
        }

        void Fill(DrawingContext dc, Palette pal, double x, double x6, double h, double alpha)
        {
            if (x < 0.5) return;
            dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(pal.Green, alpha)), null, new Rect(0, 0, Math.Min(x, x6), h));
            if (x > x6) dc.DrawRectangle(Ds.Brush(Ds.WithAlpha(clip ? pal.Red : pal.Yellow, alpha)), null, new Rect(x6, 0, x - x6, h));
        }
    }
}
