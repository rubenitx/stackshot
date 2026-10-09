// Stackshot - Scrolling capture: stitches a long image while the content scrolls.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    // The user scrolls (or presses Auto); the area is grabbed periodically and newly revealed rows are appended. Sticky
    // headers and footers are not repeated. Esc cancels; Enter, Done or the hotkey finish.
    public static class ScrollCapture
    {
        static ScrollSession current;

        public static bool Active { get { return current != null; } }

        public static void Toggle(ShotStack owner, Settings settings)
        {
            if (current != null) { current.Finish(); return; }
            Bitmap frozen;
            Rectangle vs;
            IntPtr win;
            Rectangle r = RegionPicker.Pick(RegionPicker.Mode.Scroll, out frozen, out vs, out win);
            frozen.Dispose();
            Start(owner, r);
        }

        // Starts on an area already chosen (also the all-in-one picker).
        public static void Start(ShotStack owner, Rectangle r)
        {
            if (current != null || r.IsEmpty) return;
            if (r.Width < 40 || r.Height < 40)
            {
                if (owner != null) owner.Notify("Captura con desplazamiento", "El \u00E1rea es demasiado peque\u00F1a: elige una de al menos 40 \u00D7 40 p\u00EDxeles.");
                return;
            }
            current = new ScrollSession(owner, r);
            current.Start();
        }

        public static void Stop()
        {
            if (current != null) current.Cancel();
        }

        internal static void Ended(ScrollSession s)
        {
            if (current == s) current = null;
        }
    }

    // Stitches consecutive frames of a scrolling area. Each row is hashed; the shift is the offset most unique rows
    // agree on (voting), confirmed by comparing the whole overlap. Static rows at the top (header) and bottom (footer)
    // are excluded.
    public class Stitcher
    {
        public enum Result { First, Same, Added, Lost, Full }

        readonly int w, h, hashW, maxRows;
        readonly List<int[]> rows = new List<int[]>();
        int[] prev;
        ulong[] prevHash;
        bool[] prevFlat;
        volatile int height;

        public Stitcher(int width, int height, int ignoreRight, int maxRows)
        {
            w = width;
            h = height;
            hashW = Math.Max(1, width - Math.Max(0, ignoreRight));
            this.maxRows = Math.Max(height, maxRows);
        }

        public int Height { get { return height; } }
        public int Width { get { return w; } }
        public bool IsFull { get { return height >= maxRows; } }

        // Adds a frame (top-down pixels, w x h). Returns a buffer the caller can reuse: px if it was not kept, or the
        // previous frame if px became the new reference.
        public int[] Add(int[] px, out Result result, out int added)
        {
            added = 0;
            ulong[] hs = new ulong[h];
            bool[] flat = new bool[h];
            HashRows(px, hs, flat);
            if (prev == null)
            {
                for (int y = 0; y < h; y++) rows.Add(Row(px, y));
                height = rows.Count;
                Keep(px, hs, flat);
                added = h;
                result = Result.First;
                return null;
            }

            // Rows unchanged in place: header at the top, footer at the bottom.
            int top = 0;
            while (top < h && hs[top] == prevHash[top]) top++;
            if (top == h) { result = Result.Same; return px; }
            int bot = 0;
            while (bot < h - top && hs[h - 1 - bot] == prevHash[h - 1 - bot]) bot++;

            int d = BestShift(hs, flat, top, h - bot);
            if (d == 0) { result = Result.Same; return px; }       // something animates, but nothing scrolled
            // No match: a small changed band (blinking caret, video) means no scroll; otherwise it jumped too far or
            // went up.
            if (d < 0) { result = h - top - bot < h / 6 ? Result.Same : Result.Lost; return px; }
            if (IsFull) { result = Result.Full; return px; }

            int n = Math.Min(d, maxRows - rows.Count);
            List<int[]> fresh = new List<int[]>(n);
            for (int y = h - bot - d; y < h - bot - d + n; y++) fresh.Add(Row(px, y));
            lock (rows) rows.InsertRange(rows.Count - bot, fresh);
            height = rows.Count;
            added = n;
            result = IsFull ? Result.Full : Result.Added;
            int[] old = prev;
            Keep(px, hs, flat);
            return old;
        }

        void Keep(int[] px, ulong[] hs, bool[] flat)
        {
            prev = px;
            prevHash = hs;
            prevFlat = flat;
        }

        int[] Row(int[] px, int y)
        {
            int[] r = new int[w];
            Array.Copy(px, y * w, r, 0, w);
            for (int x = 0; x < w; x++) r[x] |= unchecked((int)0xFF000000); // screen copies have alpha = 0
            return r;
        }

        void HashRows(int[] px, ulong[] hs, bool[] flat)
        {
            for (int y = 0; y < h; y++)
            {
                int o = y * w, first = px[o] & 0xFFFFFF;
                ulong hash = 14695981039346656037UL;
                bool same = true;
                for (int x = 0; x < hashW; x++)
                {
                    int p = px[o + x] & 0xFFFFFF;
                    if (p != first) same = false;
                    hash = (hash ^ (uint)p) * 1099511628211UL;
                }
                hs[y] = hash;
                flat[y] = same;
            }
        }

        // Downward shift between the previous and current frame within [from, to): > 0 scrolled, 0 static, -1 not found
        // reliably.
        int BestShift(ulong[] hs, bool[] flat, int from, int to)
        {
            Dictionary<ulong, int> before = UniqueRows(prevHash, prevFlat, from, to);
            Dictionary<ulong, int> now = UniqueRows(hs, flat, from, to);
            Dictionary<int, int> votes = new Dictionary<int, int>();
            foreach (KeyValuePair<ulong, int> kv in now)
            {
                int j;
                if (kv.Value < 0 || !before.TryGetValue(kv.Key, out j) || j < 0) continue;
                int d = j - kv.Value, v;
                votes.TryGetValue(d, out v);
                votes[d] = v + 1;
            }
            int best = 0, bestVotes = 0;
            foreach (KeyValuePair<int, int> kv in votes)
            {
                if (kv.Value > bestVotes || (kv.Value == bestVotes && kv.Key == 0)) { best = kv.Key; bestVotes = kv.Value; }
            }
            if (bestVotes < 3) return Scan(hs, flat, from, to);
            if (best == 0) return 0;
            if (best < 0) return -1;

            // Confirmation: almost every non-flat row in the overlap must match.
            int considered = 0, matched = 0;
            for (int i = from; i < to - best; i++)
            {
                if (flat[i] && prevFlat[i + best]) continue;
                considered++;
                if (hs[i] == prevHash[i + best]) matched++;
            }
            if (considered < 4 || matched < considered * 0.8) return -1;
            return best;
        }

        // Fallback when voting finds too few unique rows: the shift whose overlap matches best, if clearly good.
        int Scan(ulong[] hs, bool[] flat, int from, int to)
        {
            int best = -1;
            double bestScore = 0.9;
            for (int d = 1; d < to - from - 8; d++)
            {
                int considered = 0, matched = 0;
                for (int i = from; i < to - d; i++)
                {
                    if (flat[i] && prevFlat[i + d]) continue;
                    considered++;
                    if (hs[i] == prevHash[i + d]) matched++;
                }
                if (considered < 8) continue;
                double sc = (double)matched / considered;
                if (sc > bestScore) { bestScore = sc; best = d; }
            }
            return best;
        }

        // Hash -> row, for non-flat rows that occur once (repeated rows don't tell position).
        static Dictionary<ulong, int> UniqueRows(ulong[] hs, bool[] flat, int from, int to)
        {
            Dictionary<ulong, int> d = new Dictionary<ulong, int>();
            for (int i = from; i < to; i++)
            {
                if (flat[i]) continue;
                if (d.ContainsKey(hs[i])) d[hs[i]] = -1;
                else d[hs[i]] = i;
            }
            return d;
        }

        public Bitmap Compose()
        {
            Bitmap b;
            lock (rows)
            {
                b = new Bitmap(w, Math.Max(1, rows.Count), PixelFormat.Format32bppArgb);
                BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < rows.Count; y++)
                    {
                        Marshal.Copy(rows[y], 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), w);
                        rows[y] = null; // release rows as they are copied to lower peak memory
                    }
                }
                finally { b.UnlockBits(bd); }
                rows.Clear();
            }
            return b;
        }
    }

    public class ScrollSession
    {
        const int Interval = 120;         // ms between grabs
        const int WheelGap = 70;          // ms between a settled frame and the next wheel step in Auto mode
        const int WheelTimeout = 1500;    // ms to wait for a settled frame after a wheel step
        const double AutoEndMs = 3200;    // Auto mode: hard stop when nothing grows for this long
        const long MaxPixels = 40000000;  // size cap (~160 MB in memory)

        readonly ShotStack owner;
        readonly Rectangle area;
        readonly Stitcher stitcher;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer();
        ScrollCaptureBar bar;
        ScrollEdge[] edges;
        Thread worker;
        volatile bool stopping, cancelled;
        volatile int lost;
        long lastGrowth;
        bool auto, escDown = true, enterDown = true, ended;
        double nextWheel;
        long wheelAt, doneT0 = -1;
        volatile int lastAdded;
        bool waiting;
        int idle, wheelDelta = -120;
        Point autoAt;

        public ScrollSession(ShotStack owner, Rectangle area)
        {
            this.owner = owner;
            this.area = area;
            float s = ShotStack.ScaleFor(Screen.FromRectangle(area));
            int ignore = Math.Min((int)Math.Round(24 * s), area.Width / 5);
            int maxRows = (int)Math.Min(30000, MaxPixels / Math.Max(1, area.Width));
            stitcher = new Stitcher(area.Width, area.Height, ignore, maxRows);
        }

        public int Height { get { return stitcher.Height; } }
        public bool Auto { get { return auto; } }
        public bool Full { get { return stitcher.IsFull; } }
        public bool Losing { get { return lost >= 3; } }

        public void Start()
        {
            edges = ScrollEdge.Around(area);
            bar = new ScrollCaptureBar(this, area);
            Interlocked.Exchange(ref lastGrowth, clock.ElapsedMilliseconds);
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Start();
            ui.Interval = 40;
            ui.Tick += Tick;
            ui.Start();
            ShotStack.Log("Captura con desplazamiento: " + area.Width + "x" + area.Height);
        }

        public void Finish()
        {
            if (stopping) return;
            stopping = true;
            auto = false;
            if (bar != null) bar.Saving();
        }

        public void Cancel()
        {
            cancelled = true;
            Finish();
        }

        public void ToggleAuto()
        {
            if (stopping) return;
            auto = !auto;
            if (!auto) return;
            // Wheel input goes to the window under the cursor, so park it in the middle of the area.
            autoAt = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
            Cursor.Position = autoAt;
            Interlocked.Exchange(ref lastGrowth, clock.ElapsedMilliseconds);
            nextWheel = clock.ElapsedMilliseconds + 60;
            waiting = false;
            idle = 0;
            wheelDelta = area.Height < 300 ? -60 : -120;
            if (bar != null) bar.Invalidate();
        }

        // UI thread: polls keys without stealing them, drives Auto scrolling and the bar.
        void Tick(object sender, EventArgs e)
        {
            if (ended) return;
            bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0, enter = (GetAsyncKeyState(0x0D) & 0x8000) != 0;
            if (esc && !escDown) Cancel();
            if (enter && !enterDown) Finish();
            escDown = esc;
            enterDown = enter;
            if (auto && !stopping)
            {
                Point p = Cursor.Position;
                double now = clock.ElapsedMilliseconds;
                if (Math.Abs(p.X - autoAt.X) > 4 || Math.Abs(p.Y - autoAt.Y) > 4) { auto = false; bar.Invalidate(); } // the user moved the mouse: stop Auto
                else if (Full || now - Interlocked.Read(ref lastGrowth) > AutoEndMs) Finish();
                else if (waiting)
                {
                    // One step at a time: the next wheel waits for a settled frame grabbed after the previous one.
                    bool got = Interlocked.Read(ref doneT0) > wheelAt;
                    if (got || now - wheelAt > WheelTimeout)
                    {
                        waiting = false;
                        if (got && lastAdded > 0) idle = 0;
                        else idle++;
                        if (lost >= 2 && wheelDelta < -30) { wheelDelta /= 2; lost = 0; }
                        if (idle >= 3 && now - Interlocked.Read(ref lastGrowth) > 600) Finish();
                        else nextWheel = now + WheelGap;
                    }
                }
                else if (now >= nextWheel)
                {
                    wheelAt = clock.ElapsedMilliseconds;
                    Wheel(wheelDelta);
                    waiting = true;
                }
            }
            if (bar != null) bar.Poll();
        }

        // Capture thread: grabs and stitches at a fixed rate, then composes the result.
        void Loop()
        {
            Thread.Sleep(180); // let the region picker disappear first
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), bits, dib, old;
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = area.Width;
            bi.biHeight = -area.Height; // top-down
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            dib = Native.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = Native.SelectObject(mem, dib);
            int[] buf = new int[area.Width * area.Height], alt = null;
            string error = null;
            try
            {
                while (!stopping)
                {
                    long t0 = clock.ElapsedMilliseconds;
                    Grab(mem, screen, bits, buf);
                    bool settled = true;
                    if (auto)
                    {
                        // Smooth-scroll animations: wait until two consecutive grabs match before stitching.
                        if (alt == null) alt = new int[buf.Length];
                        for (int k = 0; k < 12 && !stopping; k++)
                        {
                            Thread.Sleep(25);
                            Grab(mem, screen, bits, alt);
                            settled = SameFrame(buf, alt);
                            int[] t = buf; buf = alt; alt = t;
                            if (settled) break;
                        }
                    }
                    Stitcher.Result r;
                    int added;
                    r = Stitcher.Result.Same;
                    added = 0;
                    if (settled)   // a frame still moving would stitch badly; the next grab retries
                    {
                        int[] free = stitcher.Add(buf, out r, out added);
                        buf = free ?? new int[area.Width * area.Height];
                    }
                    if (added > 0) { lost = 0; Interlocked.Exchange(ref lastGrowth, clock.ElapsedMilliseconds); }
                    else if (r == Stitcher.Result.Lost) lost++;
                    lastAdded = added;
                    Interlocked.Exchange(ref doneT0, t0);
                    int wait = Interval - (int)(clock.ElapsedMilliseconds - t0);
                    if (wait > 0) Thread.Sleep(wait);
                }
            }
            catch (Exception ex) { error = ex.Message; }
            finally
            {
                Native.SelectObject(mem, old);
                Native.DeleteObject(dib);
                Native.DeleteDC(mem);
                Native.ReleaseDC(IntPtr.Zero, screen);
            }
            Bitmap result = null;
            if (!cancelled && error == null && stitcher.Height > 0)
            {
                try { result = stitcher.Compose(); }
                catch (Exception ex) { error = ex.Message; }
            }
            if (error != null) ShotStack.Log("Captura con desplazamiento: " + error);
            owner.Ui(delegate { Done(result); });
        }

        void Grab(IntPtr mem, IntPtr screen, IntPtr bits, int[] dst)
        {
            Native.BitBlt(mem, 0, 0, area.Width, area.Height, screen, area.X, area.Y, Native.SRCCOPY);
            Native.GdiFlush(); // the copy is done before its bits are read
            Marshal.Copy(bits, dst, 0, dst.Length);
        }

        static bool SameFrame(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++) if (((a[i] ^ b[i]) & 0xFFFFFF) != 0) return false;
            return true;
        }

        void Done(Bitmap result)
        {
            ended = true;
            ui.Stop();
            ui.Dispose();
            if (bar != null) bar.Close();
            if (edges != null) foreach (ScrollEdge e in edges) e.Close();
            ScrollCapture.Ended(this);
            if (result == null) return;
            ShotStack.Log("Captura con desplazamiento: " + result.Width + "x" + result.Height);
            owner.SaveCapture(result, "Desplazamiento");
        }

        // Synthetic wheel (SendInput) and key polling without registering hotkeys.

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public int type; public MOUSEINPUT mi; }

        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

        static void Wheel(int delta)
        {
            INPUT[] i = new INPUT[1];
            i[0].type = 0;                  // INPUT_MOUSE
            i[0].mi.mouseData = delta;
            i[0].mi.dwFlags = 0x0800;       // MOUSEEVENTF_WHEEL
            SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // Floating HUD capsule next to the area, with a soft shadow (per-pixel window). Excluded from the capture.
    class ScrollCaptureBar : FloatWindow
    {
        static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
        readonly ScrollSession session;
        bool saving;
        int hot = -1, shownHeight = -1;
        string shownState = "";
        double pulse;
        System.Windows.Media.Imaging.BitmapSource shadow;

        public ScrollCaptureBar(ScrollSession session, Rectangle area)
        {
            this.session = session;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            Pad = P(22);
            Size sz = new Size(P(400), P(48));
            Rectangle wa = scr.WorkingArea;
            int x = area.X + (area.Width - sz.Width) / 2;
            int y = area.Bottom + P(14);
            if (y + sz.Height > wa.Bottom) y = area.Top - sz.Height - P(14);
            if (y < wa.Top) y = area.Bottom - sz.Height - P(18); // full-screen area: place it inside (it is excluded from the capture anyway)
            x = Math.Max(wa.Left + P(8), Math.Min(wa.Right - sz.Width - P(8), x));
            SetSize(sz);
            JumpTo(x, y + P(10));
            ShowQuiet();
            alpha.Go(1, 200, 0, Ease.OutCubic, null);
            MoveTo(x, y, 320, 0.78, 0);
        }

        protected override bool PerPixel { get { return true; } }

        // The session asks for repaints through Invalidate.
        public new void Invalidate() { Redraw(); }

        public void Saving() { saving = true; hot = -1; Redraw(); }

        // Repaints only on visible changes (polled every 40 ms).
        public void Poll()
        {
            pulse += 0.04;
            string state = State();
            if (session.Height == shownHeight && state == shownState && !session.Auto && !saving) return;
            Redraw();
        }

        string State()
        {
            if (saving) return "Componiendo\u2026";
            if (session.Full) return "Altura m\u00E1xima";
            if (session.Losing) return "M\u00E1s despacio";
            if (session.Auto) return "Bajando solo\u2026";
            return "Baja con la rueda \u00B7 Esc cancela";
        }

        // Layout in body coordinates.
        Rectangle AutoRect() { return new Rectangle(body.Width - P(40) - P(78) - P(6) - P(64), P(9), P(64), body.Height - P(18)); }
        Rectangle DoneRect() { return new Rectangle(body.Width - P(40) - P(78), P(9), P(78), body.Height - P(18)); }
        Rectangle CancelRect() { return new Rectangle(body.Width - P(38), P(9), P(30), body.Height - P(18)); }

        static System.Windows.Rect R(Rectangle r) { return new System.Windows.Rect(r.X, r.Y, r.Width, r.Height); }

        static System.Windows.Media.FormattedText Line(string text, System.Windows.Media.Typeface face, double px, System.Windows.Media.Color c, double max)
        {
            System.Windows.Media.FormattedText t = Ink.Px(text, face, px, c);
            t.MaxTextWidth = Math.Max(1, max);
            t.MaxLineCount = 1;
            t.Trimming = System.Windows.TextTrimming.CharacterEllipsis;
            return t;
        }

        protected override void PaintSurface(System.Windows.Media.DrawingContext dc, int w, int h)
        {
            if (shadow == null)
                shadow = Ink.Shadow(w, h, new System.Windows.Rect(Pad, Pad + P(5), body.Width, body.Height), P(14), P(16), Ds.Argb(0.45, 0, 0, 0));
            dc.DrawImage(shadow, new System.Windows.Rect(0, 0, w, h));
            dc.PushTransform(new System.Windows.Media.TranslateTransform(Pad, Pad));
            System.Windows.Rect b = new System.Windows.Rect(0, 0, body.Width, body.Height);
            Ink.Round(dc, Ds.Argb(0.93, 30, 30, 32), b, P(14));
            Ink.Hairline(dc, Palette.HudLine, b, P(14));
            double cy = body.Height / 2.0;
            shownHeight = session.Height;
            shownState = State();
            System.Windows.Media.Color white = Palette.HudLabel, dim = Palette.HudLabel2, accent = Ds.Rgb(10, 132, 255);

            // Badge: accent tile with a down arrow, breathing while Auto scrolls.
            double a = session.Auto ? 0.6 + 0.4 * Math.Cos(pulse * Math.PI * 2) : 1;
            System.Windows.Rect ic = new System.Windows.Rect(P(12), cy - P(14), P(28), P(28));
            Ink.Round(dc, Ds.WithAlpha(accent, a), ic, P(8));
            double gs = P(16);
            Glyph.Draw(dc, "down", ic.X + (ic.Width - gs) / 2, cy - gs / 2 + P(1), gs, white, Math.Max(1.6, 2 * s));

            int tx = P(50), tw = AutoRect().X - tx - P(8);
            string hs = shownHeight > 0 ? shownHeight.ToString("N0", Es) + " px" : "Preparando\u2026";
            System.Windows.Media.FormattedText ht = Line(hs, Ds.Semibold, P(14), white, tw);
            dc.DrawText(ht, new System.Windows.Point(tx, Math.Round(cy - ht.Height + P(2))));
            System.Windows.Media.Color sc = session.Losing || session.Full ? Ds.Rgb(255, 105, 97) : dim;
            System.Windows.Media.FormattedText st = Line(shownState, Ds.Regular, P(11.5f), sc, tw);
            dc.DrawText(st, new System.Windows.Point(tx, Math.Round(cy + P(1))));
            if (!saving)
            {
                System.Windows.Rect ar = R(AutoRect());
                Ink.Round(dc, session.Auto ? Ds.Argb(0.22, 10, 132, 255) : hot == 0 ? Palette.HudHover : Ds.Argb(0.10, 255, 255, 255), ar, ar.Height / 2);
                if (session.Auto)
                {
                    System.Windows.Media.Pen pen = new System.Windows.Media.Pen(Ds.Brush(Ds.WithAlpha(accent, 0.9)), 1);
                    pen.Freeze();
                    System.Windows.Rect ai = ar;
                    ai.Inflate(-0.5, -0.5);
                    dc.DrawRoundedRectangle(null, pen, ai, ai.Height / 2, ai.Height / 2);
                }
                Ink.Center(dc, Ink.Px(session.Auto ? "Pausa" : "Auto", Ds.Semibold, P(13), session.Auto ? Ds.Rgb(120, 180, 255) : white), ar);

                System.Windows.Rect dr = R(DoneRect());
                Ink.Round(dc, hot == 1 ? Ds.Rgb(64, 156, 255) : accent, dr, dr.Height / 2);
                Ink.Center(dc, Ink.Px("Listo", Ds.Semibold, P(13), white), dr);

                System.Windows.Rect cr = R(CancelRect());
                if (hot == 2) dc.DrawEllipse(Ds.Brush(Palette.HudHover), null, new System.Windows.Point(cr.X + cr.Width / 2, cy), cr.Width / 2, cr.Width / 2);
                double cs = P(14);
                Glyph.Draw(dc, "close", cr.X + (cr.Width - cs) / 2, cy - cs / 2, cs, hot == 2 ? white : dim, P(1.8f));
            }
            else
            {
                // Composing: three dots breathing in turn.
                for (int i = 0; i < 3; i++)
                {
                    double ph = (pulse * 1.6 - i * 0.25) % 1.0, k = 0.35 + 0.65 * Math.Max(0, Math.Sin(ph * Math.PI));
                    dc.DrawEllipse(Ds.Brush(Ds.WithAlpha(white, k)), null, new System.Windows.Point(body.Width - P(36) + i * P(9), cy), P(2.5f), P(2.5f));
                }
            }
            dc.Pop();
        }

        Point Local(Point p) { return new Point(p.X - Pad, p.Y - Pad); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = Local(e.Location);
            int h = saving ? -1 : AutoRect().Contains(p) ? 0 : DoneRect().Contains(p) ? 1 : CancelRect().Contains(p) ? 2 : -1;
            if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Redraw(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Redraw(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || saving) return;
            Point p = Local(e.Location);
            if (AutoRect().Contains(p)) session.ToggleAuto();
            else if (DoneRect().Contains(p)) session.Finish();
            else if (CancelRect().Contains(p)) session.Cancel();
        }
    }

    // Thin frame around the area: four click-through strips, excluded from the capture. Each strip draws its part of a
    // 1 px white ring with a faint dark halo on both sides.
    class ScrollEdge : FloatWindow
    {
        const int T = 3;
        readonly Rectangle ring, strip;

        ScrollEdge(Rectangle r, Rectangle area)
        {
            strip = r;
            ring = Rectangle.Inflate(area, T / 2 + 1, T / 2 + 1);
            SetSize(r.Size);
            JumpTo(r.X, r.Y);
            alpha.Set(1);
            ShowQuiet();
            ApplyAlpha();
        }

        protected override bool Rounded { get { return false; } }
        protected override bool PerPixel { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: wheel and clicks pass through
                return cp;
            }
        }

        protected override void PaintSurface(System.Windows.Media.DrawingContext dc, int w, int h)
        {
            dc.DrawRectangle(Ds.Brush(Ds.Argb(0.30, 0, 0, 0)), null, new System.Windows.Rect(0, 0, w, h));
            System.Windows.Media.Brush white = Ds.Brush(Ds.Argb(0.95, 255, 255, 255));
            int ox = ring.X - strip.X, oy = ring.Y - strip.Y;
            dc.DrawRectangle(white, null, new System.Windows.Rect(ox, oy, ring.Width, 1));
            dc.DrawRectangle(white, null, new System.Windows.Rect(ox, oy + ring.Height - 1, ring.Width, 1));
            dc.DrawRectangle(white, null, new System.Windows.Rect(ox, oy, 1, ring.Height));
            dc.DrawRectangle(white, null, new System.Windows.Rect(ox + ring.Width - 1, oy, 1, ring.Height));
        }

        public static ScrollEdge[] Around(Rectangle a)
        {
            return new ScrollEdge[]
            {
                new ScrollEdge(new Rectangle(a.X - T, a.Y - T, a.Width + 2 * T, T), a),
                new ScrollEdge(new Rectangle(a.X - T, a.Bottom, a.Width + 2 * T, T), a),
                new ScrollEdge(new Rectangle(a.X - T, a.Y, T, a.Height), a),
                new ScrollEdge(new Rectangle(a.Right, a.Y, T, a.Height), a)
            };
        }
    }
}
