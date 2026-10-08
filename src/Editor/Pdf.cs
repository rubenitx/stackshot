// Stackshot - PDF export: a capture (long scrolling ones included) split into A4-width pages, lossless.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Stackshot
{
    // Minimal PDF writer: each page is one image (RGB, FlateDecode, so text stays sharp at any zoom). Long captures are cut
    // into pages with the proportions of A4, choosing a flat row near each break so lines of text are not sliced in half.
    public static class Pdf
    {
        const float PageWidthPt = 595.28f; // A4 width in points

        public static void Save(Bitmap img, string path)
        {
            List<int> cuts = PageBreaks(img);
            List<long> offsets = new List<long>();
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                Action<string> write = delegate(string s) { byte[] b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); };
                write("%PDF-1.4\n");
                fs.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6); // binary marker line
                int pages = cuts.Count - 1;
                // Objects: 1 catalog, 2 page tree, then per page: page, contents, image.
                Func<int, int> pageObj = delegate(int i) { return 3 + i * 3; };
                offsets.Add(fs.Position);
                write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                StringBuilder kids = new StringBuilder();
                for (int i = 0; i < pages; i++) kids.Append(pageObj(i)).Append(" 0 R ");
                offsets.Add(fs.Position);
                write("2 0 obj\n<< /Type /Pages /Kids [" + kids.ToString().Trim() + "] /Count " + pages + " >>\nendobj\n");
                for (int i = 0; i < pages; i++)
                {
                    int y0 = cuts[i], h = cuts[i + 1] - cuts[i];
                    float hPt = PageWidthPt * h / img.Width;
                    string w = Num(PageWidthPt), hp = Num(hPt);
                    offsets.Add(fs.Position);
                    write(pageObj(i) + " 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + w + " " + hp + "] /Resources << /XObject << /Im0 " +
                          (pageObj(i) + 2) + " 0 R >> >> /Contents " + (pageObj(i) + 1) + " 0 R >>\nendobj\n");
                    string content = "q " + w + " 0 0 " + hp + " 0 0 cm /Im0 Do Q";
                    offsets.Add(fs.Position);
                    write((pageObj(i) + 1) + " 0 obj\n<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");
                    byte[] data = Deflate(Rgb(img, y0, h));
                    offsets.Add(fs.Position);
                    write((pageObj(i) + 2) + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + img.Width + " /Height " + h +
                          " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length " + data.Length + " >>\nstream\n");
                    fs.Write(data, 0, data.Length);
                    write("\nendstream\nendobj\n");
                }
                long xref = fs.Position;
                StringBuilder x = new StringBuilder();
                x.Append("xref\n0 ").Append(offsets.Count + 1).Append("\n0000000000 65535 f \n");
                foreach (long o in offsets) x.Append(o.ToString("D10")).Append(" 00000 n \n");
                x.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
                write(x.ToString());
            }
        }

        static string Num(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        // Row boundaries of each page: A4 proportions, nudged to the flattest row within 12% of the ideal cut.
        static List<int> PageBreaks(Bitmap img)
        {
            List<int> cuts = new List<int>();
            cuts.Add(0);
            int pageH = Math.Max(1, (int)Math.Round(img.Width * 1.4142));
            if (img.Height <= pageH * 1.15) { cuts.Add(img.Height); return cuts; }
            int[] score = RowScores(img);
            int y = 0;
            while (img.Height - y > pageH * 1.15)
            {
                int ideal = y + pageH, slack = (int)(pageH * 0.12), best = ideal, bestScore = int.MaxValue;
                for (int r = Math.Max(y + pageH / 2, ideal - slack); r <= Math.Min(img.Height - 1, ideal + slack); r++)
                {
                    int s = score[r] * 4 + Math.Abs(r - ideal) / 8; // flat rows win; ties go to the ideal cut
                    if (s < bestScore) { bestScore = s; best = r; }
                }
                cuts.Add(best);
                y = best;
            }
            cuts.Add(img.Height);
            return cuts;
        }

        // How busy each row is: the spread of brightness across a sample of its pixels (0 = a flat row).
        static int[] RowScores(Bitmap img)
        {
            int w = img.Width, h = img.Height;
            int[] score = new int[h];
            BitmapData bd = img.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[w];
                int step = Math.Max(1, w / 400);
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), row, 0, w);
                    int lo = 255, hi = 0;
                    for (int x = 0; x < w; x += step)
                    {
                        int c = row[x], l = (((c >> 16) & 255) * 3 + ((c >> 8) & 255) * 6 + (c & 255)) / 10;
                        if (l < lo) lo = l;
                        if (l > hi) hi = l;
                    }
                    score[y] = hi - lo;
                }
            }
            finally { img.UnlockBits(bd); }
            return score;
        }

        // Rows y0..y0+h as packed RGB (transparent pixels over white, like paper).
        static byte[] Rgb(Bitmap img, int y0, int h)
        {
            int w = img.Width;
            byte[] rgb = new byte[w * h * 3];
            BitmapData bd = img.LockBits(new Rectangle(0, y0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[w];
                int o = 0;
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), row, 0, w);
                    for (int x = 0; x < w; x++)
                    {
                        int c = row[x], a = (c >> 24) & 255;
                        int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                        if (a < 255) { r = (r * a + 255 * (255 - a)) / 255; g = (g * a + 255 * (255 - a)) / 255; b = (b * a + 255 * (255 - a)) / 255; }
                        rgb[o++] = (byte)r; rgb[o++] = (byte)g; rgb[o++] = (byte)b;
                    }
                }
            }
            finally { img.UnlockBits(bd); }
            return rgb;
        }

        // zlib stream (header + raw deflate + Adler-32), as PDF's FlateDecode expects.
        static byte[] Deflate(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    a = (a + data[i]) % 65521;
                    b = (b + a) % 65521;
                }
                uint adler = (b << 16) | a;
                ms.WriteByte((byte)(adler >> 24));
                ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8));
                ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }
    }
}
