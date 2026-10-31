// Stackshot - Markdown editing commands as pure functions: each one returns a single edit (one undo step) and the new
// selection, so they can be tested without a window.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public struct TextEdit
    {
        public int Start, Remove, SelStart, SelLength;
        public string Insert;     // null: nothing to do

        public bool None { get { return Insert == null; } }

        public string ApplyTo(string text) { return Insert == null ? text : text.Substring(0, Start) + Insert + text.Substring(Start + Remove); }

        public static TextEdit Make(int start, int remove, string insert, int selStart, int selLength)
        {
            TextEdit e = new TextEdit();
            e.Start = start; e.Remove = remove; e.Insert = insert; e.SelStart = selStart; e.SelLength = selLength;
            return e;
        }
    }

    public enum LineKind { Bullet, Numbered, Task, Quote }

    public static class EditCommands
    {
        static readonly Regex ItemRx = new Regex(@"^(\s*)([-*+]|(\d{1,9})([.)]))([ \t]+)(\[[ xX]\][ \t]+)?", RegexOptions.Compiled);
        static readonly Regex QuoteRx = new Regex(@"^(\s*(?:>[ \t]?)+)", RegexOptions.Compiled);
        static readonly Regex HeadRx = new Regex(@"^ {0,3}(#{1,6})(?:[ \t]+|$)", RegexOptions.Compiled);
        static readonly Regex UrlRx = new Regex(@"^(https?://|mailto:|www\.)\S+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ---- Lines

        public static int LineStart(string t, int i)
        {
            if (i <= 0) return 0;
            int p = t.LastIndexOf('\n', Math.Min(i, t.Length) - 1);
            return p + 1;
        }

        public static int LineEnd(string t, int i)
        {
            int p = t.IndexOf('\n', Math.Min(i, t.Length));
            return p < 0 ? t.Length : p;
        }

        // The offset of a 1-based line and column, counting \r\n, \n and \r as one line break each (wrapping on screen is
        // not a line). The column is cut to the line's length.
        public static int Offset(string t, int line, int col)
        {
            int i = 0, l = 1;
            while (l < line && i < t.Length)
            {
                char c = t[i++];
                if (c == '\r') { if (i < t.Length && t[i] == '\n') i++; l++; }
                else if (c == '\n') l++;
            }
            int start = i;
            while (i < t.Length && t[i] != '\n' && t[i] != '\r' && i - start < Math.Max(0, col - 1)) i++;
            return i;
        }

        public static void LineCol(string t, int offset, out int line, out int col)
        {
            line = 1;
            int start = 0;
            offset = Math.Max(0, Math.Min(t.Length, offset));
            for (int i = 0; i < offset; i++)
            {
                char c = t[i];
                if (c == '\n' || (c == '\r' && (i + 1 >= t.Length || t[i + 1] != '\n'))) { line++; start = i + 1; }
            }
            col = offset - start + 1;
        }

        // The smallest edit that turns text[start, start+remove) into insert (shared ends are left alone).
        static TextEdit Minimal(string t, int start, int remove, string insert, int selStart, int selLength)
        {
            string old = t.Substring(start, remove);
            int p = 0, max = Math.Min(old.Length, insert.Length);
            while (p < max && old[p] == insert[p]) p++;
            int s = 0;
            while (s < max - p && old[old.Length - 1 - s] == insert[insert.Length - 1 - s]) s++;
            return TextEdit.Make(start + p, remove - p - s, insert.Substring(p, insert.Length - p - s), selStart, selLength);
        }

        // ---- Inline marks

        static int Run(string t, int i, int dir, char c)
        {
            int n = 0;
            for (int k = dir < 0 ? i - 1 : i; k >= 0 && k < t.Length && t[k] == c; k += dir) n++;
            return n;
        }

        static bool Wrapped(int before, int after, string mark)
        {
            int k = mark.Length;
            if (before < k || after < k) return false;
            if (mark == "*") return before % 2 == 1 && after % 2 == 1;
            if (mark == "**") return (before == 2 || before == 3) && (after == 2 || after == 3);
            return true;
        }

        // Bold, italic, strikethrough or code: wraps the selection, or unwraps it when it already is. Applying it twice gives
        // the original text back. With nothing selected a placeholder is inserted and selected.
        public static TextEdit Wrap(string t, int s, int len, string mark, string placeholder)
        {
            char c = mark[0];
            int k = mark.Length;
            if (Wrapped(Run(t, s, -1, c), Run(t, s + len, 1, c), mark))
                return TextEdit.Make(s - k, len + 2 * k, t.Substring(s, len), s - k, len);
            string sel = t.Substring(s, len);
            if (len >= 2 * k + 1 && sel.StartsWith(mark, StringComparison.Ordinal) && sel.EndsWith(mark, StringComparison.Ordinal) &&
                Wrapped(Run(sel, 0, 1, c), Run(sel, sel.Length, -1, c), mark))
                return TextEdit.Make(s, len, sel.Substring(k, len - 2 * k), s, len - 2 * k);
            if (len == 0) return TextEdit.Make(s, 0, mark + placeholder + mark, s + k, placeholder.Length);
            // Spaces at the ends stay outside the marks ("**word** ", not "**word **", which is not bold).
            int a = 0, b = len;
            while (a < b && char.IsWhiteSpace(sel[a])) a++;
            while (b > a && char.IsWhiteSpace(sel[b - 1])) b--;
            if (a == b) return TextEdit.Make(s + len, 0, mark + placeholder + mark, s + len + k, placeholder.Length);
            if (sel.IndexOf('\n', a, b - a) >= 0)
            {
                // Each line on its own: marks do not span paragraphs.
                string[] lines = sel.Substring(a, b - a).Split('\n');
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    string l = lines[i], tl = l.Trim();
                    if (tl.Length == 0) { sb.Append(l); continue; }
                    int lead = l.Length - l.TrimStart().Length;
                    sb.Append(l, 0, lead).Append(mark).Append(tl).Append(mark).Append(l.Substring(lead + tl.Length));
                }
                string w = sb.ToString();
                return TextEdit.Make(s + a, b - a, w, s + a, w.Length);
            }
            return TextEdit.Make(s + a, b - a, mark + sel.Substring(a, b - a) + mark, s + a + k, b - a);
        }

        // ---- Line prefixes

        delegate string LineFix(string line, int index, out int delta);

        // Rewrites the lines the selection touches; the caret keeps its place in its line.
        static TextEdit Lines(string t, int s, int len, LineFix fix)
        {
            int a = LineStart(t, s);
            int end = len > 0 && s + len > a && t[s + len - 1] == '\n' ? s + len - 1 : s + len;
            int b = LineEnd(t, Math.Max(end, s));
            string[] ls = t.Substring(a, b - a).Split('\n');
            StringBuilder sb = new StringBuilder();
            int caretDelta = 0, pos = a, lineStart = a;
            bool found = false;
            for (int i = 0; i < ls.Length; i++)
            {
                int delta;
                string n = fix(ls[i], i, out delta);
                if (!found && s >= pos && s <= pos + ls[i].Length) { found = true; caretDelta = delta; lineStart = a + sb.Length + (i > 0 ? 1 : 0); }
                pos += ls[i].Length + 1;
                if (i > 0) sb.Append('\n');
                sb.Append(n);
            }
            string block = sb.ToString();
            if (block == t.Substring(a, b - a)) return new TextEdit();
            if (len > 0) return Minimal(t, a, b - a, block, a, block.Length);
            int caret = Math.Max(lineStart, s + caretDelta);
            return Minimal(t, a, b - a, block, caret, 0);
        }

        static string Strip(string l, out int removed)
        {
            Match m = ItemRx.Match(l);
            if (m.Success) { removed = m.Length - m.Groups[1].Length; return l.Substring(0, m.Groups[1].Length) + l.Substring(m.Length); }
            removed = 0;
            return l;
        }

        static bool Has(string l, LineKind kind)
        {
            if (kind == LineKind.Quote) return QuoteRx.IsMatch(l);
            Match m = ItemRx.Match(l);
            if (!m.Success) return false;
            if (kind == LineKind.Task) return m.Groups[6].Success;
            if (m.Groups[6].Success) return false;
            return kind == LineKind.Numbered ? m.Groups[3].Success : !m.Groups[3].Success;
        }

        // Bullet, numbered or task list, or quote: added to every selected line, or removed when they all have it. A list
        // of another kind is converted.
        public static TextEdit Prefix(string t, int s, int len, LineKind kind)
        {
            int a = LineStart(t, s), b = LineEnd(t, Math.Max(s, s + len - (len > 0 && t[s + len - 1] == '\n' ? 1 : 0)));
            string[] ls = t.Substring(a, b - a).Split('\n');
            bool all = true, any = false;
            foreach (string l in ls)
            {
                if (l.Trim().Length == 0) continue;
                any = true;
                if (!Has(l, kind)) { all = false; break; }
            }
            bool remove = any && all;
            int n = 0;
            return Lines(t, s, len, delegate(string l, int i, out int delta)
            {
                delta = 0;
                if (kind == LineKind.Quote)
                {
                    Match q = QuoteRx.Match(l);
                    if (remove)
                    {
                        if (!q.Success) return l;
                        string inner = q.Value.TrimStart();
                        int cut = q.Value.Length - inner.Length + 1 + (inner.Length > 1 && (inner[1] == ' ' || inner[1] == '\t') ? 1 : 0);
                        delta = -cut;
                        return l.Substring(0, q.Value.Length - inner.Length) + l.Substring(cut);
                    }
                    delta = l.Length == 0 ? 1 : 2;
                    return l.Length == 0 ? ">" : "> " + l;
                }
                if (l.Trim().Length == 0 && ls.Length > 1) return l;
                int removed;
                string bare = Strip(l, out removed);
                if (remove) { delta = -removed; return bare; }
                int lead = bare.Length - bare.TrimStart().Length;
                n++;
                string mark = kind == LineKind.Bullet ? "- " : kind == LineKind.Task ? "- [ ] " : n + ". ";
                delta = mark.Length - removed;
                return bare.Substring(0, lead) + mark + bare.Substring(lead);
            });
        }

        // Heading of this level on the selected lines (0 removes it; the same level again removes it too).
        public static TextEdit Heading(string t, int s, int len, int level)
        {
            int a = LineStart(t, s);
            Match first = HeadRx.Match(t.Substring(a, LineEnd(t, s) - a));
            if (first.Success && first.Groups[1].Length == level) level = 0;
            return Lines(t, s, len, delegate(string l, int i, out int delta)
            {
                Match m = HeadRx.Match(l);
                string body = m.Success ? l.Substring(m.Length) : l.TrimStart();
                int old = l.Length - body.Length;
                if (body.Length == 0 && len > 0 && level > 0 && !m.Success) { delta = 0; return l; }
                string mark = level > 0 ? new string('#', level) + " " : "";
                delta = mark.Length - old;
                return mark + body;
            });
        }

        // The next heading level for the toolbar button: none, 1, 2, 3, then none again.
        public static int NextHeading(string t, int s)
        {
            int a = LineStart(t, s);
            Match m = HeadRx.Match(t.Substring(a, LineEnd(t, s) - a));
            int lv = m.Success ? m.Groups[1].Length : 0;
            return lv >= 3 ? 0 : lv + 1;
        }

        // ---- Blocks

        public static TextEdit Link(string t, int s, int len)
        {
            string sel = t.Substring(s, len);
            if (sel.IndexOf('\n') >= 0) return new TextEdit();
            if (len > 0 && UrlRx.IsMatch(sel.Trim()))
            {
                string url = sel.Trim();
                string ph = I18n.T("texto", "text");
                string ins = "[" + ph + "](" + url + ")";
                return TextEdit.Make(s, len, ins, s + 1, ph.Length);
            }
            if (len == 0) { string ph = I18n.T("texto", "text"); return TextEdit.Make(s, 0, "[" + ph + "](https://)", s + 1, ph.Length); }
            string w = "[" + sel + "](https://)";
            return TextEdit.Make(s, len, w, s + sel.Length + 3, 8);
        }

        // A blank line before and after a new block, unless the text already has one.
        static string Around(string t, int a, int b, string block, out int lead)
        {
            string before = a == 0 ? "" : a >= 2 && t[a - 1] == '\n' && t[a - 2] == '\n' ? "" : t[a - 1] == '\n' ? "\n" : "\n\n";
            string after = b >= t.Length ? "\n" : b + 1 < t.Length && t[b] == '\n' && t[b + 1] == '\n' ? "" : t[b] == '\n' ? "\n" : "\n\n";
            lead = before.Length;
            return before + block + after;
        }

        public static TextEdit CodeBlock(string t, int s, int len)
        {
            int a = LineStart(t, s), b = LineEnd(t, Math.Max(s, s + len - (len > 0 && t[s + len - 1] == '\n' ? 1 : 0)));
            string body = len > 0 ? t.Substring(a, b - a) : t.Substring(a, b - a).Trim().Length == 0 ? "" : null;
            if (body == null)
            {
                // A caret inside a line of text: the block goes after that line.
                int lead0;
                string ins = Around(t, b, b, "```\n\n```", out lead0);
                return TextEdit.Make(b, 0, ins, b + lead0 + 3, 0);
            }
            int lead;
            string block = Around(t, a, b, "```\n" + body + "\n```", out lead);
            if (a > 0 && t[a - 1] != '\n') lead = 0;
            return TextEdit.Make(a, b - a, block.Substring(0, block.Length), a + lead + 3, 0);
        }

        public static TextEdit Table(string t, int s, int cols, int rows)
        {
            int b = LineEnd(t, s), a = LineStart(t, s);
            bool emptyLine = t.Substring(a, b - a).Trim().Length == 0;
            int at = emptyLine ? a : b;
            StringBuilder sb = new StringBuilder();
            string[] head = new string[cols];
            for (int c = 0; c < cols; c++) head[c] = I18n.T("Columna ", "Column ") + (c + 1);
            int w = head[0].Length;
            sb.Append('|');
            foreach (string h in head) sb.Append(' ').Append(h).Append(" |");
            sb.Append("\n|");
            for (int c = 0; c < cols; c++) sb.Append(' ').Append(new string('-', w)).Append(" |");
            for (int r = 0; r < rows; r++)
            {
                sb.Append("\n|");
                for (int c = 0; c < cols; c++) sb.Append(' ').Append(new string(' ', w)).Append(" |");
            }
            int lead;
            string block = Around(t, at, emptyLine ? b : at, sb.ToString(), out lead);
            return TextEdit.Make(at, emptyLine ? b - a : 0, block, at + lead + 2, head[0].Length);
        }

        public static TextEdit Rule(string t, int s)
        {
            int a = LineStart(t, s), b = LineEnd(t, s);
            bool emptyLine = t.Substring(a, b - a).Trim().Length == 0;
            int at = emptyLine ? a : b, lead;
            string block = Around(t, at, emptyLine ? b : at, "---", out lead);
            int caret = at + block.Length;
            return TextEdit.Make(at, emptyLine ? b - a : 0, block, Math.Min(caret, at + block.Length), 0);
        }

        // ---- Typing helpers

        // Enter inside a list or a quote: the next line starts with the next marker; Enter on an empty item ends the list.
        // In a numbered list the items below move up by one, so no number repeats. Null (None) when a plain line break is
        // right.
        public static TextEdit Enter(string t, int s, int len)
        {
            // Inside fenced code a line that looks like a list item is code: a plain line break.
            if (InFence(t, s)) return new TextEdit();
            TextEdit ed = EnterPlain(t, s, len);
            string cur = t.Substring(LineStart(t, s), LineEnd(t, s) - LineStart(t, s));
            if (ed.None) return ed;
            Match quoted = QuoteRx.Match(cur);
            if (quoted.Success && NumRx.IsMatch(cur.Substring(quoted.Length)))
            {
                // A numbered list inside a quote: the same renumbering on the quote's lines without their "> ".
                string done = RenumberQuoted(ed.ApplyTo(t), ed.SelStart, quoted.Value);
                return done == null ? ed : Diff(t, done, ed.SelStart);
            }
            if (!NumRx.IsMatch(cur)) return ed;
            string after = ed.ApplyTo(t);
            int at = ed.SelStart;
            if (after.Substring(LineStart(after, at), LineEnd(after, at) - LineStart(after, at)).Trim().Length == 0)
            {
                // Enter removed an empty item. A blank line does not end a Markdown list, so items further down still
                // follow on from those above (and show so): they are counted on from them.
                int i = LineEnd(after, at) + 1;
                while (i < after.Length && after.Substring(i, LineEnd(after, i) - i).Trim().Length == 0) i = LineEnd(after, i) + 1;
                if (i >= after.Length || !NumRx.IsMatch(after.Substring(i, LineEnd(after, i) - i)) || Indent(after.Substring(i, LineEnd(after, i) - i)) != Indent(cur)) return ed;
                TextEdit below = Renumber(after, i);
                return below.None ? ed : Diff(t, below.ApplyTo(after), at);
            }
            TextEdit renum = Renumber(after, at);
            if (renum.None) return ed;
            return Diff(t, renum.ApplyTo(after), renum.SelStart);
        }

        // The text with the numbered list around the caret renumbered, among the lines that start with prefix (a quote's
        // "> "), or null when nothing changes. The caret stays on its line (only numbers below it change).
        static string RenumberQuoted(string t, int s, string prefix)
        {
            string[] ln = t.Split('\n');
            int[] at = new int[ln.Length + 1];
            for (int i = 0; i < ln.Length; i++) at[i + 1] = at[i] + ln[i].Length + 1;
            int line = 0;
            while (line + 1 < ln.Length && at[line + 1] <= s) line++;
            if (!ln[line].StartsWith(prefix, StringComparison.Ordinal)) return null;
            int a = line, b = line;
            while (a > 0 && ln[a - 1].StartsWith(prefix, StringComparison.Ordinal)) a--;
            while (b + 1 < ln.Length && ln[b + 1].StartsWith(prefix, StringComparison.Ordinal)) b++;
            StringBuilder inner = new StringBuilder();
            for (int i = a; i <= b; i++) { if (i > a) inner.Append('\n'); inner.Append(ln[i].Substring(prefix.Length)); }
            string body = inner.ToString();
            int caret = Math.Max(0, s - at[line] - prefix.Length) + (at[line] - at[a]) - (line - a) * prefix.Length;
            TextEdit r = Renumber(body, Math.Min(caret, body.Length));
            if (r.None) return null;
            string[] done = r.ApplyTo(body).Split('\n');
            StringBuilder o = new StringBuilder();
            for (int i = 0; i < a; i++) o.Append(ln[i]).Append('\n');
            for (int i = 0; i < done.Length; i++) { if (i > 0) o.Append('\n'); o.Append(prefix).Append(done[i]); }
            for (int i = b + 1; i < ln.Length; i++) o.Append('\n').Append(ln[i]);
            return o.ToString();
        }

        static readonly Regex FenceRx = new Regex(@"^[ \t]*(`{3,}|~{3,})", RegexOptions.Compiled);

        // Whether the line of s is inside a fenced code block (its opening line counts as outside, its body inside).
        public static bool InFence(string t, int s)
        {
            int end = LineStart(t, Math.Min(s, t.Length));
            string mark = null;
            int i = 0;
            while (i < end)
            {
                int e = t.IndexOf('\n', i);
                if (e < 0 || e > end) e = end;
                string l = t.Substring(i, e - i);
                // A fence inside a quote counts too ("> ```").
                Match q = QuoteRx.Match(l);
                if (q.Success) l = l.Substring(q.Length);
                if (mark == null)
                {
                    Match m = FenceRx.Match(l);
                    if (m.Success) mark = m.Groups[1].Value;
                }
                else
                {
                    string tl = l.Trim();
                    if (tl.Length >= mark.Length && tl[0] == mark[0] && tl.Trim(mark[0]).Length == 0) mark = null;
                }
                i = e + 1;
            }
            return mark != null;
        }

        // ---- Numbered lists

        static readonly Regex NumRx = new Regex(@"^([ \t]*)(\d{1,9})([.)])([ \t]+|$)", RegexOptions.Compiled);

        static int Indent(string l)
        {
            int n = 0;
            foreach (char c in l) { if (c == ' ') n++; else if (c == '\t') n += 4; else break; }
            return n;
        }

        // The numbered list the caret is in, renumbered in order from its first number (only the items at the caret's
        // level; nested ones keep theirs, a blank line between items keeps the list going). None when it is already right
        // or the caret is not on a numbered item.
        public static TextEdit Renumber(string t, int s)
        {
            string[] ln = t.Split('\n');
            int[] at = new int[ln.Length + 1];
            for (int i = 0; i < ln.Length; i++) at[i + 1] = at[i] + ln[i].Length + 1;
            int line = 0;
            while (line + 1 < ln.Length && at[line + 1] <= s) line++;
            Match m0 = NumRx.Match(ln[line]);
            if (!m0.Success) return new TextEdit();
            int ind = Indent(ln[line]);
            // Up and down while the lines still belong to this list: its items, deeper lines, blank lines between items.
            int first = line, last = line;
            for (int i = line - 1; i >= 0; i--)
            {
                if (ln[i].Trim().Length == 0) continue;
                Match m = NumRx.Match(ln[i]);
                int d = Indent(ln[i]);
                if (m.Success && d == ind) { first = i; continue; }
                if (d > ind) continue;
                break;
            }
            for (int i = line + 1; i < ln.Length; i++)
            {
                if (ln[i].Trim().Length == 0) continue;
                Match m = NumRx.Match(ln[i]);
                int d = Indent(ln[i]);
                if (m.Success && d == ind) { last = i; continue; }
                if (d > ind) { last = i; continue; }
                break;
            }
            long num;
            long.TryParse(NumRx.Match(ln[first]).Groups[2].Value, out num);
            StringBuilder sb = new StringBuilder();
            int caret = s;
            for (int i = first; i <= last; i++)
            {
                string l = ln[i];
                Match m = NumRx.Match(l);
                if (m.Success && Indent(l) == ind && num <= 999999999)
                {
                    // Past nine digits a number is no longer a list item: the rest stays as written. A number with a digit
                    // more or less moves the item's text: not when a line under it would end up outside the item.
                    string want = (num++).ToString();
                    int sp = m.Groups[4].Length, body = ind + m.Groups[2].Length + 1 + (sp >= 1 && sp <= 4 ? sp : 1);
                    if (want.Length == m.Groups[2].Length || MarkdownTools.KeepsBody(ln, i, ind, body, body + want.Length - m.Groups[2].Length))
                    {
                        string fixedLine = m.Groups[1].Value + want + l.Substring(m.Groups[1].Length + m.Groups[2].Length);
                        if (i < line || (i == line && s >= at[i] + m.Groups[1].Length + m.Groups[2].Length)) caret += want.Length - m.Groups[2].Length;
                        l = fixedLine;
                    }
                }
                if (i > first) sb.Append('\n');
                sb.Append(l);
            }
            int a = at[first], b = at[last] + ln[last].Length;
            string block = sb.ToString();
            if (block == t.Substring(a, b - a)) return new TextEdit();
            return TextEdit.Make(a, b - a, block, caret, 0);
        }

        // One edit from before to after: what lies between their common start and their common end.
        static TextEdit Diff(string before, string after, int caret)
        {
            int p = 0, max = Math.Min(before.Length, after.Length);
            while (p < max && before[p] == after[p]) p++;
            int q = 0;
            while (q < max - p && before[before.Length - 1 - q] == after[after.Length - 1 - q]) q++;
            return TextEdit.Make(p, before.Length - p - q, after.Substring(p, after.Length - p - q), caret, 0);
        }

        // ---- Tables: a row or a column added or removed where the caret is, the table aligned again

        public enum TableOp { RowBelow, ColumnRight, DeleteRow, DeleteColumn, Align }

        static readonly Regex DelimRx = new Regex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.Compiled);

        // Whether the caret is in a table (a header, its line of dashes and the rows under them).
        public static bool InTable(string t, int s)
        {
            int first, last, row, col;
            return FindTable(t, s, out first, out last, out row, out col);
        }

        static bool FindTable(string t, int s, out int first, out int last, out int row, out int col)
        {
            first = last = row = col = -1;
            string[] ln = t.Split('\n');
            int pos = 0, line = 0;
            for (; line < ln.Length; line++) { if (s <= pos + ln[line].Length) break; pos += ln[line].Length + 1; }
            // A list item line is an item even with pipes in its text.
            if (line >= ln.Length || ln[line].IndexOf('|') < 0 || ItemRx.IsMatch(ln[line]) || QuoteRx.IsMatch(ln[line])) return false;
            int a = line, b = line;
            while (a > 0 && ln[a - 1].Trim().Length > 0 && ln[a - 1].IndexOf('|') >= 0 && !ItemRx.IsMatch(ln[a - 1]) && !QuoteRx.IsMatch(ln[a - 1])) a--;
            while (b + 1 < ln.Length && ln[b + 1].Trim().Length > 0 && ln[b + 1].IndexOf('|') >= 0 && !ItemRx.IsMatch(ln[b + 1]) && !QuoteRx.IsMatch(ln[b + 1])) b++;
            // The header is the line right over the line of dashes, with as many cells: the nearest such pair at or over
            // the caret. A text line with pipes right above the header is not part of the table.
            int head = -1;
            // The line of dashes needs a pipe (a bare "---" underlines a heading); a cell missing still counts here, so
            // Alinear can complete it.
            for (int h = Math.Min(line, b - 1); h >= a && head < 0; h--)
                if (DelimRx.IsMatch(ln[h + 1]) && ln[h + 1].IndexOf('-') >= 0 && ln[h + 1].IndexOf('|') >= 0) head = h;
            if (head < 0) return false;
            first = head;
            last = b;
            row = line - head;
            col = ColumnAt(ln[line], s - pos);
            return true;
        }

        // The cell the caret is in: separators before it, read the way MarkdownDoc.Cells splits a row (escaped pipes and
        // pipes inside code are not separators, nor is a leading one).
        static int ColumnAt(string line, int caret)
        {
            int indent = line.Length - line.TrimStart().Length;
            string r = line.TrimStart();
            int col = 0;
            for (int i = r.StartsWith("|", StringComparison.Ordinal) ? 1 : 0; i < r.Length && indent + i < caret; i++)
            {
                char c = r[i];
                if (c == '\\' && i + 1 < r.Length && r[i + 1] == '|') { i++; continue; }
                if (c == '`')
                {
                    int end = MarkdownDoc.CodeSpanEnd(r, i);
                    if (end > 0) { i = end - 1; continue; }
                }
                if (c == '|') col++;
            }
            return col;
        }

        public static TextEdit Table(string t, int s, TableOp op)
        {
            int first, last, row, col;
            if (!FindTable(t, s, out first, out last, out row, out col)) return new TextEdit();
            string[] ln = t.Split('\n');
            List<List<string>> rows = new List<List<string>>();
            rows.Add(MarkdownDoc.Cells(ln[first], true));
            List<string> delim = MarkdownDoc.Cells(ln[first + 1]);
            for (int i = first + 2; i <= last; i++) rows.Add(MarkdownDoc.Cells(ln[i], true));
            // A row longer than the header widens the table rather than losing its last cells.
            int n = 0;
            foreach (List<string> r in rows) n = Math.Max(n, r.Count);
            foreach (List<string> r in rows) while (r.Count < n) r.Add("");
            while (delim.Count < n) delim.Add("---");
            if (delim.Count > n) delim.RemoveRange(n, delim.Count - n);
            col = Math.Min(col, n - 1);
            // rows[0] is the header and the line of dashes has no cells, so table line k (k >= 2) is rows[k - 1].
            int r0 = row <= 1 ? 0 : row - 1, targetRow = r0, targetCol = col;
            switch (op)
            {
                case TableOp.RowBelow:
                    {
                        List<string> blank = new List<string>();
                        for (int c = 0; c < n; c++) blank.Add("");
                        rows.Insert(r0 + 1, blank);
                        targetRow = r0 + 1;
                        targetCol = 0;
                        break;
                    }
                case TableOp.ColumnRight:
                    foreach (List<string> r in rows) r.Insert(col + 1, "");
                    rows[0][col + 1] = I18n.T("Columna ", "Column ") + (n + 1);
                    delim.Insert(col + 1, "---");
                    n++;
                    targetCol = col + 1;
                    break;
                case TableOp.DeleteRow:
                    if (r0 == 0) return new TextEdit();    // the header stays: a table needs one
                    rows.RemoveAt(r0);
                    targetRow = Math.Min(r0, rows.Count - 1);
                    break;
                case TableOp.DeleteColumn:
                    if (n <= 1) return new TextEdit();
                    foreach (List<string> r in rows) r.RemoveAt(col);
                    delim.RemoveAt(col);
                    n--;
                    targetCol = Math.Min(col, n - 1);
                    break;
            }
            // A table inside a list item keeps its indentation.
            string indent = MarkdownTools.LeadingSpace(ln[first]);
            List<string> lines = MarkdownTools.FormatTable(rows, delim, indent);
            int a = 0;
            for (int i = 0; i < first; i++) a += ln[i].Length + 1;
            int b = a;
            for (int i = first; i <= last; i++) b += ln[i].Length + (i < last ? 1 : 0);
            string block = string.Join("\n", lines.ToArray());
            // The caret goes to the start of the target cell (the line of dashes sits after the header). FormatTable
            // writes the indent, "| ", then each cell padded to its column's width on screen and " |".
            int lineIndex = targetRow == 0 ? 0 : targetRow + 1, off = a;
            for (int i = 0; i < lineIndex; i++) off += lines[i].Length + 1;
            off += indent.Length + 2;
            int[] widths = MarkdownTools.ColumnWidths(rows);
            for (int c = 0; c < targetCol; c++)
            {
                string cell = rows[targetRow][c];
                off += cell.Length + Math.Max(0, widths[c] - MarkdownTools.DisplayWidth(cell)) + 3;
            }
            return TextEdit.Make(a, b - a, block, off, 0);
        }

        static TextEdit EnterPlain(string t, int s, int len)
        {
            int a = LineStart(t, s), b = LineEnd(t, s);
            if (len > 0 && t.IndexOf('\n', s, len) >= 0) return new TextEdit();
            string line = t.Substring(a, b - a);
            // A list inside a quote ("> 1. a") goes on inside the quote.
            Match q = QuoteRx.Match(line);
            string quote = q.Success ? q.Value : "";
            Match m = ItemRx.Match(line.Substring(quote.Length));
            if (m.Success && s - a >= quote.Length + m.Length)
            {
                if (line.Substring(quote.Length + m.Length).Trim().Length == 0 && len == 0)
                {
                    string keep = quote.TrimEnd();
                    return TextEdit.Make(a, b - a, keep, a + keep.Length, 0);
                }
                string marker;
                if (m.Groups[3].Success)
                {
                    long num;
                    long.TryParse(m.Groups[3].Value, out num);
                    // Ten digits would not be a list item: past nine the number repeats (Markdown counts on anyway).
                    marker = (num + 1 > 999999999 ? num : num + 1) + m.Groups[4].Value;
                }
                else marker = m.Groups[2].Value;
                string next = "\n" + quote + m.Groups[1].Value + marker + m.Groups[5].Value + (m.Groups[6].Success ? "[ ] " : "");
                return TextEdit.Make(s, len, next, s + next.Length, 0);
            }
            if (q.Success && s - a >= q.Length)
            {
                if (line.Substring(q.Length).Trim().Length == 0 && len == 0) return TextEdit.Make(a, b - a, "", a, 0);
                string next = "\n" + q.Value;
                return TextEdit.Make(s, len, next, s + next.Length, 0);
            }
            return new TextEdit();
        }

        // Enter in XML keeps the indentation of the line.
        public static TextEdit EnterIndent(string t, int s, int len)
        {
            int a = LineStart(t, s);
            int i = a;
            while (i < t.Length && i < s && (t[i] == ' ' || t[i] == '\t')) i++;
            string ins = "\n" + t.Substring(a, i - a);
            return TextEdit.Make(s, len, ins, s + ins.Length, 0);
        }

        // Tab and Shift+Tab on list items move them one level in or out; None when the caret is not in a list.
        public static TextEdit Indent(string t, int s, int len, bool outdent)
        {
            int a = LineStart(t, s);
            if (!ItemRx.IsMatch(t.Substring(a, LineEnd(t, s) - a))) return new TextEdit();
            return Lines(t, s, len, delegate(string l, int i, out int delta)
            {
                Match m = ItemRx.Match(l);
                delta = 0;
                if (!m.Success) return l;
                int step = m.Groups[3].Success ? m.Groups[2].Length + 1 : 2;
                if (!outdent) { delta = step; return new string(' ', step) + l; }
                int cut = 0;
                while (cut < step && cut < l.Length && l[cut] == ' ') cut++;
                if (cut == 0 && l.Length > 0 && l[0] == '\t') cut = 1;
                delta = -cut;
                return l.Substring(cut);
            });
        }
    }
}
