// Stackshot - Korean UI strings, keyed by the English source text.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Stackshot
{
    public static partial class I18n
    {
        static readonly Lazy<Dictionary<string, string>> KoreanCatalog =
            new Lazy<Dictionary<string, string>>(BuildKorean);

        static Dictionary<string, string> BuildKorean()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            RegisterKoreanCore(map);
            RegisterKoreanDocuments(map);
            RegisterKoreanCapture(map);
            RegisterKoreanHome(map);
            return map;
        }

        internal static string Localize(string english)
        {
            return IsKorean ? LookupKorean(english) : english;
        }

        static string LookupKorean(string english)
        {
            string korean;
            return english != null && KoreanCatalog.Value.TryGetValue(english, out korean) ? korean : english;
        }

        // Catalog arrays are also used while painting (palettes and mascot parts).
        static readonly ConditionalWeakTable<string[], string[]> KoreanLists = new ConditionalWeakTable<string[], string[]>();

        internal static string[] LocalizeList(string[] english)
        {
            return IsKorean ? KoreanLists.GetValue(english, TranslateList) : english;
        }

        static string[] TranslateList(string[] english)
        {
            return Array.ConvertAll(english, LookupKorean);
        }
    }
}
