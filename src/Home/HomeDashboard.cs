// Stackshot - Home and About sections.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using D = System.Drawing;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // ---- Home

        // Rounded card surface with a hairline border.
        static Border CardFace(double radius)
        {
            Palette pal = Ds.Brushes;
            return new Border
            {
                CornerRadius = new CornerRadius(radius),
                Background = new SolidColorBrush(pal.Group),
                BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.07, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };
        }

        // Pre-blurred shadow behind a card (see SoftShadow); its Opacity scales it, up to `boost` times the resting one.
        static SoftShadow CardShadow(double radius, double opacity, double blur, double depth, double boost)
        {
            Palette pal = Ds.Brushes;
            SoftShadow s = new SoftShadow(radius, blur, depth, Math.Min(1, (pal.Dark ? opacity * 2.5 : opacity) * boost));
            s.Opacity = 1 / boost;
            s.Margin = new Thickness(1);
            return s;
        }

        static void Glide(IAnimatable target, DependencyProperty p, double to, double ms)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            target.BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
        }

        // ---- About

        static string shownUpdate;      // what the update card showed last, to animate a change of state
        static Action updateProgress;   // moves the card's bar during a download (no page rebuild)
        static bool progressHooked;

        void BuildAbout()
        {
            Palette pal = Ds.Brushes;
            StackPanel head = new StackPanel { Margin = new Thickness(0, 64, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };

            Grid logo = new Grid { Width = 96, Height = 96, HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = false };
            Color brand = W(Mac.Brand2);
            RadialGradientBrush glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(brand, pal.Dark ? 0.34 : 0.24), 0));
            glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(brand, pal.Dark ? 0.12 : 0.08), 0.5));
            glow.GradientStops.Add(new GradientStop(Ds.WithAlpha(brand, 0), 1));
            logo.Children.Add(new System.Windows.Shapes.Ellipse { Width = 260, Height = 260, Margin = new Thickness(-82), Fill = glow, IsHitTestVisible = false });
            Image img = new Image { Width = 96, Height = 96, Source = Icon as ImageSource };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            logo.Children.Add(img);
            head.Children.Add(logo);

            TextBlock name = Label("Stackshot", Ds.Title, 30, pal.Label, new Thickness(0, 20, 0, 0));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            head.Children.Add(name);
            TextBlock ver = Label("Versi\u00F3n " + Installer.MyVersion.ToString(3) + "  \u00B7  Libre y gratuito (licencia MIT)", Ds.Regular, 12.5, pal.Label2, new Thickness(0, 4, 0, 0));
            ver.HorizontalAlignment = HorizontalAlignment.Center;
            head.Children.Add(ver);
            TextBlock tag = Paragraph("Captura, marca y comparte en segundos. Miniaturas flotantes, editor r\u00E1pido, v\u00EDdeo y GIF.", 13, pal.Label2);
            tag.TextAlignment = TextAlignment.Center;
            tag.MaxWidth = 360;
            tag.Margin = new Thickness(0, 12, 0, 0);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            head.Children.Add(tag);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
            MacButton gh = new MacButton("Ver en GitHub", ButtonKind.Primary, null, null, 32);
            gh.Click += delegate { try { Process.Start(Program.RepoUrl); } catch { } };
            MacButton data = new MacButton("Carpeta de datos", ButtonKind.Secondary, null, null, 32);
            data.Margin = new Thickness(10, 0, 0, 0);
            data.Click += delegate { try { Process.Start(Native.Explorer, "\"" + Settings.DataDir + "\""); } catch { } };
            buttons.Children.Add(gh);
            buttons.Children.Add(data);
            head.Children.Add(buttons);

            // Quiet links, as in a macOS About window.
            StackPanel links = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };
            links.Children.Add(AboutLink("Novedades", Program.RepoUrl + "/releases"));
            links.Children.Add(Label("\u00B7", Ds.Regular, 12, pal.Label3, new Thickness(8, 0, 8, 0)));
            links.Children.Add(AboutLink("Informar de un problema", Program.RepoUrl + "/issues/new?body=" +
                Uri.EscapeDataString("\n\n---\nStackshot " + Installer.MyVersion.ToString(3) + " \u00B7 Windows " + Environment.OSVersion.Version)));
            head.Children.Add(links);
            Add(head);

            Add(UpdateCard());

            if (Installer.RunningInstalled)
            {
                TextBlock un = Label("Desinstalar Stackshot\u2026", Ds.Regular, 12.5, pal.Red, new Thickness(0, 22, 0, 0));
                un.HorizontalAlignment = HorizontalAlignment.Center;
                un.Cursor = Cursors.Hand;
                un.MouseEnter += delegate { un.TextDecorations = TextDecorations.Underline; };
                un.MouseLeave += delegate { un.TextDecorations = null; };
                un.MouseLeftButtonUp += delegate { try { Process.Start(Settings.InstalledExe, "--uninstall"); } catch { } };
                Add(un);
            }
        }

        // Updates: where things stand, what's new and one button for the next step; below, automatic installation.
        FrameworkElement UpdateCard()
        {
            Palette pal = Ds.Brushes;
            Updater.Release r = Updater.Available;
            Updater.Step step = Updater.Doing;
            DateTime last = Updater.LastChecked(settings);
            string mine = Installer.MyVersion.ToString(3), v = r != null ? r.Version.ToString(3) : mine;
            bool working = r != null && (step == Updater.Step.Downloading || step == Updater.Step.Verifying || step == Updater.Step.Installing);
            Color green1 = W(Mac.Green), green2 = Ds.Rgb(0, 160, 90), gray1 = Ds.Rgb(142, 142, 147), gray2 = Ds.Rgb(99, 99, 104);

            string key, icon, title, sub, label = null, note = null;
            Color c1, c2;
            ButtonKind kind = ButtonKind.Secondary;
            Action act = Updater.Check;
            bool wait = false, link = false;
            if (working)
            {
                key = "work";
                icon = "save"; c1 = W(Mac.Blue); c2 = W(Mac.Indigo);
                title = (step == Updater.Step.Downloading ? "Descargando" : step == Updater.Step.Verifying ? "Comprobando" : "Instalando") + " Stackshot " + v;
                sub = step == Updater.Step.Downloading ? DownloadText()
                    : step == Updater.Step.Verifying ? "Se comprueban su suma SHA-256 y su firma antes de instalar nada."
                    : Updater.Held ? "En cuanto termine de guardarse la grabaci\u00F3n, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse."
                    : "Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse en unos segundos.";
            }
            else if (r != null && Updater.IsReady)
            {
                key = "ready";
                icon = "save"; c1 = green1; c2 = green2;
                title = "Stackshot " + v + " lista para instalar";
                sub = "Ya est\u00E1 descargada y verificada. " + (settings.AutoUpdate && Updater.CanInstall
                      ? "Se instalar\u00E1 sola en cuanto no est\u00E9s usando el equipo."
                      : "Al instalarla, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse en unos segundos.");
                label = "Instalar ahora"; kind = ButtonKind.Primary; act = Updater.Install;
                wait = step == Updater.Step.Checking;
            }
            else if (r != null)
            {
                key = "available";
                icon = "sparkle"; c1 = W(Mac.Blue); c2 = W(Mac.Purple);
                title = "Stackshot " + v + " disponible";
                label = "Ver la versi\u00F3n";
                if (Updater.CanInstall)
                {
                    sub = "\u00BFQuieres descargarla? Se comprueba su firma y se instala en unos segundos; Stackshot se vuelve a abrir solo.";
                    label = "Descargar e instalar";
                    link = true;
                }
                else if (Installer.ManagedByMsi) sub = "En este equipo las actualizaciones las instala inform\u00E1tica: p\u00EDdeles el nuevo Stackshot.msi.";
                else if (!Installer.RunningInstalled) sub = "\u00BFQuieres descargarla? Esta copia de Stackshot no est\u00E1 instalada: desc\u00E1rgala desde GitHub.";
                else sub = "\u00BFQuieres descargarla? Esta versi\u00F3n no trae firma de actualizaci\u00F3n: desc\u00E1rgala desde GitHub.";
                kind = ButtonKind.Primary;
                act = Updater.Install; // opens the release page when it can't be installed from here
                if (Updater.Problem != null && Updater.ProblemInstalling)
                {
                    key = "available-error";
                    note = Updater.Problem;
                    if (Updater.CanInstall) label = "Reintentar";
                }
                wait = step == Updater.Step.Checking;
            }
            else if (step == Updater.Step.Checking)
            {
                key = "checking";
                icon = "update"; c1 = gray1; c2 = gray2;
                title = "Buscando actualizaciones\u2026";
                sub = "Preguntando a GitHub por la \u00FAltima versi\u00F3n de Stackshot.";
                label = "Buscando\u2026"; wait = true;
            }
            else if (Updater.Problem != null)
            {
                key = "error";
                icon = "warning"; c1 = W(Mac.Orange); c2 = Ds.Rgb(232, 110, 20);
                title = "No se pudo comprobar";
                sub = Updater.Problem + (last > DateTime.MinValue ? " \u00DAltima comprobaci\u00F3n: " + When(last) + "." : "");
                label = "Reintentar";
            }
            else if (last > DateTime.MinValue)
            {
                key = "uptodate";
                icon = "check"; c1 = green1; c2 = green2;
                title = "Est\u00E1s al d\u00EDa";
                // Whether it looks on its own is the switch right below.
                sub = "Stackshot " + mine + " es la versi\u00F3n m\u00E1s reciente. Comprobado " + When(last) + ".";
                label = "Buscar ahora";
            }
            else
            {
                key = "never";
                icon = "update"; c1 = gray1; c2 = gray2;
                title = "Actualizaciones";
                sub = "Tienes la versi\u00F3n " + mine + ". A\u00FAn no se ha comprobado si hay otra m\u00E1s nueva.";
                label = "Buscar ahora";
            }

            Grid shell = new Grid { Margin = new Thickness(0, 36, 0, 0) };
            shell.Children.Add(CardShadow(14, 0.06, 10, 1, 1));
            Border card = CardFace(14);
            if (r != null) card.BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.6));
            shell.Children.Add(card);
            StackPanel rows = new StackPanel();
            card.Child = rows;

            Grid g = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            IconTile tile = new IconTile(icon, 34, c1, c2);
            tile.VerticalAlignment = VerticalAlignment.Top;
            tile.Margin = new Thickness(0, 1, 14, 0);
            g.Children.Add(tile);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            Grid.SetColumn(text, 1);
            TextBlock t = Label(title, Ds.Semibold, 14, pal.Label);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(t);
            TextBlock s = Label(sub, Ds.Regular, 12, pal.Label2, new Thickness(0, 2, 0, 0));
            s.TextWrapping = TextWrapping.Wrap;
            s.LineHeight = 17;
            text.Children.Add(s);
            updateProgress = null;
            if (working)
            {
                // A thin bar; during the download it follows the progress in place.
                Grid track = new Grid { Height = 4, Margin = new Thickness(0, 10, 0, 2) };
                ColumnDefinition doneCol = new ColumnDefinition(), restCol = new ColumnDefinition();
                track.ColumnDefinitions.Add(doneCol);
                track.ColumnDefinitions.Add(restCol);
                Border back = new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.08, 0, 0, 0)) };
                Grid.SetColumnSpan(back, 2);
                track.Children.Add(back);
                track.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.Accent) });
                Action show = delegate
                {
                    double f = step == Updater.Step.Downloading ? Math.Max(0.03, Math.Min(1, Updater.Fraction)) : 1;
                    doneCol.Width = new GridLength(f, GridUnitType.Star);
                    restCol.Width = new GridLength(1 - f, GridUnitType.Star);
                    if (step == Updater.Step.Downloading) s.Text = DownloadText();
                };
                show();
                text.Children.Add(track);
                if (step == Updater.Step.Downloading) updateProgress = show;
                if (!progressHooked)
                {
                    progressHooked = true;
                    Updater.Progress += delegate { Action a = updateProgress; if (a != null) a(); };
                }
            }
            if (note != null)
            {
                Grid np = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                np.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                np.ColumnDefinitions.Add(new ColumnDefinition());
                GlyphView gv = new GlyphView("warning", 14, pal.Orange, 1.5);
                gv.VerticalAlignment = VerticalAlignment.Top;
                gv.Margin = new Thickness(0, 1, 6, 0);
                np.Children.Add(gv);
                TextBlock nt = Paragraph(note, 12, pal.Orange);
                Grid.SetColumn(nt, 1);
                np.Children.Add(nt);
                text.Children.Add(np);
            }
            if (r != null && !working && r.Notes.Count > 0)
            {
                StackPanel notes = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                foreach (string n in r.Notes)
                {
                    TextBlock nt = Paragraph("\u2022  " + n, 12, pal.Label2);
                    nt.Margin = new Thickness(0, 0, 0, 3);
                    notes.Children.Add(nt);
                }
                text.Children.Add(notes);
            }
            if (link)
            {
                StackPanel lk = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                                                 Cursor = Cursors.Hand, Background = Brushes.Transparent };
                TextBlock lt = Label("Novedades en GitHub", Ds.Medium, 12, pal.Accent);
                lk.Children.Add(lt);
                GlyphView arrow = new GlyphView("open", 12, pal.Accent, 1.4);
                arrow.Margin = new Thickness(4, 1, 0, 0);
                lk.Children.Add(arrow);
                lk.MouseEnter += delegate { lt.TextDecorations = TextDecorations.Underline; };
                lk.MouseLeave += delegate { lt.TextDecorations = null; };
                lk.MouseLeftButtonUp += delegate { Updater.OpenPage(); };
                text.Children.Add(lk);
            }
            g.Children.Add(text);

            if (label != null)
            {
                MacButton btn = new MacButton(label, wait ? ButtonKind.Secondary : kind, null, null, 30);
                btn.VerticalAlignment = VerticalAlignment.Top;
                btn.Margin = new Thickness(0, 2, 0, 0);
                if (wait) btn.Opacity = 0.55;
                Grid.SetColumn(btn, 2);
                btn.Click += delegate { if (!Updater.Busy) act(); };
                g.Children.Add(btn);
            }
            rows.Children.Add(g);

            // Looking on its own, and installing on its own (which needs the looking; an MSI install is updated by IT and
            // a copy that isn't installed can't replace itself).
            bool msi = Installer.ManagedByMsi, installed = Installer.RunningInstalled;
            FrameworkElement autoRow = null;
            FrameworkElement checkRow = UpdateSwitch(rows, "Buscar actualizaciones autom\u00E1ticamente", settings.CheckUpdates, delegate(bool on)
            {
                settings.CheckUpdates = on;
                owner.ApplySettings();
                Updater.SettingsChanged();
                if (autoRow != null) { UpdateSwitchLive(autoRow, on && installed, true); autoRow.ToolTip = AutoTip(installed, on); }
            });
            checkRow.ToolTip = "Al abrir Stackshot y cada 3 horas. No env\u00EDa nada tuyo.";
            ToolTipService.SetInitialShowDelay(checkRow, 700);
            if (!msi)
            {
                autoRow = UpdateSwitch(rows, "Instalarlas autom\u00E1ticamente", settings.AutoUpdate, delegate(bool on)
                {
                    settings.AutoUpdate = on;
                    owner.ApplySettings();
                    Updater.SettingsChanged();
                    // A downloaded version says whether it will install on its own: refresh once the switch has slid.
                    if (key == "ready") Later(300, delegate { if (page == "about") Rebuild(); });
                });
                autoRow.ToolTip = AutoTip(installed, settings.CheckUpdates);
                ToolTipService.SetInitialShowDelay(autoRow, 700);
                ToolTipService.SetShowOnDisabled(autoRow, true);
                UpdateSwitchLive(autoRow, settings.CheckUpdates && installed, false);
            }

            // A new state arrives with a small pop of the icon and the text fading in.
            bool changed = shownUpdate != null && shownUpdate != key;
            shownUpdate = key;
            if (changed)
            {
                ScaleTransform pop = new ScaleTransform(0.8, 0.8, 17, 17);
                tile.RenderTransform = pop;
                DoubleAnimation grow = new DoubleAnimation(1, TimeSpan.FromMilliseconds(340));
                grow.EasingFunction = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
                pop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                pop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                text.Opacity = 0;
                Glide(text, UIElement.OpacityProperty, 1, 260);
            }
            return shell;
        }

        // A footer line of the update card: a hairline, the text and a switch. Returns the line (to dim it).
        static FrameworkElement UpdateSwitch(StackPanel rows, string text, bool value, Action<bool> toggled)
        {
            Palette pal = Ds.Brushes;
            rows.Children.Add(new Border { Height = 1, Background = Ds.Brush(pal.Separator), Margin = new Thickness(66, 0, 0, 0) });
            Grid foot = new Grid { Margin = new Thickness(66, 9, 18, 10), Background = Brushes.Transparent };
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock ft = Label(text, Ds.Regular, 12.5, pal.Label2);
            ft.TextTrimming = TextTrimming.CharacterEllipsis;
            foot.Children.Add(ft);
            MacSwitch sw = new MacSwitch(value);
            sw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sw, 1);
            sw.Toggled += delegate(bool on) { toggled(on); };
            foot.Children.Add(sw);
            rows.Children.Add(foot);
            return foot;
        }

        // What installing on its own does or, while it can't, why.
        static string AutoTip(bool installed, bool checking)
        {
            return !installed ? "Solo con Stackshot instalado; esta copia no puede instalarse sola."
                 : !checking ? "Necesita \u00ABBuscar actualizaciones autom\u00E1ticamente\u00BB."
                 : "Se descargan, se comprueba su firma y se instalan cuando no est\u00E9s usando el equipo.";
        }

        static void UpdateSwitchLive(FrameworkElement line, bool live, bool animate)
        {
            line.IsEnabled = live;
            if (animate) Glide(line, UIElement.OpacityProperty, live ? 1 : 0.42, 220);
            else line.Opacity = live ? 1 : 0.42;
        }

        static FrameworkElement AboutLink(string text, string url)
        {
            TextBlock t = Label(text, Ds.Regular, 12, Ds.Brushes.Accent);
            t.Cursor = Cursors.Hand;
            t.MouseEnter += delegate { t.TextDecorations = TextDecorations.Underline; };
            t.MouseLeave += delegate { t.TextDecorations = null; };
            t.MouseLeftButtonUp += delegate { try { Process.Start(url); } catch (Exception ex) { ShotStack.Log("Enlace: " + ex.Message); } };
            return t;
        }

        // Runs once after a short delay (no timer left running).
        static void Later(int ms, Action act)
        {
            System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += delegate { t.Stop(); act(); };
            t.Start();
        }

        static string DownloadText()
        {
            return Updater.Fraction < 0 ? "Conectando con GitHub\u2026"
                 : (int)Math.Round(Updater.Fraction * 100) + " %  \u00B7  despu\u00E9s se comprueban su suma SHA-256 y su firma";
        }

        // "ahora mismo", "hace 5 min", "hoy a las 9:30", "ayer a las 18:05", "el 3/10 a las 9:30".
        static string When(DateTime utc)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            DateTime t = utc.ToLocalTime(), now = DateTime.Now;
            double min = (now - t).TotalMinutes;
            if (min < 1) return "ahora mismo";
            if (min < 60) return "hace " + (int)min + " min";
            if (t.Date == now.Date) return "hoy a las " + t.ToString("H:mm", ci);
            if (t.Date == now.Date.AddDays(-1)) return "ayer a las " + t.ToString("H:mm", ci);
            return "el " + t.ToString("d/M", ci) + " a las " + t.ToString("H:mm", ci);
        }
    }
}
