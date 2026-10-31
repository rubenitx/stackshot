// Stackshot - The one way the app talks to the Internet (updates, reports, the watch connection).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Net;

namespace Stackshot
{
    public static class Net
    {
        // An HTTPS request with Stackshot's name and version, the system proxy with the user's Windows credentials
        // (corporate proxies), and time limits. An address that isn't HTTPS is refused, so nothing the app sends starts
        // out in the clear.
        public static HttpWebRequest Create(string url, int timeoutMs, int readWriteMs)
        {
            Uri uri = new Uri(url);
            if (uri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException(I18n.T("Solo se permiten conexiones HTTPS: ", "Only HTTPS connections are allowed: ") + uri.GetLeftPart(UriPartial.Authority));
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.UserAgent = "Stackshot/" + Installer.MyVersion.ToString(3);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = readWriteMs;
            // JSON answers come compressed (smaller, faster); a POST goes in one round trip instead of asking first
            // (some company proxies refuse that question with 417).
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.ServicePoint.Expect100Continue = false;
            if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials;
            return req;
        }
    }
}
