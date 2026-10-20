// Stackshot - Colours for the text viewer: log levels, JSON, settings files and code, in the palette of the theme.
// Light on purpose: a line is coloured as it is drawn, so it must be cheap.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Stackshot
{
    public enum TextKind { Plain, Log, Json, Settings, Code }

    public static class TextColors
    {
        static Palette P { get { return Ds.Brushes; } }

        static Color Str { get { return P.Dark ? Color.FromRgb(206, 145, 120) : Color.FromRgb(163, 21, 21); } }
        static Color Key { get { return P.Dark ? Color.FromRgb(156, 220, 254) : Color.FromRgb(4, 81, 165); } }
        static Color Num { get { return P.Dark ? Color.FromRgb(181, 206, 168) : Color.FromRgb(9, 134, 88); } }
        static Color Word { get { return P.Dark ? Color.FromRgb(86, 156, 214) : Color.FromRgb(0, 0, 255); } }
        static Color Note { get { return P.Dark ? Color.FromRgb(106, 153, 85) : Color.FromRgb(0, 128, 0); } }
        static Color Faint { get { return P.Label3; } }

        public static Color LevelColor(LogLevel l)
        {
            switch (l)
            {
                case LogLevel.Fatal: case LogLevel.Error: return P.Red;
                case LogLevel.Warn: return P.Dark ? Color.FromRgb(255, 179, 64) : Color.FromRgb(196, 110, 0);
                case LogLevel.Info: return P.Dark ? Color.FromRgb(100, 210, 255) : Color.FromRgb(0, 110, 200);
                case LogLevel.Debug: case LogLevel.Trace: return P.Label3;
            }
            return P.Label;
        }

        // A soft band behind an error line, so a long log shows where things went wrong at a glance.
        public static Color? Band(LogLevel l)
        {
            if (l == LogLevel.Error || l == LogLevel.Fatal) return Ds.WithAlpha(P.Red, P.Dark ? 0.16 : 0.08);
            if (l == LogLevel.Warn) return Ds.WithAlpha(P.Orange, P.Dark ? 0.12 : 0.07);
            return null;
        }

        public static Func<string, List<TextSpan>> For(TextKind k)
        {
            switch (k)
            {
                case TextKind.Log: return Log;
                case TextKind.Json: return Json;
                case TextKind.Settings: return Settings;
                case TextKind.Code: return Code;
            }
            return null;
        }

        // The time dimmed, the level word in its colour, the rest of an error or warning line tinted too.
        static List<TextSpan> Log(string line)
        {
            List<TextSpan> o = new List<TextSpan>();
            int stamp = LogLines.StampLength(line);
            if (stamp > 0) o.Add(new TextSpan(0, stamp, Faint, false));
            LogLevel l = LogLines.LevelOf(line);
            if (l == LogLevel.None) { if (LogLines.Continuation(line)) o.Add(new TextSpan(0, line.Length, Faint, false)); return o; }
            Color c = LevelColor(l);
            if (l == LogLevel.Debug || l == LogLevel.Trace) { o.Add(new TextSpan(stamp, line.Length - stamp, c, false)); return o; }
            int at = LevelAt(line, l);
            if (at >= 0)
            {
                int end = at;
                while (end < line.Length && (char.IsLetter(line[end]) || line[end] == '\u00D3' || line[end] == '\u00F3')) end++;
                o.Add(new TextSpan(at, end - at, c, true));
            }
            return o;
        }

        static int LevelAt(string line, LogLevel l)
        {
            string[] words;
            switch (l)
            {
                case LogLevel.Fatal: words = new string[] { "FATAL", "FTL", "CRITICAL", "CRIT" }; break;
                case LogLevel.Error: words = new string[] { "ERROR", "ERR", "SEVERE" }; break;
                case LogLevel.Warn: words = new string[] { "WARNING", "WARN", "WRN", "ADVERTENCIA", "AVISO" }; break;
                case LogLevel.Info: words = new string[] { "INFORMATION", "INFO", "INF", "NOTICE", "INFORMACI\u00D3N" }; break;
                default: words = new string[0]; break;
            }
            int lim = Math.Min(line.Length, 160);
            foreach (string w in words)
            {
                int i = line.IndexOf(w, 0, lim, StringComparison.OrdinalIgnoreCase);
                while (i >= 0)
                {
                    bool left = i == 0 || !char.IsLetter(line[i - 1]), right = i + w.Length >= line.Length || !char.IsLetter(line[i + w.Length]);
                    if (left && right) return i;
                    i = i + 1 < lim ? line.IndexOf(w, i + 1, lim - i - 1, StringComparison.OrdinalIgnoreCase) : -1;
                }
            }
            return -1;
        }

        // Keys, strings, numbers, true/false/null; braces and commas a little dimmer.
        static List<TextSpan> Json(string line)
        {
            List<TextSpan> o = new List<TextSpan>();
            int i = 0, n = Math.Min(line.Length, TextFile.LineCap);
            while (i < n)
            {
                char c = line[i];
                if (c == '"')
                {
                    int j = i + 1;
                    while (j < n && line[j] != '"') { if (line[j] == '\\') j++; j++; }
                    j = Math.Min(n, j + 1);
                    int k = j;
                    while (k < n && line[k] == ' ') k++;
                    o.Add(new TextSpan(i, j - i, k < n && line[k] == ':' ? Key : Str, false));
                    i = j;
                    continue;
                }
                if (c == '-' || char.IsDigit(c))
                {
                    int j = i + 1;
                    while (j < n && (char.IsDigit(line[j]) || line[j] == '.' || line[j] == 'e' || line[j] == 'E' || line[j] == '+' || line[j] == '-')) j++;
                    o.Add(new TextSpan(i, j - i, Num, false));
                    i = j;
                    continue;
                }
                if (char.IsLetter(c))
                {
                    int j = i;
                    while (j < n && char.IsLetter(line[j])) j++;
                    string w = line.Substring(i, j - i);
                    if (w == "true" || w == "false" || w == "null") o.Add(new TextSpan(i, j - i, Word, false));
                    i = j;
                    continue;
                }
                if (c == '{' || c == '}' || c == '[' || c == ']' || c == ',' || c == ':') o.Add(new TextSpan(i, 1, Faint, false));
                i++;
            }
            return o;
        }

        // .ini, .conf, .properties, .env, .yaml, .toml: [sections], keys, comments.
        static List<TextSpan> Settings(string line)
        {
            List<TextSpan> o = new List<TextSpan>();
            int s = 0;
            while (s < line.Length && (line[s] == ' ' || line[s] == '\t')) s++;
            if (s >= line.Length) return o;
            char c = line[s];
            if (c == '#' || c == ';' || (c == '/' && s + 1 < line.Length && line[s + 1] == '/')) { o.Add(new TextSpan(s, line.Length - s, Note, false)); return o; }
            if (c == '[') { o.Add(new TextSpan(s, line.Length - s, Key, true)); return o; }
            int eq = -1;
            for (int i = s; i < line.Length; i++) if (line[i] == '=' || (line[i] == ':' && (i + 1 >= line.Length || line[i + 1] == ' '))) { eq = i; break; }
            if (eq > s)
            {
                o.Add(new TextSpan(s, eq - s, Key, false));
                o.Add(new TextSpan(eq, 1, Faint, false));
                int v = eq + 1;
                while (v < line.Length && line[v] == ' ') v++;
                if (v < line.Length && (line[v] == '"' || line[v] == '\'')) o.Add(new TextSpan(v, line.Length - v, Str, false));
                else if (v < line.Length && (char.IsDigit(line[v]) || line[v] == '-')) o.Add(new TextSpan(v, line.Length - v, Num, false));
            }
            else if (c == '-') o.Add(new TextSpan(s, 1, Faint, false));
            return o;
        }

        static readonly HashSet<string> Words = new HashSet<string>(StringComparer.Ordinal)
        {
            "public", "private", "protected", "internal", "static", "readonly", "const", "class", "struct", "enum", "interface", "namespace", "using",
            "import", "from", "export", "default", "return", "if", "else", "for", "foreach", "while", "do", "switch", "case", "break", "continue",
            "new", "this", "base", "null", "true", "false", "void", "var", "let", "function", "def", "async", "await", "try", "catch", "finally",
            "throw", "in", "is", "as", "typeof", "int", "long", "string", "bool", "double", "float", "char", "object", "None", "True", "False",
            "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "INNER", "ON", "GROUP", "BY", "ORDER", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
            "AND", "OR", "NOT", "NULL", "CREATE", "TABLE", "select", "from", "where", "join", "and", "or", "not", "elif", "lambda", "pass", "self",
            "param", "begin", "end", "fi", "then", "echo"
        };

        // Code of most languages: comments, strings, numbers and common keywords.
        static List<TextSpan> Code(string line)
        {
            List<TextSpan> o = new List<TextSpan>();
            int i = 0, n = Math.Min(line.Length, TextFile.LineCap);
            while (i < n)
            {
                char c = line[i];
                if ((c == '/' && i + 1 < n && line[i + 1] == '/') || c == '#' || (c == '-' && i + 1 < n && line[i + 1] == '-' && (i == 0 || line[i - 1] == ' ')))
                {
                    o.Add(new TextSpan(i, n - i, Note, false));
                    break;
                }
                if (c == '/' && i + 1 < n && line[i + 1] == '*')
                {
                    int j = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    j = j < 0 ? n : j + 2;
                    o.Add(new TextSpan(i, j - i, Note, false));
                    i = j;
                    continue;
                }
                if (c == '"' || c == '\'' || c == '`')
                {
                    int j = i + 1;
                    while (j < n && line[j] != c) { if (line[j] == '\\') j++; j++; }
                    j = Math.Min(n, j + 1);
                    o.Add(new TextSpan(i, j - i, Str, false));
                    i = j;
                    continue;
                }
                if (char.IsDigit(c) && (i == 0 || !char.IsLetter(line[i - 1])))
                {
                    int j = i + 1;
                    while (j < n && (char.IsLetterOrDigit(line[j]) || line[j] == '.')) j++;
                    o.Add(new TextSpan(i, j - i, Num, false));
                    i = j;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i + 1;
                    while (j < n && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                    if (Words.Contains(line.Substring(i, j - i))) o.Add(new TextSpan(i, j - i, Word, false));
                    i = j;
                    continue;
                }
                i++;
            }
            return o;
        }

        static readonly string[] SettingsExts = { ".ini", ".cfg", ".conf", ".config", ".properties", ".env", ".yaml", ".yml", ".toml", ".reg", ".editorconfig", ".gitignore", ".gitattributes" };
        static readonly string[] CodeExts = { ".cs", ".js", ".ts", ".tsx", ".jsx", ".java", ".kt", ".py", ".rb", ".go", ".rs", ".c", ".h", ".cpp", ".hpp", ".sql", ".ps1", ".psm1", ".sh", ".bash", ".bat", ".cmd", ".php", ".swift", ".css", ".scss", ".vb", ".lua", ".r", ".dart", ".gradle" };

        // The kind of a file from its name (a .txt or a name without extension is looked at by the caller).
        public static TextKind Of(string path)
        {
            string e = (System.IO.Path.GetExtension(path) ?? "").ToLowerInvariant();
            string name = System.IO.Path.GetFileName(path ?? "").ToLowerInvariant();
            if (e == ".log" || e == ".out" || e == ".err") return TextKind.Log;
            if (e == ".json" || e == ".jsonl" || e == ".ndjson" || e == ".geojson" || e == ".har") return TextKind.Json;
            if (Array.IndexOf(SettingsExts, e) >= 0 || Array.IndexOf(SettingsExts, name) >= 0) return TextKind.Settings;
            if (Array.IndexOf(CodeExts, e) >= 0) return TextKind.Code;
            return TextKind.Plain;
        }

        public static bool IsCode(string path) { return Of(path) == TextKind.Code; }
    }
}
