// Stackshot - macOS-style context menus for WinForms windows (the pinned capture), drawn like the tray panel: the same
// rows, line icons, highlight pill and keycaps, light or dark with the app. The tray icon opens TrayPanel instead.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;
using W = System.Windows;

namespace Stackshot
{
    // Each item's Tag holds its icon name (Glyph); "app" shows the logo and the text in semibold. An item's
    // ShortcutKeyDisplayString ("Ctrl+C") is shown as keycaps. The menu rescales to the DPI of the monitor it opens on
    // and follows the current theme every time it opens; on Windows 11 the system draws corners, border and shadow.
    public static class TrayMenu
    {
        // Logical sizes, as in the tray panel.
        internal const float RowH = 30, SeparatorH = 11, Inset = 6, MinWidth = 184;

        public static ContextMenuStrip Create()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            MacMenuRenderer r = new MacMenuRenderer();
            menu.Renderer = r;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.DropShadowEnabled = Environment.OSVersion.Version.Build < 22000; // on Windows 11 DWM draws the shadow
            // Corners, border and mode are set before it shows, so it never flashes the default frame first.
            menu.Opening += delegate { Restyle(menu, r); Round(menu.Handle); };
            menu.Disposed += delegate { r.Drop(); };
            return menu;
        }

        public static ToolStripMenuItem Item(string text, string icon, EventHandler click)
        {
            return Item(text, icon, null, click);
        }

