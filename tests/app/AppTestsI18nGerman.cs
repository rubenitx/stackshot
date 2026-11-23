// Suite "i18n": the German mirror of the English audit — the same surfaces with German applied, flagging anything that
// still reads Spanish or English — plus the windows the page audit does not reach, audited in English and German.
// Windows are built and laid out, never shown.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace Stackshot.Tests
{
    static class AppTestsI18nGerman
    {
        const BindingFlags NP = AppTestsI18n.NP;

        // English function words and UI words that read as leftovers in the German interface. Everyday German words that
        // happen to be English (articles, "fast" = almost) are stripped first, and so are the nouns both languages share
        // and the anglicisms German uses on its own ("Screenshot", "Update").
        static readonly Regex EnglishWords = new Regex(@"\b(the|and|of|to|with|your|you|is|are|open|save|close|settings|capture|captures|record|recording|shortcut|shortcuts|folder|file|files|show|hide|copy|paste|delete|cancel|choose|select|edit|undo|redo|search|remove|retry|clear|screen|window|windows|history|page|pages|click|image|text|size|color|style|quality|menu|button|from|for|not|can|when|where|what|this|that|here|there|now|new|more|than|then|them|they|its|on|off|at|by|or|if|out|up|down|back|next|previous|first|last|name|names|value|values|title|count|number|time|date|today|yesterday|week|month|year|version|language|appearance|dark|light|theme|keyboard|mouse|print|share|browse|apply|reset|default|defaults|warning|error|done|ready|please|wait|minute|minutes|second|seconds|hour|hours|day|days|home|about|help|tip|tips|latest|recent|saved|saving|change|changed|free|install|installed|available|enabled|disabled)\b", RegexOptions.IgnoreCase);
        static readonly string[] SharedNouns = { "Editor", "Timer", "Video", "Videos", "Standard", "Pause", "Position", "Name", "Names", "Start", "Status", "Format", "Tipp", "Test", "Monitor", "Moment", "Layer", "Sort", "Plan", "Marker", "Zoom", "Highlight", "Screenshot", "Screenshots", "Download", "Downloads", "Upload", "Update", "Updates", "Layout", "Player", "Stop", "Printer", "Scanner", "Onboarding", "Windows", "Version", "Text", "Design", "Speaker", "Favorit", "Filter", "Export", "Import", "Sortier", "Spool", "Minute", "Minuten" };
        // Proper names the mascot may quote: a series title, not a sentence to translate.
        static readonly string[] ProperNames = { "Adventure Time" };
        static readonly Regex SharedFunctionWords = new Regex(@"\b(die|hat|fast|all|so|war|will|man|an|in|was|bis|most)\b", RegexOptions.IgnoreCase);
        static readonly Regex NameTemplateTokens = new Regex(@"\{[^}]*\}");

        // What no German page may show: Spanish that did not get translated, or English that fell back to its key.
        // Lines from the audio-device list start with the localized "Windows default" prefix: Windows names its devices
        // in the operating system's own language, which no catalog entry can change (the English audit exempts the
        // English prefix for the same reason).
        internal static string WhyBad(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (s.StartsWith("Windows default: ", StringComparison.Ordinal)) return null;
            if (s.StartsWith(I18n.Fmt("El de Windows: {0}", "Windows default: {0}", "").TrimEnd(), StringComparison.Ordinal)) return null;
            if (AppTestsI18n.LooksSpanish(s)) return "es";
            foreach (string a in AppTestsI18n.Allowed) s = s.Replace(a, "");
            foreach (string p in ProperNames) s = s.Replace(p, "");
            foreach (string w in SharedNouns) s = s.Replace(w, "");
            s = NameTemplateTokens.Replace(s, " ");   // {date}, {window}…: the tokens the file-name templates keep in English
            s = SharedFunctionWords.Replace(s, " ");
            Match m = EnglishWords.Match(s);
            return m.Success ? "en:" + m.Value : null;
        }

        internal static bool Bad(string s) { return WhyBad(s) != null; }

        // The same surfaces the English audit walks, in German.
        public static void GermanAudit()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
                I18n.Apply("auto");
                Check(I18n.IsGerman, "German Windows in Automatic");
                List<string> found = new List<string>();
                foreach (int appearance in new int[] { 1, 2 })
                {
                    HomeWindow hw = AppTestsI18n.Make(appearance);
                    try
                    {
                        foreach (string page in AppTestsI18n.Pages)
                        {
                            AppTestsI18n.SetPage(hw, page);
                            AppTestsI18n.Layout(hw);
                            foreach (string s in AppTestsI18n.Strings(hw.Content))
                            {
                                string why = WhyBad(s);
                                if (why != null) AppTestsI18n.Add(found, page + " [" + why + "]: " + s.Replace("\n", " \\n "));
                            }
                        }
                    }
                    finally { hw.Shutdown(); }
                }
                AppTestsI18n.Other(found, "markdown", 40, AppTestsI18n.AuditMarkdown);
                AppTestsI18n.Other(found, "editor", 60, AppTestsI18n.AuditEditor);
                AppTestsI18n.Other(found, "tray", 20, AppTestsI18n.AuditTray);
                AppTestsI18n.Other(found, "card", 6, AppTestsI18n.AuditCard);
                foreach (string f in found) Console.WriteLine("    german: " + f);
                Check(found.Count == 0, found.Count + " Spanish or English texts in the German interface (listed above)");
            }
            finally { I18n.Apply("es"); }
        }

        // ReportWindow, NewsWindow, LinksWindow, QuickWindow (note and reminder), the StickyNote menu, the player, the
        // region picker's hint, RecordSetup, Welcome, UpdateNotice, the pet menu and a sample of toasts: built without
        // showing them, then audited in English (nothing Spanish) and German (nothing Spanish or English).
        public static void ExtraWindowsAudit()
        {
            List<string> found = new List<string>();
            try
            {
                AuditAll(found, "en", delegate(string s) { return AppTestsI18n.LooksSpanish(s) ? "es" : null; });
                AuditAll(found, "de", WhyBad);
                foreach (string f in found) Console.WriteLine("    extra: " + f);
                Check(found.Count == 0, found.Count + " unlocalized texts in the other windows (listed above)");
            }
            finally { I18n.Apply("es"); }
        }

        static void AuditAll(List<string> found, string lang, Func<string, string> flag)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang == "de" ? "de-DE" : "en-US");
            I18n.Apply("auto");
            AuditInto(found, lang + "/report", 8, flag, ReportTexts);
            AuditInto(found, lang + "/news", 5, flag, NewsTexts);
            AuditInto(found, lang + "/links", 3, flag, LinksTexts);
            AuditInto(found, lang + "/quick-note", 3, flag, QuickNoteTexts);
            AuditInto(found, lang + "/quick-reminder", 3, flag, QuickReminderTexts);
            AuditInto(found, lang + "/welcome", 10, flag, WelcomeTexts);
            AuditInto(found, lang + "/player", 3, flag, PlayerTexts);
            AuditInto(found, lang + "/region-picker", 2, flag, RegionPickerTexts);
            AuditInto(found, lang + "/record-setup", 8, flag, RecordSetupTexts);
            AuditInto(found, lang + "/sticky-note", 2, flag, StickyTexts);
            AuditInto(found, lang + "/pet", 2, flag, PetTexts);
            AuditInto(found, lang + "/update-notice", 2, flag, UpdateNoticeTexts);
            AuditInto(found, lang + "/toasts", 2, flag, ToastTexts);
        }

        static void AuditInto(List<string> found, string where, int least, Func<string, string> flag, Func<List<string>> texts)
        {
            List<string> all;
            try { all = texts(); }
            catch (Exception ex) { AppTestsI18n.Add(found, where + ": could not be built (" + ex.GetType().Name + ": " + ex.Message + ")"); return; }
            Console.WriteLine("    " + where + ": " + all.Count + " texts read");
            if (all.Count < least) AppTestsI18n.Add(found, where + ": only " + all.Count + " texts could be read");
            foreach (string s in all) { string why = flag(s); if (why != null) AppTestsI18n.Add(found, where + " [" + why + "]: " + s.Replace("\n", " \\n ")); }
        }

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        // Whatever a window lets a user read or hear, whether Stackshot draws it with WPF or with WinForms.
        static List<string> Extract(object w)
        {
            Control f = w as Control;
            if (f != null)
            {
                List<string> acc = AppTestsI18n.Accessible(f);
                acc.Add(f.Text);
                return acc;
            }
            Window win = w as Window;
            if (win != null)
            {
                List<string> acc = AppTestsI18n.Strings(win.Content);
                acc.Add(win.Title);
                return acc;
            }
            throw new InvalidOperationException("neither Form nor Window");
        }

        static void CloseW(object w)
        {
            Control f = w as Control;
            if (f != null) { try { f.Dispose(); } catch { } return; }
            try { ((Window)w).Close(); } catch { }
        }

        static ShotStack Owner()
        {
            return (ShotStack)FormatterServices.GetUninitializedObject(typeof(ShotStack));
        }

        static List<string> ReportTexts()
        {
            ReportWindow w = (ReportWindow)typeof(ReportWindow).GetConstructor(NP, null, Type.EmptyTypes, null).Invoke(null);
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> NewsTexts()
        {
            NewsWindow w = (NewsWindow)typeof(NewsWindow).GetConstructor(NP, null, new[] { typeof(bool), typeof(string) }, null).Invoke(new object[] { true, "2.2.1" });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> LinksTexts()
        {
            LinksWindow w = (LinksWindow)typeof(LinksWindow).GetConstructor(NP, null, new[] { typeof(ShotStack) }, null).Invoke(new object[] { Owner() });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static ShotStack OwnerWithReminders()
        {
            ShotStack owner = Owner();
            FieldInfo f = typeof(ShotStack).GetField("reminders", NP);   // uninitialized owner: the list is still null
            if (f != null && f.GetValue(owner) == null)
            {
                Type lt = typeof(List<>).MakeGenericType(f.FieldType.GetGenericArguments()[0]);
                f.SetValue(owner, Activator.CreateInstance(lt));
            }
            return owner;
        }

        static List<string> QuickWindowTexts(bool note)
        {
            QuickWindow w = (QuickWindow)typeof(QuickWindow).GetConstructor(NP, null, new[] { typeof(ShotStack), typeof(bool) }, null).Invoke(new object[] { note ? Owner() : OwnerWithReminders(), note });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> QuickNoteTexts() { return QuickWindowTexts(true); }
        static List<string> QuickReminderTexts() { return QuickWindowTexts(false); }

        static List<string> WelcomeTexts()
        {
            SetupWindow w = (SetupWindow)typeof(SetupWindow).GetConstructor(NP, null, new[] { typeof(Settings) }, null).Invoke(new object[] { new Settings() });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> PlayerTexts()
        {
            string png = Path.Combine(Path.GetTempPath(), "stackshot-app-tests", "i18n-player.png");
            Directory.CreateDirectory(Path.GetDirectoryName(png));
            if (!File.Exists(png)) using (Bitmap b = new Bitmap(320, 200)) b.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            Func<ISoundOut> noSound = delegate { return null; };
            PlayerWindow w = (PlayerWindow)typeof(PlayerWindow).GetConstructor(NP, null,
                new[] { typeof(ShotStack), typeof(Settings), typeof(string), typeof(string), typeof(Func<ISoundOut>) }, null)
                .Invoke(new object[] { Owner(), new Settings(), "ffmpeg", png, noSound });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> RegionPickerTexts()
        {
            foreach (ConstructorInfo ci in typeof(RegionPicker).GetConstructors(NP))
            {
                ParameterInfo[] ps = ci.GetParameters();
                if (ps.Length != 4 || ps[0].ParameterType != typeof(Dib)) continue;
                object[] args = new object[4];
                for (int i = 0; i < 4; i++)
                {
                    Type t = ps[i].ParameterType;
                    if (t == typeof(Dib)) args[i] = new Dib(80, 60);
                    else if (t == typeof(Rectangle)) args[i] = new Rectangle(0, 0, 80, 60);
                    else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) args[i] = Activator.CreateInstance(t);
                    else if (t.IsEnum) args[i] = Enum.ToObject(t, 0);
                    else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                RegionPicker w = (RegionPicker)ci.Invoke(args);
                try { return Extract(w); } finally { CloseW(w); }
            }
            throw new InvalidOperationException("no 4-arg constructor with a Dib");
        }

        static List<string> RecordSetupTexts()
        {
            RecordSetup w = (RecordSetup)typeof(RecordSetup).GetConstructor(NP, null, new[] { typeof(Settings), typeof(Rectangle), typeof(bool) }, null)
                .Invoke(new object[] { new Settings(), new Rectangle(0, 0, 800, 600), false });
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> StickyTexts()
        {
            ShotStack.Note n = new ShotStack.Note { Line = "Prueba", Key = "prueba", When = "2025-01-01 10:00", Text = "Prueba de nota" };
            StickyNote w = new StickyNote(Owner(), n, Screen.AllScreens[0], new System.Drawing.Point(0, 0), 0, false, false);
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> PetTexts()
        {
            PetWindow w = new PetWindow(Owner(), new Settings());
            try { return Extract(w); } finally { CloseW(w); }
        }

        static List<string> UpdateNoticeTexts()
        {
            Updater.Release r = new Updater.Release { Version = new Version("2.3.0"), Tag = "v2.3.0", Page = "https://example.com", ExeUrl = "https://example.com/e.exe", Sha256 = "x" };
            UpdateNotice w = new UpdateNotice(Owner(), r);
            try { return Extract(w); } finally { CloseW(w); }
        }

        // A sample of notices as the app raises them, laid out the way the toast stack would show them.
        static List<string> ToastTexts()
        {
            string[][] sample =
            {
                new[] { "No se pudo guardar", "Could not save the recording.", "Error" },
                new[] { "Texto copiado", "Text copied to the clipboard.", "Success" },
                new[] { "La grabaci\u00F3n ha ido a tirones", "Recording was choppy.", "Warning" },
                new[] { "Algunos atajos est\u00E1n ocupados", "Some shortcuts are occupied.", "Warning" }
            };
            ToastStack stack = new ToastStack();
            List<ToastWindow> windows = new List<ToastWindow>();
            try
            {
                List<string> acc = new List<string>();
                ConstructorInfo entry = typeof(ToastStack.Entry).GetConstructor(NP, null, new[] { typeof(Notice) }, null);
                foreach (string[] s in sample)
                {
                    Notice n = new Notice(I18n.T(s[0], s[1]), "", (NoticeKind)Enum.Parse(typeof(NoticeKind), s[2]));
                    ToastWindow w = new ToastWindow(stack, (ToastStack.Entry)entry.Invoke(new object[] { n }));
                    windows.Add(w);
                    acc.Add(n.Title);
                    acc.Add(n.Text);
                }
                if (acc.Count == 0) throw new InvalidOperationException("no toasts built");
                return acc;
            }
            finally { foreach (ToastWindow w in windows) { try { w.Dispose(); } catch { } } }
        }
    }
}
