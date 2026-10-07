// Stackshot - Menú de la bandeja con el aspecto de los menús de macOS: oscuro, redondeado, con iconos.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Stackshot
{
    // Cada opción lleva en Tag el nombre de su icono (Icons). El menú se reajusta a la escala de la pantalla en
    // la que se abre; en Windows 11 las esquinas, el borde y la sombra los pone el propio sistema.
    public static class TrayMenu
    {
        public static ContextMenuStrip Create()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            MacMenuRenderer r = new MacMenuRenderer();
            menu.Renderer = r;
            menu.ShowImageMargin = true;
            menu.ShowCheckMargin = false;
            menu.BackColor = Mac.Menu;
            menu.ForeColor = Mac.Text;
            menu.DropShadowEnabled = Environment.OSVersion.Version.Build < 22000; // en 11 la sombra es la de DWM
            menu.Opening += delegate { Restyle(menu, r); };
            menu.Opened += delegate { Round(menu.Handle); };
            return menu;
        }

        public static ToolStripMenuItem Item(string text, string icon, EventHandler click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text, null, click);
            it.Tag = icon;
            it.ImageScaling = ToolStripItemImageScaling.None;
            return it;
        }

        public static ToolStripSeparator Separator()
        {
            return new ToolStripSeparator();
        }

        static void Restyle(ContextMenuStrip menu, MacMenuRenderer r)
        {
            float s = ShotStack.ScaleFor(Screen.FromPoint(Control.MousePosition));
            if (r.S == s && menu.Font != null && menu.Tag != null) return;
            r.S = s;
            menu.Tag = "ok";
            // Solo se sueltan las fuentes creadas aquí (la de serie de los menús es compartida).
            Font oldFont = menuFont, oldBold = appFont;
            menuFont = new Font(Mac.TextFont, 13 * s, GraphicsUnit.Pixel);
            appFont = new Font(Mac.TextFont, 13 * s, FontStyle.Bold, GraphicsUnit.Pixel);
            menu.Font = menuFont;
            menu.Padding = new Padding(0, P(5, s), 0, P(5, s));
            menu.ImageScalingSize = new Size(P(18, s), P(18, s));
            foreach (ToolStripItem it in menu.Items)
            {
                if (it is ToolStripSeparator) { it.AutoSize = false; it.Height = P(11, s); continue; }
                it.Padding = new Padding(P(2, s), P(5, s), P(10, s), P(5, s));
                if (it.Image != null) it.Image.Dispose();
                // Hueco para el icono (que se dibuja a mano, nítido, en OnRenderItemImage).
                it.Image = new Bitmap(P(18, s), P(18, s), PixelFormat.Format32bppPArgb);
                if ((it.Tag as string) == "app") it.Font = appFont;
            }
            if (oldFont != null) oldFont.Dispose();
            if (oldBold != null) oldBold.Dispose();
        }
        static Font menuFont, appFont;

        static int P(float v, float s) { return (int)Math.Round(v * s); }

        // Esquinas redondeadas, modo oscuro y borde fino del sistema (Windows 11).
        public static void Round(IntPtr h)
        {
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(h, 20, ref on, 4);
                int round = 2;
                Native.DwmSetWindowAttribute(h, 33, ref round, 4);
                int border = 70 | (70 << 8) | (76 << 16);
                Native.DwmSetWindowAttribute(h, 34, ref border, 4);
            }
            catch { }
        }
    }

    public class MacMenuRenderer : ToolStripRenderer
    {
        public float S = 1f;
        Image logo;

        int P(float v) { return (int)Math.Round(v * S); }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Mac.Menu);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (Environment.OSVersion.Version.Build >= 22000) return; // el borde lo pone Windows
            using (Pen p = new Pen(Mac.Separator)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(P(6), 0, e.Item.Width - P(12), e.Item.Height);
            using (GraphicsPath p = Theme.Round(r, P(6)))
            using (SolidBrush b = new SolidBrush(Mac.Blue)) g.FillPath(b, p);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool sel = e.Item.Selected && e.Item.Enabled;
            bool shortcut = e.Text != e.Item.Text;
            Color c = !e.Item.Enabled ? Mac.Text3 : sel ? (shortcut ? Color.FromArgb(215, 230, 245) : Color.White) : shortcut ? Mac.Text3 : Mac.Text;
            Rectangle r = e.TextRectangle;
            if (!shortcut) r.Offset(P(2), 0);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, r, c, e.TextFormat | TextFormatFlags.NoPrefix);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            string icon = e.Item.Tag as string;
            if (icon == null) return;
            Graphics g = e.Graphics;
            Rectangle r = e.ImageRectangle;
            r.Offset(P(6), 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (icon == "app")
            {
                if (logo == null) logo = ShotStack.LoadResourceImage("logo.png");
                Rectangle lr = Rectangle.Inflate(r, P(2), P(2));
                if (logo != null) g.DrawImage(logo, lr);
                return;
            }
            bool sel = e.Item.Selected && e.Item.Enabled;
            Color c = sel ? Color.White : icon == "stop" ? Mac.Red : Mac.Text2;
            Icons.Draw(g, icon, r, c);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Mac.Separator)) e.Graphics.DrawLine(p, P(14), y, e.Item.Width - P(14), y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Mac.Text2;
            base.OnRenderArrow(e);
        }
    }
}
