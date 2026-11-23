// Stackshot - The shared controls drawn for a Windows contrast theme: system colours only, borders instead of tints, no
// shadows or glows, and states told apart by shape and position as well as colour.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Media;

namespace Stackshot
{
    static class HcPaint
    {
        static Pen Line(Color c, double w)
        {
            Pen p = new Pen(Ds.Brush(c), w);
            p.Freeze();
            return p;
        }

        // A border in the text colour for shapes that only have a fill outside a contrast theme (null otherwise).
        public static Pen Border(double width)
        {
            return Ds.Brushes.High ? Line(Ds.Brushes.Hairline, width) : null;
        }

        // Room for the outline Outline draws under a contrast theme (none otherwise), so hovering never moves the content.
        public static Thickness Edge { get { return new Thickness(Ds.Brushes.High ? 1 : 0); } }

        // The pointer over a row or tile under a contrast theme: outlined in Highlight (a ButtonFace fill would not show).
        public static void Outline(System.Windows.Controls.Border b, bool on)
        {
            if (!Ds.Brushes.High) return;
            b.BorderBrush = on ? Ds.Brush(Ds.Brushes.Accent) : null;
            b.BorderThickness = new Thickness(1);
        }

        // Keyboard focus: a solid ring in the text colour just outside the control.
        public static void Focus(DrawingContext dc, Rect r, double radius)
        {
            r.Inflate(2, 2);
            dc.DrawRoundedRectangle(null, Line(Ds.Brushes.Label, 2), r, radius + 2, radius + 2);
        }

        // On: Highlight track with the knob (HighlightText) on the right. Off: an outlined track with a smaller knob in the
        // text colour on the left. Disabled: GrayText.
        public static void Switch(DrawingContext dc, Size size, double t, bool enabled, double hot, double px)
        {
            Palette pal = Ds.Brushes;
            HcColors s = pal.Sys;
            double w = size.Width, h = size.Height;
            Color ink = enabled ? s.WindowText : s.GrayText;
            bool on = t >= 0.5;
            Rect track = new Rect(px / 2, px / 2, Math.Max(0, w - px), Math.Max(0, h - px));
            if (on) dc.DrawRoundedRectangle(Ds.Brush(enabled ? s.Highlight : s.GrayText), Line(enabled ? s.Highlight : s.GrayText, px), track, track.Height / 2, track.Height / 2);
            else dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(ink, hot > 0.5 && enabled ? 2 * px : px), track, track.Height / 2, track.Height / 2);
            double d = on ? h - 6 : h - 10, x0 = on ? 3 : 5, x1 = w - 3 - (h - 6);
            double kx = x0 + (x1 - x0) * Math.Max(0, Math.Min(1, t));
            Color knob = on ? (enabled ? s.HighlightText : s.Window) : ink;
            dc.DrawEllipse(Ds.Brush(knob), null, new Point(kx + (h - 6) / 2, h / 2), d / 2, d / 2);
        }

