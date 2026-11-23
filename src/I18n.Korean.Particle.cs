// Stackshot - Korean particles: which of the pair follows a name or a number ("\uD558\uB298\uACFC", "\uB098\uBE44\uC640").
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public static partial class I18n
    {
        // What may follow the word without changing how it is read: spaces, sentence punctuation and closing quotes or
        // brackets ("\uD558\uB298!", "\u201C\uB098\uBE44\u201D"). A percent sign, an emoji or any other symbol is not read, so it settles nothing.
        const string KoreanIgnorable = ".,!?\u2026:;)]}>\"'~\u201C\u201D\u2018\u2019\u00BB\u203A\u300D\u300F\u3011\uFF09\u3002\u3001";

        // Whether the last word of the text ends in a consonant when read aloud: true (a Hangul syllable with a final
        // consonant, or a number read the Sino-Korean way: 0 1 3 6 7 8), false (no final consonant) or null when it can't
        // be told: a Latin or other script name ("Pixel" is read \uD53D\uC140, "Rex" \uB809\uC2A4: spelling doesn't say), an emoji or a
        // symbol (50%), nothing at all, and a digit glued to a letter ("MP3" reads \uC4F0\uB9AC or \uC0BC).
        internal static bool? KoreanFinal(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = text.Length - 1;
            while (i >= 0 && (char.IsWhiteSpace(text[i]) || KoreanIgnorable.IndexOf(text[i]) >= 0)) i--;
            if (i < 0) return null;
            char c = text[i];
            if (c >= '\uAC00' && c <= '\uD7A3') return (c - '\uAC00') % 28 != 0;
            if (c < '0' || c > '9') return null;
            int j = i;
            while (j >= 0 && ((text[j] >= '0' && text[j] <= '9') || text[j] == '.' || text[j] == ',')) j--;
            if (j >= 0 && ((text[j] >= 'A' && text[j] <= 'Z') || (text[j] >= 'a' && text[j] <= 'z'))) return null;
            return "013678".IndexOf(c) >= 0;
        }

        // The particle after the word: whenFinal after a final consonant ("\uACFC", "\uC740", "\uC774", "\uC744"), whenOpen after a vowel
        // ("\uC640", "\uB294", "\uAC00", "\uB97C"), and both, "\uACFC(\uC640)", when the word's script doesn't say.
        internal static string Josa(string word, string whenFinal, string whenOpen)
        {
            bool? f = KoreanFinal(word);
            return f == null ? whenFinal + "(" + whenOpen + ")" : f.Value ? whenFinal : whenOpen;
        }

        // A variable of the text with the pair that follows it: the variable (with its format), the closing quotes or
        // brackets between them, and the pair. Without a variable before it (a word of the text itself), only the pair.
        static readonly Regex JosaSlot = new Regex(
            "(?<ph>(?<!\\{)\\{(?<n>[0-9]+)(?:,[-+]?[0-9]+)?(?::[^{}]*)?\\})?(?<mid>[\"'\u201D\u2019\u300D\u300F\\)\\]\u3011\uFF09>]*)" +
            "(?<pair>\uC740\\(\uB294\\)|\uB294\\(\uC740\\)|\uC774\\(\uAC00\\)|\uAC00\\(\uC774\\)|\uC744\\(\uB97C\\)|\uB97C\\(\uC744\\)|\uACFC\\(\uC640\\)|\uC640\\(\uACFC\\))",
            RegexOptions.CultureInvariant);

        // A catalog text writes a particle after a variable as the pair ("{0}\uC744(\uB97C)"). Settled here, in the text and
        // before it is formatted, from the value the variable is going to hold; the pair stays as it is when that value
        // doesn't say how it ends. The values are never read back out of the formatted text, so none of them is edited.
        internal static string KoreanTemplate(string template, object[] args)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('(') < 0) return template;
            return JosaSlot.Replace(template, delegate(Match m)
            {
                Group pair = m.Groups["pair"];
                bool? f = null;
                if (m.Groups["ph"].Success)
                {
                    int n;
                    if (args != null && int.TryParse(m.Groups["n"].Value, out n) && n < args.Length && args[n] != null)
                    {
                        try { f = KoreanFinal(string.Format(Culture, m.Groups["ph"].Value, args)); }
                        catch (FormatException) { f = null; }
                    }
                }
                else f = KoreanFinal(template.Substring(0, pair.Index));
                if (f == null) return m.Value;
                string v = pair.Value;
                bool finalFirst = v[0] == '\uC740' || v[0] == '\uC774' || v[0] == '\uC744' || v[0] == '\uACFC';
                return m.Value.Substring(0, pair.Index - m.Index) + (f.Value == finalFirst ? v[0] : v[2]);
            });
        }

        // Formats a Korean text that is not a catalog key of Fmt's (already localized by the caller) the same way.
        internal static string KoreanFormat(string template, params object[] args)
        {
            return string.Format(Culture, KoreanTemplate(template, args), args);
        }
    }
}
