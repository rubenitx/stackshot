// Stackshot - German localization catalog.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static Dictionary<string, string> deByEn;
        static Dictionary<string, string> deByEs;

        static void InitGerman()
        {
            if (deByEn != null) return;
            var enMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var esMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, string> reg = delegate(string en, string es, string de)
            {
                if (!string.IsNullOrEmpty(en) && !enMap.ContainsKey(en)) enMap[en] = de;
                if (!string.IsNullOrEmpty(es) && !esMap.ContainsKey(es)) esMap[es] = de;
            };

            // Common Verbs & Actions
            reg("Save", "Guardar", "Speichern");
            reg("Copy", "Copiar", "Kopieren");
            reg("Close", "Cerrar", "Schlie\u00DFen");
            reg("Delete", "Eliminar", "L\u00F6schen");
            reg("Cancel", "Cancelar", "Abbrechen");
            reg("Done", "Listo", "Fertig");
            reg("Open", "Abrir", "\u00D6ffnen");
            reg("Quit", "Salir", "Beenden");
            reg("Settings", "Ajustes", "Einstellungen");
            reg("Edit", "Editar", "Bearbeiten");
            reg("Retry", "Reintentar", "Wiederholen");
            reg("Undo", "Deshacer", "R\u00FCckg\u00E4ngig");
            reg("Redo", "Rehacer", "Wiederherstellen");
            reg("Change\u2026", "Cambiar\u2026", "\u00C4ndern\u2026");
            reg("Clear", "Limpiar", "Leeren");
            reg("Search", "Buscar", "Suchen");
            reg("Remove", "Quitar", "Entfernen");
            reg("Not now", "Ahora no", "Nicht jetzt");
            reg("Select all", "Seleccionar todo", "Alles ausw\u00E4hlen");
            reg("Free key", "Liberar", "Taste freigeben");

            // Navigation & Main Pages
            reg("Home", "Inicio", "Startseite");
            reg("History", "Historial", "Verlauf");
            reg("Shortcuts", "Atajos", "Tastenk\u00FCrzel");
            reg("General", "General", "Allgemein");
            reg("Editor", "Editor", "Editor");
            reg("Record", "Grabar", "Aufnehmen");
            reg("Mascot", "Mascota", "Maskottchen");
            reg("About", "Acerca de", "\u00DCber Stackshot");

            // Appearance & Localization
            reg("Appearance", "Apariencia", "Erscheinungsbild");
            reg("Language", "Idioma", "Sprache");
            reg("Interface language (automatic based on Windows, Spanish or English).",
                "El idioma de la interfaz (autom\u00E1tico seg\u00FAn Windows, espa\u00F1ol o ingl\u00E9s).",
                "Sprache der Benutzeroberfl\u00E4che (automatisch nach Windows, Spanisch, Englisch oder Deutsch).");
            reg("Auto", "Autom\u00E1tico", "Automatisch");
            reg("Theme", "Tema", "Design");
            reg("Follow Windows", "Como Windows", "Wie Windows");
            reg("Light", "Claro", "Hell");
            reg("Dark", "Oscuro", "Dunkel");
            reg("Home view", "Vista de Inicio", "Startansicht");
            reg("How the main home view is displayed.", "C\u00F3mo se ve la p\u00E1gina principal.", "Darstellung der Startseite.");
            reg("Startup", "Arranque", "Start");
            reg("Files", "Archivos", "Dateien");
            reg("Updates", "Actualizaciones", "Aktualisierungen");
            reg("Profile", "Perfil", "Profil");
            reg("Quick profile", "Perfil r\u00E1pido", "Schnellprofil");

            // Welcome Screen
            reg("Welcome to Stackshot", "Te damos la bienvenida", "Willkommen bei Stackshot");
            reg("Capture, markup and share in seconds.", "Captura, marca y comparte en segundos.", "Aufnehmen, markieren und in Sekunden teilen.");
            reg("Start with Windows", "Iniciar con Windows", "Mit Windows starten");
            reg("Launch with Windows", "Iniciar con Windows", "Mit Windows starten");
            reg("Starts in background when your PC boots.", "Arranca en segundo plano al encender el equipo.", "Startet im Hintergrund beim Hochfahren des PCs.");
            reg("Opens automatically in the background on startup.", "Se abre solo, en segundo plano, al encender el equipo.", "Startet automatisch im Hintergrund beim Hochfahren.");
            reg("Use Print Screen key", "Usar la tecla Impr Pant", "Druck-Taste verwenden");
            reg("Windows uses it for Snipping Tool; Stackshot claims it for your account.", "Windows la usa para Recortes; Stackshot se la queda solo en tu usuario.", "Windows nutzt sie f\u00FCr Snipping Tool; Stackshot beansprucht sie f\u00FCr dein Konto.");
            reg("Shutter sound", "Sonido al capturar", "Ausl\u00F6seton");
            reg("A gentle camera shutter click.", "Un peque\u00F1o clic de c\u00E1mara.", "Ein sanfter Kamera-Ausl\u00F6seton.");
            reg("Copy every capture", "Copiar cada captura", "Jede Aufnahme kopieren");
            reg("Ready to paste with Ctrl+V right away.", "Lista para pegar con Ctrl+V nada m\u00E1s hacerla.", "Sofort mit Strg+V einf\u00FCgebereit.");
            reg("Where captures are saved", "D\u00F3nde se guardan las capturas", "Speicherort f\u00FCr Aufnahmen.");
            reg("Where your saved captures go.", "Donde van las capturas que guardas.", "Hier werden deine gespeicherten Aufnahmen abgelegt.");
            reg("Where do you want to save your captures?", "\u00BFD\u00F3nde quieres guardar las capturas que conserves?", "Wo m\u00F6chtest du deine Aufnahmen speichern?");

            // Settings toggles & options
            reg("Keep in tray when closed", "Seguir en la bandeja al cerrar", "Beim Schlie\u00DFen im Infobereich lassen");
            reg("Closing this window keeps Stackshot running in the tray.", "Al cerrar esta ventana, Stackshot sigue funcionando junto al reloj.", "Das Schlie\u00DFen dieses Fensters l\u00E4sst Stackshot im Infobereich weiterlaufen.");
            reg("Launch animation", "Animaci\u00F3n al abrir", "Startanimation");
            reg("Logo animation when Stackshot launches.", "La bienvenida con el logo cuando abres Stackshot.", "Logo-Animation beim Starten von Stackshot.");
            reg("Open .md files with Stackshot", "Abrir .md con Stackshot", ".md-Dateien mit Stackshot \u00F6ffnen");
            reg("Open .xml files with Stackshot", "Abrir .xml con Stackshot", ".xml-Dateien mit Stackshot \u00F6ffnen");
            reg("Restore default shortcuts", "Restaurar los atajos de serie", "Standard-Tastenk\u00FCrzel wiederherstellen");
            reg("No shortcut", "Sin atajo", "Kein Tastenk\u00FCrzel");
            reg("Press the combination\u2026", "Pulsa la combinaci\u00F3n\u2026", "Tastenkombination dr\u00FCcken\u2026");
            reg("Click a shortcut and press the combination you want, with Ctrl, Alt, Shift or Win. Del removes it and Esc cancels.",
                "Haz clic en un atajo y pulsa la combinaci\u00F3n que quieras, con Ctrl, Alt, May\u00FAs o Win. Supr lo quita y Esc cancela.",
                "Klicke auf ein Tastenk\u00FCrzel und dr\u00FCcke die gew\u00FCnschte Kombination mit Strg, Alt, Umschalt oder Win. Entf entfernt es und Esc bricht ab.");
            reg("Print Screen opens Windows Snipping Tool", "Impr Pant abre Recortes de Windows", "Druck-Taste \u00F6ffnet Windows-Snipping Tool");
            reg("To capture with Stackshot it must be freed. Only changes for your user account.",
                "Para que capture con Stackshot hay que liberarla. Solo cambia en tu usuario.",
                "Damit Stackshot aufnehmen kann, muss sie freigegeben werden. Gilt nur f\u00FCr dein Benutzerkonto.");

            // Tray Panel
            reg("Capture full screen", "Capturar pantalla completa", "Vollbild aufnehmen");
            reg("Capture region", "Capturar regi\u00F3n", "Bereich aufnehmen");
            reg("Capture window", "Capturar ventana", "Fenster aufnehmen");
            reg("Record screen", "Grabar pantalla", "Bildschirm aufnehmen");
            reg("Open folder", "Abrir carpeta", "Ordner \u00F6ffnen");
            reg("Open video\u2026", "Abrir v\u00EDdeo\u2026", "Video \u00F6ffnen\u2026");
            reg("Latest XML", "\u00DAltimo XML", "Neuestes XML");
            reg("Close all", "Cerrar todo", "Alle schlie\u00DFen");
            reg("Pause recording", "Pausar grabaci\u00F3n", "Aufnahme pausieren");
            reg("Resume recording", "Reanudar grabaci\u00F3n", "Aufnahme fortsetzen");
            reg("Stop recording", "Detener grabaci\u00F3n", "Aufnahme beenden");

            // Editor Tools & Tooltips
            reg("Arrow", "Flecha", "Pfeil");
            reg("Line", "L\u00EDnea", "Linie");
            reg("Rectangle", "Recuadro", "Rechteck");
            reg("Ellipse", "Elipse", "Ellipse");
            reg("Text", "Texto", "Text");
            reg("Numbers", "N\u00FAmeros", "Zahlen");
            reg("Highlight", "Resaltar", "Hervorheben");
            reg("Spotlight: dims the rest", "Foco: oscurece el resto", "Scheinwerfer: dunkelt den Rest ab");
            reg("Pixelate", "Pixelar", "Verpixeln");
            reg("Redact (safest for sensitive data)", "Tapar (lo m\u00E1s seguro para datos)", "Schw\u00E4rzen (am sichersten f\u00FCr Daten)");
            reg("Crop", "Recortar", "Zuschneiden");
            reg("Thin", "Fino", "D\u00FCnn");
            reg("Medium", "Medio", "Mittel");
            reg("Thick", "Grueso", "Dick");
            reg("Small", "Peque\u00F1o", "Klein");
            reg("Large", "Grande", "Gro\u00DF");
            reg("Backdrop color", "Color de fondo", "Hintergrundfarbe");
            reg("Padding", "Relleno", "Abstand");
            reg("Shadow", "Sombra", "Schatten");
            reg("Corner radius", "Borde redondeado", "Eckenradius");
            reg("Copy image", "Copiar imagen", "Bild kopieren");
            reg("Save image", "Guardar imagen", "Bild speichern");
            reg("Save as\u2026", "Guardar como\u2026", "Speichern unter\u2026");
            reg("Share", "Compartir", "Teilen");
            reg("Export", "Exportar", "Exportieren");
            reg("Sensitive data", "Datos sensibles", "Sensible Daten");
            reg("Find and redact ID, SSN, card, IBAN, email, phone numbers and secrets. Read locally on your PC and any mark can be removed  (Ctrl+Shift+X)",
                "Buscar y tapar DNI, NIE, n.\u00BA de la Seguridad Social, IBAN, tarjetas, correos, tel\u00E9fonos, SSN y claves. Se leen en tu equipo y puedes quitar cualquier marca  (Ctrl+May\u00FAs+X)",
                "IDs, Steuer-ID, IBAN, Karten, E-Mails, Telefonnummern und Geheimnisse erkennen und schw\u00E4rzen. Lokal verarbeitet, Markierungen entfernbar  (Strg+Umschalt+X)");
            reg("Searching\u2026", "Buscando\u2026", "Suche\u2026");
            reg("Searching for sensitive data\u2026", "Buscando datos sensibles\u2026", "Suche nach sensiblen Daten\u2026");
            reg("Could not read image.", "No se ha podido leer la imagen.", "Bild konnte nicht gelesen werden.");

            // Notices, Toasts & Alerts
            reg("Text copied", "Texto copiado", "Text kopiert");
            reg("Image copied", "Imagen copiada", "Bild kopiert");
            reg("Saved", "Guardado", "Gespeichert");
            reg("Capture saved", "Captura guardada", "Aufnahme gespeichert");
            reg("Audio clipped", "El sonido ha saturado", "Audio \u00FCbersteuert");
            reg("Cinema: lossless, verified", "Cine: sin p\u00E9rdidas, verificado", "Kino: verlustfrei, verifiziert");
            reg("Sensitive data not reviewed", "No se han revisado los datos sensibles", "Sensible Daten nicht \u00FCberpr\u00FCft");
            reg("Could not combine", "No se pudieron unir", "Konnte nicht zusammengef\u00FCgt werden");
            reg("Recording was choppy", "La grabaci\u00F3n ha ido a tirones", "Aufnahme lief unruhig");
            reg("Print Screen now belongs to Stackshot", "Impr Pant ya es de Stackshot", "Druck-Taste geh\u00F6rt jetzt zu Stackshot");
            reg("Recording recovered", "Grabaci\u00F3n recuperada", "Aufnahme wiederhergestellt");
            reg("Some shortcuts are occupied", "Algunos atajos est\u00E1n ocupados", "Einige Tastenk\u00FCrzel sind belegt");
            reg("Could not save", "No se pudo guardar", "Konnte nicht gespeichert werden");
            reg("Show in folder", "Mostrar en la carpeta", "Im Ordner anzeigen");
            reg("Could not open folder", "No se pudo abrir la carpeta", "Ordner konnte nicht ge\u00F6ffnet werden");
            reg("Could not copy", "No se pudo copiar", "Konnte nicht kopiert werden");
            reg("Could not open", "No se pudo abrir", "Konnte nicht ge\u00F6ffnet werden");

            // Markdown & XML Window
            reg("Markdown document", "Documento Markdown", "Markdown-Dokument");
            reg("XML document", "Documento XML", "XML-Dokument");
            reg("Find\u2026", "Buscar\u2026", "Suchen\u2026");
            reg("Replace\u2026", "Reemplazar\u2026", "Ersetzen\u2026");
            reg("Export to PDF", "Exportar a PDF", "Als PDF exportieren");
            reg("Export to HTML", "Exportar a HTML", "Als HTML exportieren");

            // Recording Setup
            reg("Microphone", "Micr\u00F3fono", "Mikrofon");
            reg("Frame rate", "Fotogramas", "Bildwiederholrate");
            reg("Frames per second", "Fotogramas por segundo", "Bilder pro Sekunde");
            reg("Scrolling capture", "Capturando con desplazamiento", "Scroll-Aufnahme");
            reg("Keep editing", "Seguir editando", "Weiter bearbeiten");

            deByEn = enMap;
            deByEs = esMap;
        }

        public static string LookupGerman(string en, string es)
        {
            InitGerman();
            string de;
            if (!string.IsNullOrEmpty(en) && deByEn.TryGetValue(en, out de)) return de;
            if (!string.IsNullOrEmpty(es) && deByEs.TryGetValue(es, out de)) return de;
            return null;
        }
    }
}

