// Stackshot - Searchable language sheet shared by Settings, Welcome and the tray.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Stackshot
{
    internal sealed class LanguagePicker : Sheet
    {
        readonly string current;
        readonly TextBox search;
        readonly ListBox list;
        readonly TextBlock empty;
        readonly MacButton apply;
        string picked;

        internal static string Choose(Window owner, string current)
        {
            var picker = new LanguagePicker(current);
            if (owner != null && owner.IsVisible)
            {
                picker.Owner = owner;
                picker.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            return picker.ShowDialog() == true ? picker.picked : null;
        }

        internal static void Apply(Settings settings, string code)
        {
            settings.Language = code;
            settings.LanguageOffered = true;
            settings.Save();
            I18n.Apply(code);
        }

        internal static string DisplayName(string code)
        {
            I18n.LanguageOption option = I18n.FindListedLanguage(code) ?? I18n.FindLanguage("auto");
            return option.Name;
        }

        // The rows at 100 % (owner: the sheet was "muy apretado"): 56 tall, so the badge and the two lines breathe,
        // and 5 apart, so the ring of the selected row never meets the hover of its neighbour. The ring keeps 12 from
        // the list's edge (the scrollbar's gutter included) and 12 more from what it holds.
        const double RowHeight = 56, RowGap = 5, RowInset = 12, RowPad = 12, Badge = 32;

        // The sheet at 100 %: a little wider and 120 taller than the old 480 x 540, so seven full rows of the new
        // height still fit; never past the work area it opens on (Sheet.FitWorkArea clamps it as well).
        internal static Size Preferred(double workWidth, double workHeight)
        {
            return new Size(Math.Min(520, Math.Max(320, workWidth)), Math.Min(660, Math.Max(240, workHeight)));
        }

        // A length of the sheet snapped to whole device pixels, so the taller rows stay crisp at 125 % and 150 %.
        double P(double dip)
        {
            double k = Ink.Scale(this);
            return Math.Max(1, Math.Round(dip * k, MidpointRounding.AwayFromZero)) / k;
        }

        LanguagePicker(string current)
        {
            this.current = current;
            Title = I18n.T("Elegir idioma", "Choose language");
            Size size = Preferred(SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            Width = size.Width;
            Height = size.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Icon = AppIcon;
            Palette pal = Ds.Brushes;

            // The aurora band of the 01-aurora artboard: the brand glow behind the title and the hairline along the
            // top edge, under everything.
            Root.Children.Insert(0, new Aurora.Band { Height = 170 });

            var layout = new Grid
            {
                Margin = new Thickness(22, 33, 22, 18),
                FlowDirection = I18n.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
            };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.Children.Add(layout);

            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = Title, FontFamily = Ds.Title.FontFamily, FontWeight = Ds.Title.Weight, FontSize = 25, Foreground = Ds.Brush(pal.Label) });
            header.Children.Add(new TextBlock
            {
                Text = I18n.PreviewsListed ? I18n.T("Busca por nombre o código. Los idiomas en vista previa usan inglés donde falte traducción.",
                                                    "Search by name or code. Preview languages use English where translations are missing.")
                                           : I18n.T("Busca por nombre o código.", "Search by name or code.", "Suche nach Name oder Sprachcode."),
                Margin = new Thickness(0, 5, 0, 10), TextWrapping = TextWrapping.Wrap, Foreground = Ds.Brush(pal.Label2)
            });
            var field = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(Ds.RPanel),
                // The artboard's field: translucent over the window, a hairline edge, an accent edge while it has focus.
                Background = Ds.Brush(pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.62, 255, 255, 255)),
                BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1), SnapsToDevicePixels = true
            };
            string searchLabel = I18n.T("Buscar idiomas", "Search languages");
            search = new TextBox
            {
                Height = 32, Padding = new Thickness(30, 0, 9, 0), MaxLength = 80,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label)
            };
            AutomationProperties.SetName(search, searchLabel);
            search.GotKeyboardFocus += delegate { field.BorderBrush = Ds.Brush(Ds.Brushes.Accent); };
            search.LostKeyboardFocus += delegate { field.BorderBrush = Ds.Brush(Ds.Brushes.Hairline); };
            var fieldBox = new Grid();
            fieldBox.Children.Add(search);
            fieldBox.Children.Add(new GlyphView("search", 13, pal.Label2, 1.7)
            {
                Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            });
            var placeholder = new TextBlock
            {
                Text = searchLabel, Margin = new Thickness(31, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ds.Brush(pal.Label3), IsHitTestVisible = false
            };
            fieldBox.Children.Add(placeholder);
            field.Child = fieldBox;
            header.Children.Add(field);
            layout.Children.Add(header);

            var results = new Grid();
            list = new ListBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                RenderTransform = new TranslateTransform()
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            AutomationProperties.SetName(list, I18n.T("Idiomas", "Languages"));
            results.Children.Add(list);
            empty = new TextBlock
            {
                Text = I18n.T("No hay idiomas que coincidan.", "No matching languages."),
                Foreground = Ds.Brush(pal.Label2), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            };
            results.Children.Add(empty);
            // The rows do not run into the search box above or the buttons below.
            results.Margin = new Thickness(0, P(3), 0, P(3));
            Grid.SetRow(results, 1);
            layout.Children.Add(results);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            // The custom buttons draw their own text. Keep their drawing coordinates
            // left-to-right so WPF mirrors their placement without mirroring the glyphs.
            var cancel = new MacButton(I18n.Cancel, ButtonKind.Secondary) { Margin = new Thickness(0, 0, 8, 0), FlowDirection = FlowDirection.LeftToRight };
            cancel.Click += delegate { Close(); };
            // The aurora primary of the artboard (Aplicar idioma in the indigo → accent gradient).
            apply = new MacButton(I18n.T("Aplicar idioma", "Apply language"), ButtonKind.Primary) { FlowDirection = FlowDirection.LeftToRight, BrandPrimary = true };
            apply.Click += Commit;
            actions.Children.Add(cancel);
            actions.Children.Add(apply);
            Grid.SetRow(actions, 2);
            layout.Children.Add(actions);

            list.SelectionChanged += delegate { apply.IsEnabled = list.SelectedItem != null; };
            list.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            {
                if (ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is ListBoxItem) Commit();
            };
            search.TextChanged += delegate
            {
                placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                Filter(true);
            };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter && (search.IsKeyboardFocusWithin || list.IsKeyboardFocusWithin)) { e.Handled = true; Commit(); }
                else if (e.Key == Key.Down && search.IsKeyboardFocusWithin && list.SelectedItem is ListBoxItem)
                {
                    e.Handled = true;
                    ((ListBoxItem)list.SelectedItem).Focus();
                }
                else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    search.Focus();
                    search.SelectAll();
                }
            };
            Filter(false);
            Loaded += delegate
            {
                if (list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
                search.Focus();
            };
        }

        void Filter(bool animate)
        {
            var previous = list.SelectedItem as ListBoxItem;
            string selected = previous == null ? current : (string)previous.Tag;
            list.Items.Clear();
            Palette pal = Ds.Brushes;
            foreach (I18n.LanguageOption option in I18n.FilterLanguages(search.Text))
            {
                bool active = option.Code == current;
                // Deutsch is complete, not partial (S3): it says so like es and en do, in every interface language.
                string detail = option.Code == "auto" ? I18n.T("Seguir el idioma de Windows", "Follow the Windows language")
                              : option.Preview ? I18n.T("Vista previa", "Preview") + " · " + option.Code
                              : I18n.T("Completo", "Complete", "Vollständig") + " · " + option.Code;
                if (active) detail += " · " + I18n.T("Actual", "Current");
                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                labels.Children.Add(new TextBlock { Text = option.Name, FontWeight = Ds.Semibold.Weight, Foreground = Ds.Brush(pal.Label) });
                labels.Children.Add(new TextBlock { Text = detail, FontSize = 11.5, Margin = new Thickness(0, P(2), 0, 0), Foreground = Ds.Brush(pal.Label2) });

                // The aurora row (01-aurora): the language's monogram tile, its texts and, on the current language,
                // its check; the gradient ring around the selected row is drawn by the ring under it.
                Color c1, c2;
                MonogramColors(option.Code, out c1, out c2);
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var tile = new Aurora.Monogram(Monogram(option.Code), c1, c2, P(Badge)) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, P(12), 0) };
                Grid.SetColumn(tile, 0);
                row.Children.Add(tile);
                Grid.SetColumn(labels, 1);
                labels.Margin = new Thickness(0, 0, P(9), 0);
                row.Children.Add(labels);
                GlyphView check = new GlyphView("check", 17, pal.Accent, 1.7) { VerticalAlignment = VerticalAlignment.Center };
                if (!active) check.Visibility = Visibility.Collapsed; else Grid.SetColumn(check, 2);
                row.Children.Add(check);
                // The texts keep clear of the ring, and the ring keeps clear of the frame and of the scrollbar.
                row.Margin = new Thickness(P(RowPad), 0, P(RowPad), 0);
                var ring = new Aurora.Ring
                {
                    Child = row, MinHeight = P(RowHeight),
                    Margin = new Thickness(P(RowInset), 0, P(RowInset), 0)
                };
                var item = new ListBoxItem { Tag = option.Code, Content = ring, MinHeight = P(RowHeight), Margin = new Thickness(0, 0, 0, P(RowGap)), Style = ItemStyle() };
                // The ring and the check's colour follow the selection (whatever moves it: mouse, keyboard, search).
                item.Selected += delegate { ring.Selected = true; if (active) check.Color = Colors.White; };
                item.Unselected += delegate { ring.Selected = false; if (active) check.Color = pal.Accent; };
                AutomationProperties.SetName(item, option.Name + ", " + detail);
                list.Items.Add(item);
                if (option.Code == selected) list.SelectedItem = item;
            }
            if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
            if (list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
            empty.Visibility = list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            apply.IsEnabled = list.SelectedItem != null;
            if (animate && SystemParameters.ClientAreaAnimation && !pal.High)
            {
                list.BeginAnimation(OpacityProperty, new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(130)));
                ((TranslateTransform)list.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(3, 0, TimeSpan.FromMilliseconds(130)) { EasingFunction = new QuadraticEase() });
            }
        }

        void Commit()
        {
            var selected = list.SelectedItem as ListBoxItem;
            if (selected == null) return;
            picked = (string)selected.Tag;
            DialogResult = true;
        }

        // The tile pair of each language (the Mac ends of Look.cs, as the artboard), and its monogram. "auto" shares
        // the grey of the artboard's first row.
        static void MonogramColors(string code, out Color c1, out Color c2)
        {
            switch (code)
            {
                case "es": c1 = Media(Mac.Blue); c2 = Media(Mac.Indigo); return;
                case "en": c1 = Media(Mac.Purple); c2 = Media(Mac.Indigo); return;
                case "de": c1 = Media(Mac.Teal); c2 = Media(Mac.Blue); return;
                case "ko": c1 = Media(Mac.Pink); c2 = Media(Mac.Purple); return;
                case "fr": c1 = Media(Mac.Blue); c2 = Media(Mac.Teal); return;
                case "pt": c1 = Media(Mac.Green); c2 = Media(Mac.Blue); return;
                case "zh": c1 = Media(Mac.Red); c2 = Media(Mac.Orange); return;
                case "hi": c1 = Media(Mac.Orange); c2 = Media(Mac.Purple); return;
                case "ar": c1 = Media(Mac.Red); c2 = Media(Mac.Purple); return;
                case "bn": c1 = Media(Mac.Teal); c2 = Media(Mac.Indigo); return;
                case "auto": c1 = Ds.Rgb(142, 142, 147); c2 = Ds.Rgb(110, 110, 115); return;
                default: c1 = Media(Mac.Indigo); c2 = Media(Mac.Blue); return;
            }
        }

        static Color Media(System.Drawing.Color c) { return Color.FromRgb(c.R, c.G, c.B); }

        static string Monogram(string code)
        {
            switch (code)
            {
                case "auto": return "A";
                case "ko": return "\uD55C";
                case "zh": return "中";
                case "hi": return "हि";
                case "ar": return "ع";
                case "bn": return "বা";
                default: return code.ToUpperInvariant();
            }
        }

        static Style itemStyle;

        // A bare host for the rows: the default list look (its blue selection and hover) gave way to the aurora ring,
        // which a Ring lives under each row and draws.
        static Style ItemStyle()
        {
            if (itemStyle != null) return itemStyle;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            var template = new ControlTemplate(typeof(ListBoxItem));
            template.VisualTree = border;
            template.Seal();
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(FrameworkElement.SnapsToDevicePixelsProperty, true));
            style.Setters.Add(new Setter(System.Windows.Controls.Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            itemStyle = style;
            return style;
        }
    }
}
