// Stackshot - WPF window base (Mica, custom caption, theme) and a GDI+ layer for hand-drawn art inside WPF.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using D = System.Drawing;

namespace Stackshot
{
    // A Windows 11 window with the macOS feel: Mica behind a translucent sidebar, no system title bar, our own
    // minimize/close, dark or light with the app theme. Lives inside the WinForms message loop.
    public class Sheet : Window
    {
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int l, r, t, b; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

        public static bool HasMica = Environment.OSVersion.Version.Build >= 22621;
        protected readonly Grid Root = new Grid();
        readonly StackPanel captions = new StackPanel();
        IntPtr hwnd;
        bool closing;

        public Sheet()
        {
            AllowsTransparency = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanMinimize;
            UseLayoutRounding = true;
            FontFamily = Ds.Text;
            FontSize = 13;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0; // dragging is done by the subclass, so anything near the top stays clickable
            chrome.GlassFrameThickness = new Thickness(-1);
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);
            Content = Root;
            captions.Orientation = Orientation.Horizontal;
            captions.HorizontalAlignment = HorizontalAlignment.Right;
            captions.VerticalAlignment = VerticalAlignment.Top;
            Panel.SetZIndex(captions, 100);
            AddCaptions();
            Root.Children.Add(captions);
            Ds.Changed += OnDs;
            ApplyBackground();
            Resources[typeof(ToolTip)] = TipStyle();
            Activated += delegate { DimCaptions(false); Ink.PerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip; };
            DpiChanged += delegate { Ink.PerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip; };
            Deactivated += delegate { DimCaptions(true); };
            try { System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this); } catch { }
        }

        public IntPtr Handle { get { return hwnd; } }

        static ImageSource appIcon;

        // The app logo for the title bar and taskbar, decoded once.
        public static ImageSource AppIcon
        {
            get
            {
                if (appIcon != null) return appIcon;
                try
                {
                    using (D.Image img = ShotStack.LoadResourceImage("logo.png"))
                    using (D.Bitmap b = img == null ? null : new D.Bitmap(img))
                        if (b != null) appIcon = Ink.FromGdi(b);
                }
                catch { }
                return appIcon;
            }
        }

        // Children added to Root sit under the caption buttons.
        protected void KeepCaptionsOnTop()
        {
            Root.Children.Remove(captions);
            Root.Children.Add(captions);
            Grid.SetColumnSpan(captions, Math.Max(1, Root.ColumnDefinitions.Count));
        }

        void AddCaptions()
        {
            captions.Children.Clear();
            captions.Children.Add(new CaptionButton("minus", false, "Minimizar", delegate { WindowState = WindowState.Minimized; }));
            captions.Children.Add(new CaptionButton("close", true, "Cerrar", delegate { Close(); }));
            if (hwnd != IntPtr.Zero && !IsActive) DimCaptions(true);
        }

        void DimCaptions(bool dim)
        {
            foreach (UIElement e in captions.Children)
            {
                CaptionButton b = e as CaptionButton;
                if (b != null) b.Inactive = dim;
            }
        }

        // Minimize and close in the Windows places, drawn in the app palette. Acts on release over the button, like the
        // system ones, so a press that slides off does nothing.
        sealed class CaptionButton : Border
        {
            readonly GlyphView glyph;
            readonly bool close;
            readonly Action act;
            bool hot, down, inactive;

            public CaptionButton(string icon, bool close, string tip, Action act)
            {
                this.close = close;
                this.act = act;
                Width = 46;
                Height = 32;
                glyph = new GlyphView(icon, close ? 15 : 14, Ds.Brushes.Label2, 1.2);
                Child = glyph;
                ToolTip = tip;
                ToolTipService.SetInitialShowDelay(this, 900);
                AutomationProperties.SetName(this, tip);
                WindowChrome.SetIsHitTestVisibleInChrome(this, true);
                Paint();
            }

            public bool Inactive { set { if (inactive == value) return; inactive = value; Paint(); } }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                if (close && hot)
                {
                    Background = Ds.Brush(down && hot ? Ds.Rgb(150, 34, 22) : Ds.Rgb(196, 43, 28));
                    glyph.Color = Colors.White;
                    return;
                }
                Background = hot ? Ds.Brush(down ? pal.Control : pal.ControlHover) : Brushes.Transparent;
                glyph.Color = hot ? pal.Label : inactive ? pal.Label3 : pal.Label2;
            }

            protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); hot = true; Paint(); }
            protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); hot = false; Paint(); }

            // While pressed the button holds the mouse, so it tracks the pointer itself: off the button it looks idle.
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!down) return;
                bool inside = new Rect(RenderSize).Contains(e.GetPosition(this));
                if (inside == hot) return;
                hot = inside;
                Paint();
            }

            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                base.OnMouseLeftButtonDown(e);
                e.Handled = true;
                down = true;
                CaptureMouse();
                Paint();
            }

            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                base.OnMouseLeftButtonUp(e);
                e.Handled = true;
                bool fire = down && new Rect(RenderSize).Contains(e.GetPosition(this));
                down = false;
                ReleaseMouseCapture();
                // The window may minimize or close under the pointer without a mouse-leave; start clean next time.
                hot = !fire && IsMouseOver;
                Paint();
                if (fire) act();
            }

            protected override void OnLostMouseCapture(MouseEventArgs e)
            {
                base.OnLostMouseCapture(e);
                if (!down) return;
                down = false;
                Paint();
            }
        }

        // Tooltips in the app's look: a small rounded label with a soft shadow, light or dark with the theme.
        static Style TipStyle()
        {
            Palette pal = Ds.Brushes;
            FrameworkElementFactory root = new FrameworkElementFactory(typeof(Grid));
            root.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 3, 8, 10));
            FrameworkElementFactory shadow = new FrameworkElementFactory(typeof(TipShadow));
            root.AppendChild(shadow);
            FrameworkElementFactory card = new FrameworkElementFactory(typeof(Border));
            card.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            card.SetValue(Border.BackgroundProperty, Ds.Brush(pal.Dark ? Ds.Rgb(50, 50, 53) : Ds.Rgb(250, 250, 251)));
            card.SetValue(Border.BorderBrushProperty, Ds.Brush(pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)));
            card.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            card.SetValue(Border.PaddingProperty, new Thickness(9, 4, 9, 5));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            card.AppendChild(content);
            root.AppendChild(card);
            ControlTemplate t = new ControlTemplate(typeof(ToolTip));
            t.VisualTree = root;
            t.Seal();
            Style s = new Style(typeof(ToolTip));
            s.Setters.Add(new Setter(Control.TemplateProperty, t));
            s.Setters.Add(new Setter(Control.ForegroundProperty, Ds.Brush(pal.Label)));
            s.Setters.Add(new Setter(Control.FontFamilyProperty, Ds.Text));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            s.Setters.Add(new Setter(System.Windows.Controls.ToolTip.HasDropShadowProperty, false));
            s.Setters.Add(new Setter(System.Windows.Controls.ToolTip.HorizontalOffsetProperty, -8.0));
            s.Setters.Add(new Setter(System.Windows.Controls.ToolTip.VerticalOffsetProperty, -2.0));
            s.Setters.Add(new Setter(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal));
            s.Setters.Add(new Setter(UIElement.SnapsToDevicePixelsProperty, true));
            s.Setters.Add(new Setter(FrameworkElement.UseLayoutRoundingProperty, true));
            // Long text wraps into a short block, as on macOS, instead of one wide line.
            FrameworkElementFactory text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
            text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            text.SetValue(FrameworkElement.MaxWidthProperty, 300.0);
            DataTemplate plain = new DataTemplate(typeof(string));
            plain.VisualTree = text;
            plain.Seal();
            s.Resources.Add(new DataTemplateKey(typeof(string)), plain);
            s.Seal();
            return s;
        }

        sealed class TipShadow : SoftShadow
        {
            public TipShadow() : base(7, 8, 2, Ds.Dark ? 0.45 : 0.16) { }
        }

        void OnDs()
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                // Queued before a close: a closed window has nothing to repaint (and its HWND may be reused).
                if (closing || IsDisposed) return;
                ApplyBackground();
                ApplyDwm();
                Resources[typeof(ToolTip)] = TipStyle();
                AddCaptions();
                ThemeChanged();
            });
        }

        protected virtual void ThemeChanged() { }

        void ApplyBackground()
        {
            Background = HasMica ? Brushes.Transparent : Ds.Brush(Ds.Brushes.Sidebar);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Ink.PerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            hwnd = new WindowInteropHelper(this).Handle;
            HwndSource src = HwndSource.FromHwnd(hwnd);
            if (HasMica) src.CompositionTarget.BackgroundColor = Colors.Transparent;
            ApplyDwm();
            HideNativeCaption();
            src.AddHook(MaxHook);
        }

        [StructLayout(LayoutKind.Sequential)] struct MINMAXINFO { public int rx, ry, sx, sy, mx, my, px, py, tx, ty; }

        // With the sizing frame back, a maximized window overhangs the screen by the frame; pin it to the work area instead.
        static IntPtr MaxHook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg != 0x0024) return IntPtr.Zero;
            try
            {
                System.Drawing.Rectangle a = System.Windows.Forms.Screen.FromHandle(h).WorkingArea;
                System.Drawing.Rectangle b = System.Windows.Forms.Screen.FromHandle(h).Bounds;
                MINMAXINFO mi = (MINMAXINFO)Marshal.PtrToStructure(l, typeof(MINMAXINFO));
                mi.mx = a.X - b.X; mi.my = a.Y - b.Y; mi.sx = a.Width; mi.sy = a.Height;
                Marshal.StructureToPtr(mi, l, false);
            }
            catch { }
            return IntPtr.Zero;
        }

        // DWM paints its own minimize/maximize/close over the glass whenever WS_CAPTION is set, even with the Aero buttons off.
        // Dropping the caption (keeping the sizing frame, sysmenu and minimize box for the taskbar) leaves only our buttons.
        void HideNativeCaption()
        {
            if (Environment.OSVersion.Version.Build < 22000) return;
            const int WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000;
            int st = Native.GetWindowLong(hwnd, -16);
            int want = (st & ~WS_CAPTION) | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX;
            if (want == st) return;
            Native.SetWindowLong(hwnd, -16, want);
            Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x27); // NOSIZE | NOMOVE | NOZORDER | FRAMECHANGED
        }

        void ApplyDwm()
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int dark = Ds.Dark ? 1 : 0;
                // DWMWA_USE_IMMERSIVE_DARK_MODE is 20 from Windows 10 20H1 and 19 before it.
                if (Native.DwmSetWindowAttribute(hwnd, 20, ref dark, 4) != 0) Native.DwmSetWindowAttribute(hwnd, 19, ref dark, 4);
                int round = 2;
                Native.DwmSetWindowAttribute(hwnd, 33, ref round, 4);
                int none = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE: no accent-colored caption strip over the sidebar
                Native.DwmSetWindowAttribute(hwnd, 35, ref none, 4);
                if (HasMica)
                {
                    MARGINS m = new MARGINS { l = -1, r = -1, t = -1, b = -1 };
                    DwmExtendFrameIntoClientArea(hwnd, ref m);
                    int mica = 2; // DWMSBT_MAINWINDOW
                    Native.DwmSetWindowAttribute(hwnd, 38, ref mica, 4);
                }
            }
            catch { }
        }

        // Real close (the app is quitting); a user close is up to the subclass.
        public void Shutdown()
        {
            closing = true;
            Ds.Changed -= OnDs;
            Close();
        }

        public bool ShuttingDown { get { return closing; } }

        public bool IsDisposed { get; private set; }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsDisposed = true;
            Ds.Changed -= OnDs;
        }

        // For WinForms dialogs and menus that need an owner.
        public System.Windows.Forms.IWin32Window Win32 { get { return new Win32Owner(hwnd); } }

        class Win32Owner : System.Windows.Forms.IWin32Window
        {
            readonly IntPtr h;
            public Win32Owner(IntPtr h) { this.h = h; }
            public IntPtr Handle { get { return h; } }
        }
    }

    // Hand-drawn GDI+ art (mascot, intro) shown in a WPF tree: painted into a premultiplied DIB and copied into a
    // WriteableBitmap only when asked, so an idle layer costs nothing.
    public class GdiLayer : FrameworkElement
    {
        Dib dib;
        WriteableBitmap wb;
        int[] cur, prev;   // this paint and the one on screen: only what differs is uploaded
        float made = 1;   // DPI scale the bitmap was painted at
        public Action<D.Graphics, float> Painter;   // graphics in device pixels, and the DPI scale

        public GdiLayer()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        public float Scale { get { return (float)VisualTreeHelper.GetDpi(this).DpiScaleX; } }

        public void Refresh()
        {
            if (Painter == null || ActualWidth < 1 || ActualHeight < 1) return;
            float k = Scale;
            int w = Math.Max(1, (int)Math.Ceiling(ActualWidth * k)), h = Math.Max(1, (int)Math.Ceiling(ActualHeight * k));
            if (dib == null || dib.Width != w || dib.Height != h || made != k)
            {
                if (dib != null) dib.Dispose();
                dib = new Dib(w, h, true);
                wb = new WriteableBitmap(w, h, 96 * k, 96 * k, PixelFormats.Pbgra32, null);
                made = k;
                cur = new int[w * h];
                prev = new int[w * h];
                InvalidateVisual();
            }
            using (D.Graphics g = dib.Graphics())
            {
                g.Clear(D.Color.Transparent);
                g.SmoothingMode = D.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = D.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = D.Drawing2D.PixelOffsetMode.HighQuality;
                try { Painter(g, k); }
                catch (Exception ex) { ShotStack.Log("Capa GDI: " + ex.Message); }
            }
            Native.GdiFlush();
            Marshal.Copy(dib.Bits, cur, 0, w * h);
            int top = -1, bot = -1, left = w, right = -1;
            for (int y = 0; y < h; y++)
            {
                int o = y * w;
                int a = 0;
                while (a < w && cur[o + a] == prev[o + a]) a++;
                if (a == w) continue;
                int b = w - 1;
                while (cur[o + b] == prev[o + b]) b--;
                if (top < 0) top = y;
                bot = y;
                if (a < left) left = a;
                if (b > right) right = b;
            }
            if (top < 0) return;   // identical to what is on screen: nothing to upload
            wb.Lock();
            try
            {
                for (int y = top; y <= bot; y++)
                    Marshal.Copy(cur, y * w + left, IntPtr.Add(wb.BackBuffer, y * wb.BackBufferStride + left * 4), right - left + 1);
                wb.AddDirtyRect(new Int32Rect(left, top, right - left + 1, bot - top + 1));
            }
            finally { wb.Unlock(); }
            int[] t = prev; prev = cur; cur = t;
        }

        // Moved to a monitor with another scale: repaint at the new pixel size instead of stretching the old bitmap.
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            if (wb != null) Refresh();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (wb != null) dc.DrawImage(wb, new Rect(0, 0, wb.PixelWidth / made, wb.PixelHeight / made));
        }

        public void Release()
        {
            if (dib != null) { dib.Dispose(); dib = null; }
            wb = null;
        }
    }
}
