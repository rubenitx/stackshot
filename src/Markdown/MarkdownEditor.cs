// Stackshot - Markdown editor: a plain TextBox with a colored copy of the text drawn under it (colors only, so both wrap identically).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed class MarkdownEditor : Border
    {
        // Above this the colored copy is skipped and the TextBox draws its own text.
        const int TintLimit = 150000;
        // Up to this size the copy is refreshed on every keystroke; beyond it, after a short pause.
        const int LiveLimit = 40000;

        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Courier New");
        static readonly Regex FenceRx = new Regex(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.Compiled);
        static readonly Regex HeadRx = new Regex(@"^( {0,3})(#{1,6})([ \t]+)(.*)$", RegexOptions.Compiled);
        static readonly Regex RuleRx = new Regex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$", RegexOptions.Compiled);
        static readonly Regex QuoteRx = new Regex(@"^(\s*(?:>\s?)+)(.*)$", RegexOptions.Compiled);
        static readonly Regex ListRx = new Regex(@"^(\s*)([-*+]|\d+[.)])([ \t]+)(\[[ xX]\][ \t]+)?(.*)$", RegexOptions.Compiled);
        static readonly Regex InlineRx = new Regex(@"(`[^`\n]+`)|(\*\*[^*\n]+\*\*|__[^_\n]+__)|(\*[^*\s][^*\n]*\*|(?<![\w])_[^_\s][^_\n]*_(?![\w]))|(\[[^\]\n]*\]\([^)\n]*\))|(~~[^~\n]+~~)", RegexOptions.Compiled);

        public readonly TextBox Box;
        public readonly ScrollViewer Scroll;
        readonly TextBlock tint;
        readonly Grid grid;
        readonly DispatcherTimer later = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        readonly Brush ink, head, mark, code, strong, em, link, quote, list, faint, xns;
        bool quiet;
        readonly bool xml;

        public event Action Edited;
        public event Action Scrolled;

        public MarkdownEditor(Palette pal, string text, bool xml = false)
        {
            this.xml = xml;
            bool dark = pal.Dark;
            ink = Ds.Brush(pal.Label);
            head = Ds.Brush(dark ? Ds.Rgb(120, 190, 255) : Ds.Rgb(0, 92, 190));
            mark = Ds.Brush(pal.Label3);
            code = Ds.Brush(dark ? Ds.Rgb(238, 166, 120) : Ds.Rgb(176, 78, 22));
            strong = Ds.Brush(dark ? Ds.Rgb(255, 205, 130) : Ds.Rgb(150, 84, 0));
            em = Ds.Brush(dark ? Ds.Rgb(204, 176, 255) : Ds.Rgb(112, 70, 190));
            link = Ds.Brush(pal.Accent);
            quote = Ds.Brush(pal.Label2);
            list = Ds.Brush(dark ? Ds.Rgb(130, 214, 160) : Ds.Rgb(18, 124, 66));
            faint = Ds.Brush(pal.Label3);
            xns = Ds.Brush(dark ? Ds.Rgb(100, 210, 220) : Ds.Rgb(0, 120, 140));
            Background = Ds.Brush(dark ? Ds.Argb(0.25, 0, 0, 0) : Ds.Argb(0.035, 0, 0, 0));

            Thickness pad = new Thickness(26, 20, 22, 36);
            Box = new TextBox
            {
                AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, FontFamily = Mono, FontSize = 13.5,
                Foreground = Brushes.Transparent, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = pad,
                CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.4)),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                SpellCheck = { IsEnabled = false }, VerticalAlignment = VerticalAlignment.Top
            };
            TextBlock.SetLineHeight(Box, 22);
            TextBlock.SetLineStackingStrategy(Box, LineStackingStrategy.BlockLineHeight);
            tint = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontFamily = Mono, FontSize = 13.5, Foreground = ink, Padding = pad, LineHeight = 22,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top
            };
            grid = new Grid { Background = Brushes.Transparent };
            grid.Children.Add(tint);
            grid.Children.Add(Box);
            grid.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
            {
                // The empty area under the text still puts the caret at the end.
                if (!Box.IsKeyboardFocused) Box.Focus();
                if (e.OriginalSource == grid) Box.CaretIndex = Box.Text.Length;
            };
            Scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = grid, Resources = HomeWindow.ScrollStyle() };
            Child = Scroll;

            Scroll.ScrollChanged += delegate(object o, ScrollChangedEventArgs e)
            {
                if (e.ViewportHeightChange != 0) grid.MinHeight = Scroll.ViewportHeight;
                if (e.VerticalChange != 0 && Scrolled != null) Scrolled();
            };
            Box.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e)
            {
                e.Handled = true;
                Scroll.ScrollToVerticalOffset(Scroll.VerticalOffset - e.Delta);
            };
            Box.SelectionChanged += delegate { KeepCaret(); };
            Box.TextChanged += delegate
            {
                if (quiet) return;
                Retint(false);
                if (Edited != null) Edited();
            };
            later.Tick += delegate { later.Stop(); Retint(true); };
            quiet = true;
            Box.Text = text;
            quiet = false;
            Retint(true);
        }

        public double ScrollMax { get { return Scroll.ScrollableHeight; } }
        public double Offset { get { return Scroll.VerticalOffset; } }

        public string Text { get { return MarkdownTools.Normalize(Box.Text); } }

        public void SetText(string t)
        {
            quiet = true;
            Box.Text = t;
            quiet = false;
            Retint(true);
        }

        public void Stop() { later.Stop(); }

        void KeepCaret()
        {
            try
            {
                Rect r = Box.GetRectFromCharacterIndex(Box.CaretIndex);
                if (r.IsEmpty || double.IsInfinity(r.Top) || double.IsNaN(r.Top)) return;
                double top = Scroll.VerticalOffset, view = Scroll.ViewportHeight;
                if (view <= 0) return;
                if (r.Top < top + 8) Scroll.ScrollToVerticalOffset(Math.Max(0, r.Top - 8));
                else if (r.Bottom > top + view - 8) Scroll.ScrollToVerticalOffset(r.Bottom - view + 8);
            }
            catch { }
        }

        // ---- XML tint

        static readonly Regex XmlTokRx = new Regex(@"<!--.*?-->|<!\[CDATA\[.*?\]\]>|<\?.*?\?>|<!DOCTYPE[^>]*>|</?[A-Za-z_:][\w:.\-]*(?:""[^""]*""|'[^']*'|[^>""'])*>|&[#\w]+;", RegexOptions.Compiled | RegexOptions.Singleline);
        static readonly Regex XmlTagRx = new Regex(@"(?<open></?)(?<name>[^\s/>]+)|(?<an>[^\s=/>]+)(?<eq>\s*=\s*)(?<av>""[^""]*""|'[^']*')|(?<close>/?>)", RegexOptions.Compiled);

        void TintXml(string t)
        {
            Brush ns = list, cdata = em, pi = strong;
            int at = 0;
            foreach (Match m in XmlTokRx.Matches(t))
            {
                if (m.Index > at) Add(t.Substring(at, m.Index - at), null);
                at = m.Index + m.Length;
                string v = m.Value;
                if (v.StartsWith("<!--", StringComparison.Ordinal)) Add(v, faint);
                else if (v.StartsWith("<![CDATA[", StringComparison.Ordinal)) Add(v, cdata);
                else if (v.StartsWith("<?", StringComparison.Ordinal) || v.StartsWith("<!", StringComparison.Ordinal)) Add(v, pi);
                else if (v[0] == '&') Add(v, strong);
                else
                {
                    int p = 0;
                    foreach (Match g in XmlTagRx.Matches(v))
                    {
                        if (g.Index > p) Add(v.Substring(p, g.Index - p), null);
                        p = g.Index + g.Length;
                        if (g.Groups["open"].Success) { Add(g.Groups["open"].Value, mark); Add(g.Groups["name"].Value, head); }
                        else if (g.Groups["an"].Success)
                        {
                            string an = g.Groups["an"].Value;
                            Add(an, an == "xmlns" || an.StartsWith("xmlns:", StringComparison.Ordinal) ? xns : code);
                            Add(g.Groups["eq"].Value, mark);
                            Add(g.Groups["av"].Value, ns);
                        }
                        else Add(g.Value, mark);
                    }
                    if (p < v.Length) Add(v.Substring(p), null);
                }
            }
            if (at < t.Length) Add(t.Substring(at), null);
        }

        // Puts the caret on a line and column (1-based), for jumping to an error.
        public void GoTo(int line, int col)
        {
            try
            {
                Box.Focus();
                int l = Math.Max(0, Math.Min(Box.LineCount - 1, line - 1));
                int i = Box.GetCharacterIndexFromLineIndex(l) + Math.Max(0, col - 1);
                Box.CaretIndex = Math.Min(Box.Text.Length, i);
                Box.ScrollToLine(l);
            }
            catch { }
        }

        // ---- Tint

        void Retint(bool now)
        {
            int len = Box.Text.Length;
            if (len > TintLimit)
            {
                later.Stop();
                tint.Inlines.Clear();
                Box.Foreground = ink;
                return;
            }
            if (!now && len > LiveLimit) { later.Stop(); later.Start(); return; }
            later.Stop();
            Box.Foreground = Brushes.Transparent;
            tint.Inlines.Clear();
            string t = MarkdownTools.Normalize(Box.Text);
            if (xml) { TintXml(t); return; }
            bool fence = false;
            string fm = null;
            int pos = 0;
            while (pos <= t.Length)
            {
                int nl = t.IndexOf('\n', pos);
                string l = nl < 0 ? t.Substring(pos) : t.Substring(pos, nl - pos);
                Line(l, ref fence, ref fm);
                if (nl < 0) break;
                Add("\n", null);
                pos = nl + 1;
            }
        }

        void Add(string s, Brush b)
        {
            if (s.Length == 0) return;
            System.Windows.Documents.Run r = new System.Windows.Documents.Run(s);
            if (b != null) r.Foreground = b;
            tint.Inlines.Add(r);
        }

        void Line(string l, ref bool fence, ref string fm)
        {
            Match f = FenceRx.Match(l);
            if (fence)
            {
                Add(l, code);
                if (f.Success && f.Groups[1].Value.StartsWith(fm, StringComparison.Ordinal) && l.Trim().Trim('`', '~').Length == 0) fence = false;
                return;
            }
            if (f.Success) { fence = true; fm = f.Groups[1].Value; Add(l, code); return; }
            Match m = HeadRx.Match(l);
            if (m.Success)
            {
                Add(m.Groups[1].Value, null);
                Add(m.Groups[2].Value, mark);
                Add(m.Groups[3].Value, null);
                Add(m.Groups[4].Value, head);
                return;
            }
            if (RuleRx.IsMatch(l)) { Add(l, faint); return; }
            m = QuoteRx.Match(l);
            if (m.Success) { Add(m.Groups[1].Value, link); Inline(m.Groups[2].Value, quote); return; }
            m = ListRx.Match(l);
            if (m.Success)
            {
                Add(m.Groups[1].Value, null);
                Add(m.Groups[2].Value, list);
                Add(m.Groups[3].Value, null);
                Add(m.Groups[4].Value, list);
                Inline(m.Groups[5].Value, null);
                return;
            }
            if (l.TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                string[] cells = l.Split('|');
                for (int i = 0; i < cells.Length; i++)
                {
                    if (i > 0) Add("|", mark);
                    Inline(cells[i], null);
                }
                return;
            }
            Inline(l, null);
        }

        void Inline(string s, Brush baseBrush)
        {
            int at = 0;
            foreach (Match m in InlineRx.Matches(s))
            {
                if (m.Index > at) Add(s.Substring(at, m.Index - at), baseBrush);
                Brush b = m.Groups[1].Success ? code : m.Groups[2].Success ? strong : m.Groups[3].Success ? em : m.Groups[4].Success ? link : faint;
                Add(m.Value, b);
                at = m.Index + m.Length;
            }
            if (at < s.Length) Add(s.Substring(at), baseBrush);
        }
    }
}
