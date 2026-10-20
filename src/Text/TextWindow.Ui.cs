// Stackshot - The text viewer's layout: the toolbar (views, search, levels, live, wrap), the status line, menus, copying
// and turning what is shown into a capture.
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public sealed partial class TextWindow
    {
        void Build()
        {
            Palette pal = Ds.Brushes;
            if (page != null) Root.Children.Remove(page);
            Root.Background = Ds.Brush(pal.Window);
            page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            Root.Children.Add(page);
            KeepCaptionsOnTop();
            page.Children.Add(Toolbar(pal));
            bannerHost = new Border();
            Grid.SetRow(bannerHost, 1);
            page.Children.Add(bannerHost);
            Grid main = new Grid();
            Grid.SetRow(main, 2);
            bodyHost = new Border { BorderBrush = Ds.Brush(pal.Separator), BorderThickness = new Thickness(0, 1, 0, 1) };
            main.Children.Add(bodyHost);
            sayHost = new Border
            {
                Background = Ds.Brush(Ds.Argb(0.78, 28, 28, 30)), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 0, 0), IsHitTestVisible = false, Opacity = 0
            };
            sayLabel = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 520 };
            sayHost.Child = sayLabel;
            main.Children.Add(sayHost);
            page.Children.Add(main);
            page.Children.Add(Footer(pal));
            proxy = new Border { Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            Grid.SetRowSpan(proxy, 4);
            page.Children.Add(proxy);
            ShowBody();
            ShowJsonError();
            SyncSub();
            if (file == null && !loading) ShowEmpty(null);
        }

        FrameworkElement Toolbar(Palette pal)
        {
            Grid bar = new Grid { Background = Brushes.Transparent };
            bar.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
            {
                if (e.OriginalSource != bar && !(e.OriginalSource is TextBlock)) return;
                if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else try { DragMove(); } catch { }
            };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0) };
            string icon = table ? "doc" : json ? "doc" : kind == TextKind.Log ? "history" : kind == TextKind.Code ? "text" : "doc";
            Color c1 = table ? pal.Green : json ? pal.Orange : kind == TextKind.Log ? pal.Purple : pal.Teal, c2 = table ? pal.Teal : json ? pal.Red : kind == TextKind.Log ? pal.Accent : pal.Accent;
            left.Children.Add(new IconTile(icon, 28, c1, c2) { VerticalAlignment = VerticalAlignment.Center });
            StackPanel names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), MaxWidth = 340 };
            titleLabel = new TextBlock { Text = path == null ? "Texto" : System.IO.Path.GetFileName(path), FontWeight = FontWeights.SemiBold, FontSize = 13.5, Foreground = Ds.Brush(pal.Label), TextTrimming = TextTrimming.CharacterEllipsis };
            subLabel = new TextBlock { FontSize = 11.5, Foreground = Ds.Brush(pal.Label2), TextTrimming = TextTrimming.CharacterEllipsis };
            names.Children.Add(titleLabel);
            names.Children.Add(subLabel);
            left.Children.Add(names);
            bar.Children.Add(left);

            StackPanel mid = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(mid, 1);
            if (root != null || table)
            {
                MacSegmented seg = new MacSegmented(new string[] { "Texto", table ? "Tabla" : "\u00C1rbol" }, view);
                seg.Changed += delegate(int i) { view = i; RunSearchSoon(); ShowBody(); Status(); };
                mid.Children.Add(seg);
            }
            if (kind == TextKind.Log && file != null)
            {
                levelButton = new MacButton(LevelName(minLevel), ButtonKind.Secondary, "filter", "\u25BE", 28);
                levelButton.Click += delegate { LevelMenu(levelButton); };
                mid.Children.Add(levelButton);
                levelChips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                mid.Children.Add(levelChips);
                FillChips();
            }
            bar.Children.Add(mid);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
            Grid.SetColumn(right, 2);
            search = new SearchField(pal, table ? "Buscar en la tabla" : root != null ? "Buscar claves y valores" : "Buscar") { Width = 230 };
            search.Text = query;
            search.Changed += delegate { searchLater.Restart(); };
            search.Step += Step;
            search.FilterChanged += delegate(bool on) { filterOn = on; if (view == 0) Refilter(); };
            search.IsEnabled = file != null;
            right.Children.Add(search);
            if (kind == TextKind.Log && file != null)
            {
                follow = new DocIconButton("play", following ? "En vivo: sigue lo que se escribe (pulsa para parar)" : "Seguir en vivo lo que se escribe", 30) { On = following, Margin = new Thickness(4, 0, 0, 0) };
                follow.Click += ToggleFollow;
                right.Children.Add(follow);
            }
            wrapButton = new DocIconButton("fitwidth", "Ajustar las l\u00EDneas a la ventana (Alt+Z)", 30) { On = wrapOn, Margin = new Thickness(2, 0, 0, 0) };
            wrapButton.IsEnabled = file != null && file.Whole && view == 0;
            wrapButton.Click += ToggleWrap;
            right.Children.Add(wrapButton);
            DocIconButton more = new DocIconButton("more", "M\u00E1s opciones", 30);
            more.Click += delegate { MoreMenu(more); };
            right.Children.Add(more);
            bar.Children.Add(right);
            return bar;
        }

        FrameworkElement Footer(Palette pal)
        {
            Grid foot = new Grid { Margin = new Thickness(14, 0, 10, 0) };
            Grid.SetRow(foot, 3);
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusLabel = new TextBlock { FontSize = 12, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            foot.Children.Add(statusLabel);
            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(acts, 1);
            MacButton copy = new MacButton("Copiar", ButtonKind.Plain, "copy", null, 26);
            copy.Click += Copy;
            MacButton shot = new MacButton("Capturar", ButtonKind.Plain, "camera", null, 26) { Margin = new Thickness(4, 0, 0, 0), ToolTip = "Lo que se ve, como una captura en la pila" };
            shot.Click += CaptureView;
            copy.IsEnabled = shot.IsEnabled = file != null;
            acts.Children.Add(copy);
            acts.Children.Add(shot);
            foot.Children.Add(acts);
            return foot;
        }

        void ShowBody()
        {
            if (bodyHost == null) return;
            if (text.Parent is Decorator) ((Decorator)text.Parent).Child = null;
            if (text.Parent is Panel) ((Panel)text.Parent).Children.Remove(text);
            if (file == null) { bodyHost.Child = null; return; }
            if (view == 1 && root != null)
            {
                if (tree == null)
                {
                    tree = new JsonTree(root);
                    tree.Picked += delegate(JsonNode n) { Status(); };
                    tree.Opened += delegate(JsonNode n) { view = 0; ShowBody(); text.CaretRow = n.Line - 1; text.ScrollToRow(n.Line - 1, false); text.InvalidateVisual(); Build(); };
                    tree.Menu += TreeMenu;
                }
                if (tree.Parent is Decorator) ((Decorator)tree.Parent).Child = null;
                bodyHost.Child = tree;
                tree.Focus();
                return;
            }
            if (view == 1 && table)
            {
                if (grid == null)
                {
                    grid = new CsvGrid(csv);
                    grid.Picked += Status;
                    grid.Menu += GridMenu;
                }
                if (grid.Parent is Decorator) ((Decorator)grid.Parent).Child = null;
                bodyHost.Child = grid;
                grid.Focus();
                return;
            }
            bodyHost.Child = text;
            text.Focus();
        }

        void RunSearchSoon() { if (query.Length > 0) searchLater.Restart(); }

        void SyncSub()
        {
            if (subLabel == null) return;
            if (file == null) { subLabel.Text = loading ? "Abriendo\u2026" : ""; return; }
            List<string> bits = new List<string>();
            if (table) bits.Add((csv.Rows.Count - (csv.HasHeader ? 1 : 0)).ToString("N0") + " filas \u00B7 " + csv.Columns + " columnas");
            else bits.Add(file.Count.ToString("N0") + (file.Done ? "" : "+") + (file.Count == 1 ? " l\u00EDnea" : " l\u00EDneas"));
            bits.Add(file.EncodingName);
            if (!string.IsNullOrEmpty(file.NewlineName)) bits.Add(file.NewlineName);
            bits.Add(Recents.Size(file.Length));
            subLabel.Text = string.Join(" \u00B7 ", bits.ToArray());
        }

        void Status()
        {
            if (statusLabel == null || file == null) return;
            string s;
            if (view == 1 && tree != null)
            {
                JsonNode n = tree.Selected;
                s = n == null ? JsonDoc.Count(root).ToString("N0") + " elementos" : JsonDoc.PathOf(n) + "  \u00B7  " + JsonKindName(n);
            }
            else if (view == 1 && grid != null)
            {
                s = grid.Shown.ToString("N0") + (grid.Shown == csv.Rows.Count - (csv.HasHeader ? 1 : 0) ? "" : " de " + (csv.Rows.Count - (csv.HasHeader ? 1 : 0)).ToString("N0")) + " filas \u00B7 separado por " + DelimiterName(csv.Delimiter) + (grid.Cell != null ? "  \u00B7  " + grid.Cell : "");
                if (csv.Truncated) s += " \u00B7 se muestran las primeras 200.000";
            }
            else
            {
                int row = text.CaretRow;
                List<string> bits = new List<string>();
                if (row >= 0 && (kept == null || kept.Count > 0)) bits.Add("L\u00EDnea " + ((kept == null ? row : kept[Math.Min(row, kept.Count - 1)]) + 1).ToString("N0"));
                if (kept != null) bits.Add(kept.Count.ToString("N0") + " de " + file.Count.ToString("N0") + " l\u00EDneas");
                if (following) bits.Add("en vivo");
                if (pretty != null) bits.Add("formateado para leerlo (el archivo no cambia)");
                s = string.Join(" \u00B7 ", bits.ToArray());
            }
            statusLabel.Text = s;
        }

        static string JsonKindName(JsonNode n)
        {
            switch (n.Kind)
            {
                case JsonKind.Object: return "objeto de " + n.Children.Count + (n.Children.Count == 1 ? " campo" : " campos");
                case JsonKind.Array: return "lista de " + n.Children.Count + (n.Children.Count == 1 ? " elemento" : " elementos");
                case JsonKind.String: return "texto";
                case JsonKind.Number: return "n\u00FAmero";
                case JsonKind.Null: return "nulo";
            }
            return "s\u00ED/no";
        }

        static string DelimiterName(char c) { return c == ';' ? "punto y coma" : c == '\t' ? "tabulador" : c == '|' ? "barra vertical" : "comas"; }

        internal void Say(string msg, bool bad) { Say(msg, bad, false); }

        void Say(string msg, bool bad, bool sticky)
        {
            lastSay = msg;
            if (sayHost == null) return;
            sayLabel.Text = msg;
            sayHost.Background = Ds.Brush(bad ? Ds.Argb(0.9, 200, 50, 40) : Ds.Argb(0.78, 28, 28, 30));
            sayHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            sayFade.Stop();
            if (!sticky) sayFade.Start();
        }

        void CloseSay()
        {
            sayFade.Stop();
            if (sayHost != null) sayHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
        }

        void ShowEmpty(string error)
        {
            Palette pal = Ds.Brushes;
            if (bodyHost == null) return;
            StackPanel s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            s.Children.Add(new GlyphView("doc", 46, pal.Label3, 1.4) { HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = error == null ? "Suelta aqu\u00ED un texto, un log o un JSON" : "No se ha podido abrir", FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 20, Foreground = Ds.Brush(pal.Label), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = error ?? "O \u00E1brelo con Ctrl+O.", FontSize = 13, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, TextAlignment = TextAlignment.Center });
            MacButton b = new MacButton("Abrir\u2026", ButtonKind.Secondary) { Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            b.Click += Browse;
            s.Children.Add(b);
            bodyHost.Child = s;
        }

        // A JSON that does not parse: where and why, over its text, with a way there.
        void ShowJsonError()
        {
            if (bannerHost == null) return;
            bannerHost.Child = null;
            if (!json || jsonError == null || file == null) return;
            Palette pal = Ds.Brushes;
            Border b = new Border { CornerRadius = new CornerRadius(8), Background = Ds.Brush(Ds.WithAlpha(pal.Red, pal.Dark ? 0.22 : 0.12)), Padding = new Thickness(12, 6, 8, 6), Margin = new Thickness(12, 0, 12, 8) };
            DockPanel d = new DockPanel();
            MacButton go = new MacButton("Ir al error", ButtonKind.Secondary);
            go.Click += delegate { view = 0; ShowBody(); text.CaretRow = Math.Max(0, jsonErrLine - 1); text.ScrollToRow(text.CaretRow, true); text.InvalidateVisual(); Status(); };
            DockPanel.SetDock(go, Dock.Right);
            d.Children.Add(go);
            d.Children.Add(new TextBlock { Text = "JSON no v\u00E1lido \u00B7 l\u00EDnea " + jsonErrLine + ", columna " + jsonErrCol + ": " + jsonError, Foreground = Ds.Brush(pal.Label), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            b.Child = d;
            bannerHost.Child = b;
        }

        void ToggleFollow()
        {
            following = !following;
            if (following) { followTimer.Start(); Poll(); text.ToEnd(); Say("En vivo: se ve lo que se va escribiendo", false); }
            else { followTimer.Stop(); Say("En pausa", false); }
            if (follow != null) { follow.On = following; follow.InvalidateVisual(); }
            Status();
        }

        void ToggleWrap()
        {
            if (file == null || !file.Whole) { Say("Este archivo es demasiado grande para ajustar sus l\u00EDneas.", false); return; }
            wrapOn = !wrapOn;
            text.Wrap = wrapOn;
            if (wrapButton != null) { wrapButton.On = wrapOn; wrapButton.InvalidateVisual(); }
        }

        // ---- Copy

        void Copy()
        {
            string t;
            if (view == 1 && tree != null) t = tree.Selected == null ? "" : JsonDoc.Compact(tree.Selected);
            else if (view == 1 && grid != null) t = grid.SelectionText();
            else t = text.SelectedText;
            if (string.IsNullOrEmpty(t)) { Say("No hay nada seleccionado.", false); return; }
            if (ToClipboard(t)) Say(text.HasSelection || view == 1 ? "Copiado" : "L\u00EDnea copiada", false);
        }

        bool ToClipboard(string t)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetText(t); return true; }
                catch (System.Runtime.InteropServices.ExternalException) { System.Threading.Thread.Sleep(40); }
                catch (Exception ex) { ShotStack.Log("Texto, portapapeles: " + ex.Message); break; }
            }
            Say("No he podido usar el portapapeles.", true);
            return false;
        }

        void CopyAll()
        {
            if (file == null) return;
            if (!file.Whole) { Say("El archivo es demasiado grande para copiarlo entero: selecciona una parte.", false); return; }
            if (ToClipboard(pretty ?? file.Text)) Say("Todo el texto copiado", false);
        }

        // ---- Captures

        // What the view shows, drawn as it is, into the stack of captures.
        void CaptureView()
        {
            FrameworkElement b = Body();
            if (b == null || b.ActualWidth < 2) return;
            try
            {
                double s = Ink.Scale(this);
                RenderTargetBitmap rtb = new RenderTargetBitmap((int)Math.Round(b.ActualWidth * s), (int)Math.Round(b.ActualHeight * s), 96 * s, 96 * s, PixelFormats.Pbgra32);
                rtb.Render(b);
                rtb.Freeze();
                if (owner == null) { Say("Capturado", false); return; }
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Say(owner.SaveCapture(Ink.ToGdi(rtb), name) != null ? "Captura en la pila" : "No se ha podido a\u00F1adir a la pila.", false);
            }
            catch (Exception ex) { ShotStack.Log("Texto, capturar: " + ex.Message); Say("No se ha podido capturar.", true); }
        }

        // ---- Menus

        Border proxy;

        // A menu where the pointer is (a context menu).
        void MenuAt(List<MenuEntry> m)
        {
            if (proxy == null || page == null) return;
            Point p = Mouse.GetPosition(page);
            proxy.Margin = new Thickness(p.X, p.Y, 0, 0);
            proxy.UpdateLayout();
            MacMenu.Show(proxy, m);
        }

        void TextMenu()
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item(text.HasSelection ? "Copiar" : "Copiar la l\u00EDnea", "copy", false, Copy));
            m.Add(MenuEntry.Item("Seleccionar todo  (Ctrl+A)", null, false, text.SelectAll));
            string sel = text.HasSelection ? text.SelectedText : "";
            if (sel.Length > 0 && sel.Length < 120 && sel.IndexOf('\n') < 0)
                m.Add(MenuEntry.Item("Buscar \u00AB" + (sel.Length > 30 ? sel.Substring(0, 30) + "\u2026" : sel) + "\u00BB", "search", false, delegate { search.Text = sel; RunSearch(); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Capturar lo que se ve", "camera", false, CaptureView));
            MenuAt(m);
        }

        void MoreMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Abrir otro\u2026  (Ctrl+O)", "open", false, Browse));
            if (file != null)
            {
                m.Add(MenuEntry.Item("Ir a la l\u00EDnea\u2026  (Ctrl+G)", "doc", false, GoToLine));
                m.Add(MenuEntry.Item("Copiar todo el texto", "copy", false, CopyAll));
                if (root != null)
                {
                    m.Add(MenuEntry.Item("Copiar formateado", "sparkle", false, delegate { if (ToClipboard(JsonDoc.Pretty(root, 2))) Say("JSON formateado copiado", false); }));
                    m.Add(MenuEntry.Item("Copiar compacto", "minus", false, delegate { if (ToClipboard(JsonDoc.Compact(root))) Say("JSON compacto copiado", false); }));
                }
                if (table)
                {
                    m.Add(MenuEntry.Item("Copiar la tabla para Excel", "copy", false, delegate { if (grid != null && ToClipboard(grid.AllText())) Say("Tabla copiada: p\u00E9gala en Excel", false); }));
                    m.Add(MenuEntry.Line());
                    m.Add(MenuEntry.Title("Separador"));
                    foreach (char d in new char[] { ',', ';', '\t', '|' })
                    {
                        char dd = d;
                        m.Add(MenuEntry.Item(DelimiterName(d).Substring(0, 1).ToUpper() + DelimiterName(d).Substring(1), null, csv.Delimiter == d, delegate { Reparse(dd); }));
                    }
                    m.Add(MenuEntry.Item("La primera fila es el encabezado", null, csv.HasHeader, delegate { csv.HasHeader = !csv.HasHeader; grid = null; ShowBody(); Status(); }));
                }
                m.Add(MenuEntry.Line());
                m.Add(MenuEntry.Item("Letra m\u00E1s grande  (Ctrl++)", "plus", false, delegate { text.SetFont(text.FontSizeNow + 1); }));
                m.Add(MenuEntry.Item("Letra m\u00E1s peque\u00F1a  (Ctrl+\u2212)", "minus", false, delegate { text.SetFont(text.FontSizeNow - 1); }));
                m.Add(MenuEntry.Line());
                m.Add(MenuEntry.Item("Editar en el Bloc de notas", "edit", false, delegate { Run("notepad.exe", "\"" + path + "\""); }));
                m.Add(MenuEntry.Item("Mostrar en la carpeta", "folder", false, delegate { Run("explorer.exe", "/select,\"" + path + "\""); }));
                m.Add(MenuEntry.Item("Volver a leer", "update", false, delegate { Load(path); }));
            }
            MacMenu.Show(anchor, m);
        }

        void Run(string exe, string args)
        {
            try { Process.Start(exe, args); }
            catch (Exception ex) { ShotStack.Log("Texto, abrir: " + ex.Message); Say("No se ha podido abrir.", true); }
        }

        void Reparse(char d)
        {
            if (file == null || !file.Whole) return;
            string err;
            CsvTable t = CsvTable.ParseWith(file.Text, d, 200000, out err);
            if (t == null) return;
            csv = t;
            grid = null;
            ShowBody();
            Status();
        }

        void TreeMenu(JsonNode n)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Copiar el valor", "copy", false, delegate { if (ToClipboard(n.Kind == JsonKind.String ? n.Value : JsonDoc.Compact(n))) Say("Valor copiado", false); }));
            m.Add(MenuEntry.Item("Copiar la ruta", "copy", false, delegate { if (ToClipboard(JsonDoc.PathOf(n))) Say(JsonDoc.PathOf(n), false); }));
            m.Add(MenuEntry.Item("Copiar como JSON", "copy", false, delegate { if (ToClipboard(JsonDoc.Pretty(n, 2))) Say("Copiado como JSON", false); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Ver en el texto", "doc", false, delegate { view = 0; ShowBody(); text.CaretRow = n.Line - 1; text.ScrollToRow(n.Line - 1, false); text.InvalidateVisual(); Build(); }));
            m.Add(MenuEntry.Item("Expandir todo", "plus", false, delegate { tree.ExpandAll(true); }));
            m.Add(MenuEntry.Item("Contraer todo", "minus", false, delegate { tree.ExpandAll(false); }));
            MenuAt(m);
        }

        void GridMenu()
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Item("Copiar", "copy", false, Copy));
            m.Add(MenuEntry.Item("Copiar la fila", "copy", false, delegate { if (ToClipboard(grid.RowText())) Say("Fila copiada", false); }));
            m.Add(MenuEntry.Item("Copiar la columna", "copy", false, delegate { if (ToClipboard(grid.ColumnText())) Say("Columna copiada", false); }));
            m.Add(MenuEntry.Line());
            m.Add(MenuEntry.Item("Copiar la tabla para Excel", "copy", false, delegate { if (ToClipboard(grid.AllText())) Say("Tabla copiada: p\u00E9gala en Excel", false); }));
            m.Add(MenuEntry.Item("Capturar lo que se ve", "camera", false, CaptureView));
            MenuAt(m);
        }

        // ---- Go to a line

        void GoToLine()
        {
            if (file == null) return;
            Palette pal = Ds.Brushes;
            TextBox box = new TextBox { Width = 90, FontSize = 14, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Label) };
            StackPanel s = new StackPanel { Orientation = Orientation.Horizontal };
            s.Children.Add(new TextBlock { Text = "L\u00EDnea", FontSize = 13, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            s.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = Ds.Brush(pal.Control), Padding = new Thickness(6, 3, 6, 3), Child = box });
            s.Children.Add(new TextBlock { Text = "de " + file.Count.ToString("N0"), FontSize = 13, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            Border card = new Border { CornerRadius = new CornerRadius(10), Background = Ds.Brush(pal.Dark ? Color.FromRgb(44, 44, 46) : Colors.White), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 14, 10), Child = s };
            Grid holder = new Grid { Margin = new Thickness(10) };
            holder.Children.Add(new SoftShadow(10, 6, 0.6, pal.Dark ? 0.5 : 0.16));
            holder.Children.Add(card);
            Popup p = new Popup { Child = holder, AllowsTransparency = true, PlacementTarget = bodyHost, Placement = PlacementMode.Relative, StaysOpen = false, HorizontalOffset = Math.Max(0, bodyHost.ActualWidth / 2 - 120), VerticalOffset = 10 };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { p.IsOpen = false; e.Handled = true; return; }
                if (e.Key != Key.Enter) return;
                int n;
                if (int.TryParse(box.Text.Trim().Replace(".", ""), NumberStyles.Integer, CultureInfo.CurrentCulture, out n) && n >= 1)
                {
                    if (view != 0) { view = 0; Build(); }
                    int row = n - 1;
                    if (kept != null) { row = kept.BinarySearch(n - 1); if (row < 0) row = Math.Min(kept.Count - 1, ~row); }
                    text.CaretRow = Math.Min(Shown - 1, row);
                    text.ScrollToRow(text.CaretRow, true);
                    text.InvalidateVisual();
                    Status();
                }
                p.IsOpen = false;
                text.Focus();
                e.Handled = true;
            };
            p.IsOpen = true;
            box.Focus();
        }
    }
}
