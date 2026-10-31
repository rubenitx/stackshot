// Stackshot - A branch of the XML tree as JSON: attributes as "@name", text as "#text" when it sits next to attributes or
// child elements, repeated names as arrays, prefixes kept, every value a string (numbers and booleans included), XML
// opened inside a field (XmlDoc.ExpandInner) as objects. Written without recursion, so any depth works.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;

namespace Stackshot
{
    public static class XmlJson
    {
        sealed class Frame
        {
            public List<KeyValuePair<string, List<XItem>>> Groups;
            public int Group, Item = -1, Depth;
            public bool Any;
        }

        static readonly List<string> pads = new List<string>();

        static string Pad(int depth)
        {
            lock (pads)
            {
                while (pads.Count <= depth) pads.Add(new string(' ', pads.Count * 2));
                return pads[depth];
            }
        }

        // An element as { "name": value }, a text node as a string. Null with the reason when the node has no JSON form or
        // the text would pass max characters.
        public static string Convert(XItem n, int max, out string error)
        {
            error = null;
            if (n == null) { error = I18n.T("No hay nada seleccionado.", "Nothing selected."); return null; }
            StringBuilder sb = new StringBuilder();
            if (n.Kind == XKind.Text || n.Kind == XKind.CData) { Str(sb, Clean(n.Value)); return sb.ToString(); }
            if (n.Kind != XKind.Element) { error = I18n.T("Solo los elementos y el texto se pueden copiar como JSON.", "Only elements and text can be copied as JSON."); return null; }
            sb.Append("{\n").Append(Pad(1));
            Str(sb, n.Name);
            sb.Append(": ");
            Stack<Frame> st = new Stack<Frame>();
            Emit(sb, n, 1, st);
            while (st.Count > 0)
            {
                if (sb.Length > max) { error = I18n.T("Es demasiado grande para copiarlo como JSON.", "Too large to copy as JSON."); return null; }
                Frame f = st.Peek();
                if (f.Group >= f.Groups.Count)
                {
                    sb.Append('\n').Append(Pad(f.Depth)).Append('}');
                    st.Pop();
                    continue;
                }
                KeyValuePair<string, List<XItem>> g = f.Groups[f.Group];
                if (g.Value.Count == 1)
                {
                    if (f.Item >= 0) { f.Group++; f.Item = -1; continue; }
                    Key(sb, f, g.Key);
                    f.Item = 0;
                    Emit(sb, g.Value[0], f.Depth + 1, st);
                    continue;
                }
                if (f.Item < 0) { Key(sb, f, g.Key); sb.Append('['); f.Item = 0; }
                if (f.Item >= g.Value.Count)
                {
                    sb.Append('\n').Append(Pad(f.Depth + 1)).Append(']');
                    f.Group++;
                    f.Item = -1;
                    continue;
                }
                sb.Append(f.Item > 0 ? ",\n" : "\n").Append(Pad(f.Depth + 2));
                XItem x = g.Value[f.Item++];
                Emit(sb, x, f.Depth + 2, st);
            }
            sb.Append("\n}");
            if (sb.Length > max) { error = I18n.T("Es demasiado grande para copiarlo como JSON.", "Too large to copy as JSON."); return null; }
            return sb.ToString();
        }

        static void Key(StringBuilder sb, Frame f, string key)
        {
            sb.Append(f.Any ? ",\n" : "\n").Append(Pad(f.Depth + 1));
            f.Any = true;
            Str(sb, key);
            sb.Append(": ");
        }

        static bool IsText(XItem k) { return k.Kind == XKind.Text || k.Kind == XKind.CData; }

        // A value: a string for text, null or a string for an element with neither attributes nor child elements, else an
        // object whose members are written by the frame pushed here.
        static void Emit(StringBuilder sb, XItem x, int depth, Stack<Frame> st)
        {
            if (IsText(x)) { Str(sb, Clean(x.Value)); return; }
            bool attrs = x.Attrs != null && x.Attrs.Count > 0, elements = false;
            foreach (XItem k in x.Kids) if (k.Kind == XKind.Element) { elements = true; break; }
            if (!attrs && !elements)
            {
                StringBuilder t = null;
                foreach (XItem k in x.Kids)
                    if (IsText(k)) { if (t == null) t = new StringBuilder(); t.Append(k.Value); }
                if (t == null) sb.Append("null"); else Str(sb, Clean(t.ToString()));
                return;
            }
            sb.Append('{');
            Frame f = new Frame { Depth = depth, Groups = new List<KeyValuePair<string, List<XItem>>>() };
            if (attrs)
                foreach (KeyValuePair<string, string> a in x.Attrs)
                {
                    sb.Append(f.Any ? ",\n" : "\n").Append(Pad(depth + 1));
                    f.Any = true;
                    Str(sb, "@" + a.Key);
                    sb.Append(": ");
                    Str(sb, a.Value);
                }
            // Members in the order their names first appear; same-named siblings anywhere in the element join one array.
            Dictionary<string, List<XItem>> byName = new Dictionary<string, List<XItem>>(StringComparer.Ordinal);
            foreach (XItem k in x.Kids)
            {
                string key = k.Kind == XKind.Element ? k.Name : IsText(k) ? "#text" : null;
                if (key == null) continue;
                List<XItem> list;
                if (!byName.TryGetValue(key, out list))
                {
                    list = new List<XItem>();
                    byName[key] = list;
                    f.Groups.Add(new KeyValuePair<string, List<XItem>>(key, list));
                }
                list.Add(k);
            }
            st.Push(f);
        }

        // Text without the whitespace XML puts around it (a no-break space is content and stays).
        static string Clean(string s) { return (s ?? "").Trim(' ', '\t', '\r', '\n'); }

        public static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
