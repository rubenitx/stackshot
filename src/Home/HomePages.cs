// Stackshot - Settings sections and their rows (switches, segmented controls, hotkeys, folder).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using D = System.Drawing;
using WF = System.Windows.Forms;

namespace Stackshot
{
    public partial class HomeWindow
    {
        static readonly string[] ActionNames = { "Capturar un \u00E1rea", "Repetir la \u00FAltima \u00E1rea", "Capturar la pantalla", "Capturar la ventana activa", "Captura con desplazamiento", "Copiar texto de la pantalla", "Grabar v\u00EDdeo", "Grabar GIF", "Ver y ordenar Markdown", "Capturar con temporizador" };
        static readonly string[] ActionShort = { "Un \u00E1rea", "La \u00FAltima \u00E1rea", "Pantalla completa", "Una ventana", "Con desplazamiento", "Copiar texto", "V\u00EDdeo", "GIF animado", "Markdown", "Con temporizador" };
        static readonly string[] ActionIcons = { "area", "update", "screen", "window", "scroll", "textscan", "video", "gif", "markdown", "clock" };
        static readonly D.Color[,] ActionColors =
        {
            { D.Color.FromArgb(64, 156, 255), D.Color.FromArgb(10, 110, 230) },
            { D.Color.FromArgb(100, 180, 255), D.Color.FromArgb(48, 118, 236) },
            { D.Color.FromArgb(90, 200, 250), D.Color.FromArgb(0, 122, 255) },
            { D.Color.FromArgb(191, 90, 242), D.Color.FromArgb(120, 70, 220) },
            { D.Color.FromArgb(52, 199, 89), D.Color.FromArgb(0, 160, 120) },
            { D.Color.FromArgb(100, 210, 200), D.Color.FromArgb(20, 150, 170) },
            { D.Color.FromArgb(255, 85, 100), D.Color.FromArgb(230, 40, 40) },
            { D.Color.FromArgb(255, 179, 64), D.Color.FromArgb(245, 110, 40) },
            { D.Color.FromArgb(140, 150, 255), D.Color.FromArgb(88, 86, 214) },
            { D.Color.FromArgb(255, 159, 10), D.Color.FromArgb(235, 100, 20) }
        };
        static readonly D.Color Gray1 = D.Color.FromArgb(152, 152, 157), Gray2 = D.Color.FromArgb(110, 110, 115);
        static readonly string[] ProfileNames = { "Rendimiento", "Equilibrado", "Completo" };

        string keysMoved;   // one-off note after a combo moved from one action to another

        // ---- Page building blocks

        // Builds one part of a page; if it fails it is logged and left out whole (no caption without its card), and what
        // follows still shows.
        void Section(string what, Action build)
        {
            int had = body.Children.Count;
            try { build(); }
            catch (Exception ex)
            {
                ShotStack.Log("P\u00E1gina " + page + ", " + what + ": " + ex.Message);
                while (body.Children.Count > had) body.Children.RemoveAt(body.Children.Count - 1);
            }
        }

        void Header(string title, string sub)
        {
            TextBlock t = Label(title, Ds.Title, 26, Ds.Brushes.Label, new Thickness(0, 58, 0, 0));
            body.Children.Add(t);
            if (sub != null)
            {
                TextBlock d = Paragraph(sub, 13, Ds.Brushes.Label2);
                d.Margin = new Thickness(0, 6, 0, 0);
                body.Children.Add(d);
            }
            body.Children.Add(new Border { Height = 24 });
        }

        // macOS Settings-style group: a small caption above, rows inside a rounded card.
        void Group(string caption, params Row[] rows)
        {
            Palette pal = Ds.Brushes;
            if (caption != null) body.Children.Add(Label(caption, Ds.Semibold, 12.5, pal.Label2, new Thickness(8, 0, 0, 8)));
            StackPanel list = new StackPanel();
            bool first = true;
            foreach (Row r in rows)
            {
                if (r == null) continue;
                r.W = this;
                r.Build();
                if (!first) list.Children.Add(new Border { Height = 1, Background = Ds.Brush(pal.Separator), Margin = new Thickness(r.Icon != null ? 54 : 14, 0, 0, 0) });
                list.Children.Add(r);
                first = false;
            }
            Border card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ds.Brush(pal.Group),
                BorderBrush = Ds.Brush(pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.07, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = list
            };
            Grid holder = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            if (!pal.Dark && !pal.High) holder.Children.Add(new SoftShadow(10, 3, 0.5, 0.06));
            holder.Children.Add(card);
            body.Children.Add(holder);
            SearchGroup(caption, holder, rows);
        }

        void Note(string text)
        {
            TextBlock n = Paragraph(text, 12, Ds.Brushes.Label2);
            n.Margin = new Thickness(8, -14, 8, 24);
            body.Children.Add(n);
        }

        // A note that ends in a quiet link (an action that is rarely needed, so no button for it).
        void NoteLink(string text, string link, Action act)
        {
            TextBlock n = Paragraph(text.Length > 0 ? text + " " : "", 12, Ds.Brushes.Label2);
            n.Margin = new Thickness(8, -14, 8, 24);
            n.Inlines.Add(InlineLink(link, act));
            body.Children.Add(n);
        }

        static System.Windows.Documents.Hyperlink InlineLink(string text, Action act)
        {
            System.Windows.Documents.Hyperlink h = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(text));
            h.Foreground = Ds.Brush(Ds.Brushes.Link);
            h.TextDecorations = null;
            h.Cursor = Cursors.Hand;
            h.Focusable = false;
            AccessLink(h);
            h.MouseEnter += delegate { h.TextDecorations = TextDecorations.Underline; };
            h.MouseLeave += delegate { h.TextDecorations = null; };
            // Run after the click: the action may repaint the very text this link lives in.
            h.Click += delegate { h.Dispatcher.BeginInvoke(act); };
            return h;
        }

        void Add(FrameworkElement e) { body.Children.Add(e); }

        // After a switch or a choice, rows that depend on it fade in or out (no rebuild, so the switch keeps sliding).
        void SyncRows()
        {
            if (body == null) return;
            List<Row> rows = new List<Row>();
            FindRows(body, rows);
            foreach (Row r in rows) r.Sync(true);
            if (profileNote != null && profileNote.Parent == body) PaintProfileNote();
            if (styleNote != null && styleNote.Parent == body) PaintStyleNote();
        }

        static void FindRows(DependencyObject o, List<Row> into)
        {
            Row r = o as Row;
            if (r != null) { into.Add(r); return; }
            foreach (object c in LogicalTreeHelper.GetChildren(o))
            {
                DependencyObject d = c as DependencyObject;
                if (d != null) FindRows(d, into);
            }
        }

        // ---- Rows

        // Setting row: colored icon, title and description, and the control on the right.
        public abstract class Row : Grid
        {
            public HomeWindow W;
            public string Title, Sub, Icon;
            public D.Color C1, C2;
            // An app's own icon in place of the coloured tile (another app's row).
            public ImageSource Picture;
            // Only applies while When holds; otherwise it dims, and WhenOff (if any) explains why.
            public Func<bool> When;
            public string WhenOff;
            // A description that follows the setting (null hides it), used instead of Sub.
            public Func<string> SubNow;
            protected TextBlock SubText;
            bool greyed;
            internal string SubShown { get { return SubText != null && SubText.Visibility == Visibility.Visible ? SubText.Text : null; } }

            string CurrentSub()
            {
                if (When != null && WhenOff != null && !When()) return WhenOff;
                return SubNow != null ? SubNow() : Sub;
            }

            protected Row(string title, string sub, string icon, D.Color c1, D.Color c2) { Title = title; Sub = sub; Icon = icon; C1 = c1; C2 = c2; }

            protected abstract FrameworkElement Control();

            public virtual void Build()
            {
                Children.Clear();
                ColumnDefinitions.Clear();
                Palette pal = Ds.Brushes;
                string sub = CurrentSub();
                MinHeight = sub != null ? 58 : 48;
                Margin = new Thickness(14, 0, 14, 0);
                ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ColumnDefinitions.Add(new ColumnDefinition());
                ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                if (Picture != null)
                {
                    Image img = new Image { Source = Picture, Width = 28, Height = 28, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    // Readers hear whose icon it is (the row's title follows).
                    System.Windows.Automation.AutomationProperties.SetName(img, "Icono de " + Title);
                    Children.Add(img);
                }
                else if (Icon != null)
                {
                    IconTile t = new IconTile(Icon, 28, HomeWindow.W(C1), HomeWindow.W(C2));
                    t.Margin = new Thickness(0, 0, 12, 0);
                    Children.Add(t);
                }
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 9, 14, 9) };
                Grid.SetColumn(text, 1);
                // A contrast theme shows a row that does not apply in GrayText instead of fading it.
                greyed = pal.High && When != null && !When();
                TextBlock title = Label(Title, Ds.Regular, 13.5, greyed ? pal.Label3 : pal.Label);
                title.TextTrimming = TextTrimming.CharacterEllipsis;
                text.Children.Add(title);
                SubText = null;
                if (sub != null || SubNow != null || (When != null && WhenOff != null))
                {
                    SubText = Label(sub ?? "", Ds.Regular, 12, greyed ? pal.Label3 : pal.Label2);
                    SubText.TextWrapping = TextWrapping.Wrap;
                    SubText.Margin = new Thickness(0, 2, 0, 0);
                    SubText.Visibility = sub != null ? Visibility.Visible : Visibility.Collapsed;
                    text.Children.Add(SubText);
                }
                Children.Add(text);
                FrameworkElement c = Control();
                if (c != null)
                {
                    c.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(c, 2);
                    Children.Add(c);
                    AccessRow(this, sub);
                }
                Sync(false);
            }

