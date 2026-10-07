using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Stackshot
{
    // Animated GIF89a encoder: per-frame Wu palette, serpentine Floyd-Steinberg dithering,
    // changed-rectangle frames with transparent unchanged pixels, and merged duplicate frames.
    public sealed class GifWriter : IDisposable
    {
        private const float DitherStrength = 0.75f;
        private const float MaxError = 48f;
        private const int SampleLimit = 400000;
        private const int SnapMinRun = 3;
        private const int SnapMaxColors = 48;
        private const int SnapMaxDistance = 1300;
        private const int CacheBits = 6;
        private const int CacheSize = 1 << (CacheBits * 3);

        private readonly int _width;
        private readonly int _height;
        private Stream _stream;
        private int[] _prev;
        private int[] _cur;
        private PendingFrame _pending;
        private readonly WuQuantizer _quantizer = new WuQuantizer();
        private readonly LzwEncoder _lzw = new LzwEncoder();

        private readonly int[] _cacheStamp = new int[CacheSize];
        private readonly byte[] _cacheValue = new byte[CacheSize];
        private readonly int[] _binStamp = new int[CacheSize];
        private readonly int[] _binFirst = new int[CacheSize];
        private readonly int[] _palNext = new int[256];
        private int _generation;

        private sealed class PendingFrame
        {
            public int X;
            public int Y;
            public int W;
            public int H;
            public byte[] ColorTable;
            public int TableBits;
            public int TransparentIndex;
            public byte[] Data;
            public int DelayCs;
        }

        public GifWriter(string path, int width, int height, int loopCount)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (width <= 0 || width > 65535) throw new ArgumentOutOfRangeException("width");
            if (height <= 0 || height > 65535) throw new ArgumentOutOfRangeException("height");
            _width = width;
            _height = height;
            _cur = new int[width * height];
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);

            WriteAscii("GIF89a");
            WriteShort(width);
            WriteShort(height);
            _stream.WriteByte(0x70); // no global table, 8-bit color resolution
            _stream.WriteByte(0);
            _stream.WriteByte(0);

            if (loopCount >= 0)
            {
                _stream.WriteByte(0x21);
                _stream.WriteByte(0xFF);
                _stream.WriteByte(11);
                WriteAscii("NETSCAPE2.0");
                _stream.WriteByte(3);
                _stream.WriteByte(1);
                WriteShort(Math.Min(loopCount, 65535));
                _stream.WriteByte(0);
            }
        }

        public void AddFrame(Bitmap frame, int delayMs)
        {
            if (_stream == null) throw new ObjectDisposedException("GifWriter");
            if (frame == null) throw new ArgumentNullException("frame");
            if (frame.Width != _width || frame.Height != _height)
                throw new ArgumentException("Frame size does not match the GIF size.");

            int delayCs = (Math.Max(0, delayMs) + 5) / 10;
            if (delayCs < 2) delayCs = 2;

            ReadPixels(frame, _cur);

            int x0, y0, x1, y1;
            bool first = _prev == null;
            if (first)
            {
                x0 = 0; y0 = 0; x1 = _width - 1; y1 = _height - 1;
            }
            else if (!FindChanges(out x0, out y0, out x1, out y1))
            {
                if (_pending != null) _pending.DelayCs = Math.Min(65535, _pending.DelayCs + delayCs);
                return;
            }

            PendingFrame encoded = Encode(x0, y0, x1 - x0 + 1, y1 - y0 + 1, !first);
            encoded.DelayCs = delayCs;
            FlushPending();
            _pending = encoded;

            int[] tmp = _prev;
            _prev = _cur;
            _cur = tmp != null ? tmp : new int[_width * _height];
        }

        public void Dispose()
        {
            if (_stream == null) return;
            try
            {
                FlushPending();
                _stream.WriteByte(0x3B);
                _stream.Flush();
            }
            finally
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        // ---- pixel access ----

        private void ReadPixels(Bitmap bmp, int[] dest)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                if (data.Stride == _width * 4)
                {
                    Marshal.Copy(data.Scan0, dest, 0, _width * _height);
                }
                else
                {
                    for (int y = 0; y < _height; y++)
                        Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), dest, y * _width, _width);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            // Composite non-opaque pixels over black; the GIF itself is fully opaque.
            for (int i = 0; i < dest.Length; i++)
            {
                int c = dest[i];
                int a = (c >> 24) & 0xFF;
                if (a == 255) continue;
                int r = ((c >> 16) & 0xFF) * a / 255;
                int g = ((c >> 8) & 0xFF) * a / 255;
                int b = (c & 0xFF) * a / 255;
                dest[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
            }
        }

        private bool FindChanges(out int x0, out int y0, out int x1, out int y1)
        {
            int w = _width, h = _height;
            int[] cur = _cur, prev = _prev;
            x0 = w; y0 = -1; x1 = -1; y1 = -1;

            for (int y = 0; y < h && y0 < 0; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                    if (cur[row + x] != prev[row + x]) { y0 = y; break; }
            }
            if (y0 < 0) { x0 = 0; return false; }

            for (int y = h - 1; y >= y0 && y1 < 0; y--)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                    if (cur[row + x] != prev[row + x]) { y1 = y; break; }
            }

            for (int y = y0; y <= y1; y++)
            {
                int row = y * w;
                for (int x = 0; x < x0; x++)
                    if (cur[row + x] != prev[row + x]) { x0 = x; break; }
                for (int x = w - 1; x > x1; x--)
                    if (cur[row + x] != prev[row + x]) { x1 = x; break; }
            }
            return true;
        }

        // ---- frame encoding ----

        private PendingFrame Encode(int fx, int fy, int fw, int fh, bool useTransparency)
        {
            int[] cur = _cur;
            int[] prev = _prev;

            // Histogram (sampled when the rectangle is large).
            _quantizer.Clear();
            long pixelCount = (long)fw * fh;
            int step = 1;
            while (pixelCount / ((long)step * step) > SampleLimit) step++;
            AddSamples(fx, fy, fw, fh, step, useTransparency);
            if (_quantizer.Count == 0 && step > 1) AddSamples(fx, fy, fw, fh, 1, useTransparency);

            int maxColors = useTransparency ? 255 : 256;
            int[] palette = _quantizer.Quantize(maxColors);
            if (palette.Length == 0) palette = new int[] { 0 };
            SnapFrequentColors(palette, fx, fy, fw, fh, useTransparency);

            int colorCount = palette.Length;
            int transparentIndex = useTransparency ? colorCount : -1;
            int entries = colorCount + (useTransparency ? 1 : 0);
            int bits = 1;
            while ((1 << bits) < entries) bits++;

            byte[] table = new byte[3 << bits];
            for (int i = 0; i < colorCount; i++)
            {
                table[i * 3] = (byte)(palette[i] >> 16);
                table[i * 3 + 1] = (byte)(palette[i] >> 8);
                table[i * 3 + 2] = (byte)palette[i];
            }

            byte[] indices = Dither(palette, fx, fy, fw, fh, useTransparency, (byte)Math.Max(0, transparentIndex));

            PendingFrame f = new PendingFrame();
            f.X = fx; f.Y = fy; f.W = fw; f.H = fh;
            f.ColorTable = table;
            f.TableBits = bits;
            f.TransparentIndex = transparentIndex;
            f.Data = _lzw.Encode(indices, Math.Max(2, bits));
            return f;
        }

        private void AddSamples(int fx, int fy, int fw, int fh, int step, bool useTransparency)
        {
            int[] cur = _cur;
            int[] prev = _prev;
            for (int y = fy; y < fy + fh; y += step)
            {
                int row = y * _width;
                for (int x = fx; x < fx + fw; x += step)
                {
                    int i = row + x;
                    if (useTransparency && cur[i] == prev[i]) continue;
                    _quantizer.Add(cur[i]);
                }
            }
        }

        // Replaces palette entries by exact colors that cover large flat areas, so they render without dither noise.
        private void SnapFrequentColors(int[] palette, int fx, int fy, int fw, int fh, bool useTransparency)
        {
            int[] cur = _cur;
            int[] prev = _prev;
            Dictionary<int, int> runs = new Dictionary<int, int>();
            for (int y = fy; y < fy + fh; y++)
            {
                int row = y * _width;
                int x = fx;
                int end = fx + fw;
                while (x < end)
                {
                    int i = row + x;
                    int c = cur[i];
                    if (useTransparency && c == prev[i]) { x++; continue; }
                    int len = 1;
                    while (x + len < end && cur[i + len] == c && !(useTransparency && prev[i + len] == c)) len++;
                    if (len >= SnapMinRun)
                    {
                        int n;
                        runs.TryGetValue(c, out n);
                        runs[c] = n + len;
                    }
                    x += len;
                }
            }
            if (runs.Count == 0) return;

            int threshold = Math.Max(24, (int)((long)fw * fh / 1500));
            List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, int> kv in runs)
                if (kv.Value >= threshold) list.Add(kv);
            list.Sort(delegate(KeyValuePair<int, int> a, KeyValuePair<int, int> b) { return b.Value.CompareTo(a.Value); });

            bool[] snapped = new bool[palette.Length];
            int limit = Math.Min(SnapMaxColors, list.Count);
            for (int k = 0; k < limit; k++)
            {
                int c = list[k].Key & 0xFFFFFF;
                int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                int best = -1, bestDist = int.MaxValue;
                for (int j = 0; j < palette.Length; j++)
                {
                    int d = Distance(r, g, b, palette[j]);
                    if (d < bestDist) { bestDist = d; best = j; }
                }
                if (best < 0 || bestDist == 0 || snapped[best] || bestDist > SnapMaxDistance) continue;
                palette[best] = c;
                snapped[best] = true;
            }
        }

        private static int Distance(int r, int g, int b, int c)
        {
            int dr = r - ((c >> 16) & 0xFF);
            int dg = g - ((c >> 8) & 0xFF);
            int db = b - (c & 0xFF);
            return 2 * dr * dr + 4 * dg * dg + 3 * db * db;
        }

        private byte[] Dither(int[] palette, int fx, int fy, int fw, int fh, bool useTransparency, byte transparentIndex)
        {
            int[] cur = _cur;
            int[] prev = _prev;
            byte[] output = new byte[fw * fh];

            _generation++;
            if (_generation == int.MaxValue)
            {
                Array.Clear(_cacheStamp, 0, _cacheStamp.Length);
                Array.Clear(_binStamp, 0, _binStamp.Length);
                _generation = 1;
            }
            int gen = _generation;

            // Palette entries grouped by cache bin, so exact matches always win over the bin-center lookup.
            int n = palette.Length;
            for (int j = 0; j < n; j++)
            {
                int p = palette[j];
                int key = CacheKey((p >> 16) & 0xFF, (p >> 8) & 0xFF, p & 0xFF);
                if (_binStamp[key] != gen) { _binStamp[key] = gen; _binFirst[key] = -1; }
                _palNext[j] = _binFirst[key];
                _binFirst[key] = j;
            }

            int[] pr = new int[n], pg = new int[n], pb = new int[n];
            for (int j = 0; j < n; j++)
            {
                pr[j] = (palette[j] >> 16) & 0xFF;
                pg[j] = (palette[j] >> 8) & 0xFF;
                pb[j] = palette[j] & 0xFF;
            }

            float[] errCur = new float[(fw + 2) * 3];
            float[] errNext = new float[(fw + 2) * 3];
            const float k7 = DitherStrength * 7f / 16f;
            const float k3 = DitherStrength * 3f / 16f;
            const float k5 = DitherStrength * 5f / 16f;
            const float k1 = DitherStrength * 1f / 16f;

            for (int y = 0; y < fh; y++)
            {
                bool ltr = (y & 1) == 0;
                Array.Clear(errNext, 0, errNext.Length);
                int srcRow = (fy + y) * _width + fx;
                int outRow = y * fw;
                for (int k = 0; k < fw; k++)
                {
                    int x = ltr ? k : fw - 1 - k;
                    int si = srcRow + x;
                    int c = cur[si];
                    if (useTransparency && c == prev[si])
                    {
                        output[outRow + x] = transparentIndex;
                        continue;
                    }

                    int e = (x + 1) * 3;
                    float r = ((c >> 16) & 0xFF) + errCur[e];
                    float g = ((c >> 8) & 0xFF) + errCur[e + 1];
                    float b = (c & 0xFF) + errCur[e + 2];
                    if (r < 0f) r = 0f; else if (r > 255f) r = 255f;
                    if (g < 0f) g = 0f; else if (g > 255f) g = 255f;
                    if (b < 0f) b = 0f; else if (b > 255f) b = 255f;

                    int ri = (int)(r + 0.5f), gi = (int)(g + 0.5f), bi = (int)(b + 0.5f);
                    int idx = Lookup(ri, gi, bi, pr, pg, pb, gen);
                    output[outRow + x] = (byte)idx;

                    float er = r - pr[idx], eg = g - pg[idx], eb = b - pb[idx];
                    if (er > MaxError) er = MaxError; else if (er < -MaxError) er = -MaxError;
                    if (eg > MaxError) eg = MaxError; else if (eg < -MaxError) eg = -MaxError;
                    if (eb > MaxError) eb = MaxError; else if (eb < -MaxError) eb = -MaxError;
                    if (er == 0f && eg == 0f && eb == 0f) continue;

                    int fwd = ltr ? e + 3 : e - 3;
                    int back = ltr ? e - 3 : e + 3;
                    errCur[fwd] += er * k7; errCur[fwd + 1] += eg * k7; errCur[fwd + 2] += eb * k7;
                    errNext[back] += er * k3; errNext[back + 1] += eg * k3; errNext[back + 2] += eb * k3;
                    errNext[e] += er * k5; errNext[e + 1] += eg * k5; errNext[e + 2] += eb * k5;
                    errNext[fwd] += er * k1; errNext[fwd + 1] += eg * k1; errNext[fwd + 2] += eb * k1;
                }
                float[] t = errCur; errCur = errNext; errNext = t;
            }
            return output;
        }

        private static int CacheKey(int r, int g, int b)
        {
            const int shift = 8 - CacheBits;
            return ((r >> shift) << (CacheBits * 2)) | ((g >> shift) << CacheBits) | (b >> shift);
        }

        private int Lookup(int r, int g, int b, int[] pr, int[] pg, int[] pb, int gen)
        {
            int key = CacheKey(r, g, b);
            int best;
            if (_cacheStamp[key] == gen)
            {
                best = _cacheValue[key];
            }
            else
            {
                const int shift = 8 - CacheBits;
                const int half = 1 << (shift - 1);
                int cr = ((r >> shift) << shift) + half;
                int cg = ((g >> shift) << shift) + half;
                int cb = ((b >> shift) << shift) + half;
                best = Nearest(cr, cg, cb, pr, pg, pb);
                _cacheStamp[key] = gen;
                _cacheValue[key] = (byte)best;
            }

            if (_binStamp[key] != gen) return best;
            int j = _binFirst[key];
            if (j < 0) return best;
            int dr = r - pr[best], dg = g - pg[best], db = b - pb[best];
            int bestDist = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
            for (; j >= 0; j = _palNext[j])
            {
                dr = r - pr[j]; dg = g - pg[j]; db = b - pb[j];
                int d = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
                if (d < bestDist) { bestDist = d; best = j; }
            }
            return best;
        }

        private static int Nearest(int r, int g, int b, int[] pr, int[] pg, int[] pb)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int j = 0; j < pr.Length; j++)
            {
                int dg = g - pg[j];
                int d = 4 * dg * dg;
                if (d >= bestDist) continue;
                int dr = r - pr[j];
                d += 2 * dr * dr;
                if (d >= bestDist) continue;
                int db = b - pb[j];
                d += 3 * db * db;
                if (d < bestDist) { bestDist = d; best = j; }
            }
            return best;
        }

        // ---- output ----

        private void FlushPending()
        {
            PendingFrame f = _pending;
            if (f == null) return;
            _pending = null;

            // Graphic control extension: disposal 1 (do not dispose), optional transparency.
            _stream.WriteByte(0x21);
            _stream.WriteByte(0xF9);
            _stream.WriteByte(4);
            _stream.WriteByte((byte)((1 << 2) | (f.TransparentIndex >= 0 ? 1 : 0)));
            WriteShort(f.DelayCs);
            _stream.WriteByte((byte)(f.TransparentIndex >= 0 ? f.TransparentIndex : 0));
            _stream.WriteByte(0);

            // Image descriptor with local color table.
            _stream.WriteByte(0x2C);
            WriteShort(f.X);
            WriteShort(f.Y);
            WriteShort(f.W);
            WriteShort(f.H);
            _stream.WriteByte((byte)(0x80 | (f.TableBits - 1)));
            _stream.Write(f.ColorTable, 0, f.ColorTable.Length);
            _stream.Write(f.Data, 0, f.Data.Length);
        }

        private void WriteShort(int v)
        {
            _stream.WriteByte((byte)(v & 0xFF));
            _stream.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private void WriteAscii(string s)
        {
            for (int i = 0; i < s.Length; i++) _stream.WriteByte((byte)s[i]);
        }

        // ---- Wu's color quantizer (6 bits per channel histogram) ----

        private sealed class WuQuantizer
        {
            private const int Side = 65;
            private const int Side2 = Side * Side;
            private const int TotalSize = Side * Side * Side;
            private const int Red = 2, Green = 1, Blue = 0;

            private readonly double[] _wt = new double[TotalSize];
            private readonly double[] _mr = new double[TotalSize];
            private readonly double[] _mg = new double[TotalSize];
            private readonly double[] _mb = new double[TotalSize];
            private readonly double[] _m2 = new double[TotalSize];
            private int _count;

            private sealed class Box
            {
                public int R0, R1, G0, G1, B0, B1, Vol;
            }

            public int Count
            {
                get { return _count; }
            }

            public void Clear()
            {
                Array.Clear(_wt, 0, TotalSize);
                Array.Clear(_mr, 0, TotalSize);
                Array.Clear(_mg, 0, TotalSize);
                Array.Clear(_mb, 0, TotalSize);
                Array.Clear(_m2, 0, TotalSize);
                _count = 0;
            }

            public void Add(int c)
            {
                int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                int i = ((r >> 2) + 1) * Side2 + ((g >> 2) + 1) * Side + (b >> 2) + 1;
                _wt[i] += 1;
                _mr[i] += r;
                _mg[i] += g;
                _mb[i] += b;
                _m2[i] += r * r + g * g + b * b;
                _count++;
            }

            public int[] Quantize(int maxColors)
            {
                if (_count == 0) return new int[0];
                Moments();

                Box[] cubes = new Box[maxColors];
                for (int i = 0; i < maxColors; i++) cubes[i] = new Box();
                cubes[0].R1 = Side - 1;
                cubes[0].G1 = Side - 1;
                cubes[0].B1 = Side - 1;
                double[] vv = new double[maxColors];

                int k = maxColors;
                int next = 0;
                for (int i = 1; i < maxColors; i++)
                {
                    if (Cut(cubes[next], cubes[i]))
                    {
                        vv[next] = cubes[next].Vol > 1 ? Var(cubes[next]) : 0.0;
                        vv[i] = cubes[i].Vol > 1 ? Var(cubes[i]) : 0.0;
                    }
                    else
                    {
                        vv[next] = 0.0;
                        i--;
                    }
                    next = 0;
                    double temp = vv[0];
                    for (int j = 1; j <= i; j++)
                    {
                        if (vv[j] > temp) { temp = vv[j]; next = j; }
                    }
                    if (temp <= 0.0) { k = i + 1; break; }
                }

                List<int> colors = new List<int>(k);
                for (int i = 0; i < k; i++)
                {
                    double w = Volume(cubes[i], _wt);
                    if (w <= 0) continue;
                    int r = Clamp((int)(Volume(cubes[i], _mr) / w + 0.5));
                    int g = Clamp((int)(Volume(cubes[i], _mg) / w + 0.5));
                    int b = Clamp((int)(Volume(cubes[i], _mb) / w + 0.5));
                    colors.Add((r << 16) | (g << 8) | b);
                }
                return colors.ToArray();
            }

            private static int Clamp(int v)
            {
                return v < 0 ? 0 : (v > 255 ? 255 : v);
            }

            private void Moments()
            {
                double[] area = new double[Side], areaR = new double[Side], areaG = new double[Side], areaB = new double[Side], area2 = new double[Side];
                for (int r = 1; r < Side; r++)
                {
                    Array.Clear(area, 0, Side);
                    Array.Clear(areaR, 0, Side);
                    Array.Clear(areaG, 0, Side);
                    Array.Clear(areaB, 0, Side);
                    Array.Clear(area2, 0, Side);
                    for (int g = 1; g < Side; g++)
                    {
                        double line = 0, lineR = 0, lineG = 0, lineB = 0, line2 = 0;
                        for (int b = 1; b < Side; b++)
                        {
                            int i1 = r * Side2 + g * Side + b;
                            line += _wt[i1];
                            lineR += _mr[i1];
                            lineG += _mg[i1];
                            lineB += _mb[i1];
                            line2 += _m2[i1];
                            area[b] += line;
                            areaR[b] += lineR;
                            areaG[b] += lineG;
                            areaB[b] += lineB;
                            area2[b] += line2;
                            int i2 = i1 - Side2;
                            _wt[i1] = _wt[i2] + area[b];
                            _mr[i1] = _mr[i2] + areaR[b];
                            _mg[i1] = _mg[i2] + areaG[b];
                            _mb[i1] = _mb[i2] + areaB[b];
                            _m2[i1] = _m2[i2] + area2[b];
                        }
                    }
                }
            }

            private static int Ix(int r, int g, int b)
            {
                return r * Side2 + g * Side + b;
            }

            private static double Volume(Box c, double[] m)
            {
                return m[Ix(c.R1, c.G1, c.B1)] - m[Ix(c.R1, c.G1, c.B0)] - m[Ix(c.R1, c.G0, c.B1)] + m[Ix(c.R1, c.G0, c.B0)]
                     - m[Ix(c.R0, c.G1, c.B1)] + m[Ix(c.R0, c.G1, c.B0)] + m[Ix(c.R0, c.G0, c.B1)] - m[Ix(c.R0, c.G0, c.B0)];
            }

            private static double Bottom(Box c, int dir, double[] m)
            {
                switch (dir)
                {
                    case Red:
                        return -m[Ix(c.R0, c.G1, c.B1)] + m[Ix(c.R0, c.G1, c.B0)] + m[Ix(c.R0, c.G0, c.B1)] - m[Ix(c.R0, c.G0, c.B0)];
                    case Green:
                        return -m[Ix(c.R1, c.G0, c.B1)] + m[Ix(c.R1, c.G0, c.B0)] + m[Ix(c.R0, c.G0, c.B1)] - m[Ix(c.R0, c.G0, c.B0)];
                    default:
                        return -m[Ix(c.R1, c.G1, c.B0)] + m[Ix(c.R1, c.G0, c.B0)] + m[Ix(c.R0, c.G1, c.B0)] - m[Ix(c.R0, c.G0, c.B0)];
                }
            }

            private static double Top(Box c, int dir, int pos, double[] m)
            {
                switch (dir)
                {
                    case Red:
                        return m[Ix(pos, c.G1, c.B1)] - m[Ix(pos, c.G1, c.B0)] - m[Ix(pos, c.G0, c.B1)] + m[Ix(pos, c.G0, c.B0)];
                    case Green:
                        return m[Ix(c.R1, pos, c.B1)] - m[Ix(c.R1, pos, c.B0)] - m[Ix(c.R0, pos, c.B1)] + m[Ix(c.R0, pos, c.B0)];
                    default:
                        return m[Ix(c.R1, c.G1, pos)] - m[Ix(c.R1, c.G0, pos)] - m[Ix(c.R0, c.G1, pos)] + m[Ix(c.R0, c.G0, pos)];
                }
            }

            private double Var(Box c)
            {
                double dr = Volume(c, _mr), dg = Volume(c, _mg), db = Volume(c, _mb);
                double xx = Volume(c, _m2);
                double w = Volume(c, _wt);
                if (w <= 0) return 0;
                return xx - (dr * dr + dg * dg + db * db) / w;
            }

            private double Maximize(Box c, int dir, int first, int last, out int cut,
                double wholeR, double wholeG, double wholeB, double wholeW)
            {
                double baseR = Bottom(c, dir, _mr);
                double baseG = Bottom(c, dir, _mg);
                double baseB = Bottom(c, dir, _mb);
                double baseW = Bottom(c, dir, _wt);
                double max = 0.0;
                cut = -1;
                for (int i = first; i < last; i++)
                {
                    double halfR = baseR + Top(c, dir, i, _mr);
                    double halfG = baseG + Top(c, dir, i, _mg);
                    double halfB = baseB + Top(c, dir, i, _mb);
                    double halfW = baseW + Top(c, dir, i, _wt);
                    if (halfW <= 0) continue;
                    double temp = (halfR * halfR + halfG * halfG + halfB * halfB) / halfW;
                    halfR = wholeR - halfR;
                    halfG = wholeG - halfG;
                    halfB = wholeB - halfB;
                    halfW = wholeW - halfW;
                    if (halfW <= 0) continue;
                    temp += (halfR * halfR + halfG * halfG + halfB * halfB) / halfW;
                    if (temp > max) { max = temp; cut = i; }
                }
                return max;
            }

            private bool Cut(Box set1, Box set2)
            {
                double wholeR = Volume(set1, _mr);
                double wholeG = Volume(set1, _mg);
                double wholeB = Volume(set1, _mb);
                double wholeW = Volume(set1, _wt);

                int cutR, cutG, cutB;
                double maxR = Maximize(set1, Red, set1.R0 + 1, set1.R1, out cutR, wholeR, wholeG, wholeB, wholeW);
                double maxG = Maximize(set1, Green, set1.G0 + 1, set1.G1, out cutG, wholeR, wholeG, wholeB, wholeW);
                double maxB = Maximize(set1, Blue, set1.B0 + 1, set1.B1, out cutB, wholeR, wholeG, wholeB, wholeW);

                int dir;
                if (maxR >= maxG && maxR >= maxB)
                {
                    dir = Red;
                    if (cutR < 0) return false;
                }
                else if (maxG >= maxR && maxG >= maxB)
                {
                    dir = Green;
                }
                else
                {
                    dir = Blue;
                }

                set2.R1 = set1.R1;
                set2.G1 = set1.G1;
                set2.B1 = set1.B1;
                switch (dir)
                {
                    case Red:
                        set2.R0 = set1.R1 = cutR;
                        set2.G0 = set1.G0;
                        set2.B0 = set1.B0;
                        break;
                    case Green:
                        set2.G0 = set1.G1 = cutG;
                        set2.R0 = set1.R0;
                        set2.B0 = set1.B0;
                        break;
                    default:
                        set2.B0 = set1.B1 = cutB;
                        set2.R0 = set1.R0;
                        set2.G0 = set1.G0;
                        break;
                }
                set1.Vol = (set1.R1 - set1.R0) * (set1.G1 - set1.G0) * (set1.B1 - set1.B0);
                set2.Vol = (set2.R1 - set2.R0) * (set2.G1 - set2.G0) * (set2.B1 - set2.B0);
                return true;
            }
        }

        // ---- variable-length LZW (GIFENCOD-style hashing, 12-bit max codes) ----

        private sealed class LzwEncoder
        {
            private const int MaxBits = 12;
            private const int MaxMaxCode = 1 << MaxBits;
            private const int HSize = 5003;

            private readonly int[] _htab = new int[HSize];
            private readonly int[] _codetab = new int[HSize];
            private readonly byte[] _block = new byte[256];
            private int _blockLen;
            private MemoryStream _out;

            private int _initBits, _nBits, _maxCode, _clearCode, _eofCode, _freeEnt;
            private bool _clearFlag;
            private int _curAccum, _curBits;

            public byte[] Encode(byte[] pixels, int minCodeSize)
            {
                _out = new MemoryStream(pixels.Length / 2 + 64);
                _out.WriteByte((byte)minCodeSize);

                _initBits = minCodeSize + 1;
                _nBits = _initBits;
                _maxCode = (1 << _nBits) - 1;
                _clearCode = 1 << minCodeSize;
                _eofCode = _clearCode + 1;
                _freeEnt = _clearCode + 2;
                _clearFlag = false;
                _curAccum = 0;
                _curBits = 0;
                _blockLen = 0;

                int hshift = 0;
                for (int fc = HSize; fc < 65536; fc *= 2) hshift++;
                hshift = 8 - hshift;

                ClearHash();
                Output(_clearCode);

                int ent = pixels[0];
                for (int p = 1; p < pixels.Length; p++)
                {
                    int c = pixels[p];
                    int fcode = (c << MaxBits) + ent;
                    int i = (c << hshift) ^ ent;

                    if (_htab[i] == fcode)
                    {
                        ent = _codetab[i];
                        continue;
                    }
                    if (_htab[i] >= 0)
                    {
                        int disp = HSize - i;
                        if (i == 0) disp = 1;
                        bool found = false;
                        do
                        {
                            i -= disp;
                            if (i < 0) i += HSize;
                            if (_htab[i] == fcode)
                            {
                                ent = _codetab[i];
                                found = true;
                                break;
                            }
                        } while (_htab[i] >= 0);
                        if (found) continue;
                    }

                    Output(ent);
                    ent = c;
                    if (_freeEnt < MaxMaxCode)
                    {
                        _codetab[i] = _freeEnt++;
                        _htab[i] = fcode;
                    }
                    else
                    {
                        ClearHash();
                        _freeEnt = _clearCode + 2;
                        _clearFlag = true;
                        Output(_clearCode);
                    }
                }
                Output(ent);
                Output(_eofCode);

                _out.WriteByte(0);
                byte[] result = _out.ToArray();
                _out = null;
                return result;
            }

            private void ClearHash()
            {
                for (int i = 0; i < HSize; i++) _htab[i] = -1;
            }

            private void Output(int code)
            {
                _curAccum &= (1 << _curBits) - 1;
                if (_curBits > 0) _curAccum |= code << _curBits;
                else _curAccum = code;
                _curBits += _nBits;

                while (_curBits >= 8)
                {
                    AddByte((byte)(_curAccum & 0xFF));
                    _curAccum >>= 8;
                    _curBits -= 8;
                }

                if (_freeEnt > _maxCode || _clearFlag)
                {
                    if (_clearFlag)
                    {
                        _nBits = _initBits;
                        _maxCode = (1 << _nBits) - 1;
                        _clearFlag = false;
                    }
                    else
                    {
                        _nBits++;
                        _maxCode = _nBits == MaxBits ? MaxMaxCode : (1 << _nBits) - 1;
                    }
                }

                if (code == _eofCode)
                {
                    while (_curBits > 0)
                    {
                        AddByte((byte)(_curAccum & 0xFF));
                        _curAccum >>= 8;
                        _curBits -= 8;
                    }
                    FlushBlock();
                }
            }

            private void AddByte(byte b)
            {
                _block[_blockLen++] = b;
                if (_blockLen >= 255) FlushBlock();
            }

            private void FlushBlock()
            {
                if (_blockLen == 0) return;
                _out.WriteByte((byte)_blockLen);
                _out.Write(_block, 0, _blockLen);
                _blockLen = 0;
            }
        }
    }
}
