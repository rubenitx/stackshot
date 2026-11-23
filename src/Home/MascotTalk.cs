// Stackshot - What the mascot says, by personality.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;

namespace Stackshot
{
    // Lines are picked at random but never repeat one said recently, so it does not sound like a loop.
    public static class MascotTalk
    {
        static readonly Random rnd = new Random();
        static readonly List<string> recent = new List<string>();

        // The label that opens the mascot's menu: "Jugar con Pixel". Korean says it with \uD558\uACE0, which needs no particle
        // chosen by how the name ends, so every name reads the same way, in any script.
        public static string PlayWith(string name)
        {
            return I18n.Fmt("Jugar con {0}", "Play with {0}", name);
        }

        static string Pick(string[] lines)
        {
            string line = null;
            for (int i = 0; i < 10; i++)
            {
                line = lines[rnd.Next(lines.Length)];
                if (!recent.Contains(line)) break;
            }
            recent.Add(line);
            if (recent.Count > 24) recent.RemoveAt(0);
            return I18n.Localize(line);
        }

        // Three voices: 0 cheerful, 1 calm, 2 cheeky.
        static string By(int personality, string[] cheerful, string[] calm, string[] cheeky)
        {
            return Pick(personality == 1 ? calm : personality == 2 ? cheeky : cheerful);
        }

        public static string Greeting()
        {
            int h = DateTime.Now.Hour;
            if (I18n.IsSpanish)
                return h < 6 ? "\u00A1Buenas noches!" : h < 14 ? "\u00A1Buenos d\u00EDas!" : h < 21 ? "\u00A1Buenas tardes!" : "\u00A1Buenas noches!";
            if (I18n.IsGerman)
                return h < 6 ? "Gute Nacht!" : h < 11 ? "Guten Morgen!" : h < 18 ? "Guten Tag!" : "Guten Abend!";
            return I18n.Localize(h < 6 ? "Good evening!" : h < 12 ? "Good morning!" : h < 18 ? "Good afternoon!" : "Good evening!");
        }

        public static string Hello(Settings s)
        {
            string g = Greeting();
            switch (s.MascotPersonality)
            {
                case 1: return g + Pick(new string[] { I18n.T(" Aqu\u00ED estoy, tranquilo.", " Here I am, nice and calm.", " Hier bin ich, ganz entspannt."),
                                                        I18n.T(" Sin prisa, ya sabes d\u00F3nde encontrarme.", " No rush, you know where to find me.", " Keine Eile, du wei\u00DFt ja, wo du mich findest."),
                                                        I18n.T(" Todo en calma por aqu\u00ED.", " All quiet over here.", " Alles ruhig hier bei mir.") });
                case 2: return g + Pick(new string[] { I18n.T(" \u00BFA qui\u00E9n capturamos hoy?", " Who are we capturing today?", " Wen nehmen wir heute auf?"),
                                                        I18n.T(" Ya era hora de que aparecieras.", " About time you showed up.", " Aber hallo, du bist ja endlich da."),
                                                        I18n.T(" \u00BFQu\u00E9 liamos hoy, jefe?", " What's the plan today, boss?", " Was steht heute an, Chef?") });
                default: return g + Pick(new string[] { I18n.T(" \u00BFQu\u00E9 capturamos?", " What are we capturing?", " Was nehmen wir auf?"),
                                                        I18n.T(" \u00BFListo para una buena captura?", " Ready for a great shot?", " Bereit f\u00FCr eine gute Aufnahme?"),
                                                        I18n.T(" \u00A1Qu\u00E9 ganas ten\u00EDa de verte!", " So great to see you!", " Ich habe mich so auf dich gefreut!") });
            }
        }

        // Sometimes one of the character's own lines (null otherwise, or when it has none).
        static string Own(Settings s, double chance)
        {
            string[] lines = MascotParts.OwnLines(MascotParts.KindLatest(s.MascotKind));   // a body redrawn since speaks as the new one
            return lines != null && rnd.NextDouble() < chance ? Pick(lines) : null;
        }

        public static string[] Pokes(Settings s)
        {
            string own = Own(s, 0.5);
            if (own != null) return new string[] { own };
            string key = Hotkeys.Display(s.HotRegion);
            switch (s.MascotPersonality)
            {
                case 1:
                    return new string[] { Pick(new string[] { I18n.T("Mmm\u2026 hola.", "Mmm\u2026 hello.", "Mmm\u2026 hallo."),
                                                              I18n.T("Con calma, que hay tiempo.", "Take it easy, there's time.", "Immer mit der Ruhe, wir haben Zeit."),
                                                              I18n.T("Respira. Captura. Repite.", "Breathe. Capture. Repeat.", "Atmen. Aufnehmen. Wiederholen."),
                                                              string.Format(I18n.T("Pulsa {0} cuando quieras.", "Press {0} whenever you want.", "Dr\u00FCck {0}, wann immer du magst."), key),
                                                              I18n.T("Un toquecito, qu\u00E9 detalle.", "A little poke, how nice.", "Ein kleines Stupsen, wie nett."),
                                                              I18n.T("Aqu\u00ED sigo, sin prisa.", "Still here, no rush.", "Ich bin noch da, ganz ohne Eile."),
                                                              I18n.T("Todo a su ritmo.", "Everything at its own pace.", "Alles in seinem Tempo."),
                                                              I18n.T("Qu\u00E9 bien se est\u00E1 aqu\u00ED.", "Feels good to be here.", "Hier ist es sch\u00F6n.") }) };
                case 2:
                    return new string[] { Pick(new string[] { I18n.T("\u00A1Eh! \u00A1Que no soy un bot\u00F3n!", "Hey! I'm not a button!", "Eh! Ich bin doch kein Knopf!"),
                                                              I18n.T("\u00BFOtra vez t\u00FA?", "You again?", "Du schon wieder?"),
                                                              I18n.T("Si me pinchas m\u00E1s, me pongo a bailar.", "Poke me again and I'll start dancing.", "Stups mich noch mal und ich fang an zu tanzen."),
                                                              I18n.T("Yo hago las capturas m\u00E1s chulas, que lo sepas.", "I take the coolest captures, just so you know.", "Ich mache die coolsten Aufnahmen, nur damit du es wei\u00DFt."),
                                                              string.Format(I18n.T("Pulsa {0} y no me hagas cosquillas.", "Press {0} and stop tickling me.", "Dr\u00FCck {0} und lass das Kitzeln."), key),
                                                              I18n.T("Cuidado, que muerdo (un poquito).", "Careful, I bite (a little bit).", "Vorsicht, ich bei\u00DFe (ein bisschen)."),
                                                              I18n.T("\u00BFTe aburres? Se nota.", "Bored? It shows.", "Langweilst du dich? Merkt man."),
                                                              I18n.T("Me debes una captura.", "You owe me a capture.", "Du schuldest mir eine Aufnahme."),
                                                              I18n.T("Soy un artista, no un juguete.", "I'm an artist, not a toy.", "Ich bin ein K\u00FCnstler, kein Spielzeug."),
                                                              I18n.T("Mira que eres pesado\u2026 me gusta.", "You really are annoying\u2026 I like it.", "Du bist wirklich anstrengend\u2026 das gef\u00E4llt mir.") }) };
                default:
                    return new string[] { Pick(new string[] { I18n.T("\u00A1Eh, que me haces cosquillas!", "Hey, that tickles!", "Eh, das kitzelt!"),
                                                              I18n.T("\u00BFHacemos una captura?", "Shall we take a capture?", "Machen wir eine Aufnahme?"),
                                                              string.Format(I18n.T("Pulsa {0} y yo me encargo.", "Press {0} and I'll handle it.", "Dr\u00FCck {0}, und ich k\u00FCmmere mich drum."), key),
                                                              I18n.T("\u00A1Sonr\u00EDe! Bueno\u2026 yo ya lo hago.", "Smile! Well\u2026 I'm already smiling.", "L\u00E4cheln! Naja\u2026 ich l\u00E4chle schon."),
                                                              I18n.T("Bip bup. Bip.", "Beep boop. Beep.", "Bip bup. Bip."),
                                                              I18n.T("Me encanta cuando curvas las flechas.", "I love it when you curve the arrows.", "Ich liebe es, wenn du die Pfeile biegst."),
                                                              I18n.T("\u00A1Hola, hola!", "Hello, hello!", "Hallo, hallo!"),
                                                              I18n.T("\u00A1Otra vez! \u00A1Otra vez!", "Again! Again!", "Nochmal! Nochmal!"),
                                                              I18n.T("Jijiji, para, para.", "Hehehe, stop, stop.", "Hihi, Stopp, Stopp."),
                                                              I18n.T("\u00A1Qu\u00E9 d\u00EDa tan bueno para capturar!", "What a great day for capturing!", "Was f\u00FCr ein tolles Aufnahme-Wetter!") }) };
            }
        }

