// Tests of the readers behind the text viewers: CSV/TSV tables (quotes, delimiter, header, numbers, sorting), JSON
// (iterative parser, errors, writing, paths, JSON Lines) and log lines (levels, stack traces, timestamps).
using System;
using System.Collections.Generic;
using System.Text;

namespace Stackshot.Tests
{
    static partial class DocTests
    {
        static CsvTable TdCsv(string text, int maxRows)
        {
            string err;
            CsvTable t = CsvTable.Parse(text, maxRows, out err);
            Check(err == null, "unexpected error: " + err);
            return t;
        }

        static string TdRow(CsvTable t, int r)
        {
            return string.Join("|", t.Rows[r]);
        }

        static string TdOrder(List<int> order)
        {
            return string.Join(",", order);
        }

        static void TdNum(string s, double want)
        {
            Check(CsvTable.IsNumber(s), "not a number: \"" + s + "\"");
            double got = CsvTable.NumberValue(s);
            Check(Math.Abs(got - want) < 1e-9, "\"" + s + "\" is " + got + ", wanted " + want);
        }

        static JsonNode TdJson(string text)
        {
            string err;
            int ln, col;
            JsonNode n = JsonDoc.Parse(text, out err, out ln, out col);
            Check(n != null && err == null, "parse failed: " + err + " at " + ln + ":" + col);
            return n;
        }

        static string TdJsonError(string text, out int ln, out int col)
        {
            string err;
            JsonNode n = JsonDoc.Parse(text, out err, out ln, out col);
            Check(n == null && err != null, "expected an error for: " + text.Replace("\n", "\\n"));
            return err;
        }

        static void TdLevels(string[] lines, LogLevel[] want)
        {
            Check(lines.Length == want.Length, "table sizes differ");
            StringBuilder bad = new StringBuilder();
            for (int k = 0; k < lines.Length; k++)
            {
                LogLevel got = LogLines.LevelOf(lines[k]);
                if (got != want[k]) bad.Append(" [").Append(lines[k]).Append(" => ").Append(got).Append(", wanted ").Append(want[k]).Append("]");
            }
            Check(bad.Length == 0, "levels:" + bad);
        }

