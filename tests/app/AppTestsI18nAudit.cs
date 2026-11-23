// Suite "i18n", audit regressions: layout and text checks that every interface language has to pass.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Stackshot.Tests
{
    static class AppTestsI18nAudit
    {
        static readonly string[] AllLanguages = { "es", "en", "de", "ko", "fr", "pt", "zh", "hi", "ar", "bn" };
        static readonly string[] SettingsPages = { "keys", "general", "editor", "record", "mascotset" };

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        // A row whose control takes the width leaves the text a sliver: the title shrinks to a few pixels and the
        // description wraps to one letter per line. Every Settings page, in every language, at the window's usual size.
        public static void SettingsRowsNotSqueezed()
        {
            List<string> squeezed = new List<string>();
            foreach (string lang in AllLanguages)
            {
                I18n.Apply(lang);
                HomeWindow hw = AppTestsI18n.Make(1);
                try
                {
                    foreach (string page in SettingsPages)
                    {
                        AppTestsI18n.SetPage(hw, page);
                        AppTestsI18n.Layout(hw);
                        AppTestsI18n.Walk(hw.Content as DependencyObject, new HashSet<DependencyObject>(), delegate(DependencyObject d)
                        {
                            TextBlock t = d as TextBlock;
                            if (t == null || string.IsNullOrEmpty(t.Text) || t.ActualWidth <= 0) return;
                            double line = t.FontSize * 1.3;
                            if (t.ActualWidth < 40 && t.ActualHeight > 3.5 * line)
                                squeezed.Add(lang + "/" + page + ": " + System.Text.RegularExpressions.Regex.Replace(t.Text, "[^\\x20-\\x7e]", "?") + " (" + (int)t.ActualWidth + " x " + (int)t.ActualHeight + ")");
                        });
                    }
                }
                finally { hw.Shutdown(); }
            }
            foreach (string s in squeezed) Console.WriteLine("    squeezed: " + s);
            Check(squeezed.Count == 0, squeezed.Count + " squeezed text blocks");
        }

        // Texts that are the same in every language: formats and product names.
        static readonly string[] SameEverywhere = { "Markdown", "GIF", "PDF", "OK", "HTML", "XML", "OCR", "Stackshot" };

        // Words German spells the same as English.
        static readonly string[] GermanSameWords = { "Extras", "Video", "Standard", "Maximum", "Text", "Timer" };

        // A list of English lines beside its Spanish partner (FooEn / FooEs) with no German partner (FooDe) is translated
        // through the catalog, one entry at a time: an entry the catalog lacks reaches the screen in English.
        static List<string> ListLeaks(string lang)
        {
            List<string> leaks = new List<string>();
            foreach (Type type in typeof(I18n).Assembly.GetTypes())
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo en in fields)
                {
                    if (en.FieldType != typeof(string[]) || !en.Name.EndsWith("En", StringComparison.Ordinal)) continue;
                    string stem = en.Name.Substring(0, en.Name.Length - 2);
                    FieldInfo es = fields.FirstOrDefault(f => f.Name == stem + "Es" && f.FieldType == typeof(string[]));
                    if (es == null || fields.Any(f => f.Name == stem + "De")) continue;
                    string[] enList = (string[])en.GetValue(null), esList = (string[])es.GetValue(null);
                    for (int i = 0; i < enList.Length; i++)
                    {
                        string english = enList[i];
                        if (string.IsNullOrWhiteSpace(english) || SameEverywhere.Contains(english) || (lang == "de" && GermanSameWords.Contains(english))) continue;
                        if (!english.Any(char.IsLetter)) continue;
                        string shown = I18n.T(i < esList.Length ? esList[i] : english, english);
                        if (shown == english) leaks.Add(type.Name + "." + en.Name + "[" + i + "] " + english);
                    }
                }
            }
            return leaks;
        }

        static void ListsLeak(string lang)
        {
            I18n.Apply(lang);
            List<string> leaks = ListLeaks(lang);
            foreach (string l in leaks) Console.WriteLine("    english in " + lang + ": " + l);
            Check(leaks.Count == 0, leaks.Count + " list entries stay in English in " + lang);
        }

        public static void EveryListTextHasGerman() { ListsLeak("de"); }
        public static void EveryListTextHasKorean() { ListsLeak("ko"); }

        const string SpanishHeadline = "Titular s\u00F3lo en espa\u00F1ol";

        static Updater.Release ParseRelease(string body)
        {
            string json = "{\"tag_name\":\"v9.9.9\",\"body\":" + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(body) + "}";
            return (Updater.Release)typeof(Updater).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { json });
        }

        static string NoticeBody(Updater.Release r)
        {
            UpdateNotice w = new UpdateNotice((ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack)), r);
            try { return (string)typeof(UpdateNotice).GetMethod("Body", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(w, null); }
            finally { w.Dispose(); }
        }

        // The release workflow writes the Spanish news between markers; a notice in any other language must not show them.
        public static void ReleaseNewsOnlyInSpanish()
        {
            string body = "- **Recording works again** after a restart.\n- **Smaller files** for videos.\n\n" +
                "<details><summary>Novedades en espa\u00F1ol</summary>\n\n" + News.Start + "\n- **" + SpanishHeadline + ".** Cuerpo.\n" + News.End + "\n\n</details>\n";
            Updater.Release r = ParseRelease(body);
            foreach (string lang in new[] { "en", "de", "ko" })
            {
                I18n.Apply(lang);
                string text = NoticeBody(r);
                Check(text.IndexOf(SpanishHeadline, StringComparison.Ordinal) < 0, lang + ": the notice shows the Spanish news: " + text);
            }
            I18n.Apply("en");
            Check(NoticeBody(r).Contains("Recording works again"), "English: the notice carries the English changelog line");
            I18n.Apply("es");
            Check(NoticeBody(r).Contains(SpanishHeadline), "Spanish: the notice carries the Spanish news");

            // A release that also carries the English news (the workflow writes both): every other language reads those.
            string both = body + "\n<details><summary>What's new in English</summary>\n\n" + News.StartEn + "\n- **Headline in English.** Body.\n" + News.EndEn + "\n\n</details>\n";
            Updater.Release r2 = ParseRelease(both);
            Check(r2.News.Count == 1 && r2.NewsEn.Count == 1, "both news blocks are read apart");
            foreach (string lang in new[] { "en", "de", "ko" })
            {
                I18n.Apply(lang);
                string text = NoticeBody(r2);
                Check(text.IndexOf(SpanishHeadline, StringComparison.Ordinal) < 0, lang + ": Spanish news with English ones present: " + text);
                Check(text.Contains("Headline in English"), lang + ": the English news are not shown: " + text);
            }
            I18n.Apply("es");
            Check(NoticeBody(r2).Contains(SpanishHeadline) && !NoticeBody(r2).Contains("Headline in English"), "Spanish keeps the Spanish news");
        }

        // The Korean catalog is a dictionary filled by assignments: a key assigned twice keeps only the last value, so
        // the first one is dead text that looks like it counts (and hides which wording the screen shows).
        public static void KoreanKeysNotRegisteredTwiceDifferently()
        {
            Regex assign = new Regex(@"map\[""((?:[^""\\]|\\.)*)""\]\s*=\s*""((?:[^""\\]|\\.)*)""\s*;");
            Dictionary<string, KeyValuePair<string, string>> seen = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
            List<string> twice = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(AppTestsI18nCoverage.RepoRoot(), "src"), "I18n.Korean*.cs"))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    Match m = assign.Match(lines[i]);
                    if (!m.Success) continue;
                    string key = Regex.Unescape(m.Groups[1].Value), value = Regex.Unescape(m.Groups[2].Value);
                    string at = Path.GetFileName(path) + ":" + (i + 1);
                    KeyValuePair<string, string> first;
                    if (!seen.TryGetValue(key, out first)) seen[key] = new KeyValuePair<string, string>(value, at);
                    else if (first.Key != value) twice.Add("'" + key + "' at " + at + " and " + first.Value);
                }
            }
            foreach (string t in twice) Console.WriteLine("    registered twice: " + Regex.Replace(t, "[^\\x20-\\x7e]", "?"));
            Check(twice.Count == 0, twice.Count + " Korean keys are registered twice with different values");
        }

        // A notice title has one line of the toast, 268 px at 100 % (and its share at every scale): a longer one ends in an ellipsis and loses its end.
        public static void NoticeTitlesFitTheirLine()
        {
            List<AppTestsI18nCoverage.TitleSite> sites = AppTestsI18nCoverage.NoticeTitleSites();
            Check(sites.Count > 40, "only " + sites.Count + " notice titles found: the scan lost them");
            List<string> cut = new List<string>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
            {
                I18n.Apply(lang);
                foreach (AppTestsI18nCoverage.TitleSite s in sites)
                {
                    string title = s.Fmt ? string.Format(CultureInfo.InvariantCulture, I18n.T(s.Es, s.En), new object[] { "Stackshot", "2", "3" }) :
                                   s.De != null ? I18n.T(s.Es, s.En, s.De) : I18n.T(s.Es, s.En);
                    foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                        if (ToastLook.For(new Notice(title, "", NoticeKind.Info), scale).TitleClipped)
                            cut.Add(lang + " " + s.Where + " at " + (int)(scale * 100) + " %: " + Regex.Replace(title, "[^\\x20-\\x7e]", "?"));
                }
            }
            foreach (string c in cut) Console.WriteLine("    cut: " + c);
            Check(cut.Count == 0, cut.Count + " notice titles are cut with an ellipsis");
        }

        // The one-time offer of a language is three lines at most; what the toast cannot show is the route to the setting.
        public static void LanguageOfferFitsItsToast()
        {
            string[][] cases = { new[] { "en", "en-US" }, new[] { "en", "fr-FR" }, new[] { "en", "pt-BR" }, new[] { "en", "vi-VN" }, new[] { "de", "de-DE" }, new[] { "ko", "ko-KR" } };
            List<string> bad = new List<string>();
            foreach (string[] c in cases)
            {
                string title, body, button;
                ShotStack.OfferTexts(c[0], new CultureInfo(c[1]), out title, out body, out button);
                Notice n = new Notice(title, body, NoticeKind.Info);
                n.ActionLabel = button;
                n.Action = delegate { };
                ToastLook look = ToastLook.For(n, 1f);
                if (look.Clipped || look.TitleClipped) bad.Add(c[1] + " -> " + c[0] + ": clipped (" + (look.TitleClipped ? "title" : "text") + ", " + look.Lines + " lines)");
                int at = body.IndexOf("\u203A", StringComparison.Ordinal);
                if (c[0] != "ko" && at < 0) bad.Add(c[1] + ": the route to the setting is missing");
            }
            foreach (string b in bad) Console.WriteLine("    offer: " + b);
            Check(bad.Count == 0, bad.Count + " language offers do not fit their notice");
        }
    }
}
