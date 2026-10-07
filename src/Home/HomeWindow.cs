// Stackshot - Ventana principal: inicio con la mascota, atajos y todos los ajustes, al estilo de macOS.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Stackshot
{
    // Una sola ventana dibujada a mano: barra lateral con secciones y, a la derecha, la sección elegida. Los
    // cambios se aplican al momento (como en los Ajustes de macOS: no hay botón de guardar). Cerrarla la deja en
    // segundo plano, en la bandeja.
    //
    // Rendimiento: la barra lateral y el contenido se dibujan en dos lienzos que solo se rehacen cuando algo
    // cambia; en cada fotograma se copian (casi gratis) y encima van la mascota y lo que se esté animando. Con la
    // ventana escondida no hay temporizador ni consumo.
    public partial class HomeWindow : Form
    {
        const int LW = 980, LH = 660, Side = 240, Bar = 52;
        static readonly string[] PageIds = { "home", "keys", "general", "editor", "record", "mascot", "about" };
        static readonly string[] PageNames = { "Inicio", "Atajos", "General", "Fondo y editor", "Grabaci\u00F3n", "Mascota", "Acerca de" };
        static readonly string[] PageIcons = { "home", "keyboard", "gear", "photo", "video", "bot", "info" };

        readonly ShotStack owner;
        readonly Settings settings;
        float s = 1f;
        string page = "home";
        readonly List<Widget> items = new List<Widget>();     // la sección actual (coordenadas de contenido)
        readonly List<Widget> nav = new List<Widget>();       // la barra lateral (coordenadas de ventana)
        Widget hot, pressed;
        Dib side, content;
        bool sideDirty = true, contentDirty = true;
        int contentHeight;
        double scroll, scrollTarget;
        readonly Mascot mascot = new Mascot();
        readonly Timer timer = new Timer();
        readonly Tween pageIn = new Tween(1);
        Intro intro;
        HotkeyRow listening;
        TextBox nameBox;
        Rectangle nameRect;
        Point mouse = new Point(-1, -1);
        Point lastScreenMouse;
        bool active, mascotHot;
        int captionHot = -1, captionDown = -1;
        double nextTip, lastFrame;
        string bubbleText;
        RectangleF heroMascot;          // dónde va la mascota grande (contenido) en Inicio
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        Image logo;

        public HomeWindow(ShotStack owner, Settings settings)
        {
            this.owner = owner;
            this.settings = settings;
            Text = "Stackshot";
            if (ShotStack.AppIcon != null) Icon = ShotStack.AppIcon;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Mac.Window;
            KeyPreview = true;
            ShowInTaskbar = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            logo = ShotStack.LoadResourceImage("logo.png");
            mascot.Hue = settings.MascotColor;
            s = ShotStack.ScaleFor(Screen.FromPoint(Control.MousePosition));
            Size = new Size(P(LW), P(LH));
            Center(Screen.FromPoint(Control.MousePosition));
            timer.Interval = 15;
            timer.Tick += Tick;
            owner.Captured += OnCaptured;
            Build();
        }

        int P(float v) { return (int)Math.Round(v * s); }
        int ViewW { get { return P(LW) - P(Side); } }
        int ViewH { get { return P(LH); } }

        void Center(Screen scr)
        {
            Rectangle wa = scr.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + Math.Max(0, (wa.Height - Height) / 2 - P(10)));
        }

        // ------------------------------------------------------------ Mostrar y esconder

        public void Present(string pageId, bool withIntro)
        {
            bool wasHidden = !Visible || WindowState == FormWindowState.Minimized;
            if (!Visible)
            {
                // Si la pantalla en la que estaba ya no está, al centro de la del ratón.
                bool onScreen = false;
                foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(Bounds)) onScreen = true;
                if (!onScreen) Center(Screen.FromPoint(Control.MousePosition));
            }
            if (pageId != null && pageId != page) SetPage(pageId, false);
            if (withIntro)
            {
                if (intro != null) intro.Dispose();
                intro = new Intro(Anim.Now);
                intro.TargetSize = P(30);
                intro.Target = new PointF(P(20) + P(15), P(24) + P(15));
            }
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.ForceForeground(Handle);
            if (intro == null && wasHidden && settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(Greeting() + " \u00BFQu\u00E9 capturamos?");
            }
            nextTip = Anim.Now + 25000;
            timer.Start();
            Invalidate();
        }

        void HideToTray()
        {
            CancelListening();
            if (!settings.CloseToTray) { owner.Quit(); return; }
            Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { timer.Stop(); ReleaseLayers(); }
        }

        // Minimizada no se ve: sin temporizador (0 % de CPU) hasta que vuelva.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) timer.Stop();
            else if (Visible && !timer.Enabled) { lastFrame = 0; timer.Start(); Invalidate(); }
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); active = true; Invalidate(); }
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); active = false; CancelListening(); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                owner.Captured -= OnCaptured;
                timer.Dispose();
                ReleaseLayers();
                if (intro != null) intro.Dispose();
                foreach (Font f in fonts.Values) f.Dispose();
                fonts.Clear();
                if (logo != null) logo.Dispose();
            }
            base.Dispose(disposing);
        }

        void ReleaseLayers()
        {
            if (side != null) { side.Dispose(); side = null; }
            if (content != null) { content.Dispose(); content = null; }
            sideDirty = contentDirty = true;
        }

        // ------------------------------------------------------------ Marco de la ventana

        // Sin barra de título de Windows (la ventana entera es nuestra), pero con la sombra, las esquinas y las
        // animaciones de minimizar del sistema.
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0083: // WM_NCCALCSIZE
                    if (m.WParam != IntPtr.Zero) { m.Result = IntPtr.Zero; return; }
                    break;
                case 0x0084: // WM_NCHITTEST: la franja de arriba arrastra la ventana
                {
                    Point p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                    if (p.Y >= 0 && p.Y < P(Bar) && CaptionAt(p) < 0 && HitTest(p) == null && intro == null) { m.Result = (IntPtr)2; return; }
                    m.Result = (IntPtr)1;
                    return;
                }
                case 0x02E0: // WM_DPICHANGED: otra pantalla con otra escala
                {
                    s = (m.WParam.ToInt64() & 0xFFFF) / 96f;
                    Native.RECT r = (Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                    foreach (Font f in fonts.Values) f.Dispose();
                    fonts.Clear();
                    Bounds = new Rectangle(r.Left, r.Top, P(LW), P(LH));
                    ReleaseLayers();
                    Build();
                    Invalidate();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);   // modo oscuro
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4); // esquinas redondeadas
                int border = 58 | (58 << 8) | (62 << 16);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch { }
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); // SWP_FRAMECHANGED
        }


        // ------------------------------------------------------------ Secciones

        void SetPage(string id, bool animate)
        {
            if (Array.IndexOf(PageIds, id) < 0) id = "home";
            CancelListening();
            page = id;
            scroll = scrollTarget = 0;
            Build();
            if (animate) { pageIn.Set(0); pageIn.Go(1, 260, 0, Ease.OutCubic, null); }
            if (id == "home" && settings.MascotOn && animate)
            {
                mascot.PopIn();
                if (settings.MascotTalks && mascot.Bubble == null && new Random().Next(3) == 0) mascot.Say(Tip(), 4200);
            }
            Invalidate();
        }

        void Build()
        {
            hot = pressed = null;
            nav.Clear();
            int y = P(80);
            for (int i = 0; i < PageIds.Length; i++)
            {
                NavItem n = new NavItem(PageIds[i], PageNames[i], PageIcons[i]);
                n.R = new Rectangle(P(12), y, P(Side - 24), P(36));
                nav.Add(n);
                y += P(38);
            }
            MiniMascot mm = new MiniMascot();
            mm.R = new Rectangle(P(12), P(LH) - P(118), P(Side - 24), P(104));
            nav.Add(mm);

            items.Clear();
            RemoveNameBox();
            switch (page)
            {
                case "keys": BuildKeys(); break;
                case "general": BuildGeneral(); break;
                case "editor": BuildEditor(); break;
                case "record": BuildRecord(); break;
                case "mascot": BuildMascot(); break;
                case "about": BuildAbout(); break;
                default: BuildHome(); break;
            }
            contentHeight = P(40);
            foreach (Widget w in items) contentHeight = Math.Max(contentHeight, w.R.Bottom + P(40));
            sideDirty = contentDirty = true;
            if (content != null && content.Height < Math.Max(ViewH, contentHeight)) { content.Dispose(); content = null; }
            PlaceNameBox();
        }

        // ------------------------------------------------------------ Bucle de animación

        void Tick(object sender, EventArgs e)
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            double now = Anim.Now;
            double dt = lastFrame == 0 ? 16 : Math.Min(60, now - lastFrame);
            lastFrame = now;

            bool repaintAll = false;
            if (intro != null)
            {
                repaintAll = true;
                if (intro.T(now) > Intro.RevealAt + 120 && !introGreeted)
                {
                    introGreeted = true;
                    if (settings.MascotOn)
                    {
                        mascot.PopIn();
                        if (settings.MascotTalks) mascot.Greet(Greeting() + " Soy " + settings.MascotName + ". \u00BFQu\u00E9 capturamos?");
                    }
                }
                if (intro.Done(now)) { intro.Dispose(); intro = null; introGreeted = false; sideDirty = true; }
            }

            // Desplazamiento suave.
            double before = scroll;
            scrollTarget = Math.Max(0, Math.Min(Math.Max(0, contentHeight - ViewH), scrollTarget));
            scroll += (scrollTarget - scroll) * (1 - Math.Exp(-dt / 70.0));
            if (Math.Abs(scrollTarget - scroll) < 0.5) scroll = scrollTarget;
            if (scroll != before) { repaintAll = true; PlaceNameBox(); }

            pageIn.Step(now);
            if (pageIn.Running) repaintAll = true;
            foreach (Widget w in items) { w.Step(now); if (w.Running) { contentDirty = true; repaintAll = true; } }
            foreach (Widget w in nav) { w.Step(now); if (w.Running) { sideDirty = true; repaintAll = true; } }

            // La mascota mira al ratón aunque esté fuera de la ventana.
            Point sm = Control.MousePosition;
            bool moved = sm != lastScreenMouse;
            lastScreenMouse = sm;
            Point cm = PointToClient(sm);
            RectangleF oldBounds = settings.MascotOn ? mascot.PaintBounds : RectangleF.Empty;
            UpdateMascotBox();
            mascot.Step(now, cm, moved);
            if (settings.MascotOn && settings.MascotTalks && now > nextTip && intro == null)
            {
                nextTip = now + 38000 + new Random().NextDouble() * 20000;
                if (mascot.Bubble == null && !mascot.Sleeping) mascot.Say(Tip(), 5200);
            }

            // Fotogramas según haga falta: 60 por segundo mientras algo se anima de verdad; 30 si solo se mueve el
            // ratón (la mascota lo sigue con la mirada); si solo flota, unos 25; sin la mascota y sin nada, 10.
            if (moved) lastMove = now;
            bool lively = repaintAll || listening != null || (settings.MascotOn && mascot.Lively);
            bool looking = settings.MascotOn && now - lastMove < 600;
            int interval = lively ? 15 : looking ? 33 : settings.MascotOn ? 40 : 100;
            if (!active && !lively && !looking) interval = settings.MascotOn ? 50 : 200;
            if (timer.Interval != interval) timer.Interval = interval;

            if (repaintAll) { Invalidate(); return; }
            if (settings.MascotOn)
            {
                RectangleF r = RectangleF.Union(oldBounds, mascot.PaintBounds);
                Invalidate(Rectangle.Round(RectangleF.Inflate(r, 2, 2)));
                if (mascot.BubbleAlpha > 0 || bubbleText != null) Invalidate(BubbleRect(true));
            }
            if (listening != null) { contentDirty = true; Invalidate(); } // el campo de atajo late
        }
        bool introGreeted;
        double lastMove;

        void UpdateMascotBox()
        {
            if (page == "home" || page == "mascot")
            {
                RectangleF b = heroMascot;
                b.Offset(P(Side), (float)(-scroll + ContentShift));
                mascot.Box = b;
            }
            else
            {
                Widget mm = nav[nav.Count - 1];
                float d = P(74);
                mascot.Box = new RectangleF(mm.R.X + P(10), mm.R.Y + P(14), d, d);
            }
        }

        float ContentShift { get { return (float)((1 - pageIn.Value) * P(12)); } }

        // ------------------------------------------------------------ Dibujo

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            EnsureLayers();
            IntPtr hdc = g.GetHdc();
            try
            {
                Native.BitBlt(hdc, 0, 0, side.Width, side.Height, side.Dc, 0, 0, Native.SRCCOPY);
                int sy = (int)Math.Round(scroll), shift = (int)Math.Round(ContentShift);
                if (shift > 0)
                {
                    // Mientras entra una sección, por arriba asoma el fondo.
                    using (Dib bg = new Dib(ViewW, shift))
                    {
                        using (Graphics bgg = bg.Graphics()) bgg.Clear(Mac.Window);
                        Native.BitBlt(hdc, P(Side), 0, ViewW, shift, bg.Dc, 0, 0, Native.SRCCOPY);
                    }
                }
                Native.BitBlt(hdc, P(Side), shift, ViewW, ViewH - shift, content.Dc, 0, sy, Native.SRCCOPY);
            }
            finally { g.ReleaseHdc(hdc); }

            Mac.Quality(g);
            if (pageIn.Value < 1)
            {
                using (SolidBrush b = new SolidBrush(Mac.Alpha(Mac.Window, 1 - pageIn.Value))) g.FillRectangle(b, P(Side), 0, ViewW, ViewH);
            }
            if (scroll > 1)
            {
                // Al desplazar, el contenido se desvanece bajo la franja de arriba (como la barra de macOS).
                Rectangle fade = new Rectangle(P(Side), 0, ViewW, P(Bar));
                double k = Math.Min(1, scroll / P(30));
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(fade.X, fade.Y - 1, fade.Width, fade.Height + 2),
                                                                       Mac.Alpha(Mac.Window, k), Color.FromArgb(0, Mac.Window), 90f))
                    g.FillRectangle(b, fade);
            }
            PaintCaption(g);
            if (settings.MascotOn && (intro == null || intro.T(Anim.Now) > Intro.RevealAt))
            {
                Region old = g.Clip;
                if (page == "home" || page == "mascot") g.SetClip(new Rectangle(P(Side), 0, ViewW, ViewH), CombineMode.Intersect);
                mascot.Paint(g);
                g.Clip = old;
                old.Dispose();
                PaintBubble(g);
            }
            if (intro != null) intro.Paint(g, ClientRectangle, Anim.Now, s);
        }

        void EnsureLayers()
        {
            if (side == null) { side = new Dib(P(Side), P(LH)); sideDirty = true; }
            int ch = Math.Max(ViewH, contentHeight);
            if (content == null || content.Height < ch || content.Width != ViewW) { if (content != null) content.Dispose(); content = new Dib(ViewW, ch); contentDirty = true; }
            if (sideDirty)
            {
                using (Graphics g = side.Graphics()) { Mac.Quality(g); PaintSide(g); }
                sideDirty = false;
            }
            if (contentDirty)
            {
                using (Graphics g = content.Graphics())
                {
                    Mac.Quality(g);
                    g.Clear(Mac.Window);
                    foreach (Widget w in items)
                    {
                        // Si una pieza falla, que no se lleve por delante el resto de la sección.
                        GraphicsState st = g.Save();
                        try { w.Paint(g, this); }
                        catch (Exception ex) { ShotStack.Log("Dibujar " + w.GetType().Name + ": " + ex); }
                        g.Restore(st);
                    }
                }
                contentDirty = false;
            }
        }

        void PaintSide(Graphics g)
        {
            g.Clear(Mac.Sidebar);
            using (Pen p = new Pen(Color.FromArgb(44, 44, 48))) g.DrawLine(p, P(Side) - 1, 0, P(Side) - 1, P(LH));
            // Marca.
            Rectangle lr = new Rectangle(P(20), P(24), P(30), P(30));
            if (intro == null || intro.T(Anim.Now) > Intro.Length - 200)
            {
                if (logo != null) g.DrawImage(logo, lr);
            }
            TextRenderer.DrawText(g, "Stackshot", F(15, 2), new Rectangle(P(58), P(22), P(170), P(20)), Mac.Text, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "Versi\u00F3n " + Installer.MyVersion.ToString(3), F(11.5f, 0), new Rectangle(P(58), P(42), P(170), P(16)), Mac.Text3, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            foreach (Widget w in nav) w.Paint(g, this);
        }

        // Botones de Windows 11 arriba a la derecha: minimizar y cerrar (a la bandeja). No hay maximizar: tamaño fijo.
        Rectangle CaptionButton(int i) { return new Rectangle(ClientSize.Width - P(46) * (2 - i), 0, P(46), P(32)); }
        Rectangle CaptionRect { get { return Rectangle.Union(CaptionButton(0), CaptionButton(1)); } }

        int CaptionAt(Point p)
        {
            for (int i = 0; i < 2; i++) if (CaptionButton(i).Contains(p)) return i;
            return -1;
        }

        void PaintCaption(Graphics g)
        {
            string[] glyphs = { "\uE921", "\uE8BB" };
            for (int i = 0; i < 2; i++)
            {
                Rectangle r = CaptionButton(i);
                bool h = captionHot == i, d = h && captionDown == i;
                Color fg = active ? Mac.Text : Mac.Text3;
                if (i == 1 && h)
                {
                    using (SolidBrush b = new SolidBrush(d ? Color.FromArgb(200, 196, 43, 28) : Color.FromArgb(196, 43, 28))) g.FillRectangle(b, r);
                    fg = Color.White;
                }
                else if (h)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(d ? 10 : 18, 255, 255, 255))) g.FillRectangle(b, r);
                }
                Font f = F(10, 3);
                TextRenderer.DrawText(g, glyphs[i], f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        // Con la mascota en grande, el bocadillo va a su derecha; en la barra lateral, encima de su tarjeta.
        bool MiniMode { get { return page != "home" && page != "mascot"; } }

        static readonly Bitmap measureBmp = new Bitmap(1, 1);

        Rectangle BubbleRect(bool inflate)
        {
            string text = mascot.Bubble ?? bubbleText;
            if (text == null) return Rectangle.Empty;
            RectangleF b = mascot.Box;
            int maxW = MiniMode ? P(Side - 24) : P(236);
            SizeF ts;
            using (Graphics mg = Graphics.FromImage(measureBmp))
            {
                mg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                ts = mg.MeasureString(text, F(13, 0), maxW - P(28));
            }
            int w = (int)Math.Ceiling(ts.Width) + P(30), h = (int)Math.Ceiling(ts.Height) + P(20);
            Rectangle r;
            if (MiniMode) r = new Rectangle(P(12), nav[nav.Count - 1].R.Y - h - P(12), w, h);
            else r = new Rectangle((int)(b.Right + P(4)), (int)(b.Y + b.Height * 0.08f), w, h);
            if (inflate) r.Inflate(P(14), P(14));
            return r;
        }

        void PaintBubble(Graphics g)
        {
            if (mascot.Bubble != null) bubbleText = mascot.Bubble;
            if (bubbleText == null) return;
            double a = mascot.BubbleAlpha;
            if (a < 0.02) { if (mascot.Bubble == null) bubbleText = null; return; }
            Rectangle r = BubbleRect(false);
            float pop = (float)(0.92 + 0.08 * a);
            GraphicsState st = g.Save();
            bool mini = MiniMode;
            float tx = mascot.Box.X + mascot.Box.Width / 2;
            float ox = mini ? tx : r.X, oy = mini ? r.Bottom : r.Y + r.Height / 2f;
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(pop, pop);
            g.TranslateTransform(-ox, -oy);
            Color fill = Color.FromArgb((int)(250 * a), 62, 62, 68);
            using (GraphicsPath p = Theme.Round(r, P(14)))
            {
                RectangleF sh = r;
                sh.Offset(0, P(3));
                using (GraphicsPath sp = Theme.Round(sh, P(14)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(70 * a), 0, 0, 0))) g.FillPath(b, sp);
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                PointF[] tail = mini
                    ? new PointF[] { new PointF(tx - P(7), r.Bottom - P(1)), new PointF(tx + P(1), r.Bottom + P(7)), new PointF(tx + P(7), r.Bottom - P(1)) }
                    : new PointF[] { new PointF(r.X + P(1), oy - P(7)), new PointF(r.X - P(7), oy + P(2)), new PointF(r.X + P(1), oy + P(6)) };
                using (SolidBrush b = new SolidBrush(fill)) g.FillPolygon(b, tail);
                using (Pen pen = new Pen(Color.FromArgb((int)(40 * a), 255, 255, 255))) g.DrawPath(pen, p);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(255 * a), Mac.Text)))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                RectangleF tr = new RectangleF(r.X + P(15), r.Y + P(10), r.Width - P(28), r.Height - P(16));
                g.DrawString(bubbleText, F(13, 0), tb, tr);
            }
            g.Restore(st);
        }

        // ------------------------------------------------------------ Ratón y teclado

        Widget HitTest(Point p)
        {
            foreach (Widget w in nav) if (w.Interactive && w.R.Contains(p)) return w;
            if (p.X >= P(Side))
            {
                Point c = new Point(p.X - P(Side), p.Y + (int)Math.Round(scroll));
                for (int i = items.Count - 1; i >= 0; i--) if (items[i].Interactive && items[i].Hit(c)) return items[i];
            }
            return null;
        }

        Point ToContent(Point p) { return new Point(p.X - P(Side), p.Y + (int)Math.Round(scroll)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            if (intro != null) return;
            int ch = CaptionAt(e.Location);
            if (ch != captionHot) { captionHot = ch; Invalidate(CaptionRect); }
            bool lh = ch >= 0;
            bool mh = settings.MascotOn && mascot.Box.Contains(e.Location);
            if (mh != mascotHot) { mascotHot = mh; mascot.Hover(mh); }
            Widget h = HitTest(e.Location);
            if (h != hot)
            {
                if (hot != null) hot.SetHot(false);
                hot = h;
                if (hot != null) hot.SetHot(true);
            }
            if (hot != null) hot.Move(this, IsNav(hot) ? e.Location : ToContent(e.Location));
            Cursor = ((hot != null && hot.Clickable) || mh) && !lh ? Cursors.Hand : Cursors.Default;
        }

        bool IsNav(Widget w) { return nav.Contains(w); }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != null) { hot.SetHot(false); hot = null; }
            if (captionHot >= 0) { captionHot = -1; captionDown = -1; Invalidate(CaptionRect); }
            if (mascotHot) { mascotHot = false; mascot.Hover(false); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (intro != null) { SkipIntro(); return; }
            if (e.Button != MouseButtons.Left) return;
            if (CaptionAt(e.Location) >= 0) { captionDown = CaptionAt(e.Location); Invalidate(CaptionRect); return; }
            if (settings.MascotOn && mascot.Box.Contains(e.Location))
            {
                if (page != "home" && page != "mascot") { SetPage("home", true); return; }
                mascot.Poke(PokeLines());
                return;
            }
            pressed = HitTest(e.Location);
            if (pressed != null) pressed.Down(this, IsNav(pressed) ? e.Location : ToContent(e.Location));
            if (listening != null && pressed != listening) CancelListening();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (captionDown >= 0)
            {
                int i = captionDown;
                captionDown = -1;
                Invalidate(CaptionRect);
                if (CaptionAt(e.Location) != i) return;
                if (i == 1) HideToTray();
                else WindowState = FormWindowState.Minimized;
                return;
            }
            Widget p = pressed;
            pressed = null;
            if (p == null) return;
            p.Up(this);
            if (HitTest(e.Location) == p) p.Click(this, IsNav(p) ? e.Location : ToContent(e.Location));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.X < P(Side)) return;
            scrollTarget -= e.Delta / 120.0 * P(72);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (intro != null && (keyData & Keys.KeyCode) != Keys.None) { SkipIntro(); return true; }
            if (listening != null)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return true;
                if (key == Keys.PrintScreen) return true; // llega al soltarla (OnKeyUp)
                listening.Take(this, keyData);
                return true;
            }
            if (nameBox != null && nameBox.Focused) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Control | Keys.W:
                case Keys.Escape:
                    HideToTray();
                    return true;
                case Keys.Control | Keys.Tab:
                case Keys.Control | Keys.Shift | Keys.Tab:
                {
                    int i = Array.IndexOf(PageIds, page) + ((keyData & Keys.Shift) != 0 ? -1 : 1);
                    SetPage(PageIds[(i + PageIds.Length) % PageIds.Length], true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (listening != null && e.KeyCode == Keys.PrintScreen) listening.Take(this, e.KeyData);
        }

        void SkipIntro()
        {
            if (intro == null) return;
            intro.Dispose();
            intro = null;
            sideDirty = true;
            if (settings.MascotOn)
            {
                mascot.PopIn();
                if (settings.MascotTalks) mascot.Greet(Greeting() + " Soy " + settings.MascotName + ".");
            }
            Invalidate();
        }

        void CancelListening()
        {
            if (listening == null) return;
            listening.Stop(this);
        }

        // ------------------------------------------------------------ Cosas que dice la mascota

        static string Greeting()
        {
            int h = DateTime.Now.Hour;
            return h < 6 ? "\u00A1Buenas noches!" : h < 14 ? "\u00A1Buenos d\u00EDas!" : h < 21 ? "\u00A1Buenas tardes!" : "\u00A1Buenas noches!";
        }

        string[] PokeLines()
        {
            return new string[]
            {
                "\u00A1Eh, que me haces cosquillas!",
                "\u00BFHacemos una captura?",
                "Pulsa " + Hotkeys.Display(settings.HotRegion) + " y yo me encargo.",
                "\u00A1Sonr\u00EDe! Bueno\u2026 yo ya lo hago.",
                "Bip bup. Bip.",
                "Me encanta cuando curvas las flechas.",
                "Puedo quedarme aqu\u00ED todo el d\u00EDa."
            };
        }

        string Tip()
        {
            string[] tips =
            {
                "Arrastra una miniatura a cualquier chat para pegarla.",
                "En el editor, tira del punto azul de una flecha para curvarla.",
                "Con la rueda del rat\u00F3n sobre la pila ves las capturas anteriores.",
                "Al capturar, un clic en una ventana la saca enterita.",
                "En el editor, Enter copia y cierra.",
                Hotkeys.Display(settings.HotScroll) + " captura una p\u00E1gina entera desplaz\u00E1ndola.",
                "El bot\u00F3n Fondo del editor deja tus capturas listas para presentar.",
                "Si cierras esta ventana sigo en la bandeja, junto al reloj."
            };
            return tips[new Random().Next(tips.Length)];
        }

        void OnCaptured(string path)
        {
            if (!Visible || !settings.MascotOn) return;
            string[] lines = { "\u00A1Buena captura!", "\u00A1Clic! Ha quedado genial.", "Esa me la guardo." };
            mascot.Celebrate(settings.MascotTalks ? lines[new Random().Next(lines.Length)] : null);
        }

        // ------------------------------------------------------------ Utilidades de dibujo

        Font F(float px, int weight)
        {
            string key = px + ":" + weight;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            string fam = weight == 3 ? Theme.IconFont : weight == 2 ? Fonts.DisplaySemibold : weight == 1 ? "Segoe UI Semibold" : Mac.TextFont;
            f = new Font(fam, Math.Max(1f, px * s), GraphicsUnit.Pixel);
            fonts[key] = f;
            return f;
        }

        static void Fill(Graphics g, Rectangle r, float radius, Color c)
        {
            using (GraphicsPath p = Theme.Round(r, radius))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        static void Txt(Graphics g, string t, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, t, f, r, c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | extra);
        }

        // Teclas como teclas: "Ctrl", "Mayús", "Impr Pant" en piezas redondeadas. Devuelve el ancho usado.
        int Keycaps(Graphics g, string combo, int right, int cy, bool big, Color fg, Color bg)
        {
            string[] keys = combo.Split(new string[] { " + " }, StringSplitOptions.None);
            Font f = F(big ? 14 : 12, 1);
            int h = P(big ? 30 : 24), pad = P(big ? 10 : 8), gap = P(5);
            int total = 0;
            int[] ws = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ws[i] = Math.Max(h, TextRenderer.MeasureText(keys[i], f).Width + pad * 2 - P(6));
                total += ws[i] + (i > 0 ? gap : 0);
            }
            int x = right - total;
            for (int i = 0; i < keys.Length; i++)
            {
                Rectangle r = new Rectangle(x, cy - h / 2, ws[i], h);
                Rectangle sh = r;
                sh.Offset(0, P(2));
                Fill(g, sh, P(6), Color.FromArgb(20, 20, 22));
                Fill(g, r, P(6), bg);
                using (GraphicsPath p = Theme.Round(r, P(6)))
                using (Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawPath(pen, p);
                Txt(g, keys[i], f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                x += ws[i] + gap;
            }
            return total;
        }

        // Cuadradito de color con un icono blanco, como en los Ajustes del iPhone.
        void IconTile(Graphics g, Rectangle r, string icon, Color a, Color b)
        {
            using (GraphicsPath p = Theme.Round(r, r.Width * 0.26f))
            using (LinearGradientBrush lb = new LinearGradientBrush(Rectangle.Inflate(r, 1, 1), a, b, 90f)) g.FillPath(lb, p);
            int m = (int)(r.Width * 0.2f);
            Icons.Draw(g, icon, Rectangle.Inflate(r, -m, -m), Color.White);
        }
    }
}
