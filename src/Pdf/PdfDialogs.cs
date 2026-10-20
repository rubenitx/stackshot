// Stackshot - The PDF viewer's small dialogs: the password of a protected PDF and which pages to capture or save.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    // A dialog in the style of DocAlert: icon, title, what to fill in, and the buttons on the right.
    public abstract class PdfAsk : Sheet
    {
        protected bool Ok;
        protected readonly StackPanel Body = new StackPanel();
        MacButton main;

        protected PdfAsk(string title, string message, string icon, Color c1, Color c2, string action)
        {
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ShowInTaskbar = false;
            Icon = AppIcon;
            foreach (UIElement e in Root.Children) if (e is StackPanel && Panel.GetZIndex(e) == 100) e.Visibility = Visibility.Collapsed;
            Palette pal = Ds.Brushes;
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            DragAnywhere();
            Grid g = new Grid { Margin = new Thickness(24, 22, 22, 20) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            IconTile tile = new IconTile(icon, 40, c1, c2) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 16, 0) };
            g.Children.Add(tile);
            StackPanel s = new StackPanel();
            Grid.SetColumn(s, 1);
            s.Children.Add(new TextBlock { Text = title, FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 15.5, Foreground = Ds.Brush(pal.Label), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(message))
                s.Children.Add(new TextBlock { Text = message, FontSize = 13, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            Body.Margin = new Thickness(0, 14, 0, 0);
            s.Children.Add(Body);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            main = new MacButton(action, ButtonKind.Primary, null, null, 30) { MinWidth = 92 };
            main.Click += delegate { Accept(); };
            MacButton cancel = new MacButton("Cancelar", ButtonKind.Secondary, null, null, 30) { MinWidth = 92, Margin = new Thickness(8, 0, 0, 0) };
            cancel.Click += delegate { Close(); };
            buttons.Children.Add(main);
            buttons.Children.Add(cancel);
            s.Children.Add(buttons);
            g.Children.Add(s);
            Root.Children.Add(g);
            KeepCaptionsOnTop();
            PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter) { e.Handled = true; Accept(); }
            };
        }

        protected virtual bool Valid() { return true; }

        void Accept()
        {
            if (!Valid()) return;
            Ok = true;
            Close();
        }

        protected bool Run(Window owner)
        {
            try
            {
                if (owner != null && owner.IsVisible) { Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
                else WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ShowDialog();
            }
            catch (Exception ex) { ShotStack.Log("PDF, di\u00E1logo: " + ex.Message); return false; }
            return Ok;
        }

        protected static Border Field(UIElement child)
        {
            Palette pal = Ds.Brushes;
            return new Border { CornerRadius = new CornerRadius(7), Background = Ds.Brush(pal.Control), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1), Padding = new Thickness(8, 4, 8, 4), Child = child };
        }
    }

    public sealed class PdfPassword : PdfAsk
    {
        readonly PasswordBox box;

        PdfPassword(string name, bool again)
            : base(again ? "La contrase\u00F1a no es esa" : "\u00AB" + name + "\u00BB tiene contrase\u00F1a",
                   again ? "Prueba otra vez. Stackshot no la guarda en ning\u00FAn sitio." : "Escr\u00EDbela para abrirlo. Stackshot no la guarda en ning\u00FAn sitio.",
                   "shield", Ds.Rgb(255, 179, 64), Ds.Rgb(245, 110, 40), "Abrir")
        {
            Palette pal = Ds.Brushes;
            box = new PasswordBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label), FontSize = 14, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            Body.Children.Add(Field(box));
            Loaded += delegate { box.Focus(); };
        }

        protected override bool Valid() { return box.Password.Length > 0; }

        // The password, or null when cancelled.
        public static string Ask(Window owner, string name, bool again)
        {
            PdfPassword w = new PdfPassword(name, again);
            return w.Run(owner) ? w.box.Password : null;
        }
    }

    // Which pages: the one shown, the ones picked in the sidebar, all, or a range; for captures, also whether they go
    // together in one tall image.
    public sealed class PdfPagesDialog : PdfAsk
    {
        readonly int pages, current;
        readonly List<int> picked;
        readonly MacSegmented which;
        readonly TextBox from, to;
        readonly MacSwitch together;
        readonly TextBlock error;
        bool joined;
        readonly int count;

        PdfPagesDialog(string title, string message, string action, int pages, int current, List<int> picked, bool capture)
            : base(title, message, capture ? "camera" : "save", Ds.Rgb(90, 200, 250), Ds.Rgb(0, 122, 255), action)
        {
            this.pages = pages;
            this.current = current;
            this.picked = picked;
            Palette pal = Ds.Brushes;
            List<string> options = new List<string> { "Esta (" + (current + 1) + ")" };
            if (picked.Count > 1) options.Add("Elegidas (" + picked.Count + ")");
            options.Add("Todas (" + pages + ")");
            options.Add("Desde\u2026");
            count = options.Count;
            which = new MacSegmented(options.ToArray(), picked.Count > 1 ? 1 : 0) { HorizontalAlignment = HorizontalAlignment.Left };
            Body.Children.Add(which);
            StackPanel range = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            from = Num((current + 1).ToString());
            to = Num(Math.Min(pages, current + 5).ToString());
            range.Children.Add(Label("Desde la p\u00E1gina", pal));
            range.Children.Add(Field(from));
            range.Children.Add(Label("hasta la", pal));
            range.Children.Add(Field(to));
            Body.Children.Add(range);
            which.Changed += delegate(int i) { range.Opacity = i == options.Count - 1 ? 1 : 0.45; error.Text = ""; };
            range.Opacity = 0.45;
            from.GotFocus += delegate { which.Selected = options.Count - 1; range.Opacity = 1; };
            to.GotFocus += delegate { which.Selected = options.Count - 1; range.Opacity = 1; };
            if (capture)
            {
                DockPanel row = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
                together = new MacSwitch(false);
                DockPanel.SetDock(together, Dock.Right);
                row.Children.Add(together);
                row.Children.Add(new TextBlock { Text = "Todas juntas en una sola imagen alta", FontSize = 13, Foreground = Ds.Brush(pal.Label), VerticalAlignment = VerticalAlignment.Center });
                Body.Children.Add(row);
            }
            error = new TextBlock { FontSize = 12, Foreground = Ds.Brush(pal.Warn), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
            Body.Children.Add(error);
        }

        static TextBlock Label(string t, Palette pal) { return new TextBlock { Text = t, FontSize = 13, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }; }

        TextBox Num(string v)
        {
            Palette pal = Ds.Brushes;
            TextBox t = new TextBox { Text = v, Width = 52, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label), FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
            t.Margin = new Thickness(0);
            return t;
        }

        List<int> result;

        protected override bool Valid()
        {
            int i = which.Selected;
            int last = count - 1;
            List<int> o = new List<int>();
            if (i == 0) o.Add(current);
            else if (picked.Count > 1 && i == 1) o.AddRange(picked);
            else if (i == last)
            {
                int a, b;
                if (!int.TryParse(from.Text.Trim(), out a) || !int.TryParse(to.Text.Trim(), out b) || a < 1 || b < 1 || a > pages || b > pages)
                {
                    error.Text = "Escribe dos p\u00E1ginas entre 1 y " + pages + ".";
                    return false;
                }
                for (int k = Math.Min(a, b); k <= Math.Max(a, b); k++) o.Add(k - 1);
            }
            else for (int k = 0; k < pages; k++) o.Add(k);
            result = o;
            joined = together != null && together.IsOn;
            return true;
        }

        // The pages chosen (0-based), or null when cancelled.
        public static List<int> Ask(Window owner, string title, string message, string action, int pages, int current, List<int> picked, bool capture, out bool joined)
        {
            PdfPagesDialog w = new PdfPagesDialog(title, message, action, pages, current, picked, capture);
            joined = false;
            if (!w.Run(owner)) return null;
            joined = w.joined;
            return w.result;
        }
    }
}
