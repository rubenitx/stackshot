// Round 6 (XML compare, SOAP header, JSON and base64), suite "w6xml": the SOAP bar over a message (operation, time, size,
// OK or fault, the files around it), Copiar como JSON, base64 attachments (detection, types, what is never opened, the
// size cap, opening and saving with the programs replaced by the test) and the comparison of two XML (the algorithm, the
// view in a parked window, its speed). Synthetic data only, in throwaway folders.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Controls;

namespace Stackshot.Tests
{
    static partial class DocTests
    {
        const string X6Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";

        static void W6XmlSuite()
        {
            Ds.Apply(1);
            W6Soap();
            W6Json();
            W6Base64();
            W6Diff();
            W6CompareView();
            R5Xml();
        }

        // ---- helpers

        static DiffResult X6DiffOf(string older, string newer)
        {
            XmlModel a = Model(older), b = Model(newer);
            XmlDoc.ExpandInner(a);
            XmlDoc.ExpandInner(b);
            DiffResult r = XmlDiff.Compare(a, b, null);
            Check(r != null, "no result");
            return r;
        }

        static string X6Counts(DiffResult r) { return r.Changed + " changed, " + r.Added + " added, " + r.Removed + " removed"; }

        // The stops as "+name", "-name", "~name[old>new]" (text) or "~name{attr:old>new}" (attributes).
        static string X6Stops(DiffResult r)
        {
            List<string> o = new List<string>();
            foreach (DiffNode d in r.Stops)
            {
                XItem x = d.Item;
                string n = x.Kind == XKind.Element ? x.Local : x.Kind == XKind.Comment ? "#comment" : "#text";
                string s = (d.Kind == DiffKind.Added ? "+" : d.Kind == DiffKind.Removed ? "-" : "~") + n;
                if (d.TextChanged) s += "[" + (d.TextOld ?? "null") + ">" + (d.TextNew ?? "null") + "]";
                if (d.Kind == DiffKind.Changed && d.Attrs != null)
                    foreach (AttrDiff a in d.Attrs)
                        if (a.Kind != DiffKind.Same) s += "{" + a.Name + ":" + (a.Old ?? "null") + ">" + (a.New ?? "null") + "}";
                o.Add(s);
            }
            return string.Join(" ", o.ToArray());
        }

        static string X6B64(byte[] b, bool lines)
        {
            string s = Convert.ToBase64String(b);
            if (!lines) return s;
            StringBuilder sb = new StringBuilder("\n");
            for (int i = 0; i < s.Length; i += 76) sb.Append("        ").Append(s.Substring(i, Math.Min(76, s.Length - i))).Append('\n');
            return sb.ToString();
        }

