// Round 6 (light-mode contrast and high contrast), suite "w6contrast": WCAG ratios of every text colour on every surface it
// is drawn on (light and dark, a table in the log and in contrast.txt), the contrast-theme palette built from injected
// system colours ("High Contrast Black" and "White", "Aquatic", "Desert"), the Home pages, the tray panel, a card, the
// Markdown and XML viewer, the editor chrome and the heads-up surfaces rendered off screen in light, dark and forced high
// contrast, the live switch, and captures and exports that must not change. Windows settings are never touched (the
// contrast theme is forced in process), nothing is shown on screen, files go only to STACKSHOT_TEST_DATA.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using D = System.Drawing;
using DI = System.Drawing.Imaging;
using W = System.Windows;
using WM = System.Windows.Media;
using WMI = System.Windows.Media.Imaging;

namespace Stackshot.Tests
{
    static class AppTestsW6Contrast
    {
        const string S = "w6contrast";
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags NS = BindingFlags.NonPublic | BindingFlags.Static;
        static readonly string[] ThemeNames = { "light", "dark", "hcblack", "hcwhite" };

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static string root;

        static string Root()
        {
            if (root != null) return root;
            string data = Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA");
            if (string.IsNullOrEmpty(data)) data = Path.GetTempPath();
            root = Path.Combine(data, "w6contrast");
            Directory.CreateDirectory(root);
            return root;
        }

        static string Dir(string name)
        {
            string d = Path.Combine(Root(), name);
            Directory.CreateDirectory(d);
            return d;
        }

        // ---- Contrast themes as Windows defines them (Settings > Accessibility > Contrast themes)

        static WM.Color Hex(int rgb) { return WM.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); }

        static HcColors Set(int window, int text, int face, int faceText, int hi, int hiText, int link, int gray)
        {
            HcColors c = new HcColors();
            c.Window = Hex(window); c.WindowText = Hex(text); c.ButtonFace = Hex(face); c.ButtonText = Hex(faceText);
            c.Highlight = Hex(hi); c.HighlightText = Hex(hiText); c.HotTrack = Hex(link); c.GrayText = Hex(gray);
            return c;
        }

        // Windows "High Contrast Black" and "High Contrast White", and Windows 11 "Aquatic" and "Desert".
        static HcColors HcBlack() { return Set(0x000000, 0xFFFFFF, 0x000000, 0xFFFFFF, 0x1AEBFF, 0x000000, 0xFFFF00, 0x3FF23F); }
        static HcColors HcWhite() { return Set(0xFFFFFF, 0x000000, 0xFFFFFF, 0x000000, 0x37006E, 0xFFFFFF, 0x00009F, 0x600000); }
        static HcColors HcAquatic() { return Set(0x202020, 0xFFFFFF, 0x202020, 0xFFFFFF, 0x8EE3F0, 0x263B50, 0x75E9FC, 0xA6A6A6); }
        static HcColors HcDesert() { return Set(0xFFFAEF, 0x3D3D3D, 0xFFFAEF, 0x202020, 0x903909, 0xFFF5E3, 0x1C5E75, 0x676767); }

        static int savedMode = -1;

        // 0 light, 1 dark, 2 High Contrast Black, 3 High Contrast White. The machine's own setting never leaks in.
        static void Theme(int which)
        {
            if (savedMode < 0) savedMode = (int)typeof(Ds).GetField("mode", NS).GetValue(null);
            HighContrast.Fake = which == 2 ? HcBlack() : which == 3 ? HcWhite() : null;
            HighContrast.Force = which >= 2 ? 1 : -1;
            Ds.Apply(which == 1 ? 2 : 1);
            Drain();
        }

        // A contrast theme over a light (appearance 1) or dark (2) app.
        static void ThemeWith(HcColors c, int appearance)
        {
            if (savedMode < 0) savedMode = (int)typeof(Ds).GetField("mode", NS).GetValue(null);
            HighContrast.Fake = c;
            HighContrast.Force = 1;
            Ds.Apply(appearance);
            Drain();
        }

        static void Restore()
        {
            HighContrast.Force = 0;
            HighContrast.Fake = null;
            Ds.Apply(savedMode < 0 ? 1 : savedMode);
            savedMode = -1;
            Drain();
        }

