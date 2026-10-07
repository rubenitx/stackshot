// Stackshot - Ventana invisible que atiende las peticiones de cierre de Windows.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Windows.Forms;

namespace Stackshot
{
    // Stackshot vive en la bandeja, sin ventanas visibles. Windows Installer (al actualizar o desinstalar el MSI),
    // el Administrador de reinicio y el cierre de sesión piden cerrar enviando WM_CLOSE o WM_ENDSESSION a las
    // ventanas de primer nivel del programa: esta las recibe y cierra Stackshot ordenadamente (dejando el
    // portapapeles en su sitio), sin que haga falta matarlo ni reiniciar el equipo.
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
            cp.ExStyle = 0x80; // WS_EX_TOOLWINDOW: fuera de Alt+Tab y de la barra de tareas
            cp.Style = 0;      // sin WS_VISIBLE: nunca se ve
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0010: // WM_CLOSE
                    Quit();
                    return;
                case 0x0011: // WM_QUERYENDSESSION: se puede cerrar cuando quieran
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
