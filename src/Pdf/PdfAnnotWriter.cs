// Stackshot - Writes marks into a PDF as its own annotations (highlight, underline, strike-out, ink, notes), in an
// incremental update: the original bytes stay exactly as they were and the new objects go after them, as Acrobat does
// when it saves comments. Every annotation carries its own appearance, so any viewer draws it the same.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace Stackshot
{
    public static class PdfAnnotWriter
    {
        sealed class Refused : Exception { public Refused(string m) : base(m) { } }

        // Writes target (atomically) and opens it again with Windows' engine before keeping it. Null on success, or the
        // reason in plain words.
        public static string WriteVerified(string source, string target, List<PdfMark> marks, string author)
        {
            byte[] original;
            try { original = File.ReadAllBytes(source); }
            catch (Exception ex) { return "No he podido leer el PDF: " + ex.Message; }
            byte[] output;
            try { output = Write(original, marks, author, DateTime.Now); }
            catch (Refused r) { return r.Message; }
            catch (Exception ex)
            {
                ShotStack.Log("PDF, escribir marcas: " + ex);
                return "Este PDF tiene una estructura que no s\u00E9 ampliar sin riesgo: no lo he tocado.";
            }
            string dir = Path.GetDirectoryName(Path.GetFullPath(target));
            string tmp = Path.Combine(dir, "~" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".pdf");
            try
            {
                File.WriteAllBytes(tmp, output);
                string err;
                using (PdfEngine check = PdfEngine.Open(tmp, null, out err))
                using (PdfEngine before = PdfEngine.Open(source, null, out err))
                {
                    if (check == null) return "La copia con las marcas no se abre bien: no la he guardado.";
                    if (before != null && before.Pages != check.Pages) return "La copia con las marcas no tiene las mismas p\u00E1ginas: no la he guardado.";
                }
                AtomicFile.Write(target, output);
                return null;
            }
            catch (Exception ex)
            {
                ShotStack.Log("PDF, guardar marcas: " + ex);
                return ShotStack.Explain(ex, target);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // ---- The update

        internal static byte[] Write(byte[] pdf, List<PdfMark> marks, string author, DateTime now)
        {
            Doc doc = new Doc(pdf);
            if (doc.Trailer.Get("Encrypt") != null) throw new Refused("Este PDF est\u00E1 cifrado: no puedo escribir en \u00E9l. Puedes guardar sus p\u00E1ginas como imagen o capturarlas.");
            List<PageInfo> pages = doc.Pages();
            int next = Math.Max(doc.Size, doc.MaxObject + 1);
            MemoryStream o = new MemoryStream();
            o.Write(pdf, 0, pdf.Length);
            if (pdf.Length > 0 && pdf[pdf.Length - 1] != '\n' && pdf[pdf.Length - 1] != '\r') Ascii(o, "\n");
            SortedDictionary<int, long> offsets = new SortedDictionary<int, long>();
            Dictionary<int, int> genOf = new Dictionary<int, int>();
            Dictionary<int, List<PdfMark>> byPage = new Dictionary<int, List<PdfMark>>();
            foreach (PdfMark m in marks)
            {
                if (m.Page < 0 || m.Page >= pages.Count) continue;
                List<PdfMark> l;
                if (!byPage.TryGetValue(m.Page, out l)) byPage[m.Page] = l = new List<PdfMark>();
                l.Add(m);
            }
            string date = "D:" + now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + Zone(now);
            foreach (KeyValuePair<int, List<PdfMark>> kv in byPage)
            {
                PageInfo p = pages[kv.Key];
                List<object> annots = new List<object>();
                object old = doc.Resolve(p.Dict.Get("Annots"));
                if (old is List<object>) annots.AddRange((List<object>)old);
                foreach (PdfMark m in kv.Value)
                {
                    int ap = next++, an = next++;
                    string apBody, annot;
                    Build(m, p, author, date, ap, out apBody, out annot);
                    offsets[ap] = o.Position;
                    Ascii(o, ap + " 0 obj\n" + apBody + "\nendobj\n");
                    offsets[an] = o.Position;
                    Ascii(o, an + " 0 obj\n" + annot.Replace("{PAGE}", p.Num + " " + p.Gen + " R") + "\nendobj\n");
                    annots.Add(new PRef(an, 0));
                }
                PDict page = p.Dict.Copy();
                page.Set("Annots", annots);
                offsets[p.Num] = o.Position;
                genOf[p.Num] = p.Gen;
                Ascii(o, p.Num + " " + p.Gen + " obj\n" + Ser(page) + "\nendobj\n");
            }
            int size = Math.Max(doc.Size, next);
            if (doc.StreamXref)
            {
                int xn = size;
                size++;
                long at = o.Position;
                offsets[xn] = at;
                List<int> nums = new List<int>(offsets.Keys);
                StringBuilder index = new StringBuilder();
                MemoryStream rows = new MemoryStream();
                foreach (int[] run in Runs(nums))
                {
                    index.Append(run[0]).Append(' ').Append(run[1]).Append(' ');
                    for (int i = 0; i < run[1]; i++)
                    {
                        int num = run[0] + i;
                        long off = offsets[num];
                        int g = genOf.ContainsKey(num) ? genOf[num] : 0;
                        rows.WriteByte(1);
                        rows.WriteByte((byte)(off >> 24)); rows.WriteByte((byte)(off >> 16)); rows.WriteByte((byte)(off >> 8)); rows.WriteByte((byte)off);
                        rows.WriteByte((byte)(g >> 8)); rows.WriteByte((byte)g);
                    }
                }
                byte[] data = rows.ToArray();
                StringBuilder dict = new StringBuilder("<< /Type /XRef /Size " + size + " /W [1 4 2] /Index [" + index.ToString().Trim() + "] /Length " + data.Length);
                TrailerKeys(doc, dict);
                dict.Append(" /Prev ").Append(doc.StartXref).Append(" >>");
                Ascii(o, xn + " 0 obj\n" + dict + "\nstream\n");
                o.Write(data, 0, data.Length);
                Ascii(o, "\nendstream\nendobj\nstartxref\n" + at + "\n%%EOF\n");
            }
            else
            {
                long at = o.Position;
                StringBuilder x = new StringBuilder("xref\n");
                foreach (int[] run in Runs(new List<int>(offsets.Keys)))
                {
                    x.Append(run[0]).Append(' ').Append(run[1]).Append('\n');
                    for (int i = 0; i < run[1]; i++)
                    {
                        int num = run[0] + i;
                        x.Append(offsets[num].ToString("D10", CultureInfo.InvariantCulture)).Append(' ').Append((genOf.ContainsKey(num) ? genOf[num] : 0).ToString("D5", CultureInfo.InvariantCulture)).Append(" n\r\n");
                    }
                }
                x.Append("trailer\n<< /Size ").Append(size);
                TrailerKeys(doc, x);
                x.Append(" /Prev ").Append(doc.StartXref).Append(" >>\nstartxref\n").Append(at).Append("\n%%EOF\n");
                Ascii(o, x.ToString());
            }
            return o.ToArray();
        }

        static void TrailerKeys(Doc doc, StringBuilder sb)
        {
            foreach (string k in new string[] { "Root", "Info", "ID" })
            {
                object v = doc.Trailer.Get(k);
                if (v != null) sb.Append(" /").Append(k).Append(' ').Append(Ser(v));
            }
        }

        // Consecutive object numbers as [first, count] runs.
        static List<int[]> Runs(List<int> nums)
        {
            nums.Sort();
            List<int[]> o = new List<int[]>();
            foreach (int n in nums)
            {
                if (o.Count > 0 && o[o.Count - 1][0] + o[o.Count - 1][1] == n) o[o.Count - 1][1]++;
                else o.Add(new int[] { n, 1 });
            }
            return o;
        }

        static string Zone(DateTime t)
        {
            TimeSpan z = TimeZoneInfo.Local.GetUtcOffset(t);
            if (z == TimeSpan.Zero) return "Z";
            return (z < TimeSpan.Zero ? "-" : "+") + Math.Abs(z.Hours).ToString("D2") + "'" + Math.Abs(z.Minutes).ToString("D2") + "'";
        }

        static void Ascii(Stream s, string t) { byte[] b = Encoding.GetEncoding(28591).GetBytes(t); s.Write(b, 0, b.Length); }

        // ---- Notes already in the PDF

        // The sticky notes the PDF itself carries (Acrobat's, anyone's, or Stackshot's once written in). Windows' engine
        // does not draw them, so the viewer does. Empty when the file can't be read this way (or is encrypted).
        internal static List<PdfMark> ReadNotes(byte[] pdf)
        {
            List<PdfMark> o = new List<PdfMark>();
            Doc doc;
            List<PageInfo> pages;
            try
            {
                doc = new Doc(pdf);
                if (doc.Trailer.Get("Encrypt") != null) return o;
                pages = doc.Pages();
            }
            catch (Exception ex) { ShotStack.Log("PDF, notas del archivo: " + ex.Message); return o; }
            for (int i = 0; i < pages.Count; i++)
            {
                List<object> annots;
                try { annots = doc.Resolve(pages[i].Dict.Get("Annots")) as List<object>; }
                catch { continue; }
                if (annots == null) continue;
                for (int k = 0; k < annots.Count; k++)
                {
                    try
                    {
                        PDict d = doc.Resolve(annots[k]) as PDict;
                        PName st = d == null ? null : d.Get("Subtype") as PName;
                        if (st == null || st.V != "Text") continue;
                        PNum f = doc.Resolve(d.Get("F")) as PNum;
                        if (f != null && ((int)f.V & 2) != 0) continue;   // hidden
                        double[] r = doc.Box(d.Get("Rect"));
                        if (r == null) continue;
                        PageInfo p = pages[i];
                        Point a = Shown(p, r[0], r[1]), b = Shown(p, r[2], r[3]);
                        PdfMark m = new PdfMark { Kind = PdfMarkKind.Note, Page = i, InFile = true, Id = "pdf-" + i + "-" + k };
                        m.At = new Point(Math.Max(0, Math.Min(a.X, b.X)), Math.Max(0, Math.Min(a.Y, b.Y)));
                        m.Text = TextString(doc.Resolve(d.Get("Contents")));
                        m.Author = TextString(doc.Resolve(d.Get("T")));
                        m.Color = ColorOf(doc, d.Get("C"));
                        DateTime when;
                        if (Date(TextString(doc.Resolve(d.Get("M"))), out when)) m.When = when;
                        o.Add(m);
                    }
                    catch { }
                }
            }
            return o;
        }

        // A point of the page's own coordinates as shown (fractions from the top left): User() the other way round.
        static Point Shown(PageInfo p, double x, double y)
        {
            double w = Math.Max(1e-9, p.X1 - p.X0), h = Math.Max(1e-9, p.Y1 - p.Y0);
            switch (p.Rotate)
            {
                case 90: return new Point((y - p.Y0) / h, (x - p.X0) / w);
                case 180: return new Point((p.X1 - x) / w, (y - p.Y0) / h);
                case 270: return new Point((p.Y1 - y) / h, (p.X1 - x) / w);
                default: return new Point((x - p.X0) / w, (p.Y1 - y) / h);
            }
        }

        static System.Windows.Media.Color ColorOf(Doc doc, object c)
        {
            List<object> a = doc.Resolve(c) as List<object>;
            if (a == null || a.Count != 3) return PdfMarks.Colors[0];
            byte[] v = new byte[3];
            for (int i = 0; i < 3; i++)
            {
                PNum n = doc.Resolve(a[i]) as PNum;
                v[i] = (byte)Math.Max(0, Math.Min(255, Math.Round((n == null ? 1 : n.V) * 255)));
            }
            return System.Windows.Media.Color.FromRgb(v[0], v[1], v[2]);
        }

        // A PDF text string: UTF-16 with its mark, UTF-8 with its mark, or the PDF's own single-byte encoding (close
        // enough to Windows-1252 for what people write in a note).
        static string TextString(object v)
        {
            PStr s = v as PStr;
            if (s == null || s.V.Length == 0) return "";
            byte[] b = s.V;
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            return Encoding.GetEncoding(1252).GetString(b);
        }

        // "D:20261020143000+02'00'" (only the date and time are needed for a note).
        static bool Date(string s, out DateTime when)
        {
            when = DateTime.MinValue;
            if (s == null) return false;
            if (s.StartsWith("D:", StringComparison.Ordinal)) s = s.Substring(2);
            if (s.Length < 8) return false;
            string digits = s.Length >= 14 ? s.Substring(0, 14) : s.Substring(0, 8).PadRight(14, '0');
            return DateTime.TryParseExact(digits, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out when);
        }

        // ---- One annotation and its appearance

        sealed class PageInfo
        {
            public int Num, Gen;
            public PDict Dict;
            public double X0, Y0, X1, Y1;   // the crop box, as shown
            public int Rotate;
        }

        // A point of the page as shown (fractions from the top left) in the page's own coordinates.
        static Point User(PageInfo p, double u, double v)
        {
            double w = p.X1 - p.X0, h = p.Y1 - p.Y0;
            switch (p.Rotate)
            {
                case 90: return new Point(p.X0 + v * w, p.Y0 + u * h);
                case 180: return new Point(p.X1 - u * w, p.Y0 + v * h);
                case 270: return new Point(p.X1 - v * w, p.Y1 - u * h);
                default: return new Point(p.X0 + u * w, p.Y1 - v * h);
            }
        }

        // Points (1/72 inch) in one fraction of the page's shown width.
        static double ShownWidth(PageInfo p) { return p.Rotate == 90 || p.Rotate == 270 ? p.Y1 - p.Y0 : p.X1 - p.X0; }
        static double ShownHeight(PageInfo p) { return p.Rotate == 90 || p.Rotate == 270 ? p.X1 - p.X0 : p.Y1 - p.Y0; }

        static void Build(PdfMark m, PageInfo p, string author, string date, int apNum, out string ap, out string annot)
        {
            double r = m.Color.R / 255.0, g = m.Color.G / 255.0, b = m.Color.B / 255.0;
            string color = N(r) + " " + N(g) + " " + N(b);
            StringBuilder draw = new StringBuilder();
            List<Point> all = new List<Point>();
            string subtype, extra = "";
            int flags = 4;
            switch (m.Kind)
            {
                case PdfMarkKind.Highlight:
                case PdfMarkKind.Underline:
                case PdfMarkKind.Strike:
                    {
                        subtype = m.Kind == PdfMarkKind.Highlight ? "Highlight" : m.Kind == PdfMarkKind.Underline ? "Underline" : "StrikeOut";
                        StringBuilder quads = new StringBuilder();
                        // A highlight multiplies its lighter tint into the page, as on screen: the words stay black.
                        System.Windows.Media.Color tint = PdfMarks.Tint(m.Color);
                        draw.Append(m.Kind == PdfMarkKind.Highlight ? "/GS0 gs " + N(tint.R / 255.0) + " " + N(tint.G / 255.0) + " " + N(tint.B / 255.0) : color).Append(" rg\n");
                        foreach (Rect x in m.Boxes)
                        {
                            Rect q = m.Kind == PdfMarkKind.Highlight ? Grow(x, 0.06) : x;
                            // Acrobat's order: upper left, upper right, lower left, lower right (of the text as read).
                            Point ul = User(p, q.Left, q.Top), ur = User(p, q.Right, q.Top), ll = User(p, q.Left, q.Bottom), lr = User(p, q.Right, q.Bottom);
                            quads.Append(P(ul)).Append(' ').Append(P(ur)).Append(' ').Append(P(ll)).Append(' ').Append(P(lr)).Append(' ');
                            all.Add(ul); all.Add(ur); all.Add(ll); all.Add(lr);
                            if (m.Kind == PdfMarkKind.Highlight) Poly(draw, ul, ur, lr, ll);
                            else
                            {
                                double t = q.Height * 0.08;   // as on screen (PdfMarks.LineWeight)
                                double y = m.Kind == PdfMarkKind.Underline ? q.Bottom - t * 0.2 : q.Top + q.Height * 0.55;
                                Poly(draw, User(p, q.Left, y - t / 2), User(p, q.Right, y - t / 2), User(p, q.Right, y + t / 2), User(p, q.Left, y + t / 2));
                            }
                        }
                        extra = " /QuadPoints [" + quads.ToString().Trim() + "]";
                        if (m.Kind == PdfMarkKind.Highlight) extra += " /CA 1";
                        break;
                    }
                case PdfMarkKind.Ink:
                    {
                        subtype = "Ink";
                        double w = Math.Max(0.5, m.Width * ShownWidth(p));
                        StringBuilder list = new StringBuilder();
                        draw.Append(color).Append(" RG ").Append(N(w)).Append(" w 1 J 1 j\n");
                        foreach (List<Point> s in m.Strokes)
                        {
                            if (s.Count == 0) continue;
                            list.Append('[');
                            for (int i = 0; i < s.Count; i++)
                            {
                                Point u = User(p, s[i].X, s[i].Y);
                                all.Add(u);
                                list.Append(P(u)).Append(' ');
                                draw.Append(P(u)).Append(i == 0 ? " m " : " l ");
                            }
                            if (s.Count == 1) { Point u = User(p, s[0].X, s[0].Y); draw.Append(N(u.X + 0.01)).Append(' ').Append(N(u.Y)).Append(" l "); }
                            draw.Append("S\n");
                            list.Append("] ");
                        }
                        // The box grows by half the pen so the round ends are not clipped.
                        List<Point> padded = new List<Point>();
                        foreach (Point u in all) { padded.Add(new Point(u.X - w, u.Y - w)); padded.Add(new Point(u.X + w, u.Y + w)); }
                        all = padded;
                        extra = " /InkList [" + list.ToString().Trim() + "] /BS << /W " + N(w) + " /LC 1 /LJ 1 >>";
                        break;
                    }
                default:
                    {
                        subtype = "Text";
                        // The icon, 22 DIPs as on screen at "fit width" (about 16 points), upright whatever the rotation.
                        double s = 16.5;
                        // The square it covers as shown, in the page's coordinates (turned pages swap the sides).
                        Point a = User(p, m.At.X, m.At.Y), c = User(p, m.At.X + s / ShownWidth(p), m.At.Y + s / ShownHeight(p));
                        double x0 = Math.Min(a.X, c.X), y1 = Math.Min(a.Y, c.Y) + s;
                        all.Add(new Point(x0, y1 - s));
                        all.Add(new Point(x0 + s, y1));
                        double k = s / 22.0;
                        draw.Append(color).Append(" rg 0 0 0 RG 0.4 w ");
                        draw.Append(N(x0)).Append(' ').Append(N(y1 - s)).Append(' ').Append(N(s)).Append(' ').Append(N(s)).Append(" re B\n");
                        draw.Append("0.16 0.12 0 RG ").Append(N(1.2 * k)).Append(" w 1 J\n");
                        foreach (double[] l in new double[][] { new double[] { 6, 8, 16 }, new double[] { 6, 11.5, 16 }, new double[] { 6, 15, 13 } })
                            draw.Append(N(x0 + l[0] * k)).Append(' ').Append(N(y1 - l[1] * k)).Append(" m ").Append(N(x0 + l[2] * k)).Append(' ').Append(N(y1 - l[1] * k)).Append(" l S\n");
                        extra = " /Name /Comment /Open false";
                        flags = 4 | 8 | 16;   // print, no zoom, no rotate: a note icon keeps its size and stays upright
                        break;
                    }
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Point u in all) { minX = Math.Min(minX, u.X); minY = Math.Min(minY, u.Y); maxX = Math.Max(maxX, u.X); maxY = Math.Max(maxY, u.Y); }
            if (all.Count == 0) { minX = p.X0; minY = p.Y0; maxX = p.X0 + 1; maxY = p.Y0 + 1; }
            string rect = "[" + N(minX) + " " + N(minY) + " " + N(maxX) + " " + N(maxY) + "]";
            string content = draw.ToString();
            string res = m.Kind == PdfMarkKind.Highlight ? " /Resources << /ExtGState << /GS0 << /Type /ExtGState /BM /Multiply /ca 1 /CA 1 >> >> >>" : "";
            byte[] body = Encoding.ASCII.GetBytes(content);
            ap = "<< /Type /XObject /Subtype /Form /FormType 1 /BBox " + rect + res + " /Length " + body.Length + " >>\nstream\n" + content + "\nendstream";
            string text = m.Kind == PdfMarkKind.Note ? m.Text : m.Comment;
            annot = "<< /Type /Annot /Subtype /" + subtype + " /Rect " + rect + " /P {PAGE} /F " + flags + " /C [" + color + "]" + extra +
                    " /NM " + Str("stackshot-" + m.Id) + " /T " + Str(string.IsNullOrEmpty(author) ? "Stackshot" : author) + " /M " + Str(date) + " /CreationDate " + Str(date) +
                    (string.IsNullOrEmpty(text) ? "" : " /Contents " + Str(text)) + " /AP << /N " + apNum + " 0 R >> >>";
        }

        static Rect Grow(Rect b, double grow)
        {
            double pad = b.Height * grow;
            return new Rect(b.X - pad * 0.5, b.Y - pad, b.Width + pad, b.Height + pad * 2);
        }

        static void Poly(StringBuilder sb, Point a, Point b, Point c, Point d)
        {
            sb.Append(P(a)).Append(" m ").Append(P(b)).Append(" l ").Append(P(c)).Append(" l ").Append(P(d)).Append(" l h f\n");
        }

        static string P(Point u) { return N(u.X) + " " + N(u.Y); }

        static string N(double v)
        {
            if (Math.Abs(v) < 1e-9) return "0";
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // A text string: UTF-16 with its byte order mark, written in hex (nothing in it needs escaping).
        static string Str(string s)
        {
            StringBuilder sb = new StringBuilder("<FEFF");
            foreach (char c in s ?? "") sb.Append(((int)c).ToString("X4"));
            return sb.Append('>').ToString();
        }

        // ---- Writing values back

        static string Ser(object v)
        {
            StringBuilder sb = new StringBuilder();
            Ser(sb, v);
            return sb.ToString();
        }

        static void Ser(StringBuilder sb, object v)
        {
            if (v == null || v is PNull) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            PNum n = v as PNum;
            if (n != null) { sb.Append(n.Raw); return; }
            PName nm = v as PName;
            if (nm != null) { Name(sb, nm.V); return; }
            PRef r = v as PRef;
            if (r != null) { sb.Append(r.N).Append(' ').Append(r.G).Append(" R"); return; }
            PStr s = v as PStr;
            if (s != null)
            {
                sb.Append('<');
                foreach (byte c in s.V) sb.Append(c.ToString("X2"));
                sb.Append('>');
                return;
            }
            List<object> a = v as List<object>;
            if (a != null)
            {
                sb.Append('[');
                for (int i = 0; i < a.Count; i++) { if (i > 0) sb.Append(' '); Ser(sb, a[i]); }
                sb.Append(']');
                return;
            }
            PDict d = v as PDict;
            if (d != null)
            {
                sb.Append("<<");
                foreach (KeyValuePair<string, object> kv in d.Items) { sb.Append(' '); Name(sb, kv.Key); sb.Append(' '); Ser(sb, kv.Value); }
                sb.Append(" >>");
                return;
            }
            throw new InvalidDataException("valor PDF desconocido");
        }

        static void Name(StringBuilder sb, string name)
        {
            sb.Append('/');
            foreach (char c in name)
            {
                if (c > 32 && c < 127 && "()<>[]{}/%#".IndexOf(c) < 0) sb.Append(c);
                else sb.Append('#').Append(((int)c & 0xFF).ToString("X2"));
            }
        }

        // ---- Reading the PDF

        sealed class PNull { public static readonly PNull Value = new PNull(); }
        sealed class PNum { public string Raw; public double V; public PNum(string raw, double v) { Raw = raw; V = v; } }
        sealed class PName { public string V; public PName(string v) { V = v; } }
        sealed class PStr { public byte[] V; public PStr(byte[] v) { V = v; } }
        sealed class PRef { public int N, G; public PRef(int n, int g) { N = n; G = g; } }
        sealed class PKeyword { public string V; public PKeyword(string v) { V = v; } }

        sealed class PDict
        {
            public readonly List<KeyValuePair<string, object>> Items = new List<KeyValuePair<string, object>>();

            public object Get(string key)
            {
                foreach (KeyValuePair<string, object> kv in Items) if (kv.Key == key) return kv.Value;
                return null;
            }

            public void Set(string key, object value)
            {
                for (int i = 0; i < Items.Count; i++)
                    if (Items[i].Key == key) { Items[i] = new KeyValuePair<string, object>(key, value); return; }
                Items.Add(new KeyValuePair<string, object>(key, value));
            }

            public PDict Copy()
            {
                PDict d = new PDict();
                d.Items.AddRange(Items);
                return d;
            }
        }

        sealed class PStream { public PDict Dict; public int Start, Length; }

        sealed class Lexer
        {
            readonly byte[] b;
            public int Pos;
            public Lexer(byte[] b, int pos) { this.b = b; Pos = pos; }

            static bool White(byte c) { return c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32; }
            static bool Delim(byte c) { return c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '%'; }

            public void Skip()
            {
                while (Pos < b.Length)
                {
                    byte c = b[Pos];
                    if (White(c)) { Pos++; continue; }
                    if (c == '%') { while (Pos < b.Length && b[Pos] != 10 && b[Pos] != 13) Pos++; continue; }
                    break;
                }
            }

            public bool At(string word)
            {
                Skip();
                if (Pos + word.Length > b.Length) return false;
                for (int i = 0; i < word.Length; i++) if (b[Pos + i] != word[i]) return false;
                return Pos + word.Length >= b.Length || White(b[Pos + word.Length]) || Delim(b[Pos + word.Length]);
            }

            string Token()
            {
                int s = Pos;
                while (Pos < b.Length && !White(b[Pos]) && !Delim(b[Pos])) Pos++;
                return Encoding.ASCII.GetString(b, s, Pos - s);
            }

            public object Read()
            {
                Skip();
                if (Pos >= b.Length) throw new InvalidDataException("fin inesperado");
                byte c = b[Pos];
                if (c == '/')
                {
                    Pos++;
                    int s = Pos;
                    StringBuilder sb = new StringBuilder();
                    while (Pos < b.Length && !White(b[Pos]) && !Delim(b[Pos]))
                    {
                        if (b[Pos] == '#' && Pos + 2 < b.Length) { sb.Append((char)Convert.ToByte(Encoding.ASCII.GetString(b, Pos + 1, 2), 16)); Pos += 3; continue; }
                        sb.Append((char)b[Pos]);
                        Pos++;
                    }
                    return new PName(sb.ToString());
                }
                if (c == '(') return Literal();
                if (c == '<')
                {
                    if (Pos + 1 < b.Length && b[Pos + 1] == '<')
                    {
                        Pos += 2;
                        PDict d = new PDict();
                        while (true)
                        {
                            Skip();
                            if (Pos + 1 < b.Length && b[Pos] == '>' && b[Pos + 1] == '>') { Pos += 2; return d; }
                            PName k = Read() as PName;
                            if (k == null) throw new InvalidDataException("clave de diccionario");
                            d.Items.Add(new KeyValuePair<string, object>(k.V, Read()));
                        }
                    }
                    Pos++;
                    List<byte> hex = new List<byte>();
                    int hi = -1;
                    while (Pos < b.Length && b[Pos] != '>')
                    {
                        int v = Hex(b[Pos++]);
                        if (v < 0) continue;
                        if (hi < 0) hi = v; else { hex.Add((byte)(hi * 16 + v)); hi = -1; }
                    }
                    if (hi >= 0) hex.Add((byte)(hi * 16));
                    Pos++;
                    return new PStr(hex.ToArray());
                }
                if (c == '[')
                {
                    Pos++;
                    List<object> a = new List<object>();
                    while (true)
                    {
                        Skip();
                        if (Pos < b.Length && b[Pos] == ']') { Pos++; return a; }
                        a.Add(Read());
                    }
                }
                if ((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.')
                {
                    string t = Token();
                    double v;
                    if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = 0;
                    PNum n = new PNum(t, v);
                    // "12 0 R": a reference.
                    if (t.IndexOf('.') < 0 && t[0] != '-' && t[0] != '+')
                    {
                        int save = Pos;
                        Skip();
                        if (Pos < b.Length && b[Pos] >= '0' && b[Pos] <= '9')
                        {
                            string g = Token();
                            Skip();
                            if (Pos < b.Length && b[Pos] == 'R' && (Pos + 1 >= b.Length || White(b[Pos + 1]) || Delim(b[Pos + 1])))
                            {
                                Pos++;
                                int gn;
                                if (int.TryParse(g, NumberStyles.Integer, CultureInfo.InvariantCulture, out gn)) return new PRef((int)v, gn);
                            }
                        }
                        Pos = save;
                    }
                    return n;
                }
                string w = Token();
                if (w.Length == 0) { Pos++; throw new InvalidDataException("car\u00E1cter inesperado"); }
                if (w == "true") return true;
                if (w == "false") return false;
                if (w == "null") return PNull.Value;
                return new PKeyword(w);
            }

            static int Hex(byte c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            PStr Literal()
            {
                Pos++;
                List<byte> o = new List<byte>();
                int depth = 1;
                while (Pos < b.Length)
                {
                    byte c = b[Pos++];
                    if (c == '\\' && Pos < b.Length)
                    {
                        byte e = b[Pos++];
                        switch (e)
                        {
                            case (byte)'n': o.Add(10); break;
                            case (byte)'r': o.Add(13); break;
                            case (byte)'t': o.Add(9); break;
                            case (byte)'b': o.Add(8); break;
                            case (byte)'f': o.Add(12); break;
                            case 13: if (Pos < b.Length && b[Pos] == 10) Pos++; break;
                            case 10: break;
                            default:
                                if (e >= '0' && e <= '7')
                                {
                                    int v = e - '0';
                                    for (int k = 0; k < 2 && Pos < b.Length && b[Pos] >= '0' && b[Pos] <= '7'; k++) v = v * 8 + (b[Pos++] - '0');
                                    o.Add((byte)v);
                                }
                                else o.Add(e);
                                break;
                        }
                        continue;
                    }
                    if (c == '(') depth++;
                    else if (c == ')' && --depth == 0) break;
                    o.Add(c);
                }
                return new PStr(o.ToArray());
            }
        }

        sealed class Entry { public int Type, A, B; }   // 1: offset A, generation B; 2: in object stream A at index B

        sealed class Doc
        {
            readonly byte[] b;
            readonly Dictionary<int, Entry> map = new Dictionary<int, Entry>();
            readonly Dictionary<int, Dictionary<int, object>> streams = new Dictionary<int, Dictionary<int, object>>();
            public PDict Trailer;
            public long StartXref;
            public bool StreamXref;
            public int Size, MaxObject;

            public Doc(byte[] pdf)
            {
                b = pdf;
                StartXref = FindStartXref();
                ReadXref((int)StartXref, new HashSet<int>(), true);
                if (Trailer == null || Trailer.Get("Root") == null) throw new InvalidDataException("sin cat\u00E1logo");
                object size = Trailer.Get("Size");
                Size = size is PNum ? (int)((PNum)size).V : 0;
                foreach (int k in map.Keys) MaxObject = Math.Max(MaxObject, k);
            }

            long FindStartXref()
            {
                int from = Math.Max(0, b.Length - 4096);
                byte[] key = Encoding.ASCII.GetBytes("startxref");
                for (int i = b.Length - key.Length; i >= from; i--)
                {
                    bool ok = true;
                    for (int k = 0; k < key.Length && ok; k++) ok = b[i + k] == key[k];
                    if (!ok) continue;
                    Lexer l = new Lexer(b, i + key.Length);
                    PNum n = l.Read() as PNum;
                    if (n == null) break;
                    return (long)n.V;
                }
                throw new InvalidDataException("sin startxref");
            }

            void ReadXref(int at, HashSet<int> seen, bool newest)
            {
                if (at <= 0 || at >= b.Length || !seen.Add(at)) return;
                Lexer l = new Lexer(b, at);
                PDict trailer;
                if (l.At("xref"))
                {
                    l.Pos += 4;
                    while (true)
                    {
                        if (l.At("trailer")) { l.Pos += 7; break; }
                        PNum start = l.Read() as PNum, count = l.Read() as PNum;
                        if (start == null || count == null) throw new InvalidDataException("tabla xref");
                        for (int i = 0; i < (int)count.V; i++)
                        {
                            PNum off = l.Read() as PNum, gen = l.Read() as PNum;
                            PKeyword kind = l.Read() as PKeyword;
                            if (off == null || gen == null || kind == null) throw new InvalidDataException("entrada xref");
                            int num = (int)start.V + i;
                            if (!map.ContainsKey(num)) map[num] = kind.V == "n" ? new Entry { Type = 1, A = (int)off.V, B = (int)gen.V } : new Entry { Type = 0 };
                        }
                    }
                    trailer = l.Read() as PDict;
                    if (trailer == null) throw new InvalidDataException("trailer");
                    if (newest) { Trailer = trailer; StreamXref = false; }
                    object stm = trailer.Get("XRefStm");
                    if (stm is PNum) ReadXref((int)((PNum)stm).V, seen, false);
                }
                else
                {
                    PStream s = Indirect(at) as PStream;
                    if (s == null || !(s.Dict.Get("Type") is PName) || ((PName)s.Dict.Get("Type")).V != "XRef") throw new InvalidDataException("xref en flujo");
                    trailer = s.Dict;
                    if (newest) { Trailer = trailer; StreamXref = true; }
                    byte[] data = Decode(s);
                    List<object> w = trailer.Get("W") as List<object>;
                    if (w == null || w.Count != 3) throw new InvalidDataException("/W");
                    int w0 = (int)((PNum)w[0]).V, w1 = (int)((PNum)w[1]).V, w2 = (int)((PNum)w[2]).V, row = w0 + w1 + w2;
                    List<object> index = trailer.Get("Index") as List<object>;
                    List<int> ix = new List<int>();
                    if (index == null) { ix.Add(0); ix.Add((int)((PNum)trailer.Get("Size")).V); }
                    else foreach (object o in index) ix.Add((int)((PNum)o).V);
                    int p = 0;
                    for (int k = 0; k + 1 < ix.Count; k += 2)
                        for (int i = 0; i < ix[k + 1] && p + row <= data.Length; i++, p += row)
                        {
                            int t = w0 == 0 ? 1 : Field(data, p, w0), a = Field(data, p + w0, w1), c = Field(data, p + w0 + w1, w2);
                            int num = ix[k] + i;
                            if (!map.ContainsKey(num)) map[num] = new Entry { Type = t, A = a, B = c };
                        }
                }
                object prev = trailer.Get("Prev");
                if (prev is PNum) ReadXref((int)((PNum)prev).V, seen, false);
            }

            static int Field(byte[] d, int at, int len)
            {
                int v = 0;
                for (int i = 0; i < len; i++) v = (v << 8) | d[at + i];
                return v;
            }

            // The object at a byte offset: "n g obj <value> [stream ... endstream] endobj".
            object Indirect(int at)
            {
                Lexer l = new Lexer(b, at);
                if (!(l.Read() is PNum) || !(l.Read() is PNum)) throw new InvalidDataException("objeto");
                PKeyword obj = l.Read() as PKeyword;
                if (obj == null || obj.V != "obj") throw new InvalidDataException("obj");
                object v = l.Read();
                PDict d = v as PDict;
                if (d == null || !l.At("stream")) return v;
                l.Pos += 6;
                if (l.Pos < b.Length && b[l.Pos] == 13) l.Pos++;
                if (l.Pos < b.Length && b[l.Pos] == 10) l.Pos++;
                object len = Resolve(d.Get("Length"));
                int n = len is PNum ? (int)((PNum)len).V : -1;
                if (n < 0 || l.Pos + n > b.Length) throw new InvalidDataException("/Length");
                return new PStream { Dict = d, Start = l.Pos, Length = n };
            }

            public object Resolve(object v)
            {
                for (int guard = 0; guard < 32; guard++)
                {
                    PRef r = v as PRef;
                    if (r == null) return v;
                    v = Get(r.N);
                }
                return null;
            }

            public object Get(int num)
            {
                Entry e;
                if (!map.TryGetValue(num, out e) || e.Type == 0) return null;
                if (e.Type == 1) return Indirect(e.A);
                if (e.Type != 2) return null;
                Dictionary<int, object> objs;
                if (!streams.TryGetValue(e.A, out objs)) streams[e.A] = objs = ObjStm(e.A);
                object o;
                return objs.TryGetValue(num, out o) ? o : null;
            }

            Dictionary<int, object> ObjStm(int num)
            {
                Dictionary<int, object> o = new Dictionary<int, object>();
                PStream s = Get(num) as PStream;
                if (s == null) throw new InvalidDataException("ObjStm");
                byte[] data = Decode(s);
                int n = (int)((PNum)s.Dict.Get("N")).V, first = (int)((PNum)s.Dict.Get("First")).V;
                Lexer head = new Lexer(data, 0);
                List<int[]> places = new List<int[]>();
                for (int i = 0; i < n; i++) places.Add(new int[] { (int)((PNum)head.Read()).V, (int)((PNum)head.Read()).V });
                foreach (int[] p in places) o[p[0]] = new Lexer(data, first + p[1]).Read();
                return o;
            }

            byte[] Decode(PStream s)
            {
                byte[] raw = new byte[s.Length];
                Buffer.BlockCopy(b, s.Start, raw, 0, s.Length);
                object f = Resolve(s.Dict.Get("Filter"));
                if (f is List<object>) f = ((List<object>)f).Count == 1 ? ((List<object>)f)[0] : f;
                if (f == null) return raw;
                PName fn = f as PName;
                if (fn == null || fn.V != "FlateDecode") throw new InvalidDataException("filtro");
                byte[] data = Inflate(raw);
                PDict parms = Resolve(s.Dict.Get("DecodeParms")) as PDict;
                if (parms == null && s.Dict.Get("DecodeParms") is List<object>) parms = Resolve(((List<object>)s.Dict.Get("DecodeParms"))[0]) as PDict;
                object pr = parms == null ? null : parms.Get("Predictor");
                int predictor = pr is PNum ? (int)((PNum)pr).V : 1;
                if (predictor < 10) { if (predictor != 1) throw new InvalidDataException("predictor"); return data; }
                object cols = parms.Get("Columns"), colors = parms.Get("Colors"), bpc = parms.Get("BitsPerComponent");
                int columns = cols is PNum ? (int)((PNum)cols).V : 1;
                int bpp = Math.Max(1, (colors is PNum ? (int)((PNum)colors).V : 1) * (bpc is PNum ? (int)((PNum)bpc).V : 8) / 8);
                return Png(data, columns * bpp, bpp);
            }

            static byte[] Inflate(byte[] raw)
            {
                int skip = raw.Length >= 2 && (raw[0] & 0x0F) == 8 && ((raw[0] << 8) | raw[1]) % 31 == 0 ? 2 : 0;
                using (MemoryStream src = new MemoryStream(raw, skip, raw.Length - skip))
                using (DeflateStream d = new DeflateStream(src, CompressionMode.Decompress))
                using (MemoryStream o = new MemoryStream())
                {
                    try { d.CopyTo(o); }
                    catch (InvalidDataException) { if (o.Length == 0) throw; }   // a truncated tail: what was read is kept
                    return o.ToArray();
                }
            }

            // PNG row filters (None, Sub, Up, Average, Paeth), one filter byte per row.
            static byte[] Png(byte[] d, int rowLen, int bpp)
            {
                int rows = d.Length / (rowLen + 1);
                byte[] o = new byte[rows * rowLen];
                for (int r = 0; r < rows; r++)
                {
                    int f = d[r * (rowLen + 1)], src = r * (rowLen + 1) + 1, dst = r * rowLen;
                    for (int i = 0; i < rowLen; i++)
                    {
                        int x = d[src + i];
                        int a = i >= bpp ? o[dst + i - bpp] : 0, up = r > 0 ? o[dst - rowLen + i] : 0, c = r > 0 && i >= bpp ? o[dst - rowLen + i - bpp] : 0;
                        int v;
                        switch (f)
                        {
                            case 1: v = x + a; break;
                            case 2: v = x + up; break;
                            case 3: v = x + (a + up) / 2; break;
                            case 4:
                                {
                                    int p = a + up - c, pa = Math.Abs(p - a), pb = Math.Abs(p - up), pc = Math.Abs(p - c);
                                    v = x + (pa <= pb && pa <= pc ? a : pb <= pc ? up : c);
                                    break;
                                }
                            default: v = x; break;
                        }
                        o[dst + i] = (byte)v;
                    }
                }
                return o;
            }

            // Every page in order, with what it inherits from the page tree (MediaBox, CropBox, Rotate).
            public List<PageInfo> Pages()
            {
                PDict root = Resolve(Trailer.Get("Root")) as PDict;
                if (root == null) throw new InvalidDataException("cat\u00E1logo");
                List<PageInfo> o = new List<PageInfo>();
                PRef top = root.Get("Pages") as PRef;
                if (top == null) throw new InvalidDataException("/Pages");
                Walk(top, null, null, null, o, new HashSet<int>(), 0);
                if (o.Count == 0) throw new InvalidDataException("sin p\u00E1ginas");
                return o;
            }

            void Walk(PRef node, object media, object crop, object rotate, List<PageInfo> o, HashSet<int> seen, int depth)
            {
                if (depth > 64 || !seen.Add(node.N)) return;
                PDict d = Get(node.N) as PDict;
                if (d == null) return;
                media = d.Get("MediaBox") ?? media;
                crop = d.Get("CropBox") ?? crop;
                rotate = d.Get("Rotate") ?? rotate;
                List<object> kids = Resolve(d.Get("Kids")) as List<object>;
                PName type = d.Get("Type") as PName;
                if (kids != null && (type == null || type.V != "Page"))
                {
                    foreach (object k in kids) if (k is PRef) Walk((PRef)k, media, crop, rotate, o, seen, depth + 1);
                    return;
                }
                double[] mb = Box(media) ?? new double[] { 0, 0, 612, 792 }, cb = Box(crop) ?? mb;
                // The crop box never reaches past the media box.
                cb = new double[] { Math.Max(cb[0], mb[0]), Math.Max(cb[1], mb[1]), Math.Min(cb[2], mb[2]), Math.Min(cb[3], mb[3]) };
                if (cb[2] <= cb[0] || cb[3] <= cb[1]) cb = mb;
                object rv = Resolve(rotate);
                int rot = rv is PNum ? (int)((PNum)rv).V : 0;
                rot = ((rot % 360) + 360) % 360;
                if (rot % 90 != 0) rot = 0;
                Entry e;
                int gen = map.TryGetValue(node.N, out e) && e.Type == 1 ? e.B : 0;
                o.Add(new PageInfo { Num = node.N, Gen = gen, Dict = d, X0 = cb[0], Y0 = cb[1], X1 = cb[2], Y1 = cb[3], Rotate = rot });
            }

            public double[] Box(object v)
            {
                List<object> a = Resolve(v) as List<object>;
                if (a == null || a.Count != 4) return null;
                double[] n = new double[4];
                for (int i = 0; i < 4; i++)
                {
                    PNum x = Resolve(a[i]) as PNum;
                    if (x == null) return null;
                    n[i] = x.V;
                }
                return new double[] { Math.Min(n[0], n[2]), Math.Min(n[1], n[3]), Math.Max(n[0], n[2]), Math.Max(n[1], n[3]) };
            }
        }
    }
}
