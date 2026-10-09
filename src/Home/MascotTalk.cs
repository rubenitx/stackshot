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
            return h < 6 ? "\u00A1Buenas noches!" : h < 14 ? "\u00A1Buenos d\u00EDas!" : h < 21 ? "\u00A1Buenas tardes!" : "\u00A1Buenas noches!";
        }

        public static string Hello(Settings s)
        {
            string g = Greeting();
            switch (s.MascotPersonality)
            {
                case 1: return g + Pick(new string[] { " Aqu\u00ED estoy, tranquilo.", " Sin prisa, ya sabes d\u00F3nde encontrarme.", " Todo en calma por aqu\u00ED." });
                case 2: return g + Pick(new string[] { " \u00BFA qui\u00E9n capturamos hoy?", " Ya era hora de que aparecieras.", " \u00BFQu\u00E9 liamos hoy, jefe?" });
                default: return g + Pick(new string[] { " \u00BFQu\u00E9 capturamos?", " \u00BFListo para una buena captura?", " \u00A1Qu\u00E9 ganas ten\u00EDa de verte!" });
            }
        }

        public static string[] Pokes(Settings s)
        {
            string key = Hotkeys.Display(s.HotRegion);
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
            string[] tips =
            {
                "Arrastra una miniatura a cualquier chat para pegarla.",
                "En el editor, tira del punto azul de una flecha para curvarla.",
                "Con la rueda del rat\u00F3n sobre la pila ves las capturas anteriores.",
                "Al capturar, un clic en una ventana la saca enterita.",
                "En el editor, Enter copia y cierra.",
                Hotkeys.Display(s.HotScroll) + " captura una p\u00E1gina entera desplaz\u00E1ndola.",
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
                default:
                    return By(personality, new string[] { "\u00A1Sonr\u00EDe!", "\u00A1Patata!", "\u00A1Clic!" }, new string[] { "Un buen recuerdo.", "Luz perfecta.", "Quieto un segundo." }, new string[] { "Salgo mejor yo.", "Esta va al marco.", "Di \u00ABwhisky\u00BB." });
            }
        }

        // What it says when told to do something (-1: woken up on request).
        public static string ForCmd(int personality, int cmd)
        {
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
            return By(s.MascotPersonality, new string[] { "\u00A1Aaaah!", "\u00A1Cuidado!", "\u00A1Que me caigo!", "\u00A1Socorro!" }, new string[] { "Vaya\u2026", "Ay, ay, ay.", "Qu\u00E9 remedio." }, new string[] { "\u00A1Me han quitado el suelo!", "\u00A1Esto es culpa tuya!", "\u00A1Mis cosas!" });
        }

        public static string Drag(Settings s)
        {
            return By(s.MascotPersonality, new string[] { "\u00A1Uoooh!", "\u00A1Vamos all\u00E1!", "\u00A1Qu\u00E9 vistas!", "\u00A1Wiii!" }, new string[] { "Con suavidad, por favor.", "Bien, bien.", "Despacito." }, new string[] { "\u00A1Eh, sin manos!", "\u00BFA d\u00F3nde me llevas?", "Soy delicado, \u00BFeh?" });
        }

        public static string Land(Settings s)
        {
            return By(s.MascotPersonality, new string[] { "\u00A1Aterrizaje perfecto!", "\u00A1Ta-ch\u00E1n!", "\u00A1Tres puntos!" }, new string[] { "Mejor aqu\u00ED.", "Firme otra vez.", "Todo en su sitio." }, new string[] { "Diez de diez.", "Eso lo tengo controlado.", "Ni un rasgu\u00F1o." });
        }

        public static string Climb(Settings s)
        {
            return By(s.MascotPersonality, new string[] { "\u00A1Hop!", "\u00A1Desde aqu\u00ED se ve todo!", "\u00A1Arriba!" }, new string[] { "Buen sitio para pensar.", "Un descansito aqu\u00ED.", "Qu\u00E9 vistas." }, new string[] { "Ahora soy el rey de la ventana.", "Qu\u00E9 desorden hay aqu\u00ED abajo.", "Esta ventana es m\u00EDa." });
        }

        public static string LevelUp(Settings s, int level)
        {
            return "\u00A1Ya somos " + MascotParts.LevelNames[level].ToLowerInvariant() + "!";
        }
    }
}
