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
            Title = I18n.T("Accesos r\u00E1pidos", "Quick links");
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
            head.Children.Add(QuickWindow.Text(I18n.T("Accesos r\u00E1pidos", "Quick links"), Ds.Title, 18, pal.Label, new Thickness(12, 0, 0, 0)));
            page.Children.Add(head);
            TextBlock about = QuickWindow.Text(I18n.T("Webs, carpetas, archivos o programas que quieras tener a mano: salen en el men\u00FA de la mascota y te los abre con un clic.", "Websites, folders, files or programs you want handy: they appear in the mascot's menu and open with a click."),
                                               Ds.Regular, 12.5, pal.Label2, new Thickness(0, 12, 0, 0));
            about.TextWrapping = TextWrapping.Wrap;
            page.Children.Add(about);

            List<QuickLink> list = owner.Links;
            bool full = list.Count >= ShotStack.MaxLinks;
            targetBox = QuickWindow.Field(target, false, 0, I18n.T("Direcci\u00F3n web o ruta", "Web address or path"));
            Border tf = QuickWindow.Frame(targetBox, I18n.T("https://\u2026 o C:\\Carpeta\\archivo", "https://\u2026 or C:\\Folder\\file"));
            tf.Margin = new Thickness(0, 16, 0, 0);
            page.Children.Add(tf);
            nameBox = QuickWindow.Field(name, false, 0, I18n.T("Nombre", "Name"));
            Border nf = QuickWindow.Frame(nameBox, I18n.T("Nombre (opcional)", "Name (optional)"));
            nf.Margin = new Thickness(0, 8, 0, 0);
            page.Children.Add(nf);

            StackPanel pick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            MacButton folder = new MacButton(I18n.T("Elegir carpeta\u2026", "Choose folder\u2026"), ButtonKind.Plain, "folder", null, 28) { Margin = new Thickness(0, 0, 6, 0) };
            folder.Click += PickFolder;
            MacButton file = new MacButton(I18n.T("Elegir archivo o programa\u2026", "Choose file or program\u2026"), ButtonKind.Plain, "doc", null, 28);
            file.Click += PickFile;
            pick.Children.Add(folder);
            pick.Children.Add(file);
            page.Children.Add(pick);

            error = QuickWindow.Text("", Ds.Regular, 12, pal.Error, new Thickness(0, 10, 0, 0));
            error.TextWrapping = TextWrapping.Wrap;
            error.Visibility = Visibility.Collapsed;
            page.Children.Add(error);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            MacButton close = new MacButton(I18n.Close, ButtonKind.Secondary, null, null, 32) { Margin = new Thickness(8, 0, 0, 0) };
            close.Click += delegate { Close(); };
            MacButton add = new MacButton(I18n.T("A\u00F1adir", "Add"), ButtonKind.Primary, "plus", null, 32);
            add.Click += AddOne;
            add.IsEnabled = !full;
            buttons.Children.Add(add);
            buttons.Children.Add(close);
            page.Children.Add(buttons);

            if (list.Count == 0) return;
            page.Children.Add(QuickWindow.Text(I18n.Fmt("Tus accesos ({0} de {1})", "Your links ({0} of {1})", list.Count, ShotStack.MaxLinks), Ds.Semibold, 12.5, pal.Label, new Thickness(0, 20, 0, 4)));
            foreach (QuickLink l in list)
            {
                QuickLink it = l;
                Grid g = new Grid { Margin = new Thickness(0, 3, 0, 0), MinHeight = 26 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
                TapBorder row = new TapBorder { Background = Brushes.Transparent, Cursor = Cursors.Hand, Label = I18n.Fmt("Abrir {0}", "Open {0}", l.Name), ToolTip = I18n.Fmt("Abrir {0}", "Open {0}", l.Target), Child = txt, CornerRadius = new CornerRadius(6) };
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
                TapBorder x = new TapBorder { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.Transparent, Cursor = Cursors.Hand, Label = I18n.Fmt("Quitar {0}", "Remove {0}", l.Name), ToolTip = I18n.T("Quitar", "Remove") };
                x.Child = new GlyphView("close", 11, pal.Label2, 1.8) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Action drop = delegate { Keep(); owner.RemoveLink(it); Build(); };
                x.MouseLeftButtonUp += delegate { drop(); };
                Access.Clickable(x, drop);
                Grid.SetColumn(x, 2);
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
            owner.Told(I18n.Fmt("\u00A1Listo! \u00AB{0}\u00BB ya est\u00E1 en mi men\u00FA.", "Done! \u201C{0}\u201D is now in my menu.", list[list.Count - 1].Name));
            name = target = "";
            Build();
            if (targetBox != null) targetBox.Focus();
        }

        void PickFolder()
        {
            using (WF.FolderBrowserDialog d = new WF.FolderBrowserDialog { Description = I18n.T("Elige la carpeta que quieres tener a mano", "Choose the folder you want to have handy"), ShowNewFolderButton = false })
                if (d.ShowDialog() == WF.DialogResult.OK) Picked(d.SelectedPath);
        }

        void PickFile()
        {
            using (WF.OpenFileDialog d = new WF.OpenFileDialog { Title = I18n.T("Elige el archivo o el programa", "Choose the file or program"), Filter = I18n.T("Todos los archivos|*.*", "All files|*.*"), DereferenceLinks = false })
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
