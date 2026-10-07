// Stackshot - What the mascot says, by personality.
// MIT License - https://github.com/rubenitx/stackshot
using System;

namespace Stackshot
{
    public static class MascotTalk
    {
        static readonly Random rnd = new Random();

        static string Pick(string[] lines) { return lines[rnd.Next(lines.Length)]; }

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
                case 1: return g + " Aqu\u00ED estoy, tranquilo.";
                case 2: return g + " \u00BFA qui\u00E9n capturamos hoy?";
                default: return g + " \u00BFQu\u00E9 capturamos?";
            }
        }

        public static string[] Pokes(Settings s)
        {
            string key = Hotkeys.Display(s.HotRegion);
            switch (s.MascotPersonality)
            {
                case 1:
                    return new string[] { "Mmm\u2026 hola.", "Con calma, que hay tiempo.", "Respira. Captura. Repite.", "Pulsa " + key + " cuando quieras." };
                case 2:
                    return new string[] { "\u00A1Eh! \u00A1Que no soy un bot\u00F3n!", "\u00BFOtra vez t\u00FA?", "Si me pinchas m\u00E1s, me pongo a bailar.",
                                          "Yo hago las capturas m\u00E1s chulas, que lo sepas.", "Pulsa " + key + " y no me hagas cosquillas." };
                default:
                    return new string[] { "\u00A1Eh, que me haces cosquillas!", "\u00BFHacemos una captura?", "Pulsa " + key + " y yo me encargo.",
                                          "\u00A1Sonr\u00EDe! Bueno\u2026 yo ya lo hago.", "Bip bup. Bip.", "Me encanta cuando curvas las flechas." };
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
                "Si me activas en el escritorio, te acompa\u00F1o junto a la barra de tareas."
            };
            return Pick(tips);
        }

        public static string Celebrate(Settings s)
        {
            switch (s.MascotPersonality)
            {
                case 1: return Pick(new string[] { "Bonita captura.", "Guardada en mi memoria.", "Muy bien." });
                case 2: return Pick(new string[] { "\u00A1Toma ya!", "\u00A1Eso es arte!", "\u00A1Directa al museo!" });
                default: return Pick(new string[] { "\u00A1Buena captura!", "\u00A1Clic! Ha quedado genial.", "Esa me la guardo." });
            }
        }

        // Spontaneous lines on the desktop.
        public static string Idle(Settings s)
        {
            switch (s.MascotPersonality)
            {
                case 1: return Pick(new string[] { "Qu\u00E9 paz\u2026", "Sigo aqu\u00ED.", "Un paseo corto y vuelvo." });
                case 2: return Pick(new string[] { "\u00BFTrabajando o mirando memes?", "Me aburro\u2026 \u00BFcapturamos algo?", "Ojo, que te vigilo." });
                default: return Pick(new string[] { "\u00A1Hola! Sigo por aqu\u00ED.", "\u00BFNecesitas una captura?", "Voy a dar una vuelta." });
            }
        }

        public static string LevelUp(Settings s, int level)
        {
            return "\u00A1Ya somos " + MascotParts.LevelNames[level].ToLowerInvariant() + "!";
        }
    }
}
