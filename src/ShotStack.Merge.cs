// Stackshot - Two captures into one: a thumbnail dropped on another joins them, one under the other (side by side when
// they are taller than wide), on a plain background with a little room around. The originals stay in the stack.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        readonly System.Windows.Forms.Timer mergeWatch = new System.Windows.Forms.Timer { Interval = 50 };
        bool mergeHooked;
        Card mergeFrom, mergeOn;
        const long MergeMaxPixels = 60000000;

        // A card is being dragged: the one under the pointer says whether they can be joined.
        public void MergeWatch(Card from)
        {
            mergeFrom = from;
            mergeOn = null;
            if (!mergeHooked)
            {
                mergeHooked = true;
                mergeWatch.Tick += delegate
                {
                    try { MergeTick(); }
                    catch (Exception ex) { Log("Unir capturas: " + ex.Message); mergeWatch.Stop(); }
                };
            }
            mergeWatch.Start();
        }

        void MergeTick()
        {
            if (mergeFrom == null) { mergeWatch.Stop(); return; }
            Card on = CardAt(Control.MousePosition, mergeFrom);
            if (on == mergeOn) return;
            if (mergeOn != null) mergeOn.MergeHint(0);
            mergeOn = on;
            if (on != null) on.MergeHint(on.IsMedia || mergeFrom.IsMedia ? 2 : 1);
        }

        // The drag is over. dropped: no other app took it. Returns true when it landed on another card (joined, or told
        // that only images join).
        public bool MergeEnd(Card from, bool dropped)
        {
            mergeWatch.Stop();
            if (mergeOn != null) mergeOn.MergeHint(0);
            mergeOn = null;
            mergeFrom = null;
            if (!dropped || from.Leaving || from.IsDisposed || cards.IndexOf(from) < 0) return false;
            Card on = CardAt(Control.MousePosition, from);
            if (on == null) return false;
            if (on.IsMedia || from.IsMedia) { on.Say(I18n.T("Solo im\u00E1genes", "Images only"), false); return true; }
            // The older one goes first (top or left).
            bool fromFirst = cards.IndexOf(from) < cards.IndexOf(on);
            Merge(fromFirst ? from : on, fromFirst ? on : from);
            return true;
        }

        Card CardAt(Point p, Card except)
        {
            foreach (Card c in cards) if (c != except && !c.Leaving && !c.Parked && c.Visible && c.HitBody(p)) return c;
            return null;
        }

        void Merge(Card first, Card second)
        {
            string a = first.FilePath, b = second.FilePath;
            bool dark = Ds.Brushes.Dark;
            first.Pulse();
            second.Pulse();
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap result = null;
                string error = null;
                try
                {
                    WaitWritten(a);
                    WaitWritten(b);
                    using (Bitmap x = LoadFull(a))
                    using (Bitmap y = LoadFull(b))
                        result = Join(x, y, dark);
                }
                catch (Exception ex) { error = ex.Message; }
                Ui(delegate
                {
                    if (exiting) { if (result != null) result.Dispose(); return; }
                    if (result == null)
                    {
                        Log("Unir capturas: " + error);
                        Notify(I18n.T("No se pudieron unir", "Could not combine"), I18n.T("Las capturas son demasiado grandes o no se han podido leer. Vuelve a intentarlo.", "The captures are too large or could not be read. Please try again."));
                        return;
                    }
                    Log("Capturas unidas: " + result.Width + "x" + result.Height);
                    SaveCapture(result, I18n.T("Unidas", "Combined"));
                });
            });
        }

        static Bitmap Join(Bitmap a, Bitmap b, bool dark)
        {
            bool vertical = a.Width + b.Width >= a.Height + b.Height;
            const int pad = 24, gap = 16;
            int w = vertical ? Math.Max(a.Width, b.Width) + 2 * pad : a.Width + b.Width + gap + 2 * pad;
            int h = vertical ? a.Height + b.Height + gap + 2 * pad : Math.Max(a.Height, b.Height) + 2 * pad;
            if ((long)w * h > MergeMaxPixels) throw new InvalidOperationException("demasiado grande (" + w + "x" + h + ")");
            Bitmap r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(r))
            {
                g.Clear(dark ? Color.FromArgb(28, 28, 30) : Color.FromArgb(242, 242, 247));
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                if (vertical)
                {
                    g.DrawImage(a, pad + (w - 2 * pad - a.Width) / 2, pad, a.Width, a.Height);
                    g.DrawImage(b, pad + (w - 2 * pad - b.Width) / 2, pad + a.Height + gap, b.Width, b.Height);
                }
                else
                {
                    g.DrawImage(a, pad, pad + (h - 2 * pad - a.Height) / 2, a.Width, a.Height);
                    g.DrawImage(b, pad + a.Width + gap, pad + (h - 2 * pad - b.Height) / 2, b.Width, b.Height);
                }
            }
            return r;
        }
    }
}
