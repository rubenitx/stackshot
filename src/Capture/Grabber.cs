// Stackshot - Copiar la pantalla, saber qué ventanas hay y dibujar el cursor.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public static class Grabber
    {
        public class Win
        {
            public IntPtr Handle;
            public Rectangle Bounds;   // en píxeles reales de pantalla, sin la sombra
            public string Name;        // nombre del programa, para el nombre del fichero
        }

        // Lo que se ve en ese rectángulo de pantalla. Las ventanas de Stackshot no salen (se excluyen de las capturas).
        public static Bitmap Grab(Rectangle r)
        {
            Bitmap b = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return b;
        }

        // Rectángulo visible de una ventana: sin la sombra invisible que Windows 10/11 añade alrededor.
        public static Rectangle Bounds(IntPtr h)
        {
            Native.RECT r;
            if (Native.DwmGetRect(h, 9, out r, 16) != 0) Native.GetWindowRect(h, out r); // DWMWA_EXTENDED_FRAME_BOUNDS
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        public static string ProcessName(IntPtr h)
        {
            try
            {
                uint pid;
                Native.GetWindowPid(h, out pid);
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return null; }
        }

        // Ventanas visibles de delante a atrás (para resaltar la que hay bajo el ratón al capturar).
        public static List<Win> Windows()
        {
            List<Win> list = new List<Win>();
            uint me = (uint)Process.GetCurrentProcess().Id;
            StringBuilder cls = new StringBuilder(64);
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                int cloaked;
                if (Native.DwmGetInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true; // DWMWA_CLOAKED: otro escritorio, apps suspendidas
                uint pid;
                Native.GetWindowPid(h, out pid);
                if (pid == me) return true;
                int ex = Native.GetWindowLong(h, -20); // GWL_EXSTYLE
                if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return true; // WS_EX_TOOLWINDOW sin WS_EX_APPWINDOW
                if ((ex & 0x20) != 0) return true; // WS_EX_TRANSPARENT: capas que no se pueden pinchar
                cls.Length = 0;
                Native.GetClassName(h, cls, cls.Capacity);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW") return true; // el escritorio: para eso está la pantalla entera
                Rectangle b = Bounds(h);
                if (b.Width < 40 || b.Height < 30) return true;
                Win w = new Win();
                w.Handle = h;
                w.Bounds = b;
                list.Add(w);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // Las ventanas de Windows 11 tienen las esquinas redondeadas: al capturarlas se dejan transparentes,
        // para que no asome lo que había detrás.
        public static Bitmap RoundCorners(Bitmap src, float radius)
        {
            if (radius < 1) return src;
            Bitmap b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            using (TextureBrush tb = new TextureBrush(src))
            using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, src.Width, src.Height), radius))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.FillPath(tb, p);
            }
            src.Dispose();
            return b;
        }

        // Dibuja el cursor del ratón en un contexto GDI cuyo origen está en (ox, oy) de la pantalla (para los vídeos).
        public static void DrawCursor(IntPtr hdc, int ox, int oy)
        {
            Native.CURSORINFO ci = new Native.CURSORINFO();
            ci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.CURSORINFO));
            if (!Native.GetCursorInfo(ref ci) || (ci.flags & 1) == 0 || ci.hCursor == IntPtr.Zero) return; // CURSOR_SHOWING
            Native.ICONINFO ii;
            int hx = 0, hy = 0;
            if (Native.GetIconInfo(ci.hCursor, out ii))
            {
                hx = ii.xHotspot;
                hy = ii.yHotspot;
                if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero) Native.DeleteObject(ii.hbmColor);
            }
            Native.DrawIconEx(hdc, ci.ptScreenPos.X - ox - hx, ci.ptScreenPos.Y - oy - hy, ci.hCursor, 0, 0, 0, IntPtr.Zero, 3); // DI_NORMAL
        }

        // Pantalla en la que está el ratón.
        public static Screen CurrentScreen()
        {
            return Screen.FromPoint(Control.MousePosition);
        }
    }
}
