// Stackshot - Capture with a timer: the area is chosen first, then a countdown beside it leaves time to open a menu or a
// tooltip; at zero that area is captured as it is then. Esc cancels. The desktop mascot counts along.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        DelayCountdown countdown;

        void CaptureDelayed()
        {
            if (countdown != null && !countdown.IsDisposed) return;
            picking = true;
            Bitmap frozen;
            Rectangle vs;
            IntPtr window;
            RegionPicker.Mode chosen;
            Rectangle r = RegionPicker.Pick(RegionPicker.Mode.Image, false, out frozen, out vs, out window, out chosen);
            frozen.Dispose();
            if (r.IsEmpty) return;
            int secs = settings.CaptureDelay;
            DelayCountdown c = new DelayCountdown(r, secs);
            countdown = c;
            c.Second = delegate(int left) { if (PetShown && left > 0) pet.FocusSay(left + "\u2026"); };
            c.Done = delegate(bool cancelled)
            {
                if (countdown == c) countdown = null;
                if (exiting) return;
                if (cancelled) { if (PetShown) pet.FocusSay("Vale, no la hago."); return; }
                // A moment for the countdown to leave the screen (it is left out of captures anyway).
                Delay(90, delegate
                {
                    if (exiting) return;
                    // Like an area capture: the pointer if wanted, a window's rounded corners, and Repetir área.
                    CaptureStart start = BeginCapture();
                    try
                    {
                        Bitmap shot = Grabber.Grab(r);
                        start.DrawPointer(shot, r.Location);
                        string name = I18n.T("Captura", "Capture");
                        if (window != IntPtr.Zero)
                        {
                            name = Grabber.ProcessName(window) ?? name;
                            if (WindowIsRounded(window, r)) shot = Grabber.RoundCorners(shot, 8 * ScaleFor(Screen.FromRectangle(r)));
                        }
                        RememberArea(r, window);
                        SaveCapture(shot, name, start);
                    }
                    catch (Exception ex)
                    {
                        Log("Captura con temporizador: " + ex.Message);
                        Notify(I18n.T("No se pudo capturar", "Could not capture"), I18n.T("Ha fallado algo inesperado. Vuelve a intentarlo.", "An unexpected error occurred. Please try again."));
                    }
                    finally { EndCapture(); }
                });
            };
            c.Begin();
            Log("Captura con temporizador: " + secs + " s");
        }
    }

    // The countdown: a dark disc beside the area with the seconds left and a ring that empties, popping at each new
    // second. Excluded from captures like every floating window.
    class DelayCountdown : FloatWindow
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        public Action<int> Second;
        public Action<bool> Done;
        readonly double total;
        readonly Timer tick = new Timer { Interval = 33 };
        double startedAt, pop = 1;
        int shown = -1;
        bool escDown = true, ended;
        System.Windows.Media.Imaging.BitmapSource shadow;

        public DelayCountdown(Rectangle area, int seconds)
        {
            total = seconds * 1000.0;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            Pad = P(20);
            Size sz = new Size(P(76), P(76));
            Rectangle wa = scr.WorkingArea;
            // Beside the area's bottom right corner, or inside it when there is no room.
            int x = area.Right + P(12), y = area.Bottom - sz.Height;
            if (x + sz.Width > wa.Right) x = area.Right - sz.Width - P(12);
            if (y < wa.Top) y = wa.Top + P(12);
            if (y + sz.Height > wa.Bottom) y = wa.Bottom - sz.Height - P(12);
            SetSize(sz);
            JumpTo(x, y);
            tick.Tick += delegate { Advance(); };
        }

        protected override bool PerPixel { get { return true; } }

        public void Begin()
        {
            startedAt = Anim.Now;
            alpha.Set(0);
            ApplyAlpha();
            ShowQuiet();
            alpha.Go(1, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
            tick.Start();
            Advance();
        }

        void Advance()
        {
            if (ended || IsDisposed) return;
            bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (esc && !escDown) { End(true); return; }
            escDown = esc;
            double left = total - (Anim.Now - startedAt);
            if (left <= 0) { End(false); return; }
            int sec = (int)Math.Ceiling(left / 1000.0);
            if (sec != shown)
            {
                shown = sec;
                pop = 1.25;
                if (Second != null) Second(sec);
            }
            pop += (1 - pop) * 0.22;
            Redraw();
        }

        void End(bool cancelled)
        {
            if (ended) return;
            ended = true;
            tick.Stop();
            Hide();
            Close();
            if (Done != null) Done(cancelled);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tick.Stop();
            tick.Dispose();
            base.OnFormClosed(e);
        }

        protected override void PaintSurface(System.Windows.Media.DrawingContext dc, int w, int h)
        {
            double r = body.Width / 2.0;
            if (shadow == null)
                shadow = Ink.Shadow(w, h, new System.Windows.Rect(Pad, Pad + P(4), body.Width, body.Height), r, P(14), Ds.Argb(0.45, 0, 0, 0));
            if (!Ds.Brushes.High) dc.DrawImage(shadow, new System.Windows.Rect(0, 0, w, h));
            dc.PushTransform(new System.Windows.Media.TranslateTransform(Pad, Pad));
            System.Windows.Point c = new System.Windows.Point(r, r);
            dc.DrawEllipse(Ds.Brush(Palette.HudBack(Ds.Argb(0.9, 28, 28, 30))), null, c, r, r);
            double th = Math.Max(2.5, P(3.5f)), rr = r - P(7);
            System.Windows.Media.Pen track = new System.Windows.Media.Pen(Ds.Brush(Ds.Argb(0.16, 255, 255, 255)), th);
            track.Freeze();
            dc.DrawEllipse(null, track, c, rr, rr);
            double left = Math.Max(0, total - (Anim.Now - startedAt)) / total;
            if (left > 0.002)
            {
                // The part still to wait, clockwise from the top.
                double a1 = -Math.PI / 2 + left * Math.PI * 2;
                System.Windows.Point p0 = new System.Windows.Point(c.X, c.Y - rr), p1 = new System.Windows.Point(c.X + rr * Math.Cos(a1), c.Y + rr * Math.Sin(a1));
                System.Windows.Media.StreamGeometry g = new System.Windows.Media.StreamGeometry();
                using (System.Windows.Media.StreamGeometryContext ctx = g.Open())
                {
                    ctx.BeginFigure(p0, false, false);
                    ctx.ArcTo(p1, new System.Windows.Size(rr, rr), 0, left > 0.5, System.Windows.Media.SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                System.Windows.Media.Pen ring = new System.Windows.Media.Pen(Ds.Brush(Palette.HudFill(Ds.Rgb(10, 132, 255))), th) { StartLineCap = System.Windows.Media.PenLineCap.Round, EndLineCap = System.Windows.Media.PenLineCap.Round };
                ring.Freeze();
                dc.DrawGeometry(null, ring, g);
            }
            System.Windows.Media.FormattedText t = Ink.Px(Math.Max(1, shown).ToString(System.Globalization.CultureInfo.InvariantCulture), Ds.Semibold, P(28), Palette.HudLabel);
            dc.PushTransform(new System.Windows.Media.ScaleTransform(pop, pop, c.X, c.Y));
            dc.DrawText(t, new System.Windows.Point(c.X - t.Width / 2, c.Y - t.Height / 2));
            dc.Pop();
            dc.Pop();
        }
    }
}
