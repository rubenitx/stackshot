// Stackshot - A SOAP message at a glance: whether the document is a SOAP envelope, the operation it carries (the first
// element inside Body) and whether that is a fault. Read from the parsed tree, nothing else.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;

namespace Stackshot
{
    public sealed class SoapInfo
    {
        public string Version = "";     // "1.1" or "1.2"
        public string Operation = "";   // local name of the first element inside Body ("" when Body is empty or missing)
        public string Qualified = "";   // that element's name as written, prefix included
        public string NsUri = "";       // and its namespace
        public bool FaultBody;          // the Body holds a SOAP Fault
    }

    public static class XmlSoap
    {
        public const string Ns11 = "http://schemas.xmlsoap.org/soap/envelope/";
        public const string Ns12 = "http://www.w3.org/2003/05/soap-envelope";

        // Null unless the root element is an Envelope in the SOAP 1.1 or 1.2 namespace.
        public static SoapInfo Read(XmlModel m)
        {
            if (m == null) return null;
            XItem root = null;
            foreach (XItem t in m.Top) if (t.Kind == XKind.Element) { root = t; break; }
            if (root == null || root.Local != "Envelope" || (root.NsUri != Ns11 && root.NsUri != Ns12)) return null;
            SoapInfo s = new SoapInfo { Version = root.NsUri == Ns11 ? "1.1" : "1.2" };
            foreach (XItem b in root.Kids)
            {
                if (b.Kind != XKind.Element || b.Local != "Body" || b.NsUri != root.NsUri) continue;
                foreach (XItem op in b.Kids)
                {
                    if (op.Kind != XKind.Element) continue;
                    s.Operation = op.Local;
                    s.Qualified = op.Name;
                    s.NsUri = op.NsUri;
                    s.FaultBody = op.Local == "Fault" && op.NsUri == root.NsUri;
                    break;
                }
                break;
            }
            return s;
        }

        // When a file was written, as the bar shows it: "12/10 10:31:22" (local time).
        public static string When(DateTime utc)
        {
            return utc.ToLocalTime().ToString(I18n.IsSpanish ? "dd/MM HH:mm:ss" : "MMM d HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
