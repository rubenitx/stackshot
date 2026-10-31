// Stackshot - A stack thumbnail for screen readers: the capture it shows and its buttons (Copiar, Guardar, Editar,
// Cerrar...), each one listed with its name and pressed as a click would.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class Card
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new CardAccess(this); }

        // What the thumbnail is, for readers: the kind of file and its name.
        internal string AccessName
        {
            get
            {
                string n = string.IsNullOrEmpty(FilePath) ? "" : Path.GetFileName(FilePath);
                return (IsMedia ? (ShotStack.IsVideoFile(FilePath) ? I18n.T("V\u00EDdeo ", "Video ") : "GIF ") : I18n.T("Captura ", "Capture ")) + n;
            }
        }

        sealed class CardAccess : PartsAccessible
        {
            readonly Card c;

            public CardAccess(Card c) : base(c) { this.c = c; }

            public override AccessibleRole Role { get { return AccessibleRole.Grouping; } }
            public override string Name { get { return c.AccessName; } }
            public override string Description { get { return c.SavedPath != null ? I18n.T("Guardada en ", "Saved in ") + c.SavedPath : I18n.T("Sin guardar todav\u00EDa", "Not saved yet"); } }

            protected override string Signature()
            {
                StringBuilder b = new StringBuilder();
                b.Append(c.Pad).Append('|').Append(c.SavedPath).Append('|').Append(c.Leaving).Append('|').Append(c.parked);
                foreach (Btn x in c.btns) b.Append('|').Append(x.Label).Append(x.R.X).Append(',').Append(x.R.Y);
                return b.ToString();
            }

            protected override void Fill(List<PartAccessible> into)
            {
                if (c.Leaving) return;
                foreach (Btn x in c.btns)
                {
                    Rectangle r = x.R;
                    r.Offset(c.Pad, c.Pad);
                    PartAccessible p = NewPart(into, x.Label, AccessibleRole.PushButton, r);
                    p.PartState = StateOf(true, false, false);
                    p.PartAction = I18n.T("Pulsar", "Press");
                    Btn b = x;
                    p.Do = delegate { if (!c.Leaving && !c.IsDisposed && b.Do != null) b.Do(); };
                }
            }
        }
    }
}
