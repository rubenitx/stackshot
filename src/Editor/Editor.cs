// Stackshot - Ventana del editor rápido.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public class Editor : Form
    {
        static readonly Tool[] ToolOrder = { Tool.Arrow, Tool.Rect, Tool.Ellipse, Tool.Text, Tool.Counter, Tool.Highlight, Tool.Pixelate, Tool.Crop };
        static readonly string[] ToolNames = { "Flecha", "Recuadro", "Elipse", "Texto", "N\u00FAmeros", "Resaltar", "Pixelar", "Recortar" };
        static readonly string[] ToolKeys = { "F", "R", "E", "T", "N", "H", "P", "C" };
        static readonly string[] ToolGlyphs = { null, "\uE739", "\uEA3A", "\uE8D2", null, "\uE7E6", "\uE8B3", "\uE7A8" };
        static readonly string[] WeightNames = { "Fino", "Medio", "Grueso" };

        readonly ShotStack owner;
        readonly string path;
        readonly Canvas canvas;
        readonly Bar bar;
        readonly Hint hint;
        readonly float s;
        readonly Timer feedback = new Timer();
        readonly Dictionary<Tool, Bar.Item> toolItems = new Dictionary<Tool, Bar.Item>();
        readonly List<Bar.Item> colorItems = new List<Bar.Item>(), weightItems = new List<Bar.Item>();
        readonly Bar.Item undoItem, redoItem, copyItem, saveItem;
        bool dirty, closeWithoutAsking;

        public Editor(ShotStack owner, string path, Bitmap img)
        {
            this.owner = owner;
            this.path = path;
            Screen scr = Screen.FromPoint(Control.MousePosition);
            s = ShotStack.ScaleFor(scr);

            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Dark;
            ForeColor = Theme.Fg;
            Font = new Font("Segoe UI", P(13), GraphicsUnit.Pixel);
            StartPosition = FormStartPosition.Manual;
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;

            canvas = new Canvas(img);
            canvas.Ui = s;
            canvas.Dock = DockStyle.Fill;
            canvas.Changed += delegate { dirty = true; UpdateUi(); };
            canvas.StateChanged += delegate { UpdateUi(); };

            bar = new Bar();
            bar.S = s;
            bar.Dock = DockStyle.Top;
            bar.Height = P(52);
            for (int i = 0; i < ToolOrder.Length; i++)
            {
                Tool t = ToolOrder[i];
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

            hint = new Hint();
            hint.S = s;
            hint.Dock = DockStyle.Bottom;
            hint.Height = P(28);

            Controls.Add(canvas);
            Controls.Add(hint);
            Controls.Add(bar);

            // Tamaño: la captura encajada en el 88 % de la pantalla, nunca más estrecha que la barra.
            int minW = bar.LayoutItems();
            Rectangle wa = scr.WorkingArea;
            float pad = 28 * s;
            int chrome = P(52) + P(28);
            float maxW = wa.Width * 0.88f - 2 * pad, maxH = wa.Height * 0.88f - chrome - 2 * pad;
            float kk = Math.Min(ShotStack.MaxZoom(img.Size), Math.Min(maxW / img.Width, maxH / img.Height));
            ClientSize = new Size(Math.Min(wa.Width, Math.Max(minW, (int)(img.Width * kk + 2 * pad))),
                                  chrome + Math.Max(P(260), (int)(img.Height * kk + 2 * pad)));
            MinimumSize = new Size(Math.Min(wa.Width, Width - ClientSize.Width + minW), P(380));
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2), wa.Top + Math.Max(0, (wa.Height - Height) / 2));

            feedback.Interval = 1300;
            feedback.Tick += delegate { feedback.Stop(); copyItem.ShowAlt = false; saveItem.ShowAlt = false; bar.Invalidate(); };
            UpdateUi();
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

        void SetTool(Tool t) { canvas.SetTool(t); }
        void SetColor(int i) { canvas.SetColor(Theme.Palette[i]); }
        void SetWeight(int i) { canvas.SetWeight(i); }

        // La barra refleja la herramienta, y el color y el grosor de la marca seleccionada (o de las siguientes).
        void UpdateUi()
        {
            foreach (KeyValuePair<Tool, Bar.Item> kv in toolItems) kv.Value.On = kv.Key == canvas.Tool;
            int c = canvas.ActiveColor.ToArgb(), w = canvas.ActiveWeight;
            for (int i = 0; i < colorItems.Count; i++) colorItems[i].On = Theme.Palette[i].ToArgb() == c;
            for (int i = 0; i < weightItems.Count; i++) weightItems[i].On = i == w;
            undoItem.Enabled = canvas.CanUndo;
            redoItem.Enabled = canvas.CanRedo;
            bar.Invalidate();
            Size o = canvas.OutputSize;
            hint.LeftText = canvas.HintText;
            hint.RightText = o.Width + " \u00D7 " + o.Height + " px  \u00B7  Enter copia y cierra";
            hint.Invalidate();
            Text = "Editar \u00B7 " + Path.GetFileName(path) + (dirty ? "  \u2022" : "");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Barra de título oscura y del mismo color que la barra de herramientas, aunque Windows tenga
            // activado el color de énfasis en los títulos.
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);                       // modo oscuro
                int caption = Theme.Dark.R | (Theme.Dark.G << 8) | (Theme.Dark.B << 16);
                Native.DwmSetWindowAttribute(Handle, 35, ref caption, 4);                  // color del título
                int text = Theme.Fg.R | (Theme.Fg.G << 8) | (Theme.Fg.B << 16);
                Native.DwmSetWindowAttribute(Handle, 36, ref text, 4);                     // color del texto
                int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);                   // borde
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            canvas.Focus();
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
                case Keys.Control | Keys.C: CopyOut(); return true;
                case Keys.Control | Keys.S: KeepCopy(); return true;
                case Keys.Enter: Done(); return true;
                case Keys.Escape:
                    if (canvas.HasSelection) canvas.SelectShape(null);
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
                case Keys.C: SetTool(Tool.Crop); return true;
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

        // Aplica las marcas (y el recorte) sobre la captura temporal. Si ya estaba guardada, actualiza también la copia.
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
                if (owner != null) owner.CopyTracked(path, canvas.Render(), false);
                else using (Bitmap b = canvas.Render()) ShotStack.CopyImage(b);
                Feedback(copyItem);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Copiar (editor): " + ex.Message);
                MessageBox.Show(this, "No se pudo copiar: " + ex.Message, "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Guardar = conservarla en Imágenes\Capturas; todo lo demás es temporal.
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

        // Enter: aplica las marcas (si hay), copia y cierra. Queda listo para pegar o arrastrar la miniatura.
        void Done()
        {
            if (dirty && !Apply()) return;
            CopyOut();
            closeWithoutAsking = true;
            Close();
        }

        // La imagen con sus marcas queda flotando encima de todo (el editor sigue abierto).
        void PinOut()
        {
            canvas.CommitText();
            try { PinWindow.Open(canvas.Render()); }
            catch (Exception ex) { ShotStack.Log("Fijar (editor): " + ex.Message); }
        }

        // Arrastrar desde el botón: la imagen marcada viaja con el cursor, centrada en él.
        void StartDragOut()
        {
            if (dirty && !Apply()) return;
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (closeWithoutAsking || !dirty || e.CloseReason != CloseReason.UserClosing) return;
            DialogResult r = MessageBox.Show(this, "\u00BFConservar las marcas en la captura?", "Stackshot",
                                             MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Apply())) e.Cancel = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            feedback.Dispose();
            canvas.Release();
        }
    }

}
