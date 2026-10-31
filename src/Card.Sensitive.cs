// Stackshot - The note a thumbnail shows about personal data: being checked, or how much was covered.
// MIT License - https://github.com/rubenitx/stackshot
using W = System.Windows;
using M = System.Windows.Media;

namespace Stackshot
{
    public partial class Card
    {
        string coverNote;

        // null hides it.
        public void CoverNote(string text)
        {
            if (IsDisposed || coverNote == text) return;
            coverNote = text;
            Redraw();
        }

        // Its capture is still being checked: nothing to drag or open yet. The card says so instead of the stack freezing.
        public bool StillChecking()
        {
            if (!CoverJob.IsChecking(FilePath)) return false;
            if (!IsDisposed) Flash(I18n.T("Revisando datos\u2026", "Checking data\u2026"), true);
            return true;
        }

        // Bottom left, like the format of a video (a covered capture is never one); hidden under the hover buttons.
        void PaintCoverNote(M.DrawingContext dc)
        {
            if (coverNote == null || IsMedia) return;
            M.FormattedText t = Ink.Px(coverNote, Ds.Semibold, P(10.5f), Palette.HudLabel);
            double gs = P(11), lh = P(18), lw = P(8) + gs + P(4) + t.WidthIncludingTrailingWhitespace + P(8);
            W.Rect lr = new W.Rect(P(8), body.Height - P(8) - lh, lw, lh);
            Ink.Round(dc, Palette.HudBack(Ds.Argb(0.62, 0, 0, 0)), lr, lh / 2);
            Glyph.Draw(dc, "shield", lr.X + P(8), lr.Y + (lh - gs) / 2, gs, Palette.HudLabel, P(1.4f));
            dc.DrawText(t, new W.Point(System.Math.Round(lr.X + P(8) + gs + P(4)), System.Math.Round(lr.Y + (lh - t.Height) / 2)));
        }
    }
}
