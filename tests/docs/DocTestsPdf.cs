// Tests of the PDF viewer: Windows' engine opening and drawing pages, page geometry with rotation, the text read from the
// pages, the marks and the PDF written with them. The PDFs are made here, so nothing real is needed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace Stackshot.Tests
{
    static partial class DocTests
    {
        // ---- Making PDFs

        sealed class FakePage
        {
            public double W = 595, H = 842;
            public int Rotate;
            public string Text = "Hola mundo";
            public double[] CropBox;    // x0 y0 x1 y1, or null
        }

        // A small PDF with Helvetica text, classic cross-reference table (stream = false) or a cross-reference stream with
        // the pages inside an object stream (stream = true, as PDF 1.5 writers do).
        static byte[] MakePdf(IList<FakePage> pages, bool stream)
        {
            List<string> objs = new List<string>();     // object n is objs[n - 1]
            int n = pages.Count;
            // 1 catalog, 2 pages, 3 font, then per page: page, contents
            objs.Add("<< /Type /Catalog /Pages 2 0 R >>");
            StringBuilder kids = new StringBuilder();
            for (int i = 0; i < n; i++) kids.Append(4 + i * 2).Append(" 0 R ");
            objs.Add("<< /Type /Pages /Kids [" + kids.ToString().Trim() + "] /Count " + n + " >>");
            objs.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            for (int i = 0; i < n; i++)
            {
                FakePage p = pages[i];
                string box = "/MediaBox [0 0 " + F(p.W) + " " + F(p.H) + "]";
                if (p.CropBox != null) box += " /CropBox [" + F(p.CropBox[0]) + " " + F(p.CropBox[1]) + " " + F(p.CropBox[2]) + " " + F(p.CropBox[3]) + "]";
                objs.Add("<< /Type /Page /Parent 2 0 R " + box + (p.Rotate != 0 ? " /Rotate " + p.Rotate : "") +
                         " /Resources << /Font << /F1 3 0 R >> >> /Contents " + (5 + i * 2) + " 0 R >>");
                string content = p.Text == null ? "" : "BT /F1 40 Tf 60 " + F(p.H - 120) + " Td (" + p.Text + ") Tj ET\nBT /F1 16 Tf 60 " + F(p.H - 170) + " Td (Pagina " + (i + 1) + " de prueba con texto) Tj ET";
                objs.Add("<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "\nendstream");
            }
            MemoryStream ms = new MemoryStream();
            Action<string> w = delegate(string s) { byte[] b = Encoding.GetEncoding(1252).GetBytes(s); ms.Write(b, 0, b.Length); };
            w("%PDF-" + (stream ? "1.5" : "1.4") + "\n%\u00E2\u00E3\u00CF\u00D3\n");
            long[] at = new long[objs.Count + 2];
            if (!stream)
            {
                for (int i = 0; i < objs.Count; i++) { at[i + 1] = ms.Position; w((i + 1) + " 0 obj\n" + objs[i] + "\nendobj\n"); }
                long x = ms.Position;
                w("xref\n0 " + (objs.Count + 1) + "\n0000000000 65535 f \n");
                for (int i = 1; i <= objs.Count; i++) w(at[i].ToString("D10") + " 00000 n \n");
                w("trailer\n<< /Size " + (objs.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + x + "\n%%EOF\n");
                return ms.ToArray();
            }
            // Page dictionaries (no streams) go into one object stream; the rest stay plain objects.
            int objStm = objs.Count + 1, xrefObj = objs.Count + 2;
            List<int> packed = new List<int>();
            for (int i = 0; i < n; i++) packed.Add(4 + i * 2);
            int[] type = new int[xrefObj + 1], f2 = new int[xrefObj + 1], f3 = new int[xrefObj + 1];
            for (int i = 0; i < objs.Count; i++)
            {
                int num = i + 1;
                if (packed.Contains(num)) continue;
                type[num] = 1; f2[num] = (int)ms.Position;
                w(num + " 0 obj\n" + objs[i] + "\nendobj\n");
            }
            StringBuilder head = new StringBuilder(), body = new StringBuilder();
            for (int k = 0; k < packed.Count; k++)
            {
                head.Append(packed[k]).Append(' ').Append(body.Length).Append(' ');
                body.Append(objs[packed[k] - 1]).Append('\n');
                type[packed[k]] = 2; f2[packed[k]] = objStm; f3[packed[k]] = k;
            }
            string plain = head.ToString() + body.ToString();
            byte[] packedBytes = Deflate(Encoding.ASCII.GetBytes(plain));
            type[objStm] = 1; f2[objStm] = (int)ms.Position;
            w(objStm + " 0 obj\n<< /Type /ObjStm /N " + packed.Count + " /First " + head.Length + " /Filter /FlateDecode /Length " + packedBytes.Length + " >>\nstream\n");
            ms.Write(packedBytes, 0, packedBytes.Length);
            w("\nendstream\nendobj\n");
            type[xrefObj] = 1; f2[xrefObj] = (int)ms.Position;
            // Rows of 1 + 4 + 2 bytes with the PNG "Up" predictor, as most writers do.
            int rowLen = 7;
            byte[] rows = new byte[(xrefObj + 1) * rowLen];
            for (int i = 0; i <= xrefObj; i++)
            {
                int t = i == 0 ? 0 : type[i], a = i == 0 ? 0 : f2[i], c = i == 0 ? 65535 : f3[i];
                rows[i * rowLen] = (byte)t;
                rows[i * rowLen + 1] = (byte)(a >> 24); rows[i * rowLen + 2] = (byte)(a >> 16); rows[i * rowLen + 3] = (byte)(a >> 8); rows[i * rowLen + 4] = (byte)a;
                rows[i * rowLen + 5] = (byte)(c >> 8); rows[i * rowLen + 6] = (byte)c;
            }
            byte[] predicted = new byte[(xrefObj + 1) * (rowLen + 1)];
            for (int i = 0; i <= xrefObj; i++)
            {
                predicted[i * (rowLen + 1)] = 2;
                for (int j = 0; j < rowLen; j++)
                {
                    byte up = i == 0 ? (byte)0 : rows[(i - 1) * rowLen + j];
                    predicted[i * (rowLen + 1) + 1 + j] = (byte)(rows[i * rowLen + j] - up);
                }
            }
            byte[] xs = Deflate(predicted);
            w(xrefObj + " 0 obj\n<< /Type /XRef /Size " + (xrefObj + 1) + " /W [1 4 2] /Root 1 0 R /Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 7 >> /Length " + xs.Length + " >>\nstream\n");
            ms.Write(xs, 0, xs.Length);
            w("\nendstream\nendobj\nstartxref\n" + f2[xrefObj] + "\n%%EOF\n");
            return ms.ToArray();
        }

        static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        // zlib: header, raw deflate, Adler-32.
        static byte[] Deflate(byte[] data)
        {
            MemoryStream o = new MemoryStream();
            o.WriteByte(0x78); o.WriteByte(0x9C);
            using (System.IO.Compression.DeflateStream d = new System.IO.Compression.DeflateStream(o, System.IO.Compression.CompressionMode.Compress, true))
                d.Write(data, 0, data.Length);
            uint a = 1, b = 0;
            foreach (byte x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
            uint ad = (b << 16) | a;
            o.WriteByte((byte)(ad >> 24)); o.WriteByte((byte)(ad >> 16)); o.WriteByte((byte)(ad >> 8)); o.WriteByte((byte)ad);
            return o.ToArray();
        }

        static string PdfFile(string name, IList<FakePage> pages, bool stream)
        {
            string dir = WorkDir("pdf");
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, MakePdf(pages, stream));
            return path;
        }

        static PdfEngine OpenPdf(string path)
        {
            string err;
            PdfEngine e = PdfEngine.Open(path, null, out err);
            Check(e != null, "open " + Path.GetFileName(path) + ": " + err);
            return e;
        }

        // Dark pixels in a page drawing: the text is there.
        static int Ink(System.Drawing.Bitmap b)
        {
            int dark = 0;
            for (int y = 0; y < b.Height; y += 2)
                for (int x = 0; x < b.Width; x += 2)
                    if (b.GetPixel(x, y).GetBrightness() < 0.4f) dark++;
            return dark;
        }

        // ---- Suite

        static void PdfSuite()
        {
            const string S = "pdf";
            Test(S, "engine-opens-and-sizes", delegate
            {
                string path = PdfFile("tres.pdf", new List<FakePage> { new FakePage(), new FakePage { W = 842, H = 595 }, new FakePage { W = 300, H = 300 } }, false);
                using (PdfEngine e = OpenPdf(path))
                {
                    Check(e.Pages == 3, "pages " + e.Pages);
                    Check(Math.Abs(e.Sizes[0].Width - 595 * 96 / 72.0) < 1.5 && Math.Abs(e.Sizes[0].Height - 842 * 96 / 72.0) < 1.5, "A4 in DIPs: " + e.Sizes[0]);
                    Check(e.Sizes[1].Width > e.Sizes[1].Height, "landscape: " + e.Sizes[1]);
                    Note("A4 " + e.Sizes[0] + ", landscape " + e.Sizes[1]);
                }
            });

            Test(S, "engine-draws-text", delegate
            {
                string path = PdfFile("hola.pdf", new List<FakePage> { new FakePage() }, false);
                using (PdfEngine e = OpenPdf(path))
                using (System.Drawing.Bitmap b = e.RenderGdi(0, 600, 849, 10000))
                {
                    Check(b != null, "no drawing");
                    Check(b.Width == 600 && b.Height == 849, "size " + b.Width + "x" + b.Height);
                    int ink = Ink(b);
                    Check(ink > 200, "no text drawn: " + ink);
                    Note("dark samples " + ink);
                }
            });

            Test(S, "engine-rotation-turns-size", delegate
            {
                string path = PdfFile("girada.pdf", new List<FakePage> { new FakePage { Rotate = 90 } }, false);
                using (PdfEngine e = OpenPdf(path))
                {
                    Check(e.Sizes[0].Width > e.Sizes[0].Height, "a portrait page turned 90 shows landscape: " + e.Sizes[0]);
                    Note("rotated " + e.Sizes[0]);
                }
            });

            Test(S, "engine-shows-the-crop-box", delegate
            {
                string path = PdfFile("recorte.pdf", new List<FakePage> { new FakePage { CropBox = new double[] { 50, 100, 350, 500 } } }, false);
                using (PdfEngine e = OpenPdf(path))
                {
                    Check(Math.Abs(e.Sizes[0].Width - 300 * 96 / 72.0) < 1.5 && Math.Abs(e.Sizes[0].Height - 400 * 96 / 72.0) < 1.5, "crop box size: " + e.Sizes[0]);
                    Note("cropped " + e.Sizes[0]);
                }
            });

            Test(S, "engine-opens-xref-streams", delegate
            {
                string path = PdfFile("moderno.pdf", new List<FakePage> { new FakePage(), new FakePage { Text = "Segunda" } }, true);
                using (PdfEngine e = OpenPdf(path))
                using (System.Drawing.Bitmap b = e.RenderGdi(1, 400, 566, 10000))
                {
                    Check(e.Pages == 2, "pages " + e.Pages);
                    Check(b != null && Ink(b) > 100, "page 2 drawn");
                }
            });

            Test(S, "engine-refuses-garbage", delegate
            {
                string path = Path.Combine(WorkDir("pdf"), "roto.pdf");
                File.WriteAllText(path, "%PDF-1.4\nesto no es un pdf\n");
                string err;
                PdfEngine e = PdfEngine.Open(path, null, out err);
                Check(e == null, "garbage opened");
                Check(!string.IsNullOrEmpty(err), "no reason");
                Note(err);
            });

            Test(S, "engine-render-async-and-tile", delegate
            {
                string path = PdfFile("tile.pdf", new List<FakePage> { new FakePage() }, false);
                using (PdfEngine e = OpenPdf(path))
                {
                    System.Windows.Media.Imaging.BitmapSource got = null;
                    ManualResetEvent done = new ManualResetEvent(false);
                    // The top-left quarter of the page, at 400 x 400.
                    System.Windows.Rect src = new System.Windows.Rect(0, 0, e.Sizes[0].Width / 2, e.Sizes[0].Height / 2);
                    e.Render(0, 400, 400, src, 0, "t", null, delegate(System.Windows.Media.Imaging.BitmapSource b) { got = b; done.Set(); });
                    Check(done.WaitOne(10000), "no answer");
                    Check(got != null && got.PixelWidth == 400 && got.PixelHeight == 400, "tile size");
                    Check(got.IsFrozen, "frozen for any thread");
                }
            });

            PdfModelTests(S);
            PdfWriterTests(S);
            PdfWindowTests(S);
        }

        // ---- Marks, text and reading

        static PdfPageText Words(params string[] lines)
        {
            List<PdfWord> w = new List<PdfWord>();
            for (int l = 0; l < lines.Length; l++)
            {
                double x = 0.1;
                foreach (string t in lines[l].Split(' '))
                {
                    double width = 0.012 * t.Length;
                    w.Add(new PdfWord { Text = t, Line = l, Box = new System.Windows.Rect(x, 0.1 + l * 0.03, width, 0.02) });
                    x += width + 0.01;
                }
            }
            return new PdfPageText(w);
        }

        static void PdfModelTests(string S)
        {
            Test(S, "text-find-ignores-case-and-accents", delegate
            {
                PdfPageText t = Words("El Cami\u00F3n lleg\u00F3 tarde", "camion de vuelta");
                List<int[]> a = t.Find("CAMION");
                Check(a.Count == 2, "camion x2: " + a.Count);
                Check(a[0][0] == 1 && a[0][1] == 1, "first at word 1");
                List<int[]> b = t.Find("llego  tarde");
                Check(b.Count == 1 && b[0][0] == 2 && b[0][1] == 3, "two words: " + (b.Count > 0 ? b[0][0] + "-" + b[0][1] : "none"));
                List<int[]> c = t.Find("tarde camion");
                Check(c.Count == 1 && c[0][0] == 3 && c[0][1] == 4, "across a line break");
                Check(t.Find("").Count == 0 && t.Find("zzz").Count == 0, "empty and missing");
            });

            Test(S, "text-between-lines-and-boxes", delegate
            {
                PdfPageText t = Words("uno dos tres", "cuatro cinco");
                Same("dos tres\ncuatro", t.Between(1, 3), "between");
                Same("dos tres\ncuatro", t.Between(3, 1), "between reversed");
                List<System.Windows.Rect> boxes = t.LineBoxes(1, 4);
                Check(boxes.Count == 2, "one box per line: " + boxes.Count);
                Check(t.Hit(new System.Windows.Point(0.105, 0.11), false) == 0, "hit first word");
                Check(t.Hit(new System.Windows.Point(0.9, 0.9), false) == -1, "miss far away");
                Check(t.Hit(new System.Windows.Point(0.9, 0.135), true) == 4, "near: last word of the second line");
                int a, b;
                Check(t.Inside(new System.Windows.Rect(0, 0.125, 1, 0.03), out a, out b) && a == 3 && b == 4, "zone covers the second line");
            });

            Test(S, "marks-undo-redo-and-save", delegate
            {
                PdfMarks m = new PdfMarks();
                int changes = 0;
                m.Changed += delegate { changes++; };
                PdfMark h = new PdfMark { Kind = PdfMarkKind.Highlight, Page = 2, Color = PdfMarks.Colors[1], Text = "algo \"citado\"\nen dos l\u00EDneas", Comment = "mi nota \u00F1" };
                h.Boxes.Add(new System.Windows.Rect(0.1, 0.2, 0.3, 0.02));
                m.Add(h);
                PdfMark ink = new PdfMark { Kind = PdfMarkKind.Ink, Page = 0, Color = PdfMarks.Colors[5], Width = 0.006 };
                ink.Strokes.Add(new List<System.Windows.Point> { new System.Windows.Point(0.1, 0.1), new System.Windows.Point(0.2, 0.25) });
                m.Add(ink);
                Check(m.All.Count == 2 && changes == 2, "added");
                PdfMark x = m.Find(h.Id).Clone();
                x.Color = PdfMarks.Colors[3];
                m.Update(x);
                Check(m.Find(h.Id).Color == PdfMarks.Colors[3], "recoloured");
                m.Remove(m.Find(ink.Id));
                Check(m.All.Count == 1, "removed");
                Check(m.Undo() && m.All.Count == 2, "undo remove");
                Check(m.Undo() && m.Find(h.Id).Color == PdfMarks.Colors[1], "undo colour");
                Check(m.Redo() && m.Find(h.Id).Color == PdfMarks.Colors[3], "redo colour");
                Check(m.All.IndexOf(m.Find(h.Id)) == 0, "a changed mark keeps its place");
                string file = Path.Combine(WorkDir("pdf"), "marks.json");
                m.Save(file);
                PdfMarks back = PdfMarks.Load(file);
                Check(back.All.Count == 2, "loaded " + back.All.Count);
                PdfMark bh = back.Find(h.Id), bi = back.Find(ink.Id);
                Check(bh != null && bh.Text == h.Text && bh.Comment == h.Comment && bh.Page == 2 && bh.Color == PdfMarks.Colors[3], "highlight round trip");
                Check(Math.Abs(bh.Boxes[0].Width - 0.3) < 1e-4, "box round trip");
                Check(bi != null && bi.Strokes.Count == 1 && bi.Strokes[0].Count == 2 && Math.Abs(bi.Width - 0.006) < 1e-6, "ink round trip");
                m.Clear();
                m.Save(file);
                Check(!File.Exists(file), "no marks, no file");
            });

            Test(S, "reader-reads-the-page", delegate
            {
                string path = PdfFile("ocr.pdf", new List<FakePage> { new FakePage { Text = "Factura de prueba" } }, false);
                using (PdfEngine e = OpenPdf(path))
                {
                    string cache = Path.Combine(WorkDir("pdf"), "text.json");
                    if (File.Exists(cache)) File.Delete(cache);
                    using (PdfReader r = new PdfReader(e, cache, Dispatcher.CurrentDispatcher))
                    {
                        r.Want(0);
                        Check(Until(delegate { return r.Get(0) != null || r.Error != null; }, 30000), "never read");
                        if (r.Error != null) Skip(r.Error);
                        PdfPageText t = r.Get(0);
                        Check(t.Find("factura").Count == 1, "OCR found the title: " + t.All);
                        Note(t.Words.Count + " words: " + t.All.Replace('\n', '|'));
                    }
                    Pump(300);
                    Check(File.Exists(cache), "text kept for next time");
                    using (PdfReader again = new PdfReader(e, cache, Dispatcher.CurrentDispatcher))
                        Check(again.Get(0) != null && again.Get(0).Find("factura").Count == 1, "read back from the cache at once");
                }
            });
        }

        // ---- Writing the marks into the PDF

        static PdfMark Hi(int page, double x, double y, double w, double h)
        {
            PdfMark m = new PdfMark { Kind = PdfMarkKind.Highlight, Page = page, Color = PdfMarks.Colors[0], Text = "zona" };
            m.Boxes.Add(new System.Windows.Rect(x, y, w, h));
            return m;
        }

        // The colour of the page drawing at a point given as fractions of the page shown.
        static System.Drawing.Color At(System.Drawing.Bitmap b, double u, double v)
        {
            return b.GetPixel(Math.Min(b.Width - 1, (int)(u * b.Width)), Math.Min(b.Height - 1, (int)(v * b.Height)));
        }

        static bool Yellow(System.Drawing.Color c) { return c.R > 200 && c.G > 170 && c.B < 140; }
        static bool White(System.Drawing.Color c) { return c.R > 235 && c.G > 235 && c.B > 235; }

        static void PdfWriterTests(string S)
        {
            foreach (bool stream in new bool[] { false, true })
            {
                bool xs = stream;
                Test(S, "writer-" + (xs ? "xref-stream" : "classic") + "-keeps-the-original-and-draws", delegate
                {
                    string path = PdfFile(xs ? "w-stream.pdf" : "w-classic.pdf", new List<FakePage> { new FakePage(), new FakePage { Text = "Dos" } }, xs);
                    byte[] before = File.ReadAllBytes(path);
                    List<PdfMark> marks = new List<PdfMark> { Hi(0, 0.2, 0.3, 0.4, 0.05) };
                    PdfMark u = new PdfMark { Kind = PdfMarkKind.Underline, Page = 1, Color = PdfMarks.Colors[5], Text = "x" };
                    u.Boxes.Add(new System.Windows.Rect(0.1, 0.5, 0.5, 0.04));
                    marks.Add(u);
                    PdfMark note = new PdfMark { Kind = PdfMarkKind.Note, Page = 1, Color = PdfMarks.Colors[0], Text = "Revisar esto \u00F1", At = new System.Windows.Point(0.7, 0.1) };
                    marks.Add(note);
                    PdfMark ink = new PdfMark { Kind = PdfMarkKind.Ink, Page = 1, Color = PdfMarks.Colors[2], Width = 0.01 };
                    ink.Strokes.Add(new List<System.Windows.Point> { new System.Windows.Point(0.2, 0.8), new System.Windows.Point(0.6, 0.85) });
                    marks.Add(ink);
                    string target = Path.Combine(WorkDir("pdf"), "salida-" + (xs ? "s" : "c") + ".pdf");
                    string err = PdfAnnotWriter.WriteVerified(path, target, marks, "Prueba");
                    Check(err == null, "write: " + err);
                    byte[] after = File.ReadAllBytes(target);
                    Check(after.Length > before.Length, "grew");
                    for (int i = 0; i < before.Length; i++) if (after[i] != before[i]) throw new TestFailed("original byte " + i + " changed");
                    using (PdfEngine e = OpenPdf(target))
                    {
                        Check(e.Pages == 2, "pages " + e.Pages);
                        using (System.Drawing.Bitmap b = e.RenderGdi(0, 600, 849, 10000))
                        {
                            Check(Yellow(At(b, 0.4, 0.325)), "highlight drawn where it was: " + At(b, 0.4, 0.325));
                            Check(White(At(b, 0.4, 0.45)), "nothing below it: " + At(b, 0.4, 0.45));
                        }
                        using (System.Drawing.Bitmap b = e.RenderGdi(1, 600, 849, 10000))
                        {
                            bool red = false;
                            for (double v = 0.525; v <= 0.545 && !red; v += 0.002) { System.Drawing.Color c = At(b, 0.3, v); red = c.R > 180 && c.G < 120 && c.B < 120; }
                            Check(red, "underline drawn along the bottom of its box");
                            System.Drawing.Color blue = At(b, 0.4, 0.825);
                            Check(blue.B > 150 && blue.R < 120, "ink drawn: " + blue);
                        }
                    }
                    // Windows' engine leaves sticky notes out: Stackshot reads them back from the file, where they were.
                    List<PdfMark> notes = PdfAnnotWriter.ReadNotes(after);
                    Check(notes.Count == 1, "one note in the file: " + notes.Count);
                    Check(notes[0].Page == 1 && notes[0].Text == "Revisar esto \u00F1" && notes[0].Author == "Prueba" && notes[0].InFile, "note text and author: " + notes[0].Text + " / " + notes[0].Author);
                    Check(Math.Abs(notes[0].At.X - 0.7) < 0.005 && Math.Abs(notes[0].At.Y - 0.1) < 0.005, "note where it was: " + notes[0].At);
                    string text = Encoding.GetEncoding(28591).GetString(after, before.Length, after.Length - before.Length);
                    Check(text.Contains("/Subtype /Highlight") && text.Contains("/QuadPoints") && text.Contains("/Subtype /Text") && text.Contains("/Subtype /Ink") && text.Contains("/Subtype /Underline"), "real annotations");
                    Check(text.Contains("/Prev "), "chained to the original");
                    Put("bytes_added", after.Length - before.Length);
                });
            }

            Test(S, "writer-maps-turned-and-cropped-pages", delegate
            {
                List<string> bad = new List<string>();
                foreach (int rot in new int[] { 0, 90, 180, 270 })
                    foreach (bool crop in new bool[] { false, true })
                    {
                        FakePage p = new FakePage { Rotate = rot, Text = null };
                        if (crop) p.CropBox = new double[] { 40, 60, 500, 700 };
                        string path = PdfFile("rot" + rot + (crop ? "c" : "") + ".pdf", new List<FakePage> { p }, false);
                        string target = Path.Combine(WorkDir("pdf"), "rot" + rot + (crop ? "c" : "") + "-m.pdf");
                        // A box near the top left as shown: wherever the page's own coordinates put it. And a note, which
                        // must come back to the same place on the turned page.
                        PdfMark note = new PdfMark { Kind = PdfMarkKind.Note, Page = 0, Color = PdfMarks.Colors[0], Text = "n", At = new System.Windows.Point(0.6, 0.5) };
                        string err = PdfAnnotWriter.WriteVerified(path, target, new List<PdfMark> { Hi(0, 0.1, 0.15, 0.3, 0.06), note }, "Prueba");
                        if (err != null) { bad.Add(rot + (crop ? "c" : "") + ": " + err); continue; }
                        List<PdfMark> back = PdfAnnotWriter.ReadNotes(File.ReadAllBytes(target));
                        if (back.Count != 1 || Math.Abs(back[0].At.X - 0.6) > 0.01 || Math.Abs(back[0].At.Y - 0.5) > 0.01) bad.Add(rot + (crop ? "c" : "") + " note " + (back.Count == 1 ? back[0].At.ToString() : back.Count + " notes"));
                        using (PdfEngine e = OpenPdf(target))
                        {
                            System.Windows.Size s = e.Sizes[0];
                            int w = 500, h = (int)(500 * s.Height / s.Width);
                            using (System.Drawing.Bitmap b = e.RenderGdi(0, w, h, 10000))
                            {
                                if (!Yellow(At(b, 0.25, 0.18))) bad.Add(rot + (crop ? "c" : "") + " centre " + At(b, 0.25, 0.18));
                                if (!White(At(b, 0.25, 0.4)) || !White(At(b, 0.7, 0.18))) bad.Add(rot + (crop ? "c" : "") + " spilled");
                            }
                        }
                    }
                Check(bad.Count == 0, string.Join("; ", bad.ToArray()));
            });

            Test(S, "writer-twice-in-a-row", delegate
            {
                string path = PdfFile("dos-veces.pdf", new List<FakePage> { new FakePage() }, true);
                string one = Path.Combine(WorkDir("pdf"), "uno.pdf"), two = Path.Combine(WorkDir("pdf"), "dos.pdf");
                Check(PdfAnnotWriter.WriteVerified(path, one, new List<PdfMark> { Hi(0, 0.1, 0.3, 0.3, 0.04) }, "a") == null, "first");
                Check(PdfAnnotWriter.WriteVerified(one, two, new List<PdfMark> { Hi(0, 0.1, 0.6, 0.3, 0.04) }, "b") == null, "second");
                using (PdfEngine e = OpenPdf(two))
                using (System.Drawing.Bitmap b = e.RenderGdi(0, 600, 849, 10000))
                {
                    Check(Yellow(At(b, 0.25, 0.32)), "first mark still there");
                    Check(Yellow(At(b, 0.25, 0.62)), "second mark there");
                }
            });

            Test(S, "writer-refuses-encrypted-and-garbage", delegate
            {
                byte[] pdf = MakePdf(new List<FakePage> { new FakePage() }, false);
                string s = Encoding.GetEncoding(28591).GetString(pdf).Replace("/Root 1 0 R >>", "/Root 1 0 R /Encrypt 99 0 R >>");
                string path = Path.Combine(WorkDir("pdf"), "cifrado.pdf");
                File.WriteAllBytes(path, Encoding.GetEncoding(28591).GetBytes(s));
                string err = PdfAnnotWriter.WriteVerified(path, Path.Combine(WorkDir("pdf"), "x.pdf"), new List<PdfMark> { Hi(0, 0.1, 0.1, 0.2, 0.05) }, "a");
                Check(err != null && err.Contains("cifrado"), "encrypted refused: " + err);
                string junk = Path.Combine(WorkDir("pdf"), "basura.pdf");
                File.WriteAllText(junk, "%PDF-1.4\nnada\n");
                err = PdfAnnotWriter.WriteVerified(junk, Path.Combine(WorkDir("pdf"), "y.pdf"), new List<PdfMark> { Hi(0, 0.1, 0.1, 0.2, 0.05) }, "a");
                Check(err != null && !File.Exists(Path.Combine(WorkDir("pdf"), "y.pdf")), "garbage refused, nothing written: " + err);
            });
        }

        // ---- The window

        static PdfWindow NewPdfWindow()
        {
            System.Reflection.ConstructorInfo ci = typeof(PdfWindow).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
            PdfWindow w = (PdfWindow)ci.Invoke(new object[] { null });
            w.Width = 1100;
            w.Height = 760;
            Park(w);
            Pump(150);
            return w;
        }

        static void LoadPdf(PdfWindow w, string path)
        {
            typeof(PdfWindow).GetMethod("Load", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, new object[] { path, null });
            Check(Until(delegate { return w.Engine != null && !w.Loading; }, 15000), "did not load");
            Pump(400);
        }

        static void PdfWindowTests(string S)
        {
            Test(S, "window-opens-draws-and-moves", delegate
            {
                List<FakePage> pages = new List<FakePage>();
                for (int i = 0; i < 12; i++) pages.Add(new FakePage { Text = "Capitulo " + (i + 1) });
                string path = PdfFile("libro.pdf", pages, true);
                PdfWindow w = NewPdfWindow();
                try
                {
                    LoadPdf(w, path);
                    Check(w.View.Pages == 12, "pages " + w.View.Pages);
                    Check(w.Title == "libro.pdf", "title " + w.Title);
                    System.Windows.Media.Imaging.BitmapSource shot = Snap(w.Content as System.Windows.FrameworkElement);
                    SavePng(shot, "pdf-window.png");
                    Check(Variety(shot) > 20, "drawn: variety " + Variety(shot));
                    w.View.GoTo(7, false);
                    Pump(300);
                    Check(w.View.CurrentPage == 7, "went to page 8: " + w.View.CurrentPage);
                    double z = w.View.Zoom;
                    w.View.ZoomStep(1);
                    Check(w.View.Zoom > z, "zoom in");
                    w.View.SetFit(2);
                    Pump(200);
                    Check(w.View.CurrentPage == 7, "whole page keeps the page: " + w.View.CurrentPage);
                    w.View.SetZoom(4, null);
                    Pump(600);
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "pdf-window-zoom.png");
                    Note("zoom " + Math.Round(w.View.Zoom, 2) + ", page " + (w.View.CurrentPage + 1));
                }
                finally { w.Close(); Pump(100); }
            });

            Test(S, "window-marks-and-search", delegate
            {
                string path = PdfFile("marcas.pdf", new List<FakePage> { new FakePage { Text = "Contrato de servicio" }, new FakePage { Text = "Anexo del contrato" } }, false);
                PdfWindow w = NewPdfWindow();
                try
                {
                    LoadPdf(w, path);
                    w.Reader.Want(0);
                    w.Reader.Want(1);
                    Check(Until(delegate { return (w.Reader.Get(0) != null && w.Reader.Get(1) != null) || w.Reader.Error != null; }, 40000), "text not read");
                    if (w.Reader.Error != null) Skip(w.Reader.Error);
                    PdfPageText t = w.Reader.Get(0);
                    List<int[]> f = t.Find("contrato");
                    Check(f.Count == 1, "OCR word: " + t.All);
                    w.View.SelectWords(0, f[0][0], f[0][1]);
                    Check(w.View.SelectedText.ToLowerInvariant().Contains("contrato"), "selected " + w.View.SelectedText);
                    w.SetTool(PdfTool.Highlight);
                    Check(w.Marks.All.Count == 1 && w.Marks.All[0].Kind == PdfMarkKind.Highlight, "the selection was highlighted");
                    Check(w.View.Tool == PdfTool.Select, "picking a tool with words selected marks them and keeps selecting");
                    w.SetTool(PdfTool.Underline);
                    Check(w.View.Tool == PdfTool.Underline, "tool chosen");
                    w.SetTool(PdfTool.Select);
                    Check(w.Marks.Undo() && w.Marks.All.Count == 0 && w.Marks.Redo(), "undo and redo");
                    string sum = w.Summary();
                    Check(sum.Contains("## P\u00E1gina 1") && sum.ToLowerInvariant().Contains("contrato"), "summary: " + sum);
                    SearchField sf = (SearchField)typeof(PdfWindow).GetField("search", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(w);
                    sf.Text = "contrato";
                    Check(Until(delegate { return w.View.Hits.Count == 2; }, 5000), "two hits: " + w.View.Hits.Count);
                    Check(w.View.HitIndex >= 0, "a hit shown");
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "pdf-window-marks.png");
                }
                finally { w.Close(); Pump(100); }
            });

            Test(S, "window-wrong-file-says-why", delegate
            {
                string junk = Path.Combine(WorkDir("pdf"), "no-es.pdf");
                File.WriteAllText(junk, "hola");
                PdfWindow w = NewPdfWindow();
                try
                {
                    typeof(PdfWindow).GetMethod("Load", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, new object[] { junk, null });
                    Check(Until(delegate { return !w.Loading; }, 10000), "still loading");
                    Pump(200);
                    Check(w.Engine == null, "opened junk");
                    Check(w.lastSay != null && w.lastSay.Length > 0, "said why");
                    Note(w.lastSay);
                }
                finally { w.Close(); Pump(100); }
            });
        }
    }
}
