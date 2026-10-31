// Stackshot - The editor's backdrop sidebar for screen readers: backdrops, colours, alignments and proportions to
// choose, the sliders (a reader can type a new value) and the auto-balance switch.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class BgPanel
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new PanelAccess(this); }

        // A choice made from a reader, as a click on it makes it.
        void AccessPick(Item it)
        {
            if (!Enabled || Bg == null) return;
            Pick(it);
        }

        // A slider set from a reader (0 to 100).
        void AccessSlide(Item it, string typed)
        {
            int v;
            if (!Enabled || Bg == null || it.Kind != Kind.Slider || typed == null) return;
            if (!int.TryParse(typed.Replace("%", "").Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out v)) return;
            On = true;
            KeepShadow();
            SetValue(it, v);
            Invalidate();
            Fire(Changed);
            Fire(Committed);
        }

        sealed class PanelAccess : PartsAccessible
        {
            readonly BgPanel p;

            public PanelAccess(BgPanel p) : base(p) { this.p = p; }

            public override AccessibleRole Role { get { return AccessibleRole.Grouping; } }
            public override string Name { get { return I18n.T("Fondo", "Backdrop"); } }

            protected override string Signature()
            {
                StringBuilder b = new StringBuilder();
                b.Append(p.Width).Append('|').Append(p.Height).Append('|').Append(p.scroll).Append('|').Append(p.On).Append('|').Append(p.Enabled).Append('|').Append(p.items.Count);
                Settings s = p.Bg;
                if (s != null)
                    b.Append('|').Append(s.BgKind).Append('|').Append(s.BgPreset).Append('|').Append(s.BgColor).Append('|').Append(s.BgBlur)
                     .Append('|').Append(s.BgPadding).Append('|').Append(s.BgRadius).Append('|').Append(s.BgShadow).Append('|').Append(s.BgShadowSize)
                     .Append('|').Append(s.BgInset).Append('|').Append(s.BgBalance).Append('|').Append(s.BgAlign).Append('|').Append(s.BgRatio);
                return b.ToString();
            }

            protected override void Fill(List<PartAccessible> into)
            {
                Settings bg = p.Bg;
                if (bg == null) return;
                if (p.items.Count == 0) p.Arrange(p.Width);
                foreach (Item it in p.items)
                {
                    Item item = it;
                    PartAccessible a;
                    System.Drawing.Rectangle r = p.Shown(it.R);
                    switch (it.Kind)
                    {
                        case Kind.Header:
                            continue;
                        case Kind.None:
                            a = NewPart(into, I18n.T("Sin fondo", "No backdrop"), AccessibleRole.RadioButton, r);
                            a.PartState = StateOf(p.Enabled, !p.On, false);
                            break;
                        case Kind.Swatch:
                            a = NewPart(into, Backdrop.Name(it.Index), AccessibleRole.RadioButton, r);
                            a.PartState = StateOf(p.Enabled, p.On && p.KindNow == 0 && bg.BgPreset == it.Index, false);
                            break;
                        case Kind.Blur:
                            a = NewPart(into, it.Label, AccessibleRole.RadioButton, r);
                            a.PartState = StateOf(p.Enabled, p.On && p.KindNow == 2 && bg.BgBlur == it.Index, false);
                            break;
                        case Kind.Plain:
                            a = NewPart(into, "Color " + it.Label.ToLowerInvariant(), AccessibleRole.RadioButton, r);
                            a.PartState = StateOf(p.Enabled, p.On && p.KindNow == 1 && (bg.BgColor & 0xFFFFFF) == it.Index, false);
                            break;
                        case Kind.Slider:
                            a = NewPart(into, it.Label, AccessibleRole.Slider, r);
                            a.PartState = StateOf(p.Enabled, false, false);
                            a.PartValue = p.ValueOf(it).ToString(CultureInfo.CurrentCulture);
                            a.PartHelp = "De 0 a 100";
                            a.SetValue = delegate(string v) { p.AccessSlide(item, v); };
                            continue;
                        case Kind.Check:
                            a = NewPart(into, it.Label, AccessibleRole.CheckButton, r);
                            a.PartState = StateOf(p.Enabled, bg.BgBalance, false);
                            break;
                        case Kind.Align:
                            a = NewPart(into, I18n.T("Alineaci\u00F3n: ", "Alignment: ") + it.Label.ToLowerInvariant(), AccessibleRole.RadioButton, r);
                            a.PartState = StateOf(p.Enabled, bg.BgAlign == it.Index, false);
                            break;
                        case Kind.Ratio:
                            // Each proportion on its own, as the menu of the button offers them.
                            for (int i = 0; i < Ratios.Length; i++)
                            {
                                int idx = i;
                                PartAccessible ra = NewPart(into, I18n.T("Proporci\u00F3n ", "Aspect ratio ") + RatioNames[i], AccessibleRole.RadioButton, r);
                                ra.PartState = StateOf(p.Enabled, bg.BgRatio == Ratios[i], false);
                                ra.PartAction = I18n.T("Elegir", "Select");
                                if (p.Enabled) ra.Do = delegate { p.SetRatio(idx); };
                            }
                            continue;
                        default:
                            // Show less or more, add a picture, another colour: buttons.
                            a = NewPart(into, it.Label, AccessibleRole.PushButton, r);
                            a.PartState = StateOf(p.Enabled, false, false);
                            break;
                    }
                    a.PartAction = it.Kind == Kind.Check ? I18n.T("Cambiar", "Toggle")
                                 : it.Kind == Kind.More || it.Kind == Kind.Add || it.Kind == Kind.Picker || it.Kind == Kind.Styles || it.Kind == Kind.Keep ? I18n.T("Pulsar", "Press")
                                 : I18n.T("Elegir", "Select");
                    if (p.Enabled) a.Do = delegate { p.AccessPick(item); };
                }
            }
        }
    }
}
