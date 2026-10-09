// Stackshot - Per-user file associations for .md and .xml (HKCU only; Windows keeps the final choice to the user).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Stackshot
{
    public sealed class AssocKind
    {
        public readonly string ProgId, Friendly;
        public readonly string[] Exts;
        public AssocKind(string progId, string friendly, params string[] exts) { ProgId = progId; Friendly = friendly; Exts = exts; }
    }

    // Stackshot is offered as an app for the extension: its own ProgID, an OpenWithProgids entry, and Capabilities with a
    // RegisteredApplications entry so it shows in Windows' default apps. UserChoice (the user's pick, protected by a hash)
    // is only ever read. Every key here is ours; nothing of another app is touched. Nothing is written unless asked.
    public static class FileAssoc
    {
        public static readonly AssocKind Markdown = new AssocKind("Stackshot.Markdown", "Documento Markdown", ".md", ".markdown");
        public static readonly AssocKind Xml = new AssocKind("Stackshot.Xml", "Documento XML", ".xml");

        const string Classes = @"Software\Classes\";
        const string CapKey = @"Software\Stackshot\Capabilities";
        const string RegApps = @"Software\RegisteredApplications";
        const string AppName = "Stackshot";

        [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        // The command Windows runs for a file: this copy, with the file as its only argument.
        public static string Command { get { return "\"" + Installer.ExePath + "\" \"%1\""; } }

        public static bool Registered(AssocKind k)
        {
            try
            {
                using (RegistryKey r = Registry.CurrentUser.OpenSubKey(Classes + k.ProgId + @"\shell\open\command"))
                    return r != null;
            }
            catch { return false; }
        }

        // What Windows has chosen for the extension (read only).
        public static string ChosenProgId(string ext)
        {
            try
            {
                using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\UserChoice"))
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
                using (RegistryKey p = Registry.CurrentUser.CreateSubKey(Classes + k.ProgId))
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
                    using (RegistryKey w = Registry.CurrentUser.CreateSubKey(Classes + ext + @"\OpenWithProgids"))
                        w.SetValue(k.ProgId, "", RegistryValueKind.String);
                using (RegistryKey cap = Registry.CurrentUser.CreateSubKey(CapKey))
                {
                    cap.SetValue("ApplicationName", AppName);
                    cap.SetValue("ApplicationDescription", "Capturas de pantalla, v\u00EDdeo y GIF, con visor de Markdown y XML");
                    cap.SetValue("ApplicationIcon", "\"" + exe + "\",0");
                    using (RegistryKey fa = cap.CreateSubKey("FileAssociations"))
                        foreach (string ext in k.Exts) fa.SetValue(ext, k.ProgId);
                }
                using (RegistryKey ra = Registry.CurrentUser.CreateSubKey(RegApps)) ra.SetValue(AppName, CapKey);
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
                Registry.CurrentUser.DeleteSubKeyTree(Classes + k.ProgId, false);
                foreach (string ext in k.Exts)
                {
                    using (RegistryKey w = Registry.CurrentUser.OpenSubKey(Classes + ext + @"\OpenWithProgids", true))
                        if (w != null) w.DeleteValue(k.ProgId, false);
                    DropIfEmpty(Classes + ext + @"\OpenWithProgids");
                    DropIfEmpty(Classes + ext);
                }
                using (RegistryKey fa = Registry.CurrentUser.OpenSubKey(CapKey + @"\FileAssociations", true))
                    if (fa != null) foreach (string ext in k.Exts) fa.DeleteValue(ext, false);
                // The last one out takes the registration with it.
                if (!Registered(Markdown) && !Registered(Xml))
                {
                    Registry.CurrentUser.DeleteSubKeyTree(CapKey, false);
                    using (RegistryKey ra = Registry.CurrentUser.OpenSubKey(RegApps, true))
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
                using (RegistryKey r = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (r == null) return;
                    empty = r.SubKeyCount == 0 && r.ValueCount == 0;
                }
                if (empty) Registry.CurrentUser.DeleteSubKey(path, false);
            }
            catch { }
        }

        static void Changed()
        {
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { } // SHCNE_ASSOCCHANGED
        }

        // Our Capabilities entry names the ProgID for every extension of the kind (it is ours only when it does).
        static bool CapabilityHas(AssocKind k)
        {
            using (RegistryKey fa = Registry.CurrentUser.OpenSubKey(CapKey + @"\FileAssociations"))
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
            foreach (AssocKind k in new AssocKind[] { Markdown, Xml })
            {
                try
                {
                    if (!Registered(k) || !CapabilityHas(k)) continue;
                    using (RegistryKey c = Registry.CurrentUser.OpenSubKey(Classes + k.ProgId + @"\shell\open\command"))
                        if (c != null && string.Equals(c.GetValue("") as string, Command, StringComparison.OrdinalIgnoreCase)) continue;
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
