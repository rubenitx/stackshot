// Stackshot - Shutter sound synthesized at runtime (no third-party audio).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.IO;
using System.Media;

namespace Stackshot
{
    public static class Shutter
    {
        static SoundPlayer player;

        // Two short, dry clicks like a mechanical shutter (~140 ms).
        static byte[] Build()
        {
            const int rate = 44100;
            int n = rate * 140 / 1000;
            short[] pcm = new short[n];
            Random rnd = new Random(7);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate, v = 0;
                v += Click(t, 0.000, 0.012, rnd) * 0.9;
                v += Click(t, 0.055, 0.020, rnd) * 0.7;
                v += Math.Sin(2 * Math.PI * 140 * t) * Math.Exp(-t * 60) * 0.25; // short low thump
                pcm[i] = (short)Math.Max(-32767, Math.Min(32767, v * 20000));
            }
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

        static double Click(double t, double at, double len, Random rnd)
        {
            double d = t - at;
            if (d < 0 || d > len * 4) return 0;
            return (rnd.NextDouble() * 2 - 1) * Math.Exp(-d / len * 3);
        }

        public static void Play()
        {
            try
            {
                // Built and loaded once: every later capture plays from memory without re-parsing the WAV.
                if (player == null)
                {
                    player = new SoundPlayer(new MemoryStream(Build()));
                    player.Load();
                }
                player.Play();
            }
            catch (Exception ex) { ShotStack.Log("Sonido: " + ex.Message); }
        }
    }
}