        public static string Tip(Settings s)
        {
            string[] tips =
            {
                I18n.T("Arrastra una miniatura a cualquier chat para pegarla.", "Drag a thumbnail to any chat to paste it.",
                       "Zieh eine Miniaturansicht in einen beliebigen Chat, um sie einzuf\u00FCgen."),
                I18n.T("En el editor, tira del punto azul de una flecha para curvarla.", "In the editor, drag the blue handle on an arrow to curve it.",
                       "Zieh im Editor am blauen Punkt eines Pfeils, um ihn zu kr\u00FCmmen."),
                I18n.T("Con la rueda del rat\u00F3n sobre la pila ves las capturas anteriores.", "Scroll over the stack with the mouse wheel to see previous captures.",
                       "Mit dem Mausrad \u00FCber dem Stapel siehst du die fr\u00FCheren Aufnahmen."),
                I18n.T("Al capturar, un clic en una ventana la saca enterita.", "When capturing, clicking a window captures it completely.",
                       "Beim Aufnehmen holst du ein Fenster mit einem Klick komplett."),
                I18n.T("En el editor, Enter copia y cierra.", "In the editor, Enter copies and closes.",
                       "Im Editor kopiert Eingabe und schlie\u00DFt."),
                // A cleared combination would read "Sin atajo captura...": then the tip names the action instead.
                Hotkeys.Split(s.HotScroll).Count > 0
                    ? string.Format(I18n.T("{0} captura una p\u00E1gina entera desplaz\u00E1ndola.",
                              "{0} captures a whole page by scrolling it.",
                              "{0} nimmt eine ganze Seite auf, indem du sie durchscrollst."), Hotkeys.Display(s.HotScroll))
                    : I18n.T("La captura con desplazamiento saca una p\u00E1gina entera; en Atajos puedes darle una combinaci\u00F3n.",
                             "Scrolling capture takes a whole page; you can assign a shortcut in Shortcuts.",
                             "Die Scroll-Aufnahme erfasst eine ganze Seite; in Tastenk\u00FCrzel kannst du ihr eine Kombination geben."),
                Hotkeys.Split(s.HotText).Count > 0
                    ? string.Format(I18n.T("{0} copia el texto de cualquier parte de la pantalla, sin guardar captura.",
                              "{0} copies text from anywhere on the screen, without saving a capture.",
                              "{0} kopiert Text von einer beliebigen Bildschirmstelle, ohne eine Aufnahme zu speichern."), Hotkeys.Display(s.HotText))
                    : I18n.T("Copiar texto de la pantalla saca el texto de cualquier sitio; en Atajos puedes darle una combinaci\u00F3n.",
                             "Copy screen text grabs text from anywhere; you can assign a shortcut in Shortcuts.",
                             "Bildschirmtext kopieren holt Text von jeder Stelle; in Tastenk\u00FCrzel kannst du ihm eine Kombination geben."),
                Hotkeys.Split(s.HotRepeat).Count > 0
                    ? string.Format(I18n.T("{0} vuelve a capturar la \u00FAltima \u00E1rea, sin elegirla otra vez.",
                              "{0} captures the last area again, without selecting it.",
                              "{0} nimmt den letzten Bereich erneut auf, ohne ihn neu zu w\u00E4hlen."), Hotkeys.Display(s.HotRepeat))
                    : I18n.T("\u00ABRepetir \u00E1rea\u00BB, en la bandeja, vuelve a capturar la \u00FAltima \u00E1rea; en Atajos puedes darle una combinaci\u00F3n.",
                             "\u201CRepeat area\u201D in the tray recaptures the last area; you can assign a shortcut in Shortcuts.",
                             "\u201EBereich wiederholen\u201C im Infobereich nimmt den letzten Bereich erneut auf; in Tastenk\u00FCrzel kannst du ihm eine Kombination geben."),
                I18n.T("El bot\u00F3n Fondo del editor deja tus capturas listas para presentar.", "The Backdrop button in the editor gets your captures ready for presentation.",
                       "Die Hintergrund-Schaltfl\u00E4che im Editor macht deine Aufnahmen pr\u00E4sentationsfertig."),
                I18n.T("Para tapar datos, usa Tapar en el editor: un bloque s\u00F3lido no se puede deshacer.",
                       "To cover private data, use Redact in the editor: a solid cover cannot be undone.",
                       "Zum Verdecken privater Daten nutze im Editor Schw\u00E4rzen: eine solide Abdeckung l\u00E4sst sich nicht r\u00FCckg\u00E4ngig machen."),
                I18n.T("En la secci\u00F3n Mascota puedes cambiarme el gorro, la ropa y hasta la especie.",
                       "In the Mascot section you can change my hat, clothes and even species.",
                       "Im Maskottchen-Bereich kannst du mir M\u00FCtze, Kleidung und sogar die Art \u00E4ndern."),
                I18n.T("Si me activas en el escritorio, te acompa\u00F1o junto a la barra de tareas.",
                       "If you enable me on the desktop, I'll keep you company near the taskbar.",
                       "Wenn du mich auf dem Desktop aktivierst, begleite ich dich neben der Taskleiste."),
                I18n.T("Puedes grabar la pantalla en v\u00EDdeo desde mi men\u00FA.", "You can record your screen as video right from my menu.",
                       "Du kannst den Bildschirm direkt \u00FCber mein Men\u00FC als Video aufnehmen."),
                I18n.T("Si me arrastras, me puedes dejar encima de cualquier ventana.", "If you drag me, you can place me on top of any window.",
                       "Wenn du mich herumziehst, kannst du mich \u00FCber ein beliebiges Fenster legen."),
                I18n.T("Un doble clic r\u00E1pido sobre m\u00ED y me pongo a hacer cosas.", "A quick double click on me and I'll do tricks.",
                       "Ein schneller Doppelklick auf mich, und ich fang an, Tricks zu machen."),
                I18n.T("Las capturas se guardan solas: \u00E1brelas desde Abrir mis capturas.",
                       "Captures save automatically: view them via Open my captures.",
                       "Aufnahmen werden automatisch gespeichert: \u00F6ffne sie \u00FCber \u201EMeine Aufnahmen \u00F6ffnen\u201C.")
            };
            return Pick(tips);
        }

        public static string Celebrate(Settings s)
        {
            return By(s.MascotPersonality,
                new string[] { I18n.T("\u00A1Buena captura!", "Great shot!", "Gute Aufnahme!"),
                               I18n.T("\u00A1Clic! Ha quedado genial.", "Click! Looking sharp.", "Klick! Perfekt geworden."),
                               I18n.T("Esa me la guardo.", "Saving this one.", "Die hebe ich mir auf."),
                               I18n.T("\u00A1Qu\u00E9 buen ojo!", "Great eye!", "Gutes Auge!"),
                               I18n.T("\u00A1Perfecta!", "Perfect!", "Perfekt!"),
                               I18n.T("\u00A1Otra para la colecci\u00F3n!", "Another one for the collection!", "Wieder eine f\u00FCr die Sammlung!") },
                new string[] { I18n.T("Bonita captura.", "Nice capture.", "Sch\u00F6ne Aufnahme."),
                               I18n.T("Guardada en mi memoria.", "Saved to my memory.", "In meinem Ged\u00E4chtnis gespeichert."),
                               I18n.T("Muy bien.", "Very nice.", "Sehr sch\u00F6n."),
                               I18n.T("Esa tiene buena luz.", "That one has great light.", "Die hat ein sch\u00F6nes Licht."),
                               I18n.T("Todo en su sitio.", "All in place.", "Alles an seinem Platz."),
                               I18n.T("Bien encuadrada.", "Nicely framed.", "Sch\u00F6n eingerahmt.") },
                new string[] { I18n.T("\u00A1Toma ya!", "Boom!", "Bumm!"),
                               I18n.T("\u00A1Eso es arte!", "Now that is art!", "Das ist echte Kunst!"),
                               I18n.T("\u00A1Directa al museo!", "Straight to the museum!", "Ab ins Museum damit!"),
                               I18n.T("Y yo sin cobrar derechos.", "And I don't even charge royalties.", "Und ich verlange nicht mal Lizenzgeb\u00FChren."),
                               I18n.T("Esa va a enmarcarse.", "That one deserves a frame.", "Die verdient einen Rahmen."),
                               I18n.T("\u00BFVes? Te lo dije.", "See? Told you so.", "Siehst du? Habe ich doch gesagt.") });
        }

