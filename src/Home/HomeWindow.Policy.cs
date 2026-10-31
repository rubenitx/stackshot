// Stackshot - The main window under IT policies: the settings they force are shown but can't be changed (see Policy).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // Read again whenever a section is built, so what IT changes applies without restarting Stackshot.
        void ReadPolicy()
        {
            try { if (Policy.Apply(settings)) owner.PolicyChanged(); }
            catch (Exception ex) { ShotStack.Log("Directivas: " + ex.Message); }
        }

        // The rows of the settings a policy forces: dimmed and disabled, saying who decides. Without policies nothing
        // here runs.
        void PolicyRows()
        {
            if (body == null || !Policy.Any) return;
            List<Row> rows = new List<Row>();
            FindRows(body, rows);
            foreach (Row r in rows)
            {
                if (!ForcedByPolicy(r)) continue;
                r.When = delegate { return false; };
                r.WhenOff = Policy.Managed;
                r.Build();
            }
        }

        // The rows (in General and in Ajustes de la mascota) of each setting a policy can force, found by the same titles
        // the pages build them with, so they match in every language.
        static bool ForcedByPolicy(Row r)
        {
            if (r is FolderRow) return Policy.FolderForced;
            string t = r.Title;
            if (t == PolicyTitles.Checks) return Policy.ChecksForced;
            if (t == PolicyTitles.Auto) return Policy.AutoForced;
            if (t == PolicyTitles.Mascot || t == PolicyTitles.Greetings || t == PolicyTitles.Desktop) return Policy.MascotForced;
            if (t == PolicyTitles.Sensitive) return Policy.SensitiveForced;
            if (t == PolicyTitles.Search) return Policy.SearchForced;
            return false;
        }

        // A control outside those rows (Acerca de, the mascot's stage) whose setting a policy forces.
        static void LockedByPolicy(FrameworkElement e, bool forced)
        {
            if (!forced) return;
            e.IsEnabled = false;
            e.Opacity = 0.42;
            if (e.Cursor == System.Windows.Input.Cursors.Hand) e.Cursor = System.Windows.Input.Cursors.Arrow;   // nothing to press
            e.ToolTip = Policy.Managed;
            ToolTipService.SetShowOnDisabled(e, true);
        }

        // The line under the update card in Acerca de while any policy applies.
        FrameworkElement PolicyNote()
        {
            Palette pal = Ds.Brushes;
            StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
            GlyphView icon = new GlyphView("info", 13, pal.Label2, 1.5);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 6, 0);
            line.Children.Add(icon);
            line.Children.Add(Label(Policy.ManagedNote, Ds.Regular, 12.5, pal.Label2));
            return line;
        }
    }

    // Titles of the rows a policy can force: shared by the pages that build them and by ForcedByPolicy.
    static class PolicyTitles
    {
        public static string Checks { get { return I18n.T("Buscar actualizaciones", "Check for updates"); } }
        public static string Auto { get { return I18n.T("Instalar actualizaciones autom\u00E1ticamente", "Install updates automatically"); } }
        public static string Mascot { get { return I18n.T("Mostrar la mascota", "Show mascot"); } }
        public static string Greetings { get { return I18n.T("Saludos y consejos", "Greetings and tips"); } }
        public static string Desktop { get { return I18n.T("Mascota en el escritorio", "Mascot on desktop"); } }
        public static string Sensitive { get { return I18n.T("Tapar los datos sensibles al capturar", "Auto-redact sensitive data on capture"); } }
        public static string Search { get { return I18n.T("Buscar por el texto de las capturas", "Search captures by OCR text"); } }
    }
}
