// Stackshot - Asking the mascot to do things: chips on Home and its page, a menu with every command, and its behavior settings.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // The behavior settings reach the shared mascot (cheap field copies, so it can run every tick).
        void ApplyMascotBehavior()
        {
            mascot.Talks = settings.MascotTalks;
            mascot.Energy = settings.MascotEnergy;
            mascot.Chatter = settings.MascotChatter;
            mascot.Alone = settings.MascotAlone;
            mascot.SleepIdle = settings.MascotSleepIdle;
            mascot.Rhythm = settings.MascotRhythm;
            MascotParts.NightCaps = settings.MascotRhythm;
        }

        // How long between its unprompted tips, by how talkative it is.
        double TipScale { get { return settings.MascotChatter == 0 ? 3.0 : settings.MascotChatter == 2 ? 0.5 : 1.0; } }

        void MascotDo(int cmd)
        {
            if (!settings.MascotOn) return;
            ApplyMascotBehavior();
            mascot.Perform(cmd);
            Wake();
        }

        MacButton CommandButton(int cmd, int height)
        {
            MacButton b = new MacButton(MascotCmd.Names[cmd], ButtonKind.Secondary, MascotCmd.Glyphs[cmd], null, height);
            b.ToolTip = MascotCmd.Hints[cmd];
            int c = cmd;
            b.Click += delegate { MascotDo(c); };
            return b;
        }

        // The usual commands side by side, and a button with all of them.
        FrameworkElement CommandChips(bool vertical)
        {
            Panel row = vertical ? (Panel)new StackPanel() : new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 560 };
            foreach (int cmd in MascotCmd.Quick)
            {
                MacButton b = CommandButton(cmd, 28);
                b.Margin = vertical ? new Thickness(0, 0, 0, 7) : new Thickness(4, 4, 4, 0);
                if (vertical) b.HorizontalAlignment = HorizontalAlignment.Left;
                row.Children.Add(b);
            }
            MacButton more = new MacButton("M\u00E1s", ButtonKind.Plain, "down", null, 28);
            more.ToolTip = "Todo lo que sabe hacer";
            more.Margin = vertical ? new Thickness(0) : new Thickness(4, 4, 4, 0);
            if (vertical) more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Click += delegate { CommandMenu(more); };
            row.Children.Add(more);
            return row;
        }

        // One small button that opens the menu, for places with no room for chips.
        FrameworkElement CommandPill()
        {
            MacButton b = new MacButton("Juega", ButtonKind.Secondary, "play", null, 28);
            b.ToolTip = "P\u00EDdele algo a " + settings.MascotName;
            b.Click += delegate { CommandMenu(b); };
            return b;
        }

        Popup CommandMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title("P\u00EDdele algo a " + settings.MascotName));
            // A dragon's own power comes first.
            if (settings.MascotOn && mascot.HasPower)
            {
                MenuEntry pe = MenuEntry.Item(MascotParts.PowerName(mascot.Look.Kind), "flame", false, delegate { ApplyMascotBehavior(); mascot.Power(); Wake(); });
                pe.Detail = MascotParts.PowerHint(mascot.Look.Kind);
                m.Add(pe);
            }
            for (int i = 0; i < MascotCmd.Count; i++)
            {
                int c = i;
                MenuEntry e = MenuEntry.Item(MascotCmd.Short[c], MascotCmd.Glyphs[c], false, delegate { MascotDo(c); });
                e.Detail = MascotCmd.Hints[c];
                m.Add(e);
                if (c == MascotCmd.Sleep || c == MascotCmd.Love || c == MascotCmd.Tricks - 1) m.Add(MenuEntry.Line());
            }
            return MacMenu.ShowGrid(anchor, null, m, 4);
        }

        // ---- Behavior, in the mascot settings

        void BuildMascotBehavior()
        {
            Func<bool> shown = delegate { return settings.MascotOn; };
            Group("Comportamiento",
                new SegRow("Actividad", "Cada cu\u00E1nto hace cosas por su cuenta.", "hop", Mac.Orange, Mac.Pink, new string[] { "Tranquila", "Normal", "Juguetona" },
                           delegate { return settings.MascotEnergy; }, delegate(int i) { settings.MascotEnergy = i; ApplyMascotBehavior(); mascot.Perform(i == 0 ? MascotCmd.Bow : i == 1 ? MascotCmd.Wave : MascotCmd.Jump); Wake(); owner.MascotChanged(); }) { When = shown },
                new SegRow("Charla", "Cu\u00E1nto comenta lo que hace y te cuenta trucos.", "sparkle", Mac.Blue, Mac.Purple, new string[] { "Poca", "Normal", "Mucha" },
                           delegate { return settings.MascotChatter; }, delegate(int i) { settings.MascotChatter = i; ApplyMascotBehavior(); if (settings.MascotTalks) mascot.Say(MascotTalk.Hello(settings), 3000); Wake(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Se duerme sola", "Si no le haces caso un rato, bosteza y se echa una siesta.", "moon", Mac.Indigo, Mac.Purple,
                              delegate { return settings.MascotSleepIdle; }, delegate(bool v) { settings.MascotSleepIdle = v; ApplyMascotBehavior(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Seg\u00FAn la hora del d\u00EDa", "Caf\u00E9 por la ma\u00F1ana, gorro de dormir por la noche y fiesta los viernes; y un flash cada vez que haces una captura.", "clock", Mac.Orange, Mac.Pink,
                              delegate { return settings.MascotRhythm; }, delegate(bool v) { settings.MascotRhythm = v; ApplyMascotBehavior(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Recorre todas las pantallas", "En el escritorio pasea de un monitor a otro en vez de quedarse en uno.", "screen", Mac.Teal, Mac.Blue,
                              delegate { return settings.MascotAllScreens; }, delegate(bool v) { settings.MascotAllScreens = v; owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Se aparta con la pantalla completa", "Si un juego, una presentaci\u00F3n o una ventana a pantalla completa (como el Escritorio remoto o una m\u00E1quina virtual) ocupa su pantalla, se va a otra libre y no entra en esa; si no hay otra, se queda detr\u00E1s. Ap\u00E1galo y se queda siempre delante.", "maximize", Mac.Indigo, Mac.Blue,
                              delegate { return settings.MascotHideFull; }, delegate(bool v) { settings.MascotHideFull = v; owner.MascotChanged(); }) { When = shown });
            Group("Lo que hace por su cuenta",
                AloneRow("Saltitos y gestos", "Hops, gui\u00F1os y estiramientos.", "hop", MascotCmd.AloneSmall),
                AloneRow("Bailes y piruetas", "Bailar, girar, voltereta y pompas.", "sound", MascotCmd.AloneDance),
                AloneRow("Juegos con objetos", "Malabares, caf\u00E9, consola, lectura y fotos.", "play", MascotCmd.AloneProps),
                AloneRow("Saludos y miradas", "Saluda, mira alrededor y hace reverencias.", "heart", MascotCmd.AloneLook));
        }

        // ---- Healthy breaks, in the mascot settings

        void BuildWellness()
        {
            Func<bool> on = delegate { return settings.WellOn; };
            Group("Pausas saludables",
                new ToggleRow("Recordarme hacer pausas", "Te avisa para beber agua, descansar la vista, moverte y respirar, y las hace contigo.", "heart", Mac.Pink, Mac.Red,
                              delegate { return settings.WellOn; }, delegate(bool v) { settings.WellOn = v; }),
                WellRow("Beber agua", "Se toma un vaso contigo.", "drop", Mac.Blue, Mac.Teal, new int[] { 0, 30, 45, 60, 90 },
                        delegate { return settings.WellWater; }, delegate(int m) { settings.WellWater = m; }, on),
                WellRow("Descansar la vista", "Regla 20-20-20: mirar algo a 6 metros durante 20 segundos.", "eye", Mac.Green, Mac.Teal, new int[] { 0, 20, 30, 45, 60 },
                        delegate { return settings.WellEyes; }, delegate(int m) { settings.WellEyes = m; }, on),
                WellRow("Moverse", "Sentadillas o estirar la espalda, una vez cada cosa.", "walk", Mac.Orange, Mac.Pink, new int[] { 0, 30, 45, 60, 90 },
                        delegate { return settings.WellMove; }, delegate(int m) { settings.WellMove = m; }, on),
                WellRow("Respirar", "Un minuto de respiraci\u00F3n lenta: 4 segundos dentro y 6 fuera.", "wind", Mac.Indigo, Mac.Purple, new int[] { 0, 60, 90, 120 },
                        delegate { return settings.WellBreathe; }, delegate(int m) { settings.WellBreathe = m; }, on),
                new ToggleRow("Avisar con notificaciones", "Cuando la mascota no est\u00E1 en el escritorio, el aviso llega como una notificaci\u00F3n de Stackshot.", "bell", Mac.Orange, Mac.Red,
                              delegate { return settings.WellToasts; }, delegate(bool v) { settings.WellToasts = v; }) { When = on });
            Note(Wellness.Summary(settings) + " Solo cuenta el tiempo que est\u00E1s delante del ordenador: si te ausentas 5 minutos, la vista y el cuerpo ya han descansado y empieza de cero. " +
                 "Nunca te interrumpe en una llamada, presentando, grabando, a pantalla completa ni en el tiempo de silencio de Windows: espera a que termines. " +
                 "Puedes hacer una pausa cuando quieras desde su men\u00FA en el escritorio (\u00ABUna pausa conmigo\u00BB).");
        }

        // ---- Focus mode (Pomodoro), in the mascot settings

        void BuildFocus()
        {
            FocusClock f = owner.FocusState;
            string line = owner.FocusLine(), today = owner.FocusToday();
            int[] work = { 25, 30, 45, 50 }, shortB = { 5, 10 }, longB = { 15, 20, 30 }, rounds = { 3, 4, 5 };
            Group("Modo concentraci\u00F3n",
                new ButtonRow(f.On ? "Concentraci\u00F3n en marcha" : "Concentrarme ahora",
                              (line ?? "Bloques de trabajo con descansos entre medias (la t\u00E9cnica Pomodoro).") + (today != null ? " " + today + "." : ""),
                              "clock", Mac.Orange, Mac.Red, f.On ? "Terminar" : "Empezar",
                              delegate { if (owner.FocusState.On) owner.FocusStop(); else owner.FocusStart(); Rebuild(); }),
                FocusRow("Cada bloque", "El tiempo de trabajo concentrado.", "clock", Mac.Orange, Mac.Pink, work,
                         delegate { return settings.FocusWork; }, delegate(int m) { settings.FocusWork = m; }),
                FocusRow("Descanso corto", "Entre un bloque y el siguiente.", "coffee", Mac.Green, Mac.Teal, shortB,
                         delegate { return settings.FocusShort; }, delegate(int m) { settings.FocusShort = m; }),
                FocusRow("Descanso largo", "Al completar una ronda de bloques.", "moon", Mac.Indigo, Mac.Purple, longB,
                         delegate { return settings.FocusLong; }, delegate(int m) { settings.FocusLong = m; }),
                new SegRow("Bloques por ronda", "Cu\u00E1ntos bloques hay antes del descanso largo.", "stack", Mac.Blue, Mac.Indigo, new string[] { "3", "4", "5" },
                           delegate { int i = Array.IndexOf(rounds, settings.FocusRounds); return i < 0 ? 1 : i; },
                           delegate(int i) { settings.FocusRounds = rounds[Math.Max(0, Math.Min(rounds.Length - 1, i))]; }),
                new ToggleRow("Seguir solo", "Al acabar un bloque empieza el descanso, y al acabar el descanso el siguiente bloque, sin preguntarte.", "update", Mac.Teal, Mac.Blue,
                              delegate { return settings.FocusAuto; }, delegate(bool v) { settings.FocusAuto = v; }),
                new ToggleRow("Sonido", "Un sonido suave y distinto para cada momento: empezar, pausar, reanudar, acabar, descansar y terminar.", "sound", Mac.Pink, Mac.Red,
                              delegate { return settings.FocusSound; }, delegate(bool v) { settings.FocusSound = v; if (v) FocusChime.Play(FocusChime.Cue.Start); }),
                new ToggleRow("Avisar con notificaciones", "Cuando la mascota no est\u00E1 en el escritorio, junto al reloj de Windows: al empezar, pausar, reanudar y terminar cada bloque y cada descanso. Con la mascota, te lo dice ella.", "bell", Mac.Orange, Mac.Red,
                              delegate { return settings.FocusNotices; }, delegate(bool v) { settings.FocusNotices = v; }));
            Note("Empieza desde aqu\u00ED o desde el men\u00FA de la mascota en el escritorio (\u00ABConcentrarme\u00BB). Mientras dura un bloque, la mascota lee a tu lado sin hablar, " +
                 "lleva el reloj encima de la cabeza y las pausas saludables esperan al descanso, donde te propone la que toque. Si te levantas m\u00E1s de 5 minutos, " +
                 "el reloj se para y al volver te pregunta si sigues. Sin la mascota en el escritorio, todo sigue solo y te avisa con notificaciones.");
        }

        // ---- Health and activity, in the mascot settings

        void BuildHealth()
        {
            int[] goals = { 0, 6, 8, 10 }, hours = { 0, 17, 18, 19 };
            List<Row> rows = new List<Row>();
            int water = owner.WaterToday();
            rows.Add(new SegRow("Objetivo de agua", "Vasos al d\u00EDa. Cuentan los que bebes con la mascota y \u00AB+1 vaso\u00BB en su men\u00FA. Hoy: " + water + (water == 1 ? " vaso." : " vasos."),
                                "drop", Mac.Blue, Mac.Teal, new string[] { "Sin objetivo", "6 vasos", "8 vasos", "10 vasos" },
                                delegate { int i = Array.IndexOf(goals, settings.WaterGoal); return i < 0 ? 2 : i; },
                                delegate(int i) { settings.WaterGoal = goals[Math.Max(0, Math.Min(goals.Length - 1, i))]; }));
            // Three usual times, or one of your own (set in the row that then shows under it).
            bool custom = SummaryCustom;
            rows.Add(new SegRow("Resumen del d\u00EDa", "A esa hora te cuenta c\u00F3mo ha ido: agua, pausas, concentraci\u00F3n, capturas y pasos.", "heart", Mac.Pink, Mac.Red,
                                new string[] { "No", "17:00", "18:00", "19:00", "Otra" },
                                delegate
                                {
                                    if (settings.SummaryHour <= 0) return 0;
                                    if (SummaryCustom) return 4;
                                    int i = Array.IndexOf(hours, settings.SummaryHour);
                                    return i < 0 ? 2 : i;
                                },
                                delegate(int i)
                                {
                                    bool was = SummaryCustom;
                                    if (i < 4) { settings.SummaryHour = hours[Math.Max(0, i)]; settings.SummaryMinute = 0; }
                                    else if (!was) { settings.SummaryHour = 20; settings.SummaryMinute = 30; }
                                    // The row with your own time comes or goes.
                                    if (SummaryCustom != was) Dispatcher.BeginInvoke((Action)delegate { if (IsVisible) Rebuild(); });
                                }));
            if (custom)
                rows.Add(new TimeRow("Tu hora para el resumen", "De 15 en 15 minutos, entre las 8:00 y las 23:45.", "clock", Mac.Pink, Mac.Orange,
                                     delegate { return settings.SummaryHour * 60 + settings.SummaryMinute; },
                                     delegate(int m) { settings.SummaryHour = m / 60; settings.SummaryMinute = m % 60; }));
            // What it tells, there and in its menu's "how was the day": one chip per part, each on or off.
            // Only the parts that can have something to tell: water and breaks while they are on, steps with the watch.
            List<string> names = new List<string>();
            List<int> bits = new List<int>();
            if (owner.WaterOn) { names.Add("Agua"); bits.Add(Settings.SummaryWater); }
            if (settings.WellOn) { names.Add("Pausas"); bits.Add(Settings.SummaryBreaks); }
            names.Add("Concentraci\u00F3n"); bits.Add(Settings.SummaryFocus);
            names.Add("Capturas"); bits.Add(Settings.SummaryCaptures);
            if (GoogleHealth.Connected) { names.Add("Pasos"); bits.Add(Settings.SummarySteps); }
            rows.Add(new ChipsRow("Qu\u00E9 incluye el resumen", "Toca para quitar o poner. Vale tambi\u00E9n para \u00AB\u00BFQu\u00E9 tal el d\u00EDa?\u00BB de su men\u00FA.", "check", Mac.Green, Mac.Teal,
                                  names.ToArray(), bits.ToArray(), delegate { return settings.SummaryParts; }, delegate(int v) { settings.SummaryParts = v; }));
            if (GoogleHealth.Available)
            {
                bool on = GoogleHealth.Connected;
                rows.Add(new WatchBenefitsRow());
                rows.Add(new ButtonRow("Pixel Watch o Fitbit",
                                       GoogleHealth.Connecting ? "Conectando\u2026 Termina de iniciar sesi\u00F3n en la pesta\u00F1a que se ha abierto en tu navegador y vuelve aqu\u00ED."
                                       : on ? "Conectado con tu cuenta de Google."
                                       : "Pasos, sue\u00F1o y minutos activos de tu reloj, para que la mascota te acompa\u00F1e mejor. Inicias sesi\u00F3n en el navegador, una vez.",
                                       "google", Mac.Teal, Mac.Green, GoogleHealth.Connecting ? "Cancelar" : on ? "Desconectar" : "Conectar",
                                       delegate
                                       {
                                           if (GoogleHealth.Connecting) GoogleHealth.Cancel();
                                           else if (GoogleHealth.Connected) { owner.DisconnectWatch(); Rebuild(); }
                                           else { owner.ConnectWatch(delegate { if (IsVisible) Rebuild(); }); Rebuild(); }
                                       }));
                if (on && !GoogleHealth.Connecting)
                {
                    rows.Add(new WatchRow());
                    int[] steps = { 6000, 8000, 10000, 12000, 15000 };
                    rows.Add(new SegRow("Objetivo de pasos", "Lo que cuenta la barra, la mascota y el resumen del d\u00EDa.", "walk", Mac.Green, Mac.Teal,
                                        new string[] { "6.000", "8.000", "10.000", "12.000", "15.000" },
                                        delegate { int i = Array.IndexOf(steps, settings.StepsGoal); return i < 0 ? 2 : i; },
                                        delegate(int i) { settings.StepsGoal = steps[Math.Max(0, Math.Min(steps.Length - 1, i))]; }));
                }
                rows.Add(new ToggleRow("Comentarios sobre tu actividad", "Te propone levantarte si llevas una hora quieto o una ma\u00F1ana floja, un paseo despu\u00E9s de comer, celebra tus paseos, minutos activos y objetivo de pasos, te dice c\u00F3mo vas y cu\u00E1nto te falta, y c\u00F3mo has dormido. Cada cosa, pocas veces al d\u00EDa.", "sparkle", Mac.Orange, Mac.Pink,
                                       delegate { return settings.HealthTalk; }, delegate(bool v) { settings.HealthTalk = v; }) { When = delegate { return GoogleHealth.Connected; } });
                rows.Add(new SoonRow());
            }
            Group("Salud y actividad", rows.ToArray());
            Note("Todo se queda en tu equipo. Con el reloj conectado, los datos van de Google a tu ordenador y no pasan por ning\u00FAn otro sitio; " +
                 "las claves se guardan cifradas con tu usuario de Windows y puedes desconectarlo cuando quieras. La mascota no da consejos m\u00E9dicos: " +
                 "solo te anima a moverte, beber y descansar. Si una noche duermes menos de 6 horas, ese d\u00EDa las pausas para la vista y para moverte llegan un poco antes; " +
                 "si el reloj ve que acabas de caminar, la pausa para moverte espera; " +
                 "y con el objetivo de agua cumplido, deja de record\u00E1rtelo.");
        }

        // ---- The assistant, in the mascot settings: reminders, notes and SOAP faults.

        void BuildAssist()
        {
            List<Reminder> pending = owner.Reminders;
            string next = pending.Count == 0 ? "Ninguno pendiente." :
                          pending.Count + (pending.Count == 1 ? " pendiente" : " pendientes") + "; el pr\u00F3ximo " + ShotStack.WhenText(pending[0].DueUtc.ToLocalTime()) + ": \u00AB" + pending[0].Text + "\u00BB.";
            Group("Ayudante",
                new ButtonRow("Recordatorios", "\u00ABRecu\u00E9rdame algo\u2026\u00BB en el men\u00FA de la mascota. " + next, "bell", Mac.Blue, Mac.Indigo, "Nuevo\u2026",
                              delegate { QuickWindow.Remind(owner); }),
                new ButtonRow("Notas", "\u00ABApunta algo\u2026\u00BB en su men\u00FA, o suelta un texto encima de ella. Van a Notas.md, en tu carpeta de capturas; en esa misma ventana marcas las hechas o las borras.", "memo", Mac.Orange, Mac.Pink, "Ver",
                              delegate { owner.OpenNotes(); }),
                new ButtonRow("Accesos r\u00E1pidos", LinksSub(), "open", Mac.Indigo, Mac.Blue, "Editar\u2026", delegate { LinksWindow.Show(owner); }),
                new ToggleRow("Avisarme de los XML con fallo", "Cuando llega a la carpeta vigilada un XML con SOAP Fault, te lo dice con un bot\u00F3n para abrirlo.", "warning", Mac.Red, Mac.Orange,
                              delegate { return settings.XmlFaultAlert; }, delegate(bool v) { settings.XmlFaultAlert = v; }));
            Note("Tambi\u00E9n puedes soltar encima de la mascota una imagen (va al editor), un v\u00EDdeo (al reproductor), un XML o un Markdown (al visor), " +
                 "un texto (a tus notas) o una carpeta (a tus accesos r\u00E1pidos). Los recordatorios esperan a que est\u00E9s delante, y si el ordenador estaba apagado a su hora, te avisa al volver.");
        }

        // "Intranet, Proyectos y 2 m\u00E1s en el men\u00FA de la mascota."
        string LinksSub()
        {
            List<QuickLink> l = owner.Links;
            if (l.Count == 0) return "Webs, carpetas, archivos o programas que la mascota te abre desde su men\u00FA. Tambi\u00E9n puedes soltarle una carpeta encima.";
            string names = l.Count == 1 ? "\u00AB" + l[0].Name + "\u00BB" : l.Count == 2 ? "\u00AB" + l[0].Name + "\u00BB y \u00AB" + l[1].Name + "\u00BB"
                         : "\u00AB" + l[0].Name + "\u00BB, \u00AB" + l[1].Name + "\u00BB y " + (l.Count - 2) + " m\u00E1s";
            int keyed = 0;
            foreach (QuickLink x in l) if (x.Keys.Length > 0) keyed++;
            return names + " en el men\u00FA de la mascota" + (keyed == 0 ? "; en Atajos les pones un atajo de teclado." : keyed == l.Count ? ", todos con su atajo." : "; " + keyed + " con atajo.");
        }

        // A summary time other than 17:00, 18:00 and 19:00.
        bool SummaryCustom { get { return settings.SummaryHour > 0 && (settings.SummaryMinute != 0 || (settings.SummaryHour != 17 && settings.SummaryHour != 18 && settings.SummaryHour != 19)); } }

        // A time of day with − and + on each side, a quarter of an hour at a time (minutes from midnight).
        public class TimeRow : Row
        {
            const int Step = 15, First = 8 * 60, Last = 23 * 60 + 45;
            readonly Func<int> get;
            readonly Action<int> set;
            TextBlock shown;

            public TimeRow(string title, string sub, string icon, System.Drawing.Color c1, System.Drawing.Color c2, Func<int> get, Action<int> set)
                : base(title, sub, icon, c1, c2)
            {
                this.get = get;
                this.set = set;
            }

            static string At(int m) { return m / 60 + ":" + (m % 60).ToString("00"); }

            protected override FrameworkElement Control()
            {
                Palette pal = Ds.Brushes;
                StackPanel p = new StackPanel { Orientation = Orientation.Horizontal };
                MacButton less = new MacButton("\u2212", ButtonKind.Secondary, null, null, 28) { ToolTip = "15 minutos antes" };
                MacButton more = new MacButton("+", ButtonKind.Secondary, null, null, 28) { ToolTip = "15 minutos despu\u00E9s" };
                shown = Label(At(get()), Ds.Semibold, 14, pal.Label, new Thickness(12, 0, 12, 0));
                shown.MinWidth = 44;
                shown.TextAlignment = TextAlignment.Center;
                System.Windows.Automation.AutomationProperties.SetName(less, "15 minutos antes");
                System.Windows.Automation.AutomationProperties.SetName(more, "15 minutos despu\u00E9s");
                less.Click += delegate { Move(-Step); };
                more.Click += delegate { Move(Step); };
                p.Children.Add(less);
                p.Children.Add(shown);
                p.Children.Add(more);
                return p;
            }

            void Move(int by)
            {
                // Snapped to the quarter hour, inside the day's range.
                int m = (int)Math.Round(get() / (double)Step) * Step + by;
                m = Math.Max(First, Math.Min(Last, m));
                bool was = W.SummaryCustom;
                set(m);
                if (shown != null) shown.Text = At(m);
                W.Changed();
                W.SyncRows();
                // Landed on 17:00, 18:00 or 19:00: that choice lights up above and this row goes.
                if (W.SummaryCustom != was) W.Dispatcher.BeginInvoke((Action)delegate { if (W.IsVisible) W.Rebuild(); });
            }
        }

        // A row of chips that each turn one bit of a setting on or off (lit: included).
        public class ChipsRow : Row
        {
            readonly string[] names;
            readonly int[] bits;
            readonly Func<int> get;
            readonly Action<int> set;
            readonly List<Border> chips = new List<Border>();

            public ChipsRow(string title, string sub, string icon, System.Drawing.Color c1, System.Drawing.Color c2, string[] names, int[] bits, Func<int> get, Action<int> set)
                : base(title, sub, icon, c1, c2)
            {
                this.names = names;
                this.bits = bits;
                this.get = get;
                this.set = set;
            }

            protected override FrameworkElement Control()
            {
                WrapPanel p = new WrapPanel { MaxWidth = 330, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 6) };
                chips.Clear();
                for (int i = 0; i < names.Length; i++)
                {
                    int bit = bits[i];
                    TapBorder c = new TapBorder { CornerRadius = new CornerRadius(13), Padding = new Thickness(11, 4, 11, 5), Margin = new Thickness(6, 3, 0, 3), Cursor = System.Windows.Input.Cursors.Hand,
                                                  Label = names[i] };
                    c.Child = new TextBlock { Text = names[i], FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
                    Action flip = delegate { set(get() ^ bit); W.Changed(); Paint(); };
                    c.MouseLeftButtonUp += delegate { flip(); };
                    Access.Clickable(c, flip);
                    chips.Add(c);
                    p.Children.Add(c);
                }
                Paint();
                return p;
            }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                int v = get();
                for (int i = 0; i < chips.Count; i++)
                {
                    bool on = (v & bits[i]) != 0;
                    Border c = chips[i];
                    c.Background = Ds.Brush(on ? (pal.High ? pal.Accent : Ds.WithAlpha(pal.Accent, pal.Dark ? 0.30 : 0.16)) : pal.Control);
                    c.BorderBrush = Ds.Brush(on && !pal.High ? Ds.WithAlpha(pal.Accent, 0.55) : pal.Hairline);
                    c.BorderThickness = new Thickness(1);
                    TextBlock t = (TextBlock)c.Child;
                    t.Text = (on ? "\u2713 " : "") + names[i];
                    t.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                    t.Foreground = Ds.Brush(on ? (pal.High ? pal.OnAccent : pal.Label) : pal.Label2);
                    System.Windows.Automation.AutomationProperties.SetItemStatus(c, on ? "incluido" : "no incluido");
                }
            }

            public override void Sync(bool animate)
            {
                base.Sync(animate);
                if (chips.Count > 0) Paint();
            }
        }

        SegRow FocusRow(string title, string sub, string icon, System.Drawing.Color c1, System.Drawing.Color c2, int[] minutes, Func<int> get, Action<int> set)
        {
            string[] names = new string[minutes.Length];
            for (int i = 0; i < minutes.Length; i++) names[i] = minutes[i] + " min";
            return new SegRow(title, sub, icon, c1, c2, names,
                              delegate { int i = Array.IndexOf(minutes, get()); return i < 0 ? 0 : i; },
                              delegate(int i) { set(minutes[Math.Max(0, Math.Min(minutes.Length - 1, i))]); });
        }

        SegRow WellRow(string title, string sub, string icon, System.Drawing.Color c1, System.Drawing.Color c2, int[] minutes, Func<int> get, Action<int> set, Func<bool> when)
        {
            string[] names = new string[minutes.Length];
            for (int i = 0; i < minutes.Length; i++) names[i] = WellKind.MinuteNames[WellKind.IndexOf(minutes[i])];
            return new SegRow(title, sub, icon, c1, c2, names,
                              delegate { int i = Array.IndexOf(minutes, get()); return i < 0 ? 0 : i; },
                              delegate(int i) { set(minutes[Math.Max(0, Math.Min(minutes.Length - 1, i))]); })
                { When = when };
        }

        ToggleRow AloneRow(string title, string sub, string icon, int bit)
        {
            return new ToggleRow(title, sub, icon, Mac.Teal, Mac.Blue,
                                 delegate { return (settings.MascotAlone & bit) != 0; },
                                 delegate(bool v) { settings.MascotAlone = v ? settings.MascotAlone | bit : settings.MascotAlone & ~bit; ApplyMascotBehavior(); owner.MascotChanged(); })
                { When = delegate { return settings.MascotOn; } };
        }
    }
}
