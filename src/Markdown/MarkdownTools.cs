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
        static readonly Regex HeadRx = new Regex(@"^ {0,3}(#{1,6})[ \t]*(\S.*?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.Compiled);
        static readonly Regex RuleRx = new Regex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$", RegexOptions.Compiled);
        static readonly Regex BulletRx = new Regex(@"^(\s*)[*+](\s+)(?=\S)", RegexOptions.Compiled);
        static readonly Regex DelimRx = new Regex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.Compiled);

        // Null when the text can be shown; otherwise a friendly reason.
        public static string Validate(string text)
        {
            if (text == null || text.Trim().Length == 0) return "Est\u00E1 vac\u00EDo: no hay nada que mostrar.";
            if (text.Length > MaxBytes) return "Es demasiado grande (m\u00E1ximo 2 MB de texto).";
            int bad = 0;
            foreach (char c in text)
                if (c == '\0' || (c < 32 && c != '\n' && c != '\r' && c != '\t' && c != '\f') || c == '\uFFFD') bad++;
            if (bad > 0 && bad * 100L > text.Length) return "No parece texto: tiene contenido binario.";
            if (text.IndexOf('\0') >= 0) return "No parece texto: tiene contenido binario.";
            return null;
        }

        public static string Load(string path, out string error)
        {
            error = null;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists) { error = "No encuentro el archivo: " + path; return null; }
                if (Array.IndexOf(Exts, fi.Extension.ToLowerInvariant()) < 0) { error = "Solo admito archivos .md, .xml o .txt: " + fi.Name; return null; }
                bool xml = XmlDoc.IsXmlPath(path);
                if (fi.Length > (xml ? XmlDoc.MaxChars : MaxBytes)) { error = "El archivo pesa demasiado (m\u00E1ximo " + (xml ? "8" : "2") + " MB): " + fi.Name; return null; }
                byte[] raw = File.ReadAllBytes(path);
                string text = Decode(raw);
                error = xml ? XmlDoc.CheckText(text) : Validate(text);
                return error == null ? Normalize(text) : null;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Markdown: " + ex.Message);
                error = "No he podido leer el archivo: " + path;
                return null;
            }
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
                else { error = "No puedo abrir un archivo remoto (" + auth + "): " + spec; return null; }
            }
            s = StripLine(s);
            if (Regex.IsMatch(s, @"^/[A-Za-z]:[\\/]")) s = s.Substring(1);
            if (DriveRx.IsMatch(s) || s.StartsWith(@"\\", StringComparison.Ordinal)) return s.Replace('/', '\\');
            if (!s.StartsWith("/", StringComparison.Ordinal) && !s.StartsWith("~/", StringComparison.Ordinal)) { error = "No reconozco esta ruta: " + spec; return null; }
            Match mnt = Regex.Match(s, @"^/mnt/([A-Za-z])(/.*)?$");
            if (mnt.Success && remote == null) return mnt.Groups[1].Value.ToUpperInvariant() + ":" + (mnt.Groups[2].Success ? mnt.Groups[2].Value : "/").Replace('/', '\\');
            string d = remote ?? DefaultDistro();
            if (d == null) { error = "No encuentro WSL para abrir: " + spec; return null; }
            string root = WslRoot(d);
            if (s.StartsWith("~/", StringComparison.Ordinal))
            {
                string home = WslHome(root);
                if (home == null) { error = "No encuentro la carpeta personal de WSL para abrir: " + spec; return null; }
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
            if (specs.Count == 0) { error = "No hay ning\u00FAn archivo."; return null; }
            string pick = specs[0];
            foreach (string s in specs) if (MdExtRx.IsMatch(s)) { pick = s; break; }
            path = ToLocal(pick, out error);
            return path == null ? null : Load(path, out error);
        }

        static string Decode(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            Encoding declared = DeclaredEncoding(b);
            if (declared != null) return declared.GetString(b);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (ArgumentException) { return Encoding.GetEncoding(1252).GetString(b); }
        }

        // Without a BOM an XML file says its own encoding in the declaration (ASCII-compatible ones only).
        static Encoding DeclaredEncoding(byte[] b)
        {
            try
            {
                int n = Math.Min(b.Length, 256);
                string head = Encoding.ASCII.GetString(b, 0, n);
                if (!head.StartsWith("<?xml", StringComparison.Ordinal)) return null;
                int end = head.IndexOf("?>", StringComparison.Ordinal);
                if (end > 0) head = head.Substring(0, end);
                Match m = Regex.Match(head, @"encoding\s*=\s*[""']([A-Za-z0-9._\-]+)[""']");
                if (!m.Success) return null;
                string name = m.Groups[1].Value;
                if (name.StartsWith("utf-16", StringComparison.OrdinalIgnoreCase) || name.StartsWith("utf-32", StringComparison.OrdinalIgnoreCase)) return null;
                if (string.Equals(name, "utf-8", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "utf8", StringComparison.OrdinalIgnoreCase)) return null;
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException) { return null; }
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
                    if (t.StartsWith(mark, StringComparison.Ordinal) && t.Trim('`', '~').Length == 0) { fence = false; blankNext = true; }
                    continue;
                }
                Match fm = FenceRx.Match(raw);
                string l = raw.TrimEnd();
                if (fm.Success)
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
                if (l.IndexOf('|') >= 0 && i + 1 < src.Length && DelimRx.IsMatch(src[i + 1]) && src[i + 1].IndexOf('-') >= 0 &&
                    MarkdownDoc.Cells(l).Count == MarkdownDoc.Cells(src[i + 1]).Count)
                {
                    int j = i + 2;
                    while (j < src.Length && src[j].Trim().Length > 0 && src[j].IndexOf('|') >= 0) j++;
                    Gap(o, true);
                    o.AddRange(Table(src, i, j));
                    blankNext = true;
                    i = j - 1;
                    continue;
                }
                if (!RuleRx.IsMatch(l)) l = BulletRx.Replace(l, "$1-$2");
                // A hard break (two spaces) before another text line survives as exactly two spaces.
                if (raw.EndsWith("  ", StringComparison.Ordinal) && i + 1 < src.Length && src[i + 1].Trim().Length > 0 &&
                    !l.StartsWith("-", StringComparison.Ordinal)) l += "  ";
                o.Add(l);
            }
            while (o.Count > 0 && o[o.Count - 1].Length == 0) o.RemoveAt(o.Count - 1);
            while (o.Count > 0 && o[0].Length == 0) o.RemoveAt(0);
            return string.Join("\n", o.ToArray()) + "\n";
        }

        static void Gap(List<string> o, bool want)
        {
            if (want && o.Count > 0 && o[o.Count - 1].Length > 0) o.Add("");
        }

        static List<string> Table(string[] src, int from, int to)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> head = MarkdownDoc.Cells(src[from]), delim = MarkdownDoc.Cells(src[from + 1]);
            int n = head.Count;
            rows.Add(head);
            for (int i = from + 2; i < to; i++)
            {
                List<string> r = MarkdownDoc.Cells(src[i]);
                while (r.Count < n) r.Add("");
                if (r.Count > n) r.RemoveRange(n, r.Count - n);
                rows.Add(r);
            }
            int[] w = new int[n];
            int[] al = new int[n];
            for (int c = 0; c < n; c++)
            {
                string d = delim[c];
                al[c] = d.StartsWith(":", StringComparison.Ordinal) && d.EndsWith(":", StringComparison.Ordinal) ? 1 : d.EndsWith(":", StringComparison.Ordinal) ? 2 : 0;
                w[c] = 3;
                foreach (List<string> r in rows) w[c] = Math.Max(w[c], r[c].Length);
            }
            List<string> o = new List<string>();
            for (int k = 0; k < rows.Count; k++)
            {
                StringBuilder sb = new StringBuilder("|");
                for (int c = 0; c < n; c++) sb.Append(' ').Append(Pad(rows[k][c], w[c], al[c])).Append(" |");
                o.Add(sb.ToString());
                if (k == 0)
                {
                    sb = new StringBuilder("|");
                    for (int c = 0; c < n; c++)
                    {
                        string bar = new string('-', w[c]);
                        if (al[c] == 1) bar = ":" + bar.Substring(2) + ":";
                        else if (al[c] == 2) bar = bar.Substring(1) + ":";
                        sb.Append(' ').Append(bar).Append(" |");
                    }
                    o.Add(sb.ToString());
                }
            }
            return o;
        }

        static string Pad(string s, int w, int align)
        {
            int gap = w - s.Length;
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
            toc.Add("## \u00CDndice");
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
