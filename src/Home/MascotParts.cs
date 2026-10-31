// Stackshot - Mascot catalog (characters, colors, eyes, hats, outfits, face accessories) and their drawing.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    // Everything the user can customize. Indices are stored in settings, so new entries go at the end.
    public class MascotLook
    {
        public int Kind, Color, Eyes, Hat, Outfit, Face, Personality, Accessory;
        public bool Seasonal = true;

        public static MascotLook From(Settings s)
        {
            MascotLook l = new MascotLook();
            l.Kind = MascotParts.KindLatest(Clamp(s.MascotKind, MascotParts.Kinds.Length));
            l.Color = Clamp(s.MascotColor, MascotParts.ColorNames.Length);
            l.Eyes = Clamp(s.MascotEyes, MascotParts.EyeNames.Length);
            l.Hat = s.MascotHat >= MascotParts.HatNames.Length ? 0 : Clamp(s.MascotHat, MascotParts.HatNames.Length);
            l.Outfit = Clamp(s.MascotOutfit, MascotParts.OutfitNames.Length);
            l.Face = Clamp(s.MascotFace, MascotParts.FaceNames.Length);
            l.Personality = Clamp(s.MascotPersonality, MascotParts.Personalities.Length);
            l.Accessory = Clamp(s.MascotAccessory, MascotParts.AccessoryNames.Length);
            l.Seasonal = s.MascotSeasonal;
            return l;
        }

        public void ApplyTo(Settings s)
        {
            s.MascotKind = Kind; s.MascotColor = Color; s.MascotEyes = Eyes; s.MascotHat = Hat;
            s.MascotOutfit = Outfit; s.MascotFace = Face; s.MascotPersonality = Personality; s.MascotSeasonal = Seasonal;
            s.MascotAccessory = Accessory;
        }

        public MascotLook Clone() { return (MascotLook)MemberwiseClone(); }

        public string Key
        {
            get { return Kind + "." + Color + "." + Eyes + "." + Hat + "." + Outfit + "." + Face + "." + (Seasonal ? 1 : 0) + (Accessory > 0 ? "." + Accessory : ""); }
        }

        // The hat actually worn: a seasonal costume replaces "no hat" around Halloween and Christmas (not on the ones that
        // already wear a hat of their own).
        public int EffectiveHat(DateTime d)
        {
            if (Hat != 0) return Hat;
            if (MascotParts.OwnHat(Kind)) return 0;
            if (Seasonal)
            {
                if (d.Month == 10 || (d.Month == 11 && d.Day <= 2)) return MascotParts.HatPumpkin;
                if ((d.Month == 12 && d.Day >= 10) || (d.Month == 1 && d.Day <= 6)) return MascotParts.HatSanta;
            }
            // Late at night, with no hat of its own: its nightcap.
            if (MascotParts.NightCaps && (d.Hour >= 22 || d.Hour < 6)) return MascotParts.HatNightcap;
            return 0;
        }

        static int Clamp(int v, int n) { return Math.Max(0, Math.Min(n - 1, v)); }
    }

    public static partial class MascotParts
    {
        static readonly string[] KindsEs = { "Robot", "Gato", "Conejo", "Fantasma", "Slime", "Drag\u00F3n", "Perro", "Chibi", "Ping\u00FCino", "Panda", "Zorro", "Rana", "Reno", "Le\u00F3n alado",
                                                   "Furia Nocturna", "Furia Luminosa", "Nadder Mort\u00EDfero", "Pesadilla Monstruosa", "Gronckle",
                                                   "Experimento azul", "Bolita rosa", "Peque\u00F1o jedi", "Peludito", "Salamandra", "Rat\u00F3n el\u00E9ctrico",
                                                   "Consola", "Gato c\u00F3smico", "Esp\u00EDritu del bosque",
                                                   "Semilla", "Tortuguita", "Globo cantor", "Dormil\u00F3n", "Sombra traviesa", "Gatito ps\u00EDquico",
                                                   "Pato confuso", "Huevito", "Gato con moneda", "Ping\u00FCinito", "Disfrazado", "Chacal de aura",
                                                   "Drag\u00F3n amable", "Masa", "Clon ps\u00EDquico", "Esp\u00EDritu sin cara", "Demonio de fuego", "Esp\u00EDritu del \u00E1rbol",
                                                   "Dinosaurio verde", "Comecocos", "Bloque verde", "Tripulante", "Fantasmita", "Gota azul",
                                                   "Perrito blanco", "Gatita con lazo", "Arbolito", "Osito relajado", "Gata gordita", "Perrito orej\u00F3n",
                                                   "Invasor", "Champi\u00F1\u00F3n gru\u00F1\u00F3n", "Fantasma t\u00EDmido", "Setita", "Champi\u00F1\u00F3n", "Bomba con llave",
                                                   "Bola encadenada", "P\u00E1jaro enfadado", "Cerdito verde", "Caballero hueco", "Cubo con coraz\u00F3n", "Bombardero",
                                                   "Globo con gafas", "Jud\u00EDa", "Cerdo de bloques", "Bloque sorpresa", "Medusa", "Bolita naranja",
                                                   "Cient\u00EDfico", "Chica del fuego", "Duplicante", "Duplicante rizado",
                                                   "Ninja rubio", "Ninja vengador", "Ninja copia", "Pirata de goma", "Espadach\u00EDn", "Cazador de demonios", "Hermana demonio",
                                                   "Ni\u00F1a fantasma", "Chico aventurero", "Perro m\u00E1gico", "Consola amiga", "Perro rastreador", "Ninja naranja",
                                                   "Ninja del abanico", "Ninja enmascarado", "Reno doctor", "Guardi\u00E1n alado", "Beagle", "Cazador novato", "Chico el\u00E9ctrico", "Mago bromista", "Chico del haori", "Chica del bamb\u00FA", "Pirata del sombrero", "Espadach\u00EDn verde", "L\u00EDder con gafas", "Ni\u00F1o travieso", "Perrito de algod\u00F3n", "Ni\u00F1o robot", "Ni\u00F1o h\u00E9roe", "Cerdito espadach\u00EDn", "Ni\u00F1o cocodrilo",
                                                   "Alquimista", "Soldado explorador", "Capit\u00E1n de \u00E9lite", "Hechicero vendado",
                                                   "H\u00E9roe calvo", "Guerrero espartano", "Soldado acorazado", "H\u00E9roe ar\u00E1cnido", "Justiciero nocturno", "Corredor veloz", "H\u00E9roe de la capa roja", "Inventor blindado",
                                                   "Caballero", "Astronauta", "Mago sabio", "Superh\u00E9roe",
                                                   "Supersoldado", "Dios del trueno", "Gigante esmeralda", "Esp\u00EDa", "Arquero", "Rey guerrero", "Hechicero supremo",
                                                   "Hero\u00EDna c\u00F3smica", "Bruja escarlata", "Tit\u00E1n colosal", "Tit\u00E1n de ataque", "Tit\u00E1n acorazado",
                                                   "Roedor el\u00E9ctrico", "Zorrito de pelo", "Lagartija de fuego", "Bulbo de planta", "Tortuga de agua", "Globo de canto",
                                                   "Dormil\u00F3n gigante", "Fantasma sonriente", "Gatito singular", "Pato con jaqueca", "Huevo con pinchos", "Gato de la moneda",
                                                   "Ping\u00FCino emperador", "Disfraz de trapo", "Guerrero del aura", "Drag\u00F3n cari\u00F1oso", "Masa que imita", "Clon gen\u00E9tico",
                                                   "Arbolito beb\u00E9", "Tit\u00E1n sonriente", "Tit\u00E1n bobalic\u00F3n" };
        static readonly string[] KindsEn = { "Robot", "Cat", "Bunny", "Ghost", "Slime", "Dragon", "Dog", "Chibi", "Penguin", "Panda", "Fox", "Frog", "Reindeer", "Winged lion",
                                             "Night Fury", "Light Fury", "Deadly Nadder", "Monstrous Nightmare", "Gronckle",
                                             "Blue experiment", "Pink puffball", "Little jedi", "Fluffy", "Salamander", "Electric mouse",
                                             "Console", "Cosmic cat", "Forest spirit",
                                             "Seed", "Little turtle", "Singing balloon", "Sleepyhead", "Mischievous shadow", "Psychic kitten",
                                             "Confused duck", "Little egg", "Cat with a coin", "Little penguin", "Disguised one", "Aura jackal",
                                             "Kind dragon", "Blob", "Psychic clone", "Faceless spirit", "Fire demon", "Tree spirit",
                                             "Green dinosaur", "Pac-Man", "Green block", "Crewmate", "Little ghost", "Blue drop",
                                             "White puppy", "Kitty with a bow", "Little tree", "Relaxed bear", "Chubby cat", "Floppy-eared puppy",
                                             "Invader", "Grumpy mushroom", "Shy ghost", "Little mushroom", "Mushroom", "Wind-up bomb",
                                             "Chained ball", "Angry bird", "Green piggy", "Hollow knight", "Heart cube", "Bomber",
                                             "Balloon with goggles", "Bean", "Blocky pig", "Mystery block", "Jellyfish", "Orange ball",
                                             "Scientist", "Fire girl", "Duplicant", "Curly duplicant",
                                             "Blond ninja", "Avenger ninja", "Copy ninja", "Rubber pirate", "Swordsman", "Demon slayer", "Demon sister",
                                             "Ghost girl", "Adventure boy", "Magic dog", "Console buddy", "Tracker dog", "Orange ninja",
                                             "Fan-crest ninja", "Masked ninja", "Doctor reindeer", "Winged guardian", "Beagle", "Rookie hunter", "Electric boy", "Prankster magician", "Haori boy", "Bamboo girl", "Straw hat pirate", "Green swordsman", "Leader in shades", "Mischievous kid", "Cotton puppy", "Robot kid", "Hero kid", "Swordsman piglet", "Crocodile kid",
                                             "Alchemist", "Scout soldier", "Elite captain", "Blindfolded sorcerer",
                                             "Bald hero", "Spartan warrior", "Armored soldier", "Spider hero", "Night vigilante", "Speedster", "Red-caped hero", "Armored inventor",
                                             "Knight", "Astronaut", "Wise wizard", "Superhero",
                                             "Super soldier", "God of thunder", "Emerald giant", "Spy", "Archer", "Warrior king", "Sorcerer supreme",
                                             "Cosmic heroine", "Scarlet witch", "Colossal titan", "Attack titan", "Armored titan",
                                             "Electric rodent", "Furry fox kit", "Fire lizard", "Plant bulb", "Water turtle", "Song balloon",
                                             "Giant sleepyhead", "Grinning ghost", "Unique kitten", "Duck with a headache", "Spiky egg", "Coin cat",
                                             "Emperor penguin", "Rag disguise", "Aura warrior", "Cuddly dragon", "Copycat blob", "Genetic clone",
                                             "Baby tree", "Smiling titan", "Goofy titan" };
        public static string[] Kinds { get { return I18n.IsSpanish ? KindsEs : KindsEn; } }

        public static readonly Color[,] Colors =
        {
            { Color.FromArgb(150, 110, 255), Color.FromArgb(40, 150, 245) },
            { Color.FromArgb(52, 211, 153), Color.FromArgb(6, 152, 200) },
            { Color.FromArgb(255, 128, 140), Color.FromArgb(245, 140, 30) },
            { Color.FromArgb(196, 160, 255), Color.FromArgb(236, 72, 153) },
            { Color.FromArgb(96, 165, 250), Color.FromArgb(37, 70, 235) },
            { Color.FromArgb(250, 204, 21), Color.FromArgb(249, 115, 22) },
            { Color.FromArgb(190, 242, 100), Color.FromArgb(22, 163, 74) },
            { Color.FromArgb(160, 166, 180), Color.FromArgb(70, 76, 92) },
            { Color.FromArgb(255, 176, 210), Color.FromArgb(236, 72, 153) },
            { Color.FromArgb(255, 180, 70), Color.FromArgb(234, 88, 12) },
            { Color.FromArgb(110, 110, 245), Color.FromArgb(40, 34, 96) },
            { Color.FromArgb(250, 252, 255), Color.FromArgb(178, 192, 225) },
            { Color.FromArgb(190, 90, 255), Color.FromArgb(20, 184, 230) },
            { Color.FromArgb(255, 99, 132), Color.FromArgb(190, 18, 60) },
            { Color.FromArgb(207, 242, 255), Color.FromArgb(56, 160, 248) },
            { Color.FromArgb(110, 225, 150), Color.FromArgb(21, 94, 60) },
            { Color.FromArgb(255, 226, 120), Color.FromArgb(196, 130, 8) },
            { Color.FromArgb(255, 218, 188), Color.FromArgb(236, 166, 128) },
            { Color.FromArgb(226, 190, 140), Color.FromArgb(150, 100, 60) },
            { Color.FromArgb(140, 230, 210), Color.FromArgb(30, 150, 140) },
            { Color.FromArgb(236, 234, 228), Color.FromArgb(172, 166, 160) },
            { Color.FromArgb(236, 150, 118), Color.FromArgb(196, 98, 70) },
            { Color.FromArgb(78, 80, 92), Color.FromArgb(22, 24, 30) },
            { Color.FromArgb(196, 128, 82), Color.FromArgb(120, 68, 38) },
            { Color.FromArgb(255, 222, 100), Color.FromArgb(238, 164, 40) },
            { Color.FromArgb(236, 82, 52), Color.FromArgb(150, 30, 22) },
            { Color.FromArgb(108, 150, 236), Color.FromArgb(52, 86, 182) },
            { Color.FromArgb(172, 208, 152), Color.FromArgb(98, 142, 92) },
            { Color.FromArgb(255, 226, 84), Color.FromArgb(238, 178, 30) },
            { Color.FromArgb(150, 222, 196), Color.FromArgb(62, 160, 140) },
            { Color.FromArgb(160, 218, 246), Color.FromArgb(70, 152, 214) },
            { Color.FromArgb(255, 200, 220), Color.FromArgb(240, 136, 176) },
            { Color.FromArgb(76, 128, 146), Color.FromArgb(30, 74, 94) },
            { Color.FromArgb(150, 110, 206), Color.FromArgb(72, 42, 124) },
            { Color.FromArgb(255, 226, 110), Color.FromArgb(236, 184, 50) },
            { Color.FromArgb(255, 244, 206), Color.FromArgb(234, 212, 160) },
            { Color.FromArgb(84, 142, 222), Color.FromArgb(36, 80, 160) },
            { Color.FromArgb(240, 218, 130), Color.FromArgb(204, 172, 84) },
            { Color.FromArgb(255, 184, 96), Color.FromArgb(232, 128, 44) },
            { Color.FromArgb(214, 164, 228), Color.FromArgb(160, 108, 196) },
            { Color.FromArgb(238, 232, 244), Color.FromArgb(186, 176, 206) },
            { Color.FromArgb(58, 52, 64), Color.FromArgb(16, 14, 20) },
            { Color.FromArgb(255, 186, 70), Color.FromArgb(236, 72, 30) },
            { Color.FromArgb(244, 248, 238), Color.FromArgb(200, 208, 192) },
            { Color.FromArgb(136, 214, 96), Color.FromArgb(54, 156, 56) },
            { Color.FromArgb(56, 120, 230), Color.FromArgb(22, 56, 160) },
            { Color.FromArgb(118, 196, 96), Color.FromArgb(56, 140, 52) },
            { Color.FromArgb(230, 50, 54), Color.FromArgb(140, 20, 32) },
            { Color.FromArgb(255, 64, 64), Color.FromArgb(206, 16, 30) },
            { Color.FromArgb(255, 255, 255), Color.FromArgb(214, 216, 226) },
            { Color.FromArgb(206, 152, 94), Color.FromArgb(150, 98, 52) },
            { Color.FromArgb(196, 192, 198), Color.FromArgb(136, 130, 142) },
            { Color.FromArgb(255, 236, 60), Color.FromArgb(240, 186, 10) },
            { Color.FromArgb(255, 190, 240), Color.FromArgb(236, 110, 200) },
            { Color.FromArgb(110, 240, 255), Color.FromArgb(20, 180, 220) },
            { Color.FromArgb(255, 196, 100), Color.FromArgb(240, 140, 30) },
            { Color.FromArgb(172, 106, 60), Color.FromArgb(112, 62, 32) },
            { Color.FromArgb(255, 226, 196), Color.FromArgb(236, 182, 142) },
            { Color.FromArgb(244, 62, 50), Color.FromArgb(184, 22, 22) },
            { Color.FromArgb(78, 78, 94), Color.FromArgb(20, 20, 30) },
            { Color.FromArgb(236, 52, 48), Color.FromArgb(170, 20, 26) },
            { Color.FromArgb(150, 224, 90), Color.FromArgb(84, 172, 46) },
            { Color.FromArgb(52, 58, 80), Color.FromArgb(18, 20, 32) },
            { Color.FromArgb(214, 218, 224), Color.FromArgb(140, 146, 156) },
            { Color.FromArgb(244, 72, 48), Color.FromArgb(186, 30, 20) },
            { Color.FromArgb(255, 160, 206), Color.FromArgb(236, 96, 166) },
            { Color.FromArgb(246, 176, 176), Color.FromArgb(214, 128, 136) },
            { Color.FromArgb(255, 206, 60), Color.FromArgb(222, 138, 18) },
            { Color.FromArgb(180, 255, 150), Color.FromArgb(86, 200, 76) },
            { Color.FromArgb(255, 160, 80), Color.FromArgb(226, 104, 40) },
            { Color.FromArgb(255, 192, 216), Color.FromArgb(242, 134, 176) },
            { Color.FromArgb(120, 214, 80), Color.FromArgb(36, 140, 44) },
            { Color.FromArgb(244, 241, 236), Color.FromArgb(198, 192, 186) },
            { Color.FromArgb(156, 104, 72), Color.FromArgb(108, 70, 46) },
            { Color.FromArgb(248, 212, 180), Color.FromArgb(220, 170, 134) },
            { Color.FromArgb(255, 234, 216), Color.FromArgb(236, 194, 168) },
            { Color.FromArgb(255, 251, 226), Color.FromArgb(226, 216, 180) }
        };
        static readonly string[] ColorNamesEs = { "Aurora", "Menta", "Coral", "Lavanda", "Oc\u00E9ano", "Sol", "Lima", "Grafito",
                                                       "Chicle", "Calabaza", "Noche", "Nieve", "Galaxia", "Cereza", "Hielo", "Bosque", "Oro", "Melocot\u00F3n", "Canela", "Turquesa", "Ceniza", "Arcilla", "Medianoche", "Chocolate", "Miel", "Brasa", "A\u00F1il", "Salvia", "El\u00E9ctrico", "Hoja", "Agua", "Algod\u00F3n", "Petr\u00F3leo", "Morado",
                                                       "Pato", "Crema", "Azul real", "Trapo", "Naranja", "Lila", "Perla", "Sombra", "Fuego", "Musgo blanco",
                                                       "Yoshi", "Azul intenso", "Creeper", "Tripulante", "Blinky", "Blanco", "Osito", "Gris gato",
                                                       "Comecocos", "Pinky", "Inky", "Clyde", "Goomba", "Piel", "Champi\u00F1\u00F3n", "Bob-omb", "P\u00E1jaro rojo", "Cerdito",
                                                       "Capa", "Cubo", "Pooka", "Fall Guy", "Cerdo", "Bloque", "Metroid", "Waddle Dee", "Kirby", "1-Up", "Palidez", "Tostado", "Duplicante", "Piel anime", "Fantasma" };
        static readonly string[] ColorNamesEn = { "Aurora", "Mint", "Coral", "Lavender", "Ocean", "Sun", "Lime", "Graphite",
                                                  "Bubblegum", "Pumpkin", "Night", "Snow", "Galaxy", "Cherry", "Ice", "Forest", "Gold", "Peach", "Cinnamon", "Turquoise", "Ash", "Clay", "Midnight", "Chocolate", "Honey", "Ember", "Indigo", "Sage", "Electric", "Leaf", "Water", "Cotton", "Petrol", "Purple",
                                                  "Duck", "Cream", "Royal blue", "Rag", "Orange", "Lilac", "Pearl", "Shadow", "Fire", "White moss",
                                                  "Yoshi", "Deep blue", "Creeper", "Crewmate", "Blinky", "White", "Teddy", "Cat gray",
                                                  "Pac-Man", "Pinky", "Inky", "Clyde", "Goomba", "Skin", "Mushroom", "Bob-omb", "Red bird", "Piggy",
                                                  "Cape", "Cube", "Pooka", "Fall Guy", "Pig", "Block", "Metroid", "Waddle Dee", "Kirby", "1-Up", "Pale", "Tan", "Duplicant", "Anime skin", "Ghost" };
        public static string[] ColorNames { get { return I18n.IsSpanish ? ColorNamesEs : ColorNamesEn; } }

        static readonly string[] EyeNamesEs = { "Brillo", "Kawaii", "Puntitos", "Anime", "Chulo", "Estrella", "Pesta\u00F1as", "Drag\u00F3n" };
        static readonly string[] EyeNamesEn = { "Sparkle", "Kawaii", "Dots", "Anime", "Cool", "Star", "Lashes", "Dragon" };
        public static string[] EyeNames { get { return I18n.IsSpanish ? EyeNamesEs : EyeNamesEn; } }

        public const int HatPumpkin = 4, HatSanta = 11, HatCrown = 13, HatHalo = 14;
        static readonly string[] HatNamesEs = { "Ninguno", "Gorra", "Gorro de lana", "Sombrero de bruja", "Calabaza", "Cuernos",
                                                     "Chistera", "Fiesta", "Vaquero", "Auriculares", "Lazo", "Pap\u00E1 Noel", "Brote",
                                                     "Corona", "Aureola", "Boina", "Gorro de chef", "Flores", "Pelo rubio", "Pelo casta\u00F1o", "Pelo de punta", "Pelo blanco", "Pelo granate", "Capucha de oso", "Banda ninja", "Sombrero de paja", "Pelo negro", "Ninja rubio", "Casco de marine", "Casco de caballero", "Casco de astronauta", "Chispa",
                                                     "Pelo de punta oscuro", "Pelo magenta", "Ninja de pelo oscuro", "Pelo verde", "Pelo de llamas", "Melena negra",
                                                     "Ninja de pelo plateado", "Capucha de murci\u00E9lago", "Casco rel\u00E1mpago", "Pelo con rizo", "Casco de armadura",
                                                     "Sombrero de doctor", "Pelo azul salvaje", "Gorro de dormir",
                                                     "Pelo de Gojo", "Pelo de Killua", "Pelo de Levi", "Pelo de Shin-chan" };
        static readonly string[] HatNamesEn = { "None", "Cap", "Beanie", "Witch hat", "Pumpkin", "Horns",
                                                "Top hat", "Party hat", "Cowboy hat", "Headphones", "Bow", "Santa hat", "Sprout",
                                                "Crown", "Halo", "Beret", "Chef's hat", "Flowers", "Blond hair", "Brown hair", "Spiky hair", "White hair", "Maroon hair", "Bear hood", "Ninja headband", "Straw hat", "Black hair", "Blond ninja", "Marine helmet", "Knight helmet", "Astronaut helmet", "Spark",
                                                "Dark spiky hair", "Magenta hair", "Dark-haired ninja", "Green hair", "Flame hair", "Long black hair",
                                                "Silver-haired ninja", "Bat cowl", "Lightning helmet", "Hair with a curl", "Armor helmet",
                                                "Doctor's hat", "Wild blue hair", "Nightcap",
                                                "Gojo's hair", "Killua's hair", "Levi's hair", "Shin-chan's hair" };
        public static string[] HatNames { get { return I18n.IsSpanish ? HatNamesEs : HatNamesEn; } }
        static readonly string[] OutfitNamesEs = { "Nada", "Bufanda", "Pajarita", "Corbata", "Capa de vampiro", "Capa de h\u00E9roe",
                                                        "Cascabel", "Cadena de oro", "Capa verde", "Abrigo rojo", "Chaleco ninja", "Kimono de lucha", "Haori a cuadros", "Mochila", "Chaleco rojo", "Camiseta roja", "Botones de consola", "Panza del bosque", "Ch\u00E1ndal naranja",
                                                        "Hombrera", "Armadura verde", "Traje espacial", "Chaqueta verde", "Camiseta morada", "Traje de buf\u00F3n",
                                                        "Cuello alto azul", "Faja verde", "Armadura blanca", "Traje amarillo", "Kimono rosa", "Chaleco verde",
                                                        "Traje ar\u00E1cnido", "Traje de murci\u00E9lago", "Traje rel\u00E1mpago", "Traje de acero", "Armadura roja",
                                                        "Pantal\u00F3n corto", "Capa roja y tatuajes" };
        static readonly string[] OutfitNamesEn = { "None", "Scarf", "Bow tie", "Tie", "Vampire cape", "Hero cape",
                                                   "Bell", "Gold chain", "Green cape", "Red coat", "Ninja vest", "Martial arts gi", "Checkered haori", "Backpack", "Red vest", "Red T-shirt", "Console buttons", "Forest belly", "Orange tracksuit",
                                                   "Shoulder pad", "Green armor", "Spacesuit", "Green jacket", "Purple T-shirt", "Jester suit",
                                                   "Blue turtleneck", "Green sash", "White armor", "Yellow suit", "Pink kimono", "Green vest",
                                                   "Spider suit", "Bat suit", "Lightning suit", "Steel suit", "Red armor",
                                                   "Shorts", "Red cape and tattoos" };
        public static string[] OutfitNames { get { return I18n.IsSpanish ? OutfitNamesEs : OutfitNamesEn; } }
        static readonly string[] FaceNamesEs = { "Nada", "Gafas de sol", "Gafas pixel", "Gafas redondas", "Mon\u00F3culo", "Antifaz",
                                                      "Parche pirata", "Bigote", "Venda", "Cejas gordas", "Mofletes rojos", "Marcas de bigote", "Tatuaje y barba", "Barba de mago", "Terminal",
                                                      "Estrella y l\u00E1grima", "Bozal de bamb\u00FA", "M\u00E1scara ninja", "M\u00E1scara ar\u00E1cnida", "Nariz azul", "Gafas triangulares" };
        static readonly string[] FaceNamesEn = { "None", "Sunglasses", "Pixel shades", "Round glasses", "Monocle", "Eye mask",
                                                 "Pirate eye patch", "Mustache", "Blindfold", "Bushy eyebrows", "Rosy cheeks", "Whisker marks", "Tattoo and beard", "Wizard beard", "Terminal",
                                                 "Star and teardrop", "Bamboo muzzle", "Ninja mask", "Spider mask", "Blue nose", "Triangle shades" };
        public static string[] FaceNames { get { return I18n.IsSpanish ? FaceNamesEs : FaceNamesEn; } }
        static readonly string[] PersonalitiesEs = { "Alegre", "Tranquilo", "Gamberro" };
        static readonly string[] PersonalitiesEn = { "Cheerful", "Calm", "Mischievous" };
        public static string[] Personalities { get { return I18n.IsSpanish ? PersonalitiesEs : PersonalitiesEn; } }

        // ---- Friendship: one point per capture. A few items unlock along the way, like companions that grow with you.
        static readonly int[] LevelAt = { 0, 10, 30, 75, 150, 300 };
        static readonly string[] LevelNamesEs = { "Reci\u00E9n llegados", "Colegas", "Amigos", "Mejores amigos", "Inseparables", "Leyenda" };
        static readonly string[] LevelNamesEn = { "Newcomers", "Buddies", "Friends", "Best Friends", "Inseparable", "Legend" };
        public static string[] LevelNames { get { return I18n.IsSpanish ? LevelNamesEs : LevelNamesEn; } }

        public static int Level(int love)
        {
            int l = 0;
            for (int i = 0; i < LevelAt.Length; i++) if (love >= LevelAt[i]) l = i;
            return l;
        }

        // Progress towards the next level (0-1) and points still needed; at the top level, 1 and 0.
        public static double Progress(int love, out int left)
        {
            int l = Level(love);
            if (l >= LevelAt.Length - 1) { left = 0; return 1; }
            left = LevelAt[l + 1] - love;
            return (love - LevelAt[l]) / (double)(LevelAt[l + 1] - LevelAt[l]);
        }

        public const int ColorGalaxy = 12, ColorGold = 16;
        public static int ColorUnlock(int i) { return i == ColorGalaxy ? 2 : i == ColorGold ? 5 : 0; }
        public static int HatUnlock(int i) { return i == HatCrown ? 3 : i == HatHalo ? 4 : 0; }
        public static int UnlockLove(int level) { return LevelAt[Math.Max(0, Math.Min(LevelAt.Length - 1, level))]; }

        // Ready-made looks: kind, color, eyes, hat, outfit, face.
        static readonly string[] StyleNamesEs = { "Cl\u00E1sico", "Halloween", "Chulo", "Cute", "Vampiro", "Dragoncito", "Pirata", "Chef", "Vaquero", "Doctor", "Brujita", "M\u00FAsico", "Invierno", "Enamorado", "Fiestero", "Aventurero", "Primavera", "Campe\u00F3n" };
        static readonly string[] StyleNamesEn = { "Classic", "Halloween", "Cool", "Cute", "Vampire", "Little Dragon", "Pirate", "Chef", "Cowboy", "Doctor", "Little Witch", "Musician", "Winter", "In Love", "Party Goer", "Adventurer", "Spring", "Champion" };
        public static string[] StyleNames { get { return I18n.IsSpanish ? StyleNamesEs : StyleNamesEn; } }
        static readonly int[,] Styles =
        {
            { 0, 0, 0, 0, 0, 0, 0 },
            { 3, 11, 1, 3, 0, 0, 0 },
            { 0, 10, 4, 1, 7, 1, 0 },
            { 2, 8, 1, 10, 6, 0, 0 },
            { 1, 10, 3, 5, 4, 0, 0 },
            { 5, 6, 1, 12, 1, 0, 0 },
            { 1, 22, 4, 15, 7, 6, 0 },
            { 2, 20, 1, 16, 2, 7, 0 },
            { 6, 23, 2, 8, 1, 0, 0 },
            { 8, 11, 0, 42, 3, 3, 0 },
            { 1, 3, 3, 3, 4, 0, 7 },
            { 9, 7, 1, 9, 0, 1, 0 },
            { 9, 11, 1, 2, 0, 0, 4 },
            { 2, 8, 1, 10, 6, 0, 1 },
            { 10, 5, 1, 7, 2, 0, 2 },
            { 11, 15, 3, 12, 0, 0, 3 },
            { 3, 19, 1, 17, 0, 10, 5 },
            { 5, 16, 4, 13, 5, 0, 8 }
        };

        public static void ApplyStyle(MascotLook l, int style)
        {
            l.Kind = Styles[style, 0]; l.Color = Styles[style, 1]; l.Eyes = Styles[style, 2];
            l.Hat = Styles[style, 3]; l.Outfit = Styles[style, 4]; l.Face = Styles[style, 5]; l.Accessory = Styles[style, 6];
        }

        // A random look that only uses unlocked items.
        public static void Randomize(MascotLook l, int level, Random rnd)
        {
            do l.Kind = KindLatest(rnd.Next(Kinds.Length)); while (KindRetired(l.Kind));
            do l.Color = rnd.Next(ColorNames.Length); while (ColorUnlock(l.Color) > level);
            l.Eyes = rnd.Next(EyeNames.Length);
            do l.Hat = rnd.Next(HatNames.Length); while (HatUnlock(l.Hat) > level);
            l.Outfit = rnd.Next(OutfitNames.Length);
            l.Face = rnd.NextDouble() < 0.45 ? 0 : rnd.Next(FaceNames.Length);
            l.Accessory = rnd.NextDouble() < 0.4 ? 0 : rnd.Next(AccessoryNames.Length);
        }

        // ---- Geometry, in units of D (the mascot box width), relative to the body center.
        public struct Geo
        {
            public float HeadW, Top, FaceY, EyeGap, NeckY, NeckW, Bottom, HandX, HandY, HatW;
        }

        public static Geo GeoFor(int kind)
        {
            if (IsExtra(kind)) return ExtraGeo(kind);
            Geo g = new Geo();
            switch (kind)
            {
                case 1: // cat
                case 9: // panda
                case 10: // fox
                case 5: // dragon
                case 6: // dog
                case 7: // chibi
                case 12: // reindeer
                case 13: // winged lion
                case 14: // Night Fury
                case 15: // Light Fury
                case 16: // Deadly Nadder
                case 17: // Monstrous Nightmare
                case 19: // blue experiment
                case 21: // little jedi
                case 22: // furball
                case 23: // salamander
                case 24: // electric mouse
                    g.HeadW = 0.70f; g.Top = -0.31f; g.FaceY = -0.05f; g.EyeGap = 0.125f; g.NeckY = 0.12f; g.NeckW = 0.60f;
                    g.Bottom = 0.31f; g.HandX = 0.33f; g.HandY = 0.21f; g.HatW = 0.58f;
                    break;
                case 2: // bunny
                case 8: // penguin
                    g.HeadW = 0.64f; g.Top = -0.30f; g.FaceY = -0.04f; g.EyeGap = 0.115f; g.NeckY = 0.12f; g.NeckW = 0.56f;
                    g.Bottom = 0.31f; g.HandX = 0.30f; g.HandY = 0.21f; g.HatW = 0.52f;
                    break;
                case 3: // ghost
                    g.HeadW = 0.64f; g.Top = -0.33f; g.FaceY = -0.08f; g.EyeGap = 0.11f; g.NeckY = 0.07f; g.NeckW = 0.62f;
                    g.Bottom = 0.34f; g.HandX = 0.34f; g.HandY = 0.06f; g.HatW = 0.54f;
                    break;
                case 4: // slime
                    g.HeadW = 0.78f; g.Top = -0.22f; g.FaceY = 0.03f; g.EyeGap = 0.13f; g.NeckY = 0.17f; g.NeckW = 0.70f;
                    g.Bottom = 0.33f; g.HandX = 0.40f; g.HandY = 0.21f; g.HatW = 0.50f;
                    break;
                case 20: // pink ball: round, arms high at its sides
                    g.HeadW = 0.72f; g.Top = -0.31f; g.FaceY = -0.06f; g.EyeGap = 0.072f; g.NeckY = 0.14f; g.NeckW = 0.62f;
                    g.Bottom = 0.31f; g.HandX = 0.335f; g.HandY = 0.08f; g.HatW = 0.56f;
                    break;
                case 18: // Gronckle: round and wide
                    g.HeadW = 0.78f; g.Top = -0.28f; g.FaceY = -0.03f; g.EyeGap = 0.14f; g.NeckY = 0.15f; g.NeckW = 0.66f;
                    g.Bottom = 0.32f; g.HandX = 0.38f; g.HandY = 0.22f; g.HatW = 0.56f;
                    break;
                case 25: // console: a tall box, the face on its screen
                    g.HeadW = 0.6f; g.Top = -0.33f; g.FaceY = -0.15f; g.EyeGap = 0.1f; g.NeckY = 0.02f; g.NeckW = 0.5f;
                    g.Bottom = 0.33f; g.HandX = 0.33f; g.HandY = 0.12f; g.HatW = 0.52f;
                    break;
                case 26: // cosmic cat: a big round head, eyes close together
                    g.HeadW = 0.74f; g.Top = -0.33f; g.FaceY = -0.12f; g.EyeGap = 0.06f; g.NeckY = 0.1f; g.NeckW = 0.6f;
                    g.Bottom = 0.32f; g.HandX = 0.37f; g.HandY = 0.17f; g.HatW = 0.56f;
                    break;
                case 27: // forest spirit: pear-shaped, eyes high
                    g.HeadW = 0.7f; g.Top = -0.29f; g.FaceY = -0.17f; g.EyeGap = 0.12f; g.NeckY = 0.0f; g.NeckW = 0.56f;
                    g.Bottom = 0.33f; g.HandX = 0.35f; g.HandY = 0.12f; g.HatW = 0.5f;
                    break;
                case 28: // seed: wide and low, the bulb above
                    g.HeadW = 0.76f; g.Top = -0.25f; g.FaceY = -0.02f; g.EyeGap = 0.15f; g.NeckY = 0.14f; g.NeckW = 0.64f;
                    g.Bottom = 0.31f; g.HandX = 0.38f; g.HandY = 0.2f; g.HatW = 0.54f;
                    break;
                case 29: // little turtle
                case 33: // psychic kitten
                    g.HeadW = 0.68f; g.Top = -0.3f; g.FaceY = -0.06f; g.EyeGap = 0.13f; g.NeckY = 0.12f; g.NeckW = 0.58f;
                    g.Bottom = 0.31f; g.HandX = 0.33f; g.HandY = 0.18f; g.HatW = 0.54f;
                    break;
                case 30: // singing balloon: a round ball, big eyes
                    g.HeadW = 0.74f; g.Top = -0.31f; g.FaceY = -0.04f; g.EyeGap = 0.14f; g.NeckY = 0.14f; g.NeckW = 0.62f;
                    g.Bottom = 0.31f; g.HandX = 0.37f; g.HandY = 0.12f; g.HatW = 0.56f;
                    break;
                case 31: // sleeper: big and wide
                    g.HeadW = 0.82f; g.Top = -0.27f; g.FaceY = -0.08f; g.EyeGap = 0.1f; g.NeckY = 0.12f; g.NeckW = 0.66f;
                    g.Bottom = 0.33f; g.HandX = 0.42f; g.HandY = 0.15f; g.HatW = 0.5f;
                    break;
                case 32: // shadow: wide, eyes far apart
                    g.HeadW = 0.78f; g.Top = -0.26f; g.FaceY = -0.07f; g.EyeGap = 0.13f; g.NeckY = 0.14f; g.NeckW = 0.64f;
                    g.Bottom = 0.31f; g.HandX = 0.4f; g.HandY = 0.14f; g.HatW = 0.56f;
                    break;
                case 11: // frog
                    g.HeadW = 0.80f; g.Top = -0.24f; g.FaceY = -0.2f; g.EyeGap = 0.17f; g.NeckY = 0.15f; g.NeckW = 0.70f;
                    g.Bottom = 0.31f; g.HandX = 0.38f; g.HandY = 0.21f; g.HatW = 0.56f;
                    break;
                default: // robot
                    g.HeadW = 0.72f; g.Top = -0.29f; g.FaceY = -0.012f; g.EyeGap = 0.115f; g.NeckY = 0.30f; g.NeckW = 0.46f;
                    g.Bottom = 0.29f; g.HandX = 0.445f; g.HandY = 0.24f; g.HatW = 0.60f;
                    break;
            }
            return g;
        }

        // ---- Drawing helpers

        public static Color Darker(Color c, double t) { return Mac.Mix(c, Color.Black, t); }
        public static Color Lighter(Color c, double t) { return Mac.Mix(c, Color.White, t); }

        public static void Fill(Graphics g, GraphicsPath p, RectangleF r, Color a, Color b, float angle)
        {
            if (r.Width < 0.5f || r.Height < 0.5f) return;
            using (LinearGradientBrush br = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), a, b, angle)) g.FillPath(br, p);
        }

        public static void FillSolid(Graphics g, GraphicsPath p, Color c)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Ellipse(Graphics g, Color c, float x, float y, float w, float h)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y, w, h);
        }

        public static void Line(Graphics g, Color c, float width, float x1, float y1, float x2, float y2)
        {
            using (Pen p = new Pen(c, Math.Max(0.6f, width)))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }

        public static void Stroke(Graphics g, GraphicsPath path, Color c, float width)
        {
            using (Pen p = new Pen(c, Math.Max(0.6f, width)))
            {
                p.LineJoin = LineJoin.Round; p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawPath(p, path);
            }
        }

        // Glossy highlight in the top-left of a shape, clipped to it.
        public static void Shine(Graphics g, GraphicsPath shape, RectangleF r, int alpha)
        {
            Region old = g.Clip;
            g.SetClip(shape, CombineMode.Intersect);
            RectangleF s = new RectangleF(r.X - r.Width * 0.04f, r.Y - r.Height * 0.12f, r.Width * 0.82f, r.Height * 0.62f);
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddEllipse(s);
                using (PathGradientBrush pb = new PathGradientBrush(sp))
                {
                    pb.CenterColor = Color.FromArgb(alpha, 255, 255, 255);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillEllipse(pb, s);
                }
            }
            RectangleF bottom = new RectangleF(r.X, r.Y + r.Height * 0.62f, r.Width, r.Height * 0.4f);
            using (LinearGradientBrush bb = new LinearGradientBrush(RectangleF.Inflate(bottom, 0, 1), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(55, 0, 0, 0), 90f))
                g.FillRectangle(bb, bottom);
            g.Clip = old;
            old.Dispose();
        }

        public static void Glow(Graphics g, float x, float y, float w, float h, Color c, int alpha)
        {
            if (w < 1 || h < 1) return;
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF r = new RectangleF(x - w / 2, y - h / 2, w, h);
                gp.AddEllipse(r);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillEllipse(pb, r);
                }
            }
        }

        // Superellipse: rounder than a rounded rectangle, softer than an ellipse.
        public static GraphicsPath Squircle(RectangleF r, double n)
        {
            GraphicsPath p = new GraphicsPath();
            PointF[] pts = new PointF[72];
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, a = r.Width / 2, b = r.Height / 2;
            for (int i = 0; i < pts.Length; i++)
            {
                double t = i * Math.PI * 2 / pts.Length, c = Math.Cos(t), s = Math.Sin(t);
                pts[i] = new PointF(cx + (float)(a * Math.Sign(c) * Math.Pow(Math.Abs(c), 2 / n)),
                                    cy + (float)(b * Math.Sign(s) * Math.Pow(Math.Abs(s), 2 / n)));
            }
            p.AddPolygon(pts);
            return p;
        }

        public static GraphicsPath Heart(float x, float y, float size)
        {
            GraphicsPath p = new GraphicsPath();
            float s = size / 2;
            p.AddBezier(x, y + s * 0.85f, x - s * 1.25f, y - s * 0.05f, x - s * 0.55f, y - s * 0.95f, x, y - s * 0.3f);
            p.AddBezier(x, y - s * 0.3f, x + s * 0.55f, y - s * 0.95f, x + s * 1.25f, y - s * 0.05f, x, y + s * 0.85f);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Star(float x, float y, float r, float inner)
        {
            GraphicsPath p = new GraphicsPath();
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : r * inner;
                pts[i] = new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr));
            }
            p.AddPolygon(pts);
            return p;
        }

        static GraphicsPath Poly(params PointF[] pts)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        static PointF P(float x, float y) { return new PointF(x, y); }
    }
}