        // Spontaneous lines on the desktop: a mix of its voice, the time of day and the odd tip.
        public static string Idle(Settings s)
        {
            int h = DateTime.Now.Hour;
            double r = rnd.NextDouble();
            if (r < 0.18) return Tip(s);
            string own = Own(s, 0.35);
            if (own != null) return own;
            if (r < 0.38)
            {
                if (h >= 7 && h < 11) return By(s.MascotPersonality,
                    new string[] { I18n.T("\u00A1Ese caf\u00E9 de la ma\u00F1ana!", "Morning coffee time!", "Zeit f\u00FCr den Morgenkaffee!"),
                                   I18n.T("Buen momento para empezar fuerte.", "Great time to start strong.", "Perfekte Zeit, stark durchzustarten."),
                                   I18n.T("\u00A1Arrancamos el d\u00EDa!", "Let's get this day going!", "Auf geht's in den Tag!") },
                    new string[] { I18n.T("La ma\u00F1ana va tranquila.", "A quiet morning.", "Ein ruhiger Morgen."),
                                   I18n.T("Con calma se empieza mejor.", "Starting slowly is starting best.", "Wer langsam anf\u00E4ngt, f\u00E4ngt gut an."),
                                   I18n.T("Un d\u00EDa a la vez.", "One day at a time.", "Einen Tag nach dem anderen.") },
                    new string[] { I18n.T("\u00BFYa has desayunado o qu\u00E9?", "Had breakfast yet or what?", "Schon gefr\u00FChst\u00FCckt oder was?"),
                                   I18n.T("Esos ojos dicen \u00ABfalta caf\u00E9\u00BB.", "Those eyes say \u201Cneeds coffee\u201D.", "Diese Augen sagen \u201Emehr Kaffee\u201C."),
                                   I18n.T("Primeras horas, primeros memes.", "Early hours, early memes.", "Fr\u00FChe Stunden, erste Memes.") });
                if (h >= 13 && h < 16) return By(s.MascotPersonality,
                    new string[] { I18n.T("\u00BFYa has comido?", "Had lunch yet?", "Schon zu Mittag gegessen?"),
                                   I18n.T("Se acerca la hora de la siesta.", "Nap time is getting close.", "Das Nickerchen r\u00FCckt n\u00E4her."),
                                   I18n.T("\u00A1A por la tarde!", "Ready for the afternoon!", "Bereit f\u00FCr den Nachmittag!") },
                    new string[] { I18n.T("Despu\u00E9s de comer, paseo corto.", "A short walk after eating.", "Nach dem Essen ein kurzer Spaziergang."),
                                   I18n.T("La sobremesa es sagrada.", "After-lunch break is sacred.", "Die Mittagspause ist heilig."),
                                   I18n.T("Qu\u00E9 sue\u00F1ecito\u2026", "Feeling sleepy\u2026", "Werde ganz sch\u00F6n m\u00FCde\u2026") },
                    new string[] { I18n.T("\u00BFY mi parte de la comida?", "Where's my share of the food?", "Wo bleibt mein Anteil vom Essen?"),
                                   I18n.T("Tras comer, ni los bots rinden.", "After eating, not even bots are productive.", "Nach dem Essen sind selbst Bots unproduktiv."),
                                   I18n.T("Ojo con la siesta, jefe.", "Watch out for that food coma, boss.", "Pass auf mit dem Essen-Koma, Chef.") });
                if (h >= 19 || h < 6) return By(s.MascotPersonality,
                    new string[] { I18n.T("\u00BFA\u00FAn trabajando? \u00A1\u00C1nimo!", "Still working? Keep going!", "Noch am Arbeiten? Weiter so!"),
                                   I18n.T("Ya queda menos para cerrar.", "Almost done for the day.", "Fast geschafft f\u00FCr heute."),
                                   I18n.T("\u00A1Qu\u00E9 horas!", "Look what time it is!", "Guck dir die Uhrzeit an!") },
                    new string[] { I18n.T("Ya es tarde. Descansa un poco.", "It's getting late. Get some rest.", "Es wird sp\u00E4t. Ru dich mal aus."),
                                   I18n.T("Los ojos tambi\u00E9n cansan.", "Eyes get tired too.", "Auch Augen werden m\u00FCde."),
                                   I18n.T("Ma\u00F1ana ser\u00E1 otro d\u00EDa.", "Tomorrow is another day.", "Morgen ist auch noch ein Tag.") },
                    new string[] { I18n.T("\u00BFNo tienes casa o qu\u00E9?", "Don't you have a home or something?", "Hast du eigentlich ein Zuhause?"),
                                   I18n.T("Ni yo trabajo a estas horas.", "Even I don't work at these hours.", "Selbst ich arbeite zu diesen Zeiten nicht."),
                                   I18n.T("Esto ya son horas extra.", "This counts as overtime.", "Das z\u00E4hlt als \u00DCberstunden.") });
            }
            return By(s.MascotPersonality,
                new string[] { I18n.T("\u00A1Hola! Sigo por aqu\u00ED.", "Hello! Still here.", "Hallo! Ich bin noch da."),
                               I18n.T("\u00BFNecesitas una captura?", "Need a capture?", "Brauchst du eine Aufnahme?"),
                               I18n.T("Voy a dar una vuelta.", "Going for a stroll.", "Ich geh mal ein bisschen spazieren."),
                               I18n.T("\u00A1Qu\u00E9 buen d\u00EDa para capturar!", "What a great day for capturing!", "Was f\u00FCr ein tolles Aufnahme-Wetter!"),
                               I18n.T("\u00BFMe ense\u00F1as lo que haces?", "Show me what you're working on?", "Zeigst du mir, woran du arbeitest?"),
                               I18n.T("Estoy aqu\u00ED por si me necesitas.", "I'm right here if you need me.", "Ich bin hier, falls du mich brauchst."),
                               I18n.T("Bip bup, todo en orden.", "Beep boop, all systems go.", "Bip bup, alle Systeme bereit."),
                               I18n.T("\u00BFUna captura r\u00E1pida?", "Quick capture?", "Eine schnelle Aufnahme?"),
                               I18n.T("Me gusta tu escritorio.", "Nice desktop you got there.", "Sch\u00F6ner Desktop, den du da hast."),
                               I18n.T("Si quieres, grabo un v\u00EDdeo.", "I can record a video if you want.", "Wenn du willst, nehme ich ein Video auf.") },
                new string[] { I18n.T("Qu\u00E9 paz\u2026", "So peaceful\u2026", "So friedlich\u2026"),
                               I18n.T("Sigo aqu\u00ED.", "Still here.", "Ich bin noch hier."),
                               I18n.T("Un paseo corto y vuelvo.", "Short walk and I'll be back.", "Kurzer Spaziergang, dann bin ich zur\u00FCck."),
                               I18n.T("Todo tranquilo.", "All quiet.", "Alles ruhig."),
                               I18n.T("Se est\u00E1 bien aqu\u00ED.", "It's nice here.", "Hier ist es sch\u00F6n."),
                               I18n.T("Sin prisa, sin pausa.", "No rush, no pause.", "Keine Eile, keine Pause."),
                               I18n.T("Disfruto del silencio.", "Enjoying the quiet.", "Ich genie\u00DFe die Stille."),
                               I18n.T("Respira hondo.", "Take a deep breath.", "Atme mal tief durch."),
                               I18n.T("Los buenos momentos no hacen ruido.", "Good moments don't make noise.", "Sch\u00F6ne Momente machen keinen L\u00E4rm.") },
                new string[] { I18n.T("\u00BFTrabajando o mirando memes?", "Working or looking at memes?", "Arbeitest du oder guckst du Memes?"),
                               I18n.T("Me aburro\u2026 \u00BFcapturamos algo?", "I'm bored\u2026 let's capture something?", "Mir ist langweilig\u2026 nehmen wir was auf?"),
                               I18n.T("Ojo, que te vigilo.", "Careful, I'm watching you.", "Vorsicht, ich beobachte dich."),
                               I18n.T("\u00BFEse es tu mejor escritorio?", "Is that your best desktop setup?", "Ist das dein bester Desktop?"),
                               I18n.T("Me debes una captura.", "You owe me a capture.", "Du schuldest mir eine Aufnahme."),
                               I18n.T("Yo no digo nada, pero\u2026", "I'm not saying anything, but\u2026", "Ich sag ja nichts, aber\u2026"),
                               I18n.T("Tienes demasiadas pesta\u00F1as abiertas.", "You have way too many tabs open.", "Du hast viel zu viele Tabs offen."),
                               I18n.T("Esa ventana la he visto antes.", "I've seen that window before.", "Das Fenster kannte ich schon."),
                               I18n.T("Si me ignoras, hago cosas raras.", "If you ignore me, I'll do weird things.", "Wenn du mich ignorierst, mache ich komische Dinge."),
                               I18n.T("Hoy me siento fotog\u00E9nico.", "Feeling photogenic today.", "Ich f\u00FChle mich heute fotogen.") });
        }

