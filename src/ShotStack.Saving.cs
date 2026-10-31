// Stackshot - While a recording is being saved, a card in the stack says so, right where its thumbnail will appear: a
// spinner (or a progress ring once the pass reports one) and what is happening. When the file is ready it fades and the
// real thumbnail slides in in its place.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        SavingCard savingCard;

        // gif: what is being saved; label: the bar's words for the pass (null: just saving); fraction: 0-1, or -1.
        public void SavingShow(bool gif, string label, double fraction)
        {
            if (sync != null && sync.InvokeRequired) { Ui(delegate { SavingShow(gif, label, fraction); }); return; }
            if (Test || exiting) return;
            if (tucked) Untuck();
            peeked = false;
            Screen scr = AnchorScreen();
            if (savingCard == null || savingCard.IsDisposed) savingCard = new SavingCard();
            savingCard.Set(SavingSlot(scr), ScaleFor(scr), label ?? (gif ? I18n.T("Guardando el GIF\u2026", "Saving GIF\u2026") : I18n.T("Guardando el v\u00EDdeo\u2026", "Saving video\u2026")), fraction);
        }

        public void SavingDone()
        {
            if (sync != null && sync.InvokeRequired) { Ui(SavingDone); return; }
            if (savingCard != null && !savingCard.IsDisposed) savingCard.Away();
            savingCard = null;
        }

        // Where the next thumbnail goes: above the newest one shown, or at the bottom of an empty stack.
        Rectangle SavingSlot(Screen scr)
        {
            Rectangle wa = scr.WorkingArea;
            float s = ScaleFor(scr);
            int margin = (int)Math.Round(16 * s), gap = (int)Math.Round(10 * s), w = (int)Math.Round(256 * s), h = (int)Math.Round(62 * s);
            int bottom = wa.Bottom - margin;
            foreach (Card c in cards) if (!c.Parked && !c.Leaving) bottom = Math.Min(bottom, c.Home.Top - gap);
            return new Rectangle(wa.Left + margin, Math.Max(wa.Top + margin, bottom - h), w, h);
        }
    }

    // The card itself: a dark capsule like the recording bar, with a spinner or a progress ring, the words and the
    // percentage. It only animates while it shows.
    class SavingCard : FloatWindow
    {
        string label = "";
        double fraction = -1, spin;
        bool shown, leaving;
        readonly Timer tick = new Timer { Interval = 33 };
        System.Windows.Media.Imaging.BitmapSource shadow;

        public SavingCard()
        {
            tick.Tick += delegate { if (leaving || IsDisposed) { tick.Stop(); return; } spin += 0.033; Redraw(); };
        }

        protected override bool PerPixel { get { return true; } }

        public void Set(Rectangle r, float scale, string text, double frac)
        {
            if (scale != s) shadow = null;
            s = scale;
            Pad = P(22);
            SetSize(r.Size);
            label = text;
            fraction = frac;
            if (!shown)
            {
                shown = true;
                JumpTo(r.X - P(56), r.Y);
                alpha.Set(0);
                ApplyAlpha();
                ShowQuiet();
                Redraw();
                alpha.Go(1, 220, 0, Ease.OutCubic, null);
                MoveTo(r.X, r.Y, 260, 0.72, 0);
                tick.Start();
                return;
            }
            MoveTo(r.X, r.Y, 320, 0.85, 0);
            Redraw();
        }

        public void Away()
        {
            if (leaving) return;
            leaving = true;
            tick.Stop();
            alpha.Go(0, 180, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            Anim.Wake(this);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tick.Stop();
            tick.Dispose();
            base.OnFormClosed(e);
        }

        protected override void PaintSurface(System.Windows.Media.DrawingContext dc, int w, int h)
        {
            bool high = Ds.Brushes.High;
            double r = P(14);
            if (shadow == null)
                shadow = Ink.Shadow(w, h, new System.Windows.Rect(Pad, Pad + P(5), body.Width, body.Height), r, P(16), Ds.Argb(0.45, 0, 0, 0));
            if (!high) dc.DrawImage(shadow, new System.Windows.Rect(0, 0, w, h));
            dc.PushTransform(new System.Windows.Media.TranslateTransform(Pad, Pad));
            System.Windows.Rect b = new System.Windows.Rect(0, 0, body.Width, body.Height);
            Ink.Round(dc, Palette.HudBack(Ds.Argb(0.93, 30, 30, 32)), b, r);
            Ink.Hairline(dc, Palette.HudLine, b, r);
            System.Windows.Media.Color white = Palette.HudLabel, dim = Palette.HudLabel2, accent = Palette.HudFill(Ds.Rgb(10, 132, 255));

            // The ring: a track and, with a fraction, the part done; without one, an arc going round.
            double cy = body.Height / 2.0, cx = P(30), rr = P(11), th = Math.Max(2, P(2.6f));
            System.Windows.Media.Pen track = new System.Windows.Media.Pen(Ds.Brush(Ds.Argb(0.16, 255, 255, 255)), th);
            track.Freeze();
            dc.DrawEllipse(null, track, new System.Windows.Point(cx, cy), rr, rr);
            double start, sweep;
            if (fraction >= 0) { start = -90; sweep = Math.Max(4, 360 * Math.Min(1, fraction)); }
            else { start = (spin * 360 * 1.1) % 360; sweep = 100 + 60 * Math.Sin(spin * Math.PI * 1.3); }
            Arc(dc, cx, cy, rr, start, sweep, accent, th);

            double tx = P(52), tw = body.Width - tx - P(14);
            System.Windows.Media.FormattedText title = Ink.Px(label, Ds.Semibold, P(13), white);
            title.MaxTextWidth = Math.Max(1, tw);
            title.MaxLineCount = 1;
            title.Trimming = System.Windows.TextTrimming.CharacterEllipsis;
            string sub = fraction >= 0 ? I18n.Fmt("{0} %  \u00B7  aparecer\u00E1 aqu\u00ED", "{0}%  \u00B7  it will appear here", (int)Math.Round(fraction * 100))
                                       : I18n.T("Aparecer\u00E1 aqu\u00ED en un momento", "It will appear here in a moment");
            System.Windows.Media.FormattedText st = Ink.Px(sub, Ds.Regular, P(11.5f), dim);
            st.MaxTextWidth = Math.Max(1, tw);
            st.MaxLineCount = 1;
            st.Trimming = System.Windows.TextTrimming.CharacterEllipsis;
            double top = (body.Height - title.Height - st.Height - P(2)) / 2;
            dc.DrawText(title, new System.Windows.Point(tx, Math.Round(top)));
            dc.DrawText(st, new System.Windows.Point(tx, Math.Round(top + title.Height + P(2))));
            dc.Pop();
        }

        static void Arc(System.Windows.Media.DrawingContext dc, double cx, double cy, double r, double startDeg, double sweepDeg, System.Windows.Media.Color c, double th)
        {
            double a0 = startDeg * Math.PI / 180, a1 = (startDeg + sweepDeg) * Math.PI / 180;
            System.Windows.Point p0 = new System.Windows.Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
            System.Windows.Point p1 = new System.Windows.Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
            System.Windows.Media.StreamGeometry g = new System.Windows.Media.StreamGeometry();
            using (System.Windows.Media.StreamGeometryContext ctx = g.Open())
            {
                ctx.BeginFigure(p0, false, false);
                ctx.ArcTo(p1, new System.Windows.Size(r, r), 0, sweepDeg > 180, System.Windows.Media.SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            System.Windows.Media.Pen pen = new System.Windows.Media.Pen(Ds.Brush(c), th) { StartLineCap = System.Windows.Media.PenLineCap.Round, EndLineCap = System.Windows.Media.PenLineCap.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, g);
        }
    }
}
