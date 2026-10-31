// Stackshot - Ajustes for the saved captures (file name template, dated subfolders) and the capture modes Home lists.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // Home's mode lists show the capture modes (indices into Settings.Actions). Repeating the last area is a second go
        // at the area mode, and so is the timed one: they live in the tray and in Atajos.
        static readonly int[] HomeModeIndex = HomeModes();

        static int[] HomeModes()
        {
            List<int> l = new List<int>();
            for (int i = 0; i < Settings.Actions.Length; i++)
                if (Settings.Actions[i] != "repeat" && Settings.Actions[i] != "delayed") l.Add(i);
            return l.ToArray();
        }

        static string[] SubfolderNames
        {
            get
            {
                return I18n.IsSpanish
                    ? new string[] { "No", "Por mes", "Por a\u00F1o y mes" }
                    : new string[] { "No", "By month", "By year and month" };
            }
        }

        SegRow SubfolderRow()
        {
            return new SegRow(I18n.T("Subcarpetas", "Subfolders"), null, "folder", Mac.Indigo, Mac.Purple, SubfolderNames,
                              delegate { return settings.SaveSubfolder; }, delegate(int i) { settings.SaveSubfolder = i; })
                { SubNow = delegate { return SubfolderNote(settings.SaveSubfolder, DateTime.Now); } };
        }

        internal static string SubfolderNote(int mode, DateTime now)
        {
            string year = now.ToString("yyyy", CultureInfo.InvariantCulture), month = now.ToString("MM", CultureInfo.InvariantCulture);
            switch (mode)
            {
                case 1: return I18n.Fmt("Lo que guardas va a una carpeta por mes, como {0}-{1}.", "Saved captures go to a monthly folder, like {0}-{1}.", year, month);
                case 2: return I18n.Fmt("Una carpeta por a\u00F1o y, dentro, una por mes, como {0}\\{1}.", "A folder per year and monthly subfolder inside, like {0}\\{1}.", year, month);
                default: return I18n.T("Todo va directamente a la carpeta elegida.", "All files go directly into the chosen folder.");
            }
        }

        // What each token stands for, in the order of NameTemplate.Tokens.
        static string[] TokenTips(DateTime now)
        {
            return I18n.IsSpanish ? new string[]
            {
                "El tipo de captura: Captura, Pantalla, el programa de la ventana, Desplazamiento\u2026",
                "La fecha, como " + now.ToString("yyyy-MM-dd"),
                "La hora, como " + now.ToString("HH.mm.ss"),
                "El programa que ten\u00EDas delante al capturar, como chrome o EXCEL",
                "El t\u00EDtulo de esa ventana",
                "El a\u00F1o, como " + now.ToString("yyyy"),
                "El mes, como " + now.ToString("MM"),
                "El d\u00EDa, como " + now.ToString("dd"),
                "Un n\u00FAmero que no se repite: 1, 2, 3\u2026 (con {fecha}, empieza cada d\u00EDa)"
            } : new string[]
            {
                "Capture type: Capture, Screen, active app name, Scrolling\u2026",
                "Date, like " + now.ToString("yyyy-MM-dd"),
                "Time, like " + now.ToString("HH.mm.ss"),
                "Active program when captured, like chrome or EXCEL",
                "Title of that window",
                "Year, like " + now.ToString("yyyy"),
                "Month, like " + now.ToString("MM"),
                "Day, like " + now.ToString("dd"),
                "Sequential unique number: 1, 2, 3\u2026 (with {date}, resets daily)"
            };
        }

        // Under the template field: what is odd about it (warn) or, when nothing is, what it applies to.
        internal static string NameHint(string template, out bool warn)
        {
            warn = true;
            string t = (template ?? "").Trim();
            List<string> unknown = NameTemplate.Unknown(t);
            if (unknown.Count > 0)
                return I18n.IsSpanish
                    ? string.Join(" ", unknown.ToArray()) + (unknown.Count == 1 ? " no es ninguno de los de arriba: se queda tal cual." : " no son de los de arriba: se quedan tal cual.")
                    : string.Join(" ", unknown.ToArray()) + (unknown.Count == 1 ? " is not one of the tokens above: kept as-is." : " are not tokens above: kept as-is.");
            if (t.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                return I18n.T("\\ / : * ? \" < > | no valen en un nombre de archivo: se cambian por _.", "\\ / : * ? \" < > | are invalid in file names: replaced with _.");
            warn = false;
            if (t.Length == 0) return I18n.T("Si lo dejas vac\u00EDo, se usa el nombre de siempre.", "If left empty, default file name is used.");
            if (!NameTemplate.IsDefault(t) && !NameTemplate.Uses(t, "{hora}") && !NameTemplate.Uses(t, "{n}"))
                return I18n.T("Sin {hora} ni {n}, las capturas que se llamen igual llevar\u00E1n (2), (3)\u2026 al final.", "Without {time} or {n}, duplicate names will append (2), (3)\u2026 at the end.");
            return I18n.T("Para las im\u00E1genes; los v\u00EDdeos y los GIF conservan su nombre.", "For images; videos and GIFs retain their own names.");
        }

        internal static string NamePreview(string template)
        {
            return (I18n.IsSpanish ? "Ejemplo: " : "Example: ") + NameTemplate.Example(template, DateTime.Now) + ".png";
        }

        // The file name of the captures: the template in a field on the right, an example of the name it gives under the
        // title, and below the tokens (a click puts one where the cursor is), a link back to the default and a hint. What
        // is typed applies at once; it is written to the settings file when the field is left.
        public class NameTemplateRow : Row
        {
            TextBox box;
            Border frame;
            TextBlock hint, reset;
            string committed;
            bool placed;    // the user has been in the field: tokens go where its cursor is

            public NameTemplateRow() : base(I18n.T("Nombre de los archivos", "File names"), "", "edit", Mac.Orange, Mac.Pink)
            {
                Unloaded += delegate { Commit(); };
            }

            public override void Build()
            {
                RowDefinitions.Clear();
                committed = W.settings.FileNameTemplate;
                SubNow = delegate { return NamePreview(box != null ? box.Text : W.settings.FileNameTemplate); };
                base.Build();
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Palette pal = Ds.Brushes;
                StackPanel below = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                Grid.SetRow(below, 1);
                Grid.SetColumn(below, 1);
                Grid.SetColumnSpan(below, 2);
                WrapPanel chips = new WrapPanel();
                string[] tips = TokenTips(DateTime.Now);
                string[] tokens = NameTemplate.Tokens;
                for (int i = 0; i < tokens.Length; i++) chips.Children.Add(Chip(tokens[i], tips[i]));
                reset = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 4), FontFamily = Ds.Text, FontSize = 12 };
                reset.Inlines.Add(InlineLink(I18n.T("Restablecer", "Reset"), delegate { SetText(NameTemplate.DefaultShown); Commit(); }));
                reset.ToolTip = I18n.Fmt("Volver a {0}", "Reset to {0}", NameTemplate.DefaultShown);
                chips.Children.Add(reset);
                below.Children.Add(chips);
                hint = Label("", Ds.Regular, 11.5, pal.Label2, new Thickness(0, 2, 0, 0));
                hint.TextWrapping = TextWrapping.Wrap;
                below.Children.Add(hint);
                Children.Add(below);
                Refresh();
            }

            protected override FrameworkElement Control()
            {
                Palette pal = Ds.Brushes;
                frame = new Border
                {
                    Width = 236, Height = 32, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 8, 0, 8), Cursor = Cursors.IBeam,
                    BorderThickness = new Thickness(1), BorderBrush = Ds.Brush(pal.Hairline), Background = Ds.Brush(pal.Control)
                };
                box = new TextBox
                {
                    Text = NameTemplate.IsDefault(W.settings.FileNameTemplate) ? NameTemplate.DefaultShown : W.settings.FileNameTemplate, MaxLength = 200, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Padding = new Thickness(0), Margin = new Thickness(9, 0, 9, 1), FontFamily = Ds.Text, FontSize = 12.5,
                    Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(pal.Accent),
                    VerticalContentAlignment = VerticalAlignment.Center, Template = DockFieldTemplate()
                };
                frame.Child = box;
                System.Windows.Automation.AutomationProperties.SetName(box, Title);
                // A one-line box would keep the wheel: the page goes on scrolling over it.
                box.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e)
                {
                    e.Handled = true;
                    MouseWheelEventArgs up = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                    up.RoutedEvent = UIElement.MouseWheelEvent;
                    up.Source = frame;
                    frame.RaiseEvent(up);
                };
                frame.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (!box.IsKeyboardFocusWithin) { box.Focus(); box.CaretIndex = box.Text.Length; e.Handled = true; } };
                box.TextChanged += delegate { Typed(); };
                box.GotKeyboardFocus += delegate
                {
                    placed = true;
                    frame.BorderBrush = Ds.Brush(Ds.WithAlpha(Ds.Brushes.Accent, 0.85));
                    frame.BorderThickness = new Thickness(1.5);
                };
                box.LostKeyboardFocus += delegate
                {
                    frame.BorderBrush = Ds.Brush(Ds.Brushes.Hairline);
                    frame.BorderThickness = new Thickness(1);
                    Commit();
                };
                // Enter keeps what is typed, Esc goes back to what was kept.
                box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter) { Commit(); Keyboard.ClearFocus(); e.Handled = true; }
                    else if (e.Key == Key.Escape)
                    {
                        // What was typed may already be on disk (each capture saves the settings): written back too.
                        bool typed = box.Text != committed;
                        SetText(committed);
                        box.SelectAll();
                        if (typed) W.Changed();
                        e.Handled = true;
                    }
                };
                return frame;
            }

            // A token as a small capsule; a click writes it where the cursor is, with a space before it if needed.
            FrameworkElement Chip(string token, string tip)
            {
                Palette pal = Ds.Brushes;
                Border b = new TapBorder
                {
                    Label = token, Do = delegate { Insert(token); },
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(0, 0, 6, 6),
                    Background = Ds.Brush(pal.Control), Cursor = Cursors.Hand, ToolTip = tip, BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = HcPaint.Edge
                };
                ToolTipService.SetInitialShowDelay(b, 500);
                b.Child = Label(token, Ds.Regular, 11.5, pal.Label2);
                b.MouseEnter += delegate { b.Background = Ds.Brush(pal.ControlHover); };
                b.MouseLeave += delegate { b.Background = Ds.Brush(pal.Control); };
                b.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; };
                b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    Insert(token);
                };
                return b;
            }

            // Where the cursor is (replacing a selection) once the field has been used; at the end before that.
            void Insert(string token)
            {
                if (box == null) return;
                string t = box.Text;
                int at = t.Length, cut = 0;
                if (placed)
                {
                    at = Math.Max(0, Math.Min(box.SelectionStart, t.Length));
                    cut = Math.Min(box.SelectionLength, t.Length - at);
                }
                t = t.Remove(at, cut);
                bool before = at > 0 && !char.IsWhiteSpace(t[at - 1]), after = at < t.Length && !char.IsWhiteSpace(t[at]);
                string add = (before ? " " : "") + token + (after ? " " : "");
                if (t.Length + add.Length > box.MaxLength) return;
                box.Text = t.Insert(at, add);
                placed = true;
                box.Focus();
                box.CaretIndex = at + add.Length - (after ? 1 : 0);
            }

            void SetText(string t)
            {
                if (box != null && box.Text != t) box.Text = t;
            }

            // What is typed applies at once (an empty field means the default).
            void Typed()
            {
                string t = box.Text.Trim();
                W.settings.FileNameTemplate = t.Length > 0 ? t : NameTemplate.DefaultShown;
                if (SubText != null) SubText.Text = NamePreview(box.Text);
                Refresh();
            }

            void Refresh()
            {
                string t = box != null ? box.Text : W.settings.FileNameTemplate;
                if (reset != null) reset.Visibility = NameTemplate.IsDefault(t) ? Visibility.Collapsed : Visibility.Visible;
                if (hint == null) return;
                bool warn;
                hint.Text = NameHint(t, out warn);
                hint.Foreground = Ds.Brush(warn ? Ds.Brushes.Warn : Ds.Brushes.Label2);
            }

            // Written to the settings file once, when the field is left (an empty one becomes the default).
            void Commit()
            {
                if (box == null || W == null) return;
                string t = box.Text.Trim();
                if (t.Length == 0) t = NameTemplate.DefaultShown;
                SetText(t);
                // The default in the other language is the same default: nothing to write.
                if (NameTemplate.IsDefault(t) && NameTemplate.IsDefault(committed)) { W.settings.FileNameTemplate = committed; return; }
                W.settings.FileNameTemplate = t;
                if (t == committed) return;
                committed = t;
                W.Changed();
            }
        }
    }
}
