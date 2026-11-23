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
            T("tray-labels-follow-the-language", TrayLabelsFollowTheLanguage);
            T("korean-gif-action-reads-gif-recording", KoreanGifActionReadsGifRecording);
            T("scene-home-actions-fit-the-stage", SceneHomeActionsFitTheStage);
            T("korean-writes-markdown-in-hangul", KoreanWritesMarkdownInHangul);
            T("no-bare-markdown-label-in-src", NoBareMarkdownLabelInSrc);
            T("tray-panel-has-no-foreign-leftovers", TrayPanelHasNoForeignLeftovers);
            T("readmes-call-korean-complete", ReadmesCallKoreanComplete);
            T("tray-recent-dates-follow-the-language", TrayRecentDatesFollowTheLanguage);
            T("picker-lists-only-the-complete-languages", PickerListsOnlyTheCompleteLanguages);
            T("hidden-languages-load-as-automatic", HiddenLanguagesLoadAsAutomatic);
            T("previews-come-back-with-the-switch", PreviewsComeBackWithTheSwitch);
            T("korean-uninstall-note-reads-clean", KoreanUninstallNoteReadsClean);
            T("level-up-lines-read-right", LevelUpLinesReadRight);
            T("korean-breaks-note-keeps-one-register", KoreanBreaksNoteKeepsOneRegister);
            T("german-durations-go-through-the-catalog", GermanDurationsGoThroughTheCatalog);
            T("segment-seconds-and-auto-follow-the-language", SegmentSecondsAndAutoFollowTheLanguage);
            T("german-capture-names-use-the-noun", GermanCaptureNamesUseTheNoun);
            T("search-words-are-folded", SearchWordsAreFolded);
            T("german-ago-has-no-double-period", GermanAgoHasNoDoublePeriod);
            T("korean-version-sentences-have-no-open-particle", KoreanVersionSentencesHaveNoOpenParticle);
            T("korean-euro-follows-the-value", KoreanEuroFollowsTheValue);
            T("korean-welcome-edit-hint", KoreanWelcomeEditHint);
            T("english-docs-say-auto-like-the-interface", EnglishDocsSayAutoLikeTheInterface);
            T("news-spanish-arrows-match", NewsSpanishArrowsMatch);
            T("picker-name-only-for-listed-languages", PickerNameOnlyForListedLanguages);
            T("korean-reminder-durations", KoreanReminderDurations);
            T("german-mascot-wording", GermanMascotWording);
            T("search-key-is-strg-in-german", SearchKeyIsStrgInGerman);
            T("korean-particles-after-quoted-values", KoreanParticlesAfterQuotedValues);
            T("scrolling-plain-notice-fits", ScrollingPlainNoticeFits);
            T("folder-errors-quote-like-the-language", FolderErrorsQuoteLikeTheLanguage);
            T("missing-folder-without-a-path-reads-once", MissingFolderWithoutAPathReadsOnce);
            T("german-none-choice-is-capitalized", GermanNoneChoiceIsCapitalized);
            T("settings-search-gaps-are-closed", SettingsSearchGapsAreClosed);
            T("markdown-zone-help-line-fits", MarkdownZoneHelpLineFits);
            T("scroll-bar-button-labels", ScrollBarButtonLabels);
            T("backdrop-slider-help-follows-the-language", BackdropSliderHelpFollowsTheLanguage);
            T("korean-names-the-media-keys", KoreanNamesTheMediaKeys);
            T("last-audit-screens-as-pictures", Pictures);
        }

        static string[] AllLanguages { get { return AppTestsI18nLayout.AllLanguages; } }

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static void Same(string expected, string actual, string what)
        {
            if (expected != actual) throw new InvalidOperationException(what + ": expected <" + Show(expected) + ">, got <" + Show(actual) + ">");
        }

        static string Show(string s) { return s == null ? "null" : Regex.Replace(s, "[^\\x20-\\x7e]", m => "\\u" + ((int)m.Value[0]).ToString("X4")); }

        // Korean is one of the four complete languages: no README may say some of its texts are still missing.
        static void ReadmesCallKoreanComplete()
        {
            string root = AppTestsI18nCoverage.RepoRoot();
            foreach (string f in new[] { "README.md", "README.es.md" })
            {
                string t = File.ReadAllText(Path.Combine(root, f), Encoding.UTF8);
                Check(t.IndexOf("Korean text still missing", StringComparison.Ordinal) < 0 &&
                      t.IndexOf("falten en coreano", StringComparison.Ordinal) < 0, f + " says Korean is incomplete");
            }
        }

        // A missing folder with no known path names the folder once, never "the folder the folder", in every language.
        static void MissingFolderWithoutAPathReadsOnce()
        {
            try
            {
                string[][] cases = { new[] { "es", "carpeta la carpeta" }, new[] { "en", "folder the folder" },
                                     new[] { "de", "Ordner den Ordner" }, new[] { "ko", "\uD3F4\uB354 \uD3F4\uB354" } };
                foreach (string[] c in cases)
                {
                    I18n.Apply(c[0]);
                    string t = ShotStack.Explain(new DirectoryNotFoundException(), null);
                    Check(t.IndexOf(c[1], StringComparison.Ordinal) < 0 && t.IndexOf("{0}", StringComparison.Ordinal) < 0, c[0] + ": " + t);
                    Check(t.Length > 10, c[0] + " empty: " + t);
                    if (c[0] == "ko") Check(!Regex.IsMatch(t, "[A-Za-z]"), "ko Latin: " + t);
                }
            }
            finally { I18n.Apply("es"); }
        }

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
            foreach (string lang in new[] { "es", "en", "fr" })
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

        // ---- Markdown in Korean, and the tray panel in every language

        const string KoMarkdown = "\uB9C8\uD06C\uB2E4\uC6B4";
        const string KoGifRecording = "GIF \uB179\uD654";

        // The tray panel's rows (id, text) as BuildItems makes them, plus its status line under the id "status".
        static List<KeyValuePair<string, string>> TrayRows(bool recording, bool scrolling)
        {
            BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance;
            TrayPanel tp = new TrayPanel(new Settings());
            try
            {
                typeof(TrayPanel).GetMethod("BuildItems", np).Invoke(tp, new object[] { recording, scrolling, false });
                List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
                foreach (object it in (System.Collections.IEnumerable)typeof(TrayPanel).GetField("items", np).GetValue(tp))
                    rows.Add(new KeyValuePair<string, string>((string)it.GetType().GetField("Id").GetValue(it), (string)it.GetType().GetField("Text").GetValue(it)));
                rows.Add(new KeyValuePair<string, string>("status", (string)typeof(TrayPanel).GetField("status", np).GetValue(tp)));
                return rows;
            }
            finally { try { tp.Dispose(); } catch (Exception) { } }
        }

        static string RowText(List<KeyValuePair<string, string>> rows, string id)
        {
            foreach (KeyValuePair<string, string> r in rows) if (r.Key == id) return r.Value;
            return null;
        }

        // The tray panel's GIF and Markdown rows: GIF is GIF except in Korean, where the action reads GIF recording; Markdown is written in hangul in Korean and stays
        // Markdown in the others (the preview languages show English by design).
        public static void TrayLabelsFollowTheLanguage()
        {
            foreach (string lang in AllLanguages)
                InLanguage(lang, delegate
                {
                    List<KeyValuePair<string, string>> rows = TrayRows(false, false);
                    Same(lang == "ko" ? KoGifRecording : "GIF", RowText(rows, "gif"), lang + " tray GIF");
                    Same(lang == "ko" ? KoMarkdown : "Markdown", RowText(rows, "markdown"), lang + " tray Markdown");
                });
        }

        static List<string> PickerBarLabels()
        {
            ConstructorInfo ci = typeof(RegionPicker).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 6);
            RegionPicker w = (RegionPicker)ci.Invoke(new object[] { new Dib(80, 60), new System.Drawing.Rectangle(0, 0, 80, 60), new List<Grabber.Win>(), RegionPicker.Mode.Image, true, false });
            try
            {
                BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(RegionPicker).GetMethod("BuildBar", np).Invoke(w, null);
                List<string> labels = new List<string>();
                foreach (object b in (System.Collections.IEnumerable)typeof(RegionPicker).GetField("bar", np).GetValue(w)) labels.Add((string)b.GetType().GetField("Label").GetValue(b));
                return labels;
            }
            finally { try { w.Dispose(); } catch (Exception) { } }
        }

        // Korean says GIF recording wherever GIF is the action the user starts (tray row and its reader name, Home quick
        // actions, the area picker's mode button) and GIF wherever it names the file format; the others keep their wording.
        public static void KoreanGifActionReadsGifRecording()
        {
            FieldInfo f = typeof(I18n).GetField("KoreanCatalog", BindingFlags.NonPublic | BindingFlags.Static);
            object lazy = f.GetValue(null);
            Dictionary<string, string> catalog = (Dictionary<string, string>)lazy.GetType().GetProperty("Value").GetValue(lazy, null);
            foreach (string lang in AllLanguages)
                InLanguage(lang, delegate
                {
                    bool ko = lang == "ko";
                    string action = ko ? KoGifRecording : "GIF";
                    Same(action, RowText(TrayRows(false, false), "gif"), lang + " tray row");
                    Same(action, HomeWindow.ActionShortest[7], lang + " shortest action");
                    Check(PickerBarLabels().Contains(action) && (ko || PickerBarLabels().Contains("GIF")), lang + " picker mode button: " + string.Join("|", PickerBarLabels()));
                    List<string> heard = AppTestsI18n.AuditTray();
                    Check(heard.Contains(action), lang + " the tray tells a reader \"" + Show(action) + "\"");
                    if (ko) Check(!heard.Contains("GIF"), "Korean reader still hears a bare GIF in the tray");
                });
            string[] shortWord = { "es", "GIF animado", "en", "Animated GIF", "de", "Animiertes GIF", "ko", KoGifRecording };
            for (int i = 0; i < shortWord.Length; i += 2)
            {
                string lang = shortWord[i], want = shortWord[i + 1];
                InLanguage(lang, delegate { Same(want, HomeWindow.ActionShort[7], lang + " short action"); });
            }
            Same("GIF", catalog["GIF"], "the format name");
            Same("GIF \uB179\uD654", catalog["Record GIF"], "the record action");
            Check(catalog["Direct adds to stack; Adjust first lets you reposition/resize before Enter; Show options lets you pick capture, video, GIF or scroll."].Contains(KoGifRecording), "the picker's mode list names the action");
            foreach (string format in new[] { "Export GIF of clip", "Create a GIF from marked clip (E & S) \u00B7 {0}  (G)", "GIF image", "No GIFs", "GIF frames per second", "Saving GIF\u2026", "complete GIF" })
                Check(catalog[format].Contains("GIF") && !catalog[format].Contains("\uB179\uD654"), "the format \"" + format + "\" stays GIF: " + Show(catalog[format]));
        }

        static IEnumerable<DependencyObject> VisualsBelow(DependencyObject o)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(o, i);
                yield return c;
                foreach (DependencyObject d in VisualsBelow(c)) yield return d;
            }
        }

        // The action rows of the scene home as the window lays them out at its default size: for each, its top and bottom
        // inside the stage, plus the bottom of the heading block above the first row, the top of that block and the stage's height.
        static void SceneGeometry(string name, out List<double[]> rows, out double headingBottom, out double headingTop, out double stage)
        {
            Settings ss = new Settings();
            ss.SaveFolder = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t29-scene");
            ss.ShowRecent = false; ss.ShowIntro = false; ss.Profile = 2; ss.Appearance = 1; ss.HomeStyle = 2; ss.MascotOn = true; ss.MascotName = name;
            Ds.Apply(1);
            HomeWindow hw = new HomeWindow((ShotStack)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ShotStack)), ss);
            try
            {
                AppTestsI18n.SetPage(hw, "home");
                AppTestsI18n.Layout(hw);
                stage = Convert.ToDouble(typeof(HomeWindow).GetField("SceneStage", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue());
                FrameworkElement box = null;
                foreach (DependencyObject d in VisualsBelow((DependencyObject)hw.Content))
                    if (d is Grid && ((Grid)d).Height == stage) { box = (FrameworkElement)d; break; }
                Check(box != null, "the scene's stage is in the window");
                rows = new List<double[]>();
                headingBottom = 0; headingTop = double.MaxValue;
                foreach (DependencyObject d in VisualsBelow(box))
                {
                    TapBorder row = d as TapBorder;
                    if (row == null || row.Height != 34) continue;
                    Rect r = row.TransformToAncestor(box).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
                    rows.Add(new[] { r.Top, r.Bottom });
                    if (rows.Count > 1) continue;
                    Panel list = (Panel)VisualTreeHelper.GetParent(row);
                    foreach (UIElement head in list.Children)
                    {
                        if (head == row) break;
                        Rect h = ((FrameworkElement)head).TransformToAncestor(box).TransformBounds(new Rect(0, 0, ((FrameworkElement)head).ActualWidth, ((FrameworkElement)head).ActualHeight));
                        headingBottom = Math.Max(headingBottom, h.Bottom);
                        headingTop = Math.Min(headingTop, h.Top);
                    }
                }
            }
            finally { hw.Shutdown(); }
        }

        // The Home "Scene" style lists the modes in a stage of fixed height: every row, the last (Markdown) too, ends inside
        // it in each language, and the first row starts under the greeting and the question, not over them.
        public static void SceneHomeActionsFitTheStage()
        {
            List<string> found = new List<string>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
                foreach (string name in new[] { "Pixel", "WWWWWWWWWWWWWWWW" })
                    InLanguage(lang, delegate
                    {
                        List<double[]> rows;
                        double headingBottom, headingTop, stage;
                        SceneGeometry(name, out rows, out headingBottom, out headingTop, out stage);
                        string at = lang + " " + name;
                        if (rows.Count < 8) { found.Add(at + ": only " + rows.Count + " action rows"); return; }
                        double last = rows[rows.Count - 1][1];
                        if (last > stage) found.Add(at + ": the last row ends at " + last + ", past the stage (" + stage + ")");
                        if (rows[0][0] < headingBottom) found.Add(at + ": the first row starts at " + rows[0][0] + ", over the heading that ends at " + headingBottom);
                        if (headingTop < 30) found.Add(at + ": the heading starts at " + headingTop + ", under the window buttons");
                    });
            foreach (string s in found) Console.WriteLine("    scene: " + s);
            Check(found.Count == 0, found.Count + " scene layouts are wrong (listed above)");
        }

        // Korean spells Markdown in hangul wherever the interface says it: the catalog, the Home action names, the document
        // window's title and the prefix of its captures. GIF stays GIF.
        public static void KoreanWritesMarkdownInHangul()
        {
            FieldInfo f = typeof(I18n).GetField("KoreanCatalog", BindingFlags.NonPublic | BindingFlags.Static);
            object lazy = f.GetValue(null);
            Dictionary<string, string> catalog = (Dictionary<string, string>)lazy.GetType().GetProperty("Value").GetValue(lazy, null);
            List<string> latin = new List<string>(), unread = new List<string>(), particle = new List<string>();
            Regex vowelParticle = new Regex(KoMarkdown + "(\uB97C|\uAC00|\uB294|\uC640)");
            foreach (KeyValuePair<string, string> p in catalog)
            {
                if (p.Value.IndexOf("Markdown", StringComparison.Ordinal) >= 0) latin.Add(p.Key + " => " + Show(p.Value));
                if (p.Key.IndexOf("Markdown", StringComparison.Ordinal) >= 0 && p.Value.IndexOf(KoMarkdown, StringComparison.Ordinal) < 0) unread.Add(p.Key);
                if (vowelParticle.IsMatch(p.Value)) particle.Add(p.Key + " => " + Show(p.Value));
            }
            foreach (string s in latin) Console.WriteLine("    latin Markdown: " + s);
            foreach (string s in particle) Console.WriteLine("    wrong particle: " + s);
            Check(latin.Count == 0, latin.Count + " Korean catalog values still say Markdown in Latin letters");
            Check(unread.Count == 0, unread.Count + " Korean catalog entries about Markdown do not say it in hangul");
            Check(particle.Count == 0, "the name ends in a consonant (" + KoMarkdown + "), so its particles are \uC744 \uC774 \uC740 \uACFC: " + particle.Count + " texts differ");
            Same(KoMarkdown, catalog["Markdown"], "the bare word");
            Same(KoMarkdown + " \uBB38\uC11C", catalog["Markdown document"], "Markdown document");
            Same("\uCEA1\uCC98\uC640 \uB3D9\uC601\uC0C1\u00B7GIF \uB179\uD654, \uB3D9\uC601\uC0C1 \uD50C\uB808\uC774\uC5B4\uC640 " + KoMarkdown + "\u00B7XML \uBDF0\uC5B4",
                 catalog["Screenshots, video and GIF, with a video player and a Markdown and XML viewer"], "the app description");

            foreach (string lang in AllLanguages)
                InLanguage(lang, delegate
                {
                    bool ko = lang == "ko";
                    string md = ko ? KoMarkdown : "Markdown";
                    Check(HomeWindow.ActionShort[8].Contains(md) && (ko || HomeWindow.ActionShort[8] == "Markdown"), lang + " short action: " + HomeWindow.ActionShort[8]);
                    Same(md, HomeWindow.ActionShortest[8], lang + " shortest action");
                    Same(ko ? KoGifRecording : "GIF", HomeWindow.ActionShortest[7], lang + " shortest GIF");
                    Check(HomeWindow.ActionNames[8].Contains(md), lang + " action name: " + HomeWindow.ActionNames[8]);
                    Same(md, MarkdownWindow.KindName(false), lang + " document kind");
                    Same("XML", MarkdownWindow.KindName(true), lang + " XML kind");
                });
        }

        // No user-visible text says "Markdown" outside I18n: every string literal that has the word is a routed text (an
        // I18n call), a log line, an ini key or a registry name, or one of the Spanish/English action lists the Home window
        // reads through I18n.
        public static void NoBareMarkdownLabelInSrc()
        {
            string src = Path.Combine(AppTestsI18nCoverage.RepoRoot(), "src");
            Regex literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*Markdown(?:[^\"\\\\]|\\\\.)*\"");
            List<string> bare = new List<string>();
            foreach (string path in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith("I18n.", StringComparison.Ordinal)) continue;   // the catalogs
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || !literal.IsMatch(line)) continue;
                    bool routed = false;
                    // The call may start on an earlier line of the same statement.
                    for (int k = i; k >= Math.Max(0, i - 4) && !routed; k--)
                    {
                        routed = lines[k].Contains("I18n.T(") || lines[k].Contains("I18n.Fmt(") || lines[k].Contains("I18n.Own(") || lines[k].Contains("I18n.Localize(");
                        string before = k > 0 ? lines[k - 1].TrimEnd() : ";";
                        if (before.EndsWith(";", StringComparison.Ordinal) || before.EndsWith("{", StringComparison.Ordinal) || before.EndsWith("}", StringComparison.Ordinal)) break;
                    }
                    bool data = line.Contains("Log(") || line.Contains("new AssocKind(") || (name == "Settings.cs" && line.Contains("AppendLine(")) ||
                                line.Contains("ActionNamesEs") || line.Contains("ActionNamesEn") || line.Contains("ActionShortEs") || line.Contains("ActionShortEn") ||
                                line.Contains("ActionShortestEs") || line.Contains("ActionShortestEn");
                    if (!routed && !data) bare.Add(name + ":" + (i + 1) + ": " + line.Trim());
                }
            }
            foreach (string b in bare) Console.WriteLine("    bare Markdown: " + b);
            Check(bare.Count == 0, bare.Count + " bare \"Markdown\" literals reach the interface without I18n (listed above)");
        }

        // Everything the tray panel says (rows while idle, recording and scrolling, and its status line) in the four complete
        // languages: no Spanish or English left over except format names that read the same in that language.
        public static void TrayPanelHasNoForeignLeftovers()
        {
            Dictionary<string, List<KeyValuePair<string, string>>[]> all = new Dictionary<string, List<KeyValuePair<string, string>>[]>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
                InLanguage(lang, delegate { all[lang] = new[] { TrayRows(false, false), TrayRows(true, false), TrayRows(false, true) }; });
            Dictionary<string, string[]> same = new Dictionary<string, string[]>();
            same["es"] = new[] { "GIF", "Markdown" };
            same["en"] = new[] { "GIF", "Markdown" };
            same["de"] = new[] { "GIF", "Markdown", "Video", "Text" };
            same["ko"] = new[] { "GIF" };
            List<string> found = new List<string>();
            foreach (string lang in new[] { "es", "de", "ko" })
                for (int state = 0; state < 3; state++)
                {
                    List<KeyValuePair<string, string>> mine = all[lang][state], en = all["en"][state], es = all["es"][state];
                    Check(mine.Count == en.Count, lang + " tray has the same rows as English");
                    for (int i = 0; i < mine.Count; i++)
                    {
                        string t = mine[i].Value;
                        if (string.IsNullOrEmpty(t) || Array.IndexOf(same[lang], t) >= 0) continue;
                        if (lang != "en" && t == en[i].Value) found.Add(lang + " [" + mine[i].Key + "] reads as English: " + t);
                        if (lang != "es" && t == es[i].Value) found.Add(lang + " [" + mine[i].Key + "] reads as Spanish: " + t);
                    }
                }
            foreach (KeyValuePair<string, string> r in all["ko"][0].Concat(all["ko"][1]).Concat(all["ko"][2]))
                if (r.Value != null && Regex.IsMatch(Regex.Replace(r.Value, "GIF|Stackshot|XML", ""), "[A-Za-z]{2,}")) found.Add("ko [" + r.Key + "] has Latin words: " + r.Value);
            // What a screen reader gets from the panel (names, actions, descriptions) in the same languages.
            Dictionary<string, List<string>> heard = new Dictionary<string, List<string>>();
            foreach (string lang in new[] { "en", "de", "ko" }) InLanguage(lang, delegate { heard[lang] = AppTestsI18n.AuditTray(); });
            Check(heard["en"].Count >= 20 && heard["ko"].Count == heard["en"].Count && heard["de"].Count == heard["en"].Count, "the panel tells a reader the same things in every language");
            foreach (string t in heard["ko"])
                if (t != null && Regex.IsMatch(Regex.Replace(t, "GIF|Stackshot|XML|Ctrl|Alt|Shift|PrtScn", ""), "[A-Za-z]{2,}")) found.Add("ko (reader) has Latin words: " + t);
            for (int i = 0; i < heard["de"].Count; i++)
                if (heard["de"][i] != null && heard["de"][i] == heard["en"][i] && Array.IndexOf(same["de"], heard["de"][i]) < 0 && heard["de"][i] != "Stackshot") found.Add("de (reader) reads as English: " + heard["de"][i]);
            foreach (string lang in new[] { "en" })
                foreach (List<KeyValuePair<string, string>> rows in all[lang])
                    foreach (KeyValuePair<string, string> r in rows) if (r.Value != null && AppTestsI18n.LooksSpanish(r.Value)) found.Add("en [" + r.Key + "] reads as Spanish: " + r.Value);
            foreach (string s in found) Console.WriteLine("    tray: " + s);
            Check(found.Count == 0, found.Count + " tray texts are in the wrong language (listed above)");
        }

        // The date of a recent capture in the tray: the weekday-less "5 Jan" in the language's own month names (German
        // is not read with the Spanish ones) and Today / Yesterday in the language.
        public static void TrayRecentDatesFollowTheLanguage()
        {
            MethodInfo when = typeof(TrayPanel).GetMethod("When", BindingFlags.NonPublic | BindingFlags.Static);
            DateTime old = new DateTime(2020, 1, 5, 14, 30, 0), today = DateTime.Today.AddHours(9).AddMinutes(5);
            Dictionary<string, string> want = new Dictionary<string, string> { { "es", "5 ene, 14:30" }, { "en", "Jan 5, 14:30" }, { "de", "5. Jan, 14:30" }, { "ko", "1/5 14:30" } };
            Dictionary<string, string> now = new Dictionary<string, string> { { "es", "Hoy, 09:05" }, { "en", "Today, 09:05" } };
            foreach (string lang in AllLanguages)
                InLanguage(lang, delegate
                {
                    string d = (string)when.Invoke(null, new object[] { old }), n = (string)when.Invoke(null, new object[] { today });
                    Same(want.ContainsKey(lang) ? want[lang] : want["en"], d, lang + " old date");
                    if (now.ContainsKey(lang)) Same(now[lang], n, lang + " today");
                    else if (lang == "de" || lang == "ko") Check(!n.StartsWith("Hoy") && !n.StartsWith("Today") && n.EndsWith("09:05"), lang + " today: " + n);
                });
        }

        // ---- The six preview languages are hidden for this release

        static readonly string[] Hidden = { "fr", "pt", "zh", "hi", "ar", "bn" };

        static string[] Codes(IEnumerable<I18n.LanguageOption> options) { return options.Select(o => o.Code).ToArray(); }

        static LanguagePicker NewPicker(string current)
        {
            return (LanguagePicker)typeof(LanguagePicker).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null).Invoke(new object[] { current });
        }

        // As shipped, the picker (and the search behind Settings, Welcome and the tray's language row) offers Automatic,
        // Spanish, English, German and Korean and nothing else, whatever the interface language is.
        public static void PickerListsOnlyTheCompleteLanguages()
        {
            Check(!I18n.PreviewsListed, "the previews are hidden by default");
            Same("auto,es,en,de,ko", string.Join(",", Codes(I18n.Languages)), "the languages on offer");
            Same("auto,es,en,de,ko", string.Join(",", Codes(I18n.FilterLanguages(""))), "an empty search");
            foreach (string q in new[] { "fr", "francais", "French", "pt", "Portuguese", "zh", "mandarin", "hi", "Hindi", "ar", "\u0627\u0644\u0639\u0631\u0628\u064A\u0629", "bn", "Bengali" })
                Check(I18n.FilterLanguages(q).Count == 0, "searching '" + q + "' finds a hidden language");
            Same("de", string.Join(",", Codes(I18n.FilterLanguages("German"))), "a complete language is still found");
            foreach (string lang in AllLanguages)
                InLanguage(lang, delegate
                {
                    LanguagePicker picker = NewPicker("de");
                    try
                    {
                        ListBox list = (ListBox)typeof(LanguagePicker).GetField("list", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);
                        Same("auto,es,en,de,ko", string.Join(",", list.Items.Cast<ListBoxItem>().Select(i => (string)i.Tag).ToArray()), lang + " picker rows");
                        // The hint under the title only makes sense with previews: it is gone, in every language.
                        foreach (string t in AppTestsI18n.Strings(picker.Content))
                            Check(t.IndexOf("preview", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("vista previa", StringComparison.OrdinalIgnoreCase) < 0 &&
                                  t.IndexOf("Vorschau", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("\uBBF8\uB9AC \uBCF4\uAE30", StringComparison.Ordinal) < 0, lang + " picker still mentions previews: " + t);
                    }
                    finally { picker.Close(); }
                });
            string hint = null;
            foreach (string lang in new[] { "es", "en", "de", "ko" })
                InLanguage(lang, delegate
                {
                    LanguagePicker picker = NewPicker("de");
                    try { hint = AppTestsI18n.Strings(picker.Content).FirstOrDefault(t => t.StartsWith("Busca por") || t.StartsWith("Search by") || t.StartsWith("Suche nach") || t.StartsWith("\uC774\uB984\uC774\uB098")); }
                    finally { picker.Close(); }
                    Same(lang == "es" ? "Busca por nombre o c\u00F3digo." : lang == "en" ? "Search by name or code." : lang == "de" ? "Suche nach Name oder Sprachcode." : "\uC774\uB984\uC774\uB098 \uCF54\uB4DC\uB85C \uAC80\uC0C9\uD558\uC138\uC694.", hint, lang + " picker hint");
                });
        }

        // A settings file that names a hidden language reads as Automatic, like an unknown code; the complete ones stay.
        public static void HiddenLanguagesLoadAsAutomatic()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t20-hidden-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                foreach (string code in Hidden.Concat(new[] { "FR", " ar " }))
                {
                    string file = Path.Combine(dir, "h" + code.Trim() + ".ini");
                    File.WriteAllText(file, "SaveAll = 1\nLanguage = " + code + "\n");
                    Same("auto", Settings.Load(file).Language, "Language = " + code);
                }
                foreach (string code in new[] { "es", "en", "de", "ko", "auto" })
                {
                    string file = Path.Combine(dir, "k" + code + ".ini");
                    File.WriteAllText(file, "SaveAll = 1\nLanguage = " + code + "\n");
                    Same(code, Settings.Load(file).Language, "Language = " + code);
                }
            }
            finally { I18n.Apply("es"); try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        // One switch (I18n.PreviewsListed) brings the six languages back: listed, found, kept from the settings file, and
        // the picker's hint about them returns. Their catalog is untouched, so they still translate when applied.
        public static void PreviewsComeBackWithTheSwitch()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t20-back-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            I18n.PreviewsListed = true;
            try
            {
                Same("auto,es,en,de,ko,fr,pt,zh,hi,ar,bn", string.Join(",", Codes(I18n.Languages)), "the languages on offer");
                Check(I18n.FilterLanguages("francais").Count == 1 && I18n.FilterLanguages("mandarin").Count == 1, "the previews are searchable again");
                foreach (string code in Hidden)
                {
                    string file = Path.Combine(dir, code + ".ini");
                    File.WriteAllText(file, "SaveAll = 1\nLanguage = " + code + "\n");
                    Same(code, Settings.Load(file).Language, "Language = " + code);
                    I18n.Apply(code);
                    Check(I18n.Language == code && I18n.Save != "Save", code + " translates");
                }
                I18n.Apply("en");
                LanguagePicker picker = NewPicker("de");
                try
                {
                    ListBox list = (ListBox)typeof(LanguagePicker).GetField("list", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);
                    Check(list.Items.Count == 11, "Automatic and the ten languages in the list: " + list.Items.Count);
                    Check(AppTestsI18n.Strings(picker.Content).Any(t => t.StartsWith("Search by name or code. Preview languages")), "the hint about previews returns");
                }
                finally { picker.Close(); }
            }
            finally
            {
                I18n.PreviewsListed = false;
                I18n.Apply("es");
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
            Check(I18n.Languages.Length == 5, "the switch back off hides them again");
        }

        // ---- T28: the last audit of main (areas A to D), one test per rule.

        // "Recovered recordings (12 MB) remain in ...": the size stays in its parentheses and no counter word follows it.
        public static void KoreanUninstallNoteReadsClean()
        {
            InLanguage("ko", delegate
            {
                string note = I18n.Fmt("\n\nLas grabaciones recuperadas ({0}) se quedan en {1}.", "\n\nRecovered recordings ({0}) remain in {1}.", "12 MB", "C:\\Stackshot\\recuperado");
                Same("\n\n\uBCF5\uAD6C\uB41C \uB179\uD654(12 MB)\uB294 C:\\Stackshot\\recuperado\uC5D0 \uBCF4\uAD00\uB429\uB2C8\uB2E4.", note, "Korean uninstall note");
            });
            InLanguage("en", delegate { Same("\n\nRecovered recordings (12 MB) remain in C:\\x.", I18n.Fmt("\n\nLas grabaciones recuperadas ({0}) se quedan en {1}.", "\n\nRecovered recordings ({0}) remain in {1}.", "12 MB", "C:\\x"), "English uninstall note"); });
        }

        // The mascot's level-up line: a sentence of its own in Korean and German; Spanish and English as they were.
        public static void LevelUpLinesReadRight()
        {
            Settings s = new Settings();
            InLanguage("ko", delegate
            {
                for (int i = 0; i < MascotParts.LevelNames.Length; i++)
                    Same("\uC6B0\uB9AC \uC0AC\uC774\uAC00 \u2018" + MascotParts.LevelNames[i] + "\u2019 \uB2E8\uACC4\uAC00 \uB410\uC5B4!", MascotTalk.LevelUp(s, i), "Korean level-up " + i);
            });
            InLanguage("de", delegate
            {
                string[] want = { "Neulinge", "Kumpels", "Freunde", "beste Freunde", "unzertrennlich", "eine Legende" };
                for (int i = 0; i < want.Length; i++) Same("Jetzt sind wir " + want[i] + "!", MascotTalk.LevelUp(s, i), "German level-up " + i);
            });
            InLanguage("es", delegate { Same("\u00A1Ya somos mejores amigos!", MascotTalk.LevelUp(s, 3), "Spanish level-up"); Same("\u00A1Ya somos leyenda!", MascotTalk.LevelUp(s, 5), "Spanish last level-up"); });
            InLanguage("en", delegate { Same("We are now best friends!", MascotTalk.LevelUp(s, 3), "English level-up"); Same("We are now legend!", MascotTalk.LevelUp(s, 5), "English last level-up"); });
        }

        // One register in the Korean breaks note, as in the settings text around it.
        public static void KoreanBreaksNoteKeepsOneRegister()
        {
            InLanguage("ko", delegate { Same("\uC624\uB298\uC740 \uC544\uC9C1 \uD734\uC2DD \uAE30\uB85D\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.", I18n.T("Hoy todav\u00EDa no has hecho ninguna pausa.", "You haven't taken any breaks yet today."), "Korean breaks note"); });
            InLanguage("de", delegate { Same("Heute hast du noch keine Pause gemacht.", I18n.T("Hoy todav\u00EDa no has hecho ninguna pausa.", "You haven't taken any breaks yet today."), "German breaks note"); });
        }

        // Durations in German go through the catalog, as Korean's do: "25 Min.", "1 h 15 Min."; Spanish and English as before.
        public static void GermanDurationsGoThroughTheCatalog()
        {
            InLanguage("de", delegate
            {
                Same("25 Min.", FocusClock.Minutes(25), "German 25");
                Same("1 h", FocusClock.Minutes(60), "German 60");
                Same("1 h 15 Min.", FocusClock.Minutes(75), "German 75");
                Same("2 h", FocusClock.Minutes(120), "German 120");
                Same("Nein|20 Min.|30 Min.|45 Min.|1 h|1 h 30 Min.|2 h", string.Join("|", WellKind.MinuteNames).Replace("Aus", "Nein"), "German wellness intervals");
                Same("1 h 15 Min.", typeof(ShotStack).GetMethod("Hours", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 75 }).ToString(), "German health hours");
            });
            foreach (string lang in new[] { "es", "en" })
                InLanguage(lang, delegate
                {
                    Same("25 min", FocusClock.Minutes(25), lang + " 25");
                    Same("1 h 15 min", FocusClock.Minutes(75), lang + " 75");
                    Same(I18n.T("No", "Off") + "|20 min|30 min|45 min|1 h|1 h 30|2 h", string.Join("|", WellKind.MinuteNames), lang + " wellness intervals");
                    Same("2 h 5 min", typeof(ShotStack).GetMethod("Hours", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 125 }).ToString(), lang + " health hours");
                });
        }

        // The seconds choices and the "Auto" choice are texts of the language, not raw literals.
        public static void SegmentSecondsAndAutoFollowTheLanguage()
        {
            InLanguage("ko", delegate
            {
                Same("3\uCD08|5\uCD08|10\uCD08", string.Join("|", new[] { RecordSetup.Seconds(3), RecordSetup.Seconds(5), RecordSetup.Seconds(10) }), "Korean seconds");
            });
            foreach (string lang in new[] { "es", "en", "de" })
                InLanguage(lang, delegate { Same("3 s|5 s|10 s", string.Join("|", new[] { RecordSetup.Seconds(3), RecordSetup.Seconds(5), RecordSetup.Seconds(10) }), lang + " seconds"); });
            foreach (string f in new[] { "Home/HomePages.cs", "Home/HomeRecordPage.cs", "Capture/RecordSetup.cs" })
                Check(!Regex.IsMatch(Src(f), "\"3 s\"\\s*,\\s*\"5 s\"\\s*,\\s*\"10 s\""), f + " spells the seconds choices itself");
            Check(!Regex.IsMatch(Src("Home/HomeBackdrop.cs"), "new string\\[\\] \\{ \"Auto\", \"16:9\""), "the aspect ratio choices spell Auto itself");
            InLanguage("ko", delegate { Same("\uC790\uB3D9", I18n.T("Auto", "Auto", "Auto"), "Korean Auto"); });
            InLanguage("de", delegate { Same("Auto", I18n.T("Auto", "Auto", "Auto"), "German Auto"); });
        }

        // A capture's file name says what it is with a noun in German ("Aufnahme"); the region picker's button keeps its verb.
        public static void GermanCaptureNamesUseTheNoun()
        {
            InLanguage("de", delegate
            {
                Same("Aufnahme", NameTemplate.CaptureWord, "German capture word");
                Same("Aufnehmen", I18n.T("Capturar", "Capture"), "the button keeps the verb");
                Check(NameTemplate.Example("{type}", new DateTime(2025, 1, 2, 3, 4, 5)).StartsWith("Aufnahme"), "the example in Settings: " + NameTemplate.Example("{type}", new DateTime(2025, 1, 2, 3, 4, 5)));
            });
            InLanguage("es", delegate { Same("Captura", NameTemplate.CaptureWord, "Spanish capture word"); });
            InLanguage("en", delegate { Same("Capture", NameTemplate.CaptureWord, "English capture word"); });
            InLanguage("ko", delegate { Same(I18n.Localize("Capture"), NameTemplate.CaptureWord, "Korean capture word"); });
            foreach (string f in new[] { "ShotStack.cs", "ShotStack.Delayed.cs", "ShotStack.RepeatArea.cs" })
                Check(!Src(f).Contains("I18n.T(\"Captura\", \"Capture\")"), f + " names a capture with the verb's translation");
        }

        // The settings search folds what it is given and what it knows: every synonym and filler word is in folded form.
        public static void SearchWordsAreFolded()
        {
            List<string> bad = new List<string>();
            foreach (string[] g in SettingsSearch.Synonyms)
                foreach (string m in g) if (HistoryIndex.Fold(m) != m) bad.Add("synonym " + Show(m));
            foreach (string w in SettingsSearch.StopWords) if (HistoryIndex.Fold(w) != w) bad.Add("filler " + Show(w));
            Check(bad.Count == 0, "unfolded search words: " + string.Join(", ", bad));
            Check(SettingsSearch.Alternatives(HistoryIndex.Fold("L\u00F6schen")).Contains("entfernen"), "l\u00F6schen finds entfernen");
            Check(SettingsSearch.Alternatives(HistoryIndex.Fold("Lautst\u00E4rke")).Contains("ton"), "Lautst\u00E4rke finds Ton");
            Check(SettingsSearch.Alternatives(HistoryIndex.Fold("Tastenk\u00FCrzel")).Contains("kurzbefehl"), "Tastenk\u00FCrzel finds Kurzbefehl");
            Check(SettingsSearch.Alternatives(HistoryIndex.Fold("Schw\u00E4rzen")).Contains("datenschutz"), "schw\u00E4rzen finds Datenschutz");
            Same("ordner|aufnahmen", string.Join("|", SettingsSearch.Words("Ordner f\u00FCr Aufnahmen \u00E4ndern")), "German filler words");
        }

        // The Settings and Home pages say "vor 12 min" in German, and no sentence that quotes it ends in two periods.
        public static void GermanAgoHasNoDoublePeriod()
        {
            MethodInfo when = typeof(HomeWindow).GetMethod("When", BindingFlags.NonPublic | BindingFlags.Static);
            DateTime past = DateTime.UtcNow.AddMinutes(-12.5);
            InLanguage("de", delegate
            {
                string ago = (string)when.Invoke(null, new object[] { past });
                Check(ago.StartsWith("vor 12"), "German ago: " + ago);
                string a = I18n.Fmt(" \u00DAltima comprobaci\u00F3n: {0}.", " Last checked: {0}.", ago), b = I18n.Fmt("Stackshot {0} es la versi\u00F3n m\u00E1s reciente. Comprobado {1}.", "Stackshot {0} is the latest version. Checked {1}.", "2.2.2", ago);
                Check(!a.Contains("..") && !b.Contains(".."), "double period: <" + a + "> <" + b + ">");
            });
            InLanguage("es", delegate { Same("hace 12 min", (string)when.Invoke(null, new object[] { past }), "Spanish ago"); });
            InLanguage("en", delegate { Same("12 min ago", (string)when.Invoke(null, new object[] { past }), "English ago"); });
            InLanguage("ko", delegate { Same("12\uBD84 \uC804", (string)when.Invoke(null, new object[] { past }), "Korean ago"); });
        }

        // Korean version sentences say "\uBC84\uC804\uC5D0\uC11C ... \uBC84\uC804\uC73C\uB85C" instead of leaving a particle pair open after a number.
        public static void KoreanVersionSentencesHaveNoOpenParticle()
        {
            InLanguage("ko", delegate
            {
                Same("Stackshot\uC744 2.2.1 \uBC84\uC804\uC5D0\uC11C 2.2.2 \uBC84\uC804\uC73C\uB85C \uC5C5\uB370\uC774\uD2B8\uD560\uAE4C\uC694?", I18n.Fmt("\u00BFActualizar Stackshot de la versi\u00F3n {0} a la {1}?", "Update Stackshot from version {0} to {1}?", "2.2.1", "2.2.2"), "update question");
                Same("2.3.0 \uBC84\uC804\uC73C\uB85C \uC5C5\uB370\uC774\uD2B8\uD560 \uC218 \uC5C6\uC74C", I18n.Fmt("No se pudo actualizar a la {0}", "Could not update to {0}", "2.3.0"), "update failed");
                Check(!I18n.Fmt("Volver a {0}", "Reset to {0}", "{type} {date}").Contains("(\uC73C)"), "reset tooltip keeps an open pair: " + I18n.Fmt("Volver a {0}", "Reset to {0}", "{type} {date}"));
                FieldInfo f = typeof(I18n).GetField("KoreanCatalog", BindingFlags.NonPublic | BindingFlags.Static);
                object lazy = f.GetValue(null);
                Dictionary<string, string> catalog = (Dictionary<string, string>)lazy.GetType().GetProperty("Value").GetValue(lazy, null);
                List<string> open = new List<string>();
                foreach (KeyValuePair<string, string> p in catalog)
                    if (Regex.IsMatch(p.Value, "\\{\\d\\}[^ ]{0,2}\\(\uC73C\\)\uB85C") && Regex.IsMatch(p.Key, "version|\\{0\\}$")) open.Add(p.Key);
                Check(open.Count == 0, "open (\uC73C)\uB85C after a variable: " + string.Join(" | ", open));
            });
        }

        // After a value, \uB85C or \uC73C\uB85C is chosen from the value (\u3139 and vowels take \uB85C), as the other pairs are.
        public static void KoreanEuroFollowsTheValue()
        {
            InLanguage("ko", delegate
            {
                string key = "{0} moved from {1} to {2}.";
                Same("Ctrl + S \uD0A4 \uD560\uB2F9\uC744 \u2018\uC601\uC5ED \uCEA1\uCC98\u2019\uC5D0\uC11C \u2018\uC804\uCCB4 \uD654\uBA74\u2019\uC73C\uB85C \uC62E\uACBC\uC2B5\uB2C8\uB2E4.", I18n.Fmt("", key, "Ctrl + S", "\uC601\uC5ED \uCEA1\uCC98", "\uC804\uCCB4 \uD654\uBA74"), "\uC73C\uB85C after a consonant");
                Same("Ctrl + S \uD0A4 \uD560\uB2F9\uC744 \u2018\uD654\uBA74 \uC804\uCCB4\u2019\uC5D0\uC11C \u2018\uC601\uC5ED \uCEA1\uCC98\u2019\uB85C \uC62E\uACBC\uC2B5\uB2C8\uB2E4.", I18n.Fmt("", key, "Ctrl + S", "\uD654\uBA74 \uC804\uCCB4", "\uC601\uC5ED \uCEA1\uCC98"), "\uB85C after a vowel");
                Same("Ctrl + S \uD0A4 \uD560\uB2F9\uC744 \u2018\uC601\uC5ED \uCEA1\uCC98\u2019\uC5D0\uC11C \u2018\uB2EC\u2019\uB85C \uC62E\uACBC\uC2B5\uB2C8\uB2E4.", I18n.Fmt("", key, "Ctrl + S", "\uC601\uC5ED \uCEA1\uCC98", "\uB2EC"), "\uB85C after \u3139");
                Same("Ctrl + S \uD0A4 \uD560\uB2F9\uC744 \u2018\uC601\uC5ED \uCEA1\uCC98\u2019\uC5D0\uC11C \u2018Pixel\u2019(\uC73C)\uB85C \uC62E\uACBC\uC2B5\uB2C8\uB2E4.", I18n.Fmt("", key, "Ctrl + S", "\uC601\uC5ED \uCEA1\uCC98", "Pixel"), "both after a Latin name");
                Same("\uC73C\uB85C", I18n.Euro("\uC0BC"), "\uC0BC \uC73C\uB85C");
                Same("\uB85C", I18n.Euro("\uC77C"), "\uC77C \uB85C");
                Same("(\uC73C)\uB85C", I18n.Euro("Rex"), "Latin");
            });
        }

        // The Korean welcome says one thing per row: the key and what it does.
        public static void KoreanWelcomeEditHint()
        {
            InLanguage("ko", delegate { Same("\uC378\uB124\uC77C\uC5D0\uC11C \uD3B8\uC9D1", I18n.T("en la miniatura para editarla", "on thumbnail to edit it"), "welcome edit hint"); });
        }

        // The English documents call the first choice of the language list what the English interface calls it.
        public static void EnglishDocsSayAutoLikeTheInterface()
        {
            InLanguage("en", delegate { Same("Auto", I18n.Languages[0].Name, "English picker name"); });
            string root = AppTestsI18nCoverage.RepoRoot();
            foreach (string f in new[] { "README.md", "CHANGELOG.md" })
            {
                string t = File.ReadAllText(Path.Combine(root, f), Encoding.UTF8);
                Check(!Regex.IsMatch(t, "Automatic, Espa\u00F1ol|choose Automatic|\\(Automatic\\)"), f + " calls the language choice Automatic");
            }
        }

        // The 2.2.2 section of the Spanish news writes the settings path with the same arrows as the other three.
        public static void NewsSpanishArrowsMatch()
        {
            string root = AppTestsI18nCoverage.RepoRoot();
            foreach (string f in new[] { "novedades.md", "novedades.en.md", "novedades.de.md", "novedades.ko.md" })
            {
                string[] lines = File.ReadAllLines(Path.Combine(root, "docs", f), Encoding.UTF8);
                int head = Array.FindIndex(lines, l => l.StartsWith("## 2.2.2", StringComparison.Ordinal));
                Check(head >= 0, f + " has no 2.2.2 section");
                for (int i = head + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++)
                    Check(!Regex.IsMatch(lines[i], "\\p{L} > \\p{L}"), f + " writes a settings path with > : " + lines[i]);
            }
        }

        // The picker's name of a language the list does not offer is Automatic's, as a saved choice of it loads as Automatic.
        public static void PickerNameOnlyForListedLanguages()
        {
            Check(!I18n.PreviewsListed, "previews are hidden");
            InLanguage("es", delegate
            {
                Same("\uD55C\uAD6D\uC5B4", LanguagePicker.DisplayName("ko"), "a listed language");
                foreach (string hidden in new[] { "fr", "pt", "zh", "hi", "ar", "bn", "xx" })
                    Same(I18n.Languages[0].Name, LanguagePicker.DisplayName(hidden), hidden + " shows as Automatic");
            });
        }

        // The wellness reminders say a duration the way the language writes it, whatever its length ("2\uC2DC\uAC04", not "120\uBD84").
        public static void KoreanReminderDurations()
        {
            Settings s = new Settings();
            InLanguage("ko", delegate
            {
                HashSet<string> seen = new HashSet<string>();
                for (int k = 0; k < WellKind.Count; k++)
                    for (int p = 0; p < 3; p++)
                    {
                        s.MascotPersonality = p;
                        for (int n = 0; n < 40; n++) { seen.Add(MascotTalk.Remind(s, k, 120)); seen.Add(MascotTalk.Remind(s, k, 90)); }
                    }
                Check(seen.Any(t => t.Contains("2\uC2DC\uAC04")), "no reminder says 2\uC2DC\uAC04");
                Check(!seen.Any(t => t.Contains("120\uBD84") || t.Contains("90\uBD84")), "a reminder counts hours in minutes: " + string.Join(" | ", seen.Where(t => t.Contains("120\uBD84") || t.Contains("90\uBD84"))));
            });
        }

        // German mascot wording: "auf dem Stuhl", and the dragon's name as the German books spell it.
        public static void GermanMascotWording()
        {
            Settings s = new Settings();
            InLanguage("de", delegate
            {
                Same("Gronckel", MascotParts.Kinds[MascotParts.KindGronckle], "German Gronckle name");
                HashSet<string> seen = new HashSet<string>();
                for (int p = 0; p < 3; p++)
                {
                    s.MascotPersonality = p;
                    for (int n = 0; n < 40; n++) seen.Add(MascotTalk.Remind(s, WellKind.Move, 60));
                }
                Check(seen.Any(t => t.Contains("auf dem Stuhl")), "no reminder says auf dem Stuhl");
                Check(!seen.Any(t => t.Contains("in dem Stuhl")), "in dem Stuhl");
            });
        }

        // The search field says Strg+F in German, Ctrl+F elsewhere.
        public static void SearchKeyIsStrgInGerman()
        {
            InLanguage("de", delegate { Same("Strg+F", Hotkeys.CtrlF, "German"); });
            foreach (string lang in new[] { "es", "en", "ko" }) InLanguage(lang, delegate { Same("Ctrl+F", Hotkeys.CtrlF, lang); });
            foreach (string f in new[] { "Home/HomeHistoryPage.cs", "Home/HomeSearch.cs" })
                Check(!Regex.IsMatch(Src(f), "(Label\\(|SetHelpText\\(box, )\"Ctrl\\+F\""), f + " shows Ctrl+F as a literal");
        }

        // No Korean text pins \uB85C, \uC740, \uC744... after a quoted variable: the pair is written and settled from the value.
        public static void KoreanParticlesAfterQuotedValues()
        {
            InLanguage("ko", delegate
            {
                FieldInfo f = typeof(I18n).GetField("KoreanCatalog", BindingFlags.NonPublic | BindingFlags.Static);
                object lazy = f.GetValue(null);
                Dictionary<string, string> catalog = (Dictionary<string, string>)lazy.GetType().GetProperty("Value").GetValue(lazy, null);
                Regex pinned = new Regex("\\{\\d\\}[\u201D\u2019\"']+(?:\uC73C?\uB85C|[\uC640\uACFC\uC740\uB294\uC774\uAC00\uC744\uB97C])(?![\uAC00-\uD7A3(])");
                List<string> bad = new List<string>();
                foreach (KeyValuePair<string, string> p in catalog) if (pinned.IsMatch(p.Value)) bad.Add(p.Key + " => " + Show(p.Value));
                foreach (string s in bad) Console.WriteLine("    pinned after a quote: " + s);
                Check(bad.Count == 0, bad.Count + " Korean texts pin a particle after a quoted variable");
                Same("Google\uC5D0\uC11C \u201Caccess_denied\u201D\uC744(\uB97C) \uBC18\uD658\uD588\uC2B5\uB2C8\uB2E4. \uD0ED\uC744 \uB2EB\uACE0 Stackshot\uC5D0\uC11C \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.", I18n.Fmt("", "Google returned \u201C{0}\u201D. Close this tab and try again from Stackshot.", "access_denied"), "an unknown ending keeps the pair");
                Same("Google\uC5D0\uC11C \u201C\uAC70\uBD80\uB428\u201D\uC744 \uBC18\uD658\uD588\uC2B5\uB2C8\uB2E4. \uD0ED\uC744 \uB2EB\uACE0 Stackshot\uC5D0\uC11C \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.", I18n.Fmt("", "Google returned \u201C{0}\u201D. Close this tab and try again from Stackshot.", "\uAC70\uBD80\uB428"), "settled by the value");
            });
        }

        // The scrolling capture's "saved as a normal capture" notice is read whole in a three-line toast at every scale.
        public static void ScrollingPlainNoticeFits()
        {
            List<string> cut = new List<string>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
            {
                I18n.Apply(lang);
                string title = I18n.T("Captura con desplazamiento", "Scrolling capture"), body = ScrollSession.PlainNotice;
                foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                {
                    ToastLook look = ToastLook.For(new Notice(title, body, NoticeKind.Info), scale);
                    if (look.Clipped || look.TitleClipped) cut.Add(lang + " at " + (int)(scale * 100) + " %: " + look.Lines + " lines, " + body.Length + " chars");
                }
            }
            I18n.Apply("es");
            foreach (string c in cut) Console.WriteLine("    cut: " + c);
            Check(cut.Count == 0, cut.Count + " notices are cut");
        }

        // A folder in an error message is quoted the way the language quotes, and the folder-less wording of German is grammatical.
        public static void FolderErrorsQuoteLikeTheLanguage()
        {
            Exception denied = new UnauthorizedAccessException(), missing = new DirectoryNotFoundException(), full = new IOException("disk", unchecked((int)0x80070070));
            string path = "C:\\Zielordner\\a.png";
            string[][] quotes = { new[] { "es", "\u00ABC:\\Zielordner\u00BB" }, new[] { "en", "\u201CC:\\Zielordner\u201D" }, new[] { "de", "\u201EC:\\Zielordner\u201C" }, new[] { "ko", "\u2018C:\\Zielordner\u2019" } };
            foreach (string[] q in quotes)
                InLanguage(q[0], delegate
                {
                    foreach (Exception ex in new[] { denied, missing, full })
                    {
                        string text = ShotStack.Explain(ex, path);
                        Check(text.Contains(q[1]), q[0] + ": the folder is not quoted " + q[1] + ": " + text);
                        if (q[0] != "es") Check(text.IndexOf('\u00AB') < 0 && text.IndexOf('\u00BB') < 0, q[0] + " uses Spanish quotes: " + text);
                    }
                });
            InLanguage("de", delegate
            {
                Same("Windows kann nicht in den Ordner schreiben. Pr\u00FCfe die Ordnerberechtigungen oder ob der \u201Ekontrollierte Ordnerzugriff\u201C ihn sch\u00FCtzt.", ShotStack.Explain(denied, null), "write denied, no folder");
                Same("Auf dem Laufwerk f\u00FCr den Ordner ist kein Platz mehr. Schaffe Platz und versuch es erneut.", ShotStack.Explain(full, null), "disk full, no folder");
                Same("Der Ordner wurde nicht gefunden (ein getrenntes oder Netzlaufwerk?).", ShotStack.Explain(missing, null), "folder missing, no folder");
                Same("Der Ordner \u201EC:\\Zielordner\u201C wurde nicht gefunden (ein getrenntes oder Netzlaufwerk?).", ShotStack.Explain(missing, path), "folder missing, with folder");
            });
            InLanguage("es", delegate { Same("Windows no deja escribir en \u00ABC:\\Zielordner\u00BB. Comprueba los permisos de la carpeta o si \u00ABAcceso controlado a carpetas\u00BB la protege.", ShotStack.Explain(denied, path), "Spanish unchanged"); });
        }

        // "None" as a choice is "Keine" in German; the lower-case word stays inside the sentence that needs it.
        public static void GermanNoneChoiceIsCapitalized()
        {
            InLanguage("de", delegate
            {
                Same("Keine", I18n.T("Ninguno", "None"), "German None choice");
                Same("Keine", I18n.T("No", "None"), "German None choice, second key");
            });
            Check(Src("Editor/ShareCheck.cs").Contains("I18n.T(\"ninguna\", \"none\", \"keine\")"), "the sentence in the share check lost its lower-case word");
        }

        // What people type that the settings search did not know yet (English, Korean), found in the language's own index.
        public static void SettingsSearchGapsAreClosed()
        {
            string[][] queries =
            {
                new[] { "en", "privacy", "any" }, new[] { "en", "delete", "any" }, new[] { "en", "hotkey", "any" },
                new[] { "en", "change language", "Language" }, new[] { "en", "screenshot folder", "folder" },
                new[] { "ko", "\uB2E4\uD06C \uBAA8\uB4DC", "\uD14C\uB9C8" }, new[] { "ko", "\uC5B8\uC5B4 \uBCC0\uACBD", "\uC5B8\uC5B4" }, new[] { "ko", "\uC708\uB3C4\uC6B0 \uC2DC\uC791", "Windows \uC2DC\uC791 \uC2DC \uC2E4\uD589" },
                new[] { "de", "tastenk\u00FCrzel", "any" }, new[] { "de", "Lautst\u00E4rke \u00E4ndern", "any" }, new[] { "de", "l\u00F6schen", "any" }
            };
            List<string> bad = new List<string>();
            foreach (string lang in new[] { "en", "ko", "de" })
            {
                I18n.Apply(lang);
                HomeWindow hw = AppTestsI18n.Make(1);
                try
                {
                    List<SearchEntry> ix = hw.SearchIndexNow(null);
                    foreach (string[] q in queries)
                    {
                        if (q[0] != lang) continue;
                        List<SearchHit> hits = SettingsSearch.Find(ix, q[1]);
                        string titles = string.Join(" | ", hits.Take(6).Select(h => h.Entry.Title));
                        bool ok = q[2] == "any" ? hits.Count > 0 : q[2] == "folder" ? hits.Any(h => h.Entry.Icon == "folder") : hits.Take(3).Any(h => h.Entry.Title == q[2]);
                        if (!ok) bad.Add(lang + " <" + q[1] + "> -> " + titles);
                    }
                }
                finally { hw.Shutdown(); I18n.Apply("es"); }
            }
            foreach (string b in bad) Console.WriteLine("    search gap: " + b);
            Check(bad.Count == 0, bad.Count + " searches find nothing useful");
        }

        // The Markdown/XML window's empty drop zone shows its help line whole, wrapped and centred, in every language.
        public static void MarkdownZoneHelpLineFits()
        {
            List<string> bad = new List<string>();
            foreach (string lang in new[] { "es", "en", "de", "ko" })
            {
                I18n.Apply(lang);
                Ds.Apply(1);
                MarkdownWindow w = (MarkdownWindow)typeof(MarkdownWindow).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(new object[] { null });
                try
                {
                    w.Left = -6000; w.Top = 0; w.ShowActivated = false; w.ShowInTaskbar = false;
                    w.Show();
                    w.UpdateLayout();
                    string help = I18n.T("Ctrl+V pega desde el portapapeles. Admite .md y .txt de hasta 2 MB, y .xml de hasta 8 MB.", "Ctrl+V pastes from the clipboard. Accepts .md and .txt up to 2 MB, and .xml up to 8 MB.");
                    TextBlock line = null;
                    AppTestsI18n.Walk((DependencyObject)w.Content, new HashSet<DependencyObject>(), delegate(DependencyObject d) { TextBlock t = d as TextBlock; if (t != null && t.Text == help) line = t; });
                    if (line == null) { bad.Add(lang + ": help line not found"); continue; }
                    FrameworkElement zone = (FrameworkElement)((FrameworkElement)line.Parent).Parent;
                    if (line.TextWrapping != TextWrapping.Wrap) bad.Add(lang + ": the help line does not wrap");
                    if (line.TextAlignment != TextAlignment.Center) bad.Add(lang + ": the help line is not centred");
                    if (line.ActualWidth > zone.ActualWidth - 40) bad.Add(lang + ": the help line is " + (int)line.ActualWidth + " wide in a zone " + (int)zone.ActualWidth);
                }
                finally { try { w.Close(); } catch (Exception) { } }
            }
            I18n.Apply("es");
            foreach (string b in bad) Console.WriteLine("    zone: " + b);
            Check(bad.Count == 0, bad.Count + " empty-zone problems");
        }

        // The scrolling capture bar's two small buttons: Auto in Hangul in Korean, "\uBA48\uCDA4" for pause in its 64 px; others as they were.
        public static void ScrollBarButtonLabels()
        {
            InLanguage("ko", delegate { Same("\uC790\uB3D9", ScrollCaptureBar.AutoLabel, "Korean Auto"); Same("\uBA48\uCDA4", ScrollCaptureBar.PauseLabel, "Korean pause"); });
            InLanguage("de", delegate { Same("Auto", ScrollCaptureBar.AutoLabel, "German Auto"); Same("Pause", ScrollCaptureBar.PauseLabel, "German pause"); });
            InLanguage("es", delegate { Same("Auto", ScrollCaptureBar.AutoLabel, "Spanish Auto"); Same("Pausa", ScrollCaptureBar.PauseLabel, "Spanish pause"); });
            InLanguage("en", delegate { Same("Auto", ScrollCaptureBar.AutoLabel, "English Auto"); Same("Pause", ScrollCaptureBar.PauseLabel, "English pause"); });
            Check(!Src("Capture/ScrollCapture.cs").Contains(": \"Auto\", Ds.Semibold"), "the bar paints a literal Auto");
        }

        // What a screen reader says for a backdrop slider is in the language.
        public static void BackdropSliderHelpFollowsTheLanguage()
        {
            string[][] want = { new[] { "es", "De 0 a 100" }, new[] { "en", "From 0 to 100" }, new[] { "de", "Von 0 bis 100" }, new[] { "ko", "0~100" } };
            foreach (string[] w in want) InLanguage(w[0], delegate { Same(w[1], BgPanel.SliderRange, w[0] + " slider help"); });
            Check(!Src("Editor/BgPanel.Access.cs").Contains("PartHelp = \"De 0 a 100\""), "the slider help is a literal");
        }

        // Korean names the media and browser keys in Hangul (the keycaps of a Korean keyboard print them in English, but these
        // have no legend of their own); the other languages keep theirs.
        public static void KoreanNamesTheMediaKeys()
        {
            System.Windows.Forms.Keys[] keys = { System.Windows.Forms.Keys.VolumeMute, System.Windows.Forms.Keys.VolumeDown, System.Windows.Forms.Keys.VolumeUp, System.Windows.Forms.Keys.MediaPlayPause,
                System.Windows.Forms.Keys.MediaNextTrack, System.Windows.Forms.Keys.MediaPreviousTrack, System.Windows.Forms.Keys.MediaStop, System.Windows.Forms.Keys.BrowserBack, System.Windows.Forms.Keys.BrowserForward,
                System.Windows.Forms.Keys.BrowserRefresh, System.Windows.Forms.Keys.BrowserStop, System.Windows.Forms.Keys.BrowserSearch, System.Windows.Forms.Keys.BrowserFavorites, System.Windows.Forms.Keys.BrowserHome,
                System.Windows.Forms.Keys.LaunchMail, System.Windows.Forms.Keys.LaunchApplication1, System.Windows.Forms.Keys.LaunchApplication2, System.Windows.Forms.Keys.SelectMedia, System.Windows.Forms.Keys.Sleep };
            InLanguage("ko", delegate
            {
                foreach (System.Windows.Forms.Keys k in keys) Check(Regex.IsMatch(Hotkeys.KeyName(k), "[\uAC00-\uD7A3]"), "Korean key name of " + k + " is " + Hotkeys.KeyName(k));
                Same("\uC74C\uC18C\uAC70", Hotkeys.KeyName(System.Windows.Forms.Keys.VolumeMute), "mute");
            });
            InLanguage("en", delegate { Same("Mute", Hotkeys.KeyName(System.Windows.Forms.Keys.VolumeMute), "English mute"); });
            InLanguage("de", delegate { Same("Stumm", Hotkeys.KeyName(System.Windows.Forms.Keys.VolumeMute), "German mute"); });
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

        // The tray panel as it paints on screen, in this language.
        static void TrayPicture(string lang, int appearance, string file)
        {
            I18n.Apply(lang);
            Ds.Apply(appearance);
            Settings s = new Settings();
            s.Appearance = appearance;
            BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance;
            TrayPanel tp = new TrayPanel(s);
            try
            {
                typeof(TrayPanel).GetMethod("Rebuild", np).Invoke(tp, new object[] { 1f, 0 });
                System.Drawing.Size body = (System.Drawing.Size)typeof(FloatWindow).GetField("body", np).GetValue(tp);
                int pad = (int)typeof(FloatWindow).GetField("Pad", np).GetValue(tp);
                int w = body.Width + 2 * pad, h = body.Height + 2 * pad;
                DrawingVisual v = new DrawingVisual();
                using (DrawingContext dc = v.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(appearance == 2 ? Color.FromRgb(30, 30, 32) : Color.FromRgb(214, 224, 238)), null, new Rect(0, 0, w, h));
                    typeof(TrayPanel).GetMethod("PaintSurface", np).Invoke(tp, new object[] { dc, w, h });
                }
                RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(v);
                SavePng(rtb, file);
            }
            finally { try { tp.Dispose(); } catch (Exception) { } }
        }

        public static void Pictures()
        {
            string dir = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                foreach (string lang in new[] { "es", "en", "de", "ko", "ar" }) TrayPicture(lang, 1, "t20-tray-" + lang + ".png");
                TrayPicture("ko", 2, "t20-tray-ko-dark.png");

                foreach (string sl in new[] { "es", "en", "de", "ko" })
                {
                    I18n.Apply(sl);
                    Ds.Apply(1);
                    Settings ss = new Settings();
                    ss.SaveFolder = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t29-scene");
                    ss.ShowRecent = false; ss.ShowIntro = false; ss.Profile = 2; ss.Appearance = 1; ss.HomeStyle = 2;
                    HomeWindow sw = new HomeWindow((ShotStack)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ShotStack)), ss);
                    try { AppTestsI18n.SetPage(sw, "home"); AppTestsI18n.Layout(sw); SaveElement((FrameworkElement)sw.Content, 980, 660, "t29-scene-" + sl + ".png"); }
                    finally { sw.Shutdown(); }
                }

                foreach (int shade in new[] { 1, 2 })
                    foreach (int style in new[] { 0, 1, 2 })
                    {
                        I18n.Apply("ko");
                        Ds.Apply(shade);
                        Settings hs = new Settings();
                        hs.SaveFolder = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "t29-shots");
                        hs.ShowRecent = false; hs.ShowIntro = false; hs.Profile = 2; hs.Appearance = shade; hs.HomeStyle = style;
                        HomeWindow hw = new HomeWindow((ShotStack)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ShotStack)), hs);
                        try { AppTestsI18n.SetPage(hw, "home"); AppTestsI18n.Layout(hw); SaveElement((FrameworkElement)hw.Content, 980, 660, "t29-ko-home-" + style + "-" + shade + ".png"); }
                        finally { hw.Shutdown(); }
                    }

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
                // T28: the Settings pages with the seconds choices, Auto and the German "Keine", and the empty Markdown window.
                foreach (string lang in new[] { "de", "ko" })
                {
                    I18n.Apply(lang);
                    Ds.Apply(1);
                    foreach (string page in new[] { "general", "record", "editor" })
                    {
                        HomeWindow hw = AppTestsI18n.Make(1);
                        try { AppTestsI18n.SetPage(hw, page); AppTestsI18n.Layout(hw); SaveElement((FrameworkElement)hw.Content, 980, 660, "t28-" + lang + "-" + page + ".png"); }
                        finally { hw.Shutdown(); }
                    }
                    MarkdownWindow mw = (MarkdownWindow)typeof(MarkdownWindow).GetConstructors(np)[0].Invoke(new object[] { null });
                    try
                    {
                        mw.Left = -6000; mw.Top = 0; mw.ShowActivated = false; mw.ShowInTaskbar = false;
                        mw.Show();
                        mw.UpdateLayout();
                        SaveElement((FrameworkElement)mw.Content, mw.ActualWidth, mw.ActualHeight, "t28-" + lang + "-markdown-empty.png");
                    }
                    finally { try { mw.Close(); } catch (Exception) { } }
                }
            }
            finally { I18n.Apply("es"); }
        }
    }
}
