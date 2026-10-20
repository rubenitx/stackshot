// Stackshot - The PDF viewer's tools: search through the text, the bar that floats over a selection (copy, mark,
// capture), notes, colours and the menus of the toolbar.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed partial class PdfWindow
    {
        // ---- Search

        string searchQuery = "";
        DispatcherTimerLite searchLaterLite;
        DispatcherTimerLite searchLater { get { if (searchLaterLite == null) searchLaterLite = new DispatcherTimerLite(220, RunSearch); return searchLaterLite; } }

        void RunSearch()
        {
            string q = search == null ? "" : search.Text.Trim();
            bool was = searchQuery.Length > 0;
            searchQuery = q;
            view.Hits.Clear();
            view.HitIndex = -1;
            if (q.Length == 0 || reader == null)
            {
                if (search != null) search.SetCount(-1, 0);
                if (sideMode == 2) sideMode = 0;
                if (was) FillSide();
                view.InvalidateVisual();
                CloseSay();
                return;
            }
            if (reader.Error != null) { Say(reader.Error, true); return; }
            for (int p = 0; p < reader.Count; p++) AddHits(p);
            reader.WantAll(view.CurrentPage);
            Progress();
            JumpNear();
            if (sideOpen) { sideMode = 2; FillSide(); }
            view.InvalidateVisual();
        }

        // A page just read: its matches join the list, in page order.
        void SearchPage(int p)
        {
            int before = view.Hits.Count;
            AddHits(p);
            if (view.Hits.Count != before)
            {
                if (view.HitIndex < 0) JumpNear();
                else Count();
                if (sideMode == 2) FillSide();
            }
            Progress();
        }

        void AddHits(int p)
        {
            PdfPageText t = reader.Get(p);
            if (t == null) return;
            if (view.Hits.Exists(delegate(PdfHitPlace h) { return h.Page == p; })) return;
            List<int[]> found = t.Find(searchQuery);
            if (found.Count == 0) return;
            int at = view.Hits.FindIndex(delegate(PdfHitPlace h) { return h.Page > p; });
            if (at < 0) at = view.Hits.Count;
            List<PdfHitPlace> add = new List<PdfHitPlace>();
            foreach (int[] f in found) add.Add(new PdfHitPlace(p, f[0], f[1]));
            view.Hits.InsertRange(at, add);
            if (view.HitIndex >= at) view.HitIndex += add.Count;
        }

        // How far the reading of the pages has gone, while a search waits for it.
        void Progress()
        {
            if (searchQuery.Length == 0 || reader == null) return;
            int done = reader.ReadCount, all = reader.Count;
            if (done < all && reader.Busy) Say("Buscando \u00AB" + searchQuery + "\u00BB\u2026 " + done + " de " + all + " p\u00E1ginas le\u00EDdas", false, true);
            else if (view.Hits.Count == 0) Say("No aparece \u00AB" + searchQuery + "\u00BB en el documento.", false);
            else CloseSay();
            Count();
        }

        // The first match at or after the page shown.
        void JumpNear()
        {
            if (view.Hits.Count == 0) { Count(); return; }
            int now = view.CurrentPage, pick = 0;
            for (int i = 0; i < view.Hits.Count; i++) if (view.Hits[i].Page >= now) { pick = i; break; }
            ShowHit(pick);
        }

        void StepHit(int d)
        {
            if (view.Hits.Count == 0) return;
            int i = view.HitIndex < 0 ? 0 : (view.HitIndex + d + view.Hits.Count) % view.Hits.Count;
            ShowHit(i);
            if (sideMode == 2) FillSide();
        }

        internal void ShowHit(int i)
        {
            if (i < 0 || i >= view.Hits.Count) return;
            view.HitIndex = i;
            PdfHitPlace h = view.Hits[i];
            PdfPageText t = reader.Get(h.Page);
            if (t != null)
            {
                Rect u = Rect.Empty;
                foreach (Rect b in t.LineBoxes(h.First, h.Last)) u = u.IsEmpty ? b : Rect.Union(u, b);
                view.Reveal(h.Page, u, true);
            }
            view.InvalidateVisual();
            Count();
        }

        void Count() { if (search != null) search.SetCount(view.HitIndex, view.Hits.Count); }

        // ---- The bar over a selection

        Popup bar;
        bool barSoon;

        void OnSelection()
        {
            if (sideMode == 1) FillSide();
            if (view.HasText || view.HasZone || view.SelectedMark != null) ShowBar();
            else CloseBar();
        }

        void ShowBarSoon()
        {
            if (barSoon) return;
            barSoon = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { barSoon = false; if (view.SelectedMark != null) ShowBar(); else if (!view.HasText && !view.HasZone) CloseBar(); }));
        }

        void ShowBar()
        {
            CloseBar();
            Palette pal = Ds.Brushes;
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            PdfMark m = view.SelectedMark;
            if (view.HasText)
            {
                row.Children.Add(BarButton("Copiar", "copy", delegate { Copy(); }));
                Sep(row);
                row.Children.Add(BarSwatch(view.Mark, "Resaltar", delegate { view.MarkWords(PdfMarkKind.Highlight); }));
                row.Children.Add(BarIcon("underline", "Subrayar", delegate { view.MarkWords(PdfMarkKind.Underline); }));
                row.Children.Add(BarIcon("strike", "Tachar", delegate { view.MarkWords(PdfMarkKind.Strike); }));
                row.Children.Add(BarIcon("sticky", "Resaltar con una nota", delegate { PdfMark x = view.MarkWords(PdfMarkKind.Highlight); if (x != null) { view.SelectMark(marks.Find(x.Id)); OpenNote(marks.Find(x.Id), true); } }));
                Sep(row);
                row.Children.Add(BarIcon("search", "Buscar en el documento", delegate { string s = view.SelectedText; search.Text = s.Length > 80 ? s.Substring(0, 80) : s; RunSearch(); }));
            }
            else if (view.HasZone)
            {
                row.Children.Add(BarButton("Capturar", "camera", delegate { CaptureZone(); }));
                row.Children.Add(BarIcon("copy", "Copiar como imagen", delegate { CopyZone(); }));
                row.Children.Add(BarIcon("textscan", "Copiar el texto", delegate { CopyZoneText(); }));
                row.Children.Add(BarIcon("edit", "Anotar en el editor", delegate { AnnotateZone(); }));
                Sep(row);
                row.Children.Add(BarSwatch(view.Mark, "Resaltar la zona", delegate { view.MarkZone(PdfMarkKind.Highlight); }));
            }
            else if (m != null && m.InFile)
            {
                // A note that is part of the PDF: read and copied here, changed with the app that put it there.
                row.Children.Add(BarButton("Ver la nota", "sticky", delegate { OpenNote(view.SelectedMark, false); }));
                if (m.Text.Length > 0) row.Children.Add(BarIcon("copy", "Copiar el texto", delegate { Copy(); }));
            }
            else if (m != null)
            {
                for (int i = 0; i < PdfMarks.Colors.Length; i++)
                {
                    Color c = PdfMarks.Colors[i];
                    row.Children.Add(BarSwatch(c, PdfMarks.ColorNames[i], delegate { Recolor(c); }, m.Color == c));
                }
                Sep(row);
                if (m.Kind == PdfMarkKind.Note || m.OnText)
                    row.Children.Add(BarIcon("sticky", m.Kind == PdfMarkKind.Note ? "Editar la nota" : m.Comment.Length > 0 ? "Editar la nota" : "A\u00F1adir una nota", delegate { OpenNote(view.SelectedMark, false); }));
                if (m.OnText && m.Text.Length > 0) row.Children.Add(BarIcon("copy", "Copiar el texto", delegate { Copy(); }));
                row.Children.Add(BarIcon("trash", "Eliminar (Supr)", delegate { DeleteSelected(); }));
            }
            Border card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ds.Brush(pal.Dark ? Color.FromRgb(44, 44, 46) : Color.FromRgb(255, 255, 255)),
                BorderBrush = Ds.Brush(pal.Hairline),
                BorderThickness = new Thickness(1),
                Child = row,
                Effect = null
            };
            Grid holder = new Grid { Margin = new Thickness(10) };
            holder.Children.Add(new SoftShadow(10, 6, 0.6, pal.Dark ? 0.5 : 0.16));
            holder.Children.Add(card);
            bar = new Popup { Child = holder, AllowsTransparency = true, PlacementTarget = view, Placement = PlacementMode.Relative, StaysOpen = true, Focusable = false };
            holder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PlaceBar();
            bar.IsOpen = true;
        }

        // Over the selection, or under it when there is no room above; it follows the pages as they scroll.
        void PlaceBar()
        {
            if (bar == null) return;
            Rect sel = view.SelectionRect;
            if (sel.IsEmpty || sel.Bottom < 0 || sel.Top > view.ActualHeight) { bar.IsOpen = false; return; }
            Size s = ((FrameworkElement)bar.Child).DesiredSize;
            double x = Math.Max(0, Math.Min(view.ActualWidth - s.Width, sel.X + sel.Width / 2 - s.Width / 2));
            double y = sel.Top - s.Height + 4;
            if (y < 0) y = sel.Bottom - 6;
            bar.HorizontalOffset = x;
            bar.VerticalOffset = y;
            if (!bar.IsOpen) bar.IsOpen = true;
        }

        void CloseBar()
        {
            if (bar == null) return;
            bar.IsOpen = false;
            bar = null;
        }

        // A popup stays where it was put on screen: nudged, it finds its place over the window again.
        static void Nudge(Popup p)
        {
            if (p == null || !p.IsOpen) return;
            double h = p.HorizontalOffset;
            p.HorizontalOffset = h + 0.5;
            p.HorizontalOffset = h;
        }

        static void Sep(Panel p) { p.Children.Add(new Border { Width = 1, Margin = new Thickness(4, 6, 4, 6), Background = Ds.Brush(Ds.Brushes.Separator) }); }

        FrameworkElement BarButton(string label, string icon, Action act)
        {
            MacButton b = new MacButton(label, ButtonKind.Plain, icon, null, 30);
            b.Click += delegate { act(); };
            return b;
        }

        FrameworkElement BarIcon(string icon, string tip, Action act)
        {
            DocIconButton b = new DocIconButton(icon, tip, 30);
            b.Click += delegate { act(); };
            return b;
        }

        // A mark's menu in the sidebar list: the same choices as its floating bar.
        void MarkMenu(FrameworkElement anchor, PdfMark m)
        {
            List<MenuEntry> e = new List<MenuEntry>();
            e.Add(MenuEntry.Item("Ir a la marca", "right", false, delegate { GoToMark(m); }));
            if (m.OnText && m.Text.Length > 0) e.Add(MenuEntry.Item("Copiar el texto", "copy", false, delegate { if (ToClipboard(delegate { Clipboard.SetText(m.Text); })) Say("Texto copiado", false); }));
            if (m.Kind == PdfMarkKind.Note || m.OnText)
                e.Add(MenuEntry.Item(m.Kind == PdfMarkKind.Note || m.Comment.Length > 0 ? "Editar la nota" : "A\u00F1adir una nota", "sticky", false, delegate { GoToMark(m); OpenNote(marks.Find(m.Id), false); }));
            e.Add(MenuEntry.Line());
            for (int i = 0; i < PdfMarks.Colors.Length; i++)
            {
                Color c = PdfMarks.Colors[i];
                e.Add(MenuEntry.Item(PdfMarks.ColorNames[i], null, m.Color == c, delegate { view.SelectMark(marks.Find(m.Id)); Recolor(c); }));
            }
            e.Add(MenuEntry.Line());
            e.Add(MenuEntry.Item("Eliminar", "trash", false, delegate { view.SelectMark(marks.Find(m.Id)); DeleteSelected(); }));
            MacMenu.Show(anchor, e);
        }

        FrameworkElement BarSwatch(Color c, string tip, Action act) { return BarSwatch(c, tip, act, false); }

        FrameworkElement BarSwatch(Color c, string tip, Action act, bool on)
        {
            ColorWell w = new ColorWell(c) { ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(w, tip);
            w.Click += delegate { act(); };
            if (!on) return w;
            Grid g = new Grid();
            g.Children.Add(w);
            g.Children.Add(new System.Windows.Shapes.Ellipse { Width = 22, Height = 22, Stroke = Ds.Brush(Ds.Brushes.Accent), StrokeThickness = 2, IsHitTestVisible = false });
            return g;
        }

        void Recolor(Color c)
        {
            PdfMark m = view.SelectedMark;
            if (m == null || m.InFile) return;
            PdfMark x = m.Clone();
            x.Color = c;
            marks.Update(x);
            if (m.Kind == PdfMarkKind.Ink) { view.Ink = lastInk = c; } else { view.Mark = lastMark = c; }
            SyncTools();
            ShowBar();
        }

        internal void DeleteSelected()
        {
            PdfMark m = view.SelectedMark;
            if (m == null) return;
            if (m.InFile) { Say("Esta nota es parte del PDF: se cambia con la app que la puso.", false); return; }
            CloseNote();
            marks.Remove(m);
            view.ClearSelection(true);
            Say("Marca eliminada \u00B7 Ctrl+Z la recupera", false);
        }

        // ---- Copy

        internal void Copy()
        {
            if (view.HasZone) { CopyZone(); return; }
            string t = view.SelectedText;
            if (t.Length == 0)
            {
                Say(reader != null && reader.Error != null ? reader.Error : "Selecciona texto o una zona (R) para copiar.", false);
                return;
            }
            if (ToClipboard(delegate { Clipboard.SetText(t); })) Say("Texto copiado", false);
        }

        void CopyZoneText()
        {
            string t = view.SelectedText;
            if (t.Length > 0) { if (ToClipboard(delegate { Clipboard.SetText(t); })) Say("Texto copiado", false); return; }
            PdfPageText pt = reader.Get(view.zonePage);
            if (pt == null) { reader.Want(view.zonePage); Say("Leyendo el texto de la p\u00E1gina\u2026 vuelve a probar en un momento.", false); return; }
            Say("No hay texto que se pueda leer en esa zona.", false);
        }

        // Retried a few times: another program may hold the clipboard for a moment.
        internal bool ToClipboard(Action act)
        {
            for (int i = 0; i < 5; i++)
            {
                try { act(); return true; }
                catch (System.Runtime.InteropServices.ExternalException) { System.Threading.Thread.Sleep(40); }
                catch (Exception ex) { ShotStack.Log("PDF, portapapeles: " + ex.Message); break; }
            }
            Say("No he podido usar el portapapeles.", true);
            return false;
        }

        // ---- Notes

        Popup note;
        PdfMark noting;
        TextBox noteBox;
        bool noteNew;

        void OpenNote(PdfMark m, bool isNew)
        {
            CloseNote();
            if (m == null) return;
            CloseBar();
            Palette pal = Ds.Brushes;
            noting = m;
            noteNew = isNew;
            bool onText = m.OnText;
            Color tint = m.Color;
            Border card = new Border
            {
                Width = 260,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Mix(tint, pal.Dark ? Color.FromRgb(40, 40, 42) : Colors.White, pal.Dark ? 0.82 : 0.78)),
                BorderBrush = Ds.Brush(Ds.WithAlpha(tint, 0.9)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 10, 10)
            };
            StackPanel s = new StackPanel();
            DockPanel head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DocIconButton del = new DocIconButton("trash", onText ? "Quitar la nota" : "Eliminar la nota", 24);
            del.Click += delegate
            {
                PdfMark x = marks.Find(m.Id);
                if (x == null) { CloseNote(); return; }
                if (onText) { PdfMark y = x.Clone(); y.Comment = ""; noteNew = false; CloseNote(); marks.Update(y); }
                else { noteNew = false; CloseNote(); marks.Remove(x); view.ClearSelection(true); }
            };
            DockPanel.SetDock(del, Dock.Right);
            if (!m.InFile) head.Children.Add(del);
            string who = m.InFile ? (m.Author.Length > 0 ? m.Author : "Nota del PDF") : onText ? "Nota sobre lo marcado" : "Nota";
            head.Children.Add(new TextBlock
            {
                Text = who + (m.When > DateTime.MinValue ? " \u00B7 " + m.When.ToString("d MMM yyyy, HH:mm", new CultureInfo("es-ES")) : ""),
                FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            });
            s.Children.Add(head);
            noteBox = new TextBox
            {
                Text = onText ? m.Comment : m.Text,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = m.InFile ? 30 : 70, MaxHeight = 260,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0),
                Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label), FontSize = 13.5,
                SpellCheck = { IsEnabled = !m.InFile }, IsReadOnly = m.InFile
            };
            noteBox.Language = System.Windows.Markup.XmlLanguage.GetLanguage("es-ES");
            noteBox.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)) { CloseNote(); view.Focus(); e.Handled = true; }
            };
            s.Children.Add(noteBox);
            s.Children.Add(new TextBlock { Text = m.InFile ? "Es parte del PDF: se cambia con la app que la puso." : "Ctrl+Intro o Esc para terminar", FontSize = 11, Foreground = Ds.Brush(pal.Label3), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
            card.Child = s;
            Grid holder = new Grid { Margin = new Thickness(10) };
            holder.Children.Add(new SoftShadow(10, 8, 0.6, pal.Dark ? 0.5 : 0.18));
            holder.Children.Add(card);
            note = new Popup { Child = holder, AllowsTransparency = true, PlacementTarget = view, Placement = PlacementMode.Relative, StaysOpen = false, Focusable = true };
            note.Closed += delegate { if (note != null) CloseNote(); };
            Rect sel = view.SelectionRect;
            if (sel.IsEmpty) sel = new Rect(view.ActualWidth / 2, view.ActualHeight / 3, 1, 1);
            double x0 = Math.Max(0, Math.Min(view.ActualWidth - 290, sel.Right + 6)), y0 = Math.Max(0, Math.Min(view.ActualHeight - 200, sel.Top - 14));
            note.HorizontalOffset = x0;
            note.VerticalOffset = y0;
            note.IsOpen = true;
            noteBox.Focus();
            noteBox.CaretIndex = noteBox.Text.Length;
        }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        // Saves what was written (a new, empty note on its own goes away again).
        void CloseNote()
        {
            if (note == null) return;
            Popup p = note;
            note = null;
            p.IsOpen = false;
            PdfMark m = noting == null || marks == null ? null : marks.Find(noting.Id);
            string text = noteBox == null ? "" : noteBox.Text.TrimEnd();
            noting = null;
            noteBox = null;
            if (m == null || m.InFile) return;
            if (m.Kind == PdfMarkKind.Note)
            {
                if (text.Length == 0 && noteNew) { marks.Remove(m); view.ClearSelection(true); return; }
                if (text == m.Text) return;
                PdfMark x = m.Clone();
                x.Text = text;
                marks.Update(x);
            }
            else if (text != m.Comment)
            {
                PdfMark x = m.Clone();
                x.Comment = text;
                marks.Update(x);
            }
        }

        // ---- Menus

        void ColorMenu(FrameworkElement anchor)
        {
            bool ink = view.Tool == PdfTool.Ink;
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title(ink ? "Color del l\u00E1piz" : "Color de las marcas"));
            for (int i = 0; i < PdfMarks.Colors.Length; i++)
            {
                Color c = PdfMarks.Colors[i];
                bool on = (ink ? view.Ink : view.Mark) == c;
                m.Add(MenuEntry.Item(PdfMarks.ColorNames[i], null, on, delegate
                {
                    if (ink) view.Ink = lastInk = c; else view.Mark = lastMark = c;
                    if (view.SelectedMark != null) Recolor(c);
                    SyncTools();
                }));
            }
            if (ink)
            {
                m.Add(MenuEntry.Line());
                m.Add(MenuEntry.Title("Grosor"));
                double[] widths = { 0.0025, 0.004, 0.008 };
                string[] names = { "Fino", "Medio", "Grueso" };
                for (int i = 0; i < widths.Length; i++)
                {
                    double w = widths[i];
                    m.Add(MenuEntry.Item(names[i], null, Math.Abs(view.InkWidth - w) < 1e-6, delegate { view.InkWidth = w; }));
                }
            }
            MacMenu.Show(anchor, m);
        }

        void ZoomMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Ajustar al ancho  (Ctrl+0)", "fitwidth", view.Fit == 1, delegate { view.SetFit(1); }));
            m.Add(MenuEntry.Item("P\u00E1gina entera", "doc", view.Fit == 2, delegate { view.SetFit(2); }));
            m.Add(MenuEntry.Item("Tama\u00F1o real  (Ctrl+1)", "actualsize", view.Fit == 0 && Math.Abs(view.Zoom - 1) < 0.001, delegate { view.SetZoom(1, null); }));
            m.Add(MenuEntry.Line());
            foreach (double z in new double[] { 0.5, 0.75, 1.25, 1.5, 2, 3, 4 })
            {
                double v = z;
                m.Add(MenuEntry.Item(Math.Round(z * 100) + " %", null, view.Fit == 0 && Math.Abs(view.Zoom - z) < 0.001, delegate { view.SetZoom(v, null); }));
            }
            MacMenu.Show(anchor, m);
        }

        void ShareMenu(FrameworkElement anchor)
        {
            List<int> pages = PickedPages();
            string which = pages.Count == 1 ? "esta p\u00E1gina" : "las " + pages.Count + " p\u00E1ginas elegidas";
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Capturar " + which, "camera", false, delegate { CapturePages(pages, false); }));
            m.Add(MenuEntry.Item("Capturar p\u00E1ginas\u2026", "pages", false, delegate { CaptureDialog(); }));
            m.Add(MenuEntry.Item("Copiar la p\u00E1gina como imagen", "copy", false, delegate { CopyPageImage(view.CurrentPage); }));
            m.Add(MenuEntry.Item("Anotar la p\u00E1gina en el editor", "edit", false, delegate { Annotate(view.CurrentPage); }));
            m.Add(MenuEntry.Item("Guardar p\u00E1ginas como imagen\u2026", "save", false, delegate { SaveDialog(); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Imprimir\u2026  (Ctrl+P)", "print", false, delegate { Print(); }));
            m.Add(MenuEntry.Line());
            MenuEntry export = MenuEntry.Item("Exportar PDF con las marcas\u2026  (Ctrl+May\u00FAs+S)", "share", false, delegate { ExportPdf(); });
            export.Enabled = marks != null && marks.Mine > 0;
            m.Add(export);
            MenuEntry into = MenuEntry.Item("Guardar las marcas dentro de este PDF\u2026", "save", false, delegate { SaveIntoPdf(); });
            into.Enabled = marks != null && marks.Mine > 0;
            m.Add(into);
            MacMenu.Show(anchor, m);
        }

        void MoreMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Abrir otro PDF\u2026  (Ctrl+O)", "open", false, Browse));
            if (engine != null)
            {
                m.Add(MenuEntry.Item("Ir a la p\u00E1gina\u2026  (Ctrl+G)", "doc", false, GoToPrompt));
                m.Add(MenuEntry.Item("Mostrar en la carpeta", "folder", false, ShowInFolder));
                m.Add(MenuEntry.Item("Abrir con la app de Windows", "open", false, OpenWithWindows));
                m.Add(MenuEntry.Line());
                MenuEntry sum = MenuEntry.Item("Copiar resumen de las marcas", "copy", false, CopySummary);
                sum.Enabled = marks != null && marks.All.Count > 0;
                m.Add(sum);
                MenuEntry md = MenuEntry.Item("Abrir el resumen como Markdown", "markdown", false, OpenSummary);
                md.Enabled = sum.Enabled;
                m.Add(md);
                MenuEntry undo = MenuEntry.Item("Deshacer  (Ctrl+Z)", "undo", false, delegate { marks.Undo(); });
                undo.Enabled = marks != null && marks.CanUndo;
                m.Add(undo);
                MenuEntry clear = MenuEntry.Item("Borrar todas las marcas\u2026", "trash", false, ClearMarks);
                clear.Enabled = marks != null && marks.Mine > 0;
                m.Add(clear);
                m.Add(MenuEntry.Line());
                m.Add(MenuEntry.Item("Teclas del visor", "keyboard", false, ShowKeys));
            }
            MacMenu.Show(anchor, m);
        }

        void ShowInFolder()
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { ShotStack.Log("PDF, carpeta: " + ex.Message); }
        }

        void OpenWithWindows()
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { ShotStack.Log("PDF, abrir: " + ex.Message); Say("Windows no tiene otra app para abrirlo.", true); }
        }

        void OpenSummary()
        {
            try { MarkdownWindow.OpenText(owner, Summary(), "Marcas de " + System.IO.Path.GetFileNameWithoutExtension(path) + ".md"); }
            catch (Exception ex) { ShotStack.Log("PDF, resumen: " + ex.Message); }
        }

        void ClearMarks()
        {
            int n = marks.Mine;
            if (DocAlert.Ask(this, "\u00BFBorrar las " + n + " marcas?", "Se quitan de este documento. Ctrl+Z las recupera mientras siga abierto.", new string[] { "Borrar", "Cancelar" }, 1, 1) != 0) return;
            CloseNote();
            view.ClearSelection(true);
            marks.Clear();
        }

        void ShowKeys()
        {
            DocAlert.Ask(this, "Teclas del visor",
                "V seleccionar \u00B7 H resaltar \u00B7 U subrayar \u00B7 K tachar \u00B7 N nota \u00B7 P l\u00E1piz \u00B7 R zona\n" +
                "Ctrl+F buscar \u00B7 F3 siguiente \u00B7 Ctrl+G ir a la p\u00E1gina \u00B7 Ctrl+A el texto de la p\u00E1gina\n" +
                "Ctrl+C copiar \u00B7 Ctrl+Z deshacer \u00B7 Supr borrar la marca \u00B7 Esc soltar\n" +
                "Ctrl+rueda o Ctrl+\u00B1 zoom \u00B7 Ctrl+0 al ancho \u00B7 Ctrl+1 tama\u00F1o real\n" +
                "Ctrl+P imprimir \u00B7 Ctrl+May\u00FAs+S exportar con marcas \u00B7 Ctrl+Alt+S barra lateral\n" +
                "Doble clic: una palabra \u00B7 triple: la l\u00EDnea \u00B7 rueda del rat\u00F3n o Espacio+arrastrar: mover",
                new string[] { "Vale" }, 0, 0);
        }

        // ---- Go to a page

        void GoToPrompt()
        {
            if (engine == null) return;
            Palette pal = Ds.Brushes;
            TextBox box = new TextBox { Width = 70, FontSize = 14, Text = (view.CurrentPage + 1).ToString(), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label), VerticalContentAlignment = VerticalAlignment.Center };
            StackPanel s = new StackPanel { Orientation = Orientation.Horizontal };
            s.Children.Add(new TextBlock { Text = "P\u00E1gina", FontSize = 13, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            Border field = new Border { CornerRadius = new CornerRadius(6), Background = Ds.Brush(pal.Control), Padding = new Thickness(6, 3, 6, 3), Child = box };
            s.Children.Add(field);
            s.Children.Add(new TextBlock { Text = "de " + engine.Pages, FontSize = 13, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            Border card = new Border { CornerRadius = new CornerRadius(10), Background = Ds.Brush(pal.Dark ? Color.FromRgb(44, 44, 46) : Colors.White), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 14, 10), Child = s };
            Grid holder = new Grid { Margin = new Thickness(10) };
            holder.Children.Add(new SoftShadow(10, 6, 0.6, pal.Dark ? 0.5 : 0.16));
            holder.Children.Add(card);
            Popup p = new Popup { Child = holder, AllowsTransparency = true, PlacementTarget = view, Placement = PlacementMode.Relative, StaysOpen = false, HorizontalOffset = Math.Max(0, view.ActualWidth / 2 - 110), VerticalOffset = 12 };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { p.IsOpen = false; e.Handled = true; }
                else if (e.Key == Key.Enter)
                {
                    int n;
                    if (int.TryParse(box.Text.Trim(), out n)) view.GoTo(n - 1, true);
                    p.IsOpen = false;
                    view.Focus();
                    e.Handled = true;
                }
            };
            p.IsOpen = true;
            box.Focus();
            box.SelectAll();
        }
    }
}