        static void TextDataSuite()
        {
            const string S = "textdata";

            // ---- CSV / TSV

            Test(S, "csv-quotes-and-embedded-newlines", delegate
            {
                string text = "name,note\r\n\"Ana\",\"dijo \"\"hola\"\"\"\r\n\"Luis\",\"l1\nl2, con coma\"\r\nZe,fin\r\n";
                CsvTable t = TdCsv(text, 100);
                Check(t.Delimiter == ',' && t.Columns == 2 && t.Rows.Count == 4, "shape: " + t.Rows.Count + " rows, " + t.Columns + " columns");
                Same("dijo \"hola\"", t.Rows[1][1], "doubled quotes");
                Same("l1\nl2, con coma", t.Rows[2][1], "line break and delimiter inside quotes");
                Same("Ze|fin", TdRow(t, 3), "the row after the multi-line one");
                Same("0,1,2,4", TdOrder(t.RowLines), "row start lines");

                CsvTable e = TdCsv("\"\"\"a\"\"\",b\n", 10);
                Same("\"a\"|b", TdRow(e, 0), "quotes doubled at both edges of a field");
                CsvTable crlf = TdCsv("\"x\r\ny\",z\r\nq,r", 10);
                Same("x\r\ny", crlf.Rows[0][0], "CRLF inside quotes stays as written");
                Same("0,2", TdOrder(crlf.RowLines), "a CRLF inside quotes is one line");
            });

            Test(S, "csv-semicolon-with-decimal-commas", delegate
            {
                CsvTable t = TdCsv("producto;precio;cantidad\nlapiz;1,5;10\ngoma;2,75;3\n", 100);
                Check(t.Delimiter == ';' && t.Columns == 3 && t.Rows.Count == 3, "shape");
                Same("1,5", t.Rows[1][1], "the decimal comma stays in its cell");
                Check(t.HasHeader, "header with numbers below");
                Check(CsvTable.Detect("1,5;2,75") == ';', "a lone line of decimal commas");
                Check(CsvTable.Detect("1,5;2,75\n3,25;4,5\n") == ';', "two lines of decimal commas");
            });

            Test(S, "csv-detect-forms", delegate
            {
                Check(CsvTable.Detect("a,b\n1,2") == ',', "comma");
                Check(CsvTable.Detect("a;b\n1;2") == ';', "semicolon");
                Check(CsvTable.Detect("a\tb\n1\t2") == '\t', "tab");
                Check(CsvTable.Detect("a|b|c\n1|2|3") == '|', "pipe");
                Check(CsvTable.Detect("") == ',' && CsvTable.Detect("hola") == ',', "nothing to split falls back to the comma");
                Check(CsvTable.Detect("Nombre;Importe\nAna;1,5\nLuis;2,25\n") == ';', "consistency beats the commas");
                Check(CsvTable.Detect("name,desc\nAna,x;y\nLuis,z;w\n") == ',', "a header makes the comma the consistent one");
                Check(CsvTable.Detect("Ana,x;y\nLuis,z;w\n") == ',', "a tie goes to the comma when the commas are not decimal");
                Check(CsvTable.Detect("n\tnote\nA\tx, y\nB\tz, w") == '\t', "tab wins over commas inside the text");
                Check(CsvTable.Detect("\"a;b\";\"c;d\"\n\"e;f\";\"g;h\"") == ';', "quotes hide their own delimiters");
                CsvTable q = TdCsv("\"a;b\";\"c;d\"\n\"e;f\";\"g;h\"", 10);
                Same("a;b|c;d", TdRow(q, 0), "quoted cells keep the other delimiter");
            });

            Test(S, "csv-parsewith-forces-the-delimiter", delegate
            {
                string text = "a;b;c\n1;2;3\n";
                string err;
                CsvTable auto = CsvTable.Parse(text, 100, out err);
                Check(err == null && auto.Delimiter == ';' && auto.Columns == 3, "detected: " + auto.Delimiter + " " + auto.Columns);
                CsvTable forced = CsvTable.ParseWith(text, ',', 100, out err);
                Check(err == null && forced.Delimiter == ',' && forced.Columns == 1 && forced.Rows.Count == 2, "comma forced: " + forced.Columns + " column(s), " + forced.Rows.Count + " rows");
                Same("a;b;c", forced.Rows[0][0], "the whole line is one cell");
                Same("1;2;3", forced.Rows[1][0], "second line too");
                CsvTable same = CsvTable.ParseWith(text, ';', 100, out err);
                Same(TdRow(auto, 1), TdRow(same, 1), "forcing the detected delimiter gives the same rows");
                CsvTable tab = CsvTable.ParseWith("x\ty;z\n1\t2;3", '\t', 100, out err);
                Check(tab.Delimiter == '\t' && tab.Columns == 2, "tab forced");
                Same("x|y;z", TdRow(tab, 0), "other delimiters stay inside the cells");
                CsvTable quoted = CsvTable.ParseWith("\"a;b\",c\n", ',', 100, out err);
                Same("a;b|c", TdRow(quoted, 0), "quotes still work");
                CsvTable limited = CsvTable.ParseWith("a,b\n1,2\n3,4\n", ',', 2, out err);
                Check(limited.Rows.Count == 2 && limited.Truncated, "row limit");
                CsvTable.ParseWith("a;\"open", ';', 100, out err);
                Same("Hay unas comillas sin cerrar al final del archivo.", err ?? "(null)", "unterminated quote with a forced delimiter");
                CsvTable none = CsvTable.ParseWith("", '|', 100, out err);
                Check(err == null && none.Delimiter == '|' && none.Rows.Count == 0, "empty text keeps the forced delimiter");
                CsvTable bom = CsvTable.ParseWith("\uFEFFa;b", ';', 100, out err);
                Same("a|b", TdRow(bom, 0), "the BOM is skipped");
            });

            Test(S, "csv-ragged-padding", delegate
            {
                CsvTable t = TdCsv("a,b,c\n1\n1,2,3,4\n", 100);
                Check(t.Columns == 4 && t.Rows.Count == 3, "shape");
                for (int r = 0; r < t.Rows.Count; r++) Check(t.Rows[r].Length == 4, "row " + r + " has " + t.Rows[r].Length + " cells");
                Same("1|||", TdRow(t, 1), "short row padded with empty cells");
                Same("a|b|c|", TdRow(t, 0), "first row padded too");
            });

            Test(S, "csv-bom-empty-and-lone-cr", delegate
            {
                CsvTable t = TdCsv("\uFEFFa,b\n1,2", 100);
                Same("a|b", TdRow(t, 0), "the BOM is not part of the first cell");
                Check(t.Delimiter == ',', "delimiter with a BOM");

                CsvTable none = TdCsv("", 100);
                Check(none.Rows.Count == 0 && none.Columns == 0 && !none.HasHeader && !none.Truncated, "empty text is an empty table");
                CsvTable bomOnly = TdCsv("\uFEFF", 100);
                Check(bomOnly.Rows.Count == 0, "a BOM alone has no rows");

                CsvTable cr = TdCsv("a, b ,c\r1,2,3\r", 100);
                Check(cr.Rows.Count == 2, "a lone CR ends a row: " + cr.Rows.Count);
                Same(" b ", cr.Rows[0][1], "cells are not trimmed");
                Same("0,1", TdOrder(cr.RowLines), "row lines with CR");
            });

            Test(S, "csv-unterminated-quote", delegate
            {
                string err;
                CsvTable t = CsvTable.Parse("a,b\n1,\"sin cerrar\n2,3", 100, out err);
                Check(t != null, "a table comes back anyway");
                Same("Hay unas comillas sin cerrar al final del archivo.", err ?? "(null)", "message");
                Check(t.Rows.Count == 2, "rows kept: " + t.Rows.Count);
                Same("sin cerrar\n2,3", t.Rows[1][1], "what was read stays in the last cell");
                string fine;
                CsvTable.Parse("a,b\n1,\"cerrada\"", 100, out fine);
                Check(fine == null, "no error when quotes close");
            });

            Test(S, "csv-blank-lines-and-max-rows", delegate
            {
                CsvTable t = TdCsv("a,b\n\n1,2\n\n\n", 100);
                Check(t.Rows.Count == 3, "inner blank line kept, trailing ones dropped: " + t.Rows.Count);
                Same("|", TdRow(t, 1), "blank row padded");
                Same("0,1,2", TdOrder(t.RowLines), "row lines count the blank line");

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 10; i++) sb.Append("r").Append(i).Append(",").Append(i).Append("\n");
                CsvTable cut = TdCsv(sb.ToString(), 4);
                Check(cut.Rows.Count == 4 && cut.Truncated, "cut at 4 rows: " + cut.Rows.Count + " " + cut.Truncated);
                Same("r3|3", TdRow(cut, 3), "the last kept row");
                CsvTable exact = TdCsv(sb.ToString(), 10);
                Check(exact.Rows.Count == 10 && !exact.Truncated, "exactly the limit is not truncated");
                CsvTable all = TdCsv(sb.ToString(), 0);
                Check(all.Rows.Count == 10 && !all.Truncated, "no limit");
            });

