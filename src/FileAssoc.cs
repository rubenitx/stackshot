// Stackshot - Per-user file associations for .md, .xml and .mkv (HKCU only; Windows keeps the final choice to the user).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Stackshot
{
    public sealed class AssocKind
    {
        public readonly string ProgId, FriendlyEs, FriendlyEn;
        public readonly string[] Exts;
        // The type's name as Explorer shows it, in the language in use when it is registered.
        public string Friendly { get { return I18n.IsSpanish ? FriendlyEs : FriendlyEn; } }
        public AssocKind(string progId, string friendlyEs, string friendlyEn, params string[] exts) { ProgId = progId; FriendlyEs = friendlyEs; FriendlyEn = friendlyEn; Exts = exts; }
    }

    // Stackshot is offered as an app for the extension: its own ProgID, an OpenWithProgids entry, and Capabilities with a
    // RegisteredApplications entry so it shows in Windows' default apps. UserChoice (the user's pick, protected by a hash)
    // is only ever read. Every key here is ours; nothing of another app is touched. Nothing is written unless asked.
    public static class FileAssoc
    {
        public static readonly AssocKind Markdown = new AssocKind("Stackshot.Markdown", "Documento Markdown", "Markdown document", ".md", ".markdown");
        public static readonly AssocKind Xml = new AssocKind("Stackshot.Xml", "Documento XML", "XML document", ".xml");
        // Cine masters: Windows' own player cannot decode them, Stackshot's can.
        public static readonly AssocKind Video = new AssocKind("Stackshot.Video", "V\u00EDdeo MKV", "MKV video", ".mkv");
        static readonly AssocKind[] All = { Markdown, Xml, Video };

        const string Classes = @"Software\Classes\";
        const string CapKey = @"Software\Stackshot\Capabilities";
        const string RegApps = @"Software\RegisteredApplications";
        const string AppName = "Stackshot";

        [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        // Tests point the current user's registry somewhere else.
        internal static RegistryKey HkcuOverride { get; set; }
        static RegistryKey Hkcu { get { return HkcuOverride ?? Registry.CurrentUser; } }

        // The command Windows runs for a file: this copy, with the file as its only argument.
        public static string Command { get { return "\"" + Installer.ExePath + "\" \"%1\""; } }

        public static bool Registered(AssocKind k)
        {
            try
            {
                using (RegistryKey r = Hkcu.OpenSubKey(Classes + k.ProgId + @"\shell\open\command"))
                    return r != null;
            }
            catch { return false; }
        }

        // What Windows has chosen for the extension (read only).
        public static string ChosenProgId(string ext)
        {
            try
            {
                using (RegistryKey r = Hkcu.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\UserChoice"))
                    return r == null ? null : r.GetValue("ProgId") as string;
            }
            catch { return null; }
        }

        public static bool IsDefault(AssocKind k)
        {
            return string.Equals(ChosenProgId(k.Exts[0]), k.ProgId, StringComparison.OrdinalIgnoreCase);
        }

        // Registers (or, when already there, refreshes) the association. False when nothing was written.
        public static bool Enable(AssocKind k)
        {
            if (Settings.ReadOnly) return false;
            try
            {
                string cmd = Command, exe = Installer.ExePath;
                using (RegistryKey p = Hkcu.CreateSubKey(Classes + k.ProgId))
                {
                    p.SetValue("", k.Friendly);
                    using (RegistryKey i = p.CreateSubKey("DefaultIcon")) i.SetValue("", "\"" + exe + "\",0");
                    using (RegistryKey o = p.CreateSubKey(@"shell\open"))
                    {
                        o.SetValue("FriendlyAppName", AppName);
                        using (RegistryKey c = o.CreateSubKey("command")) c.SetValue("", cmd);
                    }
                }
                foreach (string ext in k.Exts)
                    using (RegistryKey w = Hkcu.CreateSubKey(Classes + ext + @"\OpenWithProgids"))
                        w.SetValue(k.ProgId, "", RegistryValueKind.String);
                using (RegistryKey cap = Hkcu.CreateSubKey(CapKey))
                {
                    cap.SetValue("ApplicationName", AppName);
                    cap.SetValue("ApplicationDescription", I18n.T("Capturas de pantalla, v\u00EDdeo y GIF, con reproductor de v\u00EDdeo y visor de Markdown y XML",
                                                                  "Screenshots, video and GIF, with a video player and a Markdown and XML viewer"));
                    cap.SetValue("ApplicationIcon", "\"" + exe + "\",0");
                    using (RegistryKey fa = cap.CreateSubKey("FileAssociations"))
                        foreach (string ext in k.Exts) fa.SetValue(ext, k.ProgId);
                }
                using (RegistryKey ra = Hkcu.CreateSubKey(RegApps)) ra.SetValue(AppName, CapKey);
                Changed();
                ShotStack.Log("Asociaci\u00F3n registrada: " + k.ProgId);
                return true;
            }
            catch (Exception ex) { ShotStack.Log("Asociar " + k.ProgId + ": " + ex.Message); return false; }
        }

        // Removes only what Enable wrote. If Stackshot was the chosen app, Windows asks again next time.
        public static void Disable(AssocKind k)
        {
            if (Settings.ReadOnly) return;
            try
            {
                Hkcu.DeleteSubKeyTree(Classes + k.ProgId, false);
                foreach (string ext in k.Exts)
                {
                    using (RegistryKey w = Hkcu.OpenSubKey(Classes + ext + @"\OpenWithProgids", true))
                        if (w != null) w.DeleteValue(k.ProgId, false);
                    DropIfEmpty(Classes + ext + @"\OpenWithProgids");
                    DropIfEmpty(Classes + ext);
                }
                using (RegistryKey fa = Hkcu.OpenSubKey(CapKey + @"\FileAssociations", true))
                    if (fa != null) foreach (string ext in k.Exts) fa.DeleteValue(ext, false);
                // The last one out takes the registration with it.
                if (!Array.Exists(All, delegate(AssocKind a) { return Registered(a); }))
                {
                    Hkcu.DeleteSubKeyTree(CapKey, false);
                    DropIfEmpty(@"Software\Stackshot"); // only when nothing else of Stackshot is kept there
                    using (RegistryKey ra = Hkcu.OpenSubKey(RegApps, true))
                        if (ra != null) ra.DeleteValue(AppName, false);
                }
                Changed();
                ShotStack.Log("Asociaci\u00F3n quitada: " + k.ProgId);
            }
            catch (Exception ex) { ShotStack.Log("Quitar asociaci\u00F3n " + k.ProgId + ": " + ex.Message); }
        }

        static void DropIfEmpty(string path)
        {
            try
            {
                bool empty;
                using (RegistryKey r = Hkcu.OpenSubKey(path))
                {
                    if (r == null) return;
                    empty = r.SubKeyCount == 0 && r.ValueCount == 0;
                }
                if (empty) Hkcu.DeleteSubKey(path, false);
            }
            catch { }
        }

        static void Changed()
        {
            if (HkcuOverride != null) return;
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { } // SHCNE_ASSOCCHANGED
        }

        // Our Capabilities entry names the ProgID for every extension of the kind (it is ours only when it does).
        static bool CapabilityHas(AssocKind k)
        {
            using (RegistryKey fa = Hkcu.OpenSubKey(CapKey + @"\FileAssociations"))
            {
                if (fa == null) return false;
                foreach (string ext in k.Exts)
                    if (!string.Equals(fa.GetValue(ext) as string, k.ProgId, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }
        }

        // After an update or a move the exe may be somewhere else: refresh what is already registered (only ours).
        public static void Repair()
        {
            if (Settings.ReadOnly) return;
            foreach (AssocKind k in All)
            {
                try
                {
                    if (!Registered(k)) continue;
                    // Our ProgID (its name is ours) with another exe's command, or without its capability entry (a
                    // registration cut halfway): written again in full.
                    bool same;
                    using (RegistryKey c = Hkcu.OpenSubKey(Classes + k.ProgId + @"\shell\open\command"))
                        same = c != null && string.Equals(c.GetValue("") as string, Command, StringComparison.OrdinalIgnoreCase);
                    if (same && CapabilityHas(k)) continue;
                    Enable(k);
                }
                catch (Exception ex) { ShotStack.Log("Reparar asociaci\u00F3n: " + ex.Message); }
            }
        }

        // Windows' own page, on Stackshot's entry when it can: choosing the default app is the user's decision.
        public static void OpenDefaultApps()
        {
            if (Settings.ReadOnly) return;
            foreach (string uri in new string[] { "ms-settings:defaultapps?registeredAppUser=" + AppName, "ms-settings:defaultapps" })
            {
                try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return; }
                catch (Exception ex) { ShotStack.Log("Aplicaciones predeterminadas: " + ex.Message); }
            }
        }
    }
}
