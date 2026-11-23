// Stackshot - Global hotkeys (RegisterHotKey), their display text and the rules for recording a new one.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    // Message-only window that receives registered hotkeys and raises the action name.
    public class Hotkeys : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        public event Action<string> Pressed;
        readonly Dictionary<int, string> ids = new Dictionary<int, string>();
        int next = 1;
        // Combos another program held at the last registration, by action (the Keys page marks them).
        static readonly Dictionary<string, List<string>> busy = new Dictionary<string, List<string>>();

        public Hotkeys()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        // Registers every combo of an action ("PrintScreen, Ctrl+Shift+4"). Returns the ones that failed.
        public List<string> Register(string action, string combos)
        {
            List<string> failed = new List<string>(), taken = new List<string>();
            foreach (string c in Split(combos))
            {
                uint mods;
                Keys key;
                if (!TryParse(c, out mods, out key)) { failed.Add(c); continue; }
                int id = next++;
                if (Native.RegisterHotKey(Handle, id, mods | MOD_NOREPEAT, (uint)key)) ids[id] = action;
                else { failed.Add(c); taken.Add(c); }
            }
            if (taken.Count > 0) busy[action] = taken;
            else busy.Remove(action);
            return failed;
        }

        public void Clear()
        {
            foreach (int id in ids.Keys) Native.UnregisterHotKey(Handle, id);
            ids.Clear();
            busy.Clear();
        }

        // True when the combo could not be registered for that action because something else holds it.
        public static bool IsBusy(string action, string combo)
        {
            List<string> l;
            if (action == null || !busy.TryGetValue(action, out l)) return false;
            foreach (string c in l) if (Same(c, combo)) return true;
            return false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                string action;
                if (ids.TryGetValue(m.WParam.ToInt32(), out action) && Pressed != null) Pressed(action);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Clear();
            DestroyHandle();
        }

        public static List<string> Split(string combos)
        {
            List<string> list = new List<string>();
            foreach (string c in (combos ?? "").Split(','))
            {
                string t = c.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }

        // ---- Parsing and the stored form

        // "Ctrl+Shift+PrintScreen" -> modifiers + key. Spanish and German display names are accepted too.
        public static bool TryParse(string combo, out uint mods, out Keys key)
        {
            mods = 0;
            key = Keys.None;
            if (combo == null) return false;
            foreach (string part in combo.Split('+'))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                if (p == "ctrl" || p == "control" || p == "strg") mods |= MOD_CONTROL;
                else if (p == "shift" || p == "may\u00FAs" || p == "mayus" || p == "umschalt") mods |= MOD_SHIFT;
                else if (p == "alt") mods |= MOD_ALT;
                else if (p == "altgr") mods |= MOD_CONTROL | MOD_ALT;
                else if (p == "win" || p == "windows") mods |= MOD_WIN;
                else
                {
                    Keys k = KeyFromName(p);
                    if (k == Keys.None || key != Keys.None) return false; // unknown, or two keys
                    key = k;
                }
            }
            return key != Keys.None;
        }

        static Keys KeyFromName(string p)
        {
            switch (p)
            {
                case "impr pant": case "imprpant": case "printscreen": case "prtsc": case "prtscn": case "druck": return Keys.PrintScreen;
                case "esc": return Keys.Escape;
                case "supr": case "entf": return Keys.Delete;
                case "espacio": case "leertaste": return Keys.Space;
                case "intro": case "enter": case "eingabe": return Keys.Return;
                case "retroceso": case "rücktaste": return Keys.Back;
                case "pausa": return Keys.Pause;
                case "inicio": case "pos1": return Keys.Home;
                case "fin": case "ende": return Keys.End;
                case "ins": case "einfg": return Keys.Insert;
                case "re p\u00E1g": case "bild auf": return Keys.PageUp;
                case "av p\u00E1g": case "bild ab": return Keys.PageDown;
                case "menü": return Keys.Apps;
                case "feststelltaste": return Keys.Capital;
                case "rollen": return Keys.Scroll;
            }
            if (p.Length == 1 && p[0] >= '0' && p[0] <= '9') return Keys.D0 + (p[0] - '0');
            if (char.IsDigit(p[0]) || p[0] == '-') return Keys.None; // Enum.TryParse would take any number
            Keys k;
            if (!Enum.TryParse(p, true, out k) || !Enum.IsDefined(typeof(Keys), k)) return Keys.None;
            // Modifier flags, the modifier keys themselves, mouse buttons and the codes that stand for other input (IME,
            // injected text, the KeyCode mask) are not keys of a combo.
            if ((k & ~Keys.KeyCode) != 0 || (int)k > 0xFE || IsModifierKey(k) || k == Keys.LButton || k == Keys.RButton || k == Keys.MButton ||
                k == Keys.XButton1 || k == Keys.XButton2 || k == Keys.ProcessKey || k == Keys.Packet) return Keys.None;
            return k;
        }

        public static bool IsModifierKey(Keys k)
        {
            return k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu || k == Keys.LShiftKey || k == Keys.RShiftKey ||
                   k == Keys.LControlKey || k == Keys.RControlKey || k == Keys.LMenu || k == Keys.RMenu || k == Keys.LWin || k == Keys.RWin;
        }

        // Stable, English form stored in settings.
        public static string ToSetting(Keys keyData) { return ToSetting(keyData, false); }

        public static string ToSetting(Keys keyData, bool win)
        {
            uint mods = (win ? MOD_WIN : 0) | ((keyData & Keys.Control) != 0 ? MOD_CONTROL : 0) |
                        ((keyData & Keys.Shift) != 0 ? MOD_SHIFT : 0) | ((keyData & Keys.Alt) != 0 ? MOD_ALT : 0);
            return Stored(mods, keyData & Keys.KeyCode);
        }

        static string Stored(uint mods, Keys key)
        {
            StringBuilder s = new StringBuilder();
            if ((mods & MOD_WIN) != 0) s.Append("Win+");
            if ((mods & MOD_CONTROL) != 0) s.Append("Ctrl+");
            if ((mods & MOD_SHIFT) != 0) s.Append("Shift+");
            if ((mods & MOD_ALT) != 0) s.Append("Alt+");
            if (key >= Keys.D0 && key <= Keys.D9) s.Append((char)('0' + (key - Keys.D0)));
            else if (key == Keys.PrintScreen) s.Append("PrintScreen"); // several names share these values
            else if (key == Keys.Return) s.Append("Enter");
            else if (key == Keys.PageUp) s.Append("PageUp");
            else if (key == Keys.PageDown) s.Append("PageDown");
            else if (key == Keys.Capital) s.Append("CapsLock");
            else s.Append(key);
            return s.ToString();
        }

        // The stored form of a combo however it was written ("mayus+ctrl+4" -> "Ctrl+Shift+4"); unreadable ones as they are.
        public static string Normalize(string combo)
        {
            uint mods;
            Keys key;
            return TryParse(combo, out mods, out key) ? Stored(mods, key) : (combo ?? "").Trim();
        }

        // Same keys whatever the order or spelling.
        public static bool Same(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        // ---- Display

        // Localized display form of the first combo, e.g. "Ctrl + Mayus + Impr Pant".
        public static string Display(string combos)
        {
            List<string> parts = Split(combos);
            if (parts.Count == 0) return I18n.T("Sin atajo", "No shortcut");
            return DisplayOne(parts[0]);
        }

        internal static string CtrlF { get { return (I18n.IsGerman ? "Strg" : "Ctrl") + "+F"; } }

        public static string DisplayOne(string combo)
        {
            uint mods;
            Keys key;
            if (!TryParse(combo, out mods, out key)) return (combo ?? "").Trim();
            return Joined((mods & MOD_CONTROL) != 0, (mods & MOD_SHIFT) != 0, (mods & MOD_ALT) != 0, (mods & MOD_WIN) != 0, key);
        }

        static string Joined(bool ctrl, bool shift, bool alt, bool win, Keys key)
        {
            List<string> s = new List<string>();
            if (win) s.Add("Win");
            if (ctrl) s.Add(I18n.IsGerman ? "Strg" : "Ctrl");
            if (shift) s.Add(I18n.T("May\u00FAs", "Shift", "Umschalt"));
            if (alt) s.Add("Alt");
            if (key != Keys.None) s.Add(KeyName(key));
            return string.Join(" + ", s.ToArray());
        }

        // Modifiers being held, for the field while it records ("Ctrl + Mayus").
        public static string DisplayMods(bool ctrl, bool shift, bool alt, bool win)
        {
            return Joined(ctrl, shift, alt, win, Keys.None);
        }

        // The name printed on the key, as on a Spanish keyboard; punctuation as the current layout prints it.
        public static string KeyName(Keys key)
        {
            switch (key)
            {
                case Keys.PrintScreen: return I18n.T("Impr Pant", "PrtScn", "Druck");
                case Keys.Escape: return "Esc";
                case Keys.Space: return I18n.T("Espacio", "Space", "Leertaste");
                case Keys.Return: return I18n.T("Intro", "Enter", "Eingabe");
                case Keys.Back: return I18n.T("Retroceso", "Backspace", "R\u00FCcktaste");
                case Keys.Tab: return "Tab";
                case Keys.Delete: return I18n.T("Supr", "Del", "Entf");
                case Keys.Insert: return I18n.T("Insert", "Insert", "Einfg");
                case Keys.Home: return I18n.T("Inicio", "Home", "Pos1");
                case Keys.End: return I18n.T("Fin", "End", "Ende");
                case Keys.PageUp: return I18n.T("Re P\u00E1g", "Page Up", "Bild auf");
                case Keys.PageDown: return I18n.T("Av P\u00E1g", "Page Down", "Bild ab");
                case Keys.Left: return "\u2190";
                case Keys.Right: return "\u2192";
                case Keys.Up: return "\u2191";
                case Keys.Down: return "\u2193";
                case Keys.Pause: case Keys.Cancel: return I18n.T("Pausa", "Pause", "Pause");  // Ctrl + Pausa arrives as Cancel
                case Keys.Scroll: return I18n.T("Bloq Despl", "Scroll Lock", "Rollen");
                case Keys.Capital: return I18n.T("Bloq May\u00FAs", "Caps Lock", "Feststelltaste");
                case Keys.NumLock: return I18n.T("Bloq Num", "Num Lock", "Num");
                case Keys.Apps: return I18n.T("Men\u00FA", "Menu", "Men\u00FC");
                case Keys.Clear: return "Num 5";
                case Keys.Multiply: return "Num *";
                case Keys.Add: return "Num +";
                case Keys.Subtract: return "Num -";
                case Keys.Divide: return "Num /";
                case Keys.Decimal: return "Num .";
                case Keys.Separator: return "Num ,";
                case Keys.VolumeMute: return I18n.T("Silenciar", "Mute", "Stumm");
                case Keys.VolumeDown: return I18n.T("Bajar volumen", "Volume Down", "Leiser");
                case Keys.VolumeUp: return I18n.T("Subir volumen", "Volume Up", "Lauter");
                case Keys.MediaPlayPause: return I18n.T("Reproducir", "Play/Pause", "Wiedergabe/Pause");
                case Keys.MediaNextTrack: return I18n.T("Siguiente", "Next Track", "Weiter");
                case Keys.MediaPreviousTrack: return I18n.T("Anterior", "Previous Track", "Zur\u00FCck");
                case Keys.MediaStop: return I18n.T("Detener", "Stop", "Stopp");
                case Keys.BrowserBack: return I18n.T("Atr\u00E1s", "Back", "Zur\u00FCck");
                case Keys.BrowserForward: return I18n.T("Adelante", "Forward", "Weiter");
                case Keys.BrowserRefresh: return I18n.T("Actualizar", "Refresh", "Aktualisieren");
                case Keys.BrowserStop: return I18n.T("Detener carga", "Stop loading", "Laden beenden");
                case Keys.BrowserSearch: return I18n.T("Buscar", "Search", "Suchen");
                case Keys.BrowserFavorites: return I18n.T("Favoritos", "Favorites", "Favoriten");
                case Keys.BrowserHome: return I18n.T("Inicio web", "Browser Home", "Startseite");
                case Keys.LaunchMail: return I18n.T("Correo", "Mail", "E-Mail");
                case Keys.LaunchApplication1: return I18n.T("Aplicaci\u00F3n 1", "App 1", "App 1");
                case Keys.LaunchApplication2: return I18n.T("Calculadora", "Calculator", "Rechner");
                case Keys.SelectMedia: return I18n.T("Multimedia", "Media", "Medien");
                case Keys.Sleep: return I18n.IsKorean ? "\uC808\uC804" : I18n.T("Suspender", "Sleep", "Ruhezustand");
            }
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num " + (char)('0' + (key - Keys.NumPad0));
            if (key >= Keys.A && key <= Keys.Z) return ((char)('A' + (key - Keys.A))).ToString();
            if (key >= Keys.F1 && key <= Keys.F24) return key.ToString();
            return Printed(key) ?? Oem(key) ?? key.ToString();
        }

        // What the key types on the current layout (punctuation, letters such as the Spanish enye, dead accent keys).
        static string Printed(Keys key)
        {
            try
            {
                uint c = MapVirtualKeyEx((uint)key, 2, GetKeyboardLayout(0)) & 0xFFFF; // MAPVK_VK_TO_CHAR; the dead-key bit is dropped
                if (c > 32 && c != 127 && !char.IsControl((char)c)) return char.ToUpperInvariant((char)c).ToString();
            }
            catch { }
            return null;
        }

        // Fallback names of the punctuation keys (US layout), should the layout not say.
        static string Oem(Keys key)
        {
            switch (key)
            {
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "+";
                case Keys.Oem1: return ";";
                case Keys.Oem2: return "/";
                case Keys.Oem3: return "`";
                case Keys.Oem4: return "[";
                case Keys.Oem5: return "\\";
                case Keys.Oem6: return "]";
                case Keys.Oem7: return "'";
                case Keys.Oem102: return "<";
                case Keys.OemClear: return I18n.T("Borrar", "Clear");
            }
            return null;
        }

        // ---- Recording a new combo

        // Keys that make sense as a global hotkey on their own.
        public static bool StandsAlone(Keys k)
        {
            return (k >= Keys.F1 && k <= Keys.F24) || k == Keys.PrintScreen || k == Keys.Pause || k == Keys.Scroll || k == Keys.Insert ||
                   k == Keys.Apps || k == Keys.LaunchApplication1 || k == Keys.LaunchApplication2;
        }

        // Why a combo would be a bad global hotkey (it would stop working everywhere else), or null if it is fine.
        public static string Problem(Keys key, Keys modifiers, bool win)
        {
            bool ctrl = (modifiers & Keys.Control) != 0, shift = (modifiers & Keys.Shift) != 0, alt = (modifiers & Keys.Alt) != 0;
            if (key == Keys.Capital || key == Keys.NumLock) return I18n.Fmt("{0} no sirve de atajo", "{0} cannot be a shortcut", KeyName(key));
            if (!win)
            {
                string what = null;
                // Tab switches windows with Alt and tabs with Ctrl, backwards with Shift too. Only the part that does it is
                // named: the whole combination would not fit the field.
                if (key == Keys.Tab && (ctrl || alt)) return alt ? I18n.T("Alt + Tab cambia de ventana", "Alt + Tab switches windows") : I18n.T("Ctrl + Tab cambia de pesta\u00F1a", "Ctrl + Tab switches tabs");
                if (ctrl && !shift && !alt)
                {
                    switch (key)
                    {
                        case Keys.A: what = I18n.T("Seleccionar todo", "Select all"); break;
                        case Keys.C: case Keys.Insert: what = I18n.Copy; break;
                        case Keys.V: what = I18n.T("Pegar", "Paste"); break;
                        case Keys.X: what = I18n.T("Cortar", "Cut"); break;
                        case Keys.Z: what = I18n.T("Deshacer", "Undo"); break;
                        case Keys.Y: what = I18n.T("Rehacer", "Redo"); break;
                        case Keys.S: what = I18n.Save; break;
                        case Keys.F: what = I18n.T("Buscar", "Find"); break;
                        case Keys.P: what = I18n.T("Imprimir", "Print"); break;
                        case Keys.N: what = I18n.T("Nuevo", "New"); break;
                        case Keys.O: what = I18n.Open; break;
                        case Keys.W: what = I18n.Close; break;
                        case Keys.T: what = I18n.T("Nueva pesta\u00F1a", "New tab"); break;
                    }
                }
                else if (shift && !ctrl && !alt)
                {
                    if (key == Keys.Insert) what = I18n.T("Pegar", "Paste");
                    else if (key == Keys.Delete) what = I18n.T("Cortar", "Cut");
                    else if (key == Keys.F10) what = I18n.T("Men\u00FA contextual", "Context menu");
                }
                else if (alt && !ctrl && !shift)
                {
                    switch (key)
                    {
                        case Keys.F4: what = I18n.T("Cerrar ventana", "Close window"); break;
                        case Keys.Space: what = I18n.T("Men\u00FA de ventana", "Window menu"); break;
                        case Keys.Left: what = I18n.T("Atr\u00E1s", "Back"); break;
                        case Keys.Right: what = I18n.T("Adelante", "Forward"); break;
                    }
                }
                if (what != null) return I18n.Fmt("{0} ya es {1}", "{0} is already {1}", Joined(ctrl, shift, alt, false, key), what);
            }
            // Shift alone is not enough: Shift + a letter is just a capital letter.
            if (!ctrl && !alt && !win && !StandsAlone(key)) return I18n.T("Comb\u00EDnala con Ctrl o Alt", "Combine with Ctrl or Alt");
            if (!win)
            {
                if (!alt && Typing(key)) return I18n.T("Se usa al escribir: prueba otra", "Used while typing: try another");
                // On layouts with AltGr (Spanish among them) Ctrl + Alt + a key types a character such as @ or #.
                if (ctrl && alt)
                {
                    string ch = AltGrChar(key, shift);
                    if (ch != null) return I18n.Fmt("As\u00ED escribes \u00AB{0}\u00BB: prueba otra", "This types \u201C{0}\u201D: try another", ch);
                }
            }
            return null;
        }

        // Keys that move or edit text (with Ctrl or Shift they select words, jump, delete...).
        static bool Typing(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || k == Keys.Home || k == Keys.End || k == Keys.PageUp ||
                   k == Keys.PageDown || k == Keys.Back || k == Keys.Delete || k == Keys.Tab || k == Keys.Return || k == Keys.Space;
        }

        // The character Ctrl + Alt (AltGr) + key types on the current layout, or null.
        public static string AltGrChar(Keys key, bool shift)
        {
            try
            {
                byte[] state = new byte[256];
                state[0x11] = state[0xA2] = 0x80; // Ctrl, left Ctrl
                state[0x12] = state[0xA5] = 0x80; // Alt, right Alt (AltGr)
                if (shift) state[0x10] = state[0xA0] = 0x80;
                IntPtr hkl = GetKeyboardLayout(0);
                StringBuilder sb = new StringBuilder(8);
                // Flag 4: leave the keyboard's dead-key state alone.
                int n = ToUnicodeEx((uint)key, MapVirtualKeyEx((uint)key, 0, hkl), state, sb, sb.Capacity, 4, hkl);
                if (n == 0 || sb.Length == 0) return null;
                string s = sb.ToString(0, Math.Max(1, Math.Min(Math.Abs(n), sb.Length))).Trim();
                if (s.Length == 0 || char.IsControl(s[0])) return null;
                return s;
            }
            catch { return null; }
        }

        // The non-modifier key being held, for keys that reach the window without their code (dead keys, IME).
        public static Keys HeldKey()
        {
            byte[] s = new byte[256];
            try { if (!GetKeyboardState(s)) return Keys.None; }
            catch { return Keys.None; }
            for (int vk = 8; vk < 0xFF; vk++)
            {
                Keys k = (Keys)vk;
                if ((s[vk] & 0x80) == 0 || IsModifierKey(k) || k == Keys.ProcessKey || k == Keys.Packet || k == Keys.Capital || k == Keys.NumLock) continue;
                return k;
            }
            return Keys.None;
        }

        [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr hkl);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int size, uint flags, IntPtr hkl);
    }
}
