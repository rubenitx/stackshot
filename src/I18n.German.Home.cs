// Stackshot - German localization catalog: Home (pages, tray, welcome, sticky note, links).
// MIT License - https://github.com/rubenitx/stackshot
using System;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterGermanHome(Action<string, string, string> reg)
        {
            // Sidebar and section names
            reg("Sections", "Secciones", "Abschnitte");
            reg("Version {0}", "Versi\u00F3n {0}", "Version {0}");
            reg("Level {0} \u00B7 {1}", "Nivel {0} \u00B7 {1}", "Level {0} \u00B7 {1}");
            reg("{0} capture to level {1}", "{0} captura para el nivel {1}", "Noch {0} Aufnahme bis Level {1}");
            reg("{0} captures to level {1}", "{0} capturas para el nivel {1}", "Noch {0} Aufnahmen bis Level {1}");
            reg("Maximum level", "Nivel m\u00E1ximo", "H\u00F6chstes Level");
            reg(" I'm {0}. What to capture?", " Soy {0}. \u00BFQu\u00E9 capturamos?", " Ich bin {0}. Was nehmen wir auf?");
            reg(" I'm {0}.", " Soy {0}.", " Ich bin {0}.");
            reg("Capture an area of the screen", "Capturar un \u00E1rea de la pantalla", "Bereich vom Bildschirm aufnehmen");
            reg("Version {0} available", "Versi\u00F3n {0} disponible", "Version {0} verf\u00FCgbar");
            reg("Recording\u2026", "Grabando\u2026", "Aufnahme l\u00E4uft\u2026");
            reg("Go to Home", "Ir a Inicio", "Zur Startseite");
            reg("Press the combination you want; Del removes it and Esc cancels", "Pulsa la combinaci\u00F3n que quieras; Supr la quita y Esc cancela", "Dr\u00FCcke die gew\u00FCnschte Kombination; Entf entfernt sie, Esc bricht ab");
            reg("Recording settings", "Ajustes de grabaci\u00F3n", "Aufnahmeeinstellungen");

            // Home views and mode bar
            reg("Today", "Hoy", "Heute");
            reg("Yesterday", "Ayer", "Gestern");
            reg("This week", "Esta semana", "Diese Woche");
            reg("This month", "Este mes", "Dieser Monat");
            reg("Earlier", "Anteriores", "Fr\u00FCher");
            reg("An area", "Un \u00E1rea", "Ein Bereich");
            reg("Last area", "La \u00FAltima \u00E1rea", "Letzter Bereich");
            reg("Full screen", "Pantalla completa", "Ganzer Bildschirm");
            reg("A window", "Una ventana", "Ein Fenster");
            reg("Animated GIF", "GIF animado", "Animiertes GIF");
            reg("With timer", "Con temporizador", "Mit Timer");
            reg(" and ", " y ", " und ");
            reg("was assigned to \u201C{0}\u201D", "era de \u00AB{0}\u00BB", "geh\u00F6rte zu \u201E{0}\u201C");
            reg(", which now has no shortcut.", ", que se ha quedado sin atajo.", ", die jetzt kein Tastenk\u00FCrzel mehr hat.");
            reg("; it now belongs to \u201C{0}\u201D.", "; ahora es de \u00AB{0}\u00BB.", "; es geh\u00F6rt jetzt zu \u201E{0}\u201C.");
            reg("On the desktop it consumes slightly more: ~10 MB RAM and ~1% CPU while moving (almost none while sleeping). Automatically hides during full-screen games or presentations, and never appears in screenshots or screen shares. Click to open menu, rapid clicks to tickle; drag anywhere you like.",
                "En el escritorio consume algo m\u00E1s: unos 10 MB de memoria y en torno al 1 % de CPU mientras se mueve (casi nada cuando duerme). Se esconde sola con juegos o presentaciones a pantalla completa y nunca sale en tus capturas ni al compartir pantalla. Un clic abre su men\u00FA y varios seguidos le hacen cosquillas; puedes arrastrarla a donde quieras.",
                "Auf dem Desktop braucht es etwas mehr: rund 10 MB Arbeitsspeicher und etwa 1 % CPU w\u00E4hrend der Bewegung (fast nichts im Schlaf). Bei Vollbildspielen oder Pr\u00E4sentationen versteckt es sich von selbst und erscheint nie in deinen Aufnahmen oder bei geteiltem Bildschirm. Ein Klick \u00F6ffnet sein Men\u00FC, mehrere Klicks hintereinander kitzeln es; ziehe es, wohin du willst.");
            reg("Latest capture", "\u00DAltima captura", "Letzte Aufnahme");
            reg("Your latest capture will appear here", "Aqu\u00ED ver\u00E1s tu \u00FAltima captura", "Hier erscheint deine letzte Aufnahme");
            reg("Captures today", "Capturas hoy", "Aufnahmen heute");
            reg(" vs yesterday", " vs ayer", " vs gestern");
            reg("Level {0}", "Nivel {0}", "Level {0}");
            reg("{0} to level up", "{0} para subir", "Noch {0} bis zum n\u00E4chsten Level");
            reg("Mascot hidden", "Mascota escondida", "Maskottchen ausgeblendet");
            reg("Show in Mascot", "Mostrar en Mascota", "Unter Maskottchen anzeigen");
            reg("with sound", "con sonido", "mit Ton");
            reg("no sound", "sin sonido", "ohne Ton");
            reg("Folder", "Carpeta", "Ordner");
            reg("Tip", "Truco", "Tipp");
            reg("Copied!", "\u00A1Copiada!", "Kopiert!");
            reg("Could not read capture or clipboard is busy. Please try again.", "No se ha podido leer la captura o el portapapeles est\u00E1 ocupado. Vuelve a intentarlo.", "Die Aufnahme oder die Zwischenablage konnte nicht gelesen werden. Versuch es erneut.");
            reg("Rename", null, "Umbenennen");
            reg("Ready", null, "Bereit");
            reg("My name is now {0}!", "\u00A1Ahora me llamo {0}!", "Ich hei\u00DFe jetzt {0}!");
            reg("Empty", "Vac\u00EDa", "Leer");
            reg("Show", "Mostrar", "Zeigen");

            // History page
            reg("Your captures and recordings, newest first.", "Tus capturas y grabaciones, de la m\u00E1s reciente a la m\u00E1s antigua.", "Deine Aufnahmen und Videos, vom neuesten zum \u00E4ltesten.");
            reg("All", "Todo", "Alle");
            reg("Videos", "V\u00EDdeos", "Videos");
            reg("Search by text or name", "Buscar por texto o nombre", "Nach Text oder Name suchen");
            reg("Search by name", "Buscar por nombre", "Nach Name suchen");
            reg("This Windows does not have text recognition (OCR): searching by name only for now.", "Este Windows no tiene el reconocimiento de texto (OCR): de momento se busca solo por el nombre.", "Dieses Windows hat keine Texterkennung (OCR): vorerst wird nur nach dem Namen gesucht.");
            reg("Reading capture text\u2026 {0} of {1}", "Leyendo el texto de las capturas\u2026 {0} de {1}", "Text der Aufnahmen wird gelesen\u2026 {0} von {1}");
            reg(" \u00B7 paused while recording", " \u00B7 en pausa mientras grabas", " \u00B7 pausiert beim Aufnehmen");
            reg(" \u00B7 paused while capturing", " \u00B7 en pausa mientras capturas", " \u00B7 pausiert beim Erfassen");
            reg("Text search is turned off", "La b\u00FAsqueda por texto est\u00E1 desactivada", "Textsuche ist ausgeschaltet");
            reg("Stackshot can read text from each capture on your device, without anything leaving your computer, so you can find captures by their content. Currently searching by name only. ",
                "Stackshot puede leer el texto de cada captura en tu equipo, sin que nada salga de \u00E9l, para que la encuentres por lo que pon\u00EDa. Ahora solo se busca por el nombre. ",
                "Stackshot kann den Text jeder Aufnahme auf deinem Ger\u00E4t lesen, ohne dass etwas deinen Computer verl\u00E4sst, damit du sie nach ihrem Inhalt findest. Derzeit wird nur nach dem Namen gesucht. ");
            reg("Turn on text search", "Activar la b\u00FAsqueda por texto", "Textsuche einschalten");
            reg(" (also in General\u00A0\u203A\u00A0Recents).", " (tambi\u00E9n est\u00E1 en General\u00A0\u203A\u00A0Recientes).", " (auch unter Allgemein\u00A0\u203A\u00A0Letzte Aufnahmen).");
            reg("No captures yet", "Todav\u00EDa no hay capturas", "Noch keine Aufnahmen");
            reg("Captures and recordings you make will appear here, grouped by day.", "Las capturas y grabaciones que hagas aparecer\u00E1n aqu\u00ED, ordenadas por d\u00EDas.", "Aufnahmen und Videos, die du machst, erscheinen hier nach Tagen sortiert.");
            reg("Nothing matches \u201C{0}\u201D", "Nada coincide con \u00AB{0}\u00BB", "Nichts passt zu \u201E{0}\u201C");
            reg("Searching by name only: text search is disabled.", "Solo se busca por el nombre: la b\u00FAsqueda por texto est\u00E1 desactivada.", "Es wird nur nach dem Namen gesucht: die Textsuche ist ausgeschaltet.");
            reg("Still reading text from some captures: try again in a moment.", "Todav\u00EDa se est\u00E1 leyendo el texto de algunas capturas: prueba otra vez dentro de un momento.", "Der Text einiger Aufnahmen wird noch gelesen: versuch es gleich noch einmal.");
            reg("Try different words or switch to \u201CAll\u201D.", "Prueba con otras palabras o con \u00ABTodo\u00BB.", "Versuch es mit anderen W\u00F6rtern oder mit \u201EAlle\u201C.");
            reg("Try different words: searching in both text and file names.", "Prueba con otras palabras: se busca en el texto de cada captura y en su nombre.", "Versuch es mit anderen W\u00F6rtern: gesucht wird im Text und im Namen jeder Aufnahme.");
            reg("No videos", "No hay v\u00EDdeos", "Keine Videos");
            reg("No GIFs", "No hay GIF", "Keine GIFs");
            reg("No captures", "No hay capturas", "Keine Aufnahmen");
            reg("Try \u201CAll\u201D.", "Prueba con \u00ABTodo\u00BB.", "Versuch es mit \u201EAlle\u201C.");
            reg("1 result", "1 resultado", "1 Ergebnis");
            reg("{0} results", "{0} resultados", "{0} Ergebnisse");
            reg("1 item", "1 elemento", "1 Element");
            reg("{0} items", "{0} elementos", "{0} Elemente");
            reg("{0} cannot be opened in the editor.", null, "{0} l\u00E4sst sich im Editor nicht \u00F6ffnen.");
            reg("Capture not found", "No se encuentra la captura", "Aufnahme nicht gefunden");
            reg("{0} is no longer in its folder: moved or deleted.", null, "{0} ist nicht mehr in seinem Ordner: verschoben oder gel\u00F6scht.");
            reg(" While search remains on, Stackshot will gradually re-read them.", " Mientras la b\u00FAsqueda siga activada, Stackshot volver\u00E1 a leerlas poco a poco.", " Bei eingeschalteter Suche liest Stackshot sie nach und nach erneut.");
            reg("Delete text index?", "\u00BFBorrar el \u00EDndice de texto?", "Textindex l\u00F6schen?");
            reg("Your captures are untouched.", "Tus capturas no se tocan.", "Deine Aufnahmen bleiben unber\u00FChrt.");
            reg("Delete index", "Borrar el \u00EDndice", "Index l\u00F6schen");
            reg("Delete text index", "Borrar el \u00EDndice de texto", "Textindex l\u00F6schen");
            reg("What Stackshot has indexed from your captures ({0}). Rebuilt automatically while search is on.",
                "Lo que Stackshot ha le\u00EDdo en tus capturas ({0}). Se vuelve a crear solo mientras la b\u00FAsqueda est\u00E9 activada.",
                "Was Stackshot aus deinen Aufnahmen gelesen hat ({0}). Wird automatisch neu erstellt, solange die Suche eingeschaltet ist.");

            // Backdrop and editor
            reg("Like CleanShot X: your captures ready to present with a beautiful backdrop, padding, and shadow. In the editor, use the Backdrop button (or key B); also for videos and GIFs with \u201CPresent\u201D.",
                "Como en CleanShot X: tus capturas, listas para presentar con un fondo bonito, margen y sombra. En el editor, bot\u00F3n Fondo (o la tecla B); tambi\u00E9n para v\u00EDdeos y GIF con \u00ABPresentar\u00BB.",
                "Wie CleanShot X: deine Aufnahmen, bereit zum Pr\u00E4sentieren mit sch\u00F6nem Hintergrund, Abstand und Schatten. Im Editor die Schaltfl\u00E4che Hintergrund (oder Taste B); auch f\u00FCr Videos und GIFs mit \u201EPr\u00E4sentieren\u201C.");
            reg("Backdrop preview", "Vista previa del fondo", "Hintergrund-Vorschau");
            reg("Custom backdrops", "Tus fondos", "Eigene Hintergr\u00FCnde");
            reg("You are using your own image. You can add more or remove this one.", "Est\u00E1s usando una imagen tuya. Puedes a\u00F1adir m\u00E1s o quitar esta.", "Du verwendest ein eigenes Bild. Du kannst weitere hinzuf\u00FCgen oder dieses entfernen.");
            reg("Add a photo or image and use it as backdrop for your captures.", "A\u00F1ade una foto o imagen tuya y \u00FAsala como fondo de tus capturas.", "F\u00FCge ein eigenes Foto oder Bild hinzu und verwende es als Hintergrund f\u00FCr deine Aufnahmen.");
            reg("Add image\u2026", "A\u00F1adir imagen\u2026", "Bild hinzuf\u00FCgen\u2026");
            reg("Remove this backdrop", "Quitar este fondo", "Diesen Hintergrund entfernen");
            reg("Deletes the copy saved in Stackshot; your original image is untouched.", "Se borra la copia guardada en Stackshot; tu imagen original no se toca.", "L\u00F6scht die in Stackshot gespeicherte Kopie; dein Originalbild bleibt unber\u00FChrt.");
            reg("Open editor with backdrop enabled", "Abrir el editor con el fondo puesto", "Editor mit Hintergrund \u00F6ffnen");
            reg("Otherwise, enable it with the Backdrop button anytime.", "Si no, se pone con el bot\u00F3n Fondo cuando quieras.", "Sonst schaltest du ihn jederzeit mit der Schaltfl\u00E4che Hintergrund ein.");
            reg("Corners", "Esquinas", "Ecken");
            reg("Sharp", "Rectas", "Eckig");
            reg("Soft", "Suaves", "Sanft");
            reg("Round", "Redondas", "Rund");
            reg("Custom, from editor", "A tu medida, desde el editor", "Eigene, aus dem Editor");
            reg("In the editor you have more: plain colors, blurred capture, inset, alignment, and sliders for padding, shadow, and corners; what you last used is saved here.",
                "En el editor tienes m\u00E1s: colores lisos, la captura desenfocada, inserci\u00F3n, alineaci\u00F3n y deslizadores para el margen, la sombra y las esquinas; lo \u00FAltimo que uses se queda guardado aqu\u00ED.",
                "Im Editor gibt es mehr: Volltonfarben, unscharfe Aufnahme, Randabstand, Ausrichtung und Schieberegler f\u00FCr Abstand, Schatten und Ecken; das zuletzt Verwendete wird hier gespeichert.");
            reg("Reset style", "Restablecer el estilo", "Stil zur\u00FCcksetzen");
            reg("You already have {0} custom backdrops. Remove one to add another.", "Ya tienes {0} fondos propios. Quita uno para a\u00F1adir otro.", "Du hast bereits {0} eigene Hintergr\u00FCnde. Entferne einen, um einen weiteren hinzuzuf\u00FCgen.");
            reg("Choose an image to use as backdrop", "Elige una imagen para usar de fondo", "Bild ausw\u00E4hlen, das als Hintergrund verwendet wird");
            reg("Backdrops", "Fondos", "Hintergr\u00FCnde");
            reg("Plain color", "Color liso", "Volltonfarbe");
            reg("Blurred capture", "La captura desenfocada", "Unscharfe Aufnahme");

            // About and updates
            reg("Version {0}  \u00B7  Free and open source (MIT license)", "Versi\u00F3n {0}  \u00B7  Libre y gratuito (licencia MIT)", "Version {0}  \u00B7  Frei und kostenlos (MIT-Lizenz)");
            reg("Capture, markup and share in seconds. Floating thumbnails, quick editor, video and GIF.", "Captura, marca y comparte en segundos. Miniaturas flotantes, editor r\u00E1pido, v\u00EDdeo y GIF.", "Aufnehmen, markieren und in Sekunden teilen. Schwebende Miniaturansichten, schneller Editor, Video und GIF.");
            reg("View on GitHub", "Ver en GitHub", "Auf GitHub ansehen");
            reg("Could not open browser", "No se pudo abrir el navegador", "Browser konnte nicht ge\u00F6ffnet werden");
            reg("Data folder", "Carpeta de datos", "Datenordner");
            reg("What's new", "Novedades", "Neuigkeiten");
            reg("Report an issue", "Informar de un problema", "Problem melden");
            reg("My reports", "Mis reportes", "Meine Meldungen");
            reg("Could not open uninstaller", "No se pudo abrir el desinstalador", "Deinstaller konnte nicht ge\u00F6ffnet werden");
            reg("Uninstall Stackshot from Windows Settings > Apps.", "Desinstala Stackshot desde Configuraci\u00F3n > Aplicaciones de Windows.", "Deinstalliere Stackshot unter Windows-Einstellungen > Apps.");
            reg("Uninstall Stackshot\u2026", "Desinstalar Stackshot\u2026", "Stackshot deinstallieren\u2026");
            reg("You have version {0}. Stackshot updates are managed by your organization.", "Tienes la versi\u00F3n {0}. Las actualizaciones de Stackshot las gestiona tu organizaci\u00F3n.", "Du hast Version {0}. Die Updates f\u00FCr Stackshot verwaltet deine Organisation.");
            reg("Verifying Stackshot {0}", "Comprobando Stackshot {0}", "Stackshot {0} wird gepr\u00FCft");
            reg("Verifying SHA-256 checksum and signature before installing.", "Se comprueban su suma SHA-256 y su firma antes de instalar nada.", "SHA-256-Pr\u00FCfsumme und Signatur werden vor der Installation gepr\u00FCft.");
            reg("Stackshot will restart in a few seconds.", "Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse en unos segundos.", "Stackshot wird in wenigen Sekunden neu gestartet.");
            reg("Downloaded and verified. ", "Ya est\u00E1 descargada y verificada. ", "Heruntergeladen und gepr\u00FCft. ");
            reg("Will install automatically when system is idle.", "Se instalar\u00E1 sola en cuanto no est\u00E9s usando el equipo.", "Wird automatisch installiert, wenn der PC im Leerlauf ist.");
            reg("When installed, Stackshot will restart in a few seconds.", "Al instalarla, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse en unos segundos.", "Nach der Installation startet Stackshot in wenigen Sekunden neu.");
            reg("Download now? Signature is verified and installed in seconds; Stackshot restarts automatically.", "\u00BFQuieres descargarla? Se comprueba su firma y se instala en unos segundos; Stackshot se vuelve a abrir solo.", "Jetzt herunterladen? Signatur wird gepr\u00FCft und in Sekunden installiert; Stackshot startet danach von selbst neu.");
            reg("Download now? This copy of Stackshot is portable: download from GitHub.", "\u00BFQuieres descargarla? Esta copia de Stackshot no est\u00E1 instalada: desc\u00E1rgala desde GitHub.", "Jetzt herunterladen? Diese Kopie von Stackshot ist portabel: lade sie von GitHub herunter.");
            reg("Download now? This version does not include an update signature: download from GitHub.", "\u00BFQuieres descargarla? Esta versi\u00F3n no trae firma de actualizaci\u00F3n: desc\u00E1rgala desde GitHub.", "Jetzt herunterladen? Diese Version bringt keine Update-Signatur mit: lade sie von GitHub herunter.");
            reg("Checking for updates\u2026", "Buscando actualizaciones\u2026", "Nach Updates suchen\u2026");
            reg("Checking GitHub for the latest version of Stackshot.", "Preguntando a GitHub por la \u00FAltima versi\u00F3n de Stackshot.", "Fragt GitHub nach der neuesten Version von Stackshot.");
            reg("Could not check", "No se pudo comprobar", "Konnte nicht gepr\u00FCft werden");
            reg(" Last checked: {0}.", " \u00DAltima comprobaci\u00F3n: {0}.", " Zuletzt gepr\u00FCft: {0}.");
            reg("You're up to date", "Est\u00E1s al d\u00EDa", "Du bist auf dem neuesten Stand");
            reg("Stackshot {0} is the latest version. Checked {1}.", "Stackshot {0} es la versi\u00F3n m\u00E1s reciente. Comprobado {1}.", "Stackshot {0} ist die neueste Version. Gepr\u00FCft: {1}.");
            reg("Check now", "Buscar ahora", "Jetzt pr\u00FCfen");
            reg("Check for updates automatically", "Buscar actualizaciones autom\u00E1ticamente", "Updates automatisch suchen");
            reg("As soon as recording is saved, Stackshot will restart.", "En cuanto termine de guardarse la grabaci\u00F3n, Stackshot se cerrar\u00E1 y volver\u00E1 a abrirse.", "Sobald die Aufnahme gespeichert ist, startet Stackshot neu.");
            reg("Capture modes", "Modos de captura", "Aufnahmemodi");
            reg("How much detail and smoothness each video saves, and file size.", "Cu\u00E1nto detalle y fluidez guarda cada v\u00EDdeo, y lo que pesa.", "Wie viel Detail und Fl\u00FCssigkeit jedes Video speichert und wie gro\u00DF die Datei wird.");
            reg("You have version {0}. Not checked for updates yet.", "Tienes la versi\u00F3n {0}. A\u00FAn no se ha comprobado si hay otra m\u00E1s nueva.", "Du hast Version {0}. Es wurde noch nicht nach einer neueren Version gesucht.");
            reg("View all release notes", "Ver todas las novedades", "Alle Neuerungen ansehen");
            reg("When Stackshot opens and every 3 hours. Never sends any personal data.", "Al abrir Stackshot y cada 3 horas. No env\u00EDa nada tuyo.", "Beim \u00D6ffnen von Stackshot und alle 3 Stunden. Es werden keine pers\u00F6nlichen Daten gesendet.");
            reg("Only with installed Stackshot; portable copy cannot self-update.", "Solo con Stackshot instalado; esta copia no puede instalarse sola.", "Nur mit installiertem Stackshot; eine portable Kopie kann sich nicht selbst aktualisieren.");
            reg("Requires \u201CCheck for updates automatically\u201D.", "Necesita \u00ABBuscar actualizaciones autom\u00E1ticamente\u00BB.", "Erfordert \u201EUpdates automatisch installieren\u201C.");
            reg("Downloaded, verified, and installed when system is idle.", "Se descargan, se comprueba su firma y se instalan cuando no est\u00E9s usando el equipo.", "Wird heruntergeladen, gepr\u00FCft und installiert, wenn der PC im Leerlauf ist.");
            reg("{0}%  \u00B7  SHA-256 checksum and signature verified after", "{0} %  \u00B7  despu\u00E9s se comprueban su suma SHA-256 y su firma", "{0} %  \u00B7  danach werden SHA-256-Pr\u00FCfsumme und Signatur gepr\u00FCft");
            reg("just now", "ahora mismo", "gerade eben");
            reg("{0} min ago", "hace {0} min", "vor {0} Min.");
            reg("today at {0}", "hoy a las {0}", "heute um {0}");
            reg("yesterday at {0}", "ayer a las {0}", "gestern um {0}");
            reg("on {0} at {1}", "el {0} a las {1}", "am {0} um {1}");

            // Recording page
            reg("Video with sound (MP4, or lossless MKV in Cinema) and animated GIF. To stop, press the hotkey again or Stop in the recording bar.",
                "V\u00EDdeo con sonido (MP4, o MKV sin p\u00E9rdidas en Cine) y GIF animado. Para parar, pulsa el atajo otra vez o Detener en la barra de grabaci\u00F3n.",
                "Video mit Ton (MP4, oder verlustfreies MKV in Kino) und animiertes GIF. Zum Stoppen dr\u00FCcke die Taste erneut oder Stop in der Aufnahmeleiste.");
            reg("Light and fluid, for chat and email.", "Ligera y fluida, para chats y correos.", "Leicht und fl\u00FCssig, f\u00FCr Chat und E-Mail.");
            reg("Sharp 60 fps, ready instantly.", "60 fps n\u00EDtidos, listo al momento.", "Scharfe 60 Bilder/s, sofort fertig.");
            reg("Lossless 60 fps while recording.", "60 fps sin p\u00E9rdida al grabar.", "Verlustfreie 60 Bilder/s beim Aufnehmen.");
            reg("Lossless and verified, in MKV.", "Sin p\u00E9rdidas y verificado, en MKV.", "Verlustfrei und gepr\u00FCft, in MKV.");
            reg("Sharpness", "Nitidez", "Sch\u00E4rfe");
            reg("Smoothness", "Fluidez", "Fl\u00FCssigkeit");
            reg("Standard: 24, 30 or 60 fps with good compression. Small files ready to share in seconds.",
                "Est\u00E1ndar: 24, 30 o 60 fps con buena compresi\u00F3n. Archivos peque\u00F1os que se env\u00EDan en segundos.",
                "Standard: 24, 30 oder 60 Bilder/s mit guter Komprimierung. Kleine Dateien, die in Sekunden geteilt werden.");
            reg("High: 60 fps and rich detail, encoded while recording so it saves instantly. Larger files.",
                "Alta: 60 fps y mucho detalle, codificado mientras grabas, as\u00ED que se guarda al instante. Archivos m\u00E1s grandes.",
                "Hoch: 60 Bilder/s und viel Detail, wird beim Aufnehmen codiert und ist deshalb sofort gespeichert. Gr\u00F6\u00DFere Dateien.");
            reg("Maximum: 60 fps matched to your monitor rate, recorded losslessly (in areas larger than 1920 \u00D7 1200 with very high quality, but not lossless) and encoded with very high quality when finished. Takes a moment to save.",
                "M\u00E1xima: 60 fps al ritmo exacto de tu monitor, se graba sin ninguna p\u00E9rdida (en \u00E1reas de m\u00E1s de 1920 \u00D7 1200 con calidad muy alta, pero no sin p\u00E9rdidas) y al terminar se codifica con calidad muy alta. Tarda un poco en guardar.",
                "Maximum: 60 Bilder/s synchron zu deiner Bildwiederholrate, verlustfrei aufgenommen (in Bereichen gr\u00F6\u00DFer als 1920 \u00D7 1200 in sehr hoher, aber nicht verlustfreier Qualit\u00E4t) und nach dem Ende in sehr hoher Qualit\u00E4t codiert. Das Speichern dauert einen Moment.");
            reg("Cinema: saves every pixel and audio exactly as delivered by Windows, lossless (.mkv file), verified frame-by-frame upon completion. Uses significant disk (MBs to GBs per minute depending on motion), CPU and RAM. Plays inside Stackshot (Windows player cannot open it); to share, Export in editor creates a compatible MP4 without altering the original. Records at 60 or 30 fps (chosen below) and does not capture all refreshes above 60 Hz. Faithful to Windows output, not monitor color (Windows converts HDR to SDR). If the PC cannot keep up, recording stops safely keeping what was captured.",
                "Cine: guarda cada p\u00EDxel y el sonido tal como los entrega Windows, sin p\u00E9rdidas (archivo .mkv), y al terminar lo comprueba fotograma a fotograma. Ocupa mucho (de MB a GB por minuto, seg\u00FAn el movimiento) y usa bastante CPU, memoria y disco. Se reproduce dentro de Stackshot (el reproductor de Windows no lo abre); para compartirlo, Exportar en el editor crea un MP4 compatible y el original no cambia. Graba a 60 o 30 fps (lo eliges aqu\u00ED debajo) y no capta todos los refrescos de una pantalla de m\u00E1s de 60 Hz. Es fiel a lo que entrega Windows, no al color que muestra tu monitor (el HDR lo convierte Windows a SDR). Si el equipo no puede seguir el ritmo, la grabaci\u00F3n se detiene y conserva lo grabado.",
                "Kino: speichert jedes Pixel und den Ton genau so, wie Windows ihn liefert, verlustfrei (.mkv-Datei), und pr\u00FCft nach dem Ende jedes Einzelbild. Braucht viel Platz (von MB bis GB pro Minute, je nach Bewegung), CPU und Arbeitsspeicher. Die Wiedergabe l\u00E4uft in Stackshot (der Windows-Player \u00F6ffnet es nicht); zum Teilen erstellt Export im Editor eine kompatible MP4, ohne das Original zu ver\u00E4ndern. Nimmt mit 60 oder 30 Bildern/s auf (unten w\u00E4hlbar) und erfasst nicht alle Bildwiederholungen \u00FCber 60 Hz. Getreu der Ausgabe von Windows, nicht der Bildschirmfarbe (Windows wandelt HDR in SDR um). Kann der PC nicht mithalten, stoppt die Aufnahme sicher und beh\u00E4lt das Aufgenommene.");
            reg("up to {0} (about {1} s of peaks at {2} \u00D7 {3})", "hasta {0} (unos {1} s de picos a {2} \u00D7 {3})", "bis zu {0} (etwa {1} s Spitzen bei {2} \u00D7 {3})");
            reg("Normal: {0}. Only uses what is needed; if full, recording stops and keeps what was recorded.",
                "Normal: {0}. Solo se usa la que hace falta; si se llena, la grabaci\u00F3n se detiene y conserva lo grabado.",
                "Normal: {0}. Es wird nur so viel genutzt wie n\u00F6tig; ist der Speicher voll, stoppt die Aufnahme und beh\u00E4lt das Aufgenommene.");
            reg("High: {0}. For captures that stop with heavy motion; with low free memory the PC may slow down.",
                "Alta: {0}. Para tomas que se detienen con mucho movimiento; con poca memoria libre el equipo puede ir m\u00E1s lento.",
                "Hoch: {0}. F\u00FCr Aufnahmen mit viel Bewegung; bei wenig freiem Arbeitsspeicher kann der PC langsamer werden.");
            reg("More frames, smoother (and larger file).", "M\u00E1s fotogramas, m\u00E1s fluido (y m\u00E1s pesado).", "Mehr Bilder, fl\u00FCssiger (und gr\u00F6\u00DFere Datei).");
            reg("Memory for motion peaks", "Memoria para picos de movimiento", "Speicher f\u00FCr Bewegungsspitzen");
            reg("Smooth motion. If the area is too large for your PC, it will ask before recording.", "Movimiento fluido. Si el \u00E1rea es demasiado grande para tu equipo, te preguntar\u00E1 antes de grabar.", "Fl\u00FCssige Bewegung. Ist der Bereich f\u00FCr deinen PC zu gro\u00DF, fragt es vor der Aufnahme nach.");
            reg("Half the data and workload: more headroom for large areas or modest PCs. Fast motion looks less smooth.", "La mitad de datos y de trabajo: m\u00E1s margen para \u00E1reas grandes o equipos justos. Lo r\u00E1pido se ve menos suave.", "Halbe Datenmenge und halbe Arbeit: mehr Spielraum f\u00FCr gro\u00DFe Bereiche oder schw\u00E4chere PCs. Schnelle Bewegung wirkt weniger fl\u00FCssig.");
            reg("Sharpness and smoothness: more bars are better. Size: file space used.", "Nitidez y fluidez: cuantas m\u00E1s barras, mejor. Tama\u00F1o: lo que ocupa el archivo.", "Sch\u00E4rfe und Fl\u00FCssigkeit: je mehr Balken, desto besser. Gr\u00F6\u00DFe: Platz der Datei.");
            reg("Records what you turn on, each from the device you choose.", "Se graba lo que actives, cada uno desde el dispositivo que elijas.", "Nimmt auf, was du einschaltest, jeweils vom Ger\u00E4t deiner Wahl.");
            reg("While recording, the bar shows each sound level and clicking the mic mutes it. Sound is synced with video and never clips even if both play at once.",
                "Mientras grabas, la barra de grabaci\u00F3n muestra el nivel de cada sonido y un clic en el micr\u00F3fono lo silencia. El sonido va sincronizado con la imagen y, aunque suenen los dos a la vez, nunca se satura.",
                "W\u00E4hrend der Aufnahme zeigt die Leiste den Pegel jedes Tons an; ein Klick auf das Mikrofon schaltet es stumm. Der Ton ist zum Bild synchron und \u00FCbersteuert nie, auch wenn beide gleichzeitig laufen.");
            reg("What plays on your computer: videos, calls, alerts.", "Lo que suena en el ordenador: v\u00EDdeos, llamadas, avisos.", "Was auf deinem Computer l\u00E4uft: Videos, Anrufe, Hinweise.");
            reg("Your voice while recording, at a clear, constant volume.", "Tu voz mientras grabas, a un volumen claro y constante.", "Deine Stimme bei der Aufnahme, mit klarer, gleichbleibender Lautst\u00E4rke.");
            reg("Record audio from", "Grabar el sonido de", "Ton aufnehmen von");
            reg("Microphone to record", "Micr\u00F3fono que se graba", "Aufzunehmendes Mikrofon");
            reg("Windows default: {0}", "El de Windows: {0}", "Windows-Standard: {0}");
            reg("No audio output available", "No hay ninguna salida de sonido", "Keine Tonausgabe verf\u00FCgbar");
            reg("No microphone connected", "No hay ning\u00FAn micr\u00F3fono conectado", "Kein Mikrofon angeschlossen");
            reg("Disconnected: using Windows default", "Desconectado: se usar\u00E1 el de Windows", "Getrennt: Windows-Standard wird verwendet");
            reg("Windows default", "El de Windows", "Windows-Standard");
            reg("Selected device", "El que elegiste", "Ausgew\u00E4hltes Ger\u00E4t");
            reg("Camera", "C\u00E1mara", "Kamera");
            reg("Shape", "Forma", "Form");
            reg("Add your face in a bubble, like Loom.", "A\u00F1ade tu cara en una burbuja, como en Loom.", "Bring dein Gesicht in einer Blase unter, wie bei Loom.");
            reg("Appears in a corner of the recorded area. Drag it; double-click changes shape and right-click changes size.", "Sale en una esquina de la zona grabada. Arr\u00E1strala; doble clic cambia la forma y clic derecho el tama\u00F1o.", "Erscheint in einer Ecke des aufgenommenen Bereichs. Ziehe sie; Doppelklick \u00E4ndert die Form, Rechtsklick die Gr\u00F6\u00DFe.");
            reg("Download video engine to select camera", "Descarga el motor de v\u00EDdeo para elegir c\u00E1mara", "Video-Engine laden, um die Kamera zu w\u00E4hlen");
            reg("Searching for cameras\u2026", "Buscando c\u00E1maras\u2026", "Suche nach Kameras\u2026");
            reg("First one found: {0}", "La primera que encuentre: {0}", "Die erste gefundene: {0}");
            reg("First one found", "La primera que encuentre", "Die erste gefundene");
            reg("Camera to record", "C\u00E1mara que se graba", "Aufzunehmende Kamera");
            reg("Not connected now", "No est\u00E1 conectada ahora", "Derzeit nicht verbunden");
            reg("Search again", "Buscar de nuevo", "Erneut suchen");
            reg("GIF frames per second", "Fotogramas por segundo del GIF", "GIF-Bilder pro Sekunde");
            reg("GIFs are large: 15 fps is usually the sweet spot.", "Los GIF pesan mucho: 15 suele ser el punto justo.", "GIFs werden gro\u00DF: 15 fps sind meist der beste Kompromiss.");
            reg("Video engine", "Motor de v\u00EDdeo", "Video-Engine");
            reg("Not downloaded", "Sin descargar", "Nicht geladen");
            reg("FFmpeg records and encodes your videos and GIFs.", "FFmpeg graba y codifica tus v\u00EDdeos y GIF.", "FFmpeg nimmt deine Videos und GIFs auf und codiert sie.");
            reg("Downloads automatically (~110 MB) the first time you record.", "Se descarga solo (unos 110 MB) la primera vez que grabes.", "Wird beim ersten Aufnehmen automatisch geladen (rund 110 MB).");
            reg("Download now", "Descargar ahora", "Jetzt laden");
            reg("Before recording", "Antes de grabar", "Vor der Aufnahme");
            reg("Choosing the area shows shot options (quality, framerate, audio, camera) with the Record button, and a countdown gives you time to prepare. The countdown does not appear in the video.",
                "Al elegir el \u00E1rea aparecen las opciones de la toma (calidad, fotogramas, sonido y c\u00E1mara) con el bot\u00F3n Grabar, y una cuenta atr\u00E1s te da tiempo para prepararte. La cuenta atr\u00E1s no sale en el v\u00EDdeo.",
                "Nach der Bereichsauswahl erscheinen die Optionen der Aufnahme (Qualit\u00E4t, Bilder/s, Ton und Kamera) mit der Schaltfl\u00E4che Aufnehmen, und ein Countdown gibt dir Zeit zur Vorbereitung. Der Countdown erscheint nicht im Video.");
            reg("Show options before recording", "Mostrar las opciones antes de grabar", "Optionen vor der Aufnahme zeigen");
            reg("After recording", "Despu\u00E9s de grabar", "Nach der Aufnahme");
            reg("\u201CEdit\u201D on the thumbnail opens the video editor: trim start and end, change speed, size or format, add highlights and backdrops. Audio is preserved.",
                "\u00ABEditar\u00BB en la miniatura abre el editor de v\u00EDdeo: recorta el principio y el final, cambia la velocidad, el tama\u00F1o o el formato, a\u00F1ade marcas y ponle fondo. El sonido se mantiene.",
                "\u201EBearbeiten\u201C in der Miniaturansicht \u00F6ffnet den Videoeditor: Anfang und Ende k\u00FCrzen, Geschwindigkeit, Gr\u00F6\u00DFe oder Format \u00E4ndern, Markierungen und Hintergrund hinzuf\u00FCgen. Der Ton bleibt erhalten.");
            reg("Copy file when finished", "Copiar el archivo al terminar", "Datei nach dem Ende kopieren");
            reg("Ready to paste into chat or email right after stopping. Same setting as \u201CCopy each capture\u201D in General.",
                "Listo para pegar en un chat o un correo nada m\u00E1s parar. Es el mismo ajuste que \u00ABCopiar cada captura\u00BB, en General.",
                "Sofort nach dem Stoppen bereit zum Einf\u00FCgen in Chat oder E-Mail. Dieselbe Einstellung wie \u201EJede Aufnahme kopieren\u201C unter Allgemein.");
            reg("Built-in player supporting all formats, audio tracks, speed, and frame-by-frame. If off, opens in the default Windows app.",
                "Reproductor propio, con cualquier formato, pistas de sonido, velocidad y fotograma a fotograma. Si lo apagas, se abren en la aplicaci\u00F3n de Windows.",
                "Eigener Player mit allen Formaten, Tonspuren, Geschwindigkeit und Einzelbildern. Ausgeschaltet \u00F6ffnen sie sich in der Windows-Standard-App.");
            reg("Open a video or Cinema master\u2026", "Abrir un v\u00EDdeo o un m\u00E1ster Cine\u2026", "Video oder Kino-Master \u00F6ffnen\u2026");
            reg("Choose \u201CPresent\u201D backdrop", "Elegir el fondo de \u00ABPresentar\u00BB", "Hintergrund f\u00FCr \u201EPr\u00E4sentieren\u201C w\u00E4hlen");

            // Clicks and keys (input overlay)
            reg("Clicks & keys", "Clics y teclas", "Klicks und Tasten");
            reg("Drawn onto the video while recording so viewers can follow your actions. Not shown during pauses.", "Se dibujan en el v\u00EDdeo mientras grabas, para que quien lo vea siga lo que haces. No salen durante las pausas.", "Werden w\u00E4hrend der Aufnahme ins Video gezeichnet, damit Zuschauer deinen Schritten folgen k\u00F6nnen. In Pausen nicht sichtbar.");
            reg("Show clicks", "Mostrar los clics", "Klicks anzeigen");
            reg("A circle where you click: blue for left click and orange for right click.", "Un c\u00EDrculo donde haces clic: azul con el bot\u00F3n izquierdo y naranja con el derecho.", "Ein Kreis dort, wo du klickst: blau bei der linken, orange bei der rechten Taste.");
            reg("Show keystrokes pressed", "Mostrar las teclas pulsadas", "Gedr\u00FCckte Tasten anzeigen");
            reg("Shortcuts (Ctrl+C, Alt+Tab, Win+Shift+S\u2026) appear bottom-center. Typed text never appears, keeping your passwords safe.", "Los atajos (Ctrl + C, Alt + Tab, Win + May\u00FAs + S\u2026) salen abajo, en el centro. Lo que escribes nunca aparece, as\u00ED que tus contrase\u00F1as tampoco.", "Tastenk\u00FCrzel (Strg+C, Alt+Tab, Win+Umschalt+S\u2026) erscheinen unten in der Mitte. Getippter Text erscheint nie, so bleiben deine Passw\u00F6rter geheim.");

            // Watch and health
            reg("Reading your watch\u2026", "Leyendo tu reloj\u2026", "Deine Uhr wird gelesen\u2026");
            reg("Could not read: ", "No se ha podido leer: ", "Konnte nicht gelesen werden: ");
            reg("Not read yet today. Syncs automatically every 30 minutes.", "A\u00FAn no lo he le\u00EDdo hoy. Se lee solo cada media hora.", "Heute noch nicht gelesen. Wird automatisch alle 30 Minuten gelesen.");
            reg("Not synced yet today.", "A\u00FAn sin leer hoy.", "Heute noch nicht gelesen.");
            reg("Google has no data for today yet. Open your watch app on phone to sync, then try again.", "Google a\u00FAn no tiene datos de hoy. Abre la app de tu reloj en el m\u00F3vil para que se sincronice y vuelve a leerlo.", "Google hat f\u00FCr heute noch keine Daten. \u00D6ffne die Uhr-App auf dem Handy zum Synchronisieren und lies dann erneut.");
            reg("Google has no data for today yet. Open watch companion app on phone to sync.", "Google a\u00FAn no tiene datos de hoy. Abre la app del reloj en el m\u00F3vil para sincronizarlo.", "Google hat f\u00FCr heute noch keine Daten. \u00D6ffne die Uhr-App auf dem Handy zum Synchronisieren.");
            reg("Goal reached!", "\u00A1Objetivo cumplido!", "Ziel erreicht!");
            reg("of {0}", "de {0}", "von {0}");
            reg("Sleep", "Sue\u00F1o", "Schlaf");
            reg("last night", "anoche", "letzte Nacht");
            reg("no data", "sin datos", "keine Daten");
            reg("Active", "Activo", "Aktiv");
            reg("moderate or vigorous", "moderado o intenso", "moderat oder intensiv");
            reg("h ", "h ", "Std. ");
            reg("Your watch today", "Tu reloj hoy", "Deine Uhr heute");
            reg("Read at {0}", "Le\u00EDdo a las {0}", "Gelesen um {0}");
            reg("View in Health and activity", "Ver en Salud y actividad", "In Gesundheit und Aktivit\u00E4t ansehen");
            reg("Benefits of connecting", "Qu\u00E9 ganas conect\u00E1ndolo", "Was das Verbinden bringt");
            reg("The mascot uses your activity data to care for you: alerts after an hour of inactivity, encourages movement in morning/afternoon, celebrates walks, active minutes and step goals, adapts breaks if you had poor sleep, and includes steps in daily summary.",
                "La mascota usa tus datos para cuidarte: te avisa si llevas una hora sin moverte y te propone levantarte, te anima por la ma\u00F1ana y despu\u00E9s de comer si apenas has caminado, celebra tus paseos, tus minutos activos y tu objetivo de pasos, te dice cu\u00E1nto te falta por la tarde, adelanta las pausas si has dormido poco (y por la noche te recuerda acostarte antes) y no te pide moverte justo despu\u00E9s de caminar. Tambi\u00E9n suma tus pasos al resumen del d\u00EDa.",
                "Das Maskottchen nutzt deine Aktivit\u00E4tsdaten, um auf dich zu achten: es meldet sich nach einer Stunde ohne Bewegung und schl\u00E4gt dir vor aufzustehen, motiviert dich morgens und nach dem Mittagessen, wenn du kaum gelaufen bist, feiert deine Spazierg\u00E4nge, deine aktiven Minuten und dein Schrittziel, sagt dir nachmittags, wie viel dir noch fehlt, verlegt die Pausen vor, wenn du schlecht geschlafen hast (und erinnert dich abends, fr\u00FCher ins Bett zu gehen), und bittet dich nicht gleich nach dem Gehen zu mehr Bewegung. Es z\u00E4hlt deine Schritte auch zur Tages\u00FCbersicht.");
            reg("More smartwatches and fitness bands", "M\u00E1s relojes y pulseras", "Mehr Smartwatches und Fitness-Armb\u00E4nder");
            reg("Support for more smartwatches and fitness trackers coming soon, in addition to Pixel Watch and Fitbit.", "Pr\u00F3ximamente podr\u00E1s conectar otros relojes y pulseras, no solo Pixel Watch y Fitbit.", "Demn\u00E4chst kannst du weitere Smartwatches und Tracker verbinden, nicht nur Pixel Watch und Fitbit.");
            reg("Soon", "Pronto", "Bald");
            reg("Today on your watch", "Hoy en tu reloj", "Heute auf deiner Uhr");
            reg("Read now", "Leer ahora", "Jetzt lesen");
            reg("Read at {0}. Automatically synced every 30 minutes.", "Le\u00EDdo a las {0}. Se lee solo cada media hora.", "Gelesen um {0}. Wird automatisch alle 30 Minuten gelesen.");

            // Wellness
            reg("Rest your eyes", null, "Augen ausruhen");
            reg("You haven't taken any breaks yet today.", "Hoy todav\u00EDa no has hecho ninguna pausa.", "Heute hast du noch keine Pause gemacht.");

            // Search
            reg("Results", "Resultados", "Ergebnisse");
            reg("1 setting matches {0}.", "1 ajuste coincide con {0}.", "1 Einstellung passt zu {0}.");
            reg("{0} settings match {1}.", "{0} ajustes coinciden con {1}.", "{0} Einstellungen passen zu {1}.");
            reg("And {0} more: add another word to refine.", "Y {0} m\u00E1s: a\u00F1ade otra palabra para afinar.", "Und {0} weitere: erg\u00E4nze ein Wort, um es einzugrenzen.");
            reg("No settings match {0}", "No hay ajustes que coincidan con {0}", "Keine Einstellungen passen zu {0}");
            reg("Try different words or fewer letters, such as \u201Cfolder\u201D, \u201Csound\u201D or \u201Cshortcut\u201D.", "Prueba con otras palabras o con menos letras, como \u00ABcarpeta\u00BB, \u00ABsonido\u00BB o \u00ABatajo\u00BB.", "Versuch andere W\u00F6rter oder weniger Buchstaben, etwa \u201EOrdner\u201C, \u201ETon\u201C oder \u201ETastenk\u00FCrzel\u201C.");
            reg(" folder", " carpeta", " Ordner");

            // Saved-capture names
            reg("Subfolders", "Subcarpetas", "Unterordner");
            reg("By month", "Por mes", "Nach Monat");
            reg("By year and month", "Por a\u00F1o y mes", "Nach Jahr und Monat");
            reg("Saved captures go to a monthly folder, like {0}-{1}.", "Lo que guardas va a una carpeta por mes, como {0}-{1}.", "Gespeicherte Aufnahmen landen in einem Ordner pro Monat, etwa {0}-{1}.");
            reg("A folder per year and monthly subfolder inside, like {0}\\{1}.", "Una carpeta por a\u00F1o y, dentro, una por mes, como {0}\\{1}.", "Ein Ordner pro Jahr mit Unterordnern pro Monat, etwa {0}\\{1}.");
            reg("All files go directly into the chosen folder.", "Todo va directamente a la carpeta elegida.", "Alles landet direkt im gew\u00E4hlten Ordner.");
            reg("Capture type: Capture, Screen, active app name, Scrolling\u2026", "El tipo de captura: Captura, Pantalla, el programa de la ventana, Desplazamiento\u2026", "Aufnahmeart: Aufnahme, Bildschirm, Programm des Fensters, Bildlauf\u2026");
            reg("Date, like {0}", "La fecha, como {0}", "Datum, etwa {0}");
            reg("Time, like {0}", "La hora, como {0}", "Uhrzeit, etwa {0}");
            reg("Active program when captured, like chrome or EXCEL", "El programa que ten\u00EDas delante al capturar, como chrome o EXCEL", "Programm im Vordergrund beim Aufnehmen, etwa chrome oder EXCEL");
            reg("Title of that window", "El t\u00EDtulo de esa ventana", "Titel dieses Fensters");
            reg("Year, like {0}", "El a\u00F1o, como {0}", "Jahr, etwa {0}");
            reg("Month, like {0}", "El mes, como {0}", "Monat, etwa {0}");
            reg("Day, like {0}", "El d\u00EDa, como {0}", "Tag, etwa {0}");
            reg("Sequential unique number: 1, 2, 3\u2026 (with {date}, resets daily)", "Un n\u00FAmero que no se repite: 1, 2, 3\u2026 (con {fecha}, empieza cada d\u00EDa)", "Fortlaufende Nummer: 1, 2, 3\u2026 (mit {date} beginnt sie jeden Tag neu)");
            reg("{0} is not one of the tokens above: kept as-is.", null, "{0} geh\u00F6rt zu keinem der Platzhalter oben: bleibt unver\u00E4ndert.");
            reg("{0} are not tokens above: kept as-is.", null, "{0} sind keine der Platzhalter oben: bleiben unver\u00E4ndert.");
            reg("\\ / : * ? \" < > | are invalid in file names: replaced with _.", "\\ / : * ? \" < > | no valen en un nombre de archivo: se cambian por _.", "\\ / : * ? \" < > | sind in Dateinamen unzul\u00E4ssig: werden durch _ ersetzt.");
            reg("If left empty, default file name is used.", "Si lo dejas vac\u00EDo, se usa el nombre de siempre.", "Ist das Feld leer, wird der gewohnte Name verwendet.");
            reg("Without {time} or {n}, duplicate names will append (2), (3)\u2026 at the end.", "Sin {hora} ni {n}, las capturas que se llamen igual llevar\u00E1n (2), (3)\u2026 al final.", "Ohne {time} oder {n} bekommen gleichnamige Aufnahmen (2), (3)\u2026 ans Ende angeh\u00E4ngt.");
            reg("For images; videos and GIFs retain their own names.", "Para las im\u00E1genes; los v\u00EDdeos y los GIF conservan su nombre.", "F\u00FCr Bilder; Videos und GIFs behalten ihren eigenen Namen.");
            reg("Example: ", "Ejemplo: ", "Beispiel: ");
            reg("File names", "Nombre de los archivos", "Dateinamen");
            reg("Reset", "Restablecer", "Zur\u00FCcksetzen");
            reg("Reset to {0}", "Volver a {0}", "Zur\u00FCck auf {0}");

            // Sticky notes
            reg("Always on top", "Siempre encima", "Immer im Vordergrund");
            reg("Lock position", "Bloquear posici\u00F3n", "Position sperren");
            reg("Yellow", "Amarillo", "Gelb");
            reg("Blue", "Azul", "Blau");
            reg("Green", "Verde", "Gr\u00FCn");
            reg("Pink", "Rosa", "Rosa");
            reg("Open Notas.md", "Abrir Notas.md", "Notas.md \u00F6ffnen");
            reg("Remove from desktop", "Quitar del escritorio", "Vom Desktop entfernen");
            reg("Pinned notes", "Notas fijadas", "Angepinnte Notizen");
            reg("You already have 8 notes on your desktop: remove one before pinning another.", "Ya tienes 8 notas en el escritorio: quita alguna antes de fijar otra.", "Es sind schon 8 Notizen auf dem Desktop: entferne eine, um eine weitere anzupinnen.");

            // Quick links
            reg("Websites, folders, files or programs you want handy: they appear in the mascot's menu and open with a click.", "Webs, carpetas, archivos o programas que quieras tener a mano: salen en el men\u00FA de la mascota y te los abre con un clic.", "Websites, Ordner, Dateien oder Programme, die du griffbereit haben willst: Sie erscheinen im Men\u00FC des Maskottchens und \u00F6ffnen sich per Klick.");
            reg("Web address or path", "Direcci\u00F3n web o ruta", "Webadresse oder Pfad");
            reg("https://\u2026 or C:\\Folder\\file", "https://\u2026 o C:\\Carpeta\\archivo", "https://\u2026 oder C:\\Ordner\\Datei");
            reg("Name", "Nombre", "Name");
            reg("Name (optional)", "Nombre (opcional)", "Name (optional)");
            reg("Choose folder\u2026", "Elegir carpeta\u2026", "Ordner ausw\u00E4hlen\u2026");
            reg("Choose file or program\u2026", "Elegir archivo o programa\u2026", "Datei oder Programm ausw\u00E4hlen\u2026");
            reg("Add", "A\u00F1adir", "Hinzuf\u00FCgen");
            reg("Your links ({0} of {1})", "Tus accesos ({0} de {1})", "Deine Schnelllinks ({0} von {1})");
            reg("Remove {0}", "Quitar {0}", "{0} entfernen");
            reg("Done! \u201C{0}\u201D is now in my menu.", "\u00A1Listo! \u00AB{0}\u00BB ya est\u00E1 en mi men\u00FA.", "Fertig! \u201E{0}\u201C ist jetzt in meinem Men\u00FC.");
            reg("Choose the folder you want to have handy", "Elige la carpeta que quieres tener a mano", "W\u00E4hle den Ordner, den du griffbereit haben willst");
            reg("Choose the file or program", "Elige el archivo o el programa", "W\u00E4hle die Datei oder das Programm");

            // Intro
            reg("Capture, annotate and share in seconds", "Captura, marca y comparte en segundos", "Aufnehmen, markieren und in Sekunden teilen");
        }
    }
}
