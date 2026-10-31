// Stackshot - Exportar PDF for Markdown documents: the PDF is laid out and written on a thread of its own (with its own
// WPF dispatcher), the page count in the status bar meanwhile; closing the window stops it and leaves no file behind.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Threading;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        bool pdfBusy, pdfHooked;
        volatile bool pdfStop;
        int pdfToken, pdfPages;
        string pdfError;

        internal bool PdfBusy { get { return pdfBusy; } }
        // The last export: pages written (0 when it failed) and why it failed.
        internal int PdfPages { get { return pdfPages; } }
        internal string PdfError { get { return pdfError; } }

        MenuEntry PdfEntry()
        {
            MenuEntry e = MenuEntry.Item(pdfBusy ? I18n.T("Exportando PDF\u2026", "Exporting PDF\u2026") : I18n.T("Exportar PDF\u2026", "Export PDF\u2026"), "doc", false, delegate { ExportPdf(); });
            e.Enabled = !pdfBusy;
            return e;
        }

        // Asks where (next to the document by default) and starts the export; false when nothing was started.
        internal bool ExportPdf()
        {
            if (xml || Current() == null) return false;
            if (pdfBusy) { Say(I18n.T("Ya estoy exportando un PDF\u2026", "Already exporting a PDF\u2026"), false); return false; }
            string baseName = name != null ? System.IO.Path.GetFileNameWithoutExtension(name) : Safe(MarkdownTools.Title(text) ?? "");
            if (baseName.Length == 0) baseName = "documento";
            string target = null;
            Func<string, string> pick = PickPath;
            if (pick != null) target = pick(baseName + ".pdf");
            else
            {
                try
                {
                    using (System.Windows.Forms.SaveFileDialog d = new System.Windows.Forms.SaveFileDialog())
                    {
                        d.Title = I18n.T("Exportar PDF", "Export PDF");
                        d.Filter = I18n.T("Documento PDF|*.pdf|Todos los archivos|*.*", "PDF document|*.pdf|All files|*.*");
                        d.FileName = baseName + ".pdf";
                        d.DefaultExt = "pdf";
                        d.AddExtension = true;
                        d.OverwritePrompt = true;
                        d.InitialDirectory = DocumentFolder() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK) target = d.FileName;
                    }
                }
                catch (Exception ex) { ShotStack.Log("Exportar PDF: " + ex.Message); Say(I18n.T("No he podido abrir el di\u00E1logo de guardar.", "Could not open save dialog."), true); }
            }
            if (target == null) return false;
            string snap = text, dir = DocumentFolder(), title = MarkdownTools.Title(text) ?? baseName, path = target;
            int my = ++pdfToken;
            pdfBusy = true;
            pdfStop = false;
            pdfPages = 0;
            pdfError = null;
            if (!pdfHooked) { pdfHooked = true; Closed += delegate { pdfStop = true; }; }
            SayLasting(I18n.T("Exportando PDF\u2026", "Exporting PDF\u2026"));
            Thread t = new Thread(delegate()
            {
                int pages = 0;
                string err = null;
                bool stopped = false;
                try
                {
                    pages = MdPdf.Export(snap, dir, path, title, delegate(int n)
                    {
                        try { Dispatcher.BeginInvoke(new Action(delegate { if (my == pdfToken && pdfBusy) SayLasting(I18n.Fmt("Exportando PDF\u2026 p\u00E1gina {0}", "Exporting PDF\u2026 page {0}", n)); })); }
                        catch (Exception) { }
                    }, delegate { return pdfStop; });
                }
                catch (OperationCanceledException) { stopped = true; }
                catch (Exception ex)
                {
                    ShotStack.Log("Exportar PDF: " + ex);
                    err = ex is UnauthorizedAccessException || ex is System.IO.IOException ? I18n.T("No he podido guardar el PDF: ", "Could not save PDF: ") + ex.Message : I18n.T("No he podido exportar el PDF.", "Could not export PDF.");
                }
                finally
                {
                    try { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); } catch (Exception) { }
                }
                try { Dispatcher.BeginInvoke(new Action(delegate { PdfExported(my, path, stopped ? -1 : pages, err); })); }
                catch (Exception) { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Name = "Stackshot PDF";
            t.Start();
            return true;
        }

        // A status that stays until the next one (the export can take a few seconds).
        void SayLasting(string msg)
        {
            fade.Stop();
            status = msg;
            statusBad = false;
            Stats();
        }

        void PdfExported(int my, string path, int pages, string err)
        {
            if (my != pdfToken) return;
            pdfBusy = false;
            if (pages < 0) { pdfError = I18n.T("Cancelado.", "Cancelled."); return; }
            if (err != null || pages <= 0)
            {
                pdfError = err ?? I18n.T("No he podido exportar el PDF.", "Could not export PDF.");
                Say(pdfError, true);
                return;
            }
            pdfPages = pages;
            Say(I18n.Fmt(pages == 1 ? "PDF exportado: {0} ({1} p\u00E1gina)." : "PDF exportado: {0} ({1} p\u00E1ginas).", pages == 1 ? "PDF exported: {0} ({1} page)." : "PDF exported: {0} ({1} pages).", System.IO.Path.GetFileName(path), pages.ToString("N0")), false);
        }
    }
}
