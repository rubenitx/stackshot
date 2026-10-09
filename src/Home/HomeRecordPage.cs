// Stackshot - Recording section as widgets: quality cards, sound sources with their devices, camera, GIF and engine.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // Shown in quality order; the stored values keep 1 = Maxima from earlier versions.
        static readonly int[] RecOrder = { Recorder.Standard, Recorder.High, Recorder.Maximum, Recorder.Cinema };
        static readonly string[] RecNames = { "Est\u00E1ndar", "Alta", "M\u00E1xima", "Cine" };
        static readonly string[] RecLines = { "Ligera y fluida, para chats y correos.", "60 fps n\u00EDtidos, listo al momento.", "60 fps sin p\u00E9rdida al grabar.",
                                              "Lo mejor: el doble de resoluci\u00F3n, 60 fps." };
        static readonly string[] RecIcons = { "share", "video", "sparkle", "film" };
        static readonly Color[,] RecTiles =
        {
            { Ds.Rgb(52, 199, 89), Ds.Rgb(0, 160, 120) },
            { Ds.Rgb(64, 156, 255), Ds.Rgb(10, 110, 230) },
            { Ds.Rgb(191, 90, 242), Ds.Rgb(120, 70, 220) },
            { Ds.Rgb(255, 159, 10), Ds.Rgb(255, 69, 58) }
        };
        static readonly string[] RecMetrics = { "Nitidez", "Fluidez", "Tama\u00F1o" };
        static readonly int[,] RecLevels = { { 1, 2, 1 }, { 2, 3, 2 }, { 3, 4, 3 }, { 4, 4, 4 } };
        static readonly string[] RecNotes =
        {
            "Est\u00E1ndar: 24, 30 o 60 fps con buena compresi\u00F3n. Archivos peque\u00F1os que se env\u00EDan en segundos.",
            "Alta: 60 fps y mucho detalle, codificado mientras grabas, as\u00ED que se guarda al instante. Archivos m\u00E1s grandes.",
            "M\u00E1xima: 60 fps al ritmo exacto de tu monitor, se graba sin ninguna p\u00E9rdida y al terminar se codifica con calidad muy alta. Tarda un poco en guardar.",
            "Cine: lo m\u00E1ximo que da de s\u00ED la pantalla. Se graba sin ninguna p\u00E9rdida a 60 fps al ritmo exacto del monitor y se guarda al doble de " +
            "resoluci\u00F3n (hasta 4K) conservando el color exacto de cada p\u00EDxel: texto, c\u00F3digo y l\u00EDneas finas se ven perfectos en cualquier pantalla " +
            "y aguantan la recompresi\u00F3n de YouTube, Teams o LinkedIn. Sonido a 320 kbps. Usa m\u00E1s CPU y disco y tarda en guardar (en pantalla completa, unas dos veces lo que dura el v\u00EDdeo)."
        };
        static readonly int[] RecFps = { 24, 30, 60 }, RecGifFps = { 10, 15, 20, 25 };

        static List<string> recCameras;       // DirectShow cameras, looked up once per session (it runs FFmpeg)
        static bool recSearching;
        RecCard[] recCards;
        TextBlock recNote;
        FrameworkElement recFps;
        Border recCamPill;
        TextBlock recCamText;
        bool recCamMenuWanted;

        void BuildRecord()
        {
            RecHeader();
            RecQuality();
            RecSound();
            RecCamera();
            RecGifAndEngine();
            RecAfter();
        }

        // ---- Layout

        void RecHeader()
        {
            Palette pal = Ds.Brushes;
            Grid head = new Grid { Margin = new Thickness(0, 56, 0, 20) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel titles = new StackPanel();
            titles.Children.Add(Label("Grabaci\u00F3n", Ds.Title, 26, pal.Label));
            TextBlock sub = Paragraph("V\u00EDdeo en MP4 con sonido y GIF animado. Para parar, pulsa el atajo otra vez o el bot\u00F3n de la barrita roja.", 13, pal.Label2);
            sub.Margin = new Thickness(0, 5, 0, 0);
            titles.Children.Add(sub);
            head.Children.Add(titles);
            Grid keys = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 0, 2) };
            keys.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keys.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r = 0;
            foreach (string action in new string[] { "video", "gif" })
            {
                string combo = settings.HotkeysFor(action);
                if (Hotkeys.Split(combo).Count == 0) continue;
                keys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                TextBlock name = Label(action == "video" ? "V\u00EDdeo" : "GIF", Ds.Regular, 11.5, pal.Label2, new Thickness(0, r > 0 ? 6 : 0, 10, 0));
                name.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetRow(name, r);
                keys.Children.Add(name);
                Keycaps k = new Keycaps(Hotkeys.Display(combo), 18);
                k.HorizontalAlignment = HorizontalAlignment.Right;
                k.Margin = new Thickness(0, r > 0 ? 6 : 0, 0, 0);
                Grid.SetRow(k, r);
                Grid.SetColumn(k, 1);
                keys.Children.Add(k);
                r++;
            }
            Grid.SetColumn(keys, 1);
            head.Children.Add(keys);
            Add(head);
        }

        // A row of equal widget columns.
        Grid RecRow(int columns)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < columns; i++)
            {
                if (i > 0) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
            }
            Add(g);
            return g;
        }

        // A widget with a tinted glyph, a title and an optional line under it; returns its content column.
        StackPanel RecPanel(Grid row, int col, string icon, Color tint, string title, string sub, out TextBlock subText)
        {
            Palette pal = Ds.Brushes;
            Grid inside = Widget(row, col, 0, 1, 1, null);
            inside.Margin = new Thickness(18, 16, 18, 18);
            StackPanel p = new StackPanel();
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new GlyphView(icon, 16, tint, 1.7) { VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(Label(title, Ds.Semibold, 13.5, pal.Label, new Thickness(8, 0, 0, 0)));
            p.Children.Add(head);
            subText = null;
            if (sub != null)
            {
                subText = Paragraph(sub, 12, pal.Label2);
                subText.Margin = new Thickness(0, 5, 0, 0);
                p.Children.Add(subText);
            }
            inside.Children.Add(p);
            return p;
        }

        // Neutral fill of the tiles inside a widget.
        static Color RecTile(bool hot)
        {
            return Ds.Brushes.Dark ? Ds.Argb(hot ? 0.09 : 0.05, 255, 255, 255) : Ds.Argb(hot ? 0.06 : 0.035, 0, 0, 0);
        }

        static void RecTo(Animatable o, DependencyProperty p, double to, double ms, bool spring)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            if (spring) a.EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };
            else a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            o.BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
        }

        static void RecFade(UIElement e, double to, double ms)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.BeginAnimation(UIElement.OpacityProperty, a, HandoffBehavior.SnapshotAndReplace);
        }

        static void RecColor(SolidColorBrush b, Color to, double ms, double delay)
        {
            ColorAnimation a = new ColorAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.BeginTime = TimeSpan.FromMilliseconds(delay);
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            b.BeginAnimation(SolidColorBrush.ColorProperty, a, HandoffBehavior.SnapshotAndReplace);
        }

        // Fades and lifts in an element whose content just changed.
        static void RecIn(FrameworkElement e)
        {
            TranslateTransform t = new TranslateTransform(0, 5);
            e.RenderTransform = t;
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        // A pop-up button for a device: icon, name and up/down chevrons; the name is returned to update it in place.
        Border RecPill(string icon, out TextBlock text)
        {
            Palette pal = Ds.Brushes;
            Border b = new Border { Height = 32, CornerRadius = new CornerRadius(9), Cursor = Cursors.Hand, BorderThickness = new Thickness(1), BorderBrush = Ds.Brush(pal.Hairline) };
            Color rest = pal.Dark ? Ds.Argb(0.06, 255, 255, 255) : Colors.White, over = pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Rgb(247, 247, 249);
            SolidColorBrush bg = new SolidColorBrush(rest);
            b.Background = bg;
            Grid g = new Grid { Margin = new Thickness(10, 0, 10, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new GlyphView(icon, 15, pal.Label2, 1.6) { VerticalAlignment = VerticalAlignment.Center });
            text = Label("", Ds.Regular, 12.5, pal.Label, new Thickness(8, 0, 8, 0));
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            StackPanel chev = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            chev.Children.Add(new GlyphView("up", 10, pal.Label2, 1.8) { Margin = new Thickness(0, 0, 0, -3) });
            chev.Children.Add(new GlyphView("down", 10, pal.Label2, 1.8));
            Grid.SetColumn(chev, 2);
            g.Children.Add(chev);
            b.Child = g;
            b.MouseEnter += delegate { RecColor(bg, over, 120, 0); };
            b.MouseLeave += delegate { RecColor(bg, rest, 200, 0); };
            return b;
        }

        // Sets a pill's text (orange when something is wrong) and its tooltip, for names too long to fit.
        static void RecSetText(TextBlock t, string s, bool warn)
        {
            t.Text = s;
            t.Foreground = Ds.Brush(warn ? Ds.Brushes.Orange : Ds.Brushes.Label);
            FrameworkElement grid = t.Parent as FrameworkElement;
            if (grid != null && grid.Parent is FrameworkElement) ((FrameworkElement)grid.Parent).ToolTip = s;
        }

        // ---- Quality

        void RecQuality()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(1);
            TextBlock sub;
            StackPanel p = RecPanel(row, 0, "sparkle", pal.Purple, "Calidad", "Cu\u00E1nto detalle y fluidez guarda cada v\u00EDdeo, y lo que pesa.", out sub);
            int q = Math.Max(0, Array.IndexOf(RecOrder, settings.VideoQuality));
            Grid cards = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            recCards = new RecCard[RecOrder.Length];
            for (int i = 0; i < RecOrder.Length; i++)
            {
                if (i > 0) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                cards.ColumnDefinitions.Add(new ColumnDefinition());
                int k = i;
                RecCard c = new RecCard(RecIcons[i], RecTiles[i, 0], RecTiles[i, 1], RecNames[i], RecLines[i], RecMetrics, RecLevelsFor(i), i == q);
                Grid.SetColumn(c.Root, i * 2);
                cards.Children.Add(c.Root);
                Press(c.Root, delegate { RecPickQuality(k); });
                recCards[i] = c;
            }
            p.Children.Add(cards);

            // The chosen quality in full, with the frame rate next to it when it can be chosen.
            Border detail = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 13), Margin = new Thickness(0, 12, 0, 0), Background = Ds.Brush(RecTile(false)) };
            Grid dg = new Grid();
            dg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dg.ColumnDefinitions.Add(new ColumnDefinition());
            dg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dg.Children.Add(new GlyphView("info", 15, pal.Label2, 1.6) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0) });
            recNote = Paragraph("", 12.5, pal.Label2);
            recNote.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(recNote, 1);
            dg.Children.Add(recNote);
            StackPanel fps = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0), ToolTip = "M\u00E1s fotogramas, m\u00E1s fluido (y m\u00E1s pesado)." };
            TextBlock fl = Label("Fotogramas por segundo", Ds.Regular, 11.5, pal.Label2);
            fl.HorizontalAlignment = HorizontalAlignment.Center;
            fps.Children.Add(fl);
            MacSegmented fs = new MacSegmented(new string[] { "24", "30", "60" }, Nearest(RecFps, settings.VideoFps));
            fs.Margin = new Thickness(0, 6, 0, 0);
            fs.HorizontalAlignment = HorizontalAlignment.Center;
            fs.Changed += delegate(int i)
            {
                settings.VideoFps = RecFps[i];
                Changed();
                if (recCards != null) recCards[0].SetLevels(RecLevelsFor(0), true);
            };
            fps.Children.Add(fs);
            Grid.SetColumn(fps, 2);
            dg.Children.Add(fps);
            recFps = fps;
            detail.Child = dg;
            p.Children.Add(detail);
            RecShowNote(q, false);
        }

        // Nitidez, Fluidez and Tamano from 1 to 4; Standard follows its frame rate.
        int[] RecLevelsFor(int i)
        {
            int[] l = { RecLevels[i, 0], RecLevels[i, 1], RecLevels[i, 2] };
            if (i == 0)
            {
                int f = settings.VideoFps;
                l[1] = f >= 50 ? 3 : f >= 30 ? 2 : 1;
                l[2] = f >= 50 ? 2 : 1;
            }
            return l;
        }

        void RecPickQuality(int i)
        {
            int before = Math.Max(0, Array.IndexOf(RecOrder, settings.VideoQuality));
            if (RecOrder[i] != settings.VideoQuality)
            {
                settings.VideoQuality = RecOrder[i];
                Changed();
            }
            for (int j = 0; j < recCards.Length; j++) recCards[j].Select(j == i, true);
            if (before != i) RecShowNote(i, true);
        }

        void RecShowNote(int q, bool animate)
        {
            string s = RecNotes[q];
            int c = s.IndexOf(':');
            recNote.Inlines.Clear();
            recNote.Inlines.Add(new System.Windows.Documents.Run(s.Substring(0, c + 1)) { FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(Ds.Brushes.Label) });
            recNote.Inlines.Add(new System.Windows.Documents.Run(s.Substring(c + 1)));
            recFps.Visibility = q == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!animate) return;
            RecIn(recNote);
            if (q == 0) RecIn(recFps);
        }

        // A quality to pick: icon, name, one line and three small bar meters. The chosen one gets the accent ring, a
        // tint and a check, and its bars light up one after another.
        sealed class RecCard
        {
            public readonly Grid Root = new Grid();
            readonly SolidColorBrush fill;
            readonly Border ring;
            readonly ScaleTransform badge = new ScaleTransform(0, 0);
            readonly SolidColorBrush[,] segs;
            int[] levels;
            bool selected, hot;

            public RecCard(string icon, Color c1, Color c2, string name, string line, string[] metrics, int[] levels, bool selected)
            {
                Palette pal = Ds.Brushes;
                this.levels = levels;
                this.selected = selected;
                Root.Cursor = Cursors.Hand;
                Root.Background = Brushes.Transparent;
                fill = new SolidColorBrush(Fill());
                Root.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = fill });
                StackPanel col = new StackPanel { Margin = new Thickness(13, 13, 13, 14) };
                col.Children.Add(new IconTile(icon, 30, c1, c2) { HorizontalAlignment = HorizontalAlignment.Left });
                col.Children.Add(Label(name, Ds.Semibold, 14, pal.Label, new Thickness(0, 11, 0, 0)));
                TextBlock l = Paragraph(line, 11.5, pal.Label2);
                l.Margin = new Thickness(0, 2, 0, 0);
                l.MinHeight = 2 * l.LineHeight;
                col.Children.Add(l);
                Grid bars = new Grid { Margin = new Thickness(0, 12, 0, 0), Background = Brushes.Transparent,
                                       ToolTip = "Nitidez y fluidez: cuantas m\u00E1s barras, mejor. Tama\u00F1o: lo que ocupa el archivo." };
                bars.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                bars.ColumnDefinitions.Add(new ColumnDefinition());
                segs = new SolidColorBrush[metrics.Length, 4];
                for (int m = 0; m < metrics.Length; m++)
                {
                    bars.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    TextBlock t = Label(metrics[m], Ds.Regular, 10.5, pal.Label2, new Thickness(0, m > 0 ? 3 : 0, 8, 0));
                    Grid.SetRow(t, m);
                    bars.Children.Add(t);
                    UniformGrid u = new UniformGrid { Rows = 1, Columns = 4, Height = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, m > 0 ? 3 : 0, 0, 0) };
                    for (int s = 0; s < 4; s++)
                    {
                        segs[m, s] = new SolidColorBrush(Seg(m, s));
                        u.Children.Add(new Border { CornerRadius = new CornerRadius(2.5), Background = segs[m, s], Margin = new Thickness(s > 0 ? 1.5 : 0, 0, s < 3 ? 1.5 : 0, 0) });
                    }
                    Grid.SetRow(u, m);
                    Grid.SetColumn(u, 1);
                    bars.Children.Add(u);
                }
                col.Children.Add(bars);
                Root.Children.Add(col);
                ring = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2), BorderBrush = Ds.Brush(pal.Accent), IsHitTestVisible = false, Opacity = selected ? 1 : 0 };
                Root.Children.Add(ring);
                Grid check = new Grid { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 11, 11, 0),
                                        IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = badge };
                check.Children.Add(new System.Windows.Shapes.Ellipse { Fill = Ds.Brush(pal.Accent) });
                check.Children.Add(new GlyphView("check", 13, Colors.White, 2.2) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                Root.Children.Add(check);
                badge.ScaleX = badge.ScaleY = selected ? 1 : 0;
                Root.MouseEnter += delegate { hot = true; RecColor(fill, Fill(), 120, 0); };
                Root.MouseLeave += delegate { hot = false; RecColor(fill, Fill(), 220, 0); };
            }

            Color Fill()
            {
                Palette pal = Ds.Brushes;
                if (selected) return Ds.WithAlpha(pal.Accent, (pal.Dark ? 0.16 : 0.08) + (hot ? 0.04 : 0));
                return RecTile(hot);
            }

            Color Seg(int m, int s)
            {
                Palette pal = Ds.Brushes;
                bool on = s < levels[m];
                if (selected) return on ? pal.Accent : Ds.WithAlpha(pal.Accent, pal.Dark ? 0.24 : 0.18);
                if (pal.Dark) return on ? Ds.Argb(0.5, 255, 255, 255) : Ds.Argb(0.1, 255, 255, 255);
                return on ? Ds.Argb(0.38, 0, 0, 0) : Ds.Argb(0.08, 0, 0, 0);
            }

            public void Select(bool on, bool animate)
            {
                if (on == selected) return;
                selected = on;
                double k = animate ? 1 : 0;
                RecColor(fill, Fill(), 200 * k, 0);
                RecFade(ring, on ? 1 : 0, (on ? 180 : 140) * k);
                RecTo(badge, ScaleTransform.ScaleXProperty, on ? 1 : 0, (on ? 320 : 140) * k, on);
                RecTo(badge, ScaleTransform.ScaleYProperty, on ? 1 : 0, (on ? 320 : 140) * k, on);
                Paint(animate);
            }

            public void SetLevels(int[] l, bool animate)
            {
                levels = l;
                Paint(animate);
            }

            // When selected the bars fill in from left to right.
            void Paint(bool animate)
            {
                for (int m = 0; m < segs.GetLength(0); m++)
                    for (int s = 0; s < 4; s++)
                        RecColor(segs[m, s], Seg(m, s), animate ? 180 : 0, animate && selected ? 60 + (m * 4 + s) * 22 : 0);
            }
        }

        // ---- Sound

        void RecSound()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(1);
            TextBlock sub;
            StackPanel p = RecPanel(row, 0, "sound", pal.Orange, "Sonido", "Se graba lo que actives, cada uno desde el dispositivo que elijas.", out sub);
            Grid tiles = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement a = RecSource(true), b = RecSource(false);
            Grid.SetColumn(b, 2);
            tiles.Children.Add(a);
            tiles.Children.Add(b);
            p.Children.Add(tiles);
            TextBlock n = Paragraph("Mientras grabas, la barrita muestra el nivel de cada sonido y un clic en el micr\u00F3fono lo silencia. El sonido va sincronizado con la imagen " +
                                    "y, aunque suenen los dos a la vez, nunca se satura.", 12, pal.Label2);
            n.Margin = new Thickness(2, 12, 2, 0);
            p.Children.Add(n);
        }

        // One source: its switch, what it records and the device it records from.
        FrameworkElement RecSource(bool computer)
        {
            Palette pal = Ds.Brushes;
            bool on = computer ? settings.RecordSystemAudio : settings.RecordMic;
            Color tint = computer ? pal.Orange : pal.Pink;
            // On: a soft glow of the source's color fades in over the neutral tile.
            Grid tile = new Grid();
            tile.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = Ds.Brush(RecTile(false)) });
            Border glow = new Border
            {
                CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Opacity = on ? 1 : 0, IsHitTestVisible = false,
                Background = new LinearGradientBrush(Ds.WithAlpha(tint, pal.Dark ? 0.15 : 0.10), Ds.WithAlpha(tint, pal.Dark ? 0.03 : 0.025), 90),
                BorderBrush = Ds.Brush(Ds.WithAlpha(tint, pal.Dark ? 0.30 : 0.26))
            };
            tile.Children.Add(glow);
            StackPanel col = new StackPanel { Margin = new Thickness(13) };
            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            string icon = computer ? "sound" : "mic";
            // Gray when off; the color fades in when it is on.
            Grid icons = new Grid { Width = 30, Height = 30 };
            icons.Children.Add(new IconTile(icon, 30, Ds.Rgb(152, 152, 157), Ds.Rgb(110, 110, 115)));
            IconTile colored = new IconTile(icon, 30, W(computer ? Mac.Orange : Mac.Pink), W(Mac.Red)) { Opacity = on ? 1 : 0 };
            icons.Children.Add(colored);
            head.Children.Add(icons);
            TextBlock title = Label(computer ? "Sonido del equipo" : "Micr\u00F3fono", Ds.Semibold, 13.5, pal.Label, new Thickness(11, 0, 10, 0));
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(title, 1);
            head.Children.Add(title);
            MacSwitch sw = new MacSwitch(on) { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sw, 2);
            head.Children.Add(sw);
            col.Children.Add(head);
            TextBlock d = Paragraph(computer ? "Lo que suena en el ordenador: v\u00EDdeos, llamadas, avisos." : "Tu voz mientras grabas, a un volumen claro y constante.", 12, pal.Label2);
            d.Margin = new Thickness(0, 10, 0, 0);
            d.MinHeight = 2 * d.LineHeight;
            col.Children.Add(d);
            TextBlock name;
            Border pill = RecPill(icon, out name);
            pill.Margin = new Thickness(0, 10, 0, 0);
            pill.Opacity = on ? 1 : 0.55;
            bool warn;
            RecSetText(name, RecSoundLabel(computer, out warn), warn);
            Press(pill, delegate { RecSoundMenu(pill, computer, name); });
            col.Children.Add(pill);
            tile.Children.Add(col);
            sw.Toggled += delegate(bool v)
            {
                if (computer) settings.RecordSystemAudio = v;
                else settings.RecordMic = v;
                Changed();
                RecFade(colored, v ? 1 : 0, 220);
                RecFade(pill, v ? 1 : 0.55, 220);
                RecFade(glow, v ? 1 : 0, 280);
            };
            return tile;
        }

        string RecSoundLabel(bool computer, out bool warn)
        {
            warn = false;
            string cur = computer ? settings.SystemAudioDevice : settings.MicDevice;
            if (string.IsNullOrEmpty(cur))
            {
                string d = AudioDevices.DefaultName(!computer);
                if (d != null) return "El de Windows: " + d;
                warn = true;
                return computer ? "No hay ninguna salida de sonido" : "No hay ning\u00FAn micr\u00F3fono conectado";
            }
            foreach (KeyValuePair<string, string> m in computer ? AudioDevices.Outputs() : AudioDevices.Microphones())
                if (string.Equals(m.Key, cur, StringComparison.OrdinalIgnoreCase)) return m.Value;
            warn = true;
            return "Desconectado: se usar\u00E1 el de Windows";
        }

        void RecSoundMenu(FrameworkElement anchor, bool computer, TextBlock name)
        {
            List<KeyValuePair<string, string>> devices = computer ? AudioDevices.Outputs() : AudioDevices.Microphones();
            string cur = (computer ? settings.SystemAudioDevice : settings.MicDevice) ?? "";
            string def = AudioDevices.DefaultName(!computer);
            string icon = computer ? "sound" : "mic";
            List<MenuEntry> list = new List<MenuEntry>();
            list.Add(MenuEntry.Title(computer ? "Grabar el sonido de" : "Micr\u00F3fono que se graba"));
            MenuEntry windows = MenuEntry.Item("El de Windows", icon, cur.Length == 0, delegate { RecSetSound(computer, "", name); });
            windows.Detail = def ?? (computer ? "No hay ninguna salida de sonido" : "No hay ning\u00FAn micr\u00F3fono conectado");
            list.Add(windows);
            if (devices.Count > 0) list.Add(MenuEntry.Line());
            bool found = false;
            foreach (KeyValuePair<string, string> d in devices)
            {
                string id = d.Key;
                bool mine = string.Equals(id, cur, StringComparison.OrdinalIgnoreCase);
                found |= mine;
                list.Add(MenuEntry.Item(d.Value, icon, mine, delegate { RecSetSound(computer, id, name); }));
            }
            if (cur.Length > 0 && !found)
            {
                MenuEntry gone = MenuEntry.Item("El que elegiste", icon, true, null);
                gone.Detail = "Desconectado: se usar\u00E1 el de Windows";
                gone.Enabled = false;
                list.Add(gone);
            }
            MacMenu.Show(anchor, list, anchor.ActualWidth);
        }

        void RecSetSound(bool computer, string id, TextBlock name)
        {
            if (computer) settings.SystemAudioDevice = id;
            else settings.MicDevice = id;
            Changed();
            bool warn;
            RecSetText(name, RecSoundLabel(computer, out warn), warn);
            RecIn(name);
        }

        // ---- Camera

        void RecCamera()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(1);
            TextBlock sub;
            StackPanel p = RecPanel(row, 0, "camera", pal.Green, "C\u00E1mara", RecCamHint(), out sub);
            Grid g = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            RecCamPreview preview = new RecCamPreview(settings.Webcam, settings.WebcamSize) { Height = 132 };
            g.Children.Add(preview);

            Grid form = new Grid { VerticalAlignment = VerticalAlignment.Center };
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            form.ColumnDefinitions.Add(new ColumnDefinition());
            MacSegmented shape = new MacSegmented(new string[] { "No", "Redonda", "Cuadrada" }, settings.Webcam);
            MacSegmented size = new MacSegmented(new string[] { "Peque\u00F1a", "Mediana", "Grande" }, settings.WebcamSize);
            TextBlock camName;
            recCamPill = RecPill("camera", out camName);
            recCamText = camName;
            recCamPill.MinWidth = Math.Max(shape.Width, size.Width);
            Border pill = recCamPill;
            Press(pill, RecCamClick);
            RecCamRefresh();
            FrameworkElement[] controls = { shape, size, pill };
            string[] names = { "Forma", "Tama\u00F1o", "C\u00E1mara" };
            for (int i = 0; i < controls.Length; i++)
            {
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                TextBlock t = Label(names[i], Ds.Regular, 12.5, pal.Label2, new Thickness(0, i > 0 ? 12 : 0, 14, 0));
                Grid.SetRow(t, i);
                form.Children.Add(t);
                controls[i].HorizontalAlignment = i == 2 ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
                controls[i].Margin = new Thickness(0, i > 0 ? 12 : 0, 0, 0);
                Grid.SetRow(controls[i], i);
                Grid.SetColumn(controls[i], 1);
                form.Children.Add(controls[i]);
            }
            size.Opacity = pill.Opacity = settings.Webcam == 0 ? 0.55 : 1;
            Grid.SetColumn(form, 2);
            g.Children.Add(form);
            p.Children.Add(g);

            shape.Changed += delegate(int i)
            {
                bool was = settings.Webcam != 0;
                settings.Webcam = i;
                Changed();
                preview.Show(i, settings.WebcamSize);
                RecFade(size, i == 0 ? 0.55 : 1, 220);
                RecFade(pill, i == 0 ? 0.55 : 1, 220);
                if (was != (i != 0)) { sub.Text = RecCamHint(); RecIn(sub); }
            };
            size.Changed += delegate(int i)
            {
                settings.WebcamSize = i;
                Changed();
                preview.Show(settings.Webcam, i);
            };
        }

        string RecCamHint()
        {
            return settings.Webcam == 0 ? "A\u00F1ade tu cara en una burbuja, como en Loom." :
                   "Sale en una esquina de la zona grabada. Arr\u00E1strala; doble clic cambia la forma y clic derecho el tama\u00F1o.";
        }

        void RecCamRefresh()
        {
            if (recCamText == null) return;
            bool engine = Recorder.FindFfmpeg(settings) != null;
            string cur = settings.WebcamDevice ?? "";
            string s;
            if (!engine) s = "Descarga el motor de v\u00EDdeo para elegir c\u00E1mara";
            else if (recSearching) s = "Buscando c\u00E1maras\u2026";
            else if (cur.Length == 0) s = recCameras != null && recCameras.Count > 0 ? "La primera que encuentre: " + recCameras[0] : "La primera que encuentre";
            else s = cur;
            RecSetText(recCamText, s, !engine || (recCameras != null && cur.Length > 0 && !recCameras.Contains(cur)));
            if (engine && recSearching) recCamText.Foreground = Ds.Brush(Ds.Brushes.Label2);
        }

        void RecCamClick()
        {
            string ff = Recorder.FindFfmpeg(settings);
            if (ff == null) { RecDownloadEngine(); return; }
            if (recCameras == null) { recCamMenuWanted = true; RecFindCameras(ff); return; }
            RecCamMenu();
        }

        // Lists the cameras on a pool thread (FFmpeg asks DirectShow, which can take a few seconds); the menu opens
        // when they arrive if it was asked for and the page is still showing.
        void RecFindCameras(string ff)
        {
            if (recSearching) return;
            recSearching = true;
            RecCamRefresh();
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> found = null;
                try { found = WebcamBubble.ListDevices(ff); }
                catch (Exception ex) { ShotStack.Log("C\u00E1maras: " + ex.Message); }
                Dispatcher.BeginInvoke((Action)delegate
                {
                    recCameras = found ?? new List<string>();
                    recSearching = false;
                    bool open = recCamMenuWanted;
                    recCamMenuWanted = false;
                    if (IsDisposed || page != "record" || recCamText == null) return;
                    RecCamRefresh();
                    RecIn(recCamText);
                    if (open && IsVisible && IsActive) RecCamMenu();
                });
            });
        }

        void RecCamMenu()
        {
            if (recCamPill == null || !recCamPill.IsVisible) return;
            List<string> cams = recCameras ?? new List<string>();
            string cur = settings.WebcamDevice ?? "";
            List<MenuEntry> list = new List<MenuEntry>();
            list.Add(MenuEntry.Title("C\u00E1mara que se graba"));
            MenuEntry first = MenuEntry.Item("La primera que encuentre", "camera", cur.Length == 0, delegate { RecSetCamera(""); });
            if (cams.Count > 0) first.Detail = cams[0];
            list.Add(first);
            list.Add(MenuEntry.Line());
            foreach (string c in cams)
            {
                string cam = c;
                list.Add(MenuEntry.Item(cam, "camera", cam == cur, delegate { RecSetCamera(cam); }));
            }
            if (cur.Length > 0 && !cams.Contains(cur))
            {
                MenuEntry gone = MenuEntry.Item(cur, "camera", true, null);
                gone.Detail = "No est\u00E1 conectada ahora";
                gone.Enabled = false;
                list.Add(gone);
            }
            if (cams.Count == 0)
            {
                MenuEntry none = MenuEntry.Item("No se encuentra ninguna c\u00E1mara", null, false, null);
                none.Enabled = false;
                list.Add(none);
            }
            list.Add(MenuEntry.Line());
            list.Add(MenuEntry.Item("Buscar de nuevo", "search", false, delegate
            {
                string ff = Recorder.FindFfmpeg(settings);
                if (ff == null || recSearching) return;
                recCameras = null;
                recCamMenuWanted = true;
                RecFindCameras(ff);
            }));
            MacMenu.Show(recCamPill, list, recCamPill.ActualWidth);
        }

        void RecSetCamera(string name)
        {
            settings.WebcamDevice = name;
            Changed();
            RecCamRefresh();
            RecIn(recCamText);
        }

        void RecDownloadEngine()
        {
            FfmpegSetup.Run(settings);
            Rebuild();
        }

        // The recorded screen in miniature with the camera bubble in its corner; shape and size morph as they change.
        sealed class RecCamPreview : FrameworkElement
        {
            static readonly DependencyProperty BubbleProperty = DependencyProperty.Register("Bubble", typeof(double), typeof(RecCamPreview),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty RoundProperty = DependencyProperty.Register("Round", typeof(double), typeof(RecCamPreview),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty ShownProperty = DependencyProperty.Register("Shown", typeof(double), typeof(RecCamPreview),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly double[] Sizes = { 30, 40, 52 };

            public RecCamPreview(int shape, int size)
            {
                SetValue(BubbleProperty, Sizes[Math.Max(0, Math.Min(2, size))]);
                SetValue(RoundProperty, shape == 2 ? 0.0 : 1.0);
                SetValue(ShownProperty, shape == 0 ? 0.0 : 1.0);
                IsHitTestVisible = false;
                SnapsToDevicePixels = true;
            }

            public void Show(int shape, int size)
            {
                Go(ShownProperty, shape == 0 ? 0 : 1, shape == 0 ? 200 : 380, shape != 0);
                if (shape != 0) Go(RoundProperty, shape == 2 ? 0 : 1, 360, false);
                Go(BubbleProperty, Sizes[Math.Max(0, Math.Min(2, size))], 380, true);
            }

            void Go(DependencyProperty p, double to, double ms, bool spring)
            {
                DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
                if (spring) a.EasingFunction = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
                else a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
                BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
            }

            protected override void OnRender(DrawingContext dc)
            {
                Palette pal = Ds.Brushes;
                double w = ActualWidth, h = ActualHeight;
                if (w < 20 || h < 20) return;
                Rect all = new Rect(0, 0, w, h);
                Brush wall = pal.Dark ? new LinearGradientBrush(Ds.Rgb(64, 70, 104), Ds.Rgb(36, 38, 54), 65) : new LinearGradientBrush(Ds.Rgb(204, 222, 252), Ds.Rgb(238, 228, 252), 65);
                dc.DrawRoundedRectangle(wall, null, all, 10, 10);
                // A window on the screen.
                Rect win = new Rect(Math.Round(w * 0.27), Math.Round(h * 0.15), Math.Round(w * 0.64), Math.Round(h * 0.62));
                Rect ws = win;
                ws.Offset(0, 2);
                dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(pal.Dark ? 0.35 : 0.10, 0, 0, 0)), null, ws, 6, 6);
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Rgb(50, 50, 55) : Colors.White), null, win, 6, 6);
                Color[] dots = { Ds.Rgb(255, 95, 87), Ds.Rgb(254, 188, 46), Ds.Rgb(40, 200, 64) };
                for (int i = 0; i < 3; i++) dc.DrawEllipse(Ds.Brush(dots[i]), null, new Point(win.X + 8 + i * 7, win.Y + 7), 2.1, 2.1);
                Color line = pal.Dark ? Ds.Argb(0.16, 255, 255, 255) : Ds.Argb(0.09, 0, 0, 0);
                double[] lens = { 0.82, 0.58, 0.74, 0.42 };
                for (int i = 0; i < lens.Length; i++)
                    dc.DrawRoundedRectangle(Ds.Brush(i == 0 ? Ds.WithAlpha(pal.Accent, 0.75) : line), null, new Rect(win.X + 10, win.Y + 17 + i * 11, (win.Width - 20) * lens[i], 4.5), 2.25, 2.25);
                // Recording dot.
                dc.DrawEllipse(Ds.Brush(Ds.WithAlpha(pal.Red, 0.3)), null, new Point(w - 13, 12), 5.5, 5.5);
                dc.DrawEllipse(Ds.Brush(pal.Red), null, new Point(w - 13, 12), 3.2, 3.2);

                double shown = Math.Max(0, (double)GetValue(ShownProperty));
                double d = (double)GetValue(BubbleProperty), round = Math.Max(0, Math.Min(1, (double)GetValue(RoundProperty)));
                double rad = d * (0.2 + 0.3 * round);
                Rect b = new Rect(10, h - 10 - d, d, d);
                // Off: a dashed outline where the bubble would go.
                if (shown < 0.99)
                {
                    Pen dash = new Pen(Ds.Brush(pal.Dark ? Ds.Argb(0.35 * (1 - Math.Min(1, shown)), 255, 255, 255) : Ds.Argb(0.3 * (1 - Math.Min(1, shown)), 0, 0, 0)), 1.2);
                    dash.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
                    dc.DrawRoundedRectangle(null, dash, Rect.Inflate(b, -0.6, -0.6), rad, rad);
                }
                if (shown > 0.01)
                {
                    double s = 0.55 + 0.45 * shown;
                    dc.PushOpacity(Math.Min(1, shown));
                    dc.PushTransform(new ScaleTransform(s, s, b.X + d / 2, b.Y + d / 2));
                    Rect sh = b;
                    sh.Offset(0, 2);
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.28, 0, 0, 0)), null, Rect.Inflate(sh, 1, 1), rad + 1, rad + 1);
                    dc.PushClip(new RectangleGeometry(b, rad, rad));
                    dc.DrawRectangle(new LinearGradientBrush(Ds.Rgb(168, 174, 188), Ds.Rgb(118, 124, 138), 90), null, b);
                    Brush person = Ds.Brush(Ds.Argb(0.92, 255, 255, 255));
                    dc.DrawEllipse(person, null, new Point(b.X + d / 2, b.Y + d * 0.4), d * 0.17, d * 0.17);
                    dc.DrawEllipse(person, null, new Point(b.X + d / 2, b.Y + d * 1.02), d * 0.33, d * 0.3);
                    dc.Pop();
                    dc.DrawRoundedRectangle(null, new Pen(Brushes.White, 2), Rect.Inflate(b, -1, -1), Math.Max(0, rad - 1), Math.Max(0, rad - 1));
                    dc.Pop();
                    dc.Pop();
                }
                dc.DrawRoundedRectangle(null, new Pen(Ds.Brush(pal.Hairline), 1), new Rect(0.5, 0.5, w - 1, h - 1), 9.5, 9.5);
            }
        }

        // ---- GIF and the video engine

        void RecGifAndEngine()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(2);
            TextBlock sub;
            StackPanel gp = RecPanel(row, 0, "gif", pal.Orange, "GIF", null, out sub);
            StackPanel big = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            int gi = Nearest(RecGifFps, settings.GifFps);
            TextBlock num = Label(RecGifFps[gi].ToString(), Ds.Title, 30, pal.Label);
            big.Children.Add(num);
            big.Children.Add(Label("fotogramas por segundo", Ds.Regular, 12, pal.Label2, new Thickness(8, 12, 0, 0)));
            gp.Children.Add(big);
            MacSegmented gs = new MacSegmented(new string[] { "10", "15", "20", "25" }, gi);
            gs.HorizontalAlignment = HorizontalAlignment.Left;
            gs.Margin = new Thickness(0, 10, 0, 0);
            gs.Changed += delegate(int i)
            {
                settings.GifFps = RecGifFps[i];
                Changed();
                num.Text = RecGifFps[i].ToString();
                RecIn(num);
            };
            gp.Children.Add(gs);
            TextBlock gn = Paragraph("Los GIF pesan mucho: 15 suele ser el punto justo.", 12, pal.Label2);
            gn.Margin = new Thickness(0, 10, 0, 0);
            gp.Children.Add(gn);

            string ff = Recorder.FindFfmpeg(settings);
            StackPanel ep = RecPanel(row, 1, "gear", pal.Label2, "Motor de v\u00EDdeo", null, out sub);
            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            Color dot = ff != null ? pal.Green : pal.Orange;
            Grid halo = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            halo.Children.Add(new System.Windows.Shapes.Ellipse { Fill = Ds.Brush(Ds.WithAlpha(dot, 0.25)) });
            halo.Children.Add(new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Ds.Brush(dot) });
            state.Children.Add(halo);
            state.Children.Add(Label(ff != null ? "Listo" : "Sin descargar", Ds.Title, 20, pal.Label, new Thickness(9, 0, 0, 0)));
            ep.Children.Add(state);
            TextBlock info = Paragraph(ff != null ? "FFmpeg graba y codifica tus v\u00EDdeos y GIF." : "Se descarga solo (unos 110 MB) la primera vez que grabes.", 12, pal.Label2);
            info.Margin = new Thickness(0, 8, 0, 0);
            ep.Children.Add(info);
            if (ff != null)
            {
                TextBlock where = Label(ShortPath(ff), Ds.Regular, 11.5, pal.Label3, new Thickness(0, 4, 0, 0));
                where.TextTrimming = TextTrimming.CharacterEllipsis;
                where.ToolTip = ff;
                ep.Children.Add(where);
            }
            else
            {
                MacButton get = new MacButton("Descargar ahora", ButtonKind.Primary);
                get.HorizontalAlignment = HorizontalAlignment.Left;
                get.Margin = new Thickness(0, 12, 0, 0);
                get.Click += RecDownloadEngine;
                ep.Children.Add(get);
            }
        }

        void RecAfter()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(1);
            TextBlock sub;
            StackPanel p = RecPanel(row, 0, "edit", pal.Accent, "Despu\u00E9s de grabar",
                     "\u00ABEditar\u00BB en la miniatura abre el editor de v\u00EDdeo: recorta el principio y el final, cambia la velocidad, el tama\u00F1o o el formato, " +
                     "a\u00F1ade marcas y ponle fondo. El sonido se mantiene.", out sub);

            // The same switch as General > Copiar cada captura: a finished recording goes to the clipboard as a file.
            Border tile = new Border { CornerRadius = new CornerRadius(12), Background = Ds.Brush(RecTile(false)), Padding = new Thickness(13, 11, 13, 11), Margin = new Thickness(0, 14, 0, 0) };
            Grid inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.ColumnDefinitions.Add(new ColumnDefinition());
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.Children.Add(new IconTile("copy", 28, W(Mac.Teal), W(Mac.Blue)) { VerticalAlignment = VerticalAlignment.Center });
            StackPanel words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 12, 0) };
            words.Children.Add(Label("Copiar el archivo al terminar", Ds.Semibold, 13, pal.Label));
            // One setting for both: saying so avoids switching off the copy of captures by surprise.
            TextBlock wl = Paragraph("Listo para pegar en un chat o un correo nada m\u00E1s parar. Es el mismo ajuste que \u00ABCopiar cada captura\u00BB, en General.", 12, pal.Label2);
            wl.Margin = new Thickness(0, 2, 0, 0);
            words.Children.Add(wl);
            Grid.SetColumn(words, 1);
            inner.Children.Add(words);
            MacSwitch sw = new MacSwitch(settings.CopyToClipboard) { VerticalAlignment = VerticalAlignment.Center };
            sw.Toggled += delegate(bool v) { settings.CopyToClipboard = v; Changed(); };
            Grid.SetColumn(sw, 2);
            inner.Children.Add(sw);
            tile.Child = inner;
            p.Children.Add(tile);

            // Videos and GIF use the same backdrop as captures when presented.
            StackPanel lk = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                                             Cursor = Cursors.Hand, Background = Brushes.Transparent };
            TextBlock lt = Label("Elegir el fondo de \u00ABPresentar\u00BB", Ds.Medium, 12, pal.Accent);
            lk.Children.Add(lt);
            lk.Children.Add(new GlyphView("right", 11, pal.Accent, 1.6) { Margin = new Thickness(3, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            lk.MouseEnter += delegate { lt.TextDecorations = TextDecorations.Underline; };
            lk.MouseLeave += delegate { lt.TextDecorations = null; };
            lk.MouseLeftButtonUp += delegate { SetPage("editor", true); };
            p.Children.Add(lk);
        }
    }
}
