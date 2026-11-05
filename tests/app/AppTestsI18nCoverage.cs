// Suite "i18n": source-level guarantees. Every I18n.T / I18n.Fmt call site reads src\*.cs at test time and must have a
// German translation (a third literal argument, or a catalog entry behind LookupGerman); text ternaries on IsSpanish
// must be 3-way arrays of equal length; string literals that read as Spanish must not reach UI sinks without I18n;
// German values keep the {n} placeholders of their English key; and no German key is registered twice.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot.Tests
{
    static class AppTestsI18nCoverage
    {
        // One C# token: a string literal (S holds its decoded value), a comment, or code.
        sealed class Tok { public string S; public bool Str; public bool Com; public int Line; }

        sealed class Site { public string File; public int Line; public string En; }

        // Each allowlist entry is "file|text|reason": a string that reads Spanish but is a name, id, file mask, or a
        // wording the app shows on purpose in that language.
        static readonly string[] UiAllowList =
        {
            // ShotStack.Language.cs: the offer of English is shown only to users whose interface is Spanish, on purpose
            // in English, and it names the settings route in their language so they can find it.
            "ShotStack.Language.cs|. Switch Stackshot to English? You can change it any time in Ajustes \u203A General \u203A Apariencia \u203A Idioma (Settings \u203A General \u203A Appearance \u203A Language).|the English offer names the route in the user's current (Spanish) interface on purpose"
        };

        const int Cap = 60;

        public static void CatalogCoversEveryCallSite()
        {
            List<Site> missing = new List<Site>(), skipped = new List<Site>();
            int covered = 0, ignored = 0;
            foreach (Source f in Sources())
            {
                List<Tok> toks = Tokenize(f.Text);
                for (int i = 0; i < toks.Count - 3; i++)
                {
                    if (toks[i].Com || toks[i].Str || toks[i].S != "I18n") continue;
                    if (toks[i + 1].S != "." || (toks[i + 2].S != "T" && toks[i + 2].S != "Fmt") || toks[i + 3].S != "(") continue;
                    bool isFmt = toks[i + 2].S == "Fmt";
                    int line = toks[i].Line, at = i + 3;
                    List<List<Tok>> args = CallArgs(toks, ref at);
                    i = at - 1;

                    string es = null, en = null, de = null;
                    bool litEs = args.Count > 0 && LiteralOf(args[0], out es);
                    bool litEn = args.Count > 1 && LiteralOf(args[1], out en);
                    bool litDe = args.Count > 2 && LiteralOf(args[2], out de);

                    if (args.Count == 2 && litEs && litEn && es.Length == 0 && en.Length == 0) { ignored++; continue; }

                    if (isFmt)
                    {
                        if (args.Count >= 2 && litEn)
                        {
                            if (I18n.LookupGerman(en, litEs ? es : null) != null) covered++;
                            else missing.Add(new Site { File = f.Rel, Line = line, En = en });
                        }
                        else skipped.Add(new Site { File = f.Rel, Line = line });
                        continue;
                    }
                    if (args.Count == 3)
                    {
                        if (litEn && litDe) covered++;   // a call with a German literal argument counts as covered
                        else skipped.Add(new Site { File = f.Rel, Line = line });
                        continue;
                    }
                    if (args.Count == 2 && litEn)
                    {
                        if (I18n.LookupGerman(en, litEs ? es : null) != null) covered++;
                        else missing.Add(new Site { File = f.Rel, Line = line, En = en });
                    }
                    else skipped.Add(new Site { File = f.Rel, Line = line });
                }
            }
            missing.Sort(delegate(Site a, Site b) { return string.CompareOrdinal(a.File + ":" + a.Line.ToString("00000"), b.File + ":" + b.Line.ToString("00000")); });
            foreach (Site s in missing) Console.WriteLine("    no german: " + s.File + ":" + s.Line + ": " + s.En);
            foreach (Site s in skipped) Console.WriteLine("    skipped: " + s.File + ":" + s.Line);
            Console.WriteLine("    i18n calls: " + covered + " covered, " + ignored + " empty placeholders ignored, " +
                              missing.Count + " without German, " + skipped.Count + " non-literal skipped");

            // Text ternaries: a 2-way IsSpanish choice leaves German out, and a 3-way Es/En/De chain must keep its
            // arrays the same length.
            List<string> ternary = new List<string>();
            int unknownArrays = 0;
            foreach (Source f in Sources()) ScanTernaries(f, ternary, ref unknownArrays);
            ternary.Sort(string.CompareOrdinal);
            foreach (string s in ternary) Console.WriteLine("    ternary: " + s);
            if (unknownArrays > 0) Console.WriteLine("    ternary: " + unknownArrays + " array branches could not be measured (not static string[])");

            if (missing.Count > 0 || ternary.Count > 0)
            {
                var sb = new StringBuilder();
                if (missing.Count > 0)
                {
                    sb.Append(missing.Count + " I18n calls have no German (first " + Math.Min(Cap, missing.Count) + "):");
                    for (int i = 0; i < missing.Count && i < Cap; i++) sb.Append("\n" + missing[i].File + ":" + missing[i].Line + ": " + missing[i].En);
                }
                if (ternary.Count > 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append(ternary.Count + " IsSpanish text ternaries left (first " + Math.Min(Cap, ternary.Count) + "):");
                    for (int i = 0; i < ternary.Count && i < Cap; i++) sb.Append("\n" + ternary[i]);
                }
                throw new InvalidOperationException(sb.ToString());
            }
        }

        sealed class Branch { public int Kind; public string Text; public int Count; public string Ident; }  // Kind: 0 literal, 1 array literal, 2 identifier, 3 other

        static void ScanTernaries(Source f, List<string> issues, ref int unknownArrays)
        {
            List<Tok> toks = Tokenize(f.Text);
            int p = 0, b = 0, k = 0;   // ( )  { }  [ ]
            for (int i = 0; i < toks.Count; i++)
            {
                Tok t = toks[i];
                if (t.Com) continue;
                if (!t.Str)
                {
                    if (t.S == "(") p++; else if (t.S == ")") p--;
                    else if (t.S == "{") b++; else if (t.S == "}") b--;
                    else if (t.S == "[") k++; else if (t.S == "]") k--;
                }
                if (t.Str || t.Com || t.S != "IsSpanish") continue;
                if (p < 1 || i < 2 || i + 1 >= toks.Count) continue;
                if (toks[i - 1].S != "." || toks[i - 2].S != "I18n" || toks[i + 1].S != "?") continue;
                int line0 = t.Line;

                int dp = p, db = b, dk = k;
                List<List<Tok>> parts = new List<List<Tok>>();
                List<Tok> cur = new List<Tok>();
                int j = i + 2;
                for (; j < toks.Count; j++)
                {
                    Tok u = toks[j];
                    if (u.Com) continue;
                    if (!u.Str)
                    {
                        if (u.S == "(") p++; else if (u.S == ")") { p--; if (p < dp) break; }
                        else if (u.S == "{") { if (b == db && p == dp && k == dk) break; b++; }
                        else if (u.S == "}") { b--; if (b < db) break; }
                        else if (u.S == "[") k++; else if (u.S == "]") k--;
                        if (p == dp && b == db && k == dk && (u.S == ";" || u.S == ",")) break;
                        if (p == dp && b == db && k == dk && u.S == ":") { parts.Add(cur); cur = new List<Tok>(); continue; }
                    }
                    cur.Add(u);
                }
                if (cur.Count > 0) parts.Add(cur);
                i = j - 1;

                Classify(parts, f, line0, issues, ref unknownArrays);
            }
        }

        static string LineOf(Source f, int line)
        {
            string[] lines = f.Text.Split('\n');
            return line >= 1 && line <= lines.Length ? lines[line - 1] : "";
        }

        static void Classify(List<List<Tok>> parts, Source f, int line, List<string> issues, ref int unknownArrays)
        {
            if (parts.Count == 2)
            {
                Branch x = BranchOf(parts[0]), y = BranchOf(parts[1]);
                if (x.Kind <= 1 || y.Kind <= 1)
                {
                    // Date/time formats ("d MMM" vs "MMM d") and culture names ("es-ES" vs "en-US") are locale logic, not
                    // text; they pass only when the same line also gives German its own branch.
                    if (x.Kind == 0 && y.Kind == 0 && LocaleLiteral(x.Text) && LocaleLiteral(y.Text))
                    {
                        if (LineOf(f, line).IndexOf("IsGerman", StringComparison.Ordinal) < 0)
                            issues.Add(f.Rel + ":" + line + ": locale format without a German branch: '" + x.Text + "' / '" + y.Text + "'");
                    }
                    else
                        issues.Add(f.Rel + ":" + line + ": 2-way IsSpanish text ternary: '" + Show(parts[0]) + "' / '" + Show(parts[1]) + "'");
                }
                return;
            }
            if (parts.Count != 3) return;
            Branch a = BranchOf(parts[0]), c = BranchOf(parts[2]);
            // The middle part is "IsGerman ? de": its branch sits after the "?".
            List<Tok> mid = parts[1];
            int q = mid.FindIndex(delegate(Tok t) { return !t.Com && t.S == "?"; });
            if (q < 0)
            {
                Branch m = BranchOf(mid);
                if (m.Kind <= 1) issues.Add(f.Rel + ":" + line + ": odd IsSpanish chain: '" + Show(parts[1]) + "'");
                return;
            }
            Branch bmid = BranchOf(mid.GetRange(q + 1, mid.Count - q - 1));
            if (a.Kind == 0 && bmid.Kind == 0 && c.Kind == 0) return;   // a 3-way Es/En/De chain: German is handled
            if (a.Kind == 0 || bmid.Kind == 0 || c.Kind == 0)
            {
                issues.Add(f.Rel + ":" + line + ": IsSpanish chain mixes a literal with arrays: '" + Show(parts[0]) + "' / '" + Show(mid) + "' / '" + Show(parts[2]) + "'");
                return;
            }
            if (a.Kind > 2 || bmid.Kind > 2 || c.Kind > 2) return;   // expressions we cannot classify: the audits cover them
            int la = a.Kind == 1 ? a.Count : ArrayLengthOf(a.Ident);
            int lb = bmid.Kind == 1 ? bmid.Count : ArrayLengthOf(bmid.Ident);
            int lc = c.Kind == 1 ? c.Count : ArrayLengthOf(c.Ident);
            if (la < 0 || lb < 0 || lc < 0) { unknownArrays++; return; }
            if (la != lb || lb != lc)
                issues.Add(f.Rel + ":" + line + ": Es/En/De arrays differ in length: " + la + " / " + lb + " / " + lc + " ('" + a.Ident + "', '" + bmid.Ident + "', '" + c.Ident + "')");
        }

        static Branch BranchOf(List<Tok> part)
        {
            Branch br = new Branch();
            string v;
            if (part.Count == 0) { br.Kind = 3; return br; }
            if (LiteralOf(part, out v)) { br.Kind = 0; br.Text = v; return br; }
            if (part.Count > 2 && !part[0].Str && part[0].S == "new" && part[1].S == "string")
            {
                int open = part.FindIndex(delegate(Tok t) { return !t.Com && t.S == "{"; });
                if (open >= 0)
                {
                    int depth = 0, items = 0; bool any = false;
                    for (int i = open; i < part.Count; i++)
                    {
                        Tok t = part[i];
                        if (t.Com || t.Str) continue;
                        if (t.S == "{") { depth++; if (depth == 2) { items++; any = true; } }
                        else if (t.S == "}") depth--;
                        else if (t.S == "," && depth == 1) items++;
                    }
                    br.Kind = 1; br.Count = any ? items : 0;
                    return br;
                }
            }
            bool ident = true;
            foreach (Tok t in part) if (t.Com || t.Str || t.S.Length < 1 || !(char.IsLetter(t.S[0]) || t.S[0] == '_')) { ident = false; break; }
            if (ident && part.Count == 1) { br.Kind = 2; br.Ident = part[0].S; return br; }
            br.Kind = 3;
            return br;
        }

        // A date/time format string (custom specifiers and quoted literals only) or a culture name like "es-ES".
        static bool LocaleLiteral(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            if (Regex.IsMatch(v, "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,4}){0,2}$")) return true;
            v = v.Replace("'", "\u0001");
            int quotes = 0;
            foreach (char c in v) if (c == '\u0001') quotes++;
            if (quotes % 2 != 0) return false;
            v = Regex.Replace(v, "\u0001[^\u0001]*\u0001", "");   // quoted text may hold anything
            const string Spec = "dDfFgGhHKmMoOrRstTuUyYzZ/,.: %";
            foreach (char c in v) if (Spec.IndexOf(c) < 0) return false;
            return true;
        }

        static int ArrayLengthOf(string ident)
        {
            const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (Type t in typeof(I18n).Assembly.GetTypes())
            {
                try
                {
                    FieldInfo fi = t.GetField(ident, S);
                    if (fi != null && fi.FieldType == typeof(string[])) { string[] v = (string[])fi.GetValue(null); return v == null ? -1 : v.Length; }
                    PropertyInfo pi = t.GetProperty(ident, S);
                    if (pi != null && pi.PropertyType == typeof(string[]) && pi.GetIndexParameters().Length == 0)
                    { string[] v = (string[])pi.GetValue(null, null); return v == null ? -1 : v.Length; }
                }
                catch { return -1; }
            }
            return -1;
        }

        static string Show(List<Tok> part)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Tok t in part) { if (t.Com) continue; sb.Append(sb.Length == 0 ? "" : " ").Append(t.Str ? "\"" + t.S + "\"" : t.S); }
            string s = sb.ToString();
            return s.Length > 90 ? s.Substring(0, 90) + "\u2026" : s;
        }

        public static void NoUnwrappedSpanish()
        {
            List<string> found = new List<string>();
            foreach (Source f in Sources())
            {
                if (Path.GetFileName(f.Rel).StartsWith("I18n.German", StringComparison.Ordinal)) continue;
                List<Tok> toks = Tokenize(f.Text);
                ScanUiSinks(f.Rel, toks, found);
            }
            found.Sort(string.CompareOrdinal);
            foreach (string s in found) Console.WriteLine("    spanish: " + s);
            if (found.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append(found.Count + " Spanish literals reach a UI sink without I18n (first " + Math.Min(Cap, found.Count) + "):");
                for (int i = 0; i < found.Count && i < Cap; i++) sb.Append("\n" + found[i]);
                throw new InvalidOperationException(sb.ToString());
            }
        }

        public static void GermanPlaceholdersMatch()
        {
            List<string> found = new List<string>();
            foreach (KeyValuePair<string, string> e in AppTestsI18n.GermanByEnglish())
                if (Placeholders(e.Key) != Placeholders(e.Value))
                    found.Add("catalog en '" + e.Key + "' has " + Placeholders(e.Key) + " but de '" + e.Value + "' has " + Placeholders(e.Value));

            // The same check for the inline three-argument calls.
            foreach (Source f in Sources())
            {
                List<Tok> toks = Tokenize(f.Text);
                for (int i = 0; i < toks.Count - 3; i++)
                {
                    if (toks[i].Com || toks[i].Str || toks[i].S != "I18n") continue;
                    if (toks[i + 1].S != "." || toks[i + 2].S != "T" || toks[i + 3].S != "(") continue;
                    int line = toks[i].Line, at = i + 3;
                    List<List<Tok>> args = CallArgs(toks, ref at);
                    i = at - 1;
                    string en, de;
                    if (args.Count == 3 && LiteralOf(args[1], out en) && LiteralOf(args[2], out de) && Placeholders(en) != Placeholders(de))
                        found.Add(f.Rel + ":" + line + " en '" + en + "' has " + Placeholders(en) + " but de '" + de + "' has " + Placeholders(de));
                }
            }
            found.Sort(string.CompareOrdinal);
            foreach (string s in found) Console.WriteLine("    placeholders: " + s);
            Check(found.Count == 0, found.Count + " German texts do not keep the {n} placeholders of their English key (listed above)");
        }

        public static void NoDuplicateGermanKeys()
        {
            List<string> found = new List<string>();
            Dictionary<string, string> enAt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> esAt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Source f in Sources())
            {
                if (!Path.GetFileName(f.Rel).StartsWith("I18n.German", StringComparison.Ordinal)) continue;
                List<Tok> toks = Tokenize(f.Text);
                for (int i = 0; i < toks.Count - 1; i++)
                {
                    if (toks[i].Com || toks[i].Str || toks[i].S != "reg") continue;
                    if (toks[i + 1].S != "(") continue;
                    int line = toks[i].Line, at = i + 1;
                    List<List<Tok>> args = CallArgs(toks, ref at);
                    i = at - 1;
                    string en, es, de;
                    if (args.Count != 3 || !LiteralOf(args[0], out en) || !LiteralOf(args[1], out es) || !LiteralOf(args[2], out de)) continue;
                    string here = f.Rel + ":" + line;
                    if (en.Length > 0)
                    {
                        string was;
                        if (enAt.TryGetValue(en, out was)) found.Add("en '" + en + "' at " + here + " (already at " + was + ")");
                        else enAt[en] = here;
                    }
                    if (es.Length > 0)
                    {
                        string was;
                        if (esAt.TryGetValue(es, out was)) found.Add("es '" + es + "' at " + here + " (already at " + was + ")");
                        else esAt[es] = here;
                    }
                }
            }
            found.Sort(string.CompareOrdinal);
            foreach (string s in found) Console.WriteLine("    duplicate: " + s);
            Check(found.Count == 0, found.Count + " German catalog keys registered twice (first wins silently; listed above)");
        }

        // ---- scanning helpers

        sealed class Source { public string Rel; public string Text; }

        // The repo root: the runner exports STACKSHOT_TESTS_ROOT; otherwise walk up from the test exe or the cwd.
        static string RepoRoot()
        {
            string env = Environment.GetEnvironmentVariable("STACKSHOT_TESTS_ROOT");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "src", "I18n.cs"))) return env;
            foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
                for (DirectoryInfo d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (File.Exists(Path.Combine(d.FullName, "src", "I18n.cs"))) return d.FullName;
            throw new InvalidOperationException("cannot find the repository root (no src\\I18n.cs above the test exe)");
        }

        static List<Source> Sources()
        {
            string src = Path.Combine(RepoRoot(), "src");
            List<Source> all = new List<Source>();
            foreach (string p in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
                all.Add(new Source { Rel = p.Substring(src.Length + 1).Replace('\\', '/'), Text = File.ReadAllText(p) });
            all.Sort(delegate(Source a, Source b) { return string.CompareOrdinal(a.Rel, b.Rel); });
            return all;
        }

        static List<Tok> Tokenize(string code)
        {
            List<Tok> toks = new List<Tok>();
            int i = 0, line = 1, n = code.Length;
            while (i < n)
            {
                char c = code[i];
                if (c == '\n') { line++; i++; continue; }
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < n && code[i + 1] == '/')
                {
                    int j = code.IndexOf('\n', i); if (j < 0) j = n;
                    toks.Add(new Tok { S = code.Substring(i, j - i), Com = true, Line = line });
                    i = j; continue;
                }
                if (c == '/' && i + 1 < n && code[i + 1] == '*')
                {
                    int j = code.IndexOf("*/", i + 2, StringComparison.Ordinal); if (j < 0) j = n; else j += 2;
                    toks.Add(new Tok { S = code.Substring(i, Math.Min(j, n) - i), Com = true, Line = line });
                    for (int k = i; k < j && k < n; k++) if (code[k] == '\n') line++;
                    i = j; continue;
                }
                if (c == '@' && i + 1 < n && code[i + 1] == '"')
                {
                    int start = line, j = i + 2; StringBuilder sb = new StringBuilder();
                    while (j < n)
                    {
                        if (code[j] == '"' && j + 1 < n && code[j + 1] == '"') { sb.Append('"'); j += 2; continue; }
                        if (code[j] == '"') { j++; break; }
                        if (code[j] == '\n') line++;
                        sb.Append(code[j]); j++;
                    }
                    toks.Add(new Tok { S = sb.ToString(), Str = true, Line = start });
                    i = j; continue;
                }
                if (c == '"')
                {
                    int start = line, j = i + 1; StringBuilder sb = new StringBuilder();
                    while (j < n && code[j] != '"')
                    {
                        if (code[j] == '\\' && j + 1 < n)
                        {
                            j++;
                            char e = code[j];
                            switch (e)
                            {
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case '0': sb.Append('\0'); break;
                                case 'a': sb.Append('\a'); break;
                                case 'b': sb.Append('\b'); break;
                                case 'f': sb.Append('\f'); break;
                                case 'v': sb.Append('\v'); break;
                                case 'u':
                                    if (j + 4 < n) { int v; if (int.TryParse(code.Substring(j + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) sb.Append((char)v); j += 4; }
                                    break;
                                case 'x':
                                    { int k = j + 1, len = 0; while (k < n && len < 4 && Uri.IsHexDigit(code[k])) { len++; k++; } if (len > 0) { int v; if (int.TryParse(code.Substring(j + 1, len), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) sb.Append((char)v); j += len; } }
                                    break;
                                default: sb.Append(e); break;
                            }
                            j++;
                        }
                        else { sb.Append(code[j]); j++; }
                    }
                    i = j + 1;
                    toks.Add(new Tok { S = sb.ToString(), Str = true, Line = start });
                    continue;
                }
                if (c == '\'')
                {
                    int j = i + 1;
                    while (j < n && code[j] != '\'') { if (code[j] == '\\') j++; j++; }
                    i = j + 1;
                    toks.Add(new Tok { S = "'", Line = line });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i;
                    while (j < n && (char.IsLetterOrDigit(code[j]) || code[j] == '_')) j++;
                    toks.Add(new Tok { S = code.Substring(i, j - i), Line = line });
                    i = j; continue;
                }
                if (char.IsDigit(c))
                {
                    int j = i;
                    while (j < n && (char.IsLetterOrDigit(code[j]) || code[j] == '.')) j++;
                    toks.Add(new Tok { S = code.Substring(i, j - i), Line = line });
                    i = j; continue;
                }
                toks.Add(new Tok { S = c.ToString(), Line = line });
                i++;
            }
            return toks;
        }

        // The top-level arguments of the call whose "(" sits at toks[at]; on return at points past the ")".
        static List<List<Tok>> CallArgs(List<Tok> toks, ref int at)
        {
            List<List<Tok>> args = new List<List<Tok>>();
            List<Tok> cur = new List<Tok>();
            int depth = 1;
            at++;
            for (; at < toks.Count; at++)
            {
                Tok t = toks[at];
                if (t.Com) continue;
                if (t.S == "(" || t.S == "[" || t.S == "{") { depth++; cur.Add(t); continue; }
                if (t.S == ")" && depth == 1) { if (cur.Count > 0) args.Add(cur); at++; return args; }
                if (t.S == ")" || t.S == "]" || t.S == "}") { depth--; cur.Add(t); continue; }
                if (t.S == "," && depth == 1) { args.Add(cur); cur = new List<Tok>(); continue; }
                cur.Add(t);
            }
            return args;
        }

        // An argument made only of string literals joined by "+": its concatenated value.
        static bool LiteralOf(List<Tok> arg, out string value)
        {
            value = null;
            if (arg.Count == 0) return false;
            StringBuilder sb = new StringBuilder();
            foreach (Tok t in arg)
            {
                if (t.Str) sb.Append(t.S);
                else if (t.S == "+") { }
                else return false;
            }
            value = sb.ToString();
            return true;
        }

        static string Placeholders(string s)
        {
            SortedSet<int> set = new SortedSet<int>();
            foreach (Match m in Regex.Matches(s ?? "", @"\{([0-9]+)\}")) set.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            StringBuilder sb = new StringBuilder();
            foreach (int v in set) sb.Append(sb.Length == 0 ? "" : ",").Append(v);
            return sb.ToString();
        }

        // ---- unwrapped Spanish in UI sinks

        static readonly string[] SinkProps = { "Text", "Content", "ToolTip", "Title", "Header", "Filter" };

        static bool IsSink(string name)
        {
            if (name.Length == 0 || name.IndexOf("Log", StringComparison.Ordinal) >= 0) return false;
            switch (name)
            {
                case "Notify": case "Group": case "Header": case "Note": case "Label": case "Paragraph":
                case "MessageBox.Show": case "TrayMenu.Item": case "AutomationProperties.SetName":
                case "MacButton": case "ToolStripMenuItem":
                    return true;
            }
            int dot = name.LastIndexOf('.');
            string tail = dot >= 0 ? name.Substring(dot + 1) : name;
            return tail.EndsWith("Row", StringComparison.Ordinal);
        }

        // The qualified call name before a "(", walking back over identifier and "." tokens.
        static string QualifiedName(List<Tok> toks, int open)
        {
            StringBuilder sb = new StringBuilder();
            for (int j = open - 1; j >= 0; j--)
            {
                Tok t = toks[j];
                if (t.Com) continue;
                if (t.Str) break;
                if (t.S.Length == 1 && char.IsLetterOrDigit(t.S[0])) { sb.Insert(0, t.S); continue; }
                if (t.S == ".") { sb.Insert(0, "."); continue; }
                if (t.S == ">") { sb.Clear(); break; }   // generic: not a name we can classify
                if (t.S.Length > 1 && (char.IsLetter(t.S[0]) || t.S[0] == '_')) { sb.Insert(0, t.S); continue; }
                break;
            }
            return sb.ToString();
        }

        static void ScanUiSinks(string rel, List<Tok> toks, List<string> found)
        {
            List<string> frames = new List<string>();
            string prev1 = null, prev2 = null;
            for (int i = 0; i < toks.Count; i++)
            {
                Tok t = toks[i];
                if (t.Com) continue;
                if (t.Str)
                {
                    string v = t.S.Trim();
                    if (v.Length > 2 && HasLetters(v) && AppTestsI18n.LooksSpanish(t.S) && !AllowedIn(rel, t.S) && !Wrapped(frames) &&
                        (SinkBeforeLog(frames) || (prev1 == "=" && prev2 != "=" && prev2 != null && Array.IndexOf(SinkProps, prev2) >= 0)))
                        found.Add(rel + ":" + t.Line + ": " + v);
                }
                else if (t.S == "(") frames.Add(QualifiedName(toks, i));
                else if (t.S == ")" && frames.Count > 0) frames.RemoveAt(frames.Count - 1);
                prev2 = prev1;
                prev1 = t.Str ? "<str>" : t.S;
            }
        }

        // A literal inside I18n.T / I18n.Fmt is translated however the enclosing sink uses it (multi-line calls and
        // ternaries inside the arguments included): only literals that reach the sink outside any I18n call count.
        static bool Wrapped(List<string> frames)
        {
            foreach (string f in frames) if (f == "I18n.T" || f == "I18n.Fmt") return true;
            return false;
        }

        static bool AnySink(List<string> frames)
        {
            foreach (string f in frames) if (IsSink(f)) return true;
            return false;
        }

        // A literal is a sink argument only when a sink call encloses it before any log call does: a Log(…) inside a
        // sink's callback delegate (Notify("…", delegate { Log("es"); })) is log-only, not user-visible.
        static bool SinkBeforeLog(List<string> frames)
        {
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (IsSink(frames[i])) return true;
                if (frames[i].IndexOf("Log", StringComparison.Ordinal) >= 0) return false;
            }
            return false;
        }

        static bool AllowedIn(string rel, string text)
        {
            foreach (string e in UiAllowList)
            {
                string[] p = e.Split('|');
                if (rel.EndsWith(p[0], StringComparison.Ordinal) && p[1] == text) return true;
            }
            return false;
        }

        static bool HasLetters(string s)
        {
            foreach (char c in s) if (char.IsLetter(c)) return true;
            return false;
        }

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }
    }
}
