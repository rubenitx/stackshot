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
            Header("Fondo y editor", "Como en CleanShot X: tus capturas, listas para presentar con un fondo bonito, margen y sombra. En el editor, bot\u00F3n Fondo (o la tecla B); tambi\u00E9n para v\u00EDdeos y GIF con \u00ABPresentar\u00BB.");

            Grid shell = new Grid { Height = 250, Margin = new Thickness(0, 0, 0, 26) };
            shell.Children.Add(CardShadow(16, 0.07, 14, 2, 1));
            Border card = CardFace(16);
            card.Background = Ds.Brush(pal.Dark ? Ds.Rgb(24, 24, 27) : Ds.Rgb(236, 236, 240));
            // A swatch hovered on the page this one replaces (a theme change) is not tried on the new preview.
            tryPreset = -1;
            if (tryTimer != null) tryTimer.Stop();
            previewImage = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(20, 18, 20, 18) };
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.HighQuality);
            card.Child = previewImage;
            shell.Children.Add(card);
            Add(shell);
            RefreshPreview();

            int[] pads = { 0, 25, 50, 85 }, radii = { 0, 40, 80 };
            string[] ratios = { "auto", "16:9", "4:3", "1:1" };
            bool custom = Backdrop.IsCustom(settings.BgPreset);
            Group(null,
                new PresetRow(),
                new ButtonRow("Tus fondos", custom ? "Est\u00E1s usando una imagen tuya. Puedes a\u00F1adir m\u00E1s o quitar esta."
                                                   : "A\u00F1ade una foto o imagen tuya y \u00FAsala como fondo de tus capturas.",
                              "photo", Mac.Teal, Mac.Blue, "A\u00F1adir imagen\u2026", AddBackground),
                custom
                    ? new ButtonRow("Quitar este fondo", "Se borra la copia guardada en Stackshot; tu imagen original no se toca.", "close", Mac.Red, Mac.Orange, "Quitar",
                                    delegate
                                    {
                                        int idx = settings.BgPreset;
                                        if (!Backdrop.IsCustom(idx)) return;
                                        Backdrop.RemoveCustom(idx);
                                        swatches.Clear();
                                        composed.Clear();
                                        settings.BgPreset = 0;
                                        settings.Save();
                                        Rebuild();
                                    })
                    : null,
                new ToggleRow("Abrir el editor con el fondo puesto", "Si no, se pone con el bot\u00F3n Fondo cuando quieras.", "photo", Mac.Purple, Mac.Pink,
                              delegate { return settings.BgAuto; }, delegate(bool v) { settings.BgAuto = v; }));
            // Values tuned with the editor's sliders fall between the presets: the nearest one shows, the row says so, and
            // clicking that one sets it exactly.
            Func<bool> padTuned = delegate { return Array.IndexOf(pads, settings.BgPadding) < 0; };
            Func<bool> radTuned = delegate { return Array.IndexOf(radii, settings.BgRadius) < 0; };
            Group("Estilo",
                new SegRow("Margen", null, null, D.Color.Empty, D.Color.Empty, new string[] { "Ninguno", "Peque\u00F1o", "Medio", "Grande" },
                           delegate { return Nearest(pads, settings.BgPadding); }, delegate(int i) { settings.BgPadding = pads[i]; StyleChanged(); })
                    { SubNow = delegate { return padTuned() ? Tuned : null; }, Loose = padTuned },
                new SegRow("Esquinas", null, null, D.Color.Empty, D.Color.Empty, new string[] { "Rectas", "Suaves", "Redondas" },
                           delegate { return Nearest(radii, settings.BgRadius); }, delegate(int i) { settings.BgRadius = radii[i]; StyleChanged(); })
                    { SubNow = delegate { return radTuned() ? Tuned : null; }, Loose = radTuned },
                new ToggleRow("Sombra", null, null, D.Color.Empty, D.Color.Empty,
                              delegate { return settings.BgShadow; }, delegate(bool v) { settings.BgShadow = v; StyleChanged(); }),
                new SegRow("Proporci\u00F3n", null, null, D.Color.Empty, D.Color.Empty, new string[] { "Auto", "16:9", "4:3", "1:1" },
                           delegate { return Math.Max(0, Array.IndexOf(ratios, settings.BgRatio)); }, delegate(int i) { settings.BgRatio = ratios[i]; StyleChanged(); }));
            styleNote = Paragraph("", 12, pal.Label2);
            styleNote.Margin = new Thickness(8, -14, 8, 24);
            Add(styleNote);
            PaintStyleNote();
        }

        const string Tuned = "A tu medida, desde el editor";
        TextBlock styleNote;

        bool DefaultStyle()
        {
            Settings d = new Settings();
            return settings.BgPadding == d.BgPadding && settings.BgRadius == d.BgRadius && settings.BgShadow == d.BgShadow && settings.BgRatio == d.BgRatio;
        }

        // The note under the style, with a reset link while anything differs from the defaults.
        void PaintStyleNote()
        {
            if (styleNote == null) return;
            styleNote.Inlines.Clear();
            styleNote.Inlines.Add(new System.Windows.Documents.Run("En el editor puedes afinar el margen y las esquinas con deslizadores; lo \u00FAltimo que uses se queda guardado aqu\u00ED."));
            if (DefaultStyle()) return;
            styleNote.Inlines.Add(new System.Windows.Documents.Run(" "));
            styleNote.Inlines.Add(InlineLink("Restablecer el estilo", delegate
            {
                Settings d = new Settings();
                settings.BgPadding = d.BgPadding;
                settings.BgRadius = d.BgRadius;
                settings.BgShadow = d.BgShadow;
                settings.BgRatio = d.BgRatio;
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
                WF.MessageBox.Show(Win32, "Ya tienes " + Backdrop.MaxCustom + " fondos propios. Quita uno para a\u00F1adir otro.", "Stackshot", WF.MessageBoxButtons.OK, WF.MessageBoxIcon.Information);
                return;
            }
            using (WF.OpenFileDialog d = new WF.OpenFileDialog())
            {
                d.Title = "Elige una imagen para usar de fondo";
                d.Filter = "Im\u00E1genes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*";
                if (d.ShowDialog(Win32) != WF.DialogResult.OK) return;
                // Decoding, scaling and saving a large photo takes a moment.
                int i;
                Mouse.OverrideCursor = Cursors.Wait;
                try { i = Backdrop.AddCustom(d.FileName); }
                finally { Mouse.OverrideCursor = null; }
                if (i < 0)
                {
                    WF.MessageBox.Show(Win32, "No se ha podido abrir esa imagen.", "Stackshot", WF.MessageBoxButtons.OK, WF.MessageBoxIcon.Warning);
                    return;
                }
                swatches.Clear();
                composed.Clear();
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
            return st.BgPreset + "|" + Backdrop.Name(st.BgPreset) + "|" + st.BgPadding + "|" + st.BgRadius + "|" + st.BgShadow + "|" + st.BgRatio;
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
                    if (p < 0 || p >= Backdrop.Count || p == settings.BgPreset || Backdrop.IsCustom(p)) { RefreshPreview(); return; }
                    Settings t = settings.Clone();
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

            public PresetRow() : base("Fondo", "", "photo", Mac.Brand1, Mac.Brand3) { }

            protected override FrameworkElement Control() { return null; }

            public override void Build()
            {
                RowDefinitions.Clear();
                Sub = Backdrop.Name(Selected);
                base.Build();
                MinHeight = 0;
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                WrapPanel wrap = new WrapPanel { Margin = new Thickness(-4, -2, -4, 10) };
                Grid.SetRow(wrap, 1);
                Grid.SetColumnSpan(wrap, 3);
                rings = new Border[Backdrop.Count];
                for (int i = 0; i < rings.Length; i++) wrap.Children.Add(Cell(i));
                Children.Add(wrap);
                Paint();
            }

            int Selected { get { return Math.Max(0, Math.Min(Backdrop.Count - 1, W == null ? 0 : W.settings.BgPreset)); } }

            FrameworkElement Cell(int i)
            {
                Palette pal = Ds.Brushes;
                Border swatch = new Border
                {
                    Width = 74,
                    Height = 46,
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Argb(0.10, 0, 0, 0))
                };
                BitmapSource src = Swatch(i);
                swatch.Background = src != null ? (Brush)new ImageBrush(src) { Stretch = Stretch.UniformToFill } : Ds.Brush(pal.Control);
                Border ring = new Border
                {
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
                return ring;
            }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                int sel = Selected;
                for (int i = 0; i < rings.Length; i++)
                    rings[i].BorderBrush = i == sel ? Ds.Brush(pal.Accent) : i == hover ? Ds.Brush(pal.Label3) : Brushes.Transparent;
                if (SubText != null) SubText.Text = Backdrop.Name(hover >= 0 ? hover : sel);
            }

            void Pick(int i)
            {
                bool wasCustom = Backdrop.IsCustom(W.settings.BgPreset);
                W.settings.BgPreset = i;
                W.Changed();
                Paint();
                W.RefreshPreview();
                if (wasCustom != Backdrop.IsCustom(i)) W.Rebuild(); // show or hide "Quitar este fondo"
            }
        }
    }
}
