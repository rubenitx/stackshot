// Stackshot - A document file as it is on disk: encoding, BOM and line endings kept, so saving writes back the same bytes.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot
{
    public enum DiskState { Unchanged, Modified, Deleted }

    public sealed class DocFile
    {
        static readonly Regex DeclRx = new Regex(@"^\s*<\?xml[^>]*?encoding\s*=\s*[""']([A-Za-z0-9._\-]+)[""']", RegexOptions.Compiled);

        public string Path;           // null for pasted or new text
        public string Text;           // what the editor shows: line breaks are \n
        public Encoding Enc;
        public bool Bom;
        public string Newline = "\r\n";
        public bool Mixed;
        public bool Xml;
        public bool Lossy;            // some bytes were not valid in Enc and show as U+FFFD: saving an edit would lose them
        public DateTime StampUtc;
        public long Length;
        byte[] hash, original;
        string raw;                   // the decoded text with its own line breaks (no BOM)
        int lead;

        public string EncodingName
        {
            get
            {
                int cp = Enc.CodePage;
                string n = cp == 65001 ? "UTF-8" : cp == 1200 ? "UTF-16 LE" : cp == 1201 ? "UTF-16 BE" : cp == 12000 ? "UTF-32 LE" : cp == 12001 ? "UTF-32 BE"
                         : cp == 1252 ? "Windows-1252" : cp == 28591 ? "ISO-8859-1" : Enc.WebName.ToUpperInvariant();
                return Bom && cp == 65001 ? n + I18n.T(" con BOM", " with BOM") : n;
            }
        }

        public string NewlineName { get { return Newline == "\r\n" ? "CRLF" : Newline == "\r" ? "CR" : "LF"; } }

        // ---- Loading

        // Null with a friendly reason when the file cannot be shown.
        public static DocFile Load(string path, out string error)
        {
            error = null;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists) { error = I18n.Fmt("No encuentro el archivo: {0}", "Cannot find the file: {0}", path); return null; }
                if (!MarkdownTools.Supported(fi.Extension)) { error = I18n.Fmt("Solo admito archivos .md, .xml o .txt: {0}", "Only .md, .xml or .txt files are supported: {0}", fi.Name); return null; }
                bool xml = XmlDoc.IsXmlPath(path);
                if (fi.Length > (xml ? XmlDoc.MaxChars : MarkdownTools.MaxBytes)) { error = I18n.Fmt("El archivo pesa demasiado (m\u00E1ximo {0} MB): {1}", "The file is too big (maximum {0} MB): {1}", xml ? "8" : "2", fi.Name); return null; }
                byte[] b = File.ReadAllBytes(path);
                DocFile d = FromBytes(b, xml);
                // An empty note is a fine thing to open and start writing in; an empty XML has no tree to show.
                bool blank = d.Text == null || d.Text.Trim().Length == 0;
                error = xml ? XmlDoc.CheckText(d.Text) : blank ? null : MarkdownTools.Validate(d.Text);
                if (error != null) return null;
                d.Path = fi.FullName;
                fi.Refresh();
                d.StampUtc = fi.LastWriteTimeUtc;
                d.Length = b.Length;
                return d;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Documento: " + ex.Message);
                error = I18n.Fmt("No he podido leer el archivo: {0}", "Could not read the file: {0}", path);
                return null;
            }
        }

        public static DocFile FromBytes(byte[] b, bool xml)
        {
            DocFile d = new DocFile();
            d.Xml = xml;
            d.original = b;
            d.hash = Sha(b);
            d.Length = b.Length;
            int skip;
            bool lossy;
            Encoding e = Detect(b, out skip, out d.Bom);
            string s = Decode(e, b, skip, out lossy);
            if (lossy && !d.Bom && !IsWide(e) && e.CodePage != 65001)
            {
                // The declaration does not match the bytes: read them as UTF-8, or as Windows-1252 (which maps every byte).
                e = Utf8(b) ? Strict(65001, false) : Strict(1252, false);
                s = Decode(e, b, 0, out lossy);
            }
            d.Enc = e;
            d.Lossy = lossy;
            Init(d, s);
            return d;
        }

        static string Decode(Encoding e, byte[] b, int skip, out bool lossy)
        {
            lossy = false;
            try { return Encoding.GetEncoding(e.CodePage, EncoderFallback.ReplacementFallback, DecoderFallback.ExceptionFallback).GetString(b, skip, b.Length - skip); }
            catch (DecoderFallbackException) { }
            lossy = true;
            return Encoding.GetEncoding(e.CodePage, EncoderFallback.ReplacementFallback, new DecoderReplacementFallback("\uFFFD")).GetString(b, skip, b.Length - skip);
        }

        static bool Utf8(byte[] b)
        {
            try { new UTF8Encoding(false, true).GetCharCount(b); return true; }
            catch (ArgumentException) { return false; }
        }

        // Pasted or dropped text: UTF-8 without BOM unless an XML declaration names another encoding.
        public static DocFile FromText(string t, bool xml)
        {
            DocFile d = new DocFile();
            d.Xml = xml;
            d.Enc = Strict(65001, false);
            if (t.Length > 0 && t[0] == '\uFEFF') t = t.Substring(1);
            if (xml)
            {
                Encoding e = Named(Declared(t), false);
                if (e != null) { d.Enc = e; d.Bom = IsWide(e); }
            }
            Init(d, t);
            return d;
        }

        static void Init(DocFile d, string decoded)
        {
            d.raw = decoded;
            d.Text = MarkdownTools.Normalize(decoded);
            // A second BOM inside the text stays in the file; the editor never sees it.
            d.lead = decoded.Length > 0 && decoded[0] == '\uFEFF' ? 1 : 0;
            int crlf = 0, lf = 0, cr = 0;
            for (int i = 0; i < decoded.Length; i++)
            {
                char c = decoded[i];
                if (c == '\r') { if (i + 1 < decoded.Length && decoded[i + 1] == '\n') { crlf++; i++; } else cr++; }
                else if (c == '\n') lf++;
            }
            int kinds = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
            d.Mixed = kinds > 1;
            if (kinds > 0) d.Newline = crlf >= lf && crlf >= cr ? "\r\n" : lf >= cr ? "\n" : "\r";
        }

        static Encoding Detect(byte[] b, out int skip, out bool bom)
        {
            skip = 0; bom = true;
            if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0 && b[3] == 0) { skip = 4; return Strict(12000, true); }
            if (b.Length >= 4 && b[0] == 0 && b[1] == 0 && b[2] == 0xFE && b[3] == 0xFF) { skip = 4; return Strict(12001, true); }
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { skip = 3; return Strict(65001, true); }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { skip = 2; return Strict(1200, true); }
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { skip = 2; return Strict(1201, true); }
            bom = false;
            // UTF-16 without a BOM: an XML declaration starts with "<?" in two-byte characters.
            if (b.Length >= 4 && b[0] == '<' && b[1] == 0 && b[2] == '?' && b[3] == 0) return Strict(1200, false);
            if (b.Length >= 4 && b[0] == 0 && b[1] == '<' && b[2] == 0 && b[3] == '?') return Strict(1201, false);
            // Any other text in UTF-16 without a BOM (a note saved by an old tool): the high bytes of mostly-Latin text are
            // zero in every other position, which UTF-8 and Windows-1252 never have.
            int odd = 0, even = 0, pairs = Math.Min(b.Length / 2, 512);
            for (int i = 0; i < pairs; i++) { if (b[2 * i] == 0) even++; if (b[2 * i + 1] == 0) odd++; }
            if (pairs >= 2 && odd * 10 >= pairs * 6 && even * 20 <= pairs) return Strict(1200, false);
            if (pairs >= 2 && even * 10 >= pairs * 6 && odd * 20 <= pairs) return Strict(1201, false);
            Encoding declared = DeclaredInBytes(b);
            if (declared != null) return declared;
            return Utf8(b) ? Strict(65001, false) : Strict(1252, false);
        }

        // Without a BOM an XML file says its own encoding in the declaration (ASCII-compatible ones only).
        static Encoding DeclaredInBytes(byte[] b)
        {
            int n = Math.Min(b.Length, 256);
            string head = Encoding.ASCII.GetString(b, 0, n);
            if (!head.StartsWith("<?xml", StringComparison.Ordinal)) return null;
            Encoding e = Named(Declared(head), false);
            return e == null || IsWide(e) || e.CodePage == 65001 ? null : e;
        }

        static string Declared(string t)
        {
            Match m = DeclRx.Match(t.Length > 512 ? t.Substring(0, 512) : t);
            return m.Success ? m.Groups[1].Value : null;
        }

        static Encoding Named(string name, bool bom)
        {
            if (name == null) return null;
            try
            {
                int cp = Encoding.GetEncoding(name).CodePage;
                return Strict(cp, bom);
            }
            catch (ArgumentException) { return null; }
        }

        static bool IsWide(Encoding e) { int cp = e.CodePage; return cp == 1200 || cp == 1201 || cp == 12000 || cp == 12001; }

        static int Family(int cp) { return cp == 1201 ? 1200 : cp == 12001 ? 12000 : cp; }

        // Writing fails instead of replacing characters that do not fit.
        static Encoding Strict(int cp, bool bom)
        {
            switch (cp)
            {
                case 65001: return new UTF8Encoding(bom, true);
                case 1200: return new UnicodeEncoding(false, bom, true);
                case 1201: return new UnicodeEncoding(true, bom, true);
                case 12000: return new UTF32Encoding(false, bom, true);
                case 12001: return new UTF32Encoding(true, bom, true);
                default: return Encoding.GetEncoding(cp, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
            }
        }

        // ---- Saving

        // The bytes for this text: the original bytes when nothing changed; otherwise only the edited stretch is new, so
        // untouched lines keep their own line endings. Null when a character does not fit the encoding.
        public byte[] Encode(string text, out string warning)
        {
            warning = null;
            Encoding enc; bool bom;
            Target(text, out enc, out bom);
            if (text == Text && original != null && enc.CodePage == Enc.CodePage && bom == Bom) return (byte[])original.Clone();
            if (Lossy) { warning = LossyWarning; return null; }
            string body = Denormalize(text);
            try
            {
                byte[] pre = bom ? enc.GetPreamble() : new byte[0];
                if (bom && pre.Length == 0) pre = Strict(enc.CodePage, true).GetPreamble();
                byte[] main = enc.GetBytes(body);
                byte[] all = new byte[pre.Length + main.Length];
                Buffer.BlockCopy(pre, 0, all, 0, pre.Length);
                Buffer.BlockCopy(main, 0, all, pre.Length, main.Length);
                return all;
            }
            catch (EncoderFallbackException)
            {
                warning = I18n.Fmt("Hay caracteres que no caben en {0}.", "Some characters don't fit in {0}.", Display(enc, false));
                return null;
            }
        }

        public string LossyWarning { get { return I18n.Fmt("El archivo tiene bytes que no son {0} v\u00E1lido.", "The file has bytes that are not valid {0}.", EncodingName); } }

        // The message Encode gives when a character does not fit the encoding (in either language).
        public static bool IsCharsetError(string err)
        {
            return err != null && (err.StartsWith("Hay caracteres que no caben", StringComparison.Ordinal) || err.StartsWith("Some characters don't fit in", StringComparison.Ordinal));
        }

        // The user agreed to write the unreadable bytes as U+FFFD.
        public void AcceptLoss() { Lossy = false; }

        static string Display(Encoding e, bool bom)
        {
            DocFile d = new DocFile { Enc = e, Bom = bom };
            return d.EncodingName;
        }

        // The encoding to write with: the loaded one, unless the user changed the encoding in the XML declaration (a
        // declaration that already disagreed with the BOM or the bytes on loading does not count).
        void Target(string text, out Encoding enc, out bool bom)
        {
            enc = Enc; bom = Bom;
            if (!Xml) return;
            string now = Declared(text);
            if (now == null) return;
            if (Text != null && string.Equals(now, Declared(Text), StringComparison.OrdinalIgnoreCase)) return;
            Encoding e = Named(now, false);
            // "UTF-16" names both byte orders: the one on disk stays.
            if (e == null || Family(e.CodePage) == Family(Enc.CodePage)) return;
            enc = Strict(e.CodePage, IsWide(e));
            bom = IsWide(e);
        }

        public string Denormalize(string text)
        {
            if (raw == null || Text == null) return text.Replace("\n", Newline);
            if (text == Text) return raw;
            int max = Math.Min(Text.Length, text.Length), p = 0;
            while (p < max && Text[p] == text[p]) p++;
            int s = 0;
            while (s < max - p && Text[Text.Length - 1 - s] == text[text.Length - 1 - s]) s++;
            int rp = RawIndex(p), rs = RawIndex(Text.Length - s);
            string mid = text.Substring(p, text.Length - p - s).Replace("\n", Newline);
            return raw.Substring(0, rp) + mid + raw.Substring(rs);
        }

        // Where the n-th character of the normalized text starts in the raw text (a \r\n pair is one character).
        int RawIndex(int n)
        {
            int i = lead, k = 0;
            while (k < n && i < raw.Length)
            {
                if (raw[i] == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n') i += 2; else i++;
                k++;
            }
            return i;
        }

        // Writes the text to target (or to its own path). Saving an unchanged document to its own file writes nothing.
        public bool Save(string text, string target, out string error)
        {
            error = null;
            if (target == null) target = Path;
            if (target == null) { error = I18n.T("Elige d\u00F3nde guardarlo.", "Choose where to save it."); return false; }
            bool same = Path != null && string.Equals(Full(target), Full(Path), StringComparison.OrdinalIgnoreCase);
            if (same && text == Text && Check() == DiskState.Unchanged) return true;
            string warning;
            byte[] b = Encode(text, out warning);
            if (b == null) { error = warning; return false; }
            if (!Write(target, b, out error)) return false;
            Encoding enc; bool bom;
            Target(text, out enc, out bom);
            raw = Denormalize(text);
            Enc = enc; Bom = bom;
            Text = text;
            original = b;
            hash = Sha(b);
            Length = b.Length;
            Path = Full(target);
            try { StampUtc = new FileInfo(Path).LastWriteTimeUtc; } catch { }
            return true;
        }

        // Switches to UTF-8 (no BOM) for text that does not fit the original encoding; an XML declaration says so too.
        public string ToUtf8(string text)
        {
            Enc = Strict(65001, false);
            Bom = false;
            // The original bytes are in the old encoding: never hand them back as the UTF-8 version.
            original = null;
            if (Xml && Declared(text) != null)
            {
                Match m = DeclRx.Match(text);
                Group g = m.Groups[1];
                text = text.Substring(0, g.Index) + "UTF-8" + text.Substring(g.Index + g.Length);
            }
            return text;
        }

        static string Full(string p) { try { return System.IO.Path.GetFullPath(p); } catch { return p; } }

        // A temporary file next to the target, then File.Replace (keeps permissions and attributes). Shares where that is
        // not possible (WSL, some network drives) get a direct write.
        public static bool Write(string target, byte[] b, out string error)
        {
            error = null;
            string name = System.IO.Path.GetFileName(target), dir = System.IO.Path.GetDirectoryName(Full(target));
            try
            {
                if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
                {
                    error = I18n.T("Este archivo es de solo lectura. Usa \u00ABGuardar como\u2026\u00BB.", "This file is read-only. Use \u201CSave as\u2026\u201D.");
                    return false;
                }
            }
            catch { }
            string tmp = System.IO.Path.Combine(dir ?? "", "~" + name + ".stackshot-tmp");
            bool tmpWritten = false;
            try
            {
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(b, 0, b.Length);
                    fs.Flush(true);
                }
                tmpWritten = true;
                // An antivirus or a sync client (OneDrive) often holds the file for a moment: try again before falling
                // back to writing over it.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(target)) File.Replace(tmp, target, null, true);
                        else File.Move(tmp, target);
                        return true;
                    }
                    catch (IOException)
                    {
                        if (attempt >= 2 || !File.Exists(tmp) || !File.Exists(target)) throw;
                        System.Threading.Thread.Sleep(150);
                    }
                }
            }
            catch (IOException ex) { ShotStack.Log("Guardar (temporal): " + ex.Message); }
            catch (UnauthorizedAccessException ex) { ShotStack.Log("Guardar (temporal): " + ex.Message); }
            catch (PlatformNotSupportedException ex) { ShotStack.Log("Guardar (temporal): " + ex.Message); }
            // A failed replace can leave the target gone and the new text in the temporary file: put it in place.
            try
            {
                if (File.Exists(tmp) && !File.Exists(target)) { File.Move(tmp, target); return true; }
            }
            catch (Exception ex) { ShotStack.Log("Guardar (recuperar): " + ex.Message); }
            bool opened = false;
            try
            {
                using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    opened = true;
                    fs.Write(b, 0, b.Length);
                    fs.Flush(true);
                }
                // The temporary copy stays until the direct write is complete: a failure halfway still leaves the text.
                if (tmpWritten) { try { File.Delete(tmp); } catch (Exception ex) { ShotStack.Log("Guardar (temporal): " + ex.Message); } }
                return true;
            }
            catch (UnauthorizedAccessException) { error = I18n.Fmt("No tengo permiso para escribir en {0}. Usa \u00ABGuardar como\u2026\u00BB.", "No permission to write to {0}. Use \u201CSave as\u2026\u201D.", name); }
            catch (IOException ex) { error = I18n.Fmt("No he podido guardar {0}: {1}", "Could not save {0}: {1}", name, Short(ex.Message)); }
            catch (Exception ex) { error = I18n.Fmt("No he podido guardar {0}: {1}", "Could not save {0}: {1}", name, Short(ex.Message)); }
            // It couldn't even be opened (held by another program): the original is untouched and the text is still in the
            // window, so the temporary copy would only be clutter next to it.
            if (!opened && tmpWritten && File.Exists(target)) { try { File.Delete(tmp); } catch (Exception ex) { ShotStack.Log("Guardar (temporal): " + ex.Message); } }
            return false;
        }

        static string Short(string m) { m = (m ?? "").Trim(); return m.Length > 160 ? m.Substring(0, 160) + "\u2026" : m; }

        // ---- Changes made outside

        public DiskState Check()
        {
            if (Path == null) return DiskState.Unchanged;
            try
            {
                FileInfo fi = new FileInfo(Path);
                if (!fi.Exists) return DiskState.Deleted;
                // Same time stamp and size: another program can still have rewritten it and restored the time, so a small
                // file is compared by content too; a big one is trusted.
                bool sameStamp = fi.LastWriteTimeUtc == StampUtc && fi.Length == Length;
                if (sameStamp && (hash == null || fi.Length > 1024 * 1024)) return DiskState.Unchanged;
                if (!sameStamp && (fi.Length != Length || fi.Length > 64L * 1024 * 1024)) return DiskState.Modified;
                byte[] b = File.ReadAllBytes(Path);
                if (hash != null && Same(Sha(b), hash)) { StampUtc = fi.LastWriteTimeUtc; return DiskState.Unchanged; }
                return DiskState.Modified;
            }
            catch (IOException) { return DiskState.Unchanged; }
            catch (UnauthorizedAccessException) { return DiskState.Unchanged; }
        }

        // The version now on disk is known (the user chose to keep theirs): it no longer counts as a change.
        public void Acknowledge()
        {
            if (Path == null) return;
            try
            {
                FileInfo fi = new FileInfo(Path);
                if (!fi.Exists) return;
                StampUtc = fi.LastWriteTimeUtc;
                Length = fi.Length;
                hash = fi.Length <= 64L * 1024 * 1024 ? Sha(File.ReadAllBytes(Path)) : null;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static byte[] Sha(byte[] b) { using (SHA256 s = SHA256.Create()) return s.ComputeHash(b); }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
