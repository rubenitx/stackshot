// Stackshot - Find and replace: the search itself (plain text, whole words or a regular expression with a time limit) and
// the bar that slides in under the toolbar.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Stackshot
{
    public struct Hit
    {
        public int Start, Length;
        public Hit(int start, int length) { Start = start; Length = length; }
    }

    public sealed class FindOptions
    {
        public bool Case, Word, Regex;
    }

    public static class TextSearch
    {
        public const int MaxHits = 100000;
        static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(250);
        static readonly TimeSpan Total = TimeSpan.FromMilliseconds(1500);

        static Regex Build(string q, FindOptions o)
        {
            string p = o.Regex ? q : System.Text.RegularExpressions.Regex.Escape(q);
            if (o.Word)
            {
                // A plain query with punctuation at an edge ("(x)") needs no word boundary on that side.
                bool left = o.Regex || q.Length == 0 || IsWord(q[0]), right = o.Regex || q.Length == 0 || IsWord(q[q.Length - 1]);
                p = (left ? @"(?<![\w])" : "") + "(?:" + p + ")" + (right ? @"(?![\w])" : "");
            }
            RegexOptions ro = RegexOptions.Multiline | (o.Case ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return new Regex(p, ro, Limit);
        }

        static bool IsWord(char c)
        {
            // What the word class matches: letters, nonspacing marks, decimal digits and connector punctuation.
            switch (char.GetUnicodeCategory(c))
            {
                case System.Globalization.UnicodeCategory.UppercaseLetter:
                case System.Globalization.UnicodeCategory.LowercaseLetter:
                case System.Globalization.UnicodeCategory.TitlecaseLetter:
                case System.Globalization.UnicodeCategory.ModifierLetter:
                case System.Globalization.UnicodeCategory.OtherLetter:
                case System.Globalization.UnicodeCategory.NonSpacingMark:
                case System.Globalization.UnicodeCategory.DecimalDigitNumber:
                case System.Globalization.UnicodeCategory.ConnectorPunctuation:
                    return true;
            }
            return false;
        }

        // Null when the query can be searched; otherwise why not.
        public static string Error(string q, FindOptions o)
        {
            if (!o.Regex || string.IsNullOrEmpty(q)) return null;
            try { Build(q, o); return null; }
            catch (ArgumentException) { return I18n.T("Expresi\u00F3n no v\u00E1lida", "Invalid expression"); }
        }

        // Every match in order (at most max). timedOut: a regular expression took too long and the list is partial.
        public static List<Hit> All(string text, string q, FindOptions o, int max, out bool timedOut)
        {
            timedOut = false;
            List<Hit> hits = new List<Hit>();
            if (string.IsNullOrEmpty(q) || string.IsNullOrEmpty(text)) return hits;
            if (!o.Regex && !o.Word)
            {
                StringComparison cmp = o.Case ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int at = 0;
                while (hits.Count < max)
                {
                    int i = text.IndexOf(q, at, cmp);
                    if (i < 0) break;
                    hits.Add(new Hit(i, q.Length));
                    at = i + q.Length;
                }
                return hits;
            }
            Regex rx;
            try { rx = Build(q, o); }
            catch (ArgumentException) { return hits; }
            // The regex limit applies to each match; this one bounds the whole search.
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Match m = rx.Match(text);
                while (m.Success && hits.Count < max)
                {
                    if (sw.Elapsed > Total) { timedOut = true; break; }
                    if (m.Length > 0) hits.Add(new Hit(m.Index, m.Length));
                    m = m.Length > 0 ? m.NextMatch() : (m.Index + 1 <= text.Length ? rx.Match(text, m.Index + 1) : Match.Empty);
                }
            }
            catch (RegexMatchTimeoutException) { timedOut = true; }
            return hits;
        }

        // What replaces this match: the text as typed, or with $1 and the like filled in for a regular expression.
        public static string Replacement(string text, Hit h, string q, string repl, FindOptions o)
        {
            if (!o.Regex) return repl;
            try
            {
                Regex rx = Build(q, o);
                Match m = rx.Match(text, h.Start);
                if (m.Success && m.Index == h.Start) return m.Result(repl);
            }
            catch (ArgumentException) { }
            catch (RegexMatchTimeoutException) { }
            return repl;
        }

        // The text with every match replaced, and how many there were.
        public static string ReplaceAll(string text, string q, string repl, FindOptions o, out int count)
        {
            bool timedOut;
            return ReplaceAll(text, q, repl, o, out count, out timedOut);
        }

        // timedOut: the expression took too long; nothing is replaced rather than only the first part of the text.
        public static string ReplaceAll(string text, string q, string repl, FindOptions o, out int count, out bool timedOut)
        {
            count = 0;
            timedOut = false;
            if (string.IsNullOrEmpty(q) || string.IsNullOrEmpty(text)) return text;
            StringBuilder sb = new StringBuilder(text.Length);
            int at = 0, n = 0;
            if (!o.Regex && !o.Word)
            {
                foreach (Hit h in All(text, q, o, int.MaxValue, out timedOut))
                {
                    sb.Append(text, at, h.Start - at).Append(repl);
                    at = h.Start + h.Length;
                    n++;
                }
            }
            else
            {
                // Every match, empty ones included, as Regex.Replace does: "^" puts something before each line and "$"
                // after it (the find list skips them, as an empty match cannot be shown).
                Regex rx;
                try { rx = Build(q, o); }
                catch (ArgumentException) { return text; }
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    for (Match m = rx.Match(text); m.Success; m = m.NextMatch())
                    {
                        if (sw.Elapsed > Total) { timedOut = true; return text; }
                        sb.Append(text, at, m.Index - at).Append(o.Regex ? m.Result(repl) : repl);
                        at = m.Index + m.Length;
                        n++;
                    }
                }
                catch (RegexMatchTimeoutException) { timedOut = true; return text; }
            }
            if (n == 0) return text;
            sb.Append(text, at, text.Length - at);
            count = n;
            return sb.ToString();
        }

        // The first hit at or after pos (or the last before it, going back), wrapping around.
        public static int Next(List<Hit> hits, int pos, bool back)
        {
            if (hits.Count == 0) return -1;
            int lo = 0, hi = hits.Count - 1, first = hits.Count;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (hits[mid].Start >= pos) { first = mid; hi = mid - 1; } else lo = mid + 1;
            }
            if (!back) return first < hits.Count ? first : 0;
            int prev = first - 1;
            return prev >= 0 ? prev : hits.Count - 1;
        }
    }

    // The bar: query, options, count, previous/next and close; with replace, a second row.
    public sealed class FindBar : Border
    {
        public readonly FieldBox Query, With;
        readonly ChipToggle caseChip, wordChip, rxChip;
        readonly TextBlock count;
        readonly Grid replaceRow;
        readonly FindOptions options = new FindOptions();
        readonly DispatcherTimerLite later;
        bool replacing;

        public event Action Changed, Next, Prev, Replace, ReplaceAll, Closed;

        public FindBar(Palette pal, bool canReplace)
        {
            Background = Ds.Brush(pal.Dark ? Ds.Argb(0.04, 255, 255, 255) : Ds.Argb(0.025, 0, 0, 0));
            BorderBrush = Ds.Brush(pal.Separator);
            BorderThickness = new Thickness(0, 1, 0, 1);
            Padding = new Thickness(16, 7, 12, 7);
            later = new DispatcherTimerLite(150, delegate { Fire(Changed); });

            StackPanel rows = new StackPanel();
            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Query = new FieldBox(pal, I18n.T("Buscar", "Find"), "search", 280);
            Query.Changed += delegate { later.Restart(); };
            top.Children.Add(Query);
            StackPanel opts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            caseChip = new ChipToggle("Aa", I18n.T("Coincidir may\u00FAsculas", "Match case"), false);
            wordChip = new ChipToggle("ab", I18n.T("Palabra completa", "Whole word"), true) { Margin = new Thickness(2, 0, 0, 0) };
            rxChip = new ChipToggle(".*", I18n.T("Expresi\u00F3n regular", "Regular expression"), false) { Margin = new Thickness(2, 0, 0, 0) };
            caseChip.Toggled += delegate(bool on) { options.Case = on; Fire(Changed); };
            wordChip.Toggled += delegate(bool on) { options.Word = on; Fire(Changed); };
            rxChip.Toggled += delegate(bool on) { options.Regex = on; Fire(Changed); };
            opts.Children.Add(caseChip);
            opts.Children.Add(wordChip);
            opts.Children.Add(rxChip);
            Grid.SetColumn(opts, 1);
            top.Children.Add(opts);
            count = new TextBlock { FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(count, 2);
            top.Children.Add(count);
            StackPanel nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DocIconButton prev = new DocIconButton("up", I18n.T("Anterior (May\u00FAs+F3)", "Previous (Shift+F3)"), 26), next = new DocIconButton("down", I18n.T("Siguiente (F3)", "Next (F3)"), 26);
            DocIconButton close = new DocIconButton("close", I18n.T("Cerrar (Esc)", "Close (Esc)"), 26) { Margin = new Thickness(6, 0, 0, 0) };
            prev.Click += delegate { Fire(Prev); };
            next.Click += delegate { Fire(Next); };
            close.Click += delegate { Fire(Closed); };
            nav.Children.Add(prev);
            nav.Children.Add(next);
            nav.Children.Add(close);
            Grid.SetColumn(nav, 3);
            top.Children.Add(nav);
            rows.Children.Add(top);

            replaceRow = new Grid { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            replaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            replaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            With = new FieldBox(pal, I18n.T("Reemplazar por", "Replace with"), null, 280);
            replaceRow.Children.Add(With);
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            MacButton one = new MacButton(I18n.T("Reemplazar", "Replace"), ButtonKind.Secondary, null, null, 26);
            MacButton all = new MacButton(I18n.T("Reemplazar todo", "Replace all"), ButtonKind.Secondary, null, null, 26) { Margin = new Thickness(6, 0, 0, 0) };
            one.Click += delegate { Fire(Replace); };
            all.Click += delegate { Fire(ReplaceAll); };
            acts.Children.Add(one);
            acts.Children.Add(all);
            Grid.SetColumn(acts, 1);
            replaceRow.Children.Add(acts);
            if (canReplace) rows.Children.Add(replaceRow);
            Child = rows;

            Query.Box.PreviewKeyDown += OnKey;
            With.Box.PreviewKeyDown += OnKey;
        }

        public FindOptions Options { get { return options; } }

        public bool Replacing
        {
            get { return replacing; }
            set { replacing = value; replaceRow.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public void SetOptions(FindOptions o)
        {
            options.Case = o.Case; options.Word = o.Word; options.Regex = o.Regex;
            caseChip.IsOn = o.Case; wordChip.IsOn = o.Word; rxChip.IsOn = o.Regex;
        }

        // "3 de 17", "Sin resultados" or the reason the expression cannot be used.
        public void Show(int index, int total, string error, bool partial)
        {
            Palette pal = Ds.Brushes;
            Query.Bad = error != null || (total == 0 && Query.Text.Length > 0);
            count.Foreground = Ds.Brush(error != null ? pal.Error : pal.Label2);
            if (error != null) count.Text = error;
            else if (Query.Text.Length == 0) count.Text = "";
            else if (total == 0) count.Text = partial ? I18n.T("Sin resultados (b\u00FAsqueda cortada)", "No results (search cut short)") : I18n.T("Sin resultados", "No results");
            else count.Text = (index >= 0 ? (index + 1).ToString("N0") + I18n.T(" de ", " of ") : "") + total.ToString("N0") + (total >= TextSearch.MaxHits || partial ? "+" : "") + (index >= 0 ? "" : total == 1 ? I18n.T(" resultado", " result") : I18n.T(" resultados", " results"));
        }

        public void FocusQuery()
        {
            Query.Box.Focus();
            Query.Box.SelectAll();
        }

        public bool HasFocus { get { return IsKeyboardFocusWithin; } }

        void OnKey(object sender, KeyEventArgs e)
        {
            ModifierKeys mods = e.KeyboardDevice.Modifiers;
            bool shift = (mods & ModifierKeys.Shift) != 0;
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                later.Flush();
                if (sender == With.Box) Fire((mods & ModifierKeys.Control) != 0 ? ReplaceAll : Replace);
                else Fire(shift ? Prev : Next);
            }
            else if (e.Key == Key.Escape) { e.Handled = true; Fire(Closed); }
        }

        public void Flush() { later.Flush(); }

        static void Fire(Action a) { if (a != null) a(); }

        // Slides down from under the toolbar.
        public void Enter()
        {
            TranslateTransform tt = new TranslateTransform(0, -8);
            RenderTransform = tt;
            Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
            fade.Completed += delegate { BeginAnimation(OpacityProperty, null); Opacity = 1; RenderTransform = null; };
            BeginAnimation(OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    // A one-shot delay that restarts on every call (typing) and can be run at once.
    public sealed class DispatcherTimerLite
    {
        readonly System.Windows.Threading.DispatcherTimer t;
        readonly Action act;

        public DispatcherTimerLite(int ms, Action act)
        {
            this.act = act;
            t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += delegate { t.Stop(); act(); };
        }

        public void Restart() { t.Stop(); t.Start(); }
        public void Stop() { t.Stop(); }
        public bool Pending { get { return t.IsEnabled; } }
        public void Flush() { if (!t.IsEnabled) return; t.Stop(); act(); }
    }
}
