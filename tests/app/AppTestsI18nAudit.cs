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
            List<string> cut = new List<string>();
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
                            // A title that ends in an ellipsis loses its end: its natural width is wider than the room it got.
                            // (A row named after the user's own folder or device, "Windows default: Speakers (Model)", runs as long as that name.)
                            bool theirs = t.Text.Contains(":\\") || Regex.IsMatch(t.Text, @": .+ \(.+\)$");
                            if (!theirs && t.TextTrimming != TextTrimming.None && t.TextWrapping == TextWrapping.NoWrap)
                            {
                                TextBlock whole = new TextBlock { Text = t.Text, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle, FontStretch = t.FontStretch };
                                whole.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                if (whole.DesiredSize.Width > t.ActualWidth + 0.5)
                                    cut.Add(lang + "/" + page + ": " + t.Text + " (" + (int)whole.DesiredSize.Width + " > " + (int)t.ActualWidth + ")");
                            }
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
            foreach (string c in cut) Console.WriteLine("    cut title: " + System.Text.RegularExpressions.Regex.Replace(c, "[^\\x20-\\x7e]", "?"));
            Check(cut.Count == 0, cut.Count + " settings titles end in an ellipsis");
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

        // The one-time offer of a language is three lines at most at every scale, and it names both routes to the setting:
        // the one the user sees now (Spanish) and the one that will read after the switch. One register in Korean.
        public static void LanguageOfferFitsItsToast()
        {
            string[][] cases = { new[] { "en", "en-US" }, new[] { "en", "fr-FR" }, new[] { "en", "pt-BR" }, new[] { "en", "vi-VN" }, new[] { "de", "de-DE" }, new[] { "ko", "ko-KR" } };
            const string now = "Ajustes \u203A General \u203A Apariencia \u203A Idioma";
            Dictionary<string, string> after = new Dictionary<string, string>
            {
                { "en", "Settings \u203A General \u203A Appearance \u203A Language" },
                { "de", "Einstellungen \u203A Allgemein \u203A Erscheinungsbild \u203A Sprache" },
                { "ko", "\uC124\uC815 \u203A \uC77C\uBC18 \u203A \uD654\uBA74 \uBAA8\uC591 \u203A \uC5B8\uC5B4" }
            };
            Dictionary<string, string> opening = new Dictionary<string, string>
            {
                { "en", "Switch to English? You can change it later in " + now + " (" + after["en"] + ")." },
                { "de", "Auf Deutsch umstellen? \u00C4nderbar unter " + now + " (" + after["de"] + ")." },
                { "ko", "\uD55C\uAD6D\uC5B4\uB85C \uBC14\uAFC0\uAE4C\uC694? \uB098\uC911\uC5D0 " + now + " (" + after["ko"] + ")\uC5D0\uC11C \uBC14\uAFC0 \uC218 \uC788\uC5B4\uC694." }
            };
            List<string> bad = new List<string>();
            foreach (string[] c in cases)
            {
                string title, body, button;
                ShotStack.OfferTexts(c[0], out title, out body, out button);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                {
                    Notice n = new Notice(title, body, NoticeKind.Info);
                    n.ActionLabel = button;
                    n.Action = delegate { };
                    ToastLook look = ToastLook.For(n, scale);
                    if (look.Clipped || look.TitleClipped) bad.Add(c[1] + " -> " + c[0] + " at " + (int)(scale * 100) + " %: clipped (" + (look.TitleClipped ? "title" : "text") + ", " + look.Lines + " lines)");
                }
                if (body != opening[c[0]]) bad.Add(c[1] + " -> " + c[0] + ": the wording is '" + Regex.Replace(body, "[^\\x20-\\x7e]", "?") + "'");
                if (c[0] == "ko" && body.Contains("\uC2B5\uB2C8\uB2E4")) bad.Add(c[1] + ": Korean mixes the formal register into the offer");
            }
            foreach (string b in bad) Console.WriteLine("    offer: " + b);
            Check(bad.Count == 0, bad.Count + " language offers do not fit their notice or word their route wrongly");
        }

        // The three steps of an update read "Stackshot {0} ..." in every language, in the same order: Korean puts the
        // verb last, so "Verifying" must not lead with it while the others lead with the name.
        public static void KoreanUpdateStepsSameOrder()
        {
            I18n.Apply("ko");
            string down = I18n.Fmt("Descargando Stackshot {0}", "Downloading Stackshot {0}", "9.9.9");
            string verify = I18n.Fmt("Comprobando Stackshot {0}", "Verifying Stackshot {0}", "9.9.9");
            string install = I18n.Fmt("Instalando Stackshot {0}", "Installing Stackshot {0}", "9.9.9");
            foreach (string t in new[] { down, verify, install })
                Check(t.StartsWith("Stackshot 9.9.9 ", StringComparison.Ordinal), "Korean update step does not start with the name: " + Regex.Replace(t, "[^\\x20-\\x7e]", "?"));
        }

        // The German title of the fail-closed notice has to fit its body, which speaks of "sie" (the capture): it says
        // the capture was not checked, not that a check failed.
        public static void GermanDiscardTitleMatchesItsBody()
        {
            AppTestsI18nCoverage.TitleSite site = AppTestsI18nCoverage.NoticeTitleSites().FirstOrDefault(s => s.Es == "No se pudo revisar la captura");
            Check(site != null && site.De != null, "the capture-not-checked notice title was not found");
            Check(site.De == "Aufnahme nicht gepr\u00FCft", "German title is '" + Regex.Replace(site.De, "[^\\x20-\\x7e]", "?") + "'");
        }

        // The sensitive-data notices are read in a toast of three lines at every scale: the body must not end in an ellipsis.
        public static void SensitiveNoticeBodiesFit()
        {
            List<string> cut = new List<string>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
            {
                I18n.Apply(lang);
                string title = PolicyTitles.Sensitive;
                Dictionary<string, string> bodies = new Dictionary<string, string>();
                bodies["discard"] = ShotStack.DiscardBody();
                bodies["no OCR"] = ShotStack.OcrMissingBody(title, false);
                bodies["no OCR (policy)"] = ShotStack.OcrMissingBody(title, true);
                foreach (KeyValuePair<string, string> b in bodies)
                    foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                    {
                        ToastLook look = ToastLook.For(new Notice("Titulo", b.Value, NoticeKind.Warning), scale);
                        if (look.Clipped) cut.Add(lang + " " + b.Key + " at " + (int)(scale * 100) + " %: " + look.Lines + " lines, " + b.Value.Length + " chars");
                    }
            }
            foreach (string c in cut) Console.WriteLine("    cut body: " + c);
            Check(cut.Count == 0, cut.Count + " sensitive-data notice bodies are cut");
        }

        // Releases from before the English news were written by older workflows: only the Spanish markers, CRLF, no markers,
        // a start marker without its end, an empty body. Spanish still reads the Spanish news; any other language never
        // reads them; nothing throws and a notice never shows an empty "What's new".
        public static void OldReleaseBodiesStillRead()
        {
            string bullets = "- **Fixed the preview.** Better.\n- **Smaller files** for videos.\n";
            string esNews = News.Start + "\n- **" + SpanishHeadline + ".** Cuerpo.\n" + News.End;
            Dictionary<string, string> bodies = new Dictionary<string, string>();
            bodies["spanish markers only"] = bullets + "\n<details><summary>Novedades</summary>\n\n" + esNews + "\n\n</details>\n";
            bodies["crlf"] = (bullets + "\n<details><summary>Novedades</summary>\n\n" + esNews + "\n\n</details>\n").Replace("\n", "\r\n");
            bodies["no markers"] = bullets;
            bodies["start without end"] = bullets + "\n" + News.Start + "\n- **" + SpanishHeadline + ".** Cuerpo.\n";
            bodies["empty"] = "";
            foreach (KeyValuePair<string, string> b in bodies)
            {
                Updater.Release r = ParseRelease(b.Value);
                bool hasEs = b.Key == "spanish markers only" || b.Key == "crlf";
                Check((r.News.Count == 1) == hasEs, b.Key + ": Spanish news read = " + r.News.Count);
                Check(r.NewsEn.Count == 0, b.Key + ": English news appeared from nowhere");
                I18n.Apply("es");
                List<News.Item> es = r.Items();
                if (hasEs) Check(es.Count == 1 && es[0].Title.Contains(SpanishHeadline), b.Key + ": Spanish does not read the Spanish news");
                else Check((es.Count > 0) == (b.Key != "empty"), b.Key + ": Spanish items = " + es.Count);
                Check(NoticeBody(r) != null, b.Key + ": Spanish notice body");
                foreach (string lang in new[] { "en", "de", "ko", "fr", "ar" })
                {
                    I18n.Apply(lang);
                    List<News.Item> items = r.Items();
                    foreach (News.Item it in items)
                        Check(it.Title.IndexOf(SpanishHeadline, StringComparison.Ordinal) < 0 && it.Text.IndexOf("Cuerpo", StringComparison.Ordinal) < 0, b.Key + ": " + lang + " shows the Spanish news");
                    Check((items.Count > 0) == (b.Key != "empty"), b.Key + ": " + lang + " items = " + items.Count);
                    string text = NoticeBody(r);
                    Check(text.IndexOf(SpanishHeadline, StringComparison.Ordinal) < 0, b.Key + ": " + lang + " notice shows the Spanish news: " + text);
                    if (items.Count == 0) Check(text.IndexOf("What's new", StringComparison.Ordinal) < 0 && text.IndexOf("Novedades", StringComparison.Ordinal) < 0, b.Key + ": " + lang + " announces news it does not have: " + text);
                }
            }
            I18n.Apply("es");
        }

        // The release workflow is what puts the news between the markers the app reads: it writes both the Spanish and
        // the English ones, from the Spanish and English news files, and the newest section of each has bullets to write.
        public static void WorkflowWritesTheMarkers()
        {
            string root = AppTestsI18nCoverage.RepoRoot();
            string yml = File.ReadAllText(Path.Combine(root, ".github", "workflows", "build.yml"));
            foreach (string marker in new[] { News.Start, News.End, News.StartEn, News.EndEn })
                Check(yml.Contains("echo \"" + marker + "\""), "build.yml does not write " + marker);
            Check(yml.Contains("docs/novedades.md") && yml.Contains("docs/novedades.en.md"), "build.yml does not read both news files");
            Check(yml.IndexOf(News.Start, StringComparison.Ordinal) < yml.IndexOf(News.StartEn, StringComparison.Ordinal), "the Spanish block is written before the English one");
            foreach (string file in new[] { "novedades.md", "novedades.en.md" })
            {
                string[] lines = File.ReadAllLines(Path.Combine(root, "docs", file));
                int head = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^## \d+\.\d+\.\d+\s*$"));
                Check(head >= 0, file + " has no version section");
                int bullets = 0;
                for (int i = head + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++) if (lines[i].StartsWith("- ", StringComparison.Ordinal)) bullets++;
                Check(bullets > 0, file + ": the newest version has no news for the workflow to write");
            }
        }
    }
}
