// Stackshot - What leaves the PDF viewer: pages (with their marks) as captures in the stack, as images on the clipboard
// or on disk, in the editor to annotate further, on paper, and the PDF itself with the marks written in.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Windows;
using WF = System.Windows.Forms;

namespace Stackshot
{
    public sealed partial class PdfWindow
    {
        const double CaptureScale = 2;      // pixels per DIP for captures and images: about 192 dpi
        const int MaxJoined = 30000;        // pixels: an image of several pages taller than this is refused

        string DocName { get { return System.IO.Path.GetFileNameWithoutExtension(path); } }

        List<PdfMark> MarksOn(int page)
        {
            List<PdfMark> o = new List<PdfMark>();
            if (marks != null) foreach (PdfMark m in marks.On(page)) o.Add(m.Clone());
            return o;
        }

        // A page drawn with its marks, k pixels per DIP (never more than 6000 on its longer side). Blocks: off the UI thread.
        internal static Bitmap PageImage(PdfEngine e, int page, List<PdfMark> marks, double k)
        {
            System.Windows.Size s = e.Sizes[page];
            k = Math.Min(k, 6000 / Math.Max(s.Width, s.Height));
            int w = Math.Max(1, (int)Math.Round(s.Width * k)), h = Math.Max(1, (int)Math.Round(s.Height * k));
            Bitmap b = e.RenderGdi(page, w, h, 120000);
            if (b == null) return null;
            PdfMarks.DrawAll(b, marks);
            return b;
        }

