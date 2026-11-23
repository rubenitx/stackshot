// Stackshot - Focus mode (Pomodoro): blocks of work and breaks, a long break every few blocks, paused while you are
// away. Only the clock; the owner (ShotStack.Focus.cs) decides what to say and the desktop mascot keeps you company.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Stackshot
{
    public sealed class FocusClock
    {
        public enum Phase { Off, Work, Break }

        public Phase Now { get; private set; }
        public bool LongBreak { get; private set; }
        public bool Paused { get; private set; }
        public bool Away;               // paused because nobody was at the computer (asked about once they are back)
        public DateTime AwaySince;      // when they left
        public bool Asking;             // a phase ended and the next one waits for an answer
        public DateTime AskingSince;
        public int Block { get; private set; }     // 1 to Rounds: the block running, or the one just done
        public int Rounds = 4;

        DateTime ends;
        TimeSpan total, left;           // left: kept while paused

        public bool On { get { return Now != Phase.Off; } }
        public bool Working { get { return Now == Phase.Work && !Paused && !Asking; } }
        public TimeSpan Total { get { return total; } }

        public TimeSpan Left(DateTime now)
        {
            if (Asking) return TimeSpan.Zero;
            TimeSpan l = Paused ? left : ends - now;
            return l < TimeSpan.Zero ? TimeSpan.Zero : l;
        }

        // 0 at the start of the phase, 1 at its end.
        public double Progress(DateTime now)
        {
            if (total.Ticks <= 0) return 0;
            return Math.Max(0, Math.Min(1, 1 - Left(now).TotalSeconds / total.TotalSeconds));
        }

        // The next block (the first after starting or after a long break), or the same one again (again: true).
        public void StartWork(int minutes, DateTime now, bool again)
        {
            if (!again || Block == 0) Block = Now == Phase.Off || Block >= Rounds || (Now == Phase.Break && LongBreak) ? 1 : Block + 1;
            Begin(Phase.Work, minutes, now);
            LongBreak = false;
        }

        // The break after the block just done: a long one after the last block of a round.
        public void StartBreak(int shortMinutes, int longMinutes, DateTime now)
        {
            LongBreak = Block >= Rounds;
            Begin(Phase.Break, LongBreak ? longMinutes : shortMinutes, now);
        }

        void Begin(Phase p, int minutes, DateTime now)
        {
            Now = p;
            total = TimeSpan.FromMinutes(Math.Max(1, minutes));
            ends = now + total;
            Paused = Away = Asking = false;
        }

        public void Pause(DateTime now)
        {
            if (Paused || Asking || !On) return;
            left = Left(now);
            Paused = true;
        }

        // Nobody at the computer since then: that time doesn't count.
        public void PauseAway(DateTime since, DateTime now)
        {
            if (Paused || Asking || !On) return;
            left = ends - since;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            Paused = Away = true;
            AwaySince = since;
        }

        public void Resume(DateTime now)
        {
            if (!Paused) return;
            ends = now + left;
            Paused = Away = false;
        }

        public void Extend(int minutes, DateTime now)
        {
            if (Asking) { total = TimeSpan.FromMinutes(minutes); ends = now + total; Asking = false; return; }
            ends += TimeSpan.FromMinutes(minutes);
            total += TimeSpan.FromMinutes(minutes);
        }

        // The phase ran out: it waits for what comes next.
        public void Ask(DateTime now)
        {
            Asking = true;
            AskingSince = now;
        }

        public bool Due(DateTime now) { return On && !Paused && !Asking && now >= ends; }

        public void Stop()
        {
            Now = Phase.Off;
            Paused = Away = Asking = LongBreak = false;
            Block = 0;
        }

        // "18:42", or "1:05:00" for an hour or more.
        public static string Clock(TimeSpan t)
        {
            int s = (int)Math.Ceiling(t.TotalSeconds);
            return s >= 3600 ? (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
        }

        // "25 min", "1 h 15 min" ("25분", "1시간 15분" in Korean, which has its own units).
        public static string Minutes(int m)
        {
            if (I18n.IsKorean)
                return m < 60 ? I18n.Fmt("", "{0} min", m) : I18n.Fmt("", "{0} h", m / 60) + (m % 60 > 0 ? " " + I18n.Fmt("", "{0} min", m % 60) : "");
            return m < 60 ? m + " min" : (m / 60) + " h" + (m % 60 > 0 ? " " + (m % 60) + " min" : "");
        }
    }

    // A soft sound for each moment of focus mode, so it can be told apart without looking; synthesized like the shutter
    // (no audio files), built once each.
    public static class FocusChime
    {
        public enum Cue { Start, Pause, Resume, End, Break, Stop }

        // Each sound in memory that never moves (an asynchronous PlaySound reads it while it plays), kept for the app's life.
        static readonly IntPtr[] sounds = new IntPtr[6];

        [DllImport("winmm.dll")] static extern bool PlaySound(IntPtr sound, IntPtr module, int flags);
        const int SndAsync = 0x1, SndNoDefault = 0x2, SndMemory = 0x4;

        // Notes (Hz) and when each starts (s): rising for starting, falling for resting and ending, a bell for "time's up".
        static readonly double[][] Notes =
        {
            new double[] { 523.25, 0, 659.25, 0.12, 783.99, 0.24 },     // Start: C5 E5 G5, up
            new double[] { 783.99, 0, 659.25, 0.14 },                   // Pause: G5 E5, down
            new double[] { 659.25, 0, 783.99, 0.14 },                   // Resume: E5 G5, up
            new double[] { 1046.5, 0, 1046.5, 0.32 },                   // End: a bell rung twice
            new double[] { 783.99, 0, 659.25, 0.2, 523.25, 0.4 },      // Break: G5 E5 C5, slowly down
            new double[] { 523.25, 0, 392.0, 0.16, 261.63, 0.32 }       // Stop: C5 G4 C4, a closing
        };

        public static void Play(Cue cue)
        {
            try
            {
                int i = (int)cue;
                if (sounds[i] == IntPtr.Zero)
                {
                    byte[] wav = Build(Notes[i], cue == Cue.Pause || cue == Cue.Resume ? 0.8 : 1);
                    IntPtr p = Marshal.AllocHGlobal(wav.Length);
                    Marshal.Copy(wav, 0, p, wav.Length);
                    sounds[i] = p;
                }
                if (!PlaySound(sounds[i], IntPtr.Zero, SndMemory | SndAsync | SndNoDefault)) ShotStack.Log("Sonido de concentraci\u00F3n: Windows no lo ha reproducido (" + cue + ")");
            }
            catch (Exception ex) { ShotStack.Log("Sonido de concentraci\u00F3n: " + ex.Message); }
        }

        // Kept for the settings switch: rising plays Start, otherwise Break.
        public static void Play(bool rising) { Play(rising ? Cue.Start : Cue.Break); }

        // Silence before the notes: Bluetooth and USB headsets asleep after a quiet while drop the first moment of a sound.
        const double Lead = 0.3;

        static byte[] Build(double[] notes, double level)
        {
            const int rate = 44100;
            double last = 0;
            for (int k = 1; k < notes.Length; k += 2) last = Math.Max(last, notes[k]);
            int lead = (int)(rate * Lead), n = lead + (int)(rate * (last + 1.0));
            double[] wave = new double[n];
            double peak = 0;
            for (int i = lead; i < n; i++)
            {
                double t = (i - lead) / (double)rate, v = 0;
                for (int k = 0; k < notes.Length; k += 2) v += Note(t, notes[k + 1], notes[k]);
                wave[i] = v;
                peak = Math.Max(peak, Math.Abs(v));
            }
            // Every cue peaks at the same level, whatever its chord.
            double gain = peak > 0 ? 18000 * level / peak : 0;
            short[] pcm = new short[n];
            for (int i = 0; i < n; i++) pcm[i] = (short)Math.Max(-32767, Math.Min(32767, wave[i] * gain));
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                int data = n * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + data);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(data);
                foreach (short sample in pcm) w.Write(sample);
                return ms.ToArray();
            }
        }

        // A bell-like note: a quick attack, a long soft decay and a quiet octave on top.
        static double Note(double t, double at, double f)
        {
            double d = t - at;
            if (d < 0) return 0;
            double env = Math.Min(1, d / 0.008) * Math.Exp(-d * 4.2);
            return env * (Math.Sin(2 * Math.PI * f * d) + 0.25 * Math.Sin(2 * Math.PI * f * 2 * d));
        }
    }
}
