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
    public class Editor : Form
    {
        static readonly Tool[] ToolOrder = { Tool.Arrow, Tool.Rect, Tool.Ellipse, Tool.Text, Tool.Counter, Tool.Highlight, Tool.Pixelate, Tool.Redact, Tool.Crop };
        static readonly string[] ToolNames = { "Flecha", "Recuadro", "Elipse", "Texto", "N\u00FAmeros", "Resaltar", "Pixelar", "Tapar (lo m\u00E1s seguro para datos)", "Recortar" };
        static readonly string[] ToolKeys = { "F", "R", "E", "T", "N", "H", "P", "X", "C" };
        static readonly string[] ToolGlyphs = { "arrow", "rect", "ellipse", "text", "counter", "highlight", "pixelate!", "redact!", "crop" };
        static readonly string[] WeightNames = { "Fino", "Medio", "Grueso" };
        static readonly string[] SizeNames = { "Peque\u00F1o", "Mediano", "Grande" };
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
        bool hasAudio;
        double sourceFps = 60;
        Process export;
        string exportOut;
        bool toGif;

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
            canvas.Ui = s;
            canvas.Dock = DockStyle.Fill;
            canvas.Bg = bgs;
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
                it.Tip = "Grosor: " + WeightNames[i] + "  (\u2212 / +)";
                it.Do = delegate { SetWeight(idx); };
                weightItems.Add(it);
            }
            bar.Add(Bar.Kind.Gap);
            undoItem = Button(null, "undo", "Deshacer  (Ctrl+Z)", delegate { canvas.Undo(); }, false);
            redoItem = Button(null, "redo", "Rehacer  (Ctrl+Y)", delegate { canvas.Redo(); }, false);
            bar.Add(Bar.Kind.Gap);
            bgItem = Button("Fondo", "backdrop", "Fondo de presentaci\u00F3n: degradado, margen, esquinas y sombra  (B)", ToggleBgPanel, false);

            if (IsVideo)
            {
                Bar.Item done = Button("Exportar", "share",
                                       "Crear la versi\u00F3n con el recorte, las marcas, el fondo y las opciones de abajo  (Enter)", Done, true);
                done.Accent = true;
            }
            else
            {
                Button(null, "pin", "Fijar en pantalla: queda flotando encima de todo", PinOut, true);
                pdfItem = Button("PDF", "doc", "Guardar como PDF: las capturas largas se reparten en p\u00E1ginas sin perder nitidez  (Ctrl+P)", delegate { SaveAs(true); }, true);
                pdfItem.Alt = "PDF \u2713";
                pdfItem.Optional = true; // Ctrl+P
                Bar.Item drag = Button("Arrastrar", "share", "Arr\u00E1strala al chat o a otra aplicaci\u00F3n", null, true);
                drag.DragOut = true;
                drag.Optional = true;    // the thumbnail in the stack drags too
                copyItem = Button("Copiar", "copy", "Copiar al portapapeles  (Ctrl+C)", CopyOut, true);
                copyItem.Alt = "Copiado \u2713";
                saveItem = Button("Guardar", "save", owner != null ? "Conservarla en tu carpeta de capturas  (Ctrl+S)" : "Guardar sobre la imagen  (Ctrl+S)", KeepCopy, true);
                saveItem.Alt = "Guardada \u2713";
                Bar.Item done = Button("Listo", "check", "Aplicar, copiar y cerrar  (Enter)", Done, true);
                done.Accent = true;
                bar.DragOut += StartDragOut;
            }
            // The toolbar is also the title bar: window buttons at the top right.
            string[] captionTips = { "Minimizar", "Maximizar", "Cerrar" };
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
            bgPanel.Dock = DockStyle.Top;
            bgPanel.Visible = false;
            bgPanel.Changed += delegate
            {
                canvas.BgOn = bgPanel.On;
                canvas.Live = bgPanel.Sliding;
                dirty = true;
                bgTouched = true;
                canvas.Invalidate();
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
            hint.Height = P(28);

            if (IsVideo && duration > 0)
            {
                timeline = new Timeline();
                timeline.S = s;
                timeline.Dock = DockStyle.Bottom;
                timeline.Height = P(110);
                timeline.Init(duration);
                timeline.Format = IsGif ? 1 : 0;
                timeline.Trimmed += delegate { dirty = true; UpdateUi(); };
                timeline.OptionsChanged += delegate { dirty = true; UpdateUi(); };
                timeline.Seek += ShowFrameAt;
            }

            // Docking order matters: toolbar at the very top, backdrop strip right below it; at the bottom, the hint
            // strip and the timeline above it.
            Controls.Add(canvas);
            if (timeline != null) Controls.Add(timeline);
            Controls.Add(hint);
            Controls.Add(bgPanel);
            Controls.Add(bar);

            // Size: fit the capture in 88% of the screen, never narrower than the toolbar (its compact form is the minimum).
            int minW = bar.LayoutItems(), fullW = bar.FullWidth;
            Rectangle wa = scr.WorkingArea;
            float pad = 28 * s;
            int chrome = P(52) + P(28) + P(Canvas.PillRoom - 28) + (timeline != null ? timeline.Height : 0); // bars, and the room kept for the zoom control
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
            hint.Height = P(28);
            if (timeline != null) timeline.Height = P(110);
            Bounds = suggested;
            if (bgPanel.Visible) bgPanel.Height = bgPanel.HeightFor(ClientSize.Width);
            ResumeLayout(true);
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
            if (bar == null || !IsHandleCreated) return;
            bool z = IsZoomed(Handle);
            if (z != bar.Maximized)
            {
                bar.Maximized = z;
                if (maxItem != null) maxItem.Tip = z ? "Restaurar" : "Maximizar";
                bar.Invalidate();
            }
        }

        bool IsVideo { get { return ffmpeg != null; } }
        bool IsGif { get { return IsVideo && Path.GetExtension(path).ToLowerInvariant() == ".gif"; } }

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

        // Opening the backdrop strip with no backdrop applies the last one used.
        void ToggleBgPanel()
        {
            bool show = !bgPanel.Visible;
            if (show)
            {
                bgPanel.Height = bgPanel.HeightFor(ClientSize.Width);
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (bgPanel != null && bgPanel.Visible)
            {
                int h = bgPanel.HeightFor(ClientSize.Width);
                if (h != bgPanel.Height) bgPanel.Height = h;
            }
        }

        // Which options a kind of mark actually uses (the drawing code ignores the rest).
        static bool UsesColor(Tool t) { return t != Tool.Pixelate && t != Tool.Redact && t != Tool.Crop; }
        static bool UsesWeight(Tool t) { return t == Tool.Arrow || t == Tool.Rect || t == Tool.Ellipse || t == Tool.Text || t == Tool.Counter; }

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
                weightItems[i].Tip = (sized ? "Tama\u00F1o: " + SizeNames[i] : "Grosor: " + WeightNames[i]) + "  (\u2212 / +)";
            }
            undoItem.Enabled = canvas.CanUndo;
            redoItem.Enabled = canvas.CanRedo;
            bgItem.On = canvas.BgOn || bgPanel.Visible;
            bar.Sync();
            if (bgPanel.Visible) bgPanel.Invalidate();
            if (export != null) return; // the hint strip shows export progress
            Size o = canvas.OutputSize;
            string left = flashText ?? canvas.HintText, right = o.Width + " \u00D7 " + o.Height + " px  \u00B7  " + (IsVideo ? "Enter exporta" : "Enter copia y cierra");
            if (left != hint.LeftText || right != hint.RightText)
            {
                hint.LeftText = left;
                hint.RightText = right;
                hint.Invalidate();
            }
            string title = (IsVideo ? "Editar v\u00EDdeo \u00B7 " : "Editar \u00B7 ") + Path.GetFileName(path) + (Unsaved ? "  \u2022" : "");
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
            if (timeline != null) LoadThumbs(); // needs the window handle to hand the frames back
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
            if (export != null)
            {
                if (keyData == Keys.Escape) { CancelExport(); return true; }
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
                case Keys.Left: if (canvas.HasSelection) { canvas.Nudge(-step, 0); return true; } break;
                case Keys.Right: if (canvas.HasSelection) { canvas.Nudge(step, 0); return true; } break;
                case Keys.Up: if (canvas.HasSelection) { canvas.Nudge(0, -step); return true; } break;
                case Keys.Down: if (canvas.HasSelection) { canvas.Nudge(0, step); return true; } break;
            }
            switch (keyData)
            {
                case Keys.Control | Keys.Z: canvas.Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: canvas.Redo(); return true;
                case Keys.Control | Keys.C: if (!IsVideo) CopyOut(); return true;
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
                    if (canvas.HasSelection) canvas.SelectShape(null);
                    else if (bgPanel.Visible) ToggleBgPanel();
                    else Close();
                    return true;
                case Keys.Delete:
                case Keys.Back: canvas.DeleteSelected(); return true;
                case Keys.F: SetTool(Tool.Arrow); return true;
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
            canvas.CommitText();
            try
            {
                using (Bitmap b = canvas.Render()) Write(b, path);
                Applied();
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Encoded in memory first, so the file is never left half written.
        static void Write(Bitmap b, string file)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteImage(b, ms, FormatFor(file));
                File.WriteAllBytes(file, ms.ToArray());
            }
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
                if (canvas.HasMarks || canvas.IsCropped || (canvas.BgOn && (!bgAtOpen || bgTouched))) return true;
                if (IsVideo) return timeline != null && (timeline.IsTrimmed || timeline.HasCuts || timeline.Speed != 0 || timeline.OutSize != 0 || (timeline.Format == 1) != IsGif);
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
                d.Title = pdf ? "Guardar como PDF" : "Guardar como";
                d.Filter = "PNG|*.png|JPG|*.jpg;*.jpeg|PDF|*.pdf";
                d.FilterIndex = kind;
                d.AddExtension = true;
                d.DefaultExt = kind == 3 ? "pdf" : kind == 2 ? "jpg" : "png";
                d.FileName = Path.GetFileNameWithoutExtension(path) + "." + d.DefaultExt;
                string dir = owner != null ? owner.Settings.SaveFolder : Path.GetDirectoryName(path);
                try { Directory.CreateDirectory(dir); d.InitialDirectory = dir; } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                file = d.FileName;
            }
            string fe = Path.GetExtension(file).ToLowerInvariant();
            bool asPdf = fe == ".pdf";
            Bitmap b;
            try { b = canvas.Render(); }
            catch (Exception ex) { SaveFailed(ex); return; }
            Flash("Guardando " + Path.GetFileName(file) + "\u2026", false);
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
                        Flash("Guardada: " + Path.GetFileName(file), true);
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
            MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        void CopyOut()
        {
            canvas.CommitText();
            try
            {
                if (owner != null) owner.CopyTracked(path, canvas.Render(), false, !Pending);
                else using (Bitmap b = canvas.Render()) ShotStack.CopyImage(b);
                Feedback(copyItem);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Copiar (editor): " + ex.Message);
                MessageBox.Show(this, "No se pudo copiar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Save = keep a copy in the save folder; everything else is temporary.
        void KeepCopy()
        {
            if (!Apply()) return;
            try
            {
                if (owner != null) owner.Keep(path);
                Feedback(saveItem);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar (editor): " + ex.Message);
                MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Enter: apply, copy and close, ready to paste or drag the thumbnail.
        // For videos: export with annotations and backdrop and add the result to the stack.
        // Written before closing (not in the background), so a paste right after Enter never gets the previous clipboard.
        // The PNG is encoded once: the clipboard reads it back from the file.
        void Done()
        {
            if (IsVideo) { ExportVideo(); return; }
            canvas.CommitText(); // a text still being typed counts as a change to write
            if (Pending && !Apply()) return;
            CopyOut();
            closeWithoutAsking = true;
            Close();
        }

        // Pins the annotated image on top of everything; the editor stays open.
        void PinOut()
        {
            canvas.CommitText();
            try { PinWindow.Open(canvas.Render()); }
            catch (Exception ex) { ShotStack.Log("Fijar (editor): " + ex.Message); }
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
            string frame = Path.Combine(Path.GetTempPath(), "stackshot-frame-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                string err;
                int code = RunFfmpeg(ff, "-hide_banner -y " + Recorder.SafeInput + "-i " + Recorder.Quote(path) + " -frames:v 1 " + Recorder.Quote(frame), out err);
                if (code != 0 || !File.Exists(frame))
                {
                    ShotStack.Log("Presentar: no se pudo leer " + path + ": " + Tail(err));
                    MessageBox.Show("No se pudo abrir la grabaci\u00F3n.\n\n" + Tail(err), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                double dur = 0;
                Match m = Regex.Match(err, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                if (m.Success)
                    dur = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                Editor e = new Editor(owner, path, ShotStack.LoadFull(frame), ff, dur);
                // The sound and the frame rate are kept in the export.
                e.hasAudio = Regex.IsMatch(err, @"Stream #0:\d+[^\n]*: Audio:");
                Match fm = Regex.Match(err, @"Stream #0:\d+[^\n]*: Video:[^\n]*?([0-9.]+) fps");
                double fps;
                if (fm.Success && double.TryParse(fm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out fps) && fps > 0) e.sourceFps = fps;
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

        // Stderr is read asynchronously so the 30 s timeout works even if FFmpeg hangs.
        static int RunFfmpeg(string ff, string args, out string stderr)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = ff;
                p.StartInfo.Arguments = "-nostdin " + args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sb) { if (sb.Length < 16000) sb.AppendLine(e.Data); }
                };
                p.Start();
                p.BeginErrorReadLine();
                bool exited = p.WaitForExit(30000);
                if (!exited) { try { p.Kill(); } catch { } }
                p.WaitForExit(2000); // drain the remaining output
                lock (sb) stderr = sb.ToString();
                return exited ? p.ExitCode : -1;
            }
        }

        // Frame strip for the timeline: one FFmpeg pass extracts small, evenly spaced frames in the background.
        void LoadThumbs()
        {
            const int n = 14;
            string ff = ffmpeg, src = path;
            // As tall as the strip in device pixels, so the frames stay sharp on HiDPI screens.
            int th = Math.Max(72, timeline.StripHeight + 1) & ~1;
            double dur = duration;
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-strip-" + Guid.NewGuid().ToString("N"));
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap[] thumbs = new Bitmap[n];
                try
                {
                    Directory.CreateDirectory(dir);
                    string err, rate = (n / Math.Max(0.1, dur)).ToString("0.####", CultureInfo.InvariantCulture);
                    RunFfmpeg(ff, "-hide_banner -y " + Recorder.SafeInput + "-i " + Recorder.Quote(src) + " -vf \"fps=" + rate + ",scale=-2:" + th + "\" -frames:v " + n + " " +
                                  Recorder.Quote(Path.Combine(dir, "t%02d.png")), out err);
                    for (int i = 0; i < n; i++)
                    {
                        string f = Path.Combine(dir, "t" + (i + 1).ToString("00") + ".png");
                        if (File.Exists(f)) thumbs[i] = ShotStack.LoadFull(f);
                    }
                }
                catch (Exception ex) { ShotStack.Log("Tira de fotogramas: " + ex.Message); }
                finally { try { Directory.Delete(dir, true); } catch { } }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed) { foreach (Bitmap b in thumbs) if (b != null) b.Dispose(); return; }
                        timeline.Thumbs = thumbs;
                        timeline.Invalidate();
                    });
                }
                catch { foreach (Bitmap b in thumbs) if (b != null) b.Dispose(); }
            });
        }

        // Shows the frame at t under the marks; only the latest request wins.
        void ShowFrameAt(double t)
        {
            int req = ++frameRequest;
            string ff = ffmpeg, src = path;
            string png = Path.Combine(Path.GetTempPath(), "stackshot-frame-" + Guid.NewGuid().ToString("N") + ".png");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap bmp = null;
                try
                {
                    string err;
                    if (RunFfmpeg(ff, "-hide_banner -y -ss " + t.ToString("0.000", CultureInfo.InvariantCulture) + " " + Recorder.SafeInput + "-i " + Recorder.Quote(src) +
                                      " -frames:v 1 " + Recorder.Quote(png), out err) == 0 && File.Exists(png))
                        bmp = ShotStack.LoadFull(png);
                }
                catch (Exception ex) { ShotStack.Log("Fotograma: " + ex.Message); }
                finally { try { if (File.Exists(png)) File.Delete(png); } catch { } }
                if (bmp == null) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (req != frameRequest || IsDisposed) bmp.Dispose();
                        else canvas.ReplaceImage(bmp);
                    });
                }
                catch { bmp.Dispose(); }
            });
        }

        static string Tail(string t)
        {
            t = (t ?? "").Trim();
            return t.Length > 300 ? t.Substring(t.Length - 300) : t;
        }

        // FFmpeg puts the (cropped, padded) video below and an image with backdrop, corners and annotations on top.
        // Runs in the background; the hint strip shows progress.
        void ExportVideo()
        {
            if (export != null) return;
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
            List<double[]> keep = trimmed || cuts ? timeline.Keep() : null;
            bool multi = keep != null && keep.Count > 1;
            if (keep != null && keep.Count == 0)
            {
                MessageBox.Show(this, "No queda ning\u00FAn tramo dentro del recorte.", "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            double speed = timeline != null ? Timeline.SpeedValues[timeline.Speed] : 1;
            int maxH = timeline != null ? Timeline.SizeValues[timeline.OutSize] : 0;
            toGif = timeline != null ? timeline.Format == 1 : IsGif;
            bool options = speed != 1 || maxH > 0 || toGif != IsGif;
            if (!bg && !marks && !cropped && !trimmed && !cuts && !options) { closeWithoutAsking = true; Close(); return; }
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
                MessageBox.Show(this, "No se pudo preparar el v\u00EDdeo: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string dir = owner != null ? Settings.TempDir : Path.GetDirectoryName(path);
            string ext = toGif ? ".gif" : ".mp4";
            exportOut = ShotStack.Unique(Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + " (editado)" + ext));

            // Composited at full chroma resolution, so the backdrop's rounded corners and the marks keep clean edges;
            // the overlay image is converted with the video's own BT.709 matrix.
            string vin = multi ? "[cv]" : "[0:v]";
            int topIn = multi ? keep.Count : 1;
            string f = vin + (toGif ? "" : "format=yuv444p,") + "crop=" + crop.Width + ":" + crop.Height + ":" + crop.X + ":" + crop.Y;
            if (bg) f += ",pad=" + frame.Width + ":" + frame.Height + ":" + inner.X + ":" + inner.Y + ":black";
            if (topPng != null)
            {
                if (toGif) f += "[v];[v][" + topIn + ":v]overlay=0:0:format=auto";
                else f = "[" + topIn + ":v]scale=out_color_matrix=bt709:out_range=tv,format=yuva444p[top];" + f + "[v];[v][top]overlay=0:0:format=yuv444";
            }
            if (speed != 1) f += ",setpts=PTS/" + speed.ToString("0.###", CultureInfo.InvariantCulture);
            // Never upscale: only shrink when the result is taller than the chosen size.
            if (maxH > 0 && frame.Height > maxH)
            {
                frame = new Size(Math.Max(2, (int)Math.Round(frame.Width * (double)maxH / frame.Height)) & ~1, maxH);
                f += ",scale=" + frame.Width + ":" + frame.Height + ":flags=lanczos";
            }
            string args = "-hide_banner -loglevel error -nostats -progress pipe:1 -y ";
            bool aud = hasAudio && !toGif;
            string pre = "";
            if (keep != null)
            {
                // One input per kept range (seeking each, so nothing is buffered), joined with concat.
                foreach (double[] k in keep)
                    args += "-ss " + k[0].ToString("0.000", CultureInfo.InvariantCulture) + " -to " + k[1].ToString("0.000", CultureInfo.InvariantCulture) + " " +
                            Recorder.SafeInput + "-i " + Recorder.Quote(path) + " ";
                if (multi)
                {
                    for (int i = 0; i < keep.Count; i++) pre += "[" + i + ":v]" + (aud ? "[" + i + ":a]" : "");
                    pre += "concat=n=" + keep.Count + ":v=1:a=" + (aud ? 1 : 0) + "[cv]" + (aud ? "[ca]" : "") + ";";
                }
            }
            else args += Recorder.SafeInput + "-i " + Recorder.Quote(path);
            if (topPng != null) args += " -i " + Recorder.Quote(topPng);
            if (toGif)
            {
                // Same as recording: per-GIF palette and light dithering, at most 960 px wide; videos drop to 15 fps.
                int gw = Math.Min(frame.Width, 960) & ~1;
                f += (IsGif ? "" : ",fps=15") + ",scale=" + gw + ":-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle[o]";
                args += " -filter_complex \"" + pre + f + "\" -map \"[o]\" -loop 0 " + Recorder.Quote(exportOut);
            }
            else
            {
                f += ",format=yuv420p[o]";
                // As good as the chosen recording quality, never below "Alta": an export must not look worse than its source.
                int quality = bgs.VideoQuality == Recorder.Standard ? Recorder.High : bgs.VideoQuality;
                // The sound follows the trim and the speed (atempo keeps the pitch).
                string audio = "-an";
                if (hasAudio && speed != 1)
                {
                    f += ";" + (multi ? "[ca]" : "[0:a]") + "atempo=" + speed.ToString("0.###", CultureInfo.InvariantCulture) + "[ao]";
                    audio = "-map \"[ao]\" " + Recorder.AudioCodec(quality);
                }
                else if (hasAudio) audio = "-map " + (multi ? "\"[ca]\"" : "0:a:0") + " " + Recorder.AudioCodec(quality);
                args +=" -filter_complex \"" + pre + f + "\" -map \"[o]\" " + Recorder.FinalCodec(quality, (long)frame.Width * frame.Height, sourceFps) + " " + audio +
                        " -movflags +faststart " + Recorder.Quote(exportOut);
            }

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
                double fr = Math.Max(0, Math.Min(1, t.TotalSeconds / span));
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
                // The overlay image is no longer needed, even if cancelled or closed.
                if (topPng != null) { try { File.Delete(topPng); } catch { } }
                try { BeginInvoke((Action)delegate { ExportDone(p, code, err); }); }
                catch { try { p.Dispose(); } catch { } }
            };
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                p.Dispose();
                if (topPng != null) { try { File.Delete(topPng); } catch { } }
                MessageBox.Show(this, "No se pudo iniciar FFmpeg: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            export = p;
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
            hint.LeftText = (toGif ? "Creando el GIF\u2026 " : "Creando el v\u00EDdeo\u2026 ") + (duration > 0 ? (int)Math.Round(fr * 100) + " %" : "");
            hint.RightText = "Esc cancela";
            hint.Invalidate();
        }

        void ExportDone(Process p, int code, string err)
        {
            if (export != p) { p.Dispose(); return; } // cancelled
            export = null;
            p.Dispose();
            hint.Progress = -1;
            canvas.Enabled = true;
            bar.Locked = false;
            bgPanel.Enabled = true;
            if (timeline != null) timeline.Enabled = true;
            if (code != 0 || !File.Exists(exportOut))
            {
                ShotStack.Log("Presentar: FFmpeg " + code + ": " + Tail(err));
                try { if (File.Exists(exportOut)) File.Delete(exportOut); } catch { }
                UpdateUi();
                MessageBox.Show(this, "No se pudo crear el v\u00EDdeo.\n\n" + Tail(err), "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShotStack.Log("Presentar: listo " + Path.GetFileName(exportOut));
            if (owner != null) owner.AddRecording(exportOut);
            else MessageBox.Show(this, "Listo: " + exportOut, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
            closeWithoutAsking = true;
            Close();
        }

        void CancelExport()
        {
            Process p = export;
            if (p == null) return;
            export = null;
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { p.WaitForExit(3000); } catch { }
            try { if (File.Exists(exportOut)) File.Delete(exportOut); } catch { }
            hint.Progress = -1;
            canvas.Enabled = true;
            bar.Locked = false;
            bgPanel.Enabled = true;
            if (timeline != null) timeline.Enabled = true;
            UpdateUi();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (export != null)
            {
                if (e.CloseReason == CloseReason.UserClosing &&
                    MessageBox.Show(this, "\u00BFCancelar la exportaci\u00F3n?", "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
            if (IsVideo)
            {
                DialogResult v = MessageBox.Show(this, "\u00BFExportar la grabaci\u00F3n con los cambios?", "Stackshot",
                                                 MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (v == DialogResult.Cancel) e.Cancel = true;
                else if (v == DialogResult.Yes) { e.Cancel = true; ExportVideo(); }
                return;
            }
            DialogResult r = MessageBox.Show(this, "\u00BFConservar los cambios en la captura?", "Stackshot",
                                             MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Apply())) e.Cancel = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Ds.Changed -= onDs;
            EdTip.Cancel();
            if (owner == null) WaitForSaves();
            feedback.Dispose();
            if (flashTimer != null) flashTimer.Dispose();
            canvas.Release();
            if (timeline != null && timeline.Thumbs != null) foreach (Bitmap b in timeline.Thumbs) if (b != null) b.Dispose();
        }
    }

}
