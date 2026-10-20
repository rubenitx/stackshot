// Stackshot - The PDF viewer's sidebar: page thumbnails (several can be picked, for captures), the marks as a list to
// go back to, and the search results with the words around each one.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed partial class PdfWindow
    {
        Dictionary<int, BitmapSource> thumbs = new Dictionary<int, BitmapSource>();
        PdfThumbs thumbStrip;

        void FillSide()
        {
            if (sideHost == null) return;
            if (engine == null || !sideOpen) { sideHost.Child = null; return; }
            Palette pal = Ds.Brushes;
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            List<string> names = new List<string> { "P\u00E1ginas", "Marcas" };
            if (searchQuery.Length > 0) names.Add("Buscar");
            if (sideMode >= names.Count) sideMode = 0;
            MacSegmented seg = new MacSegmented(names.ToArray(), sideMode) { Margin = new Thickness(10, 10, 10, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            seg.Width = double.NaN;   // the width of the sidebar, shared by its segments
            seg.Changed += delegate(int i) { sideMode = i; FillSide(); };
            g.Children.Add(seg);
            FrameworkElement body;
            if (sideMode == 1) body = MarksList(pal);
            else if (sideMode == 2) body = ResultsList(pal);
            else
            {
                if (thumbStrip == null || thumbStrip.Engine != engine)
                {
                    thumbStrip = new PdfThumbs(engine, thumbs);
                    thumbStrip.Picked += delegate(int p) { view.GoTo(p, true); view.Focus(); };
                    thumbStrip.Menu += ThumbMenu;
                    thumbStrip.Drawn += delegate { view.InvalidateVisual(); };
                }
                if (thumbStrip.Parent is Panel) ((Panel)thumbStrip.Parent).Children.Remove(thumbStrip);
                thumbStrip.Current = view.CurrentPage;
                body = thumbStrip;
            }
            Grid.SetRow(body, 1);
            g.Children.Add(body);
            sideHost.Child = g;
        }

        // Pages picked in the sidebar (Ctrl or Shift + click), or the one shown.
        internal List<int> PickedPages()
        {
            List<int> o = new List<int>();
            if (thumbStrip != null && thumbStrip.Picks.Count > 1) { o.AddRange(thumbStrip.Picks); o.Sort(); return o; }
            o.Add(view.CurrentPage);
            return o;
        }

        Border proxy;

        // A menu where the pointer is (a context menu).
        void MenuAt(List<MenuEntry> m)
        {
            if (proxy == null || page == null) return;
            Point at = Mouse.GetPosition(page);
            proxy.Margin = new Thickness(at.X, at.Y, 0, 0);
            proxy.UpdateLayout();
            MacMenu.Show(proxy, m);
        }

        void ThumbMenu(int p, FrameworkElement anchor)
        {
            List<int> pages = thumbStrip != null && thumbStrip.Picks.Count > 1 && thumbStrip.Picks.Contains(p) ? PickedPages() : new List<int> { p };
            string which = pages.Count == 1 ? "esta p\u00E1gina" : "las " + pages.Count + " p\u00E1ginas";
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Capturar " + which, "camera", false, delegate { CapturePages(pages, false); }));
            if (pages.Count > 1) m.Add(MenuEntry.Item("Capturar " + which + " en una imagen", "pages", false, delegate { CapturePages(pages, true); }));
            m.Add(MenuEntry.Item("Copiar como imagen", "copy", false, delegate { CopyPageImage(pages[0]); }));
            m.Add(MenuEntry.Item("Anotar en el editor", "edit", false, delegate { Annotate(pages[0]); }));
            m.Add(MenuEntry.Item("Guardar como imagen\u2026", "save", false, delegate { SavePngs(pages); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Imprimir " + which + "\u2026", "print", false, delegate { Print(pages); }));
            MenuAt(m);
        }

        // ---- Marks

        FrameworkElement MarksList(Palette pal)
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            StackPanel list = new StackPanel { Margin = new Thickness(8, 0, 8, 12) };
            sv.Content = list;
            if (marks == null || marks.All.Count == 0)
            {
                list.Children.Add(new TextBlock
                {
                    Text = "Todav\u00EDa no hay marcas.\n\nResalta, subraya o escribe una nota (H, U, N) y aparecer\u00E1 aqu\u00ED, lista para volver a ella o copiarla como resumen.",
                    TextWrapping = TextWrapping.Wrap, Foreground = Ds.Brush(pal.Label2), FontSize = 12, Margin = new Thickness(6, 8, 6, 0)
                });
                return sv;
            }
            List<PdfMark> sorted = new List<PdfMark>(marks.All);
            sorted.Sort(delegate(PdfMark a, PdfMark b)
            {
                if (a.Page != b.Page) return a.Page.CompareTo(b.Page);
                double ya = a.Kind == PdfMarkKind.Note ? a.At.Y : a.Boxes.Count > 0 ? a.Boxes[0].Y : 0, yb = b.Kind == PdfMarkKind.Note ? b.At.Y : b.Boxes.Count > 0 ? b.Boxes[0].Y : 0;
                return ya.CompareTo(yb);
            });
            MacButton summary = new MacButton("Copiar resumen", ButtonKind.Secondary, "copy", null, 28) { Margin = new Thickness(2, 0, 2, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            summary.Click += delegate { CopySummary(); };
            list.Children.Add(summary);
            int lastPage = -1;
            foreach (PdfMark m in sorted)
            {
                if (m.Page != lastPage)
                {
                    lastPage = m.Page;
                    list.Children.Add(new TextBlock { Text = "P\u00E1gina " + (m.Page + 1), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(6, 10, 0, 4) });
                }
                list.Children.Add(MarkItem(m, pal));
            }
            return sv;
        }

        FrameworkElement MarkItem(PdfMark m, Palette pal)
        {
            bool picked = view.selected == m.Id;
            Border b = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 2), Cursor = Cursors.Hand };
            b.Background = picked ? Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.3 : 0.16)) : Brushes.Transparent;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new Border { Background = Ds.Brush(m.Color), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 0, 2) });
            StackPanel t = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(t, 1);
            string kind = m.Kind == PdfMarkKind.Highlight ? "Resaltado" : m.Kind == PdfMarkKind.Underline ? "Subrayado" : m.Kind == PdfMarkKind.Strike ? "Tachado" : m.Kind == PdfMarkKind.Note ? "Nota" : "L\u00E1piz";
            if (m.InFile) kind = m.Author.Length > 0 ? "Nota del PDF \u00B7 " + m.Author : "Nota del PDF";
            t.Children.Add(new TextBlock { Text = m.Summary, TextWrapping = TextWrapping.Wrap, MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12.5, Foreground = Ds.Brush(pal.Label) });
            if (m.OnText && m.Comment.Length > 0)
                t.Children.Add(new TextBlock { Text = m.Comment, TextWrapping = TextWrapping.Wrap, MaxHeight = 36, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, FontStyle = FontStyles.Italic, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(0, 2, 0, 0) });
            t.Children.Add(new TextBlock { Text = kind, FontSize = 11, Foreground = Ds.Brush(pal.Label3), Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(t);
            b.Child = g;
            b.MouseEnter += delegate { if (view.selected != m.Id) b.Background = Ds.Brush(pal.ControlHover); };
            b.MouseLeave += delegate { if (view.selected != m.Id) b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { GoToMark(m); };
            b.MouseRightButtonUp += delegate(object o, MouseButtonEventArgs e) { view.SelectMark(m); MarkMenu(b, m); e.Handled = true; };
            return b;
        }

        internal void GoToMark(PdfMark m)
        {
            PdfMark live = marks.Find(m.Id);
            if (live == null) return;
            Rect pr = view.PageRect(live.Page);
            view.Reveal(live.Page, live.Bounds(pr.Width, pr.Height), true);
            view.SelectMark(live);
            if (sideMode == 1) FillSide();
        }

        // The marks as Markdown, page by page: a reading summary to paste anywhere.
        internal string Summary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# ").Append(System.IO.Path.GetFileNameWithoutExtension(path)).Append("\n");
            List<PdfMark> sorted = new List<PdfMark>(marks.All);
            sorted.Sort(delegate(PdfMark a, PdfMark b) { return a.Page != b.Page ? a.Page.CompareTo(b.Page) : 0; });
            int last = -1;
            foreach (PdfMark m in sorted)
            {
                if (m.Kind == PdfMarkKind.Ink) continue;
                if (m.Page != last) { last = m.Page; sb.Append("\n## P\u00E1gina ").Append(m.Page + 1).Append("\n\n"); }
                if (m.Kind == PdfMarkKind.Note) { sb.Append("- \U0001F4DD ").Append(OneLine(m.Text)).Append('\n'); continue; }
                string words = OneLine(m.Text);
                if (words.Length == 0) words = "(zona marcada)";
                if (m.Kind == PdfMarkKind.Strike) sb.Append("- ~~").Append(words).Append("~~");
                else if (m.Kind == PdfMarkKind.Underline) sb.Append("- <u>").Append(words).Append("</u>");
                else sb.Append("- \u00AB").Append(words).Append("\u00BB");
                if (m.Comment.Length > 0) sb.Append(" \u2014 ").Append(OneLine(m.Comment));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static string OneLine(string s) { return string.Join(" ", (s ?? "").Split(new char[] { '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)); }

        void CopySummary()
        {
            if (marks == null || marks.All.Count == 0) { Say("No hay marcas que copiar.", false); return; }
            if (ToClipboard(delegate { Clipboard.SetText(Summary()); })) Say("Resumen copiado (Markdown)", false);
        }

        // ---- Search results

        FrameworkElement ResultsList(Palette pal)
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            StackPanel list = new StackPanel { Margin = new Thickness(8, 0, 8, 12) };
            sv.Content = list;
            string state = view.Hits.Count == 0 ? (reader != null && reader.Busy ? "Buscando\u2026" : "Sin resultados") : view.Hits.Count.ToString("N0") + (view.Hits.Count == 1 ? " resultado" : " resultados");
            list.Children.Add(new TextBlock { Text = state, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(6, 2, 0, 6) });
            int shown = 0;
            for (int i = 0; i < view.Hits.Count && shown < 400; i++, shown++) list.Children.Add(HitItem(i, pal));
            return sv;
        }

        FrameworkElement HitItem(int i, Palette pal)
        {
            PdfHitPlace h = view.Hits[i];
            PdfPageText t = reader.Get(h.Page);
            Border b = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 5, 8, 5), Cursor = Cursors.Hand };
            b.Background = i == view.HitIndex ? Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.3 : 0.16)) : Brushes.Transparent;
            StackPanel s = new StackPanel();
            s.Children.Add(new TextBlock { Text = "P\u00E1gina " + (h.Page + 1), FontSize = 11, Foreground = Ds.Brush(pal.Label3) });
            TextBlock line = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12.5, Foreground = Ds.Brush(pal.Label2) };
            if (t != null)
            {
                int a = Math.Max(0, h.First - 5), z = Math.Min(t.Words.Count - 1, h.Last + 6);
                if (a > 0) line.Inlines.Add(new Run("\u2026"));
                if (a < h.First) line.Inlines.Add(new Run(t.Between(a, h.First - 1).Replace('\n', ' ') + " "));
                line.Inlines.Add(new Run(t.Between(h.First, h.Last).Replace('\n', ' ')) { FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(pal.Label) });
                if (h.Last < z) line.Inlines.Add(new Run(" " + t.Between(h.Last + 1, z).Replace('\n', ' ')));
                if (z < t.Words.Count - 1) line.Inlines.Add(new Run("\u2026"));
            }
            s.Children.Add(line);
            b.Child = s;
            int at = i;
            b.MouseEnter += delegate { if (view.HitIndex != at) b.Background = Ds.Brush(pal.ControlHover); };
            b.MouseLeave += delegate { if (view.HitIndex != at) b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { ShowHit(at); FillSide(); };
            return b;
        }
    }

    // The column of page thumbnails: drawn only as they come into view, the page shown ringed, several picked with
    // Ctrl or Shift for captures and exports.
    public sealed class PdfThumbs : FrameworkElement
    {
        const double W = 120, LabelH = 22, GapY = 10, Top = 6;
        public readonly PdfEngine Engine;
        readonly Dictionary<int, BitmapSource> thumbs;
        readonly HashSet<int> asked = new HashSet<int>();
        public readonly List<int> Picks = new List<int>();
        double oy;
        double[] tops;
        double total;
        int current;
        int anchor = -1;

        public event Action<int> Picked;
        public event Action<int, FrameworkElement> Menu;
        public event Action Drawn;   // a new thumbnail (the pages use them while the sharp one is coming)

        public PdfThumbs(PdfEngine engine, Dictionary<int, BitmapSource> thumbs)
        {
            Engine = engine;
            this.thumbs = thumbs;
            ClipToBounds = true;
            Focusable = false;
            Layout();
        }

        void Layout()
        {
            tops = new double[Engine.Pages];
            double y = Top;
            for (int i = 0; i < Engine.Pages; i++)
            {
                tops[i] = y;
                y += TallOf(i) + LabelH + GapY;
            }
            total = y;
        }

        double TallOf(int i) { Size s = Engine.Sizes[i]; return W * s.Height / Math.Max(1, s.Width); }

        Rect ThumbRect(int i)
        {
            double h = TallOf(i);
            double w = W;
            // Landscape pages keep the same width; very tall ones are narrowed so a column of them stays readable.
            if (h > W * 1.6) { w = W * 1.6 * W / h; h = W * 1.6; }
            return new Rect(Math.Round((ActualWidth - w) / 2), Math.Round(tops[i] - oy), Math.Round(w), Math.Round(h));
        }

        public int Current
        {
            get { return current; }
            set
            {
                if (value == current) return;
                current = value;
                if (Picks.Count <= 1) { Picks.Clear(); Picks.Add(value); }
                Reveal(value);
                InvalidateVisual();
            }
        }

        void Reveal(int i)
        {
            if (tops == null || i < 0 || i >= tops.Length || ActualHeight <= 0) return;
            double y = tops[i], h = TallOf(i) + LabelH;
            if (y < oy + 4) oy = y - 8;
            else if (y + h > oy + ActualHeight - 4) oy = y + h - ActualHeight + 8;
            oy = Math.Max(0, Math.Min(Math.Max(0, total - ActualHeight), oy));
        }

        int At(Point p)
        {
            for (int i = 0; i < tops.Length; i++)
            {
                Rect r = ThumbRect(i);
                r.Height += LabelH;
                r.Inflate(8, 4);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnRender(DrawingContext dc)
        {
            Palette pal = Ds.Brushes;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            double d;
            try { d = VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { d = 1; }
            for (int i = 0; i < tops.Length; i++)
            {
                if (tops[i] + TallOf(i) + LabelH < oy - 200) continue;
                if (tops[i] > oy + ActualHeight + 200) break;
                Rect r = ThumbRect(i);
                bool picked = Picks.Contains(i) && Picks.Count > 1, now = i == current;
                if (now || picked)
                {
                    Rect ring = r;
                    ring.Inflate(5, 5);
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.WithAlpha(pal.Accent, picked ? 0.35 : 0.22)), now ? new Pen(Ds.Brush(pal.Accent), 2) : null, ring, 6, 6);
                }
                dc.DrawRectangle(Ds.Brush(Color.FromArgb(pal.Dark ? (byte)60 : (byte)22, 0, 0, 0)), null, new Rect(r.X - 0.5, r.Y + 0.5, r.Width + 1, r.Height + 1.5));
                dc.DrawRectangle(Brushes.White, null, r);
                BitmapSource b;
                if (thumbs.TryGetValue(i, out b)) dc.DrawImage(b, r);
                else Ask(i, (int)Math.Round(r.Width * d), (int)Math.Round(r.Height * d));
                FormattedText n = Ink.Text((i + 1).ToString("N0"), now ? Ds.Semibold : Ds.Regular, 11.5, now ? pal.Label : pal.Label2);
                dc.DrawText(n, new Point(Math.Round((ActualWidth - n.Width) / 2), r.Bottom + 4));
            }
        }

        void Ask(int i, int w, int h)
        {
            if (asked.Contains(i) || w < 1 || h < 1) return;
            asked.Add(i);
            // Twice the size: the pages use the thumbnail as a stand-in while the sharp drawing is on its way.
            int k = 2;
            Engine.Render(i, w * k, h * k, null, 6, "t" + i, Dispatcher, delegate(BitmapSource b)
            {
                asked.Remove(i);
                if (b == null) return;
                thumbs[i] = b;
                InvalidateVisual();
                Action h2 = Drawn;
                if (h2 != null) h2();
            });
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            oy = Math.Max(0, Math.Min(Math.Max(0, total - ActualHeight), oy - e.Delta * 0.8));
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            int i = At(e.GetPosition(this));
            if (i < 0) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (ctrl)
            {
                if (Picks.Count <= 1 && !Picks.Contains(current)) Picks.Add(current);
                if (Picks.Contains(i) && Picks.Count > 1) Picks.Remove(i); else if (!Picks.Contains(i)) Picks.Add(i);
                anchor = i;
                InvalidateVisual();
                return;
            }
            if (shift && (anchor >= 0 || current >= 0))
            {
                int a = anchor >= 0 ? anchor : current;
                Picks.Clear();
                for (int k = Math.Min(a, i); k <= Math.Max(a, i); k++) Picks.Add(k);
                InvalidateVisual();
                return;
            }
            Picks.Clear();
            Picks.Add(i);
            anchor = i;
            current = i;
            InvalidateVisual();
            Action<int> h = Picked;
            if (h != null) h(i);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            int i = At(e.GetPosition(this));
            if (i < 0) return;
            Action<int, FrameworkElement> h = Menu;
            if (h != null) h(i, this);
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            Reveal(current);
        }
    }
}
