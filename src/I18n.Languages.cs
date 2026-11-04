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

        internal static readonly LanguageOption[] Languages =
        {
            new LanguageOption("auto", "", "auto windows automático automatisch"),
            new LanguageOption("es", "Español", "Spanish castellano Spanisch"),
            new LanguageOption("en", "English", "inglés Englisch"),
            new LanguageOption("de", "Deutsch", "German alemán"),
            new LanguageOption("fr", "Français", "French francés Französisch", 0),
            new LanguageOption("pt", "Português", "Portuguese portugués Portugiesisch Brasil Brazil Portugal", 1),
            new LanguageOption("zh", "简体中文", "Chinese chino Chinesisch Mandarin mandarín pinyin zhongwen", 2),
            new LanguageOption("hi", "हिन्दी", "Hindi हिन्दी हिंदी", 3),
            new LanguageOption("ar", "العربية", "Arabic árabe Arabisch", 4),
            new LanguageOption("bn", "বাংলা", "Bengali bengalí Bangla Bengalisch", 5)
        };

        internal static LanguageOption FindLanguage(string code)
        {
            foreach (LanguageOption option in Languages)
                if (option.Code == code) return option;
            return null;
        }

        internal static List<LanguageOption> FilterLanguages(string query)
        {
            var result = new List<LanguageOption>();
            string value = (query ?? "").Trim();
            LanguageOption byCode = FindLanguage(value.ToLowerInvariant());
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
