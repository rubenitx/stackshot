// Stackshot - The player for screen readers: the bar's buttons (with their state and shortcut), the position and the
// volume as sliders a reader can read and set. The keys of the player work as before.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    internal sealed partial class PlayerWindow
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new PlayerAccess(this); }

        // "1:05" or "65" (seconds) typed by a reader.
        static bool AccessTime(string typed, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrEmpty(typed)) return false;
            string t = typed.Trim();
            int de = t.IndexOf(" de ", StringComparison.Ordinal);
            if (de > 0) t = t.Substring(0, de);
            double total = 0;
            foreach (string part in t.Split(':'))
            {
                double v;
                if (!double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out v) && !double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
                total = total * 60 + v;
            }
            seconds = total;
            return true;
        }

        sealed class PlayerAccess : PartsAccessible
        {
            readonly PlayerWindow w;

            public PlayerAccess(PlayerWindow w) : base(w) { this.w = w; }

            public override string Name { get { return w.Text; } }

            protected override string Signature()
            {
                StringBuilder b = new StringBuilder();
                System.Drawing.Rectangle bar = w.bar, vol = w.vol;
                b.Append(bar.X).Append(',').Append(bar.Y).Append(',').Append(bar.Width).Append('|').Append(vol.Width).Append('|').Append(w.muted).Append('|')
                 .Append(Math.Round(w.volume * 100)).Append('|').Append(Math.Floor(w.Pos)).Append('|').Append(w.message);
                foreach (Btn x in w.btns) b.Append('|').Append(x.Id).Append(x.On ? 'o' : '-').Append(x.Tip).Append(x.Text).Append(x.R.X);
                return b.ToString();
            }

            protected override void Fill(List<PartAccessible> into)
            {
                if (w.message != null) NewPart(into, w.message, AccessibleRole.StaticText, System.Drawing.Rectangle.Empty).PartState = AccessibleStates.ReadOnly;
                System.Drawing.Rectangle bar = w.bar, vol = w.vol;
                if (w.info == null || !w.info.CanPlay || bar.IsEmpty) return;
                PartAccessible seek = NewPart(into, I18n.T("Posici\u00F3n", "Position"), AccessibleRole.Slider, w.seek);
                seek.PartValue = I18n.Fmt("{0} de {1}", "{0} of {1}", PlayerKit.Clock(w.Pos), PlayerKit.Clock(w.Duration));
                seek.PartKeys = I18n.T("Izquierda / Derecha", "Left / Right");
                seek.SetValue = delegate(string v) { double t; if (AccessTime(v, out t)) w.SeekTo(Math.Max(0, Math.Min(w.Duration, t))); };
                foreach (Btn x in w.btns)
                {
                    string name = Access.Clean(x.Tip);
                    if (string.IsNullOrEmpty(name)) name = x.Text;
                    bool toggles = x.Id == "loop" || x.Id == "info" || x.Id == "mute";
                    PartAccessible p = NewPart(into, name, toggles ? AccessibleRole.CheckButton : AccessibleRole.PushButton, x.R);
                    bool on = x.On || (x.Id == "mute" && w.muted);
                    p.PartState = StateOf(true, on, false);
                    p.PartKeys = Access.Shortcut(x.Tip);
                    if (x.Text != null && x.Text != name) p.PartHelp = x.Text;
                    p.PartAction = I18n.T("Pulsar", "Press");
                    string id = x.Id;
                    p.Do = delegate { w.Act(id); };
                    if (x.Id == "mute" && !vol.IsEmpty)
                    {
                        PartAccessible v = NewPart(into, I18n.T("Volumen", "Volume"), AccessibleRole.Slider, w.vol);
                        v.PartValue = Math.Round(w.volume * 100).ToString(CultureInfo.CurrentCulture) + (I18n.IsSpanish ? " %" : "%");
                        v.PartKeys = I18n.T("Arriba / Abajo", "Up / Down");
                        v.SetValue = delegate(string s)
                        {
                            double n;
                            if (double.TryParse((s ?? "").Replace("%", "").Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out n)) w.SetVolume(Math.Max(0, Math.Min(100, n)) / 100.0, true);
                        };
                    }
                }
            }
        }
    }
}
