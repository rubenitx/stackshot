// Suite "i18n", audit regressions: layout and text checks that every interface language has to pass.
using System;
using System.Collections.Generic;
using System.Globalization;
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
    }
}
