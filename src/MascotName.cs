// Stackshot - The mascot's name, whatever the keyboard, the settings file or a paste brings: no control or bidirectional
// override characters, at most 16 UTF-16 units, and never cut through a letter that is several pieces (an emoji with its
// joiners, a flag, a Devanagari or Bengali conjunct with its vowel signs).
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Stackshot
{
    public static class MascotName
    {
        public const int MaxUnits = 16;

        // What the name may not contain: control characters and the characters that turn the text around (U+202A-U+202E
        // embeddings and overrides, U+2066-U+2069 isolates), which would make a name read as if it were another one.
        static bool Forbidden(char c)
        {
            return char.IsControl(c) || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069');
        }

        // The typed text without what is forbidden and cut to the limit: still as typed otherwise, spaces included.
        public static string Limit(string text)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in text ?? "") if (!Forbidden(c)) b.Append(c);
            string v = b.ToString();
            if (v.Length <= MaxUnits) return v;
            StringBuilder o = new StringBuilder();
            foreach (string piece in Pieces(v))
            {
                if (o.Length + piece.Length > MaxUnits) break;
                o.Append(piece);
            }
            return o.ToString();
        }

        // A field the name is typed or pasted in: what is forbidden never stays, and the text stops at the limit without
        // cutting a letter in half (the box's own MaxLength counts units and would).
        public static void Guard(System.Windows.Controls.TextBox box)
        {
            box.TextChanged += delegate
            {
                string v = Limit(box.Text);
                if (v == box.Text) return;
                int caret = box.CaretIndex;
                box.Text = v;
                box.CaretIndex = System.Math.Min(caret, v.Length);
            };
        }

        // The name to keep: Limit, with no spaces around it ("" when nothing is left; the caller picks the default).
        public static string Clean(string text)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in text ?? "") if (!Forbidden(c)) b.Append(c);
            return Limit(b.ToString().Trim()).Trim();
        }

        // The text split in what is read as one letter each.
        static List<string> Pieces(string v)
        {
            List<string> pieces = new List<string>();
            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(v);
            while (e.MoveNext())
            {
                string t = e.GetTextElement();
                if (pieces.Count > 0 && Joins(pieces[pieces.Count - 1], t)) pieces[pieces.Count - 1] += t;
                else pieces.Add(t);
            }
            return pieces;
        }

        // The framework's rules for what belongs together predate emoji sequences and conjuncts: a piece that follows a
        // joiner or a virama, starts with a joiner or is a skin tone, or the second half of a flag, stays with the one before.
        static bool Joins(string before, string next)
        {
            char last = before[before.Length - 1];
            if (last == '\u200D' || next[0] == '\u200D' || IsVirama(last)) return true;
            if (next.Length == 2 && next[0] == '\uD83C' && next[1] >= '\uDFFB' && next[1] <= '\uDFFF') return true;
            return before.Length == 2 && IsRegional(before) && next.Length == 2 && IsRegional(next);
        }

        static bool IsRegional(string s) { return s[0] == '\uD83C' && s[1] >= '\uDDE6' && s[1] <= '\uDDFF'; }

        // The vowel killers of the Indic scripts, which tie the next consonant to the previous one.
        static bool IsVirama(char c)
        {
            switch (c)
            {
                case '\u094D': case '\u09CD': case '\u0A4D': case '\u0ACD': case '\u0B4D': case '\u0BCD':
                case '\u0C4D': case '\u0CCD': case '\u0D4D': case '\u0DCA': case '\u1039': return true;
            }
            return false;
        }
    }
}
