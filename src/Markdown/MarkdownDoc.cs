// Stackshot - Markdown parser: a small CommonMark subset (headings, lists, tasks, quotes, code, tables, rules, inline).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public enum MdKind { Heading, Paragraph, Code, Quote, List, Table, Rule }

    public sealed class MdSpan
    {
        public string Text, Href;
        public bool Bold, Italic, Code, Strike, Break;
        public bool Image;   // an image: Href is its source, Text a square mark and the alt text
    }

    public sealed class MdItem
    {
        public bool? Checked;   // null: not a task
        public List<MdBlock> Blocks = new List<MdBlock>();
    }

    public sealed class MdBlock
    {
        public MdKind Kind;
        public int Level;                 // heading level; first number of an ordered list
        public string Text;               // raw inline source (headings, paragraphs) or code
        public string Lang;
        public bool Ordered;
        public List<MdBlock> Children;    // quotes
        public List<MdItem> Items;        // lists
        public List<string> Head;         // tables
        public List<List<string>> Rows;
        public List<int> Align;           // 0 left, 1 center, 2 right
        public int Line;                  // top-level headings: the line they start on (0-based); -1 inside other blocks
    }

    public static class MarkdownDoc
    {
        static readonly Regex Fence = new Regex(@"^ {0,3}(`{3,}|~{3,})\s*([\w+#.-]*)", RegexOptions.Compiled);
        static readonly Regex Atx = new Regex(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$", RegexOptions.Compiled);
        static readonly Regex RuleRx = new Regex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$", RegexOptions.Compiled);
        static readonly Regex Marker = new Regex(@"^(\s*)([-*+]|\d{1,9}[.)])[ \t]+(.*)$", RegexOptions.Compiled);
        static readonly Regex MarkerEmpty = new Regex(@"^(\s*)([-*+]|\d{1,9}[.)])[ \t]*$", RegexOptions.Compiled);
        static readonly Regex Delim = new Regex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.Compiled);
        static readonly Regex Task = new Regex(@"^\[([ xX])\][ \t]+(.*)$", RegexOptions.Compiled);
        static readonly Regex BrRx = new Regex(@"^<br\s*/?>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<MdBlock> Parse(string text)
        {
            string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            // Tabs count as indentation only at the start of a line: a trailing tab is not a hard break.
            for (int i = 0; i < lines.Length; i++)
            {
                int n = 0;
                while (n < lines[i].Length && (lines[i][n] == ' ' || lines[i][n] == '\t')) n++;
                if (lines[i].IndexOf('\t', 0, n) >= 0) lines[i] = lines[i].Substring(0, n).Replace("\t", "    ") + lines[i].Substring(n);
            }
            return Blocks(lines, 0, lines.Length, 0);
        }

        static bool Blank(string l) { return l.Trim().Length == 0; }

        static int Indent(string l)
        {
            int n = 0;
            while (n < l.Length && l[n] == ' ') n++;
            return n;
        }

        static List<MdBlock> Blocks(string[] ln, int from, int to, int depth)
        {
            List<MdBlock> list = new List<MdBlock>();
            if (depth > 12) { Plain(list, ln, from, to); return list; }
            int i = from;
            while (i < to)
            {
                string l = ln[i];
                if (Blank(l)) { i++; continue; }
                Match m = Fence.Match(l);
                if (m.Success)
                {
                    string f = m.Groups[1].Value;
                    int ind = Indent(l);
                    List<string> code = new List<string>();
                    int j = i + 1;
                    for (; j < to; j++)
                    {
                        string t = ln[j].TrimStart();
                        // A closing fence may have spaces after it.
                        if (t.StartsWith(f, StringComparison.Ordinal) && t.TrimEnd().Trim('`', '~').Length == 0) break;
                        string c = ln[j];
                        code.Add(c.Substring(Math.Min(ind, Indent(c))));
                    }
                    // Never closed: the empty line after the file's last newline is not part of the code.
                    if (j >= to && code.Count > 0 && code[code.Count - 1].Length == 0) code.RemoveAt(code.Count - 1);
                    MdBlock b = new MdBlock { Kind = MdKind.Code, Text = string.Join("\n", code.ToArray()), Lang = m.Groups[2].Value };
                    list.Add(b);
                    i = j + 1;
                    continue;
                }
                m = Atx.Match(l);
                if (m.Success)
                {
                    list.Add(new MdBlock { Kind = MdKind.Heading, Level = m.Groups[1].Length, Text = m.Groups[2].Value.Trim(), Line = depth == 0 ? i : -1 });
                    i++;
                    continue;
                }
                if (RuleRx.IsMatch(l)) { list.Add(new MdBlock { Kind = MdKind.Rule }); i++; continue; }
                if (l.TrimStart().StartsWith(">", StringComparison.Ordinal) && Indent(l) < 4)
                {
                    List<string> inner = new List<string>();
                    int j = i;
                    for (; j < to; j++)
                    {
                        string t = ln[j];
                        if (Blank(t)) break;
                        string s = t.TrimStart();
                        if (s.StartsWith(">", StringComparison.Ordinal)) inner.Add(s.Substring(s.Length > 1 && s[1] == ' ' ? 2 : 1));
                        else if (StartsBlock(t)) break;
                        else inner.Add(t);
                    }
                    list.Add(new MdBlock { Kind = MdKind.Quote, Children = Blocks(inner.ToArray(), 0, inner.Count, depth + 1) });
                    i = j;
                    continue;
                }
                if (TableStarts(ln, i, to))
                {
                    MdBlock t = new MdBlock { Kind = MdKind.Table, Head = Cells(l), Rows = new List<List<string>>(), Align = new List<int>() };
                    foreach (string d in Cells(ln[i + 1]))
                        t.Align.Add(d.StartsWith(":", StringComparison.Ordinal) && d.EndsWith(":", StringComparison.Ordinal) ? 1 : d.EndsWith(":", StringComparison.Ordinal) ? 2 : 0);
                    int j = i + 2;
                    // A list item starts another block: it ends the table even with pipes in its text.
                    for (; j < to && !Blank(ln[j]) && ln[j].IndexOf('|') >= 0 && !Marker.IsMatch(ln[j]); j++)
                    {
                        List<string> row = Cells(ln[j]);
                        while (row.Count < t.Head.Count) row.Add("");
                        if (row.Count > t.Head.Count) row.RemoveRange(t.Head.Count, row.Count - t.Head.Count);
                        t.Rows.Add(row);
                    }
                    list.Add(t);
                    i = j;
                    continue;
                }
                m = Marker.Match(l);
                if (m.Success || MarkerEmpty.IsMatch(l))
                {
                    i = ReadList(list, ln, i, to, depth);
                    continue;
                }
                // Paragraph (a setext underline turns it into a heading).
                StringBuilder p = new StringBuilder();
                int k = i;
                for (; k < to; k++)
                {
                    string t = ln[k];
                    if (Blank(t)) break;
                    // Before the block check: right under a paragraph "---" underlines a heading, it is not a rule.
                    if (k > i && Regex.IsMatch(t, @"^ {0,3}(=+|-+)\s*$"))
                    {
                        list.Add(new MdBlock { Kind = MdKind.Heading, Level = t.TrimStart()[0] == '=' ? 1 : 2, Text = p.ToString().Trim(), Line = depth == 0 ? i : -1 });
                        p.Length = 0;
                        k++;
                        break;
                    }
                    if (k > i && StartsBlock(t)) break;
                    // A line with pipes right above a line of dashes is a table's header, even with no blank line before.
                    if (k > i && TableStarts(ln, k, to)) break;
                    if (p.Length > 0) p.Append(p[p.Length - 1] == '\n' ? "" : " ");
                    string s = t.Trim();
                    bool hard = t.EndsWith("  ", StringComparison.Ordinal) || t.EndsWith("\\", StringComparison.Ordinal);
                    p.Append(t.EndsWith("\\", StringComparison.Ordinal) ? s.Substring(0, s.Length - 1) : s);
                    if (hard) p.Append('\n');
                }
                if (p.Length > 0) list.Add(new MdBlock { Kind = MdKind.Paragraph, Text = p.ToString().TrimEnd('\n') });
                i = Math.Max(k, i + 1);
            }
            return list;
        }

        // Line i with pipes over a line of dashes with as many cells: a table's header and its delimiter row.
        static bool TableStarts(string[] ln, int i, int to)
        {
            // A list item line is an item even with pipes in its text ("1. | a |"), as Tidy and the table commands read it.
            return i + 1 < to && ln[i].IndexOf('|') >= 0 && Delim.IsMatch(ln[i + 1]) && ln[i + 1].IndexOf('-') >= 0 && ln[i + 1].IndexOf('|') >= 0 &&
                   Cells(ln[i]).Count == Cells(ln[i + 1]).Count && !Marker.IsMatch(ln[i]);
        }

        static void Plain(List<MdBlock> list, string[] ln, int from, int to)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < to; i++) { if (sb.Length > 0) sb.Append('\n'); sb.Append(ln[i]); }
            list.Add(new MdBlock { Kind = MdKind.Paragraph, Text = sb.ToString() });
        }

        // A line that interrupts a paragraph.
        static bool StartsBlock(string l)
        {
            if (Fence.IsMatch(l) || Atx.IsMatch(l) && l.TrimStart().StartsWith("#", StringComparison.Ordinal) || RuleRx.IsMatch(l)) return true;
            if (l.TrimStart().StartsWith(">", StringComparison.Ordinal)) return true;
            Match m = Marker.Match(l);
            return m.Success && (m.Groups[2].Value.Length == 1 || m.Groups[2].Value.StartsWith("1", StringComparison.Ordinal));
        }

        // Whether a marker at this indentation is the next item of the list (CommonMark): left of the text of the item
        // before it, and at most three columns in (or exactly where the list started).
        static bool Sibling(int indent, int baseIndent, int prevContent, bool first)
        {
            return first || (indent < prevContent && (indent <= 3 || indent == baseIndent));
        }

        static int ReadList(List<MdBlock> into, string[] ln, int i, int to, int depth)
        {
            Match first = Marker.Match(ln[i]);
            if (!first.Success) first = MarkerEmpty.Match(ln[i]);
            int baseIndent = first.Groups[1].Length;
            bool ordered = char.IsDigit(first.Groups[2].Value[0]);
            MdBlock b = new MdBlock { Kind = MdKind.List, Ordered = ordered, Items = new List<MdItem>() };
            if (ordered) int.TryParse(first.Groups[2].Value.Substring(0, first.Groups[2].Value.Length - 1), out b.Level);
            int j = i, prevContent = baseIndent + 1;
            while (j < to)
            {
                Match m = Marker.Match(ln[j]);
                if (!m.Success) m = MarkerEmpty.Match(ln[j]);
                if (!m.Success || !Sibling(m.Groups[1].Length, baseIndent, prevContent, j == i) || char.IsDigit(m.Groups[2].Value[0]) != ordered) break;
                int content = m.Groups[1].Length + m.Groups[2].Length + 1;
                prevContent = content;
                List<string> body = new List<string>();
                string head = m.Groups.Count > 3 ? m.Groups[3].Value : "";
                body.Add(head);
                j++;
                for (; j < to; j++)
                {
                    string t = ln[j];
                    if (Blank(t))
                    {
                        int n = j + 1;
                        while (n < to && Blank(ln[n])) n++;
                        if (n < to && Indent(ln[n]) >= content) { body.Add(""); continue; }
                        break;
                    }
                    if (Indent(t) >= content) { body.Add(t.Substring(Math.Min(content, Indent(t)))); continue; }
                    if (Marker.IsMatch(t) || MarkerEmpty.IsMatch(t) || StartsBlock(t)) break;
                    if (body.Count > 0 && !Blank(body[body.Count - 1])) { body.Add(t.Trim()); continue; }
                    break;
                }
                MdItem item = new MdItem();
                Match tk = Task.Match(body[0]);
                if (tk.Success) { item.Checked = tk.Groups[1].Value != " "; body[0] = tk.Groups[2].Value; }
                item.Blocks = Blocks(body.ToArray(), 0, body.Count, depth + 1);
                b.Items.Add(item);
                // Blank lines between items are skipped when the next item continues the list.
                int peek = j;
                while (peek < to && Blank(ln[peek])) peek++;
                if (peek < to && peek > j)
                {
                    Match nx = Marker.Match(ln[peek]);
                    if (!nx.Success) nx = MarkerEmpty.Match(ln[peek]);
                    if (nx.Success && Sibling(nx.Groups[1].Length, baseIndent, prevContent, false) && char.IsDigit(nx.Groups[2].Value[0]) == ordered) j = peek;
                }
            }
            into.Add(b);
            return Math.Max(j, i + 1);
        }

        public static List<string> Cells(string row) { return Cells(row, false); }

        // raw: escaped pipes stay escaped (for rewriting a table without changing its cells).
        public static List<string> Cells(string row, bool raw)
        {
            string r = row.Trim();
            if (r.StartsWith("|", StringComparison.Ordinal)) r = r.Substring(1);
            if (r.EndsWith("|", StringComparison.Ordinal) && !r.EndsWith("\\|", StringComparison.Ordinal)) r = r.Substring(0, r.Length - 1);
            List<string> cells = new List<string>();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < r.Length; i++)
            {
                char c = r[i];
                if (c == '\\' && i + 1 < r.Length && r[i + 1] == '|') { sb.Append(raw ? "\\|" : "|"); i++; continue; }
                // A code span keeps its pipes, but only one that closes: a stray backtick ("it`s") is just a character.
                if (c == '`')
                {
                    int end = CodeSpanEnd(r, i);
                    if (end > 0) { sb.Append(r, i, end - i); i = end - 1; continue; }
                }
                if (c == '|') { cells.Add(sb.ToString().Trim()); sb.Length = 0; continue; }
                sb.Append(c);
            }
            cells.Add(sb.ToString().Trim());
            return cells;
        }

        // A code span opened by the run of backticks at i: the index just past the run of as many backticks that closes
        // it, or -1 when none does (then they are plain characters). "``a|b``" is one span with a pipe inside.
        public static int CodeSpanEnd(string r, int i)
        {
            int n = 0;
            while (i + n < r.Length && r[i + n] == '`') n++;
            for (int j = i + n; j < r.Length; j++)
            {
                if (r[j] != '`') continue;
                int m = 0;
                while (j + m < r.Length && r[j + m] == '`') m++;
                if (m == n) return j + m;
                j += m - 1;
            }
            return -1;
        }

        // ---- Inline

        public static List<MdSpan> Inline(string s)
        {
            List<MdSpan> o = new List<MdSpan>();
            Run(s ?? "", o, false, false, false, null, 0);
            return o;
        }

        static void Add(List<MdSpan> o, string t, bool b, bool i, bool st, string href)
        {
            if (t.Length == 0) return;
            MdSpan last = o.Count > 0 ? o[o.Count - 1] : null;
            if (last != null && !last.Code && !last.Break && !last.Image && last.Bold == b && last.Italic == i && last.Strike == st && last.Href == href) { last.Text += t; return; }
            o.Add(new MdSpan { Text = t, Bold = b, Italic = i, Strike = st, Href = href });
        }

        static void Run(string s, List<MdSpan> o, bool b, bool it, bool st, string href, int depth)
        {
            StringBuilder buf = new StringBuilder();
            Dictionary<string, int> noCloser = null;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\n') { Add(o, buf.ToString(), b, it, st, href); buf.Length = 0; o.Add(new MdSpan { Break = true, Text = "" }); i++; continue; }
                if (c == '\\' && i + 1 < s.Length && "\\`*_{}[]()#+-.!|~<>".IndexOf(s[i + 1]) >= 0) { buf.Append(s[i + 1]); i += 2; continue; }
                if (c == '`')
                {
                    int n = 1;
                    while (i + n < s.Length && s[i + n] == '`') n++;
                    string tick = new string('`', n);
                    int e = s.IndexOf(tick, i + n, StringComparison.Ordinal);
                    if (e > 0)
                    {
                        Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                        o.Add(new MdSpan { Text = s.Substring(i + n, e - i - n).Trim(), Code = true, Bold = b, Italic = it, Href = href });
                        i = e + n;
                        continue;
                    }
                    buf.Append(tick); i += n; continue;
                }
                if (depth < 6 && (c == '*' || c == '_' || c == '~'))
                {
                    int n = 1;
                    while (i + n < s.Length && s[i + n] == c && n < 3) n++;
                    if (c == '~' && n < 2) { buf.Append(c); i++; continue; }
                    if (c == '~') n = 2;
                    bool open = i + n < s.Length && !char.IsWhiteSpace(s[i + n]);
                    bool wordBefore = i > 0 && char.IsLetterOrDigit(s[i - 1]);
                    if (c == '_' && wordBefore) open = false;
                    if (open)
                    {
                        string d = new string(c, n);
                        int e = i + n, none;
                        // A search that found no closer from some point finds none from any later point either, so a
                        // paragraph of unclosed '*' costs one scan instead of one per opener.
                        if (noCloser == null) noCloser = new Dictionary<string, int>();
                        if (noCloser.TryGetValue(d, out none) && e >= none) e = -1;
                        else
                        {
                            int from = e;
                            while ((e = s.IndexOf(d, e, StringComparison.Ordinal)) >= 0)
                            {
                                bool closes = !char.IsWhiteSpace(s[e - 1]) && (c != '_' || e + n >= s.Length || !char.IsLetterOrDigit(s[e + n]));
                                if (closes && e > i + n) break;
                                e += n;
                            }
                            if (e < 0) noCloser[d] = from;
                        }
                        if (e > 0)
                        {
                            Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                            string inner = s.Substring(i + n, e - i - n);
                            Run(inner, o, b || n >= 2 && c != '~', it || n == 1 || n == 3, st || c == '~', href, depth + 1);
                            i = e + n;
                            continue;
                        }
                    }
                    buf.Append(s, i, n); i += n; continue;
                }
                if ((c == '[' || (c == '!' && i + 1 < s.Length && s[i + 1] == '[')) && depth < 6)
                {
                    bool img = c == '!';
                    int start = i + (img ? 1 : 0);
                    int close = Closer(s, start, '[', ']');
                    if (close > 0 && close + 1 < s.Length && s[close + 1] == '(')
                    {
                        int pe = Closer(s, close + 1, '(', ')');
                        if (pe > 0)
                        {
                            string url = s.Substring(close + 2, pe - close - 2).Trim();
                            int sp = url.IndexOf(' ');
                            if (sp > 0) url = url.Substring(0, sp);
                            url = url.Trim('<', '>');
                            Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                            string label = s.Substring(start + 1, close - start - 1);
                            if (img) o.Add(new MdSpan { Text = "\u25A3 " + (label.Length > 0 ? label : I18n.T("imagen", "image")), Bold = b, Italic = true, Strike = st, Href = url, Image = true });
                            else Run(label.Length > 0 ? label : url, o, b, it, st, url, depth + 1);
                            i = pe + 1;
                            continue;
                        }
                    }
                }
                if (c == '<')
                {
                    int e = s.IndexOf('>', i);
                    // An HTML line break, as tables use for a new line inside a cell.
                    if (e > i && e - i <= 6 && BrRx.IsMatch(s.Substring(i, e - i + 1)))
                    {
                        Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                        o.Add(new MdSpan { Break = true, Text = "" });
                        i = e + 1;
                        continue;
                    }
                    if (e > i && Regex.IsMatch(s.Substring(i + 1, e - i - 1), @"^https?://\S+$"))
                    {
                        Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                        string u = s.Substring(i + 1, e - i - 1);
                        Add(o, u, b, it, st, u);
                        i = e + 1;
                        continue;
                    }
                }
                if ((c == 'h') && href == null && (i == 0 || !char.IsLetterOrDigit(s[i - 1])) &&
                    (string.CompareOrdinal(s, i, "http://", 0, 7) == 0 || string.CompareOrdinal(s, i, "https://", 0, 8) == 0))
                {
                    int e = i;
                    while (e < s.Length && !char.IsWhiteSpace(s[e]) && s[e] != '<' && s[e] != '>') e++;
                    while (e > i && ".,;:!?)".IndexOf(s[e - 1]) >= 0) e--;
                    Add(o, buf.ToString(), b, it, st, href); buf.Length = 0;
                    string u = s.Substring(i, e - i);
                    Add(o, u, b, it, st, u);
                    i = e;
                    continue;
                }
                buf.Append(c);
                i++;
            }
            Add(o, buf.ToString(), b, it, st, href);
        }

        static int Closer(string s, int open, char a, char z)
        {
            int d = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; }
                if (s[i] == a) d++;
                else if (s[i] == z && --d == 0) return i;
            }
            return -1;
        }

        public static string Plain(string inline)
        {
            StringBuilder sb = new StringBuilder();
            foreach (MdSpan sp in Inline(inline)) sb.Append(sp.Break ? " " : sp.Text);
            return sb.ToString();
        }
    }
}
