// Stackshot - The editor's bar for screen readers: every tool, colour, weight and button it draws is an object a reader
// lists (with its state and shortcut) and presses.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class Bar
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new BarAccess(this); }

        // What an item is called: a button by its label, a tool or a colour by its tooltip, without the shortcut after it.
        internal static string AccessName(Item it, bool maximized)
        {
            if (it.Kind == Kind.Caption) return it.Index == 0 ? I18n.T("Minimizar", "Minimize") : it.Index == 1 ? (maximized ? I18n.T("Restaurar", "Restore") : I18n.T("Maximizar", "Maximize")) : I18n.T("Cerrar", "Close");
            string label = it.ShowAlt && it.Alt != null ? it.Alt : it.Label;
            string n = it.Kind == Kind.Button ? Access.Clean(label) : null;
            if (string.IsNullOrEmpty(n)) n = Access.Clean(it.Tip);
            if (string.IsNullOrEmpty(n)) n = Access.Clean(label);
            return n ?? "";
        }

        sealed class BarAccess : PartsAccessible
        {
            readonly Bar bar;

            public BarAccess(Bar bar) : base(bar) { this.bar = bar; }

            public override AccessibleRole Role { get { return AccessibleRole.ToolBar; } }
            public override string Name { get { return I18n.T("Barra del editor", "Editor toolbar"); } }

            protected override string Signature()
            {
                StringBuilder b = new StringBuilder();
                b.Append(bar.Level).Append('|').Append(bar.locked).Append('|').Append(bar.Maximized).Append('|').Append(bar.Enabled);
                foreach (Item it in bar.Items)
                    b.Append('|').Append(it.R.X).Append(',').Append(it.R.Y).Append(',').Append(it.R.Width).Append(it.On ? 'o' : '-').Append(it.Enabled ? 'e' : '-')
                     .Append(it.Visible ? 'v' : '-').Append(it.ShowAlt ? 'a' : '-').Append(it.Tip).Append(it.Label);
                return b.ToString();
            }

            protected override void Fill(List<PartAccessible> into)
            {
                foreach (Item it in bar.Items)
                {
                    if (it.Kind == Kind.Gap || !bar.Shown(it) || it.R.IsEmpty) continue;
                    bool choice = it.Kind == Kind.Tool || it.Kind == Kind.Swatch || it.Kind == Kind.Weight;
                    PartAccessible p = NewPart(into, AccessName(it, bar.Maximized), choice ? AccessibleRole.RadioButton : AccessibleRole.PushButton, it.R);
                    bool usable = it.Enabled && bar.Enabled && (!bar.locked || it.Kind == Kind.Caption);
                    p.PartState = StateOf(usable, it.On, false);
                    p.PartKeys = it.Kind == Kind.Caption ? null : Access.Shortcut(it.Tip);
                    string help = Access.HelpBesides(it.Tip, p.PartName);
                    if (help.Length > 0) p.PartHelp = help;
                    p.PartAction = choice ? I18n.T("Elegir", "Select") : I18n.T("Pulsar", "Press");
                    Item item = it;
                    if (usable && it.Do != null) p.Do = delegate { if (item.Enabled && item.Do != null) item.Do(); };
                }
            }
        }
    }
}
