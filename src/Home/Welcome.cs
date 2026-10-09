// Stackshot - Welcome window (first run of the downloaded .exe).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WF = System.Windows.Forms;

namespace Stackshot
{
    // The basics to get started, on Mica like a macOS onboarding sheet. Everything else lives in the main window.
    // Enter installs, Esc closes. When the full layout doesn't fit the screen (1080p at 125-150%, 768p laptops) it gets
    // compact and, if still too tall, the options scroll while the buttons stay in view.
    public class SetupWindow : Sheet
    {
        readonly Settings settings;
        bool compact;
        MacSwitch startup, printScreen, sound, copy;
        TextBlock folderLabel;
        Grid page;
        string folder;
        bool installed;
        // Choices kept across a rebuild when the theme changes.
        bool wantStartup = true, wantPrintScreen = true, wantSound, wantCopy;

        public bool StartWithWindows { get { return startup != null ? startup.IsOn : wantStartup; } }

        // Returns false if closed without installing.
        public static bool Welcome(Settings s, out bool startWithWindows)
        {
            SetupWindow w = new SetupWindow(s);
            w.ShowDialog();
            startWithWindows = w.StartWithWindows;
            return w.installed;
        }

        SetupWindow(Settings s)
        {
            settings = s;
            folder = s.SaveFolder;
            wantSound = s.Sound;
            wantCopy = s.CopyToClipboard;
            Title = "Stackshot";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
            Icon = AppIcon;
            // Controls handle their own presses; anything else that reaches the root moves the window.
            Root.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (e.ClickCount == 1) try { DragMove(); } catch { } };
            Fit(WorkHeight());
            KeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Handled || e.IsRepeat) return;
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter) { e.Handled = true; Save(); }
            };
        }

        // Never taller than the work area (CenterScreen uses the screen under the pointer). Compact only when the full
        // layout doesn't fit: measured, since a fresh Windows 11 adds the Print Screen row and Windows 10 has other fonts.
        void Fit(double room)
        {
            MaxHeight = Math.Max(300, room - 16);
            compact = false;
            Build();
            Root.Measure(new Size(Width, double.PositiveInfinity));
            if (Root.DesiredSize.Height <= MaxHeight) return;
            compact = true;
            Build();
        }

        static double WorkHeight()
        {
            try
            {
                WF.Screen scr = WF.Screen.FromPoint(WF.Control.MousePosition);
                return scr.WorkingArea.Height / Math.Max(1f, ShotStack.ScaleFor(scr));
            }
            catch { return SystemParameters.WorkArea.Height; }
        }

        protected override void ThemeChanged()
        {
            Remember();
            Build();
        }

        void Remember()
        {
            if (startup != null) wantStartup = startup.IsOn;
            if (printScreen != null) wantPrintScreen = printScreen.IsOn;
            if (sound != null) wantSound = sound.IsOn;
            if (copy != null) wantCopy = copy.IsOn;
        }

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);

            page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.Children.Add(page);
            KeepCaptionsOnTop();

            StackPanel col = new StackPanel { Margin = compact ? new Thickness(32, 22, 32, 0) : new Thickness(36, 34, 36, 0) };
            ScrollViewer scroll = new ScrollViewer
            {
                Content = col,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Background = Brushes.Transparent
            };
            page.Children.Add(scroll);

            // Logo with a soft halo; the shadow is pre-blurred (SoftShadow), not an effect re-run on every repaint.
            double logoSize = compact ? 72 : 96;
            Grid head = new Grid { Height = compact ? 80 : 112 };
            Border halo = new Border { Width = 300, Height = 200, Margin = new Thickness(0, -60, 0, -40), IsHitTestVisible = false };
            halo.Background = new RadialGradientBrush(Ds.Argb(pal.Dark ? 0.32 : 0.22, 110, 120, 255), Ds.Argb(0, 110, 120, 255));
            head.Children.Add(halo);
            double inset = logoSize * 6 / 256.0, body = logoSize - 2 * inset;
            head.Children.Add(new SoftShadow(body * 0.235, 12, logoSize / 16, pal.Dark ? 0.42 : 0.24)
            {
                Width = body, Height = body, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
            Image logo = new Image { Width = logoSize, Height = logoSize, Source = Icon as ImageSource, HorizontalAlignment = HorizontalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            head.Children.Add(logo);
            col.Children.Add(head);
            col.Children.Add(Text("Bienvenido a Stackshot", Ds.Title, compact ? 25 : 28, pal.Label, HorizontalAlignment.Center, new Thickness(0, compact ? 10 : 14, 0, 0)));
            string key = Hotkeys.Split(settings.HotRegion).Count > 0 ? "\nPulsa " + Hotkeys.Display(settings.HotRegion) + " y listo." : "";
            TextBlock sub = Text("Captura, marca y comparte en segundos." + key, Ds.Regular, 14, pal.Label2,
                                 HorizontalAlignment.Center, new Thickness(0, 6, 0, compact ? 18 : 24));
            sub.TextAlignment = TextAlignment.Center;
            col.Children.Add(sub);

            // Grouped card like macOS Settings: switches and the folder, separated by hairlines.
            StackPanel list = new StackPanel();
            startup = AddSwitch(list, "Iniciar con Windows", "Se abre solo, en segundo plano, al encender el equipo.", wantStartup);
            printScreen = Installer.SnippingOwnsPrintScreen
                ? AddSwitch(list, "Usar la tecla Impr Pant", "Windows la usa para Recortes; Stackshot se la queda solo en tu usuario.", wantPrintScreen)
                : null;
            sound = AddSwitch(list, "Sonido al capturar", "Un peque\u00F1o clic de c\u00E1mara.", wantSound);
            copy = AddSwitch(list, "Copiar cada captura", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla.", wantCopy);
            Grid frow = Row(list);
            StackPanel ft = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = TextPad };
            ft.Children.Add(Text("Carpeta de capturas", Ds.Regular, 13.5, pal.Label, HorizontalAlignment.Left, new Thickness(0)));
            folderLabel = Text("", Ds.Regular, 12, pal.Label2, HorizontalAlignment.Left, new Thickness(0, 2, 0, 0));
            folderLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            ft.Children.Add(folderLabel);
            frow.Children.Add(ft);
            MacButton change = new MacButton("Cambiar\u2026", ButtonKind.Secondary);
            change.Click += PickFolder;
            Grid.SetColumn(change, 1);
            change.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(change, "Cambiar la carpeta de capturas");
            frow.Children.Add(change);
            ShowFolder();
            col.Children.Add(Card(list));

            // The essentials as key caps, so there's nothing else to read.
            StackPanel keys = new StackPanel();
            string[] combos = { settings.HotRegion, settings.HotVideo, "Clic" };
            string[] what = { "Capturar un \u00E1rea o una ventana", "Grabar la pantalla", "en la miniatura para editarla" };
            for (int i = 0; i < combos.Length; i++)
            {
                Grid r = Row(keys);
                r.MinHeight = compact ? 40 : 44;
                // Wide enough for the usual shortcuts; a longer one widens its row instead of running into the text.
                r.ColumnDefinitions[0].Width = GridLength.Auto;
                r.ColumnDefinitions[0].MinWidth = 190;
                r.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                bool none = i < 2 && Hotkeys.Split(combos[i]).Count == 0;
                FrameworkElement k = none ? (FrameworkElement)Text("Sin atajo", Ds.Regular, 13, pal.Label3, HorizontalAlignment.Left, new Thickness(0))
                                          : new Keycaps(i < 2 ? Hotkeys.Display(combos[i]) : combos[i], 20);
                k.Margin = new Thickness(0, 0, 12, 0);
                k.HorizontalAlignment = HorizontalAlignment.Left;
                k.VerticalAlignment = VerticalAlignment.Center;
                r.Children.Add(k);
                TextBlock t = Text(what[i], Ds.Regular, 13, pal.Label2, HorizontalAlignment.Left, new Thickness(0, 6, 0, 6));
                t.TextWrapping = TextWrapping.Wrap;
                Grid.SetColumn(t, 1);
                r.Children.Add(t);
            }
            Border kc = Card(keys);
            kc.Margin = new Thickness(0, compact ? 12 : 16, 0, compact ? 14 : 22);
            col.Children.Add(kc);

            // Footer stays in view; a hairline marks it only while the options above are cut off.
            Grid foot = new Grid();
            Grid.SetRow(foot, 1);
            Border rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Background = Ds.Brush(pal.Separator), Visibility = Visibility.Collapsed };
            foot.Children.Add(rule);
            scroll.ScrollChanged += delegate
            {
                bool cut = scroll.ScrollableHeight > 0.5 && scroll.VerticalOffset < scroll.ScrollableHeight - 0.5;
                rule.Visibility = cut ? Visibility.Visible : Visibility.Collapsed;
            };
            Grid buttons = new Grid { Margin = compact ? new Thickness(32, 12, 32, 20) : new Thickness(36, 4, 36, 30) };
            MacButton cancel = new MacButton("Ahora no", ButtonKind.Secondary, null, null, 40);
            cancel.HorizontalAlignment = HorizontalAlignment.Left;
            cancel.Click += delegate { Close(); };
            MacButton ok = new MacButton("Instalar y empezar", ButtonKind.Primary, null, null, 40);
            ok.HorizontalAlignment = HorizontalAlignment.Right;
            ok.Click += Save;
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            foot.Children.Add(buttons);
            page.Children.Add(foot);
        }

        static TextBlock Text(string s, Typeface face, double size, Color c, HorizontalAlignment ha, Thickness m)
        {
            TextBlock t = new TextBlock { Text = s, FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Foreground = Ds.Brush(c), HorizontalAlignment = ha, Margin = m };
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        static Border Card(StackPanel list)
        {
            Palette pal = Ds.Brushes;
            return new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Ds.Brush(pal.Dark ? Ds.Argb(0.06, 255, 255, 255) : Ds.Argb(0.75, 255, 255, 255)),
                BorderBrush = Ds.Brush(pal.Hairline),
                BorderThickness = new Thickness(1),
                Child = list
            };
        }

        // A row (title area + control column) with a hairline above it when it is not the first.
        Grid Row(StackPanel list)
        {
            if (list.Children.Count > 0) list.Children.Add(new Border { Height = 1, Background = Ds.Brush(Ds.Brushes.Separator), Margin = new Thickness(16, 0, 0, 0) });
            Grid g = new Grid { MinHeight = compact ? 48 : 58, Margin = new Thickness(16, 0, 16, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            list.Children.Add(g);
            return g;
        }

        Thickness TextPad { get { return compact ? new Thickness(0, 7, 14, 7) : new Thickness(0, 9, 14, 9); } }

        MacSwitch AddSwitch(StackPanel list, string title, string desc, bool value)
        {
            Palette pal = Ds.Brushes;
            Grid g = Row(list);
            StackPanel t = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = TextPad };
            t.Children.Add(Text(title, Ds.Regular, 13.5, pal.Label, HorizontalAlignment.Left, new Thickness(0)));
            TextBlock d = Text(desc, Ds.Regular, 12, pal.Label2, HorizontalAlignment.Left, new Thickness(0, 2, 0, 0));
            d.TextWrapping = TextWrapping.Wrap;
            t.Children.Add(d);
            g.Children.Add(t);
            MacSwitch sw = new MacSwitch(value);
            sw.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(sw, title);
            Grid.SetColumn(sw, 1);
            g.Children.Add(sw);
            return sw;
        }

        void ShowFolder()
        {
            // ~ only for the user folder itself or what is inside it (not C:\Users\ana2 when the user is ana).
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            bool inside = home.Length > 0 && folder.StartsWith(home, StringComparison.OrdinalIgnoreCase) &&
                          (folder.Length == home.Length || folder[home.Length] == '\\');
            folderLabel.Text = inside ? "~" + folder.Substring(home.Length) : folder;
            folderLabel.ToolTip = folder;
        }

        void PickFolder()
        {
            using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog())
            {
                d.Description = "\u00BFD\u00F3nde quieres guardar las capturas que conserves?";
                d.ShowNewFolderButton = true;
                try { Directory.CreateDirectory(folder); d.SelectedPath = folder; } catch { }
                if (d.ShowDialog(Win32) == WF.DialogResult.OK) { folder = d.SelectedPath; ShowFolder(); }
            }
        }

        void Save()
        {
            Remember();
            settings.SaveFolder = folder;
            settings.Sound = wantSound;
            settings.CopyToClipboard = wantCopy;
            settings.FirstRunDone = true;
            settings.Save();
            if (printScreen != null && wantPrintScreen) Installer.FreePrintScreen();
            installed = true;
            Close();
        }
    }
}