            Test(S, "csv-header-heuristic", delegate
            {
                Check(TdCsv("nombre,edad\nAna,30\nLuis,25", 100).HasHeader, "names above numbers");
                Check(TdCsv("Ciudad,Pa\u00EDs\nMadrid,Espa\u00F1a\nParis,Francia", 100).HasHeader, "text only, distinct and different from row two");
                Check(TdCsv("id;nombre\n1;Ana", 100).HasHeader, "semicolon table with a header");
                Check(!TdCsv("Ana,30\nLuis,25", 100).HasHeader, "a number in the first row");
                Check(!TdCsv("1,2\n3,4", 100).HasHeader, "only numbers");
                Check(!TdCsv("Ana,Madrid\nLuis,Madrid", 100).HasHeader, "second row repeats a first-row value");
                Check(!TdCsv("x,x\ny,z", 100).HasHeader, "repeated names");
                Check(!TdCsv("a,\nb,c", 100).HasHeader, "an empty cell in the first row");
                Check(!TdCsv("nombre,edad", 100).HasHeader, "a single row");
            });

            Test(S, "csv-isnumber", delegate
            {
                string[] yes = { "12", "-3.5", "1,5", "1.234,56", "1,234.56", "3e5", "1E-3", "50 %", "12,50 \u20AC", "+7", ".5",
                                 "1.234.567", "1,234,567", "  42  ", "-0", "$12.50", "\u20AC 3,5", "0007", "-$5" };
                for (int k = 0; k < yes.Length; k++) Check(CsvTable.IsNumber(yes[k]), "should be a number: \"" + yes[k] + "\"");
                string[] no = { "", "   ", "abc", "12abc", "1.2.3", "1,2,3", "2026-10-20", "e5", "1e", "NaN", "Infinity", "--5", "1.", "0x1F", "%", "-", "1.234,56,7" };
                for (int k = 0; k < no.Length; k++) Check(!CsvTable.IsNumber(no[k]), "should not be a number: \"" + no[k] + "\"");
                Check(!CsvTable.IsNumber(null), "null");
            });

            Test(S, "csv-numbervalue", delegate
            {
                TdNum("1.234,56", 1234.56);
                TdNum("1,234.56", 1234.56);
                TdNum("1,5", 1.5);
                TdNum("-3.5", -3.5);
                TdNum("3e5", 300000);
                TdNum("1E-3", 0.001);
                TdNum("50 %", 50);
                TdNum("12,50 \u20AC", 12.5);
                TdNum("1.234.567", 1234567);
                TdNum("-$5", -5);
                TdNum(".5", 0.5);
                Check(double.IsNaN(CsvTable.NumberValue("abc")), "text is NaN");
                Check(double.IsNaN(CsvTable.NumberValue("")), "empty is NaN");
                Check(double.IsNaN(CsvTable.NumberValue(null)), "null is NaN");
            });

            Test(S, "csv-sorted-order", delegate
            {
                CsvTable t = TdCsv("n,v\nb,10\na,9\nc,\nd,100\ne,x\nf,2\n", 100);
                Check(t.HasHeader && t.Rows.Count == 7, "table shape");
                Same("6,2,1,4,5,3", TdOrder(CsvTable.SortedOrder(t, 1, false)), "ascending: numbers numerically, then text, empty last");
                Same("5,4,1,2,6,3", TdOrder(CsvTable.SortedOrder(t, 1, true)), "descending: empty still last");
                Same("2,1,3,4,5,6", TdOrder(CsvTable.SortedOrder(t, 0, false)), "text column");
                Same("1,2,3,4,5,6", TdOrder(CsvTable.SortedOrder(t, 5, false)), "a column that does not exist keeps the order");
                Same("1,2,3,4,5,6", TdOrder(CsvTable.SortedOrder(t, -1, true)), "a negative column keeps the order");

                CsvTable plain = TdCsv("3\n1\n2", 100);
                Check(!plain.HasHeader, "numbers only: no header");
                Same("1,2,0", TdOrder(CsvTable.SortedOrder(plain, 0, false)), "no header row to leave out");
                Same("0,2,1", TdOrder(CsvTable.SortedOrder(plain, 0, true)), "descending without header");
            });

            Test(S, "csv-sorted-order-culture-and-stability", delegate
            {
                CsvTable t = TdCsv("k,v\nb,1\n\u00E1,2\nB,3\na,4\nA,5\n", 100);
                Check(t.HasHeader, "header");
                Same("4,5,2,1,3", TdOrder(CsvTable.SortedOrder(t, 0, false)), "case ignored, accent after the plain letter, equal values keep their order");
                Same("1,3,2,4,5", TdOrder(CsvTable.SortedOrder(t, 0, true)), "descending keeps equal values in their order");
            });

            // ---- JSON

