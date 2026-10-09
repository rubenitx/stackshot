// Stackshot - Draws parsed Markdown as WPF elements in the app palette (light or dark), for the window and for export.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Stackshot
{
    public sealed class MarkdownView
    {
        public const double PageWidth = 760;
        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");

        public readonly StackPanel Root = new StackPanel();
        readonly Dictionary<string, FrameworkElement> anchors = new Dictionary<string, FrameworkElement>();
        readonly Dictionary<string, int> slugs = new Dictionary<string, int>();
        readonly Palette pal = Ds.Brushes;
        readonly Brush ink, dim, faint, accent, codeBack, line;
        readonly Action<string> navigate;

        // navigate: called with the target of a link (only http, https, mailto and #anchors reach it).
        public MarkdownView(List<MdBlock> blocks, Action<string> navigate)
        {
            this.navigate = navigate;
            ink = Ds.Brush(pal.Label);
            dim = Ds.Brush(Ds.WithAlpha(pal.Label, 0.78));
            faint = Ds.Brush(pal.Label2);
            accent = Ds.Brush(pal.Accent);
            codeBack = Ds.Brush(pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0));
            line = Ds.Brush(pal.Hairline);
            Root.MaxWidth = PageWidth;
            Fill(Root, blocks, 0, 14);
        }

        public bool Scroll(string slug)
        {
            FrameworkElement e;
            if (slug.StartsWith("#", StringComparison.Ordinal)) slug = slug.Substring(1);
            if (!anchors.TryGetValue(slug.ToLowerInvariant(), out e)) return false;
            e.BringIntoView(new Rect(0, 0, 10, Math.Max(10, e.ActualHeight + 120)));
            return true;
        }

        void Fill(Panel into, List<MdBlock> blocks, int depth, double gap)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                FrameworkElement e = Block(blocks[i], depth);
                if (e == null) continue;
                bool heading = blocks[i].Kind == MdKind.Heading;
                double top = into.Children.Count == 0 ? 0 : heading ? gap + 8 : 0;
                e.Margin = new Thickness(0, top, 0, i == blocks.Count - 1 && depth > 0 ? 0 : heading ? gap * 0.55 : gap);
                into.Children.Add(e);
            }
        }

        FrameworkElement Block(MdBlock b, int depth)
        {
            switch (b.Kind)
            {
                case MdKind.Heading: return Heading(b);
                case MdKind.Paragraph: return Text(b.Text, 14.5, ink, FontWeights.Normal);
                case MdKind.Code: return Code(b);
                case MdKind.Quote: return Quote(b, depth);
                case MdKind.List: return Items(b, depth);
                case MdKind.Table: return Table(b);
                default: return new Border { Height = 1, Background = line, Margin = new Thickness(0, 6, 0, 6) };
            }
        }

        FrameworkElement Heading(MdBlock b)
        {
            double[] sizes = { 29, 23, 19, 16.5, 15, 14 };
            int lv = Math.Max(1, Math.Min(6, b.Level));
            StackPanel p = new StackPanel();
            TextBlock t = Text(b.Text, sizes[lv - 1], lv == 6 ? faint : ink, FontWeights.SemiBold);
            t.FontFamily = Ds.Display;
            t.LineHeight = Math.Round(sizes[lv - 1] * 1.3);
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            p.Children.Add(t);
            if (lv <= 2) p.Children.Add(new Border { Height = 1, Background = line, Margin = new Thickness(0, 7, 0, 0) });
            string slug = MarkdownTools.Slug(b.Text);
            int n;
            if (slugs.TryGetValue(slug, out n)) { slugs[slug] = n + 1; slug += "-" + n; } else slugs[slug] = 1;
            anchors[slug] = p;
            return p;
        }

        FrameworkElement Code(MdBlock b)
        {
            Border box = new Border { Background = codeBack, CornerRadius = new CornerRadius(8), BorderBrush = line, BorderThickness = new Thickness(1), Padding = new Thickness(14, 11, 14, 12) };
            Grid g = new Grid();
            TextBlock t = new TextBlock { Text = b.Text.Length == 0 ? " " : b.Text, FontFamily = Mono, FontSize = 12.8, Foreground = ink, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            g.Children.Add(t);
            if (!string.IsNullOrEmpty(b.Lang))
            {
                TextBlock lang = new TextBlock { Text = b.Lang, FontSize = 10.5, Foreground = faint, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, -8, 0) };
                g.Children.Add(lang);
                t.Margin = new Thickness(0, 6, 0, 0);
            }
            box.Child = g;
            return box;
        }

        FrameworkElement Quote(MdBlock b, int depth)
        {
            Border bar = new Border { BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.7)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 1, 0, 1) };
            StackPanel p = new StackPanel();
            Fill(p, b.Children, depth + 1, 8);
            bar.Child = p;
            return bar;
        }

        FrameworkElement Items(MdBlock b, int depth)
        {
            StackPanel list = new StackPanel();
            int n = b.Level > 0 ? b.Level : 1;
            for (int i = 0; i < b.Items.Count; i++, n++)
            {
                MdItem it = b.Items[i];
                Grid row = new Grid { Margin = new Thickness(0, 0, 0, i == b.Items.Count - 1 ? 0 : 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(it.Checked.HasValue ? 26 : b.Ordered ? 28 : 20) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                FrameworkElement mark;
                if (it.Checked.HasValue) mark = Box(it.Checked.Value);
                else if (b.Ordered) mark = new TextBlock { Text = n + ".", Foreground = faint, FontSize = 14.5, HorizontalAlignment = HorizontalAlignment.Left };
                else mark = Bullet(depth);
                mark.VerticalAlignment = VerticalAlignment.Top;
                row.Children.Add(mark);
                StackPanel body = new StackPanel();
                Fill(body, it.Blocks, depth + 1, 6);
                Grid.SetColumn(body, 1);
                if (it.Checked == true) body.Opacity = 0.6;
                row.Children.Add(body);
                list.Children.Add(row);
            }
            return list;
        }


        FrameworkElement Bullet(int depth)
        {
            double d = 6;
            System.Windows.Shapes.Shape s = depth % 3 == 2 ? (System.Windows.Shapes.Shape)new Rectangle { Width = d - 1, Height = d - 1 } : new Ellipse { Width = d, Height = d };
            if (depth % 3 == 0) s.Fill = faint;
            else { s.Stroke = faint; s.StrokeThickness = 1.3; }
            s.Margin = new Thickness(5, 9, 0, 0);
            s.HorizontalAlignment = HorizontalAlignment.Left;
            return s;
        }

        FrameworkElement Box(bool on)
        {
            Grid g = new Grid { Width = 17, Height = 17, Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            Border b = new Border { CornerRadius = new CornerRadius(5), Background = on ? accent : null, BorderBrush = on ? null : Ds.Brush(pal.Label3), BorderThickness = new Thickness(on ? 0 : 1.5) };
            g.Children.Add(b);
            if (on) g.Children.Add(new GlyphView("check", 17, Colors.White, 2.2));
            return g;
        }

        FrameworkElement Table(MdBlock b)
        {
            int cols = b.Head.Count;
            Grid g = new Grid();
            for (int c = 0; c < cols; c++)
            {
                int w = 4;
                w = Math.Max(w, MarkdownDoc.Plain(b.Head[c]).Length);
                foreach (List<string> r in b.Rows) w = Math.Max(w, MarkdownDoc.Plain(r[c]).Length);
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Min(w, 36) + 4, GridUnitType.Star), MinWidth = 54 });
            }
            for (int r = 0; r <= b.Rows.Count; r++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 0; r <= b.Rows.Count; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool head = r == 0;
                    TextBlock t = Text(head ? b.Head[c] : b.Rows[r - 1][c], 13.5, ink, head ? FontWeights.SemiBold : FontWeights.Normal);
                    t.TextAlignment = b.Align[c] == 1 ? TextAlignment.Center : b.Align[c] == 2 ? TextAlignment.Right : TextAlignment.Left;
                    Border cell = new Border
                    {
                        Child = t,
                        Padding = new Thickness(11, 7, 11, 8),
                        Background = head ? codeBack : null,
                        BorderBrush = line,
                        BorderThickness = new Thickness(0, 0, c == cols - 1 ? 0 : 1, r == b.Rows.Count ? 0 : 1)
                    };
                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    g.Children.Add(cell);
                }
            return new Border { Child = g, BorderBrush = line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true };
        }

        TextBlock Text(string src, double size, Brush fg, FontWeight weight)
        {
            TextBlock t = new TextBlock { FontSize = size, Foreground = fg, FontWeight = weight, TextWrapping = TextWrapping.Wrap, LineHeight = Math.Round(size * 1.52) };
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            foreach (MdSpan s in MarkdownDoc.Inline(src))
            {
                if (s.Break) { t.Inlines.Add(new LineBreak()); continue; }
                Run r = new Run(s.Text);
                if (s.Bold) r.FontWeight = FontWeights.Bold;
                if (s.Italic) r.FontStyle = FontStyles.Italic;
                if (s.Strike) r.TextDecorations = TextDecorations.Strikethrough;
                if (s.Code)
                {
                    r.FontFamily = Mono;
                    r.FontSize = size * 0.9;
                    r.Background = codeBack;
                    r.Text = "\u2009" + s.Text + "\u2009";
                }
                if (s.Href != null && IsSafe(s.Href))
                {
                    Hyperlink h = new Hyperlink(r) { Foreground = accent, TextDecorations = null, Cursor = System.Windows.Input.Cursors.Hand };
                    string href = s.Href;
                    h.Click += delegate { Open(href); };
                    h.ToolTip = href;
                    t.Inlines.Add(h);
                }
                else t.Inlines.Add(r);
            }
            return t;
        }

        public static bool IsSafe(string url)
        {
            return url.StartsWith("#", StringComparison.Ordinal) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
        }

        void Open(string href)
        {
            try
            {
                if (href.StartsWith("#", StringComparison.Ordinal)) { if (!Scroll(href) && navigate != null) navigate(href); return; }
                if (navigate != null) navigate(href);
                else Process.Start(href);
            }
            catch (Exception ex) { ShotStack.Log("Markdown enlace: " + ex.Message); }
        }

        // Blocks rise and fade in one after another (the first few only; the rest simply appear).
        public void Enter()
        {
            int n = Math.Min(Root.Children.Count, 14);
            for (int i = 0; i < Root.Children.Count; i++)
            {
                UIElement e = Root.Children[i];
                if (i >= n) { continue; }
                TranslateTransform tt = new TranslateTransform(0, 14);
                e.RenderTransform = tt;
                e.Opacity = 0;
                TimeSpan begin = TimeSpan.FromMilliseconds(40 + i * 45);
                CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                DoubleAnimation up = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(340)) { BeginTime = begin, EasingFunction = ease };
                DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { BeginTime = begin };
                UIElement el = e;
                fade.Completed += delegate { el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 1; el.RenderTransform = null; };
                tt.BeginAnimation(TranslateTransform.YProperty, up);
                e.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }

        // The document as a picture on the window color, 2x when it fits.
        public static BitmapSource Render(string text, out string error)
        {
            error = null;
            Palette pal = Ds.Brushes;
            MarkdownView v = new MarkdownView(MarkdownDoc.Parse(text), null);
            Border page = new Border { Background = Ds.Brush(pal.Dark ? Ds.Rgb(30, 30, 32) : Colors.White), Padding = new Thickness(40, 36, 40, 40), Width = PageWidth + 80, UseLayoutRounding = true };
            page.Child = v.Root;
            v.Root.Width = PageWidth;
            TextOptions.SetTextFormattingMode(page, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(page, TextRenderingMode.Grayscale);
            page.SetValue(TextElement.FontFamilyProperty, Ds.Text);
            page.Measure(new Size(page.Width, double.PositiveInfinity));
            page.Arrange(new Rect(0, 0, page.DesiredSize.Width, page.DesiredSize.Height));
            page.UpdateLayout();
            int w = (int)Math.Ceiling(page.ActualWidth), h = (int)Math.Ceiling(page.ActualHeight);
            if (h > 16000) { error = "El documento es demasiado largo para una sola imagen."; return null; }
            double scale = h * 2 <= 16000 ? 2 : 1;
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(page);
            rtb.Freeze();
            return rtb;
        }
    }
}
