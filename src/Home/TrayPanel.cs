// Stackshot - Tray panel: what the tray icon opens, a calm macOS-style menu with captures, shortcuts and recents.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using D = System.Drawing;
using DD = System.Drawing.Drawing2D;
using DI = System.Drawing.Imaging;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;
using W = System.Windows;

namespace Stackshot
{
    // Borderless per-pixel popup next to the tray: header, capture actions with their shortcuts, the latest captures
    // and the app commands. Built once and reused, so it opens at once; hidden it costs nothing (no timers). The whole
    // panel is two cached layers (normal and highlighted), so hover only moves a rounded accent pill between them.
    // Commands run once it has faded out; it is excluded from capture anyway.
    public sealed class TrayPanel : FloatWindow
    {
        // region, screen, window, scroll, video, gif, home, folder, closeall, settings, quit.
        public Action<string> Command;
        public Action<string> OpenCapture;   // full path of a recent capture
        public Func<bool> HasCards;

        const int KRow = 0, KLine = 1, KHead = 2, KStrip = 3;
        const int Recent = 4;
        const float RowH = 30, Inset = 6, Radius = 12, MinWidth = 292, Gap = 10;
        const double FlashMs = 90;
        static readonly string[] Captures = { "region", "screen", "window", "scroll", "video", "gif" };

        sealed class Item
        {
            public int Kind, Group;
            public string Id, Text, Icon;
            public string[] Caps;
            public bool Enabled = true, Alert;
            public W.Rect R;   // body coordinates
        }

        readonly Settings settings;
        readonly List<Item> items = new List<Item>();
        readonly W.Rect[] thumbR = new W.Rect[Recent];
        readonly double[] thumbA = new double[Recent];
        readonly Dictionary<string, MI.BitmapSource> thumbs = new Dictionary<string, MI.BitmapSource>(StringComparer.OrdinalIgnoreCase);
        readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer();
        List<FileInfo> recent = new List<FileInfo>();
        int today = -1, thumbPx;
        double recentAt = -1e9;
        bool loading;
        string status, layoutKey;
        bool statusAlert;
        MI.BitmapSource baseLayer, selLayer;
        ShadowTile softShadow, tightShadow;
        string shadowKey;
        Dib dib;
        W.Rect drawnHi = W.Rect.Empty;
        bool stripDirty, stripShown;

        int hot = -1, hotThumb = -1, labelThumb = -1;
        double hiY, hiH, hiA, tY, tH, tA, lastTick;
        bool closing, flashing, restoreAfter, buttonsDown;
        double flashAt, closedAt = -1e9;
        string pendingCommand, pendingFile;
        IntPtr previous;
        D.Point openAt, closedPt;
        int edge;   // where the taskbar is: 0 bottom, 1 top, 2 left, 3 right

        public TrayPanel(Settings settings)
        {
            this.settings = settings;
            Text = "Stackshot";
            watch.Interval = 30;
            watch.Tick += WatchTick;
            Ds.Changed += OnTheme;
        }

        protected override bool PerPixel { get { return true; } }
        protected override bool ShowWithoutActivation { get { return false; } }

        // Unlike the other floating windows it takes the focus, like a menu: Esc and clicks elsewhere close it.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle &= ~0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = (IntPtr)1; return; }   // WM_MOUSEACTIVATE -> MA_ACTIVATE
            if (m.Msg == 0x02E0) { m.Result = IntPtr.Zero; return; } // WM_DPICHANGED: laid out for its monitor on every open
            base.WndProc(ref m);
        }

        double U(double v) { return v * s; }
        double Px(double v) { return Math.Round(v * s); }

        // ---- Opening and closing

        // Builds everything once, quietly, so the first open is as fast as the rest.
        public void Prewarm()
        {
            if (IsDisposed || Visible) return;
            try
            {
                if (!IsHandleCreated) CreateHandle(); // first, while empty: this class paints itself, not the base surface
                Screen scr = Screen.FromPoint(Control.MousePosition);
                float scale = ShotStack.ScaleFor(scr);
                Rebuild(scale, Fit(scr, scale));
                Repaint();
                Warm();
            }
            catch (Exception ex) { ShotStack.Log("Panel de la bandeja: " + ex.Message); }
        }

        // Tray icon clicked: open it, or close it if it is open.
        public void Toggle()
        {
            if (IsDisposed) return;
            if (Visible && !closing) { if (!flashing) BeginClose(true); return; }
            if (closing) return;
            if (Anim.Now - closedAt < 500 && Near(Control.MousePosition, closedPt)) return; // that same click on the icon is what just closed it
            try { Open(); }
            catch (Exception ex) { ShotStack.Log("Panel de la bandeja: " + ex); }
        }

        // Gone at once, without a command (the double click that opens the main window).
        public void CloseNow()
        {
            if (!Visible) return;
            pendingCommand = pendingFile = null;
            alpha.Set(0);
            ApplyAlpha();
            Gone();
        }

        void Open()
        {
            previous = Previous();
            openAt = Control.MousePosition;
            Screen scr = Screen.FromPoint(openAt);
            if (!IsHandleCreated) CreateHandle();
            float scale = ShotStack.ScaleFor(scr);
            Rebuild(scale, Fit(scr, scale));
            ResetHover();
            closing = flashing = false;
            pendingCommand = pendingFile = null;
            D.Point to = Place(scr), from = to;
            int slide = P(8);
            if (edge == 0) from.Y += slide; else if (edge == 1) from.Y -= slide; else if (edge == 2) from.X -= slide; else from.X += slide;
            alpha.Set(0);
            ApplyAlpha();
            Repaint();
            JumpTo(from.X, from.Y);
            Show();
            Activate();
            if (Native.GetForegroundWindow() != Handle) Native.ForceForeground(Handle);
            buttonsDown = AnyButton();
            watch.Start();
            alpha.Go(1, 150, 0, Ease.OutCubic, null);
            MoveTo(to.X, to.Y, 600, 1, 0);
            Warm();
        }

