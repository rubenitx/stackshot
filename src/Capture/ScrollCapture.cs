// Stackshot - Captura con desplazamiento: una página entera, cosida mientras se baja con la rueda.
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
    // Se elige el área y se baja con la rueda (o con "Auto"): cada poco se copia el área y se pega debajo lo que
    // ha aparecido nuevo. La cabecera y el pie fijos de la página no se repiten. Esc cancela; Enter, "Listo" o el
    // mismo atajo terminan y la captura entra en la pila como cualquier otra.
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
            if (r.Width < 40 || r.Height < 40) return;
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

    // Une fotogramas sucesivos de un área que baja. Cada fila se resume en un hash; el desplazamiento entre dos
    // fotogramas es el que más filas únicas explica (votación) y se confirma comparando todo el solape.
    // Lo que no se mueve arriba (cabecera) y abajo (pie) se queda fuera: solo se añade lo que aparece encima del pie.
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

        // Añade un fotograma (píxeles de arriba abajo, w × h). Devuelve el búfer que ya no se usa (para reutilizarlo):
        // px si no hacía falta guardarlo o el fotograma anterior si px pasa a ser el nuevo de referencia.
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

            // Filas que siguen igual en el mismo sitio: arriba, la cabecera; abajo, el pie.
            int top = 0;
            while (top < h && hs[top] == prevHash[top]) top++;
            if (top == h) { result = Result.Same; return px; }
            int bot = 0;
            while (bot < h - top && hs[h - 1 - bot] == prevHash[h - 1 - bot]) bot++;

            int d = BestShift(hs, flat, top, h - bot);
            if (d == 0) { result = Result.Same; return px; }       // algo animado, pero no se ha desplazado
            // No encaja: si solo ha cambiado una franja pequeña (un cursor que parpadea, un vídeo) no se ha desplazado;
            // si no, se ha ido demasiado de golpe o hacia arriba.
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
            for (int x = 0; x < w; x++) r[x] |= unchecked((int)0xFF000000); // la copia de pantalla trae el alfa a 0
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

        // Cuánto ha bajado el contenido entre [from, to) del fotograma anterior y del nuevo: > 0 si ha bajado,
        // 0 si no se ha movido y -1 si no se encuentra con seguridad.
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
            if (bestVotes < 3) return -1;
            if (best == 0) return 0;
            if (best < 0) return -1;

            // Confirmación: en el solape, casi todas las filas con contenido tienen que coincidir.
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

        // Hash -> fila, solo para filas con contenido que no se repiten (las repetidas no dicen dónde se está).
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
                        rows[y] = null; // se suelta según se copia: menos memoria en el pico
                    }
                }
                finally { b.UnlockBits(bd); }
                rows.Clear();
            }
            return b;
        }
    }

    // Una captura con desplazamiento en marcha.
    public class ScrollSession
    {
        const int Interval = 120;         // ms entre copias del área
        const int WheelEvery = 170;       // ms entre pasos de rueda en modo automático
        const double AutoEndMs = 1600;    // en automático, sin nada nuevo durante este rato = final de la página
        const long MaxPixels = 40000000;  // tope de tamaño (unos 160 MB en memoria)

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
            // El ratón va al centro del área: la rueda la recibe la ventana que hay debajo.
            autoAt = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
            Cursor.Position = autoAt;
            Interlocked.Exchange(ref lastGrowth, clock.ElapsedMilliseconds);
            nextWheel = clock.ElapsedMilliseconds + 60;
            if (bar != null) bar.Invalidate();
        }

        // Hilo de la interfaz: teclas (sin quitárselas a nadie), rueda automática y la barrita.
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
                if (Math.Abs(p.X - autoAt.X) > 4 || Math.Abs(p.Y - autoAt.Y) > 4) { auto = false; bar.Invalidate(); } // el usuario toma el mando
                else if (Full || now - Interlocked.Read(ref lastGrowth) > AutoEndMs) Finish();
                else if (now >= nextWheel)
                {
                    Wheel(-120);
                    nextWheel = now + WheelEvery;
                }
            }
            if (bar != null) bar.Poll();
        }

        // Hilo de captura: copia el área a ritmo fijo y la cose. Al terminar compone la imagen aquí mismo.
        void Loop()
        {
            Thread.Sleep(180); // que termine de irse el selector de área
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), bits, dib, old;
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = area.Width;
            bi.biHeight = -area.Height; // de arriba abajo
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            dib = Native.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = Native.SelectObject(mem, dib);
            int[] buf = new int[area.Width * area.Height];
            string error = null;
            try
            {
                while (!stopping)
                {
                    long t0 = clock.ElapsedMilliseconds;
                    Native.BitBlt(mem, 0, 0, area.Width, area.Height, screen, area.X, area.Y, Native.SRCCOPY);
                    Marshal.Copy(bits, buf, 0, buf.Length);
                    Stitcher.Result r;
                    int added;
                    int[] free = stitcher.Add(buf, out r, out added);
                    buf = free ?? new int[area.Width * area.Height];
                    if (added > 0) { lost = 0; Interlocked.Exchange(ref lastGrowth, clock.ElapsedMilliseconds); }
                    else if (r == Stitcher.Result.Lost) lost++;
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

        // ---- Rueda simulada (SendInput) y teclas leídas sin registrar atajos

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

    // Barrita junto al área: alto capturado, Auto, Listo y ✕. No sale en la captura.
    class ScrollCaptureBar : FloatWindow
    {
        static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
        readonly ScrollSession session;
        bool saving;
        int hot = -1, shownHeight = -1;
        string shownState = "";
        double pulse;

        public ScrollCaptureBar(ScrollSession session, Rectangle area)
        {
            this.session = session;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            BackColor = Theme.Dark;
            Size sz = new Size(P(400), P(48));
            Rectangle wa = scr.WorkingArea;
            int x = area.X + (area.Width - sz.Width) / 2;
            int y = area.Bottom + P(12);
            if (y + sz.Height > wa.Bottom) y = area.Top - sz.Height - P(12);
            if (y < wa.Top) y = area.Bottom - sz.Height - P(16); // el área ocupa toda la pantalla: dentro (no sale en la captura)
            x = Math.Max(wa.Left + P(8), Math.Min(wa.Right - sz.Width - P(8), x));
            SetSize(sz);
            JumpTo(x, y + P(8));
            ShowQuiet();
            alpha.Go(1, 180, 0, Ease.OutCubic, null);
            MoveTo(x, y, 320, 0.8, 0);
        }

        public void Saving() { saving = true; hot = -1; Invalidate(); }

        // Solo repinta si cambia algo visible (la llama un temporizador cada 40 ms).
        public void Poll()
        {
            pulse += 0.04;
            string state = State();
            if (session.Height == shownHeight && state == shownState && !session.Auto && !saving) return;
            Invalidate();
        }

        string State()
        {
            if (saving) return "Componiendo\u2026";
            if (session.Full) return "Altura m\u00E1xima";
            if (session.Losing) return "M\u00E1s despacio";
            if (session.Auto) return "Bajando solo\u2026";
            return "Baja con la rueda \u00B7 Esc cancela";
        }

        Rectangle AutoRect() { return new Rectangle(ClientSize.Width - P(40) - P(78) - P(6) - P(64), P(9), P(64), ClientSize.Height - P(18)); }
        Rectangle DoneRect() { return new Rectangle(ClientSize.Width - P(40) - P(78), P(9), P(78), ClientSize.Height - P(18)); }
        Rectangle CancelRect() { return new Rectangle(ClientSize.Width - P(38), P(9), P(30), ClientSize.Height - P(18)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Dark);
            int cy = ClientSize.Height / 2;
            shownHeight = session.Height;
            shownState = State();

            // Icono: dos hojas apiladas con una flecha hacia abajo, que respira mientras baja solo.
            Rectangle ic = new Rectangle(P(12), cy - P(13), P(26), P(26));
            double a = session.Auto ? 0.6 + 0.4 * Math.Cos(pulse * Math.PI * 2) : 1;
            using (GraphicsPath p = Theme.Round(ic, P(7)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * a), Theme.Accent))) g.FillPath(b, p);
            DrawGlyph(g, "\uE74B", ic, Theme.Dark, P(13)); // flecha abajo

            using (Font big = new Font("Segoe UI Semibold", P(14), GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", P(11), GraphicsUnit.Pixel))
            {
                int tx = P(48), tw = AutoRect().X - tx - P(6);
                string h = shownHeight > 0 ? shownHeight.ToString("N0", Es) + " px" : "Preparando\u2026";
                TextRenderer.DrawText(g, h, big, new Rectangle(tx, P(5), tw, P(22)), Theme.Fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                Color sc = session.Losing || session.Full ? Theme.Red : Theme.Muted;
                TextRenderer.DrawText(g, shownState, small, new Rectangle(tx, P(26), tw, P(16)), sc,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            if (saving) return;

            using (Font f = new Font("Segoe UI Semibold", P(13), GraphicsUnit.Pixel))
            {
                Rectangle ar = AutoRect();
                Color abg = session.Auto ? Theme.ButtonHover : hot == 0 ? Theme.ButtonHover : Theme.Button;
                using (GraphicsPath p = Theme.Round(ar, ar.Height / 2f))
                using (SolidBrush b = new SolidBrush(abg)) g.FillPath(b, p);
                if (session.Auto) using (GraphicsPath p = Theme.Round(ar, ar.Height / 2f)) using (Pen pen = new Pen(Theme.Accent, Math.Max(1f, 1.5f * s))) g.DrawPath(pen, p);
                TextRenderer.DrawText(g, session.Auto ? "Pausa" : "Auto", f, ar, session.Auto ? Theme.Accent : Theme.Fg,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                Rectangle dr = DoneRect();
                using (GraphicsPath p = Theme.Round(dr, dr.Height / 2f))
                using (SolidBrush b = new SolidBrush(hot == 1 ? Theme.Purple : Theme.Accent)) g.FillPath(b, p);
                TextRenderer.DrawText(g, "Listo", f, dr, Theme.Dark, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            Rectangle cr = CancelRect();
            if (hot == 2) using (SolidBrush b = new SolidBrush(Theme.ButtonHover)) g.FillEllipse(b, cr);
            DrawGlyph(g, "\uE711", cr, hot == 2 ? Theme.Fg : Theme.Muted, P(11));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = saving ? -1 : AutoRect().Contains(e.Location) ? 0 : DoneRect().Contains(e.Location) ? 1 : CancelRect().Contains(e.Location) ? 2 : -1;
            if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || saving) return;
            if (AutoRect().Contains(e.Location)) session.ToggleAuto();
            else if (DoneRect().Contains(e.Location)) session.Finish();
            else if (CancelRect().Contains(e.Location)) session.Cancel();
        }
    }

    // Marco azul alrededor del área: cuatro tiras finas que dejan pasar el ratón y no salen en la captura.
    class ScrollEdge : FloatWindow
    {
        ScrollEdge(Rectangle r)
        {
            BackColor = Theme.Accent;
            SetSize(r.Size);
            JumpTo(r.X, r.Y);
            alpha.Set(0.9);
            ShowQuiet();
            ApplyAlpha();
        }

        protected override bool Rounded { get { return false; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: la rueda y los clics pasan a lo que hay debajo
                return cp;
            }
        }

        public static ScrollEdge[] Around(Rectangle a)
        {
            int t = Math.Max(2, (int)Math.Round(2 * ShotStack.ScaleFor(Screen.FromRectangle(a))));
            return new ScrollEdge[]
            {
                new ScrollEdge(new Rectangle(a.X - t, a.Y - t, a.Width + 2 * t, t)),
                new ScrollEdge(new Rectangle(a.X - t, a.Bottom, a.Width + 2 * t, t)),
                new ScrollEdge(new Rectangle(a.X - t, a.Y, t, a.Height)),
                new ScrollEdge(new Rectangle(a.Right, a.Y, t, a.Height))
            };
        }
    }
}
