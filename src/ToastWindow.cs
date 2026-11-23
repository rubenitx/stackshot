// Stackshot - A notice on screen: a small rounded card by the clock, in Stackshot's own style.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // Never takes the focus or the keyboard, never shows in the taskbar, Alt+Tab or captures (FloatWindow). A click on it
    // runs its action and closes it; × only closes it. Screen readers are told through UI Automation, without the focus
    // moving to it.
    sealed class ToastWindow : FloatWindow, IToastView
    {
        const double InMs = 180, OutMs = 170, MoveMs = 180;

        readonly ToastStack stack;
        readonly ToastStack.Entry entry;
        readonly Action onTheme;
        readonly Tween glide = new Tween(1);
        double gx0, gy0, gx1, gy1;
        ToastLook look;
        int hot = ToastLook.None, side = 1;
        int armed;                  // clicks count from this tick on (one meant for what was there before is ignored)
        bool leaving, hovering, pressed, moved;
        Point restAt;               // the pointer when it appeared: one resting in the corner doesn't hold it forever
        MI.BitmapSource shadow;
        Size shadowFor;
        ToastProvider access;

        public ToastWindow(ToastStack stack, ToastStack.Entry entry)
        {
            this.stack = stack;
            this.entry = entry;
            AccessibleName = entry.Notice.Title;
            AccessibleDescription = entry.Notice.Text;
            Cursor = Cursors.Hand;
            // Topmost through WS_EX_TOPMOST and ShowQuiet: the property would make WinForms activate the window as its handle
            // is created, taking the focus from whatever Stackshot window has it.
            TopMost = false;
            onTheme = delegate { stack.Post(Restyle); };
            Ds.Changed += onTheme;
        }

        protected override bool PerPixel { get { return true; } }

        public ToastLook Look { get { return look; } }

        public Size Measure(float scale)
        {
            if (look == null || look.Scale != scale || look.Notice != entry.Notice || look.Dark != Ds.Dark)
            {
                s = scale;
                Pad = P(30);
                look = ToastLook.For(entry.Notice, scale);
            }
            return look.Size;
        }

        public void Place(Rectangle r, float scale, int enterFrom, bool animate)
        {
            if (leaving || IsDisposed) return;
            Measure(scale);
            side = enterFrom;
            bool resized = body != look.Size;
            SetSize(look.Size);
            if (!Visible)
            {
                armed = Environment.TickCount + 300;
                restAt = Control.MousePosition;
                bool had = IsHandleCreated; // a new window paints itself as its handle is made
                if (animate)
                {
                    alpha.Set(0);
                    ApplyAlpha();
                    JumpTo(r.X + enterFrom * P(36), r.Y);
                    ShowQuiet();
                    alpha.Go(1, InMs, 0, Ease.OutCubic, null);
                    GlideTo(r.X, r.Y, InMs);
                }
                else
                {
                    alpha.Set(1);
                    ApplyAlpha();
                    JumpTo(r.X, r.Y);
                    ShowQuiet();
                }
                if (had) Redraw();
                return;
            }
            if (resized) { shadow = null; Redraw(); }
            if (animate) GlideTo(r.X, r.Y, MoveMs);
            else { glide.Set(1); JumpTo(r.X, r.Y); }
        }

        public void Renew()
        {
            if (leaving || IsDisposed) return;
            AccessibleName = entry.Notice.Title;
            AccessibleDescription = entry.Notice.Text;
            if (access != null) access.Set(entry.Notice);
            armed = Environment.TickCount + 300;
            Measure(s);
            if (look.Size == body) Redraw(); // another size: the stack places it again right after, and that repaints it
        }

        public void Depart(bool animate)
        {
            if (leaving || IsDisposed) return;
            leaving = true;
            hot = ToastLook.None;
            if (!animate || !Visible || !IsHandleCreated) { Close(); return; }
            alpha.Go(0, OutMs, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            GlideTo(x + side * P(36), y, OutMs);
        }

        public void Announce()
        {
            if (access == null || IsDisposed) return;
            ToastUia.Announce(access, entry.Notice);
        }

        void Restyle()
        {
            if (IsDisposed || leaving) return;
            shadow = null;
            look = null;
            Measure(s);
            Redraw();
        }

        void GlideTo(double nx, double ny, double ms)
        {
            gx0 = x;
            gy0 = y;
            gx1 = nx;
            gy1 = ny;
            glide.Set(0);
            glide.Go(1, ms, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override bool StepExtra(double now)
        {
            if (!glide.Running) return false;
            glide.Step(now);
            x = gx0 + (gx1 - gx0) * glide.Value;
            y = gy0 + (gy1 - gy0) * glide.Value;
            ApplyPos();
            return glide.Running;
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            if (look == null || body.Width <= 0) return;
            if (shadow == null || shadowFor != new Size(w, h))
            {
                shadow = ToastLook.Shadow(w, h, Pad, look);
                shadowFor = new Size(w, h);
            }
            dc.DrawImage(shadow, new W.Rect(0, 0, w, h));
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            look.Paint(dc, hot);
            dc.Pop();
        }

        int Hit(Point client) { return look == null ? ToastLook.None : look.HitTest(new Point(client.X - Pad, client.Y - Pad)); }

        void SetHot(int h)
        {
            bool inside = h != ToastLook.None && !leaving;
            if (inside != hovering)
            {
                hovering = inside;
                stack.Hover(entry, inside);
            }
            if (h == hot) return;
            hot = h;
            Cursor = h == ToastLook.None ? Cursors.Default : Cursors.Hand;
            Redraw();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (leaving) return;
            // Windows also reports a move when the toast appears under a still pointer: only a real one counts.
            if (!moved && PointToScreen(e.Location) == restAt) return;
            moved = true;
            SetHot(Hit(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!leaving && Hit(PointToClient(Control.MousePosition)) == ToastLook.None) SetHot(ToastLook.None);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = !leaving && Hit(e.Location) != ToastLook.None;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool was = pressed;
            pressed = false;
            if (!was || leaving || unchecked(Environment.TickCount - armed) < 0) return;
            int h = Hit(e.Location);
            if (h == ToastLook.None) return;
            if (e.Button == MouseButtons.Left) stack.Click(entry, h == ToastLook.Close);
            else if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) stack.Click(entry, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            access = new ToastProvider(Handle, entry.Notice);
            base.OnHandleCreated(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x003D && access != null && unchecked((int)m.LParam.ToInt64()) == ToastUia.RootObjectId) // WM_GETOBJECT
            {
                m.Result = ToastUia.Return(Handle, m.WParam, m.LParam, access);
                return;
            }
            if (m.Msg == 0x0002 && access != null) ToastUia.Disconnect(Handle); // WM_DESTROY
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ds.Changed -= onTheme;
            base.Dispose(disposing);
        }
    }

    // Size and drawing of a toast apart from its window (the tests render it into images): the app icon or a glyph for
    // its kind, the title in bold on one line, up to three lines of text, its button and ×.
    public sealed class ToastLook
    {
        public const float BodyW = 360, Inset = 14, IconSize = 32, Gap = 12, ButtonH = 28, CloseSize = 22, Radius = 14;
        public const int None = -1, Body = 0, Button = 1, Close = 2;
        public const int MaxLines = 3;

        public readonly Notice Notice;
        public readonly float Scale;
        public readonly bool Dark;
        public Size Size;
        public Rectangle Icon, CloseBox, ButtonBox;
        public double TextLeft, TitleTop, TextTop, LineHeight;
        public int Lines;                       // lines of text shown
        public bool Clipped;                    // the text did not fit in them
        public bool TitleClipped;               // the title is wider than its line and ends in an ellipsis
        M.FormattedText title, text, label;

        ToastLook(Notice n, float scale)
        {
            Notice = n;
            Scale = scale;
            Dark = Ds.Dark;
        }

        double F(double v) { return v * Scale; }
        int P(double v) { return (int)Math.Round(v * Scale); }

        public static ToastLook For(Notice n, float scale)
        {
            ToastLook k = new ToastLook(n, scale);
            k.Arrange();
            return k;
        }

        void Arrange()
        {
            Palette pal = Ds.Brushes;
            int w = P(BodyW);
            Icon = new Rectangle(P(Inset), P(Inset), P(IconSize), P(IconSize));
            CloseBox = new Rectangle(w - P(8) - P(CloseSize), P(8), P(CloseSize), P(CloseSize));
            TextLeft = Math.Round(F(Inset + IconSize + Gap));
            double textW = w - TextLeft - F(Inset), titleW = CloseBox.X - F(4) - TextLeft;
            title = Ink.Px(Notice.Title, Ds.Semibold, F(13.5), pal.Label);
            title.MaxTextWidth = Math.Max(1, titleW);
            title.MaxLineCount = 1;
            title.Trimming = W.TextTrimming.CharacterEllipsis;
            M.FormattedText whole = Ink.Px(Notice.Title, Ds.Semibold, F(13.5), pal.Label);
            TitleClipped = whole.Width > titleW + 0.5;
            // A title alone sits level with the icon.
            TitleTop = Notice.Text.Length > 0 ? Math.Round(F(Inset) - F(1)) : Math.Round(Icon.Y + (Icon.Height - title.Height) / 2);
            TextTop = Math.Round(TitleTop + title.Height + F(2));
            LineHeight = Math.Round(F(17));
            double bottom = TitleTop + title.Height;
            text = null;
            Lines = 0;
            Clipped = false;
            if (Notice.Text.Length > 0)
            {
                text = Ink.Px(Notice.Text, Ds.Regular, F(12.5), pal.Label);
                text.MaxTextWidth = Math.Max(1, textW);
                text.LineHeight = LineHeight;
                M.FormattedText full = Ink.Px(Notice.Text, Ds.Regular, F(12.5), pal.Label);
                full.MaxTextWidth = Math.Max(1, textW);
                full.LineHeight = LineHeight;
                full.Trimming = W.TextTrimming.None;
                int all = Math.Max(1, (int)Math.Round(full.Height / LineHeight));
                text.MaxLineCount = MaxLines;
                text.Trimming = W.TextTrimming.WordEllipsis;
                // What is drawn; a word too long for its line is cut there with an ellipsis (the full text runs wider).
                Lines = Math.Min(MaxLines, Math.Max(1, (int)Math.Round(text.Height / LineHeight)));
                Clipped = all > Lines || full.Width > textW + 0.5;
                bottom = TextTop + Lines * LineHeight;
            }
            bottom = Math.Max(F(Inset + IconSize), bottom);
            int h;
            label = null;
            ButtonBox = Rectangle.Empty;
            if (!string.IsNullOrEmpty(Notice.ActionLabel) && Notice.Action != null)
            {
                label = Ink.Px(Notice.ActionLabel, Ds.Semibold, F(12.5), M.Colors.White);
                label.MaxTextWidth = Math.Max(1, textW - F(26));
                label.MaxLineCount = 1;
                label.Trimming = W.TextTrimming.CharacterEllipsis;
                int bw = (int)Math.Min(textW, Math.Max(P(76), Math.Ceiling(label.WidthIncludingTrailingWhitespace + F(26))));
                int by = (int)Math.Round(bottom + F(10));
                ButtonBox = new Rectangle(w - P(Inset) - bw, by, bw, P(ButtonH));
                h = ButtonBox.Bottom + P(Inset);
            }
            else h = (int)Math.Ceiling(bottom + F(Inset));
            Size = new Size(w, h);
        }

        // Body coordinates.
        public int HitTest(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Size.Width || p.Y >= Size.Height) return None;
            if (CloseBox.Contains(p)) return Close;
            if (!ButtonBox.IsEmpty && ButtonBox.Contains(p)) return Button;
            return Body;
        }

        public static MI.BitmapSource Shadow(int w, int h, int pad, ToastLook look)
        {
            W.Rect sb = new W.Rect(pad, pad + look.F(6), look.Size.Width, look.Size.Height);
            return Ink.Shadow(w, h, sb, look.F(Radius), look.F(18), Ds.Argb(look.Dark ? 0.55 : 0.22, 0, 0, 0));
        }

        // The body at (0, 0); hot: the part under the mouse (None when it is elsewhere).
        public void Paint(M.DrawingContext dc, int hot)
        {
            Palette pal = Ds.Brushes;
            W.Rect b = new W.Rect(0, 0, Size.Width, Size.Height);
            double r = F(Radius);
            bool over = hot != None;
            M.Color bg = Dark ? (over ? Ds.Rgb(49, 49, 53) : Ds.Rgb(42, 42, 45)) : (over ? Ds.Rgb(245, 245, 247) : Ds.Rgb(251, 251, 252));
            Ink.Round(dc, bg, b, r);
            Ink.Hairline(dc, Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.10, 0, 0, 0), b, r);
            PaintIcon(dc, pal);
            dc.DrawText(title, new W.Point(TextLeft, TitleTop));
            if (text != null) dc.DrawText(text, new W.Point(TextLeft, TextTop));
            if (label != null) PaintButton(dc, hot == Button);
            PaintClose(dc, pal, hot == Close);
        }

        void PaintIcon(M.DrawingContext dc, Palette pal)
        {
            W.Rect r = new W.Rect(Icon.X, Icon.Y, Icon.Width, Icon.Height);
            if (Notice.Kind == NoticeKind.Info)
            {
                MI.BitmapSource logo = Logo(Icon.Width);
                if (logo != null) { dc.DrawImage(logo, r); return; }
            }
            M.Color c = Notice.Kind == NoticeKind.Error ? pal.Red : Notice.Kind == NoticeKind.Warning ? pal.Orange
                      : Notice.Kind == NoticeKind.Success ? pal.Green : pal.Accent;
            double d = r.Width, cx = r.X + d / 2, cy = r.Y + d / 2;
            dc.DrawEllipse(Ds.Brush(c), null, new W.Point(cx, cy), d / 2, d / 2);
            double gs = d * 0.56;
            if (Notice.Kind == NoticeKind.Warning || Notice.Kind == NoticeKind.Info)
            {
                // An exclamation mark (an "i" for information without the logo), in white.
                bool info = Notice.Kind == NoticeKind.Info;
                M.Pen p = new M.Pen(Ds.Brush(Palette.HudLabel), F(2.6));
                p.StartLineCap = p.EndLineCap = M.PenLineCap.Round;
                p.Freeze();
                double top = cy - d * 0.25, bottom = cy + d * 0.08, dot = cy + d * 0.24;
                if (info) { top = cy - d * 0.06; bottom = cy + d * 0.25; dot = cy - d * 0.23; }
                dc.DrawLine(p, new W.Point(cx, top), new W.Point(cx, bottom));
                dc.DrawEllipse(Ds.Brush(Palette.HudLabel), null, new W.Point(cx, dot), F(1.7), F(1.7));
                return;
            }
            Glyph.Draw(dc, Notice.Kind == NoticeKind.Error ? "close" : "check", cx - gs / 2, cy - gs / 2, gs, Palette.HudLabel, F(2.6));
        }

        // Like UpdateNotice's main button: blue with a soft inner glow.
        void PaintButton(M.DrawingContext dc, bool over)
        {
            W.Rect rr = new W.Rect(ButtonBox.X, ButtonBox.Y, ButtonBox.Width, ButtonBox.Height);
            double rad = F(7);
            M.Color top = over ? Ds.Rgb(92, 170, 255) : Ds.Rgb(64, 150, 255), bottom = over ? Ds.Rgb(28, 120, 245) : Ds.Brushes.Accent;
            W.Rect sh = rr;
            sh.Offset(0, F(1.2));
            dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.24, 4, 24, 85)), null, sh, rad, rad);
            M.LinearGradientBrush fill = new M.LinearGradientBrush(top, bottom, 90);
            fill.Freeze();
            dc.DrawRoundedRectangle(fill, null, rr, rad, rad);
            M.LinearGradientBrush shine = new M.LinearGradientBrush(Ds.Argb(0.42, 255, 255, 255), Ds.Argb(0, 255, 255, 255), 90);
            shine.Freeze();
            M.Pen glow = new M.Pen(shine, 1);
            glow.Freeze();
            dc.DrawRoundedRectangle(null, glow, new W.Rect(rr.X + 0.5, rr.Y + 0.5, rr.Width - 1, rr.Height - 1), rad - 0.5, rad - 0.5);
            Ink.Center(dc, label, rr);
        }

        void PaintClose(M.DrawingContext dc, Palette pal, bool over)
        {
            W.Rect r = new W.Rect(CloseBox.X, CloseBox.Y, CloseBox.Width, CloseBox.Height);
            W.Point c = new W.Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            if (over) dc.DrawEllipse(Ds.Brush(pal.ControlHover), null, c, r.Width / 2, r.Height / 2);
            double gs = F(13);
            Glyph.Draw(dc, "close", c.X - gs / 2, c.Y - gs / 2, gs, over ? pal.Label : pal.Label2, F(1.6));
        }

        static MI.BitmapSource logo;
        static int logoPx;

        // The app logo, scaled once with GDI+ bicubic so it stays crisp small; the .exe's icon when the logo is missing.
        static MI.BitmapSource Logo(int px)
        {
            if (logo != null && logoPx == px) return logo;
            try
            {
                using (Image src = ShotStack.LoadResourceImage("logo.png") ?? IconImage(px))
                {
                    if (src == null) return null;
                    using (Bitmap b = new Bitmap(px, px, PixelFormat.Format32bppPArgb))
                    {
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.DrawImage(src, new Rectangle(0, 0, px, px));
                        }
                        logo = Ink.FromGdi(b);
                        logoPx = px;
                    }
                }
            }
            catch (Exception ex) { ShotStack.Log("Icono del aviso: " + ex.Message); }
            return logo;
        }

        static Image IconImage(int px)
        {
            using (System.IO.Stream s = ShotStack.Resource("stackshot.ico"))
            {
                if (s == null) return null;
                using (Icon i = new Icon(s, new Size(Math.Max(16, px), Math.Max(16, px)))) return i.ToBitmap();
            }
        }
    }

    // UI Automation for a toast: its name and text for screen readers, and the announcement that has Narrator read it
    // without the focus moving there (a notification event; a live-region change before Windows 10 1709).
    static class ToastUia
    {
        [ComImport, Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IRawElementProviderSimple
        {
            int ProviderOptions { get; }
            [return: MarshalAs(UnmanagedType.IUnknown)] object GetPatternProvider(int patternId);
            object GetPropertyValue(int propertyId);
            IRawElementProviderSimple HostRawElementProvider { get; }
        }

        public const int RootObjectId = -25;
        public const int ControlTypeProperty = 30003, NameProperty = 30005, IsKeyboardFocusableProperty = 30009, AutomationIdProperty = 30011,
                         HelpTextProperty = 30013, LiveSettingProperty = 30135, FullDescriptionProperty = 30159;
        public const int PaneControlType = 50033, LiveRegionChangedEvent = 20024;

        [DllImport("UIAutomationCore.dll")] static extern IntPtr UiaReturnRawElementProvider(IntPtr hwnd, IntPtr wParam, IntPtr lParam, IRawElementProviderSimple el);
        [DllImport("UIAutomationCore.dll")] public static extern int UiaHostProviderFromHwnd(IntPtr hwnd, out IRawElementProviderSimple provider);
        [DllImport("UIAutomationCore.dll")] static extern int UiaRaiseNotificationEvent(IRawElementProviderSimple provider, int kind, int processing,
            [MarshalAs(UnmanagedType.BStr)] string text, [MarshalAs(UnmanagedType.BStr)] string activityId);
        [DllImport("UIAutomationCore.dll")] static extern int UiaRaiseAutomationEvent(IRawElementProviderSimple provider, int id);
        [DllImport("UIAutomationCore.dll")] static extern bool UiaClientsAreListening();

        static bool noNotifications;

        // The last announcement (what was said, and the result of raising it), for the tests.
        public static string LastSpoken;
        public static int LastResult;

        public static IntPtr Return(IntPtr hwnd, IntPtr wParam, IntPtr lParam, IRawElementProviderSimple p)
        {
            try { return UiaReturnRawElementProvider(hwnd, wParam, lParam, p); }
            catch (Exception ex) { ShotStack.Log("UIA aviso: " + ex.Message); return IntPtr.Zero; }
        }

        // The window is going: UI Automation lets go of its provider.
        public static void Disconnect(IntPtr hwnd)
        {
            try { UiaReturnRawElementProvider(hwnd, IntPtr.Zero, IntPtr.Zero, null); } catch { }
        }

        public static void Announce(IRawElementProviderSimple p, Notice n)
        {
            string said = n.Spoken;
            LastSpoken = said;
            LastResult = 1; // S_FALSE: nobody listening
            try
            {
                if (!UiaClientsAreListening()) return;
                bool urgent = n.Kind == NoticeKind.Error || n.Kind == NoticeKind.Warning;
                if (!noNotifications)
                {
                    try
                    {
                        // NotificationKind_Other; NotificationProcessing_ImportantAll or _All.
                        LastResult = UiaRaiseNotificationEvent(p, 4, urgent ? 0 : 2, said, "StackshotNotice");
                        return;
                    }
                    catch (EntryPointNotFoundException) { noNotifications = true; }
                }
                LastResult = UiaRaiseAutomationEvent(p, LiveRegionChangedEvent);
            }
            catch (Exception ex) { ShotStack.Log("UIA aviso: " + ex.Message); }
        }
    }

    // The toast as UI Automation sees it: a pane named by its title, its text as the description, a polite live region
    // (assertive for warnings and errors); the window itself provides the rest.
    sealed class ToastProvider : ToastUia.IRawElementProviderSimple
    {
        readonly IntPtr hwnd;
        string name, text;
        bool urgent;

        public ToastProvider(IntPtr hwnd, Notice n)
        {
            this.hwnd = hwnd;
            Set(n);
        }

        public void Set(Notice n)
        {
            name = n.Title;
            text = n.Text.Replace("\n", " ");
            urgent = n.Kind == NoticeKind.Error || n.Kind == NoticeKind.Warning;
        }

        public int ProviderOptions { get { return 0x2 | 0x20; } } // ServerSideProvider | UseComThreading

        public object GetPatternProvider(int patternId) { return null; }

        public object GetPropertyValue(int id)
        {
            switch (id)
            {
                case ToastUia.NameProperty: return name;
                case ToastUia.HelpTextProperty:
                case ToastUia.FullDescriptionProperty: return text;
                case ToastUia.ControlTypeProperty: return ToastUia.PaneControlType;
                case ToastUia.LiveSettingProperty: return urgent ? 2 : 1; // Assertive : Polite
                case ToastUia.AutomationIdProperty: return "StackshotNotice";
                case ToastUia.IsKeyboardFocusableProperty: return false;
            }
            return null;
        }

        public ToastUia.IRawElementProviderSimple HostRawElementProvider
        {
            get
            {
                ToastUia.IRawElementProviderSimple host;
                try { if (ToastUia.UiaHostProviderFromHwnd(hwnd, out host) == 0) return host; }
                catch { }
                return null;
            }
        }
    }
}
