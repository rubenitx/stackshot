// Stackshot - Base de las ventanas flotantes (sin foco, siempre encima, fuera de las capturas).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    // ---------------------------------------------------------------- Ventanas flotantes

    // Base de las miniaturas y de las pastillas de la pila: no roban el foco, no salen en la barra de tareas,
    // están siempre encima, quedan fuera de las capturas y se mueven y funden con animación.
    public abstract class FloatWindow : Form
    {
        public static bool ExcludeFromCapture = true;
        public double LastStep;    // último paso de animación (lo lleva Anim)
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
                cp.ExStyle |= 0x08000000 | 0x00080000 | 0x00000080 | 0x00000008; // NOACTIVATE | LAYERED | TOOLWINDOW | TOPMOST
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
            Native.SetLayeredWindowAttributes(Handle, 0, shownAlpha, 2); // LWA_ALPHA: nace transparente y entra con fundido
            try
            {
                int round = Rounded ? 2 : 1; // DWMWCP_ROUND o DWMWCP_DONOTROUND
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
            }
            catch { }
            ApplyBorder();
            if (ExcludeFromCapture && !Native.SetWindowDisplayAffinity(Handle, 0x11)) // WDA_EXCLUDEFROMCAPTURE
            {
                ShotStack.Log("SetWindowDisplayAffinity fallo: " + Marshal.GetLastWin32Error());
            }
        }

        protected int P(float v) { return (int)Math.Round(v * s); }

        // Esquinas redondeadas de Windows 11 (las tiras del marco de grabación no las quieren).
        protected virtual bool Rounded { get { return true; } }

        // Color del borde fino que dibuja Windows alrededor (se ilumina al pasar el ratón).
        protected void SetBorder(Color c)
        {
            if (c == border) return;
            border = c;
            ApplyBorder();
        }

        void ApplyBorder()
        {
            if (!IsHandleCreated) return;
            int v = border.R | (border.G << 8) | (border.B << 16);
            try { Native.DwmSetWindowAttribute(Handle, 34, ref v, 4); } catch { }
        }

        // La muestra sin activarla y por encima de todo.
        protected void ShowQuiet()
        {
            if (Visible) return;
            Show();
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected void SetSize(Size size)
        {
            if (Size == size) return;
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, size.Width, size.Height, Native.SWP_NOMOVE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            else Size = size;
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
            int ix = (int)Math.Round(x), iy = (int)Math.Round(y);
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
            if (IsHandleCreated) Native.SetLayeredWindowAttributes(Handle, 0, b, 2);
        }

        // Mientras es true, la posición horizontal la lleva el ratón (deslizar para descartar).
        protected virtual bool HoldX { get { return false; } }
        protected virtual void Settled() { }
        // Animaciones propias de cada ventana; devuelve true si alguna sigue en marcha.
        protected virtual bool StepExtra(double now) { return false; }

        // Un paso de animación. Devuelve false cuando ya está todo quieto.
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
            if (IsDisposed) return false; // la animación de salida acaba cerrándola
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
            using (Font f = new Font(Theme.IconFont, Math.Max(1, px), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                                         TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

}
