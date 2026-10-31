// Stackshot - Stackshot's own notices: what a notice says, where the toasts go and when Windows asks for quiet.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public enum NoticeKind { Info, Warning, Error, Success }

    // One notice: a title, up to three lines of text and, optionally, a button. Action runs on a click on the button or
    // anywhere on the notice; without one, a click just closes it.
    public sealed class Notice
    {
        public readonly string Title, Text;
        public NoticeKind Kind;
        public string ActionLabel;
        public Action Action;
        public string Page, Folder;    // where Action leads, when it is a section of the main window or a folder
        public double At;              // when it was raised, on the stack's clock (ms)

        public Notice(string title, string text, NoticeKind kind)
        {
            Title = (title ?? "").Trim();
            Text = Clean(text);
            Kind = kind;
        }

        // The same title and text again refreshes the notice on screen instead of adding another.
        public string Key { get { return Title + "\n" + Text; } }

        // What a screen reader announces.
        public string Spoken
        {
            get
            {
                string t = Title.Length > 0 && !Title.EndsWith(".") ? Title + "." : Title;
                return "Stackshot: " + (Text.Length > 0 ? t + " " + Text.Replace("\n", " ") : t);
            }
        }

        // Line breaks kept, blank lines and stray spaces dropped: the toast has room for three lines.
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder b = new StringBuilder(text.Length);
            foreach (string l in lines)
            {
                string t = l.Trim();
                if (t.Length == 0) continue;
                if (b.Length > 0) b.Append('\n');
                b.Append(t);
            }
            return b.ToString();
        }

        // Spanish starts, then their English ones: a notice must be the same kind in both languages (the i18n suite checks
        // the titles that have one).
        static readonly string[] ErrorStarts = { "No se pudo", "No se pudieron", "No se ha podido", "No se puede", "No se encuentra", "Could not", "Cannot", "Capture not found" };
        static readonly string[] WarningStarts = { "No se han ", "Algunos atajos", "Grabando sin", "Grabaci\u00F3n detenida",
                                                   "La grabaci\u00F3n ha ido", "El sonido ha saturado", "Captura con desplazamiento",
                                                   "Sensitive data not", "Some shortcuts", "Recording without", "Recording stopped", "Recording was choppy",
                                                   "Audio clipped", "Scrolling capture" };
        static readonly string[] SuccessStarts = { "Texto copiado", "Grabaci\u00F3n recuperada", "Cine: sin p\u00E9rdidas", "Impr Pant ya es de Stackshot",
                                                   "Text copied", "Recording recovered", "Cinema: lossless", "Print Screen now belongs to Stackshot" };

        // The kind of a notice raised without one (Notify and NotifyPage), from its title.
        public static NoticeKind KindFor(string title)
        {
            string t = (title ?? "").Trim();
            if (Starts(t, ErrorStarts)) return NoticeKind.Error;
            if (Starts(t, WarningStarts)) return NoticeKind.Warning;
            if (Starts(t, SuccessStarts)) return NoticeKind.Success;
            return NoticeKind.Info;
        }

        static bool Starts(string t, string[] starts)
        {
            foreach (string s in starts) if (t.StartsWith(s, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    // The monitor the toasts are on: its work area, scale and the corner they stack from.
    public sealed class ToastArea
    {
        public string Device;
        public Rectangle Bounds, Work;
        public float Scale = 1f;
        public bool Top;        // the taskbar is at the top: they stack from the top-right corner

        public static ToastArea Of(Screen scr)
        {
            ToastArea a = new ToastArea();
            a.Device = scr.DeviceName;
            a.Bounds = scr.Bounds;
            a.Work = scr.WorkingArea;
            a.Scale = ShotStack.ScaleFor(scr);
            a.Top = ToastPlan.EdgeOf(a.Bounds, a.Work, ToastQuiet.TaskbarRect()) == ToastPlan.Top;
            return a;
        }
    }

    // Placement maths, pure: rectangles in, rectangles out.
    public static class ToastPlan
    {
        public const int Left = 0, Top = 1, Right = 2, Bottom = 3;   // taskbar edges, as ABE_*

        // The edge of a monitor the taskbar is on: the side its work area gives up, else (a taskbar that hides itself) the
        // side the taskbar window lies along, else the bottom.
        public static int EdgeOf(Rectangle bounds, Rectangle work, Rectangle taskbar)
        {
            if (work.Top > bounds.Top) return Top;
            if (work.Bottom < bounds.Bottom) return Bottom;
            if (work.Left > bounds.Left) return Left;
            if (work.Right < bounds.Right) return Right;
            if (taskbar.Width <= 0 || taskbar.Height <= 0 || !taskbar.IntersectsWith(bounds)) return Bottom;
            int cx = taskbar.Left + taskbar.Width / 2, cy = taskbar.Top + taskbar.Height / 2;
            if (taskbar.Width >= taskbar.Height) return cy < bounds.Top + bounds.Height / 2 ? Top : Bottom;
            return cx < bounds.Left + bounds.Width / 2 ? Left : Right;
        }

        // Where each toast goes, oldest first (the newest ends at the bottom): from the preferred corner of the work area;
        // when something occupies it, right past it on the same side, or the other side, whichever covers the least of
        // it and stays inside the work area (that order on a tie).
        public static Rectangle[] Place(Rectangle work, bool top, bool left, Size[] sizes, int margin, int gap, IList<Rectangle> occupied)
        {
            int n = sizes.Length;
            if (n == 0) return new Rectangle[0];
            if (occupied == null) occupied = new Rectangle[0];
            int width = 0;
            foreach (Size s in sizes) width = Math.Max(width, s.Width);
            Rectangle[] best = null;
            long bestBad = long.MaxValue;
            foreach (bool side in new bool[] { left, !left })
            {
                int edge = top ? work.Top + margin : work.Bottom - margin;
                foreach (int start in new int[] { edge, Past(work, top, side, width, margin, gap, occupied) })
                {
                    Rectangle[] r = Stack(work, top, side, sizes, margin, gap, start);
                    long bad = Badness(r, work, occupied);
                    if (bad < bestBad) { best = r; bestBad = bad; }
                    if (bad == 0) return best;
                }
            }
            return best;
        }

        // The column stacked from start: the bottom line when anchored at the bottom, the top line at the top.
        static Rectangle[] Stack(Rectangle work, bool top, bool left, Size[] sizes, int margin, int gap, int start)
        {
            int n = sizes.Length;
            Rectangle[] r = new Rectangle[n];
            int y = start;
            if (top)
                for (int i = 0; i < n; i++)
                {
                    r[i] = new Rectangle(X(work, left, sizes[i].Width, margin), y, sizes[i].Width, sizes[i].Height);
                    y += sizes[i].Height + gap;
                }
            else
                for (int i = n - 1; i >= 0; i--)
                {
                    y -= sizes[i].Height;
                    r[i] = new Rectangle(X(work, left, sizes[i].Width, margin), y, sizes[i].Width, sizes[i].Height);
                    y -= gap;
                }
            return r;
        }

        static int X(Rectangle work, bool left, int w, int margin) { return left ? work.Left + margin : work.Right - margin - w; }

        // The line just past what occupies that side's column (above it when anchored at the bottom, below at the top).
        static int Past(Rectangle work, bool top, bool left, int width, int margin, int gap, IList<Rectangle> occupied)
        {
            int x0 = X(work, left, width, margin), x1 = x0 + width;
            int start = top ? work.Top + margin : work.Bottom - margin;
            foreach (Rectangle o in occupied)
            {
                if (o.Width <= 0 || o.Height <= 0 || o.Right <= x0 || o.Left >= x1 || !o.IntersectsWith(work)) continue;
                start = top ? Math.Max(start, o.Bottom + gap) : Math.Min(start, o.Top - gap);
            }
            return start;
        }

        // Pixels of the toasts over something else or outside the work area: 0 is a clean spot.
        public static long Badness(Rectangle[] rects, Rectangle work, IList<Rectangle> occupied)
        {
            long bad = 0;
            foreach (Rectangle r in rects)
            {
                bad += Area(r) - Area(Rectangle.Intersect(r, work));
                foreach (Rectangle o in occupied) bad += Area(Rectangle.Intersect(r, o));
            }
            return bad;
        }

        static long Area(Rectangle r) { return r.Width <= 0 || r.Height <= 0 ? 0 : (long)r.Width * r.Height; }
    }

    // Windows' side of the notices: when it asks for quiet, where the taskbar is, whether to animate.
    public static class ToastQuiet
    {
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);

        static readonly int MyPid = Process.GetCurrentProcess().Id;

        // Why notices should wait now on that monitor, or null: nobody there (locked, screen saver), a full-screen app
        // over it, a full-screen game, presenting, or Windows' quiet time.
        public static string Reason(Rectangle monitor)
        {
            int q;
            try { if (SHQueryUserNotificationState(out q) != 0) return null; }
            catch { return null; }
            return ReasonFor(q, (q == 2 || q == 7) && ForegroundCovers(monitor));
        }

        // QUNS_* values: 1 not present, 2 busy (a full-screen app), 3 full-screen Direct3D, 4 presentation mode, 6 quiet
        // time, 7 a full-screen Store app. Busy only counts when that app covers the toasts' monitor.
        public static string ReasonFor(int state, bool foregroundCovers)
        {
            switch (state)
            {
                case 1: return I18n.T("sesi\u00F3n bloqueada o sin nadie delante", "session locked or away");
                case 2:
                case 7: return foregroundCovers ? I18n.T("aplicaci\u00F3n a pantalla completa", "full-screen application") : null;
                case 3: return I18n.T("juego a pantalla completa", "full-screen game");
                case 4: return I18n.T("modo presentaci\u00F3n", "presentation mode");
                case 6: return I18n.T("tiempo de silencio de Windows", "Windows focus assist / quiet hours");
            }
            return null;
        }

        // Another program's window (not the desktop) covers that whole monitor.
        static bool ForegroundCovers(Rectangle monitor)
        {
            try
            {
                IntPtr fg = Native.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid;
                Native.GetWindowPid(fg, out pid);
                if (pid == MyPid) return false;
                StringBuilder sb = new StringBuilder(32);
                GetClassName(fg, sb, 32);
                string c = sb.ToString();
                if (c == "Progman" || c == "WorkerW") return false;
                Native.RECT r;
                if (!Native.GetWindowRect(fg, out r)) return false;
                return r.Left <= monitor.Left && r.Top <= monitor.Top && r.Right >= monitor.Right && r.Bottom >= monitor.Bottom;
            }
            catch { return false; }
        }

        // The main taskbar's window (also when it hides itself), or an empty rectangle.
        public static Rectangle TaskbarRect()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                Native.RECT r;
                if (h != IntPtr.Zero && Native.GetWindowRect(h, out r)) return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            }
            catch { }
            return Rectangle.Empty;
        }

        // The monitor with the notification area (the main taskbar's).
        public static Screen TrayScreen()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h != IntPtr.Zero) return Screen.FromHandle(h);
            }
            catch { }
            return Screen.PrimaryScreen;
        }

        // Windows' "Animation effects" (off: toasts appear and leave at once).
        public static bool AnimationsOn()
        {
            try { return System.Windows.SystemParameters.ClientAreaAnimation; }
            catch { return true; }
        }
    }
}
