// Stackshot - Shortcuts taken before Windows or another program sees them (Win + Shift + S, a Print Screen the Snipping
// Tool holds, a combo the user chose to take from another app), and every key while a shortcut field records one.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Stackshot
{
    // What happens to each key on its way in. Apart from the hook so the tests can drive it; only the hook thread calls
    // Down and Up.
    internal sealed class KeyGrabRules
    {
        internal struct Entry
        {
            public uint Mods;
            public int Vk, Id;
            public Entry(uint mods, int vk, int id) { Mods = mods; Vk = vk; Id = id; }
        }

        public enum Act { Pass, Swallow, Fire, Take }

        // A key down this soon after the last one of the same key, with no key up between, is the keyboard repeating it.
        public const uint RepeatMs = 1500;

        Entry[] entries = new Entry[0];
        // Keys swallowed on their way down, with the time of their last key down: their repeats and their release go too.
        readonly Dictionary<int, uint> held = new Dictionary<int, uint>();

        public Entry[] Entries
        {
            get { return Volatile.Read(ref entries); }
            set { Volatile.Write(ref entries, value ?? new Entry[0]); }
        }

        volatile bool record;
        public bool Record { get { return record; } set { record = value; } }

        // here: a shortcut field is recording and its window has the focus.
        public Act Down(int vk, uint mods, uint time, bool here, out int id)
        {
            id = 0;
            if (IsModifier(vk)) return Act.Pass;
            uint last;
            if (held.TryGetValue(vk, out last))
            {
                if (unchecked(time - last) < RepeatMs) { held[vk] = time; return Act.Swallow; }
                held.Remove(vk); // its key up was lost (a secure desktop came up meanwhile)
            }
            if (record && here) { held[vk] = time; return Act.Take; }
            foreach (Entry e in Entries)
                if (e.Vk == vk && e.Mods == mods) { held[vk] = time; id = e.Id; return Act.Fire; }
            return Act.Pass;
        }

        public bool Up(int vk) { return held.Remove(vk); }

        public static bool IsModifier(int vk)
        {
            return (vk >= 0x10 && vk <= 0x12) || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
        }
    }

    // The low-level keyboard hook, on a thread of its own that does nothing else: Windows waits for it before each key
    // reaches anyone, so it must never wait on the interface. It only exists while a shortcut needs it.
    internal sealed class KeyGrab : IDisposable
    {
        public const int WM_FIRE = 0x8000 + 0x31, WM_TAKE = 0x8000 + 0x32; // WM_APP + n, to the hotkeys window
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;
        // Marks the keys Stackshot itself sends, so the hook lets them through.
        static readonly IntPtr Mark = new IntPtr(0x53534B47);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y, priv; }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool PeekMessage(out MSG m, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);

        // Hooks installed right now in the whole process (the tests check it stays at zero).
        static int live;
        internal static int Live { get { return Volatile.Read(ref live); } }

        public readonly KeyGrabRules Rules = new KeyGrabRules();
        readonly IntPtr target;
        readonly HookProc proc;   // kept alive as long as Windows may call it
        readonly Thread thread;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        volatile uint tid;
        volatile bool stop;
        IntPtr hook;
        IntPtr recordWindow;

        public volatile bool Failed;

        KeyGrab(IntPtr target)
        {
            this.target = target;
            proc = OnKey;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Name = "Stackshot: atajos";
        }

        // Waits (briefly) for the hook to be in place, so a refusal is known at once.
        public static KeyGrab Start(IntPtr target)
        {
            KeyGrab g = new KeyGrab(target);
            g.thread.Start();
            if (!g.ready.WaitOne(1500)) { g.Failed = true; ShotStack.Log("Atajos: el gancho de teclado no responde"); }
            return g;
        }

        // The window whose shortcut field is recording (zero: none).
        public IntPtr RecordWindow
        {
            get { return Volatile.Read(ref recordWindow); }
            set { Volatile.Write(ref recordWindow, value); Rules.Record = value != IntPtr.Zero; }
        }

        void Run()
        {
            try
            {
                Power.OptOutThisThread();
                hook = SetWindowsHookEx(13, proc, GetModuleHandle(null), 0); // WH_KEYBOARD_LL
                if (hook == IntPtr.Zero)
                {
                    Failed = true;
                    ShotStack.Log("Atajos: Windows no deja vigilar el teclado (" + Marshal.GetLastWin32Error() + ")");
                }
                else Interlocked.Increment(ref live);
                MSG m;
                PeekMessage(out m, IntPtr.Zero, 0, 0, 0); // the thread's message queue, so Dispose can post to it
                tid = Native.GetCurrentThreadId();
                Thread.MemoryBarrier();
                ready.Set();
                if (hook != IntPtr.Zero) while (!stop && GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { }
            }
            catch (Exception ex) { Failed = true; ShotStack.Log("Atajos (gancho): " + ex.Message); ready.Set(); }
            finally
            {
                if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; Interlocked.Decrement(ref live); }
            }
        }

        // Called by Windows from native code: nothing may escape, and anything not taken goes on untouched.
        IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && Marshal.ReadIntPtr(lParam, 16) != Mark)
                {
                    long msg = wParam.ToInt64();
                    int vk = Marshal.ReadInt32(lParam, 0);
                    if (msg == 0x100 || msg == 0x104) // WM_KEYDOWN, WM_SYSKEYDOWN
                    {
                        uint mods = Mods(), time = unchecked((uint)Marshal.ReadInt32(lParam, 12));
                        IntPtr rec = RecordWindow;
                        bool here = rec != IntPtr.Zero && Native.GetForegroundWindow() == rec;
                        int id;
                        KeyGrabRules.Act a = Rules.Down(vk, mods, time, here, out id);
                        if (a == KeyGrabRules.Act.Fire) { PostMessage(target, WM_FIRE, new IntPtr(id), IntPtr.Zero); Mask(mods); return new IntPtr(1); }
                        if (a == KeyGrabRules.Act.Take) { PostMessage(target, WM_TAKE, new IntPtr(vk), new IntPtr(mods)); Mask(mods); return new IntPtr(1); }
                        if (a == KeyGrabRules.Act.Swallow) return new IntPtr(1);
                    }
                    else if ((msg == 0x101 || msg == 0x105) && Rules.Up(vk)) return new IntPtr(1); // WM_KEYUP, WM_SYSKEYUP
                }
            }
            catch { }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        static uint Mods()
        {
            uint m = 0;
            if (GetAsyncKeyState(0x11) < 0) m |= MOD_CONTROL;
            if (GetAsyncKeyState(0x10) < 0) m |= MOD_SHIFT;
            if (GetAsyncKeyState(0x12) < 0) m |= MOD_ALT;
            if (GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0) m |= MOD_WIN;
            return m;
        }

        // With Win or Alt held, the key taken leaves Windows thinking that key was pressed alone: letting it go would open
        // Start, or the menu bar of the window in front. An unassigned key (0xE8) in between tells Windows it was a combo.
        static void Mask(uint mods)
        {
            if ((mods & (MOD_WIN | MOD_ALT)) == 0) return;
            try
            {
                INPUT[] i = new INPUT[2];
                for (int k = 0; k < 2; k++)
                {
                    i[k].type = 1; // INPUT_KEYBOARD
                    i[k].u.ki.wVk = 0xE8;
                    i[k].u.ki.dwFlags = k == 0 ? 0u : 2u; // KEYEVENTF_KEYUP
                    i[k].u.ki.dwExtraInfo = Mark;
                }
                SendInput(2, i, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        public void Dispose()
        {
            stop = true;
            uint t = tid;
            if (t != 0) PostThreadMessage(t, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
            // The event is left to the collector: a thread that started late may still set it.
            try { thread.Join(1000); } catch { }
        }
    }
}
