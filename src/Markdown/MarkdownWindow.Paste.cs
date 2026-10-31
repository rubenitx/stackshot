// Stackshot - Pasting and dropping into the Markdown editor: pictures saved in a folder next to the document, tables from a
// spreadsheet as Markdown tables, plain text with Ctrl+Shift+V; and image links that keep working in the HTML exports.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        // Tests hand the editor's paste a data object here instead of the clipboard.
        internal static Func<IDataObject> PasteSource { get; set; }

        // The document's folder (null while it has never been saved).
        string DocumentFolder()
        {
            if (file == null || file.Path == null) return null;
            try { return System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file.Path)); }
            catch (Exception) { return null; }
        }

        // A new editor for a Markdown document: its paste comes here first, its caret moves the outline.
        void HookMarkdownEditor(MarkdownEditor ed)
        {
            if (ed.IsXml) return;
            ed.SmartPaste = EditorPaste;
            OutlineHook(ed);
        }

        // Ctrl+V (plain: Ctrl+Shift+V) in the Markdown editor. True when handled here; false lets the editor paste the
        // text as always.
        internal bool EditorPaste(bool plain)
        {
            if (xml || editor == null || text == null) return false;
            IDataObject d = null;
            try
            {
                Func<IDataObject> src = PasteSource;
                if (src != null) d = src();
                else if (!Retry(delegate { d = Clipboard.GetDataObject(); })) { Say(I18n.T("No he podido usar el portapapeles.", "Could not access clipboard."), true); return true; }
            }
            catch (Exception ex) { ShotStack.Log("Markdown pegar: " + ex.Message); Say(I18n.T("No he podido leer el portapapeles.", "Could not read clipboard."), true); return true; }
            if (d == null) return plain;
            string t = ClipboardText(d);
            if (plain)
            {
                if (t != null) InsertAtCaret(MarkdownTools.Normalize(t));
                else Say(I18n.T("En el portapapeles no hay texto.", "No text on clipboard."), false);
                return true;
            }
            if (t != null)
            {
                MdTablePaste.Sheet sheet = null;
                try { sheet = MdTablePaste.Read(d); }
                catch (Exception ex) { ShotStack.Log("Pegar tabla: " + ex.Message); }
                if (sheet == null) return false;
                PasteSheet(sheet);
                return true;
            }
            byte[] png;
            BitmapSource bmp;
            if (ClipboardImage(d, out png, out bmp)) { AddPastedImage(png, bmp); return true; }
            List<string> files = DroppedImageFiles(d);
            if (files.Count > 0) { AddImageFiles(files, -1); return true; }
            return false;
        }

        // A table from a spreadsheet at the caret. A big one is converted off the UI thread and goes where the caret is
        // when it is ready.
        void PasteSheet(MdTablePaste.Sheet sheet)
        {
            int rows, cols;
            if (sheet.Text.Length + (sheet.Html == null ? 0 : sheet.Html.Length) < BigSheet)
            {
                string table = MdTablePaste.Convert(sheet, out rows, out cols);
                if (table == null) { Say(I18n.T("No he podido pegar la tabla.", "Could not paste table."), true); return; }
                InsertOnOwnLines(table);
                Say(I18n.Fmt("Tabla pegada ({0} \u00D7 {1}).", "Table pasted ({0} \u00D7 {1}).", rows, cols), false);
                return;
            }
            int my = token;
            SayLasting(I18n.T("Pegando la tabla\u2026", "Pasting table\u2026"));
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string table = null;
                int r = 0, c = 0;
                try { table = MdTablePaste.Convert(sheet, out r, out c); }
                catch (Exception ex) { ShotStack.Log("Pegar tabla: " + ex.Message); }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != token || editor == null) return;
                        if (table == null) { Say(I18n.T("No he podido pegar la tabla.", "Could not paste table."), true); return; }
                        InsertOnOwnLines(table);
                        Say(I18n.Fmt("Tabla pegada ({0} \u00D7 {1}).", "Table pasted ({0} \u00D7 {1}).", r, c), false);
                    }));
                }
                catch (Exception ex) { ShotStack.Log("Pegar tabla: " + ex.Message); }
            });
        }

        // Characters of clipboard data from which a pasted table is converted off the UI thread.
        internal const int BigSheet = 256 * 1024;

        static string ClipboardText(IDataObject d)
        {
            try
            {
                string t = d.GetDataPresent(DataFormats.UnicodeText) ? d.GetData(DataFormats.UnicodeText) as string : null;
                if (t == null && d.GetDataPresent(DataFormats.Text)) t = d.GetData(DataFormats.Text) as string;
                return t;
            }
            catch (Exception ex) { ShotStack.Log("Markdown pegar texto: " + ex.Message); return null; }
        }

        // The clipboard's picture: its own PNG bytes when it has them (they keep transparency), else the bitmap, frozen
        // so it can be encoded off the UI thread.
        static bool ClipboardImage(IDataObject d, out byte[] png, out BitmapSource bmp)
        {
            png = null;
            bmp = null;
            foreach (string f in new string[] { "PNG", "image/png" })
            {
                try
                {
                    Stream s = d.GetDataPresent(f) ? d.GetData(f) as Stream : null;
                    if (s == null) continue;
                    byte[] b;
                    using (MemoryStream ms = new MemoryStream()) { if (s.CanSeek) s.Position = 0; s.CopyTo(ms); b = ms.ToArray(); }
                    if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) { png = b; return true; }
                }
                catch (Exception ex) { ShotStack.Log("Pegar imagen PNG: " + ex.Message); }
            }
            try
            {
                BitmapSource b = d.GetDataPresent(DataFormats.Bitmap) ? d.GetData(DataFormats.Bitmap) as BitmapSource : null;
                if (b == null || b.PixelWidth <= 0 || b.PixelHeight <= 0) return false;
                if (!b.IsFrozen)
                {
                    if (b.CanFreeze) b.Freeze();
                    else { WriteableBitmap w = new WriteableBitmap(b); w.Freeze(); b = w; }
                }
                if (b.Format != PixelFormats.Bgra32) { FormatConvertedBitmap c = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0); c.Freeze(); b = c; }
                bmp = b;
                return true;
            }
            catch (Exception ex) { ShotStack.Log("Pegar imagen: " + ex.Message); return false; }
        }

        // Image files in a drop or a copy from Explorer, when every file is one.
        static List<string> DroppedImageFiles(IDataObject d)
        {
            List<string> o = new List<string>();
            try
            {
                string[] fd = d.GetDataPresent(DataFormats.FileDrop) ? d.GetData(DataFormats.FileDrop) as string[] : null;
                if (fd == null) return o;
                foreach (string f in fd)
                {
                    if (!MdImages.IsImageFile(f)) return new List<string>();
                    o.Add(f);
                }
            }
            catch (Exception ex) { ShotStack.Log("Markdown archivos: " + ex.Message); }
            return o;
        }

        // A picture needs a folder: an unsaved document is saved first, if the user agrees. True when it has one.
        bool FolderForImages(string title)
        {
            if (DocumentFolder() != null) return true;
            int r = DocAlert.Ask(this, title, I18n.Fmt("Las im\u00E1genes se guardan en la carpeta \u00AB{0}\u00BB, junto al documento.", "Images are saved in the \u201C{0}\u201D folder, next to the document.", MdImages.Folder),
                                 new string[] { I18n.T("Guardar como\u2026", "Save as\u2026"), I18n.T("Cancelar", "Cancel") }, 0, 1);
            return r == 0 && Save(true) && DocumentFolder() != null;
        }

        void AddPastedImage(byte[] png, BitmapSource bmp)
        {
            if (!FolderForImages(I18n.T("Guarda el documento antes de pegar la imagen", "Save the document before pasting image"))) return;
            string dir = DocumentFolder();
            int my = token;
            DateTime when = DateTime.Now;
            Say(I18n.T("Guardando la imagen\u2026", "Saving image\u2026"), false);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err;
                string rel = MdImages.SavePasted(dir, png, bmp, when, out err);
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != token || editor == null) return;
                        if (rel == null) { Say(err, true); return; }
                        InsertAtCaret(MdImages.Link(rel));
                        Say(I18n.Fmt("Imagen pegada: {0}.", "Image pasted: {0}.", rel), false);
                    }));
                }
                catch (Exception ex) { ShotStack.Log("Pegar imagen: " + ex.Message); }
            });
        }

        // Dropped image files: links where they fell (copies in imagenes for files from outside the document's folder).
        void AddImageFiles(List<string> files, int at)
        {
            if (!FolderForImages(files.Count == 1 ? I18n.T("Guarda el documento antes de a\u00F1adir la imagen", "Save the document before adding image") : I18n.T("Guarda el documento antes de a\u00F1adir las im\u00E1genes", "Save the document before adding images"))) return;
            string dir = DocumentFolder();
            int my = token;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> rels = new List<string>();
                string err = null;
                foreach (string f in files)
                {
                    string e;
                    string rel = MdImages.Adopt(dir, f, out e);
                    if (rel != null) rels.Add(rel); else err = e;
                }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != token || editor == null) return;
                        if (rels.Count == 0) { Say(err ?? I18n.T("No he podido a\u00F1adir la imagen.", "Could not add image."), true); return; }
                        List<string> links = new List<string>();
                        foreach (string r in rels) links.Add(MdImages.Link(r));
                        string ins = string.Join("\n\n", links.ToArray());
                        if (at >= 0 && at <= editor.Text.Length) editor.Apply(TextEdit.Make(at, 0, ins, at + ins.Length, 0));
                        else InsertAtCaret(ins);
                        if (err != null) Say(err, true);
                        else Say(rels.Count == 1 ? I18n.Fmt("Imagen a\u00F1adida: {0}.", "Image added: {0}.", rels[0]) : I18n.Fmt("{0} im\u00E1genes a\u00F1adidas.", "{0} images added.", rels.Count), false);
                    }));
                }
                catch (Exception ex) { ShotStack.Log("Soltar imagen: " + ex.Message); }
            });
        }

        // Image files dropped while editing a Markdown document; false leaves the drop to OnDrop (it opens files).
        bool DropImageFiles(DragEventArgs e)
        {
            if (xml || text == null || !editing || editor == null) return false;
            List<string> files = DroppedImageFiles(e.Data);
            if (files.Count == 0) return false;
            e.Handled = true;
            int at = -1;
            try
            {
                Point p = e.GetPosition(editor.Box);
                if (p.X >= 0 && p.Y >= 0 && p.X <= editor.Box.ActualWidth && p.Y <= editor.Box.ActualHeight) at = editor.Box.GetCharacterIndexFromPoint(p, true);
            }
            catch (Exception ex) { ShotStack.Log("Soltar imagen: " + ex.Message); }
            // After the drop returns: an alert must not keep Explorer waiting in the middle of the drag.
            Dispatcher.BeginInvoke(new Action(delegate { if (editor != null) AddImageFiles(files, at); }));
            return true;
        }

        // Text at the caret (replacing the selection) as one undo step.
        void InsertAtCaret(string s)
        {
            int a = editor.Box.SelectionStart, l = editor.Box.SelectionLength;
            editor.Apply(TextEdit.Make(a, l, s, a + s.Length, 0));
        }

        // A block on lines of its own, with a blank line before and after it.
        void InsertOnOwnLines(string block)
        {
            string t = editor.Text;
            int s = editor.Box.SelectionStart, l = editor.Box.SelectionLength, e = s + l;
            string before = s == 0 ? "" : t[s - 1] != '\n' ? "\n\n" : s >= 2 && t[s - 2] != '\n' ? "\n" : "";
            string after = e >= t.Length ? "\n" : t[e] != '\n' ? "\n\n" : e + 1 < t.Length && t[e + 1] != '\n' ? "\n" : "";
            string ins = before + block + after;
            editor.Apply(TextEdit.Make(s, l, ins, s + ins.Length, 0));
        }

        // Where an image's link points in an HTML copy: relative to the page when it is written to a file (target), as an
        // absolute file address in the clipboard. Web addresses stay; other files and schemes are left as text.
        Func<string, string> HtmlImages(string target)
        {
            string dir = DocumentFolder(), to = null;
            if (target != null) { try { to = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target)); } catch (Exception) { to = null; } }
            return delegate(string url)
            {
                string local = MdImages.LocalPath(url, dir);
                if (local == null) return MdHtml.Source(url);
                if (!MdImages.IsImageFile(local)) return null;
                try
                {
                    if (to == null) return new Uri(local).AbsoluteUri;
                    Uri rel = new Uri(to.TrimEnd('\\') + "\\").MakeRelativeUri(new Uri(local));
                    return rel.IsAbsoluteUri ? rel.AbsoluteUri : rel.OriginalString;
                }
                catch (UriFormatException) { return null; }
            };
        }
    }
}
