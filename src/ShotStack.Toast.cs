// Stackshot - The stack's notices: Notify and its overloads end here, as Stackshot's own toasts (ToastStack).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        ToastStack toasts;
        static readonly List<Notice> recorded = new List<Notice>();
        const int RecordedMax = 200;

        // A notice with a button: actionLabel names it, and action runs on a click on it or anywhere on the notice.
        public void Notify(string title, string text, NoticeKind kind, string actionLabel, Action action)
        {
            Notice n = new Notice(title, text, kind);
            if (action != null)
            {
                n.Action = action;
                n.ActionLabel = string.IsNullOrEmpty(actionLabel) ? null : actionLabel;
            }
            Post(n);
        }

        // What the notices asked for in Test mode (or on a stack the tests made without its constructor), oldest first:
        // nothing is shown on screen then.
        public static List<Notice> Recorded
        {
            get { lock (recorded) return new List<Notice>(recorded); }
        }

        public static void ForgetRecorded()
        {
            lock (recorded) recorded.Clear();
        }

        static void Record(Notice n)
        {
            lock (recorded)
            {
                recorded.Add(n);
                if (recorded.Count > RecordedMax) recorded.RemoveAt(0);
            }
        }

        // Clicking it (or its button) opens that section of the main window.
        Notice PageNotice(string title, string text, string page)
        {
            Notice n = new Notice(title, text, Notice.KindFor(title));
            if (page != null)
            {
                n.Page = page;
                n.ActionLabel = HomeWindow.NoticeButton(page);
                n.Action = delegate { if (!exiting) ShowHome(page, false); };
            }
            return n;
        }

        // Clicking it (or its button) opens that folder in Explorer.
        Notice FolderNotice(string title, string text, string dir)
        {
            Notice n = new Notice(title, text, Notice.KindFor(title));
            if (dir != null)
            {
                n.Folder = dir;
                n.ActionLabel = I18n.T("Abrir la carpeta", "Open folder");
                n.Action = delegate { OpenExplorer(dir); };
            }
            return n;
        }

        // A new XML while the viewer has unsaved changes is only announced; the button opens it in the viewer now, which
        // first asks about those changes.
        void TellArrivedXml(string path)
        {
            string op = XmlWatch.Operation(path);
            Notify(I18n.T("Nuevo XML", "New XML"), System.IO.Path.GetFileName(path) + (op.Length > 0 ? " (" + op + ")" : "") + I18n.T(". El visor tiene cambios sin guardar: \u00E1brelo desde la bandeja cuando acabes.", ". The viewer has unsaved changes: open it from the tray when finished."),
                   NoticeKind.Info, I18n.T("Abrir el XML", "Open XML"), delegate { if (!exiting) OpenViewerFile(path); });
        }

        // The busy-shortcuts notice's button: Atajos, to pick others.
        void ChooseOtherKeys()
        {
            if (!exiting) ShowHome("keys", false);
        }

        // Every notice ends here: a toast, made on the UI thread, or only recorded when nothing may be shown.
        void Post(Notice n)
        {
            if (Test || sync == null) { Record(n); return; }
            if (exiting) return;
            if (sync.InvokeRequired) { Ui(delegate { Post(n); }); return; }
            try { Toaster().Show(n); }
            catch (Exception ex) { Log("Aviso \u00AB" + n.Title + "\u00BB: " + ex.Message); }
        }

        ToastStack Toaster()
        {
            if (toasts != null) return toasts;
            ToastStack t = new ToastStack();
            t.Area = ToastAreaFor;
            t.Quiet = ToastQuietNow;
            t.Occupied = ToastOccupied;
            t.MakeView = delegate(ToastStack.Entry e) { return new ToastWindow(t, e); };
            t.Post = Ui;
            toasts = t;
            return t;
        }

        // The cards moved (or the displays changed): the toasts make room again.
        void ToastsFollowStack()
        {
            if (toasts != null) toasts.Later();
        }

        // The monitor the toasts are on, else the mouse's when the stack follows the mouse, else the one with the clock.
        ToastArea ToastAreaFor(string device)
        {
            Screen scr = null;
            if (device != null)
                foreach (Screen s in Screen.AllScreens)
                    if (s.DeviceName == device) { scr = s; break; }
            if (scr == null) scr = settings.FollowMouse ? Screen.FromPoint(Control.MousePosition) : ToastQuiet.TrayScreen();
            return ToastArea.Of(scr);
        }

        // Windows' quiet states, and an area being picked (a toast would cover part of the frozen screen): notices wait.
        static string ToastQuietNow(Rectangle monitor)
        {
            foreach (Form f in Application.OpenForms)
                if (f is RegionPicker && f.Visible) return I18n.T("eligiendo un \u00E1rea", "picking an area");
            return ToastQuiet.Reason(monitor);
        }

        // What the toasts keep clear of: the captures and pills of the stack, the update notice, the recording bar and the
        // tray panel while it is open.
        List<Rectangle> ToastOccupied()
        {
            List<Rectangle> r = new List<Rectangle>();
            foreach (Card c in cards)
                if (c.Visible && !c.Leaving && !c.Parked) r.Add(c.BodyTarget);
            foreach (Form f in Application.OpenForms)
                if (f.Visible && (f is Chip || f is UpdateNotice || f is RecordBar || f is TrayPanel || f is SavingCard)) r.Add(((FloatWindow)f).BodyTarget);
            return r;
        }
    }
}
