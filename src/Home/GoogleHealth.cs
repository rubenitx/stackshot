// Stackshot - A Pixel Watch or Fitbit through the Google Health API (the old Fitbit Web API is switched off): signed in
// once in the browser with Google (OAuth 2.0 with PKCE, no password ever reaches Stackshot), the keys kept encrypted for
// this Windows user, and today's steps, active minutes, sleep and, when Google allows it, water read straight from
// Google to this computer. Only the key exchange goes through the report relay, which adds the app's client secret
// (never in this public code); the health data does not.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Stackshot
{
    public static class GoogleHealth
    {
        public sealed class Today
        {
            public int Steps, StepsGoal, SleepMinutes, ActiveMinutes, WaterMl;
            public DateTime ReadAt;
            public bool Empty;          // Google had no steps for today at all (the watch hasn't synced yet)
        }

        const string Api = "https://health.googleapis.com/v4/users/me/dataTypes/";
        const string Scopes = "https://www.googleapis.com/auth/googlehealth.activity_and_fitness.readonly https://www.googleapis.com/auth/googlehealth.sleep.readonly";
        static readonly object gate = new object();
        static readonly object refreshing = new object();
        static string access, refresh;
        static DateTime expiresUtc;
        static bool loaded;

        static string KeyFile { get { return Path.Combine(Settings.DataDir, "google-health.dat"); } }
        static string TokenUrl { get { return Program.ReportUrl.Substring(0, Program.ReportUrl.LastIndexOf('/')) + "/google/token"; } }

        public static bool Available { get { return !string.IsNullOrEmpty(Program.GoogleClientId) && !string.IsNullOrEmpty(Program.ReportUrl); } }

        public static bool Connected
        {
            get { lock (gate) { Load(); return refresh != null; } }
        }

        // ---- Signing in

        static volatile bool connecting, cancel;
        public static bool Connecting { get { return connecting; } }
        public static string Cancelled { get { return I18n.T("Has cancelado la conexi\u00F3n.", "You cancelled the connection."); } }

        // Stops waiting for the browser (the sign-in tab was closed or left half done).
        public static void Cancel() { if (connecting) cancel = true; }

        // Opens Google's sign-in and waits (on another thread) for the browser to come back; done(error) with null when
        // it worked.
        public static void Connect(Action<string> done)
        {
            if (!Available) { done(I18n.T("Falta configurar la conexi\u00F3n con Google Health.", "The Google Health connection is not set up.")); return; }
            if (connecting) return;
            connecting = true;
            cancel = false;
            Uri back = new Uri(Program.WatchRedirect);
            string verifier = Random(64), state = Random(24);
            string challenge = Base64Url(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            TcpListener ear = null;
            try
            {
                // Loopback only: no firewall question, no administrator rights.
                ear = new TcpListener(IPAddress.Loopback, back.Port);
                ear.Start();
                string url = "https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id=" + Uri.EscapeDataString(Program.GoogleClientId) +
                             "&redirect_uri=" + Uri.EscapeDataString(Program.WatchRedirect) + "&scope=" + Uri.EscapeDataString(Scopes) +
                             "&code_challenge=" + challenge + "&code_challenge_method=S256&state=" + state + "&access_type=offline&prompt=consent";
                Process.Start(url);
            }
            catch (Exception ex)
            {
                if (ear != null) ear.Stop();
                connecting = false;
                ShotStack.Log("Google Health: " + ex.Message);
                done(ex is SocketException ? I18n.Fmt("El puerto {0} est\u00E1 ocupado: cierra lo que lo use y vuelve a intentarlo.", "Port {0} is busy: close whatever uses it and try again.", back.Port) : I18n.T("No se ha podido abrir el inicio de sesi\u00F3n: ", "Could not open login: ") + ex.Message);
                return;
            }
            Thread t = new Thread(delegate()
            {
                string error;
                try { error = Wait(ear, back, state, verifier); }
                catch (Exception ex) { error = I18n.T("No se ha podido conectar: ", "Could not connect: ") + ex.Message; }
                try { ear.Stop(); } catch { }
                connecting = false;
                ShotStack.Log(error == null ? "Google Health: conectado" : "Google Health: " + error);
                done(error);
            });
            t.IsBackground = true;
            t.Start();
        }

        // The browser's request to the redirect address, five minutes at most; then the code is traded for the keys.
        static string Wait(TcpListener ear, Uri back, string state, string verifier)
        {
            DateTime until = DateTime.UtcNow.AddMinutes(5);
            while (DateTime.UtcNow < until)
            {
                if (cancel) return Cancelled;
                if (!ear.Pending()) { Thread.Sleep(150); continue; }
                using (TcpClient c = ear.AcceptTcpClient())
                using (NetworkStream s = c.GetStream())
                {
                    // A browser sends its request at once; a connection that sends nothing doesn't hold the wait (or Cancel).
                    s.ReadTimeout = 1500;
                    string line;
                    // An empty connection (browsers open some ahead of time) is dropped and the wait goes on.
                    try { line = new StreamReader(s, Encoding.ASCII).ReadLine() ?? ""; }
                    catch (IOException) { continue; }
                    string[] parts = line.Split(' ');
                    string target = parts.Length > 1 ? parts[1] : "";
                    if (!target.StartsWith(back.AbsolutePath, StringComparison.Ordinal)) { Answer(s, 404, Mark.Fail, I18n.T("P\u00E1gina no encontrada", "Page not found"), null, I18n.T("Esta direcci\u00F3n no es de Stackshot.", "This address does not belong to Stackshot.")); continue; }
                    Dictionary<string, string> q = Query(target);
                    string got, from;
                    // Google sends the state back with an error too: without it, the error comes from somewhere else.
                    if (q.TryGetValue("error", out got) && q.TryGetValue("state", out from) && from == state)
                    {
                        if (got == "access_denied")
                        {
                            Answer(s, 200, Mark.Off, I18n.T("Conexi\u00F3n cancelada", "Connection cancelled"), null, I18n.T("No se ha conectado nada. Puedes cerrar esta pesta\u00F1a y, cuando quieras, volver a intentarlo desde Stackshot.", "Nothing was connected. You can close this tab and try again whenever you like from Stackshot."));
                            return Cancelled;
                        }
                        Answer(s, 200, Mark.Fail, I18n.T("No se ha podido conectar", "Could not connect"), null, I18n.Fmt("Google ha respondido \u00AB{0}\u00BB. Cierra esta pesta\u00F1a y vuelve a intentarlo desde Stackshot.", "Google returned \u201C{0}\u201D. Close this tab and try again from Stackshot.", got));
                        return I18n.Fmt("Google ha dicho: {0}", "Google said: {0}", got);
                    }
                    string code, st;
                    if (!q.TryGetValue("code", out code) || !q.TryGetValue("state", out st) || st != state)
                    {
                        Answer(s, 400, Mark.Fail, I18n.T("Respuesta no v\u00E1lida", "Invalid response"), null, I18n.T("Esta respuesta no viene del inicio de sesi\u00F3n que ha abierto Stackshot. Vuelve a intentarlo desde la app.", "This response is not from the login opened by Stackshot. Try again from the app."));
                        continue;
                    }
                    string error = Token("grant_type=authorization_code&code=" + Uri.EscapeDataString(code) + "&code_verifier=" + verifier +
                                         "&redirect_uri=" + Uri.EscapeDataString(Program.WatchRedirect));
                    if (error == null) Answer(s, 200, Mark.Ok, I18n.T("Reloj ", "Watch "), I18n.T("conectado", "connected"), I18n.T("Stackshot ya puede ver tus pasos, tu actividad y tu sue\u00F1o. Puedes cerrar esta pesta\u00F1a y volver a la app.", "Stackshot can now see your steps, activity and sleep. You can close this tab and return to the app."));
                    else Answer(s, 200, Mark.Fail, I18n.T("No se ha podido conectar", "Could not connect"), null, error + I18n.T(" Cierra esta pesta\u00F1a y vuelve a intentarlo desde Stackshot.", " Close this tab and try again from Stackshot."));
                    return error;
                }
            }
            return I18n.T("Se acab\u00F3 el tiempo para iniciar sesi\u00F3n. Vuelve a intentarlo.", "Login timed out. Try again.");
        }

        enum Mark { Ok, Off, Fail }

        // The page the browser shows at the end, in the look of the website (light or dark, as the system is). word: the
        // end of the title in the website's gradient.
        static void Answer(NetworkStream s, int code, Mark mark, string title, string word, string text)
        {
            string icon = mark == Mark.Ok ? "M5 12.5l4.5 4.5L19 7.5" : mark == Mark.Off ? "M7.5 7.5l9 9M16.5 7.5l-9 9" : "M12 6.5v7M12 17.5v.01";
            string html = "<!doctype html><html lang=" + I18n.T("es", "en") + "><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Stackshot</title><style>" +
                ":root{color-scheme:light dark;--bg:#f5f5f7;--card:#fff;--ink:#1d1d1f;--sub:#6e6e73;--line:#0000000f;--shade:#0000001a;--g1:#8b5cf6;--g2:#4f7bff;--g3:#0891cc}" +
                "@media (prefers-color-scheme:dark){:root{--bg:#000;--card:#161618;--ink:#f5f5f7;--sub:#98989d;--line:#ffffff14;--shade:#00000080;--g1:#a78bfa;--g2:#6d8bff;--g3:#22c3ee}}" +
                "*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;padding:24px;background:var(--bg);color:var(--ink);" +
                "font:15px/1.55 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif}" +
                "main{width:100%;max-width:440px;padding:44px 36px 30px;background:var(--card);border:1px solid var(--line);border-radius:22px;text-align:center;" +
                "box-shadow:0 24px 70px var(--shade);animation:in .5s cubic-bezier(.2,.8,.2,1) both}" +
                ".mark{width:64px;height:64px;margin:0 auto 22px;border-radius:50%;display:grid;place-items:center;background:#8e8e93}" +
                ".ok{background:linear-gradient(135deg,var(--g1),var(--g2) 50%,var(--g3));box-shadow:0 10px 30px #6d8bff59}.fail{background:#ff453a}" +
                "h1{margin:0 0 10px;font:600 28px/1.15 'Segoe UI Variable Display','Segoe UI',system-ui,sans-serif;letter-spacing:-.015em}" +
                "h1 span{background:linear-gradient(100deg,var(--g1),var(--g2) 45%,var(--g3));-webkit-background-clip:text;background-clip:text;color:transparent}" +
                "p{margin:0;color:var(--sub)}footer{margin-top:30px;padding-top:18px;border-top:1px solid var(--line);font-size:13px;color:var(--sub)}" +
                "@keyframes in{from{opacity:0;transform:translateY(10px)}}@media (prefers-reduced-motion:reduce){main{animation:none}}@media (forced-colors:active){h1 span{background:none;color:CanvasText}}</style>" +
                "<main><div class='mark" + (mark == Mark.Ok ? " ok" : mark == Mark.Fail ? " fail" : "") + "'><svg width=30 height=30 viewBox='0 0 24 24' fill=none stroke=#fff " +
                "stroke-width=2.6 stroke-linecap=round stroke-linejoin=round aria-hidden=true><path d='" + icon + "'/></svg></div>" +
                "<h1>" + WebUtility.HtmlEncode(title) + (word != null ? "<span>" + WebUtility.HtmlEncode(word) + "</span>" : "") + "</h1>" +
                "<p>" + WebUtility.HtmlEncode(text) + "</p><footer>Stackshot</footer></main></html>";
            byte[] body = Encoding.UTF8.GetBytes(html);
            string reason = code == 200 ? "OK" : code == 400 ? "Bad Request" : "Not Found";
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            // A tab closed before the page arrives changes nothing: the connection's result stands.
            try
            {
                s.Write(head, 0, head.Length);
                s.Write(body, 0, body.Length);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }

        static Dictionary<string, string> Query(string target)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            int q = target.IndexOf('?');
            if (q < 0) return d;
            foreach (string kv in target.Substring(q + 1).Split('&'))
            {
                int e = kv.IndexOf('=');
                if (e > 0) d[Uri.UnescapeDataString(kv.Substring(0, e))] = Uri.UnescapeDataString(kv.Substring(e + 1).Replace('+', ' '));
            }
            return d;
        }

        // ---- Keys: traded and refreshed through the relay (which adds the secret), kept encrypted for this Windows user.

        static string Token(string form)
        {
            Dictionary<string, object> r;
            try { r = Send("POST", TokenUrl, "application/x-www-form-urlencoded", form, null) as Dictionary<string, object>; }
            catch (WebException ex)
            {
                HttpWebResponse res = ex.Response as HttpWebResponse;
                int code = res != null ? (int)res.StatusCode : 0;
                if (res != null) res.Close();
                // A refresh key that no longer works (revoked, or the week of a project still in testing): signed out.
                if (code == 400 || code == 401) { Forget(); return I18n.T("Google ya no acepta la conexi\u00F3n: vuelve a conectar el reloj.", "Google no longer accepts the connection: connect your watch again."); }
                if (code == 503) return I18n.T("Falta la clave de Google en el intermediario.", "The relay is missing the Google key.");
                return code != 0 ? I18n.Fmt("Google no responde ({0}).", "Google is not responding ({0}).", code) : I18n.T("No hay conexi\u00F3n con Google.", "No connection to Google.");
            }
            if (r == null || !r.ContainsKey("access_token")) return I18n.T("Google no ha dado las claves.", "Google did not provide the keys.");
            lock (gate)
            {
                access = r["access_token"] as string;
                if (r.ContainsKey("refresh_token")) refresh = r["refresh_token"] as string;
                int secs = r.ContainsKey("expires_in") ? Convert.ToInt32(r["expires_in"]) : 3600;
                expiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, secs - 120));
                if (refresh == null) return I18n.T("Google no ha dado una clave duradera: vuelve a conectar.", "Google did not provide a lasting key: connect again.");
                Save();
            }
            return null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(KeyFile)) return;
                byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), Entropy, DataProtectionScope.CurrentUser);
                string[] p = Encoding.UTF8.GetString(plain).Split('\n');
                if (p.Length < 3) return;
                access = p[0]; refresh = p[1];
                expiresUtc = new DateTime(long.Parse(p[2]), DateTimeKind.Utc);
            }
            catch (Exception ex) { ShotStack.Log("Google Health: claves ilegibles (" + ex.Message + ")"); access = refresh = null; }
        }

        static void Save()
        {
            try
            {
                byte[] plain = Encoding.UTF8.GetBytes(access + "\n" + refresh + "\n" + expiresUtc.Ticks);
                Directory.CreateDirectory(Settings.DataDir);
                File.WriteAllBytes(KeyFile, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex) { ShotStack.Log("Google Health: no se han podido guardar las claves (" + ex.Message + ")"); }
        }

        static readonly byte[] Entropy = Encoding.ASCII.GetBytes("Stackshot.GoogleHealth.v1");

        static void Forget()
        {
            lock (gate)
            {
                access = refresh = null;
                try { if (File.Exists(KeyFile)) File.Delete(KeyFile); } catch { }
            }
        }

        // Signed out here and at Google.
        public static void Disconnect()
        {
            string token;
            lock (gate) { Load(); token = refresh; }
            Forget();
            if (token == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Send("POST", "https://oauth2.googleapis.com/revoke", "application/x-www-form-urlencoded", "token=" + Uri.EscapeDataString(token), null); }
                catch (Exception ex) { ShotStack.Log("Google Health: revocar: " + ex.Message); }
            });
        }

        // ---- Today's numbers (blocking: call off the UI thread)

        public static Today Read(out string error)
        {
            error = null;
            string key;
            lock (gate)
            {
                Load();
                if (refresh == null) { error = I18n.T("Sin conectar.", "Not connected."); return null; }
                key = access;
            }
            if (key == null || DateTime.UtcNow >= expiresUtc)
            {
                // One refresh at a time (the mascot and the main window may read together); whoever waited reuses it.
                lock (refreshing)
                {
                    string token;
                    bool still;
                    lock (gate)
                    {
                        key = access;
                        token = refresh;
                        still = key == null || DateTime.UtcNow >= expiresUtc;
                    }
                    if (token == null) { error = I18n.T("Sin conectar.", "Not connected."); return null; }
                    if (still)
                    {
                        error = Token("grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(token));
                        if (error != null) return null;
                        lock (gate) key = access;
                    }
                }
            }
            Today t = new Today { ReadAt = DateTime.Now, StepsGoal = 10000 };
            try
            {
                // Steps first: if this fails, nothing else will work either.
                Dictionary<string, object> steps = Dict(Rollup(key, "steps"), "steps");
                t.Empty = steps == null || !steps.ContainsKey("countSum");
                t.Steps = Long(steps, "countSum");
            }
            catch (WebException ex) { error = Explain(ex); return null; }
            // The rest is best effort: a data type the watch doesn't have, or that Google doesn't let read yet, is left at 0.
            t.ActiveMinutes = Try(delegate
            {
                int sum = 0;
                IList levels = List(Dict(Rollup(key, "active-minutes"), "activeMinutes"), "activeMinutesRollupByActivityLevel");
                if (levels != null)
                    foreach (object o in levels)
                    {
                        Dictionary<string, object> l = o as Dictionary<string, object>;
                        string level = l != null && l.ContainsKey("activityLevel") ? l["activityLevel"] as string : "";
                        if (level == "MODERATE" || level == "VIGOROUS") sum += Long(l, "activeMinutesSum");
                    }
                return sum;
            }, "minutos activos");
            t.SleepMinutes = Try(delegate { return LastNight(key); }, "sue\u00F1o");
            // Water is left out: it needs a nutrition permission Stackshot doesn't ask for.
            return t;
        }

        static readonly HashSet<string> missing = new HashSet<string>();

        static int Try(Func<int> read, string what)
        {
            try { return read(); }
            catch (Exception ex)
            {
                lock (missing) if (missing.Add(what)) ShotStack.Log("Google Health: sin " + what + " (" + ex.Message + ")");
                return 0;
            }
        }

        static string Explain(WebException ex)
        {
            HttpWebResponse res = ex.Response as HttpWebResponse;
            int code = res != null ? (int)res.StatusCode : 0;
            if (res != null) res.Close();
            if (code == 401) lock (gate) expiresUtc = DateTime.MinValue;   // refreshed at the next reading
            if (code == 403) return I18n.T("Google no deja leer los datos: revisa que el reloj est\u00E9 vinculado en la app Google Health.", "Google won't let Stackshot read the data: check that the watch is linked in the Google Health app.");
            return code == 429 ? I18n.T("Google pide esperar un poco.", "Google asks to wait a moment.") : code != 0 ? I18n.Fmt("Google no responde ({0}).", "Google is not responding ({0}).", code) : I18n.T("No hay conexi\u00F3n con Google.", "No connection to Google.");
        }

        // Today's total of one data type, from midnight to midnight in local time.
        static Dictionary<string, object> Rollup(string key, string type)
        {
            DateTime d = DateTime.Today, n = d.AddDays(1);
            string body = "{\"range\":{\"start\":{\"date\":{\"year\":" + d.Year + ",\"month\":" + d.Month + ",\"day\":" + d.Day + "},\"time\":{\"hours\":0}}," +
                          "\"end\":{\"date\":{\"year\":" + n.Year + ",\"month\":" + n.Month + ",\"day\":" + n.Day + "},\"time\":{\"hours\":0}}},\"windowSizeDays\":1}";
            Dictionary<string, object> r = Send("POST", Api + type + "/dataPoints:dailyRollUp", "application/json", body, key) as Dictionary<string, object>;
            IList points = List(r, "rollupDataPoints");
            return points != null && points.Count > 0 ? points[points.Count - 1] as Dictionary<string, object> : null;
        }

        // Minutes asleep in the sleep that ended today (the main one; or all of them if none is marked main).
        static DateTime sleepNoted;

        static int LastNight(string key)
        {
            string filter = "sleep.interval.civil_end_time >= \"" + DateTime.Today.ToString("yyyy-MM-dd") + "\"";
            Dictionary<string, object> r = Send("GET", Api + "sleep/dataPoints?pageSize=25&filter=" + Uri.EscapeDataString(filter), null, null, key) as Dictionary<string, object>;
            IList points = List(r, "dataPoints");
            int main = 0, all = 0;
            if (points != null)
                foreach (object o in points)
                {
                    Dictionary<string, object> sleep = Dict(o as Dictionary<string, object>, "sleep");
                    int m = Long(Dict(sleep, "summary"), "minutesAsleep");
                    all += m;
                    Dictionary<string, object> meta = Dict(sleep, "metadata");
                    if (meta != null && meta.ContainsKey("mainSleep") && meta["mainSleep"] is bool && (bool)meta["mainSleep"]) main += m;
                }
            int n = points != null ? points.Count : 0;
            if (n == 0 && sleepNoted != DateTime.Today) { sleepNoted = DateTime.Today; ShotStack.Log("Google Health: sin sue\u00F1o registrado desde anoche"); }
            return main > 0 ? main : all;
        }

        static object Send(string method, string url, string type, string body, string key)
        {
            HttpWebRequest req = Net.Create(url, 15000, 20000);
            req.Method = method;
            if (key != null) req.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
            if (body != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentType = type;
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            }
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader rd = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                string text = rd.ReadToEnd();
                return text.Length == 0 ? null : new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(text);
            }
        }

        static Dictionary<string, object> Dict(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        // JSON lists come as object[] (IList covers it).
        static IList List(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as IList : null;
        }

        // Google sends 64-bit numbers as strings.
        static int Long(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return 0;
            try { return (int)Math.Min(int.MaxValue, Math.Round(Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture))); }
            catch { return 0; }
        }

        static string Random(int n)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            byte[] b = new byte[n];
            using (RandomNumberGenerator r = RandomNumberGenerator.Create()) r.GetBytes(b);
            StringBuilder s = new StringBuilder(n);
            foreach (byte x in b) s.Append(chars[x % chars.Length]);
            return s.ToString();
        }

        static string Base64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }
}