            // Follows the settings it depends on without being rebuilt.
            public virtual void Sync(bool animate)
            {
                if (When != null)
                {
                    bool live = When();
                    IsEnabled = live;
                    if (Ds.Brushes.High && greyed == live) { Build(); return; }
                    double to = live || Ds.Brushes.High ? 1 : 0.42;
                    // Only a change fades (every switch on the page syncs all its rows).
                    if (animate) { if (Math.Abs(Opacity - to) > 0.001) HomeWindow.Glide(this, OpacityProperty, to, 220); }
                    else { BeginAnimation(OpacityProperty, null); Opacity = to; }
                }
                if (SubText != null && (SubNow != null || WhenOff != null))
                {
                    string sub = CurrentSub();
                    SubText.Text = sub ?? "";
                    SubText.Visibility = sub != null ? Visibility.Visible : Visibility.Collapsed;
                    MinHeight = sub != null ? 58 : 48;
                    AccessRow(this, sub);
                }
            }
        }

        public class ToggleRow : Row
        {
            readonly Func<bool> get;
            readonly Action<bool> set;
            public ToggleRow(string title, string sub, string icon, D.Color c1, D.Color c2, Func<bool> get, Action<bool> set) : base(title, sub, icon, c1, c2)
            {
                this.get = get;
                this.set = set;
            }
            MacSwitch sw;
            protected override FrameworkElement Control()
            {
                sw = new MacSwitch(get());
                sw.Toggled += delegate(bool v) { set(v); W.Changed(); W.SyncRows(); };
                return sw;
            }
            // Changed elsewhere (a profile, a reset): the knob slides over.
            public override void Sync(bool animate)
            {
                base.Sync(animate);
                if (sw != null && sw.IsOn != get()) sw.IsOn = get();
            }
        }

        public class SegRow : Row
        {
            readonly string[] options;
            readonly Func<int> get;
            readonly Action<int> set;
            public SegRow(string title, string sub, string icon, D.Color c1, D.Color c2, string[] options, Func<int> get, Action<int> set) : base(title, sub, icon, c1, c2)
            {
                this.options = options;
                this.get = get;
                this.set = set;
            }
            // True while the value sits between two options (tuned elsewhere): the nearest one shows, and clicking it
            // sets it exactly.
            public Func<bool> Loose;
            MacSegmented seg;
            protected override FrameworkElement Control()
            {
                seg = new MacSegmented(options, get());
                seg.Changed += delegate(int i) { Picked(i); };
                seg.PreviewMouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    if (Loose == null || !seg.IsMouseCaptured || !Loose()) return;
                    Point p = e.GetPosition(seg);
                    if (!new Rect(seg.RenderSize).Contains(p)) return;
                    int i = Math.Max(0, Math.Min(options.Length - 1, (int)((p.X - 2) / Math.Max(1, (seg.ActualWidth - 4) / options.Length))));
                    if (i == seg.Selected) Picked(i); // the segment's own click ignores the option already shown
                };
                return seg;
            }
            void Picked(int i) { set(i); W.Changed(); W.SyncRows(); }
            public override void Sync(bool animate)
            {
                base.Sync(animate);
                if (seg != null) seg.Selected = get();
            }
        }

        // Row with a button on the right (none if the label is null).
        public class ButtonRow : Row
        {
            readonly string label;
            readonly Action act;
            public ButtonRow(string title, string sub, string icon, D.Color c1, D.Color c2, string label, Action act) : base(title, sub, icon, c1, c2)
            {
                this.label = label;
                this.act = act;
            }
            protected override FrameworkElement Control()
            {
                if (label == null) return null;
                MacButton b = new MacButton(label, ButtonKind.Secondary);
                b.Click += delegate { if (act != null) act(); };
                return b;
            }
        }

        // Hotkey field drawn as keycaps; click it to record a new combination. While recording it shows the modifiers
        // being held and, in orange, why a combination can't be used.
        public class HotkeyRow : Row
        {
            readonly string action;
            readonly QuickLink link;      // a quick link's shortcut instead of an action's
            bool on;
            string hint;
            Border field;
            SolidColorBrush pulse;
            Color pulseColor;
            KeyEventHandler live;
            string liveShown;

            public HotkeyRow(string action, int i) : base(ActionNames[i], null, ActionIcons[i], ActionColors[i, 0], ActionColors[i, 1])
            {
                this.action = action;
                // Rebuilt away (a theme change) while recording: stop cleanly.
                Unloaded += delegate { if (on && W != null) Stop(W); };
            }

            public HotkeyRow(QuickLink link) : base(link.Name, null, link.Icon, Mac.Indigo, Mac.Blue)
            {
                this.link = link;
                action = ShotStack.LinkAction(link);
                Unloaded += delegate { if (on && W != null) Stop(W); };
            }

            internal string Action { get { return action; } }

            string Combos() { return link != null ? link.Keys : W.settings.HotkeysFor(action); }

            void Store(HomeWindow w, string combos)
            {
                if (link != null) w.owner.SetLinkKeys(link, combos);
                else w.settings.SetHotkeys(action, combos);
            }

            public override void Build()
            {
                Sub = null;
                string taken = null, took = null;
                if (!on)
                    foreach (string c in Hotkeys.Split(Combos()))
                    {
                        if (taken == null && Hotkeys.IsBusy(action, c)) taken = c;
                        if (took == null && Hotkeys.IsGrabbed(action, c)) took = c;
                    }
                if (taken != null)
                {
                    // Refused because another action of Stackshot has it (a settings file edited by hand), or another program.
                    string other = W.OwnerOfCombo(taken, action);
                    string who = W.KeyHolder(taken);
                    Sub = Hotkeys.DisplayOne(taken) + (other != null ? " tambi\u00E9n es de \u00AB" + other + "\u00BB"
                                                       : who != null ? " lo usa " + who : " lo est\u00E1 usando otro programa");
                }
                else if (took != null)
                {
                    // Taken on its way in: said quietly, so it is known why Windows' own tool no longer opens with it.
                    KeyOwners.Reserved r = KeyOwners.OfWindows(took);
                    Sub = Hotkeys.DisplayOne(took) + (r != null ? ", en lugar de " + r.Owner : ", antes que el programa que lo ten\u00EDa");
                }
                else if (link != null) Sub = Where(link);
                base.Build();
                if (taken != null && SubText != null) SubText.Foreground = Ds.Brush(Ds.Brushes.Warn);
            }

            // Where a link goes, short: the site, or the folder or file with its parent.
            static string Where(QuickLink l)
            {
                if (l.Web) return ShotStack.LinkName(l.Target);
                try
                {
                    string p = l.Target.TrimEnd('\\', '/');
                    string parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(p) ?? "");
                    return (parent.Length > 0 ? parent + "\\" : "") + System.IO.Path.GetFileName(p);
                }
                catch { return l.Target; }
            }

            protected override FrameworkElement Control()
            {
                Palette pal = Ds.Brushes;
                StopPulse();
                field = new TapBorder { Width = 236, Height = 32, CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Margin = new Thickness(0, 8, 0, 8),
                                        Do = Click, Help = on ? KeysPrompt : Hotkeys.Display(Combos()) };
                if (on)
                {
                    field.BorderThickness = new Thickness(2);
                    ShowLive();
                }
                else
                {
                    field.BorderThickness = new Thickness(1);
                    field.BorderBrush = Ds.Brush(pal.Hairline);
                    field.Background = Ds.Brush(pal.Control);
                    List<string> combos = Hotkeys.Split(Combos());
                    if (combos.Count == 0) field.Child = Center(Label("Sin atajo", Ds.Regular, 12.5, pal.Label3));
                    else
                    {
                        Grid g = new Grid();
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        g.ColumnDefinitions.Add(new ColumnDefinition());
                        FrameworkElement k = Fit(new Keycaps(Hotkeys.DisplayOne(combos[0]), 20), HorizontalAlignment.Right);
                        k.Margin = new Thickness(0, 0, 6, 0);
                        Grid.SetColumn(k, 1);
                        g.Children.Add(k);
                        // Extra combos written in the settings file are kept; say so, and list them on hover.
                        if (combos.Count > 1)
                        {
                            g.Children.Add(Label("y " + (combos.Count - 1) + " m\u00E1s", Ds.Regular, 11.5, pal.Label2, new Thickness(11, 0, 8, 0)));
                            List<string> more = new List<string>();
                            for (int i = 1; i < combos.Count; i++) more.Add(Hotkeys.DisplayOne(combos[i]));
                            field.ToolTip = "Tambi\u00E9n: " + string.Join(", ", more.ToArray());
                        }
                        field.Child = g;
                    }
                    field.MouseEnter += delegate { field.Background = Ds.Brush(pal.ControlHover); };
                    field.MouseLeave += delegate { field.Background = Ds.Brush(pal.Control); };
                }
                field.MouseLeftButtonUp += delegate { Click(); };
                return field;
            }

            static FrameworkElement Center(TextBlock t)
            {
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.TextTrimming = TextTrimming.CharacterEllipsis;
                t.Margin = new Thickness(6, 0, 6, 0);
                return t;
            }

            // Keycaps at their size, or a little smaller when a long combination would not fit the field.
            static FrameworkElement Fit(Keycaps k, HorizontalAlignment h)
            {
                return new Viewbox { Child = k, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = h, VerticalAlignment = VerticalAlignment.Center };
            }

            // The field's content while recording: the reason a combo was refused, the modifiers held, or the prompt.
            void ShowLive()
            {
                if (field == null || !on) return;
                Palette pal = Ds.Brushes;
                ModifierKeys m = Keyboard.Modifiers;
                // A pulsing ring in the accent while waiting, in orange while it shows why a combo was refused.
                Color ring = hint != null ? pal.Orange : pal.Accent;
                if (pulse == null || pulseColor != ring)
                {
                    StopPulse();
                    pulseColor = ring;
                    pulse = new SolidColorBrush(ring);
                    field.BorderBrush = pulse;
                    field.Background = Ds.Brush(Ds.WithAlpha(ring, 0.12));
                    ColorAnimation a = new ColorAnimation(Ds.WithAlpha(ring, 0.35), TimeSpan.FromMilliseconds(650));
                    a.AutoReverse = true;
                    // A few breaths, then a steady ring: a field left waiting doesn't keep redrawing the window.
                    a.RepeatBehavior = new RepeatBehavior(4);
                    pulse.BeginAnimation(SolidColorBrush.ColorProperty, a);
                }
                // Held modifiers repeat their key-down: only a change redraws the field.
                string state = hint != null ? "!" + hint : m.ToString();
                if (field.Child != null && state == liveShown) return;
                liveShown = state;
                if (hint != null)
                {
                    TextBlock t = Label(hint, Ds.Semibold, 12.5, pal.Warn);
                    field.ToolTip = hint;
                    field.Child = Center(t);
                    return;
                }
                field.ToolTip = null;
                if (m == ModifierKeys.None) { field.Child = Center(Label("Pulsa la combinaci\u00F3n\u2026", Ds.Semibold, 12.5, pal.Link)); return; }
                string held = Hotkeys.DisplayMods((m & ModifierKeys.Control) != 0, (m & ModifierKeys.Shift) != 0, (m & ModifierKeys.Alt) != 0, (m & ModifierKeys.Windows) != 0);
                FrameworkElement k = Fit(new Keycaps(held + " + \u2026", 20), HorizontalAlignment.Center);
                k.Margin = new Thickness(6, 0, 6, 0);
                field.Child = k;
            }

            void Listen(bool attach)
            {
                if (attach && live == null)
                {
                    live = delegate(object o, KeyEventArgs e)
                    {
                        Key k = e.Key == Key.System ? e.SystemKey : e.Key;
                        if (!on || !IsModifier(k)) return; // other keys reach Take()
                        if (e.IsDown) hint = null;
                        ShowLive();
                    };
                    // The window marks every key handled while recording, so listen to handled ones too.
                    W.AddHandler(Keyboard.PreviewKeyDownEvent, live, true);
                    W.AddHandler(Keyboard.PreviewKeyUpEvent, live, true);
                }
                else if (!attach && live != null)
                {
                    W.RemoveHandler(Keyboard.PreviewKeyDownEvent, live);
                    W.RemoveHandler(Keyboard.PreviewKeyUpEvent, live);
                    live = null;
                }
            }

            void StopPulse()
            {
                if (pulse == null) return;
                pulse.BeginAnimation(SolidColorBrush.ColorProperty, null);
                pulse = null;
            }

            // Starts listening, as a click on the field does (from the quick links window).
            internal void Listen() { if (!on && W != null) Click(); }

            void Click()
            {
                if (on) { Stop(W); return; }
                if (W.listening != null) W.listening.Stop(W);
                on = true;
                hint = null;
                W.listening = this;
                W.owner.SuspendHotkeys();
                // Keys Windows or another app would keep (Win + Shift + S opens the Snipping Tool) reach the field too.
                W.owner.RecordKeys(new System.Windows.Interop.WindowInteropHelper(W).Handle);
                Build();
                Listen(true);
                W.Wake();
            }

            public void Stop(HomeWindow w)
            {
                on = false;
                hint = null;
                Listen(false);
                StopPulse();
                if (w.listening == this) w.listening = null;
                w.owner.ResumeHotkeys();
                w.owner.RecordKeys(IntPtr.Zero);
                Build();
            }

            public void Take(HomeWindow w, WF.Keys keyData)
            {
                Take(w, keyData, (Keyboard.Modifiers & ModifierKeys.Windows) != 0);
            }

            // Like a classic hotkey box: Delete or Backspace clears it, Esc cancels; extra combos from the settings file
            // are kept. Combos that would break typing or a system shortcut everywhere are refused with the reason.
            public void Take(HomeWindow w, WF.Keys keyData, bool win)
            {
                WF.Keys key = keyData & WF.Keys.KeyCode, mods = keyData & WF.Keys.Modifiers;
                // Dead keys (the accent keys) and IME keys arrive without their code: ask Windows which one is down.
                if (key == WF.Keys.None || key == WF.Keys.ProcessKey || key == WF.Keys.Packet) key = Hotkeys.HeldKey();
                if (key == WF.Keys.None || Hotkeys.IsModifierKey(key)) return;
                if (key == WF.Keys.Escape) { Stop(w); return; }
                bool clear = mods == 0 && !win && (key == WF.Keys.Delete || key == WF.Keys.Back);
                string combo = "";
                if (!clear)
                {
                    string why = Hotkeys.Problem(key, mods, win);
                    // Refused: the ring breathes again, so even the same refusal twice is seen to have been heard.
                    if (why != null) { hint = why; StopPulse(); ShowLive(); return; }
                    combo = Hotkeys.ToSetting(key | mods, win);
                }
                List<string> list = Hotkeys.Split(Combos());
                if (list.Count == 0 && clear) { Stop(w); return; }
                if (!clear && list.Count > 0 && Hotkeys.Same(list[0], combo)) { Stop(w); return; } // unchanged
                if (list.Count > 0) list.RemoveAt(0);
                if (!clear)
                {
                    list.RemoveAll(delegate(string c) { return Hotkeys.Same(c, combo); });
                    list.Insert(0, combo);
                    // A combo maps to one action or link: take it from any other, and say so under the list.
                    w.TakeCombo(combo, action, Title);
                }
                Store(w, string.Join(", ", list.ToArray()));
                w.PruneKeysFirst();
                w.settings.Save();
                // Choosing Print Screen means wanting it: take it back from the Snipping Tool right away.
                bool freed = false;
                if (Hotkeys.Same(combo, "PrintScreen") && Installer.SnippingOwnsPrintScreen) { Installer.FreePrintScreen(); freed = true; }
                KeyOwners.Reserved windows = clear || freed ? null : KeyOwners.OfWindows(combo);
                Stop(w);
                if (freed)
                    w.owner.Notify("Impr Pant ya es de Stackshot", "Windows la ten\u00EDa reservada para Recortes; ya est\u00E1 liberada en tu usuario y responde Stackshot.");
                else if (windows != null && Hotkeys.IsGrabbed(action, combo))
                    w.owner.Notify(Hotkeys.DisplayOne(combo) + " ya es de Stackshot",
                                   windows.Owner + " lo usaba para " + windows.Does + ". Mientras Stackshot est\u00E9 abierto responde \u00E9l; si lo quitas de aqu\u00ED, vuelve a ser de Windows.");
                w.Rebuild();
            }
        }

        public class FolderRow : Row
        {
            public FolderRow() : base("", "Donde van las capturas que guardas.", "folder", D.Color.FromArgb(90, 200, 250), D.Color.FromArgb(0, 122, 255)) { }
            public override void Build()
            {
                Title = ShortPath(W.settings.SaveFolder);
                base.Build();
                ToolTip = W.settings.SaveFolder;
                ToolTipService.SetInitialShowDelay(this, 700);
            }
            protected override FrameworkElement Control()
            {
                StackPanel p = new StackPanel { Orientation = Orientation.Horizontal };
                MacButton open = new MacButton("Abrir", ButtonKind.Secondary);
                open.Click += delegate { W.owner.OpenFolder(); };
                MacButton change = new MacButton("Cambiar\u2026", ButtonKind.Secondary);
                change.Margin = new Thickness(8, 0, 0, 0);
                change.Click += delegate
                {
                    using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog())
                    {
                        d.Description = "\u00BFD\u00F3nde quieres guardar las capturas que conserves?";
                        d.ShowNewFolderButton = true;
                        try { Directory.CreateDirectory(W.settings.SaveFolder); d.SelectedPath = W.settings.SaveFolder; } catch { }
                        if (d.ShowDialog(W.Win32) == WF.DialogResult.OK) { W.settings.SaveFolder = d.SelectedPath; W.Changed(); Build(); }
                    }
                };
                p.Children.Add(open);
                p.Children.Add(change);
                return p;
            }
        }

        static int Nearest(int[] options, int v)
        {
            int best = 0;
            for (int i = 1; i < options.Length; i++) if (Math.Abs(options[i] - v) < Math.Abs(options[best] - v)) best = i;
            return best;
        }

        static string ShortPath(string p)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            bool inside = home.Length > 0 && p.StartsWith(home, StringComparison.OrdinalIgnoreCase) && (p.Length == home.Length || p[home.Length] == '\\');
            return inside ? "~" + p.Substring(home.Length) : p;
        }

        // "a", "a y b", "a, b y c".
        static string Listed(List<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1).ToArray()) + " y " + items[items.Count - 1];
        }

        // ---- Atajos

        void BuildKeys()
        {
            Header("Atajos", "Haz clic en un atajo y pulsa la combinaci\u00F3n que quieras, con Ctrl, Alt, May\u00FAs o Win. Supr lo quita y Esc cancela.");
            LoadKeyApps();
            Row[] rows = new Row[Settings.Actions.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = new HotkeyRow(Settings.Actions[i], i);
            Group(null, rows);
            if (keysMoved != null) { Note(keysMoved); keysMoved = null; }
            Section("accesos r\u00E1pidos", BuildLinkKeys);
            Section("atajos de Windows", BuildWindowsKeys);
            Section("atajos ocupados", BuildBusyKeys);
            Section("apps de captura", BuildCaptureApps);
            if (!DefaultHotkeys())
                NoteLink("", "Restaurar los atajos de serie", delegate
                {
                    CancelListening();
                    Settings d = new Settings();
                    foreach (string a in Settings.Actions) settings.SetHotkeys(a, d.HotkeysFor(a));
                    Changed();
                    owner.ResumeHotkeys();
                    Rebuild();
                });
        }

        bool DefaultHotkeys()
        {
            Settings d = new Settings();
            foreach (string a in Settings.Actions)
            {
                List<string> mine = Hotkeys.Split(settings.HotkeysFor(a)), def = Hotkeys.Split(d.HotkeysFor(a));
                if (mine.Count != def.Count) return false;
                for (int i = 0; i < mine.Count; i++) if (!Hotkeys.Same(mine[i], def[i])) return false;
            }
            return true;
        }

        // Windows 11 opens the Snipping Tool with Print Screen, and some shortcut here uses it on its own.
        bool PrintScreenToSnipping()
        {
            bool uses = false;
            foreach (string a in Settings.Actions)
                foreach (string c in Hotkeys.Split(settings.HotkeysFor(a)))
                    if (Hotkeys.Same(c, "PrintScreen")) uses = true;
            return uses && Installer.SnippingOwnsPrintScreen;
        }

        void FreePrintScreen()
        {
            Installer.FreePrintScreen();
            owner.ResumeHotkeys();
            Rebuild();
            owner.Notify("Impr Pant ya es de Stackshot", "Windows la ten\u00EDa reservada para Recortes; ya est\u00E1 liberada en tu usuario y responde Stackshot.");
        }

        // Windows' capture shortcuts: Print Screen for the Snipping Tool, and the Win ones Stackshot can answer instead.
        void BuildWindowsKeys()
        {
            List<Row> rows = new List<Row>();
            if (PrintScreenToSnipping())
                rows.Add(new ButtonRow("Impr Pant abre Recortes de Windows", "Para que capture con Stackshot hay que liberarla. Solo cambia en tu usuario y responde al momento.",
                                       "keyboard", Mac.Orange, Mac.Red, "Liberar", FreePrintScreen));
            List<string> missing = new List<string>(), used = new List<string>();
            foreach (string[] pair in KeyOwners.WindowsSet) (ActionOf(pair[0]) >= 0 ? used : missing).Add(Hotkeys.DisplayOne(pair[0]));
            string all = Listed(AllWindowsSet());
            if (missing.Count == 0)
                rows.Add(new ButtonRow("Los atajos de Windows abren Stackshot", all + " capturan y graban con Stackshot en lugar de Recortes.",
                                       "keyboard", Mac.Indigo, Mac.Blue, "Devolver", ReturnWindowsKeys));
            else
                rows.Add(new ButtonRow("Usar los atajos de Windows", "Win + May\u00FAs + S captura un \u00E1rea, Win + May\u00FAs + R graba la pantalla y Win + Impr Pant la captura entera, con Stackshot en lugar de Recortes." +
                                       (used.Count > 0 ? " Ya usas " + Listed(used) + "." : ""),
                                       "keyboard", Mac.Indigo, Mac.Blue, "Usar", UseWindowsKeys));
            Group("Atajos de Windows", rows.ToArray());
            Note("Cualquier atajo de captura de Windows que pongas arriba pasa a Stackshot mientras est\u00E9 abierto; si lo quitas, vuelve a ser de Windows.");
        }

        List<string> AllWindowsSet()
        {
            List<string> o = new List<string>();
            foreach (string[] pair in KeyOwners.WindowsSet) o.Add(Hotkeys.DisplayOne(pair[0]));
            return o;
        }

        // The action a combo is on, or -1.
        int ActionOf(string combo)
        {
            for (int i = 0; i < Settings.Actions.Length; i++)
                foreach (string c in Hotkeys.Split(settings.HotkeysFor(Settings.Actions[i])))
                    if (Hotkeys.Same(c, combo)) return i;
            return -1;
        }

        // The quick link a combo is on, or null.
        QuickLink LinkWith(string combo)
        {
            foreach (QuickLink l in owner.Links)
                foreach (string c in Hotkeys.Split(l.Keys))
                    if (Hotkeys.Same(c, combo)) return l;
            return null;
        }

        // The name of what else has a combo (an action or a quick link other than except), or null.
        internal string OwnerOfCombo(string combo, string except)
        {
            for (int i = 0; i < Settings.Actions.Length; i++)
            {
                if (Settings.Actions[i] == except) continue;
                foreach (string c in Hotkeys.Split(settings.HotkeysFor(Settings.Actions[i])))
                    if (Hotkeys.Same(c, combo)) return ActionNames[i];
            }
            foreach (QuickLink l in owner.Links)
            {
                if (ShotStack.LinkAction(l) == except) continue;
                foreach (string c in Hotkeys.Split(l.Keys))
                    if (Hotkeys.Same(c, combo)) return l.Name;
            }
            return null;
        }

        // A combo given to one action or link leaves whatever else had it; the note under the list says so.
        internal void TakeCombo(string combo, string to, string toName)
        {
            for (int i = 0; i < Settings.Actions.Length; i++)
            {
                string a = Settings.Actions[i];
                if (a == to) continue;
                List<string> others = Hotkeys.Split(settings.HotkeysFor(a));
                if (others.RemoveAll(delegate(string o) { return Hotkeys.Same(o, combo); }) == 0) continue;
                settings.SetHotkeys(a, string.Join(", ", others.ToArray()));
                keysMoved = Hotkeys.DisplayOne(combo) + " era de \u00AB" + ActionNames[i] + "\u00BB" +
                            (others.Count == 0 ? ", que se ha quedado sin atajo." : "; ahora es de \u00AB" + toName + "\u00BB.");
            }
            foreach (QuickLink l in owner.Links)
            {
                if (ShotStack.LinkAction(l) == to) continue;
                List<string> others = Hotkeys.Split(l.Keys);
                if (others.RemoveAll(delegate(string o) { return Hotkeys.Same(o, combo); }) == 0) continue;
                owner.SetLinkKeys(l, string.Join(", ", others.ToArray()));
                keysMoved = Hotkeys.DisplayOne(combo) + " era del acceso \u00AB" + l.Name + "\u00BB" +
                            (others.Count == 0 ? ", que se ha quedado sin atajo." : "; ahora es de \u00AB" + toName + "\u00BB.");
            }
        }

        // ---- Quick links: a shortcut for each

        void BuildLinkKeys()
        {
            List<QuickLink> links = owner.Links;
            if (links.Count == 0)
            {
                Group("Accesos r\u00E1pidos", new ButtonRow("Accesos r\u00E1pidos", "Webs, carpetas, archivos o programas que se abren con un atajo, adem\u00E1s de desde el men\u00FA de la mascota.",
                                                            "open", Mac.Indigo, Mac.Blue, "A\u00F1adir\u2026", delegate { LinksWindow.Show(owner); }));
                return;
            }
            Row[] rows = new Row[links.Count];
            for (int i = 0; i < links.Count; i++) rows[i] = new HotkeyRow(links[i]);
            Group("Accesos r\u00E1pidos", rows);
            bool bare = links.Exists(delegate(QuickLink l) { return l.Keys.Length == 0; });
            TextBlock n = Paragraph("Pulsa su atajo y se abre, est\u00E9s donde est\u00E9s. ", 12, Ds.Brushes.Label2);
            n.Margin = new Thickness(8, -14, 8, 24);
            n.Inlines.Add(InlineLink("A\u00F1adir o quitar accesos", delegate { LinksWindow.Show(owner); }));
            if (bare)
            {
                n.Inlines.Add(new System.Windows.Documents.Run(" \u00B7 "));
                n.Inlines.Add(InlineLink("Ponerles Ctrl + May\u00FAs + Alt + 1, 2, 3\u2026", NumberLinks));
            }
            body.Children.Add(n);
        }

        // Each link without a shortcut gets Ctrl+Shift+Alt and the next free digit (1 to 9, then 0), skipping the
        // combinations already used and those this keyboard types a character with.
        internal void NumberLinks()
        {
            CancelListening();
            List<string> given = new List<string>();
            int digit = 1;
            foreach (QuickLink l in owner.Links)
            {
                if (l.Keys.Length > 0) continue;
                string pick = null;
                for (; digit <= 10 && pick == null; digit++)
                {
                    string c = "Ctrl+Shift+Alt+" + (digit % 10);
                    WF.Keys key = WF.Keys.D0 + (digit % 10);
                    if (ActionOf(c) >= 0 || LinkWith(c) != null) continue;
                    if (Hotkeys.Problem(key, WF.Keys.Control | WF.Keys.Shift | WF.Keys.Alt, false) != null) continue;
                    pick = c;
                }
                if (pick == null) break;
                owner.SetLinkKeys(l, pick);
                given.Add("\u00AB" + l.Name + "\u00BB " + Hotkeys.DisplayOne(pick));
            }
            owner.ResumeHotkeys();
            Rebuild();
            if (given.Count == 0) owner.Notify("No quedan atajos libres", "Las combinaciones Ctrl + May\u00FAs + Alt + n\u00FAmero ya est\u00E1n todas en uso: ponle otra a mano.");
            else owner.Notify(given.Count == 1 ? "Acceso con atajo" : "Accesos con atajo", string.Join(", ", given.ToArray()) + ".");
        }

        // From the quick links window: Atajos opens with that link's field already listening.
        internal void RecordLink(QuickLink l)
        {
            string id = ShotStack.LinkAction(l);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(delegate
            {
                List<Row> rows = new List<Row>();
                FindRows(body, rows);
                foreach (Row r in rows)
                {
                    HotkeyRow h = r as HotkeyRow;
                    if (h == null || h.Action != id) continue;
                    h.BringIntoView();
                    h.Listen();
                    return;
                }
            }));
        }

        // Each Windows shortcut joins its action as one more combo (the user's own stay first), unless it is already on one.
        void UseWindowsKeys()
        {
            CancelListening();
            List<string> added = new List<string>();
            foreach (string[] pair in KeyOwners.WindowsSet)
            {
                if (ActionOf(pair[0]) >= 0 || LinkWith(pair[0]) != null) continue;
                List<string> l = Hotkeys.Split(settings.HotkeysFor(pair[1]));
                l.Add(pair[0]);
                settings.SetHotkeys(pair[1], string.Join(", ", l.ToArray()));
                added.Add(pair[0]);
            }
            Changed();
            owner.ResumeHotkeys();
            Rebuild();
            List<string> works = new List<string>();
            foreach (string c in added) if (Hotkeys.IsGrabbed(Settings.Actions[ActionOf(c)], c)) works.Add(Hotkeys.DisplayOne(c));
            if (works.Count > 0)
                owner.Notify("Atajos de Windows en Stackshot", Listed(works) + " ya abren Stackshot. Mientras est\u00E9 abierto responde \u00E9l; con \u00ABDevolver\u00BB vuelven a Recortes.");
        }

        void ReturnWindowsKeys()
        {
            CancelListening();
            foreach (string a in Settings.Actions)
            {
                List<string> l = Hotkeys.Split(settings.HotkeysFor(a));
                int n = l.Count;
                l.RemoveAll(delegate(string c) { foreach (string[] pair in KeyOwners.WindowsSet) if (Hotkeys.Same(c, pair[0])) return true; return false; });
                if (l.Count != n) settings.SetHotkeys(a, string.Join(", ", l.ToArray()));
            }
            PruneKeysFirst();
            Changed();
            owner.ResumeHotkeys();
            Rebuild();
        }

        // Combos another program also has: who it is and the choice to answer them first; and those Stackshot already
        // answers first, to give back.
        void BuildBusyKeys()
        {
            List<Row> rows = new List<Row>();
            // The actions and the quick links with a shortcut: id, name shown, combos.
            List<string[]> keyed = new List<string[]>();
            for (int i = 0; i < Settings.Actions.Length; i++) keyed.Add(new string[] { Settings.Actions[i], ActionNames[i], settings.HotkeysFor(Settings.Actions[i]) });
            foreach (QuickLink l in owner.Links) if (l.Keys.Length > 0) keyed.Add(new string[] { ShotStack.LinkAction(l), l.Name, l.Keys });
            foreach (string[] k in keyed)
            {
                string a = k[0], name = k[1];
                foreach (string c in Hotkeys.Split(k[2]))
                {
                    string combo = c, keys = Hotkeys.DisplayOne(c);
                    if (Hotkeys.IsBusy(a, c) && KeyOwners.OfWindows(c) == null && !InKeysFirst(c))
                    {
                        string who = KeyHolder(c);
                        rows.Add(new ButtonRow(keys + " no llega a \u00AB" + name + "\u00BB",
                                               (who != null ? "Lo usa " + who + "." : "Lo usa otro programa.") + " Stackshot puede responder antes, sin cerrarlo.",
                                               "warning", Mac.Orange, Mac.Red, "Usar en Stackshot", delegate { SetKeysFirst(combo, true); }));
                    }
                    else if (InKeysFirst(c))
                    {
                        bool works = Hotkeys.IsGrabbed(a, c);
                        string who = KeyHolder(c);
                        rows.Add(new ButtonRow(keys + (works ? " responde en Stackshot" : " sigue ocupado"),
                                               works ? "Antes que " + (who ?? "el programa que lo ten\u00EDa") + ", que ya no lo recibe." : "Windows no deja a Stackshot responder antes: elige otro atajo o cierra " + (who ?? "el otro programa") + ".",
                                               works ? "keyboard" : "warning", works ? Mac.Green : Mac.Orange, works ? Mac.Teal : Mac.Red, "Dej\u00E1rselo", delegate { SetKeysFirst(combo, false); }));
                    }
                }
            }
            if (rows.Count == 0) return;
            Group("Atajos que tiene otra app", rows.ToArray());
        }

        bool InKeysFirst(string combo)
        {
            return Hotkeys.Split(settings.KeysFirst).Exists(delegate(string c) { return Hotkeys.Same(c, combo); });
        }

        void SetKeysFirst(string combo, bool on)
        {
            CancelListening();
            List<string> l = Hotkeys.Split(settings.KeysFirst);
            l.RemoveAll(delegate(string c) { return Hotkeys.Same(c, combo); });
            if (on) l.Add(Hotkeys.Normalize(combo));
            settings.KeysFirst = string.Join(", ", l.ToArray());
            Changed();
            owner.ResumeHotkeys();
            Rebuild();
        }

        // Combos Stackshot answers first that no action or link uses any more are forgotten.
        internal void PruneKeysFirst()
        {
            List<string> l = Hotkeys.Split(settings.KeysFirst);
            if (l.RemoveAll(delegate(string c) { return ActionOf(c) < 0 && LinkWith(c) == null; }) > 0) settings.KeysFirst = string.Join(", ", l.ToArray());
        }

        // ---- Other capture apps

        List<KeyOwners.App> keyApps;
        Dictionary<string, ImageSource> keyAppIcons = new Dictionary<string, ImageSource>();
        DateTime keyAppsAt;
        bool keyAppsLoading;

        // Which capture apps are open, looked up in the background when the page opens (at most every few seconds); the
        // page is built again with them.
        void LoadKeyApps()
        {
            if (keyAppsLoading || (keyApps != null && (DateTime.UtcNow - keyAppsAt).TotalSeconds < 4)) return;
            keyAppsLoading = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeyOwners.App> found = null;
                Dictionary<string, ImageSource> icons = new Dictionary<string, ImageSource>();
                try
                {
                    found = KeyOwners.Running();
                    foreach (KeyOwners.App a in found)
                    {
                        if (a.Exe == null || icons.ContainsKey(a.Exe)) continue;
                        try
                        {
                            using (D.Icon ic = D.Icon.ExtractAssociatedIcon(a.Exe))
                            {
                                BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                b.Freeze();
                                icons[a.Exe] = b;
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex) { ShotStack.Log("Apps de captura: " + ex.Message); }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    keyAppsLoading = false;
                    bool changed = !SameApps(keyApps, found);
                    keyApps = found ?? new List<KeyOwners.App>();
                    keyAppIcons = icons;
                    keyAppsAt = DateTime.UtcNow;
                    if (changed && page == "keys" && listening == null) Rebuild();
                }));
            });
        }

        static bool SameApps(List<KeyOwners.App> a, List<KeyOwners.App> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i].Name != b[i].Name || a[i].Pids.Count != b[i].Pids.Count) return false;
            return true;
        }

        // Who most likely holds a combo Stackshot could not register (Windows itself or an open capture app), or null.
        internal string KeyHolder(string combo)
        {
            KeyOwners.Reserved r = KeyOwners.OfWindows(combo);
            if (r != null) return r.Owner;
            return KeyOwners.Holder(combo, keyApps);
        }

        void BuildCaptureApps()
        {
            if (keyApps == null || keyApps.Count == 0) return;
            List<Row> rows = new List<Row>();
            foreach (KeyOwners.App a in keyApps)
            {
                // OneDrive and Dropbox only matter when Print Screen is busy and nothing surer has it.
                if (a.Maybe && !MaybeMatters(a)) continue;
                rows.Add(new AppRow(a, a.Exe != null && keyAppIcons.ContainsKey(a.Exe) ? keyAppIcons[a.Exe] : null));
            }
            if (rows.Count == 0) return;
            Group("Otras apps de captura abiertas", rows.ToArray());
            NoteLink("Cerrar una solo la cierra ahora. Para que no se abra al encender el equipo:", "Aplicaciones de inicio", KeyOwners.OpenStartupApps);
        }

        bool MaybeMatters(KeyOwners.App app)
        {
            foreach (string a in Settings.Actions)
                foreach (string c in Hotkeys.Split(settings.HotkeysFor(a)))
                    if (Hotkeys.IsBusy(a, c) && app.Keys.Exists(delegate(string k) { return Hotkeys.Same(k, c); }) && KeyOwners.Holder(c, keyApps) == app.Name) return true;
            return false;
        }

        // An open capture app: what it uses, and closing it (gently first; forcing it only on a second, explicit click).
        public class AppRow : Row
        {
            readonly KeyOwners.App app;
            int state;      // 0 open, 1 closing, 2 would not close (the button now forces it)

            public AppRow(KeyOwners.App app, ImageSource icon) : base(app.Name, null, "app", Mac.Teal, Mac.Blue)
            {
                this.app = app;
                Picture = icon;
            }

            public override void Build()
            {
                Sub = Describe();
                base.Build();
                if (state == 2 && SubText != null) SubText.Foreground = Ds.Brush(Ds.Brushes.Warn);
            }

            string Describe()
            {
                if (state == 2) return "No se ha cerrado sola. Forzarlo pierde lo que tenga sin guardar.";
                List<string> shown = new List<string>();
                foreach (string k in app.Keys) if (shown.Count < 3) shown.Add(Hotkeys.DisplayOne(k));
                string keys = shown.Count == 0 ? "" : Listed(shown) + (app.Keys.Count > 3 ? " y " + (app.Keys.Count - 3) + " m\u00E1s" : "");
                if (app.Maybe) return "Puede quedarse " + keys + " si guarda capturas: desact\u00EDvalo en sus ajustes.";
                return keys.Length == 0 ? "Abierta" : "Abierta \u00B7 " + (app.FromSettings ? "usa " : "suele usar ") + keys;
            }

            protected override FrameworkElement Control()
            {
                if (app.Maybe) return null;
                MacButton b = new MacButton(state == 1 ? "Cerrando\u2026" : state == 2 ? "Forzar cierre" : "Cerrar", state == 2 ? ButtonKind.Danger : ButtonKind.Secondary);
                b.IsEnabled = state != 1;
                b.Click += delegate { Close(); };
                return b;
            }

            void Close()
            {
                bool force = state == 2;
                state = 1;
                Build();
                HomeWindow w = W;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    string error = null;
                    bool gone = force ? KeyOwners.CloseNow(app, out error) : KeyOwners.CloseGently(app, 2500);
                    w.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (!gone)
                        {
                            state = 2;
                            Build();
                            if (error != null) w.owner.Notify("No se ha podido cerrar " + app.Name, error);
                            return;
                        }
                        ShotStack.Log("Cerrada otra app de captura: " + app.Name);
                        w.keyAppsAt = DateTime.MinValue;
                        w.owner.ResumeHotkeys();
                        w.Rebuild();
                        w.owner.Notify(app.Name + " cerrada", "Sus atajos quedan libres para Stackshot.");
                    }));
                });
            }
        }

        // ---- General

        // Presets for the settings that weigh on performance; everything else stays as the user left it. Rendimiento has no
        // mascot at all (Settings.MascotOn), Equilibrado keeps it inside the window, Completo also on the desktop.
        void ApplyProfile(int p)
        {
            settings.Profile = p;
            settings.ShowIntro = p > 0;
            settings.MascotTalks = p > 0;
            if (p > 0) settings.MascotOn = true;
            settings.MascotDesktop = p == 2;
            // Rendimiento keeps everything inside the window: nothing pops up outside it. Video is never touched.
            if (p == 0) { settings.WellToasts = false; settings.HealthTalk = false; ReleaseMascot(); }
            owner.MascotChanged();
            owner.ApplySettings();
            RefreshNav();
            BuildWidget();
        }

        TextBlock profileNote;

        // Under the profile: what it is for or, once something it sets has been changed by hand, what and a link to apply
        // it again.
        void PaintProfileNote()
        {
            if (profileNote == null) return;
            int p = Math.Max(0, Math.Min(2, settings.Profile));
            List<string> changed = ProfileChanges(p);
            profileNote.Inlines.Clear();
            if (changed.Count == 0)
            {
                profileNote.Inlines.Add(new System.Windows.Documents.Run("Un perfil solo cambia unos pocos ajustes de golpe; despu\u00E9s puedes retocar cualquiera."));
                return;
            }
            // "Respecto a este perfil": most people never chose it, it came selected.
            profileNote.Inlines.Add(new System.Windows.Documents.Run("Has cambiado " + Listed(changed) + " respecto a este perfil. "));
            profileNote.Inlines.Add(InlineLink("Volver a aplicar \u00AB" + ProfileNames[p] + "\u00BB", delegate
            {
                ApplyProfile(p);
                Changed();
                SyncRows();
            }));
        }

        // What has been changed by hand since the profile was chosen.
        List<string> ProfileChanges(int p)
        {
            List<string> l = new List<string>();
            if (settings.ShowIntro != (p > 0)) l.Add("la animaci\u00F3n al abrir");
            if (settings.MascotTalks != (p > 0) && !Policy.MascotForced) l.Add("los saludos de la mascota");
            if (settings.MascotDesktop != (p == 2) && !Policy.MascotForced) l.Add("la mascota en el escritorio");
            if (p == 0 && settings.WellToasts) l.Add("los avisos de pausas");
            if (p == 0 && settings.HealthTalk) l.Add("los comentarios de actividad");
            return l;
        }

        void BuildGeneral()
        {
            Header("General", "C\u00F3mo arranca Stackshot y qu\u00E9 hace cada vez que capturas.");
            string[] pdesc =
            {
                "Lo justo para capturar: sin mascota (ni en la ventana ni en el escritorio, y sin gastar nada en ella), sin animaci\u00F3n al abrir y sin avisos por fuera. Ideal para equipos justos.",
                "El equilibrio de siempre: animaciones suaves y la mascota solo dentro de Stackshot.",
                "Todo activado: la mascota tambi\u00E9n paseando por el escritorio, sus saludos y los avisos de salud."
            };
            // Choosing a profile moves the switches it touches on this page too (no rebuild: the choice keeps sliding).
            Group("Perfil",
                new SegRow("Perfil r\u00E1pido", null, "gear", Gray1, Gray2, ProfileNames,
                           delegate { return settings.Profile; }, delegate(int i) { ApplyProfile(i); })
                    { SubNow = delegate { return pdesc[Math.Max(0, Math.Min(2, settings.Profile))]; } });
            profileNote = Paragraph("", 12, Ds.Brushes.Label2);
            profileNote.Margin = new Thickness(8, -14, 8, 24);
            Add(profileNote);
            PaintProfileNote();
            Group("Apariencia",
                new SegRow("Tema", "Claro u oscuro, o el mismo que tenga Windows en cada momento.", "sparkle", Mac.Indigo, Mac.Blue,
                           new string[] { "Como Windows", "Claro", "Oscuro" },
                           delegate { return settings.Appearance; }, delegate(int i) { settings.Appearance = i; }),
                new SegRow("Vista de Inicio", "C\u00F3mo se ve la p\u00E1gina principal.", "home", Mac.Blue, Mac.Indigo,
                           HomeStyles, delegate { return settings.HomeStyle; }, delegate(int i) { settings.HomeStyle = i; }));
            Group("Arranque",
                new ToggleRow("Iniciar con Windows", "Arranca en segundo plano al encender el equipo.", "power", Mac.Green, D.Color.FromArgb(0, 160, 90),
                              delegate { return Installer.StartupEnabled; }, delegate(bool v) { Installer.SetStartup(v); }),
                new ToggleRow("Seguir en la bandeja al cerrar", "Al cerrar esta ventana, Stackshot sigue funcionando junto al reloj.", "stack", Mac.Blue, Mac.Indigo,
                              delegate { return settings.CloseToTray; }, delegate(bool v) { settings.CloseToTray = v; }),
                new ToggleRow("Animaci\u00F3n al abrir", "La bienvenida con el logo cuando abres Stackshot.", "sparkle", Mac.Purple, Mac.Indigo,
                              delegate { return settings.ShowIntro; }, delegate(bool v) { settings.ShowIntro = v; }));
            Group("Actualizaciones", UpdateRows());
            Group("Archivos",
                new ToggleRow("Abrir .md con Stackshot", null, "markdown", Mac.Indigo, Mac.Purple,
                              delegate { return FileAssoc.Registered(FileAssoc.Markdown); }, delegate(bool v) { SetAssoc(FileAssoc.Markdown, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Markdown, ".md"); } },
                new ToggleRow("Abrir .xml con Stackshot", null, "doc", Mac.Teal, Mac.Blue,
                              delegate { return FileAssoc.Registered(FileAssoc.Xml); }, delegate(bool v) { SetAssoc(FileAssoc.Xml, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Xml, ".xml"); } },
                new ToggleRow("Abrir .mkv con Stackshot", null, "video", D.Color.FromArgb(64, 156, 255), D.Color.FromArgb(10, 110, 230),
                              delegate { return FileAssoc.Registered(FileAssoc.Video); }, delegate(bool v) { SetAssoc(FileAssoc.Video, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Video, ".mkv"); } },
                new ToggleRow("Abrir PDF con Stackshot", null, "book", Mac.Red, Mac.Orange,
                              delegate { return FileAssoc.Registered(FileAssoc.Pdf); }, delegate(bool v) { SetAssoc(FileAssoc.Pdf, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Pdf, ".pdf"); } },
                new ToggleRow("Abrir texto y logs con Stackshot", null, "text", Gray1, Gray2,
                              delegate { return FileAssoc.Registered(FileAssoc.Text); }, delegate(bool v) { SetAssoc(FileAssoc.Text, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Text, ".txt"); } },
                new ToggleRow("Abrir JSON y CSV con Stackshot", null, "doc", Mac.Green, Mac.Teal,
                              delegate { return FileAssoc.Registered(FileAssoc.Data); }, delegate(bool v) { SetAssoc(FileAssoc.Data, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Data, ".json"); } },
                new ToggleRow("Abrir im\u00E1genes con Stackshot", null, "photo", Mac.Blue, Mac.Teal,
                              delegate { return FileAssoc.Registered(FileAssoc.Image); }, delegate(bool v) { SetAssoc(FileAssoc.Image, v); })
                    { SubNow = delegate { return AssocNote(FileAssoc.Image, ".png"); } },
                new ButtonRow("Aplicaciones predeterminadas de Windows", "Ah\u00ED eliges qu\u00E9 programa abre cada tipo de archivo; Stackshot nunca lo cambia por su cuenta.", "gear", Gray1, Gray2, "Abrir", FileAssoc.OpenDefaultApps));
            // Shown only while there is something in it: recordings saved from a crash or a failed final pass.
            ButtonRow recovered = null;
            long recoveredBytes = WorkFiles.Bytes(ShotStack.RecoveredDir);
            if (recoveredBytes > 0)
                recovered = new ButtonRow("Grabaciones recuperadas", Recents.Size(recoveredBytes) + " de grabaciones que no llegaron a terminar. Stackshot nunca las borra: \u00E1brelas, gu\u00E1rdalas o b\u00F3rralas t\u00FA.",
                                          "folder", Mac.Orange, Mac.Red, "Abrir", OpenRecovered);
            ButtonRow print = null;
            if (PrintScreenToSnipping())
                print = new ButtonRow("Usar la tecla Impr Pant", "Ahora la usa Recortes de Windows. Se cambia solo en tu usuario.", "keyboard", Mac.Orange, Mac.Red, "Usar", FreePrintScreen);
            Group("Recientes",
                new ToggleRow("Mostrar capturas recientes en Inicio", "Desact\u00EDvalo y Inicio no muestra ni carga miniaturas.", "photo", Mac.Blue, Mac.Indigo,
                              delegate { return settings.ShowRecent; }, delegate(bool v) { settings.ShowRecent = v; Recents.Forget(); }),
                new ToggleRow("Buscar por el texto de las capturas", "Stackshot lee el texto de cada captura en tu equipo para que el Historial la encuentre por lo que pon\u00EDa.", "search", Mac.Indigo, Mac.Blue,
                              delegate { return settings.HistorySearch; }, delegate(bool v) { settings.HistorySearch = v; HistoryIndex.Apply(settings); }),
                HistoryIndexRowIfAny(),
                new SegRow("Guardar las temporales", "Cu\u00E1nto duran las capturas que no guardas en tu carpeta.", "stack", Mac.Teal, Mac.Blue, new string[] { "1 hora", "Al cerrar", "1 d\u00EDa", "7 d\u00EDas", "30 d\u00EDas" },
                           delegate { return settings.TempKeep; }, delegate(int i) { settings.TempKeep = i; owner.Sweep(); }),
                new ButtonRow("Borrar recientes", "Quita las capturas temporales; las de tu carpeta no se tocan.", "trash", Mac.Red, Mac.Orange, "Borrar", ClearRecent),
                recovered);
            Group("Al capturar",
                new SegRow("Despu\u00E9s de seleccionar un \u00E1rea", "Directo la env\u00EDa a la pila; Ajustar antes te deja moverla y redimensionarla viendo c\u00F3mo quedar\u00E1, y la capturas con Enter; con opciones adem\u00E1s eliges captura, v\u00EDdeo, GIF o desplazamiento.", "area", Mac.Blue, Mac.Indigo,
                           new string[] { "Directo a la pila", "Ajustar antes", "Mostrar opciones" },
                           delegate { return settings.AllInOne ? 2 : settings.AdjustFirst ? 1 : 0; },
                           delegate(int i) { settings.AllInOne = i == 2; settings.AdjustFirst = i == 1; }),
                new ToggleRow("Sonido de c\u00E1mara", "Un clic suave cada vez que capturas.", "sound", Mac.Pink, Mac.Red,
                              delegate { return settings.Sound; }, delegate(bool v) { settings.Sound = v; if (v) Shutter.Play(); }),
                new ToggleRow("Copiar cada captura", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla; los v\u00EDdeos y GIF, como archivo.", "copy", Mac.Teal, Mac.Blue,
                              delegate { return settings.CopyToClipboard; }, delegate(bool v) { settings.CopyToClipboard = v; }),
                new SegRow("Esconder las miniaturas", "Si no las usas, se apartan al borde y solo asoma un trocito; al pasar el rat\u00F3n por encima vuelven.", "stack", Mac.Purple, Mac.Indigo,
                           new string[] { "No", "A los 3 s", "A los 5 s", "A los 10 s" },
                           delegate { int i = Array.IndexOf(Settings.TuckSeconds, settings.StackTuck); return i < 0 ? 2 : i; },
                           delegate(int i) { settings.StackTuck = Settings.TuckSeconds[Math.Max(0, Math.Min(Settings.TuckSeconds.Length - 1, i))]; owner.Untuck(); }),
                new SegRow("Captura con temporizador", "Eliges el \u00E1rea y cuenta atr\u00E1s antes de capturar: da tiempo a abrir un men\u00FA o un aviso. Esc la cancela.", "clock", Mac.Orange, Mac.Red,
                           new string[] { "3 s", "5 s", "10 s" },
                           delegate { return settings.CaptureDelay == 10 ? 2 : settings.CaptureDelay == 5 ? 1 : 0; },
                           delegate(int i) { settings.CaptureDelay = i == 2 ? 10 : i == 1 ? 5 : 3; }),
                new ToggleRow("Seguir al rat\u00F3n entre pantallas", "Con varias pantallas, las miniaturas van a la del rat\u00F3n.", "screen", Mac.Indigo, Mac.Purple,
                              delegate { return settings.FollowMouse; }, delegate(bool v) { settings.FollowMouse = v; }),
                new ToggleRow("Incluir el puntero", "El puntero del rat\u00F3n aparece en la captura, donde estaba al empezarla.", "cursor", Mac.Blue, Mac.Indigo,
                              delegate { return settings.CaptureCursor; }, delegate(bool v) { settings.CaptureCursor = v; }),
                print);
            Group("Datos sensibles",
                new ToggleRow("Tapar los datos sensibles al capturar", "DNI, NIE, n\u00FAmero de la Seguridad Social, IBAN, tarjetas, correos y tel\u00E9fonos se tapan solos en cada captura. Se leen en tu equipo; nada sale de \u00E9l.", "redact!", Mac.Red, Mac.Orange,
                              delegate { return settings.SensitiveAuto; }, delegate(bool v) { settings.SensitiveAuto = v; }));
            Note("Apagado, decides t\u00FA: en el editor, \u00ABDatos sensibles\u00BB los tapa con marcas que puedes quitar.");
            Group("Capturas guardadas",
                new ToggleRow("Guardar todas las capturas", "Cada captura y grabaci\u00F3n va tambi\u00E9n a tu carpeta. Apagado, solo las que guardas con Guardar.", "folder", Mac.Blue, Mac.Teal,
                              delegate { return settings.SaveAll; }, delegate(bool v) { settings.SaveAll = v; }),
                new FolderRow(), new NameTemplateRow(), SubfolderRow());
            Note("Lo que no est\u00E1 en tu carpeta se borra solo seg\u00FAn lo elegido en Recientes (nunca mientras tenga miniatura o est\u00E9 en el editor).");
            BuildAdvanced();
        }

        // The folder watch: only where it is already set up or the organization sets its folder; elsewhere nothing at all.
        void BuildAdvanced()
        {
            if (!XmlWatch.Offered(settings)) return;
            Group("Avanzado",
                new ToggleRow("Abrir los XML nuevos de una carpeta", null, "doc", Mac.Teal, Mac.Blue,
                              delegate { return settings.XmlWatch; }, delegate(bool v) { settings.XmlWatch = v; })
                    { SubNow = XmlWatchNote },
                new XmlFolderRow { When = delegate { return settings.XmlWatch; } });
            Note("Pensado para programas que dejan cada mensaje SOAP como un archivo XML en una carpeta. En el visor, [ y ] pasan al XML anterior o al siguiente, y \u00ABM\u00E1s opciones\u00BB lista los \u00FAltimos que han llegado.");
        }

        string XmlWatchNote()
        {
            if (!settings.XmlWatch) return "Cada XML que aparezca en esa carpeta se abre solo en el visor de Stackshot, delante de todo.";
            string dir = XmlWatch.FolderFor(settings);
            if (dir == null) return "Elige abajo la carpeta que vigilar.";
            bool exists = false;
            try { exists = Directory.Exists(dir); } catch { }
            if (!exists) return "La carpeta no existe todav\u00EDa; Stackshot la vuelve a buscar cada minuto.";
            return "Activo: los XML nuevos se abren solos en el visor, delante de todo.";
        }

        // The folder whose new XML files open in the viewer: the organization's, or the one chosen here.
        public class XmlFolderRow : Row
        {
            public XmlFolderRow() : base("", null, "folder", D.Color.FromArgb(90, 200, 250), D.Color.FromArgb(0, 122, 255)) { }
            public override void Build()
            {
                string dir = XmlWatch.FolderFor(W.settings);
                Title = dir != null ? ShortPath(dir) : "Ninguna carpeta";
                Sub = XmlWatch.Preset() != null ? Policy.Managed + "." : dir != null ? "Carpeta elegida por ti." : "Elige la carpeta donde aparecen los XML.";
                base.Build();
                if (dir != null) ToolTip = dir;
                ToolTipService.SetInitialShowDelay(this, 700);
            }
            protected override FrameworkElement Control()
            {
                StackPanel p = new StackPanel { Orientation = Orientation.Horizontal };
                if (XmlWatch.Preset() != null) return p;
                MacButton change = new MacButton(XmlWatch.FolderFor(W.settings) != null ? "Cambiar\u2026" : "Elegir\u2026", ButtonKind.Secondary);
                change.Click += delegate
                {
                    using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog())
                    {
                        d.Description = "\u00BFQu\u00E9 carpeta vigilo? Cada XML nuevo que aparezca en ella se abrir\u00E1 en el visor.";
                        d.ShowNewFolderButton = false;
                        try { string cur = XmlWatch.FolderFor(W.settings); if (cur != null && Directory.Exists(cur)) d.SelectedPath = cur; } catch { }
                        if (d.ShowDialog(W.Win32) != WF.DialogResult.OK) return;
                        W.settings.XmlWatchFolder = d.SelectedPath;
                        W.Changed();
                        W.Rebuild();
                    }
                };
                p.Children.Add(change);
                return p;
            }
        }

        // Registers or removes the association. Windows keeps the final choice to the user, so after registering its
        // default-apps page opens on Stackshot.
        void SetAssoc(AssocKind k, bool on)
        {
            if (!on) { FileAssoc.Disable(k); return; }
            if (!FileAssoc.Enable(k)) return;
            if (FileAssoc.IsDefault(k)) return;
            string ext = k.Exts[0];
            DocAlert.Ask(this, "Stackshot ya aparece entre las aplicaciones que pueden abrir " + ext + ".",
                         "Elige Stackshot para " + ext + " en la ventana de Windows que se abre ahora: Windows pide que esa decisi\u00F3n la tomes t\u00FA.",
                         new string[] { "Abrir esa ventana" }, 0, 0);
            FileAssoc.OpenDefaultApps();
        }

        static string AssocNote(AssocKind k, string ext)
        {
            if (!FileAssoc.Registered(k))
            {
                if (k == FileAssoc.Video) return "Doble clic en un " + ext + " (los m\u00E1sters Cine, que el reproductor de Windows no abre) y se ve en el reproductor de Stackshot, a tama\u00F1o real.";
                if (k == FileAssoc.Pdf) return "Doble clic en un PDF y se abre en el visor de Stackshot: busca, resalta, anota y captura sus p\u00E1ginas.";
                if (k == FileAssoc.Text) return "Los .txt y .log, con n\u00FAmeros de l\u00EDnea, b\u00FAsqueda y los logs en vivo.";
                if (k == FileAssoc.Data) return "Los .json en \u00E1rbol y los .csv en tabla, con su texto al lado.";
                if (k == FileAssoc.Image) return "PNG, JPG y BMP se abren en el editor de Stackshot para marcarlos y compartirlos.";
                return "Doble clic en un " + ext + " y se abre en el visor de Stackshot.";
            }
            if (FileAssoc.IsDefault(k)) return "Stackshot es la aplicaci\u00F3n predeterminada para " + ext + ".";
            return "Registrado, pero Windows abre los " + ext + " con otra aplicaci\u00F3n. Elige Stackshot en Aplicaciones predeterminadas.";
        }

        static void OpenRecovered()
        {
            try { System.Diagnostics.Process.Start(Native.Explorer, "\"" + ShotStack.RecoveredDir + "\""); }
            catch (Exception ex)
            {
                ShotStack.Log("Recuperadas: " + ex.Message);
                if (ShotStack.Running != null) ShotStack.Running.Notify("No se pudo abrir la carpeta", ShotStack.RecoveredDir);
            }
        }

        void ClearRecent()
        {
            if (DocAlert.Ask(this, "\u00BFBorrar las capturas temporales?", "Las guardadas en tu carpeta no se tocan.", new string[] { "Borrar", "Cancelar" }, 0, 1) != 0) return;
            owner.ClearTemp();
            HistoryIndex.RequestScan();
            Rebuild();
            owner.Notify("Recientes borradas", "Se han borrado las capturas temporales. Las de tu carpeta siguen ah\u00ED.");
        }

        // Looking for updates, and installing them on their own (which needs the looking, and an installed copy that is
        // not managed by IT).
        Row[] UpdateRows()
        {
            bool msi = Installer.ManagedByMsi, installed = Installer.RunningInstalled;
            ToggleRow auto = new ToggleRow("Instalar actualizaciones autom\u00E1ticamente",
                                           "Se descargan, se comprueba su firma y se instalan solas cuando no est\u00E9s usando el equipo.",
                                           "save", Mac.Green, D.Color.FromArgb(0, 160, 90),
                                           delegate { return settings.AutoUpdate; }, delegate(bool v) { settings.AutoUpdate = v; Updater.SettingsChanged(); });
            auto.When = delegate { return !msi && installed && settings.CheckUpdates; };
            auto.WhenOff = msi ? "En este equipo las instala inform\u00E1tica: Stackshot solo te avisar\u00E1 de cada versi\u00F3n nueva."
                         : !installed ? "Solo con Stackshot instalado; esta copia no puede instalarse sola."
                         : "Necesita \u00ABBuscar actualizaciones\u00BB: sin buscarlas no puede instalarlas.";
            return new Row[]
            {
                new ToggleRow("Buscar actualizaciones", "Al abrir Stackshot y cada 3 horas pregunta a GitHub si hay una versi\u00F3n nueva. No env\u00EDa nada tuyo.", "update", Mac.Teal, Mac.Blue,
                              delegate { return settings.CheckUpdates; }, delegate(bool v) { settings.CheckUpdates = v; Updater.SettingsChanged(); }),
                auto
            };
        }

        // ---- Ajustes de la mascota (reached from the Ajustes button on its card)

        void BuildMascotSettings()
        {
            Header("Ajustes de la mascota", "Su forma de ser, qu\u00E9 te dice y si te acompa\u00F1a tambi\u00E9n por el escritorio.");
            Func<bool> shown = delegate { return settings.MascotOn; };
            Group("Car\u00E1cter",
                new ToggleRow("Mostrar la mascota", "En Inicio y abajo en la barra lateral.", "bot", Mac.Blue, Mac.Purple,
                              delegate { return settings.MascotOn; },
                              delegate(bool v)
                              {
                                  settings.MascotOn = v;
                                  if (v) { mascot.PopIn(); if (settings.MascotTalks) mascot.Greet("\u00A1He vuelto!"); }
                                  owner.MascotChanged();
                              }),
                new SegRow("Personalidad", "Lo que dice y c\u00F3mo se mueve.", "bot", Mac.Green, D.Color.FromArgb(0, 160, 90), MascotParts.Personalities,
                           delegate { return settings.MascotPersonality; },
                           delegate(int i)
                           {
                               MascotLook l = MascotLook.From(settings);
                               l.Personality = i;
                               LookChanged(l, null);
                               if (settings.MascotTalks) mascot.Say(MascotTalk.Hello(settings), 3000);
                           }) { When = shown },
                new ToggleRow("Saludos y consejos", "De vez en cuando te cuenta trucos de Stackshot.", "sparkle", Mac.Orange, Mac.Pink,
                              delegate { return settings.MascotTalks; }, delegate(bool v) { settings.MascotTalks = v; if (!v) mascot.Bubble = null; }) { When = shown },
                new ToggleRow("Disfraz de temporada", "Sin gorro, se pone la calabaza en Halloween y el de Pap\u00E1 Noel en Navidad.", "sparkle", Mac.Orange, Mac.Red,
                              delegate { return settings.MascotSeasonal; },
                              delegate(bool v) { MascotLook l = MascotLook.From(settings); l.Seasonal = v; LookChanged(l, null); }) { When = shown });
            // Each part on its own: one that fails is noted and the rest of the page (the desktop switch included) still shows.
            Section("comportamiento", BuildMascotBehavior);
            Section("bienestar", BuildWellness);
            Section("concentraci\u00F3n", BuildFocus);
            Section("salud", BuildHealth);
            Section("asistente", BuildAssist);
            Group("En el escritorio",
                new ToggleRow("Mascota en el escritorio", "Pasea junto a la barra de tareas, te mira, juega y se echa la siesta.", "screen", Mac.Indigo, Mac.Purple,
                              delegate { return settings.MascotDesktop; }, delegate(bool v) { settings.MascotDesktop = v; owner.MascotChanged(); })
                    {
                        // Completo's alone: below it the switch is off and says where to get it (the profile is never changed here).
                        When = delegate { return settings.MascotOn && settings.Profile == 2; },
                        WhenOff = settings.Profile != 2 ? "Solo con el perfil Completo (en General \u203A Perfil)." : "Activa antes \u00ABMostrar la mascota\u00BB."
                    },
                new ToggleRow("Sube a las ventanas", "De vez en cuando salta a la ventana que tienes delante y pasea por encima. Si la mueves, viaja con ella.", "window", Mac.Teal, Mac.Blue,
                              delegate { return settings.MascotClimb; }, delegate(bool v) { settings.MascotClimb = v; owner.MascotChanged(); })
                    { When = delegate { return settings.MascotOn && settings.MascotDesktop; } });
            Note("En el escritorio consume algo m\u00E1s: unos 10 MB de memoria y en torno al 1 % de CPU mientras se mueve (casi nada cuando duerme). " +
                 "Se esconde sola con juegos o presentaciones a pantalla completa y nunca sale en tus capturas ni al compartir pantalla. " +
                 "Un clic abre su men\u00FA y varios seguidos le hacen cosquillas; puedes arrastrarla a donde quieras.");
        }

        // Called after any look change: the live mascot, the desktop pet and every preview follow.
        void LookChanged(MascotLook l, string reaction)
        {
            l.ApplyTo(settings);
            mascot.Look = MascotLook.From(settings);
            owner.MascotChanged();
            if (reaction != null && settings.MascotTalks) mascot.Celebrate(reaction);
            else mascot.Celebrate(null);
            // Saved; the sidebar card is only rebuilt where it shows the mascot (here it shows the capture card, and the
            // next page builds it afresh), so a click never pays for rebuilding it.
            owner.ApplySettings();
            if (MiniMascot) BuildWidget();
            Wake();
        }

        // Shows a look on the big mascot without saving it (hovering a picker); null goes back to the saved one.
        void TryOn(MascotLook l)
        {
            mascot.Look = l ?? MascotLook.From(settings);
            RefreshStagePlate(l);
            if (l != null && settings.MascotOn) mascot.Hop(0.45);
            Wake();
        }
    }
}
