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
        bool coverErrorTold;

        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();

        // Whether new captures have their personal data covered before anything shows them.
        public static bool CoversCaptures(Settings s) { return s != null && s.SensitiveAuto; }

        // SaveCapture's step for SensitiveAuto. With it on, the capture is read and covered before anything shows it: the
        // card starts blurred, the clipboard is emptied instead of getting the capture (a paste meanwhile pastes nothing,
        // never the data), and a worker thread takes the capture over to read it, cover it, write it, copy it and show it.
        // The caller must not touch shot afterwards. Off: returns false and does nothing.
        bool CoverBeforeShowing(Bitmap shot, string path, ref Bitmap preview, ref Bitmap forClipboard)
        {
            if (!CoversCaptures(settings)) return false;
            // Anything that needs the file (drag, editor, copy, save) waits for the covered one.
            lock (writing) writing.Add(path);
            CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref forClipboard);
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
            return true;
        }

        static void Discard(CoverOutcome r)
        {
            if (r.Preview != null) r.Preview.Dispose();
            if (r.Copy != null) r.Copy.Dispose();
        }

        // The check is over: the card shows the capture as it is now with a note, and the copy goes to the clipboard
        // unless something else was copied meanwhile.
        void Covered(string path, CoverOutcome r, bool wantCopy, uint seq)
        {
            if (exiting) { Discard(r); return; }
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
            Log("Datos sensibles: " + System.IO.Path.GetFileName(path) + (r.Error != null ? " sin revisar (" + r.Error + ")" : ", " + r.Found.Count + " tapados") + " en " + r.Ms + " ms");
            if (r.Error != null && !coverErrorTold)
            {
                coverErrorTold = true;
                Notify(I18n.T("No se han revisado los datos sensibles", "Sensitive data not reviewed"), r.Error + (wantCopy ? I18n.T(" La captura no se ha copiado al portapapeles: c\u00F3piala t\u00FA si no tiene datos sensibles.", " The capture was not copied to clipboard: copy it manually if it contains no sensitive data.") : ""));
            }
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
