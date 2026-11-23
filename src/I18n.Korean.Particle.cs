// Stackshot - Korean particles: which of the pair follows a name or a number ("\uD558\uB298\uACFC", "\uB098\uBE44\uC640").
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public static partial class I18n
    {
        // Whether the last word of the text ends in a consonant when read aloud: true (a Hangul syllable with a final
        // consonant, or a number read the Sino-Korean way: 0 1 3 6 7 8), false (no final consonant) or null when it can't
        // be told: a Latin or other script name ("Pixel" is read \uD53D\uC140, "Rex" \uB809\uC2A4: spelling doesn't say) and a digit
        // glued to a letter ("MP3" reads \uC4F0\uB9AC or \uC0BC). Punctuation after the word doesn't count.
        internal static bool? KoreanFinal(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = text.Length - 1;
            while (i >= 0 && !char.IsLetterOrDigit(text[i])) i--;
            if (i < 0) return null;
            char c = text[i];
            if (c >= '\uAC00' && c <= '\uD7A3') return (c - '\uAC00') % 28 != 0;
            if (c < '0' || c > '9') return null;
            int j = i;
            while (j >= 0 && (char.IsDigit(text[j]) || text[j] == '.' || text[j] == ',')) j--;
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

        // The same particle but only when the word says it; null for a word the pair can't be picked for, so the caller
        // can word the sentence without a particle.
        internal static string JosaOrNull(string word, string whenFinal, string whenOpen)
        {
            bool? f = KoreanFinal(word);
            return f == null ? null : f.Value ? whenFinal : whenOpen;
        }

        static readonly Regex JosaPair = new Regex("(\uC740\\(\uB294\\)|\uB294\\(\uC740\\)|\uC774\\(\uAC00\\)|\uAC00\\(\uC774\\)|\uC744\\(\uB97C\\)|\uB97C\\(\uC744\\)|\uACFC\\(\uC640\\)|\uC640\\(\uACFC\\))", RegexOptions.CultureInvariant);

        // A catalog text writes a particle after a variable as the pair ("{0}\uC744(\uB97C)"): once the variable is filled in,
        // the pair becomes the one that fits the word before it, or stays as the pair when that word doesn't say.
        internal static string KoreanParticles(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0) return text;
            return JosaPair.Replace(text, delegate(Match m)
            {
                bool? f = KoreanFinal(text.Substring(0, m.Index));
                if (f == null) return m.Value;
                string v = m.Value;
                bool finalFirst = v[0] == '\uC740' || v[0] == '\uC774' || v[0] == '\uC744' || v[0] == '\uACFC';
                return (f.Value == finalFirst ? v[0] : v[2]).ToString();
            });
        }
    }
}
