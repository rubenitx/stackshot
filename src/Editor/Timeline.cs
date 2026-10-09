// Stackshot - Video timeline in the editor: frame strip, trim handles and playhead.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using M = System.Windows.Media;

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

        // Cuts: the clip is split into pieces at the playhead; a piece can be removed and the rest are joined on export.
        public class Piece { public double A, B; public bool Off; }
        public readonly List<Piece> Pieces = new List<Piece>();
        Rectangle splitBtn, cutBtn;
        int hotBtn = -1;
        const double MinPiece = 0.2;

        enum Grip { None, In, Out, Head }
        Grip drag, hot;

        public Timeline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = EdLook.C(Ds.Brushes.Window);
            Pieces.Add(new Piece());
        }

        public void Init(double duration)
        {
            Duration = duration;
            Out = duration;
            Pieces.Clear();
            Pieces.Add(new Piece { A = 0, B = duration });
        }

        Piece PieceAt(double t)
        {
            foreach (Piece p in Pieces) if (t >= p.A && t <= p.B) return p;
            return null;
        }

        public bool CanSplit
        {
            get
            {
                Piece p = PieceAt(Playhead);
                return p != null && Playhead > p.A + MinPiece && Playhead < p.B - MinPiece;
            }
        }

        // The piece under the playhead can be removed unless it is the last one left; a removed one can be restored.
        public bool CanCut
        {
            get
            {
                Piece p = PieceAt(Playhead);
                if (p == null) return false;
                if (p.Off) return true;
                foreach (Piece o in Pieces) if (o != p && !o.Off) return true;
                return false;
            }
        }

        public bool CutOn { get { Piece p = PieceAt(Playhead); return p != null && p.Off; } }

        public void SplitAtPlayhead()
        {
            if (!CanSplit) return;
            Piece p = PieceAt(Playhead);
            Piece n = new Piece { A = Playhead, B = p.B, Off = p.Off };
            p.B = Playhead;
            Pieces.Insert(Pieces.IndexOf(p) + 1, n);
            Changed();
        }

        public void ToggleCut()
        {
            if (!CanCut) return;
            Piece p = PieceAt(Playhead);
            p.Off = !p.Off;
            Changed();
        }

        void Changed()
        {
            Invalidate();
            if (Trimmed != null) Trimmed();
        }

        // The ranges that survive trimming and cuts, in order, adjacent ones merged.
        public List<double[]> Keep()
        {
            List<double[]> r = new List<double[]>();
            foreach (Piece p in Pieces)
            {
                if (p.Off) continue;
                double a = Math.Max(p.A, In), b = Math.Min(p.B, Out);
                if (b - a < 0.05) continue;
                if (r.Count > 0 && a - r[r.Count - 1][1] < 0.001) r[r.Count - 1][1] = b;
                else r.Add(new double[] { a, b });
            }
            return r;
        }

        public bool HasCuts
        {
            get
            {
                foreach (Piece p in Pieces) if (p.Off && Math.Min(p.B, Out) - Math.Max(p.A, In) > 0.05) return true;
                return false;
            }
        }

        public double KeptSeconds
        {
            get
            {
                double t = 0;
                foreach (double[] k in Keep()) t += k[1] - k[0];
                return t;
            }
        }

        int P(float v) { return (int)Math.Round(v * S); }
        public bool IsTrimmed { get { return Duration > 0 && (In > 0.05 || Out < Duration - 0.05); } }

        Rectangle Strip { get { return new Rectangle(P(28), P(40), Math.Max(1, Width - P(56)), Height - P(50)); } }
        public int StripHeight { get { return Strip.Height; } }
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
            Palette pal = Ds.Brushes;
            Color window = EdLook.C(pal.Window);
            g.Clear(window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (SolidBrush line = new SolidBrush(EdLook.C(pal.Separator))) g.FillRectangle(line, 0, 0, Width, 1);
            Rectangle r = Strip;
            Font f = Fonts.Get(EdLook.Text, 12f * S);
            string sel = "Duraci\u00F3n " + Clock(KeptSeconds / SpeedValues[Speed]) + (IsTrimmed ? "  \u00B7  de " + Clock(In) + " a " + Clock(Out) : "") +
                         (HasCuts ? "  \u00B7  " + Keep().Count + " tramos" : "");
            // Never under the options: a long trimmed clip in a narrow window ends in an ellipsis instead.
            int optionsLeft = PaintOptions(g, pal, r, f);
            int tx = PaintCutButtons(g, pal, r, f);
            TextKit.Draw(g, sel, f, new Rectangle(tx, P(8), Math.Max(0, optionsLeft - P(16) - tx), P(22)), EdLook.C(pal.Label2),
                         TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Frame strip with rounded ends.
            float rad = P(8);
            using (GraphicsPath p = Theme.Round(r, rad))
            {
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(EdLook.C(pal.Control))) g.FillRectangle(b, r);
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
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(Ds.Dark ? 170 : 160, window)))
                {
                    g.FillRectangle(dim, r.X, r.Y, X(In) - r.X, r.Height);
                    g.FillRectangle(dim, X(Out), r.Y, r.Right - X(Out), r.Height);
                    // Removed pieces: dimmed twice and crossed by a thin line.
                    foreach (Piece pc in Pieces)
                    {
                        if (!pc.Off) continue;
                        g.FillRectangle(dim, X(pc.A), r.Y, X(pc.B) - X(pc.A), r.Height);
                        g.FillRectangle(dim, X(pc.A), r.Y, X(pc.B) - X(pc.A), r.Height);
                        using (Pen x = new Pen(EdLook.C(pal.Label2), Math.Max(1f, S)))
                            g.DrawLine(x, X(pc.A) + P(3), r.Y + r.Height / 2f, X(pc.B) - P(3), r.Y + r.Height / 2f);
                    }
                    // Split marks.
                    using (Pen sp = new Pen(Color.FromArgb(Ds.Dark ? 200 : 230, Color.White), Math.Max(1f, S)))
                    using (Pen so = new Pen(Color.FromArgb(90, Color.Black), Math.Max(1f, S) + 2))
                        for (int i = 1; i < Pieces.Count; i++)
                        {
                            float sx = (float)Math.Round(X(Pieces[i].A));
                            g.DrawLine(so, sx, r.Y, sx, r.Bottom);
                            g.DrawLine(sp, sx, r.Y, sx, r.Bottom);
                        }
                }
                g.Clip = old;
                old.Dispose();
                using (Pen hl = new Pen(EdLook.C(pal.Separator))) g.DrawPath(hl, p);
            }

            // Yellow trim frame: thick grips at both ends joined by thin top and bottom bars.
            Color yellow = Color.FromArgb(255, 214, 10);
            float x0 = X(In), x1 = X(Out), gw = P(12), bar = Math.Max(2f, 3f * S);
            RectangleF frame = new RectangleF(x0 - gw, r.Y - bar, x1 - x0 + gw * 2, r.Height + bar * 2);
            using (GraphicsPath p = Theme.Round(frame, rad))
            {
                RectangleF hole = new RectangleF(x0, r.Y, Math.Max(0, x1 - x0), r.Height);
                if (hole.Width > 0) using (GraphicsPath h = Theme.Round(hole, 2f * S)) p.AddPath(h, false);
                p.FillMode = FillMode.Alternate;
                using (SolidBrush b = new SolidBrush(yellow)) g.FillPath(b, p);
            }
            M.Color chev = Ds.Argb(0.72, 60, 44, 0);
            EdLook.Icon(g, "back", new RectangleF(x0 - gw, r.Y, gw, r.Height), P(14), chev, Math.Max(1.5f, 2f * S));
            EdLook.Icon(g, "right", new RectangleF(x1, r.Y, gw, r.Height), P(14), chev, Math.Max(1.5f, 2f * S));

            // Playhead: thin line with a soft outline so it reads on any frame.
            float hx = (float)Math.Round(X(Playhead));
            float pw = Math.Max(2f, 2f * S);
            RectangleF head = new RectangleF(hx - pw / 2, r.Y - P(5), pw, r.Height + P(10));
            EdLook.Fill(g, RectangleF.Inflate(head, 1f, 1f), pw, Color.FromArgb(Ds.Dark ? 120 : 90, Ds.Dark ? Color.Black : Color.White));
            EdLook.Fill(g, head, pw / 2, EdLook.C(pal.Label));
            // While exporting the timeline is locked, and looks it.
            if (!Enabled) using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, window))) g.FillRectangle(veil, 0, 1, Width, Height - 1);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { drag = Grip.None; hotGroup = hotSeg = -1; }
            Invalidate();
        }

        // "Dividir" and "Quitar tramo" / "Recuperar" at the left of the header. Returns where the duration text starts.
        int PaintCutButtons(Graphics g, Palette pal, Rectangle r, Font f)
        {
            Font fs = Fonts.Get(EdLook.Semibold, 11.5f * S);
            const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            string[] labels = { "Dividir", CutOn ? "Recuperar" : "Quitar tramo" };
            bool[] on = { CanSplit, CanCut };
            int x = r.X, h = P(24), y = P(7);
            for (int i = 0; i < 2; i++)
            {
                int w = TextKit.Measure(g, labels[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + P(22);
                Rectangle b = new Rectangle(x, y, w, h);
                if (i == 0) splitBtn = b; else cutBtn = b;
                Color bg = EdLook.C(pal.Control);
                if (i == hotBtn && on[i]) bg = Color.FromArgb(Ds.Dark ? 60 : 40, EdLook.C(pal.Label));
                EdLook.Fill(g, b, P(7), bg);
                TextKit.Draw(g, labels[i], fs, b, EdLook.C(on[i] ? pal.Label : pal.Label2), C);
                x += w + P(6);
            }
            return x + P(10);
        }

        // Right-aligned groups: speed, size and format. Returns where the leftmost label starts.
        int PaintOptions(Graphics g, Palette pal, Rectangle r, Font f)
        {
            int leftmost = r.Right;
            string[][] groups = { Speeds, Sizes, Formats };
            int[] sel = { Speed, OutSize, Format };
            string[] labels = { "Velocidad", "Tama\u00F1o", "Formato" };
            Font fs = Fonts.Get(EdLook.Semibold, 11.5f * S), fr = Fonts.Get(EdLook.Text, 11.5f * S);
            const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int x = r.Right, h = P(24), y = P(7);
            for (int gi = groups.Length - 1; gi >= 0; gi--)
            {
                string[] items = groups[gi];
                int[] ws = new int[items.Length];
                int total = P(4);
                for (int i = 0; i < items.Length; i++)
                {
                    ws[i] = TextKit.Measure(g, items[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + P(18);
                    total += ws[i];
                }
                Rectangle box = new Rectangle(x - total, y, total, h);
                EdLook.Fill(g, box, P(7), EdLook.C(pal.Control));
                segs[gi] = new Rectangle[items.Length];
                int sx = box.X + P(2);
                for (int i = 0; i < items.Length; i++)
                {
                    Rectangle sr = new Rectangle(sx, y + P(2), ws[i], h - P(4));
                    segs[gi][i] = sr;
                    bool on = i == sel[gi];
                    if (on)
                    {
                        if (!Ds.Dark) EdLook.Fill(g, new RectangleF(sr.X, sr.Y + 0.75f * S, sr.Width, sr.Height), P(5), Color.FromArgb(22, 0, 0, 0));
                        EdLook.Fill(g, sr, P(5), Ds.Dark ? Color.FromArgb(92, 255, 255, 255) : Color.White);
                    }
                    else if (gi == hotGroup && i == hotSeg) EdLook.Fill(g, sr, P(5), EdLook.C(pal.Control));
                    TextKit.Draw(g, items[i], on ? fs : fr, sr, EdLook.C(on ? pal.Label : pal.Label2), C);
                    sx += ws[i];
                }
                int lw = TextKit.Measure(g, labels[gi], f, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextKit.Draw(g, labels[gi], f, new Rectangle(box.X - lw - P(8), y, lw + P(2), h), EdLook.C(pal.Label2),
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                leftmost = box.X - lw - P(8);
                x = box.X - lw - P(22);
            }
            return leftmost;
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
            if (splitBtn.Contains(e.Location)) { SplitAtPlayhead(); return; }
            if (cutBtn.Contains(e.Location)) { ToggleCut(); return; }
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
                int hb = splitBtn.Contains(e.Location) ? 0 : cutBtn.Contains(e.Location) ? 1 : -1;
                if (hb != hotBtn) { hotBtn = hb; Invalidate(new Rectangle(0, 0, Width, P(34))); }
                if (hb >= 0) { hot = (Grip)(-1); Cursor = Cursors.Hand; return; }
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
            EndDrag();
        }

        // Capture lost mid-drag (a dialog, Alt+Tab): the drag ends where it is, so the handle does not keep following
        // the pointer with no button held.
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) EndDrag();
        }

        void EndDrag()
        {
            Grip d = drag;
            drag = Grip.None;
            if (d == Grip.In || d == Grip.Out) { if (Trimmed != null) Trimmed(); }
            if (d != Grip.None && Seek != null) Seek(Playhead);
        }
    }
}
