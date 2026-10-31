// Stackshot - The outline panel of a Markdown document ("Indice" in the toolbar): its headings beside the text, a click
// jumps there (in the editor too), and the section on screen or under the caret stays marked. Hidden until asked for;
// the window remembers it while it is open. Nothing is built or outlineFlow while it is hidden.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        bool outlineOn;
        Grid outlineHost;
        Border outlinePane;
        ListBox outlineList;
        TextBlock outlineEmpty;
        OutlineToggle outlineButton;
        List<MdHeading> outline = new List<MdHeading>();
        readonly Dictionary<string, string> outlineMemo = new Dictionary<string, string>(StringComparer.Ordinal);
        List<MdBlock> previewBlocks;
        MarkdownFlow outlineFlow;
        int outlineAt = -1;

        internal bool OutlineOpen { get { return outlineOn; } }
        internal List<MdHeading> Outline { get { return outline; } }
        internal int OutlineCurrent { get { return outlineAt; } }
        internal ListBox OutlineList { get { return outlineList; } }
        internal MarkdownFlow PreviewFlow { get { return flow; } }

        // The preview's blocks (the document's folder for its pictures); the outline reads the same parse.
        void ShowBlocks()
        {
            List<MdBlock> blocks = MarkdownDoc.Parse(Current());
            flow.BaseDir = DocumentFolder();
            flow.Set(blocks);
            previewBlocks = blocks;
            if (flow != outlineFlow)
            {
                if (outlineFlow == null) Closed += delegate { MdImages.Clear(); };
                outlineFlow = flow;
                flow.Box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(delegate { if (outlineOn) OutlineFollowPreview(); }));
            }
            if (outlineOn) RefreshOutline();
        }

        // Saved in another folder: relative pictures count from there now.
        void PicturesFolderChanged()
        {
            if (!xml && flow != null && text != null && !string.Equals(flow.BaseDir, DocumentFolder(), StringComparison.OrdinalIgnoreCase)) Preview(false);
        }

        FrameworkElement OutlineTool()
        {
            outlineButton = new OutlineToggle(I18n.T("\u00CDndice", "Contents")) { Margin = new Thickness(8, 0, 0, 0), On = outlineOn };
            outlineButton.ToolTip = I18n.T("\u00CDndice: mostrar u ocultar los t\u00EDtulos del documento", "Contents: show or hide the document's headings");
            outlineButton.Click += delegate { SetOutline(!outlineOn); };
            return outlineButton;
        }

        // The body of a Markdown document with a place for the panel on its left; anything else as it is.
        FrameworkElement OutlineWrap(Grid body)
        {
            outlineHost = null;
            outlinePane = null;
            outlineList = null;
            outlineEmpty = null;
            outlineAt = -1;
            if (text == null || xml) return body;
            Grid wrap = new Grid();
            Grid.SetRow(wrap, 4);
            wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wrap.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(body, 0);
            Grid.SetColumn(body, 1);
            wrap.Children.Add(body);
            outlineHost = wrap;
            if (outlineOn) ShowOutlinePane();
            return wrap;
        }

        internal void SetOutline(bool on)
        {
            if (xml || text == null || on == outlineOn) return;
            outlineOn = on;
            if (outlineButton != null) outlineButton.On = on;
            if (!on)
            {
                if (outlinePane != null && outlineHost != null) outlineHost.Children.Remove(outlinePane);
                outlinePane = null;
                outlineList = null;
                outlineEmpty = null;
                outlineAt = -1;
                return;
            }
            ShowOutlinePane();
            RefreshOutline();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(OutlineFollowNow));
        }

        void ShowOutlinePane()
        {
            if (outlineHost == null || outlinePane != null) return;
            Palette pal = Ds.Brushes;
            outlinePane = new Border { Width = 220, BorderBrush = Ds.Brush(pal.Separator), BorderThickness = new Thickness(0, 0, 1, 0) };
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.Children.Add(new TextBlock { Text = I18n.T("\u00CDndice", "Contents"), FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = Ds.Brush(pal.Label2), Margin = new Thickness(18, 12, 12, 6) });
            outlineList = new NamedListBox { ItemName = delegate(object o) { MdHeading h = o as MdHeading; return h != null ? h.Text : null; } };
            string acc = Color.FromArgb(pal.High ? (byte)0xFF : pal.Dark ? (byte)0x55 : (byte)0x2E, pal.Accent.R, pal.Accent.G, pal.Accent.B).ToString();
            string xaml =
                "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Style TargetType='ListBoxItem'><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'>" +
                "<Border x:Name='b' Background='Transparent' CornerRadius='6' Margin='8,0,8,1'><ContentPresenter/></Border><ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#14808080'/></Trigger>" +
                "<Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='" + acc + "'/>" +
                "<Setter TargetName='b' Property='TextElement.Foreground' Value='" + (pal.High ? pal.OnAccent : pal.Label) + "'/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></ResourceDictionary>";
            ResourceDictionary rd = new ResourceDictionary();
            rd.MergedDictionaries.Add(HomeWindow.ScrollStyle());
            rd.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml));
            outlineList.Resources = rd;
            outlineList.BorderThickness = new Thickness(0);
            outlineList.Background = Brushes.Transparent;
            outlineList.Foreground = Ds.Brush(pal.Label);
            outlineList.SelectionMode = SelectionMode.Single;
            outlineList.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            outlineList.FocusVisualStyle = null;
            outlineList.Padding = new Thickness(0, 0, 0, 20);
            ScrollViewer.SetHorizontalScrollBarVisibility(outlineList, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(outlineList, ScrollBarVisibility.Auto);
            VirtualizingStackPanel.SetIsVirtualizing(outlineList, true);
            VirtualizingStackPanel.SetVirtualizationMode(outlineList, VirtualizationMode.Recycling);
            outlineList.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(MdOutlineRow)) };
            System.Windows.Automation.AutomationProperties.SetName(outlineList, I18n.T("\u00CDndice del documento", "Document contents"));
            outlineList.PreviewMouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                ListBoxItem item = e.OriginalSource is DependencyObject ? ItemsControl.ContainerFromElement(outlineList, (DependencyObject)e.OriginalSource) as ListBoxItem : null;
                MdHeading h = item != null ? item.DataContext as MdHeading : null;
                if (h != null) JumpToHeading(h);
            };
            outlineList.KeyDown += delegate(object o, KeyEventArgs e)
            {
                MdHeading h = outlineList.SelectedItem as MdHeading;
                if ((e.Key != Key.Enter && e.Key != Key.Space) || h == null) return;
                e.Handled = true;
                JumpToHeading(h);
            };
            Grid.SetRow(outlineList, 1);
            g.Children.Add(outlineList);
            outlineEmpty = new TextBlock
            {
                Text = I18n.T("No hay t\u00EDtulos todav\u00EDa. Las l\u00EDneas que empiezan por # aparecer\u00E1n aqu\u00ED.", "No headings yet. Lines that start with # will show up here."),
                FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 4, 16, 0), Visibility = Visibility.Collapsed
            };
            Grid.SetRow(outlineEmpty, 1);
            g.Children.Add(outlineEmpty);
            outlinePane.Child = g;
            outlineHost.Children.Add(outlinePane);
            outlineList.ItemsSource = outline;
            ShowOutlineEmpty();
        }

        void ShowOutlineEmpty()
        {
            if (outlineList == null) return;
            bool none = outline.Count == 0;
            outlineList.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
            outlineEmpty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        }

        // The headings again after the preview read the text (it waits for a pause in typing). The list is rebuilt only when
        // a heading changed; otherwise just where they are.
        void RefreshOutline()
        {
            if (!outlineOn || xml || text == null) return;
            List<MdHeading> next = MdOutline.Headings(previewBlocks, outlineMemo);
            MdOutline.Locate(next, Current());
            bool same = next.Count == outline.Count;
            for (int i = 0; same && i < next.Count; i++) same = next[i].Level == outline[i].Level && next[i].Depth == outline[i].Depth && next[i].Text == outline[i].Text;
            if (same)
            {
                for (int i = 0; i < next.Count; i++) { outline[i].Line = next[i].Line; outline[i].Block = next[i].Block; outline[i].Offset = next[i].Offset; }
            }
            else
            {
                outline = next;
                outlineAt = -1;
                if (outlineList != null) outlineList.ItemsSource = outline;
            }
            ShowOutlineEmpty();
            OutlineFollowNow();
        }

        void OutlineHook(MarkdownEditor ed)
        {
            ed.CaretMoved += delegate { if (outlineOn && editing && ed == editor && ed.Box.IsKeyboardFocused) OutlineFollowCaret(); };
        }

        void OutlineFollowNow()
        {
            if (!outlineOn) return;
            if (editing && editor != null && editor.Box.IsKeyboardFocused) OutlineFollowCaret(); else OutlineFollowPreview();
        }

        // While typing: the section the caret is in.
        void OutlineFollowCaret()
        {
            if (editor == null || outline.Count == 0) return;
            OutlineMark(MdOutline.At(outline, editor.Box.CaretIndex));
        }

        // While reading: the last heading at or above the top of the preview (the caret wins while the editor has the focus).
        void OutlineFollowPreview()
        {
            if (editing && editor != null && editor.Box.IsKeyboardFocused) { OutlineFollowCaret(); return; }
            if (flow == null || outline.Count == 0) return;
            ScrollViewer sv = flow.Scroller;
            if (sv == null) return;
            double top = sv.VerticalOffset + 24;
            int lo = 0, hi = outline.Count - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                double y = flow.Top(outline[mid].Block);
                if (double.IsNaN(y)) return;
                if (y <= top) { found = mid; lo = mid + 1; } else hi = mid - 1;
            }
            // At the end the last headings may never reach the top: then the last one on screen is the one being read.
            if (sv.ScrollableHeight > 0 && sv.VerticalOffset >= sv.ScrollableHeight - 1)
            {
                double bottom = sv.VerticalOffset + sv.ViewportHeight - 24;
                for (int k = outline.Count - 1; k > found; k--)
                {
                    double y = flow.Top(outline[k].Block);
                    if (!double.IsNaN(y) && y <= bottom) { found = k; break; }
                }
            }
            OutlineMark(found);
        }

        void OutlineMark(int k)
        {
            if (k >= outline.Count) k = outline.Count - 1;
            if (outlineList == null) { outlineAt = k; return; }
            if (k == outlineAt && outlineList.SelectedIndex == k) return;
            outlineAt = k;
            outlineList.SelectedIndex = k;
            if (k >= 0) outlineList.ScrollIntoView(outline[k]);
        }

        // A heading picked in the panel: the preview goes there and, while editing, the caret goes to its line.
        internal void JumpToHeading(MdHeading h)
        {
            int k = outline.IndexOf(h);
            if (k < 0 || flow == null) return;
            OutlineMark(k);
            int block = h.Block;
            if (editing && editor != null && h.Line >= 0)
            {
                int at = EditCommands.Offset(Current(), h.Line + 1, 1);
                editor.Box.Focus();
                editor.Select(at, 0);
                // After the editor has scrolled (the preview follows it by proportion), the heading's exact place.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { if (flow != null) { flow.ScrollTo(block); OutlineMark(k); } }));
                return;
            }
            flow.ScrollTo(block);
        }
    }
}
