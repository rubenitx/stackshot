// Stackshot - The bar under the toolbar for a SOAP message: its operation, when the file was written, its size, OK or the
// fault (with its code and message: it takes the place of the red fault banner) and the XML before and after it in the
// folder. While comparing, the compare bar goes under it.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Stackshot
{
    public sealed partial class MarkdownWindow
    {
        Border headerHost;
        XmlModel soapFor;
        SoapInfo soapInfo;
        DocIconButton soapPrev, soapNext;
        // The neighbours of the file on disk, looked up off the UI thread: for which file (and version) and the answer.
        string neighboursFor;
        bool hasPrev, hasNext, neighboursKnown, headerHooked;
        int neighbourToken;
        string sizeFor;
        long sizeBytes;

        internal Border HeaderHost { get { return headerHost; } }
        internal bool HasPrevious { get { return hasPrev; } }
        internal bool HasNext { get { return hasNext; } }
        internal bool NeighboursKnown { get { return neighboursKnown; } }

        // Row 1 of the page (the Markdown formatting bar's row, free for an XML).
        FrameworkElement NewHeaderHost()
        {
            headerHost = new Border();
            Grid.SetRow(headerHost, 1);
            return headerHost;
        }

        // The SOAP data of the open document once its tree is up; null when it is not a SOAP envelope.
        SoapInfo Soap()
        {
            if (!xml || model == null || treeFor != model) return null;
            if (soapFor != model) { soapFor = model; soapInfo = XmlSoap.Read(model); }
            return soapInfo;
        }

        // The fault is shown in the SOAP bar, not in a banner of its own.
        bool FaultInHeader() { return Soap() != null; }

        // Fills row 1: the SOAP bar and/or the compare bar.
        void ShowHeader()
        {
            if (headerHost == null) return;
            if (!headerHooked)
            {
                headerHooked = true;
                // The folder may have new files by the time the user comes back.
                Activated += delegate { if (xml && file != null && file.Path != null) RefreshNeighbours(true); };
                // Attachments opened in an earlier session go the way of the temporary captures.
                int keep = TempKeep;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate { XmlAttachment.Prune(keep); });
            }
            DropStaleCompare();
            RefreshNeighbours(false);
            FrameworkElement soap = SoapBar(), cmp = compare != null ? CompareBar() : null;
            if (soap == null || cmp == null) { headerHost.Child = soap ?? cmp; return; }
            StackPanel both = new StackPanel();
            both.Children.Add(soap);
            both.Children.Add(cmp);
            headerHost.Child = both;
        }

        FrameworkElement SoapBar()
        {
            soapPrev = soapNext = null;
            SoapInfo s = Soap();
            if (s == null) return null;
            Palette pal = Ds.Brushes;
            bool fault = faultCode != null;
            Color red = pal.Error, green = pal.High ? pal.Label : pal.Dark ? pal.Green : Ds.Rgb(20, 112, 48);
            Border b = new Border { BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(18, 5, 12, 5) };
            b.Background = Ds.Brush(pal.High ? pal.Window : fault ? Ds.WithAlpha(pal.Red, pal.Dark ? 0.16 : 0.10) : pal.Dark ? Ds.Argb(0.035, 255, 255, 255) : Ds.Argb(0.025, 0, 0, 0));
            b.BorderBrush = Ds.Brush(fault && !pal.High ? Ds.WithAlpha(pal.Red, 0.35) : pal.Separator);
            Grid g = new Grid();
            for (int i = 0; i < 5; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            TextBlock op = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 420 };
            op.Text = s.FaultBody ? "SOAP Fault" : s.Operation.Length > 0 ? s.Operation : I18n.T("Body vac\u00EDo", "Empty Body");
            op.Foreground = Ds.Brush(s.FaultBody ? red : pal.Label);
            op.ToolTip = s.Qualified.Length > 0 ? s.Qualified + (s.NsUri.Length > 0 ? "\n" + s.NsUri : "") + "\nSOAP " + s.Version : "SOAP " + s.Version;
            g.Children.Add(op);

            if (fault)
            {
                TextBlock msg = new TextBlock { FontSize = 13, Foreground = Ds.Brush(pal.Label), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 0, 8, 0) };
                msg.Text = faultText.Length > 0 ? faultText : I18n.T("Sin mensaje", "No message");
                if (faultText.Length == 0) msg.Foreground = Ds.Brush(pal.Label2);
                msg.ToolTip = (faultCode + "\n" + faultText).Trim();
                Grid.SetColumn(msg, 1);
                g.Children.Add(msg);
            }

            TextBlock meta = new TextBlock { FontSize = 12.5, Foreground = Ds.Brush(pal.Label2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 10, 0) };
            meta.Text = SoapMeta();
            Grid.SetColumn(meta, 2);
            g.Children.Add(meta);

            Border chip = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 1, 8, 2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            chip.Background = Ds.Brush(pal.High ? pal.Window : Ds.WithAlpha(fault ? pal.Red : pal.Green, pal.Dark ? 0.22 : 0.16));
            chip.BorderBrush = Ds.Brush(pal.Hairline);
            chip.BorderThickness = HcPaint.Edge;
            TextBlock ct = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ds.Brush(fault ? red : green), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 240 };
            ct.Text = fault ? "Fault" + (faultCode.Length > 0 ? " \u00B7 " + faultCode : "") : "OK";
            chip.ToolTip = fault ? (faultCode.Length > 0 ? I18n.Fmt("C\u00F3digo del fallo: {0}", "Fault code: {0}", faultCode) : I18n.T("Respuesta con fallo", "Fault response")) : I18n.T("Sin SOAP Fault", "No SOAP Fault");
            chip.Child = ct;
            Grid.SetColumn(chip, 3);
            g.Children.Add(chip);

            StackPanel nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            soapPrev = new DocIconButton("back", I18n.T("XML anterior de la carpeta ([)", "Previous XML in folder ([)"), 26);
            soapPrev.Click += delegate { Neighbour(-1); };
            soapNext = new DocIconButton("right", I18n.T("XML siguiente de la carpeta (])", "Next XML in folder (])"), 26) { Margin = new Thickness(2, 0, 0, 0) };
            soapNext.Click += delegate { Neighbour(1); };
            nav.Children.Add(soapPrev);
            nav.Children.Add(soapNext);
            Grid.SetColumn(nav, 4);
            g.Children.Add(nav);
            UpdateNav();
            b.Child = g;
            return b;
        }

        // "12/10 10:31:22 · 3 KB": the file's time when it is on disk, and its size (the text's, for pasted text).
        string SoapMeta()
        {
            bool onDisk = file != null && file.Path != null;
            long bytes;
            if (onDisk) bytes = file.Length;
            else
            {
                if (sizeFor != text)
                {
                    sizeFor = text;
                    Encoding enc = file != null && file.Enc != null ? file.Enc : Encoding.UTF8;
                    try { sizeBytes = enc.GetByteCount(text ?? ""); }
                    catch (Exception) { sizeBytes = Encoding.UTF8.GetByteCount(text ?? ""); }
                }
                bytes = sizeBytes;
            }
            string size = Recents.Size(Math.Max(1, bytes));
            return onDisk ? XmlSoap.When(file.StampUtc) + "  \u00B7  " + size : size;
        }

        void UpdateNav()
        {
            bool onDisk = file != null && file.Path != null;
            if (soapPrev != null) soapPrev.IsEnabled = onDisk && neighboursKnown && hasPrev;
            if (soapNext != null) soapNext.IsEnabled = onDisk && neighboursKnown && hasNext;
        }

        // Whether there are XML files before and after this one in its folder, listed off the UI thread (a network share
        // can be slow). force: again even for the same file (the folder may have changed).
        void RefreshNeighbours(bool force)
        {
            string p = xml && file != null ? file.Path : null;
            if (p == null)
            {
                neighboursFor = null;
                hasPrev = hasNext = neighboursKnown = false;
                neighbourToken++;
                return;
            }
            string key = p + "|" + file.StampUtc.Ticks;
            if (!force && key == neighboursFor) return;
            if (key != neighboursFor) { hasPrev = hasNext = neighboursKnown = false; UpdateNav(); }
            neighboursFor = key;
            int my = ++neighbourToken;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool prev = false, next = false;
                try
                {
                    XmlWatch.Siblings(p, out prev, out next);
                }
                catch (Exception ex) { ShotStack.Log("XML vecinos: " + ex.Message); }
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != neighbourToken) return;
                        hasPrev = prev;
                        hasNext = next;
                        neighboursKnown = true;
                        UpdateNav();
                    }));
                }
                catch { }
            });
        }
    }
}
