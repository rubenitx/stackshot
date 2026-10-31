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
        // The videos joined on this timeline (their thumbnails and where each starts); one entry is the usual single video.
        public List<VideoClip> Clips = new List<VideoClip>();
        public event Action Trimmed;          // handle released: In/Out changed
        public event Action<double> Seek;     // playhead released at a time
        public event Action OptionsChanged;
        public event Action AddClip;               // the add-video button
        public event Action<int> ClipRemove;       // a clip's x or its menu
        public event Action<int, int> ClipMove;    // clip index and direction (-1 left, +1 right)

        // Export options, shown as segmented controls in the header.
        static readonly string[] SpeedsEs = { "1\u00D7", "1,5\u00D7", "2\u00D7" };
        static readonly string[] SpeedsEn = { "1\u00D7", "1.5\u00D7", "2\u00D7" };
        public static string[] Speeds { get { return I18n.IsSpanish ? SpeedsEs : SpeedsEn; } }
        public static readonly double[] SpeedValues = { 1, 1.5, 2 };
        public static readonly string[] Sizes = { "Original", "1080p", "720p" };
        public static readonly int[] SizeValues = { 0, 1080, 720 };
        // The last two fit a size: WhatsApp's 16 MB and the 25 MB most mail takes (VideoExport.TargetBytes).
        static readonly string[] QualitiesEs = { "M\u00E1xima", "Alta", "Ligera", "16 MB", "25 MB" };
        static readonly string[] QualitiesEn = { "Maximum", "High", "Light", "16 MB", "25 MB" };
        public static string[] Qualities { get { return I18n.IsSpanish ? QualitiesEs : QualitiesEn; } }
        public static readonly string[] Formats = { "MP4", "GIF" };
        public int Speed, OutSize, Quality, Format;
        readonly Rectangle[][] segs = new Rectangle[4][];
        int hotGroup = -1, hotSeg = -1;

        // Cuts: the clip is split into pieces at the playhead; a piece can be removed and the rest are joined on export.
        public class Piece { public double A, B; public bool Off; }
        public readonly List<Piece> Pieces = new List<Piece>();
        Rectangle splitBtn, cutBtn, playBtn, soundBtn, addBtn, textRect;
        int hotBtn = -1, hoverClip = -1;
        public bool CanAddClips;

        // Playback: the editor drives the playhead and the clock while it plays, and shows the sound button when the file has sound.
        bool playing, muted;
        public bool CanPlay, HasSound;
        public event Action PlayToggle;
        public event Action<Point> SoundClick;   // screen point under the sound button, for the menu
        public bool Playing { get { return playing; } set { if (playing != value) { playing = value; Invalidate(); } } }
        public bool Muted { get { return muted; } set { if (muted != value) { muted = value; Invalidate(); } } }
        public bool Dragging { get { return drag != Grip.None; } }

        // Moves the playhead and repaints only its old and new place (and the clock while playing).
        public void SetPlayhead(double t)
        {
            if (t == Playhead) return;
            InvalidateHead();
            Playhead = t;
            InvalidateHead();
            if (playing) Invalidate(textRect);
        }

        void InvalidateHead()
        {
            Rectangle r = Strip;
            int x = (int)X(Playhead);
            Invalidate(new Rectangle(x - P(8), r.Y - P(8), P(16), r.Height + P(16)));
        }

        // The time playing from t should continue at: t itself inside what is kept, the start of the next kept range
        // when t falls in a removed piece, positive infinity when nothing is left to play.
        public double PlayableFrom(double t)
        {
            foreach (Piece p in Pieces)
            {
                if (p.Off) continue;
                double a = Math.Max(p.A, In), b = Math.Min(p.B, Out);
                if (b - a < 0.05) continue;
                if (t < b - 0.001) return Math.Max(t, a);
            }
            return double.PositiveInfinity;
        }
        const double MinPiece = 0.2;

        enum Grip { None, In, Out, Head }
        Grip drag, hot;

        public Timeline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = EdLook.C(Ds.Brushes.Window);
            Pieces.Add(new Piece());
        }

        // Before every trim, split or cut (for undo). Adding, removing or moving videos starts a new layout: older steps
        // no longer fit it and leave the timeline as it is.
        public event Action Changing;
        int layout;
        bool dragNoted;

        public sealed class State { public int Layout; public double In, Out; public Piece[] Pieces; }

        public State Save()
        {
            State st = new State { Layout = layout, In = In, Out = Out, Pieces = new Piece[Pieces.Count] };
            for (int i = 0; i < Pieces.Count; i++) st.Pieces[i] = new Piece { A = Pieces[i].A, B = Pieces[i].B, Off = Pieces[i].Off };
            return st;
        }

        // False when the state belongs to another layout (nothing changes then).
        public bool Load(State st)
        {
            if (st == null || st.Layout != layout) return false;
            In = st.In;
            Out = st.Out;
            Pieces.Clear();
            foreach (Piece p in st.Pieces) Pieces.Add(new Piece { A = p.A, B = p.B, Off = p.Off });
            Playhead = Math.Max(In, Math.Min(Out, Playhead));
            Invalidate();
            return true;
        }

        void Noting() { if (Changing != null) Changing(); }

        // The keyboard's way to move the playhead: kept within the trim, then shown as a released drag would be, and
        // told to screen readers.
        public void MoveHead(double t)
        {
            if (Duration <= 0) return;
            t = Math.Max(In, Math.Min(Out, t));
            SetPlayhead(t);
            if (Seek != null) Seek(t);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new Access(this); }

        // A slider for screen readers: its name, position and length, and whether it is trimmed or cut.
        sealed class Access : ControlAccessibleObject
        {
            readonly Timeline t;
            public Access(Timeline t) : base(t) { this.t = t; }
            public override AccessibleRole Role { get { return AccessibleRole.Slider; } }
            public override string Name { get { return I18n.T("L\u00EDnea de tiempo", "Timeline"); } }
            public override string Value
            {
                get
                {
                    string v = I18n.Fmt("{0} de {1}", "{0} of {1}", Clock(t.Playhead), Clock(t.Duration));
                    if (t.IsTrimmed) v += I18n.Fmt(", recortada de {0} a {1}", ", trimmed from {0} to {1}", Clock(t.In), Clock(t.Out));
                    if (t.HasCuts) v += I18n.T(", con tramos quitados", ", with pieces removed");
                    return v;
                }
            }
            public override string Description
            {
                get
                {
                    return I18n.T("Flechas: fotograma a fotograma. May\u00FAs y flechas: de segundo en segundo. Inicio y Fin: principio y final del recorte.",
                                  "Arrows: frame by frame. Shift and arrows: second by second. Home and End: start and end of the trim.");
                }
            }
        }

        public void Init(double duration)
        {
            layout++;
            Duration = duration;
            Out = duration;
            Pieces.Clear();
            Pieces.Add(new Piece { A = 0, B = duration });
        }

        // A video was appended: it is kept whole. What the old end trim left out becomes a cut piece, so the trim of the
        // previous video still holds and the new one is not left outside the handles.
        public void Extend(double total)
        {
            layout++;
            double old = Duration, oldOut = Out;
            if (oldOut < old - 0.05)
            {
                Piece p = PieceAt(oldOut);
                if (p != null && oldOut > p.A + 0.001 && oldOut < p.B - 0.001)
                {
                    Piece n = new Piece { A = oldOut, B = p.B, Off = p.Off };
                    p.B = oldOut;
                    Pieces.Insert(Pieces.IndexOf(p) + 1, n);
                }
                foreach (Piece q in Pieces) if (q.A >= oldOut - 0.001) q.Off = true;
            }
            Pieces.Add(new Piece { A = old, B = total });
            Duration = total;
            Out = total;
            Invalidate();
        }

        // Videos were removed or reordered: every time moves with its video. map takes a time and whether it is the end
        // of a span (the end of a piece belongs to the video it closes, a start to the one it opens).
        public void Relayout(Func<double, bool, double> map, double total)
        {
            layout++;
            List<Piece> moved = new List<Piece>();
            foreach (Piece p in Pieces)
            {
                double a = map(p.A, false), b = map(p.B, true);
                if (b - a > 0.0005) moved.Add(new Piece { A = a, B = b, Off = p.Off });
            }
            moved.Sort(delegate(Piece x, Piece y) { return x.A.CompareTo(y.A); });
            // A handle at the very start or end stays there, whichever clip ends up first or last.
            bool inAtStart = In <= 0.0005, outAtEnd = Out >= Duration - 0.0005;
            In = inAtStart ? 0 : Math.Max(0, Math.Min(total, map(In, false)));
            Out = outAtEnd ? total : Math.Max(In, Math.Min(total, map(Out, true)));
            Duration = total;
            Pieces.Clear();
            Pieces.AddRange(moved);
            if (Pieces.Count == 0) Pieces.Add(new Piece { A = 0, B = total });
            Playhead = Math.Max(In, Math.Min(Out, Playhead));
            Invalidate();
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
            Noting();
            Piece p = PieceAt(Playhead);
            Piece n = new Piece { A = Playhead, B = p.B, Off = p.Off };
            p.B = Playhead;
            Pieces.Insert(Pieces.IndexOf(p) + 1, n);
            Changed();
        }

        public void ToggleCut()
        {
            if (!CanCut) return;
            Noting();
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

        public double KeptSeconds { get { return Seconds(Keep()); } }

        static double Seconds(List<double[]> keep)
        {
            double t = 0;
            foreach (double[] k in keep) t += k[1] - k[0];
            return t;
        }

        int P(float v) { return (int)Math.Round(v * S); }
        public bool IsTrimmed { get { return Duration > 0 && (In > 0.05 || Out < Duration - 0.05); } }

        Rectangle Strip { get { return new Rectangle(P(28), P(40), Math.Max(1, Width - P(56)), Height - P(50)); } }
        public int StripHeight { get { return Strip.Height; } }
        float X(double t) { Rectangle r = Strip; return r.X + (float)(r.Width * (Duration > 0 ? t / Duration : 0)); }
        double T(int x) { Rectangle r = Strip; return Math.Max(0, Math.Min(Duration, (x - r.X) / (double)Math.Max(1, r.Width) * Duration)); }

        static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

        public static string Clock(double t)
        {
            int m = (int)(t / 60);
            return m + ":" + (t - m * 60).ToString("00.0", I18n.IsSpanish ? Spanish : CultureInfo.InvariantCulture);
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
            List<double[]> keep = Keep();
            string sel = (playing ? Clock(Playhead) + "  \u00B7  " : "") + (Clips.Count > 1 ? I18n.Fmt("{0} v\u00EDdeos  \u00B7  ", "{0} videos  \u00B7  ", Clips.Count) : "") +
                         I18n.T("Duraci\u00F3n ", "Duration ") + Clock(Seconds(keep) / SpeedValues[Speed]) +
                         (IsTrimmed ? I18n.Fmt("  \u00B7  de {0} a {1}", "  \u00B7  from {0} to {1}", Clock(In), Clock(Out)) : "") +
                         (HasCuts ? I18n.Fmt("  \u00B7  {0} tramos", "  \u00B7  {0} pieces", keep.Count) : "");
            // Never under the options: a long trimmed clip in a narrow window ends in an ellipsis instead.
            int tx = PaintCutButtons(g, pal, r, f);
            int optionsLeft = PaintOptions(g, pal, r, f, tx);
            textRect = new Rectangle(tx, P(8), Math.Max(0, optionsLeft - P(16) - tx), P(22));
            TextKit.Draw(g, sel, f, textRect, EdLook.C(pal.Label2),
                         TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Frame strip with rounded ends.
            float rad = P(8);
            using (GraphicsPath p = Theme.Round(r, rad))
            {
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(EdLook.C(pal.Control))) g.FillRectangle(b, r);
                PaintThumbs(g, r);
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
            Color yellow = pal.High ? EdLook.C(pal.Accent) : Color.FromArgb(255, 214, 10);
            float x0 = X(In), x1 = X(Out), gw = P(12), bar = Math.Max(2f, 3f * S);
            RectangleF frame = new RectangleF(x0 - gw, r.Y - bar, x1 - x0 + gw * 2, r.Height + bar * 2);
            using (GraphicsPath p = Theme.Round(frame, rad))
            {
                RectangleF hole = new RectangleF(x0, r.Y, Math.Max(0, x1 - x0), r.Height);
                if (hole.Width > 0) using (GraphicsPath h = Theme.Round(hole, 2f * S)) p.AddPath(h, false);
                p.FillMode = FillMode.Alternate;
                using (SolidBrush b = new SolidBrush(yellow)) g.FillPath(b, p);
            }
            M.Color chev = pal.High ? pal.OnAccent : Ds.Argb(0.72, 60, 44, 0);
            EdLook.Icon(g, "back", new RectangleF(x0 - gw, r.Y, gw, r.Height), P(14), chev, Math.Max(1.5f, 2f * S));
            EdLook.Icon(g, "right", new RectangleF(x1, r.Y, gw, r.Height), P(14), chev, Math.Max(1.5f, 2f * S));

            // Playhead: thin line with a soft outline so it reads on any frame.
            float hx = (float)Math.Round(X(Playhead));
            float pw = Math.Max(2f, 2f * S);
            RectangleF head = new RectangleF(hx - pw / 2, r.Y - P(5), pw, r.Height + P(10));
            EdLook.Fill(g, RectangleF.Inflate(head, 1f, 1f), pw, Color.FromArgb(Ds.Dark ? 120 : 90, Ds.Dark ? Color.Black : Color.White));
            EdLook.Fill(g, head, pw / 2, EdLook.C(pal.Label));
            PaintClipHover(g, pal, r);
            // While exporting the timeline is locked, and looks it.
            if (!Enabled) using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, window))) g.FillRectangle(veil, 0, 1, Width, Height - 1);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { drag = Grip.None; hotGroup = hotSeg = -1; }
            Invalidate();
        }

        // Where a clip sits in the strip (the whole strip's height).
        Rectangle ClipRect(int i)
        {
            Rectangle r = Strip;
            VideoClip c = Clips[i];
            int x0 = i == 0 ? r.X : (int)Math.Round(X(c.Offset)), x1 = i == Clips.Count - 1 ? r.Right : (int)Math.Round(X(c.Offset + c.Duration));
            return new Rectangle(x0, r.Y, Math.Max(1, x1 - x0), r.Height);
        }

        int ClipIndexAt(Point p)
        {
            if (Clips.Count < 2 || !Strip.Contains(p)) return -1;
            for (int i = 0; i < Clips.Count; i++) if (p.X >= ClipRect(i).X && p.X < ClipRect(i).Right) return i;
            return -1;
        }

        // The small x that removes a clip (never the first, which defines the canvas), at the top right of the clip.
        Rectangle XRect(int i)
        {
            Rectangle c = ClipRect(i);
            int sz = P(18);
            return new Rectangle(Math.Max(c.X, c.Right - sz - P(4)), c.Y + P(4), sz, sz);
        }

        // Each clip's thumbnails over its own part of the strip, with a boundary and the name where there are several.
        void PaintThumbs(Graphics g, Rectangle r)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            // While playing only the playhead's sliver is repainted: slots outside it would draw nothing.
            RectangleF painted = g.ClipBounds;
            painted.Inflate(1, 1);
            for (int ci = 0; ci < Clips.Count; ci++)
            {
                Bitmap[] th = Clips[ci].Thumbs;
                Rectangle cr = ClipRect(ci);
                if (th == null || th.Length == 0) continue;
                // Never slots much narrower than the strip is tall: a clip squeezed into a short span shows only some of its frames.
                int show = th.Length;
                float minSlot = r.Height * 0.45f;
                if (cr.Width / (float)show < minSlot) show = Math.Max(1, (int)(cr.Width / minSlot));
                float tw = cr.Width / (float)show;
                for (int i = 0; i < show; i++)
                {
                    if (!new RectangleF(cr.X + i * tw, r.Y, tw, r.Height).IntersectsWith(painted)) continue;
                    Bitmap t = th[show == th.Length ? i : Math.Min(th.Length - 1, (int)((i + 0.5) * th.Length / show))];
                    if (t == null) continue;
                    // Cover each slot, cropping the frame's sides.
                    float k = Math.Max(tw / t.Width, r.Height / (float)t.Height);
                    float w = t.Width * k, h = t.Height * k;
                    Region keep = g.Clip;
                    g.SetClip(new RectangleF(cr.X + i * tw, r.Y, tw, r.Height), CombineMode.Intersect);
                    g.DrawImage(t, cr.X + i * tw + (tw - w) / 2, r.Y + (r.Height - h) / 2, w, h);
                    g.Clip = keep;
                    keep.Dispose();
                }
            }
            if (Clips.Count < 2) return;
            Font nf = Fonts.Get(EdLook.Semibold, 10.5f * S);
            for (int ci = 0; ci < Clips.Count; ci++)
            {
                Rectangle cr = ClipRect(ci);
                if (ci > 0)
                {
                    using (Pen dark = new Pen(Color.FromArgb(150, Color.Black), Math.Max(1f, S) * 4))
                    using (Pen line = new Pen(Color.White, Math.Max(1f, S) * 2))
                    {
                        g.DrawLine(dark, cr.X, r.Y, cr.X, r.Bottom);
                        g.DrawLine(line, cr.X, r.Y, cr.X, r.Bottom);
                    }
                }
                if (cr.Width < P(48)) continue;
                string label = (ci + 1) + "  " + Clips[ci].Name;
                Rectangle pill = new Rectangle(cr.X + P(6), r.Y + P(5), Math.Min(cr.Width - P(12) - (ci > 0 ? P(22) : 0), TextKit.Measure(g, label, nf, Size.Empty, TextFormatFlags.NoPadding).Width + P(14)), P(18));
                if (pill.Width < P(20)) continue;
                EdLook.Fill(g, pill, P(5), Color.FromArgb(150, 0, 0, 0));
                TextKit.Draw(g, label, nf, new Rectangle(pill.X + P(7), pill.Y, pill.Width - P(10), pill.Height), Color.White,
                             TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }

        void PaintClipHover(Graphics g, Palette pal, Rectangle r)
        {
            if (hoverClip < 1 || hoverClip >= Clips.Count || drag != Grip.None || !Enabled) return;
            Rectangle x = XRect(hoverClip);
            EdLook.Fill(g, x, x.Width / 2, Color.FromArgb(200, 0, 0, 0));
            using (Pen p = new Pen(Color.White, Math.Max(1.5f, 1.6f * S)))
            {
                float m = x.Width * 0.3f;
                g.DrawLine(p, x.X + m, x.Y + m, x.Right - m, x.Bottom - m);
                g.DrawLine(p, x.Right - m, x.Y + m, x.X + m, x.Bottom - m);
            }
        }

        // The clip's menu: the first one only says why it stays; the others move left or right, or go.
        void ClipMenu(int i, Point at)
        {
            ContextMenuStrip m = TrayMenu.Create();
            if (i == 0)
            {
                ToolStripMenuItem info = TrayMenu.Item(I18n.T("V\u00EDdeo base: marca el tama\u00F1o y los fotogramas", "Base video: sets resolution and frame rate"), null, delegate { });
                info.Enabled = false;
                m.Items.Add(info);
            }
            else
            {
                ToolStripMenuItem left = TrayMenu.Item(I18n.T("Mover a la izquierda", "Move left"), null, delegate { if (ClipMove != null) ClipMove(i, -1); });
                left.Enabled = i >= 2;
                ToolStripMenuItem right = TrayMenu.Item(I18n.T("Mover a la derecha", "Move right"), null, delegate { if (ClipMove != null) ClipMove(i, 1); });
                right.Enabled = i < Clips.Count - 1;
                m.Items.Add(left);
                m.Items.Add(right);
                m.Items.Add(TrayMenu.Separator());
                m.Items.Add(TrayMenu.Item(I18n.T("Quitar este v\u00EDdeo", "Remove this video"), null, delegate { if (ClipRemove != null) ClipRemove(i); }));
            }
            m.Closed += delegate { BeginInvoke((Action)delegate { m.Dispose(); }); };
            m.Show(PointToScreen(at));
        }

        // Play or pause as an icon button.
        void PaintIconButton(Graphics g, Palette pal, Rectangle b, string glyph, bool hot)
        {
            if (pal.High) { EdLook.Icon(g, glyph, b, P(14), EdHc.Button(g, b, P(7), hot ? 1 : 0, true), Math.Max(1.5f, 1.9f * S)); return; }
            Color bg = EdLook.C(pal.Control);
            if (hot) bg = Color.FromArgb(Ds.Dark ? 60 : 40, EdLook.C(pal.Label));
            EdLook.Fill(g, b, P(7), bg);
            EdLook.Icon(g, glyph, b, P(14), pal.Label, Math.Max(1.5f, 1.9f * S));
        }

        // Play, then "Dividir" and "Quitar tramo" / "Recuperar" (not while playing) and the sound button, at the left of
        // the header. Returns where the duration text starts.
        int PaintCutButtons(Graphics g, Palette pal, Rectangle r, Font f)
        {
            Font fs = Fonts.Get(EdLook.Semibold, 11.5f * S);
            const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            string[] labels = { I18n.T("Dividir", "Split"), CutOn ? I18n.T("Recuperar", "Restore") : I18n.T("Quitar tramo", "Remove piece") };
            bool[] on = { CanSplit && !playing, CanCut && !playing };
            int x = r.X, h = P(24), y = P(7);
            if (CanPlay)
            {
                playBtn = new Rectangle(x, y, P(30), h);
                PaintIconButton(g, pal, playBtn, playing ? "pause" : "play!", hotBtn == 2);
                x += playBtn.Width + P(6);
            }
            else playBtn = Rectangle.Empty;
            for (int i = 0; i < 2; i++)
            {
                int w = TextKit.Measure(g, labels[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + P(22);
                Rectangle b = new Rectangle(x, y, w, h);
                if (i == 0) splitBtn = b; else cutBtn = b;
                Color bg = EdLook.C(pal.Control);
                if (i == hotBtn && on[i]) bg = Color.FromArgb(Ds.Dark ? 60 : 40, EdLook.C(pal.Label));
                M.Color ink = on[i] ? pal.Label : pal.Label2;
                if (pal.High) ink = EdHc.Button(g, b, P(7), i == hotBtn ? 1 : 0, on[i]);
                else EdLook.Fill(g, b, P(7), bg);
                TextKit.Draw(g, labels[i], fs, b, EdLook.C(ink), C);
                x += w + P(6);
            }
            if (HasSound)
            {
                soundBtn = new Rectangle(x, y, P(30), h);
                PaintIconButton(g, pal, soundBtn, muted ? "soundoff" : "sound", hotBtn == 3);
                x += soundBtn.Width + P(6);
            }
            else soundBtn = Rectangle.Empty;
            if (CanAddClips)
            {
                // A narrow window gets the short label.
                string add = Width < P(1000) ? I18n.T("+ V\u00EDdeo", "+ Video") : I18n.T("A\u00F1adir v\u00EDdeo", "Add video");
                int w = TextKit.Measure(g, add, fs, Size.Empty, TextFormatFlags.NoPadding).Width + P(22);
                addBtn = new Rectangle(x, y, w, h);
                Color bg = EdLook.C(pal.Control);
                if (hotBtn == 4) bg = Color.FromArgb(Ds.Dark ? 60 : 40, EdLook.C(pal.Label));
                M.Color addInk = pal.Label;
                if (pal.High) addInk = EdHc.Button(g, addBtn, P(7), hotBtn == 4 ? 1 : 0, true);
                else EdLook.Fill(g, addBtn, P(7), bg);
                TextKit.Draw(g, add, fs, addBtn, EdLook.C(addInk), C);
                x += w + P(6);
            }
            else addBtn = Rectangle.Empty;
            return x + P(10);
        }

        // Right-aligned groups: speed, size, quality and format. Returns where the leftmost label starts. Their names go
        // when the window is too narrow to hold them next to the buttons that start at minLeft.
        int PaintOptions(Graphics g, Palette pal, Rectangle r, Font f, int minLeft)
        {
            int leftmost = r.Right;
            string[][] groups = { Speeds, Sizes, Qualities, Formats };
            int[] sel = { Speed, OutSize, Quality, Format };
            string[] labels = { I18n.T("Velocidad", "Speed"), I18n.T("Tama\u00F1o", "Size"), I18n.T("Calidad", "Quality"), I18n.T("Formato", "Format") };
            Font fs = Fonts.Get(EdLook.Semibold, 11.5f * S), fr = Fonts.Get(EdLook.Text, 11.5f * S);
            const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int x = r.Right, h = P(24), y = P(7);
            // Names and roomy segments while they fit; then no names; then tighter segments.
            int named = 0, plain = 0;
            for (int gi = 0; gi < groups.Length; gi++)
            {
                int lw0 = TextKit.Measure(g, labels[gi], f, Size.Empty, TextFormatFlags.NoPadding).Width;
                named += P(4) + P(22) + P(8) + lw0;
                plain += P(4) + P(14);
                foreach (string it in groups[gi])
                {
                    int tw0 = TextKit.Measure(g, it, fs, Size.Empty, TextFormatFlags.NoPadding).Width;
                    named += tw0 + P(18);
                    plain += tw0 + P(18);
                }
            }
            int room = r.Right - minLeft - P(110);
            bool names = named <= room, roomy = names || plain <= room;
            int segPad = roomy ? P(18) : P(10), groupGap = roomy ? P(14) : P(8);
            for (int gi = groups.Length - 1; gi >= 0; gi--)
            {
                string[] items = groups[gi];
                int[] ws = new int[items.Length];
                int total = P(4);
                for (int i = 0; i < items.Length; i++)
                {
                    ws[i] = TextKit.Measure(g, items[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + segPad;
                    total += ws[i];
                }
                Rectangle box = new Rectangle(x - total, y, total, h);
                EdLook.Fill(g, box, P(7), EdLook.C(pal.Control));
                if (pal.High) EdHc.Track(g, box, P(7));
                segs[gi] = new Rectangle[items.Length];
                int sx = box.X + P(2);
                for (int i = 0; i < items.Length; i++)
                {
                    Rectangle sr = new Rectangle(sx, y + P(2), ws[i], h - P(4));
                    segs[gi][i] = sr;
                    bool on = i == sel[gi];
                    Color ink = EdLook.C(on ? pal.Label : pal.Label2);
                    if (pal.High) ink = EdHc.Segment(g, sr, P(5), on, gi == hotGroup && i == hotSeg, true);
                    else if (on)
                    {
                        if (!Ds.Dark) EdLook.Fill(g, new RectangleF(sr.X, sr.Y + 0.75f * S, sr.Width, sr.Height), P(5), Color.FromArgb(22, 0, 0, 0));
                        EdLook.Fill(g, sr, P(5), Ds.Dark ? Color.FromArgb(92, 255, 255, 255) : Color.White);
                    }
                    else if (gi == hotGroup && i == hotSeg) EdLook.Fill(g, sr, P(5), EdLook.C(pal.Control));
                    TextKit.Draw(g, items[i], on ? fs : fr, sr, ink, C);
                    sx += ws[i];
                }
                int lw = names ? TextKit.Measure(g, labels[gi], f, Size.Empty, TextFormatFlags.NoPadding).Width : 0;
                if (names)
                    TextKit.Draw(g, labels[gi], f, new Rectangle(box.X - lw - P(8), y, lw + P(2), h), EdLook.C(pal.Label2),
                                          TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                leftmost = box.X - (names ? lw + P(8) : 0);
                x = box.X - (names ? lw + P(8) : 0) - (names ? P(14) : groupGap);
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
            if (e.Button == MouseButtons.Right)
            {
                int rc = ClipIndexAt(e.Location);
                if (rc >= 0 && !playing) ClipMenu(rc, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (hoverClip >= 1 && hoverClip < Clips.Count && !playing && XRect(hoverClip).Contains(e.Location))
            {
                int gone = hoverClip;
                hoverClip = -1;
                Invalidate();
                if (ClipRemove != null) ClipRemove(gone);
                return;
            }
            if (addBtn.Contains(e.Location)) { if (AddClip != null) AddClip(); return; }
            if (playBtn.Contains(e.Location)) { if (PlayToggle != null) PlayToggle(); return; }
            if (soundBtn.Contains(e.Location)) { if (SoundClick != null) SoundClick(PointToScreen(new Point(soundBtn.X, soundBtn.Bottom + P(4)))); return; }
            if (splitBtn.Contains(e.Location)) { if (!playing) SplitAtPlayhead(); return; }
            if (cutBtn.Contains(e.Location)) { if (!playing) ToggleCut(); return; }
            int gi, si;
            if (SegAt(e.Location, out gi, out si))
            {
                if (gi == 0) Speed = si; else if (gi == 1) OutSize = si; else if (gi == 2) Quality = si; else Format = si;
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
                int hb = splitBtn.Contains(e.Location) ? 0 : cutBtn.Contains(e.Location) ? 1 : playBtn.Contains(e.Location) ? 2 : soundBtn.Contains(e.Location) ? 3 : addBtn.Contains(e.Location) ? 4 : -1;
                if (hb != hotBtn) { hotBtn = hb; Invalidate(new Rectangle(0, 0, Width, P(34))); }
                int hc = playing ? -1 : ClipIndexAt(e.Location);
                if (hc != hoverClip) { hoverClip = hc; Invalidate(Strip); }
                if (hc >= 1 && XRect(hc).Contains(e.Location)) { hot = (Grip)(-1); Cursor = Cursors.Hand; return; }
                if (hb >= 0) { hot = (Grip)(-1); Cursor = Cursors.Hand; return; }
                if (gi != hotGroup || si != hotSeg) { hotGroup = gi; hotSeg = si; Invalidate(new Rectangle(0, 0, Width, P(34))); }
                if (gi >= 0) { hot = (Grip)(-1); Cursor = Cursors.Hand; return; } // forces a cursor refresh on the way out
                Grip h = GripAt(e.Location);
                if (h != hot) { hot = h; Cursor = h == Grip.In || h == Grip.Out ? Cursors.SizeWE : h == Grip.Head ? Cursors.Hand : Cursors.Default; }
                return;
            }
            double t = T(e.X), min = Math.Min(0.3, Duration / 4);
            if ((drag == Grip.In || drag == Grip.Out) && !dragNoted) { dragNoted = true; Noting(); }
            if (drag == Grip.In) { In = Math.Min(t, Out - min); Playhead = In; }
            else if (drag == Grip.Out) { Out = Math.Max(t, In + min); Playhead = Out; }
            else Playhead = Math.Max(In, Math.Min(Out, t));
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hotGroup >= 0) { hotGroup = hotSeg = -1; Invalidate(); }
            if (hoverClip >= 0) { hoverClip = -1; Invalidate(Strip); }
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
            dragNoted = false;
            if (d == Grip.In || d == Grip.Out) { if (Trimmed != null) Trimmed(); }
            if (d != Grip.None && Seek != null) Seek(Playhead);
        }
    }
}
