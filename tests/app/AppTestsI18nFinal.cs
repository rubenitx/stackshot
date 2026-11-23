// Suite "i18n", the last audit's findings: file names, Korean particles, German grammar and Pokemon names, the report
// status word, wellness texts, the mascot's name in any script, and the news a release carries in German and Korean.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot.Tests
{
    static class AppTestsI18nFinal
    {
        // The tests of this file, under the names the suite lists them by.
        public static void Suite(Action<string, Action> T)
        {
            T("export-suffixes-follow-the-language", ExportSuffixesFollowTheLanguage);
            T("korean-particles-follow-the-word", KoreanParticlesFollowTheWord);
            T("report-status-open-is-not-the-verb", ReportStatusOpenIsNotTheVerb);
            T("wellness-title-keeps-german-nouns", WellnessTitleKeepsGermanNouns);
            T("german-title-hint-is-a-sentence", GermanTitleHintIsASentence);
            T("german-pokemon-have-german-names", GermanPokemonHaveGermanNames);
            T("korean-wellness-intervals", KoreanWellnessIntervals);
            T("german-update-errors-are-sentences", GermanUpdateErrorsAreSentences);
            T("korean-zoom-text-is-spaced", KoreanZoomTextIsSpaced);
            T("player-audio-button-is-localized", PlayerAudioButtonIsLocalized);
            T("report-machine-line-bits", ReportMachineLineBits);
            T("german-split-is-not-share", GermanSplitIsNotShare);
            T("korean-particles-never-edit-values", KoreanParticlesNeverEditValues);
            T("opening-several-files-notice-uses-fmt", OpeningSeveralFilesNoticeUsesFmt);
            T("mascot-name-is-clean-wherever-it-comes-from", MascotNameIsCleanWhereverItComesFrom);
            T("mascot-name-field-never-alters-typing", MascotNameFieldNeverAltersTyping);
            T("mascot-name-cut-edge-cases", MascotNameCutEdgeCases);
            T("settings-rows-exempt-their-paths", SettingsRowsExemptTheirPaths);
            T("release-notices-read-their-language", ReleaseNoticesReadTheirLanguage);
            T("last-audit-screens-as-pictures", Pictures);
        }

        static string[] AllLanguages { get { return AppTestsI18nLayout.AllLanguages; } }

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static void Same(string expected, string actual, string what)
        {
            if (expected != actual) throw new InvalidOperationException(what + ": expected <" + Show(expected) + ">, got <" + Show(actual) + ">");
        }

        static string Show(string s) { return s == null ? "null" : Regex.Replace(s, "[^\\x20-\\x7e]", m => "\\u" + ((int)m.Value[0]).ToString("X4")); }

        static string Src(string rel) { return File.ReadAllText(Path.Combine(AppTestsI18nCoverage.RepoRoot(), "src", rel), Encoding.UTF8); }

        static void InLanguage(string lang, Action body)
        {
            I18n.Apply(lang);
            try { body(); }
            finally { I18n.Apply("es"); }
        }

        // Where each export adds a word to the video's name: Spanish keeps its words, German, Korean and English have their
        // own, and the preview languages use the English ones. Nothing in the program reads the words back.
        public static void ExportSuffixesFollowTheLanguage()
        {
            Dictionary<string, string[]> want = new Dictionary<string, string[]>();
            want["es"] = new[] { " (tramo)", " (editado)", " (para compartir)", " (sin validar)" };
            want["en"] = new[] { " (clip)", " (edited)", " (for sharing)", " (unverified)" };
            want["de"] = new[] { " (Ausschnitt)", " (bearbeitet)", " (zum Teilen)", " (ungepr\u00FCft)" };
            want["ko"] = new[] { " (\uAD6C\uAC04)", " (\uD3B8\uC9D1\uB428)", " (\uACF5\uC720\uC6A9)", " (\uBBF8\uAC80\uC99D)" };
            foreach (string lang in AllLanguages)
            {
                I18n.Apply(lang);
                string[] w = want.ContainsKey(lang) ? want[lang] : want["en"];
                string[] got = { ExportName.Clip, ExportName.Edited, ExportName.ForSharing, ExportName.Unverified };
                for (int i = 0; i < 4; i++) Same(w[i], got[i], lang + " export word " + i);
                if (lang != "es")
                    foreach (string s in got)
                        Check(!Regex.IsMatch(s, "tramo|editado|compartir|validar"), lang + " export word is Spanish: " + s);
            }
            I18n.Apply("es");
            // No call site spells the Spanish words itself.
            List<string> bad = new List<string>();
            foreach (string f in Directory.GetFiles(Path.Combine(AppTestsI18nCoverage.RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(f) == "ExportName.cs") continue;
                string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                    if (!lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && Regex.IsMatch(lines[i], "\\((tramo|editado|para compartir|sin validar)\\)"))
                        bad.Add(Path.GetFileName(f) + ":" + (i + 1));
            }
            Check(bad.Count == 0, "Spanish export words spelled in code: " + string.Join(", ", bad));
        }

        // A particle after a name or a number follows how that ends. Latin-script names say nothing about the sound.
        public static void KoreanParticlesFollowTheWord()
        {
            InLanguage("ko", delegate
            {
                string sky = "\uD558\uB298", butterfly = "\uB098\uBE44";
                // The mascot's menu label has no particle at all: the same wording for every name, Latin script or not.
                foreach (string name in new[] { sky, butterfly, "Pixel", "\uD83D\uDE00", "50%" })
                {
                    Same(name + "\uD558\uACE0 \uB180\uAE30", I18n.Fmt("", "Play with {0}", name), "play with " + name);
                    Same(name + "\uD558\uACE0 \uB180\uAE30", MascotTalk.PlayWith(name), "the menu label for " + name);
                }
                Same("Pixel\uC744(\uB97C) \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\nx", I18n.Fmt("", "Could not open {0}.\n\n{1}", "Pixel", "x"), "an unknown ending keeps the pair");
                Same(sky + "\uC744 \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\nx", I18n.Fmt("", "Could not open {0}.\n\n{1}", sky, "x"), "pair settled by the name");

                string latest = "Stackshot {0} is the latest version. Checked {1}.";
                Check(I18n.Fmt("", latest, "2.2.3", "x").StartsWith("Stackshot 2.2.3\uC774 \uCD5C\uC2E0", StringComparison.Ordinal), "2.2.3 reads sam: " + I18n.Fmt("", latest, "2.2.3", "x"));
                Check(I18n.Fmt("", latest, "2.2.2", "x").StartsWith("Stackshot 2.2.2\uAC00 \uCD5C\uC2E0", StringComparison.Ordinal), "2.2.2 reads i: " + I18n.Fmt("", latest, "2.2.2", "x"));
                Check(I18n.Fmt("", "This machine has Stackshot {0} installed via MSI package.\n\nTo update to {1}, install Stackshot.msi for that version (or ask IT).", "2.2.2", "2.3.0").Contains("Stackshot 2.2.2\uAC00 "), "MSI notice with 2.2.2");
                Check(I18n.Fmt("", "FFmpeg exited with code {0}.", 1).Contains("\uCF54\uB4DC 1\uC744 "), "exit code 1");
                Check(I18n.Fmt("", "FFmpeg exited with code {0}.", 2).Contains("\uCF54\uB4DC 2\uB97C "), "exit code 2");

                Same("\uACFC", I18n.Josa(sky, "\uACFC", "\uC640"), "Josa after a final consonant");
                Same("\uC640", I18n.Josa(butterfly, "\uACFC", "\uC640"), "Josa after a vowel");
                Same("\uACFC(\uC640)", I18n.Josa("Pixel", "\uACFC", "\uC640"), "Josa after a Latin name");
                Same("\uB294", I18n.Josa(butterfly, "\uC740", "\uB294"), "topic particle after a vowel");
                Same("\uC774", I18n.Josa(sky, "\uC774", "\uAC00"), "subject particle after a consonant");
                Same("\uB97C", I18n.Josa("2", "\uC744", "\uB97C"), "a number read \uC774 ends in a vowel");
                Same("\uC744", I18n.Josa("3", "\uC744", "\uB97C"), "a number read \uC0BC ends in a consonant");
                Same("\uAC00", I18n.Josa("2", "\uC774", "\uAC00"), "2 is read \uC774, no final consonant");
                Check(I18n.KoreanFinal("MP3") == null, "a digit glued to letters is not settled");
                Check(I18n.KoreanFinal("50%") == null && I18n.KoreanFinal("\uD558\uB298\uD83D\uDE00") == null && I18n.KoreanFinal("") == null && I18n.KoreanFinal(null) == null, "%, an emoji and nothing are not settled");
                Check(I18n.KoreanFinal("\uD558\uB298!") == true && I18n.KoreanFinal("\u201C\uB098\uBE44\u201D") == false, "closing punctuation does not count");
                Check(I18n.KoreanFinal("2.2.3") == true && I18n.KoreanFinal("2.2.2") == false, "version numbers by their last digit");

                // No catalog text pins a particle after a variable: the pair is written and settled when it is filled in.
                FieldInfo f = typeof(I18n).GetField("KoreanCatalog", BindingFlags.NonPublic | BindingFlags.Static);
                object lazy = f.GetValue(null);
                Dictionary<string, string> catalog = (Dictionary<string, string>)lazy.GetType().GetProperty("Value").GetValue(lazy, null);
                List<string> pinned = new List<string>();
                Regex bare = new Regex("\\{\\d\\}[\uC640\uACFC\uC740\uB294\uC774\uAC00\uC744\uB97C](?![\uAC00-\uD7A3(])");
                foreach (KeyValuePair<string, string> p in catalog)
                    if (bare.IsMatch(p.Value)) pinned.Add(p.Key + " => " + Show(p.Value));
                foreach (string s in pinned) Console.WriteLine("    pinned particle: " + s);
                Check(pinned.Count == 0, pinned.Count + " Korean texts pin a particle after a variable");
            });
        }

        // "Open" as the state of a report is not the verb the menus use.
        public static void ReportStatusOpenIsNotTheVerb()
        {
            Dictionary<string, string> want = new Dictionary<string, string> { { "es", "Abierto" }, { "en", "Open" }, { "de", "Offen" }, { "ko", "\uC9C4\uD589 \uC911" } };
            foreach (string lang in AllLanguages)
            {
                I18n.Apply(lang);
                Same(want.ContainsKey(lang) ? want[lang] : "Open", ReportWindow.OpenStatus, lang + " report status");
                if (lang == "de" || lang == "ko") Check(ReportWindow.OpenStatus != I18n.Open, lang + ": the status is the verb " + I18n.Open);
            }
            I18n.Apply("es");
            Check(!Src("ReportWindow.cs").Contains("I18n.T(\"Abierto\", \"Open\")"), "ReportRow still takes the verb's translation");
        }

        // A German title keeps the capitals its nouns have; Spanish and English still write the break in lower case.
        public static void WellnessTitleKeepsGermanNouns()
        {
            InLanguage("de", delegate
            {
                Same("Pixel: Wasser trinken", ShotStack.WellTitle("Pixel", WellKind.Water), "German water title");
                for (int k = 0; k < WellKind.Count; k++)
                {
                    string t = ShotStack.WellTitle("Pixel", k);
                    string what = t.Substring("Pixel: ".Length);
                    Check(what.Length > 0 && char.IsUpper(what[0]), "German title after the name starts lower case: " + t);
                }
            });
            InLanguage("es", delegate { Same("Pixel: beber agua", ShotStack.WellTitle("Pixel", WellKind.Water), "Spanish title"); });
            InLanguage("en", delegate { Same("Pixel: drink water", ShotStack.WellTitle("Pixel", WellKind.Water), "English title"); });
            InLanguage("ko", delegate { Same("Pixel: " + WellKind.Names[WellKind.Water], ShotStack.WellTitle("Pixel", WellKind.Water), "Korean title"); });
        }

        // The report form's hint on the title and on the other field: "braucht noch 1 Buchstaben" (accusative), "... 3 Buchstaben".
        public static void GermanTitleHintIsASentence()
        {
            MethodInfo letters = typeof(ReportWindow).GetMethod("Letters", BindingFlags.NonPublic | BindingFlags.Static);
            InLanguage("de", delegate
            {
                foreach (int n in new[] { 1, 3 })
                {
                    string hint = I18n.Fmt("El t\u00EDtulo necesita {0} m\u00E1s.", "Title needs {0} more.", (string)letters.Invoke(null, new object[] { n }));
                    Same(n == 1 ? "Der Titel braucht noch 1 Buchstaben." : "Der Titel braucht noch 3 Buchstaben.", hint, "German title hint for " + n);
                    string field = I18n.Fmt("{0} necesita {1} m\u00E1s.", "{0} needs {1} more.", "Thema", (string)letters.Invoke(null, new object[] { n }));
                    Same(n == 1 ? "Thema braucht noch 1 Buchstaben." : "Thema braucht noch 3 Buchstaben.", field, "German field hint for " + n);
                }
            });
            InLanguage("es", delegate { Same("El t\u00EDtulo necesita 3 letras m\u00E1s.", I18n.Fmt("El t\u00EDtulo necesita {0} m\u00E1s.", "Title needs {0} more.", (string)letters.Invoke(null, new object[] { 3 })), "Spanish hint unchanged"); });
        }

        static readonly string[] EnglishPokemon = { "Eevee", "Charmander", "Bulbasaur", "Squirtle", "Jigglypuff", "Snorlax", "Psyduck", "Meowth", "Piplup", "Mimikyu", "Dragonite", "Mewtwo" };
        // What their cries are cut from in English: the part of the name a Pokemon says.
        static readonly string[] EnglishCries = { "Vee!", "Char!", "Bulba!", "Jiggly", "Psy\u2026", "Pip pip", "Mimiky" };

        // In German the Pokemon have their German names (Bisasam, Glumanda, Schiggy, ...): neither the costume list nor
        // what they say may carry the English ones.
        public static void GermanPokemonHaveGermanNames()
        {
            InLanguage("de", delegate
            {
                List<string> bad = new List<string>();
                foreach (string n in MascotParts.AnimeNames)
                    if (EnglishPokemon.Contains(n)) bad.Add("costume " + n);
                foreach (string n in new[] { "Evoli", "Glumanda", "Bisasam", "Schiggy", "Pummeluff", "Relaxo", "Enton", "Mauzi", "Plinfa", "Mimigma", "Dragoran" })
                    if (!MascotParts.AnimeNames.Contains(n)) bad.Add("no costume " + n);
                int[] kinds = { MascotParts.KindEevee, MascotParts.KindCharmander, MascotParts.KindBulbasaur, MascotParts.KindSquirtle, MascotParts.KindJigglypuff, MascotParts.KindSnorlax,
                                MascotParts.KindPsyduck, MascotParts.KindMeowth, MascotParts.KindPiplup, MascotParts.KindMimikyu, MascotParts.KindDragonite, MascotParts.KindMewtwo,
                                MascotParts.KindPikachu, MascotParts.KindGengar, MascotParts.KindMew, MascotParts.KindTogepi, MascotParts.KindLucario, MascotParts.KindDitto };
                foreach (int kind in kinds)
                {
                    string[] lines = MascotParts.OwnLines(kind);
                    if (lines == null) continue;
                    foreach (string line in lines)
                        foreach (string name in EnglishPokemon.Concat(EnglishCries))
                            if (line.IndexOf(name, StringComparison.Ordinal) >= 0) bad.Add(line + " (" + name + ")");
                }
                // The catalog's German cries, which the same lines reach through the English key.
                Same("Evoli!", I18n.T("\u00A1Eevee!", "Eevee!"), "German Eevee cry");
                Same("Glum!", I18n.T("\u00A1Char!", "Char!"), "German Charmander cry");
                foreach (string s in bad) Console.WriteLine("    english Pokemon in German: " + s);
                Check(bad.Count == 0, bad.Count + " English Pokemon names in German");
            });
            InLanguage("es", delegate { Check(MascotParts.AnimeNames.Contains("Bulbasaur") && MascotParts.AnimeNames.Contains("Eevee"), "Spanish keeps the names it has"); });
        }

        // Wellness reminders in Korean say their units like every other duration.
        public static void KoreanWellnessIntervals()
        {
            InLanguage("ko", delegate
            {
                string[] names = WellKind.MinuteNames;
                string[] want = { null, "20\uBD84", "30\uBD84", "45\uBD84", "1\uC2DC\uAC04", "1\uC2DC\uAC04 30\uBD84", "2\uC2DC\uAC04" };
                Check(names.Length == want.Length, "seven choices");
                for (int i = 1; i < want.Length; i++) Same(want[i], names[i], "Korean interval " + i);
            });
            foreach (string lang in new[] { "es", "en", "de", "fr" })
                InLanguage(lang, delegate
                {
                    string[] names = WellKind.MinuteNames;
                    for (int i = 1; i < 7; i++) Same(new[] { null, "20 min", "30 min", "45 min", "1 h", "1 h 30", "2 h" }[i], names[i], lang + " interval " + i);
                });
        }

        // German update failures are sentences with a verb and a capital, in the notice and in Acerca de.
        public static void GermanUpdateErrorsAreSentences()
        {
            MethodInfo why = typeof(Updater).GetMethod("Why", BindingFlags.NonPublic | BindingFlags.Static);
            Func<Exception, bool, string> run = (ex, download) => (string)why.Invoke(null, new object[] { ex, download });
            InLanguage("de", delegate
            {
                string download = I18n.T("descargar la actualizaci\u00F3n", "download the update");
                Same("Herunterladen des Updates", download, "German name of the download");
                string[] messages =
                {
                    run(new Exception("x"), true), run(new Exception("x"), false), run(new InvalidDataException("kaputt."), true), run(new InvalidOperationException("x"), false),
                    I18n.Fmt("El proxy de la red no deja {0}.", "The network proxy does not let Stackshot {0}.", download),
                    I18n.Fmt("No se pudo {0} (error {1}).", "Could not {0} (error {1}).", download, 404)
                };
                Same("Beim Herunterladen des Updates ist ein Fehler aufgetreten. Es wird sp\u00E4ter erneut versucht.", messages[0], "unknown failure");
                Same("Beim Abfragen von GitHub ist ein Fehler aufgetreten. Es wird sp\u00E4ter erneut versucht.", messages[1], "check failure");
                Same("Beim Herunterladen des Updates ist ein Fehler aufgetreten (kaputt).", messages[2], "invalid data");
                Same("Der Netzwerk-Proxy blockiert Stackshot beim Herunterladen des Updates.", messages[4], "proxy");
                Same("Beim Herunterladen des Updates ist ein Fehler aufgetreten (Fehler 404).", messages[5], "http error");
                foreach (string m in messages) Check(char.IsUpper(m[0]), "a German sentence starts in lower case: " + m);
            });
            InLanguage("es", delegate { Same("No se pudo descargar la actualizaci\u00F3n. Se volver\u00E1 a intentar m\u00E1s tarde.", run(new Exception("x"), true), "Spanish unchanged"); });
        }

        // "100% (" with a space, as the other languages write it.
        public static void KoreanZoomTextIsSpaced()
        {
            InLanguage("ko", delegate
            {
                Check(I18n.T("", "100 % (actual size: 1 video pixel = 1 screen pixel)").StartsWith("100% (", StringComparison.Ordinal), "100% (");
                Check(I18n.T("", " % (scaled to fill window)").StartsWith("% (", StringComparison.Ordinal), "% (");
            });
        }

        // The player's audio track button follows the language; "Audio" is the word in Spanish, English and German.
        public static void PlayerAudioButtonIsLocalized()
        {
            foreach (string lang in new[] { "es", "en", "de" }) InLanguage(lang, delegate { Same("Audio", PlayerKit.AudioLabel, lang + " audio button"); });
            InLanguage("ko", delegate { Same("\uC624\uB514\uC624", PlayerKit.AudioLabel, "Korean audio button"); });
            Check(!Regex.IsMatch(Src("Editor\\PlayerWindow.cs"), "Make\\(\"tracks\", null, \"Audio\""), "the player still hard-codes the Audio label");
        }

        // "(64 bits)" in the report's machine line follows the language.
        public static void ReportMachineLineBits()
        {
            if (!Environment.Is64BitOperatingSystem) return;
            Dictionary<string, string> want = new Dictionary<string, string> { { "es", " (64 bits)" }, { "en", " (64-bit)" }, { "de", " (64-Bit)" }, { "ko", " (64\uBE44\uD2B8)" } };
            foreach (string lang in AllLanguages)
            {
                I18n.Apply(lang);
                string v = ReportWindow.WindowsVersion();
                string w = want.ContainsKey(lang) ? want[lang] : want["en"];
                Check(v.EndsWith(w, StringComparison.Ordinal), lang + " machine line: " + Show(v));
            }
            I18n.Apply("es");
        }

        // Split is a verb on a button; "Teilen" also means share.
        public static void GermanSplitIsNotShare()
        {
            InLanguage("de", delegate { Same("Aufteilen", I18n.T("Dividir", "Split"), "German Split"); });
        }

        // The pair is settled in the catalog text, from the value that is going to fill the variable, before the text is
        // formatted: what the user wrote (a file name, a mascot name) goes in untouched.
        public static void KoreanParticlesNeverEditValues()
        {
            InLanguage("ko", delegate
            {
                string open = "Could not open {0}.\n\n{1}";
                string tail = " \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\n";
                Same("\uAC51\uC740(\uB294).md\uC744(\uB97C)" + tail + "x", I18n.Fmt("", open, "\uAC51\uC740(\uB294).md", "x"), "a file name that holds a pair stays intact");
                Same("\uAC51\uC740(\uB294)\uC744" + tail + "x", I18n.Fmt("", open, "\uAC51\uC740(\uB294)", "x"), "a value ending in a pair stays intact (\uB294 has a final consonant)");
                Same("\uD83D\uDE00\uC744(\uB97C)" + tail + "x", I18n.Fmt("", open, "\uD83D\uDE00", "x"), "an emoji keeps the pair");
                Same("\uD558\uB298\uD83D\uDE00\uC744(\uB97C)" + tail + "x", I18n.Fmt("", open, "\uD558\uB298\uD83D\uDE00", "x"), "the word before an emoji is not read");
                Same("50%\uC744(\uB97C)" + tail + "x", I18n.Fmt("", open, "50%", "x"), "a percentage keeps the pair");
                Same("\uC744(\uB97C)" + tail + "x", I18n.Fmt("", open, "", "x"), "an empty value keeps the pair");
                Same("\uD558\uB298\uC744" + tail + "\uC740(\uB294) \uC774(\uAC00) \uC744(\uB97C) \uACFC(\uC640)", I18n.Fmt("", open, "\uD558\uB298", "\uC740(\uB294) \uC774(\uAC00) \uC744(\uB97C) \uACFC(\uC640)"), "the other values are never touched");
                Same("Stackshot 2.2.2\uAC00 \uCD5C\uC2E0 \uBC84\uC804\uC785\uB2C8\uB2E4. \uD655\uC778 \uC2DC\uAC01: \uC774(\uAC00)", I18n.Fmt("", "Stackshot {0} is the latest version. Checked {1}.", "2.2.2", "\uC774(\uAC00)"), "a later value that is a pair");
                Same("2.2.3\uC774", I18n.Fmt("", "Stackshot {0} is the latest version. Checked {1}.", "2.2.3", "x").Substring("Stackshot ".Length, "2.2.3\uC774".Length), "2.2.3");
                Check(I18n.Fmt("", "FFmpeg exited with code {0}.", 1).Contains("\uCF54\uB4DC 1\uC744 "), "1 read as a number");

                // The template: a closing quote or bracket between the variable and the pair does not hide the variable.
                string attach = "Open attachment \u201C{0}\u201D?";
                Same("\uCCA8\uBD80 \uD30C\uC77C \u2018\uD558\uB298\u2019\uC744 \uC5F4\uAE4C\uC694?", I18n.Fmt("", attach, "\uD558\uB298"), "quotes around the name, consonant");
                Same("\uCCA8\uBD80 \uD30C\uC77C \u2018\uB098\uBE44\u2019\uB97C \uC5F4\uAE4C\uC694?", I18n.Fmt("", attach, "\uB098\uBE44"), "quotes around the name, vowel");
                Same("\uCCA8\uBD80 \uD30C\uC77C \u2018\uD83D\uDE00\u2019\uC744(\uB97C) \uC5F4\uAE4C\uC694?", I18n.Fmt("", attach, "\uD83D\uDE00"), "quotes around an emoji");
                Same("({0})\uC744 \uC5F4\uAE30 {0:N0}\uAC1C", I18n.KoreanTemplate("({0})\uC744(\uB97C) \uC5F4\uAE30 {0:N0}\uAC1C", new object[] { "\uD558\uB298" }), "a bracket between");
                Same("{0:N0}\uC744 \uC5F4\uAE30", I18n.KoreanTemplate("{0:N0}\uC744(\uB97C) \uC5F4\uAE30", new object[] { 1000 }), "a format is read as the text it makes (1,000: a zero)");
                Same("{0}\uC744(\uB97C) \uC5F4\uAE30", I18n.KoreanTemplate("{0}\uC744(\uB97C) \uC5F4\uAE30", new object[] { null }), "a missing value keeps the pair");
            });
            // Spanish, English and German do not go through any of this.
            foreach (string lang in new[] { "es", "en", "de" })
                InLanguage(lang, delegate
                {
                    string s = I18n.Fmt("No se pudo abrir {0}.\n\n{1}", "Could not open {0}.\n\n{1}", "\uAC51\uC740(\uB294).md", "\uC744(\uB97C)");
                    Check(s.Contains("\uAC51\uC740(\uB294).md") && s.EndsWith("\uC744(\uB97C)", StringComparison.Ordinal), lang + ": a value changed: " + Show(s));
                });
        }

        // The notice that says how many other files were left out of the viewer is a catalog text: formatted by Fmt, so the
        // Korean pair after the file name is settled and never shown as it is written.
        public static void OpeningSeveralFilesNoticeUsesFmt()
        {
            Check(!Regex.IsMatch(Src("ShotStack.cs"), "string\\.Format\\(I18n\\.T\\(\"Se abre"), "ShotStack still formats the notice by hand");
            InLanguage("ko", delegate
            {
                string text = I18n.Fmt("Se abre {0}. {1} {2} (el visor muestra uno cada vez).", "Opening {0}. {1} {2} (the viewer shows one at a time).", "\uD558\uB298", 2, I18n.T("archivos m\u00E1s", "more files"));
                Check(text.StartsWith("\uD558\uB298\uC744 \uC5FD\uB2C8\uB2E4.", StringComparison.Ordinal) && text.IndexOf("(\uB97C)", StringComparison.Ordinal) < 0, "Korean notice: " + Show(text));
            });
        }

        // A name from the settings file loses control and bidi characters like a typed one, and the 16 units are cut by
        // letters: an emoji, a flag or a conjunct is never left in half.
        public static void MascotNameIsCleanWhereverItComesFrom()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "name-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Func<string, string> load = delegate(string raw)
                {
                    string ini = Path.Combine(dir, "n.ini");
                    File.WriteAllText(ini, "FirstRunDone = 1\nMascotName = " + raw + "\n", new UTF8Encoding(false));
                    return Settings.Load(ini).MascotName;
                };
                Same("evilx", load("\u202Eevil\u0007\u2066x\u2069"), "settings.ini: bidi and control characters");
                Same("Pixel", load("\u202E\u2069"), "settings.ini: nothing left keeps the default");
                Same("1234567890123456", load("12345678901234567890"), "settings.ini: 16 units");
                MethodInfo typed = typeof(HomeWindow).GetMethod("CleanName", BindingFlags.NonPublic | BindingFlags.Static);
                Func<string, string> type = s => (string)typed.Invoke(null, new object[] { s });
                Same("evilx", type("\u202Aev\u202Eil\u0007\u2066x\u2069"), "typed: bidi characters");
                Same("Pixel", type("\u202E"), "typed: nothing left is Pixel");

                string family = "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67\u200D\uD83D\uDC66";
                string flags = "\uD83C\uDDEA\uD83C\uDDF8\uD83C\uDDE9\uD83C\uDDEA\uD83C\uDDF0\uD83C\uDDF7\uD83C\uDDEB\uD83C\uDDF7\uD83C\uDDEE\uD83C\uDDF9";
                string thumbs = "\uD83D\uDC4D\uD83C\uDFFD";
                string deva = "\u0928\u092E\u0938\u094D\u0924\u0947 \u0915\u094D\u0937\u0924\u094D\u0930\u093F\u092F \u0926\u0941\u0928\u093F\u092F\u093E";
                string beng = "\u09AC\u09BE\u0982\u09B2\u09BE\u09A6\u09C7\u09B6 \u0995\u09CD\u09B7\u09C1\u09A6\u09CD\u09B0 \u0995\u09A8\u09CD\u09AF\u09BE";
                string hangul = "\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558";
                Same(family, MascotName.Clean(family + family), "two family emoji keep one whole");
                Same(flags.Substring(0, 16), MascotName.Clean(flags), "flags keep four whole");
                Same(thumbs + thumbs + thumbs + thumbs, MascotName.Clean(thumbs + thumbs + thumbs + thumbs + thumbs + thumbs), "skin tones stay with their hand");
                Same(hangul.Substring(0, 16), MascotName.Clean(hangul), "Hangul syllables are one unit each");
                // Whatever the cut, it never ends or continues inside a letter.
                foreach (string name in new[] { deva, beng, family + "ab" + family, "a" + family + "bc" + family, "ab" + flags + flags, "x" + thumbs + deva + beng })
                    for (int skip = 0; skip < 6; skip++)
                    {
                        string input = new string('a', skip) + name;
                        string cut = MascotName.Clean(input);
                        string tail = input.Substring(cut.Length).TrimStart();
                        Check(cut.Length <= 16, "over 16 units: " + Show(cut));
                        Check(input.StartsWith(cut, StringComparison.Ordinal), "not a start of the text: " + Show(input));
                        char last = cut.Length > 0 ? cut[cut.Length - 1] : 'a';
                        Check(last != '\u200D' && last != '\u094D' && last != '\u09CD' && !char.IsHighSurrogate(last), "ends inside a letter: " + Show(cut));
                        if (cut.Length < input.TrimEnd().Length)
                        {
                            char next = input[cut.Length];
                            UnicodeCategory cat = char.GetUnicodeCategory(next);
                            Check(next != '\u200D' && !char.IsLowSurrogate(next) && cat != UnicodeCategory.NonSpacingMark && cat != UnicodeCategory.SpacingCombiningMark && cat != UnicodeCategory.EnclosingMark,
                                  "the cut leaves a piece of a letter: " + Show(cut) + " | " + Show(tail));
                        }
                    }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        static bool TypeInto(TextBox box, string text)
        {
            TextCompositionEventArgs pre = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, text));
            pre.RoutedEvent = UIElement.PreviewTextInputEvent;
            box.RaiseEvent(pre);
            if (pre.Handled) return false;
            TextCompositionEventArgs input = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, text));
            input.RoutedEvent = UIElement.TextInputEvent;
            box.RaiseEvent(input);
            return true;
        }

        // What the clipboard command does: the pasting handlers may change or cancel it, and what is left goes in.
        static void PasteInto(TextBox box, string text, bool dragDrop)
        {
            DataObject data = new DataObject(DataFormats.UnicodeText, text);
            DataObjectPastingEventArgs e = new DataObjectPastingEventArgs(data, dragDrop, DataFormats.UnicodeText);
            e.RoutedEvent = DataObject.PastingEvent;
            box.RaiseEvent(e);
            if (!e.CommandCancelled) box.SelectedText = (string)e.DataObject.GetData(DataFormats.UnicodeText);
        }

        // The name's field never rewrites what is in it: a full name refuses one more letter instead of dropping another
        // one, wherever the caret is; a paste is cleaned and cut by letters, and what was there stays.
        public static void MascotNameFieldNeverAltersTyping()
        {
            string full = "1234567890123456";
            string family = "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67";
            TextBox box = new TextBox();
            MascotName.Guard(box);

            box.Text = full;
            box.CaretIndex = 3;
            Check(!TypeInto(box, "X"), "a full name accepted a letter in the middle");
            Same(full, box.Text, "typing into a full name leaves it as it is");
            Same("3", box.CaretIndex.ToString(), "and the caret stays");
            box.CaretIndex = 16;
            Check(!TypeInto(box, "X"), "a full name accepted a letter at the end");
            Same(full, box.Text, "typing at the end of a full name");
            Check(!TypeInto(box, "\uD83D\uDE00"), "a full name accepted an emoji");
            Same(full, box.Text, "an emoji into a full name");
            Check(!TypeInto(box, "\u202E"), "a bidi override was accepted");
            Same(full, box.Text, "a bidi override into a full name");

            box.Text = "Mr Pixel ";
            box.CaretIndex = 9;
            Check(TypeInto(box, "X"), "a name with room refused a letter");
            Same("Mr Pixel X", box.Text, "typing with room keeps the spaces");
            box.Text = "ab";
            box.CaretIndex = 1;
            Check(TypeInto(box, "X"), "typing in the middle was refused");
            Same("aXb", box.Text, "typing in the middle");
            box.Text = full;
            box.SelectAll();
            Check(TypeInto(box, "X"), "typing over the selection was refused");
            Same("X", box.Text, "typing over a selection frees the room");
            box.Text = "ab";
            box.CaretIndex = 2;
            Check(!TypeInto(box, "\u202E"), "a bidi override typed into a name was kept");
            Check(!TypeInto(box, "\u200B"), "a zero-width space typed into a name was kept");
            Same("ab", box.Text, "forbidden characters are not typed");

            // Pasting: cleaned, then cut by letters to what room is left.
            box.Text = "abc";
            box.CaretIndex = 3;
            PasteInto(box, "\u202E" + "de\u0007\r\nf\u2066", false);
            Same("abcdef", box.Text, "a paste loses bidi and control characters");
            box.Text = "abc";
            box.CaretIndex = 3;
            PasteInto(box, family + family, false);
            Same("abc" + family, box.Text, "a paste is cut by letters, not in the middle of a family");
            box.Text = "abcd";
            box.CaretIndex = 1;
            PasteInto(box, "1234567890123456789", false);
            Same("a" + "123456789012" + "bcd", box.Text, "a paste in the middle keeps both sides and is cut");
            Check(box.Text.Length == 16, "16 units after the paste");
            box.Text = full;
            box.CaretIndex = 4;
            PasteInto(box, "zz", false);
            Same(full, box.Text, "a paste into a full name changes nothing");
            box.Text = "abc";
            PasteInto(box, "dropped", true);
            Same("abc", box.Text, "a drop is refused");
        }

        // The cut at 16 units: decomposed Hangul is put together first, a flag made of tag characters stays whole, and the
        // invisible spaces and direction marks that would make two names look alike are removed.
        public static void MascotNameCutEdgeCases()
        {
            // NFD Hangul (three jamo per syllable) counts as the syllables it reads as.
            string syllables = "\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44\uD558\uB298\uB098\uBE44";
            string jamo = syllables.Normalize(NormalizationForm.FormD);
            Check(jamo.Length > 40, "the test text is decomposed");
            Same(syllables.Substring(0, 16), MascotName.Clean(jamo), "decomposed Hangul is cut by syllables, composed");
            Same("\uD55C\uAE00", MascotName.Clean("\u1112\u1161\u11AB\u1100\u1173\u11AF"), "two decomposed syllables read as two");
            // A flag drawn with tag characters (England) is one letter of 14 units: whole or not at all.
            string england = "\uD83C\uDFF4\uDB40\uDC67\uDB40\uDC62\uDB40\uDC65\uDB40\uDC6E\uDB40\uDC67\uDB40\uDC7F";
            Same(england.Length.ToString(), "14", "the flag is 14 units");
            Same("ab" + england, MascotName.Clean("ab" + england), "a flag that fits stays whole");
            Same("abc", MascotName.Clean("abc" + england), "a flag that does not fit is left out, not cut");
            Same("abc", MascotName.Clean("abc" + england + england), "two flags");
            Same(england, MascotName.Clean(england + england), "one flag of two");
            Same("a" + england + "b", MascotName.Clean("a" + england + "bcd"), "the letters after a whole flag still count");
            // Invisible characters and direction marks are not part of a name.
            Same("abcdef", MascotName.Clean("a\u200Bb\uFEFFc\u200Ed\u200Fe\u061Cf"), "zero-width space, BOM, LRM, RLM and ALM");
            Same("Pixel", HomeWindowCleanName("\u200B\uFEFF\u200E\u200F\u061C"), "nothing left is Pixel");
            Same("a\u200Db", MascotName.Clean("a\u200Db"), "the joiner of emoji and scripts stays");
            Same("\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645", MascotName.Clean("\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645"), "the Persian half-space stays");
        }

        static string HomeWindowCleanName(string s)
        {
            MethodInfo typed = typeof(HomeWindow).GetMethod("CleanName", BindingFlags.NonPublic | BindingFlags.Static);
            return (string)typed.Invoke(null, new object[] { s });
        }

        // The rows test leaves out texts that are the user's own: a folder in any form Stackshot shows it.
        public static void SettingsRowsExemptTheirPaths()
        {
            foreach (string theirs in new[] { "C:\\Users\\me\\Pictures", "~\\Pictures\\Stackshot", "~\\Videos", "Windows default: Speakers (Model)" })
                Check(AppTestsI18nAudit.IsTheirs(theirs), "not exempt: " + theirs);
            foreach (string ours in new[] { "Retention of temporary captures", "Auto-Ausgleich", "Temporary files ~ a day" })
                Check(!AppTestsI18nAudit.IsTheirs(ours), "exempt though it is ours: " + ours);
        }

        // Each language reads its own block of the release's news, falling back to the English one and then to the
        // changelog lines, and never to the Spanish block.
        public static void ReleaseNoticesReadTheirLanguage()
        {
            Func<string, string, string> block = (marker, text) => "<details><summary>x</summary>\n\n<!-- " + marker + " -->\n- **" + text + ".** Body.\n<!-- /" + marker + " -->\n\n</details>\n";
            string lines = "- **Changelog line.** Plain.\n\n";
            string all = lines + block("novedades", "Spanisch") + block("novedades-en", "English") + block("novedades-de", "Deutsch") + block("novedades-ko", "Korean");
            string noGerman = lines + block("novedades", "Spanisch") + block("novedades-en", "English");
            string onlyDe = lines + block("novedades", "Spanisch") + block("novedades-de", "Deutsch");
            string onlyEs = lines + block("novedades", "Spanisch");
            Dictionary<string, string[]> want = new Dictionary<string, string[]>();
            //                      es         en         de         ko         fr         ar
            want[all] = new[] { "Spanisch", "English", "Deutsch", "Korean", "English", "English" };
            want[noGerman] = new[] { "Spanisch", "English", "English", "English", "English", "English" };
            want[onlyDe] = new[] { "Spanisch", "Changelog line", "Deutsch", "Changelog line", "Changelog line", "Changelog line" };
            want[onlyEs] = new[] { "Spanisch", "Changelog line", "Changelog line", "Changelog line", "Changelog line", "Changelog line" };
            string[] langs = { "es", "en", "de", "ko", "fr", "ar" };
            foreach (KeyValuePair<string, string[]> c in want)
            {
                Updater.Release r = AppTestsI18nAudit.ParseRelease(c.Key);
                for (int i = 0; i < langs.Length; i++)
                {
                    I18n.Apply(langs[i]);
                    List<News.Item> items = r.Items();
                    Check(items.Count == 1 && items[0].Title.Contains(c.Value[i]), langs[i] + ": expected " + c.Value[i] + ", got " + (items.Count == 0 ? "nothing" : items[0].Title));
                }
            }
            I18n.Apply("es");
            Updater.Release full = AppTestsI18nAudit.ParseRelease(all);
            Check(full.News.Count == 1 && full.NewsEn.Count == 1 && full.NewsDe.Count == 1 && full.NewsKo.Count == 1, "the four blocks are read apart");
            // The block markers do not get mixed up: "novedades" is not a prefix of the others in the way the app looks for it.
            Check(News.FromRelease(lines + block("novedades-de", "Deutsch")).Count == 0, "a German block read as the Spanish one");
        }

        // ---- Pictures: with STACKSHOT_UI_SHOTS set to a folder, the screens this audit touched are saved there as PNG.

        static void SavePng(BitmapSource b, string name)
        {
            PngBitmapEncoder e = new PngBitmapEncoder();
            e.Frames.Add(BitmapFrame.Create(b));
            using (FileStream fs = File.Create(Path.Combine(Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS"), name))) e.Save(fs);
        }

        static void SaveElement(FrameworkElement root, double width, double height, string name)
        {
            root.Measure(new Size(width, height > 0 ? height : double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, width, height > 0 ? height : root.DesiredSize.Height));
            root.UpdateLayout();
            int w = (int)Math.Ceiling(root.ActualWidth), h = (int)Math.Ceiling(root.ActualHeight);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Ds.Brush(Ds.Brushes.Window), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            SavePng(rtb, name);
        }

        public static void Pictures()
        {
            string dir = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                // The mascot's menu button on the Home page, in Korean, for names that end in a consonant, a vowel and Latin script.
                foreach (string[] n in new[] { new[] { "\uD558\uB298", "hanul" }, new[] { "\uB098\uBE44", "nabi" }, new[] { "Pixel", "pixel" } })
                {
                    I18n.Apply("ko");
                    Ds.Apply(1);
                    Settings st = new Settings();
                    st.SaveFolder = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t18-shots");
                    st.ShowRecent = false; st.ShowIntro = false; st.Profile = 2; st.Appearance = 1; st.MascotOn = true; st.MascotName = n[0];
                    HomeWindow hw = new HomeWindow((ShotStack)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ShotStack)), st);
                    try { AppTestsI18n.SetPage(hw, "home"); AppTestsI18n.Layout(hw); SaveElement((FrameworkElement)hw.Content, 980, 660, "t18-ko-play-" + n[1] + ".png"); }
                    finally { hw.Shutdown(); }
                }

                // The German wellness notice: the mascot's name, a colon and the break with its capital.
                I18n.Apply("de");
                Ds.Apply(1);
                Settings ws = new Settings();
                ws.MascotName = "Pixel";
                Notice notice = new Notice(ShotStack.WellTitle("Pixel", WellKind.Water), MascotTalk.Remind(ws, WellKind.Water, 20), NoticeKind.Info);
                ToastLook look = ToastLook.For(notice, 1f);
                int pad = 30, tw = look.Size.Width + 2 * pad, th = look.Size.Height + 2 * pad;
                BitmapSource shadow = ToastLook.Shadow(tw, th, pad, look);
                SavePng(Ink.Render(tw, th, delegate(DrawingContext dc)
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(214, 224, 238)), null, new Rect(0, 0, tw, th));
                    dc.DrawImage(shadow, new Rect(0, 0, tw, th));
                    dc.PushTransform(new TranslateTransform(pad, pad));
                    look.Paint(dc, ToastLook.None);
                    dc.Pop();
                }, null), "t18-de-wellness-notice.png");

                // The report form with the title hint, and the list of reports with a report that is still open.
                foreach (string lang in new[] { "de", "ko" })
                {
                    I18n.Apply(lang);
                    Ds.Apply(1);
                    ReportWindow w = (ReportWindow)typeof(ReportWindow).GetConstructor(np, null, Type.EmptyTypes, null).Invoke(null);
                    try
                    {
                        typeof(ReportWindow).GetField("title", np).SetValue(w, "Abc");
                        typeof(ReportWindow).GetMethod("Build", np).Invoke(w, null);
                        SaveElement((FrameworkElement)w.Content, 540, 0, "t18-" + lang + "-report-form.png");

                        ReportHistory.Add(41, false, "Aufnahme bricht ab");
                        ReportHistory.Add(42, true, "Dark mode for the editor");
                        ReportHistory.Add(43, false, "Wrong crop on a second screen");
                        Dictionary<int, ReportHistory.State> states = new Dictionary<int, ReportHistory.State>();
                        states[41] = new ReportHistory.State { Open = true };
                        states[42] = new ReportHistory.State { Open = false, Reason = "completed" };
                        states[43] = new ReportHistory.State { Open = false, Reason = "duplicate" };
                        typeof(ReportWindow).GetField("states", np).SetValue(w, states);
                        typeof(ReportWindow).GetField("mine", np).SetValue(w, true);
                        typeof(ReportWindow).GetMethod("Build", np).Invoke(w, null);
                        SaveElement((FrameworkElement)w.Content, 540, 0, "t18-" + lang + "-my-reports.png");
                    }
                    finally { try { w.Close(); } catch (Exception) { } }
                }
            }
            finally { I18n.Apply("es"); }
        }
    }
}
