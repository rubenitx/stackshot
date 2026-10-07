// Stackshot - Miniatura de una captura en la pila.
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
    // ---------------------------------------------------------------- Miniaturas

    // Una miniatura de la pila. Todo va animado: entra deslizándose desde el borde, las demás se recolocan con un
    // muelle, se desvanece al quitarla y, si la pila cambia de pantalla, se funde aquí y aparece allí.
    // Al pasar el ratón la imagen se sigue viendo entera: solo aparece una barrita abajo y la ✕ arriba,
    // y se puede arrastrar desde cualquier punto (también desde un botón).
    public class Card : FloatWindow
    {
        public enum Exit { Slide, Swipe, Drop }

        public static bool ForceHover = false;

        const string GClose = "\uE711", GPin = "\uE718", GFolder = "\uE838", GSave = "\uE74E", GCheck = "\uE73E",
                     GPlay = "\uE768", GCopy = "\uE8C8", GEdit = "\uE70F";
        const TextFormatFlags Centered = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                         TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        const double FlashMs = 1150;

        class Btn { public Rectangle R; public string Glyph; public string Label; public Action Do; public bool Round; }

        public string FilePath;
        public string SavedPath;   // copia en Imagenes\Capturas si el usuario la guardo
        public readonly bool IsMedia;
        public string Device;      // pantalla en la que está
        public bool Leaving;       // ya no está en la pila: solo le queda la animación de salida
        readonly ShotStack owner;
        readonly List<Btn> btns = new List<Btn>();
        Bitmap preview, face, hoverBuf;
        Rectangle faceRect, bar;
        Size origSize = new Size(16, 9);
        float nextScale = 1f;
        Rectangle home;
        bool hover, pressed, gestureDecided, swiping, dragging, migrating, parked, fadeByPos, closeAfterDrag;
        int hot = -1, prevHot = -1, pressBtn = -1, pressShown = -1, swipeStartX;
        Point pressPt;
        double swipeV, swipeLastX, swipeLastT;
        string flash;
        Color flashColor;
        readonly Tween dim = new Tween(1), hoverT = new Tween(0), hotT = new Tween(1), pressT = new Tween(0),
                       flashT = new Tween(0), badgeT = new Tween(1), wait = new Tween(0);

        public Card(ShotStack owner, string path)
        {
            this.owner = owner;
            FilePath = path;
            IsMedia = ShotStack.IsMediaFile(path);
        }

        // Pulsada, deslizándose o arrastrándose: la pila no debe moverla de pantalla.
        public bool Busy { get { return pressed || swiping || dragging; } }

        // Fuera de la vista porque hay más de las que caben.
        public bool Parked { get { return parked; } }

        public Size WantedFor(float scale)
        {
            int w = (int)Math.Round(240 * scale);
            int h = (int)Math.Round(w * (double)origSize.Height / Math.Max(1, origSize.Width));
            return new Size(w, Math.Max((int)Math.Round(96 * scale), Math.Min((int)Math.Round(200 * scale), h)));
        }

        protected override bool HoldX { get { return swiping; } }

        protected override void Settled()
        {
            if (!swiping && !Leaving) fadeByPos = false;
        }

        // Al deslizarla hacia el borde se va desvaneciendo: pasado el 30 % de su ancho, soltarla la descarta.
        protected override double AlphaFactor()
        {
            double f = dim.Value;
            if (fadeByPos)
            {
                double d = (home.X - x) / Math.Max(1.0, Width * 0.6);
                f *= 1 - 0.85 * Math.Max(0, Math.Min(1, d));
            }
            return f;
        }

        // ------------------------------------------------------------ Sitio en la pila

        // La pila le da su sitio. Si es nueva, entra deslizándose desde el borde; si la pila se ha ido a otra
        // pantalla, se desvanece aquí y aparece allí (de abajo arriba); si no cabe (hay muchas), se aparca:
        // se va por arriba o por abajo mientras se desvanece y se oculta hasta que vuelva a tocarle.
        public void Place(Rectangle r, string device, float scale, int order, bool visible)
        {
            home = r;
            if (Leaving) return;
            if (Device == null)
            {
                Device = device;
                s = scale;
                SetSize(r.Size);
                if (visible) SlideIn();
                else { parked = true; JumpTo(r.X, r.Y); }
                return;
            }
            if (!visible)
            {
                bool sameScreen = device == Device;
                Device = device;
                nextScale = scale;
                if (parked) return;
                parked = true;
                migrating = false;
                if (!Visible) return;
                if (sameScreen) MoveTo(r.X, r.Y, 320, 0.9, 0); // se va hacia su lado mientras se desvanece
                alpha.Go(0, 170, 0, Ease.OutCubic, Park);
                Anim.Wake(this);
                return;
            }
            if (parked)
            {
                // Vuelve a tocarle: aparece por el lado por el que se fue.
                parked = false;
                bool fromAbove = y < r.Y;
                Device = device;
                s = scale;
                SetSize(r.Size);
                BuildButtons();
                if (!Visible)
                {
                    alpha.Set(0);
                    ApplyAlpha();
                    JumpTo(r.X, r.Y + (fromAbove ? -P(48) : P(48)));
                }
                ShowQuiet();
                alpha.Go(1, 200, order * 18, Ease.OutCubic, null);
                MoveTo(r.X, r.Y, 320, 0.8, order * 18);
                return;
            }
            if (device != Device)
            {
                Device = device;
                nextScale = scale;
                if (!migrating)
                {
                    migrating = true;
                    double delay = order * 28;
                    MoveTo(x - P(28), y, 300, 1, delay);
                    alpha.Go(0, 130, delay, Ease.InCubic, Arrive);
                    Anim.Wake(this);
                }
                return;
            }
            if (migrating) { nextScale = scale; return; } // Arrive ya usará el sitio nuevo
            if (scale != s || r.Size != Size) { s = scale; SetSize(r.Size); BuildButtons(); }
            MoveTo(r.X, r.Y, 320, 0.8, 0);
        }

        void SlideIn()
        {
            JumpTo(home.X - P(56), home.Y);
            alpha.Set(0);
            ApplyAlpha();
            alpha.Go(1, 220, 0, Ease.OutCubic, null);
            MoveTo(home.X, home.Y, 260, 0.68, 0);
        }

        // Ya invisible en la pantalla de antes: salta a la nueva y entra.
        void Arrive()
        {
            migrating = false;
            if (Leaving || IsDisposed) return;
            s = nextScale;
            SetSize(home.Size);
            BuildButtons();
            SlideIn();
        }

        // Aparcada del todo: se oculta y suelta las imágenes (vuelven a cargarse si vuelve a verse).
        void Park()
        {
            if (!parked || Leaving) return;
            moving = false;
            Hide();
            ReleaseImages();
        }

        // Sale de la pila con una animación y se cierra al acabar.
        public void Dismiss(Exit how, double delay)
        {
            if (Leaving) return;
            Leaving = true;
            migrating = false;
            pressed = false;
            hover = false;
            if (!IsHandleCreated || !Visible || parked) { Close(); return; }
            SetHot(-1);
            UpdateHover();
            switch (how)
            {
                case Exit.Swipe: // sigue el gesto hacia el borde
                    fadeByPos = true;
                    MoveTo(x - P(160), y, 140, 1, 0);
                    alpha.Go(0, 180, 0, Ease.OutCubic, Finish);
                    break;
                case Exit.Drop: // ya está en otra aplicación: se desvanece donde está
                    alpha.Go(0, 170, 0, Ease.OutCubic, Finish);
                    break;
                default:
                    MoveTo(x - P(44), y, 300, 1, delay);
                    alpha.Go(0, 190, delay, Ease.InCubic, Finish);
                    break;
            }
            Anim.Wake(this);
        }

        void Finish()
        {
            if (dragging) { closeAfterDrag = true; return; }
            Close();
        }

        // Se ha pegado: un "Pegada" visible un momento y luego se va.
        public void Used(string text, Action then)
        {
            if (Leaving) return;
            if (parked) { then(); return; }
            Flash(text, Theme.Green);
            wait.Set(0);
            wait.Go(1, 560, 0, Ease.Linear, then);
            Anim.Wake(this);
        }

        protected override bool StepExtra(double now)
        {
            bool repaint = hoverT.Running || hotT.Running || pressT.Running || flashT.Running || badgeT.Running;
            dim.Step(now);
            hoverT.Step(now);
            hotT.Step(now);
            pressT.Step(now);
            flashT.Step(now);
            badgeT.Step(now);
            wait.Step(now);
            if (IsDisposed) return false;
            SetBorder(Mix(Theme.Border, Theme.Accent, ForceHover ? 1 : hoverT.Value));
            // Sin ratón encima, el lienzo del velo no hace falta: se suelta.
            if (!hoverT.Running && hoverT.Value <= 0 && hoverBuf != null && !ForceHover) { hoverBuf.Dispose(); hoverBuf = null; }
            if (repaint) Invalidate();
            return dim.Running || hoverT.Running || hotT.Running || pressT.Running || flashT.Running || badgeT.Running || wait.Running;
        }

        // ------------------------------------------------------------ Contenido

        // Comprueba que se puede leer y se queda con su tamaño real. La vista previa se suelta en cuanto se ha
        // dibujado la miniatura, así que cada miniatura ocupa poco más que lo que se ve.
        public bool Reload()
        {
            Bitmap p = LoadPreview();
            if (p == null && !IsMedia) return false;
            ReleaseImages();
            preview = p;
            Invalidate();
            return true;
        }

        // Captura recién hecha: la vista previa llega ya preparada (el PNG todavía se está escribiendo).
        public void UsePreview(Bitmap p, Size orig)
        {
            ReleaseImages();
            preview = p;
            origSize = orig;
            Invalidate();
        }

        Bitmap LoadPreview()
        {
            try
            {
                Size orig;
                if (IsMedia)
                {
                    Bitmap t = ShotStack.ShellThumb(FilePath, 512);
                    if (t != null) origSize = t.Size;
                    return t;
                }
                Bitmap b = ShotStack.LoadPreview(FilePath, 600, out orig);
                origSize = orig;
                return b;
            }
            catch (Exception ex)
            {
                ShotStack.Log("No se pudo leer " + FilePath + ": " + ex.Message);
                return null;
            }
        }

        void ReleaseImages()
        {
            if (preview != null) { preview.Dispose(); preview = null; }
            if (face != null) { face.Dispose(); face = null; }
            if (hoverBuf != null) { hoverBuf.Dispose(); hoverBuf = null; }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            BuildButtons();
            Invalidate();
        }

        // Arriba a la izquierda, la ✕; abajo, una barrita con Copiar, Editar, Guardar y Fijar.
        void BuildButtons()
        {
            btns.Clear();
            int W = ClientSize.Width, H = ClientSize.Height;
            int cs = P(24), m = P(6);
            AddBtn(new Rectangle(m, m, cs, cs), GClose, "Cerrar", delegate { owner.Remove(this); }, true);

            List<Btn> tools = new List<Btn>();
            tools.Add(MakeBtn(GCopy, "Copiar", Copy));
            if (IsMedia)
            {
                tools.Add(MakeBtn(GPlay, "Abrir", OpenFile));
                tools.Add(MakeBtn(GEdit, "Presentar", Edit)); // marcas y fondo sobre la grabación
            }
            else tools.Add(MakeBtn(GEdit, "Editar", Edit));
            if (SavedPath == null) tools.Add(MakeBtn(GSave, "Guardar", Keep));
            else tools.Add(MakeBtn(GFolder, "Abrir carpeta", ShowInFolder));
            if (!IsMedia) tools.Add(MakeBtn(GPin, "Fijar en pantalla", Pin));
            int bw = P(32), bh = P(28), pad = P(3);
            int tw = tools.Count * bw + 2 * pad, th = bh + 2 * pad;
            bar = new Rectangle((W - tw) / 2, H - th - P(8), tw, th);
            for (int i = 0; i < tools.Count; i++)
            {
                tools[i].R = new Rectangle(bar.X + pad + i * bw, bar.Y + pad, bw, bh);
                btns.Add(tools[i]);
            }
        }

        static Btn MakeBtn(string glyph, string label, Action act)
        {
            Btn b = new Btn();
            b.Glyph = glyph; b.Label = label; b.Do = act;
            return b;
        }

        void AddBtn(Rectangle r, string glyph, string label, Action act, bool round)
        {
            Btn b = MakeBtn(glyph, label, act);
            b.R = r;
            b.Round = round;
            btns.Add(b);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < btns.Count; i++)
            {
                if (btns[i].R.Contains(p)) return i;
            }
            return -1;
        }

        // Encaja la imagen sin ampliarla por encima de su tamaño real.
        Rectangle ImageRect(Rectangle box)
        {
            double f = Math.Min((double)box.Width / origSize.Width, (double)box.Height / origSize.Height);
            f = Math.Min(f, Math.Max(1.0, s));
            int w = Math.Max(1, (int)Math.Round(origSize.Width * f));
            int h = Math.Max(1, (int)Math.Round(origSize.Height * f));
            return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        // La vista previa ya escalada al tamaño de la miniatura: dibujar cada fotograma cuesta casi nada.
        Bitmap Face()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return null;
            Rectangle ir = ImageRect(ClientRectangle);
            if (face != null && faceRect == ir) return face;
            if (preview == null) preview = LoadPreview();
            if (preview == null) return null;
            if (face != null) face.Dispose();
            face = new Bitmap(ir.Width, ir.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(face))
            using (ImageAttributes ia = new ImageAttributes())
            {
                Quality(g);
                ia.SetWrapMode(WrapMode.TileFlipXY); // sin bordes oscuros al reducir
                g.DrawImage(preview, new Rectangle(0, 0, ir.Width, ir.Height), 0, 0, preview.Width, preview.Height, GraphicsUnit.Pixel, ia);
            }
            faceRect = ir;
            preview.Dispose();
            preview = null;
            return face;
        }

        // ------------------------------------------------------------ Dibujo

        static void Blit(Graphics g, Image img, int left, int top, ImageAttributes ia)
        {
            InterpolationMode im = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(img, new Rectangle(left, top, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            g.InterpolationMode = im;
        }

        static Rectangle Shrink(Rectangle r, double f)
        {
            int dx = (int)Math.Round(r.Width * f / 2), dy = (int)Math.Round(r.Height * f / 2);
            return Rectangle.Inflate(r, -dx, -dy);
        }

        // Debajo, la miniatura sola; encima, la capa con los botones, que aparece con un fundido.
        // Las dos capas son opacas, así que los iconos se dibujan igual de nítidos que siempre.
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Quality(g);
            Rectangle r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;
            double h = ForceHover ? 1 : hoverT.Value;
            if (h < 0.995) PaintBase(g, r);
            if (h > 0.005)
            {
                if (hoverBuf == null || hoverBuf.Size != r.Size)
                {
                    if (hoverBuf != null) hoverBuf.Dispose();
                    hoverBuf = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
                }
                using (Graphics hg = Graphics.FromImage(hoverBuf)) PaintHover(hg, r, h);
                if (h >= 0.995) Blit(g, hoverBuf, 0, 0, null);
                else
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix33 = (float)h;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        Blit(g, hoverBuf, 0, 0, ia);
                    }
                }
                PaintLabel(g, h);
            }
            PaintFlash(g, r);
        }

        void PaintFace(Graphics g, Rectangle r)
        {
            g.Clear(Theme.Bg);
            Bitmap f = Face();
            if (f != null) Blit(g, f, faceRect.X, faceRect.Y, null);
            else
            {
                using (Font fn = new Font("Segoe UI", P(12), GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, Path.GetFileName(FilePath), fn, r, Theme.Fg, Centered | TextFormatFlags.EndEllipsis);
            }
        }

        void PaintBase(Graphics g, Rectangle r)
        {
            PaintFace(g, r);
            if (IsMedia)
            {
                int d = P(40);
                Rectangle c = new Rectangle((r.Width - d) / 2, (r.Height - d) / 2, d, d);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(210, Theme.Dark))) g.FillEllipse(b, c);
                DrawGlyph(g, GPlay, c, Theme.Fg, P(16));
                string ext = Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
                using (Font f = new Font("Segoe UI Semibold", P(11), GraphicsUnit.Pixel))
                {
                    Size ts = TextRenderer.MeasureText(ext, f);
                    Rectangle lr = new Rectangle(P(8), r.Height - P(8) - P(20), ts.Width + P(12), P(20));
                    using (GraphicsPath p = Theme.Round(lr, P(10)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(210, Theme.Dark))) g.FillPath(b, p);
                    TextRenderer.DrawText(g, ext, f, lr, Theme.Fg, Centered);
                }
            }
            if (SavedPath != null)
            {
                // El check verde de guardada aparece con un pequeño salto.
                double pop = badgeT.Value;
                int d = (int)Math.Round(P(22) * pop);
                if (d > 2)
                {
                    int cx = r.Width - P(8) - P(11), cy = r.Height - P(8) - P(11);
                    Rectangle c = new Rectangle(cx - d / 2, cy - d / 2, d, d);
                    using (SolidBrush b = new SolidBrush(Theme.Green)) g.FillEllipse(b, c);
                    DrawGlyph(g, GCheck, c, Theme.Dark, (int)Math.Round(P(11) * pop));
                }
            }
        }

        // La imagen se queda a la vista: solo se oscurecen un poco los bordes de arriba y de abajo,
        // donde van los botones, para que se lean sobre cualquier captura.
        void PaintHover(Graphics g, Rectangle r, double h)
        {
            Quality(g);
            PaintFace(g, r);
            int gb = Math.Min(r.Height / 2, P(58));
            using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, r.Height - gb - 1, r.Width, gb + 2),
                   Color.FromArgb(0, 8, 8, 14), Color.FromArgb(170, 8, 8, 14), 90f))
                g.FillRectangle(lg, 0, r.Height - gb, r.Width, gb);
            int gt = P(40);
            using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, -1, r.Width, gt + 2),
                   Color.FromArgb(130, 8, 8, 14), Color.FromArgb(0, 8, 8, 14), 90f))
                g.FillRectangle(lg, 0, 0, r.Width, gt);

            int lift = (int)Math.Round((1 - h) * P(8)); // la barra sube un poco al aparecer
            Rectangle br = bar;
            br.Offset(0, lift);
            using (GraphicsPath p = Theme.Round(br, br.Height / 2f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(236, Theme.Dark)))
            using (Pen pen = new Pen(Color.FromArgb(160, Theme.Border)))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            for (int i = 0; i < btns.Count; i++) DrawBtn(g, btns[i], i, h, btns[i].Round ? 0 : lift);
        }

        double HotAmount(int i)
        {
            double t = hotT.Value;
            return (i == hot ? t : 0) + (i == prevHot ? 1 - t : 0);
        }

        void DrawBtn(Graphics g, Btn b, int i, double h, int lift)
        {
            double hotA = HotAmount(i);
            double press = i == pressShown ? pressT.Value : 0;
            if (b.Round)
            {
                // La ✕ crece un poco al aparecer y se pone roja al pasar por encima.
                Rectangle r = Shrink(b.R, (1 - h) * 0.3 + press * 0.1);
                using (SolidBrush br = new SolidBrush(Mix(Color.FromArgb(225, Theme.Dark), Theme.Red, hotA))) g.FillEllipse(br, r);
                int px = (int)Math.Round(P(11) * r.Width / (double)Math.Max(1, b.R.Width));
                DrawGlyph(g, b.Glyph, r, Mix(Theme.Fg, Theme.Dark, hotA), px);
                return;
            }
            Rectangle rr = b.R;
            rr.Offset(0, lift);
            rr = Shrink(rr, press * 0.1);
            if (hotA > 0.01)
            {
                using (GraphicsPath p = Theme.Round(rr, rr.Height / 2f))
                using (SolidBrush br = new SolidBrush(Mix(Color.FromArgb(0, Theme.Accent), Theme.Accent, hotA))) g.FillPath(br, p);
            }
            DrawGlyph(g, b.Glyph, rr, Mix(Theme.Fg, Theme.Dark, hotA), P(15));
        }

        // Nombre del botón de la barra que está debajo del ratón, encima de la barra.
        void PaintLabel(Graphics g, double h)
        {
            int i = hot >= 0 ? hot : prevHot;
            if (i < 0 || i >= btns.Count || btns[i].Round) return;
            double a = h * (i == hot ? hotT.Value : 1 - hotT.Value);
            int ai = (int)Math.Round(255 * Math.Max(0, Math.Min(1, a)));
            if (ai < 4) return;
            Btn b = btns[i];
            using (Font f = new Font("Segoe UI Semibold", P(12), GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                SizeF ts = g.MeasureString(b.Label, f, PointF.Empty, sf);
                float w = ts.Width + P(14), hh = P(22);
                float cx = b.R.X + b.R.Width / 2f;
                float left = Math.Max(P(4), Math.Min(ClientSize.Width - P(4) - w, cx - w / 2));
                float top = bar.Y - hh - P(5) + (float)((1 - a) * P(3));
                RectangleF lr = new RectangleF(left, top, w, hh);
                using (GraphicsPath p = Theme.Round(lr, hh / 2f))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(ai * 236 / 255, Theme.Dark))) g.FillPath(bg, p);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush t = new SolidBrush(Color.FromArgb(ai, Theme.Fg))) g.DrawString(b.Label, f, t, lr, sf);
            }
        }

        // "Copiado", "Guardada", "Pegada": salta con un pequeño rebote, se queda un momento y se desvanece.
        void PaintFlash(Graphics g, Rectangle r)
        {
            if (flash == null) return;
            double ms = flashT.Value * FlashMs, sc, a;
            if (ms < 240) { double t = ms / 240; sc = 0.7 + 0.3 * Ease.OutBack(t); a = Ease.OutCubic(t); }
            else if (ms < FlashMs - 260) { sc = 1; a = 1; }
            else { double t = (ms - (FlashMs - 260)) / 260; sc = 1 - 0.08 * t; a = 1 - t; }
            int ai = (int)Math.Round(255 * Math.Max(0, Math.Min(1, a)));
            if (ai == 0) return;
            using (Font f = new Font("Segoe UI Semibold", (float)Math.Max(1, P(13) * sc), GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                SizeF ts = g.MeasureString(flash, f, PointF.Empty, sf);
                float w = ts.Width + (float)(P(22) * sc), hh = (float)(P(30) * sc);
                RectangleF fr = new RectangleF((r.Width - w) / 2f, (r.Height - hh) / 2f, w, hh);
                RectangleF sh = fr;
                sh.Offset(0, P(2));
                using (GraphicsPath p = Theme.Round(sh, hh / 2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(ai * 90 / 255, 0, 0, 0))) g.FillPath(b, p);
                using (GraphicsPath p = Theme.Round(fr, hh / 2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(ai, flashColor))) g.FillPath(b, p);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush t = new SolidBrush(Color.FromArgb(ai, Theme.Dark))) g.DrawString(flash, f, t, fr, sf);
            }
        }

        // ------------------------------------------------------------ Ratón

        // Los botones aparecen con un instante de retraso, para que no parpadeen al pasar de largo.
        void UpdateHover()
        {
            double want = hover && !swiping && !dragging && !Leaving ? 1 : 0;
            if (hoverT.Target == want) return;
            if (want > 0) hoverT.Go(1, 150, hoverT.Value > 0.05 ? 0 : 60, Ease.OutCubic, null);
            else hoverT.Go(0, 170, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        void SetHot(int h)
        {
            if (h == hot) return;
            prevHot = hot;
            hot = h;
            hotT.Set(0);
            hotT.Go(1, 110, 0, Ease.OutCubic, null);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Anim.Wake(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Leaving) owner.Wheel(e.Delta);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Leaving || e.Button != MouseButtons.Left) return;
            pressed = true;
            gestureDecided = false;
            swiping = false;
            pressPt = e.Location;
            pressBtn = HitTest(e.Location);
            if (pressBtn >= 0)
            {
                pressShown = pressBtn;
                pressT.Go(1, 90, 0, Ease.OutCubic, null);
                Anim.Wake(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Leaving) return;
            if (swiping) { SwipeMove(); return; }
            if (!hover) { hover = true; UpdateHover(); }
            if (pressed && !gestureDecided && (e.Button & MouseButtons.Left) != 0)
            {
                int dx = e.X - pressPt.X, dy = e.Y - pressPt.Y;
                Size d = SystemInformation.DragSize;
                if (Math.Abs(dx) > d.Width || Math.Abs(dy) > d.Height)
                {
                    // Da igual dónde se pulsara (también sobre un botón): si se mueve, es un gesto, no un clic.
                    gestureDecided = true;
                    pressBtn = -1;
                    if (pressT.Target > 0) pressT.Go(0, 120, 0, Ease.OutCubic, null);
                    // Hacia el borde izquierdo: deslizar para descartar, como en CleanShot.
                    if (dx < 0 && Math.Abs(dx) >= Math.Abs(dy)) { StartSwipe(dx); return; }
                    // Hacia cualquier otro lado: arrastrar el fichero a otra aplicación.
                    pressed = false;
                    StartDrag();
                    return;
                }
            }
            if (!pressed) SetHot(HitTest(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (swiping || ClientRectangle.Contains(PointToClient(Control.MousePosition))) return;
            hover = false;
            SetHot(-1);
            UpdateHover();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (Leaving) return;
            if (swiping) { EndSwipe(); return; }
            if (pressShown >= 0 && pressT.Target > 0) { pressT.Go(0, 160, 0, Ease.OutCubic, null); Anim.Wake(this); }
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) { owner.Remove(this); return; }
            if (e.Button != MouseButtons.Left || !pressed) return;
            pressed = false;
            int down = pressBtn;
            pressBtn = -1;
            Action act = null;
            if (down >= 0) { if (HitTest(e.Location) == down) act = btns[down].Do; }
            else if (ClientRectangle.Contains(e.Location)) act = IsMedia ? (Action)OpenFile : (Action)Edit;
            if (act != null) BeginInvoke(act);
            SetHot(HitTest(e.Location));
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Si se pierde el ratón a medio deslizar (Alt+Tab, etc.), vuelve a su sitio.
            BeginInvoke((Action)delegate
            {
                if (!swiping || IsDisposed) return;
                swiping = false;
                pressed = false;
                MoveTo(home.X, home.Y, 420, 0.62, 0);
                UpdateHover();
            });
        }

        void StartSwipe(int dx)
        {
            swiping = true;
            fadeByPos = true;
            swipeStartX = Control.MousePosition.X - dx;
            swipeV = 0;
            swipeLastX = x;
            swipeLastT = Anim.Now;
            vx = 0;
            Cursor = Cursors.Default;
            SetHot(-1);
            UpdateHover();
            SwipeMove();
        }

        void SwipeMove()
        {
            Point m = Control.MousePosition;
            // Si el ratón se mete en otra pantalla (la de la izquierda), no quiere descartarla: quiere llevarla allí.
            if (Screen.FromPoint(new Point(m.X + P(30), m.Y)).DeviceName != Device)
            {
                swiping = false;
                pressed = false;
                MoveTo(home.X, home.Y, 420, 0.75, 0);
                StartDrag();
                return;
            }
            int dx = m.X - swipeStartX;
            // Hacia la derecha solo cede un poco, como una goma.
            double nx = home.X + (dx < 0 ? dx : Math.Min(P(14), dx * 0.2));
            double now = Anim.Now, dt = now - swipeLastT;
            if (dt >= 4)
            {
                swipeV = 0.6 * ((nx - swipeLastX) / (dt / 1000.0)) + 0.4 * swipeV;
                swipeLastX = nx;
                swipeLastT = now;
            }
            x = nx;
            vx = 0;
            ApplyPos();
            ApplyAlpha();
        }

        void EndSwipe()
        {
            swiping = false;
            pressed = false;
            double gone = home.X - x;
            if (Anim.Now - swipeLastT > 90) swipeV = 0; // se paró antes de soltar
            // Descartada si pasa del 30 % o si se lanza con fuerza hacia el borde.
            if (gone > Width * 0.3 || (swipeV < -700 && gone > P(12)))
            {
                vx = Math.Min(swipeV, -500);
                owner.Remove(this, Exit.Swipe);
                return;
            }
            MoveTo(home.X, home.Y, 420, 0.62, 0); // vuelve a su sitio con un pequeño rebote
            UpdateHover();
        }

        // Mientras se arrastra, la miniatura va pegada al cursor y la original se queda en penumbra.
        void StartDrag()
        {
            ShotStack.WaitWritten(FilePath);
            if (!File.Exists(FilePath)) return;
            dragging = true;
            SetHot(-1);
            UpdateHover();
            dim.Go(0.35, 160, 0, Ease.OutCubic, null);
            Anim.Wake(this);
            DragDropEffects result = DragDropEffects.None;
            try
            {
                Point grab = pressPt;
                Bitmap ghost;
                int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
                using (Bitmap flat = new Bitmap(w, h, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(flat))
                    {
                        Quality(g);
                        PaintBase(g, new Rectangle(0, 0, w, h));
                    }
                    ghost = FileDrag.Ghost(flat, P(320), P(8), ref grab);
                }
                result = FileDrag.Run(this, FilePath, ghost, grab);
            }
            catch (Exception ex) { ShotStack.Log("Arrastrar: " + ex.Message); }
            dragging = false;
            if (IsDisposed) return;
            if (closeAfterDrag) { Close(); return; }
            // Soltada en otra aplicación: ya cumplió, fuera de la pila.
            if (result != DragDropEffects.None) { owner.Remove(this, Exit.Drop); return; }
            dim.Go(1, 220, 0, Ease.OutCubic, null);
            hover = ClientRectangle.Contains(PointToClient(Control.MousePosition));
            UpdateHover();
            Anim.Wake(this);
        }

        // ------------------------------------------------------------ Acciones

        void Copy()
        {
            try
            {
                if (IsMedia)
                {
                    StringCollection sc = new StringCollection();
                    sc.Add(FilePath);
                    Clipboard.SetFileDropList(sc);
                }
                else
                {
                    owner.CopyTracked(FilePath, ShotStack.LoadFull(FilePath), false, true);
                }
                Flash("Copiado", Theme.Green);
            }
            catch (Exception ex)
            {
                ShotStack.Log("Copiar: " + ex.Message);
                Flash("No se pudo copiar", Theme.Red);
            }
        }

        void Edit() { owner.OpenEditor(FilePath); }

        void Pin()
        {
            owner.Pin(FilePath);
            owner.Remove(this);
        }

        void OpenFile()
        {
            ShotStack.WaitWritten(FilePath);
            try { Process.Start(FilePath); }
            catch (Exception ex) { ShotStack.Log("Abrir: " + ex.Message); }
        }

        void ShowInFolder()
        {
            ShotStack.WaitWritten(FilePath);
            try { Process.Start("explorer.exe", "/select,\"" + (SavedPath ?? FilePath) + "\""); }
            catch (Exception ex) { ShotStack.Log("Carpeta: " + ex.Message); }
        }

        void Keep()
        {
            try { owner.Keep(FilePath); }
            catch (Exception ex)
            {
                ShotStack.Log("Guardar: " + ex.Message);
                Flash("No se pudo guardar", Theme.Red);
            }
        }

        public void MarkSaved(string dest)
        {
            bool first = SavedPath == null;
            SavedPath = dest;
            BuildButtons();
            if (first)
            {
                badgeT.Set(0);
                badgeT.Go(1, 420, 0, Ease.OutBack, null);
                Flash("Guardada", Theme.Green);
            }
            else Invalidate();
        }

        void Flash(string text, Color c)
        {
            flash = text;
            flashColor = c;
            flashT.Set(0);
            flashT.Go(1, FlashMs, 0, Ease.Linear, delegate { flash = null; Invalidate(); });
            Anim.Wake(this);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            ReleaseImages();
        }
    }

    // Pastilla encima o debajo de la pila cuando no caben todas: "3 anteriores" / "2 más recientes".
    // Un clic pasa una página; la rueda del ratón (aquí o sobre cualquier miniatura) va de una en una.
}
