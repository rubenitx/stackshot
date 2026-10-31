// Stackshot - The mascot page for screen readers and the keyboard: the dock's tabs and tiles (one stop for Tab, arrows
// through the tiles, the try-on and the label follow the keyboard as they follow the mouse), the stage's pills and name.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // A pill with a switch inside is the switch for readers and the keyboard: one stop, Space flips it.
        static void AccessSwitchPill(Border pill, MacSwitch sw, string label)
        {
            ITap t = pill as ITap;
            if (t == null) return;
            t.State.Role = Access.Check;
            t.State.Label = label;
            t.State.On = delegate { return sw.IsOn; };
            Action flip = t.State.Do;
            if (flip != null) t.State.Do = delegate { flip(); Access.Toggled(pill, sw.IsOn); };
            sw.Focusable = false;
            sw.AccessHidden = true;
        }

        // A name shown as text that turns into a field when clicked: Space or Enter open the field too.
        static void AccessNameEdit(Border view, TextBox box, Action begin)
        {
            AutomationProperties.SetName(box, I18n.T("Nombre de la mascota", "Mascot name"));
            Access.Clickable(view, begin);
        }

        // A link that only means something while its message shows.
        static void AccessShown(UIElement link, bool shown)
        {
            if (link != null) link.IsEnabled = shown;
        }

        // A dock tile: its name and what it brings, worn or not, its menu (Shift+F10), and the try-on when the keyboard
        // reaches it, as when the mouse does.
        void AccessDockTile(ShelfTile c, MItem it, MGroup g)
        {
            c.Role = Access.ListItem;
            c.State.LabelNow = delegate { return Before(ItemText(it)); };
            c.State.HelpNow = delegate { return After(ItemText(it)); };
            c.Do = delegate { c.AccessClick(); };
            if (it.Slot >= 0 || it.Saved != null) c.On = delegate { return c.Worn; };
            c.State.Menu = delegate { ItemMenu(it); };
            Shelf shelf = dockShelf;
            if (shelf != null) shelf.AccessName = DockTabNames[Math.Max(0, Math.Min(DockTabNames.Length - 1, dockTab))];
            c.GotKeyboardFocus += delegate(object o, KeyboardFocusChangedEventArgs e)
            {
                if (e.NewFocus != c || g != dockGroup) return;
                g.Hover(it);
                if (shelf != null) shelf.Into(it);
            };
            c.LostKeyboardFocus += delegate(object o, KeyboardFocusChangedEventArgs e)
            {
                if (e.OldFocus != c || g != dockGroup) return;
                g.Unhover(it);
                DependencyObject to = e.NewFocus as DependencyObject;
                if (shelf != null && !shelf.IsMouseOver && (to == null || !shelf.IsAncestorOf(to))) DockLeft();
            };
        }

        const string Dot = "  \u00B7  ";

        static string Before(string s)
        {
            int i = s == null ? -1 : s.IndexOf(Dot, StringComparison.Ordinal);
            return i < 0 ? s : s.Substring(0, i);
        }

        static string After(string s)
        {
            int i = s == null ? -1 : s.IndexOf(Dot, StringComparison.Ordinal);
            return i < 0 ? null : s.Substring(i + Dot.Length).Replace(Dot, ". ");
        }

        partial class ShelfTile
        {
            internal void AccessClick()
            {
                Action h = Click;
                if (h != null) h();
            }
        }

        // The row of tiles: a list for readers, named after its tab, and a single stop for Tab (the arrows go through it).
        partial class Shelf
        {
            internal string AccessName;

            protected override void OnInitialized(EventArgs e)
            {
                base.OnInitialized(e);
                KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
            }

            protected override AutomationPeer OnCreateAutomationPeer() { return new ShelfPeer(this); }

            // Scrolls so the tile the keyboard reached is in full view.
            internal void Into(object tag)
            {
                double x = Pad;
                foreach (Entry e in list)
                {
                    double w = e.W * e.VisTo;
                    if (e.Tag == tag)
                    {
                        double max = TargetMax(), to = offsetTo;
                        if (x - to < Pad) to = x - Pad;
                        else if (x + w - to > width - Pad) to = x + w - width + Pad;
                        to = Math.Max(0, Math.Min(max, to));
                        if (Math.Abs(to - offsetTo) < 0.5) return;
                        offsetTo = to;
                        Kick();
                        return;
                    }
                    x += w;
                }
            }

            sealed class ShelfPeer : FrameworkElementAutomationPeer
            {
                public ShelfPeer(Shelf s) : base(s) { }
                protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.List; }
                protected override string GetClassNameCore() { return "Shelf"; }
                protected override bool IsControlElementCore() { return true; }
                protected override string GetNameCore()
                {
                    string n = base.GetNameCore();
                    return string.IsNullOrEmpty(n) ? ((Shelf)Owner).AccessName ?? "" : n;
                }
            }
        }

        // The dock's tabs: tabs for readers, reached with Tab, chosen with the arrows.
        partial class DockTabsBar : IStrip
        {
            protected override void OnInitialized(EventArgs e)
            {
                base.OnInitialized(e);
                AcceptFocus();
            }

            protected override void Activate() { }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (IsEnabled) Access.StripKey(this, e);
            }

            protected override double RingRadius { get { return 9; } }
            internal override string AccessName { get { return I18n.T("Categor\u00EDas", "Categories"); } }
            internal override AutomationControlType AccessType { get { return AutomationControlType.Tab; } }
            internal override object AccessPattern(PatternInterface p) { return Access.StripPattern(this, p); }
            internal override List<AutomationPeer> AccessChildren() { return Access.StripItems(this); }

            string[] IStrip.StripOptions { get { return labels; } }
            int IStrip.StripSelected { get { return selected; } }
            AutomationControlType IStrip.StripItemType { get { return AutomationControlType.TabItem; } }
            Rect IStrip.StripSegment(int i) { return i >= 0 && i < labels.Length ? new Rect(xs[i], Inset, ws[i], Math.Max(0, ActualHeight - Inset * 2)) : Rect.Empty; }

            void IStrip.StripPick(int i)
            {
                if (i < 0 || i >= labels.Length || i == selected) return;
                Selected = i;
                Access.StripChanged(this, i);
                Action<int> h = Changed;
                if (h != null) h(i);
            }
        }
    }
}