        // Outlined track; the chosen segment filled with Highlight and its text in HighlightText; the hovered one outlined.
        public static void Segmented(DrawingContext dc, Size size, string[] options, double slide, int hover, int selected, bool enabled, double px)
        {
            Palette pal = Ds.Brushes;
            HcColors s = pal.Sys;
            double w = size.Width, h = size.Height, seg = Math.Max(1, (w - 4) / options.Length);
            Color ink = enabled ? s.WindowText : s.GrayText;
            dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(ink, px), new Rect(px / 2, px / 2, w - px, h - px), 7, 7);
            Rect pill = new Rect(2 + seg * slide, 2, seg, h - 4);
            dc.DrawRoundedRectangle(Ds.Brush(enabled ? s.Highlight : s.GrayText), null, pill, 5.5, 5.5);
            for (int i = 0; i < options.Length; i++)
            {
                bool on = i == selected;
                if (i == hover && !on && enabled) dc.DrawRoundedRectangle(null, Line(s.Highlight, px), new Rect(2 + seg * i + px / 2, 2 + px / 2, seg - px, h - 4 - px), 5.5, 5.5);
                FormattedText t = Ink.Text(options[i], on ? Ds.Semibold : Ds.Regular, 12.5, on ? (enabled ? s.HighlightText : s.Window) : ink);
                t.MaxTextWidth = Math.Max(1, seg - MacSegmented.TextPad);
                t.MaxLineCount = 1;
                t.Trimming = TextTrimming.CharacterEllipsis;
                Ink.Center(dc, t, new Rect(2 + seg * i, 0, seg, h));
            }
        }

        // Tabs of different widths (the mascot's dock): as Segmented, with a dot in the link colour on tabs with matches.
        public static void Tabs(DrawingContext dc, Size size, string[] labels, double[] xs, double[] ws, Rect pill, int selected, int hover, int[] matches, bool enabled, double px)
        {
            HcColors s = Ds.Brushes.Sys;
            Color ink = enabled ? s.WindowText : s.GrayText;
            dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(ink, px), new Rect(px / 2, px / 2, size.Width - px, size.Height - px), 9, 9);
            dc.DrawRoundedRectangle(Ds.Brush(enabled ? s.Highlight : s.GrayText), null, pill, 7, 7);
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == selected;
                if (i == hover && !on && enabled) dc.DrawRoundedRectangle(null, Line(s.Highlight, px), new Rect(xs[i] + px / 2, pill.Y + px / 2, ws[i] - px, pill.Height - px), 7, 7);
                FormattedText t = Ink.Text(labels[i], on ? Ds.Semibold : Ds.Regular, 12, on ? (enabled ? s.HighlightText : s.Window) : ink);
                Ink.Center(dc, t, new Rect(xs[i], 0, ws[i], size.Height));
                if (matches != null && matches[i] > 0)
                {
                    double dx = Math.Round(xs[i] + (ws[i] + t.WidthIncludingTrailingWhitespace) / 2) + 3.5;
                    dc.DrawEllipse(Ds.Brush(on ? s.HighlightText : s.HotTrack), null, new Point(dx, size.Height / 2 - 5), 2.5, 2.5);
                }
            }
        }

        // Buttons: the main one in Highlight, the rest in ButtonFace with a ButtonText border, plain ones as links. The
        // pointer over a button thickens its border. Returns the colour for the label and icon.
        public static Color Button(DrawingContext dc, Rect r, double radius, ButtonKind kind, bool enabled, double hot, double down, double px)
        {
            Palette pal = Ds.Brushes;
            HcColors s = pal.Sys;
            Rect inner = Ink.Inset(r, px / 2);
            double edge = (hot > 0.5 || down > 0.5) && enabled ? 2 * px : px;
            if (!enabled)
            {
                dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(s.GrayText, px), inner, radius, radius);
                return s.GrayText;
            }
            switch (kind)
            {
                case ButtonKind.Primary:
                    dc.DrawRoundedRectangle(Ds.Brush(s.Highlight), Line(edge > px ? s.WindowText : s.Highlight, edge), Ink.Inset(r, edge / 2), radius, radius);
                    return s.HighlightText;
                case ButtonKind.Plain:
                    if (edge > px) dc.DrawRoundedRectangle(null, Line(s.HotTrack, px), inner, radius, radius);
                    return s.HotTrack;
                default:
                    dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(s.ButtonText, edge), Ink.Inset(r, edge / 2), radius, radius);
                    return s.ButtonText;
            }
        }

        // An on/off icon or chip: on in Highlight, the pointer over it outlined. Returns the colour for its icon or text.
        public static Color Toggle(DrawingContext dc, Rect r, double radius, bool on, double hot, double down, bool enabled)
        {
            HcColors s = Ds.Brushes.Sys;
            if (on) dc.DrawRoundedRectangle(Ds.Brush(enabled ? s.Highlight : s.GrayText), null, r, radius, radius);
            else if ((hot > 0.5 || down > 0.5) && enabled) dc.DrawRoundedRectangle(null, Line(s.Highlight, 1), Ink.Inset(r, 0.5), radius, radius);
            return on ? (enabled ? s.HighlightText : s.Window) : enabled ? s.WindowText : s.GrayText;
        }

        // A keycap: ButtonFace with a border in the text colour.
        public static void Keycap(DrawingContext dc, Rect r, double radius, double px)
        {
            HcColors s = Ds.Brushes.Sys;
            dc.DrawRoundedRectangle(Ds.Brush(s.ButtonFace), Line(s.WindowText, px), Ink.Inset(r, px / 2), radius, radius);
        }

        // The tray's small keycaps: on the highlighted row only an outline in HighlightText.
        public static void TrayKeycap(DrawingContext dc, Rect r, double radius, bool sel)
        {
            HcColors s = Ds.Brushes.Sys;
            dc.DrawRoundedRectangle(sel ? null : Ds.Brush(s.ButtonFace), Line(sel ? s.HighlightText : s.WindowText, 1), Ink.Inset(r, 0.5), radius, radius);
        }

        // The coloured app-style icon as a solid tile in the text colour with the glyph cut out in the window colour.
        public static void IconTile(DrawingContext dc, string icon, double size, bool enabled)
        {
            HcColors s = Ds.Brushes.Sys;
            double r = size * 0.25, g = size * 0.62;
            dc.DrawRoundedRectangle(Ds.Brush(enabled ? s.WindowText : s.GrayText), null, new Rect(0, 0, size, size), r, r);
            Glyph.Draw(dc, icon, (size - g) / 2, (size - g) / 2, g, s.Window, Math.Max(1.4, size / 16));
        }
    }
}