        // Next to the tray: above a bottom taskbar (below a top one, beside a side one), centered on the click and
        // kept inside the monitor's work area.
        D.Point Place(Screen scr)
        {
            D.Rectangle wa = scr.WorkingArea, b = scr.Bounds;
            int bw = body.Width, bh = body.Height, m = P(Gap), bar = P(52);
            D.Point c = openAt;
            bool reserved = true;
            if (wa.Bottom < b.Bottom) edge = 0;
            else if (wa.Top > b.Top) edge = 1;
            else if (wa.Left > b.Left) edge = 2;
            else if (wa.Right < b.Right) edge = 3;
            else
            {
                // Auto-hidden taskbar: the edge nearest to the click.
                reserved = false;
                int db = b.Bottom - c.Y, dt = c.Y - b.Top, dl = c.X - b.Left, dr = b.Right - c.X;
                int min = Math.Min(Math.Min(db, dt), Math.Min(dl, dr));
                edge = min == db ? 0 : min == dt ? 1 : min == dl ? 2 : 3;
            }
            int x = c.X - bw / 2, y = c.Y - bh / 2;
            if (edge == 0) y = (reserved ? wa.Bottom : b.Bottom - bar) - m - bh;
            else if (edge == 1) y = (reserved ? wa.Top : b.Top + bar) + m;
            else if (edge == 2) x = (reserved ? wa.Left : b.Left + bar) + m;
            else x = (reserved ? wa.Right : b.Right - bar) - m - bw;
            x = Math.Max(wa.Left + m, Math.Min(wa.Right - m - bw, x));
            y = bh > wa.Height - 2 * m ? wa.Top + m : Math.Max(wa.Top + m, Math.Min(wa.Bottom - m - bh, y));
            return new D.Point(x, y);
        }

        void BeginClose(bool restore)
        {
            if (closing || !Visible) return;
            closing = true;
            watch.Stop();
            if (restore) Restore();
            alpha.Go(0, 120, 0, Ease.OutCubic, Gone);
            Anim.Wake(this);
        }

        void Gone()
        {
            Hide();
            watch.Stop();
            closing = flashing = false;
            ResetHover();
            previous = IntPtr.Zero;
            string cmd = pendingCommand, file = pendingFile;
            pendingCommand = pendingFile = null;
            if (cmd == null && file == null) return;
            BeginInvoke((Action)delegate
            {
                try
                {
                    if (file != null) { if (OpenCapture != null) OpenCapture(file); }
                    else if (Command != null) Command(cmd);
                }
                catch (Exception ex) { ShotStack.Log("Panel (" + (cmd ?? file) + "): " + ex.Message); }
            });
        }

