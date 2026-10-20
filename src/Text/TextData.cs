// Stackshot - Reads CSV/TSV tables, JSON documents (and JSON Lines) and log lines into plain data for the text viewers.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Stackshot
{
    // ---- CSV / TSV

    public sealed class CsvTable
    {
        public readonly List<string[]> Rows = new List<string[]>();   // every row padded to Columns
        public char Delimiter;
        public bool HasHeader;      // first row looks like column names
        public int Columns;
        public bool Truncated;      // stopped at maxRows
        public readonly List<int> RowLines = new List<int>();   // 0-based text line where each row starts (for "show in the text")

        const int DetectRecords = 50;
        static StringComparer textComparer;

        // Collects records for Parse and for the delimiter probe. A blank line waits until a real record follows it, so
        // blank lines at the end never become rows.
        sealed class Records
        {
            public readonly List<string[]> Rows;
            public readonly List<int> Lines;
            public bool Truncated, Full;
            readonly int max;
            readonly bool keepBlank;
            readonly List<int> blanks = new List<int>();

            public Records(List<string[]> rows, List<int> lines, int max, bool keepBlank)
            {
                Rows = rows;
                Lines = lines;
                this.max = max;
                this.keepBlank = keepBlank;
            }

            public void End(List<string> cur, bool blank, int line)
            {
                if (Full) return;
                if (blank)
                {
                    if (keepBlank) blanks.Add(line);
                    return;
                }
                for (int k = 0; k < blanks.Count; k++)
                    if (!Add(new string[] { "" }, blanks[k])) return;
                blanks.Clear();
                Add(cur.ToArray(), line);
            }

            bool Add(string[] row, int line)
            {
                if (Rows.Count >= max) { Truncated = true; Full = true; return false; }
                Rows.Add(row);
                Lines.Add(line);
                return true;
            }
        }

        public static CsvTable Parse(string text, int maxRows, out string error)
        {
            return ParseWith(text, Detect(text), maxRows, out error);
        }

        // Same as Parse with the delimiter chosen by the caller (no detection).
        public static CsvTable ParseWith(string text, char delimiter, int maxRows, out string error)
        {
            error = null;
            CsvTable t = new CsvTable();
            t.Delimiter = delimiter;
            if (string.IsNullOrEmpty(text)) return t;
            int start = text[0] == '\uFEFF' ? 1 : 0;
            Records rd = new Records(t.Rows, t.RowLines, maxRows > 0 ? maxRows : int.MaxValue, true);
            if (ReadAll(text, start, delimiter, rd)) error = "Hay unas comillas sin cerrar al final del archivo.";
            t.Truncated = rd.Truncated;
            int cols = 0;
            for (int r = 0; r < t.Rows.Count; r++) cols = Math.Max(cols, t.Rows[r].Length);
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string[] row = t.Rows[r];
                if (row.Length == cols) continue;
                string[] padded = new string[cols];
                Array.Copy(row, padded, row.Length);
                for (int c = row.Length; c < cols; c++) padded[c] = "";
                t.Rows[r] = padded;
            }
            t.Columns = cols;
            t.HasHeader = LooksLikeHeader(t);
            return t;
        }

        // RFC 4180 reading from start. Returns true when a quote was left open at the end of the text.
        static bool ReadAll(string text, int start, char delim, Records rd)
        {
            int n = text.Length, i = start, line = 0, recLine = 0;
            List<string> cur = new List<string>();
            StringBuilder f = new StringBuilder();
            bool quoted = false, inQuotes = false;
            while (i < n && !rd.Full)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"') { f.Append('"'); i += 2; continue; }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    // CRLF counts as one line break; the characters themselves stay as written.
                    if (c == '\n' || (c == '\r' && !(i + 1 < n && text[i + 1] == '\n'))) line++;
                    f.Append(c);
                    i++;
                    continue;
                }
                if (c == '"' && f.Length == 0 && !quoted) { inQuotes = true; quoted = true; i++; continue; }
                if (c == delim) { cur.Add(f.ToString()); f.Length = 0; quoted = false; i++; continue; }
                if (c == '\r' || c == '\n')
                {
                    bool blank = cur.Count == 0 && f.Length == 0 && !quoted;
                    cur.Add(f.ToString());
                    f.Length = 0;
                    quoted = false;
                    i += (c == '\r' && i + 1 < n && text[i + 1] == '\n') ? 2 : 1;
                    rd.End(cur, blank, recLine);
                    cur.Clear();
                    line++;
                    recLine = line;
                    continue;
                }
                f.Append(c);
                i++;
            }
            bool open = inQuotes;
            if (!rd.Full && (cur.Count > 0 || f.Length > 0 || quoted))
            {
                cur.Add(f.ToString());
                rd.End(cur, false, recLine);
            }
            return open;
        }

        // The delimiter whose field counts agree most over the first records; ties go to tab, then to the comma unless
        // the commas look like decimal commas (then ';' wins, as in Spanish Excel files).
        public static char Detect(string text)
        {
            if (string.IsNullOrEmpty(text)) return ',';
            int start = text[0] == '\uFEFF' ? 1 : 0;
            bool decimalCommas = CommasAreDecimal(text, start);
            char[] candidates = { '\t', ';', '|', ',' };
            char best = ',';
            double bestShare = 0;
            int bestRank = -1;
            for (int k = 0; k < candidates.Length; k++)
            {
                char d = candidates[k];
                double share = Consistency(text, start, d);
                if (share <= 0) continue;
                int rank = d == '\t' ? 4 : d == ',' ? (decimalCommas ? 1 : 3) : d == ';' ? (decimalCommas ? 3 : 2) : (decimalCommas ? 2 : 1);
                bool better = share > bestShare + 1e-9 || (share > bestShare - 1e-9 && rank > bestRank);
                if (!better) continue;
                best = d;
                bestShare = share;
                bestRank = rank;
            }
            return best;
        }

        // Share of the first records that have the most common field count (above 1); 0 when no count above 1 exists.
        static double Consistency(string text, int start, char d)
        {
            List<string[]> rows = new List<string[]>();
            List<int> lines = new List<int>();
            ReadAll(text, start, d, new Records(rows, lines, DetectRecords, false));
            if (rows.Count == 0) return 0;
            int bestCount = 0, bestFreq = 0;
            for (int a = 0; a < rows.Count; a++)
            {
                int len = rows[a].Length;
                if (len < 2) continue;
                int freq = 0;
                for (int b = 0; b < rows.Count; b++)
                    if (rows[b].Length == len) freq++;
                if (freq > bestFreq || (freq == bestFreq && len > bestCount)) { bestFreq = freq; bestCount = len; }
            }
            return bestFreq == 0 ? 0 : (double)bestFreq / rows.Count;
        }

        static bool CommasAreDecimal(string text, int start)
        {
            int end = Math.Min(text.Length, start + 4000), total = 0, dec = 0;
            for (int k = start; k < end; k++)
            {
                if (text[k] != ',') continue;
                total++;
                if (k > start && k + 1 < text.Length && IsDigit(text[k - 1]) && IsDigit(text[k + 1])) dec++;
            }
            return dec > 0 && dec * 2 > total;
        }

        static bool LooksLikeHeader(CsvTable t)
        {
            if (t.Rows.Count < 2) return false;
            string[] h = t.Rows[0];
            for (int c = 0; c < h.Length; c++)
                if (h[c].Trim().Length == 0 || IsNumber(h[c])) return false;
            int limit = Math.Min(t.Rows.Count, 200);
            for (int r = 1; r < limit; r++)
                for (int c = 0; c < t.Rows[r].Length; c++)
                    if (IsNumber(t.Rows[r][c])) return true;
            for (int a = 0; a < h.Length; a++)
                for (int b = a + 1; b < h.Length; b++)
                    if (string.Equals(h[a].Trim(), h[b].Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            string[] second = t.Rows[1];
            for (int c = 0; c < h.Length; c++)
                if (string.Equals(h[c].Trim(), second[c].Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        // ---- Numbers

        public static bool IsNumber(string s)
        {
            double v;
            return TryNumber(s, out v);
        }

        // The value as shown ("50 %" is 50), or NaN when s is not a number.
        public static double NumberValue(string s)
        {
            double v;
            return TryNumber(s, out v) ? v : double.NaN;
        }

        // Accepts 12, -3.5, 1,5, 1.234,56, 1,234.56, 3e5, 50 %, 12,50 EUR. A lone '.' or ',' is the decimal mark;
        // with both, the last one is; repeated ones must be thousands groups of three digits.
        static bool TryNumber(string s, out double v)
        {
            v = double.NaN;
            if (s == null) return false;
            int a = 0, b = s.Length;
            while (a < b && char.IsWhiteSpace(s[a])) a++;
            while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
            if (a >= b || b - a > 64) return false;
            bool unit = false, neg = false, hasSign = false;
            if (IsUnit(s[b - 1]))
            {
                unit = true;
                b--;
                while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
            }
            if (a < b && IsSign(s[a])) { neg = s[a] != '+'; hasSign = true; a++; }
            if (!unit && a < b && IsUnit(s[a]) && s[a] != '%')
            {
                a++;
                while (a < b && char.IsWhiteSpace(s[a])) a++;
                if (!hasSign && a < b && IsSign(s[a])) { neg = s[a] != '+'; a++; }
            }
            if (a >= b) return false;

            int e = -1;
            for (int k = a; k < b; k++)
                if (s[k] == 'e' || s[k] == 'E') { e = k; break; }
            string exp = null;
            int mantissaEnd = b;
            if (e >= 0)
            {
                mantissaEnd = e;
                int q = e + 1;
                if (q < b && (s[q] == '+' || s[q] == '-')) q++;
                if (q >= b) return false;
                for (int k = q; k < b; k++)
                    if (!IsDigit(s[k])) return false;
                exp = s.Substring(e + 1, b - e - 1);
            }
            string m = s.Substring(a, mantissaEnd - a);
            if (m.Length == 0) return false;

            int dots = 0, commas = 0, lastDot = -1, lastComma = -1;
            for (int k = 0; k < m.Length; k++)
            {
                char c = m[k];
                if (c == '.') { dots++; lastDot = k; }
                else if (c == ',') { commas++; lastComma = k; }
                else if (!IsDigit(c)) return false;
            }
            char dec = '\0', thou = '\0';
            if (dots > 0 && commas > 0)
            {
                dec = lastDot > lastComma ? '.' : ',';
                thou = dec == '.' ? ',' : '.';
            }
            else if (dots > 0) { if (dots == 1) dec = '.'; else thou = '.'; }
            else if (commas > 0) { if (commas == 1) dec = ','; else thou = ','; }

            string whole = m, frac = "";
            if (dec != '\0')
            {
                int at = m.LastIndexOf(dec);
                if (m.IndexOf(dec) != at) return false;
                whole = m.Substring(0, at);
                frac = m.Substring(at + 1);
                if (frac.Length == 0 || frac.IndexOf('.') >= 0 || frac.IndexOf(',') >= 0) return false;
            }
            if (thou != '\0')
            {
                if (exp != null || whole.Length == 0) return false;
                string[] groups = whole.Split(thou);
                if (groups[0].Length < 1 || groups[0].Length > 3) return false;
                for (int k = 1; k < groups.Length; k++)
                    if (groups[k].Length != 3) return false;
                whole = string.Concat(groups);
            }
            string canon = (whole.Length == 0 ? "0" : whole) + (frac.Length > 0 ? "." + frac : "") + (exp != null ? "e" + exp : "");
            double parsed;
            if (!double.TryParse(canon, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return false;
            if (double.IsInfinity(parsed) || double.IsNaN(parsed)) return false;
            v = neg ? -parsed : parsed;
            return true;
        }

        static bool IsDigit(char c) { return c >= '0' && c <= '9'; }
        static bool IsSign(char c) { return c == '-' || c == '+' || c == '\u2212'; }
        static bool IsUnit(char c) { return c == '%' || c == '$' || c == '\u20AC' || c == '\u00A3' || c == '\u00A5'; }

        // ---- Sorting

        // Row indices ordered by a column, header row left out. Numbers go first (numerically), then text (culture
        // aware, ignoring case); empty cells stay last in either direction; equal values keep their order.
        public static List<int> SortedOrder(CsvTable t, int column, bool descending)
        {
            List<int> order = new List<int>();
            int first = t.HasHeader ? 1 : 0;
            for (int r = first; r < t.Rows.Count; r++) order.Add(r);
            if (column < 0 || column >= t.Columns) return order;
            int count = t.Rows.Count;
            double[] num = new double[count];
            byte[] kind = new byte[count];    // 0 number, 1 text, 2 empty
            string[] text = new string[count];
            for (int r = first; r < count; r++)
            {
                string cell = t.Rows[r][column];
                text[r] = cell.Trim();
                if (text[r].Length == 0) { kind[r] = 2; continue; }
                double v;
                if (TryNumber(cell, out v)) num[r] = v;
                else kind[r] = 1;
            }
            StringComparer cmp = TextComparer();
            order.Sort(delegate(int x, int y)
            {
                int kx = kind[x], ky = kind[y];
                if (kx == 2 || ky == 2) return kx != ky ? (kx == 2 ? 1 : -1) : x.CompareTo(y);
                int c;
                if (kx != ky) c = kx - ky;
                else if (kx == 0) c = num[x].CompareTo(num[y]);
                else c = cmp.Compare(text[x], text[y]);
                if (descending) c = c < 0 ? 1 : c > 0 ? -1 : 0;
                return c != 0 ? c : x.CompareTo(y);
            });
            return order;
        }

        static StringComparer TextComparer()
        {
            if (textComparer == null)
            {
                CultureInfo ci;
                try { ci = new CultureInfo("es-ES"); }
                catch (CultureNotFoundException) { ci = CultureInfo.InvariantCulture; }
                textComparer = StringComparer.Create(ci, true);
            }
            return textComparer;
        }
    }

    // ---- JSON

    public enum JsonKind { Object, Array, String, Number, True, False, Null }

    public sealed class JsonNode
    {
        public JsonKind Kind;
        public string Key;          // the member name in its parent object, or null (array items, the root)
        public int Index = -1;      // position in its parent array, or -1
        public string Value;        // String: decoded text; Number: as written; True/False/Null: "true"/"false"/"null"; Object/Array: null
        public List<JsonNode> Children;   // Object/Array only (never null for them)
        public JsonNode Parent;
        public int Line, Col;       // 1-based position where the value starts in the text
        public int EndLine;         // 1-based line where it ends (for folding)
    }

    public static class JsonDoc
    {
        const string MsgComment = "Los comentarios no son JSON v\u00E1lido";
        const int MaxIndentDepth = 200;   // deeper levels share the last indent, so a 10000-deep file stays printable

        // Iterative (explicit stack), so very deep nesting is fine. Strict JSON; error is Spanish, line/col are 1-based.
        public static JsonNode Parse(string text, out string error, out int line, out int col)
        {
            JsonReader r = new JsonReader(text ?? "");
            JsonNode root = r.Run();
            error = r.Error;
            line = r.ErrLine;
            col = r.ErrCol;
            return root;
        }

        // One value per non-blank line; the result is an Array whose children are the values in order.
        public static JsonNode ParseLines(string text, out string error, out int line, out int col)
        {
            error = null;
            line = 0;
            col = 0;
            JsonNode all = new JsonNode();
            all.Kind = JsonKind.Array;
            all.Children = new List<JsonNode>();
            all.Line = 1;
            all.Col = 1;
            all.EndLine = 1;
            if (string.IsNullOrEmpty(text)) return all;
            JsonReader r = new JsonReader(text);
            int n = text.Length, pos = r.Start, number = 1;
            while (true)
            {
                int end = pos;
                while (end < n && text[end] != '\n' && text[end] != '\r') end++;
                if (!Blank(text, pos, end))
                {
                    r.Reset(pos, end, number);
                    JsonNode v = r.Run();
                    if (v == null)
                    {
                        error = r.Error;
                        line = r.ErrLine;
                        col = r.ErrCol;
                        return null;
                    }
                    v.Parent = all;
                    v.Index = all.Children.Count;
                    all.Children.Add(v);
                }
                if (end >= n) break;
                pos = end + ((text[end] == '\r' && end + 1 < n && text[end + 1] == '\n') ? 2 : 1);
                number++;
            }
            all.EndLine = number;
            return all;
        }

        static bool Blank(string s, int from, int to)
        {
            for (int k = from; k < to; k++)
                if (!char.IsWhiteSpace(s[k])) return false;
            return true;
        }

        sealed class JsonReader
        {
            const int StValue = 0, StAfter = 1, StKey = 2, StColon = 3;

            readonly string text;
            readonly List<JsonNode> stack = new List<JsonNode>();
            readonly StringBuilder sb = new StringBuilder();
            int i, n, line, lineStart;
            public readonly int Start;
            public string Error;
            public int ErrLine, ErrCol;

            public JsonReader(string text)
            {
                this.text = text;
                Start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
                Reset(Start, text.Length, 1);
            }

            // Reads text[from..to) as one document whose first character sits on line atLine.
            public void Reset(int from, int to, int atLine)
            {
                i = from;
                n = to;
                line = atLine;
                lineStart = from;
                stack.Clear();
                Error = null;
                ErrLine = 0;
                ErrCol = 0;
            }

            JsonNode Fail(string message, int atLine, int atCol)
            {
                Error = message;
                ErrLine = atLine;
                ErrCol = atCol;
                return null;
            }

            JsonNode Here(string message)
            {
                return Fail(message, line, i - lineStart + 1);
            }

            JsonNode FailAt(string message, int pos)
            {
                return Fail(message, line, pos - lineStart + 1);
            }

            bool IsComment()
            {
                return text[i] == '/' && i + 1 < n && (text[i + 1] == '/' || text[i + 1] == '*');
            }

            void SkipWs()
            {
                while (i < n)
                {
                    char c = text[i];
                    if (c == ' ' || c == '\t') i++;
                    else if (c == '\n') { i++; line++; lineStart = i; }
                    else if (c == '\r')
                    {
                        i++;
                        if (i < n && text[i] == '\n') i++;
                        line++;
                        lineStart = i;
                    }
                    else break;
                }
            }

            static JsonNode Make(JsonKind kind, string value, JsonNode parent, string key, int atLine, int atCol)
            {
                JsonNode node = new JsonNode();
                node.Kind = kind;
                node.Value = value;
                node.Line = atLine;
                node.Col = atCol;
                node.EndLine = atLine;
                if (kind == JsonKind.Object || kind == JsonKind.Array) node.Children = new List<JsonNode>();
                if (parent != null)
                {
                    node.Parent = parent;
                    if (parent.Kind == JsonKind.Object) node.Key = key;
                    else node.Index = parent.Children.Count;
                    parent.Children.Add(node);
                }
                return node;
            }

            static string Extra(string close)
            {
                return "Coma de m\u00E1s antes de \u00AB" + close + "\u00BB";
            }

            // Reads the string that starts at the quote under i.
            bool ReadString(out string result)
            {
                result = null;
                int sLine = line, sCol = i - lineStart + 1;
                i++;
                sb.Length = 0;
                while (true)
                {
                    int run = i;
                    while (i < n)
                    {
                        char d = text[i];
                        if (d == '"' || d == '\\' || d < ' ') break;
                        i++;
                    }
                    if (i > run) sb.Append(text, run, i - run);
                    if (i >= n) { Fail("Cadena sin cerrar", sLine, sCol); return false; }
                    char c = text[i];
                    if (c == '"') { i++; result = sb.ToString(); return true; }
                    if (c < ' ')
                    {
                        Here(c == '\n' || c == '\r' ? "Salto de l\u00EDnea dentro de una cadena" : "Car\u00E1cter de control sin escapar en una cadena");
                        return false;
                    }
                    int escCol = i - lineStart + 1;
                    i++;
                    if (i >= n) { Fail("Cadena sin cerrar", sLine, sCol); return false; }
                    char x = text[i];
                    switch (x)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 >= n) { Fail("Escape \\u no v\u00E1lido", line, escCol); return false; }
                            int code = 0;
                            for (int k = 1; k <= 4; k++)
                            {
                                int h = Hex(text[i + k]);
                                if (h < 0) { Fail("Escape \\u no v\u00E1lido", line, escCol); return false; }
                                code = code * 16 + h;
                            }
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default:
                            Fail("Escape no v\u00E1lido en una cadena", line, escCol);
                            return false;
                    }
                    i++;
                }
            }

            static int Hex(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            static bool IsDigit(char c) { return c >= '0' && c <= '9'; }

            bool ReadNumber(out string value)
            {
                value = null;
                int p = i;
                if (text[p] == '-') p++;
                if (p >= n || !IsDigit(text[p])) { FailAt("N\u00FAmero no v\u00E1lido", p); return false; }
                if (text[p] == '0')
                {
                    p++;
                    if (p < n && IsDigit(text[p])) { FailAt("Un n\u00FAmero no puede empezar por 0", i); return false; }
                }
                else
                {
                    while (p < n && IsDigit(text[p])) p++;
                }
                if (p < n && text[p] == '.')
                {
                    p++;
                    if (p >= n || !IsDigit(text[p])) { FailAt("Faltan d\u00EDgitos despu\u00E9s del punto decimal", p); return false; }
                    while (p < n && IsDigit(text[p])) p++;
                }
                if (p < n && (text[p] == 'e' || text[p] == 'E'))
                {
                    p++;
                    if (p < n && (text[p] == '+' || text[p] == '-')) p++;
                    if (p >= n || !IsDigit(text[p])) { FailAt("Faltan d\u00EDgitos en el exponente", p); return false; }
                    while (p < n && IsDigit(text[p])) p++;
                }
                value = text.Substring(i, p - i);
                i = p;
                return true;
            }

            bool Literal(string word)
            {
                if (i + word.Length <= n && string.CompareOrdinal(text, i, word, 0, word.Length) == 0)
                {
                    i += word.Length;
                    return true;
                }
                Here("Valor no v\u00E1lido");
                return false;
            }

            public JsonNode Run()
            {
                JsonNode root = null;
                int st = StValue;
                bool first = false;     // right after an opening bracket
                string key = null;
                int commaLine = 0, commaCol = 0;
                while (true)
                {
                    SkipWs();
                    JsonNode top = stack.Count > 0 ? stack[stack.Count - 1] : null;
                    if (st == StAfter && top == null)
                    {
                        if (i < n) return Here(IsComment() ? MsgComment : "Hay texto de m\u00E1s despu\u00E9s del valor");
                        return root;
                    }
                    if (i >= n)
                    {
                        if (top == null) return Here("No hay ning\u00FAn valor JSON");
                        return Fail("Falta cerrar \u00AB" + (top.Kind == JsonKind.Object ? "{" : "[") + "\u00BB abierto aqu\u00ED", top.Line, top.Col);
                    }
                    char c = text[i];

                    if (st == StKey)
                    {
                        if (c == '}')
                        {
                            if (!first) return Fail(Extra("}"), commaLine, commaCol);
                            stack.RemoveAt(stack.Count - 1);
                            top.EndLine = line;
                            i++;
                            st = StAfter;
                            continue;
                        }
                        if (c == '"')
                        {
                            string k;
                            if (!ReadString(out k)) return null;
                            key = k;
                            st = StColon;
                            continue;
                        }
                        return Here(IsComment() ? MsgComment : "Los nombres van entre comillas dobles");
                    }

                    if (st == StColon)
                    {
                        if (c == ':') { i++; st = StValue; continue; }
                        return Here(IsComment() ? MsgComment : "Falta \u00AB:\u00BB despu\u00E9s del nombre");
                    }

                    if (st == StAfter)
                    {
                        if (c == ',')
                        {
                            commaLine = line;
                            commaCol = i - lineStart + 1;
                            i++;
                            first = false;
                            st = top.Kind == JsonKind.Object ? StKey : StValue;
                            continue;
                        }
                        if (c == '}' || c == ']')
                        {
                            JsonKind want = c == '}' ? JsonKind.Object : JsonKind.Array;
                            if (top.Kind != want) return Here("Aqu\u00ED deber\u00EDa haber \u00AB" + (top.Kind == JsonKind.Object ? "}" : "]") + "\u00BB");
                            stack.RemoveAt(stack.Count - 1);
                            top.EndLine = line;
                            i++;
                            continue;
                        }
                        return Here(IsComment() ? MsgComment : "Falta una coma o un cierre");
                    }

                    // A value is expected.
                    if (c == ']' && top != null && top.Kind == JsonKind.Array)
                    {
                        if (!first) return Fail(Extra("]"), commaLine, commaCol);
                        stack.RemoveAt(stack.Count - 1);
                        top.EndLine = line;
                        i++;
                        st = StAfter;
                        continue;
                    }
                    int vLine = line, vCol = i - lineStart + 1;
                    if (c == '{' || c == '[')
                    {
                        JsonNode box = Make(c == '{' ? JsonKind.Object : JsonKind.Array, null, top, key, vLine, vCol);
                        if (top == null) root = box;
                        stack.Add(box);
                        i++;
                        first = true;
                        key = null;
                        st = c == '{' ? StKey : StValue;
                        continue;
                    }
                    JsonNode leaf;
                    if (c == '"')
                    {
                        string sv;
                        if (!ReadString(out sv)) return null;
                        leaf = Make(JsonKind.String, sv, top, key, vLine, vCol);
                    }
                    else if (c == '-' || IsDigit(c))
                    {
                        string nv;
                        if (!ReadNumber(out nv)) return null;
                        leaf = Make(JsonKind.Number, nv, top, key, vLine, vCol);
                    }
                    else if (c == 't')
                    {
                        if (!Literal("true")) return null;
                        leaf = Make(JsonKind.True, "true", top, key, vLine, vCol);
                    }
                    else if (c == 'f')
                    {
                        if (!Literal("false")) return null;
                        leaf = Make(JsonKind.False, "false", top, key, vLine, vCol);
                    }
                    else if (c == 'n')
                    {
                        if (!Literal("null")) return null;
                        leaf = Make(JsonKind.Null, "null", top, key, vLine, vCol);
                    }
                    else if (c == ',' || c == ':' || c == '}' || c == ']')
                    {
                        return Here("Falta un valor antes de \u00AB" + c + "\u00BB");
                    }
                    else if (c == '\'')
                    {
                        return Here("Las cadenas van entre comillas dobles");
                    }
                    else
                    {
                        return Here(IsComment() ? MsgComment : "Valor no v\u00E1lido");
                    }
                    if (top == null) root = leaf;
                    key = null;
                    st = StAfter;
                }
            }
        }

        // ---- Writing

        // Members and items one per line, ": " after names, empty {} [] kept on one line. Strings are escaped minimally,
        // numbers stay as written.
        public static string Pretty(JsonNode root, int indent)
        {
            return Write(root, Math.Max(0, Math.Min(indent, 16)), true);
        }

        public static string Compact(JsonNode root)
        {
            return Write(root, 0, false);
        }

        static string Write(JsonNode root, int indent, bool pretty)
        {
            if (root == null) return "";
            StringBuilder sb = new StringBuilder();
            List<JsonNode> st = new List<JsonNode>();
            List<int> at = new List<int>();
            if (Open(sb, root)) { st.Add(root); at.Add(0); }
            while (st.Count > 0)
            {
                int top = st.Count - 1;
                JsonNode n = st[top];
                int k = at[top];
                if (k >= n.Children.Count)
                {
                    st.RemoveAt(top);
                    at.RemoveAt(top);
                    if (pretty) NewLine(sb, st.Count, indent);
                    sb.Append(n.Kind == JsonKind.Object ? '}' : ']');
                    continue;
                }
                at[top] = k + 1;
                if (k > 0) sb.Append(',');
                if (pretty) NewLine(sb, st.Count, indent);
                JsonNode child = n.Children[k];
                if (n.Kind == JsonKind.Object)
                {
                    AppendString(sb, child.Key ?? "");
                    sb.Append(pretty ? ": " : ":");
                }
                if (Open(sb, child)) { st.Add(child); at.Add(0); }
            }
            return sb.ToString();
        }

        // Writes a value in full, except a non-empty container: only its opening bracket is written and true returned.
        static bool Open(StringBuilder sb, JsonNode n)
        {
            if (n.Kind == JsonKind.Object || n.Kind == JsonKind.Array)
            {
                bool obj = n.Kind == JsonKind.Object;
                if (n.Children == null || n.Children.Count == 0)
                {
                    sb.Append(obj ? "{}" : "[]");
                    return false;
                }
                sb.Append(obj ? '{' : '[');
                return true;
            }
            if (n.Kind == JsonKind.String) AppendString(sb, n.Value);
            else if (n.Kind == JsonKind.Number) sb.Append(string.IsNullOrEmpty(n.Value) ? "0" : n.Value);
            else if (n.Kind == JsonKind.True) sb.Append("true");
            else if (n.Kind == JsonKind.False) sb.Append("false");
            else sb.Append("null");
            return false;
        }

        static void NewLine(StringBuilder sb, int depth, int indent)
        {
            sb.Append('\n');
            sb.Append(' ', Math.Min(depth, MaxIndentDepth) * indent);
        }

        static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                int run = 0;
                for (int k = 0; k < s.Length; k++)
                {
                    char c = s[k];
                    if (c >= ' ' && c != '"' && c != '\\') continue;
                    sb.Append(s, run, k - run);
                    run = k + 1;
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default: sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); break;
                    }
                }
                sb.Append(s, run, s.Length - run);
            }
            sb.Append('"');
        }

        // ---- Describing

        // $ for the root, $.name, $["odd name"], $.items[3].id
        public static string PathOf(JsonNode n)
        {
            if (n == null) return "";
            List<JsonNode> chain = new List<JsonNode>();
            for (JsonNode p = n; p != null; p = p.Parent) chain.Add(p);
            StringBuilder sb = new StringBuilder("$");
            for (int k = chain.Count - 2; k >= 0; k--)
            {
                JsonNode c = chain[k], parent = chain[k + 1];
                if (parent.Kind == JsonKind.Array)
                {
                    int idx = c.Index >= 0 ? c.Index : parent.Children.IndexOf(c);
                    sb.Append('[').Append(idx.ToString(CultureInfo.InvariantCulture)).Append(']');
                    continue;
                }
                string key = c.Key ?? "";
                if (IsPlainName(key)) sb.Append('.').Append(key);
                else
                {
                    sb.Append('[');
                    AppendString(sb, key);
                    sb.Append(']');
                }
            }
            return sb.ToString();
        }

        static bool IsPlainName(string s)
        {
            if (s.Length == 0) return false;
            for (int k = 0; k < s.Length; k++)
            {
                char c = s[k];
                bool letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '$';
                if (letter || (k > 0 && c >= '0' && c <= '9')) continue;
                return false;
            }
            return true;
        }

        // "{ 3 }" / "[ 12 ]" for containers, the value itself otherwise (strings quoted, cut at 80 characters).
        public static string Summary(JsonNode n)
        {
            if (n == null) return "";
            if (n.Kind == JsonKind.Object || n.Kind == JsonKind.Array)
            {
                string count = (n.Children == null ? 0 : n.Children.Count).ToString(CultureInfo.InvariantCulture);
                return n.Kind == JsonKind.Object ? "{ " + count + " }" : "[ " + count + " ]";
            }
            if (n.Kind == JsonKind.String)
            {
                string v = n.Value ?? "";
                int cut = CutAt(v);
                StringBuilder sb = new StringBuilder();
                AppendString(sb, v.Substring(0, cut));
                if (cut < v.Length) sb.Insert(sb.Length - 1, '\u2026');
                return sb.ToString();
            }
            string lit = n.Kind == JsonKind.True ? "true" : n.Kind == JsonKind.False ? "false" : n.Kind == JsonKind.Null ? "null" : (n.Value ?? "");
            int at = CutAt(lit);
            return at < lit.Length ? lit.Substring(0, at) + "\u2026" : lit;
        }

        static int CutAt(string v)
        {
            if (v.Length <= 80) return v.Length;
            return char.IsHighSurrogate(v[79]) ? 79 : 80;
        }

        public static int Count(JsonNode root)
        {
            if (root == null) return 0;
            int total = 0;
            List<JsonNode> pending = new List<JsonNode>();
            pending.Add(root);
            while (pending.Count > 0)
            {
                JsonNode n = pending[pending.Count - 1];
                pending.RemoveAt(pending.Count - 1);
                total++;
                if (n.Children == null) continue;
                for (int k = 0; k < n.Children.Count; k++) pending.Add(n.Children[k]);
            }
            return total;
        }
    }

    // ---- Logs

    public enum LogLevel { None, Trace, Debug, Info, Warn, Error, Fatal }

    public static class LogLines
    {
        const int LevelWindow = 160;
        static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        // The level a line shows, or None (also for stack trace continuations).
        public static LogLevel LevelOf(string line)
        {
            if (string.IsNullOrEmpty(line) || Continuation(line)) return LogLevel.None;
            LogLevel a = Android(line);
            return a != LogLevel.None ? a : ByWord(line);
        }

        // "   at Foo.Bar()", "\tat com.x", "Caused by:", "... 23 more", Python's '  File "x.py"'.
        public static bool Continuation(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            int k = 0;
            while (k < line.Length && (line[k] == ' ' || line[k] == '\t')) k++;
            if (k >= line.Length) return false;
            if (string.CompareOrdinal(line, k, "Caused by:", 0, 10) == 0) return true;
            if (k == 0) return false;
            if (string.CompareOrdinal(line, k, "Suppressed:", 0, 11) == 0) return true;
            if (string.CompareOrdinal(line, k, "File \"", 0, 6) == 0) return true;
            if (string.CompareOrdinal(line, k, "... ", 0, 4) == 0) return k + 4 < line.Length && IsDigit(line[k + 4]);
            if (string.CompareOrdinal(line, k, "at ", 0, 3) != 0) return false;
            // "at least one" is prose; a frame has a dotted, parenthesised or path-like name after "at ".
            int e = k + 3;
            while (e < line.Length && line[e] != ' ' && line[e] != '\t') e++;
            for (int m = k + 3; m < e; m++)
                if (".(<$/\\".IndexOf(line[m]) >= 0) return true;
            return false;
        }

        // At least 40% of the first 200 non-blank lines start with a timestamp, or 30% show a level. Stack trace
        // continuations belong to the line before, so they are left out of the percentages.
        public static bool LooksLikeLog(IList<string> lines)
        {
            if (lines == null) return false;
            int seen = 0, counted = 0, stamped = 0, leveled = 0;
            for (int k = 0; k < lines.Count && seen < 200; k++)
            {
                string l = lines[k];
                if (string.IsNullOrWhiteSpace(l)) continue;
                seen++;
                if (Continuation(l)) continue;
                counted++;
                if (StampLength(l) > 0) stamped++;
                if (LevelOf(l) != LogLevel.None) leveled++;
            }
            if (counted == 0) return false;
            return stamped * 100 >= counted * 40 || leveled * 100 >= counted * 30;
        }

        // ---- Levels

        static LogLevel ByWord(string line)
        {
            int limit = Math.Min(line.Length, LevelWindow);
            int i = 0;
            while (i < limit)
            {
                char c = line[i];
                if (!WordChar(c)) { i++; continue; }
                int j = i + 1;
                while (j < line.Length && WordChar(line[j])) j++;
                int len = j - i;
                if (len >= 3 && len <= 12 && char.IsLetter(c))
                {
                    string tok = line.Substring(i, len);
                    bool shortForm;
                    LogLevel lv = WordLevel(tok.ToUpperInvariant(), out shortForm);
                    if (lv != LogLevel.None && Accept(line, i, j, tok, shortForm)) return lv;
                }
                i = j;
            }
            return LogLevel.None;
        }

        static bool WordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // A level word counts when it is upper case, or delimited ([warn], |info|, error:, level=debug); a short form
        // (ERR, WRN...) also with just one delimiter. Words inside paths (bin/DEBUG) do not count.
        static bool Accept(string line, int i, int j, string tok, bool shortForm)
        {
            char prev = i > 0 ? line[i - 1] : '\0';
            char next = j < line.Length ? line[j] : '\0';
            if (prev == '/' || prev == '\\' || prev == '.' || prev == '$') return false;
            if (tok == tok.ToUpperInvariant()) return true;
            bool open = prev == '[' || prev == '|' || prev == '(' || prev == '<' || prev == '"';
            bool close = next == ']' || next == '|' || next == ')' || next == '>' || next == ':' || next == '"';
            if (open && close) return true;
            if (next == ':' || prev == '=') return true;
            return shortForm && (open || close);
        }

        static LogLevel WordLevel(string up, out bool shortForm)
        {
            shortForm = false;
            switch (up)
            {
                case "ERROR":
                case "SEVERE":
                    return LogLevel.Error;
                case "ERR":
                    shortForm = true;
                    return LogLevel.Error;
                case "FATAL":
                case "CRITICAL":
                case "CRIT":
                    return LogLevel.Fatal;
                case "FTL":
                    shortForm = true;
                    return LogLevel.Fatal;
                case "WARN":
                case "WARNING":
                case "AVISO":
                case "ADVERTENCIA":
                    return LogLevel.Warn;
                case "WRN":
                    shortForm = true;
                    return LogLevel.Warn;
                case "INFO":
                case "INFORMATION":
                case "NOTICE":
                case "INFORMACI\u00D3N":
                case "INFORMACION":
                    return LogLevel.Info;
                case "INF":
                    shortForm = true;
                    return LogLevel.Info;
                case "DEBUG":
                case "DEPURACI\u00D3N":
                case "DEPURACION":
                    return LogLevel.Debug;
                case "DBG":
                    shortForm = true;
                    return LogLevel.Debug;
                case "TRACE":
                case "VERBOSE":
                    return LogLevel.Trace;
                case "TRC":
                case "VRB":
                    shortForm = true;
                    return LogLevel.Trace;
            }
            return LogLevel.None;
        }

        // Logcat: "10-20 12:00:00.123  1234  5678 E Tag: msg", "E/Tag(1234): msg", "12:00:00 W/Tag: msg".
        static LogLevel Android(string line)
        {
            int p = AndroidStamp(line);
            if (p > 0)
            {
                for (int guard = 0; guard < 4; guard++)
                {
                    while (p < line.Length && line[p] == ' ') p++;
                    int q = p;
                    while (q < line.Length && line[q] != ' ' && line[q] != '/') q++;
                    if (q == p) break;
                    if (q - p == 1 && AndroidLetter(line[p]) && (q >= line.Length || line[q] == ' ' || line[q] == '/')) return AndroidLevel(line[p]);
                    if (!PidLike(line, p, q)) break;
                    p = q;
                }
            }
            int limit = Math.Min(line.Length, 80);
            for (int k = 0; k + 1 < limit; k++)
            {
                if (line[k + 1] != '/' || !AndroidLetter(line[k]) || (k > 0 && line[k - 1] != ' ')) continue;
                int e = k + 2;
                while (e < line.Length && (char.IsLetterOrDigit(line[e]) || line[e] == '_' || line[e] == '.' || line[e] == '-' || line[e] == '$')) e++;
                // "I/O error" is not a logcat tag: it needs two characters and then '(' or ':'.
                if (e - (k + 2) >= 2 && e < line.Length && (line[e] == '(' || line[e] == ':')) return AndroidLevel(line[k]);
            }
            return LogLevel.None;
        }

        static bool AndroidLetter(char c)
        {
            return "VDIWEFA".IndexOf(c) >= 0;
        }

        static LogLevel AndroidLevel(char c)
        {
            switch (c)
            {
                case 'V': return LogLevel.Trace;
                case 'D': return LogLevel.Debug;
                case 'I': return LogLevel.Info;
                case 'W': return LogLevel.Warn;
                case 'E': return LogLevel.Error;
                default: return LogLevel.Fatal;
            }
        }

        static bool PidLike(string s, int from, int to)
        {
            if (to - from >= 2 && s[from] == 'u' && IsDigit(s[from + 1])) return true;
            for (int k = from; k < to; k++)
                if (!IsDigit(s[k])) return false;
            return true;
        }

        // "MM-DD HH:MM:SS.mmm" at the start: its length, else 0.
        static int AndroidStamp(string s)
        {
            if (s.Length < 18) return 0;
            if (s[2] != '-' || s[5] != ' ' || s[8] != ':' || s[11] != ':' || s[14] != '.') return 0;
            int[] digits = { 0, 1, 3, 4, 6, 7, 9, 10, 12, 13, 15, 16, 17 };
            for (int k = 0; k < digits.Length; k++)
                if (!IsDigit(s[digits[k]])) return 0;
            return 18;
        }

        // ---- Timestamps

        // Where the timestamp at the start of a line ends (0 when none). A leading '[' and its closing ']' are included.
        public static int StampLength(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int p = line[0] == '[' ? 1 : 0;
            int e = StampAt(line, p);
            if (e < 0) return 0;
            if (p == 1 && e < line.Length && line[e] == ']') e++;
            return e;
        }

        static int StampAt(string s, int p)
        {
            int e = DateTimeAt(s, p);
            if (e >= 0) return e;
            e = MonthDayAt(s, p);
            if (e >= 0) return e;
            e = SyslogAt(s, p);
            if (e >= 0) return e;
            e = TimeAt(s, p);
            if (e >= 0) return e;
            return EpochAt(s, p);
        }

        static int Digits(string s, int p, int max)
        {
            int c = 0;
            while (c < max && p + c < s.Length && IsDigit(s[p + c])) c++;
            return c;
        }

        static bool IsDigit(char c) { return c >= '0' && c <= '9'; }

        static int Skip(string s, int p, char c)
        {
            while (p < s.Length && s[p] == c) p++;
            return p;
        }

        // hh:mm:ss with an optional fraction and zone; the index after it, or -1.
        static int TimeAt(string s, int p)
        {
            if (Digits(s, p, 2) != 2 || p + 2 >= s.Length || s[p + 2] != ':') return -1;
            if (Digits(s, p + 3, 2) != 2 || p + 5 >= s.Length || s[p + 5] != ':') return -1;
            if (Digits(s, p + 6, 2) != 2) return -1;
            int hh = (s[p] - '0') * 10 + (s[p + 1] - '0');
            int mm = (s[p + 3] - '0') * 10 + (s[p + 4] - '0');
            int ss = (s[p + 6] - '0') * 10 + (s[p + 7] - '0');
            if (hh > 23 || mm > 59 || ss > 60) return -1;
            int e = p + 8;
            if (e < s.Length && (s[e] == '.' || s[e] == ','))
            {
                int d = Digits(s, e + 1, 9);
                if (d > 0) e += 1 + d;
            }
            if (e < s.Length)
            {
                char z = s[e];
                if (z == 'Z' && (e + 1 >= s.Length || !char.IsLetterOrDigit(s[e + 1]))) e++;
                else if ((z == '+' || z == '-') && Digits(s, e + 1, 2) == 2)
                {
                    int f = e + 3;
                    if (f < s.Length && s[f] == ':' && Digits(s, f + 1, 2) == 2) f += 3;
                    else if (Digits(s, f, 2) == 2) f += 2;
                    e = f;
                }
            }
            return e;
        }

        // 2026-10-20 12:00:00, 2026-10-20T12:00:00Z, 20/10/2026 12:00:00, 20/10/2026, 12:00:00
        static int DateTimeAt(string s, int p)
        {
            int a = Digits(s, p, 4);
            if (a != 1 && a != 2 && a != 4) return -1;
            int q = p + a;
            if (q >= s.Length) return -1;
            char sep = s[q];
            if (sep != '-' && sep != '/' && sep != '.') return -1;
            int b = Digits(s, q + 1, 2);
            if (b == 0) return -1;
            int q2 = q + 1 + b;
            if (q2 >= s.Length || s[q2] != sep) return -1;
            int c = Digits(s, q2 + 1, 4);
            if (a == 4 ? (c < 1 || c > 2) : c != 4) return -1;
            int r = q2 + 1 + c;
            if (r < s.Length && s[r] == 'T') r++;
            else
            {
                if (r < s.Length && s[r] == ',') r++;
                int sp = r;
                r = Skip(s, r, ' ');
                if (r == sp) return -1;
            }
            return TimeAt(s, r);
        }

        // Android: 10-20 12:00:00.123
        static int MonthDayAt(string s, int p)
        {
            if (Digits(s, p, 2) != 2 || p + 2 >= s.Length || s[p + 2] != '-' || Digits(s, p + 3, 2) != 2) return -1;
            int r = p + 5;
            int sp = r;
            r = Skip(s, r, ' ');
            if (r == sp) return -1;
            return TimeAt(s, r);
        }

        // syslog: Oct 20 12:00:00
        static int SyslogAt(string s, int p)
        {
            if (p + 3 > s.Length) return -1;
            bool month = false;
            for (int k = 0; k < Months.Length && !month; k++)
                month = string.Compare(s, p, Months[k], 0, 3, StringComparison.OrdinalIgnoreCase) == 0;
            if (!month) return -1;
            int r = p + 3;
            int sp = r;
            r = Skip(s, r, ' ');
            if (r == sp) return -1;
            int d = Digits(s, r, 2);
            if (d == 0) return -1;
            r += d;
            sp = r;
            r = Skip(s, r, ' ');
            if (r == sp) return -1;
            return TimeAt(s, r);
        }

        // 10 to 13 digits (seconds or milliseconds since 1970), optionally with a fraction.
        static int EpochAt(string s, int p)
        {
            int d = Digits(s, p, 14);
            if (d < 10 || d > 13) return -1;
            int e = p + d;
            if (e < s.Length && s[e] == '.')
            {
                int f = Digits(s, e + 1, 9);
                if (f > 0) e += 1 + f;
            }
            if (e < s.Length && " \t,|]:".IndexOf(s[e]) < 0) return -1;
            return e;
        }
    }
}
