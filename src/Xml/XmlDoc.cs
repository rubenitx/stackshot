// Stackshot - XML helpers: safe parsing into a light tree, friendly errors, XPath, indenting and compacting.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Stackshot
{
    public enum XKind { Decl, Element, Text, Comment, CData, Pi }

    public sealed class XItem
    {
        public XKind Kind;
        public string Name = "", Local = "", Prefix = "", NsUri = "", Value = "";
        public List<KeyValuePair<string, string>> Attrs;
        public XItem Parent;
        public List<XItem> Sibs;
        public readonly List<XItem> Kids = new List<XItem>();
        public bool Empty, Expanded = true;

        // One row: no children, or just one piece of text.
        public bool Inline { get { return Kind != XKind.Element || Kids.Count == 0 || (Kids.Count == 1 && Kids[0].Kind == XKind.Text); } }
    }

    public sealed class XmlModel
    {
        public readonly List<XItem> Top = new List<XItem>();
        public int Count;
        public bool Truncated;
    }

    public static class XmlDoc
    {
        public const int MaxChars = 8 * 1024 * 1024;
        public const int MaxNodes = 250000;
        static readonly string[] Exts = { ".xml", ".xsd", ".config", ".svg", ".xaml" };

        public static bool IsXmlPath(string path)
        {
            if (path == null) return false;
            string e;
            try { e = Path.GetExtension(StripLine(path)).ToLowerInvariant(); } catch { return false; }
            return Array.IndexOf(Exts, e) >= 0;
        }

        static string StripLine(string s) { return Regex.Replace(s, @"(?<=[^:\\/]):\d+(?::\d+)?$", ""); }

        // Pasted or dropped text: XML when it says so, or when the whole text is one well-formed document.
        public static bool Sniff(string t)
        {
            if (t == null) return false;
            string s = t.TrimStart();
            if (s.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Length < 3 || s[0] != '<' || t.Length > 1024 * 1024) return false;
            XmlModel m; string err; int l, c;
            return Parse(t, out m, out err, out l, out c) && m != null && m.Top.Any(n => n.Kind == XKind.Element);
        }

        public static string CheckText(string t)
        {
            if (t == null || t.Trim().Length == 0) return "Est\u00E1 vac\u00EDo: no hay nada que mostrar.";
            if (t.Length > MaxChars) return "Es demasiado grande (m\u00E1ximo 8 MB de XML).";
            if (t.IndexOf('\0') >= 0) return "No parece texto: tiene contenido binario.";
            return null;
        }

        static XmlReader Reader(string text, bool ignoreWs)
        {
            XmlReaderSettings s = new XmlReaderSettings();
            s.DtdProcessing = DtdProcessing.Prohibit;
            s.XmlResolver = null;
            s.IgnoreWhitespace = ignoreWs;
            s.CheckCharacters = true;
            return XmlReader.Create(new StringReader(text), s);
        }

        // True when it is well-formed (model filled); otherwise error holds a friendly message with line and column.
        public static bool Parse(string text, out XmlModel model, out string error, out int line, out int col)
        {
            model = null; error = null; line = 0; col = 0;
            XmlModel m = new XmlModel();
            XItem cur = null;
            try
            {
                using (XmlReader r = Reader(text, true))
                {
                    bool capped = false;
                    while (r.Read())
                    {
                        if (capped) continue;
                        if (m.Count >= MaxNodes) { capped = true; m.Truncated = true; continue; }
                        XItem n = null;
                        switch (r.NodeType)
                        {
                            case XmlNodeType.XmlDeclaration: n = new XItem { Kind = XKind.Decl, Name = "xml", Value = r.Value }; break;
                            case XmlNodeType.Element:
                                n = new XItem { Kind = XKind.Element, Name = r.Name, Local = r.LocalName, Prefix = r.Prefix, NsUri = r.NamespaceURI, Empty = r.IsEmptyElement };
                                if (r.HasAttributes)
                                {
                                    n.Attrs = new List<KeyValuePair<string, string>>();
                                    while (r.MoveToNextAttribute()) n.Attrs.Add(new KeyValuePair<string, string>(r.Name, r.Value));
                                    r.MoveToElement();
                                }
                                break;
                            case XmlNodeType.EndElement: if (cur != null) cur = cur.Parent; continue;
                            case XmlNodeType.Text:
                            case XmlNodeType.SignificantWhitespace:
                            case XmlNodeType.Whitespace:
                                if (r.Value.Trim().Length == 0) continue;
                                n = new XItem { Kind = XKind.Text, Value = r.Value };
                                break;
                            case XmlNodeType.CDATA: n = new XItem { Kind = XKind.CData, Value = r.Value }; break;
                            case XmlNodeType.Comment: n = new XItem { Kind = XKind.Comment, Value = r.Value }; break;
                            case XmlNodeType.ProcessingInstruction: n = new XItem { Kind = XKind.Pi, Name = r.Name, Value = r.Value }; break;
                            default: continue;
                        }
                        n.Parent = cur;
                        n.Sibs = cur != null ? cur.Kids : m.Top;
                        n.Sibs.Add(n);
                        m.Count++;
                        if (n.Kind == XKind.Element && !n.Empty) cur = n;
                    }
                }
            }
            catch (XmlException ex)
            {
                line = ex.LineNumber; col = ex.LinePosition;
                error = (line > 0 ? "L\u00EDnea " + line + ", columna " + col + ": " : "") + Friendly(ex);
                return false;
            }
            catch (Exception ex)
            {
                error = "No he podido leer el XML: " + ex.Message;
                return false;
            }
            if (m.Top.Count == 0) { error = "No hay ning\u00FAn elemento XML."; return false; }
            model = m;
            return true;
        }

        static string Friendly(XmlException ex)
        {
            string m = ex.Message;
            m = Regex.Replace(m, @"\s*(Line|L[i\u00ED]nea)\s+\d+,\s*(position|posici[o\u00F3]n)\s+\d+\.?\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (m.IndexOf("DTD", StringComparison.Ordinal) >= 0)
                return "Tiene una declaraci\u00F3n DOCTYPE (DTD), que Stackshot no procesa por seguridad. Quita esa l\u00EDnea para verlo.";
            Match g;
            g = Regex.Match(m, @"The '([^']*)' start tag on line (\d+) position (\d+) does not match the end tag of '([^']*)'");
            if (g.Success) return "La etiqueta de cierre </" + g.Groups[4].Value + "> no coincide con <" + g.Groups[1].Value + ">, abierta en la l\u00EDnea " + g.Groups[2].Value + ", columna " + g.Groups[3].Value + ".";
            g = Regex.Match(m, @"etiqueta de apertura '([^']*)' en la l[i\u00ED]nea (\d+) posici[o\u00F3]n (\d+) no coincide con la etiqueta de cierre de '([^']*)'", RegexOptions.IgnoreCase);
            if (g.Success) return "La etiqueta de cierre </" + g.Groups[4].Value + "> no coincide con <" + g.Groups[1].Value + ">, abierta en la l\u00EDnea " + g.Groups[2].Value + ", columna " + g.Groups[3].Value + ".";
            g = Regex.Match(m, @"Unexpected end of file has occurred\. The following elements are not closed: ([^.]*)");
            if (g.Success) return "El archivo termina sin cerrar: " + g.Groups[1].Value + ".";
            if (m.StartsWith("Unexpected end of file", StringComparison.Ordinal)) return "El archivo termina antes de lo esperado.";
            if (m.StartsWith("Root element is missing", StringComparison.Ordinal)) return "Falta el elemento ra\u00EDz.";
            if (m.StartsWith("There are multiple root elements", StringComparison.Ordinal)) return "Hay m\u00E1s de un elemento ra\u00EDz.";
            if (m.StartsWith("Data at the root level is invalid", StringComparison.Ordinal)) return "Hay texto fuera del elemento ra\u00EDz o el documento empieza mal.";
            g = Regex.Match(m, @"'([^']*)' is a duplicate attribute name");
            if (g.Success) return "El atributo " + g.Groups[1].Value + " est\u00E1 repetido.";
            if (m.IndexOf("entity", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0) return "Referencia a una entidad no v\u00E1lida (para escribir & usa &amp;).";
            if (m.IndexOf("invalid character", StringComparison.OrdinalIgnoreCase) >= 0) return "Hay un car\u00E1cter que XML no admite. " + m;
            if (m.IndexOf("namespace", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("prefix", StringComparison.OrdinalIgnoreCase) >= 0) return "Prefijo de espacio de nombres sin declarar. " + m;
            return m;
        }

        // ---- Rows as text

        public static string Esc(string s, bool attr)
        {
            if (s.IndexOfAny(new char[] { '&', '<', '>', '"' }) < 0) return s;
            s = s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return attr ? s.Replace("\"", "&quot;") : s;
        }

        // ---- XPath of a node (indexes only where a sibling shares the name; default-namespace names use local-name())

        public static string XPath(XItem n)
        {
            List<string> parts = new List<string>();
            for (XItem c = n; c != null; c = c.Parent)
            {
                string step;
                switch (c.Kind)
                {
                    case XKind.Element:
                    {
                        bool dflt = c.Prefix.Length == 0 && c.NsUri.Length > 0;
                        step = dflt ? "*[local-name()='" + c.Local + "']" : c.Name;
                        int same = 0, idx = 0;
                        foreach (XItem s in c.Sibs)
                            if (s.Kind == XKind.Element && s.Local == c.Local && s.NsUri == c.NsUri) { same++; if (s == c) idx = same; }
                        if (same > 1) step += "[" + idx + "]";
                        break;
                    }
                    case XKind.Text: case XKind.CData: step = Counted(c, "text()", true); break;
                    case XKind.Comment: step = Counted(c, "comment()", false); break;
                    case XKind.Pi: step = "processing-instruction('" + c.Name + "')"; break;
                    default: continue;
                }
                parts.Add(step);
            }
            parts.Reverse();
            return "/" + string.Join("/", parts.ToArray());
        }

        static string Counted(XItem c, string fn, bool text)
        {
            int same = 0, idx = 0;
            foreach (XItem s in c.Sibs)
                if (text ? (s.Kind == XKind.Text || s.Kind == XKind.CData) : s.Kind == c.Kind) { same++; if (s == c) idx = same; }
            return same > 1 ? fn + "[" + idx + "]" : fn;
        }

        // ---- Indent and compact (only whitespace between elements changes; text next to markup is kept as is)

        static XDocument Load(string text)
        {
            using (XmlReader r = Reader(text, false)) return XDocument.Load(r, LoadOptions.PreserveWhitespace);
        }

        static void DropBlanks(XDocument d)
        {
            foreach (XElement e in d.Descendants().ToList())
            {
                bool mixed = e.Nodes().Any(x => x is XCData || (x is XText && ((XText)x).Value.Trim().Length > 0));
                if (mixed) continue;
                foreach (XText t in e.Nodes().OfType<XText>().Where(x => !(x is XCData)).ToList()) t.Remove();
            }
        }

        static string Write(XDocument d, bool indent)
        {
            DropBlanks(d);
            XmlWriterSettings s = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = indent, IndentChars = "  ", NewLineChars = "\n", NewLineHandling = NewLineHandling.None, ConformanceLevel = ConformanceLevel.Document };
            StringBuilder body = new StringBuilder();
            using (StringWriter sw = new StringWriter(body))
            using (XmlWriter w = XmlWriter.Create(sw, s)) d.Save(w);
            // The writer breaks the line after a declaration it was told to leave out: drop that, add our own.
            string text = body.ToString().TrimStart('\n');
            if (d.Declaration != null) text = d.Declaration.ToString() + (indent ? "\n" : "") + text;
            return indent ? text + "\n" : text;
        }

        // Null with the reason in error when the text is not well-formed.
        public static string Format(string text, bool indent, out string error)
        {
            error = null;
            try { return Write(Load(text), indent); }
            catch (XmlException ex) { error = (ex.LineNumber > 0 ? "L\u00EDnea " + ex.LineNumber + ", columna " + ex.LinePosition + ": " : "") + Friendly(ex); return null; }
            catch (OutOfMemoryException) { error = "Est\u00E1 tan anidado o es tan grande que no se puede formatear con sangr\u00EDa."; return null; }
            catch (Exception ex) { error = "No he podido leer el XML: " + ex.Message; return null; }
        }
    }
}
