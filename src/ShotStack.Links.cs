// Stackshot - Quick links: web addresses, folders, files and programs the mascot opens from its menu. Kept in
// accesos.txt (name, tab, target, one per line) next to the reminders.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Stackshot
{
    public sealed class QuickLink
    {
        public string Name, Target;
        public string Keys = "";     // its shortcut (Atajos > Accesos rápidos), or none

        // A line of accesos.txt: name, tab, target and, when it has one, tab and its shortcut.
        public string Line { get { return Name + "\t" + Target + (Keys.Length > 0 ? "\t" + Keys : ""); } }

        public static QuickLink Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] p = line.Split('\t');
            if (p.Length < 2 || p[0].Trim().Length == 0) return null;
            QuickLink l = new QuickLink { Name = p[0].Trim(), Target = p[1].Trim() };
            if (p.Length > 2) l.Keys = Hotkeys.Normalize(p[2].Trim());
            return l;
        }

        public bool Web { get { return Target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Target.StartsWith("https://", StringComparison.OrdinalIgnoreCase); } }

        // The glyph for its kind: a page, a folder, a program or a file. Worked out from the text alone (no disk is
        // asked while the menu opens): a folder is saved with a "\" at the end.
        public string Icon
        {
            get
            {
                if (Web) return "open";
                if (Folder) return "folder";
                string ext = Path.GetExtension(Target).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk" || ext == ".bat" || ext == ".cmd") return "app";
                return ext.Length == 0 ? "folder" : "doc";
            }
        }

        public bool Folder { get { return !Web && (Target.EndsWith("\\") || Target.EndsWith("/")); } }
    }

    public partial class ShotStack
    {
        public const int MaxLinks = 12;
        List<QuickLink> links;

        static string LinksFile { get { return Path.Combine(Settings.DataDir, "accesos.txt"); } }

        public List<QuickLink> Links
        {
            get
            {
                if (links == null) LoadLinks();
                return new List<QuickLink>(links);
            }
        }

        // What was typed made into something that opens: a web address (https:// added when missing) or a path (with
        // %VARIABLES% expanded). error: why it can't be used.
        public static string LinkTarget(string typed, out string error)
        {
            error = null;
            string t = (typed ?? "").Trim().Trim('"').Trim();
            if (t.Length == 0) { error = "Escribe una direcci\u00F3n web o la ruta de una carpeta o un archivo."; return null; }
            Uri u;
            if (Uri.TryCreate(t, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)) return u.AbsoluteUri;
            string path = Environment.ExpandEnvironmentVariables(t);
            if (path.StartsWith("\\\\")) return path;   // a network share: not checked (it could take long to answer)
            if (path.Length >= 3 && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
            {
                if (Directory.Exists(path)) return path.EndsWith("\\") || path.EndsWith("/") ? path : path + "\\";
                if (File.Exists(path)) return path;
                error = "No encuentro esa carpeta o ese archivo.";
                return null;
            }
            if (t.IndexOf(' ') < 0 && t.IndexOf('.') > 0 && Uri.TryCreate("https://" + t, UriKind.Absolute, out u)) return u.AbsoluteUri;
            error = "Escribe una direcci\u00F3n web (como intranet.empresa.es) o una ruta completa (como C:\\Proyectos).";
            return null;
        }

        // The name shown when none is given: the site without "www.", or the folder or file name.
        public static string LinkName(string target)
        {
            Uri u;
            if (Uri.TryCreate(target, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            {
                string host = u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host.Substring(4) : u.Host;
                return host.Length > 0 ? host : target;
            }
            string name = Path.GetFileName(target.TrimEnd('\\', '/'));
            if (Path.GetExtension(name).ToLowerInvariant() == ".lnk") name = Path.GetFileNameWithoutExtension(name);
            return name.Length > 0 ? name : target;
        }

        // Null when added, or why not.
        public string AddLink(string name, string typed)
        {
            string error;
            string target = LinkTarget(typed, out error);
            if (target == null) return error;
            if (!Safe(target)) return "Esa direcci\u00F3n o ruta no se puede usar.";
            if (links == null) LoadLinks();
            foreach (QuickLink l in links)
                if (string.Equals(l.Target, target, StringComparison.OrdinalIgnoreCase)) return "Ya lo tienes en tus accesos r\u00E1pidos: \u00AB" + l.Name + "\u00BB.";
            if (links.Count >= MaxLinks) return "Ya tienes " + MaxLinks + " accesos: quita alguno para a\u00F1adir otro.";
            name = LinkText(name);
            if (name.Length == 0) name = LinkText(LinkName(target));
            if (name.Length > 40) name = name.Substring(0, 40).Trim();
            links.Add(new QuickLink { Name = name, Target = target });
            SaveLinks();
            Log("Acceso r\u00E1pido a\u00F1adido");
            return null;
        }

        public void RemoveLink(QuickLink l)
        {
            if (links == null) LoadLinks();
            bool keyed = false;
            foreach (QuickLink x in links) if (x.Name == l.Name && x.Target == l.Target && x.Keys.Length > 0) keyed = true;
            links.RemoveAll(delegate(QuickLink x) { return x.Name == l.Name && x.Target == l.Target; });
            SaveLinks();
            // Its shortcut goes with it at once.
            if (keyed) RegisterHotkeys(false);
        }

        // ---- A shortcut for each link (registered with the hotkeys, as "link:" + its target, which is unique)

        public const string LinkPrefix = "link:";

        public static string LinkAction(QuickLink l) { return LinkPrefix + l.Target; }

        public QuickLink LinkOf(string action)
        {
            if (action == null || !action.StartsWith(LinkPrefix, StringComparison.Ordinal)) return null;
            if (links == null) LoadLinks();
            string target = action.Substring(LinkPrefix.Length);
            foreach (QuickLink l in links) if (string.Equals(l.Target, target, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        // Saves the link's shortcut (the caller registers the hotkeys again).
        public void SetLinkKeys(QuickLink l, string combos)
        {
            if (links == null) LoadLinks();
            foreach (QuickLink x in links)
                if (string.Equals(x.Target, l.Target, StringComparison.OrdinalIgnoreCase)) x.Keys = combos ?? "";
            l.Keys = combos ?? "";
            SaveLinks();
            LinksWindow.Refresh();
        }

        // Atajos, with that link's field listening for its new shortcut (from the quick links window).
        public void RecordLinkKeys(QuickLink l)
        {
            ShowHome("keys", false);
            if (home != null) home.RecordLink(l);
        }

        // Pressed its shortcut: opened at once; the mascot, when out, says so, and a failure is always told.
        void RunLink(string action)
        {
            QuickLink l = LinkOf(action);
            if (l == null) return;
            string why = OpenLink(l);
            if (why != null)
            {
                if (PetShown) pet.FocusSay(why);
                else Notify("No se ha podido abrir \u00AB" + l.Name + "\u00BB", why);
                return;
            }
            if (PetShown && settings.MascotTalks) pet.SayOpening(l);
        }

        // "--link Intranet" or "--link 2": a link by its name (or the start of it) or its place in the list.
        void RunLinkNamed(string which)
        {
            List<QuickLink> all = Links;
            QuickLink pick = null;
            int n;
            string w = (which ?? "").Trim();
            if (int.TryParse(w, out n) && n >= 1 && n <= all.Count) pick = all[n - 1];
            if (pick == null) foreach (QuickLink l in all) if (string.Equals(l.Name, w, StringComparison.CurrentCultureIgnoreCase)) { pick = l; break; }
            if (pick == null && w.Length > 0)
            {
                string f = PdfPageText.Fold(w);
                foreach (QuickLink l in all) if (PdfPageText.Fold(l.Name).StartsWith(f, StringComparison.Ordinal)) { pick = l; break; }
            }
            if (pick == null)
            {
                List<string> names = new List<string>();
                foreach (QuickLink l in all) names.Add("\u00AB" + l.Name + "\u00BB");
                Notify("No encuentro el acceso \u00AB" + w + "\u00BB", all.Count == 0 ? "Todav\u00EDa no tienes accesos r\u00E1pidos." : "Tus accesos: " + string.Join(", ", names.ToArray()) + ".");
                return;
            }
            RunLink(LinkAction(pick));
        }

        // Opens it the way Windows would (the browser, Explorer, the program); null when it opened, or why not.
        public string OpenLink(QuickLink l)
        {
            try
            {
                if (l.Folder)
                {
                    // Without the last backslash (a quoted "C:\Carpeta\" ends in an escaped quote), except for a drive.
                    string p = l.Target.TrimEnd('\\', '/');
                    // A drive goes unquoted for the same reason ("D:\" has no spaces).
                    Process.Start(Native.Explorer, p.Length == 2 && p[1] == ':' ? p + "\\" : "\"" + p + "\"");
                }
                else Process.Start(new ProcessStartInfo(l.Target) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                Log("Acceso r\u00E1pido: " + ex.Message);
                return l.Web ? "No he podido abrir el navegador." : "No he podido abrir \u00AB" + l.Name + "\u00BB: puede que ya no exista.";
            }
        }

        static string LinkText(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim(); }

        // What may come from accesos.txt: a web address or a full path (no other schemes, no quotes that would break the
        // command line), as adding one only allows.
        static bool Safe(string target)
        {
            if (target.Length == 0 || target.IndexOf('"') >= 0) return false;
            Uri u;
            if (Uri.TryCreate(target, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)) return true;
            return target.StartsWith("\\\\") || (target.Length >= 3 && char.IsLetter(target[0]) && target[1] == ':' && (target[2] == '\\' || target[2] == '/'));
        }

        void LoadLinks()
        {
            links = new List<QuickLink>();
            try
            {
                if (!File.Exists(LinksFile)) return;
                foreach (string line in File.ReadAllLines(LinksFile, Encoding.UTF8))
                {
                    QuickLink l = QuickLink.Parse(line);
                    if (l != null && Safe(l.Target) && links.Count < MaxLinks) links.Add(l);
                }
            }
            catch (Exception ex) { Log("Accesos r\u00E1pidos: " + ex.Message); }
        }

        void SaveLinks()
        {
            try
            {
                StringBuilder b = new StringBuilder();
                foreach (QuickLink l in links) b.Append(l.Line).Append('\n');
                Directory.CreateDirectory(Settings.DataDir);
                File.WriteAllText(LinksFile, b.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("Accesos r\u00E1pidos: " + ex.Message); }
        }
    }
}
