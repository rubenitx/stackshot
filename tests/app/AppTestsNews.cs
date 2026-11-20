// What users are told about each version, suite "news": the news file is well formed and read whole, a release's
// Spanish news are found in its notes, and after an update each user sees everything new since the version they had.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Stackshot.Tests
{
    static class AppTestsNews
    {
        const string S = "news";

        static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        static string Here([System.Runtime.CompilerServices.CallerFilePath] string file = "") { return file; }

        static string Sample()
        {
            return "# Novedades\n\nIntro.\n\n## 2.2.1\n\n- **Uno.** Primero.\n- **Dos.** Segundo.\n\n## 2.2.0\n\n- **Tres.** Tercero.\n- **Cuatro.** Cuarto.\n- **Cinco.** Quinto.\n\n## 2.1.9\n\n- **Viejo.** No sale nunca: no hay versiones antes de 2.2.0 con novedades.\n";
        }

        static string Keys(List<KeyValuePair<string, List<News.Item>>> groups)
        {
            List<string> k = new List<string>();
            foreach (KeyValuePair<string, List<News.Item>> g in groups) k.Add(g.Key + ":" + g.Value.Count);
            return string.Join(",", k.ToArray());
        }

        // Who updates from where, and what the window lists.
        static void Since()
        {
            News.UseForTests(Sample());
            try
            {
                Check(Keys(News.Since("2.2.0", "2.2.1")) == "2.2.1:2", "from 2.2.0: " + Keys(News.Since("2.2.0", "2.2.1")));
                Check(Keys(News.Since("2.1.9", "2.2.1")) == "2.2.1:2,2.2.0:3", "from 2.1.9: " + Keys(News.Since("2.1.9", "2.2.1")));
                Check(Keys(News.Since("", "2.2.1")) == "2.2.1:2,2.2.0:3,2.1.9:1", "from a version that kept no record: " + Keys(News.Since("", "2.2.1")));
                Check(Keys(News.Since("2.2.1", "2.2.1")) == "", "nothing new since the same version: " + Keys(News.Since("2.2.1", "2.2.1")));
                Check(Keys(News.Since("2.0.0", "2.2.0")) == "2.2.0:3,2.1.9:1", "never past the version running: " + Keys(News.Since("2.0.0", "2.2.0")));
                Check(Keys(News.Since("rubbish", "2.2.1")) == "2.2.1:2,2.2.0:3,2.1.9:1", "an unreadable record counts as none");
                Check(News.Of("2.2.0").Count == 3 && News.Of("2.2.0")[0].Title == "Tres." && News.Of("2.2.0")[0].Text == "Tercero.", "one version's news");
            }
            finally { News.UseForTests(null); }
        }

        // docs/novedades.md as it ships: every line of every version is a news item the app reads whole, the newest
        // version is the one the program says it is, and the release notes CI builds carry them between the markers.
        static void RealFile()
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()), @"..\..\docs\novedades.md"));
            Check(File.Exists(path), "no news file at " + path);
            string md = File.ReadAllText(path, Encoding.UTF8);
            News.UseForTests(md);
            try
            {
                MatchCollection versions = Regex.Matches(md, @"^## (\d+\.\d+\.\d+)\s*$", RegexOptions.Multiline);
                Check(versions.Count >= 1, "no version in the news file");
                Version prev = null;
                foreach (Match m in versions)
                {
                    string v = m.Groups[1].Value;
                    Version cur = new Version(v);
                    Check(prev == null || cur < prev, "versions not newest first at " + v);
                    prev = cur;
                    string body = Regex.Match(md, "^## " + Regex.Escape(v) + @"\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline).Groups[1].Value;
                    int bullets = 0;
                    foreach (string line in body.Replace("\r", "").Split('\n'))
                    {
                        if (line.Trim().Length == 0) continue;
                        Check(Regex.IsMatch(line, @"^- \*\*[^*]+\.\*\* \S"), v + ": not a news line (\"- **Title.** Text\"): " + line);
                        bullets++;
                    }
                    Check(News.Of(v).Count == bullets, v + ": the app reads " + News.Of(v).Count + " of its " + bullets + " news");
                }
                string newest;
                News.Newest(out newest);
                string program = typeof(News).Assembly.GetName().Version.ToString(3);
                Check(newest == program || new Version(newest) > new Version(program), "the news file's newest version " + newest + " is older than the program " + program);
                // The notes a release carries: what CI writes between the markers, read back as the update notice does.
                string first = versions[0].Groups[1].Value;
                StringBuilder notes = new StringBuilder("Notes in English.\n\n<details><summary>Novedades en espa\u00F1ol</summary>\n\n" + News.Start + "\n");
                foreach (News.Item it in News.Of(first)) notes.Append("- **").Append(it.Title).Append("** ").Append(it.Text).Append('\n');
                notes.Append(News.End + "\n\n</details>\n");
                Check(News.FromRelease(notes.ToString()).Count == News.Of(first).Count, "the update notice does not read the release's news back");
            }
            finally { News.UseForTests(null); }
        }

        // The window someone coming from 2.1 sees after updating: each version's news under its number, all of them.
        static void Window()
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()), @"..\..\docs\novedades.md"));
            News.UseForTests(File.ReadAllText(path, Encoding.UTF8));
            try
            {
                string program = typeof(News).Assembly.GetName().Version.ToString(3);
                List<KeyValuePair<string, List<News.Item>>> groups = News.Since("", program);
                int all = 0;
                foreach (KeyValuePair<string, List<News.Item>> g in groups) all += g.Value.Count;
                System.Reflection.ConstructorInfo ctor = typeof(NewsWindow).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                                                                                         new Type[] { typeof(bool), typeof(string) }, null);
                for (int theme = 1; theme <= 2; theme++)
                {
                    Ds.Apply(theme);
                    NewsWindow w = (NewsWindow)ctor.Invoke(new object[] { false, "" });
                    try
                    {
                        System.Windows.UIElement content = (System.Windows.UIElement)w.Content;
                        content.Measure(new System.Windows.Size(540, 900));
                        content.Arrange(new System.Windows.Rect(0, 0, 540, 900));
                        content.UpdateLayout();
                        List<string> texts = new List<string>();
                        Collect(content, texts);
                        if (groups.Count > 1)
                            foreach (KeyValuePair<string, List<News.Item>> g in groups)
                                Check(texts.Contains("En la " + g.Key), "no heading for " + g.Key + " in the window");
                        int shown = 0;
                        foreach (KeyValuePair<string, List<News.Item>> g in groups)
                            foreach (News.Item it in g.Value) if (texts.Contains(it.Title)) shown++;
                        Check(shown == all, "the window shows " + shown + " of the " + all + " news since 2.1");
                        System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(540, 900, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        System.Windows.Media.DrawingVisual dv = new System.Windows.Media.DrawingVisual();
                        using (System.Windows.Media.DrawingContext dc = dv.RenderOpen()) dc.DrawRectangle(Ds.Brush(Ds.Brushes.Window), null, new System.Windows.Rect(0, 0, 540, 900));
                        rtb.Render(dv);
                        rtb.Render(content);
                        System.Windows.Media.Imaging.PngBitmapEncoder enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                        string dir = Path.Combine(Path.GetTempPath(), "news");
                        Directory.CreateDirectory(dir);
                        using (FileStream fs = File.Create(Path.Combine(dir, "since-2.1-" + (theme == 1 ? "light" : "dark") + ".png"))) enc.Save(fs);
                    }
                    finally { w.Close(); }
                }
                Ds.Apply(0);
            }
            finally { News.UseForTests(null); }
        }

        static void Collect(System.Windows.DependencyObject d, List<string> into)
        {
            System.Windows.Controls.TextBlock t = d as System.Windows.Controls.TextBlock;
            if (t != null && !string.IsNullOrEmpty(t.Text)) into.Add(t.Text);
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) Collect(System.Windows.Media.VisualTreeHelper.GetChild(d, i), into);
        }

        static void KoreanNotes()
        {
            string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()), @"..\..\docs"));
            string source = File.ReadAllText(Path.Combine(dir, "novedades.md"));
            string translated = File.ReadAllText(Path.Combine(dir, "novedades.ko.md"));
            string previous = I18n.Mode;
            try
            {
                News.UseForTests(source, translated);
                I18n.Apply("es");
                string version = typeof(News).Assembly.GetName().Version.ToString(3);
                string sourceVersions = Keys(News.Since("", version));
                I18n.Apply("ko");
                Check(Keys(News.Since("", version)) == sourceVersions, "bundled Korean notes cover the same versions and entries");
                Check(News.Of("2.2.1")[0].Title == "\uBC30\uACBD \uC2A4\uD0C0\uC77C \uC800\uC7A5.", "Korean release notes");
                I18n.Apply("en");
                Check(News.Of("2.2.1")[0].Title == "Estilos guardados.", "switching languages preserves the original release notes");
            }
            finally { News.UseForTests(null); I18n.Apply(previous); }
        }

        public static void Suite()
        {
            AppTests.Test(S, "Korean bundled release notes and language switching", KoreanNotes);
            AppTests.Test(S, "after an update each user sees everything new since the version they had", Since);
            AppTests.Test(S, "docs/novedades.md is well formed, read whole and matches the program's version", RealFile);
            AppTests.Test(S, "the news window after updating from 2.1 shows every version's news under its number", Window);
        }
    }
}
