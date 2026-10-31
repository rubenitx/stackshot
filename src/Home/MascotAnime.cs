// Stackshot - Anime-inspired costume pieces: hair, hoods, headbands, capes and vests, plus the dog and chibi bodies.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Generic archetypes (adventurer, ninja, alchemist, scout...) in the mascot's own style: no names, logos or emblems.
    public static partial class MascotParts
    {
        public const int KindDog = 6, KindChibi = 7;
        public const int HairFirst = 18, HatHood = 23, HatNinja = 24, HatStraw = 25;
        public const int OutfitScoutCape = 8, OutfitRedCoat = 9;

        // Fan tributes: the characters' own names and where each comes from (shown on hover). Not affiliated with or
        // endorsed by their owners; see the README. Ready-made costumes below: kind, color, eyes, hat, outfit, face.
        // Each language has its own table, index for index (settings store the index); Captions, Beside and the
        // catalog order match on the Spanish one, so they work the same in both languages.
        public static string[] AnimeNames { get { return I18n.IsSpanish ? AnimeNamesEs : AnimeNamesEn; } }
        public static string[] AnimeInspiration { get { return I18n.IsSpanish ? AnimeInspirationEs : AnimeInspirationEn; } }
        static readonly string[] AnimeNamesEs = { "Finn", "Jake", "Pakkun", "Edward Elric", "Eren Jaeger", "Luffy", "Tanjiro", "Gojo",
                                                       "Shin-chan", "BMO", "Pikachu", "Doraemon", "Naruto", "Totoro", "Kratos", "Doom Slayer", "Claude",
                                                       "Codex", "Gon", "Killua", "Hisoka", "Sasuke", "Kakashi", "Zoro", "Chopper", "Saitama", "Levi",
                                                       "Nezuko", "Spider-Man", "Batman", "Flash", "Superman", "Iron Man",
                                                       "Caballero", "Astronauta", "Mago sabio", "Superh\u00E9roe", "Kero", "Kamina",
                                                       "Desdentao", "Furia Luminosa", "Tormenta", "Colmillo", "Alb\u00F3ndiga",
                                                       "Stitch", "Kirby", "Grogu", "Eevee", "Charmander",
                                                       "Bulbasaur", "Squirtle", "Jigglypuff", "Snorlax", "Gengar", "Mew",
                                                       "Psyduck", "Togepi", "Meowth", "Piplup", "Mimikyu", "Lucario", "Dragonite", "Ditto", "Mewtwo",
                                                       "Sin Cara", "Calcifer", "Kodama", "Yoshi", "Pac-Man", "Creeper", "Tripulante", "Blinky", "Slime",
                                                       "Snoopy", "Hello Kitty", "Baby Groot", "Rilakkuma", "Pusheen", "Pochacco",
                                                       "Pinky", "Inky", "Clyde", "Space Invader", "Goomba", "Boo", "Toad", "Champi\u00F1\u00F3n", "Bob-omb", "Chomp Cadenas",
                                                       "Red", "Cerdito", "El Caballero", "Cubo de compa\u00F1\u00EDa", "Bomberman", "Pooka", "Fall Guy", "Cerdo", "Bloque ?", "Metroid", "Waddle Dee", "Champi\u00F1\u00F3n 1-Up",
                                                       "Wilson", "Willow", "Bumpo", "Ollmeister", "Abigail",
                                                       "Nevado", "Cerdito Valiente", "Shin-chan de robot Kantam", "Shin-chan de Ultrah\u00E9roe", "Shin-chan de Waniyama",
                                                       "Capit\u00E1n Am\u00E9rica", "Thor", "Hulk", "Viuda Negra", "Ojo de Halc\u00F3n", "Pantera Negra", "Doctor Strange",
                                                       "Capitana Marvel", "Bruja Escarlata", "Tit\u00E1n Colosal", "Tit\u00E1n de Ataque", "Tit\u00E1n Acorazado",
                                                       "Tit\u00E1n sonriente", "Tit\u00E1n bobalic\u00F3n" };
        static readonly string[] AnimeNamesEn = { "Finn", "Jake", "Pakkun", "Edward Elric", "Eren Jaeger", "Luffy", "Tanjiro", "Gojo",
                                                  "Shin-chan", "BMO", "Pikachu", "Doraemon", "Naruto", "Totoro", "Kratos", "Doom Slayer", "Claude",
                                                  "Codex", "Gon", "Killua", "Hisoka", "Sasuke", "Kakashi", "Zoro", "Chopper", "Saitama", "Levi",
                                                  "Nezuko", "Spider-Man", "Batman", "Flash", "Superman", "Iron Man",
                                                  "Knight", "Astronaut", "Wise wizard", "Superhero", "Kero", "Kamina",
                                                  "Toothless", "Light Fury", "Stormfly", "Hookfang", "Meatlug",
                                                  "Stitch", "Kirby", "Grogu", "Eevee", "Charmander",
                                                  "Bulbasaur", "Squirtle", "Jigglypuff", "Snorlax", "Gengar", "Mew",
                                                  "Psyduck", "Togepi", "Meowth", "Piplup", "Mimikyu", "Lucario", "Dragonite", "Ditto", "Mewtwo",
                                                  "No-Face", "Calcifer", "Kodama", "Yoshi", "Pac-Man", "Creeper", "Crewmate", "Blinky", "Slime",
                                                  "Snoopy", "Hello Kitty", "Baby Groot", "Rilakkuma", "Pusheen", "Pochacco",
                                                  "Pinky", "Inky", "Clyde", "Space Invader", "Goomba", "Boo", "Toad", "Super Mushroom", "Bob-omb", "Chain Chomp",
                                                  "Red", "Piggy", "The Knight", "Companion Cube", "Bomberman", "Pooka", "Fall Guy", "Pig", "? Block", "Metroid", "Waddle Dee", "1-Up Mushroom",
                                                  "Wilson", "Willow", "Bumpo", "Ollmeister", "Abigail",
                                                  "Shiro", "Buriburizaemon", "Shin-chan as Kantam Robo", "Shin-chan as Action Kamen", "Shin-chan as Waniyama",
                                                  "Captain America", "Thor", "Hulk", "Black Widow", "Hawkeye", "Black Panther", "Doctor Strange",
                                                  "Captain Marvel", "Scarlet Witch", "Colossal Titan", "Attack Titan", "Armored Titan",
                                                  "Smiling Titan", "Silly Titan" };
        static readonly string[] AnimeInspirationEs = { "Hora de aventuras", "Hora de aventuras", "Naruto", "Fullmetal Alchemist", "Ataque a los titanes",
                                                             "One Piece", "Kimetsu no Yaiba", "Jujutsu Kaisen", "Crayon Shin-chan",
                                                             "Hora de aventuras", "Pok\u00E9mon", "Doraemon", "Naruto", "Mi vecino Totoro", "God of War", "Doom",
                                                             "Anthropic", "OpenAI", "Hunter x Hunter", "Hunter x Hunter", "Hunter x Hunter", "Naruto", "Naruto",
                                                             "One Piece", "One Piece", "One Punch Man", "Ataque a los titanes", "Kimetsu no Yaiba",
                                                             "Marvel", "DC", "DC", "DC", "Marvel", null, null, null, null,
                                                             "Cardcaptor Sakura", "Tengen Toppa Gurren Lagann",
                                                             "C\u00F3mo entrenar a tu drag\u00F3n", "C\u00F3mo entrenar a tu drag\u00F3n", "C\u00F3mo entrenar a tu drag\u00F3n",
                                                             "C\u00F3mo entrenar a tu drag\u00F3n", "C\u00F3mo entrenar a tu drag\u00F3n",
                                                             "Lilo y Stitch", "Kirby", "The Mandalorian", "Pok\u00E9mon", "Pok\u00E9mon",
                                                             "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon",
                                                             "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon",
                                                             "El viaje de Chihiro", "El castillo ambulante", "La princesa Mononoke", "Super Mario", "Pac-Man", "Minecraft",
                                                             "Among Us", "Pac-Man", "Dragon Quest", "Peanuts", "Sanrio", "Marvel", "San-X", "Pusheen", "Sanrio",
                                                             "Pac-Man", "Pac-Man", "Pac-Man", "Space Invaders", "Super Mario", "Super Mario", "Super Mario", "Super Mario", "Super Mario", "Super Mario",
                                                             "Angry Birds", "Angry Birds", "Hollow Knight", "Portal", "Bomberman", "Dig Dug", "Fall Guys", "Minecraft", "Super Mario", "Metroid", "Kirby", "Super Mario",
                                                             "Don't Starve Together", "Don't Starve Together", "Oxygen Not Included", "Oxygen Not Included",
                                                             "Don't Starve Together",
                                                             "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan",
                                                             "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel",
                                                             "Ataque a los titanes", "Ataque a los titanes", "Ataque a los titanes",
                                                             "Ataque a los titanes", "Ataque a los titanes" };
        static readonly string[] AnimeInspirationEn = { "Adventure Time", "Adventure Time", "Naruto", "Fullmetal Alchemist", "Attack on Titan",
                                                        "One Piece", "Demon Slayer", "Jujutsu Kaisen", "Crayon Shin-chan",
                                                        "Adventure Time", "Pok\u00E9mon", "Doraemon", "Naruto", "My Neighbor Totoro", "God of War", "Doom",
                                                        "Anthropic", "OpenAI", "Hunter x Hunter", "Hunter x Hunter", "Hunter x Hunter", "Naruto", "Naruto",
                                                        "One Piece", "One Piece", "One Punch Man", "Attack on Titan", "Demon Slayer",
                                                        "Marvel", "DC", "DC", "DC", "Marvel", null, null, null, null,
                                                        "Cardcaptor Sakura", "Tengen Toppa Gurren Lagann",
                                                        "How to Train Your Dragon", "How to Train Your Dragon", "How to Train Your Dragon",
                                                        "How to Train Your Dragon", "How to Train Your Dragon",
                                                        "Lilo & Stitch", "Kirby", "The Mandalorian", "Pok\u00E9mon", "Pok\u00E9mon",
                                                        "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon",
                                                        "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon", "Pok\u00E9mon",
                                                        "Spirited Away", "Howl's Moving Castle", "Princess Mononoke", "Super Mario", "Pac-Man", "Minecraft",
                                                        "Among Us", "Pac-Man", "Dragon Quest", "Peanuts", "Sanrio", "Marvel", "San-X", "Pusheen", "Sanrio",
                                                        "Pac-Man", "Pac-Man", "Pac-Man", "Space Invaders", "Super Mario", "Super Mario", "Super Mario", "Super Mario", "Super Mario", "Super Mario",
                                                        "Angry Birds", "Angry Birds", "Hollow Knight", "Portal", "Bomberman", "Dig Dug", "Fall Guys", "Minecraft", "Super Mario", "Metroid", "Kirby", "Super Mario",
                                                        "Don't Starve Together", "Don't Starve Together", "Oxygen Not Included", "Oxygen Not Included",
                                                        "Don't Starve Together",
                                                        "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan", "Crayon Shin-chan",
                                                        "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel", "Marvel",
                                                        "Attack on Titan", "Attack on Titan", "Attack on Titan",
                                                        "Attack on Titan", "Attack on Titan" };
        static readonly int[,] Anime =
        {
            { 88, 17, 2, 0, 0, 0 },
            { 89, 5, 2, 0, 0, 0 },
            { 91, 18, 0, 0, 0, 0 },
            { 112, 17, 0, 0, 0, 0 },
            { 113, 17, 0, 0, 0, 0 },
            { 103, 75, 0, 0, 0, 0 },
            { 101, 75, 0, 0, 0, 0 },
            { 115, 17, 0, 0, 0, 0 },
            { 106, 17, 0, 0, 0, 0 },
            { 90, 19, 2, 0, 0, 0 },
            { 140, 28, 0, 0, 0, 0 },
            { 26, 4, 7, 0, 0, 0 },
            { 92, 75, 7, 0, 0, 0 },
            { 27, 7, 7, 0, 0, 0 },
            { 117, 20, 0, 0, 0, 0 },
            { 118, 15, 0, 0, 0, 0 },
            { 4, 21, 1, 31, 0, 0 },
            { 0, 22, 0, 0, 0, 14 },
            { 98, 75, 0, 0, 0, 0 },
            { 99, 75, 0, 0, 0, 0 },
            { 100, 75, 0, 0, 0, 0 },
            { 93, 75, 7, 0, 0, 0 },
            { 94, 75, 7, 0, 0, 0 },
            { 104, 75, 0, 0, 0, 0 },
            { 95, 23, 0, 0, 0, 0 },
            { 116, 17, 0, 0, 0, 0 },
            { 114, 17, 0, 0, 0, 0 },
            { 102, 75, 0, 0, 0, 0 },
            { 119, 13, 0, 0, 0, 0 },
            { 120, 17, 0, 0, 0, 0 },
            { 121, 17, 0, 0, 0, 0 },
            { 122, 17, 0, 0, 0, 0 },
            { 123, 17, 0, 0, 0, 0 },
            { 124, 17, 0, 0, 0, 0 },
            { 125, 17, 0, 0, 0, 0 },
            { 126, 17, 0, 0, 0, 0 },
            { 127, 17, 0, 0, 0, 0 },
            { 96, 24, 0, 0, 0, 0 },
            { 105, 17, 0, 0, 0, 0 },
            { 14, 22, 7, 0, 0, 0 },
            { 15, 11, 7, 0, 0, 0 },
            { 16, 4, 7, 0, 0, 0 },
            { 17, 25, 7, 0, 0, 0 },
            { 18, 23, 7, 0, 0, 0 },
            { 19, 26, 7, 0, 0, 0 },
            { 20, 70, 7, 0, 0, 0 },
            { 21, 27, 7, 0, 0, 0 },
            { 141, 18, 0, 0, 0, 0 },
            { 142, 9, 0, 0, 0, 0 },
            { 143, 29, 0, 0, 0, 0 },
            { 144, 30, 0, 0, 0, 0 },
            { 145, 31, 0, 0, 0, 0 },
            { 146, 32, 0, 0, 0, 0 },
            { 147, 33, 0, 0, 0, 0 },
            { 148, 31, 0, 0, 0, 0 },
            { 149, 34, 0, 0, 0, 0 },
            { 150, 35, 0, 0, 0, 0 },
            { 151, 35, 0, 0, 0, 0 },
            { 152, 36, 0, 0, 0, 0 },
            { 153, 37, 0, 0, 0, 0 },
            { 154, 36, 0, 0, 0, 0 },
            { 155, 38, 0, 0, 0, 0 },
            { 156, 39, 0, 0, 0, 0 },
            { 157, 40, 0, 0, 0, 0 },
            { 43, 41, 7, 0, 0, 0 },
            { 44, 42, 7, 0, 0, 0 },
            { 45, 43, 7, 0, 0, 0 },
            { 46, 44, 7, 0, 0, 0 },
            { 47, 52, 7, 0, 0, 0 },
            { 48, 46, 7, 0, 0, 0 },
            { 49, 47, 7, 0, 0, 0 },
            { 50, 48, 7, 0, 0, 0 },
            { 51, 4, 7, 0, 0, 0 },
            { 97, 49, 0, 0, 0, 0 },
            { 53, 49, 7, 0, 0, 0 },
            { 158, 23, 0, 0, 0, 0 },
            { 55, 50, 7, 0, 0, 0 },
            { 56, 51, 7, 0, 0, 0 },
            { 57, 49, 7, 0, 0, 0 },
            { 50, 53, 7, 0, 0, 0 },
            { 50, 54, 7, 0, 0, 0 },
            { 50, 55, 7, 0, 0, 0 },
            { 58, 46, 7, 0, 0, 0 },
            { 59, 56, 7, 0, 0, 0 },
            { 60, 49, 7, 0, 0, 0 },
            { 61, 57, 7, 0, 0, 0 },
            { 62, 58, 7, 0, 0, 0 },
            { 63, 59, 7, 0, 0, 0 },
            { 64, 59, 7, 0, 0, 0 },
            { 65, 60, 7, 0, 0, 0 },
            { 66, 61, 7, 0, 0, 0 },
            { 67, 62, 7, 0, 0, 0 },
            { 68, 63, 7, 0, 0, 0 },
            { 69, 49, 7, 0, 0, 0 },
            { 70, 64, 7, 0, 0, 0 },
            { 71, 65, 7, 0, 0, 0 },
            { 72, 66, 7, 0, 0, 0 },
            { 73, 67, 7, 0, 0, 0 },
            { 74, 68, 7, 0, 0, 0 },
            { 75, 69, 7, 0, 0, 0 },
            { 62, 71, 7, 0, 0, 0 },
            { 76, 72, 7, 0, 0, 0 },
            { 77, 72, 7, 0, 0, 0 },
            { 78, 73, 7, 0, 0, 0 },
            { 79, 74, 7, 0, 0, 0 },
            { 87, 76, 7, 0, 0, 0 },
            { 107, 17, 0, 0, 0, 0 },
            { 110, 17, 0, 0, 0, 0 },
            { 108, 17, 0, 0, 0, 0 },
            { 109, 17, 0, 0, 0, 0 },
            { 111, 17, 0, 0, 0, 0 },
            { 128, 26, 0, 0, 0, 0 },
            { 129, 46, 0, 0, 0, 0 },
            { 130, 47, 0, 0, 0, 0 },
            { 131, 25, 0, 0, 0, 0 },
            { 132, 33, 0, 0, 0, 0 },
            { 133, 41, 0, 0, 0, 0 },
            { 134, 13, 0, 0, 0, 0 },
            { 135, 9, 0, 0, 0, 0 },
            { 136, 48, 0, 0, 0, 0 },
            { 137, 25, 0, 0, 0, 0 },
            { 138, 23, 0, 0, 0, 0 },
            { 139, 18, 0, 0, 0, 0 },
            { 159, 17, 0, 0, 0, 0 },
            { 160, 17, 0, 0, 0, 0 }
        };

        // Taken out of the catalog: Fall Guy and Mewtwo (both its bodies). Their rows stay, so the indices saved for the ones
        // after them still match.
        public static bool AnimeRetired(int i) { return i == 95 || i == 63; }
        public static bool KindRetired(int kind) { return kind == KindFallGuy || kind == KindMewtwo || kind == KindMewtwoSd; }

        // Bodies drawn again as new ones (the Pokemon figures, the chibi ninjas, pirates and demon slayers, BMO, Snoopy,
        // Baby Groot, Chopper's reindeer and Kero's winged lion): the old body is no longer offered in "Cuerpo" and a look that still wears it shows the new one.
        // Both stay in the list, so saved indices keep their meaning. -1: not replaced.
        public static int KindNewer(int kind)
        {
            switch (kind)
            {
                case KindPikachu: return KindPikachuSd;
                case KindEevee: return KindEeveeSd;
                case KindCharmander: return KindCharmanderSd;
                case KindBulbasaur: return KindBulbasaurSd;
                case KindSquirtle: return KindSquirtleSd;
                case KindJigglypuff: return KindJigglySd;
                case KindSnorlax: return KindSnorlaxSd;
                case KindGengar: return KindGengarSd;
                case KindMew: return KindMewSd;
                case KindPsyduck: return KindPsyduckSd;
                case KindTogepi: return KindTogepiSd;
                case KindMeowth: return KindMeowthSd;
                case KindPiplup: return KindPiplupSd;
                case KindMimikyu: return KindMimikyuSd;
                case KindLucario: return KindLucarioSd;
                case KindDragonite: return KindDragoniteSd;
                case KindDitto: return KindDittoSd;
                case KindGroot: return KindGrootSd;
                case KindBmo: return KindBmoAt;
                case KindSnoopy: return KindSnoopySd;
                case KindNaruto: return KindNarutoSd;
                case KindSasuke: return KindSasukeSd;
                case KindKakashi: return KindKakashiSd;
                case KindLuffy: return KindLuffySd;
                case KindZoro: return KindZoroSd;
                case KindTanjiro: return KindTanjiroSd;
                case KindNezuko: return KindNezukoSd;
                case KindReindeer: return KindChopper;
                case KindKero: return KindKeroSd;
                default: return -1;
            }
        }

        public static int KindLatest(int kind) { int n = KindNewer(kind); return n >= 0 ? n : kind; }

        // What each character is in its story, shown on the vitrine's plate.
        public static string[] AnimeRoles { get { return I18n.IsSpanish ? AnimeRolesEs : AnimeRolesEn; } }
        static readonly string[] AnimeRolesEn =
        {
            "Protagonist", "Companion", "Companion", "Protagonist", "Protagonist", "Protagonist", "Protagonist", "Mentor",
            "Protagonist", "Companion", "Companion", "Protagonist", "Protagonist", "Spirit", "Protagonist", "Protagonist",
            "AI assistant", "AI assistant", "Protagonist", "Companion", "Villain", "Rival", "Mentor", "Companion", "Companion",
            "Protagonist", "Captain", "Companion", "Avenger", "Superhero", "Superhero", "Superhero", "Avenger",
            "Original", "Original", "Original", "Original", "Guardian", "Leader",
            "Co-protagonist", "Companion", "Companion", "Companion", "Companion",
            "Protagonist", "Protagonist", "Companion", "Companion", "Starter",
            "Starter", "Starter", "Singer", "Sleepyhead", "Ghost", "Mythical",
            "Companion", "Companion", "Villain", "Starter", "In disguise", "Guardian", "Pseudo-legendary", "Shapeshifter", "Legendary",
            "Spirit", "Fire demon", "Spirit", "Companion", "Protagonist", "Enemy", "Impostor?", "Enemy", "Monster",
            "Pet", "Icon", "Guardian", "Icon", "Icon", "Icon",
            "Enemy", "Enemy", "Enemy", "Enemy", "Enemy", "Enemy", "Ally", "Power-up", "Enemy", "Enemy",
            "Protagonist", "Villain", "Protagonist", "Companion", "Protagonist", "Enemy", "Contestant", "Animal", "Object", "Enemy", "Enemy", "Extra life",
            "Protagonist", "Survivor", "Duplicant", "Duplicant", "Ghost",
            "Pet", "Antihero", "Costume", "Costume", "Costume",
            "Leader", "Avenger", "Avenger", "Avenger", "Avenger", "King", "Sorcerer", "Avenger", "Avenger",
            "Titan", "Protagonist", "Titan", "Titan", "Titan"
        };
        static readonly string[] AnimeRolesEs =
        {
            "Protagonista", "Compa\u00F1ero", "Compa\u00F1ero", "Protagonista", "Protagonista", "Protagonista", "Protagonista", "Mentor",
            "Protagonista", "Compa\u00F1ero", "Compa\u00F1ero", "Protagonista", "Protagonista", "Esp\u00EDritu", "Protagonista", "Protagonista",
            "Asistente de IA", "Asistente de IA", "Protagonista", "Compa\u00F1ero", "Villano", "Rival", "Mentor", "Compa\u00F1ero", "Compa\u00F1ero",
            "Protagonista", "Capit\u00E1n", "Compa\u00F1era", "Vengador", "Superh\u00E9roe", "Superh\u00E9roe", "Superh\u00E9roe", "Vengador",
            "Original", "Original", "Original", "Original", "Guardi\u00E1n", "L\u00EDder",
            "Coprotagonista", "Compa\u00F1era", "Compa\u00F1era", "Compa\u00F1ero", "Compa\u00F1era",
            "Protagonista", "Protagonista", "Compa\u00F1ero", "Compa\u00F1ero", "Inicial",
            "Inicial", "Inicial", "Cantante", "Dormil\u00F3n", "Fantasma", "Singular",
            "Compa\u00F1ero", "Compa\u00F1ero", "Villano", "Inicial", "Disfrazado", "Guardi\u00E1n", "Pseudolegendario", "Transformista", "Legendario",
            "Esp\u00EDritu", "Demonio de fuego", "Esp\u00EDritu", "Compa\u00F1ero", "Protagonista", "Enemigo", "\u00BFImpostor?", "Enemigo", "Monstruo",
            "Mascota", "Icono", "Guardi\u00E1n", "Icono", "Icono", "Icono",
            "Enemigo", "Enemigo", "Enemigo", "Enemigo", "Enemigo", "Enemigo", "Aliado", "Potenciador", "Enemigo", "Enemigo",
            "Protagonista", "Villano", "Protagonista", "Compa\u00F1ero", "Protagonista", "Enemigo", "Concursante", "Animal", "Objeto", "Enemigo", "Enemigo", "Vida extra",
            "Protagonista", "Superviviente", "Duplicante", "Duplicante", "Fantasma",
            "Mascota", "Antih\u00E9roe", "Disfraz", "Disfraz", "Disfraz",
            "L\u00EDder", "Vengador", "Vengador", "Vengadora", "Vengador", "Rey", "Hechicero", "Vengadora", "Vengadora",
            "Tit\u00E1n", "Protagonista", "Tit\u00E1n", "Tit\u00E1n", "Tit\u00E1n"
        };

        public static string AnimeRole(int i) { return i >= 0 && i < AnimeRoles.Length ? AnimeRoles[i] : null; }

        // The name under a tile, short enough to read whole there (the group over the tiles says the rest).
        // Keyed by the Spanish name; then the Spanish and the English caption.
        static readonly string[,] Captions =
        {
            { "Shin-chan de robot Kantam", "Kantam", "Kantam" }, { "Shin-chan de Ultrah\u00E9roe", "Ultrah\u00E9roe", "Action Kamen" }, { "Shin-chan de Waniyama", "Waniyama", "Waniyama" },
            { "Champi\u00F1\u00F3n 1-Up", "1-Up", "1-Up" }, { "Chomp Cadenas", "Chomp", "Chomp" }, { "Cubo de compa\u00F1\u00EDa", "Cubo", "Cube" }
        };

        public static string AnimeCaption(int i)
        {
            if (i < 0 || i >= AnimeNames.Length) return "";
            for (int k = 0; k < Captions.GetLength(0); k++) if (Captions[k, 0] == AnimeNamesEs[i]) return I18n.IsSpanish ? Captions[k, 1] : Captions[k, 2];
            return AnimeNames[i];
        }

        // What the dock writes over each group of characters: a long series name goes by its short one, and Studio
        // Ghibli's films, the AI assistants, the kawaii classics and the lone characters of smaller games share a group, so
        // the row reads as a few clear groups instead of a name over every other tile. Only the dock: the plate and the
        // label on hover keep each one's own series.
        // Series as the Spanish table names it, as the English one does, then the group in Spanish and in English.
        static readonly string[,] SeriesGroups =
        {
            { "Tengen Toppa Gurren Lagann", "Tengen Toppa Gurren Lagann", "Gurren Lagann", "Gurren Lagann" }, { "Cardcaptor Sakura", "Cardcaptor Sakura", "Sakura", "Sakura" },
            { "Crayon Shin-chan", "Crayon Shin-chan", "Shin-chan", "Shin-chan" },
            { "Fullmetal Alchemist", "Fullmetal Alchemist", "Fullmetal", "Fullmetal" }, { "Don't Starve Together", "Don't Starve Together", "Don't Starve", "Don't Starve" },
            { "Oxygen Not Included", "Oxygen Not Included", "Oxygen", "Oxygen" },
            { "Mi vecino Totoro", "My Neighbor Totoro", "Studio Ghibli", "Studio Ghibli" }, { "El viaje de Chihiro", "Spirited Away", "Studio Ghibli", "Studio Ghibli" },
            { "El castillo ambulante", "Howl's Moving Castle", "Studio Ghibli", "Studio Ghibli" },
            { "La princesa Mononoke", "Princess Mononoke", "Studio Ghibli", "Studio Ghibli" }, { "The Mandalorian", "The Mandalorian", "Star Wars", "Star Wars" },
            { "Anthropic", "Anthropic", "Asistentes de IA", "AI assistants" },
            { "OpenAI", "OpenAI", "Asistentes de IA", "AI assistants" }, { "Sanrio", "Sanrio", "Kawaii", "Kawaii" }, { "San-X", "San-X", "Kawaii", "Kawaii" },
            { "Pusheen", "Pusheen", "Kawaii", "Kawaii" },
            { "Among Us", "Among Us", "M\u00E1s videojuegos", "More games" }, { "Dragon Quest", "Dragon Quest", "M\u00E1s videojuegos", "More games" },
            { "Space Invaders", "Space Invaders", "M\u00E1s videojuegos", "More games" },
            { "Hollow Knight", "Hollow Knight", "M\u00E1s videojuegos", "More games" }, { "Portal", "Portal", "M\u00E1s videojuegos", "More games" },
            { "Bomberman", "Bomberman", "M\u00E1s videojuegos", "More games" },
            { "Dig Dug", "Dig Dug", "M\u00E1s videojuegos", "More games" }, { "Fall Guys", "Fall Guys", "M\u00E1s videojuegos", "More games" },
            { "Metroid", "Metroid", "M\u00E1s videojuegos", "More games" }
        };

        // Takes a series in either language and answers in the current one.
        public static string SeriesGroup(string s)
        {
            if (s == null) return null;
            for (int i = 0; i < SeriesGroups.GetLength(0); i++)
                if (SeriesGroups[i, 0] == s || SeriesGroups[i, 1] == s) return I18n.IsSpanish ? SeriesGroups[i, 2] : SeriesGroups[i, 3];
            return s;
        }

        // The characters grouped by series (each group where it first appears in the catalog), so every Pokemon or every
        // dragon sit together: the app's own originals first, then the series drawn in full detail (the vinyl figures,
        // then Klei's in their own drawn style), then all the others. Only the order shown: the indices the settings store
        // stay.
        static int[] animeOrder;
        static readonly string[] KleiSeries = { "Don't Starve Together", "Oxygen Not Included" };
        static readonly string[,] Beside = { { "Champi\u00F1\u00F3n 1-Up", "Champi\u00F1\u00F3n" } };
        public static int[] AnimeOrder
        {
            get
            {
                if (animeOrder != null) return animeOrder;
                System.Collections.Generic.List<string> groups = new System.Collections.Generic.List<string>(), first = new System.Collections.Generic.List<string>(), rest = new System.Collections.Generic.List<string>(), klei = new System.Collections.Generic.List<string>();
                foreach (string s in AnimeInspiration) if (s != null && !groups.Contains(SeriesGroup(s))) groups.Add(SeriesGroup(s));
                foreach (string s in KleiSeries) klei.Add(SeriesGroup(s));
                foreach (string gr in groups) if (!klei.Contains(gr)) (GroupDetailed(gr) ? first : rest).Add(gr);
                first.AddRange(klei);
                first.AddRange(rest);
                System.Collections.Generic.List<int> o = new System.Collections.Generic.List<int>();
                for (int i = 0; i < AnimeNames.Length; i++) if (AnimeInspiration[i] == null) o.Add(i);
                foreach (string gr in first)
                    for (int i = 0; i < AnimeNames.Length; i++) if (AnimeInspiration[i] != null && SeriesGroup(AnimeInspiration[i]) == gr) o.Add(i);
                // Variants sit right next to the one they come from (the green 1-Up beside the red mushroom).
                for (int k = 0; k < Beside.GetLength(0); k++)
                {
                    int v = Array.IndexOf(AnimeNamesEs, Beside[k, 0]), b = Array.IndexOf(AnimeNamesEs, Beside[k, 1]);
                    if (v < 0 || b < 0) continue;
                    o.Remove(v);
                    o.Insert(o.IndexOf(b) + 1, v);
                }
                animeOrder = o.ToArray();
                return animeOrder;
            }
        }

        // A group shown right after the app's own: drawn in full detail, or one of Klei's.
        public static bool GroupFeatured(string gr)
        {
            foreach (string s in KleiSeries) if (SeriesGroup(s) == gr) return true;
            return GroupDetailed(gr);
        }

        // A group drawn in full detail: one of its characters is a vinyl figure.
        static bool GroupDetailed(string gr)
        {
            for (int i = 0; i < AnimeNames.Length; i++) if (AnimeInspiration[i] != null && SeriesGroup(AnimeInspiration[i]) == gr && IsToon(Anime[i, 0])) return true;
            return false;
        }

        public static void ApplyAnime(MascotLook l, int i)
        {
            l.Kind = Anime[i, 0]; l.Color = Anime[i, 1]; l.Eyes = Anime[i, 2];
            l.Hat = Anime[i, 3]; l.Outfit = Anime[i, 4]; l.Face = Anime[i, 5];
        }

        // ---- Dog: wagging tail behind, floppy ears and nose in front, muzzle on the body.
        static void DogTail(Graphics g, float D, Color c2, double now)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(0.27f * D, 0.2f * D);
            g.RotateTransform((float)(-35 + Math.Sin(now / 95.0) * 22));
            using (GraphicsPath t = new GraphicsPath())
            {
                t.AddBezier(P(0, 0), P(0.06f * D, -0.04f * D), P(0.12f * D, -0.1f * D), P(0.13f * D, -0.17f * D));
                using (Pen p = new Pen(c2, D * 0.065f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, t); }
            }
            g.Restore(st);
        }

        static void DogEars(Graphics g, float D, Color c2, MascotPose p)
        {
            foreach (int s in new int[] { -1, 1 })
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(s * 0.26f * D, -0.25f * D);
                g.RotateTransform((float)(s * (14 + (s < 0 ? p.EarL : p.EarR) * 0.5)));
                using (GraphicsPath e = new GraphicsPath())
                {
                    e.AddBezier(P(-0.06f * D, 0), P(-0.1f * D, 0.12f * D), P(-0.06f * D, 0.26f * D), P(0.01f * D, 0.27f * D));
                    e.AddBezier(P(0.01f * D, 0.27f * D), P(0.08f * D, 0.27f * D), P(0.08f * D, 0.1f * D), P(0.05f * D, 0));
                    e.CloseFigure();
                    RectangleF r = new RectangleF(-0.1f * D, 0, 0.18f * D, 0.27f * D);
                    Fill(g, e, r, Darker(c2, 0.15), Darker(c2, 0.4), 90f);
                    Stroke(g, e, Color.FromArgb(40, 0, 0, 0), D * 0.008f);
                }
                g.Restore(st);
            }
        }

        static void DogMuzzle(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            Ellipse(g, Color.FromArgb(150, 255, 248, 235), -0.12f * D, fy + 0.035f * D, 0.24f * D, 0.15f * D);
        }

        static void DogNose(Graphics g, Geo geo, float D)
        {
            float fy = geo.FaceY * D;
            using (GraphicsPath n = new GraphicsPath())
            {
                RectangleF r = new RectangleF(-0.04f * D, fy + 0.045f * D, 0.08f * D, 0.05f * D);
                n.AddEllipse(r);
                Fill(g, n, r, Color.FromArgb(70, 60, 80), Color.FromArgb(20, 18, 28), 90f);
            }
            Ellipse(g, Color.FromArgb(150, 255, 255, 255), -0.022f * D, fy + 0.052f * D, 0.022f * D, 0.012f * D);
        }

        // ---- Hair: one shape with a style per character. Origin at the top of the head, u = hat unit.
        // Hair styles: colors, how far the spikes stick out, how many, and how low the bangs come (fixed tables, not per frame).
        static readonly Color[,] HairTones =
        {
            { Color.FromArgb(255, 222, 110), Color.FromArgb(214, 150, 30) },   // blond
            { Color.FromArgb(120, 80, 52), Color.FromArgb(62, 38, 24) },       // brown
            { Color.FromArgb(58, 56, 72), Color.FromArgb(16, 16, 24) },        // black, spiky
            { Color.FromArgb(250, 250, 255), Color.FromArgb(190, 200, 225) },  // white
            { Color.FromArgb(150, 50, 60), Color.FromArgb(70, 18, 28) },       // dark red
            { Color.FromArgb(52, 50, 60), Color.FromArgb(14, 14, 20) },         // black, smooth
            { Color.FromArgb(255, 222, 90), Color.FromArgb(225, 150, 20) },      // blond, spiky
            { Color.FromArgb(46, 74, 54), Color.FromArgb(10, 22, 14) },       // dark green-black, tall spikes
            { Color.FromArgb(240, 96, 128), Color.FromArgb(150, 30, 72) },    // magenta, slicked up
            { Color.FromArgb(62, 66, 100), Color.FromArgb(16, 18, 36) },      // dark navy, spiky back
            { Color.FromArgb(116, 206, 124), Color.FromArgb(38, 120, 60) },   // green, short
            { Color.FromArgb(58, 56, 72), Color.FromArgb(16, 16, 24) },       // black, tall flame
            { Color.FromArgb(58, 42, 56), Color.FromArgb(16, 10, 18) },       // black, long
            { Color.FromArgb(222, 226, 236), Color.FromArgb(150, 156, 172) }  // silver, slanted spikes
        };
        static readonly float[] HairSpike = { 0.12f, 0.04f, 0.5f, 0.3f, 0.06f, 0.02f, 0.42f, 0.8f, 0.45f, 0.38f, 0.08f, 1.15f, 0.03f, 0.55f };
        static readonly int[] HairSpikes = { 7, 9, 7, 8, 9, 10, 8, 6, 6, 7, 10, 5, 10, 6 };
        static readonly float[] HairBangs = { 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.34f, 0.3f, 0.18f, 0.34f, 0.24f, 0.16f, 0.32f, 0.3f };

        static void Hair(Graphics g, float u, int style, double now)
        {
            Color[,] tones = HairTones;
            float[] spike = HairSpike;
            int[] spikes = HairSpikes;
            float cx = 0, cy = 0.4f * u, rx = 0.64f * u, ry = 0.46f * u;
            int n = spikes[style] * 2;
            PointF[] top = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI + Math.PI * i / n;
                float k = 1 + (i % 2 == 1 ? spike[style] * (float)Math.Pow(Math.Sin(Math.PI * i / n), 1.5) : 0);
                top[i] = new PointF(cx + (float)Math.Cos(a) * rx * (i % 2 == 1 ? 1 + spike[style] * 0.3f : 1), cy + (float)Math.Sin(a) * ry * k);
            }
            using (GraphicsPath h = new GraphicsPath())
            {
                float side = style == 12 ? 1.05f * u : 0.5f * u; // long hair falls past the cheeks
                h.AddLine(P(-0.66f * u, side), top[0]);
                h.AddLines(top);
                h.AddLine(top[n], P(0.66f * u, side));
                // Bangs: points hanging over the forehead.
                int b = style == 2 ? 3 : 5;
                for (int i = 0; i <= b * 2; i++)
                {
                    float x = 0.62f * u - 1.24f * u * i / (b * 2);
                    float y = i % 2 == 1 ? HairBangs[style] * u : (i == 0 || i == b * 2 ? side : Math.Min(0.16f, HairBangs[style] * 0.5f) * u);
                    if (style == 0 && i == b) y = 0.12f * u; // center part
                    h.AddLine(P(x, y), P(x, y));
                }
                h.CloseFigure();
                RectangleF r = new RectangleF(-0.68f * u, -0.3f * u, 1.36f * u, 0.82f * u);
                Fill(g, h, r, tones[style, 0], tones[style, 1], 70f);
                Shine(g, h, r, style == 3 ? 60 : 90);
                Stroke(g, h, Color.FromArgb(50, 0, 0, 0), u * 0.014f);
            }
            if (style == 0)
            {
                // A curly strand on top that bounces.
                float sway = (float)Math.Sin(now / 400.0) * 0.03f * u;
                using (GraphicsPath a = new GraphicsPath())
                {
                    a.AddBezier(P(0, -0.04f * u), P(0.02f * u, -0.2f * u), P(0.16f * u + sway, -0.24f * u), P(0.12f * u + sway, -0.14f * u));
                    using (Pen p = new Pen(tones[0, 1], u * 0.05f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawPath(p, a); }
                }
            }
        }

        static void Hood(Graphics g, float u)
        {
            Color a = Color.FromArgb(255, 255, 255), b = Color.FromArgb(206, 214, 230);
            foreach (int s in new int[] { -1, 1 }) Ellipse(g, b, s * 0.32f * u - 0.12f * u, -0.3f * u, 0.24f * u, 0.24f * u);
            using (GraphicsPath h = new GraphicsPath(FillMode.Alternate))
            {
                h.AddArc(-0.68f * u, -0.2f * u, 1.36f * u, 1.1f * u, 180, 180);
                h.AddLine(P(0.68f * u, 0.35f * u), P(0.6f * u, 0.95f * u));
                h.AddLine(P(0.6f * u, 0.95f * u), P(-0.6f * u, 0.95f * u));
                h.CloseFigure();
                h.AddEllipse(-0.5f * u, 0.08f * u, 1.0f * u, 0.95f * u); // the face opening
                RectangleF r = new RectangleF(-0.68f * u, -0.2f * u, 1.36f * u, 1.15f * u);
                Fill(g, h, r, a, b, 90f);
                Stroke(g, h, Color.FromArgb(45, 0, 0, 0), u * 0.014f);
            }
        }

        static void NinjaBand(Graphics g, float u, double now)
        {
            float y = 0.2f * u;
            float flow = (float)Math.Sin(now / 300.0) * 3;
            GraphicsState st = g.Save();
            g.TranslateTransform(0.6f * u, y + 0.04f * u);
            foreach (float rot in new float[] { 20 + flow, 40 + flow })
            {
                GraphicsState s2 = g.Save();
                g.RotateTransform(rot);
                RectangleF tail = new RectangleF(0, -0.04f * u, 0.32f * u, 0.08f * u);
                using (GraphicsPath tp = Theme.Round(tail, 0.03f * u)) FillSolid(g, tp, Color.FromArgb(30, 44, 90));
                g.Restore(s2);
            }
            g.Restore(st);
            RectangleF band = new RectangleF(-0.66f * u, y - 0.07f * u, 1.32f * u, 0.15f * u);
            using (GraphicsPath bp = Theme.Round(band, 0.06f * u)) Fill(g, bp, band, Color.FromArgb(52, 72, 140), Color.FromArgb(28, 40, 86), 90f);
            RectangleF plate = new RectangleF(-0.26f * u, y - 0.09f * u, 0.52f * u, 0.19f * u);
            using (GraphicsPath pp = Theme.Round(plate, 0.04f * u))
            {
                Fill(g, pp, plate, Color.FromArgb(232, 236, 244), Color.FromArgb(150, 158, 175), 90f);
                Shine(g, pp, plate, 120);
                Stroke(g, pp, Color.FromArgb(90, 60, 66, 80), u * 0.012f);
            }
            foreach (int s in new int[] { -1, 1 })
                foreach (int t in new int[] { -1, 1 })
                    Ellipse(g, Color.FromArgb(120, 70, 76, 92), s * 0.21f * u - 0.012f * u, y + t * 0.05f * u - 0.012f * u, 0.024f * u, 0.024f * u);
            for (int i = -1; i <= 1; i++) Line(g, Color.FromArgb(110, 70, 76, 92), u * 0.014f, -0.1f * u, y + i * 0.03f * u, 0.1f * u, y + i * 0.03f * u);
        }

        static void StrawHat(Graphics g, float u)
        {
            Color s1 = Color.FromArgb(250, 220, 140), s2 = Color.FromArgb(205, 160, 70);
            RectangleF brim = new RectangleF(-0.8f * u, -0.08f * u, 1.6f * u, 0.3f * u);
            using (GraphicsPath b = new GraphicsPath())
            {
                b.AddEllipse(brim);
                Fill(g, b, brim, s1, s2, 90f);
                Stroke(g, b, Color.FromArgb(70, 120, 80, 20), u * 0.012f);
            }
            RectangleF dome = new RectangleF(-0.44f * u, -0.44f * u, 0.88f * u, 0.7f * u);
            using (GraphicsPath d = new GraphicsPath())
            {
                d.AddArc(dome, 180, 180);
                d.CloseFigure();
                RectangleF r = new RectangleF(dome.X, dome.Y, dome.Width, dome.Height / 2);
                Fill(g, d, r, s1, s2, 90f);
                Region old = g.Clip;
                g.SetClip(d, CombineMode.Intersect);
                for (float yy = dome.Y + 0.06f * u; yy < 0; yy += 0.06f * u) Line(g, Color.FromArgb(40, 120, 80, 20), u * 0.01f, dome.X, yy, dome.Right, yy);
                g.Clip = old;
                old.Dispose();
                Shine(g, d, r, 80);
            }
            RectangleF band = new RectangleF(-0.44f * u, -0.1f * u, 0.88f * u, 0.1f * u);
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddRectangle(band);
                Fill(g, bp, band, Color.FromArgb(235, 50, 50), Color.FromArgb(160, 20, 30), 90f);
            }
        }

        // ---- Outfits.
        static void Straps(Graphics g, float D, float y, float w, Color c)
        {
            foreach (int s in new int[] { -1, 1 })
                Line(g, c, D * 0.035f, s * w * 0.32f, y - 0.03f * D, s * w * 0.18f, y + 0.2f * D);
            Line(g, c, D * 0.035f, -w * 0.3f, y + 0.12f * D, w * 0.3f, y + 0.12f * D);
        }

        static void Panels(Graphics g, float D, float y, float w, Geo geo, Color a, Color b, bool checker)
        {
            foreach (int s in new int[] { -1, 1 })
            {
                using (GraphicsPath p = Poly(P(s * 0.05f * D, y - 0.03f * D), P(s * w * 0.56f, y - 0.06f * D), P(s * w * 0.6f, geo.Bottom * D), P(s * 0.13f * D, geo.Bottom * D)))
                {
                    RectangleF r = new RectangleF(-w * 0.6f, y - 0.06f * D, w * 1.2f, geo.Bottom * D - y + 0.06f * D);
                    if (!checker) Fill(g, p, r, a, b, 90f);
                    else
                    {
                        FillSolid(g, p, b);
                        Region old = g.Clip;
                        g.SetClip(p, CombineMode.Intersect);
                        float q = Math.Max(2f, 0.05f * D);
                        using (SolidBrush br = new SolidBrush(a))
                            for (float yy = r.Y; yy < r.Bottom; yy += q)
                                for (float xx = r.X + ((int)((yy - r.Y) / q) % 2) * q; xx < r.Right; xx += q * 2) g.FillRectangle(br, xx, yy, q, q);
                        g.Clip = old;
                        old.Dispose();
                    }
                    Stroke(g, p, Color.FromArgb(60, 0, 0, 0), D * 0.01f);
                }
            }
        }

        static void GiCollar(Graphics g, float D, float y, float w)
        {
            Color blue = Color.FromArgb(37, 70, 200);
            using (GraphicsPath v = Poly(P(-0.14f * D, y - 0.04f * D), P(0, y + 0.1f * D), P(0.14f * D, y - 0.04f * D), P(0.08f * D, y - 0.04f * D), P(0, y + 0.04f * D), P(-0.08f * D, y - 0.04f * D)))
                FillSolid(g, v, blue);
            RectangleF belt = new RectangleF(-w * 0.5f, y + 0.13f * D, w, 0.06f * D);
            using (GraphicsPath bp = Theme.Round(belt, 0.03f * D)) Fill(g, bp, belt, Color.FromArgb(70, 110, 240), blue, 90f);
            Ellipse(g, Color.FromArgb(30, 50, 160), -0.035f * D, y + 0.125f * D, 0.07f * D, 0.07f * D);
        }

        static void CoatCollar(Graphics g, float D, float y, float w)
        {
            // Black shirt under the open coat.
            RectangleF r = new RectangleF(-0.16f * D, y - 0.06f * D, 0.32f * D, 0.4f * D);
            using (GraphicsPath p = new GraphicsPath()) { p.AddRectangle(r); Fill(g, p, r, Color.FromArgb(44, 42, 52), Color.FromArgb(16, 14, 20), 90f); }
        }

        static void Blindfold(Graphics g, float D, Geo geo)
        {
            float fy = geo.FaceY * D;
            RectangleF band = new RectangleF(-geo.HeadW * D * 0.52f, fy - 0.06f * D, geo.HeadW * D * 1.04f, 0.12f * D);
            using (GraphicsPath p = Theme.Round(band, 0.03f * D)) Fill(g, p, band, Color.FromArgb(46, 46, 56), Color.FromArgb(14, 14, 20), 90f);
        }

        // Called from the main switches.
        static bool PaintAnimeHat(Graphics g, int hat, float u, double now)
        {
            if (PaintSeriesHair(g, hat, u, now)) return true;
            if (hat >= HairFirst && hat < HairFirst + 5) { Hair(g, u, hat - HairFirst, now); return true; }
            if (hat == HatHood) { Hood(g, u); return true; }
            if (hat == HatNinja) { NinjaBand(g, u, now); return true; }
            if (hat == HatStraw) { StrawHat(g, u); return true; }
            if (PaintMoreHat(g, hat, u, now)) return true;
            return false;
        }

        static bool PaintAnimeOutfit(Graphics g, MascotLook l, float D, float y, float w)
        {
            if (l.Outfit < 8) return false;
            Geo geo = GeoFor(l.Kind);
            // Clothes stay inside the body silhouette.
            GraphicsPath body = l.Kind == 0 ? Theme.Round(new RectangleF(-geo.HeadW * D / 2, geo.Top * D, geo.HeadW * D, (geo.Bottom - geo.Top) * D), (geo.Bottom - geo.Top) * D * 0.42f)
                                            : BodyPath(l.Kind, geo, D, 0);
            Region old = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            try { return Outfit(g, l, D, y, w, geo); }
            finally { g.Clip = old; old.Dispose(); body.Dispose(); }
        }

        static bool Outfit(Graphics g, MascotLook l, float D, float y, float w, Geo geo)
        {
            switch (l.Outfit)
            {
                case 8: Straps(g, D, y, w, Color.FromArgb(120, 74, 40)); return true;
                case 9: CoatCollar(g, D, y, w); Panels(g, D, y, w, geo, Color.FromArgb(225, 45, 45), Color.FromArgb(140, 16, 24), false); return true;
                case 10: Panels(g, D, y, w, geo, Color.FromArgb(70, 100, 190), Color.FromArgb(32, 50, 120), false); return true;
                case 11: GiCollar(g, D, y, w); return true;
                case 12: Panels(g, D, y, w, geo, Color.FromArgb(20, 20, 24), Color.FromArgb(22, 140, 100), true); return true;
                case 13: Straps(g, D, y, w, Color.FromArgb(34, 160, 80)); return true;
                case 14: Panels(g, D, y, w, geo, Color.FromArgb(240, 60, 60), Color.FromArgb(170, 20, 30), false); return true;
                default: return MoreOutfit(g, l, D, y, w, geo);
            }
        }
    }
}
