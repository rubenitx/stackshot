// Stackshot - Pop-up menu for WPF windows, in the style of macOS menus.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Stackshot
{
    public class MenuEntry
    {
        public string Label, Detail, Icon;   // Detail: a second, dimmer line (a device's full name, a hint)
        public bool Checked, Enabled = true, Separator, Header;
        public Action Do;

        public static MenuEntry Item(string label, string icon, bool check, Action act)
        {
            MenuEntry e = new MenuEntry();
            e.Label = label; e.Icon = icon; e.Checked = check; e.Do = act;
            return e;
        }

        public static MenuEntry Line() { MenuEntry e = new MenuEntry(); e.Separator = true; return e; }

        public static MenuEntry Title(string text) { MenuEntry e = new MenuEntry(); e.Label = text; e.Header = true; e.Enabled = false; return e; }
    }

    // A rounded menu with a soft shadow that opens under (or over) an element, left edges aligned, fades in, closes on a
    // click outside or Esc, and runs the chosen entry after closing. Arrow keys, Home and End move the highlight; Enter
    // or Space choose.
    public static class MacMenu
    {
        public static Popup Show(FrameworkElement anchor, IList<MenuEntry> entries)
        {
            return Show(anchor, entries, 0);
        }

        // Keeps a popup inside the monitor's working area: under the anchor when it fits, over it otherwise.
        static void Place(Popup pop, FrameworkElement anchor, double shadowX)
        {
            pop.Placement = PlacementMode.Custom;
            pop.CustomPopupPlacementCallback = delegate(Size popup, Size target, Point offset)
            {
                double k = 1;
                System.Windows.Forms.Screen scr = System.Windows.Forms.Screen.PrimaryScreen;
                Point t = new Point(0, 0);
                try
                {
                    k = VisualTreeHelper.GetDpi(anchor).DpiScaleX;
                    Point px = anchor.PointToScreen(new Point(0, 0));
                    scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)px.X, (int)px.Y));
                    t = new Point(px.X / k, px.Y / k);
                }
                catch (Exception) { }
                System.Drawing.Rectangle wa = scr.WorkingArea;
                double l = wa.Left / k, r = wa.Right / k, top = wa.Top / k, bot = wa.Bottom / k;
                double x = -shadowX, y = target.Height + 4;
                if (t.Y + y + popup.Height > bot) y = -popup.Height - 4 + 14;
                if (t.Y + y < top) y = top - t.Y;
                if (t.Y + y + popup.Height > bot) y = bot - popup.Height - t.Y;
                if (t.X + x + popup.Width > r) x = r - popup.Width - t.X;
                if (t.X + x < l) x = l - t.X;
                return new CustomPopupPlacement[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None) };
            };
        }

        static double WorkHeight(FrameworkElement anchor)
        {
            try
            {
                double k = VisualTreeHelper.GetDpi(anchor).DpiScaleY;
                Point p = anchor.PointToScreen(new Point(0, 0));
                return System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)p.X, (int)p.Y)).WorkingArea.Height / k;
            }
            catch (Exception) { return 600; }
        }

        // Compact popover of icon tiles, several per row; a separator entry starts a new group. The detail is the tooltip.
        public static Popup ShowGrid(FrameworkElement anchor, string title, IList<MenuEntry> entries, int columns)
        {
            Palette pal = Ds.Brushes;
            Popup pop = new Popup { AllowsTransparency = true, StaysOpen = false, PlacementTarget = anchor, PopupAnimation = PopupAnimation.None };
            Place(pop, anchor, 14);
            StackPanel list = new StackPanel { Margin = new Thickness(8, 6, 8, 8) };
            if (!string.IsNullOrEmpty(title)) list.Children.Add(Text(title, Ds.Semibold, 11.5, pal.Label3, new Thickness(4, 2, 4, 6)));
            UniformGrid grid = null;
            foreach (MenuEntry e in entries)
            {
                if (e == null) continue;
                if (e.Separator) { grid = null; continue; }
                if (e.Header)
                {
                    grid = null;
                    list.Children.Add(Text(e.Label, Ds.Semibold, 11.5, pal.Label3, new Thickness(4, 2, 4, 6)));
                    continue;
                }
                if (grid == null)
                {
                    grid = new UniformGrid { Columns = columns, Margin = new Thickness(0, list.Children.Count > 0 ? 4 : 0, 0, 0) };
                    list.Children.Add(grid);
                }
                MenuEntry en = e;
                Border b = new Border { CornerRadius = new CornerRadius(8), Height = 58, Width = 84, Margin = new Thickness(1), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                GlyphView ic = new GlyphView(en.Icon ?? "sparkle", 20, pal.Label2, 1.6);
                ic.HorizontalAlignment = HorizontalAlignment.Center;
                s.Children.Add(ic);
                TextBlock tx = Text(en.Label, Ds.Regular, 11.5, pal.Label, new Thickness(0, 5, 0, 0));
                tx.HorizontalAlignment = HorizontalAlignment.Center;
                tx.TextTrimming = TextTrimming.CharacterEllipsis;
                tx.MaxWidth = 78;
                s.Children.Add(tx);
                b.Child = s;
                if (en.Detail != null) b.ToolTip = en.Detail;
                b.MouseEnter += delegate { b.Background = Ds.Brush(pal.Accent); ic.Color = Colors.White; tx.Foreground = Brushes.White; };
                b.MouseLeave += delegate { b.Background = Brushes.Transparent; ic.Color = pal.Label2; tx.Foreground = Ds.Brush(pal.Label); };
                b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs a)
                {
                    a.Handled = true;
                    pop.IsOpen = false;
                    if (en.Do != null) pop.Dispatcher.BeginInvoke(en.Do);
                };
                grid.Children.Add(b);
            }
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list, MaxHeight = Math.Max(200, WorkHeight(anchor) - 80) };
            Border card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Ds.Brush(pal.Dark ? Ds.Rgb(44, 44, 47) : Ds.Rgb(250, 250, 251)),
                BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = sv
            };
            Grid root = new Grid { Margin = new Thickness(14, 4, 14, 18), UseLayoutRounding = true };
            root.Children.Add(new SoftShadow(12, 14, 6, pal.Dark ? 0.5 : 0.18));
            root.Children.Add(card);
            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);
            root.PreviewKeyDown += delegate(object o, KeyEventArgs k) { if (k.Key == Key.Escape) { pop.IsOpen = false; k.Handled = true; } };
            pop.Child = root;
            root.Opacity = 0;
            pop.Opened += delegate { root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))); };
            pop.IsOpen = true;
            root.Focusable = true;
            root.Focus();
            return pop;
        }

        public static Popup Show(FrameworkElement anchor, IList<MenuEntry> entries, double minWidth)
        {
            Palette pal = Ds.Brushes;
            Popup pop = new Popup();
            pop.AllowsTransparency = true;
            pop.StaysOpen = false;
            pop.PlacementTarget = anchor;
            Place(pop, anchor, 14);
            pop.PopupAnimation = PopupAnimation.None;

            StackPanel list = new StackPanel { Margin = new Thickness(5) };
            Border card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ds.Brush(pal.Dark ? Ds.Rgb(44, 44, 47) : Ds.Rgb(250, 250, 251)),
                BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list, MaxHeight = Math.Max(200, WorkHeight(anchor) - 80) },
                MinWidth = Math.Max(minWidth, 200)
            };
            Grid root = new Grid { Margin = new Thickness(14, 4, 14, 18), UseLayoutRounding = true };
            root.Children.Add(new SoftShadow(10, 14, 6, pal.Dark ? 0.5 : 0.18));
            root.Children.Add(card);
            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);

            Rows rows = new Rows(pop);
            foreach (MenuEntry e in entries)
            {
                if (e == null) continue;
                if (e.Separator) { list.Children.Add(new Border { Height = 1, Margin = new Thickness(8, 4, 8, 4), Background = Ds.Brush(pal.Separator) }); continue; }
                if (e.Header)
                {
                    list.Children.Add(Text(e.Label, Ds.Semibold, 11.5, pal.Label3, new Thickness(10, 6, 10, 3)));
                    continue;
                }
                list.Children.Add(rows.Add(e));
            }
            pop.Child = root;
            root.Opacity = 0;
            TranslateTransform tt = new TranslateTransform(0, -4);
            root.RenderTransform = tt;
            pop.Opened += delegate
            {
                root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            };
            root.PreviewKeyDown += delegate(object o, KeyEventArgs k)
            {
                switch (k.Key)
                {
                    case Key.Escape: pop.IsOpen = false; break;
                    case Key.Down: rows.Move(1); break;
                    case Key.Up: rows.Move(-1); break;
                    case Key.Home: rows.Jump(true); break;
                    case Key.End: rows.Jump(false); break;
                    case Key.Enter:
                    case Key.Space: rows.Choose(); break;
                    case Key.Tab: break;
                    default: return;
                }
                k.Handled = true;
            };
            // The menu takes the keyboard while open and gives it back to where it was when it closes, unless something
            // else took it meanwhile.
            IInputElement before = Keyboard.FocusedElement;
            pop.Closed += delegate
            {
                UIElement back = before as UIElement;
                IInputElement now = Keyboard.FocusedElement;
                if (back == null || back == now || !back.IsVisible) return;
                Window win = Window.GetWindow(back);
                if (win != null && !win.IsActive) return;   // closed because the user switched away: leave it be
                Visual v = now as Visual;
                if (now == null || now is Window || (v != null && root.IsAncestorOf(v))) back.Focus();
            };
            pop.IsOpen = true;
            root.Focusable = true;
            root.FocusVisualStyle = null;
            root.Focus();
            return pop;
        }

        // The selectable rows and the one highlighted, by the mouse or the keyboard.
        sealed class Rows
        {
            readonly Popup pop;
            readonly List<MenuEntry> entries = new List<MenuEntry>();
            readonly List<Action<bool>> paint = new List<Action<bool>>();
            int hot = -1;

            public Rows(Popup pop) { this.pop = pop; }

            public FrameworkElement Add(MenuEntry e)
            {
                Palette pal = Ds.Brushes;
                Border b = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, e.Detail != null ? 5 : 6, 12, 6), Background = Brushes.Transparent };
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                Color ink = e.Enabled ? pal.Label : pal.Label3;
                GlyphView check = null, icon = null;
                if (e.Checked) { check = new GlyphView("check", 14, pal.Accent, 2); g.Children.Add(check); }
                else if (e.Icon != null) { icon = new GlyphView(e.Icon, 15, e.Enabled ? pal.Label2 : pal.Label3, 1.6); g.Children.Add(icon); }
                StackPanel t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(t, 1);
                TextBlock main = Text(e.Label, e.Checked ? Ds.Semibold : Ds.Regular, 13, ink, new Thickness(4, 0, 0, 0));
                main.TextTrimming = TextTrimming.CharacterEllipsis;
                main.MaxWidth = 360;
                t.Children.Add(main);
                TextBlock detail = null;
                if (e.Detail != null)
                {
                    detail = Text(e.Detail, Ds.Regular, 11.5, pal.Label2, new Thickness(4, 1, 0, 0));
                    detail.TextTrimming = TextTrimming.CharacterEllipsis;
                    detail.MaxWidth = 360;
                    t.Children.Add(detail);
                }
                g.Children.Add(t);
                b.Child = g;
                if (!e.Enabled) return b;
                int index = entries.Count;
                entries.Add(e);
                paint.Add(delegate(bool on)
                {
                    b.Background = on ? Ds.Brush(pal.Accent) : Brushes.Transparent;
                    main.Foreground = on ? Brushes.White : Ds.Brush(ink);
                    if (detail != null) detail.Foreground = Ds.Brush(on ? Ds.Argb(0.8, 255, 255, 255) : pal.Label2);
                    if (check != null) check.Color = on ? Colors.White : pal.Accent;
                    if (icon != null) icon.Color = on ? Colors.White : pal.Label2;
                });
                b.Cursor = Cursors.Hand;
                b.MouseEnter += delegate { Set(index); };
                b.MouseLeave += delegate { if (hot == index) Set(-1); };
                b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs a)
                {
                    a.Handled = true;
                    Run(index);
                };
                return b;
            }

            void Set(int i)
            {
                if (i == hot) return;
                if (hot >= 0) paint[hot](false);
                hot = i;
                if (hot >= 0) paint[hot](true);
            }

            public void Move(int step)
            {
                if (entries.Count == 0) return;
                Set(hot < 0 ? (step > 0 ? 0 : entries.Count - 1) : (hot + step + entries.Count) % entries.Count);
            }

            public void Jump(bool first) { if (entries.Count > 0) Set(first ? 0 : entries.Count - 1); }

            public void Choose() { if (hot >= 0) Run(hot); }

            void Run(int i)
            {
                pop.IsOpen = false;
                Action act = entries[i].Do;
                if (act != null) pop.Dispatcher.BeginInvoke(act);
            }
        }

        static TextBlock Text(string s, Typeface face, double size, Color c, Thickness m)
        {
            return new TextBlock { Text = s, FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Foreground = Ds.Brush(c), Margin = m };
        }
    }
}