            Test(S, "json-valid-nested-positions", delegate
            {
                string src = "{\n  \"name\": \"Ana\",\n  \"age\": 30,\n  \"tags\": [\"a\", \"b\"],\n  \"ok\": true,\n  \"none\": null,\n  \"pt\": {\"x\": 1.5e2, \"y\": -0}\n}";
                JsonNode root = TdJson(src);
                Check(root.Kind == JsonKind.Object && root.Children.Count == 6 && root.Key == null && root.Index == -1 && root.Parent == null, "root");
                Check(root.Line == 1 && root.Col == 1 && root.EndLine == 8, "root span: " + root.Line + ":" + root.Col + "-" + root.EndLine);
                JsonNode name = root.Children[0];
                Check(name.Key == "name" && name.Kind == JsonKind.String && name.Value == "Ana" && name.Line == 2 && name.Col == 11 && name.Parent == root, "name member");
                JsonNode age = root.Children[1];
                Check(age.Kind == JsonKind.Number && age.Value == "30", "number");
                JsonNode tags = root.Children[2];
                Check(tags.Kind == JsonKind.Array && tags.Children.Count == 2 && tags.Line == 4 && tags.Col == 11 && tags.EndLine == 4, "array span");
                Check(tags.Children[1].Index == 1 && tags.Children[1].Value == "b" && tags.Children[1].Key == null && tags.Children[1].Parent == tags, "array item");
                Check(root.Children[3].Kind == JsonKind.True && root.Children[3].Value == "true", "true");
                Check(root.Children[4].Kind == JsonKind.Null && root.Children[4].Value == "null", "null");
                JsonNode pt = root.Children[5];
                Check(pt.Kind == JsonKind.Object && pt.Line == 7 && pt.EndLine == 7, "inner object span");
                Check(pt.Children[0].Value == "1.5e2" && pt.Children[1].Value == "-0", "numbers as written");
                Check(JsonDoc.Count(root) == 11, "node count: " + JsonDoc.Count(root));

                Check(TdJson("5").Kind == JsonKind.Number && TdJson(" \"x\" ").Value == "x" && TdJson("null").Kind == JsonKind.Null && TdJson("false").Kind == JsonKind.False, "scalar roots");
                Check(TdJson("[ ]").Children.Count == 0 && TdJson("{ }").Children.Count == 0, "empty containers");
                Check(TdJson("[\"//not a comment\", \"/* nor this */\"]").Children.Count == 2, "comment-like text inside strings");
            });

            Test(S, "json-compact-exact", delegate
            {
                string src = "{\n  \"name\": \"Ana\",\n  \"age\": 30,\n  \"tags\": [\"a\", \"b\"],\n  \"ok\": true,\n  \"none\": null,\n  \"pt\": {\"x\": 1.5e2, \"y\": -0}\n}";
                Same("{\"name\":\"Ana\",\"age\":30,\"tags\":[\"a\",\"b\"],\"ok\":true,\"none\":null,\"pt\":{\"x\":1.5e2,\"y\":-0}}", JsonDoc.Compact(TdJson(src)), "compact");
                Same("", JsonDoc.Compact(null), "null root");
            });

            Test(S, "json-pretty-exact-and-roundtrip", delegate
            {
                string src = "{\"a\":[1,2,{\"b\":null}],\"c\":{},\"d\":[],\"e\":\"x\"}";
                JsonNode root = TdJson(src);
                Same("{\n  \"a\": [\n    1,\n    2,\n    {\n      \"b\": null\n    }\n  ],\n  \"c\": {},\n  \"d\": [],\n  \"e\": \"x\"\n}", JsonDoc.Pretty(root, 2), "indent 2");
                string p4 = JsonDoc.Pretty(root, 4);
                Check(p4.StartsWith("{\n    \"a\": [\n        1,\n", StringComparison.Ordinal), "indent 4 starts as expected");
                Same(JsonDoc.Compact(root), JsonDoc.Compact(TdJson(p4)), "Parse(Pretty) gives the same Compact");
                Same("[]", JsonDoc.Pretty(TdJson("[]"), 2), "empty array");
                Same("{}", JsonDoc.Pretty(TdJson("{}"), 2), "empty object");
                Same("[\n  [],\n  {}\n]", JsonDoc.Pretty(TdJson("[[],{}]"), 2), "empty containers inside");
                Same("5", JsonDoc.Pretty(TdJson("5"), 2), "a scalar root");
                Same("", JsonDoc.Pretty(null, 2), "null root");
            });

            Test(S, "json-escapes-and-surrogates", delegate
            {
                string src = @"[""a\tb"",""\u00e9"",""\ud83d\ude00"",""\/"",""\\"",""q\"""",""\u0001"",""\u00E9\u00f1""]";
                JsonNode root = TdJson(src);
                Check(root.Children.Count == 8, "items: " + root.Children.Count);
                Same("a\tb", root.Children[0].Value, "tab");
                Same("\u00E9", root.Children[1].Value, "BMP escape");
                string pair = root.Children[2].Value;
                Check(pair.Length == 2 && char.ConvertToUtf32(pair, 0) == 0x1F600, "surrogate pair decodes to U+1F600");
                Same("/", root.Children[3].Value, "escaped slash");
                Same("\\", root.Children[4].Value, "escaped backslash");
                Same("q\"", root.Children[5].Value, "escaped quote");
                Same("\u0001", root.Children[6].Value, "control character");
                Same("\u00E9\u00F1", root.Children[7].Value, "two escapes, mixed case hex");
                Same("[\"a\\tb\",\"\u00E9\",\"\uD83D\uDE00\",\"/\",\"\\\\\",\"q\\\"\",\"\\u0001\",\"\u00E9\u00F1\"]", JsonDoc.Compact(root), "minimal re-escaping, non-ASCII kept");

                int ln, col;
                string err = TdJsonError(@"[""\x""]", out ln, out col);
                Check(ln == 1 && col == 3, "bad escape position " + ln + ":" + col + " (" + err + ")");
                TdJsonError(@"[""\u12G4""]", out ln, out col);
                TdJsonError(@"[""\u12""]", out ln, out col);
                err = TdJsonError("[\"abc", out ln, out col);
                Check(ln == 1 && col == 2, "unterminated string points at its quote: " + ln + ":" + col + " (" + err + ")");
                TdJsonError("[\"a\nb\"]", out ln, out col);
            });

