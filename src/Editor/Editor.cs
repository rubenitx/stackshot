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
        static readonly string[] ToolGlyphs = { null, "\uE739", "\uEA3A", "\uE8D2", null, "\uE7E6", "\uE8B3", null, "\uE7A8" };
        static readonly string[] WeightNames = { "Fino", "Medio", "Grueso" };
        // Captures that already have the backdrop applied, so reopening them doesn't add a second one.
        static readonly HashSet<string> withBackdrop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        readonly ShotStack owner;
        readonly string path;
        readonly Canvas canvas;
        readonly Bar bar;
        readonly BgPanel bgPanel;
        readonly Hint hint;
        readonly Timeline timeline;
        readonly float s;
        int frameRequest;
        readonly Settings bgs;
        readonly Timer feedback = new Timer();
        readonly Dictionary<Tool, Bar.Item> toolItems = new Dictionary<Tool, Bar.Item>();
        readonly List<Bar.Item> colorItems = new List<Bar.Item>(), weightItems = new List<Bar.Item>();
        readonly Bar.Item undoItem, redoItem, copyItem, saveItem, bgItem;
        bool dirty, closeWithoutAsking;
        // Presentation mode: the first frame is annotated and FFmpeg applies the result to the whole video.
        readonly string ffmpeg;
        readonly double duration;
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
            BackColor = Theme.Dark;
            ForeColor = Theme.Fg;
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            StartPosition = FormStartPosition.Manual;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;

            canvas = new Canvas(img);
            canvas.Ui = s;
            canvas.Dock = DockStyle.Fill;
            canvas.Bg = bgs;
            canvas.BgOn = bgs.BgAuto && !withBackdrop.Contains(path);
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
            bar.Add(Bar.Kind.Gap);
            for (int i = 0; i < Theme.Palette.Length; i++)
            {
                int idx = i;
                Bar.Item it = bar.Add(Bar.Kind.Swatch);
                it.Swatch = Theme.Palette[i];
                it.Tip = Theme.PaletteNames[i] + "  (" + (i + 1) + ")";
                it.Do = delegate { SetColor(idx); };
                colorItems.Add(it);
            }
            bar.Add(Bar.Kind.Gap);
            for (int i = 0; i < WeightNames.Length; i++)
            {
                int idx = i;
                Bar.Item it = bar.Add(Bar.Kind.Weight);
                it.Index = i;
                it.Tip = "Grosor: " + WeightNames[i] + "  (\u2212 / +)";
                it.Do = delegate { SetWeight(idx); };
                weightItems.Add(it);
            }
            bar.Add(Bar.Kind.Gap);
            undoItem = Button(null, "\uE7A7", "Deshacer  (Ctrl+Z)", delegate { canvas.Undo(); }, false);
            redoItem = Button(null, "\uE7A6", "Rehacer  (Ctrl+Y)", delegate { canvas.Redo(); }, false);
            bar.Add(Bar.Kind.Gap);
            bgItem = Button("Fondo", "\uE771", "Fondo de presentaci\u00F3n: degradado, margen, esquinas y sombra  (B)", ToggleBgPanel, false);

            if (IsVideo)
            {
                Bar.Item done = Button("Exportar", "\uE898",
                                       "Crear la versi\u00F3n con el recorte, las marcas, el fondo y las opciones de abajo  (Enter)", Done, true);
                done.Accent = true;
            }
            else
            {
                Button(null, "\uE718", "Fijar en pantalla: queda flotando encima de todo", PinOut, true);
                Bar.Item drag = Button("Arrastrar", "\uE7C2", "Arr\u00E1strala al chat o a otra aplicaci\u00F3n", null, true);
                drag.DragOut = true;
                copyItem = Button("Copiar", "\uE8C8", "Copiar al portapapeles  (Ctrl+C)", CopyOut, true);
                copyItem.Alt = "Copiado \u2713";
                saveItem = Button("Guardar", "\uE74E", owner != null ? "Conservarla en tu carpeta de capturas  (Ctrl+S)" : "Guardar sobre la imagen  (Ctrl+S)", KeepCopy, true);
                saveItem.Alt = "Guardada \u2713";
                Bar.Item done = Button("Listo", "\uE73E", "Aplicar, copiar y cerrar  (Enter)", Done, true);
                done.Accent = true;
                bar.DragOut += StartDragOut;
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
                timeline.Duration = duration;
                timeline.Out = duration;
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

            // Size: fit the capture in 88% of the screen, never narrower than the toolbar.
            int minW = bar.LayoutItems();
            Rectangle wa = scr.WorkingArea;
            float pad = 28 * s;
            int chrome = P(52) + P(28) + (timeline != null ? timeline.Height : 0);
            float maxW = wa.Width * 0.88f - 2 * pad, maxH = wa.Height * 0.88f - chrome - 2 * pad;
            float kk = Math.Min(ShotStack.MaxZoom(img.Size), Math.Min(maxW / img.Width, maxH / img.Height));
            ClientSize = new Size(Math.Min(wa.Width, Math.Max(minW, (int)(img.Width * kk + 2 * pad))),
                                  chrome + Math.Max(P(260), (int)(img.Height * kk + 2 * pad)));
            MinimumSize = new Size(Math.Min(wa.Width, Width - ClientSize.Width + minW), P(380));
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2), wa.Top + Math.Max(0, (wa.Height - Height) / 2));

            feedback.Interval = 1300;
            feedback.Tick += delegate
            {
                feedback.Stop();
                if (copyItem != null) copyItem.ShowAlt = false;
                if (saveItem != null) saveItem.ShowAlt = false;
                bar.Invalidate();
            };
            UpdateUi();
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

        // The toolbar reflects the tool and the selected shape's color and weight (or the next shape's).
        void UpdateUi()
        {
            foreach (KeyValuePair<Tool, Bar.Item> kv in toolItems) kv.Value.On = kv.Key == canvas.Tool;
            int c = canvas.ActiveColor.ToArgb(), w = canvas.ActiveWeight;
            for (int i = 0; i < colorItems.Count; i++) colorItems[i].On = Theme.Palette[i].ToArgb() == c;
            for (int i = 0; i < weightItems.Count; i++) weightItems[i].On = i == w;
            undoItem.Enabled = canvas.CanUndo;
            redoItem.Enabled = canvas.CanRedo;
            bgItem.On = canvas.BgOn || bgPanel.Visible;
            bar.Invalidate();
            bgPanel.Invalidate();
            if (export != null) return; // the hint strip shows export progress
            Size o = canvas.OutputSize;
            hint.LeftText = canvas.HintText;
            hint.RightText = o.Width + " \u00D7 " + o.Height + " px  \u00B7  " + (IsVideo ? "Enter exporta" : "Enter copia y cierra");
            hint.Invalidate();
            Text = (IsVideo ? "Editar v\u00EDdeo \u00B7 " : "Editar \u00B7 ") + Path.GetFileName(path) + (dirty ? "  \u2022" : "");
            if (timeline != null) timeline.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Dark title bar matching the toolbar, even if accent-colored title bars are enabled.
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);                       // DWMWA_USE_IMMERSIVE_DARK_MODE
                int caption = Theme.Dark.R | (Theme.Dark.G << 8) | (Theme.Dark.B << 16);
                Native.DwmSetWindowAttribute(Handle, 35, ref caption, 4);                  // DWMWA_CAPTION_COLOR
                int text = Theme.Fg.R | (Theme.Fg.G << 8) | (Theme.Fg.B << 16);
                Native.DwmSetWindowAttribute(Handle, 36, ref text, 4);                     // DWMWA_TEXT_COLOR
                int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);                   // DWMWA_BORDER_COLOR
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

        // Writes annotations, crop and backdrop to the temp capture, and refreshes the saved copy if there is one.
        bool Apply()
        {
            canvas.CommitText();
            try
            {
                using (Bitmap b = canvas.Render())
                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, FormatFor(path));
                    File.WriteAllBytes(path, ms.ToArray());
                }
                if (canvas.BgOn) withBackdrop.Add(path);
                dirty = false;
                UpdateUi();
                if (owner != null && owner.IsKept(path)) owner.Keep(path);
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Something to write: unapplied annotations or a backdrop not yet in the file.
        bool Pending { get { return dirty || (canvas.BgOn && !withBackdrop.Contains(path)); } }

        void Feedback(Bar.Item it)
        {
            copyItem.ShowAlt = it == copyItem;
            saveItem.ShowAlt = it == saveItem;
            bar.Invalidate();
            feedback.Stop();
            feedback.Start();
        }

        void CopyOut()
        {
            canvas.CommitText();
            try
            {
                if (owner != null) owner.CopyTracked(path, canvas.Render(), false, false);
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
        void Done()
        {
            if (IsVideo) { ExportVideo(); return; }
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
                return new Editor(owner, path, ShotStack.LoadFull(frame), ff, dur);
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
            double dur = duration;
            string dir = Path.Combine(Path.GetTempPath(), "stackshot-strip-" + Guid.NewGuid().ToString("N"));
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap[] thumbs = new Bitmap[n];
                try
                {
                    Directory.CreateDirectory(dir);
                    string err, rate = (n / Math.Max(0.1, dur)).ToString("0.####", CultureInfo.InvariantCulture);
                    RunFfmpeg(ff, "-hide_banner -y " + Recorder.SafeInput + "-i " + Recorder.Quote(src) + " -vf \"fps=" + rate + ",scale=-2:72\" -frames:v " + n + " " +
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
            bool bg = canvas.BgOn, marks = canvas.HasMarks;
            bool cropped = crop != new Rectangle(0, 0, canvas.Img.Width & ~1, canvas.Img.Height & ~1);
            bool trimmed = timeline != null && timeline.IsTrimmed;
            double speed = timeline != null ? Timeline.SpeedValues[timeline.Speed] : 1;
            int maxH = timeline != null ? Timeline.SizeValues[timeline.OutSize] : 0;
            toGif = timeline != null ? timeline.Format == 1 : IsGif;
            bool options = speed != 1 || maxH > 0 || toGif != IsGif;
            if (!bg && !marks && !cropped && !trimmed && !options) { closeWithoutAsking = true; Close(); return; }
            double span = (trimmed ? timeline.Out - timeline.In : duration) / speed;

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

            string f = "[0:v]crop=" + crop.Width + ":" + crop.Height + ":" + crop.X + ":" + crop.Y;
            if (bg) f += ",pad=" + frame.Width + ":" + frame.Height + ":" + inner.X + ":" + inner.Y + ":black";
            if (topPng != null) f += "[v];[v][1:v]overlay=0:0:format=auto";
            if (speed != 1) f += ",setpts=PTS/" + speed.ToString("0.###", CultureInfo.InvariantCulture);
            // Never upscale: only shrink when the result is taller than the chosen size.
            if (maxH > 0 && frame.Height > maxH)
            {
                frame = new Size(Math.Max(2, (int)Math.Round(frame.Width * (double)maxH / frame.Height)) & ~1, maxH);
                f += ",scale=" + frame.Width + ":" + frame.Height + ":flags=lanczos";
            }
            string args = "-hide_banner -loglevel error -nostats -progress pipe:1 -y ";
            if (trimmed)
            {
                double tin = timeline.In, tout = timeline.Out;
                args += "-ss " + tin.ToString("0.000", CultureInfo.InvariantCulture) + " -to " + tout.ToString("0.000", CultureInfo.InvariantCulture) + " ";
            }
            args += Recorder.SafeInput + "-i " + Recorder.Quote(path);
            if (topPng != null) args += " -i " + Recorder.Quote(topPng);
            if (toGif)
            {
                // Same as recording: per-GIF palette and light dithering, at most 960 px wide; videos drop to 15 fps.
                int gw = Math.Min(frame.Width, 960) & ~1;
                f += (IsGif ? "" : ",fps=15") + ",scale=" + gw + ":-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle[o]";
                args += " -filter_complex \"" + f + "\" -map \"[o]\" -loop 0 " + Recorder.Quote(exportOut);
            }
            else
            {
                f += ",format=yuv420p[o]";
                args += " -filter_complex \"" + f + "\" -map \"[o]\" -an -c:v libx264 -preset medium -crf 16 -movflags +faststart " + Recorder.Quote(exportOut);
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
            bar.Enabled = false;
            bgPanel.Enabled = false;
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
            bar.Enabled = true;
            bgPanel.Enabled = true;
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
            bar.Enabled = true;
            bgPanel.Enabled = true;
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
            if (closeWithoutAsking || !dirty || e.CloseReason != CloseReason.UserClosing) return;
            if (IsVideo)
            {
                DialogResult v = MessageBox.Show(this, "\u00BFExportar la grabaci\u00F3n con los cambios?", "Stackshot",
                                                 MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (v == DialogResult.Cancel) e.Cancel = true;
                else if (v == DialogResult.Yes) { e.Cancel = true; ExportVideo(); }
                return;
            }
            DialogResult r = MessageBox.Show(this, "\u00BFConservar las marcas en la captura?", "Stackshot",
                                             MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Apply())) e.Cancel = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            feedback.Dispose();
            canvas.Release();
            if (timeline != null && timeline.Thumbs != null) foreach (Bitmap b in timeline.Thumbs) if (b != null) b.Dispose();
        }
    }

}
