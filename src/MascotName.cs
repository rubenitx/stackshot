// Stackshot - The mascot's name, whatever the keyboard, the settings file or a paste brings: no control, invisible or
// bidirectional characters, at most 16 UTF-16 units, composed (NFC), and never cut through a letter that is several pieces
// (an emoji with its joiners, a flag, a Devanagari or Bengali conjunct with its vowel signs).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Stackshot
{
    public static class MascotName
    {
        public const int MaxUnits = 16;

        // What the name may not contain: control characters, the characters that turn the text around (U+202A-U+202E
        // embeddings and overrides, U+2066-U+2069 isolates, the marks U+200E U+200F U+061C) and the ones that show nothing
        // (U+200B zero-width space, U+FEFF), which would make a name read as if it were another one. The joiners that emoji
        // and scripts need (U+200D, U+200C) stay.
        static bool Forbidden(char c)
        {
            return char.IsControl(c) || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069')
                || c == '\u200B' || c == '\u200E' || c == '\u200F' || c == '\u061C' || c == '\uFEFF';
        }

        // The text without what is forbidden, put together (NFC: Hangul typed as separate jamo is one syllable each, and
        // counts like it).
        static string Strip(string text)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in text ?? "") if (!Forbidden(c)) b.Append(c);
            string v = b.ToString();
            try { return v.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return v; }   // a lone surrogate has no normal form: it stays as it is
        }

        // The text without what is forbidden and cut to the limit: still as typed otherwise, spaces included.
        public static string Limit(string text)
        {
            return Cut(Strip(text), MaxUnits);
        }

        // The start of the text of at most max units, ending between two letters.
        static string Cut(string v, int max)
        {
            if (v.Length <= max) return v;
            StringBuilder o = new StringBuilder();
            foreach (string piece in Pieces(v))
            {
                if (o.Length + piece.Length > max) break;
                o.Append(piece);
            }
            return o.ToString();
        }

        // A field the name is typed or pasted in. What is typed is checked as it arrives (PreviewTextInput comes after an
        // IME has committed its composition): a letter that does not fit is refused, so the name in the box is never altered
        // behind the user's back, wherever the caret is. A paste is cleaned and cut by letters to the room left; a drop is
        // refused. The box's own MaxLength would count units and cut a letter in half.
        public static void Guard(TextBox box)
        {
            box.PreviewTextInput += delegate(object o, TextCompositionEventArgs e)
            {
                string typed = Strip(e.Text);
                if (typed.Length == 0 || box.Text.Length - box.SelectionLength + typed.Length > MaxUnits)
                {
                    e.Handled = true;
                    return;
                }
                if (typed == e.Text) return;
                e.Handled = true;
                box.SelectedText = typed;
                box.CaretIndex = box.SelectionStart + box.SelectionLength;
            };
            DataObject.AddPastingHandler(box, delegate(object o, DataObjectPastingEventArgs e)
            {
                e.CancelCommand();
                if (e.IsDragDrop) return;
                string text = e.DataObject.GetDataPresent(DataFormats.UnicodeText) ? e.DataObject.GetData(DataFormats.UnicodeText) as string : null;
                if (string.IsNullOrEmpty(text)) return;
                string fits = Cut(Strip(text), Math.Max(0, MaxUnits - (box.Text.Length - box.SelectionLength)));
                if (fits.Length == 0) return;
                box.SelectedText = fits;
                box.CaretIndex = box.SelectionStart + box.SelectionLength;
            });
        }

        // The name to keep: Limit, with no spaces around it ("" when nothing is left; the caller picks the default).
        public static string Clean(string text)
        {
            return Limit(Strip(text).Trim()).Trim();
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
        // joiner or a virama, starts with a joiner or is a skin tone, a tag character (the letters and the cancel tag that spell a
        // subdivision flag after the black flag) or the second half of a flag, stays with the one before.
        static bool Joins(string before, string next)
        {
            char last = before[before.Length - 1];
            if (last == '\u200D' || next[0] == '\u200D' || IsVirama(last)) return true;
            if (next.Length == 2 && next[0] == '\uD83C' && next[1] >= '\uDFFB' && next[1] <= '\uDFFF') return true;
            if (next.Length == 2 && next[0] == '\uDB40' && next[1] >= '\uDC20' && next[1] <= '\uDC7F') return true;
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
