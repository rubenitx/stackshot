// Stackshot - The XML tree and the comparison for screen readers and the keyboard: a tree whose lines are read as they
// are shown (with what changed, in a comparison) and open or close like any tree; the focused line gets the focus ring.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Stackshot
{
    // The list behind the XML tree and the comparison view.
    public sealed class XmlLineList : ListBox
    {
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            FrameworkElement c = element as FrameworkElement;
            if (c != null) c.FocusVisualStyle = Access.FocusRing;   // keyboard only, as WPF shows focus visuals
        }

        protected override AutomationPeer OnCreateAutomationPeer() { return new Peer(this); }

        // What a line says, without the colours (and, in a comparison, what happened to it).
        internal static string Say(object item)
        {
            StringBuilder b = new StringBuilder();
            XRow x = item as XRow;
            if (x != null)
            {
                foreach (Piece p in XmlRows.Pieces(x, false)) b.Append(p.Text);
                return b.ToString().Trim();
            }
            DiffRow d = item as DiffRow;
            if (d == null) return null;
            DiffKind k = d.Mark;
            if (k == DiffKind.Added) b.Append(I18n.T("A\u00F1adido: ", "Added: "));
            else if (k == DiffKind.Removed) b.Append(I18n.T("Quitado: ", "Removed: "));
            else if (k == DiffKind.Changed) b.Append(I18n.T("Cambiado: ", "Changed: "));
            foreach (DiffPiece p in DiffRows.Pieces(d, false)) b.Append(p.Text);
            return b.ToString().Trim();
        }

        static bool Collapsible(object item, out bool collapsed)
        {
            XRow x = item as XRow;
            DiffRow d = item as DiffRow;
            collapsed = x != null ? x.Collapsed : d != null && d.Collapsed;
            return x != null ? x.Collapsible : d != null && d.Collapsible;
        }

        void Toggle(object item)
        {
            XmlTreeView t = Parent as XmlTreeView;
            XmlDiffView v = Parent as XmlDiffView;
            if (t != null) t.AccessToggle(item as XRow);
            else if (v != null) v.AccessToggle(item as DiffRow);
        }

        sealed class Peer : ListBoxAutomationPeer
        {
            readonly XmlLineList list;
            public Peer(XmlLineList list) : base(list) { this.list = list; }
            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Tree; }
            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) { return new Line(item, this, list); }

            protected override string GetNameCore()
            {
                string n = base.GetNameCore();
                if (!string.IsNullOrEmpty(n)) return n;
                return list.Parent is XmlDiffView ? I18n.T("Diferencias entre los dos XML", "Differences between the two XML files") : I18n.T("\u00C1rbol del XML", "XML tree");
            }
        }

        sealed class Line : ListBoxItemAutomationPeer, IExpandCollapseProvider
        {
            readonly XmlLineList list;
            public Line(object item, SelectorAutomationPeer owner, XmlLineList list) : base(item, owner) { this.list = list; }

            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.TreeItem; }

            protected override string GetNameCore()
            {
                string s = null;
                try { s = Say(Item); } catch (Exception) { s = null; }
                return string.IsNullOrEmpty(s) ? base.GetNameCore() : s;
            }

            public override object GetPattern(PatternInterface p)
            {
                bool closed;
                if (p == PatternInterface.ExpandCollapse) return Collapsible(Item, out closed) ? this : null;
                return base.GetPattern(p);
            }

            public ExpandCollapseState ExpandCollapseState
            {
                get
                {
                    bool closed;
                    if (!Collapsible(Item, out closed)) return ExpandCollapseState.LeafNode;
                    return closed ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded;
                }
            }

            public void Expand() { Change(true); }
            public void Collapse() { Change(false); }

            void Change(bool open)
            {
                bool closed;
                if (!Collapsible(Item, out closed)) throw new InvalidOperationException();
                if (closed != open) return;
                object item = Item;
                Access.Invoke(list, delegate { list.Toggle(item); });
            }
        }
    }

    public sealed partial class XmlTreeView
    {
        // A line opened or closed by a screen reader, as a click on its arrow does.
        internal void AccessToggle(XRow r)
        {
            if (r != null) Toggle(rows.IndexOf(r));
        }
    }

    public sealed partial class XmlDiffView
    {
        internal void AccessToggle(DiffRow r)
        {
            if (r != null) Toggle(rows.IndexOf(r));
        }
    }
}
