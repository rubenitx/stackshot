// Stackshot - Comparing the open XML with the one before it in its folder, or with any other file: computed off the UI
// thread, shown in place of the tree (read-only) with a bar to filter, step through the differences and leave.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        // Tests answer the file picker of "Comparar con otro archivo..." here (null cancels).
        internal static Func<string> PickCompare { get; set; }

        sealed class Comparison
        {
            public string Text;         // the document text it was made from
            public DiffResult Result;
            public XmlDiffView View;
            public Palette Pal;
            public string OldPath;
            public bool OnlyDiffs = true;
        }

        Comparison compare;
        int compareToken;
        bool comparing;
        TextBlock comparePos;
        DocIconButton compareUp, compareDown;

        internal XmlDiffView CompareView { get { return compare != null ? compare.View : null; } }
        internal bool Comparing { get { return compare != null; } }
        internal bool CompareBusy { get { return comparing; } }

        // A comparison belongs to the text it was made from, in Vista: editing, another document or a reload ends it.
        void DropStaleCompare()
        {
            if (compare != null && (!xml || editing || text == null || compare.Text != text)) compare = null;
        }

        bool CompareShown()
        {
            DropStaleCompare();
            return compare != null && compare.View != null;
        }

        // Once the tree is in the preview: the comparison takes its place while it lasts (redrawn for a new theme).
        void ShowCompareView()
        {
            if (!CompareShown() || previewHost == null) return;
            if (compare.Pal != Ds.Brushes) NewCompareView();
            // Still the child of the preview host of the page this one replaced.
            Border was = compare.View.Parent as Border;
            if (was != null && was != previewHost) was.Child = null;
            previewHost.Child = compare.View;
        }

        void NewCompareView()
        {
            DiffNode keep = compare.View != null ? compare.View.SelectedNode : null;
            XmlDiffView v = new XmlDiffView(Ds.Brushes, compare.Result, compare.OnlyDiffs);
            v.Moved += UpdateComparePos;
            v.Menu += ShowMenu;
            v.CopyRequest += delegate(string t, string what)
            {
                if (Retry(delegate { Clipboard.SetText(t); })) Say(what, false);
                else Say(I18n.T("No he podido usar el portapapeles.", "Could not access clipboard."), true);
            };
            compare.View = v;
            compare.Pal = Ds.Brushes;
            if (keep != null) v.Reveal(keep);
        }

        // "3 cambios · 1 añadido · 2 quitados" (only what there is), for the status line.
        internal string CompareStatus()
        {
            if (compare == null) return "";
            DiffResult r = compare.Result;
            if (r.Total == 0) return I18n.T("Sin diferencias", "No differences");
            List<string> parts = new List<string>();
            if (r.Changed > 0) parts.Add(I18n.Fmt(r.Changed == 1 ? "{0} cambio" : "{0} cambios", r.Changed == 1 ? "{0} change" : "{0} changes", r.Changed.ToString("N0")));
            if (r.Added > 0) parts.Add(I18n.Fmt(r.Added == 1 ? "{0} a\u00F1adido" : "{0} a\u00F1adidos", r.Added == 1 ? "{0} added" : "{0} added", r.Added.ToString("N0")));
            if (r.Removed > 0) parts.Add(I18n.Fmt(r.Removed == 1 ? "{0} quitado" : "{0} quitados", r.Removed == 1 ? "{0} removed" : "{0} removed", r.Removed.ToString("N0")));
            return string.Join(" \u00B7 ", parts.ToArray());
        }

        FrameworkElement CompareBar()
        {
            Palette pal = Ds.Brushes;
            Border b = new Border { Background = Ds.Brush(pal.High ? pal.Window : Ds.WithAlpha(pal.Accent, pal.Dark ? 0.12 : 0.06)), BorderBrush = Ds.Brush(pal.High ? pal.Hairline : Ds.WithAlpha(pal.Accent, 0.28)), BorderThickness = new Thickness(0, Soap() != null ? 0 : 1, 0, 1), Padding = new Thickness(18, 4, 12, 4) };
            Grid g = new Grid();
            for (int i = 0; i < 6; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            g.Children.Add(new TextBlock { Text = I18n.T("Comparaci\u00F3n", "Comparison"), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Label), VerticalAlignment = VerticalAlignment.Center });

            string oldName = System.IO.Path.GetFileName(compare.OldPath), newName = name ?? I18n.T("Sin t\u00EDtulo", "Untitled");
            TextBlock files = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 0, 10, 0) };
            files.Inlines.Add(new Run(I18n.T("antes ", "before ")) { Foreground = Ds.Brush(pal.Label2) });
            files.Inlines.Add(new Run(oldName) { Foreground = Ds.Brush(pal.Label) });
            files.Inlines.Add(new Run(I18n.T("   ahora ", "   after ")) { Foreground = Ds.Brush(pal.Label2) });
            files.Inlines.Add(new Run(newName) { Foreground = Ds.Brush(pal.Label) });
            files.ToolTip = I18n.Fmt("Antes: {0}\nAhora: {1}", "Before: {0}\nAfter: {1}", compare.OldPath, (file != null && file.Path != null ? file.Path : newName));
            Grid.SetColumn(files, 1);
            g.Children.Add(files);

            ChipToggle only = new ChipToggle(I18n.T("Solo diferencias", "Differences only"), I18n.T("Mostrar solo lo que cambia (y lo que lleva hasta ello)", "Show only what changes (and path leading to it)"), false) { IsOn = compare.OnlyDiffs, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            only.Toggled += delegate(bool on)
            {
                if (compare == null) return;
                compare.OnlyDiffs = on;
                if (compare.View != null) compare.View.OnlyDiffs = on;
            };
            Grid.SetColumn(only, 2);
            g.Children.Add(only);

            comparePos = new TextBlock { FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            Grid.SetColumn(comparePos, 3);
            g.Children.Add(comparePos);

            StackPanel nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            compareUp = new DocIconButton("up", I18n.T("Diferencia anterior (May\u00FAs+F7)", "Previous difference (Shift+F7)"), 26);
            compareUp.Click += delegate { StepCompare(-1); };
            compareDown = new DocIconButton("down", I18n.T("Siguiente diferencia (F7)", "Next difference (F7)"), 26) { Margin = new Thickness(2, 0, 0, 0) };
            compareDown.Click += delegate { StepCompare(1); };
            nav.Children.Add(compareUp);
            nav.Children.Add(compareDown);
            Grid.SetColumn(nav, 4);
            g.Children.Add(nav);

            MacButton exit = new MacButton(I18n.T("Salir de la comparaci\u00F3n", "Exit comparison"), ButtonKind.Secondary, "close", null, 26) { VerticalAlignment = VerticalAlignment.Center };
            exit.ToolTip = I18n.T("Salir de la comparaci\u00F3n (Esc)", "Exit comparison (Esc)");
            exit.Click += ExitCompare;
            Grid.SetColumn(exit, 5);
            g.Children.Add(exit);
            b.Child = g;
            UpdateComparePos();
            return b;
        }

        void UpdateComparePos()
        {
            if (compare == null || comparePos == null) return;
            int total = compare.Result.Stops.Count, cur = compare.View != null ? compare.View.Current : -1;
            comparePos.Text = cur >= 0 ? I18n.Fmt("{0} de {1}", "{0} of {1}", (cur + 1).ToString("N0"), total.ToString("N0")) : total == 0 ? I18n.T("Sin diferencias", "No differences") : I18n.Fmt(total == 1 ? "{0} diferencia" : "{0} diferencias", total == 1 ? "{0} difference" : "{0} differences", total.ToString("N0"));
            if (compareUp != null) compareUp.IsEnabled = total > 0;
            if (compareDown != null) compareDown.IsEnabled = total > 0;
        }

        void StepCompare(int dir)
        {
            if (!CompareShown()) return;
            int r = compare.View.Step(dir);
            if (r == 0) Say(I18n.T("No hay diferencias.", "No differences."), false);
            else if (r == 2) Say(dir > 0 ? I18n.T("De vuelta en la primera diferencia.", "Back to first difference.") : I18n.T("De vuelta en la \u00FAltima diferencia.", "Back to last difference."), false);
        }

        // F7 / Shift+F7 step through the differences; Esc leaves the comparison (the find bar and text fields first).
        bool CompareKey(Key key, bool ctrl, bool shift, bool alt)
        {
            if (compare == null || !CompareShown()) return false;
            if (key == Key.F7 && !ctrl && !alt) { StepCompare(shift ? -1 : 1); return true; }
            if (key == Key.Escape && findBar == null && !(Keyboard.FocusedElement is TextBox)) { ExitCompare(); return true; }
            return false;
        }

        // Más opciones for an XML: the comparisons and the JSON copy.
        void XmlExtraEntries(List<MenuEntry> m, bool onDisk)
        {
            bool ready = xml && Current() != null && model != null && parsedText == text;
            m.Add(MenuEntry.Line());
            MenuEntry prev = MenuEntry.Item(I18n.T("Comparar con el XML anterior de la carpeta", "Compare with previous XML in folder"), "doc", false, delegate { CompareWith(null); });
            prev.Enabled = ready && onDisk && (!neighboursKnown || hasPrev);
            m.Add(prev);
            MenuEntry other = MenuEntry.Item(I18n.T("Comparar con otro archivo\u2026", "Compare with another file\u2026"), "doc", false, delegate { CompareOther(); });
            other.Enabled = ready;
            m.Add(other);
            if (compare != null) m.Add(MenuEntry.Item(I18n.T("Salir de la comparaci\u00F3n   Esc", "Exit comparison   Esc"), "close", false, delegate { ExitCompare(); }));
            m.Add(MenuEntry.Line());
            MenuEntry json = MenuEntry.Item(I18n.T("Copiar como JSON", "Copy as JSON"), "copy", false, delegate { CopyJsonFromMenu(); });
            json.Detail = JsonSelection() != null ? I18n.T("El nodo seleccionado", "Selected node") : I18n.T("Todo el documento", "Whole document");
            json.Enabled = ready && tree != null;
            m.Add(json);
        }

        void CompareOther()
        {
            string path = null;
            Func<string> pick = PickCompare;
            if (pick != null) path = pick();
            else
            {
                try
                {
                    using (System.Windows.Forms.OpenFileDialog d = new System.Windows.Forms.OpenFileDialog())
                    {
                        d.Title = I18n.T("Comparar con otro XML", "Compare with another XML");
                        d.Filter = I18n.T("XML|*.xml;*.xsd;*.config;*.svg;*.xaml|Todos los archivos|*.*", "XML|*.xml;*.xsd;*.config;*.svg;*.xaml|All files|*.*");
                        if (file != null && file.Path != null) d.InitialDirectory = System.IO.Path.GetDirectoryName(file.Path);
                        if (d.ShowDialog(Win32) == System.Windows.Forms.DialogResult.OK) path = d.FileName;
                    }
                }
                catch (Exception ex) { ShotStack.Log("Comparar: " + ex.Message); Say(I18n.T("No he podido abrir el selector de archivos.", "Could not open file picker."), true); return; }
            }
            if (path != null) CompareWith(path);
        }

        // The other file (null: the XML before this one in the folder) against the open document, as it is on screen.
        void CompareWith(string other)
        {
            if (!xml || Current() == null) return;
            if (editing) SetEditing(false);
            if (model == null || parsedText != text || treeFor != model) { Say(model == null && parsedText == text ? I18n.T("Corrige el XML antes de compararlo.", "Fix XML before comparing.") : I18n.T("Un momento: todav\u00EDa estoy leyendo el XML.", "One moment: still reading XML."), true); return; }
            string cur = file != null ? file.Path : null;
            if (other == null && cur == null) return;
            XmlModel now = model;
            string snap = text;
            int my = ++compareToken;
            comparing = true;
            fade.Stop();
            status = I18n.T("Comparando\u2026", "Comparing\u2026");
            statusBad = false;
            Stats();
            Cursor = Cursors.AppStarting;
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                string err = null, oldPath = other;
                bool soft = false, cut = now.Truncated;
                DiffResult r = null;
                Func<bool> stop = delegate { return System.Threading.Volatile.Read(ref compareToken) != my || open != this; };
                try
                {
                    if (oldPath == null)
                    {
                        oldPath = XmlWatch.Sibling(cur, -1);
                        if (oldPath == null) { err = I18n.T("No hay XML anteriores en la carpeta.", "No previous XML in folder."); soft = true; }
                    }
                    if (oldPath != null)
                    {
                        string lerr;
                        DocFile d = DocFile.Load(oldPath, out lerr);
                        if (d == null) err = lerr ?? I18n.T("No he podido leer el archivo.", "Could not read file.");
                        else
                        {
                            XmlModel om;
                            string perr;
                            int l, c;
                            if (!XmlDoc.Parse(d.Text, stop, out om, out perr, out l, out c))
                                err = perr != null ? I18n.Fmt("\u00AB{0}\u00BB no es un XML bien formado. {1}", "\u201C{0}\u201D is not well-formed XML. {1}", System.IO.Path.GetFileName(oldPath), perr) : null;
                            else
                            {
                                XmlDoc.ExpandInner(om);
                                cut |= om.Truncated;
                                r = XmlDiff.Compare(om, now, stop);
                            }
                        }
                    }
                }
                catch (Exception ex) { ShotStack.Log("XML comparar: " + ex.Message); err = I18n.T("No he podido comparar los XML.", "Could not compare XML."); r = null; }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != compareToken || open != this) return;
                        comparing = false;
                        Cursor = null;
                        status = null;
                        if (r == null) { if (err != null) Say(err, !soft); else Stats(); return; }
                        if (text != snap || model != now || editing || !xml) { Stats(); return; }
                        StartCompare(r, oldPath, cut);
                    }));
                }
                catch { }
            }, 16 * 1024 * 1024);
            t.IsBackground = true;
            t.Name = "Stackshot XML compare";
            t.Start();
        }

        void StartCompare(DiffResult r, string oldPath, bool cut)
        {
            compare = new Comparison { Text = text, Result = r, OldPath = oldPath };
            NewCompareView();
            if (previewHost != null) previewHost.Child = compare.View;
            ShowHeader();
            Stats();
            if (r.Stops.Count > 0) compare.View.Step(1);
            else compare.View.List.Focus();
            UpdateComparePos();
            if (cut) Say(I18n.Fmt("El XML es muy grande: solo se comparan sus primeros {0} nodos.", "The XML is very large: only its first {0} nodes are compared.", XmlDoc.MaxNodes.ToString("N0")), false);
        }

        internal void ExitCompare()
        {
            compareToken++;
            if (comparing) { comparing = false; Cursor = null; status = null; }
            if (compare == null) { Stats(); return; }
            compare = null;
            comparePos = null;
            compareUp = compareDown = null;
            if (previewHost != null && tree != null && model != null && treeFor == model) previewHost.Child = tree;
            ShowHeader();
            Stats();
            if (tree != null && previewHost != null && previewHost.Child == tree) tree.List.Focus();
        }

        // Expandir todo / Contraer todo act on what is on screen.
        void ExpandXml(bool open)
        {
            if (CompareShown()) { if (open) compare.View.ExpandAll(); else compare.View.CollapseAll(); return; }
            if (tree == null) return;
            if (open) tree.ExpandAll(); else tree.CollapseAll();
        }

        // Copiar como imagen and Listo picture the comparison while it is on screen.
        BitmapSource CompareImage(out string err)
        {
            return XmlDiffView.Render(compare.View.Rows, compare.View.Look, out err);
        }
    }
}
