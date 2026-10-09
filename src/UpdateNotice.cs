// Stackshot - New version notice: a small banner by the clock that offers the update and follows its progress.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Windows.Forms;
using W = System.Windows;
using M = System.Windows.Media;
using MI = System.Windows.Media.Imaging;

namespace Stackshot
{
    // Bottom right of the main screen, where Windows shows its notifications. It never takes the focus nor appears in
    // captures. Its buttons download and install the update (or open the release page when it can't be installed from
    // here); a click anywhere else opens Acerca de. It leaves on its own after a while, unless the mouse is on it or the
    // update is under way.
    public class UpdateNotice : FloatWindow
    {
        const float BodyW = 360, Inset = 14, IconSize = 40, Gap = 12, ButtonH = 28;

        readonly ShotStack owner;
        readonly Updater.Release release;
        readonly Timer life = new Timer();
        readonly Action onUpdater, onProgress, onTheme;
        string state = "";              // offer | work | error
        string title, primaryLabel, secondaryLabel;
        bool bar, leaving;
        bool followed;                  // it has shown an update under way (so a failure is its own to report)
        int armed;                      // clicks count from this tick on (a double click doesn't hit the next state)
        int hot = -1;                   // 0 primary button, 1 secondary button, 2 anywhere else
        Rectangle primary, secondary;   // body coordinates
        MI.BitmapSource shadow;
        Size shadowFor;
        static MI.BitmapSource logo;

        public UpdateNotice(ShotStack owner, Updater.Release r)
        {
            this.owner = owner;
            release = r;
            armed = Environment.TickCount;
            s = ShotStack.ScaleFor(Screen.PrimaryScreen);
            Pad = P(30);
            Arrange();
            life.Tick += delegate { life.Stop(); if (hot < 0) Dismiss(); };
            onUpdater = OnUpdater;
            onProgress = delegate { if (state == "work") Redraw(); };
            onTheme = delegate { if (owner != null) owner.Ui(Restyle); };
            Updater.Changed += onUpdater;
            Updater.Progress += onProgress;
            Ds.Changed += onTheme;
        }

        protected override bool PerPixel { get { return true; } }

        double F(double v) { return v * s; }

        public void Present()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int px = wa.Right - P(16) - body.Width, py = wa.Bottom - P(16) - body.Height;
            alpha.Set(0);
            JumpTo(px + P(40), py);
            armed = Environment.TickCount + 250;
            ShowQuiet();
            alpha.Go(1, 240, 0, Ease.OutCubic, null);
            MoveTo(px, py, 260, 0.92, 0);
            Linger(25000);
        }

