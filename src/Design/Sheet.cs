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

        // Text snapped to whole pixels (Display) and ClearType at every scale: the sharpest Windows can draw. ClearType
        // is asked for explicitly because the Mica backdrop makes the window see-through, and WPF would otherwise fall
        // back to grey, softer text there.
        void SharpText()
        {
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(Root, ClearTypeHint.Enabled);
        }

        public Sheet()
        {
            AllowsTransparency = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanMinimize;
            UseLayoutRounding = true;
            FontFamily = Ds.Text;
            FontSize = 13;
            SharpText();
            Loaded += delegate { SharpText(); };
            DpiChanged += delegate { SharpText(); };
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
            Access.Track(this);
            SizeChanged += delegate(object o, SizeChangedEventArgs e) { Refit(e.PreviousSize); };
            LocationChanged += delegate { if (!placing && IsLoaded) moved = true; };
        }

        bool moved, placing;

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
            captions.Children.Add(new CaptionButton("minus", false, I18n.T("Minimizar", "Minimize"), delegate { WindowState = WindowState.Minimized; }));
            captions.Children.Add(new CaptionButton("close", true, I18n.Close, delegate { Close(); }));
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

            public void SetIcon(string icon, string tip)
            {
                glyph.Icon = icon;
                ToolTip = tip;
                AutomationProperties.SetName(this, tip);
            }

            public bool Inactive { set { if (inactive == value) return; inactive = value; Paint(); } }

            protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() { return new ActionPeer(this, act); }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                if (pal.High) { Background = hot ? Ds.Brush(pal.Accent) : Brushes.Transparent; glyph.Color = hot ? pal.OnAccent : inactive ? pal.Label3 : pal.Label; return; }
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
            card.SetValue(Border.BackgroundProperty, Ds.Brush(pal.High ? pal.Window : pal.Dark ? Ds.Rgb(50, 50, 53) : Ds.Rgb(250, 250, 251)));
            card.SetValue(Border.BorderBrushProperty, Ds.Brush(pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)));
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
            s.Setters.Add(new Setter(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display));   // small text: on whole pixels
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
            public TipShadow() : base(7, 8, 2, Ds.Brushes.High ? 0 : Ds.Dark ? 0.45 : 0.16) { }
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
            if (HighContrast.OsMica) src.CompositionTarget.BackgroundColor = Colors.Transparent;   // Mica may come back with the theme
            ApplyDwm();
            HideNativeCaption();
            src.AddHook(MaxHook);
            FitWorkArea();
        }

        // A page that scrolls only when the window can't show all of it; the bar appears just then.
        protected static ScrollViewer Scrolling(FrameworkElement content)
        {
            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Background = Brushes.Transparent,
                Resources = HomeWindow.ScrollStyle()
            };
        }

        // Never larger than the work area of the screen it opens on (CenterScreen uses the one under the pointer): a
        // 1366 x 768 laptop at 150 % leaves about 480 px of height. Windows that size to their content scroll inside.
        void FitWorkArea()
        {
            try
            {
                System.Windows.Forms.Screen scr = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
                double s = Math.Max(1f, ShotStack.ScaleFor(scr));
                double w = Math.Max(320, scr.WorkingArea.Width / s - 16), h = Math.Max(240, scr.WorkingArea.Height / s - 16);
                if (MinWidth > w) MinWidth = w;
                if (MinHeight > h) MinHeight = h;
                if (!double.IsNaN(Width) && Width > w) Width = w;
                if (!double.IsNaN(Height) && Height > h) Height = h;
                // Only a window that sizes itself to its content needs a ceiling (to scroll instead of growing past the
                // screen); a resizable one keeps no cap, so it can still be made bigger on a larger monitor.
                if ((SizeToContent == SizeToContent.Height || SizeToContent == SizeToContent.WidthAndHeight) && MaxHeight > h) MaxHeight = h;
            }
            catch (Exception ex) { ShotStack.Log("Ventana al \u00E1rea de trabajo: " + ex.Message); }
        }

        // A window that sizes itself to its content and grows after opening (another page, a longer list) stays on its
        // screen: one never moved by hand grows around its middle, as it opened centred, and any of them is pushed back
        // inside the work area instead of running past the bottom.
        void Refit(Size before)
        {
            if (SizeToContent == SizeToContent.Manual || !IsLoaded || hwnd == IntPtr.Zero || WindowState != WindowState.Normal || before.Height <= 0) return;
            try
            {
                System.Windows.Forms.Screen scr = System.Windows.Forms.Screen.FromHandle(hwnd);
                double k = Math.Max(1f, ShotStack.ScaleFor(scr));
                D.Rectangle wa = scr.WorkingArea;
                double wl = wa.Left / k, wt = wa.Top / k, wr = wa.Right / k, wb = wa.Bottom / k, h = Math.Max(240, wa.Height / k - 16);
                if (MaxHeight > h) MaxHeight = h;
                double top = Top, left = Left;
                if (!moved)
                {
                    top -= (ActualHeight - before.Height) / 2;
                    left -= (ActualWidth - before.Width) / 2;
                }
                top = Math.Max(wt + 8, Math.Min(top, wb - ActualHeight - 8));
                left = Math.Max(wl + 8, Math.Min(left, wr - ActualWidth - 8));
                if (Math.Abs(top - Top) < 0.5 && Math.Abs(left - Left) < 0.5) return;
                placing = true;
                try { Top = top; Left = left; }
                finally { placing = false; }
            }
            catch (Exception ex) { ShotStack.Log("Ventana al \u00E1rea de trabajo: " + ex.Message); }
        }

        // Drags the window from any spot a click does nothing on (the page, a title, a paragraph); buttons, boxes, links
        // and scroll bars keep their clicks.
        protected void DragAnywhere()
        {
            Root.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
            {
                if (e.ClickCount != 1 || Clickable(e.OriginalSource as DependencyObject)) return;
                try { DragMove(); } catch { }
            };
        }

        static bool Clickable(DependencyObject d)
        {
            for (int i = 0; d != null && i < 64; i++)
            {
                if (d is System.Windows.Documents.Hyperlink || d is System.Windows.Controls.Primitives.ButtonBase || d is System.Windows.Controls.Primitives.TextBoxBase ||
                    d is PasswordBox || d is System.Windows.Controls.Primitives.Thumb || d is System.Windows.Controls.Primitives.ScrollBar ||
                    d is System.Windows.Controls.Primitives.Selector || d is System.Windows.Controls.Primitives.RangeBase || d is Drawn) return true;
                ITap tap = d as ITap;
                if (tap != null && (tap.State.Interactive || tap.State.Do != null)) return true;
                FrameworkElement fe = d as FrameworkElement;
                if (fe != null && (fe.Cursor == Cursors.Hand || fe.Cursor == Cursors.IBeam)) return true;
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
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

        // A window that can be maximized and snapped (Win + Z shows Windows 11's snap layouts, Win + arrows snap it).
        protected virtual bool Snaps { get { return false; } }

        // DWM paints its own minimize/maximize/close over the glass whenever WS_CAPTION is set, even with the Aero buttons off.
        // Dropping the caption (keeping the sizing frame, sysmenu and minimize box for the taskbar) leaves only our buttons.
        void HideNativeCaption()
        {
            if (Environment.OSVersion.Version.Build < 22000) return;
            const int WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000;
            int st = Native.GetWindowLong(hwnd, -16);
            int want = (st & ~WS_CAPTION) | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX;
            if (Snaps) want |= WS_MAXIMIZEBOX;   // without a caption DWM draws no button for it
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

        // WriteableBitmap.Lock waits for WPF's render thread and can run other messages meanwhile (a DPI change, the next
        // frame): a Refresh reached that way would replace the bitmap under the one in progress. It waits for the next.
        bool refreshing;

        public void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try { RefreshNow(); }
            finally { refreshing = false; }
        }

        void RefreshNow()
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
            WriteableBitmap target = wb;   // the one locked is the one unlocked, whatever happens meanwhile
            target.Lock();
            try
            {
                for (int y = top; y <= bot; y++)
                    Marshal.Copy(cur, y * w + left, IntPtr.Add(target.BackBuffer, y * target.BackBufferStride + left * 4), right - left + 1);
                target.AddDirtyRect(new Int32Rect(left, top, right - left + 1, bot - top + 1));
            }
            finally { target.Unlock(); }
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
