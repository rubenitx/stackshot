// Stackshot - Recording section: clicks and keys drawn into the video.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // Both off by default; nothing watches the mouse or the keyboard unless one is on and a recording is running.
        void RecInput()
        {
            Palette pal = Ds.Brushes;
            Grid row = RecRow(1);
            TextBlock sub;
            StackPanel p = RecPanel(row, 0, "cursor", pal.Accent, I18n.T("Clics y teclas", "Clicks & keys"),
                                    I18n.T("Se dibujan en el v\u00EDdeo mientras grabas, para que quien lo vea siga lo que haces. No salen durante las pausas.", "Drawn onto the video while recording so viewers can follow your actions. Not shown during pauses."), out sub);
            p.Children.Add(RecInputTile("cursor", W(Mac.Teal), W(Mac.Blue), I18n.T("Mostrar los clics", "Show clicks"),
                                        I18n.T("Un c\u00EDrculo donde haces clic: azul con el bot\u00F3n izquierdo y naranja con el derecho.", "A circle where you click: blue for left click and orange for right click."),
                                        settings.RecordClicks, delegate(bool v) { settings.RecordClicks = v; }, 14));
            p.Children.Add(RecInputTile("keyboard", W(Mac.Purple), W(Mac.Indigo), I18n.T("Mostrar las teclas pulsadas", "Show keystrokes pressed"),
                                        I18n.T("Los atajos (Ctrl + C, Alt + Tab, Win + May\u00FAs + S\u2026) salen abajo, en el centro. Lo que escribes nunca aparece, as\u00ED que tus contrase\u00F1as tampoco.", "Shortcuts (Ctrl+C, Alt+Tab, Win+Shift+S\u2026) appear bottom-center. Typed text never appears, keeping your passwords safe."),
                                        settings.RecordKeys, delegate(bool v) { settings.RecordKeys = v; }, 8));
        }

        // One option: its icon, what it does and its switch, on a tile like the others of this page.
        FrameworkElement RecInputTile(string icon, Color c1, Color c2, string title, string line, bool on, Action<bool> set, double top)
        {
            Palette pal = Ds.Brushes;
            Border tile = new Border { CornerRadius = new CornerRadius(12), Background = Ds.Brush(RecTile(false)), Padding = new Thickness(13, 11, 13, 11), Margin = new Thickness(0, top, 0, 0) };
            Grid inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.ColumnDefinitions.Add(new ColumnDefinition());
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.Children.Add(new IconTile(icon, 28, c1, c2) { VerticalAlignment = VerticalAlignment.Center });
            StackPanel words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 12, 0) };
            words.Children.Add(Label(title, Ds.Semibold, 13, pal.Label));
            TextBlock l = Paragraph(line, 12, pal.Label2);
            l.Margin = new Thickness(0, 2, 0, 0);
            words.Children.Add(l);
            Grid.SetColumn(words, 1);
            inner.Children.Add(words);
            MacSwitch sw = new MacSwitch(on) { VerticalAlignment = VerticalAlignment.Center };
            Access.Describe(sw, title, line);
            sw.Toggled += delegate(bool v) { set(v); Changed(); };
            Grid.SetColumn(sw, 2);
            inner.Children.Add(sw);
            tile.Child = inner;
            SearchMark(tile, title, line, icon, c1, c2);
            return tile;
        }
    }
}