        public void Dismiss()
        {
            if (leaving || IsDisposed) return;
            leaving = true;
            life.Stop();
            alpha.Go(0, 200, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            MoveTo(x + P(40), y, 260, 1, 0);
        }

        // Leaves after ms without the mouse on it (never while the update runs).
        void Linger(int ms)
        {
            life.Stop();
            if (state == "work" || leaving) return;
            life.Interval = ms;
            life.Start();
        }

        // A failure is shown only for an update this notice has followed; an automatic one that failed before it
        // appeared is simply offered.
        string StateNow()
        {
            Updater.Step d = Updater.Doing;
            if (d == Updater.Step.Downloading || d == Updater.Step.Verifying || d == Updater.Step.Installing) { followed = true; return "work"; }
            if (followed && Updater.Problem != null && Updater.ProblemInstalling) return "error";
            return "offer";
        }

        string Body()
        {
            if (state == "work")
            {
                if (Updater.Doing == Updater.Step.Installing)
                    return Updater.Held ? "En cuanto termine de guardarse la grabaci\u00F3n, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse."
                                        : "Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse en unos segundos.";
                if (Updater.Doing == Updater.Step.Verifying) return "Suma SHA-256 y firma digital.";
                return Updater.Fraction < 0 ? "Conectando con GitHub\u2026" : (int)Math.Round(Updater.Fraction * 100) + " %  \u00B7  despu\u00E9s se comprueba su firma";
            }
            if (state == "error") return Updater.Problem ?? "";
            if (Updater.IsReady && Updater.CanInstall) return "Ya est\u00E1 descargada y verificada. Al instalarla, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse.";
            if (Updater.CanInstall) return "\u00BFQuieres descargarla? Se comprueba su firma y se instala en unos segundos.";
            if (Installer.ManagedByMsi) return "En este equipo las actualizaciones las instala inform\u00E1tica. \u00BFQuieres ver la nueva versi\u00F3n?";
            return "\u00BFQuieres descargarla? Esta versi\u00F3n se descarga desde su p\u00E1gina de GitHub.";
        }

        M.FormattedText TitleText(double width)
        {
            M.FormattedText t = Ink.Px(title, Ds.Semibold, F(13.5), Ds.Brushes.Label);
            t.MaxTextWidth = width;
            t.MaxLineCount = 1;
            t.Trimming = W.TextTrimming.CharacterEllipsis;
            return t;
        }

        M.FormattedText BodyText(double width)
        {
            M.FormattedText t = Ink.Px(Body(), Ds.Regular, F(12.5), Ds.Brushes.Label2);
            t.MaxTextWidth = width;
            t.LineHeight = Math.Round(F(17));
            t.MaxLineCount = 4;
            t.Trimming = W.TextTrimming.WordEllipsis;
            return t;
        }

        int ButtonWidth(string label)
        {
            return Math.Max(P(76), (int)Math.Ceiling(Ink.Px(label, Ds.Semibold, F(12.5), M.Colors.Black).WidthIncludingTrailingWhitespace + F(26)));
        }

        double TextLeft { get { return F(Inset + IconSize + Gap); } }
        double TextWidth { get { return F(BodyW) - TextLeft - F(Inset); } }

        // Texts and buttons for the current state, and the size they need.
        void Arrange()
        {
            state = StateNow();
            string v = release.Version.ToString(3);
            primaryLabel = secondaryLabel = null;
            bar = false;
            if (state == "work")
            {
                title = Updater.Doing == Updater.Step.Downloading ? "Descargando Stackshot " + v
                      : Updater.Doing == Updater.Step.Verifying ? "Comprobando la descarga\u2026" : "Instalando Stackshot " + v;
                bar = Updater.Doing != Updater.Step.Installing;
                if (Updater.Doing != Updater.Step.Installing) secondaryLabel = "Ocultar";
            }
            else if (state == "error")
            {
                title = "No se pudo actualizar a la " + v;
                primaryLabel = Updater.CanInstall ? "Reintentar" : "Ver la versi\u00F3n";
                secondaryLabel = "Cerrar";
            }
            else if (Updater.IsReady && Updater.CanInstall)
            {
                title = "Stackshot " + v + " lista para instalar";
                primaryLabel = "Instalar ahora";
                secondaryLabel = "Ahora no";
            }
            else
            {
                title = "Stackshot " + v + " disponible";
                primaryLabel = Updater.CanInstall ? "Descargar e instalar" : "Ver la versi\u00F3n";
                secondaryLabel = "Ahora no";
            }
            double bottom = F(Inset) - F(1) + TitleText(TextWidth).Height + F(2) + BodyText(TextWidth).Height;
            if (bar) bottom += F(10) + F(4);
            bottom = Math.Max(F(Inset + IconSize), bottom);
            int h;
            primary = secondary = Rectangle.Empty;
            if (primaryLabel != null || secondaryLabel != null)
            {
                int by = (int)Math.Round(bottom + F(12)), bh = P(ButtonH), right = P(BodyW) - P(Inset);
                if (primaryLabel != null)
                {
                    int w = ButtonWidth(primaryLabel);
                    primary = new Rectangle(right - w, by, w, bh);
                    right -= w + P(8);
                }
                if (secondaryLabel != null)
                {
                    int w = ButtonWidth(secondaryLabel);
                    secondary = new Rectangle(right - w, by, w, bh);
                }
                h = by + bh + P(Inset);
            }
            else h = (int)Math.Ceiling(bottom + F(Inset));
            SetSize(new Size(P(BodyW), h));
        }

        void OnUpdater()
        {
            if (leaving || IsDisposed) return;
            Updater.Release r = Updater.Available;
            if (r == null || r.Tag != release.Tag) { Dismiss(); return; }
            string before = state;
            Size was = body;
            Arrange();
            if (body.Height != was.Height && Visible)
            {
                // Grows or shrinks upwards: the bottom stays put above the taskbar (any slide under way goes on).
                int dh = body.Height - was.Height;
                y -= dh;
                ty -= dh;
                ApplyPos();
            }
            Redraw();
            if (state != before)
            {
                armed = Environment.TickCount + 350;
                if (hot < 0) Linger(state == "error" ? 30000 : 25000);
            }
        }

        void Restyle()
        {
            if (IsDisposed) return;
            shadow = null;
            Redraw();
        }

        static MI.BitmapSource Logo()
        {
            if (logo != null) return logo;
            try
            {
                using (Image img = ShotStack.LoadResourceImage("logo.png"))
                using (Bitmap b = new Bitmap(img)) logo = Ink.FromGdi(b);
            }
            catch { }
            return logo;
        }

        protected override void PaintSurface(M.DrawingContext dc, int w, int h)
        {
            Palette pal = Ds.Brushes;
            W.Rect b = new W.Rect(Pad, Pad, body.Width, body.Height);
            double R = F(14);
            if (shadow == null || shadowFor != new Size(w, h))
            {
                W.Rect sb = b;
                sb.Offset(0, F(6));
                shadow = Ink.Shadow(w, h, sb, R, F(18), Ds.Argb(pal.Dark ? 0.55 : 0.2, 0, 0, 0));
                shadowFor = new Size(w, h);
            }
            dc.DrawImage(shadow, new W.Rect(0, 0, w, h));
            Ink.Round(dc, pal.Dark ? Ds.Rgb(42, 42, 45) : Ds.Rgb(251, 251, 252), b, R);
            Ink.Hairline(dc, pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.10, 0, 0, 0), b, R);
            dc.PushTransform(new M.TranslateTransform(Pad, Pad));
            MI.BitmapSource icon = Logo();
            if (icon != null) dc.DrawImage(icon, new W.Rect(F(Inset), F(Inset), F(IconSize), F(IconSize)));
            double tx = Math.Round(TextLeft), tw = TextWidth, ty = F(Inset) - F(1);
            M.FormattedText ft = TitleText(tw);
            dc.DrawText(ft, new W.Point(tx, Math.Round(ty)));
            ty += ft.Height + F(2);
            M.FormattedText bt = BodyText(tw);
            dc.DrawText(bt, new W.Point(tx, Math.Round(ty)));
            ty += bt.Height;
            if (bar)
            {
                W.Rect track = new W.Rect(tx, Math.Round(ty + F(10)), tw, Math.Round(F(4)));
                Ink.Round(dc, pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.08, 0, 0, 0), track, track.Height / 2);
                double f = Updater.Doing == Updater.Step.Downloading ? Math.Max(0.03, Updater.Fraction) : 1;
                Ink.Round(dc, pal.Accent, new W.Rect(track.X, track.Y, Math.Max(track.Height, track.Width * f), track.Height), track.Height / 2);
            }
            if (!primary.IsEmpty) PaintButton(dc, primary, primaryLabel, true, hot == 0);
            if (!secondary.IsEmpty) PaintButton(dc, secondary, secondaryLabel, false, hot == 1);
            dc.Pop();
        }

        // Like MacButton: the blue one with a soft inner glow, or a quiet neutral one.
        void PaintButton(M.DrawingContext dc, Rectangle r, string label, bool main, bool over)
        {
            Palette pal = Ds.Brushes;
            W.Rect rr = new W.Rect(r.X, r.Y, r.Width, r.Height);
            double rad = F(7);
            M.Color fg;
            if (main)
            {
                M.Color top = over ? Ds.Rgb(92, 170, 255) : Ds.Rgb(64, 150, 255), bottom = over ? Ds.Rgb(28, 120, 245) : pal.Accent;
                W.Rect sh = rr;
                sh.Offset(0, F(1.2));
                dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.24, 4, 24, 85)), null, sh, rad, rad);
                dc.DrawRoundedRectangle(new M.LinearGradientBrush(top, bottom, 90), null, rr, rad, rad);
                M.Pen glow = new M.Pen(new M.LinearGradientBrush(Ds.Argb(0.42, 255, 255, 255), Ds.Argb(0, 255, 255, 255), 90), 1);
                dc.DrawRoundedRectangle(null, glow, new W.Rect(rr.X + 0.5, rr.Y + 0.5, rr.Width - 1, rr.Height - 1), rad - 0.5, rad - 0.5);
                fg = M.Colors.White;
            }
            else
            {
                M.Color bg = pal.Dark ? (over ? Ds.Rgb(84, 84, 89) : Ds.Rgb(66, 66, 70)) : (over ? Ds.Rgb(242, 242, 245) : Ds.Rgb(255, 255, 255));
                if (!pal.Dark)
                {
                    W.Rect sh = rr;
                    sh.Offset(0, F(0.6));
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.10, 0, 0, 0)), null, sh, rad, rad);
                }
                M.Pen edge = new M.Pen(Ds.Brush(pal.Dark ? Ds.Argb(0.10, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0)), 1);
                dc.DrawRoundedRectangle(Ds.Brush(bg), edge, new W.Rect(rr.X + 0.5, rr.Y + 0.5, rr.Width - 1, rr.Height - 1), rad, rad);
                fg = pal.Label;
            }
            Ink.Center(dc, Ink.Px(label, Ds.Semibold, F(12.5), fg), rr);
        }

        int HitTest(Point p)
        {
            Point q = new Point(p.X - Pad, p.Y - Pad);
            if (primary.Contains(q)) return 0;
            if (secondary.Contains(q)) return 1;
            return new Rectangle(0, 0, body.Width, body.Height).Contains(q) ? 2 : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            life.Stop();
            int h = HitTest(e.Location);
            if (h == hot) return;
            hot = h;
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Redraw();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Redraw(); }
            Linger(8000);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || leaving || unchecked(Environment.TickCount - armed) < 0) return;
            int h = HitTest(e.Location);
            if (h == 0)
            {
                // Download and install here (the notice follows it), or the release page when it can't be installed.
                if (Updater.CanInstall) Updater.Install();
                else { Updater.OpenPage(); Dismiss(); }
            }
            else if (h == 1) Dismiss();
            else if (h == 2)
            {
                owner.ShowHome("about", false);
                Dismiss();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Updater.Changed -= onUpdater;
                Updater.Progress -= onProgress;
                Ds.Changed -= onTheme;
                life.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
