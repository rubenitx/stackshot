// Stackshot - The document window for screen readers: names for the parts the window builds without a label of their own
// (the mode switch, the text, the preview, the divider between them and the search box).
// MIT License - https://github.com/rubenitx/stackshot
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        // After each build: only names, so it costs a handful of property sets.
        void AccessNames()
        {
            if (mode != null) AutomationProperties.SetName(mode, I18n.T("Modo", "Mode"));
            if (editor != null) AutomationProperties.SetName(editor.Box, xml ? I18n.T("Texto del XML", "XML text") : I18n.T("Texto Markdown", "Markdown text"));
            if (flow != null) AutomationProperties.SetName(flow.Box, I18n.T("Vista previa", "Preview"));
            if (search != null) AutomationProperties.SetName(search.Box, I18n.T("Buscar o filtrar", "Search or filter"));
            GridSplitter gs = Divider(page, 0);
            if (gs != null) { AutomationProperties.SetName(gs, I18n.T("Ancho del texto y de la vista previa", "Width of the text and the preview")); gs.FocusVisualStyle = Access.FocusRing; }
        }

        static GridSplitter Divider(DependencyObject o, int depth)
        {
            if (o == null || depth > 4) return null;
            GridSplitter g = o as GridSplitter;
            if (g != null) return g;
            Panel p = o as Panel;
            if (p != null)
            {
                foreach (UIElement c in p.Children)
                {
                    GridSplitter f = Divider(c, depth + 1);
                    if (f != null) return f;
                }
                return null;
            }
            Decorator d = o as Decorator;
            return d != null ? Divider(d.Child, depth + 1) : null;
        }
    }
}
