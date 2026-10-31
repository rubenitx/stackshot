// Stackshot - The mascot as an assistant: quick reminders ("Recuérdame en 30 min..."), notes appended to Notas.md,
// files and text dropped on it (each to where it opens) and a word when a SOAP fault arrives in the watched folder.
// Reminders are kept on disk and wait until they can be shown (never lost to a call or a full-screen app); one due
// while the computer was off comes as soon as it is back, saying when it was for.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Stackshot
{
    public sealed class Reminder
    {
        public DateTime DueUtc;
        public string Text;
    }

    public partial class ShotStack
    {
        readonly List<Reminder> reminders = new List<Reminder>();
        Timer remindTimer;
        double remindWait;

        static string RemindersFile { get { return Path.Combine(Settings.DataDir, "recordatorios.txt"); } }

        void StartAssist()
        {
            if (Test) return;
            LoadReminders();
            remindTimer = new Timer { Interval = 15000 };
            remindTimer.Tick += delegate
            {
                try { RemindTick(); }
                catch (Exception ex) { Log("Recordatorios: " + ex.Message); }
            };
            if (reminders.Count > 0) remindTimer.Start();
            StickyNotes.RestoreAll(this);
        }

        // ---- Reminders

        public List<Reminder> Reminders
        {
            get
            {
                List<Reminder> r = new List<Reminder>(reminders);
                r.Sort(delegate(Reminder a, Reminder b) { return a.DueUtc.CompareTo(b.DueUtc); });
                return r;
            }
        }

        public void AddReminder(string text, DateTime dueLocal, bool quiet)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            if (text.Length == 0) return;
            reminders.Add(new Reminder { DueUtc = dueLocal.ToUniversalTime(), Text = text });
            SaveReminders();
            if (remindTimer != null) remindTimer.Start();
            Log("Recordatorio para las " + dueLocal.ToString("HH:mm"));
            if (quiet) return;
            string when = WhenText(dueLocal);
            if (PetShown) pet.FocusSay(I18n.Fmt("Te lo recuerdo {0}.", "I'll remind you {0}.", when));
            else Notify(I18n.T("Recordatorio guardado", "Reminder saved"), I18n.Fmt("Te lo recuerdo {0}: \u00AB{1}\u00BB.", "I'll remind you {0}: \u201C{1}\u201D.", when, text));
        }

        public void RemoveReminder(Reminder r)
        {
            reminders.Remove(r);
            SaveReminders();
        }

        // "a las 11:45", "mañana a las 9:00", "el 16/10 a las 9:00".
        public static string WhenText(DateTime local)
        {
            string at = I18n.Fmt("a las {0}", "at {0}", local.ToString("H:mm"));
            if (local.Date == DateTime.Today) return at;
            if (local.Date == DateTime.Today.AddDays(1)) return I18n.Fmt("ma\u00F1ana {0}", "tomorrow {0}", at);
            return I18n.Fmt("el {0} {1}", "on {0} {1}", local.ToString("d/M"), at);
        }

        void RemindTick()
        {
            if (exiting || reminders.Count == 0) { if (remindTimer != null) remindTimer.Stop(); return; }
            Reminder due = null;
            foreach (Reminder r in Reminders) if (r.DueUtc <= DateTime.UtcNow) { due = r; break; }
            if (due == null) return;
            // Shown when someone is there to see it, and kept until it can be: the mascot when it is free, otherwise a
            // notice (which itself waits through presentations and full-screen apps).
            if (Native.IdleMs() >= Wellness.ActiveIdleMs) return;
            DateTime local = due.DueUtc.ToLocalTime();
            string late = DateTime.UtcNow - due.DueUtc > TimeSpan.FromMinutes(3) ? I18n.Fmt("Era para {0}: ", "Was for {0}: ", WhenText(local).Replace("ma\u00F1ana ", "").Replace("tomorrow ", "")) : "";
            Teller teller = TellerFor(ref remindWait, 60000);
            if (teller == Teller.Wait) return;
            if (teller == Teller.Pet)
            {
                RemoveReminder(due);
                pet.ReminderCard(late + due.Text, delegate(int minutes) { AddReminder(due.Text, DateTime.Now.AddMinutes(minutes), true); });
                return;
            }
            if (ToastQuietNow(Screen.PrimaryScreen.Bounds) != null) return;
            RemoveReminder(due);
            Notify(I18n.T("Recordatorio", "Reminder"), late + due.Text, NoticeKind.Info, I18n.T("10 min m\u00E1s", "10 more min"), delegate { AddReminder(due.Text, DateTime.Now.AddMinutes(10), true); });
        }

        void LoadReminders()
        {
            reminders.Clear();
            try
            {
                if (!File.Exists(RemindersFile)) return;
                foreach (string line in File.ReadAllLines(RemindersFile, Encoding.UTF8))
                {
                    string[] p = line.Split(new char[] { '\t' }, 2);
                    long ticks;
                    if (p.Length == 2 && long.TryParse(p[0], out ticks) && p[1].Trim().Length > 0)
                        reminders.Add(new Reminder { DueUtc = new DateTime(ticks, DateTimeKind.Utc), Text = p[1].Trim() });
                }
            }
            catch (Exception ex) { Log("Recordatorios: " + ex.Message); }
        }

        void SaveReminders()
        {
            try
            {
                StringBuilder b = new StringBuilder();
                foreach (Reminder r in reminders) b.Append(r.DueUtc.Ticks).Append('\t').Append(r.Text).Append('\n');
                Directory.CreateDirectory(Settings.DataDir);
                File.WriteAllText(RemindersFile, b.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("Recordatorios: " + ex.Message); }
        }

        // ---- Notes

        // Notas.md in the save folder, next to the captures.
        public string NotesPath { get { return Path.Combine(settings.SaveFolder, "Notas.md"); } }

        public bool AddNote(string text)
        {
            text = (text ?? "").Replace("\r", "").Trim();
            if (text.Length == 0) return false;
            try
            {
                Directory.CreateDirectory(settings.SaveFolder);
                bool fresh = !File.Exists(NotesPath);
                // A note of several lines keeps them, indented under its bullet. Blank lines go: one would end the note.
                List<string> kept = new List<string>();
                foreach (string l in text.Split('\n')) if (l.Trim().Length > 0) kept.Add(l.TrimEnd());
                string body = string.Join("\n  ", kept.ToArray());
                string entry = (fresh ? I18n.T("# Notas\n\n", "# Notes\n\n") : "") + "- [ ] **" + DateTime.Now.ToString("d/M/yyyy H:mm", CultureInfo.GetCultureInfo("es-ES")) + "** \u00B7 " + body + "\n";
                File.AppendAllText(NotesPath, entry, new UTF8Encoding(false));
                Log("Nota apuntada");
                return true;
            }
            catch (Exception ex)
            {
                Log("Notas: " + ex.Message);
                Notify(I18n.T("No se pudo apuntar", "Could not add note"), Explain(ex, NotesPath));
                return false;
            }
        }

        // A note as Notas.md keeps it: its first line (to find it again), when, the text on one line and whether it is done.
        // Key is that first line without its tick box, so it stays the same when the note is ticked; Body holds the lines
        // under it as written (a list keeps its items), without their indent.
        public sealed class Note
        {
            public string Line, Key, When, Text;
            public bool Done;
            public readonly List<string> Body = new List<string>();
        }

        // The latest notes, newest first.
        public List<Note> LastNotes(int count)
        {
            List<Note> all = ReadNotes();
            all.Reverse();
            if (all.Count > count) all.RemoveRange(count, all.Count - count);
            return all;
        }

        // The newest note with that key, as the file has it now, or null when it is gone.
        public Note FindNote(string key)
        {
            List<Note> all = ReadNotes();
            for (int i = all.Count - 1; i >= 0; i--) if (all[i].Key == key) return all[i];
            return null;
        }

        // Every note in the file, oldest first.
        List<Note> ReadNotes()
        {
            List<Note> all = new List<Note>();
            try
            {
                if (!File.Exists(NotesPath)) return all;
                foreach (string line in File.ReadAllLines(NotesPath, Encoding.UTF8))
                {
                    // "- [ ] **14/10/2026 16:05** - text" ("- **...**" before notes could be ticked); the indented lines
                    // under it belong to it.
                    bool done;
                    string rest = NoteRest(line, out done);
                    if (rest != null && rest.StartsWith("**"))
                    {
                        int end = rest.IndexOf("**", 2);
                        if (end < 0) continue;
                        all.Add(new Note { Line = line, Key = rest, When = rest.Substring(2, end - 2), Text = rest.Substring(end + 2).TrimStart(' ', '\u00B7').Trim(), Done = done });
                    }
                    else if (line.StartsWith("  ") && all.Count > 0 && all[all.Count - 1] != null && line.Trim().Length > 0)
                    {
                        Note last = all[all.Count - 1];
                        last.Text += " " + line.Trim();
                        last.Body.Add(line.Substring(2).TrimEnd());
                    }
                    else all.Add(null);   // anything else, a blank line too, ends the note above (as EditNote counts it)
                }
            }
            catch (Exception ex) { Log("Notas: " + ex.Message); }
            all.RemoveAll(delegate(Note n) { return n == null; });
            return all;
        }

        // Ticks or unticks one of the note's boxes: 0 is the note itself, 1 on its body lines (only those written as
        // "- [ ] ..."). False when the note or that line is no longer in the file as it was.
        public bool NoteLineDone(Note n, int index, bool done)
        {
            if (index <= 0) return NoteDone(n, done);
            bool ok = false;
            bool edited = EditNote(n, delegate(List<string> lines, int at, int count)
            {
                if (index >= count) return;
                string line = lines[at + index];
                int box = line.IndexOf("- [");
                if (box < 0 || line.Length < box + 6 || line.Substring(0, box).Trim().Length > 0 || line[box + 4] != ']') return;
                lines[at + index] = line.Substring(0, box + 3) + (done ? "x" : " ") + line.Substring(box + 4);
                ok = true;
            });
            return edited && ok;
        }

        // What follows "- " and the tick box, if the line is a list item (null otherwise).
        static string NoteRest(string line, out bool done)
        {
            done = false;
            if (!line.StartsWith("- ")) return null;
            string rest = line.Substring(2);
            if (rest.StartsWith("[ ] ")) return rest.Substring(4);
            if (rest.StartsWith("[x] ") || rest.StartsWith("[X] ")) { done = true; return rest.Substring(4); }
            return rest;
        }

        // Ticks a note or unticks it; false when it is no longer in the file as it was.
        public bool NoteDone(Note n, bool done)
        {
            return EditNote(n, delegate(List<string> lines, int at, int count)
            {
                bool was;
                lines[at] = "- [" + (done ? "x" : " ") + "] " + NoteRest(lines[at], out was);
            });
        }

        // Removes a note and the lines that belong to it.
        public bool DeleteNote(Note n)
        {
            bool ok = EditNote(n, delegate(List<string> lines, int at, int count) { lines.RemoveRange(at, count); });
            if (ok) StickyNotes.Forget(n.Key);
            return ok;
        }

        // Finds the note again by its first line (the last one like it: the newest) and rewrites the file.
        bool EditNote(Note n, Action<List<string>, int, int> change)
        {
            try
            {
                if (!File.Exists(NotesPath)) return false;
                List<string> lines = new List<string>(File.ReadAllLines(NotesPath, Encoding.UTF8));
                int at = lines.LastIndexOf(n.Line);
                if (at < 0) return false;
                int count = 1;
                while (at + count < lines.Count && lines[at + count].StartsWith("  ") && lines[at + count].Trim().Length > 0) count++;
                change(lines, at, count);
                File.WriteAllText(NotesPath, string.Join("\n", lines.ToArray()) + "\n", new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                Log("Notas: " + ex.Message);
                Notify(I18n.T("No se pudo cambiar la nota", "Could not update note"), Explain(ex, NotesPath));
                return false;
            }
        }

        public void OpenNotes()
        {
            if (!File.Exists(NotesPath))
                try { Directory.CreateDirectory(settings.SaveFolder); File.WriteAllText(NotesPath, I18n.T("# Notas\n\n", "# Notes\n\n"), new UTF8Encoding(false)); }
                catch (Exception ex) { Log("Notas: " + ex.Message); }
            OpenViewerFile(NotesPath);
        }

        // ---- Dropped on the mascot

        static readonly string[] Pictures = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff" };
        static readonly string[] Videos = { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".wmv", ".m4v" };
        static readonly string[] Docs = { ".md", ".markdown", ".xml", ".txt" };

        static bool Is(string path, string[] exts) { return Array.IndexOf(exts, Path.GetExtension(path).ToLowerInvariant()) >= 0; }

        public static bool CanTake(string path) { return Is(path, Pictures) || Is(path, Videos) || Is(path, Docs) || Directory.Exists(path); }

        // Each file to where it opens (at most five), a folder to the quick links; what the mascot says about it.
        public string OpenDropped(string[] files)
        {
            int pics = 0, vids = 0, docs = 0, kept = 0;
            string skipped = null, folder = null, why = null;
            foreach (string f in files)
            {
                if (Directory.Exists(f))
                {
                    string no = AddLink(null, f);
                    if (no == null) { kept++; folder = LinkName(f); } else if (why == null) why = no;
                    continue;
                }
                if (pics + vids + docs >= 5 || !File.Exists(f)) continue;
                if (Is(f, Docs)) { OpenViewerFile(f); docs++; }
                else if (Is(f, Pictures) || Is(f, Videos)) { OpenMedia(f); if (Is(f, Videos)) vids++; else pics++; }
                else if (skipped == null) skipped = Path.GetFileName(f);
            }
            int n = pics + vids + docs;
            // Folders kept as quick links: said first (with the files opened, if any).
            if (kept > 0)
                return (kept == 1 ? I18n.Fmt("\u00A1Apuntada! \u00AB{0}\u00BB ya est\u00E1 en mis accesos r\u00E1pidos.", "Noted! \u201C{0}\u201D is now in my quick links.", folder) : I18n.Fmt("\u00A1Apuntadas {0} carpetas en mis accesos r\u00E1pidos!", "Noted {0} folders in my quick links!", kept)) +
                       (n > 0 ? " " + (n == 1 ? I18n.T("Y te abro el archivo.", "And I'll open the file for you.") : I18n.Fmt("Y te abro los {0} archivos.", "And I'll open the {0} files for you.", n)) : "");
            if (why != null && n == 0) return why;
            if (n == 0) return skipped != null ? I18n.Fmt("No s\u00E9 qu\u00E9 hacer con \u00AB{0}\u00BB. Su\u00E9ltame im\u00E1genes, v\u00EDdeos, XML o Markdown.", "I don't know what to do with \u201C{0}\u201D. Drop images, videos, XML or Markdown.", skipped) : null;
            if (n > 1) return I18n.Fmt("\u00A1Abro {0} archivos!", "Opening {0} files!", n);
            return pics == 1 ? I18n.T("\u00A1Al editor!", "To the editor!") : vids == 1 ? I18n.T("\u00A1A verlo!", "Let's watch it!") : I18n.T("\u00A1Te lo abro!", "Opening it for you!");
        }

        // ---- A SOAP fault arriving in the watched folder

        // Read off this thread; told by the mascot when it is free, otherwise by a notice (a fault shouldn't wait).
        void CheckArrivedFault(string path)
        {
            if (!settings.XmlFaultAlert) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string code = null, message = null, op = null;
                try
                {
                    if (new FileInfo(path).Length > 8 * 1024 * 1024) return;
                    string err;
                    string text = MarkdownTools.Load(path, out err);
                    XmlModel m;
                    int line, col;
                    if (text == null || !XmlDoc.Parse(text, out m, out err, out line, out col) || m == null) return;
                    XmlDoc.ExpandInner(m);
                    if (!XmlDoc.Fault(m, out code, out message)) return;
                    SoapInfo s = XmlSoap.Read(m);
                    op = s != null && s.Operation.Length > 0 ? s.Operation : null;
                }
                catch (Exception ex) { Log("XML con fallo: " + ex.Message); return; }
                Ui(delegate
                {
                    string what = (op != null ? op + ": " : "") + (string.IsNullOrEmpty(message) ? (string.IsNullOrEmpty(code) ? "SOAP Fault" : code) : message);
                    Log("XML con fallo: " + Path.GetFileName(path));
                    if (pet != null && pet.CanRemind) pet.FaultCard(I18n.T("Ojo: ha llegado un XML con fallo. ", "Watch out: an XML with a fault has arrived. ") + what, delegate { OpenViewerFile(path); });
                    else Notify(I18n.T("XML con fallo", "XML with fault"), Path.GetFileName(path) + " \u00B7 " + what, NoticeKind.Warning, I18n.T("Abrir el XML", "Open XML"), delegate { if (!exiting) OpenViewerFile(path); });
                });
            });
        }
    }
}
