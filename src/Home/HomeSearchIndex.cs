// Stackshot - Settings search, the index: what the settings pages register while they are built (each row and widget,
// its group and page), collected by building those pages off screen, and found by words in any order, without accents
// or case, with a few synonyms for the words people type.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    // One setting the search can find, as its page shows it.
    internal sealed class SearchEntry
    {
        public string Page, PageName, Group, Title, Sub, Extra, Icon, Key;
        public string Reveal;               // what opening it must expand first ("advanced"), or null
        public Color C1, C2;
        public int Order;
        public FrameworkElement View;       // the element on the page on screen (null in the index)
        internal string FTitle, FSub, FGroup, FPage, FExtra;

        internal void Fold()
        {
            FTitle = HistoryIndex.Fold(Title ?? "");
            FSub = HistoryIndex.Fold(Sub ?? "");
            FGroup = HistoryIndex.Fold(Group ?? "");
            FPage = HistoryIndex.Fold(PageName ?? "");
            FExtra = HistoryIndex.Fold(Extra ?? "");
        }
    }

    internal sealed class SearchHit
    {
        public SearchEntry Entry;
        public double Score;
        public readonly List<KeyValuePair<int, int>> TitleMarks = new List<KeyValuePair<int, int>>(), SubMarks = new List<KeyValuePair<int, int>>();
    }

    internal static class SettingsSearch
    {
        // Words that find each other: whoever types one of a group means the setting that says another (folded).
        internal static readonly string[][] Synonyms =
        {
            new string[] { "atajo", "tecla", "combinacion", "shortcut" },
            new string[] { "carpeta", "guardar", "directorio", "ruta" },
            new string[] { "micro", "microfono", "voz" },
            new string[] { "sonido", "audio", "volumen" },
            new string[] { "puntero", "raton", "cursor" },
            new string[] { "privacidad", "datos sensibles", "tapar", "ocultar" },
            new string[] { "tema", "oscuro", "claro", "apariencia" },
            new string[] { "actualizar", "actualizacion", "version" },
            new string[] { "captura", "pantallazo", "screenshot" },
            new string[] { "video", "grabar", "grabacion" },
            new string[] { "arranque", "iniciar", "encender" },
            new string[] { "camara", "webcam" },
            new string[] { "copiar", "portapapeles", "pegar" },
            new string[] { "borrar", "eliminar", "limpiar" },
            new string[] { "fotogramas", "fps" },
            new string[] { "nombre", "archivo", "fichero" },
            new string[] { "reloj", "fitbit", "watch", "pulsera", "pasos", "sueno" },
            new string[] { "acceso", "enlace", "favorito", "web", "url" },
            new string[] { "completa", "virtual", "remoto", "presentacion" },
            new string[] { "resumen", "hora" },
            new string[] { "nota", "apunte", "apuntar" }
        };

        // Left out of a search with other words: "carpeta de capturas" needs no "de" in the row, "modo oscuro" or
        // "activar micro" no "modo" or "activar".
        static readonly string[] StopWords =
        {
            "a", "al", "con", "de", "del", "el", "en", "la", "las", "lo", "los", "mi", "mis", "o", "para", "por", "que", "se", "tu", "tus", "u", "un", "una", "y",
            "activar", "desactivar", "ajuste", "ajustes", "cambiar", "como", "configurar", "configuracion", "modo", "opcion", "opciones"
        };

        // A synonym counts half: a row that says the word itself, even inside a longer one, comes before one that only
        // says a synonym.
        const double TitleWeight = 100, GroupWeight = 45, PageWeight = 35, SubWeight = 28, ExtraWeight = 22, Synonym = 0.5;

        // The folded words of a search, each once.
        internal static string[] Words(string query)
        {
            List<string> all = new List<string>(), kept = new List<string>();
            string f = HistoryIndex.Fold(query ?? "");
            int start = -1;
            for (int i = 0; i <= f.Length; i++)
            {
                bool letter = i < f.Length && char.IsLetterOrDigit(f[i]);
                if (letter && start < 0) start = i;
                if (letter || start < 0) continue;
                string w = f.Substring(start, i - start);
                if (!all.Contains(w)) all.Add(w);
                start = -1;
            }
            foreach (string w in all) if (Array.IndexOf(StopWords, w) < 0) kept.Add(w);
            return (kept.Count > 0 ? kept : all).ToArray();
        }

        // The word and, when it is (the start of) a word of a synonym group, the rest of the group.
        internal static List<string> Alternatives(string w)
        {
            List<string> alts = new List<string> { w };
            foreach (string[] g in Synonyms)
            {
                bool hit = false;
                foreach (string m in g)
                    if (m == w || (w.Length >= 3 && m.StartsWith(w, StringComparison.Ordinal)) || (m.Length >= 4 && w.StartsWith(m, StringComparison.Ordinal)) || Stem(m, w))
                    { hit = true; break; }
                if (!hit) continue;
                foreach (string m in g) if (!alts.Contains(m)) alts.Add(m);
            }
            return alts;
        }

        // "guardar" and "guardadas", "capturar" and "capturas": long words that share all but their ending.
        static bool Stem(string text, string w)
        {
            if (w.Length < 5) return false;
            int cp = 0;
            while (cp < w.Length && cp < text.Length && text[cp] == w[cp]) cp++;
            return cp < w.Length && cp >= Math.Max(4, w.Length - 2);
        }

        // How w is found in a folded text: 3 a word that starts with it, 2 a word with the same stem, 1 inside a word
        // (three letters or more), 0 not at all; at and len tell where, for the first best one. A synonym (not lenient)
        // only counts at the start of a word.
        internal static int Match(string f, string w, bool lenient, out int at, out int len)
        {
            at = -1;
            len = 0;
            if (string.IsNullOrEmpty(f) || string.IsNullOrEmpty(w)) return 0;
            int best = 0;
            for (int i = 0; i < f.Length; i++)
            {
                if (!char.IsLetterOrDigit(f[i]) || (i > 0 && char.IsLetterOrDigit(f[i - 1]))) continue;
                int cp = 0;
                while (cp < w.Length && i + cp < f.Length && f[i + cp] == w[cp]) cp++;
                if (cp == w.Length) { at = i; len = cp; return 3; }
                if (lenient && best < 2 && w.Length >= 5 && cp >= Math.Max(4, w.Length - 2)) { best = 2; at = i; len = cp; }
            }
            if (best > 0) return best;
            if (lenient && w.Length >= 3)
            {
                int k = f.IndexOf(w, StringComparison.Ordinal);
                if (k >= 0) { at = k; len = w.Length; return 1; }
            }
            return 0;
        }

        static double Quality(int q) { return q == 3 ? 1 : q == 2 ? 0.75 : q == 1 ? 0.55 : 0; }

        // Every word must be found (any order). Title matches rank first; the hits come grouped by page, the page with the
        // best hit first, and in each page by score and then in the order of the page.
        internal static List<SearchHit> Find(List<SearchEntry> entries, string query)
        {
            List<SearchHit> hits = new List<SearchHit>();
            string[] words = Words(query);
            if (words.Length == 0 || entries == null) return hits;
            List<string>[] alts = new List<string>[words.Length];
            for (int i = 0; i < words.Length; i++) alts[i] = Alternatives(words[i]);
            foreach (SearchEntry e in entries)
            {
                if (e.FTitle == null) e.Fold();
                double total = 0;
                bool allInTitle = true, found = true;
                for (int i = 0; i < words.Length && found; i++)
                {
                    double best = 0;
                    bool inTitle = false;
                    for (int j = 0; j < alts[i].Count; j++)
                    {
                        string a = alts[i][j];
                        bool own = j == 0;
                        double k = own ? 1 : Synonym;
                        int at, len, q = Match(e.FTitle, a, own, out at, out len);
                        if (q > 0 && own) inTitle = true;
                        best = Math.Max(best, TitleWeight * Quality(q) * k);
                        best = Math.Max(best, GroupWeight * Quality(Match(e.FGroup, a, own, out at, out len)) * k);
                        best = Math.Max(best, PageWeight * Quality(Match(e.FPage, a, own, out at, out len)) * k);
                        best = Math.Max(best, SubWeight * Quality(Match(e.FSub, a, own, out at, out len)) * k);
                        best = Math.Max(best, ExtraWeight * Quality(Match(e.FExtra, a, own, out at, out len)) * k);
                    }
                    if (best <= 0) found = false;
                    total += best;
                    allInTitle &= inTitle;
                }
                if (!found) continue;
                if (allInTitle) total += 25;
                int at0, len0;
                if (Match(e.FTitle, words[0], true, out at0, out len0) == 3 && at0 == 0) total += 15;
                SearchHit h = new SearchHit { Entry = e, Score = total };
                for (int i = 0; i < words.Length; i++)
                    for (int j = 0; j < alts[i].Count; j++)
                    {
                        MarkAll(e.FTitle, alts[i][j], j == 0, h.TitleMarks);
                        MarkAll(e.FSub, alts[i][j], j == 0, h.SubMarks);
                    }
                Merge(h.TitleMarks);
                Merge(h.SubMarks);
                hits.Add(h);
            }
            Dictionary<string, double> pageBest = new Dictionary<string, double>();
            foreach (SearchHit h in hits)
            {
                double b;
                if (!pageBest.TryGetValue(h.Entry.Page, out b) || h.Score > b) pageBest[h.Entry.Page] = h.Score;
            }
            List<string> order = new List<string>();
            foreach (SearchEntry e in entries) if (pageBest.ContainsKey(e.Page) && !order.Contains(e.Page)) order.Add(e.Page);
            hits.Sort(delegate(SearchHit x, SearchHit y)
            {
                if (x.Entry.Page != y.Entry.Page)
                {
                    int c = pageBest[y.Entry.Page].CompareTo(pageBest[x.Entry.Page]);
                    return c != 0 ? c : order.IndexOf(x.Entry.Page).CompareTo(order.IndexOf(y.Entry.Page));
                }
                int s = y.Score.CompareTo(x.Score);
                return s != 0 ? s : x.Entry.Order.CompareTo(y.Entry.Order);
            });
            return hits;
        }

        // Every word of the text that starts with w (or shares its stem); w inside a word only when nothing else is. A
        // synonym (not lenient) only where a word starts with it.
        static void MarkAll(string f, string w, bool lenient, List<KeyValuePair<int, int>> into)
        {
            if (string.IsNullOrEmpty(f) || string.IsNullOrEmpty(w)) return;
            bool any = false;
            for (int i = 0; i < f.Length; i++)
            {
                if (!char.IsLetterOrDigit(f[i]) || (i > 0 && char.IsLetterOrDigit(f[i - 1]))) continue;
                int cp = 0;
                while (cp < w.Length && i + cp < f.Length && f[i + cp] == w[cp]) cp++;
                if (cp == w.Length || (lenient && w.Length >= 5 && cp >= Math.Max(4, w.Length - 2))) { into.Add(new KeyValuePair<int, int>(i, cp)); any = true; }
            }
            if (any || !lenient || w.Length < 3) return;
            int k = f.IndexOf(w, StringComparison.Ordinal);
            if (k >= 0) into.Add(new KeyValuePair<int, int>(k, w.Length));
        }

        static void Merge(List<KeyValuePair<int, int>> marks)
        {
            if (marks.Count < 2) return;
            marks.Sort(delegate(KeyValuePair<int, int> a, KeyValuePair<int, int> b) { return a.Key.CompareTo(b.Key); });
            List<KeyValuePair<int, int>> m = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, int> r in marks)
            {
                if (m.Count > 0 && r.Key <= m[m.Count - 1].Key + m[m.Count - 1].Value)
                {
                    KeyValuePair<int, int> last = m[m.Count - 1];
                    m[m.Count - 1] = new KeyValuePair<int, int>(last.Key, Math.Max(last.Value, r.Key + r.Value - last.Key));
                }
                else m.Add(r);
            }
            marks.Clear();
            marks.AddRange(m);
        }
    }

    public partial class HomeWindow
    {
        // The pages with settings, in the sidebar's order (the mascot's own settings after its page). Their rows and
        // widgets are whatever the builders register; nothing here lists them.
        static readonly string[] SearchPages = { "keys", "general", "editor", "record", "mascotset" };

        // A group of the page on show, for the section chips: its title and the element at its top.
        sealed class SectionMark
        {
            public string Title;
            public FrameworkElement Anchor;
        }

        bool indexing;                          // a page is being built off screen for the index
        List<SearchEntry> indexMarks;           // what that build collects
        string indexReveal;                     // what the marks of this pass need expanded to be seen
        List<SearchEntry> liveMarks = new List<SearchEntry>();
        List<SectionMark> liveSections = new List<SectionMark>();
        StackPanel liveBody;                    // the page body those two belong to
        string markGroup, markIcon;             // the widget panel the next marks belong to
        Color markC1, markC2;
        SearchEntry markWidget;

        List<SearchEntry> searchIndex;          // every page's entries, or null until the first search
        List<SearchEntry> searchPartial;        // what the build under way has collected so far
        string searchIndexSig;
        Palette searchIndexPalette;
        bool searchIndexStale;
        int searchJob, searchJobNext = -1;
        internal int SearchIndexBuilds;         // finished builds

        internal static string PageTitle(string id)
        {
            if (id == "mascotset") return I18n.T("Ajustes de la mascota", "Mascot settings");
            int i = Array.IndexOf(PageIds, id);
            return i >= 0 ? PageNames[i] : id;
        }

        // ---- What the pages register while they are built

        List<SearchEntry> Marks()
        {
            if (indexing) return indexMarks;
            if (body == null) return null;
            if (liveBody != body)
            {
                liveBody = body;
                liveMarks = new List<SearchEntry>();
                liveSections = new List<SectionMark>();
                markGroup = markIcon = null;
                markWidget = null;
            }
            return liveMarks;
        }

        // A group of rows (Group): a section when it has a caption, and an entry per row.
        void SearchGroup(string caption, FrameworkElement holder, Row[] rows)
        {
            List<SearchEntry> into = Marks();
            if (into == null) return;
            if (caption != null && !indexing)
            {
                int i = body.Children.IndexOf(holder);
                FrameworkElement top = i > 0 ? body.Children[i - 1] as FrameworkElement : null;
                liveSections.Add(new SectionMark { Title = caption, Anchor = top ?? holder });
            }
            foreach (Row r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Title)) continue;
                string sub = string.IsNullOrEmpty(r.Sub) ? r.SubShown : r.Sub, extra = null;
                if (r is HotkeyRow)
                {
                    // The combination it has, which is also what people may type ("impr pant").
                    int a = Array.IndexOf(ActionNames, r.Title);
                    List<string> combos = a >= 0 && a < Settings.Actions.Length ? Hotkeys.Split(settings.HotkeysFor(Settings.Actions[a])) : new List<string>();
                    List<string> shown = new List<string>();
                    foreach (string c in combos) shown.Add(Hotkeys.DisplayOne(c));
                    sub = shown.Count > 0 ? string.Join(", ", shown.ToArray()) : I18n.T("Sin atajo", "No shortcut");
                    extra = sub;
                }
                AddMark(into, r, caption, r.Title, sub, extra, r.Icon, W(r.C1), W(r.C2));
            }
        }

        // A widget of the Grabacion page: a section, an entry, and the group of the tiles inside it.
        void SearchWidget(Grid inside, string icon, Color tint, string title, string sub)
        {
            List<SearchEntry> into = Marks();
            if (into == null) return;
            FrameworkElement card = inside.Parent as FrameworkElement ?? inside;
            if (!indexing)
            {
                FrameworkElement top = BodyChild(card);
                liveSections.Add(new SectionMark { Title = title, Anchor = top ?? card });
            }
            Color deep = Color.FromRgb((byte)(tint.R * 3 / 4), (byte)(tint.G * 3 / 4), (byte)(tint.B * 3 / 4));
            markWidget = AddMark(into, card, null, title, sub, null, icon, tint, deep);
            markGroup = title;
            markIcon = icon;
            markC1 = tint;
            markC2 = deep;
        }

        // A setting inside the last widget; without an icon it takes the widget's.
        void SearchMark(FrameworkElement at, string title, string sub, string icon, Color c1, Color c2)
        {
            List<SearchEntry> into = Marks();
            if (into == null || string.IsNullOrEmpty(title)) return;
            if (icon == null) { icon = markIcon; c1 = markC1; c2 = markC2; }
            AddMark(into, at, markGroup, title, sub, null, icon, c1, c2);
        }

        SearchEntry AddMark(List<SearchEntry> into, FrameworkElement view, string group, string title, string sub, string extra, string icon, Color c1, Color c2)
        {
            title = title.Trim();
            // A folder row's title is the folder's path: it is found by what it is.
            if (icon == "folder") extra = extra == null ? I18n.T("carpeta", "folder") : extra + I18n.T(" carpeta", " folder");
            int same = 0;
            foreach (SearchEntry o in into)
                if (o.Group == group && o.Title == title)
                {
                    if (indexReveal != null && o.Reveal == null) return null;   // already shown without expanding anything
                    same++;
                }
            SearchEntry e = new SearchEntry
            {
                Page = page, PageName = PageTitle(page), Group = group, Title = title, Sub = string.IsNullOrEmpty(sub) ? null : sub, Extra = extra,
                Icon = icon, C1 = c1, C2 = c2, View = view, Order = into.Count, Reveal = indexReveal, Key = (group ?? "") + "\n" + title + "\n" + same
            };
            into.Add(e);
            return e;
        }

        // What a widget says further down (the GIF's advice, the engine's state): its description in the results when it
        // has none under its title, and more words it is found by.
        void SearchWidgetMore(string sub, string words)
        {
            if (Marks() == null || markWidget == null) return;
            if (markWidget.Sub == null && !string.IsNullOrEmpty(sub)) markWidget.Sub = sub;
            if (!string.IsNullOrEmpty(words)) markWidget.Extra = markWidget.Extra == null ? words : markWidget.Extra + " " + words;
        }

        // Grabacion > Calidad once the chosen quality shows its options: those options (the frame rate of Estandar, the
        // two of Cine), and the words of each quality's options on its card, so a search finds the card that brings them.
        void SearchQuality(string fpsTitle)
        {
            List<SearchEntry> into = Marks();
            if (into == null || recCards == null || recCards.Length != RecOrder.Length || recFps == null || recCine == null) return;
            List<string> cine = new List<string>();
            Panel cols = recCine as Panel;
            if (cols != null)
                foreach (UIElement c in cols.Children)
                {
                    StackPanel col = c as StackPanel;
                    TextBlock t = col != null && col.Children.Count > 0 ? col.Children[0] as TextBlock : null;
                    if (t == null) continue;
                    cine.Add(t.Text);
                    TextBlock note = col.Children[col.Children.Count - 1] as TextBlock;
                    if (recCine.Visibility == Visibility.Visible) SearchMark(col, t.Text, note != t && note != null ? note.Text : null, null, Colors.Transparent, Colors.Transparent);
                }
            if (recFps.Visibility == Visibility.Visible) SearchMark(recFps, fpsTitle, recFps.ToolTip as string, null, Colors.Transparent, Colors.Transparent);
            foreach (SearchEntry e in into)
            {
                if (e.View == recCards[0].Root) e.Extra = fpsTitle;
                else if (e.View == recCards[RecOrder.Length - 1].Root) e.Extra = string.Join(" ", cine.ToArray());
            }
        }

        // The child of the page body that holds e.
        FrameworkElement BodyChild(FrameworkElement e)
        {
            DependencyObject d = e;
            while (d != null)
            {
                DependencyObject up = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
                if (up == body) return d as FrameworkElement;
                d = up;
            }
            return null;
        }

        // ---- Building the index

        static FieldInfo[] stateFields;

        // The window's own fields a page builder may set (its controls, one-off notes, the page and body): kept before an
        // index build and put back after it, so the page on screen never notices. Fields other threads write are left out.
        static FieldInfo[] StateFields()
        {
            if (stateFields != null) return stateFields;
            List<FieldInfo> l = new List<FieldInfo>();
            foreach (FieldInfo f in typeof(HomeWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (f.IsInitOnly || f.IsLiteral) continue;
                if (Array.IndexOf(f.GetRequiredCustomModifiers(), typeof(System.Runtime.CompilerServices.IsVolatile)) >= 0) continue;
                l.Add(f);
            }
            stateFields = l.ToArray();
            return stateFields;
        }

        void BuildFor(string id)
        {
            switch (id)
            {
                case "keys": BuildKeys(); break;
                case "general": BuildGeneral(); break;
                case "editor": BuildEditor(); break;
                case "record": BuildRecord(); break;
                case "mascotset": BuildMascotSettings(); break;
            }
        }

        // Builds one page into a body that is never shown and returns what it registered. The builders know (indexing)
        // to leave out what would reach outside the page: the microphone test, the device list, the preview images.
        List<SearchEntry> IndexPage(string id)
        {
            FieldInfo[] fs = StateFields();
            object[] kept = new object[fs.Length];
            for (int i = 0; i < fs.Length; i++) kept[i] = fs[i].GetValue(this);
            List<SearchEntry> got = new List<SearchEntry>();
            try
            {
                indexing = true;
                indexMarks = got;
                indexReveal = null;
                markGroup = markIcon = null;
                markWidget = null;
                page = id;
                body = new StackPanel();
                BuildFor(id);
            }
            catch (Exception ex) { ShotStack.Log("Buscar ajustes (" + id + "): " + ex.Message); }
            finally
            {
                for (int i = 0; i < fs.Length; i++) fs[i].SetValue(this, kept[i]);
            }
            foreach (SearchEntry e in got)
            {
                e.View = null;
                e.Fold();
            }
            return got;
        }

        // Everything that decides what the pages show: any setting (the tray, a policy or the command line may change one
        // while Home is open).
        static FieldInfo[] settingsFields;

        string SearchSignature()
        {
            if (settingsFields == null) settingsFields = typeof(Settings).GetFields(BindingFlags.Instance | BindingFlags.Public);
            StringBuilder b = new StringBuilder();
            foreach (FieldInfo f in settingsFields) b.Append(f.GetValue(settings)).Append('\u001F');
            return b.ToString();
        }

        bool SearchIndexReady
        {
            get { return searchIndex != null && !searchIndexStale && searchIndexPalette == Ds.Brushes && searchIndexSig == SearchSignature(); }
        }

        // What a search looks in: the index or, while the first one is being built, the pages done so far.
        List<SearchEntry> SearchEntries()
        {
            if (searchIndex != null) return searchIndex;
            return searchPartial;
        }

        // Builds the index a page per turn of the UI thread, below input, so typing never waits for it.
        void SearchIndexSoon()
        {
            if (searchJobNext >= 0 || SearchIndexReady) return;
            int job = ++searchJob;
            string sig = SearchSignature();
            Palette pal = Ds.Brushes;
            List<SearchEntry> got = new List<SearchEntry>();
            searchPartial = got;
            searchIndexStale = false;
            searchJobNext = 0;
            Action step = null;
            step = delegate
            {
                if (job != searchJob) return;
                if (IsDisposed) { searchJobNext = -1; return; }
                string pg = SearchPages[searchJobNext++];
                if (PageShown(pg)) got.AddRange(IndexPage(pg));
                if (searchJobNext < SearchPages.Length)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, step);
                    return;
                }
                SearchIndexDone(got, sig, pal);
                if (SearchShowing) SearchRun();
            };
            Dispatcher.BeginInvoke(DispatcherPriority.Background, step);
        }

        void SearchIndexDone(List<SearchEntry> got, string sig, Palette pal)
        {
            for (int i = 0; i < got.Count; i++) got[i].Order = i;
            searchIndex = got;
            searchIndexSig = sig;
            searchIndexPalette = pal;
            searchPartial = null;
            searchJobNext = -1;
            SearchIndexBuilds++;
        }

        // The whole index at once (tests, and a search that can't wait); ms gets each page's time when given.
        internal List<SearchEntry> SearchIndexNow(List<double> ms)
        {
            searchJob++;
            string sig = SearchSignature();
            Palette pal = Ds.Brushes;
            searchIndexStale = false;
            List<SearchEntry> got = new List<SearchEntry>();
            foreach (string id in SearchPages)
            {
                Stopwatch sw = Stopwatch.StartNew();
                got.AddRange(IndexPage(id));
                if (ms != null) ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            SearchIndexDone(got, sig, pal);
            return got;
        }
    }
}
