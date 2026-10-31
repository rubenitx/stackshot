// Stackshot - The XML tree's value entries: Copiar valor (the text, or the attribute under the mouse), Copiar como JSON
// (worked out off the UI thread) and, for a field that carries a base64 file, Abrir el adjunto and Guardar el adjunto.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Stackshot
{
    public sealed partial class XmlTreeView
    {
        public event Action<XItem, bool> Attachment;    // a base64 field to open (true) or to save (false)
        public event Action<string, bool> Notice;       // a message for the status line (true: an error)

        // The last field looked at by the menu (a right click on the same node again reads nothing).
        string peekText;
        bool peekIsFile;
        AttachType peekType;

        void ValueEntries(List<MenuEntry> m, XRow r, MouseButtonEventArgs e)
        {
            XItem n = r.Node;
            if (!r.Close && n.Kind != XKind.Decl)
            {
                string v = n.Kind == XKind.Element ? (n.Inline && n.Kids.Count == 1 ? n.Kids[0].Value : null) : n.Value;
                if (v != null) m.Add(MenuEntry.Item(I18n.T("Copiar valor", "Copy value"), "copy", false, delegate { Ask(v, I18n.T("Valor copiado.", "Value copied.")); }));
                int a = AttrUnder(r, e);
                if (a >= 0)
                {
                    string key = n.Attrs[a].Key, val = n.Attrs[a].Value;
                    m.Add(MenuEntry.Item(I18n.Fmt("Copiar valor de \u00AB{0}\u00BB", "Copy value of \u201C{0}\u201D", key), "copy", false, delegate { Ask(val, I18n.T("Valor copiado.", "Value copied.")); }));
                }
            }
            if (n.Kind == XKind.Element || n.Kind == XKind.Text || n.Kind == XKind.CData)
                m.Add(MenuEntry.Item(I18n.T("Copiar como JSON", "Copy as JSON"), "copy", false, delegate { CopyJson(n); }));
            AttachEntries(m, n);
        }

        void AttachEntries(List<MenuEntry> m, XItem n)
        {
            string b64 = XmlAttachment.ValueOf(n);
            if (b64 == null || b64.Length < XmlAttachment.MinChars) return;
            if (!ReferenceEquals(b64, peekText))
            {
                peekText = b64;
                peekIsFile = XmlAttachment.IsBase64(b64);
                peekType = peekIsFile ? XmlAttachment.Peek(b64) : null;
            }
            if (!peekIsFile) return;
            AttachType t = peekType;
            string hint = XmlAttachment.HintName(n);
            bool blocked = XmlAttachment.Blocked(t, hint), can = t.Opens && !blocked;
            m.Add(MenuEntry.Line());
            MenuEntry open = MenuEntry.Item(can ? I18n.Fmt("Abrir el adjunto ({0})", "Open attachment ({0})", t.Label) : I18n.T("Abrir el adjunto", "Open attachment"), "open", false, delegate { if (Attachment != null) Attachment(n, true); });
            if (!can)
            {
                open.Enabled = false;
                bool article = t.Kind == AttachKind.Executable || t.Kind == AttachKind.Shortcut || t.Kind == AttachKind.Zip || t.Kind == AttachKind.OfficeMacros;
                open.Detail = article ? I18n.Fmt("Es {0}: solo se puede guardar", "It is {0}: can only be saved", t.Label)
                            : blocked ? I18n.T("Su nombre es de un programa o un script: solo se puede guardar", "Its name is for a program or script: can only be saved")
                            : I18n.T("Tipo desconocido: solo se puede guardar", "Unknown type: can only be saved");
            }
            m.Add(open);
            m.Add(MenuEntry.Item(I18n.T("Guardar el adjunto como\u2026", "Save attachment as\u2026"), "save", false, delegate { if (Attachment != null) Attachment(n, false); }));
        }

        // Copies the branch as JSON; the text is built off the UI thread (a whole document can be big).
        public void CopyJson(XItem n)
        {
            if (n == null) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null, json = null;
                try { json = XmlJson.Convert(n, MaxCopy, out err); }
                catch (Exception ex) { ShotStack.Log("XML a JSON: " + ex.Message); err = null; json = null; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (json != null) Ask(json, I18n.T("JSON copiado.", "JSON copied."));
                        else if (Notice != null) Notice(err ?? I18n.T("No he podido convertirlo a JSON.", "Could not convert to JSON."), true);
                    }));
                }
                catch { }
            });
        }

        // Which attribute of the row the mouse is on, or -1.
        int AttrUnder(XRow r, MouseButtonEventArgs e)
        {
            if (e == null || r.Close || r.Node.Kind != XKind.Element || r.Node.Attrs == null) return -1;
            System.Windows.Point p;
            XmlRowView v = RowAt(e, out p);
            if (v == null || v.Row != r || v.Children.Count == 0) return -1;
            TextBlock tb = v.Children[0] as TextBlock;
            if (tb == null) return -1;
            TextPointer tp = tb.GetPositionFromPoint(e.GetPosition(tb), false);
            Run run = tp != null ? tp.Parent as Run : null;
            if (run == null) return -1;
            int offset = 0;
            foreach (Inline i in tb.Inlines)
            {
                Run x = i as Run;
                if (x == null) continue;
                if (x == run) return AttrAt(r, offset + Math.Max(0, x.ContentStart.GetOffsetToPosition(tp)));
                offset += x.Text.Length;
            }
            return -1;
        }

        // The attribute drawn at a character of the row's text (as XmlRows.Pieces lays it out), or -1.
        internal static int AttrAt(XRow r, int offset)
        {
            XItem n = r.Node;
            if (r.Close || n.Kind != XKind.Element || n.Attrs == null || offset < 0) return -1;
            List<Piece> ps = XmlRows.Pieces(r, false);
            int at = 0, piece = -1;
            for (int i = 0; i < ps.Count; i++)
            {
                if (offset < at + ps[i].Text.Length) { piece = i; break; }
                at += ps[i].Text.Length;
            }
            if (piece < 0) return -1;
            // "<", the name (one piece, or prefix and local name), then per attribute: " ", name, "=\"", value..., "\"".
            int idx = 1 + (n.Prefix.Length > 0 && n.Name.StartsWith(n.Prefix + ":", StringComparison.Ordinal) ? 2 : 1);
            for (int a = 0; a < n.Attrs.Count; a++)
            {
                int start = idx;
                idx += 3;
                while (idx < ps.Count && !(ps[idx].Kind == Seg.Punct && ps[idx].Text == "\"")) idx++;
                idx++;
                if (piece >= start && piece < idx) return a;
            }
            return -1;
        }
    }
}
