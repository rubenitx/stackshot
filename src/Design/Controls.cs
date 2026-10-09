// Stackshot - macOS-style controls for WPF windows: switch, segmented control, buttons, icon tiles and keycaps.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public static partial class Glyph
    {
        public static Geometry Shape(string name) { return Get(name); }
    }

    // Base for owner-drawn controls: hover and press tracked with eased values; repaints only while they move.
    // Subclasses that call AcceptFocus() take Tab focus, show a macOS focus ring when reached from the keyboard and react to
    // Space (and Enter for buttons). A control disabled on its own is drawn faded; one in a disabled parent leaves the
    // fading to the parent.
    public abstract class Drawn : FrameworkElement
    {
        public static readonly DependencyProperty HotProperty = DependencyProperty.Register("Hot", typeof(double), typeof(Drawn),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty DownProperty = DependencyProperty.Register("Down", typeof(double), typeof(Drawn),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Hot { get { return (double)GetValue(HotProperty); } }
        public double Down { get { return (double)GetValue(DownProperty); } }
        protected bool Interactive = true;
        bool ring;

        protected Drawn()
        {
            Focusable = false;
            FocusVisualStyle = null;
            SnapsToDevicePixels = true;
            IsEnabledChanged += delegate
            {
                if (!IsEnabled)
                {
                    BeginAnimation(HotProperty, null);
                    BeginAnimation(DownProperty, null);
                    if (IsMouseCaptured) ReleaseMouseCapture();
                }
                InvalidateVisual();
            };
        }

        // Opt in to keyboard focus.
        protected void AcceptFocus() { Focusable = true; }

        protected static void Ease(DependencyObject o, DependencyProperty p, double to, double ms)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
        }

        protected static void Spring(DependencyObject o, DependencyProperty p, double to, double ms)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
            ((IAnimatable)o).BeginAnimation(p, a, HandoffBehavior.SnapshotAndReplace);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Interactive) return;
            Ease(this, HotProperty, 1, 120);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            Ease(this, HotProperty, 0, 220);
            Ease(this, DownProperty, 0, 180);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!Interactive) return;
            CaptureMouse();
            pressedIn = true;
            Ease(this, DownProperty, 1, 70);
            Pressed(e.GetPosition(this));
            e.Handled = true;
        }

        // While pressed, sliding off lets go of the pressed look and sliding back takes it again, as on macOS; the
        // release only clicks over the control.
        bool pressedIn;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!IsMouseCaptured || !Interactive) return;
            bool inside = new Rect(RenderSize).Contains(e.GetPosition(this));
            if (inside == pressedIn) return;
            pressedIn = inside;
            Ease(this, DownProperty, inside ? 1 : 0, inside ? 70 : 160);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (!pressedIn) return;
            // Taken away mid-press (another window, a dialog): no stuck pressed look.
            pressedIn = false;
            Ease(this, DownProperty, 0, 200);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!Interactive) return;
            bool inside = IsMouseCaptured && new Rect(RenderSize).Contains(e.GetPosition(this));
            pressedIn = false;
            ReleaseMouseCapture();
            Ease(this, DownProperty, 0, 200);
            if (inside) Clicked(e.GetPosition(this));
            e.Handled = true;
        }

        protected virtual void Pressed(Point p) { }
        protected virtual void Clicked(Point p) { }

        // Keyboard activation; by default a click in the middle.
        protected virtual void Activate() { Clicked(new Point(ActualWidth / 2, ActualHeight / 2)); }
        protected virtual bool EnterActivates { get { return false; } }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || !Focusable || !Interactive || !IsEnabled || Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key != Key.Space && !(e.Key == Key.Enter && EnterActivates)) return;
            e.Handled = true;
            if (e.IsRepeat) return;
            // A short press, as if clicked.
            DoubleAnimationUsingKeyFrames k = new DoubleAnimationUsingKeyFrames();
            k.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            k.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            BeginAnimation(DownProperty, k, HandoffBehavior.SnapshotAndReplace);
            Activate();
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            InputDevice d = InputManager.Current.MostRecentInputDevice;
            ring = !(d is MouseDevice) && !(d is StylusDevice);
            InvalidateVisual();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            if (!ring) return;
            ring = false;
            InvalidateVisual();
        }

        // Faded when disabled on its own. Inside a disabled parent (a settings row that fades itself) fading again would
        // leave the control nearly invisible next to its label.
        bool Dim
        {
            get
            {
                if (IsEnabled) return false;
                UIElement p = VisualTreeHelper.GetParent(this) as UIElement;
                return p == null || p.IsEnabled;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            bool dim = Dim;
            if (dim) dc.PushOpacity(0.4);
            Paint(dc);
            if (dim) dc.Pop();
            if (ring && IsKeyboardFocused)
            {
                // macOS focus ring: translucent accent hugging the control's outline.
                Rect r = RingBounds;
                r.Inflate(1.5, 1.5);
                double rad = RingRadius + 1.5;
                Pen p = new Pen(Ds.Brush(Ds.WithAlpha(Ds.Brushes.Accent, Ds.Dark ? 0.6 : 0.5)), 3);
                p.Freeze();
                dc.DrawRoundedRectangle(null, p, r, rad, rad);
            }
        }

        // Shadows and the focus ring are drawn just outside the bounds. At 125% layout rounding can make the arranged size a
        // fraction smaller than Width/Height, and WPF then clips to it, cutting them off on some controls and not others.
        protected override Geometry GetLayoutClip(Size layoutSlotSize) { return ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null; }

        protected virtual void Paint(DrawingContext dc) { }

        // Hairlines are sized in device pixels; repaint when the window moves to a monitor with another scale.
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); InvalidateVisual(); }
        protected virtual Rect RingBounds { get { return new Rect(RenderSize); } }
        protected virtual double RingRadius { get { return Ds.RControl; } }

        // Accessibility: a name and a role for screen readers.
        internal virtual string AccessName { get { return null; } }
        internal virtual AutomationControlType AccessType { get { return AutomationControlType.Custom; } }

        protected override AutomationPeer OnCreateAutomationPeer() { return new DrawnPeer(this); }

        sealed class DrawnPeer : FrameworkElementAutomationPeer
        {
            public DrawnPeer(Drawn d) : base(d) { }
            protected override AutomationControlType GetAutomationControlTypeCore() { return ((Drawn)Owner).AccessType; }
            protected override string GetClassNameCore() { return Owner.GetType().Name; }
            protected override string GetNameCore()
            {
                string n = base.GetNameCore();
                return string.IsNullOrEmpty(n) ? ((Drawn)Owner).AccessName ?? "" : n;
            }
        }

        protected static FormattedText Text(string s, Typeface face, double size, Color c)
        {
            return Ink.Text(s, face, size, c);
        }

        protected static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
                                  (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
    }

    // Soft shadow of a rounded card, pre-blurred and then just drawn. WPF's DropShadowEffect is re-run on every frame
    // anything near it changes (an animated mascot next to a card cost a third of a core); this costs nothing.
    // Past the corners and the reach of the blur every row (and column) is the same, so a large shadow is drawn from a
    // small blurred template with its middle row and column stretched: a few image draws instead of a 30-50 ms blur of
    // the whole card, and small shared templates. Animate its Opacity for a stronger or weaker shadow.
    public class SoftShadow : FrameworkElement
    {
        readonly double radius, blur, depth, strength;

        sealed class Template
        {
            public BitmapSource Full;
            public bool SliceX, SliceY;
            public long Bytes;
            public readonly BitmapSource[] Parts = new BitmapSource[9];
        }

        // Templates shared between instances (menus, tooltips and cards that come back don't blur again), within a budget.
        static readonly Dictionary<string, Template> recent = new Dictionary<string, Template>();
        static readonly Queue<string> order = new Queue<string>();
        static long cached;
        const long Budget = 3L << 20;

        public SoftShadow(double radius, double blur, double depth, double strength)
        {
            this.radius = radius;
            this.blur = blur;
            this.depth = depth;
            this.strength = strength;
            IsHitTestVisible = false;
            // Parts meet edge to edge; without edge antialiasing they never leave a faint seam at fractional DPI.
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        protected override Geometry GetLayoutClip(Size layoutSlotSize) { return ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null; }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 1 || h < 1) return;
            int m = (int)Math.Ceiling(blur * 1.5) + 2;
            double r = Math.Max(0, Math.Min(radius, Math.Min(w, h) / 2));
            int core = 2 * ((int)Math.Ceiling(r) + (int)Math.Ceiling(blur) + 2) + 2;
            double tw = w >= core ? core : w, th = h >= core ? core : h;
            Template t = Get(tw, th, r, m, w >= core, h >= core);
            int bw = t.Full.PixelWidth, bh = t.Full.PixelHeight;
            double x0 = -m, y0 = -m + depth;
            if (!t.SliceX && !t.SliceY)
            {
                dc.DrawImage(t.Full, new Rect(x0, y0, bw, bh));
                return;
            }
            for (int j = 0; j < 3; j++)
            {
                int ys, yn;
                double yd, yh;
                if (!Span(t.SliceY, j, bh, h - th, out ys, out yn, out yd, out yh)) continue;
                for (int i = 0; i < 3; i++)
                {
                    int xs, xn;
                    double xd, xw;
                    if (!Span(t.SliceX, i, bw, w - tw, out xs, out xn, out xd, out xw)) continue;
                    BitmapSource part = t.Parts[j * 3 + i];
                    if (part == null) t.Parts[j * 3 + i] = part = Slice(t.Full, new Int32Rect(xs, ys, xn, yn));
                    dc.DrawImage(part, new Rect(x0 + xd, y0 + yd, xw, yh));
                }
            }
        }

        // Part i of 3 along one axis: before the middle pixel, the middle pixel stretched by extra, the rest. Unsliced,
        // part 0 is the whole length.
        static bool Span(bool sliced, int i, int size, double extra, out int s0, out int sn, out double d0, out double dn)
        {
            int c = size / 2;
            s0 = 0; sn = size; d0 = 0; dn = size;
            if (!sliced) return i == 0;
            if (i == 0) { sn = c; dn = c; }
            else if (i == 1) { s0 = c; sn = 1; d0 = c; dn = 1 + extra; }
            else { s0 = c + 1; sn = size - c - 1; d0 = c + 1 + extra; dn = sn; }
            return true;
        }

        Template Get(double tw, double th, double r, int m, bool sx, bool sy)
        {
            string key = tw + "|" + th + "|" + r + "|" + blur + "|" + strength;
            Template t;
            if (recent.TryGetValue(key, out t)) return t;
            int bw = (int)Math.Ceiling(tw) + 2 * m, bh = (int)Math.Ceiling(th) + 2 * m;
            t = new Template();
            t.Full = Ink.Shadow(bw, bh, new Rect(m, m, tw, th), r, blur, Ds.Argb(strength, 0, 0, 0));
            t.SliceX = sx;
            t.SliceY = sy;
            t.Bytes = 8L * bw * bh;   // the template and its parts
            recent[key] = t;
            order.Enqueue(key);
            cached += t.Bytes;
            while (order.Count > 1 && (cached > Budget || order.Count > 64))
            {
                Template old;
                if (recent.TryGetValue(order.Peek(), out old)) { cached -= old.Bytes; recent.Remove(order.Peek()); }
                order.Dequeue();
            }
            return t;
        }

        static BitmapSource Slice(BitmapSource s, Int32Rect r)
        {
            int stride = r.Width * 4;
            byte[] px = new byte[stride * r.Height];
            s.CopyPixels(r, px, stride, 0);
            BitmapSource b = BitmapSource.Create(r.Width, r.Height, 96, 96, PixelFormats.Pbgra32, null, px, stride);
            b.Freeze();
            return b;
        }
    }

    // A line icon as an element.
    public class GlyphView : FrameworkElement
    {
        string icon;
        Color color;
        double stroke;

        public GlyphView(string icon, double size, Color color) : this(icon, size, color, 1.6) { }

        public GlyphView(string icon, double size, Color color, double stroke)
        {
            this.icon = icon;
            this.color = color;
            this.stroke = stroke;
            Width = Height = size;
            IsHitTestVisible = false;
        }

        public string Icon { get { return icon; } set { if (icon == value) return; icon = value; InvalidateVisual(); } }
        public Color Color { get { return color; } set { if (color == value) return; color = value; InvalidateVisual(); } }

        protected override void OnRender(DrawingContext dc)
        {
            double s = Math.Min(ActualWidth, ActualHeight);
            Glyph.Draw(dc, icon, (ActualWidth - s) / 2, (ActualHeight - s) / 2, s, color, stroke);
        }
    }

    // Rounded square with a glyph, like the colored icons of macOS System Settings.
    public class IconTile : FrameworkElement
    {
        readonly string icon;
        readonly Color c1, c2;

        public IconTile(string icon, double size, Color c1, Color c2)
        {
            this.icon = icon;
            this.c1 = c1;
            this.c2 = c2;
            Width = Height = size;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double s = ActualWidth, r = s * 0.25;
            LinearGradientBrush b = new LinearGradientBrush(Mix(c1, Colors.White, 0.12), c2, 90);
            dc.DrawRoundedRectangle(b, null, new Rect(0, 0, s, s), r, r);
            Pen hl = new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 1);
            dc.DrawRoundedRectangle(null, hl, new Rect(0.5, 0.5, s - 1, s - 1), r - 0.5, r - 0.5);
            double g = s * 0.62;
            Glyph.Draw(dc, icon, (s - g) / 2, (s - g) / 2, g, Colors.White, Math.Max(1.4, s / 16));
        }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }
    }

    // Toggle switch: sliding knob, accent when on. Space toggles it when focused.
    public class MacSwitch : Drawn
    {
        public static readonly DependencyProperty OnProperty = DependencyProperty.Register("OnAmount", typeof(double), typeof(MacSwitch),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        bool on;
        public event Action<bool> Toggled;

        public MacSwitch(bool on)
        {
            this.on = on;
            SetValue(OnProperty, on ? 1.0 : 0.0);
            Width = 38;
            Height = 22;
            AcceptFocus();
        }

        public bool IsOn
        {
            get { return on; }
            set { if (on == value) return; on = value; Spring(this, OnProperty, on ? 1 : 0, 260); }
        }

        protected override void Clicked(Point p)
        {
            IsOn = !on;
            Action<bool> h = Toggled;
            if (h != null) h(on);
        }

        protected override double RingRadius { get { return ActualHeight / 2; } }
        internal override AutomationControlType AccessType { get { return AutomationControlType.CheckBox; } }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double t = (double)GetValue(OnProperty), w = ActualWidth, h = ActualHeight;
            Color off = pal.Dark ? Ds.Rgb(57, 57, 61) : Ds.Rgb(226, 226, 230);
            Color track = Mix(off, pal.Accent, t);
            if (Hot > 0) track = Mix(track, pal.Dark ? Colors.White : Colors.Black, 0.06 * Hot);
            dc.DrawRoundedRectangle(Ds.Brush(track), null, new Rect(0, 0, w, h), h / 2, h / 2);
            double d = h - 4, stretch = Down * 4, kx = 2 + (w - 4 - d) * Math.Max(0, Math.Min(1, t));
            if (t > 0.5) kx -= stretch;
            Rect knob = new Rect(kx, 2, d + stretch, d);
            Rect sh = knob;
            sh.Offset(0, 0.8);
            dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.22, 0, 0, 0)), null, sh, d / 2, d / 2);
            dc.DrawRoundedRectangle(Brushes.White, null, knob, d / 2, d / 2);
        }
    }

    // Segmented control with a sliding selection. Arrow keys move it when focused.
    public class MacSegmented : Drawn
    {
        public static readonly DependencyProperty SlideProperty = DependencyProperty.Register("Slide", typeof(double), typeof(MacSegmented),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        readonly string[] options;
        int selected, hover = -1;
        public event Action<int> Changed;

        public MacSegmented(string[] options, int selected)
        {
            this.options = options;
            this.selected = Math.Max(0, Math.Min(options.Length - 1, selected));
            SetValue(SlideProperty, (double)this.selected);
            double seg = 52;
            foreach (string o in options) seg = Math.Max(seg, Text(o, Ds.Semibold, 12.5, Colors.Black).WidthIncludingTrailingWhitespace + 24);
            Width = Math.Ceiling(seg * options.Length + 4);
            Height = 28;
            AcceptFocus();
        }

        public int Selected
        {
            get { return selected; }
            set { value = Math.Max(0, Math.Min(options.Length - 1, value)); if (value == selected) return; selected = value; Spring(this, SlideProperty, value, 300); }
        }

        double Seg { get { return Math.Max(1, (ActualWidth - 4) / options.Length); } }

        // Repaints only when the hovered segment changes, not on every mouse move.
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = At(e.GetPosition(this));
            if (i == hover) return;
            hover = i;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover < 0) return;
            hover = -1;
            InvalidateVisual();
        }

        int At(Point p) { return Math.Max(0, Math.Min(options.Length - 1, (int)((p.X - 2) / Seg))); }

        void Pick(int i)
        {
            if (i == selected) return;
            Selected = i;
            Action<int> h = Changed;
            if (h != null) h(i);
        }

        protected override void Clicked(Point p) { Pick(At(p)); }
        protected override void Activate() { }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || !Focusable || !IsEnabled || Keyboard.Modifiers != ModifierKeys.None) return;
            int to = e.Key == Key.Left || e.Key == Key.Up ? selected - 1 : e.Key == Key.Right || e.Key == Key.Down ? selected + 1
                   : e.Key == Key.Home ? 0 : e.Key == Key.End ? options.Length - 1 : int.MinValue;
            if (to == int.MinValue) return;
            e.Handled = true;
            Pick(Math.Max(0, Math.Min(options.Length - 1, to)));
        }

        protected override double RingRadius { get { return 7; } }
        internal override string AccessName { get { return options[selected]; } }
        internal override AutomationControlType AccessType { get { return AutomationControlType.Tab; } }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double w = ActualWidth, h = ActualHeight, x = (double)GetValue(SlideProperty), seg = Seg;
            dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0)), null, new Rect(0, 0, w, h), 7, 7);
            int hov = hover >= 0 && Hot > 0 ? hover : -1;
            for (int i = 0; i < options.Length; i++)
            {
                if (i == hov && i != selected)
                    dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.06, 255, 255, 255) : Ds.Argb(0.05, 0, 0, 0)), null, new Rect(2 + seg * i, 2, seg, h - 4), 5.5, 5.5);
                if (i > 0 && i != selected && i - 1 != selected && Math.Abs(x - i) > 0.6 && Math.Abs(x - (i - 1)) > 0.6)
                    dc.DrawRectangle(Ds.Brush(pal.Separator), null, new Rect(Ink.Align(this, 2 + seg * i), 8, Ink.Hair(this), h - 16));
            }
            Rect pill = new Rect(2 + seg * x, 2, seg, h - 4);
            if (!pal.Dark)
            {
                Rect sh = pill;
                sh.Offset(0, Ink.Hair(this));
                dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.14, 0, 0, 0)), null, sh, 5.5, 5.5);
            }
            dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Rgb(99, 99, 104) : Colors.White), null, pill, 5.5, 5.5);
            for (int i = 0; i < options.Length; i++)
            {
                bool on = i == selected;
                FormattedText t = Text(options[i], on ? Ds.Semibold : Ds.Regular, 12.5, on ? pal.Label : Mix(pal.Label2, pal.Label, i == hov ? 0.6 : 0));
                t.MaxTextWidth = Math.Max(1, seg - 8);
                t.MaxLineCount = 1;
                t.Trimming = TextTrimming.CharacterEllipsis;
                Ink.Center(dc, t, new Rect(2 + seg * i, 0, seg, h));
            }
        }
    }

    public enum ButtonKind { Primary, Secondary, Plain, Danger, Glass }

    // Push button. Primary is the blue one with the soft inner glow of cleanshot.com; Glass sits on dark imagery.
    // Space or Enter press it when focused.
    public class MacButton : Drawn
    {
        string label;
        readonly string icon, trailing;
        readonly ButtonKind kind;
        readonly double radius, fontSize;
        public event Action Click;

        public MacButton(string label, ButtonKind kind) : this(label, kind, null, null, 28) { }

        public MacButton(string label, ButtonKind kind, string icon, string trailing, double height)
        {
            this.label = label;
            this.kind = kind;
            this.icon = icon;
            this.trailing = trailing;
            Height = height;
            fontSize = height >= 38 ? 14 : 13;
            radius = height >= 38 ? 12 : 7;
            Width = Measure();
            AcceptFocus();
        }

        public string Label { get { return label; } set { if (label == value) return; label = value; Width = Measure(); InvalidateVisual(); } }

        double Measure()
        {
            double w = Text(label, Ds.Semibold, fontSize, Colors.Black).WidthIncludingTrailingWhitespace + (Height >= 38 ? 40 : 26);
            if (icon != null) w += fontSize + 7;
            if (trailing != null) w += Text(trailing, Ds.Regular, fontSize - 1, Colors.Black).WidthIncludingTrailingWhitespace + 18;
            return Math.Ceiling(w);
        }

        protected override void Clicked(Point p)
        {
            Action h = Click;
            if (h != null) h();
        }

        protected override bool EnterActivates { get { return true; } }
        protected override double RingRadius { get { return radius; } }
        internal override string AccessName { get { return label; } }
        internal override AutomationControlType AccessType { get { return AutomationControlType.Button; } }

        protected override void Paint(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double w = ActualWidth, h = ActualHeight, hot = Hot, down = Down, px = Ink.Hair(this);
            Rect r = new Rect(0, 0, w, h);
            Color fg;
            switch (kind)
            {
                case ButtonKind.Primary:
                {
                    Color top = Mix(Ds.Rgb(64, 150, 255), Ds.Rgb(92, 170, 255), hot), bottom = Mix(pal.Accent, Ds.Rgb(28, 120, 245), hot);
                    if (down > 0) { top = Mix(top, Colors.Black, 0.12 * down); bottom = Mix(bottom, Colors.Black, 0.12 * down); }
                    Rect sh = r;
                    sh.Offset(0, Ink.Snap(this, 1.5));
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.28 * (1 - down), 4, 24, 85)), null, sh, radius, radius);
                    dc.DrawRoundedRectangle(new LinearGradientBrush(top, bottom, 90), null, r, radius, radius);
                    Pen glow = new Pen(new LinearGradientBrush(Ds.Argb(0.45, 255, 255, 255), Ds.Argb(0.0, 255, 255, 255), 90), px);
                    Rect gr = Ink.Inset(r, px / 2);
                    dc.DrawRoundedRectangle(null, glow, gr, radius - px / 2, radius - px / 2);
                    fg = Colors.White;
                    break;
                }
                case ButtonKind.Danger:
                {
                    Color bg = Mix(Ds.WithAlpha(pal.Red, 0.14), Ds.WithAlpha(pal.Red, 0.22), hot);
                    if (down > 0) bg = Mix(bg, Ds.WithAlpha(pal.Red, 0.30), down);
                    dc.DrawRoundedRectangle(Ds.Brush(bg), null, r, radius, radius);
                    fg = pal.Red;
                    break;
                }
                case ButtonKind.Plain:
                    if (hot > 0 || down > 0) dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.ControlHover, Math.Min(1, hot + down * 0.6))), null, r, radius, radius);
                    fg = pal.Accent;
                    break;
                case ButtonKind.Glass:
                {
                    Color bg = Mix(Ds.Argb(0.2, 255, 255, 255), Ds.Argb(0.32, 255, 255, 255), hot);
                    if (down > 0) bg = Mix(bg, Ds.Argb(0.12, 255, 255, 255), down);
                    dc.DrawRoundedRectangle(Ds.Brush(bg), null, r, radius, radius);
                    fg = Colors.White;
                    break;
                }
                default:
                {
                    Color bg = pal.Dark ? Mix(Ds.Rgb(70, 70, 74), Ds.Rgb(84, 84, 89), hot) : Mix(Colors.White, Ds.Rgb(246, 246, 248), hot);
                    if (down > 0) bg = Mix(bg, pal.Dark ? Colors.Black : Ds.Rgb(220, 220, 224), 0.2 * down);
                    // Hairline and contact shadow on whole device pixels: crisp at 125% and 150% instead of a soft double edge.
                    if (!pal.Dark)
                    {
                        Rect sh = r;
                        sh.Offset(0, px);
                        dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.10, 0, 0, 0)), null, sh, radius, radius);
                    }
                    Pen edge = new Pen(Ds.Brush(pal.Dark ? Ds.Argb(0.10, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)), px);
                    dc.DrawRoundedRectangle(Ds.Brush(bg), edge, Ink.Inset(r, px / 2), radius - px / 2, radius - px / 2);
                    fg = pal.Label;
                    break;
                }
            }
            double pad = h >= 38 ? 20 : 13, x = r.X + pad;
            if (trailing == null && icon == null)
            {
                Ink.Center(dc, Text(label, Ds.Semibold, fontSize, fg), r);
                return;
            }
            if (icon != null)
            {
                Glyph.Draw(dc, icon, x, r.Y + (r.Height - fontSize - 2) / 2, fontSize + 2, fg, 1.7);
                x += fontSize + 9;
            }
            FormattedText t = Text(label, Ds.Semibold, fontSize, fg);
            dc.DrawText(t, new Point(Math.Round(x), Math.Round(r.Y + (r.Height - t.Height) / 2)));
            if (trailing != null)
            {
                FormattedText k = Text(trailing, Ds.Regular, fontSize - 1, Ds.WithAlpha(fg, 0.78));
                dc.DrawText(k, new Point(Math.Round(r.Right - pad - k.WidthIncludingTrailingWhitespace), Math.Round(r.Y + (r.Height - k.Height) / 2)));
            }
        }
    }

    // Shortcut shown as keycaps, right-aligned.
    public class Keycaps : FrameworkElement
    {
        readonly string[] keys;
        readonly double size;

        public Keycaps(string combo, double size)
        {
            keys = combo.Split(new string[] { " + " }, StringSplitOptions.None);
            this.size = size;
            Height = size + 6;
            IsHitTestVisible = false;
            double w = 0;
            foreach (string k in keys) w += Cap(k) + 5;
            Width = Math.Max(0, w - 5);
            AutomationProperties.SetName(this, combo);
        }

        protected override Geometry GetLayoutClip(Size layoutSlotSize) { return ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null; }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); InvalidateVisual(); }

        double Cap(string k) { return Math.Ceiling(Math.Max(size + 4, Ink.Text(k, Ds.Medium, size * 0.52, Colors.Black).WidthIncludingTrailingWhitespace + 14)); }

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            double x = 0, h = size + 4, px = Ink.Hair(this);
            Pen edge = new Pen(Ds.Brush(pal.Hairline), px);
            edge.Freeze();
            foreach (string k in keys)
            {
                double w = Cap(k);
                Rect r = new Rect(x, 0, w, h);
                Rect sh = r;
                sh.Offset(0, Ink.Snap(this, 1.5));
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.5, 0, 0, 0) : Ds.Argb(0.16, 0, 0, 0)), null, sh, 5, 5);
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Rgb(72, 72, 77) : Colors.White), edge, Ink.Inset(r, px / 2), 5 - px / 2, 5 - px / 2);
                Ink.Center(dc, Ink.Text(k, Ds.Medium, size * 0.52, pal.Label), r);
                x += w + 5;
            }
        }
    }
}