        // What it says when it starts doing something with its props (null: nothing this time).
        public static string ForAct(int personality, int act)
        {
            if (rnd.NextDouble() < 0.45) return null;
            switch (act)
            {
                case MascotProps.Coffee:
                    return By(personality, new string[] { I18n.T("\u00A1Un caf\u00E9!", "Coffee time!", "Kaffeezeit!"), I18n.T("Pausa para el caf\u00E9.", "Coffee break.", "Kaffeepause."), I18n.T("Mmm, qu\u00E9 rico.", "Mmm, smells so good.", "Mmm, das riecht so gut.") },
                                               new string[] { I18n.T("Un cafecito con calma.", "A quiet coffee.", "Ein Kaffee in Ruhe."), I18n.T("Sorbo a sorbo.", "Sip by sip.", "Schluck f\u00FCr Schluck."), I18n.T("Qu\u00E9 aroma.", "What an aroma.", "Was f\u00FCr ein Aroma.") },
                                               new string[] { I18n.T("Ni se te ocurra pedirme.", "Don't even think about asking.", "Komm gar nicht erst auf die Idee."), I18n.T("Caf\u00E9 para m\u00ED, no para ti.", "Coffee for me, not for you.", "Kaffee f\u00FCr mich, nicht f\u00FCr dich."), I18n.T("Mi quinto de hoy.", "My fifth one today.", "Mein f\u00FCnfter heute.") });
                case MascotProps.Juggle:
                    return By(personality, new string[] { I18n.T("\u00A1Mira, mira!", "Look, look!", "Guck mal, mal!"), I18n.T("\u00A1Tres a la vez!", "Three at once!", "Drei auf einmal!"), I18n.T("\u00A1Sin manos! Bueno, con ellas.", "No hands! Well, with hands.", "Ohne H\u00E4nde! Naja, mit H\u00E4nden.") },
                                               new string[] { I18n.T("Un poco de pr\u00E1ctica.", "Just a bit of practice.", "Ein bisschen \u00DAbung."), I18n.T("El ritmo es lo que cuenta.", "Rhythm is what counts.", "Auf den Rhythmus kommt's an."), I18n.T("Concentraci\u00F3n.", "Focus.", "Konzentration.") },
                                               new string[] { I18n.T("\u00BFA que no puedes t\u00FA?", "Bet you can't do this!", "Du schaffst das doch nicht!"), I18n.T("Cobro entrada, eh.", "Admission isn't free, you know.", "Eintritt kostet was, wei\u00DFt du."), I18n.T("Ojo, que se me caen.", "Careful, might drop one.", "Vorsicht, einer f\u00E4llt gleich runter.") });
                case MascotProps.Console:
                    return By(personality, new string[] { I18n.T("\u00A1Un ratito de juego!", "A bit of gaming!", "Eine Runde zocken!"), I18n.T("\u00A1Nuevo r\u00E9cord!", "New high score!", "Neuer Highscore!"), I18n.T("Una partidita r\u00E1pida.", "Quick match.", "Eine kurze Partie.") },
                                               new string[] { I18n.T("Un nivelito y ya.", "Just one level.", "Nur ein Level."), I18n.T("Juego tranquilo.", "Casual gaming.", "Gem\u00FCtlich zocken."), I18n.T("Sin prisas, sin vidas.", "No rush, infinite lives.", "Keine Eile, unendlich viele Leben.") },
                                               new string[] { I18n.T("Ni me hables, voy ganando.", "Don't talk to me, I'm winning.", "Sprich mich nicht an, ich gewinne."), I18n.T("\u00A1Cuarta vida!", "Fourth life!", "Viertes Leben!"), I18n.T("Esto no es trabajo, es cultura.", "This isn't work, this is culture.", "Das ist keine Arbeit, das ist Kultur.") });
                case MascotProps.Stretch:
                    return By(personality, new string[] { I18n.T("\u00A1A estirar!", "Time to stretch!", "Zeit zum Strecken!"), I18n.T("Ufff, qu\u00E9 bien sienta.", "Phew, feels so good.", "Uff, das tut so gut."), I18n.T("\u00A1Cr\u00E1c, cr\u00E1c!", "Crack, crack!", "Knacks, knacks!") },
                                               new string[] { I18n.T("Estirar tambi\u00E9n es descansar.", "Stretching is resting too.", "Strecken ist auch Erholung."), I18n.T("Aaah\u2026", "Aaah\u2026", "Aaah\u2026"), I18n.T("Un poco de movimiento.", "A little movement.", "Ein bisschen Bewegung.") },
                                               new string[] { I18n.T("Hasta los bots se anquilosan.", "Even bots get stiff.", "Sogar Bots werden steif."), I18n.T("Mira qu\u00E9 flexibilidad.", "Look at that flexibility.", "Guck dir diese Beweglichkeit an."), I18n.T("Tanto estar sentado\u2026", "All that sitting down\u2026", "Das viele Sitzen\u2026") });
                case MascotProps.Read:
                    return By(personality, new string[] { I18n.T("\u00A1Qu\u00E9 libro m\u00E1s bueno!", "What a great book!", "Was f\u00FCr ein gutes Buch!"), I18n.T("Un capitulito.", "Just one chapter.", "Nur ein Kapitel."), I18n.T("\u00A1Mira qu\u00E9 final!", "Look at that ending!", "Guck dir das Ende an!") },
                                               new string[] { I18n.T("Un buen libro y silencio.", "A good book and silence.", "Ein gutes Buch und Stille."), I18n.T("Cap\u00EDtulo a cap\u00EDtulo.", "Chapter by chapter.", "Kapitel f\u00FCr Kapitel."), I18n.T("Leyendo un rato.", "Reading for a bit.", "Ich lese ein bisschen.") },
                                               new string[] { I18n.T("No hagas spoilers.", "No spoilers.", "Keine Spoiler."), I18n.T("Shhh, que llego al final.", "Shhh, reaching the climax.", "Pst, ich bin kurz vorm H\u00F6hepunkt."), I18n.T("Mejor que tu escritorio.", "Better than your desktop.", "Besser als dein Desktop.") });
                case MascotProps.Water:
                    return By(personality, new string[] { I18n.T("\u00A1Ag\u00FCita fresca!", "Fresh water!", "Frisches Wasser!"), I18n.T("\u00A1A tu salud!", "Cheers to you!", "Auf dich!"), I18n.T("Glu, glu, glu.", "Gulp, gulp, gulp.", "Gluck, gluck, gluck.") },
                                               new string[] { I18n.T("Un poco de agua sienta bien.", "A little water does wonders.", "Ein bisschen Wasser tut Wunder."), I18n.T("Sorbo a sorbo.", "Sip by sip.", "Schluck f\u00FCr Schluck."), I18n.T("Hidratarse es cuidarse.", "Hydration is self-care.", "Trinken ist Selbstf\u00FCrsorge.") },
                                               new string[] { I18n.T("Agua, el refresco de los campeones.", "Water, the drink of champions.", "Wasser, das Getr\u00E4nk der Champions."), I18n.T("\u00BFBrindamos? T\u00FA tambi\u00E9n.", "Cheers? You too.", "Ansto\u00DFen? Du auch."), I18n.T("Ni caf\u00E9 ni nada: agua.", "No coffee or soda: just water.", "Kein Kaffee, keine Brause: nur Wasser.") });
                case MascotProps.EyeRest:
                    return By(personality, new string[] { I18n.T("\u00A1Qu\u00E9 lejos se ve todo!", "Everything looks so far away!", "Wie weit man da alles sieht!"), I18n.T("Miro el horizonte.", "Gazing at the horizon.", "Ich schau in die Ferne."), I18n.T("\u00A1All\u00ED a lo lejos!", "Way over there!", "Dort dr\u00FCben in der Ferne!") },
                                               new string[] { I18n.T("Los ojos tambi\u00E9n descansan.", "Eyes need rest too.", "Auch Augen brauchen Pause."), I18n.T("Mirar lejos relaja.", "Looking far away relaxes.", "In die Ferne schauen entspannt."), I18n.T("Calma para la vista.", "Calm for the sight.", "Ruhe f\u00FCr die Augen.") },
                                               new string[] { I18n.T("Veo, veo\u2026 algo lejano.", "I spy\u2026 something distant.", "Ich sehe, ich sehe\u2026 etwas Fernes."), I18n.T("Tu pantalla puede esperar.", "Your screen can wait.", "Dein Bildschirm kann warten."), I18n.T("Mira, un p\u00E1jaro. O no.", "Look, a bird. Or not.", "Guck mal, ein Vogel. Oder auch nicht.") });
                case MascotProps.Legs:
                    return By(personality, new string[] { I18n.T("\u00A1Arriba y abajo!", "Up and down!", "Auf und ab!"), I18n.T("\u00A1Una, dos!", "One, two!", "Eins, zwei!"), I18n.T("\u00A1Vamos, piernas!", "Let's go, legs!", "Los, Beine!") },
                                               new string[] { I18n.T("Despacito, sin prisa.", "Slowly, no rush.", "Langsam, keine Eile."), I18n.T("Mover las piernas sienta bien.", "Moving your legs feels good.", "Beine bewegen tut gut."), I18n.T("Arriba\u2026 y abajo.", "Up\u2026 and down.", "Auf\u2026 und ab.") },
                                               new string[] { I18n.T("Esto es mejor que el gimnasio.", "Better than the gym.", "Besser als das Fitnessstudio."), I18n.T("No me copies, que te canso.", "Don't copy me, you'll get tired.", "Mach mich nicht nach, du wirst m\u00FCde."), I18n.T("\u00A1Mira qu\u00E9 piernas!", "Look at these legs!", "Guck dir diese Beine an!") });
                case MascotProps.Breathe:
                    return By(personality, new string[] { I18n.T("Respira conmigo.", "Breathe with me.", "Atme mit mir."), I18n.T("Dentro\u2026 y fuera.", "In\u2026 and out.", "Ein\u2026 und aus."), I18n.T("\u00A1Qu\u00E9 bien se respira!", "Breathing feels amazing!", "Atmen f\u00FChlt sich so gut an!") },
                                               new string[] { I18n.T("Inspira\u2026 suelta.", "Breathe in\u2026 release.", "Einatmen\u2026 loslassen."), I18n.T("Todo a su ritmo.", "Everything at its pace.", "Alles in seinem Tempo."), I18n.T("Un poco de calma.", "A moment of calm.", "Ein Moment der Ruhe.") },
                                               new string[] { I18n.T("Respirar es gratis, aprovecha.", "Breathing is free, enjoy it.", "Atmen ist gratis, nutz es."), I18n.T("Hasta un bot necesita aire.", "Even a bot needs air.", "Selbst ein Bot braucht Luft."), I18n.T("Inspira hondo, que no cobro.", "Deep breath, I won't charge.", "Atme tief durch, das kostet nichts.") });
                default:
                    return By(personality, new string[] { I18n.T("\u00A1Sonr\u00EDe!", "Smile!", "L\u00E4cheln!"), I18n.T("\u00A1Patata!", "Cheese!", "Cheese!"), I18n.T("\u00A1Clic!", "Click!", "Klick!") },
                                               new string[] { I18n.T("Un buen recuerdo.", "A nice memory.", "Eine sch\u00F6ne Erinnerung."), I18n.T("Luz perfecta.", "Perfect lighting.", "Perfektes Licht."), I18n.T("Quieto un segundo.", "Hold still a second.", "Ruhig eine Sekunde lang.") },
                                               new string[] { I18n.T("Salgo mejor yo.", "I look better.", "Ich komme besser r\u00FCber."), I18n.T("Esta va al marco.", "Frame-worthy.", "Die kommt in den Rahmen."), I18n.T("Di \u00ABwhisky\u00BB.", "Say cheese.", "Sag Cheese.") });
            }
        }

