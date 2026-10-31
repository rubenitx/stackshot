// Stackshot - Accessibility for the controls Stackshot draws itself: names, roles and patterns for screen readers (UI
// Automation peers), keyboard activation, and the focus ring, which only shows when the focus came from the keyboard.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public static class Access
    {
        // Roles, short to write where the controls are made.
        public static readonly AutomationControlType Button = AutomationControlType.Button, Link = AutomationControlType.Hyperlink,
            Tab = AutomationControlType.Tab, TabItem = AutomationControlType.TabItem, List = AutomationControlType.List,
            ListItem = AutomationControlType.ListItem, Menu = AutomationControlType.Menu, MenuItem = AutomationControlType.MenuItem,
            Radio = AutomationControlType.RadioButton, Check = AutomationControlType.CheckBox, Group = AutomationControlType.Group;

        // A surface that runs act when clicked runs it from the keyboard and for readers too (a plain group becomes a button).
        public static void Clickable(FrameworkElement e, Action act)
        {
            ITap t = e as ITap;
            if (t == null || t.State.Do != null) return;
            t.State.Do = act;
            t.State.Interactive = true;
            if (t.State.Role == Group) t.State.Role = Button;
        }

        // A dialog: readers announce it as one, with its message.
        public static void Dialog(Window w, string message)
        {
            AutomationProperties.SetIsDialog(w, true);
            if (!string.IsNullOrEmpty(message)) AutomationProperties.SetHelpText(w, message);
        }

        // ---- Names: "Guardar (Ctrl+S)" is read as "Guardar", with Ctrl+S as its shortcut.

        static readonly Regex Combo = new Regex(@"^(?:Ctrl|Alt|May\u00FAs|Shift|Win)\s*\+\s*\S", RegexOptions.Compiled);
        // The English key names and joining words too, for tooltips written in English.
        static readonly Regex Single = new Regex(@"^(?:F\d{1,2}|Esc|Intro|Enter|Supr|Tab|Espacio|Inicio|Fin|Retroceso|Impr Pant|Space|Del|Home|End|Backspace|PrtScn|[^\s\w]|\w)$", RegexOptions.Compiled);

        static readonly Regex Joint = new Regex(@"\s+(?:/|o|a|y|or|to|and)\s+", RegexOptions.Compiled);

        // "Ctrl+S", "F7", "[", "Ctrl+1 a Ctrl+6", "− / +". Strict: a lone letter or digit is not a key ("Cámara (2)" is a
        // name); the app writes those keys after two spaces ("Flecha  (A)").
        static bool IsKeys(string s, bool strict)
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            foreach (string part in Joint.Split(s))
            {
                if (Combo.IsMatch(part)) continue;
                if (!Single.IsMatch(part) || (strict && part.Length == 1 && char.IsLetterOrDigit(part[0]))) return false;
            }
            return true;
        }

        static bool Split(string s, out string name, out string keys)
        {
            name = s;
            keys = null;
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.TrimEnd();
            if (t.EndsWith(")", StringComparison.Ordinal))
            {
                int open = t.LastIndexOf('(');
                bool spaced = open >= 2 && t[open - 1] == ' ' && t[open - 2] == ' ';
                if (open > 0 && IsKeys(t.Substring(open + 1, t.Length - open - 2), !spaced) && t.Substring(0, open).Trim().Length > 0)
                {
                    name = t.Substring(0, open).TrimEnd();
                    keys = t.Substring(open + 1, t.Length - open - 2).Trim();
                    return true;
                }
            }
            int gap = t.LastIndexOf("  ", StringComparison.Ordinal);
            if (gap > 0 && IsKeys(t.Substring(gap), false) && t.Substring(0, gap).Trim().Length > 0)
            {
                name = t.Substring(0, gap).TrimEnd();
                keys = t.Substring(gap).Trim();
                return true;
            }
            return false;
        }

        // The name without a shortcut written after it.
        public static string Clean(string s)
        {
            string name, keys;
            Split(s, out name, out keys);
            return name == null ? null : name.Trim();
        }

        // The shortcut written after a name ("Ctrl+S" from "Guardar (Ctrl+S)"), or null.
        public static string Shortcut(string s)
        {
            string name, keys;
            return Split(s, out name, out keys) ? keys : null;
        }

        static string Tip(DependencyObject o)
        {
            FrameworkElement fe = o as FrameworkElement;
            return fe == null ? null : fe.ToolTip as string;
        }

        // What an element is called by its own label: the name it was given, the label of a button, a tap's label.
        internal static string NameOf(DependencyObject o)
        {
            if (o == null) return null;
            string n = AutomationProperties.GetName(o);
            if (string.IsNullOrEmpty(n))
            {
                ITap t = o as ITap;
                Drawn d = o as Drawn;
                if (t != null) n = t.State.CurrentLabel;
                else if (d != null) n = d.AccessName;
            }
            if (string.IsNullOrEmpty(n)) n = Tip(o);
            return Clean(n);
        }

        // The visible text inside an element (a few labels at most), for elements that have no name of their own.
        internal static string TextOf(DependencyObject o)
        {
            return TextOf(o, 4);
        }

        internal static string TextOf(DependencyObject o, int most)
        {
            List<string> parts = new List<string>();
            Texts(o, parts, 0, most);
            return parts.Count == 0 ? null : string.Join(" ", parts.ToArray());
        }

        // Help that only repeats the name (a tooltip that is the label) says nothing; a shortcut after it is read apart.
        internal static string HelpBesides(string help, string name)
        {
            string h = Clean(help);
            if (string.IsNullOrEmpty(h) || string.Equals(h, Clean(name), StringComparison.CurrentCultureIgnoreCase)) return "";
            return h;
        }

        static void Texts(DependencyObject o, List<string> parts, int depth, int most)
        {
            if (o == null || parts.Count >= most || depth > 8) return;
            UIElement u = o as UIElement;
            if (u != null && u.Visibility != Visibility.Visible) return;
            TextBlock t = o as TextBlock;
            if (t != null)
            {
                string s = t.Text;
                if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0) parts.Add(s.Trim());
                return;
            }
            int n = VisualTreeHelper.GetChildrenCount(o);
            for (int i = 0; i < n; i++) Texts(VisualTreeHelper.GetChild(o, i), parts, depth + 1, most);
        }

        // Settings rows: the row's title names its control (a button keeps its own label after it) and the line under the
        // title is its help text.
        public static void Describe(FrameworkElement control, string title, string help)
        {
            if (control == null) return;
            if (control is Drawn || control is ITap || control is TextBox) { DescribeOne(control, title, help); return; }
            int n = VisualTreeHelper.GetChildrenCount(control);
            for (int i = 0; i < n; i++)
            {
                FrameworkElement c = VisualTreeHelper.GetChild(control, i) as FrameworkElement;
                if (c != null) Describe(c, title, help);
            }
        }

        static void DescribeOne(FrameworkElement e, string title, string help)
        {
            string own = null;
            MacButton b = e as MacButton;
            ITap t = e as ITap;
            if (b != null) own = b.Label;
            else if (t != null) own = t.State.CurrentLabel;
            own = Clean(own);
            string name = title;
            if (string.IsNullOrEmpty(title)) name = own;
            else if (!string.IsNullOrEmpty(own) && CultureInfo.CurrentCulture.CompareInfo.IndexOf(title, own, CompareOptions.IgnoreCase) < 0) name = title + ", " + own;
            if (!string.IsNullOrEmpty(name)) AutomationProperties.SetName(e, name);
            if (!string.IsNullOrEmpty(help)) AutomationProperties.SetHelpText(e, help);
            else e.ClearValue(AutomationProperties.HelpTextProperty);
        }

        // ---- The focus ring: 2 px of the accent, 1 px outside the control, following its corners.

        public const double RingWidth = 2, RingGap = 1;

        public static void Ring(DrawingContext dc, Rect bounds, double radius)
        {
            if (Ds.Brushes.High) { HcPaint.Focus(dc, bounds, radius); return; }   // a contrast theme: solid, in the text colour
            double o = RingGap + RingWidth / 2;
            Rect r = bounds;
            r.Inflate(o, o);
            if (r.Width <= 0 || r.Height <= 0) return;
            Pen p = new Pen(Ds.Brush(Ds.Brushes.Accent), RingWidth);
            p.Freeze();
            double rad = Math.Max(0, radius) + o;
            dc.DrawRoundedRectangle(null, p, r, rad, rad);
        }

        // Corner radius of an element, for its ring.
        internal static double RadiusOf(UIElement e)
        {
            ITap t = e as ITap;
            if (t != null && !double.IsNaN(t.State.Radius)) return t.State.Radius;
            Border b = e as Border;
            if (b != null) return b.CornerRadius.TopLeft;
            return 4;
        }

        static bool pointer;
        static bool hooked;

        // True when a focus that arrives now should show the ring: the last input was not the mouse, a pen or a touch.
        public static bool KeyboardCue
        {
            get
            {
                Hook();
                InputDevice d = InputManager.Current.MostRecentInputDevice;
                return !(d is MouseDevice) && !(d is StylusDevice) && !pointer;
            }
        }

        // A press of the mouse on one of our controls: rings stay hidden until a key is pressed again.
        internal static void PointerUsed()
        {
            Hook();
            pointer = true;
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(delegate { pointer = false; }), true);
        }

        [ThreadStatic] static Style focusRing;

        // For WPF's own focusable elements (links in text): the same ring, drawn by WPF's keyboard-only focus visual.
        public static Style FocusRing
        {
            get
            {
                if (focusRing != null) return focusRing;
                ControlTemplate t = new ControlTemplate(typeof(Control));
                t.VisualTree = new FrameworkElementFactory(typeof(RingVisual));
                t.Seal();
                Style s = new Style(typeof(Control));
                s.Setters.Add(new Setter(Control.TemplateProperty, t));
                s.Seal();
                focusRing = s;
                return s;
            }
        }

        // ---- Keeping the focus when a page is built again

        // Remembers the control that has the keyboard focus in this window; when a rebuild takes it away while the
        // keyboard is in use, the same control in the new page (same kind and name) gets it back.
        public static void Track(Window w)
        {
            FocusMemory m = new FocusMemory(w);
            w.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(m.Got), true);
        }

        internal static string KeyOf(FrameworkElement e)
        {
            return e.GetType().FullName + "|" + (NameOf(e) ?? "");
        }

        static bool Remembered(FrameworkElement e)
        {
            return e.Focusable && (e is Drawn || e is ITap || e is TextBoxBase || e is ButtonBase);
        }

        // Elements in tree order that could hold the focus (skipping the insides of text boxes, lists and documents).
        static void Walk(DependencyObject o, Func<FrameworkElement, bool> visit, ref int budget)
        {
            if (o == null || --budget < 0) return;
            FrameworkElement fe = o as FrameworkElement;
            if (fe != null && Remembered(fe) && !visit(fe)) { budget = -1; return; }
            if (o is TextBoxBase || o is ItemsControl || o is FlowDocumentScrollViewer || o is GdiLayer || o is Drawn) return;
            int n = VisualTreeHelper.GetChildrenCount(o);
            for (int i = 0; i < n && budget >= 0; i++) Walk(VisualTreeHelper.GetChild(o, i), visit, ref budget);
        }

        internal static int Nth(Window w, FrameworkElement target, string key)
        {
            int count = 0, found = -1, budget = 20000;
            Walk(w, delegate(FrameworkElement e)
            {
                if (e == target) { found = count; return false; }
                if (KeyOf(e) == key) count++;
                return true;
            }, ref budget);
            return found;
        }

        internal static FrameworkElement Find(Window w, string key, int nth)
        {
            FrameworkElement hit = null, first = null;
            int count = 0, budget = 20000;
            Walk(w, delegate(FrameworkElement e)
            {
                if (!e.IsVisible || !e.IsEnabled || KeyOf(e) != key) return true;
                if (first == null) first = e;
                if (count++ == nth) { hit = e; return false; }
                return true;
            }, ref budget);
            return hit ?? first;
        }

        sealed class FocusMemory
        {
            readonly Window w;
            string key;
            int nth;

            public FocusMemory(Window w) { this.w = w; }

            public void Got(object sender, KeyboardFocusChangedEventArgs e)
            {
                FrameworkElement now = e.NewFocus as FrameworkElement;
                if (now != null && now != w && Remembered(now))
                {
                    // Only the keyboard's focus is ever given back, so only then is it worth remembering.
                    if (!KeyboardCue) { key = null; return; }
                    key = KeyOf(now);
                    nth = Nth(w, now, key);
                    return;
                }
                if (key == null) return;
                // WPF hands the focus to an ancestor (usually the window) when the focused control leaves the tree.
                UIElement old = e.OldFocus as UIElement;
                if (old == null || (old.IsVisible && PresentationSource.FromVisual(old) != null)) return;   // moved on purpose
                if (!KeyboardCue) return;
                string k = key;
                int n = nth;
                IInputElement landed = e.NewFocus;
                w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                {
                    if (Keyboard.FocusedElement != landed || !w.IsActive) return;   // the user moved on meanwhile
                    FrameworkElement t = Find(w, k, n);
                    if (t != null) t.Focus();
                });
            }
        }

        // ---- Names, help and patterns of the drawn controls (Controls.cs)

        internal static string DrawnName(Drawn d, string given)
        {
            string n = Clean(given);
            if (string.IsNullOrEmpty(n)) n = Clean(d.AccessName);
            if (string.IsNullOrEmpty(n)) n = Clean(d.ToolTip as string);
            return n ?? "";
        }

        // given: the help text it was given or, failing that, its tooltip (what WPF's own peer reports).
        internal static string DrawnHelp(Drawn d, string given, string name)
        {
            return HelpBesides(string.IsNullOrEmpty(given) ? d.ToolTip as string : given, name);
        }

        internal static string DrawnKeys(Drawn d, string given)
        {
            if (!string.IsNullOrEmpty(given)) return given;
            return Shortcut(AutomationProperties.GetName(d)) ?? Shortcut(d.ToolTip as string) ?? "";
        }

        internal static object DrawnPattern(Drawn d, PatternInterface p)
        {
            AutomationControlType type = d.AccessType;
            if (p == PatternInterface.Invoke && !d.AccessChecked.HasValue && (type == AutomationControlType.Button || type == AutomationControlType.Hyperlink ||
                                                                             type == AutomationControlType.MenuItem || type == AutomationControlType.SplitButton))
                return new DrawnInvoke(d);
            if (p == PatternInterface.Toggle && d.AccessChecked.HasValue) return new DrawnToggle(d);
            return null;
        }

        internal static void Invoke(UIElement e, Action act)
        {
            if (!e.IsEnabled) throw new ElementNotEnabledException();
            // As WPF's own buttons do: the action runs after the call returns, so a dialog it opens never blocks the reader.
            e.Dispatcher.BeginInvoke(DispatcherPriority.Input, act);
        }

        sealed class DrawnInvoke : IInvokeProvider
        {
            readonly Drawn d;
            public DrawnInvoke(Drawn d) { this.d = d; }
            public void Invoke() { Access.Invoke(d, d.AccessActivate); }
        }

        sealed class DrawnToggle : IToggleProvider
        {
            readonly Drawn d;
            public DrawnToggle(Drawn d) { this.d = d; }
            public ToggleState ToggleState { get { return d.AccessChecked == true ? ToggleState.On : ToggleState.Off; } }
            public void Toggle() { Access.Invoke(d, d.AccessActivate); }
        }

        // A switch changed: readers that listen hear the new state.
        internal static void Toggled(UIElement e, bool on)
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged)) return;
            AutomationPeer peer = UIElementAutomationPeer.FromElement(e);
            if (peer != null) peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, on ? ToggleState.Off : ToggleState.On, on ? ToggleState.On : ToggleState.Off);
        }

        // ---- Strips of options drawn as one control (a segmented control, the dock's tabs): a list (or a row of tabs)
        // whose options are items that can be selected.

        static readonly ConditionalWeakTable<Drawn, List<AutomationPeer>> segItems = new ConditionalWeakTable<Drawn, List<AutomationPeer>>();

        internal static List<AutomationPeer> StripItems(Drawn d)
        {
            IStrip s = d as IStrip;
            if (s == null) return null;
            List<AutomationPeer> items;
            if (segItems.TryGetValue(d, out items) && items.Count == s.StripOptions.Length) return items;
            if (items != null) segItems.Remove(d);
            items = new List<AutomationPeer>();
            for (int i = 0; i < s.StripOptions.Length; i++) items.Add(new SegItemPeer(d, s, i));
            segItems.Add(d, items);
            return items;
        }

        internal static object StripPattern(Drawn d, PatternInterface p)
        {
            return p == PatternInterface.Selection && d is IStrip ? new SegSelection(d) : null;
        }

        internal static void StripChanged(Drawn d, int index)
        {
            List<AutomationPeer> items;
            if (!segItems.TryGetValue(d, out items) || index < 0 || index >= items.Count) return;
            if (AutomationPeer.ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected))
                items[index].RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
        }

        // Left, Right, Home and End move the choice; true when the key was one of them.
        internal static bool StripKey(IStrip s, KeyEventArgs e)
        {
            if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) return false;
            int n = s.StripOptions.Length, at = s.StripSelected;
            int to = e.Key == Key.Left || e.Key == Key.Up ? at - 1 : e.Key == Key.Right || e.Key == Key.Down ? at + 1
                   : e.Key == Key.Home ? 0 : e.Key == Key.End ? n - 1 : int.MinValue;
            if (to == int.MinValue) return false;
            e.Handled = true;
            to = Math.Max(0, Math.Min(n - 1, to));
            if (to != at) s.StripPick(to);
            return true;
        }

        sealed class SegSelection : ISelectionProvider
        {
            readonly Drawn d;
            public SegSelection(Drawn d) { this.d = d; }
            public bool CanSelectMultiple { get { return false; } }
            public bool IsSelectionRequired { get { return true; } }
            public IRawElementProviderSimple[] GetSelection()
            {
                List<AutomationPeer> items = StripItems(d);
                int i = ((IStrip)d).StripSelected;
                if (items == null || i < 0 || i >= items.Count) return new IRawElementProviderSimple[0];
                return new IRawElementProviderSimple[] { ((SegItemPeer)items[i]).Provider() };
            }
        }

        // Screen rectangle (device pixels) of part of an element, or empty when it is not on screen.
        internal static Rect ScreenRect(UIElement e, Rect local)
        {
            try
            {
                if (e == null || !e.IsVisible || PresentationSource.FromVisual(e) == null) return Rect.Empty;
                return new Rect(e.PointToScreen(local.TopLeft), e.PointToScreen(local.BottomRight));
            }
            catch (InvalidOperationException) { return Rect.Empty; }
        }

        sealed class SegItemPeer : AutomationPeer, ISelectionItemProvider
        {
            readonly Drawn d;
            readonly IStrip s;
            readonly int i;

            public SegItemPeer(Drawn d, IStrip s, int i) { this.d = d; this.s = s; this.i = i; }

            internal IRawElementProviderSimple Provider() { return ProviderFromPeer(this); }

            protected override string GetNameCore() { return i < s.StripOptions.Length ? s.StripOptions[i] : ""; }
            protected override AutomationControlType GetAutomationControlTypeCore() { return s.StripItemType; }
            protected override string GetClassNameCore() { return d.GetType().Name + "Item"; }
            protected override Rect GetBoundingRectangleCore() { return ScreenRect(d, s.StripSegment(i)); }
            protected override Point GetClickablePointCore()
            {
                Rect r = GetBoundingRectangleCore();
                return r.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            }
            protected override List<AutomationPeer> GetChildrenCore() { return null; }
            protected override string GetAcceleratorKeyCore() { return ""; }
            protected override string GetAccessKeyCore() { return ""; }
            protected override string GetAutomationIdCore() { return ""; }
            protected override string GetHelpTextCore() { return ""; }
            protected override string GetItemStatusCore() { return ""; }
            protected override string GetItemTypeCore() { return ""; }
            protected override AutomationPeer GetLabeledByCore() { return null; }
            protected override AutomationOrientation GetOrientationCore() { return AutomationOrientation.None; }
            protected override bool HasKeyboardFocusCore() { return d.IsKeyboardFocused && s.StripSelected == i; }
            protected override bool IsContentElementCore() { return true; }
            protected override bool IsControlElementCore() { return true; }
            protected override bool IsEnabledCore() { return d.IsEnabled; }
            protected override bool IsKeyboardFocusableCore() { return false; }
            protected override bool IsOffscreenCore() { return !d.IsVisible; }
            protected override bool IsPasswordCore() { return false; }
            protected override bool IsRequiredForFormCore() { return false; }
            protected override void SetFocusCore() { d.Focus(); }
            protected override int GetPositionInSetCore() { return i + 1; }
            protected override int GetSizeOfSetCore() { return s.StripOptions.Length; }
            public override object GetPattern(PatternInterface p) { return p == PatternInterface.SelectionItem ? this : null; }

            public bool IsSelected { get { return s.StripSelected == i; } }
            public IRawElementProviderSimple SelectionContainer
            {
                get
                {
                    AutomationPeer parent = UIElementAutomationPeer.FromElement(d);
                    return parent == null ? null : ProviderFromPeer(parent);
                }
            }
            public void Select() { Access.Invoke(d, delegate { if (d.IsEnabled && i < s.StripOptions.Length) s.StripPick(i); }); }
            public void AddToSelection() { if (s.StripSelected != i) throw new InvalidOperationException(); }
            public void RemoveFromSelection() { if (s.StripSelected == i) throw new InvalidOperationException(); }
        }
    }

    // A row of options drawn as one control; Access gives it its items for screen readers.
    internal interface IStrip
    {
        string[] StripOptions { get; }
        int StripSelected { get; }
        Rect StripSegment(int i);
        void StripPick(int i);     // as a click on that option does (Changed fires)
        AutomationControlType StripItemType { get; }
    }

    // ---- Our own clickable surfaces (cards, tiles, chips, links): a button (or a link, a tab, a menu item) for readers,
    // reached with Tab, pressed with Space or Enter, moved through with the arrows when they are a group.

    public interface ITap { TapState State { get; } }

    public sealed class TapState
    {
        readonly FrameworkElement owner;
        Adorner ring;
        AdornerLayer ringLayer;
        public string Label, Help, Keys;
        public Func<string> LabelNow, HelpNow;   // for a label or a help that changes (they win over Label and Help)

        public string CurrentLabel { get { Func<string> f = LabelNow; return f != null ? f() : Label; } }
        public string CurrentHelp { get { Func<string> f = HelpNow; return f != null ? f() : Help; } }
        public AutomationControlType Role = AutomationControlType.Button;
        public Action Do, Menu;
        public Func<bool> On;           // checked (CheckBox) or selected (TabItem, ListItem, RadioButton)
        public double Radius = double.NaN;
        public bool ShowsFocus;         // it marks the focus itself (a menu row lights up): no ring on top

        internal TapState(FrameworkElement owner)
        {
            this.owner = owner;
            owner.Focusable = true;
            owner.FocusVisualStyle = null;
        }

        public FrameworkElement Owner { get { return owner; } }

        // Off for a container that only gives its children a role (a menu, a group of tabs): not a stop for Tab.
        public bool Interactive
        {
            get { return owner.Focusable; }
            set { owner.Focusable = value; }
        }

        public bool RingShown { get { return ring != null; } }

        public void Run()
        {
            Action a = Do;
            if (a != null && owner.IsEnabled) a();
        }

        bool Grouped
        {
            get { return Role == AutomationControlType.TabItem || Role == AutomationControlType.ListItem || Role == AutomationControlType.RadioButton; }
        }

        internal void KeyDown(KeyEventArgs e)
        {
            if (e.Handled || !owner.IsEnabled || e.OriginalSource != owner) return;   // its own keys, not those of a field inside it
            Key k = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys m = Keyboard.Modifiers;
            if ((k == Key.Apps && m == ModifierKeys.None) || (k == Key.F10 && m == ModifierKeys.Shift))
            {
                if (Menu == null) return;
                e.Handled = true;
                Menu();
                return;
            }
            if (m != ModifierKeys.None) return;
            if (k == Key.Space || (k == Key.Enter && Role != AutomationControlType.CheckBox))
            {
                if (Do == null) return;
                e.Handled = true;
                // After the key's character has gone by: an action that opens a text field must not get the space typed in it.
                if (!e.IsRepeat) owner.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)Run);
                return;
            }
            if (!Grouped) return;
            int step = k == Key.Left || k == Key.Up ? -1 : k == Key.Right || k == Key.Down ? 1 : 0;
            if (step == 0) return;
            Panel p = VisualTreeHelper.GetParent(owner) as Panel;
            if (p == null) return;
            List<FrameworkElement> peers = new List<FrameworkElement>();
            foreach (UIElement c in p.Children)
            {
                ITap t = c as ITap;
                if (t != null && t.State.Role == Role && t.State.Interactive && c.IsVisible && c.IsEnabled) peers.Add((FrameworkElement)c);
            }
            int at = peers.IndexOf(owner);
            if (at < 0 || peers.Count < 2) return;
            e.Handled = true;
            peers[(at + step + peers.Count) % peers.Count].Focus();
        }

        internal void GotFocus(KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus != owner) return;
            if (Access.KeyboardCue && !ShowsFocus) ShowRing();
        }

        internal void LostFocus(KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus == owner) HideRing();
        }

        internal void Pressed()
        {
            Access.PointerUsed();
            HideRing();
        }

        void ShowRing()
        {
            if (ring != null) return;
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(owner);
            if (layer == null) return;
            ring = new RingAdorner(owner, Access.RadiusOf(owner));
            ringLayer = layer;
            layer.Add(ring);
        }

        void HideRing()
        {
            if (ring == null) return;
            if (ringLayer != null) ringLayer.Remove(ring);
            ring = null;
            ringLayer = null;
        }

        sealed class RingAdorner : Adorner
        {
            readonly double radius;
            public RingAdorner(UIElement e, double radius) : base(e) { this.radius = radius; IsHitTestVisible = false; }
            protected override void OnRender(DrawingContext dc) { Access.Ring(dc, new Rect(AdornedElement.RenderSize), radius); }
        }
    }

    public sealed class TapPeer : FrameworkElementAutomationPeer, IInvokeProvider, IToggleProvider, ISelectionItemProvider
    {
        readonly TapState tap;

        public TapPeer(FrameworkElement owner, TapState tap) : base(owner) { this.tap = tap; }

        protected override AutomationControlType GetAutomationControlTypeCore() { return tap.Role; }
        protected override string GetClassNameCore() { return Owner.GetType().Name; }
        protected override bool IsControlElementCore() { return true; }
        protected override bool IsContentElementCore() { return true; }

        protected override string GetNameCore()
        {
            string n = Access.Clean(AutomationProperties.GetName(Owner));
            if (string.IsNullOrEmpty(n)) n = Access.Clean(tap.CurrentLabel);
            if (string.IsNullOrEmpty(n)) n = Access.Clean(base.GetNameCore());
            if (string.IsNullOrEmpty(n)) n = Access.Clean(((FrameworkElement)Owner).ToolTip as string);
            // A control is named by what it shows; a group only by the title it was given.
            if (string.IsNullOrEmpty(n) && tap.Interactive) n = Access.TextOf(Owner);
            return n ?? "";
        }

        protected override string GetHelpTextCore()
        {
            string own = AutomationProperties.GetHelpText(Owner);
            string name = GetNameCore();
            string help = tap.CurrentHelp;
            string h = Access.HelpBesides(string.IsNullOrEmpty(help) ? ((FrameworkElement)Owner).ToolTip as string : help, name);
            own = Access.HelpBesides(own, name);
            if (string.IsNullOrEmpty(own)) return h;
            return string.IsNullOrEmpty(h) || h == own ? own : h + ". " + own;
        }

        protected override string GetAcceleratorKeyCore()
        {
            string k = AutomationProperties.GetAcceleratorKey(Owner);
            if (string.IsNullOrEmpty(k)) k = tap.Keys;
            if (string.IsNullOrEmpty(k)) k = Access.Shortcut(((FrameworkElement)Owner).ToolTip as string);
            return k ?? "";
        }

        public override object GetPattern(PatternInterface p)
        {
            AutomationControlType r = tap.Role;
            bool select = r == AutomationControlType.TabItem || r == AutomationControlType.ListItem || r == AutomationControlType.RadioButton;
            if (p == PatternInterface.Invoke && tap.Do != null && r != AutomationControlType.CheckBox && !(select && tap.On != null)) return this;
            if (p == PatternInterface.Toggle && (r == AutomationControlType.CheckBox || r == AutomationControlType.MenuItem) && tap.On != null) return this;
            if (p == PatternInterface.SelectionItem && select && tap.On != null) return this;
            return base.GetPattern(p);
        }

        public void Invoke() { Access.Invoke(Owner, tap.Run); }

        public ToggleState ToggleState { get { return tap.On != null && tap.On() ? ToggleState.On : ToggleState.Off; } }
        public void Toggle() { Access.Invoke(Owner, tap.Run); }

        public bool IsSelected { get { return tap.On != null && tap.On(); } }
        public IRawElementProviderSimple SelectionContainer { get { return null; } }
        public void Select() { if (!IsSelected) Access.Invoke(Owner, tap.Run); }
        public void AddToSelection() { if (!IsSelected) throw new InvalidOperationException(); }
        public void RemoveFromSelection() { if (IsSelected) throw new InvalidOperationException(); }
    }

    public class TapBorder : Border, ITap
    {
        readonly TapState state;
        public TapBorder() { state = new TapState(this); }
        public TapState State { get { return state; } }
        public string Label { get { return state.Label; } set { state.Label = value; } }
        public string Help { get { return state.Help; } set { state.Help = value; } }
        public AutomationControlType Role { get { return state.Role; } set { state.Role = value; } }
        public Action Do { get { return state.Do; } set { state.Do = value; } }
        public bool Interactive { get { return state.Interactive; } set { state.Interactive = value; } }
        public Func<bool> On { get { return state.On; } set { state.On = value; } }
        public double Radius { get { return state.Radius; } set { state.Radius = value; } }
        protected override AutomationPeer OnCreateAutomationPeer() { return new TapPeer(this, state); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); state.KeyDown(e); }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); state.GotFocus(e); }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); state.LostFocus(e); }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) { base.OnPreviewMouseDown(e); state.Pressed(); }
    }

    public class TapGrid : Grid, ITap
    {
        readonly TapState state;
        public TapGrid() { state = new TapState(this); }
        public TapState State { get { return state; } }
        public string Label { get { return state.Label; } set { state.Label = value; } }
        public string Help { get { return state.Help; } set { state.Help = value; } }
        public AutomationControlType Role { get { return state.Role; } set { state.Role = value; } }
        public Action Do { get { return state.Do; } set { state.Do = value; } }
        public bool Interactive { get { return state.Interactive; } set { state.Interactive = value; } }
        public Func<bool> On { get { return state.On; } set { state.On = value; } }
        public double Radius { get { return state.Radius; } set { state.Radius = value; } }
        protected override AutomationPeer OnCreateAutomationPeer() { return new TapPeer(this, state); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); state.KeyDown(e); }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); state.GotFocus(e); }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); state.LostFocus(e); }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) { base.OnPreviewMouseDown(e); state.Pressed(); }
    }

    public class TapPanel : StackPanel, ITap
    {
        readonly TapState state;
        public TapPanel() { state = new TapState(this); }
        public TapState State { get { return state; } }
        public string Label { get { return state.Label; } set { state.Label = value; } }
        public string Help { get { return state.Help; } set { state.Help = value; } }
        public AutomationControlType Role { get { return state.Role; } set { state.Role = value; } }
        public Action Do { get { return state.Do; } set { state.Do = value; } }
        public bool Interactive { get { return state.Interactive; } set { state.Interactive = value; } }
        public Func<bool> On { get { return state.On; } set { state.On = value; } }
        public double Radius { get { return state.Radius; } set { state.Radius = value; } }
        protected override AutomationPeer OnCreateAutomationPeer() { return new TapPeer(this, state); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); state.KeyDown(e); }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); state.GotFocus(e); }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); state.LostFocus(e); }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) { base.OnPreviewMouseDown(e); state.Pressed(); }
    }

    public class TapText : TextBlock, ITap
    {
        readonly TapState state;
        public TapText() { state = new TapState(this); state.Role = AutomationControlType.Hyperlink; state.Radius = 3; }
        public TapState State { get { return state; } }
        public string Label { get { return state.Label; } set { state.Label = value; } }
        public string Help { get { return state.Help; } set { state.Help = value; } }
        public AutomationControlType Role { get { return state.Role; } set { state.Role = value; } }
        public Action Do { get { return state.Do; } set { state.Do = value; } }
        public bool Interactive { get { return state.Interactive; } set { state.Interactive = value; } }
        public Func<bool> On { get { return state.On; } set { state.On = value; } }
        public double Radius { get { return state.Radius; } set { state.Radius = value; } }
        protected override AutomationPeer OnCreateAutomationPeer() { return new TapPeer(this, state); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); state.KeyDown(e); }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); state.GotFocus(e); }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); state.LostFocus(e); }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) { base.OnPreviewMouseDown(e); state.Pressed(); }
    }

    // A wrapping row of choices (the backdrops): a list for readers, named after what it chooses.
    public class ListWrap : WrapPanel
    {
        public string Label;

        protected override AutomationPeer OnCreateAutomationPeer() { return new Peer(this); }

        sealed class Peer : FrameworkElementAutomationPeer
        {
            public Peer(ListWrap w) : base(w) { }
            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.List; }
            protected override string GetClassNameCore() { return "ListWrap"; }
            protected override bool IsControlElementCore() { return true; }
            protected override string GetNameCore()
            {
                string n = base.GetNameCore();
                return string.IsNullOrEmpty(n) ? ((ListWrap)Owner).Label ?? "" : n;
            }
        }
    }

    // A container that only gives its children a role (a menu): readers hear what it is, it does nothing itself.
    public class RoleGrid : Grid
    {
        public AutomationControlType Role = AutomationControlType.Group;
        public string Label;

        public RoleGrid() { FocusVisualStyle = null; }

        protected override AutomationPeer OnCreateAutomationPeer() { return new Peer(this); }

        sealed class Peer : FrameworkElementAutomationPeer
        {
            public Peer(RoleGrid g) : base(g) { }
            protected override AutomationControlType GetAutomationControlTypeCore() { return ((RoleGrid)Owner).Role; }
            protected override string GetClassNameCore() { return "RoleGrid"; }
            protected override bool IsControlElementCore() { return true; }
            protected override string GetNameCore()
            {
                string n = base.GetNameCore();
                return string.IsNullOrEmpty(n) ? ((RoleGrid)Owner).Label ?? "" : n;
            }
        }
    }

    // A panel that holds items (a grid of captures): what it is for readers and its name; its children are the items.
    public sealed class RolePeer : FrameworkElementAutomationPeer
    {
        readonly AutomationControlType type;
        readonly string name;

        public RolePeer(FrameworkElement owner, AutomationControlType type, string name) : base(owner) { this.type = type; this.name = name; }

        protected override AutomationControlType GetAutomationControlTypeCore() { return type; }
        protected override string GetClassNameCore() { return Owner.GetType().Name; }
        protected override bool IsControlElementCore() { return true; }
        protected override string GetNameCore()
        {
            string n = base.GetNameCore();
            return string.IsNullOrEmpty(n) ? name ?? "" : n;
        }
    }

    // A level shown as a bar (the microphone's): a read-only progress bar for readers, 0 to 100.
    public sealed class LevelPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        readonly Func<double> value;

        public LevelPeer(FrameworkElement owner, Func<double> value) : base(owner) { this.value = value; }

        protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.ProgressBar; }
        protected override string GetClassNameCore() { return Owner.GetType().Name; }
        protected override bool IsControlElementCore() { return true; }
        public override object GetPattern(PatternInterface p) { return p == PatternInterface.RangeValue ? this : base.GetPattern(p); }

        public double Value { get { double v = value(); return double.IsNaN(v) ? 0 : Math.Round(Math.Max(0, Math.Min(100, v))); } }
        public bool IsReadOnly { get { return true; } }
        public double Maximum { get { return 100; } }
        public double Minimum { get { return 0; } }
        public double LargeChange { get { return 10; } }
        public double SmallChange { get { return 1; } }
        public void SetValue(double v) { throw new InvalidOperationException(); }
    }

    // A picture that only decorates (a logo next to the app's name): left out of what screen readers list.
    public sealed class DecorImage : Image
    {
        protected override AutomationPeer OnCreateAutomationPeer() { return null; }
    }

    // The ring of WPF's focus visual (Access.FocusRing) around the element it adorns.
    public sealed class RingVisual : FrameworkElement
    {
        public RingVisual() { IsHitTestVisible = false; SnapsToDevicePixels = true; }

        protected override Geometry GetLayoutClip(Size layoutSlotSize) { return null; }

        protected override void OnRender(DrawingContext dc)
        {
            double radius = 3;
            for (DependencyObject p = VisualTreeHelper.GetParent(this); p != null; p = VisualTreeHelper.GetParent(p))
            {
                Adorner a = p as Adorner;
                if (a == null) continue;
                if (a.AdornedElement is Border) radius = Access.RadiusOf(a.AdornedElement);
                break;
            }
            Access.Ring(dc, new Rect(RenderSize), radius);
        }
    }

    // A button for readers made of any element that already reacts to clicks (the window's minimize and close).
    public sealed class ActionPeer : FrameworkElementAutomationPeer, IInvokeProvider
    {
        readonly Action act;
        readonly string name;

        public ActionPeer(FrameworkElement owner, Action act) : this(owner, act, null) { }

        public ActionPeer(FrameworkElement owner, Action act, string name) : base(owner) { this.act = act; this.name = name; }

        protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Button; }
        protected override string GetClassNameCore() { return Owner.GetType().Name; }
        protected override bool IsControlElementCore() { return true; }
        protected override bool IsContentElementCore() { return true; }
        protected override string GetNameCore() { return name ?? Access.NameOf(Owner) ?? ""; }
        protected override string GetHelpTextCore() { return Access.HelpBesides(base.GetHelpTextCore(), GetNameCore()); }
        public override object GetPattern(PatternInterface p) { return p == PatternInterface.Invoke ? this : base.GetPattern(p); }
        public void Invoke() { Access.Invoke(Owner, act); }
    }

    // A list whose items are plain data: readers hear what each row says instead of the type of its object.
    public class NamedListBox : ListBox
    {
        public Func<object, string> ItemName;

        protected override AutomationPeer OnCreateAutomationPeer() { return new Peer(this); }

        // The focused row gets the focus ring (WPF shows it only after the keyboard).
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            FrameworkElement c = element as FrameworkElement;
            if (c != null) c.FocusVisualStyle = Access.FocusRing;
        }

        sealed class Peer : ListBoxAutomationPeer
        {
            readonly NamedListBox list;
            public Peer(NamedListBox list) : base(list) { this.list = list; }
            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) { return new ItemPeer(item, this, list); }
        }

        sealed class ItemPeer : ListBoxItemAutomationPeer
        {
            readonly NamedListBox list;
            public ItemPeer(object item, SelectorAutomationPeer owner, NamedListBox list) : base(item, owner) { this.list = list; }
            protected override string GetNameCore()
            {
                Func<object, string> f = list.ItemName;
                string n = null;
                if (f != null) try { n = f(Item); } catch (Exception) { n = null; }
                return string.IsNullOrEmpty(n) ? base.GetNameCore() : n;
            }
        }
    }
}
