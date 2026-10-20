// Shortcuts that Windows or another app also answer, suite "keys": what the keyboard hook takes, repeats and releases,
// the field recording through it, Windows' own capture keys, and who holds a busy one. No hook is installed here.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;

namespace Stackshot.Tests
{
    static class AppTestsKeys
    {
        const string S = "keys";
        const uint ALT = 1, CTRL = 2, SHIFT = 4, WIN = 8;
        const int VK_S = 0x53, VK_R = 0x52, VK_SNAPSHOT = 0x2C, VK_LWIN = 0x5B, VK_SHIFT = 0xA0;

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        public static void Suite()
        {
            AppTests.Test(S, "rules-fire-only-on-the-exact-combo", delegate
            {
                KeyGrabRules r = new KeyGrabRules();
                r.Entries = new KeyGrabRules.Entry[] { new KeyGrabRules.Entry(WIN | SHIFT, VK_S, 7) };
                int id;
                Check(r.Down(VK_S, WIN, 1000, false, out id) == KeyGrabRules.Act.Pass, "Win+S is not Win+Shift+S");
                Check(r.Up(VK_S) == false, "its release passes");
                Check(r.Down(VK_S, WIN | SHIFT | CTRL, 1100, false, out id) == KeyGrabRules.Act.Pass, "an extra modifier is another combo");
                Check(r.Down(VK_S, WIN | SHIFT, 1200, false, out id) == KeyGrabRules.Act.Fire && id == 7, "Win+Shift+S fires its action");
                Check(r.Down(VK_S, WIN | SHIFT, 1230, false, out id) == KeyGrabRules.Act.Swallow, "the keyboard repeating it is swallowed, not fired again");
                Check(r.Up(VK_S), "its release is swallowed too");
                Check(r.Up(VK_S) == false, "only once");
                Check(r.Down(VK_S, WIN | SHIFT, 1300, false, out id) == KeyGrabRules.Act.Fire, "pressed again: fires again");
            });

            AppTests.Test(S, "rules-never-touch-modifiers", delegate
            {
                KeyGrabRules r = new KeyGrabRules();
                r.Entries = new KeyGrabRules.Entry[] { new KeyGrabRules.Entry(WIN | SHIFT, VK_S, 1) };
                r.Record = true;
                int id;
                foreach (int vk in new int[] { VK_LWIN, 0x5C, VK_SHIFT, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x10, 0x11, 0x12 })
                    Check(r.Down(vk, WIN | SHIFT, 1000, true, out id) == KeyGrabRules.Act.Pass, "modifier " + vk.ToString("X2") + " passes");
            });

            AppTests.Test(S, "rules-a-lost-release-does-not-block-the-key", delegate
            {
                KeyGrabRules r = new KeyGrabRules();
                r.Entries = new KeyGrabRules.Entry[] { new KeyGrabRules.Entry(0, VK_SNAPSHOT, 3) };
                int id;
                Check(r.Down(VK_SNAPSHOT, 0, 5000, false, out id) == KeyGrabRules.Act.Fire, "first press");
                // Its key up never came (Win+L, a UAC prompt): long after, a key down is a new press.
                Check(r.Down(VK_SNAPSHOT, 0, 5000 + KeyGrabRules.RepeatMs + 50, false, out id) == KeyGrabRules.Act.Fire, "new press after a lost release");
                // The tick count wraps after 49 days.
                Check(r.Up(VK_SNAPSHOT), "release");
                Check(r.Down(VK_SNAPSHOT, 0, uint.MaxValue - 10, false, out id) == KeyGrabRules.Act.Fire, "near the wrap");
                Check(r.Down(VK_SNAPSHOT, 0, 20, false, out id) == KeyGrabRules.Act.Swallow, "a repeat across the wrap is still a repeat");
            });

            AppTests.Test(S, "rules-record-takes-every-key-only-in-its-window", delegate
            {
                KeyGrabRules r = new KeyGrabRules();
                r.Entries = new KeyGrabRules.Entry[] { new KeyGrabRules.Entry(WIN | SHIFT, VK_S, 1) };
                int id;
                r.Record = true;
                Check(r.Down(VK_R, CTRL, 1000, true, out id) == KeyGrabRules.Act.Take, "recording: a key goes to the field");
                Check(r.Up(VK_R), "and its release does not reach the app below");
                Check(r.Down(VK_S, WIN | SHIFT, 1100, true, out id) == KeyGrabRules.Act.Take, "even a combo Stackshot answers: the field records it");
                r.Up(VK_S);
                Check(r.Down(VK_S, WIN | SHIFT, 1200, false, out id) == KeyGrabRules.Act.Fire, "another window in front: the shortcut works as usual");
                r.Up(VK_S);
                r.Record = false;
                Check(r.Down(VK_R, CTRL, 1300, true, out id) == KeyGrabRules.Act.Pass, "not recording: nothing taken");
            });

            AppTests.Test(S, "windows-capture-keys", delegate
            {
                Check(KeyOwners.OfWindows("Win+Shift+S") != null && KeyOwners.OfWindows("Win+Shift+S").Owner == KeyOwners.Snipping, "Win+Shift+S is the Snipping Tool's");
                Check(KeyOwners.OfWindows("shift+win+s") != null, "in any order and spelling");
                Check(KeyOwners.OfWindows("Win+Shift+R") != null && KeyOwners.OfWindows("Win+Alt+R").Owner == KeyOwners.GameBar, "recording keys");
                Check(KeyOwners.OfWindows("Win+PrintScreen") != null, "Win+Print Screen");
                Check(KeyOwners.OfWindows("Ctrl+Shift+S") == null && KeyOwners.OfWindows("Win+S") == null && KeyOwners.OfWindows("") == null, "others are not");
                foreach (string[] pair in KeyOwners.WindowsSet)
                {
                    Check(KeyOwners.OfWindows(pair[0]) != null, pair[0] + " is a Windows key");
                    Check(Array.IndexOf(Settings.Actions, pair[1]) >= 0, pair[1] + " is an action");
                }
            });

            AppTests.Test(S, "who-holds-a-busy-key", delegate
            {
                KeyOwners.FindProcesses = delegate(string name) { return name == "ShareX" ? new int[] { 4242 } : name == "OneDrive" ? new int[] { 77 } : name == "Greenshot" ? new int[] { 99 } : new int[0]; };
                KeyOwners.ReadKeys = delegate(string app) { return app == "ShareX" ? new List<string> { "Ctrl+PrintScreen", "Alt+PrintScreen" } : null; };
                try
                {
                    List<KeyOwners.App> apps = KeyOwners.Running();
                    Check(apps.Count == 3, "three open: " + apps.Count);
                    KeyOwners.App sharex = apps.Find(delegate(KeyOwners.App a) { return a.Name == "ShareX"; });
                    Check(sharex != null && sharex.FromSettings && sharex.Pids.Contains(4242), "ShareX with the keys its settings say");
                    Check(KeyOwners.Holder("Ctrl+PrintScreen", apps) == "ShareX o Greenshot", "both use Ctrl+Print Screen: " + KeyOwners.Holder("Ctrl+PrintScreen", apps));
                    Check(KeyOwners.Holder("Shift+PrintScreen", apps) == "Greenshot", "only Greenshot (its usual keys)");
                    // ShareX's settings move Print Screen elsewhere; Greenshot uses it; OneDrive only might.
                    Check(KeyOwners.Holder("PrintScreen", apps) == "Greenshot", "the surer one is named: " + KeyOwners.Holder("PrintScreen", apps));
                    Check(KeyOwners.Holder("Ctrl+Alt+F9", apps) == null, "nobody known");
                }
                finally { KeyOwners.FindProcesses = null; KeyOwners.ReadKeys = null; }
            });

            AppTests.Test(S, "onedrive-is-named-only-when-nothing-surer-is", delegate
            {
                KeyOwners.FindProcesses = delegate(string name) { return name == "OneDrive" ? new int[] { 77 } : new int[0]; };
                KeyOwners.ReadKeys = delegate(string app) { return null; };
                try
                {
                    List<KeyOwners.App> apps = KeyOwners.Running();
                    Check(apps.Count == 1 && apps[0].Maybe, "OneDrive only maybe");
                    Check(KeyOwners.Holder("PrintScreen", apps) == "OneDrive", "named for Print Screen");
                }
                finally { KeyOwners.FindProcesses = null; KeyOwners.ReadKeys = null; }
            });

            AppTests.Test(S, "links-keep-their-shortcut-in-the-file", delegate
            {
                QuickLink old = QuickLink.Parse("Intranet\thttps://intranet.example.com/");
                Check(old != null && old.Keys == "" && old.Line == "Intranet\thttps://intranet.example.com/", "a line from before shortcuts reads and writes the same");
                QuickLink k = QuickLink.Parse("Proyectos\tC:\\Proyectos\\\tmayus+ctrl+alt+1");
                Check(k != null && k.Target == "C:\\Proyectos\\" && k.Keys == "Ctrl+Shift+Alt+1", "the shortcut is read in its stored form: " + (k == null ? "null" : k.Keys));
                Check(k.Line == "Proyectos\tC:\\Proyectos\\\tCtrl+Shift+Alt+1", "and written back: " + k.Line);
                Check(QuickLink.Parse("") == null && QuickLink.Parse("solo") == null && QuickLink.Parse("\tC:\\x") == null, "broken lines are skipped");
                Check(ShotStack.LinkAction(k) == "link:C:\\Proyectos\\", "its hotkey id is its target");
            });

            AppTests.Test(S, "link-command-line", delegate
            {
                string bad;
                Check(CliCommand.Parse(new string[] { "--link", "Intranet" }, out bad) == "link Intranet" && bad == null, "--link name");
                Check(CliCommand.Parse(new string[] { "/link:2" }, out bad) == "link 2", "/link:2");
                Check(CliCommand.Parse(new string[] { "--link \"Mis proyectos\"" }, out bad) == "link Mis proyectos", "quoted as one argument by a launcher");
                Check(CliCommand.Parse(new string[] { "--link" }, out bad) == null && bad != null, "--link without a name is refused");
                Check(CliCommand.Known("link Intranet") && !CliCommand.Known("link "), "known");
                Check(CliCommand.Arguments("link Mis proyectos", null) == "--link \"Mis proyectos\"", "passed on quoted: " + CliCommand.Arguments("link Mis proyectos", null));
                List<string> run = CliCommand.Batch(new List<string> { "link A", "capture region", "link A", "link B" }, null);
                Check(run.Count == 3 && run[0] == "link A" && run[1] == "link B" && run[2] == "capture region", "each link once, the capture last: " + string.Join("|", run.ToArray()));
            });

            AppTests.Test(S, "no-hook-in-tests", delegate
            {
                Check(KeyGrab.Live == 0, "a keyboard hook is installed: " + KeyGrab.Live);
            });
        }
    }
}
