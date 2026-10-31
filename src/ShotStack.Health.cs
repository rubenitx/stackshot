// Stackshot - Health with the mascot: a daily water goal (the glasses done with it, "+1 vaso" and the water logged on
// the watch), the day's summary at the hour chosen, and, with a Pixel Watch or Fitbit connected, a few gentle words
// about steps and sleep (once each, never during a focus block, a call or a recording). A short night makes the eye
// and movement breaks come a little sooner that day. The watch is read every half hour, only while connected.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        Timer healthTimer;
        GoogleHealth.Today watch;
        bool watchReading;
        double nextWatchRead;
        const double WatchEveryMs = 30 * 60 * 1000;
        static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

        static string Today { get { return DateTime.Today.ToString("yyyy-MM-dd"); } }

        // 4.343 in Spanish, 4,343 in English.
        static string N0(int n) { return n.ToString("N0", I18n.IsSpanish ? Es : CultureInfo.InvariantCulture); }

        void StartHealth()
        {
            if (Test) return;
            watch = LastReading();
            if (watch != null && !watch.Empty && watch.ReadAt.Date == DateTime.Today) Logged(watch);
            nextWatchRead = Anim.Now + 60000;
            healthTimer = new Timer { Interval = 60000 };
            healthTimer.Tick += delegate
            {
                try { HealthTick(); }
                catch (Exception ex) { Log("Salud: " + ex.Message); }
            };
            healthTimer.Start();
        }

        void HealthTick()
        {
            if (exiting) return;
            if (GoogleHealth.Connected && !watchReading && Anim.Now >= nextWatchRead) ReadWatch(null);
            DaySummaryTick();
            WatchOfferTick();
        }

        // ---- Offering to connect a watch: once, a while after starting, by the mascot when it is free (or a notice);
        // "Luego" asks again three days later, "No tengo reloj" never again, and a connected watch is never asked about.

        double startedAt = Anim.Now;

        void WatchOfferTick()
        {
            if (!GoogleHealth.Available || GoogleHealth.Connected || GoogleHealth.Connecting || settings.FitbitAsked == "no") return;
            DateTime put;
            if (settings.FitbitAsked.Length > 0 && DateTime.TryParseExact(settings.FitbitAsked, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out put) && DateTime.Today < put.AddDays(3)) return;
            if (Anim.Now - startedAt < 10 * 60 * 1000) return;
            if (Native.IdleMs() >= Wellness.ActiveIdleMs || WellQuiet() || focus.On) return;
            if (pet != null && !pet.CanRemind) return;
            settings.FitbitAsked = Today;
            settings.Save();
            string text = I18n.T("\u00BFTienes un Pixel Watch o un Fitbit? Si lo conectas, te acompa\u00F1o con tus pasos, tu actividad y tu sue\u00F1o. Los datos van de Google a tu equipo.",
                                 "Have a Pixel Watch or a Fitbit? If you connect it, I can accompany you with your steps, activity and sleep. Data goes from Google directly to your PC.");
            if (pet != null) pet.WatchOffer(text, delegate { ConnectWatch(null); }, delegate { settings.FitbitAsked = "no"; settings.Save(); });
            else Notify(I18n.T("\u00BFConectamos tu reloj?", "Connect your smartwatch?"), text, NoticeKind.Info, I18n.T("Conectar", "Connect"), delegate { ConnectWatch(null); });
        }

        // ---- Water

        // Water is followed once the healthy breaks are on and there is a reminder to drink or a daily goal; otherwise
        // nothing offers or counts it.
        public bool WaterOn { get { return settings.WellOn && (settings.WellWater > 0 || settings.WaterGoal > 0); } }

        // Glasses today: the ones done with the mascot or "+1 vaso", or the water logged on the watch if that is more.
        public int WaterToday()
        {
            int app = Wellness.Today(settings)[WellKind.Water];
            int fromWatch = watch != null && watch.ReadAt.Date == DateTime.Today ? watch.WaterMl / 250 : 0;
            return Math.Max(app, fromWatch);
        }

        // A glass drunk: counted (a friendship point) and the water reminder starts over.
        public int WaterPlusOne(out string levelUp)
        {
            WellDone(WellKind.Water, out levelUp);
            return WaterToday();
        }

        // ---- The day's summary

        // "Hoy: 6 de 8 vasos de agua, 3 descansos de vista, 2 bloques de concentración (50 min), 5.400 pasos y 14 capturas."
        public string DaySummary()
        {
            // Only what the user wants in it (Mascota > Ajustes > Salud y actividad).
            int want = settings.SummaryParts;
            List<string> parts = new List<string>();
            int water = WaterToday();
            if ((want & Settings.SummaryWater) != 0 && WaterOn && (water > 0 || settings.WaterGoal > 0))
                parts.Add(settings.WaterGoal > 0 ? I18n.Fmt("{0} de {1} vasos de agua", "{0} of {1} glasses of water", water, settings.WaterGoal) : I18n.Fmt(water == 1 ? "{0} vaso de agua" : "{0} vasos de agua", water == 1 ? "{0} glass of water" : "{0} glasses of water", water));
            if ((want & Settings.SummaryBreaks) != 0 && settings.WellOn)
            {
                int[] done = Wellness.Today(settings);
                int eyes = done[WellKind.Eyes], move = done[WellKind.Move], breathe = done[WellKind.Breathe];
                if (eyes > 0) parts.Add(I18n.Fmt(eyes == 1 ? "{0} descanso de vista" : "{0} descansos de vista", eyes == 1 ? "{0} eye rest" : "{0} eye rests", eyes));
                if (move > 0) parts.Add(I18n.Fmt(move == 1 ? "{0} pausa para moverte" : "{0} pausas para moverte", move == 1 ? "{0} movement break" : "{0} movement breaks", move));
                if (breathe > 0) parts.Add(I18n.Fmt(breathe == 1 ? "{0} minuto respirando" : "{0} minutos respirando", breathe == 1 ? "{0} minute breathing" : "{0} minutes breathing", breathe));
            }
            FocusDayCheck();
            if ((want & Settings.SummaryFocus) != 0 && settings.FocusBlocks > 0)
                parts.Add(I18n.Fmt(settings.FocusBlocks == 1 ? "{0} bloque de concentraci\u00F3n ({1})" : "{0} bloques de concentraci\u00F3n ({1})", settings.FocusBlocks == 1 ? "{0} focus block ({1})" : "{0} focus blocks ({1})", settings.FocusBlocks, FocusClock.Minutes(settings.FocusMinutes)));
            if ((want & Settings.SummarySteps) != 0 && GoogleHealth.Connected && watch != null && watch.ReadAt.Date == DateTime.Today && watch.Steps > 0)
                parts.Add(watch.Steps.ToString("N0", I18n.IsSpanish ? Es : CultureInfo.InvariantCulture) + I18n.T(" pasos", " steps"));
            StatsDayCheck();
            if ((want & Settings.SummaryCaptures) != 0 && settings.StatsCaptures > 0)
                parts.Add(I18n.Fmt(settings.StatsCaptures == 1 ? "{0} captura" : "{0} capturas", settings.StatsCaptures == 1 ? "{0} capture" : "{0} captures", settings.StatsCaptures));
            if (parts.Count == 0) return null;
            string last = parts[parts.Count - 1];
            parts.RemoveAt(parts.Count - 1);
            return I18n.T("Hoy: ", "Today: ") + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) + I18n.T(" y ", " and ") + last : last) + ".";
        }

        double summaryWait;

        void DaySummaryTick()
        {
            if (settings.SummaryHour <= 0 || DateTime.Now.TimeOfDay < new TimeSpan(settings.SummaryHour, settings.SummaryMinute, 0) || settings.SummaryDay == Today) return;
            if (Native.IdleMs() >= Wellness.ActiveIdleMs || WellQuiet() || focus.Working) return;
            Teller teller = TellerFor(ref summaryWait, 120000);
            if (teller == Teller.Wait) return;
            string text = DaySummary();
            settings.SummaryDay = Today;
            settings.Save();
            if (text == null) return;
            if (teller == Teller.Pet) pet.HealthTell(I18n.T("\u00BFQu\u00E9 tal el d\u00EDa? ", "How was your day? ") + text, true);
            else Notify(I18n.T("Tu d\u00EDa", "Your day"), text);
        }

        // ---- Today's captures

        void StatsDayCheck()
        {
            if (settings.StatsDay == Today) return;
            settings.StatsDay = Today;
            settings.StatsCaptures = 0;
        }

        void CountCapture()
        {
            StatsDayCheck();
            settings.StatsCaptures++;
            settings.Save();
            // A busy day gets a word from the mascot, at a few round numbers.
            int n = settings.StatsCaptures;
            if (settings.MascotRhythm && PetShown && (n == 25 || n == 50 || n == 100)) pet.FocusSay(I18n.Fmt("\u00A1Vaya d\u00EDa! Ya llevas {0} capturas.", "What a day! You're already at {0} captures.", n));
        }

        // ---- The watch

        // Today's reading, with the steps goal chosen in the settings.
        public GoogleHealth.Today Watch
        {
            get
            {
                if (watch == null || watch.ReadAt.Date != DateTime.Today) return null;
                watch.StepsGoal = settings.StepsGoal;
                return watch;
            }
        }

        // Today's steps at each reading (to tell a walk from an hour sitting still); kept while the app runs.
        readonly List<KeyValuePair<DateTime, int>> stepLog = new List<KeyValuePair<DateTime, int>>();

        // "Hoy llevas 4.343 pasos (el 43 % de tu objetivo), anoche dormiste 7 h 10 min y llevas 12 min de actividad moderada o intensa."
        public string ActivityLine()
        {
            GoogleHealth.Today w = Watch;
            if (w == null) return WatchError != null ? I18n.T("No he podido leer tu reloj: ", "I could not read your watch: ") + WatchError
                                                     : I18n.T("Todav\u00EDa no he le\u00EDdo tu reloj hoy.", "I have not read your watch yet today.");
            if (w.Empty) return I18n.T("Google a\u00FAn no tiene datos de hoy. Abre la app de tu reloj en el m\u00F3vil para que se sincronice y vuelvo a mirarlo.",
                                       "Google has no data for today yet. Open your watch's app on your phone so it syncs, and I will check again.");
            int goal = w.StepsGoal;
            if (!I18n.IsSpanish)
            {
                string stepsEn = w.Steps >= goal ? "You've already walked " + N0(w.Steps) + " steps today: goal reached!"
                               : "You've walked " + N0(w.Steps) + " steps today, " + w.Steps * 100 / goal + "% of your " + N0(goal) + " goal.";
                List<string> moreEn = new List<string>();
                if (w.SleepMinutes > 0) moreEn.Add("you slept " + Hours(w.SleepMinutes) + " last night");
                if (w.ActiveMinutes > 0) moreEn.Add("you have " + w.ActiveMinutes + " min of moderate or vigorous activity");
                string extraEn = moreEn.Count == 0 ? "" : " " + char.ToUpper(moreEn[0][0]) + string.Join(" and ", moreEn.ToArray()).Substring(1) + ".";
                if (w.Steps < goal && DateTime.Now.Hour >= 12) extraEn += " " + N0(goal - w.Steps) + " to go: about " + WalkMinutes(goal - w.Steps) + " min of walking.";
                return stepsEn + extraEn;
            }
            string steps = w.Steps >= goal ? "\u00A1Hoy ya llevas " + w.Steps.ToString("N0", Es) + " pasos: objetivo cumplido!"
                         : "Hoy llevas " + w.Steps.ToString("N0", Es) + " pasos, el " + w.Steps * 100 / goal + " % de tu objetivo de " + goal.ToString("N0", Es) + ".";
            List<string> more = new List<string>();
            if (w.SleepMinutes > 0) more.Add("anoche dormiste " + Hours(w.SleepMinutes));
            if (w.ActiveMinutes > 0) more.Add("llevas " + w.ActiveMinutes + " min de actividad moderada o intensa");
            string extra = more.Count == 0 ? "" : " " + char.ToUpper(more[0][0]) + string.Join(" y ", more.ToArray()).Substring(1) + ".";
            if (w.Steps < goal && DateTime.Now.Hour >= 12) extra += " Te faltan " + (goal - w.Steps).ToString("N0", Es) + ": unos " + WalkMinutes(goal - w.Steps) + " min andando.";
            return steps + extra;
        }

        // About a hundred steps a minute at an easy pace.
        static int WalkMinutes(int steps) { return Math.Max(5, (int)Math.Ceiling(steps / 100.0 / 5) * 5); }

        // "Voy a dar un paseo": the watch is read again in twenty minutes, to tell how it went.
        public void WalkStarted()
        {
            nextWatchRead = Math.Min(nextWatchRead, Anim.Now + 20 * 60 * 1000);
        }

        static string Hours(int minutes) { return minutes / 60 + " h" + (minutes % 60 > 0 ? " " + minutes % 60 + " min" : ""); }

        public bool WatchReading { get { return watchReading; } }

        // Why the last reading failed (null when it worked).
        public string WatchError { get; private set; }

        // A reading has finished, well or not (the windows showing the numbers follow it).
        public event Action WatchRead;

        // Read now (asked from the settings) or on the half-hour; done runs here with the error, or null.
        public void ReadWatch(Action<string> done) { ReadWatch(done, false); }

        // quiet: the mascot says nothing about it on its own (whoever asked tells it).
        public void ReadWatch(Action<string> done, bool quiet)
        {
            if (watchReading) return;
            // Asked again from the mascot's menu right after a reading: that one is fresh enough, no three new requests.
            GoogleHealth.Today last = Watch;
            if (quiet && done != null && last != null && !last.Empty && (DateTime.Now - last.ReadAt).TotalMinutes < 2)
            {
                Ui(delegate { done(null); });
                return;
            }
            watchReading = true;
            nextWatchRead = Anim.Now + WatchEveryMs;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                GoogleHealth.Today t = null;
                try { t = GoogleHealth.Read(out error); }
                catch (Exception ex) { error = ex.Message; }
                Ui(delegate
                {
                    watchReading = false;
                    WatchError = t != null ? null : error;
                    if (t != null)
                    {
                        if (!t.Empty) Logged(t);
                        // An empty answer after a good one today (Google catching up) keeps the good one.
                        if (!(t.Empty && Watch != null && !Watch.Empty))
                        {
                            watch = t;
                            settings.FitbitLast = t.Steps + "|" + t.StepsGoal + "|" + t.SleepMinutes + "|" + t.ActiveMinutes + "|" + t.WaterMl + "|" + t.ReadAt.Ticks + "|" + (t.Empty ? "vacio" : "");
                            settings.Save();
                        }
                        Log(t.Empty ? "Google Health: le\u00EDdo, Google a\u00FAn no tiene pasos de hoy"
                                    : "Google Health: le\u00EDdo, " + t.Steps + " pasos, " + t.SleepMinutes + " min de sue\u00F1o, " + t.ActiveMinutes + " min activo");
                        if (!t.Empty) WatchMoved();
                        if (!quiet) WatchSays();
                    }
                    else Log("Google Health: " + error);
                    if (done != null) done(error);
                    Action h = WatchRead;
                    if (h != null) h();
                });
            });
        }

        GoogleHealth.Today LastReading()
        {
            string[] p = (settings.FitbitLast ?? "").Split('|');
            long ticks;
            if (p.Length < 6 || !long.TryParse(p[5], out ticks)) return null;
            GoogleHealth.Today t = new GoogleHealth.Today { ReadAt = new DateTime(ticks) };
            int.TryParse(p[0], out t.Steps); int.TryParse(p[1], out t.StepsGoal); int.TryParse(p[2], out t.SleepMinutes);
            int.TryParse(p[3], out t.ActiveMinutes); int.TryParse(p[4], out t.WaterMl);
            t.Empty = p.Length > 6 && p[6] == "vacio";
            return t;
        }

        public void ConnectWatch(Action changed)
        {
            GoogleHealth.Connect(delegate(string error)
            {
                Ui(delegate
                {
                    if (error == GoogleHealth.Cancelled) { }
                    else if (error != null) Notify(I18n.T("No se ha conectado el reloj", "Watch could not connect"), error);
                    else
                    {
                        Notify(I18n.T("Reloj conectado", "Watch connected"),
                               I18n.T("Stackshot leer\u00E1 tus pasos, tu actividad y tu sue\u00F1o cada media hora. Los datos van de Google a tu equipo.",
                                      "Stackshot will read your steps, activity and sleep every half hour. Data goes from Google directly to your PC."));
                        ReadWatch(delegate { if (changed != null) changed(); });
                    }
                    if (changed != null) changed();
                });
            });
        }

        public void DisconnectWatch()
        {
            GoogleHealth.Disconnect();
            watch = null;
            settings.FitbitLast = "";
            settings.Save();
            Log("Google Health: desconectado");
        }

        // Keeps today's readings (a new day starts the list over).
        void Logged(GoogleHealth.Today t)
        {
            stepLog.RemoveAll(delegate(KeyValuePair<DateTime, int> e) { return e.Key.Date != t.ReadAt.Date || e.Value > t.Steps; });
            stepLog.Add(new KeyValuePair<DateTime, int>(t.ReadAt, t.Steps));
            if (stepLog.Count > 64) stepLog.RemoveAt(0);
        }

        // Steps since the latest reading at least `minutes` old (and how long ago that was), or -1 without one.
        int StepsSince(double minutes, out double ago)
        {
            ago = 0;
            if (stepLog.Count < 2) return -1;
            KeyValuePair<DateTime, int> last = stepLog[stepLog.Count - 1];
            for (int i = stepLog.Count - 2; i >= 0; i--)
            {
                double m = (last.Key - stepLog[i].Key).TotalMinutes;
                if (m >= minutes) { ago = m; return last.Value - stepLog[i].Value; }
            }
            return -1;
        }

        // Walking since the last reading counts as the "Moverse" break: its timer starts over, so it doesn't ask you to
        // move right after a walk.
        void WatchMoved()
        {
            double ago;
            int walked = StepsSince(1, out ago);
            if (walked >= 250 && ago <= 50) wellness.Done(WellKind.Move, Anim.Now);
        }

        // A few words about the numbers, each once a day (or once every few hours): never in a block, a call, a recording
        // or with nobody there, and only through the mascot (no notices for this). Big news in a card, small in a bubble.
        void WatchSays()
        {
            GoogleHealth.Today w = Watch;
            if (!settings.HealthTalk || w == null || w.Empty || pet == null || !pet.CanRemind) return;
            if (Native.IdleMs() >= Wellness.ActiveIdleMs || WellQuiet() || focus.Working) return;
            DateTime now = DateTime.Now;
            int goal = w.StepsGoal;
            double ago;
            // Last night, in the morning.
            if (w.SleepMinutes > 0 && now.Hour >= 8 && now.Hour < 13 && Said("sueno"))
            {
                if (w.SleepMinutes < 360)
                {
                    settings.ShortSleepDay = Today;
                    settings.Save();
                    pet.HealthTell(I18n.Fmt("Esta noche has dormido {0}. Hoy te propondr\u00E9 las pausas para la vista y para moverte un poco antes.",
                                            "You slept {0} last night. Today I will suggest the eye and movement breaks a little earlier.", Hours(w.SleepMinutes)), false);
                }
                else if (w.SleepMinutes >= 450) pet.HealthTell(I18n.Fmt("Has dormido {0}. \u00A1Buen d\u00EDa para concentrarse!", "You slept {0}. A good day to focus!", Hours(w.SleepMinutes)), false);
                else pet.FocusSay(I18n.Fmt("Anoche dormiste {0}. \u00A1A por el d\u00EDa!", "You slept {0} last night. Let's go!", Hours(w.SleepMinutes)));
                return;
            }
            // The goal: a card, a cheer and a friendship point.
            if (w.Steps >= goal && Said("meta"))
            {
                string up = LoveUp();
                pet.HealthTell(I18n.Fmt("\u00A1{0} pasos! Has cumplido tu objetivo de hoy.", "{0} steps! You reached today's goal.", N0(w.Steps)) + (up != null ? " " + up : ""), true);
                return;
            }
            // A good walk since the last reading.
            int walked = StepsSince(1, out ago);
            if (walked >= 1000 && ago <= 50 && Said("paseo" + now.Hour / 3))
            {
                pet.FocusSay(I18n.Fmt("\u00A1Buen paseo! {0} pasos m\u00E1s en {1} min.", "Nice walk! {0} more steps in {1} min.", N0(walked), (int)Math.Round(ago)));
                return;
            }
            // Halfway there.
            if (w.Steps >= goal / 2 && w.Steps < goal && Said("mitad"))
            {
                pet.FocusSay(I18n.Fmt("\u00A1Ya vas por la mitad! {0} de {1} pasos.", "Halfway there! {0} of {1} steps.", N0(w.Steps), N0(goal)));
                return;
            }
            // An hour sitting still during the working day: a nudge (it stands for the "Moverse" break). Not a single step means
            // the phone hasn't synced the watch yet, not that you sat still: then it says nothing.
            int still = StepsSince(55, out ago);
            if (now.Hour >= 9 && now.Hour < 19 && still > 0 && still < 150 && ago <= 120 && Said("quieto" + now.Hour / 2))
            {
                if (settings.WellOn) WellLater(WellKind.Move, 40 * 60 * 1000);
                pet.HealthAskMove(I18n.IsSpanish
                    ? "En " + (ago >= 90 ? "la \u00FAltima hora y media" : "la \u00FAltima hora") + " solo has dado " + still + " pasos. \u00BFEstiramos las piernas un par de minutos?"
                    : "In the last " + (ago >= 90 ? "hour and a half" : "hour") + " you have only taken " + still + " steps. Shall we stretch our legs for a couple of minutes?");
                return;
            }
            // A slow morning: before lunch, up for a glass of water and a short round.
            if (now.Hour >= 11 && now.Hour < 13 && w.Steps < goal / 5 && Said("manana"))
            {
                if (settings.WellOn) WellLater(WellKind.Move, 40 * 60 * 1000);
                pet.HealthAskMove(I18n.Fmt("Esta ma\u00F1ana llevas {0} pasos. \u00BFTe levantas a por un vaso de agua y das una vuelta corta?",
                                           "{0} steps so far this morning. How about getting up for a glass of water and a short walk?", N0(w.Steps)));
                return;
            }
            // After lunch, hardly a step yet: a few minutes walking clear the head.
            int sinceLunch = StepsSince(60, out ago);
            if (now.Hour >= 14 && now.Hour < 16 && sinceLunch >= 0 && sinceLunch < 300 && ago <= 120 && Said("comida"))
            {
                pet.HealthAskWalk(I18n.T("Despu\u00E9s de comer sienta muy bien caminar: un paseo de diez minutos y vuelves con la cabeza despejada.",
                                         "A walk after lunch feels great: ten minutes out and you come back with a clear head."));
                return;
            }
            // Active minutes: half an hour, then a whole one.
            if (w.ActiveMinutes >= 60 && Said("activo60"))
            {
                pet.FocusSay(I18n.T("\u00A1Una hora activa hoy! Tu cuerpo te lo agradece.", "An active hour today! Your body thanks you."));
                return;
            }
            if (w.ActiveMinutes >= 30 && w.ActiveMinutes < 60 && Said("activo30"))
            {
                pet.FocusSay(I18n.Fmt("Ya llevas {0} minutos activos hoy. \u00A1As\u00ED se hace!", "{0} active minutes today already. Way to go!", w.ActiveMinutes));
                return;
            }
            // Late in the day and close to the goal: how far it is, and a walk to get there.
            if (now.Hour >= 16 && now.Hour < 20 && w.Steps >= goal * 6 / 10 && w.Steps < goal && Said("falta"))
            {
                int left = goal - w.Steps;
                pet.HealthAskWalk(I18n.Fmt("Te faltan {0} pasos para tu objetivo: un paseo de unos {1} minutos y lo tienes.",
                                           "{0} steps to your goal: a walk of about {1} minutes and you're there.", N0(left), WalkMinutes(left)));
                return;
            }
            // After a short night: early to bed tonight.
            if (settings.ShortSleepDay == Today && now.Hour >= 21 && Said("acostarse"))
            {
                pet.FocusSay(I18n.T("Anoche dormiste poco: hoy intenta acostarte un poco antes. \u00A1Ma\u00F1ana lo notar\u00E1s!",
                                    "You slept little last night: try to go to bed a bit earlier today. You'll feel it tomorrow!"));
                return;
            }
            // How it is going, now and then through the working day.
            if (now.Hour >= 10 && now.Hour < 19 && w.Steps > 0 && w.Steps < goal && Said("dato" + now.Hour / 3))
                pet.FocusSay(I18n.IsSpanish ? "Llevas " + w.Steps.ToString("N0", Es) + " pasos, el " + (w.Steps * 100 / Math.Max(1, goal)) + " % de tu objetivo de hoy."
                                            : N0(w.Steps) + " steps so far, " + (w.Steps * 100 / Math.Max(1, goal)) + "% of today's goal.");
        }

        // True the first time this is said today (and remembered); false if it was already said.
        bool Said(string key)
        {
            string[] p = (settings.HealthSaid ?? "").Split('|');
            List<string> keys = p.Length == 2 && p[0] == Today ? new List<string>(p[1].Split(',')) : new List<string>();
            if (keys.Contains(key)) return false;
            keys.Add(key);
            settings.HealthSaid = Today + "|" + string.Join(",", keys.ToArray());
            settings.Save();
            return true;
        }

        // The minutes between healthy breaks today: after a short night, eyes and movement come a quarter sooner; with
        // the day's water goal reached, water is no longer reminded.
        int[] WellMinutes()
        {
            int[] m = (int[])Wellness.MinutesOf(settings).Clone();
            if (settings.WaterGoal > 0 && WaterToday() >= settings.WaterGoal) m[WellKind.Water] = 0;
            if (settings.ShortSleepDay == Today)
                foreach (int k in new int[] { WellKind.Eyes, WellKind.Move })
                    if (m[k] > 0) m[k] = Math.Max(15, m[k] * 3 / 4);
            return m;
        }
    }
}
