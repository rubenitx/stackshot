// Stackshot - "Novedades": what a version brings, inside the app, in the interface language (docs/novedades.md and
// its English, German and Korean twins, built into the program). The one running carries its own, and a release on
// GitHub carries the next one's between <!-- novedades --> markers, so it can be told before updating.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public static class News
    {
        public sealed class Item
        {
            public string Title, Text;
        }

        public const string Start = "<!-- novedades -->", End = "<!-- /novedades -->";

        static string embedded, testEn, testDe, testKo;
        static bool loaded;
        static readonly object gate = new object();
        static readonly Dictionary<string, string> read = new Dictionary<string, string>();

        // Tests: the news as if the program carried this text (the same for every language, or one file for each: a
        // language left out reads the Spanish one).
        internal static void UseForTests(string md) { embedded = md; loaded = true; testEn = testDe = testKo = md; }
        internal static void UseForTests(string es, string en, string de, string ko = null) { embedded = es; loaded = true; testEn = en; testDe = de; testKo = ko; }

        // docs/novedades.md, built into the program: the source the versions are listed from.
        static string Embedded()
        {
            return loaded ? embedded : Read("novedades.md");
        }

        // The file of the interface language (the .en, .de and .ko twins; null for Spanish or when it is missing). An
        // open window re-reads it on I18n.Changed: it rebuilds and asks again.
        static string Twin()
        {
            if (I18n.IsSpanish) return null;
            if (loaded) return I18n.IsGerman ? testDe : I18n.IsKorean ? testKo : testEn;
            return Read(I18n.IsGerman ? "novedades.de.md" : I18n.IsKorean ? "novedades.ko.md" : "novedades.en.md");
        }

        static string Read(string name)
        {
            lock (gate)
            {
                string cached;
                if (read.TryGetValue(name, out cached)) return cached;
            }
            string md = null;
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                    if (s != null) using (StreamReader r = new StreamReader(s, Encoding.UTF8)) md = r.ReadToEnd();
            }
            catch (Exception ex) { ShotStack.Log("Novedades: " + ex.Message); }
            lock (gate) read[name] = md;
            return md;
        }

        // The newest version described in the program and its news (none: null and an empty list).
        public static List<Item> Newest(out string version)
        {
            version = null;
            string md = Embedded();
            if (md == null) return new List<Item>();
            Match m = Regex.Match(md, @"^## (\d+\.\d+\.\d+)\s*$", RegexOptions.Multiline);
            if (!m.Success) return new List<Item>();
            version = m.Groups[1].Value;
            return Section(md, version);
        }

        // The news of exactly this version, as carried by the program (empty if it has none).
        public static List<Item> Of(string version)
        {
            string md = Embedded();
            return md == null ? new List<Item>() : Section(md, version);
        }

        // Every version the program describes after `seen` and up to `current`, newest first, with its news: what is
        // new to someone who last saw the news of `seen`. An empty `seen` (a version that kept no record, before 2.2.0)
        // gets them all.
        public static List<KeyValuePair<string, List<Item>>> Since(string seen, string current)
        {
            List<KeyValuePair<string, List<Item>>> all = new List<KeyValuePair<string, List<Item>>>();
            string md = Embedded();
            Version top, from = null;
            if (md == null || !Version.TryParse(current, out top)) return all;
            if (!string.IsNullOrEmpty(seen) && !Version.TryParse(seen, out from)) from = null;
            foreach (Match m in Regex.Matches(md, @"^## (\d+\.\d+\.\d+)\s*$", RegexOptions.Multiline))
            {
                Version v = new Version(m.Groups[1].Value);
                if (v > top || (from != null && v <= from)) continue;
                List<Item> items = Section(md, m.Groups[1].Value);
                if (items.Count > 0) all.Add(new KeyValuePair<string, List<Item>>(m.Groups[1].Value, items));
            }
            all.Sort(delegate(KeyValuePair<string, List<Item>> a, KeyValuePair<string, List<Item>> b) { return new Version(b.Key).CompareTo(new Version(a.Key)); });
            return all;
        }

        static List<Item> Section(string md, string version)
        {
            // The twin tells a version only when it has it: the Spanish list stays authoritative when a future release adds notes.
            string twin = Twin();
            if (twin != null && Regex.IsMatch(twin, @"^## " + Regex.Escape(version ?? "") + @"\s*$", RegexOptions.Multiline))
                md = twin;
            List<string> lines = new List<string>();
            bool inside = false;
            foreach (string raw in md.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("## ", StringComparison.Ordinal)) { inside = raw.Substring(3).Trim() == version; continue; }
                if (inside) lines.Add(raw);
            }
            return Bullets(lines);
        }

        // The news a release on GitHub carries for its version (empty if it has none, as releases before 2.2.0).
        public static List<Item> FromRelease(string body)
        {
            if (string.IsNullOrEmpty(body)) return new List<Item>();
            int a = body.IndexOf(Start, StringComparison.Ordinal), b = a < 0 ? -1 : body.IndexOf(End, a, StringComparison.Ordinal);
            if (a < 0 || b < 0) return new List<Item>();
            string part = body.Substring(a + Start.Length, b - a - Start.Length);
            return Bullets(new List<string>(part.Replace("\r", "").Split('\n')));
        }

        // "- **Title.** Text": the title and its explanation, without Markdown.
        static List<Item> Bullets(List<string> lines)
        {
            List<Item> items = new List<Item>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (!line.StartsWith("- ", StringComparison.Ordinal)) continue;
                line = line.Substring(2).Trim();
                Match m = Regex.Match(line, @"^\*\*(.+?)\*\*\s*(.*)$");
                Item it = m.Success ? new Item { Title = Plain(m.Groups[1].Value), Text = Plain(m.Groups[2].Value) } : new Item { Title = Plain(line), Text = "" };
                if (it.Title.Length > 0) items.Add(it);
                if (items.Count == 60) break;   // the window scrolls; a runaway body stays bounded
            }
            return items;
        }

        static string Plain(string s)
        {
            s = Regex.Replace(s, @"<[^>]+>|\*\*|`", "");
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");
            return s.Trim();
        }

        // A short line for a notice: the first titles, without their final full stop.
        public static string Headline(List<Item> items, int count)
        {
            List<string> t = new List<string>();
            for (int i = 0; i < items.Count && t.Count < count; i++) t.Add(items[i].Title.TrimEnd('.'));
            return string.Join(" \u00B7 ", t.ToArray());
        }
    }
}
