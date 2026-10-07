// Stackshot - Elegir qué capturar: la pantalla se congela y se arrastra un área o se pincha una ventana.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Stackshot
{
    // Una sola ventana cubre todas las pantallas con la imagen congelada (oscurecida). Al pasar el ratón se ilumina
    // la ventana que hay debajo (clic = esa ventana; sobre el escritorio, la pantalla entera); al arrastrar se elige
    // un área. Lupa, cruceta y medidas siguen al ratón. Solo se repinta lo que cambia, así que va fluido aunque
    // haya varias pantallas grandes.
    public class RegionPicker : Form
    {
        public enum Mode { Image, Video, Gif }

        readonly Bitmap shot;
        readonly Rectangle vs;
        readonly List<Grabber.Win> wins;
        readonly Mode mode;
        IntPtr memDC, hbm, oldBmp;
        Point cur = new Point(-10000, -10000), start;
        bool down, dragging;
        Rectangle sel, hover;
        IntPtr hoverHandle;
        Rectangle lastDecor;
        Rectangle result = Rectangle.Empty;
        IntPtr resultWindow = IntPtr.Zero;

        RegionPicker(Bitmap shot, Rectangle vs, List<Grabber.Win> wins, Mode mode)
        {
            this.shot = shot;
            this.vs = vs;
            this.wins = wins;
            this.mode = mode;
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Bounds = vs;
            Cursor = Cursors.Cross;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            hbm = shot.GetHbitmap();
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screen);
            Native.ReleaseDC(IntPtr.Zero, screen);
            oldBmp = Native.SelectObject(memDC, hbm);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8; // TOOLWINDOW (fuera de Alt+Tab) | TOPMOST
                return cp;
            }
        }

        // Congela la pantalla y deja elegir. Devuelve el rectángulo elegido en coordenadas de pantalla (vacío si se
        // cancela), la imagen congelada de toda la pantalla virtual y la ventana pinchada (si se pinchó una).
        public static Rectangle Pick(Mode mode, out Bitmap frozen, out Rectangle virtualScreen, out IntPtr window)
        {
            virtualScreen = SystemInformation.VirtualScreen;
            List<Grabber.Win> wins = Grabber.Windows();
            frozen = Grabber.Grab(virtualScreen);
            using (RegionPicker p = new RegionPicker(frozen, virtualScreen, wins, mode))
            {
                p.ShowDialog();
                window = p.resultWindow;
                return p.result;
            }
        }

        float S { get { return ShotStack.ScaleFor(Screen.FromPoint(ToScreen(cur))); } }
        int Pz(float v) { return (int)Math.Round(v * S); }
        Point ToScreen(Point p) { return new Point(p.X + vs.X, p.Y + vs.Y); }

        Rectangle CurrentMonitor()
        {
            Rectangle b = Screen.FromPoint(ToScreen(cur)).Bounds;
            b.Offset(-vs.X, -vs.Y);
            return b;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.ForceForeground(Handle);
            Activate();
            cur = PointToClient(Control.MousePosition);
            UpdateHover();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Se pinta en un lienzo del tamaño de lo que cambia y se vuelca de una vez (sin parpadeos).
        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, ClientRectangle);
            if (clip.Width <= 0 || clip.Height <= 0) return;
            using (Bitmap buf = new Bitmap(clip.Width, clip.Height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(buf))
                {
                    IntPtr hdc = g.GetHdc();
                    Native.BitBlt(hdc, 0, 0, clip.Width, clip.Height, memDC, clip.X, clip.Y, Native.SRCCOPY);
                    g.ReleaseHdc(hdc);
                    g.TranslateTransform(-clip.X, -clip.Y);
                    PaintOverlay(g, clip);
                }
                e.Graphics.DrawImageUnscaled(buf, clip.X, clip.Y);
            }
        }

        void PaintOverlay(Graphics g, Rectangle clip)
        {
            Rectangle bright = dragging ? sel : hover;
            using (Region dim = new Region(clip))
            {
                if (!bright.IsEmpty) dim.Exclude(bright);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(105, 0, 0, 0))) g.FillRegion(b, dim);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (dragging)
            {
                Rectangle r = sel;
                using (Pen outer = new Pen(Color.FromArgb(90, 0, 0, 0), 3f)) g.DrawRectangle(outer, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
                using (Pen pen = new Pen(Color.White, 1.5f)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            else if (!hover.IsEmpty)
            {
                Rectangle r = hover;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, Theme.Accent))) g.FillRectangle(b, r);
                float w = Math.Max(2f, 3f * S);
                using (Pen pen = new Pen(Theme.Accent, w)) g.DrawRectangle(pen, r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            }
            PaintHints(g, clip);
            if (!dragging) PaintCross(g);
            PaintMagnifier(g);
        }

        // Ayuda arriba de cada pantalla.
        void PaintHints(Graphics g, Rectangle clip)
        {
            string text = mode == Mode.Image ? "Arrastra para capturar un \u00E1rea  \u00B7  Clic: ventana o pantalla entera  \u00B7  Esc: cancelar"
                        : mode == Mode.Video ? "Grabar v\u00EDdeo  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar"
                        : "Grabar GIF  \u00B7  Arrastra un \u00E1rea o haz clic en una ventana  \u00B7  Esc: cancelar";
            foreach (Screen sc in Screen.AllScreens)
            {
                Rectangle b = sc.Bounds;
                b.Offset(-vs.X, -vs.Y);
                float s = ShotStack.ScaleFor(sc);
                using (Font f = new Font("Segoe UI Semibold", 13 * s, GraphicsUnit.Pixel))
                {
                    Size ts = TextRenderer.MeasureText(text, f);
                    Rectangle pill = new Rectangle(b.X + (b.Width - ts.Width) / 2 - (int)(16 * s), b.Y + (int)(18 * s), ts.Width + (int)(32 * s), ts.Height + (int)(14 * s));
                    if (!pill.IntersectsWith(clip)) continue;
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

        void PaintCross(Graphics g)
        {
            Rectangle m = CurrentMonitor();
            g.SmoothingMode = SmoothingMode.None;
            using (Pen pen = new Pen(Color.FromArgb(110, 255, 255, 255)))
            {
                g.DrawLine(pen, m.Left, cur.Y, m.Right, cur.Y);
                g.DrawLine(pen, cur.X, m.Top, cur.X, m.Bottom);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        // Lupa: 15 × 15 píxeles alrededor del ratón, ampliados, con las medidas debajo.
        Rectangle MagnifierRect(out Rectangle label)
        {
            int size = Pz(120), off = Pz(22), lh = Pz(26);
            Rectangle m = CurrentMonitor();
            int x = cur.X + off, y = cur.Y + off;
            if (x + size > m.Right) x = cur.X - off - size;
            if (y + size + lh + Pz(6) > m.Bottom) y = cur.Y - off - size - lh - Pz(6);
            label = new Rectangle(x, y + size + Pz(6), size, lh);
            return new Rectangle(x, y, size, size);
        }

        void PaintMagnifier(Graphics g)
        {
            Rectangle label;
            Rectangle r = MagnifierRect(out label);
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
                using (Pen pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1.5f, 2f * S))) g.DrawPath(pen, p);
            }
            string text;
            if (dragging) text = sel.Width + " \u00D7 " + sel.Height;
            else if (!hover.IsEmpty) text = hover.Width + " \u00D7 " + hover.Height;
            else text = "";
            if (text.Length == 0) return;
            using (Font f = new Font("Segoe UI Semibold", 12 * S, GraphicsUnit.Pixel))
            using (GraphicsPath p = Theme.Round(label, label.Height / 2f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, Theme.Dark)))
            {
                g.FillPath(b, p);
                TextRenderer.DrawText(g, text, f, label, Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        // Todo lo que sigue al ratón (cruceta, lupa, medidas): se borra donde estaba y se pinta donde va.
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
            Invalidate(lastDecor);
            Invalidate(now);
            Rectangle m = CurrentMonitor();
            // Las líneas de la cruceta, de una en una (son muy finas): donde estaban y donde van.
            Invalidate(new Rectangle(crossMon.Left, crossY - 1, crossMon.Width, 3));
            Invalidate(new Rectangle(crossX - 1, crossMon.Top, 3, crossMon.Height));
            Invalidate(new Rectangle(m.Left, cur.Y - 1, m.Width, 3));
            Invalidate(new Rectangle(cur.X - 1, m.Top, 3, m.Height));
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
            Invalidate(r);
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
            if (r.IsEmpty) r = Screen.FromPoint(sp).Bounds; // sobre el escritorio: la pantalla entera
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            cur = e.Location;
            if (down && !dragging && (Math.Abs(cur.X - start.X) > Pz(4) || Math.Abs(cur.Y - start.Y) > Pz(4)))
            {
                dragging = true;
                InvalidateFrame(hover); // la ventana resaltada se apaga: ahora manda el área
            }
            if (dragging)
            {
                Rectangle old = sel;
                sel = Normalize(start, cur);
                Rectangle u = old.IsEmpty ? sel : Rectangle.Union(old, sel);
                u.Inflate(Pz(4), Pz(4));
                Invalidate(u);
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
                // Las flechas mueven el ratón píxel a píxel, para afinar.
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
            if (memDC != IntPtr.Zero)
            {
                Native.SelectObject(memDC, oldBmp);
                Native.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
            if (hbm != IntPtr.Zero) { Native.DeleteObject(hbm); hbm = IntPtr.Zero; }
        }
    }
}
