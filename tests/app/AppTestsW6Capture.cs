// Round 6 (pointer, file names, dated folders, repeat area), suite "w6capture". Everything is drawn or written here: the
// pointers are shapes made in the test, the images are synthetic and the folders live under STACKSHOT_TEST_DATA. No
// screen is read, no window is shown and the only hotkey call registers nothing.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D = System.Drawing;

namespace Stackshot.Tests
{
    static class AppTestsW6Capture
    {
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PrivStatic = BindingFlags.NonPublic | BindingFlags.Static;

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static void Eq<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(what + ": expected <" + expected + ">, got <" + actual + ">");
        }

        static string Root()
        {
            string root = Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA");
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            return Path.Combine(root, "w6capture");
        }

        // A fresh, empty folder for one test.
        static string Fresh(string name)
        {
            string d = Path.Combine(Root(), name);
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
            return d;
        }

        static string ShotsDir()
        {
            string d = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(d)) d = Path.Combine(Root(), "shots");
            Directory.CreateDirectory(d);
            return d;
        }

        static void Touch(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        }

        // ---- File names

        // What ShotStack.SaveCapture wrote before templates existed.
        static string OldName(string name, DateTime now)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Length > 40 ? name.Substring(0, 40) : name;
            return name + " " + now.ToString("yyyy-MM-dd HH.mm.ss");
        }

        static NameTemplate.Values Vals(string type, string app, string window, DateTime when)
        {
            NameTemplate.Values v = new NameTemplate.Values();
            v.Type = type;
            v.App = app;
            v.Window = window;
            v.When = when;
            return v;
        }

        static readonly DateTime When = new DateTime(2026, 3, 7, 9, 5, 4);

        static readonly string[] OddNames =
        {
            "Captura", "Pantalla", "chrome", "Ventana", "Desplazamiento", "Markdown", "XML",
            "Fotograma V\u00EDdeo 2026-10-12 10.00.00 (para compartir)", "a<b>c:d\"e/f\\g|h?i*j", "dos  espacios", "acaba en punto.",
            " empieza con espacio", "tab\taqu\u00ED", "\u65E5\u672C\u8A9E", "CON", "nul", "Exactamente cuarenta caracteres de nombre!",
            "Cuarenta caracteres y un espacio justo en  el corte", ""
        };

        static void DefaultIsByteForByte()
        {
            foreach (string t in new string[] { null, "", "   ", Settings.DefaultNameTemplate, "  " + Settings.DefaultNameTemplate + " " })
                foreach (string name in OddNames)
                    Eq(OldName(name, When), NameTemplate.Format(t, Vals(name, "EXCEL", "Hoja", When), 7), "default template <" + t + "> for <" + name + ">");
            // And the path SaveCapture takes, including the " (2)" of a name already used.
            string dir = Fresh("default-path");
            Settings s = new Settings();
            s.SaveFolder = Path.Combine(dir, "guardado");
            foreach (string name in OddNames)
            {
                string expected = Path.Combine(dir, OldName(name, When) + ".png");
                Eq(expected, ShotStack.CapturePath(s, dir, name, null, When), "path for <" + name + ">");
            }
            File.WriteAllBytes(Path.Combine(dir, OldName("Pantalla", When) + ".png"), new byte[1]);
            Eq(Path.Combine(dir, OldName("Pantalla", When) + " (2).png"), ShotStack.CapturePath(s, dir, "Pantalla", null, When), "taken name");
        }

        static void TokensExpand()
        {
            DateTime w = new DateTime(2026, 3, 7, 9, 5, 4);
            NameTemplate.Values v = Vals("Captura", "EXCEL", "Informe", w);
            Eq("Captura-2026-03-07-09.05.04-EXCEL-Informe-2026-03-07-12", NameTemplate.Format("{tipo}-{fecha}-{hora}-{app}-{ventana}-{a\u00F1o}-{mes}-{dia}-{n}", v, 12), "every token");
            Eq("Captura 2026-03-07", NameTemplate.Format("{TIPO} {Fecha}", v, 1), "any case");
            Eq("07 07", NameTemplate.Format("{dia} {d\u00EDa}", v, 1), "dia with and without accent");
            Eq("2026", NameTemplate.Format("{A\u00D1O}", v, 1), "a\u00F1o in capitals");
            Eq("Captura {fehca} {x", NameTemplate.Format("{tipo} {fehca} {x", v, 1), "unknown tokens stay as typed");
            Eq("{Captura}", NameTemplate.Format("{{tipo}}", v, 1), "braces around a token");
            Eq("Informe 1", NameTemplate.Format("{ventana} {n}", v, 1), "counter");
            string longType = new string('a', 39) + "\uD83D\uDE00" + "b";
            Eq(new string('a', 39), NameTemplate.Format("{tipo}", Vals(longType, "", "", w), 1), "{tipo} cut at 40 never splits a pair");
            Eq(OldName(longType, w), NameTemplate.Format(Settings.DefaultNameTemplate, Vals(longType, "", "", w), 1), "the default cuts as it always did");
            Check(NameTemplate.UsesCounter("{tipo} {N}") && !NameTemplate.UsesCounter("{tipo}") && !NameTemplate.UsesCounter(Settings.DefaultNameTemplate), "UsesCounter");
            Check(NameTemplate.Uses("{App}", "{app}") && !NameTemplate.Uses(null, "{app}") && !NameTemplate.Uses("", "{n}"), "Uses");
            Check(NameTemplate.IsDefault(null) && NameTemplate.IsDefault(" ") && NameTemplate.IsDefault(" {tipo} {fecha} {hora} ") && !NameTemplate.IsDefault("{tipo} {fecha}"), "IsDefault");
            List<string> unknown = NameTemplate.Unknown("{tipo} {fehca} {N} {otro} {fehca}");
            Eq("{fehca}|{otro}", string.Join("|", unknown.ToArray()), "unknown tokens listed once");
            Eq(9, NameTemplate.Tokens.Length, "tokens offered");
            foreach (string t in NameTemplate.Tokens) Eq(0, NameTemplate.Unknown(t).Count, "offered token is known: " + t);
        }

        static void NamesAreSafe()
        {
            NameTemplate.Values v = Vals("Captura", "app", "C:\\docs\\a:b*c?d\"e<f>g|h", When);
            Eq("C__docs_a_b_c_d_e_f_g_h", NameTemplate.Format("{ventana}", v, 1), "forbidden characters");
            Eq("a b c d", NameTemplate.Format("a   b\t\tc\r\nd", v, 1), "spaces collapsed");
            Eq("Captura", NameTemplate.Format("  {tipo} ... . ", v, 1), "trailing dots and spaces, leading spaces");
            Eq(".oculto Captura", NameTemplate.Format(".oculto {tipo}", v, 1), "a leading dot stays");
            Eq("___", NameTemplate.Format("???", v, 1), "only forbidden characters is still a name");
            // Never empty: falls back to the name Stackshot always gave.
            foreach (string t in new string[] { "...", " . . ", "{app}", "{ventana}" })
                Eq(OldName("Captura", When), NameTemplate.Format(t, Vals("Captura", "", "", When), 1), "empty result for <" + t + ">");
            // Devices.
            Eq("CON_", NameTemplate.Format("CON", v, 1), "CON");
            Eq("nul_", NameTemplate.Format("nul", v, 1), "nul");
            Eq("COM1_", NameTemplate.Format("{app}", Vals("x", "COM1", "", When), 1), "COM1 from a token");
            Eq("LPT9_.algo", NameTemplate.Format("LPT9.algo", v, 1), "device before a dot");
            Eq("AUX_ .x", NameTemplate.Format("AUX .x", v, 1), "device with spaces before the dot");
            Eq("CON_", NameTemplate.Format("CON.", v, 1), "a device left by trimming a dot");
            Eq("CONSOLA", NameTemplate.Format("CONSOLA", v, 1), "a longer word is no device");
            Eq("con x", NameTemplate.Format("con x", v, 1), "a device word followed by more is fine");
            // Length.
            string longTitle = new string('a', 200) + " fin";
            string n = NameTemplate.Format("{ventana}", Vals("x", "", longTitle, When), 1);
            Eq(NameTemplate.MaxLength, n.Length, "cut to 120");
            string spaced = new string('b', 119) + "    final";
            Eq(new string('b', 119), NameTemplate.Format(spaced, v, 1), "no trailing space left by the cut");
            string emoji = new string('c', 119) + "\uD83D\uDE00" + "zz";
            string e = NameTemplate.Format(emoji, v, 1);
            Check(e.Length <= NameTemplate.MaxLength && !char.IsHighSurrogate(e[e.Length - 1]), "a surrogate pair is never split: " + e.Length);
            Eq(new string('c', 119), e, "the pair that does not fit goes whole");
            // Every result is a name Windows takes.
            string dir = Fresh("safe");
            foreach (string t in new string[] { "{ventana}", "CON", "a   b", "LPT9.algo", "???", spaced, emoji })
            {
                string file = Path.Combine(dir, NameTemplate.Format(t, v, 1) + ".png");
                File.WriteAllBytes(file, new byte[1]);
                Check(File.Exists(file) && new FileInfo(file).Length == 1, "Windows refused " + file);
            }
        }

        static void CounterGoesPastUsedNumbers()
        {
            string temp = Fresh("counter-temp"), save = Fresh("counter-save");
            Settings s = new Settings();
            s.SaveFolder = save;
            s.FileNameTemplate = "{tipo} {fecha} {n}";
            DateTime day = new DateTime(2026, 10, 12, 17, 3, 26);
            NameTemplate.Values v = Vals("Captura", "", "", day);
            Eq(1L, NameTemplate.FirstCounter(s.FileNameTemplate, v, ".png", temp, save), "nothing yet");
            Eq(Path.Combine(temp, "Captura 2026-10-12 1.png"), ShotStack.CapturePath(s, temp, "Captura", null, day), "first of the day");
            Touch(Path.Combine(temp, "Captura 2026-10-12 1.png"));
            Touch(Path.Combine(temp, "Captura 2026-10-12 3.png"));
            Touch(Path.Combine(save, "Captura 2026-10-12 7.png"));
            Touch(Path.Combine(save, "Captura 2026-10-11 9.png"));     // another day
            Touch(Path.Combine(save, "Captura 2026-10-12 x.png"));     // not a number
            Touch(Path.Combine(save, "Pantalla 2026-10-12 50.png"));   // another type
            Touch(Path.Combine(save, "Captura 2026-10-12 40.jpg"));    // another extension
            Eq(8L, NameTemplate.FirstCounter(s.FileNameTemplate, v, ".png", temp, save), "one past the highest of the day in both folders");
            Eq(Path.Combine(temp, "Captura 2026-10-12 8.png"), ShotStack.CapturePath(s, temp, "Captura", null, day), "next name");
            Eq(Path.Combine(temp, "Pantalla 2026-10-12 51.png"), ShotStack.CapturePath(s, temp, "Pantalla", null, day), "per type");
            Eq(Path.Combine(temp, "Captura 2026-10-13 1.png"), ShotStack.CapturePath(s, temp, "Captura", null, day.AddDays(1)), "a new day starts at 1");
            // Kept captures in a monthly folder count too.
            s.SaveSubfolder = 1;
            Touch(Path.Combine(save, "2026-10", "Captura 2026-10-12 20.png"));
            Eq(Path.Combine(temp, "Captura 2026-10-12 21.png"), ShotStack.CapturePath(s, temp, "Captura", null, day), "the dated folder counts");
            // {n} twice: probed one by one.
            s.SaveSubfolder = 0;
            s.FileNameTemplate = "{n}-{n}";
            Touch(Path.Combine(temp, "1-1.png"));
            Touch(Path.Combine(save, "2-2.png"));
            Eq(Path.Combine(temp, "3-3.png"), ShotStack.CapturePath(s, temp, "Captura", null, day), "counter used twice");
            // A title so long that the counter is cut off: " (2)" right away instead of trying number after number.
            s.FileNameTemplate = "{ventana} {n}";
            ShotStack.CaptureStart longStart = new ShotStack.CaptureStart();
            longStart.Window = new string('v', 130);
            Touch(Path.Combine(temp, new string('v', 120) + ".png"));
            Eq(Path.Combine(temp, new string('v', 120) + " (2).png"), ShotStack.CapturePath(s, temp, "Captura", longStart, day), "counter cut off");
            // Without {n} a repeated name gets " (2)".
            s.FileNameTemplate = "{tipo} {fecha}";
            Touch(Path.Combine(temp, "Captura 2026-10-12.png"));
            Eq(Path.Combine(temp, "Captura 2026-10-12 (2).png"), ShotStack.CapturePath(s, temp, "Captura", null, day), "no counter");
            // A missing folder is no error.
            s.FileNameTemplate = "{tipo} {n}";
            Eq(1L, NameTemplate.FirstCounter(s.FileNameTemplate, v, ".png", Path.Combine(temp, "no-existe"), null, ""), "missing folders");
        }

        static void TemplateUsesTheStartOfTheCapture()
        {
            string temp = Fresh("start");
            Settings s = new Settings();
            s.SaveFolder = Path.Combine(temp, "guardado");
            s.FileNameTemplate = "{app} - {ventana} {hora}";
            ShotStack.CaptureStart start = new ShotStack.CaptureStart();
            start.App = "EXCEL";
            start.Window = "Informe: ventas/2026";
            Eq(Path.Combine(temp, "EXCEL - Informe_ ventas_2026 09.05.04.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "app and title from the start");
            // Off by default: nothing is read when the template doesn't ask for it, and no pointer without the option.
            Settings plain = new Settings();
            using (ShotStack.CaptureStart c = ShotStack.CaptureStart.Take(plain))
                Check(c.App == "" && c.Window == "" && c.Pointer == null, "a default capture start read something");
            Check(!plain.CaptureCursor && plain.FileNameTemplate == Settings.DefaultNameTemplate && plain.SaveSubfolder == 0 && plain.HotRepeat == "", "defaults");
        }

        static void SettingsHints()
        {
            bool warn;
            string h = HomeWindow.NameHint("{tipo} {fehca}", out warn);
            Check(warn && h.Contains("{fehca}"), "unknown token: " + h);
            h = HomeWindow.NameHint("{tipo}: {fecha}", out warn);
            Check(warn && h.Contains("se cambian por _"), "forbidden character: " + h);
            h = HomeWindow.NameHint("", out warn);
            Check(!warn && h.Contains("nombre de siempre"), "empty: " + h);
            h = HomeWindow.NameHint("{tipo} {fecha}", out warn);
            Check(!warn && h.Contains("(2)"), "no hour nor counter: " + h);
            h = HomeWindow.NameHint(Settings.DefaultNameTemplate, out warn);
            Check(!warn && h.Contains("v\u00EDdeos"), "default: " + h);
            h = HomeWindow.NameHint("{tipo} {n}", out warn);
            Check(!warn && !h.Contains("(2)"), "with a counter: " + h);
            Check(HomeWindow.NamePreview(Settings.DefaultNameTemplate).StartsWith("Ejemplo: Captura 20") && HomeWindow.NamePreview("{app}").EndsWith("EXCEL.png"), "preview");
            DateTime oct = new DateTime(2026, 10, 12);
            Check(HomeWindow.SubfolderNote(0, oct).Length > 0, "no subfolders");
            Check(HomeWindow.SubfolderNote(1, oct).Contains("2026-10"), "monthly: " + HomeWindow.SubfolderNote(1, oct));
            Check(HomeWindow.SubfolderNote(2, oct).Contains("2026\\10"), "yearly: " + HomeWindow.SubfolderNote(2, oct));
            foreach (string t in new string[] { Settings.DefaultNameTemplate, "{app}", "", "CON", "{n}" })
                Check(!HomeWindow.NamePreview(t).Contains("Sin atajo"), "preview");
        }

        // ---- The pointer

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref Native.ICONINFO info);
        [DllImport("user32.dll")] static extern IntPtr CreateCursor(IntPtr inst, int hx, int hy, int w, int h, byte[] and, byte[] xor);
        [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr h);
        [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int w, int h, uint planes, uint bpp, byte[] bits);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        // A 32 x 32 colour pointer with alpha: opaque red in x 0-9, y 0-13; half-transparent blue in x 12-19, y 0-5; the rest
        // fully transparent. Hot spot at (hx, hy).
        static IntPtr ColorPointer(int hx, int hy)
        {
            const int S = 32;
            int[] px = new int[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (x < 10 && y < 14) px[y * S + x] = unchecked((int)0xFFFF0000);
                    else if (x >= 12 && x < 20 && y < 6) px[y * S + x] = unchecked((int)0x800000FF);
                }
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bi.biWidth = S;
            bi.biHeight = -S;
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            IntPtr bits;
            IntPtr color = CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            Marshal.Copy(px, 0, bits, px.Length);
            byte[] mask = new byte[S * S / 8];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (((uint)px[y * S + x] >> 24) == 0) mask[y * (S / 8) + x / 8] |= (byte)(0x80 >> (x % 8));
            IntPtr monochrome = CreateBitmap(S, S, 1, 1, mask);
            Native.ICONINFO ii = new Native.ICONINFO();
            ii.fIcon = false;
            ii.xHotspot = hx;
            ii.yHotspot = hy;
            ii.hbmMask = monochrome;
            ii.hbmColor = color;
            IntPtr h = CreateIconIndirect(ref ii);
            Native.DeleteObject(monochrome);
            Native.DeleteObject(color);
            Check(h != IntPtr.Zero, "could not make the test pointer");
            return h;
        }

        // A 32 x 32 monochrome pointer that inverts the screen in x 4-11, y 4-11 and leaves the rest alone (like the I-beam).
        static IntPtr InvertingPointer()
        {
            byte[] and = new byte[128], xor = new byte[128];
            for (int i = 0; i < and.Length; i++) and[i] = 0xFF;
            for (int y = 4; y < 12; y++)
                for (int x = 4; x < 12; x++) xor[y * 4 + x / 8] |= (byte)(0x80 >> (x % 8));
            IntPtr h = CreateCursor(Marshal.GetHINSTANCE(typeof(AppTestsW6Capture).Module), 0, 0, 32, 32, and, xor);
            Check(h != IntPtr.Zero, "could not make the inverting pointer");
            return h;
        }

        static Bitmap Plain(int w, int h, D.Color c)
        {
            Bitmap b = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.Clear(c);
            return b;
        }

        static string Px(D.Color c) { return c.A + "," + c.R + "," + c.G + "," + c.B; }

        static bool Near(D.Color c, int a, int r, int g, int b, int tol)
        {
            return c.A == a && Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol;
        }

        static void PointerSizeRules()
        {
            Eq(32, PointerShot.SizeFor(32, 96, 96, 32), "same DPI");
            Eq(48, PointerShot.SizeFor(32, 144, 96, 32), "system-sized shape on a 150 % monitor");
            Eq(48, PointerShot.SizeFor(48, 144, 96, 32), "already the monitor's size");
            Eq(32, PointerShot.SizeFor(48, 96, 144, 32), "system 150 %, pointer on a 100 % monitor");
            Eq(40, PointerShot.SizeFor(32, 120, 96, 32), "125 %");
            Eq(96, PointerShot.SizeFor(64, 144, 96, 64), "larger pointer chosen in Accessibility");
            Eq(48, PointerShot.SizeFor(48, 96, 144, 48), "a larger pointer already at the monitor's size is not mistaken for a 32 px one");
            Eq(48, PointerShot.SizeFor(72, 96, 144, 48), "a larger pointer at the system's size");
            Eq(32, PointerShot.SizeFor(32, 96, 144, 48), "an app's 32 px pointer already at the monitor's size");
            Eq(20, PointerShot.SizeFor(20, 144, 96, 32), "an app's own size stays");
            Eq(32, PointerShot.SizeFor(32, 0, 96, 32), "unknown DPI");
            Eq(0, PointerShot.SizeFor(0, 144, 96, 32), "no shape");
        }

        static void PointerDrawnWhereItWas()
        {
            IntPtr h = ColorPointer(2, 3);
            try
            {
                using (PointerShot p = PointerShot.From(h, new D.Point(1052, 563), 96, 96, 32))
                using (Bitmap img = Plain(200, 150, D.Color.White))
                {
                    Check(p != null, "no snapshot");
                    Eq(new D.Size(32, 32), p.Size, "size");
                    Eq(new D.Point(2, 3), p.Hotspot, "hot spot");
                    Check(p.DrawInto(img, new D.Point(1000, 500)), "not drawn");
                    // The shape's top-left lands at (1052 - 2 - 1000, 563 - 3 - 500) = (50, 60).
                    Check(Near(img.GetPixel(50, 60), 255, 255, 0, 0, 0), "red at the top-left: " + Px(img.GetPixel(50, 60)));
                    Check(Near(img.GetPixel(59, 73), 255, 255, 0, 0, 0), "red at the bottom-right: " + Px(img.GetPixel(59, 73)));
                    Check(Near(img.GetPixel(60, 60), 255, 255, 255, 255, 0), "transparent part keeps the capture: " + Px(img.GetPixel(60, 60)));
                    Check(Near(img.GetPixel(49, 60), 255, 255, 255, 255, 0), "left of it untouched: " + Px(img.GetPixel(49, 60)));
                    Check(Near(img.GetPixel(64, 62), 255, 128, 128, 255, 3), "half-transparent part blends: " + Px(img.GetPixel(64, 62)));
                    for (int y = 0; y < img.Height; y++)
                        for (int x = 0; x < img.Width; x++)
                            if (img.GetPixel(x, y).A != 255) throw new InvalidOperationException("transparent pixel at " + x + "," + y);
                }
            }
            finally { Native.DestroyIcon(h); }
        }

        static void PointerOutsideIsLeftOut()
        {
            IntPtr h = ColorPointer(0, 0);
            try
            {
                using (Bitmap img = Plain(100, 80, D.Color.FromArgb(255, 20, 40, 60)))
                {
                    byte[] before = Bytes(img);
                    foreach (D.Point at in new D.Point[] { new D.Point(-1, 10), new D.Point(10, -1), new D.Point(100, 10), new D.Point(10, 80), new D.Point(-30, -30), new D.Point(500, 500) })
                        using (PointerShot p = PointerShot.From(h, at, 96, 96, 32))
                            Check(!p.DrawInto(img, D.Point.Empty), "drawn with its hot spot outside at " + at);
                    Check(Same(before, Bytes(img)), "the image changed");
                    // Its body overlapping the area while the hot spot is outside does not count either.
                    using (PointerShot p = PointerShot.From(ColorPointerOwned(10, 10), new D.Point(5, 5), 96, 96, 32))
                        Check(!p.DrawInto(img, new D.Point(6, 6)), "drawn with the hot spot just outside");
                }
                // Inside but at the edge: drawn and cut, no exception; the last pixel counts as inside.
                using (Bitmap img = Plain(100, 80, D.Color.White))
                {
                    using (PointerShot p = PointerShot.From(h, new D.Point(99, 79), 96, 96, 32))
                        Check(p.DrawInto(img, D.Point.Empty), "not drawn at the last pixel");
                    Check(Near(img.GetPixel(99, 79), 255, 255, 0, 0, 0), "corner pixel red: " + Px(img.GetPixel(99, 79)));
                    using (PointerShot p = PointerShot.From(h, new D.Point(-200, -100), 96, 96, 32))
                        Check(p.DrawInto(img, new D.Point(-205, -110)), "monitors left of and above the primary (negative coordinates)");
                    Check(Near(img.GetPixel(5, 10), 255, 255, 0, 0, 0), "drawn at negative coordinates: " + Px(img.GetPixel(5, 10)));
                }
            }
            finally { Native.DestroyIcon(h); }
        }

        static readonly List<IntPtr> owned = new List<IntPtr>();

        static IntPtr ColorPointerOwned(int hx, int hy)
        {
            IntPtr h = ColorPointer(hx, hy);
            owned.Add(h);
            return h;
        }

        static byte[] Bytes(Bitmap b)
        {
            BitmapData bd = b.LockBits(new D.Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] r = new byte[bd.Stride * b.Height];
                Marshal.Copy(bd.Scan0, r, 0, r.Length);
                return r;
            }
            finally { b.UnlockBits(bd); }
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void InvertingPointerAsOnScreen()
        {
            IntPtr h = InvertingPointer();
            try
            {
                foreach (int shade in new int[] { 255, 0, 128 })
                    using (Bitmap img = Plain(64, 64, D.Color.FromArgb(255, shade, shade, shade)))
                    using (PointerShot p = PointerShot.From(h, new D.Point(10, 10), 96, 96, 32))
                    {
                        Check(p.DrawInto(img, D.Point.Empty), "not drawn");
                        D.Color inside = img.GetPixel(17, 17), outside = img.GetPixel(30, 30);
                        Check(Near(inside, 255, 255 - shade, 255 - shade, 255 - shade, 0), "inverted on " + shade + ": " + Px(inside));
                        Check(Near(outside, 255, shade, shade, shade, 0), "left alone on " + shade + ": " + Px(outside));
                    }
            }
            finally { DestroyCursor(h); }
        }

        static void PointerScaledToTheMonitor()
        {
            IntPtr h = ColorPointer(2, 4);
            try
            {
                using (PointerShot p = PointerShot.From(h, new D.Point(20, 20), 144, 96, 32))
                using (Bitmap img = Plain(120, 120, D.Color.White))
                {
                    Eq(new D.Size(48, 48), p.Size, "scaled size");
                    Eq(new D.Point(3, 6), p.Hotspot, "scaled hot spot");
                    Check(p.DrawInto(img, D.Point.Empty), "not drawn");
                    // Red was 10 x 14 at 100 %: 15 x 21 now, from (20 - 3, 20 - 6) to (31, 34).
                    Check(Near(img.GetPixel(17, 14), 255, 255, 0, 0, 8) && Near(img.GetPixel(30, 33), 255, 255, 0, 0, 8),
                          "scaled red block: " + Px(img.GetPixel(17, 14)) + " / " + Px(img.GetPixel(30, 33)));
                    Check(Near(img.GetPixel(33, 14), 255, 255, 255, 255, 0) && Near(img.GetPixel(17, 37), 255, 255, 255, 255, 0),
                          "not larger than scaled: " + Px(img.GetPixel(33, 14)) + " / " + Px(img.GetPixel(17, 37)));
                }
            }
            finally { Native.DestroyIcon(h); }
        }

        static void PointerSnapshotLifetime()
        {
            IntPtr h = ColorPointer(0, 0);
            PointerShot p = PointerShot.From(h, new D.Point(5, 5), 96, 96, 32);
            // The copy outlives the original shape (an app dropping its pointer while the area is chosen).
            Native.DestroyIcon(h);
            using (Bitmap img = Plain(40, 40, D.Color.White))
            {
                Check(p.DrawInto(img, D.Point.Empty), "the copy did not survive the original");
                Check(Near(img.GetPixel(6, 6), 255, 255, 0, 0, 0), "drawn from the copy");
                p.Dispose();
                p.Dispose();
                Check(!p.DrawInto(img, D.Point.Empty), "drawn after Dispose");
            }
            Check(PointerShot.From(IntPtr.Zero, D.Point.Empty, 96, 96, 32) == null, "no handle");
            Check(PointerShot.From(new IntPtr(0x7A5A0), D.Point.Empty, 96, 96, 32) == null, "a handle that is no pointer");
            // Through the capture start, which never lets a broken image stop the capture.
            ShotStack.CaptureStart start = new ShotStack.CaptureStart();
            using (Bitmap img = Plain(40, 40, D.Color.White))
            {
                start.DrawPointer(img, D.Point.Empty);
                Check(Near(img.GetPixel(6, 6), 255, 255, 255, 255, 0), "drawn without a pointer");
                start.Pointer = PointerShot.From(ColorPointerOwned(0, 0), new D.Point(5, 5), 96, 96, 32);
                start.DrawPointer(img, D.Point.Empty);
                Check(Near(img.GetPixel(6, 6), 255, 255, 0, 0, 0), "not drawn through the capture start");
                Bitmap gone = Plain(10, 10, D.Color.White);
                gone.Dispose();
                start.DrawPointer(gone, D.Point.Empty);
                start.DrawPointer(null, D.Point.Empty);
                start.Dispose();
                Check(start.Pointer == null, "pointer kept after Dispose");
                start.Dispose();
            }
            foreach (IntPtr o in owned) Native.DestroyIcon(o);
            owned.Clear();
        }

        // ---- Repeat the last area

        static List<KeyValuePair<string, D.Rectangle>> Mons(params object[] pairs)
        {
            List<KeyValuePair<string, D.Rectangle>> l = new List<KeyValuePair<string, D.Rectangle>>();
            for (int i = 0; i < pairs.Length; i += 2) l.Add(new KeyValuePair<string, D.Rectangle>((string)pairs[i], (D.Rectangle)pairs[i + 1]));
            return l;
        }

        static void LastAreaFits()
        {
            D.Rectangle left = new D.Rectangle(-1920, 0, 1920, 1080), main = new D.Rectangle(0, 0, 2560, 1440), right = new D.Rectangle(2560, 200, 1280, 1024);
            List<KeyValuePair<string, D.Rectangle>> now = Mons(@"\\.\DISPLAY1", main, @"\\.\DISPLAY2", left, @"\\.\DISPLAY3", right);
            ShotStack.LastArea a = ShotStack.MakeArea(new D.Rectangle(100, 100, 400, 300), IntPtr.Zero, now);
            Eq(1, a.Monitors.Count, "monitors it touches");
            Check(ShotStack.AreaFits(a, now), "same monitors");
            Check(!ShotStack.AreaFits(null, now), "no area yet");
            Check(!ShotStack.AreaFits(a, Mons(@"\\.\DISPLAY2", left)), "its monitor unplugged");
            Check(!ShotStack.AreaFits(a, Mons(@"\\.\DISPLAY1", new D.Rectangle(0, 0, 1920, 1080), @"\\.\DISPLAY2", left)), "another resolution");
            Check(!ShotStack.AreaFits(a, Mons(@"\\.\DISPLAY4", main)), "another monitor in its place");
            Check(ShotStack.AreaFits(a, Mons(@"\\.\display1", main)), "names in any case");
            Check(ShotStack.AreaFits(a, Mons(@"\\.\DISPLAY1", main)), "a monitor it didn't touch is gone");
            ShotStack.LastArea across = ShotStack.MakeArea(new D.Rectangle(-200, 300, 600, 200), IntPtr.Zero, now);
            Eq(2, across.Monitors.Count, "across two monitors");
            Check(ShotStack.AreaFits(across, now), "both still there");
            Check(!ShotStack.AreaFits(across, Mons(@"\\.\DISPLAY1", main)), "one of them gone");
            Check(!ShotStack.AreaFits(ShotStack.MakeArea(new D.Rectangle(9000, 9000, 50, 50), IntPtr.Zero, now), now), "off every monitor");
            Check(!ShotStack.AreaFits(ShotStack.MakeArea(D.Rectangle.Empty, IntPtr.Zero, now), now), "empty area");
            Check(ShotStack.AreaFits(ShotStack.MakeArea(new D.Rectangle(0, 0, 1, 1), IntPtr.Zero, now), now), "one pixel");
        }

        static void RepeatActionSettings()
        {
            Settings s = new Settings();
            Eq("", s.HotRepeat, "no shortcut by default");
            Eq(0, Hotkeys.Split(s.HotkeysFor("repeat")).Count, "HotkeysFor(repeat)");
            Eq(1, Array.IndexOf(Settings.Actions, "repeat"), "right after the area");
            s.SetHotkeys("repeat", "Ctrl+Alt+F11");
            Eq("Ctrl+Alt+F11", s.HotRepeat, "SetHotkeys(repeat)");
            Check(s.HotGif == "Ctrl+Shift+PrintScreen" && s.HotRegion == "PrintScreen", "SetHotkeys(repeat) touched another action");
            string dir = Fresh("settings");
            string path = Path.Combine(dir, "settings.ini");
            MethodInfo write = typeof(Settings).GetMethod("Write", Priv);
            write.Invoke(new Settings(), new object[] { path });
            string text = File.ReadAllText(path);
            Check(text.Contains("\nMarkdown = Ctrl+Shift+Alt+M\r\nRepeat = \r\n"), "default Repeat line: " + text.Substring(0, Math.Min(600, text.Length)));
            Eq("", Settings.Load(path).HotRepeat, "empty read back as empty");
            write.Invoke(s, new object[] { path });
            Eq("Ctrl+Alt+F11", Settings.Load(path).HotRepeat, "read back");
            File.WriteAllText(path, "Region = PrintScreen\r\nRepeat = Ctrl+Shift+F12\r\n");
            Eq("Ctrl+Shift+F12", Settings.Load(path).HotRepeat, "hand-written");
            File.WriteAllText(path, "Region = PrintScreen\r\n");
            Eq("", Settings.Load(path).HotRepeat, "older file without the line");
            // Restoring the defaults leaves it without a shortcut.
            Settings d = new Settings();
            foreach (string a in Settings.Actions) s.SetHotkeys(a, d.HotkeysFor(a));
            Eq("", s.HotRepeat, "restored");
        }

        static Settings Blank()
        {
            Settings s = new Settings();
            foreach (string a in Settings.Actions) s.SetHotkeys(a, "");
            return s;
        }

        // Nothing is registered for it by default, and an action without a shortcut is no reason for a notice.
        static void NothingRegisteredByDefault()
        {
            using (Hotkeys hk = new Hotkeys())
            {
                List<string> failed = hk.Register("repeat", new Settings().HotRepeat);
                Eq(0, failed.Count, "failures");
                System.Collections.IDictionary ids = (System.Collections.IDictionary)typeof(Hotkeys).GetField("ids", Priv).GetValue(hk);
                Eq(0, ids.Count, "registered combos");
            }
            using (Hotkeys hk = new Hotkeys())
            using (System.Windows.Forms.NotifyIcon tray = new System.Windows.Forms.NotifyIcon())
            {
                ShotStack st = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
                SetField(st, "settings", Blank());
                SetField(st, "hotkeys", hk);
                SetField(st, "tray", tray);
                ShotStack.ForgetRecorded();
                typeof(ShotStack).GetMethod("RegisterHotkeys", Priv).Invoke(st, new object[] { true });
                Check(ShotStack.Recorded.Count == 0, "a notice for actions without shortcuts: " + (ShotStack.Recorded.Count > 0 ? ShotStack.Recorded[0].Text : ""));
            }
        }

        static void SetField(object o, string field, object value)
        {
            FieldInfo f = o.GetType().GetField(field, Priv);
            Check(f != null, "no field " + field + " in " + o.GetType().Name);
            f.SetValue(o, value);
        }

        static object Field(object o, string field)
        {
            FieldInfo f = o.GetType().GetField(field, Priv);
            Check(f != null, "no field " + field + " in " + o.GetType().Name);
            return f.GetValue(o);
        }

        // A table of HomeWindow, kept as a field or (when it follows the language) as a property.
        static string[] Strings(string name)
        {
            FieldInfo f = typeof(HomeWindow).GetField(name, PrivStatic);
            if (f != null) return (string[])f.GetValue(null);
            PropertyInfo p = typeof(HomeWindow).GetProperty(name, PrivStatic);
            return p == null ? null : (string[])p.GetValue(null, null);
        }

        static void ArraysStayAligned()
        {
            foreach (string name in new string[] { "ActionNames", "ActionShort", "ActionIcons", "ActionShortest" })
            {
                string[] a = Strings(name);
                Check(a != null, "no " + name);
                Eq(Settings.Actions.Length, a.Length, name + " length");
            }
            D.Color[,] colors = (D.Color[,])typeof(HomeWindow).GetField("ActionColors", PrivStatic).GetValue(null);
            Eq(Settings.Actions.Length, colors.GetLength(0), "ActionColors length");
            string[] icons = (string[])typeof(HomeWindow).GetField("ActionIcons", PrivStatic).GetValue(null);
            foreach (string icon in icons) Check(Glyph.Has(icon), "no glyph " + icon);
            string[] names = Strings("ActionNames");
            Eq("Repetir la \u00FAltima \u00E1rea", names[Array.IndexOf(Settings.Actions, "repeat")], "name of the action");
            Eq("Capturar un \u00E1rea", names[Array.IndexOf(Settings.Actions, "region")], "the area keeps its name");
            int[] home = (int[])typeof(HomeWindow).GetField("HomeModeIndex", PrivStatic).GetValue(null);
            // The repeat and the timed area are ways of taking an area (tray and shortcuts), not modes of their own.
            Eq(Settings.Actions.Length - 2, home.Length, "Home lists every mode but the repeat and the timed area");
            foreach (int i in home) Check(Settings.Actions[i] != "repeat" && Settings.Actions[i] != "delayed", "Home lists " + Settings.Actions[i]);
        }

        // ---- Tray, keys page, Home and the mascot

        static object TrayItem(TrayPanel tp, string id)
        {
            typeof(TrayPanel).GetMethod("BuildItems", Priv).Invoke(tp, new object[] { false, false, false });
            foreach (object it in (System.Collections.IEnumerable)typeof(TrayPanel).GetField("items", Priv).GetValue(tp))
                if (string.Equals((string)it.GetType().GetField("Id").GetValue(it), id, StringComparison.Ordinal)) return it;
            return null;
        }

        static void TrayOffersRepeat()
        {
            object row = TrayItem(new TrayPanel(new Settings()), "repeat");
            Check(row != null, "no repeat row in the tray");
            Eq("Repetir \u00E1rea", (string)row.GetType().GetField("Text").GetValue(row), "label");
            Check(row.GetType().GetField("Caps").GetValue(row) == null, "keycaps for an action without a shortcut");
            Settings s = new Settings();
            s.HotRepeat = "Ctrl+Alt+F11";
            object withKey = TrayItem(new TrayPanel(s), "repeat");
            string[] caps = (string[])withKey.GetType().GetField("Caps").GetValue(withKey);
            Check(caps != null && caps[caps.Length - 1] == "F11", "keycaps of a shortcut");
            string[] captures = (string[])typeof(TrayPanel).GetField("Captures", PrivStatic).GetValue(null);
            Check(Array.IndexOf(captures, "repeat") >= 0, "the repeat gives the focus back like the other captures");
        }

        static HomeWindow Make(int appearance, Settings s)
        {
            Ds.Apply(appearance);
            s.Appearance = appearance;
            ShotStack owner = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            return new HomeWindow(owner, s);
        }

        static void SetPage(HomeWindow hw, string id)
        {
            typeof(HomeWindow).GetMethod("SetPage", Priv).Invoke(hw, new object[] { id, false });
        }

        static void Collect(object o, List<string> acc)
        {
            TextBlock tb = o as TextBlock;
            if (tb != null && tb.Text != null) acc.Add(tb.Text);
            DependencyObject d = o as DependencyObject;
            if (d == null) return;
            foreach (object c in LogicalTreeHelper.GetChildren(d)) Collect(c, acc);
        }

        static List<string> Texts(HomeWindow hw)
        {
            List<string> acc = new List<string>();
            Collect(hw.Content, acc);
            return acc;
        }

        static bool Has(List<string> texts, string part)
        {
            foreach (string t in texts) if (t.IndexOf(part, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static void SavePng(UIElement root, System.Windows.Media.Brush background, int w, int h, string file)
        {
            root.Measure(new System.Windows.Size(w, h));
            root.Arrange(new Rect(0, 0, w, h));
            root.UpdateLayout();
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(background, null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(Path.Combine(ShotsDir(), file))) enc.Save(fs);
        }

        static void KeysPageRow(int appearance, string file)
        {
            HomeWindow hw = Make(appearance, new Settings());
            SetPage(hw, "keys");
            List<string> texts = Texts(hw);
            SavePng((UIElement)hw.Content, hw.Background, 980, 2600, file);
            Check(Has(texts, "Repetir la \u00FAltima \u00E1rea"), "keys page: no repeat row");
            Check(!Has(texts, "lo est\u00E1 usando otro programa"), "keys page blames another program");
            Check(!Has(texts, "Restaurar los atajos de serie"), "the defaults look changed");
        }

        static void GeneralPageRows(int appearance, string file)
        {
            Settings s = new Settings();
            HomeWindow hw = Make(appearance, s);
            SetPage(hw, "general");
            List<string> texts = Texts(hw);
            SavePng((UIElement)hw.Content, hw.Background, 980, 4600, file);
            Check(Has(texts, "Nombre de los archivos"), "no template row");
            Check(Has(texts, "Subcarpetas"), "no subfolders row");
            Check(Has(texts, "Ejemplo: Captura 20"), "no example name");
            Check(Has(texts, "{tipo}") && Has(texts, "{ventana}") && Has(texts, "{n}"), "tokens not listed");
            Check(Has(texts, "Todo va directamente a la carpeta elegida."), "subfolder note");
        }

        static T Find<T>(DependencyObject root) where T : class
        {
            if (root == null) return null;
            T t = root as T;
            if (t != null) return t;
            foreach (object c in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject d = c as DependencyObject;
                T found = d == null ? null : Find<T>(d);
                if (found != null) return found;
            }
            return null;
        }

        // Typing applies the template at once (the file is written when the field is left, which needs a shown window).
        static void TemplateRowTyping()
        {
            Settings s = new Settings();
            HomeWindow hw = Make(1, s);
            SetPage(hw, "general");
            HomeWindow.NameTemplateRow row = Find<HomeWindow.NameTemplateRow>(hw.Content as DependencyObject);
            Check(row != null, "no template row");
            TextBox box = (TextBox)Field(row, "box");
            TextBlock hint = (TextBlock)Field(row, "hint"), reset = (TextBlock)Field(row, "reset");
            Eq(Settings.DefaultNameTemplate, box.Text, "the field shows the template");
            Check(reset.Visibility != Visibility.Visible, "Restablecer shown for the default");
            box.Text = "{app} {fehca}";
            Eq("{app} {fehca}", s.FileNameTemplate, "applied at once");
            Check(reset.Visibility == Visibility.Visible, "no Restablecer for another template");
            Check(hint.Text.Contains("{fehca}"), "hint: " + hint.Text);
            List<string> texts = Texts(hw);
            Check(Has(texts, "Ejemplo: EXCEL {fehca}.png"), "example follows the typing");
            box.Text = "   ";
            Eq(Settings.DefaultNameTemplate, s.FileNameTemplate, "an empty field means the default");
            box.Text = "{tipo}";
            box.SelectionStart = box.Text.Length;
            typeof(HomeWindow.NameTemplateRow).GetMethod("Insert", Priv).Invoke(row, new object[] { "{n}" });
            Eq("{tipo} {n}", box.Text, "a token goes in with a space");
            box.SelectionStart = 0;
            box.SelectionLength = 6;
            typeof(HomeWindow.NameTemplateRow).GetMethod("Insert", Priv).Invoke(row, new object[] { "{app}" });
            Eq("{app} {n}", box.Text, "a token replaces the selection");
            Eq("{app} {n}", s.FileNameTemplate, "and applies");
            box.Text = "{tipo}{hora}";
            box.SelectionStart = 6;
            box.SelectionLength = 0;
            typeof(HomeWindow.NameTemplateRow).GetMethod("Insert", Priv).Invoke(row, new object[] { "{n}" });
            Eq("{tipo} {n} {hora}", box.Text, "spaces on both sides in the middle");
            Eq(10, box.CaretIndex, "the cursor right after the token");
            // A field never used takes the token at the end.
            HomeWindow fresh = Make(1, new Settings());
            SetPage(fresh, "general");
            HomeWindow.NameTemplateRow other = Find<HomeWindow.NameTemplateRow>(fresh.Content as DependencyObject);
            typeof(HomeWindow.NameTemplateRow).GetMethod("Insert", Priv).Invoke(other, new object[] { "{app}" });
            Eq(Settings.DefaultNameTemplate + " {app}", ((TextBox)Field(other, "box")).Text, "at the end of an untouched field");
        }

        static void HomeListsTheModes()
        {
            for (int style = 0; style <= 2; style++)
            {
                Settings s = new Settings();
                s.HomeStyle = style;
                HomeWindow hw = Make(1, s);
                SetPage(hw, "home");
                List<string> texts = Texts(hw);
                Check(!Has(texts, "Repetir") && !Has(texts, "La \u00FAltima \u00E1rea"), "home style " + style + " lists the repeat");
                SavePng((UIElement)hw.Content, hw.Background, 980, 660, "home" + style + "-w6.png");
            }
            Settings m = new Settings();
            HomeWindow minimal = Make(1, m);
            SetPage(minimal, "home");
            SavePng((UIElement)minimal.Content, minimal.Background, 980, 660, "home0-w6-bar.png");
            string[] icons = BarIcons(minimal.Content as DependencyObject);
            Check(icons != null && icons.Length == 8 && Array.IndexOf(icons, "update") < 0 && icons[0] == "area" && icons[4] == "textscan", "mode bar: " + (icons == null ? "none" : string.Join(",", icons)));
        }

        static string[] BarIcons(DependencyObject root)
        {
            if (root == null) return null;
            if (root.GetType().Name == "HomeModeBar") return (string[])root.GetType().GetField("icons", Priv).GetValue(root);
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                string[] r = BarIcons(VisualTreeHelper.GetChild(root, i));
                if (r != null) return r;
            }
            return null;
        }

        static void MascotTipsNeverSayNoShortcut()
        {
            Settings none = new Settings(), keyed = new Settings();
            keyed.HotRepeat = "Ctrl+Alt+F11";
            int mentions = 0;
            for (int i = 0; i < 1500; i++)
            {
                string a = MascotTalk.Tip(none), b = MascotTalk.Tip(keyed);
                Check(!a.Contains("Sin atajo") && !b.Contains("Sin atajo"), "a tip says Sin atajo: " + a + " | " + b);
                if (b.Contains("Ctrl + Alt + F11")) mentions++;
            }
            Check(mentions > 0, "no tip names the repeat's shortcut");
        }

        // ---- Dated subfolders

        static ShotStack Stack(Settings s, string temp)
        {
            ShotStack st = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            SetField(st, "settings", s);
            SetField(st, "folder", temp);
            SetField(st, "kept", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            SetField(st, "copyRuns", new CopyRuns());
            SetField(st, "cards", new List<Card>());
            SetField(st, "copying", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            SetField(st, "takenAt", new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));
            return st;
        }

        static string Capture(string temp, string name, DateTime made)
        {
            string p = Path.Combine(temp, name);
            File.WriteAllBytes(p, new byte[] { 137, 80, 78, 71 });
            File.SetCreationTime(p, made);
            File.SetLastWriteTime(p, made);
            return p;
        }

        static void KeepGoesToDatedFolder()
        {
            string temp = Fresh("keep-temp"), save = Fresh("keep-save");
            DateTime march = new DateTime(2026, 3, 15, 10, 0, 0);
            for (int mode = 0; mode <= 2; mode++)
            {
                Settings s = new Settings();
                s.SaveFolder = save;
                s.SaveSubfolder = mode;
                ShotStack st = Stack(s, temp);
                string cap = Capture(temp, "Captura " + mode + ".png", march);
                string dest = st.Keep(cap);
                string expected = mode == 0 ? save : mode == 1 ? Path.Combine(save, "2026-03") : Path.Combine(save, "2026", "03");
                Eq(Path.Combine(expected, "Captura " + mode + ".png"), dest, "kept with SaveSubfolder " + mode);
                Check(File.Exists(dest), "not copied: " + dest);
                Check(st.IsKept(cap), "not marked as kept");
                // Saving again (after an edit) goes to the same copy.
                Eq(dest, st.Keep(cap), "saved again");
            }
            // A capture of this session goes by when it was taken, even if its file says otherwise.
            Settings s1 = new Settings();
            s1.SaveFolder = save;
            s1.SaveSubfolder = 1;
            ShotStack late = Stack(s1, temp);
            string nye = Capture(temp, "Nochevieja.png", new DateTime(2026, 1, 1, 0, 0, 5));
            ((Dictionary<string, DateTime>)Field(late, "takenAt"))[nye] = new DateTime(2025, 12, 31, 23, 59, 59);
            Eq(Path.Combine(save, "2025-12", "Nochevieja.png"), late.Keep(nye), "taken at the end of the year");
            // One already in a dated folder (opened from Home) is kept where it is: no copy in the root.
            string inMonth = Path.Combine(save, "2026-03", "Captura 1.png");
            Eq(inMonth, late.Keep(inMonth), "already in a monthly folder");
            string inYear = Path.Combine(save, "2026", "03", "Captura 2.png");
            Eq(inYear, late.Keep(inYear), "already in a year\\month folder (the setting changed since)");
            Check(!File.Exists(Path.Combine(save, "Captura 1.png")) && !File.Exists(Path.Combine(save, "Captura 2.png")), "copied into the root");
            string got = null;
            late.KeepAsync(inYear, delegate(string d, string error) { got = d + "|" + error; });
            Eq(inYear + "|", got, "Guardar on a file already in a dated folder");
            // A folder that only looks dated is not the save folder.
            Check(!SaveFolders.Inside(s1, Path.Combine(save, "2026-13")) && !SaveFolders.Inside(s1, Path.Combine(save, "otros")), "not dated");
        }

        static void ExportNextToDatedSource()
        {
            string save = @"C:\Users\X\Pictures\Stackshot";
            Eq(save + @"\2026-10", ShotStack.ExportFolder(save + @"\2026-10\Cine 2026-10-11 17.03.26.mkv", save), "monthly folder");
            Eq(save + @"\2026\10", ShotStack.ExportFolder(save + "\\2026\\10\\V\u00EDdeo.mp4", save), "year and month");
            Eq(save, ShotStack.ExportFolder(save + "\\V\u00EDdeo.mp4", save), "the root as before");
            Eq(Settings.TempDir, ShotStack.ExportFolder(save + "\\otros\\V\u00EDdeo.mp4", save), "another subfolder is temporary");
            Eq(Settings.TempDir, ShotStack.ExportFolder(save + "\\2026-13\\V\u00EDdeo.mp4", save), "not a month");
            Eq(Settings.TempDir, ShotStack.ExportFolder("D:\\2026-10\\V\u00EDdeo.mp4", save), "a dated folder elsewhere");
        }

        static void RecentsListDatedFolders()
        {
            string save = Fresh("recents-save"), temp = Fresh("recents-temp");
            string before = Recents.TempFolder;
            Recents.TempFolder = temp;
            try
            {
                Touch(Path.Combine(save, "raiz.png"));
                Touch(Path.Combine(save, "2026-09", "septiembre.png"));
                Touch(Path.Combine(save, "2026", "10", "octubre.mp4"));
                Touch(Path.Combine(save, "otros", "ajena.png"));
                Touch(Path.Combine(save, "2026", "13", "mes-raro.png"));
                Touch(Path.Combine(save, "2026-10", "~trabajo.png"));
                Touch(Path.Combine(temp, "temporal.png"));
                File.WriteAllBytes(Path.Combine(temp, "septiembre.png"), new byte[] { 1, 2, 3 }); // its temporary original
                Settings s = new Settings();
                s.SaveFolder = save;
                Recents.Forget();
                List<string> names = new List<string>();
                foreach (FileInfo f in Recents.Latest(s, 50)) names.Add(f.Name);
                names.Sort(StringComparer.Ordinal);
                Eq("octubre.mp4|raiz.png|septiembre.png|temporal.png", string.Join("|", names.ToArray()), "recent captures");
                Eq(4, Recents.PerDay(s, 1)[0], "captures today");
            }
            finally
            {
                Recents.TempFolder = before;
                Recents.Forget();
            }
        }

        static void PostersOfDatedRecordingsStay()
        {
            string temp = Fresh("posters-temp"), save = Fresh("posters-save"), posters = Fresh("posters");
            string before = ShotStack.PosterDir;
            ShotStack.PosterDir = posters;
            try
            {
                Touch(Path.Combine(save, "2026-10", "Cine 2026-10-11 17.03.26.mkv"));
                foreach (string n in new string[] { "Cine 2026-10-11 17.03.26.mkv.png", "Cine 2026-01-01 00.00.00.mkv.png" })
                {
                    string p = Path.Combine(posters, n);
                    File.WriteAllBytes(p, new byte[] { 1 });
                    File.SetLastWriteTime(p, DateTime.Now.AddDays(-3));
                }
                Settings s = new Settings();
                s.SaveFolder = save;
                ShotStack st = Stack(s, temp);
                typeof(ShotStack).GetMethod("PrunePosters", Priv).Invoke(st, null);
                Check(File.Exists(Path.Combine(posters, "Cine 2026-10-11 17.03.26.mkv.png")), "the poster of a master kept in a dated folder was deleted");
                Check(!File.Exists(Path.Combine(posters, "Cine 2026-01-01 00.00.00.mkv.png")), "an orphan poster stayed");
            }
            finally { ShotStack.PosterDir = before; }
        }

        // A recording finished while Stackshot closes goes to the folder of the month it was recorded in.
        static void RecordingKeptOnQuitInDatedFolder()
        {
            string dir = Fresh("quit");
            Settings s = new Settings();
            s.SaveFolder = Path.Combine(dir, "guardado");
            s.SaveSubfolder = 1;
            string output = Path.Combine(dir, "V\u00EDdeo prueba.mp4");
            File.WriteAllBytes(output, new byte[2048]);
            Session session = new Session(null, s, "ffmpeg.exe", new D.Rectangle(0, 0, 64, 48), false, 60);
            session.PrepareForTest(output, null, null, output, 120, 60, 1, true, "prueba", null);
            DateTime now = DateTime.Now;
            typeof(Session).GetMethod("KeepOnQuit", Priv).Invoke(session, null);
            string expected = Path.Combine(SaveFolders.Target(s, now), "V\u00EDdeo prueba.mp4");
            Check(File.Exists(expected), "not in the dated folder: " + expected);
            Check(!File.Exists(output), "still in the temporary folder");
            Check((bool)typeof(Session).GetField("quitSaved", Priv).GetValue(session), "not reported as saved");
        }

        // ---- Review (round 6, wave 2)

        // "~" starts the recorder's work files: Home, the tray strip and the history skip such names, the sweep leaves
        // "~grabando ..." alone and the next start moves it to "recuperado". A template never makes one.
        static void NamesNeverLookLikeWorkFiles()
        {
            NameTemplate.Values v = Vals("Captura", "~app", "", When);
            Eq("_Captura", NameTemplate.Format("~{tipo}", v, 1), "a leading ~");
            Eq("_Captura", NameTemplate.Format("  ~{tipo}", v, 1), "a ~ after leading spaces");
            Eq("_app", NameTemplate.Format("{app}", v, 1), "a ~ from a token");
            Eq("a~b Captura", NameTemplate.Format("a~b {tipo}", v, 1), "a ~ inside stays");
            string name = NameTemplate.Format("~grabando {fecha} {hora}", v, 1);
            Check(!WorkFiles.IsWorkFile(name + ".png") && !name.StartsWith("~", StringComparison.Ordinal), "a work file name: " + name);
            string temp = Fresh("tilde-temp"), save = Fresh("tilde-save");
            Settings s = new Settings();
            s.SaveFolder = save;
            s.FileNameTemplate = "~{tipo} {n}";
            string p = ShotStack.CapturePath(s, temp, "Captura", null, When);
            Eq(Path.Combine(temp, "_Captura 1.png"), p, "capture path");
            Touch(p);
            Eq(Path.Combine(temp, "_Captura 2.png"), ShotStack.CapturePath(s, temp, "Captura", null, When), "the counter sees it");
            string before = Recents.TempFolder;
            Recents.TempFolder = temp;
            try
            {
                Recents.Forget();
                Eq(1, Recents.Latest(s, 10).Count, "Home and the tray list it");
            }
            finally
            {
                Recents.TempFolder = before;
                Recents.Forget();
            }
        }

        // Two captures in a row: the first one's PNG is still being written (not on disk yet) when the second is named.
        static void CounterSkipsCapturesStillBeingWritten()
        {
            string temp = Fresh("counter-writing"), save = Fresh("counter-writing-save");
            Settings s = new Settings();
            s.SaveFolder = save;
            s.FileNameTemplate = "{tipo} {n}";
            HashSet<string> writing = (HashSet<string>)typeof(ShotStack).GetField("writing", PrivStatic).GetValue(null);
            string first = ShotStack.CapturePath(s, temp, "Captura", null, When);
            Eq(Path.Combine(temp, "Captura 1.png"), first, "first");
            lock (writing) writing.Add(first);
            try
            {
                Eq(Path.Combine(temp, "Captura 2.png"), ShotStack.CapturePath(s, temp, "Captura", null, When), "a capture still being written keeps its number");
                Eq(Path.Combine(temp, "Captura 2.png"), ShotStack.CountedPathBounded(s.FileNameTemplate, Vals("Captura", "", "", When), temp, save), "also through the worker");
            }
            finally { lock (writing) writing.Remove(first); }
        }

        // A save folder that doesn't answer (a lost network share holds each call for many seconds) never holds the capture
        // up: the worker is waited for briefly, a folder still busy from before is not asked again, and the name then goes
        // past the numbers of the temporary folder only.
        static void CounterNeverWaitsForASlowSaveFolder()
        {
            Check(ShotStack.LocalFolder(Path.GetTempPath()), "the temporary folder is local");
            Check(!ShotStack.LocalFolder(@"\\servidor\capturas\2026-10") && !ShotStack.LocalFolder(@"\\?\UNC\servidor\capturas") && !ShotStack.LocalFolder("relativa") && !ShotStack.LocalFolder(null),
                  "shares and odd paths are read with a limit");
            string temp = Fresh("slow-temp"), save = Fresh("slow-save");
            Touch(Path.Combine(temp, "Captura 1.png"));
            Touch(Path.Combine(save, "Captura 7.png"));
            NameTemplate.Values v = Vals("Captura", "", "", When);
            Eq(Path.Combine(temp, "Captura 8.png"), ShotStack.CountedPathBounded("{tipo} {n}", v, temp, save), "a folder that answers counts");
            int ran = 0;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            string slow = ShotStack.FromSlowFolder(save, delegate { System.Threading.Interlocked.Increment(ref ran); System.Threading.Thread.Sleep(4000); return "tarde"; });
            long waited = sw.ElapsedMilliseconds;
            Check(slow == null && waited < 2500, "waited " + waited + " ms for a folder that doesn't answer");
            sw.Restart();
            string again = ShotStack.FromSlowFolder(save, delegate { System.Threading.Interlocked.Increment(ref ran); return "otra"; });
            Check(again == null && sw.ElapsedMilliseconds < 1500, "a folder still busy was asked again (" + sw.ElapsedMilliseconds + " ms)");
            sw.Restart();
            Eq(Path.Combine(temp, "Captura 2.png"), ShotStack.CountedPathBounded("{tipo} {n}", v, temp, save), "the temporary folder only, while the save folder is busy");
            Check(sw.ElapsedMilliseconds < 1500, "named in " + sw.ElapsedMilliseconds + " ms");
            Eq(1, System.Threading.Volatile.Read(ref ran), "reads started");
            DateTime until = DateTime.Now.AddSeconds(10);
            string back = null;
            while (back == null && DateTime.Now < until)
            {
                System.Threading.Thread.Sleep(100);
                back = ShotStack.FromSlowFolder(save, delegate { return "ya"; });
            }
            Eq("ya", back, "asked again once the slow read ended");
            Eq(Path.Combine(temp, "Captura 8.png"), ShotStack.CountedPathBounded("{tipo} {n}", v, temp, save), "and it counts again");
            Check(ShotStack.FromSlowFolder(save, delegate { throw new IOException("red"); }) == null, "a read that fails");
        }

        [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint flags);
        [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr inst, IntPtr name);

        static void Gui(out uint gdi, out uint user)
        {
            IntPtr me = System.Diagnostics.Process.GetCurrentProcess().Handle;
            gdi = GetGuiResources(me, 0);  // GR_GDIOBJECTS
            user = GetGuiResources(me, 1); // GR_USEROBJECTS
        }

        static int Snapshots(IntPtr[] pointers, Bitmap img, int rounds, bool dispose)
        {
            int drawn = 0;
            int[,] dpis = { { 96, 96 }, { 144, 96 }, { 96, 144 }, { 120, 96 } };
            for (int i = 0; i < rounds; i++)
                foreach (IntPtr h in pointers)
                    for (int d = 0; d < dpis.GetLength(0); d++)
                    {
                        PointerShot p = PointerShot.From(h, new D.Point(40 + i % 50, 30 + i % 40), dpis[d, 0], dpis[d, 1], 32);
                        Check(p != null, "no snapshot of " + h);
                        if (p.DrawInto(img, new D.Point(i % 7, i % 5))) drawn++;
                        if (dispose) p.Dispose();
                    }
            return drawn;
        }

        // Every capture with the pointer copies a shape and draws it through a DIB: nothing of it may stay behind, with
        // the shapes Windows shares (the arrow, the I-beam) as with an app's own, scaled or not, disposed or forgotten.
        static void PointerSnapshotsLeakNothing()
        {
            IntPtr color = ColorPointer(3, 3), inverting = InvertingPointer();
            IntPtr arrow = LoadCursor(IntPtr.Zero, new IntPtr(32512)), beam = LoadCursor(IntPtr.Zero, new IntPtr(32513));
            Check(arrow != IntPtr.Zero && beam != IntPtr.Zero, "no system pointers");
            IntPtr[] all = { color, inverting, arrow, beam };
            try
            {
                using (Bitmap img = Plain(200, 160, D.Color.White))
                {
                    Snapshots(all, img, 5, true);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    uint gdi0, user0, gdi1, user1, gdi2, user2;
                    Gui(out gdi0, out user0);
                    int drawn = Snapshots(all, img, 50, true);
                    Gui(out gdi1, out user1);
                    Check(drawn == 50 * all.Length * 4, "drawn " + drawn);
                    Check(gdi1 <= gdi0 + 4 && user1 <= user0 + 4, "disposed snapshots left GDI " + gdi0 + " -> " + gdi1 + ", USER " + user0 + " -> " + user1);
                    // Never disposed (an exception on the way): the finalizer gives them back.
                    Snapshots(all, img, 25, false);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Gui(out gdi2, out user2);
                    Check(gdi2 <= gdi0 + 4 && user2 <= user0 + 4, "forgotten snapshots left GDI " + gdi0 + " -> " + gdi2 + ", USER " + user0 + " -> " + user2);
                }
                // The shared shapes themselves are untouched.
                foreach (IntPtr h in new IntPtr[] { arrow, beam })
                {
                    Native.ICONINFO ii;
                    Check(Native.GetIconInfo(h, out ii), "a shared pointer was destroyed");
                    if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                    if (ii.hbmColor != IntPtr.Zero) Native.DeleteObject(ii.hbmColor);
                }
            }
            finally
            {
                Native.DestroyIcon(color);
                DestroyCursor(inverting);
            }
        }

        // Changing Subcarpetas between two saves of one capture: the same copy is refreshed, no second one appears.
        static void SubfolderChangeKeepsOneCopy()
        {
            string temp = Fresh("switch-temp"), save = Fresh("switch-save");
            Settings s = new Settings();
            s.SaveFolder = save;
            ShotStack st = Stack(s, temp);
            string cap = Capture(temp, "Captura cambio.png", new DateTime(2026, 3, 15, 10, 0, 0));
            string first = st.Keep(cap);
            Eq(Path.Combine(save, "Captura cambio.png"), first, "kept in the root");
            foreach (int mode in new int[] { 1, 2, 0 })
            {
                s.SaveSubfolder = mode;
                File.WriteAllBytes(cap, new byte[] { 137, 80, 78, 71, (byte)mode, 9 });
                Eq(first, st.Keep(cap), "saved again with SaveSubfolder " + mode);
                Eq(6L, new FileInfo(first).Length, "the copy has the edit");
            }
            Check(!File.Exists(Path.Combine(save, "2026-03", "Captura cambio.png")) && !File.Exists(Path.Combine(save, "2026", "03", "Captura cambio.png")), "a second copy");
            Check(!Directory.Exists(Path.Combine(save, "2026-03")) && !Directory.Exists(Path.Combine(save, "2026")), "an empty dated folder was made for a copy that lives elsewhere");
            // Guardar on the card (KeepAsync) after another change: the same copy again, no folder made for nothing.
            SetField(st, "copiesIdle", new System.Threading.ManualResetEvent(true));
            s.SaveSubfolder = 2;
            File.WriteAllBytes(cap, new byte[] { 137, 80, 78, 71, 7, 7, 7 });
            st.KeepAsync(cap, null);
            Check(((System.Threading.ManualResetEvent)Field(st, "copiesIdle")).WaitOne(10000), "the copy never ended");
            ((HashSet<string>)Field(st, "copying")).Remove(cap); // its last step runs on the UI thread, which this test doesn't have
            Eq(7L, new FileInfo(first).Length, "Guardar refreshed the same copy");
            Check(!Directory.Exists(Path.Combine(save, "2026")), "Guardar made an empty dated folder");
            // The copy gone meanwhile (its folder deleted by hand): saving again makes the folder back and writes it there.
            s.SaveSubfolder = 1;
            string elsewhere = Capture(temp, "Captura mayo.png", new DateTime(2026, 5, 2, 9, 0, 0));
            string mayCopy = st.Keep(elsewhere);
            Eq(Path.Combine(save, "2026-05", "Captura mayo.png"), mayCopy, "kept in its month");
            Directory.Delete(Path.Combine(save, "2026-05"), true);
            s.SaveSubfolder = 0;
            Eq(mayCopy, st.Keep(elsewhere), "saved again after its folder was deleted");
            Check(File.Exists(mayCopy), "not written back");
            File.Delete(mayCopy);
            Directory.Delete(Path.Combine(save, "2026-05"));
            string before = Recents.TempFolder;
            Recents.TempFolder = temp;
            try
            {
                Recents.Forget();
                int n = 0;
                foreach (FileInfo f in Recents.Latest(s, 20)) if (f.Name == "Captura cambio.png") n++;
                Eq(1, n, "listed once in Home");
            }
            finally
            {
                Recents.TempFolder = before;
                Recents.Forget();
            }
        }

        // With "Tapar los datos sensibles" on, the personal data in a window title stays out of the file name as it stays
        // out of the image (an Outlook title carries the mailbox's address); off, the title goes in as it is.
        static void CoveredCapturesKeepPersonalDataOutOfTheName()
        {
            string temp = Fresh("covered-names");
            Settings s = new Settings();
            s.SaveFolder = Path.Combine(temp, "guardado");
            s.FileNameTemplate = "{app} {ventana}";
            ShotStack.CaptureStart start = new ShotStack.CaptureStart();
            start.App = "OUTLOOK";
            start.Window = "Bandeja de entrada - ana.perez@ejemplo.es - Outlook";
            Eq(Path.Combine(temp, "OUTLOOK Bandeja de entrada - ana.perez@ejemplo.es - Outlook.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "covering off");
            s.SensitiveAuto = true;
            Eq(Path.Combine(temp, "OUTLOOK Bandeja de entrada - \u2022\u2022\u2022 - Outlook.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "an e-mail address");
            start.Window = "Expediente 12345678Z, cuenta ES91 2100 0418 4502 0005 1332 - Word";
            Eq(Path.Combine(temp, "OUTLOOK Expediente \u2022\u2022\u2022, cuenta \u2022\u2022\u2022 - Word.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "a DNI and an IBAN");
            start.Window = "Informe de ventas 2026.xlsx - Excel";
            Eq(Path.Combine(temp, "OUTLOOK Informe de ventas 2026.xlsx - Excel.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "nothing personal");
            s.FileNameTemplate = "{ventana} {n}";
            start.Window = "ana.perez@ejemplo.es";
            Eq(Path.Combine(temp, "\u2022\u2022\u2022 1.png"), ShotStack.CapturePath(s, temp, "Captura", start, When), "a title that is only an address, with {n}");
            Eq("", Sensitive.Scrub(null), "no title");
            Eq(Path.Combine(temp, OldName("Captura", When) + ".png"), ShotStack.CapturePath(new Settings { SensitiveAuto = true }, temp, "Captura", start, When), "the default template");
        }

        // The hint about unknown tokens sits under the token chips: it points up at them.
        static void HintPointsAtTheTokensAboveIt()
        {
            bool warn;
            foreach (string t in new string[] { "{tipo} {fehca}", "{fehca} {otro}" })
            {
                string h = HomeWindow.NameHint(t, out warn);
                Check(warn && h.Contains("de arriba") && !h.Contains("de abajo"), "hint for <" + t + ">: " + h);
            }
            HomeWindow hw = Make(1, new Settings());
            SetPage(hw, "general");
            HomeWindow.NameTemplateRow row = Find<HomeWindow.NameTemplateRow>(hw.Content as DependencyObject);
            TextBlock hint = (TextBlock)Field(row, "hint");
            StackPanel below = hint.Parent as StackPanel;
            Check(below != null, "the hint is not in the panel under the field");
            int chips = -1;
            for (int i = 0; i < below.Children.Count; i++) if (below.Children[i] is WrapPanel) chips = i;
            Check(chips >= 0 && chips < below.Children.IndexOf(hint), "the hint is not under the tokens");
        }

        static void PressKey(UIElement target, System.Windows.Input.Key k, PresentationSource src)
        {
            System.Windows.Input.KeyEventArgs e = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, src, 0, k) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            target.RaiseEvent(e);
            Check(e.Handled, k + " not handled by the field");
        }

        static string FileTemplate()
        {
            foreach (string line in File.ReadAllLines(Settings.FilePath))
                if (line.StartsWith("FileNameTemplate = ", StringComparison.Ordinal)) return line.Substring(19);
            return null;
        }

        // The template field and the settings file: Esc goes back to what was kept (also on disk, should a capture have
        // saved the settings meanwhile), leaving the field and Enter write it, forbidden characters still give a name.
        static void TemplateRowKeysAndFile()
        {
            string data = Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA");
            if (string.IsNullOrEmpty(data) || !Path.GetFullPath(Settings.FilePath).StartsWith(Path.GetFullPath(data), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  (the settings file part needs STACKSHOT_TEST_DATA: skipped)");
                return;
            }
            string[] files = { Settings.FilePath, Settings.FilePath + ".tmp", Settings.FilePath + ".bak" };
            foreach (string f in files) Check(!File.Exists(f), "a settings file is already in the test data: " + f);
            Settings s = new Settings();
            HomeWindow hw = Make(1, s);
            object owner = Field(hw, "owner");
            SetField(owner, "settings", s);
            SetField(owner, "cards", new List<Card>());
            SetPage(hw, "general");
            HomeWindow.NameTemplateRow row = Find<HomeWindow.NameTemplateRow>(hw.Content as DependencyObject);
            TextBox box = (TextBox)Field(row, "box");
            System.Windows.Interop.HwndSource src = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("w6capture")
            {
                WindowStyle = 0, ExtendedWindowStyle = 0x08000080, PositionX = -32000, PositionY = -32000, Width = 1, Height = 1
            });
            Settings.ReadOnly = false;
            try
            {
                box.Text = "{app} {n}";
                Eq("{app} {n}", s.FileNameTemplate, "typed");
                s.Save(); // a capture meanwhile (each one saves the settings)
                Eq("{app} {n}", FileTemplate(), "saved by the capture");
                PressKey(box, System.Windows.Input.Key.Escape, src);
                Eq(Settings.DefaultNameTemplate, box.Text, "Esc: the field");
                Eq(Settings.DefaultNameTemplate, s.FileNameTemplate, "Esc: the setting");
                Eq(Settings.DefaultNameTemplate, FileTemplate(), "Esc: the settings file");
                box.Text = "{tipo} {n}";
                box.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, box, null) { RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent });
                Eq("{tipo} {n}", FileTemplate(), "written when the field is left");
                PressKey(box, System.Windows.Input.Key.Escape, src);
                Eq("{tipo} {n}", s.FileNameTemplate, "Esc after leaving the field keeps it");
                box.Text = "  {tipo}: {fecha}/{hora}  ";
                PressKey(box, System.Windows.Input.Key.Enter, src);
                Eq("{tipo}: {fecha}/{hora}", FileTemplate(), "Enter writes it, trimmed");
                Eq("{tipo}: {fecha}/{hora}", box.Text, "the field shows what was kept");
                string dir = Fresh("row-names");
                string p = ShotStack.CapturePath(s, dir, "Captura", null, When);
                Eq(Path.Combine(dir, "Captura_ 2026-03-07_09.05.04.png"), p, "forbidden characters in the name");
                File.WriteAllBytes(p, new byte[1]);
                box.Text = "";
                box.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, box, null) { RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent });
                Eq(Settings.DefaultNameTemplate, box.Text, "an empty field shows the default again");
                Eq(Settings.DefaultNameTemplate, FileTemplate(), "an empty field writes the default");
            }
            finally
            {
                Settings.ReadOnly = true;
                src.Dispose();
                foreach (string f in files) try { if (File.Exists(f)) File.Delete(f); } catch (Exception) { }
            }
        }

        public static void Suite()
        {
            AppTests.Test("w6capture", "default name template: byte for byte the names of before", DefaultIsByteForByte);
            AppTests.Test("w6capture", "tokens, any case, unknown ones as typed", TokensExpand);
            AppTests.Test("w6capture", "names are safe: characters, spaces, dots, devices, 120 characters, never empty", NamesAreSafe);
            AppTests.Test("w6capture", "{n} goes past the numbers of the day in the temporary and kept folders", CounterGoesPastUsedNumbers);
            AppTests.Test("w6capture", "{app} and {ventana} come from the start of the capture; nothing read by default", TemplateUsesTheStartOfTheCapture);
            AppTests.Test("w6capture", "settings: hint, example and subfolder notes", SettingsHints);
            AppTests.Test("w6capture", "pointer: size for the monitor's DPI", PointerSizeRules);
            AppTests.Test("w6capture", "pointer: drawn where it was, alpha blended, the capture stays opaque", PointerDrawnWhereItWas);
            AppTests.Test("w6capture", "pointer: left out when its hot spot is outside, cut at the edges", PointerOutsideIsLeftOut);
            AppTests.Test("w6capture", "pointer: an inverting one inverts the capture, as on screen", InvertingPointerAsOnScreen);
            AppTests.Test("w6capture", "pointer: scaled to the monitor when Windows gives the system size", PointerScaledToTheMonitor);
            AppTests.Test("w6capture", "pointer: the copy outlives the original; Dispose; bad handles", PointerSnapshotLifetime);
            AppTests.Test("w6capture", "repeat: the last area fits only on the same monitors", LastAreaFits);
            AppTests.Test("w6capture", "repeat: action, setting, file line, no shortcut by default", RepeatActionSettings);
            AppTests.Test("w6capture", "repeat: nothing registered by default, no notice", NothingRegisteredByDefault);
            AppTests.Test("w6capture", "repeat: names, icons and colors stay aligned with the actions", ArraysStayAligned);
            AppTests.Test("w6capture", "repeat: tray row without keycaps until it has a shortcut", TrayOffersRepeat);
            AppTests.Test("w6capture", "repeat: keys page row, light", delegate { KeysPageRow(1, "keys-w6-light.png"); });
            AppTests.Test("w6capture", "repeat: keys page row, dark", delegate { KeysPageRow(2, "keys-w6-dark.png"); });
            AppTests.Test("w6capture", "repeat: Home lists the capture modes only", HomeListsTheModes);
            AppTests.Test("w6capture", "repeat: mascot tips never say Sin atajo", MascotTipsNeverSayNoShortcut);
            AppTests.Test("w6capture", "settings page: template and subfolder rows, light", delegate { GeneralPageRows(1, "general-w6-light.png"); });
            AppTests.Test("w6capture", "settings page: template and subfolder rows, dark", delegate { GeneralPageRows(2, "general-w6-dark.png"); });
            AppTests.Test("w6capture", "settings page: typing applies the template, tokens go in at the cursor", TemplateRowTyping);
            AppTests.Test("w6capture", "dated folders: Guardar copies into the month of the capture", KeepGoesToDatedFolder);
            AppTests.Test("w6capture", "dated folders: an export goes next to its dated source", ExportNextToDatedSource);
            AppTests.Test("w6capture", "dated folders: recent captures include them", RecentsListDatedFolders);
            AppTests.Test("w6capture", "dated folders: posters of masters kept there stay", PostersOfDatedRecordingsStay);
            AppTests.Test("w6capture", "dated folders: a recording saved on quit goes to its month", RecordingKeptOnQuitInDatedFolder);
            AppTests.Test("w6capture", "review: a name never starts with ~ (the recorder's work files)", NamesNeverLookLikeWorkFiles);
            AppTests.Test("w6capture", "review: {n} skips a capture still being written", CounterSkipsCapturesStillBeingWritten);
            AppTests.Test("w6capture", "review: {n} never waits for a save folder that doesn't answer", CounterNeverWaitsForASlowSaveFolder);
            AppTests.Test("w6capture", "review: pointer snapshots leave no GDI or USER handles", PointerSnapshotsLeakNothing);
            AppTests.Test("w6capture", "review: changing Subcarpetas between saves keeps one copy", SubfolderChangeKeepsOneCopy);
            AppTests.Test("w6capture", "review: covered captures keep personal data out of the name", CoveredCapturesKeepPersonalDataOutOfTheName);
            AppTests.Test("w6capture", "review: the unknown-token hint points at the tokens above it", HintPointsAtTheTokensAboveIt);
            AppTests.Test("w6capture", "review: template field, Esc, leaving it, Enter and the settings file", TemplateRowKeysAndFile);
        }
    }
}
