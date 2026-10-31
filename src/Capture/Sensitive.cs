// Stackshot - Personal data in a capture: DNI, NIE, Social Security number, IBAN, cards, e-mail addresses and Spanish
// phone numbers, found in the words Windows' OCR reads and checked with their own control digits. Everything stays on
// the computer.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Stackshot
{
    public enum SensitiveKind { Dni, Nie, Nss, Iban, Card, Email, Phone }

    // One piece of personal data: what it is, the text as read, where it is in its line of text and, found in an image,
    // the box to cover (its words, padded; image pixels).
    public sealed class SensitiveFinding
    {
        public SensitiveKind Kind;
        public string Text;
        public int Start, Length;
        public Rectangle Box;
    }

    public static class Sensitive
    {
        // ---- In an image: the words OCR read, line by line

        // A gap between two words wider than this many line heights is a column of a table: nothing is joined across it.
        const float ColumnGap = 1.6f;

        public static List<SensitiveFinding> Find(IList<Ocr.Word> words, Size image)
        {
            List<SensitiveFinding> found = new List<SensitiveFinding>();
            if (words == null || words.Count == 0) return found;
            Dictionary<int, List<Ocr.Word>> lines = new Dictionary<int, List<Ocr.Word>>();
            List<int> order = new List<int>();
            foreach (Ocr.Word w in words)
            {
                if (w == null || string.IsNullOrEmpty(w.Text)) continue;
                List<Ocr.Word> l;
                if (!lines.TryGetValue(w.Line, out l)) { l = new List<Ocr.Word>(); lines[w.Line] = l; order.Add(w.Line); }
                l.Add(w);
            }
            foreach (int key in order)
            {
                List<Ocr.Word> line = lines[key];
                StringBuilder text = new StringBuilder();
                List<int> owner = new List<int>();
                for (int i = 0; i < line.Count; i++)
                {
                    if (i > 0)
                    {
                        RectangleF a = line[i - 1].Upright, b = line[i].Upright;
                        float h = Math.Max(1f, Math.Max(a.Height, b.Height));
                        text.Append(b.Left - a.Right > h * ColumnGap ? '\n' : ' ');
                        owner.Add(-1);
                    }
                    foreach (char c in line[i].Text) { text.Append(c); owner.Add(i); }
                }
                foreach (SensitiveFinding f in Scan(text.ToString()))
                {
                    RectangleF box = RectangleF.Empty;
                    float h = 0;
                    int n = 0;
                    for (int c = f.Start; c < f.Start + f.Length; c++)
                    {
                        int wi = owner[c];
                        if (wi < 0 || (c > f.Start && owner[c - 1] == wi)) continue;
                        RectangleF wb = line[wi].Box;
                        box = box.IsEmpty ? wb : RectangleF.Union(box, wb);
                        h += line[wi].Upright.Height;
                        n++;
                    }
                    if (box.IsEmpty || n == 0) continue;
                    f.Box = Pad(box, h / n, image);
                    if (!f.Box.IsEmpty) found.Add(f);
                }
            }
            return found;
        }

        // Covered with room to spare: OCR boxes hug the letters, and accents, descenders and anti-aliasing reach a little
        // further.
        static Rectangle Pad(RectangleF b, float lineHeight, Size image)
        {
            float px = Math.Max(3f, lineHeight * 0.18f), py = Math.Max(3f, lineHeight * 0.28f);
            Rectangle r = Rectangle.FromLTRB((int)Math.Floor(b.Left - px), (int)Math.Floor(b.Top - py), (int)Math.Ceiling(b.Right + px), (int)Math.Ceiling(b.Bottom + py));
            if (image.Width > 0 && image.Height > 0) r.Intersect(new Rectangle(Point.Empty, image));
            return r.Width > 0 && r.Height > 0 ? r : Rectangle.Empty;
        }

        // ---- In a line of text

        // Every piece of personal data in one line of text (a '\n' in it is a break nothing crosses), in the order found.
        // E-mail addresses go first (they may hold digits), then the long numbers, then the short ones.
        public static List<SensitiveFinding> Scan(string text)
        {
            List<SensitiveFinding> o = new List<SensitiveFinding>();
            if (string.IsNullOrEmpty(text)) return o;
            bool[] used = new bool[text.Length];
            Emails(text, used, o);
            Ibans(text, used, o);
            Nss(text, used, o);
            Cards(text, used, o);
            Nies(text, used, o);
            Dnis(text, used, o);
            Phones(text, used, o);
            o.Sort(delegate(SensitiveFinding a, SensitiveFinding b) { return a.Start.CompareTo(b.Start); });
            return o;
        }

        // Whether the whole text (trimmed) is exactly one piece of personal data of that kind.
        public static bool Is(string text, SensitiveKind kind)
        {
            string t = (text ?? "").Trim();
            List<SensitiveFinding> f = Scan(t);
            return f.Count == 1 && f[0].Kind == kind && f[0].Start == 0 && f[0].Length == t.Length;
        }

        // The text with its personal data blanked out ("Bandeja de entrada - ••• - Outlook"), for names made from a
        // window's title.
        public static string Scrub(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            List<SensitiveFinding> found;
            try { found = Scan(text); }
            catch (Exception) { return ""; }
            if (found.Count == 0) return text;
            StringBuilder sb = new StringBuilder(text.Length);
            int at = 0;
            foreach (SensitiveFinding f in found)
            {
                if (f.Start < at) continue;
                sb.Append(text, at, f.Start - at).Append("\u2022\u2022\u2022");
                at = f.Start + f.Length;
            }
            return sb.Append(text, at, text.Length - at).ToString();
        }

        static bool Free(bool[] used, int start, int end)
        {
            for (int i = start; i < end; i++) if (used[i]) return false;
            return true;
        }

        static void Add(List<SensitiveFinding> o, bool[] used, SensitiveKind kind, string text, int start, int end, string value)
        {
            for (int i = start; i < end; i++) used[i] = true;
            SensitiveFinding f = new SensitiveFinding();
            f.Kind = kind;
            f.Start = start;
            f.Length = end - start;
            f.Text = value ?? text.Substring(start, end - start);
            o.Add(f);
        }

        // A digit as OCR may give it inside a number: O for 0, I or l for 1, S for 5, B for 8. -1 for anything else.
        static int Digit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            switch (c)
            {
                case 'O': case 'o': return 0;
                case 'I': case 'l': case '|': return 1;
                case 'S': return 5;
                case 'B': return 8;
            }
            return -1;
        }

        static bool Real(char c) { return c >= '0' && c <= '9'; }

        static bool Alnum(char c) { return char.IsLetterOrDigit(c); }

        static bool Latin(char c) { return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'); }

        // Digits as read, with OCR's look-alike letters turned into digits; null when one is not a digit at all. subs: how
        // many were look-alikes.
        static string Digits(string t, int start, int end, out int subs)
        {
            subs = 0;
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                char c = t[i];
                int d = Digit(c);
                if (d < 0) continue; // separators
                if (!Real(c)) subs++;
                sb.Append((char)('0' + d));
            }
            return sb.ToString();
        }

        // Look-alikes are taken for digits only in a number that is mostly real digits.
        static bool FewSubs(int subs, int digits) { return subs <= 2 && subs * 4 <= digits; }

        // A number starts here: at the start of the text, after something that is not a letter or digit, or right after a
        // label OCR joined to it ("DNI12345678Z").
        static bool Starts(string t, int i)
        {
            if (i <= 0 || !Alnum(t[i - 1])) return true;
            int s = i;
            while (s > 0 && char.IsLetter(t[s - 1])) s--;
            if (s > 0 && Alnum(t[s - 1])) return false;
            return Glued.Contains(Fold(t.Substring(s, i - s)));
        }

        // Nothing that belongs to the number goes on after end.
        static bool Ends(string t, int end) { return end >= t.Length || !Alnum(t[end]); }

        // The first group of the run at start belongs to a date or a time just before it ("15/10/2026 600 123 456",
        // "14:30 4539 ..."): the number may begin after it.
        static bool Attached(string t, int start) { return start > 0 && (t[start - 1] == '/' || t[start - 1] == ':' || t[start - 1] == '.'); }

        // Where the run of groups at start goes on once its first skip groups are left out.
        static int After(List<int> sizes, int start, int skip)
        {
            for (int k = 0; k < skip; k++) start += sizes[k] + 1;
            return start;
        }

        // Groups of digits from i, one separator from seps between them (the same one all along). Returns where they end;
        // sizes gets each group's length.
        static int Groups(string t, int i, string seps, List<int> sizes, out char sep)
        {
            sep = '\0';
            int j = i;
            while (true)
            {
                int s = j;
                while (j < t.Length && Digit(t[j]) >= 0) j++;
                if (j == s) break;
                sizes.Add(j - s);
                if (j + 1 < t.Length && seps.IndexOf(t[j]) >= 0 && (sep == '\0' || sep == t[j]) && Digit(t[j + 1]) >= 0 && Real(t[j + 1]))
                {
                    sep = t[j];
                    j++;
                    continue;
                }
                break;
            }
            return j;
        }

        static int Sum(List<int> l)
        {
            int n = 0;
            foreach (int x in l) n += x;
            return n;
        }

        // Lower case, no accents, letters and digits only: "Teléfono:" is "telefono", "N.º" is "n".
        static string Fold(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch) && ch != '\u00BA' && ch != '\u00AA') sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        // Labels a number may be joined to by OCR.
        static readonly HashSet<string> Glued = new HashSet<string> { "dni", "nif", "nie", "nss", "naf", "iban", "tel", "tlf", "telf", "tfno", "movil", "fax" };

        // Words that say the number after them is a phone.
        static readonly HashSet<string> PhoneWords = new HashSet<string> { "tel", "telefono", "telefonos", "telf", "tfno", "tlf", "tlfno", "movil", "moviles", "fax",
                                                                           "whatsapp", "llamar", "llama", "llamanos", "contacto", "phone", "mobile", "cel", "celular", "fijo" };

        // Words that say the number after them is an identifier, an amount or a code, not a phone or a card.
        static readonly HashSet<string> IdWords = new HashSet<string> { "factura", "fra", "n", "no", "num", "numero", "nro", "ref", "referencia", "pedido", "albaran",
                                                                        "expediente", "exp", "cliente", "codigo", "cod", "id", "poliza", "siniestro", "lote", "serie",
                                                                        "orden", "ticket", "recibo", "contrato", "importe", "total", "subtotal", "base", "cp", "postal",
                                                                        "version", "v", "build", "pag", "pagina", "isbn", "sku", "ean", "art", "articulo", "matricula" };

        // Units and currencies that make a group of digits an amount.
        static readonly HashSet<string> UnitWords = new HashSet<string> { "eur", "euro", "euros", "usd", "dolares", "kg", "km", "m", "m2", "uds", "unidades", "ud", "pts", "mb", "gb", "kb" };

        // The words just before i on its line (nearest first), folded, at most max of them.
        static List<string> WordsBefore(string t, int i, int max)
        {
            List<string> o = new List<string>();
            int j = i - 1;
            while (j >= 0 && o.Count < max)
            {
                while (j >= 0 && !char.IsLetterOrDigit(t[j]) && t[j] != '\n') j--;
                if (j < 0 || t[j] == '\n') break;
                int e = j + 1;
                while (j >= 0 && (char.IsLetterOrDigit(t[j]) || t[j] == '.' || t[j] == '\u00BA' || t[j] == '\u00AA')) j--;
                string w = Fold(t.Substring(j + 1, e - j - 1));
                if (w.Length > 0) o.Add(w);
            }
            return o;
        }

        static bool IdBefore(string t, int i)
        {
            List<string> w = WordsBefore(t, i, 1);
            return w.Count > 0 && IdWords.Contains(w[0]);
        }

        static bool PhoneWordBefore(string t, int i)
        {
            foreach (string w in WordsBefore(t, i, 4)) if (PhoneWords.Contains(w)) return true;
            return false;
        }

        // An amount: a currency sign or a unit right after the number (or a currency sign right before it).
        static bool Amount(string t, int start, int end)
        {
            int j = end;
            while (j < t.Length && t[j] == ' ') j++;
            if (j < t.Length && (t[j] == '\u20AC' || t[j] == '$' || t[j] == '\u00A3' || t[j] == '%')) return true;
            int k = j;
            while (k < t.Length && char.IsLetterOrDigit(t[k])) k++;
            if (k > j && UnitWords.Contains(Fold(t.Substring(j, k - j)))) return true;
            int b = start - 1;
            while (b >= 0 && t[b] == ' ') b--;
            return b >= 0 && (t[b] == '\u20AC' || t[b] == '$' || t[b] == '\u00A3');
        }

        // ---- E-mail addresses

        static bool LocalChar(char c) { return (c < 128 && char.IsLetterOrDigit(c)) || c == '.' || c == '_' || c == '%' || c == '+' || c == '-'; }

        static bool DomainChar(char c) { return (c < 128 && char.IsLetterOrDigit(c)) || c == '.' || c == '-'; }

        static void Emails(string t, bool[] used, List<SensitiveFinding> o)
        {
            for (int at = t.IndexOf('@'); at >= 0; at = at + 1 < t.Length ? t.IndexOf('@', at + 1) : -1)
            {
                // OCR sometimes leaves a space on either side of the @.
                int l = at > 0 && t[at - 1] == ' ' ? at - 1 : at;
                int r = at + 1 < t.Length && t[at + 1] == ' ' ? at + 2 : at + 1;
                int ls = l;
                while (ls > 0 && LocalChar(t[ls - 1])) ls--;
                while (ls < l && (t[ls] == '.' || t[ls] == '-')) ls++;
                int re = r;
                while (re < t.Length && DomainChar(t[re])) re++;
                while (re > r && (t[re - 1] == '.' || t[re - 1] == '-')) re--;
                if (ls >= l || re <= r) continue;
                string local = t.Substring(ls, l - ls), domain = t.Substring(r, re - r);
                if (!ValidLocal(local) || !(ValidDomain(domain) || LostDot(local, domain)) || !Free(used, ls, re)) continue;
                Add(o, used, SensitiveKind.Email, t, ls, re, local + "@" + domain);
            }
        }

        static bool ValidLocal(string s)
        {
            if (s.Length == 0 || s.Length > 64 || s[0] == '.' || s[s.Length - 1] == '.' || s.Contains("..")) return false;
            foreach (char c in s) if (!LocalChar(c)) return false;
            return true;
        }

        static bool ValidDomain(string s)
        {
            string[] parts = s.Split('.');
            if (parts.Length < 2 || s.Length > 253) return false;
            foreach (string p in parts)
            {
                if (p.Length == 0 || p.Length > 63 || p[0] == '-' || p[p.Length - 1] == '-') return false;
                foreach (char c in p) if (!DomainChar(c) || c == '.') return false;
            }
            string tld = parts[parts.Length - 1];
            if (tld.Length < 2 || tld.Length > 24) return false;
            foreach (char c in tld) if (!Latin(c)) return false;
            return true;
        }

        // OCR sometimes loses the dot before the domain's ending ("empresaes"): an @ with a real name on both sides is an
        // address all the same.
        static bool LostDot(string local, string domain)
        {
            if (local.Length < 2 || domain.Length < 4 || domain.IndexOf('.') >= 0 || domain[0] == '-' || domain[domain.Length - 1] == '-') return false;
            int letters = 0;
            foreach (char c in domain)
            {
                if (!DomainChar(c)) return false;
                if (Latin(c)) letters++;
            }
            return letters >= 3;
        }

        // ---- IBAN (ISO 13616, checked with ISO 7064 mod 97-10)

        static readonly Dictionary<string, int> IbanLengths = Lengths(
            "AD24 AE23 AL28 AT20 AZ28 BA20 BE16 BG22 BH22 BR29 BY28 CH21 CR22 CY28 CZ24 DE22 DK18 DO28 EE20 EG29 ES24 FI18 FO18 FR27 GB22 GE22 GI23 GL18 GR27 GT28 " +
            "HR21 HU28 IE22 IL23 IQ23 IS26 IT27 JO30 KW30 KZ20 LB28 LC32 LI21 LT20 LU20 LV21 MC27 MD24 ME22 MK19 MR27 MT31 MU30 NL18 NO15 PK24 PL28 PS29 PT25 QA29 " +
            "RO24 RS22 SA24 SC31 SE24 SI19 SK24 SM27 ST25 SV28 TL23 TN24 TR26 UA29 VA22 VG24 XK20");

        static Dictionary<string, int> Lengths(string table)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            foreach (string e in table.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) d[e.Substring(0, 2)] = int.Parse(e.Substring(2), CultureInfo.InvariantCulture);
            return d;
        }

        // A capital letter, or the letter a digit stands for (OCR reads S as 5 and B as 8); '\0' otherwise.
        static char Letter(char c)
        {
            if (c >= 'A' && c <= 'Z') return c;
            switch (c)
            {
                case '5': return 'S';
                case '8': return 'B';
                case '0': return 'O';
                case '1': return 'I';
            }
            return '\0';
        }

        static bool IbanChar(char c) { return (c >= 'A' && c <= 'Z') || Real(c) || c == 'o' || c == 'l'; }

        static void Ibans(string t, bool[] used, List<SensitiveFinding> o)
        {
            for (int i = 0; i + 15 <= t.Length; i++)
            {
                char c0 = Letter(t[i]), c1 = Letter(t[i + 1]);
                if (c0 == '\0' || c1 == '\0' || !Starts(t, i)) continue;
                int len;
                if (!IbanLengths.TryGetValue(new string(new char[] { c0, c1 }), out len)) continue;
                // Groups of letters and digits, one space or dash between them, as many characters as the country's IBAN.
                StringBuilder raw = new StringBuilder();
                int j = i, end = -1;
                while (j < t.Length)
                {
                    char c = t[j];
                    if (IbanChar(c))
                    {
                        raw.Append(c);
                        j++;
                        if (raw.Length == len) { end = j; break; }
                        continue;
                    }
                    if ((c == ' ' || c == '-') && raw.Length >= 2 && j + 1 < t.Length && IbanChar(t[j + 1]) && t[j - 1] != ' ' && t[j - 1] != '-') { j++; continue; }
                    break;
                }
                if (end < 0 || !Ends(t, end) || !Free(used, i, end)) continue;
                string iban = Fix(raw.ToString(), c0, c1, len);
                if (iban == null) continue;
                Add(o, used, SensitiveKind.Iban, t, i, end, iban);
                i = end - 1;
            }
        }

        // The IBAN as written, with the look-alikes OCR gives turned into what makes it valid; null when it is not one.
        static string Fix(string raw, char c0, char c1, int len)
        {
            StringBuilder plain = new StringBuilder(), digits = new StringBuilder();
            int subs = 0, numeric = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i < 2) { plain.Append(i == 0 ? c0 : c1); digits.Append(i == 0 ? c0 : c1); if (c != (i == 0 ? c0 : c1)) subs++; continue; }
                char up = c == 'o' ? 'O' : c == 'l' ? 'I' : c;
                plain.Append(up);
                int d = Digit(c);
                if (i < 4 && d < 0) return null; // the check digits are digits
                if (i < 4 && !Real(c)) { subs++; plain[i] = (char)('0' + d); }
                digits.Append(d >= 0 ? (char)('0' + d) : up);
                if (d >= 0) numeric++;
            }
            string a = plain.ToString();
            if (Valid(a, len) && subs <= 2) return a;
            string b = digits.ToString();
            int more = 0;
            for (int i = 4; i < raw.Length; i++) if (Digit(raw[i]) >= 0 && !Real(raw[i])) more++;
            // Up to 4 ("OOOS" for 0005 in Calibri): the mod 97 check still has the last word.
            if (subs + more <= 4 && (subs + more) * 4 <= numeric + 2 && Valid(b, len)) return b;
            return null;
        }

        static bool Valid(string iban, int len)
        {
            return iban.Length == len && IbanValid(iban);
        }

        // ISO 7064 mod 97-10 on a compact IBAN in capitals ("ES9121000418450200051332").
        public static bool IbanValid(string iban)
        {
            if (iban == null || iban.Length < 15 || iban.Length > 34) return false;
            int len;
            if (!IbanLengths.TryGetValue(iban.Substring(0, 2), out len) || len != iban.Length) return false;
            if (!Real(iban[2]) || !Real(iban[3])) return false;
            string r = iban.Substring(4) + iban.Substring(0, 4);
            int m = 0;
            foreach (char c in r)
            {
                if (Real(c)) m = (m * 10 + (c - '0')) % 97;
                else if (c >= 'A' && c <= 'Z') m = (m * 100 + (c - 'A' + 10)) % 97;
                else return false;
            }
            return m == 1;
        }

        // ---- Spanish Social Security number (NSS / NAF): province, number and two control digits

        static void Nss(string t, bool[] used, List<SensitiveFinding> o)
        {
            for (int i = 0; i + 11 <= t.Length; i++)
            {
                if (Digit(t[i]) < 0 || !Starts(t, i)) continue;
                int run = i;
                while (run < t.Length && Digit(t[run]) >= 0) run++;
                int end, subs;
                string digits;
                long province, number, control;
                if (run - i == 12)
                {
                    // Written together: 281234567840.
                    end = run;
                    digits = Digits(t, i, end, out subs);
                    province = long.Parse(digits.Substring(0, 2), CultureInfo.InvariantCulture);
                    number = long.Parse(digits.Substring(2, 8), CultureInfo.InvariantCulture);
                    control = long.Parse(digits.Substring(10, 2), CultureInfo.InvariantCulture);
                }
                else if (run - i == 2)
                {
                    // 28/12345678/40, 28 12345678 40, 28-12345678-40, 28/1234567/40 (a number below 10 million).
                    int j = run;
                    char s1 = Separator(t, ref j);
                    if (s1 == '\0') continue;
                    int b = j;
                    while (j < t.Length && Digit(t[j]) >= 0) j++;
                    int bl = j - b;
                    if (bl != 7 && bl != 8) continue;
                    if (bl == 7 && CccBefore(t, i)) continue;
                    char s2 = Separator(t, ref j);
                    if (s2 != s1) continue;
                    int c = j;
                    while (j < t.Length && Digit(t[j]) >= 0) j++;
                    if (j - c != 2) continue;
                    end = j;
                    digits = Digits(t, i, end, out subs);
                    province = long.Parse(digits.Substring(0, 2), CultureInfo.InvariantCulture);
                    number = long.Parse(digits.Substring(2, bl), CultureInfo.InvariantCulture);
                    control = long.Parse(digits.Substring(2 + bl, 2), CultureInfo.InvariantCulture);
                }
                else continue;
                if (!Ends(t, end) || !FewSubs(subs, digits.Length) || !Free(used, i, end)) continue;
                if (!Province(province) || NssControl(province, number) != control) continue;
                Add(o, used, SensitiveKind.Nss, t, i, end, digits);
                i = end - 1;
            }
        }

        // One separator of a written number: '/', '-' or '.', with a space either side or not, or a lone space (' ').
        static char Separator(string t, ref int j)
        {
            int k = j;
            bool space = false;
            if (k < t.Length && t[k] == ' ') { k++; space = true; }
            char s = '\0';
            if (k < t.Length && (t[k] == '/' || t[k] == '-' || t[k] == '.')) { s = t[k]; k++; if (k < t.Length && t[k] == ' ') k++; }
            else if (space) s = ' ';
            if (s == '\0' || k >= t.Length || Digit(t[k]) < 0) return '\0';
            j = k;
            return s;
        }

        // A company's contribution account (CCC: province, 7 digits and the same control) is written like an old NSS: a
        // CCC label among the words before ("CCC 0111 08 1234567 89", "Cuenta de cotización de la empresa: ...") says it
        // is the company's, unless an NSS label comes first.
        static bool CccBefore(string t, int i)
        {
            foreach (string w in WordsBefore(t, i, 6))
            {
                if (w == "ccc" || w == "cotizacion") return true;
                if (w == "nss" || w == "naf" || w == "nuss" || w == "afiliacion" || w == "afiliado") return false;
            }
            return false;
        }

        static bool Province(long p) { return (p >= 1 && p <= 53) || p == 66; }

        // The control digits: (province and number) mod 97, where a number below 10 million is joined to the province
        // without its leading zero (the Seguridad Social's rule).
        public static long NssControl(long province, long number)
        {
            long d = number < 10000000 ? province * 10000000 + number : province * 100000000 + number;
            return d % 97;
        }

        // ---- Payment cards (13 to 19 digits, Luhn)

        static void Cards(string t, bool[] used, List<SensitiveFinding> o)
        {
            List<int> sizes = new List<int>();
            int runEnd = 0;
            for (int i = 0; i < t.Length; i++)
            {
                // Each run of groups is looked at once, from its first group: never from the middle of a longer number.
                if (i < runEnd || !Real(t[i]) || !Starts(t, i)) continue;
                sizes.Clear();
                char sep;
                int end = Groups(t, i, " -", sizes, out sep);
                runEnd = end;
                if (!Ends(t, end)) continue;
                for (int skip = 0; skip < sizes.Count && (skip == 0 || Attached(t, i)); skip++)
                {
                    List<int> g = sizes.GetRange(skip, sizes.Count - skip);
                    int start = After(sizes, i, skip);
                    if (!CardShape(g) || !Free(used, start, end) || !Real(t[start])) continue;
                    int subs;
                    string digits = Digits(t, start, end, out subs);
                    if (!FewSubs(subs, digits.Length) || !CardNumber(digits)) continue;
                    // A long reference written in one piece after "Ref." or "Pedido" is not a card.
                    if (g.Count == 1 && IdBefore(t, start)) continue;
                    if (Amount(t, start, end)) continue;
                    Add(o, used, SensitiveKind.Card, t, start, end, digits);
                    break;
                }
            }
        }

        // 4 4 4 4 (the last group may be shorter, up to 19 digits), 4 6 5 (American Express), 4 6 4 (Diners) or all together;
        // also with a space OCR lost (4539 14880343 6467): every break where one of those layouts has one.
        static bool CardShape(List<int> g)
        {
            int n = Sum(g);
            if (n < 13 || n > 19) return false;
            bool fours = true, amex = g.Count <= 3;
            int at = 0;
            for (int i = 0; i < g.Count - 1; i++)
            {
                at += g[i];
                if (at % 4 != 0) fours = false;
                if (at != 4 && at != 10) amex = false;
            }
            return fours || (amex && (n == 14 || n == 15));
        }

        // A number a card network issues (its first digits and length) that passes the Luhn check.
        public static bool CardNumber(string d)
        {
            int n = d.Length;
            if (n < 13 || n > 19 || !Luhn(d)) return false;
            int p2 = int.Parse(d.Substring(0, 2), CultureInfo.InvariantCulture), p3 = int.Parse(d.Substring(0, 3), CultureInfo.InvariantCulture);
            int p4 = int.Parse(d.Substring(0, 4), CultureInfo.InvariantCulture);
            if (d[0] == '4') return n == 13 || n == 16 || n == 19;                                   // Visa
            if (p2 >= 51 && p2 <= 55) return n == 16;                                                // Mastercard
            if (p4 >= 2221 && p4 <= 2720) return n == 16;                                            // Mastercard
            if (p2 == 34 || p2 == 37) return n == 15;                                                // American Express
            if ((p3 >= 300 && p3 <= 305) || p2 == 36 || p2 == 38 || p2 == 39) return n >= 14;        // Diners
            if (p4 >= 3528 && p4 <= 3589) return n >= 16;                                            // JCB
            if (d[0] == '6') return n >= 16;                                                         // Discover, UnionPay, Maestro
            if (p2 == 50 || (p2 >= 56 && p2 <= 58)) return true;                                     // Maestro
            return false;
        }

        public static bool Luhn(string d)
        {
            int sum = 0;
            bool twice = false;
            for (int i = d.Length - 1; i >= 0; i--)
            {
                int v = d[i] - '0';
                if (v < 0 || v > 9) return false;
                if (twice) { v *= 2; if (v > 9) v -= 9; }
                sum += v;
                twice = !twice;
            }
            return sum % 10 == 0;
        }

        // ---- DNI and NIE (the letter is the number mod 23)

        const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

        public static char DniLetter(long number) { return DniLetters[(int)(number % 23)]; }

        // n digits (look-alikes included) from i, not followed by another real digit.
        static bool Fixed(string t, int i, int n)
        {
            if (i + n > t.Length) return false;
            for (int k = i; k < i + n; k++) if (Digit(t[k]) < 0) return false;
            return i + n >= t.Length || !Real(t[i + n]);
        }

        // The control letter after the number at j: right after it, after a dash, or after a space (then in capitals).
        // Returns where the letter is, or -1.
        static int ControlLetter(string t, int j)
        {
            bool spaced = false;
            if (j < t.Length && (t[j] == '-' || t[j] == ' ')) { spaced = t[j] == ' '; j++; }
            if (j >= t.Length || !Latin(t[j])) return -1;
            if (spaced && !char.IsUpper(t[j])) return -1;
            return Ends(t, j + 1) ? j : -1;
        }

        static void Dnis(string t, bool[] used, List<SensitiveFinding> o)
        {
            for (int i = 0; i + 8 <= t.Length; i++)
            {
                if (Digit(t[i]) < 0 || !Starts(t, i)) continue;
                int j;
                if (Fixed(t, i, 8)) j = i + 8;
                else if (i + 10 <= t.Length && Fixed(t, i, 2) && t[i + 2] == '.' && Fixed(t, i + 3, 3) && t[i + 6] == '.' && Fixed(t, i + 7, 3)) j = i + 10; // 12.345.678-Z
                else if (i + 9 <= t.Length && ((Fixed(t, i, 2) && t[i + 2] == '.' && Fixed(t, i + 3, 6)) || (Fixed(t, i, 5) && t[i + 5] == '.' && Fixed(t, i + 6, 3)))) j = i + 9; // one of its dots lost by OCR
                else if (Run(t, i) == 9) j = i + 8; // the letter read as a digit: "201979751" for 20197975L
                else continue;
                int subs;
                string digits = Digits(t, i, j, out subs);
                if (digits.Length != 8 || !FewSubs(subs, 8)) continue;
                long n = long.Parse(digits, CultureInfo.InvariantCulture);
                // A label vouches only for a number of real digits ("NIF B1234567..." is a company's).
                bool labelled = subs == 0 && IdLabelBefore(t, i);
                // A digit for the letter without a label: not where a phone, an amount or a numbered document could be.
                bool loose = labelled || (subs == 0 && digits[0] < '6' && !IdBefore(t, i) && !Amount(t, i, j + 1));
                int end = ControlEnd(t, j, DniLetter(n), labelled, loose);
                if (end < 0 || !Free(used, i, end)) continue;
                Add(o, used, SensitiveKind.Dni, t, i, end, digits + (end > j ? DniLetter(n).ToString() : ""));
                i = end - 1;
            }
        }

        static void Nies(string t, bool[] used, List<SensitiveFinding> o)
        {
            for (int i = 0; i + 8 <= t.Length; i++)
            {
                char x = t[i];
                if ((x != 'X' && x != 'Y' && x != 'Z') || !Starts(t, i)) continue;
                int j = i + 1;
                if (j < t.Length && (t[j] == '-' || t[j] == ' ' || t[j] == '.')) j++;
                if (!Fixed(t, j, 7) && Run(t, j) != 8) continue;
                int num = j;
                j += 7;
                int subs;
                string digits = Digits(t, num, num + 7, out subs);
                if (digits.Length != 7 || !FewSubs(subs, 7)) continue;
                long n = (x - 'X') * 10000000L + long.Parse(digits, CultureInfo.InvariantCulture);
                bool labelled = subs == 0 && IdLabelBefore(t, i);
                int end = ControlEnd(t, j < t.Length && t[j] == '.' ? j + 1 : j, DniLetter(n), labelled, true);
                if (end < 0 || !Free(used, i, end)) continue;
                Add(o, used, SensitiveKind.Nie, t, i, end, x + digits + (end > j ? DniLetter(n).ToString() : ""));
                i = end - 1;
            }
        }

        // How many digits (look-alikes included) follow one another from i.
        static int Run(string t, int i)
        {
            int j = i;
            while (j < t.Length && Digit(t[j]) >= 0) j++;
            return j - i;
        }

        // Windows' OCR reads a control letter after a run of digits as what it looks like: "1" for L, "0" for D, "8" or "3"
        // for B, "6" or "C" for G, "]" for J... For each letter, what may stand for it.
        static readonly Dictionary<char, string> Misread = new Dictionary<char, string>
        {
            { 'L', "1lI|!" }, { 'D', "0O" }, { 'Q', "0O" }, { 'J', "])}0" }, { 'B', "83" }, { 'G', "6C" }, { 'C', "G(" },
            { 'E', "F" }, { 'F', "E" }, { 'P', "R" }, { 'R', "P" }, { 'V', "Y" }, { 'Y', "V" }
        };

        static bool StandsFor(char c, char letter)
        {
            string s;
            return Misread.TryGetValue(letter, out s) && s.IndexOf(c) >= 0;
        }

        // Words that say the number after them is a DNI or an NIE.
        static readonly HashSet<string> IdLabels = new HashSet<string> { "dni", "nif", "nie", "tie", "identidad" };

        // A DNI or NIE label among the few words before i, with no other value, ID word or phone word in between ("DNI del
        // trabajador: ...", "Nº DNI ...", "DNI nº ...").
        static bool IdLabelBefore(string t, int i)
        {
            foreach (string w in WordsBefore(t, i, 4))
            {
                if (IdLabels.Contains(w)) return true;
                if (w == "n" || w == "no" || w == "num" || w == "numero" || w == "nro") continue;
                if (w.Length == 1) return false; // "NIF B-12345674": a company's letter
                foreach (char c in w) if (Real(c)) return false;
                if (IdWords.Contains(w) || PhoneWords.Contains(w)) return false;
            }
            return false;
        }

        // A sign OCR may give for a control letter (not ordinary punctuation).
        static bool ControlSign(char c) { return Real(c) || "])}|!<>[({'\"".IndexOf(c) >= 0; }

        // Where a DNI or NIE whose number ends at j ends, its control letter (want) taken as OCR may have read it: the
        // letter, a look-alike of it, or, with a label before the number, whatever stands in its place or nothing at all
        // (some forms leave the letter out). loose: a look-alike digit may stand for the letter. -1 when it does not fit.
        static int ControlEnd(string t, int j, char want, bool labelled, bool loose)
        {
            int at = ControlLetter(t, j);
            if (at >= 0)
            {
                char c = char.ToUpperInvariant(t[at]);
                return c == want || StandsFor(c, want) || labelled ? at + 1 : -1;
            }
            int k = j < t.Length && t[j] == '-' ? j + 1 : j;
            if (k < t.Length && ControlSign(t[k]))
            {
                // Two signs for one letter ("1<" for K), only after a label.
                if (labelled && k + 1 < t.Length && ControlSign(t[k + 1]) && !Real(t[k + 1]) && Ends(t, k + 2)) return k + 2;
                if (Ends(t, k + 1) && (labelled || (StandsFor(t[k], want) && (loose || !Real(t[k]))))) return k + 1;
            }
            return labelled && Ends(t, j) ? j : -1;
        }

        // ---- Spanish phone numbers: nine digits from 6 to 9, optionally after +34 or 0034

        static void Phones(string t, bool[] used, List<SensitiveFinding> o)
        {
            List<int> sizes = new List<int>();
            int runEnd = 0;
            for (int i = 0; i < t.Length; i++)
            {
                // Each run of groups is looked at once, from its first group: never from the middle of a longer number.
                if (i < runEnd || !Starts(t, i)) continue;
                int j = i;
                bool prefixed = Prefix(t, ref j);
                if (!prefixed && Digit(t[i]) < 0) continue;
                if (j >= t.Length || !Real(t[j])) continue;
                sizes.Clear();
                char sep;
                int end = Groups(t, j, " -.", sizes, out sep);
                runEnd = end;
                if (!Ends(t, end)) continue;
                for (int skip = 0; skip < sizes.Count && (skip == 0 || (!prefixed && Attached(t, j))); skip++)
                {
                    List<int> g = sizes.GetRange(skip, sizes.Count - skip);
                    int start = prefixed ? i : After(sizes, j, skip), first = After(sizes, j, skip);
                    if (Sum(g) != 9 || !PhoneShape(g) || !Real(t[first]) || !Free(used, start, end)) continue;
                    int subs;
                    string digits = Digits(t, first, end, out subs);
                    if (digits.Length != 9 || digits[0] < '6' || !FewSubs(subs, 9)) continue;
                    if (!prefixed)
                    {
                        bool labelled = PhoneWordBefore(t, start);
                        if (IdBefore(t, start) || Amount(t, start, end)) continue;
                        if (sep == '.' && !labelled) continue;                              // 600.123.456 reads as an amount
                        if (g.Count == 1 && digits[0] >= '8' && !labelled) continue;        // a bare 9xxxxxxxx may be any code
                    }
                    Add(o, used, SensitiveKind.Phone, t, start, end, digits);
                    break;
                }
            }
        }

        // +34, + 34, 0034, (+34) or (34) and the space or dash after it. Moves j past it.
        static bool Prefix(string t, ref int j)
        {
            int k = j;
            bool paren = k < t.Length && t[k] == '(';
            if (paren) k++;
            if (k < t.Length && t[k] == '+')
            {
                k++;
                if (k < t.Length && t[k] == ' ') k++;
            }
            else if (k + 2 <= t.Length && t[k] == '0' && t[k + 1] == '0') k += 2;
            else if (!paren) return false;
            if (k + 2 > t.Length || t[k] != '3' || t[k + 1] != '4') return false;
            k += 2;
            if (paren) { if (k >= t.Length || t[k] != ')') return false; k++; }
            if (k < t.Length && (t[k] == ' ' || t[k] == '-' || t[k] == '.')) k++;
            if (k >= t.Length || !Real(t[k])) return false;
            j = k;
            return true;
        }

        // Where the groups of 600 123 456, 600 12 34 56 and 91 234 56 78 break.
        static readonly int[][] PhoneBreaks = { new int[] { 3, 6 }, new int[] { 3, 5, 7 }, new int[] { 2, 5, 7 } };

        // 600123456, 600 123 456, 600 12 34 56, 91 234 56 78, or one of those with spaces OCR lost (600 123456, 91 2345678,
        // 91234 56 78): the groups break only where a usual layout does.
        static bool PhoneShape(List<int> g)
        {
            if (g.Count == 1) return g[0] == 9;
            List<int> breaks = new List<int>();
            int at = 0;
            for (int i = 0; i < g.Count - 1; i++) { at += g[i]; breaks.Add(at); }
            foreach (int[] layout in PhoneBreaks)
                if (breaks.TrueForAll(delegate(int b) { return Array.IndexOf(layout, b) >= 0; })) return true;
            return false;
        }

        // ---- Words for the user

        static readonly string[] OneEs = { "DNI", "NIE", "n.\u00BA de la Seguridad Social", "IBAN", "tarjeta", "correo", "tel\u00E9fono" };
        static readonly string[] OneEn = { "ID (DNI)", "Foreigner ID (NIE)", "Social Security no.", "IBAN", "card", "email", "phone" };
        static string[] One { get { return I18n.IsSpanish ? OneEs : OneEn; } }

        static readonly string[] ManyEs = { "DNI", "NIE", "n.\u00BA de la Seguridad Social", "IBAN", "tarjetas", "correos", "tel\u00E9fonos" };
        static readonly string[] ManyEn = { "IDs (DNI)", "Foreigner IDs (NIE)", "Social Security nos.", "IBANs", "cards", "emails", "phones" };
        static string[] Many { get { return I18n.IsSpanish ? ManyEs : ManyEn; } }

        public static string Name(SensitiveKind k, int n) { return n == 1 ? One[(int)k] : Many[(int)k]; }

        // "1 DNI, 1 IBAN, 2 correos", in a fixed order.
        public static string Summary(IList<SensitiveFinding> found)
        {
            int[] count = new int[One.Length];
            foreach (SensitiveFinding f in found) count[(int)f.Kind]++;
            List<string> parts = new List<string>();
            for (int i = 0; i < count.Length; i++) if (count[i] > 0) parts.Add(count[i] + " " + Name((SensitiveKind)i, count[i]));
            return string.Join(", ", parts.ToArray());
        }
    }
}
