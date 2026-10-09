// Stackshot - Base class for floating windows (no focus, always on top, excluded from capture).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    // Base for stack thumbnails and pills: never activates, no taskbar entry, topmost, excluded from capture, animated
    // position and opacity.
    public abstract class FloatWindow : Form
    {
        public static bool ExcludeFromCapture = true;
        public double LastStep;    // last animation step time (owned by Anim)
        protected float s = 1f;
        protected double x, y, vx, vy, tx, ty;
        protected bool moving;
        protected readonly Tween alpha = new Tween(0);
        double k = 320, z = 0.8, moveAt;
        int shownX = int.MinValue, shownY = int.MinValue;
        byte shownAlpha;
        Color border = Theme.Border;

        protected FloatWindow()
        {
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00080000 | 0x00000080 | 0x00000008; // WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!PerPixel) Native.SetLayeredWindowAttributes(Handle, 0, shownAlpha, 2); // LWA_ALPHA: starts transparent and fades in
            try
            {
                int round = Rounded && !PerPixel ? 2 : 1; // DWMWCP_ROUND or DWMWCP_DONOTROUND
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
            }
            catch { }
            ApplyBorder();
            if (ExcludeFromCapture && !Native.SetWindowDisplayAffinity(Handle, 0x11)) // WDA_EXCLUDEFROMCAPTURE
            {
                ShotStack.Log("SetWindowDisplayAffinity fallo: " + Marshal.GetLastWin32Error());
            }
            if (PerPixel) Redraw();
        }

        protected int P(float v) { return (int)Math.Round(v * s); }

        // Windows 11 rounded corners (the recording frame strips opt out).
        protected virtual bool Rounded { get { return true; } }

        // Color of the thin DWM border (highlighted on hover).
        protected void SetBorder(Color c)
        {
            if (c == border) return;
            border = c;
            ApplyBorder();
        }

        void ApplyBorder()
        {
            if (!IsHandleCreated) return;
            int v = PerPixel ? unchecked((int)0xFFFFFFFE) : border.R | (border.G << 8) | (border.B << 16); // DWMWA_COLOR_NONE
            try { Native.DwmSetWindowAttribute(Handle, 34, ref v, 4); } catch { }
        }

        // Show without activating, on top of everything.
        protected void ShowQuiet()
        {
            if (Visible) return;
            Show();
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected void SetSize(Size size)
        {
            body = size;
            size = new Size(size.Width + 2 * Pad, size.Height + 2 * Pad);
            if (Size == size) return;
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, size.Width, size.Height, Native.SWP_NOMOVE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            else Size = size;
        }

        // Per-pixel windows: the visible body is inset by Pad (room for the soft shadow); x and y track the body.
        protected virtual bool PerPixel { get { return false; } }
        protected int Pad;
        protected Size body;
        Surface surface;

        protected virtual void PaintSurface(System.Windows.Media.DrawingContext dc, int w, int h) { }

        // Repaints a per-pixel window now (plain windows just invalidate).
        protected void Redraw()
        {
            if (!PerPixel) { Invalidate(); return; }
            if (!IsHandleCreated || IsDisposed) return;
            int w = body.Width + 2 * Pad, h = body.Height + 2 * Pad;
            if (w <= 0 || h <= 0) return;
            if (surface == null) surface = new Surface();
            surface.Paint(w, h, delegate(System.Windows.Media.DrawingContext dc) { PaintSurface(dc, w, h); });
            surface.Present(Handle, shownAlpha);
        }

        protected Bitmap SurfaceSnapshot(Rectangle part) { return surface == null ? null : surface.Snapshot(part); }

        // Frees the pixel buffer of a hidden window; the next Redraw creates it again.
        protected void ReleaseSurface()
        {
            if (surface != null) { surface.Dispose(); surface = null; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (surface != null) { surface.Dispose(); surface = null; }
        }

        protected void JumpTo(double nx, double ny)
        {
            x = tx = nx;
            y = ty = ny;
            vx = vy = 0;
            moving = false;
            ApplyPos();
        }

        protected void MoveTo(double nx, double ny, double stiffness, double damping, double delay)
        {
            tx = nx;
            ty = ny;
            k = stiffness;
            z = damping;
            moveAt = Anim.Now + delay;
            moving = true;
            Anim.Wake(this);
        }

        protected void ApplyPos()
        {
            int ix = (int)Math.Round(x) - Pad, iy = (int)Math.Round(y) - Pad;
            if (ix == shownX && iy == shownY) return;
            shownX = ix;
            shownY = iy;
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, IntPtr.Zero, ix, iy, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            else Location = new Point(ix, iy);
        }

        protected virtual double AlphaFactor() { return 1; }

        protected void ApplyAlpha()
        {
            double a = alpha.Value * AlphaFactor();
            byte b = (byte)Math.Max(0, Math.Min(255, Math.Round(a * 255)));
            if (b == shownAlpha) return;
            shownAlpha = b;
            if (!IsHandleCreated) return;
            if (PerPixel) Native.FadeLayered(Handle, b);
            else Native.SetLayeredWindowAttributes(Handle, 0, b, 2);
        }

        // While true, the mouse drives the horizontal position (swipe to dismiss).
        protected virtual bool HoldX { get { return false; } }
        protected virtual void Settled() { }
        // Per-window animations; returns true while any is running.
        protected virtual bool StepExtra(double now) { return false; }

        // One animation step. Returns false once everything is still.
        public bool Step(double now)
        {
            double dt = Math.Max(0, Math.Min(0.05, (now - LastStep) / 1000.0));
            LastStep = now;
            if (moving && now >= moveAt)
            {
                bool hold = HoldX;
                if (!hold) Anim.Spring(ref x, ref vx, tx, k, z, dt);
                Anim.Spring(ref y, ref vy, ty, k, z, dt);
                bool xDone = hold || (Math.Abs(tx - x) < 0.5 && Math.Abs(vx) < 12);
                bool settled = xDone && Math.Abs(ty - y) < 0.5 && Math.Abs(vy) < 12;
                if (settled)
                {
                    if (!hold) { x = tx; vx = 0; }
                    y = ty;
                    vy = 0;
                    moving = false;
                }
                ApplyPos();
                if (settled) Settled();
            }
            alpha.Step(now);
            if (IsDisposed) return false; // the exit animation ends by closing it
            bool more = StepExtra(now);
            if (IsDisposed) return false;
            ApplyAlpha();
            return moving || alpha.Running || more;
        }

        protected static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        protected static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)Math.Round(a.A + (b.A - a.A) * t), (int)Math.Round(a.R + (b.R - a.R) * t),
                                  (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        protected static void DrawGlyph(Graphics g, string glyph, Rectangle r, Color c, int px)
        {
            TextKit.Draw(g, glyph, Fonts.Get(Theme.IconFont, Math.Max(1, px)), r, c, TextFormatFlags.HorizontalCenter |
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

}
