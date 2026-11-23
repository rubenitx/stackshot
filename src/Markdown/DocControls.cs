// Stackshot - Small controls for the document window: icon buttons for the formatting bar, option chips, a text field and
// the Guardar / No guardar / Cancelar alert, all drawn with the design system.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    // An icon-only button: a Glyph name, one of the formatting icons below, or "text:X" drawn as a letter.
    public sealed class DocIconButton : Drawn
    {
        // Formatting icons on the 24x24 grid of Glyphs.cs: "stroke|fill" path data.
        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            { "md-list", "M9.5,6.5 H20 M9.5,12 H20 M9.5,17.5 H20|M4.5,5.2 A1.3,1.3 0 1 1 4.5,7.8 A1.3,1.3 0 1 1 4.5,5.2 Z M4.5,10.7 A1.3,1.3 0 1 1 4.5,13.3 A1.3,1.3 0 1 1 4.5,10.7 Z M4.5,16.2 A1.3,1.3 0 1 1 4.5,18.8 A1.3,1.3 0 1 1 4.5,16.2 Z" },
            { "md-numlist", "M10,6.5 H20 M10,12 H20 M10,17.5 H20 M4,5.2 L5.3,4.5 V8.5 M3.8,10.6 C4.3,9.7 6.3,9.8 6.2,11 C6.1,12 3.9,12.7 3.9,13.6 H6.4 M3.9,15.6 H6.2 L4.9,17 C6.6,17 6.7,19.5 3.9,19" },
            { "md-task", "M3.5,4.5 H9 V10 H3.5 Z M4.9,7.3 L6.1,8.5 L8,6 M12,7.2 H20.5 M3.5,14 H9 V19.5 H3.5 Z M12,16.8 H20.5" },
            { "md-quote", "M5,5 V19 M9.5,7 H19 M9.5,12 H19 M9.5,17 H15.5" },
            { "md-link", "M10,14 L14,10 M10.8,8.2 L12.5,6.5 A3.2,3.2 0 0 1 17.5,11.5 L15.8,13.2 M13.2,15.8 L11.5,17.5 A3.2,3.2 0 0 1 6.5,12.5 L8.2,10.8" },
            { "md-code", "M8.5,7.5 L4,12 L8.5,16.5 M15.5,7.5 L20,12 L15.5,16.5 M13.5,5.5 L10.5,18.5" },
            { "md-codeblock", "M5.5,4.5 H18.5 A2,2 0 0 1 20.5,6.5 V17.5 A2,2 0 0 1 18.5,19.5 H5.5 A2,2 0 0 1 3.5,17.5 V6.5 A2,2 0 0 1 5.5,4.5 Z M9.5,9.5 L7,12 L9.5,14.5 M14.5,9.5 L17,12 L14.5,14.5" },
            { "md-table", "M5.5,4.5 H18.5 A2,2 0 0 1 20.5,6.5 V17.5 A2,2 0 0 1 18.5,19.5 H5.5 A2,2 0 0 1 3.5,17.5 V6.5 A2,2 0 0 1 5.5,4.5 Z M3.5,9.5 H20.5 M3.5,14.5 H20.5 M9.5,9.5 V19.5 M14.5,9.5 V19.5" },
            { "md-rule", "M3.5,12 H20.5 M6,7.5 H18 M6,16.5 H18" },
            { "filter", "M4,5.5 H20 L14,12.5 V18 L10,20 V12.5 Z" },
            { "more", "|M6,10.6 A1.4,1.4 0 1 1 6,13.4 A1.4,1.4 0 1 1 6,10.6 Z M12,10.6 A1.4,1.4 0 1 1 12,13.4 A1.4,1.4 0 1 1 12,10.6 Z M18,10.6 A1.4,1.4 0 1 1 18,13.4 A1.4,1.4 0 1 1 18,10.6 Z" },
        };
        static readonly Dictionary<string, Geometry[]> cache = new Dictionary<string, Geometry[]>();

        readonly string icon, tip;
        public event Action Click;
        public bool On;

        public DocIconButton(string icon, string tip) : this(icon, tip, 28) { }

        public DocIconButton(string icon, string tip, double size)
        {
            this.icon = icon;
            this.tip = tip;
            Width = Height = size;
            ToolTip = tip;
            AutomationProperties.SetName(this, tip);
            AcceptFocus();
        }

        protected override void Clicked(Point p) { Action h = Click; if (h != null) h(); }
        protected override bool EnterActivates { get { return true; } }
        protected override double RingRadius { get { return 6; } }
        internal override string AccessName { get { return tip; } }
        internal override AutomationControlType AccessType { get { return AutomationControlType.Button; } }

        static Geometry[] Shapes(string name)
        {
            Geometry[] g;
            if (cache.TryGetValue(name, out g)) return g;
            string d;
            if (!Icons.TryGetValue(name, out d)) return null;
            string[] parts = d.Split('|');
            g = new Geometry[2];
            for (int i = 0; i < parts.Length && i < 2; i++)
                if (parts[i].Length > 0) { g[i] = Geometry.Parse(parts[i]); g[i].Freeze(); }
            cache[name] = g;
            return g;
        }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double w = ActualWidth, h = ActualHeight;
            Rect r = new Rect(0, 0, w, h);
            Color bg = On ? Ds.WithAlpha(pal.Accent, pal.Dark ? 0.28 : 0.16) : Ds.WithAlpha(pal.ControlHover, Math.Min(1, Hot + Down * 0.6));
            Color fg = On ? pal.Link : pal.Label;
            if (pal.High) fg = HcPaint.Toggle(dc, r, 6, On, Hot, Down, IsEnabled);
            else if (On || Hot > 0 || Down > 0) dc.DrawRoundedRectangle(Ds.Brush(bg), null, r, 6, 6);
            double s = 17, x = (w - s) / 2, y = (h - s) / 2;
            if (icon.StartsWith("text:", StringComparison.Ordinal))
            {
                string letter = icon.Substring(5);
                Typeface face = letter == "I" ? new Typeface(Ds.Text, FontStyles.Italic, FontWeights.SemiBold, FontStretches.Normal)
                              : letter == "B" ? new Typeface(Ds.Text, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal) : Ds.Title;
                FormattedText t = Ink.Text(letter, face, 15, fg);
                if (letter == "S") t.SetTextDecorations(TextDecorations.Strikethrough);
                Ink.Center(dc, t, r);
                return;
            }
            Geometry[] g = Shapes(icon);
            if (g == null) { Glyph.Draw(dc, icon, x, y, s, fg, 1.6); return; }
            double k = s / 24.0;
            dc.PushTransform(new MatrixTransform(k, 0, 0, k, x, y));
            Brush b = Ds.Brush(fg);
            if (g[0] != null)
            {
                Pen p = new Pen(b, 1.6 / k);
                p.StartLineCap = p.EndLineCap = PenLineCap.Round;
                p.LineJoin = PenLineJoin.Round;
                dc.DrawGeometry(null, p, g[0]);
            }
            if (g[1] != null) dc.DrawGeometry(b, null, g[1]);
            dc.Pop();
        }

        // The same icons drawn elsewhere (menus are not given them, but tests render them).
        public static bool Known(string name) { return Icons.ContainsKey(name) || Glyph.Has(name); }
    }

    // A small on/off option, like "Aa" (match case) in the find bar.
    public sealed class ChipToggle : Drawn
    {
        readonly string label, tip;
        readonly bool underline;
        bool on;
        public event Action<bool> Toggled;

        public ChipToggle(string label, string tip, bool underline)
        {
            this.label = label;
            this.tip = tip;
            this.underline = underline;
            Height = 24;
            Width = Math.Ceiling(Ink.Text(label, Ds.Semibold, 12, Colors.Black).WidthIncludingTrailingWhitespace + 14);
            ToolTip = tip;
            AutomationProperties.SetName(this, tip);
            AcceptFocus();
        }

        public bool IsOn
        {
            get { return on; }
            set { if (on == value) return; on = value; InvalidateVisual(); }
        }

        protected override void Clicked(Point p)
        {
            on = !on;
            InvalidateVisual();
            Access.Toggled(this, on);
            Action<bool> h = Toggled;
            if (h != null) h(on);
        }

        protected override double RingRadius { get { return 6; } }
        internal override string AccessName { get { return tip; } }
        internal override AutomationControlType AccessType { get { return AutomationControlType.CheckBox; } }
        internal override bool? AccessChecked { get { return on; } }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            Rect r = new Rect(0, 0, ActualWidth, ActualHeight);
            // On: the accent tint behind text in the label colour (accent text on that tint stays under 4.5:1).
            Color ink = on ? pal.Label : pal.Label2;
            if (pal.High) ink = HcPaint.Toggle(dc, r, 6, on, Hot, Down, IsEnabled);
            else if (on) dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.30 : 0.16)), null, r, 6, 6);
            else if (Hot > 0 || Down > 0) dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.ControlHover, Math.Min(1, Hot + Down * 0.6))), null, r, 6, 6);
            FormattedText t = Ink.Text(label, Ds.Semibold, 12, ink);
            if (underline) t.SetTextDecorations(TextDecorations.Underline);
            Ink.Center(dc, t, r);
        }
    }

    // A rounded text field with a placeholder (and an optional icon), like the search box of the XML tree.
    public sealed class FieldBox : Border
    {
        public readonly TextBox Box;
        readonly TextBlock hint;
        public event Action<string> Changed;

        public FieldBox(Palette pal, string placeholder, string icon, double width)
        {
            Height = 28;
            Width = width;
            CornerRadius = new CornerRadius(7);
            Background = Ds.Brush(pal.High ? pal.Window : pal.Dark ? Ds.Argb(0.10, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0));
            Bad = false;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            if (icon != null) g.Children.Add(new GlyphView(icon, 14, pal.Label2, 1.6) { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            Box = new TextBox
            {
                BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label),
                FontSize = 13, Padding = new Thickness(icon != null ? 5 : 8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center,
                SelectionBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.4)), SpellCheck = { IsEnabled = false }
            };
            AutomationProperties.SetName(Box, placeholder);
            Grid.SetColumn(Box, 1);
            hint = new TextBlock { Text = placeholder, FontSize = 13, Foreground = Ds.Brush(pal.Label3), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(icon != null ? 7 : 10, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(hint, 1);
            g.Children.Add(Box);
            g.Children.Add(hint);
            Child = g;
            Box.TextChanged += delegate
            {
                hint.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (Changed != null) Changed(Box.Text);
            };
        }

        public string Text
        {
            get { return Box.Text; }
            set { Box.Text = value ?? ""; }
        }

        public bool Bad
        {
            set
            {
                Palette pal = Ds.Brushes;
                // A contrast theme keeps a border on the field and thickens it when the text is wrong.
                BorderBrush = value ? Ds.Brush(pal.High ? pal.Hairline : Ds.WithAlpha(pal.Red, 0.8)) : pal.High ? Ds.Brush(pal.Hairline) : null;
                BorderThickness = new Thickness(value ? (pal.High ? 2 : 1) : pal.High ? 1 : 0);
            }
        }
    }

    // A modal alert in the window's style: a title, a message and two or three buttons (Enter is the focused one or the
    // main one, Esc the cancel one). Tests answer through Answer instead of showing it.
    public sealed class DocAlert : Sheet
    {
        public static Func<string, string, string[], int> Answer;

        int result;
        readonly int cancel;

        // Returns the index of the button pressed, or cancel when it is closed.
        public static int Ask(Window owner, string title, string message, string[] buttons, int primary, int cancel)
        {
            Func<string, string, string[], int> a = Answer;
            if (a != null) return a(title, message, buttons);
            DocAlert w = new DocAlert(title, message, buttons, primary, cancel);
            try
            {
                if (owner != null && owner.IsVisible) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
                else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                w.ShowDialog();
            }
            catch (Exception ex) { ShotStack.Log("Aviso: " + ex.Message); return cancel; }
            return w.result;
        }

        // The same for any window of the app (a WinForms one passes its handle): with its own icon, centered on the
        // owner, or always on top when there is none, so it never hides behind the capture and recording overlays.
        internal static int Ask(IntPtr owner, string title, string message, string[] buttons, int primary, int cancel, string icon, Color c1, Color c2)
        {
            Func<string, string, string[], int> a = Answer;
            if (a != null) return a(title, message, buttons);
            DocAlert w = new DocAlert(title, message, buttons, primary, cancel);
            try
            {
                w.UseIcon(icon, c1, c2);
                if (owner != IntPtr.Zero)
                {
                    new System.Windows.Interop.WindowInteropHelper(w).Owner = owner;
                    w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    w.Topmost = true;
                }
                w.ShowDialog();
            }
            catch (Exception ex) { ShotStack.Log("Aviso: " + ex.Message); return cancel; }
            return w.result;
        }

        Grid grid;
        IconTile tile;

        void UseIcon(string icon, Color c1, Color c2)
        {
            int at = grid.Children.IndexOf(tile);
            if (at < 0) return;
            IconTile t = new IconTile(icon, 40, c1, c2) { VerticalAlignment = VerticalAlignment.Top, Margin = tile.Margin };
            Grid.SetRowSpan(t, 2);
            grid.Children.RemoveAt(at);
            grid.Children.Insert(at, t);
            tile = t;
        }

        DocAlert(string title, string message, string[] buttons, int primary, int cancel)
        {
            this.cancel = cancel;
            result = cancel;
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ShowInTaskbar = false;
            Icon = AppIcon;
            // An alert has no minimize or close of its own: Esc and its buttons answer it.
            foreach (UIElement e in Root.Children) if (e is StackPanel && Panel.GetZIndex(e) == 100) e.Visibility = Visibility.Collapsed;
            Palette pal = Ds.Brushes;
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            DragAnywhere();

            Grid g = new Grid { Margin = new Thickness(24, 22, 22, 20) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid = g;
            tile = new IconTile("markdown", 40, Ds.Rgb(140, 150, 255), Ds.Rgb(88, 86, 214)) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 16, 0) };
            Grid.SetRowSpan(tile, 2);
            g.Children.Add(tile);
            TextBlock head = new TextBlock { Text = title, FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 15.5, Foreground = Ds.Brush(pal.Label), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(head, 1);
            g.Children.Add(head);
            TextBlock body = new TextBlock { Text = message, FontSize = 13, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible };
            Grid.SetColumn(body, 1);
            Grid.SetRow(body, 1);
            g.Children.Add(body);

            // In Windows' order, as its own dialogs: on the right, the main choice first, the others, and Cancel last.
            Grid row = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            Grid.SetRow(row, 2);
            Grid.SetColumnSpan(row, 2);
            MacButton main = null;
            MacButton[] all = new MacButton[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int k = i;
                bool isMain = i == primary;
                MacButton b = new MacButton(buttons[i], isMain ? ButtonKind.Primary : ButtonKind.Secondary, null, null, 30) { MinWidth = 92 };
                all[i] = b;
                b.Click += delegate { result = k; Close(); };
                if (isMain) main = b;
            }
            List<int> order = new List<int>();
            if (primary >= 0 && primary < all.Length) order.Add(primary);
            for (int i = 0; i < all.Length; i++) if (i != primary && i != cancel) order.Add(i);
            if (cancel >= 0 && cancel < all.Length && cancel != primary) order.Add(cancel);
            // One row; when a language makes the buttons longer than the room they have, the rest go on a second row
            // (still on the right, in the same order) instead of running off the edge.
            double room = Width - g.Margin.Left - g.Margin.Right, used = 0;
            StackPanel rows = new StackPanel();
            StackPanel right = null;
            for (int n = 0; n < order.Count; n++)
            {
                MacButton b = all[order[n]];
                double w = Math.Max(b.Width, b.MinWidth);
                if (right == null || used + 8 + w > room)
                {
                    right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, right == null ? 0 : 8, 0, 0) };
                    rows.Children.Add(right);
                    used = 0;
                }
                b.Margin = new Thickness(used == 0 ? 0 : 8, 0, 0, 0);
                used += (used == 0 ? 0 : 8) + w;
                right.Children.Add(b);
            }
            row.Children.Add(rows);
            g.Children.Add(row);
            Root.Children.Add(g);
            KeepCaptionsOnTop();

            PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; result = this.cancel; Close(); }
                else if (e.Key == Key.Enter)
                {
                    // Enter answers with the focused button (after Tab), otherwise with the main one.
                    int pick = primary;
                    IInputElement f = FocusManager.GetFocusedElement(this);
                    for (int i = 0; i < all.Length; i++) if (all[i] == f || all[i].IsKeyboardFocusWithin) pick = i;
                    if (pick < 0) return;
                    e.Handled = true;
                    result = pick;
                    Close();
                }
            };
            Loaded += delegate { if (main != null) main.Focus(); };
            Access.Dialog(this, message);
        }
    }
}
