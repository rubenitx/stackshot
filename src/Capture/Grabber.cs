// Stackshot - Screen copy, window enumeration and cursor drawing.
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
            public Rectangle Bounds;   // physical pixels, without the invisible shadow
            public string Name;        // process name, used for the file name
        }

        // Stackshot windows are excluded from capture, so they never show up here.
        public static Bitmap Grab(Rectangle r)
        {
            Bitmap b = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return b;
        }

        // Visible bounds, without the invisible resize border Windows 10/11 adds.
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

        // Visible top-level windows, front to back (for hover highlighting in the picker).
        public static List<Win> Windows()
        {
            List<Win> list = new List<Win>();
            uint me;
            using (Process self = Process.GetCurrentProcess()) me = (uint)self.Id;
            StringBuilder cls = new StringBuilder(64);
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                int cloaked;
                if (Native.DwmGetInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true; // DWMWA_CLOAKED: other virtual desktops, suspended apps
                uint pid;
                Native.GetWindowPid(h, out pid);
                if (pid == me) return true;
                int ex = Native.GetWindowLong(h, -20); // GWL_EXSTYLE
                if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return true; // WS_EX_TOOLWINDOW without WS_EX_APPWINDOW
                if ((ex & 0x20) != 0) return true; // WS_EX_TRANSPARENT: click-through overlays
                cls.Length = 0;
                Native.GetClassName(h, cls, cls.Capacity);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW") return true; // the desktop: full-screen capture covers it
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

        // Windows 11 windows have rounded corners: make the corners transparent so the background does not leak in.
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

        // Draws the cursor onto an HDC whose origin is screen point (ox, oy). Used for recordings.
        public static void DrawCursor(IntPtr hdc, int ox, int oy)
        {
            DrawCursor(hdc, ox, oy, CursorNow());
        }

        // The cursor as it is now (shape, position, visibility).
        public static Native.CURSORINFO CursorNow()
        {
            Native.CURSORINFO ci = new Native.CURSORINFO();
            ci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.CURSORINFO));
            if (!Native.GetCursorInfo(ref ci)) ci.flags = 0;
            return ci;
        }

        public static void DrawCursor(IntPtr hdc, int ox, int oy, Native.CURSORINFO ci)
        {
            if ((ci.flags & 1) == 0 || ci.hCursor == IntPtr.Zero) return; // CURSOR_SHOWING
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

        public static Screen CurrentScreen()
        {
            return Screen.FromPoint(Control.MousePosition);
        }
    }
}