        // ---- Healthy breaks: the reminder, what it says while you do it, and the thanks.

        public static string Remind(Settings s, int kind, int minutes)
        {
            if (I18n.IsKorean)
            {
                string tEn = string.Format(I18n.Localize("{0} minutes"), minutes);
                switch (kind)
                {
                    case WellKind.Water:
                        return By(s.MascotPersonality,
                            new string[] { string.Format(I18n.Localize("Time to drink water! It's been {0} without a sip."), tEn), "A glass of water? I'll have one with you.", "Fresh water! Your body will thank you." },
                            new string[] { "A little water will do you good.", string.Format(I18n.Localize("You've been working for {0}. Shall we drink water?"), tEn), "A glass of water and we continue." },
                            new string[] { string.Format(I18n.Localize("Water? Ring a bell? It's been {0}."), tEn), "If you don't drink it, I will.", "Plants that aren't watered dry out." });
                    case WellKind.Eyes:
                        return By(s.MascotPersonality,
                            new string[] { "Rest your eyes! Look at something far away for 20 seconds.", string.Format(I18n.Localize("Your eyes have been glued to the screen for {0}. Look away?"), tEn), "20-20-20 rule: look 20 feet away for 20 seconds." },
                            new string[] { "Give your eyes a break: gaze into the distance for a moment.", "20 seconds looking far away. No rush.", "Your eyes deserve a pause." },
                            new string[] { "That screen isn't going anywhere. Look out the window.", "Blink, you're forgetting.", "Your eyes are begging for a vacation. Twenty seconds." });
                    case WellKind.Move:
                        return By(s.MascotPersonality,
                            new string[] { string.Format(I18n.Localize("Up! You've been in that chair for {0}. Let's stretch those legs!"), tEn), "Time to move! Stand up for a moment.", "Short walk? Your legs are asking for it." },
                            new string[] { "Stand up and stretch your body a bit.", "A minute on your feet feels wonderful.", string.Format(I18n.Localize("You've been sitting for {0}. Move around a bit."), tEn) },
                            new string[] { "Your chair is going to charge rent.", "If you don't stand up, I'll start dancing.", "Are you glued to that chair? Up!" });
                    default:
                        return By(s.MascotPersonality,
                            new string[] { "Shall we breathe together for a minute?", "A breathing break will feel great.", "Six slow breaths and you're good as new." },
                            new string[] { "Take a deep breath with me for a moment.", "A minute of calm: let's breathe.", "Breathe in, let go. Simple as that." },
                            new string[] { "Breathe, I can see the stress.", "One minute without emails. Breathe.", "Even bots breathe. Well, almost." });
                }
            }
            string t = minutes >= 60
                ? (minutes % 60 == 0
                    ? (minutes / 60 == 1 ? I18n.T("una hora", "an hour", "eine Stunde")
                                         : minutes / 60 + " " + I18n.T("horas", "hours", "Stunden"))
                    : I18n.T("hora y media", "an hour and a half", "eineinhalb Stunden"))
                : minutes + " " + I18n.T("minutos", "minutes", "Minuten");
            switch (kind)
            {
                case WellKind.Water:
                    return By(s.MascotPersonality,
                        new string[] { I18n.T("\u00A1Hora de beber agua! Llevas " + t + " sin un trago.", "Time to drink water! It's been " + t + " without a sip.", "Zeit zu trinken! Du bist schon " + t + " ohne einen Schluck."),
                                       I18n.T("\u00BFUn vaso de agua? Yo me tomo uno contigo.", "A glass of water? I'll have one with you.", "Ein Glas Wasser? Ich trinke eins mit dir mit."),
                                       I18n.T("\u00A1Ag\u00FCita! Tu cuerpo te lo agradecer\u00E1.", "Fresh water! Your body will thank you.", "Frisches Wasser! Dein K\u00F6rper wird es dir danken.") },
                        new string[] { I18n.T("Un poco de agua te sentar\u00E1 bien.", "A little water will do you good.", "Ein bisschen Wasser tut dir gut."),
                                       I18n.T("Llevas " + t + " trabajando. \u00BFBebemos agua?", "You've been working for " + t + ". Shall we drink water?", "Du arbeitest schon " + t + ". Trinken wir ein Glas Wasser?"),
                                       I18n.T("Un vaso de agua y seguimos.", "A glass of water and we continue.", "Ein Glas Wasser, und es geht weiter.") },
                        new string[] { I18n.T("\u00BFAgua? \u00BFTe suena? Llevas " + t + ".", "Water? Ring a bell? It's been " + t + ".", "Wasser? Klingelt was? Du bist schon " + t + " ohne."),
                                       I18n.T("Si no bebes agua, me la bebo yo.", "If you don't drink it, I will.", "Wenn du nicht trinkst, trinke ich es eben."),
                                       I18n.T("Plantita que no se riega, se seca.", "Plants that aren't watered dry out.", "Pflanzen, die nicht gegossen werden, trocknen aus.") });
                case WellKind.Eyes:
                    return By(s.MascotPersonality,
                        new string[] { I18n.T("\u00A1Descansa la vista! Mira algo lejano 20 segundos.", "Rest your eyes! Look at something far away for 20 seconds.", "Ruh die Augen aus! Schau 20 Sekunden auf etwas Fernes."),
                                       I18n.T("Tus ojos llevan " + t + " pegados a la pantalla. \u00BFMiramos lejos?", "Your eyes have been glued to the screen for " + t + ". Look away?", "Deine Augen kleben schon " + t + " am Bildschirm. Schauen wir mal weg?"),
                                       I18n.T("Regla 20-20-20: mira a 6 metros durante 20 segundos.", "20-20-20 rule: look 20 feet away for 20 seconds.", "20-20-20-Regel: Schau 6 Meter weit, 20 Sekunden lang.") },
                        new string[] { I18n.T("Deja que tus ojos descansen: mira a lo lejos un momento.", "Give your eyes a break: gaze into the distance for a moment.", "Gib deinen Augen eine Pause: schau kurz in die Ferne."),
                                       I18n.T("20 segundos mirando lejos. Sin prisa.", "20 seconds looking far away. No rush.", "20 Sekunden in die Ferne schauen. Keine Eile."),
                                       I18n.T("Tus ojos merecen una pausa.", "Your eyes deserve a pause.", "Deine Augen verdienen eine Pause.") },
                        new string[] { I18n.T("Esa pantalla no se va a escapar. Mira por la ventana.", "That screen isn't going anywhere. Look out the window.", "Der Bildschirm l\u00E4uft ja nicht weg. Guck mal zum Fenster."),
                                       I18n.T("Parpadea, que se te olvida.", "Blink, you're forgetting.", "Blinzel mal, du vergisst es ja."),
                                       I18n.T("Tus ojos piden vacaciones. Veinte segundos.", "Your eyes are begging for a vacation. Twenty seconds.", "Deine Augen flehen um Urlaub. Zwanzig Sekunden.") });
                case WellKind.Move:
                    return By(s.MascotPersonality,
                        new string[] { I18n.T("\u00A1Arriba! Llevas " + t + " en la silla. \u00A1Estiramos las piernas!", "Up! You've been in that chair for " + t + ". Let's stretch those legs!", "Auf! Du sitzt schon " + t + " in dem Stuhl. Strecken wir die Beine!"),
                                       I18n.T("\u00A1Hora de moverse! Lev\u00E1ntate un momento.", "Time to move! Stand up for a moment.", "Zeit, dich zu bewegen! Steh kurz auf."),
                                       I18n.T("\u00BFDamos un pase\u00EDto? Tus piernas lo piden.", "Short walk? Your legs are asking for it.", "Kurzer Spaziergang? Deine Beine betteln darum.") },
                        new string[] { I18n.T("Lev\u00E1ntate y estira un poco el cuerpo.", "Stand up and stretch your body a bit.", "Steh auf und streck dich ein bisschen."),
                                       I18n.T("Un minuto de pie sienta de maravilla.", "A minute on your feet feels wonderful.", "Eine Minute auf den Beinen tut Wunder."),
                                       I18n.T("Llevas " + t + " en la silla. Mu\u00E9vete un poco.", "You've been sitting for " + t + ". Move around a bit.", "Du sitzt schon " + t + ". Beweg dich mal ein bisschen.") },
                        new string[] { I18n.T("Tu silla te va a cobrar alquiler.", "Your chair is going to charge rent.", "Dein Stuhl wird bald Miete verlangen."),
                                       I18n.T("Si no te levantas, me pongo a bailar.", "If you don't stand up, I'll start dancing.", "Wenn du nicht aufstehst, fange ich an zu tanzen."),
                                       I18n.T("\u00BFTe has pegado a la silla? Arriba.", "Are you glued to that chair? Up!", "Am Stuhl festgeklebt? Auf!") });
                default:
                    return By(s.MascotPersonality,
                        new string[] { I18n.T("\u00BFRespiramos juntos un minuto?", "Shall we breathe together for a minute?", "Atmen wir zusammen eine Minute?"),
                                       I18n.T("Una pausa para respirar te va a encantar.", "A breathing break will feel great.", "Eine Atempause tut richtig gut."),
                                       I18n.T("Seis respiraciones lentas y como nuevo.", "Six slow breaths and you're good as new.", "Sechs langsame Atemz\u00FCge und du bist wie neu.") },
                        new string[] { I18n.T("Respira hondo conmigo un momento.", "Take a deep breath with me for a moment.", "Atme kurz mit mir tief durch."),
                                       I18n.T("Un minuto de calma: respiremos.", "A minute of calm: let's breathe.", "Eine Minute Ruhe: atmen wir."),
                                       I18n.T("Inspira, suelta. As\u00ED de simple.", "Breathe in, let go. Simple as that.", "Einatmen, loslassen. So einfach.") },
                        new string[] { I18n.T("Respira, que se te nota el estr\u00E9s.", "Breathe, I can see the stress.", "Atme, ich sehe den Stress."),
                                       I18n.T("Un minuto sin correos. Respira.", "One minute without emails. Breathe.", "Eine Minute ohne Mails. Atme."),
                                       I18n.T("Hasta los bots respiramos. Bueno, casi.", "Even bots breathe. Well, almost.", "Sogar Bots atmen. Naja, fast.") });
            }
        }

