// Stackshot - The tray panel for screen readers: a menu whose rows and recent captures can be listed, read and chosen,
// and whose keyboard highlight is announced as it moves.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public sealed partial class TrayPanel
    {
        PanelAccess access;   // made only when a screen reader (or a test) asks for it

        protected override AccessibleObject CreateAccessibilityInstance() { access = new PanelAccess(this); return access; }

        // Left and Right go through the recent captures (Up and Down back to the rows); Enter opens the one marked.
        void ArrowThumb(int dir)
        {
            int n = ThumbCount();
            if (n == 0) return;
            int next = hotThumb < 0 ? (dir > 0 ? 0 : n - 1) : (hotThumb + dir + n) % n;
            SetHot(-1);
            SetHotThumb(next);
            AccessFocus(-2 - next);
        }

        // The keyboard moved the highlight to a row (or to recent capture k, given as -2 - k): readers say which.
        void AccessFocus(int item)
        {
            PanelAccess a = access;
            if (a == null || !IsHandleCreated || IsDisposed) return;   // nobody is listening: nothing to say
            List<PartAccessible> parts = a.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (a.ItemOf(i) != item) continue;
                AccessibilityNotifyClients(AccessibleEvents.Focus, i);
                AccessibilityNotifyClients(AccessibleEvents.Selection, i);
                return;
            }
        }

        static Rectangle Whole(System.Windows.Rect r, int pad)
        {
            return new Rectangle(pad + (int)Math.Round(r.X), pad + (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
        }

        sealed class PanelAccess : PartsAccessible
        {
            readonly TrayPanel p;
            readonly List<int> itemOf = new List<int>();

            public PanelAccess(TrayPanel p) : base(p) { this.p = p; }

            public override AccessibleRole Role { get { return AccessibleRole.MenuPopup; } }
            public override string Name { get { return "Stackshot"; } }
            public override string Description { get { return p.status; } }

            // The panel's item behind part i: its index, -2 - k for recent capture k, -1 for the header.
            public int ItemOf(int i) { return i >= 0 && i < itemOf.Count ? itemOf[i] : -1; }

            protected override string Signature()
            {
                StringBuilder b = new StringBuilder();
                b.Append(p.layoutKey).Append('|').Append(p.hot).Append('|').Append(p.hotThumb).Append('|').Append(p.ThumbCount()).Append('|').Append(p.status).Append('|').Append(p.Pad);
                foreach (Item it in p.items) b.Append('|').Append(it.R.Y).Append(it.Enabled);
                return b.ToString();
            }

            protected override void Fill(List<PartAccessible> into)
            {
                itemOf.Clear();
                for (int i = 0; i < p.items.Count; i++)
                {
                    Item it = p.items[i];
                    if (it.Kind == KHead)
                    {
                        NewPart(into, "Stackshot: " + p.status, AccessibleRole.StaticText, Whole(it.R, p.Pad)).PartState = AccessibleStates.ReadOnly;
                        itemOf.Add(-1);
                    }
                    else if (it.Kind == KRow)
                    {
                        PartAccessible r = NewPart(into, it.Text, AccessibleRole.MenuItem, Whole(it.R, p.Pad));
                        r.PartState = StateOf(it.Enabled, false, i == p.hot);
                        if (it.Caps != null) r.PartKeys = Hotkeys.Display(p.settings.HotkeysFor(it.Id));
                        r.PartAction = I18n.T("Ejecutar", "Run");
                        string id = it.Id;
                        if (it.Enabled) r.Do = delegate { p.Choose(id, null); };
                        itemOf.Add(i);
                    }
                    else if (it.Kind == KStrip)
                    {
                        for (int t = 0; t < p.ThumbCount(); t++)
                        {
                            FileInfo f = p.recent[t];
                            string kind = !ShotStack.IsMediaFile(f.FullName) ? I18n.T("Captura", "Capture") : ShotStack.IsVideoFile(f.FullName) ? I18n.T("V\u00EDdeo", "Video") : "GIF";
                            PartAccessible c = NewPart(into, kind + I18n.T(" reciente: ", " recent: ") + f.Name, AccessibleRole.PushButton, Whole(p.thumbR[t], p.Pad));
                            c.PartHelp = When(f.LastWriteTime);
                            c.PartState = StateOf(true, false, t == p.hotThumb);
                            c.PartAction = I18n.T("Abrir", "Open");
                            string path = f.FullName;
                            c.Do = delegate { p.Choose(null, path); };
                            itemOf.Add(-2 - t);
                        }
                    }
                }
            }
        }
    }
}
