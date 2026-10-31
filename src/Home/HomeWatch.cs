// Stackshot - The watch's numbers on screen: a "Tu reloj hoy" tile on the widgets home and a row under Salud y actividad
// with today's steps, last night's sleep and the active minutes, and a button to read them now. Both follow each reading.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public partial class HomeWindow
    {
        static CultureInfo WatchEs { get { return CultureInfo.GetCultureInfo(I18n.IsSpanish ? "es-ES" : "en-US"); } }
        bool watchHooked;

        // Once per window: a reading refreshes what shows it.
        void WatchHook()
        {
            if (watchHooked) return;
            watchHooked = true;
            Action read = delegate
            {
                if (!IsVisible) return;
                if (page == "mascotset") SyncRows();
                else if (page == "home" && HomeSignature() != homeSig) HomeRefresh();
            };
            owner.WatchRead += read;
            Closed += delegate { owner.WatchRead -= read; };
        }

        // Where the numbers stand, in a sentence (null: they are on show and need no words).
        string WatchStatus(bool longer)
        {
            GoogleHealth.Today w = owner.Watch;
            if (owner.WatchReading) return I18n.T("Leyendo tu reloj\u2026", "Reading your watch\u2026");
            if (owner.WatchError != null && (w == null || w.Empty)) return I18n.T("No se ha podido leer: ", "Could not read: ") + owner.WatchError;
            if (w == null) return longer ? I18n.T("A\u00FAn no lo he le\u00EDdo hoy. Se lee solo cada media hora.", "Not read yet today. Syncs automatically every 30 minutes.") : I18n.T("A\u00FAn sin leer hoy.", "Not synced yet today.");
            if (w.Empty) return longer ? I18n.T("Google a\u00FAn no tiene datos de hoy. Abre la app de tu reloj en el m\u00F3vil para que se sincronice y vuelve a leerlo.", "Google has no data for today yet. Open your watch app on phone to sync, then try again.")
                                       : I18n.T("Google a\u00FAn no tiene datos de hoy. Abre la app del reloj en el m\u00F3vil para sincronizarlo.", "Google has no data for today yet. Open watch companion app on phone to sync.");
            return null;
        }

        // Steps (with the way to the goal), sleep and active minutes side by side.
        FrameworkElement WatchStats(GoogleHealth.Today w, double big)
        {
            Palette pal = Ds.Brushes;
            Grid g = new Grid();
            for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
            int goal = w.StepsGoal > 0 ? w.StepsGoal : 10000;
            FrameworkElement steps = WatchStat("walk", pal.Green, I18n.T("Pasos", "Steps"), w.Steps.ToString("N0", WatchEs), null, big, null);
            Grid bar = new Grid { Height = 4, Margin = new Thickness(0, 7, 14, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.High ? pal.Label3 : Ds.WithAlpha(pal.Green, 0.2)) });
            double done = Math.Max(0, Math.Min(1, w.Steps / (double)goal));
            Grid fillCols = new Grid();
            fillCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, done), GridUnitType.Star) });
            fillCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, 1 - done), GridUnitType.Star) });
            if (done > 0) fillCols.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Ds.Brush(pal.High ? pal.Label : pal.Green) });
            bar.Children.Add(fillCols);
            ((StackPanel)steps).Children.Add(bar);
            ((StackPanel)steps).Children.Add(Label(w.Steps >= goal ? I18n.T("\u00A1Objetivo cumplido!", "Goal reached!") : I18n.Fmt("de {0}", "of {0}", goal.ToString("N0", WatchEs)), Ds.Regular, 11, pal.Label3, new Thickness(0, 4, 0, 0)));
            g.Children.Add(steps);
            FrameworkElement sleep = w.SleepMinutes > 0
                ? WatchStat("moon", pal.Purple, I18n.T("Sue\u00F1o", "Sleep"), (w.SleepMinutes / 60).ToString(), "h " + (w.SleepMinutes % 60).ToString("00"), big, I18n.T("anoche", "last night"))
                : WatchStat("moon", pal.Purple, I18n.T("Sue\u00F1o", "Sleep"), "\u2014", null, big, I18n.T("sin datos", "no data"));
            Grid.SetColumn(sleep, 1);
            g.Children.Add(sleep);
            FrameworkElement active = WatchStat("heart", pal.Pink, I18n.T("Activo", "Active"), w.ActiveMinutes.ToString(), "min", big, I18n.T("moderado o intenso", "moderate or vigorous"));
            Grid.SetColumn(active, 2);
            g.Children.Add(active);
            return g;
        }

        // A small coloured glyph and name, the value in large type with its unit beside it, and a note under it.
        FrameworkElement WatchStat(string glyph, Color c, string name, string value, string unit, double big, string note)
        {
            Palette pal = Ds.Brushes;
            StackPanel s = new StackPanel();
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new GlyphView(glyph, 13, pal.High ? pal.Label : c, 1.7));
            head.Children.Add(Label(name, Ds.Regular, 11.5, pal.Label2, new Thickness(5, 0, 0, 0)));
            s.Children.Add(head);
            StackPanel v = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            TextBlock n = Label(value, Ds.Title, big, value == "\u2014" ? pal.Label3 : pal.Label);
            n.VerticalAlignment = VerticalAlignment.Bottom;
            v.Children.Add(n);
            if (unit != null)
            {
                TextBlock u = Label(unit, Ds.Medium, Math.Round(big * 0.55), pal.Label2, new Thickness(3, 0, 0, Math.Round(big * 0.16)));
                u.VerticalAlignment = VerticalAlignment.Bottom;
                v.Children.Add(u);
            }
            s.Children.Add(v);
            if (note != null)
            {
                TextBlock t = Label(note, Ds.Regular, 11, pal.Label3, new Thickness(0, 2, 0, 0));
                t.TextTrimming = TextTrimming.CharacterEllipsis;
                s.Children.Add(t);
            }
            return s;
        }

        // The widgets tile; a click opens Salud y actividad in the mascot's settings.
        void WatchTile(Grid tile)
        {
            Palette pal = Ds.Brushes;
            WatchHook();
            GoogleHealth.Today w = owner.Watch;
            StackPanel p = new StackPanel();
            Grid head = new Grid();
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new GlyphView("google", 14, pal.Label2, 1.6));
            left.Children.Add(Label(I18n.T("Tu reloj hoy", "Your watch today"), Ds.Semibold, 12, pal.Label, new Thickness(6, 0, 0, 0)));
            head.Children.Add(left);
            if (w != null && !owner.WatchReading)
            {
                TextBlock at = Label(w.ReadAt.ToString("HH:mm"), Ds.Regular, 12, pal.Label3);
                at.HorizontalAlignment = HorizontalAlignment.Right;
                at.ToolTip = I18n.Fmt("Le\u00EDdo a las {0}", "Read at {0}", w.ReadAt.ToString("H:mm"));
                head.Children.Add(at);
            }
            p.Children.Add(head);
            string status = WatchStatus(false);
            if (status == null)
            {
                FrameworkElement stats = WatchStats(w, 22);
                stats.Margin = new Thickness(0, 12, 0, 0);
                p.Children.Add(stats);
            }
            else
            {
                TextBlock t = Paragraph(status, 12.5, pal.Label2);
                t.Margin = new Thickness(0, 8, 0, 0);
                p.Children.Add(t);
            }
            tile.Children.Add(p);
            tile.Cursor = System.Windows.Input.Cursors.Hand;
            tile.ToolTip = I18n.T("Ver en Salud y actividad", "View in Health and activity");
            ToolTipService.SetInitialShowDelay(tile, 900);
            Press(tile, WatchOpenSettings);
        }

        void WatchOpenSettings()
        {
            SetPage("mascotset", true);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
            {
                int i = SectionTitles.IndexOf(I18n.T("Salud y actividad", "Health and activity"));
                if (i >= 0) SectionGo(i);
            });
        }

        // Why connecting the watch is worth it: what the mascot does with its numbers, in plain words.
        public class WatchBenefitsRow : Row
        {
            public WatchBenefitsRow() : base(I18n.T("Qu\u00E9 ganas conect\u00E1ndolo", "Benefits of connecting"),
                I18n.T("La mascota usa tus datos para cuidarte: te avisa si llevas una hora sin moverte y te propone levantarte, " +
                "te anima por la ma\u00F1ana y despu\u00E9s de comer si apenas has caminado, celebra tus paseos, tus minutos activos y tu objetivo de pasos, " +
                "te dice cu\u00E1nto te falta por la tarde, adelanta las pausas si has dormido poco (y por la noche te recuerda acostarte antes) " +
                "y no te pide moverte justo despu\u00E9s de caminar. Tambi\u00E9n suma tus pasos al resumen del d\u00EDa.",
                "The mascot uses your activity data to care for you: alerts after an hour of inactivity, encourages movement in morning/afternoon, " +
                "celebrates walks, active minutes and step goals, adapts breaks if you had poor sleep, and includes steps in daily summary."), "info", Mac.Blue, Mac.Teal) { }

            protected override FrameworkElement Control() { return null; }
        }

        // What is coming: more watches and bands, with a "Pronto" tag in the website's gradient.
        public class SoonRow : Row
        {
            public SoonRow() : base(I18n.T("M\u00E1s relojes y pulseras", "More smartwatches and fitness bands"),
                                    I18n.T("Pr\u00F3ximamente podr\u00E1s conectar otros relojes y pulseras, no solo Pixel Watch y Fitbit.",
                                           "Support for more smartwatches and fitness trackers coming soon, in addition to Pixel Watch and Fitbit."), "sparkle", Mac.Purple, Mac.Indigo) { }

            protected override FrameworkElement Control()
            {
                Palette pal = Ds.Brushes;
                Border tag = new Border
                {
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 10, 4),
                    Background = pal.High ? Ds.Brush(pal.Control) : (Brush)new LinearGradientBrush(Ds.Rgb(124, 92, 255), Ds.Rgb(8, 145, 204), 0)
                };
                tag.Child = Label(I18n.T("Pronto", "Soon"), Ds.Semibold, 11.5, pal.High ? pal.Label : Colors.White);
                return tag;
            }
        }

        // Under Salud y actividad: today's numbers, where they stand and a button to read them now.
        public class WatchRow : Row
        {
            MacButton now;
            Border stats;

            public WatchRow() : base(I18n.T("Hoy en tu reloj", "Today on your watch"), null, "walk", Mac.Green, Mac.Teal) { }

            protected override FrameworkElement Control()
            {
                now = new MacButton(I18n.T("Leer ahora", "Read now"), ButtonKind.Secondary);
                now.Click += delegate
                {
                    if (W.owner.WatchReading) return;
                    W.owner.ReadWatch(null);
                    W.SyncRows();
                };
                return now;
            }

            public override void Build()
            {
                W.WatchHook();
                SubNow = delegate
                {
                    string s = W.WatchStatus(true);
                    GoogleHealth.Today w = W.owner.Watch;
                    return s ?? I18n.Fmt("Le\u00EDdo a las {0}. Se lee solo cada media hora.", "Read at {0}. Automatically synced every 30 minutes.", w.ReadAt.ToString("H:mm"));
                };
                RowDefinitions.Clear();
                base.Build();
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                stats = new Border { Margin = new Thickness(40, 2, 0, 14) };
                Grid.SetRow(stats, 1);
                Grid.SetColumnSpan(stats, 3);
                Children.Add(stats);
                Fill();
            }

            public override void Sync(bool animate)
            {
                base.Sync(animate);
                Fill();
            }

            void Fill()
            {
                if (stats == null) return;
                if (now != null) now.IsEnabled = !W.owner.WatchReading;
                GoogleHealth.Today w = W.owner.Watch;
                bool show = w != null && !w.Empty;
                stats.Child = show ? W.WatchStats(w, 24) : null;
                stats.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