        // A short blink of the chosen row, as on macOS, then fade out and run it.
        void Choose(string command, string file)
        {
            if (closing || flashing) return;
            pendingCommand = command;
            pendingFile = file;
            // Captures (and the commands that open nothing) give the focus back to the window that had it.
            restoreAfter = command != null && (Array.IndexOf(Captures, command) >= 0 || command == "closeall" || command == "quit");
            if (file != null) { BeginClose(false); return; }
            flashing = true;
            flashAt = Anim.Now;
            Anim.Wake(this);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!Visible || closing || flashing) return;
            Dismissed(Control.MousePosition);
        }

        // Closed by a press elsewhere; if that press was on the tray icon, its click must not open it again.
        void Dismissed(D.Point at)
        {
            closedAt = Anim.Now;
            closedPt = at;
            BeginClose(false);
        }

        bool Near(D.Point a, D.Point b)
        {
            int d = Math.Max(8, P(16));
            return Math.Abs(a.X - b.X) <= d && Math.Abs(a.Y - b.Y) <= d;
        }

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

        // The buttons as they are now, wherever the pointer is (Control.MouseButtons only sees this thread's input).
        static bool AnyButton()
        {
            return (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0 || (GetAsyncKeyState(0x04) & 0x8000) != 0;
        }

        // While open, a press anywhere else closes it: also on windows that never take the focus (the thumbnails, the
        // recording bar) and when Windows refused it the focus. Only presses that start while it is open count.
        void WatchTick(object sender, EventArgs e)
        {
            if (!Visible || closing) { watch.Stop(); return; }
            bool down = AnyButton(), pressed = down && !buttonsDown;
            buttonsDown = down;
            if (!pressed || flashing) return;
            D.Point p = Control.MousePosition;
            D.Rectangle me = new D.Rectangle(Location.X + Pad, Location.Y + Pad, body.Width, body.Height);
            if (!me.Contains(p)) Dismissed(p);
        }

        // ---- Focus: the window that was active before the tray click (the taskbar itself does not count)

        static readonly string[] ShellClasses = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
                                                  "Progman", "WorkerW", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow" };

        static IntPtr Previous()
        {
            try
            {
                IntPtr fg = Native.GetForegroundWindow();
                if (Usable(fg, true)) return fg;
                IntPtr found = IntPtr.Zero;
                Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    if (!Usable(h, false)) return true;
                    found = h;
                    return false;
                }, IntPtr.Zero);
                return found;
            }
            catch { return IntPtr.Zero; }
        }

        static bool Usable(IntPtr h, bool foreground)
        {
            if (h == IntPtr.Zero || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return false;
            StringBuilder c = new StringBuilder(64);
            Native.GetClassName(h, c, 64);
            if (Array.IndexOf(ShellClasses, c.ToString()) >= 0) return false;
            int ex = Native.GetWindowLong(h, -20);
            if (!foreground)
            {
                if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return false;   // tool window, not an app window
                if ((ex & 0x08000000) != 0 || (ex & 0x20) != 0 || (ex & 0x8) != 0) return false; // no-activate, click-through or always-on-top overlays
                if (Native.GetAncestor(h, 3) != h) return false;              // owned pop-ups
            }
            int cloaked;
            if (Native.DwmGetInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
            Native.RECT r;
            return Native.GetWindowRect(h, out r) && r.Right - r.Left > 1 && r.Bottom - r.Top > 1;
        }

        void Restore()
        {
            IntPtr p = previous;
            previous = IntPtr.Zero;
            if (p == IntPtr.Zero || !IsHandleCreated || !Native.IsWindowVisible(p)) return;
            IntPtr fg = Native.GetForegroundWindow();
            if (fg != Handle && Usable(fg, true)) return; // the user already went to another window
            try { Native.SetForegroundWindow(p); } catch { }
        }

        // ---- Recent captures, listed on the thread pool (hovering the tray icon starts it)

        public void Warm()
        {
            if (IsDisposed || loading || Anim.Now - recentAt < 2500) return;
            if (!IsHandleCreated) CreateHandle();
            loading = true;
            Settings st = settings;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<FileInfo> list = null;
                int n = -1;
                try
                {
                    list = Recents.Latest(st, Recent);
                    n = Recents.PerDay(st, 1)[0];
                }
                catch (Exception ex) { ShotStack.Log("Panel, recientes: " + ex.Message); }
                try { BeginInvoke((Action)delegate { loading = false; recentAt = Anim.Now; Listed(list, n); }); }
                catch { loading = false; }
            });
        }

        void Listed(List<FileInfo> list, int n)
        {
            if (IsDisposed || list == null) return;
            bool same = n == today && list.Count == recent.Count;
            for (int i = 0; same && i < list.Count; i++)
                same = string.Equals(list[i].FullName, recent[i].FullName, StringComparison.OrdinalIgnoreCase) && list[i].LastWriteTimeUtc == recent[i].LastWriteTimeUtc;
            recent = list;
            today = n;
            if (same) return;
            foreach (string k in new List<string>(thumbs.Keys))
                if (recent.FindIndex(delegate(FileInfo f) { return string.Equals(f.FullName, k, StringComparison.OrdinalIgnoreCase); }) < 0) thumbs.Remove(k);
            if (Visible && !closing) Relayout();
            LoadThumbs();
        }

        void LoadThumbs()
        {
            if (thumbPx <= 0) return;
            foreach (FileInfo f in recent)
            {
                string path = f.FullName;
                Recents.Thumb(path, thumbPx, delegate(MI.BitmapSource src)
                {
                    if (IsDisposed || src == null) return;
                    MI.BitmapSource had;
                    if (thumbs.TryGetValue(path, out had) && had == src) return;
                    if (recent.FindIndex(delegate(FileInfo x) { return string.Equals(x.FullName, path, StringComparison.OrdinalIgnoreCase); }) < 0) return;
                    thumbs[path] = src;
                    baseLayer = null;
                    RedrawSoon();
                });
            }
        }

        // Thumbnails arrive one by one (sometimes in the middle of a layout): one repaint for all of them.
        void RedrawSoon()
        {
            if (redrawQueued || !IsHandleCreated || IsDisposed) return;
            redrawQueued = true;
            BeginInvoke((Action)delegate { redrawQueued = false; if (Visible) Repaint(); });
        }
        bool redrawQueued;

        // The content changed while open: lay out again, keeping it anchored to the tray.
        void Relayout()
        {
            if (!Rebuild(s, Fit(Screen.FromPoint(openAt), s))) return;
            ResetHover();
            D.Point to = Place(Screen.FromPoint(openAt));
            JumpTo(to.X, to.Y);
            Repaint();
        }

        void OnTheme()
        {
            if (!IsHandleCreated || IsDisposed) { layoutKey = null; return; }
            BeginInvoke((Action)delegate
            {
                layoutKey = null;
                shadowKey = null;
                if (Visible && !closing) Relayout();
            });
        }

        // ---- Content and layout

        Item Add(int kind, string id, string text, string icon, int group)
        {
            Item it = new Item();
            it.Kind = kind;
            it.Id = id;
            it.Text = text;
            it.Icon = icon;
            it.Group = group;
            items.Add(it);
            return it;
        }

        Item AddCapture(string id, string text, string icon, int group)
        {
            Item it = Add(KRow, id, text, icon, group);
            string combo = settings.HotkeysFor(id);
            if (Hotkeys.Split(combo).Count > 0)
            {
                it.Caps = Hotkeys.Display(combo).Split(new string[] { " + " }, StringSplitOptions.None);
                for (int i = 0; i < it.Caps.Length; i++) if (it.Caps[i] == "May\u00FAs") it.Caps[i] = ShiftKey;
                it.Caps[it.Caps.Length - 1] = KeyCap(Hotkeys.Split(combo)[0], it.Caps[it.Caps.Length - 1]);
            }
            return it;
        }

        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);

        // The key as printed on this keyboard ("," "-" "\u00D1") rather than its internal name ("Oemcomma").
        static string KeyCap(string combo, string shown)
        {
            uint mods;
            Keys key;
            if (!Hotkeys.TryParse(combo, out mods, out key)) return shown;
            if (key == Keys.Space) return "Espacio";
            if ((key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.F1 && key <= Keys.F24) || key == Keys.PrintScreen || key == Keys.Escape) return shown;
            uint ch = MapVirtualKey((uint)key, 2) & 0x7FFFFFFF; // MAPVK_VK_TO_CHAR; the top bit marks dead keys
            return ch > 32 ? char.ToUpper((char)ch, CultureInfo.CurrentCulture).ToString() : shown;
        }

        void BuildItems(bool recording, bool scrolling, bool strip)
        {
            items.Clear();
            Add(KHead, null, null, null, 0);
            Add(KLine, null, null, null, 0);
            AddCapture("region", "\u00C1rea", "area", 1);
            AddCapture("screen", "Pantalla", "screen", 1);
            AddCapture("window", "Ventana", "window", 1);
            AddCapture("scroll", scrolling ? "Terminar desplazamiento" : "Desplazamiento", "scroll", 1);
            Add(KLine, null, null, null, 0);
            AddCapture("video", recording ? "Detener grabaci\u00F3n" : "V\u00EDdeo", recording ? "stop!" : "video", 2).Alert = recording;
            if (!recording) AddCapture("gif", "GIF", "gif", 2);
            AddCapture("markdown", "Markdown", "markdown", 2);
            Add(KLine, null, null, null, 0);
            if (strip && recent.Count > 0)
            {
                Add(KStrip, null, null, null, 0);
                Add(KLine, null, null, null, 0);
            }
            Add(KRow, "home", "Abrir Stackshot", "home", 3);
            Add(KRow, "folder", "Abrir capturas guardadas", "folder", 3);
            Add(KRow, "closeall", "Cerrar todas las miniaturas", "stack", 3).Enabled = HasCards == null || HasCards();
            Add(KRow, "settings", "Ajustes\u2026", "gear", 3);
            Add(KLine, null, null, null, 0);
            Add(KRow, "quit", "Salir", "power", 4);
            statusAlert = recording;
            status = recording ? "Grabando la pantalla" : scrolling ? "Captura con desplazamiento en curso"
                   : today == 1 ? "1 captura hoy" : today > 1 ? today + " capturas hoy" : "Listo para capturar";
        }

        // Items from the current state; lays out (and drops the cached layers) only if anything changed. fit is the
        // height available on the monitor: when the panel would not fit, the recent captures make room.
        bool Rebuild(float scale, int fit)
        {
            s = scale;
            List<Item> laidOut = new List<Item>(items);
            bool rec = Recorder.Recording, scrolling = ScrollCapture.Active;
            BuildItems(rec, scrolling, true);
            StringBuilder k = new StringBuilder();
            k.Append(s).Append('|').Append(fit).Append('|').Append(Ds.Dark).Append('|').Append(status);
            foreach (Item it in items)
            {
                k.Append('|').Append(it.Kind).Append(it.Text).Append(it.Icon).Append(it.Enabled);
                if (it.Caps != null) k.Append(string.Join("+", it.Caps));
            }
            foreach (FileInfo f in recent) k.Append('|').Append(f.FullName);
            string key = k.ToString();
            if (key == layoutKey && baseLayer != null)
            {
                // Unchanged: keep the items already laid out (the fresh ones have no positions yet).
                items.Clear();
                items.AddRange(laidOut);
                return false;
            }
            layoutKey = key;
            LayoutItems();
            if (fit > 0 && body.Height > fit && recent.Count > 0)
            {
                BuildItems(rec, scrolling, false);
                LayoutItems();
            }
            baseLayer = selLayer = null;
            return true;
        }

        // Room for the panel on a monitor at a scale: its work area minus the margins.
        static int Fit(Screen scr, float scale)
        {
            return scr.WorkingArea.Height - 2 * (int)Math.Round(Gap * scale);
        }

        void LayoutItems()
        {
            Pad = P(36);
            double textW = 0, capsW = 0;
            foreach (Item it in items)
            {
                if (it.Kind != KRow) continue;
                textW = Math.Max(textW, Ink.Px(it.Text, Ds.Regular, U(13), M.Colors.Black).WidthIncludingTrailingWhitespace);
                if (it.Caps != null) capsW = Math.Max(capsW, KeycapsWidth(it.Caps, s));
            }
            double rows = U(Inset + 10 + 16 + 10) + textW + (capsW > 0 ? U(28) + capsW : 0) + U(10 + Inset);
            double head = U(16 + 32 + 12 + 16) + Math.Max(Ink.Px("Stackshot", Ds.Title, U(14.5), M.Colors.Black).WidthIncludingTrailingWhitespace,
                                                          Ink.Px(status, Ds.Regular, U(11.5), M.Colors.Black).WidthIncludingTrailingWhitespace + U(12));
            int width = (int)Math.Ceiling(Math.Max(U(MinWidth), Math.Max(rows, head)));
            double inset = Px(Inset), y = 0, side = Px(Inset + 10), gap = Px(8);
            double cw = Math.Floor((width - 2 * side - (Recent - 1) * gap) / Recent), ch = Math.Round(cw * 0.625);
            int px = (int)Math.Ceiling(cw * 2.4);
            if (px != thumbPx) { thumbPx = px; thumbs.Clear(); LoadThumbs(); } // sized even without the strip, so they load before it shows
            stripShown = false;
            foreach (Item it in items)
            {
                double h;
                if (it.Kind == KHead) { h = Px(60); it.R = new W.Rect(0, y, width, h); }
                else if (it.Kind == KLine) { h = Px(11); it.R = new W.Rect(0, y, width, h); }
                else if (it.Kind == KStrip)
                {
                    double top = Px(25);
                    for (int i = 0; i < Recent; i++) thumbR[i] = new W.Rect(side + i * (cw + gap), y + top, cw, ch);
                    stripShown = true;
                    h = top + ch + Px(9);
                    it.R = new W.Rect(0, y, width, h);
                }
                else { h = Px(RowH); it.R = new W.Rect(inset, y, width - 2 * inset, h); }
                y += h;
            }
            y += inset;
            SetSize(new D.Size(width, (int)Math.Ceiling(y)));
        }

        // ---- Painting

        internal static M.Color Back(bool dark) { return dark ? Ds.Rgb(40, 40, 43) : Ds.Rgb(248, 248, 249); }

        // The panel keeps its own pixels: a full repaint only when the content changes, and while hovering just the band
        // the highlight moved through (a tenth of the window), so animating costs next to nothing.
        void Repaint()
        {
            if (!IsHandleCreated || IsDisposed || body.Width <= 0 || body.Height <= 0) return;
            int w = body.Width + 2 * Pad, h = body.Height + 2 * Pad;
            bool full = dib == null || dib.Width != w || dib.Height != h || baseLayer == null || baseLayer.PixelWidth != w || baseLayer.PixelHeight != h;
            if (dib != null && (dib.Width != w || dib.Height != h)) { dib.Dispose(); dib = null; }
            if (dib == null) dib = new Dib(w, h, true);
            W.Rect hi = HighlightRect(), dirty = new W.Rect(0, 0, w, h);
            if (!full)
            {
                dirty = drawnHi;
                if (!hi.IsEmpty) dirty.Union(hi);
                if (stripDirty) dirty.Union(StripRect());
                if (dirty.IsEmpty) return;
                dirty.Inflate(3, 3);
                dirty.Intersect(new W.Rect(0, 0, w, h));
                if (dirty.IsEmpty) return;
            }
            int x = (int)Math.Floor(dirty.X), y = (int)Math.Floor(dirty.Y);
            int bw = Math.Min(w - x, (int)Math.Ceiling(dirty.Right) - x), bh = Math.Min(h - y, (int)Math.Ceiling(dirty.Bottom) - y);
            if (bw <= 0 || bh <= 0) return;
            MI.RenderTargetBitmap band = Ink.Render(bw, bh, delegate(M.DrawingContext dc)
            {
                dc.PushTransform(new M.TranslateTransform(-x, -y));
                Compose(dc, w, h);
                dc.Pop();
            }, null);
            Native.GdiFlush();
            int offset = (y * w + x) * 4;
            band.CopyPixels(new W.Int32Rect(0, 0, bw, bh), IntPtr.Add(dib.Bits, offset), w * 4 * h - offset, w * 4);
            Native.PresentHere(Handle, dib.Dc, w, h, (byte)Math.Max(0, Math.Min(255, Math.Round(alpha.Value * 255))));
            drawnHi = hi;
            stripDirty = false;
        }

        // Where the highlight is painted now (window coordinates), or empty.
        W.Rect HighlightRect()
        {
            if (HighlightAlpha() <= 0.003 || hiH <= 0) return W.Rect.Empty;
            double inset = Px(Inset);
            return new W.Rect(Pad + inset, Pad + hiY, body.Width - 2 * inset, hiH);
        }

        double HighlightAlpha()
        {
            double a = hiA;
            if (flashing) a *= 1 - 0.85 * Math.Sin(Math.PI * Math.Max(0, Math.Min(1, (Anim.Now - flashAt) / FlashMs)));
            return a;
        }

        W.Rect StripRect()
        {
            foreach (Item it in items)
                if (it.Kind == KStrip) return new W.Rect(it.R.X + Pad, it.R.Y + Pad, it.R.Width, it.R.Height);
            return W.Rect.Empty;
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            Compose(dc, w, h);
        }

        void Compose(M.DrawingContext dc, int w, int h)
        {
            if (body.Width <= 0 || body.Height <= 0) return;
            if (baseLayer == null || baseLayer.PixelWidth != w || baseLayer.PixelHeight != h)
            {
                baseLayer = Ink.Render(w, h, delegate(M.DrawingContext d) { PaintBase(d, w, h); }, null);
                selLayer = Ink.Render(w, h, PaintSelected, null);
            }
            W.Rect all = new W.Rect(0, 0, w, h);
            dc.DrawImage(baseLayer, all);
            Palette pal = Ds.Brushes;
            double a = HighlightAlpha();
            W.Rect hr = HighlightRect();
            if (!hr.IsEmpty)
            {
                double rr = Px(6);
                dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, a)), null, hr, rr, rr);
                dc.PushClip(new M.RectangleGeometry(hr, rr, rr));
                dc.PushOpacity(a);
                dc.DrawImage(selLayer, all);
                dc.Pop();
                dc.Pop();
            }
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            for (int i = 0; i < ThumbCount(); i++)
            {
                if (thumbA[i] < 0.003) continue;
                W.Rect r = thumbR[i];
                r.Inflate(U(1.5), U(1.5));
                M.Pen ring = new M.Pen(Ds.Brush(Ds.WithAlpha(pal.Accent, thumbA[i])), U(2));
                dc.DrawRoundedRectangle(null, ring, r, U(6.5), U(6.5));
            }
            if (labelThumb >= 0 && labelThumb < ThumbCount() && thumbA[labelThumb] > 0.003)
            {
                M.FormattedText t = Ink.Px(When(recent[labelThumb].LastWriteTime), Ds.Regular, U(11), Ds.WithAlpha(pal.Label2, thumbA[labelThumb]));
                W.Rect last = thumbR[Recent - 1];
                dc.DrawText(t, new W.Point(Math.Round(last.Right - t.WidthIncludingTrailingWhitespace), Math.Round(last.Y - Px(19))));
            }
            dc.Pop();
        }

        // A wide soft shadow plus a tight one under the edge; blurred once per scale and theme, any size after that.
        void PaintShadow(M.DrawingContext dc)
        {
            string key = s + "|" + Ds.Dark;
            if (softShadow == null || key != shadowKey)
            {
                bool dark = Ds.Dark;
                double r = Px(Radius);
                softShadow = new ShadowTile(r, Px(24), Ds.Argb(dark ? 0.55 : 0.22, 0, 0, 0));
                tightShadow = new ShadowTile(r, Math.Max(2, Px(2.5)), Ds.Argb(dark ? 0.40 : 0.10, 0, 0, 0));
                shadowKey = key;
            }
            softShadow.Draw(dc, new W.Rect(Pad + Px(4), Pad + Px(12), body.Width - 2 * Px(4), body.Height - Px(8)));
            tightShadow.Draw(dc, new W.Rect(Pad, Pad + Px(1), body.Width, body.Height));
        }

        // Soft shadow of a rounded rectangle cut in nine from one small blurred tile: corners as they are, edges and middle
        // stretched (they are uniform along the stretch, so nearest-neighbour scaling is exact and leaves no seams).
        sealed class ShadowTile
        {
            readonly MI.BitmapSource[] parts = new MI.BitmapSource[9];
            readonly int spread, slice;

            public ShadowTile(double radius, double blur, M.Color c)
            {
                spread = (int)Math.Ceiling(blur * 1.6) + 1;
                int corner = (int)Math.Ceiling(radius + blur) + 1, core = 2 * corner + 2, size = core + 2 * spread;
                slice = spread + corner;
                MI.BitmapSource tile = Ink.Shadow(size, size, new W.Rect(spread, spread, core, core), radius, blur, c);
                int[] e = { 0, slice, size - slice, size };
                for (int i = 0; i < 9; i++)
                {
                    int cx = i % 3, cy = i / 3;
                    MI.CroppedBitmap part = new MI.CroppedBitmap(tile, new W.Int32Rect(e[cx], e[cy], e[cx + 1] - e[cx], e[cy + 1] - e[cy]));
                    part.Freeze();
                    parts[i] = part;
                }
            }

            public void Draw(M.DrawingContext dc, W.Rect r)
            {
                double[] xs = { r.X - spread, r.X - spread + slice, r.Right + spread - slice, r.Right + spread };
                double[] ys = { r.Y - spread, r.Y - spread + slice, r.Bottom + spread - slice, r.Bottom + spread };
                if (xs[2] < xs[1] || ys[2] < ys[1]) return;
                M.DrawingGroup g = new M.DrawingGroup();
                M.RenderOptions.SetBitmapScalingMode(g, M.BitmapScalingMode.NearestNeighbor);
                using (M.DrawingContext gc = g.Open())
                {
                    for (int i = 0; i < 9; i++)
                    {
                        int cx = i % 3, cy = i / 3;
                        gc.DrawImage(parts[i], new W.Rect(xs[cx], ys[cy], xs[cx + 1] - xs[cx], ys[cy + 1] - ys[cy]));
                    }
                }
                dc.DrawDrawing(g);
            }
        }

        void PaintBase(M.DrawingContext dc, int w, int h)
        {
            Palette pal = Ds.Brushes;
            PaintShadow(dc);
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            double r = Px(Radius);
            W.Rect b = new W.Rect(0, 0, body.Width, body.Height);
            if (pal.Dark)
            {
                // A crisp dark outline outside and a faint light one inside, like dark macOS menus.
                M.Pen outer = new M.Pen(Ds.Brush(Ds.Argb(0.75, 0, 0, 0)), 1);
                dc.DrawRoundedRectangle(null, outer, new W.Rect(-0.5, -0.5, b.Width + 1, b.Height + 1), r + 0.5, r + 0.5);
            }
            Ink.Round(dc, Back(pal.Dark), b, r);
            Ink.Hairline(dc, pal.Dark ? Ds.Argb(0.13, 255, 255, 255) : Ds.Argb(0.13, 0, 0, 0), b, r);
            foreach (Item it in items)
            {
                if (it.Kind == KRow) PaintRow(dc, it, false);
                else if (it.Kind == KLine) dc.DrawRectangle(Ds.Brush(pal.Separator), null, new W.Rect(Px(Inset + 10), Math.Round(it.R.Y + it.R.Height / 2), body.Width - 2 * Px(Inset + 10), 1));
                else if (it.Kind == KHead) PaintHead(dc, it);
                else PaintStrip(dc, it);
            }
            dc.Pop();
        }

        void PaintSelected(M.DrawingContext dc)
        {
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            foreach (Item it in items) if (it.Kind == KRow && it.Enabled) PaintRow(dc, it, true);
            dc.Pop();
        }

        void PaintRow(M.DrawingContext dc, Item it, bool sel)
        {
            Palette pal = Ds.Brushes;
            M.Color ink = sel ? M.Colors.White : it.Enabled ? pal.Label : pal.Label3;
            M.Color icon = sel ? M.Colors.White : !it.Enabled ? pal.Label3 : it.Alert ? pal.Red : Ds.WithAlpha(pal.Label, pal.Dark ? 0.72 : 0.68);
            double x = it.R.X + Px(10), cy = it.R.Y + it.R.Height / 2, gs = Px(16);
            Glyph.Draw(dc, it.Icon, x, Math.Round(cy - gs / 2), gs, icon, U(1.45));
            M.FormattedText t = Ink.Px(it.Text, Ds.Regular, U(13), ink);
            dc.DrawText(t, new W.Point(Math.Round(x + gs + Px(10)), Math.Round(cy - t.Height / 2)));
            if (it.Caps != null) DrawKeycaps(dc, it.Caps, it.R.Right - Px(7), cy, sel, s);
        }

        // ---- Keycaps (also drawn by the context menus, TrayMenu)

        static double CapW(string k, double s)
        {
            return Math.Max(Math.Round(18 * s), Math.Ceiling(Ink.Px(k, Ds.Medium, 10.5 * s, M.Colors.Black).WidthIncludingTrailingWhitespace + 10 * s));
        }

        internal static double KeycapsWidth(string[] caps, double s)
        {
            double w = 0, gap = Math.Round(3 * s);
            foreach (string k in caps) w += CapW(k, s) + gap;
            return caps.Length > 0 ? w - gap : 0;
        }

        internal const string ShiftKey = "\u21E7";
        static readonly M.Geometry ShiftShape = Frozen(M.Geometry.Parse("M12,4 L20,12.5 H15.5 V20 H8.5 V12.5 H4 Z"));

        static M.Geometry Frozen(M.Geometry g) { g.Freeze(); return g; }

        // "Ctrl+Shift+C" as the keys printed on the caps.
        internal static string[] KeycapsFor(string keys)
        {
            if (string.IsNullOrEmpty(keys)) return null;
            List<string> caps = new List<string>();
            foreach (string part in keys.Split('+'))
            {
                string k = part.Trim();
                if (k.Length == 0) continue;
                if (k == "Shift" || k == "May\u00FAs") k = ShiftKey;
                caps.Add(k);
            }
            return caps.Count > 0 ? caps.ToArray() : null;
        }

        // Shortcut keys as small keycaps, right-aligned at right.
        internal static void DrawKeycaps(M.DrawingContext dc, string[] caps, double right, double cy, bool sel, double s)
        {
            Palette pal = Ds.Brushes;
            double x = Math.Round(right - KeycapsWidth(caps, s)), h = Math.Round(18 * s), y = Math.Round(cy - h / 2), r = 4.5 * s;
            foreach (string k in caps)
            {
                double cw = CapW(k, s);
                W.Rect cr = new W.Rect(x, y, cw, h);
                if (sel) Ink.Round(dc, Ds.Argb(0.22, 255, 255, 255), cr, r);
                else if (pal.Dark)
                {
                    Ink.Round(dc, Ds.Argb(0.09, 255, 255, 255), cr, r);
                    Ink.Hairline(dc, Ds.Argb(0.07, 255, 255, 255), cr, r);
                }
                else
                {
                    W.Rect sh = cr;
                    sh.Offset(0, 1);
                    Ink.Round(dc, Ds.Argb(0.10, 0, 0, 0), sh, r);
                    Ink.Round(dc, M.Colors.White, cr, r);
                    Ink.Hairline(dc, Ds.Argb(0.10, 0, 0, 0), cr, r);
                }
                M.Color ink = sel ? Ds.Argb(0.96, 255, 255, 255) : pal.Label2;
                if (k == ShiftKey)
                {
                    // Drawn, not typed: the fallback font makes the arrow tiny.
                    double g = Math.Round(h * 0.66), z = g / 24;
                    M.Pen pen = new M.Pen(Ds.Brush(ink), 1.15 * s / z);
                    pen.LineJoin = M.PenLineJoin.Round;
                    dc.PushTransform(new M.MatrixTransform(z, 0, 0, z, cr.X + Math.Round((cw - g) / 2), cr.Y + Math.Round((h - g) / 2)));
                    dc.DrawGeometry(null, pen, ShiftShape);
                    dc.Pop();
                }
                else Ink.Center(dc, Ink.Px(k, Ds.Medium, 10.5 * s, ink), cr);
                x += cw + Math.Round(3 * s);
            }
        }

        static MI.BitmapSource logo;
        static int logoPx;

        // The app logo scaled once with GDI+ bicubic, so it stays crisp at small sizes.
        internal static MI.BitmapSource Logo(int px)
        {
            if (logo != null && logoPx == px) return logo;
            using (D.Image src = ShotStack.LoadResourceImage("logo.png"))
            {
                if (src == null) return null;
                using (D.Bitmap b = new D.Bitmap(px, px, DI.PixelFormat.Format32bppPArgb))
                {
                    using (D.Graphics g = D.Graphics.FromImage(b))
                    {
                        g.InterpolationMode = DD.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = DD.PixelOffsetMode.HighQuality;
                        g.SmoothingMode = DD.SmoothingMode.AntiAlias;
                        g.DrawImage(src, new D.Rectangle(0, 0, px, px));
                    }
                    logo = Ink.FromGdi(b);
                    logoPx = px;
                }
            }
            return logo;
        }

        void PaintHead(M.DrawingContext dc, Item it)
        {
            Palette pal = Ds.Brushes;
            int lp = P(34);
            double lx = Px(14), ly = Math.Round(it.R.Y + (it.R.Height - lp) / 2 + U(1));
            MI.BitmapSource lg = Logo(lp);
            if (lg != null) dc.DrawImage(lg, new W.Rect(lx, ly, lp, lp));
            M.FormattedText title = Ink.Px("Stackshot", Ds.Title, U(14.5), pal.Label);
            M.FormattedText st = Ink.Px(status, Ds.Regular, U(11.5), statusAlert ? pal.Label : pal.Label2);
            double tx = lx + lp + Px(11), block = title.Height + st.Height - U(3), top = Math.Round(it.R.Y + (it.R.Height - block) / 2 + U(1));
            dc.DrawText(title, new W.Point(Math.Round(tx), top));
            double sx = tx, sy = Math.Round(top + title.Height - U(3));
            if (statusAlert)
            {
                double d = U(3.5);
                dc.DrawEllipse(Ds.Brush(pal.Red), null, new W.Point(sx + d, sy + st.Height / 2 + U(0.5)), d, d);
                sx += Px(12);
            }
            dc.DrawText(st, new W.Point(Math.Round(sx), sy));
        }

        void PaintStrip(M.DrawingContext dc, Item it)
        {
            Palette pal = Ds.Brushes;
            M.FormattedText t = Ink.Px("Recientes", Ds.Semibold, U(11), pal.Label2);
            dc.DrawText(t, new W.Point(thumbR[0].X, Math.Round(thumbR[0].Y - Px(19))));
            double rr = Px(5);
            for (int i = 0; i < Recent && i < recent.Count; i++)
            {
                W.Rect r = thumbR[i];
                string path = recent[i].FullName;
                bool media = ShotStack.IsMediaFile(path);
                Ink.Round(dc, pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.05, 0, 0, 0), r, rr);
                MI.BitmapSource src;
                if (thumbs.TryGetValue(path, out src) && src != null && src.PixelWidth > 0 && src.PixelHeight > 0)
                {
                    double k = Math.Max(r.Width / src.PixelWidth, r.Height / src.PixelHeight);
                    double dw = src.PixelWidth * k, dh = src.PixelHeight * k;
                    dc.PushClip(new M.RectangleGeometry(r, rr, rr));
                    dc.DrawImage(src, new W.Rect(r.X + (r.Width - dw) / 2, r.Y, dw, dh));
                    dc.Pop();
                }
                else
                {
                    double g = Px(14);
                    Glyph.Draw(dc, media ? "video" : "photo", Math.Round(r.X + (r.Width - g) / 2), Math.Round(r.Y + (r.Height - g) / 2), g, pal.Label3, U(1.4));
                }
                if (media)
                {
                    double d = Px(14), g = Px(8);
                    W.Rect c = new W.Rect(r.X + Px(4), r.Bottom - Px(4) - d, d, d);
                    dc.DrawEllipse(Ds.Brush(Ds.Argb(0.55, 0, 0, 0)), null, new W.Point(c.X + d / 2, c.Y + d / 2), d / 2, d / 2);
                    Glyph.Draw(dc, "play!", c.X + (d - g) / 2 + U(0.5), c.Y + (d - g) / 2, g, M.Colors.White, 1);
                }
                Ink.Hairline(dc, pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0), r, rr);
            }
        }

        static readonly CultureInfo Es = new CultureInfo("es-ES");

        static string When(DateTime t)
        {
            DateTime d = t.Date, now = DateTime.Today;
            string hm = t.ToString("HH:mm", Es);
            if (d == now) return "Hoy, " + hm;
            if (d == now.AddDays(-1)) return "Ayer, " + hm;
            return t.ToString("d MMM", Es).TrimEnd('.') + ", " + hm;
        }

        // ---- Hover, keyboard and clicks

        void ResetHover()
        {
            hot = hotThumb = labelThumb = -1;
            hiA = tA = 0;
            for (int i = 0; i < Recent; i++) thumbA[i] = 0;
            stripDirty = true; // a ring left in the pixels from last time
        }

        // Moves the highlight to a row: it slides within a group and reappears in place across groups.
        void SetHot(int index)
        {
            if (index == hot) return;
            int old = hot;
            hot = index;
            if (index < 0) tA = 0;
            else
            {
                Item it = items[index];
                tY = it.R.Y;
                tH = it.R.Height;
                tA = 1;
                bool slide = old >= 0 && hiA > 0.2 && items[old].Group == it.Group;
                if (!slide)
                {
                    hiY = tY;
                    hiH = tH;
                    if (old >= 0) hiA = Math.Min(hiA, 0.35);
                }
            }
            Kick();
        }

        void SetHotThumb(int i)
        {
            if (i == hotThumb) return;
            hotThumb = i;
            if (i >= 0) labelThumb = i;
            Kick();
        }

        void Kick()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Anim.Wake(this);
        }

        protected override bool StepExtra(double now)
        {
            double dt = Math.Max(1, Math.Min(17, now - lastTick));
            lastTick = now;
            bool more = false, changed = false;
            double kPos = 1 - Math.Exp(-dt / 30.0), kIn = 1 - Math.Exp(-dt / 28.0), kOut = 1 - Math.Exp(-dt / 55.0);
            hiY = Approach(hiY, tY, kPos, 0.3, ref more, ref changed);
            hiH = Approach(hiH, tH, kPos, 0.3, ref more, ref changed);
            hiA = Approach(hiA, tA, tA > hiA ? kIn : kOut, 0.004, ref more, ref changed);
            bool thumbsMoved = false;
            for (int i = 0; i < Recent; i++)
                thumbA[i] = Approach(thumbA[i], i == hotThumb ? 1 : 0, i == hotThumb ? kIn : kOut, 0.004, ref more, ref thumbsMoved);
            if (thumbsMoved) { stripDirty = true; changed = true; }
            if (flashing)
            {
                changed = true;
                if (now - flashAt >= FlashMs)
                {
                    flashing = false;
                    BeginClose(restoreAfter);
                }
            }
            if (changed && Visible) Repaint();
            return more || flashing;
        }

        static double Approach(double v, double target, double k, double eps, ref bool more, ref bool changed)
        {
            if (v == target) return v;
            changed = true;
            double n = v + (target - v) * k;
            if (Math.Abs(target - n) < eps) return target;
            more = true;
            return n;
        }

        D.Point Local(D.Point p) { return new D.Point(p.X - Pad, p.Y - Pad); }

        int RowAt(D.Point p)
        {
            if (p.X < 0 || p.X >= body.Width) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.Kind == KRow && it.Enabled && p.Y >= it.R.Y && p.Y < it.R.Bottom) return i;
            }
            return -1;
        }

        int ThumbAt(D.Point p)
        {
            for (int i = 0; i < ThumbCount(); i++)
                if (thumbR[i].Contains(new W.Point(p.X, p.Y))) return i;
            return -1;
        }

        // How many recent captures are on the panel now (none when the strip had to make room).
        int ThumbCount()
        {
            return stripShown ? Math.Min(Recent, recent.Count) : 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (closing || flashing) return;
            D.Point p = Local(e.Location);
            int t = ThumbAt(p);
            SetHotThumb(t);
            SetHot(t >= 0 ? -1 : RowAt(p));
            Cursor want = t >= 0 ? Cursors.Hand : Cursors.Default;
            if (Cursor != want) Cursor = want;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (closing || flashing) return;
            SetHotThumb(-1);
            SetHot(-1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            D.Point p = Local(e.Location);
            if ((p.X < 0 || p.Y < 0 || p.X >= body.Width || p.Y >= body.Height) && !closing && !flashing) Dismissed(Control.MousePosition); // on the shadow
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (closing || flashing) return;
            D.Point p = Local(e.Location);
            int t = ThumbAt(p);
            if (t >= 0) { Choose(null, recent[t].FullName); return; }
            int r = RowAt(p);
            if (r < 0) return;
            SetHot(r);
            Choose(items[r].Id, null);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!Visible || closing || flashing) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Escape:
                    BeginClose(true);
                    return true;
                case Keys.Down:
                case Keys.Tab:
                    Arrow(1);
                    return true;
                case Keys.Up:
                case Keys.Shift | Keys.Tab:
                    Arrow(-1);
                    return true;
                case Keys.Home:
                case Keys.PageUp:
                    Arrow(0);
                    return true;
                case Keys.End:
                case Keys.PageDown:
                    Arrow(2);
                    return true;
                case Keys.Enter:
                case Keys.Space:
                    if (hot >= 0) Choose(items[hot].Id, null);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // dir: 1 next row, -1 previous (both wrap around), 0 first, 2 last.
        void Arrow(int dir)
        {
            List<int> rows = new List<int>();
            for (int i = 0; i < items.Count; i++) if (items[i].Kind == KRow && items[i].Enabled) rows.Add(i);
            if (rows.Count == 0) return;
            int at = rows.IndexOf(hot);
            int next = dir == 0 ? 0 : dir == 2 ? rows.Count - 1 : at < 0 ? (dir > 0 ? 0 : rows.Count - 1) : (at + dir + rows.Count) % rows.Count;
            SetHotThumb(-1);
            SetHot(rows[next]);
        }

        // Really closes it (the app is quitting); Alt+F4 and the like only hide it, since it is reused.
        public void Shutdown()
        {
            shuttingDown = true;
            Close();
        }
        bool shuttingDown;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // Only the app quitting or Windows ending the session really close it; a WM_CLOSE from anywhere else hides it.
            if (shuttingDown || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing ||
                e.CloseReason == CloseReason.ApplicationExitCall) return;
            e.Cancel = true;
            BeginClose(true);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            thumbs.Clear();
            baseLayer = selLayer = null;
            softShadow = tightShadow = null;
            if (dib != null) { dib.Dispose(); dib = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Ds.Changed -= OnTheme;
                watch.Dispose();
                if (dib != null) { dib.Dispose(); dib = null; }
            }
            base.Dispose(disposing);
        }
    }
}
