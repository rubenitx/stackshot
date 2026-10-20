// Stackshot - The pages of a PDF, one under the other as in Preview: smooth scrolling, zoom around the pointer, pages
// drawn pixel for pixel at the zoom shown (and a sharper piece of the page when zoomed far in), text selection on the
// words the OCR found, and the marking tools.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Stackshot
{
    public enum PdfTool { Select, Highlight, Underline, Strike, Note, Ink, Zone }

    public sealed class PdfView : FrameworkElement
    {
        public const double Gap = 16, Pad = 28;
        const double MinZoom = 0.1, MaxZoom = 8;
        const long MaxPixels = 14L * 1000 * 1000;    // a whole page drawn bigger than this is shown in pieces

        public PdfEngine Engine { get; private set; }
        public PdfReader Reader { get; private set; }
        public PdfMarks Marks { get; private set; }
        public Dictionary<int, BitmapSource> Thumbs;   // small pages from the sidebar: shown while the real one comes

        double zoom = 1;
        int fit = 1;                  // 1 fit width, 2 whole page, 0 the zoom chosen
        double ox, oy;                // scroll offsets in DIPs
        double[] tops = new double[0];
        double contentW, contentH, widest;

        sealed class Shot { public BitmapSource Bitmap; public int Width; public double Zoom; }
        readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();
        readonly Dictionary<int, int> pending = new Dictionary<int, int>();   // page -> width asked of the engine
        Shot tile;
        int tilePage = -1;
        Rect tileSrc;
        bool planned;
        readonly DispatcherTimer settle;
        DateTime zoomedAt = DateTime.MinValue;

        public PdfTool Tool = PdfTool.Select;
        public Color Ink = PdfMarks.Colors[5];
        public Color Mark = PdfMarks.Colors[0];

        // Search: every match and the current one.
        public List<PdfHitPlace> Hits = new List<PdfHitPlace>();
        public int HitIndex = -1;

        public event Action ViewChanged;        // scrolled, zoomed or resized
        public event Action SelectionChanged;   // text, zone or mark picked (or nothing any more)
        public event Action<PdfMark, bool> NoteOpen;   // a note to write (true: just made)
        public event Action<int> TextWanted;    // a page whose text is needed now

        public PdfView()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            settle = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(140) };
            settle.Tick += delegate { settle.Stop(); Plan(); };
            SizeChanged += delegate { Relayout(true); };
            IsManipulationEnabled = true;
        }

        public void Attach(PdfEngine engine, PdfReader reader, PdfMarks marks)
        {
            Engine = engine;
            Reader = reader;
            Marks = marks;
            shots.Clear();
            pending.Clear();
            layered.Clear();
            tileLayered = null;
            tile = null;
            fit = 3;
            ox = oy = 0;
            ClearSelection(false);
            Relayout(true);
        }

        // ---- Geometry

        public double Zoom { get { return zoom; } }
        public int Fit { get { return fit; } }
        public int Pages { get { return Engine == null ? 0 : Engine.Pages; } }

        double Dpi { get { try { return VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { return 1; } } }

        void Relayout(bool keep)
        {
            if (Engine == null) { InvalidateVisual(); return; }
            // Keep what is at the top of the view where it is while the zoom or the size changes.
            int anchorPage = -1;
            double anchorY = 0;
            if (keep && tops.Length == Pages && Pages > 0)
            {
                anchorPage = PageNear(oy + Pad);
                anchorY = (oy - tops[anchorPage]) / Math.Max(1, Engine.Sizes[anchorPage].Height * zoom);
            }
            if (fit != 0 && ActualWidth > 0) zoom = FitZoom(fit);
            Layout();
            if (anchorPage >= 0) oy = tops[anchorPage] + anchorY * Engine.Sizes[anchorPage].Height * zoom;
            Clamp();
            Changed();
        }

        void Layout()
        {
            int n = Pages;
            tops = new double[n];
            widest = 0;
            double y = Pad;
            for (int i = 0; i < n; i++)
            {
                tops[i] = y;
                y += Engine.Sizes[i].Height * zoom + Gap;
                widest = Math.Max(widest, Engine.Sizes[i].Width * zoom);
            }
            contentH = n == 0 ? 0 : y - Gap + Pad;
            contentW = Math.Max(ActualWidth, widest + Pad * 2);
        }

        double FitZoom(int mode)
        {
            if (Pages == 0 || ActualWidth <= 0) return 1;
            double w = 0, h = 0;
            int at = CurrentPage;
            for (int i = 0; i < Pages; i++) w = Math.Max(w, Engine.Sizes[i].Width);
            h = Engine.Sizes[Math.Max(0, Math.Min(Pages - 1, at))].Height;
            double byWidth = (ActualWidth - Pad * 2) / Math.Max(1, w);
            // 3: as it opens, the width of the window but never bigger than one and a half times (a huge window would
            // otherwise make the text enormous); 1: the width, as asked.
            if (mode == 3) return Clamp(Math.Min(byWidth, 1.5), MinZoom, MaxZoom);
            if (mode == 1) return Clamp(Math.Min(byWidth, 4), MinZoom, MaxZoom);
            double byHeight = (ActualHeight - Pad * 2) / Math.Max(1, h);
            return Clamp(Math.Min(byWidth, byHeight), MinZoom, MaxZoom);
        }

        static double Clamp(double v, double a, double b) { return v < a ? a : v > b ? b : v; }

        void Clamp()
        {
            double maxY = Math.Max(0, contentH - ActualHeight), maxX = Math.Max(0, contentW - ActualWidth);
            oy = Clamp(oy, 0, maxY);
            ox = Clamp(ox, 0, maxX);
        }

        // Where a page sits in the view (DIPs, already scrolled), snapped to whole device pixels so it is drawn 1:1.
        public Rect PageRect(int i)
        {
            Size s = Engine.Sizes[i];
            double w = s.Width * zoom, h = s.Height * zoom, x = (contentW - w) / 2 - ox, y = tops[i] - oy;
            double d = Dpi;
            return new Rect(Math.Round(x * d) / d, Math.Round(y * d) / d, Math.Round(w * d) / d, Math.Round(h * d) / d);
        }

        int PageNear(double contentY)
        {
            int lo = 0, hi = Pages - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (tops[mid] <= contentY) lo = mid; else hi = mid - 1;
            }
            return Math.Max(0, lo);
        }

        // The page most of the view shows: the one under a point a third of the way down.
        public int CurrentPage
        {
            get
            {
                if (Pages == 0 || tops.Length != Pages) return 0;
                return PageNear(oy + ActualHeight / 3);
            }
        }

        void VisibleRange(out int first, out int last)
        {
            first = PageNear(oy);
            last = PageNear(oy + ActualHeight);
            if (Pages == 0) { first = 0; last = -1; }
        }

        // The page under a point of the view and where on it (fractions), or -1.
        public int PageAt(Point p, out Point f)
        {
            f = new Point();
            if (Pages == 0) return -1;
            int i = PageNear(p.Y + oy);
            for (int k = Math.Max(0, i - 1); k <= Math.Min(Pages - 1, i + 1); k++)
            {
                Rect r = PageRect(k);
                if (r.Contains(p)) { f = new Point((p.X - r.X) / r.Width, (p.Y - r.Y) / r.Height); return k; }
            }
            return -1;
        }

        // The same, for a point that may be off the page (a drag that leaves it): clamped to that page.
        Point OnPage(int page, Point p)
        {
            Rect r = PageRect(page);
            return new Point(Clamp((p.X - r.X) / r.Width, 0, 1), Clamp((p.Y - r.Y) / r.Height, 0, 1));
        }

        // ---- Moving around

        public void GoTo(int page, bool smooth)
        {
            if (Pages == 0) return;
            page = Math.Max(0, Math.Min(Pages - 1, page));
            ScrollTo(tops[page] - Pad / 2, smooth);
        }

        // Brings a box of a page (fractions) into view, a little above the middle.
        public void Reveal(int page, Rect box, bool smooth)
        {
            if (page < 0 || page >= Pages) return;
            Size s = Engine.Sizes[page];
            double y = tops[page] + box.Y * s.Height * zoom, h = box.Height * s.Height * zoom;
            if (y - oy >= 40 && y + h - oy <= ActualHeight - 40) return;
            ScrollTo(y - ActualHeight * 0.35, smooth);
        }

        double targetY;
        bool gliding;

        public void ScrollTo(double y, bool smooth)
        {
            targetY = Clamp(y, 0, Math.Max(0, contentH - ActualHeight));
            if (!smooth) { StopGlide(); oy = targetY; Changed(); return; }
            if (!gliding) { gliding = true; CompositionTarget.Rendering += Glide; }
        }

        public void ScrollBy(double dy, bool smooth)
        {
            ScrollTo((gliding ? targetY : oy) + dy, smooth);
        }

        void Glide(object sender, EventArgs e)
        {
            double d = targetY - oy;
            if (Math.Abs(d) < 0.5) { oy = targetY; StopGlide(); Changed(); return; }
            oy += d * 0.28;
            Changed();
        }

        void StopGlide()
        {
            if (!gliding) return;
            gliding = false;
            CompositionTarget.Rendering -= Glide;
        }

        public void SetZoom(double z, Point? anchor)
        {
            fit = 0;
            ZoomAround(Clamp(z, MinZoom, MaxZoom), anchor ?? new Point(ActualWidth / 2, ActualHeight / 3));
        }

        public void SetFit(int mode)
        {
            fit = mode;
            ZoomAround(FitZoom(mode), new Point(ActualWidth / 2, 0));
            if (mode == 2) GoTo(CurrentPage, false);
        }

        public void ZoomStep(int dir)
        {
            double[] steps = { 0.25, 0.33, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5, 6, 8 };
            double z = zoom;
            if (dir > 0) { foreach (double s in steps) if (s > z + 0.001) { SetZoom(s, null); return; } SetZoom(MaxZoom, null); }
            else { for (int i = steps.Length - 1; i >= 0; i--) if (steps[i] < z - 0.001) { SetZoom(steps[i], null); return; } SetZoom(MinZoom, null); }
        }

        void ZoomAround(double z, Point anchor)
        {
            if (Pages == 0) return;
            StopGlide();
            Point f;
            int page = PageAt(anchor, out f);
            if (page < 0)
            {
                page = PageNear(anchor.Y + oy);
                Rect r0 = PageRect(page);
                f = new Point((anchor.X - r0.X) / r0.Width, (anchor.Y - r0.Y) / r0.Height);
            }
            zoom = z;
            Layout();
            Size s = Engine.Sizes[page];
            double pw = s.Width * zoom, ph = s.Height * zoom;
            ox = (contentW - pw) / 2 + f.X * pw - anchor.X;
            oy = tops[page] + f.Y * ph - anchor.Y;
            Clamp();
            zoomedAt = DateTime.UtcNow;
            Changed();
        }

        void Changed()
        {
            InvalidateVisual();
            SchedulePlan();
            Action h = ViewChanged;
            if (h != null) h();
        }

        // ---- Getting pages drawn

        void SchedulePlan()
        {
            // While zooming, the pages already drawn are stretched; the sharp ones come once it stops.
            if ((DateTime.UtcNow - zoomedAt).TotalMilliseconds < 140) { settle.Stop(); settle.Start(); return; }
            if (planned) return;
            planned = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { planned = false; Plan(); }));
        }

        public void Plan()
        {
            if (Engine == null || Pages == 0 || ActualWidth <= 0) return;
            int first, last;
            VisibleRange(out first, out last);
            double d = Dpi;
            for (int i = Math.Max(0, first - 1); i <= Math.Min(Pages - 1, last + 1); i++)
            {
                bool seen = i >= first && i <= last;
                Size s = Engine.Sizes[i];
                int w = (int)Math.Round(s.Width * zoom * d), h = (int)Math.Round(s.Height * zoom * d);
                double k = 1;
                if ((long)w * h > MaxPixels) k = Math.Sqrt(MaxPixels / ((double)w * h));
                int tw = Math.Max(1, (int)(w * k)), th = Math.Max(1, (int)(h * k));
                Shot have;
                if (shots.TryGetValue(i, out have) && Math.Abs(have.Width - tw) <= 1) continue;
                // One request per page at a time: a newer size replaces the one still waiting in the engine.
                int was;
                if (pending.TryGetValue(i, out was) && was == tw) continue;
                pending[i] = tw;
                int page = i;
                double z = zoom;
                PdfEngine e = Engine;
                Engine.Render(i, tw, th, null, seen ? 0 : 2, "p" + i, Dispatcher, delegate(BitmapSource b)
                {
                    int now;
                    if (pending.TryGetValue(page, out now) && now == tw) pending.Remove(page);
                    if (b == null || Engine != e) return;
                    shots[page] = new Shot { Bitmap = b, Width = tw, Zoom = z };
                    InvalidateVisual();
                });
            }
            PlanTile(first, last, d);
            Evict(first, last);
        }

        // Zoomed in past what a whole page may weigh: the part in view is drawn again at full sharpness.
        void PlanTile(int first, int last, double d)
        {
            int page = -1;
            for (int i = first; i <= last; i++)
            {
                Size s = Engine.Sizes[i];
                if ((long)(s.Width * zoom * d) * (long)(s.Height * zoom * d) > MaxPixels) { page = i; break; }
            }
            if (page < 0) { tile = null; tilePage = -1; return; }
            Rect r = PageRect(page), view = new Rect(0, 0, ActualWidth, ActualHeight);
            Rect part = Rect.Intersect(r, view);
            if (part.IsEmpty || part.Width < 2 || part.Height < 2) return;
            Rect src = new Rect((part.X - r.X) / zoom, (part.Y - r.Y) / zoom, part.Width / zoom, part.Height / zoom);
            if (tile != null && tilePage == page && Math.Abs(tile.Zoom - zoom) < 1e-6 && Near(tileSrc, src)) return;
            int w = Math.Max(1, (int)Math.Round(part.Width * d)), h = Math.Max(1, (int)Math.Round(part.Height * d));
            double z = zoom;
            Engine.Render(page, w, h, src, 0, "tile", Dispatcher, delegate(BitmapSource b)
            {
                if (b == null) return;
                tile = new Shot { Bitmap = b, Width = w, Zoom = z };
                tilePage = page;
                tileSrc = src;
                InvalidateVisual();
            });
        }

        static bool Near(Rect a, Rect b) { return Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5; }

        // Pages far from view give their drawings back (a long PDF would otherwise keep every page it showed).
        void Evict(int first, int last)
        {
            List<int> drop = new List<int>();
            foreach (KeyValuePair<int, Shot> kv in shots)
                if (kv.Key < first - 3 || kv.Key > last + 3) drop.Add(kv.Key);
            foreach (int p in drop) { shots.Remove(p); layered.Remove(p); }
        }

        // A page drawn at this size right now (the export and the capture use it when it is sharp enough).
        public void Forget() { shots.Clear(); layered.Clear(); tileLayered = null; tile = null; InvalidateVisual(); SchedulePlan(); }

        // ---- Drawing

        static readonly Color HitTint = Color.FromRgb(255, 236, 140), HitNowTint = Color.FromRgb(255, 186, 84), SelectTint = Color.FromRgb(181, 214, 255);

        static Brush Frozen(Color c) { SolidColorBrush b = new SolidColorBrush(c); b.Freeze(); return b; }

        // ---- What sits behind the ink: highlights, search matches and the selection, multiplied into a copy of the page
        // drawing as a highlighter would, so the words stay black. Made again only when one of them changes.

        sealed class Layered { public BitmapSource From, Image; public long Key; }
        readonly Dictionary<int, Layered> layered = new Dictionary<int, Layered>();
        Layered tileLayered;

        List<KeyValuePair<Rect, Color>> Tints(int page)
        {
            List<KeyValuePair<Rect, Color>> o = new List<KeyValuePair<Rect, Color>>();
            if (Marks != null)
                foreach (PdfMark m in Marks.On(page))
                    if (m.Kind == PdfMarkKind.Highlight)
                        foreach (Rect b in m.Boxes) o.Add(new KeyValuePair<Rect, Color>(PdfMarks.Grown(b, 0.06), PdfMarks.Tint(m.Color)));
            PdfPageText t = Reader == null ? null : Reader.Get(page);
            if (t == null) return o;
            for (int h = 0; h < Hits.Count; h++)
            {
                if (Hits[h].Page != page) continue;
                foreach (Rect b in t.LineBoxes(Hits[h].First, Hits[h].Last)) o.Add(new KeyValuePair<Rect, Color>(PdfMarks.Grown(b, 0.12), h == HitIndex ? HitNowTint : HitTint));
            }
            if (selPage == page && selA >= 0 && selB >= 0)
            {
                bool marking = dragKind == Drag.Text && Tool != PdfTool.Select;
                if (!marking || Tool == PdfTool.Highlight)
                    foreach (Rect b in t.LineBoxes(selA, selB)) o.Add(new KeyValuePair<Rect, Color>(PdfMarks.Grown(b, marking ? 0.06 : 0.08), marking ? PdfMarks.Tint(Mark) : SelectTint));
            }
            return o;
        }

        static long KeyOf(List<KeyValuePair<Rect, Color>> tints)
        {
            unchecked
            {
                long k = 17;
                foreach (KeyValuePair<Rect, Color> t in tints)
                    k = k * 31 + t.Key.X.GetHashCode() * 3 + t.Key.Y.GetHashCode() * 5 + t.Key.Width.GetHashCode() * 7 + t.Key.Height.GetHashCode() * 11 + t.Value.GetHashCode();
                return k;
            }
        }

        // The page drawing with its tints; part: where that drawing sits in the page (fractions), all of it unless a tile.
        BitmapSource Tinted(int page, BitmapSource from, Rect part, ref Layered cache)
        {
            List<KeyValuePair<Rect, Color>> tints = Tints(page);
            if (tints.Count == 0) return from;
            long key = KeyOf(tints) ^ part.GetHashCode();
            if (cache != null && cache.From == from && cache.Key == key) return cache.Image;
            WriteableBitmap w = new WriteableBitmap(from);
            foreach (KeyValuePair<Rect, Color> t in tints)
            {
                Rect f = t.Key;
                double x0 = (f.X - part.X) / part.Width * w.PixelWidth, y0 = (f.Y - part.Y) / part.Height * w.PixelHeight;
                double x1 = (f.Right - part.X) / part.Width * w.PixelWidth, y1 = (f.Bottom - part.Y) / part.Height * w.PixelHeight;
                if (x1 < 0 || y1 < 0 || x0 > w.PixelWidth || y0 > w.PixelHeight) continue;
                PdfMarks.Multiply(w, new Int32Rect((int)Math.Floor(x0), (int)Math.Floor(y0), (int)Math.Ceiling(x1 - Math.Floor(x0)), (int)Math.Ceiling(y1 - Math.Floor(y0))), t.Value);
            }
            w.Freeze();
            cache = new Layered { From = from, Image = w, Key = key };
            return w;
        }

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            dc.DrawRectangle(new SolidColorBrush(pal.Dark ? Color.FromRgb(24, 24, 26) : Color.FromRgb(226, 226, 230)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (Engine == null || Pages == 0) return;
            int first, last;
            VisibleRange(out first, out last);
            Brush shadow1 = Frozen(Color.FromArgb(pal.Dark ? (byte)70 : (byte)18, 0, 0, 0)), shadow2 = Frozen(Color.FromArgb(pal.Dark ? (byte)40 : (byte)10, 0, 0, 0));
            for (int i = first; i <= last; i++)
            {
                Rect r = PageRect(i);
                dc.DrawRoundedRectangle(shadow2, null, new Rect(r.X - 2, r.Y, r.Width + 4, r.Height + 5), 4, 4);
                dc.DrawRectangle(shadow1, null, new Rect(r.X - 0.5, r.Y + 0.5, r.Width + 1, r.Height + 1.5));
                dc.DrawRectangle(Brushes.White, null, r);
                Shot s;
                BitmapSource th;
                if (shots.TryGetValue(i, out s))
                {
                    Layered l;
                    layered.TryGetValue(i, out l);
                    Layered before = l;
                    dc.DrawImage(Tinted(i, s.Bitmap, new Rect(0, 0, 1, 1), ref l), r);
                    if (l != before) layered[i] = l;
                }
                else if (Thumbs != null && Thumbs.TryGetValue(i, out th)) dc.DrawImage(th, r);
                if (tile != null && tilePage == i && Math.Abs(tile.Zoom - zoom) < 1e-6)
                {
                    Size ps = Engine.Sizes[i];
                    Rect part = new Rect(tileSrc.X / ps.Width, tileSrc.Y / ps.Height, tileSrc.Width / ps.Width, tileSrc.Height / ps.Height);
                    dc.DrawImage(Tinted(i, tile.Bitmap, part, ref tileLayered), new Rect(r.X + tileSrc.X * zoom, r.Y + tileSrc.Y * zoom, tileSrc.Width * zoom, tileSrc.Height * zoom));
                }
                if (Marks != null)
                    foreach (PdfMark m in Marks.On(i)) PdfMarks.Draw(dc, m, r, selected != null && m.Id == selected, pal);
                DrawSelection(dc, i, r, pal);
            }
            DrawBar(dc);
        }

        // Over the page: the stroke or the underline being made, and a zone (the selection and highlights are tints).
        void DrawSelection(DrawingContext dc, int page, Rect r, Palette pal)
        {
            if (dragKind == Drag.Ink && dragPage == page && stroke != null)
            {
                PdfMark live = new PdfMark { Kind = PdfMarkKind.Ink, Color = Ink, Width = InkWidth };
                live.Strokes.Add(stroke);
                PdfMarks.Draw(dc, live, r, false, pal);
            }
            if (selPage == page && Reader != null && dragKind == Drag.Text && (Tool == PdfTool.Underline || Tool == PdfTool.Strike))
            {
                PdfPageText t = Reader.Get(page);
                if (t != null && selA >= 0 && selB >= 0)
                {
                    PdfMark live = new PdfMark { Kind = KindFor(Tool), Color = Mark };
                    live.Boxes.AddRange(t.LineBoxes(selA, selB));
                    PdfMarks.Draw(dc, live, r, false, pal);
                }
            }
            if (zonePage == page && !zone.IsEmpty)
            {
                Rect z = new Rect(r.X + zone.X * r.Width, r.Y + zone.Y * r.Height, zone.Width * r.Width, zone.Height * r.Height);
                Pen dash = new Pen(new SolidColorBrush(pal.Accent), 1.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
                dash.Freeze();
                dc.DrawRectangle(Frozen(Ds.WithAlpha(pal.Accent, 0.1)), dash, z);
            }
        }

        public static PdfMarkKind KindFor(PdfTool t)
        {
            return t == PdfTool.Underline ? PdfMarkKind.Underline : t == PdfTool.Strike ? PdfMarkKind.Strike : PdfMarkKind.Highlight;
        }

        public double InkWidth = 0.004;

        // ---- Selection: words of one page, a zone of one page, or a mark

        public int selPage = -1, selA = -1, selB = -1;
        public int zonePage = -1;
        public Rect zone = Rect.Empty;
        public string selected;      // id of the mark picked

        public bool HasText { get { return selPage >= 0 && selA >= 0 && selB >= 0; } }
        public bool HasZone { get { return zonePage >= 0 && !zone.IsEmpty && zone.Width > 0.002 && zone.Height > 0.002; } }
        public PdfMark SelectedMark { get { return selected == null || Marks == null ? null : Marks.Find(selected); } }

        public string SelectedText
        {
            get
            {
                if (HasText) { PdfPageText t = Reader.Get(selPage); return t == null ? "" : t.Between(selA, selB); }
                if (HasZone)
                {
                    PdfPageText t = Reader.Get(zonePage);
                    int a, b;
                    if (t != null && t.Inside(zone, out a, out b)) return t.Between(a, b);
                }
                PdfMark m = SelectedMark;
                if (m != null) return m.Kind == PdfMarkKind.Note ? m.Text : m.Text;
                return "";
            }
        }

        public void ClearSelection(bool tell)
        {
            bool had = selPage >= 0 || zonePage >= 0 || selected != null;
            selPage = selA = selB = -1;
            zonePage = -1;
            zone = Rect.Empty;
            selected = null;
            InvalidateVisual();
            if (had && tell) Tell();
        }

        public void SelectWords(int page, int a, int b)
        {
            ClearSelection(false);
            selPage = page; selA = a; selB = b;
            InvalidateVisual();
            Tell();
        }

        public void SelectMark(PdfMark m)
        {
            ClearSelection(false);
            selected = m == null ? null : m.Id;
            InvalidateVisual();
            Tell();
        }

        void Tell() { Action h = SelectionChanged; if (h != null) h(); }

        // Where the selection is in the view (for the bar that floats over it), or Rect.Empty.
        public Rect SelectionRect
        {
            get
            {
                if (HasText)
                {
                    PdfPageText t = Reader.Get(selPage);
                    if (t == null) return Rect.Empty;
                    Rect u = Rect.Empty;
                    foreach (Rect b in t.LineBoxes(selA, selB)) u = u.IsEmpty ? b : Rect.Union(u, b);
                    return ToView(selPage, u);
                }
                if (HasZone) return ToView(zonePage, zone);
                PdfMark m = SelectedMark;
                if (m != null)
                {
                    Rect pr = PageRect(m.Page);
                    if (m.Kind == PdfMarkKind.Note) return new Rect(pr.X + m.At.X * pr.Width, pr.Y + m.At.Y * pr.Height, PdfMarks.NoteSize, PdfMarks.NoteSize);
                    return ToView(m.Page, m.Bounds(pr.Width, pr.Height));
                }
                return Rect.Empty;
            }
        }

        public Rect ToView(int page, Rect f)
        {
            Rect r = PageRect(page);
            return new Rect(r.X + f.X * r.Width, r.Y + f.Y * r.Height, f.Width * r.Width, f.Height * r.Height);
        }

        public PdfMark MarkAt(Point p)
        {
            if (Marks == null) return null;
            Point f;
            int page = PageAt(p, out f);
            PdfMark best = null;
            for (int k = Marks.All.Count - 1; k >= 0; k--)
            {
                PdfMark m = Marks.All[k];
                if (page >= 0 && m.Page != page && m.Kind != PdfMarkKind.Note) continue;
                Rect pr = PageRect(m.Page);
                if (m.Kind == PdfMarkKind.Note)
                {
                    Rect icon = new Rect(pr.X + m.At.X * pr.Width, pr.Y + m.At.Y * pr.Height, PdfMarks.NoteSize, PdfMarks.NoteSize);
                    icon.Inflate(3, 3);
                    if (icon.Contains(p)) return m;
                    continue;
                }
                if (m.Page != page) continue;
                if (m.Kind == PdfMarkKind.Ink)
                {
                    double tol = Math.Max(5, m.Width * pr.Width);
                    foreach (List<Point> s in m.Strokes)
                        for (int i = 0; i < s.Count; i++)
                        {
                            Point a = new Point(pr.X + s[i].X * pr.Width, pr.Y + s[i].Y * pr.Height);
                            Point b = i + 1 < s.Count ? new Point(pr.X + s[i + 1].X * pr.Width, pr.Y + s[i + 1].Y * pr.Height) : a;
                            if (Distance(p, a, b) <= tol) return m;
                        }
                    continue;
                }
                foreach (Rect b in m.Boxes)
                {
                    Rect r = PdfMarks.Place(b, pr, 0.15);
                    if (r.Contains(p) && best == null) best = m;
                }
            }
            return best;
        }

        static double Distance(Point p, Point a, Point b)
        {
            Vector ab = b - a, ap = p - a;
            double len = ab.LengthSquared;
            double t = len < 1e-9 ? 0 : Clamp((ap.X * ab.X + ap.Y * ab.Y) / len, 0, 1);
            return (p - (a + ab * t)).Length;
        }

        // ---- Mouse

        enum Drag { None, Text, Zone, Ink, Pan, Bar, Move }
        Drag dragKind;
        string moving;              // the note being moved
        Point moveFrom, moveAt;
        int dragPage = -1, dragWord = -1;
        Point dragStart, dragFrom, lastMouse;
        double panX, panY;
        List<Point> stroke;
        DispatcherTimer edge;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point p = e.GetPosition(this);
            lastMouse = p;
            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
            {
                StartPan(p);
                e.Handled = true;
                return;
            }
            if (e.ChangedButton != MouseButton.Left || Engine == null) return;
            if (OverBar(p)) { dragKind = Drag.Bar; dragFrom = p; panY = oy; CaptureMouse(); e.Handled = true; return; }
            Point f;
            int page = PageAt(p, out f);
            PdfPageText t = page >= 0 && Reader != null ? Reader.Get(page) : null;
            if (page >= 0 && t == null) { Action<int> want = TextWanted; if (want != null) want(page); }

            if (Tool == PdfTool.Select && e.ClickCount >= 2 && t != null)
            {
                int w = t.Hit(f, false);
                if (w >= 0)
                {
                    if (e.ClickCount == 2) SelectWords(page, w, w);
                    else
                    {
                        int a = w, b = w;
                        while (a > 0 && t.Words[a - 1].Line == t.Words[w].Line) a--;
                        while (b < t.Words.Count - 1 && t.Words[b + 1].Line == t.Words[w].Line) b++;
                        SelectWords(page, a, b);
                    }
                    e.Handled = true;
                    return;
                }
            }

            if (Tool == PdfTool.Select || Tool == PdfTool.Note)
            {
                PdfMark m = MarkAt(p);
                if (m != null)
                {
                    SelectMark(m);
                    if (m.Kind == PdfMarkKind.Note)
                    {
                        // A note follows the pointer when dragged (one of the user's); a click without moving opens it.
                        dragKind = Drag.Move;
                        dragStart = p;
                        moving = m.Id;
                        moveFrom = m.At;
                        moveAt = m.At;
                        CaptureMouse();
                    }
                    e.Handled = true;
                    return;
                }
            }
            ClearSelection(true);
            if (page < 0) { StartPan(p); e.Handled = true; return; }
            dragPage = page;
            dragStart = p;
            switch (Tool)
            {
                case PdfTool.Note:
                    {
                        PdfMark n = new PdfMark { Kind = PdfMarkKind.Note, Page = page, Color = Mark, Text = "" };
                        Rect pr = PageRect(page);
                        n.At = new Point(Clamp(f.X, 0, 1 - PdfMarks.NoteSize / pr.Width), Clamp(f.Y, 0, 1 - PdfMarks.NoteSize / pr.Height));
                        Marks.Add(n);
                        SelectMark(Marks.Find(n.Id));
                        Action<PdfMark, bool> open = NoteOpen;
                        if (open != null) open(Marks.Find(n.Id), true);
                        e.Handled = true;
                        return;
                    }
                case PdfTool.Ink:
                    dragKind = Drag.Ink;
                    stroke = new List<Point> { f };
                    break;
                case PdfTool.Zone:
                    dragKind = Drag.Zone;
                    zonePage = page;
                    zone = new Rect(f, new Size(0, 0));
                    break;
                default:
                    int word = t != null ? t.Hit(f, false) : -1;
                    if (word >= 0)
                    {
                        dragKind = Drag.Text;
                        dragWord = word;
                        selPage = page; selA = selB = word;
                    }
                    else
                    {
                        // No words there (an image, a scan, text not read yet): a zone, which a marking tool turns
                        // into a highlighted area.
                        dragKind = Drag.Zone;
                        zonePage = page;
                        zone = new Rect(f, new Size(0, 0));
                    }
                    break;
            }
            CaptureMouse();
            StartEdge();
            InvalidateVisual();
            e.Handled = true;
        }

        void StartPan(Point p)
        {
            dragKind = Drag.Pan;
            dragFrom = p;
            panX = ox; panY = oy;
            StopGlide();
            CaptureMouse();
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            lastMouse = p;
            if (dragKind == Drag.None) { HoverCursor(p); ShowBar(p.X > ActualWidth - 16); return; }
            Track(p);
        }

        void Track(Point p)
        {
            switch (dragKind)
            {
                case Drag.Pan:
                    ox = panX - (p.X - dragFrom.X);
                    oy = panY - (p.Y - dragFrom.Y);
                    Clamp();
                    Changed();
                    return;
                case Drag.Bar:
                    {
                        double track = Math.Max(1, ActualHeight - BarLength());
                        oy = panY + (p.Y - dragFrom.Y) * Math.Max(0, contentH - ActualHeight) / track;
                        Clamp();
                        Changed();
                        return;
                    }
                case Drag.Text:
                    {
                        PdfPageText t = Reader.Get(dragPage);
                        if (t == null) return;
                        int w = t.Hit(OnPage(dragPage, p), true);
                        if (w < 0) return;
                        selA = Math.Min(dragWord, w);
                        selB = Math.Max(dragWord, w);
                        InvalidateVisual();
                        return;
                    }
                case Drag.Zone:
                    {
                        Point a = OnPage(dragPage, dragStart), b = OnPage(dragPage, p);
                        zone = new Rect(a, b);
                        InvalidateVisual();
                        return;
                    }
                case Drag.Ink:
                    {
                        Point f = OnPage(dragPage, p);
                        Point last = stroke[stroke.Count - 1];
                        Rect pr = PageRect(dragPage);
                        if (Math.Abs(f.X - last.X) * pr.Width + Math.Abs(f.Y - last.Y) * pr.Height >= 1.5) stroke.Add(f);
                        InvalidateVisual();
                        return;
                    }
                case Drag.Move:
                    {
                        PdfMark m = Marks == null ? null : Marks.Find(moving);
                        if (m == null || m.InFile || (p - dragStart).Length < 4) return;
                        Rect pr = PageRect(m.Page);
                        double nx = moveFrom.X + (p.X - dragStart.X) / pr.Width, ny = moveFrom.Y + (p.Y - dragStart.Y) / pr.Height;
                        moveAt = new Point(Clamp(nx, 0, 1 - PdfMarks.NoteSize / pr.Width), Clamp(ny, 0, 1 - PdfMarks.NoteSize / pr.Height));
                        m.At = moveAt;   // shown where it goes; written for good (undoable) when it is let go
                        Cursor = Cursors.SizeAll;
                        InvalidateVisual();
                        return;
                    }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragKind == Drag.None) return;
            Drag kind = dragKind;
            dragKind = Drag.None;
            StopEdge();
            ReleaseMouseCapture();
            Cursor = null;
            Point p = e.GetPosition(this);
            switch (kind)
            {
                case Drag.Text:
                    if (Tool == PdfTool.Select) { Tell(); break; }
                    MarkWords(KindFor(Tool));
                    break;
                case Drag.Zone:
                    if ((p - dragStart).Length < 4) { ClearSelection(true); break; }
                    if (Tool == PdfTool.Highlight || Tool == PdfTool.Underline || Tool == PdfTool.Strike) { MarkZone(KindFor(Tool)); break; }
                    Tell();
                    break;
                case Drag.Ink:
                    if (stroke != null && stroke.Count > 0)
                    {
                        PdfMark m = new PdfMark { Kind = PdfMarkKind.Ink, Page = dragPage, Color = Ink, Width = InkWidth };
                        m.Strokes.Add(Simplify(stroke, 0.0008));
                        Marks.Add(m);
                    }
                    stroke = null;
                    InvalidateVisual();
                    break;
                case Drag.Move:
                    {
                        PdfMark m = Marks == null ? null : Marks.Find(moving);
                        moving = null;
                        if (m == null) break;
                        if (m.At == moveFrom)
                        {
                            Action<PdfMark, bool> open = NoteOpen;
                            if (open != null) open(m, false);
                            break;
                        }
                        // Put back, then moved through the marks, so Ctrl+Z brings it back to where it was.
                        PdfMark moved = m.Clone();
                        m.At = moveFrom;
                        Marks.Update(moved);
                        SelectMark(Marks.Find(moved.Id));
                        break;
                    }
            }
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (dragKind == Drag.None) return;
            if (dragKind == Drag.Move && moving != null && Marks != null)
            {
                PdfMark m = Marks.Find(moving);
                if (m != null) m.At = moveFrom;
                moving = null;
            }
            dragKind = Drag.None;
            stroke = null;
            StopEdge();
            Cursor = null;
            InvalidateVisual();
        }

        // The words selected become a mark of that kind (the selection goes: the mark shows instead).
        public PdfMark MarkWords(PdfMarkKind kind)
        {
            if (!HasText) return null;
            PdfPageText t = Reader.Get(selPage);
            if (t == null) return null;
            PdfMark m = new PdfMark { Kind = kind, Page = selPage, Color = Mark, Text = t.Between(selA, selB) };
            m.Boxes.AddRange(t.LineBoxes(selA, selB));
            Marks.Add(m);
            ClearSelection(true);
            return m;
        }

        // A zone marked by hand: a highlighted area, or a line along its bottom (underline) or middle (strike-out).
        public PdfMark MarkZone(PdfMarkKind kind)
        {
            if (!HasZone) return null;
            PdfMark m = new PdfMark { Kind = kind, Page = zonePage, Color = Mark };
            PdfPageText t = Reader == null ? null : Reader.Get(zonePage);
            int a, b;
            if (t != null && t.Inside(zone, out a, out b)) { m.Boxes.AddRange(t.LineBoxes(a, b)); m.Text = t.Between(a, b); }
            else m.Boxes.Add(zone);
            Marks.Add(m);
            ClearSelection(true);
            return m;
        }

        // Fewer points for the same stroke (Ramer-Douglas-Peucker on page fractions).
        static List<Point> Simplify(List<Point> pts, double eps)
        {
            if (pts.Count < 3) return new List<Point>(pts);
            bool[] keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            Stack<int[]> todo = new Stack<int[]>();
            todo.Push(new int[] { 0, pts.Count - 1 });
            while (todo.Count > 0)
            {
                int[] r = todo.Pop();
                double far = 0;
                int at = -1;
                for (int i = r[0] + 1; i < r[1]; i++)
                {
                    double d = Distance(pts[i], pts[r[0]], pts[r[1]]);
                    if (d > far) { far = d; at = i; }
                }
                if (at >= 0 && far > eps) { keep[at] = true; todo.Push(new int[] { r[0], at }); todo.Push(new int[] { at, r[1] }); }
            }
            List<Point> o = new List<Point>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) o.Add(pts[i]);
            return o;
        }

        void HoverCursor(Point p)
        {
            if (Engine == null) { Cursor = null; return; }
            if (OverBar(p)) { Cursor = Cursors.Arrow; return; }
            if ((Tool == PdfTool.Select || Tool == PdfTool.Note) && MarkAt(p) != null) { Cursor = Cursors.Hand; return; }
            Point f;
            int page = PageAt(p, out f);
            if (page < 0) { Cursor = null; return; }
            switch (Tool)
            {
                case PdfTool.Ink: case PdfTool.Zone: case PdfTool.Note: Cursor = Cursors.Cross; return;
            }
            PdfPageText t = Reader == null ? null : Reader.Get(page);
            Cursor = t != null && t.Hit(f, false) >= 0 ? Cursors.IBeam : Tool == PdfTool.Select ? null : Cursors.Cross;
        }

        // While a selection is dragged against the top or bottom edge, the pages scroll under it.
        void StartEdge()
        {
            if (edge == null)
            {
                edge = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
                edge.Tick += delegate
                {
                    if (dragKind != Drag.Text && dragKind != Drag.Zone && dragKind != Drag.Ink) { edge.Stop(); return; }
                    double y = lastMouse.Y, dy = 0;
                    if (y < 24) dy = -(24 - y) * 0.9;
                    else if (y > ActualHeight - 24) dy = (y - (ActualHeight - 24)) * 0.9;
                    if (dy == 0) return;
                    oy += dy;
                    Clamp();
                    Changed();
                    Track(lastMouse);
                };
            }
            edge.Start();
        }

        void StopEdge() { if (edge != null) edge.Stop(); }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Engine == null) return;
            Point p = e.GetPosition(this);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                // A notch is about 10 %; a precision touchpad's pinch sends smaller steps, which zoom as finely.
                SetZoom(zoom * Math.Pow(1.1, e.Delta / 120.0), p);
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                ox -= e.Delta * 0.8;
                Clamp();
                Changed();
            }
            else
            {
                bool notch = e.Delta % 120 == 0;
                ScrollBy(-e.Delta * (notch ? 0.9 : 0.75), notch);
                ShowBar(true);
            }
            e.Handled = true;
        }

        protected override void OnManipulationStarting(ManipulationStartingEventArgs e)
        {
            e.ManipulationContainer = this;
            e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
            e.Handled = true;
        }

        protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
        {
            Vector s = e.DeltaManipulation.Scale;
            if (Math.Abs(s.X - 1) > 0.001) SetZoom(zoom * s.X, e.ManipulationOrigin);
            ox -= e.DeltaManipulation.Translation.X;
            oy -= e.DeltaManipulation.Translation.Y;
            Clamp();
            Changed();
            e.Handled = true;
        }

        // ---- Scroll bar: a thin bar that shows while scrolling or when the pointer is near the edge, as on macOS

        double barAlpha;
        DispatcherTimer barFade;
        DateTime barSeen;

        double BarLength()
        {
            if (contentH <= ActualHeight) return 0;
            return Math.Max(36, ActualHeight * ActualHeight / contentH);
        }

        Rect BarRect()
        {
            double len = BarLength();
            if (len <= 0) return Rect.Empty;
            double track = ActualHeight - len, y = contentH - ActualHeight <= 0 ? 0 : oy / (contentH - ActualHeight) * track;
            return new Rect(ActualWidth - 11, y + 2, 7, len - 4);
        }

        bool OverBar(Point p)
        {
            Rect r = BarRect();
            if (r.IsEmpty) return false;
            r.Inflate(4, 0);
            return r.Contains(p);
        }

        void ShowBar(bool on)
        {
            if (!on) return;
            barSeen = DateTime.UtcNow;
            if (barAlpha < 1) { barAlpha = 1; InvalidateVisual(); }
            if (barFade == null)
            {
                barFade = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
                barFade.Tick += delegate
                {
                    if (dragKind == Drag.Bar || (DateTime.UtcNow - barSeen).TotalMilliseconds < 900) return;
                    barAlpha -= 0.12;
                    if (barAlpha <= 0) { barAlpha = 0; barFade.Stop(); }
                    InvalidateVisual();
                };
            }
            barFade.Start();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (dragKind == Drag.None) Cursor = null;
        }

        void DrawBar(DrawingContext dc)
        {
            if (barAlpha <= 0) return;
            Rect r = BarRect();
            if (r.IsEmpty) return;
            Palette pal = Ds.Brushes;
            Color c = pal.Dark ? Color.FromArgb((byte)(150 * barAlpha), 255, 255, 255) : Color.FromArgb((byte)(120 * barAlpha), 0, 0, 0);
            dc.DrawRoundedRectangle(new SolidColorBrush(c), null, r, 3.5, 3.5);
        }

        // ---- Keyboard (the window forwards the keys it does not use)

        public bool HandleKey(KeyEventArgs e)
        {
            if (Engine == null) return false;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            double page = Math.Max(60, ActualHeight - 60);
            switch (e.Key)
            {
                case System.Windows.Input.Key.Down: ScrollBy(60, true); return true;
                case System.Windows.Input.Key.Up: ScrollBy(-60, true); return true;
                case System.Windows.Input.Key.PageDown: ScrollBy(page, true); return true;
                case System.Windows.Input.Key.PageUp: ScrollBy(-page, true); return true;
                case System.Windows.Input.Key.Space: ScrollBy(shift ? -page : page, true); return true;
                case System.Windows.Input.Key.Home: ScrollTo(0, !ctrl); return true;
                case System.Windows.Input.Key.End: ScrollTo(contentH, !ctrl); return true;
                case System.Windows.Input.Key.Left:
                    if (contentW > ActualWidth + 1 && !ctrl) { ox -= 60; Clamp(); Changed(); } else GoTo(CurrentPage - 1, true);
                    return true;
                case System.Windows.Input.Key.Right:
                    if (contentW > ActualWidth + 1 && !ctrl) { ox += 60; Clamp(); Changed(); } else GoTo(CurrentPage + 1, true);
                    return true;
            }
            return false;
        }

        public void Detach()
        {
            StopGlide();
            settle.Stop();
            StopEdge();
            if (barFade != null) barFade.Stop();
            Engine = null;
            Reader = null;
            Marks = null;
            shots.Clear();
            layered.Clear();
            tileLayered = null;
            tile = null;
        }
    }

    // A search match: words First..Last of a page.
    public struct PdfHitPlace
    {
        public int Page, First, Last;
        public PdfHitPlace(int page, int first, int last) { Page = page; First = first; Last = last; }
    }
}
