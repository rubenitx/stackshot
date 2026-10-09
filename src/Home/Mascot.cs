// Stackshot - The mascot: a customizable little character that floats, follows the mouse and reacts.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Drawn in code with simple physics: floats, blinks, follows the mouse (even outside its window), fidgets when
    // idle, yawns and falls asleep when ignored, and gets dizzy when poked too much. The owner feeds it the time and
    // mouse each frame; the look (character, colors, clothes) comes from MascotLook.
    public class Mascot
    {
        public enum Mood { Idle, Happy, Surprised, Sleep, Dizzy, Wink, Love, Yawn }

        static readonly Color EyeCore = Color.FromArgb(236, 254, 255);
        static readonly Color EyeGlow = Color.FromArgb(103, 232, 249);
        static readonly Color LoveColor = Color.FromArgb(255, 92, 140);
        static readonly Color Ink = Color.FromArgb(36, 32, 56);

        class Particle
        {
            public char Kind;          // h heart, s star, z sleep, p spark, b bat
            public double X, Y, Vx, Vy, Age, Life, Rot, Vr, Size;
            public int Tint;
        }

        public MascotLook Look = new MascotLook();
        public string Bubble;          // current speech bubble (null = none)
        public double BubbleAlpha;     // bubble fade in/out
        public RectangleF Box;         // where it is drawn (set by the owner)
        public bool ShowShadow = true;
        public int Energy = 1;          // 0 calm, 1 normal, 2 lively: how often it does things alone
        public int Chatter = 1;         // 0 stays quiet while busy, 1 sometimes comments, 2 always does
        public int Alone = MascotCmd.AloneAll;   // what it may do alone (MascotCmd.Alone* bits)
        public bool SleepIdle = true;   // dozes off when ignored
        public double Facing;          // -1..1: leans that way (the desktop pet walking)
        public bool Dangling;          // held by the mouse
        public double Pull;            // 0..1: stretched along its way (dragged or thrown)
        public bool Busy;              // walking, falling or showing a menu: no idle activities
        public bool Talks;             // may comment on what it is doing

        readonly Random rnd = new Random();
        readonly List<Particle> parts = new List<Particle>();
        readonly MascotPose pose = new MascotPose();
        Mood mood = Mood.Idle;
        double now, last, moodUntil, bubbleUntil, nextBlink, blinkStart = -1, waveUntil, lastPoke, lastSeen, lastSpark, nextFidget;
        double lookX, lookY, tilt, tiltV, antenna, antennaV, squashX = 1, squashXV, squashY = 1, squashYV, jump, jumpV;
        double earL, earLV, earR, earRV, spin, spinV, appear = 1, sweepUntil, danceUntil;
        double bowUntil, bubblesUntil, nextHop, pendingSleep, lastBubble;
        int hopsLeft;
        int lastAlone = -1, prevAlone = -1;
        public Func<double> TestClock;   // deterministic clock for tests (null: the real one)
        int pokes;
        bool hovering, pendingHappy, forcedSleep, wasSleep;
        int act, sleepVar;
        double actStart, actUntil, stretch, actKNow, actTNow;

        double Clock() { return TestClock != null ? TestClock() : Anim.Now; }
        public int LastAlone { get { return lastAlone; } }

        public Mascot()
        {
            nextBlink = 1800;
            nextFidget = 9000;
        }

        public Mood Current { get { return mood; } }
        public bool Sleeping { get { return mood == Mood.Sleep; } }

        // Doing more than floating (owners raise the frame rate only then).
        public bool Lively
        {
            get
            {
                return parts.Count > 0 || (mood != Mood.Idle && mood != Mood.Sleep) || blinkStart >= 0 || now < waveUntil || now < danceUntil || now < bowUntil || now < bubblesUntil || hopsLeft > 0 || pendingSleep > 0 ||
                       Math.Abs(jumpV) > 0.15 || Math.Abs(squashXV) > 0.15 || Math.Abs(squashYV) > 0.15 || Math.Abs(antennaV) > 60 ||
                       act != MascotProps.None || Math.Abs(earLV) > 40 || appear < 0.99 || (BubbleAlpha > 0.02 && BubbleAlpha < 0.98) || Dangling;
            }
        }

        double SleepAfter { get { return !SleepIdle ? 1e12 : Look.Personality == 1 ? 25000 : Look.Personality == 2 ? 90000 : 45000; } }
        double FidgetScale { get { return (Look.Personality == 1 ? 1.8 : Look.Personality == 2 ? 0.55 : 1.0) * (Energy == 0 ? 2.2 : Energy == 2 ? 0.5 : 1.0); } }

        public void Say(string text, double ms)
        {
            now = Clock();
            Bubble = text;
            bubbleUntil = now + ms;
        }

        void SetMood(Mood m, double ms)
        {
            now = Clock();
            mood = m;
            moodUntil = now + ms;
        }

        // Pops in with a bounce.
        public void PopIn()
        {
            appear = 0.01;
            squashX = 0.6; squashY = 1.35;
            jumpV = -2.2;
        }

        public void Greet(string text)
        {
            now = Clock();
            Wake();
            SetMood(Mood.Happy, 2200);
            waveUntil = now + 1800;
            jumpV -= 1.2;
            if (text != null) Say(text, 4200);
        }

        public void Celebrate(string text)
        {
            now = Clock();
            Wake();
            SetMood(Mood.Surprised, 420);
            squashX = 1.18; squashY = 0.84;
            jumpV -= 1.8;
            for (int i = 0; i < 10; i++) Spawn('p', 0, -0.05, 0.9);
            if (DateTime.Now.Month == 10) for (int i = 0; i < 3; i++) Spawn('b', 0, -0.2, 0.55);
            if (text != null) Say(text, 2600);
            pendingHappy = true;
        }

        // Each poke does something different; too many in a row make it dizzy.
        public void Poke(string[] lines)
        {
            now = Clock();
            Wake();
            pokes = now - lastPoke < 1200 ? pokes + 1 : 1;
            lastPoke = now;
            squashX = 1.22; squashY = 0.8;
            antennaV += 900;
            earLV += 500; earRV -= 500;
            if (pokes >= 4)
            {
                pokes = 0;
                SetMood(Mood.Dizzy, 2600);
                spinV = 900;
                for (int i = 0; i < 6; i++) Spawn('s', 0, -0.32, 0.6);
                Say("\u00A1Qu\u00E9 mareo! \u00BFPor qu\u00E9 hay tres ratones?", 2600);
                return;
            }
            switch (rnd.Next(6))
            {
                case 0: jumpV -= 2.6; SetMood(Mood.Happy, 1400); break;
                case 4: Dance(); break;
                case 5: Twirl(); break;
                case 1:
                    SetMood(Mood.Love, 1800);
                    for (int i = 0; i < 5; i++) Spawn('h', 0, -0.2, 0.7);
                    break;
                case 2: SetMood(Mood.Wink, 1200); tiltV += 140; break;
                default: SetMood(Mood.Surprised, 700); jumpV -= 1.4; break;
            }
            if (lines != null && lines.Length > 0) Say(lines[rnd.Next(lines.Length)], 2600);
        }

        public void Hover(bool on)
        {
            if (on == hovering) return;
            hovering = on;
            if (on) { Wake(); antennaV += 260; earLV -= 200; earRV += 200; }
        }

        public void Hop(double strength)
        {
            jumpV -= strength;
            squashX = 1.12; squashY = 0.9;
            earLV += 250 * strength; earRV -= 250 * strength;
        }

        // A little wobble after a toss: ears and antenna swing, optionally a smile.
        public void Wobble(double k, bool smile)
        {
            now = Clock();
            tiltV += 160 * k;
            earLV -= 400 * k; earRV += 400 * k;
            antennaV -= 600 * k;
            if (smile) SetMood(Mood.Happy, 1300);
        }

        public void Land(double impact)
        {
            squashX = 1 + 0.25 * impact; squashY = 1 - 0.22 * impact;
            earLV += 600 * impact; earRV -= 600 * impact;
            antennaV += 500 * impact;
        }

        public void SleepNow()
        {
            forcedSleep = true;
            if (mood != Mood.Sleep) { mood = Mood.Sleep; Bubble = null; }
        }

        public void WakeUp()
        {
            forcedSleep = false;
            Wake();
        }

        public int Activity { get { return act; } }

        // Idle activity with props (coffee, juggling, handheld console, stretching, reading, photos).
        public void StartAct(int kind)
        {
            now = Clock();
            if (mood != Mood.Idle || Dangling || Busy) return;
            act = kind;
            actStart = now;
            actUntil = now + MascotProps.Duration(kind);
            if (kind == MascotProps.Stretch) SetMood(Mood.Yawn, 1900);
            if (Talks && Chatter > 0) { string line = MascotTalk.ForAct(Look.Personality, kind); if (line != null) Say(line, 3200); }
        }

        void Wake()
        {
            now = Clock();
            act = MascotProps.None;
            lastSeen = now;
            forcedSleep = false;
            pendingSleep = 0;
            if (mood == Mood.Sleep || mood == Mood.Yawn) { mood = Mood.Idle; squashY = 1.12; jumpV -= 1; }
        }

        void Spawn(char kind, double x, double y, double speed)
        {
            Particle p = new Particle();
            p.Kind = kind;
            double a = -Math.PI / 2 + (rnd.NextDouble() - 0.5) * (kind == 'p' ? Math.PI * 2 : kind == 'c' ? 2.6 : 1.6);
            double v = speed * (0.55 + rnd.NextDouble() * 0.6);
            p.X = x + (rnd.NextDouble() - 0.5) * 0.2;
            p.Y = y;
            p.Vx = Math.Cos(a) * v;
            p.Vy = Math.Sin(a) * v;
            p.Life = kind == 'z' ? 2200 : kind == 'p' ? 700 : kind == 'b' ? 1600 : kind == 'o' ? 2800 : kind == 'c' ? 1900 : 1300;
            p.Tint = rnd.Next(4);
            p.Rot = rnd.NextDouble() * 360;
            p.Vr = (rnd.NextDouble() - 0.5) * 360;
            p.Size = kind == 'p' ? 0.035 + rnd.NextDouble() * 0.03 : kind == 'b' ? 0.09 : kind == 'c' ? 0.045 : kind == 'o' ? 0.05 + rnd.NextDouble() * 0.05 : 0.07 + rnd.NextDouble() * 0.04;
            parts.Add(p);
        }

        // A little dance: sways to the beat, bounces and gives off music notes.
        public void Dance()
        {
            now = Clock();
            Wake();
            danceUntil = now + 2000;
            SetMood(Mood.Happy, 2000);
        }

        // A full pirouette with a burst of sparkles.
        public void Twirl()
        {
            now = Clock();
            Wake();
            spinV = 1100;
            jumpV -= 1.6;
            SetMood(Mood.Wink, 900);
            for (int i = 0; i < 8; i++) Spawn('p', 0, -0.1, 0.8);
        }

        // Unprompted behavior: a weighted pick among every command its Alone categories allow (never the last two again),
        // run exactly like a command from the user, only without counting as attention (so it still dozes off).
        public const int AloneSweep = 100, AloneWink = 101, AloneWiggle = 102;

        public static int PickAlone(int alone, int personality, int last, int prev, Random r)
        {
            int[] ids = new int[24];
            double[] w = new double[24];
            int n = 0;
            double tot = 0;
            for (int pass = 0; pass < 2 && n == 0; pass++)
            {
                if ((alone & MascotCmd.AloneSmall) != 0)
                {
                    Add(ids, w, ref n, MascotCmd.Jump, 3, last, prev, pass); Add(ids, w, ref n, MascotCmd.Love, 1.5, last, prev, pass);
                    Add(ids, w, ref n, MascotCmd.Cheer, 1, last, prev, pass); Add(ids, w, ref n, AloneWink, 1.5, last, prev, pass); Add(ids, w, ref n, AloneWiggle, 1.5, last, prev, pass);
                }
                if ((alone & MascotCmd.AloneDance) != 0)
                {
                    Add(ids, w, ref n, MascotCmd.Dance, 3, last, prev, pass); Add(ids, w, ref n, MascotCmd.Twirl, 2, last, prev, pass);
                    Add(ids, w, ref n, MascotCmd.Flip, 1.5, last, prev, pass); Add(ids, w, ref n, MascotCmd.Bubbles, 2, last, prev, pass);
                }
                if ((alone & MascotCmd.AloneProps) != 0)
                {
                    Add(ids, w, ref n, MascotCmd.Juggle, personality == 2 ? 4 : 2, last, prev, pass); Add(ids, w, ref n, MascotCmd.Coffee, personality == 1 ? 4 : 2, last, prev, pass);
                    Add(ids, w, ref n, MascotCmd.Read, personality == 1 ? 4 : 2, last, prev, pass); Add(ids, w, ref n, MascotCmd.Console, personality == 2 ? 4 : 2, last, prev, pass);
                    Add(ids, w, ref n, MascotCmd.Photo, 1.5, last, prev, pass); Add(ids, w, ref n, MascotCmd.Stretch, 1.5, last, prev, pass);
                    Add(ids, w, ref n, MascotCmd.Play, 1.5, last, prev, pass);
                }
                if ((alone & MascotCmd.AloneLook) != 0)
                {
                    Add(ids, w, ref n, MascotCmd.Wave, 3, last, prev, pass); Add(ids, w, ref n, MascotCmd.Bow, 1.5, last, prev, pass); Add(ids, w, ref n, AloneSweep, 2, last, prev, pass);
                }
            }
            if (n == 0) return -1;
            for (int i = 0; i < n; i++) tot += w[i];
            double x = r.NextDouble() * tot;
            for (int i = 0; i < n; i++) { x -= w[i]; if (x < 0) return ids[i]; }
            return ids[n - 1];
        }

        // Pass 0 skips the two most recent picks; pass 1 (only when nothing else is allowed) takes anything.
        static void Add(int[] ids, double[] w, ref int n, int id, double weight, int last, int prev, int pass)
        {
            if (pass == 0 && (id == last || id == prev)) return;
            ids[n] = id; w[n] = weight; n++;
        }

        // Nothing else is going on: no mood, prop, dance, wave, bow, bubbles, hops, spin or pending nap, and nobody holds it.
        public bool AloneFree
        {
            get
            {
                return mood == Mood.Idle && act == MascotProps.None && !Busy && !Dangling && !forcedSleep && pendingSleep == 0 && hopsLeft == 0 &&
                       now >= danceUntil && now >= waveUntil && now >= bowUntil && now >= bubblesUntil && now >= sweepUntil && Math.Abs(spinV) < 30 && !pendingHappy;
            }
        }

        void Fidget()
        {
            int cmd = PickAlone(Alone, Look.Personality, lastAlone, prevAlone, rnd);
            if (cmd < 0) return;
            prevAlone = lastAlone;
            lastAlone = cmd;
            switch (cmd)
            {
                case AloneSweep: sweepUntil = now + 2200; break;
                case AloneWink: SetMood(Mood.Wink, 700); break;
                case AloneWiggle: squashX = 0.86; squashY = 1.18; earLV += 300; earRV -= 300; break;
                default: Run(cmd, false); break;
            }
            if (cmd < 100 && Talks && Chatter > 0 && rnd.NextDouble() < (Chatter == 2 ? 0.8 : 0.3)) SayFor(cmd);
        }

        void SayFor(int cmd)
        {
            int prop = cmd == MascotCmd.Coffee ? MascotProps.Coffee : cmd == MascotCmd.Juggle ? MascotProps.Juggle : cmd == MascotCmd.Console ? MascotProps.Console :
                       cmd == MascotCmd.Stretch ? MascotProps.Stretch : cmd == MascotCmd.Read ? MascotProps.Read : cmd == MascotCmd.Photo ? MascotProps.Photo : 0;
            string line = prop != 0 ? MascotTalk.ForAct(Look.Personality, prop) : MascotTalk.ForCmd(Look.Personality, cmd);
            if (line != null) Say(line, 3000);
        }

        void Flip()
        {
            spinV = -1250;
            jumpV -= 2.3;
            SetMood(Mood.Surprised, 520);
            for (int i = 0; i < 6; i++) Spawn('p', 0, -0.05, 0.8);
        }

        void ForceAct(int kind)
        {
            act = kind;
            actStart = now;
            actUntil = now + MascotProps.Duration(kind);
            if (kind == MascotProps.Stretch) SetMood(Mood.Yawn, 1900);
        }

        // Does what it was asked, whatever it was doing (or asleep): the chips, the page and the desktop menu come through here.
        public void Perform(int cmd)
        {
            now = Clock();
            if (cmd == MascotCmd.Sleep)
            {
                if (mood == Mood.Sleep)
                {
                    WakeUp();
                    Hop(1.2);
                    if (Talks) Say(MascotTalk.ForCmd(Look.Personality, -1), 2400);
                    return;
                }
                Wake();
                act = MascotProps.None;
                SetMood(Mood.Yawn, 1500);
                pendingSleep = now + 1500;
                if (Talks) Say(MascotTalk.ForCmd(Look.Personality, cmd), 1800);
                return;
            }
            Wake();
            Run(cmd, true);
        }

        // Starts a command (not Sleep) from a clean slate.
        void Run(int cmd, bool asked)
        {
            act = MascotProps.None;
            mood = Mood.Idle;
            pendingHappy = false;
            danceUntil = waveUntil = sweepUntil = bowUntil = bubblesUntil = 0;
            hopsLeft = 0;
            switch (cmd)
            {
                case MascotCmd.Play:
                {
                    double r = rnd.NextDouble();
                    if (r < 0.4) ForceAct(MascotProps.Juggle);
                    else if (r < 0.7) ForceAct(MascotProps.Console);
                    else { bubblesUntil = now + 4000; hopsLeft = 3; nextHop = now; SetMood(Mood.Happy, 3200); }
                    break;
                }
                case MascotCmd.Juggle: ForceAct(MascotProps.Juggle); break;
                case MascotCmd.Coffee: ForceAct(MascotProps.Coffee); break;
                case MascotCmd.Read: ForceAct(MascotProps.Read); break;
                case MascotCmd.Console: ForceAct(MascotProps.Console); break;
                case MascotCmd.Photo: ForceAct(MascotProps.Photo); break;
                case MascotCmd.Stretch: ForceAct(MascotProps.Stretch); break;
                case MascotCmd.Dance: danceUntil = now + 3600; SetMood(Mood.Happy, 3600); break;
                case MascotCmd.Jump: hopsLeft = 3; nextHop = now; SetMood(Mood.Happy, 1900); break;
                case MascotCmd.Twirl: Twirl(); break;
                case MascotCmd.Flip: Flip(); break;
                case MascotCmd.Wave: waveUntil = now + 2600; SetMood(Mood.Happy, 2400); jumpV -= 0.8; break;
                case MascotCmd.Bow: bowUntil = now + 1500; SetMood(Mood.Happy, 1500); break;
                case MascotCmd.Cheer:
                    waveUntil = now + 2000;
                    jumpV -= 2.4;
                    SetMood(Mood.Happy, 2200);
                    for (int i = 0; i < 16; i++) Spawn('c', 0, -0.1, 0.9);
                    for (int i = 0; i < 4; i++) Spawn('s', 0, -0.1, 0.6);
                    break;
                case MascotCmd.Love:
                    SetMood(Mood.Love, 2800);
                    tiltV += 160;
                    for (int i = 0; i < 6; i++) Spawn('h', 0, -0.2, 0.7);
                    break;
                case MascotCmd.Bubbles: bubblesUntil = now + 4200; SetMood(Mood.Happy, 2400); break;
            }
            if (asked && Talks) SayFor(cmd);
        }

        // Advances the animation. mouse: cursor in the owner's coordinates; moved: whether it moved.
        public void Step(double t, PointF mouse, bool moved)
        {
            now = t;
            double dt = Math.Min(0.05, Math.Max(0.001, (t - last) / 1000.0));
            if (last == 0) dt = 0.016;
            last = t;
            if (lastSeen == 0) lastSeen = t;
            if (moved) lastSeen = t;
            if (moved && mood == Mood.Sleep && !forcedSleep) { Wake(); Hop(0.6); }   // dozed off from being ignored: wakes when someone is back

            if (pendingSleep > 0 && now >= pendingSleep) { pendingSleep = 0; forcedSleep = true; }
            if (mood != Mood.Idle && mood != Mood.Sleep && now > moodUntil) mood = Mood.Idle;
            if (pendingHappy && mood == Mood.Idle) { pendingHappy = false; SetMood(Mood.Happy, 1500); }
            if (mood == Mood.Idle && !forcedSleep && now - lastSeen > SleepAfter - 2200 && now - lastSeen < SleepAfter) SetMood(Mood.Yawn, 1900);
            if ((mood == Mood.Idle || mood == Mood.Yawn) && (forcedSleep || (act == MascotProps.None && now - lastSeen > SleepAfter))) { mood = Mood.Sleep; Bubble = null; }
            if (mood == Mood.Idle && now > nextFidget)
            {
                // Yields to anything in progress or to someone moving around it; asks again soon instead of waiting a full interval.
                if (!AloneFree || now - lastSeen < (Energy == 2 ? 600 : 1500)) nextFidget = now + 1200;
                else { nextFidget = now + (7000 + rnd.NextDouble() * 9000) * FidgetScale; Fidget(); }
            }
            if (act != MascotProps.None && (now > actUntil || Busy || Dangling || (mood != Mood.Idle && mood != Mood.Yawn))) act = MascotProps.None;
            actTNow = now - actStart;
            actKNow = act == MascotProps.None ? 0 : Math.Max(0, Math.Min(1, Math.Min(actTNow / 350.0, (actUntil - now) / 350.0)));
            stretch = act == MascotProps.Stretch ? MascotProps.StretchAmt(actTNow) * actKNow : 0;
            if (mood == Mood.Sleep && !wasSleep) sleepVar = rnd.Next(5);
            wasSleep = mood == Mood.Sleep;
            if (Bubble != null && now > bubbleUntil) Bubble = null;
            BubbleAlpha += ((Bubble != null ? 1 : 0) - BubbleAlpha) * (1 - Math.Exp(-dt / 0.09));
            if (Bubble == null && BubbleAlpha < 0.02) BubbleAlpha = 0;

            // Gaze follows the mouse; while looking around it sweeps side to side instead.
            float d = Math.Max(1, Box.Width);
            double cx = Box.X + d / 2, cy = Box.Y + d * 0.45;
            double tx = Math.Max(-1, Math.Min(1, (mouse.X - cx) / (d * 2.2)));
            double ty = Math.Max(-1, Math.Min(1, (mouse.Y - cy) / (d * 2.2)));
            if (now < sweepUntil) { tx = Math.Sin((sweepUntil - now) / 350.0) * 0.9; ty = -0.2; }
            switch (act)
            {
                case MascotProps.Juggle: tx = Math.Sin(now / 500.0) * 0.5; ty = -0.9; break;
                case MascotProps.Console: tx = 0; ty = 1; break;
                case MascotProps.Read: tx = ((now / 900.0) % 1) * 1.2 - 0.6; ty = 0.9; break;
                case MascotProps.Stretch: tx = 0; ty = -0.5; break;
                case MascotProps.Coffee: ty = Math.Max(ty, 0.2); break;
            }
            if (mood == Mood.Sleep || mood == Mood.Dizzy) { tx = 0; ty = 0.3; }
            double follow = 1 - Math.Exp(-dt / 0.07);
            lookX += (tx - lookX) * follow;
            lookY += (ty - lookY) * follow;

            double lean = lookX * 7 + Facing * 9 + (Dangling ? Math.Sin(now / 160.0) * 10 : 0);
            if (now < danceUntil)
            {
                double beat = Math.Sin(now / 150.0);
                lean += beat * 13;
                if (Math.Abs(beat) > 0.97 && jumpV > -0.2) { jumpV -= 0.55; squashY = 0.93; squashX = 1.06; }
                if (now - lastSpark > 330) { lastSpark = now; Spawn('n', beat > 0 ? 0.3 : -0.3, -0.28, 0.32); }
            }
            if (now < bowUntil) lean += Math.Sin((1 - (bowUntil - now) / 1500.0) * Math.PI) * 24;
            if (hopsLeft > 0 && now >= nextHop)
            {
                hopsLeft--;
                nextHop = now + 520;
                jumpV -= 2.2;
                squashX = 1.1; squashY = 0.88;
                for (int i = 0; i < 3; i++) Spawn('p', 0, 0.05, 0.5);
            }
            if (now < bubblesUntil && now - lastBubble > 240) { lastBubble = now; Spawn('o', 0.13, -0.02, 0.2); }
            if (mood == Mood.Sleep && sleepVar == 3) lean += 11 + Math.Sin(now / 2300.0) * 7;
            Anim.Spring(ref tilt, ref tiltV, lean, 90, 0.5, dt);
            Anim.Spring(ref antenna, ref antennaV, -tilt * 1.6, 60, 0.12, dt);
            Anim.Spring(ref earL, ref earLV, -tilt * 0.8 + (mood == Mood.Sleep ? 18 : 0), 70, 0.18, dt);
            Anim.Spring(ref earR, ref earRV, tilt * 0.8 + (mood == Mood.Sleep ? 18 : 0), 70, 0.18, dt);
            Anim.Spring(ref squashX, ref squashXV, 1, 380, 0.28, dt);
            Anim.Spring(ref squashY, ref squashYV, 1, 380, 0.28, dt);
            Anim.Spring(ref jump, ref jumpV, 0, 120, 0.4, dt);
            if (mood == Mood.Dizzy) spin += spinV * dt;
            else
            {
                double target = Math.Round(spin / 360) * 360;
                if (Math.Abs(spinV) > 200) spin += spinV * dt * 0.5;
                Anim.Spring(ref spin, ref spinV, target, 60, 0.6, dt);
            }
            appear += (1 - appear) * (1 - Math.Exp(-dt / 0.12));
            if (Look.Kind == 4) squashX += Math.Sin(now / 260.0) * 0.0015; // jelly wobble

            if (blinkStart < 0 && now > nextBlink)
            {
                blinkStart = now;
                nextBlink = now + 2200 + rnd.NextDouble() * 3600;
                if (rnd.NextDouble() < 0.2) nextBlink = now + 260;
            }
            if (blinkStart >= 0 && now - blinkStart > 150) blinkStart = -1;

            if (mood == Mood.Sleep && now - lastSpark > 1300) { lastSpark = now; Spawn('z', 0.18, -0.3, 0.16); }
            if (mood == Mood.Love && now - lastSpark > 380) { lastSpark = now; Spawn('h', 0, -0.25, 0.45); }
            if (mood == Mood.Dizzy && now - lastSpark > 500) { lastSpark = now; Spawn('s', 0, -0.36, 0.3); }

            pMinX = pMinY = 0; pMaxX = pMaxY = 0;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Particle p = parts[i];
                pMinX = Math.Min(pMinX, p.X - p.Size); pMaxX = Math.Max(pMaxX, p.X + p.Size);
                pMinY = Math.Min(pMinY, p.Y - p.Size); pMaxY = Math.Max(pMaxY, p.Y + p.Size);
                p.Age += dt * 1000;
                if (p.Age > p.Life) { parts.RemoveAt(i); continue; }
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (p.Kind == 'p') { p.Vx *= 0.92; p.Vy *= 0.92; }
                else if (p.Kind == 'z' || p.Kind == 'n') p.X += Math.Sin(p.Age / 300.0) * 0.002;
                else if (p.Kind == 'o') { p.X += Math.Sin(p.Age / 260.0) * 0.003; p.Vx *= 0.98; p.Vy *= 0.985; }
                else if (p.Kind == 'b') { p.Vy -= 0.05 * dt; p.X += Math.Sin(p.Age / 120.0) * 0.003; }
                else p.Vy += 0.12 * dt;
                p.Rot += p.Vr * dt;
            }
        }

        Color C1 { get { return MascotParts.Colors[Look.Color, 0]; } }
        Color C2 { get { return MascotParts.Colors[Look.Color, 1]; } }

        public void Paint(Graphics g)
        {
            RectangleF box = Box;
            float D = box.Width;
            if (D < 4) return;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Color c1 = C1, c2 = C2;
            MascotParts.Geo geo = MascotParts.GeoFor(Look.Kind);

            double speed = mood == Mood.Sleep ? 4200.0 : 2600.0;
            float amp = Look.Kind == 3 ? 0.035f : 0.022f;
            float bob = Dangling ? 0 : (float)(Math.Sin(now / speed * Math.PI * 2) * D * amp);
            float jy = (float)(jump * D * 0.1);
            float cx = box.X + D / 2, cy = box.Y + D * 0.46f + bob + jy;
            float scale = (float)Math.Max(0.01, appear);
            float breathe = (float)(mood == Mood.Sleep ? Math.Sin(now / 900.0) * 0.02 : Math.Sin(now / 1300.0) * 0.007);

            if (ShowShadow)
            {
                float lift = Math.Max(0, -(bob + jy)) / D;
                float sw = D * 0.5f * (1 - lift * 1.6f) * scale, sh = D * 0.075f * (1 - lift * 1.2f) * scale;
                if (Look.Kind == 3) { sw *= 0.8f; sh *= 0.8f; }
                RectangleF shadow = new RectangleF(cx - sw / 2, box.Y + D * 0.9f - sh / 2, sw, sh);
                if (sw > 2 && sh > 2)
                    MascotParts.Glow(g, shadow.X + sw / 2, shadow.Y + sh / 2, sw, sh, Color.Black, (int)(110 * (1 - lift) * (Look.Kind == 3 ? 0.6 : 1)));
            }

            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(tilt + spin % 360));
            g.ScaleTransform((float)squashX * scale * (1 - breathe) * (float)(1 - 0.04 * stretch - 0.06 * Pull), (float)squashY * scale * (1 + breathe) * (float)(1 + 0.1 * stretch + 0.12 * Pull));

            pose.Now = now;
            pose.EarL = earL;
            pose.EarR = earR;
            pose.Antenna = antenna;
            pose.Pulse = 0.5 + 0.5 * Math.Sin(now / 520.0);
            pose.Sleeping = mood == Mood.Sleep;
            pose.HideHands = act != MascotProps.None && actKNow > 0.01;
            pose.Wave = now < waveUntil ? Math.Min(1, (waveUntil - now) / 1800.0 * 3) : 0;
            pose.WaveRot = Math.Sin(now / 90.0) * 28;

            MascotParts.PaintBack(g, Look, D, c1, c2, pose);
            if (Look.Kind != 0) MascotParts.PaintHands(g, Look, D, c1, c2, pose);
            using (GraphicsPath plate = MascotParts.PaintBody(g, Look, D, c1, c2, pose))
            {
                if (plate != null)
                {
                    Region old = g.Clip;
                    g.SetClip(plate, CombineMode.Intersect);
                    PaintFace(g, D, geo, true);
                    g.Clip = old;
                    old.Dispose();
                    MascotParts.Stroke(g, plate, Color.FromArgb(40, 255, 255, 255), Math.Max(1f, D * 0.006f));
                }
                else PaintFace(g, D, geo, false);
            }
            MascotParts.PaintDetails(g, Look, D, c2, pose);
            if (Look.Kind == 0) MascotParts.PaintHands(g, Look, D, c1, c2, pose);
            MascotParts.PaintOutfit(g, Look, D, now);
            MascotParts.PaintFaceAccessory(g, Look, D);
            MascotParts.PaintAccessory(g, Look, D, now, false);
            MascotParts.PaintHat(g, Look, Look.EffectiveHat(DateTime.Now), D, now);
            MascotParts.PaintAccessory(g, Look, D, now, true);
            if (act != MascotProps.None) MascotProps.Paint(g, act, D, actTNow, actKNow, c1, c2);
            else if (mood == Mood.Sleep) MascotProps.PaintSleep(g, sleepVar, D, now, c1, c2);
            g.Restore(st);
            PaintParticles(g, box);
        }

        // Eyes, mouth and cheeks, depending on mood and eye style. On the robot they glow inside the visor.
        void PaintFace(Graphics g, float D, MascotParts.Geo geo, bool visor)
        {
            Color ink = visor ? EyeCore : Ink;
            float fy = geo.FaceY * D, gap = geo.EyeGap * D;
            float ex = (float)(lookX * D * (visor ? 0.075 : 0.05)), ey = fy + (float)(lookY * D * (visor ? 0.055 : 0.035));
            float blink = 1;
            if (blinkStart >= 0)
            {
                double p = (now - blinkStart) / 150.0;
                blink = (float)Math.Max(0.1, 1 - Math.Sin(Math.PI * Math.Min(1, p)) * 0.95);
            }
            float stroke = Math.Max(1.5f, D * (visor ? 0.03f : 0.026f));
            bool cheeks = !visor || mood == Mood.Happy || mood == Mood.Love || mood == Mood.Wink;
            if (cheeks)
            {
                int a = mood == Mood.Happy || mood == Mood.Love ? 140 : 70;
                foreach (int s in new int[] { -1, 1 })
                    MascotParts.Glow(g, s * (gap + D * 0.075f), fy + D * (visor ? 0.11f : 0.075f), D * 0.13f, D * 0.07f, Color.FromArgb(255, 110, 150), a);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -gap : gap) + ex, y = ey;
                Mood m = mood;
                if (m == Mood.Wink && i == 1) m = Mood.Sleep;
                if (visor) MascotParts.Glow(g, x, y, D * 0.19f, D * 0.22f, m == Mood.Love ? LoveColor : EyeGlow, mood == Mood.Sleep ? 40 : 95);
                Color core = m == Mood.Love ? (visor ? Mac.Mix(LoveColor, Color.White, 0.25) : LoveColor) : ink;
                switch (m)
                {
                    case Mood.Happy:
                    case Mood.Wink:
                        using (Pen p = new Pen(core, stroke)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, x - D * 0.06f, y - D * 0.02f, D * 0.12f, D * 0.1f, 200, 140); }
                        break;
                    case Mood.Sleep:
                    case Mood.Yawn:
                        using (Pen p = new Pen(Mac.Alpha(core, 0.8), stroke)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, x - D * 0.055f, y - D * 0.04f, D * 0.11f, D * 0.07f, 20, 140); }
                        break;
                    case Mood.Surprised:
                        EyeShape(g, x, y, D, visor, 1, core, true);
                        break;
                    case Mood.Dizzy:
                        using (Pen p = new Pen(core, stroke * 0.8f))
                        using (GraphicsPath sp = new GraphicsPath())
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                            List<PointF> pts = new List<PointF>();
                            double rot = now / 120.0 * (i == 0 ? 1 : -1);
                            for (int k = 0; k <= 30; k++)
                            {
                                double a = rot + k * 0.42, rr = D * 0.008 + k * D * 0.0022;
                                pts.Add(new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr)));
                            }
                            sp.AddCurve(pts.ToArray());
                            g.DrawPath(p, sp);
                        }
                        break;
                    case Mood.Love:
                        using (GraphicsPath hp = MascotParts.Heart(x, y, D * 0.15f)) MascotParts.FillSolid(g, hp, core);
                        break;
                    default:
                        EyeShape(g, x, y, D, visor, blink, core, false);
                        break;
                }
            }
            PaintMouth(g, D, ex * 0.6f, fy, visor);
        }

        void EyeShape(Graphics g, float x, float y, float D, bool visor, float blink, Color core, bool surprised)
        {
            int style = surprised ? 1 : Look.Eyes;
            float w, h;
            switch (style)
            {
                case 1: w = h = D * 0.12f; break;
                case 2: w = h = D * 0.058f; break;
                case 3: w = D * 0.085f; h = D * 0.13f; break;
                case 4: w = D * 0.1f; h = D * 0.07f; break;
                case 5: w = h = D * 0.11f; break;
                default: w = D * (visor ? 0.072f : 0.066f); h = D * (visor ? 0.145f : 0.115f); break;
            }
            if (surprised) { w *= 1.1f; h *= 1.1f; }
            h *= blink;
            RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
            if (style == 4)
            {
                // Half-closed, unbothered eyes: an ellipse cut by a straight lid.
                RectangleF full = new RectangleF(x - w / 2, y - w / 2, w, w);
                Region old = g.Clip;
                g.SetClip(new RectangleF(full.X - 2, y - w * 0.05f, full.Width + 4, full.Height), CombineMode.Intersect);
                MascotParts.Ellipse(g, core, full.X, full.Y, full.Width, full.Height * blink);
                g.Clip = old;
                old.Dispose();
                MascotParts.Line(g, core, D * 0.022f, x - w * 0.6f, y - w * 0.05f, x + w * 0.6f, y - w * 0.1f);
                return;
            }
            if (style == 5)
            {
                using (GraphicsPath sp = MascotParts.Star(x, y, w * (0.35f + 0.3f * blink), 0.5f)) MascotParts.FillSolid(g, sp, core);
                return;
            }
            using (GraphicsPath p = style == 0 ? Theme.Round(r, w / 2) : Ellipse(r))
            {
                if (style == 3 && !visor) MascotParts.Fill(g, p, r, Mac.Mix(C2, Ink, 0.35), Ink, 90f);
                else MascotParts.FillSolid(g, p, core);
            }
            if (style == 6)
            {
                float sd = x < 0 ? -1 : 1;
                MascotParts.Line(g, core, D * 0.012f, x + sd * w * 0.45f, y - h * 0.38f, x + sd * w * 0.95f, y - h * 0.62f);
                MascotParts.Line(g, core, D * 0.012f, x + sd * w * 0.5f, y - h * 0.12f, x + sd * w * 1.05f, y - h * 0.22f);
            }
            if (blink < 0.5 || style == 2) return;
            // Highlights: what makes eyes look cute.
            Color hl = visor ? Color.FromArgb(150, 20, 30, 60) : Color.FromArgb(240, 255, 255, 255);
            float hr = Math.Min(w, h) * 0.32f;
            MascotParts.Ellipse(g, hl, x + w * 0.04f, y - h * 0.32f, hr, hr);
            if (style == 1 || style == 3) MascotParts.Ellipse(g, hl, x - w * 0.28f, y + h * 0.12f, hr * 0.5f, hr * 0.5f);
        }

        static GraphicsPath Ellipse(RectangleF r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        void PaintMouth(Graphics g, float D, float mx, float fy, bool visor)
        {
            float my = fy + D * (visor ? 0.085f : 0.085f);
            Color ink = visor ? Mac.Alpha(EyeCore, 0.9) : Ink;
            float lw = Math.Max(1.2f, D * (visor ? 0.018f : 0.016f));
            bool talking = Bubble != null && mood != Mood.Sleep && mood != Mood.Dizzy;
            if (mood == Mood.Yawn)
            {
                double o = Math.Sin(Math.Min(1, (moodUntil - now) / 1900.0) * Math.PI);
                float r = D * (0.025f + 0.035f * (float)o);
                MascotParts.Ellipse(g, visor ? ink : Color.FromArgb(70, 30, 50), mx - r * 0.8f, my - r * 0.6f, r * 1.6f, r * 2);
                return;
            }
            if (mood == Mood.Surprised || mood == Mood.Dizzy)
            {
                float r = D * 0.02f;
                MascotParts.Ellipse(g, visor ? ink : Color.FromArgb(70, 30, 50), mx - r, my - r * 0.4f, r * 2, r * 2.4f);
                return;
            }
            if (mood == Mood.Happy || mood == Mood.Love || (talking && !visor))
            {
                float w = D * 0.1f, h = D * 0.06f;
                if (talking && mood == Mood.Idle) h *= (float)(0.45 + 0.55 * Math.Abs(Math.Sin(now / 110.0)));
                if (visor)
                {
                    using (Pen p = new Pen(ink, lw)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, mx - w * 0.4f, my - h * 0.5f, w * 0.8f, h * 0.8f, 20, 140); }
                    return;
                }
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddArc(mx - w / 2, my - h / 2, w, h, 0, 180);
                    m.CloseFigure();
                    MascotParts.FillSolid(g, m, Color.FromArgb(90, 30, 60));
                    Region old = g.Clip;
                    g.SetClip(m, CombineMode.Intersect);
                    MascotParts.Ellipse(g, Color.FromArgb(255, 120, 150), mx - w * 0.3f, my + h * 0.1f, w * 0.6f, h * 0.6f);
                    g.Clip = old;
                    old.Dispose();
                }
                return;
            }
            if (visor) { if (!talking) return; float w = D * 0.08f, h = D * 0.045f * (float)(0.5 + 0.5 * Math.Abs(Math.Sin(now / 110.0)));
                using (Pen p = new Pen(ink, lw)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, mx - w / 2, my - h * 0.5f, w, h, 20, 140); } return; }
            if (mood == Mood.Sleep) { MascotParts.Line(g, ink, lw, mx - D * 0.02f, my, mx + D * 0.02f, my); return; }
            using (Pen p = new Pen(ink, lw))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float s = D * 0.028f;
                if (Look.Kind == 1 || Look.Kind == 2 || Look.Kind == 5 || Look.Kind == MascotParts.KindDog || Look.Kind == MascotParts.KindPanda || Look.Kind == MascotParts.KindFox ||
                    Look.Kind == MascotParts.KindReindeer)
                {
                    // Cat-like "w" mouth.
                    g.DrawArc(p, mx - s * 2, my - s * 0.7f, s * 2, s * 1.4f, 10, 160);
                    g.DrawArc(p, mx, my - s * 0.7f, s * 2, s * 1.4f, 10, 160);
                    if (Look.Kind == 2) { MascotParts.Ellipse(g, Color.White, mx - s * 0.55f, my + s * 0.55f, s * 0.5f, s * 0.6f); MascotParts.Ellipse(g, Color.White, mx + s * 0.05f, my + s * 0.55f, s * 0.5f, s * 0.6f); }
                }
                else g.DrawArc(p, mx - s * 1.3f, my - s, s * 2.6f, s * 1.6f, 20, 140);
            }
        }

        void PaintParticles(Graphics g, RectangleF box)
        {
            float D = box.Width;
            float cx = box.X + D / 2, cy = box.Y + D * 0.46f;
            foreach (Particle p in parts)
            {
                double life = p.Age / p.Life;
                int a = (int)(255 * Math.Min(1, Math.Min(life * 6, (1 - life) * 2.5)));
                if (a <= 0) continue;
                float x = cx + (float)(p.X * D), y = cy + (float)(p.Y * D), s = (float)(p.Size * D);
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)p.Rot * (p.Kind == 'z' || p.Kind == 'b' || p.Kind == 'n' ? 0.08f : p.Kind == 'o' ? 0 : 1));
                switch (p.Kind)
                {
                    case 'h':
                        using (GraphicsPath hp = MascotParts.Heart(0, 0, s)) MascotParts.FillSolid(g, hp, Color.FromArgb(a, LoveColor));
                        break;
                    case 's':
                        using (GraphicsPath sp = MascotParts.Star(0, 0, s * 0.6f, 0.45f)) MascotParts.FillSolid(g, sp, Color.FromArgb(a, 255, 214, 10));
                        break;
                    case 'n':
                    {
                        Color nc = Color.FromArgb(a, Mac.Mix(C1, Color.White, 0.35));
                        MascotParts.Ellipse(g, nc, -s * 0.45f, s * 0.1f, s * 0.5f, s * 0.38f);
                        MascotParts.Line(g, nc, Math.Max(1f, s * 0.12f), s * 0.0f, s * 0.28f, s * 0.0f, -s * 0.6f);
                        MascotParts.Line(g, nc, Math.Max(1f, s * 0.12f), s * 0.0f, -s * 0.6f, s * 0.35f, -s * 0.4f);
                        break;
                    }
                    case 'o':
                    {
                        using (Pen bp = new Pen(Color.FromArgb(a * 2 / 3, 255, 255, 255), Math.Max(1f, s * 0.12f))) g.DrawEllipse(bp, -s * 0.5f, -s * 0.5f, s, s);
                        MascotParts.Ellipse(g, Color.FromArgb(a / 2, 190, 230, 255), -s * 0.3f, -s * 0.34f, s * 0.2f, s * 0.14f);
                        break;
                    }
                    case 'c':
                    {
                        Color cc = p.Tint == 0 ? C1 : p.Tint == 1 ? C2 : p.Tint == 2 ? Color.FromArgb(255, 214, 10) : LoveColor;
                        g.ScaleTransform(1, (float)Math.Max(0.15, Math.Abs(Math.Cos(p.Age / 90.0 + p.Tint))));
                        using (SolidBrush cb = new SolidBrush(Color.FromArgb(a, cc))) g.FillRectangle(cb, -s * 0.5f, -s * 0.28f, s, s * 0.56f);
                        break;
                    }
                    case 'z':
                        Font f = Fonts.Get("Segoe UI Black", (float)Math.Round(Math.Max(6f, s * 1.2f)));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a * 3 / 4, 200, 210, 255)))
                            g.DrawString("z", f, b, -s * 0.4f, -s * 0.7f);
                        break;
                    case 'b':
                    {
                        float flap = (float)Math.Abs(Math.Sin(p.Age / 70.0));
                        using (GraphicsPath bat = new GraphicsPath())
                        {
                            bat.AddPolygon(new PointF[] { new PointF(0, -s * 0.1f), new PointF(-s * 0.5f, -s * 0.3f * flap), new PointF(-s * 0.35f, s * 0.05f),
                                                          new PointF(0, s * 0.15f), new PointF(s * 0.35f, s * 0.05f), new PointF(s * 0.5f, -s * 0.3f * flap) });
                            MascotParts.FillSolid(g, bat, Color.FromArgb(a, 40, 30, 60));
                        }
                        break;
                    }
                    default:
                    {
                        Color c = (int)(p.Rot) % 2 == 0 ? C1 : C2;
                        using (GraphicsPath sp = MascotParts.Star(0, 0, s, 0.45f)) MascotParts.FillSolid(g, sp, Color.FromArgb(a, Mac.Mix(c, Color.White, 0.4)));
                        break;
                    }
                }
                g.Restore(st);
            }
        }

        // Area it may cover (hats, ears, hands, jumps and wherever its particles are now), so only that is repainted
        // and nothing leaves trails.
        public RectangleF PaintBounds
        {
            get
            {
                RectangleF r = Box;
                r.Inflate(Box.Width * 0.6f, Box.Width * 0.75f);
                float d = Box.Width, cx = Box.X + d / 2, cy = Box.Y + d * 0.46f;
                RectangleF p = RectangleF.FromLTRB(cx + (float)(pMinX - 0.15) * d, cy + (float)(pMinY - 0.15) * d, cx + (float)(pMaxX + 0.15) * d, cy + (float)(pMaxY + 0.15) * d);
                return RectangleF.Union(r, p);
            }
        }
        double pMinX, pMaxX, pMinY, pMaxY;

        // A still, front-facing render for pickers and previews.
        public static void RenderStill(Graphics g, RectangleF box, MascotLook look)
        {
            Mascot m = new Mascot();
            m.Look = look;
            m.Box = box;
            m.now = 1;
            m.blinkStart = -1;
            m.Paint(g);
        }
    }
}
