// Round 6 (personal data found and covered), suite "w6sensitive": the checks of each kind of data (with near misses),
// OCR words and their boxes, data drawn in images at several sizes and fonts and read back, the covers hiding every
// pixel of it, no false alarms on an invoice, the editor's covers and their undo, the capture path with the option on
// and off, the kept originals, and timings. Images are drawn here; no screen, no clipboard, no window shown.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Stackshot.Tests
{
    static class AppTestsW6Sensitive
    {
        const string SuiteName = "w6sensitive";
        static string work;

        sealed class Failed : Exception { public Failed(string m) : base(m) { } }
        static void Check(bool ok, string what) { if (!ok) throw new Failed(what); }

        public static void Suite()
        {
            work = Path.Combine(Path.GetTempPath(), "stackshot-w6sensitive", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string storeBefore = CoverStore.Dir;
            CoverStore.Dir = Path.Combine(work, "originales");
            try
            {
                AppTests.Test(SuiteName, "dni: control letter, formats and near misses", Dnis);
                AppTests.Test(SuiteName, "nie: X, Y and Z, formats and near misses", Nies);
                AppTests.Test(SuiteName, "social security number: mod 97, leading-zero rule, formats", Nss);
                AppTests.Test(SuiteName, "iban: mod 97-10, Spain and other countries, groups, OCR look-alikes", Ibans);
                AppTests.Test(SuiteName, "cards: Luhn, networks, groups and near misses", Cards);
                AppTests.Test(SuiteName, "e-mail addresses and near misses", Emails);
                AppTests.Test(SuiteName, "phones: Spanish numbers, prefixes, groups, labels and near misses", Phones);
                AppTests.Test(SuiteName, "ssn: 3-2-4 layout, SSA rules and formats", Ssns);
                AppTests.Test(SuiteName, "secrets: tokens, API keys and private keys", Secrets);
                AppTests.Test(SuiteName, "ocr look-alikes only inside a number that otherwise fits", LookAlikes);
                AppTests.Test(SuiteName, "no false alarms: dates, amounts, invoices, postcodes, times, versions", NoFalseAlarmsText);
                AppTests.Test(SuiteName, "several in one line, tokens split into words, columns not joined", Lines);
                AppTests.Test(SuiteName, "summary and editor messages in Spanish", Words);
                AppTests.Test(SuiteName, "ocr words: boxes in the original image, same reading as the text", OcrWords);
                AppTests.Test(SuiteName, "ocr words: a tall capture read in bands, boxes where the lines are", OcrWordsTall);
                AppTests.Test(SuiteName, "ocr words: sideways text, boxes turned back onto the image", OcrWordsSideways);
                AppTests.Test(SuiteName, "images: every kind found at several sizes and fonts", ImagesFonts);
                AppTests.Test(SuiteName, "images: the covers hide every pixel of the data and nothing else", CoversHideInk);
                AppTests.Test(SuiteName, "images: no false alarms on an invoice with a table, dates and amounts", NoFalseAlarmsImage);
                AppTests.Test(SuiteName, "images: a capture without text finds nothing", NoText);
                AppTests.Test(SuiteName, "editor canvas: covers in one undo step, no duplicates, undo and redo", CanvasUndo);
                AppTests.Test(SuiteName, "editor: the Datos sensibles button covers, says so, and twice adds nothing", EditorButton);
                AppTests.Test(SuiteName, "capture path: off by default, nothing runs; on: the veil hides the thumbnail", Gate);
                AppTests.Test(SuiteName, "capture path on: file, thumbnail and clipboard copy get only covered pixels", CaptureJob);
                AppTests.Test(SuiteName, "capture path on: OCR failure copies nothing, the capture still arrives", CaptureJobFailure);
                AppTests.Test(SuiteName, "capture path on: a burst of captures is checked one after another, off the caller's thread", JobQueue);
                AppTests.Test(SuiteName, "originals: kept privately, restored as marks, dropped when edited or gone", Originals);
                AppTests.Test(SuiteName, "timings: 1080p and 4K synthetic captures", Timings);
                AppTests.Test(SuiteName, "review: off, the stack's covering step leaves the capture path untouched", ReviewOffUntouched);
                AppTests.Test(SuiteName, "review: on, through the stack: name held, file covered, what the history index reads is clean", ReviewStackOn);
                AppTests.Test(SuiteName, "review: a long check never freezes the stack (editor, drag) and workers wait past 10 s", ReviewLongCheck);
                AppTests.Test(SuiteName, "review: a reopened covered capture asks before losing removed covers, not after an undo", ReviewRestoredCoversUnsaved);
                AppTests.Test(SuiteName, "review: DNI and NIE with the control letter misread by OCR, and what still is not one", ReviewMisreadLetters);
                AppTests.Test(SuiteName, "review: a tall scrolling capture is covered everywhere, also across pieces", ReviewTallCapture);
                AppTests.Test(SuiteName, "review: uninstalling removes the originals, also when captures are saved in the data folder", ReviewUninstall);
                AppTests.Test(SuiteName, "review: quitting waits for captures still being checked, within its time", ReviewExitWaits);
                AppTests.Test(SuiteName, "review: on, a window title in the file name loses its personal data", ReviewWindowTitleName);
                AppTests.Test(SuiteName, "review: a company's contribution account (CCC) is not a person's NSS", ReviewCompanyCcc);
                AppTests.Test(SuiteName, "review: a capture taking an earlier one's name does not keep that one's original", ReviewStaleOriginal);
                AppTests.Test(SuiteName, "review: the editor's search sees data already covered, and covers what a cover only half hides", ReviewEditorAlreadyCovered);
                AppTests.Test(SuiteName, "review: OCR's slips on common formats (a DNI's dot, a phone's space, look-alikes in an IBAN)", ReviewOcrSlips);
                AppTests.Test(SuiteName, "review: a burst through the stack leaves nothing waiting, held or running", ReviewBurst);
                AppTests.Test(SuiteName, "review: the originals are never listed in Recientes or Historial", ReviewNeverListed);
                AppTests.Test(SuiteName, "review: IT's SensitiveAuto policy turns the covering on and off over the user's choice", ReviewPolicy);
            }
            finally
            {
                CoverStore.Dir = storeBefore;
                try { Directory.Delete(work, true); } catch { }
            }
        }

        static string Group(string d)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < d.Length; i++) { if (i > 0 && i % 4 == 0) sb.Append(' '); sb.Append(d[i]); }
            return sb.ToString();
        }

        // ---- Sample values, computed here with the official rules (not with the code under test)

        const string Letters = "TRWAGMYFPDXBNJZSQVHLCKE";

        // A Visa number with varied digits: Windows' OCR leaves out a whole line with the test number 4111 1111 1111 1111.
        static readonly string Card = Group(Luhnify("453914880343646"));

        static string Dni(long n) { return n.ToString("00000000", CultureInfo.InvariantCulture) + Letters[(int)(n % 23)]; }

        static string Nie(char x, long n) { return x + n.ToString("0000000", CultureInfo.InvariantCulture) + Letters[(int)(((x - 'X') * 10000000L + n) % 23)]; }

        static string NssControl(long p, long n)
        {
            long d = n < 10000000 ? p * 10000000 + n : p * 100000000 + n;
            return (d % 97).ToString("00", CultureInfo.InvariantCulture);
        }

        static string Luhnify(string body)
        {
            // The check digit that makes body + digit pass Luhn.
            for (int c = 0; c <= 9; c++)
            {
                string s = body + c;
                int sum = 0;
                bool twice = false;
                for (int i = s.Length - 1; i >= 0; i--)
                {
                    int v = s[i] - '0';
                    if (twice) { v *= 2; if (v > 9) v -= 9; }
                    sum += v;
                    twice = !twice;
                }
                if (sum % 10 == 0) return s;
            }
            throw new Failed("no Luhn digit");
        }

        static string WrongLetter(string dni)
        {
            char last = dni[dni.Length - 1];
            char other = last == 'A' ? 'B' : 'A';
            if (other == 'B' && last == 'B') other = 'C';
            return dni.Substring(0, dni.Length - 1) + other;
        }

        static void Yes(string text, SensitiveKind kind)
        {
            List<SensitiveFinding> f = Sensitive.Scan(text);
            Check(f.Count == 1 && f[0].Kind == kind, "\u00AB" + text + "\u00BB should be one " + kind + ", got " + Show(f));
        }

        static void No(string text)
        {
            List<SensitiveFinding> f = Sensitive.Scan(text);
            Check(f.Count == 0, "\u00AB" + text + "\u00BB should give nothing, got " + Show(f));
        }

        static void NoKind(string text, SensitiveKind kind)
        {
            foreach (SensitiveFinding f in Sensitive.Scan(text)) Check(f.Kind != kind, "\u00AB" + text + "\u00BB should not give a " + kind + ", got " + Show(Sensitive.Scan(text)));
        }

        static string Show(List<SensitiveFinding> f)
        {
            List<string> s = new List<string>();
            foreach (SensitiveFinding x in f) s.Add(x.Kind + "<" + x.Text + ">@" + x.Start + "+" + x.Length);
            return s.Count == 0 ? "nothing" : string.Join(", ", s.ToArray());
        }

        // ---- Validators

        static void Dnis()
        {
            foreach (long n in new long[] { 12345678, 87654321, 0, 1, 99999999, 45872391, 30000001, 7654321 })
            {
                string d = Dni(n);
                Yes(d, SensitiveKind.Dni);
                Check(Sensitive.Is(d, SensitiveKind.Dni), d + " is a DNI");
                No(WrongLetter(d));
            }
            Check(Dni(12345678) == "12345678Z", "known DNI " + Dni(12345678));
            Yes("12345678-Z", SensitiveKind.Dni);
            Yes("12.345.678-Z", SensitiveKind.Dni);
            Yes("12.345.678Z", SensitiveKind.Dni);
            Yes("12345678 Z", SensitiveKind.Dni);
            Yes("12345678z", SensitiveKind.Dni);
            Yes("DNI: 12345678Z.", SensitiveKind.Dni);
            Yes("DNI12345678Z", SensitiveKind.Dni);
            Yes("NIF 12345678Z", SensitiveKind.Dni);
            No("12345678 z");             // a lone small letter after a space is a word, not a control letter
            No("1234567Z");               // seven digits
            No("123456789Z");             // nine
            No("AB12345678Z");            // inside a longer code
            No("12345678ZX");
            No("12345678Z9");
            No("12345678");
            No("12 345 678 Z");
        }

        static void Nies()
        {
            foreach (char x in new char[] { 'X', 'Y', 'Z' })
                foreach (long n in new long[] { 1234567, 0, 9999999, 4589012 })
                {
                    string d = Nie(x, n);
                    Yes(d, SensitiveKind.Nie);
                    Yes(d.Substring(0, 1) + "-" + d.Substring(1, 7) + "-" + d.Substring(8), SensitiveKind.Nie);
                    Yes(d.Substring(0, 1) + " " + d.Substring(1, 7) + " " + d.Substring(8), SensitiveKind.Nie);
                    No(WrongLetter(d));
                }
            Check(Nie('X', 1234567) == "X1234567L", "known NIE " + Nie('X', 1234567));
            Yes("NIE: X1234567L", SensitiveKind.Nie);
            No("W1234567L");
            No("X123456L");
            No("X12345678L");
            No("x1234567L");
            No("AX1234567L");
        }

        static void Nss()
        {
            foreach (long p in new long[] { 28, 8, 1, 46, 53 })
                foreach (long n in new long[] { 12345678, 98765432, 10000000, 1234567, 999, 2345678 })
                {
                    string pp = p.ToString("00", CultureInfo.InvariantCulture), cc = NssControl(p, n);
                    string n8 = n.ToString("00000000", CultureInfo.InvariantCulture);
                    Yes(pp + "/" + n8 + "/" + cc, SensitiveKind.Nss);
                    Yes(pp + " " + n8 + " " + cc, SensitiveKind.Nss);
                    Yes(pp + "-" + n8 + "-" + cc, SensitiveKind.Nss);
                    Yes(pp + n8 + cc, SensitiveKind.Nss);
                    Yes(pp + " / " + n8 + " / " + cc, SensitiveKind.Nss);
                    if (n < 10000000 && n >= 1000000) Yes(pp + "/" + n.ToString(CultureInfo.InvariantCulture) + "/" + cc, SensitiveKind.Nss); // 7 digits, no leading zero
                    string bad = ((int.Parse(cc, CultureInfo.InvariantCulture) + 1) % 97).ToString("00", CultureInfo.InvariantCulture);
                    No(pp + "/" + n8 + "/" + bad);
                    No(pp + n8 + bad);
                }
            // The leading-zero rule: a number below 10 million joins the province without its zero.
            Check(Sensitive.NssControl(28, 1234567) == (281234567L % 97), "leading-zero rule");
            Check(Sensitive.NssControl(28, 12345678) == (2812345678L % 97), "8-digit rule");
            string c = NssControl(99, 12345678);
            No("99/12345678/" + c);                                         // no such province
            No("28/12345678-" + NssControl(28, 12345678));                  // two different separators
            No("12/10/2026");
            Yes("NSS: 28/12345678/" + NssControl(28, 12345678), SensitiveKind.Nss);
            Yes("N\u00BA afiliaci\u00F3n 281234567840".Replace("281234567840", "28" + "12345678" + NssControl(28, 12345678)), SensitiveKind.Nss);
        }

        static void Ibans()
        {
            Yes("ES91 2100 0418 4502 0005 1332", SensitiveKind.Iban);
            Yes("ES9121000418450200051332", SensitiveKind.Iban);
            Yes("IBAN: ES91 2100 0418 4502 0005 1332", SensitiveKind.Iban);
            Yes("IBAN ES91-2100-0418-4502-0005-1332", SensitiveKind.Iban);
            Yes("ES 91 2100 0418 45 0200051332", SensitiveKind.Iban);
            Yes("GB82 WEST 1234 5698 7654 32", SensitiveKind.Iban);
            Yes("DE89 3704 0044 0532 0130 00", SensitiveKind.Iban);
            Yes("FR14 2004 1010 0505 0001 3M02 606", SensitiveKind.Iban);
            Yes("PT50 0002 0123 1234 5678 9015 4", SensitiveKind.Iban);
            Yes("NL91 ABNA 0417 1643 00", SensitiveKind.Iban);
            Yes("ES91 21OO O418 4502 0005 1332", SensitiveKind.Iban);   // O read for 0
            Yes("E591 2100 0418 4502 0005 1332", SensitiveKind.Iban);   // 5 read for S
            Check(Sensitive.IbanValid("ES9121000418450200051332"), "valid IBAN");
            No("ES91 2100 0418 4502 0005 1333");
            No("ES92 2100 0418 4502 0005 1332");
            No("ES91 2100 0418 4502 0005 133");
            No("ES91 2100 0418 4502 0005 13321");
            No("XX91 2100 0418 4502 0005 1332");
            No("ES91 2100 0418 4502 0005 1332X");
            No("es91 2100 0418 4502 0005 1332 x".Substring(0, 4));
            // The account inside the IBAN is not also a card or a phone.
            List<SensitiveFinding> f = Sensitive.Scan("Cuenta ES91 2100 0418 4502 0005 1332 domiciliada");
            Check(f.Count == 1 && f[0].Kind == SensitiveKind.Iban && f[0].Text == "ES9121000418450200051332", "IBAN in a sentence: " + Show(f));
        }

        static void Cards()
        {
            foreach (string c in new string[] { "4111 1111 1111 1111", "4111-1111-1111-1111", "4111111111111111", "5500 0000 0000 0004", "3782 822463 10005",
                                                "6011 1111 1111 1117", "2223 0031 2200 3222", "3530 1113 3330 0000", "4012 8888 8888 1881", "3056 930902 5904" })
                Yes(c, SensitiveKind.Card);
            Yes(Luhnify("454545454545454"), SensitiveKind.Card);
            Yes("Tarjeta: 4111 1111 1111 1111 caduca 12/28", SensitiveKind.Card);
            Yes("Pago 15/10/2026 4539 1488 0343 6467", SensitiveKind.Card);
            Yes("4539 14880343 6467", SensitiveKind.Card);              // a space OCR lost
            Yes("45391488 0343 6467", SensitiveKind.Card);
            No("0049 1500 0512 3456 7892");                          // twenty digits in groups of four: an account
            Check(Sensitive.Luhn("4111111111111111") && !Sensitive.Luhn("4111111111111112"), "Luhn");
            No("4111 1111 1111 1112");                 // Luhn fails
            No("1234 5678 9012 3456");                 // no network starts with 1
            No("4111 1111 1111");                      // 12 digits
            No("4111 1111 1111 1111 1");               // 17 digits for a Visa
            No("4111 11111 111 1111");                 // groups no card is printed in
            No(Luhnify("710000000000000"));            // 7: no network
            No("Ref. " + Luhnify("400000123456789"));  // a reference after Ref., in one piece
            No("4111 1111 1111 1111 \u20AC".Replace("4111 1111 1111 1111", "1111 2222 3333 4444"));
        }

        static void Emails()
        {
            foreach (string e in new string[] { "juan.perez@empresa.es", "maria_lopez+rrhh@mutua-ejemplo.com", "a@b.co", "RRHH@Empresa.ES", "j.p-1@sub.dominio.org" })
                Yes(e, SensitiveKind.Email);
            Yes("Correo: juan.perez@empresa.es.", SensitiveKind.Email);
            Yes("juan.perez @empresa.es", SensitiveKind.Email);
            Yes("juan.perez@ empresa.es", SensitiveKind.Email);
            List<SensitiveFinding> f = Sensitive.Scan("Escr\u00EDbeme a juan.perez@empresa.es, gracias");
            Check(f.Count == 1 && f[0].Text == "juan.perez@empresa.es", "e-mail in a sentence: " + Show(f));
            Yes("juan.perez@empresaes", SensitiveKind.Email);   // OCR lost the dot before "es"
            No("juan@emp");
            No("@empresa.es");
            No("juan@.es");
            No("juan@empresa.e");
            No("precio@1.000");
            No("juan..perez@empresa.es".Substring(0, 6));
            No("juan@empresa.123");
            // Digits in an address are the address, not a DNI or a phone.
            f = Sensitive.Scan("12345678Z@empresa.es 600123456@sms.es");
            Check(f.Count == 2 && f[0].Kind == SensitiveKind.Email && f[1].Kind == SensitiveKind.Email, "addresses with numbers: " + Show(f));
        }

        static void Phones()
        {
            foreach (string p in new string[] { "600 123 456", "600123456", "700 123 456", "+34 600 123 456", "+34600123456", "+ 34 600 123 456", "0034 912 345 678", "0034912345678",
                                                "(+34) 91 234 56 78", "912 34 56 78", "91 234 56 78", "600 12 34 56", "600-123-456", "912 345 678", "600 123456", "91 2345678" })
                Yes(p, SensitiveKind.Phone);
            Yes("Tel: 912345678", SensitiveKind.Phone);
            Yes("Tel\u00E9fono 812345678", SensitiveKind.Phone);
            Yes("M\u00F3vil: 612.345.678", SensitiveKind.Phone);
            Yes("Tlf. 912.345.678", SensitiveKind.Phone);
            Yes("Ll\u00E1manos al 900 100 200", SensitiveKind.Phone);
            // Right after a date or a time (a table row): the date's last group is not part of the phone.
            Yes("15/10/2026 600 123 456", SensitiveKind.Phone);
            Yes("15/10/2026 600123456", SensitiveKind.Phone);
            Yes("2026-10-12 600123456", SensitiveKind.Phone);
            Yes("15.10.2026 600 12 34 56", SensitiveKind.Phone);
            Yes("14:30 600 123 456", SensitiveKind.Phone);
            No("1 600 123 456 789");
            No("15/10/2026 912345678");
            No("912345678");                 // bare, from 8 or 9: any code
            No("500 123 456");               // 5 is no Spanish phone
            No("600 123 45");                // eight digits
            No("600 123 4567");              // ten
            No("6001 234 567".Substring(0, 4));
            No("Factura 612345678");          // after an identifier label
            No("N\u00BA 612345678");
            No("Pedido: 600 123 456");
            No("600.123.456");               // dots without a label read as an amount
            No("Total 600 123 456 \u20AC");
            No("600 123 456 \u20AC");
            No("600 123 456 kg");
            No("34 600 123 456 789");
            No("600 123 456 789");
        }

        static void Ssns()
        {
            Yes("123-45-6789", SensitiveKind.Ssn);
            Yes("SSN: 123 45 6789", SensitiveKind.Ssn);
            Yes("Social Security: 123-45-6789", SensitiveKind.Ssn);
            Check(Sensitive.Is("123-45-6789", SensitiveKind.Ssn), "is SSN");
            No("000-45-6789");
            NoKind("666-45-6789", SensitiveKind.Ssn);
            NoKind("900-45-6789", SensitiveKind.Ssn);
            No("123-00-6789");
            No("123-45-0000");
        }

        static void Secrets()
        {
            Yes("ghp_123456789012345678901234567890123456", SensitiveKind.Secret);
            Yes("sk-proj-123456789012345678901234567890123456", SensitiveKind.Secret);
            Yes("AKIAIOSFODNN7EXAMPLE", SensitiveKind.Secret);
            Yes("-----BEGIN RSA PRIVATE KEY-----", SensitiveKind.Secret);
            Check(Sensitive.Is("ghp_123456789012345678901234567890123456", SensitiveKind.Secret), "is Secret");
        }

        static void LookAlikes()
        {
            // O, I, l, S and B inside a number that otherwise fits; checked with the control digits all the same.
            string d = Dni(12345678);
            Yes("I2345678Z", SensitiveKind.Dni);
            Yes("1234S678Z", SensitiveKind.Dni);
            Yes("12345B78Z".Replace("12345B78Z", "1234567BZ").Replace("1234567BZ", Dni(12345678).Replace('8', 'B')), SensitiveKind.Dni);
            Yes("6OO 123 456", SensitiveKind.Phone);
            Yes("4111 1lll 1111 1111".Replace("1lll", "11l1"), SensitiveKind.Card);
            Check(d == "12345678Z", "sample");
            // Not when the number is mostly letters, nor when the check fails.
            No("SOLO BIEN");
            No("IOSBlOSB");
            No("I2S4S6B8Z");
            No("1234S678A");
            // Not in plain words, even with the look-alike letters.
            No("BOSS 1 SOLO");
        }

        static readonly string[] Invoice =
        {
            "Factura n.\u00BA 2026-000147   Fecha: 12/10/2026   Vencimiento: 15/11/2026 14:30",
            "Cliente 000123   Pedido 4500012345   Albar\u00E1n 7001234567   Lote 20261012",
            "Referencia F2026/0147   Expediente 2026/001234   C\u00F3digo postal 28001   Madrid",
            "Base imponible 1.234,56 \u20AC   IVA 21 % 259,26 \u20AC   Total 1.493,82 \u20AC",
            "3 x Monitor 24\"  189,00 \u20AC  567,00 \u20AC   2 x Teclado  45,50 \u20AC  91,00 \u20AC",
            "Versi\u00F3n 10.0.19045.3693   build 2.4.1   Hora 08:45:12   Duraci\u00F3n 01:23:45",
            "Importe 600 123 456 \u20AC   Unidades 912345678 uds   P\u00E1g. 1 de 3",
            "Total kilos 1 234 567,89   Ratio 0,9127   Saldo 123.456.789,00 \u20AC",
            "ISBN 978-84-376-0494-7   EAN 8412345678905   Matr\u00EDcula 1234 BCD",
            "Periodo 2026-10-01 a 2026-10-31   Semana 41   N\u00FAmero de serie 4G7H-2K9L",
        };

        static void NoFalseAlarmsText()
        {
            foreach (string line in Invoice) No(line);
            No("12/10/2026 14:30:15");
            No("1.234.567,89");
            No("v2.0.1 build 12345");
            No("28001 28080 08001");
            No("Tel\u00E9fono");
            No("2026");
        }

        static void Lines()
        {
            string nss = "28/12345678/" + NssControl(28, 12345678);
            string line = "DNI 12345678Z, NIE " + Nie('Y', 7654321) + ", NSS " + nss + ", IBAN ES91 2100 0418 4502 0005 1332, tarjeta 4111 1111 1111 1111, juan@empresa.es, +34 600 123 456";
            List<SensitiveFinding> f = Sensitive.Scan(line);
            Check(f.Count == 7, "seven in one line: " + Show(f));
            SensitiveKind[] want = { SensitiveKind.Dni, SensitiveKind.Nie, SensitiveKind.Nss, SensitiveKind.Iban, SensitiveKind.Card, SensitiveKind.Email, SensitiveKind.Phone };
            for (int i = 0; i < 7; i++) Check(f[i].Kind == want[i], "order " + Show(f));
            // A table: the account in one column and a number in the next are not joined across the gap.
            No("4111 1111\n1111 1111");
            No("600 123\n456");
            // From words: an IBAN, a phone and an NSS split into words by OCR are found with all their words.
            List<Ocr.Word> words = new List<Ocr.Word>();
            float x = 10;
            foreach (string w in new string[] { "IBAN:", "ES91", "2100", "0418", "4502", "0005", "1332" }) { words.Add(Word(w, x, 10, 0)); x += w.Length * 8 + 6; }
            x = 10;
            foreach (string w in new string[] { "Tel:", "600", "123", "456" }) { words.Add(Word(w, x, 40, 1)); x += w.Length * 8 + 6; }
            x = 10;
            foreach (string w in new string[] { "NSS", "28", "/", "12345678", "/", NssControl(28, 12345678) }) { words.Add(Word(w, x, 70, 2)); x += w.Length * 8 + 6; }
            // Two columns far apart: the digits on the right are not added to the number on the left.
            words.Add(Word("600123456", 10, 100, 3));
            words.Add(Word("789", 400, 100, 3));
            f = Sensitive.Find(words, new Size(800, 200));
            Check(f.Count == 4, "found from words: " + Show(f));
            Check(f[0].Kind == SensitiveKind.Iban && f[0].Box.Left <= words[1].Box.Left && f[0].Box.Right >= words[6].Box.Right, "IBAN box spans its words: " + f[0].Box);
            Check(f[0].Box.Left > words[0].Box.Right - 8, "the IBAN box leaves the label alone: " + f[0].Box);
            Check(f[1].Kind == SensitiveKind.Phone && f[1].Box.Right >= words[10].Box.Right, "phone box spans its words: " + f[1].Box);
            Check(f[2].Kind == SensitiveKind.Nss && f[2].Box.Right >= words[16].Box.Right, "NSS box spans its words: " + f[2].Box);
            Check(f[3].Kind == SensitiveKind.Phone && f[3].Box.Right < 300, "the far column stays out: " + f[3].Box);
            foreach (SensitiveFinding s in f) Check(s.Box.Top < 10 + 40 * 0 + 200 && s.Box.Height >= 14 + 4, "padded: " + s.Box);
        }

        static Ocr.Word Word(string text, float x, float y, int line)
        {
            Ocr.Word w = new Ocr.Word();
            w.Text = text;
            w.Box = new RectangleF(x, y, text.Length * 8, 14);
            w.Upright = w.Box;
            w.Line = line;
            return w;
        }

        static void Words()
        {
            List<SensitiveFinding> f = Sensitive.Scan("12345678Z ES91 2100 0418 4502 0005 1332 a@b.es c@d.es");
            Check(Sensitive.Summary(f) == "1 DNI, 1 IBAN, 2 correos", "summary: " + Sensitive.Summary(f));
            string msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { f, 4 });
            Check(msg == "4 datos sensibles tapados: 1 DNI, 1 IBAN, 2 correos. Puedes quitar cualquiera.", "message: " + msg);
            List<SensitiveFinding> one = Sensitive.Scan("600 123 456");
            msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { one, 1 });
            Check(msg == "1 dato sensible tapado: 1 tel\u00E9fono. Puedes quitarlo.", "message: " + msg);
            msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { new List<SensitiveFinding>(), 0 });
            Check(msg == "No he encontrado datos sensibles.", "message: " + msg);
            msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { one, 0 });
            Check(msg == "Los datos sensibles ya estaban tapados.", "message: " + msg);
            CoverOutcome r = new CoverOutcome();
            Check(SensitiveCover.Note(r) == null, "no note with nothing found");
            r.Found = f;
            Check(SensitiveCover.Note(r) == "4 datos tapados", "note " + SensitiveCover.Note(r));
            r.Found = one;
            Check(SensitiveCover.Note(r) == "1 dato tapado", "note " + SensitiveCover.Note(r));
            r.Error = "x";
            Check(SensitiveCover.Note(r) == "Sin revisar", "note " + SensitiveCover.Note(r));
            Check(Sensitive.Name(SensitiveKind.Nss, 2) == "n.\u00BA de la Seguridad Social" && Sensitive.Name(SensitiveKind.Phone, 2) == "tel\u00E9fonos", "names");
        }

        // ---- OCR words

        static Bitmap Page(int w, int h)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.White);
            return b;
        }

        // The box of the dark pixels in r (or Empty).
        static Rectangle Ink(Bitmap b, Rectangle r, int dark)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            r.Intersect(new Rectangle(0, 0, b.Width, b.Height));
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[b.Width];
                for (int y = r.Top; y < r.Bottom; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, b.Width);
                    for (int x = r.Left; x < r.Right; x++)
                    {
                        int p = row[x];
                        int m = Math.Min((p >> 16) & 255, Math.Min((p >> 8) & 255, p & 255));
                        if (m >= dark) continue;
                        if (x < x0) x0 = x;
                        if (y < y0) y0 = y;
                        if (x > x1) x1 = x;
                        if (y > y1) y1 = y;
                    }
                }
            }
            finally { b.UnlockBits(d); }
            return x1 < 0 ? Rectangle.Empty : Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
        }

        static List<Ocr.Word> ReadWords(Bitmap b)
        {
            string error;
            List<Ocr.Word> w = Ocr.ReadWords(b, out error);
            Check(w != null, "the OCR failed: " + error);
            return w;
        }

        static string Joined(List<Ocr.Word> words)
        {
            StringBuilder sb = new StringBuilder();
            int line = -1;
            foreach (Ocr.Word w in words)
            {
                if (sb.Length > 0) sb.Append(w.Line != line ? " | " : " ");
                sb.Append(w.Text);
                line = w.Line;
            }
            return sb.ToString();
        }

        static string Norm(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static void OcrWords()
        {
            using (Bitmap b = Page(700, 140))
            {
                string[] text = { "Primera l\u00EDnea de prueba", "Segunda con n\u00FAmeros 12345" };
                using (Graphics g = Graphics.FromImage(b))
                using (Font f = new Font("Segoe UI", 11))
                {
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.DrawString(text[0], f, Brushes.Black, 30, 20);
                    g.DrawString(text[1], f, Brushes.Black, 260, 80);
                }
                List<Ocr.Word> words = ReadWords(b);
                Console.WriteLine("    " + Joined(words));
                string error;
                string read = Ocr.Read(b, out error);
                Check(read != null && Norm(read) == Norm(Joined(words).Replace("|", "")), "words and text differ: <" + read + "> vs <" + Joined(words) + ">");
                Check(words.Count >= 7, "words: " + words.Count);
                foreach (Ocr.Word w in words)
                {
                    Rectangle box = Rectangle.Round(w.Box);
                    Rectangle ink = Ink(b, Rectangle.Inflate(box, 4, 4), 140);
                    Check(!ink.IsEmpty, "no ink under <" + w.Text + "> at " + w.Box);
                    Rectangle grown = Rectangle.Inflate(box, 3, 3);
                    Check(grown.Contains(ink), "<" + w.Text + "> box " + w.Box + " does not hold its ink " + ink);
                    Check(w.Box.Width < 260 && w.Box.Height < 40, "<" + w.Text + "> box too big " + w.Box);
                    Check(w.Upright == w.Box, "upright text keeps its box");
                }
                Check(words[0].Line == words[1].Line && words[words.Count - 1].Line != words[0].Line, "lines");
                Check(words[words.Count - 1].Box.Left > 250 && words[words.Count - 1].Box.Top > 70, "second line where it was drawn: " + words[words.Count - 1].Box);
            }
            // Empty and odd images never throw.
            using (Bitmap b = Page(1, 1)) Check(ReadWords(b).Count == 0, "1x1");
            using (Bitmap b = Page(3000, 4)) Check(ReadWords(b).Count == 0, "thin strip");
        }

        static void OcrWordsTall()
        {
            const int H = 5000;
            using (Bitmap b = Page(600, H))
            {
                Dictionary<string, int> at = new Dictionary<string, int>();
                using (Graphics g = Graphics.FromImage(b))
                using (Font f = new Font("Segoe UI", 10))
                {
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    for (int y = 30, i = 1; y < H - 40; y += 230, i++)
                    {
                        string s = "Marca" + i.ToString("000", CultureInfo.InvariantCulture);
                        g.DrawString(s, f, Brushes.Black, 40 + (i % 5) * 90, y);
                        at[s.ToLowerInvariant()] = y;
                    }
                    // Right on the edge between the first two pieces.
                    g.DrawString("Frontera", f, Brushes.Black, 300, Stackshot.Ocr.Band - 50);
                    at["frontera"] = Stackshot.Ocr.Band - 50;
                }
                List<Ocr.Word> words = ReadWords(b);
                int hits = 0;
                foreach (Ocr.Word w in words)
                {
                    string key = Norm(w.Text);
                    int y;
                    if (!at.TryGetValue(key, out y)) continue;
                    hits++;
                    Check(Math.Abs(w.Box.Top - y) < 25, "<" + w.Text + "> at y " + w.Box.Top + ", drawn at " + y);
                    at.Remove(key); // each once
                }
                Console.WriteLine("    " + hits + " words placed, missing: " + string.Join(", ", new List<string>(at.Keys).ToArray()));
                Check(at.Count == 0, "not read: " + string.Join(", ", new List<string>(at.Keys).ToArray()));
            }
        }

        static void OcrWordsSideways()
        {
            // The turn back, exactly: a block drawn in an image, the image turned, the block's box in the turned image
            // brought back lands on the block.
            Rectangle block = new Rectangle(37, 11, 90, 23);
            foreach (RotateFlipType turn in new RotateFlipType[] { RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone })
                using (Bitmap b = Page(300, 120))
                {
                    using (Graphics g = Graphics.FromImage(b)) g.FillRectangle(Brushes.Black, block);
                    b.RotateFlip(turn);
                    Rectangle seen = Ink(b, new Rectangle(0, 0, b.Width, b.Height), 128);
                    RectangleF back = Ocr.Unturn(seen, turn, new Size(300, 120));
                    Check(Rectangle.Round(back) == block, (int)turn + ": " + seen + " turned back is " + back + ", drawn at " + block);
                }
            // A portrait page (text running down or up), as the text suite draws it: when the reading is the turned one,
            // the words that come back sit on their own pixels. Whether a turn is tried at all is Read's own choice.
            int turnedReads = 0;
            foreach (string[] lines in new string[][] { new string[] { "Documento girado para leer", "Segunda linea del texto", "Tercera linea" },
                                                        new string[] { "Documento girado para leer", "Segunda linea del texto", "Correo juan.perez@empresa.es" } })
                foreach (int angle in new int[] { 90, 270 })
                {
                    using (Bitmap b = Page(230, 640))
                    {
                        using (Graphics g = Graphics.FromImage(b))
                        using (Font f = new Font("Segoe UI", 12f, GraphicsUnit.Point))
                        {
                            g.TextRenderingHint = TextRenderingHint.AntiAlias;
                            g.TranslateTransform(115, 320);
                            g.RotateTransform(angle);
                            float top = -(lines.Length * 30) / 2f;
                            for (int k = 0; k < lines.Length; k++)
                            {
                                SizeF s = g.MeasureString(lines[k], f);
                                g.DrawString(lines[k], f, Brushes.Black, -s.Width / 2, top + k * 30);
                            }
                        }
                        List<Ocr.Word> words = ReadWords(b);
                        string error;
                        Check(Norm(Ocr.Read(b, out error) ?? "") == Norm(Joined(words).Replace("|", "")), angle + ": words and text differ");
                        bool turned = words.Exists(delegate(Ocr.Word w) { return w.Upright != w.Box; });
                        List<SensitiveFinding> found = Sensitive.Find(words, b.Size);
                        Console.WriteLine("    " + angle + " degrees" + (turned ? " (turned back)" : " (not turned)") + ": " + Joined(words) + (found.Count > 0 ? "  -> " + Show(found) : ""));
                        if (!turned) continue;
                        turnedReads++;
                        foreach (Ocr.Word w in words)
                        {
                            Rectangle box = Rectangle.Round(w.Box);
                            Check(box.Height >= box.Width || w.Text.Length <= 2, angle + ": <" + w.Text + "> box should run down the image " + w.Box);
                            Rectangle ink = Ink(b, Rectangle.Inflate(box, 4, 4), 140);
                            Check(!ink.IsEmpty && Rectangle.Inflate(box, 5, 5).Contains(ink), angle + ": <" + w.Text + "> box " + w.Box + " vs ink " + ink);
                        }
                        foreach (SensitiveFinding s in found)
                        {
                            Rectangle mail = Ink(b, s.Box, 140);
                            Check(s.Kind == SensitiveKind.Email && !mail.IsEmpty && s.Box.Height > s.Box.Width, angle + ": " + Show(found) + " box " + s.Box);
                        }
                    }
                }
            Check(turnedReads >= 1, "no sideways page was read turned back");
        }

        // ---- Images with data drawn in them

        // A form with every kind of data, label and value drawn apart so the value's own pixels are known (values: the
        // same form with only the values drawn).
        static Bitmap Form(string font, float pt, float scale, out Bitmap values, out string[] drawn)
        {
            string nss = "28/12345678/" + NssControl(28, 12345678);
            string[][] rows =
            {
                new string[] { "DNI del trabajador: ", Dni(45872391) },
                new string[] { "NIE: ", Nie('Y', 7654321) },
                new string[] { "N\u00BA Seguridad Social: ", nss },
                new string[] { "IBAN: ", "ES91 2100 0418 4502 0005 1332" },
                new string[] { "Tarjeta: ", Card },
                new string[] { "Correo: ", "juan.perez@empresa.es" },
                new string[] { "Tel\u00E9fono: ", "600 123 456" },
            };
            drawn = new string[rows.Length];
            float px = pt * 96 / 72 * scale;
            int lh = (int)Math.Ceiling(px * 2.1f);
            int w = (int)(px * 34) + 60, h = lh * rows.Length + 60;
            Bitmap b = Page(w, h);
            values = Page(w, h);
            using (Graphics g = Graphics.FromImage(b))
            using (Graphics gv = Graphics.FromImage(values))
            using (Font f = new Font(font, px, GraphicsUnit.Pixel))
            using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                g.TextRenderingHint = gv.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                for (int i = 0; i < rows.Length; i++)
                {
                    float y = 30 + i * lh, x = 30;
                    g.DrawString(rows[i][0], f, Brushes.Black, x, y, sf);
                    float lw = g.MeasureString(rows[i][0], f, PointF.Empty, sf).Width;
                    g.DrawString(rows[i][1], f, Brushes.Black, x + lw, y, sf);
                    gv.DrawString(rows[i][1], f, Brushes.Black, x + lw, y, sf);
                    drawn[i] = rows[i][1];
                }
            }
            return b;
        }

        static void ImagesFonts()
        {
            object[][] cases =
            {
                new object[] { "Segoe UI", 9f, 1f }, new object[] { "Segoe UI", 9f, 1.5f }, new object[] { "Segoe UI", 11f, 1f }, new object[] { "Segoe UI", 14f, 2f },
                new object[] { "Arial", 10f, 1f }, new object[] { "Calibri", 12f, 1f }, new object[] { "Consolas", 10f, 1f }, new object[] { "Tahoma", 9f, 1.25f },
                new object[] { "Times New Roman", 12f, 1f }, new object[] { "Verdana", 9f, 1f },
            };
            // The fonts Windows and most apps draw their windows with must give every kind; fonts the engine misreads
            // (Calibri's 9 as B, Consolas' dotted zero, Verdana's split words) at least five of seven.
            HashSet<string> ui = new HashSet<string> { "Segoe UI", "Arial", "Tahoma", "Times New Roman" };
            int total = 0, found = 0;
            List<string> misses = new List<string>(), uiMisses = new List<string>();
            foreach (object[] c in cases)
            {
                Bitmap values;
                string[] drawn;
                using (Bitmap b = Form((string)c[0], (float)c[1], (float)c[2], out values, out drawn))
                using (values)
                {
                    List<Ocr.Word> words = ReadWords(b);
                    List<SensitiveFinding> f = Sensitive.Find(words, b.Size);
                    HashSet<SensitiveKind> kinds = new HashSet<SensitiveKind>();
                    foreach (SensitiveFinding s in f) kinds.Add(s.Kind);
                    string name = c[0] + " " + c[1] + " pt x" + c[2];
                    foreach (SensitiveKind k in new SensitiveKind[] { SensitiveKind.Dni, SensitiveKind.Nie, SensitiveKind.Nss, SensitiveKind.Iban, SensitiveKind.Card, SensitiveKind.Email, SensitiveKind.Phone })
                    {
                        total++;
                        if (kinds.Contains(k)) found++;
                        else
                        {
                            misses.Add(name + ": " + k);
                            if (ui.Contains((string)c[0])) uiMisses.Add(name + ": " + k);
                        }
                    }
                    Check(f.Count <= 7, name + ": more findings than data: " + Show(f));
                    Check(kinds.Count >= ((string)c[0] == "Consolas" ? 3 : 5), name + ": only " + kinds.Count + " of 7 kinds");
                    Console.WriteLine("    " + name + ": " + f.Count + " of 7" + (f.Count < 7 ? "  read: " + Joined(words) : ""));
                }
            }
            Console.WriteLine("    " + found + " of " + total + " found" + (misses.Count > 0 ? "; missed " + string.Join(", ", misses.ToArray()) : ""));
            Check(uiMisses.Count == 0, "missed " + string.Join(", ", uiMisses.ToArray()));
        }

        // Every dark pixel of every value lies under a cover, and the covers stay on their lines.
        static void CoversHideInk()
        {
            foreach (object[] c in new object[][] { new object[] { "Segoe UI", 9f, 1f }, new object[] { "Segoe UI", 12f, 1.5f }, new object[] { "Arial", 10f, 1f }, new object[] { "Tahoma", 11f, 1f },
                                                    new object[] { "Times New Roman", 12f, 2f } })
            {
                Bitmap values;
                string[] drawn;
                using (Bitmap b = Form((string)c[0], (float)c[1], (float)c[2], out values, out drawn))
                using (values)
                {
                    List<SensitiveFinding> f = Sensitive.Find(ReadWords(b), b.Size);
                    List<Rectangle> boxes = SensitiveCover.Boxes(f);
                    string name = c[0] + " " + c[1] + " pt x" + c[2];
                    Check(f.Count == 7, name + ": " + Show(f));
                    // Pixels of the data (dark in the values-only picture, even the faintest anti-aliasing) and of the labels
                    // (dark in the form where the values-only picture is blank).
                    int ink = 0, open = 0, label = 0, hidden = 0;
                    Rectangle all = new Rectangle(0, 0, values.Width, values.Height);
                    BitmapData d = values.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    BitmapData fd = b.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int[] row = new int[values.Width], frow = new int[values.Width];
                        for (int y = 0; y < values.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, values.Width);
                            System.Runtime.InteropServices.Marshal.Copy(new IntPtr(fd.Scan0.ToInt64() + (long)y * fd.Stride), frow, 0, values.Width);
                            for (int x = 0; x < values.Width; x++)
                            {
                                bool under = false;
                                foreach (Rectangle r in boxes) if (r.Contains(x, y)) { under = true; break; }
                                int m = Min3(row[x]);
                                if (m <= 235)
                                {
                                    ink++;
                                    if (!under) open++;
                                }
                                else if (m >= 250 && Min3(frow[x]) < 160)
                                {
                                    label++;
                                    if (under) hidden++;
                                }
                            }
                        }
                    }
                    finally { values.UnlockBits(d); b.UnlockBits(fd); }
                    Console.WriteLine("    " + name + ": " + ink + " pixels of data, " + open + " left visible; " + label + " pixels of labels, " + hidden + " under a cover");
                    Check(ink > 500 && open == 0, name + ": " + open + " of " + ink + " pixels of the data are not covered");
                    Check(label > 500 && hidden * 50 <= label, name + ": the covers hide " + hidden + " of " + label + " pixels of the labels");
                    // And nothing of the data can be read any more.
                    using (Bitmap covered = new Bitmap(b))
                    {
                        SensitiveCover.Paint(covered, boxes);
                        string error;
                        string left = Ocr.Read(covered, out error) ?? "";
                        Console.WriteLine("    covered reads: " + left.Replace("\n", " | "));
                        Check(Sensitive.Scan(left.Replace("\n", " ")).Count == 0 && !left.Contains("@"), name + ": data still readable after covering: " + left);
                    }
                }
            }
        }

        static int Min3(int p) { return Math.Min((p >> 16) & 255, Math.Min((p >> 8) & 255, p & 255)); }

        static void NoFalseAlarmsImage()
        {
            foreach (object[] c in new object[][] { new object[] { "Segoe UI", 9f }, new object[] { "Arial", 10f }, new object[] { "Consolas", 10f } })
            {
                float px = (float)c[1] * 96 / 72;
                int lh = (int)Math.Ceiling(px * 2);
                using (Bitmap b = Page((int)(px * 70), lh * (Invoice.Length + 8) + 40))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    using (Font f = new Font((string)c[0], px, GraphicsUnit.Pixel))
                    using (Pen grid = new Pen(Color.FromArgb(200, 200, 200)))
                    {
                        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                        for (int i = 0; i < Invoice.Length; i++) g.DrawString(Invoice[i], f, Brushes.Black, 20, 20 + i * lh);
                        // A table of lines with amounts, dates and codes in columns.
                        string[][] table =
                        {
                            new string[] { "Fecha", "Concepto", "Cantidad", "Precio", "Importe" },
                            new string[] { "01/10/2026", "Consulta 2026/88", "1", "45,00 \u20AC", "45,00 \u20AC" },
                            new string[] { "03/10/2026", "Revisi\u00F3n 7.2", "12", "1.250,00 \u20AC", "15.000,00 \u20AC" },
                            new string[] { "15/10/2026", "Lote 600123", "456", "0,99 \u20AC", "451,44 \u20AC" },
                            new string[] { "31/10/2026", "Ref. 912345", "678", "10,00 \u20AC", "6.780,00 \u20AC" },
                        };
                        int top = 20 + Invoice.Length * lh + 10;
                        int[] cols = { 20, (int)(px * 10), (int)(px * 26), (int)(px * 36), (int)(px * 50) };
                        for (int r = 0; r < table.Length; r++)
                        {
                            for (int k = 0; k < cols.Length; k++) g.DrawString(table[r][k], f, Brushes.Black, cols[k], top + r * lh);
                            g.DrawLine(grid, 10, top + r * lh - 3, b.Width - 10, top + r * lh - 3);
                        }
                    }
                    List<Ocr.Word> words = ReadWords(b);
                    List<SensitiveFinding> found = Sensitive.Find(words, b.Size);
                    Console.WriteLine("    " + c[0] + ": " + words.Count + " words, " + found.Count + " findings");
                    Check(found.Count == 0, c[0] + ": false alarms " + Show(found) + " in: " + Joined(words));
                }
            }
        }

        static void NoText()
        {
            using (Bitmap b = new Bitmap(800, 500, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                using (LinearGradientBrush br = new LinearGradientBrush(new Rectangle(0, 0, 800, 500), Color.SkyBlue, Color.SeaGreen, 35f))
                {
                    g.FillRectangle(br, 0, 0, 800, 500);
                    g.FillEllipse(Brushes.Orange, 200, 100, 220, 220);
                }
                Check(Sensitive.Find(ReadWords(b), b.Size).Count == 0, "findings in a picture without text");
            }
        }

        // ---- Editor

        static List<Rectangle> SampleBoxes()
        {
            return new List<Rectangle> { new Rectangle(20, 20, 120, 18), new Rectangle(20, 60, 200, 18), new Rectangle(300, 100, 80, 20) };
        }

        static void CanvasUndo()
        {
            Canvas c = new Canvas(Page(500, 200));
            try
            {
                Check(c.Pristine && !c.CanUndo, "fresh canvas");
                int changed = 0;
                c.Changed += delegate { changed++; };
                Check(c.AddCovers(SampleBoxes()) == 3, "three covers");
                Check(c.HasMarks && c.RedactBoxes().Count == 3 && changed == 1, "marks " + c.RedactBoxes().Count + ", changes " + changed);
                Check(!c.Pristine && c.CanUndo, "one undo step");
                using (Bitmap r = c.Render())
                {
                    Color p = r.GetPixel(80, 29);
                    Check(p.R == 22 && p.G == 22 && p.B == 26, "covered with the Redact colour: " + p);
                    Color o = r.GetPixel(450, 10);
                    Check(o.R == 255, "the rest untouched");
                }
                Check(c.AddCovers(SampleBoxes()) == 0, "the same boxes again add nothing");
                Check(c.AddCovers(new List<Rectangle> { new Rectangle(22, 21, 110, 16) }) == 0, "a box already under a cover adds nothing");
                Check(c.AddCovers(new List<Rectangle> { new Rectangle(-50, -50, 10, 10) }) == 0, "outside the image adds nothing");
                c.Undo();
                Check(!c.HasMarks && !c.CanUndo, "one undo removes all three");
                c.Redo();
                Check(c.RedactBoxes().Count == 3, "redo brings the three back");
                // Each is an ordinary mark: one can go on its own.
                Check(c.AddCovers(new List<Rectangle> { new Rectangle(300, 150, 60, 20) }) == 1, "a new one");
                c.Undo();
                Check(c.RedactBoxes().Count == 3, "undo of the second search keeps the first");
            }
            finally { c.Release(); c.Dispose(); }
        }

        // What the editor's search said last: its message leaves the hint strip after 6 s, which a busy computer can let pass
        // before the test looks.
        static string Said(Editor ed)
        {
            System.Windows.Forms.Timer t = (System.Windows.Forms.Timer)typeof(Editor).GetField("sensitiveFlash", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ed);
            return t != null && t.Tag != null ? (string)t.Tag : (string)typeof(Editor).GetField("flashText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ed);
        }

        static void EditorButton()
        {
            Bitmap values;
            string[] drawn;
            Bitmap img = Form("Segoe UI", 10f, 1f, out values, out drawn);
            values.Dispose();
            string file = Path.Combine(work, "editor.png");
            img.Save(file, ImageFormat.Png);
            Editor ed = new Editor(null, file, img);
            try
            {
                IntPtr h = ed.Handle; // never shown
                Check(h != IntPtr.Zero && !ed.Visible, "hidden editor");
                Canvas canvas = (Canvas)typeof(Editor).GetField("canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ed);
                MethodInfo find = typeof(Editor).GetMethod("FindSensitive", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo flash = typeof(Editor).GetField("flashText", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo busy = typeof(Editor).GetField("sensitiveBusy", BindingFlags.NonPublic | BindingFlags.Instance);
                // The button: on the right of the toolbar, with its tip and shortcut.
                Bar bar = (Bar)typeof(Editor).GetField("bar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ed);
                Bar.Item item = bar.Items.Find(delegate(Bar.Item it) { return it.Label == "Datos sensibles"; });
                Check(item != null && item.Glyph == "shield" && Glyph.Has("shield") && item.Right && item.Kind == Bar.Kind.Button && item.Enabled, "the Datos sensibles button");
                Check(item.Tip.Contains("Ctrl+May\u00FAs+X") && item.Tip.Contains("IBAN"), "tip: " + item.Tip);
                // Ctrl+Shift+X starts it like the button does.
                MethodInfo keys = typeof(Editor).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance);
                object[] args = { new Message(), Keys.Control | Keys.Shift | Keys.X };
                Check((bool)keys.Invoke(ed, args), "the shortcut is taken");
                Check((bool)busy.GetValue(ed), "busy while reading");
                Check(!item.Enabled, "the button waits while reading");
                Check(((string)flash.GetValue(ed) ?? "").StartsWith("Buscando", StringComparison.Ordinal), "progress shown: " + flash.GetValue(ed));
                find.Invoke(ed, null); // a second press while reading does nothing
                Stopwatch sw = Stopwatch.StartNew();
                while ((bool)busy.GetValue(ed) && sw.ElapsedMilliseconds < 60000) { Application.DoEvents(); Thread.Sleep(10); }
                Check(!(bool)busy.GetValue(ed), "no answer in 60 s");
                string said = Said(ed);
                Console.WriteLine("    " + said + " (" + sw.ElapsedMilliseconds + " ms)");
                Check(said == "7 datos sensibles tapados: 1 DNI, 1 NIE, 1 n.\u00BA de la Seguridad Social, 1 IBAN, 1 tarjeta, 1 correo, 1 tel\u00E9fono. Puedes quitar cualquiera.", "said: " + said);
                Check(canvas.RedactBoxes().Count == 7 && canvas.CanUndo, "seven covers in the canvas: " + canvas.RedactBoxes().Count);
                // Again: nothing new.
                find.Invoke(ed, null);
                sw = Stopwatch.StartNew();
                while ((bool)busy.GetValue(ed) && sw.ElapsedMilliseconds < 60000) { Application.DoEvents(); Thread.Sleep(10); }
                said = Said(ed);
                Console.WriteLine("    again: " + said);
                Check(canvas.RedactBoxes().Count == 7, "pressed twice: " + canvas.RedactBoxes().Count + " covers");
                Check(said == "No he encontrado datos sensibles." || said == "Los datos sensibles ya estaban tapados.", "said again: " + said);
                canvas.Undo();
                Check(canvas.RedactBoxes().Count == 0, "one undo removes the search's covers");
                // A cropped image: the covers land on the image, not on the crop's own corner.
                canvas.Crop = new RectangleF(10, (float)(img.Height / 2), img.Width - 20, img.Height / 2 - 5);
                find.Invoke(ed, null);
                sw = Stopwatch.StartNew();
                while ((bool)busy.GetValue(ed) && sw.ElapsedMilliseconds < 60000) { Application.DoEvents(); Thread.Sleep(10); }
                Console.WriteLine("    cropped: " + Said(ed));
                List<Rectangle> got = canvas.RedactBoxes();
                Check(got.Count >= 2, "covers in the cropped part: " + got.Count);
                foreach (Rectangle r in got) Check(r.Top >= img.Height / 2 - 10, "cover outside the crop " + r);
            }
            finally { ed.Dispose(); }
        }

        // ---- The capture path

        // A window as a screen shows it at that display scale (1 = 100 %: Segoe UI 9 pt, 12 px), filling w x h.
        static Bitmap Screenshot(int w, int h, bool withData)
        {
            return Screenshot(w, h, withData, 1f);
        }

        static Bitmap Screenshot(int w, int h, bool withData, float s)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            using (Font f = new Font("Segoe UI", 12 * s, GraphicsUnit.Pixel))
            using (Font big = new Font("Segoe UI Semibold", 20 * s, GraphicsUnit.Pixel))
            using (SolidBrush side = new SolidBrush(Color.FromArgb(243, 243, 243)))
            using (SolidBrush bar = new SolidBrush(Color.FromArgb(32, 32, 32)))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.FillRectangle(bar, 0, 0, w, 40 * s);
                g.FillRectangle(side, 0, 40 * s, 300 * s, h);
                g.DrawString("Expediente del trabajador - Mutua", big, Brushes.White, 16 * s, 8 * s);
                string[] menu = { "Inicio", "Expedientes", "Bajas", "Informes", "Ajustes" };
                for (int i = 0; i < menu.Length; i++) g.DrawString(menu[i], f, Brushes.Black, 24 * s, (70 + i * 34) * s);
                float y = 70 * s;
                for (int i = 0; y < h - 60 * s; i++, y += 26 * s)
                {
                    string line = "Observaci\u00F3n " + (i + 1) + ": revisi\u00F3n del parte del " + (1 + i % 28) + "/10/2026, importe " + (100 + i * 7) + ",50 \u20AC";
                    if (withData && i == 3) line = "Trabajador con DNI " + Dni(45872391) + " y tel\u00E9fono 600 123 456";
                    if (withData && i == 7) line = "Cuenta para el pago: ES91 2100 0418 4502 0005 1332";
                    if (withData && i == 11) line = "Contacto: juan.perez@empresa.es";
                    g.DrawString(line, f, Brushes.Black, 330 * s, y);
                }
            }
            return b;
        }

        static void Gate()
        {
            Settings s = new Settings();
            Check(!s.SensitiveAuto, "SensitiveAuto must be off by default");
            Check(!ShotStack.CoversCaptures(s), "off: captures are not covered");
            s.SensitiveAuto = true;
            Check(ShotStack.CoversCaptures(s), "on: captures are covered");
            // On: SaveCapture's thumbnail is replaced by a veil nothing can be read in, and the clipboard copy is dropped.
            using (Bitmap shot = Screenshot(1920, 1080, true))
            {
                Bitmap preview = ShotStack.Preview(shot, 600);
                Bitmap clip = TrackedData.DeepCopy(shot);
                string path = Path.Combine(work, "gate.png");
                CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref clip);
                try
                {
                    Check(clip == null && job.WantCopy, "the uncovered copy for the clipboard is dropped, and wanted later");
                    Check(job.Shot == shot && job.Path == path && CoverStore.Has(path), "the job owns the capture; its original may come");
                    using (Bitmap big = new Bitmap(preview, preview.Width * 3, preview.Height * 3))
                    {
                        string error;
                        string read = Ocr.Read(big, out error) ?? "";
                        Check(Norm(read).Length <= 3, "text readable in the veil: " + read);
                    }
                }
                finally
                {
                    preview.Dispose();
                    CoverStore.Settle(path);
                }
                Check(!CoverStore.Has(path), "settled");
                Bitmap p2 = ShotStack.Preview(shot, 600), c2 = null;
                CoverJob j2 = SensitiveCover.Prepare(shot, path, ref p2, ref c2);
                Check(!j2.WantCopy, "no copy wanted when copying is off");
                p2.Dispose();
                CoverStore.Settle(path);
            }
        }

        // The job as SaveCapture runs it, with the stack's writes and hand-over replaced by the test's.
        static CoverOutcome RunJob(Bitmap shot, string path, bool wantCopy, out Bitmap written)
        {
            Bitmap preview = ShotStack.Preview(shot, 600);
            Bitmap clip = wantCopy ? TrackedData.DeepCopy(shot) : null;
            CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref clip);
            preview.Dispose();
            Bitmap got = null;
            CoverOutcome outcome = null;
            int order = 0, writeAt = 0, doneAt = 0;
            job.Write = delegate(Bitmap img) { got = img; writeAt = ++order; };
            job.Done = delegate(CoverOutcome r) { outcome = r; doneAt = ++order; };
            job.Run();
            Check(got != null && outcome != null, "the job wrote and answered");
            Check(writeAt == 1 && doneAt == 2, "written, then handed over");
            Check(!CoverStore.Has(path) || File.Exists(Path.Combine(CoverStore.Dir, Path.GetFileNameWithoutExtension(path) + ".txt")), "settled");
            written = got;
            return outcome;
        }

        static bool Covered(Bitmap b, Rectangle r)
        {
            Color c = b.GetPixel(r.X + r.Width / 2, r.Y + r.Height / 2);
            return c.R == 22 && c.G == 22 && c.B == 26;
        }

        static void CaptureJob()
        {
            using (Bitmap shot = Screenshot(1920, 1080, true))
            using (Bitmap before = new Bitmap(shot))
            {
                string path = Path.Combine(work, "Captura 2026-10-12 10.00.00.png");
                Bitmap written;
                CoverOutcome r = RunJob(shot, path, true, out written);
                Console.WriteLine("    " + r.Found.Count + " found in " + r.Ms + " ms: " + Sensitive.Summary(r.Found));
                Check(r.Error == null && r.Found.Count == 4, "found: " + Sensitive.Summary(r.Found) + " " + r.Error);
                Check(written == shot, "the capture itself is written, covered in place");
                foreach (SensitiveFinding f in r.Found)
                {
                    Check(Covered(written, f.Box), "file: " + f.Kind + " not covered");
                    Check(Covered(r.Copy, f.Box), "clipboard: " + f.Kind + " not covered");
                }
                // Nothing of the data can be read in what goes out.
                string error;
                foreach (Bitmap outgoing in new Bitmap[] { written, r.Copy })
                {
                    string read = Ocr.Read(outgoing, out error) ?? "";
                    Check(Sensitive.Scan(read.Replace("\n", " ")).Count == 0 && !read.Contains("@"), "data readable in what goes out: " + read);
                }
                using (Bitmap big = new Bitmap(r.Preview, r.Preview.Width * 3, r.Preview.Height * 3))
                {
                    string read = Ocr.Read(big, out error) ?? "";
                    Check(Sensitive.Scan(read.Replace("\n", " ")).Count == 0, "data readable in the thumbnail: " + read);
                }
                Check(r.Size == before.Size && r.Preview.Width == 600, "thumbnail size " + r.Preview.Size);
                // The rest of the capture is untouched.
                Check(written.GetPixel(10, 10) == before.GetPixel(10, 10) && written.GetPixel(1900, 1070) == before.GetPixel(1900, 1070), "outside the covers");
                // The original is kept privately, uncovered, with the boxes.
                string orig = Path.Combine(CoverStore.Dir, "Captura 2026-10-12 10.00.00.png");
                Check(File.Exists(orig) && File.Exists(Path.ChangeExtension(orig, ".txt")), "original kept");
                Check(!File.Exists(Path.Combine(work, "Captura 2026-10-12 10.00.00.orig.png")), "nothing next to the capture");
                using (Bitmap o = new Bitmap(orig)) Check(SensitiveCover.SamePixels(o, before), "the original is the capture as taken");
                r.Preview.Dispose();
                r.Copy.Dispose();
            }
            // Nothing to cover: no original, the copy is still made.
            using (Bitmap shot = Screenshot(1920, 1080, false))
            {
                string path = Path.Combine(work, "Captura limpia.png");
                Bitmap written;
                CoverOutcome r = RunJob(shot, path, true, out written);
                Check(r.Error == null && r.Found.Count == 0, "false alarms on a capture without data: " + Show(r.Found));
                Check(r.Copy != null, "copied all the same");
                Check(!File.Exists(Path.Combine(CoverStore.Dir, "Captura limpia.png")), "no original without covers");
                r.Preview.Dispose();
                r.Copy.Dispose();
            }
        }

        static void CaptureJobFailure()
        {
            // A disposed image cannot be read: the job says so, writes what it has and copies nothing.
            Bitmap shot = Screenshot(800, 400, true);
            using (Bitmap clone = new Bitmap(shot))
            {
                string path = Path.Combine(work, "fallo.png");
                Bitmap preview = ShotStack.Preview(clone, 600), clip = TrackedData.DeepCopy(clone);
                CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref clip);
                preview.Dispose();
                shot.Dispose();
                bool wrote = false;
                CoverOutcome outcome = null;
                job.Write = delegate(Bitmap img) { wrote = true; };
                job.Done = delegate(CoverOutcome r) { outcome = r; };
                job.Run();
                Check(wrote, "still written (the stack must never wait for it forever)");
                Check(outcome != null && outcome.Error != null && outcome.Copy == null, "an error and no copy: " + (outcome == null ? "no answer" : outcome.Error));
                Check(SensitiveCover.Note(outcome) == "Sin revisar", "note");
                Check(!CoverStore.Has(path), "settled after a failure");
            }
        }

        static void JobQueue()
        {
            const int N = 4;
            List<int> threads = new List<int>();
            List<int> order = new List<int>();
            int found = 0;
            CountdownEvent left = new CountdownEvent(N);
            int caller = Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                int n = i;
                Bitmap shot = Screenshot(900, 360, i % 2 == 1);
                Bitmap preview = ShotStack.Preview(shot, 600), clip = null;
                CoverJob job = SensitiveCover.Prepare(shot, Path.Combine(work, "cola " + i + ".png"), ref preview, ref clip);
                preview.Dispose();
                job.Write = delegate(Bitmap img) { img.Dispose(); };
                job.Done = delegate(CoverOutcome r)
                {
                    lock (threads) { threads.Add(Thread.CurrentThread.ManagedThreadId); order.Add(n); found += r.Found.Count; }
                    if (r.Preview != null) r.Preview.Dispose();
                    left.Signal();
                };
                job.Start();
            }
            long call = sw.ElapsedMilliseconds;
            Check(left.Wait(120000), "not all checked in 120 s");
            Console.WriteLine("    " + N + " started in " + call + " ms, all checked in " + sw.ElapsedMilliseconds + " ms, order " + string.Join(",", order.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()));
            Check(call < 1000, "starting the jobs held the caller " + call + " ms");
            Check(!threads.Contains(caller), "ran on the caller's thread");
            Check(threads.TrueForAll(delegate(int t) { return t == threads[0]; }), "not one after another on one thread");
            for (int i = 0; i < N; i++) Check(order[i] == i, "out of order: " + string.Join(",", order.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()));
            Check(found >= 2 * 3, "found " + found + " in the captures with data");
            // The originals are kept after the hand-over; then every job is settled.
            for (int i = 0; i < N; i++)
            {
                string p = Path.Combine(work, "cola " + i + ".png");
                Stopwatch wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < 20000 && CoverStore.Has(p) && !File.Exists(Path.Combine(CoverStore.Dir, "cola " + i + ".txt"))) Thread.Sleep(20);
                Check(!CoverStore.Has(p) || File.Exists(Path.Combine(CoverStore.Dir, "cola " + i + ".txt")), "job " + i + " not settled");
                Check((i % 2 == 1) == File.Exists(Path.Combine(CoverStore.Dir, "cola " + i + ".txt")), "original kept only for the captures with data");
            }
        }

        static void Originals()
        {
            string before = CoverStore.Dir;
            CoverStore.Dir = Path.Combine(work, "originales-propios");
            try { OriginalsIn(Path.Combine(work, "temp")); }
            finally { CoverStore.Dir = before; }
        }

        static void OriginalsIn(string temp)
        {
            Directory.CreateDirectory(temp);
            string path = Path.Combine(temp, "Captura 2026-10-12 11.00.00.png");
            using (Bitmap shot = Screenshot(1280, 720, true))
            using (Bitmap before = new Bitmap(shot))
            {
                Bitmap written;
                CoverOutcome r = RunJob(shot, path, false, out written);
                Check(r.Found.Count == 4 && r.Copy == null, "found " + r.Found.Count);
                ShotStack.BeginWrite(path, written, null);
                ShotStack.WaitWritten(path);
                r.Preview.Dispose();
                Check(CoverStore.Has(path), "kept");
                List<Rectangle> boxes;
                Bitmap orig = CoverStore.Restore(path, out boxes);
                Check(orig != null && boxes.Count == SensitiveCover.Boxes(r.Found).Count, "restored with its boxes");
                Check(SensitiveCover.SamePixels(orig, before), "restored pixels are the original");
                // In the editor's canvas: the covers as marks from the start, rendering exactly the file.
                Canvas c;
                using (Bitmap file = ShotStack.LoadFull(path))
                {
                    c = new Canvas(new Bitmap(file));
                    Check(c.ShowCovers(orig, boxes), "shown");
                    Check(c.RedactBoxes().Count == boxes.Count && !c.CanUndo, "marks with no undo step");
                    using (Bitmap again = c.Render()) Check(SensitiveCover.SamePixels(again, file), "the canvas renders exactly the file: nothing to save");
                }
                int changes = 0;
                c.Changed += delegate { changes++; };
                Check(changes == 0, "no change");
                c.Release();
                c.Dispose();
                // Not on a canvas already edited.
                using (Bitmap file = ShotStack.LoadFull(path))
                {
                    Canvas e = new Canvas(new Bitmap(file));
                    e.AddCovers(new List<Rectangle> { new Rectangle(1, 1, 20, 20) });
                    List<Rectangle> b2;
                    Bitmap o2 = CoverStore.Restore(path, out b2);
                    Check(!e.ShowCovers(o2, b2) && e.RedactBoxes().Count == 1, "an edited canvas is left alone");
                    e.Release();
                    e.Dispose();
                }
                // The capture edited and written again: its original goes.
                using (Bitmap file = ShotStack.LoadFull(path))
                {
                    using (Graphics g = Graphics.FromImage(file)) g.FillRectangle(Brushes.Red, 5, 5, 30, 30);
                    file.Save(path, ImageFormat.Png);
                }
                List<Rectangle> b3;
                Check(CoverStore.Restore(path, out b3) == null && !CoverStore.Has(path), "an edited capture drops its original");
            }
            // Gone with the capture (old enough not to be on its way), not before.
            string gone = Path.Combine(temp, "Captura borrada.png");
            using (Bitmap shot = Screenshot(1280, 720, true))
            {
                Bitmap written;
                CoverOutcome r = RunJob(shot, gone, false, out written);
                written.Dispose();
                r.Preview.Dispose();
            }
            string origFile = Path.Combine(CoverStore.Dir, "Captura borrada.png"), list = Path.Combine(CoverStore.Dir, "Captura borrada.txt");
            Check(File.Exists(origFile) && File.Exists(list), "kept");
            Check(CoverStore.Prune(temp, delegate(string c) { return string.Equals(c, gone, StringComparison.OrdinalIgnoreCase); }) == 0 && File.Exists(origFile),
                  "kept while its capture is still being written");
            CoverStore.Expect(gone);
            Check(CoverStore.Prune(temp, null) == 0 && File.Exists(origFile), "kept while its capture is being checked");
            CoverStore.Settle(gone);
            File.WriteAllText(Path.Combine(CoverStore.Dir, "Otra.png.part"), "x");
            Check(CoverStore.Prune(temp, null) == 3 && !File.Exists(origFile) && !File.Exists(list), "gone at once with the capture (and a leftover .part)");
            // One whose capture is still there stays.
            string kept = Path.Combine(temp, "Captura sigue.png");
            using (Bitmap shot = Screenshot(1280, 720, true))
            {
                Bitmap written;
                CoverOutcome r = RunJob(shot, kept, false, out written);
                ShotStack.BeginWrite(kept, written, null);
                ShotStack.WaitWritten(kept);
                r.Preview.Dispose();
            }
            CoverStore.Prune(temp, null);
            Check(CoverStore.Has(kept), "kept while its capture is there");
            CoverStore.Forget(kept);
            Check(!CoverStore.Has(kept), "forgotten");
        }

        static void Timings()
        {
            // 1080p at 100 %, 4K at 150 % (the usual scale of a 4K screen) and at 100 % (more, smaller text).
            foreach (float[] size in new float[][] { new float[] { 1920, 1080, 1f }, new float[] { 3840, 2160, 1.5f }, new float[] { 3840, 2160, 1f } })
            {
                long best = long.MaxValue;
                int found = 0;
                string name = size[0] + "x" + size[1] + " at " + (int)(size[2] * 100) + " %";
                for (int run = 0; run < 2; run++)
                {
                    using (Bitmap shot = Screenshot((int)size[0], (int)size[1], true, size[2]))
                    {
                        string path = Path.Combine(work, "tiempo " + size[0] + " " + size[2] + " " + run + ".png");
                        Stopwatch sw = Stopwatch.StartNew();
                        Bitmap written;
                        CoverOutcome r = RunJob(shot, path, true, out written);
                        long ms = sw.ElapsedMilliseconds;
                        written.Dispose();
                        best = Math.Min(best, r.Ms);
                        found = r.Found.Count;
                        Console.WriteLine("    " + name + " run " + (run + 1) + ": covered and handed over in " + r.Ms + " ms (with the original kept: " + ms + " ms), " + r.Found.Count + " found");
                        r.Preview.Dispose();
                        if (r.Copy != null) r.Copy.Dispose();
                    }
                }
                Check(found == 4, name + ": found " + found);
                Check(best < (size[0] > 2000 ? 20000 : 8000), name + " took " + best + " ms");
            }
            // The UI thread's share (veil and dropping the copy) stays small.
            using (Bitmap shot = Screenshot(3840, 2160, true))
            {
                Bitmap preview = ShotStack.Preview(shot, 600), clip = null;
                Stopwatch sw = Stopwatch.StartNew();
                CoverJob job = SensitiveCover.Prepare(shot, Path.Combine(work, "ui.png"), ref preview, ref clip);
                long ms = sw.ElapsedMilliseconds;
                Console.WriteLine("    on the UI thread (veil): " + ms + " ms");
                Check(ms < 40, "the veil took " + ms + " ms on the UI thread");
                preview.Dispose();
                CoverStore.Settle(job.Path);
            }
        }

        // ---- Review: the covering step inside the stack, long checks, the editor and uninstall

        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        // A stack with only what SaveCapture's covering step and OpenEditor use: never shown, nothing registered. ui is
        // its sync control (handle on this thread).
        static ShotStack Stack(Settings s, string folder, Control ui)
        {
            ShotStack st = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            SetField(st, "settings", s);
            SetField(st, "folder", folder);
            SetField(st, "sync", ui);
            SetField(st, "cards", new List<Card>());
            SetField(st, "editors", new Dictionary<string, Editor>(StringComparer.OrdinalIgnoreCase));
            return st;
        }

        static void SetField(ShotStack st, string name, object value) { typeof(ShotStack).GetField(name, Priv).SetValue(st, value); }

        static void Pump(int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); }
        }

        static void ReviewOffUntouched()
        {
            Settings s = new Settings();
            Check(!s.SensitiveAuto, "off by default");
            string temp = Path.Combine(work, "rev-off");
            Directory.CreateDirectory(temp);
            using (Control ui = new Control())
            {
                ui.CreateControl();
                ShotStack st = Stack(s, temp, ui);
                MethodInfo step = typeof(ShotStack).GetMethod("CoverBeforeShowing", Priv);
                using (Bitmap shot = Screenshot(800, 400, true))
                {
                    string path = Path.Combine(temp, "Captura 2026-10-12 11.30.00.png");
                    Bitmap preview = ShotStack.Preview(shot, 600);
                    object[] args = { shot, path, preview, null };
                    Check(!(bool)step.Invoke(st, args), "off: SaveCapture writes the capture itself, as always");
                    Check(object.ReferenceEquals(args[2], preview) && args[3] == null, "off: the thumbnail is the capture's own");
                    Check(ShotStack.Unique(path) == path && !CoverJob.IsChecking(path) && !CoverStore.Has(path), "off: nothing held, checked or expected");
                    Check(preview.Width == 600 && shot.GetPixel(5, 5).A == 255, "off: nothing disposed");
                    preview.Dispose();
                }
            }
        }

        static void ReviewStackOn()
        {
            Settings s = new Settings();
            s.SensitiveAuto = true;
            string temp = Path.Combine(work, "rev-on");
            Directory.CreateDirectory(temp);
            using (Control ui = new Control())
            {
                ui.CreateControl();
                ShotStack st = Stack(s, temp, ui);
                MethodInfo step = typeof(ShotStack).GetMethod("CoverBeforeShowing", Priv);
                string path = Path.Combine(temp, "Captura 2026-10-12 12.00.00.png");
                Bitmap shot = Screenshot(1280, 720, true);
                Bitmap preview = ShotStack.Preview(shot, 600);
                object[] args = { shot, path, preview, null };
                Check((bool)step.Invoke(st, args), "on: the capture is checked first");
                Bitmap veil = (Bitmap)args[2];
                Check(!object.ReferenceEquals(veil, preview) && veil.Width == 600, "the card gets the veil");
                veil.Dispose();
                // While it is checked the name stays taken: a capture in the same second gets " (2)", never the same file.
                Check(ShotStack.Unique(path) != path, "the name is free during the check");
                Check(CoverJob.IsChecking(path) && !File.Exists(path), "nothing on disk before the check");
                // A worker that needs the file (history index, Copiar, Guardar) gets the covered one.
                string got = null;
                Thread t = new Thread(delegate() { ShotStack.WaitWritten(path); got = File.Exists(path) ? "ok" : "missing"; });
                t.IsBackground = true;
                t.Start();
                Stopwatch sw = Stopwatch.StartNew();
                while (t.IsAlive && sw.ElapsedMilliseconds < 120000) Pump(20);
                Check(got == "ok", "the worker came back without the file: " + got);
                // The hand-over runs on the stack's thread; the original is kept once it is settled.
                string list = Path.Combine(CoverStore.Dir, Path.GetFileNameWithoutExtension(path) + ".txt");
                sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 30000 && !(File.Exists(list) && !CoverJob.IsChecking(path))) Pump(20);
                Pump(100);
                Check(File.Exists(list), "original kept");
                // What the history index reads (the file, with Windows' OCR) holds none of the data, the rest still reads.
                using (Bitmap file = ShotStack.LoadFull(path))
                {
                    string error;
                    string read = Ocr.Read(file, out error) ?? "";
                    string norm = Norm(read);
                    Check(Sensitive.Scan(read.Replace("\n", " ")).Count == 0 && !read.Contains("@") && !norm.Contains("45872391") && !norm.Contains("21000418"), "the index would read: " + read);
                    Check(norm.Contains("observaci") || norm.Contains("obsetvaci"), "the rest of the capture should still read: " + read);
                }
                // The capture goes: so does its original, at the stack's next prune.
                File.Delete(path);
                typeof(ShotStack).GetMethod("PruneOriginals", Priv).Invoke(st, null);
                Check(!File.Exists(list) && !File.Exists(Path.ChangeExtension(list, ".png")), "the original outlived its capture");
            }
        }

        static void ReviewLongCheck()
        {
            string temp = Path.Combine(work, "rev-long");
            Directory.CreateDirectory(temp);
            string path = Path.Combine(temp, "Desplazamiento 2026-10-12 12.30.00.png");
            HashSet<string> writing = (HashSet<string>)typeof(ShotStack).GetField("writing", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            lock (writing) writing.Add(path); // as SaveCapture's covering step does
            ManualResetEvent release = new ManualResetEvent(false);
            Bitmap shot = Page(400, 200);
            Bitmap preview = ShotStack.Preview(shot, 600), clip = null;
            CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref clip);
            preview.Dispose();
            // A check longer than WaitWritten's 10 s (a tall scrolling capture on a busy computer): held here until released.
            job.Write = delegate(Bitmap img) { release.WaitOne(60000); ShotStack.BeginWrite(path, img, null); };
            job.Done = delegate(CoverOutcome r) { if (r.Preview != null) r.Preview.Dispose(); };
            job.Start();
            int ui = Thread.CurrentThread.ManagedThreadId;
            using (Control sync = new Control())
            using (Control sync2 = new Control())
            {
                sync.CreateControl();
                sync2.CreateControl();
                try
                {
                    Check(CoverJob.IsChecking(path), "being checked");
                    // Opening it in the editor: no freeze, the editor comes once it is covered. This stack never opens
                    // it (it is exiting by then), so no window is ever made here.
                    ShotStack st = Stack(new Settings(), temp, sync);
                    Stopwatch sw = Stopwatch.StartNew();
                    bool opened = st.OpenEditor(path);
                    long ms = sw.ElapsedMilliseconds;
                    Dictionary<string, Editor> editors = (Dictionary<string, Editor>)typeof(ShotStack).GetField("editors", Priv).GetValue(st);
                    Check(opened && editors.Count == 0, "the editor should wait for the check: " + opened + ", " + editors.Count);
                    Check(ms < 500, "opening the editor froze the stack " + ms + " ms");
                    SetField(st, "exiting", true);
                    // Dragging the card: no freeze, the card says it is still being checked.
                    Card card = new Card(st, path);
                    try
                    {
                        sw = Stopwatch.StartNew();
                        typeof(Card).GetMethod("StartDrag", Priv).Invoke(card, null);
                        ms = sw.ElapsedMilliseconds;
                        Check(ms < 500, "dragging froze the stack " + ms + " ms");
                        string flash = (string)typeof(Card).GetField("flash", Priv).GetValue(card);
                        Check(flash == "Revisando datos\u2026", "the card should say why: " + flash);
                    }
                    finally { card.Dispose(); }
                    // Something waiting for the check runs on the UI thread once the file is there.
                    ShotStack st2 = Stack(new Settings(), temp, sync2);
                    int ranOn = -1;
                    bool fileThen = false;
                    st2.AfterChecked(path, delegate { ranOn = Thread.CurrentThread.ManagedThreadId; fileThen = File.Exists(path); });
                    // A worker (Copiar, Guardar, the history index) waits for the check, not just WaitWritten's 10 s.
                    long back = -1;
                    bool fileBack = false;
                    Stopwatch since = Stopwatch.StartNew();
                    Thread t = new Thread(delegate() { ShotStack.WaitWritten(path); back = since.ElapsedMilliseconds; fileBack = File.Exists(path); });
                    t.IsBackground = true;
                    t.Start();
                    Pump(11000);
                    Check(back < 0 && ranOn < 0, "gave up before the check was over (" + back + " ms)");
                    release.Set();
                    sw = Stopwatch.StartNew();
                    while ((t.IsAlive || ranOn < 0) && sw.ElapsedMilliseconds < 30000) Pump(20);
                    Console.WriteLine("    worker back after " + back + " ms, with the file: " + fileBack);
                    Check(back >= 11000 && fileBack, "the worker came back at " + back + " ms, file " + fileBack);
                    Check(ranOn == ui && fileThen, "after the check: ran on thread " + ranOn + " (UI " + ui + "), file " + fileThen);
                }
                finally
                {
                    release.Set();
                    Stopwatch w = Stopwatch.StartNew();
                    while (CoverJob.IsChecking(path) && w.ElapsedMilliseconds < 30000) Thread.Sleep(20);
                    ShotStack.WaitWritten(path);
                    CoverStore.Settle(path);
                }
            }
        }

        static void ReviewRestoredCoversUnsaved()
        {
            using (Bitmap orig = Screenshot(900, 400, true))
            {
                List<Rectangle> boxes = new List<Rectangle> { new Rectangle(330, 140, 300, 20), new Rectangle(330, 245, 300, 20) };
                Bitmap file = TrackedData.DeepCopy(orig);
                SensitiveCover.Paint(file, boxes);
                string path = Path.Combine(work, "rev-editor.png");
                file.Save(path, ImageFormat.Png);
                Editor ed = new Editor(null, path, file);
                try
                {
                    IntPtr h = ed.Handle; // never shown
                    Check(h != IntPtr.Zero && !ed.Visible, "hidden editor");
                    Canvas canvas = (Canvas)typeof(Editor).GetField("canvas", Priv).GetValue(ed);
                    PropertyInfo unsaved = typeof(Editor).GetProperty("Unsaved", Priv);
                    Bar bar = (Bar)typeof(Editor).GetField("bar", Priv).GetValue(ed);
                    Bar.Item item = bar.Items.Find(delegate(Bar.Item it) { return it.Label == "Datos sensibles"; });
                    Check(item != null && item.Tip.Contains("puedes quitar cualquier marca"), "tip: " + (item == null ? "none" : item.Tip));
                    // As RestoreCovers leaves it: the original with the covers as marks.
                    Check(canvas.ShowCovers(TrackedData.DeepCopy(orig), boxes), "covers shown");
                    Check(canvas.MarksAsOpened && !(bool)unsaved.GetValue(ed, null), "reopened: nothing to keep");
                    // A mark drawn and undone: back to what the file holds, nothing to ask about.
                    canvas.AddCovers(new List<Rectangle> { new Rectangle(20, 300, 100, 30) });
                    Check(!canvas.MarksAsOpened && (bool)unsaved.GetValue(ed, null), "a new mark is a change");
                    canvas.Undo();
                    Check(canvas.MarksAsOpened && !(bool)unsaved.GetValue(ed, null), "drawn and undone: nothing to ask about, the file already is this");
                    // Every cover removed on purpose: the file still has them, so closing must not drop that silently.
                    List<Shape> shapes = (List<Shape>)typeof(Canvas).GetField("shapes", Priv).GetValue(canvas);
                    while (shapes.Count > 0) { canvas.SelectShape(shapes[0]); canvas.DeleteSelected(); }
                    Check(!canvas.HasMarks && (bool)unsaved.GetValue(ed, null), "the covers removed: closing would lose that without asking");
                    canvas.Undo();
                    Check((bool)unsaved.GetValue(ed, null), "one cover back, one still removed: still a change");
                    canvas.Undo();
                    Check(canvas.MarksAsOpened && !(bool)unsaved.GetValue(ed, null), "both back: as opened");
                }
                finally { ed.Dispose(); }
            }
            // An editor without reopened covers behaves as always: marks are changes, undone they are not.
            Bitmap plain = Page(300, 200);
            string p2 = Path.Combine(work, "rev-editor-plain.png");
            plain.Save(p2, ImageFormat.Png);
            Editor e2 = new Editor(null, p2, plain);
            try
            {
                IntPtr h = e2.Handle;
                Canvas c2 = (Canvas)typeof(Editor).GetField("canvas", Priv).GetValue(e2);
                PropertyInfo unsaved = typeof(Editor).GetProperty("Unsaved", Priv);
                Check(c2.MarksAsOpened && !(bool)unsaved.GetValue(e2, null), "fresh");
                c2.AddCovers(new List<Rectangle> { new Rectangle(10, 10, 50, 20) });
                Check(!c2.MarksAsOpened && (bool)unsaved.GetValue(e2, null), "a mark");
                c2.Undo();
                Check(c2.MarksAsOpened && !(bool)unsaved.GetValue(e2, null), "undone");
            }
            finally { e2.Dispose(); }
        }

        // What Windows' OCR gave for real drawn DNIs (Segoe UI, Tahoma, Arial, Calibri): the letter after the digits read as a
        // digit, another letter or a sign.
        static void ReviewMisreadLetters()
        {
            // 20197975L as ...1, 21227445D as ...0, 21385825B as ...8 or ...3, 20831495G as ...C or ...6, 21544205J as ...] or ...0.
            foreach (string s in new string[] { "201979751", "212274450", "213858258", "213858253", "20831495C", "208314956", "21544205]", "20197975I", "21544205)" })
            {
                Yes(s, SensitiveKind.Dni);
                Yes("Trabajador con DNI " + s, SensitiveKind.Dni);
                Yes("Juan Garc\u00EDa L\u00F3pez   " + s + "   Activo", SensitiveKind.Dni);
            }
            // After a DNI label anything in the letter's place counts, and so does the number alone.
            Yes("DNI: 203563551<", SensitiveKind.Dni);
            Yes("DNI 20673115'\"", SensitiveKind.Dni);
            Yes("D.N.I.: 20197975 L", SensitiveKind.Dni);
            Yes("DNI n.\u00BA 20197975-1", SensitiveKind.Dni);
            Yes("DNI del trabajador: 12345678", SensitiveKind.Dni);
            Check(Sensitive.Scan("DNI 203563551<")[0].Length == "203563551<".Length, "the whole misread letter is covered");
            // NIE likewise.
            Yes("X12345671", SensitiveKind.Nie);
            Yes("NIE: X1234567", SensitiveKind.Nie);
            // Still not a DNI: a company's NIF, a number after an ID word or with a unit, a phone, a word after the number.
            No("NIF: B12345674");
            No("NIF B-12345674");
            No("NIF: S2800001E");
            No("CIF/NIF: B87654321");
            No("Pedido 412345678");
            No("N.\u00BA 412345678");
            No("Factura 20260015C");
            No("412345678 \u20AC");
            No("Lote 20261012");
            No("12345678");
            No("12345678 z");
            // "Documento" alone is any document's number: only a real DNI (its letter checked) counts after it.
            No("Documento 20261234");
            No("Documento n.\u00BA 12345678");
            No("N.\u00BA de documento: 20261012");
            Yes("Documento: 12345678Z", SensitiveKind.Dni);
            Yes("Documento de identidad: 12345678", SensitiveKind.Dni);
            Yes("600123456", SensitiveKind.Phone);
            Yes("Tel. 912345678", SensitiveKind.Phone);
            List<SensitiveFinding> two = Sensitive.Scan("DNI: 12345678Z, tel\u00E9fono 600123456");
            Check(two.Count == 2 && two[0].Kind == SensitiveKind.Dni && two[1].Kind == SensitiveKind.Phone, "a phone after a DNI stays a phone: " + Show(two));
            List<SensitiveFinding> pair = Sensitive.Scan("DNI 12345678 y tel\u00E9fono 600 123 456");
            Check(pair.Count == 2 && pair[0].Length == 8 && pair[1].Kind == SensitiveKind.Phone, "the number alone, not the word after it: " + Show(pair));
            List<SensitiveFinding> after = Sensitive.Scan("DNI: 12345678, 87654321");
            Check(after.Count == 1 && after[0].Start == 5, "only the number next to the label: " + Show(after));
        }

        static void ReviewTallCapture()
        {
            const int W = 1280, H = 12000;
            using (Bitmap shot = Page(W, H))
            {
                List<Rectangle> spots = new List<Rectangle>(), data = new List<Rectangle>();
                using (Graphics g = Graphics.FromImage(shot))
                using (Font f = new Font("Segoe UI", 12, GraphicsUnit.Pixel))
                {
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    // A line every 24 px all the way down; every tenth one holds a DNI, so many land near the edges of the
                    // pieces the OCR reads (every 1040 px, overlapping by 160).
                    for (int y = 20, i = 0; y < H - 30; y += 24, i++)
                    {
                        if (i % 10 != 5)
                        {
                            g.DrawString("Observaci\u00F3n " + (i + 1) + ": revisi\u00F3n del parte del " + (1 + i % 28) + "/10/2026, importe " + (100 + i * 7) + ",50 \u20AC", f, Brushes.Black, 40, y);
                            continue;
                        }
                        string label = "Trabajador con DNI ";
                        int x = 40 + (int)g.MeasureString(label, f).Width + 3;
                        g.DrawString(label, f, Brushes.Black, 40, y);
                        g.DrawString(Dni(20000000L + i * 7919L), f, Brushes.Black, x, y);
                        spots.Add(new Rectangle(x - 2, y - 2, 140, 22));
                    }
                }
                foreach (Rectangle s in spots) data.Add(Ink(shot, s, 160));
                using (Bitmap before = new Bitmap(shot))
                {
                    Bitmap written;
                    CoverOutcome r = RunJob(shot, Path.Combine(work, "Desplazamiento alto.png"), false, out written);
                    int dnis = 0;
                    foreach (SensitiveFinding fd in r.Found) if (fd.Kind == SensitiveKind.Dni) dnis++;
                    Console.WriteLine("    " + W + "x" + H + ": " + dnis + " of " + data.Count + " DNI covered in " + r.Ms + " ms (" + Sensitive.Summary(r.Found) + ")");
                    Check(r.Error == null, "error: " + r.Error);
                    Check(dnis == data.Count, dnis + " of " + data.Count + " DNI found");
                    // Every pixel of every DNI is under a cover.
                    foreach (Rectangle ink in data)
                    {
                        Check(!ink.IsEmpty, "no ink drawn");
                        for (int y = ink.Top; y < ink.Bottom; y++)
                            for (int x = ink.Left; x < ink.Right; x++)
                            {
                                Color c = written.GetPixel(x, y);
                                if (c.R != 22 || c.G != 22 || c.B != 26) throw new Failed("DNI pixel left visible at " + x + "," + y + " (" + ink + ")");
                            }
                    }
                    Check(written.GetPixel(10, 10) == before.GetPixel(10, 10), "outside the covers");
                    written.Dispose();
                    r.Preview.Dispose();
                    Check(r.Ms < 120000, "took " + r.Ms + " ms");
                }
            }
        }

        static void ReviewUninstall()
        {
            // The save folder is the data folder itself: only what Stackshot keeps there goes, and the originals are its own.
            string data = Path.Combine(work, "rev-uninstall");
            Directory.CreateDirectory(Path.Combine(data, "originales"));
            File.WriteAllText(Path.Combine(data, "originales", "Captura 2026-10-12 10.00.00.png"), "x");
            File.WriteAllText(Path.Combine(data, "originales", "Captura 2026-10-12 10.00.00.txt"), "1 2 3 4 Dni\n");
            // The history's text index holds what the captures said: Stackshot's own too.
            Directory.CreateDirectory(Path.Combine(data, "historial"));
            File.WriteAllText(Path.Combine(data, "historial", "indice.gz"), "x");
            Directory.CreateDirectory(Path.Combine(data, "Capturas"));
            File.WriteAllText(Path.Combine(data, "Capturas", "mia.png"), "x");
            File.WriteAllText(Path.Combine(data, "suelta.png"), "x");
            Installer.DeleteData(data, data);
            Check(!Directory.Exists(Path.Combine(data, "originales")), "uncovered originals left behind after uninstalling");
            Check(!Directory.Exists(Path.Combine(data, "historial")), "the history's text index left behind after uninstalling");
            Check(File.Exists(Path.Combine(data, "Capturas", "mia.png")) && File.Exists(Path.Combine(data, "suelta.png")), "the user's captures stay");
            // The usual case: the whole data folder goes.
            string data2 = Path.Combine(work, "rev-uninstall2");
            Directory.CreateDirectory(Path.Combine(data2, "originales"));
            File.WriteAllText(Path.Combine(data2, "originales", "Captura.png"), "x");
            Installer.DeleteData(data2, Path.Combine(work, "rev-pictures"));
            Check(!Directory.Exists(data2), "data folder gone");
        }

        // A capture whose check is held by the test (Write waits for release), written with the stack's own BeginWrite.
        static CoverJob HeldJob(string path, ManualResetEvent release)
        {
            HashSet<string> writing = (HashSet<string>)typeof(ShotStack).GetField("writing", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            lock (writing) writing.Add(path); // as SaveCapture's covering step does
            Bitmap shot = Page(400, 200);
            Bitmap preview = ShotStack.Preview(shot, 600), clip = null;
            CoverJob job = SensitiveCover.Prepare(shot, path, ref preview, ref clip);
            preview.Dispose();
            job.Write = delegate(Bitmap img) { release.WaitOne(60000); ShotStack.BeginWrite(path, img, null); };
            job.Done = delegate(CoverOutcome r) { if (r.Preview != null) r.Preview.Dispose(); };
            return job;
        }

        static void ReviewExitWaits()
        {
            string temp = Path.Combine(work, "rev-exit");
            Directory.CreateDirectory(temp);
            using (Control ui = new Control())
            {
                ui.CreateControl();
                ShotStack st = Stack(new Settings(), temp, ui);
                MethodInfo waitCopies = typeof(ShotStack).GetMethod("WaitCopies", Priv);
                // Nothing being checked: quitting does not wait.
                Stopwatch sw = Stopwatch.StartNew();
                waitCopies.Invoke(st, new object[] { 5000 });
                Check(sw.ElapsedMilliseconds < 200, "waited " + sw.ElapsedMilliseconds + " ms with nothing to wait for");
                // A capture still being checked when Stackshot quits is checked and written, not lost.
                string path = Path.Combine(temp, "Captura 2026-10-12 13.00.00.png");
                ManualResetEvent release = new ManualResetEvent(false);
                HeldJob(path, release).Start();
                try
                {
                    Check(CoverJob.IsChecking(path), "being checked");
                    ThreadPool.QueueUserWorkItem(delegate { Thread.Sleep(800); release.Set(); });
                    sw = Stopwatch.StartNew();
                    waitCopies.Invoke(st, new object[] { 30000 });
                    Console.WriteLine("    quitting waited " + sw.ElapsedMilliseconds + " ms for the check");
                    Check(File.Exists(path), "the capture being checked was lost when quitting");
                    Check(!CoverJob.IsChecking(path), "still checking");
                }
                finally { release.Set(); }
                // One that takes too long holds the exit no longer than the time given.
                string slow = Path.Combine(temp, "Captura 2026-10-12 13.00.01.png");
                ManualResetEvent later = new ManualResetEvent(false);
                HeldJob(slow, later).Start();
                try
                {
                    sw = Stopwatch.StartNew();
                    waitCopies.Invoke(st, new object[] { 1500 });
                    long ms = sw.ElapsedMilliseconds;
                    Check(ms >= 1400 && ms < 6000, "a check that does not end held the exit " + ms + " ms (limit 1500)");
                }
                finally
                {
                    later.Set();
                    Stopwatch w = Stopwatch.StartNew();
                    while (CoverJob.IsChecking(slow) && w.ElapsedMilliseconds < 30000) Thread.Sleep(20);
                    ShotStack.WaitWritten(slow);
                    CoverStore.Settle(slow);
                }
            }
        }

        static void ReviewWindowTitleName()
        {
            string temp = Path.Combine(work, "rev-names");
            Directory.CreateDirectory(temp);
            DateTime when = new DateTime(2026, 10, 12, 14, 0, 0);
            ShotStack.CaptureStart start = new ShotStack.CaptureStart();
            start.App = "OUTLOOK";
            start.Window = "Bandeja de entrada - juan.perez@empresa.es - Outlook";
            Settings s = new Settings();
            s.FileNameTemplate = "{ventana} {fecha}";
            // Off: the title as it is (the template's own behaviour).
            string off = Path.GetFileName(ShotStack.CapturePath(s, temp, "Captura", start, when));
            Check(off == "Bandeja de entrada - juan.perez@empresa.es - Outlook 2026-10-12.png", "off: " + off);
            // On: what the image hides does not go out in its name either (drag and drop, the history, the log).
            s.SensitiveAuto = true;
            string on = Path.GetFileName(ShotStack.CapturePath(s, temp, "Captura", start, when));
            Check(on == "Bandeja de entrada - \u2022\u2022\u2022 - Outlook 2026-10-12.png", "on: " + on);
            start.Window = "Ficha 12345678Z - Juan Garc\u00EDa - Tel. 600 123 456";
            on = Path.GetFileName(ShotStack.CapturePath(s, temp, "Captura", start, when));
            Check(on == "Ficha \u2022\u2022\u2022 - Juan Garc\u00EDa - Tel. \u2022\u2022\u2022 2026-10-12.png", "on: " + on);
            // A title with nothing personal, and the default names, are left as they are.
            start.Window = "Informe de ventas.xlsx - Excel";
            on = Path.GetFileName(ShotStack.CapturePath(s, temp, "Captura", start, when));
            Check(on == "Informe de ventas.xlsx - Excel 2026-10-12.png", "on, nothing personal: " + on);
            s.FileNameTemplate = Settings.DefaultNameTemplate;
            on = Path.GetFileName(ShotStack.CapturePath(s, temp, "Captura", start, when));
            Check(on == "Captura 2026-10-12 14.00.00.png", "default template: " + on);
            Check(Sensitive.Scrub("") == "" && Sensitive.Scrub(null) == "" && Sensitive.Scrub("juan@empresa.es") == "\u2022\u2022\u2022", "scrub edges");
        }

        static void ReviewCompanyCcc()
        {
            string c = NssControl(8, 1234567);
            // A company's contribution account is written like an NSS with 7 digits and passes its check: with its label it
            // is the company's, not a person's.
            No("CCC 08/1234567/" + c);
            No("C.C.C.: 0111 08 1234567 " + c);
            No("C\u00F3digo cuenta de cotizaci\u00F3n de la empresa: 08-1234567-" + c);
            // Without a label, or with a person's label, as before; a person's 8-digit number is never taken for a CCC.
            Yes("08/1234567/" + c, SensitiveKind.Nss);
            Yes("NSS 08/1234567/" + c, SensitiveKind.Nss);
            Yes("CCC del trabajador, n\u00BA de afiliaci\u00F3n 08/1234567/" + c, SensitiveKind.Nss);
            Yes("CCC: 28/12345678/" + NssControl(28, 12345678), SensitiveKind.Nss);
            List<SensitiveFinding> f = Sensitive.Scan("CCC 08/1234567/" + c + "   NSS 28/12345678/" + NssControl(28, 12345678));
            Check(f.Count == 1 && f[0].Kind == SensitiveKind.Nss && f[0].Text == "28" + "12345678" + NssControl(28, 12345678), "only the worker's: " + Show(f));
        }

        // Presses "Datos sensibles" in a hidden editor and waits for its answer.
        static string PressSensitive(Editor ed)
        {
            typeof(Editor).GetMethod("FindSensitive", Priv).Invoke(ed, null);
            FieldInfo busy = typeof(Editor).GetField("sensitiveBusy", Priv);
            Stopwatch sw = Stopwatch.StartNew();
            while ((bool)busy.GetValue(ed) && sw.ElapsedMilliseconds < 60000) { Application.DoEvents(); Thread.Sleep(10); }
            Check(!(bool)busy.GetValue(ed), "no answer in 60 s");
            return Said(ed);
        }

        static void ReviewEditorAlreadyCovered()
        {
            using (Bitmap orig = Screenshot(1280, 720, true))
            {
                Bitmap written;
                string path = Path.Combine(work, "rev-already.png");
                CoverOutcome r = RunJob(new Bitmap(orig), path, false, out written);
                r.Preview.Dispose();
                CoverStore.Forget(path);
                List<Rectangle> boxes = SensitiveCover.Boxes(r.Found);
                Check(boxes.Count == 4, "found " + Show(r.Found));
                written.Save(path, ImageFormat.Png);
                // A capture covered when it was taken, reopened: the covers are marks over the original. Searching sees the
                // data under them and says it is covered, instead of that there is none.
                Editor ed = new Editor(null, path, written);
                try
                {
                    IntPtr h = ed.Handle; // never shown
                    Canvas canvas = (Canvas)typeof(Editor).GetField("canvas", Priv).GetValue(ed);
                    Check(h != IntPtr.Zero && canvas.ShowCovers(TrackedData.DeepCopy(orig), boxes), "covers shown");
                    string said = PressSensitive(ed);
                    Check(said == "Los datos sensibles ya estaban tapados." && canvas.RedactBoxes().Count == 4 && !canvas.CanUndo, "reopened: " + said + " (" + canvas.RedactBoxes().Count + " marks)");
                    // One cover removed by mistake: searching again puts back just that one, and says so.
                    List<Shape> shapes = (List<Shape>)typeof(Canvas).GetField("shapes", Priv).GetValue(canvas);
                    canvas.SelectShape(shapes[0]);
                    canvas.DeleteSelected();
                    said = PressSensitive(ed);
                    Check(said == "He tapado 1 dato sensible m\u00E1s; los otros 3 ya lo estaban." && canvas.RedactBoxes().Count == 4, "one removed: " + said + " (" + canvas.RedactBoxes().Count + " marks)");
                }
                finally { ed.Dispose(); }
                // Half of the IBAN hidden by hand: the rest of it is found and covered too.
                Rectangle iban = Rectangle.Empty;
                foreach (SensitiveFinding f in r.Found) if (f.Kind == SensitiveKind.Iban) iban = f.Box;
                Check(!iban.IsEmpty, "no IBAN");
                Editor e2 = new Editor(null, path, TrackedData.DeepCopy(orig));
                try
                {
                    IntPtr h = e2.Handle;
                    Canvas canvas = (Canvas)typeof(Editor).GetField("canvas", Priv).GetValue(e2);
                    Check(h != IntPtr.Zero && canvas.AddCovers(new List<Rectangle> { new Rectangle(iban.X, iban.Y, iban.Width / 2, iban.Height) }) == 1, "half covered");
                    string said = PressSensitive(e2);
                    Check(said.StartsWith("4 datos sensibles tapados", StringComparison.Ordinal), "half covered: " + said);
                    using (Bitmap out2 = canvas.Render())
                        Check(Covered(out2, new Rectangle(iban.X + iban.Width / 2, iban.Y, iban.Width / 2, iban.Height)), "the visible half of the IBAN was left uncovered");
                }
                finally { e2.Dispose(); }
            }
            // Two pieces of data in one word share one mark: a first search still reports them as covered now.
            List<SensitiveFinding> two = Sensitive.Scan("a@b.es c@d.es");
            string msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { two, 2 });
            Check(msg == "2 datos sensibles tapados: 2 correos. Puedes quitar cualquiera.", "message: " + msg);
            msg = (string)typeof(Editor).GetMethod("SensitiveResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { two, 1 });
            Check(msg == "He tapado 1 dato sensible m\u00E1s; el otro ya lo estaba.", "message: " + msg);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint flags);

        static void ReviewBurst()
        {
            // A burst of captures through the stack's covering step: each one is checked and written, and afterwards nothing
            // is left waiting, held or running (no thread, no GDI objects, no pending original).
            Settings s = new Settings();
            s.SensitiveAuto = true;
            string temp = Path.Combine(work, "rev-burst");
            Directory.CreateDirectory(temp);
            IntPtr me = Process.GetCurrentProcess().Handle;
            using (Control ui = new Control())
            {
                ui.CreateControl();
                ShotStack st = Stack(s, temp, ui);
                MethodInfo step = typeof(ShotStack).GetMethod("CoverBeforeShowing", Priv);
                Pump(50);
                uint gdi = GetGuiResources(me, 0);
                const int N = 6;
                List<string> paths = new List<string>();
                List<long> callerMs = new List<long>();
                for (int i = 0; i < N; i++)
                {
                    string path = Path.Combine(temp, "Captura 2026-10-12 16.00.0" + i + ".png");
                    paths.Add(path);
                    Bitmap shot = Screenshot(1280, 720, i % 2 == 0);
                    Bitmap preview = ShotStack.Preview(shot, 600);
                    object[] args = { shot, path, preview, null };
                    Stopwatch sw = Stopwatch.StartNew();
                    Check((bool)step.Invoke(st, args), "covering");
                    callerMs.Add(sw.ElapsedMilliseconds);
                    ((Bitmap)args[2]).Dispose(); // the veil (no card here)
                }
                // GDI+ runs one DrawImage at a time: a veil drawn while the OCR scales the capture before waits for it.
                Console.WriteLine("    on the caller: " + string.Join(", ", callerMs.ConvertAll<string>(delegate(long x) { return x + " ms"; }).ToArray()));
                callerMs.Sort();
                Stopwatch all = Stopwatch.StartNew();
                while (all.ElapsedMilliseconds < 180000 && (CoverJob.Checking().Count > 0 || paths.Exists(delegate(string p) { return CoverStore.Has(p) && !File.Exists(Path.Combine(CoverStore.Dir, Path.GetFileNameWithoutExtension(p) + ".txt")); })))
                    Pump(20);
                foreach (string p in paths) ShotStack.WaitWritten(p);
                Pump(300);
                Console.WriteLine("    " + N + " captures all checked in " + all.ElapsedMilliseconds + " ms");
                Check(callerMs[N / 2] < 40 && callerMs[N - 1] < 600, "the caller was held " + callerMs[N / 2] + " ms (median), " + callerMs[N - 1] + " ms (worst)");
                Check(CoverJob.Checking().Count == 0, "still checking " + CoverJob.Checking().Count);
                HashSet<string> writing = (HashSet<string>)typeof(ShotStack).GetField("writing", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                foreach (string p in paths)
                {
                    Check(File.Exists(p), "not written: " + p);
                    lock (writing) Check(!writing.Contains(p), "still marked as being written: " + p);
                }
                Stopwatch quit = Stopwatch.StartNew();
                while ((bool)typeof(CoverJob).GetField("running", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) && quit.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                Check(!(bool)typeof(CoverJob).GetField("running", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null), "the checking thread is still running with nothing to do");
                uint after = GetGuiResources(me, 0);
                Check(after <= gdi + 2, "GDI objects " + gdi + " -> " + after);
                // The ones with data keep their original until the capture goes; then the stack's prune takes them.
                for (int i = 0; i < N; i++) Check((i % 2 == 0) == File.Exists(Path.Combine(CoverStore.Dir, Path.GetFileNameWithoutExtension(paths[i]) + ".txt")), "original of " + i);
                foreach (string p in paths) File.Delete(p);
                typeof(ShotStack).GetMethod("PruneOriginals", Priv).Invoke(st, null);
                foreach (string p in paths) Check(!CoverStore.Has(p), "original left: " + p);
            }
        }

        static void ReviewNeverListed()
        {
            // Even with the save folder being the data folder itself, the originals are in neither Recientes nor Historial.
            string data = Path.Combine(work, "rev-listed");
            string temp = Path.Combine(work, "rev-listed-temp");
            Directory.CreateDirectory(Path.Combine(data, "originales"));
            Directory.CreateDirectory(Path.Combine(data, "2026-10"));
            Directory.CreateDirectory(temp);
            using (Bitmap b = Page(40, 30))
            {
                b.Save(Path.Combine(data, "originales", "Captura 2026-10-12 15.00.00.png"), ImageFormat.Png);
                b.Save(Path.Combine(data, "2026-10", "Guardada.png"), ImageFormat.Png);
                b.Save(Path.Combine(temp, "Captura 2026-10-12 15.00.00.png"), ImageFormat.Png);
            }
            Settings s = new Settings();
            s.SaveFolder = data;
            string recentsBefore = Recents.TempFolder, historyBefore = HistoryIndex.TempFolder;
            Recents.TempFolder = temp;
            HistoryIndex.TempFolder = temp;
            try
            {
                Recents.Forget();
                List<FileInfo> recents = (List<FileInfo>)typeof(Recents).GetMethod("Files", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s });
                List<HistoryItem> history = HistoryIndex.List(s, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                Check(recents.Count >= 2 && history.Count >= 2, "listed " + recents.Count + " recent, " + history.Count + " in the history");
                foreach (FileInfo f in recents) Check(f.DirectoryName.IndexOf("originales", StringComparison.OrdinalIgnoreCase) < 0, "an original in Recientes: " + f.FullName);
                foreach (HistoryItem it in history) Check(it.Path.IndexOf("originales", StringComparison.OrdinalIgnoreCase) < 0, "an original in Historial: " + it.Path);
            }
            finally
            {
                Recents.TempFolder = recentsBefore;
                HistoryIndex.TempFolder = historyBefore;
                Recents.Forget();
            }
        }

        static void ReviewPolicy()
        {
            // IT's SensitiveAuto policy decides whether captures are covered (and their names cleaned), whatever the user
            // chose; the user's own choice is what settings.ini keeps.
            string root = @"Software\StackshotTests\w6sensitive-" + Guid.NewGuid().ToString("N");
            RegistryKeyHolder keys = new RegistryKeyHolder(root);
            try
            {
                Settings s = new Settings();
                keys.Dword("SensitiveAuto", 1);
                Policy.Apply(s);
                Check(Policy.SensitiveForced && ShotStack.CoversCaptures(s), "forced on: captures are covered");
                Check(!Policy.ForFile(s).SensitiveAuto, "settings.ini keeps the user's choice (off)");
                ShotStack.CaptureStart start = new ShotStack.CaptureStart();
                start.Window = "Ficha 12345678Z";
                s.FileNameTemplate = "{ventana}";
                string name = Path.GetFileName(ShotStack.CapturePath(s, work, "Captura", start, DateTime.Now));
                Check(name == "Ficha \u2022\u2022\u2022.png", "forced on, the name: " + name);
                Settings own = new Settings();
                own.SensitiveAuto = true;
                keys.Dword("SensitiveAuto", 0);
                Policy.Apply(own);
                Check(!ShotStack.CoversCaptures(own), "forced off: captures are not covered");
                Check(Policy.ForFile(own).SensitiveAuto, "settings.ini keeps the user's choice (on)");
            }
            finally
            {
                keys.Dispose();
                Policy.Refresh();
                Check(!Policy.Any, "policies forgotten after the test");
            }
        }

        // A throwaway HKCU key standing for the user's hive in Policy (the machine's is an empty one), removed afterwards.
        sealed class RegistryKeyHolder : IDisposable
        {
            readonly string path;
            readonly Microsoft.Win32.RegistryKey machine, user;

            public RegistryKeyHolder(string path)
            {
                this.path = path;
                machine = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path + @"\Machine");
                user = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path + @"\User");
                Policy.MachineOverride = machine;
                Policy.UserOverride = user;
            }

            public void Dword(string name, int value)
            {
                using (Microsoft.Win32.RegistryKey k = user.CreateSubKey(Policy.KeyPath)) k.SetValue(name, value, Microsoft.Win32.RegistryValueKind.DWord);
            }

            public void Dispose()
            {
                Policy.MachineOverride = null;
                Policy.UserOverride = null;
                machine.Close();
                user.Close();
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
                try
                {
                    bool empty;
                    using (Microsoft.Win32.RegistryKey t = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\StackshotTests"))
                        empty = t != null && t.SubKeyCount == 0 && t.ValueCount == 0;
                    if (empty) Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(@"Software\StackshotTests", false);
                }
                catch { }
            }
        }

        static void ReviewOcrSlips()
        {
            // What Windows' OCR gives for common formats in Calibri (Office's font) at 9-10 pt: a dot of a DNI lost, a
            // space of a phone lost, four look-alikes in one IBAN group.
            Yes("DNI del titular: 1234S.678-Z", SensitiveKind.Dni);
            Yes("D.N.I.: 45.872391 H", SensitiveKind.Dni);
            Yes("12.345678Z", SensitiveKind.Dni);
            Yes("12345.678-Z", SensitiveKind.Dni);
            Yes("DNI 12.345678", SensitiveKind.Dni);
            Yes("Fijo: 91123 45 67", SensitiveKind.Phone);
            Yes("Tel\u00E9fono: +34 60012 34 56", SensitiveKind.Phone);
            Yes("600123 456", SensitiveKind.Phone);
            Yes("600 1234 56", SensitiveKind.Phone);
            Yes("IBAN ES91 2100 0418 4502 OOOS 1332", SensitiveKind.Iban);
            Yes("ES912100 0418 4502 0005 1332", SensitiveKind.Iban);
            // Still nothing: coordinates and amounts with one dot, groupings no phone has, too many look-alikes, a wrong letter.
            No("41.385064, 2.173403");
            No("12.345678");
            No("Importe 12345.678 \u20AC");
            No("12.345678A");
            No("6001 23 456");
            No("6 0012 3456");
            No("600 12 3 456");
            No("Pedido 60012 3456");
            No("ES91 2IOO O418 4502 OOOS 1332");
            No("ES91 2100 0418 4502 OOOS 1333");
            // Drawn and read back.
            string[] lines =
            {
                "DNI del titular: 12.345.678-Z",
                "D.N.I.: 45.872.391 H",
                "Tel\u00E9fono: +34 600 12 34 56",
                "Fijo: 91 123 45 67",
                "IBAN ES91 2100 0418 4502 0005 1332",
            };
            foreach (string font in new string[] { "Calibri", "Segoe UI", "Tahoma" })
                foreach (float px in new float[] { 12f, 13.33f, 14.67f })
                {
                    using (Bitmap b = new Bitmap(700, 40 + lines.Length * (int)(px * 2.2f), PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(b))
                        using (Font f = new Font(font, px, GraphicsUnit.Pixel))
                        {
                            g.Clear(Color.White);
                            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                            for (int i = 0; i < lines.Length; i++) g.DrawString(lines[i], f, Brushes.Black, 30, 20 + i * px * 2.2f);
                        }
                        List<SensitiveFinding> found = Sensitive.Find(ReadWords(b), b.Size);
                        Console.WriteLine("    " + font + " " + px.ToString(CultureInfo.InvariantCulture) + " px: " + Show(found));
                        for (int i = 0; i < lines.Length; i++)
                        {
                            float y = 20 + i * px * 2.2f;
                            Check(found.Exists(delegate(SensitiveFinding fd) { return fd.Box.Top <= y + px && fd.Box.Bottom >= y + px * 0.5f; }), font + " " + px + " px: not found \u00AB" + lines[i] + "\u00BB");
                        }
                    }
                }
        }

        static void ReviewStaleOriginal()
        {
            string temp = Path.Combine(work, "rev-stale");
            Directory.CreateDirectory(temp);
            // An earlier capture with the same name ("chrome.png", from a template without the time) left its original;
            // that capture is gone. The new one, with nothing to cover, must not keep it alive.
            string path = Path.Combine(temp, "chrome.png");
            string img = Path.Combine(CoverStore.Dir, "chrome.png"), list = Path.Combine(CoverStore.Dir, "chrome.txt");
            using (Bitmap old = Screenshot(800, 400, true))
            {
                Directory.CreateDirectory(CoverStore.Dir);
                old.Save(img, ImageFormat.Png);
                File.WriteAllText(list, "330 140 300 20 Dni\n");
            }
            using (Bitmap shot = Screenshot(800, 400, false))
            {
                Bitmap written;
                CoverOutcome r = RunJob(shot, path, false, out written);
                Check(r.Found.Count == 0, "found " + Show(r.Found));
                ShotStack.BeginWrite(path, written, null);
                ShotStack.WaitWritten(path);
                r.Preview.Dispose();
            }
            Check(!File.Exists(img) && !File.Exists(list), "the earlier capture's uncovered original outlived it");
            Check(!CoverStore.Has(path), "the new capture has no original");
            // With something to cover, the original kept is the new capture's.
            string path2 = Path.Combine(temp, "chrome (2).png");
            File.WriteAllText(Path.Combine(CoverStore.Dir, "chrome (2).txt"), "1 1 5 5 Dni\n");
            using (Bitmap shot = Screenshot(1280, 720, true))
            using (Bitmap before = new Bitmap(shot))
            {
                Bitmap written;
                CoverOutcome r = RunJob(shot, path2, false, out written);
                Check(r.Found.Count == 4, "found " + Show(r.Found));
                ShotStack.BeginWrite(path2, written, null);
                ShotStack.WaitWritten(path2);
                r.Preview.Dispose();
                List<Rectangle> boxes;
                Bitmap orig = CoverStore.Restore(path2, out boxes);
                Check(orig != null && boxes.Count == 4, "the new original");
                Check(SensitiveCover.SamePixels(orig, before), "the original kept is the new capture's");
                orig.Dispose();
            }
            CoverStore.Forget(path2);
        }
    }
}
