// Stackshot - The reports sent from this computer and how they are doing ("Mis reportes"). Nobody needs a GitHub
// account: the list lives here, and the state and replies come through the report relay (or GitHub's public API).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Stackshot
{
    public static class ReportHistory
    {
        public sealed class Entry
        {
            public int Number, Seen;     // Seen: replies already shown in the list
            public int Told;             // replies already announced by a notice
            public bool Idea;
            public DateTime SentUtc;
            public string Title;
            public string State = "";    // last known: "" not asked yet, "open", or "closed:<reason>"
        }

        public sealed class Reply
        {
            public bool Team;            // written by whoever makes Stackshot
            public DateTime AtUtc;
            public string Text;
        }

        public sealed class State
        {
            public bool Open;
            public string Reason = "";   // when closed: completed, not_planned, duplicate
            public List<Reply> Replies = new List<Reply>();
        }

        static readonly object gate = new object();
        static string FilePath { get { return Path.Combine(Settings.DataDir, "reportes.txt"); } }

        // Newest first. One line each: number, kind, sent (UTC ticks), replies seen, replies told, state, title (the first
        // lists had only number, kind, sent, seen and title).
        public static List<Entry> Load()
        {
            List<Entry> list = new List<Entry>();
            lock (gate)
            {
                try
                {
                    if (!File.Exists(FilePath)) return list;
                    foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string[] p = line.Split('\t');
                        int n, seen, told = 0;
                        long ticks;
                        if (p.Length < 5 || !int.TryParse(p[0], out n) || n <= 0 || !long.TryParse(p[2], out ticks) || !int.TryParse(p[3], out seen)) continue;
                        bool full = p.Length >= 7 && int.TryParse(p[4], out told);
                        list.Add(new Entry
                        {
                            Number = n, Idea = p[1] == "idea", SentUtc = new DateTime(ticks, DateTimeKind.Utc), Seen = seen,
                            Told = full ? told : seen, State = full ? p[5] : "", Title = full ? p[6] : p[4]
                        });
                    }
                }
                catch (Exception ex) { ShotStack.Log("Mis reportes: " + ex.Message); }
            }
            list.Sort(delegate(Entry a, Entry b) { return b.SentUtc.CompareTo(a.SentUtc); });
            return list;
        }

        public static void Add(int number, bool idea, string title)
        {
            lock (gate)
            {
                List<Entry> list = Load();
                list.RemoveAll(delegate(Entry e) { return e.Number == number; });
                list.Add(new Entry { Number = number, Idea = idea, SentUtc = DateTime.UtcNow, Title = OneLine(title) });
                Save(list);
            }
        }

        public static string Key(State s) { return s.Open ? "open" : "closed:" + s.Reason; }

        // What GitHub said is kept as known (and announced). shown: the list showed it, so its replies are no longer new.
        public static void Record(Dictionary<int, State> got, bool shown)
        {
            lock (gate)
            {
                List<Entry> list = Load();
                bool changed = false;
                foreach (Entry e in list)
                {
                    State s;
                    if (!got.TryGetValue(e.Number, out s)) continue;
                    int n = s.Replies.Count;
                    string k = Key(s);
                    if (k != e.State) { e.State = k; changed = true; }
                    if (n > e.Told) { e.Told = n; changed = true; }
                    if (shown && n != e.Seen) { e.Seen = n; changed = true; }
                }
                if (changed) Save(list);
            }
        }

        // What changed since it was last known: one line per report ("tiene una respuesta", "está arreglado"...), for a notice.
        public static List<string> Changes(List<Entry> list, Dictionary<int, State> got)
        {
            List<string> o = new List<string>();
            foreach (Entry e in list)
            {
                State s;
                if (!got.TryGetValue(e.Number, out s)) continue;
                string what = null;
                string k = Key(s);
                if (k != e.State && e.State.Length > 0)
                    what = s.Open ? I18n.T("se ha vuelto a abrir", "was reopened")
                         : s.Reason == "completed" ? (e.Idea ? I18n.T("est\u00E1 hecho", "is done") : I18n.T("est\u00E1 arreglado", "is fixed"))
                         : s.Reason == "not_planned" ? I18n.T("se ha descartado", "was discarded") : s.Reason == "duplicate" ? I18n.T("est\u00E1 repetido", "is a duplicate") : I18n.T("se ha cerrado", "was closed");
                int fresh = s.Replies.Count - e.Told;
                if (fresh > 0) what = (fresh == 1 ? I18n.T("tiene una respuesta nueva", "has a new reply") : I18n.Fmt("tiene {0} respuestas nuevas", "has {0} new replies", fresh)) + (what != null ? I18n.T(" y ", " and ") + what : "");
                if (what != null) o.Add(I18n.Fmt("\u00AB{0}\u00BB {1}", "\u201C{0}\u201D {1}", e.Title, what));
            }
            return o;
        }

        // Still worth asking about: not closed, or not asked yet.
        public static List<int> Pending(List<Entry> list)
        {
            List<int> o = new List<int>();
            foreach (Entry e in list) if (!e.State.StartsWith("closed", StringComparison.Ordinal)) o.Add(e.Number);
            return o;
        }

        static void Save(List<Entry> list)
        {
            try
            {
                StringBuilder b = new StringBuilder();
                foreach (Entry e in list)
                    b.Append(e.Number).Append('\t').Append(e.Idea ? "idea" : "bug").Append('\t').Append(e.SentUtc.Ticks).Append('\t')
                     .Append(e.Seen).Append('\t').Append(e.Told).Append('\t').Append(OneLine(e.State)).Append('\t').Append(OneLine(e.Title)).Append('\n');
                Directory.CreateDirectory(Settings.DataDir);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, b.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { ShotStack.Log("Mis reportes: " + ex.Message); }
        }

        static string OneLine(string s) { return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim(); }

        // ---- How they are doing (blocking: called off the UI thread)

        // Reports GitHub no longer has, or that did not come from the app, are left out of the answer.
        public static Dictionary<int, State> Fetch(List<int> numbers, out string error)
        {
            error = null;
            Dictionary<int, State> states = new Dictionary<int, State>();
            if (numbers.Count == 0) return states;
            if (numbers.Count > 20) numbers = numbers.GetRange(0, 20);
            if (!string.IsNullOrEmpty(Program.ReportUrl))
            {
                try { return FromRelay(numbers); }
                catch (Exception ex) { ShotStack.Log("Mis reportes (intermediario): " + ex.Message); }
            }
            // The relay is not there (or not up to date): GitHub's public API, which needs no account either.
            try { return FromGitHub(numbers); }
            catch (Exception ex)
            {
                ShotStack.Log("Mis reportes (GitHub): " + ex.Message);
                error = I18n.T("Ahora no se puede consultar c\u00F3mo van. Comprueba la conexi\u00F3n y pulsa Actualizar.", "Cannot check how they are doing right now. Check your connection and click Refresh.");
                return states;
            }
        }

        static Dictionary<int, State> FromRelay(List<int> numbers)
        {
            string url = Program.ReportUrl.Substring(0, Program.ReportUrl.LastIndexOf('/')) + "/status?n=" + Join(numbers);
            Dictionary<string, object> r = Get(url, null) as Dictionary<string, object>;
            object list;
            // JSON lists come as object[] (IList covers that and ArrayList alike).
            if (r == null || !r.TryGetValue("issues", out list) || !(list is IList)) throw new InvalidDataException("respuesta sin estado");
            Dictionary<int, State> states = new Dictionary<int, State>();
            foreach (object o in (IList)list)
            {
                Dictionary<string, object> i = o as Dictionary<string, object>;
                if (i == null) continue;
                State s = new State { Open = Str(i, "state") == "open", Reason = Str(i, "reason") };
                object cs;
                if (i.TryGetValue("comments", out cs) && cs is IList)
                    foreach (object c in (IList)cs)
                    {
                        Dictionary<string, object> d = c as Dictionary<string, object>;
                        if (d != null) s.Replies.Add(new Reply { Team = d.ContainsKey("team") && d["team"] is bool && (bool)d["team"], AtUtc = When(Str(d, "at")), Text = Str(d, "text") });
                    }
                states[Convert.ToInt32(i["number"])] = s;
            }
            return states;
        }

        static Dictionary<int, State> FromGitHub(List<int> numbers)
        {
            string api = "https://api.github.com/repos/rubenitx/stackshot/issues/";
            Dictionary<int, State> states = new Dictionary<int, State>();
            foreach (int n in numbers)
            {
                Dictionary<string, object> i = Get(api + n, "application/vnd.github+json") as Dictionary<string, object>;
                if (i == null) continue;
                State s = new State { Open = Str(i, "state") == "open", Reason = Str(i, "state_reason") };
                object count;
                if (i.TryGetValue("comments", out count) && count != null && Convert.ToInt32(count) > 0)
                {
                    IList cs = Get(api + n + "/comments?per_page=50", "application/vnd.github+json") as IList;
                    if (cs != null)
                        foreach (object c in cs)
                        {
                            Dictionary<string, object> d = c as Dictionary<string, object>;
                            if (d == null) continue;
                            string who = Str(d, "author_association");
                            s.Replies.Add(new Reply { Team = who == "OWNER" || who == "MEMBER" || who == "COLLABORATOR", AtUtc = When(Str(d, "created_at")), Text = Str(d, "body") });
                        }
                }
                states[n] = s;
            }
            return states;
        }

        static object Get(string url, string accept)
        {
            HttpWebRequest req = Net.Create(url, 15000, 20000);
            if (accept != null) req.Accept = accept;
            try
            {
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (StreamReader rd = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(rd.ReadToEnd());
            }
            catch (WebException ex)
            {
                // A report deleted on GitHub: simply not in the answer.
                HttpWebResponse r = ex.Response as HttpWebResponse;
                if (r != null && (r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Gone) && url.StartsWith("https://api.github.com/", StringComparison.Ordinal)) { r.Close(); return null; }
                if (r != null) r.Close();
                throw;
            }
        }

        static string Join(List<int> numbers)
        {
            StringBuilder b = new StringBuilder();
            foreach (int n in numbers) { if (b.Length > 0) b.Append(','); b.Append(n); }
            return b.ToString();
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static DateTime When(string iso)
        {
            DateTime t;
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t) ? t : DateTime.UtcNow;
        }
    }
}
