// Stackshot - Pin a capture on screen as an always-on-top window.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // Drag moves it; wheel zooms; Ctrl+wheel changes opacity; double-click or Esc closes; right-click opens a menu.
    // A per-pixel layered window: rounded image, 1 px hairline and a soft shadow around it (Pad px of margin).
    // The image and its shadow are rendered once per size and theme; hovering only swaps the close button in and out.
    public class PinWindow : Form
    {
        readonly Bitmap img;
        readonly MI.BitmapSource src;
        readonly float s;
        readonly int Pad;
        readonly float maxZoom;
        float zoom = 1f;
        bool hover, hotClose;
        byte opacity = 255;
        Size body;
        Dib dib;                                  // what the window shows (premultiplied)
        string drawnFor;                          // size and theme the image layer in dib was rendered for
        Dib under;                                // image pixels under the close button while it is shown
        readonly Dib[] closeButton = new Dib[2];  // normal and hot
        int buttonShown = -1;
        MI.BitmapSource shadowTile;
        string shadowKey;
        readonly Action restyle;

        public static void Open(Bitmap image)
        {
            new PinWindow(image).Show();
        }

        PinWindow(Bitmap image)
        {
            img = image;
            src = Ink.FromGdi(image);
            Screen scr = Grabber.CurrentScreen();
            s = ShotStack.ScaleFor(scr);
            Pad = P(24);
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            // Fit within 70% of the screen, never upscale. Zooming in stops at 4x, or at 1.5 screens for large
            // captures (never below their real size): beyond that the window would only cost memory.
            Rectangle wa = scr.WorkingArea;
            zoom = Math.Min(1f, Math.Min(wa.Width * 0.7f / img.Width, wa.Height * 0.7f / img.Height));
            maxZoom = Math.Max(1f, Math.Min(4f, Math.Min(wa.Width * 1.5f / img.Width, wa.Height * 1.5f / img.Height)));
            body = Scaled();
            Point m = Control.MousePosition;
            int bx = Math.Max(wa.Left, Math.Min(wa.Right - body.Width, m.X - body.Width / 2));
            int by = Math.Max(wa.Top, Math.Min(wa.Bottom - body.Height, m.Y - body.Height / 2));
            Bounds = new Rectangle(bx - Pad, by - Pad, body.Width + 2 * Pad, body.Height + 2 * Pad);
            ContextMenuStrip = BuildMenu();
            restyle = delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)Redraw); };
            Ds.Changed += restyle;
        }

        int P(float v) { return (int)Math.Round(v * s); }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000;  // WS_EX_TOOLWINDOW (no taskbar or Alt+Tab entry) | WS_EX_LAYERED
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redraw();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        Size Scaled()
        {
            return new Size(Math.Max(40, (int)Math.Round(img.Width * zoom)), Math.Max(30, (int)Math.Round(img.Height * zoom)));
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = TrayMenu.Create();
            m.Items.Add(TrayMenu.Item("Copiar", "copy", delegate { ShotStack.CopyImage(img); }));
            m.Items.Add(TrayMenu.Item("Guardar como\u2026", "save", delegate { SaveAs(); }));
            m.Items.Add(TrayMenu.Item("Tama\u00F1o real", "actualsize", delegate { SetZoom(1f, new Point(body.Width / 2, body.Height / 2)); }));
            m.Items.Add(TrayMenu.Separator());
            m.Items.Add(TrayMenu.Item("Cerrar", "close", delegate { BeginInvoke((Action)Close); })); // after the menu finishes, since closing disposes it
            return m;
        }

        void SaveAs()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Imagen PNG|*.png";
                d.FileName = "Stackshot " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png";
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try { img.Save(d.FileName, ImageFormat.Png); }
                    catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            }
        }

        // Zoom around the point under the cursor (body coordinates): resized, moved and repainted in one update.
        void SetZoom(float z, Point anchor)
        {
            z = Math.Max(0.1f, Math.Min(maxZoom, z));
            if (z == zoom || !IsHandleCreated) return;
            float fx = anchor.X / (float)Math.Max(1, body.Width), fy = anchor.Y / (float)Math.Max(1, body.Height);
            Native.RECT wr;
            Native.GetWindowRect(Handle, out wr);
            Point screenAnchor = new Point(wr.Left + Pad + anchor.X, wr.Top + Pad + anchor.Y);
            zoom = z;
            body = Scaled();
            Redraw(screenAnchor.X - (int)Math.Round(fx * body.Width) - Pad, screenAnchor.Y - (int)Math.Round(fy * body.Height) - Pad);
        }

        double Radius { get { return Math.Min(P(10), Math.Min(body.Width, body.Height) / 4.0); } }

        Rectangle CloseRect()
        {
            int d = P(24), m = P(8);
            return new Rectangle(body.Width - d - m, m, d, d);
        }

        // The close button's patch, in window coordinates (room for its faint shadow).
        Rectangle ButtonPatch()
        {
            Rectangle c = CloseRect();
            c.Offset(Pad, Pad);
            return Rectangle.Inflate(c, 3, 3);
        }

        void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Native.RECT wr;
            Native.GetWindowRect(Handle, out wr);
            Redraw(wr.Left, wr.Top);
        }

        void Redraw(int x, int y)
        {
            if (!IsHandleCreated || IsDisposed) return;
            int w = body.Width + 2 * Pad, h = body.Height + 2 * Pad;
            string key = w + "x" + h + "|" + zoom + "|" + Ds.Dark;
            if (key != drawnFor)
            {
                if (dib == null || dib.Width != w || dib.Height != h)
                {
                    if (dib != null) dib.Dispose();
                    dib = new Dib(w, h, true);
                }
                MI.RenderTargetBitmap rtb = Ink.Render(w, h, delegate(M.DrawingContext dc) { PaintImage(dc, w, h); }, null);
                Native.GdiFlush();
                rtb.CopyPixels(new W.Int32Rect(0, 0, w, h), dib.Bits, w * 4 * h, w * 4);
                drawnFor = key;
                buttonShown = -1;
            }
            ShowButton(hover ? (hotClose ? 1 : 0) : -1);
            Present(x, y, w, h);
        }

        // Shadow, the image clipped to the rounded body (sharp pixels when zoomed in) and its hairline.
        void PaintImage(M.DrawingContext dc, int w, int h)
        {
            double r = Radius;
            PaintShadow(dc, w, h, r);
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            W.Rect b = new W.Rect(0, 0, body.Width, body.Height);
            M.DrawingGroup pic = new M.DrawingGroup();
            M.RenderOptions.SetBitmapScalingMode(pic, zoom > 1.5f ? M.BitmapScalingMode.NearestNeighbor : M.BitmapScalingMode.HighQuality);
            using (M.DrawingContext pc = pic.Open())
            {
                pc.PushClip(new M.RectangleGeometry(b, r, r));
                pc.DrawImage(src, b);
                pc.Pop();
            }
            dc.DrawDrawing(pic);
            Ink.Hairline(dc, Ds.Dark ? Ds.Argb(0.16, 255, 255, 255) : Ds.Argb(0.14, 0, 0, 0), b, r);
            dc.Pop();
        }

        // -1 = none, 0 = close button, 1 = close button hot. The image underneath is kept to restore it.
        void ShowButton(int state)
        {
            if (state == buttonShown) return;
            Rectangle r = ButtonPatch();
            if (buttonShown >= 0 && under != null)
                Native.BitBlt(dib.Dc, r.X, r.Y, r.Width, r.Height, under.Dc, 0, 0, Native.SRCCOPY);
            if (state >= 0)
            {
                if (under == null || under.Width != r.Width || under.Height != r.Height)
                {
                    if (under != null) under.Dispose();
                    under = new Dib(r.Width, r.Height, true);
                }
                Native.BitBlt(under.Dc, 0, 0, r.Width, r.Height, dib.Dc, r.X, r.Y, Native.SRCCOPY);
                Dib b = Button(state == 1, r.Size);
                Native.AlphaBlend(dib.Dc, r.X, r.Y, r.Width, r.Height, b.Dc, 0, 0, r.Width, r.Height, (255 << 16) | (1 << 24)); // AC_SRC_ALPHA
            }
            Native.GdiFlush();
            buttonShown = state;
        }

        Dib Button(bool hot, Size size)
        {
            int i = hot ? 1 : 0;
            Dib d = closeButton[i];
            if (d != null && d.Width == size.Width && d.Height == size.Height) return d;
            if (d != null) d.Dispose();
            d = new Dib(size.Width, size.Height, true);
            double cw = CloseRect().Width;
            MI.RenderTargetBitmap rtb = Ink.Render(size.Width, size.Height, delegate(M.DrawingContext dc)
            {
                W.Point center = new W.Point(size.Width / 2.0, size.Height / 2.0);
                dc.DrawEllipse(Ds.Brush(Ds.Argb(0.14, 0, 0, 0)), null, new W.Point(center.X, center.Y + 0.5), cw / 2.0 + 1, cw / 2.0 + 1);
                dc.DrawEllipse(Ds.Brush(hot ? Ds.Argb(0.92, 58, 58, 62) : Palette.Hud), null, center, cw / 2.0, cw / 2.0);
                M.Pen ring = new M.Pen(Ds.Brush(Palette.HudLine), 1);
                ring.Freeze();
                dc.DrawEllipse(null, ring, center, cw / 2.0 - 0.5, cw / 2.0 - 0.5);
                double gs = P(14);
                Glyph.Draw(dc, "close", center.X - gs / 2, center.Y - gs / 2, gs, hot ? Palette.HudLabel : Ds.Argb(0.85, 255, 255, 255), Math.Max(1.4, 1.7 * s));
            }, null);
            Native.GdiFlush();
            rtb.CopyPixels(new W.Int32Rect(0, 0, size.Width, size.Height), d.Bits, size.Width * 4 * size.Height, size.Width * 4);
            closeButton[i] = d;
            return d;
        }

        [StructLayout(LayoutKind.Sequential)] struct Blend { public byte Op, Flags, Alpha, Format; }

        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow")]
        static extern bool UpdateLayered(IntPtr hwnd, IntPtr hdcDst, ref Native.POINT dst, ref Native.SIZE size, IntPtr hdcSrc, ref Native.POINT src, int key, ref Blend blend, int flags);

        // Position, size, pixels and opacity in a single update, so zooming never shows a stretched or shifted frame.
        void Present(int x, int y, int w, int h)
        {
            Native.POINT dst; dst.X = x; dst.Y = y;
            Native.POINT from; from.X = 0; from.Y = 0;
            Native.SIZE size; size.cx = w; size.cy = h;
            Blend bf = new Blend();
            bf.Alpha = opacity;
            bf.Format = 1; // AC_SRC_ALPHA
            UpdateLayered(Handle, IntPtr.Zero, ref dst, ref size, dib.Dc, ref from, 0, ref bf, 2); // ULW_ALPHA
            if (Left != x || Top != y || Width != w || Height != h) UpdateBounds(); // WinForms is not told about it
        }

        // Shadow drawn as a nine-slice of one small pre-blurred tile, so zooming never re-blurs a large surface.
        void PaintShadow(M.DrawingContext dc, int w, int h, double r)
        {
            int blur = P(16), off = P(5);
            int c = (int)Math.Ceiling(r) + blur + off + 2;
            int t = 2 * Pad + 2 * c;
            M.Color col = Ds.Dark ? Ds.Argb(0.60, 0, 0, 0) : Ds.Argb(0.36, 0, 0, 0);
            if (body.Width < 2 * c || body.Height < 2 * c)
            {
                dc.DrawImage(Ink.Shadow(w, h, new W.Rect(Pad, Pad + off, body.Width, body.Height), r, blur, col), new W.Rect(0, 0, w, h));
                return;
            }
            if (shadowTile == null || shadowTile.PixelWidth != t || shadowKey != col.ToString() + r)
            {
                shadowTile = Ink.Shadow(t, t, new W.Rect(Pad, Pad + off, 2 * c, 2 * c), r, blur, col);
                shadowKey = col.ToString() + r;
            }
            int half = t / 2;
            int[] sx = { 0, half - 1, half + 1 }, sw = { half - 1, 2, t - half - 1 };
            int[] dx = { 0, half - 1, w - (t - half - 1) }, dw = { half - 1, w - t + 2, t - half - 1 };
            int[] dy = { 0, half - 1, h - (t - half - 1) }, dh = { half - 1, h - t + 2, t - half - 1 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1) continue; // hidden under the image
                    MI.CroppedBitmap part = new MI.CroppedBitmap(shadowTile, new W.Int32Rect(sx[i], sx[j], sw[i], sw[j]));
                    dc.DrawImage(part, new W.Rect(dx[i], dy[j], dw[i], dh[j]));
                }
        }

        Point Local(Point p) { return new Point(p.X - Pad, p.Y - Pad); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = CloseRect().Contains(Local(e.Location));
            if (!hover || h != hotClose) { hover = true; hotClose = h; Cursor = h ? Cursors.Hand : Cursors.SizeAll; Redraw(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!hover) return;
            hover = false;
            hotClose = false;
            Redraw();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            // Double-click closes. Handled here: the native move below takes the button-up, so no double-click event
            // would ever arrive.
            if (e.Clicks >= 2 || CloseRect().Contains(Local(e.Location))) { Close(); return; }
            // Native move: smooth and with edge snapping.
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN on HTCAPTION
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0)
            {
                int a = Math.Max(51, Math.Min(255, opacity + (e.Delta > 0 ? 26 : -26)));
                if (a == opacity) return;
                opacity = (byte)a;
                Native.FadeLayered(Handle, opacity);
            }
            else SetZoom(zoom * (e.Delta > 0 ? 1.1f : 1 / 1.1f), Local(e.Location));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Point center = new Point(body.Width / 2, body.Height / 2);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyData == (Keys.Control | Keys.C)) ShotStack.CopyImage(img);
            else if (e.KeyData == (Keys.Control | Keys.D0) || e.KeyData == (Keys.Control | Keys.NumPad0)) SetZoom(1f, center);
            else if (e.KeyData == (Keys.Control | Keys.Oemplus) || e.KeyData == (Keys.Control | Keys.Add)) SetZoom(zoom * 1.25f, center);
            else if (e.KeyData == (Keys.Control | Keys.OemMinus) || e.KeyData == (Keys.Control | Keys.Subtract)) SetZoom(zoom / 1.25f, center);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Ds.Changed -= restyle;
            if (dib != null) { dib.Dispose(); dib = null; }
            if (under != null) { under.Dispose(); under = null; }
            for (int i = 0; i < closeButton.Length; i++) if (closeButton[i] != null) { closeButton[i].Dispose(); closeButton[i] = null; }
            img.Dispose();
            if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
        }
    }
}