        // Runs work off the UI thread with the pages' marks taken now, then done on the UI thread.
        void Work(string doing, Func<object> work, Action<object> done)
        {
            if (engine == null) return;
            Say(doing, false, true);
            Cursor = System.Windows.Input.Cursors.AppStarting;
            int my = token;
            ThreadPool.QueueUserWorkItem(delegate
            {
                object r = null;
                try { r = work(); }
                catch (Exception ex) { ShotStack.Log("PDF: " + ex); r = ex; }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    Cursor = null;
                    if (my != token) { Dispose(r); return; }
                    CloseSay();
                    if (r is Exception) { Say("No se ha podido: " + ((Exception)r).Message, true); return; }
                    done(r);
                }));
            });
        }

        static void Dispose(object o)
        {
            IDisposable d = o as IDisposable;
            if (d != null) d.Dispose();
            List<Bitmap> l = o as List<Bitmap>;
            if (l != null) foreach (Bitmap b in l) if (b != null) b.Dispose();
        }

        // ---- Captures in the stack

        internal void CapturePages(List<int> pages, bool joined)
        {
            if (engine == null || pages == null || pages.Count == 0) return;
            if (pages.Count > 40 && !joined &&
                DocAlert.Ask(this, "\u00BFCapturar " + pages.Count + " p\u00E1ginas?", "Cada una ser\u00E1 una captura en la pila. Para tenerlas en disco, \u00ABGuardar p\u00E1ginas como imagen\u00BB las deja en una carpeta.", new string[] { "Capturar", "Cancelar" }, 0, 1) != 0) return;
            PdfEngine e = engine;
            Dictionary<int, List<PdfMark>> on = new Dictionary<int, List<PdfMark>>();
            foreach (int p in pages) on[p] = MarksOn(p);
            string name = DocName;
            Work(pages.Count == 1 ? "Capturando la p\u00E1gina\u2026" : "Capturando " + pages.Count + " p\u00E1ginas\u2026", delegate
            {
                List<Bitmap> shots = new List<Bitmap>();
                foreach (int p in pages) shots.Add(PageImage(e, p, on[p], CaptureScale));
                if (!joined || shots.Count < 2) return shots;
                Bitmap one = Join(shots);
                foreach (Bitmap b in shots) if (b != null) b.Dispose();
                return new List<Bitmap> { one };
            }, delegate(object r)
            {
                List<Bitmap> shots = (List<Bitmap>)r;
                if (shots.Count == 1 && shots[0] == null) { Say(joined ? "Son demasiadas p\u00E1ginas para una sola imagen: captura menos o por separado." : "No se ha podido dibujar la p\u00E1gina.", true); return; }
                int made = 0;
                for (int i = 0; i < shots.Count; i++)
                {
                    if (shots[i] == null) continue;
                    string label = joined && pages.Count > 1 ? name + " p\u00E1gs. " + (pages[0] + 1) + "-" + (pages[pages.Count - 1] + 1) : name + " p\u00E1g. " + (pages[i] + 1);
                    string saved = shots.Count == 1 ? owner.SaveCapture(shots[i], label) : owner.SaveCaptureQuiet(shots[i], label);
                    if (saved != null) made++;
                }
                if (shots.Count > 1) Shutter.Play();
                Say(made == 0 ? "No se ha podido a\u00F1adir a la pila." : made == 1 ? "Captura en la pila" + (shots.Count == 1 ? " y copiada" : "") : made + " capturas en la pila", made == 0);
            });
        }

        // Pages one under the other on white, with a hairline between them (a single image to share a few pages).
        static Bitmap Join(List<Bitmap> shots)
        {
            int w = 0, h = 0, gap = 24;
            foreach (Bitmap b in shots) { if (b == null) continue; w = Math.Max(w, b.Width); h += b.Height + gap; }
            h -= gap;
            if (w < 1 || h < 1 || h > MaxJoined) return null;
            Bitmap o = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(o))
            using (System.Drawing.Pen line = new System.Drawing.Pen(System.Drawing.Color.FromArgb(225, 225, 228), 2))
            {
                g.Clear(System.Drawing.Color.White);
                int y = 0;
                foreach (Bitmap b in shots)
                {
                    if (b == null) continue;
                    g.DrawImageUnscaled(b, (w - b.Width) / 2, y);
                    y += b.Height;
                    if (y < h) g.DrawLine(line, 0, y + gap / 2, w, y + gap / 2);
                    y += gap;
                }
            }
            return o;
        }

        void CaptureDialog()
        {
            if (engine == null) return;
            bool joined;
            List<int> pages = PdfPagesDialog.Ask(this, "Capturar p\u00E1ginas", "Cada p\u00E1gina, con sus marcas, va a la pila como una captura m\u00E1s: lista para pegar, arrastrar o editar.", "Capturar",
                                                 engine.Pages, view.CurrentPage, PickedPages(), true, out joined);
            if (pages != null) CapturePages(pages, joined);
        }

        // ---- Images

        internal void CopyPageImage(int page)
        {
            PdfEngine e = engine;
            List<PdfMark> on = MarksOn(page);
            Work("Copiando la p\u00E1gina\u2026", delegate { return PageImage(e, page, on, CaptureScale); }, delegate(object r)
            {
                using (Bitmap b = (Bitmap)r)
                {
                    if (b == null) { Say("No se ha podido dibujar la p\u00E1gina.", true); return; }
                    if (PutImage(b, null)) Say("P\u00E1gina " + (page + 1) + " copiada como imagen", false);
                }
            });
        }

        // Image (and text, when there is) on the clipboard, never synced to other devices, as Stackshot's captures.
        bool PutImage(Bitmap b, string text)
        {
            System.Windows.Media.Imaging.BitmapSource img = Ink.FromGdi(b);
            DataObject d = new DataObject();
            d.SetImage(img);
            if (!string.IsNullOrEmpty(text)) d.SetText(text);
            d.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
            return ToClipboard(delegate { Clipboard.SetDataObject(d, true); });
        }

        internal void Annotate(int page)
        {
            PdfEngine e = engine;
            List<PdfMark> on = MarksOn(page);
            string label = DocName + " p\u00E1g. " + (page + 1);
            Work("Abriendo la p\u00E1gina en el editor\u2026", delegate { return PageImage(e, page, on, CaptureScale); }, delegate(object r)
            {
                Bitmap b = (Bitmap)r;
                if (b == null) { Say("No se ha podido dibujar la p\u00E1gina.", true); return; }
                string saved = owner.SaveCaptureQuiet(b, label);
                if (saved == null) { Say("No se ha podido a\u00F1adir a la pila.", true); return; }
                owner.OpenEditor(saved);
            });
        }

        void SaveDialog()
        {
            if (engine == null) return;
            bool joined;
            List<int> pages = PdfPagesDialog.Ask(this, "Guardar p\u00E1ginas como imagen", "En PNG, con sus marcas, n\u00EDtidas para presentar o imprimir.", "Elegir carpeta\u2026",
                                                 engine.Pages, view.CurrentPage, PickedPages(), false, out joined);
            if (pages != null) SavePngs(pages);
        }

        internal void SavePngs(List<int> pages)
        {
            if (engine == null || pages == null || pages.Count == 0) return;
            string target;
            if (pages.Count == 1)
            {
                string suggested = DocName + " - p\u00E1gina " + (pages[0] + 1) + ".png";
                if (PickPath != null) target = PickPath(suggested);
                else
                    using (WF.SaveFileDialog d = new WF.SaveFileDialog { Title = "Guardar la p\u00E1gina como imagen", Filter = "PNG|*.png", FileName = suggested, InitialDirectory = System.IO.Path.GetDirectoryName(path) })
                        target = d.ShowDialog() == WF.DialogResult.OK ? d.FileName : null;
            }
            else
            {
                if (PickPath != null) target = PickPath("");
                else
                    using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog { Description = "\u00BFD\u00F3nde guardo las " + pages.Count + " p\u00E1ginas?", ShowNewFolderButton = true, SelectedPath = System.IO.Path.GetDirectoryName(path) })
                        target = d.ShowDialog() == WF.DialogResult.OK ? d.SelectedPath : null;
            }
            if (target == null) return;
            PdfEngine e = engine;
            Dictionary<int, List<PdfMark>> on = new Dictionary<int, List<PdfMark>>();
            foreach (int p in pages) on[p] = MarksOn(p);
            string name = DocName;
            int digits = Math.Max(2, e.Pages.ToString().Length);
            Work("Guardando " + (pages.Count == 1 ? "la p\u00E1gina" : pages.Count + " p\u00E1ginas") + "\u2026", delegate
            {
                int saved = 0;
                foreach (int p in pages)
                {
                    string file = pages.Count == 1 ? target : System.IO.Path.Combine(target, Safe(name) + " - p\u00E1gina " + (p + 1).ToString("D" + digits) + ".png");
                    using (Bitmap b = PageImage(e, p, on[p], CaptureScale))
                    {
                        if (b == null) continue;
                        AtomicFile.Write(file, delegate(Stream s) { b.Save(s, System.Drawing.Imaging.ImageFormat.Png); });
                        saved++;
                    }
                }
                return saved;
            }, delegate(object r)
            {
                int saved = (int)r;
                string where = pages.Count == 1 ? System.IO.Path.GetFileName(target) : target;
                if (saved == 0) { Say("No se ha guardado ninguna p\u00E1gina.", true); return; }
                owner.Notify(saved == 1 ? "P\u00E1gina guardada" : saved + " p\u00E1ginas guardadas", where, pages.Count == 1 ? System.IO.Path.GetDirectoryName(target) : target);
            });
        }

        static string Safe(string s)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        // ---- A zone of a page

        // The zone as an image: the page drawn sharp enough that the zone alone is about twice its size on screen.
        Bitmap ZoneImage(PdfEngine e, int page, Rect zone, List<PdfMark> on)
        {
            System.Windows.Size s = e.Sizes[page];
            double k = Math.Max(CaptureScale, Math.Min(6, 1400 / Math.Max(1, Math.Max(zone.Width * s.Width, zone.Height * s.Height))));
            using (Bitmap full = PageImage(e, page, on, k))
            {
                if (full == null) return null;
                Rectangle r = Rectangle.Intersect(new Rectangle((int)(zone.X * full.Width), (int)(zone.Y * full.Height), Math.Max(1, (int)(zone.Width * full.Width)), Math.Max(1, (int)(zone.Height * full.Height))), new Rectangle(0, 0, full.Width, full.Height));
                if (r.Width < 1 || r.Height < 1) return null;
                return full.Clone(r, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            }
        }

        void ZoneWork(string doing, Action<Bitmap, string> done)
        {
            if (!view.HasZone) return;
            PdfEngine e = engine;
            int page = view.zonePage;
            Rect zone = view.zone;
            List<PdfMark> on = MarksOn(page);
            string text = view.SelectedText;
            Work(doing, delegate { return ZoneImage(e, page, zone, on); }, delegate(object r)
            {
                Bitmap b = (Bitmap)r;
                if (b == null) { Say("No se ha podido dibujar la zona.", true); return; }
                done(b, text);
            });
        }

        void CaptureZone()
        {
            int page = view.zonePage;
            ZoneWork("Capturando la zona\u2026", delegate(Bitmap b, string t)
            {
                if (owner.SaveCapture(b, DocName + " p\u00E1g. " + (page + 1)) == null) Say("No se ha podido a\u00F1adir a la pila.", true);
                else { Say("Zona en la pila", false); view.ClearSelection(true); }
            });
        }

        void CopyZone()
        {
            ZoneWork("Copiando la zona\u2026", delegate(Bitmap b, string t)
            {
                using (b) if (PutImage(b, t)) Say(t.Length > 0 ? "Zona copiada (imagen y texto)" : "Zona copiada como imagen", false);
            });
        }

        void AnnotateZone()
        {
            int page = view.zonePage;
            ZoneWork("Abriendo la zona en el editor\u2026", delegate(Bitmap b, string t)
            {
                string saved = owner.SaveCaptureQuiet(b, DocName + " p\u00E1g. " + (page + 1));
                if (saved != null) { view.ClearSelection(true); owner.OpenEditor(saved); }
            });
        }

        // ---- Print

        internal void Print() { Print(null); }

        // Windows' print dialog (its own page range); each page, with its marks, fitted to the paper and turned to it.
        internal void Print(List<int> only)
        {
            if (engine == null) return;
            PrintDocument doc = new PrintDocument { DocumentName = System.IO.Path.GetFileName(path) };
            doc.PrinterSettings.MinimumPage = 1;
            doc.PrinterSettings.MaximumPage = engine.Pages;
            doc.PrinterSettings.FromPage = only != null && only.Count > 0 ? only[0] + 1 : 1;
            doc.PrinterSettings.ToPage = only != null && only.Count > 0 ? only[only.Count - 1] + 1 : engine.Pages;
            using (WF.PrintDialog d = new WF.PrintDialog { Document = doc, AllowSomePages = true, AllowCurrentPage = true, UseEXDialog = true })
            {
                if (only != null) d.PrinterSettings.PrintRange = PrintRange.SomePages;
                if (d.ShowDialog(Win32) != WF.DialogResult.OK) return;
            }
            List<int> pages = new List<int>();
            PrinterSettings ps = doc.PrinterSettings;
            if (ps.PrintRange == PrintRange.SomePages) for (int p = Math.Max(1, ps.FromPage); p <= Math.Min(engine.Pages, ps.ToPage); p++) pages.Add(p - 1);
            else if (ps.PrintRange == PrintRange.CurrentPage) pages.Add(view.CurrentPage);
            else if (only != null && only.Count > 0) pages.AddRange(only);
            else for (int p = 0; p < engine.Pages; p++) pages.Add(p);
            if (pages.Count == 0) return;
            PdfEngine e = engine;
            Dictionary<int, List<PdfMark>> on = new Dictionary<int, List<PdfMark>>();
            foreach (int p in pages) on[p] = MarksOn(p);
            int at = 0;
            doc.PrintController = new StandardPrintController();
            doc.PrintPage += delegate(object o, PrintPageEventArgs a)
            {
                int page = pages[at];
                int dpi = Math.Min(300, Math.Max(150, a.PageSettings.PrinterResolution.X > 0 ? a.PageSettings.PrinterResolution.X : 300));
                System.Windows.Size s = e.Sizes[page];
                using (Bitmap b = PageImage(e, page, on[page], dpi / 96.0))
                {
                    if (b != null)
                    {
                        RectangleF area = a.MarginBounds;
                        // A page that lies the other way from the paper is turned to fill it, as Preview does.
                        bool turn = (s.Width > s.Height) != (area.Width > area.Height) && Math.Abs(s.Width - s.Height) > 1;
                        if (turn) b.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        double k = Math.Min(area.Width / b.Width, area.Height / b.Height);
                        float w = (float)(b.Width * k), h = (float)(b.Height * k);
                        a.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        a.Graphics.DrawImage(b, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                    }
                }
                at++;
                int n = at;
                Dispatcher.BeginInvoke(new Action(delegate { Say("Imprimiendo\u2026 " + n + " de " + pages.Count, false, true); }));
                a.HasMorePages = at < pages.Count;
            };
            Say("Imprimiendo\u2026", false, true);
            Thread t = new Thread(delegate()
            {
                string error = null;
                try { doc.Print(); }
                catch (Exception ex) { error = ex.Message; ShotStack.Log("PDF, imprimir: " + ex); }
                finally { doc.Dispose(); }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (error != null) Say("No se ha podido imprimir: " + error, true);
                    else Say(pages.Count == 1 ? "P\u00E1gina enviada a la impresora" : pages.Count + " p\u00E1ginas enviadas a la impresora", false);
                }));
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Name = "Stackshot: imprimir PDF";
            t.Start();
        }

        // ---- The PDF with the marks

        // A copy of the PDF with the marks as real annotations (any viewer shows them and Acrobat edits them).
        internal void ExportPdf()
        {
            if (engine == null || marks == null || marks.Mine == 0) { Say("Todav\u00EDa no hay marcas que exportar.", false); return; }
            string suggested = DocName + " (con marcas).pdf", target;
            if (PickPath != null) target = PickPath(suggested);
            else
                using (WF.SaveFileDialog d = new WF.SaveFileDialog { Title = "Exportar PDF con las marcas", Filter = "PDF|*.pdf", FileName = suggested, InitialDirectory = System.IO.Path.GetDirectoryName(path) })
                    target = d.ShowDialog(Win32) == WF.DialogResult.OK ? d.FileName : null;
            if (target == null) return;
            if (string.Equals(System.IO.Path.GetFullPath(target), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) { SaveIntoPdf(); return; }
            WriteMarks(target, false);
        }

        // The marks into this very file: they become part of the PDF (its own annotations) and leave Stackshot's list.
        internal void SaveIntoPdf()
        {
            if (engine == null || marks == null || marks.Mine == 0) { Say("Todav\u00EDa no hay marcas que guardar.", false); return; }
            if (DocAlert.Ask(this, "\u00BFGuardar las marcas dentro de \u00AB" + System.IO.Path.GetFileName(path) + "\u00BB?",
                             "Pasan a ser parte del PDF: cualquier visor las ver\u00E1 y Acrobat podr\u00E1 editarlas. Aqu\u00ED ya no se podr\u00E1n cambiar. El contenido del PDF no se toca: solo se a\u00F1aden al final.",
                             new string[] { "Guardar en el PDF", "Cancelar" }, 0, 1) != 0) return;
            WriteMarks(path, true);
        }

        void WriteMarks(string target, bool inPlace)
        {
            List<PdfMark> all = new List<PdfMark>();
            foreach (PdfMark m in marks.All) if (!m.InFile) all.Add(m.Clone());
            string source = path, author = Environment.UserName;
            ignoreDisk = inPlace;
            Work(inPlace ? "Guardando las marcas en el PDF\u2026" : "Exportando el PDF\u2026", delegate
            {
                return PdfAnnotWriter.WriteVerified(source, target, all, author);
            }, delegate(object r)
            {
                string error = (string)r;
                if (error != null) { ignoreDisk = false; Say(error, true); return; }
                if (!inPlace)
                {
                    marks.SinceExport = false;
                    owner.Notify("PDF exportado con las marcas", System.IO.Path.GetFileName(target), System.IO.Path.GetDirectoryName(target));
                    return;
                }
                // The marks are in the file now: shown by the page drawing itself, so they leave the list.
                marks.Clear();
                SaveMarks();
                Say("Marcas guardadas dentro del PDF", false);
                Dispatcher.BeginInvoke(new Action(delegate { ignoreDisk = false; Reload(); }));
            });
        }
    }
}
