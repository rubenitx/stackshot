// Stackshot - The stack's side of covering personal data in new captures (Settings.SensitiveAuto, off unless turned on).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();

        // Whether new captures have their personal data covered before anything shows them.
        public static bool CoversCaptures(Settings s) { return s != null && s.SensitiveAuto; }

        // SaveCapture's step for SensitiveAuto. With it on, the capture is read and covered before anything shows it: the
        // card starts with a solid placeholder, the clipboard is emptied instead of getting the capture (a paste meanwhile pastes nothing,
        // never the data), and a worker thread takes the capture over to read it, cover it, write it, copy it and show it.
        // The caller must not touch shot afterwards. Off: returns false and does nothing.
        bool CoverBeforeShowing(Bitmap shot, string path, ref Bitmap preview, ref Bitmap forClipboard)
        {
            if (!CoversCaptures(settings)) return false;
            CoverDiscarded = false;
            // Anything that needs the file (drag, editor, copy, save) waits for the covered one.
            lock (writing) writing.Add(path);
            CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref forClipboard);
            try
            {
                bool wantCopy = job.WantCopy;
                uint seq = 0;
                if (wantCopy)
                {
                    try { Clipboard.Clear(); }
                    catch (Exception ex) { Log("Portapapeles: " + ex.Message); }
                    seq = GetClipboardSequenceNumber();
                }
                job.Write = delegate(Bitmap img) { BeginWrite(path, img, delegate(Exception ex) { Ui(delegate { CoverWriteFailed(path, ex); }); }); };
                job.Done = delegate(CoverOutcome r)
                {
                    if (r.Error != null) DoneWriting(path);
                    if (exiting) { Discard(r); return; }
                    try { sync.BeginInvoke((Action)delegate { Covered(path, r, wantCopy, seq); }); }
                    catch (Exception ex) { Log("Ui: " + ex.Message); Discard(r); }
                };
                // Runs after SaveCapture has added the card.
                Ui(delegate
                {
                    Card c = Find(path);
                    if (c != null) c.CoverNote(I18n.T("Revisando datos\u2026", "Checking data\u2026"));
                });
                job.Start();
            }
            catch (Exception ex)
            {
                // The capture is the job's already: a failure handing it over is fail-closed too (never shown, copied or
                // written), with everything the job held let go.
                Log("Datos sensibles: " + ex.Message);
                job.Abort();
                lock (writing) writing.Remove(path);
                CoverDiscarded = true;
                DiscardCaptureNotice();
            }
            return true;
        }

        // Set when the covering step itself had to discard the capture (it could not even be handed over): SaveCapture
        // must not add a card for it.
        internal bool CoverDiscarded;

        // The fail-closed notice when a capture cannot be covered at all.
        void DiscardCaptureNotice()
        {
            Notify(I18n.T("No se pudo revisar la captura", "The capture could not be checked", "Aufnahme nicht gepr\u00FCft"), DiscardBody(), NoticeKind.Error, null, null);
        }

        internal static string DiscardBody()
        {
            return I18n.T("No se pudieron tapar sus datos sensibles, as\u00ED que se ha descartado: nunca se muestra ni se guarda sin revisar.", "Its sensitive data could not be covered, so it was discarded: it is never shown or saved unchecked.", "Ihre sensiblen Daten konnten nicht geschw\u00E4rzt werden, deshalb wurde sie verworfen: Sie wird nie ungepr\u00FCft gezeigt oder gespeichert.");
        }

        static void Discard(CoverOutcome r)
        {
            if (r.Preview != null) r.Preview.Dispose();
            if (r.Copy != null) r.Copy.Dispose();
        }

        // Said already in this session: the warning about captures blocked without OCR shows once, not per capture.
        internal static bool OcrMissingWarned;

        // One clear warning per session while covering is on (by choice or by policy) and this Windows has no OCR: the
        // captures are discarded fail-closed, and here is why.
        void WarnOcrMissing()
        {
            if (OcrMissingWarned) return;
            OcrMissingWarned = true;
            // The switch is named by its own setting title, so the quoted words are the ones the user sees in whatever
            // language (the German one comes from the same catalog key, never retyped here).
            string title = PolicyTitles.Sensitive;
            Notify(I18n.T("Capturas descartadas: falta el OCR", "Captures discarded: no OCR", "Aufnahmen verworfen: OCR fehlt"), OcrMissingBody(title, Policy.SensitiveForced), NoticeKind.Warning, null, null);
        }

        // What a policy forces cannot be turned off: say who requires it instead of naming a way out the user has.
        internal static string OcrMissingBody(string title, bool forced)
        {
            // Three toast lines: the way to the OCR languages is in Windows' own settings, so the text stays short (the German
            // name of the switch alone fills a line and a half, so German says what it does).
            return I18n.IsKorean
                       ? forced ? I18n.Localize("Your organization requires redaction, but this Windows has no OCR, so captures are discarded. Install an OCR language or contact IT.")
                                : I18n.Fmt("", "No OCR on this Windows, so captures are discarded. Install an OCR language or turn off \u201C{0}\u201D.", title)
                   : forced
                       ? I18n.T("Tu organizaci\u00F3n exige tapar los datos sensibles y este Windows no tiene OCR: se descartan las capturas. Consulta con inform\u00E1tica.",
                                "Your organization requires redaction, but this Windows has no OCR, so captures are discarded. Install an OCR language or contact IT.",
                                "Deine Organisation verlangt Schw\u00E4rzung, aber dieses Windows hat keine OCR: Aufnahmen werden verworfen. Frag die IT.")
                       : I18n.T("Este Windows no tiene OCR y las capturas se descartan. Instala un idioma de OCR o desactiva \u00AB" + title + "\u00BB.",
                                "No OCR on this Windows, so captures are discarded. Install an OCR language or turn off \u201C" + title + "\u201D.",
                                "Ohne OCR werden Aufnahmen verworfen. Installiere eine OCR-Sprache oder schalte das automatische Schw\u00E4rzen aus.");
        }

        // The check is over: the card shows the capture as it is now with a note, and the copy goes to the clipboard
        // unless something else was copied meanwhile.
        void Covered(string path, CoverOutcome r, bool wantCopy, uint seq)
        {
            if (exiting) { Discard(r); return; }
            if (r.Error != null)
            {
                Discard(r);
                RemoveByPath(path);
                // Without OCR every capture is blocked, not just this one: one clear warning per session, instead of
                // the same loss repeated silently (or once per capture) with no explanation.
                if (r.OcrMissing) WarnOcrMissing();
                else Notify(I18n.T("No se pudo guardar la captura", "Could not save capture"),
                            I18n.T("Se ha descartado porque no se pudo revisar si conten\u00EDa datos sensibles.", "The capture was discarded because it could not be checked for sensitive data.") + " " + r.Error,
                            NoticeKind.Error, null, null);
                return;
            }
            Card c = Find(path);
            if (r.Preview != null)
            {
                if (c != null) c.UsePreview(r.Preview, r.Size);
                else r.Preview.Dispose();
            }
            if (c != null) c.CoverNote(SensitiveCover.Note(r));
            if (r.Copy != null)
            {
                if (wantCopy && GetClipboardSequenceNumber() == seq)
                {
                    try { CopyTracked(path, r.Copy, false, true); }
                    catch (Exception ex) { Log("Portapapeles: " + ex.Message); }
                }
                else r.Copy.Dispose();
            }
            Log("Datos sensibles: " + System.IO.Path.GetFileName(path) + ", " + r.Found.Count + " tapados en " + r.Ms + " ms");
            // Captures deleted meanwhile (by hand, or swept) take their originals with them now, not at the next sweep.
            PruneOriginals();
        }

        void CoverWriteFailed(string path, Exception ex)
        {
            if (Find(path) == null)
            {
                DropUnsaved(path);
                Notify(I18n.T("No se pudo guardar la captura", "Could not save capture"), Explain(ex, path));
            }
            else Notify(I18n.T("No se pudo guardar la captura", "Could not save capture"), Explain(ex, path) + I18n.T(" La miniatura sigue en la pila: puedes copiarla o guardarla en tu carpeta.", " The thumbnail remains in the stack: you can copy it or save it to your folder."));
        }

        // How long a worker that needs a capture's file waits for its check (a tall scrolling capture takes a while).
        const int CheckWaitMs = 180000;

        // WaitWritten's first step: a capture still being checked has no PNG on its way yet, so a worker thread waits for
        // the check before the usual 10 s for the write. The UI thread never waits here: the stack would freeze.
        static void WaitForCheck(string path)
        {
            if (Application.MessageLoop || !CoverJob.IsChecking(path)) return;
            CoverJob.WaitChecked(path, CheckWaitMs);
        }

        // The editor asked for a capture still being checked opens once it is covered and written, instead of the stack
        // freezing until then. False when there is nothing to wait for.
        bool OpenWhenChecked(string path)
        {
            if (!CoverJob.IsChecking(path)) return false;
            Card c = Find(path);
            if (c != null) c.StillChecking();
            AfterChecked(path, delegate { OpenEditor(path); });
            return true;
        }

        // then runs on the UI thread once the capture's check is over and its file written (never, if the check is still
        // not over after CheckWaitMs).
        internal void AfterChecked(string path, Action then)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                CoverJob.WaitChecked(path, CheckWaitMs);
                if (CoverJob.IsChecking(path)) { Log("Datos sensibles: " + System.IO.Path.GetFileName(path) + " sigue sin revisar"); return; }
                WaitWritten(path);
                Ui(then);
            });
        }

        // Before exiting: captures still being checked are covered and written (or the time runs out) instead of lost.
        static void WaitChecks(int timeoutMs)
        {
            List<string> left = CoverJob.Checking();
            if (left.Count == 0) return;
            Log("Salida: esperando a que se revisen " + left.Count + " capturas");
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            foreach (string p in left)
            {
                int ms = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                if (ms > 0) CoverJob.WaitChecked(p, ms);
                if (CoverJob.IsChecking(p)) { Log("Salida: alguna captura no se revis\u00F3 a tiempo"); return; }
                WaitWritten(p);
            }
        }

        // Originals of covered captures go with their capture (Sweep).
        void PruneOriginals()
        {
            int n = CoverStore.Prune(folder, delegate(string capture) { lock (writing) return writing.Contains(capture); });
            if (n > 0) Log("Limpieza: " + n + " originales de capturas tapadas borrados");
        }
    }
}