        static byte[] X6Picture(System.Drawing.Imaging.ImageFormat f)
        {
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(48, 32))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.FillRectangle(System.Drawing.Brushes.SteelBlue, 4, 4, 30, 20);
                    g.FillEllipse(System.Drawing.Brushes.OrangeRed, 20, 10, 20, 16);
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, f);
                    return ms.ToArray();
                }
            }
        }

        static byte[] X6ZipOf(params string[] entries)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive z = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    foreach (string e in entries)
                        using (StreamWriter w = new StreamWriter(z.CreateEntry(e).Open())) w.Write("<x/>");
                return ms.ToArray();
            }
        }

        static byte[] X6Pdf()
        {
            return Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n" + new string('x', 200));
        }

        static bool X6SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void X6NeedTestData()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA"))) Skip("STACKSHOT_TEST_DATA is not set: the attachment folder would be the real one");
        }

        // ---- SOAP bar

        static string X6SoapFault(string code, string text)
        {
            return "<?xml version=\"1.0\"?>\n<S:Envelope xmlns:S=\"" + X6Soap11 + "\"><S:Body><S:Fault><faultcode>" + code + "</faultcode><faultstring>" + text + "</faultstring></S:Fault></S:Body></S:Envelope>\n";
        }

        static void W6Soap()
        {
            Test("w6xml", "soap-info", delegate
            {
                SoapInfo s = XmlSoap.Read(Model(Soap("<return>uno</return>")));
                Check(s != null, "SOAP 1.1 envelope not seen");
                SameV("1.1", s.Version, "version");
                SameV("consultaPruebaResponse", s.Operation, "operation");
                SameV("ns2:consultaPruebaResponse", s.Qualified, "qualified");
                SameV("urn:prueba", s.NsUri, "namespace");
                Check(!s.FaultBody, "fault in a normal answer");
                SoapInfo f = XmlSoap.Read(Model("<env:Envelope xmlns:env=\"http://www.w3.org/2003/05/soap-envelope\"><env:Header><h:t xmlns:h=\"urn:h\"/></env:Header><!-- c --><env:Body><env:Fault><env:Code><env:Value>env:Sender</env:Value></env:Code></env:Fault></env:Body></env:Envelope>"));
                Check(f != null && f.Version == "1.2" && f.FaultBody && f.Operation == "Fault", "SOAP 1.2 fault: " + (f == null ? "null" : f.Operation));
                SoapInfo empty = XmlSoap.Read(Model("<S:Envelope xmlns:S=\"" + X6Soap11 + "\"><S:Body/></S:Envelope>"));
                Check(empty != null && empty.Operation == "" && !empty.FaultBody, "empty Body");
                Check(XmlSoap.Read(Model("<Envelope><Body><op/></Body></Envelope>")) == null, "an Envelope without the SOAP namespace counts");
                Check(XmlSoap.Read(Model("<e:Envelope xmlns:e=\"urn:otro\"><e:Body><op/></e:Body></e:Envelope>")) == null, "another namespace counts");
                Check(XmlSoap.Read(Model("<pedido><linea/></pedido>")) == null, "a plain document counts");
                // A Body of another namespace is not the envelope's.
                SoapInfo odd = XmlSoap.Read(Model("<S:Envelope xmlns:S=\"" + X6Soap11 + "\"><x:Body xmlns:x=\"urn:x\"><op/></x:Body></S:Envelope>"));
                Check(odd != null && odd.Operation == "", "foreign Body read as the envelope's");
                SameV("12/10 09:05:07", XmlSoap.When(new DateTime(2026, 10, 12, 9, 5, 7, DateTimeKind.Local).ToUniversalTime()), "time format");
            });

            Test("w6xml", "soap-bar-in-window", delegate
            {
                string dir = WorkDir("soapbar");
                DateTime t0 = new DateTime(2026, 10, 12, 9, 5, 7);
                string a = Put(dir, "tmp1.xml", Soap("<return>uno</return>"));
                string b = Put(dir, "tmp2.xml", X6SoapFault("S:Client", "Falta el DNI"));
                string c = Put(dir, "tmp3.xml", "<respuesta><Fault><faultcode>X1</faultcode><faultstring>Banner de siempre</faultstring></Fault></respuesta>");
                File.SetLastWriteTime(a, t0);
                File.SetLastWriteTime(b, t0.AddMinutes(1));
                File.SetLastWriteTime(c, t0.AddMinutes(2));
                MarkdownWindow w = NewWindow();
                try
                {
                    Func<Border> banner = delegate { return (Border)typeof(MarkdownWindow).GetField("bannerHost", NP).GetValue(w); };
                    Open(w, a);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Rows.Count > 0 && w.HeaderHost != null && w.HeaderHost.Child != null; }, 8000), "no SOAP bar");
                    string bar = Flat(w.HeaderHost.Child);
                    Check(bar.Contains("consultaPruebaResponse"), "operation missing: " + bar);
                    Check(bar.Contains("12/10 09:05:07"), "time missing: " + bar);
                    Check(bar.Contains(Recents.Size(new FileInfo(a).Length)), "size missing: " + bar);
                    Check(bar.Contains("OK"), "OK chip missing: " + bar);
                    Check(banner().Child == null, "a banner on a normal answer");
                    Check(Until(delegate { return w.NeighboursKnown; }, 5000), "neighbours never known");
                    DocIconButton prev = Field<DocIconButton>(w, "soapPrev"), next = Field<DocIconButton>(w, "soapNext");
                    Check(!prev.IsEnabled && next.IsEnabled, "buttons on the first file: prev " + prev.IsEnabled + ", next " + next.IsEnabled);
                    Check(((string)prev.ToolTip).Contains("[") && ((string)next.ToolTip).Contains("]"), "tooltips do not mention [ and ]");
                    X6BitmapSourceCheck(w, "soap-ok-light");
                    // The next button is the same as ] .
                    Call(next, "Clicked", new System.Windows.Point(5, 5));
                    Check(Until(delegate { return w.Doc != null && w.Doc.Path == b && w.Tree != null && w.Tree.Rows.Count > 0 && w.HeaderHost != null && w.HeaderHost.Child != null && Flat(w.HeaderHost.Child).Contains("Falta"); }, 8000), "the next button did not open the fault");
                    bar = Flat(w.HeaderHost.Child);
                    Check(bar.Contains("SOAP Fault") && bar.Contains("S:Client") && bar.Contains("Falta el DNI"), "fault in the bar: " + bar);
                    Check(banner().Child == null, "the fault shows twice (bar and banner)");
                    Check(Until(delegate { return w.NeighboursKnown && Field<DocIconButton>(w, "soapPrev").IsEnabled && Field<DocIconButton>(w, "soapNext").IsEnabled; }, 5000), "both buttons on the middle file");
                    Pump(400);
                    X6BitmapSourceCheck(w, "soap-fault-light");
                    Ds.Apply(2);
                    Pump(300);
                    Check(Flat(w.HeaderHost.Child).Contains("Falta el DNI"), "fault bar after the theme");
                    X6BitmapSourceCheck(w, "soap-fault-dark");
                    Ds.Apply(1);
                    Pump(300);
                    Call(Field<DocIconButton>(w, "soapNext"), "Clicked", new System.Windows.Point(5, 5));
                    Check(Until(delegate { return w.Doc != null && w.Doc.Path == c && w.Tree != null && w.Tree.Rows.Count > 0; }, 8000), "third file");
                    Pump(150);
                    Check(w.HeaderHost == null || w.HeaderHost.Child == null, "a SOAP bar on a plain document");
                    Check(banner().Child != null && Flat(banner().Child).Contains("Banner de siempre"), "a fault outside SOAP lost its banner");
                    // Pasted text: no file, so no time and no neighbours.
                    Shut(w);
                    w = NewWindow();
                    Call(w, "LoadText", Soap("<return>pegado</return>"), null, null);
                    Check(Until(delegate { return w.HeaderHost != null && w.HeaderHost.Child != null; }, 8000), "no bar for pasted SOAP");
                    bar = Flat(w.HeaderHost.Child);
                    Check(!bar.Contains("12/10") && bar.Contains("KB"), "pasted text: no time, a size: " + bar);
                    Check(!Field<DocIconButton>(w, "soapPrev").IsEnabled && !Field<DocIconButton>(w, "soapNext").IsEnabled, "buttons enabled for pasted text");
                }
                finally { Ds.Apply(1); Shut(w); DeleteDir(dir); }
            });
        }

        // ---- JSON

        static string X6Json(string xml, string local)
        {
            XmlModel m = Model(xml);
            XmlDoc.ExpandInner(m);
            XItem n = local == null ? m.Top.Find(delegate(XItem x) { return x.Kind == XKind.Element; }) : FirstNamed(m.Top, local);
            string err;
            string j = XmlJson.Convert(n, XmlTreeView.MaxCopy, out err);
            Check(j != null, "no JSON: " + err);
            return j;
        }

        static void X6ValidJson(string j)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            js.DeserializeObject(j);
        }

        static void W6Json()
        {
            Test("w6xml", "json-attributes-arrays-strings", delegate
            {
                string j = X6Json("<r a=\"1\" b=\"x&quot;y\"><i>1</i><i>2</i><e/><t>true</t><n> 3.5 </n><i>3</i></r>", null);
                Same("{\n  \"r\": {\n    \"@a\": \"1\",\n    \"@b\": \"x\\\"y\",\n    \"i\": [\n      \"1\",\n      \"2\",\n      \"3\"\n    ],\n    \"e\": null,\n    \"t\": \"true\",\n    \"n\": \"3.5\"\n  }\n}", j, "json");
                X6ValidJson(j);
            });
            Test("w6xml", "json-mixed-text-and-attributes", delegate
            {
                Same("{\n  \"p\": {\n    \"#text\": [\n      \"Hola\",\n      \"!\"\n    ],\n    \"b\": \"mundo\"\n  }\n}", X6Json("<p>Hola <b>mundo</b>!</p>", null), "mixed");
                Same("{\n  \"v\": {\n    \"@unidad\": \"kg\",\n    \"#text\": \"12.5\"\n  }\n}", X6Json("<v unidad=\"kg\">12.5</v>", null), "attribute and text");
                Same("{\n  \"a\": {\n    \"@x\": \"1\"\n  }\n}", X6Json("<a x=\"1\"/>", null), "attributes only");
                Same("{\n  \"c\": \"dato\"\n}", X6Json("<c><![CDATA[dato]]></c>", null), "CDATA is text");
                // Arrays of objects.
                string j = X6Json("<l><f id=\"1\"><v>a</v></f><f id=\"2\"/></l>", null);
                Same("{\n  \"l\": {\n    \"f\": [\n      {\n        \"@id\": \"1\",\n        \"v\": \"a\"\n      },\n      {\n        \"@id\": \"2\"\n      }\n    ]\n  }\n}", j, "array of objects");
                X6ValidJson(j);
            });
            Test("w6xml", "json-namespaces-and-inner-xml", delegate
            {
                string j = X6Json("<S:Envelope xmlns:S=\"" + X6Soap11 + "\"><S:Body><ns2:op xmlns:ns2=\"urn:x\"><ns2:a>1</ns2:a></ns2:op></S:Body></S:Envelope>", null);
                Same("{\n  \"S:Envelope\": {\n    \"@xmlns:S\": \"" + X6Soap11 + "\",\n    \"S:Body\": {\n      \"ns2:op\": {\n        \"@xmlns:ns2\": \"urn:x\",\n        \"ns2:a\": \"1\"\n      }\n    }\n  }\n}", j, "prefixes kept");
                string inner = X6Json(Soap("<return>" + SecurityElement.Escape(InnerDoc) + "</return>"), "return");
                Same("{\n  \"return\": {\n    \"Respuesta\": {\n      \"Codigo\": \"0\",\n      \"Persona\": {\n        \"@nombre\": \"Ana\",\n        \"Nota\": {\n          \"b\": \"negrita\"\n        }\n      }\n    }\n  }\n}", inner, "XML inside a field as objects");
                X6ValidJson(inner);
            });
            Test("w6xml", "json-escaping-text-node-and-limits", delegate
            {
                string j = X6Json("<m>Comillas \" barra \\ tab&#9;fin\nl\u00EDnea \u00F1 \u2028</m>", null);
                Same("{\n  \"m\": \"Comillas \\\" barra \\\\ tab\\tfin\\nl\u00EDnea \u00F1 \\u2028\"\n}", j, "escaping");
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> back = (Dictionary<string, object>)js.DeserializeObject(j);
                Same("Comillas \" barra \\ tab\tfin\nl\u00EDnea \u00F1 \u2028", (string)back["m"], "round trip");
                XmlModel m = Model("<a>  texto  <!-- nota --><?pi x?></a>");
                string err;
                XItem text = m.Top[0].Kids[0];
                Same("\"texto\"", XmlJson.Convert(text, 1000, out err), "a text node is a string");
                Check(XmlJson.Convert(m.Top[0].Kids[1], 1000, out err) == null && err != null, "a comment has no JSON");
                Check(XmlJson.Convert(m.Top[0], 5, out err) == null && err.Contains("grande"), "cap ignored");
                // Any depth: no recursion (20,000 levels of indentation pass the cap and say so; 3,000 fit).
                Func<int, XmlModel> deepDoc = delegate(int levels)
                {
                    StringBuilder deep = new StringBuilder();
                    for (int i = 0; i < levels; i++) deep.Append("<d>");
                    deep.Append("fondo");
                    for (int i = 0; i < levels; i++) deep.Append("</d>");
                    return Model(deep.ToString());
                };
                string dj = XmlJson.Convert(deepDoc(3000).Top[0], XmlTreeView.MaxCopy, out err);
                Check(dj != null && dj.Contains("\"fondo\"") && dj.EndsWith("}"), "deep document: " + err);
                Check(XmlJson.Convert(deepDoc(20000).Top[0], XmlTreeView.MaxCopy, out err) == null && err.Contains("grande"), "very deep document: " + err);
            });
            Test("w6xml", "json-copy-from-the-tree", delegate
            {
                XmlModel m = Model("<r><a x=\"1\">uno</a></r>");
                XmlTreeView tree = new XmlTreeView(Ds.Brushes);
                System.Windows.Window host = new System.Windows.Window { Width = 600, Height = 400, WindowStyle = System.Windows.WindowStyle.None, Content = tree };
                Park(host);
                try
                {
                    tree.Load(m);
                    string got = null, said = null, notice = null;
                    tree.CopyRequest += delegate(string t, string what) { got = t; said = what; };
                    tree.Notice += delegate(string msg, bool bad) { notice = msg; };
                    tree.CopyJson(m.Top[0]);
                    Check(Until(delegate { return got != null; }, 5000), "no copy");
                    Same("{\n  \"r\": {\n    \"a\": {\n      \"@x\": \"1\",\n      \"#text\": \"uno\"\n    }\n  }\n}", got, "copied JSON");
                    SameV("JSON copiado.", said, "message");
                    tree.CopyJson(m.Top[0].Kids[0].Kids[0]);
                    Check(Until(delegate { return got == "\"uno\""; }, 5000), "text node copy: " + got);
                    // The menu: Copiar valor, Copiar como JSON (no attachment entries for short text).
                    List<MenuEntry> menu = new List<MenuEntry>();
                    XRow row = tree.Rows.Find(delegate(XRow x) { return x.Node.Name == "a" && !x.Close; });
                    Call(tree, "ValueEntries", menu, row, null);
                    List<string> labels = menu.ConvertAll(delegate(MenuEntry e) { return e.Label; });
                    Check(labels.Contains("Copiar valor") && labels.Contains("Copiar como JSON") && !labels.Exists(delegate(string s) { return s != null && s.Contains("adjunto"); }), "entries: " + string.Join(", ", labels.ToArray()));
                    Check(notice == null, "notice " + notice);
                }
                finally { host.Close(); }
            });
            Test("w6xml", "copy-value-of-the-attribute-under-the-mouse", delegate
            {
                XmlModel m = Model("<r xmlns:x=\"urn:x\"><a x:tipo=\"T\" id=\"7\" nombre=\"Ana &amp; Eva\" vacio=\"\">texto</a></r>");
                XItem a = m.Top[0].Kids[0];
                XRow row = new XRow { Node = a, Depth = 1, Look = new XmlLook(Ds.Brushes) };
                StringBuilder sb = new StringBuilder();
                foreach (Piece p in XmlRows.Pieces(row, false)) sb.Append(p.Text);
                string line = sb.ToString();
                SameV(-1, XmlTreeView.AttrAt(row, line.IndexOf("<a") + 1), "on the name");
                SameV(0, XmlTreeView.AttrAt(row, line.IndexOf("x:tipo") + 2), "prefixed attribute name");
                SameV(1, XmlTreeView.AttrAt(row, line.IndexOf("\"7\"") + 1), "value of id");
                SameV(2, XmlTreeView.AttrAt(row, line.IndexOf("Ana") + 5), "escaped value");
                SameV(3, XmlTreeView.AttrAt(row, line.IndexOf("vacio=") + 7), "empty value");
                SameV(-1, XmlTreeView.AttrAt(row, line.IndexOf("texto") + 1), "on the text");
                SameV(-1, XmlTreeView.AttrAt(new XRow { Node = a, Close = true, Look = row.Look }, 3), "closing tag");
            });
        }

        // ---- base64 attachments

        static void W6Base64()
        {
            Test("w6xml", "base64-detection", delegate
            {
                Random rnd = new Random(7);
                byte[] bin = new byte[300];
                rnd.NextBytes(bin);
                Check(XmlAttachment.IsBase64(X6B64(bin, false)), "plain base64");
                Check(XmlAttachment.IsBase64(X6B64(bin, true)), "base64 in lines with indentation");
                byte[] one = new byte[49], two = new byte[50];
                rnd.NextBytes(one); rnd.NextBytes(two);
                Check(Convert.ToBase64String(one).EndsWith("=") && XmlAttachment.IsBase64(Convert.ToBase64String(one)), "one padding character");
                Check(Convert.ToBase64String(two).EndsWith("==") || XmlAttachment.IsBase64(Convert.ToBase64String(two)), "padding");
                Check(XmlAttachment.IsBase64(X6B64(X6Pdf(), false)), "a PDF");
                string hex = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
                Check(!XmlAttachment.IsBase64(hex), "a SHA-256 in hex");
                Check(!XmlAttachment.IsBase64("SolicitudDeConsultaDeExpedientePorNumeroDeAfiliadoYFechaDeAltaXy"), "a long word");
                string b64 = Convert.ToBase64String(bin);
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 60)), "shorter than 64");
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 65)), "not a whole number of groups");
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 40) + "-" + b64.Substring(41, 59)), "a character outside the alphabet");
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 40) + "=" + b64.Substring(41, 59)), "padding in the middle");
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 98) + "==AB"), "data after the padding");
                Check(!XmlAttachment.IsBase64(b64.Substring(0, 97) + "==="), "three padding characters");
                Check(!XmlAttachment.IsBase64("Hola, esto es un texto normal con espacios y puntos que no es base64."), "a sentence");
                Check(!XmlAttachment.IsBase64(null) && !XmlAttachment.IsBase64("") && !XmlAttachment.IsBase64(new string(' ', 100)), "empty");
                // Without a digit, '+' or '/', only a long field counts (a short one may be a word).
                StringBuilder letters = new StringBuilder();
                for (int i = 0; i < 20; i++) letters.Append("QUFB");
                Check(!XmlAttachment.IsBase64(letters.ToString()), "80 letters");
                for (int i = 0; i < 50; i++) letters.Append("QUFB");
                Check(XmlAttachment.IsBase64(letters.ToString()), "280 characters of base64");
                string err;
                Check(X6SameBytes(bin, XmlAttachment.Decode(X6B64(bin, true), out err)), "decode with whitespace: " + err);
            });
            Test("w6xml", "base64-right-click-stays-instant", delegate
            {
                // A 6 MB file in a field (8 MB of base64 in lines): the menu looks at it on the UI thread.
                byte[] big = new byte[6 * 1024 * 1024];
                new Random(5).NextBytes(big);
                byte[] head = X6Pdf();
                Buffer.BlockCopy(head, 0, big, 0, head.Length);
                string field = X6B64(big, true);
                // The code compiled first (on a small field, like the first right click of a session), then timed.
                string small = X6B64(head, false);
                XmlAttachment.IsBase64(small);
                XmlAttachment.Peek(small);
                XmlAttachment.Peek(field.Substring(0, SampleSize(field)));
                Stopwatch sw = Stopwatch.StartNew();
                bool isFile = XmlAttachment.IsBase64(field);
                AttachType t = XmlAttachment.Peek(field);
                Budget("type an 8 MB base64 field", sw.Elapsed.TotalMilliseconds, 16);
                Check(isFile && t.Kind == AttachKind.Pdf, "big field: " + isFile + " " + t.Kind);
                // Judged by its ends: a bad character in the middle is caught when it is decoded.
                string broken = field.Substring(0, field.Length / 2) + "*" + field.Substring(field.Length / 2 + 1);
                string err;
                Check(XmlAttachment.IsBase64(broken) && XmlAttachment.Decode(broken, out err) == null && err.Contains("base64"), "broken big field");
                Check(!XmlAttachment.IsBase64("*" + field.Substring(1)) && !XmlAttachment.IsBase64(field.Substring(0, field.Length - 10) + "==AAAAAAAA"), "bad start or end of a big field");
                sw.Restart();
                byte[] back = XmlAttachment.Decode(field, out err);
                Add("decode " + Ms(sw.Elapsed.TotalMilliseconds) + " (background)");
                Check(X6SameBytes(big, back), "big decode: " + err);
            });
            Test("w6xml", "attachment-types", delegate
            {
                Dictionary<string, byte[]> cases = new Dictionary<string, byte[]>();
                cases["Pdf .pdf"] = X6Pdf();
                cases["Png .png"] = X6Picture(System.Drawing.Imaging.ImageFormat.Png);
                cases["Jpeg .jpg"] = X6Picture(System.Drawing.Imaging.ImageFormat.Jpeg);
                cases["Gif .gif"] = X6Picture(System.Drawing.Imaging.ImageFormat.Gif);
                cases["Bmp .bmp"] = X6Picture(System.Drawing.Imaging.ImageFormat.Bmp);
                cases["Tiff .tif"] = X6Picture(System.Drawing.Imaging.ImageFormat.Tiff);
                cases["Zip .zip"] = X6ZipOf("a.txt", "b/c.txt");
                cases["Word .docx"] = X6ZipOf("[Content_Types].xml", "word/document.xml", "word/styles.xml");
                cases["Excel .xlsx"] = X6ZipOf("[Content_Types].xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml");
                cases["PowerPoint .pptx"] = X6ZipOf("[Content_Types].xml", "ppt/presentation.xml");
                cases["OfficeMacros .docm"] = X6ZipOf("[Content_Types].xml", "word/document.xml", "word/vbaProject.bin");
                cases["Xml .xml"] = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><a>1</a>");
                cases["Xml .xml bom"] = Encoding.UTF8.GetPreamble().Length == 3 ? new byte[] { 0xEF, 0xBB, 0xBF, (byte)'<', (byte)'a', (byte)'/', (byte)'>' } : null;
                cases["Xml .xml utf16"] = Encoding.Unicode.GetPreamble().Length == 2 ? X6Concat(Encoding.Unicode.GetPreamble(), Encoding.Unicode.GetBytes("<r>\u00F1</r>")) : null;
                cases["Text .txt"] = Encoding.UTF8.GetBytes("Hola, se\u00F1or.\r\nSegunda l\u00EDnea\tcon tabulador.");
                cases["Text .txt 1252"] = Encoding.GetEncoding(1252).GetBytes("Espa\u00F1a, coraz\u00F3n");
                cases["Executable .exe"] = X6Concat(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, new byte[60]);
                cases["Shortcut .lnk"] = X6Concat(new byte[] { 0x4C, 0, 0, 0, 0x01, 0x14, 0x02, 0x00 }, new byte[60]);
                byte[] noise = new byte[512];
                new Random(3).NextBytes(noise);
                noise[0] = 0x01; noise[1] = 0x02;
                cases["Unknown .bin"] = noise;
                cases["Unknown .bin text-like BM"] = Encoding.ASCII.GetBytes("BM").Length == 2 ? X6Concat(Encoding.ASCII.GetBytes("BMW es una marca"), new byte[] { 0x00, 0x01, 0x02 }) : null;
                foreach (KeyValuePair<string, byte[]> kv in cases)
                {
                    string[] want = kv.Key.Split(' ');
                    AttachType t = XmlAttachment.Detect(kv.Value, false);
                    SameV(want[0], t.Kind.ToString(), kv.Key + ": kind");
                    SameV(want[1], t.Ext, kv.Key + ": extension");
                    SameV(t.Kind == AttachKind.Executable || t.Kind == AttachKind.Shortcut, XmlAttachment.Blocked(t, null), kv.Key + ": blocked");
                    AttachType p = XmlAttachment.Peek(Convert.ToBase64String(kv.Value));
                    SameV(t.Kind, p.Kind, kv.Key + ": peek of a small field");
                }
                Check(XmlAttachment.Detect(X6Picture(System.Drawing.Imaging.ImageFormat.Png), false).Image, "PNG is an image");
                Check(XmlAttachment.Detect(X6Pdf(), false).Document && !XmlAttachment.Detect(X6Pdf(), false).Image, "PDF is a document");
                Check(!XmlAttachment.Detect(X6ZipOf("a.txt"), false).Opens, "a plain ZIP opens");
                // A big field is typed from its start: a ZIP might still be an Office file.
                byte[] bigZip = X6ZipOf("[Content_Types].xml", "word/document.xml");
                byte[] padded = new byte[400 * 1024];
                Buffer.BlockCopy(bigZip, 0, padded, 0, bigZip.Length);
                AttachType big = XmlAttachment.Peek(Convert.ToBase64String(padded));
                Check(big.Kind == AttachKind.Zip && big.Partial && big.Opens, "big ZIP: " + big.Kind + " partial " + big.Partial);
                AttachType bigPdf = XmlAttachment.Peek(Convert.ToBase64String(X6Concat(X6Pdf(), new byte[400 * 1024])));
                SameV(AttachKind.Pdf, bigPdf.Kind, "big PDF typed from its start");
            });
            Test("w6xml", "blocked-names-hints-and-file-names", delegate
            {
                AttachType text = XmlAttachment.Detect(Encoding.UTF8.GetBytes("echo hola"), false), pdf = XmlAttachment.Detect(X6Pdf(), false);
                foreach (string bad in new string[] { "script.js", "run.BAT", "x.exe ", "a.lnk", "doc.docm", "inst.msi", "p.ps1", "h.hta", "s.scr", "v.vbs", "c.cmd", "x.exe." })
                    Check(XmlAttachment.Blocked(text, bad), "not blocked: " + bad);
                foreach (string ok in new string[] { null, "", "factura.PDF", "datos.xml", "notas.txt", "foto.jpeg" })
                    Check(!XmlAttachment.Blocked(text, ok), "blocked: " + ok);
                XmlModel m = Model("<r><doc nombreFichero=\"..\\..\\carpeta/malo.exe\">QUJD</doc><adj><nombreFichero>factura 2026.pdf</nombreFichero><contenido>QUJD</contenido></adj><sin>QUJD</sin><raro filename=\"sin extension\">QUJD</raro></r>");
                SameV("malo.exe", XmlAttachment.HintName(FirstNamed(m.Top, "doc")), "attribute hint, path removed");
                SameV("factura 2026.pdf", XmlAttachment.HintName(FirstNamed(m.Top, "contenido")), "sibling hint");
                SameV("factura 2026.pdf", XmlAttachment.HintName(FirstNamed(m.Top, "contenido").Kids[0]), "hint from the text node");
                SameV(null, XmlAttachment.HintName(FirstNamed(m.Top, "sin")), "no hint");
                SameV(null, XmlAttachment.HintName(FirstNamed(m.Top, "raro")), "a value without an extension is no file name");
                SameV("factura.pdf", XmlAttachment.FileName(pdf, "factura.pdf"), "own name");
                SameV("virus.pdf", XmlAttachment.FileName(pdf, "virus.exe"), "the detected extension wins");
                SameV("adjunto.pdf", XmlAttachment.FileName(pdf, null), "no name");
                SameV("datos.dat", XmlAttachment.FileName(new AttachType(), "datos.dat"), "unknown keeps its extension");
                SameV("adjunto.bin", XmlAttachment.FileName(new AttachType(), null), "unknown without a name");
                SameV("foto.jpeg", XmlAttachment.FileName(XmlAttachment.Detect(X6Picture(System.Drawing.Imaging.ImageFormat.Jpeg), false), "foto.jpeg"), ".jpeg kept");
                SameV("abc.txt", XmlAttachment.CleanName(" C:\\x\\a<b>c?.txt "), "clean name");
                SameV(null, XmlAttachment.CleanName(" ... "), "nothing left");
                SameV("_CON.pdf", XmlAttachment.CleanName("CON.pdf"), "device name");
                SameV("_nul", XmlAttachment.CleanName("nul"), "device name without extension");
                SameV("_com3.txt", XmlAttachment.CleanName("com3.txt"), "COM port");
                SameV("console.txt", XmlAttachment.CleanName("console.txt"), "a name that only starts like a device");
                SameV("_CON.pdf", XmlAttachment.FileName(pdf, "CON.exe"), "device name from a hint");
            });
            Test("w6xml", "decode-cap-and-bad-base64", delegate
            {
                int keep = XmlAttachment.MaxBytes;
                try
                {
                    XmlAttachment.MaxBytes = 1000;
                    string err;
                    byte[] got = XmlAttachment.Decode(Convert.ToBase64String(new byte[3000]), out err);
                    Check(got == null && err != null && err.Contains("demasiado grande"), "cap: " + err);
                    got = XmlAttachment.Decode(Convert.ToBase64String(new byte[900]), out err);
                    Check(got != null && got.Length == 900, "under the cap: " + err);
                    SameV(50 * 1024 * 1024, keep, "default cap");
                    got = XmlAttachment.Decode("QUJD*EFG", out err);
                    Check(got == null && err.Contains("base64"), "invalid: " + err);
                }
                finally { XmlAttachment.MaxBytes = keep; }
            });
            Test("w6xml", "prune-like-temporary-captures", delegate
            {
                X6NeedTestData();
                string dir = XmlAttachment.Folder;
                Directory.CreateDirectory(dir);
                string old = Path.Combine(dir, "viejo.pdf"), day = Path.Combine(dir, "ayer.pdf"), fresh = Path.Combine(dir, "nuevo.pdf");
                try
                {
                    File.WriteAllText(old, "x"); File.SetLastWriteTime(old, DateTime.Now.AddHours(-2));
                    File.WriteAllText(day, "x"); File.SetLastWriteTime(day, DateTime.Now.AddDays(-2));
                    File.WriteAllText(fresh, "x");
                    SameV(1, XmlAttachment.Prune(2), "a day: only the two-day-old one");
                    Check(File.Exists(old) && !File.Exists(day) && File.Exists(fresh), "a day");
                    SameV(1, XmlAttachment.Prune(0), "an hour");
                    Check(!File.Exists(old) && File.Exists(fresh), "an hour");
                    File.WriteAllText(old, "x"); File.SetLastWriteTime(old, DateTime.Now.AddDays(-1));
                    SameV(1, XmlAttachment.Prune(1), "until Stackshot closes: files from before this session");
                    Check(File.Exists(fresh), "this session's file went");
                }
                finally { DeleteDir(dir); }
            });
            Test("w6xml", "attachments-open-and-save-in-window", delegate
            {
                X6NeedTestData();
                string dir = WorkDir("adjuntos");
                byte[] pdf = X6Pdf(), png = X6Picture(System.Drawing.Imaging.ImageFormat.Png), gif = X6Picture(System.Drawing.Imaging.ImageFormat.Gif);
                byte[] tail = new byte[200];
                new Random(11).NextBytes(tail);
                byte[] exe = X6Concat(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, tail);
                byte[] inner = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a>1</a>\n");
                string doc = "<r>\n  <pdf nombre=\"factura.pdf\">" + X6B64(pdf, true) + "</pdf>\n  <img>" + X6B64(png, false) + "</img>\n  <gif>" + X6B64(gif, false) + "</gif>\n" +
                             "  <exe>" + X6B64(exe, false) + "</exe>\n  <xml>" + X6B64(inner, false) + "</xml>\n  <txt>" + X6B64(Encoding.UTF8.GetBytes("Texto plano del adjunto, con m\u00E1s de cuarenta y ocho bytes."), false) + "</txt>\n</r>\n";
                string p = Put(dir, "adjuntos.xml", doc);
                List<string> apps = new List<string>(), edits = new List<string>();
                int asked = 0, answer = 0;
                MarkdownWindow.OpenWithApp = delegate(string f) { apps.Add(f); return true; };
                MarkdownWindow.OpenInEditor = delegate(string f) { edits.Add(f); return true; };
                DocAlert.Answer = delegate { asked++; return answer; };
                MarkdownWindow w = NewWindow();
                try
                {
                    Open(w, p);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Model != null; }, 8000), "no tree");
                    Func<string, XItem> node = delegate(string name) { return FirstNamed(w.Tree.Model.Top, name); };
                    // The menu.
                    Func<string, List<MenuEntry>> menu = delegate(string name)
                    {
                        List<MenuEntry> m = new List<MenuEntry>();
                        XRow row = w.Tree.Rows.Find(delegate(XRow x) { return x.Node == node(name) && !x.Close; });
                        Call(w.Tree, "ValueEntries", m, row, null);
                        return m;
                    };
                    List<MenuEntry> pm = menu("pdf");
                    MenuEntry openPdf = pm.Find(delegate(MenuEntry e) { return e.Label != null && e.Label.StartsWith("Abrir el adjunto"); });
                    Check(openPdf != null && openPdf.Enabled && openPdf.Label == "Abrir el adjunto (PDF)", "PDF entry: " + (openPdf == null ? "none" : openPdf.Label));
                    Check(pm.Exists(delegate(MenuEntry e) { return e.Label == "Guardar el adjunto como\u2026" && e.Enabled; }), "save entry");
                    MenuEntry openExe = menu("exe").Find(delegate(MenuEntry e) { return e.Label != null && e.Label.StartsWith("Abrir el adjunto"); });
                    Check(openExe != null && !openExe.Enabled && openExe.Detail.Contains("ejecutable"), "exe entry: " + (openExe == null ? "none" : openExe.Label + " / " + openExe.Detail));
                    Check(menu("img").Exists(delegate(MenuEntry e) { return e.Label == "Abrir el adjunto (imagen PNG)"; }), "PNG entry");

                    // PDF: asks, then the default program (the test's) gets the file from the private folder.
                    Call(w, "OnAttachment", node("pdf"), true);
                    Check(Until(delegate { return apps.Count == 1; }, 8000), "PDF not opened (asked " + asked + ")");
                    SameV(1, asked, "asked before opening the PDF");
                    Check(apps[0].StartsWith(XmlAttachment.Folder, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(apps[0]) == "factura.pdf", "PDF path " + apps[0]);
                    Check(X6SameBytes(pdf, File.ReadAllBytes(apps[0])), "PDF bytes");
                    answer = 1;
                    Call(w, "OnAttachment", node("pdf"), true);
                    Check(Until(delegate { return asked == 2; }, 8000), "second question");
                    Pump(200);
                    SameV(1, apps.Count, "opened after Cancelar");
                    answer = 0;
                    // Images: Stackshot's editor; a GIF goes as a PNG of its first frame.
                    Call(w, "OnAttachment", node("img"), true);
                    Check(Until(delegate { return edits.Count == 1; }, 8000), "PNG not opened in the editor");
                    Check(edits[0].EndsWith(".png") && X6SameBytes(png, File.ReadAllBytes(edits[0])), "PNG file " + edits[0]);
                    Call(w, "OnAttachment", node("gif"), true);
                    Check(Until(delegate { return edits.Count == 2; }, 8000), "GIF not opened in the editor");
                    byte[] conv = File.ReadAllBytes(edits[1]);
                    Check(edits[1].EndsWith(".png") && conv.Length > 8 && conv[0] == 0x89 && conv[1] == 0x50, "GIF as PNG " + edits[1]);
                    SameV(2, asked, "asked for an image");
                    // A program is never opened, nor written to the folder.
                    int filesBefore = Directory.GetFiles(XmlAttachment.Folder).Length;
                    Call(w, "OnAttachment", node("exe"), true);
                    Check(Until(delegate { string s = Field<string>(w, "status"); return s != null && s.Contains("seguridad"); }, 8000), "no word about the program: " + Field<string>(w, "status"));
                    Check(apps.Count == 1 && edits.Count == 2 && asked == 2, "something ran the program");
                    SameV(filesBefore, Directory.GetFiles(XmlAttachment.Folder).Length, "the program was written to the folder");
                    // Saving: decoded, named after the message, written where the user says.
                    string target = Path.Combine(dir, "guardado.pdf");
                    string suggested = null;
                    MarkdownWindow.PickPath = delegate(string s) { suggested = s; return target; };
                    Call(w, "OnAttachment", node("pdf"), false);
                    Check(Until(delegate { return File.Exists(target); }, 8000), "not saved");
                    Pump(100);
                    SameV("factura.pdf", suggested, "suggested name");
                    Check(X6SameBytes(pdf, File.ReadAllBytes(target)), "saved bytes");
                    // XML and text: a new document that is not a file (Guardar asks where).
                    Call(w, "OnAttachment", node("xml"), true);
                    Check(Until(delegate { return w.Doc != null && w.Doc.Path == null && w.Text != null && w.Text.Contains("<a>1</a>"); }, 8000), "inner XML not shown: " + (w.Doc != null ? w.Doc.Path : "null"));
                    Check(w.Title.StartsWith("adjunto.xml"), "title " + w.Title);
                    Check(!w.Dirty, "a fresh attachment counts as unsaved");
                    // Unsaved changes: Cancelar keeps the document.
                    Open(w, p);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Model != null; }, 8000), "tree again");
                    XItem txtNode = node("txt");
                    w.SetEditing(true);
                    Pump(200);
                    w.Editor.Apply(TextEdit.Make(w.Editor.Text.Length, 0, "\n<!-- cambio -->", 0, 0));
                    Pump(100);
                    answer = 2;
                    int before = asked;
                    Call(w, "OnAttachment", txtNode, true);
                    Check(Until(delegate { return asked == before + 1; }, 8000), "not asked about the unsaved changes");
                    Pump(200);
                    Check(w.Doc.Path == p && w.Dirty, "the document with changes was replaced");
                    answer = 1;
                    Call(w, "OnAttachment", txtNode, true);
                    Check(Until(delegate { return w.Doc != null && w.Doc.Path == null && w.Text.StartsWith("Texto plano"); }, 8000), "text attachment after No guardar");
                    Check(w.Title.StartsWith("adjunto.txt"), "text title " + w.Title);
                }
                finally
                {
                    MarkdownWindow.OpenWithApp = null;
                    MarkdownWindow.OpenInEditor = null;
                    MarkdownWindow.PickPath = null;
                    DocAlert.Answer = null;
                    Shut(w);
                    DeleteDir(dir);
                    R5ClearDir(XmlAttachment.Folder);
                }
            });
        }

        // A prefix longer than the menu decodes whole, so Peek takes its "start only" path.
        static int SampleSize(string field) { return Math.Min(field.Length, 300 * 1024); }

        static byte[] X6Concat(byte[] a, byte[] b)
        {
            byte[] o = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, o, 0, a.Length);
            Buffer.BlockCopy(b, 0, o, a.Length, b.Length);
            return o;
        }

        // ---- the comparison

        static string X6Items(int from, int to, Func<int, string> one)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = from; i <= to; i++) sb.Append(one(i));
            return sb.ToString();
        }

        static void W6Diff()
        {
            Test("w6xml", "diff-insert-in-the-middle", delegate
            {
                DiffResult r = X6DiffOf("<r><i>1</i><i>2</i><i>3</i><i>4</i></r>", "<r><i>1</i><i>2</i><i>9</i><i>3</i><i>4</i></r>");
                SameV("0 changed, 1 added, 0 removed", X6Counts(r), "unkeyed insert");
                SameV("+i", X6Stops(r), "stops");
                SameV("9", XmlDoc.TextOf(r.Stops[0].New), "the inserted one");
                // Keyed, with a change further on: still one insert and one change.
                Func<int, string> item = delegate(int i) { return "<item id=\"" + i + "\"><v>" + i + "</v></item>"; };
                string a = "<r>" + X6Items(1, 6, item) + "</r>";
                string b = "<r>" + X6Items(1, 2, item) + "<item id=\"9\"><v>9</v></item>" + X6Items(3, 5, item) + "<item id=\"6\"><v>seis</v></item></r>";
                r = X6DiffOf(a, b);
                SameV("1 changed, 1 added, 0 removed", X6Counts(r), "keyed insert and change");
                SameV("+item ~v[6>seis]", X6Stops(r), "keyed stops");
                // No key, the new element next to a changed one: the most alike pair up, the new one is added.
                r = X6DiffOf("<r><p><a>1</a><b>2</b></p><p><a>3</a><b>4</b></p></r>", "<r><p><a>1</a><b>2</b></p><p><a>X</a><b>Y</b></p><p><a>3</a><b>5</b></p></r>");
                SameV("1 changed, 1 added, 0 removed", X6Counts(r), "similar pairs");
                SameV("+p ~b[4>5]", X6Stops(r), "similar stops");
            });
            Test("w6xml", "diff-reorder-delete-and-add", delegate
            {
                DiffResult r = X6DiffOf("<r><a/><b/><c/></r>", "<r><c/><a/><b/></r>");
                SameV("0 changed, 1 added, 1 removed", X6Counts(r), "moved element");
                SameV("+c -c", X6Stops(r), "reorder stops");
                r = X6DiffOf("<r><a>1</a><b>2</b><c>3</c></r>", "<r><a>1</a><c>3</c></r>");
                SameV("-b", X6Stops(r), "deleted");
                r = X6DiffOf("<r><a>1</a></r>", "<r><a>1</a><n><m>1</m><m>2</m></n></r>");
                SameV("0 changed, 1 added, 0 removed", X6Counts(r), "a new branch counts once");
                DiffNode added = r.Stops[0];
                Check(added.Kids.Count == 2 && added.Kids[0].Kind == DiffKind.Added && !added.Kids[0].Stop, "added branch children");
                // Keys that differ in the same place: replaced, not changed.
                r = X6DiffOf("<r><x id=\"1\"><v>a</v></x><x id=\"2\"><v>b</v></x></r>", "<r><x id=\"2\"><v>b</v></x><x id=\"3\"><v>c</v></x></r>");
                SameV("0 changed, 1 added, 1 removed", X6Counts(r), "keyed replace");
            });
            Test("w6xml", "diff-attributes", delegate
            {
                DiffResult r = X6DiffOf("<r><x id=\"1\" estado=\"A\"/></r>", "<r><x id=\"1\" estado=\"B\"/></r>");
                SameV("~x{estado:A>B}", X6Stops(r), "changed value");
                r = X6DiffOf("<r><x a=\"1\" c=\"3\"/></r>", "<r><x b=\"2\" c=\"3\"/></r>");
                SameV("~x{b:null>2}{a:1>null}", X6Stops(r), "added and removed");
                SameV(DiffKind.Added, r.Stops[0].Attrs.Find(delegate(AttrDiff d) { return d.Name == "b"; }).Kind, "added kind");
                r = X6DiffOf("<r><x a=\"1\" b=\"2\"/></r>", "<r><x b=\"2\" a=\"1\"/></r>");
                SameV(0, r.Total, "attribute order");
                // The key of a lone element changed: a change. In a list, other keys are other items.
                r = X6DiffOf("<r><x id=\"1\">v</x></r>", "<r><x id=\"2\">v</x></r>");
                SameV("~x{id:1>2}", X6Stops(r), "lone element with another key");
                r = X6DiffOf("<r><x id=\"1\"/><x id=\"2\"/></r>", "<r><x id=\"3\"/><x id=\"4\"/></r>");
                SameV("-x -x +x +x", X6Stops(r), "list with other keys");
                // A key that appears is an attribute change.
                r = X6DiffOf("<r><x>v</x></r>", "<r><x codigo=\"A\">v</x></r>");
                SameV("~x{codigo:null>A}", X6Stops(r), "key added");
            });
            Test("w6xml", "diff-text-deep-and-identical", delegate
            {
                DiffResult r = X6DiffOf("<r><nombre>Ana</nombre><edad>3</edad></r>", "<r><nombre>Eva</nombre><edad>3</edad></r>");
                SameV("~nombre[Ana>Eva]", X6Stops(r), "text");
                Check(r.Stops[0].Kids.Count == 0 && DiffRows.Inline(r.Stops[0]), "text change not on the element's line");
                r = X6DiffOf("<r><a/></r>", "<r><a>x</a></r>");
                SameV("~a[null>x]", X6Stops(r), "text where there was none");
                string deepA = "<r><a><b><c><d><e><f>uno</f><g>2</g></e></d></c></b></a><z>1</z></r>";
                r = X6DiffOf(deepA, deepA.Replace("uno", "dos"));
                SameV("~f[uno>dos]", X6Stops(r), "deep");
                for (DiffNode d = r.Stops[0].Parent; d != null; d = d.Parent) Check(d.Inside && d.Kind == DiffKind.Same, "ancestor " + d.Item.Local + " not marked as holding the change");
                r = X6DiffOf(deepA, deepA);
                Check(r.Total == 0 && r.Stops.Count == 0, "identical: " + X6Counts(r));
                Check(!r.Top[0].Inside && r.Top[0].Kind == DiffKind.Same, "identical root");
                SameV(12, r.Nodes, "nodes of an unchanged document (made or not)");
                // An unchanged branch makes its children when looked at, paired in order.
                DiffNode root = r.Top[0];
                Check(root.Kids.Count == 2 && root.Kids[0].Old.Local == "a" && root.Kids[1].New.Local == "z" && root.Kids[0].Kind == DiffKind.Same && root.Kids[0].Old != root.Kids[0].New, "children made on demand");
                Check(root.Kids[0].Kids[0].Kids[0].Kids[0].Kids[0].Kids[0].Old.Local == "f", "deep children made on demand");
                r = X6DiffOf("<r><!-- uno --><a/></r>", "<r><!-- dos --><a/></r>");
                SameV("~#comment[ uno > dos ]", X6Stops(r), "comment");
                r = X6DiffOf("<r><a>1</a>texto<b/></r>", "<r><a>1</a>otro<b/></r>");
                SameV("~#text[texto>otro]", X6Stops(r), "mixed text");
            });
            Test("w6xml", "diff-namespaces-whitespace-cdata", delegate
            {
                string a = "<S:Envelope xmlns:S=\"" + X6Soap11 + "\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><S:Body><ns2:op xmlns:ns2=\"urn:x\"><ns2:a xsi:nil=\"true\"/></ns2:op></S:Body></S:Envelope>";
                string b = "<soapenv:Envelope xmlns:soapenv=\"" + X6Soap11 + "\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><soapenv:Body><op xmlns=\"urn:x\"><a i:nil=\"true\"/></op></soapenv:Body></soapenv:Envelope>";
                DiffResult r = X6DiffOf(a, b);
                SameV(0, r.Total, "prefix-only change: " + X6Stops(r));
                r = X6DiffOf("<a xmlns=\"urn:1\"><b>1</b></a>", "<a xmlns=\"urn:2\"><b>1</b></a>");
                Check(r.Total > 0, "another namespace URI went unnoticed");
                string pretty = "<?xml version=\"1.0\"?>\r\n<r>\r\n    <a  x=\"1\" >Hola   mundo\r\n</a>\r\n    <b/>\r\n</r>\r\n";
                string tight = "<r><a x=\"1\">Hola mundo</a><b></b></r>";
                r = X6DiffOf(pretty, tight);
                SameV(0, r.Total, "whitespace only: " + X6Stops(r));
                r = X6DiffOf("<r><c><![CDATA[dato]]></c></r>", "<r><c>dato</c></r>");
                SameV(0, r.Total, "CDATA against the same text");
                r = X6DiffOf("<r>Hola mundo</r>", "<r>Hola  mundo!</r>");
                SameV(1, r.Changed, "a real text change hidden by whitespace rules");
            });
            Test("w6xml", "diff-inner-xml", delegate
            {
                string a = Soap("<return>" + SecurityElement.Escape(InnerDoc) + "</return>");
                string b = Soap("<return>" + SecurityElement.Escape(InnerDoc.Replace("<Codigo>0</Codigo>", "<Codigo>1</Codigo>").Replace("nombre=\"Ana\"", "nombre=\"Eva\"")) + "</return>");
                DiffResult r = X6DiffOf(a, b);
                SameV("~Codigo[0>1] ~Persona{nombre:Ana>Eva}", X6Stops(r), "inner changes");
                Check(r.Stops[0].New.Parent.Parent.Inner, "the change is inside the opened field");
                // Formatting inside the escaped document does not count.
                string c = Soap("<return>" + SecurityElement.Escape(InnerDoc.Replace("><", ">\n   <")) + "</return>");
                SameV(0, X6DiffOf(a, c).Total, "whitespace inside the escaped document");
            });
            Test("w6xml", "diff-big-lists", delegate
            {
                StringBuilder a = new StringBuilder("<r>"), b = new StringBuilder("<r>");
                for (int i = 0; i < 10000; i++)
                {
                    a.Append("<row><v>" + i + "</v></row>");
                    if (i == 5000) b.Append("<row><v>nuevo</v></row>");
                    b.Append("<row><v>" + i + "</v></row>");
                }
                a.Append("</r>"); b.Append("</r>");
                Stopwatch sw = Stopwatch.StartNew();
                DiffResult r = X6DiffOf(a.ToString(), b.ToString());
                Budget("10,000 rows, one inserted", sw.Elapsed.TotalMilliseconds, 1500);
                SameV("0 changed, 1 added, 0 removed", X6Counts(r), "big insert");
                // Everything different, nothing unique on both sides: still a pairing, still fast.
                a = new StringBuilder("<r>"); b = new StringBuilder("<r>");
                for (int i = 0; i < 3000; i++) { a.Append("<row>a" + i + "</row>"); b.Append("<row>b" + i + "</row>"); }
                a.Append("</r>"); b.Append("</r>");
                sw.Restart();
                r = X6DiffOf(a.ToString(), b.ToString());
                Budget("3,000 rows all changed", sw.Elapsed.TotalMilliseconds, 2500);
                SameV("3000 changed, 0 added, 0 removed", X6Counts(r), "all changed");
                // Deep documents: nothing recurses.
                StringBuilder deep = new StringBuilder();
                for (int i = 0; i < 20000; i++) deep.Append("<d>");
                for (int i = 0; i < 20000; i++) deep.Append("</d>");
                string dd = deep.ToString();
                r = X6DiffOf(dd, dd.Insert(20000 * 3, "x"));
                SameV("~d[null>x]", X6Stops(r), "deep change");
                r = X6DiffOf(dd, dd.Insert(20000 * 3 + 4 * 2500, "x"));
                SameV("+#text", X6Stops(r), "text added next to a child, deep down");
            });
            Test("w6xml", "diff-perf-two-5mb-soap", delegate
            {
                // As many records as make the first one 5 MB; the second has the same ones but four differences.
                int records = 0;
                Func<bool, string> make = delegate(bool second)
                {
                    StringBuilder sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<S:Envelope xmlns:S=\"" + X6Soap11 + "\">\n  <S:Header><t:Traza xmlns:t=\"urn:traza\">" + (second ? "B" : "A") + "</t:Traza></S:Header>\n  <S:Body>\n    <ns2:consultaResponse xmlns:ns2=\"urn:ejemplo\">\n");
                    for (int i = 0; second ? i < records : sb.Length < 5 * 1024 * 1024; i++)
                    {
                        if (second && i == 1234) sb.Append("      <expediente codigo=\"N-1\"><nombre>Nuevo</nombre></expediente>\n");
                        if (!(second && i == 20000))
                            sb.Append("      <expediente codigo=\"E-" + i + "\" estado=\"" + (second && i == 7000 ? "CERRADO" : "ABIERTO") + "\"><nombre>Persona n\u00FAmero " + i + "</nombre><importe>" + (i * 3) + ".50</importe><fecha>2026-10-" + (1 + i % 28).ToString("00") + "</fecha></expediente>\n");
                        if (!second) records = i + 1;
                    }
                    sb.Append("    </ns2:consultaResponse>\n  </S:Body>\n</S:Envelope>\n");
                    return sb.ToString();
                };
                string ta = make(false), tb = make(true);
                Stopwatch sw = Stopwatch.StartNew();
                XmlModel a = Model(ta), b = Model(tb);
                XmlDoc.ExpandInner(a);
                XmlDoc.ExpandInner(b);
                double parse = sw.Elapsed.TotalMilliseconds;
                Put("parse_both_ms", Math.Round(parse, 1));
                // The first comparison in the process (code still to compile included), then a second one.
                sw.Restart();
                DiffResult r = XmlDiff.Compare(a, b, null);
                Budget("compare two 5 MB SOAP messages", sw.Elapsed.TotalMilliseconds, 1000);
                sw.Restart();
                XmlDiff.Compare(a, b, null);
                Add("again " + Ms(sw.Elapsed.TotalMilliseconds) + ", " + a.Count.ToString("N0") + " nodes each");
                Add("parsing both " + Ms(parse) + " (the open one is already parsed in the viewer)");
                SameV("2 changed, 1 added, 1 removed", X6Counts(r), "changes found");
                SameV("~Traza[A>B] +expediente ~expediente{estado:ABIERTO>CERRADO} -expediente", X6Stops(r), "stops");
                // What the UI thread does with it: lay out the view's lines.
                XmlDiffLook look = new XmlDiffLook(Ds.Brushes);
                sw.Restart();
                List<DiffRow> rows = DiffRows.Walk(r.Top, true, look);
                Budget("lines of the differences", sw.Elapsed.TotalMilliseconds, 16);
                Check(rows.Count < 40, "lines " + rows.Count);
                // The view itself, and the whole document (unchanged branches closed) when the filter is turned off.
                sw.Restart();
                XmlDiffView view = new XmlDiffView(Ds.Brushes, r, true);
                Add("view made in " + Ms(sw.Elapsed.TotalMilliseconds));
                Check(view.Rows.Count == rows.Count, "view lines " + view.Rows.Count);
                sw.Restart();
                view.OnlyDiffs = false;
                Budget("all " + view.Rows.Count.ToString("N0") + " lines without the filter", sw.Elapsed.TotalMilliseconds, 150);
                Check(view.Rows.Count > records && view.Rows.TrueForAll(delegate(DiffRow x) { return x.Gap == 0; }), "full lines " + view.Rows.Count);
            });
        }

        // ---- the compare view in the window

        static string X6StatusText(MarkdownWindow w) { return Field<TextBlock>(w, "statusLabel").Text; }

        static void W6CompareView()
        {
            Test("w6xml", "compare-view-in-window", delegate
            {
                string dir = WorkDir("compare");
                string older = "<?xml version=\"1.0\"?>\n<pedido id=\"7\">\n  <fecha><dia>12</dia><mes>10</mes></fecha>\n  <cliente>Ana</cliente>\n  <lineas>\n<linea codigo=\"A-1\" cantidad=\"2\"/>\n    <linea codigo=\"B-2\" cantidad=\"1\"/>\n    <linea codigo=\"C-3\" cantidad=\"5\"/>\n    <linea codigo=\"D-4\" cantidad=\"1\"/>\n  </lineas>\n  <nota>urgente</nota>\n  <total>10</total>\n</pedido>\n";
                string newer = "<?xml version=\"1.0\"?>\n<pedido id=\"7\">\n  <fecha><dia>12</dia><mes>10</mes></fecha>\n  <cliente>Eva</cliente>\n  <lineas>\n    <linea codigo=\"A-1\" cantidad=\"3\"/>\n    <linea codigo=\"E-5\" cantidad=\"1\"/>\n  </lineas>\n  <total>12</total>\n</pedido>\n";
                string a = Put(dir, "tmp1.xml", older), b = Put(dir, "tmp2.xml", newer), other = Put(dir, "otro.xml", newer.Replace("Eva", "Luis"));
                File.SetLastWriteTime(a, DateTime.Now.AddMinutes(-5));
                File.SetLastWriteTime(b, DateTime.Now.AddMinutes(-4));
                File.SetLastWriteTime(other, DateTime.Now.AddMinutes(-3));
                MarkdownWindow w = NewWindow();
                try
                {
                    Open(w, b);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Rows.Count > 0; }, 8000), "no tree");
                    Call(w, "CompareWith", new object[] { null });
                    Check(Until(delegate { return w.Comparing && !w.CompareBusy; }, 8000), "no comparison: " + Field<string>(w, "status"));
                    Pump(150);
                    XmlDiffView v = w.CompareView;
                    // cliente, total and A-1's quantity changed; E-5 added; B-2, C-3, D-4 and nota removed (lines are keyed by codigo).
                    DiffResult r = v.Result;
                    SameV("3 cambios \u00B7 1 a\u00F1adido \u00B7 4 quitados", X6StatusText(w), "status line (" + X6Counts(r) + ": " + X6Stops(r) + ")");
                    Border host = Field<Border>(w, "previewHost");
                    Check(host.Child == v, "the view is not on screen");
                    string bar = Flat(w.HeaderHost.Child);
                    Check(bar.Contains("tmp1.xml") && bar.Contains("tmp2.xml") && bar.Contains("Comparaci\u00F3n"), "compare bar: " + bar);
                    List<ChipToggle> chips = new List<ChipToggle>();
                    Collect(w.HeaderHost, chips);
                    Check(chips.Count == 1 && chips[0].IsOn, "Solo diferencias toggle: " + chips.Count);
                    List<MacButton> buttons = new List<MacButton>();
                    Collect(w.HeaderHost, buttons);
                    Check(buttons.Exists(delegate(MacButton x) { return x.Label == "Salir de la comparaci\u00F3n"; }), "no way out on the bar");
                    SameV(0, v.Current, "the first difference is selected");
                    int total = r.Stops.Count;
                    Press(v.List, System.Windows.Input.Key.F7, System.Windows.Input.ModifierKeys.None);
                    Pump(50);
                    SameV(1, v.Current, "F7");
                    Check(Flat(w.HeaderHost.Child).Contains("2 de " + total), "position: " + Flat(w.HeaderHost.Child));
                    Press(v.List, System.Windows.Input.Key.F7, System.Windows.Input.ModifierKeys.Shift);
                    Pump(50);
                    SameV(0, v.Current, "Shift+F7");
                    Press(v.List, System.Windows.Input.Key.F7, System.Windows.Input.ModifierKeys.Shift);
                    Pump(50);
                    SameV(total - 1, v.Current, "Shift+F7 wraps to the last");
                    Check(Field<string>(w, "status") != null && Field<string>(w, "status").Contains("\u00FAltima"), "no word about wrapping");
                    // Only the differences (and what leads to them) by default; all of it, dimmed, without the filter.
                    int onlyRows = v.Rows.Count;
                    Check(v.Rows.Exists(delegate(DiffRow x) { return x.Gap > 0; }), "no line for the unchanged ones left out");
                    Check(!v.Rows.Exists(delegate(DiffRow x) { return x.Gap == 0 && x.Dim; }), "unchanged lines while filtering");
                    v.OnlyDiffs = false;
                    Pump(50);
                    Check(v.Rows.Count > onlyRows && v.Rows.Exists(delegate(DiffRow x) { return x.Dim; }) && !v.Rows.Exists(delegate(DiffRow x) { return x.Gap > 0; }), "full view: " + v.Rows.Count + " vs " + onlyRows);
                    X6BitmapSourceCheck(w, "compare-full");
                    v.OnlyDiffs = true;
                    Pump(50);
                    X6BitmapSourceCheck(w, "compare-only");
                    // Copiar como imagen and Listo picture the comparison, not the tree behind it.
                    MethodInfo render = typeof(MarkdownWindow).GetMethod("RenderDoc", NP);
                    object[] rargs = new object[] { null };
                    System.Windows.Media.Imaging.BitmapSource img = (System.Windows.Media.Imaging.BitmapSource)render.Invoke(w, rargs);
                    Check(img != null && img.PixelWidth > 800, "picture of the comparison: " + rargs[0]);
                    SavePng(img, "compare-picture.png");
                    string err;
                    System.Windows.Media.Imaging.BitmapSource same = XmlDiffView.Render(v.Rows, v.Look, out err);
                    Check(same != null && same.PixelHeight == img.PixelHeight, "the window's picture is not the comparison's");
                    // Copying the lines keeps their marks.
                    v.List.SelectAll();
                    string copied = v.SelectionText();
                    Check(copied.Contains("+ ") && copied.Contains("- ") && copied.Contains("~ ") && copied.Contains("\u2192"), "copied text: " + copied);
                    // Contraer / Expandir todo work on the comparison.
                    Call(w, "ExpandXml", false);
                    Pump(50);
                    DiffRow lineas = v.Rows.Find(delegate(DiffRow x) { return x.Gap == 0 && !x.Close && x.Node.Item.Local == "lineas"; });
                    Check(v.Rows.Count < onlyRows && lineas != null && lineas.Collapsed, "collapsed rows " + v.Rows.Count + " of " + onlyRows);
                    Call(w, "ExpandXml", true);
                    Pump(50);
                    Check(v.Rows.Count >= onlyRows, "expanded rows " + v.Rows.Count);
                    // A new theme keeps the comparison.
                    Ds.Apply(2);
                    Pump(300);
                    Check(w.Comparing && w.CompareView != null && host != Field<Border>(w, "previewHost") && Field<Border>(w, "previewHost").Child == w.CompareView, "comparison lost with the theme");
                    DiffNode before = v.SelectedNode;
                    Check(w.CompareView != v, "the view was not redrawn for the theme");
                    Check(w.CompareView.SelectedNode == before && before != null, "selection lost with the theme: " + (w.CompareView.SelectedNode == null ? "none" : w.CompareView.SelectedNode.Item.Local) + " (was " + (before == null ? "none" : before.Item.Local) + ")");
                    Check(Flat(w.HeaderHost.Child).Contains(total + " de " + total), "position after the theme: " + Flat(w.HeaderHost.Child));
                    X6BitmapSourceCheck(w, "compare-dark");
                    Ds.Apply(1);
                    Pump(300);
                    // Esc leaves.
                    Press(w.CompareView.List, System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None);
                    Pump(100);
                    Check(!w.Comparing && w.IsVisible, "Esc did not leave the comparison (or closed the window)");
                    Check(Field<Border>(w, "previewHost").Child == w.Tree, "the tree is not back");
                    Check(!Flat(w.HeaderHost == null ? null : w.HeaderHost.Child).Contains("Solo diferencias"), "compare bar left over");
                    Check(!X6StatusText(w).Contains("cambio"), "status still counts: " + X6StatusText(w));
                    // Another file, picked by the user.
                    MarkdownWindow.PickCompare = delegate { return other; };
                    Call(w, "CompareOther");
                    Check(Until(delegate { return w.Comparing && !w.CompareBusy; }, 8000), "no comparison with another file");
                    Pump(100);
                    SameV("1 cambio", X6StatusText(w), "counts against the other file");
                    // Editing leaves it.
                    w.SetEditing(true);
                    Pump(300);
                    Check(!w.Comparing, "still comparing while editing");
                    w.SetEditing(false);
                    Pump(300);
                    Check(!w.Comparing && Field<Border>(w, "previewHost").Child == w.Tree, "the comparison came back");
                    // Moving to another file leaves it too.
                    Call(w, "CompareWith", new object[] { null });
                    Check(Until(delegate { return w.Comparing && !w.CompareBusy; }, 8000), "third comparison");
                    Call(w, "Neighbour", -1);
                    Check(Until(delegate { return w.Doc != null && w.Doc.Path == a && w.Tree != null && w.Tree.Rows.Count > 0; }, 8000), "[ did not move");
                    Pump(150);
                    Check(!w.Comparing && Field<Border>(w, "previewHost").Child == w.Tree, "comparison kept for another document");
                    // The first file has none before it.
                    Call(w, "CompareWith", new object[] { null });
                    Check(Until(delegate { string s = Field<string>(w, "status"); return s != null && s.Contains("anteriores"); }, 8000), "no word about the missing previous file");
                    Check(!w.Comparing, "compared with nothing");
                }
                finally { MarkdownWindow.PickCompare = null; Ds.Apply(1); Shut(w); DeleteDir(dir); }
            });
            Test("w6xml", "compare-identical-and-menu", delegate
            {
                string dir = WorkDir("same");
                string a = Put(dir, "a.xml", Soap("<return>uno</return>")), b = Put(dir, "b.xml", Soap("<return>uno</return>").Replace("S:", "soap:").Replace(":S=", ":soap="));
                File.SetLastWriteTime(a, DateTime.Now.AddMinutes(-2));
                MarkdownWindow w = NewWindow();
                try
                {
                    Open(w, b);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Rows.Count > 0 && w.NeighboursKnown; }, 8000), "no tree");
                    List<MenuEntry> m = new List<MenuEntry>();
                    Call(w, "XmlExtraEntries", m, true);
                    MenuEntry prev = m.Find(delegate(MenuEntry e) { return e.Label == "Comparar con el XML anterior de la carpeta"; });
                    MenuEntry json = m.Find(delegate(MenuEntry e) { return e.Label == "Copiar como JSON"; });
                    Check(prev != null && prev.Enabled, "compare with the previous one");
                    Check(m.Exists(delegate(MenuEntry e) { return e.Label == "Comparar con otro archivo\u2026" && e.Enabled; }), "compare with another file");
                    Check(json != null && json.Enabled && json.Detail == "Todo el documento", "JSON entry");
                    Call(w, "CompareWith", new object[] { null });
                    Check(Until(delegate { return w.Comparing && !w.CompareBusy; }, 8000), "no comparison");
                    Pump(100);
                    SameV("Sin diferencias", X6StatusText(w), "status");
                    Pump(300);
                    X6BitmapSourceCheck(w, "compare-soap-identical");
                    Check(w.HeaderHost.Child is StackPanel && Flat(w.HeaderHost.Child).Contains("consultaPruebaResponse") && Flat(w.HeaderHost.Child).Contains("Comparaci\u00F3n"), "SOAP bar and compare bar: " + Flat(w.HeaderHost.Child));
                    Check(w.CompareView.Empty, "no 'identical' message");
                    Check(Flat(w.CompareView).Contains("Los dos XML son iguales"), "message text");
                    Press(w.CompareView.List, System.Windows.Input.Key.F7, System.Windows.Input.ModifierKeys.None);
                    Pump(50);
                    Check(Field<string>(w, "status") == "No hay diferencias.", "F7 with nothing to find: " + Field<string>(w, "status"));
                    m.Clear();
                    Call(w, "XmlExtraEntries", m, true);
                    MenuEntry leave = m.Find(delegate(MenuEntry e) { return e.Label != null && e.Label.StartsWith("Salir de la comparaci\u00F3n"); });
                    Check(leave != null, "no way out in the menu");
                    leave.Do();
                    Pump(50);
                    Check(!w.Comparing, "the menu entry did not leave");
                    // The first file: nothing before it in the folder.
                    Open(w, a);
                    Check(Until(delegate { return w.Tree != null && w.Tree.Rows.Count > 0 && w.NeighboursKnown; }, 8000), "first file");
                    m.Clear();
                    Call(w, "XmlExtraEntries", m, true);
                    Check(!m.Find(delegate(MenuEntry e) { return e.Label == "Comparar con el XML anterior de la carpeta"; }).Enabled, "compare with a previous file that does not exist");
                }
                finally { Shut(w); DeleteDir(dir); }
            });
        }

        static void X6BitmapSourceCheck(MarkdownWindow w, string name)
        {
            System.Windows.Media.Imaging.BitmapSource s = Snap((System.Windows.FrameworkElement)w.Content);
            SavePng(s, name + ".png");
            Check(Variety(s) > 20, name + " looks blank");
        }

        // ---- Review of the XML additions (wave 2): what the review found, fixed and kept tested

        static void R5Xml()
        {
            Test("w6xml", "r5-editor-keeps-attachment-images-like-captures", R5EditorKeepsAttachment);
            Test("w6xml", "r5-opened-documents-read-only-and-still-pruned", R5ReadOnlyDocuments);
            Test("w6xml", "r5-long-attachment-names-keep-their-extension", R5LongNames);
            Test("w6xml", "r5-attachments-in-window-read-only-and-early-refusal", R5AttachmentsInWindow);
            Test("w6xml", "r5-diff-prefixes-defaults-moves-and-namespaces", R5DiffCases);
            Test("w6xml", "r5-attachments-marked-as-from-the-internet", R5FromInternet);
            Test("w6xml", "r5-attachments-all-go-when-the-temporary-captures-do", delegate
            {
                // Stackshot's sweep of the temporary captures takes the attachments along: at start and on its timer by the
                // same rule, and all of them when every capture goes (Borrar temporales, or closing Stackshot with "hasta
                // cerrar Stackshot", where this session's attachments used to outlive the session).
                X6NeedTestData();
                R5ClearDir(XmlAttachment.Folder);
                string err;
                string fresh = XmlAttachment.WriteTemp(X6Pdf(), "r5-hoy.pdf", true, out err);
                string img = XmlAttachment.WriteTemp(X6Picture(System.Drawing.Imaging.ImageFormat.Png), "r5-hoy.png", out err);
                try
                {
                    SameV(0, XmlAttachment.Prune(1), "this session's attachments went while it lasts");
                    SameV(0, XmlAttachment.Prune(0), "fresh attachments went within the hour");
                    SameV(2, XmlAttachment.Prune(-1), "all of them");
                    Check(!File.Exists(fresh) && !File.Exists(img), "left behind");
                }
                finally { R5ClearDir(XmlAttachment.Folder); }
            });
            Test("w6xml", "r5-device-names-with-superscript-digits", delegate
            {
                // Windows reads COM and LPT with a superscript 1, 2 or 3 as devices too: a name from the message must not
                // send the attachment to a serial port.
                foreach (string dev in new string[] { "COM\u00B9", "com\u00B2", "LPT\u00B3" })
                    SameV("_" + dev + ".pdf", XmlAttachment.CleanName(dev + ".pdf"), "device name " + dev);
                SameV("COM\u00B9x.pdf", XmlAttachment.CleanName("COM\u00B9x.pdf"), "a longer name is a file");
                SameV("_COM1.pdf", XmlAttachment.CleanName("COM1.pdf"), "plain digits as before");
            });
        }

        // Opened attachments may be read-only: cleared before the folder goes.
        static void R5ClearDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            }
            catch { }
            DeleteDir(dir);
        }

        static void R5Set(object o, string field, object value)
        {
            o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
        }

        static bool R5ReadOnly(string f) { return (File.GetAttributes(f) & FileAttributes.ReadOnly) != 0; }

        static void R5Age(string f, TimeSpan by)
        {
            FileAttributes a = File.GetAttributes(f);
            File.SetAttributes(f, FileAttributes.Normal);
            File.SetLastWriteTime(f, DateTime.Now - by);
            File.SetAttributes(f, a);
        }

        // An image attachment opens in the editor from temp\adjuntos. Guardar there used to rewrite it in that folder (it
        // is pruned an hour later, so the work was lost); it is a temporary capture now: Guardar keeps a copy in the save
        // folder, like for any capture.
        static void R5EditorKeepsAttachment()
        {
            X6NeedTestData();
            string save = WorkDir("capturas");
            Directory.CreateDirectory(XmlAttachment.Folder);
            string png = Path.Combine(XmlAttachment.Folder, "r5-factura.png");
            File.WriteAllBytes(png, X6Picture(System.Drawing.Imaging.ImageFormat.Png));
            ShotStack owner = (ShotStack)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ShotStack));
            Settings s = new Settings();
            s.SaveFolder = save;
            s.SaveSubfolder = 0;
            R5Set(owner, "settings", s);
            R5Set(owner, "kept", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            R5Set(owner, "cards", new List<Card>());
            R5Set(owner, "copyRuns", new CopyRuns());
            var sync = new System.Windows.Forms.Control();
            Check(sync.Handle != IntPtr.Zero, "UI dispatcher handle");
            R5Set(owner, "sync", sync);
            Editor ed = new Editor(owner, png, ShotStack.LoadFull(png));
            try
            {
                IntPtr h = ed.Handle; // never shown
                Check(h != IntPtr.Zero && !ed.Visible, "hidden editor");
                Check((bool)typeof(Editor).GetProperty("InTemp", NP).GetValue(ed, null), "an attachment image counts as the user's own file");
                Bar.Item item = (Bar.Item)typeof(Editor).GetField("saveItem", NP).GetValue(ed);
                Check(item.Tip.StartsWith("Conservarla en tu carpeta de capturas", StringComparison.Ordinal), "Guardar tip: " + item.Tip);
                Call(ed, "KeepCopy");
                string kept = Path.Combine(save, "r5-factura.png");
                Check(Until(delegate { return File.Exists(kept); }, 5000), "Guardar did not keep a copy in the save folder");
                Check(File.Exists(png) && owner.IsKept(png), "the attachment is not marked as kept");
                // A file in another folder under temp is still the user's own (unchanged).
                Check(!Editor.IsTempCapture(Path.Combine(Settings.TempDir, "otra", "x.png"), Settings.TempDir), "a subfolder of temp counts as temp");
            }
            finally
            {
                R5Set(ed, "closeWithoutAsking", true);
                try { ed.Close(); } catch { }
                ed.Dispose();
                sync.Dispose();
                try { File.Delete(png); } catch { }
                DeleteDir(save);
            }
        }

        // PDF and Office files go to their own program from the same pruned folder: written read-only, an edit there must be
        // saved elsewhere instead of disappearing with the folder's next clean-up. The clean-up still removes them, except
        // one still open in its program (which stays read-only).
        static void R5ReadOnlyDocuments()
        {
            X6NeedTestData();
            R5ClearDir(XmlAttachment.Folder);
            string err;
            string pdf = XmlAttachment.WriteTemp(X6Pdf(), "r5-informe.pdf", true, out err);
            string png = XmlAttachment.WriteTemp(X6Picture(System.Drawing.Imaging.ImageFormat.Png), "r5-foto.png", out err);
            string busy = XmlAttachment.WriteTemp(X6Pdf(), "r5-abierto.pdf", true, out err);
            FileStream held = null;
            try
            {
                Check(pdf != null && R5ReadOnly(pdf), "a document for another program is writable");
                Check(png != null && !R5ReadOnly(png), "an image for the editor is read-only (its Guardar writes it)");
                R5Age(pdf, TimeSpan.FromHours(3));
                R5Age(busy, TimeSpan.FromHours(3));
                held = new FileStream(busy, FileMode.Open, FileAccess.Read, FileShare.Read);
                SameV(1, XmlAttachment.Prune(0), "pruned");
                Check(!File.Exists(pdf), "a read-only attachment is never pruned");
                Check(File.Exists(png), "a fresh one went");
                Check(File.Exists(busy) && R5ReadOnly(busy), "the one still open lost its protection");
                held.Dispose();
                held = null;
                SameV(1, XmlAttachment.Prune(0), "pruned once closed");
            }
            finally
            {
                if (held != null) held.Dispose();
                R5ClearDir(XmlAttachment.Folder);
            }
        }

        // The name is cut to 120 characters: a long one used to lose the end of its extension (".pd"), so the program
        // that should open it was not found.
        static void R5LongNames()
        {
            AttachType pdf = XmlAttachment.Detect(X6Pdf(), false), docx = XmlAttachment.Detect(X6ZipOf("[Content_Types].xml", "word/document.xml"), false);
            string a = XmlAttachment.FileName(pdf, new string('a', 117) + ".p");
            Check(a.EndsWith(".pdf", StringComparison.Ordinal) && a.Length <= 120, "long name: " + a);
            string b = XmlAttachment.FileName(docx, XmlAttachment.CleanName(new string('b', 150) + ".docx"));
            Check(b.EndsWith(".docx", StringComparison.Ordinal) && b.Length <= 120, "name cut by CleanName: " + b);
            string c = XmlAttachment.FileName(pdf, new string('c', 110) + ".exe.p");
            Check(c.EndsWith(".pdf", StringComparison.Ordinal) && !XmlAttachment.BlockedName(c), "dots in a long stem: " + c);
            SameV("factura.pdf", XmlAttachment.FileName(pdf, "factura.pdf"), "a short name is unchanged");
        }

        // In the window: the PDF handed to its program is read-only, the image for the editor is not; a text the viewer
        // cannot show (over 2 MB) is refused at once, without asking about the unsaved changes first; a small one opens.
        static void R5AttachmentsInWindow()
        {
            X6NeedTestData();
            string dir = WorkDir("r5adjuntos");
            StringBuilder big = new StringBuilder();
            while (big.Length < MarkdownTools.MaxBytes + 4096) big.Append("L\u00EDnea de texto del adjunto grande, sin nada raro.\n");
            byte[] bigText = Encoding.UTF8.GetBytes(big.ToString());
            string doc = "<r>\n  <pdf nombre=\"r5.pdf\">" + X6B64(X6Pdf(), true) + "</pdf>\n  <img>" + X6B64(X6Picture(System.Drawing.Imaging.ImageFormat.Png), false) + "</img>\n" +
                         "  <grande>" + X6B64(bigText, false) + "</grande>\n  <txt>" + X6B64(Encoding.UTF8.GetBytes("Texto peque\u00F1o del adjunto, con m\u00E1s de cuarenta y ocho bytes."), false) + "</txt>\n</r>\n";
            string p = Put(dir, "r5.xml", doc);
            List<string> apps = new List<string>(), edits = new List<string>();
            int asked = 0, answer = 0;
            MarkdownWindow.OpenWithApp = delegate(string f) { apps.Add(f); return true; };
            MarkdownWindow.OpenInEditor = delegate(string f) { edits.Add(f); return true; };
            DocAlert.Answer = delegate { asked++; return answer; };
            MarkdownWindow w = NewWindow();
            try
            {
                Open(w, p);
                Check(Until(delegate { return w.Tree != null && w.Tree.Model != null; }, 8000), "no tree");
                Func<string, XItem> node = delegate(string name) { return FirstNamed(w.Tree.Model.Top, name); };
                Call(w, "OnAttachment", node("pdf"), true);
                Check(Until(delegate { return apps.Count == 1; }, 8000), "PDF not opened");
                Check(R5ReadOnly(apps[0]), "the PDF handed to its program can be saved over in the pruned folder");
                Call(w, "OnAttachment", node("img"), true);
                Check(Until(delegate { return edits.Count == 1; }, 8000), "image not opened");
                Check(!R5ReadOnly(edits[0]), "the image for the editor is read-only");
                // Unsaved changes, then a text that cannot be shown: refused, nothing asked, the document kept.
                w.SetEditing(true);
                Pump(200);
                w.Editor.Apply(TextEdit.Make(w.Editor.Text.Length, 0, "\n<!-- cambio -->", 0, 0));
                Pump(100);
                int before = asked;
                Call(w, "OnAttachment", node("grande"), true);
                Check(Until(delegate { string st = Field<string>(w, "status"); return st != null && st.Contains("demasiado grande"); }, 15000), "no word about the size: " + Field<string>(w, "status"));
                string said = Field<string>(w, "status");
                Check(said.Contains("Guardar el adjunto como"), "the message does not say what to do: " + said);
                SameV(before, asked, "asked about the unsaved changes for a text that cannot be shown");
                Check(w.Doc.Path == p && w.Dirty, "the document was replaced");
                // A small one: asked (No guardar), then shown.
                answer = 1;
                Call(w, "OnAttachment", node("txt"), true);
                Check(Until(delegate { return w.Doc != null && w.Doc.Path == null && w.Text != null && w.Text.StartsWith("Texto peque\u00F1o", StringComparison.Ordinal); }, 8000), "small text attachment not shown");
                SameV(before + 1, asked, "questions");
            }
            finally
            {
                MarkdownWindow.OpenWithApp = null;
                MarkdownWindow.OpenInEditor = null;
                DocAlert.Answer = null;
                Shut(w);
                DeleteDir(dir);
                R5ClearDir(XmlAttachment.Folder);
            }
        }

        static void R5DiffCases()
        {
            // Prefixes, the default namespace and attribute order do not count.
            DiffResult r = X6DiffOf("<a:r xmlns:a=\"urn:x\" xmlns:b=\"urn:y\" b:k=\"1\" z=\"2\"><a:i>1</a:i><!-- c --></a:r>",
                                    "<r xmlns=\"urn:x\" xmlns:c=\"urn:y\" z=\"2\" c:k=\"1\"><i>1</i><!-- c --></r>");
            SameV(0, r.Total, "same document written with other prefixes: " + X6Stops(r));
            // The same prefix bound to another namespace is another attribute.
            r = X6DiffOf("<r xmlns:p=\"urn:1\" p:x=\"1\"/>", "<r xmlns:p=\"urn:2\" p:x=\"1\"/>");
            Check(r.Total == 1 && r.Changed == 1 && X6Stops(r).Contains("{p:x:null>1}") && X6Stops(r).Contains("{p:x:1>null}"), "namespace of an attribute: " + X6Stops(r));
            // An element in another namespace is another element.
            r = X6DiffOf("<r><e xmlns=\"urn:1\">v</e></r>", "<r><e xmlns=\"urn:2\">v</e></r>");
            SameV("0 changed, 1 added, 1 removed", X6Counts(r), "element namespace: " + X6Stops(r));
            // A move reads as removed and added.
            r = X6DiffOf("<r><a>1</a><b>2</b><c>3</c></r>", "<r><b>2</b><c>3</c><a>1</a></r>");
            SameV("0 changed, 1 added, 1 removed", X6Counts(r), "moved element: " + X6Stops(r));
            // Whitespace runs inside text do not count; a comment's text does.
            r = X6DiffOf("<r><t>uno  dos\n tres</t><!-- a --></r>", "<r><t>uno dos tres</t><!-- b --></r>");
            SameV("~#comment[ a > b ]", X6Stops(r), "whitespace and comments");
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle R5CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        // The Zone.Identifier stream of a file (read through Windows: .NET Framework paths take no stream names), or null.
        static string R5Zone(string path)
        {
            const uint GenericRead = 0x80000000, OpenExisting = 3;
            using (Microsoft.Win32.SafeHandles.SafeFileHandle h = R5CreateFile(path + ":Zone.Identifier", GenericRead, 1, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                using (FileStream fs = new FileStream(h, FileAccess.Read))
                using (StreamReader r = new StreamReader(fs, Encoding.ASCII)) return r.ReadToEnd();
            }
        }

        // What an attachment hands to another program, and what is saved from one, carries the mark of a file from the
        // Internet, as a mail program does: a .docx without macros can still load a remote template with them, and a
        // program saved from a message must get SmartScreen's look when it is run. Without it Office opened them as the
        // user's own (no Protected View). Images for Stackshot's own editor are left alone.
        static void R5FromInternet()
        {
            X6NeedTestData();
            R5ClearDir(XmlAttachment.Folder);
            string dir = WorkDir("r5zona");
            string err;
            string docx = XmlAttachment.WriteTemp(X6ZipOf("[Content_Types].xml", "word/document.xml"), "r5-carta.docx", true, out err);
            string png = XmlAttachment.WriteTemp(X6Picture(System.Drawing.Imaging.ImageFormat.Png), "r5-foto.png", false, out err);
            MarkdownWindow w = null;
            try
            {
                string z = R5Zone(docx);
                Check(z != null && z.Contains("[ZoneTransfer]") && z.Contains("ZoneId=3"), "a document for another program is not marked: " + (z ?? "no stream"));
                Check(R5ReadOnly(docx), "read-only");
                SameV(null, R5Zone(png), "an image for the editor is marked");
                // A file that is not there is not created by marking it.
                string ghost = Path.Combine(dir, "no-existe.pdf");
                Check(!XmlAttachment.MarkFromInternet(ghost) && !File.Exists(ghost), "marking created a file");
                // Saved from the window through Guardar el adjunto como (a PDF here: the test writes no program to disk;
                // every type goes the same way).
                byte[] pdf = X6Pdf();
                string p = Put(dir, "r5zona.xml", "<r>\n  <doc nombre=\"informe.pdf\">" + X6B64(pdf, false) + "</doc>\n</r>\n");
                string target = Path.Combine(dir, "informe.pdf");
                MarkdownWindow.PickPath = delegate(string s) { return target; };
                w = NewWindow();
                Open(w, p);
                Check(Until(delegate { return w.Tree != null && w.Tree.Model != null; }, 8000), "no tree");
                Call(w, "OnAttachment", FirstNamed(w.Tree.Model.Top, "doc"), false);
                Check(Until(delegate { string st = Field<string>(w, "status"); return st != null && st.StartsWith("Adjunto guardado"); }, 8000), "not saved: " + Field<string>(w, "status"));
                Check(X6SameBytes(pdf, File.ReadAllBytes(target)), "saved bytes");
                z = R5Zone(target);
                Check(z != null && z.Contains("ZoneId=3"), "a saved attachment is not marked: " + (z ?? "no stream"));
            }
            finally
            {
                MarkdownWindow.PickPath = null;
                if (w != null) Shut(w);
                DeleteDir(dir);
                R5ClearDir(XmlAttachment.Folder);
            }
        }
    }
}
