// Stackshot - Main window sections and their controls (toggles, segmented controls, hotkeys, tiles).
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

        // macOS Settings-style group: small caption above, rows inside a card.
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

        // Actions from Home hide the window first so it doesn't appear in the capture.
        void RunAction(string action)
        {
            if (action != "video" || !Recorder.Recording) Hide();
            owner.Run(action);
        }

        void BuildHome()
        {
            int y = P(44);
            Hero hero = new Hero();
            hero.R = new Rectangle(X0, y, CW, P(266));
            items.Add(hero);
            heroMascot = new RectangleF(X0 + P(30), y + P(84), P(166), P(166));
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

        // Presets for a few settings that weigh on performance; everything else stays as the user left it.
        void ApplyProfile(int p)
        {
            settings.Profile = p;
            settings.ShowIntro = p > 0;
            settings.MascotTalks = p > 0;
            settings.MascotDesktop = p == 2;
            settings.VideoQuality = p == 2 ? 2 : 0;
            if (p == 0) { settings.VideoFps = 30; settings.Webcam = 0; }
            owner.MascotChanged();
            owner.ApplySettings();
        }

        void AddBackground()
        {
            if (Backdrop.CustomCount >= Backdrop.MaxCustom)
            {
                MessageBox.Show(this, "Ya tienes " + Backdrop.MaxCustom + " fondos propios. Quita uno para a\u00F1adir otro.", "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Elige una imagen para usar de fondo";
                d.Filter = "Im\u00E1genes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int i = Backdrop.AddCustom(d.FileName);
                if (i < 0)
                {
                    MessageBox.Show(this, "No se ha podido abrir esa imagen.", "Stackshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                settings.BgPreset = i;
                settings.Save();
                Rebuild();
            }
        }

        void BuildGeneral()
        {
            int y = 0;
            Header(ref y, "General", "C\u00F3mo arranca Stackshot y qu\u00E9 hace cada vez que capturas.");
            string[] pdesc =
            {
                "Lo m\u00EDnimo: sin animaci\u00F3n al abrir, mascota quieta en la ventana y v\u00EDdeo ligero. Ideal para equipos justos.",
                "El equilibrio de siempre: animaciones suaves y la mascota solo en la ventana.",
                "Todo activado: mascota paseando por el escritorio, saludos y v\u00EDdeo en calidad alta."
            };
            Group(ref y, "Perfil",
                new SegRow("Perfil r\u00E1pido", pdesc[Math.Max(0, Math.Min(2, settings.Profile))], "gear", Mac.Blue, Mac.Indigo,
                           new string[] { "Rendimiento", "Equilibrado", "Completo" },
                           delegate { return settings.Profile; }, delegate(int i) { ApplyProfile(i); Rebuild(); }));
            Note(ref y, "Un perfil solo cambia unos pocos ajustes de golpe; despu\u00E9s puedes retocar cualquiera.");
            Group(ref y, "Arranque",
                new ToggleRow("Iniciar con Windows", "Arranca en segundo plano al encender el equipo.", "power", Mac.Green, Color.FromArgb(0, 160, 90),
                              delegate { return Installer.StartupEnabled; }, delegate(bool v) { Installer.SetStartup(v); }),
                new ToggleRow("Seguir en la bandeja al cerrar", "Al cerrar esta ventana, Stackshot sigue funcionando junto al reloj.", "stack", Mac.Blue, Mac.Indigo,
                              delegate { return settings.CloseToTray; }, delegate(bool v) { settings.CloseToTray = v; }),
                new ToggleRow("Animaci\u00F3n al abrir", "La bienvenida con el logo cuando abres Stackshot.", "sparkle", Mac.Purple, Mac.Indigo,
                              delegate { return settings.ShowIntro; }, delegate(bool v) { settings.ShowIntro = v; }),
                new ToggleRow("Buscar actualizaciones", "Una vez al d\u00EDa pregunta a GitHub si hay versi\u00F3n nueva. No env\u00EDa nada tuyo.", "open", Mac.Teal, Mac.Blue,
                              delegate { return settings.CheckUpdates; }, delegate(bool v) { settings.CheckUpdates = v; }));
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

        void BuildRecord()
        {
            int y = 0;
            Header(ref y, "Grabaci\u00F3n", "V\u00EDdeo en MP4 y GIF animado. Para parar, pulsa el atajo otra vez o el bot\u00F3n de la barrita roja.");
            int[] vf = { 24, 30, 60 }, gf = { 10, 15, 20, 25 }, qv = { 0, 2, 1 }, qs = { 0, 2, 1 };
            Group(ref y, "V\u00EDdeo",
                // Shown in quality order; stored values keep 1 = M\u00E1xima from earlier versions.
                new SegRow("Calidad", settings.VideoQuality == 1 ? "60 fps sin p\u00E9rdida al grabar; texto n\u00EDtido como en pantalla. Tarda m\u00E1s en guardar."
                                    : settings.VideoQuality == 2 ? "60 fps con mucho detalle y se guarda al momento. Archivos m\u00E1s grandes."
                                                                 : "Ligera y fluida, perfecta para chats y correos.",
                           "sparkle", Mac.Purple, Mac.Indigo, new string[] { "Est\u00E1ndar", "Alta", "M\u00E1xima" },
                           delegate { return qv[Math.Max(0, Math.Min(2, settings.VideoQuality))]; }, delegate(int i) { settings.VideoQuality = qs[i]; Rebuild(); }),
                settings.VideoQuality != 0 ? null :
                new SegRow("Fotogramas por segundo", "M\u00E1s fotogramas, m\u00E1s fluido (y m\u00E1s pesado).", "video", Mac.Pink, Mac.Red, new string[] { "24", "30", "60" },
                           delegate { return Nearest(vf, settings.VideoFps); }, delegate(int i) { settings.VideoFps = vf[i]; }),
                new SegRow("C\u00E1mara", settings.Webcam == 0 ? "A\u00F1ade tu cara en una burbuja, como en Loom." :
                                       "Sale en una esquina de la zona grabada. Arr\u00E1strala; doble clic cambia la forma y clic derecho el tama\u00F1o.",
                           "camera", Mac.Green, Mac.Teal, new string[] { "No", "Redonda", "Cuadrada" },
                           delegate { return settings.Webcam; }, delegate(int i) { settings.Webcam = i; Rebuild(); }));
            Note(ref y, "Despu\u00E9s de grabar, \u00ABEditar\u00BB en la miniatura abre el editor de v\u00EDdeo: recorta el principio y el final, cambia la velocidad, el tama\u00F1o o el formato, a\u00F1ade marcas y ponle fondo.");
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

        void BuildMascot()
        {
            int y = 0;
            Header(ref y, "Mascota", "Tu compa\u00F1ero de capturas. Elige personaje, colores, gorro y ropa: todo se aplica al momento.");
            MascotStage stage = new MascotStage();
            stage.R = new Rectangle(X0, y, CW, P(276));
            items.Add(stage);
            heroMascot = new RectangleF(X0 + P(44), y + P(82), P(166), P(166));
            y = stage.R.Bottom + P(26);
            Group(ref y, "Estilos r\u00E1pidos", new StyleRow("Estilos", "Un clic y listo; despu\u00E9s puedes retocar cada pieza.", "sparkle", Mac.Pink, Mac.Purple,
                MascotParts.StyleNames, MascotParts.ApplyStyle, "style", true));
            Group(ref y, "Personajes", new StyleRow("Homenajes fan", "Anime, series, juegos y m\u00e1s. Pasa el rat\u00f3n para ver de d\u00f3nde sale cada uno.", "sparkle", Mac.Orange, Mac.Red,
                MascotParts.AnimeNames, MascotParts.ApplyAnime, "anime", false) { Hints = MascotParts.AnimeInspiration });
            NameRow name = new NameRow();
            Group(ref y, "Personaje",
                name,
                new PickerRow("Especie", "bot", Mac.Blue, Mac.Purple, MascotParts.Kinds,
                              delegate(MascotLook l) { return l.Kind; }, delegate(MascotLook l, int i) { l.Kind = i; }, null),
                new PickerRow("Color", "brush", Mac.Pink, Mac.Orange, MascotParts.ColorNames,
                              delegate(MascotLook l) { return l.Color; }, delegate(MascotLook l, int i) { l.Color = i; }, MascotParts.ColorUnlock),
                new SegRow("Ojos", null, null, Color.Empty, Color.Empty, MascotParts.EyeNames,
                           delegate { return settings.MascotEyes; },
                           delegate(int i) { MascotLook l = MascotLook.From(settings); l.Eyes = i; LookChanged(l, null); }));
            Group(ref y, "Armario",
                new PickerRow("Gorro", "sparkle", Mac.Purple, Mac.Indigo, MascotParts.HatNames,
                              delegate(MascotLook l) { return l.Hat; }, delegate(MascotLook l, int i) { l.Hat = i; }, MascotParts.HatUnlock),
                new PickerRow("Ropa", "stack", Mac.Red, Mac.Orange, MascotParts.OutfitNames,
                              delegate(MascotLook l) { return l.Outfit; }, delegate(MascotLook l, int i) { l.Outfit = i; }, null),
                new PickerRow("Complementos", "photo", Mac.Teal, Mac.Blue, MascotParts.FaceNames,
                              delegate(MascotLook l) { return l.Face; }, delegate(MascotLook l, int i) { l.Face = i; }, null),
                new ToggleRow("Disfraz de temporada", "Sin gorro, se pone la calabaza en Halloween y el de Pap\u00E1 Noel en Navidad.", "sparkle", Mac.Orange, Mac.Red,
                              delegate { return settings.MascotSeasonal; },
                              delegate(bool v) { MascotLook l = MascotLook.From(settings); l.Seasonal = v; LookChanged(l, null); }));
            Group(ref y, "Car\u00E1cter",
                new SegRow("Personalidad", "Lo que dice y c\u00F3mo se mueve.", "bot", Mac.Green, Color.FromArgb(0, 160, 90), MascotParts.Personalities,
                           delegate { return settings.MascotPersonality; },
                           delegate(int i) { MascotLook l = MascotLook.From(settings); l.Personality = i; LookChanged(l, null); mascot.Say(MascotTalk.Hello(settings), 3000); }),
                new ToggleRow("Mostrar la mascota", "En Inicio y abajo en la barra lateral.", "bot", Mac.Blue, Mac.Purple,
                              delegate { return settings.MascotOn; }, delegate(bool v) { settings.MascotOn = v; if (v) { mascot.PopIn(); mascot.Greet("\u00A1He vuelto!"); } owner.MascotChanged(); }),
                new ToggleRow("Saludos y consejos", "De vez en cuando te cuenta trucos de Stackshot.", "sparkle", Mac.Orange, Mac.Pink,
                              delegate { return settings.MascotTalks; }, delegate(bool v) { settings.MascotTalks = v; if (!v) mascot.Bubble = null; }));
            Group(ref y, "En el escritorio",
                new ToggleRow("Mascota en el escritorio", "Pasea junto a la barra de tareas, te mira, juega y se echa la siesta.", "screen", Mac.Indigo, Mac.Purple,
                              delegate { return settings.MascotDesktop; }, delegate(bool v) { settings.MascotDesktop = v; owner.MascotChanged(); }),
                new ToggleRow("Sube a las ventanas", "De vez en cuando salta a la ventana que tienes delante y pasea por encima. Si la mueves, viaja con ella.", "window", Mac.Teal, Mac.Blue,
                              delegate { return settings.MascotClimb; }, delegate(bool v) { settings.MascotClimb = v; owner.MascotChanged(); }));
            Note(ref y, "En el escritorio consume algo m\u00E1s: unos 10 MB de memoria y en torno al 1 % de CPU mientras se mueve (casi nada cuando duerme). " +
                        "Se esconde sola con juegos o presentaciones a pantalla completa y nunca sale en tus capturas ni al compartir pantalla. " +
                        "Un clic abre su men\u00FA y varios seguidos le hacen cosquillas; puedes arrastrarla a donde quieras.");
            name.Layout(this);
            nameRect = name.Field;
        }

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
                new ButtonRow("Tus fondos", Backdrop.IsCustom(settings.BgPreset) ? "Est\u00E1s usando una imagen tuya. Puedes a\u00F1adir m\u00E1s o quitar esta."
                                                                                  : "A\u00F1ade una foto o imagen tuya y \u00FAsala como fondo de tus capturas.",
                              "photo", Mac.Teal, Mac.Blue, "A\u00F1adir imagen\u2026", AddBackground),
                Backdrop.IsCustom(settings.BgPreset)
                    ? new ButtonRow("Quitar este fondo", "Se borra la copia guardada en Stackshot; tu imagen original no se toca.", "close", Mac.Red, Mac.Orange, "Quitar",
                                    delegate { int idx = settings.BgPreset; if (!Backdrop.IsCustom(idx)) return; Backdrop.RemoveCustom(idx); settings.BgPreset = 0; settings.Save(); Rebuild(); })
                    : null,
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

        void BuildAbout()
        {
            int y = P(70);
            AboutHead head = new AboutHead();
            head.R = new Rectangle(X0, y, CW, P(260));
            items.Add(head);
            y = head.R.Bottom + P(8);
            Font bf = F(13, 1);
            PillW gh = new PillW("Ver en GitHub", true, delegate { try { Process.Start(Program.RepoUrl); } catch { } });
            PillW data = new PillW("Carpeta de datos", false, delegate { try { Process.Start(Native.Explorer, "\"" + Settings.DataDir + "\""); } catch { } });
            int w1 = PillWidth(gh.Label, bf), w2 = PillWidth(data.Label, bf), gap = P(12);
            int x = X0 + (CW - w1 - w2 - gap) / 2;
            gh.R = new Rectangle(x, y, w1, P(36));
            data.R = new Rectangle(x + w1 + gap, y, w2, P(36));
            items.Add(gh);
            items.Add(data);
            y += P(60);
            UpdateCard card = new UpdateCard();
            card.R = new Rectangle(X0, y, CW, card.HeightFor(this));
            items.Add(card);
            y = card.R.Bottom + P(22);
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

        static string updateStatus;

        // Updates: what's new and a one-click update (or, if IT manages the install, a link to the release).
        class UpdateCard : Widget
        {
            Rectangle btn;
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return !Updater.Busy && btn.Contains(p); }

            public int HeightFor(HomeWindow w)
            {
                int notes = Updater.Available != null ? Updater.Available.Notes.Count : 0;
                return w.P(92) + notes * w.P(20) + (updateStatus != null ? w.P(22) : 0);
            }

            string Label
            {
                get
                {
                    if (Updater.Available == null) return Updater.Busy ? "Buscando\u2026" : "Buscar ahora";
                    if (Updater.Busy) return "Un momento\u2026";
                    return Updater.CanInstall ? "Actualizar ahora" : "Ver la versi\u00F3n";
                }
            }

            public override void Paint(Graphics g, HomeWindow w)
            {
                Fill(g, R, w.P(14), Mac.Card);
                using (GraphicsPath p = Theme.Round(R, w.P(14)))
                using (Pen pen = new Pen(Updater.Available != null ? Mac.Alpha(Mac.Blue, 0.6) : Color.FromArgb(18, 255, 255, 255))) g.DrawPath(pen, p);
                Updater.Release r = Updater.Available;
                int x = R.X + w.P(18), top = R.Y + w.P(18);
                w.IconTile(g, new Rectangle(x, top, w.P(34), w.P(34)), r != null ? "sparkle" : "open", r != null ? Mac.Blue : Color.FromArgb(142, 142, 147), r != null ? Mac.Purple : Color.FromArgb(99, 99, 104));
                int tx = x + w.P(48);
                Font bf = w.F(13, 1);
                int bw = w.PillWidth(Label, bf), bh = w.P(32);
                btn = new Rectangle(R.Right - w.P(18) - bw, top + w.P(2), bw, bh);
                string title = r != null ? "Stackshot " + r.Version.ToString(3) + " disponible"
                             : string.IsNullOrEmpty(owner(w).Settings.LastUpdateCheck) ? "Actualizaciones" : "Est\u00E1s al d\u00EDa";
                string sub = r != null ? (Installer.ManagedByMsi ? "Lo gestiona inform\u00E1tica: instala el nuevo Stackshot.msi." : "Se descarga, se verifica su firma y se instala sola.")
                                       : "Versi\u00F3n " + Installer.MyVersion.ToString(3) + (owner(w).Settings.CheckUpdates ? "  \u00B7  se comprueba una vez al d\u00EDa" : "  \u00B7  b\u00FAsqueda autom\u00E1tica desactivada");
                Txt(g, title, w.F(14, 1), new Rectangle(tx, top - w.P(1), btn.X - tx - w.P(12), w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Txt(g, sub, w.F(12, 0), new Rectangle(tx, top + w.P(19), btn.X - tx - w.P(12), w.P(18)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                int y = top + w.P(48);
                if (r != null)
                    foreach (string n in r.Notes)
                    {
                        Txt(g, "\u2022  " + n, w.F(12, 0), new Rectangle(tx, y, R.Right - tx - w.P(18), w.P(18)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                        y += w.P(20);
                    }
                if (updateStatus != null)
                    Txt(g, updateStatus, w.F(12, 0), new Rectangle(tx, y + w.P(2), R.Right - tx - w.P(18), w.P(18)), Mac.Orange, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Color bg = r != null && !Updater.Busy ? Mac.Mix(Mac.Blue, Color.FromArgb(64, 156, 255), hotT.Value) : Mac.Mix(Color.FromArgb(58, 58, 62), Color.FromArgb(78, 78, 84), hotT.Value);
                Fill(g, btn, bh / 2f, bg);
                Txt(g, Label, bf, btn, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            static ShotStack owner(HomeWindow w) { return w.owner; }

            public override void Click(HomeWindow w, Point p)
            {
                updateStatus = null;
                Action<string> status = delegate(string t) { updateStatus = t; w.Rebuild(); };
                if (Updater.Available == null)
                {
                    Updater.Check(delegate(string err) { updateStatus = err; w.Rebuild(); });
                    return;
                }
                if (!Updater.CanInstall) { Updater.OpenPage(); return; }
                Updater.Install(status, status);
            }
        }

        // Rebuilds the current section (e.g. when update information arrives).
        void Rebuild()
        {
            if (IsDisposed) return;
            Build();
            Invalidate();
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

        // Bottom of the sidebar: small mascot with its name and app status.
        class MiniMascot : Widget
        {
            public override bool Clickable { get { return true; } }
            bool ShowMascot(HomeWindow w) { return w.settings.MascotOn && w.page != "home" && w.page != "mascot"; }

            public override void Paint(Graphics g, HomeWindow w)
            {
                double h = hotT.Value, d = downT.Value;
                Rectangle r = R;
                r.Offset(0, (int)Math.Round(-w.P(2) * h + w.P(1) * d));
                LayeredCard(g, w, r, w.P(16), h);
                Color dot;
                string status = w.Status(out dot);
                if (ShowMascot(w))
                {
                    int tx = r.X + w.P(92);
                    Txt(g, w.settings.MascotName, w.F(14, 1), new Rectangle(tx, r.Y + w.P(30), r.Right - tx - w.P(10), w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, tx, r.Y + w.P(58), w.P(7), w.P(7));
                    Txt(g, status, w.F(11.5f, 0), new Rectangle(tx + w.P(12), r.Y + w.P(52), r.Right - tx - w.P(20), w.P(36)), Mac.Text2, TextFormatFlags.WordBreak);
                    return;
                }
                // Capture widget: icon, title, status and a real button with the shortcut, like a macOS widget.
                int pad = w.P(14);
                Rectangle ic = new Rectangle(r.X + pad, r.Y + pad, w.P(32), w.P(32));
                using (GraphicsPath p = Theme.Round(ic, w.P(9)))
                using (LinearGradientBrush lb = new LinearGradientBrush(Rectangle.Inflate(ic, 1, 1), ActionColors[0, 0], ActionColors[0, 1], 60f)) g.FillPath(lb, p);
                Icons.Draw(g, "area", Rectangle.Inflate(ic, -w.P(7), -w.P(7)), Color.White);
                int tx2 = ic.Right + w.P(10);
                Txt(g, "Capturar un \u00E1rea", w.F(13.5f, 1), new Rectangle(tx2, ic.Y - w.P(1), r.Right - tx2 - pad, w.P(18)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, tx2, ic.Y + w.P(23), w.P(6), w.P(6));
                Txt(g, status, w.F(11.5f, 0), new Rectangle(tx2 + w.P(10), ic.Y + w.P(18), r.Right - tx2 - pad - w.P(10), w.P(16)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Rectangle btn = new Rectangle(r.X + pad, r.Bottom - pad - w.P(30), r.Width - pad * 2, w.P(30));
                using (GraphicsPath p = Theme.Round(btn, btn.Height / 2f))
                using (LinearGradientBrush lb = new LinearGradientBrush(Rectangle.Inflate(btn, 0, 1), Mac.Mix(Color.FromArgb(48, 148, 255), Color.FromArgb(84, 168, 255), h), Mac.Blue, 90f))
                {
                    g.FillPath(lb, p);
                    using (Pen hl = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawLine(hl, btn.X + btn.Height / 2, btn.Y + 1, btn.Right - btn.Height / 2, btn.Y + 1);
                }
                Txt(g, "Capturar", w.F(12.5f, 1), new Rectangle(btn.X + w.P(14), btn.Y, btn.Width, btn.Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                string key = Hotkeys.Display(w.settings.HotRegion).Replace(" + ", "+");
                Txt(g, key, w.F(11.5f, 0), new Rectangle(btn.X, btn.Y, btn.Width - w.P(14), btn.Height), Color.FromArgb(205, 255, 255, 255), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            public override void Click(HomeWindow w, Point p)
            {
                if (ShowMascot(w)) w.SetPage("home", true);
                else w.RunAction("region");
            }
        }

        // Current app status (sidebar and hero).
        string Status(out Color dot)
        {
            if (Recorder.Recording) { dot = Mac.Red; return "Grabando\u2026"; }
            if (ScrollCapture.Active) { dot = Mac.Blue; return "Capturando con desplazamiento"; }
            if (Updater.Available != null) { dot = Mac.Blue; return "Versi\u00F3n " + Updater.Available.Version.ToString(3) + " disponible"; }
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

        // Setting row: colored icon, title and description; the control sits on the right.
        public abstract class Row : Widget
        {
            public string Title, Sub, Icon;
            public Color C1, C2;
            protected Row(string title, string sub, string icon, Color c1, Color c2) { Title = title; Sub = sub; Icon = icon; C1 = c1; C2 = c2; }
            public virtual int Height(HomeWindow w) { return w.P(Sub != null ? 60 : 50); }
            protected int ControlLeft;   // where the control starts (keeps text clear of it)
            // Lay out the control (and ControlLeft) before drawing text.
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

        // Segmented control with a sliding selection pill.
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

        // Row with a button on the right (none if the label is null).
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

        // Hotkey field drawn as keycaps; click it to record a new combination.
        class HotkeyRow : Row
        {
            readonly string action;
            Rectangle field;
            bool on, needsModifier;

            // Non-typing keys that may be used as a hotkey on their own.
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
            // Like a classic hotkey box: Delete clears it; extra combos from the settings file are kept.
            public void Take(HomeWindow w, Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.Escape && (keyData & Keys.Modifiers) == 0) { Stop(w); return; }
                bool clear = (key == Keys.Delete || key == Keys.Back) && (keyData & Keys.Modifiers) == 0;
                // A plain key alone (letter, Space, Enter...) would stop working system-wide.
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
                // Same combo on another action: remove it there (a combo maps to one action).
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
                // Choosing Print Screen means wanting it: take it back from the Snipping Tool right away.
                if (string.Equals(combo, "PrintScreen", StringComparison.OrdinalIgnoreCase) && Installer.SnippingOwnsPrintScreen)
                {
                    Installer.FreePrintScreen();
                    w.owner.Notify("Impr Pant ya es de Stackshot",
                                   "Windows la ten\u00EDa reservada para Recortes; ya est\u00E1 liberada en tu usuario. Si a\u00FAn se abre Recortes, cierra sesi\u00F3n y vuelve a entrar.");
                }
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
                if ((open.Contains(p) != open.Contains(last)) || (change.Contains(p) != change.Contains(last))) w.DirtyContent(R);
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
            // The real TextBox is placed over this field (HomeWindow.PlaceNameBox).
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

        // Header with icon, title and the hovered (or selected) option name; shared by the mascot grids.
        static void GridHeader(Graphics g, HomeWindow w, Row r)
        {
            Rectangle head = new Rectangle(r.R.X, r.R.Y, r.R.Width, w.P(56));
            int x = r.R.X + w.P(16);
            if (r.Icon != null)
            {
                w.IconTile(g, new Rectangle(x, head.Y + (head.Height - w.P(30)) / 2, w.P(30), w.P(30)), r.Icon, r.C1, r.C2);
                x += w.P(42);
            }
            int cy = head.Y + head.Height / 2;
            Txt(g, r.Title, w.F(13.5f, 0), new Rectangle(x, cy - w.P(20), r.R.Right - x - w.P(16), w.P(20)), Mac.Text, TextFormatFlags.SingleLine);
            Txt(g, r.Sub ?? "", w.F(12, 0), new Rectangle(x, cy + w.P(1), r.R.Right - x - w.P(16), w.P(18)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // Hover moved between grid cells: repaint those two cells (plus a label strip below) and the header name.
        static void HoverMoved(HomeWindow w, Row row, Rectangle[] cells, int was, int now, int below)
        {
            if (was >= 0) w.DirtyContent(new Rectangle(cells[was].X, cells[was].Y, cells[was].Width, cells[was].Height + below));
            if (now >= 0) w.DirtyContent(new Rectangle(cells[now].X, cells[now].Y, cells[now].Width, cells[now].Height + below));
            w.DirtyContent(new Rectangle(row.R.X, row.R.Y, row.R.Width, w.P(56)));
        }

        static void Padlock(Graphics g, HomeWindow w, Rectangle c, int level)
        {
            Fill(g, c, w.P(12), Color.FromArgb(150, 18, 18, 20));
            int s = w.P(16), x = c.X + (c.Width - s) / 2, y = c.Y + c.Height / 2 - s / 2 - w.P(4);
            using (Pen p = new Pen(Mac.Text, Math.Max(1.5f, w.P(2)))) g.DrawArc(p, x + s * 0.2f, y - s * 0.35f, s * 0.6f, s * 0.7f, 180, 180);
            Fill(g, new Rectangle(x, y, s, (int)(s * 0.8f)), w.P(3), Mac.Text);
            Txt(g, "Nv. " + (level + 1), w.F(10.5f, 1), new Rectangle(c.X, y + s, c.Width, w.P(16)), Mac.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }

        // Grid of variations, each one previewed on the mascot itself. Locked items show the level that unlocks them.
        class PickerRow : Row
        {
            readonly string[] names;
            readonly Func<MascotLook, int> get;
            readonly Action<MascotLook, int> set;
            readonly Func<int, int> unlock;
            Rectangle[] cells = new Rectangle[0];
            Point last;

            public PickerRow(string title, string icon, Color c1, Color c2, string[] names, Func<MascotLook, int> get, Action<MascotLook, int> set, Func<int, int> unlock)
                : base(title, "", icon, c1, c2)
            {
                this.names = names; this.get = get; this.set = set; this.unlock = unlock;
            }

            public override bool Clickable { get { return false; } }
            int Cell(HomeWindow w) { return w.P(66); }
            int Gap(HomeWindow w) { return w.P(8); }
            int Cols(HomeWindow w) { return Math.Max(1, (w.CW - w.P(32) + Gap(w)) / (Cell(w) + Gap(w))); }
            public override int Height(HomeWindow w)
            {
                int rows = (names.Length + Cols(w) - 1) / Cols(w);
                return w.P(58) + rows * (Cell(w) + Gap(w)) + w.P(8);
            }
            public override void Layout(HomeWindow w)
            {
                int cols = Cols(w), cs = Cell(w), gap = Gap(w);
                cells = new Rectangle[names.Length];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new Rectangle(R.X + w.P(16) + (i % cols) * (cs + gap), R.Y + w.P(56) + (i / cols) * (cs + gap), cs, cs);
                ControlLeft = 0;
            }
            public override bool Hit(Point p) { return IndexAt(cells, p) >= 0; }
            public override void Move(HomeWindow w, Point p)
            {
                int was = IndexAt(cells, last), now = IndexAt(cells, p);
                last = p;
                if (was == now) return;
                HoverMoved(w, this, cells, was, now, 0);
                // Try it on: the big mascot wears the hovered option until the mouse leaves.
                if (now >= 0 && !Locked(w, now))
                {
                    MascotLook l = MascotLook.From(w.settings);
                    set(l, now);
                    w.TryOn(l);
                }
                else w.TryOn(null);
            }
            bool Locked(HomeWindow w, int i) { return unlock != null && unlock(i) > MascotParts.Level(w.settings.MascotLove); }

            public override void Paint(Graphics g, HomeWindow w)
            {
                Layout(w);
                MascotLook cur = MascotLook.From(w.settings);
                int sel = get(cur), hov = hotT.Value > 0 ? IndexAt(cells, last) : -1, shown = hov >= 0 ? hov : sel;
                Sub = names[shown];
                if (Locked(w, shown))
                    Sub += "  \u00B7  se desbloquea al ser \u00AB" + MascotParts.LevelNames[unlock(shown)].ToLowerInvariant() + "\u00BB (" + MascotParts.UnlockLove(unlock(shown)) + " capturas)";
                RectangleF clip = g.ClipBounds;
                if (clip.IntersectsWith(new Rectangle(R.X, R.Y, R.Width, w.P(56)))) GridHeader(g, w, this);
                for (int i = 0; i < cells.Length; i++)
                {
                    Rectangle c = cells[i];
                    if (!clip.IntersectsWith(Rectangle.Inflate(c, w.P(4), w.P(4)))) continue; // partial repaint: skip cells outside it
                    Fill(g, c, w.P(12), i == sel ? Color.FromArgb(34, 10, 132, 255) : i == hov ? Color.FromArgb(48, 48, 53) : Color.FromArgb(33, 33, 36));
                    MascotLook l = cur.Clone();
                    l.Seasonal = false;
                    set(l, i);
                    Bitmap pv = w.Preview(l, c.Width, Title + i, c);
                    if (pv != null) g.DrawImageUnscaled(pv, c.X, c.Y);
                    if (Locked(w, i)) Padlock(g, w, c, unlock(i));
                    if (i == sel || i == hov)
                        using (GraphicsPath p = Theme.Round(Rectangle.Inflate(c, w.P(2), w.P(2)), w.P(13)))
                        using (Pen pen = new Pen(i == sel ? Mac.Blue : Color.FromArgb(70, 255, 255, 255), w.P(2))) g.DrawPath(pen, p);
                }
            }
            protected override void PaintControl(Graphics g, HomeWindow w) { }
            public override void Click(HomeWindow w, Point p)
            {
                int i = IndexAt(cells, p);
                if (i < 0) return;
                if (Locked(w, i))
                {
                    int left = MascotParts.UnlockLove(unlock(i)) - w.settings.MascotLove;
                    if (w.settings.MascotOn) w.mascot.Say("\u00A1A\u00FAn no! Te faltan " + left + (left == 1 ? " captura" : " capturas") + " para eso.", 2800);
                    return;
                }
                MascotLook l = MascotLook.From(w.settings);
                if (get(l) == i) return;
                set(l, i);
                w.LookChanged(l, i > 0 ? "\u00A1" + names[i] + "! \u00BFQu\u00E9 tal me queda?" : null);
            }
        }

        // One-click looks, plus a random one.
        class StyleRow : Row
        {
            Rectangle[] cells = new Rectangle[0];
            Point last;
            static readonly Random rnd = new Random();
            readonly string[] names;
            readonly Action<MascotLook, int> apply;
            readonly string slot;
            readonly bool surprise;
            public string[] Hints;   // where each costume comes from, shown in the header while hovering a cell
            readonly string baseSub;
            public StyleRow(string title, string desc, string icon, Color c1, Color c2, string[] names, Action<MascotLook, int> apply, string slot, bool surprise)
                : base(title, desc, icon, c1, c2)
            {
                this.names = names; this.apply = apply; this.slot = slot; this.surprise = surprise;
                baseSub = desc;
            }
            public override bool Clickable { get { return false; } }
            int Count { get { return names.Length + (surprise ? 1 : 0); } }
            int PerRow(HomeWindow w) { return Math.Max(1, (w.CW - w.P(24)) / (w.P(88) + w.P(8))); } // rows are as wide as the content column
            public override int Height(HomeWindow w) { int rows = (Count + PerRow(w) - 1) / PerRow(w); return w.P(58) + rows * (w.P(66) + w.P(26)) - w.P(4) + w.P(4); }
            public override void Layout(HomeWindow w)
            {
                int gap = w.P(8), per = Math.Min(Count, PerRow(w)), cs = Math.Min(w.P(88), (R.Width - w.P(32) - gap * (per - 1)) / per);
                cells = new Rectangle[Count];
                for (int i = 0; i < Count; i++)
                    cells[i] = new Rectangle(R.X + w.P(16) + (i % per) * (cs + gap), R.Y + w.P(56) + (i / per) * (w.P(66) + w.P(26)), cs, w.P(66));
                ControlLeft = 0;
            }
            public override bool Hit(Point p) { return IndexAt(cells, p) >= 0; }
            public override void Move(HomeWindow w, Point p)
            {
                int was = IndexAt(cells, last), now = IndexAt(cells, p);
                last = p;
                if (was == now) return;
                HoverMoved(w, this, cells, was, now, w.P(26));
                if (Hints != null) w.DirtyContent(new Rectangle(R.X, R.Y, R.Width, w.P(56)));
                if (now >= 0 && now < names.Length)
                {
                    MascotLook l = MascotLook.From(w.settings);
                    apply(l, now);
                    w.TryOn(l);
                }
                else w.TryOn(null);
            }
            public override void Paint(Graphics g, HomeWindow w)
            {
                Layout(w);
                int hv = hotT.Value > 0 ? IndexAt(cells, last) : -1;
                if (Hints != null) Sub = hv >= 0 && hv < Hints.Length && Hints[hv] != null ? "Homenaje fan \u00B7 " + Hints[hv] : baseSub;
                GridHeader(g, w, this);
                int hov = hotT.Value > 0 ? IndexAt(cells, last) : -1;
                for (int i = 0; i < cells.Length; i++)
                {
                    Rectangle c = cells[i];
                    Fill(g, c, w.P(12), i == hov ? Color.FromArgb(48, 48, 53) : Color.FromArgb(33, 33, 36));
                    if (i < names.Length)
                    {
                        MascotLook l = MascotLook.From(w.settings);
                        l.Seasonal = false;
                        apply(l, i);
                        Bitmap pv = w.Preview(l, c.Height, slot + i, c);
                        if (pv != null) g.DrawImageUnscaled(pv, c.X + (c.Width - pv.Width) / 2, c.Y);
                    }
                    else
                    {
                        int s = w.P(30);
                        w.IconTile(g, new Rectangle(c.X + (c.Width - s) / 2, c.Y + (c.Height - s) / 2, s, s), "sparkle", Mac.Pink, Mac.Blue);
                    }
                    string name = i < names.Length ? names[i] : "Sorpr\u00E9ndeme";
                    Txt(g, name, w.F(11.5f, i == hov ? 1 : 0), new Rectangle(c.X - w.P(4), c.Bottom + w.P(4), c.Width + w.P(8), w.P(18)), i == hov ? Mac.Text : Mac.Text2,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }
            protected override void PaintControl(Graphics g, HomeWindow w) { }
            public override void Click(HomeWindow w, Point p)
            {
                int i = IndexAt(cells, p);
                if (i < 0) return;
                MascotLook l = MascotLook.From(w.settings);
                if (i < names.Length)
                {
                    apply(l, i);
                    w.LookChanged(l, Hints != null ? "\u00A1Hoy soy " + names[i] + "!" : "\u00A1Modo " + names[i].ToLowerInvariant() + " activado!");
                }
                else
                {
                    MascotParts.Randomize(l, MascotParts.Level(w.settings.MascotLove), rnd);
                    w.LookChanged(l, "\u00A1Tach\u00E1n! \u00BFQu\u00E9 te parece?");
                }
            }
        }

        // Standalone pill button. Accent = primary (blue).
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

        // Card with real depth: a soft multi-layer shadow, a subtle vertical gradient, a lit top edge and a hairline
        // border. hot (0-1) lifts it slightly. Used for every surface of the home page and the sidebar widget.
        static void LayeredCard(Graphics g, HomeWindow w, Rectangle r, float radius, double hot)
        {
            for (int i = 3; i >= 1; i--)
            {
                Rectangle sh = r;
                sh.Inflate(-w.P(2) + i, 0);
                sh.Offset(0, (int)(w.P(2) * i + w.P(2) * hot));
                Fill(g, sh, radius + i, Color.FromArgb((int)((16 + 10 * hot) / i * 1.4), 0, 0, 0));
            }
            using (GraphicsPath p = Theme.Round(r, radius))
            {
                Color top = Mac.Mix(Color.FromArgb(46, 46, 50), Color.FromArgb(56, 56, 61), hot), bottom = Mac.Mix(Color.FromArgb(37, 37, 40), Color.FromArgb(45, 45, 49), hot);
                using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(r, 0, 1), top, bottom, 90f)) g.FillPath(b, p);
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (LinearGradientBrush hl = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, (int)(radius * 1.6f) + 1), Color.FromArgb(34, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(hl, r.X, r.Y, r.Width, radius * 1.6f);
                g.Clip = old;
                old.Dispose();
                using (Pen pen = new Pen(Color.FromArgb((int)(16 + 18 * hot), 255, 255, 255))) g.DrawPath(pen, p);
            }
        }

        // Home hero, Apple-style: calm card, the mascot on the left (drawn separately), a big question and one
        // primary action.
        class Hero : Widget
        {
            Rectangle btn;
            public override bool Clickable { get { return false; } }
            public override bool Hit(Point p) { return btn.Contains(p); }
            public override void Paint(Graphics g, HomeWindow w)
            {
                LayeredCard(g, w, R, w.P(22), 0);
                if (w.settings.MascotOn)
                    using (GraphicsPath p = Theme.Round(R, w.P(22)))
                    {
                        Region old = g.Clip;
                        g.SetClip(p, CombineMode.Intersect);
                        Color c = MascotParts.Colors[MascotLook.From(w.settings).Color, 0];
                        Intro.PaintGlow(g, R.X + w.P(118), R.Y + R.Height / 2, w.P(330), Color.FromArgb(42, c));
                        g.Clip = old;
                        old.Dispose();
                    }
                int x = w.settings.MascotOn ? R.X + w.P(262) : R.X + w.P(36), right = R.Right - w.P(32);
                int y = R.Y + (R.Height - w.P(150)) / 2;
                Txt(g, MascotTalk.Greeting().Replace("\u00A1", "").TrimEnd('!'), w.F(13, 1), new Rectangle(x, y, right - x, w.P(18)), Mac.Text2, TextFormatFlags.SingleLine);
                Txt(g, "\u00BFQu\u00E9 capturamos?", w.F(30, 2), new Rectangle(x, y + w.P(20), right - x, w.P(42)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Color dot;
                string status = w.Status(out dot);
                using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, x + w.P(1), y + w.P(73), w.P(7), w.P(7));
                Txt(g, status, w.F(13, 0), new Rectangle(x + w.P(14), y + w.P(67), right - x - w.P(14), w.P(20)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                // Primary action: blue pill with the shortcut inside, as on macOS.
                string key = Hotkeys.Display(w.settings.HotRegion).Replace(" + ", "+");
                Font bf = w.F(13.5f, 1), kf = w.F(12, 0);
                int bw = TextRenderer.MeasureText("Capturar un \u00E1rea", bf).Width + TextRenderer.MeasureText(key, kf).Width + w.P(56);
                btn = new Rectangle(x, y + w.P(104), bw, w.P(40));
                double h = hotT.Value, d = downT.Value;
                Rectangle br = btn;
                br.Offset(0, (int)Math.Round(w.P(1) * d));
                Fill(g, new Rectangle(br.X + w.P(4), br.Y + w.P(4), br.Width - w.P(8), br.Height), br.Height / 2f, Color.FromArgb((int)(50 + 30 * h), 10, 80, 200));
                using (GraphicsPath p = Theme.Round(br, br.Height / 2f))
                using (LinearGradientBrush lb = new LinearGradientBrush(Rectangle.Inflate(br, 0, 1), Mac.Mix(Color.FromArgb(54, 152, 255), Color.FromArgb(88, 172, 255), h), Mac.Blue, 90f))
                    g.FillPath(lb, p);
                using (Pen hl = new Pen(Color.FromArgb(70, 255, 255, 255))) g.DrawLine(hl, br.X + br.Height / 2, br.Y + 1, br.Right - br.Height / 2, br.Y + 1);
                Txt(g, "Capturar un \u00E1rea", bf, new Rectangle(br.X + w.P(20), br.Y, br.Width, br.Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Txt(g, key, kf, new Rectangle(br.X, br.Y, br.Width - w.P(20), br.Height), Color.FromArgb(200, 255, 255, 255), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            public override void Click(HomeWindow w, Point p) { if (btn.Contains(p)) w.RunAction("region"); }
        }

        // Home tile: a capture type. Minimal: icon, name and its shortcut in quiet text; lifts on hover.
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
                LayeredCard(g, w, r, w.P(16), h);
                Rectangle ic = new Rectangle(r.X + w.P(18), r.Y + w.P(18), w.P(34), w.P(34));
                bool stop = action == "video" && Recorder.Recording;
                using (GraphicsPath p = Theme.Round(ic, w.P(10)))
                using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(ic, 1, 1), ActionColors[index, 0], ActionColors[index, 1], 60f)) g.FillPath(b, p);
                int m = w.P(8);
                Icons.Draw(g, stop ? "stop" : ActionIcons[index], Rectangle.Inflate(ic, -m, -m), Color.White);
                string title = stop ? "Detener grabaci\u00F3n" : ActionShort[index];
                Txt(g, title, w.F(14, 1), new Rectangle(r.X + w.P(18), r.Bottom - w.P(46), r.Width - w.P(28), w.P(20)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                string combo = w.settings.HotkeysFor(action);
                string disp = Hotkeys.Split(combo).Count > 0 ? Hotkeys.Display(combo).Replace(" + ", "+") : "Sin atajo";
                Txt(g, disp, w.F(12, 0), new Rectangle(r.X + w.P(18), r.Bottom - w.P(26), r.Width - w.P(28), w.P(18)), Mac.Text3, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
            public override void Click(HomeWindow w, Point p) { w.RunAction(action); }
        }

        // Mascot section stage: the big mascot (drawn separately) and its name.
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
                    Color c = MascotParts.Colors[Math.Max(0, Math.Min(MascotParts.Colors.GetLength(0) - 1, w.settings.MascotColor)), 0];
                    Intro.PaintGlow(g, R.X + w.P(124), R.Y + R.Height / 2, w.P(420), Color.FromArgb(70, c));
                    g.Clip = old;
                    old.Dispose();
                    using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255))) g.DrawPath(pen, p);
                }
                int x = R.X + w.P(262), right = R.Right - w.P(28);
                Settings s = w.settings;
                Txt(g, s.MascotOn ? s.MascotName : "Escondido", w.F(28, 2), new Rectangle(x, R.Y + w.P(56), right - x, w.P(40)), Mac.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                string who = s.MascotOn ? MascotParts.Kinds[MascotLook.From(s).Kind] + "  \u00B7  " + MascotParts.Personalities[MascotLook.From(s).Personality]
                                        : "Activa \u00ABMostrar la mascota\u00BB para que vuelva.";
                Txt(g, who, w.F(13, 0), new Rectangle(x, R.Y + w.P(100), right - x, w.P(20)), Mac.Text2, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                // Friendship: hearts, level name and progress to the next one.
                int level = MascotParts.Level(s.MascotLove), left;
                double prog = MascotParts.Progress(s.MascotLove, out left);
                int hy = R.Y + w.P(140), hs = w.P(18);
                for (int i = 0; i < MascotParts.LevelNames.Length; i++)
                    using (GraphicsPath h = MascotParts.Heart(x + i * (hs + w.P(6)) + hs / 2f, hy + hs / 2f, hs))
                        MascotParts.FillSolid(g, h, i <= level ? Color.FromArgb(255, 92, 140) : Color.FromArgb(60, 255, 255, 255));
                Txt(g, MascotParts.LevelNames[level], w.F(13, 1), new Rectangle(x, hy + w.P(26), right - x, w.P(20)), Mac.Text, TextFormatFlags.SingleLine);
                Rectangle bar = new Rectangle(x, hy + w.P(54), Math.Min(w.P(320), right - x), w.P(8));
                Fill(g, bar, bar.Height / 2f, Color.FromArgb(50, 255, 255, 255));
                Rectangle done = new Rectangle(bar.X, bar.Y, Math.Max(bar.Height, (int)(bar.Width * prog)), bar.Height);
                using (GraphicsPath p = Theme.Round(done, bar.Height / 2f))
                using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(bar, 1, 1), Color.FromArgb(255, 92, 140), Mac.Purple, 0f)) g.FillPath(b, p);
                string next = left > 0 ? "Te faltan " + left + (left == 1 ? " captura" : " capturas") + " para \u00AB" + MascotParts.LevelNames[level + 1].ToLowerInvariant() + "\u00BB. Cada captura suma."
                                       : "Nivel m\u00E1ximo: lo hab\u00E9is desbloqueado todo.";
                Txt(g, next, w.F(12, 0), new Rectangle(x, bar.Bottom + w.P(8), right - x, w.P(36)), Mac.Text3, TextFormatFlags.WordBreak);
            }
        }

        // Backdrop preview with a sample window (never a real capture).
        class BackdropPreview : Widget
        {
            static Bitmap sample;
            static Bitmap composed;      // shared across visits so each visit doesn't leak one
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

        // Grid with every backdrop; the selected one has a ring.
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
                if (was != now) HoverMoved(w, this, cells, was, now, 0);
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
                    bool wasCustom = Backdrop.IsCustom(w.settings.BgPreset);
                    w.settings.BgPreset = i;
                    w.Changed();
                    if (wasCustom != Backdrop.IsCustom(i)) w.Rebuild(); // show or hide "Quitar este fondo"
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
                Txt(g, "Captura, marca y comparte en segundos. Miniaturas flotantes, editor r\u00E1pido, v\u00EDdeo y GIF.", w.F(13, 0), new Rectangle(R.X + w.P(60), lr.Bottom + w.P(92), R.Width - w.P(120), w.P(40)), Mac.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            }
        }
    }
}
