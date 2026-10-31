// Stackshot - Quick editor window (captures, and videos/GIFs in presentation mode).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class Editor : Form
    {
        static readonly Tool[] ToolOrder = { Tool.Arrow, Tool.Line, Tool.Rect, Tool.Ellipse, Tool.Text, Tool.Counter, Tool.Highlight, Tool.Spotlight, Tool.Pixelate, Tool.Redact, Tool.Crop };
        static readonly string[] ToolNamesEs = { "Flecha", "L\u00EDnea", "Recuadro", "Elipse", "Texto", "N\u00FAmeros", "Resaltar", "Foco: oscurece el resto", "Pixelar", "Tapar (lo m\u00E1s seguro para datos)", "Recortar" };
        static readonly string[] ToolNamesEn = { "Arrow", "Line", "Rectangle", "Ellipse", "Text", "Numbers", "Highlight", "Spotlight: dims the rest", "Pixelate", "Redact (safest for sensitive data)", "Crop" };
        static string[] ToolNames { get { return I18n.IsSpanish ? ToolNamesEs : ToolNamesEn; } }
        static readonly string[] ToolKeys = { "F", "L", "R", "E", "T", "N", "H", "O", "P", "X", "C" };
        static readonly string[] ToolGlyphs = { "arrow", "line", "rect", "ellipse", "text", "counter", "highlight", "spotlight", "pixelate!", "redact!", "crop" };
        static readonly string[] WeightNamesEs = { "Fino", "Medio", "Grueso" };
        static readonly string[] WeightNamesEn = { "Thin", "Medium", "Thick" };
        static string[] WeightNames { get { return I18n.IsSpanish ? WeightNamesEs : WeightNamesEn; } }
        static readonly string[] SizeNamesEs = { "Peque\u00F1o", "Mediano", "Grande" };
        static readonly string[] SizeNamesEn = { "Small", "Medium", "Large" };
        static string[] SizeNames { get { return I18n.IsSpanish ? SizeNamesEs : SizeNamesEn; } }
        // Captures that already have the backdrop applied, so reopening them doesn't add a second one.
        static readonly HashSet<string> withBackdrop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        readonly ShotStack owner;
        readonly string path;
        readonly Canvas canvas;
        readonly Bar bar;
        readonly BgPanel bgPanel;
        readonly Hint hint;
        readonly Timeline timeline;
        float s;
        int frameRequest;
        readonly Settings bgs;
        readonly Timer feedback = new Timer();
        readonly Dictionary<Tool, Bar.Item> toolItems = new Dictionary<Tool, Bar.Item>();
        readonly List<Bar.Item> colorItems = new List<Bar.Item>(), weightItems = new List<Bar.Item>();
        readonly Bar.Item undoItem, redoItem, copyItem, saveItem, bgItem, pdfItem;
        bool dirty, closeWithoutAsking;
        // The file had a backdrop baked in when the editor opened; the file was rewritten in this session.
        readonly bool openedWithBackdrop;
        bool written;
        // Whether the backdrop was on when the editor opened (automatic backdrop), and whether it was touched since.
        readonly bool bgAtOpen;
        bool bgTouched;
        // Presentation mode: the first frame is annotated and FFmpeg applies the result to the whole video.
        readonly string ffmpeg;
        readonly double duration;
        // The videos joined in this session; the first is the file the editor opened, defines the canvas and keeps the marks.
        readonly List<VideoClip> clips = new List<VideoClip>();
        // The size of the picture the marks are drawn on (never changes; read from the playback thread).
        readonly Size canvasSize;
        bool hasAudio;
        double sourceFps = 60;
        Process export;
        List<string> exportPasses;     // the FFmpeg runs of the export under way (two for a size limit)
        ExportSpec exportSpec;         // what they were made from (a second pass that missed its size is redone from it)
        int exportRetries;
        string exportLog, exportTop;   // x264's two-pass log and the overlay image, deleted when it ends
        double exportSpan;
        string exportOut;
        bool toGif;
        // RGB video (Cine master, GIF): the export converts it to YUV with BT.709 itself.
        bool rgbSource;
        // The frame rate was read from the file (otherwise 60 is a guess and the export's rate is not checked).
        bool fpsKnown;
        // The finished export is being checked on this thread; the abort flag kills the check's FFmpeg.
        System.Threading.Thread checkThread;
        ExportAbort checkAbort;
        ShareExpect exportExpect;
        readonly Bar.Item exportItem;
        // Every editor window open in this process, for the questions asked before quitting.
        static readonly List<Editor> open = new List<Editor>();

        public Editor(ShotStack owner, string path, Bitmap img) : this(owner, path, img, null, 0) { }

        Editor(ShotStack owner, string path, Bitmap img, string ffmpeg, double duration)
        {
            this.owner = owner;
            this.path = path;
            this.ffmpeg = ffmpeg;
            this.duration = duration;
            Screen scr = Screen.FromPoint(Control.MousePosition);
            s = ShotStack.ScaleFor(scr);
            bgs = owner != null ? owner.Settings : Settings.Load();

            AutoScaleMode = AutoScaleMode.None;
            BackColor = EdLook.C(Ds.Brushes.Window);
            ForeColor = EdLook.C(Ds.Brushes.Label);
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            StartPosition = FormStartPosition.Manual;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;

            canvas = new Canvas(img);
            canvasSize = img.Size;
            canvas.Ui = s;
            canvas.Dock = DockStyle.Fill;
            canvas.Bg = bgs;
            canvas.Video = IsVideo;
            openedWithBackdrop = withBackdrop.Contains(path);
            canvas.BgOn = bgs.BgAuto && !openedWithBackdrop;
            bgAtOpen = canvas.BgOn;
            canvas.Changed += delegate { dirty = true; UpdateUi(); };
            canvas.StateChanged += delegate { UpdateUi(); };

            bar = new Bar();
            bar.S = s;
            bar.Dock = DockStyle.Top;
            bar.Height = P(52);
            for (int i = 0; i < ToolOrder.Length; i++)
            {
                Tool t = ToolOrder[i];
                if (IsVideo && t == Tool.Pixelate) continue; // pixelating would need frame-by-frame tracking
                Bar.Item it = bar.Add(Bar.Kind.Tool);
                it.Tool = t;
                it.Glyph = ToolGlyphs[i];
                it.Tip = ToolNames[i] + "  (" + ToolKeys[i] + ")";
                it.Do = delegate { SetTool(t); };
                toolItems[t] = it;
            }
            bar.Add(Bar.Kind.Gap).Group = 1;
            for (int i = 0; i < Theme.Palette.Length; i++)
            {
                int idx = i;
                Bar.Item it = bar.Add(Bar.Kind.Swatch);
                it.Swatch = Theme.Palette[i];
                it.Tip = Theme.PaletteNames[i] + "  (" + (i + 1) + ")";
                it.Group = 1;
                it.Do = delegate { SetColor(idx); };
                colorItems.Add(it);
            }
            bar.Add(Bar.Kind.Gap).Group = 2;
            for (int i = 0; i < WeightNames.Length; i++)
            {
                int idx = i;
                Bar.Item it = bar.Add(Bar.Kind.Weight);
                it.Index = i;
                it.Group = 2;
                it.Tip = (I18n.IsSpanish ? "Grosor: " : "Thickness: ") + WeightNames[i] + "  (\u2212 / +)";
                it.Do = delegate { SetWeight(idx); };
                weightItems.Add(it);
            }
            bar.Add(Bar.Kind.Gap);
            undoItem = Button(null, "undo", I18n.T("Deshacer  (Ctrl+Z)", "Undo  (Ctrl+Z)"), delegate { canvas.Undo(); }, false);
            redoItem = Button(null, "redo", I18n.T("Rehacer  (Ctrl+Y)", "Redo  (Ctrl+Y)"), delegate { canvas.Redo(); }, false);
            bar.Add(Bar.Kind.Gap);
            bgItem = Button(I18n.T("Fondo", "Backdrop"), "backdrop", I18n.T("Fondo de presentaci\u00F3n: degradados, im\u00E1genes, desenfocado o color liso, con margen, sombra y esquinas  (B)", "Presentation backdrop: gradients, images, blur or solid color, with padding, shadow and rounded corners  (B)"), ToggleBgPanel, false);

            if (IsVideo)
            {
                exportItem = Button(I18n.T("Exportar", "Export"), "share", ExportTip(), Done, true);
                exportItem.Accent = true;
            }
            else
            {
                SensitiveSetup();
                Button(null, "pin", I18n.T("Fijar en pantalla: queda flotando encima de todo", "Pin to screen: stays floating on top"), PinOut, true);
                pdfItem = Button("PDF", "doc", I18n.T("Guardar como PDF: las capturas largas se reparten en p\u00E1ginas sin perder nitidez  (Ctrl+P)", "Save as PDF: tall scrolling captures split across pages without losing crispness  (Ctrl+P)"), delegate { SaveAs(true); }, true);
                pdfItem.Alt = "PDF \u2713";
                pdfItem.Optional = true; // Ctrl+P
                Bar.Item drag = Button(I18n.T("Arrastrar", "Drag"), "share", I18n.T("Arr\u00E1strala al chat o a otra aplicaci\u00F3n", "Drag to chat or another application"), null, true);
                drag.DragOut = true;
                drag.Optional = true;    // the thumbnail in the stack drags too
                Bar.Item textItem = Button(I18n.T("Copiar texto", "Copy text"), "textscan", I18n.T("Copiar el texto de la imagen (reconocimiento de texto de Windows)  (Ctrl+May\u00FAs+C)", "Copy text from image (Windows OCR)  (Ctrl+Shift+C)"), CopyTextOut, true);
                textItem.Optional = true; // Ctrl+Shift+C
                copyItem = Button(I18n.T("Copiar", "Copy"), "copy", I18n.T("Copiar al portapapeles  (Ctrl+C)", "Copy to clipboard  (Ctrl+C)"), CopyOut, true);
                copyItem.Alt = I18n.T("Copiado \u2713", "Copied \u2713");
                saveItem = Button(I18n.T("Guardar", "Save"), "save", InTemp ? I18n.T("Conservarla en tu carpeta de capturas  (Ctrl+S)", "Keep in your captures folder  (Ctrl+S)") : I18n.T("Guardar sobre la imagen  (Ctrl+S)", "Save over the image  (Ctrl+S)"), KeepCopy, true);
                saveItem.Alt = I18n.T("Guardada \u2713", "Saved \u2713");
                Bar.Item done = Button(I18n.T("Listo", "Done"), "check", I18n.T("Aplicar, copiar y cerrar  (Enter)", "Apply, copy and close  (Enter)"), Done, true);
                done.Accent = true;
                bar.DragOut += StartDragOut;
            }
            // The toolbar is also the title bar: window buttons at the top right.
            string[] captionTips = I18n.IsSpanish ? new string[] { "Minimizar", "Maximizar", "Cerrar" } : new string[] { "Minimize", "Maximize", "Close" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                Bar.Item cap = bar.Add(Bar.Kind.Caption);
                cap.Index = i;
                cap.Right = true;
                cap.Tip = captionTips[i];
                cap.Do = delegate { CaptionClick(idx); };
                if (i == 1) maxItem = cap;
            }

            bgPanel = new BgPanel();
            bgPanel.S = s;
            bgPanel.Bg = bgs;
            bgPanel.On = canvas.BgOn;
            bgPanel.Video = IsVideo;
            bgPanel.BlurThumb = canvas.BlurThumb;
            bgPanel.Dock = DockStyle.Left;
            bgPanel.Width = P(BgPanel.Wide);
            bgPanel.Visible = false;
            bgPanel.Changed += delegate
            {
                bool turned = canvas.BgOn != bgPanel.On;
                canvas.BgOn = bgPanel.On;
                canvas.Live = bgPanel.Sliding;
                dirty = true;
                bgTouched = true;
                if (turned) canvas.Invalidate();
                else canvas.BackdropChanged();
                UpdateUi();
            };
            bgPanel.Committed += delegate
            {
                canvas.Live = false; // back to full quality on release
                canvas.Invalidate();
                bgs.Save();          // remember the backdrop for next time
            };

            hint = new Hint();
            hint.S = s;
            hint.Dock = DockStyle.Bottom;
            hint.Height = P(34);
            if (!IsVideo)
            {
                hint.DragOut += StartDragOut;
                // Under the middle of the capture's area, beside the sidebar when it is open.
                hint.HandleCenter = delegate { return canvas.Left + canvas.Width / 2 - hint.Left; };
                canvas.SizeChanged += delegate { hint.Invalidate(); };
            }

            if (IsVideo && duration > 0)
            {
                timeline = new Timeline();
                timeline.S = s;
                timeline.Dock = DockStyle.Bottom;
                timeline.Height = P(110);
                timeline.Init(duration);
                timeline.Format = IsGif ? 1 : 0;
                timeline.Quality = Math.Max(0, Math.Min(Timeline.Qualities.Length - 1, bgs.ExportQuality));
                VideoClip first = new VideoClip();
                first.Path = path;
                first.Duration = duration;
                first.Width = img.Width;
                first.Height = img.Height;
                clips.Add(first);
                ClipSeq.Layout(clips);
                timeline.Clips = clips;
                timeline.CanAddClips = ffmpeg != null;
                timeline.Trimmed += delegate { SyncPlayEnd(); dirty = true; UpdateUi(); };
                // Ctrl+Z also takes back trims, splits and cuts.
                timeline.Changing += canvas.Checkpoint;
                canvas.SaveExtra = delegate { return timeline.Save(); };
                canvas.LoadExtra = delegate(object o) { if (timeline.Load(o as Timeline.State)) { SyncPlayEnd(); dirty = true; UpdateUi(); } };
                timeline.OptionsChanged += delegate { RememberQuality(bgs, timeline.Quality); dirty = true; UpdateUi(); };
                timeline.Seek += OnTimelineSeek;
                timeline.AddClip += AskForClips;
                timeline.ClipRemove += RemoveClip;
                timeline.ClipMove += MoveClip;
            }

            // Docking order matters (the last added docks first): toolbar at the very top; at the bottom, the hint strip
            // and the timeline above it, both the whole width; the backdrop sidebar on the left between them.
            Controls.Add(canvas);
            Controls.Add(bgPanel);
            if (timeline != null) Controls.Add(timeline);
            Controls.Add(hint);
            Controls.Add(bar);
            // Video files dropped on the editor (on the form or on what covers it) are added after the first one.
            if (timeline != null)
                foreach (Control c in new Control[] { this, canvas, timeline, hint })
                {
                    c.AllowDrop = true;
                    c.DragEnter += OnClipDragEnter;
                    c.DragDrop += OnClipDrop;
                }

            // Size: fit the capture in 88% of the screen, never narrower than the toolbar (its compact form is the minimum).
            int minW = bar.LayoutItems(), fullW = bar.FullWidth;
            Rectangle wa = scr.WorkingArea;
            float pad = 28 * s;
            int chrome = P(52) + P(34) + P(Canvas.PillRoom - 28) + (timeline != null ? timeline.Height : 0); // bars, and the room kept for the zoom control
            float maxW = wa.Width * 0.88f - 2 * pad, maxH = wa.Height * 0.88f - chrome - 2 * pad;
            float kk = Math.Min(ShotStack.MaxZoom(img.Size), Math.Min(maxW / img.Width, maxH / img.Height));
            // Tall captures (scrolling ones) get a window as wide as they read best and as tall as the screen allows.
            float kw = img.Height > img.Width * 2.2f ? Math.Min(1f, maxW / img.Width) : kk;
            // No system frame: the window size is the client size.
            Size = new Size(Math.Min(wa.Width, Math.Max(fullW, (int)(img.Width * kw + 2 * pad))),
                            Math.Min(wa.Height, chrome + Math.Max(P(260), kw != kk ? (int)maxH : (int)(img.Height * kk + 2 * pad))));
            MinimumSize = new Size(Math.Min(wa.Width, minW), P(380));
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2), wa.Top + Math.Max(0, (wa.Height - Height) / 2));

            feedback.Interval = 1300;
            feedback.Tick += delegate
            {
                feedback.Stop();
                if (copyItem != null) copyItem.ShowAlt = false;
                if (saveItem != null) saveItem.ShowAlt = false;
                if (pdfItem != null) pdfItem.ShowAlt = false;
                bar.Sync();
            };
            foreach (Control c in new Control[] { bar, hint, canvas, bgPanel, timeline })
                if (c != null) new FrameHook(this, c);
            onDs = delegate
            {
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)Restyle); } catch { }
            };
            Ds.Changed += onDs;
            open.Add(this);
            UpdateUi();
        }

        readonly Action onDs;
        readonly Bar.Item maxItem;

        // Theme switched in the app: new colors everywhere and a matching window frame.
        void Restyle()
        {
            if (IsDisposed) return;
            BackColor = EdLook.C(Ds.Brushes.Window);
            ForeColor = EdLook.C(Ds.Brushes.Label);
            bar.BackColor = hint.BackColor = bgPanel.BackColor = BackColor;
            if (timeline != null) timeline.BackColor = BackColor;
            canvas.BackColor = EdLook.Surround;
            ApplyFrame();
            Invalidate(true);
        }

        void CaptionClick(int i)
        {
            if (i == 0) WindowState = FormWindowState.Minimized;
            else if (i == 1) WindowState = IsZoomed(Handle) ? FormWindowState.Normal : FormWindowState.Maximized;
            else Close();
        }

        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hWnd);

        const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                  HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        // What the point (screen coordinates) is in terms of the window frame: resize edges, the draggable toolbar or
        // plain client area.
        internal int FrameHit(Point screen)
        {
            Point p = PointToClient(screen);
            if (!IsZoomed(Handle))
            {
                int b = P(6), c = P(14);
                bool l = p.X < b, r = p.X >= Width - b, t = p.Y < b, bo = p.Y >= Height - b;
                bool lc = p.X < c, rc = p.X >= Width - c, tc = p.Y < c, bc = p.Y >= Height - c;
                if ((t && lc) || (l && tc)) return HTTOPLEFT;
                if ((t && rc) || (r && tc)) return HTTOPRIGHT;
                if ((bo && lc) || (l && bc)) return HTBOTTOMLEFT;
                if ((bo && rc) || (r && bc)) return HTBOTTOMRIGHT;
                if (l) return HTLEFT;
                if (r) return HTRIGHT;
                if (t) return HTTOP;
                if (bo) return HTBOTTOM;
            }
            if (bar.Visible && bar.Bounds.Contains(p) && bar.IsEmptyAt(bar.PointToClient(screen))) return HTCAPTION;
            return HTCLIENT;
        }

        static Point PointFrom(IntPtr lParam)
        {
            long v = lParam.ToInt64();
            return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x83 && m.WParam != IntPtr.Zero) // WM_NCCALCSIZE: the client area is the whole window
            {
                if (IsZoomed(m.HWnd))
                {
                    // Maximized windows overhang the screen by the frame width: keep the content on the work area.
                    Native.RECT r = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                    Rectangle wa = Screen.FromRectangle(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)).WorkingArea;
                    r.Left = wa.Left; r.Top = wa.Top; r.Right = wa.Right; r.Bottom = wa.Bottom;
                    Marshal.StructureToPtr(r, m.LParam, false);
                }
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x84) // WM_NCHITTEST
            {
                m.Result = (IntPtr)FrameHit(PointFrom(m.LParam));
                return;
            }
            if (m.Msg == 0x02E0) // WM_DPICHANGED: moved to a monitor with another scale
            {
                Native.RECT r = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                Rescale((m.WParam.ToInt64() & 0xFFFF) / 96f, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // Everything laid out again at the new monitor's scale, in the rectangle Windows suggests.
        void Rescale(float scale, Rectangle suggested)
        {
            if (scale <= 0 || Math.Abs(scale - s) < 0.001f) return;
            s = scale;
            EdTip.Cancel();
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            bar.S = s;
            hint.S = s;
            bgPanel.S = s;
            canvas.Ui = s;
            if (timeline != null) timeline.S = s;
            MinimumSize = Size.Empty;
            SuspendLayout();
            bar.Height = P(52);
            hint.Height = P(34);
            if (timeline != null) timeline.Height = P(110);
            bgPanel.Width = P(BgPanel.Wide);
            Bounds = suggested;
            ResumeLayout(true);
            bgPanel.Relayout();
            MinimumSize = new Size(Math.Min(Screen.FromRectangle(suggested).WorkingArea.Width, bar.LayoutItems()), P(380));
            canvas.Rescaled();
            Invalidate(true);
        }

        // Children let the frame edges and the empty toolbar through to the window.
        sealed class FrameHook : NativeWindow
        {
            readonly Editor ed;

            public FrameHook(Editor ed, Control c)
            {
                this.ed = ed;
                if (c.IsHandleCreated) AssignHandle(c.Handle);
                c.HandleCreated += delegate { AssignHandle(c.Handle); };
                c.HandleDestroyed += delegate { ReleaseHandle(); };
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84 && !ed.IsDisposed && ed.FrameHit(PointFrom(m.LParam)) != HTCLIENT)
                {
                    m.Result = (IntPtr)(-1); // HTTRANSPARENT
                    return;
                }
                base.WndProc(ref m);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (playing && WindowState == FormWindowState.Minimized) PausePlay();
            if (bar == null || !IsHandleCreated) return;
            bool z = IsZoomed(Handle);
            if (z != bar.Maximized)
            {
                bar.Maximized = z;
                if (maxItem != null) maxItem.Tip = z ? I18n.T("Restaurar", "Restore") : I18n.T("Maximizar", "Maximize");
                bar.Invalidate();
            }
        }

        bool IsVideo { get { return ffmpeg != null; } }
        bool IsGif { get { return IsVideo && Path.GetExtension(path).ToLowerInvariant() == ".gif"; } }
        // A lossless Cine master: what gets shared is an MP4 exported from it, and the master itself never changes.
        // Cine masters are RGB: any other Matroska (OBS, an older recording) is lossy and gets the usual texts.
        bool IsMaster { get { return IsVideo && rgbSource && string.Equals(Path.GetExtension(path), Cine.Extension, StringComparison.OrdinalIgnoreCase); } }
        bool ExportsGif { get { return timeline != null ? timeline.Format == 1 : IsGif; } }

        string ExportTip()
        {
            if (ExportsGif) return I18n.T("Crear un GIF con el recorte, las marcas, el fondo y las opciones de abajo  (Enter)", "Create a GIF with trims, annotations, backdrop and the options below  (Enter)");
            if (IsMaster) return I18n.T("Exportar para compartir: crea un MP4 (comprimido) con el recorte, las marcas, el fondo y las opciones de abajo. El m\u00E1ster sin p\u00E9rdidas no cambia  (Enter)", "Export to share: creates a compressed MP4 with trims, annotations, backdrop and the options below. The lossless master remains unchanged  (Enter)");
            return I18n.T("Crear un MP4 con el recorte, las marcas, el fondo y las opciones de abajo; el original no cambia  (Enter)", "Create an MP4 with trims, annotations, backdrop and options below; the original is unchanged  (Enter)");
        }

        int P(float v) { return (int)Math.Round(v * s); }

        Bar.Item Button(string label, string glyph, string tip, Action act, bool right)
        {
            Bar.Item it = bar.Add(Bar.Kind.Button);
            it.Label = label;
            it.Glyph = glyph;
            it.Tip = tip;
            it.Do = act;
            it.Right = right;
            return it;
        }

        void SetTool(Tool t)
        {
            if (IsVideo && t == Tool.Pixelate) return;
            canvas.SetTool(t);
        }
        void SetColor(int i) { canvas.SetColor(Theme.Palette[i]); }
        void SetWeight(int i) { canvas.SetWeight(i); }

        // Opening the backdrop sidebar with no backdrop applies the last one used.
        void ToggleBgPanel()
        {
            bool show = !bgPanel.Visible;
            if (show)
            {
                bgPanel.Relayout();   // the user's pictures may have changed since
                // Room for the capture beside the sidebar: a narrow window grows (within the screen) rather than squeeze it.
                int need = bgPanel.Width + P(420) - ClientSize.Width;
                if (need > 0 && WindowState == FormWindowState.Normal && IsHandleCreated && Visible)
                {
                    Rectangle wa = Screen.FromControl(this).WorkingArea;
                    int w = Math.Min(wa.Width, Width + need);
                    int x = Math.Max(wa.Left, Math.Min(Left - (w - Width) / 2, wa.Right - w));
                    Bounds = new Rectangle(x, Top, w, Height);
                }
                if (!canvas.BgOn)
                {
                    canvas.BgOn = true;
                    bgPanel.On = true;
                    dirty = true;
                    bgTouched = true;
                }
            }
            bgPanel.Visible = show;
            canvas.Invalidate();
            UpdateUi();
        }

        // Which options a kind of mark actually uses (the drawing code ignores the rest).
        static bool UsesColor(Tool t) { return t != Tool.Pixelate && t != Tool.Redact && t != Tool.Crop && t != Tool.Spotlight; }
        static bool UsesWeight(Tool t) { return t == Tool.Arrow || t == Tool.Line || t == Tool.Rect || t == Tool.Ellipse || t == Tool.Text || t == Tool.Counter; }

        // The toolbar reflects the tool and the selected shape's color and weight (or the next shape's); only the
        // options that apply to it are shown, and for text and numbers the weight reads as a size.
        void UpdateUi()
        {
            foreach (KeyValuePair<Tool, Bar.Item> kv in toolItems) kv.Value.On = kv.Key == canvas.Tool;
            int c = canvas.ActiveColor.ToArgb(), w = canvas.ActiveWeight;
            Tool kind = canvas.ActiveKind;
            bool sized = kind == Tool.Text || kind == Tool.Counter;
            foreach (Bar.Item it in bar.Items)
            {
                if (it.Group == 1) it.Visible = UsesColor(kind);
                else if (it.Group == 2) it.Visible = UsesWeight(kind);
            }
            for (int i = 0; i < colorItems.Count; i++) colorItems[i].On = Theme.Palette[i].ToArgb() == c;
            for (int i = 0; i < weightItems.Count; i++)
            {
                weightItems[i].On = i == w;
                weightItems[i].Sized = sized;
                weightItems[i].Tip = (sized ? (I18n.IsSpanish ? "Tama\u00F1o: " : "Size: ") + SizeNames[i] : (I18n.IsSpanish ? "Grosor: " : "Thickness: ") + WeightNames[i]) + "  (\u2212 / +)";
            }
            undoItem.Enabled = canvas.CanUndo;
            redoItem.Enabled = canvas.CanRedo;
            bgItem.On = canvas.BgOn || bgPanel.Visible;
            if (exportItem != null) exportItem.Tip = ExportTip();
            bar.Sync();
            if (bgPanel.Visible && !bgPanel.Sliding) bgPanel.Invalidate();   // a slider being dragged repaints only itself
            if (Exporting) return; // the hint strip shows export progress
            Size o = canvas.OutputSize;
            string action = !IsVideo ? I18n.T("Enter copia y cierra", "Enter copies and closes")
                          : ExportsGif ? I18n.T("Enter crea el GIF", "Enter creates GIF")
                          : IsMaster ? I18n.T("Enter crea un MP4 para compartir", "Enter creates shareable MP4")
                          : I18n.T("Enter exporta", "Enter exports");
            string left = flashText ?? (playing ? I18n.T("Reproduciendo \u00B7 Espacio pausa", "Playing \u00B7 Space pauses") : canvas.HintText), right = o.Width + " \u00D7 " + o.Height + " px  \u00B7  " + action;
            // Marks on a video are drawn over all of it: said while choosing where to put one.
            if (IsVideo && flashText == null && !playing && !canvas.HasSelection && canvas.Tool != Tool.Crop) left += I18n.T(" \u00B7 se ve durante todo el v\u00EDdeo", " \u00B7 visible throughout video");
            if (left != hint.LeftText || right != hint.RightText)
            {
                hint.LeftText = left;
                hint.RightText = right;
                hint.Invalidate();
            }
            string title = (IsVideo ? I18n.T("Editar v\u00EDdeo \u00B7 ", "Edit video \u00B7 ") : I18n.T("Editar \u00B7 ", "Edit \u00B7 ")) + Path.GetFileName(path) + (Unsaved ? "  \u2022" : "");
            if (title != Text) Text = title;
        }

        // A tip left showing (it floats above everything) would linger over other apps.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            EdTip.Cancel();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyFrame();
        }

        // Frame drawn by Windows (border, shadow, rounded corners) in the app's light or dark mode.
        void ApplyFrame()
        {
            try
            {
                int dark = Ds.Dark ? 1 : 0;
                Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);                     // DWMWA_USE_IMMERSIVE_DARK_MODE
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);                    // DWMWA_WINDOW_CORNER_PREFERENCE: round
                int auto = -1;
                Native.DwmSetWindowAttribute(Handle, 34, ref auto, 4);                     // DWMWA_BORDER_COLOR: default
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            canvas.Focus();
            if (timeline != null) LoadThumbs(clips[0]); // needs the window handle to hand the frames back
        }

        public void BringUp()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.ForceForeground(Handle);
            canvas.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Exporting)
            {
                if (keyData == Keys.Escape) { StopExport(false); return true; }
                return base.ProcessCmdKey(ref msg, keyData); // exporting: nothing else is allowed
            }
            if (canvas.Typing) return base.ProcessCmdKey(ref msg, keyData);
            if (canvas.Busy)
            {
                // Mid-drag only Esc counts (it puts the mark back); shortcuts wait for the mouse to be let go.
                if (keyData == Keys.Escape) { canvas.CancelGesture(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            int step = (keyData & Keys.Shift) != 0 ? 10 : 1;
            switch (keyData & ~Keys.Shift)
            {
                case Keys.Left: if (canvas.HasSelection) { canvas.Nudge(-step, 0); return true; } if (StepHead(-1, step > 1)) return true; break;
                case Keys.Right: if (canvas.HasSelection) { canvas.Nudge(step, 0); return true; } if (StepHead(1, step > 1)) return true; break;
                case Keys.Home: if (timeline != null && !canvas.HasSelection) { timeline.MoveHead(timeline.In); return true; } break;
                case Keys.End: if (timeline != null && !canvas.HasSelection) { timeline.MoveHead(timeline.Out); return true; } break;
                case Keys.Up: if (canvas.HasSelection) { canvas.Nudge(0, -step); return true; } break;
                case Keys.Down: if (canvas.HasSelection) { canvas.Nudge(0, step); return true; } break;
            }
            switch (keyData)
            {
                case Keys.Space: if (playTimer == null) break; TogglePlay(); return true;
                case Keys.Control | Keys.Z: canvas.Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: canvas.Redo(); return true;
                case Keys.Control | Keys.C: if (!IsVideo) CopyOut(); return true;
                case Keys.Control | Keys.Shift | Keys.C: if (!IsVideo) CopyTextOut(); return true;
                case Keys.Control | Keys.Shift | Keys.X: if (!IsVideo) FindSensitive(); return true;
                case Keys.Control | Keys.S: if (IsVideo) Done(); else KeepCopy(); return true;
                case Keys.Control | Keys.Shift | Keys.S: if (!IsVideo) SaveAs(false); return true;
                case Keys.Control | Keys.P: if (!IsVideo) SaveAs(true); return true;
                case Keys.Control | Keys.D: canvas.DuplicateSelected(); return true;
                case Keys.Control | Keys.W: Close(); return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0: canvas.ZoomFit(); return true;
                case Keys.Control | Keys.D1:
                case Keys.Control | Keys.NumPad1: canvas.ZoomActual(); return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add: canvas.ZoomBy(1.25f); return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract: canvas.ZoomBy(0.8f); return true;
                case Keys.Enter: Done(); return true;
                case Keys.Escape:
                    if (playing) PausePlay();
                    else if (canvas.HasSelection) canvas.SelectShape(null);
                    else if (bgPanel.Visible) ToggleBgPanel();
                    else Close();
                    return true;
                case Keys.Delete:
                case Keys.Back: canvas.DeleteSelected(); return true;
                case Keys.F: SetTool(Tool.Arrow); return true;
                case Keys.L: SetTool(Tool.Line); return true;
                case Keys.O: SetTool(Tool.Spotlight); return true;
                case Keys.R: SetTool(Tool.Rect); return true;
                case Keys.E: SetTool(Tool.Ellipse); return true;
                case Keys.T: SetTool(Tool.Text); return true;
                case Keys.N: SetTool(Tool.Counter); return true;
                case Keys.H: SetTool(Tool.Highlight); return true;
                case Keys.P: SetTool(Tool.Pixelate); return true;
                case Keys.X: SetTool(Tool.Redact); return true;
                case Keys.C: SetTool(Tool.Crop); return true;
                case Keys.B: ToggleBgPanel(); return true;
                case Keys.D1: case Keys.NumPad1: SetColor(0); return true;
                case Keys.D2: case Keys.NumPad2: SetColor(1); return true;
                case Keys.D3: case Keys.NumPad3: SetColor(2); return true;
                case Keys.D4: case Keys.NumPad4: SetColor(3); return true;
                case Keys.D5: case Keys.NumPad5: SetColor(4); return true;
                case Keys.OemMinus: case Keys.Subtract: SetWeight(canvas.ActiveWeight - 1); return true;
                case Keys.Oemplus: case Keys.Add: SetWeight(canvas.ActiveWeight + 1); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Arrows on a video with no mark selected: a frame back or forward (with Shift, a second), as in the player.
        bool StepHead(int dir, bool second)
        {
            if (timeline == null || timeline.Dragging) return false;
            if (playing) PausePlay();
            double frame = 1.0 / Math.Max(1, sourceFps);
            timeline.MoveHead(timeline.Playhead + dir * (second ? 1.0 : frame));
            return true;
        }

        static ImageFormat FormatFor(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg") return ImageFormat.Jpeg;
            if (ext == ".bmp") return ImageFormat.Bmp;
            if (ext == ".tif" || ext == ".tiff") return ImageFormat.Tiff;
            return ImageFormat.Png;
        }

        // JPEG at quality 95 over white: GDI+ defaults to 75, which visibly degrades the picture every time it is saved.
        internal static void WriteImage(Bitmap b, Stream s, ImageFormat f)
        {
            if (f.Guid != ImageFormat.Jpeg.Guid) { b.Save(s, f); return; }
            ImageCodecInfo jpg = null;
            foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders()) if (ci.FormatID == ImageFormat.Jpeg.Guid) jpg = ci;
            using (Bitmap flat = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb))
            using (EncoderParameters ep = new EncoderParameters(1))
            {
                using (Graphics g = Graphics.FromImage(flat))
                {
                    g.Clear(Color.White);
                    g.DrawImage(b, new Rectangle(0, 0, b.Width, b.Height));
                }
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                if (jpg != null) flat.Save(s, jpg, ep);
                else flat.Save(s, ImageFormat.Jpeg);
            }
        }

        // Writes annotations, crop and backdrop to the temp capture, and refreshes the saved copy if there is one.
        bool Apply()
        {
            Stall.Mark("guardar en el editor");
            canvas.CommitText();
            try
            {
                using (Bitmap b = canvas.Render())
                {
                    // Newer than anything still waiting to be written in the background, which then skips its write.
                    lock (backgroundLock) { long l; newest.TryGetValue(path, out l); newest[path] = l + 1; }
                    lock (writeLock) Write(b, path);
                }
                Applied();
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                Dialogs.Show(this, I18n.T("No se pudo guardar: ", "Could not save: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Encoded in memory, written to a temporary file and only then swapped in: a failed save keeps the old file.
        internal static void Write(Bitmap b, string file)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteImage(b, ms, FormatFor(file));
                AtomicFile.Write(file, delegate(Stream s) { ms.WriteTo(s); });
            }
        }

        // Like Apply, without making the user wait for the disk: the picture is made here (it needs the marks), the
        // encoding and the write run on a worker thread. Until it is written the file counts as being written
        // (ShotStack.WaitWritten), so a copy made right away still gives the new picture and the thumbnail, saving and
        // dragging wait for it on their own threads. then runs once it is on disk. The standalone editor keeps Apply.
        bool ApplyInBackground(Action then)
        {
            Stall.Mark("guardar en el editor");
            if (owner == null)
            {
                if (!Apply()) return false;
                if (then != null) then();
                return true;
            }
            canvas.CommitText();
            Bitmap b;
            try { b = canvas.Render(); }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                Dialogs.Show(this, I18n.T("No se pudo guardar: ", "Could not save: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            string file = path;
            ShotStack stack = owner;
            if (canvas.BgOn) withBackdrop.Add(file);
            else if (!openedWithBackdrop) withBackdrop.Remove(file);
            written = true;
            dirty = false;
            UpdateUi();
            long seq;
            lock (backgroundLock)
            {
                backgroundWrites++;
                int n;
                inFlight.TryGetValue(file, out n);
                inFlight[file] = n + 1;
                if (n == 0) ShotStack.MarkWriting(file);
                long last;
                newest.TryGetValue(file, out last);
                seq = newest[file] = last + 1;
            }
            Editor ed = this;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Exception error = null;
                bool wrote = false;
                try
                {
                    // One at a time, and only the newest of the picture of a file: a quick second save never loses
                    // to the first one finishing later (which skips its write and leaves the rest to the newer one).
                    lock (writeLock)
                    {
                        long latest;
                        lock (backgroundLock) newest.TryGetValue(file, out latest);
                        if (latest == seq) { Write(b, file); wrote = true; }
                    }
                }
                catch (Exception ex) { error = ex; }
                finally
                {
                    b.Dispose();
                    lock (backgroundLock)
                    {
                        int n = inFlight[file] - 1;
                        if (n > 0) inFlight[file] = n;
                        else { inFlight.Remove(file); ShotStack.DoneWriting(file); }
                    }
                }
                Action after = delegate
                {
                    if (error != null)
                    {
                        ShotStack.Log("Guardar: " + error.Message);
                        // Still open: it has those changes again, to try once more.
                        if (!ed.IsDisposed) { ed.dirty = true; ed.written = false; ed.UpdateUi(); }
                        stack.Notify(I18n.T("No se pudo guardar la edici\u00F3n", "Could not save edit"), error.Message);
                        return;
                    }
                    if (!wrote) return;
                    try { if (stack.IsKept(file)) stack.Keep(file); }
                    catch (Exception ex) { ShotStack.Log("Guardar (editor): " + ex.Message); }
                    if (then != null) then();
                };
                // Quitting: the interface thread is waiting for this write (WaitBackgroundWrites), so what follows it
                // runs here and is done before the app goes.
                if (stack.Exiting) { try { after(); } catch (Exception ex) { ShotStack.Log("Guardar (editor): " + ex.Message); } }
                else stack.Ui(after);
                lock (backgroundLock)
                {
                    backgroundWrites--;
                    System.Threading.Monitor.PulseAll(backgroundLock);
                }
            });
            return true;
        }

        static readonly object backgroundLock = new object(), writeLock = new object();
        static int backgroundWrites;
        static readonly Dictionary<string, int> inFlight = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, long> newest = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // At exit: edits still being written in the background finish first (at most a few seconds).
        public static void WaitBackgroundWrites()
        {
            DateTime end = DateTime.UtcNow.AddSeconds(10);
            lock (backgroundLock)
                while (backgroundWrites > 0 && DateTime.UtcNow < end) System.Threading.Monitor.Wait(backgroundLock, 250);
        }

        // The file now holds what the canvas shows.
        void Applied()
        {
            // The file has the backdrop now; without it, it only keeps one if it already had it when opened.
            if (canvas.BgOn) withBackdrop.Add(path);
            else if (!openedWithBackdrop) withBackdrop.Remove(path);
            written = true;
            dirty = false;
            UpdateUi();
            if (owner != null && owner.IsKept(path)) owner.Keep(path);
        }

        // Something to write: unapplied annotations or a backdrop not yet in the file.
        bool Pending { get { return dirty || (canvas.BgOn && !withBackdrop.Contains(path)); } }

        // What closing would lose: changes that still leave something to keep (marks, a crop, a backdrop added or adjusted,
        // trimming or export options), or a file already rewritten in this session. Undoing everything, or turning off
        // the automatic backdrop (the file never had it), leaves nothing to ask about.
        bool Unsaved
        {
            get
            {
                if (!dirty) return false;
                if (!canvas.MarksAsOpened || canvas.IsCropped || (canvas.BgOn && (!bgAtOpen || bgTouched))) return true;
                if (IsVideo) return timeline != null && (timeline.IsTrimmed || timeline.HasCuts || timeline.Speed != 0 || timeline.OutSize != 0 || (timeline.Format == 1) != IsGif || clips.Count > 1);
                return written;
            }
        }

        // Ctrl+Shift+S, and the PDF button (Ctrl+P) with PDF chosen: the result as PNG, JPG or PDF wherever the user
        // wants. Long captures become several A4-width pages in a PDF. Encoding and writing happen off the UI thread.
        void SaveAs(bool pdf)
        {
            canvas.CommitText();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int kind = pdf ? 3 : ext == ".jpg" || ext == ".jpeg" ? 2 : 1;
            string file;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = pdf ? I18n.T("Guardar como PDF", "Save as PDF") : I18n.T("Guardar como", "Save as");
                d.Filter = "PNG|*.png|JPG|*.jpg;*.jpeg|PDF|*.pdf";
                d.FilterIndex = kind;
                d.AddExtension = true;
                d.DefaultExt = kind == 3 ? "pdf" : kind == 2 ? "jpg" : "png";
                d.FileName = Path.GetFileNameWithoutExtension(path) + "." + d.DefaultExt;
                string dir = owner != null ? SaveFolders.Target(owner.Settings, DateTime.Now) : Path.GetDirectoryName(path);
                try { Directory.CreateDirectory(dir); d.InitialDirectory = dir; } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                file = d.FileName;
            }
            string fe = Path.GetExtension(file).ToLowerInvariant();
            bool asPdf = fe == ".pdf";
            Bitmap b;
            try { b = canvas.Render(); }
            catch (Exception ex) { SaveFailed(ex); return; }
            Flash(I18n.Fmt("Guardando {0}\u2026", "Saving {0}\u2026", Path.GetFileName(file)), false);
            lock (savingLock) saving++;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Exception error = null;
                try
                {
                    if (asPdf) Pdf.Save(b, file);
                    else Write(b, file);
                }
                catch (Exception ex) { error = ex; }
                finally
                {
                    b.Dispose();
                    lock (savingLock) { saving--; System.Threading.Monitor.PulseAll(savingLock); }
                }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed) return;
                        if (error != null) { Flash(null, false); SaveFailed(error); return; }
                        ShotStack.Log("Guardada como: " + file);
                        Flash(I18n.Fmt("Guardada: {0}", "Saved: {0}", Path.GetFileName(file)), true);
                        if (asPdf && pdfItem != null) Feedback(pdfItem);
                    });
                }
                catch { }
            });
        }

        // Saves still being written in the background. Opened on its own (--edit), the process ends with the window, so
        // closing waits for them: a file is never left half written.
        int saving;
        readonly object savingLock = new object();

        void WaitForSaves()
        {
            DateTime end = DateTime.UtcNow.AddSeconds(30);
            lock (savingLock)
                while (saving > 0 && DateTime.UtcNow < end) System.Threading.Monitor.Wait(savingLock, 250);
        }

        void SaveFailed(Exception ex)
        {
            ShotStack.Log("Guardar como: " + ex.Message);
            Dialogs.Show(this, I18n.T("No se pudo guardar: ", "Could not save: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // A short message in the hint strip (null: back to the usual help); timed ones fade back on their own.
        string flashText;
        Timer flashTimer;

        void Flash(string text, bool timed)
        {
            flashText = text;
            if (flashTimer == null)
            {
                flashTimer = new Timer();
                flashTimer.Interval = 2600;
                flashTimer.Tick += delegate { flashTimer.Stop(); flashText = null; UpdateUi(); };
            }
            flashTimer.Stop();
            if (text != null && timed) flashTimer.Start();
            UpdateUi();
        }

        void Feedback(Bar.Item it)
        {
            copyItem.ShowAlt = it == copyItem;
            saveItem.ShowAlt = it == saveItem;
            if (pdfItem != null) pdfItem.ShowAlt = it == pdfItem;
            bar.Sync();
            feedback.Stop();
            feedback.Start();
        }

        // With nothing pending, the file on disk is exactly this image: the clipboard PNG is read from it, not encoded again.
        void CopyOut() { CopyOut(true); }

        // fileOk false: the file may not hold this picture (still being written, and the write could fail), so the
        // clipboard's PNG is made from the picture itself instead of read back from the file.
        void CopyOut(bool fileOk)
        {
            canvas.CommitText();
            try
            {
                if (owner != null) owner.CopyTracked(path, canvas.Render(), false, fileOk && !Pending);
                else using (Bitmap b = canvas.Render()) ShotStack.CopyImage(b);
                Feedback(copyItem);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Copiar (editor): " + ex.Message);
                Dialogs.Show(this, I18n.T("No se pudo copiar: ", "Could not copy: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // The text in the image as it looks now (crop and notes included), read by Windows' OCR.
        void CopyTextOut()
        {
            canvas.CommitText();
            ShotStack stack = owner ?? ShotStack.Running;
            if (stack == null) return;
            Flash(I18n.T("Leyendo el texto\u2026", "Reading text\u2026"), false);
            try
            {
                stack.CopyText(canvas.Render(), true, delegate(bool ok, string message) { if (!IsDisposed) Flash(ok ? I18n.T("Texto copiado al portapapeles.", "Text copied to clipboard.") : message + ".", true); });
            }
            catch (Exception ex)
            {
                ShotStack.Log("Copiar texto (editor): " + ex.Message);
                Flash(I18n.T("No se ha podido leer el texto.", "Could not read text."), true);
            }
        }

        // A capture still in the temporary folder; anything else (opened from the save folder, Home or --edit) is the
        // user's own file, saved in place. An XML attachment opened from its private subfolder counts as temporary too.
        bool InTemp { get { return owner != null && (IsTempCapture(path, Settings.TempDir) || IsTempCapture(path, XmlAttachment.Folder)); } }

        internal static bool IsTempCapture(string file, string tempDir)
        {
            return AtomicFile.SameFolder(Path.GetDirectoryName(Path.GetFullPath(file)), tempDir);
        }

        // Save = keep a copy in the save folder; everything else is temporary. A file that already lives outside the
        // temporary folder is only rewritten: copying it would add "name (2).png" next to itself.
        void KeepCopy()
        {
            canvas.CommitText();
            Action keep = delegate
            {
                try
                {
                    if (InTemp) owner.Keep(path);
                    if (!IsDisposed) Feedback(saveItem);
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Guardar (editor): " + ex.Message);
                    if (!IsDisposed) Dialogs.Show(this, I18n.T("No se pudo guardar: ", "Could not save: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else if (owner != null) owner.Notify(I18n.T("No se pudo guardar", "Could not save"), ex.Message);
                }
            };
            if (Pending || InTemp) ApplyInBackground(keep);
            else keep();
        }

        // Enter: apply, copy and close, ready to paste or drag the thumbnail.
        // For videos: export with annotations and backdrop and add the result to the stack.
        // The clipboard is filled before closing, so a paste right after Enter never gets the previous one; the file is
        // written in the background, and the clipboard's PNG is made from the picture itself (not from that file).
        void Done()
        {
            if (IsVideo) { ExportVideo(); return; }
            canvas.CommitText(); // a text still being typed counts as a change to write
            bool writing = Pending;
            if (writing && !ApplyInBackground(null)) return;
            CopyOut(!writing);
            closeWithoutAsking = true;
            Close();
        }

        // Pins the annotated image on top of everything; the editor stays open.
        void PinOut()
        {
            canvas.CommitText();
            try { PinWindow.Open(canvas.Render()); }
            catch (Exception ex) { ShotStack.Log("Fijar (editor): " + ex.Message); Flash(I18n.T("No se pudo fijar la imagen en pantalla.", "Could not pin image to screen."), true); }
        }

        // Dragging from the button: the annotated image follows the cursor, centered.
        void StartDragOut()
        {
            canvas.CommitText();
            if (Pending && !Apply()) return;
            DragDropEffects r = DragDropEffects.None;
            try
            {
                Bitmap ghost;
                Point grab;
                using (Bitmap full = canvas.Render())
                {
                    grab = new Point(full.Width / 2, full.Height / 2);
                    ghost = FileDrag.Ghost(full, P(260), P(8), ref grab);
                }
                r = FileDrag.Run(this, path, ghost, grab);
            }
            catch (Exception ex) { ShotStack.Log("Arrastrar (editor): " + ex.Message); }
            if (r == DragDropEffects.None) return;
            if (owner != null) owner.RemoveByPath(path);
            closeWithoutAsking = true;
            Close();
        }

        // Opens the editor on a recording, annotating its first frame. Returns null without FFmpeg or if the file can't
        // be read.
        public static Editor ForVideo(ShotStack owner, string path)
        {
            Settings st = owner != null ? owner.Settings : Settings.Load();
            string ff = Recorder.FindFfmpeg(st);
            if (ff == null) ff = FfmpegSetup.Run(st);
            if (ff == null) return null;
            return FromPrepared(owner, path, ff, PrepareVideo(ff, path));
        }

        // What opening a recording needs from FFmpeg (its first frame, length and streams). It can take long on a network
        // drive or a damaged file, so the stack reads it off the UI thread and builds the window with FromPrepared.
        internal sealed class VideoPrep { public string Frame, Err, Failure; public double Duration; public MediaInfo Info; public bool Quiet; }

        internal static VideoPrep PrepareVideo(string ff, string path)
        {
            VideoPrep v = new VideoPrep();
            v.Frame = Path.Combine(Path.GetTempPath(), "stackshot-frame-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                string err;
                int code = RunFfmpeg(ff, "-hide_banner -y " + Recorder.SafeInput + "-i " + Recorder.Quote(path) + " -frames:v 1 " + Recorder.Quote(v.Frame), out err);
                v.Err = err;
                if (code != 0 || !File.Exists(v.Frame))
                {
                    ShotStack.Log("Presentar: no se pudo leer " + path + ": " + Tail(err));
                    v.Failure = Tail(err);
                    return v;
                }
                Match m = Regex.Match(err, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                if (m.Success)
                    v.Duration = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                if (v.Duration <= 0) v.Duration = MediaInfo.ScanDuration(ff, path);
                v.Info = MediaInfo.Probe(ff, path);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Presentar: " + ex.Message);
                v.Failure = "";
            }
            return v;
        }

        // The window for a prepared recording (UI thread); null when it could not be read, after saying so.
        internal static Editor FromPrepared(ShotStack owner, string path, string ff, VideoPrep v)
        {
            string frame = v.Frame, err = v.Err ?? "";
            try
            {
                if (v.Failure != null)
                {
                    if (!v.Quiet)
                        Dialogs.Show(I18n.T("No se pudo abrir la grabaci\u00F3n.", "Could not open recording.") + (v.Failure.Length > 0 ? "\n\n" + v.Failure : I18n.T("\n\nEl archivo no se puede leer ahora; vuelve a intentarlo.", "\n\nThe file cannot be read right now; please try again.")),
                                     "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                double dur = v.Duration;
                Editor e = new Editor(owner, path, ShotStack.LoadFull(frame), ff, dur);
                // The sound and the frame rate are kept in the export.
                e.hasAudio = Regex.IsMatch(err, @"Stream #0:\d+[^\n]*: Audio:");
                double fps;
                bool constant = ConstantRate(err, out fps);
                if (fps > 0) { e.sourceFps = fps; e.fpsKnown = constant; }
                e.rgbSource = VideoExport.IsRgb(path, err);
                if (e.clips.Count > 0)
                {
                    e.clips[0].Fps = e.sourceFps;
                    e.clips[0].HasAudio = e.hasAudio;
                    e.clips[0].Rgb = e.rgbSource;
                }
                e.SetupPlayer(v.Info ?? MediaInfo.Probe(ff, path));
                if (e.IsMaster)
                {
                    e.Flash(I18n.T("M\u00E1ster sin p\u00E9rdidas: Exportar crea un MP4 para compartir; el m\u00E1ster no cambia.", "Lossless master: Export creates a shareable MP4; the master does not change."), true);
                    e.flashTimer.Interval = 7000;
                }
                return e;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Presentar: " + ex.Message);
                return null;
            }
            finally
            {
                try { if (File.Exists(frame)) File.Delete(frame); } catch { }
            }
        }

        static int RunFfmpeg(string ff, string args, out string stderr)
        {
            return VideoExport.Run(ff, args, 30000, out stderr);
        }

        // Frame strip for the timeline: evenly spaced small frames of a clip, each with its own fast seek in the
        // background, shown as they arrive (a long recording no longer has to be decoded whole).
        void LoadThumbs(VideoClip clip)
        {
            double total = ClipSeq.Layout(clips);
            int n = clips.Count < 2 ? 14 : Math.Max(3, Math.Min(14, (int)Math.Round(14 * clip.Duration / Math.Max(1, total))));
            string ff = ffmpeg, src = clip.Path;
            // As tall as the strip in device pixels, so the frames stay sharp on HiDPI screens.
            int th = Math.Max(72, timeline.StripHeight + 1) & ~1;
            double dur = clip.Duration;
            Bitmap[] thumbs = new Bitmap[n];
            clip.Thumbs = thumbs;
            bool closed = false;
            FormClosed += delegate { closed = true; };
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    FrameStrip.Extract(ff, src, dur, n, th, delegate(int i, Bitmap b)
                    {
                        try
                        {
                            BeginInvoke((Action)delegate
                            {
                                if (closed || IsDisposed || clip.Thumbs != thumbs) { b.Dispose(); return; }
                                thumbs[i] = b;
                                timeline.Invalidate();
                            });
                        }
                        catch { b.Dispose(); }
                    }, delegate { return closed || clip.Thumbs != thumbs; });
                }
                catch (Exception ex) { ShotStack.Log("Tira de fotogramas: " + ex.Message); }
            });
        }

        // Shows the frame at timeline time t under the marks; only the latest request wins. A frame of another clip
        // is brought to the canvas the way the export does (fitted, black bars), so the marks stay where they are.
        void ShowFrameAt(double t)
        {
            ShowFrameAt(t, 0);
        }

        void ShowFrameAt(double t, double early)
        {
            int req = ++frameRequest;
            double local = 0;
            int ci = clips.Count > 0 ? ClipSeq.Locate(clips, t, out local) : -1;
            VideoClip clip = ci >= 0 ? clips[ci] : null;
            string ff = ffmpeg, src = clip != null ? clip.Path : path;
            if (clip != null)
            {
                // Never past the last frame, where FFmpeg would find nothing.
                double fps = clip.Fps > 0 ? clip.Fps : sourceFps;
                local = Math.Max(0, Math.Min(local - early, Math.Max(0, clip.Duration - 0.5 / fps)));
            }
            else local = Math.Max(0, t - early);
            Size cs = canvasSize;
            double at = local;
            string png = Path.Combine(Path.GetTempPath(), "stackshot-frame-" + Guid.NewGuid().ToString("N") + ".png");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap bmp = null;
                try
                {
                    string err;
                    if (RunFfmpeg(ff, "-hide_banner -y " + PlayerArgs.Input(src, at) +
                                      " -frames:v 1 " + Recorder.Quote(png), out err) == 0 && File.Exists(png))
                        bmp = ShotStack.LoadFull(png);
                    if (bmp != null && (bmp.Width != cs.Width || bmp.Height != cs.Height)) { Bitmap raw = bmp; bmp = null; bmp = OnCanvas(raw, cs); }
                }
                catch (Exception ex) { ShotStack.Log("Fotograma: " + ex.Message); }
                finally { try { if (File.Exists(png)) File.Delete(png); } catch { } }
                if (bmp == null) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (req != frameRequest || IsDisposed || playing) bmp.Dispose();
                        else canvas.ReplaceImage(bmp);
                    });
                }
                catch { bmp.Dispose(); }
            });
        }

        // The picture of another clip on a black canvas of the first clip's size, as large as fits and centred.
        internal static Bitmap OnCanvas(Bitmap src, Size canvasSize)
        {
            Bitmap r = new Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(r))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(src, ClipSeq.Fit(src.Size, canvasSize));
                }
                return r;
            }
            catch { r.Dispose(); throw; }
            finally { src.Dispose(); }
        }

        static string Tail(string t)
        {
            t = (t ?? "").Trim();
            return t.Length > 300 ? t.Substring(t.Length - 300) : t;
        }

        // ---- playback: the built-in player draws its frames on the canvas; pausing puts the exact frame back ----
        // Each clip has its own Playback, made when it is first played. Playing crosses a clip boundary by starting the
        // next clip's Playback when the current one ends (a gap of a few tens of milliseconds, under 100 ms).

        Playback play;       // the one of the clip being played
        int playClip;        // that clip's index
        MediaInfo media;     // of the first clip: its sound tracks name the menu
        Timer playTimer;
        bool playing;
        int audioTrack;
        double volume = 1;
        // The picture on screen and where playback paused (timeline times), to resume without starting again.
        double shownTime = -1, pausedAt = -1;
        readonly object shownLock = new object();
        // A clip that is not the canvas's shape is shown fitted on black, composed here from the frames FFmpeg decodes.
        byte[] composed;
        int composedW, composedH;

        void SetupPlayer(MediaInfo mi)
        {
            media = mi;
            if (timeline == null || mi == null || !mi.CanPlay) return;
            VideoClip first = clips[0];
            first.Info = mi;
            if (mi.Fps > 0) first.Fps = mi.Fps;
            first.HasAudio = mi.Audio.Count > 0;
            first.Rgb = rgbSource;
            first.TakeColour(mi);
            timeline.CanPlay = true;
            timeline.HasSound = ClipSeq.AnyAudio(clips);
            foreach (AudioTrackInfo a in mi.Audio) if (a.Default) { audioTrack = a.Index; break; }
            timeline.PlayToggle += TogglePlay;
            timeline.SoundClick += ShowSoundMenu;
            canvas.DrawStart += delegate { if (playing) PausePlay(); };
            playTimer = new Timer();
            playTimer.Interval = 33;
            playTimer.Tick += PlayTick;
        }

        double ShownTime { get { lock (shownLock) return shownTime; } }

        // The tracks of the first clip are numbered by the menu; a clip with fewer tracks plays its default one.
        int TrackFor(VideoClip c)
        {
            return VideoExport.ClipTrack(c, audioTrack);
        }

        // Where playback of a clip stops, in the clip's own time: its end, or the out handle if that comes first.
        double LocalEnd(VideoClip c)
        {
            return Math.Max(0.05, Math.Min(c.Duration, timeline.Out - c.Offset));
        }

        void SyncPlayEnd()
        {
            if (play != null && playClip < clips.Count) play.EndTime = LocalEnd(clips[playClip]);
        }

        void OnTimelineSeek(double t)
        {
            SyncPlayEnd();
            pausedAt = -1;
            if (!playing) { ShowFrameAt(t); return; }
            double local;
            int ci = ClipSeq.Locate(clips, t, out local);
            if (play != null && ci == playClip) play.Seek(local);
            else if (!PlayFrom(t, false)) PlayFailed(I18n.Fmt("No se puede reproducir {0}.", "Cannot play {0}.", clips[ci].Name));
        }

        void TogglePlay()
        {
            if (playTimer == null || Exporting) return;
            if (playing) PausePlay(); else StartPlay();
        }

        Playback PlayFor(int ci)
        {
            VideoClip c = clips[ci];
            if (c.Play != null) return c.Play;
            if (c.Info == null || !c.Info.CanPlay) return null;
            try
            {
                Playback p = new Playback(ffmpeg, c.Path, c.Info);
                p.Track = TrackFor(c);
                p.Volume = volume;
                p.Skip = delegate(double t)
                {
                    double g = timeline.PlayableFrom(c.Offset + t);
                    if (double.IsPositiveInfinity(g) || g - c.Offset >= c.Duration - 0.001) return double.PositiveInfinity;
                    return g - c.Offset;
                };
                p.FrameReady += delegate(PlayerFrame f) { ShowPlayFrame(c, f); };
                p.Ended += delegate { OnUi(delegate { ClipEnded(c); }); };
                p.Error += delegate(string m) { OnUi(delegate { PlayFailed(m); }); };
                p.Notice += delegate(string m) { OnUi(delegate { Flash(m, true); }); };
                c.Play = p;
                return p;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: " + ex.Message);
                return null;
            }
        }

        // Engine thread: hands a decoded frame to the canvas, on a black canvas-shaped buffer when the clip is not the
        // canvas's shape.
        void ShowPlayFrame(VideoClip c, PlayerFrame f)
        {
            Size cs = canvasSize;
            Rectangle fit = ClipSeq.Fit(new Size(c.Width, c.Height), cs);
            if (fit.Size == cs || fit.Width <= 0)
            {
                canvas.ShowPlayFrame(f.Data, f.Width, f.Height);
            }
            else
            {
                // The frame is the fitted part at some scale; the buffer is the whole canvas at that scale.
                double k = f.Width / (double)fit.Width;
                int cw = Math.Max(f.Width, (int)Math.Round(cs.Width * k)), ch = Math.Max(f.Height, (int)Math.Round(cs.Height * k));
                lock (shownLock)
                {
                    if (composed == null || composedW != cw || composedH != ch)
                    {
                        composed = new byte[cw * ch * 4];
                        for (int i = 3; i < composed.Length; i += 4) composed[i] = 255;
                        composedW = cw;
                        composedH = ch;
                    }
                    int ox = (cw - f.Width) / 2, oy = (ch - f.Height) / 2;
                    for (int y = 0; y < f.Height; y++) Buffer.BlockCopy(f.Data, y * f.Width * 4, composed, ((oy + y) * cw + ox) * 4, f.Width * 4);
                    canvas.ShowPlayFrame(composed, cw, ch);
                }
            }
            lock (shownLock) shownTime = c.Offset + f.Time;
        }

        void OnUi(Action a)
        {
            try { BeginInvoke((Action)delegate { if (!IsDisposed) a(); }); }
            catch (InvalidOperationException) { }
        }

        // Starts (or resumes, when it paused exactly there) the clip that holds timeline time g.
        bool PlayFrom(double g, bool allowResume)
        {
            double local;
            int ci = ClipSeq.Locate(clips, g, out local);
            Playback p = PlayFor(ci);
            if (p == null) return false;
            if (play != null && play != p) play.Stop();
            play = p;
            playClip = ci;
            VideoClip c = clips[ci];
            // Decoded no larger than the canvas shows it (a quarter to full size of the clip's part of the canvas).
            Rectangle fit = ClipSeq.Fit(new Size(c.Width, c.Height), canvasSize);
            double k = Math.Max(0.25, Math.Min(1, canvas.ZoomPercent / 100.0));
            p.SetDecodeSize((int)Math.Round(fit.Width * k), (int)Math.Round(fit.Height * k));
            double end = LocalEnd(c);
            p.EndTime = end;
            if (allowResume && p.State == PlayState.Paused && pausedAt >= 0 && Math.Abs(g - pausedAt) < 0.0005) p.Resume();
            else p.Start(local, end);
            return true;
        }

        void StartPlay()
        {
            canvas.CommitText();
            frameRequest++; // an exact frame still on its way is no longer wanted
            double slack = Math.Max(0.05, 1.5 / sourceFps);
            double t = timeline.PlayableFrom(timeline.Playhead);
            // At the end (or past it): from the start of what is kept.
            if (double.IsPositiveInfinity(t) || t >= timeline.Out - slack) t = timeline.PlayableFrom(timeline.In);
            if (double.IsPositiveInfinity(t)) return;
            lock (shownLock) shownTime = -1;
            playing = true;
            timeline.Playing = true;
            if (flashText != null) Flash(null, false);
            if (!PlayFrom(t, true))
            {
                playing = false;
                timeline.Playing = false;
                Flash(I18n.T("No se pudo iniciar la reproducci\u00F3n.", "Could not start playback."), true);
                return;
            }
            pausedAt = -1;
            playTimer.Start();
            UpdateUi();
        }

        // Pausing, or anything that needs the video still: the exact frame at the playhead is fetched, so marks are
        // placed on full-resolution pixels.
        void PausePlay()
        {
            if (!playing) return;
            playing = false;
            playTimer.Stop();
            timeline.Playing = false;
            if (play != null) play.Pause();
            ShowStill();
            UpdateUi();
        }

        void ShowStill()
        {
            double t = ShownTime;
            if (t < 0 && play != null) t = clips[playClip].Offset + play.Position;
            if (t < 0) t = timeline.Playhead;
            t = Math.Max(timeline.In, Math.Min(timeline.Out, t));
            timeline.Playhead = t;
            timeline.Invalidate();
            pausedAt = t;
            double local;
            VideoClip c = clips[ClipSeq.Locate(clips, t, out local)];
            // A quarter of a frame early, so the seek lands on the picture that was showing.
            ShowFrameAt(t, 0.25 / (c.Fps > 0 ? c.Fps : sourceFps));
        }

        // A clip played to its end (or to the out handle): on to the next kept part, which may be in the next clip.
        void ClipEnded(VideoClip c)
        {
            if (!playing || play == null || play != c.Play || play.State == PlayState.Playing) return;
            double next = timeline.PlayableFrom(c.End);
            if (double.IsPositiveInfinity(next)) { PlayEnded(); return; }
            if (!PlayFrom(next, false)) PlayFailed(I18n.T("No se puede reproducir el siguiente v\u00EDdeo.", "Could not play the next video."));
        }

        void PlayEnded()
        {
            if (!playing) return;
            playing = false;
            playTimer.Stop();
            timeline.Playing = false;
            ShowStill();
            pausedAt = -1;
            UpdateUi();
        }

        void PlayFailed(string message)
        {
            if (!playing) return;
            playing = false;
            playTimer.Stop();
            timeline.Playing = false;
            ShowStill();
            pausedAt = -1;
            int nl = (message ?? "").IndexOf('\n');
            Flash(nl > 0 ? message.Substring(0, nl) : message, true);
        }

        void PlayTick(object sender, EventArgs e)
        {
            if (!playing || play == null || timeline.Dragging) return;
            timeline.SetPlayhead(Math.Max(timeline.In, Math.Min(timeline.Out, clips[playClip].Offset + play.Position)));
        }

        // Every player is idle, with its processes gone: an export is starting or the window is closing.
        void StopPlayer()
        {
            if (playing) PausePlay();
            foreach (VideoClip c in clips) if (c.Play != null) c.Play.Stop();
        }

        void ShowSoundMenu(Point at)
        {
            ContextMenuStrip m = TrayMenu.Create();
            if (media.Audio.Count > 1)
            {
                foreach (AudioTrackInfo a in media.Audio)
                {
                    int idx = a.Index;
                    ToolStripMenuItem it = TrayMenu.Item(string.IsNullOrEmpty(a.Title) ? I18n.Fmt("Pista {0}", "Track {0}", idx + 1) : a.Title, null, delegate { SetTrack(idx); });
                    it.Checked = idx == audioTrack;
                    m.Items.Add(it);
                }
                m.Items.Add(TrayMenu.Separator());
            }
            string[] names = { I18n.T("Silencio", "Mute"), "25 %", "50 %", "75 %", "100 %" };
            double[] steps = { 0, 0.25, 0.5, 0.75, 1 };
            for (int i = 0; i < names.Length; i++)
            {
                double v = steps[i];
                ToolStripMenuItem it = TrayMenu.Item(names[i], null, delegate { SetVolume(v); });
                it.Checked = Math.Abs(volume - v) < 0.001;
                m.Items.Add(it);
            }
            m.Closed += delegate { OnUi(delegate { m.Dispose(); }); };
            m.Show(at);
        }

        void SetTrack(int i)
        {
            audioTrack = i;
            foreach (VideoClip c in clips) if (c.Play != null) c.Play.Track = TrackFor(c);
        }

        void SetVolume(double v)
        {
            volume = v;
            timeline.Muted = v == 0;
            foreach (VideoClip c in clips) if (c.Play != null) c.Play.Volume = v;
        }

        // ---- several videos: appended, removed and moved on the timeline ----

        // The export quality chosen in the timeline is the one used next time.
        internal static void RememberQuality(Settings s, int level)
        {
            if (level < 0 || level >= Timeline.Qualities.Length || s.ExportQuality == level) return;
            s.ExportQuality = level;
            s.Save();
        }

        void AskForClips()
        {
            if (Exporting || timeline == null) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = I18n.T("A\u00F1adir v\u00EDdeo", "Add video");
                d.Filter = ShotStack.VideoFilter;
                d.Multiselect = true;
                d.CheckFileExists = true;
                try { d.InitialDirectory = Path.GetDirectoryName(clips[clips.Count - 1].Path); } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                AddClips(d.FileNames);
            }
        }

        void OnClipDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = timeline != null && !Exporting && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnClipDrop(object sender, DragEventArgs e)
        {
            if (timeline == null || Exporting || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) AddClips(files);
        }

        // Probes the files in the background; the readable ones are appended in order, the others are named in one message.
        void AddClips(string[] files)
        {
            if (files == null || files.Length == 0 || timeline == null || Exporting) return;
            if (playing) PausePlay();
            string ff = ffmpeg;
            string[] list = (string[])files.Clone();
            Flash(list.Length == 1 ? I18n.Fmt("A\u00F1adiendo {0}\u2026", "Adding {0}\u2026", Path.GetFileName(list[0])) : I18n.Fmt("A\u00F1adiendo {0} v\u00EDdeos\u2026", "Adding {0} videos\u2026", list.Length), false);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> bad = new List<string>();
                List<KeyValuePair<string, MediaInfo>> good = new List<KeyValuePair<string, MediaInfo>>();
                foreach (string f in list)
                {
                    MediaInfo mi = File.Exists(f) ? MediaInfo.Probe(ff, f) : null;
                    if (mi != null && mi.CanPlay && mi.Duration > 0) good.Add(new KeyValuePair<string, MediaInfo>(f, mi));
                    else bad.Add(Path.GetFileName(f));
                }
                OnUi(delegate
                {
                    if (Exporting) { Flash(null, false); return; }
                    foreach (KeyValuePair<string, MediaInfo> g in good) AppendClip(g.Key, g.Value);
                    Flash(good.Count > 0 ? (good.Count == 1 ? I18n.Fmt("A\u00F1adido: {0}", "Added: {0}", Path.GetFileName(good[0].Key)) : I18n.Fmt("A\u00F1adido: {0} v\u00EDdeos", "Added: {0} videos", good.Count)) : null, true);
                    if (bad.Count > 0)
                        Dialogs.Show(this, I18n.T("No se pudo abrir como v\u00EDdeo:\n\n", "Could not open as video:\n\n") + string.Join("\n", bad.ToArray()), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            });
        }

        void AppendClip(string file, MediaInfo mi)
        {
            VideoClip c = new VideoClip();
            c.Path = file;
            c.Duration = mi.Duration;
            c.Fps = mi.Fps;
            c.Width = mi.Width;
            c.Height = mi.Height;
            c.HasAudio = mi.Audio.Count > 0;
            c.Rgb = VideoExport.IsRgbFormat(file, mi.PixelFormat);
            c.TakeColour(mi);
            c.Info = mi;
            clips.Add(c);
            double total = ClipSeq.Layout(clips);
            timeline.Extend(total);
            timeline.HasSound = ClipSeq.AnyAudio(clips);
            LoadThumbs(c);
            dirty = true;
            UpdateUi();
            ShotStack.Log("Presentar: a\u00F1adido " + Path.GetFileName(file));
        }

        void ReleaseClip(VideoClip c)
        {
            if (c.Play != null) { c.Play.Dispose(); c.Play = null; }
            Bitmap[] th = c.Thumbs;
            c.Thumbs = null;
            if (th != null) foreach (Bitmap b in th) if (b != null) b.Dispose();
        }

        // The first video is never removed: it defines the canvas the marks are drawn on.
        void RemoveClip(int i)
        {
            if (Exporting || i < 1 || i >= clips.Count) return;
            StopPlayer();
            VideoClip c = clips[i];
            double start = c.Offset, len = c.Duration;
            clips.RemoveAt(i);
            double total = ClipSeq.Layout(clips);
            timeline.Relayout(ClipSeq.RemovedMap(start, len), total);
            if (play == c.Play) play = null;
            ReleaseClip(c);
            if (playClip >= clips.Count) playClip = 0;
            timeline.HasSound = ClipSeq.AnyAudio(clips);
            dirty = true;
            pausedAt = -1;
            ShowFrameAt(timeline.Playhead);
            UpdateUi();
        }

        void MoveClip(int i, int dir)
        {
            if (Exporting || i < 1 || i + dir < 1 || i + dir >= clips.Count) return;
            StopPlayer();
            List<VideoClip> before = new List<VideoClip>(clips);
            double[] oldOff = ClipSeq.Offsets(before);
            ClipSeq.Move(clips, i, dir);
            double total = ClipSeq.Layout(clips);
            timeline.Relayout(ClipSeq.MovedMap(before, oldOff), total);
            if (playClip >= clips.Count) playClip = 0;
            dirty = true;
            pausedAt = -1;
            ShowFrameAt(timeline.Playhead);
            UpdateUi();
        }

        // FFmpeg puts the (cropped, padded) video below and an image with backdrop, corners and annotations on top.
        // Runs in the background; the hint strip shows progress.
        void ExportVideo()
        {
            if (Exporting) return;
            StopPlayer();
            canvas.CommitText();
            Rectangle crop = canvas.CropRect;
            crop.Intersect(new Rectangle(0, 0, canvas.Img.Width, canvas.Img.Height));
            // H.264 yuv420p needs even dimensions.
            crop = new Rectangle(crop.X & ~1, crop.Y & ~1, Math.Max(16, crop.Width & ~1), Math.Max(16, crop.Height & ~1));
            // A crop narrower than 16 px at the right or bottom edge must not reach past the frame.
            crop.X = Math.Max(0, Math.Min(crop.X, (canvas.Img.Width - crop.Width) & ~1));
            crop.Y = Math.Max(0, Math.Min(crop.Y, (canvas.Img.Height - crop.Height) & ~1));
            bool bg = canvas.BgOn, marks = canvas.HasMarks;
            bool cropped = crop != new Rectangle(0, 0, canvas.Img.Width & ~1, canvas.Img.Height & ~1);
            bool trimmed = timeline != null && timeline.IsTrimmed;
            bool cuts = timeline != null && timeline.HasCuts;
            // Kept ranges: one when only trimmed, several when middle pieces were removed (joined on export).
            bool joined = clips.Count > 1;
            List<double[]> keep = trimmed || cuts || joined ? timeline.Keep() : null;
            if (keep != null && keep.Count == 0)
            {
                Dialogs.Show(this, I18n.T("No queda ning\u00FAn tramo dentro del recorte.", "No segment remains within crop."), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            double speed = timeline != null ? Timeline.SpeedValues[timeline.Speed] : 1;
            int maxH = timeline != null ? Timeline.SizeValues[timeline.OutSize] : 0;
            toGif = ExportsGif;
            // A source that isn't ready to share as it is (a Cine master, a .mkv or .mov) always gets a new file, and so
            // does one to fit in 16 or 25 MB.
            bool fit = !toGif && VideoExport.TargetBytes(timeline != null ? timeline.Quality : bgs.ExportQuality) > 0;
            bool options = speed != 1 || maxH > 0 || joined || fit || VideoExport.NeedsTranscode(path, toGif);
            if (!bg && !marks && !cropped && !trimmed && !cuts && !options) { Flash(I18n.T("No hay nada que exportar: el v\u00EDdeo ya est\u00E1 listo para compartir tal cual.", "Nothing to export: video is already ready to share as is."), true); return; }
            double span = (keep != null ? timeline.KeptSeconds : duration) / speed;

            string topPng = null;
            Size frame = crop.Size;
            Rectangle inner = new Rectangle(Point.Empty, crop.Size);
            try
            {
                Bitmap top = null;
                using (Bitmap m = marks ? canvas.RenderMarks(crop) : null)
                {
                    if (bg)
                    {
                        Backdrop.Measure(crop.Size, bgs, true, out frame, out inner);
                        top = Backdrop.VideoTop(frame, inner, bgs, Backdrop.RadiusFor(crop.Size, bgs), m);
                    }
                    else if (m != null) top = new Bitmap(m);
                }
                if (top != null)
                {
                    topPng = Path.Combine(Path.GetTempPath(), "stackshot-top-" + Guid.NewGuid().ToString("N") + ".png");
                    using (top) top.Save(topPng, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                ShotStack.Log("Presentar: " + ex.Message);
                Dialogs.Show(this, I18n.T("No se pudo preparar el v\u00EDdeo: ", "Could not prepare video: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Next to a saved video (kept like it), else with the temporary captures.
            string dir = owner != null ? owner.ExportFolder(path) : Path.GetDirectoryName(path);
            string ext = toGif ? ".gif" : ".mp4";
            string suffix = IsMaster && !bg && !marks && !cropped && !trimmed && !cuts && !joined ? " (para compartir)" : " (editado)";
            exportOut = ShotStack.Reserve(Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + suffix + ext));

            ExportSpec spec = new ExportSpec();
            spec.Source = path;
            spec.Overlay = topPng;
            spec.Output = exportOut;
            spec.Crop = crop;
            spec.Backdrop = bg;
            spec.Frame = frame;
            spec.Inner = inner;
            spec.Keep = keep;
            spec.Speed = speed;
            spec.MaxHeight = maxH;
            spec.ToGif = toGif;
            spec.SourceIsGif = IsGif && !joined;
            spec.HasAudio = joined ? ClipSeq.AnyAudio(clips) : hasAudio;
            spec.RgbSource = rgbSource;
            // The quality chosen under "Calidad" (maximum by default) applies to every export, masters included.
            spec.Level = timeline != null ? timeline.Quality : bgs.ExportQuality;
            spec.Fps = sourceFps;
            // The sound track chosen in the timeline's sound menu (the one being heard).
            // Without a timeline (a length that could not be read) there is no clip list: the track as chosen.
            spec.AudioTrack = joined ? audioTrack : clips.Count > 0 ? VideoExport.ClipTrack(clips[0], audioTrack) : Math.Max(0, audioTrack);
            if (joined)
            {
                // Several videos: each piece is brought to the canvas of the first one (its size and frame rate).
                spec.Clips = new List<VideoClip>(clips);
                spec.Canvas = new Size(canvas.Img.Width, canvas.Img.Height);
            }
            spec.Seconds = span;
            exportTop = topPng;
            if (!toGif && VideoExport.TargetBytes(spec.Level) > 0 && span <= 0)
            {
                CleanExportFiles();
                ShotStack.Unreserve(exportOut);
                Tell(I18n.Fmt("No se sabe cu\u00E1nto dura este v\u00EDdeo, as\u00ED que no se puede ajustar a {0}. Elige M\u00E1xima, Alta o Ligera en Calidad.",
                              "The duration of this video is unknown, so it cannot be fit to {0}. Choose Maximum, High or Light in Quality.", Timeline.Qualities[spec.Level]));
                return;
            }
            if (!toGif && VideoExport.TargetBytes(spec.Level) > 0 && VideoExport.TargetKbps(spec) < VideoExport.TargetMinKbps)
            {
                CleanExportFiles();
                ShotStack.Unreserve(exportOut);
                Tell(I18n.Fmt("Es demasiado largo para caber en {0} con buena calidad (caben {1}). Recorta el v\u00EDdeo, s\u00FAbelo de velocidad o elige 720p o menos en Tama\u00F1o.",
                              "Too long to fit into {0} with good quality (holds {1}). Crop the video, increase speed or choose 720p or lower in Size.", Timeline.Qualities[spec.Level], VideoExport.MaxLength(spec.Level, spec.HasAudio)));
                return;
            }
            exportLog = Path.Combine(Path.GetTempPath(), "stackshot-2pass-" + Guid.NewGuid().ToString("N"));
            exportPasses = VideoExport.Passes(spec, exportLog);
            exportSpec = spec;
            exportRetries = 0;
            exportSpan = span;
            exportExpect = ExpectFor(spec, duration, sourceFps, fpsKnown);
            exportExpect.MaxBytes = toGif ? 0 : VideoExport.TargetBytes(spec.Level);
            if (!StartPass(0))
            {
                CleanExportFiles();
                ShotStack.Unreserve(exportOut);
                return;
            }
            canvas.Enabled = false;
            bar.Locked = true;
            bgPanel.Enabled = false;
            if (timeline != null) timeline.Enabled = false;
            ShowProgress(0);
            ShotStack.Log("Presentar: exportando " + Path.GetFileName(exportOut));
        }

        void ShowProgress(double fr)
        {
            if (export == null) return;
            hint.Progress = duration > 0 ? fr : 0.5;
            hint.LeftText = (toGif ? I18n.T("Creando el GIF\u2026 ", "Creating the GIF\u2026 ") : I18n.T("Creando el v\u00EDdeo\u2026 ", "Creating the video\u2026 ")) +
                            (duration > 0 ? (int)Math.Round(fr * 100) + (I18n.IsSpanish ? " %" : "%") : "");
            hint.RightText = I18n.T("Esc cancela", "Esc cancels");
            hint.Invalidate();
        }

        // One FFmpeg run of the export (a size limit takes two, one after the other); the bar shows each as its share.
        // A message about the export; tests set Quiet so nothing blocks (the text stays in ExportNote).
        internal static bool Quiet { get; set; }
        internal string ExportNote;

        void Tell(string text)
        {
            ExportNote = text;
            if (!Quiet) Dialogs.Show(this, text, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        bool StartPass(int index)
        {
            string args = exportPasses[index];
            int count = exportPasses.Count;
            double span = exportSpan;
            StringBuilder errors = new StringBuilder();
            Process p = new Process();
            p.StartInfo.FileName = ffmpeg;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                // -progress prints "out_time=00:00:01.234567" several times per second.
                if (e.Data == null || !e.Data.StartsWith("out_time=") || span <= 0) return;
                TimeSpan t;
                if (!TimeSpan.TryParse(e.Data.Substring(9).Trim(), CultureInfo.InvariantCulture, out t)) return;
                double fr = (index + Math.Max(0, Math.Min(1, t.TotalSeconds / span))) / count;
                try { BeginInvoke((Action)delegate { ShowProgress(fr); }); } catch { }
            };
            p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (errors) { if (errors.Length < 4000) errors.AppendLine(e.Data); }
            };
            p.Exited += delegate
            {
                int code = -1;
                try { p.WaitForExit(); code = p.ExitCode; } catch { }
                string err;
                lock (errors) err = errors.ToString();
                try { BeginInvoke((Action)delegate { PassDone(p, index, code, err); }); }
                catch { try { p.Dispose(); } catch { } CleanExportFiles(); }
            };
            try
            {
                p.Start();
                ChildJob.Add(p);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                p.Dispose();
                Dialogs.Show(this, I18n.T("No se pudo iniciar FFmpeg: ", "Could not start FFmpeg: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            export = p;
            return true;
        }

        void PassDone(Process p, int index, int code, string err)
        {
            if (export != p) { p.Dispose(); return; } // cancelled
            // Over its size limit after the second pass: that pass again with the bitrate scaled down (twice at most).
            bool refit = code == 0 && exportPasses != null && exportPasses.Count == 2 && index == 1 && exportRetries < 2 && VideoExport.Refit(exportSpec, exportOut);
            if (refit)
            {
                exportRetries++;
                ShotStack.Log("Presentar: m\u00E1s grande que " + Timeline.Qualities[exportSpec.Level] + ", segunda pasada otra vez al " + (exportSpec.Fit * 100).ToString("0", CultureInfo.InvariantCulture) + " %");
                exportPasses[1] = VideoExport.Arguments(exportSpec, 2, exportLog);
            }
            if (code == 0 && exportPasses != null && (index + 1 < exportPasses.Count || refit))
            {
                export = null;
                p.Dispose();
                if (StartPass(refit ? 1 : index + 1)) return;
                CleanExportFiles();
                ShotStack.Unreserve(exportOut);
                try { if (File.Exists(exportOut)) File.Delete(exportOut); } catch { }
                Unlock();
                UpdateUi();
                return;
            }
            ExportDone(p, code, err);
        }

        // The overlay image and x264's two-pass log are no longer needed, whatever happened.
        void CleanExportFiles()
        {
            if (exportTop != null) { try { File.Delete(exportTop); } catch { } exportTop = null; }
            VideoExport.DeletePassLog(exportLog);
            exportLog = null;
        }

        void ExportDone(Process p, int code, string err)
        {
            if (export != p) { p.Dispose(); return; } // cancelled
            export = null;
            p.Dispose();
            CleanExportFiles();
            ShotStack.Unreserve(exportOut); // FFmpeg is done: the file is on disk (or failed)
            if (code != 0 || !File.Exists(exportOut))
            {
                ShotStack.Log("Presentar: FFmpeg " + code + ": " + Tail(err));
                try { if (File.Exists(exportOut)) File.Delete(exportOut); } catch { }
                Unlock();
                UpdateUi();
                Dialogs.Show(this, I18n.T("No se pudo crear el v\u00EDdeo.\n\n", "Could not create video.\n\n") + Tail(err), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            BeginCheck();
        }

        void Unlock()
        {
            hint.Progress = -1;
            canvas.Enabled = true;
            bar.Locked = false;
            bgPanel.Enabled = true;
            if (timeline != null) timeline.Enabled = true;
        }

        // FFmpeg finished: the file is opened and checked on a background thread before it is offered (Esc cancels).
        void BeginCheck()
        {
            ExportAbort ab = new ExportAbort();
            string ff = ffmpeg, file = exportOut;
            ShareExpect expect = exportExpect;
            checkAbort = ab;
            hint.Progress = -1;
            hint.LeftText = I18n.T("Comprobando el v\u00EDdeo\u2026", "Checking video\u2026");
            hint.RightText = I18n.T("Esc cancela", "Esc cancels");
            hint.Invalidate();
            ShotStack.Log("Presentar: comprobando " + Path.GetFileName(file));
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                VideoExport.Abort = ab;
                ShareReport rep = ShareCheck.Validate(ff, file, expect);
                try { BeginInvoke((Action)delegate { CheckDone(ab, rep); }); } catch { }
            });
            t.IsBackground = true;
            checkThread = t;
            t.Start();
        }

        void CheckDone(ExportAbort ab, ShareReport rep)
        {
            if (checkAbort != ab) return; // cancelled
            checkAbort = null;
            checkThread = null;
            Unlock();
            ShotStack.Log("Presentar: comprobaci\u00F3n " + (rep.Passed ? "correcta" : "FALLIDA") + " de " + Path.GetFileName(exportOut) + "\n" + rep);
            if (rep.Passed)
            {
                ShotStack.Log("Presentar: listo " + Path.GetFileName(exportOut));
                // The editor stays open: the same edit can go out again as a GIF or in another size.
                dirty = false;
                if (owner != null)
                {
                    owner.AddRecording(exportOut);
                    Flash(I18n.Fmt("Exportado: {0} ya est\u00E1 en la pila.", "Exported: {0} is now in stack.", Path.GetFileName(exportOut)), true);
                }
                else Flash(I18n.Fmt("Exportado: {0}", "Exported: {0}", exportOut), true);
                UpdateUi();
                return;
            }
            // Not a normal result: kept under another name so it is never taken for a good export; the editor stays open.
            string kept = exportOut;
            try
            {
                kept = ShotStack.Unique(Path.Combine(Path.GetDirectoryName(exportOut), Path.GetFileNameWithoutExtension(exportOut) + " (sin validar)" + Path.GetExtension(exportOut)));
                File.Move(exportOut, kept);
                exportOut = kept;
            }
            catch (Exception ex)
            {
                kept = exportOut;
                ShotStack.Log("Presentar: no se pudo renombrar: " + ex.Message);
            }
            UpdateUi();
            Dialogs.Show(this, rep.Summary + I18n.Fmt("\n\nEl archivo no se ha a\u00F1adido a la pila. Queda en:\n{0}\n\nEl editor sigue abierto: puedes volver a exportar.",
                                                     "\n\nThe file was not added to stack. Stored in:\n{0}\n\nThe editor remains open: you can export again.", kept),
                         "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // The file being written or checked, and the work around it, are dropped.
        void CancelExport() { StopExport(true); }

        // wait: closing or quitting waits for FFmpeg to let go of the file before it is deleted. Esc in an open window
        // doesn't: the window is free at once and the leftovers go in the background.
        void StopExport(bool wait)
        {
            Process p = export;
            ExportAbort ab = checkAbort;
            if (p == null && ab == null) return;
            System.Threading.Thread check = checkThread;
            if (p != null)
            {
                export = null;
                try { if (!p.HasExited) p.Kill(); } catch { }
            }
            if (ab != null)
            {
                checkAbort = null;
                ab.Set = true;
                checkThread = null;
            }
            string output = exportOut, top = exportTop, log = exportLog;
            exportTop = null;
            exportLog = null;
            Action finish = delegate
            {
                if (p != null) { try { p.WaitForExit(3000); } catch { } }
                if (check != null) { try { check.Join(3000); } catch { } }
                try { if (File.Exists(output)) File.Delete(output); } catch (Exception ex) { ShotStack.Log("Exportar, cancelar: " + ex.Message); }
                ShotStack.Unreserve(output);
                if (top != null) { try { File.Delete(top); } catch { } }
                VideoExport.DeletePassLog(log);
            };
            if (wait) finish();
            else System.Threading.ThreadPool.QueueUserWorkItem(delegate { finish(); });
            Unlock();
            UpdateUi();
        }

        bool Exporting { get { return export != null || checkAbort != null; } }

        // What an export of this spec must contain, worked out from the spec: the output size, the frame rate of the
        // source, and the length of what is kept. With cuts the length is counted in whole source frames (a piece from a to b
        // holds ceil(b*fps) - ceil(a*fps) of them, as -ss/-to select them), which assumes a constant frame rate starting
        // at 0; what is left over is covered by ShareCheck's own tolerance (duration: max(0.15 s, 2 frames), frame count: 1)
        // plus 2 frames of slack per piece when several are joined. A GIF is only checked for its size (width capped at
        // 960, height rounded to even). Left unchecked (0): a frame rate that was not read from the file, a GIF source,
        // and an unknown length.
        internal static ShareExpect ExpectFor(ExportSpec s, double duration, double fps, bool fpsKnown)
        {
            Size o = VideoExport.OutputSize(s);
            ShareExpect e = new ShareExpect();
            e.Gif = s.ToGif;
            e.Width = o.Width;
            e.Height = o.Height;
            if (s.ToGif)
            {
                e.Width = Math.Min(o.Width, 960) & ~1;
                // FFmpeg's scale=w:-2 rounds half away from zero (av_rescale), not to even.
                e.Height = Math.Max(2, (int)Math.Round(o.Height * (double)e.Width / o.Width / 2, MidpointRounding.AwayFromZero) * 2);
                return e;
            }
            e.HasAudio = s.HasAudio;
            e.RgbSource = s.RgbSource;
            e.CentredChroma = VideoExport.ChromaMadeHere(s);
            e.Level = VideoExport.H264Level(o, VideoExport.OutFps(s.Fps, s.Speed > 0 ? s.Speed : 1));
            double speed = s.Speed > 0 ? s.Speed : 1;
            bool known = fpsKnown && fps > 0 && !s.SourceIsGif;
            double f = known ? ExactFps(fps) : 0;
            if (s.Clips != null && s.Clips.Count > 1)
            {
                // Joined videos: the canvas rate, the kept spans added up, always tagged BT.709, and up to two frames of
                // slack at every join (each piece is brought to the canvas rate on its own).
                e.RgbSource = true;
                if (known) e.Fps = VideoExport.OutFps(f, speed);
                List<ClipSegment> segs = ClipSeq.Segments(s.Clips, s.Keep);
                double sum = 0;
                foreach (ClipSegment g in segs) sum += g.B - g.A;
                e.Seconds = sum / speed;
                e.Slack = segs.Count * 2.0 / (f > 0 ? f : 30);
                return e;
            }
            // A speed change or joined pieces end in an fps filter (VideoExport.OutFps), so every export has a constant
            // rate to check.
            bool joined = s.Keep != null && s.Keep.Count > 1;
            if (known) e.Fps = VideoExport.OutFps(f, speed);
            double kept = 0;
            if (s.Keep == null) kept = duration;
            else if (known)
            {
                double frames = 0;
                foreach (double[] k in s.Keep) frames += Math.Ceiling(k[1] * f - 1e-6) - Math.Ceiling(k[0] * f - 1e-6);
                kept = frames / f;
            }
            else foreach (double[] k in s.Keep) kept += k[1] - k[0];
            e.Seconds = kept / speed;
            // Joining pieces leaves up to a couple of frames of gap or overlap at each join.
            if (joined) e.Slack = s.Keep.Count * 2.0 / (f > 0 ? f : 30);
            return e;
        }

        // The average frame rate FFmpeg prints for the first video stream ("30.34 fps"), and whether it is constant: the same
        // as its base rate ("60 tbr" says frames come as often as 1/60 s, so a phone video that changes its rate). Only a
        // constant rate is checked after the export; a variable one keeps every frame at its time and is checked by length.
        internal static bool ConstantRate(string probe, out double fps)
        {
            fps = 0;
            Match m = Regex.Match(probe ?? "", @"Stream #0:\d+[^\n]*: Video:[^\n]*?([0-9.]+) fps(?:, ([0-9.]+) tbr)?");
            if (!m.Success || !double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out fps) || fps <= 0) { fps = 0; return false; }
            double tbr;
            if (!m.Groups[2].Success || !double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out tbr) || tbr <= 0) return true;
            return Math.Abs(fps - tbr) < 0.01;
        }

        // FFmpeg prints 23.98, 29.97 and 59.94 for the NTSC rates; the exact ones keep long videos from drifting a frame.
        internal static double ExactFps(double fps)
        {
            foreach (int n in new int[] { 24, 30, 48, 60, 120 })
            {
                double ntsc = n * 1000.0 / 1001;
                if (Math.Abs(fps - ntsc) < 0.006) return ntsc;
            }
            return fps;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (Exporting)
            {
                if (e.CloseReason == CloseReason.UserClosing &&
                    Dialogs.Ask(this, I18n.T("\u00BFCancelar la exportaci\u00F3n?\n\nEl v\u00EDdeo a medio crear se descarta.", "Cancel export?\n\nThe partially created video will be discarded."), MessageBoxIcon.Question,
                                new string[] { I18n.T("Cancelar la exportaci\u00F3n", "Cancel export"), I18n.T("Seguir exportando", "Keep exporting") }, 1, 1) != 0)
                {
                    e.Cancel = true;
                    return;
                }
                CancelExport();
                return;
            }
            if (closeWithoutAsking || e.CloseReason != CloseReason.UserClosing) return;
            canvas.CommitText();
            if (!Unsaved) return;
            if (playing) PausePlay();
            if (IsVideo)
            {
                int v = Dialogs.Ask(this, I18n.T("\u00BFExportar la grabaci\u00F3n con los cambios?\n\nSi la descartas, los cambios se pierden; el v\u00EDdeo original no cambia.",
                                                "Export recording with changes?\n\nIf you discard them, changes are lost; the original video does not change."), MessageBoxIcon.Question,
                                    new string[] { I18n.T("Exportar", "Export"), I18n.T("Descartar cambios", "Discard changes"), I18n.T("Seguir editando", "Keep editing") }, 0, 2);
                if (v == 2) e.Cancel = true;
                else if (v == 0) { e.Cancel = true; ExportVideo(); }
                return;
            }
            int r = Dialogs.Ask(this, I18n.T("\u00BFConservar los cambios en la captura?", "Keep changes to capture?"), MessageBoxIcon.Question,
                                new string[] { I18n.T("Conservar", "Keep"), I18n.T("Descartar cambios", "Discard changes"), I18n.T("Seguir editando", "Keep editing") }, 0, 2);
            if (r == 2 || (r == 0 && !Apply())) e.Cancel = true;
        }

        static List<Editor> OpenEditors()
        {
            open.RemoveAll(delegate(Editor x) { return x.IsDisposed; });
            return new List<Editor>(open);
        }

        // Something quitting now would lose: unapplied marks, video edits not exported yet, an export or a save running.
        bool WouldLose
        {
            get
            {
                if (closeWithoutAsking) return false;
                if (Exporting) return true;
                lock (savingLock) if (saving > 0) return true;
                // Text still being typed counts, without committing it: this is also asked while the user works.
                return canvas.Typing || Unsaved;
            }
        }

        // Cross-package API (UI thread): an open image or video editor has changes that quitting would lose.
        public static bool AnyUnsaved
        {
            get
            {
                foreach (Editor ed in OpenEditors()) if (ed.WouldLose) return true;
                return false;
            }
        }

        // Cross-package API (UI thread), before quitting: every editor with changes asks Guardar / No guardar / Cancelar.
        // False when the user cancels (or chose to export a video first); true when every editor can close.
        public static bool ConfirmCloseAll()
        {
            foreach (Editor ed in OpenEditors())
            {
                if (ed.IsDisposed || !ed.WouldLose) continue;
                ed.BringUp();
                if (!ed.ConfirmQuit()) return false;
            }
            foreach (Editor ed in OpenEditors()) ed.WaitForSaves();
            return true;
        }

        // Leaves no "answered" flag: a later editor or document can still cancel the quit, and the editor must keep
        // asking then. An approved quit ends the process without closing editors as UserClosing.
        bool ConfirmQuit()
        {
            string name = Path.GetFileName(path);
            canvas.CommitText();
            if (Exporting)
            {
                if (Dialogs.Ask(this, I18n.Fmt("Se est\u00E1 exportando {0}.\n\nSi sales ahora, el v\u00EDdeo a medio crear se descarta.",
                                               "{0} is currently exporting.\n\nIf you exit now, the half-created video will be discarded.", name), MessageBoxIcon.Question,
                                new string[] { I18n.T("Cancelar y salir", "Cancel and exit"), I18n.T("Seguir exportando", "Keep exporting") }, 1, 1) != 0) return false;
                CancelExport();
                return true;
            }
            if (!Unsaved) return true; // a save still finishing only needs a moment: waited for afterwards
            if (IsVideo)
            {
                int v = Dialogs.Ask(this, I18n.Fmt("Hay cambios sin exportar en {0}.\n\nCon Exportar, Stackshot crea el v\u00EDdeo y no se cierra: sal de nuevo cuando termine.",
                                                   "There are unexported changes in {0}.\n\nWith Export, Stackshot creates the video and stays open: exit again when finished.", name),
                                    MessageBoxIcon.Question, new string[] { I18n.T("Exportar", "Export"), I18n.T("Salir sin exportar", "Exit without exporting"), I18n.T("Seguir editando", "Keep editing") }, 0, 2);
                if (v == 2) return false;
                if (v == 0) { ExportVideo(); return false; }
                return true;
            }
            int r = Dialogs.Ask(this, I18n.Fmt("\u00BFGuardar los cambios en {0} antes de salir?", "Save changes in {0} before exiting?", name), MessageBoxIcon.Question,
                                new string[] { I18n.T("Guardar", "Save"), I18n.T("No guardar", "Don't save"), I18n.T("Seguir editando", "Keep editing") }, 0, 2);
            if (r == 2) return false;
            if (r == 0)
            {
                if (!Apply()) return false;
                // A capture still in the temporary folder would be swept: keep it in the save folder.
                if (InTemp)
                {
                    try { owner.Keep(path); }
                    catch (Exception ex)
                    {
                        ShotStack.Log("Guardar (editor): " + ex.Message);
                        Dialogs.Show(this, I18n.T("No se pudo guardar: ", "Could not save: ") + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
            }
            return true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            open.Remove(this);
            Ds.Changed -= onDs;
            EdTip.Cancel();
            if (owner == null) WaitForSaves();
            feedback.Dispose();
            if (flashTimer != null) flashTimer.Dispose();
            if (playTimer != null) playTimer.Dispose();
            foreach (VideoClip c in clips) if (c.Play != null) c.Play.Dispose();
            canvas.Release();
            foreach (VideoClip c in clips)
            {
                Bitmap[] th = c.Thumbs;
                c.Thumbs = null;
                if (th != null) foreach (Bitmap b in th) if (b != null) b.Dispose();
            }
        }
    }

}
