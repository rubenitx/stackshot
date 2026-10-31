// Stackshot - Home titles in the look of the website: the key word picked in a selection marquee with handles and the
// website's gradient ("¿Qué [capturamos?]"), and a gradient stroke that marks a word ("Hoy"). In a contrast theme they
// are plain text.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // The website's gradient (violet, blue, cyan), a shade deeper on a light window so the word keeps its contrast.
        static GradientStopCollection HomeGradient(double mid)
        {
            bool dark = Ds.Brushes.Dark;
            GradientStopCollection s = new GradientStopCollection();
            s.Add(new GradientStop(dark ? Ds.Rgb(167, 139, 250) : Ds.Rgb(139, 92, 246), 0));
            s.Add(new GradientStop(dark ? Ds.Rgb(109, 139, 255) : Ds.Rgb(79, 123, 255), mid));
            s.Add(new GradientStop(dark ? Ds.Rgb(34, 195, 238) : Ds.Rgb(8, 145, 204), 1));
            s.Freeze();
            return s;
        }

        // "¿Qué capturamos?" with its last word picked. The marquee settles in after the words (delay: when they arrive)
        // and its ants march a moment.
        FrameworkElement HomeTitle(double size, double fit, double delay)
        {
            HomePick t = I18n.IsSpanish ? new HomePick("\u00BFQu\u00E9 ", "capturamos?", size, fit) : new HomePick("What to ", "capture?", size, fit);
            homeEnter.Add(delegate { t.Enter(delay); });
            return t;
        }

        // A title with the website's hand-drawn gradient stroke under it, drawn from the left on the entrance.
        FrameworkElement HomeMarked(string text, double size, double delay)
        {
            Palette pal = Ds.Brushes;
            TextBlock t = Label(text, Ds.Title, size, pal.Label);
            if (pal.High) return t;
            FormattedText f = Ink.Text(text, Ds.Title, size, pal.Label);
            Grid g = new Grid { VerticalAlignment = VerticalAlignment.Center };
            Rectangle line = new Rectangle
            {
                Height = Math.Max(2.5, Math.Round(size * 0.11, 1)), RadiusX = 9, RadiusY = 9, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(size * 0.02, f.Baseline + size * 0.16, size * 0.04, 0), IsHitTestVisible = false,
                Fill = new LinearGradientBrush(HomeGradient(0.55), new Point(0, 0.5), new Point(1, 0.5))
            };
            line.Effect = new DropShadowEffect { Color = Ds.Rgb(109, 139, 255), ShadowDepth = 0, BlurRadius = size * 0.35, Opacity = pal.Dark ? 0.45 : 0.3 };
            ScaleTransform draw = new ScaleTransform();
            TransformGroup tg = new TransformGroup();
            tg.Children.Add(draw);
            tg.Children.Add(new RotateTransform(-1.4));
            line.RenderTransform = tg;
            line.RenderTransformOrigin = new Point(0, 0.5);
            g.Children.Add(line);
            t.VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(t);
            homeEnter.Add(delegate { HomeTween(draw, ScaleTransform.ScaleXProperty, 0, 1, delay, 900, new QuinticEase { EasingMode = EasingMode.EaseOut }); });
            return g;
        }

        // Drawn in one piece so the marquee hugs the word's ink (a text box carries the font's whole line height) and the
        // lead words keep the same baseline. Under the mouse the ants march a few steps and stop by themselves.
        sealed class HomePick : FrameworkElement
        {
            static readonly DependencyProperty ShowProperty = DependencyProperty.Register("Show", typeof(double), typeof(HomePick),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
            static readonly DependencyProperty AntsProperty = DependencyProperty.Register("Ants", typeof(double), typeof(HomePick),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            const double Stroke = 1.5, Period = 6;   // dash period in stroke widths: 9 px, 54% drawn, as on the website

            readonly string spoken;
            FormattedText lead, word;
            double knob;
            Point origin, wordAt;
            Rect box;
            DoubleAnimation marching;
            bool endless;

            // fit: the widest it may be (0: any); a narrower room takes a smaller size instead of cutting the marquee.
            public HomePick(string leadText, string wordText, double size, double fit)
            {
                spoken = leadText + wordText;
                Lay(leadText, wordText, size);
                if (fit > 0 && box.Right > fit) Lay(leadText, wordText, Math.Floor(size * fit / box.Right * 10) / 10);
                SnapsToDevicePixels = true;
                MouseEnter += delegate { if (!endless) March(0, 0); };
                MouseLeave += delegate { Settle(); };
                IsVisibleChanged += delegate { if (!IsVisible) Still(); };
            }

            void Lay(string leadText, string wordText, double size)
            {
                Palette pal = Ds.Brushes;
                lead = Ink.Text(leadText, Ds.Title, size, pal.Label);
                word = Ink.Text(wordText, Ds.Title, size, pal.Label);
                knob = Math.Max(3, Math.Min(4.5, Math.Round(size * 0.1, 1)));
                double padX = Math.Round(size * 0.22), padY = Math.Round(size * 0.1, 1), reach = knob + 1.5;
                double inkBottom = word.Height + word.OverhangAfter, inkTop = inkBottom - word.Extent;
                // Room above the line for the top handles if the font leaves too little.
                double top = Math.Max(0, reach - (inkTop - padY));
                origin = new Point(0, Math.Ceiling(top));
                double lw = lead.WidthIncludingTrailingWhitespace, ww = word.WidthIncludingTrailingWhitespace;
                box = new Rect(Math.Round(lw), Math.Round(origin.Y + inkTop - padY), Math.Round(ww + 2 * padX), Math.Round(word.Extent + 2 * padY));
                wordAt = new Point(box.X + padX, origin.Y);
                if (!pal.High) word.SetForegroundBrush(new LinearGradientBrush(HomeGradient(0.45), new Point(0, 0.2), new Point(1, 0.8)));
                // The handles on the right hang past the width, so a centred title is centred on the marquee's edge.
                Width = box.Right;
                Height = Math.Ceiling(Math.Max(origin.Y + word.Height, box.Bottom + reach));
            }

            // The marquee fades in and its handles grow, then the ants march.
            public void Enter(double delay)
            {
                if (Ds.Brushes.High) return;
                HomeTween(this, ShowProperty, 0, 1, delay, 520, new CubicEase { EasingMode = EasingMode.EaseOut });
                March(delay + 380, 3);
            }

            // times 0: like a live selection, until the mouse leaves.
            void March(double delay, int times)
            {
                // Under the mouse they march even with Windows animations off: it answers the pointer, like a selection does.
                if (Ds.Brushes.High || (times > 0 && !HomeMotion())) return;
                DoubleAnimation a = new DoubleAnimation(0, -Period, TimeSpan.FromMilliseconds(900))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delay), RepeatBehavior = times > 0 ? new RepeatBehavior(times) : RepeatBehavior.Forever
                };
                a.Completed += delegate { if (marching == a) Still(); };
                marching = a;
                endless = times <= 0;
                BeginAnimation(AntsProperty, a);
            }

            // The mouse left: the ants finish the step they are on and stop (one period on is the same picture as none).
            void Settle()
            {
                if (!endless || marching == null) return;
                endless = false;
                double at = Math.Max(-Period, Math.Min(0, (double)GetValue(AntsProperty)));
                DoubleAnimation a = new DoubleAnimation(at, -Period, TimeSpan.FromMilliseconds(Math.Max(1, 900 * (Period + at) / Period)));
                a.Completed += delegate { if (marching == a) Still(); };
                marching = a;
                BeginAnimation(AntsProperty, a);
            }

            void Still()
            {
                marching = null;
                endless = false;
                BeginAnimation(AntsProperty, null);
            }

            protected override void OnRender(DrawingContext dc)
            {
                Palette pal = Ds.Brushes;
                double show = Math.Max(0, Math.Min(1, (double)GetValue(ShowProperty)));
                if (!pal.High && show > 0)
                {
                    Color sel = pal.Accent;
                    dc.PushOpacity(show);
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(sel, pal.Dark ? 0.1 : 0.07)), null, box, 2, 2);
                    Pen ants = new Pen(Ds.Brush(Ds.WithAlpha(sel, 0.9)), Stroke)
                    {
                        DashStyle = new DashStyle(new double[] { Period * 0.54, Period * 0.46 }, (double)GetValue(AntsProperty)), DashCap = PenLineCap.Flat
                    };
                    dc.DrawRoundedRectangle(null, ants, box, 2, 2);
                    double r = knob * (0.4 + 0.6 * show);
                    Brush ring = Ds.Brush(sel), fill = Brushes.White;
                    foreach (Point c in new Point[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight })
                    {
                        dc.DrawEllipse(ring, null, c, r + 1.5, r + 1.5);
                        dc.DrawEllipse(fill, null, c, r, r);
                    }
                    dc.Pop();
                }
                dc.DrawText(lead, origin);
                dc.DrawText(word, wordAt);
            }

            // Hit anywhere in its box, not only on the letters.
            protected override HitTestResult HitTestCore(PointHitTestParameters p)
            {
                return new Rect(RenderSize).Contains(p.HitPoint) ? new PointHitTestResult(this, p.HitPoint) : null;
            }

            protected override AutomationPeer OnCreateAutomationPeer() { return new Peer(this); }

            sealed class Peer : FrameworkElementAutomationPeer
            {
                public Peer(HomePick owner) : base(owner) { }
                protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Text; }
                protected override string GetNameCore() { return ((HomePick)Owner).spoken; }
                protected override string GetClassNameCore() { return "HomePick"; }
            }
        }
    }
}
