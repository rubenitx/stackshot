// Stackshot - German localization catalog: MascotHome.
// MIT License - https://github.com/rubenitx/stackshot
using System;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterGermanMascotHome(Action<string, string, string> reg)
        {
            // The commands menu on Home and the mascot page
            reg("More", "Más", "Mehr");
            reg("Everything it knows how to do", "Todo lo que sabe hacer", "Alles, was es kann");
            reg("Play", "Juega", "Spielen");
            reg("Ask {0} to do something", "Pídele algo a {0}", "Frag {0}, was es tun soll");

            // Behavior, in the mascot settings
            reg("Behavior", "Comportamiento", "Verhalten");
            reg("Activity", "Actividad", "Aktivität");
            reg("How often it does things on its own.", "Cada cuánto hace cosas por su cuenta.", "Wie oft es von sich aus etwas macht.");
            reg("Calm", "Tranquila", "Ruhig");
            reg("Normal", "Normal", "Normal");
            reg("Playful", "Juguetona", "Verspielt");
            reg("Chatter", "Charla", "Geplapper");
            reg("How often it comments on what it does and shares tips.", "Cuánto comenta lo que hace y te cuenta trucos.", "Wie viel es kommentiert, was es tut, und Tipps verrät.");
            reg("Low", "Poca", "Wenig");
            reg("Falls asleep when idle", "Se duerme sola", "Schläft bei Untätigkeit ein");
            reg("If left alone for a while, yawns and takes a nap.", "Si no le haces caso un rato, bosteza y se echa una siesta.", "Wenn du es eine Weile ignorierst, gähnt es und macht ein Nickerchen.");
            reg("Time-based behavior", "Según la hora del día", "Je nach Tageszeit");
            reg("Morning coffee, nightcap in the evening, Friday celebrations, and camera flash on captures.",
                "Café por la mañana, gorro de dormir por la noche y fiesta los viernes; y un flash cada vez que haces una captura.",
                "Morgens Kaffee, abends Schlafmütze, freitags Party — und bei jeder Aufnahme ein Blitz.");
            reg("Roam all monitors", "Recorre todas las pantallas", "Alle Bildschirme erkunden");
            reg("On the desktop, walks across monitors instead of staying on one.", "En el escritorio pasea de un monitor a otro en vez de quedarse en uno.", "Auf dem Desktop läuft es von einem Bildschirm zum anderen, statt auf einem zu bleiben.");
            reg("Hide in full screen", "Se aparta con la pantalla completa", "Weicht im Vollbild aus");
            reg("During full screen games, presentations, or remote sessions, moves to another screen or hides. Turn off to keep always on top.",
                "Si un juego, una presentación o una ventana a pantalla completa (como el Escritorio remoto o una máquina virtual) ocupa su pantalla, se va a otra libre y no entra en esa; si no hay otra, se queda detrás. Apágalo y se queda siempre delante.",
                "Bei Vollbildspielen, Präsentationen oder Remote-Sitzungen weicht es auf einen anderen Bildschirm aus oder versteckt sich. Schalte es aus, damit es immer im Vordergrund bleibt.");
            reg("Idle activities", "Lo que hace por su cuenta", "Was es von sich aus macht");
            reg("Hops and gestures", "Saltitos y gestos", "Hüpfer und Gesten");
            reg("Hops, winks, and stretches.", "Hops, guiños y estiramientos.", "Hüpft, zwinkert und streckt sich.");
            reg("Dances and twirls", "Bailes y piruetas", "Tänze und Drehungen");
            reg("Dancing, spinning, flips, and bubbles.", "Bailar, girar, voltereta y pompas.", "Tanzen, Drehen, Saltos und Seifenblasen.");
            reg("Props and activities", "Juegos con objetos", "Requisiten und Spiele");
            reg("Juggling, coffee, gaming, reading, and photos.", "Malabares, café, consola, lectura y fotos.", "Jonglieren, Kaffee, Konsole, Lesen und Fotos.");
            reg("Waves and glances", "Saludos y miradas", "Winken und Umsehen");
            reg("Waves, looks around, and bows.", "Saluda, mira alrededor y hace reverencias.", "Winkt, sieht sich um und verbeugt sich.");

            // Healthy breaks, in the mascot settings
            reg("Healthy breaks", "Pausas saludables", "Gesunde Pausen");
            reg("Remind me to take breaks", "Recordarme hacer pausas", "An Pausen erinnern");
            reg("Reminds you to drink water, rest eyes, stretch, and breathe — and joins in with you.",
                "Te avisa para beber agua, descansar la vista, moverte y respirar, y las hace contigo.",
                "Es erinnert dich ans Trinken, an Augenpausen, Bewegung und Atemübungen — und macht alles mit dir.");
            reg("Drink water", "Beber agua", "Wasser trinken");
            reg("Drinks a glass of water with you.", "Se toma un vaso contigo.", "Trinkt ein Glas Wasser mit dir.");
            reg("Rest eyes", "Descansar la vista", "Augen ausruhen");
            reg("20-20-20 rule: look at something 20 feet away for 20 seconds.", "Regla 20-20-20: mirar algo a 6 metros durante 20 segundos.", "20-20-20-Regel: 20 Sekunden auf etwas in 6 Metern Entfernung schauen.");
            reg("Movement break", "Moverse", "Bewegungspause");
            reg("Squats or back stretches, alternating each time.", "Sentadillas o estirar la espalda, una vez cada cosa.", "Kniebeugen oder Rücken dehnen, im Wechsel.");
            reg("Breathing session", "Respirar", "Atemübung");
            reg("One minute of deep breathing: 4 seconds in, 6 seconds out.", "Un minuto de respiración lenta: 4 segundos dentro y 6 fuera.", "Eine Minute ruhiges Atmen: 4 Sekunden ein-, 6 Sekunden ausatmen.");
            reg("Notify with system toasts", "Avisar con notificaciones", "Mit Systembenachrichtigungen erinnern");
            reg("When mascot is not on desktop, reminders appear as Stackshot notifications.", "Cuando la mascota no está en el escritorio, el aviso llega como una notificación de Stackshot.", "Ist das Maskottchen nicht auf dem Desktop, erscheint die Erinnerung als Stackshot-Benachrichtigung.");
            reg("Only counts active time at the PC: if you step away for 5 minutes, your eyes and body have rested and timer resets. " +
                "Never interrupts during calls, presentations, recordings, full screen, or Windows Focus Assist: waits until you finish. " +
                "Take a break anytime from the desktop mascot menu (\u201CTake a break with me\u201D).",
                "Solo cuenta el tiempo que estás delante del ordenador: si te ausentas 5 minutos, la vista y el cuerpo ya han descansado y empieza de cero. " +
                "Nunca te interrumpe en una llamada, presentando, grabando, a pantalla completa ni en el tiempo de silencio de Windows: espera a que termines. " +
                "Puedes hacer una pausa cuando quieras desde su menú en el escritorio («Una pausa conmigo».",
                "Zählt nur die Zeit, in der du am Rechner bist: Gehst du 5 Minuten weg, haben Augen und Körper sich ausgeruht und die Zählung beginnt von vorn. " +
                "Unterbricht nie während eines Anrufs, einer Präsentation, einer Aufnahme, im Vollbild oder während des Fokus von Windows: Es wartet, bis du fertig bist. " +
                "Eine Pause machst du jederzeit über das Menü des Maskottchens auf dem Desktop („Mach eine Pause mit mir“).");
            reg("Water", "Agua", "Wasser");
            reg("Breaks", "Pausas", "Pausen");

            // Focus mode, in the mascot settings
            reg("Focus mode", "Modo concentración", "Fokusmodus");
            reg("Focus in progress", "Concentración en marcha", "Fokus läuft");
            reg("Focus now", "Concentrarme ahora", "Jetzt fokussieren");
            reg("Work intervals with scheduled breaks in between (Pomodoro technique).", "Bloques de trabajo con descansos entre medias (la técnica Pomodoro).", "Arbeitsblöcke mit geplanten Pausen dazwischen (Pomodoro-Technik).");
            reg("Start", "Empezar", "Start");
            reg("Work interval", "Cada bloque", "Arbeitsblock");
            reg("Length of each focused work period.", "El tiempo de trabajo concentrado.", "Dauer eines konzentrierten Arbeitsblocks.");
            reg("Short break", "Descanso corto", "Kurze Pause");
            reg("Break between work intervals.", "Entre un bloque y el siguiente.", "Pause zwischen zwei Arbeitsblöcken.");
            reg("Long break", "Descanso largo", "Lange Pause");
            reg("Break after completing a full set of intervals.", "Al completar una ronda de bloques.", "Pause nach einer kompletten Runde Arbeitsblöcke.");
            reg("Intervals per set", "Bloques por ronda", "Blöcke pro Runde");
            reg("Number of focus intervals before a long break.", "Cuántos bloques hay antes del descanso largo.", "Wie viele Arbeitsblöcke es vor der langen Pause gibt.");
            reg("Auto-start breaks & intervals", "Seguir solo", "Automatisch weiter");
            reg("Automatically transition between focus intervals and breaks without asking.", "Al acabar un bloque empieza el descanso, y al acabar el descanso el siguiente bloque, sin preguntarte.", "Nach einem Block beginnt die Pause, nach der Pause der nächste Block — ganz ohne Nachfragen.");
            reg("Chimes", "Sonido", "Klangsignal");
            reg("Gentle chimes for starting, pausing, resuming, and completing breaks and intervals.", "Un sonido suave y distinto para cada momento: empezar, pausar, reanudar, acabar, descansar y terminar.", "Ein leiser, eigener Klang für jeden Moment: Start, Pause, Weiter und Ende von Block und Pause.");
            reg("When mascot is not on desktop, show Windows notifications for each event. On desktop, mascot speaks.",
                "Cuando la mascota no está en el escritorio, junto al reloj de Windows: al empezar, pausar, reanudar y terminar cada bloque y cada descanso. Con la mascota, te lo dice ella.",
                "Ist das Maskottchen nicht auf dem Desktop, zeigt Windows Benachrichtigungen neben der Uhr: für Start, Pause, Weiter und Ende jedes Blocks und jeder Pause. Mit dem Maskottchen sagt es es dir selbst.");
            reg("Start from here or from the mascot desktop menu (\u201CFocus\u201D). During focus, mascot quietly reads by your side without speaking, " +
                "wearing a timer on its head. Healthy breaks wait until rest periods. If you step away for 5+ minutes, timer pauses. " +
                "Without the mascot on desktop, runs quietly and notifies via system toasts.",
                "Empieza desde aquí o desde el menú de la mascota en el escritorio («Concentrarme»). Mientras dura un bloque, la mascota lee a tu lado sin hablar, " +
                "lleva el reloj encima de la cabeza y las pausas saludables esperan al descanso, donde te propone la que toque. Si te levantas más de 5 minutos, " +
                "el reloj se para y al volver te pregunta si sigues. Sin la mascota en el escritorio, todo sigue solo y te avisa con notificaciones.",
                "Starte hier oder über das Menü des Maskottchens auf dem Desktop („Fokus“). Während eines Blocks liest es still an deiner Seite mit, " +
                "trägt die Uhr auf dem Kopf, und die gesunden Pausen warten auf die Pause, wo es dir die passende vorschlägt. Gehst du länger als 5 Minuten weg, " +
                "pausiert die Uhr, und beim Zurückkommen fragt es, ob du weitermachst. Ohne Maskottchen auf dem Desktop läuft alles von allein und meldet sich über Systembenachrichtigungen.");

            // Health and activity, in the mascot settings
            reg("Health and activity", "Salud y actividad", "Gesundheit und Aktivität");
            reg("Daily water goal", "Objetivo de agua", "Tagesziel Wasser");
            reg("Glasses per day. The ones you drink with the mascot and \u201C+1 glass\u201D in its menu count. Today: {0} {1}.",
                "Vasos al día. Cuentan los que bebes con la mascota y «+1 vaso» en su menú. Hoy: {0} {1}.",
                "Gläser pro Tag. Mitgezählt werden die, die du mit dem Maskottchen trinkst, und „+1 Glas“ in seinem Menü. Heute: {0} {1}.");
            reg("glass", "vaso", "Glas");
            reg("glasses", "vasos", "Gläser");
            reg("No goal", "Sin objetivo", "Kein Ziel");
            reg("6 glasses", "6 vasos", "6 Gläser");
            reg("8 glasses", "8 vasos", "8 Gläser");
            reg("10 glasses", "10 vasos", "10 Gläser");
            reg("Daily summary", "Resumen del día", "Tagesrückblick");
            reg("Recaps your day at this time: water, breaks, focus, captures, and steps.", "A esa hora te cuenta cómo ha ido: agua, pausas, concentración, capturas y pasos.", "Zu dieser Zeit erzählt es dir, wie der Tag war: Wasser, Pausen, Fokus, Aufnahmen und Schritte.");
            reg("Off", "No", "Aus");
            reg("Custom", "Otra", "Eigene");
            reg("Custom summary time", "Tu hora para el resumen", "Eigene Uhrzeit für den Rückblick");
            reg("15-minute increments between 8:00 and 23:45.", "De 15 en 15 minutos, entre las 8:00 y las 23:45.", "In 15-Minuten-Schritten, zwischen 8:00 und 23:45 Uhr.");
            reg("Summary contents", "Qué incluye el resumen", "Was in den Rückblick kommt");
            reg("Click to toggle. Also applies to \u201CHow's your day going?\u201D in the mascot's menu.", "Toca para quitar o poner. Vale también para «¿Qué tal el día?» de su menú.", "Zum An- und Abwählen anklicken. Gilt auch für „Wie läuft dein Tag?“ im Menü des Maskottchens.");
            reg("Focus", "Concentración", "Fokus");
            reg("Captures", "Capturas", "Aufnahmen");
            reg("Steps", "Pasos", "Schritte");
            reg("Pixel Watch or Fitbit", "Pixel Watch o Fitbit", "Pixel Watch oder Fitbit");
            reg("Connecting\u2026 Finish signing in on the browser tab and return here.", "Conectando\u2026 Termina de iniciar sesión en la pestaña que se ha abierto en tu navegador y vuelve aquí.", "Verbindung wird hergestellt … Schließe die Anmeldung im Browser-Tab ab und komm hierher zurück.");
            reg("Connected with your Google account.", "Conectado con tu cuenta de Google.", "Mit deinem Google-Konto verbunden.");
            reg("Sync steps, sleep, and active minutes from your smartwatch. One-time browser login.", "Pasos, sueño y minutos activos de tu reloj, para que la mascota te acompañe mejor. Inicias sesión en el navegador, una vez.", "Schritte, Schlaf und aktive Minuten von deiner Smartwatch, damit das Maskottchen dich besser begleitet. Einmalige Anmeldung im Browser.");
            reg("Connect", "Conectar", "Verbinden");
            reg("Disconnect", "Desconectar", "Trennen");
            reg("Daily step goal", "Objetivo de pasos", "Tagesziel Schritte");
            reg("Used for the status bar, mascot speech, and daily summary.", "Lo que cuenta la barra, la mascota y el resumen del día.", "Für die Leiste, die Sprache des Maskottchens und den Tagesrückblick.");
            reg("6,000", "6.000", "6.000");
            reg("8,000", "8.000", "8.000");
            reg("10,000", "10.000", "10.000");
            reg("12,000", "12.000", "12.000");
            reg("15,000", "15.000", "15.000");
            reg("Activity comments", "Comentarios sobre tu actividad", "Kommentare zu deiner Aktivität");
            reg("Gentle reminders to move after an hour of sitting, celebrates walks, active minutes, and step goals, notes sleep duration. Rare, unobtrusive daily speech.",
                "Te propone levantarte si llevas una hora quieto o una mañana floja, un paseo después de comer, celebra tus paseos, minutos activos y objetivo de pasos, te dice cómo vas y cuánto te falta, y cómo has dormido. Cada cosa, pocas veces al día.",
                "Schlägt vor aufzustehen, wenn du eine Stunde stillgesessen hast oder der Morgen träge war, schlägt einen Spaziergang nach dem Mittagessen vor, feiert deine Spaziergänge, aktiven Minuten und dein Schrittziel, erzählt, wie es läuft und wie viel noch fehlt, und wie du geschlafen hast. Jedes davon selten am Tag.");
            reg("Everything stays local. With smartwatch connected, data travels directly between Google and your PC; " +
                "credentials are encrypted with Windows DPAPI and you can disconnect anytime. Mascot offers encouragement, not medical advice. " +
                "If you sleep under 6 hours, break intervals adapt slightly earlier; if you recently walked, movement pauses wait; " +
                "once daily water goal is reached, reminders stop.",
                "Todo se queda en tu equipo. Con el reloj conectado, los datos van de Google a tu ordenador y no pasan por ningún otro sitio; " +
                "las claves se guardan cifradas con tu usuario de Windows y puedes desconectarlo cuando quieras. La mascota no da consejos médicos: " +
                "solo te anima a moverte, beber y descansar. Si una noche duermes menos de 6 horas, ese día las pausas para la vista y para moverte llegan un poco antes; " +
                "si el reloj ve que acabas de caminar, la pausa para moverte espera; " +
                "y con el objetivo de agua cumplido, deja de recordártelo.",
                "Alles bleibt auf deinem Rechner. Mit verbundener Uhr laufen die Daten direkt zwischen Google und deinem PC hin und her; " +
                "die Schlüssel werden verschlüsselt mit deinem Windows-Konto gespeichert, und du kannst die Verbindung jederzeit trennen. Das Maskottchen gibt keine medizinischen Ratschläge: " +
                "es ermutigt dich nur, dich zu bewegen, zu trinken und dich auszuruhen. Hast du in einer Nacht weniger als 6 Stunden geschlafen, kommen die Augen- und Bewegungspausen an dem Tag etwas früher; " +
                "hat die Uhr gesehen, dass du gerade gegangen bist, wartet die Bewegungspause; " +
                "und ist das Wasserziel erreicht, erinnert es dich nicht mehr daran.");

            // The assistant, in the mascot settings
            reg("Assistant", "Ayudante", "Assistent");
            reg("Reminders", "Recordatorios", "Erinnerungen");
            reg("\u201CRemind me of something\u2026\u201D in the mascot's menu. ", "«Recuérdame algo…» en el menú de la mascota. ", "„Erinnere mich an etwas …“ im Menü des Maskottchens. ");
            reg("None pending.", "Ninguno pendiente.", "Keine offen.");
            reg("{0} pendiente", "{0} pendiente", "{0} offen");
            reg("{0} pendientes", "{0} pendientes", "{0} offen");
            reg("; next {0}: \u201C{1}\u201D.", "; el próximo {0}: «{1}».", "; das nächste {0}: „{1}“.");
            reg("New\u2026", "Nuevo\u2026", "Neu …");
            reg("Notes", "Notas", "Notizen");
            reg("\u201CNote something\u2026\u201D in its menu, or drop text onto the mascot. Saved to Notas.md in captures folder; check off or delete items there.",
                "«Apunta algo…» en su menú, o suelta un texto encima de ella. Van a Notas.md, en tu carpeta de capturas; en esa misma ventana marcas las hechas o las borras.",
                "„Notiz schreiben …“ in seinem Menü, oder zieh einen Text auf das Maskottchen. Sie landen in Notas.md in deinem Aufnahmeordner; im selben Fenster hakst du sie ab oder löschst sie.");
            reg("Quick links", "Accesos rápidos", "Schnelllinks");
            reg("Edit\u2026", "Editar\u2026", "Bearbeiten …");
            reg("Notify on failed XML files", "Avisarme de los XML con fallo", "Bei fehlgeschlagenen XML-Dateien benachrichtigen");
            reg("Alerts with an open button when an XML with a SOAP Fault arrives in the monitored folder.", "Cuando llega a la carpeta vigilada un XML con SOAP Fault, te lo dice con un botón para abrirlo.", "Kommt eine XML-Datei mit SOAP-Fault im überwachten Ordner an, meldet es sich mit einem Button zum Öffnen.");
            reg("You can also drag & drop files onto the mascot: images go to editor, videos to player, XML/Markdown to viewer, text to notes, folders to quick links. Reminders wait until you are active, and overdue reminders alert upon wake.",
                "También puedes soltar encima de la mascota una imagen (va al editor), un vídeo (al reproductor), un XML o un Markdown (al visor), " +
                "un texto (a tus notas) o una carpeta (a tus accesos rápidos). Los recordatorios esperan a que estés delante, y si el ordenador estaba apagado a su hora, te avisa al volver.",
                "Du kannst dem Maskottchen auch ein Bild (geht in den Editor), ein Video (in den Player), ein XML oder Markdown (in die Ansicht), " +
                "einen Text (in deine Notizen) oder einen Ordner (in deine Schnelllinks) aufgeben. Erinnerungen warten, bis du wieder am Rechner bist; war er zu ihrer Zeit aus, bekommst du sie beim Wiederkehren.");
            reg("Websites, folders, files, or apps opened from mascot menu. You can also drop a folder on it.", "Webs, carpetas, archivos o programas que la mascota te abre desde su menú. También puedes soltarle una carpeta encima.", "Webseiten, Ordner, Dateien oder Programme, die das Maskottchen aus seinem Menü für dich öffnet. Du kannst ihm auch einen Ordner daraufziehen.");
            reg("\u201C{0}\u201D", "«{0}»", "„{0}“");
            reg("\u201C{0}\u201D and \u201C{1}\u201D", "«{0}» y «{1}»", "„{0}“ und „{1}“");
            reg("\u201C{0}\u201D, \u201C{1}\u201D and {2} more", "«{0}», «{1}» y {2} más", "„{0}“, „{1}“ und {2} weitere");
            reg("{0} in the mascot's menu.", "{0} en el menú de la mascota.", "{0} im Menü des Maskottchens.");

            // Time and chips rows in the mascot settings
            reg("15 minutes earlier", "15 minutos antes", "15 Minuten früher");
            reg("15 minutes later", "15 minutos después", "15 Minuten später");
            reg("included", "incluido", "enthalten");
            reg("not included", "no incluido", "nicht enthalten");
            reg("{0} min", "{0} min", "{0} Min.");

            // The mascot page: stage, name, personality and friendship
            reg("Hidden", "Escondido", "Versteckt");
            reg("Resting until you call it.", "Descansa hasta que lo llames.", "Ruhet sich aus, bis du es rufst.");
            reg("Show the mascot", "Mostrar la mascota", "Maskottchen anzeigen");
            reg("Friendship: ", "Amistad: ", "Freundschaft: ");
            reg("level ", "nivel ", "Stufe ");
            reg("Change its personality", "Cambiar su forma de ser", "Seinen Charakter ändern");
            reg("Lively and always eager to capture", "Animado y siempre con ganas de capturar", "Lebhaft und immer bereit für eine Aufnahme");
            reg("Calm: speaks little and takes a nap first", "Calmado: habla poco y se echa la siesta antes", "Ruhig: spricht wenig und macht erst ein Nickerchen");
            reg("Playful, active, and a bit cheeky", "Bromista, movido y un poco chulito", "Scherzhaft, rege und etwas frech");
            reg("Friendship  \u00B7  ", "Amistad  \u00B7  ", "Freundschaft  \u00B7  ");
            reg("{0} capture", "{0} captura", "{0} Aufnahme");
            reg("{0} captures", "{0} capturas", "{0} Aufnahmen");
            reg("At {0} captures", "A las {0} capturas", "Ab {0} Aufnahmen");
            reg("Where it all begins", "Donde empieza todo", "Wo alles beginnt");
            reg("Every capture counts", "Cada captura suma", "Jede Aufnahme zählt");
            reg("{0} capture together", "{0} captura juntos", "{0} Aufnahme gemeinsam");
            reg("{0} captures together", "{0} capturas juntos", "{0} Aufnahmen gemeinsam");
            reg("  \u00B7  {0} to go for \u201C{1}\u201D", "  \u00B7  faltan {0} para «{1}»", "  \u00B7  noch {0} bis „{1}“");
            reg("  \u00B7  max level", "  \u00B7  nivel máximo", "  \u00B7  höchste Stufe");
            reg("{0} color", "color {0}", "Farbe {0}");
            reg("Unlocks {0}", "Desbloquea {0}", "Schaltet frei: {0}");

            // The wardrobe dock and its tabs
            reg("My Looks", "Mis looks", "Meine Looks");
            reg("Characters", "Personajes", "Figuren");
            reg("Body", "Cuerpo", "Körper");
            reg("Hats", "Gorros", "Hüte");
            reg("Outfits", "Ropa", "Kleidung");
            reg("Face", "Cara", "Gesicht");
            reg("Extras", "Extras", "Extras");
            reg("Actions", "Acciones", "Aktionen");
            reg("Styles", "Estilos", "Stile");
            reg("Favorites", "Favoritos", "Favoriten");
            reg("Originals", "Originales", "Originale");
            reg("Character", "Personaje", "Figur");
            reg("Style", "Estilo", "Stil");
            reg("Species", "Especie", "Art");
            reg("Color", "Color", "Farbe");
            reg("Hat", "Gorro", "Hut");
            reg("Outfit", "Ropa", "Kleidung");
            reg("Accessory", "Accesorio", "Accessoire");
            reg("Eyes", "Ojos", "Augen");
            reg("Extra", "Extra", "Extra");
            reg("Save look", "Guardar look", "Look speichern");
            reg("Save current outfit", "Guardar lo que lleva puesto", "Das aktuelle Outfit speichern");
            reg("Save the combination it is wearing and star what you like most: everything will appear here.", "Guarda la combinación que lleva puesta y marca con la estrella lo que más te guste: todo aparecerá aquí.", "Speichere die Kombination, die es trägt, und markiere mit dem Stern, was dir am besten gefällt: Alles erscheint hier.");
            reg("Hover over any item and click its star: it will appear here and at the top of its list.", "Pasa el ratón por cualquier prenda y pulsa su estrella: aparecerá aquí y la primera de su lista.", "Fahre über ein Teil und klicke auf seinen Stern: Es erscheint hier — ganz vorne in seiner Liste.");
            reg("Clear search", "Borrar la búsqueda", "Suche löschen");
            reg("Nothing with \u201C{0}\u201D in {1}", "Nada con «{0}» en {1}", "Nichts mit „{0}“ in {1}");
            reg("See {0} in {1}", "Ver {0} en {1}", "{0} in {1} ansehen");
            reg("Try another word", "Prueba con otra palabra", "Versuch ein anderes Wort");
            reg("Eyes: ", "Ojos: ", "Augen: ");
            reg("  \u00B7  unlocked at \u201C{0}\u201D ({1} captures)", "  \u00B7  llega con «{0}» ({1} capturas)", "  \u00B7  ab „{0}“ ({1} Aufnahmen)");
            reg("You already had it: moved to first.", "Ya lo tenías: lo pongo el primero.", "Hattest du schon: ab damit an den Anfang.");
            reg("Saved to My Looks!", "¡Guardado en Mis looks!", "In Meine Looks gespeichert!");
            reg("Ta-da! What do you think?", "¡Tachán! ¿Qué te parece?", "Tadaa! Was hältst du davon?");
            reg("Not yet! You need 1 more capture for that.", "¡Aún no! Te falta 1 captura para eso.", "Noch nicht! Dazu fehlt dir noch eine Aufnahme.");
            reg("Not yet! You need {0} more captures for that.", "¡Aún no! Te faltan {0} capturas para eso.", "Noch nicht! Dazu fehlen dir noch {0} Aufnahmen.");
            reg("I love this look!", "¡Me encanta este look!", "Ich liebe diesen Look!");
            reg("Today I'm {0}!", "¡Hoy soy {0}!", "Heute bin ich {0}!");
            reg("{0} mode on!", "¡Modo {0} activado!", "{0}-Modus an!");
            reg("How do I look like this?", "¿Qué tal me ves así?", "Wie sehe ich damit aus?");
            reg("{0}! How does it look on me?", "¡{0}! ¿Qué tal me queda?", "{0}! Wie sieht das an mir aus?");
            reg("Saved look", "Look guardado", "Gespeicherter Look");
            reg("Wear it", "Ponérmelo", "Anziehen");
            reg("Remove from My looks", "Quitar de Mis looks", "Aus Meine Looks entfernen");
            reg("Remove from favorites", "Quitar de favoritos", "Aus Favoriten entfernen");
            reg("Add to favorites", "Añadir a favoritos", "Zu Favoriten hinzufügen");
            reg("Surprise me", "Sorpréndeme", "Überrasch mich");
            reg("A random look with what you have unlocked", "Un look al azar con lo que tenéis desbloqueado", "Ein zufälliger Look mit dem, was ihr freigeschaltet habt");
            reg("Greetings, seasonal costume, desktop\u2026", "Saludos, disfraz de temporada, escritorio\u2026", "Begrüßungen, Saisonkostüm, Desktop …");
            reg("Strolls by the taskbar while you work", "Pasea junto a la barra de tareas mientras trabajas", "Schlendert bei der Arbeit an der Taskleiste entlang");
            reg("Only with Full profile (in General \u203A Profile)", "Solo con el perfil Completo (en General \u203A Perfil)", "Nur mit dem vollständigen Profil unter „Allgemein \u203A Profil“");
            reg("Change name", "Cambiar el nombre", "Namen ändern");
            reg("On desktop", "En el escritorio", "Auf dem Desktop");

            // Accessibility of the mascot page
            reg("Mascot name", "Nombre de la mascota", "Name des Maskottchens");
            reg("Categories", "Categorías", "Kategorien");

            // The desktop mascot: greetings and menus
            reg("So dizzy! Why are there three mice?", "¡Qué mareo! ¿Por qué hay tres ratones?", "Mir ist ganz schwindelig! Warum gibt es drei Mäuse?");
            reg(" Click me.", " Haz clic en mí.", " Klick mich.");
            reg("Off through the vent\u2026 don't let anyone see me!", "Me voy por la alcantarilla… ¡que nadie me vea!", "Ich verschwinde durch den Gully … dass mich keiner sieht!");
            reg("Heading home! See you later!", "¡Me voy a casa! ¡Hasta luego!", "Ich geh nach Hause! Bis später!");
            reg(" \u201C{0}\u201D", " «{0}»", " „{0}“");

            // The desktop mascot: healthy breaks and their reminders
            reg("I'll drink a glass with you", "Bebo un vaso contigo", "Ich trinke ein Glas mit dir");
            reg("Start (20 s)", "Empezar (20 s)", "Starten (20 s)");
            reg("Let's stretch together", "Estirarnos juntos", "Wir strecken uns zusammen");
            reg("Do 8 squats", "Hacer 8 sentadillas", "8 Kniebeugen machen");
            reg("Breathe for 1 minute", "Respirar 1 minuto", "1 Minute atmen");
            reg("Already done", "Ya lo he hecho", "Habe ich schon");
            reg("Remind me in 10 min", "Recuérdamelo en 10 min", "Erinnere mich in 10 Min.");
            reg("Not today", "Hoy no", "Heute nicht");
            reg("Drink water with me", "Bebe agua conmigo", "Trink Wasser mit mir");
            reg("Rest your eyes (20 s)", "Descansa la vista (20 s)", "Augen ausruhen (20 s)");
            reg("Stretch your back", "Estira la espalda", "Rücken strecken");
            reg("Stretch your legs", "Estira las piernas", "Beine strecken");
            reg("Breathe with me (1 min)", "Respira conmigo (1 min)", "Atme mit mir (1 Min.)");
            reg("Adjust reminders", "Ajustar los avisos", "Erinnerungen anpassen");
            reg("A glass of water! Drink with me.", "¡Un vaso de agua! Bebe conmigo.", "Ein Glas Wasser! Trink mit mir.");
            reg("Sip by sip\u2026", "Sorbo a sorbo…", "Schluck für Schluck …");
            reg("Look at something far away, over 20 feet\u2026 {0}", "Mira algo lejano, a más de 6 metros… {0}", "Schau etwas in der Ferne an, über 6 Meter entfernt … {0}");
            reg("Done!", "¡Listo!", "Fertig!");
            reg("Arms up and stretch your back\u2026 {0} of 3", "Brazos arriba y estira la espalda… {0} de 3", "Arme hoch und streck den Rücken … {0} von 3");
            reg("Squat {0} of 8", "Sentadilla {0} de 8", "Kniebeuge {0} von 8");
            reg("Breathe in slowly through your nose\u2026 {0} of 6", "Inspira despacio por la nariz… {0} de 6", "Atme langsam durch die Nase ein … {0} von 6");
            reg("Let the air out little by little\u2026 {0} of 6", "Suelta el aire poco a poco… {0} de 6", "Lass die Luft langsam heraus … {0} von 6");
            reg("Let's leave it for 10 minutes.", "Lo dejamos para dentro de 10 minutos.", "Machen wir in 10 Minuten.");

            // The desktop mascot: news after an update
            reg("Stackshot {0} is here! Want to hear what's new?", "¡Estrenamos Stackshot {0}! ¿Te cuento qué trae?", "Stackshot {0} ist da! Soll ich dir erzählen, was neu ist?");
            reg(" For example: \u201C{0}\u201D.", " Por ejemplo: «{0}».", " Zum Beispiel: „{0}“.");
            reg("See what's new", "Ver las novedades", "Neuerungen ansehen");
            reg("Later", "Luego", "Später");
            reg("Whenever you like: Home, About, What's new.", "Cuando quieras: Inicio, Acerca de, Novedades.", "Wann immer du magst: Startseite, Über, Neuerungen.");

            // The desktop mascot: focus mode
            reg("Block {0} of {1} done!", "¡Bloque {0} de {1} hecho!", "Block {0} von {1} geschafft!");
            reg("Break's over. Shall we go on with block {0} of {1}?", "Fin del descanso. ¿Seguimos con el bloque {0} de {1}?", "Pause vorbei. Weiter mit Block {0} von {1}?");
            reg("{0} long break", "Descanso largo de {0}", "Lange Pause von {0} Min.");
            reg("{0} break", "Descanso de {0}", "Pause von {0} Min.");
            reg("{0} with me", "{0} conmigo", "{0} mit mir");
            reg("Another block, no break", "Otro bloque sin descanso", "Noch ein Block ohne Pause");
            reg("Done for today", "Terminar por hoy", "Für heute fertig");
            reg("Start the block", "Empezar el bloque", "Block starten");
            reg("5 more minutes of break", "5 minutos más de descanso", "5 Minuten mehr Pause");
            reg("Pick up where I left off", "Seguir donde lo dejé", "Weiter, wo ich aufgehört habe");
            reg("Go on with the break", "Seguir con el descanso", "Weiter mit der Pause");
            reg("Restart the block", "Empezar el bloque de nuevo", "Block neu starten");
            reg("End focus", "Terminar la concentración", "Fokus beenden");
            reg("Skip the break", "Saltar el descanso", "Pause überspringen");
            reg("Block done! Tap me", "¡Bloque hecho! Tócame", "Block geschafft! Tippe mich");
            reg("Shall we go on? Tap me", "¿Seguimos? Tócame", "Weiter? Tippe mich");
            reg("Paused", "En pausa", "Pausiert");
            reg("Break", "Descanso", "Pause");

            // The desktop mascot: health and the watch
            reg("Thanks", "Gracias", "Danke");
            reg("Let's stretch our legs", "Estiramos las piernas", "Wir strecken die Beine");
            reg("OK, we'll move later.", "Vale, luego nos movemos.", "OK, wir bewegen uns später.");
            reg("I'll go for a walk", "Voy a dar un paseo", "Ich geh spazieren");
            reg("Great! When you're back I'll tell you how many steps you took.", "¡Genial! Cuando vuelvas te digo cuántos pasos has dado.", "Super! Wenn du zurück bist, sage ich dir, wie viele Schritte du gemacht hast.");
            reg("OK. If you feel like it later, I'm here.", "Vale. Si luego te apetece, aquí estoy.", "OK. Wenn du später Lust hast, bin ich hier.");
            reg("Nothing to tell about today yet. Let's go!", "Todavía no ha pasado nada que contar hoy. ¡Vamos allá!", "Heute gab es noch nichts zu erzählen. Los geht's!");
            reg("Checking your watch\u2026", "Miro tu reloj…", "Ich schaue auf deine Uhr …");
            reg(" today", " hoy", " heute");
            reg(" de ", " de ", " von ");

            // The desktop mascot: reminders, notes and SOAP faults
            reg("Reminder: ", "Recordatorio: ", "Erinnerung: ");
            reg("OK, in 10 minutes.", "Vale, en 10 minutos.", "OK, in 10 Minuten.");
            reg("In 1 hour", "En 1 hora", "In 1 Stunde");
            reg("OK, in an hour.", "Vale, dentro de una hora.", "OK, in einer Stunde.");
            reg("Open the XML", "Abrir el XML", "XML öffnen");
            reg("OK", "Vale", "OK");
            reg("Drop it and I'll save it in my shortcuts!", "¡Suéltala y la guardo en mis accesos rápidos!", "Lass sie fallen und ich speichere sie in meinen Schnelllinks!");
            reg("Drop it here and I'll open it!", "¡Suéltalo aquí y te lo abro!", "Lass es hier fallen und ich öffne es!");
            reg("Drop it and I'll add it to your notes!", "¡Suéltalo y lo apunto en tus notas!", "Lass es fallen und ich schreibe es in deine Notizen!");
            reg("Added to your notes.", "Apuntado en tus notas.", "In deine Notizen eingetragen.");
            reg("Connect my watch", "Conectar mi reloj", "Meine Uhr verbinden");
            reg("OK, I'll ask you again another day.", "Vale, otro día te lo vuelvo a preguntar.", "OK, das frage ich dich an einem anderen Tag noch einmal.");
            reg("I don't have a watch", "No tengo reloj", "Ich habe keine Uhr");
            reg("Got it! I'll keep you company without a watch too.", "¡Entendido! Sin reloj también te acompaño.", "Verstanden! Auch ohne Uhr bin ich für dich da.");
        }
    }
}