        // keys: the shortcut shown on the right as keycaps, e.g. "Ctrl+C" (shown only; the window handles the keys).
        public static ToolStripMenuItem Item(string text, string icon, string keys, EventHandler click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text, null, click);
            it.Tag = icon;
            if (!string.IsNullOrEmpty(keys)) it.ShortcutKeyDisplayString = keys;
            return it;
        }

        public static ToolStripSeparator Separator()
        {
            return new ToolStripSeparator();
        }

        internal static Color Back { get { return Ds.Gdi(TrayPanel.Back(Ds.Dark)); } }

        // Sizes the rows for this scale (they are painted by the renderer, so WinForms only needs their sizes) and drops
        // the rows painted for another scale, theme or set of items.
        static void Restyle(ContextMenuStrip menu, MacMenuRenderer r)
        {
            // Where it opens: at the pointer, or over its window when opened from the keyboard (menu key, Shift+F10)
            // while the pointer rests on another monitor.
            Point at = Control.MousePosition;
            Control src = menu.SourceControl;
            if (src != null && src.IsHandleCreated && !src.RectangleToScreen(src.ClientRectangle).Contains(at))
                at = src.PointToScreen(new Point(src.ClientSize.Width / 2, src.ClientSize.Height / 2));
            Restyle(menu, r, ShotStack.ScaleFor(Screen.FromPoint(at)));
        }

        internal static void Restyle(ContextMenuStrip menu, MacMenuRenderer r, float s)
        {
            menu.BackColor = Back;
            StringBuilder k = new StringBuilder();
            k.Append(s).Append('|').Append(Ds.Dark);
            foreach (ToolStripItem it in menu.Items) k.Append('|').Append(it.Text).Append(it.Tag as string).Append(ShortcutOf(it)).Append(it.Available);
            string key = k.ToString();
            if (key == r.Key) return;
            r.Drop();
            r.Key = key;
            r.S = s;
            int width = Width(menu, s);
            menu.SuspendLayout();
            menu.Font = Fonts.Get(Mac.TextFont, 13 * s); // shared and cached: never disposed here
            menu.MinimumSize = new Size(width, 0);
            ToolStripItem first = null, last = null;
            foreach (ToolStripItem it in menu.Items)
            {
                it.AutoSize = false;
                it.Margin = Padding.Empty;
                it.Padding = Padding.Empty;
                it.Size = new Size(width, P(it is ToolStripSeparator ? SeparatorH : RowH, s));
                if (!it.Available) continue;
                if (first == null) first = it;
                last = it;
            }
            // The drop-down keeps its own padding (2 px above and below); the panel's inset comes from the end rows.
            int extra = Math.Max(0, P(Inset, s) - menu.Padding.Top);
            if (first != null) first.Margin = new Padding(0, extra, 0, first == last ? extra : 0);
            if (last != null && last != first) last.Margin = new Padding(0, 0, 0, extra);
            menu.ResumeLayout(true);
        }

        internal static string ShortcutOf(ToolStripItem it)
        {
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            return mi != null && mi.ShowShortcutKeys ? mi.ShortcutKeyDisplayString : null;
        }

        // Wide enough for the longest row: icon, text and keycaps with the panel's spacing.
        static int Width(ContextMenuStrip menu, float s)
        {
            double w = MinWidth * s;
            foreach (ToolStripItem it in menu.Items)
            {
                if (it is ToolStripSeparator || !it.Available) continue;
                bool app = (it.Tag as string) == "app";
                double text = Ink.Px(it.Text ?? "", app ? Ds.Semibold : Ds.Regular, 13 * s, M.Colors.Black).WidthIncludingTrailingWhitespace;
                string[] caps = TrayPanel.KeycapsFor(ShortcutOf(it));
                double row = (Inset + 10 + 16 + 10) * s + text + (caps != null ? 28 * s + TrayPanel.KeycapsWidth(caps, s) : 0) + (10 + Inset) * s;
                w = Math.Max(w, row);
            }
            return (int)Math.Ceiling(w);
        }

        static int P(float v, float s) { return (int)Math.Round(v * s); }

        // Rounded corners, the theme's mode and a thin border the color of the panel's hairline (Windows 11).
        public static void Round(IntPtr h)
        {
            try
            {
                int dark = Ds.Dark ? 1 : 0;
                Native.DwmSetWindowAttribute(h, 20, ref dark, 4);
                int round = 2;
                Native.DwmSetWindowAttribute(h, 33, ref round, 4);
                int border = Ds.Dark ? (68 | (68 << 8) | (71 << 16)) : (216 | (216 << 8) | (217 << 16));
                Native.DwmSetWindowAttribute(h, 34, ref border, 4);
            }
            catch { }
        }
    }

    // Paints whole rows with the tray panel's WPF text and glyphs, once per row and state, and keeps them: hovering only
    // swaps two bitmaps. WinForms' own text, image and check painting is skipped.
    public class MacMenuRenderer : ToolStripRenderer
    {
        public float S = 1f;
        internal string Key;    // what the cached rows were painted for (see TrayMenu.Restyle)
        readonly Dictionary<string, Bitmap> rows = new Dictionary<string, Bitmap>();

        // The menu is gone (or restyled): what was painted for it goes too.
        public void Drop()
        {
            foreach (Bitmap b in rows.Values) b.Dispose();
            rows.Clear();
            Key = null;
        }

        int P(float v) { return (int)Math.Round(v * S); }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(TrayMenu.Back);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (Environment.OSVersion.Version.Build >= 22000) return; // Windows draws the border
            using (Pen p = new Pen(Ds.Dark ? Color.FromArgb(68, 68, 71) : Color.FromArgb(216, 216, 217))) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem it = e.Item;
            Bitmap b = Row(it, it.Selected && it.Enabled, e.ToolStrip.Width, it.Height);
            if (b == null) return;
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(b, new Rectangle(-it.Bounds.X, 0, b.Width, b.Height));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int inset = P(TrayMenu.Inset + 10), x = inset - e.Item.Bounds.X, w = e.ToolStrip.Width - 2 * inset;
            using (SolidBrush b = new SolidBrush(Ds.Gdi(Ds.Brushes.Separator))) e.Graphics.FillRectangle(b, x, e.Item.Height / 2, Math.Max(0, w), 1);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && e.Item.Selected ? Color.White : Ds.Gdi(Ds.Brushes.Label2);
            base.OnRenderArrow(e);
        }

        // The row across the whole menu width, in that state.
        Bitmap Row(ToolStripItem it, bool sel, int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            string icon = it.Tag as string, keys = TrayMenu.ShortcutOf(it), text = it.Text ?? "";
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            bool check = mi != null && mi.Checked, enabled = it.Enabled;
            string key = text + "\n" + icon + "\n" + keys + "\n" + sel + enabled + check + w + "x" + h;
            Bitmap b;
            if (rows.TryGetValue(key, out b)) return b;
            if (rows.Count > 48) { foreach (Bitmap old in rows.Values) old.Dispose(); rows.Clear(); }
            double s = S;
            MI.RenderTargetBitmap img = Ink.Render(w, h, delegate(M.DrawingContext dc) { Paint(dc, text, icon, keys, sel, enabled, check, w, h, s); }, null);
            b = Ink.ToGdi(img);
            rows[key] = b;
            return b;
        }

        static void Paint(M.DrawingContext dc, string text, string icon, string keys, bool sel, bool enabled, bool check, int w, int h, double s)
        {
            Palette pal = Ds.Brushes;
            double inset = Math.Round(TrayMenu.Inset * s), cy = h / 2.0;
            W.Rect r = new W.Rect(inset, 0, w - 2 * inset, h);
            if (sel)
            {
                double rr = Math.Round(6 * s);
                dc.DrawRoundedRectangle(Ds.Brush(pal.Accent), null, r, rr, rr);
            }
            M.Color ink = sel ? M.Colors.White : enabled ? pal.Label : pal.Label3;
            double x = r.X + Math.Round(10 * s), gs = Math.Round(16 * s);
            bool app = icon == "app";
            if (app)
            {
                int lp = (int)Math.Round(18 * s);
                MI.BitmapSource logo = TrayPanel.Logo(lp);
                if (logo != null) dc.DrawImage(logo, new W.Rect(x - Math.Round((lp - gs) / 2), Math.Round(cy - lp / 2.0), lp, lp));
            }
            else if (check || icon != null)
            {
                M.Color c = sel ? M.Colors.White : !enabled ? pal.Label3 : check ? pal.Accent : icon == "stop" ? pal.Red : Ds.WithAlpha(pal.Label, pal.Dark ? 0.72 : 0.68);
                string g = check ? "check" : icon == "stop" ? "stop!" : icon;
                Glyph.Draw(dc, g, x, Math.Round(cy - gs / 2), gs, c, 1.45 * s);
            }
            M.FormattedText t = Ink.Px(text, app ? Ds.Semibold : Ds.Regular, 13 * s, ink);
            dc.DrawText(t, new W.Point(Math.Round(x + gs + Math.Round(10 * s)), Math.Round(cy - t.Height / 2)));
            string[] caps = TrayPanel.KeycapsFor(keys);
            if (caps != null) TrayPanel.DrawKeycaps(dc, caps, r.Right - Math.Round(7 * s), cy, sel, s);
        }
    }
}
