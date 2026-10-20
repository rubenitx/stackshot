// Stackshot - What the user marks on a PDF: highlighted, underlined and struck-out text, notes and pen strokes. Kept
// on their own next to the document's cache (the PDF is never touched until asked), with undo, and drawn the same way
// on screen, in exports and in print.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;

namespace Stackshot
{
    public enum PdfMarkKind { Highlight, Underline, Strike, Note, Ink }

    // Coordinates are fractions of the page as shown (0..1 from its top left corner), so a mark stays put at any zoom.
    public sealed class PdfMark
    {
        public string Id = Guid.NewGuid().ToString("N");
        public PdfMarkKind Kind;
        public int Page;
        public Color Color;
        public List<Rect> Boxes = new List<Rect>();          // text marks: one box per line
        public Point At;                                     // a note: its top left corner
        public List<List<Point>> Strokes = new List<List<Point>>();
        public double Width = 0.004;                         // pen strokes: fraction of the page width
        public string Text = "";                             // text marks: the words; a note: what it says
        public string Comment = "";                          // a note written on a text mark
        public DateTime When = DateTime.Now;
        // A note that is part of the PDF itself (written by Stackshot or another app): shown, never saved or written again.
        public bool InFile;
        public string Author = "";

        public bool OnText { get { return Kind == PdfMarkKind.Highlight || Kind == PdfMarkKind.Underline || Kind == PdfMarkKind.Strike; } }

        public PdfMark Clone()
        {
            PdfMark m = (PdfMark)MemberwiseClone();
            m.Boxes = new List<Rect>(Boxes);
            m.Strokes = new List<List<Point>>();
            foreach (List<Point> s in Strokes) m.Strokes.Add(new List<Point>(s));
            return m;
        }

        // The area it covers on the page (fractions); a note counts as its icon at the given page width in DIPs.
        public Rect Bounds(double pageWidth, double pageHeight)
        {
            if (Kind == PdfMarkKind.Note)
            {
                double s = PdfMarks.NoteSize;
                return new Rect(At.X, At.Y, s / Math.Max(1, pageWidth), s / Math.Max(1, pageHeight));
            }
            Rect r = Rect.Empty;
            foreach (Rect b in Boxes) r = r.IsEmpty ? b : Rect.Union(r, b);
            foreach (List<Point> s in Strokes)
                foreach (Point p in s) r = r.IsEmpty ? new Rect(p, new Size(0, 0)) : Rect.Union(r, p);
            if (Kind == PdfMarkKind.Ink && !r.IsEmpty) r.Inflate(Width, Width * pageWidth / Math.Max(1, pageHeight));
            return r;
        }

        // A short description for lists: the words marked or the note.
        public string Summary
        {
            get
            {
                string t = Kind == PdfMarkKind.Note ? Text : Kind == PdfMarkKind.Ink ? "Trazo a mano" : Text;
                t = (t ?? "").Replace('\n', ' ').Trim();
                if (t.Length == 0) t = Kind == PdfMarkKind.Highlight ? "Zona resaltada" : Kind == PdfMarkKind.Note ? "Nota vac\u00EDa" : "Marca";
                return t.Length > 140 ? t.Substring(0, 139).TrimEnd() + "\u2026" : t;
            }
        }
    }

    public sealed class PdfMarks
    {
        public const double NoteSize = 22;      // the note icon, in DIPs at any zoom
        const int Version = 1;

        // Preview's colours, a little deeper so they read on white and in print.
        public static readonly Color[] Colors =
        {
            Color.FromRgb(255, 214, 10),    // amarillo
            Color.FromRgb(52, 199, 89),     // verde
            Color.FromRgb(10, 132, 255),    // azul
            Color.FromRgb(255, 55, 135),    // rosa
            Color.FromRgb(175, 82, 222),    // morado
            Color.FromRgb(255, 59, 48)      // rojo
        };
        public static readonly string[] ColorNames = { "Amarillo", "Verde", "Azul", "Rosa", "Morado", "Rojo" };

