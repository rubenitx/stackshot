// Stackshot - Healthy breaks: the mascot reminds you to drink water, rest your eyes, move and breathe.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;
using Microsoft.Win32;

namespace Stackshot
{
    public static class WellKind
    {
        public const int Water = 0, Eyes = 1, Move = 2, Breathe = 3;
        public const int Count = 4;

        static readonly string[] NamesEs = { "Beber agua", "Descansar la vista", "Moverse", "Respirar" };
        static readonly string[] NamesEn = { "Drink water", "Rest your eyes", "Move", "Breathe" };
        public static string[] Names { get { return I18n.IsSpanish ? NamesEs : NamesEn; } }
        public static readonly string[] Glyphs = { "drop", "eye", "walk", "wind" };
        // Choices for each interval, in minutes (0: off).
        public static readonly int[] Minutes = { 0, 20, 30, 45, 60, 90, 120 };
        static readonly string[] MinuteNamesEs = { "No", "20 min", "30 min", "45 min", "1 h", "1 h 30", "2 h" };
        static readonly string[] MinuteNamesEn = { "Off", "20 min", "30 min", "45 min", "1 h", "1 h 30", "2 h" };
        public static string[] MinuteNames { get { return I18n.IsSpanish ? MinuteNamesEs : MinuteNamesEn; } }

        public static int IndexOf(int minutes)
        {
            int i = Array.IndexOf(Minutes, minutes);
            return i < 0 ? 0 : i;
        }
    }

    // Counts only the time you are at the computer: a pause of 5 minutes or more is already a break for the eyes and
    // the body, so those start over. One reminder at a time, at least 4 minutes apart, never while Windows asks for
    // quiet, in a call, recording or picking an area (the owner checks that and simply asks again later).
    public sealed class Wellness
    {
        public const int ActiveIdleMs = 90000;      // input within this counts as being at the computer
        public const int BreakIdleMs = 300000;      // away this long: eyes, move and breathe start over
        public const double SpacingMs = 240000;     // between two reminders
        public const double IgnoredMs = 600000;     // a reminder nobody answered comes back after this

        readonly double[] active = new double[WellKind.Count];
        readonly double[] snooze = new double[WellKind.Count];
        double last, nextAllowed;
        public bool MoveIsStretch;                  // Move alternates between the legs and a back stretch

        public double Active(int kind) { return active[kind]; }

        // Advances the clocks. now: ms (monotonic); idleMs: time since the last keyboard or mouse input.
        public void Tick(double now, int idleMs)
        {
            double dt = last == 0 ? 0 : now - last;
            last = now;
            // A long gap is a suspend or a stall, not time spent at the screen.
            if (dt <= 0 || dt > 120000) dt = 0;
            if (idleMs >= BreakIdleMs)
            {
                active[WellKind.Eyes] = active[WellKind.Move] = active[WellKind.Breathe] = 0;
                return;
            }
            if (idleMs < ActiveIdleMs)
                for (int k = 0; k < WellKind.Count; k++) active[k] += dt;
        }

        // The reminder to show now, or -1: the most overdue one that is switched on and not snoozed.
        public int Due(double now, int[] minutes)
        {
            if (now < nextAllowed) return -1;
            int best = -1;
            double most = 0;
            for (int k = 0; k < WellKind.Count; k++)
            {
                if (minutes[k] <= 0 || now < snooze[k]) continue;
                double over = active[k] / (minutes[k] * 60000.0);
                if (over >= 1 && over > most) { most = over; best = k; }
            }
            return best;
        }

        public void Shown(double now) { nextAllowed = now + SpacingMs; }

        public void Done(int kind, double now)
        {
            active[kind] = 0;
            snooze[kind] = 0;
            nextAllowed = Math.Max(nextAllowed, now + SpacingMs);
            if (kind == WellKind.Move) MoveIsStretch = !MoveIsStretch;
        }

        public void Later(int kind, double now, double ms)
        {
            snooze[kind] = now + ms;
            nextAllowed = Math.Max(nextAllowed, now + SpacingMs);
        }

