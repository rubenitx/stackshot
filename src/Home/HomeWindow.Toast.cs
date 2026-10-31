// Stackshot - The main window's side of the notices: the button that leads to one of its sections.
// MIT License - https://github.com/rubenitx/stackshot
using System;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // "Abrir Atajos"; Grabación is named in full so it doesn't read as opening a recording.
        internal static string NoticeButton(string page)
        {
            if (page == "record") return I18n.T("Ajustes de grabaci\u00F3n", "Recording settings");
            int i = Array.IndexOf(PageIds, page);
            return i < 0 ? I18n.T("Abrir Stackshot", "Open Stackshot") : I18n.Fmt("Abrir {0}", "Open {0}", PageNames[i]);
        }
    }
}