        static void Drain()
        {
            try { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, (Action)delegate { }); } catch { }
            System.Windows.Forms.Application.DoEvents();
        }

        static void Pump(int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { System.Windows.Forms.Application.DoEvents(); Drain(); Thread.Sleep(5); }
        }

        // ---- WCAG 2 contrast

        static byte Mix(byte top, byte under, double a) { return (byte)Math.Round(top * a + under * (1 - a)); }

        // A translucent colour over an opaque one, as it reaches the screen.
        static WM.Color Over(WM.Color top, WM.Color under)
        {
            double a = top.A / 255.0;
            return WM.Color.FromRgb(Mix(top.R, under.R, a), Mix(top.G, under.G, a), Mix(top.B, under.B, a));
        }

        static double Ratio(WM.Color fg, WM.Color bg)
        {
            WM.Color f = fg.A < 255 ? Over(fg, bg) : fg;
            double x = HighContrast.Luminance(f), y = HighContrast.Luminance(bg);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        sealed class Surface
        {
            public string Name;
            public WM.Color Color;
            public bool Hover;   // a passing state (the pointer over it)
            public Surface(string name, WM.Color c, bool hover) { Name = name; Color = c; Hover = hover; }
        }

        // Every surface secondary text sits on, composited down to what reaches the screen.
        static List<Surface> Surfaces(Palette p)
        {
            List<Surface> l = new List<Surface>();
            WM.Color win = p.Window, side = p.Sidebar, group = Over(p.Group, win);
            l.Add(new Surface("Window", win, false));
            l.Add(new Surface("Sidebar", side, false));
            l.Add(new Surface("Group", group, false));
            l.Add(new Surface("Control/Window", Over(p.Control, win), false));
            l.Add(new Surface("Control/Group", Over(p.Control, group), false));
            l.Add(new Surface("Control/Sidebar", Over(p.Control, side), false));
            l.Add(new Surface("Hover/Window", Over(p.ControlHover, win), true));
            l.Add(new Surface("Hover/Group", Over(p.ControlHover, group), true));
            l.Add(new Surface("Hover/Sidebar", Over(p.ControlHover, side), true));
            l.Add(new Surface("Segmented/Group", Over(p.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0), group), false));
            l.Add(new Surface("Tray panel", TrayPanel.Back(p.Dark), false));
            l.Add(new Surface("Menu", p.Dark ? Ds.Rgb(44, 44, 47) : Ds.Rgb(250, 250, 251), false));
            l.Add(new Surface("Tooltip", p.Dark ? Ds.Rgb(50, 50, 53) : Ds.Rgb(250, 250, 251), false));
            l.Add(new Surface("Card", p.Thumb, false));
            l.Add(new Surface("Home tile", p.Dark ? Ds.Rgb(42, 42, 45) : WM.Colors.White, false));
            l.Add(new Surface("Editor surround", Ds.Rgb(EdLook.Surround.R, EdLook.Surround.G, EdLook.Surround.B), false));
            return l;
        }

        sealed class Table
        {
            public readonly StringBuilder Log = new StringBuilder();
            public readonly List<string> Fails = new List<string>();
            public int Rows;

            public void Add(string mode, string fg, WM.Color fc, string bg, WM.Color bc, double need)
            {
                double r = Ratio(fc, bc);
                bool ok = r + 1e-9 >= need;
                Rows++;
                string line = string.Format("{0,-7} {1,-22} on {2,-26} {3,6:0.00}  need {4:0.0}  {5}", mode, fg, bg, r, need, ok ? "ok" : "FAIL");
                Log.AppendLine(line);
                if (!ok) Fails.Add(line);
            }
        }

        static void Roles(Table t, string mode, Palette p)
        {
            List<Surface> all = Surfaces(p);
            foreach (Surface s in all)
            {
                t.Add(mode, "Label", p.Label, s.Name, s.Color, 4.5);
                t.Add(mode, "Label2 (secondary)", p.Label2, s.Name, s.Color, 4.5);
                if (!s.Hover) t.Add(mode, "Label3 (hints, counts)", p.Label3, s.Name, s.Color, 3.0);
            }
            foreach (Surface s in all)
            {
                if (s.Hover || s.Name == "Editor surround") continue;
                // Links and plain buttons sit on the page surfaces (a plain button's hover is a Control fill on the window).
                if (s.Name != "Control/Sidebar" && s.Name != "Control/Group" && s.Name != "Segmented/Group") t.Add(mode, "Link", p.Link, s.Name, s.Color, 4.5);
                t.Add(mode, "Error", p.Error, s.Name, s.Color, 4.5);
                t.Add(mode, "Warn", p.Warn, s.Name, s.Color, 4.5);
                t.Add(mode, "MenuIcon (icon)", p.MenuIcon, s.Name, s.Color, 3.0);
                t.Add(mode, "Link (icon)", p.Link, s.Name, s.Color, 3.0);
            }
            WM.Color win = p.Window, group = Over(p.Group, win);
            // A chosen tool in the editor: its icon in the accent on the accent tint.
            t.Add(mode, "Accent (icon)", p.Accent, "Accent 16%/Window", Over(Ds.WithAlpha(p.Accent, 0.16), win), 3.0);
            // White labels on accent fills: buttons, the selected sidebar row and segment, the hovered menu row, the
            // pressed and hovered states (never lighter than the accent).
            t.Add(mode, "OnAccent", p.OnAccent, "Accent", p.Accent, 4.5);
            t.Add(mode, "OnAccent", p.OnAccent, "AccentDeep (hover)", p.AccentDeep, 4.5);
            t.Add(mode, "OnAccent", p.OnAccent, "Accent 90% (hover)", p.Deeper(p.Accent, 0.9), 4.5);
            t.Add(mode, "OnAccent", p.OnAccent, "Primary gradient end", Mix(p.Accent, WM.Colors.Black, 0.16), 4.5);
            t.Add(mode, "OnAccent", p.OnAccent, "Today tile (indigo)", Ds.Rgb(94, 92, 230), 4.5);
            // Accent text on a light accent tint: toggles that are on.
            foreach (double a in new double[] { 0.14, 0.16, 0.20, 0.22 })
            {
                t.Add(mode, "TintInk", p.TintInk, "Accent " + (int)(a * 100) + "%/Window", Over(Ds.WithAlpha(p.Accent, a), win), 4.5);
                t.Add(mode, "TintInk", p.TintInk, "Accent " + (int)(a * 100) + "%/Group", Over(Ds.WithAlpha(p.Accent, a), group), 4.5);
            }
            // The outline toggle's icon in dark mode sits on a stronger tint.
            foreach (double a in new double[] { 0.28, 0.30, 0.36 })
                if (p.Dark) t.Add(mode, "TintInk (icon)", p.TintInk, "Accent " + (int)(a * 100) + "%/Window", Over(Ds.WithAlpha(p.Accent, a), win), 3.0);
            // Danger buttons and fault bars: red text on a red tint.
            foreach (double a in new double[] { 0.10, 0.14, 0.16, 0.22 })
                t.Add(mode, "Error", p.Error, "Red " + (int)(a * 100) + "%/Group", Over(Ds.WithAlpha(p.Red, a), group), 4.5);
            // The SOAP bar's chips.
            WM.Color bar = Over(p.Dark ? Ds.Argb(0.035, 255, 255, 255) : Ds.Argb(0.025, 0, 0, 0), win);
            t.Add(mode, "SOAP OK", p.Dark ? p.Green : Ds.Rgb(20, 112, 48), "Green chip/SOAP bar", Over(Ds.WithAlpha(p.Green, p.Dark ? 0.22 : 0.16), bar), 4.5);
            t.Add(mode, "SOAP Fault", p.Error, "Red chip/fault bar", Over(Ds.WithAlpha(p.Red, p.Dark ? 0.22 : 0.16), Over(Ds.WithAlpha(p.Red, p.Dark ? 0.16 : 0.10), win)), 4.5);
            // The comparison: signs and changed pieces on their row tints.
            XmlDiffLook dl = new XmlDiffLook(p);
            WM.Brush[] inks = { dl.AddInk, dl.DelInk, dl.ChgInk }, backs = { dl.AddBack, dl.DelBack, dl.ChgBack };
            string[] kinds = { "added", "removed", "changed" };
            for (int i = 0; i < 3; i++)
                for (int k = 0; k < 3; k++)
                {
                    if (i != k && k != 2) continue;   // a sign on its own row, the changed pieces on changed rows
                    t.Add(mode, "Diff " + kinds[i], ((WM.SolidColorBrush)inks[i]).Color, kinds[k] + " row", Over(((WM.SolidColorBrush)backs[k]).Color, win), 4.5);
                }
            // XML syntax on the page.
            XmlLook x = new XmlLook(p);
            string[] names = { "XML name", "XML attribute", "XML value", "XML CDATA", "XML PI", "XML namespace", "XML comment" };
            WM.Brush[] brushes = { x.Name, x.AttrName, x.AttrValue, x.CData, x.Pi, x.Ns, x.Comment };
            for (int i = 0; i < names.Length; i++)
            {
                WM.Color c = ((WM.SolidColorBrush)brushes[i]).Color;
                double need = i == 6 ? 3.0 : 4.5;
                t.Add(mode, names[i], c, "Window", win, need);
                t.Add(mode, names[i], c, "Group", group, need);
            }
        }

        static WM.Color Mix(WM.Color a, WM.Color b, double t)
        {
            return WM.Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        // The heads-up surfaces (always dark, translucent over anything): checked over white, the worst case, and black.
        static void Heads(Table t, string mode)
        {
            WM.Color[] unders = { WM.Colors.White, WM.Colors.Black, Ds.Rgb(128, 128, 128) };
            string[] un = { "white", "black", "grey" };
            WM.Color[] backs = { Palette.Hud, Ds.Argb(0.93, 30, 30, 32), Ds.Argb(0.94, 30, 30, 32), Ds.Argb(214 / 255.0, 30, 30, 32) };
            string[] bn = { "HUD", "Recording bar", "Editor tip", "Player bar" };
            for (int i = 0; i < backs.Length; i++)
                for (int k = 0; k < unders.Length; k++)
                {
                    WM.Color b = Over(backs[i], unders[k]);
                    t.Add(mode, "HudLabel", Palette.HudLabel, bn[i] + " over " + un[k], b, 4.5);
                    t.Add(mode, "HudLabel2", Palette.HudLabel2, bn[i] + " over " + un[k], b, 4.5);
                    t.Add(mode, "HudAccent", Palette.HudAccent, bn[i] + " over " + un[k], b, 4.5);
                }
        }

        static string TableFile() { return Path.Combine(Root(), "contrast.txt"); }

        // Every text colour against every surface it is drawn on, light and dark: AA for text, 3:1 for hints and icons.
        static void ContrastTable()
        {
            Table t = new Table();
            try
            {
                Theme(0);
                Roles(t, "light", Ds.Brushes);
                Heads(t, "light");
                Theme(1);
                Roles(t, "dark", Ds.Brushes);
                Heads(t, "dark");
                HcColors[] sets = { HcBlack(), HcWhite(), HcAquatic(), HcDesert() };
                string[] names = { "hcblack", "hcwhite", "aquatic", "desert" };
                for (int i = 0; i < sets.Length; i++)
                {
                    ThemeWith(sets[i], 1);
                    Palette p = Ds.Brushes;
                    // The system's own pairs: Windows designs these themes for AA, the two classic ones well above it.
                    double need = i < 2 ? 7 : 4.5;
                    t.Add(names[i], "WindowText", p.Label, "Window", p.Window, need);
                    t.Add(names[i], "HighlightText", p.OnAccent, "Highlight", p.Accent, need);
                    t.Add(names[i], "HotTrack", p.Link, "Window", p.Window, 4.5);
                    t.Add(names[i], "ButtonText", p.Sys.ButtonText, "ButtonFace", p.Sys.ButtonFace, need);
                    t.Add(names[i], "GrayText", p.Label3, "Window", p.Window, 3.0);
                    t.Add(names[i], "HudLabel", Palette.HudLabel, "HUD", Palette.Hud, need);
                }
            }
            finally { Restore(); }
            File.WriteAllText(TableFile(), t.Log.ToString(), Encoding.UTF8);
            Console.WriteLine("    " + t.Rows + " pairs, table in " + TableFile());
            foreach (string f in t.Fails) Console.WriteLine("      " + f);
            Check(t.Fails.Count == 0, t.Fails.Count + " pairs under their minimum (see the log)");
        }

        // ---- The contrast-theme palette

        static void Same(WM.Color expected, WM.Color actual, string role)
        {
            Check(expected == actual, role + ": expected " + expected + ", got " + actual);
        }

        static void PaletteFromSystem()
        {
            HcColors[] sets = { HcBlack(), HcWhite(), HcAquatic(), HcDesert() };
            string[] names = { "High Contrast Black", "High Contrast White", "Aquatic", "Desert" };
            try
            {
                for (int i = 0; i < sets.Length; i++)
                {
                    HcColors s = sets[i];
                    ThemeWith(s, i % 2 == 0 ? 1 : 2);
                    Palette p = Ds.Brushes;
                    string n = names[i] + " ";
                    Check(p.High && HighContrast.On, n + "palette not built from the system colours");
                    Check(p.Sys != null && p.Sys.Same(s), n + "Sys does not hold the injected colours");
                    Same(s.WindowText, p.Label, n + "Label"); Same(s.WindowText, p.Label2, n + "Label2"); Same(s.GrayText, p.Label3, n + "Label3");
                    Same(s.Window, p.Window, n + "Window"); Same(s.Window, p.Sidebar, n + "Sidebar"); Same(s.Window, p.Group, n + "Group");
                    Same(s.ButtonFace, p.Control, n + "Control"); Same(s.ButtonFace, p.ControlHover, n + "ControlHover");
                    Same(s.WindowText, p.Separator, n + "Separator"); Same(s.WindowText, p.Hairline, n + "Hairline");
                    Same(s.Highlight, p.Accent, n + "Accent"); Same(s.HighlightText, p.OnAccent, n + "OnAccent"); Same(s.HighlightText, p.TintInk, n + "TintInk");
                    Same(s.Highlight, p.AccentDeep, n + "AccentDeep"); Same(s.Highlight, p.Deeper(p.Accent, 0.8), n + "Deeper");
                    Same(s.HotTrack, p.Link, n + "Link");
                    Same(s.WindowText, p.Error, n + "Error"); Same(s.WindowText, p.Warn, n + "Warn"); Same(s.WindowText, p.MenuIcon, n + "MenuIcon");
                    WM.Color[] tints = { p.Red, p.Green, p.Orange, p.Yellow, p.Purple, p.Pink, p.Teal };
                    foreach (WM.Color c in tints) Same(s.WindowText, c, n + "colour role");
                    Same(s.Window, p.Thumb, n + "Thumb");
                    Check(p.Shadow.A == 0, n + "a shadow is drawn");
                    Same(s.Window, Palette.Hud, n + "Hud"); Same(s.WindowText, Palette.HudLabel, n + "HudLabel"); Same(s.WindowText, Palette.HudLabel2, n + "HudLabel2");
                    Same(s.WindowText, Palette.HudLine, n + "HudLine"); Same(s.Window, Palette.HudBack(Ds.Argb(0.5, 0, 0, 0)), n + "HudBack");
                    Same(s.WindowText, Palette.HudInk(Ds.Rgb(255, 0, 0)), n + "HudInk"); Same(s.Highlight, Palette.HudFill(Ds.Rgb(255, 0, 0)), n + "HudFill");
                    Same(s.HighlightText, Palette.OnHudFill(WM.Colors.White), n + "OnHudFill"); Same(s.GrayText, Palette.HudFaint(Ds.Argb(0.2, 255, 255, 255)), n + "HudFaint");
                    Same(s.HotTrack, Palette.HudAccent, n + "HudAccent");
                    // Nothing translucent but the shadow (and the pointer's wash on heads-up buttons).
                    WM.Color[] solid = { p.Label, p.Label2, p.Label3, p.Window, p.Sidebar, p.Group, p.Control, p.ControlHover, p.Separator, p.Hairline, p.Accent, p.Thumb };
                    foreach (WM.Color c in solid) Check(c.A == 255, n + "a translucent role: " + c);
                    Check(p.Dark == (HighContrast.Luminance(s.Window) < 0.4), n + "Dark does not follow the window colour");
                    // The app's own appearance setting stays what it was under the theme (exports follow it).
                    Check(HighContrast.BaseDark == (i % 2 == 1), n + "BaseDark lost the app's appearance");
                    Check(!Sheet.HasMica, n + "Mica stays on under a contrast theme");
                    // The GDI bridges.
                    Check(Stackshot.Theme.Bg == Ds.Gdi(s.Window) && Stackshot.Theme.Border == Ds.Gdi(s.WindowText), n + "Theme (GDI) does not follow");
                    Check(EdLook.Surround == Ds.Gdi(s.Window), n + "the editor's surround does not follow");
                    Check(TrayMenu.Back == Ds.Gdi(s.Window) && TrayPanel.Back(p.Dark) == s.Window, n + "the tray does not follow");
                }
                // The real system colours are read with GetSysColor: whatever the theme, every one is opaque.
                HcColors real = HcColors.FromSystem();
                WM.Color[] rc = { real.Window, real.WindowText, real.ButtonFace, real.ButtonText, real.Highlight, real.HighlightText, real.HotTrack, real.GrayText };
                foreach (WM.Color c in rc) Check(c.A == 255, "GetSysColor gave a translucent colour");
            }
            finally { Restore(); }
            Check(!HighContrast.On && !Ds.Brushes.High, "the contrast theme outlived Restore");
            Check(Sheet.HasMica == (bool)typeof(HighContrast).GetProperty("OsMica", NS).GetValue(null, null), "Mica not given back");
        }

        // With no contrast theme nothing changes but the secondary greys and the accent's depth.
        static void NothingElseChanges()
        {
            try
            {
                Theme(0);
                Palette l = Ds.Brushes;
                Check(!l.High && l.Sys == null, "light: a contrast palette");
                Same(Ds.Argb(0.88, 0, 0, 0), l.Label, "light Label"); Same(Ds.Argb(0.60, 0, 0, 0), l.Label2, "light Label2"); Same(Ds.Argb(0.45, 0, 0, 0), l.Label3, "light Label3");
                Same(Ds.Rgb(246, 246, 246), l.Window, "light Window"); Same(Ds.Rgb(232, 232, 234), l.Sidebar, "light Sidebar"); Same(WM.Colors.White, l.Group, "light Group");
                Same(Ds.Argb(0.06, 0, 0, 0), l.Control, "light Control"); Same(Ds.Argb(0.12, 0, 0, 0), l.Hairline, "light Hairline");
                Same(Ds.Rgb(255, 59, 48), l.Red, "light Red"); Same(Ds.Rgb(52, 199, 89), l.Green, "light Green"); Same(Ds.Rgb(242, 242, 247), l.Thumb, "light Thumb");
                Same(WM.Colors.White, l.OnAccent, "light OnAccent");
                Same(Ds.Argb(0.86, 28, 28, 30), Palette.Hud, "Hud"); Same(Ds.Rgb(255, 255, 255), Palette.HudLabel, "HudLabel");
                Same(Ds.Argb(0.6, 255, 255, 255), Palette.HudLabel2, "HudLabel2"); Same(Ds.Argb(0.12, 255, 255, 255), Palette.HudLine, "HudLine");
                Same(Ds.Argb(0.18, 255, 255, 255), Palette.HudHover, "HudHover");
                WM.Color red = Ds.Rgb(255, 0, 0), veil = Ds.Argb(0.4, 0, 0, 0);
                Same(veil, Palette.HudBack(veil), "HudBack"); Same(red, Palette.HudInk(red), "HudInk"); Same(red, Palette.HudFill(red), "HudFill");
                Same(red, Palette.OnHudFill(red), "OnHudFill"); Same(veil, Palette.HudFaint(veil), "HudFaint"); Same(veil, Palette.HudWash(veil), "HudWash");
                Check(Stackshot.Theme.Bg == D.Color.FromArgb(246, 246, 246) && Stackshot.Theme.Border == D.Color.FromArgb(212, 212, 216), "light Theme (GDI) changed");
                Check(EdLook.Surround == D.Color.FromArgb(233, 233, 235), "light surround changed");
                Check(TrayPanel.Back(false) == Ds.Rgb(248, 248, 249), "light tray changed");
                Theme(1);
                Palette d = Ds.Brushes;
                Same(Ds.Argb(0.92, 255, 255, 255), d.Label, "dark Label"); Same(Ds.Argb(0.64, 255, 255, 255), d.Label2, "dark Label2"); Same(Ds.Argb(0.40, 255, 255, 255), d.Label3, "dark Label3");
                Same(Ds.Rgb(30, 30, 30), d.Window, "dark Window"); Same(Ds.Rgb(37, 37, 38), d.Sidebar, "dark Sidebar"); Same(Ds.Argb(0.05, 255, 255, 255), d.Group, "dark Group");
                Same(Ds.Rgb(255, 69, 58), d.Red, "dark Red"); Same(Ds.Rgb(44, 44, 46), d.Thumb, "dark Thumb");
                Check(Stackshot.Theme.Bg == D.Color.FromArgb(30, 30, 30) && EdLook.Surround == D.Color.FromArgb(22, 22, 24) && TrayPanel.Back(true) == Ds.Rgb(40, 40, 43), "dark GDI colours changed");
                Check(Sheet.HasMica == (bool)typeof(HighContrast).GetProperty("OsMica", NS).GetValue(null, null), "Mica changed without a contrast theme");
                // Exports keep the old greys and blue.
                Palette x = Palette.Legacy(false), y = Palette.Legacy(true);
                Same(Ds.Argb(0.50, 0, 0, 0), x.Label2, "legacy light Label2"); Same(Ds.Argb(0.26, 0, 0, 0), x.Label3, "legacy light Label3"); Same(Ds.Rgb(0, 122, 255), x.Accent, "legacy light Accent");
                Same(Ds.Argb(0.55, 255, 255, 255), y.Label2, "legacy dark Label2"); Same(Ds.Argb(0.28, 255, 255, 255), y.Label3, "legacy dark Label3"); Same(Ds.Rgb(10, 132, 255), y.Accent, "legacy dark Accent");
                Check(!x.High && !y.High, "a legacy palette is a contrast one");
            }
            finally { Restore(); }
        }

        // ---- Live switch

        static void LiveSwitch()
        {
            int fired = 0, gen0;
            Action count = delegate { fired++; };
            Settings s = NewSettings();
            HomeWindow hw = null;
            MarkdownWindow md = null;
            Editor ed = null;
            try
            {
                Theme(0);
                hw = Home(s);
                SetPage(hw, "general");
                md = Doc(SampleMd, "informe.md");
                ed = NewEditor(new D.Size(1100, 700));
                Bar bar = (Bar)typeof(Editor).GetField("bar", NP).GetValue(ed);
                D.Color barLight = BarPixel(bar);
                Ds.Changed += count;
                gen0 = HighContrast.Generation;

                ThemeWith(HcBlack(), 1);
                Pump(300);
                Check(fired == 1, "switching to a contrast theme raised Changed " + fired + " times");
                Check(HighContrast.Generation != gen0, "the palette generation did not move");
                Check(Ds.Brushes.High && Ds.Dark, "High Contrast Black is not the palette");
                WMI.BitmapSource home = RenderWindow(hw, 980, 700);
                Check(Pixel(home, 6, 690) == WM.Colors.Black, "Home's sidebar did not become the system window colour: " + Pixel(home, 6, 690));
                Check(BrushColor(hw.Background) == WM.Colors.Black, "Home's background did not follow");
                Check(BrushColor(md.Background) == WM.Colors.Black, "the Markdown window's background did not follow");
                D.Color barHc = BarPixel(bar);
                Check(barHc.ToArgb() == D.Color.Black.ToArgb() && barLight.ToArgb() != barHc.ToArgb(), "the editor's bar did not repaint in the system colours: " + barHc);

                // The same colours again change nothing; another contrast theme does.
                Ds.Apply(1);
                Drain();
                Check(fired == 1, "the same theme again raised Changed");
                ThemeWith(HcWhite(), 1);
                Pump(300);
                Check(fired == 2 && !Ds.Dark, "High Contrast White did not replace High Contrast Black");
                Check(Pixel(RenderWindow(hw, 980, 700), 6, 690) == WM.Colors.White, "Home's sidebar did not follow the second theme");
                Check(BarPixel(bar).ToArgb() == D.Color.White.ToArgb(), "the editor's bar did not follow the second theme");

                // The app's own light/dark setting under a theme changes nothing either; turning the theme off restores it.
                Ds.Apply(2);
                Drain();
                Check(fired == 2 && Ds.Brushes.High, "the appearance setting overrode the contrast theme");
                HighContrast.Force = -1;
                HighContrast.Fake = null;
                Ds.Apply(1);
                Pump(300);
                Check(fired == 3 && !Ds.Brushes.High && !Ds.Dark, "turning the theme off did not bring light back");
                Check(BarPixel(bar).ToArgb() == barLight.ToArgb(), "the editor's bar did not come back to light");
                Check(BrushColor(hw.Background) != WM.Colors.White || Sheet.HasMica, "Home kept the contrast background");
            }
            finally
            {
                Ds.Changed -= count;
                if (ed != null) ed.Dispose();
                if (md != null) CloseDoc(md);
                if (hw != null) hw.Shutdown();
                Restore();
            }
        }

        static WM.Color BrushColor(WM.Brush b)
        {
            WM.SolidColorBrush s = b as WM.SolidColorBrush;
            return s != null ? s.Color : WM.Colors.Transparent;
        }

        static D.Color BarPixel(Bar bar)
        {
            using (D.Bitmap b = new D.Bitmap(bar.Width, bar.Height))
            {
                using (D.Graphics g = D.Graphics.FromImage(b)) bar.PaintTo(g, new D.Rectangle(0, 0, bar.Width, bar.Height));
                return b.GetPixel(2, bar.Height / 2);
            }
        }

        static WM.Color Pixel(WMI.BitmapSource b, int x, int y)
        {
            byte[] px = new byte[4];
            new WMI.FormatConvertedBitmap(b, WM.PixelFormats.Bgra32, null, 0).CopyPixels(new W.Int32Rect(x, y, 1, 1), px, 4, 0);
            return WM.Color.FromArgb(px[3], px[2], px[1], px[0]);
        }

        // ---- Captures and exports never change

        static void ExportsUnchanged()
        {
            string sampleB = SampleXml.Replace("<importe>12.50</importe>", "<importe>13.00</importe><nota>urgente</nota>");
            try
            {
                for (int dark = 0; dark < 2; dark++)
                {
                    string mode = dark == 1 ? "dark" : "light";
                    Theme(dark);
                    WMI.BitmapSource md0 = RenderMarkdownPicture(), xml0 = RenderXmlPicture(), diff0 = RenderDiffPicture(sampleB);
                    D.Bitmap ed0 = RenderEditedCapture();
                    WMI.BitmapSource print0 = RenderPrintable();
                    ThemeWith(HcBlack(), dark == 1 ? 2 : 1);
                    WMI.BitmapSource md1 = RenderMarkdownPicture(), xml1 = RenderXmlPicture(), diff1 = RenderDiffPicture(sampleB);
                    D.Bitmap ed1 = RenderEditedCapture();
                    WMI.BitmapSource print1 = RenderPrintable();
                    ThemeWith(HcWhite(), dark == 1 ? 2 : 1);
                    WMI.BitmapSource md2 = RenderMarkdownPicture(), xml2 = RenderXmlPicture();
                    Check(SamePixels(md0, md1) && SamePixels(md0, md2), mode + ": the Markdown picture changed under a contrast theme");
                    Check(SamePixels(xml0, xml1) && SamePixels(xml0, xml2), mode + ": the XML picture changed under a contrast theme");
                    Check(SamePixels(diff0, diff1), mode + ": the XML comparison picture changed under a contrast theme");
                    Check(SamePixels(print0, print1), mode + ": the printed page changed under a contrast theme");
                    Check(SameBitmaps(ed0, ed1), mode + ": the edited capture changed under a contrast theme");
                    Save(md0, "export-markdown-" + mode + ".png");
                    Save(xml0, "export-xml-" + mode + ".png");
                    Save(diff0, "export-xmldiff-" + mode + ".png");
                    ed0.Dispose();
                    ed1.Dispose();
                    Restore();
                }
                // The pictures keep the look they had before the contrast fixes (the old secondary grey): the comment of the
                // XML picture is still drawn at 26% black.
                Theme(0);
                XmlLook look = new XmlLook(Palette.Export());
                Same(Ds.Argb(0.26, 0, 0, 0), ((WM.SolidColorBrush)look.Comment).Color, "export comment colour");
            }
            finally { Restore(); }
        }

        static WMI.BitmapSource RenderMarkdownPicture()
        {
            string err;
            WMI.BitmapSource b = MarkdownView.Render(SampleMd, out err);
            Check(b != null, "Markdown picture: " + err);
            return b;
        }

        static WMI.BitmapSource RenderXmlPicture()
        {
            MarkdownWindow w = Doc(SampleXml, "pedido.xml");
            try
            {
                object[] args = { null };
                WMI.BitmapSource b = (WMI.BitmapSource)typeof(MarkdownWindow).GetMethod("RenderDoc", NP).Invoke(w, args);
                Check(b != null, "XML picture: " + args[0]);
                return b;
            }
            finally { CloseDoc(w); }
        }

        static XmlModel Model(string text)
        {
            XmlModel m;
            string error;
            int line, col;
            Check(XmlDoc.Parse(text, out m, out error, out line, out col), "XML: " + error);
            return m;
        }

        static WMI.BitmapSource RenderDiffPicture(string newer)
        {
            DiffResult r = XmlDiff.Compare(Model(SampleXml), Model(newer), null);
            XmlDiffLook look = new XmlDiffLook(Ds.Brushes);
            List<DiffRow> rows = DiffRows.Walk(r.Top, false, look);
            string err;
            WMI.BitmapSource b = XmlDiffView.Render(rows, look, out err);
            Check(b != null, "comparison picture: " + err);
            return b;
        }

        static WMI.BitmapSource RenderPrintable()
        {
            System.Windows.Documents.FlowDocument d = MarkdownFlow.Printable(MarkdownDoc.Parse(SampleMd), Dir("pics"), 400, null);
            d.PageWidth = 700;
            d.PageHeight = 900;
            d.PagePadding = new W.Thickness(40);
            System.Windows.Documents.DocumentPaginator pg = ((System.Windows.Documents.IDocumentPaginatorSource)d).DocumentPaginator;
            pg.PageSize = new W.Size(700, 900);
            pg.ComputePageCount();
            System.Windows.Documents.DocumentPage page = pg.GetPage(0);
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(700, 900, 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(page.Visual);
            rtb.Freeze();
            return rtb;
        }

        // The capture as the editor exports it, with an arrow, a rectangle and a counter on it.
        static D.Bitmap RenderEditedCapture()
        {
            Editor ed = NewEditor(new D.Size(900, 600));
            try
            {
                Canvas c = (Canvas)typeof(Editor).GetField("canvas", NP).GetValue(ed);
                List<Shape> shapes = (List<Shape>)typeof(Canvas).GetField("shapes", NP).GetValue(c);
                shapes.Add(new Shape { Kind = Tool.Arrow, Color = Stackshot.Theme.Palette[0], Width = 6, A = new D.PointF(40, 40), B = new D.PointF(300, 200) });
                shapes.Add(new Shape { Kind = Tool.Rect, Color = Stackshot.Theme.Palette[3], Width = 4, A = new D.PointF(320, 60), B = new D.PointF(560, 260) });
                return c.Render();
            }
            finally { ed.Dispose(); }
        }

        static byte[] Pixels(WMI.BitmapSource b)
        {
            WMI.FormatConvertedBitmap f = new WMI.FormatConvertedBitmap(b, WM.PixelFormats.Pbgra32, null, 0);
            byte[] px = new byte[f.PixelWidth * f.PixelHeight * 4];
            f.CopyPixels(px, f.PixelWidth * 4, 0);
            return px;
        }

        static bool SamePixels(WMI.BitmapSource a, WMI.BitmapSource b)
        {
            if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight) return false;
            byte[] x = Pixels(a), y = Pixels(b);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        static bool SameBitmaps(D.Bitmap a, D.Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            D.Rectangle r = new D.Rectangle(0, 0, a.Width, a.Height);
            DI.BitmapData da = a.LockBits(r, DI.ImageLockMode.ReadOnly, DI.PixelFormat.Format32bppArgb);
            DI.BitmapData db = b.LockBits(r, DI.ImageLockMode.ReadOnly, DI.PixelFormat.Format32bppArgb);
            try
            {
                byte[] x = new byte[da.Stride * a.Height], y = new byte[db.Stride * b.Height];
                System.Runtime.InteropServices.Marshal.Copy(da.Scan0, x, 0, x.Length);
                System.Runtime.InteropServices.Marshal.Copy(db.Scan0, y, 0, y.Length);
                for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
                return true;
            }
            finally { a.UnlockBits(da); b.UnlockBits(db); }
        }

        // ---- Surfaces off screen

        static void Save(WMI.BitmapSource b, string file)
        {
            WMI.PngBitmapEncoder enc = new WMI.PngBitmapEncoder();
            enc.Frames.Add(WMI.BitmapFrame.Create(b));
            using (FileStream fs = File.Create(Path.Combine(Dir("shots"), file))) enc.Save(fs);
        }

        static void Save(D.Bitmap b, string file) { b.Save(Path.Combine(Dir("shots"), file), DI.ImageFormat.Png); }

        static Settings NewSettings()
        {
            Settings s = new Settings();
            s.SaveFolder = Dir("Capturas");   // never the user's own pictures
            return s;
        }

        static HomeWindow Home(Settings s)
        {
            ShotStack owner = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            HomeWindow hw = new HomeWindow(owner, s);
            Drain();
            return hw;
        }

        static void SetPage(HomeWindow hw, string id)
        {
            MethodInfo m = typeof(HomeWindow).GetMethod("SetPage", NP);
            try { m.Invoke(hw, new object[] { id, false }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("page " + id + ": " + ex.InnerException, ex.InnerException); }
        }

        // The window's own background first (it is not part of its content), then the content.
        static WMI.BitmapSource RenderWindow(W.Window win, int w, int h)
        {
            W.UIElement content = (W.UIElement)win.Content;
            content.Measure(new W.Size(w, h));
            content.Arrange(new W.Rect(0, 0, w, h));
            content.UpdateLayout();
            Pump(150);
            content.Measure(new W.Size(w, h));
            content.Arrange(new W.Rect(0, 0, w, h));
            content.UpdateLayout();
            WM.DrawingVisual dv = new WM.DrawingVisual();
            using (WM.DrawingContext dc = dv.RenderOpen()) dc.DrawRectangle(win.Background ?? WM.Brushes.Magenta, null, new W.Rect(0, 0, w, h));
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(w, h, 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Render(content);
            rtb.Freeze();
            return rtb;
        }

        static WMI.BitmapSource RenderTray(Settings s)
        {
            TrayPanel tp = new TrayPanel(s);
            try
            {
                typeof(TrayPanel).GetMethod("Rebuild", NP).Invoke(tp, new object[] { 1f, 0 });
                D.Size body = (D.Size)typeof(FloatWindow).GetField("body", NP).GetValue(tp);
                int pad = (int)typeof(FloatWindow).GetField("Pad", NP).GetValue(tp);
                int w = body.Width + 2 * pad, h = body.Height + 2 * pad;
                MethodInfo paint = typeof(TrayPanel).GetMethod("PaintSurface", NP);
                return Ink.Render(w, h, delegate(WM.DrawingContext dc)
                {
                    dc.DrawRectangle(WM.Brushes.Gray, null, new W.Rect(0, 0, w, h));
                    paint.Invoke(tp, new object[] { dc, w, h });
                }, null);
            }
            finally { tp.Dispose(); }
        }

        static string SamplePicture()
        {
            string path = Path.Combine(Dir("pics"), "muestra.png");
            if (File.Exists(path)) return path;
            using (D.Bitmap b = new D.Bitmap(640, 400))
            {
                using (D.Graphics g = D.Graphics.FromImage(b))
                {
                    using (D.Drawing2D.LinearGradientBrush lg = new D.Drawing2D.LinearGradientBrush(new D.Rectangle(0, 0, 640, 400), D.Color.FromArgb(70, 120, 200), D.Color.FromArgb(230, 200, 120), 30f))
                        g.FillRectangle(lg, 0, 0, 640, 400);
                    g.FillRectangle(D.Brushes.White, 40, 40, 300, 200);
                    using (D.Font f = new D.Font("Segoe UI", 20)) g.DrawString("Captura de prueba", f, D.Brushes.Black, 60, 60);
                }
                b.Save(path, DI.ImageFormat.Png);
            }
            return path;
        }

        static WMI.BitmapSource RenderCard(string path, bool hover)
        {
            Card c = new Card(null, path);
            bool keep = Card.ForceHover;
            try
            {
                typeof(FloatWindow).GetField("s", NP).SetValue(c, 1f);
                typeof(Card).GetMethod("Fit", NP).Invoke(c, new object[] { new D.Size(256, 160) });
                Card.ForceHover = hover;
                D.Size body = (D.Size)typeof(FloatWindow).GetField("body", NP).GetValue(c);
                int pad = (int)typeof(FloatWindow).GetField("Pad", NP).GetValue(c);
                int w = body.Width + 2 * pad, h = body.Height + 2 * pad;
                MethodInfo paint = typeof(Card).GetMethod("PaintSurface", NP);
                return Ink.Render(w, h, delegate(WM.DrawingContext dc)
                {
                    dc.DrawRectangle(WM.Brushes.Gray, null, new W.Rect(0, 0, w, h));
                    paint.Invoke(c, new object[] { dc, w, h });
                }, null);
            }
            finally { Card.ForceHover = keep; c.Dispose(); }
        }

        static void Park(W.Window w)
        {
            w.WindowStartupLocation = W.WindowStartupLocation.Manual;
            w.Left = -32000;
            w.Top = -32000;
            w.ShowActivated = false;
            w.ShowInTaskbar = false;
            w.Show();
        }

        static MarkdownWindow Doc(string text, string name)
        {
            ConstructorInfo ci = typeof(MarkdownWindow).GetConstructors(NP)[0];
            MarkdownWindow w = (MarkdownWindow)ci.Invoke(new object[] { null });
            Park(w);
            Pump(100);
            MethodInfo load = typeof(MarkdownWindow).GetMethod("LoadText", NP);
            load.Invoke(w, new object[] { text, name, null });
            Pump(500);
            return w;
        }

        static void CloseDoc(MarkdownWindow w)
        {
            Func<string, string, string[], int> keep = DocAlert.Answer;
            DocAlert.Answer = delegate { return 1; };
            try { w.Close(); } catch { }
            DocAlert.Answer = keep;
            Pump(30);
        }

        static WMI.BitmapSource RenderDocWindow(MarkdownWindow w)
        {
            W.FrameworkElement root = (W.FrameworkElement)w.Content;
            root.UpdateLayout();
            int ww = (int)Math.Ceiling(root.ActualWidth), wh = (int)Math.Ceiling(root.ActualHeight);
            WM.DrawingVisual dv = new WM.DrawingVisual();
            using (WM.DrawingContext dc = dv.RenderOpen()) dc.DrawRectangle(w.Background ?? WM.Brushes.Magenta, null, new W.Rect(0, 0, ww, wh));
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(Math.Max(1, ww), Math.Max(1, wh), 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Render(root);
            rtb.Freeze();
            return rtb;
        }

        const string SampleMd = "# Informe de pruebas\n\nTexto normal con un [enlace](https://example.com) y `c\u00F3digo`.\n\n> Una cita con texto secundario.\n\n- [x] Hecho\n- [ ] Pendiente\n\n| Columna | Valor |\n|---|---|\n| Uno | 1 |\n\n```\nvar x = 1;\n```\n";
        const string SampleXml = "<?xml version=\"1.0\"?>\n<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><!-- nota --><pedido id=\"7\" estado=\"ok\"><cliente>Ana</cliente><importe>12.50</importe></pedido></soap:Body></soap:Envelope>";

        static Editor NewEditor(D.Size size)
        {
            D.Bitmap img = new D.Bitmap(640, 360);
            using (D.Graphics g = D.Graphics.FromImage(img))
            {
                g.Clear(D.Color.FromArgb(200, 210, 230));
                g.FillRectangle(D.Brushes.White, 60, 50, 300, 160);
            }
            Editor ed = new Editor(null, Path.Combine(Dir("editor"), "prueba.png"), img);
            ed.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            ed.Location = new D.Point(-32000, -32000);
            ed.Size = size;
            ed.PerformLayout();
            return ed;
        }

        // The editor's chrome as each control paints itself (no window handle needed): the bar with a tool chosen and a
        // button under the pointer, the backdrop strip, the canvas with the zoom control, and the hint strip.
        static D.Bitmap RenderEditor(D.Size size)
        {
            Editor ed = NewEditor(size);
            try
            {
                Bar bar = (Bar)typeof(Editor).GetField("bar", NP).GetValue(ed);
                BgPanel bg = (BgPanel)typeof(Editor).GetField("bgPanel", NP).GetValue(ed);
                int tool = 0;
                for (int i = 0; i < bar.Items.Count; i++)
                {
                    Bar.Item it = bar.Items[i];
                    if (it.Kind != Bar.Kind.Tool) continue;
                    it.On = tool == 1;
                    if (tool == 3) bar.Pose(i, 1, 0);
                    tool++;
                }
                // The backdrop sidebar (shown with the Fondo button) on the left, with the backdrop on.
                typeof(Editor).GetMethod("ToggleBgPanel", NP).Invoke(ed, null);
                ed.PerformLayout();
                D.Bitmap pic = new D.Bitmap(size.Width, size.Height);
                using (D.Graphics g = D.Graphics.FromImage(pic))
                {
                    g.Clear(D.Color.Magenta);
                    MethodInfo onPaint = typeof(System.Windows.Forms.Control).GetMethod("OnPaint", NP);
                    foreach (System.Windows.Forms.Control c in ed.Controls)
                    {
                        // Visible reads false while the form is hidden: every docked child with a size is drawn.
                        if (c.Width <= 0 || c.Height <= 0) continue;
                        using (D.Bitmap part = new D.Bitmap(c.Width, c.Height))
                        {
                            using (D.Graphics pg = D.Graphics.FromImage(part))
                            {
                                pg.Clear(c.BackColor.A == 255 ? c.BackColor : D.Color.Magenta);
                                onPaint.Invoke(c, new object[] { new System.Windows.Forms.PaintEventArgs(pg, new D.Rectangle(0, 0, c.Width, c.Height)) });
                            }
                            g.DrawImage(part, c.Bounds);
                        }
                    }
                }
                return pic;
            }
            finally { ed.Dispose(); }
        }

        // The heads-up surfaces: an editor tip, the area picker's hint pill and the countdown, over a light and a dark
        // background.
        static D.Bitmap RenderHeads()
        {
            D.Bitmap pic = new D.Bitmap(760, 240);
            using (D.Graphics g = D.Graphics.FromImage(pic))
            {
                g.FillRectangle(D.Brushes.White, 0, 0, 380, 240);
                g.FillRectangle(new D.SolidBrush(D.Color.FromArgb(40, 40, 46)), 380, 0, 380, 240);
                for (int side = 0; side < 2; side++)
                {
                    int x0 = side * 380;
                    using (D.Bitmap tip = EdTip.Preview("Flecha  (A)", 1f)) g.DrawImage(tip, x0 + 10, 10);
                    RegionPicker.PaintHintPreview(g, new D.Point(x0 + 10, 70), 1f, "area", "Arrastra para elegir  ·  Esc: salir");
                }
                Type cd = typeof(RegionPicker).Assembly.GetType("Stackshot.CountdownWindow");
                using (System.Windows.Forms.Form f = (System.Windows.Forms.Form)Activator.CreateInstance(cd, NP | BindingFlags.Public, null, new object[] { new D.Rectangle(-32000, -32000, 300, 300), 3 }, null))
                {
                    ((System.Windows.Forms.Timer)cd.GetField("timer", NP).GetValue(f)).Stop();
                    using (D.Bitmap part = new D.Bitmap(f.ClientSize.Width, f.ClientSize.Height))
                    {
                        using (D.Graphics pg = D.Graphics.FromImage(part))
                        {
                            pg.Clear(f.BackColor);
                            typeof(System.Windows.Forms.Control).GetMethod("OnPaint", NP).Invoke(f, new object[] { new System.Windows.Forms.PaintEventArgs(pg, new D.Rectangle(D.Point.Empty, f.ClientSize)) });
                        }
                        g.DrawImage(part, 200, 115, 110, 110);
                    }
                }
            }
            return pic;
        }

        static readonly string[] Pages = { "home", "history", "keys", "general", "editor", "record", "mascot", "about" };

        static void Snapshots()
        {
            string pic = SamplePicture();
            try
            {
                for (int t = 0; t < 4; t++)
                {
                    Theme(t);
                    string n = ThemeNames[t];
                    Settings s = NewSettings();
                    HomeWindow hw = Home(s);
                    try
                    {
                        foreach (string p in Pages)
                        {
                            SetPage(hw, p);
                            Save(RenderWindow(hw, 980, 1100), "home-" + p + "-" + n + ".png");
                        }
                    }
                    finally { hw.Shutdown(); }
                    // The other two Home styles: Widgets and Escena.
                    for (int style = 1; style <= 2; style++)
                    {
                        Settings ss = NewSettings();
                        ss.HomeStyle = style;
                        HomeWindow hs = Home(ss);
                        try
                        {
                            SetPage(hs, "home");
                            Save(RenderWindow(hs, 980, 760), "home-style" + style + "-" + n + ".png");
                        }
                        finally { hs.Shutdown(); }
                    }
                    Save(RenderTray(s), "tray-" + n + ".png");
                    Save(RenderCard(pic, false), "card-" + n + ".png");
                    Save(RenderCard(pic, true), "card-hover-" + n + ".png");
                    MarkdownWindow md = Doc(SampleMd, "informe.md");
                    try { Save(RenderDocWindow(md), "markdown-" + n + ".png"); }
                    finally { CloseDoc(md); }
                    MarkdownWindow xml = Doc(SampleXml, "pedido.xml");
                    try { Save(RenderDocWindow(xml), "xml-" + n + ".png"); }
                    finally { CloseDoc(xml); }
                    using (D.Bitmap ed = RenderEditor(new D.Size(1100, 700))) Save(ed, "editor-" + n + ".png");
                    using (D.Bitmap hd = RenderHeads()) Save(hd, "heads-" + n + ".png");
                    // The language picker of the 01-aurora artboard, in every theme (a contrast theme must show none of the
                    // treatment), in Spanish, English, Korean and Arabic (right to left).
                    bool mica = Sheet.HasMica;
                    Sheet.HasMica = false;
                    string uiNow = I18n.Language;
                    try
                    {
                        foreach (string ui in new[] { "es", "en", "ko", "ar" })
                        {
                            I18n.Apply(ui);
                            LanguagePicker pk = (LanguagePicker)typeof(LanguagePicker).GetConstructor(NP, null, new[] { typeof(string) }, null).Invoke(new object[] { ui });
                            try { Save(RenderWindow(pk, (int)pk.Width, (int)pk.Height), "picker-" + ui + "-" + n + ".png"); }
                            finally { pk.Close(); }
                        }
                    }
                    finally { I18n.Apply(uiNow); Sheet.HasMica = mica; }
                }
            }
            finally { Restore(); }
            Console.WriteLine("    snapshots in " + Dir("shots"));
        }

        // The player's chrome maps its whites to the system colours only under a contrast theme.
        static void PlayerChrome()
        {
            Type pw = typeof(Editor).Assembly.GetType("Stackshot.PlayerWindow");
            MethodInfo white = pw.GetMethod("ChromeWhite", NS), back = pw.GetMethod("ChromeBack", NS), on = pw.GetMethod("ChromeOn", NS);
            try
            {
                Theme(0);
                Check((D.Color)white.Invoke(null, new object[] { 150, 255, 1.0 }) == D.Color.FromArgb(150, 255, 255, 255), "player white changed without a contrast theme");
                Check((D.Color)back.Invoke(null, new object[] { 214, 30, 30, 32, 0.5 }) == D.Color.FromArgb(107, 30, 30, 32), "player bar changed without a contrast theme");
                Same(Ds.WithAlpha(Palette.HudAccent, 1), (WM.Color)on.Invoke(null, new object[] { 1.0 }), "player on-button colour");
                ThemeWith(HcWhite(), 1);
                Check(((D.Color)white.Invoke(null, new object[] { 235, 255, 1.0 })).ToArgb() == D.Color.Black.ToArgb(), "player text is not WindowText");
                Check(((D.Color)white.Invoke(null, new object[] { 64, 255, 1.0 })).ToArgb() == D.Color.FromArgb(0x60, 0, 0).ToArgb(), "player track is not GrayText");
                Check(((D.Color)back.Invoke(null, new object[] { 214, 30, 30, 32, 1.0 })).ToArgb() == D.Color.White.ToArgb(), "player bar is not Window");
                Same(Hex(0x37006E), (WM.Color)on.Invoke(null, new object[] { 1.0 }), "player on-button under a contrast theme");
            }
            finally { Restore(); }
        }

        static string RowName(System.Windows.Controls.ListBox list, string start)
        {
            foreach (object o in list.Items)
            {
                System.Windows.Controls.ListBoxItem it = (System.Windows.Controls.ListBoxItem)o;
                string name = W.Automation.AutomationProperties.GetName(it);
                if (name != null && name.StartsWith(start, StringComparison.Ordinal)) return name;
            }
            return null;
        }

        // Deutsch and 한국어 read complete in every interface language ("Completo" / "Complete" / "Vollständig" / "번역 완료"),
        // the previews stay previews, and no row keeps the old "partial translation" line.
        static void LanguageRows()
        {
            string save = I18n.Language;
            try
            {
                foreach (string ui in new[] { "es", "en", "de", "ko" })
                {
                    I18n.Apply(ui);
                    string complete = I18n.T("Completo", "Complete", "Vollst\u00E4ndig");
                    Check((ui != "de" && ui != "ko") || complete != "Complete", "no word for 'complete' in " + ui);
                    LanguagePicker picker = (LanguagePicker)typeof(LanguagePicker).GetConstructor(NP, null, new[] { typeof(string) }, null).Invoke(new object[] { "de" });
                    try
                    {
                        System.Windows.Controls.ListBox list = (System.Windows.Controls.ListBox)typeof(LanguagePicker).GetField("list", NP).GetValue(picker);
                        string row = RowName(list, "Deutsch");
                        Check(row != null, "there is no Deutsch row");
                        string wanted = "Deutsch, " + complete + " \u00B7 de \u00B7 " + I18n.T("Actual", "Current");
                        Check(row == wanted, "Deutsch's row in " + ui + ": '" + row + "' instead of '" + wanted + "'");
                        string ko = RowName(list, "\uD55C\uAD6D\uC5B4");
                        Check(ko == "\uD55C\uAD6D\uC5B4, " + complete + " \u00B7 ko", "the Korean row in " + ui + ": '" + ko + "'");
                        Check(RowName(list, "Fran\u00E7ais") == null && list.Items.Count == 5, "the hidden French row is listed in " + ui);
                    }
                    finally { picker.Close(); }
                }
            }
            finally { I18n.Apply(save); Drain(); }
            // With the switch flipped the previews are listed again, as previews.
            bool listed = I18n.PreviewsListed;
            I18n.PreviewsListed = true;
            try
            {
                foreach (string ui in new[] { "es", "en", "de", "ko" })
                {
                    I18n.Apply(ui);
                    LanguagePicker picker = (LanguagePicker)typeof(LanguagePicker).GetConstructor(NP, null, new[] { typeof(string) }, null).Invoke(new object[] { "de" });
                    try
                    {
                        System.Windows.Controls.ListBox list = (System.Windows.Controls.ListBox)typeof(LanguagePicker).GetField("list", NP).GetValue(picker);
                        string fr = RowName(list, "Fran\u00E7ais");
                        Check(fr == "Fran\u00E7ais, " + I18n.T("Vista previa", "Preview") + " \u00B7 fr", "the French preview row in " + ui + ": '" + fr + "'");
                    }
                    finally { picker.Close(); }
                }
            }
            finally { I18n.PreviewsListed = listed; I18n.Apply(save); Drain(); }
        }

        // The Aurora treatment keeps AA wherever it paints, shows the gradient in dark and light, and a contrast theme
        // turns all of it off (plain system colours); the untouched primary keeps its old vertical gradient.
        static void AuroraContrast()
        {
            try
            {
                for (int t = 0; t < 2; t++)
                {
                    Theme(t);
                    Palette p = Ds.Brushes;
                    Check(!p.High, "a contrast palette without a contrast theme");
                    // The gradient primary's white label: both ends, hovered as well (the ends are never lighter).
                    Check(Ratio(WM.Colors.White, Aurora.Primary1) >= 4.5, "white on the aurora primary's indigo end is under 4.5");
                    Check(Ratio(WM.Colors.White, p.Accent) >= 4.5, "white on the aurora primary's accent end is under 4.5");
                    Check(Ratio(WM.Colors.White, Mix(Aurora.Primary1, WM.Colors.White, 0.06)) >= 4.5, "white on the hovered indigo end is under 4.5");
                    Check(Ratio(WM.Colors.White, Mix(p.Accent, WM.Colors.White, 0.02)) >= 4.5, "white on the hovered accent end is under 4.5");
                    // The selected row's texts on its wash over the thumbnail colour (the strongest stops).
                    WM.Color topFill = Over(Ds.WithAlpha(Aurora.Brand1, p.Dark ? 0.16 : 0.10), p.Thumb);
                    WM.Color lowFill = Over(Ds.WithAlpha(Aurora.Brand3, 0.10), p.Thumb);
                    foreach (WM.Color bg in new[] { topFill, lowFill })
                    {
                        Check(Ratio(p.Label, bg) >= 4.5, "the selected row's label is under 4.5 on " + bg);
                        Check(Ratio(p.Label2, bg) >= 4.5, "the selected row's secondary text is under 4.5 on " + bg);
                    }
                    // Rendered: the gradient primary really grades from indigo into the accent, and the selected row's
                    // ring really is the brand gradient.
                    MacButton pri = new MacButton("Aplicar idioma", ButtonKind.Primary);
                    pri.BrandPrimary = true;
                    WMI.BitmapSource g = RenderVisual(pri, (int)pri.Width, (int)pri.Height);
                    WM.Color left = Pixel(g, (int)(pri.Width * 0.12), (int)(pri.Height * 0.18));
                    WM.Color right = Pixel(g, (int)(pri.Width * 0.88), (int)(pri.Height * 0.18));
                    Check(left != right, "the aurora primary has no gradient: " + left + " == " + right);
                    Aurora.Ring ring = new Aurora.Ring { Width = 160, Height = 40, Selected = true };
                    WMI.BitmapSource rg = RenderVisual(ring, 160, 40);
                    Check(Pixel(rg, 12, 20) != Pixel(rg, 148, 20), "the selected row's ring has no gradient");
                    // The old primary keeps its vertical accent gradient (the aurora one is opt-in); the inner glow bends
                    // both sampled pixels a few units, so they are compared with a small tolerance.
                    MacButton plain = new MacButton("Aceptar", ButtonKind.Primary);
                    WMI.BitmapSource og = RenderVisual(plain, (int)plain.Width, (int)plain.Height);
                    WM.Color topPx = Pixel(og, (int)(plain.Width / 2), 4), bottomPx = Pixel(og, (int)(plain.Width / 2), (int)plain.Height - 5);
                    Check(Near(topPx, p.Accent, 12), "the plain primary's top is not the accent: " + topPx);
                    Check(Near(bottomPx, Mix(p.Accent, WM.Colors.Black, 0.16), 12), "the plain primary's bottom changed: " + bottomPx);
                }
                // A contrast theme: flat, nothing translucent, no band.
                ThemeWith(HcBlack(), 1);
                Check(Ds.Brushes.High, "the contrast theme did not come on");
                MacButton hc = new MacButton("Aplicar idioma", ButtonKind.Primary);
                hc.BrandPrimary = true;
                WMI.BitmapSource hg = RenderVisual(hc, (int)hc.Width, (int)hc.Height);
                WM.Color hl = Pixel(hg, (int)(hc.Width * 0.12), (int)(hc.Height * 0.18)), hrEnd = Pixel(hg, (int)(hc.Width * 0.88), (int)(hc.Height * 0.18));
                Check(hl == hrEnd, "the primary kept a gradient under a contrast theme: " + hl + " vs " + hrEnd);
                Aurora.Band band = new Aurora.Band { Width = 200, Height = 90 };
                WMI.BitmapSource hb = RenderVisual(band, 200, 90);
                Check(Pixel(hb, 100, 45).A == 0, "the aurora band drew a glow under a contrast theme");
                Aurora.Ring hring = new Aurora.Ring { Width = 160, Height = 40, Selected = true };
                WMI.BitmapSource hrg = RenderVisual(hring, 160, 40);
                WM.Color r1 = Pixel(hrg, 12, 20), r2 = Pixel(hrg, 148, 20);
                Check(r1 == r2, "the selected row's ring kept a gradient under a contrast theme: " + r1 + " vs " + r2);
                Check(r1.A == 0 || r1 == Ds.Brushes.Accent || r1 == Ds.Brushes.Window, "the ring under a contrast theme is neither plain nor empty: " + r1);
            }
            finally { Restore(); }
        }

        // The picker's room (owner: "está muy apretado"): rows 56 tall with a gap between them so a selected ring never
        // meets the hover of its neighbour, the ring clear of the scrollbar, and the whole sheet inside a laptop's area.
        // Measured with the ten languages listed: five rows never reach the scrollbar, and the room has to hold when the
        // previews come back.
        static void PickerRoom()
        {
            bool listed = I18n.PreviewsListed;
            I18n.PreviewsListed = true;
            try { PickerRoomListed(); }
            finally { I18n.PreviewsListed = listed; }
        }

        static void PickerRoomListed()
        {
            W.Size fit = LanguagePicker.Preferred(1366, 728);
            Check(fit.Width <= 1366 && fit.Height <= 728, "the sheet does not fit a 1366 x 728 work area: " + fit);
            Check(fit.Width > 480 || fit.Height > 540, "the sheet was given no more room: " + fit);
            LanguagePicker picker = (LanguagePicker)typeof(LanguagePicker).GetConstructor(NP, null, new[] { typeof(string) }, null).Invoke(new object[] { "en" });
            try
            {
                W.FrameworkElement content = (W.FrameworkElement)picker.Content;
                content.Measure(new W.Size(picker.Width, picker.Height));
                content.Arrange(new W.Rect(0, 0, picker.Width, picker.Height));
                content.UpdateLayout();
                System.Windows.Controls.ListBox list = (System.Windows.Controls.ListBox)typeof(LanguagePicker).GetField("list", NP).GetValue(picker);
                double bottom = double.NaN, left = double.NaN, right = double.NaN;
                int rows = 0;
                foreach (object o in list.Items)
                {
                    System.Windows.Controls.ListBoxItem item = o as System.Windows.Controls.ListBoxItem;
                    if (item == null || item.ActualHeight <= 0) continue;   // past the list's edge: not laid out
                    Aurora.Ring ring = item.Content as Aurora.Ring;
                    Check(ring != null, "a row without its ring");
                    Check(item.ActualHeight >= 56, "a row is under 56 px tall: " + item.ActualHeight);
                    W.Rect r = Bounds(ring, content);
                    if (!double.IsNaN(bottom)) Check(r.Top - bottom >= 4, "two rows are under 4 px apart: " + (r.Top - bottom));
                    bottom = r.Bottom;
                    if (double.IsNaN(left)) left = r.Left;
                    right = r.Right;
                    rows++;
                }
                Check(rows >= 6, "only " + rows + " rows were laid out");
                Check(left >= 8, "the ring runs into the list's edge: " + left);
                // The ring of the bottom row never slides under the vertical bar: the gutter between them.
                System.Windows.Controls.Primitives.ScrollBar bar = VertBar(list);
                Check(bar != null, "the list shows no vertical scrollbar");
                double gutter = Bounds(bar, content).Left - right;
                Check(gutter >= 8, "the ring is " + gutter + " px from the scrollbar, under 8");
            }
            finally { picker.Close(); }
        }

        // An element's rectangle in the coordinates of one of its ancestors.
        static W.Rect Bounds(W.FrameworkElement e, WM.Visual ancestor)
        {
            return e.TransformToAncestor(ancestor).TransformBounds(new W.Rect(0, 0, e.ActualWidth, e.ActualHeight));
        }

        // The vertical bar of a list (its horizontal one is disabled).
        static System.Windows.Controls.Primitives.ScrollBar VertBar(W.DependencyObject o)
        {
            System.Windows.Controls.Primitives.ScrollBar b = o as System.Windows.Controls.Primitives.ScrollBar;
            if (b != null) return b.Orientation == System.Windows.Controls.Orientation.Vertical ? b : null;
            for (int i = 0; i < WM.VisualTreeHelper.GetChildrenCount(o); i++)
            {
                b = VertBar(WM.VisualTreeHelper.GetChild(o, i));
                if (b != null) return b;
            }
            return null;
        }

        // One element rendered into pixels without a window.
        static WMI.RenderTargetBitmap RenderVisual(W.Media.Visual v, int w, int h)
        {
            W.FrameworkElement fe = v as W.FrameworkElement;
            if (fe != null)
            {
                fe.Measure(new W.Size(w, h));
                fe.Arrange(new W.Rect(0, 0, w, h));
            }
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(w, h, 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(v);
            rtb.Freeze();
            return rtb;
        }

        static bool Near(WM.Color a, WM.Color b, int tol = 0)
        {
            return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;
        }

        public static void Suite()
        {
            AppTests.Test(S, "contrast of every text colour on its surfaces, light and dark (table)", ContrastTable);
            AppTests.Test(S, "contrast-theme palette maps every role to the injected system colours", PaletteFromSystem);
            AppTests.Test(S, "without a contrast theme only the secondary greys and the accent changed", NothingElseChanges);
            AppTests.Test(S, "live switch: Home, the Markdown window and the editor follow, and come back", LiveSwitch);
            AppTests.Test(S, "captures and exports are the same under a contrast theme", ExportsUnchanged);
            AppTests.Test(S, "Deutsch and Korean read complete in every language and the previews stay previews", LanguageRows);
            AppTests.Test(S, "the picker's rows are taller and apart and the sheet still fits a laptop", PickerRoom);
            AppTests.Test(S, "the aurora treatment keeps AA and a contrast theme draws none of it", AuroraContrast);
            AppTests.Test(S, "player chrome follows only under a contrast theme", PlayerChrome);
            AppTests.Test(S, "surfaces rendered in light, dark and high contrast", Snapshots);
        }
    }
}
