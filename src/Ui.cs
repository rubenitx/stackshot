// Stackshot - Controles de interfaz propios: ventana oscura sin marco, botones, interruptores y progreso.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Fonts
    {
        static string display, displaySemibold;

        // La variante seminegrita para títulos (Windows recorta su nombre a 31 letras).
        public static string DisplaySemibold
        {
            get
            {
                if (displaySemibold != null) return displaySemibold;
                displaySemibold = "Segoe UI Semibold";
                using (InstalledFontCollection fc = new InstalledFontCollection())
                {
                    foreach (FontFamily f in fc.Families)
                    {
                        if (f.Name.StartsWith("Segoe UI Variable Display Semib")) { displaySemibold = f.Name; break; }
                    }
                }
                return displaySemibold;
            }
        }

        // Segoe UI Variable en Windows 11; Segoe UI en Windows 10.
        public static string Display
        {
            get
            {
                if (display != null) return display;
                display = "Segoe UI";
                using (InstalledFontCollection fc = new InstalledFontCollection())
                {
                    foreach (FontFamily f in fc.Families)
                    {
                        if (f.Name == "Segoe UI Variable Display") { display = f.Name; break; }
                    }
                }
                return display;
            }
        }
    }

    // Ventana de diálogo oscura, sin marco, con esquinas redondeadas y sombra. Se arrastra desde cualquier hueco.
    public class DarkForm : Form
    {
        protected readonly float s;
        readonly Rectangle closeArea;
        bool closeHot;

        public DarkForm(int width, int height)
        {
            s = ShotStack.ScaleFor(Grabber.CurrentScreen());
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            KeyPreview = true;
            MaximizeBox = false; // doble clic en un hueco no debe maximizarla
            MinimizeBox = false;
            Text = "Stackshot";
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            ClientSize = new Size(P(width), P(height));
            Rectangle wa = Grabber.CurrentScreen().WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
            int d = P(30);
            closeArea = new Rectangle(ClientSize.Width - d - P(12), P(12), d, d);
        }

        protected int P(float v) { return (int)Math.Round(v * s); }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
                int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Arrastrar la ventana desde cualquier hueco (menos la ✕).
            if (m.Msg == 0x84 && m.Result == (IntPtr)1)
            {
                Point p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                if (!closeArea.Contains(p)) m.Result = (IntPtr)2;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (closeHot)
            {
                using (SolidBrush b = new SolidBrush(Theme.ButtonHover)) g.FillEllipse(b, closeArea);
            }
            using (Font f = new Font(Theme.IconFont, P(11), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "\uE711", f, closeArea, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = closeArea.Contains(e.Location);
            if (h != closeHot) { closeHot = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(closeArea); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (closeHot) { closeHot = false; Invalidate(closeArea); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && closeArea.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }

        // Etiqueta de texto sin fondo.
        protected Label AddLabel(string text, int x, int y, int w, float px, Color color, bool bold, ContentAlignment align)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.BackColor = Color.Transparent;
            l.ForeColor = color;
            l.TextAlign = align;
            l.UseMnemonic = false;
            l.Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", P(px), GraphicsUnit.Pixel);
            Size sz = TextRenderer.MeasureText(text, l.Font, new Size(P(w), int.MaxValue), TextFormatFlags.WordBreak);
            l.Bounds = new Rectangle(P(x), P(y), P(w), sz.Height + P(2));
            Controls.Add(l);
            return l;
        }
    }

    // Botón redondeado. Accent = el principal (azul).
    public class Pill : Control
    {
        public bool Accent;
        bool hot, down;

        public Pill(string text, bool accent)
        {
            Text = text;
            Accent = accent;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = Accent ? (down ? Theme.Fg2 : hot ? Theme.Purple : Theme.Accent) : (down ? Theme.Border : hot ? Theme.ButtonHover : Theme.Button);
            if (!Enabled) bg = Theme.Button;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(r, Height / 2f))
            using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
            Color fg = Accent ? Theme.Dark : Enabled ? Theme.Fg : Theme.Muted;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    // Interruptor como los de iPhone, con la bola deslizándose.
    public class Toggle : Control
    {
        bool on;
        float pos;
        readonly Timer anim = new Timer();
        public event EventHandler CheckedChanged;

        public Toggle(bool value)
        {
            on = value;
            pos = value ? 1 : 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
            anim.Interval = 12;
            anim.Tick += delegate
            {
                float target = on ? 1 : 0;
                pos += (target - pos) * 0.35f;
                if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                anim.Start();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF track = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Color c = Color.FromArgb(255,
                (int)(Theme.Button.R + (Theme.Accent.R - Theme.Button.R) * pos),
                (int)(Theme.Button.G + (Theme.Accent.G - Theme.Button.G) * pos),
                (int)(Theme.Button.B + (Theme.Accent.B - Theme.Button.B) * pos));
            using (GraphicsPath p = Theme.Round(track, Height / 2f))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
            float d = Height - 6, x = 3 + (Width - 6 - d) * pos;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, x, 4, d, d);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x, 3, d, d);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }

    // Barra de progreso fina y redondeada.
    public class Progress : Control
    {
        double value;

        public Progress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public double Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0, 0, Width, Height);
            using (GraphicsPath p = Theme.Round(r, Height / 2f))
            using (SolidBrush b = new SolidBrush(Theme.Button)) g.FillPath(b, p);
            if (value <= 0) return;
            RectangleF f = new RectangleF(0, 0, (float)Math.Max(Height, Width * value), Height);
            using (GraphicsPath p = Theme.Round(f, Height / 2f))
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(1, Width), Height), Theme.Accent, Theme.Purple, 0f))
                g.FillPath(b, p);
        }
    }
}
