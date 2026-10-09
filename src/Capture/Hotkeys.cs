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

        // "Ctrl+Shift+PrintScreen" -> modifiers + key. Spanish names are accepted too.
        public static bool TryParse(string combo, out uint mods, out Keys key)
        {
            mods = 0;
            key = Keys.None;
            if (combo == null) return false;
            foreach (string part in combo.Split('+'))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                if (p == "ctrl" || p == "control") mods |= MOD_CONTROL;
                else if (p == "shift" || p == "may\u00FAs" || p == "mayus") mods |= MOD_SHIFT;
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
                case "impr pant": case "imprpant": case "printscreen": case "prtsc": case "prtscn": return Keys.PrintScreen;
                case "esc": return Keys.Escape;
                case "supr": return Keys.Delete;
                case "espacio": return Keys.Space;
                case "intro": case "enter": return Keys.Return;
                case "retroceso": return Keys.Back;
                case "pausa": return Keys.Pause;
                case "inicio": return Keys.Home;
                case "fin": return Keys.End;
                case "ins": return Keys.Insert;
                case "re p\u00E1g": return Keys.PageUp;
                case "av p\u00E1g": return Keys.PageDown;
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
            if (parts.Count == 0) return "Sin atajo";
            return DisplayOne(parts[0]);
        }

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
            if (ctrl) s.Add("Ctrl");
            if (shift) s.Add("May\u00FAs");
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
                case Keys.PrintScreen: return "Impr Pant";
                case Keys.Escape: return "Esc";
                case Keys.Space: return "Espacio";
                case Keys.Return: return "Intro";
                case Keys.Back: return "Retroceso";
                case Keys.Tab: return "Tab";
                case Keys.Delete: return "Supr";
                case Keys.Insert: return "Insert";
                case Keys.Home: return "Inicio";
                case Keys.End: return "Fin";
                case Keys.PageUp: return "Re P\u00E1g";
                case Keys.PageDown: return "Av P\u00E1g";
                case Keys.Left: return "\u2190";
                case Keys.Right: return "\u2192";
                case Keys.Up: return "\u2191";
                case Keys.Down: return "\u2193";
                case Keys.Pause: case Keys.Cancel: return "Pausa";  // Ctrl + Pausa arrives as Cancel
                case Keys.Scroll: return "Bloq Despl";
                case Keys.Capital: return "Bloq May\u00FAs";
                case Keys.NumLock: return "Bloq Num";
                case Keys.Apps: return "Men\u00FA";
                case Keys.Clear: return "Num 5";
                case Keys.Multiply: return "Num *";
                case Keys.Add: return "Num +";
                case Keys.Subtract: return "Num -";
                case Keys.Divide: return "Num /";
                case Keys.Decimal: return "Num .";
                case Keys.Separator: return "Num ,";
                case Keys.VolumeMute: return "Silenciar";
                case Keys.VolumeDown: return "Bajar volumen";
                case Keys.VolumeUp: return "Subir volumen";
                case Keys.MediaPlayPause: return "Reproducir";
                case Keys.MediaNextTrack: return "Siguiente";
                case Keys.MediaPreviousTrack: return "Anterior";
                case Keys.MediaStop: return "Detener";
                case Keys.BrowserBack: return "Atr\u00E1s";
                case Keys.BrowserForward: return "Adelante";
                case Keys.BrowserRefresh: return "Actualizar";
                case Keys.BrowserStop: return "Detener carga";
                case Keys.BrowserSearch: return "Buscar";
                case Keys.BrowserFavorites: return "Favoritos";
                case Keys.BrowserHome: return "Inicio web";
                case Keys.LaunchMail: return "Correo";
                case Keys.LaunchApplication1: return "Aplicaci\u00F3n 1";
                case Keys.LaunchApplication2: return "Calculadora";
                case Keys.SelectMedia: return "Multimedia";
                case Keys.Sleep: return "Suspender";
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
                case Keys.OemClear: return "Borrar";
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
            if (key == Keys.Capital || key == Keys.NumLock) return KeyName(key) + " no sirve de atajo";
            if (!win)
            {
                string what = null;
                // Tab switches windows with Alt and tabs with Ctrl, backwards with Shift too. Only the part that does it is
                // named: the whole combination would not fit the field.
                if (key == Keys.Tab && (ctrl || alt)) return alt ? "Alt + Tab cambia de ventana" : "Ctrl + Tab cambia de pesta\u00F1a";
                if (ctrl && !shift && !alt)
                {
                    switch (key)
                    {
                        case Keys.A: what = "Seleccionar todo"; break;
                        case Keys.C: case Keys.Insert: what = "Copiar"; break;
                        case Keys.V: what = "Pegar"; break;
                        case Keys.X: what = "Cortar"; break;
                        case Keys.Z: what = "Deshacer"; break;
                        case Keys.Y: what = "Rehacer"; break;
                        case Keys.S: what = "Guardar"; break;
                        case Keys.F: what = "Buscar"; break;
                        case Keys.P: what = "Imprimir"; break;
                        case Keys.N: what = "Nuevo"; break;
                        case Keys.O: what = "Abrir"; break;
                        case Keys.W: what = "Cerrar"; break;
                        case Keys.T: what = "Nueva pesta\u00F1a"; break;
                    }
                }
                else if (shift && !ctrl && !alt)
                {
                    if (key == Keys.Insert) what = "Pegar";
                    else if (key == Keys.Delete) what = "Cortar";
                    else if (key == Keys.F10) what = "Men\u00FA contextual";
                }
                else if (alt && !ctrl && !shift)
                {
                    switch (key)
                    {
                        case Keys.F4: what = "Cerrar ventana"; break;
                        case Keys.Space: what = "Men\u00FA de ventana"; break;
                        case Keys.Left: what = "Atr\u00E1s"; break;
                        case Keys.Right: what = "Adelante"; break;
                    }
                }
                if (what != null) return Joined(ctrl, shift, alt, false, key) + " ya es " + what;
            }
            // Shift alone is not enough: Shift + a letter is just a capital letter.
            if (!ctrl && !alt && !win && !StandsAlone(key)) return "Comb\u00EDnala con Ctrl o Alt";
            if (!win)
            {
                if (!alt && Typing(key)) return "Se usa al escribir: prueba otra";
                // On layouts with AltGr (Spanish among them) Ctrl + Alt + a key types a character such as @ or #.
                if (ctrl && alt)
                {
                    string ch = AltGrChar(key, shift);
                    if (ch != null) return "As\u00ED escribes \u00AB" + ch + "\u00BB: prueba otra";
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
