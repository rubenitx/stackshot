// Stackshot - Accessibility for the surfaces drawn with GDI+ (the tray panel, the thumbnails, the editor's and the player's
// bars): each part is an object a screen reader can list, read and press.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Stackshot
{
    // One drawn part of a control (a row, a button, a slider) as screen readers see it.
    public sealed class PartAccessible : AccessibleObject
    {
        readonly PartsAccessible parent;
        readonly int index;
        public string PartName, PartHelp, PartKeys, PartValue, PartAction;
        public AccessibleRole PartRole = AccessibleRole.PushButton;
        public AccessibleStates PartState = AccessibleStates.None;
        public Rectangle Local;           // in the owner's client coordinates
        public Action Do;                 // the default action (run after the call returns)
        public Action<string> SetValue;   // sliders: a new value typed by the reader

        public PartAccessible(PartsAccessible parent, int index)
        {
            this.parent = parent;
            this.index = index;
        }

        public int Index { get { return index; } }
        public override AccessibleObject Parent { get { return parent; } }
        public override string Name { get { return PartName; } }
        public override AccessibleRole Role { get { return PartRole; } }
        public override AccessibleStates State { get { return PartState; } }
        public override string Help { get { return PartHelp; } }
        public override string Description { get { return PartHelp; } }
        public override string KeyboardShortcut { get { return PartKeys; } }
        public override string DefaultAction { get { return Do == null ? null : PartAction ?? I18n.T("Pulsar", "Press"); } }

        public override string Value
        {
            get { return PartValue; }
            set { if (SetValue != null) parent.Later(delegate { SetValue(value); }); }
        }

        public override Rectangle Bounds { get { return parent.ToScreen(Local); } }

        public override void DoDefaultAction()
        {
            Action a = Do;
            if (a != null) parent.Later(a);
        }

        public override AccessibleObject Navigate(AccessibleNavigation n)
        {
            int to = n == AccessibleNavigation.Next || n == AccessibleNavigation.Down || n == AccessibleNavigation.Right ? index + 1
                   : n == AccessibleNavigation.Previous || n == AccessibleNavigation.Up || n == AccessibleNavigation.Left ? index - 1 : -1;
            return to >= 0 && to < parent.GetChildCount() ? parent.GetChild(to) : null;
        }
    }

    // A control whose drawn parts are its accessible children. Parts are made again only when Signature changes.
    public abstract class PartsAccessible : Control.ControlAccessibleObject
    {
        readonly Control owner;
        List<PartAccessible> parts = new List<PartAccessible>();
        string made;

        protected PartsAccessible(Control owner) : base(owner) { this.owner = owner; }

        // Changes whenever the parts would be different.
        protected abstract string Signature();
        protected abstract void Fill(List<PartAccessible> into);

        public List<PartAccessible> Parts
        {
            get
            {
                string sig = Signature();
                if (sig != made)
                {
                    made = sig;
                    List<PartAccessible> l = new List<PartAccessible>();
                    Fill(l);
                    parts = l;
                }
                return parts;
            }
        }

        public PartAccessible NewPart(List<PartAccessible> into, string name, AccessibleRole role, Rectangle local)
        {
            PartAccessible p = new PartAccessible(this, into.Count);
            p.PartName = name;
            p.PartRole = role;
            p.Local = local;
            into.Add(p);
            return p;
        }

        public override int GetChildCount() { return Parts.Count; }

        public override AccessibleObject GetChild(int index)
        {
            List<PartAccessible> l = Parts;
            return index >= 0 && index < l.Count ? l[index] : null;
        }

        public override AccessibleObject HitTest(int x, int y)
        {
            foreach (PartAccessible p in Parts)
                if (p.Bounds.Contains(x, y)) return p;
            return base.HitTest(x, y);
        }

        public override AccessibleObject GetFocused()
        {
            foreach (PartAccessible p in Parts)
                if ((p.PartState & AccessibleStates.Focused) != 0) return p;
            return base.GetFocused();
        }

        public override AccessibleObject GetSelected()
        {
            foreach (PartAccessible p in Parts)
                if ((p.PartState & AccessibleStates.Selected) != 0) return p;
            return base.GetSelected();
        }

        internal Rectangle ToScreen(Rectangle local)
        {
            if (!owner.IsHandleCreated || owner.IsDisposed || local.IsEmpty) return Rectangle.Empty;
            try { return owner.RectangleToScreen(local); }
            catch (InvalidOperationException) { return Rectangle.Empty; }
        }

        // Actions run after the reader's call returns, so a menu or a dialog they open never blocks it.
        internal void Later(Action a)
        {
            if (owner.IsDisposed) return;
            if (!owner.IsHandleCreated) { a(); return; }
            try { owner.BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        // The state every part shares: focusable, and unavailable when off.
        public static AccessibleStates StateOf(bool enabled, bool on, bool focused)
        {
            AccessibleStates s = AccessibleStates.Focusable;
            if (!enabled) s |= AccessibleStates.Unavailable;
            if (on) s |= AccessibleStates.Checked;
            if (focused) s |= AccessibleStates.Focused | AccessibleStates.Selected | AccessibleStates.HotTracked;
            return s;
        }
    }
}
