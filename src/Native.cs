// Stackshot - Win32 interop (user32, dwmapi, shell32...) and COM interfaces.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx; public int cy; }

        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from, uint to, bool attach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("ole32.dll")] public static extern int OleFlushClipboard();
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct SHDRAGIMAGE { public SIZE sizeDragImage; public POINT ptOffset; public IntPtr hbmpDragImage; public int crColorKey; }

        // Capture: windows, global hotkeys, cursor and fast GDI screen copy.

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public POINT ptScreenPos; }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO { public bool fIcon; public int xHotspot; public int yHotspot; public IntPtr hbmMask; public IntPtr hbmColor; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder s, int max);
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")] public static extern uint GetWindowPid(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmGetRect(IntPtr hwnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmGetInt(IntPtr hwnd, int attr, out int value, int size);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO ci);
        [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
        [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int w, int h, int step, IntPtr brush, int flags);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        // Full paths, so a planted explorer.exe or cmd.exe next to a portable copy or in the current folder never runs.
        public static readonly string Explorer = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        public static string System32(string exe) { return System.IO.Path.Combine(Environment.SystemDirectory, exe); }
        public const int SRCCOPY = 0x00CC0020;
        public const int CAPTUREBLT = 0x40000000; // includes layered windows (e.g. the camera bubble)
        [StructLayout(LayoutKind.Sequential)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT dst, ref SIZE size, IntPtr hdcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);

        // Shows a premultiplied-alpha DIB as the whole content of a layered window at (x, y).
        public static void Present(IntPtr hwnd, IntPtr dc, int x, int y, int w, int h) { Present(hwnd, dc, x, y, w, h, 0, 0); }

        // Same, using only the part of the DIB that starts at (sx, sy): the window becomes that size.
        public static void Present(IntPtr hwnd, IntPtr dc, int x, int y, int w, int h, int sx, int sy)
        {
            POINT dst; dst.X = x; dst.Y = y;
            POINT src; src.X = sx; src.Y = sy;
            SIZE size; size.cx = w; size.cy = h;
            BLENDFUNCTION bf = new BLENDFUNCTION();
            bf.SourceConstantAlpha = 255;
            bf.AlphaFormat = 1; // AC_SRC_ALPHA
            UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, dc, ref src, 0, ref bf, 2); // ULW_ALPHA
        }

        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
        static extern bool UpdateLayeredWindowHere(IntPtr hwnd, IntPtr hdcDst, IntPtr dst, ref SIZE size, IntPtr hdcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);
        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
        static extern bool UpdateLayeredWindowBlend(IntPtr hwnd, IntPtr hdcDst, IntPtr dst, IntPtr size, IntPtr hdcSrc, IntPtr src, int key, ref BLENDFUNCTION blend, int flags);

        // New content for a layered window, keeping its position (moves go through SetWindowPos).
        public static void PresentHere(IntPtr hwnd, IntPtr dc, int w, int h, byte alpha)
        {
            POINT src; src.X = 0; src.Y = 0;
            SIZE size; size.cx = w; size.cy = h;
            BLENDFUNCTION bf = new BLENDFUNCTION();
            bf.SourceConstantAlpha = alpha;
            bf.AlphaFormat = 1;
            UpdateLayeredWindowHere(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, dc, ref src, 0, ref bf, 2);
        }

        // Only the overall opacity of a layered window, without sending its pixels again.
        public static void FadeLayered(IntPtr hwnd, byte alpha)
        {
            BLENDFUNCTION bf = new BLENDFUNCTION();
            bf.SourceConstantAlpha = alpha;
            bf.AlphaFormat = 1;
            UpdateLayeredWindowBlend(hwnd, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref bf, 2);
        }
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public short bmPlanes, bmBitsPixel; public IntPtr bmBits; }
        [DllImport("gdi32.dll")] public static extern int GetObject(IntPtr obj, int size, out BITMAP bm);
        [DllImport("msimg32.dll")] public static extern bool AlphaBlend(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, int blend);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] public static extern void CopyMemory(IntPtr dst, IntPtr src, UIntPtr count);
        [DllImport("kernel32.dll")] public static extern bool SetDefaultDllDirectories(uint flags);

        public static void ForceForeground(IntPtr hwnd)
        {
            if (IsIconic(hwnd)) ShowWindow(hwnd, 9); // SW_RESTORE
            IntPtr fg = GetForegroundWindow();
            if (fg == hwnd) return;
            uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero), me = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(me, fgThread, false);
        }
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false, EntryPoint = "SHCreateItemFromParsingName")]
        public static extern void SHCreateShellItem([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Native.SIZE size, int flags, out IntPtr phbm);
    }

    // Only the first method is declared; it is the only one used.
    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                                        [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
    }

    // CLSID_DragDropHelper: shows the thumbnail next to the cursor while dragging.
    [ComImport, Guid("4657278A-411B-11D2-839A-00C04FD918D0")]
    public class DragDropHelper { }

    [ComImport, Guid("83E07D0D-0C5F-4163-BF1A-60B274051E40"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDragSourceHelper2
    {
        [PreserveSig] int InitializeFromBitmap(ref Native.SHDRAGIMAGE pshdi, ComTypes.IDataObject pDataObject);
        [PreserveSig] int InitializeFromWindow(IntPtr hwnd, ref Native.POINT ppt, ComTypes.IDataObject pDataObject);
        [PreserveSig] int SetFlags(int dwFlags);
    }

}
