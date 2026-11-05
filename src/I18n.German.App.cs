// Stackshot - German localization catalog: App.
// MIT License - https://github.com/rubenitx/stackshot
using System;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterGermanApp(Action<string, string, string> reg)
        {
            // FileAssoc
            reg("Screenshots, video and GIF, with a video player and a Markdown and XML viewer",
                "Capturas de pantalla, v\u00EDdeo y GIF, con reproductor de v\u00EDdeo y visor de Markdown y XML",
                "Bildschirmaufnahmen, Video und GIF, mit einem Video-Player und einer Markdown- und XML-Ansicht");
            reg("MKV video", "V\u00EDdeo MKV", "MKV-Video");

            // Net, Policy
            reg("Only HTTPS connections are allowed: ", "Solo se permiten conexiones HTTPS: ", "Nur HTTPS-Verbindungen sind erlaubt: ");
            reg("Managed by your organization", "Lo decide tu organizaci\u00F3n", "Von deiner Organisation verwaltet");
            reg("Some settings are managed by your organization.", "Algunos ajustes los gestiona tu organizaci\u00F3n.", "Einige Einstellungen werden von deiner Organisation verwaltet.");

            // Setup / uninstall
            reg("Stackshot: screenshots, video and GIF", "Stackshot: capturas de pantalla, v\u00EDdeo y GIF", "Stackshot: Bildschirmaufnahmen, Video und GIF");
            reg("\n\nRecovered recordings ({0}) remain in {1}.", "\n\nLas grabaciones recuperadas ({0}) se quedan en {1}.", "\n\nDie wiederhergestellten Aufnahmen ({0}) bleiben in {1}.");
            reg("Uninstall Stackshot?\n\nCaptures saved in {0} will not be deleted.{1}",
                "\u00BFDesinstalar Stackshot?\n\nLas capturas que hayas guardado en {0} no se borran.{1}",
                "Stackshot deinstallieren?\n\nAufnahmen, die du in {0} gespeichert hast, werden nicht gel\u00F6scht.{1}");
            reg("Stackshot is still open: there are unsaved changes or a recording is being saved.\n\nClose it and uninstall again.",
                "Stackshot sigue abierto: hay cambios sin guardar o una grabaci\u00F3n guard\u00E1ndose.\n\nCi\u00E9rralo y vuelve a desinstalar.",
                "Stackshot l\u00E4uft noch: es gibt ungespeicherte \u00C4nderungen oder eine Aufnahme wird gerade gespeichert.\n\nSchlie\u00DFe es und deinstalliere erneut.");
            reg("Stackshot has been uninstalled. Thank you for trying it!", "Stackshot se ha desinstalado. \u00A1Gracias por probarlo!", "Stackshot wurde deinstalliert. Danke, dass du es ausprobiert hast!");
            reg("\n\nThe Print Screen key opens Windows Snipping Tool again.", "\n\nLa tecla Impr Pant vuelve a abrir Recortes de Windows.", "\n\nDie Druck-Taste \u00F6ffnet wieder das Windows-Snipping-Tool.");
            reg("\n\nRecovered recordings remain in {0}.", "\n\nLas grabaciones recuperadas siguen en {0}.", "\n\nDie wiederhergestellten Aufnahmen bleiben in {0}.");

            // Program: open errors and updates
            reg("Could not open {0}.\n\n{1}", "No se pudo abrir {0}.\n\n{1}", "{0} konnte nicht ge\u00F6ffnet werden.\n\n{1}");
            reg("This machine has Stackshot {0} installed via MSI package.\n\nTo update to {1}, install Stackshot.msi for that version (or ask IT).",
                "Este equipo tiene Stackshot {0} instalado con el paquete MSI.\n\nPara pasar a la {1}, instala Stackshot.msi de esa versi\u00F3n (o p\u00EDdeselo a inform\u00E1tica).",
                "Auf diesem Rechner ist Stackshot {0} \u00FCber das MSI-Paket installiert.\n\nF\u00FCr {1} installiere Stackshot.msi dieser Version (oder frag die IT).");
            reg("Update Stackshot from version {0} to {1}?", "\u00BFActualizar Stackshot de la versi\u00F3n {0} a la {1}?", "Stackshot von Version {0} auf {1} aktualisieren?");
            reg("Does not seem to be a valid image, or is corrupted.", "No parece una imagen v\u00E1lida, o est\u00E1 da\u00F1ada.", "Sieht nicht nach einem g\u00FCltigen Bild aus oder ist besch\u00E4digt.");
            reg("The file cannot be opened in the editor.", "El archivo no se puede abrir en el editor.", "Die Datei l\u00E4sst sich im Editor nicht \u00F6ffnen.");
            reg("Could not install Stackshot.\n\n", "No se pudo instalar Stackshot.\n\n", "Stackshot konnte nicht installiert werden.\n\n");
            reg("\n\nIf Stackshot is still open (saving a recording, unsaved changes), close it and try again.",
                "\n\nSi Stackshot sigue abierto (una grabaci\u00F3n guard\u00E1ndose, cambios sin guardar), ci\u00E9rralo y vuelve a intentarlo.",
                "\n\nWenn Stackshot noch offen ist (eine Aufnahme wird gespeichert, \u00C4nderungen sind ungespeichert), schlie\u00DFe es und versuch es erneut.");

            // ShotStack: XML watch, shortcuts, saving, rescue, open errors
            reg("Choose the XML folder", "Elige la carpeta de los XML", "XML-Ordner ausw\u00E4hlen");
            reg("XML monitoring is on, but has no folder. Pick one in General > Advanced.", "La vigilancia de XML est\u00E1 activada pero no tiene carpeta. El\u00EDgela en General > Avanzado.", "Die XML-\u00DCberwachung ist aktiv, hat aber keinen Ordner. W\u00E4hle einen unter Allgemein > Erweitert.");
            reg("Some shortcuts do not work", "Algunos atajos no funcionan", "Einige Tastenk\u00FCrzel funktionieren nicht");
            reg("Choose other shortcuts", "Elegir otros atajos", "Andere Tastenk\u00FCrzel w\u00E4hlen");
            reg("is used by another program (OneDrive, ShareX, Lightshot, Greenshot\u2026?).", "los usa otro programa (\u00BFOneDrive, ShareX, Lightshot, Greenshot\u2026?).", "werden von einem anderen Programm verwendet (OneDrive, ShareX, Lightshot, Greenshot\u2026?).");
            reg("is set twice in Stackshot shortcuts: only the first counts.", "est\u00E1 puesto dos veces en los atajos de Stackshot: solo cuenta la primera.", "ist doppelt in den Stackshot-Tastenk\u00FCrzeln vergeben: nur das erste z\u00E4hlt.");
            reg("is not a valid key combination.", "no es una combinaci\u00F3n de teclas v\u00E1lida.", "ist keine g\u00FCltige Tastenkombination.");
            reg("Videos|", "V\u00EDdeos|", "Videos|");
            reg("All files", "Todos los archivos", "Alle Dateien");
            reg("Pasted", "Pegada", "Eingef\u00FCgt");
            reg("Could not save to your folder", "No se pudo guardar en tu carpeta", "In deinem Ordner konnte nicht gespeichert werden");
            reg(" The thumbnail remains in the stack: you can copy it or save it to your folder.", " La miniatura sigue en la pila: puedes copiarla o guardarla en tu carpeta.", " Die Miniaturansicht bleibt im Stapel: du kannst sie kopieren oder in deinem Ordner speichern.");
            reg(" The thumbnail remains in the stack: you can click Save again.", " La miniatura sigue en la pila: puedes volver a pulsar Guardar.", " Die Miniaturansicht bleibt im Stapel: du kannst erneut auf Speichern klicken.");
            reg("Stackshot closed while recording{0}. The recording is safe", "Stackshot se cerr\u00F3 mientras grababa{0}. Lo grabado est\u00E1 a salvo", "Stackshot wurde mitten in einer Aufnahme beendet{0}. Die Aufnahme ist in Sicherheit");
            reg("Stackshot closed during {0} recordings. The recordings are safe", "Stackshot se cerr\u00F3 durante {0} grabaciones. Lo grabado est\u00E1 a salvo", "Stackshot wurde w\u00E4hrend {0} Aufnahmen geschlossen. Die Aufnahmen sind in Sicherheit");
            reg(" (the one from {0}{1})", " (la de las {0}{1})", " (die von {0} Uhr{1})");
            reg(" on {0}", " del {0}", " am {0}");
            reg("in the \u201Crecuperado\u201D folder.", "en la carpeta \u00ABrecuperado\u00BB.", "im Ordner \u201Erecuperado\u201C.");
            reg("Windows cannot write to {0}. Check folder permissions or if Controlled Folder Access is protecting it.",
                "Windows no deja escribir en {0}. Comprueba los permisos de la carpeta o si \u00ABAcceso controlado a carpetas\u00BB la protege.",
                "Windows kann nicht in {0} schreiben. Pr\u00FCfe die Ordnerberechtigungen oder ob der \u201Ekontrollierte Ordnerzugriff\u201C sie sch\u00FCtzt.");
            reg("No space left on the disk of {0}. Free up space and try again.", "No queda espacio en el disco de {0}. Libera espacio y vuelve a intentarlo.", "Auf dem Laufwerk von {0} ist kein Platz mehr. Schaffe Platz und versuch es erneut.");
            reg("Cannot find folder {0} (a disconnected or network drive?).", "No se encuentra la carpeta {0} (\u00BFuna unidad desconectada o de red?).", "Ordner {0} wurde nicht gefunden (ein getrenntes oder Netzlaufwerk?).");
            reg("the folder", "la carpeta", "dem Ordner");
            reg("Opening {0}. {1} {2} (the viewer shows one at a time).", "Se abre {0}. {1} {2} (el visor muestra uno cada vez).", "{0} wird ge\u00F6ffnet. {1} {2} (die Ansicht zeigt immer nur eine).");
            reg("more file", "archivo m\u00E1s", "weitere Datei");
            reg("more files", "archivos m\u00E1s", "weitere Dateien");
            reg("Stackshot is saving a recording\u2026", "Stackshot est\u00E1 guardando una grabaci\u00F3n\u2026", "Stackshot speichert eine Aufnahme\u2026");

            // ShotStack.Assist: reminders and quick links
            reg("I'll remind you {0}.", "Te lo recuerdo {0}.", "Ich erinnere dich {0}.");
            reg("I'll remind you {0}: \u201C{1}\u201D.", "Te lo recuerdo {0}: \u00AB{1}\u00BB.", "Ich erinnere dich {0}: \u201E{1}\u201C.");
            reg("Reminder saved", "Recordatorio guardado", "Erinnerung gespeichert");
            reg("Reminder", "Recordatorio", "Erinnerung");
            reg("10 more min", "10 min m\u00E1s", "10 weitere Min.");
            reg("at {0}", "a las {0}", "um {0}");
            reg("tomorrow {0}", "ma\u00F1ana {0}", "morgen {0}");
            reg("tomorrow ", "ma\u00F1ana ", "morgen ");
            reg("on {0} {1}", "el {0} {1}", "am {0} um {1}");
            reg("Was for {0}: ", "Era para {0}: ", "Galt f\u00FCr {0}: ");
            reg("Remove this reminder", "Quitar este recordatorio", "Diese Erinnerung entfernen");
            reg("Noted! \u201C{0}\u201D is now in my quick links.", "\u00A1Apuntada! \u00AB{0}\u00BB ya est\u00E1 en mis accesos r\u00E1pidos.", "Notiert! \u201E{0}\u201C ist jetzt in meinen Schnelllinks.");
            reg("Noted {0} folders in my quick links!", "\u00A1Apuntadas {0} carpetas en mis accesos r\u00E1pidos!", "{0} Ordner in meinen Schnelllinks notiert!");
            reg("Watch out: an XML with a fault has arrived. ", "Ojo: ha llegado un XML con fallo. ", "Achtung: eine fehlerhafte XML-Datei ist angekommen. ");
            reg("And I'll open the file for you.", "Y te abro el archivo.", "Und ich \u00F6ffne dir die Datei.");
            reg("And I'll open the {0} files for you.", "Y te abro los {0} archivos.", "Und ich \u00F6ffne dir die {0} Dateien.");
            reg("Let's watch it!", "\u00A1A verlo!", "Schauen wir es uns an!");
            reg("Opening it for you!", "\u00A1Te lo abro!", "Ich \u00F6ffne es dir!");
            reg("Opening {0} files!", "\u00A1Abro {0} archivos!", "Ich \u00F6ffne {0} Dateien!");
            reg("To the editor!", "\u00A1Al editor!", "Ab in den Editor!");
            reg("XML with fault", "XML con fallo", "Fehlerhafte XML-Datei");

            // QuickWindow: reminders and notes
            reg("What should I remind you of?", "\u00BFQu\u00E9 te recuerdo?", "Woran soll ich dich erinnern?");
            reg("Type what I should remind you of.", "Escribe qu\u00E9 te recuerdo.", "Schreib, woran ich dich erinnern soll.");
            reg("What should I note?", "\u00BFQu\u00E9 apunto?", "Was soll ich notieren?");
            reg("Type what you want to note.", "Escribe lo que quieres apuntar.", "Schreib, was du notieren m\u00F6chtest.");
            reg("Take note", "Apuntar", "Notieren");
            reg("When?", "\u00BFCu\u00E9ndo?", "Wann?");
            reg("At\u2026", "A las\u2026", "Um\u2026");
            reg("(for example 16:30)", "(por ejemplo 16:30)", "(zum Beispiel 16:30)");
            reg("Type the time like 16:30.", "Escribe la hora como 16:30.", "Schreib die Uhrzeit wie 16:30.");
            reg("Tomorrow", "Ma\u00F1ana", "Morgen");
            reg("Write your note\u2026", "Escribe tu nota\u2026", "Schreib deine Notiz\u2026");
            reg("What happened?", "\u00BFQu\u00E9 ha pasado?", "Was ist passiert?");
            reg("What would you like?", "\u00BFQu\u00E9 te gustar\u00EDa?", "Was m\u00F6chtest du?");
            reg("\u201CWhat happened?\u201D", "\u00AB\u00BFQu\u00E9 ha pasado?\u00BB", "\u201EWas ist passiert?\u201C");
            reg("\u201CWhat would you like?\u201D", "\u00AB\u00BFQu\u00E9 te gustar\u00EDa?\u00BB", "\u201EWas m\u00F6chtest du?\u201C");
            reg("Edit note", "Editar nota", "Notiz bearbeiten");
            reg("Delete this note", "Borrar esta nota", "Diese Notiz l\u00F6schen");
            reg("View my notes", "Ver mis notas", "Meine Notizen ansehen");
            reg("View all my notes", "Ver todas mis notas", "Alle meine Notizen ansehen");
            reg("Recent notes", "\u00DAltimas notas", "Letzte Notizen");
            reg("Saved in Notas.md, next to your captures", "Se guarda en Notas.md, junto a tus capturas", "Gespeichert in Notas.md, neben deinen Aufnahmen");
            reg("Type a web address (like intranet.company.com) or a full path (like C:\\Projects).",
                "Escribe una direcci\u00F3n web (como intranet.empresa.es) o una ruta completa (como C:\\Proyectos).",
                "Schreib eine Webadresse (wie intranet.firma.de) oder einen vollst\u00E4ndigen Pfad (wie C:\\Projekte).");
            reg("Type a web address or the path of a folder or file.", "Escribe una direcci\u00F3n web o la ruta de una carpeta o un archivo.", "Schreib eine Webadresse oder den Pfad eines Ordners oder einer Datei.");
            reg("That address or path cannot be used.", "Esa direcci\u00F3n o ruta no se puede usar.", "Diese Adresse oder dieser Pfad kann nicht verwendet werden.");
            reg("You already have it in your quick links: \u201C{0}\u201D.", "Ya lo tienes en tus accesos r\u00E1pidos: \u00AB{0}\u00BB.", "Hast du schon in deinen Schnelllinks: \u201E{0}\u201C.");
            reg("You already have {0} links: remove one to add another.", "Ya tienes {0} accesos: quita alguno para a\u00F1adir otro.", "Du hast schon {0} Schnelllinks: entferne einen, um einen weiteren hinzuzuf\u00FCgen.");
            reg("Could not open browser.", "No he podido abrir el navegador.", "Der Browser lie\u00DF sich nicht \u00F6ffnen.");
            reg("Could not open \u201C{0}\u201D: it may no longer exist.", "No he podido abrir \u00AB{0}\u00BB: puede que ya no exista.", "\u201E{0}\u201C lie\u00DF sich nicht \u00F6ffnen: es existiert vielleicht nicht mehr.");
            reg("I cannot find that folder or file.", "No encuentro esa carpeta o ese archivo.", "Diesen Ordner oder diese Datei finde ich nicht.");
            reg("Noted, with {0}.", "Apuntado, con {0}.", "Notiert, mit {0}.");
            reg("Note saved.", "Nota guardada.", "Notiz gespeichert.");
            reg("Could not add note", "No se pudo apuntar", "Die Notiz konnte nicht gespeichert werden");
            reg("Could not update note", "No se pudo cambiar la nota", "Die Notiz konnte nicht ge\u00E4ndert werden");
            reg("That note changed in Notas.md: open it to see it.", "Esa nota ha cambiado en Notas.md: \u00E1brela para verla.", "Diese Notiz hat sich in Notas.md ge\u00E4ndert: \u00F6ffne sie, um sie zu sehen.");
            reg("That note is no longer in Notas.md (it was deleted or changed outside).", "Esa nota ya no est\u00E1 en Notas.md (se borr\u00F3 o cambi\u00F3 fuera).", "Diese Notiz ist nicht mehr in Notas.md (gel\u00F6scht oder au\u00DFerhalb ge\u00E4ndert).");

            // ShotStack.Links / LinksWindow

            // ShotStack.Focus
            reg(" paused", " en pausa", " pausiert");
            reg("block {0} of {1}", "bloque {0} de {1}", "Block {0} von {1}");
            reg("{0} left", "quedan {0}", "noch {0}");
            reg("{0} left.", "Quedan {0}.", "Noch {0}.");
            reg("Break of {0}", null, "Pause von {0}");
            reg("Long break of {0}", null, "Lange Pause von {0}");
            reg(": stand up, look away, drink water. I'll let you know when it's done.", ": lev\u00E1ntate, mira lejos, bebe agua. Te aviso al terminar.", ": steh auf, schau in die Ferne, trink Wasser. Ich melde mich, wenn er vorbei ist.");
            reg("Focus paused", "Concentraci\u00F3n en pausa", "Fokus pausiert");
            reg("Focus finished", "Concentraci\u00F3n terminada", "Fokus beendet");
            reg(". Well done!", ". \u00A1Bien hecho!", ". Gut gemacht!");
            reg("Let's leave it here.", "Lo dejamos aqu\u00ED.", "Dann lassen wir es dabei.");
            reg("Break finished", "Fin del descanso", "Pause beendet");
            reg("Continuing break", "Seguimos con el descanso", "Pause l\u00E4uft weiter");
            reg("Continuing focus", "Seguimos con la concentraci\u00F3n", "Fokus l\u00E4uft weiter");
            reg("Keep recording", "Seguir grabando", "Weiter aufnehmen");
            reg("Keep typing", "Seguir escribiendo", "Schreib weiter");
            reg("Stop and exit", "Detener y salir", "Stoppen und beenden");
            reg("Let's focus! {0}. I'll let you know when it's done.", "\u00A1A concentrarnos! {0}. Te aviso al terminar.", "Jetzt Fokus! {0}. Ich melde mich, wenn er vorbei ist.");
            reg("{0} of focus. Let's go!", "{0} de concentraci\u00F3n. \u00A1Vamos!", "{0} Fokus. Los geht's!");
            reg("{0} more minutes. I'll let you know when it's done.", "{0} minutos m\u00E1s. Te aviso al terminar.", "Noch {0} Minuten. Ich melde mich, wenn es fertig ist.");
            reg("Block {0} again", "Bloque {0} de nuevo", "Block {0} wiederholen");
            reg("Block {0} of {1} done", "Bloque {0} de {1} hecho", "Block {0} von {1} geschafft");
            reg("Block {0} of {1}: ", "Bloque {0} de {1}: ", "Block {0} von {1}: ");
            reg("block", "bloque", "Block");
            reg("blocks", "bloques", "Bl\u00F6cke");

            // ShotStack.Health: watch, water, steps, sleep
            reg("Connect your smartwatch?", "\u00BFConectamos tu reloj?", "Uhr verbinden?");
            reg("Have a Pixel Watch or a Fitbit? If you connect it, I can accompany you with your steps, activity and sleep. Data goes from Google directly to your PC.",
                "\u00BFTienes un Pixel Watch o un Fitbit? Si lo conectas, te acompa\u00F1o con tus pasos, tu actividad y tu sue\u00F1o. Los datos van de Google a tu equipo.",
                "Hast du eine Pixel Watch oder ein Fitbit? Wenn du sie verbindest, begleite ich dich mit deinen Schritten, deiner Aktivit\u00E4t und deinem Schlaf. Die Daten gehen von Google direkt an deinen PC.");
            reg("Stackshot will read your steps, activity and sleep every half hour. Data goes from Google directly to your PC.",
                "Stackshot leer\u00E1 tus pasos, tu actividad y tu sue\u00F1o cada media hora. Los datos van de Google a tu equipo.",
                "Stackshot liest deine Schritte, deine Aktivit\u00E4t und deinen Schlaf jede halbe Stunde. Die Daten gehen von Google direkt an deinen PC.");
            reg("Watch connected", "Reloj conectado", "Uhr verbunden");
            reg("Watch could not connect", "No se ha conectado el reloj", "Uhr konnte sich nicht verbinden");
            reg("I could not read your watch: ", "No he podido leer tu reloj: ", "Ich konnte deine Uhr nicht lesen: ");
            reg("I have not read your watch yet today.", "Todav\u00EDa no he le\u00EDdo tu reloj hoy.", "Deine Uhr habe ich heute noch nicht gelesen.");
            reg("Google has no data for today yet. Open your watch's app on your phone so it syncs, and I will check again.",
                "Google a\u00FAn no tiene datos de hoy. Abre la app de tu reloj en el m\u00F3vil para que se sincronice y vuelvo a mirarlo.",
                "Google hat f\u00FCr heute noch keine Daten. \u00D6ffne die Uhr-App auf deinem Handy, damit sie sich synchronisiert, dann sehe ich noch einmal nach.");
            reg("You slept {0} last night. Today I will suggest the eye and movement breaks a little earlier.",
                "Esta noche has dormido {0}. Hoy te propondr\u00E9 las pausas para la vista y para moverte un poco antes.",
                "Du hast letzte Nacht {0} geschlafen. Heute schlage ich die Pausen f\u00FCr die Augen und zum Bewegen etwas fr\u00FCher vor.");
            reg("How was your day? ", "\u00BFQu\u00E9 tal el d\u00EDa? ", "Wie war dein Tag? ");
            reg("Your day", "Tu d\u00EDa", "Dein Tag");
            reg("Today: ", "Hoy: ", "Heute: ");
            reg("Today: {0} {1}, {2} of focus", "Hoy: {0} {1}, {2} de concentraci\u00F3n", "Heute: {0} {1}, {2} Fokus");
            reg("{0} focus block ({1})", "{0} bloque de concentraci\u00F3n ({1})", "{0} Fokusblock ({1})");
            reg("{0} focus blocks ({1})", "{0} bloques de concentraci\u00F3n ({1})", "{0} Fokusbl\u00F6cke ({1})");
            reg("You've already walked {0} steps today: goal reached!", "\u00A1Hoy ya llevas {0} pasos: objetivo cumplido!", "Du hast heute schon {0} Schritte geschafft: Tagesziel erreicht!");
            reg("You've walked {0} steps today, {1}% of your {2} goal.", "Hoy llevas {0} pasos, el {1} % de tu objetivo de {2}.", "Du bist heute {0} Schritte gegangen, {1} % deines Ziels von {2}.");
            reg("you slept {0} last night", "anoche dormiste {0}", "hast du letzte Nacht {0} geschlafen");
            reg("you have {0} min of moderate or vigorous activity", "llevas {0} min de actividad moderada o intensa", "hast du schon {0} Min. moderate oder anstrengende Aktivit\u00E4t");
            reg(" {0} to go: about {1} min of walking.", " Te faltan {0}: unos {1} min andando.", " Dir fehlen noch {0}: etwa {1} Min. zu Fu\u00DF.");
            reg("In the last hour and a half you have only taken {0} steps. Shall we stretch our legs for a couple of minutes?",
                "En la \u00FAltima hora y media solo has dado {0} pasos. \u00BFEstiramos las piernas un par de minutos?",
                "In der letzten anderthalben Stunde bist du nur {0} Schritte gegangen. Sollen wir die Beine ein paar Minuten vertreten?");
            reg("In the last hour you have only taken {0} steps. Shall we stretch our legs for a couple of minutes?",
                "En la \u00FAltima hora solo has dado {0} pasos. \u00BFEstiramos las piernas un par de minutos?",
                "In der letzten Stunde bist du nur {0} Schritte gegangen. Sollen wir die Beine ein paar Minuten vertreten?");
            reg("{0} steps so far this morning. How about getting up for a glass of water and a short walk?",
                "Esta ma\u00F1ana llevas {0} pasos. \u00BFTe levantas a por un vaso de agua y das una vuelta corta?",
                "Heute Vormittag bist du bei {0} Schritten. Wie w\u00E4re es mit einem Glas Wasser und einer kurzen Runde?");
            reg("A walk after lunch feels great: ten minutes out and you come back with a clear head.",
                "Despu\u00E9s de comer sienta muy bien caminar: un paseo de diez minutos y vuelves con la cabeza despejada.",
                "Ein Spaziergang nach dem Essen tut gut: zehn Minuten raus und du kommst mit klarem Kopf zur\u00FCck.");
            reg("An active hour today! Your body thanks you.", "\u00A1Una hora activa hoy! Tu cuerpo te lo agradece.", "Eine aktive Stunde heute! Dein K\u00F6rper bedankt sich.");
            reg("{0} active minutes today already. Way to go!", "Ya llevas {0} minutos activos hoy. \u00A1As\u00ED se hace!", "Schon {0} aktive Minuten heute. Weiter so!");
            reg("{0} steps to your goal: a walk of about {1} minutes and you're there.",
                "Te faltan {0} pasos para tu objetivo: un paseo de unos {1} minutos y lo tienes.",
                "Dir fehlen noch {0} Schritte zum Ziel: ein Spaziergang von etwa {1} Minuten, und du hast es geschafft.");
            reg("You slept little last night: try to go to bed a bit earlier today. You'll feel it tomorrow!",
                "Anoche dormiste poco: hoy intenta acostarte un poco antes. \u00A1Ma\u00F1ana lo notar\u00E1s!",
                "Du hast letzte Nacht wenig geschlafen: geh heute etwas fr\u00FCher ins Bett. Morgen wirst du es sp\u00FCren!");
            reg("{0} steps so far, {1}% of today's goal.", "Llevas {0} pasos, el {1} % de tu objetivo de hoy.", "Du bist bei {0} Schritten, {1} % des heutigen Ziels.");
            reg("You slept {0} last night. Let's go!", "Anoche dormiste {0}. \u00A1A por el d\u00EDa!", "Du hast letzte Nacht {0} geschlafen. Los geht's!");
            reg("You slept {0}. A good day to focus!", "Has dormido {0}. \u00A1Buen d\u00EDa para concentrarse!", "Du hast {0} geschlafen. Ein guter Tag zum Fokussieren!");
            reg("You were away {0} min and I stopped the clock: {1} left.", "Has estado fuera {0} min y he parado el reloj: quedaban {1}.", "Du warst {0} Min. weg und ich habe die Uhr angehalten: {1} bleiben.");
            reg("{0} of {1} glasses of water", "{0} de {1} vasos de agua", "{0} von {1} Gl\u00E4sern Wasser");
            reg("Nice walk! {0} more steps in {1} min.", "\u00A1Buen paseo! {0} pasos m\u00E1s en {1} min.", "Sch\u00F6ner Spaziergang! {0} Schritte mehr in {1} Min.");
            reg("Halfway there! {0} of {1} steps.", "\u00A1Ya vas por la mitad! {0} de {1} pasos.", "Halb geschafft! {0} von {1} Schritten.");
            reg("{0} steps! You reached today's goal.", "\u00A1{0} pasos! Has cumplido tu objetivo de hoy.", "{0} Schritte! Dein heutiges Ziel ist geschafft.");
            reg("What a day! You're already at {0} captures.", "\u00A1Vaya d\u00EDa! Ya llevas {0} capturas.", "Was f\u00FCr ein Tag! Du bist schon bei {0} Aufnahmen.");
            reg("Okay, I won't.", "Vale, no la hago.", "Na gut, dann nicht.");

            // ReportWindow: report an issue, my reports
            reg("New report", "Nuevo reporte", "Neue Meldung");
            reg("What you sent from this computer, how it's going and the replies you get. No account needed.",
                "Lo que has enviado desde este equipo, c\u00F3mo va y las respuestas que recibas. No hace falta ninguna cuenta.",
                "Was du von diesem Rechner gesendet hast, wie es l\u00E4uft und die Antworten darauf. Kein Konto n\u00F6tig.");
            reg("You haven't sent anything from this computer yet. When you do, it will appear here.",
                "Todav\u00EDa no has enviado nada desde este equipo. Cuando lo hagas, aparecer\u00E1 aqu\u00ED.",
                "Von diesem Rechner hast du noch nichts gesendet. Sobald du es tust, erscheint es hier.");
            reg("Checking how they're doing\u2026", "Consultando c\u00F3mo van\u2026", "Stand wird abgerufen\u2026");
            reg("Refresh", "Actualizar", "Aktualisieren");
            reg("Pending", "Pendientes", "Offen");
            reg("Cannot check how they are doing right now. Check your connection and click Refresh.",
                "Ahora no se puede consultar c\u00F3mo van. Comprueba la conexi\u00F3n y pulsa Actualizar.",
                "Der Stand l\u00E4sst sich gerade nicht abrufen. Pr\u00FCfe die Verbindung und klicke auf Aktualisieren.");
            reg("No connection to reports server.", "No hay conexi\u00F3n con el servidor de reportes.", "Keine Verbindung zum Meldungsserver.");
            reg("GitHub rate limit reached; will try again later.", "GitHub ha limitado las consultas durante un rato; se volver\u00E1 a intentar m\u00E1s tarde.", "Das GitHub-Limit ist erreicht; es wird sp\u00E4ter erneut versucht.");
            reg("GitHub is not responding right now; will try again later.", "GitHub no responde ahora mismo; se volver\u00E1 a intentar m\u00E1s tarde.", "GitHub antwortet gerade nicht; es wird sp\u00E4ter erneut versucht.");
            reg("The server did not confirm the report.", "El servidor no ha confirmado el reporte.", "Der Server hat die Meldung nicht best\u00E4tigt.");
            reg("Too many reports in a row. Try again in a minute.", "Demasiados reportes seguidos. Prueba dentro de un minuto.", "Zu viele Meldungen hintereinander. Versuch es in einer Minute wieder.");
            reg("Wait a minute before sending another report.", "Espera un minuto antes de enviar otro reporte.", "Warte eine Minute, bevor du eine weitere Meldung sendest.");
            reg("Title", "T\u00EDtulo", "Titel");
            reg("Description", "Descripci\u00F3n", "Beschreibung");
            reg("Steps to reproduce", "Pasos para repetirlo", "Schritte zum Nachstellen");
            reg("How to reproduce?", "\u00BFC\u00F3mo se repite?", "Wie l\u00E4sst es sich nachstellen?");
            reg("How to reproduce? (optional)", "\u00BFC\u00F3mo se repite? (opcional)", "Wie l\u00E4sst es sich nachstellen? (optional)");
            reg("Comment", "Comentario", "Kommentar");
            reg("Idea", "Idea", "Idee");
            reg("Bug", "Fallo", "Fehler");
            reg("Include Stackshot log", "Incluir el registro de Stackshot", "Stackshot-Protokoll einschlie\u00DFen");
            reg("Log", "Registro", "Protokoll");
            reg("(no log)", "(sin registro)", "(ohne Protokoll)");
            reg("--- Log (only with switch turned on) ---", "--- Registro (solo con el interruptor encendido) ---", "--- Protokoll (nur bei eingeschaltetem Schalter) ---");
            reg("What is sent", "Lo que se env\u00EDa", "Was gesendet wird");
            reg("Hide what is sent", "Ocultar lo que se env\u00EDa", "Verbergen, was gesendet wird");
            reg("View what is sent", "Ver lo que se env\u00EDa", "Anzeigen, was gesendet wird");
            reg("Always added: ", "Siempre se a\u00F1ade: ", "Immer dabei: ");
            reg(". Your username, computer name, emails, IDs, account and card numbers, and phone numbers are removed from what you write and the log.",
                ". De lo que escribes y del registro se quitan tu usuario, el nombre del equipo, correos, DNI y NIE, cuentas, tarjetas y tel\u00E9fonos.",
                ". Benutzername, Computername, E-Mail-Adressen, Ausweisnummern, Konto- und Kartennummern sowie Telefonnummern werden aus dem Geschriebenen und dem Protokoll entfernt.");
            reg("It goes straight to the people who make Stackshot, with no accounts or emails. ", "Llega directamente a quien hace Stackshot, sin cuentas ni correos. ", "Sie geht direkt an die Menschen hinter Stackshot, ohne Konten oder E-Mails. ");
            reg("It is published on GitHub, which is public: do not include personal data, passwords or company info.",
                "Se publica en GitHub, que es p\u00FAblico: no pongas datos personales, contrase\u00F1as ni nada de tu empresa.",
                "Es wird auf GitHub ver\u00F6ffentlicht, das \u00F6ffentlich ist: keine pers\u00F6nlichen Daten, Passw\u00F6rter oder Firmeninterna.");
            reg("Continuing opens GitHub with everything you wrote prefilled. ", "Al continuar se abre GitHub con todo lo que escribas ya rellenado. ", "Weiter \u00F6ffnet GitHub, mit allem was du geschrieben hast, schon ausgef\u00FCllt. ");
            reg("Opens the issue in browser with everything you wrote", "Abre la incidencia en el navegador con todo lo que has escrito", "\u00D6ffnet die Meldung im Browser, mit allem was du geschrieben hast");
            reg("Open on GitHub", "Abrir en GitHub", "Auf GitHub \u00F6ffnen");
            reg("Continue on GitHub", "Continuar en GitHub", "Auf GitHub fortfahren");
            reg("Send", "Enviar", "Senden");
            reg("Sending\u2026", "Enviando\u2026", "Wird gesendet\u2026");
            reg("Sent", "Enviado", "Gesendet");
            reg("Sent. Thank you", "Enviado. Gracias", "Gesendet. Danke");
            reg("Could not send: ", "No se ha podido enviar: ", "Senden fehlgeschlagen: ");
            reg("Could not send ({0}).", "No se ha podido enviar ({0}).", "Senden fehlgeschlagen ({0}).");
            reg("What you wrote has not been sent.", "Lo que has escrito no se ha enviado.", "Was du geschrieben hast, wurde nicht gesendet.");
            reg("You can retry or open on GitHub.", "Puedes reintentarlo o abrirlo en GitHub.", "Du kannst es erneut versuchen oder auf GitHub \u00F6ffnen.");
            reg("Tell us what happened", "Cu\u00E9ntanos qu\u00E9 pasa", "Erz\u00E4hl uns, was passiert ist");
            reg("Type a title and {0}.", "Escribe un t\u00EDtulo y {0}.", "Schreib einen Titel und {0}.");
            reg("Title needs {0} more.", "El t\u00EDtulo necesita {0} m\u00E1s.", "Dem Titel fehlen noch {0}.");
            reg("{0} needs {1} more.", "{0} necesita {1} m\u00E1s.", "{0} braucht noch {1}.");
            reg("minimum {0} characters", "m\u00EDnimo {0} letras", "mindestens {0} Zeichen");
            reg("1 letter", "1 letra", "1 Buchstabe");
            reg("{0} letters", "{0} letras", "{0} Buchstaben");
            reg("Discard report?", "\u00BFDescartar el reporte?", "Meldung verwerfen?");
            reg("Your report is number {0}. Under \u201CMy reports\u201D you will see its progress and replies, right here with no account.",
                "Tu reporte es el n\u00FAmero {0}. En \u00ABMis reportes\u00BB ver\u00E1s c\u00F3mo avanza y las respuestas, aqu\u00ED mismo y sin cuenta.",
                "Deine Meldung hat die Nummer {0}. Unter \u201EMeine Meldungen\u201C siehst du den Stand und die Antworten, direkt hier und ohne Konto.");
            reg("#{0}  \u00B7  {1}  \u00B7  sent on {2}", "N.\u00BA {0}  \u00B7  {1}  \u00B7  enviado el {2}", "Nr. {0}  \u00B7  {1}  \u00B7  gesendet am {2}");
            reg("Closed", "Cerrado", "Geschlossen");
            reg("Discarded", "Descartado", "Verworfen");
            reg("Duplicate", "Repetido", "Duplikat");
            reg("Fixed", "Arreglado", "Behoben");
            reg("is done", "est\u00E1 hecho", "ist erledigt");
            reg("is fixed", "est\u00E1 arreglado", "wurde behoben");
            reg("was discarded", "se ha descartado", "wurde verworfen");
            reg("is a duplicate", "est\u00E1 repetido", "ist ein Duplikat");
            reg("was closed", "se ha cerrado", "wurde geschlossen");
            reg("was reopened", "se ha vuelto a abrir", "wurde wieder ge\u00F6ffnet");
            reg("has a new reply", "tiene una respuesta nueva", "hat eine neue Antwort");
            reg("has {0} new replies", "tiene {0} respuestas nuevas", "hat {0} neue Antworten");
            reg("\u201C{0}\u201D {1}", "\u00AB{0}\u00BB {1}", "\u201E{0}\u201C {1}");
            reg("New reply", "Respuesta nueva", "Neue Antwort");
            reg("Reply from Stackshot", "Respuesta de Stackshot", "Antwort von Stackshot");
            reg("Resolve it", "Resolverlo", "Beheben");
            reg("Mark as done", "Marcar como hecha", "Als erledigt markieren");
            reg("Mark as pending", "Marcar como pendiente", "Als offen markieren");
            reg("Not available", "No disponible", "Nicht verf\u00FCgbar");
            reg("Could not capture", "No se pudo capturar", "Es konnte nicht aufgenommen werden");
            reg("Could not {0} ({1}).", "No se pudo {0} ({1}).", "{0} fehlgeschlagen ({1}).");
            reg("Could not {0}. ", "No se pudo {0}. ", "{0} schlug fehl. ");
            reg("Could not {0}. Will try again later.", "No se pudo {0}. Se volver\u00E1 a intentar m\u00E1s tarde.", "{0} war nicht m\u00F6glich. Es wird sp\u00E4ter erneut versucht.");
            reg("Could not {0} (error {1}).", "No se pudo {0} (error {1}).", "{0} fehlgeschlagen (Fehler {1}).");
            reg("The network proxy does not let Stackshot {0}.", "El proxy de la red no deja {0}.", "Der Netzwerk-Proxy l\u00E4sst Stackshot {0} nicht zu.");
            reg("download the update", "descargar la actualizaci\u00F3n", "das Herunterladen des Updates");
            reg("check GitHub", "consultar GitHub", "das \u00DCberpr\u00FCfen von GitHub");
            reg("Failed to open the file. Try again; if it recurs, the Stackshot log has the details.",
                "Ha fallado al abrir el archivo. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.",
                "Die Datei lie\u00DF sich nicht \u00F6ffnen. Versuch es erneut; wenn es wieder passiert, stehen die Details im Stackshot-Protokoll.");
            reg("Failed to read or write the file. Try again; if it recurs, the Stackshot log has the details.",
                "Ha fallado al leer o escribir el archivo. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.",
                "Die Datei lie\u00DF sich nicht lesen oder schreiben. Versuch es erneut; wenn es wieder passiert, stehen die Details im Stackshot-Protokoll.");
            reg("An unexpected error occurred. Please try again.", "Ha fallado algo inesperado. Vuelve a intentarlo.", "Ein unerwarteter Fehler ist aufgetreten. Versuch es bitte erneut.");
            reg("An unexpected error occurred. Please try again; if it recurs, check the Stackshot log for details.",
                "Ha fallado algo inesperado. Vuelve a intentarlo; si se repite, el registro de Stackshot tiene los detalles.",
                "Ein unerwarteter Fehler ist aufgetreten. Versuch es erneut; wenn es wieder passiert, schau im Stackshot-Protokoll nach den Details.");
            reg("Another program is using the file. Close it and try again.", "Otro programa est\u00E1 usando el archivo. Ci\u00E9rralo y vuelve a intentarlo.", "Ein anderes Programm verwendet die Datei. Schlie\u00DFe es und versuch es erneut.");
            reg("The file no longer exists.", "El archivo ya no existe.", "Die Datei existiert nicht mehr.");
            reg("Windows cannot open the file (due to permissions or antivirus).", "Windows no deja abrir el archivo (por sus permisos o por el antivirus).", "Windows kann die Datei nicht \u00F6ffnen (wegen der Berechtigungen oder des Virenschutzes).");
            reg("Windows has no application to open this type of file.", "Windows no tiene ninguna aplicaci\u00F3n para abrir este tipo de archivo.", "Windows hat keine App, um diesen Dateityp zu \u00F6ffnen.");
            reg("Windows or the antivirus blocked it.", "Windows o el antivirus lo ha bloqueado.", "Windows oder der Virenschutz hat es blockiert.");
            reg("The path is too long for Windows. Choose a folder with a shorter path.", "La ruta es demasiado larga para Windows. Elige una carpeta con una ruta m\u00E1s corta.", "Der Pfad ist f\u00FCr Windows zu lang. W\u00E4hle einen Ordner mit k\u00FCrzerem Pfad.");
            reg("Not enough memory. Close something and try again.", "No hay memoria suficiente. Cierra algo y vuelve a intentarlo.", "Nicht genug Arbeitsspeicher. Schlie\u00DFe etwas und versuch es erneut.");
            reg("Not enough memory for such a large image. Try a smaller area.", "No hay memoria suficiente para una imagen tan grande. Prueba con un \u00E1rea m\u00E1s peque\u00F1a.", "Nicht genug Arbeitsspeicher f\u00FCr so ein gro\u00DFes Bild. Nimm einen kleineren Bereich.");
            reg("1 display: ", "1 pantalla: ", "1 Bildschirm: ");
            reg("{0} displays: ", "{0} pantallas: ", "{0} Bildschirme: ");
            reg(" at ", " al ", " mit ");
            reg("Time", "Hora", "Uhrzeit");

            // Card, Card.Access
            reg("Pin to screen", "Fijar en pantalla", "Auf dem Bildschirm anheften");
            reg("Copy at normal size", "Copiar a tama\u00F1o normal", "In normaler Gr\u00F6\u00DFe kopieren");
            reg("Copy in high definition (\u00D72)", "Copiar en alta definici\u00F3n (\u00D72)", "In hoher Aufl\u00F6sung kopieren (\u00D72)");
            reg("Join", "Unir", "Zusammenf\u00FCgen");
            reg("Images only", "Solo im\u00E1genes", "Nur Bilder");
            reg("Copied in HD", "Copiado en HD", "In HD kopiert");
            reg("Reading\u2026", "Leyendo\u2026", "Wird gelesen\u2026");
            reg("Could not read", "No se pudo leer", "Konnte nicht gelesen werden");
            reg("Could not pin capture", "No se pudo fijar la captura", "Die Aufnahme konnte nicht angeheftet werden");
            reg("Could not read the image. Please try again.", "No se ha podido leer la imagen. Vuelve a intentarlo.", "Das Bild konnte nicht gelesen werden. Versuch es bitte erneut.");
            reg("Please try again.", "Vuelve a intentarlo.", "Versuch es bitte erneut.");
            reg("Could not show", "No se pudo mostrar", "Konnte nicht angezeigt werden");
            reg("Video ", "V\u00EDdeo ", "Video ");
            reg("Capture ", "Captura ", "Aufnahme ");
            reg("Saved in ", "Guardada en ", "Gespeichert in ");
            reg("Not saved yet", "Sin guardar todav\u00EDa", "Noch nicht gespeichert");
            reg("Combined", "Unidas", "Zusammengef\u00FCgt");
            reg("The captures are too large or could not be read. Please try again.", "Las capturas son demasiado grandes o no se han podido leer. Vuelve a intentarlo.", "Die Aufnahmen sind zu gro\u00DF oder lie\u00DFen sich nicht lesen. Versuch es bitte erneut.");
            reg("Could not open viewer", "No se pudo abrir el visor", "Die Ansicht konnte nicht ge\u00F6ffnet werden");
            reg("Saving GIF\u2026", "Guardando el GIF\u2026", "GIF wird gespeichert\u2026");
            reg("Saving video\u2026", "Guardando el v\u00EDdeo\u2026", "Video wird gespeichert\u2026");
            reg("{0}%  \u00B7  it will appear here", "{0} %  \u00B7  aparecer\u00E1 aqu\u00ED", "{0} %  \u00B7  erscheint gleich hier");
            reg("It will appear here in a moment", "Aparecer\u00E1 aqu\u00ED en un momento", "Sie erscheint gleich hier");
            reg("Sales Report.xlsx - Excel", "Informe de ventas.xlsx - Excel", "Verkaufsbericht.xlsx - Excel");

            // Updater
            reg("Could not start update. ", "No se pudo iniciar la actualizaci\u00F3n. ", "Das Update konnte nicht gestartet werden. ");
            reg("too large", "demasiado grande", "zu gro\u00DF");   // the download-size guard, shown in the failed-update notice
            reg("code ", "c\u00F3digo ", "Code ");
            reg("Installation did not complete{0}. Try again or download from GitHub.", "La instalaci\u00F3n no se ha completado{0}. Vuelve a intentarlo o desc\u00E1rgala desde GitHub.", "Die Installation wurde nicht abgeschlossen{0}. Versuch es erneut oder lade sie von GitHub herunter.");
            reg("Installing {0} did not complete. Try again or download it from GitHub.", "La instalaci\u00F3n de la {0} no se complet\u00F3. Vuelve a intentarlo o desc\u00E1rgala desde GitHub.", "Die Installation von {0} wurde nicht abgeschlossen. Versuch es erneut oder lade sie von GitHub herunter.");
            reg("The download is incomplete; it was discarded.", "La descarga est\u00E1 incompleta; se ha descartado.", "Der Download ist unvollst\u00E4ndig; er wurde verworfen.");
            reg("The download does not match the published SHA-256 checksum; it was discarded.", "La descarga no coincide con la suma SHA-256 publicada; se ha descartado.", "Der Download stimmt nicht mit der ver\u00F6ffentlichten SHA-256-Pr\u00FCfsumme \u00FCberein; er wurde verworfen.");
            reg("The update's signature is not valid; it was discarded.", "La firma de la actualizaci\u00F3n no es v\u00E1lida; se ha descartado.", "Die Signatur des Updates ist ung\u00FCltig; es wurde verworfen.");
            reg("The downloaded version does not match the published one; it was discarded.", "La versi\u00F3n descargada no coincide con la publicada; se ha descartado.", "Die heruntergeladene Version stimmt nicht mit der ver\u00F6ffentlichten \u00FCberein; sie wurde verworfen.");
            reg("Connection lost during download.", "Se ha perdido la conexi\u00F3n durante la descarga.", "Verbindung w\u00E4hrend des Downloads verloren.");
            reg("No connection to GitHub. Will try again when network is restored.", "Sin conexi\u00F3n con GitHub. Se volver\u00E1 a intentar cuando vuelva la red.", "Keine Verbindung zu GitHub. Es wird erneut versucht, sobald das Netz wieder da ist.");
            reg("Could not open a secure connection to GitHub.", "No se pudo abrir una conexi\u00F3n segura con GitHub.", "Es konnte keine sichere Verbindung zu GitHub ge\u00F6ffnet werden.");
            reg("There are unsaved changes in Stackshot", "Hay cambios sin guardar en Stackshot", "Es gibt ungespeicherte \u00C4nderungen in Stackshot");
            reg("A recording is in progress in Stackshot", "Hay una grabaci\u00F3n en marcha en Stackshot", "In Stackshot l\u00E4uft eine Aufnahme");
            reg("A recording is in progress.\n\nStackshot will stop and save it to your folder before exiting.",
                "Hay una grabaci\u00F3n en marcha.\n\nStackshot la detiene y la guarda en tu carpeta antes de salir.",
                "Es l\u00E4uft eine Aufnahme.\n\nStackshot stoppt sie und speichert sie in deinem Ordner, bevor es beendet wird.");
            reg("Windows wants to sign out and a recording is in progress.\n\nStackshot will stop and save it to your folder before exiting.",
                "Windows quiere cerrar la sesi\u00F3n y hay una grabaci\u00F3n en marcha.\n\nStackshot la detiene y la guarda en tu carpeta antes de salir.",
                "Windows will sich abmelden und es l\u00E4uft eine Aufnahme.\n\nStackshot stoppt sie und speichert sie in deinem Ordner, bevor es beendet wird.");

            // News
            reg("What's new in Stackshot {0}", "Novedades de Stackshot {0}", "Neu in Stackshot {0}");
            reg("The new version: here is what's new.", "La versi\u00F3n nueva: esto es lo que trae.", "Die neue Version: das ist neu darin.");
            reg("Everything new since the version you had.", "Todo lo nuevo desde la versi\u00F3n que ten\u00EDas.", "Alles Neue seit der Version, die du hattest.");
            reg("What's new in your current version.", "Lo que trae la versi\u00F3n que tienes.", "Was es in deiner aktuellen Version Neues gibt.");
            reg("In {0}", "En la {0}", "In {0}");
            reg("This version has no release notes.", "Esta versi\u00F3n no trae notas.", "Diese Version hat keine Versionshinweise.");
            reg("Updating\u2026", "Actualizando\u2026", "Wird aktualisiert\u2026");
            reg("Stackshot {0} is here", "Stackshot {0} ya est\u00E1 aqu\u00ED", "Stackshot {0} ist da");
            reg("Highlights include: \u201C{0}\u201D. ", "Entre sus novedades: \u00AB{0}\u00BB. ", "Zu den Neuheiten geh\u00F6rt: \u201E{0}\u201C. ");
            reg("Would you like to see what's new?", "\u00BFQuieres ver qu\u00E9 trae?", "M\u00F6chtest du sehen, was neu ist?");
            reg("View what's new", "Ver novedades", "Neuigkeiten ansehen");
            reg("Updates on your report", "Novedades en tu reporte", "Neues zu deiner Meldung");
            reg("Updates on {0} of your reports", "Novedades en {0} de tus reportes", "Neues zu {0} deiner Meldungen");
            reg("View my reports", "Ver mis reportes", "Meine Meldungen ansehen");

            // Toast / ShotStack.Toast
            reg("New XML", "Nuevo XML", "Neue XML-Datei");
            reg(". The viewer has unsaved changes: open it from the tray when finished.", ". El visor tiene cambios sin guardar: \u00E1brelo desde la bandeja cuando acabes.", ". Die Ansicht hat ungespeicherte \u00C4nderungen: \u00F6ffne sie \u00FCber den Infobereich, wenn du fertig bist.");
            reg("Open XML", null, "XML \u00F6ffnen");
            reg("picking an area", "eligiendo un \u00E1rea", "ein Bereich wird ausgew\u00E4hlt");
            reg("session locked or away", "sesi\u00F3n bloqueada o sin nadie delante", "Sitzung gesperrt oder niemand am Rechner");
            reg("full-screen application", "aplicaci\u00F3n a pantalla completa", "Anwendung im Vollbild");
            reg("full-screen game", "juego a pantalla completa", "Spiel im Vollbild");
            reg("presentation mode", "modo presentaci\u00F3n", "Pr\u00E4sentationsmodus");
            reg("Windows focus assist / quiet hours", "tiempo de silencio de Windows", "Windows-Fokusassistent / Ruhezeiten");
            reg("There is no XML in {0}.", "No hay ning\u00FAn XML en {0}.", "In {0} gibt es keine XML-Datei.");
            reg("No text found", "No hay texto", "Kein Text gefunden");
            reg("No text found in the image.", "No se ha encontrado texto en la imagen.", "Im Bild wurde kein Text gefunden.");
            reg("Could not read text", "No se ha podido leer el texto", "Text konnte nicht gelesen werden");

            // Settings: comments written into the settings file
            reg("# Stackshot settings. Change them from the tray icon > Settings.",
                "# Ajustes de Stackshot. Se pueden cambiar desde el icono de la bandeja > Ajustes.",
                "# Stackshot-Einstellungen. \u00C4nderbar \u00FCber das Symbol im Infobereich > Einstellungen.");
            reg("# Shortcuts (several, comma separated). Examples: PrintScreen, Ctrl+Shift+S, Alt+F1",
                "# Atajos (varios separados por comas). Ejemplos: PrintScreen, Ctrl+Shift+S, Alt+F1",
                "# Tastenk\u00FCrzel (mehrere, durch Kommas getrennt). Beispiele: PrintScreen, Strg+Umschalt+S, Alt+F1");
            reg("# Path to ffmpeg.exe (empty = the one Stackshot downloads the first time you record)",
                "# Ruta a ffmpeg.exe (vac\u00EDo = el que descarga Stackshot la primera vez que grabas)",
                "# Pfad zu ffmpeg.exe (leer = den Stackshot beim ersten Aufnehmen herunterl\u00E4dt)");
            reg("# Editor backdrop (margin and corners 0 to 100; ratio: auto, 16:9, 4:3 or 1:1)",
                "# Fondo de presentaci\u00F3n del editor (margen y esquinas de 0 a 100; proporci\u00F3n: auto, 16:9, 4:3 o 1:1)",
                "# Editor-Hintergrund (Rand und Ecken 0 bis 100; Verh\u00E4ltnis: auto, 16:9, 4:3 oder 1:1)");
            reg("# Backdrop type: 0 gradient or image, 1 solid color (BgColor, #RRGGBB), 2 the blurred capture (BgBlur: 0 as is, 1 darker, 2 lighter)",
                "# Tipo de fondo: 0 degradado o imagen, 1 color liso (BgColor, #RRGGBB), 2 la captura desenfocada (BgBlur: 0 tal cual, 1 oscura, 2 clara)",
                "# Hintergrundtyp: 0 Verlauf oder Bild, 1 einfarbig (BgColor, #RRGGBB), 2 die unscharfe Aufnahme (BgBlur: 0 original, 1 dunkler, 2 heller)");
            reg("# Shadow, inset 0 to 100; alignment 0 to 8 by rows from the top left (4 centered)",
                "# Sombra, inserci\u00F3n de 0 a 100; alineaci\u00F3n de 0 a 8 por filas desde arriba a la izquierda (4 centrada)",
                "# Schatten, Einr\u00FCckung 0 bis 100; Ausrichtung 0 bis 8 nach Reihen von oben links (4 zentriert)");
            reg("# Theme: 0 like Windows, 1 light, 2 dark", "# Tema: 0 como Windows, 1 claro, 2 oscuro", "# Design: 0 wie Windows, 1 hell, 2 dunkel");
            reg("# Temporary captures: 0 one hour, 1 until closing, 2 one day, 3 one week, 4 one month",
                "# Capturas temporales: 0 una hora, 1 hasta cerrar, 2 un d\u00EDa, 3 una semana, 4 un mes",
                "# Tempor\u00E4re Aufnahmen: 0 eine Stunde, 1 bis zum Schlie\u00DFen, 2 ein Tag, 3 eine Woche, 4 ein Monat");
            reg("# Healthy breaks: minutes at the computer between reminders (0 = no)",
                "# Pausas saludables: minutos delante del ordenador entre avisos (0 = no)",
                "# Gesunde Pausen: Minuten am Computer zwischen den Erinnerungen (0 = nein)");
            reg("# Focus mode: minutes of the block, of the short and the long break, blocks until the long one",
                "# Modo concentracion: minutos del bloque, del descanso corto y del largo, bloques hasta el largo",
                "# Fokusmodus: Minuten des Blocks, der kurzen und der langen Pause, Bl\u00F6cke bis zur langen Pause");
            reg("# Health: glasses of water a day (0 = no goal) and hour of the day summary (0 = no)",
                "# Salud: vasos de agua al dia (0 = sin objetivo) y hora del resumen del dia (0 = no)",
                "# Gesundheit: Gl\u00E4ser Wasser pro Tag (0 = kein Ziel) und Stunde der Tageszusammenfassung (0 = nein)");
            reg("# Cinema: frames per second (60 or 30) and memory for motion spikes (0 normal, 1 high)",
                "# Cine: fotogramas por segundo (60 o 30) y memoria para picos de movimiento (0 normal, 1 alta)",
                "# Kino: Bilder pro Sekunde (60 oder 30) und Speicher f\u00FCr Bewegungsspitzen (0 normal, 1 hoch)");
            reg("# Before recording: shot options (1/0) and countdown in seconds (0, 3, 5 or 10)",
                "# Antes de grabar: opciones de la toma (1/0) y cuenta atr\u00E1s en segundos (0, 3, 5 o 10)",
                "# Vor der Aufnahme: Optionen der Aufnahme (1/0) und Countdown in Sekunden (0, 3, 5 oder 10)");
            reg("# Player: open videos in Stackshot (1/0), scaling (0 best, 1 fast), stretch to fill the window (1/0; 0 = actual size), volume 0-100, repeat (1/0)",
                "# Reproductor: abrir los v\u00EDdeos en Stackshot (1/0), escalado (0 mejor, 1 r\u00E1pido), ampliar para llenar la ventana (1/0; 0 = tama\u00F1o real), volumen 0-100, repetir (1/0)",
                "# Player: Videos in Stackshot \u00F6ffnen (1/0), Skalierung (0 besser, 1 schnell), auf Fenstergr\u00F6\u00DFe vergr\u00F6\u00DFern (1/0; 0 = Originalgr\u00F6\u00DFe), Lautst\u00E4rke 0-100, Wiederholen (1/0)");
            reg("# Editor MP4: 0 maximum, 1 high, 2 light, 3 up to 16 MB (WhatsApp), 4 up to 25 MB (email)",
                "# MP4 del editor: 0 m\u00E1xima, 1 alta, 2 ligera, 3 hasta 16 MB (WhatsApp), 4 hasta 25 MB (correo)",
                "# Editor-MP4: 0 maximal, 1 hoch, 2 leicht, 3 bis 16 MB (WhatsApp), 4 bis 25 MB (E-Mail)");
            reg("# XML viewer: open only the new XML files from a folder (1/0) and that folder (empty = none)",
                "# Visor XML: abrir solos los XML nuevos de una carpeta (1/0) y esa carpeta (vac\u00EDa = ninguna)",
                "# XML-Ansicht: nur neue XML-Dateien aus einem Ordner \u00F6ffnen (1/0) und dieser Ordner (leer = keiner)");
            reg("# Captures: cursor (1/0), name ({tipo} {fecha} {hora} {app} {ventana}), subfolders when saving (0 no, 1 by month, 2 by year and month), cover sensitive data (1/0), search by text (1/0)",
                "# Capturas: puntero (1/0), nombre ({tipo} {fecha} {hora} {app} {ventana}), subcarpetas al guardar (0 no, 1 por mes, 2 por a\u00F1o y mes), tapar datos sensibles (1/0), buscar por el texto (1/0)",
                "# Aufnahmen: Mauszeiger (1/0), Name ({tipo} {fecha} {hora} {app} {ventana}), Unterordner beim Speichern (0 nein, 1 pro Monat, 2 pro Jahr und Monat), sensible Daten schw\u00E4rzen (1/0), nach Text suchen (1/0)");
            reg("# Recording: show the clicks (1/0) and the keys pressed (1/0)",
                "# Grabaci\u00F3n: mostrar los clics (1/0) y las teclas pulsadas (1/0)",
                "# Aufnahme: Klicks anzeigen (1/0) und gedr\u00FCckte Tasten anzeigen (1/0)");
            reg("# State: what Stackshot remembers on its own (no need to touch it)",
                "# Estado: lo que Stackshot recuerda por su cuenta (no hace falta tocarlo)",
                "# Zustand: was Stackshot sich selbst merkt (muss nicht ge\u00E4ndert werden)");

            // Welcome
            reg("Press {0} to capture.", "Pulsa {0} y listo.", "Dr\u00FCcke {0} und fertig.");   // Welcome subtitle line; the caller prepends the line break

            // QuickWindow extras
            reg("Whatever you want to note", "Lo que quieras apuntar", "Was du notieren willst");
            reg("For example: call Laura", "Por ejemplo: llamar a Laura", "Zum Beispiel: Laura anrufen");
            reg("Start a line with \u201C- [ ] \u201D for a task list.", "Empieza una l\u00EDnea con \u00AB- [ ] \u00BB para una lista de tareas.", "Beginne eine Zeile mit \u201E- [ ] \u201C f\u00FCr eine Aufgabenliste.");
            reg("Pin to desktop", "Fijar en el escritorio", "Auf dem Desktop anheften");
            reg("View all (Notas.md, in your captures folder)", "Ver todas (Notas.md, en tu carpeta de capturas)", "Alle ansehen (Notas.md, in deinem Aufnahmeordner)");
            reg("View my notes (Notas.md, in your captures folder)", "Ver mis notas (Notas.md, en tu carpeta de capturas)", "Meine Notizen ansehen (Notas.md, in deinem Aufnahmeordner)");
            reg("Remind me", "Recu\u00E9rdamelo", "Erinnere mich");
            reg("Ctrl+Enter", "Ctrl+Intro", "Strg+Eingabe");
            reg("Ctrl + Enter", "Ctrl + Intro", "Strg + Eingabe");
            reg("Save and pin", "Guardar y fijar", "Speichern und anheften");

            // ReportWindow extras
            reg("Something's broken", "Algo falla", "Etwas funktioniert nicht");
            reg("I have an idea", "Tengo una idea", "Ich habe eine Idee");

            // ShotStack.Assist extras
            reg("# Notes\n\n", "# Notas\n\n", "# Notizen\n\n");
            reg("I don't know what to do with \u201C{0}\u201D. Drop images, videos, XML or Markdown.",
                "No s\u00E9 qu\u00E9 hacer con \u00AB{0}\u00BB. Su\u00E9ltame im\u00E1genes, v\u00EDdeos, XML o Markdown.",
                "Ich wei\u00DF nicht, was ich mit \u201E{0}\u201C anfangen soll. Zieh mir Bilder, Videos, XML oder Markdown her.");

            // ShotStack.Focus extras
            reg("Focus: block 1 of {0}", "Concentraci\u00F3n: bloque 1 de {0}", "Fokus: Block 1 von {0}");
            reg("Focus: block {0} of {1}", "Concentraci\u00F3n: bloque {0} de {1}", "Fokus: Block {0} von {1}");
            reg("Continue", "Seguir", "Fortsetzen");

            // ShotStack extras
            reg("No XML folder is monitored yet: choose it in Settings > General > Advanced.",
                "Todav\u00EDa no hay ninguna carpeta de XML vigilada: el\u00EDgela en Ajustes > General > Avanzado.",
                "Es wird noch kein XML-Ordner \u00FCberwacht: w\u00E4hle ihn unter Einstellungen > Allgemein > Erweitert.");
            reg("{0} older", "{0} anterior", "{0} \u00E4ltere");
            reg("{0} newer", "{0} m\u00E1s reciente", "{0} neuere");

            // ShotStack.Health day summary parts
            reg("{0} glass of water", "{0} vaso de agua", "{0} Glas Wasser");
            reg("{0} glasses of water", "{0} vasos de agua", "{0} Gl\u00E4ser Wasser");
            reg("{0} eye rest", "{0} descanso de vista", "{0} Augenpause");
            reg("{0} eye rests", "{0} descansos de vista", "{0} Augenpausen");
            reg("{0} movement break", "{0} pausa para moverte", "{0} Bewegungspause");
            reg("{0} movement breaks", "{0} pausas para moverte", "{0} Bewegungspausen");
            reg("{0} minute breathing", "{0} minuto respirando", "{0} Minute Durchatmen");
            reg("{0} minutes breathing", "{0} minutos respirando", "{0} Minuten Durchatmen");
        }
    }
}
