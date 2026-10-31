// Stackshot - Saved backdrop styles in the editor's sidebar: keep the one on screen with +, bring one back from the menu.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class BgPanel
    {
        // Settings.BgStyles: styles separated by ';', each "kind,preset,colour,blur,margin,corners,shadow,size,inset,
        // balance,alignment,proportion" in numbers (the proportion as written in BgRatio).
        public static class Styles
        {
            public const int Max = 8;

            public static List<string> All(Settings s)
            {
                List<string> l = new List<string>();
                foreach (string p in (s.BgStyles ?? "").Split(';'))
                    if (Valid(p)) l.Add(p);
                return l;
            }

            public static int Count(Settings s) { return All(s).Count; }

            // The style on screen.
            public static string Of(Settings s)
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                return string.Join(",", new string[]
                {
                    s.BgKind.ToString(ci), s.BgPreset.ToString(ci), (s.BgColor & 0xFFFFFF).ToString(ci), s.BgBlur.ToString(ci),
                    s.BgPadding.ToString(ci), s.BgRadius.ToString(ci), s.BgShadow ? "1" : "0", s.BgShadowSize.ToString(ci),
                    s.BgInset.ToString(ci), s.BgBalance ? "1" : "0", s.BgAlign.ToString(ci), Array.IndexOf(Ratios, s.BgRatio) >= 0 ? s.BgRatio : "auto"
                });
            }

            static int[] Numbers(string style)
            {
                string[] f = (style ?? "").Split(',');
                if (f.Length != 12 || Array.IndexOf(Ratios, f[11]) < 0) return null;
                int[] n = new int[11];
                for (int i = 0; i < 11; i++)
                    if (!int.TryParse(f[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i])) return null;
                return n;
            }

            public static bool Valid(string style) { return Numbers(style) != null; }

            static int Clamp(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }

            // Puts a saved style on screen; a picture removed since then falls back to the first gradient.
            public static bool Apply(Settings s, string style)
            {
                int[] n = Numbers(style);
                if (n == null) return false;
                s.BgKind = Clamp(n[0], 0, 2);
                s.BgPreset = n[1] >= 0 && n[1] < Backdrop.Count ? n[1] : 0;
                s.BgColor = n[2] & 0xFFFFFF;
                s.BgBlur = Clamp(n[3], 0, 2);
                s.BgPadding = Clamp(n[4], 0, 100);
                s.BgRadius = Clamp(n[5], 0, 100);
                s.BgShadow = n[6] != 0;
                s.BgShadowSize = Clamp(n[7], 1, 100);
                s.BgInset = Clamp(n[8], 0, 100);
                s.BgBalance = n[9] != 0;
                s.BgAlign = Clamp(n[10], 0, 8);
                s.BgRatio = style.Substring(style.LastIndexOf(',') + 1);
                return true;
            }

            // The style on screen joins the list (the oldest goes past Max); false if it was already there.
            public static bool Keep(Settings s)
            {
                string mine = Of(s);
                List<string> l = All(s);
                if (l.Contains(mine)) return false;
                l.Add(mine);
                while (l.Count > Max) l.RemoveAt(0);
                s.BgStyles = string.Join(";", l.ToArray());
                return true;
            }

            // A picture of the user's own was removed: the styles that used one after it follow it up the list, those that
            // used it fall back to the first gradient.
            public static void PictureRemoved(Settings s, int removed)
            {
                List<string> l = All(s);
                for (int i = 0; i < l.Count; i++)
                {
                    string[] f = l[i].Split(',');
                    int p;
                    if (f[0] != "0" || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out p) || p < removed) continue;
                    f[1] = (p == removed ? 0 : p - 1).ToString(CultureInfo.InvariantCulture);
                    l[i] = string.Join(",", f);
                }
                s.BgStyles = string.Join(";", l.ToArray());
            }

            public static void Remove(Settings s, int i)
            {
                List<string> l = All(s);
                if (i < 0 || i >= l.Count) return;
                l.RemoveAt(i);
                s.BgStyles = string.Join(";", l.ToArray());
            }

            // What the menu calls it: the backdrop, then what differs from the usual.
            public static string Name(string style)
            {
                int[] n = Numbers(style);
                if (n == null) return "";
                StringBuilder b = new StringBuilder();
                if (n[0] == 1)
                {
                    int rgb = n[2] & 0xFFFFFF, at = Array.IndexOf(Plains, rgb);
                    b.Append(at >= 0 ? "Color " + PlainNames[at].ToLowerInvariant() : "Color #" + rgb.ToString("X6", CultureInfo.InvariantCulture));
                }
                else if (n[0] == 2) b.Append(BlurNames[Clamp(n[3], 0, 2)]);
                else b.Append(n[1] >= 0 && n[1] < Backdrop.Count ? Backdrop.Name(n[1]) : Backdrop.Name(0));
                b.Append(I18n.T(" \u00B7 margen ", " \u00B7 padding ")).Append(Clamp(n[4], 0, 100));
                if (n[8] > 0) b.Append(I18n.T(", inserci\u00F3n", ", border inset"));
                if (n[6] == 0) b.Append(I18n.T(", sin sombra", ", no shadow"));
                if (n[10] != 4) b.Append(", ").Append(AlignNames[Clamp(n[10], 0, 8)].ToLowerInvariant());
                string ratio = style.Substring(style.LastIndexOf(',') + 1);
                if (ratio != "auto") b.Append(", ").Append(ratio);
                return b.ToString();
            }
        }

        void ApplyStyle(string style)
        {
            if (!Styles.Apply(Bg, style)) return;
            On = true;
            Relayout();
            Fire(Changed);
            Fire(Committed);
        }

        void KeepStyle()
        {
            if (!Styles.Keep(Bg)) { EdTip.Schedule(this, Shown(KeepRect()), I18n.T("Este estilo ya est\u00E1 guardado", "This style is already saved")); return; }
            On = true;
            Invalidate();
            Fire(Changed);
            Fire(Committed);   // saved with the settings
            EdTip.Schedule(this, Shown(KeepRect()), I18n.T("Estilo guardado: est\u00E1 en Estilos guardados", "Style saved: it is in Saved styles"));
        }

        Rectangle KeepRect()
        {
            foreach (Item it in items) if (it.Kind == Kind.Keep) return it.R;
            return Rectangle.Empty;
        }

        void StylesMenu(Item it)
        {
            ContextMenuStrip m = TrayMenu.Create();
            List<string> all = Styles.All(Bg);
            string mine = On ? Styles.Of(Bg) : null;
            if (all.Count == 0)
            {
                ToolStripMenuItem none = TrayMenu.Item(I18n.T("A\u00FAn no hay estilos: pulsa + para guardar el de ahora", "No styles yet: click + to save the current one"), null, delegate { });
                none.Enabled = false;
                m.Items.Add(none);
            }
            foreach (string st in all)
            {
                string style = st;
                ToolStripMenuItem mi = TrayMenu.Item(Styles.Name(style), null, delegate { ApplyStyle(style); });
                mi.Checked = style == mine;
                m.Items.Add(mi);
            }
            if (all.Count > 0)
            {
                m.Items.Add(TrayMenu.Separator());
                ToolStripMenuItem remove = TrayMenu.Item(I18n.T("Quitar un estilo", "Remove a style"), "trash", delegate { });
                for (int i = 0; i < all.Count; i++)
                {
                    int idx = i;
                    remove.DropDownItems.Add(TrayMenu.Item(Styles.Name(all[i]), null, delegate { Styles.Remove(Bg, idx); Invalidate(); Fire(Committed); }));
                }
                m.Items.Add(remove);
            }
            DisposeOnClose(m);
            Rectangle r = Shown(it.R);
            m.Show(this, new Point(r.X, r.Bottom + P(4)));
        }
    }
}
