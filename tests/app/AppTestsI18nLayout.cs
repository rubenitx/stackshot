// Suite "i18n", layout checks that every interface language has to pass: windows are built and laid out at their real
// width, never shown, and what they draw has to sit inside the window instead of running off its edge. With
// STACKSHOT_UI_SHOTS set to a folder, the German, Korean and English ones are also saved there as PNG to be looked at.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot.Tests
{
    static class AppTestsI18nLayout
    {
        internal static readonly string[] AllLanguages = { "es", "en", "de", "ko", "fr", "pt", "zh", "hi", "ar", "bn" };
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static string Ascii(string s) { return Regex.Replace(s ?? "", "[^\\x20-\\x7e]", "?"); }

        // The window's content laid out at the width the window has (its height follows the content).
        static FrameworkElement Lay(Window w, double width)
        {
            FrameworkElement root = (FrameworkElement)w.Content;
            root.Measure(new Size(width, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, width, root.DesiredSize.Height));
            root.UpdateLayout();
            return root;
        }

        // The content as a PNG, on the window's own background (a window's background is not part of its Content).
        static void Shot(FrameworkElement root, string name)
        {
            string dir = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            int w = (int)Math.Ceiling(root.ActualWidth), h = (int)Math.Ceiling(root.ActualHeight);
            if (w <= 0 || h <= 0) return;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Ds.Brush(Ds.Brushes.Window), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(Path.Combine(dir, "i18n-layout-" + name + ".png"))) enc.Save(fs);
        }

        // A window that was built and laid out but never shown still listens to theme changes until it is closed.
        static void Dispose(Window w) { try { w.Close(); } catch (Exception) { } }

        static bool Pictured(string lang) { return lang == "de" || lang == "ko" || lang == "en"; }

        // A WinForms control as a PNG, without showing it.
        static void ShotControl(System.Windows.Forms.Control c, int height, string name)
        {
            string dir = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            c.Height = height;
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, c.Width, c.Height));
                bmp.Save(Path.Combine(dir, "i18n-layout-" + name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        // The room a button has: it starts inside the window and ends before the window's padding, and is not squeezed.
        static void ButtonsInside(FrameworkElement root, string where, List<string> bad)
        {
            int buttons = 0;
            AppTestsI18n.Walk(root, new HashSet<DependencyObject>(), delegate(DependencyObject d)
            {
                MacButton b = d as MacButton;
                if (b == null) return;
                buttons++;
                Rect r = b.TransformToAncestor(root).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight));
                if (b.ActualWidth < b.Width - 0.5) bad.Add(where + ": \"" + Ascii(b.Label) + "\" squeezed to " + (int)b.ActualWidth + " of " + (int)b.Width);
                else if (r.Left < 8 || r.Right > root.ActualWidth - 8)
                    bad.Add(where + ": \"" + Ascii(b.Label) + "\" runs from " + (int)r.Left + " to " + (int)r.Right + " in a window " + (int)root.ActualWidth + " wide");
            });
            if (buttons == 0) bad.Add(where + ": no button found");
        }

        static void Report(List<string> bad, string what)
        {
            foreach (string s in bad) Console.WriteLine("    " + what + ": " + s);
            Check(bad.Count == 0, bad.Count + " " + what);
        }

        // ---- Quick note and reminder

        static ShotStack Owner(bool reminders)
        {
            ShotStack owner = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            FieldInfo f = typeof(ShotStack).GetField("reminders", NP);   // uninitialized owner: the list is still null
            if (reminders && f != null && f.GetValue(owner) == null)
                f.SetValue(owner, Activator.CreateInstance(typeof(List<>).MakeGenericType(f.FieldType.GetGenericArguments()[0])));
            return owner;
        }

        static QuickWindow Quick(bool note)
        {
            return (QuickWindow)typeof(QuickWindow).GetConstructor(NP, null, new[] { typeof(ShotStack), typeof(bool) }, null).Invoke(new object[] { Owner(!note), note });
        }

        // The save, pin and cancel buttons of the small window the mascot opens sit in one row: in no language does the
        // row run past the window's edge (German used to cut the primary button).
        public static void QuickWindowButtonsFit()
        {
            List<string> bad = new List<string>();
            try
            {
                foreach (bool note in new[] { true, false })
                    foreach (string lang in AllLanguages)
                    {
                        I18n.Apply(lang);
                        QuickWindow w = Quick(note);
                        FrameworkElement root = Lay(w, w.Width);
                        ButtonsInside(root, lang + (note ? "/note" : "/reminder"), bad);
                        if (Pictured(lang)) Shot(root, "quick-" + (note ? "note" : "reminder") + "-" + lang);
                        Dispose(w);
                    }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "quick window buttons out of the window");
        }

        // "When?" gives its six choices one sixth of the width each: none of them is cut with an ellipsis.
        public static void QuickReminderChoicesFit()
        {
            List<string> bad = new List<string>();
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    QuickWindow w = Quick(false);
                    FrameworkElement root = Lay(w, w.Width);
                    int found = 0;
                    AppTestsI18n.Walk(root, new HashSet<DependencyObject>(), delegate(DependencyObject d)
                    {
                        MacSegmented seg = d as MacSegmented;
                        if (seg == null) return;
                        found++;
                        string[] options = (string[])typeof(MacSegmented).GetField("options", NP).GetValue(seg);
                        double room = (seg.ActualWidth - 4) / options.Length - MacSegmented.TextPad;
                        foreach (string o in options)
                        {
                            double need = Ink.Text(o, Ds.Semibold, 12.5, Colors.Black).WidthIncludingTrailingWhitespace;
                            if (need > room + 0.5) bad.Add(lang + ": \"" + Ascii(o) + "\" needs " + (int)Math.Ceiling(need) + " px, has " + (int)room);
                        }
                    });
                    if (found != 1) bad.Add(lang + ": expected one choice strip, found " + found);
                    Dispose(w);
                }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "reminder time choices cut");
        }

        // ---- Confirmation dialogs

        // The button sets of the confirmations the app asks (editor, documents, settings), as the call sites write them.
        static List<string[]> AlertSets()
        {
            string cancel = I18n.Cancel;
            return new List<string[]>
            {
                new[] { I18n.T("Guardar", "Save"), I18n.T("No guardar", "Don't save"), I18n.T("Cancelar", "Cancel") },                                    // document: unsaved changes
                new[] { I18n.T("Exportar", "Export"), I18n.T("Descartar cambios", "Discard changes"), I18n.T("Seguir editando", "Keep editing") },       // editor: recording, closing
                new[] { I18n.T("Conservar", "Keep"), I18n.T("Descartar cambios", "Discard changes"), I18n.T("Seguir editando", "Keep editing") },        // editor: capture, closing
                new[] { I18n.T("Exportar", "Export"), I18n.T("Salir sin exportar", "Exit without exporting"), I18n.T("Seguir editando", "Keep editing") }, // editor: recording, exiting
                new[] { I18n.T("Guardar", "Save"), I18n.T("No guardar", "Don't save"), I18n.T("Seguir editando", "Keep editing") },                     // editor: capture, exiting
                new[] { I18n.T("S\u00ED", "Yes", "Ja"), I18n.T("No", "No", "Nein"), cancel },                                                          // Dialogs: Yes / No / Cancel
                new[] { I18n.T("Cancelar la exportaci\u00F3n", "Cancel export"), I18n.T("Seguir exportando", "Keep exporting") },
                new[] { I18n.T("Cancelar y salir", "Cancel and exit"), I18n.T("Seguir exportando", "Keep exporting") },
                new[] { I18n.T("Descartar", "Discard"), I18n.T("Seguir escribiendo", "Keep typing") },
                new[] { I18n.T("Detener y salir", "Stop and exit"), I18n.T("Seguir grabando", "Keep recording") },
                new[] { I18n.T("Guardar igualmente", "Save anyway"), cancel },
                new[] { I18n.T("Guardar en UTF-8", "Save in UTF-8"), cancel },
                new[] { I18n.T("Guardar como PDF", "Save as PDF"), cancel },
                new[] { I18n.T("Guardar como\u2026", "Save as\u2026"), cancel },
                new[] { I18n.T("Borrar el \u00EDndice", "Delete index"), cancel },
            };
        }

        public static void AlertButtonsFit()
        {
            List<string> bad = new List<string>();
            ConstructorInfo ctor = typeof(DocAlert).GetConstructor(NP, null, new[] { typeof(string), typeof(string), typeof(string[]), typeof(int), typeof(int) }, null);
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    List<string[]> sets = AlertSets();
                    for (int k = 0; k < sets.Count; k++)
                    {
                        string[] set = sets[k];
                        int primary = 0, cancel = set.Length - 1;
                        DocAlert w = (DocAlert)ctor.Invoke(new object[] { I18n.Fmt("\u00BFGuardar los cambios en {0} antes de salir?", "Save changes in {0} before exiting?", "captura.png"), I18n.T("Si no los guardas, se perder\u00E1n.", "If you don't save them, they will be lost."), set, primary, cancel });
                        FrameworkElement root = Lay(w, w.Width);
                        string where = lang + "/" + Ascii(string.Join(" | ", set));
                        ButtonsInside(root, where, bad);
                        if (Pictured(lang) && k == 1) Shot(root, "alert-" + lang);
                        Dispose(w);
                    }
                }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "confirmation buttons out of the dialog");
        }

        // ---- Scrolling-capture bar

        // The bar's two text lines (what it shows and what the button under the mouse does) have one line of StatusRoom
        // pixels: every status text, and the height line at its longest, fits it in every language.
        public static void ScrollBarTextsFit()
        {
            List<string> bad = new List<string>();
            // saving, esc, done, full, wentUp, losing, hot, auto, stuck: one case for every branch of the status line.
            object[][] cases =
            {
                new object[] { true, false, false, false, false, false, -1, false, false },
                new object[] { false, true, false, false, false, false, -1, false, false },
                new object[] { false, false, true, false, false, false, -1, false, false },
                new object[] { false, false, false, true, false, false, -1, false, false },
                new object[] { false, false, false, false, true, false, -1, false, false },
                new object[] { false, false, false, false, false, true, -1, false, false },
                new object[] { false, false, false, false, false, false, 0, true, false },
                new object[] { false, false, false, false, false, false, 0, false, false },
                new object[] { false, false, false, false, false, false, 1, false, false },
                new object[] { false, false, false, false, false, false, 2, false, false },
                new object[] { false, false, false, false, false, false, -1, true, false },
                new object[] { false, false, false, false, false, false, -1, false, true },
                new object[] { false, false, false, false, false, false, -1, false, false },
            };
            int statusPx = (int)Math.Round(11.5f), heightPx = 14;
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    foreach (object[] c in cases)
                    {
                        string text = ScrollCaptureBar.StateText((bool)c[0], (bool)c[1], (bool)c[2], (bool)c[3], (bool)c[4], (bool)c[5], (int)c[6], (bool)c[7], (bool)c[8]);
                        double need = Ink.Px(text, Ds.Regular, statusPx, Colors.White).WidthIncludingTrailingWhitespace;
                        if (need > ScrollCaptureBar.StatusRoom + 0.5) bad.Add(lang + ": \"" + Ascii(text) + "\" needs " + (int)Math.Ceiling(need) + " px of " + ScrollCaptureBar.StatusRoom);
                    }
                    foreach (int h in new[] { 0, 900, 12345, 99999 })
                    {
                        string text = ScrollCaptureBar.HeightText(h, 700);
                        double need = Ink.Px(text, Ds.Semibold, heightPx, Colors.White).WidthIncludingTrailingWhitespace;
                        if (need > ScrollCaptureBar.StatusRoom + 0.5) bad.Add(lang + ": \"" + Ascii(text) + "\" needs " + (int)Math.Ceiling(need) + " px of " + ScrollCaptureBar.StatusRoom);
                    }
                }
            }
            finally { I18n.Apply("es"); }
            ScrollBarShots();
            Report(bad, "scrolling-capture bar texts cut");
        }

        // The bar itself, painted without showing it (the real window would sit on the user's screen): the longest status
        // line ("Done again saves it") and the hint of the Pause button, 1.6 screens of the page behind it.
        static void ScrollBarShot(string lang, string name, bool doneArmed, int hot, bool auto)
        {
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            I18n.Apply(lang);
            ScrollSession session = new ScrollSession((ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack)), new System.Drawing.Rectangle(0, 0, 800, 600));
            typeof(Stitcher).GetField("height", Any).SetValue(typeof(ScrollSession).GetField("stitcher", Any).GetValue(session), 960);
            if (doneArmed) typeof(ScrollSession).GetField("doneArmed", Any).SetValue(session, 0L);
            if (auto) typeof(ScrollSession).GetField("auto", Any).SetValue(session, true);
            ScrollCaptureBar bar = (ScrollCaptureBar)FormatterServices.GetUninitializedObject(typeof(ScrollCaptureBar));
            typeof(ScrollCaptureBar).GetField("session", Any).SetValue(bar, session);
            typeof(ScrollCaptureBar).GetField("hot", Any).SetValue(bar, hot);
            typeof(ScrollCaptureBar).GetField("s", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy).SetValue(bar, 1f);
            typeof(FloatWindow).GetField("Pad", Any).SetValue(bar, 22);
            typeof(FloatWindow).GetField("body", Any).SetValue(bar, new System.Drawing.Size(ScrollCaptureBar.BarWidth, 48));
            int w = ScrollCaptureBar.BarWidth + 44, h = 48 + 44;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(236, 236, 240)), null, new Rect(0, 0, w, h));
                typeof(ScrollCaptureBar).GetMethod("PaintSurface", Any).Invoke(bar, new object[] { dc, w, h });
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(Path.Combine(Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS"), "i18n-layout-scroll-bar-" + name + "-" + lang + ".png"))) enc.Save(fs);
        }

        public static void ScrollBarShots()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS"))) return;
            try
            {
                foreach (string lang in new[] { "de", "ko", "en" })
                {
                    ScrollBarShot(lang, "done-again", true, -1, false);
                    ScrollBarShot(lang, "pause-hint", false, 0, true);
                }
            }
            finally { I18n.Apply("es"); }
        }

        // ---- Editor backdrop panel

        static int TextW(BgPanel panel, string method, string text)
        {
            System.Drawing.Font f = (System.Drawing.Font)typeof(BgPanel).GetMethod(method, NP).Invoke(panel, null);
            return TextKit.Measure(text, f, System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding).Width;
        }

        // The sidebar draws its labels in one line with no room to wrap: the checkbox beside "Border inset", the two
        // headers over the half columns, a slider's name beside its value, the pop-up buttons: all of them fit their
        // space in every language, at the sidebar's width.
        public static void BackdropPanelLabelsFit()
        {
            List<string> bad = new List<string>();
            Type kind = typeof(BgPanel).GetNestedType("Kind", BindingFlags.NonPublic);
            Type itemType = typeof(BgPanel).GetNestedType("Item", BindingFlags.NonPublic);
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    foreach (bool video in new[] { false, true })
                    {
                        BgPanel panel = new BgPanel { S = 1f, Bg = new Settings(), Video = video };
                        panel.Width = (int)BgPanel.Wide;
                        typeof(BgPanel).GetMethod("Arrange", NP).Invoke(panel, new object[] { panel.Width });
                        System.Collections.IList items = (System.Collections.IList)typeof(BgPanel).GetField("items", NP).GetValue(panel);
                        int seen = 0;
                        foreach (object it in items)
                        {
                            string name = itemType.GetField("Kind").GetValue(it).ToString();
                            string label = (string)itemType.GetField("Label").GetValue(it);
                            System.Drawing.Rectangle r = (System.Drawing.Rectangle)itemType.GetField("R").GetValue(it);
                            if (label == null) continue;
                            int need, room;
                            string how;
                            switch (name)
                            {
                                case "Check": need = TextW(panel, "Small", label); room = r.Width - 16 - 8; how = "beside its box"; break;
                                case "Slider": need = TextW(panel, "Small", label) + 8 + TextW(panel, "Small", "100"); room = r.Width; how = "with its value"; break;
                                case "Header": need = TextW(panel, "Small", label); room = r.Width; how = "as a header"; break;
                                case "None": need = TextW(panel, "Semibold", label); room = r.Width; how = "on its button"; break;
                                case "More": need = TextW(panel, "Small", label) + 18; room = r.Width; how = "beside its arrow"; break;
                                default: continue;
                            }
                            seen++;
                            if (need > room) bad.Add(lang + (video ? "/video" : "") + ": \"" + Ascii(label) + "\" (" + how + ") needs " + need + " px, has " + room);
                        }
                        foreach (string text in new[] { I18n.T("Estilos guardados\u2026", "Saved styles\u2026"), I18n.Fmt("Estilos guardados ({0})", "Saved styles ({0})", 12) })
                        {
                            int need = TextW(panel, "Regular", text), room = (int)BgPanel.Wide - 28 - 38 - 30;
                            if (need > room) bad.Add(lang + ": \"" + Ascii(text) + "\" (saved styles button) needs " + need + " px, has " + room);
                        }
                        if (seen < 8) bad.Add(lang + ": only " + seen + " labels found");
                        if (!video && Pictured(lang)) ShotControl(panel, (int)typeof(BgPanel).GetField("contentH", NP).GetValue(panel), "backdrop-panel-" + lang);
                    }
                }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "backdrop panel labels cut");
        }

        // ---- Editor hint strip: what the sensitive-data search says

        // The result of "Hide sensitive data" is said in the hint strip, one line between its left edge and the drag handle
        // (or the size line): at the editor's usual 1264 px every wording of it, in every language, fits without the ellipsis.
        public static void SensitiveResultsFit()
        {
            List<string> bad = new List<string>();
            List<SensitiveFinding> four = Sensitive.Scan("12345678Z ES91 2100 0418 4502 0005 1332 a@b.es c@d.es");
            List<SensitiveFinding> one = Sensitive.Scan("600 123 456");
            Check(four.Count >= 3 && one.Count == 1, "the sample text should be found: " + four.Count + " / " + one.Count);
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    List<string> messages = new List<string>
                    {
                        Editor.SensitiveResult(four, four.Count), Editor.SensitiveResult(four, 1), Editor.SensitiveResult(four, 0),
                        Editor.SensitiveResult(one, 1), Editor.SensitiveResult(one, 0), Editor.SensitiveResult(new List<SensitiveFinding>(), 0),
                        // What the editor says when it opens a capture that was covered when it was taken.
                        I18n.T("El dato sensible se tap\u00F3 al capturar: es una marca y puedes quitarla.", "Sensitive data was redacted on capture: it is a mark and you can remove it."),
                        I18n.T("Los datos sensibles se taparon al capturar: son marcas y puedes quitar cualquiera.", "Sensitive data was redacted on capture: they are marks and you can remove any."),
                    };
                    string action = I18n.T("Enter copia y cierra", "Enter copies and closes");
                    using (Hint hint = new Hint { S = 1f, Width = 1264, Height = 34 })
                    {
                        hint.DragOut += delegate { };
                        hint.RightText = "1264 \u00D7 700 px  \u00B7  " + action;
                        System.Drawing.Font f = Fonts.Get(EdLook.Text, 12f);
                        System.Drawing.Rectangle handle = hint.HandleRect;
                        int right = hint.Width - TextKit.Measure(hint.RightText, f, System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding).Width - 14;
                        int room = (handle.IsEmpty ? right : handle.X) - 2 * 14;   // the left text's rectangle, as the strip paints it
                        foreach (string m in messages)
                        {
                            int need = TextKit.Measure(m, f, System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding).Width;
                            if (need > room) bad.Add(lang + ": \"" + Ascii(m) + "\" needs " + need + " px, has " + room);
                        }
                        if (Pictured(lang)) { hint.LeftText = messages[0]; ShotControl(hint, 34, "sensitive-result-" + lang); }
                    }
                }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "sensitive-data messages cut");
        }

        // ---- Numbers

        static void Pump(Func<bool> done, int ms)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (!done() && sw.ElapsedMilliseconds < ms)
            {
                System.Windows.Threading.DispatcherFrame fr = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, (Action)delegate { fr.Continue = false; });
                System.Windows.Threading.Dispatcher.PushFrame(fr);
                if (!done()) System.Threading.Thread.Sleep(5);
            }
        }

        static string StatusOf(MarkdownWindow w, string file)
        {
            typeof(MarkdownWindow).GetMethod("LoadFile", NP).Invoke(w, new object[] { file });
            Pump(delegate { return w.Doc != null && w.Doc.Path != null && Path.GetFileName(w.Doc.Path) == Path.GetFileName(file); }, 15000);
            Pump(delegate { return false; }, 600);   // the XML tree is built in the background
            typeof(MarkdownWindow).GetMethod("Stats", NP).Invoke(w, null);
            return ((TextBlock)typeof(MarkdownWindow).GetField("statusLabel", NP).GetValue(w)).Text;
        }

        // Shown numbers follow the interface language, not the regional format of Windows: a Spanish Windows with the
        // interface in English reads "30,002 nodes", an English Windows with the interface in Spanish "30.002 nodos".
        public static void NumbersFollowTheInterfaceLanguage()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-numbers");
            Directory.CreateDirectory(dir);
            string md = Path.Combine(dir, "long.md"), xml = Path.Combine(dir, "many.xml");
            System.Text.StringBuilder words = new System.Text.StringBuilder();
            for (int i = 0; i < 30002; i++) words.Append("word ");
            File.WriteAllText(md, words.ToString());
            System.Text.StringBuilder nodes = new System.Text.StringBuilder("<r>");
            for (int i = 0; i < 30001; i++) nodes.Append("<i/>");
            File.WriteAllText(xml, nodes.Append("</r>").ToString());
            CultureInfo regional = System.Threading.Thread.CurrentThread.CurrentCulture, ui = System.Threading.Thread.CurrentThread.CurrentUICulture;
            List<string> bad = new List<string>();
            try
            {
                foreach (string lang in new[] { "en", "ko", "es", "de" })
                {
                    // The Windows regional format is the other one: Spanish when the interface is not, English when it is.
                    System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(lang == "es" || lang == "de" ? "en-US" : "es-ES");
                    I18n.Apply(lang);
                    string shown = I18n.Culture.Name == "en-US" ? "30,002" : "30.002";
                    MarkdownWindow w = (MarkdownWindow)typeof(MarkdownWindow).GetConstructors(NP)[0].Invoke(new object[] { null });
                    typeof(MarkdownWindow).GetField("open", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, w);
                    try
                    {
                        w.Left = -6000; w.Top = 0; w.ShowActivated = false; w.ShowInTaskbar = false;
                        w.Show();
                        string text = StatusOf(w, md);
                        if (!text.Contains(shown)) bad.Add(lang + " words: \"" + Ascii(text) + "\" should count " + shown);
                        if (lang == "en" || lang == "ko") { w.UpdateLayout(); Shot((FrameworkElement)w.Content, "document-status-" + lang + "-windows-es-ES"); }
                        text = StatusOf(w, xml);
                        if (!text.Contains(shown)) bad.Add(lang + " nodes: \"" + Ascii(text) + "\" should count " + shown);
                    }
                    finally
                    {
                        Func<string, string, string[], int> keep = DocAlert.Answer;
                        DocAlert.Answer = delegate { return 1; };
                        try { w.Close(); } catch { }
                        DocAlert.Answer = keep;
                    }
                    string size = Recents.Size(5L * 1048576 + 500000);
                    string wantSize = (I18n.Culture.Name == "en-US" ? "5.5" : "5,5") + " MB";
                    if (size != wantSize) bad.Add(lang + " size: \"" + size + "\" should be \"" + wantSize + "\"");
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = regional;
                System.Threading.Thread.CurrentThread.CurrentUICulture = ui;
                I18n.Apply("es");
            }
            Report(bad, "numbers in the Windows format");
        }

        // The rest of the places that show a count or a size say them in a culture: a number turned into text with no
        // culture takes the Windows regional format, and the interface language is the one that goes around it.
        public static void NoNumberTakesTheWindowsFormat()
        {
            string src = Path.Combine(AppTestsI18nCoverage.RepoRoot(), "src");
            Regex bare = new Regex("\\.ToString\\(\"[NnFfPp][0-9]*\"\\)|\\.ToString\\(\"0\\.[0#]+\"\\)|\\.ToString\\([^()\"]*\\? \"0\\.[0#]+\"|\\{[0-9]+:[NnFfPp][0-9]*\\}(?=[^\\n]*(Format|Localize))");
            List<string> bad = new List<string>();
            foreach (string path in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.TrimStart().StartsWith("//") || line.Contains("I18n.Culture") || line.Contains("Guid") || line.Contains("InvariantCulture")) continue;
                    MatchCollection ms = bare.Matches(line);
                    foreach (Match m in ms) bad.Add(path.Substring(src.Length + 1) + ":" + (i + 1) + "  " + m.Value);
                }
            }
            Report(bad, "numbers turned into text with no culture");
        }

        // ---- Minutes

        // "25 min" is an English unit: the Korean pet menu says "25\uBD84", an hour and a quarter "1\uC2DC\uAC04 15\uBD84"; the other
        // languages keep theirs (and a language that only previews English reads the English one).
        public static void FocusMinutesAreLocalized()
        {
            List<string> bad = new List<string>();
            try
            {
                foreach (string lang in AllLanguages)
                {
                    I18n.Apply(lang);
                    string[] want = lang == "ko" ? new[] { "25\uBD84", "1\uC2DC\uAC04", "1\uC2DC\uAC04 15\uBD84", "2\uC2DC\uAC04" } : new[] { "25 min", "1 h", "1 h 15 min", "2 h" };
                    int[] minutes = { 25, 60, 75, 120 };
                    for (int i = 0; i < minutes.Length; i++)
                        if (FocusClock.Minutes(minutes[i]) != want[i]) bad.Add(lang + ": " + minutes[i] + " min reads \"" + Ascii(FocusClock.Minutes(minutes[i])) + "\", expected \"" + Ascii(want[i]) + "\"");
                    if (lang == "ko")
                    {
                        // The choices of the quick reminder say their unit in Korean too.
                        QuickWindow w = Quick(false);
                        Lay(w, w.Width);
                        MacSegmented seg = null;
                        AppTestsI18n.Walk((DependencyObject)w.Content, new HashSet<DependencyObject>(), delegate(DependencyObject d) { if (d is MacSegmented) seg = (MacSegmented)d; });
                        string[] options = (string[])typeof(MacSegmented).GetField("options", NP).GetValue(seg);
                        for (int i = 0; i < 4; i++)
                            if (options[i] != FocusClock.Minutes(new[] { 10, 30, 60, 120 }[i])) bad.Add("ko: quick reminder choice " + i + " reads \"" + Ascii(options[i]) + "\"");
                        Dispose(w);
                    }
                }
            }
            finally { I18n.Apply("es"); }
            Report(bad, "focus minutes not localized");
        }
    }
}
