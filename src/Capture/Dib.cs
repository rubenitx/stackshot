// Stackshot - Lienzo de 32 bits compartido entre GDI y GDI+.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Stackshot
{
    // GDI copia bloques enormes casi gratis (BitBlt) y GDI+ dibuja encima con antialias, los dos sobre los mismos
    // bytes y sin copias de por medio. Es lo que hace que el selector de capturas vaya fluido con pantallas grandes.
    public sealed class Dib : IDisposable
    {
        public readonly int Width, Height;
        public readonly IntPtr Dc;
        public readonly IntPtr Bits;
        public readonly Bitmap Bitmap;   // los mismos píxeles vistos desde GDI+ (el canal alfa no se usa)
        IntPtr hbm, old;

        public Dib(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = Width;
            bi.biHeight = -Height; // de arriba abajo, como los Bitmap de GDI+
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            IntPtr bits;
            hbm = Native.CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (hbm == IntPtr.Zero) throw new OutOfMemoryException("CreateDIBSection " + Width + "x" + Height);
            Bits = bits;
            Dc = Native.CreateCompatibleDC(IntPtr.Zero);
            old = Native.SelectObject(Dc, hbm);
            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppRgb, bits);
        }

        // Antes de dibujar con GDI+ hay que vaciar lo que GDI tenga pendiente.
        public Graphics Graphics()
        {
            Native.GdiFlush();
            return System.Drawing.Graphics.FromImage(Bitmap);
        }

        // Lo que se ve ahora en ese rectángulo de pantalla.
        public static Dib FromScreen(Rectangle r)
        {
            Dib d = new Dib(r.Width, r.Height);
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            try { Native.BitBlt(d.Dc, 0, 0, d.Width, d.Height, screen, r.X, r.Y, Native.SRCCOPY); }
            finally { Native.ReleaseDC(IntPtr.Zero, screen); }
            Native.GdiFlush();
            return d;
        }

        // Copia independiente para GDI+ (sigue viva aunque el lienzo se libere).
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

        // Oscurece todo el lienzo (alpha 0-255 de negro encima).
        public void Darken(byte alpha)
        {
            using (Dib black = new Dib(1, 1))
            {
                // El píxel nace a cero: negro.
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
