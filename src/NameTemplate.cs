// Stackshot - File names of the captures, from a template: {tipo} {fecha} {hora} {app} {ventana} {año} {mes} {dia} {n}.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    // The default template gives exactly the names Stackshot always gave ("Captura 2026-10-12 17.03.26"). Any other one is
    // filled in and made safe for Windows: forbidden characters become "_", spaces are collapsed, the end loses its dots
    // and spaces, it is cut to 120 characters, device names (CON, NUL, COM1...) are avoided and it is never empty.
    public static class NameTemplate
    {
        public const int MaxLength = 120;   // characters before the extension

        // The tokens Ajustes offers, in its order.
        public static string[] Tokens
        {
            get
            {
                return I18n.IsSpanish
                    ? new string[] { "{tipo}", "{fecha}", "{hora}", "{app}", "{ventana}", "{a\u00F1o}", "{mes}", "{dia}", "{n}" }
                    : new string[] { "{type}", "{date}", "{time}", "{app}", "{window}", "{year}", "{month}", "{day}", "{n}" };
            }
        }

        static readonly string[] Devices = { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
                                             "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM\u00B9", "COM\u00B2", "COM\u00B3",
                                             "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3" };
        const char CounterMark = '\uE000';   // stands for {n} while looking for the numbers already used

        // What a name is made from.
        public sealed class Values
        {
            public string Type = "";        // what was captured: Captura, Pantalla, the window's program, Desplazamiento...
            public string App = "";         // program of the window in front when the capture started ("" if unknown)
            public string Window = "";      // that window's title
            public DateTime When = DateTime.Now;
        }

        // The default as Ajustes shows it, in the interface's language; both spellings are the default.
        public const string DefaultEnglish = "{type} {date} {time}";
        public static string DefaultShown { get { return I18n.IsSpanish ? Settings.DefaultNameTemplate : DefaultEnglish; } }

        // Empty or the default (however it is spaced around, in either language): today's names.
        public static bool IsDefault(string template)
        {
            if (template == null || template.Trim().Length == 0) return true;
            string t = template.Trim();
            return t == Settings.DefaultNameTemplate || t == DefaultEnglish;
        }

        // True when the template has that token ("{n}"), in any case.
        public static bool Uses(string template, string token)
        {
            if (IsDefault(template)) return false;
            if (template.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{hora}" && template.IndexOf("{time}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{fecha}" && template.IndexOf("{date}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{tipo}" && template.IndexOf("{type}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{ventana}" && template.IndexOf("{window}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{a\u00F1o}" && template.IndexOf("{year}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (token == "{mes}" && template.IndexOf("{month}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if ((token == "{dia}" || token == "{d\u00EDa}") && template.IndexOf("{day}", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool UsesCounter(string template) { return Uses(template, "{n}"); }

        // The name without extension; counter is what {n} becomes.
        public static string Format(string template, Values v, long counter)
        {
            if (IsDefault(template)) return Legacy(v);
            string name = Clean(Expand(template, v, counter.ToString(CultureInfo.InvariantCulture)));
            return name.Length > 0 ? name : Legacy(v);
        }

        // The names Stackshot has always given (the default template): the type cut to 40 characters, then the time.
        static string Legacy(Values v)
        {
            return Type(v.Type) + " " + v.When.ToString("yyyy-MM-dd HH.mm.ss");
        }

        // As ShotStack.Clean: forbidden characters out, at most 40.
        static string Type(string name)
        {
            name = name ?? "";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length > 40 ? name.Substring(0, 40) : name;
        }

        // Known tokens replaced (any case); anything else stays as typed, braces included.
        static string Expand(string t, Values v, string counter)
        {
            StringBuilder b = new StringBuilder(t.Length + 40);
            int i = 0;
            while (i < t.Length)
            {
                if (t[i] == '{')
                {
                    int end = t.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        string value = Token(t.Substring(i + 1, end - i - 1), v, counter);
                        if (value != null)
                        {
                            b.Append(value);
                            i = end + 1;
                            continue;
                        }
                    }
                }
                b.Append(t[i]);
                i++;
            }
            return b.ToString();
        }

        static string Token(string name, Values v, string counter)
        {
            switch (name.ToLowerInvariant())
            {
                case "tipo": case "type":
                    string type = Type(v.Type);
                    return type.Length == 40 && char.IsHighSurrogate(type[39]) ? type.Substring(0, 39) : type;
                case "fecha": case "date": return v.When.ToString("yyyy-MM-dd");
                case "hora": case "time": return v.When.ToString("HH.mm.ss");
                case "app": return v.App ?? "";
                case "ventana": case "window": return v.Window ?? "";
                case "a\u00F1o": case "year": return v.When.ToString("yyyy");
                case "mes": case "month": return v.When.ToString("MM");
                case "dia": case "d\u00EDa": case "day": return v.When.ToString("dd");
                case "n": return counter;
                default: return null;
            }
        }

        // A name Windows accepts as it is (or "" when nothing is left).
        internal static string Clean(string s)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            StringBuilder b = new StringBuilder(s.Length);
            bool space = false;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) { space = b.Length > 0; continue; }
                if (space) { b.Append(' '); space = false; }
                b.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            }
            string r = Cut(b.ToString()).TrimEnd(' ', '.');
            if (r.StartsWith("~", StringComparison.Ordinal)) r = "_" + r.Substring(1); // "~" marks the recorder's work files
            r = AvoidDevice(r);
            if (r.Length > MaxLength) r = Cut(r).TrimEnd(' ', '.');
            return r;
        }

        // At most MaxLength UTF-16 units, never half of a surrogate pair.
        static string Cut(string s)
        {
            if (s.Length <= MaxLength) return s;
            return s.Substring(0, char.IsHighSurrogate(s[MaxLength - 1]) ? MaxLength - 1 : MaxLength);
        }

        // "CON", "nul.algo", "COM1 .x": Windows takes the part before the first dot as a device, so it gets a "_".
        static string AvoidDevice(string name)
        {
            int dot = name.IndexOf('.');
            string head = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ');
            foreach (string d in Devices)
                if (string.Equals(head, d, StringComparison.OrdinalIgnoreCase)) return head + "_" + name.Substring(head.Length);
            return name;
        }

        // The first value of {n} worth trying: one past the highest number a name of the same shape already has in any of
        // the folders (the captures of that day in the temporary folder and where kept ones go), or 1. Only names that
        // differ in that number count, so with {fecha} the count starts again every day.
        public static long FirstCounter(string template, Values v, string ext, params string[] folders)
        {
            if (!UsesCounter(template)) return 1;
            string marked = Clean(Expand(template, v, CounterMark.ToString()));
            int at = marked.IndexOf(CounterMark);
            if (at < 0 || marked.IndexOf(CounterMark, at + 1) >= 0) return 1; // cut off, or used twice: the caller probes
            string before = marked.Substring(0, at), after = marked.Substring(at + 1);
            Regex shape = new Regex("^" + Regex.Escape(before) + "([0-9]{1,15})" + Regex.Escape(after + ext) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            long most = 0;
            foreach (string dir in folders)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    // The pattern lets Windows filter: only names of this shape are listed, however full the folder.
                    foreach (string f in Directory.EnumerateFiles(dir, before + "*" + after + ext))
                    {
                        Match m = shape.Match(Path.GetFileName(f));
                        long n;
                        if (m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n > most) most = n;
                    }
                }
                catch (Exception ex) { ShotStack.Log("Nombre de captura (" + dir + "): " + ex.Message); }
            }
            return most + 1;
        }

        // An example for Ajustes, with made-up values and the current time.
        public static string Example(string template, DateTime when)
        {
            Values v = new Values();
            v.Type = I18n.T("Captura", "Capture");
            v.App = "EXCEL";
            v.Window = I18n.T("Informe de ventas.xlsx - Excel", "Sales Report.xlsx - Excel");
            v.When = when;
            return Format(template, v, 1);
        }

        // Tokens that look like one but aren't known ("{fehca}"), for the hint under the field.
        public static List<string> Unknown(string template)
        {
            List<string> r = new List<string>();
            if (template == null) return r;
            foreach (Match m in Regex.Matches(template, "\\{[^{}]*\\}"))
            {
                string inner = m.Value.Substring(1, m.Value.Length - 2);
                if (Token(inner, new Values(), "1") == null && !r.Contains(m.Value)) r.Add(m.Value);
            }
            return r;
        }
    }
}
