// Stackshot - In-app updates from GitHub Releases, verified before anything runs.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Stackshot
{
    // Looks for a new release a little after startup, every three hours, and when the computer wakes up or gets its
    // network back (never in --test). Requests carry the last ETag, so an unchanged answer doesn't use up GitHub's rate
    // limit. Each new version is announced once (UpdateNotice); with automatic updates it is downloaded and verified in
    // the background instead, and installed at a quiet moment. An update is installed only if:
    //   - it comes from this repository's release downloads (fixed URL prefix, HTTPS),
    //   - its SHA-256 matches the digest GitHub reports for the asset, and
    //   - its RSA-PSS signature verifies with the public key below. The private key exists only as a secret of the
    //     release workflow, so a compromised download host or a re-uploaded asset can't push code to users.
    // Anything missing or wrong fails closed, and the file is verified again, held open, right before it runs.
    // MSI-managed installs only get a notice: IT deploys the new MSI.
    public static class Updater
    {
        const string Api = "https://api.github.com/repos/rubenitx/stackshot/releases/latest";
        const string Downloads = "https://github.com/rubenitx/stackshot/releases/download/";
        const string PublicKey = "<RSAKeyValue><Modulus>+WMiH/o5pUTcAyQmGtGFWfdjYmxL09Qte3q7eMMPBX4s7rxMNOSaqeL69kB3wVTZtduGIDzW2Mtmp+rYUl7zzXuN4/hUW5WEBtpOxpwe5bnOqN+43DILNNugqbsEYym9TLnwcsq2KtTNOXKY5ho8wZNw82yCvdLUGtbi8Rh0h/UMhOG9o2quLu+3RCFkkqjnFkzeaMkwhYfIeFKtUHVci1MEfyZG6zVUXYbka0gWCmGF5+uZ2lhksnewvviFhf/B2NY6ziLDY1yFOdBn0wxOL23G4mVmLdvuGGBhmfFyTPTPtCyEZiQchObTOOYH6JScAzx9LuuB8k5XCmX5Wpakmg/8KBdbc330F/D+O5uzPKt3r4E3p7LMSRw+JKNXYNZdwPzcoSZFP0LS59uD2h8O4mTdOO8QG3ugeeQR7LEfQCk9z4XZSZ9pobmCyDE5Yjg228uVBUf6N2JAiXcMZGv4PzlR7rA1F4PrVJ3pJoGd3k2q1HqdR8iA+++MvPifg03d</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        const long MaxDownload = 64L << 20;

        // When to look: shortly after startup, then every few hours; sooner after a failure (doubling up to the usual
        // gap), and again after waking up or reconnecting if the last answer is older than WakeGap.
        public static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan Every = TimeSpan.FromHours(3);
        public static readonly TimeSpan Retry = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan WakeGap = TimeSpan.FromMinutes(30);
        static readonly TimeSpan MinGap = TimeSpan.FromMinutes(2);     // between two looks (a flapping network)
        static readonly TimeSpan ReadyNag = TimeSpan.FromHours(24);    // ready but never a quiet moment: offer it by hand
        const int AwayMs = 2 * 60 * 1000;     // no input for this long: nobody is looking, a notice waits
        const int QuietMs = 10 * 60 * 1000;   // and for this long: a quiet time to close, update and reopen
        const int AutoTries = 3;              // downloads cut short before an automatic update is offered by hand

        public enum Step { None, Checking, Downloading, Verifying, Installing }

        public class Release
        {
            public Version Version;
            public string Tag, Page, ExeUrl, SigUrl, Sha256;
            public List<string> Notes = new List<string>();
        }

        public static Release Available;        // newer than this build, or null
        public static Step Doing;
        public static string Problem;           // why the last check or update failed; null if it didn't
        public static bool ProblemInstalling;   // the problem came from downloading or installing, not from the check
        public static double Fraction = -1;     // download progress 0-1 (-1 unknown)
        public static event Action Changed;     // state changes (Acerca de rebuilds)
        public static event Action Progress;    // download progress only (shown in place)

        public static bool Busy { get { return Doing != Step.None; } }

        // Installing, but held until the recording (or scrolling capture) under way has been saved.
        public static bool Held { get { return hold != null && hold.Enabled; } }

        // The environment, replaceable by the tests: may a notice appear now, is it a quiet time to install, show one.
        internal static Func<bool> CanPresent = PresentNow;
        internal static Func<bool> Quiet = QuietNow;
        internal static Action<Release> Present = ShowNotice;

        static ShotStack owner;
        static Timer timer, nudge, hold;
        static DateTime due = DateTime.MaxValue, lastTry = DateTime.MinValue;
        static int failures;
        static string etag;
        static Release answer;                  // GitHub's last answer, kept for "not modified"
        static Release ready;                   // downloaded and verified, waiting to be installed
        static string readyExe;
        static byte[] readySig;
        static DateTime readyAt;
        static string autoFailed;               // tag whose automatic update failed: offered by hand from then on
        static string autoTag, problemTag;
        static int autoCuts;                    // automatic downloads of autoTag cut short so far
        static bool installAfterCheck;          // "Descargar e instalar" pressed while a check was under way
        static bool launchManual;               // the install under way was asked for by hand
        static Process installer;
        static UpdateNotice notice;

        static Settings S { get { return owner.Settings; } }
        static string UpdateDir { get { return Path.Combine(Settings.DataDir, "update"); } }

        public static void Start(ShotStack o)
        {
            owner = o;
            if (ShotStack.Test) return;
            // Old downloads go a little later: right after an update the installer that started this copy (it runs from
            // that folder) may still be closing.
            Timer clean = new Timer();
            clean.Interval = 10000;
            clean.Tick += delegate { clean.Dispose(); CleanUpdateDir(); };
            clean.Start();
            timer = new Timer();
            timer.Tick += delegate { timer.Stop(); if (S.CheckUpdates || failedBefore != null) Check(false); };
            // While something waits (a notice for when someone is back, an install for a quiet moment), look every minute.
            nudge = new Timer();
            nudge.Interval = 60 * 1000;
            nudge.Tick += delegate { Act(); };
            // Back after a failed update: look at once (even with checks off, since it was under way), so Acerca de
            // explains what happened instead of saying all is up to date.
            if (S.CheckUpdates || failedBefore != null) Schedule(FirstDelay(failedBefore != null));
            SystemEvents.PowerModeChanged += OnPower;
            NetworkChange.NetworkAvailabilityChanged += OnNetwork;
        }

        // Everything in the update folder but the download waiting to be installed.
        static void CleanUpdateDir()
        {
            try
            {
                if (!Directory.Exists(UpdateDir)) return;
                foreach (string f in Directory.GetFiles(UpdateDir))
                    if (!string.Equals(f, readyExe, StringComparison.OrdinalIgnoreCase) && !(Busy && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) Delete(f);
                if (readyExe == null && !Busy && Directory.GetFileSystemEntries(UpdateDir).Length == 0) Directory.Delete(UpdateDir);
            }
            catch { }
        }

        static Version failedBefore;

        // Started again after the installer of that version failed (it had already closed this copy): that version is
        // no longer installed on its own, only offered by hand, with the reason in Acerca de.
        public static void FailedBefore(Version v)
        {
            failedBefore = v;
            ShotStack.Log("La actualizaci\u00F3n a la " + v.ToString(3) + " no se pudo instalar; se ofrecer\u00E1 a mano");
        }

        public static void Stop()
        {
            if (timer != null) { timer.Dispose(); timer = null; }
            if (nudge != null) { nudge.Dispose(); nudge = null; }
            if (hold != null) { hold.Dispose(); hold = null; }
            try { SystemEvents.PowerModeChanged -= OnPower; } catch { }
            try { NetworkChange.NetworkAvailabilityChanged -= OnNetwork; } catch { }
            if (notice != null && !notice.IsDisposed) notice.Close();
            notice = null;
        }

        static void Schedule(TimeSpan after)
        {
            if (timer == null) return;
            timer.Stop();
            timer.Interval = (int)Math.Max(1000, Math.Min(int.MaxValue, after.TotalMilliseconds));
            timer.Start();
            due = DateTime.UtcNow + after;
        }

        static void OnPower(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) Woke(TimeSpan.FromSeconds(20)); // the network needs a moment after waking
        }

        static void OnNetwork(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable) Woke(TimeSpan.FromSeconds(5));
        }

        // Back from sleep or back online (any thread).
        static void Woke(TimeSpan after)
        {
            ShotStack o = owner;
            if (o == null) return;
            o.Ui(delegate
            {
                if (timer == null || !S.CheckUpdates || Busy) return;
                DateTime now = DateTime.UtcNow;
                if (!DueOnWake(now, LastChecked(S), failures)) return;
                // A network that comes and goes doesn't turn into a burst of requests.
                TimeSpan since = now - lastTry;
                if (since < MinGap && MinGap - since > after) after = MinGap - since;
                if (!timer.Enabled || due > now + after) Schedule(after);
            });
        }

        // The update switches changed.
        public static void SettingsChanged()
        {
            if (owner == null) return;
            if (timer != null)
            {
                if (!S.CheckUpdates) timer.Stop();
                else if (!timer.Enabled && !Busy)
                {
                    TimeSpan since = DateTime.UtcNow - LastChecked(S);
                    Schedule(since >= Every ? TimeSpan.FromSeconds(5) : Every - since);
                }
            }
            Act();
        }

        // ---- Timing and announcing rules (pure, tested)

        // The first look after startup: soon, and at once when an update has just failed.
        public static TimeSpan FirstDelay(bool afterFailedUpdate)
        {
            return afterFailedUpdate ? TimeSpan.FromSeconds(2) : FirstCheck;
        }

        // Next automatic look after a check: the usual gap, or 15, 30, 60... minutes after failures in a row.
        public static TimeSpan NextDelay(int failuresInARow)
        {
            if (failuresInARow <= 0) return Every;
            double minutes = Retry.TotalMinutes * Math.Pow(2, Math.Min(failuresInARow - 1, 8));
            return TimeSpan.FromMinutes(Math.Min(Every.TotalMinutes, minutes));
        }

        // After waking up or reconnecting: look again if the last good answer is old or the last try failed.
        public static bool DueOnWake(DateTime nowUtc, DateTime lastUtc, int failuresInARow)
        {
            return failuresInARow > 0 || nowUtc - lastUtc >= WakeGap;
        }

        // Each version is announced once.
        public static bool ShouldAnnounce(Release r, string announced)
        {
            return r != null && !string.Equals(r.Tag, announced, StringComparison.Ordinal);
        }

        static bool SameVersion(Version a, Version b)
        {
            return a.Major == b.Major && a.Minor == b.Minor && Math.Max(0, a.Build) == Math.Max(0, b.Build);
        }

        // What the new version is started with: it installs itself and starts again quietly after an automatic update,
        // or on Acerca de after one asked for by hand.
        public static string InstallArguments(bool manual, bool startup)
        {
            return "--install" + (startup ? " --startup" : "") + " --updated" + (manual ? " --home" : "");
        }

        public static DateTime LastChecked(Settings s)
        {
            DateTime t;
            if (s == null || !DateTime.TryParse(s.LastUpdateCheck, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) return DateTime.MinValue;
            return t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : t;
        }

        // ---- Checking

        // "Buscar ahora".
        public static void Check() { Check(true); }

        static void Check(bool manual)
        {
            if (owner == null) return;
            if (Busy)
            {
                if (!manual) Schedule(Retry); // an update is under way: look again later
                return;
            }
            Doing = Step.Checking;
            lastTry = DateTime.UtcNow;
            Raise();
            string sent = etag;
            Release known = answer;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int started = Environment.TickCount;
                Release r = null;
                string error = null, tag = sent;
                try { r = Fetch(ref tag, known); }
                catch (Exception ex)
                {
                    error = Why(ex, false);
                    ShotStack.Log("Actualizaciones: " + ex.Message);
                }
                // "Buscar ahora" shows its searching state long enough to be read, not as a flicker.
                int left = manual ? 700 - unchecked(Environment.TickCount - started) : 0;
                if (left > 0) System.Threading.Thread.Sleep(left);
                owner.Ui(delegate { Checked(r, tag, error, manual); });
            });
        }

        // A check's answer, on the UI thread.
        internal static void Checked(Release r, string tag, string error, bool manual)
        {
            Doing = Step.None;
            if (error != null)
            {
                failures++;
                if (Available == null) { Problem = error; ProblemInstalling = false; }
            }
            else
            {
                failures = 0;
                etag = tag;
                answer = r;
                S.LastUpdateCheck = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                S.Save();
                Available = r != null && r.Version > Installer.MyVersion ? r : null;
                if (ready != null && (Available == null || ready.Tag != Available.Tag)) Discard();
                // An install problem stays next to its version; a newer one starts clean.
                if (Available == null || !ProblemInstalling || problemTag != Available.Tag) { Problem = null; ProblemInstalling = false; }
                if (Available != null && failedBefore != null && SameVersion(Available.Version, failedBefore))
                {
                    failedBefore = null;
                    autoFailed = problemTag = Available.Tag;
                    Problem = "La instalaci\u00F3n de la " + Available.Version.ToString(3) + " no se complet\u00F3. Vuelve a intentarlo o desc\u00E1rgala desde GitHub.";
                    ProblemInstalling = true;
                }
                // Found from Acerca de: it's already on screen, no need to announce it.
                if (manual && Available != null) Announced(Available);
            }
            if (timer != null && S.CheckUpdates) Schedule(NextDelay(failures));
            Raise();
            if (installAfterCheck)
            {
                installAfterCheck = false;
                if (Available != null) Install();
            }
            Act();
        }

        // What to do about the available version: with automatic updates, get it ready and install it at a quiet
        // moment; otherwise announce it, once, when someone is at the computer.
        static void Act()
        {
            Release r = Available;
            bool waiting = false;
            if (r != null && !Busy)
            {
                if (S.AutoUpdate && CanInstall && autoFailed != r.Tag)
                {
                    // Downloads only after a good answer (offline, a failed check would just fail it again).
                    if (!IsReady) { if (failures == 0) Prepare(r, false); }
                    else if (Quiet()) Launch(false);
                    else
                    {
                        waiting = true;
                        // No quiet moment for a long while (it's always in use): offer it, once.
                        if (DateTime.UtcNow - readyAt > ReadyNag && ShouldAnnounce(r, S.UpdateNotified) && CanPresent()) { Announced(r); Present(r); }
                    }
                }
                else if (ShouldAnnounce(r, S.UpdateNotified))
                {
                    if (CanPresent()) { Announced(r); Present(r); }
                    else waiting = true;
                }
            }
            if (nudge != null) nudge.Enabled = waiting;
        }

        static void Announced(Release r)
        {
            if (S.UpdateNotified == r.Tag) return;
            S.UpdateNotified = r.Tag;
            S.Save();
        }

        static void ShowNotice(Release r)
        {
            if (notice != null && !notice.IsDisposed) notice.Close();
            UpdateNotice n = new UpdateNotice(owner, r);
            n.FormClosed += delegate { if (notice == n) notice = null; };
            notice = n;
            n.Present();
        }

        static void Raise()
        {
            Action h = Changed;
            if (h != null) h();
        }

        static void RaiseProgress()
        {
            Action h = Progress;
            if (h != null) h();
        }

        static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Stackshot/" + Installer.MyVersion.ToString(3);
            req.Timeout = 30000;
            req.ReadWriteTimeout = 60000;
            if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials;
            return req;
        }

        // The latest release. tag: the ETag sent (the cached answer still holds if GitHub says "not modified"), then
        // the one received.
        static Release Fetch(ref string tag, Release known)
        {
            HttpWebRequest req = Request(Api);
            req.Accept = "application/vnd.github+json";
            if (tag != null && known != null) req.Headers[HttpRequestHeader.IfNoneMatch] = tag;
            string json;
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.NotModified) return known;
                    using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = rd.ReadToEnd();
                    tag = resp.Headers[HttpResponseHeader.ETag];
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotModified) { resp.Close(); return known; }
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) { resp.Close(); tag = null; return null; } // nothing published yet
                throw;
            }
            return Parse(json);
        }

        static Release Parse(string json)
        {
            Dictionary<string, object> root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            Release r = new Release();
            r.Tag = Str(root, "tag_name");
            Version v;
            if (r.Tag == null || !Version.TryParse(r.Tag.TrimStart('v', 'V'), out v)) return null;
            r.Version = v.Build < 0 ? new Version(v.Major, v.Minor, 0) : v; // "v2.0" -> 2.0.0, so ToString(3) never throws
            r.Page = Str(root, "html_url");
            if (r.Page == null || !r.Page.StartsWith("https://github.com/rubenitx/stackshot/", StringComparison.Ordinal)) r.Page = Program.RepoUrl + "/releases";
            object assets;
            if (root.TryGetValue("assets", out assets) && assets is IEnumerable)
            {
                foreach (object o in (IEnumerable)assets)
                {
                    Dictionary<string, object> a = o as Dictionary<string, object>;
                    if (a == null) continue;
                    string name = Str(a, "name"), url = Str(a, "browser_download_url"), digest = Str(a, "digest");
                    if (url == null || !url.StartsWith(Downloads, StringComparison.Ordinal)) continue;
                    if (name == "Stackshot.exe")
                    {
                        r.ExeUrl = url;
                        if (digest != null && digest.StartsWith("sha256:", StringComparison.Ordinal)) r.Sha256 = digest.Substring(7).ToLowerInvariant();
                    }
                    else if (name == "Stackshot.exe.sig") r.SigUrl = url;
                }
            }
            // Release notes: the changelog bullets, without Markdown emphasis.
            string body = Str(root, "body") ?? "";
            foreach (string raw in body.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("###")) break;
                if (!line.StartsWith("- ")) continue;
                line = Regex.Replace(line.Substring(2), @"\*\*|`|<[^>]+>", "");
                int dot = line.IndexOf(". ");
                if (dot > 0 && dot < 90) line = line.Substring(0, dot + 1);
                r.Notes.Add(line);
                if (r.Notes.Count == 5) break;
            }
            return r;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        // A failure in words for Acerca de (the details go to the log).
        static string Why(Exception ex, bool download)
        {
            string what = download ? "descargar la actualizaci\u00F3n" : "consultar GitHub";
            WebException we = ex as WebException;
            if (we == null) return "No se pudo " + what + " (" + ex.Message.TrimEnd('.') + ").";
            HttpWebResponse resp = we.Response as HttpWebResponse;
            if (resp != null)
            {
                int code = 0;
                try { code = (int)resp.StatusCode; resp.Close(); } catch { }
                if (code == 403 || code == 429) return "GitHub ha limitado las consultas durante un rato; se volver\u00E1 a intentar m\u00E1s tarde.";
                if (code == 407) return "El proxy de la red no deja " + what + ".";
                if (code >= 500) return "GitHub no responde ahora mismo; se volver\u00E1 a intentar m\u00E1s tarde.";
                return "No se pudo " + what + " (error " + code + ").";
            }
            switch (we.Status)
            {
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.ProxyNameResolutionFailure:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.ConnectionClosed:
                case WebExceptionStatus.ReceiveFailure:
                case WebExceptionStatus.SendFailure:
                    return download ? "Se ha perdido la conexi\u00F3n durante la descarga." : "Sin conexi\u00F3n con GitHub. Se volver\u00E1 a intentar cuando vuelva la red.";
                case WebExceptionStatus.TrustFailure:
                case WebExceptionStatus.SecureChannelFailure:
                    return "No se pudo abrir una conexi\u00F3n segura con GitHub.";
            }
            return "No se pudo " + what + " (" + we.Message.TrimEnd('.') + ").";
        }

        // ---- Installing

        public static bool CanInstall
        {
            // Only releases that carry their signature can be installed from here; unsigned ones open the download page.
            get
            {
                return Available != null && Available.ExeUrl != null && Available.SigUrl != null && Available.Sha256 != null &&
                       !Installer.ManagedByMsi && Installer.RunningInstalled;
            }
        }

        // Downloaded and verified, waiting for a quiet moment (automatic updates).
        public static bool IsReady { get { return ready != null && readyExe != null && Available != null && ready.Tag == Available.Tag; } }

        // "Descargar e instalar": downloads and verifies (unless it already is), then hands over to the new version, which
        // closes this one and installs itself. A version that can't be installed from here opens its page instead.
        public static void Install()
        {
            Release r = Available;
            if (r == null) return;
            if (Doing == Step.Checking) { installAfterCheck = true; return; } // right after this check
            if (Busy) return;
            if (!CanInstall) { OpenPage(); return; }
            Problem = null;
            ProblemInstalling = false;
            Announced(r);
            if (IsReady) Launch(true);
            else Prepare(r, true);
        }

        static void Prepare(Release r, bool thenInstall)
        {
            Doing = Step.Downloading;
            Fraction = -1;
            Raise();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null, exe = null;
                byte[] sig = null;
                bool cut = false;           // the download itself failed (connection), not its verification
                try
                {
                    byte[] data = Download(r.ExeUrl, true);
                    sig = Convert.FromBase64String(Encoding.ASCII.GetString(Download(r.SigUrl, false)).Trim());
                    owner.Ui(delegate { if (Doing == Step.Downloading) { Doing = Step.Verifying; Raise(); } });
                    error = Verify(data, sig, r.Sha256);
                    if (error == null)
                    {
                        Directory.CreateDirectory(UpdateDir);
                        exe = Path.Combine(UpdateDir, "Stackshot-" + r.Version.ToString(3) + ".exe");
                        File.WriteAllBytes(exe, data);
                        error = Matches(exe, r);
                    }
                }
                catch (FormatException) { error = "La firma de la actualizaci\u00F3n no es v\u00E1lida; se ha descartado."; }
                catch (Exception ex)
                {
                    error = Why(ex, true);
                    cut = ex is WebException || ex is IOException;
                    ShotStack.Log("Descargar " + r.Tag + ": " + ex.Message);
                }
                owner.Ui(delegate
                {
                    Doing = Step.None;
                    if (error != null)
                    {
                        Delete(exe);
                        Failed(r, error, !thenInstall, cut);
                        return;
                    }
                    if (readyExe != exe) Delete(readyExe);
                    ready = r;
                    readyExe = exe;
                    readySig = sig;
                    readyAt = DateTime.UtcNow;
                    if (ProblemInstalling && problemTag == r.Tag) { Problem = null; ProblemInstalling = false; }
                    ShotStack.Log("Actualizaci\u00F3n " + r.Tag + " descargada y verificada (SHA-256 y firma)");
                    if (thenInstall) Launch(true);
                    else
                    {
                        Raise();
                        Act();
                    }
                });
            });
        }

        static byte[] Download(string url, bool report)
        {
            HttpWebRequest req = Request(url);
            using (WebResponse resp = req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            {
                long total = resp.ContentLength;
                if (total > MaxDownload) throw new InvalidDataException("demasiado grande");
                MemoryStream ms = new MemoryStream(total > 0 ? (int)total : 1 << 20);
                byte[] buf = new byte[81920];
                int n, shown = Environment.TickCount;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > MaxDownload) throw new InvalidDataException("demasiado grande");
                    if (report && total > 0 && unchecked(Environment.TickCount - shown) >= 60)
                    {
                        shown = Environment.TickCount;
                        double f = Math.Min(1, ms.Length / (double)total);
                        owner.Ui(delegate { Fraction = f; RaiseProgress(); });
                    }
                }
                if (report) owner.Ui(delegate { Fraction = 1; RaiseProgress(); });
                return ms.ToArray();
            }
        }

        // The signature covers the bytes, not the tag: an older signed build relabeled as new is refused.
        static string Matches(string exe, Release r)
        {
            Version got;
            string fv = FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "";
            if (!Version.TryParse(fv, out got) || got <= Installer.MyVersion || got.Major != r.Version.Major || got.Minor != r.Version.Minor ||
                Math.Max(0, got.Build) != r.Version.Build)
                return "La versi\u00F3n descargada no coincide con la publicada; se ha descartado.";
            return null;
        }

        // Verifies the file once more while holding it (nothing can swap it in between) and runs it.
        static void Launch(bool manual)
        {
            Release r = ready;
            if (r == null || readyExe == null) return;
            launchManual = manual;
            Doing = Step.Installing;
            if (Recorder.Recording || ScrollCapture.Active)
            {
                // Closing now would lose the recording: wait until it has been saved.
                if (hold == null)
                {
                    hold = new Timer();
                    hold.Interval = 1000;
                    hold.Tick += delegate
                    {
                        if (Recorder.Recording || ScrollCapture.Active) return;
                        hold.Stop();
                        if (Doing == Step.Installing) Launch(launchManual);
                    };
                }
                hold.Start();
                Raise();
                return;
            }
            if (hold != null) hold.Stop();
            Raise();
            string error = null;
            try
            {
                using (FileStream fs = new FileStream(readyExe, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] data = new byte[fs.Length];
                    int off = 0, n;
                    while (off < data.Length && (n = fs.Read(data, off, data.Length - off)) > 0) off += n;
                    error = off != data.Length ? "La descarga est\u00E1 incompleta; se ha descartado." : Verify(data, readySig, r.Sha256) ?? Matches(readyExe, r);
                    if (error == null)
                    {
                        ShotStack.Log("Actualizando a " + r.Tag + (manual ? "" : " (autom\u00E1tica)") + " (verificada: SHA-256 y firma)");
                        Process p = new Process();
                        p.StartInfo = new ProcessStartInfo(readyExe, InstallArguments(manual, Installer.StartupEnabled));
                        p.StartInfo.UseShellExecute = false;
                        // Watched before it starts, so even an installer that ends at once is noticed.
                        p.EnableRaisingEvents = true;
                        p.Exited += delegate { owner.Ui(delegate { InstallerExited(p, r); }); };
                        p.Start();
                        installer = p;
                    }
                }
            }
            catch (Exception ex) { error = "No se pudo iniciar la actualizaci\u00F3n (" + ex.Message.TrimEnd('.') + ")."; }
            if (error != null) Failed(r, error, !manual, false);
        }

        // A successful installer closes this instance before it ends; still here afterwards means it didn't work.
        static void InstallerExited(Process p, Release r)
        {
            int code = -1;
            try { code = p.ExitCode; p.Dispose(); } catch { }
            if (installer == p) installer = null;
            if (Doing != Step.Installing) return;
            Failed(r, "La instalaci\u00F3n no se ha completado" + (code != 0 ? " (c\u00F3digo " + code + ")" : "") + ". Vuelve a intentarlo o desc\u00E1rgala desde GitHub.",
                   !launchManual, false);
        }

        // cut: the download was cut short (an automatic update tries again at a later check, a few times).
        static void Failed(Release r, string error, bool auto, bool cut)
        {
            Doing = Step.None;
            if (hold != null) hold.Stop();
            ShotStack.Log("Actualizar a " + r.Tag + ": " + error);
            Discard();
            Problem = error;
            ProblemInstalling = true;
            problemTag = r.Tag;
            bool retry = false;
            if (auto)
            {
                if (autoTag != r.Tag) { autoTag = r.Tag; autoCuts = 0; }
                retry = cut && ++autoCuts < AutoTries;
                if (!retry) autoFailed = r.Tag;
            }
            Raise();
            if (retry) { if (S.CheckUpdates) Schedule(Retry); }
            else if (auto) Act(); // an automatic update that failed is offered by hand instead
        }

        static void Discard()
        {
            Delete(readyExe);
            ready = null;
            readyExe = null;
            readySig = null;
        }

        static void Delete(string f)
        {
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }

        // Fails closed: returns null only when both the digest and the signature match.
        public static string Verify(byte[] data, byte[] sig, string sha256)
        {
            string actual;
            using (SHA256 sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            if (sha256 == null || actual != sha256) return "La descarga no coincide con la suma SHA-256 publicada; se ha descartado.";
            using (RSACng rsa = new RSACng())
            {
                rsa.FromXmlString(PublicKey);
                if (sig == null || !rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                    return "La firma de la actualizaci\u00F3n no es v\u00E1lida; se ha descartado.";
            }
            return null;
        }

        public static void OpenPage()
        {
            try { Process.Start(Available != null ? Available.Page : Program.RepoUrl + "/releases"); } catch { }
        }

        // ---- Moments

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public int cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        static int IdleMs()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            return unchecked(Environment.TickCount - (int)li.dwTime);
        }

        // 1 away or locked, 2 an app is full screen, 3 a full-screen game, 4 presenting, 7 a full-screen Store app; 0 if unknown.
        static int Presence()
        {
            int q;
            try { return SHQueryUserNotificationState(out q) == 0 ? q : 0; }
            catch { return 0; }
        }

        // A notice appears only with someone at the computer, never over a full-screen app, a recording or an area
        // being picked.
        static bool PresentNow()
        {
            if (Recorder.Recording || ScrollCapture.Active || IdleMs() > AwayMs) return false;
            int q = Presence();
            if ((q >= 1 && q <= 4) || q == 7) return false;
            foreach (Form f in Application.OpenForms) if (f is RegionPicker && f.Visible) return false;
            return true;
        }

        // Stackshot may close and reopen on its own: nobody at the computer, no full-screen app, and nothing of Stackshot
        // in use (captures in the stack, editors and pinned images would be lost).
        static bool QuietNow()
        {
            if (Recorder.Recording || ScrollCapture.Active || IdleMs() < QuietMs) return false;
            int q = Presence();
            if ((q >= 2 && q <= 4) || q == 7) return false;
            foreach (Form f in Application.OpenForms)
            {
                if (f is Card || f is Editor || f is PinWindow) return false;
                if (f.Visible && !(f is PetWindow) && !(f is UpdateNotice) && !(f is Chip)) return false;
            }
            return true;
        }
    }
}
