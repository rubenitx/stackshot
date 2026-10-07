// Stackshot - Secciones de la ventana principal y sus controles (interruptores, selectores, atajos, fichas).
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
    public partial class HomeWindow
    {
        static readonly string[] ActionNames = { "Capturar un \u00E1rea", "Capturar la pantalla", "Capturar la ventana activa", "Captura con desplazamiento", "Grabar v\u00EDdeo", "Grabar GIF" };
        static readonly string[] ActionShort = { "Un \u00E1rea", "Pantalla completa", "Una ventana", "Con desplazamiento", "V\u00EDdeo", "GIF animado" };
        static readonly string[] ActionIcons = { "area", "screen", "window", "scroll", "video", "gif" };
        static readonly Color[,] ActionColors =
        {
            { Color.FromArgb(64, 156, 255), Color.FromArgb(88, 86, 214) },
            { Color.FromArgb(90, 200, 250), Color.FromArgb(0, 122, 255) },
            { Color.FromArgb(191, 90, 242), Color.FromArgb(94, 92, 230) },
            { Color.FromArgb(52, 199, 89), Color.FromArgb(0, 168, 180) },
            { Color.FromArgb(255, 85, 120), Color.FromArgb(255, 59, 48) },
            { Color.FromArgb(255, 179, 64), Color.FromArgb(255, 70, 110) }
        };

        int X0 { get { return P(40); } }
        int CW { get { return ViewW - 2 * P(40); } }

        // ------------------------------------------------------------ Piezas comunes

        void Header(ref int y, string title, string sub)
        {
            y = P(46);
            TextW t = new TextW(title, 26, 2, Mac.Text);
            t.R = new Rectangle(X0, y, CW, P(36));
            items.Add(t);
            y += P(40);
            if (sub != null)
            {
                TextW d = new TextW(sub, 13, 0, Mac.Text2);
                d.Wrap = true;
                d.R = new Rectangle(X0, y, CW, MeasureH(sub, F(13, 0), CW));
                items.Add(d);
                y = d.R.Bottom;
            }
            y += P(22);
        }

        int MeasureH(string text, Font f, int w)
        {
            return TextRenderer.MeasureText(text, f, new Size(w, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + P(2);
        }

        // Grupo al estilo de los Ajustes de macOS: título pequeño encima y las filas dentro de una tarjeta.
        void Group(ref int y, string caption, params Row[] rows)
        {
            if (caption != null)
            {
                TextW c = new TextW(caption, 12.5f, 1, Mac.Text2);
                c.R = new Rectangle(X0 + P(6), y, CW, P(18));
                items.Add(c);
                y += P(24);
            }
            GroupW g = new GroupW();
            int top = y;
            foreach (Row r in rows)
            {
                if (r == null) continue;
                int h = r.Height(this);
                r.R = new Rectangle(X0, y, CW, h);
                r.Layout(this);
                g.Rows.Add(r);
                y += h;
            }
            g.R = new Rectangle(X0, top, CW, y - top);
            items.Add(g);
            foreach (Row r in g.Rows) items.Add(r);
            y += P(26);
        }

        void Note(ref int y, string text)
        {
            TextW n = new TextW(text, 12, 0, Mac.Text3);
            n.Wrap = true;
            n.R = new Rectangle(X0 + P(6), y - P(14), CW - P(12), MeasureH(text, F(12, 0), CW - P(12)));
            items.Add(n);
            y = n.R.Bottom + P(18);
        }

        void Changed()
        {
            owner.ApplySettings();
            contentDirty = true;
            sideDirty = true;
            Invalidate();
        }

        // Una acción desde Inicio: la ventana se esconde primero para no salir en la captura.
        void RunAction(string action)
        {
            if (action != "video" || !Recorder.Recording) Hide();
            owner.Run(action);
        }

        // ------------------------------------------------------------ Inicio

        void BuildHome()
        {
            int y = P(44);
            Hero hero = new Hero();
            hero.R = new Rectangle(X0, y, CW, P(218));
            items.Add(hero);
            heroMascot = new RectangleF(X0 + P(30), y + P(26), P(166), P(166));
            y = hero.R.Bottom + P(28);
            TextW t = new TextW("Capturar", 13, 1, Mac.Text2);
            t.R = new Rectangle(X0 + P(4), y, CW, P(20));
            items.Add(t);
            y += P(28);
            int gap = P(14), tw = (CW - 2 * gap) / 3, th = P(100);
            for (int i = 0; i < 6; i++)
            {
                Tile tile = new Tile(Settings.Actions[i], i);
                tile.R = new Rectangle(X0 + (i % 3) * (tw + gap), y + (i / 3) * (th + gap), tw, th);
                items.Add(tile);
            }
        }

        // ------------------------------------------------------------ Atajos

        void BuildKeys()
        {
            int y = 0;
            Header(ref y, "Atajos", "Haz clic en un atajo y pulsa la combinaci\u00F3n que quieras. Supr lo quita y Esc cancela.");
            Row[] rows = new Row[Settings.Actions.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = new HotkeyRow(Settings.Actions[i], i);
            Group(ref y, null, rows);
            Note(ref y, "Si alguno no responde, puede que lo est\u00E9 usando otro programa (Recortes, ShareX, Lightshot\u2026). Stackshot te avisar\u00E1 junto al reloj.");
            PillW reset = new PillW("Restaurar los de serie", false, delegate
            {
                Settings d = new Settings();
                foreach (string a in Settings.Actions) settings.SetHotkeys(a, d.HotkeysFor(a));
                Changed();
                owner.ResumeHotkeys();
                Build();
            });
            reset.R = new Rectangle(X0, y, PillWidth(reset.Label, F(13, 1)), P(34));
            items.Add(reset);
        }

        // ------------------------------------------------------------ General

        void BuildGeneral()
        {
            int y = 0;
            Header(ref y, "General", "C\u00F3mo arranca Stackshot y qu\u00E9 hace cada vez que capturas.");
            Group(ref y, "Arranque",
                new ToggleRow("Iniciar con Windows", "Arranca en segundo plano al encender el equipo.", "power", Mac.Green, Color.FromArgb(0, 160, 90),
                              delegate { return Installer.StartupEnabled; }, delegate(bool v) { Installer.SetStartup(v); }),
                new ToggleRow("Seguir en la bandeja al cerrar", "Al cerrar esta ventana, Stackshot sigue funcionando junto al reloj.", "stack", Mac.Blue, Mac.Indigo,
                              delegate { return settings.CloseToTray; }, delegate(bool v) { settings.CloseToTray = v; }),
                new ToggleRow("Animaci\u00F3n al abrir", "La bienvenida con el logo cuando abres Stackshot.", "sparkle", Mac.Purple, Mac.Indigo,
                              delegate { return settings.ShowIntro; }, delegate(bool v) { settings.ShowIntro = v; }));
            ButtonRow print = null;
            if (Installer.SnippingOwnsPrintScreen)
                print = new ButtonRow("Usar la tecla Impr Pant", "Ahora la usa Recortes de Windows. Se cambia solo en tu usuario.", "keyboard", Mac.Orange, Mac.Red, "Usar", delegate
                {
                    Installer.FreePrintScreen();
                    owner.ResumeHotkeys();
                    Build();
                    Invalidate();
                });
            Group(ref y, "Al capturar",
                new ToggleRow("Sonido de c\u00E1mara", "Un clic suave cada vez que capturas.", "sound", Mac.Pink, Mac.Red,
                              delegate { return settings.Sound; }, delegate(bool v) { settings.Sound = v; if (v) Shutter.Play(); }),
                new ToggleRow("Copiar cada captura", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla.", "copy", Mac.Teal, Mac.Blue,
                              delegate { return settings.CopyToClipboard; }, delegate(bool v) { settings.CopyToClipboard = v; }),
                new ToggleRow("Seguir al rat\u00F3n entre pantallas", "Con varias pantallas, las miniaturas van a la del rat\u00F3n.", "screen", Mac.Indigo, Mac.Purple,
                              delegate { return settings.FollowMouse; }, delegate(bool v) { settings.FollowMouse = v; }),
                print);
            Group(ref y, "Capturas guardadas", new FolderRow());
            Note(ref y, "Lo que no guardas se borra solo al cabo de una hora (nunca mientras tenga miniatura o est\u00E9 en el editor).");
        }

        // ------------------------------------------------------------ Grabación

        void BuildRecord()
        {
            int y = 0;
            Header(ref y, "Grabaci\u00F3n", "V\u00EDdeo en MP4 y GIF animado. Para parar, pulsa el atajo otra vez o el bot\u00F3n de la barrita roja.");
            int[] vf = { 24, 30, 60 }, gf = { 10, 15, 20, 25 };
            Group(ref y, "V\u00EDdeo",
                new SegRow("Fotogramas por segundo", "M\u00E1s fotogramas, m\u00E1s fluido (y m\u00E1s pesado).", "video", Mac.Pink, Mac.Red, new string[] { "24", "30", "60" },
                           delegate { return Nearest(vf, settings.VideoFps); }, delegate(int i) { settings.VideoFps = vf[i]; }));
            Group(ref y, "GIF",
                new SegRow("Fotogramas por segundo", "Los GIF pesan mucho: 15 suele ser el punto justo.", "gif", Mac.Orange, Mac.Pink, new string[] { "10", "15", "20", "25" },
                           delegate { return Nearest(gf, settings.GifFps); }, delegate(int i) { settings.GifFps = gf[i]; }));
            string ff = Recorder.FindFfmpeg(settings);
            Group(ref y, "Motor de v\u00EDdeo",
                new ButtonRow("FFmpeg", ff != null ? "Listo: " + ShortPath(ff) : "Se descarga solo (unos 80 MB) la primera vez que grabes.", "gear", Color.FromArgb(142, 142, 147), Color.FromArgb(99, 99, 104),
                              ff != null ? null : "Descargar ahora", delegate
                              {
                                  FfmpegSetup.Run(settings);
                                  Build();
                                  Invalidate();
                              }));
        }

        static int Nearest(int[] options, int v)
        {
            int best = 0;
            for (int i = 1; i < options.Length; i++) if (Math.Abs(options[i] - v) < Math.Abs(options[best] - v)) best = i;
            return best;
        }

        static string ShortPath(string p)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return p.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + p.Substring(home.Length) : p;
        }

        // ------------------------------------------------------------ Mascota

        void BuildMascot()
        {
            int y = 0;
            Header(ref y, "Mascota", "Tu compa\u00F1ero de capturas. Hazle clic para jugar; si le insistes mucho, se marea.");
            MascotStage stage = new MascotStage();
            stage.R = new Rectangle(X0, y, CW, P(220));
            items.Add(stage);
            heroMascot = new RectangleF(X0 + P(40), y + P(24), P(168), P(168));
            y = stage.R.Bottom + P(26);
            NameRow name = new NameRow();
            Group(ref y, null,
                name,
                new SwatchRow(),
                new ToggleRow("Mostrar la mascota", "En Inicio y abajo en la barra lateral.", "bot", Mac.Blue, Mac.Purple,
                              delegate { return settings.MascotOn; }, delegate(bool v) { settings.MascotOn = v; if (v) { mascot.PopIn(); mascot.Greet("\u00A1He vuelto!"); } }),
                new ToggleRow("Saludos y consejos", "De vez en cuando te cuenta trucos de Stackshot.", "sparkle", Mac.Orange, Mac.Pink,
                              delegate { return settings.MascotTalks; }, delegate(bool v) { settings.MascotTalks = v; if (!v) mascot.Bubble = null; }));
            name.Layout(this);
            nameRect = name.Field;
        }

        // ------------------------------------------------------------ Fondo y editor

        void BuildEditor()
        {
            int y = 0;
            Header(ref y, "Fondo y editor", "Como en CleanShot X: tus capturas, listas para presentar con un fondo bonito, margen y sombra. En el editor, bot\u00F3n Fondo (o la tecla B); tambi\u00E9n para v\u00EDdeos y GIF con \u00ABPresentar\u00BB.");
            BackdropPreview prev = new BackdropPreview();
            prev.R = new Rectangle(X0, y, CW, P(250));
            items.Add(prev);
            y = prev.R.Bottom + P(26);
            int[] pads = { 0, 25, 50, 85 }, radii = { 0, 40, 80 };
            string[] ratios = { "auto", "16:9", "4:3", "1:1" };
            Group(ref y, null,
                new PresetRow(),
                new ToggleRow("Abrir el editor con el fondo puesto", "Si no, se pone con el bot\u00F3n Fondo cuando quieras.", "photo", Mac.Purple, Mac.Pink,
                              delegate { return settings.BgAuto; }, delegate(bool v) { settings.BgAuto = v; }));
            Group(ref y, "Estilo",
                new SegRow("Margen", null, null, Color.Empty, Color.Empty, new string[] { "Ninguno", "Peque\u00F1o", "Medio", "Grande" },
                           delegate { return Nearest(pads, settings.BgPadding); }, delegate(int i) { settings.BgPadding = pads[i]; }),
                new SegRow("Esquinas", null, null, Color.Empty, Color.Empty, new string[] { "Rectas", "Suaves", "Redondas" },
                           delegate { return Nearest(radii, settings.BgRadius); }, delegate(int i) { settings.BgRadius = radii[i]; }),
                new ToggleRow("Sombra", null, null, Color.Empty, Color.Empty,
                              delegate { return settings.BgShadow; }, delegate(bool v) { settings.BgShadow = v; }),
                new SegRow("Proporci\u00F3n", null, null, Color.Empty, Color.Empty, new string[] { "Auto", "16:9", "4:3", "1:1" },
                           delegate { return Math.Max(0, Array.IndexOf(ratios, settings.BgRatio)); }, delegate(int i) { settings.BgRatio = ratios[i]; }));
            Note(ref y, "En el editor puedes afinar el margen y las esquinas con deslizadores; lo \u00FAltimo que uses se queda guardado aqu\u00ED.");
        }

        void PlaceNameBox()
        {
            if (page != "mascot" || nameRect.IsEmpty) { RemoveNameBox(); return; }
            if (nameBox == null)
            {
                nameBox = new TextBox();
                nameBox.BorderStyle = BorderStyle.None;
                nameBox.BackColor = Mac.Control;
                nameBox.ForeColor = Mac.Text;
                nameBox.MaxLength = 16;
                nameBox.Font = F(13.5f, 0);
                nameBox.Text = settings.MascotName;
                nameBox.TextChanged += delegate
                {
                    string v = nameBox.Text.Trim();
                    settings.MascotName = v.Length > 0 ? v : "Pixel";
                    settings.Save();
                    sideDirty = true;
                    contentDirty = true;
                    Invalidate();
                };
                Controls.Add(nameBox);
            }
            int pad = P(10);
            Rectangle r = new Rectangle(P(Side) + nameRect.X + pad, nameRect.Y - (int)Math.Round(scroll) + (nameRect.Height - nameBox.PreferredHeight) / 2,
                                        nameRect.Width - 2 * pad, nameBox.PreferredHeight);
            nameBox.Bounds = r;
            nameBox.Visible = r.Top > P(Bar) / 2 && r.Bottom < ViewH;
        }

        void RemoveNameBox()
        {
            if (nameBox == null) return;
            Controls.Remove(nameBox);
            nameBox.Dispose();
            nameBox = null;
        }

        // ------------------------------------------------------------ Acerca de

        void BuildAbout()
        {
            int y = P(70);
            AboutHead head = new AboutHead();
            head.R = new Rectangle(X0, y, CW, P(260));
            items.Add(head);
            y = head.R.Bottom + P(8);
            Font bf = F(13, 1);
            PillW gh = new PillW("Ver en GitHub", true, delegate { try { Process.Start(Program.RepoUrl); } catch { } });
            PillW data = new PillW("Carpeta de datos", false, delegate { try { Process.Start("explorer.exe", "\"" + Settings.DataDir + "\""); } catch { } });
            int w1 = PillWidth(gh.Label, bf), w2 = PillWidth(data.Label, bf), gap = P(12);
            int x = X0 + (CW - w1 - w2 - gap) / 2;
            gh.R = new Rectangle(x, y, w1, P(36));
            data.R = new Rectangle(x + w1 + gap, y, w2, P(36));
            items.Add(gh);
            items.Add(data);
            y += P(64);
            if (Installer.RunningInstalled)
            {
                LinkW un = new LinkW("Desinstalar Stackshot\u2026", Mac.Red, delegate
                {
                    try { Process.Start(Settings.InstalledExe, "--uninstall"); } catch { }
                });
                int lw = TextRenderer.MeasureText(un.Label, F(12.5f, 0)).Width + P(8);
                un.R = new Rectangle(X0 + (CW - lw) / 2, y, lw, P(22));
                items.Add(un);
            }
        }

        static int IndexAt(Rectangle[] rs, Point p)
        {
            for (int i = 0; i < rs.Length; i++) if (rs[i].Contains(p)) return i;
            return -1;
        }

        int PillWidth(string label, Font f)
        {
            return TextRenderer.MeasureText(label, f).Width + P(36);
        }

        // ================================================================= Controles

        public abstract class Widget
        {
            public Rectangle R;
            public bool Interactive = true;
            protected readonly Tween hotT = new Tween(0);
            protected readonly Tween downT = new Tween(0);
            public virtual bool Clickable { get { return Interactive; } }
            public virtual bool Running { get { return hotT.Running || downT.Running; } }
            public virtual void Step(double now) { hotT.Step(now); downT.Step(now); }
            public virtual void SetHot(bool on) { hotT.Go(on ? 1 : 0, on ? 110 : 220, 0, Ease.OutCubic, null); }
            public virtual bool Hit(Point p) { return R.Contains(p); }
            public virtual void Move(HomeWindow w, Point p) { }
            public virtual void Down(HomeWindow w, Point p) { downT.Go(1, 80, 0, Ease.OutCubic, null); }
            public virtual void Up(HomeWindow w) { downT.Go(0, 200, 0, Ease.OutCubic, null); }
            public virtual void Click(HomeWindow w, Point p) { }
            public abstract void Paint(Graphics g, HomeWindow w);
        }

        class TextW : Widget
        {
            readonly string text;
            readonly float px;
            readonly int weight;
            readonly Color color;
            public bool Wrap;
            public TextW(string text, float px, int weight, Color color) { this.text = text; this.px = px; this.weight = weight; this.color = color; Interactive = false; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                TextFormatFlags f = Wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                Txt(g, text, w.F(px, weight), R, color, f);
            }
        }

        class NavItem : Widget
        {
            readonly string id, name, icon;
            public NavItem(string id, string name, string icon) { this.id = id; this.name = name; this.icon = icon; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                bool on = w.page == id;
                double h = hotT.Value;
                if (on) Fill(g, R, w.P(8), Color.FromArgb(34, 255, 255, 255));
                else if (h > 0) Fill(g, R, w.P(8), Color.FromArgb((int)(16 * h), 255, 255, 255));
                Rectangle ir = new Rectangle(R.X + w.P(10), R.Y + (R.Height - w.P(18)) / 2, w.P(18), w.P(18));
                Icons.Draw(g, icon, ir, on ? Mac.Blue : Mac.Text2);
                Txt(g, name, w.F(13.5f, on ? 1 : 0), new Rectangle(R.X + w.P(38), R.Y, R.Width - w.P(40), R.Height), on ? Mac.Text : Mac.Mix(Mac.Text2, Mac.Text, h),
                     TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            public override void Click(HomeWindow w, Point p) { if (w.page != id) w.SetPage(id, true); }
        }

        // Abajo en la barra lateral: la mascota en pequeño con su nombre y cómo está Stackshot.
        class MiniMascot : Widget
        {
            public override bool Clickable { get { return true; } }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(14), Color.FromArgb(10, 255, 255, 255));
                using (GraphicsPath p = Theme.Round(R, w.P(14)))
                using (Pen pen = new Pen(Color.FromArgb(18, 255, 255, 255))) g.DrawPath(pen, p);
                bool big = w.page == "home" || w.page == "mascot";
                bool show = w.settings.MascotOn && !big;
                int tx = show ? R.X + w.P(92) : R.X + w.P(16);
                string title = show ? w.settings.MascotName : "Capturar un \u00E1rea";
                Txt(g, title, w.F(14, 1), new Rectangle(tx, R.Y + w.P(show ? 28 : 16), R.Right - tx - w.P(10), w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Color dot;
                string status = w.Status(out dot);
                if (show)
                {
                    using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, tx, R.Y + w.P(57), w.P(7), w.P(7));
                    Txt(g, status, w.F(11.5f, 0), new Rectangle(tx + w.P(12), R.Y + w.P(51), R.Right - tx - w.P(20), w.P(36)), Mac.Text2, TextFormatFlags.WordBreak);
                }
                else
                {
                    w.Keycaps(g, Hotkeys.Display(w.settings.HotRegion), R.Right - w.P(16), R.Y + w.P(52), false, Mac.Text, Mac.Control);
                    using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, tx, R.Y + w.P(80), w.P(7), w.P(7));
                    Txt(g, status, w.F(11.5f, 0), new Rectangle(tx + w.P(12), R.Y + w.P(74), R.Width - w.P(40), w.P(18)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }
            public override void Click(HomeWindow w, Point p) { if (w.page != "home") w.SetPage("home", true); }
        }

        // Cómo está Stackshot ahora mismo (para la barra lateral y la portada).
        string Status(out Color dot)
        {
            if (Recorder.Recording) { dot = Mac.Red; return "Grabando\u2026"; }
            if (ScrollCapture.Active) { dot = Mac.Blue; return "Capturando con desplazamiento"; }
            if (settings.MascotOn && mascot.Sleeping) { dot = Mac.Text3; return "Echando una siesta"; }
            dot = Mac.Green;
            return "Listo para capturar";
        }

        class GroupW : Widget
        {
            public readonly List<Row> Rows = new List<Row>();
            public GroupW() { Interactive = false; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(12), Mac.Card);
                using (GraphicsPath p = Theme.Round(R, w.P(12)))
                using (Pen pen = new Pen(Color.FromArgb(14, 255, 255, 255))) g.DrawPath(pen, p);
                using (Pen pen = new Pen(Mac.Separator))
                {
                    for (int i = 1; i < Rows.Count; i++)
                    {
                        int inset = Rows[i].Icon != null ? w.P(58) : w.P(16);
                        g.DrawLine(pen, R.X + inset, Rows[i].R.Y, R.Right, Rows[i].R.Y);
                    }
                }
            }
        }

        // Fila de ajuste: icono de color, título y explicación; el control va a la derecha.
        public abstract class Row : Widget
        {
            public string Title, Sub, Icon;
            public Color C1, C2;
            protected Row(string title, string sub, string icon, Color c1, Color c2) { Title = title; Sub = sub; Icon = icon; C1 = c1; C2 = c2; }
            public virtual int Height(HomeWindow w) { return w.P(Sub != null ? 60 : 50); }
            protected int ControlLeft;   // donde empieza el control (para no pisar el texto)
            // Coloca el control (y ControlLeft) antes de dibujar el texto.
            public virtual void Layout(HomeWindow w) { }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Layout(w);
                if (Clickable && hotT.Value > 0)
                {
                    Rectangle hr = Rectangle.Inflate(R, -w.P(4), -w.P(3));
                    Fill(g, hr, w.P(9), Color.FromArgb((int)(9 * hotT.Value), 255, 255, 255));
                }
                int x = R.X + w.P(16);
                if (Icon != null)
                {
                    w.IconTile(g, new Rectangle(x, R.Y + (R.Height - w.P(30)) / 2, w.P(30), w.P(30)), Icon, C1, C2);
                    x += w.P(42);
                }
                int right = ControlLeft > 0 ? ControlLeft - w.P(16) : R.Right - w.P(16);
                if (Sub == null)
                    Txt(g, Title, w.F(13.5f, 0), new Rectangle(x, R.Y, right - x, R.Height), Mac.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                else
                {
                    int cy = R.Y + R.Height / 2;
                    Txt(g, Title, w.F(13.5f, 0), new Rectangle(x, cy - w.P(20), right - x, w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    Txt(g, Sub, w.F(12, 0), new Rectangle(x, cy + w.P(1), right - x, w.P(18)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                PaintControl(g, w);
            }
            protected abstract void PaintControl(Graphics g, HomeWindow w);
        }

        // Interruptor verde como los de Apple: toda la fila lo cambia.
        class ToggleRow : Row
        {
            readonly Func<bool> get;
            readonly Action<bool> set;
            readonly Tween pos;
            public ToggleRow(string title, string sub, string icon, Color c1, Color c2, Func<bool> get, Action<bool> set) : base(title, sub, icon, c1, c2)
            {
                this.get = get;
                this.set = set;
                pos = new Tween(get() ? 1 : 0);
            }
            public override bool Running { get { return base.Running || pos.Running; } }
            public override void Step(double now) { base.Step(now); pos.Step(now); }
            Rectangle track;
            public override void Layout(HomeWindow w)
            {
                int tw = w.P(42), th = w.P(25);
                track = new Rectangle(R.Right - w.P(16) - tw, R.Y + (R.Height - th) / 2, tw, th);
                ControlLeft = track.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                int tw = track.Width, th = track.Height;
                double v = pos.Value;
                Fill(g, track, th / 2f, Mac.Mix(Color.FromArgb(72, 72, 76), Mac.Green, v));
                float d = th - w.P(4), kx = (float)(track.X + w.P(2) + (tw - w.P(4) - d) * v), ky = track.Y + w.P(2);
                float stretch = (float)(downT.Value * w.P(4));
                if (v > 0.5) kx -= stretch;
                RectangleF knob = new RectangleF(kx, ky, d + stretch, d);
                using (GraphicsPath p = Theme.Round(new RectangleF(knob.X, knob.Y + w.P(1), knob.Width, knob.Height), d / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillPath(b, p);
                using (GraphicsPath p = Theme.Round(knob, d / 2))
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
            }
            public override void Click(HomeWindow w, Point p)
            {
                bool v = !get();
                set(v);
                pos.Go(v ? 1 : 0, 260, 0, Ease.OutBack, null);
                w.Changed();
            }
        }

        // Control segmentado (varias opciones, una elegida) con la pastilla deslizándose.
        class SegRow : Row
        {
            readonly string[] options;
            readonly Func<int> get;
            readonly Action<int> set;
            readonly Tween sel;
            Rectangle box;
            int segW;
            public SegRow(string title, string sub, string icon, Color c1, Color c2, string[] options, Func<int> get, Action<int> set) : base(title, sub, icon, c1, c2)
            {
                this.options = options;
                this.get = get;
                this.set = set;
                sel = new Tween(get());
            }
            public override bool Running { get { return base.Running || sel.Running; } }
            public override void Step(double now) { base.Step(now); sel.Step(now); }
            public override void Layout(HomeWindow w)
            {
                segW = w.P(54);
                foreach (string o in options) segW = Math.Max(segW, TextRenderer.MeasureText(o, w.F(12.5f, 1)).Width + w.P(24));
                int h = w.P(30);
                box = new Rectangle(R.Right - w.P(16) - segW * options.Length - w.P(4), R.Y + (R.Height - h) / 2, segW * options.Length + w.P(4), h);
                ControlLeft = box.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                Fill(g, box, w.P(8), Color.FromArgb(28, 28, 31));
                Rectangle pill = new Rectangle((int)(box.X + w.P(2) + segW * sel.Value), box.Y + w.P(2), segW, box.Height - w.P(4));
                Rectangle ps = pill;
                ps.Offset(0, w.P(1));
                Fill(g, ps, w.P(7), Color.FromArgb(50, 0, 0, 0));
                Fill(g, pill, w.P(7), Color.FromArgb(92, 92, 98));
                int cur = get();
                for (int i = 0; i < options.Length; i++)
                {
                    Rectangle r = new Rectangle(box.X + w.P(2) + segW * i, box.Y, segW, box.Height);
                    Txt(g, options[i], w.F(12.5f, i == cur ? 1 : 0), r, i == cur ? Mac.Text : Mac.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            }
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return box.Contains(p); }
            public override void Click(HomeWindow w, Point p)
            {
                int i = Math.Max(0, Math.Min(options.Length - 1, (p.X - box.X - w.P(2)) / Math.Max(1, segW)));
                if (i == get()) return;
                set(i);
                sel.Go(i, 280, 0, Ease.OutBack, null);
                w.Changed();
            }
        }

        // Fila con un botón a la derecha (o sin él, si el texto es null).
        class ButtonRow : Row
        {
            readonly string label;
            readonly Action act;
            Rectangle btn;
            public ButtonRow(string title, string sub, string icon, Color c1, Color c2, string label, Action act) : base(title, sub, icon, c1, c2)
            {
                this.label = label;
                this.act = act;
                Interactive = label != null;
            }
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return btn.Contains(p); }
            public override void Layout(HomeWindow w)
            {
                if (label == null) return;
                int bw = w.PillWidth(label, w.F(12.5f, 1)) - w.P(8), bh = w.P(30);
                btn = new Rectangle(R.Right - w.P(16) - bw, R.Y + (R.Height - bh) / 2, bw, bh);
                ControlLeft = btn.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                if (label == null) return;
                Fill(g, btn, w.P(8), Mac.Mix(Color.FromArgb(72, 72, 78), Color.FromArgb(92, 92, 98), hotT.Value));
                Txt(g, label, w.F(12.5f, 1), btn, Mac.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            public override void Click(HomeWindow w, Point p) { if (act != null) act(); }
        }

        // Atajo de una acción: teclas dibujadas como teclas; al pincharlo, escucha la combinación nueva.
        class HotkeyRow : Row
        {
            readonly string action;
            Rectangle field;
            bool on, needsModifier;

            // Teclas que no se usan para escribir y pueden ser atajo por sí solas.
            static bool StandsAlone(Keys k)
            {
                return (k >= Keys.F1 && k <= Keys.F24) || k == Keys.PrintScreen || k == Keys.Pause || k == Keys.Scroll ||
                       k == Keys.Insert || k == Keys.Apps;
            }
            public HotkeyRow(string action, int i) : base(ActionNames[i], null, ActionIcons[i], ActionColors[i, 0], ActionColors[i, 1]) { this.action = action; }
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return field.Contains(p); }
            public override bool Running { get { return base.Running || on; } }
            public override void Layout(HomeWindow w)
            {
                int fw = w.P(250), fh = w.P(34);
                field = new Rectangle(R.Right - w.P(14) - fw, R.Y + (R.Height - fh) / 2, fw, fh);
                ControlLeft = field.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                Color bg = on ? Color.FromArgb(24, 40, 66) : Mac.Mix(Color.FromArgb(31, 31, 34), Color.FromArgb(44, 44, 48), hotT.Value);
                Fill(g, field, w.P(9), bg);
                using (GraphicsPath p = Theme.Round(field, w.P(9)))
                {
                    double pulse = 0.55 + 0.45 * Math.Sin(Anim.Now / 160.0);
                    using (Pen pen = new Pen(on ? Mac.Alpha(Mac.Blue, pulse) : Color.FromArgb(20, 255, 255, 255), on ? w.P(2) : 1)) g.DrawPath(pen, p);
                }
                if (on)
                {
                    if (needsModifier)
                        Txt(g, "A\u00F1ade Ctrl, Alt o May\u00FAs", w.F(12.5f, 1), field, Mac.Orange, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    else
                        Txt(g, "Pulsa la combinaci\u00F3n\u2026", w.F(12.5f, 1), field, Mac.Blue, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    return;
                }
                string v = w.settings.HotkeysFor(action);
                if (Hotkeys.Split(v).Count == 0)
                {
                    Txt(g, "Sin atajo", w.F(12.5f, 0), field, Mac.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    return;
                }
                w.Keycaps(g, Hotkeys.Display(v), field.Right - w.P(8), field.Y + field.Height / 2, false, Mac.Text, Color.FromArgb(66, 66, 72));
            }
            public override void Click(HomeWindow w, Point p)
            {
                if (on) { Stop(w); return; }
                if (w.listening != null) w.listening.Stop(w);
                on = true;
                w.listening = this;
                w.owner.SuspendHotkeys();
                w.contentDirty = true;
            }
            public void Stop(HomeWindow w)
            {
                on = false;
                needsModifier = false;
                if (w.listening == this) w.listening = null;
                w.owner.ResumeHotkeys();
                w.contentDirty = true;
                w.Invalidate();
            }
            // Igual que en la caja de atajos clásica: Supr lo quita y se conservan las combinaciones extra del fichero.
            public void Take(HomeWindow w, Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.Escape && (keyData & Keys.Modifiers) == 0) { Stop(w); return; }
                bool clear = (key == Keys.Delete || key == Keys.Back) && (keyData & Keys.Modifiers) == 0;
                // Una tecla normal sola (una letra, el espacio, Enter…) dejaría de funcionar en todo Windows.
                if (!clear && (keyData & Keys.Modifiers) == 0 && !StandsAlone(key))
                {
                    needsModifier = true;
                    w.contentDirty = true;
                    return;
                }
                string value = w.settings.HotkeysFor(action), rest = "";
                int comma = value.IndexOf(',');
                if (comma >= 0) rest = value.Substring(comma);
                string combo;
                if ((key == Keys.Delete || key == Keys.Back) && (keyData & Keys.Modifiers) == 0) { combo = ""; value = rest.TrimStart(',', ' '); }
                else { combo = Hotkeys.ToSetting(keyData); value = combo + rest; }
                // La misma combinación en otra acción: se la quita a esa (solo puede hacer una cosa).
                if (combo.Length > 0)
                {
                    foreach (string a in Settings.Actions)
                    {
                        if (a == action) continue;
                        List<string> others = Hotkeys.Split(w.settings.HotkeysFor(a));
                        if (others.RemoveAll(delegate(string o) { return string.Equals(o, combo, StringComparison.OrdinalIgnoreCase); }) > 0)
                            w.settings.SetHotkeys(a, string.Join(", ", others.ToArray()));
                    }
                }
                w.settings.SetHotkeys(action, value);
                w.settings.Save();
                Stop(w);
                w.sideDirty = true;
            }
        }

        class FolderRow : Row
        {
            Rectangle open, change;
            public FolderRow() : base("", null, "folder", Color.FromArgb(90, 200, 250), Mac.Blue) { }
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return open.Contains(p) || change.Contains(p); }
            public override int Height(HomeWindow w) { return w.P(60); }
            Point last;
            public override void Move(HomeWindow w, Point p)
            {
                if ((open.Contains(p) != open.Contains(last)) || (change.Contains(p) != change.Contains(last))) w.contentDirty = true;
                last = p;
            }
            public override void Layout(HomeWindow w)
            {
                Font f = w.F(12.5f, 1);
                int bh = w.P(30), y = R.Y + (R.Height - bh) / 2;
                int cw = w.PillWidth("Cambiar\u2026", f) - w.P(8), ow = w.PillWidth("Abrir", f) - w.P(8);
                change = new Rectangle(R.Right - w.P(16) - cw, y, cw, bh);
                open = new Rectangle(change.X - w.P(8) - ow, y, ow, bh);
                Title = ShortPath(w.settings.SaveFolder);
                Sub = "Donde van las capturas que guardas.";
                ControlLeft = open.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                Font f = w.F(12.5f, 1);
                bool h = hotT.Value > 0;
                Fill(g, open, w.P(8), h && open.Contains(last) ? Color.FromArgb(92, 92, 98) : Color.FromArgb(72, 72, 78));
                Fill(g, change, w.P(8), h && change.Contains(last) ? Color.FromArgb(92, 92, 98) : Color.FromArgb(72, 72, 78));
                Txt(g, "Abrir", f, open, Mac.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Txt(g, "Cambiar\u2026", f, change, Mac.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            public override void Click(HomeWindow w, Point p)
            {
                if (open.Contains(p)) { w.owner.OpenFolder(); return; }
                if (!change.Contains(p)) return;
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = "\u00BFD\u00F3nde quieres guardar las capturas que conserves?";
                    d.ShowNewFolderButton = true;
                    try { Directory.CreateDirectory(w.settings.SaveFolder); d.SelectedPath = w.settings.SaveFolder; } catch { }
                    if (d.ShowDialog(w) == DialogResult.OK) { w.settings.SaveFolder = d.SelectedPath; w.Changed(); }
                }
            }
        }

        class NameRow : Row
        {
            public Rectangle Field;
            public NameRow() : base("Nombre", null, "bot", Mac.Blue, Mac.Purple) { Interactive = false; }
            // Encima del campo se pone el cuadro de texto de verdad (HomeWindow.PlaceNameBox).
            public override void Layout(HomeWindow w)
            {
                int fw = w.P(220), fh = w.P(32);
                Field = new Rectangle(R.Right - w.P(16) - fw, R.Y + (R.Height - fh) / 2, fw, fh);
                ControlLeft = Field.X;
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                Fill(g, Field, w.P(8), Mac.Control);
            }
        }

        class SwatchRow : Row
        {
            Rectangle[] dots = new Rectangle[0];
            Point last;
            public SwatchRow() : base("Color", null, "sparkle", Mac.Pink, Mac.Orange) { }
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { foreach (Rectangle d in dots) if (d.Contains(p)) return true; return false; }
            public override void Move(HomeWindow w, Point p)
            {
                int was = IndexAt(dots, last), now = IndexAt(dots, p);
                last = p;
                if (was != now) { w.contentDirty = true; w.Invalidate(); } // solo si cambia la muestra de debajo
            }
            public override void Layout(HomeWindow w)
            {
                int n = Mascot.BodyNames.Length, d = w.P(22), gap = w.P(9);
                int x = R.Right - w.P(16) - n * d - (n - 1) * gap;
                ControlLeft = x;
                dots = new Rectangle[n];
                for (int i = 0; i < n; i++) dots[i] = new Rectangle(x + i * (d + gap), R.Y + (R.Height - d) / 2, d, d);
            }
            protected override void PaintControl(Graphics g, HomeWindow w)
            {
                for (int i = 0; i < dots.Length; i++)
                {
                    Rectangle r = dots[i];
                    bool sel = w.settings.MascotColor == i, h = hotT.Value > 0 && r.Contains(last);
                    if (sel || h)
                    {
                        Rectangle ring = Rectangle.Inflate(r, w.P(4), w.P(4));
                        using (Pen p = new Pen(sel ? Mac.Text : Color.FromArgb(90, 255, 255, 255), w.P(2))) g.DrawEllipse(p, ring);
                    }
                    using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(r, 1, 1), Mascot.Bodies[i, 0], Mascot.Bodies[i, 1], 55f)) g.FillEllipse(b, r);
                }
            }
            public override void Click(HomeWindow w, Point p)
            {
                for (int i = 0; i < dots.Length; i++)
                {
                    if (!dots[i].Contains(p)) continue;
                    w.settings.MascotColor = i;
                    w.mascot.Hue = i;
                    w.mascot.Celebrate(w.settings.MascotTalks ? "\u00A1" + Mascot.BodyNames[i] + "! Me queda bien, \u00BFa que s\u00ED?" : null);
                    w.Changed();
                    return;
                }
            }
        }

        // Botón suelto (pastilla). Accent = el principal, en azul.
        class PillW : Widget
        {
            public readonly string Label;
            readonly bool accent;
            readonly Action act;
            public PillW(string label, bool accent, Action act) { Label = label; this.accent = accent; this.act = act; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Color bg = accent ? Mac.Mix(Mac.Blue, Color.FromArgb(64, 156, 255), hotT.Value) : Mac.Mix(Color.FromArgb(58, 58, 62), Color.FromArgb(78, 78, 84), hotT.Value);
                if (downT.Value > 0) bg = Mac.Mix(bg, Color.Black, downT.Value * 0.15);
                Fill(g, R, R.Height / 2f, bg);
                Txt(g, Label, w.F(13, 1), R, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            public override void Click(HomeWindow w, Point p) { if (act != null) act(); }
        }

        class LinkW : Widget
        {
            public readonly string Label;
            readonly Color color;
            readonly Action act;
            public LinkW(string label, Color color, Action act) { Label = label; this.color = color; this.act = act; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Font f = w.F(12.5f, 0);
                Txt(g, Label, f, R, Mac.Mix(Mac.Alpha(color, 0.85), color, hotT.Value), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                if (hotT.Value > 0.5)
                {
                    int tw = TextRenderer.MeasureText(Label, f).Width, y = R.Bottom - w.P(2);
                    using (Pen p = new Pen(color)) g.DrawLine(p, R.X + (R.Width - tw) / 2, y, R.X + (R.Width + tw) / 2, y);
                }
            }
            public override void Click(HomeWindow w, Point p) { if (act != null) act(); }
        }

        // Portada de Inicio: la mascota a la izquierda (se dibuja aparte) y a la derecha cómo está Stackshot y el atajo.
        class Hero : Widget
        {
            public Hero() { Interactive = false; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(18), Mac.Card);
                using (GraphicsPath p = Theme.Round(R, w.P(18)))
                {
                    Region old = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    Intro.PaintGlow(g, R.X + w.P(110), R.Y + w.P(80), w.P(420), Color.FromArgb(70, Mac.Brand1));
                    Intro.PaintGlow(g, R.Right - w.P(60), R.Bottom - w.P(10), w.P(460), Color.FromArgb(55, Mac.Brand3));
                    Intro.PaintGlow(g, R.X + R.Width / 2, R.Y - w.P(40), w.P(380), Color.FromArgb(40, Mac.Brand2));
                    g.Clip = old;
                    old.Dispose();
                    using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255))) g.DrawPath(pen, p);
                }
                int colW = w.P(236), cx = R.Right - w.P(28) - colW;
                if (!w.settings.MascotOn)
                {
                    Txt(g, Greeting(), w.F(28, 2), new Rectangle(R.X + w.P(32), R.Y + w.P(56), cx - R.X - w.P(40), w.P(40)), Mac.Text, TextFormatFlags.SingleLine);
                    Txt(g, "Todo listo para capturar.", w.F(14, 0), new Rectangle(R.X + w.P(32), R.Y + w.P(102), cx - R.X - w.P(40), w.P(22)), Mac.Text2, TextFormatFlags.SingleLine);
                }
                Color dot;
                string status = w.Status(out dot);
                int y = R.Y + w.P(52);
                using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, R.Right - w.P(28) - w.P(8), y + w.P(6), w.P(8), w.P(8));
                Txt(g, status, w.F(13, 1), new Rectangle(cx, y, colW - w.P(16), w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.Right);
                y += w.P(52);
                string region = w.settings.HotkeysFor("region");
                if (Hotkeys.Split(region).Count > 0) w.Keycaps(g, Hotkeys.Display(region), R.Right - w.P(28), y, true, Mac.Text, Color.FromArgb(70, 70, 78));
                y += w.P(30);
                Txt(g, "para capturar un \u00E1rea", w.F(12.5f, 0), new Rectangle(cx, y, colW, w.P(20)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.Right);
                Txt(g, "o elige abajo qu\u00E9 quieres hacer", w.F(12.5f, 0), new Rectangle(cx, y + w.P(20), colW, w.P(20)), Mac.Text3, TextFormatFlags.SingleLine | TextFormatFlags.Right);
            }
        }

        // Ficha de Inicio: un tipo de captura con su icono, su nombre y su atajo. Se eleva un poco al pasar por encima.
        class Tile : Widget
        {
            readonly string action;
            readonly int index;
            public Tile(string action, int index) { this.action = action; this.index = index; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                double h = hotT.Value, d = downT.Value;
                Rectangle r = R;
                r.Offset(0, (int)Math.Round(-w.P(3) * h + w.P(2) * d));
                Rectangle sh = r;
                sh.Inflate(-w.P(4), 0);
                sh.Offset(0, (int)(w.P(4) + w.P(5) * h));
                Fill(g, sh, w.P(14), Color.FromArgb((int)(30 + 40 * h), 0, 0, 0));
                Fill(g, r, w.P(14), Mac.Mix(Mac.Card, Color.FromArgb(52, 52, 57), h));
                using (GraphicsPath p = Theme.Round(r, w.P(14)))
                using (Pen pen = new Pen(Color.FromArgb((int)(14 + 20 * h), 255, 255, 255))) g.DrawPath(pen, p);
                Rectangle ic = new Rectangle(r.X + w.P(16), r.Y + w.P(16), w.P(38), w.P(38));
                bool stop = action == "video" && Recorder.Recording;
                using (GraphicsPath p = Theme.Round(ic, w.P(11)))
                using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(ic, 1, 1), ActionColors[index, 0], ActionColors[index, 1], 60f)) g.FillPath(b, p);
                int m = w.P(9);
                Icons.Draw(g, stop ? "stop" : ActionIcons[index], Rectangle.Inflate(ic, -m, -m), Color.White);
                string title = stop ? "Detener grabaci\u00F3n" : ActionShort[index];
                Txt(g, title, w.F(14, 1), new Rectangle(r.X + w.P(16), r.Bottom - w.P(36), r.Width - w.P(24), w.P(22)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                string combo = w.settings.HotkeysFor(action);
                if (Hotkeys.Split(combo).Count > 0)
                {
                    Font small = w.F(11, 0); // en la caché de fuentes: nada que crear en cada repintado
                    {
                        string disp = Hotkeys.Display(combo).Replace(" + ", "+");
                        Size ts = TextRenderer.MeasureText(disp, small);
                        Rectangle kr = new Rectangle(r.Right - w.P(14) - ts.Width - w.P(14), r.Y + w.P(16), ts.Width + w.P(14), w.P(22));
                        Fill(g, kr, w.P(6), Color.FromArgb(28, 255, 255, 255));
                        Txt(g, disp, small, kr, Mac.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                }
            }
            public override void Click(HomeWindow w, Point p) { w.RunAction(action); }
        }

        // Escenario de la sección Mascota: la mascota en grande (se dibuja aparte) y su nombre.
        class MascotStage : Widget
        {
            public MascotStage() { Interactive = false; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(18), Mac.Card);
                using (GraphicsPath p = Theme.Round(R, w.P(18)))
                {
                    Region old = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    Color c = Mascot.Bodies[Math.Max(0, Math.Min(Mascot.Bodies.GetLength(0) - 1, w.settings.MascotColor)), 0];
                    Intro.PaintGlow(g, R.X + w.P(124), R.Y + R.Height / 2, w.P(420), Color.FromArgb(70, c));
                    g.Clip = old;
                    old.Dispose();
                    using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255))) g.DrawPath(pen, p);
                }
                int x = R.X + w.P(250);
                Txt(g, w.settings.MascotOn ? w.settings.MascotName : "Escondido", w.F(28, 2), new Rectangle(x, R.Y + w.P(58), R.Right - x - w.P(24), w.P(40)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                string sub = w.settings.MascotOn ? "Mira al rat\u00F3n, salta, saluda y se duerme si le ignoras un rato. Celebra cada captura contigo."
                                                  : "Activa \u00ABMostrar la mascota\u00BB para que vuelva.";
                Txt(g, sub, w.F(13, 0), new Rectangle(x, R.Y + w.P(106), R.Right - x - w.P(28), w.P(60)), Mac.Text2, TextFormatFlags.WordBreak);
            }
        }

        // Vista previa del fondo con una ventana de ejemplo (nunca una captura de verdad).
        class BackdropPreview : Widget
        {
            static Bitmap sample;
            static Bitmap composed;      // compartida entre visitas a la sección: no se queda una por visita
            static string composedFor;
            public BackdropPreview() { Interactive = false; }

            static Bitmap Sample()
            {
                if (sample != null) return sample;
                sample = new Bitmap(560, 340, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(sample))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(30, 30, 36));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(42, 42, 50))) g.FillRectangle(b, 0, 0, 560, 34);
                    Color[] dots = { Color.FromArgb(255, 95, 87), Color.FromArgb(254, 188, 46), Color.FromArgb(40, 200, 64) };
                    for (int i = 0; i < 3; i++) using (SolidBrush b = new SolidBrush(dots[i])) g.FillEllipse(b, 14 + i * 20, 11, 12, 12);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 46))) g.FillRectangle(b, 0, 34, 130, 306);
                    for (int i = 0; i < 6; i++)
                        using (GraphicsPath p = Theme.Round(new RectangleF(16, 56 + i * 30, 70 + (i * 23) % 30, 10), 5))
                        using (SolidBrush b = new SolidBrush(i == 1 ? Color.FromArgb(10, 132, 255) : Color.FromArgb(70, 70, 82))) g.FillPath(b, p);
                    using (GraphicsPath p = Theme.Round(new RectangleF(152, 56, 220, 18), 6))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 222, 235))) g.FillPath(b, p);
                    for (int i = 0; i < 4; i++)
                        using (GraphicsPath p = Theme.Round(new RectangleF(152, 92 + i * 22, 360 - (i * 57) % 120, 9), 4))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(84, 84, 98))) g.FillPath(b, p);
                    int[] bars = { 60, 95, 72, 130, 110, 150, 124 };
                    for (int i = 0; i < bars.Length; i++)
                    {
                        RectangleF br = new RectangleF(156 + i * 52, 310 - bars[i], 34, bars[i]);
                        using (GraphicsPath p = Theme.Round(br, 6))
                        using (LinearGradientBrush lb = new LinearGradientBrush(RectangleF.Inflate(br, 1, 1), Mac.Brand1, Mac.Brand3, 90f)) g.FillPath(lb, p);
                    }
                }
                return sample;
            }

            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(18), Color.FromArgb(24, 24, 27));
                using (GraphicsPath p = Theme.Round(R, w.P(18)))
                using (Pen pen = new Pen(Color.FromArgb(18, 255, 255, 255))) g.DrawPath(pen, p);
                Settings st = w.settings;
                string key = st.BgPreset + "|" + st.BgPadding + "|" + st.BgRadius + "|" + st.BgShadow + "|" + st.BgRatio;
                if (composed == null || composedFor != key)
                {
                    if (composed != null) composed.Dispose();
                    try { composed = Backdrop.Compose(Sample(), st); }
                    catch (Exception ex) { ShotStack.Log("Vista previa del fondo: " + ex.Message); composed = null; }
                    composedFor = key;
                }
                if (composed == null) return;
                Rectangle area = Rectangle.Inflate(R, -w.P(20), -w.P(18));
                double k = Math.Min(area.Width / (double)composed.Width, area.Height / (double)composed.Height);
                int dw = (int)(composed.Width * k), dh = (int)(composed.Height * k);
                Rectangle dst = new Rectangle(area.X + (area.Width - dw) / 2, area.Y + (area.Height - dh) / 2, dw, dh);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(composed, dst);
            }
        }

        // Rejilla con todos los fondos; el elegido lleva un aro.
        class PresetRow : Row
        {
            Rectangle[] cells = new Rectangle[0];
            Point last;
            public PresetRow() : base("Fondo", "", "photo", Mac.Brand1, Mac.Brand3) { }
            public override bool Clickable { get { return false; } }
            int Cols(HomeWindow w) { return Math.Max(1, (w.CW - w.P(32) + w.P(10)) / (w.P(74) + w.P(10))); }
            public override int Height(HomeWindow w)
            {
                int rows = (Backdrop.Count + Cols(w) - 1) / Cols(w);
                return w.P(64) + rows * w.P(56) + w.P(8);
            }
            public override void Layout(HomeWindow w)
            {
                int cols = Cols(w), cw = w.P(74), ch = w.P(46), gap = w.P(10);
                cells = new Rectangle[Backdrop.Count];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new Rectangle(R.X + w.P(16) + (i % cols) * (cw + gap), R.Y + w.P(60) + (i / cols) * w.P(56), cw, ch);
                ControlLeft = 0;
            }
            public override bool Hit(Point p) { foreach (Rectangle c in cells) if (c.Contains(p)) return true; return false; }
            public override void Move(HomeWindow w, Point p)
            {
                int was = IndexAt(cells, last), now = IndexAt(cells, p);
                last = p;
                if (was != now) { w.contentDirty = true; w.Invalidate(); }
            }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Layout(w);
                int hov = -1;
                for (int i = 0; i < cells.Length; i++) if (hotT.Value > 0 && cells[i].Contains(last)) hov = i;
                Sub = Backdrop.Name(hov >= 0 ? hov : Math.Max(0, Math.Min(Backdrop.Count - 1, w.settings.BgPreset)));
                Rectangle head = new Rectangle(R.X, R.Y, R.Width, w.P(56));
                int x = R.X + w.P(16);
                w.IconTile(g, new Rectangle(x, head.Y + (head.Height - w.P(30)) / 2, w.P(30), w.P(30)), Icon, C1, C2);
                x += w.P(42);
                int cy = head.Y + head.Height / 2;
                Txt(g, Title, w.F(13.5f, 0), new Rectangle(x, cy - w.P(20), R.Right - x - w.P(16), w.P(20)), Mac.Text, TextFormatFlags.SingleLine);
                Txt(g, Sub, w.F(12, 0), new Rectangle(x, cy + w.P(1), R.Right - x - w.P(16), w.P(18)), Mac.Text2, TextFormatFlags.SingleLine);
                for (int i = 0; i < cells.Length; i++)
                {
                    Rectangle c = cells[i];
                    bool sel = w.settings.BgPreset == i;
                    if (sel || i == hov)
                    {
                        Rectangle ring = Rectangle.Inflate(c, w.P(3), w.P(3));
                        using (GraphicsPath p = Theme.Round(ring, w.P(11)))
                        using (Pen pen = new Pen(sel ? Mac.Blue : Color.FromArgb(90, 255, 255, 255), w.P(2))) g.DrawPath(pen, p);
                    }
                    using (GraphicsPath p = Theme.Round(c, w.P(8)))
                    {
                        Region old = g.Clip;
                        g.SetClip(p, CombineMode.Intersect);
                        try { Backdrop.PaintSwatch(g, c, i); }
                        catch { }
                        g.Clip = old;
                        old.Dispose();
                        using (Pen pen = new Pen(Color.FromArgb(28, 255, 255, 255))) g.DrawPath(pen, p);
                    }
                }
            }
            protected override void PaintControl(Graphics g, HomeWindow w) { }
            public override void Click(HomeWindow w, Point p)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!cells[i].Contains(p)) continue;
                    w.settings.BgPreset = i;
                    w.Changed();
                    return;
                }
            }
        }

        class AboutHead : Widget
        {
            public AboutHead() { Interactive = false; }
            public override void Paint(Graphics g, HomeWindow w)
            {
                int d = w.P(116);
                Rectangle lr = new Rectangle(R.X + (R.Width - d) / 2, R.Y, d, d);
                Intro.PaintGlow(g, lr.X + d / 2, lr.Y + d / 2, d * 2.6f, Color.FromArgb(60, Mac.Brand2));
                if (w.logo != null) g.DrawImage(w.logo, lr);
                Txt(g, "Stackshot", w.F(30, 2), new Rectangle(R.X, lr.Bottom + w.P(16), R.Width, w.P(40)), Mac.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
                Txt(g, "Versi\u00F3n " + Installer.MyVersion.ToString(3) + "  \u00B7  Libre y gratuito (licencia MIT)", w.F(12.5f, 0), new Rectangle(R.X, lr.Bottom + w.P(60), R.Width, w.P(20)), Mac.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
                Txt(g, "Capturas de pantalla preciosas para Windows: miniaturas flotantes, editor r\u00E1pido, v\u00EDdeo y GIF.", w.F(13, 0), new Rectangle(R.X + w.P(60), lr.Bottom + w.P(92), R.Width - w.P(120), w.P(40)), Mac.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            }
        }
    }
}
