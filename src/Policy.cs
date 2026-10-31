// Stackshot - IT policies: settings an organization forces through the registry (Group Policy, Intune, a .reg file).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Stackshot
{
    // HKLM\SOFTWARE\Policies\Stackshot and HKCU\SOFTWARE\Policies\Stackshot; the machine's value wins when both have one.
    //   SaveFolder     REG_SZ / REG_EXPAND_SZ   forced save folder (a full path; %VARIABLES% are expanded)
    //   CheckUpdates   DWORD 0                  never look for updates (so never install them either)
    //   AutoUpdate     DWORD 0                  never install updates on its own
    //   Mascot         DWORD 0                  no mascot at all: window, desktop and its tips
    //   SensitiveAuto  DWORD 1 / 0              always / never cover personal data in new captures
    //   HistorySearch  DWORD 0                  never read the text of captures
    //   XmlWatchFolder REG_SZ / REG_EXPAND_SZ   the folder whose new XML files can open in the viewer (General > Avanzado)
    // A DWORD 1 in the switches that only turn something off leaves the choice to the user. Anything else is ignored and
    // logged. A forced value replaces the user's while the policy exists, but settings.ini keeps the user's own choice
    // (Settings.Save writes ForFile), so removing the policy brings it back. Read by Settings.Load and every time a
    // section of the main window is built.
    public static class Policy
    {
        public const string KeyPath = @"SOFTWARE\Policies\Stackshot";
        public static string Managed { get { return I18n.T("Lo decide tu organizaci\u00F3n", "Managed by your organization"); } }
        public static string ManagedNote { get { return I18n.T("Algunos ajustes los gestiona tu organizaci\u00F3n.", "Some settings are managed by your organization."); } }

        // Tests read the policies from throwaway keys: each stands for a hive root (the policy key is looked for inside).
        internal static RegistryKey MachineOverride { get; set; }
        internal static RegistryKey UserOverride { get; set; }

        // What the registry forces: null or -1 where nothing is forced.
        internal sealed class Values
        {
            public string SaveFolder, XmlFolder;
            public int CheckUpdates = -1, AutoUpdate = -1, Mascot = -1, SensitiveAuto = -1, HistorySearch = -1;
        }

        static volatile Values current = new Values();

        public static bool FolderForced { get { return current.SaveFolder != null; } }
        public static string XmlFolder { get { return current.XmlFolder; } }
        public static bool ChecksForced { get { return current.CheckUpdates == 0; } }
        public static bool AutoForced { get { Values v = current; return v.AutoUpdate == 0 || v.CheckUpdates == 0; } }
        public static bool MascotForced { get { return current.Mascot == 0; } }
        public static bool SensitiveForced { get { return current.SensitiveAuto >= 0; } }
        public static bool SearchForced { get { return current.HistorySearch == 0; } }

        // Some setting is in the organization's hands.
        public static bool Any
        {
            get { return FolderForced || ChecksForced || AutoForced || MascotForced || SensitiveForced || SearchForced || XmlFolder != null; }
        }

        // Reads the policies again and applies them to these settings. True when a setting in use changed.
        public static bool Apply(Settings s)
        {
            Refresh();
            return Enforce(s, current);
        }

        // Reads the policies again (cheap: two registry keys, usually missing).
        public static void Refresh()
        {
            Values v = Read();
            current = v;
            Describe(v);
        }

        // ---- Reading

        static Values Read()
        {
            Values v = new Values();
            RegistryKey m = Open(MachineOverride, Registry.LocalMachine, "HKLM");
            RegistryKey u = Open(UserOverride, Registry.CurrentUser, "HKCU");
            try
            {
                if (m == null && u == null) return v;
                v.SaveFolder = FolderIn(m, "HKLM", "SaveFolder") ?? FolderIn(u, "HKCU", "SaveFolder");
                v.XmlFolder = FolderIn(m, "HKLM", "XmlWatchFolder") ?? FolderIn(u, "HKCU", "XmlWatchFolder");
                // A 1 in a switch that only turns something off leaves it to the user, even over the user's key's 0.
                v.CheckUpdates = Dword(m, u, "CheckUpdates") == 0 ? 0 : -1;
                v.AutoUpdate = Dword(m, u, "AutoUpdate") == 0 ? 0 : -1;
                v.Mascot = Dword(m, u, "Mascot") == 0 ? 0 : -1;
                v.SensitiveAuto = Dword(m, u, "SensitiveAuto");
                v.HistorySearch = Dword(m, u, "HistorySearch") == 0 ? 0 : -1;
            }
            finally
            {
                if (m != null) m.Close();
                if (u != null) u.Close();
            }
            return v;
        }

        static RegistryKey Open(RegistryKey testRoot, RegistryKey root, string where)
        {
            try { return (testRoot ?? root).OpenSubKey(KeyPath); }
            catch (Exception ex) { Once("Directivas (" + where + "): " + ex.Message); return null; }
        }

        // 0 or 1 from the machine's key, otherwise from the user's; -1 when neither has a valid one.
        static int Dword(RegistryKey m, RegistryKey u, string name)
        {
            int r = DwordIn(m, "HKLM", name);
            return r >= 0 ? r : DwordIn(u, "HKCU", name);
        }

        static int DwordIn(RegistryKey k, string where, string name)
        {
            if (k == null) return -1;
            try
            {
                object o = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (o == null) return -1;
                RegistryValueKind kind = k.GetValueKind(name);
                if (kind == RegistryValueKind.DWord && o is int && ((int)o == 0 || (int)o == 1)) return (int)o;
                Invalid(where, name, kind, o, "un DWORD 0 o 1");
            }
            catch (Exception ex) { Once("Directiva " + where + "\\" + name + ": " + ex.Message); }
            return -1;
        }

        // A full folder path (drive or network); environment variables are expanded for both string types.
        static string FolderIn(RegistryKey k, string where, string name)
        {
            if (k == null) return null;
            try
            {
                object o = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (o == null) return null;
                RegistryValueKind kind = k.GetValueKind(name);
                string text = o as string;
                if ((kind == RegistryValueKind.String || kind == RegistryValueKind.ExpandString) && text != null)
                {
                    text = text.Trim().Trim('"').Trim();
                    string full = text.Length > 0 ? Settings.Folder(text, null) : null;
                    if (full != null) return full;
                }
                Invalid(where, name, kind, o, "la ruta completa de una carpeta");
            }
            catch (Exception ex) { Once("Directiva " + where + "\\" + name + ": " + ex.Message); }
            return null;
        }

        static void Invalid(string where, string name, RegistryValueKind kind, object value, string expected)
        {
            string shown = value is string ? "\u00AB" + value + "\u00BB" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            Once("Directiva " + where + "\\" + KeyPath + "\\" + name + " no v\u00E1lida (" + kind + " " + shown + "; se espera " + expected + "): se ignora");
        }

        // ---- Logging: each problem and each set of policies once per run, not on every section built.

        static readonly HashSet<string> logged = new HashSet<string>();
        static string described = "";

        static void Once(string message)
        {
            lock (logged)
            {
                if (logged.Count > 200 || !logged.Add(message)) return;
            }
            ShotStack.Log(message);
        }

        static void Describe(Values v)
        {
            List<string> l = new List<string>();
            if (v.SaveFolder != null) l.Add("SaveFolder = " + v.SaveFolder);
            if (v.XmlFolder != null) l.Add("XmlWatchFolder = " + v.XmlFolder);
            if (v.CheckUpdates == 0) l.Add("CheckUpdates = 0");
            if (v.AutoUpdate == 0) l.Add("AutoUpdate = 0");
            if (v.Mascot == 0) l.Add("Mascot = 0");
            if (v.SensitiveAuto >= 0) l.Add("SensitiveAuto = " + v.SensitiveAuto);
            if (v.HistorySearch == 0) l.Add("HistorySearch = 0");
            string now = string.Join(", ", l.ToArray());
            lock (logged)
            {
                if (now == described) return;
                described = now;
            }
            ShotStack.Log(now.Length > 0 ? "Directivas de la organizaci\u00F3n: " + now : "Ya no hay directivas de la organizaci\u00F3n");
        }

        // ---- Applying: the settings a policy can force, with their user's own value kept aside

        sealed class Slot
        {
            public readonly Func<Settings, object> Get;
            public readonly Action<Settings, object> Set;
            public readonly Func<Values, object> Forced;    // the forced value, or null

            public Slot(Func<Settings, object> get, Action<Settings, object> set, Func<Values, object> forced)
            {
                Get = get;
                Set = set;
                Forced = forced;
            }
        }

        static object Off(bool forced) { return forced ? (object)false : null; }

        static readonly Slot[] Slots =
        {
            new Slot(s => s.SaveFolder, (s, o) => s.SaveFolder = (string)o, v => v.SaveFolder),
            new Slot(s => s.CheckUpdates, (s, o) => s.CheckUpdates = (bool)o, v => Off(v.CheckUpdates == 0)),
            new Slot(s => s.AutoUpdate, (s, o) => s.AutoUpdate = (bool)o, v => Off(v.AutoUpdate == 0 || v.CheckUpdates == 0)),
            new Slot(s => s.MascotOnSaved, (s, o) => s.MascotOnSaved = (bool)o, v => Off(v.Mascot == 0)),
            new Slot(s => s.MascotTalks, (s, o) => s.MascotTalks = (bool)o, v => Off(v.Mascot == 0)),
            new Slot(s => s.MascotDesktopSaved, (s, o) => s.MascotDesktopSaved = (bool)o, v => Off(v.Mascot == 0)),
            new Slot(s => s.SensitiveAuto, (s, o) => s.SensitiveAuto = (bool)o, v => v.SensitiveAuto >= 0 ? (object)(v.SensitiveAuto == 1) : null),
            new Slot(s => s.HistorySearch, (s, o) => s.HistorySearch = (bool)o, v => Off(v.HistorySearch == 0))
        };

        // Per settings object: the user's own value of each forced setting, and the value forced on it.
        internal sealed class Kept
        {
            public readonly object[] Own = new object[Slots.Length], Applied = new object[Slots.Length];
            public readonly bool[] Held = new bool[Slots.Length];
        }

        static readonly object gate = new object();

        // Puts the forced values in place and the user's back where nothing is forced any more. A forced setting changed
        // by the app meanwhile (a profile, --install --folder) counts as the user's new choice for later. True when a
        // value in use changed.
        internal static bool Enforce(Settings s, Values v)
        {
            if (s == null) return false;
            bool changed = false;
            lock (gate)
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    Slot slot = Slots[i];
                    object want = slot.Forced(v), live = slot.Get(s);
                    Kept k = s.PolicyKept;
                    bool held = k != null && k.Held[i];
                    if (want != null)
                    {
                        if (k == null) s.PolicyKept = k = new Kept();
                        if (!held) { k.Own[i] = live; k.Held[i] = true; }
                        else if (!Equals(live, k.Applied[i])) k.Own[i] = live;
                        k.Applied[i] = want;
                        if (!Equals(live, want)) { slot.Set(s, want); changed = true; }
                    }
                    else if (held)
                    {
                        object back = Equals(live, k.Applied[i]) ? k.Own[i] : live;
                        k.Held[i] = false;
                        k.Own[i] = null;
                        k.Applied[i] = null;
                        if (!Equals(live, back)) { slot.Set(s, back); changed = true; }
                    }
                }
            }
            return changed;
        }

        // What settings.ini gets: these settings with the user's own choices where a policy forces something.
        internal static Settings ForFile(Settings s)
        {
            Enforce(s, current);
            lock (gate)
            {
                Kept k = s.PolicyKept;
                if (k == null || Array.IndexOf(k.Held, true) < 0) return s;
                Settings f = s.Clone();
                for (int i = 0; i < Slots.Length; i++) if (k.Held[i]) Slots[i].Set(f, k.Own[i]);
                return f;
            }
        }
    }
}
