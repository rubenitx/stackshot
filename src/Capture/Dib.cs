// Stackshot - 32-bit DIB shared by GDI and GDI+.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Stackshot
{
    // GDI blits large areas almost for free and GDI+ draws antialiased on the same bytes, with no copies in between.
    public sealed class Dib : IDisposable
    {
        public readonly int Width, Height;
        public readonly IntPtr Dc;
        public readonly IntPtr Bits;
        public readonly Bitmap Bitmap;   // same pixels seen from GDI+ (alpha unused unless premultiplied)
        IntPtr hbm, old;

        public Dib(int width, int height) : this(width, height, false) { }

        // premultiplied: GDI+ draws real (premultiplied) alpha, as UpdateLayeredWindow expects.
        public Dib(int width, int height, bool premultiplied)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = Width;
            bi.biHeight = -Height; // top-down, like GDI+ bitmaps
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            IntPtr bits;
            hbm = Native.CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (hbm == IntPtr.Zero) throw new OutOfMemoryException("CreateDIBSection " + Width + "x" + Height);
            Bits = bits;
            Dc = Native.CreateCompatibleDC(IntPtr.Zero);
            old = Native.SelectObject(Dc, hbm);
            Bitmap = new Bitmap(Width, Height, Width * 4, premultiplied ? PixelFormat.Format32bppPArgb : PixelFormat.Format32bppRgb, bits);
        }

        // Flush pending GDI work before drawing with GDI+.
        public Graphics Graphics()
        {
            Native.GdiFlush();
            return System.Drawing.Graphics.FromImage(Bitmap);
        }

        // GDI+ on the DIB's DC. TextRenderer then draws straight into the DIB; on a Bitmap-based Graphics every text
        // call copies the whole image out and back, which costs milliseconds per string on large canvases.
        public Graphics DcGraphics()
        {
            Native.GdiFlush();
            return System.Drawing.Graphics.FromHdc(Dc);
        }

        public static Dib FromScreen(Rectangle r)
        {
            Dib d = new Dib(r.Width, r.Height);
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            try { Native.BitBlt(d.Dc, 0, 0, d.Width, d.Height, screen, r.X, r.Y, Native.SRCCOPY); }
            finally { Native.ReleaseDC(IntPtr.Zero, screen); }
            Native.GdiFlush();
            return d;
        }

        // Independent copy that outlives this DIB.
        public Bitmap ToBitmap()
        {
            Native.GdiFlush();
            Bitmap b = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
            BitmapData bd = b.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                if (bd.Stride == Width * 4) Native.CopyMemory(bd.Scan0, Bits, new UIntPtr((ulong)Width * 4 * (ulong)Height));
                else
                {
                    for (int y = 0; y < Height; y++)
                        Native.CopyMemory(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), new IntPtr(Bits.ToInt64() + (long)y * Width * 4), new UIntPtr((uint)(Width * 4)));
                }
            }
            finally { b.UnlockBits(bd); }
            return b;
        }

        public void Darken(byte alpha)
        {
            using (Dib black = new Dib(1, 1))
            {
                // A new DIB is zero-filled: one black pixel.
                Native.AlphaBlend(Dc, 0, 0, Width, Height, black.Dc, 0, 0, 1, 1, alpha << 16);
            }
            Native.GdiFlush();
        }

        public void Dispose()
        {
            Bitmap.Dispose();
            if (Dc != IntPtr.Zero)
            {
                Native.SelectObject(Dc, old);
                Native.DeleteDC(Dc);
            }
            if (hbm != IntPtr.Zero) { Native.DeleteObject(hbm); hbm = IntPtr.Zero; }
        }
    }
}
