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
            return line;
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
            return h < 6 ? "Good evening!" : h < 12 ? "Good morning!" : h < 18 ? "Good afternoon!" : "Good evening!";
        }

        public static string Hello(Settings s)
        {
            string g = Greeting();
            if (!I18n.IsSpanish)
            {
                switch (s.MascotPersonality)
                {
                    case 1: return g + Pick(new string[] { " Here I am, nice and calm.", " No rush, you know where to find me.", " All quiet over here." });
                    case 2: return g + Pick(new string[] { " Who are we capturing today?", " About time you showed up.", " What's the plan today, boss?" });
                    default: return g + Pick(new string[] { " What are we capturing?", " Ready for a great shot?", " So great to see you!" });
                }
            }
            switch (s.MascotPersonality)
            {
                case 1: return g + Pick(new string[] { " Aqu\u00ED estoy, tranquilo.", " Sin prisa, ya sabes d\u00F3nde encontrarme.", " Todo en calma por aqu\u00ED." });
                case 2: return g + Pick(new string[] { " \u00BFA qui\u00E9n capturamos hoy?", " Ya era hora de que aparecieras.", " \u00BFQu\u00E9 liamos hoy, jefe?" });
                default: return g + Pick(new string[] { " \u00BFQu\u00E9 capturamos?", " \u00BFListo para una buena captura?", " \u00A1Qu\u00E9 ganas ten\u00EDa de verte!" });
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
            if (!I18n.IsSpanish)
            {
                switch (s.MascotPersonality)
                {
                    case 1:
                        return new string[] { Pick(new string[] { "Mmm\u2026 hello.", "Take it easy, there's time.", "Breathe. Capture. Repeat.", "Press " + key + " whenever you want.",
                                                                  "A little poke, how nice.", "Still here, no rush.", "Everything at its own pace.", "Feels good to be here." }) };
                    case 2:
                        return new string[] { Pick(new string[] { "Hey! I'm not a button!", "You again?", "Poke me again and I'll start dancing.",
                                                                  "I take the coolest captures, just so you know.", "Press " + key + " and stop tickling me.",
                                                                  "Careful, I bite (a little bit).", "Bored? It shows.", "You owe me a capture.",
                                                                  "I'm an artist, not a toy.", "You really are annoying\u2026 I like it." }) };
                    default:
                        return new string[] { Pick(new string[] { "Hey, that tickles!", "Shall we take a capture?", "Press " + key + " and I'll handle it.",
                                                                  "Smile! Well\u2026 I'm already smiling.", "Beep boop. Beep.", "I love it when you curve the arrows.",
                                                                  "Hello, hello!", "Again! Again!", "Hehehe, stop, stop.", "What a great day for capturing!" }) };
                }
            }
            switch (s.MascotPersonality)
            {
                case 1:
                    return new string[] { Pick(new string[] { "Mmm\u2026 hola.", "Con calma, que hay tiempo.", "Respira. Captura. Repite.", "Pulsa " + key + " cuando quieras.",
                                                              "Un toquecito, qu\u00E9 detalle.", "Aqu\u00ED sigo, sin prisa.", "Todo a su ritmo.", "Qu\u00E9 bien se est\u00E1 aqu\u00ED." }) };
                case 2:
                    return new string[] { Pick(new string[] { "\u00A1Eh! \u00A1Que no soy un bot\u00F3n!", "\u00BFOtra vez t\u00FA?", "Si me pinchas m\u00E1s, me pongo a bailar.",
                                                              "Yo hago las capturas m\u00E1s chulas, que lo sepas.", "Pulsa " + key + " y no me hagas cosquillas.",
                                                              "Cuidado, que muerdo (un poquito).", "\u00BFTe aburres? Se nota.", "Me debes una captura.",
                                                              "Soy un artista, no un juguete.", "Mira que eres pesado\u2026 me gusta." }) };
                default:
                    return new string[] { Pick(new string[] { "\u00A1Eh, que me haces cosquillas!", "\u00BFHacemos una captura?", "Pulsa " + key + " y yo me encargo.",
                                                              "\u00A1Sonr\u00EDe! Bueno\u2026 yo ya lo hago.", "Bip bup. Bip.", "Me encanta cuando curvas las flechas.",
                                                              "\u00A1Hola, hola!", "\u00A1Otra vez! \u00A1Otra vez!", "Jijiji, para, para.", "\u00A1Qu\u00E9 d\u00EDa tan bueno para capturar!" }) };
            }
        }

        public static string Tip(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                string[] tipsEn =
                {
                    "Drag a thumbnail to any chat to paste it.",
                    "In the editor, drag the blue handle on an arrow to curve it.",
                    "Scroll over the stack with the mouse wheel to see previous captures.",
                    "When capturing, clicking a window captures it completely.",
                    "In the editor, Enter copies and closes.",
                    Hotkeys.Split(s.HotScroll).Count > 0 ? Hotkeys.Display(s.HotScroll) + " captures a whole page by scrolling it."
                                                         : "Scrolling capture takes a whole page; you can assign a shortcut in Shortcuts.",
                    Hotkeys.Split(s.HotText).Count > 0 ? Hotkeys.Display(s.HotText) + " copies text from anywhere on the screen, without saving a capture."
                                                       : "Copy screen text grabs text from anywhere; you can assign a shortcut in Shortcuts.",
                    Hotkeys.Split(s.HotRepeat).Count > 0 ? Hotkeys.Display(s.HotRepeat) + " captures the last area again, without selecting it."
                                                         : "\u201CRepeat area\u201D in the tray recaptures the last area; you can assign a shortcut in Shortcuts.",
                    "The Backdrop button in the editor gets your captures ready for presentation.",
                    "To cover private data, use Redact in the editor: a solid cover cannot be undone.",
                    "In the Mascot section you can change my hat, clothes and even species.",
                    "If you enable me on the desktop, I'll keep you company near the taskbar.",
                    "You can record your screen as video right from my menu.",
                    "If you drag me, you can place me on top of any window.",
                    "A quick double click on me and I'll do tricks.",
                    "Captures save automatically: view them via Open my captures."
                };
                return Pick(tipsEn);
            }
            string[] tips =
            {
                "Arrastra una miniatura a cualquier chat para pegarla.",
                "En el editor, tira del punto azul de una flecha para curvarla.",
                "Con la rueda del rat\u00F3n sobre la pila ves las capturas anteriores.",
                "Al capturar, un clic en una ventana la saca enterita.",
                "En el editor, Enter copia y cierra.",
                // A cleared combination would read "Sin atajo captura...": then the tip names the action instead.
                Hotkeys.Split(s.HotScroll).Count > 0 ? Hotkeys.Display(s.HotScroll) + " captura una p\u00E1gina entera desplaz\u00E1ndola."
                                                     : "La captura con desplazamiento saca una p\u00E1gina entera; en Atajos puedes darle una combinaci\u00F3n.",
                Hotkeys.Split(s.HotText).Count > 0 ? Hotkeys.Display(s.HotText) + " copia el texto de cualquier parte de la pantalla, sin guardar captura."
                                                   : "Copiar texto de la pantalla saca el texto de cualquier sitio; en Atajos puedes darle una combinaci\u00F3n.",
                Hotkeys.Split(s.HotRepeat).Count > 0 ? Hotkeys.Display(s.HotRepeat) + " vuelve a capturar la \u00FAltima \u00E1rea, sin elegirla otra vez."
                                                     : "\u00ABRepetir \u00E1rea\u00BB, en la bandeja, vuelve a capturar la \u00FAltima \u00E1rea; en Atajos puedes darle una combinaci\u00F3n.",
                "El bot\u00F3n Fondo del editor deja tus capturas listas para presentar.",
                "Para tapar datos, usa Tapar en el editor: un bloque s\u00F3lido no se puede deshacer.",
                "En la secci\u00F3n Mascota puedes cambiarme el gorro, la ropa y hasta la especie.",
                "Si me activas en el escritorio, te acompa\u00F1o junto a la barra de tareas.",
                "Puedes grabar la pantalla en v\u00EDdeo desde mi men\u00FA.",
                "Si me arrastras, me puedes dejar encima de cualquier ventana.",
                "Un doble clic r\u00E1pido sobre m\u00ED y me pongo a hacer cosas.",
                "Las capturas se guardan solas: \u00E1brelas desde Abrir mis capturas."
            };
            return Pick(tips);
        }

        public static string Celebrate(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality,
                    new string[] { "Great shot!", "Click! Looking sharp.", "Saving this one.", "Great eye!", "Perfect!", "Another one for the collection!" },
                    new string[] { "Nice capture.", "Saved to my memory.", "Very nice.", "That one has great light.", "All in place.", "Nicely framed." },
                    new string[] { "Boom!", "Now that is art!", "Straight to the museum!", "And I don't even charge royalties.", "That one deserves a frame.", "See? Told you so." });
            }
            return By(s.MascotPersonality,
                new string[] { "\u00A1Buena captura!", "\u00A1Clic! Ha quedado genial.", "Esa me la guardo.", "\u00A1Qu\u00E9 buen ojo!", "\u00A1Perfecta!", "\u00A1Otra para la colecci\u00F3n!" },
                new string[] { "Bonita captura.", "Guardada en mi memoria.", "Muy bien.", "Esa tiene buena luz.", "Todo en su sitio.", "Bien encuadrada." },
                new string[] { "\u00A1Toma ya!", "\u00A1Eso es arte!", "\u00A1Directa al museo!", "Y yo sin cobrar derechos.", "Esa va a enmarcarse.", "\u00BFVes? Te lo dije." });
        }

        // Spontaneous lines on the desktop: a mix of its voice, the time of day and the odd tip.
        public static string Idle(Settings s)
        {
            int h = DateTime.Now.Hour;
            double r = rnd.NextDouble();
            if (r < 0.18) return Tip(s);
            string own = Own(s, 0.35);
            if (own != null) return own;
            if (!I18n.IsSpanish)
            {
                if (r < 0.38)
                {
                    if (h >= 7 && h < 11) return By(s.MascotPersonality, new string[] { "Morning coffee time!", "Great time to start strong.", "Let's get this day going!" },
                        new string[] { "A quiet morning.", "Starting slowly is starting best.", "One day at a time." },
                        new string[] { "Had breakfast yet or what?", "Those eyes say \"needs coffee\".", "Early hours, early memes." });
                    if (h >= 13 && h < 16) return By(s.MascotPersonality, new string[] { "Had lunch yet?", "Nap time is getting close.", "Ready for the afternoon!" },
                        new string[] { "A short walk after eating.", "After-lunch break is sacred.", "Feeling sleepy\u2026" },
                        new string[] { "Where's my share of the food?", "After eating, not even bots are productive.", "Watch out for that food coma, boss." });
                    if (h >= 19 || h < 6) return By(s.MascotPersonality, new string[] { "Still working? Keep going!", "Almost done for the day.", "Look what time it is!" },
                        new string[] { "It's getting late. Get some rest.", "Eyes get tired too.", "Tomorrow is another day." },
                        new string[] { "Don't you have a home or something?", "Even I don't work at these hours.", "This counts as overtime." });
                }
                return By(s.MascotPersonality,
                    new string[] { "Hello! Still here.", "Need a capture?", "Going for a stroll.", "What a great day for capturing!", "Show me what you're working on?",
                                   "I'm right here if you need me.", "Beep boop, all systems go.", "Quick capture?", "Nice desktop you got there.", "I can record a video if you want." },
                    new string[] { "So peaceful\u2026", "Still here.", "Short walk and I'll be back.", "All quiet.", "It's nice here.", "No rush, no pause.",
                                   "Enjoying the quiet.", "Take a deep breath.", "Good moments don't make noise." },
                    new string[] { "Working or looking at memes?", "I'm bored\u2026 let's capture something?", "Careful, I'm watching you.", "Is that your best desktop setup?", "You owe me a capture.",
                                   "I'm not saying anything, but\u2026", "You have way too many tabs open.", "I've seen that window before.", "If you ignore me, I'll do weird things.", "Feeling photogenic today." });
            }
            if (r < 0.38)
            {
                if (h >= 7 && h < 11) return By(s.MascotPersonality, new string[] { "\u00A1Ese caf\u00E9 de la ma\u00F1ana!", "Buen momento para empezar fuerte.", "\u00A1Arrancamos el d\u00EDa!" },
                    new string[] { "La ma\u00F1ana va tranquila.", "Con calma se empieza mejor.", "Un d\u00EDa a la vez." },
                    new string[] { "\u00BFYa has desayunado o qu\u00E9?", "Esos ojos dicen \u00ABfalta caf\u00E9\u00BB.", "Primeras horas, primeros memes." });
                if (h >= 13 && h < 16) return By(s.MascotPersonality, new string[] { "\u00BFYa has comido?", "Se acerca la hora de la siesta.", "\u00A1A por la tarde!" },
                    new string[] { "Despu\u00E9s de comer, paseo corto.", "La sobremesa es sagrada.", "Qu\u00E9 sue\u00F1ecito\u2026" },
                    new string[] { "\u00BFY mi parte de la comida?", "Tras comer, ni los bots rinden.", "Ojo con la siesta, jefe." });
                if (h >= 19 || h < 6) return By(s.MascotPersonality, new string[] { "\u00BFA\u00FAn trabajando? \u00A1\u00C1nimo!", "Ya queda menos para cerrar.", "\u00A1Qu\u00E9 horas!" },
                    new string[] { "Ya es tarde. Descansa un poco.", "Los ojos tambi\u00E9n cansan.", "Ma\u00F1ana ser\u00E1 otro d\u00EDa." },
                    new string[] { "\u00BFNo tienes casa o qu\u00E9?", "Ni yo trabajo a estas horas.", "Esto ya son horas extra." });
            }
            return By(s.MascotPersonality,
                new string[] { "\u00A1Hola! Sigo por aqu\u00ED.", "\u00BFNecesitas una captura?", "Voy a dar una vuelta.", "\u00A1Qu\u00E9 buen d\u00EDa para capturar!", "\u00BFMe ense\u00F1as lo que haces?",
                               "Estoy aqu\u00ED por si me necesitas.", "Bip bup, todo en orden.", "\u00BFUna captura r\u00E1pida?", "Me gusta tu escritorio.", "Si quieres, grabo un v\u00EDdeo." },
                new string[] { "Qu\u00E9 paz\u2026", "Sigo aqu\u00ED.", "Un paseo corto y vuelvo.", "Todo tranquilo.", "Se est\u00E1 bien aqu\u00ED.", "Sin prisa, sin pausa.",
                               "Disfruto del silencio.", "Respira hondo.", "Los buenos momentos no hacen ruido." },
                new string[] { "\u00BFTrabajando o mirando memes?", "Me aburro\u2026 \u00BFcapturamos algo?", "Ojo, que te vigilo.", "\u00BFEse es tu mejor escritorio?", "Me debes una captura.",
                               "Yo no digo nada, pero\u2026", "Tienes demasiadas pesta\u00F1as abiertas.", "Esa ventana la he visto antes.", "Si me ignoras, hago cosas raras.", "Hoy me siento fotog\u00E9nico." });
        }

        // What it says when it starts doing something with its props (null: nothing this time).
        public static string ForAct(int personality, int act)
        {
            if (rnd.NextDouble() < 0.45) return null;
            if (!I18n.IsSpanish)
            {
                switch (act)
                {
                    case MascotProps.Coffee:
                        return By(personality, new string[] { "Coffee time!", "Coffee break.", "Mmm, smells so good." }, new string[] { "A quiet coffee.", "Sip by sip.", "What an aroma." }, new string[] { "Don't even think about asking.", "Coffee for me, not for you.", "My fifth one today." });
                    case MascotProps.Juggle:
                        return By(personality, new string[] { "Look, look!", "Three at once!", "No hands! Well, with hands." }, new string[] { "Just a bit of practice.", "Rhythm is what counts.", "Focus." }, new string[] { "Bet you can't do this!", "Admission isn't free, you know.", "Careful, might drop one." });
                    case MascotProps.Console:
                        return By(personality, new string[] { "A bit of gaming!", "New high score!", "Quick match." }, new string[] { "Just one level.", "Casual gaming.", "No rush, infinite lives." }, new string[] { "Don't talk to me, I'm winning.", "Fourth life!", "This isn't work, this is culture." });
                    case MascotProps.Stretch:
                        return By(personality, new string[] { "Time to stretch!", "Phew, feels so good.", "Crack, crack!" }, new string[] { "Stretching is resting too.", "Aaah\u2026", "A little movement." }, new string[] { "Even bots get stiff.", "Look at that flexibility.", "All that sitting down\u2026" });
                    case MascotProps.Read:
                        return By(personality, new string[] { "What a great book!", "Just one chapter.", "Look at that ending!" }, new string[] { "A good book and silence.", "Chapter by chapter.", "Reading for a bit." }, new string[] { "No spoilers.", "Shhh, reaching the climax.", "Better than your desktop." });
                    case MascotProps.Water:
                        return By(personality, new string[] { "Fresh water!", "Cheers to you!", "Gulp, gulp, gulp." }, new string[] { "A little water does wonders.", "Sip by sip.", "Hydration is self-care." }, new string[] { "Water, the drink of champions.", "Cheers? You too.", "No coffee or soda: just water." });
                    case MascotProps.EyeRest:
                        return By(personality, new string[] { "Everything looks so far away!", "Gazing at the horizon.", "Way over there!" }, new string[] { "Eyes need rest too.", "Looking far away relaxes.", "Calm for the sight." }, new string[] { "I spy\u2026 something distant.", "Your screen can wait.", "Look, a bird. Or not." });
                    case MascotProps.Legs:
                        return By(personality, new string[] { "Up and down!", "One, two!", "Let's go, legs!" }, new string[] { "Slowly, no rush.", "Moving your legs feels good.", "Up\u2026 and down." }, new string[] { "Better than the gym.", "Don't copy me, you'll get tired.", "Look at these legs!" });
                    case MascotProps.Breathe:
                        return By(personality, new string[] { "Breathe with me.", "In\u2026 and out.", "Breathing feels amazing!" }, new string[] { "Breathe in\u2026 release.", "Everything at its pace.", "A moment of calm." }, new string[] { "Breathing is free, enjoy it.", "Even a bot needs air.", "Deep breath, I won't charge." });
                    default:
                        return By(personality, new string[] { "Smile!", "Cheese!", "Click!" }, new string[] { "A nice memory.", "Perfect lighting.", "Hold still a second." }, new string[] { "I look better.", "Frame-worthy.", "Say cheese." });
                }
            }
            switch (act)
            {
                case MascotProps.Coffee:
                    return By(personality, new string[] { "\u00A1Un caf\u00E9!", "Pausa para el caf\u00E9.", "Mmm, qu\u00E9 rico." }, new string[] { "Un cafecito con calma.", "Sorbo a sorbo.", "Qu\u00E9 aroma." }, new string[] { "Ni se te ocurra pedirme.", "Caf\u00E9 para m\u00ED, no para ti.", "Mi quinto de hoy." });
                case MascotProps.Juggle:
                    return By(personality, new string[] { "\u00A1Mira, mira!", "\u00A1Tres a la vez!", "\u00A1Sin manos! Bueno, con ellas." }, new string[] { "Un poco de pr\u00E1ctica.", "El ritmo es lo que cuenta.", "Concentraci\u00F3n." }, new string[] { "\u00BFA que no puedes t\u00FA?", "Cobro entrada, eh.", "Ojo, que se me caen." });
                case MascotProps.Console:
                    return By(personality, new string[] { "\u00A1Un ratito de juego!", "\u00A1Nuevo r\u00E9cord!", "Una partidita r\u00E1pida." }, new string[] { "Un nivelito y ya.", "Juego tranquilo.", "Sin prisas, sin vidas." }, new string[] { "Ni me hables, voy ganando.", "\u00A1Cuarta vida!", "Esto no es trabajo, es cultura." });
                case MascotProps.Stretch:
                    return By(personality, new string[] { "\u00A1A estirar!", "Ufff, qu\u00E9 bien sienta.", "\u00A1Cr\u00E1c, cr\u00E1c!" }, new string[] { "Estirar tambi\u00E9n es descansar.", "Aaah\u2026", "Un poco de movimiento." }, new string[] { "Hasta los bots se anquilosan.", "Mira qu\u00E9 flexibilidad.", "Tanto estar sentado\u2026" });
                case MascotProps.Read:
                    return By(personality, new string[] { "\u00A1Qu\u00E9 libro m\u00E1s bueno!", "Un capitulito.", "\u00A1Mira qu\u00E9 final!" }, new string[] { "Un buen libro y silencio.", "Cap\u00EDtulo a cap\u00EDtulo.", "Leyendo un rato." }, new string[] { "No hagas spoilers.", "Shhh, que llego al final.", "Mejor que tu escritorio." });
                case MascotProps.Water:
                    return By(personality, new string[] { "\u00A1Ag\u00FCita fresca!", "\u00A1A tu salud!", "Glu, glu, glu." }, new string[] { "Un poco de agua sienta bien.", "Sorbo a sorbo.", "Hidratarse es cuidarse." }, new string[] { "Agua, el refresco de los campeones.", "\u00BFBrindamos? T\u00FA tambi\u00E9n.", "Ni caf\u00E9 ni nada: agua." });
                case MascotProps.EyeRest:
                    return By(personality, new string[] { "\u00A1Qu\u00E9 lejos se ve todo!", "Miro el horizonte.", "\u00A1All\u00ED a lo lejos!" }, new string[] { "Los ojos tambi\u00E9n descansan.", "Mirar lejos relaja.", "Calma para la vista." }, new string[] { "Veo, veo\u2026 algo lejano.", "Tu pantalla puede esperar.", "Mira, un p\u00E1jaro. O no." });
                case MascotProps.Legs:
                    return By(personality, new string[] { "\u00A1Arriba y abajo!", "\u00A1Una, dos!", "\u00A1Vamos, piernas!" }, new string[] { "Despacito, sin prisa.", "Mover las piernas sienta bien.", "Arriba\u2026 y abajo." }, new string[] { "Esto es mejor que el gimnasio.", "No me copies, que te canso.", "\u00A1Mira qu\u00E9 piernas!" });
                case MascotProps.Breathe:
                    return By(personality, new string[] { "Respira conmigo.", "Dentro\u2026 y fuera.", "\u00A1Qu\u00E9 bien se respira!" }, new string[] { "Inspira\u2026 suelta.", "Todo a su ritmo.", "Un poco de calma." }, new string[] { "Respirar es gratis, aprovecha.", "Hasta un bot necesita aire.", "Inspira hondo, que no cobro." });
                default:
                    return By(personality, new string[] { "\u00A1Sonr\u00EDe!", "\u00A1Patata!", "\u00A1Clic!" }, new string[] { "Un buen recuerdo.", "Luz perfecta.", "Quieto un segundo." }, new string[] { "Salgo mejor yo.", "Esta va al marco.", "Di \u00ABwhisky\u00BB." });
            }
        }

        // ---- Healthy breaks: the reminder, what it says while you do it, and the thanks.

        public static string Remind(Settings s, int kind, int minutes)
        {
            if (!I18n.IsSpanish)
            {
                string tEn = minutes >= 60 ? (minutes % 60 == 0 ? (minutes / 60 == 1 ? "an hour" : minutes / 60 + " hours") : "an hour and a half") : minutes + " minutes";
                switch (kind)
                {
                    case WellKind.Water:
                        return By(s.MascotPersonality,
                            new string[] { "Time to drink water! It's been " + tEn + " without a sip.", "A glass of water? I'll have one with you.", "Fresh water! Your body will thank you." },
                            new string[] { "A little water will do you good.", "You've been working for " + tEn + ". Shall we drink water?", "A glass of water and we continue." },
                            new string[] { "Water? Ring a bell? It's been " + tEn + ".", "If you don't drink it, I will.", "Plants that aren't watered dry out." });
                    case WellKind.Eyes:
                        return By(s.MascotPersonality,
                            new string[] { "Rest your eyes! Look at something far away for 20 seconds.", "Your eyes have been glued to the screen for " + tEn + ". Look away?", "20-20-20 rule: look 20 feet away for 20 seconds." },
                            new string[] { "Give your eyes a break: gaze into the distance for a moment.", "20 seconds looking far away. No rush.", "Your eyes deserve a pause." },
                            new string[] { "That screen isn't going anywhere. Look out the window.", "Blink, you're forgetting.", "Your eyes are begging for a vacation. Twenty seconds." });
                    case WellKind.Move:
                        return By(s.MascotPersonality,
                            new string[] { "Up! You've been in that chair for " + tEn + ". Let's stretch those legs!", "Time to move! Stand up for a moment.", "Short walk? Your legs are asking for it." },
                            new string[] { "Stand up and stretch your body a bit.", "A minute on your feet feels wonderful.", "You've been sitting for " + tEn + ". Move around a bit." },
                            new string[] { "Your chair is going to charge rent.", "If you don't stand up, I'll start dancing.", "Are you glued to that chair? Up!" });
                    default:
                        return By(s.MascotPersonality,
                            new string[] { "Shall we breathe together for a minute?", "A breathing break will feel great.", "Six slow breaths and you're good as new." },
                            new string[] { "Take a deep breath with me for a moment.", "A minute of calm: let's breathe.", "Breathe in, let go. Simple as that." },
                            new string[] { "Breathe, I can see the stress.", "One minute without emails. Breathe.", "Even bots breathe. Well, almost." });
                }
            }
            string t = minutes >= 60 ? (minutes % 60 == 0 ? (minutes / 60 == 1 ? "una hora" : minutes / 60 + " horas") : "hora y media") : minutes + " minutos";
            switch (kind)
            {
                case WellKind.Water:
                    return By(s.MascotPersonality,
                        new string[] { "\u00A1Hora de beber agua! Llevas " + t + " sin un trago.", "\u00BFUn vaso de agua? Yo me tomo uno contigo.", "\u00A1Ag\u00FCita! Tu cuerpo te lo agradecer\u00E1." },
                        new string[] { "Un poco de agua te sentar\u00E1 bien.", "Llevas " + t + " trabajando. \u00BFBebemos agua?", "Un vaso de agua y seguimos." },
                        new string[] { "\u00BFAgua? \u00BFTe suena? Llevas " + t + ".", "Si no bebes agua, me la bebo yo.", "Plantita que no se riega, se seca." });
                case WellKind.Eyes:
                    return By(s.MascotPersonality,
                        new string[] { "\u00A1Descansa la vista! Mira algo lejano 20 segundos.", "Tus ojos llevan " + t + " pegados a la pantalla. \u00BFMiramos lejos?", "Regla 20-20-20: mira a 6 metros durante 20 segundos." },
                        new string[] { "Deja que tus ojos descansen: mira a lo lejos un momento.", "20 segundos mirando lejos. Sin prisa.", "Tus ojos merecen una pausa." },
                        new string[] { "Esa pantalla no se va a escapar. Mira por la ventana.", "Parpadea, que se te olvida.", "Tus ojos piden vacaciones. Veinte segundos." });
                case WellKind.Move:
                    return By(s.MascotPersonality,
                        new string[] { "\u00A1Arriba! Llevas " + t + " en la silla. \u00A1Estiramos las piernas!", "\u00A1Hora de moverse! Lev\u00E1ntate un momento.", "\u00BFDamos un pase\u00EDto? Tus piernas lo piden." },
                        new string[] { "Lev\u00E1ntate y estira un poco el cuerpo.", "Un minuto de pie sienta de maravilla.", "Llevas " + t + " en la silla. Mu\u00E9vete un poco." },
                        new string[] { "Tu silla te va a cobrar alquiler.", "Si no te levantas, me pongo a bailar.", "\u00BFTe has pegado a la silla? Arriba." });
                default:
                    return By(s.MascotPersonality,
                        new string[] { "\u00BFRespiramos juntos un minuto?", "Una pausa para respirar te va a encantar.", "Seis respiraciones lentas y como nuevo." },
                        new string[] { "Respira hondo conmigo un momento.", "Un minuto de calma: respiremos.", "Inspira, suelta. As\u00ED de simple." },
                        new string[] { "Respira, que se te nota el estr\u00E9s.", "Un minuto sin correos. Respira.", "Hasta los bots respiramos. Bueno, casi." });
            }
        }

        public static string WellDone(Settings s, int kind, int today)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality, new string[] { "Awesome! ", "Great job! ", "That's what I like! " }, new string[] { "Well done. ", "Perfect. ", "Very nice. " },
                          new string[] { "Look at that discipline. ", "Not bad at all. ", "Bravo. " }) + WellCount(kind, today, s.WaterGoal);
            }
            return By(s.MascotPersonality, new string[] { "\u00A1Genial! ", "\u00A1Muy bien! ", "\u00A1As\u00ED me gusta! " }, new string[] { "Bien hecho. ", "Perfecto. ", "Qu\u00E9 bien. " },
                      new string[] { "Mira t\u00FA qu\u00E9 disciplina. ", "No est\u00E1 mal. ", "Ole. " }) + WellCount(kind, today, s.WaterGoal);
        }

        // How many of that break today, said plainly (also when the mascot doesn't chat).
        public static string WellCount(int kind, int today) { return WellCount(kind, today, 0); }

        // waterGoal: the glasses a day aimed at (0 none), said with the water count.
        public static string WellCount(int kind, int today, int waterGoal)
        {
            if (!I18n.IsSpanish)
            {
                if (kind == WellKind.Water && waterGoal > 0)
                    return today >= waterGoal ? (today == waterGoal ? "Water goal reached: " + waterGoal + " glasses!" : today + " glasses today: goal achieved!")
                         : today == 1 ? "Your first glass today (out of " + waterGoal + ")." : today + " of " + waterGoal + " glasses today.";
                return kind == WellKind.Water ? (today == 1 ? "Your first glass today." : today + " glasses today.")
                     : kind == WellKind.Eyes ? (today == 1 ? "First eye rest today." : today + " eye rests today.")
                     : kind == WellKind.Move ? (today == 1 ? "First movement break today." : today + " movement breaks today.")
                     : (today == 1 ? "First breathing session today." : today + " breathing sessions today.");
            }
            if (kind == WellKind.Water && waterGoal > 0)
                return today >= waterGoal ? (today == waterGoal ? "\u00A1Objetivo de agua cumplido: " + waterGoal + " vasos!" : "Ya van " + today + " vasos hoy: objetivo cumplido.")
                     : today == 1 ? "Tu primer vaso de hoy (de " + waterGoal + ")." : "Ya van " + today + " de " + waterGoal + " vasos hoy.";
            return kind == WellKind.Water ? (today == 1 ? "Tu primer vaso de hoy." : "Ya van " + today + " vasos hoy.")
                 : kind == WellKind.Eyes ? (today == 1 ? "Primer descanso de vista de hoy." : today + " descansos de vista hoy.")
                 : kind == WellKind.Move ? (today == 1 ? "Primera pausa para moverte." : today + " pausas para moverte hoy.")
                 : (today == 1 ? "Primera respiraci\u00F3n de hoy." : today + " respiraciones hoy.");
        }

        // Answers to "Recu\u00E9rdamelo en 10 min" and "Hoy no": they always say what was decided, chatty or not.
        public static string WellLater(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                if (!s.MascotTalks) return "I'll remind you in 10 minutes.";
                return By(s.MascotPersonality, new string[] { "Got it! I'll remind you in 10 minutes.", "Alright! Reminding you in 10 minutes." }, new string[] { "No problem: I'll notify you in 10 minutes.", "I'll remind you in 10 minutes." },
                          new string[] { "Fine, 10 minutes and you can't escape.", "I'll be back in 10 minutes, okay?" });
            }
            if (!s.MascotTalks) return "Te lo recuerdo en 10 minutos.";
            return By(s.MascotPersonality, new string[] { "\u00A1Vale! Te aviso en 10 minutos.", "\u00A1De acuerdo! En 10 minutos te lo recuerdo." }, new string[] { "Sin problema: en 10 minutos te aviso.", "Te lo recuerdo en 10 minutos." },
                      new string[] { "Vale, 10 minutos y no te escapas.", "En 10 minutos vuelvo, \u00BFeh?" });
        }

        public static string WellToday(Settings s, int kind)
        {
            if (!I18n.IsSpanish)
            {
                string whatEn = kind == WellKind.Water ? "water reminders" : kind == WellKind.Eyes ? "eye rest reminders" : kind == WellKind.Move ? "movement breaks" : "breathing breaks";
                if (!s.MascotTalks) return "No more " + whatEn + " today.";
                return By(s.MascotPersonality, new string[] { "Got it! No more " + whatEn + " today." }, new string[] { "Alright, no more " + whatEn + " for today." },
                          new string[] { "Fine, you're off the hook for " + whatEn + " today." });
            }
            string what = kind == WellKind.Water ? "lo del agua" : kind == WellKind.Eyes ? "lo de la vista" : kind == WellKind.Move ? "lo de moverte" : "lo de respirar";
            if (!s.MascotTalks) return "Hoy no te recuerdo m\u00E1s " + what + ".";
            return By(s.MascotPersonality, new string[] { "\u00A1Vale! Hoy no te recuerdo m\u00E1s " + what + "." }, new string[] { "De acuerdo, por hoy no te recuerdo m\u00E1s " + what + "." },
                      new string[] { "Vale, hoy te libras de " + what + "." });
        }

        // What it says when told to do something (-1: woken up on request).
        public static string ForCmd(int personality, int cmd)
        {
            if (!I18n.IsSpanish)
            {
                switch (cmd)
                {
                    case -1: return By(personality, new string[] { "I'm awake!", "Good morning again!" }, new string[] { "Mmm\u2026 coming.", "Just another minute and I'm up." }, new string[] { "Hey! I was having a great dream.", "Already? Come on." });
                    case MascotCmd.Play: return By(personality, new string[] { "Let's play!", "Yeees, let's play!", "I was so hoping for this!" }, new string[] { "A little playtime, sure.", "Let's play, no rush." }, new string[] { "Now you'll see!", "I challenge you to see who lasts longer." });
                    case MascotCmd.Juggle: return By(personality, new string[] { "Look, look!", "Three balls! Here I go!" }, new string[] { "With rhythm and steady pace.", "Let's see if I don't drop any." }, new string[] { "I can do this with my eyes closed.", "Don't look away, they're flying." });
                    case MascotCmd.Dance: return By(personality, new string[] { "Music, maestro!", "Let's dance!" }, new string[] { "A gentle dance.", "Moving the bones slowly." }, new string[] { "Check out these moves.", "Watch out, Fred Astaire." });
                    case MascotCmd.Sleep: return By(personality, new string[] { "Aaah\u2026 see you later.", "Good night!" }, new string[] { "So sleepy\u2026 zzz.", "Closing my eyes for a bit." }, new string[] { "Wake me up if there's churros.", "Don't you dare touch me." });
                    case MascotCmd.Jump: return By(personality, new string[] { "Hop, hop, hop!", "Up!" }, new string[] { "Just a little hop.", "Hop. Hop. Hop." }, new string[] { "Higher? Watch this!", "Easy peasy." });
                    case MascotCmd.Twirl: return By(personality, new string[] { "Weee!", "Ta-da!" }, new string[] { "A little twirl.", "Spinning gently." }, new string[] { "And not even dizzy!", "Applause, please." });
                    case MascotCmd.Wave: return By(personality, new string[] { "Hello, hello!", "Hi there!" }, new string[] { "Hello, how's it going.", "A calm greeting." }, new string[] { "Yeah, I see you.", "What's up, boss?" });
                    case MascotCmd.Flip: return By(personality, new string[] { "Somersault!", "Backflip!" }, new string[] { "Here goes, carefully.", "A gentle somersault." }, new string[] { "Can your mouse do that?", "Ten out of ten." });
                    case MascotCmd.Bow: return By(personality, new string[] { "Thank you so much, thank you!", "The audience loves me!" }, new string[] { "With all due respect.", "A polite gesture." }, new string[] { "Line up for autographs.", "Thank you, it's all me." });
                    case MascotCmd.Cheer: return By(personality, new string[] { "Hooray!", "Party time!", "You did it!" }, new string[] { "Very well done.", "Good reason to smile." }, new string[] { "That's it! More confetti!", "Blow the horn!" });
                    case MascotCmd.Love: return By(personality, new string[] { "I love you so much!", "How sweet!" }, new string[] { "How wonderful.", "Thank you for being here." }, new string[] { "Okay, maybe a little.", "Don't tell anyone." });
                    case MascotCmd.Bubbles: return By(personality, new string[] { "Bubbles!", "Look how they shine!" }, new string[] { "Bubbles and quiet.", "So relaxing." }, new string[] { "Don't pop them.", "This one is the biggest." });
                    default: return null;
                }
            }
            switch (cmd)
            {
                case -1: return By(personality, new string[] { "\u00A1Ya estoy despierto!", "\u00A1Buenos d\u00EDas otra vez!" }, new string[] { "Mmm\u2026 ya voy.", "Un ratito m\u00E1s y me levanto." }, new string[] { "\u00A1Eh! Estaba so\u00F1ando algo genial.", "\u00BFYa? Qu\u00E9 pesado." });
                case MascotCmd.Play: return By(personality, new string[] { "\u00A1A jugar!", "\u00A1Siii\u00ED, juguemos!", "\u00A1Qu\u00E9 ganas ten\u00EDa!" }, new string[] { "Un ratito de juego, vale.", "Jugamos sin prisa." }, new string[] { "\u00A1Ahora ver\u00E1s!", "Te reto a ver qui\u00E9n aguanta m\u00E1s." });
                case MascotCmd.Juggle: return By(personality, new string[] { "\u00A1Mira, mira!", "\u00A1Tres bolas! \u00A1All\u00E1 voy!" }, new string[] { "Con ritmo y sin prisa.", "A ver si no se cae ninguna." }, new string[] { "Esto lo hago con los ojos cerrados.", "No te despistes, que vuelan." });
                case MascotCmd.Dance: return By(personality, new string[] { "\u00A1M\u00FAsica, maestro!", "\u00A1A bailar!" }, new string[] { "Un bailecito suave.", "Muevo el esqueleto despacito." }, new string[] { "Mira estos pasos.", "Tiembla, Fred Astaire." });
                case MascotCmd.Sleep: return By(personality, new string[] { "Aaah\u2026 hasta luego.", "\u00A1Buenas noches!" }, new string[] { "Qu\u00E9 sue\u00F1ecito\u2026 zzz.", "Cierro los ojos un rato." }, new string[] { "Despi\u00E9rtame si hay churros.", "Ni se te ocurra tocarme." });
                case MascotCmd.Jump: return By(personality, new string[] { "\u00A1Hop, hop, hop!", "\u00A1Arriba!" }, new string[] { "Un saltito, nada m\u00E1s.", "Hop. Hop. Hop." }, new string[] { "\u00BFM\u00E1s alto? \u00A1Mira!", "Esto es f\u00E1cil." });
                case MascotCmd.Twirl: return By(personality, new string[] { "\u00A1Weee!", "\u00A1Ta-ch\u00E1n!" }, new string[] { "Una vueltecita.", "Giro con calma." }, new string[] { "\u00A1Y sin marearme!", "Aplauso, por favor." });
                case MascotCmd.Wave: return By(personality, new string[] { "\u00A1Hola, hola!", "\u00A1Buenas!" }, new string[] { "Hola, qu\u00E9 tal.", "Un saludo tranquilo." }, new string[] { "S\u00ED, te he visto.", "\u00BFQu\u00E9 pasa, jefe?" });
                case MascotCmd.Flip: return By(personality, new string[] { "\u00A1Voltereta!", "\u00A1Mortal atr\u00E1s!" }, new string[] { "Ah\u00ED va, con cuidado.", "Una voltereta suave." }, new string[] { "\u00BFEso sabe hacerlo tu rat\u00F3n?", "Diez de diez." });
                case MascotCmd.Bow: return By(personality, new string[] { "Muchas gracias, muchas gracias.", "\u00A1El p\u00FAblico me adora!" }, new string[] { "Con todos mis respetos.", "Un gesto cort\u00E9s." }, new string[] { "Hagan cola para el aut\u00F3grafo.", "Gracias, soy yo." });
                case MascotCmd.Cheer: return By(personality, new string[] { "\u00A1Hurra!", "\u00A1Fiesta, fiesta!", "\u00A1Lo has conseguido!" }, new string[] { "Muy bien hecho.", "Hay motivos para sonre\u00EDr." }, new string[] { "\u00A1Eso es! \u00A1M\u00E1s confeti!", "\u00A1Que suene la bocina!" });
                case MascotCmd.Love: return By(personality, new string[] { "\u00A1Te quiero un mont\u00F3n!", "\u00A1Qu\u00E9 bonito!" }, new string[] { "Qu\u00E9 gusto.", "Gracias por estar." }, new string[] { "Vale, un poquito s\u00ED.", "No se lo cuentes a nadie." });
                case MascotCmd.Bubbles: return By(personality, new string[] { "\u00A1Pompas!", "\u00A1Mira c\u00F3mo brillan!" }, new string[] { "Pompas y silencio.", "Qu\u00E9 relajante." }, new string[] { "No las explotes t\u00FA.", "Esta es la m\u00E1s grande." });
                default: return null;
            }
        }

        // Reactions to being moved around.
        public static string Fall(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality, new string[] { "Aaaah!", "Careful!", "I'm falling!", "Help!" }, new string[] { "Oh dear\u2026", "Uh-oh, uh-oh.", "Here we go." }, new string[] { "They took the floor away!", "This is your fault!", "My stuff!" });
            }
            return By(s.MascotPersonality, new string[] { "\u00A1Aaaah!", "\u00A1Cuidado!", "\u00A1Que me caigo!", "\u00A1Socorro!" }, new string[] { "Vaya\u2026", "Ay, ay, ay.", "Qu\u00E9 remedio." }, new string[] { "\u00A1Me han quitado el suelo!", "\u00A1Esto es culpa tuya!", "\u00A1Mis cosas!" });
        }

        public static string Drag(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality, new string[] { "Whoooa!", "Here we go!", "What a view!", "Weee!" }, new string[] { "Gently, please.", "Alright, alright.", "Nice and slow." }, new string[] { "Hey, hands off!", "Where are you taking me?", "I'm delicate, you know?" });
            }
            return By(s.MascotPersonality, new string[] { "\u00A1Uoooh!", "\u00A1Vamos all\u00E1!", "\u00A1Qu\u00E9 vistas!", "\u00A1Wiii!" }, new string[] { "Con suavidad, por favor.", "Bien, bien.", "Despacito." }, new string[] { "\u00A1Eh, sin manos!", "\u00BFA d\u00F3nde me llevas?", "Soy delicado, \u00BFeh?" });
        }

        public static string Land(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality, new string[] { "Perfect landing!", "Ta-da!", "Three-point landing!" }, new string[] { "Better here.", "Solid ground again.", "Everything in place." }, new string[] { "Ten out of ten.", "Totally had that under control.", "Not a scratch." });
            }
            return By(s.MascotPersonality, new string[] { "\u00A1Aterrizaje perfecto!", "\u00A1Ta-ch\u00E1n!", "\u00A1Tres puntos!" }, new string[] { "Mejor aqu\u00ED.", "Firme otra vez.", "Todo en su sitio." }, new string[] { "Diez de diez.", "Eso lo tengo controlado.", "Ni un rasgu\u00F1o." });
        }

        public static string Climb(Settings s)
        {
            if (!I18n.IsSpanish)
            {
                return By(s.MascotPersonality, new string[] { "Hop!", "You can see everything from here!", "Up!" }, new string[] { "Good spot for thinking.", "A nice rest here.", "What a view." }, new string[] { "Now I'm the king of the window.", "Such a mess down there.", "This window is mine." });
            }
            return By(s.MascotPersonality, new string[] { "\u00A1Hop!", "\u00A1Desde aqu\u00ED se ve todo!", "\u00A1Arriba!" }, new string[] { "Buen sitio para pensar.", "Un descansito aqu\u00ED.", "Qu\u00E9 vistas." }, new string[] { "Ahora soy el rey de la ventana.", "Qu\u00E9 desorden hay aqu\u00ED abajo.", "Esta ventana es m\u00EDa." });
        }

        public static string LevelUp(Settings s, int level)
        {
            return I18n.IsSpanish
                ? "\u00A1Ya somos " + MascotParts.LevelNames[level].ToLowerInvariant() + "!"
                : "We are now " + MascotParts.LevelNames[level].ToLowerInvariant() + "!";
        }
    }
}
