// Stackshot - Command-line actions: --capture <mode>, --open-home and --last-xml (keypad buttons, scripts).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.IO;

namespace Stackshot
{
    // A command is carried out by the Stackshot already running: it travels like an "open this file" request (a line in
    // open.txt, then the Open event) and that copy does what the hotkey or the tray does. When none is running, this copy
    // starts as usual, without its window, and carries it out once its tray icon is up (ShotStack.CommandAtStart).
    //   --capture <mode>   any hotkey action (region, screen, window, scroll, text, video, gif, markdown...) or pause
    //   --open-home        the main window
    //   --last-xml         the newest XML of the watched folder, in the viewer
    //   --link <name|n>    one of the quick links, by its name (or how it starts) or its place in the list
    // Options may also be written /capture region, /capture:region or --capture=region, in any case.
    public static class CliCommand
    {
        public const string Home = "open-home", LastXml = "last-xml", Capture = "capture ", Link = "link ";

        // A command's line in open.txt starts with a character no path can start with, so a file is never taken for one
        // (not even one named "--capture").
        const char Marker = '*';

        // A command is only worth doing right after it was asked for: a capture popping up later would be a surprise.
        public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);

        // What --capture accepts: every hotkey action, plus pausing a recording.
        public static bool IsMode(string mode)
        {
            return mode == "pause" || Array.IndexOf(Settings.Actions, mode) >= 0;
        }

        // A command this version knows how to carry out.
        public static bool Known(string command)
        {
            if (command == Home || command == LastXml) return true;
            if (command != null && command.StartsWith(Link, StringComparison.Ordinal)) return command.Substring(Link.Length).Trim().Length > 0;
            return command != null && command.StartsWith(Capture, StringComparison.Ordinal) && IsMode(command.Substring(Capture.Length));
        }

        // The command these arguments ask for ("capture region", "open-home", "last-xml"), or null. bad: what was asked
        // for when it names an unknown capture mode (or none); the command is then null and nothing is done. An argument
        // that is an existing file is a file, whatever its name.
        public static string Parse(string[] args, out string bad)
        {
            bad = null;
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i], name, value;
                if (!Option(a, out name, out value) || (name != "capture" && name != Home && name != LastXml && name != "link") || IsFile(a)) continue;
                if (name == "link")
                {
                    bool next = value == null && i + 1 < args.Length;
                    if (next) value = args[i + 1];
                    string which = (value ?? "").Trim().Trim('"').Trim();
                    if (which.Length > 0 && which.Length <= 80) return Link + which;
                    bad = next ? a + " " + value : a;
                    return null;
                }
                if (name == "capture")
                {
                    bool next = value == null && i + 1 < args.Length;
                    if (next) value = args[i + 1];
                    string mode = (value ?? "").Trim().ToLowerInvariant();
                    if (IsMode(mode)) return Capture + mode;
                    bad = next ? a + " " + value : a;
                    return null;
                }
                if (value != null) continue;
                if (name == Home) return Home;
                if (name == LastXml) return LastXml;
            }
            return null;
        }

        // "--name", "/name", with an optional "=value", ":value" or " value"; name in lower case.
        static bool Option(string a, out string name, out string value)
        {
            name = value = null;
            if (string.IsNullOrEmpty(a)) return false;
            a = a.Trim();
            string body = a.StartsWith("--", StringComparison.Ordinal) ? a.Substring(2) : a.StartsWith("/", StringComparison.Ordinal) ? a.Substring(1) : null;
            if (string.IsNullOrEmpty(body)) return false;
            // A launcher that quotes its whole arguments field hands over "--capture region" as one argument.
            int sep = body.IndexOfAny(new char[] { '=', ':', ' ', '\t' });
            if (sep == 0) return false;
            if (sep > 0) { value = body.Substring(sep + 1); body = body.Substring(0, sep); }
            name = body.ToLowerInvariant();
            return true;
        }

        static bool IsFile(string a)
        {
            try { return File.Exists(a); }
            catch { return false; }
        }

        // The arguments that ask another copy (the installed one) for the same, with the file to open if there is one.
        public static string Arguments(string command, string view)
        {
            string a = command.StartsWith(Capture, StringComparison.Ordinal) ? "--capture " + command.Substring(Capture.Length)
                     : command.StartsWith(Link, StringComparison.Ordinal) ? "--link \"" + command.Substring(Link.Length) + "\"" : "--" + command;
            return view != null ? a + " --open \"" + view + "\"" : a;
        }

        // The line written to open.txt for a command (SendOpen adds the time).
        public static string Request(string command) { return Marker + command; }

        // A request line that carries a command rather than a path.
        public static bool IsRequest(string line) { return line.Length > 1 && line[0] == Marker; }

        // Adds a command read from open.txt to the list, once, unless it is old, from the future, untimed or unknown.
        public static void Take(List<string> into, string line, long ticks)
        {
            string command = line.Substring(1).Trim();
            long now = DateTime.UtcNow.Ticks;
            if (!Known(command)) { ShotStack.Log("Orden desconocida en open.txt: " + command); return; }
            if (ticks <= 0 || now - ticks > Fresh.Ticks || ticks - now > TimeSpan.TicksPerMinute) { ShotStack.Log("Orden caducada, no se hace: " + command); return; }
            if (!into.Contains(command)) into.Add(command);
        }

        // What to carry out of the commands taken together (and the one this copy was started with): each once; of the
        // captures, only the last one asked for, after the rest (a capture is something to do now, not to queue).
        public static List<string> Batch(List<string> taken, string atStart)
        {
            List<string> all = new List<string>(), run = new List<string>();
            if (atStart != null) all.Add(atStart);
            if (taken != null) all.AddRange(taken);
            string capture = null;
            foreach (string c in all)
            {
                if (!Known(c)) continue;
                if (c.StartsWith(Capture, StringComparison.Ordinal)) capture = c;
                else if (!run.Contains(c)) run.Add(c);
            }
            if (capture != null) run.Add(capture);
            return run;
        }

        // Hands the command to the running Stackshot and returns the exit code: 0 delivered, 1 it could not be written.
        // This copy was started by a key or a click, so it lets the running one take the foreground (the area picker,
        // the window) instead of flashing in the taskbar.
        public static int Forward(string command, string view)
        {
            try { Native.AllowSetForegroundWindow(-1); } catch { }
            bool sent = Installer.SendOpen(Request(command), 3000);
            if (view != null) Installer.SendOpen(view, 3000);
            if (sent) return 0;
            ShotStack.Log("L\u00EDnea de \u00F3rdenes: no se pudo pasar \u00AB" + command + "\u00BB a Stackshot");
            return 1;
        }
    }
}
