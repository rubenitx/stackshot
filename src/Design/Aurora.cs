// Stackshot - The "Aurora" look of the language picker (the 01-aurora artboard): the brand gradient band behind the
// title, the gradient ring around the selected row, the language monogram tiles and the brand colours the primary
// button uses. Dark and light follow Ds; under a contrast theme every piece falls back to plain system colours (no
// gradients, no glows). Nothing here animates on its own, so reduced motion is respected.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Media;

namespace Stackshot
{
    public static class Aurora
    {
        // The brand gradient (Look.cs): violet, blue, cyan.
        public static readonly Color Brand1 = Color.FromRgb(139, 92, 246);
        public static readonly Color Brand2 = Color.FromRgb(79, 123, 255);
        public static readonly Color Brand3 = Color.FromRgb(20, 184, 230);

        // The gradient primary button starts in this indigo and ends in the accent; both ends keep 4.5:1 under white.
        public static readonly Color Primary1 = Color.FromRgb(94, 92, 230);

        // Diagonal brush (the artboard's 135 degrees): the first colour at the top left, the last at the bottom right.
        public static LinearGradientBrush Diagonal(params Color[] stops)
        {
            LinearGradientBrush b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            for (int i = 0; i < stops.Length; i++) b.GradientStops.Add(new GradientStop(stops[i], stops.Length == 1 ? 0 : (double)i / (stops.Length - 1)));
            b.Freeze();
            return b;
        }

        public static LinearGradientBrush BrandBrush(Rect r) { return Diagonal(Brand1, Brand2, Brand3); }

        // The aurora glow behind a dialog's title plus the brand hairline along the window's top edge: one element to
        // place under the content. Under a contrast theme it draws nothing.
        public sealed class Band : FrameworkElement
        {
            Action refresh;

            public Band()
            {
                IsHitTestVisible = false;
                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Top;
                Loaded += delegate { refresh = InvalidateVisual; Ds.Changed += refresh; };
                Unloaded += delegate { Ds.Changed -= refresh; refresh = null; };
            }

            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight;
                if (w < 2 || h < 4) return;
                Palette pal = Ds.Brushes;
                if (pal.High) return;   // a contrast theme: plain system colours, no glow
                LinearGradientBrush fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                fade.GradientStops.Add(new GradientStop(Ds.Argb(1, 0, 0, 0), 0));
                fade.GradientStops.Add(new GradientStop(Ds.Argb(0.75, 0, 0, 0), 0.38));
                fade.GradientStops.Add(new GradientStop(Colors.Transparent, 0.95));
                fade.Freeze();
                dc.PushOpacity(pal.Dark ? 0.18 : 0.13);
                dc.PushOpacityMask(fade);
                dc.DrawRectangle(BrandBrush(new Rect(0, 0, w, h)), null, new Rect(0, 0, w, h));
                dc.Pop();
                dc.Pop();
                // The hairline of the artboard (its ::after): violet into cyan along the very top edge.
                LinearGradientBrush line = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                line.GradientStops.Add(new GradientStop(Brand1, 0));
                line.GradientStops.Add(new GradientStop(Brand2, 0.55));
                line.GradientStops.Add(new GradientStop(Brand3, 1));
                line.Freeze();
                dc.PushOpacity(0.75);
                dc.DrawRectangle(line, null, new Rect(0, 0, w, 2 * Ink.Hair(this)));
                dc.Pop();
            }
        }

        // A row that draws the gradient ring when it is the selected one and the plain hover of the list otherwise.
        // In a contrast theme the selection is a plain system-colour border around the row.
        public sealed class Ring : System.Windows.Controls.Decorator
        {
            public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register("Selected", typeof(bool), typeof(Ring),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

            bool hot;
            Action refresh;

            public Ring()
            {
                Loaded += delegate { refresh = InvalidateVisual; Ds.Changed += refresh; };
                Unloaded += delegate { Ds.Changed -= refresh; refresh = null; };
                MouseEnter += delegate { hot = true; InvalidateVisual(); };
                MouseLeave += delegate { hot = false; InvalidateVisual(); };
            }

            public bool Selected { get { return (bool)GetValue(SelectedProperty); } set { SetValue(SelectedProperty, value); } }

            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight;
                if (w < 2 || h < 2) return;
                Palette pal = Ds.Brushes;
                Rect r = new Rect(0, 0, w, h);
                double rr = Ds.RPanel;
                if (!Selected)
                {
                    if (hot) dc.DrawRoundedRectangle(Ds.Brush(pal.ControlHover), null, r, rr, rr);
                    return;
                }
                if (pal.High)
                {
                    dc.DrawRoundedRectangle(null, new Pen(Ds.Brush(pal.Accent), 2 * Ink.Hair(this)), Ink.Inset(r, Ink.Hair(this)), rr, rr);
                    return;
                }
                dc.DrawRoundedRectangle(BrandBrush(r), null, r, rr, rr);
                double t = Math.Max(1, Ink.Hair(this) + 0.5);
                Rect inner = Ink.Inset(r, t);
                double ir = Math.Max(1, rr - t);
                LinearGradientBrush wash = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(Brand1, pal.Dark ? 0.16 : 0.10), 0));
                wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(Brand2, 0.10), 0.55));
                wash.GradientStops.Add(new GradientStop(Ds.WithAlpha(Brand3, 0.10), 1));
                wash.Freeze();
                dc.PushClip(new RectangleGeometry(inner, ir, ir));
                dc.DrawRoundedRectangle(Ds.Brush(pal.Thumb), null, inner, ir, ir);
                dc.DrawRoundedRectangle(wash, null, inner, ir, ir);
                dc.Pop();
            }
        }

        // A monogram tile: a rounded square in the language's two colours, with the light of the top (IconTile's) and
        // its letter. A contrast theme draws the button face and the text colour.
        public sealed class Monogram : FrameworkElement
        {
            readonly string text;
            readonly Color c1, c2;

            public Monogram(string text, Color c1, Color c2, double size)
            {
                this.text = text;
                this.c1 = c1;
                this.c2 = c2;
                Width = Height = size;
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext dc)
            {
                double s = ActualWidth;
                if (s < 1) return;
                double r = s * 0.25;
                Palette pal = Ds.Brushes;
                if (pal.High)
                {
                    dc.DrawRoundedRectangle(Ds.Brush(pal.Control), new Pen(Ds.Brush(pal.Hairline), Ink.Hair(this)), new Rect(0.5, 0.5, s - 1, s - 1), r, r);
                    Ink.Center(dc, Ink.Text(text, Ds.Semibold, s * 0.4, pal.Label), new Rect(0, 0, s, s));
                    return;
                }
                dc.DrawRoundedRectangle(new LinearGradientBrush(Mix(c1, Colors.White, 0.12), c2, 0) { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) }, null, new Rect(0, 0, s, s), r, r);
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 1), new Rect(0.5, 0.5, s - 1, s - 1), r - 0.5, r - 0.5);
                Ink.Center(dc, Ink.Text(text, Ds.Semibold, s * 0.4, Colors.White), new Rect(0, 0, s, s));
            }

            static Color Mix(Color a, Color b, double t)
            {
                return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
            }
        }
    }
}
