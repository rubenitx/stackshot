// Stackshot - Files that travel as base64 inside an XML field (a PDF, a scanned image, another XML): recognised by their
// text, decoded off the UI thread, typed by their first bytes and written to a private temporary folder. Executables,
// scripts and shortcuts are never opened, only saved where the user says.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Stackshot
{
    public enum AttachKind { Unknown, Pdf, Png, Jpeg, Gif, Bmp, Tiff, Zip, Word, Excel, PowerPoint, OfficeMacros, Xml, Text, Executable, Shortcut }

    public sealed class AttachType
    {
        public AttachKind Kind;
        public string Ext = ".bin", Label;
        public bool Partial;    // typed from the first bytes only: a ZIP may still turn out to be a Word or Excel file

        public AttachType() { Label = I18n.T("tipo desconocido", "unknown type"); }

        public bool Image { get { return Kind == AttachKind.Png || Kind == AttachKind.Jpeg || Kind == AttachKind.Gif || Kind == AttachKind.Bmp || Kind == AttachKind.Tiff; } }
        // Opened with the app Windows has for them, after asking.
        public bool Document { get { return Kind == AttachKind.Pdf || Kind == AttachKind.Word || Kind == AttachKind.Excel || Kind == AttachKind.PowerPoint; } }
        // Opened in the viewer itself.
        public bool Readable { get { return Kind == AttachKind.Xml || Kind == AttachKind.Text; } }
        public bool Opens { get { return Image || Document || Readable || (Partial && Kind == AttachKind.Zip); } }
    }

    public static class XmlAttachment
    {
        public const int MinChars = 64;
        // Decoded files stop here (tests lower it).
        internal static int MaxBytes = 50 * 1024 * 1024;
        // Up to this many characters the menu decodes the whole field to name its type; beyond, only the start.
        const int PeekWhole = 256 * 1024, PeekChars = 4096;

        static readonly string[] BlockedExts = { ".exe", ".dll", ".com", ".scr", ".pif", ".cpl", ".sys", ".drv", ".ocx", ".msi", ".msp", ".mst", ".bat", ".cmd",
                                                 ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".vb", ".js", ".jse", ".wsf", ".wsh", ".ws", ".wsc", ".sct", ".hta", ".lnk",
                                                 ".url", ".scf", ".reg", ".inf", ".jar", ".application", ".appref-ms", ".gadget", ".msc", ".chm", ".iso", ".img",
                                                 ".vhd", ".vhdx", ".appx", ".msix", ".appxbundle", ".msixbundle", ".cab", ".xll", ".docm", ".dotm", ".xlsm", ".xltm",
                                                 ".xlam", ".pptm", ".potm", ".ppam", ".settingcontent-ms", ".library-ms", ".search-ms" };
        static readonly string[] HintNames = { "filename", "nombrefichero", "nombrearchivo", "nombredocumento", "nombreadjunto", "documentname", "attachmentname", "fichero", "archivo", "file", "nombre", "name" };

        // Where opened attachments are written: inside Stackshot's temporary folder, cleaned like the captures there.
        public static string Folder { get { return Path.Combine(Settings.TempDir, "adjuntos"); } }

        // The text a node carries: an element with one piece of text (or CDATA), or that text itself. Null otherwise.
        public static string ValueOf(XItem n)
        {
            if (n == null) return null;
            if (n.Kind == XKind.Text || n.Kind == XKind.CData) return n.Value;
            if (n.Kind == XKind.Element && !n.Inner && n.Kids.Count == 1 && (n.Kids[0].Kind == XKind.Text || n.Kids[0].Kind == XKind.CData)) return n.Kids[0].Value;
            return null;
        }

        static bool Ws(char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n'; }

        static int Sextet(char c)
        {
            if (c >= 'A' && c <= 'Z') return c - 'A';
            if (c >= 'a' && c <= 'z') return c - 'a' + 26;
            if (c >= '0' && c <= '9') return c - '0' + 52;
            if (c == '+') return 62;
            if (c == '/') return 63;
            return -1;
        }

        // Base64 as a field carries a file: only the base64 alphabet and whitespace, padding only at the end, a whole number
        // of 4-character groups, at least MinChars of them. Hex digests and long words are not files: some character
        // outside 0-9/A-F/a-f must appear, and in a field shorter than 256 characters some digit, '+' or '/' too.
        // A field of more than this many characters is judged by its start and its end, so a right click stays instant;
        // decoding it (off the UI thread) checks every character anyway.
        const int SampleOver = 1024 * 1024, Sample = 64 * 1024;

        public static bool IsBase64(string s)
        {
            if (s == null || s.Length < MinChars) return false;
            if (s.Length > SampleOver)
            {
                int pad = 0;
                bool nonHex = false, digit = false;
                return Scan(s, 0, Sample, false, ref pad, ref nonHex, ref digit) >= 0 && Scan(s, s.Length - Sample, s.Length, true, ref pad, ref nonHex, ref digit) >= 0 && nonHex;
            }
            int p = 0;
            bool hex = false, dig = false;
            int n = Scan(s, 0, s.Length, true, ref p, ref hex, ref dig);
            return n >= MinChars && n % 4 == 0 && hex && (dig || n >= 256);
        }

        // The base64 characters in s[from, to), or -1 at the first character that cannot be there (padding only at the end
        // when end says the stretch is the field's end).
        static int Scan(string s, int from, int to, bool end, ref int pad, ref bool nonHex, ref bool digit)
        {
            int n = 0;
            for (int i = from; i < to; i++)
            {
                char c = s[i];
                if (Ws(c)) continue;
                if (c == '=') { if (!end || ++pad > 2) return -1; n++; continue; }
                if (pad > 0) return -1;
                int v = Sextet(c);
                if (v < 0) return -1;
                if (!nonHex && !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) nonHex = true;
                if (!digit && v >= 52) digit = true;
                n++;
            }
            return n;
        }

        // The bytes of a base64 field, or null with the reason (too big for MaxBytes, or not valid base64).
        public static byte[] Decode(string s, out string error)
        {
            error = null;
            if (s == null) { error = I18n.T("No hay ning\u00FAn adjunto.", "There is no attachment."); return null; }
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (!Ws(s[i])) n++;
            if ((long)n / 4 * 3 > MaxBytes) { error = I18n.Fmt("El adjunto es demasiado grande (m\u00E1ximo {0} MB).", "The attachment is too large (maximum {0} MB).", (MaxBytes / (1024 * 1024))); return null; }
            char[] buf = new char[n];
            int k = 0;
            for (int i = 0; i < s.Length; i++) if (!Ws(s[i])) buf[k++] = s[i];
            try { return Convert.FromBase64CharArray(buf, 0, n); }
            catch (FormatException) { error = I18n.T("El campo no es base64 v\u00E1lido.", "Field is not valid base64."); return null; }
        }

        // The type, for the menu: cheap enough for a right click (the whole field when small, else its first bytes).
        public static AttachType Peek(string s)
        {
            if (s == null) return new AttachType();
            if (s.Length <= PeekWhole)
            {
                string err;
                byte[] all = Decode(s, out err);
                return all != null ? Detect(all, false) : new AttachType();
            }
            StringBuilder head = new StringBuilder(PeekChars);
            for (int i = 0; i < s.Length && head.Length < PeekChars; i++) if (!Ws(s[i])) head.Append(s[i]);
            int keep = head.Length - head.Length % 4;
            try { return Detect(Convert.FromBase64String(head.ToString(0, keep)), true); }
            catch (FormatException) { return new AttachType(); }
        }

        static bool Starts(byte[] b, params int[] sig)
        {
            if (b.Length < sig.Length) return false;
            for (int i = 0; i < sig.Length; i++) if (sig[i] >= 0 && b[i] != sig[i]) return false;
            return true;
        }

        static AttachType Type(AttachKind k, string ext, string label) { return new AttachType { Kind = k, Ext = ext, Label = label }; }

        // What the bytes are, by their magic numbers (partial: only the start of the file is here).
        public static AttachType Detect(byte[] b, bool partial)
        {
            if (b == null || b.Length == 0) return new AttachType();
            if (Starts(b, 0x25, 0x50, 0x44, 0x46, 0x2D)) return Type(AttachKind.Pdf, ".pdf", "PDF");
            if (Starts(b, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return Type(AttachKind.Png, ".png", I18n.T("imagen PNG", "PNG image"));
            if (Starts(b, 0xFF, 0xD8, 0xFF)) return Type(AttachKind.Jpeg, ".jpg", I18n.T("imagen JPEG", "JPEG image"));
            if (Starts(b, 0x47, 0x49, 0x46, 0x38, -1, 0x61) && (b[4] == 0x37 || b[4] == 0x39)) return Type(AttachKind.Gif, ".gif", I18n.T("imagen GIF", "GIF image"));
            if (Bmp(b)) return Type(AttachKind.Bmp, ".bmp", I18n.T("imagen BMP", "BMP image"));
            if (Starts(b, 0x49, 0x49, 0x2A, 0x00) || Starts(b, 0x4D, 0x4D, 0x00, 0x2A)) return Type(AttachKind.Tiff, ".tif", I18n.T("imagen TIFF", "TIFF image"));
            if (Starts(b, 0x4D, 0x5A)) return Type(AttachKind.Executable, ".exe", I18n.T("un ejecutable", "an executable"));
            if (Starts(b, 0x4C, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00)) return Type(AttachKind.Shortcut, ".lnk", I18n.T("un acceso directo", "a shortcut"));
            if (Starts(b, 0x50, 0x4B, 0x03, 0x04) || Starts(b, 0x50, 0x4B, 0x05, 0x06)) return partial ? new AttachType { Kind = AttachKind.Zip, Ext = ".zip", Label = I18n.T("ZIP u Office", "ZIP or Office"), Partial = true } : Zip(b);
            bool xml;
            if (Text(b, out xml)) return xml ? Type(AttachKind.Xml, ".xml", "XML") : Type(AttachKind.Text, ".txt", I18n.T("texto", "text"));
            return new AttachType();
        }

        // "BM", the reserved words at zero and a known DIB header size: plain text starting with "BM" is not a bitmap.
        static bool Bmp(byte[] b)
        {
            if (b.Length < 26 || b[0] != 0x42 || b[1] != 0x4D) return false;
            if (b[6] != 0 || b[7] != 0 || b[8] != 0 || b[9] != 0) return false;
            int dib = b[14] | (b[15] << 8) | (b[16] << 16) | (b[17] << 24);
            return dib == 12 || dib == 40 || dib == 52 || dib == 56 || dib == 64 || dib == 108 || dib == 124;
        }

        // Word, Excel and PowerPoint files are ZIP packages; one with macros (vbaProject.bin) is only saved.
        static AttachType Zip(byte[] b)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(b, false))
                using (ZipArchive z = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    bool types = false, word = false, excel = false, ppt = false, macros = false;
                    foreach (ZipArchiveEntry e in z.Entries)
                    {
                        string n = e.FullName.Replace('\\', '/');
                        if (n == "[Content_Types].xml") types = true;
                        else if (n.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)) word = true;
                        else if (n.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase)) excel = true;
                        else if (n.Equals("ppt/presentation.xml", StringComparison.OrdinalIgnoreCase)) ppt = true;
                        if (n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)) macros = true;
                    }
                    if (types && (word || excel || ppt))
                    {
                        if (macros) return Type(AttachKind.OfficeMacros, word ? ".docm" : excel ? ".xlsm" : ".pptm", I18n.T("un documento de Office con macros", "an Office document with macros"));
                        if (word) return Type(AttachKind.Word, ".docx", I18n.T("documento de Word", "Word document"));
                        if (excel) return Type(AttachKind.Excel, ".xlsx", I18n.T("hoja de Excel", "Excel spreadsheet"));
                        return Type(AttachKind.PowerPoint, ".pptx", I18n.T("presentaci\u00F3n de PowerPoint", "PowerPoint presentation"));
                    }
                }
            }
            catch (Exception) { }
            return Type(AttachKind.Zip, ".zip", I18n.T("un archivo ZIP", "a ZIP file"));
        }

        // Text: a Unicode BOM, or no NUL and no control characters other than tab, line breaks and form feed (UTF-8 or a
        // single-byte code page). XML when the first thing in it is a tag.
        static bool Text(byte[] b, out bool xml)
        {
            xml = false;
            int n = Math.Min(b.Length, 64 * 1024);
            string head;
            if (Starts(b, 0xEF, 0xBB, 0xBF)) head = Encoding.UTF8.GetString(b, 3, Math.Min(b.Length - 3, 2048));
            else if (Starts(b, 0xFF, 0xFE)) head = Encoding.Unicode.GetString(b, 2, Math.Min(b.Length - 2, 4096) & ~1);
            else if (Starts(b, 0xFE, 0xFF)) head = Encoding.BigEndianUnicode.GetString(b, 2, Math.Min(b.Length - 2, 4096) & ~1);
            else
            {
                for (int i = 0; i < n; i++)
                {
                    byte c = b[i];
                    if (c < 0x20 && c != 0x09 && c != 0x0A && c != 0x0D && c != 0x0C) return false;
                    if (c == 0x7F) return false;
                }
                head = Encoding.UTF8.GetString(b, 0, Math.Min(b.Length, 2048));
            }
            head = head.TrimStart(' ', '\t', '\r', '\n', '\uFEFF');
            xml = head.Length > 1 && head[0] == '<' && (head[1] == '?' || head[1] == '!' || head[1] == '_' || char.IsLetter(head[1]));
            return true;
        }

        // The file name the message gives the attachment, if any: an attribute of its element (filename, nombre...) or a
        // sibling element named like one. Only the name part, cleaned of characters a file name cannot have.
        public static string HintName(XItem n)
        {
            XItem e = n == null ? null : n.Kind == XKind.Element ? n : n.Parent;
            if (e == null) return null;
            string found = null;
            if (e.Attrs != null)
                foreach (string want in HintNames)
                {
                    foreach (KeyValuePair<string, string> a in e.Attrs)
                    {
                        int colon = a.Key.IndexOf(':');
                        string local = (colon >= 0 ? a.Key.Substring(colon + 1) : a.Key).ToLowerInvariant();
                        if (local == want && LooksLikeFile(a.Value)) { found = a.Value; break; }
                    }
                    if (found != null) break;
                }
            if (found == null && e.Sibs != null)
                foreach (string want in HintNames)
                {
                    foreach (XItem s in e.Sibs)
                    {
                        if (s == e || s.Kind != XKind.Element) continue;
                        string local = s.Local.ToLowerInvariant();
                        if (local != want && !(want.Length > 6 && local.Contains(want))) continue;
                        string v = XmlDoc.TextOf(s);
                        if (LooksLikeFile(v)) { found = v; break; }
                    }
                    if (found != null) break;
                }
            return found == null ? null : CleanName(found);
        }

        static bool LooksLikeFile(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim();
            if (v.Length < 3 || v.Length > 200) return false;
            int dot = v.LastIndexOf('.');
            if (dot <= 0 || dot >= v.Length - 1 || v.Length - dot > 16) return false;
            for (int i = dot + 1; i < v.Length; i++) if (!char.IsLetterOrDigit(v[i]) && v[i] != '-') return false;
            return true;
        }

        // Only the last part of a path, without characters Windows forbids, never empty.
        public static string CleanName(string v)
        {
            if (v == null) return null;
            v = v.Trim();
            int cut = v.LastIndexOfAny(new char[] { '/', '\\', ':' });
            if (cut >= 0) v = v.Substring(cut + 1);
            StringBuilder sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in v)
            {
                if (c < 0x20 || Array.IndexOf(bad, c) >= 0) continue;
                sb.Append(c);
                if (sb.Length >= 120) break;
            }
            string r = sb.ToString().Trim().Trim('.').Trim();
            if (r.Length == 0) return null;
            // CON, NUL, COM1... name devices, not files, whatever their extension (also with a superscript 1 to 3).
            int dot = r.IndexOf('.');
            string stem = (dot < 0 ? r : r.Substring(0, dot)).TrimEnd().ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 ((stem[3] >= '0' && stem[3] <= '9') || stem[3] == '\u00B9' || stem[3] == '\u00B2' || stem[3] == '\u00B3')))
                r = "_" + r;
            return r;
        }

        // An executable or a shortcut by its bytes, or a name that would run something (a script, an installer...).
        public static bool Blocked(AttachType t, string hint)
        {
            if (t != null && (t.Kind == AttachKind.Executable || t.Kind == AttachKind.Shortcut)) return true;
            return BlockedName(hint);
        }

        public static bool BlockedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string ext;
            try { ext = Path.GetExtension(name.Trim().TrimEnd('.', ' ')).ToLowerInvariant(); }
            catch (ArgumentException) { return true; }
            return Array.IndexOf(BlockedExts, ext) >= 0;
        }

        // The name it is saved or opened with: the message's own name when it has one (its extension replaced by the
        // detected one when they disagree), else "adjunto".
        public static string FileName(AttachType t, string hint)
        {
            string ext = t != null ? t.Ext : ".bin";
            string stem = hint != null ? Path.GetFileNameWithoutExtension(hint) : null;
            if (string.IsNullOrEmpty(stem)) stem = "adjunto";
            string own = hint != null ? Path.GetExtension(hint) : "";
            // A known type keeps the detected extension; an unknown one keeps the message's (it is only ever saved).
            if ((t == null || t.Kind == AttachKind.Unknown) && own.Length > 1) ext = own;
            if (t != null && t.Kind == AttachKind.Jpeg && (own.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || own.Equals(".jpg", StringComparison.OrdinalIgnoreCase))) ext = own;
            if (t != null && t.Kind == AttachKind.Tiff && own.Equals(".tiff", StringComparison.OrdinalIgnoreCase)) ext = own;
            // CleanName keeps 120 characters: a long name loses the end of its stem, never its extension.
            if (stem.Length + ext.Length > 120) stem = stem.Substring(0, Math.Max(1, 120 - ext.Length));
            return CleanName(stem + ext) ?? "adjunto" + ext;
        }

        // Writes the bytes to the private folder under a name of its own; null with the reason when it cannot.
        public static string WriteTemp(byte[] data, string fileName, out string error)
        {
            return WriteTemp(data, fileName, false, out error);
        }

        // readOnly: for what another program opens, so an edit there is saved elsewhere ("Guardar como") and not in a
        // folder that gets cleaned; it is also marked as come from the Internet, as a mail program marks an attachment.
        public static string WriteTemp(byte[] data, string fileName, bool readOnly, out string error)
        {
            error = null;
            try
            {
                string dir = Folder;
                Directory.CreateDirectory(dir);
                string p = ShotStack.Unique(Path.Combine(dir, CleanName(fileName) ?? "adjunto.bin"));
                File.WriteAllBytes(p, data);
                if (readOnly)
                {
                    MarkFromInternet(p);
                    File.SetAttributes(p, File.GetAttributes(p) | FileAttributes.ReadOnly);
                }
                return p;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Adjunto temporal: " + ex.Message);
                error = I18n.T("No he podido escribir el adjunto en la carpeta temporal.", "Could not write attachment to temporary folder.");
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        // The Zone.Identifier stream browsers and mail programs write: Windows, Office and PDF readers then open the file
        // with their protections (Protected View, SmartScreen, macros blocked) instead of trusting it as the user's own.
        // An attachment can name a remote template with macros or run something through its program. Best effort.
        public static bool MarkFromInternet(string path)
        {
            try
            {
                const uint GenericWrite = 0x40000000, CreateAlways = 2, Normal = 0x80;
                // The stream of a file that is not there would create the file.
                if (!File.Exists(path)) return false;
                using (Microsoft.Win32.SafeHandles.SafeFileHandle h = CreateFile(path + ":Zone.Identifier", GenericWrite, 0, IntPtr.Zero, CreateAlways, Normal, IntPtr.Zero))
                {
                    if (h.IsInvalid)
                    {
                        ShotStack.Log("Adjunto (zona) " + Path.GetFileName(path) + ": error " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                        return false;
                    }
                    byte[] b = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
                    using (FileStream fs = new FileStream(h, FileAccess.Write)) fs.Write(b, 0, b.Length);
                }
                return true;
            }
            catch (Exception ex) { ShotStack.Log("Adjunto (zona): " + ex.Message); return false; }
        }

        static readonly TimeSpan[] KeepSpans = { TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromDays(1), TimeSpan.FromDays(7), TimeSpan.FromDays(30) };

        // Deletes the attachments older than the temporary captures may get (Settings.TempKeep; "until Stackshot closes"
        // keeps this session's; below zero: all, as when the temporary captures are all cleaned). Files still open
        // elsewhere stay for the next pass. Returns how many went.
        public static int Prune(int tempKeep)
        {
            int n = 0;
            // A test run without a private data folder leaves the real one alone.
            if (Settings.ReadOnly && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA"))) return 0;
            try
            {
                string dir = Folder;
                if (!Directory.Exists(dir)) return 0;
                int k = Math.Max(0, Math.Min(KeepSpans.Length - 1, tempKeep));
                DateTime limit;
                if (tempKeep < 0) limit = DateTime.MaxValue;
                else if (k == 1)
                {
                    try { limit = Process.GetCurrentProcess().StartTime; }
                    catch (Exception) { return 0; }
                }
                else limit = DateTime.Now - KeepSpans[k];
                foreach (string f in Directory.GetFiles(dir))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) > limit) continue;
                        FileAttributes fa = File.GetAttributes(f);
                        if ((fa & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(f, fa & ~FileAttributes.ReadOnly);
                            // Still open in its program: read-only again until a later pass.
                            try { File.Delete(f); }
                            catch (Exception) { File.SetAttributes(f, fa); continue; }
                        }
                        else File.Delete(f);
                        n++;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception ex) { ShotStack.Log("Adjuntos: " + ex.Message); }
            return n;
        }
    }
}
