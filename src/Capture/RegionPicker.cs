// Stackshot - Region picker: freezes the screen and lets the user drag an area or click a window.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    // One window covers every monitor with the frozen, dimmed screenshot. Hovering highlights the window underneath
    // (click = that window, or the whole monitor over the desktop); dragging selects an area.
    // Performance: the dimmed and bright images (with hints) are prepared once. Each mouse move only recomposes the
    // small rectangles that change (crosshair lines, magnifier, selection edges) with BitBlt; GDI+ only draws the
    // overlay on top.
    public class RegionPicker : Form
    {
        public enum Mode { Image, Video, Gif, Scroll }

        readonly Bitmap shot;          // GDI+ copy, for the magnifier and the final crop
        readonly Dib bright, dimmed;   // plain and dimmed screen, both with hints
        readonly Rectangle vs;
        readonly List<Grabber.Win> wins;
        readonly List<Rectangle> dirty = new List<Rectangle>();
        Point cur = new Point(-10000, -10000), start;
        bool down, dragging;
        Rectangle sel, hover;
        IntPtr hoverHandle;
        Rectangle lastDecor;
        Rectangle result = Rectangle.Empty;
        IntPtr resultWindow = IntPtr.Zero;
        Rectangle curMon = Rectangle.Empty;   // monitor under the cursor, in client coordinates
        float curScale = 1f;
        Font labelFont;
        float labelFontScale;

        RegionPicker(Dib grab, Rectangle vs, List<Grabber.Win> wins, Mode mode)
        {
            this.vs = vs;
            this.wins = wins;
            bright = grab;
            shot = grab.ToBitmap();
            dimmed = new Dib(grab.Width, grab.Height);
            Native.BitBlt(dimmed.Dc, 0, 0, grab.Width, grab.Height, grab.Dc, 0, 0, Native.SRCCOPY);
            dimmed.Darken(105);
            PaintHints(bright, mode);
            PaintHints(dimmed, mode);

            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Bounds = vs;
            Cursor = Cursors.Cross;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8; // WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        // Returns the chosen rectangle in screen coordinates (empty if cancelled), the frozen virtual screen and the
        // clicked window, if any.
        public static Rectangle Pick(Mode mode, out Bitmap frozen, out Rectangle virtualScreen, out IntPtr window)
        {
            virtualScreen = SystemInformation.VirtualScreen;
            Dib grab = Dib.FromScreen(virtualScreen);
            List<Grabber.Win> wins = Grabber.Windows();
            using (RegionPicker p = new RegionPicker(grab, virtualScreen, wins, mode))
            {
                p.ShowDialog();
                frozen = p.shot;
                window = p.resultWindow;
                return p.result;
            }
        }

        float S { get { return curScale; } }
        int Pz(float v) { return (int)Math.Round(v * S); }
        Point ToScreen(Point p) { return new Point(p.X + vs.X, p.Y + vs.Y); }

        // Only re-query the monitor when the cursor leaves it; asking Windows on every move costs more than drawing.
        void TrackMonitor()
        {
            if (!curMon.IsEmpty && curMon.Contains(cur)) return;
            Screen sc = Screen.FromPoint(ToScreen(cur));
            Rectangle b = sc.Bounds;
            b.Offset(-vs.X, -vs.Y);
            curMon = b;
            curScale = ShotStack.ScaleFor(sc);
            if (labelFont == null || labelFontScale != curScale)
            {
                if (labelFont != null) labelFont.Dispose();
                labelFont = new Font("Segoe UI Semibold", 12 * curScale, GraphicsUnit.Pixel);
                labelFontScale = curScale;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.ForceForeground(Handle);
            Activate();
            cur = PointToClient(Control.MousePosition);
            TrackMonitor();
            UpdateHover();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Dirty rectangles are tracked here as well, so only those parts get recomposed.
        void Dirty(Rectangle r)
        {
            r.Intersect(ClientRectangle);
            if (r.Width <= 0 || r.Height <= 0) return;
            dirty.Add(r);
            Invalidate(r);
        }

        [DllImport("user32.dll")] static extern int GetUpdateRgn(IntPtr hwnd, IntPtr rgn, bool erase);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dst, IntPtr a, IntPtr b, int mode);
        bool uncovered; // Windows asked to repaint something outside our own dirty list (another window passed over)

        protected override void WndProc(ref Message msg)
        {
            if (msg.Msg == 0x000F && dirty.Count > 0) // WM_PAINT: compare the real update region with the dirty list
            {
                IntPtr update = CreateRectRgn(0, 0, 0, 0), ours = CreateRectRgn(0, 0, 0, 0), part = CreateRectRgn(0, 0, 0, 0);
                try
                {
                    GetUpdateRgn(Handle, update, false);
                    foreach (Rectangle r in dirty)
                    {
                        IntPtr one = CreateRectRgn(r.Left, r.Top, r.Right, r.Bottom);
                        CombineRgn(ours, ours, one, 2); // RGN_OR
                        Native.DeleteObject(one);
                    }
                    uncovered = CombineRgn(part, update, ours, 4) != 1; // RGN_DIFF; 1 = NULLREGION
                }
                finally { Native.DeleteObject(update); Native.DeleteObject(ours); Native.DeleteObject(part); }
            }
            base.WndProc(ref msg);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            List<Rectangle> parts = new List<Rectangle>();
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, ClientRectangle);
            if (dirty.Count > 0)
            {
                Rectangle box = dirty[0];
                foreach (Rectangle r in dirty) box = Rectangle.Union(box, r);
                if (!uncovered && box.Contains(clip) && dirty.Count <= 16) parts.AddRange(dirty);
                else parts.Add(clip); // Windows asked for more, or too many parts: repaint the whole clip at once
            }
            else parts.Add(clip);
            uncovered = false;
            dirty.Clear();

            IntPtr hdc = e.Graphics.GetHdc();
            try
            {
                foreach (Rectangle r in parts)
                {
                    if (r.Width <= 0 || r.Height <= 0) continue;
                    Dib buf = Scratch(r.Size);
                    Compose(buf, r);
                    Native.BitBlt(hdc, r.X, r.Y, r.Width, r.Height, buf.Dc, 0, 0, Native.SRCCOPY);
                }
            }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }

        // One part: dimmed background, highlighted area in bright, then border, crosshair and magnifier.
        void Compose(Dib buf, Rectangle r)
        {
            Native.BitBlt(buf.Dc, 0, 0, r.Width, r.Height, dimmed.Dc, r.X, r.Y, Native.SRCCOPY);
            Rectangle lit = Rectangle.Intersect(dragging ? sel : hover, r);
            if (lit.Width > 0 && lit.Height > 0)
                Native.BitBlt(buf.Dc, lit.X - r.X, lit.Y - r.Y, lit.Width, lit.Height, bright.Dc, lit.X, lit.Y, Native.SRCCOPY);
            using (Graphics g = buf.Graphics())
            {
                g.TranslateTransform(-r.X, -r.Y);
                g.SetClip(r);
                PaintOverlay(g, r);
            }
        }

        void PaintOverlay(Graphics g, Rectangle clip)
        {
            if (dragging)
            {
                Rectangle r = sel;
                if (Touches(r, clip, 3))
                {
                    using (Pen outer = new Pen(Color.FromArgb(90, 0, 0, 0), 3f)) g.DrawRectangle(outer, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
                    using (Pen pen = new Pen(Color.White, 1.5f)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }
            else if (!hover.IsEmpty && Touches(hover, clip, Pz(4)))
            {
                Rectangle r = hover;
                float w = Math.Max(2f, 3f * S);
                using (Pen pen = new Pen(Theme.Accent, w)) g.DrawRectangle(pen, r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            }
            if (!dragging) PaintCross(g, clip);
            Rectangle label;
            Rectangle mag = MagnifierRect(out label);
            Rectangle decor = Rectangle.Union(mag, label);
            decor.Inflate(Pz(6), Pz(8));
            if (decor.IntersectsWith(clip))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PaintMagnifier(g, mag, label);
            }
        }

        // Does r's border (with margin m) cross the clip?
        static bool Touches(Rectangle r, Rectangle clip, int m)
        {
            Rectangle outer = r;
            outer.Inflate(m, m);
            if (!outer.IntersectsWith(clip)) return false;
            Rectangle inner = r;
            inner.Inflate(-m, -m);
            return inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(clip);
        }

        // Hint pill at the top of each monitor, painted once onto both background images.
        void PaintHints(Dib target, Mode mode)
        {
            string text = mode == Mode.Image ? "Arrastra para capturar un \u00E1rea  \u00B7  Clic: ventana o pantalla entera  \u00B7  Esc: cancelar"
                        : mode == Mode.Video ? "Grabar v\u00EDdeo  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : mode == Mode.Gif ? "Grabar GIF  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : "Captura con desplazamiento  \u00B7  Arrastra el \u00E1rea que se va a desplazar  \u00B7  Esc: cancelar";
            using (Graphics g = target.Graphics())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (Screen sc in Screen.AllScreens)
                {
                    Rectangle b = sc.Bounds;
                    b.Offset(-vs.X, -vs.Y);
                    float s = ShotStack.ScaleFor(sc);
                    using (Font f = new Font("Segoe UI Semibold", 13 * s, GraphicsUnit.Pixel))
                    {
                        Size ts = TextRenderer.MeasureText(text, f);
                        Rectangle pill = new Rectangle(b.X + (b.Width - ts.Width) / 2 - (int)(16 * s), b.Y + (int)(18 * s), ts.Width + (int)(32 * s), ts.Height + (int)(14 * s));
                        using (GraphicsPath p = Theme.Round(pill, pill.Height / 2f))
                        using (SolidBrush bg = new SolidBrush(Color.FromArgb(225, Theme.Dark)))
                        using (Pen border = new Pen(Color.FromArgb(140, Theme.Border)))
                        {
                            g.FillPath(bg, p);
                            g.DrawPath(border, p);
                        }
                        TextRenderer.DrawText(g, text, f, pill, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                }
            }
        }

        void PaintCross(Graphics g, Rectangle clip)
        {
            Rectangle m = curMon;
            g.SmoothingMode = SmoothingMode.None;
            using (Pen pen = new Pen(Color.FromArgb(110, 255, 255, 255)))
            {
                if (cur.Y >= clip.Top - 1 && cur.Y <= clip.Bottom) g.DrawLine(pen, m.Left, cur.Y, m.Right, cur.Y);
                if (cur.X >= clip.Left - 1 && cur.X <= clip.Right) g.DrawLine(pen, cur.X, m.Top, cur.X, m.Bottom);
            }
        }

        // Magnifier: 15x15 pixels around the cursor, plus the size label below.
        Rectangle MagnifierRect(out Rectangle label)
        {
            int size = Pz(120), off = Pz(22), lh = Pz(26);
            Rectangle m = curMon;
            int x = cur.X + off, y = cur.Y + off;
            if (x + size > m.Right) x = cur.X - off - size;
            if (y + size + lh + Pz(6) > m.Bottom) y = cur.Y - off - size - lh - Pz(6);
            label = new Rectangle(x, y + size + Pz(6), size, lh);
            return new Rectangle(x, y, size, size);
        }

        void PaintMagnifier(Graphics g, Rectangle r, Rectangle label)
        {
            const int n = 15;
            Rectangle src = new Rectangle(cur.X - n / 2, cur.Y - n / 2, n, n);
            using (GraphicsPath p = Theme.Round(r, Pz(14)))
            {
                RectangleF sh = r;
                sh.Offset(0, Pz(3));
                using (GraphicsPath ps = Theme.Round(sh, Pz(14)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(b, ps);
                using (SolidBrush b = new SolidBrush(Color.Black)) g.FillPath(b, p);
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                InterpolationMode im = g.InterpolationMode;
                PixelOffsetMode pm = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(shot, r, src, GraphicsUnit.Pixel);
                g.InterpolationMode = im;
                g.PixelOffsetMode = pm;
                float cell = r.Width / (float)n;
                RectangleF c = new RectangleF(r.X + cell * (n / 2), r.Y + cell * (n / 2), cell, cell);
                using (Pen pen = new Pen(Color.FromArgb(200, 0, 0, 0), 2f)) g.DrawRectangle(pen, c.X - 1, c.Y - 1, c.Width + 2, c.Height + 2);
                using (Pen pen = new Pen(Color.White, 1f)) g.DrawRectangle(pen, c.X, c.Y, c.Width, c.Height);
                g.Clip = old;
                old.Dispose();
                using (Pen pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1.5f, 2f * S))) g.DrawPath(pen, p);
            }
            string text;
            if (dragging) text = sel.Width + " \u00D7 " + sel.Height;
            else if (!hover.IsEmpty) text = hover.Width + " \u00D7 " + hover.Height;
            else text = "";
            if (text.Length == 0) return;
            using (GraphicsPath p = Theme.Round(label, label.Height / 2f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, Theme.Dark)))
            {
                g.FillPath(b, p);
                TextRenderer.DrawText(g, text, labelFont, label, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        // Bounds of everything that follows the cursor (crosshair, magnifier, label).
        Rectangle DecorBounds()
        {
            Rectangle label;
            Rectangle r = MagnifierRect(out label);
            r = Rectangle.Union(r, label);
            r.Inflate(Pz(6), Pz(8));
            return r;
        }

        void InvalidateDecor()
        {
            Rectangle now = DecorBounds();
            Dirty(lastDecor);
            Dirty(now);
            Rectangle m = curMon;
            // The crosshair lines are thin: invalidate old and new positions separately.
            if (crossY != cur.Y || crossMon != m) { Dirty(new Rectangle(crossMon.Left, crossY - 1, crossMon.Width, 3)); Dirty(new Rectangle(m.Left, cur.Y - 1, m.Width, 3)); }
            if (crossX != cur.X || crossMon != m) { Dirty(new Rectangle(crossX - 1, crossMon.Top, 3, crossMon.Height)); Dirty(new Rectangle(cur.X - 1, m.Top, 3, m.Height)); }
            crossX = cur.X;
            crossY = cur.Y;
            crossMon = m;
            lastDecor = now;
        }

        int crossX = -10000, crossY = -10000;
        Rectangle crossMon;

        void InvalidateFrame(Rectangle r)
        {
            if (r.IsEmpty) return;
            r.Inflate(Pz(6), Pz(6));
            Dirty(r);
        }

        void UpdateHover()
        {
            Point sp = ToScreen(cur);
            Rectangle r = Rectangle.Empty;
            IntPtr h = IntPtr.Zero;
            foreach (Grabber.Win w in wins)
            {
                if (w.Bounds.Contains(sp)) { r = w.Bounds; h = w.Handle; break; }
            }
            if (r.IsEmpty) { r = curMon; r.Offset(vs.X, vs.Y); } // over the desktop: whole monitor
            r.Intersect(vs);
            r.Offset(-vs.X, -vs.Y);
            if (r == hover) return;
            InvalidateFrame(hover);
            hover = r;
            hoverHandle = h;
            InvalidateFrame(hover);
        }

        static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);
        }

        // Only the bands between the old and new selection change: invalidate each band, not the union.
        void InvalidateSelection(Rectangle old, Rectangle now)
        {
            int m = Pz(4);
            if (old.IsEmpty) { Rectangle r = now; r.Inflate(m, m); Dirty(r); return; }
            Rectangle u = Rectangle.Union(old, now);
            u.Inflate(m, m);
            Rectangle keep = Rectangle.Intersect(old, now);
            keep.Inflate(-m, -m);
            if (keep.Width <= 0 || keep.Height <= 0) { Dirty(u); return; }
            Dirty(Rectangle.FromLTRB(u.Left, u.Top, u.Right, keep.Top));
            Dirty(Rectangle.FromLTRB(u.Left, keep.Bottom, u.Right, u.Bottom));
            Dirty(Rectangle.FromLTRB(u.Left, keep.Top, keep.Left, keep.Bottom));
            Dirty(Rectangle.FromLTRB(keep.Right, keep.Top, u.Right, keep.Bottom));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Location == cur) return;
            cur = e.Location;
            TrackMonitor();
            if (down && !dragging && (Math.Abs(cur.X - start.X) > Pz(4) || Math.Abs(cur.Y - start.Y) > Pz(4)))
            {
                dragging = true;
                InvalidateFrame(hover); // the hovered window turns off once dragging starts
            }
            if (dragging)
            {
                Rectangle old = sel;
                sel = Normalize(start, cur);
                InvalidateSelection(old, sel);
            }
            else if (!down) UpdateHover();
            InvalidateDecor();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                if (down || dragging) Reset();
                else Cancel();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            down = true;
            start = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !down) return;
            if (dragging)
            {
                if (sel.Width >= 4 && sel.Height >= 4) Accept(sel, IntPtr.Zero);
                else Reset();
            }
            else Accept(hover, hoverHandle);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int step = e.Shift ? 10 : 1;
            switch (e.KeyCode)
            {
                case Keys.Escape: Cancel(); break;
                case Keys.Enter: Accept(dragging ? sel : hover, dragging ? IntPtr.Zero : hoverHandle); break;
                // Arrow keys nudge the cursor for pixel-precise selection.
                case Keys.Left: Cursor.Position = new Point(Cursor.Position.X - step, Cursor.Position.Y); break;
                case Keys.Right: Cursor.Position = new Point(Cursor.Position.X + step, Cursor.Position.Y); break;
                case Keys.Up: Cursor.Position = new Point(Cursor.Position.X, Cursor.Position.Y - step); break;
                case Keys.Down: Cursor.Position = new Point(Cursor.Position.X, Cursor.Position.Y + step); break;
            }
        }

        void Reset()
        {
            Rectangle old = sel;
            down = false;
            dragging = false;
            sel = Rectangle.Empty;
            InvalidateFrame(old);
            UpdateHover();
            InvalidateFrame(hover);
            InvalidateDecor();
        }

        void Accept(Rectangle r, IntPtr window)
        {
            if (r.Width < 1 || r.Height < 1) { Cancel(); return; }
            r.Offset(vs.X, vs.Y);
            result = r;
            resultWindow = window;
            DialogResult = DialogResult.OK;
            Close();
        }

        void Cancel()
        {
            result = Rectangle.Empty;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            bright.Dispose();
            dimmed.Dispose();
            if (scratch != null) scratch.Dispose();
            if (labelFont != null) labelFont.Dispose();
        }

        // One composition buffer reused across paints (grown when needed): creating a DIB section for every dirty
        // rectangle on every mouse move was a measurable part of the cost.
        Dib scratch;

        Dib Scratch(Size need)
        {
            if (scratch != null && scratch.Width >= need.Width && scratch.Height >= need.Height) return scratch;
            int w = need.Width, h = need.Height;
            if (scratch != null)
            {
                w = Math.Max(w, scratch.Width);
                h = Math.Max(h, scratch.Height);
                scratch.Dispose();
            }
            scratch = new Dib(w, h);
            return scratch;
        }
    }
}
