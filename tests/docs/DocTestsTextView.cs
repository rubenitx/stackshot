// Tests of the text viewer: files read as lines whatever their encoding and size, a log followed as it grows, and the
// window with a log, a JSON and a CSV, drawn off screen.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Stackshot.Tests
{
    static partial class DocTests
    {
        static string TextFileAt(string name, byte[] bytes)
        {
            string p = Path.Combine(WorkDir("text"), name);
            File.WriteAllBytes(p, bytes);
            return p;
        }

        static TextFile OpenText(string path)
        {
            string err;
            TextFile t = TextFile.Open(path, out err);
            Check(t != null, "open " + Path.GetFileName(path) + ": " + err);
            return t;
        }

        static void TextViewSuite()
        {
            const string S = "textview";

            Test(S, "file-encodings-and-line-ends", delegate
            {
                string text = "ca\u00F1\u00F3n\r\nsegunda\nter\u00E7era\rcuarta";
                Dictionary<string, byte[]> cases = new Dictionary<string, byte[]>();
                cases["utf8"] = new UTF8Encoding(false).GetBytes(text);
                cases["utf8bom"] = Concat(new byte[] { 0xEF, 0xBB, 0xBF }, new UTF8Encoding(false).GetBytes(text));
                cases["utf16le"] = Concat(new byte[] { 0xFF, 0xFE }, Encoding.Unicode.GetBytes(text));
                cases["utf16be"] = Concat(new byte[] { 0xFE, 0xFF }, Encoding.BigEndianUnicode.GetBytes(text));
                cases["utf16nobom"] = Encoding.Unicode.GetBytes(text);
                cases["cp1252"] = Encoding.GetEncoding(1252).GetBytes(text);
                foreach (KeyValuePair<string, byte[]> c in cases)
                    using (TextFile t = OpenText(TextFileAt(c.Key + ".txt", c.Value)))
                    {
                        Check(t.Count == 4, c.Key + " lines " + t.Count);
                        Same("ca\u00F1\u00F3n", t.Line(0), c.Key + " line 1");
                        Same("ter\u00E7era", t.Line(2), c.Key + " line 3");
                        Same("cuarta", t.Line(3), c.Key + " last line");
                    }
                using (TextFile t = OpenText(TextFileAt("mixed.txt", Encoding.ASCII.GetBytes("a\r\nb\nc\r\n"))))
                    Check(t.NewlineName.StartsWith("CRLF") && t.NewlineName.Contains("mezclado"), "mixed newlines named: " + t.NewlineName);
            });

            Test(S, "big-log-indexed-in-the-background", delegate
            {
                // About 40 MB, past what is kept whole: lines are found on disk as they are needed.
                string path = Path.Combine(WorkDir("text"), "grande.log");
                using (StreamWriter w = new StreamWriter(path, false, new UTF8Encoding(false)))
                    for (int i = 0; i < 420000; i++) w.Write("2026-10-20 12:00:" + (i % 60).ToString("D2") + ".123 INFO  [main] l\u00EDnea n\u00FAmero " + i + " con algo de texto para pesar\r\n");
                using (TextFile t = OpenText(path))
                {
                    Check(!t.Whole, "not kept whole");
                    Check(Until(delegate { return t.Done; }, 30000), "indexed");
                    Check(t.Count == 420001 || t.Count == 420000, "lines " + t.Count);
                    Check(t.Line(0).StartsWith("2026-10-20 12:00:00.123 INFO"), "first: " + t.Line(0));
                    Check(t.Line(419999).Contains("n\u00FAmero 419999"), "last: " + t.Line(419999));
                    Check(t.Line(210000).Contains("n\u00FAmero 210000"), "middle");
                    Put("lines", t.Count);
                }
            });

            Test(S, "follow-a-growing-log", delegate
            {
                string path = Path.Combine(WorkDir("text"), "vivo.log");
                File.WriteAllText(path, "uno\r\ndos\r\n");
                using (TextFile t = OpenText(path))
                {
                    int before = t.Count;
                    using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { byte[] b = Encoding.ASCII.GetBytes("tres\r\ncuatro\r\n"); fs.Write(b, 0, b.Length); }
                    Check(t.Refresh(), "noticed it grew");
                    Check(t.Count == before + 2, "two more lines: " + before + " -> " + t.Count);
                    Same("cuatro", t.Line(3), "new last line");
                    File.WriteAllText(path, "nuevo\r\n");
                    Check(t.Refresh(), "noticed it was replaced");
                    Same("nuevo", t.Line(0), "read again from the start");
                }
            });

            Test(S, "window-shows-a-log-with-levels-and-filter", delegate
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 300; i++)
                {
                    string lvl = i % 50 == 7 ? "ERROR" : i % 20 == 3 ? "WARN " : "INFO ";
                    sb.Append("2026-10-20 10:" + (i / 60).ToString("D2") + ":" + (i % 60).ToString("D2") + " " + lvl + " Servicio de pruebas: paso " + i + "\r\n");
                    if (i % 50 == 7) sb.Append("   at Prueba.Metodo() in C:\\x.cs:line 12\r\n");
                }
                string path = TextFileAt("servicio.log", new UTF8Encoding(false).GetBytes(sb.ToString()));
                TextWindow w = NewTextWindow(path);
                try
                {
                    Check(w.File.Count >= 300, "lines " + w.File.Count);
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "text-log.png");
                    Call(w, "SetLevel", LogLevel.Error);
                    Check(Until(delegate { return w.Shown < 50; }, 5000), "only errors (and their stack lines): " + w.Shown);
                    Check(w.Shown == 12, "6 errors with one continuation each: " + w.Shown);
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "text-log-errors.png");
                    Call(w, "SetLevel", LogLevel.None);
                    Check(Until(delegate { return w.Shown >= 300; }, 5000), "all again");
                }
                finally { w.Close(); Pump(100); }
            });

            Test(S, "window-shows-json-as-a-tree", delegate
            {
                string json = "{\"cliente\":{\"nombre\":\"Ana\",\"pedidos\":[{\"id\":1,\"total\":12.5},{\"id\":2,\"total\":7}]},\"activo\":true,\"nota\":null}";
                string path = TextFileAt("pedido.json", new UTF8Encoding(false).GetBytes(json));
                TextWindow w = NewTextWindow(path);
                try
                {
                    Check(w.JsonRoot != null, "parsed");
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "text-json-tree.png");
                    Check(w.View != null, "text view");
                }
                finally { w.Close(); Pump(100); }
                string bad = TextFileAt("roto.json", Encoding.ASCII.GetBytes("{\n  \"a\": 1,\n  \"b\": 2,\n}\n"));
                TextWindow w2 = NewTextWindow(bad);
                try
                {
                    Check(w2.JsonRoot == null, "broken JSON has no tree");
                    SavePng(Snap(w2.Content as System.Windows.FrameworkElement), "text-json-error.png");
                }
                finally { w2.Close(); Pump(100); }
            });

            Test(S, "window-shows-csv-as-a-table", delegate
            {
                string csv = "Fecha;Concepto;Importe\r\n01/10/2026;Caf\u00E9;1,50\r\n02/10/2026;\"Comida; men\u00FA\";12,90\r\n03/10/2026;Libro;23,00\r\n";
                string path = TextFileAt("gastos.csv", new UTF8Encoding(true).GetBytes(csv));
                TextWindow w = NewTextWindow(path);
                try
                {
                    Check(w.Table != null && w.Table.Delimiter == ';' && w.Table.HasHeader && w.Table.Columns == 3, "a table with ';' and a header");
                    Same("Comida; men\u00FA", w.Table.Rows[2][1], "quoted cell with the delimiter inside");
                    SavePng(Snap(w.Content as System.Windows.FrameworkElement), "text-csv.png");
                }
                finally { w.Close(); Pump(100); }
            });
        }

        static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] o = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, o, 0, a.Length);
            Buffer.BlockCopy(b, 0, o, a.Length, b.Length);
            return o;
        }

        static TextWindow NewTextWindow(string path)
        {
            ConstructorInfo ci = typeof(TextWindow).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
            TextWindow w = (TextWindow)ci.Invoke(new object[] { null });
            w.Width = 1000;
            w.Height = 700;
            Park(w);
            Pump(100);
            typeof(TextWindow).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(w, new object[] { path });
            Check(Until(delegate { return w.File != null && !w.Loading; }, 15000), "did not load " + Path.GetFileName(path));
            Pump(300);
            return w;
        }
    }
}
