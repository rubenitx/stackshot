// Stackshot - Pictures in the Markdown preview: local images at their own size up to the width of the column (also in
// lists and tables), loaded off the UI thread; a quiet note in their place when the file is missing or unreadable.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stackshot
{
    public sealed partial class MarkdownFlow
    {
        string baseDir;
        // Printing (MarkdownFlow.Print.cs): pictures load at once and are never taller than a page.
        bool printing;
        double tallest = double.PositiveInfinity;

        // The document's folder: relative image links count from it (null for text that is not a file).
        public string BaseDir
        {
            get { return baseDir; }
            set
            {
                if (string.Equals(baseDir, value, StringComparison.OrdinalIgnoreCase)) return;
                baseDir = value;
                // Every picture may point somewhere else now: the next Set builds the whole document again.
                doc = null;
            }
        }

        // A local picture in place of an image's text; false for web addresses and the rest (they stay as before).
        bool Picture(Paragraph p, MdSpan s)
        {
            string path = MdImages.LocalPath(s.Href, baseDir);
            if (path == null || MdImages.Remote(path, baseDir)) return false;
            Border host = new Border();
            System.Windows.Automation.AutomationProperties.SetName(host, s.Text.StartsWith("\u25A3 ", StringComparison.Ordinal) ? s.Text.Substring(2) : s.Text);
            p.Inlines.Add(new InlineUIContainer(host));
            // Lines of text have a fixed height; a line with a picture grows to hold it instead of drawing over the text above.
            p.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            string href = s.Href;
            if (printing)
            {
                string err;
                BitmapSource b = MdImages.Load(path, out err);
                Show(host, b, err, href);
                return true;
            }
            // The picture already loaded shows at once (no jump while typing); the file is checked again off the thread.
            BitmapSource seen = MdImages.Peek(path);
            if (seen != null) Show(host, seen, null, href);
            MdImages.Request(path, delegate(BitmapSource b, string err) { if (b == null || b != seen) Show(host, b, err, href); });
            return true;
        }

        void Show(Border host, BitmapSource b, string err, string href)
        {
            if (b != null)
            {
                Image img = new Image { Source = b, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = tallest };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                System.Windows.Automation.AutomationProperties.SetName(img, System.Windows.Automation.AutomationProperties.GetName(host));   // readers hear its text
                host.Background = null;
                host.Padding = new Thickness(0);
                host.ToolTip = null;
                host.Child = img;
                return;
            }
            host.Background = codeBack;
            host.CornerRadius = new CornerRadius(6);
            host.Padding = new Thickness(8, 3, 10, 4);
            host.ToolTip = printing ? null : href;
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            // The icon cache belongs to the UI thread; a printed note does without it.
            if (!printing) row.Children.Add(new GlyphView("photo", 14, pal.Label2, 1.6) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            row.Children.Add(new TextBlock { Text = (err ?? I18n.T("No puedo mostrar esta imagen", "Cannot show this image")) + ": " + Uri.UnescapeDataString(href), FontSize = 12.5, Foreground = faint, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            host.Child = row;
        }
    }
}
