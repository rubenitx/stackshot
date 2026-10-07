// Stackshot - Video timeline in the editor: frame strip, trim handles and playhead.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Stackshot
{
    // iPhone-style trimming: yellow handles at both ends of a strip of frames; the part outside is dimmed. Clicking or
    // dragging inside the strip moves the playhead to preview that moment. Times are in seconds.
    public class Timeline : Control
    {
        public float S = 1f;
        public double Duration, In, Out, Playhead;
        public Bitmap[] Thumbs;
        public event Action Trimmed;          // handle released: In/Out changed
        public event Action<double> Seek;     // playhead released at a time
        public event Action OptionsChanged;

        // Export options, shown as segmented controls in the header.
        public static readonly string[] Speeds = { "1\u00D7", "1,5\u00D7", "2\u00D7" };
        public static readonly double[] SpeedValues = { 1, 1.5, 2 };
        public static readonly string[] Sizes = { "Original", "1080p", "720p" };
        public static readonly int[] SizeValues = { 0, 1080, 720 };
        public static readonly string[] Formats = { "MP4", "GIF" };
        public int Speed, OutSize, Format;
        readonly Rectangle[][] segs = new Rectangle[3][];
        int hotGroup = -1, hotSeg = -1;

        enum Grip { None, In, Out, Head }
        Grip drag, hot;
        static readonly Color Yellow = Color.FromArgb(255, 204, 0);

        public Timeline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Dark;
        }

        int P(float v) { return (int)Math.Round(v * S); }
        public bool IsTrimmed { get { return Duration > 0 && (In > 0.05 || Out < Duration - 0.05); } }

        Rectangle Strip { get { return new Rectangle(P(28), P(40), Math.Max(1, Width - P(56)), Height - P(50)); } }
        float X(double t) { Rectangle r = Strip; return r.X + (float)(r.Width * (Duration > 0 ? t / Duration : 0)); }
        double T(int x) { Rectangle r = Strip; return Math.Max(0, Math.Min(Duration, (x - r.X) / (double)Math.Max(1, r.Width) * Duration)); }

        public static string Clock(double t)
        {
            int m = (int)(t / 60);
            return m + ":" + (t - m * 60).ToString("00.0", CultureInfo.GetCultureInfo("es-ES"));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen line = new Pen(Theme.Border)) g.DrawLine(line, 0, 0, Width, 0);
            Rectangle r = Strip;
            Font f = Fonts.Get("Segoe UI", 11.5f * S), fb = Fonts.Get("Segoe UI Semibold", 11.5f * S);
            string sel = "Duraci\u00F3n " + Clock((Out - In) / SpeedValues[Speed]) + (IsTrimmed ? "  \u00B7  de " + Clock(In) + " a " + Clock(Out) : "");
            TextRenderer.DrawText(g, sel, fb, new Rectangle(r.X, P(7), r.Width, P(22)), Theme.Fg, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            PaintOptions(g, r, f);

            // Frame strip.
            using (GraphicsPath p = Theme.Round(r, P(6)))
            {
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Theme.Button)) g.FillRectangle(b, r);
                if (Thumbs != null && Thumbs.Length > 0)
                {
                    float tw = r.Width / (float)Thumbs.Length;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    for (int i = 0; i < Thumbs.Length; i++)
                    {
                        Bitmap t = Thumbs[i];
                        if (t == null) continue;
                        // Cover each slot, cropping the frame's sides.
                        float k = Math.Max(tw / t.Width, r.Height / (float)t.Height);
                        float w = t.Width * k, h = t.Height * k;
                        g.DrawImage(t, r.X + i * tw + (tw - w) / 2, r.Y + (r.Height - h) / 2, w, h);
                    }
                }
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(170, 10, 10, 14)))
                {
                    g.FillRectangle(dim, r.X, r.Y, X(In) - r.X, r.Height);
                    g.FillRectangle(dim, X(Out), r.Y, r.Right - X(Out), r.Height);
                }
                g.Clip = old;
                old.Dispose();
            }

            // Yellow trim frame with two grips.
            float x0 = X(In), x1 = X(Out), gw = P(12);
            RectangleF frame = new RectangleF(x0 - gw, r.Y - P(3), x1 - x0 + gw * 2, r.Height + P(6));
            using (GraphicsPath p = Theme.Round(frame, P(7)))
            using (Pen pen = new Pen(Yellow, P(3))) g.DrawPath(pen, p);
            foreach (float gx in new float[] { x0 - gw, x1 })
            {
                RectangleF grip = new RectangleF(gx, r.Y - P(3), gw, r.Height + P(6));
                using (GraphicsPath p = Theme.Round(grip, P(5))) MascotParts.FillSolid(g, p, Yellow);
                using (Pen tick = new Pen(Color.FromArgb(150, 60, 40, 0), Math.Max(1.5f, 2 * S)))
                {
                    tick.StartCap = LineCap.Round; tick.EndCap = LineCap.Round;
                    g.DrawLine(tick, grip.X + gw / 2, grip.Y + grip.Height * 0.35f, grip.X + gw / 2, grip.Y + grip.Height * 0.65f);
                }
            }

            // Playhead.
            float hx = X(Playhead);
            using (Pen ph = new Pen(Color.White, Math.Max(2f, 2.5f * S))) g.DrawLine(ph, hx, r.Y - P(6), hx, r.Bottom + P(6));
            g.FillEllipse(Brushes.White, hx - P(5), r.Y - P(10), P(10), P(10));
        }

        // Right-aligned groups: Velocidad, Tamaño, Formato.
        void PaintOptions(Graphics g, Rectangle r, Font f)
        {
            string[][] groups = { Speeds, Sizes, Formats };
            int[] sel = { Speed, OutSize, Format };
            string[] labels = { "Velocidad", "Tama\u00F1o", "Formato" };
            Font fs = Fonts.Get("Segoe UI Semibold", 11.5f * S);
            const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int x = r.Right, h = P(22), y = P(7);
            for (int gi = groups.Length - 1; gi >= 0; gi--)
            {
                string[] items = groups[gi];
                int[] ws = new int[items.Length];
                int total = P(4);
                for (int i = 0; i < items.Length; i++)
                {
                    ws[i] = TextRenderer.MeasureText(g, items[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + P(18);
                    total += ws[i];
                }
                Rectangle box = new Rectangle(x - total, y, total, h);
                using (GraphicsPath p = Theme.Round(box, P(7))) MascotParts.FillSolid(g, p, Theme.Button);
                segs[gi] = new Rectangle[items.Length];
                int sx = box.X + P(2);
                for (int i = 0; i < items.Length; i++)
                {
                    Rectangle sr = new Rectangle(sx, y + P(2), ws[i], h - P(4));
                    segs[gi][i] = sr;
                    bool on = i == sel[gi];
                    if (on || (gi == hotGroup && i == hotSeg))
                        using (GraphicsPath p = Theme.Round(sr, P(5))) MascotParts.FillSolid(g, p, on ? Theme.ButtonHover : Theme.Border);
                    TextRenderer.DrawText(g, items[i], on ? fs : f, sr, on ? Theme.Fg : Theme.Muted, C);
                    sx += ws[i];
                }
                int lw = TextRenderer.MeasureText(g, labels[gi], f, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, labels[gi], f, new Rectangle(box.X - lw - P(8), y, lw + P(2), h), Theme.Muted,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                x = box.X - lw - P(22);
            }
        }

        bool SegAt(Point p, out int group, out int seg)
        {
            for (group = 0; group < segs.Length; group++)
            {
                if (segs[group] == null) continue;
                for (seg = 0; seg < segs[group].Length; seg++) if (segs[group][seg].Contains(p)) return true;
            }
            group = seg = -1;
            return false;
        }

        Grip GripAt(Point p)
        {
            Rectangle r = Strip;
            if (p.Y < r.Y - P(10) || p.Y > r.Bottom + P(10)) return Grip.None;
            float gw = P(12);
            if (Math.Abs(p.X - (X(In) - gw / 2)) <= gw) return Grip.In;
            if (Math.Abs(p.X - (X(Out) + gw / 2)) <= gw) return Grip.Out;
            return r.Contains(p) ? Grip.Head : Grip.None;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int gi, si;
            if (SegAt(e.Location, out gi, out si))
            {
                if (gi == 0) Speed = si; else if (gi == 1) OutSize = si; else Format = si;
                Invalidate();
                if (OptionsChanged != null) OptionsChanged();
                return;
            }
            if (Duration <= 0) return;
            drag = GripAt(e.Location);
            OnMouseMove(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag == Grip.None)
            {
                int gi, si;
                SegAt(e.Location, out gi, out si);
                if (gi != hotGroup || si != hotSeg) { hotGroup = gi; hotSeg = si; Invalidate(new Rectangle(0, 0, Width, P(34))); }
                if (gi >= 0) { hot = (Grip)(-1); Cursor = Cursors.Hand; return; } // forces a cursor refresh on the way out
                Grip h = GripAt(e.Location);
                if (h != hot) { hot = h; Cursor = h == Grip.In || h == Grip.Out ? Cursors.SizeWE : h == Grip.Head ? Cursors.Hand : Cursors.Default; }
                return;
            }
            double t = T(e.X), min = Math.Min(0.3, Duration / 4);
            if (drag == Grip.In) { In = Math.Min(t, Out - min); Playhead = In; }
            else if (drag == Grip.Out) { Out = Math.Max(t, In + min); Playhead = Out; }
            else Playhead = Math.Max(In, Math.Min(Out, t));
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hotGroup >= 0) { hotGroup = hotSeg = -1; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Grip d = drag;
            drag = Grip.None;
            if (d == Grip.In || d == Grip.Out) { if (Trimmed != null) Trimmed(); }
            if (d != Grip.None && Seek != null) Seek(Playhead);
        }
    }
}
