// Stackshot - "Novedades": what a version brings, inside the app. Either the version running (from what it carries)
// or the one available to update to (from its release), with the button to get it.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Stackshot
{
    public class NewsWindow : Sheet
    {
        static NewsWindow open;
        readonly bool upcoming;     // the version available, not the one running
        readonly string since;      // right after updating: the version whose news were seen last ("" none), else null
        StackPanel page;
        ScrollViewer pageHost;   // scrolls the page when the window is cut to the work area

        // upcoming: the version available to update to, when there is one (otherwise the one running).
        public static void Open(bool upcoming) { Open(upcoming, null); }

        // since: right after an update, the news of every version since that one, newest first.
        public static void Open(bool upcoming, string since)
        {
            if (upcoming && Updater.Available == null) upcoming = false;
            if (upcoming) since = null;
            if (open != null && (open.upcoming != upcoming || open.since != since)) open.Close();
            if (open != null)
            {
                if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
                open.Activate();
                return;
            }
            open = new NewsWindow(upcoming, since);
            open.Closed += delegate { open = null; };
            open.Show();
            open.Activate();
        }

        NewsWindow(bool upcoming, string since)
        {
            this.upcoming = upcoming;
            this.since = since;
            Title = I18n.T("Novedades", "What's new");
            Width = 540;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = AppIcon;
            DragAnywhere();
            KeyDown += delegate(object o, KeyEventArgs e) { if (!e.Handled && e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Build();
        }

        protected override void ThemeChanged() { Build(); }

        void Build()
        {
            Palette pal = Ds.Brushes;
            if (pageHost != null) Root.Children.Remove(pageHost);
            Root.Background = HasMica ? Brushes.Transparent : Ds.Brush(pal.Window);
            page = new StackPanel { Margin = new Thickness(30, 30, 30, 26) };
            pageHost = Scrolling(page);
            Root.Children.Add(pageHost);
            KeepCaptionsOnTop();

            Updater.Release r = upcoming ? Updater.Available : null;
            string version;
            List<News.Item> items;
            if (r != null)
            {
                version = r.Version.ToString(3);
                items = new List<News.Item>(r.News);
                // A release without Spanish news (older ones): its own headlines, as they are.
                if (items.Count == 0) foreach (string n in r.Notes) items.Add(new News.Item { Title = n, Text = "" });
            }
            else
            {
                version = Installer.MyVersion.ToString(3);
                items = News.Of(version);
                // A build that carries the news of a version still to be numbered (while it is being made).
                if (items.Count == 0) items = News.Newest(out version);
                if (version == null) version = Installer.MyVersion.ToString(3);
            }
            // Right after an update over more than one version (from 2.1 to 2.2.1, say): each one's news under its number.
            List<KeyValuePair<string, List<News.Item>>> groups = r == null && since != null ? News.Since(since, Installer.MyVersion.ToString(3)) : null;
            if (groups != null && groups.Count < 2) groups = null;

            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new IconTile("sparkle", 40, Ds.Rgb(255, 204, 0), Ds.Rgb(255, 149, 0)));
            StackPanel titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Text(I18n.Fmt("Novedades de Stackshot {0}", "What's new in Stackshot {0}", version), Ds.Title, 20, pal.Label, new Thickness(0)));
            string sub = r != null ? I18n.T("La versi\u00F3n nueva: esto es lo que trae.", "The new version: here is what's new.")
                       : groups != null ? I18n.T("Todo lo nuevo desde la versi\u00F3n que ten\u00EDas.", "Everything new since the version you had.")
                       : I18n.T("Lo que trae la versi\u00F3n que tienes.", "What's new in your current version.");
            titles.Children.Add(Text(sub, Ds.Regular, 12.5, pal.Label2, new Thickness(0, 2, 0, 0)));
            head.Children.Add(titles);
            page.Children.Add(head);

            StackPanel list = new StackPanel();
            if (groups != null)
                for (int g = 0; g < groups.Count; g++)
                {
                    list.Children.Add(Text(I18n.Fmt("En la {0}", "In {0}", groups[g].Key), Ds.Semibold, 12.5, pal.Label2, new Thickness(0, g == 0 ? 0 : 6, 0, 10)));
                    AddItems(list, groups[g].Value, pal);
                }
            else if (items.Count == 0) list.Children.Add(Para(I18n.T("Esta versi\u00F3n no trae notas.", "This version has no release notes."), pal.Label3, new Thickness(0)));
            else AddItems(list, items, pal);
            ScrollViewer sv = new ScrollViewer { MaxHeight = 470, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list, Margin = new Thickness(0, 22, 0, 0), Resources = HomeWindow.ScrollStyle(), Focusable = false };
            page.Children.Add(sv);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            MacButton close = new MacButton(r != null ? I18n.T("Ahora no", "Not now") : I18n.Close, r != null ? ButtonKind.Secondary : ButtonKind.Primary, null, null, 32);
            close.Click += delegate { Close(); };
            buttons.Children.Add(close);
            if (r != null)
            {
                // The same as the notice's button: download, verify and install here, or the release page when it can't.
                bool working = Updater.Doing == Updater.Step.Downloading || Updater.Doing == Updater.Step.Verifying || Updater.Doing == Updater.Step.Installing;
                string label = working ? I18n.T("Actualizando\u2026", "Updating\u2026") : !Updater.CanInstall ? I18n.T("Ver la versi\u00F3n", "View version") : Updater.IsReady ? I18n.T("Instalar ahora", "Install now") : I18n.T("Descargar e instalar", "Download and install");
                MacButton get = new MacButton(label, ButtonKind.Primary, Updater.CanInstall ? "update" : "open", null, 32) { IsEnabled = !working };
                get.Click += delegate
                {
                    if (Updater.CanInstall) Updater.Install();
                    else Updater.OpenPage();
                    Close();
                };
                // Windows' order: the main button first, "not now" after it.
                buttons.Children.Insert(0, get);
                close.Margin = new Thickness(8, 0, 0, 0);
            }
            page.Children.Add(buttons);
        }

        static void AddItems(StackPanel list, List<News.Item> items, Palette pal)
        {
            foreach (News.Item it in items)
            {
                Grid row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                GlyphView dot = new GlyphView("check", 15, pal.High ? pal.Label : pal.Green, 2) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
                row.Children.Add(dot);
                StackPanel txt = new StackPanel();
                TextBlock t = Text(it.Title, Ds.Semibold, 13.5, pal.Label, new Thickness(0));
                t.TextWrapping = TextWrapping.Wrap;
                t.VerticalAlignment = VerticalAlignment.Top;
                txt.Children.Add(t);
                if (it.Text.Length > 0) txt.Children.Add(Para(it.Text, pal.Label2, new Thickness(0, 3, 0, 0)));
                Grid.SetColumn(txt, 1);
                row.Children.Add(txt);
                list.Children.Add(row);
            }
        }

        static TextBlock Text(string s, Typeface face, double size, Color c, Thickness m)
        {
            return new TextBlock { Text = s, FontFamily = face.FontFamily, FontWeight = face.Weight, FontSize = size, Foreground = Ds.Brush(c), Margin = m, VerticalAlignment = VerticalAlignment.Center };
        }

        static TextBlock Para(string s, Color c, Thickness m)
        {
            TextBlock t = Text(s, Ds.Regular, 12.5, c, m);
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = 18;
            return t;
        }
    }
}
