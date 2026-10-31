// Stackshot - Alerts drawn like the app's own dialogs, called as MessageBox.Show is.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Threading;
using System.Windows.Forms;
using M = System.Windows.Media;

namespace Stackshot
{
    // The text's first paragraph becomes the alert's title and the rest its message. Without an owner it stays on top of
    // everything, so it never hides behind the capture and recording overlays. A thread that can't show WPF windows
    // (not STA) gets the system box instead.
    public static class Dialogs
    {
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
        {
            return Show(null, text, caption, buttons, icon, def);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                return owner != null ? MessageBox.Show(owner, text, caption, buttons, icon, def) : MessageBox.Show(text, caption, buttons, icon, def);
            string[] labels;
            DialogResult[] results;
            int cancel;
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel:
                    labels = new string[] { I18n.T("Aceptar", "OK"), I18n.Cancel };
                    results = new DialogResult[] { DialogResult.OK, DialogResult.Cancel };
                    cancel = 1;
                    break;
                case MessageBoxButtons.YesNo:
                    labels = new string[] { I18n.T("S\u00ED", "Yes"), "No" };
                    results = new DialogResult[] { DialogResult.Yes, DialogResult.No };
                    cancel = 1;
                    break;
                case MessageBoxButtons.YesNoCancel:
                    labels = new string[] { I18n.T("S\u00ED", "Yes"), "No", I18n.Cancel };
                    results = new DialogResult[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel };
                    cancel = 2;
                    break;
                case MessageBoxButtons.RetryCancel:
                    labels = new string[] { I18n.T("Reintentar", "Retry"), I18n.Cancel };
                    results = new DialogResult[] { DialogResult.Retry, DialogResult.Cancel };
                    cancel = 1;
                    break;
                default:
                    labels = new string[] { I18n.T("Aceptar", "OK") };
                    results = new DialogResult[] { DialogResult.OK };
                    cancel = 0;
                    break;
            }
            int primary = def == MessageBoxDefaultButton.Button2 ? 1 : def == MessageBoxDefaultButton.Button3 ? 2 : 0;
            if (primary >= labels.Length) primary = 0;
            string title, message;
            Split(text ?? "", out title, out message);
            string glyph;
            M.Color c1, c2;
            Look(icon, out glyph, out c1, out c2);
            IntPtr handle = IntPtr.Zero;
            try { if (owner != null) handle = owner.Handle; } catch (Exception) { }
            int i = DocAlert.Ask(handle, title, message, labels, primary, cancel, glyph, c1, c2);
            return results[Math.Max(0, Math.Min(results.Length - 1, i))];
        }

        // With buttons that say what they do ("Exportar", "Descartar cambios"...): the index of the one pressed, or cancel.
        // Off an STA thread nothing can be asked: the answer is cancel.
        public static int Ask(IWin32Window owner, string text, MessageBoxIcon icon, string[] labels, int primary, int cancel)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return cancel;
            string title, message;
            Split(text ?? "", out title, out message);
            string glyph;
            M.Color c1, c2;
            Look(icon, out glyph, out c1, out c2);
            IntPtr handle = IntPtr.Zero;
            try { if (owner != null) handle = owner.Handle; } catch (Exception) { }
            return DocAlert.Ask(handle, title, message, labels, primary, cancel, glyph, c1, c2);
        }

        // "Title.\n\nDetails" as title and message; a single paragraph is the title alone when short, or its first
        // sentence and the rest.
        internal static void Split(string text, out string title, out string message)
        {
            text = text.Replace("\r", "").Trim();
            int k = text.IndexOf("\n\n", StringComparison.Ordinal);
            if (k > 0 && k <= 200) { title = text.Substring(0, k).Trim(); message = text.Substring(k + 2).Trim(); return; }
            if (text.Length <= 140 && text.IndexOf('\n') < 0) { title = text; message = ""; return; }
            int dot = text.IndexOf(". ", StringComparison.Ordinal), colon = text.IndexOf(": ", StringComparison.Ordinal);
            int cut = dot > 0 && dot < 140 ? dot + 1 : colon > 0 && colon < 140 ? colon + 1 : -1;
            if (cut > 0) { title = text.Substring(0, cut).Trim(); message = text.Substring(cut).Trim(); return; }
            title = "Stackshot";
            message = text;
        }

        static void Look(MessageBoxIcon icon, out string glyph, out M.Color c1, out M.Color c2)
        {
            if (icon == MessageBoxIcon.Error) { glyph = "warning"; c1 = Ds.Rgb(255, 105, 97); c2 = Ds.Rgb(215, 0, 21); }
            else if (icon == MessageBoxIcon.Warning) { glyph = "warning"; c1 = Ds.Rgb(255, 179, 64); c2 = Ds.Rgb(255, 120, 0); }
            else if (icon == MessageBoxIcon.Question) { glyph = "info"; c1 = Ds.Rgb(140, 150, 255); c2 = Ds.Rgb(88, 86, 214); }
            else { glyph = "info"; c1 = Ds.Rgb(100, 180, 255); c2 = Ds.Rgb(0, 110, 240); }
        }
    }
}
