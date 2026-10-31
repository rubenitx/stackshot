// Stackshot - Base64 attachments of an XML field, opened or saved from the tree: images in Stackshot's editor, XML and
// text as a new document here, PDF and Office files in their own program after asking. Programs, scripts and shortcuts
// are never opened. Also the tree's wiring for its value entries and the JSON copy of Más opciones.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Windows.Input;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        // Tests answer these instead of opening a program or the image editor (the file in; false when it could not).
        internal static Func<string, bool> OpenWithApp { get; set; }
        internal static Func<string, bool> OpenInEditor { get; set; }
        bool attachHooked;

        void WireTree()
        {
            tree.Attachment += OnAttachment;
            tree.Notice += delegate(string msg, bool bad) { Say(msg, bad); };
        }

        // The node of the tree that has a JSON form and is selected, or null.
        XItem JsonSelection()
        {
            XRow r = tree != null ? tree.List.SelectedItem as XRow : null;
            return r != null && (r.Node.Kind == XKind.Element || r.Node.Kind == XKind.Text || r.Node.Kind == XKind.CData) ? r.Node : null;
        }

        // Más opciones > Copiar como JSON: the node selected in the tree, or the whole document.
        void CopyJsonFromMenu()
        {
            if (tree == null || model == null) return;
            XItem n = JsonSelection();
            if (n == null) foreach (XItem t in model.Top) if (t.Kind == XKind.Element) { n = t; break; }
            if (n != null) tree.CopyJson(n);
        }

        int TempKeep { get { return owner != null && owner.Settings != null ? owner.Settings.TempKeep : 0; } }

        void OnAttachment(XItem n, bool toOpen)
        {
            string value = XmlAttachment.ValueOf(n);
            if (value == null) return;
            string hint = XmlAttachment.HintName(n);
            if (toOpen) OpenAttachment(value, hint); else SaveAttachment(value, hint);
        }

        // Decoded (and, for what another program opens, written to the private folder) off the UI thread.
        void OpenAttachment(string value, string hint)
        {
            fade.Stop();
            status = I18n.T("Abriendo el adjunto\u2026", "Opening attachment\u2026");
            statusBad = false;
            Stats();
            Cursor = Cursors.AppStarting;
            int keep = TempKeep;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null, path = null;
                byte[] data = null;
                AttachType t = null;
                DocFile doc = null;
                try
                {
                    data = XmlAttachment.Decode(value, out err);
                    if (data != null)
                    {
                        t = XmlAttachment.Detect(data, false);
                        // XML and text are read here too (a big one would hold the window), and refused before anything
                        // is asked when the viewer could not show them.
                        if (t.Readable && !XmlAttachment.Blocked(t, hint))
                        {
                            doc = DocFile.FromBytes(data, t.Kind == AttachKind.Xml);
                            err = t.Kind == AttachKind.Xml ? XmlDoc.CheckText(doc.Text) : MarkdownTools.Validate(doc.Text);
                            if (err != null) { err += " " + I18n.T("Usa \u00ABGuardar el adjunto como\u2026\u00BB.", "Use \u201CSave attachment as\u2026\u201D."); doc = null; }
                        }
                        if (!XmlAttachment.Blocked(t, hint) && (t.Image || t.Document))
                        {
                            path = XmlAttachment.WriteTemp(data, XmlAttachment.FileName(t, hint), t.Document, out err);
                            // The editor takes PNG, JPEG, BMP and TIFF; a GIF goes in as its first frame.
                            if (path != null && t.Kind == AttachKind.Gif) path = GifToPng(path, out err);
                            XmlAttachment.Prune(keep);
                        }
                    }
                }
                catch (Exception ex) { ShotStack.Log("Adjunto: " + ex.Message); err = I18n.T("No he podido leer el adjunto.", "Could not read attachment."); data = null; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (open != this) return;
                        Cursor = null;
                        status = null;
                        Stats();
                        Opened(data, t, hint, path, doc, err);
                    }));
                }
                catch { }
            });
        }

        void Opened(byte[] data, AttachType t, string hint, string path, DocFile doc, string err)
        {
            if (data == null) { Say(err ?? I18n.T("No he podido leer el adjunto.", "Could not read attachment."), true); return; }
            string saveIt = I18n.T("usa \u00ABGuardar el adjunto como\u2026\u00BB", "use \u201CSave attachment as\u2026\u201D");
            if (XmlAttachment.Blocked(t, hint)) { Say(I18n.Fmt("Por seguridad no abro programas, scripts ni accesos directos: {0}.", "For security reasons programs, scripts and shortcuts are not opened: {0}.", saveIt), true); return; }
            if (t.Readable)
            {
                if (doc == null) { Say(err ?? I18n.T("No he podido leer el adjunto.", "Could not read attachment."), true); return; }
                ShowAttachmentText(doc, t, hint);
                return;
            }
            if (!t.Image && !t.Document)
            {
                Say(t.Kind == AttachKind.Zip ? I18n.Fmt("Es un archivo ZIP: {0}.", "It is a ZIP file: {0}.", saveIt) : t.Kind == AttachKind.OfficeMacros ? I18n.Fmt("Tiene macros: {0} si conf\u00EDas en \u00E9l.", "Contains macros: {0} if you trust it.", saveIt) : I18n.Fmt("No s\u00E9 qu\u00E9 tipo de archivo es: {0}.", "Unknown file type: {0}.", saveIt), true);
                return;
            }
            if (path == null) { Say(err ?? I18n.T("No he podido escribir el adjunto.", "Could not write attachment."), true); return; }
            if (!attachHooked)
            {
                attachHooked = true;
                Closed += delegate { int k = TempKeep; System.Threading.ThreadPool.QueueUserWorkItem(delegate { XmlAttachment.Prune(k); }); };
            }
            string shown = Path.GetFileName(path);
            if (t.Image)
            {
                bool ok = false;
                Func<string, bool> hook = OpenInEditor;
                try { ok = hook != null ? hook(path) : owner != null && owner.OpenEditor(path); }
                catch (Exception ex) { ShotStack.Log("Adjunto en el editor: " + ex.Message); }
                if (!ok) Say(I18n.T("No he podido abrir la imagen en el editor.", "Could not open image in editor."), true);
                return;
            }
            string ext = t.Ext;
            string what = I18n.IsSpanish ? (t.Kind == AttachKind.Excel || t.Kind == AttachKind.PowerPoint ? "Es una " : "Es un ") + t.Label
                                         : (t.Label.Length > 0 && "AEIOUaeiou".IndexOf(t.Label[0]) >= 0 ? "an " : "a ") + t.Label;
            int r = DocAlert.Ask(this, I18n.Fmt("\u00BFAbrir el adjunto \u00AB{0}\u00BB?", "Open attachment \u201C{0}\u201D?", shown),
                                 I18n.Fmt("{0} que viene dentro del XML. Se abrir\u00E1 con el programa que Windows tenga para los archivos {1}.", "It's {0} that came inside the XML. It will open with the program Windows uses for {1} files.", what, ext),
                                 new string[] { I18n.T("Abrir", "Open"), I18n.T("Cancelar", "Cancel") }, 0, 1);
            if (r != 0) return;
            bool done = false;
            Func<string, bool> app = OpenWithApp;
            try
            {
                if (app != null) done = app(path);
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    done = true;
                }
            }
            catch (Exception ex) { ShotStack.Log("Abrir adjunto: " + ex.Message); }
            if (!done) Say(I18n.Fmt("No he podido abrir \u00AB{0}\u00BB: quiz\u00E1 no haya ning\u00FAn programa para los archivos {1}.", "Could not open \u201C{0}\u201D: perhaps there is no program for {1} files.", shown, ext), true);
        }

        // XML or text from a field: a new document that is not a file yet (Guardar asks where), after the usual question
        // about unsaved changes in the one open.
        void ShowAttachmentText(DocFile d, AttachType t, string hint)
        {
            if (!ConfirmDiscard()) return;
            LoadText(d.Text, XmlAttachment.FileName(t, hint), d);
        }

        static string GifToPng(string gif, out string err)
        {
            err = null;
            try
            {
                string png = ShotStack.Unique(Path.ChangeExtension(gif, ".png"));
                using (System.Drawing.Image img = System.Drawing.Image.FromFile(gif)) img.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                return png;
            }
            catch (Exception ex) { ShotStack.Log("Adjunto GIF: " + ex.Message); err = I18n.T("No he podido leer la imagen GIF.", "Could not read GIF image."); return null; }
        }

        // Decoded first (its type gives the name), then the user says where, then it is written; all but the dialog off
        // the UI thread.
        void SaveAttachment(string value, string hint)
        {
            fade.Stop();
            status = I18n.T("Leyendo el adjunto\u2026", "Reading attachment\u2026");
            statusBad = false;
            Stats();
            Cursor = Cursors.AppStarting;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                byte[] data = null;
                AttachType t = null;
                try
                {
                    data = XmlAttachment.Decode(value, out err);
                    if (data != null) t = XmlAttachment.Detect(data, false);
                }
                catch (Exception ex) { ShotStack.Log("Adjunto: " + ex.Message); err = I18n.T("No he podido leer el adjunto.", "Could not read attachment."); data = null; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (open != this) return;
                        Cursor = null;
                        status = null;
                        Stats();
                        if (data == null) { Say(err ?? I18n.T("No he podido leer el adjunto.", "Could not read attachment."), true); return; }
                        string target = AskAttachmentPath(XmlAttachment.FileName(t, hint));
                        if (target != null) WriteAttachment(data, target);
                    }));
                }
                catch { }
            });
        }

        void WriteAttachment(byte[] data, string target)
        {
            Say(I18n.T("Guardando el adjunto\u2026", "Saving attachment\u2026"), false);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err;
                bool ok = DocFile.Write(target, data, out err);
                // Saved where the user wants it, but still something that came inside a message (a program run from there
                // gets SmartScreen's look first).
                if (ok) XmlAttachment.MarkFromInternet(target);
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (open != this) return;
                        if (ok) Say(I18n.Fmt("Adjunto guardado: {0}.", "Attachment saved: {0}.", Path.GetFileName(target)), false);
                        else Say(err ?? I18n.T("No he podido guardar el adjunto.", "Could not save attachment."), true);
                    }));
                }
                catch { }
            });
        }

        string AskAttachmentPath(string suggested)
        {
            Func<string, string> pick = PickPath;
            if (pick != null) return pick(suggested);
            try
            {
                using (System.Windows.Forms.SaveFileDialog d = new System.Windows.Forms.SaveFileDialog())
                {
                    string ext = Path.GetExtension(suggested);
                    d.Title = I18n.T("Guardar el adjunto", "Save attachment");
                    d.Filter = (ext.Length > 1 ? I18n.Fmt("Archivo {0}|*{1}|", "{0} file|*{1}|", ext.Substring(1).ToUpperInvariant(), ext) : "") + I18n.T("Todos los archivos|*.*", "All files|*.*");
                    d.FileName = suggested;
                    d.AddExtension = ext.Length > 1;
                    if (ext.Length > 1) d.DefaultExt = ext.Substring(1);
                    d.OverwritePrompt = true;
                    d.InitialDirectory = file != null && file.Path != null ? Path.GetDirectoryName(file.Path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK ? d.FileName : null;
                }
            }
            catch (Exception ex) { ShotStack.Log("Guardar adjunto: " + ex.Message); Say(I18n.T("No he podido abrir el di\u00E1logo de guardar.", "Could not open save dialog."), true); return null; }
        }
    }
}
