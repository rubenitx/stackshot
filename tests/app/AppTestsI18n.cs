// Suite "i18n": the language follows Windows in Automatic, existing users keep Spanish, notices keep their kind in both
// languages, and an audit of everything the main window shows in English (text, tooltips and what screen readers hear on
// every page) finds no Spanish left. Windows are built and laid out, never shown.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Stackshot.Tests
{
    static class AppTestsI18n
    {
        const string Suite_ = "i18n";
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly string[] Pages = { "home", "history", "keys", "general", "editor", "record", "mascot", "mascotset", "about" };

        static void T(string name, Action body) { AppTests.Test(Suite_, name, body); }
        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        public static void Suite()
        {
            Func<bool> motion = HomeWindow.HomeMotion;
            HomeWindow.HomeMotion = delegate { return false; };
            CultureInfo ui = Thread.CurrentThread.CurrentUICulture;
            try
            {
                T("auto-follows-the-windows-language", AutoFollowsWindows);
                T("existing-users-keep-spanish", ExistingUsers);
                T("english-offered-once-on-another-windows", EnglishOffered);
                T("notice-kinds-match-in-both-languages", NoticeKinds);
                T("english-audit-of-every-page", EnglishAudit);
                T("spanish-pages-unchanged-by-a-round-trip", RoundTrip);
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = ui;
                HomeWindow.HomeMotion = motion;
                I18n.Apply("es");
            }
        }

        // Automatic reads the Windows display language, as a process started on that Windows gets it.
        static void AutoFollowsWindows()
        {
            string[] spanish = { "es-ES", "es-MX", "es-AR", "ca-ES", "gl-ES", "eu-ES" };
            string[] english = { "en-US", "en-GB", "fr-FR", "de-DE", "pt-BR" };
            foreach (string c in spanish)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(c);
                I18n.Apply("auto");
                Check(I18n.IsSpanish, "Windows in " + c + " should give Spanish");
            }
            foreach (string c in english)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(c);
                I18n.Apply("auto");
                Check(!I18n.IsSpanish, "Windows in " + c + " should give English");
            }
            // An explicit choice wins over Windows.
            I18n.Apply("es");
            Check(I18n.IsSpanish, "es on English Windows");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("es-ES");
            I18n.Apply("en");
            Check(!I18n.IsSpanish, "en on Spanish Windows");
        }

        // A settings file written before languages existed has no Language line: those users keep the Spanish they had.
        static void ExistingUsers()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string old = Path.Combine(dir, "old.ini"), chosen = Path.Combine(dir, "chosen.ini"), none = Path.Combine(dir, "none.ini");
                File.WriteAllText(old, "# Ajustes de Stackshot\nFirstRunDone = 1\nSound = 0\n");
                File.WriteAllText(chosen, "FirstRunDone = 1\nLanguage = en\n");
                Check(Settings.Load(old).Language == "es", "a file without Language should keep Spanish, not " + Settings.Load(old).Language);
                Check(Settings.Load(chosen).Language == "en", "a chosen language is read back");
                Check(Settings.Load(none).Language == "auto", "a new install (no file) follows Windows");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        // Only to whoever has Spanish on a Windows that is not in Spanish, and only until it was offered or chosen.
        static void EnglishOffered()
        {
            Settings s = new Settings();
            s.Language = "es";
            Check(ShotStack.ShouldOfferEnglish(s, new CultureInfo("en-US")), "Spanish on English Windows: offered");
            Check(ShotStack.ShouldOfferEnglish(s, new CultureInfo("fr-FR")), "Spanish on French Windows: offered");
            Check(!ShotStack.ShouldOfferEnglish(s, new CultureInfo("es-ES")), "Spanish on Spanish Windows: nothing to offer");
            Check(!ShotStack.ShouldOfferEnglish(s, new CultureInfo("ca-ES")), "Spanish on Catalan Windows: nothing to offer");
            s.LanguageOffered = true;
            Check(!ShotStack.ShouldOfferEnglish(s, new CultureInfo("en-US")), "offered once only");
            s.LanguageOffered = false;
            s.Language = "auto";
            Check(!ShotStack.ShouldOfferEnglish(s, new CultureInfo("en-US")), "Automatic already follows Windows");
            s.Language = "en";
            Check(!ShotStack.ShouldOfferEnglish(s, new CultureInfo("en-US")), "already in English");
            // An old settings file (no Language line) on English Windows is exactly the case.
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-offer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string old = Path.Combine(dir, "old.ini");
                File.WriteAllText(old, "FirstRunDone = 1\n");
                Check(ShotStack.ShouldOfferEnglish(Settings.Load(old), new CultureInfo("en-US")), "an existing user on English Windows is offered English");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        static void NoticeKinds()
        {
            // Each pair is one notice in Spanish and English: both must be shown as the same kind.
            string[,] pairs =
            {
                { "El sonido ha saturado", "Audio clipped" },
                { "Cine: sin p\u00E9rdidas, verificado", "Cinema: lossless, verified" },
                { "No se han revisado los datos sensibles", "Sensitive data not reviewed" },
                { "No se pudieron unir", "Could not combine" },
                { "La grabaci\u00F3n ha ido a tirones", "Recording was choppy" },
                { "Impr Pant ya es de Stackshot", "Print Screen now belongs to Stackshot" },
                { "Grabaci\u00F3n recuperada", "Recording recovered" },
                { "Texto copiado", "Text copied" },
                { "Algunos atajos est\u00E1n ocupados", "Some shortcuts are occupied" },
                { "No se pudo guardar", "Could not save" },
            };
            for (int i = 0; i < pairs.GetLength(0); i++)
                Check(Notice.KindFor(pairs[i, 0]) == Notice.KindFor(pairs[i, 1]),
                      "'" + pairs[i, 0] + "' is " + Notice.KindFor(pairs[i, 0]) + " but '" + pairs[i, 1] + "' is " + Notice.KindFor(pairs[i, 1]));
        }

        // ---- The audit

        // Spanish gives itself away by its letters or its small words (lower case: "Del" is a key); names are allowed, and
        // so is what Windows itself names in its own language (audio devices).
        static readonly Regex SpanishLetters = new Regex("[\u00E1\u00E9\u00ED\u00F3\u00FA\u00F1\u00C1\u00C9\u00CD\u00D3\u00DA\u00D1\u00BF\u00A1]");
        static readonly Regex SpanishWords = new Regex(@"\b(de|del|la|las|el|los|con|para|una|uno|que|por|sin|al|se|y|tu|tus|en|esta|este|hay|m\u00E1s|aqu\u00ED|nada|todo|captura|capturas|grabar|guardar|abrir|cerrar|ajustes|carpeta|archivo|ventana|pantalla|estilo|nivel|vaso|conmigo|recuperado|Captura|Capturas|Capturar|Grabar|Guardar|Abrir|Cerrar|Copiar|Pegar|Borrar|Quitar|Ajustes|Carpeta|Archivo|Ventana|Pantalla|Estilo|Nivel|Listo|Hecho|Buscar|Mostrar|Ocultar|Volver|Ver)\b");
        static readonly string[] Allowed = { "Espa\u00F1ol", "Stackshot", "Kero-chan", "Le\u00F3n", "Pok\u00E9mon" };

        internal static bool LooksSpanish(string s)
        {
            if (string.IsNullOrEmpty(s) || s.StartsWith("Windows default: ", StringComparison.Ordinal)) return false;
            foreach (string a in Allowed) s = s.Replace(a, "");
            return SpanishLetters.IsMatch(s) || SpanishWords.IsMatch(s);
        }

        // Every page of the main window, in light and dark, then the Markdown/XML window, the editor and the tray panel.
        static void EnglishAudit()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            I18n.Apply("auto");
            Check(!I18n.IsSpanish, "English Windows in Automatic");
            List<string> found = new List<string>();
            foreach (int appearance in new int[] { 1, 2 })
            {
                HomeWindow hw = Make(appearance);
                try
                {
                    foreach (string page in Pages)
                    {
                        SetPage(hw, page);
                        Layout(hw);
                        foreach (string s in Strings(hw.Content))
                            if (LooksSpanish(s)) Add(found, page + ": " + s);
                    }
                }
                finally { hw.Shutdown(); }
            }
            Other(found, "markdown", 40, AuditMarkdown);
            Other(found, "editor", 60, AuditEditor);
            Other(found, "tray", 20, AuditTray);
            Other(found, "card", 6, AuditCard);
            foreach (string f in found) Console.WriteLine("    spanish: " + f);
            Check(found.Count == 0, found.Count + " Spanish texts in the English interface (listed above)");
        }

        // least: what that window shows at the very least, so one that hands over (almost) nothing does not pass unseen.
        static void Other(List<string> found, string where, int least, Func<List<string>> texts)
        {
            List<string> all;
            try { all = texts(); }
            catch (Exception ex) { Add(found, where + ": could not be built (" + ex.GetType().Name + ": " + ex.Message + ")"); return; }
            Console.WriteLine("    " + where + ": " + all.Count + " texts read");
            if (all.Count < least) Add(found, where + ": only " + all.Count + " texts could be read");
            foreach (string s in all) if (LooksSpanish(s)) Add(found, where + ": " + s);
        }

        // The Markdown/XML window empty, then with a Markdown document (read and edit) and an XML one.
        static List<string> AuditMarkdown()
        {
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-docs");
            Directory.CreateDirectory(dir);
            string md = Path.Combine(dir, "notes.md"), xml = Path.Combine(dir, "order.xml");
            File.WriteAllText(md, "# Title\n\nSome **bold** text and a [link](https://example.com).\n\n## Part\n\n- one\n- two\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```\ncode\n```\n");
            File.WriteAllText(xml, "<?xml version=\"1.0\"?>\n<order id=\"7\"><item sku=\"A1\">Pen</item><total>3.50</total></order>\n");
            ConstructorInfo ci = typeof(MarkdownWindow).GetConstructors(NP)[0];
            MarkdownWindow w = (MarkdownWindow)ci.Invoke(new object[] { null });
            try
            {
                w.Left = -6000; w.Top = 0; w.ShowActivated = false; w.ShowInTaskbar = false;
                w.Show();
                w.UpdateLayout();
                List<string> acc = Strings(w);
                acc.Add(w.Title);
                foreach (string p in new string[] { md, xml })
                {
                    typeof(MarkdownWindow).GetMethod("LoadFile", NP).Invoke(w, new object[] { p });
                    Pump(delegate { return w.Doc != null && w.Doc.Path != null && Path.GetFileName(w.Doc.Path) == Path.GetFileName(p); }, 8000);
                    Pump(delegate { return false; }, 300);
                    w.UpdateLayout();
                    acc.AddRange(Strings(w));
                    acc.Add(w.Title);
                    if (p == md)
                    {
                        w.SetEditing(true);
                        Pump(delegate { return false; }, 300);
                        w.UpdateLayout();
                        acc.AddRange(Strings(w));
                        w.SetEditing(false);
                    }
                }
                return acc;
            }
            finally
            {
                Func<string, string, string[], int> keep = DocAlert.Answer;
                DocAlert.Answer = delegate { return 1; };
                try { w.Close(); } catch { }
                DocAlert.Answer = keep;
            }
        }

        // Runs the dispatcher until done() or ms have passed.
        static void Pump(Func<bool> done, int ms)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (!done() && sw.ElapsedMilliseconds < ms)
            {
                System.Windows.Threading.DispatcherFrame f = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, (Action)delegate { f.Continue = false; });
                System.Windows.Threading.Dispatcher.PushFrame(f);
                if (!done()) Thread.Sleep(5);
            }
        }

        static List<string> AuditEditor()
        {
            string png = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-editor.png");
            Directory.CreateDirectory(Path.GetDirectoryName(png));
            using (System.Drawing.Bitmap b = new System.Drawing.Bitmap(320, 200)) b.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            System.Drawing.Bitmap img = new System.Drawing.Bitmap(png);
            Editor e = new Editor(null, png, img);
            try
            {
                // The toolbar and the backdrop panel laid out as on screen, as the a11y suite reads them.
                Bar bar = (Bar)typeof(Editor).GetField("bar", NP).GetValue(e);
                bar.Size = new System.Drawing.Size(1200, bar.Height > 0 ? bar.Height : 52);
                bar.LayoutItems();
                BgPanel bg = (BgPanel)typeof(Editor).GetField("bgPanel", NP).GetValue(e);
                bg.Size = new System.Drawing.Size(252, 900);
                List<string> acc = Accessible(e);
                acc.AddRange(Accessible(bar));
                acc.AddRange(Accessible(bg));
                acc.Add(e.Text);
                return acc;
            }
            finally { try { e.Dispose(); } catch { } }
        }

        // The tray menu: what its rows say and what a screen reader hears.
        static List<string> AuditTray()
        {
            TrayPanel t = new TrayPanel(new Settings());
            try
            {
                typeof(TrayPanel).GetMethod("Rebuild", NP).Invoke(t, new object[] { 1f, 0 });
                List<string> acc = Accessible(t);
                foreach (object it in (System.Collections.IEnumerable)typeof(TrayPanel).GetField("items", NP).GetValue(t))
                {
                    FieldInfo text = it.GetType().GetField("Text");
                    if (text != null) acc.Add((string)text.GetValue(it));
                }
                return acc;
            }
            finally { try { t.Dispose(); } catch { } }
        }

        // A capture's floating card and its buttons.
        static List<string> AuditCard()
        {
            string png = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-card.png");
            Directory.CreateDirectory(Path.GetDirectoryName(png));
            using (System.Drawing.Bitmap b = new System.Drawing.Bitmap(320, 200)) b.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            Card c = new Card(null, png);
            try
            {
                c.Place(new System.Drawing.Rectangle(-32000, -32000, 256, 160), "test", 1f, 0, false);
                return Accessible(c);
            }
            finally { try { c.Dispose(); } catch { } }
        }

        // What screen readers get from a WinForms window Stackshot draws itself: names, descriptions, help and values.
        static List<string> Accessible(System.Windows.Forms.Control c)
        {
            List<string> acc = new List<string>();
            AccessibleWalk(c.AccessibilityObject, acc, 0);
            foreach (System.Windows.Forms.Control k in c.Controls) { acc.Add(k.Text); acc.Add(k.AccessibleName); acc.Add(k.AccessibleDescription); }
            acc.RemoveAll(delegate(string s) { return string.IsNullOrEmpty(s); });
            return acc;
        }

        static void AccessibleWalk(System.Windows.Forms.AccessibleObject o, List<string> acc, int depth)
        {
            if (o == null || depth > 8) return;
            try { acc.Add(o.Name); acc.Add(o.Description); acc.Add(o.Help); acc.Add(o.KeyboardShortcut); } catch { }
            int n = 0;
            try { n = o.GetChildCount(); } catch { }
            for (int i = 0; i < n && i < 400; i++)
            {
                System.Windows.Forms.AccessibleObject k = null;
                try { k = o.GetChild(i); } catch { }
                if (k != null && k != o) AccessibleWalk(k, acc, depth + 1);
            }
        }

        // Spanish, then English, then Spanish again in one window: the texts come back as they were.
        static void RoundTrip()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("es-ES");
            I18n.Apply("es");
            HomeWindow hw = Make(1);
            try
            {
                List<string> before = new List<string>(), after = new List<string>();
                foreach (string page in Pages) { SetPage(hw, page); Layout(hw); before.AddRange(Strings(hw.Content)); }
                I18n.Apply("en");
                foreach (string page in Pages) { SetPage(hw, page); Layout(hw); }
                I18n.Apply("es");
                foreach (string page in Pages) { SetPage(hw, page); Layout(hw); after.AddRange(Strings(hw.Content)); }
                Check(before.Count == after.Count, "texts before " + before.Count + ", after " + after.Count);
                List<string> leftover = new List<string>();
                // Digits aside: examples show the current time, which moves on meanwhile.
                Regex digits = new Regex("[0-9]");
                for (int i = 0; i < before.Count; i++)
                    if (digits.Replace(before[i], "#") != digits.Replace(after[i], "#")) leftover.Add(before[i] + " -> " + after[i]);
                foreach (string l in leftover) Console.WriteLine("    changed: " + l);
                Check(leftover.Count == 0, leftover.Count + " texts changed after going to English and back");
            }
            finally { hw.Shutdown(); }
        }

        static void Add(List<string> found, string s) { if (!found.Contains(s)) found.Add(s); }

        // What a user can read or hear: text and runs, tooltips, and the names screen readers announce for the controls
        // Stackshot draws itself (segments, buttons, switches).
        static List<string> Strings(object root)
        {
            List<string> acc = new List<string>();
            HashSet<DependencyObject> seen = new HashSet<DependencyObject>();
            Walk(root as DependencyObject, seen, delegate(DependencyObject d)
            {
                TextBlock t = d as TextBlock;
                if (t != null)
                {
                    string s = t.Text ?? "";
                    if (s.Length == 0) foreach (Inline i in t.Inlines) { Run r = i as Run; if (r != null) s += r.Text; }
                    acc.Add(s);
                }
                FrameworkElement fe = d as FrameworkElement;
                if (fe != null)
                {
                    string tip = fe.ToolTip as string;
                    if (tip != null) acc.Add(tip);
                    acc.Add(AutomationProperties.GetName(fe));
                    acc.Add(AutomationProperties.GetHelpText(fe));
                    ContentControl cc = fe as ContentControl;
                    if (cc != null && cc.Content is string) acc.Add((string)cc.Content);
                    try
                    {
                        AutomationPeer p = UIElementAutomationPeer.CreatePeerForElement(fe);
                        if (p != null) { acc.Add(p.GetName()); acc.Add(p.GetHelpText()); acc.Add(p.GetItemStatus()); }
                    }
                    catch { }
                }
            });
            acc.RemoveAll(delegate(string s) { return string.IsNullOrEmpty(s); });
            return acc;
        }

        static void Walk(DependencyObject d, HashSet<DependencyObject> seen, Action<DependencyObject> visit)
        {
            if (d == null || !seen.Add(d)) return;
            UIElement u = d as UIElement;
            if (u != null && u.Visibility != Visibility.Visible) return;
            visit(d);
            foreach (object c in LogicalTreeHelper.GetChildren(d)) Walk(c as DependencyObject, seen, visit);
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i), seen, visit);
        }

        static HomeWindow Make(int appearance)
        {
            Ds.Apply(appearance);
            Settings s = new Settings();
            s.SaveFolder = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-shots");
            s.ShowRecent = false;
            s.ShowIntro = false;
            s.Profile = 2;
            s.Appearance = appearance;
            ShotStack owner = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            return new HomeWindow(owner, s);
        }

        static void SetPage(HomeWindow hw, string id)
        {
            try { typeof(HomeWindow).GetMethod("SetPage", NP).Invoke(hw, new object[] { id, false }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("page " + id + ": " + ex.InnerException, ex.InnerException); }
        }

        static void Layout(HomeWindow hw)
        {
            UIElement root = (UIElement)hw.Content;
            root.Measure(new Size(980, 660));
            root.Arrange(new Rect(0, 0, 980, 660));
            root.UpdateLayout();
        }
    }
}