            Test(S, "json-deep-nesting-10000", delegate
            {
                const int D = 10000;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < D; i++) sb.Append('[');
                sb.Append('1');
                for (int i = 0; i < D; i++) sb.Append(']');
                string arr = sb.ToString();
                JsonNode root = TdJson(arr);
                Check(JsonDoc.Count(root) == D + 1, "array count " + JsonDoc.Count(root));
                JsonNode n = root;
                int depth = 0;
                while (n.Kind == JsonKind.Array) { n = n.Children[0]; depth++; }
                Check(depth == D && n.Kind == JsonKind.Number, "array depth " + depth);
                string path = JsonDoc.PathOf(n);
                Check(path.Length == 1 + 3 * D, "array path length " + path.Length);
                Same("$[0][0]", path.Substring(0, 7), "array path start");
                Same(arr, JsonDoc.Compact(root), "array compact");

                StringBuilder ob = new StringBuilder();
                for (int i = 0; i < D; i++) ob.Append("{\"a\":");
                ob.Append('1');
                for (int i = 0; i < D; i++) ob.Append('}');
                string obj = ob.ToString();
                JsonNode oroot = TdJson(obj);
                Check(JsonDoc.Count(oroot) == D + 1, "object count");
                JsonNode m = oroot;
                int od = 0;
                while (m.Kind == JsonKind.Object) { m = m.Children[0]; od++; }
                Check(od == D, "object depth " + od);
                Check(JsonDoc.PathOf(m).Length == 1 + 2 * D, "object path length");
                Same(obj, JsonDoc.Compact(oroot), "object compact");

                string pretty = JsonDoc.Pretty(root, 2);
                Check(pretty.Length < 20000000, "pretty output stays bounded: " + pretty.Length);
                Same(arr, JsonDoc.Compact(TdJson(pretty)), "pretty text of the deep array parses back");
            });

            Test(S, "json-error-missing-comma-position", delegate
            {
                int ln, col;
                string err = TdJsonError("{\n  \"a\": 1\n  \"b\": 2\n}", out ln, out col);
                Same("Falta una coma o un cierre", err, "message");
                Check(ln == 3 && col == 3, "position " + ln + ":" + col);
                err = TdJsonError("[1 2]", out ln, out col);
                Same("Falta una coma o un cierre", err, "message on one line");
                Check(ln == 1 && col == 4, "position " + ln + ":" + col);
                err = TdJsonError("{\"a\":[1,2", out ln, out col);
                Check(err.StartsWith("Falta cerrar", StringComparison.Ordinal) && ln == 1 && col == 6, "unclosed points at its opening bracket: " + ln + ":" + col + " " + err);
                err = TdJsonError("[1}", out ln, out col);
                Same("Aqu\u00ED deber\u00EDa haber \u00AB]\u00BB", err, "wrong closer");
                Check(ln == 1 && col == 3, "wrong closer position " + ln + ":" + col);
            });

            Test(S, "json-trailing-comma-messages", delegate
            {
                int ln, col;
                string err = TdJsonError("[1,2,]", out ln, out col);
                Same("Coma de m\u00E1s antes de \u00AB]\u00BB", err, "array");
                Check(ln == 1 && col == 5, "array comma position " + ln + ":" + col);
                err = TdJsonError("{\"a\":1,}", out ln, out col);
                Same("Coma de m\u00E1s antes de \u00AB}\u00BB", err, "object");
                Check(ln == 1 && col == 7, "object comma position " + ln + ":" + col);
                err = TdJsonError("[\n  1,\n  2,\n]", out ln, out col);
                Same("Coma de m\u00E1s antes de \u00AB]\u00BB", err, "multi-line array");
                Check(ln == 3 && col == 4, "multi-line comma position " + ln + ":" + col);
            });

            Test(S, "json-comments-message", delegate
            {
                int ln, col;
                string err = TdJsonError("{\"a\":1 // x\n}", out ln, out col);
                Same("Los comentarios no son JSON v\u00E1lido", err, "line comment after a value");
                Check(ln == 1 && col == 8, "position " + ln + ":" + col);
                err = TdJsonError("/* c */ [1]", out ln, out col);
                Same("Los comentarios no son JSON v\u00E1lido", err, "block comment first");
                Check(ln == 1 && col == 1, "position " + ln + ":" + col);
                err = TdJsonError("[1, // x\n2]", out ln, out col);
                Same("Los comentarios no son JSON v\u00E1lido", err, "comment where a value is expected");
                Check(ln == 1 && col == 5, "position " + ln + ":" + col);
            });

            Test(S, "json-other-errors", delegate
            {
                int ln, col;
                TdJsonError("", out ln, out col);
                TdJsonError("  \n ", out ln, out col);
                string err = TdJsonError("[1] x", out ln, out col);
                Same("Hay texto de m\u00E1s despu\u00E9s del valor", err, "text after the value");
                Check(ln == 1 && col == 5, "position " + ln + ":" + col);
                TdJsonError("{\"a\":1}}", out ln, out col);
                err = TdJsonError("{a:1}", out ln, out col);
                Same("Los nombres van entre comillas dobles", err, "bare name");
                Check(ln == 1 && col == 2, "bare name position");
                err = TdJsonError("['a']", out ln, out col);
                Same("Las cadenas van entre comillas dobles", err, "single quotes");
                Check(ln == 1 && col == 2, "single quote position");
                err = TdJsonError("[1,,2]", out ln, out col);
                Same("Falta un valor antes de \u00AB,\u00BB", err, "double comma");
                Check(ln == 1 && col == 4, "double comma position");
                err = TdJsonError("{\"a\" 1}", out ln, out col);
                Same("Falta \u00AB:\u00BB despu\u00E9s del nombre", err, "missing colon");
                Check(ln == 1 && col == 6, "missing colon position");
                string[] bad = { "[01]", "[1.]", "[-]", "[1e]", "[+1]", "[.5]", "[NaN]", "[tru]", "[truee]", "{\"a\":}", "[1,]", "{,}", "[}", "\"abc\"x" };
                for (int k = 0; k < bad.Length; k++) TdJsonError(bad[k], out ln, out col);
            });

            Test(S, "json-bom-and-line-breaks", delegate
            {
                JsonNode root = TdJson("\uFEFF  \r\n [ 1 , 2 ] \r\n ");
                Check(root.Kind == JsonKind.Array && root.Children.Count == 2, "array");
                Check(root.Line == 2 && root.Col == 2 && root.EndLine == 2, "root position " + root.Line + ":" + root.Col);
                Check(root.Children[0].Col == 4 && root.Children[1].Col == 8, "item columns");

                int ln, col;
                string err = TdJsonError("[1,\r2,\r3 4]", out ln, out col);
                Same("Falta una coma o un cierre", err, "message");
                Check(ln == 3 && col == 3, "a lone CR is a line break: " + ln + ":" + col);
                TdJsonError("\uFEFF[1,\r\n2,\r\n3 4]", out ln, out col);
                Check(ln == 3 && col == 3, "CRLF after a BOM: " + ln + ":" + col);
            });

            Test(S, "json-path-of-odd-names", delegate
            {
                JsonNode root = TdJson("{\"name\":\"x\",\"odd name\":{\"a-b\":[10,{\"id\":7}]},\"\":1,\"q\\\"t\":2,\"_ok\":3,\"1st\":4}");
                Same("$", JsonDoc.PathOf(root), "root");
                Same("$.name", JsonDoc.PathOf(root.Children[0]), "plain name");
                JsonNode odd = root.Children[1];
                Same("$[\"odd name\"]", JsonDoc.PathOf(odd), "name with a space");
                JsonNode list = odd.Children[0];
                Same("$[\"odd name\"][\"a-b\"]", JsonDoc.PathOf(list), "name with a dash");
                Same("$[\"odd name\"][\"a-b\"][0]", JsonDoc.PathOf(list.Children[0]), "array item");
                Same("$[\"odd name\"][\"a-b\"][1].id", JsonDoc.PathOf(list.Children[1].Children[0]), "member of an item");
                Same("$[\"\"]", JsonDoc.PathOf(root.Children[2]), "empty name");
                Same("$[\"q\\\"t\"]", JsonDoc.PathOf(root.Children[3]), "name with a quote");
                Same("$._ok", JsonDoc.PathOf(root.Children[4]), "underscore name");
                Same("$[\"1st\"]", JsonDoc.PathOf(root.Children[5]), "name starting with a digit");
            });

            Test(S, "json-summary", delegate
            {
                JsonNode d = TdJson("{\"o\":{\"a\":1,\"b\":2,\"c\":3},\"l\":[1,2,3,4,5,6,7,8,9,10,11,12],\"s\":\"hola\",\"n\":1.5e2,\"t\":true,\"f\":false,\"z\":null,\"nl\":\"a\\nb\",\"e\":{}}");
                Same("{ 3 }", JsonDoc.Summary(d.Children[0]), "object");
                Same("[ 12 ]", JsonDoc.Summary(d.Children[1]), "array");
                Same("\"hola\"", JsonDoc.Summary(d.Children[2]), "string");
                Same("1.5e2", JsonDoc.Summary(d.Children[3]), "number");
                Same("true", JsonDoc.Summary(d.Children[4]), "true");
                Same("false", JsonDoc.Summary(d.Children[5]), "false");
                Same("null", JsonDoc.Summary(d.Children[6]), "null");
                Same("\"a\\nb\"", JsonDoc.Summary(d.Children[7]), "a line break shows as an escape");
                Same("{ 0 }", JsonDoc.Summary(d.Children[8]), "empty object");
                Same("{ 9 }", JsonDoc.Summary(d), "root");
                Same("\"" + new string('x', 80) + "\u2026\"", JsonDoc.Summary(TdJson("\"" + new string('x', 100) + "\"")), "long string cut at 80");
                Same("\"" + new string('y', 80) + "\"", JsonDoc.Summary(TdJson("\"" + new string('y', 80) + "\"")), "80 characters are not cut");
            });

            Test(S, "json-lines", delegate
            {
                string err;
                int ln, col;
                JsonNode root = JsonDoc.ParseLines("{\"a\":1}\n\n{\"a\":2}\r\n[3]\n\"x\"\n", out err, out ln, out col);
                Check(root != null && err == null, "parse failed: " + err);
                Check(root.Kind == JsonKind.Array && root.Children.Count == 4, "four values");
                Check(root.Children[0].Kind == JsonKind.Object && root.Children[0].Parent == root && root.Children[0].Index == 0 && root.Children[0].Line == 1, "first value");
                Check(root.Children[1].Index == 1 && root.Children[1].Line == 3 && root.Children[1].Children[0].Value == "2", "second value sits on line 3");
                Check(root.Children[2].Kind == JsonKind.Array && root.Children[2].Line == 4, "array value on line 4");
                Check(root.Children[3].Kind == JsonKind.String && root.Children[3].Value == "x" && root.Children[3].Line == 5 && root.Children[3].Col == 1, "string value on line 5");
                Same("$[1].a", JsonDoc.PathOf(root.Children[1].Children[0]), "path through the lines array");

                Check(JsonDoc.ParseLines("{\"a\":1}\n{\"a\":2}\n{\"a\": }\n{\"a\":4}", out err, out ln, out col) == null, "bad line gives null");
                Same("Falta un valor antes de \u00AB}\u00BB", err, "message of the bad line");
                Check(ln == 3 && col == 7, "bad line position " + ln + ":" + col);
                Check(JsonDoc.ParseLines("{\"a\":1}\nnot json\n", out err, out ln, out col) == null && ln == 2 && col == 1, "second line is bad: " + ln + ":" + col);
                Check(JsonDoc.ParseLines("{\"a\":1}\n{\"a\":", out err, out ln, out col) == null && ln == 2 && col == 1 && err.StartsWith("Falta cerrar", StringComparison.Ordinal), "a cut last line: " + ln + ":" + col);

                JsonNode empty = JsonDoc.ParseLines("", out err, out ln, out col);
                Check(empty != null && err == null && empty.Kind == JsonKind.Array && empty.Children.Count == 0, "empty text is an empty array");
                JsonNode bom = JsonDoc.ParseLines("\uFEFF{\"a\":1}\n", out err, out ln, out col);
                Check(bom != null && bom.Children.Count == 1 && bom.Children[0].Col == 1, "a BOM does not move the columns");
            });

            // ---- Logs

            Test(S, "log-levelof-forms", delegate
            {
                TdLevels(new string[]
                {
                    "2026-10-20 12:00:00,123 ERROR [main] com.x.Foo - boom",
                    "[WARN] disk almost full",
                    "12:00:00 | INFO | started",
                    "DEBUG: cache miss",
                    "[trace] entering",
                    "FATAL: out of memory",
                    "2026-10-20T12:00:00Z CRITICAL db down",
                    "SEVERE: cannot bind",
                    "WARNING:root:careful",
                    "NOTICE: configuration reloaded",
                    "VERBOSE chatter",
                    "AVISO: poco espacio en disco",
                    "ADVERTENCIA: versi\u00F3n antigua",
                    "INFORMACI\u00D3N: listo",
                    "DEPURACI\u00D3N: paso 3",
                    "10-20 12:00:00.123  1234  5678 E Tag: msg",
                    "10-20 12:00:00.123  1234  5678 W Tag: msg",
                    "10-20 12:00:00.123  1234  5678 I Tag: msg",
                    "10-20 12:00:00.123  1234  5678 D Tag: msg",
                    "10-20 12:00:00.123  1234  5678 V Tag: msg",
                    "10-20 12:00:00.123  1234  5678 F Tag: msg",
                    "E/AndroidRuntime(1234): FATAL EXCEPTION: main",
                    "12:00:00.123 W/ActivityManager: slow",
                    "[12:00:00 ERR] x",
                    "[ERR] x",
                    "[WRN] low memory",
                    "[INF] up",
                    "[DBG] probe",
                    "[VRB] noise",
                    "[FTL] gone",
                    "level=error msg=boom",
                    "{\"level\":\"warn\",\"msg\":\"x\"}",
                    "Oct 20 12:00:00 myhost app[123]: error: something",
                    "2026-10-20 12:00:00 [error] boom",
                    "[Warning] mixed case"
                },
                new LogLevel[]
                {
                    LogLevel.Error, LogLevel.Warn, LogLevel.Info, LogLevel.Debug, LogLevel.Trace,
                    LogLevel.Fatal, LogLevel.Fatal, LogLevel.Error, LogLevel.Warn, LogLevel.Info,
                    LogLevel.Trace, LogLevel.Warn, LogLevel.Warn, LogLevel.Info, LogLevel.Debug,
                    LogLevel.Error, LogLevel.Warn, LogLevel.Info, LogLevel.Debug, LogLevel.Trace,
                    LogLevel.Fatal, LogLevel.Error, LogLevel.Warn, LogLevel.Error, LogLevel.Error,
                    LogLevel.Warn, LogLevel.Info, LogLevel.Debug, LogLevel.Trace, LogLevel.Fatal,
                    LogLevel.Error, LogLevel.Warn, LogLevel.Error, LogLevel.Error, LogLevel.Warn
                });
            });

            Test(S, "log-levelof-negatives", delegate
            {
                string[] lines =
                {
                    "The error was found in the report",
                    "Information about your account",
                    "Debugging is fun",
                    "ERROR_CODE=5 lookup",
                    "I am happy",
                    "Plan A/B test",
                    "I/O error while reading",
                    "C:\\build\\DEBUG\\app.dll",
                    "No hay nada que ver",
                    "",
                    "   ",
                    "   at Foo.Bar()",
                    "\tat com.x.Foo.run(Foo.java:10)",
                    "Caused by: java.lang.IllegalStateException: ERROR state",
                    new string('x', 170) + " ERROR now"
                };
                LogLevel[] none = new LogLevel[lines.Length];
                TdLevels(lines, none);
                Check(LogLines.LevelOf(null) == LogLevel.None, "null");
            });

            Test(S, "log-continuation", delegate
            {
                Check(LogLines.Continuation("   at Foo.Bar()"), "indented frame");
                Check(LogLines.Continuation("\tat com.x"), "tab and dotted name");
                Check(LogLines.Continuation("Caused by: java.io.IOException: gone"), "caused by");
                Check(LogLines.Continuation("\t... 23 more"), "elided frames");
                Check(LogLines.Continuation("  File \"app.py\", line 3, in run"), "python frame");
                Check(!LogLines.Continuation("at the station"), "prose without indent");
                Check(!LogLines.Continuation("   at least one"), "indented prose");
                Check(!LogLines.Continuation("ERROR boom"), "a log line");
                Check(!LogLines.Continuation(""), "empty");
                Check(!LogLines.Continuation(null), "null");
            });

            Test(S, "log-looks-like-log", delegate
            {
                List<string> app = new List<string>(new string[]
                {
                    "2026-10-20 12:00:00,123 INFO  Starting app",
                    "2026-10-20 12:00:01,456 WARN  Disk at 91%",
                    "2026-10-20 12:00:02,789 ERROR Failed to open file",
                    "java.lang.RuntimeException: boom",
                    "\tat com.x.Foo.run(Foo.java:10)",
                    "\tat com.x.Main.main(Main.java:5)",
                    "Caused by: java.io.IOException: gone",
                    "2026-10-20 12:00:03,000 INFO  Done"
                });
                Check(LogLines.LooksLikeLog(app), "application log with a stack trace");
                Check(LogLines.LooksLikeLog(new string[] { "10-20 12:00:00.123  1234  5678 I Tag: a", "10-20 12:00:00.124  1234  5678 D Tag: b", "10-20 12:00:00.125  1234  5678 W Tag: c" }), "logcat");
                Check(LogLines.LooksLikeLog(new string[] { "Oct 20 12:00:00 host sshd[1]: started", "Oct 20 12:00:01 host sshd[1]: ready", "Oct 20 12:00:02 host cron[2]: tick" }), "syslog");
                Check(LogLines.LooksLikeLog(new string[] { "1760961600 started", "1760961601 running", "1760961602 done" }), "epoch stamps");

                List<string> thirty = new List<string>(new string[] { "ERROR could not connect", "WARN retrying", "INFO connected" });
                for (int i = 1; i <= 7; i++) thirty.Add("plain line " + i);
                Check(LogLines.LooksLikeLog(thirty), "exactly 30% with a level");
                List<string> twenty = new List<string>(new string[] { "ERROR a", "INFO b" });
                for (int i = 1; i <= 8; i++) twenty.Add("plain line " + i);
                Check(!LogLines.LooksLikeLog(twenty), "20% with a level is not enough");

                Check(!LogLines.LooksLikeLog(new string[]
                {
                    "Esto es un texto normal.",
                    "La reuni\u00F3n es ma\u00F1ana por la tarde.",
                    "No hay ning\u00FAn error en estas l\u00EDneas, solo prosa.",
                    "",
                    "Gracias por leer hasta aqu\u00ED."
                }), "prose");
                Check(!LogLines.LooksLikeLog(new string[0]) && !LogLines.LooksLikeLog(null) && !LogLines.LooksLikeLog(new string[] { "", "  " }), "nothing to judge");
            });

            Test(S, "log-stamp-length", delegate
            {
                Check(LogLines.StampLength("2026-10-20 12:00:00,123 INFO x") == 23, "ISO with space and comma fraction");
                Check(LogLines.StampLength("2026-10-20T12:00:00Z x") == 20, "ISO with Z");
                Check(LogLines.StampLength("2026-10-20T12:00:00+02:00 x") == 25, "ISO with offset");
                Check(LogLines.StampLength("[12:00:00] msg") == 10, "bracketed time");
                Check(LogLines.StampLength("12:00:00.123 INFO") == 12, "bare time with fraction");
                Check(LogLines.StampLength("[2026-10-20 12:00:00] [error] x") == 21, "bracketed date and time");
                Check(LogLines.StampLength("[12:00:00 INF] msg") == 9, "Serilog style: the bracket stays open");
                Check(LogLines.StampLength("20/10/2026 12:00:00 x") == 19, "day first");
                Check(LogLines.StampLength("Oct 20 12:00:00 host") == 15, "syslog");
                Check(LogLines.StampLength("10-20 12:00:00.123 I Tag") == 18, "logcat");
                Check(LogLines.StampLength("1760961600123 x") == 13, "epoch milliseconds");
                Check(LogLines.StampLength("1760961600 x") == 10, "epoch seconds");
                Check(LogLines.StampLength("123456789 x") == 0, "nine digits are not an epoch");
                Check(LogLines.StampLength("hello") == 0 && LogLines.StampLength("") == 0 && LogLines.StampLength(null) == 0, "no stamp");
                Check(LogLines.StampLength("ERROR 12:00:00") == 0, "only at the start");
                Check(LogLines.StampLength("Mayor 5 12:00:00") == 0, "a word that starts like a month");
                Check(LogLines.StampLength("25:00:00 x") == 0, "impossible hour");
            });
        }
    }
}
