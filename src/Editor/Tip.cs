// Stackshot - macOS-style tooltip for the editor chrome: a small dark rounded label with the shortcut in a keycap.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // One shared, click-through window. The first tip waits ColdDelay; moving to the next item while one is showing (or
    // just after) swaps it at once, as on macOS.
    public sealed class EdTip : FloatWindow
    {
        public const int ColdDelay = 550;
        const double WarmMs = 650;

        static EdTip one;
        static Timer delay;
        static double hiddenAt = -1e9;
        static Control owner;
        static Rectangle anchor;
        static string pending;
        static bool up;

        string name = "", key;
        bool wanted;
        M.FormattedText nameText, keyText;
        MI.BitmapSource shadow;
        Size shadowFor;

        EdTip() { }

        protected override bool PerPixel { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: never takes the mouse
                return cp;
            }
        }

        // Tip for the item at r (client coordinates of c). "Name  (Key)" shows the key in a keycap.
        public static void Schedule(Control c, Rectangle r, string tip) { Schedule(c, r, tip, false); }

        // Above: for controls near the bottom of the window.
        public static void Schedule(Control c, Rectangle r, string tip, bool above)
        {
            bool warm = (one != null && one.wanted) || Anim.Now - hiddenAt < WarmMs;
            Cancel();
            if (tip == null || c == null || c.IsDisposed || !c.IsHandleCreated) return;
            owner = c;
            anchor = r;
            pending = tip;
            up = above;
            if (delay == null)
            {
                delay = new Timer();
                delay.Tick += delegate { delay.Stop(); ShowPending(); };
            }
            delay.Interval = warm ? 1 : ColdDelay;
            delay.Start();
        }

        // Hides the tip (and forgets a pending one).
        public static void Cancel()
        {
            if (delay != null) delay.Stop();
            pending = null;
            if (one == null || one.IsDisposed || !one.wanted) return;
            one.wanted = false;
            hiddenAt = Anim.Now;
            EdTip t = one;
            t.alpha.Go(0, 90, 0, Ease.OutCubic, delegate { if (!t.wanted && !t.IsDisposed) t.Hide(); });
            Anim.Wake(t);
        }

        static void ShowPending()
        {
            Control c = owner;
            string tip = pending;
            pending = null;
            if (tip == null || c == null || c.IsDisposed || !c.IsHandleCreated || !c.Visible) return;
            if (one == null || one.IsDisposed) one = new EdTip();
            one.Open(c, anchor, tip);
        }

        // Splits "Copiar al portapapeles  (Ctrl+C)" into the name and the key.
        public static void Split(string tip, out string name, out string key)
        {
            name = tip;
            key = null;
            int i = tip.LastIndexOf("  (", StringComparison.Ordinal);
            if (i > 0 && tip.EndsWith(")") && tip.Length - i - 4 <= 14)
            {
                name = tip.Substring(0, i);
                key = tip.Substring(i + 3, tip.Length - i - 4);
            }
        }

        void Measure(string tip)
        {
            Split(tip, out name, out key);
            nameText = Ink.Px(name, Ds.Regular, P(12), Palette.HudLabel);
            nameText.MaxTextWidth = P(300);
            keyText = key != null ? Ink.Px(key, Ds.Medium, P(11), Palette.HudLabel2) : null;
        }

        Size BodySize()
        {
            double w = P(9) + nameText.WidthIncludingTrailingWhitespace + P(9);
            if (keyText != null) w += P(6) + keyText.WidthIncludingTrailingWhitespace + P(10);
            double h = Math.Max(P(24), nameText.Height + P(10));
            return new Size((int)Math.Ceiling(w), (int)Math.Ceiling(h));
        }

        void Open(Control c, Rectangle r, string tip)
        {
            Screen scr = Screen.FromControl(c);
            s = ShotStack.ScaleFor(scr);
            Pad = P(12);
            Measure(tip);
            Size b = BodySize();
            SetSize(b);
            shadow = null;
            Rectangle sr = c.RectangleToScreen(r);
            Rectangle wa = scr.WorkingArea;
            int x = sr.X + (sr.Width - b.Width) / 2, y = sr.Bottom + P(6);
            if (up || y + b.Height > wa.Bottom - P(4)) y = sr.Y - P(6) - b.Height;
            x = Math.Max(wa.Left + P(4), Math.Min(wa.Right - P(4) - b.Width, x));
            bool fresh = !wanted || !Visible;
            wanted = true;
            JumpTo(x, y);
            if (fresh && !Visible) { alpha.Set(0); ApplyAlpha(); }
            Redraw();
            ShowQuiet();
            alpha.Go(1, 110, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            W.Rect b = new W.Rect(Pad, Pad, body.Width, body.Height);
            double R = P(6);
            if (shadow == null || shadowFor != new Size(w, h))
            {
                W.Rect sb = b;
                sb.Offset(0, P(2));
                shadow = Ink.Shadow(w, h, sb, R, P(7), Ds.Argb(0.32, 0, 0, 0));
                shadowFor = new Size(w, h);
            }
            dc.DrawImage(shadow, new W.Rect(0, 0, w, h));
            Ink.Round(dc, Ds.Argb(0.94, 30, 30, 32), b, R);
            Ink.Hairline(dc, Palette.HudLine, b, R);
            double x = b.X + P(9);
            dc.DrawText(nameText, new W.Point(Math.Round(x), Math.Round(b.Y + (b.Height - nameText.Height) / 2)));
            if (keyText == null) return;
            double kw = keyText.WidthIncludingTrailingWhitespace + P(10), kh = P(16);
            W.Rect k = new W.Rect(Math.Round(x + nameText.WidthIncludingTrailingWhitespace + P(6)), Math.Round(b.Y + (b.Height - kh) / 2), Math.Ceiling(kw), kh);
            Ink.Round(dc, Palette.HudHover, k, P(4));
            Ink.Center(dc, keyText, k);
        }

        // For offscreen checks: the tip as it would be drawn, without showing a window.
        public static Bitmap Preview(string tip, float scale)
        {
            EdTip t = new EdTip();
            try
            {
                t.s = scale;
                t.Pad = t.P(12);
                t.Measure(tip);
                Size b = t.BodySize();
                t.body = b;
                int w = b.Width + 2 * t.Pad, h = b.Height + 2 * t.Pad;
                return Ink.ToGdi(Ink.Render(w, h, delegate(M.DrawingContext dc) { t.PaintSurface(dc, w, h); }, null));
            }
            finally { t.Dispose(); }
        }
    }
}
