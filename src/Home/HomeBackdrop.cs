// Stackshot - Backdrop and editor section.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D = System.Drawing;
using D2 = System.Drawing.Drawing2D;
using WF = System.Windows.Forms;

namespace Stackshot
{
    public partial class HomeWindow
    {
        Image previewImage;

        void BuildEditor()
        {
            Palette pal = Ds.Brushes;
            Header(I18n.T("Fondo y editor", "Backdrop & editor"), I18n.T("Como en CleanShot X: tus capturas, listas para presentar con un fondo bonito, margen y sombra. En el editor, bot\u00F3n Fondo (o la tecla B); tambi\u00E9n para v\u00EDdeos y GIF con \u00ABPresentar\u00BB.", "Like CleanShot X: your captures ready to present with a beautiful backdrop, padding, and shadow. In the editor, use the Backdrop button (or key B); also for videos and GIFs with \u201CPresent\u201D."));

            Grid shell = new Grid { Height = 250, Margin = new Thickness(0, 0, 0, 26) };
            shell.Children.Add(CardShadow(16, 0.07, 14, 2, 1));
            Border card = CardFace(16);
            card.Background = Ds.Brush(pal.High ? pal.Window : pal.Dark ? Ds.Rgb(24, 24, 27) : Ds.Rgb(236, 236, 240));
            // A swatch hovered on the page this one replaces (a theme change) is not tried on the new preview.
            tryPreset = -1;
            if (tryTimer != null && !indexing) tryTimer.Stop();
            previewImage = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(20, 18, 20, 18) };
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.HighQuality);
            System.Windows.Automation.AutomationProperties.SetName(previewImage, I18n.T("Vista previa del fondo", "Backdrop preview"));
            card.Child = previewImage;
            shell.Children.Add(card);
            Add(shell);
            if (!indexing) RefreshPreview();

