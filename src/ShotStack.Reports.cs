// Stackshot - Replies to the reports sent from this computer, noticed in the background: every few hours, and only while
// some report is still open, a notice says what changed (once per change) and opens "Mis reportes".
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Stackshot
{
    public partial class ShotStack
    {
        Timer reportTimer;
        bool reportChecking;

        void StartReportWatch()
        {
            if (Test) return;
            // The first look a few minutes after starting, then every three hours.
            reportTimer = new Timer { Interval = 3 * 60 * 1000 };
            reportTimer.Tick += delegate
            {
                reportTimer.Interval = 3 * 60 * 60 * 1000;
                try { CheckReports(); }
                catch (Exception ex) { Log("Mis reportes: " + ex.Message); }
            };
            reportTimer.Start();
        }

        void CheckReports()
        {
            if (exiting || reportChecking) return;
            List<int> ask = ReportHistory.Pending(ReportHistory.Load());
            if (ask.Count == 0) return;
            reportChecking = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<int, ReportHistory.State> got = null;
                try
                {
                    string error;
                    got = ReportHistory.Fetch(ask, out error);
                }
                catch (Exception ex) { Log("Mis reportes: " + ex.Message); }
                Ui(delegate
                {
                    reportChecking = false;
                    if (exiting || got == null || got.Count == 0) return;
                    // Compared with what was known before it is recorded, so each change is told once.
                    List<string> changes = ReportHistory.Changes(ReportHistory.Load(), got);
                    ReportHistory.Record(got, false);
                    if (changes.Count == 0) return;
                    string title = changes.Count == 1 ? I18n.T("Novedades en tu reporte", "Updates on your report") : I18n.Fmt("Novedades en {0} de tus reportes", "Updates on {0} of your reports", changes.Count);
                    Notify(title, string.Join(". ", changes.ToArray()) + ".", NoticeKind.Info, I18n.T("Ver mis reportes", "View my reports"), delegate { if (!exiting) ReportWindow.OpenMine(); });
                });
            });
        }
    }
}
