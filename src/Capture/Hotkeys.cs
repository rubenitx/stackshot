// Stackshot - Atajos de teclado globales (RegisterHotKey) y su texto legible.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Stackshot
{
    // Ventana invisible que recibe los atajos registrados y avisa con el nombre de la acción.
    public class Hotkeys : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        public event Action<string> Pressed;
        readonly Dictionary<int, string> ids = new Dictionary<int, string>();
        int next = 1;

        public Hotkeys()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE: ventana solo de mensajes
            CreateHandle(cp);
        }

        // Registra todas las combinaciones de una acción ("PrintScreen, Ctrl+Shift+4"). Devuelve las que no se pudieron.
        public List<string> Register(string action, string combos)
        {
            List<string> failed = new List<string>();
            foreach (string c in Split(combos))
            {
                uint mods;
                Keys key;
                if (!TryParse(c, out mods, out key)) { failed.Add(c); continue; }
                int id = next++;
                if (Native.RegisterHotKey(Handle, id, mods | MOD_NOREPEAT, (uint)key)) ids[id] = action;
                else failed.Add(c);
            }
            return failed;
        }

        public void Clear()
        {
            foreach (int id in ids.Keys) Native.UnregisterHotKey(Handle, id);
            ids.Clear();
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

        // "Ctrl+Shift+PrintScreen" -> modificadores + tecla. Acepta también los nombres en español.
        public static bool TryParse(string combo, out uint mods, out Keys key)
        {
            mods = 0;
            key = Keys.None;
            foreach (string part in combo.Split('+'))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                if (p == "ctrl" || p == "control") mods |= MOD_CONTROL;
                else if (p == "shift" || p == "may\u00FAs" || p == "mayus") mods |= MOD_SHIFT;
                else if (p == "alt") mods |= MOD_ALT;
                else if (p == "win") mods |= MOD_WIN;
                else
                {
                    Keys k = KeyFromName(p);
                    if (k == Keys.None) return false;
                    key = k;
                }
            }
            return key != Keys.None;
        }

        static Keys KeyFromName(string p)
        {
            if (p == "impr pant" || p == "imprpant" || p == "printscreen" || p == "prtsc") return Keys.PrintScreen;
            if (p.Length == 1 && p[0] >= '0' && p[0] <= '9') return Keys.D0 + (p[0] - '0');
            Keys k;
            if (Enum.TryParse(p, true, out k)) return k;
            return Keys.None;
        }

        // Combinación para guardar en los ajustes (nombres en inglés, estables).
        public static string ToSetting(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            string s = "";
            if ((keyData & Keys.Control) != 0) s += "Ctrl+";
            if ((keyData & Keys.Shift) != 0) s += "Shift+";
            if ((keyData & Keys.Alt) != 0) s += "Alt+";
            if (key >= Keys.D0 && key <= Keys.D9) return s + (char)('0' + (key - Keys.D0));
            return s + key;
        }

        // Combinación para enseñarla: "Ctrl + Mayús + Impr Pant".
        public static string Display(string combos)
        {
            List<string> parts = Split(combos);
            if (parts.Count == 0) return "Sin atajo";
            uint mods;
            Keys key;
            if (!TryParse(parts[0], out mods, out key)) return parts[0];
            List<string> s = new List<string>();
            if ((mods & MOD_WIN) != 0) s.Add("Win");
            if ((mods & MOD_CONTROL) != 0) s.Add("Ctrl");
            if ((mods & MOD_SHIFT) != 0) s.Add("May\u00FAs");
            if ((mods & MOD_ALT) != 0) s.Add("Alt");
            string k = key.ToString();
            if (key == Keys.PrintScreen) k = "Impr Pant";
            else if (key >= Keys.D0 && key <= Keys.D9) k = ((char)('0' + (key - Keys.D0))).ToString();
            else if (key == Keys.Escape) k = "Esc";
            s.Add(k);
            return string.Join(" + ", s);
        }
    }
}
