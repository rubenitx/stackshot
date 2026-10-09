// Stackshot - The three home views (minimal, widgets, scene); only the chosen one is ever built.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using D = System.Drawing;
using WF = System.Windows.Forms;

namespace Stackshot
{
    public partial class HomeWindow
    {
        static readonly string[] HomeStyles = { "Minimalista", "Widgets", "Escena" };
        const double HomeSwitchTop = 44;          // top inset of the home content

        // Entrance: when Home is shown, its pieces fade and rise in turn. Rebuilds of the same view (theme, a new capture)
        // don't replay it, and once it settles no animation stays attached to the clock.
        readonly List<Action> homeEnter = new List<Action>();
        bool homeEnterNext, homeHooked, homeStale;
        string sideLastPage;          // page the sidebar widget was last built for (BuildWidget sets it)
        int homeTileOrder;

        HashSet<string> homeShownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase), homeShownBefore;
        readonly List<UIElement> homeFresh = new List<UIElement>();   // captures this build shows for the first time

        void BuildHome()
        {
            HomeHooks();
            homeEnter.Clear();
            homeTileOrder = 0;
            homeStale = false;
            bool play = homeEnterNext || sideLastPage != "home";
            homeShownBefore = play ? null : homeShownFiles;
            homeShownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            homeFresh.Clear();
            switch (settings.HomeStyle)
            {
                case 1: BuildHomeWidgets(); break;
                case 2: BuildHomeScene(); break;
                default: BuildHomeMinimal(); break;
            }
            homeEnterNext = false;
            homeSig = HomeSignature();
            if (play) HomePlayEntrance();
            else foreach (UIElement e in homeFresh) HomePop(e);
            homeShownBefore = null;
            homeFresh.Clear();
        }

        string homeSig;               // what Home showed when it was built

