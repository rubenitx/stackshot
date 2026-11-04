// Stackshot - German localization catalog.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static readonly Lazy<Tuple<Dictionary<string, string>, Dictionary<string, string>>> GermanCatalog =
            new Lazy<Tuple<Dictionary<string, string>, Dictionary<string, string>>>(BuildGerman);

        static Tuple<Dictionary<string, string>, Dictionary<string, string>> BuildGerman()
        {
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
            reg("Backdrop & editor", null, "Hintergrund und Editor");
            reg("Recording", null, "Aufnahme");
            reg("Search settings", null, "Einstellungen durchsuchen");
            reg("Open {0}", null, "{0} öffnen");
            reg("Recent captures", null, "Letzte Aufnahmen");
            reg("View all", null, "Alle ansehen");
            reg("Play with {0}", null, "Mit {0} spielen");
            reg("Ask {0} something", null, "Frag {0} etwas");
            reg("Taking a nap", null, "Macht ein Nickerchen");
            reg(" for an area", null, " für einen Bereich");
            reg(" to stop", null, " zum Anhalten");
            reg(" to record", null, " zum Aufnehmen");
            reg("Repeat", null, "Wiederholen");
            reg("Scroll", null, "Scrollen");
            reg("Timer", null, "Timer");
            reg("Stop", null, "Stopp");
            reg("Repeat last area", null, "Letzten Bereich erneut aufnehmen");
            reg("Capture active window", null, "Aktives Fenster aufnehmen");
            reg("Copy screen text", null, "Bildschirmtext kopieren");
            reg("View and sort Markdown", null, "Markdown ansehen und sortieren");
            reg("Timed capture", null, "Aufnahme mit Timer");
            reg("Record", "Grabar", "Aufnehmen");
            reg("Mascot", "Mascota", "Maskottchen");
            reg("About", "Acerca de", "\u00DCber Stackshot");

            // Appearance & Localization
            reg("Appearance", "Apariencia", "Erscheinungsbild");
            reg("Language", "Idioma", "Sprache");
            reg("Choose language", null, "Sprache auswählen");
            reg("Languages", null, "Sprachen");
            reg("Search languages", null, "Sprachen suchen");
            reg("Apply language", null, "Sprache übernehmen");
            reg("No matching languages.", null, "Keine passende Sprache gefunden.");
            reg("Search for and choose the language for Stackshot.", null, "Suche und wähle die Sprache für Stackshot.");
            reg("Search by name or code. Preview languages use English where translations are missing.", null, "Suche nach Name oder Sprachcode. In Vorschausprachen erscheinen noch nicht übersetzte Texte auf Englisch.");
            reg("Follow the Windows language", null, "Windows-Sprache übernehmen");
            reg("Preview", null, "Vorschau");
            reg("Partial translation", null, "Teilweise übersetzt");
            reg("Current", null, "Aktuell");
            reg("Interface language (automatic based on Windows, Spanish, English or German).",
                "El idioma de la interfaz (autom\u00E1tico seg\u00FAn Windows, espa\u00F1ol, ingl\u00E9s o alem\u00E1n).",
                "Sprache der Benutzeroberfl\u00E4che: automatisch wie in Windows oder manuell auf Spanisch, Englisch oder Deutsch.");
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
            reg("Save all captures", "Guardar todas las capturas", "Alle Aufnahmen speichern");
            reg("Saves to folder immediately. If off, only captures you choose to keep.", null, "Speichert Aufnahmen sofort im Zielordner. Ist die Option aus, speicherst du nur ausgewählte Aufnahmen.");
            reg("Mascot & healthy breaks", null, "Maskottchen und Pausen");
            reg("A window companion that reminds you to hydrate and rest your eyes. Can be enabled later.", null, "Das Maskottchen erinnert dich daran, Wasser zu trinken und deinen Augen eine Pause zu gönnen. Du kannst es auch später aktivieren.");
            reg("Captures folder", null, "Aufnahmeordner");
            reg("Change captures folder", null, "Aufnahmeordner ändern");
            reg("Click", null, "Klick");
            reg("Capture an area or window", null, "Bereich oder Fenster aufnehmen");
            reg("Record the screen", null, "Bildschirm aufnehmen");
            reg("on thumbnail to edit it", null, "auf die Miniaturansicht zum Bearbeiten");
            reg("Install & get started", null, "Installieren und loslegen");
            reg("Where captures are saved", "D\u00F3nde se guardan las capturas", "Speicherort f\u00FCr Aufnahmen.");
            reg("Where your saved captures go.", "Donde van las capturas que guardas.", "Hier werden deine gespeicherten Aufnahmen abgelegt.");
            reg("Where do you want to save your captures?", "\u00BFD\u00F3nde quieres guardar las capturas que conserves?", "Wo m\u00F6chtest du deine Aufnahmen speichern?");

            // Settings toggles & options
            reg("Light, dark, or follow Windows mode.", null, "Helles oder dunkles Design wählen oder die Windows-Einstellung übernehmen.");
            reg("Open .mkv files with Stackshot", null, ".mkv-Dateien mit Stackshot öffnen");
            reg("Windows default apps", null, "Standard-Apps in Windows");
            reg("Choose which program opens each file type; Stackshot never changes it on its own.", null, "Lege in Windows fest, welche App den jeweiligen Dateityp öffnet.");
            reg("Recovered recordings", null, "Wiederhergestellte Aufnahmen");
            reg(" of recordings that did not finish. Stackshot never deletes them.", null, " an unvollständigen Aufnahmen. Stackshot bewahrt diese Dateien auf.");
            reg("Currently used by Windows Snipping Tool. Only changes for your user account.", null, "Die Druck-Taste ist dem Windows-Snipping-Tool zugeordnet. Die Änderung gilt nur für dein Benutzerkonto.");
            reg("Use", null, "Verwenden");
            reg("Recents", null, "Letzte Aufnahmen");
            reg("Show recent captures on Home", null, "Letzte Aufnahmen auf der Startseite anzeigen");
            reg("Disable to hide thumbnails on Home.", null, "Schalte die Option aus, um Miniaturansichten auf der Startseite auszublenden.");
            reg("Reads text in captures locally so History can find them by content.", null, "Erkennt Text in Aufnahmen lokal, damit du im Verlauf nach ihrem Inhalt suchen kannst.");
            reg("Temporary captures retention", null, "Aufbewahrung temporärer Aufnahmen");
            reg("How long unsaved captures remain.", null, "Wie lange ungespeicherte Aufnahmen verfügbar bleiben.");
            reg("Clear recents", null, "Letzte Aufnahmen löschen");
            reg("Removes temporary captures; saved files are untouched.", null, "Löscht die temporären Aufnahmen aus der Liste.");
            reg("When capturing", null, "Beim Aufnehmen");
            reg("After selecting an area", null, "Nach der Bereichsauswahl");
            reg("Direct adds to stack; Adjust first lets you reposition/resize before Enter; Show options lets you pick capture, video, GIF or scroll.", null, "Direkt legt die Aufnahme in der Stapelansicht ab. Beim Anpassen kannst du den Bereich vor der Eingabe verschieben oder skalieren. Die Optionen bieten Bild, Video, GIF und Scrollaufnahme.");
            reg("Direct to stack", null, "Direkt zum Stapel");
            reg("Adjust before capture", null, "Vorher anpassen");
            reg("Show options", null, "Optionen anzeigen");
            reg("A gentle camera shutter sound on capture.", null, "Spielt beim Aufnehmen einen leisen Kameraton ab.");
            reg("Ready to paste immediately with Ctrl+V; videos and GIFs as files.", null, "Sofort mit Strg+V einfügen. Videos und GIFs werden als Dateien kopiert.");
            reg("Auto-hide thumbnails", null, "Miniaturansichten automatisch ausblenden");
            reg("When unused, tuck thumbnails into screen edge until hovered.", null, "Schiebt Miniaturansichten an den Bildschirmrand, bis der Mauszeiger darübersteht.");
            reg("Never", null, "Nie");
            reg("After 3 s", null, "Nach 3 s");
            reg("After 5 s", null, "Nach 5 s");
            reg("After 10 s", null, "Nach 10 s");
            reg("Countdown timer", null, "Aufnahmetimer");
            reg("Select area and counts down before capture: gives time to open menus. Esc cancels.", null, "Nach der Bereichsauswahl läuft ein Countdown. So bleibt Zeit, ein Menü zu öffnen. Esc bricht ab.");
            reg("Follow cursor across monitors", null, "Mauszeiger über Bildschirme hinweg folgen");
            reg("On multi-monitor setups, thumbnails appear on the active screen.", null, "Bei mehreren Bildschirmen erscheinen die Miniaturansichten auf dem Bildschirm mit dem Mauszeiger.");
            reg("Include mouse cursor", null, "Mauszeiger mit aufnehmen");
            reg("The mouse cursor is captured where it was when capture started.", null, "Die Aufnahme zeigt den Mauszeiger an seiner Position beim Start.");
            reg("National IDs, Social Security numbers, IBANs, cards, emails and phone numbers are covered automatically in every capture. They are read on your computer; nothing leaves it.", null, "Stackshot sucht lokal nach sensiblen Daten und Zugangsdaten und schwärzt erkannte Stellen. Prüfe das Ergebnis vor dem Teilen.");
            reg("When off, you decide: in the editor, \u201CSensitive data\u201D covers them with marks you can remove.", null, "Ist die Option aus, kannst du die Suche im Editor über „Sensible Daten“ starten und die Markierungen bearbeiten.");
            reg("Saved captures", null, "Gespeicherte Aufnahmen");
            reg("Automatically save all captures", null, "Alle Aufnahmen automatisch speichern");
            reg("Every capture and recording is saved to your folder. When off, only those manually kept.", null, "Speichert Bilder und Videos automatisch im Zielordner. Ist die Option aus, wählst du selbst, was du speicherst.");
            reg("Files not in your save folder are deleted automatically based on Recents retention (never while shown as thumbnail or in editor).", null, "Temporäre Dateien werden nach der gewählten Aufbewahrungsdauer gelöscht, sobald ihre Miniaturansicht und der Editor geschlossen sind.");
            reg(" is also used by \u201C{0}\u201D", null, " wird auch von „{0}“ verwendet");
            reg(" is being used by another program", null, " wird von einer anderen Anwendung verwendet");
            reg("and {0} more", null, "und {0} weitere");
            reg("Also: {0}", null, "Außerdem: {0}");
            reg("Windows had reserved it for Snipping Tool; it is now freed for your user. If Snipping Tool still opens, sign out and back in.", null, "Die Druck-Taste wurde für dein Konto freigegeben. Wenn sich weiterhin das Snipping-Tool öffnet, melde dich ab und wieder an.");
            reg("If another program (Snipping Tool, ShareX, Lightshot\u2026) already uses one, it will be marked in orange and Stackshot will notify you.", null, "Wenn eine andere Anwendung wie das Snipping-Tool, ShareX oder Lightshot ein Kürzel belegt, markiert Stackshot es orange und zeigt einen Hinweis.");
            reg("A profile only changes a few settings at once; you can tweak any of them later.", null, "Ein Profil ändert mehrere Einstellungen auf einmal. Du kannst jede davon später einzeln anpassen.");
            reg("You changed {0} relative to this profile. ", null, "Du hast gegenüber diesem Profil Folgendes geändert: {0}. ");
            reg("Reapply \u201C{0}\u201D", null, "„{0}“ erneut anwenden");
            reg("the launch animation", null, "die Startanimation");
            reg("mascot greetings", null, "die Begrüßungen des Maskottchens");
            reg("break reminders", null, "die Pausenerinnerungen");
            reg("activity comments", null, "die Kommentare zu Aktivitäten");
            reg("How Stackshot starts and behaves when you capture.", null, "Start- und Aufnahmeverhalten von Stackshot.");
            reg("Advanced", null, "Erweitert");
            reg("Check for updates", null, "Nach Updates suchen");
            reg("Install updates automatically", null, "Updates automatisch installieren");
            reg("Show mascot", null, "Maskottchen anzeigen");
            reg("Greetings and tips", null, "Begrüßungen und Tipps");
            reg("Mascot on desktop", null, "Maskottchen auf dem Desktop");
            reg("Auto-redact sensitive data on capture", null, "Sensible Daten beim Aufnehmen automatisch schwärzen");
            reg("Search captures by OCR text", null, "Aufnahmen nach erkanntem Text durchsuchen");
            reg("Open new XML files from a folder", null, "Neue XML-Dateien aus einem Ordner öffnen");
            reg("Designed for programs that save SOAP messages as XML files in a folder. In the viewer, [ and ] navigate between XML files, and \u201CMore options\u201D lists the latest ones that arrived.", null, "Für Programme, die SOAP-Nachrichten als XML-Dateien ablegen. In der Ansicht wechselst du mit [ und ] zwischen Dateien. Unter „Weitere Optionen“ findest du die zuletzt eingegangenen Dateien.");
            reg("Every XML file appearing in that folder opens automatically in the Stackshot viewer.", null, "Neue XML-Dateien in diesem Ordner öffnen sich automatisch in der Stackshot-Ansicht.");
            reg("Choose the folder to monitor below.", null, "Wähle unten den Ordner aus, den Stackshot überwachen soll.");
            reg("Folder does not exist yet; Stackshot checks again every minute.", null, "Der Ordner existiert noch nicht. Stackshot prüft jede Minute erneut.");
            reg("Active: new XML files open automatically in the viewer.", null, "Aktiv. Neue XML-Dateien öffnen sich automatisch in der Ansicht.");
            reg("No folder", null, "Kein Ordner");
            reg("Folder chosen by you.", null, "Der von dir gewählte Ordner.");
            reg("Choose the folder where XML files appear.", null, "Wähle den Ordner, in dem neue XML-Dateien abgelegt werden.");
            reg("Choose\u2026", null, "Auswählen\u2026");
            reg("Which folder to monitor? Every new XML appearing in it opens in viewer.", null, "Welchen Ordner möchtest du überwachen? Neue XML-Dateien darin öffnen sich automatisch in der Ansicht.");
            reg("Stackshot now appears among the apps that can open {0}.", null, "Stackshot steht jetzt zum Öffnen von {0} zur Auswahl.");
            reg("Choose Stackshot for {0} in the Windows window that opens now: Windows asks you to make that choice yourself.", null, "Wähle im Windows-Dialog Stackshot zum Öffnen von {0} aus.");
            reg("Open that window", null, "Fenster öffnen");
            reg("Double-click a {0} (Cinema masters that Windows Media Player won't open) to view in Stackshot player at actual size.", null, "Öffne eine {0}-Datei per Doppelklick, um sie im Stackshot-Player in Originalgröße wiederzugeben. Das gilt auch für Cinema-Masterdateien, die Windows Media Player nicht öffnet.");
            reg("Double-click a {0} to open it in the Stackshot viewer.", null, "Öffne eine {0}-Datei per Doppelklick in der Stackshot-Ansicht.");
            reg("Stackshot is the default app for {0}.", null, "Stackshot ist die Standard-App für {0}.");
            reg("Registered, but Windows opens {0} with another app. Choose Stackshot in Default Apps.", null, "Stackshot ist registriert, aber Windows öffnet {0} mit einer anderen App. Wähle Stackshot unter „Standard-Apps“ aus.");
            reg("Delete temporary captures?", null, "Temporäre Aufnahmen löschen?");
            reg("Saved captures in your folder will not be touched.", null, "Aufnahmen in deinem Speicherordner bleiben erhalten.");
            reg("Recent captures cleared", null, "Liste der letzten Aufnahmen geleert");
            reg("Temporary captures have been deleted. Those in your folder remain.", null, "Temporäre Aufnahmen wurden gelöscht. Die Dateien in deinem Speicherordner bleiben erhalten.");
            reg("Downloaded, signature verified and installed automatically when your PC is idle.", null, "Stackshot lädt Updates herunter, prüft die Signatur und installiert sie automatisch, wenn dein PC im Leerlauf ist.");
            reg("Managed by IT on this PC: Stackshot will only notify you of new versions.", null, "Die IT verwaltet Updates auf diesem PC. Stackshot weist nur auf neue Versionen hin.");
            reg("Only with Stackshot installed; portable copy cannot self-install.", null, "Die automatische Installation ist nur für installierte Versionen verfügbar.");
            reg("Requires \u201CCheck for updates\u201D: cannot install without checking.", null, "Aktiviere dafür „Nach Updates suchen“.");
            reg("Checks GitHub for new versions on launch and every 3 hours. Does not send any personal data.", null, "Sucht beim Start und danach alle drei Stunden auf GitHub nach neuen Versionen. Dabei werden keine personenbezogenen Daten gesendet.");
            reg("Mascot settings", null, "Maskottchen-Einstellungen");
            reg("Personality, dialogue, and roaming the desktop.", null, "Persönlichkeit, Dialoge und Verhalten auf dem Desktop.");
            reg("Character", null, "Figur");
            reg("On Home and at the bottom of the sidebar.", null, "Auf der Startseite und unten in der Seitenleiste.");
            reg("I'm back!", null, "Ich bin wieder da!");
            reg("Personality", null, "Persönlichkeit");
            reg("What it says and how it moves.", null, "Was das Maskottchen sagt und wie es sich bewegt.");
            reg("Occasionally shares tips and tricks about Stackshot.", null, "Gibt gelegentlich Tipps zu Stackshot.");
            reg("Seasonal costume", null, "Kostüm zur Jahreszeit");
            reg("Without a hat, wears a pumpkin on Halloween and Santa hat at Christmas.", null, "Ohne eigenen Hut trägt das Maskottchen zu Halloween einen Kürbis und zu Weihnachten eine Weihnachtsmütze.");
            reg("On desktop", null, "Auf dem Desktop");
            reg("Walks along taskbar, looks around, plays and naps.", null, "Läuft auf der Taskleiste herum, schaut sich um, spielt und schläft.");
            reg("Only with Full profile (in General \u203A Profile).", null, "Nur mit dem vollständigen Profil unter „Allgemein › Profil“.");
            reg("Enable \u201CShow mascot\u201D first.", null, "Aktiviere zuerst „Maskottchen anzeigen“.");
            reg("Climb on windows", null, "Auf Fenster klettern");
            reg("Occasionally jumps onto active window and walks on top. Moves along with it.", null, "Springt gelegentlich auf das aktive Fenster und läuft darauf herum. Bewegt sich mit dem Fenster.");
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
            reg("Space", null, "Leertaste");
            reg("Recent", null, "Zuletzt");
            reg("Today, ", null, "Heute, ");
            reg("Yesterday, ", null, "Gestern, ");
            reg("Area", null, "Bereich");
            reg("Repeat area", null, "Bereich erneut aufnehmen");
            reg("Area in {0} s", null, "Bereich in {0} s");
            reg("Screen", null, "Bildschirm");
            reg("Window", null, "Fenster");
            reg("Finish scrolling", null, "Scrollaufnahme beenden");
            reg("Scrolling", null, "Scrollaufnahme");
            reg("Video", null, "Video");
            reg("Open Stackshot", null, "Stackshot öffnen");
            reg("Open saved captures", null, "Gespeicherte Aufnahmen öffnen");
            reg("Open latest XML", null, "Neueste XML-Datei öffnen");
            reg("Close all thumbnails", null, "Alle Miniaturansichten schließen");
            reg("Settings\u2026", null, "Einstellungen\u2026");
            reg("Recording paused", null, "Aufnahme pausiert");
            reg("Recording screen", null, "Bildschirmaufnahme läuft");
            reg("Scrolling capture in progress", null, "Scrollaufnahme läuft");
            reg("1 capture today", null, "Heute 1 Aufnahme");
            reg("{0} captures today", null, "Heute {0} Aufnahmen");
            reg("Ready to capture", null, "Bereit zur Aufnahme");
            reg("Capture an area", null, "Bereich aufnehmen");
            reg("Record a video", null, "Video aufnehmen");
            reg("Open my captures", null, "Meine Aufnahmen öffnen");
            reg("Add a quick link\u2026", null, "Schnelllink hinzufügen\u2026");
            reg("Quick links\u2026", null, "Schnelllinks\u2026");
            reg(" more", null, " weitere");
            reg("Remind me of something\u2026", null, "Erinnere mich an etwas\u2026");
            reg(" pending", null, " offen");
            reg("Note something\u2026", null, "Notiz schreiben\u2026");
            reg("Focus ", null, "Fokus ");
            reg("Take a break with me", null, "Mach eine Pause mit mir");
            reg("+1 glass", null, "+1 Glas");
            reg("How's your day going?", null, "Wie läuft dein Tag?");
            reg("My activity", null, "Meine Aktivität");
            reg(" steps", null, " Schritte");
            reg("Connect my watch", null, "Meine Uhr verbinden");
            reg("Opening the sign-in page in your browser.", null, "Die Anmeldeseite wird im Browser geöffnet.");
            reg("Change my outfit", null, "Mein Outfit ändern");
            reg("Go home", null, "Nach Hause gehen");
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

            RegisterGermanEditor(reg);

            // Notices, Toasts & Alerts
            reg("You cancelled the connection.", null, "Du hast die Verbindung abgebrochen.");
            reg("The Google Health connection is not set up.", null, "Die Verbindung zu Google Health ist noch nicht eingerichtet.");
            reg("Port {0} is busy: close whatever uses it and try again.", null, "Port {0} ist belegt. Schließe die Anwendung, die ihn verwendet, und versuche es erneut.");
            reg("Could not open login: ", null, "Die Anmeldeseite konnte nicht geöffnet werden: ");
            reg("Could not connect: ", null, "Verbindung fehlgeschlagen: ");
            reg("Page not found", null, "Seite nicht gefunden");
            reg("This address does not belong to Stackshot.", null, "Diese Adresse gehört nicht zu Stackshot.");
            reg("Connection cancelled", null, "Verbindung abgebrochen");
            reg("Nothing was connected. You can close this tab and try again whenever you like from Stackshot.", null, "Du kannst diesen Tab schließen und die Verbindung jederzeit in Stackshot erneut starten.");
            reg("Could not connect", null, "Verbindung fehlgeschlagen");
            reg("Google returned “{0}”. Close this tab and try again from Stackshot.", null, "Google hat „{0}“ zurückgegeben. Schließe diesen Tab und versuche es in Stackshot erneut.");
            reg("Google said: {0}", null, "Antwort von Google: {0}");
            reg("Invalid response", null, "Ungültige Antwort");
            reg("This response is not from the login opened by Stackshot. Try again from the app.", null, "Die Antwort gehört nicht zu der von Stackshot gestarteten Anmeldung. Versuch es in der App erneut.");
            reg("Watch ", null, "Uhr ");
            reg("connected", null, "verbunden");
            reg("Stackshot can now see your steps, activity and sleep. You can close this tab and return to the app.", null, "Stackshot kann jetzt deine Schrittzahl sowie Aktivitäts- und Schlafdaten lesen. Du kannst diesen Tab schließen und zur App zurückkehren.");
            reg(" Close this tab and try again from Stackshot.", null, " Schließe diesen Tab und versuche es in Stackshot erneut.");
            reg("Login timed out. Try again.", null, "Zeitlimit für die Anmeldung überschritten. Versuch es erneut.");
            reg("Google no longer accepts the connection: connect your watch again.", null, "Google akzeptiert die Verbindung nicht mehr. Verbinde deine Uhr erneut.");
            reg("The relay is missing the Google key.", null, "Dem Vermittlungsserver fehlt der Google-Schlüssel.");
            reg("Google is not responding ({0}).", null, "Google antwortet nicht ({0}).");
            reg("No connection to Google.", null, "Keine Verbindung zu Google.");
            reg("Google did not provide the keys.", null, "Google hat keine Zugriffstoken geliefert.");
            reg("Google did not provide a lasting key: connect again.", null, "Google hat kein Aktualisierungstoken geliefert. Stelle die Verbindung erneut her.");
            reg("Not connected.", null, "Keine Verbindung.");
            reg("Google won't let Stackshot read the data: check that the watch is linked in the Google Health app.", null, "Google erlaubt Stackshot den Zugriff auf die Daten nicht. Prüfe, ob deine Uhr in der Google-Health-App verknüpft ist.");
            reg("Google asks to wait a moment.", null, "Google bittet um einen Moment Geduld.");
            reg("As soon as the recording finishes saving, Stackshot will close and reopen.", null, "Stackshot startet neu, sobald die Aufnahme gespeichert ist.");
            reg("Stackshot will close and reopen in a few seconds.", null, "Stackshot startet in wenigen Sekunden neu.");
            reg("SHA-256 hash and digital signature.", null, "SHA-256-Prüfsumme und digitale Signatur.");
            reg("Connecting to GitHub\u2026", null, "Verbindung zu GitHub wird hergestellt\u2026");
            reg("signature checked afterwards", null, "Signatur wird danach geprüft");
            reg("What's new: {0}.\n", null, "Neu in dieser Version: {0}.\n");
            reg("Already downloaded and verified. Upon installing, Stackshot will close and reopen.", null, "Die Datei wurde bereits heruntergeladen und geprüft. Nach der Installation startet Stackshot neu.");
            reg("Do you want to download it? Its signature will be verified and it installs in seconds.", null, "Möchtest du das Update herunterladen? Stackshot prüft die Signatur und installiert das Update in wenigen Sekunden.");
            reg("Do you want to download it? This version is downloaded from its GitHub page.", null, "Möchtest du diese Version von GitHub herunterladen?");
            reg("See what's new \u203A", null, "Neuerungen ansehen \u203A");
            reg("Downloading Stackshot {0}", null, "Stackshot {0} wird heruntergeladen");
            reg("Verifying download\u2026", null, "Download wird geprüft\u2026");
            reg("Installing Stackshot {0}", null, "Stackshot {0} wird installiert");
            reg("Hide", null, "Ausblenden");
            reg("Could not update to {0}", null, "Update auf {0} fehlgeschlagen");
            reg("View version", null, "Version ansehen");
            reg("Stackshot {0} ready to install", null, "Stackshot {0} ist bereit zur Installation");
            reg("Install now", null, "Jetzt installieren");
            reg("Stackshot {0} available", null, "Stackshot {0} ist verfügbar");
            reg("Download and install", null, "Herunterladen und installieren");
            reg("Updates are managed by IT on this computer: request the latest Stackshot.msi.", null, "Die IT verwaltet die Updates auf diesem Computer. Bitte frag nach der neuesten Stackshot.msi.");
            reg("On this machine updates are managed by IT. Would you like to view the new version?", null, "Die IT verwaltet die Updates auf diesem Computer. Möchtest du dir die neue Version ansehen?");
            reg("Could not save capture", null, "Aufnahme konnte nicht gespeichert werden");
            reg("The capture was discarded because it could not be checked for sensitive data.", null, "Die Aufnahme wurde verworfen, weil sie nicht auf sensible Daten geprüft werden konnte.");
            reg("Checking data\u2026", null, "Daten werden geprüft\u2026");
            reg("Unchecked", null, "Ungeprüft");
            reg("1 piece of data redacted", null, "Eine Stelle geschwärzt");
            reg("{0} items redacted", null, "{0} Stellen geschwärzt");
            reg("Could not check the image.", null, "Das Bild konnte nicht geprüft werden.");
            reg("Not enough memory to check such a large image.", null, "Für die Prüfung dieses großen Bildes ist nicht genug Arbeitsspeicher verfügbar.");
            reg("This Windows does not have text recognition (OCR) installed.", null, "Auf diesem Windows-System ist die Texterkennung (OCR) nicht installiert.");
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

            RegisterGermanDocuments(reg);

            RegisterGermanCapture(reg);

            return Tuple.Create(enMap, esMap);
        }

        public static string LookupGerman(string en, string es)
        {
            var catalog = GermanCatalog.Value;
            string de;
            if (!string.IsNullOrEmpty(en) && catalog.Item1.TryGetValue(en, out de)) return de;
            if (!string.IsNullOrEmpty(es) && catalog.Item2.TryGetValue(es, out de)) return de;
            return null;
        }
    }
}
