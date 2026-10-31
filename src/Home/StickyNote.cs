// Stackshot - A note from Notas.md pinned on the desktop as a small always-on-top card.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // Drag moves it, a tick box ticks the line in Notas.md, double-click opens the file, right-click opens a menu.
    // A per-pixel layered window like PinWindow (rounded body, hairline, soft shadow in Pad px of margin), left out of captures.
    public class StickyNote : Form
    {
        sealed class Hit
        {
            public Rectangle R;
            public int Index;
            public bool Done;
        }

        static readonly string[] ColorNamesEs = { "Amarillo", "Azul", "Verde", "Rosa" };
        static readonly string[] ColorNamesEn = { "Yellow", "Blue", "Green", "Pink" };
        static readonly M.Color[] Light = { Ds.Rgb(255, 244, 196), Ds.Rgb(219, 234, 254), Ds.Rgb(220, 245, 224), Ds.Rgb(252, 228, 236) };
        static readonly M.Color[] Dark = { Ds.Rgb(74, 66, 32), Ds.Rgb(30, 48, 74), Ds.Rgb(28, 60, 40), Ds.Rgb(72, 36, 50) };

        readonly ShotStack owner;
        readonly string key;
        readonly Screen scr;
        readonly float s;
        readonly int Pad, cw;
        readonly Action restyle;
        readonly List<Hit> hits = new List<Hit>();
        ShotStack.Note note;
        int color;
        bool topmost, locked, truncated;
        int misses;
        Size body;
        Dib dib;
        ToolStripMenuItem topItem, lockItem;
        readonly ToolStripMenuItem[] colorItems = new ToolStripMenuItem[4];

        public string Key { get { return key; } }
        public int Color { get { return color; } }
        public bool Topmost { get { return topmost; } }
        public bool Locked { get { return locked; } }
        public bool Missing { get { return misses > 0; } }
        public Point BodyLocation { get { return new Point(Left + Pad, Top + Pad); } }

        public StickyNote(ShotStack owner, ShotStack.Note n, Screen screen, Point at, int color, bool top, bool locked)
        {
            this.owner = owner;
            note = n;
            key = n.Key;
            scr = screen;
            this.color = Math.Max(0, Math.Min(3, color));
            topmost = top;
            this.locked = locked;
            s = ShotStack.ScaleFor(scr);
            Pad = P(24);
            cw = P(250);
            Text = "Stackshot";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = topmost;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            Rectangle wa = scr.WorkingArea;
            body = new Size(cw, Lay(null, MaxH()));
            int bx = Math.Max(wa.Left, Math.Min(wa.Right - body.Width, at.X));
            int by = Math.Max(wa.Top, Math.Min(wa.Bottom - body.Height, at.Y));
            Bounds = new Rectangle(bx - Pad, by - Pad, body.Width + 2 * Pad, body.Height + 2 * Pad);
            ContextMenuStrip = BuildMenu();
            restyle = delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)Redraw); };
            Ds.Changed += restyle;
        }

        int P(float v) { return (int)Math.Round(v * s); }

        int MaxH()
        {
            Rectangle wa = IsHandleCreated && !IsDisposed ? Screen.FromHandle(Handle).WorkingArea : scr.WorkingArea;
            return Math.Max(P(120), (int)(wa.Height * 0.6));
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000;  // WS_EX_TOOLWINDOW (no taskbar or Alt+Tab entry) | WS_EX_LAYERED
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (FloatWindow.ExcludeFromCapture && !ShotStack.Test) Native.SetWindowDisplayAffinity(Handle, 0x11); // WDA_EXCLUDEFROMCAPTURE
            Redraw();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = TrayMenu.Create();
            topItem = TrayMenu.Item(I18n.T("Siempre encima", "Always on top"), null, delegate
            {
                topmost = !topmost;
                TopMost = topmost;
                StickyNotes.Save();
            });
            lockItem = TrayMenu.Item(I18n.T("Bloquear posici\u00F3n", "Lock position"), null, delegate { locked = !locked; StickyNotes.Save(); });
            m.Items.Add(topItem);
            m.Items.Add(lockItem);
            m.Items.Add(TrayMenu.Separator());
            for (int i = 0; i < 4; i++)
            {
                int c = i;
                string label = I18n.T("Color " + ColorNamesEs[i].ToLowerInvariant(), ColorNamesEn[i] + " color");
                colorItems[i] = TrayMenu.Item(label, null, delegate { color = c; Redraw(); StickyNotes.Save(); });
                m.Items.Add(colorItems[i]);
            }
            m.Items.Add(TrayMenu.Separator());
            m.Items.Add(TrayMenu.Item(I18n.T("Abrir Notas.md", "Open Notas.md"), "doc", delegate { owner.OpenNotes(); }));
            m.Items.Add(TrayMenu.Separator());
            m.Items.Add(TrayMenu.Item(I18n.T("Quitar del escritorio", "Remove from desktop"), "close", delegate { BeginInvoke((Action)delegate { StickyNotes.Forget(key); }); }));
            m.Opening += delegate
            {
                topItem.Checked = topmost;
                lockItem.Checked = locked;
                for (int i = 0; i < 4; i++) colorItems[i].Checked = color == i;
            };
            return m;
        }

        // ---- Content

        // The first line of the note without its date: Key is "**when** · text".
        string MainText()
        {
            int end = note.Key.IndexOf("**", 2);
            if (!note.Key.StartsWith("**") || end < 0) return note.Text;
            return note.Key.Substring(end + 2).TrimStart(' ', '\u00B7').Trim();
        }

        M.Color Back()
        {
            Palette pal = Ds.Brushes;
            if (pal.High) return pal.Window;
            return pal.Dark ? Dark[color] : Light[color];
        }

        // "**bold**" spans: the text without the markers, and (start, length) pairs of the bold parts.
        static string Plain(string t, List<int> spans)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < t.Length)
            {
                if (t[i] == '*' && i + 1 < t.Length && t[i + 1] == '*')
                {
                    int e = t.IndexOf("**", i + 2);
                    if (e > i + 2)
                    {
                        spans.Add(sb.Length);
                        spans.Add(e - i - 2);
                        sb.Append(t, i + 2, e - i - 2);
                        i = e + 2;
                        continue;
                    }
                }
                sb.Append(t[i]);
                i++;
            }
            return sb.ToString();
        }

        // Lays the card out in body coordinates, and draws it when dc is not null. Returns the body height (at most maxH).
        int Lay(M.DrawingContext dc, int maxH)
        {
            Palette pal = Ds.Brushes;
            if (dc != null) hits.Clear();
            truncated = false;
            int pad = P(14);
            int bottom = maxH - pad - P(2);
            M.Color faint = Ds.WithAlpha(pal.Label, 0.55);

            W.Media.FormattedText when = Ink.Px(note.When, Ds.Regular, P(11), faint);
            if (dc != null)
            {
                dc.DrawText(when, new W.Point(pad, pad));
                double gs = P(12);
                Glyph.Draw(dc, "pin", cw - pad - gs, pad + (when.Height - gs) / 2, gs, faint, Math.Max(1.2, 1.4 * s));
            }
            int y = pad + (int)Math.Ceiling(when.Height) + P(8);

            y = Row(dc, y, MainText(), 0, 1, note.Done, 0, P(14), Ds.Regular, bottom);
            for (int i = 0; i < note.Body.Count && !truncated; i++)
            {
                string line = note.Body[i];
                int sp = 0;
                while (sp < line.Length && line[sp] == ' ') sp++;
                string t = line.Substring(sp);
                int lead = 0;
                bool done = false;
                if (t.StartsWith("- [ ] ")) { lead = 1; t = t.Substring(6); }
                else if (t.StartsWith("- [x] ") || t.StartsWith("- [X] ")) { lead = 1; done = true; t = t.Substring(6); }
                else if (t.StartsWith("- ") || t.StartsWith("* ")) { lead = 2; t = t.Substring(2); }
                y = Row(dc, y, t, P(12) * Math.Min(4, sp / 2), lead, done, i + 1, P(13), Ds.Regular, bottom);
            }
            return Math.Min(maxH, Math.Max(P(60), y - P(5) + pad));
        }

        // One wrapped line of text with its lead (0 none, 1 tick box, 2 bullet); returns the y below it.
        int Row(M.DrawingContext dc, int y, string text, int indent, int lead, bool done, int index, double size, M.Typeface face, int bottom)
        {
            Palette pal = Ds.Brushes;
            int pad = P(14), avail = bottom - y;
            if (avail < P(14))
            {
                truncated = true;
                if (dc != null && avail >= P(8)) dc.DrawText(Ink.Px("\u2026", face, size, Ds.WithAlpha(pal.Label, 0.55)), new W.Point(pad, y));
                return y;
            }
            int boxD = P(16);
            int lead2 = lead == 1 ? boxD + P(8) : lead == 2 ? P(14) : 0;
            double tx = pad + indent + lead2;
            double w = Math.Max(P(40), cw - pad - tx);
            List<int> spans = new List<int>();
            string plain = Plain(text, spans);
            M.Color ink = done || note.Done ? Ds.WithAlpha(pal.Label, 0.5) : pal.Label;
            M.FormattedText ft = Ink.Px(plain, face, size, ink);
            ft.MaxTextWidth = w;
            for (int i = 0; i < spans.Count; i += 2) ft.SetFontWeight(W.FontWeights.SemiBold, spans[i], spans[i + 1]);
            if (done || (note.Done && index == 0)) ft.SetTextDecorations(W.TextDecorations.Strikethrough);
            if (ft.Height > avail)
            {
                ft.MaxTextHeight = avail;
                ft.Trimming = W.TextTrimming.CharacterEllipsis;
                truncated = true;
            }
            if (dc != null)
            {
                int lineH = (int)Math.Round(size * 1.4);
                dc.DrawText(ft, new W.Point(tx, y));
                if (lead == 1)
                {
                    Rectangle r = new Rectangle(pad + indent, y + (lineH - boxD) / 2, boxD, boxD);
                    W.Point c = new W.Point(r.X + boxD / 2.0, r.Y + boxD / 2.0);
                    bool ticked = index == 0 ? note.Done : done;
                    if (ticked)
                    {
                        dc.DrawEllipse(Ds.Brush(pal.Accent), null, c, boxD / 2.0, boxD / 2.0);
                        double g = boxD * 0.7;
                        Glyph.Draw(dc, "check", c.X - g / 2, c.Y - g / 2, g, pal.OnAccent, Math.Max(1.4, 1.7 * s));
                    }
                    else
                    {
                        M.Pen ring = new M.Pen(Ds.Brush(Ds.WithAlpha(pal.Label, 0.5)), Math.Max(1.2, 1.5 * s));
                        ring.Freeze();
                        dc.DrawEllipse(null, ring, c, boxD / 2.0 - 0.75 * s, boxD / 2.0 - 0.75 * s);
                    }
                    Hit h = new Hit();
                    h.R = Rectangle.Inflate(r, P(3), P(3));
                    h.Index = index;
                    h.Done = ticked;
                    hits.Add(h);
                }
                else if (lead == 2) dc.DrawText(Ink.Px("\u2022", face, size, ink), new W.Point(pad + indent + P(3), y));
            }
            return y + (int)Math.Ceiling(ft.Height) + P(5);
        }

        // ---- Painting

        void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Native.RECT wr;
            Native.GetWindowRect(Handle, out wr);
            Redraw(wr.Left, wr.Top);
        }

        void Redraw(int x, int y)
        {
            if (!IsHandleCreated || IsDisposed) return;
            int maxH = MaxH();
            body = new Size(cw, Lay(null, maxH));
            int w = body.Width + 2 * Pad, h = body.Height + 2 * Pad;
            if (dib == null || dib.Width != w || dib.Height != h)
            {
                if (dib != null) dib.Dispose();
                dib = new Dib(w, h, true);
            }
            MI.RenderTargetBitmap rtb = Ink.Render(w, h, delegate(M.DrawingContext dc) { PaintCard(dc, w, h, maxH); }, null);
            Native.GdiFlush();
            rtb.CopyPixels(new W.Int32Rect(0, 0, w, h), dib.Bits, w * 4 * h, w * 4);
            Present(x, y, w, h);
        }

        void PaintCard(M.DrawingContext dc, int w, int h, int maxH)
        {
            Palette pal = Ds.Brushes;
            double r = P(12);
            if (!pal.High)
            {
                int blur = P(16), off = P(5);
                M.Color col = pal.Dark ? Ds.Argb(0.60, 0, 0, 0) : Ds.Argb(0.30, 0, 0, 0);
                dc.DrawImage(Ink.Shadow(w, h, new W.Rect(Pad, Pad + off, body.Width, body.Height), r, blur, col), new W.Rect(0, 0, w, h));
            }
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            W.Rect b = new W.Rect(0, 0, body.Width, body.Height);
            dc.DrawRoundedRectangle(Ds.Brush(Back()), null, b, r, r);
            Ink.Hairline(dc, pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.16, 255, 255, 255) : Ds.Argb(0.14, 0, 0, 0), b, r);
            Lay(dc, maxH);
            dc.Pop();
        }

        [StructLayout(LayoutKind.Sequential)] struct Blend { public byte Op, Flags, Alpha, Format; }

        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow")]
        static extern bool UpdateLayered(IntPtr hwnd, IntPtr hdcDst, ref Native.POINT dst, ref Native.SIZE size, IntPtr hdcSrc, ref Native.POINT src, int key, ref Blend blend, int flags);

        void Present(int x, int y, int w, int h)
        {
            Native.POINT dst; dst.X = x; dst.Y = y;
            Native.POINT from; from.X = 0; from.Y = 0;
            Native.SIZE size; size.cx = w; size.cy = h;
            Blend bf = new Blend();
            bf.Alpha = 255;
            bf.Format = 1; // AC_SRC_ALPHA
            UpdateLayered(Handle, IntPtr.Zero, ref dst, ref size, dib.Dc, ref from, 0, ref bf, 2); // ULW_ALPHA
            if (Left != x || Top != y || Width != w || Height != h) UpdateBounds();
        }

        // ---- Keeping it fresh

        public void ShowFront()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Native.SetWindowPos(Handle, (IntPtr)(topmost ? -1 : 0), 0, 0, 0, 0, 0x13); // NOMOVE | NOSIZE | NOACTIVATE
        }

        // Re-reads the note. False when it is no longer there (twice in a row: the file may just be mid-write).
        public bool Reload()
        {
            ShotStack.Note n = owner.FindNote(key);
            if (n == null) { misses++; return misses < 2; }
            misses = 0;
            note = n;
            Redraw();
            return true;
        }

        // ---- Input

        Hit HitAt(Point p)
        {
            Point q = new Point(p.X - Pad, p.Y - Pad);
            foreach (Hit h in hits) if (h.R.Contains(q)) return h;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = HitAt(e.Location) != null ? Cursors.Hand : locked ? Cursors.Default : Cursors.SizeAll;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Hit h = HitAt(e.Location);
            if (h != null) { Toggle(h); return; }
            if (e.Clicks >= 2) { owner.OpenNotes(); return; }
            if (locked) return;
            int sx = Left, sy = Top;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN on HTCAPTION: native move
            if (!IsDisposed && (Left != sx || Top != sy)) StickyNotes.Save();
        }

        void Toggle(Hit h)
        {
            ShotStack.Note n = owner.FindNote(key);
            if (n == null) { StickyNotes.Forget(key); return; }
            // A write that failed (the file held by a sync app) keeps the card; the owner already said why.
            owner.NoteLineDone(n, h.Index, !h.Done);
            n = owner.FindNote(key);
            if (n == null) { StickyNotes.Forget(key); return; }
            note = n;
            Redraw();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Ds.Changed -= restyle;
            StickyNotes.Closed(this);
            if (dib != null) { dib.Dispose(); dib = null; }
            if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
        }
    }

    // The cards open on the desktop and where they sit (notas-fijadas.txt: x, y, colour, flags and the note's key).
    public static class StickyNotes
    {
        const int Max = 8;
        static readonly List<StickyNote> open = new List<StickyNote>();
        static Timer timer;
        static DateTime lastWrite;
        static ShotStack timerOwner;

        static string FilePath { get { return Path.Combine(Settings.DataDir, "notas-fijadas.txt"); } }

        public static bool IsPinned(string key)
        {
            return Find(key) != null;
        }

        static StickyNote Find(string key)
        {
            foreach (StickyNote w in open) if (w.Key == key) return w;
            return null;
        }

        static Screen ScreenAt(Point p)
        {
            foreach (Screen sc in Screen.AllScreens) if (sc.Bounds.Contains(p)) return sc;
            return Screen.PrimaryScreen;
        }

        public static void Pin(ShotStack owner, ShotStack.Note n)
        {
            StickyNote have = Find(n.Key);
            if (have != null) { have.ShowFront(); return; }
            if (open.Count >= Max)
            {
                owner.Notify(I18n.T("Notas fijadas", "Pinned notes"), I18n.T("Ya tienes 8 notas en el escritorio: quita alguna antes de fijar otra.", "You already have 8 notes on your desktop: remove one before pinning another."));
                return;
            }
            Point m = Control.MousePosition;
            Open(owner, n, ScreenAt(m), new Point(m.X - 20, m.Y - 20), 0, true, false);
            Save();
        }

        public static void RestoreAll(ShotStack owner)
        {
            try
            {
                // Notas.md not there yet (a synced folder still mounting at sign-in): keep the pins for next time.
                if (!File.Exists(FilePath) || !File.Exists(owner.NotesPath)) return;
                bool dropped = false;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string[] p = line.Split(new char[] { '\t' }, 5);
                    int x, y, c, f;
                    if (p.Length < 5 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                        || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                        || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out c)
                        || !int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out f)) { dropped = true; continue; }
                    ShotStack.Note n = owner.FindNote(p[4]);
                    if (n == null || open.Count >= Max || Find(n.Key) != null) { dropped = true; continue; }
                    Point at = new Point(x, y);
                    Open(owner, n, ScreenAt(at), at, c, (f & 1) != 0, (f & 2) != 0);
                }
                if (dropped) Save();
            }
            catch (Exception ex) { ShotStack.Log("Notas fijadas: " + ex.Message); }
        }

        static void Open(ShotStack owner, ShotStack.Note n, Screen scr, Point at, int color, bool top, bool locked)
        {
            StickyNote w = new StickyNote(owner, n, scr, at, color, top, locked);
            open.Add(w);
            w.Show();
            if (timer == null)
            {
                timerOwner = owner;
                timer = new Timer { Interval = 5000 };
                timer.Tick += delegate { Tick(); };
            }
            if (open.Count == 1)
            {
                lastWrite = File.GetLastWriteTimeUtc(owner.NotesPath);
                timer.Start();
            }
        }

        static void Tick()
        {
            try
            {
                DateTime t = File.GetLastWriteTimeUtc(timerOwner.NotesPath);
                if (t == lastWrite) return;
                lastWrite = t;
                bool missing = false;
                foreach (StickyNote w in new List<StickyNote>(open))
                {
                    if (w.IsDisposed) continue;
                    if (w.Reload()) continue;
                    Forget(w.Key);
                    missing = true;
                }
                if (missing) return;
                foreach (StickyNote w in open) if (w.Missing) lastWrite = DateTime.MinValue; // a first miss: look again next time
            }
            catch (Exception ex) { ShotStack.Log("Notas fijadas: " + ex.Message); }
        }

        // The window is gone: out of the list, and the timer rests when none is left.
        internal static void Closed(StickyNote w)
        {
            open.Remove(w);
            if (open.Count == 0 && timer != null) timer.Stop();
        }

        public static void Forget(string key)
        {
            StickyNote w = Find(key);
            if (w != null)
            {
                open.Remove(w);
                if (!w.IsDisposed) w.Close();
                if (open.Count == 0 && timer != null) timer.Stop();
                Save();
            }
        }

        // App exit: the windows go, the file stays.
        public static void CloseAll()
        {
            if (timer != null) timer.Stop();
            foreach (StickyNote w in new List<StickyNote>(open)) if (!w.IsDisposed) w.Close();
            open.Clear();
        }

        internal static void Save()
        {
            try
            {
                StringBuilder b = new StringBuilder();
                foreach (StickyNote w in open)
                {
                    if (w.IsDisposed) continue;
                    Point p = w.BodyLocation;
                    int flags = (w.Topmost ? 1 : 0) | (w.Locked ? 2 : 0);
                    b.Append(p.X.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(p.Y.ToString(CultureInfo.InvariantCulture)).Append('\t')
                     .Append(w.Color.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(flags.ToString(CultureInfo.InvariantCulture)).Append('\t')
                     .Append(w.Key).Append('\n');
                }
                Directory.CreateDirectory(Settings.DataDir);
                File.WriteAllText(FilePath, b.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { ShotStack.Log("Notas fijadas: " + ex.Message); }
        }
    }
}
