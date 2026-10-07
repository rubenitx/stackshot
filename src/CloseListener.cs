// Stackshot - Hidden window that handles close requests from Windows.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows.Forms;

namespace Stackshot
{
    // Stackshot has no visible top-level window. Windows Installer, the Restart Manager and sign-out send WM_CLOSE /
    // WM_ENDSESSION to top-level windows; this one receives them and shuts down cleanly (keeping the clipboard), so no
    // kill or reboot is needed.
    public sealed class CloseListener : NativeWindow, IDisposable
    {
        readonly Action quit;
        bool quitting;

        public CloseListener(Action quit)
        {
            this.quit = quit;
            CreateParams cp = new CreateParams();
            cp.Caption = "Stackshot";
            cp.ClassName = null;
            cp.ExStyle = 0x80; // WS_EX_TOOLWINDOW: no Alt+Tab or taskbar entry
            cp.Style = 0;      // not WS_VISIBLE
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0010: // WM_CLOSE
                    Quit();
                    return;
                case 0x0011: // WM_QUERYENDSESSION
                    m.Result = (IntPtr)1;
                    return;
                case 0x0016: // WM_ENDSESSION
                    if (m.WParam != IntPtr.Zero) Quit();
                    m.Result = IntPtr.Zero;
                    return;
            }
            base.WndProc(ref m);
        }

        void Quit()
        {
            if (quitting) return;
            quitting = true;
            ShotStack.Log("Cierre pedido por Windows");
            quit();
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