        public static string WellDone(Settings s, int kind, int today)
        {
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Genial! ", "Awesome! ", "Super! "), I18n.T("\u00A1Muy bien! ", "Great job! ", "Gut gemacht! "), I18n.T("\u00A1As\u00ED me gusta! ", "That's what I like! ", "So gef\u00E4llt mir das! ") },
                      new string[] { I18n.T("Bien hecho. ", "Well done. ", "Gut gemacht. "), I18n.T("Perfecto. ", "Perfect. ", "Perfekt. "), I18n.T("Qu\u00E9 bien. ", "Very nice. ", "Sehr sch\u00F6n. ") },
                      new string[] { I18n.T("Mira t\u00FA qu\u00E9 disciplina. ", "Look at that discipline. ", "Guck dir diese Disziplin an. "), I18n.T("No est\u00E1 mal. ", "Not bad at all. ", "Nicht schlecht. "), I18n.T("Ole. ", "Bravo. ", "Bravo. ") })
                   + WellCount(kind, today, s.WaterGoal);
        }

        // How many of that break today, said plainly (also when the mascot doesn't chat).
        public static string WellCount(int kind, int today) { return WellCount(kind, today, 0); }

        // waterGoal: the glasses a day aimed at (0 none), said with the water count.
        public static string WellCount(int kind, int today, int waterGoal)
        {
            if (I18n.IsKorean)
            {
                if (kind == WellKind.Water && waterGoal > 0)
                    return string.Format(I18n.Localize(today >= waterGoal ? "Water goal reached: {0} glasses today!" : "{0} of {1} glasses today."), today, waterGoal);
                return string.Format(I18n.Localize(kind == WellKind.Water ? "{0} glasses today." : kind == WellKind.Eyes ? "{0} eye rests today." :
                    kind == WellKind.Move ? "{0} movement breaks today." : "{0} breathing sessions today."), today);
            }
            if (kind == WellKind.Water && waterGoal > 0)
                return today >= waterGoal
                    ? (today == waterGoal
                        ? I18n.T("\u00A1Objetivo de agua cumplido: " + waterGoal + " vasos!", "Water goal reached: " + waterGoal + " glasses!", "Wasserziel erreicht: " + waterGoal + " Gl\u00E4ser!")
                        : I18n.T("Ya van " + today + " vasos hoy: objetivo cumplido.", today + " glasses today: goal achieved!", "Schon " + today + " Gl\u00E4ser heute: Ziel geschafft!"))
                    : (today == 1
                        ? I18n.T("Tu primer vaso de hoy (de " + waterGoal + ").", "Your first glass today (out of " + waterGoal + ").", "Dein erstes Glas heute (von " + waterGoal + ").")
                        : I18n.T("Ya van " + today + " de " + waterGoal + " vasos hoy.", today + " of " + waterGoal + " glasses today.", "Schon " + today + " von " + waterGoal + " Gl\u00E4sern heute."));
            return kind == WellKind.Water ? (today == 1 ? I18n.T("Tu primer vaso de hoy.", "Your first glass today.", "Dein erstes Glas heute.")
                                                        : I18n.T("Ya van " + today + " vasos hoy.", today + " glasses today.", "Schon " + today + " Gl\u00E4ser heute."))
                 : kind == WellKind.Eyes ? (today == 1 ? I18n.T("Primer descanso de vista de hoy.", "First eye rest today.", "Erste Augenpause heute.")
                                                       : I18n.T(today + " descansos de vista hoy.", today + " eye rests today.", today + " Augenpausen heute."))
                 : kind == WellKind.Move ? (today == 1 ? I18n.T("Primera pausa para moverte.", "First movement break today.", "Erste Bewegungspause heute.")
                                                       : I18n.T(today + " pausas para moverte hoy.", today + " movement breaks today.", today + " Bewegungspausen heute."))
                 : (today == 1 ? I18n.T("Primera respiraci\u00F3n de hoy.", "First breathing session today.", "Erste Atem\u00FCbung heute.")
                               : I18n.T(today + " respiraciones hoy.", today + " breathing sessions today.", today + " Atem\u00FCbungen heute."));
        }

