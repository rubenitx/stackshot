// Round 6 (Stackshot's own notices), suite "w6toast": the notices that replaced the tray balloons. The placement maths are
// pure functions over rectangles; the stack runs on a clock, a quiet state and views given by the test; the toast is
// drawn into images (PNG in STACKSHOT_TEST_DATA\w6toast\shots); its window is only ever shown parked far off screen,
// without the focus. No real input, no hook, no screen read; the only hotkey call registers nothing (an invalid combo).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using D = System.Drawing;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot.Tests
{
    static class AppTestsW6Toast
    {
        const string SuiteName = "w6toast";
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static void Eq<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(what + ": expected <" + expected + ">, got <" + actual + ">");
        }

        static string Root()
        {
            string root = Environment.GetEnvironmentVariable("STACKSHOT_TEST_DATA");
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            return Path.Combine(root, "w6toast");
        }

        static string ShotsDir()
        {
            string d = Environment.GetEnvironmentVariable("STACKSHOT_UI_SHOTS");
            if (string.IsNullOrEmpty(d)) d = Path.Combine(Root(), "shots");
            Directory.CreateDirectory(d);
            return d;
        }

        public static void Suite()
        {
            AppTests.Test(SuiteName, "kinds, clean text and what is spoken", Kinds);
            AppTests.Test(SuiteName, "Notify, NotifyPage and the new overload are recorded with their actions", Routing);
            AppTests.Test(SuiteName, "callers: busy shortcuts, new XML, scroll capture, page buttons", Callers);
            AppTests.Test(SuiteName, "callers: every notice goes through Notify, no tray balloon left (IL)", CallSites);
            AppTests.Test(SuiteName, "Test mode records and never makes a toast", TestModeShowsNothing);
            AppTests.Test(SuiteName, "taskbar edge of a monitor (also hidden, other monitors)", Edges);
            AppTests.Test(SuiteName, "placement: each corner and taskbar side", PlaceCorners);
            AppTests.Test(SuiteName, "placement at 125% and 150%, monitors left of the main one", PlaceDpi);
            AppTests.Test(SuiteName, "placement keeps clear of the captures (above them or the free side)", PlaceAroundCards);
            AppTests.Test(SuiteName, "stack: newest at the bottom, three at most, oldest leaves", StackOrder);
            AppTests.Test(SuiteName, "stack: the same title and text refreshes instead of repeating", StackReplace);
            AppTests.Test(SuiteName, "stack: lifetime, longer for warnings with a button, held while hovered", StackExpiry);
            AppTests.Test(SuiteName, "stack: click runs the action and closes, x only closes", StackClick);
            AppTests.Test(SuiteName, "stack: quiet states hold notices for up to a minute", StackQuiet);
            AppTests.Test(SuiteName, "stack: the captures moving places them again, once per turn", StackFollow);
            AppTests.Test(SuiteName, "stack: no animation when Windows has them off; nothing runs when empty", StackNoAnimation);
            AppTests.Test(SuiteName, "look: kinds, light/dark, long text clipped at 3 lines, button (PNG)", Looks);
            AppTests.Test(SuiteName, "look: hit areas and long words kept inside", LookHits);
            AppTests.Test(SuiteName, "look: paths wrap at their separators, long words count as clipped", LookPaths);
            AppTests.Test(SuiteName, "window: no activation, no taskbar, excluded from capture (parked)", WindowStyles);
            AppTests.Test(SuiteName, "window: UI Automation name, live setting and announcement", WindowAccess);
            AppTests.Test(SuiteName, "window: hover holds it, click runs the action, x only closes", WindowClicks);
            AppTests.Test(SuiteName, "window: slides in and out off screen, theme switch repaints", WindowAnimated);
        }

        // ---- Model

        static void Kinds()
        {
            string[][] cases =
            {
                new[] { "No se pudo fijar la captura", "Error" }, new[] { "No se pudo abrir", "Error" }, new[] { "No se pudo mostrar", "Error" },
                new[] { "No se pudo guardar", "Error" }, new[] { "No se pudo guardar la captura", "Error" }, new[] { "No se pudo copiar", "Error" },
                new[] { "No se pudo abrir el visor", "Error" }, new[] { "No se pudo capturar", "Error" }, new[] { "No se ha podido leer el texto", "Error" },
                new[] { "No se encuentra la captura", "Error" },
                new[] { "No se han revisado los datos sensibles", "Warning" }, new[] { "Algunos atajos est\u00E1n ocupados", "Warning" },
                new[] { "Algunos atajos no funcionan", "Warning" }, new[] { "Grabando sin parte del sonido", "Warning" },
                new[] { "Grabando sin c\u00E1mara", "Warning" }, new[] { "Grabaci\u00F3n detenida", "Warning" },
                new[] { "La grabaci\u00F3n ha ido a tirones", "Warning" }, new[] { "El sonido ha saturado", "Warning" },
                new[] { "Captura con desplazamiento", "Warning" },
                new[] { "Texto copiado", "Success" }, new[] { "Grabaci\u00F3n recuperada", "Success" }, new[] { "Cine: sin p\u00E9rdidas, verificado", "Success" },
                new[] { "Impr Pant ya es de Stackshot", "Success" },
                new[] { "Stackshot", "Info" }, new[] { "Nuevo XML", "Info" }, new[] { "No hay texto", "Info" }, new[] { "", "Info" }, new[] { null, "Info" },
            };
            foreach (string[] c in cases) Eq(c[1], Notice.KindFor(c[0]).ToString(), "kind of \u00AB" + c[0] + "\u00BB");

            Eq("", Notice.Clean(null), "null text");
            Eq("a\nb", Notice.Clean("  a \r\n\r\n\n  b  "), "blank lines and spaces");
            Eq("Primera l\u00EDnea\nSegunda", Notice.Clean("Primera l\u00EDnea\n\nSegunda\n"), "the Cine report's paragraphs");
            Notice n = new Notice(" T\u00EDtulo ", "Uno\n\nDos", NoticeKind.Info);
            Eq("T\u00EDtulo", n.Title, "title trimmed");
            Eq("T\u00EDtulo\nUno\nDos", n.Key, "key");
            Eq("Stackshot: T\u00EDtulo. Uno Dos", n.Spoken, "spoken");
            Eq("Stackshot: Hecho.", new Notice("Hecho.", "", NoticeKind.Success).Spoken, "spoken without text");
            Eq(ToastStack.LongLifeMs, ToastStack.LifeOf(WithAction(new Notice("a", "b", NoticeKind.Warning))), "warning with a button");
            Eq(ToastStack.LifeMs, ToastStack.LifeOf(new Notice("a", "b", NoticeKind.Warning)), "warning without a button");
            Eq(ToastStack.LifeMs, ToastStack.LifeOf(WithAction(new Notice("a", "b", NoticeKind.Error))), "error with a button");
            Check(ToastStack.LifeMs >= 7000 && ToastStack.LifeMs <= 9000 && ToastStack.LongLifeMs >= 11000 && ToastStack.LongLifeMs <= 13000, "lifetimes");
        }

        static Notice WithAction(Notice n)
        {
            n.ActionLabel = "Hacer";
            n.Action = delegate { };
            return n;
        }

        static ShotStack Blank()
        {
            ShotStack st = (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
            GC.SuppressFinalize(st);
            return st;
        }

        static Notice Last()
        {
            List<Notice> r = ShotStack.Recorded;
            Check(r.Count > 0, "nothing recorded");
            return r[r.Count - 1];
        }

        static void Routing()
        {
            ShotStack st = Blank();
            ShotStack.ForgetRecorded();
            st.Notify("No se pudo guardar", "Disco lleno.");
            Notice n = Last();
            Eq("No se pudo guardar", n.Title, "title");
            Eq("Disco lleno.", n.Text, "text");
            Eq(NoticeKind.Error, n.Kind, "kind");
            Check(n.Action == null && n.ActionLabel == null && n.Page == null && n.Folder == null, "a plain notice leads nowhere");

            string dir = Path.Combine(Root(), "recuperado");
            st.Notify("Grabaci\u00F3n recuperada", "Lo grabado est\u00E1 a salvo en la carpeta \u00ABrecuperado\u00BB.", dir);
            n = Last();
            Eq(dir, n.Folder, "folder");
            Eq("Abrir la carpeta", n.ActionLabel, "folder button");
            Check(n.Action != null, "no action for the folder");
            Eq(NoticeKind.Success, n.Kind, "recovered is good news");
            Check(n.Text.IndexOf("Haz clic", StringComparison.Ordinal) < 0, "the text still says to click: " + n.Text);

            st.Notify("Stackshot", "Nada.", (string)null);
            n = Last();
            Check(n.Action == null && n.ActionLabel == null && n.Folder == null, "no folder, no button");

            st.NotifyPage("La grabaci\u00F3n ha ido a tirones", "Se han perdido fotogramas.", "record");
            n = Last();
            Eq("record", n.Page, "page");
            Eq("Ajustes de grabaci\u00F3n", n.ActionLabel, "record button");
            Eq(NoticeKind.Warning, n.Kind, "kind");
            Check(n.Action != null, "no action for the page");
            st.NotifyPage("Algo", "Texto.", "keys");
            Eq("Abrir Atajos", Last().ActionLabel, "keys button");
            st.NotifyPage("Algo", "Texto.", "general");
            Eq("Abrir General", Last().ActionLabel, "general button");
            st.NotifyPage("Algo", "Texto.", null);
            Check(Last().Action == null && Last().ActionLabel == null, "no page, no button");

            int ran = 0;
            st.Notify("Hecho", "Con bot\u00F3n.", NoticeKind.Success, "Ver", delegate { ran++; });
            n = Last();
            Eq("Ver", n.ActionLabel, "own button");
            Eq(NoticeKind.Success, n.Kind, "own kind");
            n.Action();
            Eq(1, ran, "own action");
            st.Notify("Hecho", "Sin acci\u00F3n.", NoticeKind.Info, "Ver", null);
            Check(Last().ActionLabel == null && Last().Action == null, "a label without an action is no button");

            // From another thread (the recorder's): recorded all the same.
            int before = ShotStack.Recorded.Count;
            Thread t = new Thread(delegate() { st.Notify("Grabando sin c\u00E1mara", "No se encuentra."); });
            t.Start();
            t.Join();
            Eq(before + 1, ShotStack.Recorded.Count, "notice from another thread");
            Eq(NoticeKind.Warning, Last().Kind, "camera kind");

            // Bounded: a long Test-mode session keeps only the latest ones.
            for (int i = 0; i < 260; i++) st.Notify("n", i.ToString());
            Check(ShotStack.Recorded.Count <= 200, "recorded list unbounded: " + ShotStack.Recorded.Count);
            Eq("259", Last().Text, "the newest is kept");
            ShotStack.ForgetRecorded();
        }

        static void SetField(object o, string field, object value)
        {
            FieldInfo f = typeof(ShotStack).GetField(field, Priv);
            Check(f != null, "no field " + field + " in ShotStack");
            f.SetValue(o, value);
        }

        static void Callers()
        {
            // Busy shortcuts (an unreadable combo: nothing is registered): a warning with a button to Atajos.
            ShotStack st = Blank();
            Settings s = new Settings();
            foreach (string a in Settings.Actions) s.SetHotkeys(a, "");
            s.SetHotkeys("region", "Ctrl+Nope");
            using (Hotkeys hk = new Hotkeys())
            using (NotifyIcon tray = new NotifyIcon())
            {
                SetField(st, "settings", s);
                SetField(st, "hotkeys", hk);
                SetField(st, "tray", tray);
                ShotStack.ForgetRecorded();
                typeof(ShotStack).GetMethod("RegisterHotkeys", Priv).Invoke(st, new object[] { true });
                Notice n = Last();
                Eq("Algunos atajos no funcionan", n.Title, "busy title");
                Check(n.Text.Contains("\u00ABCtrl+Nope\u00BB no es una combinaci\u00F3n de teclas v\u00E1lida."), "busy text: " + n.Text);
                Check(n.Text.IndexOf("Haz clic", StringComparison.Ordinal) < 0, "the text still says to click: " + n.Text);
                Eq(NoticeKind.Warning, n.Kind, "busy kind");
                Eq("Elegir otros atajos", n.ActionLabel, "busy button");
                Check(n.Action != null && n.Action.Method.Name == "ChooseOtherKeys", "busy action: " + (n.Action == null ? "none" : n.Action.Method.Name));
                Eq(ToastStack.LongLifeMs, ToastStack.LifeOf(n), "a warning with a button stays longer");
                Check(string.IsNullOrEmpty(tray.BalloonTipText), "the tray balloon is still used");
            }

            // A new XML while the viewer has unsaved changes: announced, with a button that opens it in the viewer.
            string dir = Path.Combine(Root(), "xml");
            Directory.CreateDirectory(dir);
            string xml = Path.Combine(dir, "Entrada 0001.xml");
            File.WriteAllText(xml, "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><AltaParte/></soap:Body></soap:Envelope>");
            ShotStack.ForgetRecorded();
            typeof(ShotStack).GetMethod("TellArrivedXml", Priv).Invoke(st, new object[] { xml });
            Notice x = Last();
            Eq("Nuevo XML", x.Title, "XML title");
            Check(x.Text.StartsWith("Entrada 0001.xml (AltaParte). El visor tiene cambios sin guardar", StringComparison.Ordinal), "XML text: " + x.Text);
            Eq("Abrir el XML", x.ActionLabel, "XML button");
            Check(x.Action != null, "no action for the XML");
            Eq(NoticeKind.Info, x.Kind, "XML kind");

            // A real caller elsewhere: the scroll capture refusing a tiny area.
            ShotStack.ForgetRecorded();
            ScrollCapture.Start(st, new D.Rectangle(10, 10, 20, 20));
            Notice sc = Last();
            Eq("Captura con desplazamiento", sc.Title, "scroll title");
            Eq(NoticeKind.Warning, sc.Kind, "scroll kind");
            Check(sc.Action == null, "scroll notice leads nowhere");

            // The tray's latest-XML entry on a folder without any: told, plainly.
            string empty = Path.Combine(Root(), "sin-xml");
            Directory.CreateDirectory(empty);
            foreach (string f in Directory.GetFiles(empty)) File.Delete(f);
            Settings xs = new Settings();
            xs.XmlWatchFolder = empty;
            SetField(st, "settings", xs);
            ShotStack.ForgetRecorded();
            typeof(ShotStack).GetMethod("OpenLatestXml", Priv).Invoke(st, null);
            Notice none = Last();
            Eq("Stackshot", none.Title, "no XML title");
            Eq("No hay ning\u00FAn XML en " + empty + ".", none.Text, "no XML text");
            Eq(NoticeKind.Info, none.Kind, "no XML kind");
            Check(none.Action == null, "no XML leads nowhere");

            // Every section has a readable button.
            foreach (string page in new string[] { "home", "history", "keys", "general", "editor", "record", "mascot", "about", "nope" })
            {
                string label = HomeWindow.NoticeButton(page);
                Check(!string.IsNullOrEmpty(label) && label.Length < 30, "button for " + page + ": " + label);
            }
            ShotStack.ForgetRecorded();
        }

        // Every method call in Stackshot's own code (namespace Stackshot), read from the compiled IL: caller -> callee.
        static List<KeyValuePair<string, MethodBase>> Calls()
        {
            Dictionary<short, System.Reflection.Emit.OpCode> ops = new Dictionary<short, System.Reflection.Emit.OpCode>();
            foreach (FieldInfo f in typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                System.Reflection.Emit.OpCode op = (System.Reflection.Emit.OpCode)f.GetValue(null);
                ops[op.Value] = op;
            }
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            List<KeyValuePair<string, MethodBase>> calls = new List<KeyValuePair<string, MethodBase>>();
            foreach (Type t in typeof(ShotStack).Assembly.GetTypes())
            {
                if (t.Namespace != "Stackshot") continue;
                List<MethodBase> methods = new List<MethodBase>(t.GetMethods(All));
                methods.AddRange(t.GetConstructors(All));
                Type outer = t;
                while (outer.DeclaringType != null) outer = outer.DeclaringType;
                foreach (MethodBase m in methods)
                {
                    MethodBody body = null;
                    try { body = m.GetMethodBody(); } catch { }
                    if (body == null) continue;
                    byte[] il = body.GetILAsByteArray();
                    // An anonymous method is named after the one that holds it: <Keep>b__0 -> Keep.
                    string name = m.Name;
                    if (name.StartsWith("<", StringComparison.Ordinal) && name.IndexOf('>') > 1) name = name.Substring(1, name.IndexOf('>') - 1);
                    string caller = outer.Name + "." + name;
                    int i = 0;
                    while (i < il.Length)
                    {
                        short v = il[i] == 0xFE && i + 1 < il.Length ? unchecked((short)(0xFE00 | il[i + 1])) : il[i];
                        System.Reflection.Emit.OpCode op;
                        if (!ops.TryGetValue(v, out op)) throw new InvalidOperationException("unknown opcode 0x" + v.ToString("X") + " in " + caller);
                        i += op.Size;
                        switch (op.OperandType)
                        {
                            case System.Reflection.Emit.OperandType.InlineNone: break;
                            case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                            case System.Reflection.Emit.OperandType.ShortInlineI:
                            case System.Reflection.Emit.OperandType.ShortInlineVar: i += 1; break;
                            case System.Reflection.Emit.OperandType.InlineVar: i += 2; break;
                            case System.Reflection.Emit.OperandType.InlineI8:
                            case System.Reflection.Emit.OperandType.InlineR: i += 8; break;
                            case System.Reflection.Emit.OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                            case System.Reflection.Emit.OperandType.InlineMethod:
                                {
                                    int token = BitConverter.ToInt32(il, i);
                                    i += 4;
                                    MethodBase target = null;
                                    try
                                    {
                                        target = m.Module.ResolveMethod(token, t.IsGenericType ? t.GetGenericArguments() : null,
                                                                        m.IsGenericMethod ? m.GetGenericArguments() : null);
                                    }
                                    catch { }
                                    if (target != null) calls.Add(new KeyValuePair<string, MethodBase>(caller, target));
                                    break;
                                }
                            default: i += 4; break;
                        }
                    }
                }
            }
            return calls;
        }

        static string Sig(MethodBase m)
        {
            List<string> p = new List<string>();
            foreach (ParameterInfo pi in m.GetParameters()) p.Add(pi.ParameterType.Name);
            return m.Name + "(" + string.Join(",", p.ToArray()) + ")";
        }

        static void CallSites()
        {
            List<KeyValuePair<string, MethodBase>> calls = Calls();
            Check(calls.Count > 5000, "too few calls read: " + calls.Count);
            Dictionary<string, List<string>> routes = new Dictionary<string, List<string>>(); // caller -> overloads it uses
            int notices = 0;
            foreach (KeyValuePair<string, MethodBase> c in calls)
            {
                MethodBase m = c.Value;
                if (m.DeclaringType == typeof(NotifyIcon) && m.Name.IndexOf("BalloonTip", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(c.Key + " still uses the tray balloon: " + m.Name);
                if (m.DeclaringType != typeof(ShotStack) || (m.Name != "Notify" && m.Name != "NotifyPage")) continue;
                notices++;
                List<string> l;
                if (!routes.TryGetValue(c.Key, out l)) routes[c.Key] = l = new List<string>();
                if (!l.Contains(Sig(m))) l.Add(Sig(m));
            }
            Console.WriteLine("    " + notices + " calls to Notify/NotifyPage from " + routes.Count + " methods");
            Check(notices >= 25, "fewer notices than the sources have: " + notices);
            string plain = "Notify(String,String)", folder = "Notify(String,String,String)", page = "NotifyPage(String,String,String)",
                   button = "Notify(String,String,NoticeKind,String,Action)";
            string[][] want =
            {
                new[] { "ShotStack.TellKeys", button },             // busy shortcuts: "Resolverlo" or "Elegir otros atajos"
                new[] { "ShotStack.TellArrivedXml", button },       // new XML with unsaved changes: "Abrir el XML"
                new[] { "ShotStack.OpenArrivedXml", null },         // ... only through TellArrivedXml
                new[] { "ShotStack.RescueOrphans", folder },        // recovered recording: its folder
                new[] { "ShotStack.OpenLatestXml", plain },
                new[] { "ShotStack.Covered", plain },               // sensitive data not checked
                new[] { "Session.Outcome", page },                  // recording stopped or choppy: Grabación
                new[] { "Session.CineOutcome", plain },
                new[] { "ScrollCapture.Start", plain },
                new[] { "Card.OffUi", plain },
                new[] { "HomeWindow.HistoryOpen", plain },
                new[] { "HomeWindow.HistoryMissing", plain },
            };
            foreach (string[] w in want)
            {
                List<string> l;
                bool has = routes.TryGetValue(w[0], out l);
                if (w[1] == null)
                {
                    if (w[0] == "ShotStack.OpenArrivedXml") Check(!has, "OpenArrivedXml notifies directly: " + (has ? string.Join(" ", l.ToArray()) : ""));
                    continue;
                }
                Check(has, "no notice from " + w[0]);
                Check(l.Contains(w[1]), w[0] + " uses " + string.Join(" ", l.ToArray()) + ", not " + w[1]);
            }
            // The overloads themselves end in Post (what records in Test mode and shows a toast otherwise).
            int posts = 0;
            foreach (KeyValuePair<string, MethodBase> c in calls)
                if (c.Key.StartsWith("ShotStack.Notify", StringComparison.Ordinal) && c.Value.DeclaringType == typeof(ShotStack) && c.Value.Name == "Post") posts++;
            Check(posts >= 3, "Notify overloads that post: " + posts);
        }

        static void TestModeShowsNothing()
        {
            ShotStack st = Blank();
            Control sync = new Control();
            sync.CreateControl();
            GC.KeepAlive(sync.Handle);
            bool was = ShotStack.Test;
            try
            {
                SetField(st, "sync", sync);
                ShotStack.Test = true;
                ShotStack.ForgetRecorded();
                int forms = Application.OpenForms.Count;
                st.Notify("No se pudo guardar", "Prueba.");
                st.NotifyPage("Grabaci\u00F3n detenida", "Prueba.", "record");
                st.Notify("Grabaci\u00F3n recuperada", "Prueba.", Root());
                st.Notify("Hecho", "Prueba.", NoticeKind.Success, "Ver", delegate { });
                Eq(4, ShotStack.Recorded.Count, "recorded in Test mode");
                Check(typeof(ShotStack).GetField("toasts", Priv).GetValue(st) == null, "a toast stack was made in Test mode");
                Eq(forms, Application.OpenForms.Count, "a window was made in Test mode");
                foreach (Form f in Application.OpenForms) Check(!(f is ToastWindow), "a toast window exists");
            }
            finally
            {
                ShotStack.Test = was;
                sync.Dispose();
                ShotStack.ForgetRecorded();
            }
        }

        // ---- Placement maths

        static D.Rectangle R(int x, int y, int w, int h) { return new D.Rectangle(x, y, w, h); }

        static void Edges()
        {
            D.Rectangle mon = R(0, 0, 1920, 1080);
            Eq(ToastPlan.Bottom, ToastPlan.EdgeOf(mon, R(0, 0, 1920, 1032), D.Rectangle.Empty), "bottom");
            Eq(ToastPlan.Top, ToastPlan.EdgeOf(mon, R(0, 48, 1920, 1032), D.Rectangle.Empty), "top");
            Eq(ToastPlan.Left, ToastPlan.EdgeOf(mon, R(62, 0, 1858, 1080), D.Rectangle.Empty), "left");
            Eq(ToastPlan.Right, ToastPlan.EdgeOf(mon, R(0, 0, 1858, 1080), D.Rectangle.Empty), "right");
            // A taskbar that hides itself leaves the whole monitor as work area: its window says where it is.
            Eq(ToastPlan.Bottom, ToastPlan.EdgeOf(mon, mon, R(0, 1078, 1920, 48)), "hidden at the bottom");
            Eq(ToastPlan.Top, ToastPlan.EdgeOf(mon, mon, R(0, -46, 1920, 48)), "hidden at the top");
            Eq(ToastPlan.Left, ToastPlan.EdgeOf(mon, mon, R(-60, 0, 62, 1080)), "hidden on the left");
            Eq(ToastPlan.Right, ToastPlan.EdgeOf(mon, mon, R(1918, 0, 62, 1080)), "hidden on the right");
            // A second monitor with no taskbar of its own.
            D.Rectangle second = R(-2560, -200, 2560, 1440);
            Eq(ToastPlan.Bottom, ToastPlan.EdgeOf(second, second, R(0, 1032, 1920, 48)), "other monitor");
            Eq(ToastPlan.Top, ToastPlan.EdgeOf(second, R(-2560, -152, 2560, 1392), D.Rectangle.Empty), "other monitor, taskbar on top");
            Eq(ToastPlan.Bottom, ToastPlan.EdgeOf(mon, mon, D.Rectangle.Empty), "nothing known");
        }

        static D.Size[] Sizes(params int[] heights)
        {
            D.Size[] s = new D.Size[heights.Length];
            for (int i = 0; i < heights.Length; i++) s[i] = new D.Size(360, heights[i]);
            return s;
        }

        static void PlaceCorners()
        {
            D.Rectangle[] none = new D.Rectangle[0];
            // Taskbar at the bottom: bottom-right, newest lowest, 10 px apart.
            D.Rectangle work = R(0, 0, 1920, 1032);
            D.Rectangle[] r = ToastPlan.Place(work, false, false, Sizes(80, 100, 120), 16, 10, none);
            Eq(R(1544, 896, 360, 120), r[2], "newest at the bottom-right");
            Eq(R(1544, 786, 360, 100), r[1], "the one before above it");
            Eq(R(1544, 696, 360, 80), r[0], "oldest on top");
            // Taskbar at the top: top-right, oldest at the top and the newest below.
            work = R(0, 48, 1920, 1032);
            Check(ToastPlan.EdgeOf(R(0, 0, 1920, 1080), work, D.Rectangle.Empty) == ToastPlan.Top, "top edge");
            r = ToastPlan.Place(work, true, false, Sizes(80, 100), 16, 10, none);
            Eq(R(1544, 64, 360, 80), r[0], "oldest at the top-right");
            Eq(R(1544, 154, 360, 100), r[1], "newest below it");
            // Taskbar on the left or the right: still the bottom-right of the work area.
            r = ToastPlan.Place(R(62, 0, 1858, 1080), false, false, Sizes(90), 16, 10, none);
            Eq(R(1544, 974, 360, 90), r[0], "taskbar on the left");
            r = ToastPlan.Place(R(0, 0, 1858, 1080), false, false, Sizes(90), 16, 10, none);
            Eq(R(1482, 974, 360, 90), r[0], "taskbar on the right");
            // The other corners, asked for directly.
            r = ToastPlan.Place(R(0, 0, 1920, 1032), false, true, Sizes(90), 16, 10, none);
            Eq(R(16, 926, 360, 90), r[0], "bottom-left");
            r = ToastPlan.Place(R(0, 0, 1920, 1032), true, true, Sizes(90), 16, 10, none);
            Eq(R(16, 16, 360, 90), r[0], "top-left");
            Eq(0, ToastPlan.Place(work, false, false, new D.Size[0], 16, 10, none).Length, "nothing to place");
            Eq(0L, ToastPlan.Badness(r, R(0, 0, 1920, 1032), none), "clean spot");
        }

        static void PlaceDpi()
        {
            // 150%: everything comes measured at that scale, margins included.
            Notice n = new Notice("Algunos atajos est\u00E1n ocupados", "Ctrl + May\u00FAs + Alt + F10 los usa otro programa.", NoticeKind.Warning);
            ToastLook look = ToastLook.For(WithAction(n), 1.5f);
            Eq(540, look.Size.Width, "width at 150%");
            D.Rectangle work = R(0, 0, 2880, 1728);
            D.Rectangle[] r = ToastPlan.Place(work, false, false, new D.Size[] { look.Size }, 24, 15, new D.Rectangle[0]);
            Eq(R(2880 - 24 - 540, 1728 - 24 - look.Size.Height, 540, look.Size.Height), r[0], "bottom-right at 150%");
            // 125% on a monitor left of and above the main one (negative coordinates).
            ToastLook l125 = ToastLook.For(n, 1.25f);
            Eq(450, l125.Size.Width, "width at 125%");
            D.Rectangle second = R(-2400, -300, 2400, 1350);
            r = ToastPlan.Place(second, false, false, new D.Size[] { l125.Size, l125.Size }, 20, 13, new D.Rectangle[0]);
            Eq(-20 - 450, r[1].X, "x on the left monitor");
            Eq(-300 + 1350 - 20 - l125.Size.Height, r[1].Y, "y on the left monitor");
            Eq(r[1].Y - 13 - l125.Size.Height, r[0].Y, "stacked at 125%");
            Check(look.Size.Height > ToastLook.For(WithAction(n), 1f).Size.Height * 1.4, "height scales too");
        }

        static void PlaceAroundCards()
        {
            D.Rectangle work = R(0, 0, 1920, 1032);
            // The usual: captures at the bottom-left never touch the toasts at the right.
            D.Rectangle[] cards = { R(16, 870, 256, 146), R(16, 714, 256, 146) };
            D.Rectangle[] r = ToastPlan.Place(work, false, false, Sizes(80), 16, 10, cards);
            Eq(R(1544, 936, 360, 80), r[0], "captures on the left");
            // Something at the bottom-right corner (a narrow work area, a recording bar): right above it, same side.
            D.Rectangle[] corner = { R(1600, 900, 300, 116) };
            r = ToastPlan.Place(work, false, false, Sizes(80, 90), 16, 10, corner);
            Eq(900 - 10, r[1].Bottom, "newest just above it");
            Eq(1544, r[1].X, "same side");
            Eq(0L, ToastPlan.Badness(r, work, corner), "no overlap above it");
            // That side full to the top: the free side.
            D.Rectangle[] column = { R(1500, 10, 420, 1010) };
            r = ToastPlan.Place(work, false, false, Sizes(80), 16, 10, column);
            Eq(R(16, 936, 360, 80), r[0], "free side");
            // A narrow portrait monitor where the captures' column reaches the toasts': above the captures.
            D.Rectangle narrow = R(0, 0, 600, 1200);
            D.Rectangle[] stack = { R(16, 1030, 256, 154), R(16, 866, 256, 154) };
            r = ToastPlan.Place(narrow, false, false, Sizes(80, 80), 16, 10, stack);
            Eq(0L, ToastPlan.Badness(r, narrow, stack), "no overlap on a narrow monitor");
            Eq(866 - 10, r[1].Bottom, "above the captures");
            Eq(224, r[1].X, "right side still");
            // Taskbar at the top and something at the top-right: right below it.
            D.Rectangle top = R(0, 48, 1920, 1032);
            D.Rectangle[] upper = { R(1500, 60, 400, 200) };
            r = ToastPlan.Place(top, true, false, Sizes(80), 16, 10, upper);
            Eq(270, r[0].Y, "below it at the top");
            // Nowhere clean: the least covered spot, still inside the work area.
            D.Rectangle small = R(0, 0, 400, 300);
            D.Rectangle[] full = { R(0, 0, 400, 260) };
            r = ToastPlan.Place(small, false, false, Sizes(80), 16, 10, full);
            Check(small.Contains(r[0]), "out of the work area: " + r[0]);
            Check(ToastPlan.Badness(r, small, full) < 360L * 80, "not the least covered: " + r[0]);
            // Empty or zero rectangles (a card not laid out yet) are ignored.
            r = ToastPlan.Place(work, false, false, Sizes(80), 16, 10, new D.Rectangle[] { D.Rectangle.Empty, R(1600, 950, 0, 0) });
            Eq(R(1544, 936, 360, 80), r[0], "empty rectangles");
        }

        // ---- The stack, with its clock, quiet state and views given here

        sealed class FakeView : IToastView
        {
            public readonly ToastStack.Entry Entry;
            public int Places, Renews, Departs, Announces, From;
            public bool Animated, DepartAnimated;
            public D.Rectangle Body;
            public float Scale;
            public int Height = 80;
            public FakeView(ToastStack.Entry e) { Entry = e; }
            public D.Size Measure(float scale) { return new D.Size((int)Math.Round(360 * scale), (int)Math.Round(Height * scale)); }
            public void Place(D.Rectangle body, float scale, int enterFrom, bool animate) { Places++; Body = body; Scale = scale; From = enterFrom; Animated = animate; }
            public void Renew() { Renews++; }
            public void Depart(bool animate) { Departs++; DepartAnimated = animate; }
            public void Announce() { Announces++; }
        }

        sealed class Rig
        {
            public double Now = 1000;
            public string QuietReason;
            public bool Animate = true;
            public ToastArea Area = MakeArea(R(0, 0, 1920, 1080), R(0, 0, 1920, 1032), 1f, false);
            public readonly List<D.Rectangle> Occupied = new List<D.Rectangle>();
            public readonly List<FakeView> Views = new List<FakeView>();
            public readonly List<double> Wakes = new List<double>();
            public readonly List<Action> Posted = new List<Action>();
            public bool RunPosted = true;
            public int AreaCalls;
            public readonly ToastStack Stack = new ToastStack();

            public Rig()
            {
                Stack.Clock = delegate { return Now; };
                Stack.Quiet = delegate(D.Rectangle m) { return QuietReason; };
                Stack.Area = delegate(string device) { AreaCalls++; return Area; };
                Stack.Occupied = delegate { return new List<D.Rectangle>(Occupied); };
                Stack.MakeView = delegate(ToastStack.Entry e) { FakeView v = new FakeView(e); Views.Add(v); return v; };
                Stack.Animate = delegate { return Animate; };
                Stack.Post = delegate(Action a) { Posted.Add(a); if (RunPosted) a(); };
                Stack.Wake = delegate(double ms) { Wakes.Add(ms); };
            }

            public FakeView View(int i) { return (FakeView)Stack.Shown[i].View; }
            public double LastWake { get { return Wakes.Count == 0 ? double.NaN : Wakes[Wakes.Count - 1]; } }
        }

        static ToastArea MakeArea(D.Rectangle bounds, D.Rectangle work, float scale, bool top)
        {
            ToastArea a = new ToastArea();
            a.Device = "\\\\.\\DISPLAY1";
            a.Bounds = bounds;
            a.Work = work;
            a.Scale = scale;
            a.Top = top;
            return a;
        }

        static Notice N(string title) { return new Notice(title, "Texto de " + title + ".", NoticeKind.Info); }

        static void StackOrder()
        {
            Rig g = new Rig();
            g.Stack.Show(N("uno"));
            Eq(1, g.Stack.Shown.Count, "one");
            FakeView first = g.View(0);
            Eq(R(1544, 936, 360, 80), first.Body, "first at the bottom-right");
            Eq(1, first.Places, "placed once");
            Eq(1, first.From, "enters from the right");
            Check(first.Animated, "animated");
            Eq(1, first.Announces, "announced");
            Eq(ToastStack.LifeMs, g.LastWake, "wakes for its expiry");
            g.Now += 100;
            g.Stack.Show(N("dos"));
            g.Now += 100;
            g.Stack.Show(N("tres"));
            Eq(3, g.Stack.Shown.Count, "three");
            Eq("tres", g.Stack.Shown[2].Notice.Title, "newest last");
            Eq(R(1544, 936, 360, 80), g.View(2).Body, "newest at the bottom");
            Eq(R(1544, 846, 360, 80), g.View(1).Body, "second above");
            Eq(R(1544, 756, 360, 80), g.View(0).Body, "oldest on top");
            Eq(3, first.Places, "the oldest moved up twice");
            g.Stack.Show(N("cuatro"));
            Eq(3, g.Stack.Shown.Count, "never more than three");
            Eq(1, first.Departs, "the oldest left");
            Check(first.DepartAnimated, "it left animated");
            Eq("dos", g.Stack.Shown[0].Notice.Title, "now the oldest");
            Eq("cuatro", g.Stack.Shown[2].Notice.Title, "now the newest");
            Eq(R(1544, 936, 360, 80), g.View(2).Body, "newest at the bottom again");
            // The oldest under the mouse is kept: the next one leaves instead.
            g.Stack.Hover(g.Stack.Shown[0], true);
            FakeView second = g.View(1);
            g.Stack.Show(N("cinco"));
            Eq("dos", g.Stack.Shown[0].Notice.Title, "the hovered one stays");
            Eq(1, second.Departs, "the next oldest left");
            // The same monitor while any is shown.
            Eq(5, g.Views.Count, "views made");
            Check(g.Stack.CurrentArea != null && g.Stack.CurrentArea.Device == g.Area.Device, "area kept");
            // Area unknown (no monitor): nothing happens.
            Rig none = new Rig();
            none.Area = null;
            none.Stack.Show(N("x"));
            Eq(0, none.Stack.Shown.Count, "no area, no toast");
        }

        static void StackReplace()
        {
            Rig g = new Rig();
            g.Stack.Show(N("uno"));
            FakeView v = g.View(0);
            g.Now += 5000;
            int ran = 0;
            Notice again = WithAction(N("uno"));
            again.Action = delegate { ran++; };
            g.Stack.Show(again);
            Eq(1, g.Stack.Shown.Count, "not repeated");
            Eq(1, g.Views.Count, "no new view");
            Eq(1, v.Renews, "refreshed");
            Eq(2, v.Announces, "announced again");
            Check(ReferenceEquals(again, g.Stack.Shown[0].Notice), "the new notice (its action) replaces the old");
            Eq(g.Now + ToastStack.LifeMs, g.Stack.Shown[0].Deadline, "its time starts again");
            g.Stack.Click(g.Stack.Shown[0], false);
            Eq(1, ran, "the new action runs");
            // Same title, other text: another notice.
            g.Stack.Show(new Notice("uno", "Otro texto.", NoticeKind.Info));
            g.Stack.Show(new Notice("uno", "Texto de uno.", NoticeKind.Info));
            Eq(2, g.Stack.Shown.Count, "different text is a different notice");
            // While hovered, a refresh gives it its whole time again for when the mouse leaves.
            g.Stack.Hover(g.Stack.Shown[1], true);
            g.Now += 7000;
            g.Stack.Show(new Notice("uno", "Texto de uno.", NoticeKind.Info));
            Eq(ToastStack.LifeMs, g.Stack.Shown[1].Left, "time left while hovered");
        }

        static void StackExpiry()
        {
            Rig g = new Rig();
            g.Stack.Show(N("info"));
            g.Stack.Show(WithAction(new Notice("aviso", "Con bot\u00F3n.", NoticeKind.Warning)));
            g.Stack.Show(WithAction(new Notice("error", "Con bot\u00F3n.", NoticeKind.Error)));
            Eq(ToastStack.LifeMs, g.LastWake, "next wake at the first expiry");
            g.Now += ToastStack.LifeMs - 1;
            g.Stack.Tick();
            Eq(3, g.Stack.Shown.Count, "nothing before 8 s");
            g.Now += 1;
            g.Stack.Tick();
            Eq(1, g.Stack.Shown.Count, "info and error gone at 8 s");
            Eq("aviso", g.Stack.Shown[0].Notice.Title, "the warning with a button stays");
            Eq(R(1544, 936, 360, 80), g.View(0).Body, "it slides down to the bottom");
            Eq(ToastStack.LongLifeMs - ToastStack.LifeMs, g.LastWake, "wakes for the warning");
            g.Now += ToastStack.LongLifeMs - ToastStack.LifeMs;
            g.Stack.Tick();
            Eq(0, g.Stack.Shown.Count, "warning gone at 12 s");
            Eq(-1.0, g.LastWake, "nothing more to wake for");
            Check(g.Stack.CurrentArea == null, "the monitor is chosen again next time");

            // Hover: the time stops, and after the mouse leaves it stays at least 3 s more.
            g.Stack.Show(N("leer"));
            ToastStack.Entry e = g.Stack.Shown[0];
            g.Now += 5000;
            g.Stack.Hover(e, true);
            Eq(-1.0, g.LastWake, "no timer while the only toast is hovered");
            g.Now += 60000;
            g.Stack.Tick();
            Eq(1, g.Stack.Shown.Count, "kept while hovered");
            g.Stack.Hover(e, false);
            Eq(g.Now + 3000, e.Deadline, "3 s left after the mouse leaves");
            g.Now += 2999;
            g.Stack.Tick();
            Eq(1, g.Stack.Shown.Count, "still there");
            g.Now += 1;
            g.Stack.Tick();
            Eq(0, g.Stack.Shown.Count, "gone");
            // With more time left than that, it keeps it.
            g.Stack.Show(N("largo"));
            e = g.Stack.Shown[0];
            g.Now += 1000;
            g.Stack.Hover(e, true);
            g.Now += 500;
            g.Stack.Hover(e, false);
            Eq(g.Now + ToastStack.LifeMs - 1000, e.Deadline, "keeps the 7 s it had");
            g.Stack.Hover(e, false);
            Eq(g.Now + ToastStack.LifeMs - 1000, e.Deadline, "leaving twice changes nothing");
        }

        static void StackClick()
        {
            Rig g = new Rig();
            g.RunPosted = false;
            int ran = 0;
            Notice a = WithAction(N("a"));
            a.Action = delegate { ran++; };
            g.Stack.Show(a);
            g.Stack.Show(N("b"));
            FakeView va = g.View(0), vb = g.View(1);
            g.Stack.Click(g.Stack.Shown[0], false);
            Eq(1, va.Departs, "closed on click");
            Eq(0, ran, "the action runs after the click is handled");
            Eq(1, g.Posted.Count, "action posted");
            g.Posted[0]();
            Eq(1, ran, "action ran");
            Eq(1, g.Stack.Shown.Count, "one left");
            g.Stack.Click(g.Stack.Shown[0], true);
            Eq(1, vb.Departs, "x closes");
            Eq(1, g.Posted.Count, "x runs nothing");
            // Clicks on one already gone do nothing.
            ToastStack.Entry gone = null;
            g.Stack.Show(a);
            gone = g.Stack.Shown[0];
            g.Stack.Click(gone, true);
            g.Stack.Click(gone, false);
            Eq(1, g.Posted.Count, "a click after it left");
            // Without an action a click only closes it; an action that fails is logged, not thrown.
            g.RunPosted = true;
            g.Stack.Show(N("sin"));
            g.Stack.Click(g.Stack.Shown[0], false);
            Eq(0, g.Stack.Shown.Count, "closed without an action");
            Notice bad = WithAction(N("mal"));
            bad.Action = delegate { throw new IOException("prueba"); };
            g.Stack.Show(bad);
            g.Stack.Click(g.Stack.Shown[0], false);
            Eq(0, g.Stack.Shown.Count, "closed after a failing action");
            // Right after a click the others move into place.
            g.Stack.Show(N("p"));
            g.Stack.Show(N("q"));
            FakeView vp = g.View(0);
            g.Stack.Click(g.Stack.Shown[1], true);
            Eq(R(1544, 936, 360, 80), vp.Body, "the other slides down");
        }

        static void StackQuiet()
        {
            Eq(null, ToastQuiet.ReasonFor(5, false), "accepts notifications");
            Eq(null, ToastQuiet.ReasonFor(0, false), "unknown");
            Check(ToastQuiet.ReasonFor(1, false) != null, "locked or away");
            Eq(null, ToastQuiet.ReasonFor(2, false), "full screen on another monitor");
            Check(ToastQuiet.ReasonFor(2, true) != null, "full screen over the toasts");
            Check(ToastQuiet.ReasonFor(3, false) != null, "a game");
            Check(ToastQuiet.ReasonFor(4, false) != null, "presenting");
            Check(ToastQuiet.ReasonFor(6, false) != null, "quiet time");
            Check(ToastQuiet.ReasonFor(7, true) != null && ToastQuiet.ReasonFor(7, false) == null, "a full-screen Store app");
            string live = ToastQuiet.Reason(R(-32000, -32000, 100, 100));
            Console.WriteLine("    quiet state now: " + (live ?? "none"));

            Rig g = new Rig();
            g.QuietReason = "modo presentaci\u00F3n";
            g.Stack.Show(N("uno"));
            Eq(0, g.Stack.Shown.Count, "nothing pops up while presenting");
            Eq(0, g.Views.Count, "no window made");
            Eq(1, g.Stack.Waiting.Count, "it waits");
            Eq(ToastStack.RecheckMs, g.LastWake, "checked again in 2 s");
            g.Stack.Show(N("uno"));
            Eq(1, g.Stack.Waiting.Count, "the same one waits once");
            g.Now += 20000;
            g.Stack.Tick();
            Eq(1, g.Stack.Waiting.Count, "still presenting");
            g.QuietReason = null;
            g.Now += 10000;
            g.Stack.Tick();
            Eq(0, g.Stack.Waiting.Count, "presenting ended");
            Eq(1, g.Stack.Shown.Count, "shown now");
            Eq(1, g.View(0).Announces, "announced when shown");

            Rig late = new Rig();
            late.QuietReason = "juego a pantalla completa";
            late.Stack.Show(N("viejo"));
            late.Now += 30000;
            late.Stack.Show(N("nuevo"));
            late.Now += 31000;
            late.Stack.Tick();
            Eq(1, late.Stack.Waiting.Count, "the one older than a minute is dropped");
            Eq("nuevo", late.Stack.Waiting[0].Title, "the recent one waits on");
            late.Stack.Show(N("a"));
            late.Stack.Show(N("b"));
            late.Stack.Show(N("c"));
            Eq(3, late.Stack.Waiting.Count, "at most three wait");
            Eq("a", late.Stack.Waiting[0].Title, "the oldest waiting is dropped first");
            late.Now += 61000;
            late.Stack.Tick();
            Eq(0, late.Stack.Waiting.Count, "all too old");
            Eq(-1.0, late.LastWake, "nothing left to check");
            Eq(0, late.Views.Count, "never shown");

            // Already on screen when it starts: those stay, new ones wait.
            Rig mid = new Rig();
            mid.Stack.Show(N("antes"));
            mid.QuietReason = "aplicaci\u00F3n a pantalla completa";
            mid.Stack.Show(N("durante"));
            Eq(1, mid.Stack.Shown.Count, "the one shown stays");
            Eq(1, mid.Stack.Waiting.Count, "the new one waits");
        }

        static void StackFollow()
        {
            Rig g = new Rig();
            g.RunPosted = false;
            g.Stack.Later();
            Eq(0, g.Posted.Count, "nothing to place: nothing posted");
            g.Stack.Show(N("uno"));
            FakeView v = g.View(0);
            Eq(R(1544, 936, 360, 80), v.Body, "at the corner");
            // A capture card lands there (a narrow monitor): placed again once per turn, however many layouts ran.
            g.Occupied.Add(R(1500, 900, 420, 120));
            g.Stack.Later();
            g.Stack.Later();
            g.Stack.Later();
            Eq(1, g.Posted.Count, "coalesced");
            g.Posted[0]();
            Check(!v.Body.IntersectsWith(g.Occupied[0]), "still over the capture: " + v.Body);
            Eq(2, v.Places, "moved once");
            // Nothing changed: not moved again.
            g.Stack.Later();
            g.Posted[1]();
            Eq(2, v.Places, "not moved when nothing changed");
            // A display change to 150%: measured and placed at the new scale.
            g.Occupied.Clear();
            g.Area = MakeArea(R(0, 0, 2880, 1620), R(0, 0, 2880, 1548), 1.5f, false);
            g.Stack.Later();
            g.Posted[2]();
            Eq(1.5f, v.Scale, "new scale");
            Eq(R(2880 - 24 - 540, 1548 - 24 - 120, 540, 120), v.Body, "placed at 150%");
        }

        static void StackNoAnimation()
        {
            Rig g = new Rig();
            g.Animate = false;
            g.Stack.Show(N("uno"));
            g.Stack.Show(N("dos"));
            Check(!g.View(0).Animated && !g.View(1).Animated, "placed without animation");
            FakeView first = g.View(0);
            g.Stack.Click(g.Stack.Shown[0], true);
            Check(first.Departs == 1 && !first.DepartAnimated, "left without animation");
            g.Stack.CloseAll();
            Eq(0, g.Stack.Shown.Count, "closed all");
            // Nothing scheduled with nothing shown or waiting.
            Rig idle = new Rig();
            idle.Stack.Tick();
            Eq(-1.0, idle.LastWake, "no timer when empty");
            Console.WriteLine("    Windows animations: " + (ToastQuiet.AnimationsOn() ? "on" : "off"));
        }

        // ---- The look, drawn into images

        static readonly string LongText = "Ctrl + May\u00FAs + Alt + F10 y Ctrl + May\u00FAs + Alt + F11 los usa otro programa (\u00BFOneDrive, ShareX, Lightshot, Greenshot\u2026?). " +
                                          "\u00ABCtrl+Nope\u00BB no es una combinaci\u00F3n de teclas v\u00E1lida. Impr Pant est\u00E1 puesto dos veces en los atajos de Stackshot: solo cuenta la primera.";

        static MI.BitmapSource Render(ToastLook look, int hot, bool dark)
        {
            int pad = (int)Math.Round(30 * look.Scale);
            int w = look.Size.Width + 2 * pad, h = look.Size.Height + 2 * pad;
            MI.BitmapSource shadow = ToastLook.Shadow(w, h, pad, look);
            M.LinearGradientBrush desk = new M.LinearGradientBrush(dark ? Ds.Rgb(30, 40, 60) : Ds.Rgb(200, 215, 235), dark ? Ds.Rgb(12, 14, 20) : Ds.Rgb(235, 225, 210), 45);
            desk.Freeze();
            return Ink.Render(w, h, delegate(M.DrawingContext dc)
            {
                dc.DrawRectangle(desk, null, new W.Rect(0, 0, w, h));
                dc.DrawImage(shadow, new W.Rect(0, 0, w, h));
                dc.PushTransform(new M.TranslateTransform(pad, pad));
                look.Paint(dc, hot);
                dc.Pop();
            }, null);
        }

        static void SavePng(MI.BitmapSource b, string name)
        {
            MI.PngBitmapEncoder e = new MI.PngBitmapEncoder();
            e.Frames.Add(MI.BitmapFrame.Create(b));
            using (FileStream fs = File.Create(Path.Combine(ShotsDir(), name))) e.Save(fs);
        }

        static M.Color Px(MI.BitmapSource b, int x, int y)
        {
            byte[] p = new byte[4];
            b.CopyPixels(new W.Int32Rect(x, y, 1, 1), p, 4, 0);
            return M.Color.FromArgb(p[3], p[2], p[1], p[0]);
        }

        static bool Near(M.Color a, M.Color b, int tol)
        {
            return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;
        }

        // Every pixel of that body rectangle is the card's background.
        static bool Plain(MI.BitmapSource b, int pad, D.Rectangle r, M.Color bg, out string where)
        {
            where = null;
            for (int y = r.Top; y < r.Bottom; y++)
                for (int x = r.Left; x < r.Right; x++)
                    if (!Near(Px(b, pad + x, pad + y), bg, 6)) { where = x + "," + y + " is " + Px(b, pad + x, pad + y); return false; }
            return true;
        }

        static void Looks()
        {
            bool dark = Ds.Dark;
            try
            {
                foreach (int mode in new int[] { 1, 2 })
                {
                    Ds.Apply(mode);
                    Palette pal = Ds.Brushes;
                    string theme = mode == 1 ? "light" : "dark";
                    M.Color bg = mode == 1 ? Ds.Rgb(251, 251, 252) : Ds.Rgb(42, 42, 45);
                    foreach (NoticeKind kind in new NoticeKind[] { NoticeKind.Info, NoticeKind.Warning, NoticeKind.Error, NoticeKind.Success })
                    {
                        foreach (float scale in new float[] { 1f, 1.5f })
                        {
                            int pad = (int)Math.Round(30 * scale);
                            // Short, without a button.
                            Notice shortN = new Notice(kind == NoticeKind.Error ? "No se pudo guardar la captura" : kind == NoticeKind.Warning ? "Grabando sin c\u00E1mara"
                                                       : kind == NoticeKind.Success ? "Texto copiado" : "Stackshot", "Una l\u00EDnea corta.", kind);
                            ToastLook a = ToastLook.For(shortN, scale);
                            Eq((int)Math.Round(360 * scale), a.Size.Width, "width " + kind + " " + scale);
                            Eq(1, a.Lines, "one line");
                            Check(!a.Clipped, "short text clipped");
                            Check(a.ButtonBox.IsEmpty, "a button without an action");
                            MI.BitmapSource img = Render(a, ToastLook.None, mode == 2);
                            SavePng(img, "toast-" + theme + "-" + kind.ToString().ToLowerInvariant() + "-short-" + (int)(scale * 100) + ".png");
                            Check(Near(Px(img, pad + a.Size.Width / 2, pad + (int)Math.Round(3 * scale)), bg, 2), "background " + theme + ": " + Px(img, pad + a.Size.Width / 2, pad + 3));
                            if (kind != NoticeKind.Info)
                            {
                                M.Color want = kind == NoticeKind.Error ? pal.Red : kind == NoticeKind.Warning ? pal.Orange : pal.Green;
                                int cx = pad + a.Icon.X + a.Icon.Width / 2 + (int)(a.Icon.Width * 0.36), cy = pad + a.Icon.Y + a.Icon.Height / 2;
                                Check(Near(Px(img, cx, cy), want, 12), kind + " icon color: " + Px(img, cx, cy));
                            }
                            else Check(!Near(Px(img, pad + a.Icon.X + a.Icon.Width / 2, pad + a.Icon.Y + a.Icon.Height / 2), bg, 10), "no app icon");
                            Eq((int)Math.Ceiling(Math.Max(a.TextTop + a.LineHeight, (14 + 32) * scale) + 14 * scale), a.Size.Height, "height of one line");

                            // Long, with a button: three lines at most, an ellipsis, the button under the text.
                            Notice longN = new Notice(shortN.Title, LongText, kind);
                            longN.ActionLabel = kind == NoticeKind.Warning ? "Elegir otros atajos" : "Abrir la carpeta";
                            longN.Action = delegate { };
                            ToastLook b = ToastLook.For(longN, scale);
                            Eq(ToastLook.MaxLines, b.Lines, "long text lines");
                            Check(b.Clipped, "long text not clipped");
                            Check(!b.ButtonBox.IsEmpty, "no button");
                            Check(new D.Rectangle(D.Point.Empty, b.Size).Contains(b.ButtonBox), "button outside the card");
                            Check(b.ButtonBox.Top >= b.TextTop + 3 * b.LineHeight, "button over the text");
                            Eq(b.Size.Width - (int)Math.Round(14 * scale), b.ButtonBox.Right, "button at the right");
                            Eq(b.ButtonBox.Bottom + (int)Math.Round(14 * scale), b.Size.Height, "inset under the button");
                            MI.BitmapSource img2 = Render(b, ToastLook.None, mode == 2);
                            SavePng(img2, "toast-" + theme + "-" + kind.ToString().ToLowerInvariant() + "-long-button-" + (int)(scale * 100) + ".png");
                            // Nothing drawn between the third line and the button: no fourth line.
                            int gapTop = (int)Math.Ceiling(b.TextTop + 3 * b.LineHeight) + 1, gapBottom = b.ButtonBox.Top - 2;
                            string where;
                            if (gapBottom > gapTop)
                                Check(Plain(img2, pad, D.Rectangle.FromLTRB((int)b.TextLeft, gapTop, b.ButtonBox.Left - 2, gapBottom), bg, out where), "a fourth line under the third: " + where);
                            // The same long text without a button: still three lines, nothing below them.
                            ToastLook c = ToastLook.For(new Notice(shortN.Title, LongText, kind), scale);
                            Eq(3, c.Lines, "three lines without a button");
                            Check(c.Size.Height < b.Size.Height, "the button adds height");
                            Eq((int)Math.Ceiling(c.TextTop + 3 * c.LineHeight + 14 * scale), c.Size.Height, "height of three lines");
                            if (scale == 1f && kind == NoticeKind.Warning)
                            {
                                // Hovered: the card lightens or darkens a little, the button and x light up.
                                MI.BitmapSource over = Render(b, ToastLook.Body, mode == 2);
                                SavePng(over, "toast-" + theme + "-warning-hover.png");
                                Check(!Near(Px(over, pad + b.Size.Width / 2, pad + 3), bg, 2), "no hover feedback");
                                SavePng(Render(b, ToastLook.Button, mode == 2), "toast-" + theme + "-warning-hover-button.png");
                                SavePng(Render(b, ToastLook.Close, mode == 2), "toast-" + theme + "-warning-hover-close.png");
                            }
                        }
                    }
                    // A very long title: one line, cut with an ellipsis, clear of the x.
                    ToastLook t = ToastLook.For(new Notice("Un t\u00EDtulo much\u00EDsimo m\u00E1s largo de lo que cabe en una sola l\u00EDnea del aviso", "Texto.", NoticeKind.Info), 1f);
                    MI.BitmapSource ti = Render(t, ToastLook.None, mode == 2);
                    SavePng(ti, "toast-" + theme + "-long-title.png");
                    string w;
                    Check(Plain(ti, 30, D.Rectangle.FromLTRB(t.CloseBox.Left - 3, t.CloseBox.Bottom + 1, t.Size.Width - 3, (int)t.TextTop - 1), bg, out w), "the title runs under the x: " + w);
                    // No text at all.
                    ToastLook only = ToastLook.For(new Notice("Hecho", "", NoticeKind.Success), 1f);
                    Eq(0, only.Lines, "no lines");
                    Eq(14 + 32 + 14, only.Size.Height, "title only: the icon sets the height");
                    SavePng(Render(only, ToastLook.None, mode == 2), "toast-" + theme + "-title-only.png");
                }
                Console.WriteLine("    PNG in " + ShotsDir());
            }
            finally { Ds.Apply(dark ? 2 : 1); Ds.Apply(0); }
        }

        static void LookHits()
        {
            Notice n = WithAction(new Notice("No hay ning\u00FAn XML", "No hay ning\u00FAn XML en C:\\Empresa\\Mensajes\\SOAP\\EntradaConUnNombreDeCarpetaMuyLargoSinEspaciosQueNoCabeEnUnaLinea\\2026\\10\\12.", NoticeKind.Info));
            foreach (float scale in new float[] { 1f, 1.25f, 1.5f, 2f })
            {
                ToastLook k = ToastLook.For(n, scale);
                D.Point closeC = new D.Point(k.CloseBox.X + k.CloseBox.Width / 2, k.CloseBox.Y + k.CloseBox.Height / 2);
                D.Point buttonC = new D.Point(k.ButtonBox.X + k.ButtonBox.Width / 2, k.ButtonBox.Y + k.ButtonBox.Height / 2);
                Eq(ToastLook.Close, k.HitTest(closeC), "x at " + scale);
                Eq(ToastLook.Button, k.HitTest(buttonC), "button at " + scale);
                Eq(ToastLook.Body, k.HitTest(new D.Point(k.Size.Width / 3, k.Size.Height / 2)), "body at " + scale);
                Eq(ToastLook.Body, k.HitTest(new D.Point(1, 1)), "corner of the body at " + scale);
                Eq(ToastLook.None, k.HitTest(new D.Point(-1, 5)), "left of it at " + scale);
                Eq(ToastLook.None, k.HitTest(new D.Point(5, k.Size.Height)), "under it at " + scale);
                Check(k.CloseBox.Width >= 20 * scale, "x too small to hit");
                Check(k.Lines <= 3, "lines " + k.Lines);
                // The long path wraps inside the card: nothing drawn in the right inset beside the text.
                bool dark = Ds.Dark;
                MI.BitmapSource img = Render(k, ToastLook.None, dark);
                if (scale == 1f) SavePng(img, "toast-long-path.png");
                int pad = (int)Math.Round(30 * scale);
                M.Color bg = dark ? Ds.Rgb(42, 42, 45) : Ds.Rgb(251, 251, 252);
                string where;
                D.Rectangle inset = D.Rectangle.FromLTRB(k.Size.Width - (int)Math.Round(14 * scale) + 2, (int)k.TextTop, k.Size.Width - 3, (int)(k.TextTop + k.Lines * k.LineHeight));
                Check(Plain(img, pad, inset, bg, out where), "text past the right inset at " + scale + ": " + where);
            }
        }

        static void LookPaths()
        {
            // A long path that fits in three lines is shown whole, wrapped at its separators.
            string dir = "C:\\Empresa\\Mensajes\\SOAP\\EntradaDeLaMutuaColaboradora\\2026\\10\\12\\Lote0001";
            foreach (float scale in new float[] { 1f, 1.5f })
            {
                ToastLook k = ToastLook.For(new Notice("Stackshot", "No hay ning\u00FAn XML en " + dir + ".", NoticeKind.Info), scale);
                Check(!k.Clipped, "the path is cut at " + scale);
                Check(k.Lines >= 2 && k.Lines <= 3, "lines at " + scale + ": " + k.Lines);
                if (scale == 1f) SavePng(Render(k, ToastLook.None, Ds.Dark), "toast-path-wrapped.png");
            }
            // A word longer than a line has no place to wrap: it is cut there, and that counts as clipped.
            ToastLook w = ToastLook.For(new Notice("Stackshot", "Archivo: EntradaConUnNombreMuyLargoSinEspaciosNiSeparadoresQueNoCabeEnUnaLinea.xml", NoticeKind.Info), 1f);
            SavePng(Render(w, ToastLook.None, Ds.Dark), "toast-long-word.png");
            Check(w.Clipped, "a long word cut is not counted as clipped");
            Check(w.Lines >= 1 && w.Lines <= 3, "lines of a long word: " + w.Lines);
            Eq((int)Math.Ceiling(Math.Max(w.TextTop + w.Lines * w.LineHeight, 14 + 32) + 14), w.Size.Height, "height follows the lines drawn");
        }

        // ---- The window, parked far off screen

        sealed class Parked : IDisposable
        {
            public readonly ToastStack Stack = new ToastStack();
            public readonly List<ToastWindow> Windows = new List<ToastWindow>();
            public bool Animate;
            public Parked(bool animate)
            {
                Animate = animate;
                Stack.Area = delegate(string d) { return MakeArea(R(-32000, -32000, 1920, 1080), R(-32000, -32000, 1920, 1032), 1f, false); };
                Stack.Quiet = delegate(D.Rectangle m) { return null; };
                Stack.Animate = delegate { return Animate; };
                Stack.Post = delegate(Action a) { a(); };
                Stack.Wake = delegate(double ms) { };
                Stack.Occupied = delegate { return new List<D.Rectangle>(); };
                Stack.MakeView = delegate(ToastStack.Entry e) { ToastWindow w = new ToastWindow(Stack, e); Windows.Add(w); return w; };
            }
            public void Dispose()
            {
                Stack.CloseAll();
                foreach (ToastWindow w in Windows) if (!w.IsDisposed) w.Dispose();
                Pump(null, 50);
            }
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr h, out uint affinity);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

        static void Pump(Func<bool> done, int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                Application.DoEvents();
                if (done != null && done()) return;
                Thread.Sleep(5);
            }
        }

        static D.Rectangle WindowRect(IntPtr h)
        {
            Native.RECT r;
            Native.GetWindowRect(h, out r);
            return D.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        static void OffScreen(IntPtr h)
        {
            D.Rectangle r = WindowRect(h);
            foreach (Screen s in Screen.AllScreens)
                if (s.Bounds.IntersectsWith(r)) { Native.ShowWindow(h, 0); throw new InvalidOperationException("the toast is on a screen: " + r); }
        }

        static void WindowStyles()
        {
            using (Parked p = new Parked(false))
            {
                IntPtr fg = Native.GetForegroundWindow(), active = GetActiveWindow();
                p.Stack.Show(new Notice("Prueba de estilos", "Aparcado fuera de la pantalla.", NoticeKind.Info));
                Pump(null, 30);
                ToastWindow w = p.Windows[0];
                IntPtr h = w.Handle;
                OffScreen(h);
                Check(w.Visible && Native.IsWindowVisible(h), "not shown (parked)");
                int ex = GetWindowLong(h, -20);
                Check((ex & 0x08000000) != 0, "no WS_EX_NOACTIVATE: " + ex.ToString("X"));
                Check((ex & 0x00000080) != 0, "no WS_EX_TOOLWINDOW: " + ex.ToString("X"));
                Check((ex & 0x00000008) != 0, "not topmost: " + ex.ToString("X"));
                Check((ex & 0x00080000) != 0, "not layered: " + ex.ToString("X"));
                Check((ex & 0x00040000) == 0, "WS_EX_APPWINDOW (taskbar, Alt+Tab): " + ex.ToString("X"));
                Check(!w.ShowInTaskbar, "in the taskbar");
                Check((bool)typeof(Form).GetProperty("ShowWithoutActivation", Priv).GetValue(w, null), "shows activated");
                Eq((IntPtr)3, Native.SendMessage(h, 0x0021, h, (IntPtr)0x02010001), "WM_MOUSEACTIVATE answer (MA_NOACTIVATE)");
                Check(Native.GetForegroundWindow() != h, "it took the foreground");
                if (Native.GetForegroundWindow() != fg) Console.WriteLine("    (the foreground changed meanwhile, not to the toast)");
                Check(GetActiveWindow() != h, "it became the active window");
                Check(GetActiveWindow() == active, "the active window changed");
                uint aff;
                if (GetWindowDisplayAffinity(h, out aff)) Eq(0x11u, aff, "capture exclusion (WDA_EXCLUDEFROMCAPTURE)");
                else Console.WriteLine("    (display affinity not readable here)");
                // Where the stack put it: the body at the corner of the parked work area, the shadow around it.
                ToastStack.Entry e = p.Stack.Shown[0];
                D.Rectangle win = WindowRect(h), body = e.Slot;
                Eq(R(-32000 + 1920 - 16 - 360, -32000 + 1032 - 16 - body.Height, 360, body.Height), body, "slot");
                Check(win.Contains(body) && win.Width == body.Width + 60 && win.Height == body.Height + 60, "window around the body: " + win + " for " + body);
                Check(Application.OpenForms.Count > 0, "forms");
            }
        }

        static void WindowAccess()
        {
            using (Parked p = new Parked(false))
            {
                Notice n = new Notice("No se pudo guardar", "El disco est\u00E1 lleno.\nLibera espacio.", NoticeKind.Error);
                p.Stack.Show(n);
                Pump(null, 30);
                ToastWindow w = p.Windows[0];
                OffScreen(w.Handle);
                Eq(n.Title, w.AccessibleName, "accessible name");
                object prov = typeof(ToastWindow).GetField("access", Priv).GetValue(w);
                Check(prov != null, "no UI Automation provider");
                ToastUia.IRawElementProviderSimple ps = (ToastUia.IRawElementProviderSimple)prov;
                Eq(n.Title, ps.GetPropertyValue(ToastUia.NameProperty), "UIA name");
                Eq("El disco est\u00E1 lleno. Libera espacio.", ps.GetPropertyValue(ToastUia.FullDescriptionProperty), "UIA description");
                Eq(2, ps.GetPropertyValue(ToastUia.LiveSettingProperty), "assertive for an error");
                Eq(ToastUia.PaneControlType, ps.GetPropertyValue(ToastUia.ControlTypeProperty), "pane");
                Eq(false, ps.GetPropertyValue(ToastUia.IsKeyboardFocusableProperty), "not focusable");
                Check(ps.GetPropertyValue(30000) == null, "unknown property");
                Check(ps.HostRawElementProvider != null, "no host provider for the window");
                Eq(0x22, ps.ProviderOptions, "server side, COM threading");
                Eq(n.Spoken, ToastUia.LastSpoken, "announced as it appeared");
                Check(ToastUia.LastResult == 0 || ToastUia.LastResult == 1, "announcement failed: 0x" + ToastUia.LastResult.ToString("X"));
                Console.WriteLine("    announcement: \"" + ToastUia.LastSpoken + "\" (" + (ToastUia.LastResult == 0 ? "raised" : "nobody listening") + ")");
                // The same notice again: announced again, and an info one is polite.
                Notice again = new Notice("No se pudo guardar", "El disco est\u00E1 lleno.\nLibera espacio.", NoticeKind.Info);
                ToastUia.LastSpoken = null;
                p.Stack.Show(again);
                Eq(again.Spoken, ToastUia.LastSpoken, "announced again on refresh");
                Eq(1, ps.GetPropertyValue(ToastUia.LiveSettingProperty), "polite for information");
                Eq(1, p.Windows.Count, "refreshed in place");
            }
        }

        static void Mouse(ToastWindow w, string method, MouseButtons b, D.Point client)
        {
            typeof(ToastWindow).GetMethod(method, Priv).Invoke(w, new object[] { new MouseEventArgs(b, 1, client.X, client.Y, 0) });
        }

        static D.Point ClientOf(ToastWindow w, D.Rectangle part)
        {
            return new D.Point(30 + part.X + part.Width / 2, 30 + part.Y + part.Height / 2);
        }

        static void Arm(ToastWindow w)
        {
            typeof(ToastWindow).GetField("armed", Priv).SetValue(w, Environment.TickCount - 1);
        }

        static void WindowClicks()
        {
            using (Parked p = new Parked(false))
            {
                int ran = 0;
                Notice a = new Notice("Algunos atajos est\u00E1n ocupados", "Ctrl + May\u00FAs + Alt + F10 los usa otro programa.", NoticeKind.Warning);
                a.ActionLabel = "Elegir otros atajos";
                a.Action = delegate { ran++; };
                p.Stack.Show(a);
                Pump(null, 30);
                ToastWindow w = p.Windows[0];
                OffScreen(w.Handle);
                ToastStack.Entry e = p.Stack.Shown[0];
                ToastLook look = w.Look;
                // A pointer resting where the toast appeared (Windows reports a move then) does not hold it.
                D.Point rest = ClientOf(w, new D.Rectangle(80, 20, 10, 10));
                typeof(ToastWindow).GetField("restAt", Priv).SetValue(w, w.PointToScreen(rest));
                Mouse(w, "OnMouseMove", MouseButtons.None, rest);
                Check(!e.Hovered, "a resting pointer holds it");
                // Hover holds it; moving onto the shadow lets it go.
                Mouse(w, "OnMouseMove", MouseButtons.None, ClientOf(w, new D.Rectangle(60, 20, 10, 10)));
                Check(e.Hovered, "hover not seen");
                Mouse(w, "OnMouseMove", MouseButtons.None, new D.Point(3, 3));
                Check(!e.Hovered, "hover kept over the shadow");
                // A click right as it appears is ignored (it was meant for what was there).
                typeof(ToastWindow).GetField("armed", Priv).SetValue(w, Environment.TickCount + 10000);
                Mouse(w, "OnMouseDown", MouseButtons.Left, ClientOf(w, look.ButtonBox));
                Mouse(w, "OnMouseUp", MouseButtons.Left, ClientOf(w, look.ButtonBox));
                Eq(1, p.Stack.Shown.Count, "an early click closed it");
                Eq(0, ran, "an early click ran the action");
                // The button: action, then closed.
                Arm(w);
                Mouse(w, "OnMouseDown", MouseButtons.Left, ClientOf(w, look.ButtonBox));
                Mouse(w, "OnMouseUp", MouseButtons.Left, ClientOf(w, look.ButtonBox));
                Eq(1, ran, "button ran the action");
                Eq(0, p.Stack.Shown.Count, "button closed it");
                Check(w.IsDisposed, "window not closed");

                // x: closed, no action.
                p.Stack.Show(a);
                Pump(null, 30);
                ToastWindow w2 = p.Windows[1];
                Arm(w2);
                Mouse(w2, "OnMouseDown", MouseButtons.Left, ClientOf(w2, w2.Look.CloseBox));
                Mouse(w2, "OnMouseUp", MouseButtons.Left, ClientOf(w2, w2.Look.CloseBox));
                Eq(1, ran, "x ran the action");
                Check(w2.IsDisposed && p.Stack.Shown.Count == 0, "x did not close it");

                // The body: action and closed; right click: closed only; a press that started elsewhere: nothing.
                p.Stack.Show(a);
                Pump(null, 30);
                ToastWindow w3 = p.Windows[2];
                Arm(w3);
                Mouse(w3, "OnMouseUp", MouseButtons.Left, ClientOf(w3, new D.Rectangle(60, 20, 10, 10)));
                Eq(1, ran, "a release without a press acted");
                Mouse(w3, "OnMouseDown", MouseButtons.Left, ClientOf(w3, new D.Rectangle(60, 20, 10, 10)));
                Mouse(w3, "OnMouseUp", MouseButtons.Left, ClientOf(w3, new D.Rectangle(60, 20, 10, 10)));
                Eq(2, ran, "body ran the action");
                Check(w3.IsDisposed, "body did not close it");
                p.Stack.Show(a);
                Pump(null, 30);
                ToastWindow w4 = p.Windows[3];
                Arm(w4);
                Mouse(w4, "OnMouseDown", MouseButtons.Right, ClientOf(w4, new D.Rectangle(60, 20, 10, 10)));
                Mouse(w4, "OnMouseUp", MouseButtons.Right, ClientOf(w4, new D.Rectangle(60, 20, 10, 10)));
                Eq(2, ran, "right click ran the action");
                Check(w4.IsDisposed, "right click did not close it");
            }
        }

        static void WindowAnimated()
        {
            using (Parked p = new Parked(true))
            {
                p.Stack.Show(new Notice("Uno", "Entra deslizando.", NoticeKind.Success));
                ToastWindow w = p.Windows[0];
                OffScreen(w.Handle);
                ToastStack.Entry e = p.Stack.Shown[0];
                D.Rectangle want = e.Slot;
                want.Inflate(30, 30);
                D.Rectangle start = WindowRect(w.Handle);
                Check(start.X > want.X, "does not start to the right: " + start + " vs " + want);
                Pump(delegate { return WindowRect(w.Handle) == want; }, 1500);
                Eq(want, WindowRect(w.Handle), "slid into place");
                // A second one: the first glides up to make room.
                p.Stack.Show(new Notice("Dos", "Empuja al primero.", NoticeKind.Info));
                D.Rectangle up = p.Stack.Shown[0].Slot;
                up.Inflate(30, 30);
                Check(up.Y < want.Y, "the first is not moved up");
                Pump(delegate { return WindowRect(w.Handle) == up; }, 1500);
                Eq(up, WindowRect(w.Handle), "glided up");
                OffScreen(p.Windows[1].Handle);
                // Theme switch: repainted without errors, still there.
                bool dark = Ds.Dark;
                Ds.Apply(dark ? 1 : 2);
                Pump(null, 50);
                Ds.Apply(dark ? 2 : 1);
                Ds.Apply(0);
                Pump(null, 50);
                Check(!w.IsDisposed, "closed by a theme switch");
                // Leaving: fades out and closes.
                p.Stack.Click(p.Stack.Shown[0], true);
                Check(!w.IsDisposed, "closed at once though animated");
                Pump(delegate { return w.IsDisposed; }, 1500);
                Check(w.IsDisposed, "not closed after its exit");
            }
        }
    }
}
