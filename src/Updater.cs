// Stackshot - In-app updates from GitHub Releases, verified before anything runs.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Stackshot
{
    // Checks the latest release at most once a day (never in --test). An update is installed only if:
    //   - it comes from this repository's release downloads (fixed URL prefix, HTTPS),
    //   - its SHA-256 matches the digest GitHub reports for the asset, and
    //   - its RSA-PSS signature verifies with the public key below. The private key exists only as a secret of the
    //     release workflow, so a compromised download host or a re-uploaded asset can't push code to users.
    // Anything missing or wrong fails closed. MSI-managed installs only get a notice: IT deploys the new MSI.
    public static class Updater
    {
        const string Api = "https://api.github.com/repos/rubenitx/stackshot/releases/latest";
        const string Downloads = "https://github.com/rubenitx/stackshot/releases/download/";
        const string PublicKey = "<RSAKeyValue><Modulus>+WMiH/o5pUTcAyQmGtGFWfdjYmxL09Qte3q7eMMPBX4s7rxMNOSaqeL69kB3wVTZtduGIDzW2Mtmp+rYUl7zzXuN4/hUW5WEBtpOxpwe5bnOqN+43DILNNugqbsEYym9TLnwcsq2KtTNOXKY5ho8wZNw82yCvdLUGtbi8Rh0h/UMhOG9o2quLu+3RCFkkqjnFkzeaMkwhYfIeFKtUHVci1MEfyZG6zVUXYbka0gWCmGF5+uZ2lhksnewvviFhf/B2NY6ziLDY1yFOdBn0wxOL23G4mVmLdvuGGBhmfFyTPTPtCyEZiQchObTOOYH6JScAzx9LuuB8k5XCmX5Wpakmg/8KBdbc330F/D+O5uzPKt3r4E3p7LMSRw+JKNXYNZdwPzcoSZFP0LS59uD2h8O4mTdOO8QG3ugeeQR7LEfQCk9z4XZSZ9pobmCyDE5Yjg228uVBUf6N2JAiXcMZGv4PzlR7rA1F4PrVJ3pJoGd3k2q1HqdR8iA+++MvPifg03d</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public class Release
        {
            public Version Version;
            public string Tag, Page, ExeUrl, SigUrl, Sha256;
            public List<string> Notes = new List<string>();
        }

        public static Release Available;        // newer than this build, or null
        public static string LastError;
        public static bool Busy;
        public static event Action Changed;

        static ShotStack owner;
        static Timer timer;
        static string notified;

        static string UpdateDir { get { return Path.Combine(Settings.DataDir, "update"); } }

        public static void Start(ShotStack o)
        {
            owner = o;
            if (ShotStack.Test) return;
            try { if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true); } catch { }
            timer = new Timer();
            timer.Interval = 20000;
            timer.Tick += delegate { timer.Interval = 60 * 60 * 1000; CheckIfDue(); };
            timer.Start();
        }

        public static void Stop()
        {
            if (timer != null) { timer.Dispose(); timer = null; }
        }

        static void CheckIfDue()
        {
            Settings s = owner.Settings;
            if (!s.CheckUpdates) return;
            DateTime last;
            if (DateTime.TryParse(s.LastUpdateCheck, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out last) &&
                (DateTime.UtcNow - last).TotalHours < 20) return;
            Check(null);
        }

        // Asks GitHub for the latest release on a worker thread; done (UI thread) gets an error message or null.
        public static void Check(Action<string> done)
        {
            if (Busy) return;
            Busy = true;
            Raise();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Release r = null;
                string error = null;
                try { r = Fetch(); }
                catch (Exception ex) { error = "No se pudo consultar GitHub: " + ex.Message; }
                owner.Ui(delegate
                {
                    Busy = false;
                    LastError = error;
                    if (error == null)
                    {
                        owner.Settings.LastUpdateCheck = DateTime.UtcNow.ToString("o");
                        owner.Settings.Save();
                        Available = r != null && r.Version > Installer.MyVersion ? r : null;
                        if (Available != null && notified != Available.Tag)
                        {
                            notified = Available.Tag;
                            owner.NotifyUpdate(Available);
                        }
                    }
                    else ShotStack.Log("Actualizaciones: " + error);
                    Raise();
                    if (done != null) done(error);
                });
            });
        }

        static void Raise()
        {
            if (Changed != null) Changed();
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            WebClient web = new WebClient();
            web.Headers[HttpRequestHeader.UserAgent] = "Stackshot/" + Installer.MyVersion.ToString(3);
            if (web.Proxy != null) web.Proxy.Credentials = CredentialCache.DefaultCredentials;
            return web;
        }

        static Release Fetch()
        {
            string json;
            using (WebClient web = Client())
            {
                web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                web.Encoding = Encoding.UTF8;
                json = web.DownloadString(Api);
            }
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

        public static bool CanInstall
        {
            // Only releases that carry their signature can be installed from here; unsigned ones open the download page.
            get
            {
                return Available != null && Available.ExeUrl != null && Available.SigUrl != null && Available.Sha256 != null &&
                       !Installer.ManagedByMsi && Installer.RunningInstalled;
            }
        }

        // Downloads, verifies and hands over to the new version (it closes this one and installs itself).
        public static void Install(Action<string> status, Action<string> failed)
        {
            Release r = Available;
            if (r == null || Busy) return;
            if (!CanInstall) { OpenPage(); return; }
            if (r.ExeUrl == null || r.SigUrl == null || r.Sha256 == null)
            {
                failed("Esta versi\u00F3n no trae firma de actualizaci\u00F3n. Desc\u00E1rgala desde GitHub.");
                return;
            }
            Busy = true;
            Raise();
            status("Descargando " + r.Tag + "\u2026");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null, exe = null;
                try
                {
                    Directory.CreateDirectory(UpdateDir);
                    exe = Path.Combine(UpdateDir, "Stackshot-" + r.Version.ToString(3) + ".exe");
                    byte[] data, sig;
                    using (WebClient web = Client())
                    {
                        data = web.DownloadData(r.ExeUrl);
                        sig = Convert.FromBase64String(Encoding.ASCII.GetString(web.DownloadData(r.SigUrl)).Trim());
                    }
                    error = Verify(data, sig, r.Sha256);
                    if (error == null)
                    {
                        File.WriteAllBytes(exe, data);
                        // The signature covers the bytes, not the tag: an older signed build relabeled as new is refused.
                        Version got;
                        string fv = FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "";
                        if (!Version.TryParse(fv, out got) || got <= Installer.MyVersion || got.ToString(3) != r.Version.ToString(3))
                            error = "La versi\u00F3n descargada no coincide con la publicada; se ha descartado.";
                    }
                }
                catch (Exception ex) { error = "No se pudo descargar: " + ex.Message; }
                owner.Ui(delegate
                {
                    Busy = false;
                    Raise();
                    if (error != null)
                    {
                        ShotStack.Log("Actualizar a " + r.Tag + ": " + error);
                        try { if (exe != null && File.Exists(exe)) File.Delete(exe); } catch { }
                        failed(error);
                        return;
                    }
                    ShotStack.Log("Actualizando a " + r.Tag + " (verificada: SHA-256 y firma)");
                    status("Instalando " + r.Tag + "\u2026");
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(exe, "--install" + (Installer.StartupEnabled ? " --startup" : ""));
                        psi.UseShellExecute = false;
                        Process.Start(psi).Dispose();
                    }
                    catch (Exception ex) { failed("No se pudo iniciar la actualizaci\u00F3n: " + ex.Message); }
                });
            });
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
    }
}
