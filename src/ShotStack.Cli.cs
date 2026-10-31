// Stackshot - The stack carrying out command-line actions (--capture, --open-home, --last-xml; see CliCommand).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;

namespace Stackshot
{
    public partial class ShotStack
    {
        // The command this copy was started with (set by Program before the stack is built), carried out once it is up.
        public static string CommandAtStart;

        // Commands other copies sent, and the one this copy started with: each runs on its own turn of the message loop,
        // exactly as its hotkey or tray entry would.
        void RunCommands(List<string> taken)
        {
            string atStart = CommandAtStart;
            CommandAtStart = null;
            if (atStart == null && taken.Count == 0) return;
            foreach (string c in CliCommand.Batch(taken, atStart))
            {
                string command = c;
                sync.BeginInvoke((Action)delegate { if (!exiting) RunCommand(command); });
            }
        }

        // While an area is being picked nothing else runs, as with the hotkeys (a window would cover the frozen screen).
        void RunCommand(string command)
        {
            if (picking) return;
            if (command == CliCommand.Home) ShowHome("home", true);
            else if (command == CliCommand.LastXml) OpenLatestXmlQuietly();
            else if (command.StartsWith(CliCommand.Capture, StringComparison.Ordinal)) OnHotkey(command.Substring(CliCommand.Capture.Length));
        }

        // Like the tray's latest-XML entry, with the folder listed off the UI thread (it may hold thousands of files).
        void OpenLatestXmlQuietly()
        {
            string dir = XmlWatch.FolderFor(settings);
            if (dir == null) { Notify("Stackshot", NoXmlFolder); return; }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string last = XmlWatch.Latest(dir);
                Ui(delegate
                {
                    if (last == null) Notify("Stackshot", I18n.Fmt("No hay ning\u00FAn XML en {0}.", "There is no XML in {0}.", dir));
                    else OpenViewerFile(last);
                });
            });
        }
    }
}
