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
            I18n.LanguageOption option = I18n.FindLanguage(code) ?? I18n.FindLanguage("auto");
            return option.Name;
        }

        LanguagePicker(string current)
        {
            this.current = current;
            Title = I18n.T("Elegir idioma", "Choose language");
            Width = Math.Min(480, SystemParameters.WorkArea.Width);
            Height = Math.Min(540, SystemParameters.WorkArea.Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Icon = AppIcon;
            Palette pal = Ds.Brushes;

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
                Text = I18n.T("Busca por nombre o código. Los idiomas en vista previa usan inglés donde falte traducción.",
                              "Search by name or code. Preview languages use English where translations are missing."),
                Margin = new Thickness(0, 5, 0, 10), TextWrapping = TextWrapping.Wrap, Foreground = Ds.Brush(pal.Label2)
            });
            var field = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            string searchLabel = I18n.T("Buscar idiomas", "Search languages");
            search = new TextBox
            {
                Height = 34, Padding = new Thickness(30, 0, 9, 0), MaxLength = 80,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Ds.Brush(pal.Window), BorderBrush = Ds.Brush(pal.Hairline),
                Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label)
            };
            AutomationProperties.SetName(search, searchLabel);
            field.Children.Add(search);
            field.Children.Add(new GlyphView("search", 13, pal.Label2, 1.7)
            {
                Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            });
            var placeholder = new TextBlock
            {
                Text = searchLabel, Margin = new Thickness(31, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ds.Brush(pal.Label3), IsHitTestVisible = false
            };
            field.Children.Add(placeholder);
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
            Grid.SetRow(results, 1);
            layout.Children.Add(results);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            // The custom buttons draw their own text. Keep their drawing coordinates
            // left-to-right so WPF mirrors their placement without mirroring the glyphs.
            var cancel = new MacButton(I18n.Cancel, ButtonKind.Secondary) { Margin = new Thickness(0, 0, 8, 0), FlowDirection = FlowDirection.LeftToRight };
            cancel.Click += delegate { Close(); };
            apply = new MacButton(I18n.T("Aplicar idioma", "Apply language"), ButtonKind.Primary) { FlowDirection = FlowDirection.LeftToRight };
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
                string detail = option.Code == "auto" ? I18n.T("Seguir el idioma de Windows", "Follow the Windows language")
                              : option.Preview ? I18n.T("Vista previa", "Preview") + " · " + option.Code
                              : option.Code == "de" ? I18n.T("Traducción parcial", "Partial translation") + " · de"
                              : option.Code;
                if (active) detail += " · " + I18n.T("Actual", "Current");
                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                labels.Children.Add(new TextBlock { Text = option.Name, FontWeight = Ds.Semibold.Weight, Foreground = Ds.Brush(pal.Label) });
                labels.Children.Add(new TextBlock { Text = detail, FontSize = 11.5, Foreground = Ds.Brush(pal.Label2) });
                var row = new DockPanel { Margin = new Thickness(8, 5, 8, 5) };
                row.Children.Add(new GlyphView(active ? "check" : "globe", 17, active ? pal.Accent : pal.Label2, 1.7)
                { Margin = new Thickness(0, 0, 11, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(labels);
                var item = new ListBoxItem { Tag = option.Code, Content = row, MinHeight = 47, HorizontalContentAlignment = HorizontalAlignment.Stretch };
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
    }
}
