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
        public bool Inner;        // an element whose children are the XML escaped in its text (ExpandInner)
        public int Line, Col;     // where it starts in the text (1-based; an element at its name)
        public int EndLine, EndCol;   // an element: where its end tag names it (0 when it is empty)

        // One row: no children, or just one piece of text.
        public bool Inline { get { return Kind != XKind.Element || Kids.Count == 0 || (Kids.Count == 1 && Kids[0].Kind == XKind.Text); } }
    }

    // How Formatear lays the XML out.
    public sealed class XmlFormat
    {
        public string Indent = "  ";
        public bool AttrLines;      // each attribute on its own line
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
            // Read through without building the tree: the background parse that follows builds it once.
            try
            {
                bool element = false;
                using (XmlReader r = Reader(t, true))
                    while (r.Read()) if (r.NodeType == XmlNodeType.Element) element = true;
                return element;
            }
            catch (Exception) { return false; }
        }

        public static string CheckText(string t)
        {
            if (t == null || t.Trim().Length == 0) return I18n.T("Est\u00E1 vac\u00EDo: no hay nada que mostrar.", "It's empty: there is nothing to show.");
            if (t.Length > MaxChars) return I18n.T("Es demasiado grande (m\u00E1ximo 8 MB de XML).", "It's too big (maximum 8 MB of XML).");
            if (t.IndexOf('\0') >= 0) return I18n.T("No parece texto: tiene contenido binario.", "This doesn't look like text: it has binary content.");
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
            return Parse(text, null, out model, out error, out line, out col);
        }

        // cancelled: asked now and then; when it says so the parse stops and returns false with no error.
        public static bool Parse(string text, Func<bool> cancelled, out XmlModel model, out string error, out int line, out int col)
        {
            model = null; error = null; line = 0; col = 0;
            XmlModel m = new XmlModel();
            XItem cur = null;
            try
            {
                using (XmlReader r = Reader(text, true))
                {
                    IXmlLineInfo li = r as IXmlLineInfo;
                    bool capped = false;
                    int reads = 0;
                    while (r.Read())
                    {
                        if (cancelled != null && (++reads & 4095) == 0 && cancelled()) return false;
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
                            case XmlNodeType.EndElement: if (cur != null) { if (li != null) { cur.EndLine = li.LineNumber; cur.EndCol = li.LinePosition; } cur = cur.Parent; } continue;
                            case XmlNodeType.Text:
                            case XmlNodeType.SignificantWhitespace:
                            case XmlNodeType.Whitespace:
                                if (Blank(r.Value)) continue;
                                n = new XItem { Kind = XKind.Text, Value = r.Value };
                                break;
                            case XmlNodeType.CDATA: n = new XItem { Kind = XKind.CData, Value = r.Value }; break;
                            case XmlNodeType.Comment: n = new XItem { Kind = XKind.Comment, Value = r.Value }; break;
                            case XmlNodeType.ProcessingInstruction: n = new XItem { Kind = XKind.Pi, Name = r.Name, Value = r.Value }; break;
                            default: continue;
                        }
                        if (li != null) { n.Line = li.LineNumber; n.Col = li.LinePosition; }
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
                error = (line > 0 ? I18n.Fmt("L\u00EDnea {0}, columna {1}: ", "Line {0}, column {1}: ", line, col) : "") + Friendly(ex);
                return false;
            }
            catch (Exception ex)
            {
                error = I18n.T("No he podido leer el XML: ", "Could not read the XML: ") + ex.Message;
                return false;
            }
            if (m.Top.Count == 0) { error = I18n.T("No hay ning\u00FAn elemento XML.", "No XML elements found."); return false; }
            model = m;
            return true;
        }

        static string Friendly(XmlException ex)
        {
            string m = ex.Message;
            m = Regex.Replace(m, @"\s*(Line|L[i\u00ED]nea)\s+\d+,\s*(position|posici[o\u00F3]n)\s+\d+\.?\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (m.IndexOf("DTD", StringComparison.Ordinal) >= 0)
                return I18n.T("Tiene una declaraci\u00F3n DOCTYPE (DTD), que Stackshot no procesa por seguridad. Quita esa l\u00EDnea para verlo.", "It has a DOCTYPE (DTD) declaration, which Stackshot doesn't process for security. Remove that line to view it.");
            Match g;
            g = Regex.Match(m, @"The '([^']*)' start tag on line (\d+) position (\d+) does not match the end tag of '([^']*)'");
            if (g.Success) return I18n.Fmt("La etiqueta de cierre </{0}> no coincide con <{1}>, abierta en la l\u00EDnea {2}, columna {3}.", "Closing tag </{0}> does not match <{1}>, opened on line {2}, column {3}.", g.Groups[4].Value, g.Groups[1].Value, g.Groups[2].Value, g.Groups[3].Value);
            g = Regex.Match(m, @"etiqueta de apertura '([^']*)' en la l[i\u00ED]nea (\d+) posici[o\u00F3]n (\d+) no coincide con la etiqueta de cierre de '([^']*)'", RegexOptions.IgnoreCase);
            if (g.Success) return I18n.Fmt("La etiqueta de cierre </{0}> no coincide con <{1}>, abierta en la l\u00EDnea {2}, columna {3}.", "Closing tag </{0}> does not match <{1}>, opened on line {2}, column {3}.", g.Groups[4].Value, g.Groups[1].Value, g.Groups[2].Value, g.Groups[3].Value);
            g = Regex.Match(m, @"Unexpected end of file has occurred\. The following elements are not closed: ([^.]*)");
            if (g.Success) return I18n.Fmt("El archivo termina sin cerrar: {0}.", "The file ends without closing: {0}.", g.Groups[1].Value);
            if (m.StartsWith("Unexpected end of file", StringComparison.Ordinal)) return I18n.T("El archivo termina antes de lo esperado.", "The file ends sooner than expected.");
            if (m.StartsWith("Root element is missing", StringComparison.Ordinal)) return I18n.T("Falta el elemento ra\u00EDz.", "Root element is missing.");
            if (m.StartsWith("There are multiple root elements", StringComparison.Ordinal)) return I18n.T("Hay m\u00E1s de un elemento ra\u00EDz.", "There are multiple root elements.");
            if (m.StartsWith("Data at the root level is invalid", StringComparison.Ordinal)) return I18n.T("Hay texto fuera del elemento ra\u00EDz o el documento empieza mal.", "There is text outside the root element, or the document starts wrong.");
            g = Regex.Match(m, @"'([^']*)' is a duplicate attribute name");
            if (g.Success) return I18n.Fmt("El atributo {0} est\u00E1 repetido.", "The attribute {0} is repeated.", g.Groups[1].Value);
            if (m.IndexOf("entity", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0) return I18n.T("Referencia a una entidad no v\u00E1lida (para escribir & usa &amp;).", "Invalid entity reference (to write & use &amp;).");
            if (m.IndexOf("invalid character", StringComparison.OrdinalIgnoreCase) >= 0) return I18n.T("Hay un car\u00E1cter que XML no admite. ", "There is a character XML doesn't allow. ") + m;
            if (m.IndexOf("namespace", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("prefix", StringComparison.OrdinalIgnoreCase) >= 0) return I18n.T("Prefijo de espacio de nombres sin declarar. ", "Undeclared namespace prefix. ") + m;
            return m;
        }

        // ---- Rows as text

        public static string Esc(string s, bool attr)
        {
            if (s.IndexOfAny(new char[] { '&', '<', '>', '"' }) < 0) return s;
            s = s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return attr ? s.Replace("\"", "&quot;") : s;
        }

        // ---- XML inside XML: a web service that answers with a whole document escaped in one field

        // Elements whose only content is text that is itself a document (it starts with '<' and parses) show that
        // document as their children, marked Inner; the file itself is not changed. Up to three levels deep and 4 MB in
        // all. Inner nodes point at their host's line. Returns how many fields were opened.
        public static int ExpandInner(XmlModel m)
        {
            int budget = 4 * 1024 * 1024, found = 0;
            Queue<KeyValuePair<XItem, int>> work = new Queue<KeyValuePair<XItem, int>>();
            foreach (XItem t in m.Top) work.Enqueue(new KeyValuePair<XItem, int>(t, 0));
            while (work.Count > 0)
            {
                KeyValuePair<XItem, int> w = work.Dequeue();
                XItem e = w.Key;
                if (e.Kind != XKind.Element) continue;
                // Opened by an earlier pass: its document still counts as a level.
                if (e.Inner) { foreach (XItem k in e.Kids) work.Enqueue(new KeyValuePair<XItem, int>(k, w.Value + 1)); continue; }
                if (w.Value < 3 && e.Kids.Count == 1 && (e.Kids[0].Kind == XKind.Text || e.Kids[0].Kind == XKind.CData))
                {
                    string v = e.Kids[0].Value.Trim();
                    XmlModel inner;
                    string err;
                    int l, c;
                    if (v.Length > 2 && v[0] == '<' && v[v.Length - 1] == '>' && v.Length <= budget &&
                        Parse(v, out inner, out err, out l, out c) && inner != null && inner.Top.Exists(delegate(XItem x) { return x.Kind == XKind.Element; }))
                    {
                        budget -= v.Length;
                        found++;
                        e.Kids.Clear();
                        foreach (XItem k in inner.Top)
                        {
                            if (k.Kind == XKind.Decl) continue;
                            k.Parent = e;
                            k.Sibs = e.Kids;
                            e.Kids.Add(k);
                        }
                        e.Inner = true;
                        PointAt(e.Kids, e.Line, e.Col);
                        foreach (XItem k in e.Kids) work.Enqueue(new KeyValuePair<XItem, int>(k, w.Value + 1));
                        continue;
                    }
                }
                foreach (XItem k in e.Kids) work.Enqueue(new KeyValuePair<XItem, int>(k, w.Value));
            }
            return found;
        }

        static void PointAt(List<XItem> items, int line, int col)
        {
            foreach (XItem k in items)
            {
                k.Line = line; k.Col = col; k.EndLine = 0; k.EndCol = 0;
                PointAt(k.Kids, line, col);
            }
        }

        // A SOAP fault anywhere in the document (escaped XML included, once expanded): its code and its message (SOAP 1.1
        // faultcode and faultstring, 1.2 Code/Value and Reason/Text). False when there is none.
        public static bool Fault(XmlModel m, out string code, out string message)
        {
            code = message = null;
            XItem f = Find(m.Top, delegate(XItem x) { return x.Kind == XKind.Element && x.Local == "Fault"; });
            if (f == null) return false;
            XItem c = Find(f.Kids, delegate(XItem x) { return x.Kind == XKind.Element && (x.Local == "faultcode" || (x.Local == "Value" && x.Parent != null && x.Parent.Local == "Code")); });
            XItem s = Find(f.Kids, delegate(XItem x) { return x.Kind == XKind.Element && (x.Local == "faultstring" || (x.Local == "Text" && x.Parent != null && x.Parent.Local == "Reason")); });
            code = c != null ? TextOf(c) : "";
            message = s != null ? TextOf(s) : "";
            return true;
        }

        static XItem Find(List<XItem> items, Predicate<XItem> want)
        {
            foreach (XItem i in items)
            {
                if (want(i)) return i;
                XItem k = Find(i.Kids, want);
                if (k != null) return k;
            }
            return null;
        }

        // The text right inside an element, trimmed.
        public static string TextOf(XItem e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (XItem k in e.Kids) if (k.Kind == XKind.Text || k.Kind == XKind.CData) sb.Append(k.Value);
            return sb.ToString().Trim();
        }

        // ---- XPath of a node (indexes only where a sibling shares the name; default-namespace names use local-name())

        // Inside escaped XML (ExpandInner) the path has two parts: the host field's, then the one within that document.
        public static string XPath(XItem n)
        {
            List<string> docs = new List<string>(), parts = new List<string>();
            for (XItem c = n; c != null; c = c.Parent)
            {
                string step = null;
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
                }
                if (step != null) parts.Add(step);
                if (c.Parent != null && c.Parent.Inner)
                {
                    parts.Reverse();
                    docs.Add("/" + string.Join("/", parts.ToArray()));
                    parts.Clear();
                }
            }
            parts.Reverse();
            docs.Add("/" + string.Join("/", parts.ToArray()));
            docs.Reverse();
            return string.Join(" \u00BB ", docs.ToArray());
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

        // Whitespace as XML means it: space, tab, CR and LF only. A no-break or ideographic space is content (string.Trim
        // would take it for blank and formatting would delete <b>&#160;</b>'s text).
        static bool Blank(string s)
        {
            foreach (char c in s ?? "") if (c != ' ' && c != '\t' && c != '\r' && c != '\n') return false;
            return true;
        }

        // Whitespace inside xml:space="preserve" (SVG text, XAML runs, Office XML) is content: it is never touched.
        static void DropBlanks(XDocument d)
        {
            XName space = XNamespace.Xml + "space";
            // Whitespace between the declaration, comments and the root would make the writer stop indenting there.
            foreach (XText t in d.Nodes().OfType<XText>().ToList()) t.Remove();
            Stack<KeyValuePair<XElement, bool>> st = new Stack<KeyValuePair<XElement, bool>>();
            if (d.Root != null) st.Push(new KeyValuePair<XElement, bool>(d.Root, false));
            while (st.Count > 0)
            {
                KeyValuePair<XElement, bool> top = st.Pop();
                XElement e = top.Key;
                XAttribute a = e.Attribute(space);
                bool keep = a != null ? a.Value == "preserve" : top.Value;
                if (!keep)
                {
                    bool mixed = e.Nodes().Any(x => x is XCData || (x is XText && !Blank(((XText)x).Value)));
                    if (!mixed) foreach (XText t in e.Nodes().OfType<XText>().Where(x => !(x is XCData)).ToList()) t.Remove();
                }
                foreach (XElement k in e.Elements()) st.Push(new KeyValuePair<XElement, bool>(k, keep));
            }
        }

        static string Write(XDocument d, bool indent, XmlFormat f)
        {
            DropBlanks(d);
            XmlWriterSettings s = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = indent, IndentChars = f.Indent, NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize, ConformanceLevel = ConformanceLevel.Document, NewLineOnAttributes = indent && f.AttrLines };
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
            return Format(text, indent, new XmlFormat(), out error);
        }

        public static string Format(string text, bool indent, XmlFormat f, out string error)
        {
            error = null;
            try { return Write(Load(text), indent, f ?? new XmlFormat()); }
            catch (XmlException ex) { error = (ex.LineNumber > 0 ? I18n.Fmt("L\u00EDnea {0}, columna {1}: ", "Line {0}, column {1}: ", ex.LineNumber, ex.LinePosition) : "") + Friendly(ex); return null; }
            catch (OutOfMemoryException) { error = I18n.T("Est\u00E1 tan anidado o es tan grande que no se puede formatear con sangr\u00EDa.", "It's so deeply nested or so big that it can't be formatted with indentation."); return null; }
            catch (Exception ex) { error = I18n.T("No he podido leer el XML: ", "Could not read the XML: ") + ex.Message; return null; }
        }
    }
}