        // Everything Home shows that changes on its own: the day and time of day, the captures, the recording, the mascot.
        string HomeSignature()
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            b.Append(DateTime.Today.Ticks).Append(MascotTalk.Greeting()).Append(Recorder.Recording).Append(settings.MascotLove).Append(settings.MascotName);
            if (settings.ShowRecent) foreach (FileInfo f in Recents.Latest(settings, 5)) b.Append('|').Append(f.FullName).Append(f.LastWriteTimeUtc.Ticks);
            return b.ToString();
        }

        void HomePlayEntrance()
        {
            foreach (Action a in homeEnter.ToArray()) a();
        }

        // Home keeps itself current. Hidden or minimized, it goes stale; shown again it plays its entrance, rebuilt first
        // only if what it shows has changed (new captures, the time of day, the recording). A capture taken while it is on
        // screen shows up once its file is on disk, and the recording state is checked when the window is activated.
        void HomeHooks()
        {
            if (homeHooked) return;
            homeHooked = true;
            IsVisibleChanged += delegate
            {
                if (!IsVisible) { homeStale = true; return; }
                if (Left < -30000) return;   // prewarming off screen
                HomeShownAgain();
            };
            StateChanged += delegate
            {
                if (WindowState == WindowState.Minimized) homeStale = true;
                else if (IsVisible && Left > -30000) HomeShownAgain();
            };
            Activated += delegate { HomeRecheck(); };
            Action<string> captured = delegate(string path)
            {
                Dispatcher ui = Dispatcher;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    ShotStack.WaitWritten(path);
                    Recents.Forget();
                    ui.BeginInvoke((Action)delegate { if (IsVisible && page == "home") HomeRefresh(); });
                });
            };
            owner.Captured += captured;
            Closed += delegate { owner.Captured -= captured; };
        }

        void HomeShownAgain()
        {
            bool stale = homeStale, built = false;
            homeStale = false;
            if (stale) Recents.Forget();
            if (page == "home")
            {
                if (stale && HomeSignature() != homeSig) { homeEnterNext = true; Build(); built = true; }
                else HomePlayEntrance();
            }
            if (stale && !built) BuildWidget();
            HomeRecheck();
            HomeArmWatch();
        }

        bool sideRecording;           // Recorder.Recording when the views were last built
        DispatcherTimer recWatch;

        // Rebuilds what shows the recording state once it no longer matches.
        void HomeRecheck()
        {
            if (!IsVisible || Recorder.Recording == sideRecording) return;
            if (page == "home") HomeRefresh();
            if (page != "home" || homeNameOutside != null) BuildWidget();
        }

        bool homeRefreshLater;        // something changed while the mascot's name was being edited

        // Shows what changed now or, while the name field is open (a rebuild would close it), as soon as it closes.
        void HomeRefresh()
        {
            if (homeNameOutside == null) { homeRefreshLater = false; Rebuild(); }
            else homeRefreshLater = true;
        }

        // While a recording runs and the window is on screen, looks a few times a second for its end (stopping finishes
        // asynchronously, from here or from the record bar). The check stops with the recording or when the window hides.
        void HomeWatchRecording()
        {
            sideRecording = Recorder.Recording;
            HomeArmWatch();
        }

        void HomeArmWatch()
        {
            if (!sideRecording || !IsVisible) { if (recWatch != null) recWatch.Stop(); return; }
            if (recWatch == null)
            {
                recWatch = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(400) };
                recWatch.Tick += delegate
                {
                    if (!IsVisible || Recorder.Recording != sideRecording) recWatch.Stop();
                    HomeRecheck();
                };
            }
            if (!recWatch.IsEnabled) recWatch.Start();
        }

        // Registers e to fade in after `delay` ms, gliding from (dx, dy) and growing from `scale`.
        void HomeEnter(UIElement e, double delay, double dx, double dy, double scale)
        {
            HomeEnter(e, delay, dx, dy, scale, 460);
        }

        void HomeEnter(UIElement e, double delay, double dx, double dy, double scale, double ms)
        {
            TranslateTransform move = new TranslateTransform();
            ScaleTransform grow = new ScaleTransform();
            TransformGroup g = new TransformGroup();
            if (e.RenderTransform != null && e.RenderTransform != Transform.Identity) g.Children.Add(e.RenderTransform);
            g.Children.Add(grow);
            g.Children.Add(move);
            e.RenderTransform = g;
            if (scale != 1) e.RenderTransformOrigin = new Point(0.5, 0.5);
            homeEnter.Add(delegate
            {
                HomeTween(e, UIElement.OpacityProperty, 0, 1, delay, ms * 0.7, new CubicEase { EasingMode = EasingMode.EaseOut });
                if (dx != 0) HomeTween(move, TranslateTransform.XProperty, dx, 0, delay, ms, new QuinticEase { EasingMode = EasingMode.EaseOut });
                if (dy != 0) HomeTween(move, TranslateTransform.YProperty, dy, 0, delay, ms, new QuinticEase { EasingMode = EasingMode.EaseOut });
                if (scale != 1)
                {
                    HomeTween(grow, ScaleTransform.ScaleXProperty, scale, 1, delay, ms, new QuinticEase { EasingMode = EasingMode.EaseOut });
                    HomeTween(grow, ScaleTransform.ScaleYProperty, scale, 1, delay, ms, new QuinticEase { EasingMode = EasingMode.EaseOut });
                }
            });
        }

        // From `from` to `to` after `delay`; when it ends the value is kept and the animation dropped. No ease = linear.
        static void HomeTween(DependencyObject o, DependencyProperty p, double from, double to, double delay, double ms, IEasingFunction ease)
        {
            o.SetValue(p, from);
            DoubleAnimation a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms));
            a.BeginTime = TimeSpan.FromMilliseconds(delay);
            if (ease != null) a.EasingFunction = ease;
            a.Completed += delegate { HomeSettle(o, p, to); };
            // The clock starts once the frame that builds and lays out the page is on screen: started now, a long layout
            // would eat the first frames of the animation and it would jump.
            DispatcherObject d = o as DispatcherObject;
            if (d == null) { ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace); return; }
            d.Dispatcher.BeginInvoke((Action)delegate { ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace); }, DispatcherPriority.Loaded);
        }

        // From wherever it is now to `to`, eased out.
        static void HomeGlide(DependencyObject o, DependencyProperty p, double to, double ms, IEasingFunction ease)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut };
            a.Completed += delegate { HomeSettle(o, p, to); };
            ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
        }

        static void HomeSettle(DependencyObject o, DependencyProperty p, double to)
        {
            if (Math.Abs((double)o.GetValue(p) - to) > 1e-6) return;   // a newer animation took over
            o.SetValue(p, to);
            ((IAnimatable)o).BeginAnimation(p, null);
        }

        // ---- Minimal: the mascot, a big question, one bar with every capture mode and the latest captures.

        void BuildHomeMinimal()
        {
            Palette pal = Ds.Brushes;
            body.MaxWidth = 700;
            Add(new Grid { Height = HomeSwitchTop });

            // With the mascot, the column sits lower: the block is centred under the switch and a one-line speech bubble
            // (a rename, "Copiada") clears the switch instead of covering it; a longer one moves aside by itself.
            StackPanel col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, settings.MascotOn ? 58 : 26, 0, 0) };
            if (settings.MascotOn)
            {
                MascotHost host = new MascotHost(mascot, 84, false);
                host.HorizontalAlignment = HorizontalAlignment.Center;
                host.Margin = new Thickness(0, -30, 0, -6);
                Panel.SetZIndex(host, 1);   // its bubble over the greeting, if it has to come down beside the mascot
                col.Children.Add(host);
                UseMascot(host);
            }
            FrameworkElement hi = HomeGreeting(13.5, pal.Label2, 260);
            hi.HorizontalAlignment = HorizontalAlignment.Center;
            hi.Margin = new Thickness(0, 8, 0, 0);
            col.Children.Add(hi);
            TextBlock q = Label("\u00BFQu\u00E9 capturamos?", Ds.Title, 34, pal.Label, new Thickness(0, 2, 0, 0));
            q.HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(q);
            Add(col);
            HomeEnter(hi, 30, 0, 8, 1);
            HomeEnter(q, 80, 0, 12, 1);

            // The capture bar: a capsule with a blue pill that follows the mouse.
            Grid barHolder = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 28, 0, 0) };
            HomeModeBar bar = new HomeModeBar(this);
            barHolder.Children.Add(new SoftShadow(bar.Height / 2, 22, 8, pal.Dark ? 0.45 : 0.10));
            barHolder.Children.Add(bar);
            // Never clipped: when the window is narrower than the bar, it scales down to fit.
            Viewbox fit = new Viewbox { Child = barHolder, StretchDirection = StretchDirection.DownOnly, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 28, 0, 0) };
            barHolder.Margin = new Thickness(0);
            Add(fit);
            HomeEnter(fit, 150, 0, 14, 0.96);
            homeEnter.Add(delegate { bar.PlayIntro(210); });

            // The two shortcuts worth learning; one that isn't set is left out instead of reading "Sin atajo para...".
            List<string> hints = new List<string>();
            if (Hotkeys.Split(settings.HotRegion).Count > 0) hints.Add(Hotkeys.Display(settings.HotRegion) + " para un \u00E1rea");
            if (Hotkeys.Split(settings.HotVideo).Count > 0) hints.Add(Hotkeys.Display(settings.HotVideo) + (Recorder.Recording ? " para detener" : " para grabar"));
            if (hints.Count > 0)
            {
                TextBlock keys = Label(string.Join("  \u00B7  ", hints.ToArray()), Ds.Regular, 12, pal.Label3, new Thickness(0, 14, 0, 0));
                keys.HorizontalAlignment = HorizontalAlignment.Center;
                Add(keys);
                HomeEnter(keys, 380, 0, 6, 1);
            }

            if (settings.MascotOn)
            {
                MacButton chips = new MacButton("Jugar con " + settings.MascotName, ButtonKind.Plain, "play", null, 26);
                chips.ToolTip = "P\u00EDdele algo a " + settings.MascotName;
                chips.HorizontalAlignment = HorizontalAlignment.Center;
                chips.Margin = new Thickness(0, 12, 0, 0);
                chips.Click += delegate { CommandMenu(chips); };
                Add(chips);
                HomeEnter(chips, 400, 0, 6, 1);
            }

            List<FileInfo> latest = settings.ShowRecent ? Recents.Latest(settings, 4) : new List<FileInfo>();
            if (latest.Count == 0) return;
            Grid head = new Grid { Margin = new Thickness(0, 40, 0, 10), MaxWidth = 580 };
            head.Children.Add(Label("\u00DAltimas capturas", Ds.Regular, 12.5, pal.Label2));
            head.Children.Add(Link("Ver todas", delegate { owner.OpenFolder(); }));
            Add(head);
            HomeEnter(head, 420, 0, 8, 1);
            Grid row = (Grid)ThumbRow(latest, 4, 580, 10);
            Add(row);
            for (int i = 0; i < row.Children.Count; i++) HomeEnter(row.Children[i], 450 + i * 50, 0, 12, 0.97);
        }

        static readonly string[] ActionShortest = { "\u00C1rea", "Pantalla", "Ventana", "Desplazar", "V\u00EDdeo", "GIF", "Markdown" };

        // The capture modes in one capsule. A blue pill rests on the main mode and glides to the one under the mouse; the
        // labels turn white exactly where it passes. It only draws while the pill or its entrance moves.
        sealed class HomeModeBar : Grid
        {
            const double Tall = 50, Pad = 5, Icon = 18, Gap = 6, Inset = 9, IntroMs = 620;
            static readonly DependencyProperty PillXProperty = DependencyProperty.Register("PillX", typeof(double), typeof(HomeModeBar),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty PillWProperty = DependencyProperty.Register("PillW", typeof(double), typeof(HomeModeBar),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty PushProperty = DependencyProperty.Register("Push", typeof(double), typeof(HomeModeBar),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty IntroProperty = DependencyProperty.Register("Intro", typeof(double), typeof(HomeModeBar),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

            readonly int count, rest;
            readonly string[] icons;
            readonly double[] xs, ws;
            readonly FormattedText[] ink, white;
            readonly Brush fill;
            readonly Pen glow;
            int target;
            bool springing;
            double vx, vw;
            TimeSpan last;

            public HomeModeBar(HomeWindow home)
            {
                Palette pal = Ds.Brushes;
                count = Settings.Actions.Length;
                icons = new string[count];
                xs = new double[count];
                ws = new double[count];
                ink = new FormattedText[count];
                white = new FormattedText[count];
                string[] tips = new string[count];
                double x = Pad;
                for (int i = 0; i < count; i++)
                {
                    string action = Settings.Actions[i];
                    bool stop = action == "video" && Recorder.Recording;
                    string text = stop ? "Detener" : ActionShortest[i];
                    icons[i] = stop ? "stop" : ActionIcons[i];
                    ink[i] = Ink.Text(text, Ds.Medium, 13, pal.Label);
                    white[i] = Ink.Text(text, Ds.Medium, 13, Colors.White);
                    ws[i] = Math.Ceiling(Icon + Gap + ink[i].WidthIncludingTrailingWhitespace + 2 * Inset);
                    xs[i] = x;
                    x += ws[i];
                    string combo = home.settings.HotkeysFor(action);
                    tips[i] = (stop ? "Detener la grabaci\u00F3n" : ActionNames[i]) + (Hotkeys.Split(combo).Count > 0 ? "  (" + Hotkeys.Display(combo) + ")" : "");
                }
                Width = x + Pad;
                Height = Tall;
                int video = Array.IndexOf(Settings.Actions, "video");
                rest = Recorder.Recording && video >= 0 ? video : 0;
                target = rest;
                SetValue(PillXProperty, xs[rest]);
                SetValue(PillWProperty, ws[rest]);
                fill = new LinearGradientBrush(Ds.Rgb(64, 150, 255), pal.Accent, 90);
                fill.Freeze();
                glow = new Pen(new LinearGradientBrush(Ds.Argb(0.42, 255, 255, 255), Ds.Argb(0, 255, 255, 255), 90), 1);
                glow.Freeze();

                // Transparent cells on top take the mouse; the drawing below follows them.
                StackPanel cells = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(Pad, Pad, 0, Pad) };
                for (int i = 0; i < count; i++)
                {
                    int k = i;
                    string action = Settings.Actions[i];
                    Border c = new Border { Width = ws[i], Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tips[i] };
                    ToolTipService.SetInitialShowDelay(c, 900);
                    c.MouseEnter += delegate { Go(k); };
                    c.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
                    {
                        c.CaptureMouse();
                        Go(k);
                        HomeGlide(this, PushProperty, 1, 70, null);
                        e.Handled = true;
                    };
                    c.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                    {
                        bool inside = c.IsMouseCaptured && new Rect(c.RenderSize).Contains(e.GetPosition(c));
                        c.ReleaseMouseCapture();
                        HomeGlide(this, PushProperty, 0, 200, null);
                        e.Handled = true;
                        if (inside) home.RunAction(action);
                    };
                    // Capture taken away mid-press (another window, Alt+Tab): the pill must not stay pressed in.
                    c.LostMouseCapture += delegate { if ((double)GetValue(PushProperty) != 0) HomeGlide(this, PushProperty, 0, 200, null); };
                    cells.Children.Add(c);
                }
                cells.MouseLeave += delegate { if (Mouse.Captured == null) Go(rest); };
                Children.Add(cells);
            }

            void Go(int i)
            {
                if (i == target) return;
                target = i;
                if (springing) return;
                springing = true;
                last = TimeSpan.Zero;
                CompositionTarget.Rendering += Step;
            }

            // Short spring (about 120 ms); the render hook is attached only while the pill moves.
            void Step(object o, EventArgs e)
            {
                TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
                double dt = last == TimeSpan.Zero ? 1.0 / 60 : Math.Min(0.05, (now - last).TotalSeconds);
                last = now;
                if (dt <= 0) return;
                double px = (double)GetValue(PillXProperty), pw = (double)GetValue(PillWProperty);
                Stackshot.Anim.Spring(ref px, ref vx, xs[target], 2400, 0.9, dt);
                Stackshot.Anim.Spring(ref pw, ref vw, ws[target], 2400, 0.9, dt);
                if (Math.Abs(px - xs[target]) < 0.1 && Math.Abs(pw - ws[target]) < 0.1 && Math.Abs(vx) < 1 && Math.Abs(vw) < 1)
                {
                    px = xs[target]; pw = ws[target]; vx = vw = 0;
                    StopSpring();
                }
                SetValue(PillXProperty, px);
                SetValue(PillWProperty, pw);
            }

            void StopSpring()
            {
                if (!springing) return;
                springing = false;
                CompositionTarget.Rendering -= Step;
            }

            // Labels rise in one after another, then the pill pops onto the resting mode.
            public void PlayIntro(double delay)
            {
                target = rest;
                StopSpring();
                vx = vw = 0;
                BeginAnimation(PillXProperty, null);
                BeginAnimation(PillWProperty, null);
                SetValue(PillXProperty, xs[rest]);
                SetValue(PillWProperty, ws[rest]);
                HomeTween(this, IntroProperty, 0, 1, delay, IntroMs, null);
            }

            static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

            protected override void OnRender(DrawingContext dc)
            {
                Palette pal = Ds.Brushes;
                double w = ActualWidth, h = ActualHeight;
                if (w < 1 || h < 1) return;
                Rect all = new Rect(0, 0, w, h);
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Rgb(42, 42, 45) : Colors.White), null, all, h / 2, h / 2);
                Ink.Hairline(dc, pal.Hairline, all, h / 2);

                double t = (double)GetValue(IntroProperty) * IntroMs, push = (double)GetValue(PushProperty);
                double pt = Clamp01((t - 150) / 380), show = Clamp01(pt * 2.4), grow = 0.55 + 0.45 * Stackshot.Ease.OutBack(pt);
                Rect pill = new Rect((double)GetValue(PillXProperty), Pad, (double)GetValue(PillWProperty), h - 2 * Pad);
                pill.Inflate(-push * 1.5, -push * 1.5);
                if (grow < 0.999 || grow > 1.001)
                {
                    Point c = new Point(pill.X + pill.Width / 2, pill.Y + pill.Height / 2);
                    pill = new Rect(c.X - pill.Width * grow / 2, c.Y - pill.Height * grow / 2, pill.Width * grow, pill.Height * grow);
                }
                double r = pill.Height / 2;
                Geometry inside = new RectangleGeometry(pill, r, r);
                if (show > 0)
                {
                    dc.PushOpacity(show);
                    Rect sh = pill;
                    sh.Offset(0, 1.5);
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(pal.Dark ? 0.40 : 0.22, 4, 40, 110)), null, sh, r, r);
                    dc.DrawRoundedRectangle(fill, null, pill, r, r);
                    if (push > 0) dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.12 * push, 0, 0, 0)), null, pill, r, r);
                    dc.DrawRoundedRectangle(null, glow, Ink.Inset(pill, 0.5), r - 0.5, r - 0.5);
                    dc.Pop();
                }

                // Ink outside the pill, white inside it (cross-faded while the pill appears).
                dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(all), inside));
                Items(dc, ink, pal.Label, t);
                dc.Pop();
                dc.PushClip(inside);
                if (show < 1) { dc.PushOpacity(1 - show); Items(dc, ink, pal.Label, t); dc.Pop(); }
                if (show > 0) { dc.PushOpacity(show); Items(dc, white, Colors.White, t); dc.Pop(); }
                dc.Pop();
            }

            void Items(DrawingContext dc, FormattedText[] texts, Color c, double t)
            {
                double h = ActualHeight;
                for (int i = 0; i < count; i++)
                {
                    double k = Clamp01((t - i * 42) / 330);
                    if (k <= 0) continue;
                    double dy = (1 - Stackshot.Ease.OutCubic(k)) * 8;
                    if (k < 1) dc.PushOpacity(k);
                    FormattedText ft = texts[i];
                    double cw = Icon + Gap + ft.WidthIncludingTrailingWhitespace;
                    double x = Math.Round(xs[i] + (ws[i] - cw) / 2), cy = h / 2 + dy;
                    Glyph.Draw(dc, icons[i], x, Math.Round(cy - Icon / 2), Icon, c, 1.6);
                    dc.DrawText(ft, new Point(x + Icon + Gap, Math.Round(cy - ft.Height / 2)));
                    if (k < 1) dc.Pop();
                }
            }
        }

        // Click with a small press-in; act runs on release inside.
        static void Press(FrameworkElement e, Action act)
        {
            ScaleTransform s = new ScaleTransform(1, 1);
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            e.RenderTransform = s;
            e.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs a)
            {
                e.CaptureMouse();
                s.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, TimeSpan.FromMilliseconds(70)));
                s.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, TimeSpan.FromMilliseconds(70)));
                a.Handled = true;
            };
            e.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs a)
            {
                bool inside = e.IsMouseCaptured && new Rect(e.RenderSize).Contains(a.GetPosition(e));
                e.ReleaseMouseCapture();
                s.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
                s.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
                a.Handled = true;
                if (inside) act();
            };
            // Capture lost mid-press (another window took it): spring back instead of staying pressed in.
            e.LostMouseCapture += delegate
            {
                if (s.ScaleX >= 1) return;
                s.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
                s.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            };
        }

        FrameworkElement Link(string text, Action act)
        {
            TextBlock t = Label(text, Ds.Regular, 12.5, Ds.Brushes.Accent);
            t.HorizontalAlignment = HorizontalAlignment.Right;
            t.Cursor = Cursors.Hand;
            t.MouseEnter += delegate { t.TextDecorations = TextDecorations.Underline; };
            t.MouseLeave += delegate { t.TextDecorations = null; };
            t.MouseLeftButtonUp += delegate { act(); };
            return t;
        }

        // ---- The mascot's name: a quiet button that turns into a field to rename it.

        Action homeNameOutside;       // set while the name is being edited
        bool homeNameHooked, homeNameDismissed;   // dismissed: the current press closed the field (it does nothing else)
        const double HomePen = 15;    // room the pencil takes on the right of the name

        // "Buenas tardes, soy Pixel". The pencil hangs past the end of the line, so a centred greeting stays centred; a
        // long name trims to the room the line has. room: widest the field may grow (0: what the line leaves).
        FrameworkElement HomeGreeting(double size, Color color, double room)
        {
            string hello = MascotTalk.Greeting().Replace("\u00A1", "").TrimEnd('!');
            if (!settings.MascotOn) return Label(hello, Ds.Regular, size, color);
            DockPanel row = new DockPanel();
            TextBlock lead = Label(hello + ", soy", Ds.Regular, size, color);
            DockPanel.SetDock(lead, Dock.Left);
            row.Children.Add(lead);
            FrameworkElement tag = HomeNameTag(Ds.Medium, size, Ds.Brushes.Label, false, room);
            tag.Margin = new Thickness(0, 0, -HomePen, 0);
            row.Children.Add(tag);
            return row;
        }

        // The name alone; centred, it gets a spacer on the left as wide as the pencil on the right. It trims to the width
        // it is given and the field never grows past `room` (0: the width of its slot).
        FrameworkElement HomeNameTag(Typeface face, double size, Color color, bool centred, double room)
        {
            Palette pal = Ds.Brushes;
            Grid slot = new Grid();
            Border chip = new Border
            {
                CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 0, 4, 1), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                ToolTip = "Cambiar el nombre", HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left
            };
            ToolTipService.SetInitialShowDelay(chip, 700);
            DockPanel row = new DockPanel();
            if (centred)
            {
                Border spacer = new Border { Width = HomePen - 4 };
                DockPanel.SetDock(spacer, Dock.Left);
                row.Children.Add(spacer);
            }
            GlyphView pen = new GlyphView("edit", Math.Round(size * 0.82), pal.Label2, 1.5);
            pen.Margin = new Thickness(4, 1, 0, 0);
            pen.Opacity = 0;
            DockPanel.SetDock(pen, Dock.Right);
            row.Children.Add(pen);
            TextBlock name = Label(settings.MascotName, face, size, color);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(name);
            chip.Child = row;
            slot.Children.Add(chip);

            TextBox box = new TextBox
            {
                MaxLength = 16, MinWidth = 60, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Padding = new Thickness(4, 0, 4, 1),
                Margin = new Thickness(0),
                TextAlignment = centred ? TextAlignment.Center : TextAlignment.Left,
                Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.4)),
                Background = Ds.Brush(pal.Control), BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.85)), BorderThickness = new Thickness(0, 0, 0, 1.5),
                Template = HomeFieldTemplate()
            };
            slot.Children.Add(box);

            bool editing = false, pressed = false;
            Action outside = null;
            Action<bool> end = null;
            end = delegate(bool keep)
            {
                if (!editing) return;
                editing = false;
                if (homeNameOutside == outside) homeNameOutside = null;
                string typed = box.Text;
                box.Visibility = Visibility.Collapsed;
                chip.Visibility = Visibility.Visible;
                if (keep) HomeRename(typed, name);
                if (homeRefreshLater && homeNameOutside == null)
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate { if (homeRefreshLater && IsVisible && page == "home") HomeRefresh(); });
            };
            chip.MouseEnter += delegate { chip.Background = Ds.Brush(pal.Control); HomeGlide(pen, OpacityProperty, 1, 140, null); };
            chip.MouseLeave += delegate { chip.Background = Brushes.Transparent; HomeGlide(pen, OpacityProperty, 0, 220, null); pressed = false; };
            chip.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; pressed = true; };
            chip.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (!pressed || editing) return;
                pressed = false;
                editing = true;
                box.Text = settings.MascotName;
                double most = room > 0 ? room : Math.Max(80, slot.ActualWidth);
                box.MaxWidth = most;
                box.MinWidth = Math.Min(most, Math.Max(60, name.ActualWidth + 14));
                chip.Visibility = Visibility.Hidden;
                box.Visibility = Visibility.Visible;
                // A click anywhere else ends the edit (clicking a plain element doesn't move the focus).
                outside = delegate { if (!box.IsMouseOver) { homeNameDismissed = true; end(true); } };
                homeNameOutside = outside;
                if (!homeNameHooked)
                {
                    homeNameHooked = true;
                    PreviewMouseLeftButtonDown += delegate { homeNameDismissed = false; Action a = homeNameOutside; if (a != null) a(); };
                }
                box.Focus();
                box.SelectAll();
                Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate { if (editing) { Keyboard.Focus(box); box.SelectAll(); } });
            };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { e.Handled = true; end(true); }
                else if (e.Key == Key.Escape) { e.Handled = true; end(false); }
            };
            box.LostKeyboardFocus += delegate { end(true); };
            // A click inside the open field belongs to it, not to whatever card holds the name.
            slot.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { if (editing) e.Handled = true; };
            return slot;
        }

        // Saves the new name (empty means Pixel) and lets the mascot say it.
        void HomeRename(string typed, TextBlock shown)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            foreach (char c in typed ?? "") if (!char.IsControl(c)) b.Append(c);
            string v = b.ToString().Trim();
            if (v.Length > 16) v = v.Substring(0, 16);
            if (v.Length > 0 && char.IsHighSurrogate(v[v.Length - 1])) v = v.Substring(0, v.Length - 1);   // never half an emoji
            v = v.Trim();
            if (v.Length == 0) v = "Pixel";
            if (shown != null) shown.Text = v;
            if (v == settings.MascotName) return;
            settings.MascotName = v;
            settings.Save();
            BuildWidget();
            if (!settings.MascotOn) return;
            // A direct answer to what the user just did, so it speaks even when its tips are off.
            mascot.Celebrate("\u00A1Ahora me llamo " + v + "!");
            Wake();
        }

        static ControlTemplate homeFieldTemplate;
        static ControlTemplate HomeFieldTemplate()
        {
            if (homeFieldTemplate != null) return homeFieldTemplate;
            homeFieldTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'>" +
                "<Border CornerRadius='6' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>" +
                "<ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/></Border></ControlTemplate>");
            return homeFieldTemplate;
        }

        // ---- Recent captures

        // A row of equal 16:10 thumbnails, `width` wide.
        FrameworkElement ThumbRow(List<FileInfo> files, int count, double width, double gap)
        {
            Grid g = new Grid { MaxWidth = width };
            for (int i = 0; i < count; i++)
            {
                if (i > 0) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
            }
            double cell = (width - gap * (count - 1)) / count;
            for (int i = 0; i < files.Count && i < count; i++)
            {
                FrameworkElement t = Thumb(files[i], cell, cell * 10 / 16, 9);
                Grid.SetColumn(t, i * 2);
                g.Children.Add(t);
            }
            return g;
        }

        // One capture: its image filling a rounded tile; hover shows Copiar / Editar; click opens it.
        FrameworkElement Thumb(FileInfo f, double w, double h, double radius)
        {
            Palette pal = Ds.Brushes;
            string path = f.FullName;
            bool media = ShotStack.IsMediaFile(path);
            Grid g = new Grid { Height = h, Cursor = Cursors.Hand, ToolTip = f.Name + "\n" + f.LastWriteTime.ToString("g") };
            Border img = new Border { CornerRadius = new CornerRadius(radius), Background = Ds.Brush(pal.Thumb), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1) };
            g.Children.Add(img);
            double k = 1.25;
            try { k = VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { }
            Recents.Thumb(path, (int)Math.Ceiling(Math.Max(w, h) * k * 1.4), delegate(BitmapSource src)
            {
                ImageBrush ib = new ImageBrush(src) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
                RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.HighQuality);
                img.Background = ib;
            });
            if (media)
            {
                Border tag = new Border { CornerRadius = new CornerRadius(8), Background = Ds.Brush(Ds.Argb(0.55, 0, 0, 0)), Padding = new Thickness(6, 1, 6, 2),
                                          HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6), IsHitTestVisible = false };
                tag.Child = Label(f.Extension.TrimStart('.').ToUpperInvariant(), Ds.Semibold, 10, Colors.White);
                g.Children.Add(tag);
            }
            Border over = new Border { CornerRadius = new CornerRadius(radius), Background = Ds.Brush(Ds.Argb(0.42, 0, 0, 0)), Opacity = 0 };
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            acts.Children.Add(PillAction("Copiar", delegate(TextBlock l) { CopyFile(path, l); }));
            acts.Children.Add(PillAction(media ? "Abrir" : "Editar", delegate(TextBlock l) { OpenCapture(path); }));
            over.Child = acts;
            g.Children.Add(over);
            g.MouseEnter += delegate { HomeGlide(over, OpacityProperty, 1, 140, null); };
            g.MouseLeave += delegate { HomeGlide(over, OpacityProperty, 0, 200, null); };
            g.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { if (!e.Handled) OpenCapture(path); };
            homeShownFiles.Add(path);
            if (homeShownBefore != null && !homeShownBefore.Contains(path)) homeFresh.Add(g);
            return g;
        }

        FrameworkElement PillAction(string text, Action<TextBlock> act)
        {
            Border b = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 4), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand };
            SolidColorBrush bg = new SolidColorBrush(Ds.Argb(0.22, 255, 255, 255));
            b.Background = bg;
            TextBlock label = Label(text, Ds.Semibold, 11.5, Colors.White);
            b.Child = label;
            b.MouseEnter += delegate { bg.Color = Ds.Argb(0.36, 255, 255, 255); };
            b.MouseLeave += delegate { bg.Color = Ds.Argb(0.22, 255, 255, 255); };
            b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; act(label); };
            return b;
        }

        // A button says what it just did for a moment ("Copiada"), then goes back to its name.
        static void FlashLabel(TextBlock t, string text)
        {
            if (t == null || t.Text == text) return;
            string was = t.Text;
            t.Text = text;
            DispatcherTimer back = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1400) };
            back.Tick += delegate { back.Stop(); t.Text = was; };
            back.Start();
        }

        // A capture that wasn't on Home a moment ago (it was just taken) pops into its place.
        static void HomePop(UIElement e)
        {
            ScaleTransform s = new ScaleTransform();
            TransformGroup g = new TransformGroup();
            if (e.RenderTransform != null && e.RenderTransform != Transform.Identity) g.Children.Add(e.RenderTransform);
            g.Children.Add(s);
            e.RenderTransform = g;
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            HomeTween(e, UIElement.OpacityProperty, 0, 1, 40, 260, new CubicEase { EasingMode = EasingMode.EaseOut });
            HomeTween(s, ScaleTransform.ScaleXProperty, 0.88, 1, 40, 520, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });
            HomeTween(s, ScaleTransform.ScaleYProperty, 0.88, 1, 40, 520, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });
        }

        void OpenCapture(string path)
        {
            if (ShotStack.IsMediaFile(path)) { try { Process.Start(path); } catch (Exception ex) { ShotStack.Log("Abrir: " + ex.Message); } return; }
            owner.OpenEditor(path);
        }

        // Videos and GIFs go as files; an image is decoded on the thread pool (a big PNG would stall the window) and put
        // on the clipboard back on this thread. The button and the mascot confirm it.
        void CopyFile(string path, TextBlock button)
        {
            if (ShotStack.IsMediaFile(path))
            {
                try
                {
                    StringCollection sc = new StringCollection();
                    sc.Add(path);
                    WF.Clipboard.SetFileDropList(sc);
                    Copied(button);
                }
                catch (Exception ex) { ShotStack.Log("Copiar: " + ex.Message); }
                return;
            }
            Dispatcher ui = Dispatcher;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                D.Bitmap img = null;
                string error = null;
                try { img = ShotStack.LoadFull(path); }
                catch (Exception ex) { error = ex.Message; }
                ui.BeginInvoke((Action)delegate
                {
                    if (img == null) { ShotStack.Log("Copiar: " + (error ?? path)); return; }
                    if (IsDisposed) { img.Dispose(); return; }
                    try { owner.CopyTracked(path, img, false, true); Copied(button); }
                    catch (Exception ex) { ShotStack.Log("Copiar: " + ex.Message); }
                });
            });
        }

        void Copied(TextBlock button)
        {
            FlashLabel(button, "Copiada");
            if (settings.MascotOn) mascot.Say("\u00A1Copiada!", 1600);
            Wake();
        }

        // ---- Widgets: today at a glance.

        void BuildHomeWidgets()
        {
            Palette pal = Ds.Brushes;
            body.MaxWidth = 760;
            // The title row is centred on the switch, which keeps the spot it has in the other views.
            Grid head = new Grid { Margin = new Thickness(0, HomeSwitchTop - 3, 0, 16) };
            StackPanel titles = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Label("Hoy", Ds.Title, 26, pal.Label));
            string date = DateTime.Now.ToString("dddd, d 'de' MMMM", new System.Globalization.CultureInfo("es-ES"));
            titles.Children.Add(Label(char.ToUpper(date[0]) + date.Substring(1), Ds.Regular, 12.5, pal.Label2, new Thickness(12, 8, 0, 0)));
            head.Children.Add(titles);
            Add(head);
            HomeEnter(titles, 0, 0, 8, 1);

            Grid grid = new Grid { Height = 470 };
            for (int i = 0; i < 4; i++) { if (i > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); }
            for (int i = 0; i < 3; i++) { if (i > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); grid.RowDefinitions.Add(new RowDefinition()); }
            bool recent = settings.ShowRecent;   // off: no last-capture card and no thumbnails; the others take its room
            List<FileInfo> latest = recent ? Recents.Latest(settings, 1) : new List<FileInfo>();

            // Last capture (2x2).
            Grid last = recent ? HomeTile(grid, 0, 0, 2, 2, null, false) : new Grid();
            if (latest.Count > 0)
            {
                FileInfo f = latest[0];
                Grid lg = new Grid();
                lg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                lg.RowDefinitions.Add(new RowDefinition());
                lg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid t = new Grid();
                t.Children.Add(Label("\u00DAltima captura", Ds.Semibold, 12, pal.Label));
                TextBlock when = Label(f.LastWriteTime.ToString("HH:mm"), Ds.Regular, 12, pal.Label3);
                when.HorizontalAlignment = HorizontalAlignment.Right;
                t.Children.Add(when);
                lg.Children.Add(t);
                FrameworkElement th = Thumb(f, 300, 180, 12);
                ((Grid)th).Height = double.NaN;
                th.Margin = new Thickness(0, 10, 0, 10);
                Grid.SetRow(th, 1);
                lg.Children.Add(th);
                UniformGrid acts = new UniformGrid { Columns = 3 };
                acts.Children.Add(WidgetButton("Copiar", true, delegate(TextBlock l) { CopyFile(f.FullName, l); }));
                acts.Children.Add(WidgetButton(ShotStack.IsMediaFile(f.FullName) ? "Abrir" : "Editar", false, delegate(TextBlock l) { OpenCapture(f.FullName); }));
                bool kept = SameFolder(f.DirectoryName, settings.SaveFolder);
                acts.Children.Add(WidgetButton(kept ? "Mostrar" : "Guardar", false, delegate(TextBlock l)
                {
                    if (kept) { try { Process.Start(Native.Explorer, "/select,\"" + f.FullName + "\""); } catch { } }
                    else HomeKeep(f.FullName);
                }));
                Grid.SetRow(acts, 2);
                lg.Children.Add(acts);
                last.Children.Add(lg);
            }
            else
            {
                StackPanel empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                empty.Children.Add(new GlyphView("photo", 34, pal.Label3, 1.5));
                TextBlock et = Label("Aqu\u00ED ver\u00E1s tu \u00FAltima captura", Ds.Regular, 13, pal.Label2, new Thickness(0, 10, 0, 0));
                et.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Children.Add(et);
                last.Children.Add(empty);
            }

            // Captures today, with the last five days.
            int[] days = Recents.PerDay(settings, 5);
            Grid today = HomeTile(grid, recent ? 2 : 0, 0, recent ? 1 : 2, recent ? 1 : 2, new LinearGradientBrush(Ds.Rgb(60, 139, 255), Ds.Rgb(94, 92, 230), 60), false);
            StackPanel tp = new StackPanel();
            tp.Children.Add(Label("Capturas hoy", Ds.Semibold, 12, Ds.Argb(0.85, 255, 255, 255)));
            StackPanel num = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            num.Children.Add(Label(days[4].ToString(), Ds.Title, 40, Colors.White));
            int diff = days[4] - days[3];
            if (diff != 0) num.Children.Add(Label((diff > 0 ? "+" : "") + diff + " vs ayer", Ds.Regular, 12, Ds.Argb(0.8, 255, 255, 255), new Thickness(8, 18, 0, 0)));
            tp.Children.Add(num);
            today.Children.Add(tp);
            int max = 1;
            foreach (int d in days) max = Math.Max(max, d);
            Grid bars = new Grid { Height = recent ? 24 : 60, VerticalAlignment = VerticalAlignment.Bottom };
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                bars.ColumnDefinitions.Add(new ColumnDefinition());
                Border bb = new Border { CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, Height = Math.Max(3, (recent ? 24.0 : 60.0) * days[i] / max),
                                         Background = Ds.Brush(i == 4 ? Colors.White : Ds.Argb(0.4, 255, 255, 255)) };
                Grid.SetColumn(bb, i * 2);
                bars.Children.Add(bb);
                // The bars grow from the floor as the widget comes in.
                ScaleTransform up = new ScaleTransform(1, 1);
                bb.RenderTransformOrigin = new Point(0.5, 1);
                bb.RenderTransform = up;
                double at = 260 + i * 55;
                homeEnter.Add(delegate { HomeTween(up, ScaleTransform.ScaleYProperty, 0, 1, at, 520, new QuinticEase { EasingMode = EasingMode.EaseOut }); });
            }
            today.Children.Add(bars);

            // The mascot.
            // The mascot. A click on it pokes it, as everywhere else; the rest of the tile opens its page.
            Grid mw = HomeTile(grid, 3, 0, 1, 1, null, true);
            mw.Cursor = Cursors.Hand;
            if (settings.MascotOn)
            {
                StackPanel mp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                MascotHost host = new MascotHost(mascot, 58, true);
                host.HorizontalAlignment = HorizontalAlignment.Center;
                host.Margin = new Thickness(0, -22, 0, -4);
                mp.Children.Add(host);
                UseMascot(host);
                FrameworkElement mn = HomeNameTag(Ds.Semibold, 13, pal.Label, true, 0);
                mp.Children.Add(mn);
                int left;
                MascotParts.Progress(settings.MascotLove, out left);
                TextBlock lv = Label("Nivel " + (MascotParts.Level(settings.MascotLove) + 1) + (left > 0 ? " \u00B7 " + left + " para subir" : ""), Ds.Regular, 11, pal.Label2);
                lv.HorizontalAlignment = HorizontalAlignment.Center;
                mp.Children.Add(lv);
                mw.Children.Add(mp);
                // A small play glyph in the corner hints that the tile opens the command menu.
                GlyphView playGlyph = new GlyphView("play", 12, pal.Label3, 1.5);
                playGlyph.HorizontalAlignment = HorizontalAlignment.Right;
                playGlyph.VerticalAlignment = VerticalAlignment.Bottom;
                playGlyph.IsHitTestVisible = false;
                mw.Children.Add(playGlyph);
                mw.ToolTip = "P\u00EDdele algo a " + settings.MascotName;
                // A click that only closes the name field (to confirm it) stays here.
                mw.MouseLeftButtonUp += delegate { if (!host.IsMouseOver && !homeNameDismissed) CommandMenu(mw); };
            }
            else
            {
                // Hidden, the tile still leads to its page, where it can be called back.
                StackPanel off = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                GlyphView bot = new GlyphView("bot", 30, pal.Label3, 1.4);
                bot.HorizontalAlignment = HorizontalAlignment.Center;
                off.Children.Add(bot);
                TextBlock t = Label("Mascota escondida", Ds.Medium, 12.5, pal.Label, new Thickness(0, 8, 0, 0));
                t.HorizontalAlignment = HorizontalAlignment.Center;
                off.Children.Add(t);
                TextBlock go = Label("Mostrar en Mascota", Ds.Regular, 11, pal.Accent, new Thickness(0, 2, 0, 0));
                go.HorizontalAlignment = HorizontalAlignment.Center;
                off.Children.Add(go);
                mw.Children.Add(off);
                mw.MouseLeftButtonUp += delegate { SetPage("mascot", true); };
            }

            // Every capture mode.
            Grid quick = HomeTile(grid, 2, 1, 2, 1, null, false);
            UniformGrid qg = new UniformGrid { Columns = 4, Rows = 2 };
            for (int i = 0; i < Settings.Actions.Length; i++) qg.Children.Add(QuickMode(i));
            quick.Children.Add(qg);

            // Recording.
            Grid rec = HomeTile(grid, 0, 2, recent ? 1 : 2, 1, null, true);
            string[] qn = { "Est\u00E1ndar", "M\u00E1xima", "Alta", "Cine" };
            StackPanel rp = new StackPanel();
            rp.Children.Add(Label("Grabar", Ds.Semibold, 12, pal.Label));
            StackPanel rr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 8) };
            bool recording = Recorder.Recording;
            Grid dotBox = new Grid { Width = 30, Height = 30 };
            dotBox.Children.Add(new System.Windows.Shapes.Ellipse { Fill = Ds.Brush(pal.Red), Stroke = Ds.Brush(Ds.WithAlpha(pal.Red, 0.25)), StrokeThickness = 5 });
            // While it records, the button is the way to stop: a stop square sits in the dot.
            if (recording) dotBox.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2), Background = Brushes.White });
            rr.Children.Add(dotBox);
            StackPanel rq = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            rq.Children.Add(Label(recording ? "Detener" : qn[Math.Max(0, Math.Min(3, settings.VideoQuality))], Ds.Semibold, 13, pal.Label));
            rq.Children.Add(Label(recording ? "Grabando\u2026" : (settings.RecordSystemAudio || settings.RecordMic) ? "con sonido" : "sin sonido", Ds.Regular, 11, pal.Label2));
            rec.ToolTip = recording ? "Detener la grabaci\u00f3n" : "Grabar v\u00eddeo";
            ToolTipService.SetInitialShowDelay(rec, 900);
            rr.Children.Add(rq);
            rp.Children.Add(rr);
            rp.Children.Add(Label(Hotkeys.Display(settings.HotVideo), Ds.Regular, 11, pal.Label3));
            rec.Children.Add(rp);
            rec.Cursor = Cursors.Hand;
            Press(rec, delegate { RunAction("video"); });

            // The save folder; its size arrives from the thread pool and is filled in place.
            Grid fold = HomeTile(grid, recent ? 1 : 2, recent ? 2 : 0, 1, 1, null, true);
            StackPanel fp = new StackPanel();
            fp.Children.Add(Label("Carpeta", Ds.Semibold, 12, pal.Label));
            TextBlock size = null;
            size = Label(Recents.Size(Recents.FolderBytes(settings, delegate { if (size != null) size.Text = Recents.Size(Recents.FolderBytes(settings, null)); })),
                         Ds.Title, 22, pal.Label, new Thickness(0, 10, 0, 6));
            fp.Children.Add(size);
            TextBlock where = Label(ShortPath(settings.SaveFolder), Ds.Regular, 11, pal.Label2);
            where.TextTrimming = TextTrimming.CharacterEllipsis;
            fp.Children.Add(where);
            fold.Children.Add(fp);
            fold.Cursor = Cursors.Hand;
            Press(fold, delegate { owner.OpenFolder(); });

            // Tip of the day from the mascot's catalog of tips.
            Grid tip = HomeTile(grid, 2, 2, 2, 1, null, false);
            StackPanel tpp = new StackPanel();
            StackPanel th2 = new StackPanel { Orientation = Orientation.Horizontal };
            th2.Children.Add(new GlyphView("sparkle", 14, pal.Orange, 1.6));
            th2.Children.Add(Label("Truco", Ds.Semibold, 12, pal.Label, new Thickness(6, 0, 0, 0)));
            tpp.Children.Add(th2);
            TextBlock tt = Paragraph(MascotTalk.Tip(settings), 12.5, pal.Label2);
            tt.Margin = new Thickness(0, 8, 0, 0);
            tpp.Children.Add(tt);
            tip.Children.Add(tpp);

            Add(grid);
        }

        // Keeps a temporary capture in the save folder; the listing is read again so the tile offers Mostrar at once.
        void HomeKeep(string path)
        {
            try { owner.Keep(path); }
            catch (Exception ex) { ShotStack.Log("Guardar: " + ex.Message); return; }
            Recents.Forget();
            Rebuild();
        }

        // The same folder however it is written (trailing slash, short 8.3 names, relative parts).
        static bool SameFolder(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        }

        // A rounded widget cell placed on the grid (columns and rows count the gap tracks).
        Grid Widget(Grid grid, int col, int row, int cols, int rows, Brush fill)
        {
            Grid holder;
            return WidgetCell(grid, col, row, cols, rows, fill, out holder);
        }

        Grid WidgetCell(Grid grid, int col, int row, int cols, int rows, Brush fill, out Grid holder)
        {
            Palette pal = Ds.Brushes;
            holder = new Grid();
            Grid.SetColumn(holder, col * 2);
            Grid.SetRow(holder, row * 2);
            Grid.SetColumnSpan(holder, cols * 2 - 1);
            Grid.SetRowSpan(holder, rows * 2 - 1);
            if (!pal.Dark) holder.Children.Add(new SoftShadow(20, 10, 2, 0.06));
            Border card = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = fill ?? Ds.Brush(pal.Dark ? Ds.Rgb(42, 42, 45) : Colors.White),
                BorderBrush = Ds.Brush(fill != null ? Colors.Transparent : pal.Hairline),
                BorderThickness = new Thickness(1)
            };
            holder.Children.Add(card);
            Grid inside = new Grid { Margin = new Thickness(14) };
            card.Child = inside;
            grid.Children.Add(holder);
            return inside;
        }

        // A Home widget: comes in with the others in reading order. Widgets that are a click target lift a little under
        // the mouse (and their content area has no dead gaps between its texts); the ones that only inform stay put.
        Grid HomeTile(Grid grid, int col, int row, int cols, int rows, Brush fill, bool lifts)
        {
            Grid holder;
            Grid inside = WidgetCell(grid, col, row, cols, rows, fill, out holder);
            if (lifts)
            {
                inside.Background = Brushes.Transparent;
                // The deeper shadow is a blurred bitmap: made on the first hover, not with the page.
                SoftShadow lift = null;
                Grid card = holder;
                TranslateTransform up = new TranslateTransform();
                holder.RenderTransform = up;
                holder.MouseEnter += delegate
                {
                    if (lift == null)
                    {
                        lift = new SoftShadow(20, 18, 7, Ds.Brushes.Dark ? 0.55 : 0.11) { Opacity = 0 };
                        card.Children.Insert(0, lift);
                    }
                    HomeGlide(up, TranslateTransform.YProperty, -2, 220, null);
                    HomeGlide(lift, OpacityProperty, 1, 220, null);
                };
                holder.MouseLeave += delegate
                {
                    HomeGlide(up, TranslateTransform.YProperty, 0, 280, null);
                    if (lift != null) HomeGlide(lift, OpacityProperty, 0, 280, null);
                };
            }
            HomeEnter(holder, 40 + homeTileOrder++ * 50, 0, 16, 0.97);
            return inside;
        }

        FrameworkElement WidgetButton(string text, bool primary, Action<TextBlock> act)
        {
            Palette pal = Ds.Brushes;
            Border b = new Border { CornerRadius = new CornerRadius(8), Height = 30, Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand };
            Color rest = primary ? pal.Accent : pal.Control, over = primary ? Ds.Rgb(48, 150, 255) : pal.ControlHover;
            SolidColorBrush bg = new SolidColorBrush(rest);
            b.Background = bg;
            TextBlock t = Label(text, Ds.Semibold, 12, primary ? Colors.White : pal.Label);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            b.Child = t;
            b.MouseEnter += delegate { bg.Color = over; };
            b.MouseLeave += delegate { bg.Color = rest; };
            Press(b, delegate { act(t); });
            return b;
        }

        FrameworkElement QuickMode(int i)
        {
            Palette pal = Ds.Brushes;
            string action = Settings.Actions[i];
            // While a recording runs, the video mode stops it (as in the other views).
            bool stop = action == "video" && Recorder.Recording;
            Border b = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(3), Cursor = Cursors.Hand, ToolTip = stop ? "Detener la grabaci\u00F3n" : ActionNames[i] };
            SolidColorBrush bg = new SolidColorBrush(pal.Control);
            b.Background = bg;
            StackPanel p = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            p.Children.Add(new IconTile(stop ? "stop" : ActionIcons[i], 22, W(ActionColors[i, 0]), W(ActionColors[i, 1])));
            ((FrameworkElement)p.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;
            TextBlock lab = Label(stop ? "Detener" : ActionShortest[i], Ds.Semibold, 11, pal.Label, new Thickness(0, 6, 0, 0));
            lab.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(lab);
            b.Child = p;
            b.MouseEnter += delegate { bg.Color = pal.ControlHover; };
            b.MouseLeave += delegate { bg.Color = pal.Control; };
            Press(b, delegate { RunAction(action); });
            return b;
        }

        // ---- Scene: the mascot on stage, the modes as a list beside it and the recent captures below.

        const double SceneStage = 430, SceneBack = 680;

        void BuildHomeScene()
        {
            Palette pal = Ds.Brushes;
            body.MaxWidth = double.PositiveInfinity;
            body.Margin = new Thickness(0, 0, 0, 30);
            Grid scene = new Grid();
            StackPanel content = new StackPanel();
            Grid stage = new Grid { Height = SceneStage };
            content.Children.Add(stage);

            // The stage light and the mascot's glow live on a layer behind everything, taller than the stage: it runs on
            // under the recent captures and fades into the window instead of stopping at an edge.
            Grid back = new Grid { Height = SceneBack, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 0, -SceneBack), IsHitTestVisible = false };
            Color top = pal.Dark ? Ds.Rgb(40, 38, 46) : Colors.White;
            LinearGradientBrush wash = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            wash.GradientStops.Add(new GradientStop(top, 0));
            wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(top, 0.85), 0.3));
            wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(top, 0.45), 0.58));
            wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(top, 0.14), 0.8));
            wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(top, 0), 1));
            back.Children.Add(new Border { Background = wash });
            scene.Children.Add(back);

            if (settings.MascotOn)
            {
                const double box = 170, left = 40, bottom = 34;
                double cx = left + box * 1.7 / 2, cy = SceneStage - bottom - box * 0.06 - box / 2, feet = SceneStage - bottom - box * 0.06;
                Color mc = W(MascotParts.Colors[Math.Max(0, Math.Min(MascotParts.Colors.GetLength(0) - 1, MascotLook.From(settings).Color)), 0]);
                double a = pal.Dark ? 0.34 : 0.32, gw = 600, gh = 620;
                RadialGradientBrush glow = new RadialGradientBrush();
                glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(mc, a), 0));
                glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(mc, a * 0.78), 0.22));
                glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(mc, a * 0.45), 0.48));
                glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(mc, a * 0.16), 0.74));
                glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(mc, 0), 1));
                Border halo = new Border
                {
                    Width = gw, Height = gh, Background = glow, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(cx - gw / 2, cy - gh / 2, 0, 0)
                };
                // Cached as a bitmap: the fade and growth then act on a smooth texture (a faint gradient re-rasterized at low
                // opacity shows rings) and the mascot redrawing in front no longer re-rasterizes it.
                halo.CacheMode = new BitmapCache();
                back.Children.Add(halo);
                HomeEnter(halo, 0, 0, 0, 0.9, 900);

                // A pool of light on the floor where it stands.
                RadialGradientBrush pool = new RadialGradientBrush();
                Color lit = Colors.White;
                pool.GradientStops.Add(new GradientStop(Ds.WithAlpha(lit, pal.Dark ? 0.07 : 0.75), 0));
                pool.GradientStops.Add(new GradientStop(Ds.WithAlpha(lit, pal.Dark ? 0.03 : 0.32), 0.55));
                pool.GradientStops.Add(new GradientStop(Ds.WithAlpha(lit, 0), 1));
                Border floor = new Border
                {
                    Width = 300, Height = 64, Background = pool, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(cx - 150, feet - 30, 0, 0)
                };
                back.Children.Add(floor);
                HomeEnter(floor, 120, 0, 0, 1, 700);

                MascotHost host = new MascotHost(mascot, box, false);
                host.HorizontalAlignment = HorizontalAlignment.Left;
                host.VerticalAlignment = VerticalAlignment.Bottom;
                host.Margin = new Thickness(left, 0, 0, bottom);
                stage.Children.Add(host);
                UseMascot(host);
                MacButton play = new MacButton("Jugar con " + settings.MascotName, ButtonKind.Plain, "play", null, 26);
                play.ToolTip = "P\u00EDdele algo a " + settings.MascotName;
                play.Click += delegate { CommandMenu(play); };
                play.HorizontalAlignment = HorizontalAlignment.Left;
                play.VerticalAlignment = VerticalAlignment.Bottom;
                play.Margin = new Thickness(left + box * 1.7 / 2 - 62, 0, 0, 2);
                Panel.SetZIndex(play, 2);
                stage.Children.Add(play);
            }
            StackPanel right = new StackPanel { Width = 300, HorizontalAlignment = settings.MascotOn ? HorizontalAlignment.Right : HorizontalAlignment.Center, Margin = new Thickness(0, 74, 40, 0) };
            FrameworkElement hi = HomeGreeting(13, pal.Label2, 0);
            right.Children.Add(hi);
            TextBlock q = Label("\u00BFQu\u00E9 capturamos?", Ds.Title, 30, pal.Label, new Thickness(0, 0, 0, 10));
            right.Children.Add(q);
            HomeEnter(hi, 40, 18, 0, 1);
            HomeEnter(q, 90, 18, 0, 1);
            // The blue row is the main mode, or stopping while a recording runs (as the minimal bar does).
            int lead = Recorder.Recording ? Math.Max(0, Array.IndexOf(Settings.Actions, "video")) : 0;
            for (int i = 0; i < Settings.Actions.Length; i++)
            {
                FrameworkElement mr = ModeRow(i, i == lead);
                right.Children.Add(mr);
                HomeEnter(mr, 150 + i * 45, 22, 0, 1);
            }
            stage.Children.Add(right);

            List<FileInfo> latest = settings.ShowRecent ? Recents.Latest(settings, 5) : new List<FileInfo>();
            if (latest.Count > 0)
            {
                Grid head = new Grid { Margin = new Thickness(40, 22, 40, 10) };
                head.Children.Add(Label("Recientes", Ds.Semibold, 13, pal.Label));
                head.Children.Add(Link("Ver todas", delegate { owner.OpenFolder(); }));
                content.Children.Add(head);
                HomeEnter(head, 400, 0, 8, 1);
                // Full width under its heading, so the last thumbnail ends where "Ver todas" does.
                Grid row = (Grid)ThumbRow(latest, 5, 664, 12);
                row.Margin = new Thickness(40, 0, 40, 0);
                content.Children.Add(row);
                for (int i = 0; i < row.Children.Count; i++) HomeEnter(row.Children[i], 440 + i * 50, 0, 12, 0.97);
            }
            scene.Children.Add(content);
            Add(scene);
        }

        FrameworkElement ModeRow(int i, bool primary)
        {
            Palette pal = Ds.Brushes;
            string action = Settings.Actions[i];
            bool stop = action == "video" && Recorder.Recording;
            Border b = new Border { Height = 34, CornerRadius = new CornerRadius(9), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 3) };
            Color rest = primary ? pal.Accent : Colors.Transparent, over = primary ? Ds.Rgb(48, 150, 255) : pal.ControlHover;
            SolidColorBrush bg = new SolidColorBrush(rest);
            b.Background = bg;
            Color ink = primary ? Colors.White : pal.Label;
            Grid g = new Grid { Margin = new Thickness(10, 0, 10, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new GlyphView(stop ? "stop" : ActionIcons[i], 18, ink, 1.7));
            TextBlock t = Label(stop ? "Detener grabaci\u00F3n" : ActionShort[i], Ds.Medium, 13, ink, new Thickness(10, 0, 0, 0));
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            string combo = settings.HotkeysFor(action);
            TextBlock k = Label(Hotkeys.Split(combo).Count > 0 ? Hotkeys.Display(combo).Replace(" + ", "+") : "", Ds.Regular, 11.5, primary ? Ds.Argb(0.78, 255, 255, 255) : pal.Label3);
            Grid.SetColumn(k, 2);
            g.Children.Add(k);
            b.Child = g;
            b.MouseEnter += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(over, TimeSpan.FromMilliseconds(120))); };
            b.MouseLeave += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(rest, TimeSpan.FromMilliseconds(200))); };
            Press(b, delegate { RunAction(action); });
            return b;
        }
    }
}