        public readonly List<PdfMark> All = new List<PdfMark>();
        readonly List<Step> undo = new List<Step>(), redo = new List<Step>();
        public event Action Changed;
        public bool Dirty;        // changed since the last save to the cache
        public bool SinceExport;  // changed since the marks were last written into a PDF

        // One undoable change: marks that were there before and after (by id).
        sealed class Step { public List<PdfMark> Before = new List<PdfMark>(), After = new List<PdfMark>(); }

        public IEnumerable<PdfMark> On(int page)
        {
            foreach (PdfMark m in All) if (m.Page == page) yield return m;
        }

        public PdfMark Find(string id)
        {
            foreach (PdfMark m in All) if (m.Id == id) return m;
            return null;
        }

        public void Add(PdfMark m)
        {
            Step s = new Step();
            s.After.Add(m.Clone());
            Apply(s, true);
        }

        public void Remove(PdfMark m)
        {
            if (Find(m.Id) == null) return;
            Step s = new Step();
            s.Before.Add(Find(m.Id).Clone());
            Apply(s, true);
        }

        // The same mark (same id) changed: a colour, a note's text.
        public void Update(PdfMark changed)
        {
            PdfMark old = Find(changed.Id);
            if (old == null) return;
            Step s = new Step();
            s.Before.Add(old.Clone());
            s.After.Add(changed.Clone());
            Apply(s, true);
        }

        // The user's marks go; the notes inside the PDF stay (they belong to the file).
        public void Clear()
        {
            if (Mine == 0) return;
            Step s = new Step();
            foreach (PdfMark m in All) if (!m.InFile) s.Before.Add(m.Clone());
            Apply(s, true);
        }

        // How many marks are the user's (the notes inside the PDF don't count).
        public int Mine
        {
            get
            {
                int n = 0;
                foreach (PdfMark m in All) if (!m.InFile) n++;
                return n;
            }
        }

        // The notes found inside the PDF, shown with the rest; not undoable, not saved.
        public void FromFile(List<PdfMark> notes)
        {
            All.RemoveAll(delegate(PdfMark m) { return m.InFile; });
            foreach (PdfMark n in notes) { n.InFile = true; All.Add(n); }
            Action h = Changed;
            if (h != null) h();
        }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            Step s = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Run(s.After, s.Before);
            redo.Add(s);
            Done();
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            Step s = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Run(s.Before, s.After);
            undo.Add(s);
            Done();
            return true;
        }

        void Apply(Step s, bool record)
        {
            Run(s.Before, s.After);
            if (record)
            {
                undo.Add(s);
                if (undo.Count > 200) undo.RemoveAt(0);
                redo.Clear();
            }
            Done();
        }

        // Takes out the marks of from (by id) and puts in those of to, at the place they had.
        void Run(List<PdfMark> from, List<PdfMark> to)
        {
            int at = -1;
            foreach (PdfMark m in from)
            {
                int i = All.FindIndex(delegate(PdfMark x) { return x.Id == m.Id; });
                if (i < 0) continue;
                if (at < 0 || i < at) at = i;
                All.RemoveAt(i);
            }
            foreach (PdfMark m in to)
            {
                if (at >= 0 && at <= All.Count) All.Insert(at++, m.Clone());
                else All.Add(m.Clone());
            }
        }

        void Done()
        {
            Dirty = true;
            SinceExport = true;
            Action h = Changed;
            if (h != null) h();
        }

        // ---- Saving ({"v":1,"marks":[{...}]})

