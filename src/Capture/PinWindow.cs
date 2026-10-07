// Stackshot - Pin a capture on screen as an always-on-top window.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Stackshot
{
    // Drag moves it; wheel zooms; Ctrl+wheel changes opacity; double-click or Esc closes; right-click opens a menu.
    public class PinWindow : Form
    {
        readonly Bitmap img;
        readonly float s;
        float zoom = 1f;
        bool hover, hotClose;

        public static void Open(Bitmap image)
        {
            new PinWindow(image).Show();
        }

        PinWindow(Bitmap image)
        {
            img = image;
            Screen scr = Grabber.CurrentScreen();
            s = ShotStack.ScaleFor(scr);
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Theme.Bg;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            // Fit within 70% of the screen, never upscale.
            Rectangle wa = scr.WorkingArea;
            zoom = Math.Min(1f, Math.Min(wa.Width * 0.7f / img.Width, wa.Height * 0.7f / img.Height));
            Size sz = Scaled();
            Point m = Control.MousePosition;
            Location = new Point(Math.Max(wa.Left, Math.Min(wa.Right - sz.Width, m.X - sz.Width / 2)),
                                 Math.Max(wa.Top, Math.Min(wa.Bottom - sz.Height, m.Y - sz.Height / 2)));
            ClientSize = sz;
            ContextMenuStrip = BuildMenu();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW
                cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW: no taskbar or Alt+Tab entry
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // DWMWCP_ROUND
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
                int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch { }
        }

        Size Scaled()
        {
            return new Size(Math.Max(40, (int)Math.Round(img.Width * zoom)), Math.Max(30, (int)Math.Round(img.Height * zoom)));
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Copiar", null, delegate { ShotStack.CopyImage(img); });
            m.Items.Add("Guardar como\u2026", null, delegate { SaveAs(); });
            m.Items.Add("Tama\u00F1o real", null, delegate { SetZoom(1f, new Point(ClientSize.Width / 2, ClientSize.Height / 2)); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Cerrar", null, delegate { BeginInvoke((Action)Close); }); // after the menu finishes, since closing disposes it
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

        // Zoom around the point under the cursor.
        void SetZoom(float z, Point anchor)
        {
            z = Math.Max(0.1f, Math.Min(4f, z));
            float fx = anchor.X / (float)Math.Max(1, ClientSize.Width), fy = anchor.Y / (float)Math.Max(1, ClientSize.Height);
            Point screenAnchor = PointToScreen(anchor);
            zoom = z;
            Size sz = Scaled();
            Bounds = new Rectangle(screenAnchor.X - (int)(fx * sz.Width), screenAnchor.Y - (int)(fy * sz.Height), sz.Width, sz.Height);
            Invalidate();
        }

        Rectangle CloseRect()
        {
            int d = (int)Math.Round(24 * s), m = (int)Math.Round(8 * s);
            return new Rectangle(ClientSize.Width - d - m, m, d, d);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = zoom > 1.5f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, ClientRectangle);
            if (!hover) return;
            Rectangle c = CloseRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(hotClose ? Theme.Red : Color.FromArgb(220, Theme.Dark))) g.FillEllipse(b, c);
            using (Font f = new Font(Theme.IconFont, (float)Math.Round(10 * s), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "\uE711", f, c, hotClose ? Color.White : Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = CloseRect().Contains(e.Location);
            if (!hover || h != hotClose) { hover = true; hotClose = h; Cursor = h ? Cursors.Hand : Cursors.SizeAll; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            hotClose = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (CloseRect().Contains(e.Location)) { Close(); return; }
            // Native move: smooth and with edge snapping.
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN on HTCAPTION
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) Close();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0) Opacity = Math.Max(0.2, Math.Min(1.0, Opacity + (e.Delta > 0 ? 0.1 : -0.1)));
            else SetZoom(zoom * (e.Delta > 0 ? 1.1f : 1 / 1.1f), e.Location);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyData == (Keys.Control | Keys.C)) ShotStack.CopyImage(img);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            img.Dispose();
            if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
        }
    }
}