        // ---- Today's count of each break, kept in the settings ("yyyy-MM-dd" and four numbers).

        public static int[] Today(Settings s)
        {
            int[] n = new int[WellKind.Count];
            if (s.WellDay != DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) return n;
            string[] p = (s.WellDone ?? "").Split(',');
            for (int i = 0; i < n.Length && i < p.Length; i++)
            {
                int v;
                if (int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) n[i] = Math.Max(0, Math.Min(999, v));
            }
            return n;
        }

        // One more of that break today; returns the new count. (To read it without adding, Today(s)[kind].)
        public static int AddOne(Settings s, int kind)
        {
            int[] n = Today(s);
            n[kind]++;
            s.WellDay = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            s.WellDone = string.Join(",", Array.ConvertAll(n, delegate(int v) { return v.ToString(CultureInfo.InvariantCulture); }));
            return n[kind];
        }

        public static string Summary(Settings s)
        {
            int[] n = Today(s);
            if (n[0] + n[1] + n[2] + n[3] == 0) return I18n.T("Hoy todav\u00EDa no has hecho ninguna pausa.", "You haven't taken any breaks yet today.");
            if (I18n.IsSpanish)
            {
                string r = "Hoy: " + n[0] + (n[0] == 1 ? " vaso de agua" : " vasos de agua") + " \u00B7 " + n[1] + (n[1] == 1 ? " descanso de vista" : " descansos de vista") +
                           " \u00B7 " + n[2] + (n[2] == 1 ? " pausa para moverte" : " pausas para moverte");
                if (n[3] > 0) r += " \u00B7 " + n[3] + (n[3] == 1 ? " respiraci\u00F3n" : " respiraciones");
                return r + ".";
            }
            else
            {
                string r = "Today: " + n[0] + (n[0] == 1 ? " glass of water" : " glasses of water") + " \u00B7 " + n[1] + (n[1] == 1 ? " eye rest" : " eye rests") +
                           " \u00B7 " + n[2] + (n[2] == 1 ? " movement break" : " movement breaks");
                if (n[3] > 0) r += " \u00B7 " + n[3] + (n[3] == 1 ? " breathing break" : " breathing breaks");
                return r + ".";
            }
        }

        public static int[] MinutesOf(Settings s)
        {
            return new int[] { s.WellWater, s.WellEyes, s.WellMove, s.WellBreathe };
        }

        // ---- In a call: some app (Teams, Zoom, a browser) is using the microphone or the camera right now. Windows keeps
        // that per app in the privacy store; LastUsedTimeStop is 0 while it is in use. Only read, off the UI thread,
        // every 30 s. Use that began more than 4 hours ago doesn't count: an app that keeps the microphone open all day
        // (noise suppression, push-to-talk) or an entry left behind by a crash would silence the reminders for good.
        const long CallMaxTicks = 4 * TimeSpan.TicksPerHour;

        public static bool InCall()
        {
            return InUse("microphone") || InUse("webcam");
        }

        static bool InUse(string capability)
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\" + capability))
                {
                    if (root == null) return false;
                    if (AnyInUse(root)) return true;
                    using (RegistryKey np = root.OpenSubKey("NonPackaged")) if (np != null && AnyInUse(np)) return true;
                }
            }
            catch { }
            return false;
        }

        static bool AnyInUse(RegistryKey parent)
        {
            foreach (string name in parent.GetSubKeyNames())
            {
                if (name == "NonPackaged" || name.IndexOf("Stackshot", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                using (RegistryKey k = parent.OpenSubKey(name))
                {
                    if (k == null) continue;
                    object start = k.GetValue("LastUsedTimeStart"), stop = k.GetValue("LastUsedTimeStop");
                    if (!(start is long) || (long)start <= 0 || !(stop is long) || (long)stop != 0) continue;
                    long age = DateTime.UtcNow.ToFileTimeUtc() - (long)start;
                    if (age >= 0 && age < CallMaxTicks) return true;
                }
            }
            return false;
        }
    }
}