        public static PdfMarks Load(string file)
        {
            PdfMarks o = new PdfMarks();
            if (file == null || !File.Exists(file)) return o;
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                Dictionary<string, object> root = js.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("marks")) return o;
                object[] list = root["marks"] as object[];
                if (list == null) return o;
                foreach (object x in list)
                {
                    Dictionary<string, object> d = x as Dictionary<string, object>;
                    if (d == null) continue;
                    PdfMark m = new PdfMark();
                    m.Id = Str(d, "id") ?? m.Id;
                    PdfMarkKind k;
                    if (!Enum.TryParse(Str(d, "kind") ?? "", true, out k)) continue;
                    m.Kind = k;
                    m.Page = Int(d, "page");
                    m.Color = ParseColor(Str(d, "color"));
                    m.Text = Str(d, "text") ?? "";
                    m.Comment = Str(d, "comment") ?? "";
                    m.Width = d.ContainsKey("width") ? Num(d["width"]) : m.Width;
                    DateTime when;
                    if (DateTime.TryParse(Str(d, "when") ?? "", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out when)) m.When = when;
                    object[] at = d.ContainsKey("at") ? d["at"] as object[] : null;
                    if (at != null && at.Length == 2) m.At = new Point(Num(at[0]), Num(at[1]));
                    object[] boxes = d.ContainsKey("boxes") ? d["boxes"] as object[] : null;
                    if (boxes != null)
                        foreach (object b in boxes)
                        {
                            object[] r = b as object[];
                            if (r != null && r.Length == 4) m.Boxes.Add(new Rect(Num(r[0]), Num(r[1]), Math.Max(0, Num(r[2])), Math.Max(0, Num(r[3]))));
                        }
                    object[] strokes = d.ContainsKey("strokes") ? d["strokes"] as object[] : null;
                    if (strokes != null)
                        foreach (object s in strokes)
                        {
                            object[] pts = s as object[];
                            if (pts == null) continue;
                            List<Point> line = new List<Point>();
                            for (int i = 0; i + 1 < pts.Length; i += 2) line.Add(new Point(Num(pts[i]), Num(pts[i + 1])));
                            if (line.Count > 0) m.Strokes.Add(line);
                        }
                    o.All.Add(m);
                }
            }
            catch (Exception ex) { ShotStack.Log("PDF, marcas: " + ex.Message); }
            return o;
        }

        public void Save(string file)
        {
            if (file == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"v\":").Append(Version).Append(",\"marks\":[");
            int written = 0;
            for (int i = 0; i < All.Count; i++)
            {
                PdfMark m = All[i];
                if (m.InFile) continue;
                if (written++ > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(PdfReader.Json(m.Id))
                  .Append(",\"kind\":").Append(PdfReader.Json(m.Kind.ToString()))
                  .Append(",\"page\":").Append(m.Page)
                  .Append(",\"color\":").Append(PdfReader.Json(ColorText(m.Color)))
                  .Append(",\"text\":").Append(PdfReader.Json(m.Text))
                  .Append(",\"comment\":").Append(PdfReader.Json(m.Comment))
                  .Append(",\"width\":").Append(D(m.Width))
                  .Append(",\"when\":").Append(PdfReader.Json(m.When.ToString("o", CultureInfo.InvariantCulture)))
                  .Append(",\"at\":[").Append(D(m.At.X)).Append(',').Append(D(m.At.Y)).Append(']')
                  .Append(",\"boxes\":[");
                for (int b = 0; b < m.Boxes.Count; b++)
                {
                    Rect r = m.Boxes[b];
                    if (b > 0) sb.Append(',');
                    sb.Append('[').Append(D(r.X)).Append(',').Append(D(r.Y)).Append(',').Append(D(r.Width)).Append(',').Append(D(r.Height)).Append(']');
                }
                sb.Append("],\"strokes\":[");
                for (int s = 0; s < m.Strokes.Count; s++)
                {
                    if (s > 0) sb.Append(',');
                    sb.Append('[');
                    for (int p = 0; p < m.Strokes[s].Count; p++)
                    {
                        if (p > 0) sb.Append(',');
                        sb.Append(D(m.Strokes[s][p].X)).Append(',').Append(D(m.Strokes[s][p].Y));
                    }
                    sb.Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                if (written == 0 && File.Exists(file)) File.Delete(file);
                else if (written > 0) AtomicFile.Write(file, new UTF8Encoding(false).GetBytes(sb.ToString()));
                Dirty = false;
            }
            catch (Exception ex) { ShotStack.Log("PDF, guardar marcas: " + ex.Message); }
        }

        static string Str(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null; }
        static int Int(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : 0; }
        static double Num(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }
        static string D(double v) { return v.ToString("0.#####", CultureInfo.InvariantCulture); }

        public static string ColorText(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        public static Color ParseColor(string s)
        {
            try
            {
                if (s != null && s.Length == 7 && s[0] == '#')
                    return Color.FromRgb(byte.Parse(s.Substring(1, 2), NumberStyles.HexNumber), byte.Parse(s.Substring(3, 2), NumberStyles.HexNumber), byte.Parse(s.Substring(5, 2), NumberStyles.HexNumber));
            }
            catch { }
            return Colors[0];
        }

        // ---- Look (shared by the screen, exports and print)

        // How thick an underline or strike-out is for a line of text this tall.
        public static double LineWeight(double lineHeight) { return Math.Max(1, lineHeight * 0.08); }

        // ---- Highlighter: the colour multiplied into the page, so the ink under it stays black (as on paper)

        // The highlighter's colour: the mark's own, a little lighter so a page full of it still reads.
        public static Color Tint(Color c) { return Color.FromRgb(Lift(c.R), Lift(c.G), Lift(c.B)); }
        static byte Lift(byte v) { return (byte)(v + (255 - v) * 0.3); }

        // A highlighted box a touch taller than its words (fractions of the page).
        public static Rect Grown(Rect b, double grow)
        {
            double pad = b.Height * grow;
            return new Rect(b.X - pad * 0.5, b.Y - pad, b.Width + pad, b.Height + pad * 2);
        }

        // Multiplies a colour into a 24 or 32 bpp page image, inside a box given as fractions of the image.
        public static void Multiply(System.Drawing.Bitmap img, Rect box, Color c)
        {
            System.Drawing.Rectangle r = System.Drawing.Rectangle.Intersect(new System.Drawing.Rectangle(
                (int)Math.Floor(box.X * img.Width), (int)Math.Floor(box.Y * img.Height), (int)Math.Ceiling(box.Width * img.Width) + 1, (int)Math.Ceiling(box.Height * img.Height) + 1),
                new System.Drawing.Rectangle(0, 0, img.Width, img.Height));
            if (r.Width <= 0 || r.Height <= 0) return;
            int bpp = img.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb ? 3 : 4;
            if (bpp == 4 && img.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppRgb && img.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb && img.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppPArgb) return;
            System.Drawing.Imaging.BitmapData d = img.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadWrite, img.PixelFormat);
            try
            {
                byte[] row = new byte[r.Width * bpp];
                for (int y = 0; y < r.Height; y++)
                {
                    IntPtr at = new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(at, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += bpp)
                    {
                        row[x] = (byte)(row[x] * c.B / 255);
                        row[x + 1] = (byte)(row[x + 1] * c.G / 255);
                        row[x + 2] = (byte)(row[x + 2] * c.R / 255);
                    }
                    System.Runtime.InteropServices.Marshal.Copy(row, 0, at, row.Length);
                }
            }
            finally { img.UnlockBits(d); }
        }

        // The same into a page drawing on screen (BGR32, its own copy).
        public static void Multiply(System.Windows.Media.Imaging.WriteableBitmap img, Int32Rect r, Color c)
        {
            r = Clip(r, img.PixelWidth, img.PixelHeight);
            if (r.Width <= 0 || r.Height <= 0) return;
            int[] px = new int[r.Width * r.Height];
            img.CopyPixels(r, px, r.Width * 4, 0);
            for (int i = 0; i < px.Length; i++)
            {
                int p = px[i];
                int b = (p & 0xFF) * c.B / 255, g = ((p >> 8) & 0xFF) * c.G / 255, rr = ((p >> 16) & 0xFF) * c.R / 255;
                px[i] = (int)(0xFF000000u | (uint)(rr << 16) | (uint)(g << 8) | (uint)b);
            }
            img.WritePixels(r, px, r.Width * 4, 0);
        }

        static Int32Rect Clip(Int32Rect r, int w, int h)
        {
            int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y), x1 = Math.Min(w, r.X + r.Width), y1 = Math.Min(h, r.Y + r.Height);
            return new Int32Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        // The mark on screen, page being where the page is drawn (DIPs). The note icon keeps its size at any zoom.
        // Highlights are not drawn here: the view multiplies them into the page drawing (Tint).
        public static void Draw(DrawingContext dc, PdfMark m, Rect page, bool selected, Palette pal)
        {
            Brush fill = new SolidColorBrush(m.Color);
            fill.Freeze();
            switch (m.Kind)
            {
                case PdfMarkKind.Highlight:
                    break;
                case PdfMarkKind.Underline:
                case PdfMarkKind.Strike:
                    foreach (Rect b in m.Boxes)
                    {
                        Rect r = Place(b, page, 0);
                        double t = LineWeight(r.Height), y = m.Kind == PdfMarkKind.Underline ? r.Bottom - t * 0.2 : r.Top + r.Height * 0.55;
                        dc.DrawRectangle(fill, null, new Rect(r.Left, y - t / 2, r.Width, t));
                    }
                    break;
                case PdfMarkKind.Ink:
                    {
                        Pen pen = new Pen(fill, Math.Max(1, m.Width * page.Width)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                        pen.Freeze();
                        foreach (List<Point> s in m.Strokes) dc.DrawGeometry(null, pen, Stroke(s, page));
                        break;
                    }
                case PdfMarkKind.Note:
                    DrawNote(dc, m, new Point(page.X + m.At.X * page.Width, page.Y + m.At.Y * page.Height));
                    break;
            }
            if (m.OnText && m.Comment.Length > 0 && m.Boxes.Count > 0)
            {
                // A text mark with a note: a small speech dot at the end of its first line.
                Rect r = Place(m.Boxes[0], page, 0);
                Brush dot = new SolidColorBrush(m.Color);
                dot.Freeze();
                dc.DrawEllipse(dot, new Pen(Brushes.White, 1.5), new Point(r.Right + 6, r.Top + 2), 4.5, 4.5);
            }
            if (selected)
            {
                Rect b = m.Bounds(page.Width, page.Height);
                Rect r = new Rect(page.X + b.X * page.Width, page.Y + b.Y * page.Height, b.Width * page.Width, b.Height * page.Height);
                if (m.Kind == PdfMarkKind.Note) r = new Rect(r.X, r.Y, NoteSize, NoteSize);
                r.Inflate(4, 3);
                Pen ring = new Pen(new SolidColorBrush(pal.Accent), 1.5);
                ring.Freeze();
                dc.DrawRoundedRectangle(null, ring, r, 4, 4);
            }
        }

        static void DrawNote(DrawingContext dc, PdfMark m, Point at)
        {
            double s = NoteSize;
            Rect r = new Rect(at.X, at.Y, s, s);
            Brush fill = new SolidColorBrush(m.Color);
            fill.Freeze();
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), null, new Rect(r.X + 0.5, r.Y + 1.5, s, s), 4, 4);
            dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 0.75), r, 4, 4);
            Pen line = new Pen(new SolidColorBrush(Color.FromArgb(150, 40, 30, 0)), 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            line.Freeze();
            dc.DrawLine(line, new Point(r.X + 6, r.Y + 8), new Point(r.Right - 6, r.Y + 8));
            dc.DrawLine(line, new Point(r.X + 6, r.Y + 11.5), new Point(r.Right - 6, r.Y + 11.5));
            dc.DrawLine(line, new Point(r.X + 6, r.Y + 15), new Point(r.Right - 9, r.Y + 15));
        }

        // A box of the page (fractions) where it is drawn; grow makes highlights a touch taller than the words.
        public static Rect Place(Rect b, Rect page, double grow)
        {
            double h = b.Height * page.Height, pad = h * grow;
            return new Rect(page.X + b.X * page.Width - pad * 0.5, page.Y + b.Y * page.Height - pad, b.Width * page.Width + pad, h + pad * 2);
        }

        static Geometry Stroke(List<Point> s, Rect page)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                if (s.Count > 0)
                {
                    c.BeginFigure(new Point(page.X + s[0].X * page.Width, page.Y + s[0].Y * page.Height), false, false);
                    if (s.Count == 1) c.LineTo(new Point(page.X + s[0].X * page.Width + 0.01, page.Y + s[0].Y * page.Height), true, true);
                    for (int i = 1; i < s.Count; i++) c.LineTo(new Point(page.X + s[i].X * page.Width, page.Y + s[i].Y * page.Height), true, true);
                }
            }
            g.Freeze();
            return g;
        }

        // Every mark of a page drawn on its image (exports, captures, print): highlights multiplied in first, the rest on top.
        public static void DrawAll(System.Drawing.Bitmap img, IList<PdfMark> marks)
        {
            if (marks == null || marks.Count == 0) return;
            foreach (PdfMark m in marks)
                if (m.Kind == PdfMarkKind.Highlight)
                    foreach (Rect b in m.Boxes) Multiply(img, Grown(b, 0.06), Tint(m.Color));
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(img))
                foreach (PdfMark m in marks) Draw(g, m, new System.Drawing.RectangleF(0, 0, img.Width, img.Height));
        }

        // One mark on a page image (highlights go through DrawAll). Notes keep a size relative to the page: an icon in
        // the margin of a printed page should be as big there as it looks on screen at "fit width".
        public static void Draw(System.Drawing.Graphics g, PdfMark m, System.Drawing.RectangleF page)
        {
            System.Drawing.Color c = System.Drawing.Color.FromArgb(m.Color.R, m.Color.G, m.Color.B);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rect pr = new Rect(page.X, page.Y, page.Width, page.Height);
            switch (m.Kind)
            {
                case PdfMarkKind.Highlight:
                    break;
                case PdfMarkKind.Underline:
                case PdfMarkKind.Strike:
                    using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(c))
                        foreach (Rect x in m.Boxes)
                        {
                            Rect r = Place(x, pr, 0);
                            double t = LineWeight(r.Height), y = m.Kind == PdfMarkKind.Underline ? r.Bottom - t * 0.2 : r.Top + r.Height * 0.55;
                            g.FillRectangle(b, (float)r.X, (float)(y - t / 2), (float)r.Width, (float)t);
                        }
                    break;
                case PdfMarkKind.Ink:
                    using (System.Drawing.Pen p = new System.Drawing.Pen(c, (float)Math.Max(1, m.Width * page.Width)))
                    {
                        p.StartCap = p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                        foreach (List<Point> s in m.Strokes)
                        {
                            if (s.Count == 0) continue;
                            System.Drawing.PointF[] pts = new System.Drawing.PointF[Math.Max(2, s.Count)];
                            for (int i = 0; i < pts.Length; i++)
                            {
                                Point q = s[Math.Min(i, s.Count - 1)];
                                pts[i] = new System.Drawing.PointF((float)(page.X + q.X * page.Width + (s.Count == 1 && i == 1 ? 0.01 : 0)), (float)(page.Y + q.Y * page.Height));
                            }
                            g.DrawLines(p, pts);
                        }
                    }
                    break;
                case PdfMarkKind.Note:
                    {
                        float s = page.Width * 0.034f, x = page.X + (float)(m.At.X * page.Width), y = page.Y + (float)(m.At.Y * page.Height), k = s / 22f;
                        using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(c))
                        using (System.Drawing.Pen edge = new System.Drawing.Pen(System.Drawing.Color.FromArgb(70, 0, 0, 0), Math.Max(0.75f, 0.75f * k)))
                        using (System.Drawing.Pen line = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 40, 30, 0), Math.Max(1f, 1.4f * k)))
                        {
                            g.FillRectangle(b, x, y, s, s);
                            g.DrawRectangle(edge, x, y, s, s);
                            g.DrawLine(line, x + 6 * k, y + 8 * k, x + s - 6 * k, y + 8 * k);
                            g.DrawLine(line, x + 6 * k, y + 11.5f * k, x + s - 6 * k, y + 11.5f * k);
                            g.DrawLine(line, x + 6 * k, y + 15 * k, x + s - 9 * k, y + 15 * k);
                        }
                        break;
                    }
            }
        }
    }
}
