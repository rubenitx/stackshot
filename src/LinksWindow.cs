// Stackshot - Quick links, from the mascot's menu or its settings: add a web address, a folder, a file or a program
// (typed or picked) and see or remove the ones there are. It stays open to add several; Enter adds, Esc closes.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WF = System.Windows.Forms;

namespace Stackshot
{
    public class LinksWindow : Sheet
    {
        static LinksWindow open;
        readonly ShotStack owner;
        string name = "", target = "";
        StackPanel page;
        ScrollViewer pageHost;   // scrolls the page when the window is cut to the work area
        TextBox nameBox, targetBox;
        TextBlock error;

        // A link's shortcut changed in Atajos: the list shows it.
        public static void Refresh()
        {
            if (open == null) return;
            open.Keep();
            open.Build();
        }

        public static void Show(ShotStack owner)
        {
            if (open != null) { open.Activate(); return; }
            open = new LinksWindow(owner);
            open.Closed += delegate { open = null; };
            ((Window)open).Show();
            open.Activate();
        }

        LinksWindow(ShotStack owner)
        {
            this.owner = owner;
            Title = "Accesos r\u00E1pidos";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = AppIcon;
            DragAnywhere();
            KeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Handled) return;
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter) { e.Handled = true; AddOne(); }
            };
            Build();
            Loaded += delegate { if (targetBox != null) targetBox.Focus(); };
        }

        protected override void ThemeChanged() { Keep(); Build(); }

        void Keep()
        {
            if (nameBox != null) name = nameBox.Text;
            if (targetBox != null) target = targetBox.Text;
        }

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (pageHost != null) Root.Children.Remove(pageHost);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new StackPanel { Margin = new Thickness(26, 26, 26, 22) };
            pageHost = Scrolling(page);
            Root.Children.Add(pageHost);
            KeepCaptionsOnTop();

            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new IconTile("open", 36, Ds.Rgb(124, 92, 255), Ds.Rgb(10, 132, 255)));
            head.Children.Add(QuickWindow.Text("Accesos r\u00E1pidos", Ds.Title, 18, pal.Label, new Thickness(12, 0, 0, 0)));
            page.Children.Add(head);
            TextBlock about = QuickWindow.Text("Webs, carpetas, archivos o programas que quieras tener a mano: salen en el men\u00FA de la mascota y te los abre con un clic. Con un atajo de teclado, desde cualquier sitio.",
                                               Ds.Regular, 12.5, pal.Label2, new Thickness(0, 12, 0, 0));
            about.TextWrapping = TextWrapping.Wrap;
            page.Children.Add(about);

            List<QuickLink> list = owner.Links;
            bool full = list.Count >= ShotStack.MaxLinks;
            targetBox = QuickWindow.Field(target, false, 0, "Direcci\u00F3n web o ruta");
            Border tf = QuickWindow.Frame(targetBox, "https://\u2026 o C:\\Carpeta\\archivo");
            tf.Margin = new Thickness(0, 16, 0, 0);
            page.Children.Add(tf);
            nameBox = QuickWindow.Field(name, false, 0, "Nombre");
            Border nf = QuickWindow.Frame(nameBox, "Nombre (opcional)");
            nf.Margin = new Thickness(0, 8, 0, 0);
            page.Children.Add(nf);

            StackPanel pick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            MacButton folder = new MacButton("Elegir carpeta\u2026", ButtonKind.Plain, "folder", null, 28) { Margin = new Thickness(0, 0, 6, 0) };
            folder.Click += PickFolder;
            MacButton file = new MacButton("Elegir archivo o programa\u2026", ButtonKind.Plain, "doc", null, 28);
            file.Click += PickFile;
            pick.Children.Add(folder);
            pick.Children.Add(file);
            page.Children.Add(pick);

            error = QuickWindow.Text("", Ds.Regular, 12, pal.Error, new Thickness(0, 10, 0, 0));
            error.TextWrapping = TextWrapping.Wrap;
            error.Visibility = Visibility.Collapsed;
            page.Children.Add(error);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            MacButton close = new MacButton("Cerrar", ButtonKind.Secondary, null, null, 32) { Margin = new Thickness(8, 0, 0, 0) };
            close.Click += delegate { Close(); };
            MacButton add = new MacButton("A\u00F1adir", ButtonKind.Primary, "plus", null, 32);
            add.Click += AddOne;
            add.IsEnabled = !full;
            buttons.Children.Add(add);
            buttons.Children.Add(close);
            page.Children.Add(buttons);

            if (list.Count == 0) return;
            page.Children.Add(QuickWindow.Text("Tus accesos (" + list.Count + " de " + ShotStack.MaxLinks + ")", Ds.Semibold, 12.5, pal.Label, new Thickness(0, 20, 0, 4)));
            foreach (QuickLink l in list)
            {
                QuickLink it = l;
                Grid g = new Grid { Margin = new Thickness(0, 3, 0, 0), MinHeight = 26 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                // Its shortcut, or a way to give it one: Atajos opens with its field listening.
                TapBorder keys = new TapBorder { Background = Brushes.Transparent, Cursor = Cursors.Hand, CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center,
                                                 Label = l.Keys.Length > 0 ? "Cambiar el atajo de " + l.Name : "Ponerle un atajo a " + l.Name, ToolTip = l.Keys.Length > 0 ? "Cambiar el atajo" : "Ponerle un atajo de teclado" };
                if (l.Keys.Length > 0) keys.Child = new Keycaps(Hotkeys.Display(l.Keys), 18);
                else keys.Child = QuickWindow.Text("Atajo\u2026", Ds.Regular, 12, pal.Link, new Thickness(0));
                Action setKeys = delegate { owner.RecordLinkKeys(it); };
                keys.MouseEnter += delegate { keys.Background = Ds.Brush(Ds.Brushes.Control); };
                keys.MouseLeave += delegate { keys.Background = Brushes.Transparent; };
                keys.MouseLeftButtonUp += delegate { setKeys(); };
                Access.Clickable(keys, setKeys);
                Grid.SetColumn(keys, 2);
                g.Children.Add(keys);
                g.Children.Add(new GlyphView(l.Icon, 15, pal.Label2, 1.6) { VerticalAlignment = VerticalAlignment.Center });
                // Name, then where it goes, cut with "…" to the room left.
                DockPanel txt = new DockPanel { Margin = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, LastChildFill = true };
                TextBlock nm = QuickWindow.Text(l.Name, Ds.Medium, 12.5, pal.Label, new Thickness(0));
                nm.MaxWidth = 180;
                nm.TextTrimming = TextTrimming.CharacterEllipsis;
                DockPanel.SetDock(nm, Dock.Left);
                txt.Children.Add(nm);
                TextBlock where = QuickWindow.Text(l.Target, Ds.Regular, 12, pal.Label3, new Thickness(8, 0, 0, 0));
                where.TextTrimming = TextTrimming.CharacterEllipsis;
                txt.Children.Add(where);
                // A click on it opens it, as from the mascot's menu.
                TapBorder row = new TapBorder { Background = Brushes.Transparent, Cursor = Cursors.Hand, Label = "Abrir " + l.Name, ToolTip = "Abrir " + l.Target, Child = txt, CornerRadius = new CornerRadius(6) };
                Action go = delegate
                {
                    string why = owner.OpenLink(it);
                    if (why != null) Say(why);
                };
                row.MouseEnter += delegate { row.Background = Ds.Brush(Ds.Brushes.Control); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                row.MouseLeftButtonUp += delegate { go(); };
                Access.Clickable(row, go);
                Grid.SetColumn(row, 1);
                g.Children.Add(row);
                TapBorder x = new TapBorder { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.Transparent, Cursor = Cursors.Hand, Label = "Quitar " + l.Name, ToolTip = "Quitar" };
                x.Child = new GlyphView("close", 11, pal.Label2, 1.8) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Action drop = delegate { Keep(); owner.RemoveLink(it); Build(); };
                x.MouseLeftButtonUp += delegate { drop(); };
                Access.Clickable(x, drop);
                Grid.SetColumn(x, 3);
                g.Children.Add(x);
                page.Children.Add(g);
            }
        }

        void AddOne()
        {
            Keep();
            string why = owner.AddLink(name, target);
            if (why != null) { Say(why); return; }
            List<QuickLink> list = owner.Links;
            owner.Told("\u00A1Listo! \u00AB" + list[list.Count - 1].Name + "\u00BB ya est\u00E1 en mi men\u00FA.");
            name = target = "";
            Build();
            if (targetBox != null) targetBox.Focus();
        }

        void PickFolder()
        {
            using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog { Description = "Elige la carpeta que quieres tener a mano", ShowNewFolderButton = false })
                if (d.ShowDialog() == WF.DialogResult.OK) Picked(d.SelectedPath);
        }

        void PickFile()
        {
            using (WF.OpenFileDialog d = new WF.OpenFileDialog { Title = "Elige el archivo o el programa", Filter = "Todos los archivos|*.*", DereferenceLinks = false })
                if (d.ShowDialog() == WF.DialogResult.OK) Picked(d.FileName);
        }

        void Picked(string path)
        {
            Keep();
            target = path;
            if (name.Trim().Length == 0) name = ShotStack.LinkName(path);
            Build();
            if (targetBox != null) { targetBox.Focus(); targetBox.CaretIndex = targetBox.Text.Length; }
        }

        void Say(string s)
        {
            if (error == null) return;
            error.Text = s;
            error.Visibility = Visibility.Visible;
        }
    }
}
