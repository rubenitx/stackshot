// Stackshot - Main window: home with the mascot, hotkeys and all settings, in the style of macOS System Settings.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using D = System.Drawing;
using WF = System.Windows.Forms;

namespace Stackshot
{
    // Sidebar on Mica, the current section on a solid pane that scrolls under a toolbar. Changes apply immediately (no
    // Save button). Closing it keeps Stackshot in the tray. Nothing ticks while hidden; while shown, only the mascot
    // layer is redrawn, and only as often as it moves.
    public partial class HomeWindow : Sheet
    {
        const double LW = 980, LH = 660, Side = 236, Bar = 52;
        static readonly string[] PageIds = { "home", "history", "keys", "general", "editor", "record", "mascot", "about" };
        static readonly string[] PageNamesEs = { "Inicio", "Historial", "Atajos", "General", "Fondo y editor", "Grabaci\u00F3n", "Mascota", "Acerca de" };
        static readonly string[] PageNamesEn = { "Home", "History", "Shortcuts", "General", "Backdrop & editor", "Recording", "Mascot", "About" };
        static string[] PageNames { get { return I18n.IsSpanish ? PageNamesEs : PageNamesEn; } }
        static readonly string[] PageIcons = { "home", "history", "keyboard", "gear", "photo", "video", "bot", "info" };
        static readonly D.Color[] PageColors = { D.Color.FromArgb(10, 132, 255), D.Color.FromArgb(94, 92, 230), D.Color.FromArgb(142, 142, 147), D.Color.FromArgb(142, 142, 147),
                                                 D.Color.FromArgb(175, 82, 222), D.Color.FromArgb(255, 59, 48), D.Color.FromArgb(255, 149, 0),
                                                 D.Color.FromArgb(142, 142, 147) };

        readonly ShotStack owner;
        readonly Settings settings;
        string page = "home";
        readonly Grid sidebar = new Grid(), pane = new Grid();
        readonly StackPanel navList = new TapPanel { Interactive = false, Role = Access.Tab, Label = "Secciones" };
        readonly Border widgetSlot = new Border();
        readonly ScrollViewer scroller = new ScrollViewer();
        readonly Grid pageHost = new Grid();
        readonly ToolbarStrip toolbar = new ToolbarStrip();
        readonly TextBlock toolbarTitle = new TextBlock();
        readonly List<FrameworkElement> bubbleAvoid = new List<FrameworkElement>();    // controls the speech bubble must not cover
        StackPanel body;                       // current section column
        readonly Mascot mascot = new Mascot();
        MascotHost mascotHost;                 // where the mascot lives on this page (hero, stage or sidebar)
        readonly FrameClock timer = new FrameClock();
        double quietUntil;            // a page is entering: the mascot ticks at half rate until it has landed
        GdiLayer introLayer;
        Intro intro;
        bool introGreeted;
        HotkeyRow listening;
        D.Point lastScreenMouse;
        double nextTip, lastMove;
        bool active;
        static readonly Random Rng = new Random();

