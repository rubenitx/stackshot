// Stackshot - Markdown helpers: validation, loading, tidying and the table of contents.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public static class MarkdownTools
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        static readonly string[] Exts = { ".md", ".markdown", ".mdown", ".mkd", ".txt", ".xml", ".xsd", ".config", ".svg", ".xaml" };
        static readonly Regex FenceRx = new Regex(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.Compiled);
        // Tidy also knows a fence indented under a list item: its lines ("* uno" in a code sample) are never rewritten.
        static readonly Regex AnyFenceRx = new Regex(@"^[ \t]*(`{3,}|~{3,})", RegexOptions.Compiled);
        // A space after the #s, as in MarkdownDoc: "#123" or "#include" are text.
        static readonly Regex HeadRx = new Regex(@"^ {0,3}(#{1,6})[ \t]+(\S.*?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.Compiled);
        static readonly Regex EmptyHeadRx = new Regex(@"^ {0,3}#{1,6}$", RegexOptions.Compiled);
        static readonly Regex RuleRx = new Regex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$", RegexOptions.Compiled);
        static readonly Regex BulletRx = new Regex(@"^(\s*)[*+](\s+)(?=\S)", RegexOptions.Compiled);
        static readonly Regex DelimRx = new Regex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.Compiled);

        // Null when the text can be shown; otherwise a friendly reason.
        public static string Validate(string text)
        {
            if (text == null || text.Trim().Length == 0) return I18n.T("Est\u00E1 vac\u00EDo: no hay nada que mostrar.", "It's empty: there is nothing to show.");
            if (text.Length > MaxBytes) return I18n.T("Es demasiado grande (m\u00E1ximo 2 MB de texto).", "It's too big (maximum 2 MB of text).");
            int bad = 0;
            foreach (char c in text)
                if (c == '\0' || (c < 32 && c != '\n' && c != '\r' && c != '\t' && c != '\f') || c == '\uFFFD') bad++;
            if (bad > 0 && bad * 100L > text.Length) return I18n.T("No parece texto: tiene contenido binario.", "This doesn't look like text: it has binary content.");
            if (text.IndexOf('\0') >= 0) return I18n.T("No parece texto: tiene contenido binario.", "This doesn't look like text: it has binary content.");
            return null;
        }

        public static bool Supported(string ext)
        {
            return ext != null && Array.IndexOf(Exts, ext.ToLowerInvariant()) >= 0;
        }

        public static string Load(string path, out string error)
        {
            DocFile d = DocFile.Load(path, out error);
            return d == null ? null : d.Text;
        }

        // ---- Where a document comes from: files, URIs and paths dropped or pasted from editors and Explorer

        static readonly Regex DriveRx = new Regex(@"^[A-Za-z]:[\\/]", RegexOptions.Compiled);
        static readonly Regex UriRx = new Regex(@"^[A-Za-z][A-Za-z0-9+.\-]+://", RegexOptions.Compiled);
        static readonly Regex UnixRx = new Regex(@"^(?:/(?:home|mnt|root|usr|opt|var|tmp|etc|srv|Users|workspaces?)/\S[^\n]*|/[^/\s][^\n]*/[^/\n]+\.(?:md|markdown|mdown|mkd|txt|xml|xsd|config|svg|xaml)(?::\d+){0,2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex MdExtRx = new Regex(@"\.(?:md|markdown|mdown|mkd|xml|xsd|config|svg|xaml)(?=$|[:#?])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex JsonStrRx = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
        static readonly Regex JsonUriRx = new Regex("\"(?:external|fsPath)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
        static string distro;
        static bool distroDone;
        static readonly object wslLock = new object();

        static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && (s[0] == '"' && s[s.Length - 1] == '"' || s[0] == '\'' && s[s.Length - 1] == '\'')) s = s.Substring(1, s.Length - 2).Trim();
            return s;
        }

        static bool IsSpec(string s)
        {
            if (s.Length < 2 || s.Length > 1024 || s.IndexOf('\n') >= 0) return false;
            if (DriveRx.IsMatch(s) || UriRx.IsMatch(s)) return true;
            if (s.StartsWith(@"\\", StringComparison.Ordinal)) return s.Length > 2 && s[2] != '\\';
            if (s.StartsWith("~/", StringComparison.Ordinal)) return s.Length > 2;
            return UnixRx.IsMatch(s);
        }

        // The lines of text when every one of them is a path or URI; otherwise null (it is a document).
        static List<string> PathsIn(string text)
        {
            List<string> r = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string l = Unquote(raw);
                if (l.Length == 0) continue;
                if (!IsSpec(l) || r.Count >= 20) return null;
                r.Add(l);
            }
            return r.Count == 0 ? null : r;
        }

        static string Str(object o)
        {
            if (o == null) return null;
            string s = o as string;
            if (s != null) return s;
            string[] arr = o as string[];
            if (arr != null) return string.Join("\n", arr);
            Stream st = o as Stream;
            if (st == null) return null;
            using (MemoryStream ms = new MemoryStream())
            {
                st.CopyTo(ms);
                byte[] b = ms.ToArray();
                if (b.Length > 1 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2).TrimEnd('\0');
                bool wide = b.Length > 3 && b[1] == 0 && b[3] == 0;
                return (wide ? Encoding.Unicode.GetString(b) : Encoding.UTF8.GetString(b)).TrimEnd('\0');
            }
        }

        static string Json(string s)
        {
            s = s.Replace("\\/", "/").Replace("\\\"", "\"");
            s = Regex.Replace(s, @"\\u([0-9a-fA-F]{4})", delegate(Match m) { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); });
            return s.Replace("\\\\", "\\");
        }

        // Every path or URI the data carries (FileDrop first, then what Cursor and VS Code put there), plus the plain text
        // when it is not just a path.
        public static List<string> Specs(IDataObject d, out string plain)
        {
            plain = null;
            List<string> specs = new List<string>();
            if (d == null) return specs;
            try
            {
                string[] fd = d.GetData(DataFormats.FileDrop) as string[];
                if (fd != null) specs.AddRange(fd);
            }
            catch { }
            string[] formats = { "text/uri-list", "CodeFiles", "ResourceURLs", "CodeEditors" };
            foreach (string f in formats)
            {
                try
                {
                    if (!d.GetDataPresent(f)) continue;
                    string s = Str(d.GetData(f));
                    if (string.IsNullOrEmpty(s)) continue;
                    if (f == "text/uri-list")
                    {
                        foreach (string line in s.Split('\n'))
                        {
                            string l = line.Trim();
                            if (l.Length > 0 && l[0] != '#') specs.Add(l);
                        }
                    }
                    else if (f == "CodeEditors")
                    {
                        foreach (Match m in JsonUriRx.Matches(s)) specs.Add(Json(m.Groups[1].Value));
                    }
                    else
                    {
                        foreach (Match m in JsonStrRx.Matches(s)) { string v = Json(m.Groups[1].Value); if (IsSpec(v)) specs.Add(v); }
                    }
                }
                catch { }
            }
            try
            {
                if (d.GetDataPresent(DataFormats.UnicodeText) || d.GetDataPresent(DataFormats.Text))
                {
                    string t = Str(d.GetData(DataFormats.UnicodeText)) ?? Str(d.GetData(DataFormats.Text));
                    if (t != null)
                    {
                        List<string> ps = PathsIn(t.Replace("\r", ""));
                        if (ps != null) specs.AddRange(ps);
                        else plain = t;
                    }
                }
            }
            catch { }
            List<string> uniq = new List<string>();
            foreach (string s in specs) if (s.Trim().Length > 0 && !uniq.Contains(s)) uniq.Add(s);
            return uniq;
        }

        // The data is something to load as a file (as opposed to text for the editor).
        public static bool HasFiles(IDataObject d)
        {
            if (d == null) return false;
            foreach (string f in new string[] { DataFormats.FileDrop, "text/uri-list", "CodeFiles", "ResourceURLs", "CodeEditors" })
            {
                try { if (d.GetDataPresent(f)) return true; } catch { }
            }
            return false;
        }

        static string DefaultDistro()
        {
            lock (wslLock)
            {
                if (distroDone) return distro;
                distroDone = true;
                try
                {
                    using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss"))
                    {
                        string guid = k == null ? null : k.GetValue("DefaultDistribution") as string;
                        if (guid != null)
                            using (Microsoft.Win32.RegistryKey sub = k.OpenSubKey(guid))
                            {
                                string n = sub == null ? null : sub.GetValue("DistributionName") as string;
                                if (!string.IsNullOrEmpty(n) && !n.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase)) distro = n;
                            }
                    }
                }
                catch (Exception ex) { ShotStack.Log("Markdown WSL: " + ex.Message); }
                if (distro == null)
                {
                    foreach (string root in new string[] { @"\\wsl.localhost", @"\\wsl$" })
                    {
                        try
                        {
                            foreach (string dir in Directory.GetDirectories(root))
                            {
                                string n = System.IO.Path.GetFileName(dir);
                                if (!n.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase)) { distro = n; break; }
                            }
                        }
                        catch { }
                        if (distro != null) break;
                    }
                }
                return distro;
            }
        }

        static string WslRoot(string d)
        {
            string a = @"\\wsl.localhost\" + d;
            try { if (Directory.Exists(a)) return a; } catch { }
            return @"\\wsl$\" + d;
        }

        static string WslHome(string root)
        {
            try
            {
                string pick = null;
                foreach (string dir in Directory.GetDirectories(root + @"\home"))
                {
                    string n = System.IO.Path.GetFileName(dir);
                    if (string.Equals(n, Environment.UserName, StringComparison.OrdinalIgnoreCase)) return dir;
                    if (pick == null) pick = dir;
                }
                return pick;
            }
            catch { return null; }
        }

        // A local or UNC path for a path, file:// or vscode-remote:// URI, or a WSL path; null with the reason in error.
        // May touch the network share of WSL, so call it off the UI thread.
        public static string ToLocal(string spec, out string error)
        {
            error = null;
            string s = Unquote(spec), remote = null;
            if (UriRx.IsMatch(s))
            {
                int cut = s.IndexOfAny(new char[] { '#', '?' });
                if (cut >= 0) s = s.Substring(0, cut);
                int sep = s.IndexOf("://", StringComparison.Ordinal);
                string scheme = s.Substring(0, sep).ToLowerInvariant(), rest = s.Substring(sep + 3);
                int slash = rest.IndexOf('/');
                string auth = Uri.UnescapeDataString(slash < 0 ? rest : rest.Substring(0, slash));
                string path = Uri.UnescapeDataString(slash < 0 ? "" : rest.Substring(slash));
                if (scheme == "file")
                {
                    if (auth.Length > 0 && auth != "localhost") return StripLine(@"\\" + auth + path.Replace('/', '\\'));
                    s = path;
                }
                else if (scheme == "vscode-remote" && auth.StartsWith("wsl+", StringComparison.OrdinalIgnoreCase)) { remote = auth.Substring(4); s = path; }
                else if (scheme == "vscode-file" || scheme == "vscode-userdata") s = path;
                else { error = I18n.Fmt("No puedo abrir un archivo remoto ({0}): {1}", "Cannot open a remote file ({0}): {1}", auth, spec); return null; }
            }
            s = StripLine(s);
            if (Regex.IsMatch(s, @"^/[A-Za-z]:[\\/]")) s = s.Substring(1);
            if (DriveRx.IsMatch(s) || s.StartsWith(@"\\", StringComparison.Ordinal)) return s.Replace('/', '\\');
            if (!s.StartsWith("/", StringComparison.Ordinal) && !s.StartsWith("~/", StringComparison.Ordinal)) { error = I18n.Fmt("No reconozco esta ruta: {0}", "Cannot recognize this path: {0}", spec); return null; }
            Match mnt = Regex.Match(s, @"^/mnt/([A-Za-z])(/.*)?$");
            if (mnt.Success && remote == null) return mnt.Groups[1].Value.ToUpperInvariant() + ":" + (mnt.Groups[2].Success ? mnt.Groups[2].Value : "/").Replace('/', '\\');
            string d = remote ?? DefaultDistro();
            if (d == null) { error = I18n.Fmt("No encuentro WSL para abrir: {0}", "Cannot find WSL to open: {0}", spec); return null; }
            string root = WslRoot(d);
            if (s.StartsWith("~/", StringComparison.Ordinal))
            {
                string home = WslHome(root);
                if (home == null) { error = I18n.Fmt("No encuentro la carpeta personal de WSL para abrir: {0}", "Cannot find the WSL home folder to open: {0}", spec); return null; }
                return home + s.Substring(1).Replace('/', '\\');
            }
            return root + s.Replace('/', '\\');
        }

        static string StripLine(string s)
        {
            return Regex.Replace(s, @"(?<=[^:\\/]):\d+(?::\d+)?$", "");
        }

        // Loads the first Markdown document among the paths (or the first path when none looks like Markdown).
        public static string LoadSpecs(List<string> specs, out string path, out string error)
        {
            path = null;
            error = null;
            if (specs.Count == 0) { error = I18n.T("No hay ning\u00FAn archivo.", "There is no file."); return null; }
            string pick = specs[0];
            foreach (string s in specs) if (MdExtRx.IsMatch(s)) { pick = s; break; }
            path = ToLocal(pick, out error);
            return path == null ? null : Load(path, out error);
        }

        // The same, keeping how the file is stored so it can be saved back.
        public static DocFile LoadDoc(List<string> specs, out string error)
        {
            error = null;
            if (specs.Count == 0) { error = I18n.T("No hay ning\u00FAn archivo.", "There is no file."); return null; }
            string pick = specs[0];
            foreach (string s in specs) if (MdExtRx.IsMatch(s)) { pick = s; break; }
            string path = ToLocal(pick, out error);
            return path == null ? null : DocFile.Load(path, out error);
        }

        public static string Normalize(string t)
        {
            if (t.Length > 0 && t[0] == '\uFEFF') t = t.Substring(1);
            return t.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        // ---- Tidy

        public static string Tidy(string text)
        {
            string[] src = Normalize(text).Split('\n');
            List<string> o = new List<string>();
            bool fence = false, blankNext = false;
            string mark = null;
            for (int i = 0; i < src.Length; i++)
            {
                string raw = src[i];
                if (fence)
                {
                    o.Add(raw);
                    string t = raw.TrimStart();
                    if (t.StartsWith(mark, StringComparison.Ordinal) && t.TrimEnd().Trim('`', '~').Length == 0) { fence = false; blankNext = true; }
                    continue;
                }
                Match fm = AnyFenceRx.Match(raw);
                string l = raw.TrimEnd();
                if (fm.Success && FenceHere(o, o.Count, raw))
                {
                    fence = true;
                    mark = fm.Groups[1].Value;
                    Gap(o, true);
                    o.Add(l);
                    blankNext = false;
                    continue;
                }
                if (l.Length == 0)
                {
                    if (o.Count > 0 && o[o.Count - 1].Length > 0) o.Add("");
                    blankNext = false;
                    continue;
                }
                if (blankNext) { Gap(o, true); blankNext = false; }
                Match hm = HeadRx.Match(l);
                if (hm.Success && !RuleRx.IsMatch(l))
                {
                    Gap(o, true);
                    o.Add(hm.Groups[1].Value + " " + hm.Groups[2].Value.Trim());
                    blankNext = true;
                    continue;
                }
                // A list item line is an item, even with pipes in its text ("1. | a |"): not a table's header.
                if (l.IndexOf('|') >= 0 && i + 1 < src.Length && DelimRx.IsMatch(src[i + 1]) && src[i + 1].IndexOf('-') >= 0 && src[i + 1].IndexOf('|') >= 0 &&
                    MarkdownDoc.Cells(l).Count == MarkdownDoc.Cells(src[i + 1]).Count && !ItemMarkRx.IsMatch(l) && !l.TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    int j = i + 2;
                    while (j < src.Length && src[j].Trim().Length > 0 && src[j].IndexOf('|') >= 0 && !ItemMarkRx.IsMatch(src[j]) && !src[j].TrimStart().StartsWith(">", StringComparison.Ordinal)) j++;
                    // Right under a list item's text (no blank line) the table belongs to the item: it is indented to the
                    // item's text so the blank line put above it does not take it out of the list.
                    int item = o.Count > 0 && o[o.Count - 1].Length > 0 ? ItemTextColumn(o) : -1;
                    Gap(o, true);
                    o.AddRange(Table(src, i, j, item > Indent(l) ? new string(' ', item) : null));
                    // A table inside a list item (indented) keeps what is glued under it: a blank line would end the item there.
                    blankNext = item <= Indent(l) && Indent(l) == 0;
                    i = j - 1;
                    continue;
                }
                if (!RuleRx.IsMatch(l)) l = BulletRx.Replace(l, "$1-$2");
                // A hard break (two spaces) before another text line survives as exactly two spaces.
                // Not before a heading, a fence or a table: those get a blank line above, where a hard break means nothing.
                if (raw.EndsWith("  ", StringComparison.Ordinal) && i + 1 < src.Length && src[i + 1].Trim().Length > 0 && !GapAbove(src, i + 1) &&
                    !RuleRx.IsMatch(l) && !EmptyHeadRx.IsMatch(l) && !Interrupts(src[i + 1])) l += "  ";
                o.Add(l);
            }
            while (o.Count > 0 && o[o.Count - 1].Length == 0) o.RemoveAt(o.Count - 1);
            while (o.Count > 0 && o[0].Length == 0) o.RemoveAt(0);
            RenumberLists(o);
            return string.Join("\n", o.ToArray()) + "\n";
        }

        static readonly Regex QuotePrefixRx = new Regex(@"^[ \t]*>[ ]?", RegexOptions.Compiled);

        // Whether Tidy puts a blank line above line k: a heading, a fence or a table's header.
        // Whether a line starts a new block right under a paragraph (CommonMark): a quote, a bullet item with text, or a
        // numbered one starting at 1. "2. b" under text is more text, so a hard break before it still counts.
        static bool Interrupts(string next)
        {
            if (next.TrimStart().StartsWith(">", StringComparison.Ordinal)) return true;
            Match m = ItemMarkRx.Match(next);
            if (!m.Success) return false;
            string mark = m.Groups[2].Value;
            return !char.IsDigit(mark[0]) || mark.Substring(0, mark.Length - 1) == "1";
        }

        // A fence line indented four or more is a fence only inside a list item (between its text column and three past
        // it); anywhere else it is text (under a paragraph) or indented code.
        static bool FenceHere(IList<string> before, int count, string l)
        {
            int d = Indent(l);
            if (d <= 3) return true;
            for (int k = count - 1; k >= 0; k--)
            {
                string p = before[k];
                if (p.Trim().Length == 0 || Indent(p) >= d) continue;
                Match m = ItemMarkRx.Match(p);
                if (!m.Success) return false;
                int sp = Indent(m.Groups[3].Value);
                int text = Indent(m.Groups[1].Value) + m.Groups[2].Length + (sp >= 1 && sp <= 4 ? sp : 1);
                return d >= text && d - text <= 3;
            }
            return false;
        }

        static bool GapAbove(string[] src, int k)
        {
            string l = src[k];
            if (HeadRx.IsMatch(l) && !RuleRx.IsMatch(l)) return true;
            if (AnyFenceRx.IsMatch(l) && FenceHere(src, k, l)) return true;
            return l.IndexOf('|') >= 0 && !ItemMarkRx.IsMatch(l) && !l.TrimStart().StartsWith(">", StringComparison.Ordinal) &&
                   k + 1 < src.Length && DelimRx.IsMatch(src[k + 1]) && src[k + 1].IndexOf('-') >= 0 && src[k + 1].IndexOf('|') >= 0 &&
                   MarkdownDoc.Cells(l).Count == MarkdownDoc.Cells(src[k + 1]).Count;
        }

        static readonly Regex NumItemRx = new Regex(@"^([ \t]*)(\d{1,9})([.)])([ \t]+|$)", RegexOptions.Compiled);
        static readonly Regex AnyBulletRx = new Regex(@"^[ \t]*[-*+]([ \t]+|$)", RegexOptions.Compiled);

        static int Indent(string l)
        {
            int n = 0;
            foreach (char c in l) { if (c == ' ') n++; else if (c == '\t') n += 4; else break; }
            return n;
        }

        sealed class ListLevel
        {
            public int Marker, Content;   // columns of the number and of the item's text
            public long Next;
            public char Delim;            // a period or a parenthesis: "1)" after "1." starts another list
            public bool Empty;            // the current item has no text yet (nothing for a lazy line to continue)
        }

        // Numbered lists counted again from their first number, the way CommonMark reads them. Only real list items
        // change: a number that merely continues a paragraph (it would not start a list there) or sits in code stays as
        // written, and so does one whose wider or narrower number would move the lines under it out of the item.
        static void RenumberLists(List<string> o)
        {
            List<ListLevel> lv = new List<ListLevel>();
            bool fence = false, prevBlank = true, para = false;
            string mark = null;
            for (int i = 0; i < o.Count; i++)
            {
                string l = o[i];
                if (fence)
                {
                    string t = l.TrimStart();
                    if (t.StartsWith(mark, StringComparison.Ordinal) && t.TrimEnd().Trim('`', '~').Length == 0) { fence = false; para = false; }
                    continue;
                }
                if (l.Trim().Length == 0) { prevBlank = true; para = false; continue; }
                int d = Indent(l);
                int top = lv.Count - 1;
                if (l.TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    // A quote ends the lists around it; the lists inside it are counted on their own.
                    int j = i;
                    List<string> prefix = new List<string>(), inner = new List<string>();
                    for (; j < o.Count && o[j].TrimStart().StartsWith(">", StringComparison.Ordinal); j++)
                    {
                        Match q = QuotePrefixRx.Match(o[j]);
                        prefix.Add(q.Value);
                        inner.Add(o[j].Substring(q.Length));
                    }
                    RenumberLists(inner);
                    for (int k = 0; k < inner.Count; k++) o[i + k] = prefix[k] + inner[k];
                    while (lv.Count > 0 && lv[lv.Count - 1].Marker >= d) lv.RemoveAt(lv.Count - 1);
                    i = j - 1;
                    prevBlank = false;
                    para = true;
                    continue;
                }
                Match fm = AnyFenceRx.Match(l);
                if (fm.Success && FenceHere(o, i, l))
                {
                    fence = true;
                    mark = fm.Groups[1].Value;
                    while (lv.Count > 0 && lv[lv.Count - 1].Content > d) lv.RemoveAt(lv.Count - 1);
                    prevBlank = false;
                    continue;
                }
                Match m = NumItemRx.Match(l);
                if (m.Success)
                {
                    // Left of a level's marker that level is over. An item starts at most three columns past the text of
                    // the item it sits in (the margin at the top level): further in it is text or code, not an item.
                    while (top >= 0 && d < lv[top].Marker) { lv.RemoveAt(top); top--; }
                    int sp = m.Groups[4].Length;
                    bool empty = l.Substring(m.Length).Trim().Length == 0;
                    int body = d + m.Groups[2].Length + 1 + (sp >= 1 && sp <= 4 ? sp : 1);
                    char delim = m.Groups[3].Value[0];
                    int outer = top >= 1 ? lv[top - 1].Content : 0;
                    bool sibling = top >= 0 && d < lv[top].Content && d - outer <= 3;
                    if (sibling && lv[top].Delim != delim) { lv.RemoveAt(top); top--; sibling = false; }
                    bool nested = top < 0 ? d <= 3 : d >= lv[top].Content && d - lv[top].Content <= 3;
                    // Only a non-empty item numbered 1 may interrupt a paragraph.
                    bool canStart = !para || (m.Groups[2].Value == "1" && !empty);
                    if (sibling)
                    {
                        // Past nine digits a number is no longer a list item: from there on it stays as written.
                        if (lv[top].Next <= 999999999)
                        {
                            string want = lv[top].Next.ToString();
                            int moved = body + want.Length - m.Groups[2].Length;
                            if (moved == body || KeepsBody(o, i, d, body, moved))
                            {
                                o[i] = m.Groups[1].Value + want + l.Substring(m.Groups[1].Length + m.Groups[2].Length);
                                body = moved;
                            }
                        }
                        lv[top].Next++;
                        lv[top].Content = body;
                        lv[top].Empty = empty;
                        para = !empty;
                    }
                    else if (nested && canStart)
                    {
                        long num;
                        long.TryParse(m.Groups[2].Value, out num);
                        ListLevel n = new ListLevel();
                        n.Marker = d; n.Content = body; n.Next = num + 1; n.Delim = delim; n.Empty = empty;
                        lv.Add(n);
                        para = !empty;
                    }
                    else para = true;
                }
                else
                {
                    if (top >= 0)
                    {
                        // A bullet, heading or rule at a list's level ends it; a line at the item's text belongs to it; a
                        // line further left continues the item's text, unless a blank line came first or the item has no
                        // text to continue.
                        if (AnyBulletRx.IsMatch(l) || HeadRx.IsMatch(l) || RuleRx.IsMatch(l))
                            while (lv.Count > 0 && lv[lv.Count - 1].Content > d) lv.RemoveAt(lv.Count - 1);
                        else if (d >= lv[top].Content) lv[top].Empty = false;
                        else if (prevBlank || lv[top].Empty)
                            while (lv.Count > 0 && lv[lv.Count - 1].Content > d) lv.RemoveAt(lv.Count - 1);
                    }
                    para = !RuleRx.IsMatch(l) && !(HeadRx.IsMatch(l) && !AnyBulletRx.IsMatch(l));
                }
                prevBlank = false;
            }
        }

        // Whether an item's text can move from one column to another (its number got a digit more or less) without
        // changing where the lines under it belong: none of them may start between the two columns.
        internal static bool KeepsBody(IList<string> lines, int item, int marker, int from, int to)
        {
            int lo = Math.Min(from, to), hi = Math.Max(from, to);
            bool blank = false;
            for (int j = item + 1; j < lines.Count; j++)
            {
                string l = lines[j];
                if (l.Trim().Length == 0) { blank = true; continue; }
                int d = Indent(l);
                // At or left of the number the item is over, unless it is plain text right under it (a lazy line).
                if (d <= marker && (blank || ItemMarkRx.IsMatch(l) || HeadRx.IsMatch(l) || RuleRx.IsMatch(l) || AnyFenceRx.IsMatch(l) ||
                                    l.TrimStart().StartsWith(">", StringComparison.Ordinal))) break;
                if (d >= lo && d < hi) return false;
                blank = false;
            }
            return true;
        }

        static void Gap(List<string> o, bool want)
        {
            if (want && o.Count > 0 && o[o.Count - 1].Length > 0) o.Add("");
        }

        static readonly Regex ItemMarkRx = new Regex(@"^([ \t]*)([-*+]|\d{1,9}[.)])([ \t]+)\S", RegexOptions.Compiled);

        // The column where the text of the list item over the end of o starts, when the lines since then are its text
        // (no blank line between); -1 otherwise.
        static int ItemTextColumn(List<string> o)
        {
            for (int k = o.Count - 1; k >= 0 && o[k].Trim().Length > 0; k--)
            {
                Match m = ItemMarkRx.Match(o[k]);
                if (!m.Success || RuleRx.IsMatch(o[k])) continue;
                int sp = Indent(m.Groups[3].Value);
                return Indent(m.Groups[1].Value) + m.Groups[2].Length + (sp >= 1 && sp <= 4 ? sp : 1);
            }
            return -1;
        }

        static List<string> Table(string[] src, int from, int to, string indent)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> head = MarkdownDoc.Cells(src[from], true), delim = MarkdownDoc.Cells(src[from + 1]);
            int n = head.Count;
            rows.Add(head);
            for (int i = from + 2; i < to; i++)
            {
                List<string> r = MarkdownDoc.Cells(src[i], true);
                while (r.Count < n) r.Add("");
                if (r.Count > n) r.RemoveRange(n, r.Count - n);
                rows.Add(r);
            }
            return FormatTable(rows, delim, indent ?? LeadingSpace(src[from]));
        }

        // The spaces and tabs a line starts with (a table inside a list item keeps them).
        public static string LeadingSpace(string l)
        {
            int i = 0;
            while (i < l.Length && (l[i] == ' ' || l[i] == '\t')) i++;
            return l.Substring(0, i);
        }

        public static List<string> FormatTable(List<List<string>> rows, List<string> delim)
        {
            return FormatTable(rows, delim, "");
        }

        // A table written out aligned: rows[0] is the header, delim the cells of its line of dashes (they set each
        // column's alignment), every line starting with indent. Every row must have the header's number of cells.
        public static List<string> FormatTable(List<List<string>> rows, List<string> delim, string indent)
        {
            int n = rows[0].Count;
            int[] w = ColumnWidths(rows);
            int[] al = new int[n];
            for (int c = 0; c < n; c++)
            {
                string d = c < delim.Count ? delim[c].Trim() : "---";
                // 0 plain, 1 centre, 2 right, 3 left written out (":---"), kept as the author wrote it.
                bool left = d.StartsWith(":", StringComparison.Ordinal), right = d.EndsWith(":", StringComparison.Ordinal);
                al[c] = left && right ? 1 : right ? 2 : left ? 3 : 0;
            }
            List<string> o = new List<string>();
            for (int k = 0; k < rows.Count; k++)
            {
                StringBuilder sb = new StringBuilder(indent + "|");
                for (int c = 0; c < n; c++) sb.Append(' ').Append(Pad(rows[k][c], w[c], al[c])).Append(" |");
                o.Add(sb.ToString());
                if (k == 0)
                {
                    sb = new StringBuilder(indent + "|");
                    for (int c = 0; c < n; c++)
                    {
                        string bar = new string('-', w[c]);
                        if (al[c] == 1) bar = ":" + bar.Substring(2) + ":";
                        else if (al[c] == 2) bar = bar.Substring(1) + ":";
                        else if (al[c] == 3) bar = ":" + bar.Substring(1);
                        sb.Append(' ').Append(bar).Append(" |");
                    }
                    o.Add(sb.ToString());
                }
            }
            return o;
        }

        // Each column's width in screen columns, at least 3 (the dashes).
        public static int[] ColumnWidths(List<List<string>> rows)
        {
            int n = rows[0].Count;
            int[] w = new int[n];
            for (int c = 0; c < n; c++)
            {
                w[c] = 3;
                foreach (List<string> r in rows) w[c] = Math.Max(w[c], DisplayWidth(r[c]));
            }
            return w;
        }

        // How many columns a text takes in a monospaced editor: East Asian wide characters and most emoji take two,
        // combining marks and joiners none.
        public static int DisplayWidth(string s)
        {
            int w = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int cp = s[i];
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { cp = char.ConvertToUtf32(s[i], s[i + 1]); i++; }
                w += CharWidth(cp);
            }
            return w;
        }

        static readonly int[] Wide =
        {
            0x1100, 0x115F, 0x231A, 0x231B, 0x2329, 0x232A, 0x23E9, 0x23EC, 0x23F0, 0x23F0, 0x23F3, 0x23F3, 0x25FD, 0x25FE,
            0x2614, 0x2615, 0x2648, 0x2653, 0x267F, 0x267F, 0x2693, 0x2693, 0x26A1, 0x26A1, 0x26AA, 0x26AB, 0x26BD, 0x26BE,
            0x26C4, 0x26C5, 0x26CE, 0x26CE, 0x26D4, 0x26D4, 0x26EA, 0x26EA, 0x26F2, 0x26F3, 0x26F5, 0x26F5, 0x26FA, 0x26FA,
            0x26FD, 0x26FD, 0x2705, 0x2705, 0x270A, 0x270B, 0x2728, 0x2728, 0x274C, 0x274C, 0x274E, 0x274E, 0x2753, 0x2755,
            0x2757, 0x2757, 0x2795, 0x2797, 0x27B0, 0x27B0, 0x27BF, 0x27BF, 0x2B1B, 0x2B1C, 0x2B50, 0x2B50, 0x2B55, 0x2B55,
            0x2E80, 0x303E, 0x3041, 0x33FF, 0x3400, 0x4DBF, 0x4E00, 0x9FFF, 0xA000, 0xA4CF, 0xA960, 0xA97F, 0xAC00, 0xD7A3,
            0xF900, 0xFAFF, 0xFE10, 0xFE19, 0xFE30, 0xFE6F, 0xFF00, 0xFF60, 0xFFE0, 0xFFE6, 0x1F300, 0x1F64F, 0x1F680, 0x1F6FF,
            0x1F900, 0x1F9FF, 0x1FA70, 0x1FAFF, 0x20000, 0x3FFFD
        };

        static int CharWidth(int cp)
        {
            if (cp == 0x200B || cp == 0x200C || cp == 0x200D || cp == 0x2060 || cp == 0xFEFF || (cp >= 0xFE00 && cp <= 0xFE0F)) return 0;
            if (cp < 0x10000)
            {
                System.Globalization.UnicodeCategory cat = char.GetUnicodeCategory((char)cp);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark || cat == System.Globalization.UnicodeCategory.EnclosingMark) return 0;
            }
            for (int i = 0; i < Wide.Length; i += 2) if (cp >= Wide[i] && cp <= Wide[i + 1]) return 2;
            return 1;
        }

        static string Pad(string s, int w, int align)
        {
            int gap = w - DisplayWidth(s);
            if (gap <= 0) return s;
            if (align == 2) return new string(' ', gap) + s;
            if (align == 1) return new string(' ', gap / 2) + s + new string(' ', gap - gap / 2);
            return s + new string(' ', gap);
        }

        // ---- Table of contents

        public static string Slug(string title)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in MarkdownDoc.Plain(title).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else if (c == ' ') sb.Append('-');
            }
            return sb.ToString();
        }

        static bool IsTocTitle(string t)
        {
            t = t.Trim().ToLowerInvariant();
            return t == "\u00EDndice" || t == "indice" || t == "tabla de contenidos" || t == "contenido" || t == "table of contents" || t == "contents";
        }

        public static string Toc(string text)
        {
            List<string> lines = new List<string>(Normalize(text).Split('\n'));
            List<int> at = new List<int>(), lvl = new List<int>();
            List<string> titles = new List<string>();
            bool fence = false;
            int tocAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (FenceRx.IsMatch(lines[i])) { fence = !fence; continue; }
                if (fence) continue;
                Match m = HeadRx.Match(lines[i]);
                if (!m.Success || RuleRx.IsMatch(lines[i])) continue;
                if (IsTocTitle(m.Groups[2].Value)) { if (tocAt < 0) tocAt = i; continue; }
                at.Add(i); lvl.Add(m.Groups[1].Length); titles.Add(m.Groups[2].Value.Trim());
            }
            if (titles.Count == 0) return null;
            int min = 6;
            foreach (int l in lvl) min = Math.Min(min, l);
            // A single leading H1 is the document title, not an entry.
            int skip = lvl[0] == 1 && lvl.FindAll(delegate(int l) { return l == 1; }).Count == 1 ? 1 : 0;
            if (skip == 1) { min = 6; for (int k = 1; k < lvl.Count; k++) min = Math.Min(min, lvl[k]); }
            if (skip == 1 && titles.Count == 1) return null;
            List<string> toc = new List<string>();
            toc.Add(I18n.T("## \u00CDndice", "## Contents"));
            toc.Add("");
            Dictionary<string, int> seen = new Dictionary<string, int>();
            for (int k = 0; k < titles.Count; k++)
            {
                string slug = Slug(titles[k]);
                int n;
                if (seen.TryGetValue(slug, out n)) { seen[slug] = n + 1; slug += "-" + n; } else seen[slug] = 1;
                if (k < skip) continue;
                toc.Add(new string(' ', 2 * Math.Min(3, lvl[k] - min)) + "- [" + titles[k] + "](#" + slug + ")");
            }
            toc.Add("");
            if (tocAt >= 0)
            {
                int end = tocAt + 1;
                while (end < lines.Count && (lines[end].Trim().Length == 0 || Regex.IsMatch(lines[end], @"^\s*([-*+]|\d+[.)])\s"))) end++;
                lines.RemoveRange(tocAt, end - tocAt);
                lines.InsertRange(tocAt, toc);
                return string.Join("\n", lines.ToArray());
            }
            int pos = skip == 1 ? at[0] + 1 : 0;
            if (pos < lines.Count && lines[pos].Trim().Length == 0) toc.RemoveAt(toc.Count - 1);
            if (pos > 0) toc.Insert(0, "");
            lines.InsertRange(pos, toc);
            return string.Join("\n", lines.ToArray());
        }

        public static int Words(string text)
        {
            int n = 0;
            bool inWord = false;
            foreach (char c in text)
            {
                bool w = !char.IsWhiteSpace(c);
                if (w && !inWord) n++;
                inWord = w;
            }
            return n;
        }

        public static string Title(string text)
        {
            foreach (MdBlock b in MarkdownDoc.Parse(text))
                if (b.Kind == MdKind.Heading) return MarkdownDoc.Plain(b.Text);
            return null;
        }
    }
}
