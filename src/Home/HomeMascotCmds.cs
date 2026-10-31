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
            MacButton more = new MacButton(I18n.T("M\u00E1s", "More"), ButtonKind.Plain, "down", null, 28);
            more.ToolTip = I18n.T("Todo lo que sabe hacer", "Everything it knows how to do");
            more.Margin = vertical ? new Thickness(0) : new Thickness(4, 4, 4, 0);
            if (vertical) more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Click += delegate { CommandMenu(more); };
            row.Children.Add(more);
            return row;
        }

        // One small button that opens the menu, for places with no room for chips.
        FrameworkElement CommandPill()
        {
            MacButton b = new MacButton(I18n.T("Juega", "Play"), ButtonKind.Secondary, "play", null, 28);
            b.ToolTip = I18n.Fmt("P\u00EDdele algo a {0}", "Ask {0} to do something", settings.MascotName);
            b.Click += delegate { CommandMenu(b); };
            return b;
        }

        Popup CommandMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title(I18n.Fmt("P\u00EDdele algo a {0}", "Ask {0} to do something", settings.MascotName)));
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
            Group(I18n.T("Comportamiento", "Behavior"),
                new SegRow(I18n.T("Actividad", "Activity"), I18n.T("Cada cu\u00E1nto hace cosas por su cuenta.", "How often it does things on its own."), "hop", Mac.Orange, Mac.Pink,
                           I18n.IsSpanish ? new string[] { "Tranquila", "Normal", "Juguetona" } : new string[] { "Calm", "Normal", "Playful" },
                           delegate { return settings.MascotEnergy; }, delegate(int i) { settings.MascotEnergy = i; ApplyMascotBehavior(); mascot.Perform(i == 0 ? MascotCmd.Bow : i == 1 ? MascotCmd.Wave : MascotCmd.Jump); Wake(); owner.MascotChanged(); }) { When = shown },
                new SegRow(I18n.T("Charla", "Chatter"), I18n.T("Cu\u00E1nto comenta lo que hace y te cuenta trucos.", "How often it comments on what it does and shares tips."), "sparkle", Mac.Blue, Mac.Purple,
                           I18n.IsSpanish ? new string[] { "Poca", "Normal", "Mucha" } : new string[] { "Low", "Normal", "High" },
                           delegate { return settings.MascotChatter; }, delegate(int i) { settings.MascotChatter = i; ApplyMascotBehavior(); if (settings.MascotTalks) mascot.Say(MascotTalk.Hello(settings), 3000); Wake(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow(I18n.T("Se duerme sola", "Falls asleep when idle"), I18n.T("Si no le haces caso un rato, bosteza y se echa una siesta.", "If left alone for a while, yawns and takes a nap."), "moon", Mac.Indigo, Mac.Purple,
                              delegate { return settings.MascotSleepIdle; }, delegate(bool v) { settings.MascotSleepIdle = v; ApplyMascotBehavior(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow(I18n.T("Seg\u00FAn la hora del d\u00EDa", "Time-based behavior"), I18n.T("Caf\u00E9 por la ma\u00F1ana, gorro de dormir por la noche y fiesta los viernes; y un flash cada vez que haces una captura.", "Morning coffee, nightcap in the evening, Friday celebrations, and camera flash on captures."), "clock", Mac.Orange, Mac.Pink,
                              delegate { return settings.MascotRhythm; }, delegate(bool v) { settings.MascotRhythm = v; ApplyMascotBehavior(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow(I18n.T("Recorre todas las pantallas", "Roam all monitors"), I18n.T("En el escritorio pasea de un monitor a otro en vez de quedarse en uno.", "On the desktop, walks across monitors instead of staying on one."), "screen", Mac.Teal, Mac.Blue,
                              delegate { return settings.MascotAllScreens; }, delegate(bool v) { settings.MascotAllScreens = v; owner.MascotChanged(); }) { When = shown },
                new ToggleRow(I18n.T("Se aparta con la pantalla completa", "Hide in full screen"), I18n.T("Si un juego, una presentaci\u00F3n o una ventana a pantalla completa (como el Escritorio remoto o una m\u00E1quina virtual) ocupa su pantalla, se va a otra libre y no entra en esa; si no hay otra, se queda detr\u00E1s. Ap\u00E1galo y se queda siempre delante.", "During full screen games, presentations, or remote sessions, moves to another screen or hides. Turn off to keep always on top."), "maximize", Mac.Indigo, Mac.Blue,
                              delegate { return settings.MascotHideFull; }, delegate(bool v) { settings.MascotHideFull = v; owner.MascotChanged(); }) { When = shown });
            Group(I18n.T("Lo que hace por su cuenta", "Idle activities"),
                AloneRow(I18n.T("Saltitos y gestos", "Hops and gestures"), I18n.T("Hops, gui\u00F1os y estiramientos.", "Hops, winks, and stretches."), "hop", MascotCmd.AloneSmall),
                AloneRow(I18n.T("Bailes y piruetas", "Dances and twirls"), I18n.T("Bailar, girar, voltereta y pompas.", "Dancing, spinning, flips, and bubbles."), "sound", MascotCmd.AloneDance),
                AloneRow(I18n.T("Juegos con objetos", "Props and activities"), I18n.T("Malabares, caf\u00E9, consola, lectura y fotos.", "Juggling, coffee, gaming, reading, and photos."), "play", MascotCmd.AloneProps),
                AloneRow(I18n.T("Saludos y miradas", "Waves and glances"), I18n.T("Saluda, mira alrededor y hace reverencias.", "Waves, looks around, and bows."), "heart", MascotCmd.AloneLook));
        }

        // ---- Healthy breaks, in the mascot settings

        void BuildWellness()
        {
            Func<bool> on = delegate { return settings.WellOn; };
            Group(I18n.T("Pausas saludables", "Healthy breaks"),
                new ToggleRow(I18n.T("Recordarme hacer pausas", "Remind me to take breaks"), I18n.T("Te avisa para beber agua, descansar la vista, moverte y respirar, y las hace contigo.", "Reminds you to drink water, rest eyes, stretch, and breathe \u2014 and joins in with you."), "heart", Mac.Pink, Mac.Red,
                              delegate { return settings.WellOn; }, delegate(bool v) { settings.WellOn = v; }),
                WellRow(I18n.T("Beber agua", "Drink water"), I18n.T("Se toma un vaso contigo.", "Drinks a glass of water with you."), "drop", Mac.Blue, Mac.Teal, new int[] { 0, 30, 45, 60, 90 },
                        delegate { return settings.WellWater; }, delegate(int m) { settings.WellWater = m; }, on),
                WellRow(I18n.T("Descansar la vista", "Rest eyes"), I18n.T("Regla 20-20-20: mirar algo a 6 metros durante 20 segundos.", "20-20-20 rule: look at something 20 feet away for 20 seconds."), "eye", Mac.Green, Mac.Teal, new int[] { 0, 20, 30, 45, 60 },
                        delegate { return settings.WellEyes; }, delegate(int m) { settings.WellEyes = m; }, on),
                WellRow(I18n.T("Moverse", "Movement break"), I18n.T("Sentadillas o estirar la espalda, una vez cada cosa.", "Squats or back stretches, alternating each time."), "walk", Mac.Orange, Mac.Pink, new int[] { 0, 30, 45, 60, 90 },
                        delegate { return settings.WellMove; }, delegate(int m) { settings.WellMove = m; }, on),
                WellRow(I18n.T("Respirar", "Breathing session"), I18n.T("Un minuto de respiraci\u00F3n lenta: 4 segundos dentro y 6 fuera.", "One minute of deep breathing: 4 seconds in, 6 seconds out."), "wind", Mac.Indigo, Mac.Purple, new int[] { 0, 60, 90, 120 },
                        delegate { return settings.WellBreathe; }, delegate(int m) { settings.WellBreathe = m; }, on),
                new ToggleRow(I18n.T("Avisar con notificaciones", "Notify with system toasts"), I18n.T("Cuando la mascota no est\u00E1 en el escritorio, el aviso llega como una notificaci\u00F3n de Stackshot.", "When mascot is not on desktop, reminders appear as Stackshot notifications."), "bell", Mac.Orange, Mac.Red,
                              delegate { return settings.WellToasts; }, delegate(bool v) { settings.WellToasts = v; }) { When = on });
            Note(Wellness.Summary(settings) + " " + I18n.T("Solo cuenta el tiempo que est\u00E1s delante del ordenador: si te ausentas 5 minutos, la vista y el cuerpo ya han descansado y empieza de cero. " +
                 "Nunca te interrumpe en una llamada, presentando, grabando, a pantalla completa ni en el tiempo de silencio de Windows: espera a que termines. " +
                 "Puedes hacer una pausa cuando quieras desde su men\u00FA en el escritorio (\u00ABUna pausa conmigo\u00BB).",
                 "Only counts active time at the PC: if you step away for 5 minutes, your eyes and body have rested and timer resets. " +
                 "Never interrupts during calls, presentations, recordings, full screen, or Windows Focus Assist: waits until you finish. " +
                 "Take a break anytime from the desktop mascot menu (\u201CTake a break with me\u201D)."));
        }

        // ---- Focus mode (Pomodoro), in the mascot settings

        void BuildFocus()
        {
            FocusClock f = owner.FocusState;
            string line = owner.FocusLine(), today = owner.FocusToday();
            int[] work = { 25, 30, 45, 50 }, shortB = { 5, 10 }, longB = { 15, 20, 30 }, rounds = { 3, 4, 5 };
            Group(I18n.T("Modo concentraci\u00F3n", "Focus mode"),
                new ButtonRow(f.On ? I18n.T("Concentraci\u00F3n en marcha", "Focus in progress") : I18n.T("Concentrarme ahora", "Focus now"),
                              (line ?? I18n.T("Bloques de trabajo con descansos entre medias (la t\u00E9cnica Pomodoro).", "Work intervals with scheduled breaks in between (Pomodoro technique).")) + (today != null ? " " + today + "." : ""),
                              "clock", Mac.Orange, Mac.Red, f.On ? I18n.T("Terminar", "Stop") : I18n.T("Empezar", "Start"),
                              delegate { if (owner.FocusState.On) owner.FocusStop(); else owner.FocusStart(); Rebuild(); }),
                FocusRow(I18n.T("Cada bloque", "Work interval"), I18n.T("El tiempo de trabajo concentrado.", "Length of each focused work period."), "clock", Mac.Orange, Mac.Pink, work,
                         delegate { return settings.FocusWork; }, delegate(int m) { settings.FocusWork = m; }),
                FocusRow(I18n.T("Descanso corto", "Short break"), I18n.T("Entre un bloque y el siguiente.", "Break between work intervals."), "coffee", Mac.Green, Mac.Teal, shortB,
                         delegate { return settings.FocusShort; }, delegate(int m) { settings.FocusShort = m; }),
                FocusRow(I18n.T("Descanso largo", "Long break"), I18n.T("Al completar una ronda de bloques.", "Break after completing a full set of intervals."), "moon", Mac.Indigo, Mac.Purple, longB,
                         delegate { return settings.FocusLong; }, delegate(int m) { settings.FocusLong = m; }),
                new SegRow(I18n.T("Bloques por ronda", "Intervals per set"), I18n.T("Cu\u00E1ntos bloques hay antes del descanso largo.", "Number of focus intervals before a long break."), "stack", Mac.Blue, Mac.Indigo, new string[] { "3", "4", "5" },
                           delegate { int i = Array.IndexOf(rounds, settings.FocusRounds); return i < 0 ? 1 : i; },
                           delegate(int i) { settings.FocusRounds = rounds[Math.Max(0, Math.Min(rounds.Length - 1, i))]; }),
                new ToggleRow(I18n.T("Seguir solo", "Auto-start breaks & intervals"), I18n.T("Al acabar un bloque empieza el descanso, y al acabar el descanso el siguiente bloque, sin preguntarte.", "Automatically transition between focus intervals and breaks without asking."), "update", Mac.Teal, Mac.Blue,
                              delegate { return settings.FocusAuto; }, delegate(bool v) { settings.FocusAuto = v; }),
                new ToggleRow(I18n.T("Sonido", "Chimes"), I18n.T("Un sonido suave y distinto para cada momento: empezar, pausar, reanudar, acabar, descansar y terminar.", "Gentle chimes for starting, pausing, resuming, and completing breaks and intervals."), "sound", Mac.Pink, Mac.Red,
                              delegate { return settings.FocusSound; }, delegate(bool v) { settings.FocusSound = v; if (v) FocusChime.Play(FocusChime.Cue.Start); }),
                new ToggleRow(I18n.T("Avisar con notificaciones", "Notify with system toasts"), I18n.T("Cuando la mascota no est\u00E1 en el escritorio, junto al reloj de Windows: al empezar, pausar, reanudar y terminar cada bloque y cada descanso. Con la mascota, te lo dice ella.", "When mascot is not on desktop, show Windows notifications for each event. On desktop, mascot speaks."), "bell", Mac.Orange, Mac.Red,
                              delegate { return settings.FocusNotices; }, delegate(bool v) { settings.FocusNotices = v; }));
            Note(I18n.T("Empieza desde aqu\u00ED o desde el men\u00FA de la mascota en el escritorio (\u00ABConcentrarme\u00BB). Mientras dura un bloque, la mascota lee a tu lado sin hablar, " +
                 "lleva el reloj encima de la cabeza y las pausas saludables esperan al descanso, donde te propone la que toque. Si te levantas m\u00E1s de 5 minutos, " +
                 "el reloj se para y al volver te pregunta si sigues. Sin la mascota en el escritorio, todo sigue solo y te avisa con notificaciones.",
                 "Start from here or from the mascot desktop menu (\u201CFocus\u201D). During focus, mascot quietly reads by your side without speaking, " +
                 "wearing a timer on its head. Healthy breaks wait until rest periods. If you step away for 5+ minutes, timer pauses. " +
                 "Without the mascot on desktop, runs quietly and notifies via system toasts."));
        }

        // ---- Health and activity, in the mascot settings

        void BuildHealth()
        {
            int[] goals = { 0, 6, 8, 10 }, hours = { 0, 17, 18, 19 };
            List<Row> rows = new List<Row>();
            int water = owner.WaterToday();
            rows.Add(new SegRow(I18n.T("Objetivo de agua", "Daily water goal"),
                                I18n.Fmt("Vasos al d\u00EDa. Cuentan los que bebes con la mascota y \u00AB+1 vaso\u00BB en su men\u00FA. Hoy: {0} {1}.",
                                         "Glasses per day. The ones you drink with the mascot and \u201C+1 glass\u201D in its menu count. Today: {0} {1}.",
                                         water, water == 1 ? I18n.T("vaso", "glass") : I18n.T("vasos", "glasses")),
                                "drop", Mac.Blue, Mac.Teal,
                                I18n.IsSpanish ? new string[] { "Sin objetivo", "6 vasos", "8 vasos", "10 vasos" } : new string[] { "No goal", "6 glasses", "8 glasses", "10 glasses" },
                                delegate { int i = Array.IndexOf(goals, settings.WaterGoal); return i < 0 ? 2 : i; },
                                delegate(int i) { settings.WaterGoal = goals[Math.Max(0, Math.Min(goals.Length - 1, i))]; }));
            // Three usual times, or one of your own (set in the row that then shows under it).
            bool custom = SummaryCustom;
            rows.Add(new SegRow(I18n.T("Resumen del d\u00EDa", "Daily summary"), I18n.T("A esa hora te cuenta c\u00F3mo ha ido: agua, pausas, concentraci\u00F3n, capturas y pasos.", "Recaps your day at this time: water, breaks, focus, captures, and steps."), "heart", Mac.Pink, Mac.Red,
                                I18n.IsSpanish ? new string[] { "No", "17:00", "18:00", "19:00", "Otra" } : new string[] { "Off", "17:00", "18:00", "19:00", "Custom" },
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
                rows.Add(new TimeRow(I18n.T("Tu hora para el resumen", "Custom summary time"), I18n.T("De 15 en 15 minutos, entre las 8:00 y las 23:45.", "15-minute increments between 8:00 and 23:45."), "clock", Mac.Pink, Mac.Orange,
                                     delegate { return settings.SummaryHour * 60 + settings.SummaryMinute; },
                                     delegate(int m) { settings.SummaryHour = m / 60; settings.SummaryMinute = m % 60; }));
            // What it tells, there and in its menu's "how was the day": one chip per part, each on or off.
            // Only the parts that can have something to tell: water and breaks while they are on, steps with the watch.
            List<string> names = new List<string>();
            List<int> bits = new List<int>();
            if (owner.WaterOn) { names.Add(I18n.T("Agua", "Water")); bits.Add(Settings.SummaryWater); }
            if (settings.WellOn) { names.Add(I18n.T("Pausas", "Breaks")); bits.Add(Settings.SummaryBreaks); }
            names.Add(I18n.T("Concentraci\u00F3n", "Focus")); bits.Add(Settings.SummaryFocus);
            names.Add(I18n.T("Capturas", "Captures")); bits.Add(Settings.SummaryCaptures);
            if (GoogleHealth.Connected) { names.Add(I18n.T("Pasos", "Steps")); bits.Add(Settings.SummarySteps); }
            rows.Add(new ChipsRow(I18n.T("Qu\u00E9 incluye el resumen", "Summary contents"), I18n.T("Toca para quitar o poner. Vale tambi\u00E9n para \u00AB\u00BFQu\u00E9 tal el d\u00EDa?\u00BB de su men\u00FA.", "Click to toggle. Also applies to \u201CHow's your day going?\u201D in the mascot's menu."), "check", Mac.Green, Mac.Teal,
                                  names.ToArray(), bits.ToArray(), delegate { return settings.SummaryParts; }, delegate(int v) { settings.SummaryParts = v; }));
            if (GoogleHealth.Available)
            {
                bool on = GoogleHealth.Connected;
                rows.Add(new WatchBenefitsRow());
                rows.Add(new ButtonRow(I18n.T("Pixel Watch o Fitbit", "Pixel Watch or Fitbit"),
                                       GoogleHealth.Connecting ? I18n.T("Conectando\u2026 Termina de iniciar sesi\u00F3n en la pesta\u00F1a que se ha abierto en tu navegador y vuelve aqu\u00ED.", "Connecting\u2026 Finish signing in on the browser tab and return here.")
                                       : on ? I18n.T("Conectado con tu cuenta de Google.", "Connected with your Google account.")
                                       : I18n.T("Pasos, sue\u00F1o y minutos activos de tu reloj, para que la mascota te acompa\u00F1e mejor. Inicias sesi\u00F3n en el navegador, una vez.", "Sync steps, sleep, and active minutes from your smartwatch. One-time browser login."),
                                       "google", Mac.Teal, Mac.Green, GoogleHealth.Connecting ? I18n.T("Cancelar", "Cancel") : on ? I18n.T("Desconectar", "Disconnect") : I18n.T("Conectar", "Connect"),
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
                    rows.Add(new SegRow(I18n.T("Objetivo de pasos", "Daily step goal"), I18n.T("Lo que cuenta la barra, la mascota y el resumen del d\u00EDa.", "Used for the status bar, mascot speech, and daily summary."), "walk", Mac.Green, Mac.Teal,
                                        I18n.IsSpanish ? new string[] { "6.000", "8.000", "10.000", "12.000", "15.000" } : new string[] { "6,000", "8,000", "10,000", "12,000", "15,000" },
                                        delegate { int i = Array.IndexOf(steps, settings.StepsGoal); return i < 0 ? 2 : i; },
                                        delegate(int i) { settings.StepsGoal = steps[Math.Max(0, Math.Min(steps.Length - 1, i))]; }));
                }
                rows.Add(new ToggleRow(I18n.T("Comentarios sobre tu actividad", "Activity comments"), I18n.T("Te propone levantarte si llevas una hora quieto o una ma\u00F1ana floja, un paseo despu\u00E9s de comer, celebra tus paseos, minutos activos y objetivo de pasos, te dice c\u00F3mo vas y cu\u00E1nto te falta, y c\u00F3mo has dormido. Cada cosa, pocas veces al d\u00EDa.", "Gentle reminders to move after an hour of sitting, celebrates walks, active minutes, and step goals, notes sleep duration. Rare, unobtrusive daily speech."), "sparkle", Mac.Orange, Mac.Pink,
                                       delegate { return settings.HealthTalk; }, delegate(bool v) { settings.HealthTalk = v; }) { When = delegate { return GoogleHealth.Connected; } });
                rows.Add(new SoonRow());
            }
            Group(I18n.T("Salud y actividad", "Health and activity"), rows.ToArray());
            Note(I18n.T("Todo se queda en tu equipo. Con el reloj conectado, los datos van de Google a tu ordenador y no pasan por ning\u00FAn otro sitio; " +
                 "las claves se guardan cifradas con tu usuario de Windows y puedes desconectarlo cuando quieras. La mascota no da consejos m\u00E9dicos: " +
                 "solo te anima a moverte, beber y descansar. Si una noche duermes menos de 6 horas, ese d\u00EDa las pausas para la vista y para moverte llegan un poco antes; " +
                 "si el reloj ve que acabas de caminar, la pausa para moverte espera; " +
                 "y con el objetivo de agua cumplido, deja de record\u00E1rtelo.",
                 "Everything stays local. With smartwatch connected, data travels directly between Google and your PC; " +
                 "credentials are encrypted with Windows DPAPI and you can disconnect anytime. Mascot offers encouragement, not medical advice. " +
                 "If you sleep under 6 hours, break intervals adapt slightly earlier; if you recently walked, movement pauses wait; " +
                 "once daily water goal is reached, reminders stop."));
        }

        // ---- The assistant, in the mascot settings: reminders, notes and SOAP faults.

        void BuildAssist()
        {
            List<Reminder> pending = owner.Reminders;
            string next = pending.Count == 0 ? I18n.T("Ninguno pendiente.", "None pending.") :
                          I18n.Fmt(pending.Count == 1 ? "{0} pendiente" : "{0} pendientes", pending.Count == 1 ? "{0} pending" : "{0} pending", pending.Count) +
                          I18n.Fmt("; el pr\u00F3ximo {0}: \u00AB{1}\u00BB.", "; next {0}: \u201C{1}\u201D.", ShotStack.WhenText(pending[0].DueUtc.ToLocalTime()), pending[0].Text);
            Group(I18n.T("Ayudante", "Assistant"),
                new ButtonRow(I18n.T("Recordatorios", "Reminders"), I18n.T("\u00ABRecu\u00E9rdame algo\u2026\u00BB en el men\u00FA de la mascota. ", "\u201CRemind me of something\u2026\u201D in the mascot's menu. ") + next, "bell", Mac.Blue, Mac.Indigo, I18n.T("Nuevo\u2026", "New\u2026"),
                              delegate { QuickWindow.Remind(owner); }),
                new ButtonRow(I18n.T("Notas", "Notes"), I18n.T("\u00ABApunta algo\u2026\u00BB en su men\u00FA, o suelta un texto encima de ella. Van a Notas.md, en tu carpeta de capturas; en esa misma ventana marcas las hechas o las borras.", "\u201CNote something\u2026\u201D in its menu, or drop text onto the mascot. Saved to Notas.md in captures folder; check off or delete items there."), "memo", Mac.Orange, Mac.Pink, I18n.T("Ver", "View"),
                              delegate { owner.OpenNotes(); }),
                new ButtonRow(I18n.T("Accesos r\u00E1pidos", "Quick links"), LinksSub(), "open", Mac.Indigo, Mac.Blue, I18n.T("Editar\u2026", "Edit\u2026"), delegate { LinksWindow.Show(owner); }),
                new ToggleRow(I18n.T("Avisarme de los XML con fallo", "Notify on failed XML files"), I18n.T("Cuando llega a la carpeta vigilada un XML con SOAP Fault, te lo dice con un bot\u00F3n para abrirlo.", "Alerts with an open button when an XML with a SOAP Fault arrives in the monitored folder."), "warning", Mac.Red, Mac.Orange,
                              delegate { return settings.XmlFaultAlert; }, delegate(bool v) { settings.XmlFaultAlert = v; }));
            Note(I18n.T("Tambi\u00E9n puedes soltar encima de la mascota una imagen (va al editor), un v\u00EDdeo (al reproductor), un XML o un Markdown (al visor), " +
                 "un texto (a tus notas) o una carpeta (a tus accesos r\u00E1pidos). Los recordatorios esperan a que est\u00E9s delante, y si el ordenador estaba apagado a su hora, te avisa al volver.",
                 "You can also drag & drop files onto the mascot: images go to editor, videos to player, XML/Markdown to viewer, text to notes, folders to quick links. Reminders wait until you are active, and overdue reminders alert upon wake."));
        }

        // "Intranet, Proyectos y 2 m\u00E1s en el men\u00FA de la mascota."
        string LinksSub()
        {
            List<QuickLink> l = owner.Links;
            if (l.Count == 0) return I18n.T("Webs, carpetas, archivos o programas que la mascota te abre desde su men\u00FA. Tambi\u00E9n puedes soltarle una carpeta encima.", "Websites, folders, files, or apps opened from mascot menu. You can also drop a folder on it.");
            string names = l.Count == 1 ? I18n.Fmt("\u00AB{0}\u00BB", "\u201C{0}\u201D", l[0].Name) : l.Count == 2 ? I18n.Fmt("\u00AB{0}\u00BB y \u00AB{1}\u00BB", "\u201C{0}\u201D and \u201C{1}\u201D", l[0].Name, l[1].Name)
                         : I18n.Fmt("\u00AB{0}\u00BB, \u00AB{1}\u00BB y {2} m\u00E1s", "\u201C{0}\u201D, \u201C{1}\u201D and {2} more", l[0].Name, l[1].Name, l.Count - 2);
            return I18n.Fmt("{0} en el men\u00FA de la mascota.", "{0} in the mascot's menu.", names);
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
                MacButton less = new MacButton("\u2212", ButtonKind.Secondary, null, null, 28) { ToolTip = I18n.T("15 minutos antes", "15 minutes earlier") };
                MacButton more = new MacButton("+", ButtonKind.Secondary, null, null, 28) { ToolTip = I18n.T("15 minutos despu\u00E9s", "15 minutes later") };
                shown = Label(At(get()), Ds.Semibold, 14, pal.Label, new Thickness(12, 0, 12, 0));
                shown.MinWidth = 44;
                shown.TextAlignment = TextAlignment.Center;
                System.Windows.Automation.AutomationProperties.SetName(less, I18n.T("15 minutos antes", "15 minutes earlier"));
                System.Windows.Automation.AutomationProperties.SetName(more, I18n.T("15 minutos despu\u00E9s", "15 minutes later"));
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
                    System.Windows.Automation.AutomationProperties.SetItemStatus(c, on ? I18n.T("incluido", "included") : I18n.T("no incluido", "not included"));
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
