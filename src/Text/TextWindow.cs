// Stackshot - The text viewer: plain text, code and settings files with colours; logs with their levels, filters and
// live following; JSON as a tree beside its text; CSV and TSV as a table. Any size: big files are read as they are shown.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed partial class TextWindow : Sheet
    {
        static readonly List<TextWindow> open = new List<TextWindow>();

        static readonly string[] DataExts = { ".csv", ".tsv", ".tab" };
        static readonly string[] JsonExts = { ".json", ".jsonl", ".ndjson", ".geojson", ".har" };

        readonly ShotStack owner;
        string path;
        TextFile file;
        TextKind kind = TextKind.Plain;
        bool table, json;          // the file is a table / JSON (its views exist)
        int view;                  // 0 text, 1 tree or table
        readonly TextView text = new TextView();
        JsonTree tree;
        CsvGrid grid;
        JsonNode root;
        string jsonError;
        int jsonErrLine, jsonErrCol;
        string pretty;
        string[] prettyLines;
        CsvTable csv;
        bool loading;
        int token;

        Grid page;
        Border bodyHost, bannerHost, sayHost;
        TextBlock titleLabel, subLabel, statusLabel, sayLabel;
        SearchField search;
        DocIconButton follow, wrapButton;
        MacButton levelButton;
        StackPanel levelChips;
        readonly DispatcherTimer followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        readonly DispatcherTimer sayFade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        DispatcherTimerLite searchLater;
        bool following;
        LogLevel minLevel = LogLevel.None;
        bool filterOn;
        string query = "";

        // Rows shown: every line, or the ones the filter keeps (indices into the file's lines).
        List<int> kept;
        // Search matches: row of the file, column, and the one shown.
        readonly List<int[]> matches = new List<int[]>();
        int matchAt = -1;
        int[] levelCount = new int[7];

        internal static Func<string, string> PickPath { get; set; }

        public static bool Opens(string path)
        {
            string e = (System.IO.Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (e == ".txt" || e == ".text" || e == ".log" || e == ".out" || e == ".err") return true;
            if (Array.IndexOf(DataExts, e) >= 0 || Array.IndexOf(JsonExts, e) >= 0) return true;
            TextKind k = TextColors.Of(path);
            return k == TextKind.Settings || k == TextKind.Code;
        }

        public static TextWindow OpenFile(ShotStack owner, string path)
        {
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch { full = path; }
            foreach (TextWindow w in open)
                if (string.Equals(w.path, full, StringComparison.OrdinalIgnoreCase)) { w.Front(); return w; }
            TextWindow n = new TextWindow(owner);
            open.Add(n);
            n.reveal = true;
            n.Load(full);
            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            t.Tick += delegate { t.Stop(); n.RevealNow(); };
            t.Start();
            return n;
        }

        public static void CloseAll()
        {
            foreach (TextWindow w in new List<TextWindow>(open)) { try { w.Close(); } catch { } }
        }

        bool reveal;

        void RevealNow()
        {
            if (!reveal) return;
            reveal = false;
            if (!IsVisible) Show();
            Front();
        }

        TextWindow(ShotStack owner)
        {
            this.owner = owner;
            Title = "Texto";
            Width = 1060;
            Height = 760;
            MinWidth = 600;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = AppIcon;
            AllowDrop = true;
            PreviewKeyDown += OnKey;
            PreviewDragOver += delegate(object o, DragEventArgs e) { e.Effects = Dropped(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            PreviewDrop += delegate(object o, DragEventArgs e) { string f = Dropped(e.Data); if (f == null) return; e.Handled = true; if (file == null) Load(f); else OpenFile(owner, f); };
            searchLater = new DispatcherTimerLite(200, RunSearch);
            followTimer.Tick += delegate { Poll(); };
            sayFade.Tick += delegate { sayFade.Stop(); if (sayHost != null) sayHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260))); };
            text.Moved += Status;
            text.Menu += delegate { TextMenu(); };
            Closed += delegate
            {
                token++;
                followTimer.Stop();
                text.Detach();
                if (file != null) file.Dispose();
                open.Remove(this);
            };
            Build();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            System.Windows.Interop.HwndSource src = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
            if (src != null) src.AddHook(EdgeHit);
        }

        IntPtr EdgeHit(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg != 0x84 || WindowState != WindowState.Normal) return IntPtr.Zero;
            int x = (short)((long)lp & 0xFFFF), y = (short)(((long)lp >> 16) & 0xFFFF);
            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return IntPtr.Zero;
            int b = (int)Math.Round(6 * Ink.Scale(this));
            bool l = x < r.Left + b, rt = x >= r.Right - b, t = y < r.Top + b, bt = y >= r.Bottom - b;
            int hit = t ? (l ? 13 : rt ? 14 : 12) : bt ? (l ? 16 : rt ? 17 : 15) : l ? 10 : rt ? 11 : 0;
            if (hit == 0) return IntPtr.Zero;
            handled = true;
            return new IntPtr(hit);
        }

        protected override bool Snaps { get { return true; } }
        protected override void ThemeChanged() { Build(); }

        void Front()
        {
            try { if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Topmost = true; Topmost = false; Activate(); }
            catch { }
        }

        internal TextView View { get { return text; } }
        internal TextFile File { get { return file; } }
        internal bool Loading { get { return loading; } }
        internal JsonNode JsonRoot { get { return root; } }
        internal CsvTable Table { get { return csv; } }
        internal int Shown { get { return kept != null ? kept.Count : file == null ? 0 : file.Count; } }
        internal string lastSay;

        // ---- Loading

        void Load(string f)
        {
            int my = ++token;
            loading = true;
            path = f;
            Title = System.IO.Path.GetFileName(f);
            Dispatcher ui = Dispatcher;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                TextFile tf = TextFile.Open(f, out error);
                // What can be worked out off the UI thread: the kind of file and, when it fits, its JSON or its table.
                TextKind k = TextColors.Of(f);
                string ext = (System.IO.Path.GetExtension(f) ?? "").ToLowerInvariant();
                JsonNode jr = null; string je = null; int jl = 0, jc = 0; string pj = null;
                CsvTable ct = null;
                if (tf != null)
                {
                    if (k == TextKind.Plain && tf.Count > 0)
                    {
                        List<string> head = new List<string>();
                        for (int i = 0; i < Math.Min(tf.Count, 200); i++) head.Add(tf.Line(i));
                        if (LogLines.LooksLikeLog(head)) k = TextKind.Log;
                    }
                    if (tf.Whole && Array.IndexOf(JsonExts, ext) >= 0)
                    {
                        string err; int line, col;
                        bool lines = ext == ".jsonl" || ext == ".ndjson";
                        JsonNode parsed = lines ? JsonDoc.ParseLines(tf.Text, out err, out line, out col) : JsonDoc.Parse(tf.Text, out err, out line, out col);
                        if (parsed == null) { je = err; jl = line; jc = col; }
                        else
                        {
                            // Shown formatted (a minified file is one endless line); its positions are those of the text shown.
                            pj = JsonDoc.Pretty(parsed, 2);
                            string e2; int l2, c2;
                            jr = JsonDoc.Parse(pj, out e2, out l2, out c2) ?? parsed;
                        }
                    }
                    if (tf.Whole && Array.IndexOf(DataExts, ext) >= 0)
                    {
                        string err;
                        ct = CsvTable.Parse(tf.Text, 200000, out err);
                    }
                }
                ui.BeginInvoke(new Action(delegate
                {
                    if (my != token) { if (tf != null) tf.Dispose(); return; }
                    loading = false;
                    if (tf == null) { RevealNow(); Say(error, true, true); ShowEmpty(error); return; }
                    if (file != null) file.Dispose();
                    file = tf;
                    kind = k;
                    json = Array.IndexOf(JsonExts, ext) >= 0;
                    table = ct != null;
                    root = jr; jsonError = je; jsonErrLine = jl; jsonErrCol = jc; pretty = pj;
                    prettyLines = pj == null ? null : pj.Replace("\r\n", "\n").Split('\n');
                    csv = ct;
                    view = (root != null || table) ? 1 : 0;
                    file.Grew += delegate { Dispatcher.BeginInvoke(new Action(delegate { if (file == tf) Grown(); })); };
                    kept = null;
                    matches.Clear();
                    matchAt = -1;
                    // A log written to in the last minute is followed live from the start.
                    following = kind == TextKind.Log && Recent(f);
                    if (following) followTimer.Start();
                    CountLevels();
                    Build();
                    ShowRows();
                    if (kind == TextKind.Log && following) text.ToEnd();
                    RevealNow();
                }));
            });
        }

        static bool Recent(string f)
        {
            try { return (DateTime.Now - System.IO.File.GetLastWriteTime(f)).TotalMinutes < 1; }
            catch { return false; }
        }

        // ---- Rows

        // What the text view shows now: the formatted JSON, the file's lines, or the lines a filter keeps.
        void ShowRows()
        {
            if (file == null) return;
            text.Colors = TextColors.For(pretty != null ? TextKind.Json : kind);
            if (pretty != null)
            {
                string[] pl = prettyLines;
                text.Show(new TextRows { Count = delegate { return pl.Length; }, Text = delegate(int r) { return pl[r]; }, Number = delegate(int r) { return r; } });
            }
            else
            {
                TextFile f = file;
                List<int> k = kept;
                Func<int, Color?> band = null;
                if (kind == TextKind.Log) band = delegate(int r) { return TextColors.Band(LogLines.LevelOf(f.Line(k == null ? r : k[r]))); };
                text.Show(new TextRows
                {
                    Count = delegate { return k == null ? f.Count : k.Count; },
                    Text = delegate(int r) { return f.Line(k == null ? r : k[r]); },
                    Number = delegate(int r) { return k == null ? r : k[r]; },
                    Band = band
                });
            }
            text.Wrap = wrapOn && file.Whole;
            text.Query = query;
            Status();
        }

        bool wrapOn;

        void Grown()
        {
            if (file == null) return;
            if (kept != null) Refilter(); else text.Refresh(following);
            CountLevels();
            SyncSub();
            if (query.Length > 0 && !filterOn) searchLater.Restart();
        }

        void Poll()
        {
            if (file == null || !following) return;
            int before = file.Count;
            if (file.Refresh())
            {
                if (file.Count < before) { kept = null; ShowRows(); }
                Grown();
                if (following) text.ToEnd();
            }
        }

        // ---- Log levels

        void CountLevels()
        {
            levelCount = new int[7];
            if (file == null || kind != TextKind.Log) return;
            int n = file.Count;
            // A big log is counted in the background as it is indexed; the first part right away.
            int quick = Math.Min(n, 200000);
            for (int i = 0; i < quick; i++) levelCount[(int)LogLines.LevelOf(file.Line(i))]++;
            if (n > quick)
            {
                TextFile f = file;
                int my = token;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    int[] c = new int[7];
                    for (int i = 0; i < f.Count; i++) c[(int)LogLines.LevelOf(f.Line(i))]++;
                    Dispatcher.BeginInvoke(new Action(delegate { if (my == token && file == f) { levelCount = c; FillChips(); } }));
                });
            }
            FillChips();
        }

        void FillChips()
        {
            if (levelChips == null) return;
            levelChips.Children.Clear();
            int errors = levelCount[(int)LogLevel.Error] + levelCount[(int)LogLevel.Fatal], warns = levelCount[(int)LogLevel.Warn];
            if (errors > 0) levelChips.Children.Add(Chip(errors.ToString("N0") + (errors == 1 ? " error" : " errores"), Ds.Brushes.Red, delegate { NextLevel(LogLevel.Error); }));
            if (warns > 0) levelChips.Children.Add(Chip(warns.ToString("N0") + (warns == 1 ? " aviso" : " avisos"), Ds.Brushes.Orange, delegate { NextLevel(LogLevel.Warn); }));
        }

        FrameworkElement Chip(string label, Color c, Action act)
        {
            Border b = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, Background = Ds.Brush(Ds.WithAlpha(c, 0.16)), ToolTip = "Ir al siguiente (F8)" };
            b.Child = new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(c) };
            b.MouseLeftButtonUp += delegate { act(); };
            return b;
        }

        // The next line of that level (or worse) after the caret, round to the start.
        void NextLevel(LogLevel l)
        {
            if (file == null) return;
            int n = Shown, from = Math.Max(0, text.CaretRow) + 1;
            for (int k = 0; k < n; k++)
            {
                int r = (from + k) % n;
                LogLevel x = LogLines.LevelOf(file.Line(kept == null ? r : kept[r]));
                if (x >= l && x != LogLevel.None) { text.CaretRow = r; text.ScrollToRow(r, true); text.InvalidateVisual(); Status(); return; }
            }
            Say("No hay m\u00E1s l\u00EDneas de ese nivel.", false);
        }

        void LevelMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Todos los niveles", null, minLevel == LogLevel.None, delegate { SetLevel(LogLevel.None); }));
            m.Add(MenuEntry.Item("Informaci\u00F3n y m\u00E1s", null, minLevel == LogLevel.Info, delegate { SetLevel(LogLevel.Info); }));
            m.Add(MenuEntry.Item("Avisos y errores", null, minLevel == LogLevel.Warn, delegate { SetLevel(LogLevel.Warn); }));
            m.Add(MenuEntry.Item("Solo errores", null, minLevel == LogLevel.Error, delegate { SetLevel(LogLevel.Error); }));
            MacMenu.Show(anchor, m);
        }

        void SetLevel(LogLevel l)
        {
            minLevel = l;
            if (levelButton != null) levelButton.Label = LevelName(l);
            Refilter();
        }

        static string LevelName(LogLevel l)
        {
            return l == LogLevel.None ? "Todos los niveles" : l == LogLevel.Info ? "Informaci\u00F3n y m\u00E1s" : l == LogLevel.Warn ? "Avisos y errores" : "Solo errores";
        }

        // ---- Filter: only the lines that match the search and the level (a log's continuation lines go with theirs)

        void Refilter()
        {
            if (file == null) return;
            bool byText = filterOn && query.Length > 0, byLevel = minLevel != LogLevel.None && kind == TextKind.Log;
            if (!byText && !byLevel) { kept = null; ShowRows(); return; }
            TextFile f = file;
            string q = PdfPageText.Fold(query);
            LogLevel min = minLevel;
            int my = ++filterToken;
            Say("Filtrando\u2026", false, true);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<int> o = new List<int>();
                LogLevel last = LogLevel.None;
                int n = f.Count;
                for (int i = 0; i < n && my == filterToken; i++)
                {
                    string line = f.Line(i);
                    LogLevel l = LogLines.LevelOf(line);
                    if (l == LogLevel.None && LogLines.Continuation(line)) l = last; else if (l != LogLevel.None) last = l;
                    if (byLevel && (l == LogLevel.None || l < min)) continue;
                    if (byText && PdfPageText.Fold(line).IndexOf(q, StringComparison.Ordinal) < 0) continue;
                    o.Add(i);
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (my != filterToken || file != f) return;
                    kept = o;
                    ShowRows();
                    CloseSay();
                    if (o.Count == 0) Say("Ninguna l\u00EDnea coincide.", false);
                }));
            });
        }

        int filterToken;

        // ---- Search

        void RunSearch()
        {
            string q = search == null ? "" : search.Text.Trim();
            query = q;
            text.Query = q;
            matches.Clear();
            matchAt = -1;
            text.Current = null;
            if (view == 1 && tree != null) { tree.Filter(q); search.SetCount(-1, tree.Matches); }
            if (view == 1 && grid != null) { grid.Filter(q); search.SetCount(-1, grid.Shown); }
            if (filterOn) { Refilter(); return; }
            text.InvalidateVisual();
            if (q.Length == 0 || file == null) { if (search != null && view == 0) search.SetCount(-1, 0); return; }
            if (view == 1) return;
            string fq = PdfPageText.Fold(q);
            int n = Shown;
            List<int> k = kept;
            TextFile f = file;
            string[] pl = prettyLines;
            int my = ++searchToken;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<int[]> found = new List<int[]>();
                for (int r = 0; r < n && found.Count < 100000 && my == searchToken; r++)
                {
                    string line = pl != null ? pl[r] : f.Line(k == null ? r : k[r]);
                    string fl = PdfPageText.Fold(line);
                    if (fl.Length != line.Length) continue;
                    int at = fl.IndexOf(fq, StringComparison.Ordinal);
                    while (at >= 0) { found.Add(new int[] { r, at, fq.Length }); at = fl.IndexOf(fq, at + Math.Max(1, fq.Length), StringComparison.Ordinal); }
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (my != searchToken) return;
                    matches.Clear();
                    matches.AddRange(found);
                    int from = Math.Max(0, text.TopRow);
                    matchAt = -1;
                    for (int i = 0; i < matches.Count; i++) if (matches[i][0] >= from) { matchAt = i; break; }
                    if (matchAt < 0 && matches.Count > 0) matchAt = 0;
                    ShowMatch();
                }));
            });
        }

        int searchToken;

        void Step(int d)
        {
            if (view == 1 && tree != null) { tree.Step(d); return; }
            if (view == 1 && grid != null) return;
            if (matches.Count == 0) return;
            matchAt = matchAt < 0 ? 0 : (matchAt + d + matches.Count) % matches.Count;
            ShowMatch();
        }

        void ShowMatch()
        {
            if (search != null) search.SetCount(matchAt, matches.Count);
            if (matchAt < 0 || matchAt >= matches.Count) { text.Current = null; text.InvalidateVisual(); return; }
            int[] m = matches[matchAt];
            text.Current = m;
            text.CaretRow = m[0];
            text.ScrollToRow(m[0], true);
            text.InvalidateVisual();
            Status();
        }

        // ---- Keys

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) { if (e.Key == Key.Escape) { Body().Focus(); e.Handled = true; } return; }
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            Key k = e.Key == Key.System ? e.SystemKey : e.Key;
            if (ctrl && k == Key.W) { Close(); e.Handled = true; return; }
            if (ctrl && k == Key.O) { Browse(); e.Handled = true; return; }
            if (file == null) return;
            if (ctrl && k == Key.F) { search.Box.Focus(); search.Box.SelectAll(); e.Handled = true; return; }
            if (k == Key.F3 || (k == Key.Enter && search != null && search.Text.Length > 0)) { Step(shift ? -1 : 1); e.Handled = true; return; }
            if (k == Key.F8 && kind == TextKind.Log) { NextLevel(LogLevel.Warn); e.Handled = true; return; }
            if (ctrl && k == Key.G) { GoToLine(); e.Handled = true; return; }
            if (ctrl && k == Key.C) { Copy(); e.Handled = true; return; }
            if (ctrl && k == Key.A && view == 0) { text.SelectAll(); e.Handled = true; return; }
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && k == Key.Z) { ToggleWrap(); e.Handled = true; return; }
            if (ctrl && (k == Key.OemPlus || k == Key.Add)) { text.SetFont(text.FontSizeNow + 1); e.Handled = true; return; }
            if (ctrl && (k == Key.OemMinus || k == Key.Subtract)) { text.SetFont(text.FontSizeNow - 1); e.Handled = true; return; }
            if (ctrl && (k == Key.D0 || k == Key.NumPad0)) { text.SetFont(13); e.Handled = true; return; }
            if (k == Key.Escape)
            {
                if (search != null && search.Text.Length > 0) { search.Text = ""; e.Handled = true; return; }
                if (text.HasSelection) { text.ClearSelection(); e.Handled = true; return; }
            }
            if (view == 0 && text.HandleKey(e)) e.Handled = true;
            else if (view == 1 && tree != null && tree.HandleKey(e)) e.Handled = true;
            else if (view == 1 && grid != null && grid.HandleKey(e)) e.Handled = true;
        }

        FrameworkElement Body()
        {
            if (view == 1 && tree != null) return tree;
            if (view == 1 && grid != null) return grid;
            return text;
        }

        static string Dropped(IDataObject d)
        {
            try
            {
                string[] files = d.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return null;
                foreach (string f in files) if (Opens(f) && System.IO.File.Exists(f)) return f;
            }
            catch { }
            return null;
        }

        void Browse()
        {
            using (System.Windows.Forms.OpenFileDialog d = new System.Windows.Forms.OpenFileDialog())
            {
                d.Title = "Abrir texto, log o datos";
                d.Filter = "Texto, logs y datos|*.txt;*.log;*.json;*.jsonl;*.csv;*.tsv;*.ini;*.cfg;*.conf;*.yaml;*.yml;*.toml;*.xml;*.cs;*.js;*.ts;*.py;*.sql;*.ps1|Todos los archivos|*.*";
                if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                if (file == null) Load(d.FileName); else OpenFile(owner, d.FileName);
            }
        }
    }
}