        public HomeWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            Title = "Stackshot";
            Width = LW;
            Height = LH;
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (AppIcon != null) Icon = AppIcon;
            mascot.Look = MascotLook.From(settings);
            ApplyMascotBehavior();
            Center();

            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Side) });
            Root.ColumnDefinitions.Add(new ColumnDefinition());
            sidebar.MouseLeftButtonDown += DragFromBackground;
            BuildSidebar();
            BuildPane();
            KeepCaptionsOnTop();

            timer.Tick += Tick;
            owner.Captured += OnCaptured;
            Updater.Changed += OnUpdaterChanged;
            I18n.Changed += OnLanguageChanged;
            Closing += delegate(object o, System.ComponentModel.CancelEventArgs e)
            {
                if (ShuttingDown) return;
                e.Cancel = true;
                HideToTray();
            };
            Closed += delegate
            {
                timer.Stop();
                owner.Captured -= OnCaptured;
                Updater.Changed -= OnUpdaterChanged;
                I18n.Changed -= OnLanguageChanged;
            };
            IsVisibleChanged += delegate { VisibilityChanged(); };
            StateChanged += delegate { VisibilityChanged(); };
            // Brought to the front: back to its full rate at once, not at the next slow check.
            Activated += delegate { active = true; covered = false; if (timer.IsEnabled) timer.Interval = MascotRate.Anim; else Wake(); };
            Deactivated += delegate { active = false; CancelListening(); };
            PreviewKeyDown += OnKeyDown;
            PreviewKeyUp += OnKeyUp;
            PreviewMouseDown += OnMouseBack;
            Build();
        }

        void OnLanguageChanged()
        {
            // As for a theme change: the sidebar (version, search box) is built once, so it is built again with the page.
            Dispatcher.BeginInvoke((Action)delegate { BuildSidebar(); Build(); });
        }

        void Center()
        {
            WF.Screen scr = WF.Screen.FromPoint(WF.Control.MousePosition);
            double k = ShotStack.ScaleFor(scr);
            D.Rectangle wa = scr.WorkingArea;
            Left = (wa.Left + (wa.Width - LW * k) / 2) / k;
            Top = (wa.Top + Math.Max(0, (wa.Height - LH * k) / 2 - 10 * k)) / k;
        }

        public void Present(string pageId, bool withIntro)
        {
            bool prewarming = Left < -30000;
            if (prewarming) { ShowInTaskbar = true; ShowActivated = true; Center(); }
            bool wasHidden = !IsVisible || WindowState == WindowState.Minimized || prewarming;
            if (pageId != null && pageId != page) SetPage(pageId, false);
            if (!IsVisible)
            {
                bool onScreen = false;
                double k = ShotStack.ScaleFor(WF.Screen.FromPoint(WF.Control.MousePosition));
                D.Rectangle me = new D.Rectangle((int)(Left * k), (int)(Top * k), (int)(LW * k), (int)(LH * k));
                foreach (WF.Screen sc in WF.Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(me)) onScreen = true;
                if (!onScreen) Center();
            }
            if (withIntro) StartIntro();
            // Asked for while it was still warming up off screen: it is already "visible", so nothing else would tell the
            // stack (and the desktop pet) that Home is now on screen.
            if (prewarming && IsVisible) VisibilityChanged();
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            if (Handle != IntPtr.Zero) Native.ForceForeground(Handle);
            if (intro == null && wasHidden && settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(MascotTalk.Hello(settings));
            }
            nextTip = Anim.Now + 25000;
            Wake();
        }

        // Renders once off-screen and hides again: the graphics device, fonts and layout are ready, so the first real
        // open takes a fraction of the time.
        public void Prewarm()
        {
            if (IsVisible) return;
            double left = Left, top = Top;
            ShowInTaskbar = false;
            ShowActivated = false;
            Left = -32000;
            EventHandler done = null;
            done = delegate
            {
                ContentRendered -= done;
                if (!ShowInTaskbar && Left < -30000)
                {
                    Hide();
                    Left = left;
                    Top = top;
                }
                ShowInTaskbar = true;
                ShowActivated = true;
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)PrepareMascotPage);
            };
            ContentRendered += done;
            Show();
        }

        void HideToTray()
        {
            CancelListening();
            if (!settings.CloseToTray) { owner.Quit(); return; }
            Hide();
        }

        void VisibilityChanged()
        {
            bool shown = IsVisible && WindowState != WindowState.Minimized && Left > -30000;
            if (shown) Wake();
            else
            {
                timer.Stop();
                if (mascotHost != null) mascotHost.Layer.Release();
            }
            BrandTimer(shown);
            owner.HomeShown(shown);
        }

        protected override void ThemeChanged()
        {
            BuildSidebar();
            Build();
        }

        // ---- Layout

        void BuildSidebar()
        {
            ((TapPanel)navList).Label = I18n.T("Secciones", "Sections");
            sidebar.Children.Clear();
            sidebar.RowDefinitions.Clear();
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78 + SearchBand) });
            sidebar.RowDefinitions.Add(new RowDefinition());
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.Background = HasMica ? Ds.Brush(Ds.Brushes.Dark ? Ds.Argb(0.10, 0, 0, 0) : Ds.Argb(0.18, 255, 255, 255)) : Ds.Brush(Ds.Brushes.Sidebar);
            if (sidebar.Parent == null) { Grid.SetColumn(sidebar, 0); Root.Children.Add(sidebar); }

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 26, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            brand.Children.Add(BrandMark());
            StackPanel names = new StackPanel { Margin = new Thickness(10, -1, 0, 0) };
            names.Children.Add(Label("Stackshot", Ds.Title, 15, Ds.Brushes.Label));
            names.Children.Add(Label(I18n.Fmt("Versi\u00F3n {0}", "Version {0}", Installer.MyVersion.ToString(3)), Ds.Regular, 11.5, Ds.Brushes.Label3));
            brand.Children.Add(names);
            sidebar.Children.Add(brand);
            sidebar.Children.Add(SearchField());

            navList.Margin = new Thickness(10, 4, 10, 0);
            Grid.SetRow(navList, 1);
            sidebar.Children.Add(navList);
            RefreshNav();

            widgetSlot.Margin = new Thickness(12, 0, 12, 14);
            Grid.SetRow(widgetSlot, 2);
            sidebar.Children.Add(widgetSlot);

            Border line = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Background = Ds.Brush(Ds.Brushes.Separator) };
            Grid.SetRowSpan(line, 3);
            sidebar.Children.Add(line);
        }

        // One row per section, built with the sidebar (and again on a theme change); moving between sections only changes
        // how the rows look, so the list never flickers and the mouse keeps its hover.
        sealed class NavRow
        {
            public string Id;
            public TapBorder Box;
            public SolidColorBrush Fill;
            public TextBlock Name;
            public Border Bar;
            public bool On, Hot, Shown;
        }

        readonly List<NavRow> navRows = new List<NavRow>();

        void RefreshNav()
        {
            navList.Children.Clear();
            navRows.Clear();
            for (int i = 0; i < PageIds.Length; i++) if (PageShown(PageIds[i])) navList.Children.Add(NavItem(i));
            UpdateNav(false);
        }

        // The mascot's pages only where the profile has a mascot (Rendimiento leaves it out altogether).
        bool PageShown(string id) { return settings.Profile != 0 || (id != "mascot" && id != "mascotset"); }

        FrameworkElement NavItem(int i)
        {
            NavRow r = new NavRow { Id = PageIds[i], Fill = new SolidColorBrush(Colors.Transparent) };
            string id = r.Id;
            TapBorder b = new TapBorder { Height = 32, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 0, 2), Cursor = Cursors.Hand, BorderThickness = HcPaint.Edge,
                                          Background = r.Fill, Label = PageNames[i], Role = Access.TabItem, On = delegate { return r.On; },
                                          Do = delegate { if (page != id) SetPage(id, true); else SearchHide(); } };
            Grid g = new Grid();
            // A small accent bar at the left edge of the section you are in.
            r.Bar = new Border { Width = 3, Height = 16, CornerRadius = new CornerRadius(1.5), Background = Ds.Brush(Ds.Brushes.Accent), HorizontalAlignment = HorizontalAlignment.Left,
                                 VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), Opacity = 0, IsHitTestVisible = false };
            g.Children.Add(r.Bar);
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            row.Children.Add(new IconTile(PageIcons[i], 20, W(PageColors[i]), W(Darker(PageColors[i]))));
            r.Name = Label(PageNames[i], Ds.Regular, 13, Ds.Brushes.Label, new Thickness(9, 0, 0, 0));
            row.Children.Add(r.Name);
            g.Children.Add(row);
            b.Child = g;
            r.Box = b;
            b.MouseEnter += delegate { r.Hot = true; PaintNav(r, true); HcPaint.Outline(b, !r.On); };
            b.MouseLeave += delegate { r.Hot = false; PaintNav(r, true); HcPaint.Outline(b, false); };
            b.MouseLeftButtonUp += delegate { if (page != id) SetPage(id, true); else SearchHide(); };
            navRows.Add(r);
            return b;
        }

        // The section you are in: a soft accent tint and the bar, faded in place. Going quickly through the sections no
        // longer sends a block of colour jumping up and down the list. Contrast themes keep the solid highlight.
        void UpdateNav(bool animate)
        {
            foreach (NavRow r in navRows)
            {
                bool on = page == r.Id || (r.Id == "mascot" && page == "mascotset");
                if (on == r.On && r.Shown) continue;
                r.On = on;
                r.Shown = true;
                PaintNav(r, animate);
            }
        }

        void PaintNav(NavRow r, bool animate)
        {
            Palette pal = Ds.Brushes;
            Color fill = r.On ? (pal.High ? pal.Accent : Ds.WithAlpha(pal.Accent, pal.Dark ? 0.26 : 0.14))
                              : r.Hot ? pal.Control : Colors.Transparent;
            r.Name.FontWeight = r.On ? Ds.Medium.Weight : Ds.Regular.Weight;
            r.Name.Foreground = Ds.Brush(r.On && pal.High ? pal.OnAccent : pal.Label);
            double bar = r.On && !pal.High ? 1 : 0;
            if (!animate || pal.High || !ToastQuiet.AnimationsOn())
            {
                r.Fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
                r.Fill.Color = fill;
                r.Bar.BeginAnimation(OpacityProperty, null);
                r.Bar.Opacity = bar;
                return;
            }
            // Short, gentle fades: quicker under the mouse, a little slower for the selection.
            TimeSpan t = TimeSpan.FromMilliseconds(r.Hot && !r.On ? 110 : 180);
            QuadraticEase ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            r.Fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(fill, t) { EasingFunction = ease });
            r.Bar.BeginAnimation(OpacityProperty, new DoubleAnimation(bar, t) { EasingFunction = ease });
        }

        void BuildPane()
        {
            Grid.SetColumn(pane, 1);
            pane.Background = Ds.Brush(Ds.Brushes.Window);
            Root.Children.Add(pane);

            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroller.Focusable = false;
            scroller.PanningMode = PanningMode.VerticalOnly;
            scroller.Resources = ScrollStyle();
            scroller.Content = pageHost;
            // The scrolled page is a layer of its own: ClearType is asked for again inside it (see Sheet.SharpText).
            RenderOptions.SetClearTypeHint(scroller, ClearTypeHint.Enabled);
            RenderOptions.SetClearTypeHint(pageHost, ClearTypeHint.Enabled);
            scroller.ScrollChanged += delegate { ToolbarShade(); };
            pane.Children.Add(scroller);

            // macOS toolbar: invisible at the top, the section name and a hairline fade in when the page scrolls under it.
            // It drags the window; while it is still clear, its lower edge belongs to the page below (the controls that
            // start right under it, like Home's view switch, take their clicks there).
            toolbar.Height = Bar;
            toolbar.VerticalAlignment = VerticalAlignment.Top;
            toolbar.Background = Brushes.Transparent;
            toolbar.MouseLeftButtonDown += DragFromBackground;
            toolbar.Through = ToolbarThrough;
            toolbarTitle.FontFamily = Ds.Text;
            toolbarTitle.FontWeight = FontWeights.SemiBold;
            toolbarTitle.FontSize = 13.5;
            toolbarTitle.VerticalAlignment = VerticalAlignment.Center;
            toolbarTitle.Margin = new Thickness(40, 0, 0, 0);
            toolbarTitle.Opacity = 0;
            toolbarRow.Orientation = Orientation.Horizontal;
            toolbar.Child = toolbarRow;
            pane.Children.Add(toolbar);
        }

        readonly StackPanel toolbarRow = new StackPanel();

        const double DragBand = 40;   // the toolbar's height that always drags; page content starts below it

        bool ToolbarThrough(Point p)
        {
            return p.Y >= DragBand && PaneOffset() <= 20;
        }

        // The toolbar's own surface, with a part the hit test falls through.
        sealed class ToolbarStrip : Border
        {
            public Func<Point, bool> Through;

            protected override HitTestResult HitTestCore(PointHitTestParameters p)
            {
                Func<Point, bool> t = Through;
                if (t != null && t(p.HitPoint)) return null;
                return base.HitTestCore(p);
            }
        }

        // Sub-pages get a back button in the toolbar, where nothing scrolls over it.
        void BuildToolbar()
        {
            toolbarRow.Children.Clear();
            if (page == "mascotset")
            {
                MacButton back = new MacButton(settings.MascotName, ButtonKind.Plain, "back", null, 28);
                back.Margin = new Thickness(30, 0, 0, 0);
                back.VerticalAlignment = VerticalAlignment.Center;
                back.Click += delegate { SetPage("mascot", true); };
                toolbarRow.Children.Add(back);
                toolbarTitle.Margin = new Thickness(8, 0, 0, 0);
            }
            else toolbarTitle.Margin = new Thickness(40, 0, 0, 0);
            toolbarRow.Children.Add(toolbarTitle);
        }

        void ToolbarShade()
        {
            double k = Math.Max(0, Math.Min(1, PaneOffset() / 40.0));
            Color bg = Ds.Brushes.Window;
            toolbar.Background = Ds.Brush(Color.FromArgb((byte)(k * (Ds.Brushes.High ? 255 : 245)), bg.R, bg.G, bg.B));
            toolbar.BorderBrush = Ds.Brush(Ds.WithAlpha(Ds.Brushes.Separator, k));
            toolbar.BorderThickness = new Thickness(0, 0, 0, 1);
            toolbarTitle.Opacity = Math.Max(0, Math.Min(1, (PaneOffset() - 30) / 30.0));
            SectionShade();
        }

        void DragFromBackground(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource != sender && e.OriginalSource != toolbarRow && e.OriginalSource != toolbarTitle) return;
            if (e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { }
        }

        static ResourceDictionary scrollStyle;

        // Thin overlay-style scrollbar instead of the classic one.
        internal static ResourceDictionary ScrollStyle()
        {
            if (scrollStyle != null) return scrollStyle;
            string xaml =
                "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Style TargetType='ScrollBar'><Setter Property='Width' Value='12'/><Setter Property='MinWidth' Value='12'/><Setter Property='Template'><Setter.Value>" +
                "<ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'>" +
                "<Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'>" +
                "<Border x:Name='b' CornerRadius='3' Width='6' Margin='0,4,3,4' HorizontalAlignment='Right' Background='#66808080'/>" +
                "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Width' Value='8'/>" +
                "<Setter TargetName='b' Property='Background' Value='#99808080'/></Trigger></ControlTemplate.Triggers>" +
                "</ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>" +
                "</ResourceDictionary>";
            scrollStyle = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
            return scrollStyle;
        }

        // ---- Pages

        void SetPage(string id, bool animate)
        {
            Stall.Mark("pesta\u00F1a " + id);
            if ((Array.IndexOf(PageIds, id) < 0 && id != "mascotset") || !PageShown(id)) id = "home";
            CancelListening();
            page = id;
            Build();
            scroller.ScrollToVerticalOffset(0);
            if (animate) quietUntil = Anim.Now + 600;
            if (animate)
            {
                // The new section fades and rises into place; the animations let go once they land.
                TranslateTransform tt = new TranslateTransform(0, 10);
                pageHost.RenderTransform = tt;
                HomeTween(pageHost, OpacityProperty, 0, 1, 0, 220, null);
                HomeTween(tt, TranslateTransform.YProperty, 10, 0, 0, 320, new CubicEase { EasingMode = EasingMode.EaseOut });
            }
            if (id == "home" && settings.MascotOn && animate)
            {
                mascot.PopIn();
                if (settings.MascotTalks && mascot.Bubble == null && Rng.Next(3) == 0) mascot.Say(Tip(), 4200);
            }
            Wake();
        }

        void Build()
        {
            ReadPolicy();
            pane.Background = Ds.Brush(Ds.Brushes.Window);
            toolbarTitle.Foreground = Ds.Brush(Ds.Brushes.Label);
            int pi = Array.IndexOf(PageIds, page == "mascotset" ? "mascot" : page);
            toolbarTitle.Text = page == "mascotset" ? I18n.T("Ajustes de la mascota", "Mascot settings") : PageNames[Math.Max(0, pi)];
            BuildToolbar();
            if (navRows.Count == 0) RefreshNav();
            else UpdateNav(IsVisible);
            pageHost.Children.Clear();
            bubbleAvoid.Clear();
            body = new StackPanel { Margin = new Thickness(40, 0, 40, 40), MaxWidth = 660, HorizontalAlignment = HorizontalAlignment.Stretch };
            pageHost.Children.Add(body);
            if (mascotHost != null) { mascotHost.Detach(); mascotHost = null; }
            try
            {
                switch (page)
                {
                    case "history": BuildHistory(); break;
                    case "keys": BuildKeys(); break;
                    case "general": BuildGeneral(); break;
                    case "editor": BuildEditor(); break;
                    case "record": BuildRecord(); break;
                    case "mascot": BuildMascot(); break;
                    case "mascotset": BuildMascotSettings(); break;
                    case "about": BuildAbout(); break;
                    default: BuildHome(); break;
                }
            }
            catch (Exception ex) { ShotStack.Log("P\u00E1gina " + page + ": " + ex); }
            PolicyRows();
            PageBuilt();
            BuildWidget();
            ToolbarShade();
            Wake();
        }

        // Rebuilds the current section keeping the scroll position (e.g. when update information arrives).
        void Rebuild()
        {
            if (IsDisposed) return;
            double off = scroller.VerticalOffset;
            Build();
            scroller.UpdateLayout();
            scroller.ScrollToVerticalOffset(off);
        }

        void Changed()
        {
            owner.ApplySettings();
            BuildWidget();
        }

        // Actions from Home hide the window first so it doesn't appear in the capture.
        void RunAction(string action)
        {
            if (action != "video" || !Recorder.Recording) Hide();
            owner.Run(action);
        }

        // ---- Sidebar widget: the mascot standing on a little stage with its name and level or, where the mascot is
        // already big on the page (or hidden), a capture card drawn as a selection marquee.

        bool MiniMascot { get { return settings.MascotOn && page != "home" && page != "mascot"; } }
        bool sideBuilt, sideWasMini;

        void BuildWidget()
        {
            sideLastPage = page;
            HomeWatchRecording();
            bool mini = MiniMascot;
            sideMeter = null;
            FrameworkElement card = mini ? SideMascot() : SideCapture();
            // Changing kind (to or from Home) eases the new card in and the mascot hops onto its plate; rebuilds of the
            // same kind (a setting, the status) swap silently.
            if (sideBuilt && mini != sideWasMini)
            {
                HomeTween(card, OpacityProperty, 0, 1, 0, 240, null);
                if (mini) mascot.PopIn();
            }
            sideBuilt = true;
            sideWasMini = mini;
            widgetSlot.Child = card;
        }

        // The plate both kinds sit on: translucent over the sidebar with a hairline, a touch brighter under the mouse.
        Border SideCard(bool hoverable)
        {
            Palette pal = Ds.Brushes;
            Color rest = pal.High ? pal.Window : pal.Dark ? Ds.Argb(0.06, 255, 255, 255) : Ds.Argb(0.55, 255, 255, 255);
            Color over = pal.High ? pal.Window : pal.Dark ? Ds.Argb(0.09, 255, 255, 255) : Ds.Argb(0.85, 255, 255, 255);
            SolidColorBrush bg = new SolidColorBrush(rest);
            Border card = new TapBorder { CornerRadius = new CornerRadius(14), Background = bg, BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1),
                                          Interactive = hoverable, Role = hoverable ? Access.Button : Access.Group };
            if (hoverable)
            {
                card.Cursor = Cursors.Hand;
                card.MouseEnter += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(over, TimeSpan.FromMilliseconds(140))); };
                card.MouseLeave += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(rest, TimeSpan.FromMilliseconds(220))); };
            }
            return card;
        }

        // The mascot on a pool of light under a halo of its colour; its name and level below (or the app status when there
        // is news) and a thin meter of friendship towards the next level. A click goes Home.
        FrameworkElement SideMascot()
        {
            Palette pal = Ds.Brushes;
            Border card = SideCard(true);
            Grid g = new Grid();
            // A look shown bigger here (Finn, Jake, BMO) that would reach over the plate makes it a little taller instead.
            int kind = mascot.Look.Kind;
            double extra = Math.Max(0, Math.Ceiling((0.44 - MascotParts.HeadTop(kind)) * 56 * MascotParts.PreviewZoom(kind) - (98 - 56 * 0.16 - 24)));
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(84 + extra) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Both glows fade to nothing inside their own box, so no edge ever shows.
            Color mc = W(MascotParts.Colors[Math.Max(0, Math.Min(MascotParts.Colors.GetLength(0) - 1, mascot.Look.Color)), 0]);
            g.Children.Add(new Border { Background = SideGlow(mc, pal.Dark ? 0.30 : 0.24, new Point(0.5, 0.46), 0.44, 0.54), IsHitTestVisible = false });
            System.Windows.Shapes.Ellipse pool = new System.Windows.Shapes.Ellipse
            {
                Width = 112, Height = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1), IsHitTestVisible = false, Fill = SideGlow(mc, pal.Dark ? 0.42 : 0.34, new Point(0.5, 0.5), 0.5, 0.5)
            };
            g.Children.Add(pool);

            MascotHost host = new MascotHost(mascot, 56, true);
            host.HorizontalAlignment = HorizontalAlignment.Center;
            host.VerticalAlignment = VerticalAlignment.Bottom;
            host.Margin = new Thickness(0, -30, 0, 7);
            host.Ceiling = 24 - extra;   // 3 px under the plate's top edge (the 98 high host overhangs the row by 21 - extra)
            g.Children.Add(host);
            UseMascot(host);

            StackPanel info = new StackPanel { Margin = new Thickness(14, 4, 14, 12) };
            Grid.SetRow(info, 1);
            TextBlock name = Label(settings.MascotName, Ds.Semibold, 13.5, pal.Label);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            info.Children.Add(name);
            sideStatus = new Border { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
            sideStatusMini = true;
            FillSideStatus();
            info.Children.Add(sideStatus);
            Grid meter = new Grid { Height = 3, Margin = new Thickness(30, 9, 30, 0) };
            meter.ColumnDefinitions.Add(new ColumnDefinition());
            meter.ColumnDefinitions.Add(new ColumnDefinition());
            Border track = new Border { CornerRadius = new CornerRadius(1.5), Background = Ds.Brush(pal.Control) };
            Grid.SetColumnSpan(track, 2);
            meter.Children.Add(track);
            meter.Children.Add(new Border { CornerRadius = new CornerRadius(1.5), Background = Ds.Brush(pal.Accent) });
            info.Children.Add(meter);
            sideMeter = meter;
            FillSideMeter();
            g.Children.Add(info);

            card.Child = g;
            card.MouseLeftButtonUp += delegate { SetPage("home", true); };
            AccessSideCard(card, delegate { SetPage("home", true); });
            return card;
        }

        Border sideStatus;            // the card's status line, refreshed in place (the mascot dozes off, wakes up)
        bool sideStatusMini, sideSleepShown;
        Grid sideMeter;

        void FillSideStatus()
        {
            if (sideStatus == null) return;
            D.Color dot;
            string status = Status(out dot);
            sideSleepShown = mascot.Sleeping;
            if (sideStatusMini && dot.ToArgb() == Mac.Green.ToArgb())
            {
                int lv = MascotParts.Level(Math.Max(0, settings.MascotLove));
                sideStatus.Child = Label(I18n.T("Nivel ", "Level ") + (lv + 1) + " \u00B7 " + MascotParts.LevelNames[lv], Ds.Regular, 11.5, Ds.Brushes.Label2);
            }
            else
            {
                FrameworkElement line = StatusLine(status, dot, 11.5);
                line.Margin = new Thickness(0);
                sideStatus.Child = line;
            }
        }

        // Friendship towards the next level; a capture moves it on without rebuilding the card.
        void FillSideMeter()
        {
            if (sideMeter == null) return;
            int love = Math.Max(0, settings.MascotLove), lv = MascotParts.Level(love), left;
            double prog = Math.Max(0, Math.Min(1, MascotParts.Progress(love, out left)));
            sideMeter.ColumnDefinitions[0].Width = new GridLength(Math.Max(0.0001, prog), GridUnitType.Star);
            sideMeter.ColumnDefinitions[1].Width = new GridLength(Math.Max(0.0001, 1 - prog), GridUnitType.Star);
            FrameworkElement info = sideMeter.Parent as FrameworkElement;
            if (info != null) info.ToolTip = left > 0 ? (I18n.IsSpanish ? (left + (left == 1 ? " captura" : " capturas") + " para el nivel " + (lv + 2)) : (left + (left == 1 ? " capture" : " captures") + " to level " + (lv + 2))) : I18n.T("Nivel m\u00E1ximo", "Maximum level");
        }

        // A soft radial glow of one colour that reaches zero at its radius.
        static RadialGradientBrush SideGlow(Color c, double a, Point centre, double rx, double ry)
        {
            if (Ds.Brushes.High) a = 0;   // no glows in a contrast theme
            RadialGradientBrush b = new RadialGradientBrush();
            b.Center = b.GradientOrigin = centre;
            b.RadiusX = rx;
            b.RadiusY = ry;
            b.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, a), 0));
            b.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, a * 0.62), 0.35));
            b.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, a * 0.22), 0.7));
            b.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, 0), 1));
            b.Freeze();
            return b;
        }

        // Capture card: a selection marquee that is itself the button, with the status and a record button below.
        FrameworkElement SideCapture()
        {
            Palette pal = Ds.Brushes;
            Border card = SideCard(false);
            card.Padding = new Thickness(10);
            StackPanel st = new StackPanel();

            Grid area = new TapGrid { Height = 66, Cursor = Cursors.Hand, Background = Brushes.Transparent, Label = I18n.T("Capturar un \u00E1rea", "Capture an area") };
            Color restTint = Ds.WithAlpha(pal.Accent, pal.High ? 0 : pal.Dark ? 0.13 : 0.07), overTint = Ds.WithAlpha(pal.Accent, pal.High ? 0 : pal.Dark ? 0.22 : 0.13);
            SolidColorBrush tint = new SolidColorBrush(restTint);
            System.Windows.Shapes.Rectangle marquee = new System.Windows.Shapes.Rectangle
            {
                Margin = new Thickness(3), RadiusX = 5, RadiusY = 5, Fill = tint, Stroke = Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.9 : 0.75)),
                StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 3.5, 3 }, IsHitTestVisible = false
            };
            area.Children.Add(marquee);
            HorizontalAlignment[] hs = { HorizontalAlignment.Left, HorizontalAlignment.Right };
            VerticalAlignment[] vs = { VerticalAlignment.Top, VerticalAlignment.Bottom };
            foreach (HorizontalAlignment ha in hs)
                foreach (VerticalAlignment va in vs)
                    area.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Background = Brushes.White, BorderBrush = Ds.Brush(pal.Accent),
                                                   BorderThickness = new Thickness(1.5), HorizontalAlignment = ha, VerticalAlignment = va, IsHitTestVisible = false });
            StackPanel txt = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            TextBlock title = Label(I18n.T("Capturar un \u00E1rea", "Capture an area"), Ds.Semibold, 13, pal.Label);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            txt.Children.Add(title);
            if (Hotkeys.Split(settings.HotRegion).Count > 0)
            {
                TextBlock k = Label(Hotkeys.Display(settings.HotRegion), Ds.Regular, 11.5, pal.Label2, new Thickness(0, 1, 0, 0));
                k.HorizontalAlignment = HorizontalAlignment.Center;
                txt.Children.Add(k);
            }
            area.Children.Add(txt);
            // Under the mouse the tint deepens and the ants march a few steps; they stop by themselves (a resting mouse
            // must not keep the window drawing), when it leaves or when the window hides. One step is one dash period.
            Action still = delegate { marquee.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null); };
            DoubleAnimation[] marching = new DoubleAnimation[1];
            area.MouseEnter += delegate
            {
                tint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(overTint, TimeSpan.FromMilliseconds(140)));
                DoubleAnimation march = new DoubleAnimation(0, -6.5, TimeSpan.FromMilliseconds(650)) { RepeatBehavior = new RepeatBehavior(4) };
                march.Completed += delegate { if (marching[0] == march) still(); };
                marching[0] = march;
                marquee.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, march);
            };
            area.MouseLeave += delegate
            {
                tint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(restTint, TimeSpan.FromMilliseconds(220)));
                still();
            };
            area.IsVisibleChanged += delegate { if (!area.IsVisible) still(); };
            area.ToolTip = I18n.T("Capturar un \u00E1rea de la pantalla", "Capture an area of the screen");
            ToolTipService.SetInitialShowDelay(area, 900);
            Press(area, delegate { still(); RunAction("region"); });
            st.Children.Add(area);

            Grid foot = new Grid { Margin = new Thickness(3, 9, 0, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sideStatus = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            sideStatusMini = false;
            FillSideStatus();
            foot.Children.Add(sideStatus);
            FrameworkElement rec = SideRecord();
            Grid.SetColumn(rec, 1);
            foot.Children.Add(rec);
            st.Children.Add(foot);
            card.Child = st;
            return card;
        }

        // Round record button; while recording it stops.
        FrameworkElement SideRecord()
        {
            Palette pal = Ds.Brushes;
            bool on = Recorder.Recording;
            Color rest = on ? Ds.WithAlpha(pal.Red, 0.16) : pal.Control, over = on ? Ds.WithAlpha(pal.Red, 0.26) : pal.ControlHover;
            SolidColorBrush bg = new SolidColorBrush(rest);
            Border b = new TapBorder { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = bg, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                                       BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = HcPaint.Edge };
            b.Child = new GlyphView(on ? "stop!" : "record!", on ? 13 : 15, pal.Red, 1.6);
            string combo = settings.HotVideo;
            b.ToolTip = (on ? I18n.T("Detener la grabaci\u00F3n", "Stop recording") : I18n.T("Grabar v\u00EDdeo", "Record video")) + (Hotkeys.Split(combo).Count > 0 ? "  (" + Hotkeys.Display(combo) + ")" : "");
            b.MouseEnter += delegate { bg.Color = over; };
            b.MouseLeave += delegate { bg.Color = rest; };
            Press(b, delegate { RunAction("video"); });
            return b;
        }

        // A status dot and its text; the text trims when the line is short of room.
        FrameworkElement StatusLine(string status, D.Color dot, double size)
        {
            DockPanel p = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            System.Windows.Shapes.Ellipse e = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Ds.Brush(Ds.Brushes.High ? Ds.Brushes.Label : W(dot)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 0) };
            DockPanel.SetDock(e, Dock.Left);
            p.Children.Add(e);
            TextBlock t = Label(status, Ds.Regular, size, Ds.Brushes.Label2);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            p.Children.Add(t);
            return p;
        }

        // Current app status (sidebar and hero).
        string Status(out D.Color dot)
        {
            if (Recorder.Recording) { dot = Mac.Red; return I18n.T("Grabando\u2026", "Recording\u2026"); }
            if (ScrollCapture.Active) { dot = Mac.Blue; return I18n.T("Capturando con desplazamiento", "Scrolling capture"); }
            if (Updater.Available != null) { dot = Mac.Blue; return I18n.Fmt("Versi\u00F3n {0} disponible", "Version {0} available", Updater.Available.Version.ToString(3)); }
            if (settings.MascotOn && mascot.Sleeping) { dot = Mac.Text3; return I18n.T("Echando una siesta", "Taking a nap"); }
            dot = Mac.Green;
            return I18n.T("Listo para capturar", "Ready to capture");
        }

        // ---- Mascot and the frame clock

        // The page (or the sidebar) hands over the slot where the mascot should live.
        void UseMascot(MascotHost host)
        {
            if (mascotHost != null && mascotHost != host) mascotHost.Detach();
            mascotHost = settings.MascotOn ? host : null;
            if (mascotHost != null)
            {
                host.Room = BubbleRoom;
                host.Avoid = bubbleAvoid;
                host.Poked += delegate
                {
                    if (page != "home" && page != "mascot") { SetPage("home", true); return; }
                    mascot.Poke(MascotTalk.Pokes(settings));
                    Wake();
                };
            }
            Wake();
        }

        // Where a speech bubble may go: the sidebar for the little one, otherwise the visible page under the toolbar.
        Rect BubbleRoom(MascotHost h)
        {
            if (sidebar.IsAncestorOf(h))
                return h.Settled(sidebar, new Rect(6, 6, Math.Max(0, sidebar.ActualWidth - 12), Math.Max(0, sidebar.ActualHeight - 12)));
            double w = scroller.ViewportWidth, ht = scroller.ViewportHeight;
            if (w < 40 || ht < Bar + 40) return Rect.Empty;
            return h.Settled(scroller, new Rect(6, Bar, w - 12, ht - Bar - 6));
        }

        double burstUntil;            // something just set the mascot moving: full rate for a moment, even if it naps

        void Wake()
        {
            burstUntil = Anim.Now + 1200;
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!timer.IsEnabled) { timer.Interval = MascotRate.Anim; timer.Start(); }
            else if (timer.Interval > MascotRate.Anim) timer.Interval = MascotRate.Anim;   // a slow clock (frozen, hidden) answers at once
        }

        void Tick(object sender, EventArgs e)
        {
            if (!IsVisible || WindowState == WindowState.Minimized) { timer.Stop(); return; }
            double now = Anim.Now;
            if (Unseen()) { timer.Interval = MascotRate.Hidden; lastStir = now; return; }
            // Nobody at the computer for long, the window not in front or its mascot asleep: nothing moves until someone is
            // back (Wake answers a click at once).
            bool resting = !active || (mascotHost != null && mascot.Sleeping && mascot.Bubble == null);
            if (intro == null && MascotRate.Frozen(Native.IdleMs(), resting)) { timer.Interval = MascotRate.Hidden; lastStir = now; return; }
            bool lively = false;
            if (intro != null)
            {
                lively = true;
                introLayer.Refresh();
                if (intro.T(now) > Intro.RevealAt + 120 && !introGreeted)
                {
                    introGreeted = true;
                    if (settings.MascotOn)
                    {
                        mascot.PopIn();
                        if (settings.MascotTalks) mascot.Greet(MascotTalk.Greeting() + (I18n.IsSpanish ? " Soy " : " I'm ") + settings.MascotName + (I18n.IsSpanish ? ". \u00BFQu\u00E9 capturamos?" : ". What to capture?"));
                    }
                }
                if (intro.Done(now)) EndIntro();
            }

            D.Point sm = WF.Control.MousePosition;
            bool moved = sm != lastScreenMouse;
            // Only a move that turns the mascot's eyes keeps the clock fast; one far away leaves the picture as it is.
            if (moved && (mascotHost == null || mascotHost.GazeMoves(lastScreenMouse, sm))) lastMove = now;
            lastScreenMouse = sm;
            // A slot that left the window (its card was swapped, the mascot was hidden from a page that stays) is let go,
            // so nothing keeps the clock running for it.
            if (mascotHost != null && (!settings.MascotOn || !mascotHost.Live)) { mascotHost.Detach(); mascotHost = null; }
            if (mascotHost != null)
            {
                ApplyMascotBehavior();
                mascotHost.Step(now, sm, moved);
                if (settings.MascotTalks && now > nextTip && intro == null)
                {
                    nextTip = now + (38000 + Rng.NextDouble() * 20000) * TipScale;
                    if (mascot.Bubble == null && !mascot.Sleeping) mascot.Say(Tip(), 5200);
                }
                // Lively already covers a bubble fading in or out; one at rest needs no more than the idle rate. Asleep, only
                // a pop-in or a reaction that starts while it naps runs at the full rate.
                if (mascot.Lively && (!mascot.Sleeping || now < burstUntil)) lively = true;
                // Dozing off and waking up are the only status changes nothing announces.
                if (sideStatus != null && mascot.Sleeping != sideSleepShown) FillSideStatus();
            }

            // 60 fps while something animates, 32 while the mouse moves (the mascot follows it) or for a while after, then
            // 16 (8 in the background once nothing has happened for 15 s) with the mascot just breathing; with nothing to
            // draw, the clock stops until something wakes it (MascotRate.Home). The mascot moves on real time, so a lower
            // rate is never slow motion.
            bool looking = mascotHost != null && now - lastMove < MascotRate.NearMs;
            if (lively || looking) lastStir = now;
            int ms = MascotRate.Home(true, false, mascotHost != null, lively, now < quietUntil, looking, mascotHost != null && mascot.Sleeping,
                                     active || intro != null, now - lastStir);
            if (ms == 0) { timer.Stop(); return; }
            timer.Interval = ms;
        }

        double lastStir;

        // On another virtual desktop (cloaked), hidden under another window, or with the screen off or locked nobody sees
        // it: the clock only checks, twice a second, whether it is back. MascotScreen reports the screen state with or
        // without the desktop pet.
        bool Unseen()
        {
            int cloaked;
            MascotScreen.Ensure();
            if (MascotRate.ScreenOff) MascotScreen.Check();
            if (MascotRate.ScreenOff) return true;
            if (Handle == IntPtr.Zero) return false;
            if (Native.DwmGetInt(Handle, 14, out cloaked, 4) == 0 && cloaked != 0) return true;   // DWMWA_CLOAKED
            if (active || intro != null) return covered = false;
            double now = Anim.Now;
            if (now - coveredAt > 300) { coveredAt = now; covered = Native.Covered(Handle); }
            return covered;
        }

        bool covered;
        double coveredAt = -1e9;

        void StartIntro()
        {
            EndIntro();
            intro = new Intro(Anim.Now);
            intro.TargetSize = 30;
            intro.Target = new D.PointF(20 + 15, 26 + 15);
            introLayer = new GdiLayer();
            introLayer.Painter = delegate(D.Graphics g, float k)
            {
                intro.TargetSize = 30 * k;
                intro.Target = new D.PointF((20 + 15) * k, (26 + 15) * k);
                intro.Paint(g, new D.Rectangle(0, 0, (int)(ActualWidth * k), (int)(ActualHeight * k)), Anim.Now, k);
            };
            introLayer.IsHitTestVisible = true;
            introLayer.MouseLeftButtonDown += delegate { SkipIntro(); };
            Grid.SetColumnSpan(introLayer, 2);
            Panel.SetZIndex(introLayer, 200);
            Root.Children.Add(introLayer);
            introGreeted = false;
        }

        void EndIntro()
        {
            if (intro == null) return;
            intro.Dispose();
            intro = null;
            if (introLayer != null) { introLayer.Release(); Root.Children.Remove(introLayer); introLayer = null; }
        }

        void SkipIntro()
        {
            if (intro == null) return;
            EndIntro();
            if (settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(MascotTalk.Greeting() + (I18n.IsSpanish ? " Soy " : " I'm ") + settings.MascotName + ".");
            }
            Wake();
        }

        string Tip() { return MascotTalk.Tip(settings); }

        void OnCaptured(string path)
        {
            if (!IsVisible || !settings.MascotOn) return;
            int level = MascotParts.Level(settings.MascotLove);
            bool up = level > MascotParts.Level(settings.MascotLove - 1);
            mascot.Celebrate(settings.MascotTalks ? (up ? MascotTalk.LevelUp(settings, level) : MascotTalk.Celebrate(settings)) : null);
            // Friendship and unlocks move on in place: no rebuild, so the dock keeps its scroll and the search its focus.
            if (page == "mascot") MascotPageOnCaptured();
            FillSideMeter();
            FillSideStatus();
            Wake();
        }

        void OnUpdaterChanged()
        {
            if (page == "about") Rebuild();
            else BuildWidget();
        }

        // ---- Keyboard

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (intro != null) { SkipIntro(); e.Handled = true; return; }
            if (listening != null)
            {
                e.Handled = true;
                if (IsModifier(key) || key == Key.Snapshot) return; // Print Screen arrives on key up
                listening.Take(this, ToKeys(key));
                return;
            }
            if (e.OriginalSource is TextBox) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            // Ctrl+W is the close button. Esc only tucks the window away: it never quits Stackshot, even when closing does.
            if (ctrl && key == Key.W) { HideToTray(); e.Handled = true; return; }
            if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && SearchEscape()) { e.Handled = true; return; }
            if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (settings.CloseToTray) HideToTray();
                e.Handled = true;
                return;
            }
            if (ctrl && key == Key.Tab)
            {
                int i = Array.IndexOf(PageIds, page == "mascotset" ? "mascot" : page), n = PageIds.Length;
                do i = (i + (shift ? -1 : 1) + n) % n; while (!PageShown(PageIds[i]));
                SetPage(PageIds[i], true);
                e.Handled = true;
                return;
            }
            // Back from a sub-page, as the toolbar's back button does.
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            if (page == "mascotset" && ((alt && key == Key.Left) || key == Key.BrowserBack))
            {
                SetPage("mascot", true);
                e.Handled = true;
            }
        }

        // The mouse's back button leaves a sub-page too.
        void OnMouseBack(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.XButton1 || page != "mascotset") return;
            SetPage("mascot", true);
            e.Handled = true;
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (listening != null && key == Key.Snapshot) { listening.Take(this, ToKeys(key)); e.Handled = true; }
        }

        static bool IsModifier(Key k)
        {
            return k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftShift || k == Key.RightShift || k == Key.LeftAlt || k == Key.RightAlt ||
                   k == Key.LWin || k == Key.RWin;
        }

        static WF.Keys ToKeys(Key k)
        {
            WF.Keys r = (WF.Keys)KeyInterop.VirtualKeyFromKey(k);
            ModifierKeys m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Control) != 0) r |= WF.Keys.Control;
            if ((m & ModifierKeys.Shift) != 0) r |= WF.Keys.Shift;
            if ((m & ModifierKeys.Alt) != 0) r |= WF.Keys.Alt;
            return r;
        }

        void CancelListening()
        {
            if (listening == null) return;
            listening.Stop(this);
        }

        // ---- Helpers shared by the pages

        static Color W(D.Color c) { return Color.FromArgb(c.A, c.R, c.G, c.B); }

        static D.Color Darker(D.Color c) { return D.Color.FromArgb(c.A, c.R * 3 / 4, c.G * 3 / 4, c.B * 3 / 4); }

        static TextBlock Label(string text, Typeface face, double size, Color color) { return Label(text, face, size, color, new Thickness(0)); }

        static TextBlock Label(string text, Typeface face, double size, Color color, Thickness margin)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontFamily = face.FontFamily;
            t.FontWeight = face.Weight;
            t.FontSize = size;
            t.Foreground = Ds.Brush(color);
            t.Margin = margin;
            if (size <= 22) TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        static TextBlock Paragraph(string text, double size, Color color)
        {
            TextBlock t = Label(text, Ds.Regular, size, color);
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = Math.Round(size * 1.38);
            return t;
        }
    }

    // A slot for the mascot: a GDI+ layer a bit larger than the mascot (hats, hearts and stars spill over) and a speech
    // bubble in real text. The bubble sits over the mascot's head; when there is no room there (the toolbar, the edge of
    // the page) or it would cover a control, it slides sideways, moves beside the mascot or, as a last resort, below it.
    // The mascot's clock. Only the full rate rides the render loop (one frame, one tick, no second timer competing with
    // it); 32 fps and below use a low-priority timer, so an idle window never keeps the renderer running at the monitor's
    // refresh rate.
    sealed class FrameClock
    {
        readonly DispatcherTimer slow = new DispatcherTimer(DispatcherPriority.Background);
        bool hooked, on;
        int interval = 16;
        TimeSpan last = TimeSpan.Zero;
        public event EventHandler Tick;

        public FrameClock() { slow.Tick += delegate { Fire(); }; }
        public bool IsEnabled { get { return on; } }

        public int Interval
        {
            get { return interval; }
            set
            {
                interval = value;
                if (on) Apply();
            }
        }

        public void Start() { on = true; Apply(); }

        public void Stop()
        {
            on = false;
            slow.Stop();
            Unhook();
        }

        void Apply()
        {
            if (interval < 20)
            {
                slow.Stop();
                if (!hooked) { hooked = true; last = TimeSpan.Zero; CompositionTarget.Rendering += Frame; }
            }
            else
            {
                Unhook();
                TimeSpan t = TimeSpan.FromMilliseconds(interval);
                if (slow.Interval != t) slow.Interval = t;
                if (!slow.IsEnabled) slow.Start();
            }
        }

        void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            CompositionTarget.Rendering -= Frame;
        }

        void Frame(object o, EventArgs e)
        {
            TimeSpan t = ((RenderingEventArgs)e).RenderingTime;
            if (t == last) return;
            // a frame early is tolerated
            if (last != TimeSpan.Zero && (t - last).TotalMilliseconds < interval - 4) return;
            last = t;
            Fire();
        }

        void Fire()
        {
            EventHandler h = Tick;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

    public class MascotHost : Grid
    {
        readonly Mascot mascot;
        readonly double box;
        readonly bool mini;
        public readonly GdiLayer Layer = new GdiLayer();
        readonly Border bubble = new Border();
        readonly TextBlock bubbleText = new TextBlock();
        readonly ScaleTransform bubblePop = new ScaleTransform(1, 1);
        bool attached = true, hot, placed;
        public event Action Poked;
        public Func<MascotHost, Rect> Room;     // where the bubble may go, in this host's coordinates (empty: anywhere)
        public List<FrameworkElement> Avoid;    // controls the bubble must not cover
        public double Ceiling;                  // how far down from its top a look shown bigger must stay (a plate's edge)

        public MascotHost(Mascot mascot, double box, bool mini)
        {
            this.mascot = mascot;
            this.box = box;
            this.mini = mini;
            Width = box * 1.7;
            Height = box * 1.75;
            ClipToBounds = false;
            Layer.Painter = Paint;
            Children.Add(Layer);
            bubble.CornerRadius = new CornerRadius(14);
            bubble.Padding = new Thickness(13, 8, 13, 9);
            bubble.HorizontalAlignment = HorizontalAlignment.Center;
            bubble.VerticalAlignment = VerticalAlignment.Bottom;
            bubble.MaxWidth = mini ? 200 : 230;
            bubble.IsHitTestVisible = false;
            bubble.Opacity = 0;
            bubble.Visibility = Visibility.Collapsed;
            bubble.RenderTransform = bubblePop;
            bubbleText.TextWrapping = TextWrapping.Wrap;
            bubbleText.FontSize = 13;
            TextOptions.SetTextFormattingMode(bubbleText, TextFormattingMode.Display);
            TextOptions.SetTextHintingMode(bubbleText, TextHintingMode.Fixed);
            bubble.UseLayoutRounding = true;
            bubble.SnapsToDevicePixels = true;
            bubbleText.FontFamily = Ds.Text;
            bubble.Child = bubbleText;
            System.Windows.Controls.Canvas c = new System.Windows.Controls.Canvas { ClipToBounds = false, IsHitTestVisible = false };
            c.Children.Add(bubble);
            Children.Add(c);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            MouseEnter += delegate { hot = true; mascot.Hover(true); };
            MouseLeave += delegate { hot = false; mascot.Hover(false); };
            MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; Action h = Poked; if (h != null) h(); };
            SizeChanged += delegate { Layer.Width = ActualWidth; Layer.Height = ActualHeight; };
            // The current pose is drawn as soon as the slot has a size, so a rebuilt page never shows a frame without it.
            Layer.SizeChanged += delegate { if (attached && PresentationSource.FromVisual(Layer) != null) Layer.Refresh(); };
        }

        // Still in a window and in charge of the mascot.
        public bool Live { get { return attached && PresentationSource.FromVisual(this) != null; } }

        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() { return new ActionPeer(this, delegate { Action h = Poked; if (h != null) h(); }, I18n.T("Mascota", "Mascot")); }

        // The bubble lives outside the slot. A slot pulled up with a negative margin (the sidebar card) would otherwise be
        // clipped to its own box by layout and the bubble would never show.
        protected override Geometry GetLayoutClip(Size layoutSlotSize)
        {
            return ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;
        }

        public void Detach()
        {
            attached = false;
            if (hot) { hot = false; mascot.Hover(false); }
            Layer.Release();
        }

        // The mascot box inside the layer, in device pixels. A look shown bigger grows up from where its shadow lies.
        D.RectangleF Box(float k)
        {
            float d = (float)(box * k), w = (float)(ActualWidth * k), h = (float)(ActualHeight * k), dz = d * (float)Zoom();
            return new D.RectangleF((w - dz) / 2, h - d * 0.16f - dz * 0.9f, dz, dz);
        }

        // Finn, Jake and BMO show bigger in the small views (not on the big stages), as far as the room over them allows.
        double Zoom()
        {
            int kind = mascot.Look.Kind;
            double z = box < 100 ? MascotParts.PreviewZoom(kind) : 1;
            if (z <= 1) return 1;
            double reach = 0.44 - MascotParts.HeadTop(kind), room = ActualHeight - box * 0.16 - Ceiling;
            return Math.Max(1, Math.Min(z, room / (reach * box)));
        }

        public void Step(double now, D.Point screenMouse, bool moved)
        {
            if (!attached || !IsVisible || PresentationSource.FromVisual(this) == null) return;
            float k = Layer.Scale;
            mascot.Box = Box(k);
            Point p = PointFromScreen(new Point(screenMouse.X, screenMouse.Y));
            mascot.Step(now, new D.PointF((float)(p.X * k), (float)(p.Y * k)), moved);
            Layer.Refresh();
            UpdateBubble();
        }

        public bool GazeMoves(D.Point from, D.Point to)
        {
            if (!attached || PresentationSource.FromVisual(this) == null) return true;
            float k = Layer.Scale;
            Point a = PointFromScreen(new Point(from.X, from.Y)), b = PointFromScreen(new Point(to.X, to.Y));
            return mascot.GazeMoves(new D.PointF((float)(a.X * k), (float)(a.Y * k)), new D.PointF((float)(b.X * k), (float)(b.Y * k)));
        }

        void Paint(D.Graphics g, float k)
        {
            mascot.Box = Box(k);
            mascot.Paint(g);
            // What it just drew is in the UI thread's part cache from now on (MascotWarm doesn't draw it again).
            if (mascot.Look != seenLook || mascot.Box.Width != seenSize)
            {
                seenLook = mascot.Look;
                seenSize = mascot.Box.Width;
                MascotWarm.Seen(seenLook, seenSize);
            }
        }

        MascotLook seenLook;
        float seenSize;

        // How wide this slot draws the mascot, in device pixels (a look prepared ahead for it is drawn at this size).
        public float PaintSize { get { return (float)(box * Layer.Scale) * (float)Zoom(); } }

        double shownAlpha = -1;
        Palette shownPalette;

        // Touches the visual tree only when the bubble actually changes (an idle mascot must not relayout anything).
        void UpdateBubble()
        {
            string text = mascot.Bubble;
            double a = Math.Max(0, Math.Min(1, mascot.BubbleAlpha));
            bool newText = text != null && bubbleText.Text != text;
            bool waiting = a > 0.01 && !placed;
            if (!newText && !waiting && Math.Abs(a - shownAlpha) < 0.004 && shownPalette == Ds.Brushes) return;
            shownAlpha = a;
            if (newText) { bubbleText.Text = text; placed = false; }
            Palette pal = Ds.Brushes;
            if (shownPalette != pal)
            {
                shownPalette = pal;
                bubble.Background = Ds.Brush(pal.High ? pal.Window : pal.Dark ? Ds.Rgb(58, 58, 62) : Colors.White);
                bubble.BorderBrush = Ds.Brush(pal.Hairline);
                bubble.BorderThickness = new Thickness(1);
                bubbleText.Foreground = Ds.Brush(pal.Label);
            }
            bubble.Opacity = (a > 0.97 ? 1 : a);
            // A slot built a moment ago (a new page, a view, a rebuild while it talks) may not have its spot yet: its
            // bubble waits a frame or two instead of being placed against a box that is about to move.
            bool show = a > 0.01 && (placed || LaidOut());
            if (a <= 0.01) placed = false;
            bubble.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            if (!placed) Place();
            double pop = a > 0.97 ? 1 : 0.92 + 0.08 * a; // settled: no transform, so the text is not resampled
            bubblePop.ScaleX = pop;
            bubblePop.ScaleY = pop;
        }

        int layoutWaits;

        // The slot and everything above it have been measured and arranged (a few frames at most after it is built).
        bool LaidOut()
        {
            if (ActualWidth < 1 || ActualHeight < 1) return false;
            for (DependencyObject d = this; d != null; d = VisualTreeHelper.GetParent(d))
            {
                UIElement u = d as UIElement;
                if (u != null && (!u.IsMeasureValid || !u.IsArrangeValid))
                {
                    if (++layoutWaits < 30) return false;
                    break;   // something keeps invalidating layout: place it anyway
                }
            }
            layoutWaits = 0;
            return true;
        }

        // Chooses the bubble's spot once per line, so it never jumps while it is read.
        void Place()
        {
            placed = true;
            bubble.Measure(new Size(bubble.MaxWidth, double.PositiveInfinity));
            Size s = bubble.DesiredSize;
            double w = ActualWidth, h = ActualHeight, cx = w / 2;
            double top = h - box - box * 0.06, z = Zoom();
            double head = Math.Min(top, h - box * 0.16 - (0.44 - MascotParts.HeadTop(mascot.Look.Kind)) * box * z) - (mascot.Look.EffectiveHat(DateTime.Now) != 0 ? box * 0.16 * z : 0);
            Rect room = Rect.Empty;
            Func<MascotHost, Rect> rf = Room;
            if (rf != null) { try { room = rf(this); } catch (InvalidOperationException) { room = Rect.Empty; } }
            List<Rect> keep = new List<Rect>();
            if (Avoid != null)
                foreach (FrameworkElement e in Avoid)
                {
                    if (e == null || !e.IsVisible) continue;
                    try
                    {
                        Rect r = Settled(e, new Rect(e.RenderSize));
                        r.Inflate(8, 8);
                        keep.Add(r);
                    }
                    catch (InvalidOperationException) { }
                }

            // Over the head; then slid a little sideways (still over the mascot); beside it; below it.
            double ax = cx - s.Width / 2, ay = head - s.Height - 4, reach = s.Width / 4;
            List<Rect> tries = new List<Rect>();
            tries.Add(new Rect(ax, ay, s.Width, s.Height));
            if (!room.IsEmpty) tries.Add(new Rect(Slide(Math.Max(room.Left, Math.Min(room.Right - s.Width, ax)), ax, reach), ay, s.Width, s.Height));
            foreach (Rect r in keep)
            {
                tries.Add(new Rect(Slide(r.Left - s.Width - 1, ax, reach), ay, s.Width, s.Height));
                tries.Add(new Rect(Slide(r.Right + 1, ax, reach), ay, s.Width, s.Height));
            }
            double sy = top + box * 0.4 - s.Height / 2;
            if (!room.IsEmpty) sy = Math.Max(room.Top, Math.Min(room.Bottom - s.Height, sy));
            Rect right = new Rect(cx + box * 0.42 + 6, sy, s.Width, s.Height), left = new Rect(cx - box * 0.42 - 6 - s.Width, sy, s.Width, s.Height);
            bool rightFirst = room.IsEmpty || room.Right - right.Left >= left.Right - room.Left;
            tries.Add(rightFirst ? right : left);
            tries.Add(rightFirst ? left : right);
            tries.Add(new Rect(ax, h + 4, s.Width, s.Height));

            Rect pick = Rect.Empty;
            foreach (Rect t in tries) if (Fits(t, room, keep)) { pick = t; break; }
            if (pick.IsEmpty)
            {
                // Nowhere is clear: over the head, kept inside the room as well as it goes.
                pick = tries[0];
                if (!room.IsEmpty) pick.X = Math.Max(room.Left, Math.Min(room.Right - s.Width, pick.X));
            }
            System.Windows.Controls.Canvas.SetLeft(bubble, Math.Round(pick.X));
            System.Windows.Controls.Canvas.SetTop(bubble, Math.Round(pick.Y));
            // It grows out of the side that faces the mascot.
            if (pick.Bottom <= top + 1) bubble.RenderTransformOrigin = new Point(Math.Max(0.1, Math.Min(0.9, (cx - pick.X) / s.Width)), 1);
            else if (pick.Top >= h) bubble.RenderTransformOrigin = new Point(0.5, 0);
            else bubble.RenderTransformOrigin = new Point(pick.X > cx ? 0 : 1, 0.5);
        }

        // r (in from's coordinates) in this slot's, as things will be once they land. Only layout offsets count: the spot is
        // chosen once per line, often while the page or the slot's tile still glides in or lifts under the mouse, and a
        // spot picked against those transforms would end up on the view switch or under the toolbar when they settle.
        public Rect Settled(Visual from, Rect r)
        {
            Visual a, b;
            Vector at = LayoutOffset(from, out a), me = LayoutOffset(this, out b);
            if (a != b) throw new InvalidOperationException("Not in the same window.");
            r.Offset(at - me);
            return r;
        }

        static Vector LayoutOffset(Visual v, out Visual root)
        {
            Vector o = new Vector();
            root = v;
            for (Visual d = v; d != null; d = VisualTreeHelper.GetParent(d) as Visual)
            {
                o += VisualTreeHelper.GetOffset(d);
                root = d;
            }
            return o;
        }

        static double Slide(double x, double from, double reach) { return Math.Max(from - reach, Math.Min(from + reach, x)); }

        static bool Fits(Rect t, Rect room, List<Rect> keep)
        {
            if (!room.IsEmpty && (t.Left < room.Left - 0.5 || t.Top < room.Top - 0.5 || t.Right > room.Right + 0.5 || t.Bottom > room.Bottom + 0.5)) return false;
            foreach (Rect k in keep) if (k.IntersectsWith(t)) return false;
            return true;
        }

        public bool Hot { get { return hot; } }
    }
}
