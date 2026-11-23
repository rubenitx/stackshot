// Suite "i18n", audit regressions: layout and text checks that every interface language has to pass.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
    }
}