            int[] pads = { 0, 25, 50, 85 }, radii = { 0, 40, 80 };
            string[] ratios = { "auto", "16:9", "4:3", "1:1" };
            bool custom = Backdrop.IsCustom(settings.BgPreset);
            Group(null,
                new PresetRow(),
                new ButtonRow(I18n.T("Tus fondos", "Custom backdrops"), custom ? I18n.T("Est\u00E1s usando una imagen tuya. Puedes a\u00F1adir m\u00E1s o quitar esta.", "You are using your own image. You can add more or remove this one.")
                                                   : I18n.T("A\u00F1ade una foto o imagen tuya y \u00FAsala como fondo de tus capturas.", "Add a photo or image and use it as backdrop for your captures."),
                              "photo", Mac.Teal, Mac.Blue, I18n.T("A\u00F1adir imagen\u2026", "Add image\u2026"), AddBackground),
                custom
                    ? new ButtonRow(I18n.T("Quitar este fondo", "Remove this backdrop"), I18n.T("Se borra la copia guardada en Stackshot; tu imagen original no se toca.", "Deletes the copy saved in Stackshot; your original image is untouched."), "close", Mac.Red, Mac.Orange, I18n.T("Quitar", "Remove"),
                                    delegate
                                    {
                                        int idx = settings.BgPreset;
                                        if (!Backdrop.IsCustom(idx)) return;
                                        Backdrop.RemoveCustom(idx);
                                        BgPanel.Styles.PictureRemoved(settings, idx);
                                        swatches.Clear();
                                        composed.Clear();
                                        settings.BgPreset = 0;
                                        settings.Save();
                                        Rebuild();
                                    })
                    : null,
                new ToggleRow(I18n.T("Abrir el editor con el fondo puesto", "Open editor with backdrop enabled"), I18n.T("Si no, se pone con el bot\u00F3n Fondo cuando quieras.", "Otherwise, enable it with the Backdrop button anytime."), "photo", Mac.Purple, Mac.Pink,
                              delegate { return settings.BgAuto; }, delegate(bool v) { settings.BgAuto = v; }));
            // Values tuned with the editor's sliders fall between the presets: the nearest one shows, the row says so, and
            // clicking that one sets it exactly.
            Func<bool> padTuned = delegate { return Array.IndexOf(pads, settings.BgPadding) < 0; };
            Func<bool> radTuned = delegate { return Array.IndexOf(radii, settings.BgRadius) < 0; };
            Group(I18n.T("Estilo", "Style"),
                new SegRow(I18n.T("Margen", "Padding"), null, null, D.Color.Empty, D.Color.Empty, I18n.IsSpanish ? new string[] { "Ninguno", "Peque\u00F1o", "Medio", "Grande" } : new string[] { "None", "Small", "Medium", "Large" },
                           delegate { return Nearest(pads, settings.BgPadding); }, delegate(int i) { settings.BgPadding = pads[i]; StyleChanged(); })
                    { SubNow = delegate { return padTuned() ? Tuned : null; }, Loose = padTuned },
                new SegRow(I18n.T("Esquinas", "Corners"), null, null, D.Color.Empty, D.Color.Empty, I18n.IsSpanish ? new string[] { "Rectas", "Suaves", "Redondas" } : new string[] { "Sharp", "Soft", "Round" },
                           delegate { return Nearest(radii, settings.BgRadius); }, delegate(int i) { settings.BgRadius = radii[i]; StyleChanged(); })
                    { SubNow = delegate { return radTuned() ? Tuned : null; }, Loose = radTuned },
                new ToggleRow(I18n.T("Sombra", "Shadow"), null, null, D.Color.Empty, D.Color.Empty,
                              delegate { return settings.BgShadow; }, delegate(bool v) { settings.BgShadow = v; StyleChanged(); }),
                new SegRow(I18n.T("Proporci\u00F3n", "Aspect ratio"), null, null, D.Color.Empty, D.Color.Empty, new string[] { "Auto", "16:9", "4:3", "1:1" },
                           delegate { return Math.Max(0, Array.IndexOf(ratios, settings.BgRatio)); }, delegate(int i) { settings.BgRatio = ratios[i]; StyleChanged(); }));
            styleNote = Paragraph("", 12, pal.Label2);
            styleNote.Margin = new Thickness(8, -14, 8, 24);
            Add(styleNote);
            PaintStyleNote();
        }

        static string Tuned { get { return I18n.T("A tu medida, desde el editor", "Custom, from editor"); } }
        TextBlock styleNote;

        bool DefaultStyle()
        {
            Settings d = new Settings();
            return settings.BgPadding == d.BgPadding && settings.BgRadius == d.BgRadius && settings.BgShadow == d.BgShadow && settings.BgRatio == d.BgRatio &&
                   settings.BgShadowSize == d.BgShadowSize && settings.BgInset == d.BgInset && settings.BgBalance == d.BgBalance && settings.BgAlign == d.BgAlign;
        }

        // The note under the style, with a reset link while anything differs from the defaults.
        void PaintStyleNote()
        {
            if (styleNote == null) return;
            styleNote.Inlines.Clear();
            styleNote.Inlines.Add(new System.Windows.Documents.Run(I18n.T("En el editor tienes m\u00E1s: colores lisos, la captura desenfocada, inserci\u00F3n, alineaci\u00F3n y deslizadores para el margen, la sombra y las esquinas; lo \u00FAltimo que uses se queda guardado aqu\u00ED.", "In the editor you have more: plain colors, blurred capture, inset, alignment, and sliders for padding, shadow, and corners; what you last used is saved here.")));
            if (DefaultStyle()) return;
            styleNote.Inlines.Add(new System.Windows.Documents.Run(" "));
            styleNote.Inlines.Add(InlineLink(I18n.T("Restablecer el estilo", "Reset style"), delegate
            {
                Settings d = new Settings();
                settings.BgPadding = d.BgPadding;
                settings.BgRadius = d.BgRadius;
                settings.BgShadow = d.BgShadow;
                settings.BgRatio = d.BgRatio;
                settings.BgShadowSize = d.BgShadowSize;
                settings.BgInset = d.BgInset;
                settings.BgBalance = d.BgBalance;
                settings.BgAlign = d.BgAlign;
                Changed();
                RefreshPreview();
                SyncRows(); // the controls slide back and this note loses its link
            }));
        }

        // A style choice: the preview follows (the rows and this note follow through SyncRows).
        void StyleChanged()
        {
            RefreshPreview();
        }

        void AddBackground()
        {
            if (Backdrop.CustomCount >= Backdrop.MaxCustom)
            {
                Dialogs.Show(Win32, I18n.Fmt("Ya tienes {0} fondos propios. Quita uno para a\u00F1adir otro.", "You already have {0} custom backdrops. Remove one to add another.", Backdrop.MaxCustom), "Stackshot", WF.MessageBoxButtons.OK, WF.MessageBoxIcon.Information);
                return;
            }
            using (WF.OpenFileDialog d = new WF.OpenFileDialog())
            {
                d.Title = I18n.T("Elige una imagen para usar de fondo", "Choose an image to use as backdrop");
                d.Filter = I18n.T("Im\u00E1genes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
                if (d.ShowDialog(Win32) != WF.DialogResult.OK) return;
                // Decoding, scaling and saving a large photo takes a moment.
                int i;
                Mouse.OverrideCursor = Cursors.Wait;
                try { i = Backdrop.AddCustom(d.FileName); }
                finally { Mouse.OverrideCursor = null; }
                if (i < 0)
                {
                    Dialogs.Show(Win32, I18n.T("No se ha podido abrir esa imagen.", "Could not open that image."), "Stackshot", WF.MessageBoxButtons.OK, WF.MessageBoxIcon.Warning);
                    return;
                }
                swatches.Clear();
                composed.Clear();
                settings.BgKind = 0;
                settings.BgPreset = i;
                settings.Save();
                Rebuild();
            }
        }

        // ---- Preview with a sample window (never a real capture)

        static D.Bitmap sample;
        static readonly Dictionary<string, BitmapSource> composed = new Dictionary<string, BitmapSource>(); // a few, shared across visits
        System.Windows.Threading.DispatcherTimer tryTimer;
        int tryPreset = -1;

        void RefreshPreview()
        {
            if (previewImage == null) return;
            previewImage.Source = Composed(settings);
        }

        static string ComposedKey(Settings st)
        {
            return st.BgPreset + "|" + Backdrop.Name(st.BgPreset) + "|" + st.BgPadding + "|" + st.BgRadius + "|" + st.BgShadow + "|" + st.BgRatio + "|" + st.BgKind + "|" +
                   st.BgColor + "|" + st.BgBlur + "|" + st.BgShadowSize + "|" + st.BgInset + "|" + st.BgBalance + "|" + st.BgAlign;
        }

        // Each preview is about 1.4 MB and the cache lives as long as the app: a few are kept, always the chosen one.
        BitmapSource Composed(Settings st)
        {
            string key = ComposedKey(st);
            BitmapSource s;
            if (composed.TryGetValue(key, out s)) return s;
            if (composed.Count >= 4)
            {
                string mine = ComposedKey(settings);
                BitmapSource keep;
                bool had = composed.TryGetValue(mine, out keep);
                composed.Clear();
                if (had) composed[mine] = keep;
            }
            try
            {
                using (D.Bitmap b = Backdrop.Compose(Sample(), st)) s = Ink.FromGdi(b);
            }
            catch (Exception ex) { ShotStack.Log("Vista previa del fondo: " + ex.Message); }
            composed[key] = s;
            return s;
        }

        // Pointing at a swatch tries it on the preview once the mouse rests there (nothing is composed while it sweeps
        // across); leaving goes back to the chosen one. Photos of your own are read at full size, so they show once chosen.
        void TryBackdrop(int i)
        {
            if (previewImage == null) return;
            tryPreset = i;
            if (tryTimer == null)
            {
                tryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
                tryTimer.Tick += delegate
                {
                    tryTimer.Stop();
                    if (page != "editor" || previewImage == null) return;
                    int p = tryPreset;
                    if (p < 0 || p >= Backdrop.Count || (p == settings.BgPreset && settings.BgKind == 0) || Backdrop.IsCustom(p)) { RefreshPreview(); return; }
                    Settings t = settings.Clone();
                    t.BgKind = 0;
                    t.BgPreset = p;
                    previewImage.Source = Composed(t);
                };
            }
            tryTimer.Stop();
            tryTimer.Start();
        }

        static D.Bitmap Sample()
        {
            if (sample != null) return sample;
            D.Bitmap bmp = new D.Bitmap(560, 340, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (D.Graphics g = D.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = D2.SmoothingMode.AntiAlias;
                g.Clear(D.Color.FromArgb(30, 30, 36));
                using (D.SolidBrush b = new D.SolidBrush(D.Color.FromArgb(42, 42, 50))) g.FillRectangle(b, 0, 0, 560, 34);
                D.Color[] dots = { D.Color.FromArgb(255, 95, 87), D.Color.FromArgb(254, 188, 46), D.Color.FromArgb(40, 200, 64) };
                for (int i = 0; i < 3; i++) using (D.SolidBrush b = new D.SolidBrush(dots[i])) g.FillEllipse(b, 14 + i * 20, 11, 12, 12);
                using (D.SolidBrush b = new D.SolidBrush(D.Color.FromArgb(38, 38, 46))) g.FillRectangle(b, 0, 34, 130, 306);
                for (int i = 0; i < 6; i++)
                    using (D2.GraphicsPath p = Theme.Round(new D.RectangleF(16, 56 + i * 30, 70 + (i * 23) % 30, 10), 5))
                    using (D.SolidBrush b = new D.SolidBrush(i == 1 ? D.Color.FromArgb(10, 132, 255) : D.Color.FromArgb(70, 70, 82))) g.FillPath(b, p);
                using (D2.GraphicsPath p = Theme.Round(new D.RectangleF(152, 56, 220, 18), 6))
                using (D.SolidBrush b = new D.SolidBrush(D.Color.FromArgb(220, 222, 235))) g.FillPath(b, p);
                for (int i = 0; i < 4; i++)
                    using (D2.GraphicsPath p = Theme.Round(new D.RectangleF(152, 92 + i * 22, 360 - (i * 57) % 120, 9), 4))
                    using (D.SolidBrush b = new D.SolidBrush(D.Color.FromArgb(84, 84, 98))) g.FillPath(b, p);
                int[] bars = { 60, 95, 72, 130, 110, 150, 124 };
                for (int i = 0; i < bars.Length; i++)
                {
                    D.RectangleF br = new D.RectangleF(156 + i * 52, 310 - bars[i], 34, bars[i]);
                    using (D2.GraphicsPath p = Theme.Round(br, 6))
                    using (D2.LinearGradientBrush lb = new D2.LinearGradientBrush(D.RectangleF.Inflate(br, 1, 1), Mac.Brand1, Mac.Brand3, 90f)) g.FillPath(lb, p);
                }
            }
            sample = bmp;
            return sample;
        }

        // ---- Backdrop picker

        static readonly Dictionary<int, BitmapSource> swatches = new Dictionary<int, BitmapSource>();

        static BitmapSource Swatch(int i)
        {
            BitmapSource s;
            if (swatches.TryGetValue(i, out s)) return s;
            s = null;
            try
            {
                using (D.Bitmap b = new D.Bitmap(148, 92, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (D.Graphics g = D.Graphics.FromImage(b))
                    {
                        g.SmoothingMode = D2.SmoothingMode.AntiAlias;
                        g.InterpolationMode = D2.InterpolationMode.HighQualityBicubic;
                        Backdrop.PaintSwatch(g, new D.Rectangle(0, 0, b.Width, b.Height), i);
                    }
                    s = Ink.FromGdi(b);
                }
            }
            catch { }
            swatches[i] = s;
            return s;
        }

        // Grid with every backdrop; the selected one has an accent ring, the hovered one a quiet ring.
        class PresetRow : Row
        {
            Border[] rings = new Border[0];
            int hover = -1;

            public PresetRow() : base(I18n.T("Fondo", "Backdrop"), "", "photo", Mac.Brand1, Mac.Brand3) { }

            protected override FrameworkElement Control() { return null; }

            public override void Build()
            {
                RowDefinitions.Clear();
                Sub = Chosen(-1);
                base.Build();
                MinHeight = 0;
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                WrapPanel wrap = new ListWrap { Margin = new Thickness(-4, -2, -4, 10), Label = I18n.T("Fondos", "Backdrops") };
                Grid.SetRow(wrap, 1);
                Grid.SetColumnSpan(wrap, 3);
                rings = new Border[W.indexing ? 0 : Backdrop.Count];
                for (int i = 0; i < rings.Length; i++) wrap.Children.Add(Cell(i));
                Children.Add(wrap);
                Paint();
            }

            // None while the editor's plain colour or blurred capture is the backdrop.
            int Selected
            {
                get
                {
                    if (W != null && W.settings.BgKind != 0) return -1;
                    return Math.Max(0, Math.Min(Backdrop.Count - 1, W == null ? 0 : W.settings.BgPreset));
                }
            }

            // What the row says: the swatch pointed at, else the backdrop in use.
            string Chosen(int hovered)
            {
                if (hovered >= 0) return Backdrop.Name(hovered);
                if (Selected >= 0) return Backdrop.Name(Selected);
                return W.settings.BgKind == 1 ? I18n.T("Color liso", "Plain color") : I18n.T("La captura desenfocada", "Blurred capture");
            }

            FrameworkElement Cell(int i)
            {
                Palette pal = Ds.Brushes;
                Border swatch = new Border
                {
                    Width = 74,
                    Height = 46,
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Ds.Brush(pal.High ? pal.Hairline : pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Argb(0.10, 0, 0, 0))
                };
                BitmapSource src = Swatch(i);
                swatch.Background = src != null ? (Brush)new ImageBrush(src) { Stretch = Stretch.UniformToFill } : Ds.Brush(pal.Control);
                Border ring = new TapBorder
                {
                    Role = Access.ListItem, On = delegate { return Selected == i; }, Do = delegate { Pick(i); },
                    CornerRadius = new CornerRadius(11),
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, 4, 4),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = swatch,
                    ToolTip = Backdrop.Name(i)
                };
                ToolTipService.SetInitialShowDelay(ring, 700);
                rings[i] = ring;
                ring.MouseEnter += delegate { hover = i; Paint(); W.TryBackdrop(i); };
                ring.MouseLeave += delegate { if (hover == i) { hover = -1; W.TryBackdrop(-1); } Paint(); };
                ring.MouseLeftButtonUp += delegate { Pick(i); };
                ring.GotKeyboardFocus += delegate { hover = i; Paint(); W.TryBackdrop(i); };   // the keyboard tries it on, as the mouse does
                ring.LostKeyboardFocus += delegate { if (hover == i && !ring.IsMouseOver) { hover = -1; W.TryBackdrop(-1); } Paint(); };
                return ring;
            }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                int sel = Selected;
                for (int i = 0; i < rings.Length; i++)
                    rings[i].BorderBrush = i == sel ? Ds.Brush(pal.Accent) : i == hover ? Ds.Brush(pal.Label3) : Brushes.Transparent;
                if (SubText != null) SubText.Text = Chosen(hover);
            }

            void Pick(int i)
            {
                bool wasCustom = Backdrop.IsCustom(W.settings.BgPreset);
                W.settings.BgKind = 0;
                W.settings.BgPreset = i;
                W.Changed();
                Paint();
                W.RefreshPreview();
                if (wasCustom != Backdrop.IsCustom(i)) W.Rebuild(); // show or hide "Quitar este fondo"
            }
        }
    }
}
