// Stackshot - Markdown and XML editor: a TextBox that scrolls itself (WPF lays out only what is on screen) with its text
// drawn in colour by a layer underneath, line by line from the TextBox's own layout, so colours and caret never drift.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed partial class MarkdownEditor : Border
    {
        // Cascadia shapes text several times slower than Consolas, and wrapping costs more than the rest of the layout:
        // measured on 5 MB of XML, the first layout took 17 s with Cascadia and wrapping, 7 s with Consolas and wrapping
        // and 1 s with Consolas on unwrapped lines. Bigger documents trade the nicer font, then wrapping, for speed;
        // except text with very long lines (minified XML), which only wrapping keeps usable: an unwrapped 600 KB line
        // took minutes, the same line wrapped under half a second.
        public const int WrapLimit = 512 * 1024, NiceFontLimit = 128 * 1024, LongLine = 2000;
        static readonly FontFamily Nice = new FontFamily("Cascadia Mono, Consolas, Courier New"), Fast = new FontFamily("Consolas, Courier New");
        public const double FontPx = 13.5, LineHeight = 22;
        static readonly Thickness Pad = new Thickness(26, 20, 22, 36);

        public readonly TextBox Box;
        readonly Tint layer;
        readonly Syntax syntax;
        readonly bool xml;
        bool wrap;
        readonly Typeface face;
        readonly LineIndex lines = new LineIndex();
        Brush ink, hitBrush, nowBrush, errBrush;
        readonly Brush[] brushes = new Brush[(int)Tok.Count];
        string cur;
        List<Hit> hits;
        int now = -1, errorAt = -1;
        bool repaint, quiet;
        ScrollViewer host;

        public event Action Edited;
        public event Action Scrolled;
        public event Action CaretMoved;
        public event Action<MouseButtonEventArgs, List<MenuEntry>> Menu;
        public event Action<string> Copied;     // copied or cut from its menu: what to tell the user

        public MarkdownEditor(Palette pal, string text, bool xml)
        {
            this.xml = xml;
            int size = text == null ? 0 : text.Length;
            wrap = size <= WrapLimit || Longest(text) > LongLine;
            FontFamily mono = size <= NiceFontLimit ? Nice : Fast;
            face = new Typeface(mono, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            syntax = Syntax.For(xml);
            Box = new TextBox
            {
                AcceptsReturn = true, AcceptsTab = true, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, FontFamily = mono, FontSize = FontPx,
                Foreground = Brushes.Transparent, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = Pad,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
                SpellCheck = { IsEnabled = false }, IsInactiveSelectionHighlightEnabled = true, Resources = HomeWindow.ScrollStyle(),
                FocusVisualStyle = null
            };
            TextBlock.SetLineHeight(Box, LineHeight);
            TextBlock.SetLineStackingStrategy(Box, LineStackingStrategy.BlockLineHeight);
            layer = new Tint(this) { IsHitTestVisible = false };
            Grid g = new Grid { Background = Brushes.Transparent };
            g.Children.Add(layer);
            g.Children.Add(Box);
            Child = g;
            SetPalette(pal);

            Box.TextChanged += OnText;
            Box.SelectionChanged += delegate { if (CaretMoved != null) CaretMoved(); };
            Box.SizeChanged += delegate { repaint = true; };
            // Painted after layout, when the TextBox's lines are up to date.
            Box.LayoutUpdated += delegate { if (!repaint) return; repaint = false; layer.InvalidateVisual(); };
            Box.Loaded += delegate { Hook(); };
            Box.PreviewKeyDown += OnKey;
            Box.ContextMenuOpening += delegate(object o, ContextMenuEventArgs e) { e.Handled = true; };
            Box.PreviewMouseRightButtonUp += OnRightUp;
            DataObject.AddPastingHandler(Box, OnPaste);

            quiet = true;
            Box.Text = text ?? "";
            quiet = false;
            Box.UndoLimit = 400;
        }

        static int Longest(string t)
        {
            int best = 0, at = 0;
            while (at <= t.Length)
            {
                int nl = t.IndexOf('\n', at);
                int end = nl < 0 ? t.Length : nl;
                best = Math.Max(best, end - at);
                if (nl < 0) break;
                at = nl + 1;
            }
            return best;
        }

        public bool Wraps { get { return wrap; } }

        // A very long line is about to arrive (a paste, Compactar): unwrapped it would take WPF minutes to lay out.
        void EnsureWrap()
        {
            if (wrap) return;
            wrap = true;
            Box.TextWrapping = TextWrapping.Wrap;
            Box.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            y0 = double.NaN;
            repaint = true;
        }

        public void SetPalette(Palette pal)
        {
            bool dark = pal.Dark;
            ink = Ds.Brush(pal.Label);
            Color head = dark ? Ds.Rgb(120, 190, 255) : Ds.Rgb(0, 92, 190), code = dark ? Ds.Rgb(238, 166, 120) : Ds.Rgb(176, 78, 22),
                  strong = dark ? Ds.Rgb(255, 205, 130) : Ds.Rgb(150, 84, 0), em = dark ? Ds.Rgb(204, 176, 255) : Ds.Rgb(112, 70, 190),
                  green = dark ? Ds.Rgb(130, 214, 160) : Ds.Rgb(18, 124, 66), ns = dark ? Ds.Rgb(100, 210, 220) : Ds.Rgb(0, 120, 140),
                  pi = dark ? Ds.Rgb(240, 140, 190) : Ds.Rgb(180, 40, 110);
            brushes[(int)Tok.Plain] = ink;
            brushes[(int)Tok.Head] = Ds.Brush(head);
            brushes[(int)Tok.Mark] = Ds.Brush(pal.Label3);
            brushes[(int)Tok.Code] = Ds.Brush(code);
            brushes[(int)Tok.Strong] = Ds.Brush(strong);
            brushes[(int)Tok.Em] = Ds.Brush(em);
            brushes[(int)Tok.Link] = Ds.Brush(pal.Link);
            brushes[(int)Tok.Quote] = Ds.Brush(pal.Label2);
            brushes[(int)Tok.List] = Ds.Brush(green);
            brushes[(int)Tok.Faint] = Ds.Brush(pal.Label3);
            brushes[(int)Tok.Name] = Ds.Brush(head);
            brushes[(int)Tok.Attr] = Ds.Brush(code);
            brushes[(int)Tok.Value] = Ds.Brush(green);
            brushes[(int)Tok.Ns] = Ds.Brush(ns);
            brushes[(int)Tok.Comment] = Ds.Brush(pal.Label3);
            brushes[(int)Tok.CData] = Ds.Brush(em);
            brushes[(int)Tok.Pi] = Ds.Brush(pi);
            brushes[(int)Tok.Entity] = Ds.Brush(strong);
            hitBrush = Ds.Brush(dark ? Ds.Argb(0.34, 255, 214, 10) : Ds.Argb(0.45, 255, 214, 10));
            nowBrush = Ds.Brush(dark ? Ds.Argb(0.70, 255, 159, 10) : Ds.Argb(0.62, 255, 149, 0));
            errBrush = Ds.Brush(pal.Red);
            Background = Ds.Brush(dark ? Ds.Argb(0.25, 0, 0, 0) : Ds.Argb(0.035, 0, 0, 0));
            Box.CaretBrush = Ds.Brush(pal.Accent);
            Box.SelectionBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.4));
            if (pal.High) HighContrastTokens(pal);
            layer.InvalidateVisual();
        }

        void Hook()
        {
            if (host != null) return;
            repaint = true;
            Box.ApplyTemplate();
            host = Box.Template.FindName("PART_ContentHost", Box) as ScrollViewer;
            if (host == null) return;
            host.ScrollChanged += delegate(object o, ScrollChangedEventArgs e)
            {
                layer.InvalidateVisual();
                if (e.VerticalChange != 0 && Scrolled != null) Scrolled();
            };
        }

        public bool IsXml { get { return xml; } }
        public double ScrollMax { get { return host != null ? host.ScrollableHeight : 0; } }
        public double Offset { get { return host != null ? host.VerticalOffset : 0; } }

        // The text (line breaks as typed: \n, as the window keeps them). Read once per change.
        public string Text
        {
            get
            {
                if (cur == null) cur = Box.Text;
                return cur;
            }
        }

        void OnText(object sender, TextChangedEventArgs e)
        {
            int off = int.MaxValue;
            foreach (TextChange c in e.Changes) off = Math.Min(off, c.Offset);
            cur = null;
            syntax.Changed(Text, off == int.MaxValue ? 0 : off);
            lines.Changed(off == int.MaxValue ? 0 : off);
            hits = null;
            now = -1;
            repaint = true;
            if (!quiet && Edited != null) Edited();
        }

        // ---- Editing

        // One undo step; the selection the edit asks for afterwards.
        public void Apply(TextEdit ed)
        {
            if (ed.None) return;
            if (!wrap && ed.Insert.Length > LongLine && Longest(ed.Insert) > LongLine) EnsureWrap();
            Box.BeginChange();
            try
            {
                Box.Select(ed.Start, ed.Remove);
                Box.SelectedText = ed.Insert;
            }
            finally { Box.EndChange(); }
            int len = Text.Length;
            int s = Math.Max(0, Math.Min(len, ed.SelStart));
            Box.Select(s, Math.Max(0, Math.Min(len - s, ed.SelLength)));
            Reveal(s);
        }

        // Replaces the whole text as one undoable edit that touches only what differs; the caret keeps its place.
        public void SetText(string t)
        {
            string old = Text;
            if (t == old) return;
            int max = Math.Min(old.Length, t.Length), p = 0;
            while (p < max && old[p] == t[p]) p++;
            int s = 0;
            while (s < max - p && old[old.Length - 1 - s] == t[t.Length - 1 - s]) s++;
            int caret = Box.CaretIndex;
            int mapped = caret <= p ? caret : caret >= old.Length - s ? caret + (t.Length - old.Length) : p + (t.Length - p - s);
            Apply(TextEdit.Make(p, old.Length - p - s, t.Substring(p, t.Length - p - s), Math.Max(0, Math.Min(t.Length, mapped)), 0));
        }

        public void Stop() { }

        // Puts the caret on a line and column (1-based) of the text, as an XML error reports them.
        public void GoTo(int line, int col)
        {
            try
            {
                int i = EditCommands.Offset(Text, line, col);
                Box.Focus();
                Box.Select(i, 0);
                Reveal(i);
            }
            catch (Exception ex) { ShotStack.Log("Editor ir a: " + ex.Message); }
        }

        public void Select(int start, int length)
        {
            int len = Text.Length;
            start = Math.Max(0, Math.Min(len, start));
            Box.Select(start, Math.Max(0, Math.Min(len - start, length)));
            Reveal(start);
        }

        // Scrolls a character into view when it is off screen, leaving a little room above it. (The TextBox's own line
        // methods assume the font's natural line height, not LineHeight, so positions come from character rectangles.)
        public void Reveal(int index)
        {
            try
            {
                if (host == null) { Box.UpdateLayout(); Hook(); }
                if (host == null) return;
                Rect r = Box.GetRectFromCharacterIndex(Math.Max(0, Math.Min(Text.Length, index)), false);
                if (r.IsEmpty || double.IsNaN(r.Top)) return;
                Point o = host.TranslatePoint(new Point(0, 0), Box);
                double view = host.ViewportHeight;
                if (r.Top < o.Y + 4 || r.Bottom > o.Y + view - 4) host.ScrollToVerticalOffset(Math.Max(0, host.VerticalOffset + r.Top - o.Y - view / 3));
                if (!wrap && (r.Left < o.X + Pad.Left || r.Left > o.X + host.ViewportWidth - 24))
                    host.ScrollToHorizontalOffset(Math.Max(0, host.HorizontalOffset + r.Left - o.X - host.ViewportWidth / 2));
            }
            catch (Exception ex) { ShotStack.Log("Editor mostrar: " + ex.Message); }
        }

        // Where line 0 sits in the content (glyph top and left), measured once: every visual line is LineHeight below the
        // previous one, so a wrapped line's characters are found by hit-testing its middle.
        double y0 = double.NaN, x0, glyphH;
        bool stale;
        int retries;

        bool Measure()
        {
            if (!double.IsNaN(y0)) return true;
            Rect r = Box.GetRectFromCharacterIndex(0, false);
            if (r.IsEmpty || double.IsNaN(r.Top)) return false;
            y0 = r.Top + host.VerticalOffset;
            x0 = r.Left + host.HorizontalOffset;
            glyphH = r.Height;
            return true;
        }

        int LineStartAt(int k, double off)
        {
            if (!wrap) return lines.Start(Text, k);
            return Box.GetCharacterIndexFromPoint(new Point(x0 + 0.5, y0 + k * LineHeight + glyphH / 2 - off), true);
        }

        // The visual lines on screen: their first character, length (without the line break) and index.
        public List<int[]> VisibleLines()
        {
            List<int[]> o = new List<int[]>();
            Hook();
            stale = true;
            if (host == null || !Box.IsLoaded || !Measure()) return o;
            string t = Text;
            double off = host.VerticalOffset, view = host.ViewportHeight;
            int lc = wrap ? Box.LineCount : 0;
            // Mid-layout (a resize) the TextBox has no lines to give yet: the layer paints again once it has.
            if (wrap && lc < 0) return o;
            int count = wrap ? Math.Max(1, lc) : int.MaxValue;
            int first = Math.Max(0, (int)Math.Floor((off - y0) / LineHeight)), last = (int)Math.Min(count - 1L, (long)Math.Ceiling((off + view - y0) / LineHeight));
            int a = LineStartAt(first, off);
            if (a < 0 && t.Length > 0) return o;
            stale = false;
            for (int k = first; k <= last && a >= 0 && a <= t.Length; k++)
            {
                int next = k + 1 < count ? LineStartAt(k + 1, off) : t.Length;
                if (next <= a) next = k + 1 < count ? EditCommands.LineEnd(t, a) + 1 : t.Length;
                int n = Math.Min(next, t.Length) - a;
                while (n > 0 && (t[a + n - 1] == '\n' || t[a + n - 1] == '\r')) n--;
                o.Add(new int[] { a, Math.Max(0, n), k });
                if (next >= t.Length) break;
                a = next;
            }
            return o;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            ModifierKeys mods = Keyboard.Modifiers;
            if (e.Key == Key.Enter && (mods == ModifierKeys.None || mods == ModifierKeys.Shift))
            {
                e.Handled = true;
                int s = Box.SelectionStart, l = Box.SelectionLength;
                TextEdit ed = mods == ModifierKeys.Shift ? new TextEdit() : xml ? EditCommands.EnterIndent(Text, s, l) : EditCommands.Enter(Text, s, l);
                // Line breaks are typed as \n, like the rest of the text.
                if (ed.None) ed = TextEdit.Make(s, l, "\n", s + 1, 0);
                Apply(ed);
            }
            else if (e.Key == Key.Tab && !xml && (mods == ModifierKeys.None || mods == ModifierKeys.Shift))
            {
                TextEdit ed = EditCommands.Indent(Text, Box.SelectionStart, Box.SelectionLength, mods == ModifierKeys.Shift);
                if (ed.None) return;
                e.Handled = true;
                Apply(ed);
            }
            else if (PasteKey(e)) e.Handled = true;
        }

        void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)) return;
                string t = e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string;
                if (t != null && !wrap && t.Length > LongLine && Longest(t) > LongLine) EnsureWrap();
                if (t == null || t.IndexOf('\r') < 0) return;
                DataObject d = new DataObject();
                d.SetData(DataFormats.UnicodeText, MarkdownTools.Normalize(t));
                e.DataObject = d;
            }
            catch (Exception ex) { ShotStack.Log("Editor pegar: " + ex.Message); }
        }

        void OnRightUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Menu == null) return;
            Box.Focus();
            List<MenuEntry> m = new List<MenuEntry>();
            bool sel = Box.SelectionLength > 0;
            MenuEntry undo = MenuEntry.Item(I18n.T("Deshacer", "Undo"), "undo", false, delegate { Box.Undo(); });
            undo.Enabled = Box.CanUndo;
            MenuEntry redo = MenuEntry.Item(I18n.T("Rehacer", "Redo"), "redo", false, delegate { Box.Redo(); });
            redo.Enabled = Box.CanRedo;
            MenuEntry cut = MenuEntry.Item(I18n.T("Cortar", "Cut"), null, false, delegate { Box.Cut(); if (Copied != null) Copied(I18n.T("Cortado.", "Cut.")); });
            cut.Enabled = sel;
            MenuEntry copy = MenuEntry.Item(I18n.T("Copiar", "Copy"), "copy", false, delegate { Box.Copy(); if (Copied != null) Copied(I18n.T("Copiado.", "Copied.")); });
            copy.Enabled = sel;
            m.Add(undo); m.Add(redo); m.Add(MenuEntry.Line());
            m.Add(cut); m.Add(copy);
            m.Add(MenuEntry.Item(I18n.T("Pegar", "Paste"), null, false, delegate { if (!Pasted(false)) Box.Paste(); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item(I18n.T("Seleccionar todo", "Select all"), "check", false, delegate { Box.SelectAll(); }));
            Menu(e, m);
        }

        // ---- Decorations

        public void SetHits(List<Hit> h, int current)
        {
            hits = h;
            now = current;
            layer.InvalidateVisual();
        }

        // The offset of an XML error (-1 for none): a red wavy line under it.
        public void SetError(int offset)
        {
            if (errorAt == offset) return;
            errorAt = offset;
            layer.InvalidateVisual();
        }

        public int ErrorOffset { get { return errorAt; } }

        public void Repaint() { layer.InvalidateVisual(); }

        // Where each line starts, found as far as asked and dropped after an edit (for unwrapped text, visual lines are
        // the text's lines).
        sealed class LineIndex
        {
            readonly List<int> starts = new List<int> { 0 };
            bool complete;

            public void Changed(int offset)
            {
                int lo = 0, hi = starts.Count - 1, k = 0;
                while (lo <= hi) { int mid = (lo + hi) / 2; if (starts[mid] <= offset) { k = mid; lo = mid + 1; } else hi = mid - 1; }
                if (k + 1 < starts.Count) starts.RemoveRange(k + 1, starts.Count - k - 1);
                complete = false;
            }

            public int Start(string t, int k)
            {
                while (!complete && starts.Count <= k)
                {
                    int nl = t.IndexOf('\n', starts[starts.Count - 1]);
                    if (nl < 0) { complete = true; break; }
                    starts.Add(nl + 1);
                }
                return k < starts.Count ? starts[k] : t.Length + 1;
            }
        }

        // ---- Painting

        sealed class Tint : FrameworkElement
        {
            readonly MarkdownEditor ed;
            public Tint(MarkdownEditor ed) { this.ed = ed; }
            protected override void OnRender(DrawingContext dc) { ed.Paint(dc, this); }
        }

        // Lines painted in the last pass (tests check the work stays proportional to the screen).
        internal int PaintedLines;
        // Text in one colour (tests compare it with what the TextBox itself would draw).
        internal bool Plain { get; set; }

        void Paint(DrawingContext dc, Visual on)
        {
            PaintedLines = 0;
            Hook();
            if (host == null || !Box.IsLoaded) { repaint = true; return; }
            string t = Text;
            if (t.Length == 0) return;
            try
            {
                Point o = host.TranslatePoint(new Point(0, 0), layer), d = Box.TranslatePoint(new Point(0, 0), layer);
                dc.PushClip(new RectangleGeometry(new Rect(o.X, o.Y, Math.Max(0, host.ActualWidth), Math.Max(0, host.ActualHeight))));
                double ppd = VisualTreeHelper.GetDpi(on).PixelsPerDip, off = host.VerticalOffset;
                List<TokSpan> spans = new List<TokSpan>();
                foreach (int[] line in VisibleLines())
                {
                    int a = line[0], n = line[1];
                    if (a < 0 || a > t.Length) continue;
                    // Every visual line starts at the same left edge, LineHeight below the one before.
                    Point at = new Point(x0 - host.HorizontalOffset + d.X, y0 + line[2] * LineHeight - off + d.Y);
                    if (n == 0)
                    {
                        if (errorAt == a) Squiggle(dc, at.X, at.X + 7, at.Y + LineHeight - 4);
                        continue;
                    }
                    PaintedLines++;
                    FormattedText ft = new FormattedText(t.Substring(a, n), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, FontPx, ink, ppd);
                    syntax.Spans(a, a + n, spans);
                    if (!Plain) foreach (TokSpan sp in spans) ft.SetForegroundBrush(brushes[(int)sp.Kind], sp.Start - a, sp.Length);
                    if (hits != null && hits.Count > 0) PaintHits(dc, ft, at, a, n);
                    dc.DrawText(ft, at);
                    if (errorAt >= a && errorAt <= a + n)
                    {
                        int i = Math.Min(errorAt - a, n - 1);
                        Rect g = ft.BuildHighlightGeometry(at, i, 1).Bounds;
                        if (errorAt == a + n) g = new Rect(at.X + ft.WidthIncludingTrailingWhitespace, g.Top, 7, g.Height);
                        Squiggle(dc, g.Left, Math.Max(g.Right, g.Left + 6), at.Y + LineHeight - 4);
                    }
                }
                dc.Pop();
                // A few tries at most: an editor that never gets its lines must not keep the CPU busy.
                if (stale && ++retries < 20) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { layer.InvalidateVisual(); }));
                else if (!stale) retries = 0;
            }
            catch (Exception ex) { ShotStack.Log("Editor pintar: " + ex.Message); }
        }

        void PaintHits(DrawingContext dc, FormattedText ft, Point at, int a, int n)
        {
            int lo = 0, hi = hits.Count - 1, k = hits.Count;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (hits[mid].Start >= a) { k = mid; hi = mid - 1; } else lo = mid + 1;
            }
            // A match that starts on an earlier line may end on this one.
            if (k > 0 && hits[k - 1].Start + hits[k - 1].Length > a) k--;
            for (int i = k; i < hits.Count && hits[i].Start < a + n; i++)
            {
                int s = Math.Max(a, hits[i].Start), e = Math.Min(a + n, hits[i].Start + hits[i].Length);
                if (e <= s) continue;
                Geometry g = ft.BuildHighlightGeometry(at, s - a, e - s);
                if (g != null) dc.DrawGeometry(i == now ? nowBrush : hitBrush, null, g);
            }
        }

        void Squiggle(DrawingContext dc, double x0, double x1, double y)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(x0, y), false, false);
                bool up = true;
                for (double x = x0 + 2; x <= x1 + 0.5; x += 2) { c.LineTo(new Point(x, up ? y - 1.6 : y), true, true); up = !up; }
            }
            g.Freeze();
            dc.DrawGeometry(null, new Pen(errBrush, 1.1), g);
        }
    }
}