        // Answers to "Recu\u00E9rdamelo en 10 min" and "Hoy no": they always say what was decided, chatty or not.
        public static string WellLater(Settings s)
        {
            if (!s.MascotTalks) return I18n.T("Te lo recuerdo en 10 minutos.", "I'll remind you in 10 minutes.", "Ich erinnere dich in 10 Minuten.");
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Vale! Te aviso en 10 minutos.", "Got it! I'll remind you in 10 minutes.", "Alles klar! Ich erinnere dich in 10 Minuten."),
                                                          I18n.T("\u00A1De acuerdo! En 10 minutos te lo recuerdo.", "Alright! Reminding you in 10 minutes.", "Gut! In 10 Minuten erinnere ich dich.") },
                      new string[] { I18n.T("Sin problema: en 10 minutos te aviso.", "No problem: I'll notify you in 10 minutes.", "Kein Problem: In 10 Minuten melde ich mich."),
                                     I18n.T("Te lo recuerdo en 10 minutos.", "I'll remind you in 10 minutes.", "Ich erinnere dich in 10 Minuten.") },
                      new string[] { I18n.T("Vale, 10 minutos y no te escapas.", "Fine, 10 minutes and you can't escape.", "Gut, 10 Minuten, und du entkommst nicht."),
                                     I18n.T("En 10 minutos vuelvo, \u00BFeh?", "I'll be back in 10 minutes, okay?", "In 10 Minuten bin ich zur\u00FCck, okay?") });
        }

        public static string WellToday(Settings s, int kind)
        {
            if (I18n.IsKorean)
            {
                string whatEn = I18n.Localize(kind == WellKind.Water ? "water reminders" : kind == WellKind.Eyes ? "eye rest reminders" : kind == WellKind.Move ? "movement breaks" : "breathing breaks");
                if (!s.MascotTalks) return I18n.KoreanFormat(I18n.Localize("No more {0} today."), whatEn);
                return By(s.MascotPersonality, new string[] { I18n.KoreanFormat(I18n.Localize("Got it! No more {0} today."), whatEn) }, new string[] { I18n.KoreanFormat(I18n.Localize("Alright, no more {0} for today."), whatEn) },
                          new string[] { I18n.KoreanFormat(I18n.Localize("Fine, you're off the hook for {0} today."), whatEn) });
            }
            string what = kind == WellKind.Water ? I18n.T("lo del agua", "water reminders", "Wasser-Erinnerungen")
                        : kind == WellKind.Eyes ? I18n.T("lo de la vista", "eye rest reminders", "Augenpausen")
                        : kind == WellKind.Move ? I18n.T("lo de moverte", "movement breaks", "Bewegungspausen")
                        : I18n.T("lo de respirar", "breathing breaks", "Atem\u00FCbungen");
            if (!s.MascotTalks) return I18n.T("Hoy no te recuerdo m\u00E1s " + what + ".", "No more " + what + " today.", "Heute keine " + what + " mehr.");
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Vale! Hoy no te recuerdo m\u00E1s " + what + ".", "Got it! No more " + what + " today.", "Alles klar! Heute keine " + what + " mehr.") },
                      new string[] { I18n.T("De acuerdo, por hoy no te recuerdo m\u00E1s " + what + ".", "Alright, no more " + what + " for today.", "In Ordnung, f\u00FCr heute keine " + what + " mehr.") },
                      new string[] { I18n.T("Vale, hoy te libras de " + what + ".", "Fine, you're off the hook for " + what + " today.", "Na gut, f\u00FCr heute entfallen " + what + ".") });
        }

        // What it says when told to do something (-1: woken up on request).
        public static string ForCmd(int personality, int cmd)
        {
            switch (cmd)
            {
                case -1: return By(personality, new string[] { I18n.T("\u00A1Ya estoy despierto!", "I'm awake!", "Ich bin wach!"), I18n.T("\u00A1Buenos d\u00EDas otra vez!", "Good morning again!", "Nochmal guten Morgen!") },
                                                  new string[] { I18n.T("Mmm\u2026 ya voy.", "Mmm\u2026 coming.", "Mmm\u2026 ich komm ja schon."), I18n.T("Un ratito m\u00E1s y me levanto.", "Just another minute and I'm up.", "Noch eine Minute, dann stehe ich auf.") },
                                                  new string[] { I18n.T("\u00A1Eh! Estaba so\u00F1ando algo genial.", "Hey! I was having a great dream.", "Eh! Ich hatte gerade einen richtig sch\u00F6nen Traum."), I18n.T("\u00BFYa? Qu\u00E9 pesado.", "Already? Come on.", "Schon? Mensch.") });
                case MascotCmd.Play: return By(personality, new string[] { I18n.T("\u00A1A jugar!", "Let's play!", "Spielen!"), I18n.T("\u00A1Siii\u00ED, juguemos!", "Yeees, let's play!", "Jaaaa, lass uns spielen!"), I18n.T("\u00A1Qu\u00E9 ganas ten\u00EDa!", "I was so hoping for this!", "Darauf habe ich mich so gefreut!") },
                                                     new string[] { I18n.T("Un ratito de juego, vale.", "A little playtime, sure.", "Eine kleine Spielrunde, gerne."), I18n.T("Jugamos sin prisa.", "Let's play, no rush.", "Lass uns spielen, ganz ohne Eile.") },
                                                     new string[] { I18n.T("\u00A1Ahora ver\u00E1s!", "Now you'll see!", "Jetzt wirst du es sehen!"), I18n.T("Te reto a ver qui\u00E9n aguanta m\u00E1s.", "I challenge you to see who lasts longer.", "Ich wette, ich halte l\u00E4nger durch als du.") });
                case MascotCmd.Juggle: return By(personality, new string[] { I18n.T("\u00A1Mira, mira!", "Look, look!", "Guck mal, mal!"), I18n.T("\u00A1Tres bolas! \u00A1All\u00E1 voy!", "Three balls! Here I go!", "Drei B\u00E4lle! Los geht's!") },
                                                     new string[] { I18n.T("Con ritmo y sin prisa.", "With rhythm and steady pace.", "Mit Rhythmus und ruhiger Hand."), I18n.T("A ver si no se cae ninguna.", "Let's see if I don't drop any.", "Mal sehen, ob keiner runterf\u00E4llt.") },
                                                     new string[] { I18n.T("Esto lo hago con los ojos cerrados.", "I can do this with my eyes closed.", "Das mache ich sogar mit geschlossenen Augen."), I18n.T("No te despistes, que vuelan.", "Don't look away, they're flying.", "Gib nicht acht, sie fliegen sowieso.") });
                case MascotCmd.Dance: return By(personality, new string[] { I18n.T("\u00A1M\u00FAsica, maestro!", "Music, maestro!", "Musik, Maestro!"), I18n.T("\u00A1A bailar!", "Let's dance!", "Lass uns tanzen!") },
                                                     new string[] { I18n.T("Un bailecito suave.", "A gentle dance.", "Ein sanfter Tanz."), I18n.T("Muevo el esqueleto despacito.", "Moving the bones slowly.", "Ich schwing langsam die H\u00FCften.") },
                                                     new string[] { I18n.T("Mira estos pasos.", "Check out these moves.", "Guck dir diese Moves an."), I18n.T("Tiembla, Fred Astaire.", "Watch out, Fred Astaire.", "Zitter, Fred Astaire.") });
                case MascotCmd.Sleep: return By(personality, new string[] { I18n.T("Aaah\u2026 hasta luego.", "Aaah\u2026 see you later.", "Aaah\u2026 bis sp\u00E4ter."), I18n.T("\u00A1Buenas noches!", "Good night!", "Gute Nacht!") },
                                                     new string[] { I18n.T("Qu\u00E9 sue\u00F1ecito\u2026 zzz.", "So sleepy\u2026 zzz.", "So m\u00FCde\u2026 zzz."), I18n.T("Cierro los ojos un rato.", "Closing my eyes for a bit.", "Ich mach die Augen kurz zu.") },
                                                     new string[] { I18n.T("Despi\u00E9rtame si hay churros.", "Wake me up if there's churros.", "Weck mich, wenn's Churros gibt."), I18n.T("Ni se te ocurra tocarme.", "Don't you dare touch me.", "Fass mich blo\u00DF nicht an.") });
                case MascotCmd.Jump: return By(personality, new string[] { I18n.T("\u00A1Hop, hop, hop!", "Hop, hop, hop!", "Hopp, hopp, hopp!"), I18n.T("\u00A1Arriba!", "Up!", "Auf!") },
                                                     new string[] { I18n.T("Un saltito, nada m\u00E1s.", "Just a little hop.", "Nur ein kleiner H\u00FCpfer."), I18n.T("Hop. Hop. Hop.", "Hop. Hop. Hop.", "Hopp. Hopp. Hopp.") },
                                                     new string[] { I18n.T("\u00BFM\u00E1s alto? \u00A1Mira!", "Higher? Watch this!", "H\u00F6her? Schau her!"), I18n.T("Esto es f\u00E1cil.", "Easy peasy.", "Ganz easy.") });
                case MascotCmd.Twirl: return By(personality, new string[] { I18n.T("\u00A1Weee!", "Weee!", "Wiii!"), I18n.T("\u00A1Ta-ch\u00E1n!", "Ta-da!", "Ta-daa!") },
                                                     new string[] { I18n.T("Una vueltecita.", "A little twirl.", "Eine kleine Drehung."), I18n.T("Giro con calma.", "Spinning gently.", "Sachte gedreht.") },
                                                     new string[] { I18n.T("\u00A1Y sin marearme!", "And not even dizzy!", "Und noch nicht mal schwindlig!"), I18n.T("Aplauso, por favor.", "Applause, please.", "Applaus, bitte.") });
                case MascotCmd.Wave: return By(personality, new string[] { I18n.T("\u00A1Hola, hola!", "Hello, hello!", "Hallo, hallo!"), I18n.T("\u00A1Buenas!", "Hi there!", "Hi!") },
                                                     new string[] { I18n.T("Hola, qu\u00E9 tal.", "Hello, how's it going.", "Hallo, wie geht's."), I18n.T("Un saludo tranquilo.", "A calm greeting.", "Ein ruhiger Gru\u00DF.") },
                                                     new string[] { I18n.T("S\u00ED, te he visto.", "Yeah, I see you.", "Ja, ich hab dich gesehen."), I18n.T("\u00BFQu\u00E9 pasa, jefe?", "What's up, boss?", "Was geht, Chef?") });
                case MascotCmd.Flip: return By(personality, new string[] { I18n.T("\u00A1Voltereta!", "Somersault!", "Purzelchen!"), I18n.T("\u00A1Mortal atr\u00E1s!", "Backflip!", "R\u00FCckw\u00E4rtssalto!") },
                                                     new string[] { I18n.T("Ah\u00ED va, con cuidado.", "Here goes, carefully.", "So, ganz vorsichtig."), I18n.T("Una voltereta suave.", "A gentle somersault.", "Ein sanfter Salto.") },
                                                     new string[] { I18n.T("\u00BFEso sabe hacerlo tu rat\u00F3n?", "Can your mouse do that?", "Kann deine Maus das?"), I18n.T("Diez de diez.", "Ten out of ten.", "Glatt zehn von zehn.") });
                case MascotCmd.Bow: return By(personality, new string[] { I18n.T("Muchas gracias, muchas gracias.", "Thank you so much, thank you!", "Vielen Dank, vielen Dank!"), I18n.T("\u00A1El p\u00FAblico me adora!", "The audience loves me!", "Das Publikum liebt mich!") },
                                                     new string[] { I18n.T("Con todos mis respetos.", "With all due respect.", "Mit allem geb\u00FChrenden Respekt."), I18n.T("Un gesto cort\u00E9s.", "A polite gesture.", "Eine h\u00F6fliche Verbeugung.") },
                                                     new string[] { I18n.T("Hagan cola para el aut\u00F3grafo.", "Line up for autographs.", "Stellt euch f\u00FCr Autogramme an."), I18n.T("Gracias, soy yo.", "Thank you, it's all me.", "Danke, ja, ich war's.") });
                case MascotCmd.Cheer: return By(personality, new string[] { I18n.T("\u00A1Hurra!", "Hooray!", "Hurra!"), I18n.T("\u00A1Fiesta, fiesta!", "Party time!", "Party, Party!"), I18n.T("\u00A1Lo has conseguido!", "You did it!", "Du hast es geschafft!") },
                                                     new string[] { I18n.T("Muy bien hecho.", "Very well done.", "Sehr gut gemacht."), I18n.T("Hay motivos para sonre\u00EDr.", "Good reason to smile.", "Ein Grund zum L\u00E4cheln.") },
                                                     new string[] { I18n.T("\u00A1Eso es! \u00A1M\u00E1s confeti!", "That's it! More confetti!", "Genau so! Mehr Konfetti!"), I18n.T("\u00A1Que suene la bocina!", "Blow the horn!", "Lass die Hupe ert\u00F6nen!") });
                case MascotCmd.Love: return By(personality, new string[] { I18n.T("\u00A1Te quiero un mont\u00F3n!", "I love you so much!", "Ich hab dich so lieb!"), I18n.T("\u00A1Qu\u00E9 bonito!", "How sweet!", "Wie s\u00FC\u00DF!") },
                                                     new string[] { I18n.T("Qu\u00E9 gusto.", "How wonderful.", "Wie sch\u00F6n."), I18n.T("Gracias por estar.", "Thank you for being here.", "Danke, dass du da bist.") },
                                                     new string[] { I18n.T("Vale, un poquito s\u00ED.", "Okay, maybe a little.", "Okay, ein kleines bisschen schon."), I18n.T("No se lo cuentes a nadie.", "Don't tell anyone.", "Sag's keinem weiter.") });
                case MascotCmd.Bubbles: return By(personality, new string[] { I18n.T("\u00A1Pompas!", "Bubbles!", "Seifenblasen!"), I18n.T("\u00A1Mira c\u00F3mo brillan!", "Look how they shine!", "Guck, wie sie schimmern!") },
                                                     new string[] { I18n.T("Pompas y silencio.", "Bubbles and quiet.", "Seifenblasen und Stille."), I18n.T("Qu\u00E9 relajante.", "So relaxing.", "So entspannend.") },
                                                     new string[] { I18n.T("No las explotes t\u00FA.", "Don't pop them.", "Zerplatz sie nicht."), I18n.T("Esta es la m\u00E1s grande.", "This one is the biggest.", "Die hier ist die gr\u00F6\u00DFte.") });
                default: return null;
            }
        }

        // Reactions to being moved around.
        public static string Fall(Settings s)
        {
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Aaaah!", "Aaaah!", "Aaaah!"), I18n.T("\u00A1Cuidado!", "Careful!", "Vorsicht!"), I18n.T("\u00A1Que me caigo!", "I'm falling!", "Ich falle!"), I18n.T("\u00A1Socorro!", "Help!", "Hilfe!") },
                          new string[] { I18n.T("Vaya\u2026", "Oh dear\u2026", "Ohje\u2026"), I18n.T("Ay, ay, ay.", "Uh-oh, uh-oh.", "Oh weh, oh weh."), I18n.T("Qu\u00E9 remedio.", "Here we go.", "Was soll's.") },
                          new string[] { I18n.T("\u00A1Me han quitado el suelo!", "They took the floor away!", "Man hat mir den Boden weggezogen!"), I18n.T("\u00A1Esto es culpa tuya!", "This is your fault!", "Das ist deine Schuld!"), I18n.T("\u00A1Mis cosas!", "My stuff!", "Meine Sachen!") });
        }

        public static string Drag(Settings s)
        {
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Uoooh!", "Whoooa!", "Uoooh!"), I18n.T("\u00A1Vamos all\u00E1!", "Here we go!", "Los geht's!"), I18n.T("\u00A1Qu\u00E9 vistas!", "What a view!", "Was f\u00FCr eine Aussicht!"), I18n.T("\u00A1Wiii!", "Weee!", "Wiii!") },
                          new string[] { I18n.T("Con suavidad, por favor.", "Gently, please.", "Sanft, bitte."), I18n.T("Bien, bien.", "Alright, alright.", "Gut, gut."), I18n.T("Despacito.", "Nice and slow.", "Ganz langsam.") },
                          new string[] { I18n.T("\u00A1Eh, sin manos!", "Hey, hands off!", "Eh, H\u00E4nde weg!"), I18n.T("\u00BFA d\u00F3nde me llevas?", "Where are you taking me?", "Wo bringst du mich hin?"), I18n.T("Soy delicado, \u00BFeh?", "I'm delicate, you know?", "Ich bin empfindlich, wei\u00DFt du?") });
        }

        public static string Land(Settings s)
        {
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Aterrizaje perfecto!", "Perfect landing!", "Perfekte Landung!"), I18n.T("\u00A1Ta-ch\u00E1n!", "Ta-da!", "Ta-daa!"), I18n.T("\u00A1Tres puntos!", "Three-point landing!", "Dreipunktlandung!") },
                          new string[] { I18n.T("Mejor aqu\u00ED.", "Better here.", "Hier ist's besser."), I18n.T("Firme otra vez.", "Solid ground again.", "Wieder fester Boden."), I18n.T("Todo en su sitio.", "Everything in place.", "Alles an seinem Platz.") },
                          new string[] { I18n.T("Diez de diez.", "Ten out of ten.", "Glatt zehn von zehn."), I18n.T("Eso lo tengo controlado.", "Totally had that under control.", "Ich hatte das v\u00F6llig im Griff."), I18n.T("Ni un rasgu\u00F1o.", "Not a scratch.", "Kein einziger Kratzer.") });
        }

        public static string Climb(Settings s)
        {
            return By(s.MascotPersonality, new string[] { I18n.T("\u00A1Hop!", "Hop!", "Hopp!"), I18n.T("\u00A1Desde aqu\u00ED se ve todo!", "You can see everything from here!", "Von hier sieht man alles!"), I18n.T("\u00A1Arriba!", "Up!", "Auf!") },
                          new string[] { I18n.T("Buen sitio para pensar.", "Good spot for thinking.", "Ein guter Platz zum Nachdenken."), I18n.T("Un descansito aqu\u00ED.", "A nice rest here.", "Ein kleines Ruhen hier."), I18n.T("Qu\u00E9 vistas.", "What a view.", "Was f\u00FCr ein Ausblick.") },
                          new string[] { I18n.T("Ahora soy el rey de la ventana.", "Now I'm the king of the window.", "Jetzt bin ich der K\u00F6nig des Fensters."), I18n.T("Qu\u00E9 desorden hay aqu\u00ED abajo.", "Such a mess down there.", "Was ist da unten ein Durcheinander."), I18n.T("Esta ventana es m\u00EDa.", "This window is mine.", "Dieses Fenster geh\u00F6rt mir.") });
        }

        public static string LevelUp(Settings s, int level)
        {
            if (I18n.IsKorean) return string.Format(I18n.Localize("We are now {0}!"), MascotParts.LevelNames[level]);
            string lower = MascotParts.LevelNames[level].ToLowerInvariant();
            return I18n.T("\u00A1Ya somos " + lower + "!",
                          "We are now " + lower + "!",
                          "Jetzt sind wir " + MascotParts.LevelNames[level] + "!");
        }
    }
}
