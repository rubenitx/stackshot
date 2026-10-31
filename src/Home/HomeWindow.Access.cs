// Stackshot - Accessibility of the main window: names and actions for screen readers and the keyboard on the surfaces the
// pages build themselves (cards, links, settings rows).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // What a hotkey field says to a reader while it waits for the combination.
        static string KeysPrompt { get { return I18n.T("Pulsa la combinaci\u00F3n que quieras; Supr la quita y Esc cancela", "Press the combination you want; Del removes it and Esc cancels"); } }

        // The sidebar's card: a button that goes home.
        void AccessSideCard(Border card, Action act)
        {
            ITap t = card as ITap;
            if (t == null) return;
            t.State.Label = I18n.T("Ir a Inicio", "Go to Home");
            t.State.Help = settings.MascotName;
            Access.Clickable(card, act);
        }

        // A card to choose among others (the recording quality): a radio button with its name and its line.
        static void AccessCard(Grid root, string name, string line, Func<bool> chosen)
        {
            ITap t = root as ITap;
            if (t == null) return;
            t.State.Label = name;
            t.State.Help = line;
            t.State.On = chosen;
        }

        // A widget of a page: a group named by its title.
        static void AccessGroup(FrameworkElement g, string title)
        {
            ITap t = g as ITap;
            if (t != null && !t.State.Interactive) t.State.Label = title;
        }

        // A pop-up button for a device: what it chooses; the device it shows is its help (its tooltip).
        static void AccessPill(Border pill, string label)
        {
            ITap t = pill as ITap;
            if (t != null) t.State.Label = label;
        }

        // A settings row names its control with its title; the line under the title is the control's help.
        static void AccessRow(Row r, string sub)
        {
            foreach (UIElement c in r.Children)
            {
                FrameworkElement fe = c as FrameworkElement;
                if (fe != null && Grid.GetColumn(fe) == 2) Access.Describe(fe, r.Title, sub);
            }
        }

        static TapText LinkLabel(string text, Typeface face, double size, Color color, Thickness margin, Action act)
        {
            TapText t = LinkLabel(text, face, size, color, act);
            t.Margin = margin;
            return t;
        }

        // A label that is a link: the same text as Label, reached with Tab and read as a link.
        static TapText LinkLabel(string text, Typeface face, double size, Color color, Action act)
        {
            TapText t = new TapText();
            t.Text = text;
            t.FontFamily = face.FontFamily;
            t.FontWeight = face.Weight;
            t.FontSize = size;
            t.Foreground = Ds.Brush(color);
            if (size <= 15) TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Do = act;
            return t;
        }

        // Links inside a paragraph (notes under a group): reached with Tab (Enter follows them), with the ring only from the
        // keyboard. A click still leaves the focus where it was, as before.
        static void AccessLink(System.Windows.Documents.Hyperlink h)
        {
            h.Focusable = true;
            h.FocusVisualStyle = Access.FocusRing;
            h.PreviewMouseLeftButtonDown += delegate
            {
                h.Focusable = false;
                h.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, (Action)delegate { h.Focusable = true; });
            };
        }
    }
}
