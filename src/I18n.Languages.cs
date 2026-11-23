// Stackshot - Language metadata shared by settings, the picker and OCR.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Stackshot
{
    public static partial class I18n
    {
        internal sealed class LanguageOption
        {
            internal readonly string Code, NativeName, SearchNames;
            internal readonly int PreviewIndex;

            internal LanguageOption(string code, string name, string searchNames, int previewIndex = -1)
            {
                Code = code;
                NativeName = name;
                SearchNames = searchNames;
                PreviewIndex = previewIndex;
            }

            internal bool Preview { get { return PreviewIndex >= 0; } }
            internal string Name { get { return Code == "auto" ? T("Automático", "Auto") : NativeName; } }
        }

        // The six preview languages (fr, pt, zh, hi, ar, bn) are written but not offered until their translations are
        // complete: with this off the picker, the settings search, Settings and Welcome list only Automatic, Spanish,
        // English, German and Korean, and a saved choice of a hidden language loads as Automatic. Flip it to list them
        // again; their code and catalog (I18n.Preview.cs) stay as they are. Tests flip it to prove the way back.
        internal static bool PreviewsListed = false;

        // Every language the program knows, listed or not. Apply still takes any of them, so the hidden languages'
        // code paths can be exercised directly.
        internal static readonly LanguageOption[] Known =
        {
            new LanguageOption("auto", "", "auto windows automático automatisch"),
            new LanguageOption("es", "Español", "Spanish castellano Spanisch"),
            new LanguageOption("en", "English", "inglés Englisch"),
            new LanguageOption("de", "Deutsch", "German alemán"),
            new LanguageOption("ko", "\uD55C\uAD6D\uC5B4", "Korean"),
            new LanguageOption("fr", "Français", "French francés Französisch", 0),
            new LanguageOption("pt", "Português", "Portuguese portugués Portugiesisch Brasil Brazil Portugal", 1),
            new LanguageOption("zh", "简体中文", "Chinese chino Chinesisch Mandarin mandarín pinyin zhongwen", 2),
            new LanguageOption("hi", "हिन्दी", "Hindi हिन्दी हिंदी", 3),
            new LanguageOption("ar", "العربية", "Arabic árabe Arabisch", 4),
            new LanguageOption("bn", "বাংলা", "Bengali bengalí Bangla Bengalisch", 5)
        };

        // What the user can choose from: Automatic, the complete languages and, only when PreviewsListed, the previews.
        internal static LanguageOption[] Languages
        {
            get { return PreviewsListed ? Known : Array.FindAll(Known, delegate(LanguageOption o) { return !o.Preview; }); }
        }

        // Any known language by code, listed or not (Apply and direct construction of a window in a hidden language).
        internal static LanguageOption FindLanguage(string code)
        {
            foreach (LanguageOption option in Known)
                if (option.Code == code) return option;
            return null;
        }

        // A language the user can choose now: what a saved setting or a typed code must match to be taken.
        internal static LanguageOption FindListedLanguage(string code)
        {
            foreach (LanguageOption option in Languages)
                if (option.Code == code) return option;
            return null;
        }

        internal static List<LanguageOption> FilterLanguages(string query)
        {
            var result = new List<LanguageOption>();
            string value = (query ?? "").Trim();
            LanguageOption byCode = FindListedLanguage(value.ToLowerInvariant());
            if (byCode != null)
            {
                result.Add(byCode);
                return result;
            }
            string[] terms = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            foreach (LanguageOption option in Languages)
            {
                string haystack = option.Name + " " + option.SearchNames + " " + option.Code;
                bool matches = Array.TrueForAll(terms, term => CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                    haystack, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
                if (matches) result.Add(option);
            }
            return result;
        }
    }
}
