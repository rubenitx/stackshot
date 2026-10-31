// Stackshot - The classic game characters' tricks, as in their games: Pac-Man eats a row of dots and a power pellet
// and a frightened ghost runs off; the ghosts turn blue and flash white; Boo hides its face (also when the pointer comes
// close) and then pops out; Bob-omb flashes red and goes off in a harmless puff; Chain Chomp lunges; the Creeper hisses,
// swells, flashes white and pops; Red is launched as from the slingshot; the ? block bumps and a coin pops out. Plus
// each character's own lines. Fan tributes, not affiliated with their owners (see the README).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static partial class MascotParts
    {
        public const int PowerChomp = 13, PowerScared = 14, PowerShy = 15, PowerBoom = 16, PowerLunge = 17, PowerHiss = 18,
                         PowerSling = 19, PowerCoin = 20;

        public static bool ArcadeHasPower(int kind)
        {
            return kind == KindPacman || kind == KindBlinky || kind == KindBoo || kind == KindBobomb || kind == KindChomp ||
                   kind == KindCreeper || kind == KindRedBird || kind == KindQBlock;
        }

        static string ArcadePowerName(int kind)
        {
            switch (kind)
            {
                case KindPacman: return I18n.T("C\u00F3mete los puntitos", "Eat the dots");
                case KindBlinky: return I18n.T("Ponte azul de miedo", "Turn blue with fright");
                case KindBoo: return I18n.T("T\u00E1pate la cara", "Hide your face");
                case KindBobomb: return I18n.T("Explota (sin da\u00F1o)", "Explode (harmlessly)");
                case KindChomp: return I18n.T("Da un tir\u00F3n", "Lunge");
                case KindCreeper: return I18n.T("Sisea", "Hiss");
                case KindRedBird: return I18n.T("Al tirachinas", "Into the slingshot");
                default: return I18n.T("Suelta una moneda", "Drop a coin");
            }
        }

        static string ArcadePowerHint(int kind)
        {
            switch (kind)
            {
                case KindPacman: return I18n.T("Se come una fila de puntitos y una p\u00EDldora, y un fantasma huye asustado", "Eats a row of dots and a power pellet, and a ghost runs off scared");
                case KindBlinky: return I18n.T("Se vuelve azul, tiembla y parpadea en blanco antes de volver", "Turns blue, trembles and flashes white before turning back");
                case KindBoo: return I18n.T("Se tapa la cara con las manos y luego da un susto", "Covers its face with its hands, then gives you a scare");
                case KindBobomb: return I18n.T("Parpadea en rojo y hace \u00ABpum\u00BB con humo", "Flashes red and goes \u201Cboom\u201D in a puff of smoke");
                case KindChomp: return I18n.T("Se lanza hacia delante tirando de la cadena", "Lunges forward, tugging at its chain");
                case KindCreeper: return I18n.T("Sisea, se hincha, se pone blanco y hace \u00ABpof\u00BB", "Hisses, swells, turns white and goes \u201Cpoof\u201D");
                case KindRedBird: return I18n.T("Se tensa y sale disparado dando vueltas", "Pulls back and shoots off spinning");
                default: return I18n.T("Da un salto y sale una moneda girando", "Bumps up and a spinning coin pops out");
            }
        }

        static string ArcadePowerLine(int power)
        {
            switch (power)
            {
                case PowerChomp: return I18n.T("\u00A1Waka waka waka!", "Waka waka waka!");
                case PowerScared: return I18n.T("\u00A1Ay, que viene Pac-Man!", "Eek, here comes Pac-Man!");
                case PowerShy: return I18n.T("No me mires\u2026", "Don't look at me\u2026");
                case PowerBoom: return I18n.T("Tic, tac, tic\u2026", "Tick, tock, tick\u2026");
                case PowerLunge: return I18n.T("\u00A1CHOMP!", "CHOMP!");
                case PowerHiss: return "Sssssss\u2026";
                case PowerSling: return I18n.T("\u00A1Yaaaaa!", "Yaaaaa!");
                default: return I18n.T("\u00A1Ding!", "Ding!");
            }
        }

        // Colours while a trick runs: the ghosts frightened blue (white flashes as it wears off), Bob-omb flashing red
        // faster and faster, the Creeper flashing white.
        static void ArcadeTint(MascotLook l, MascotPose p, ref Color c1, ref Color c2)
        {
            if (p.Fx <= 0) return;
            if (l.Kind == KindBlinky && p.Power == PowerScared)
            {
                bool white = p.Fx < 0.28 && ((long)(p.Now / 160.0)) % 2 == 0;
                c1 = white ? Color.FromArgb(250, 250, 255) : Color.FromArgb(60, 90, 240);
                c2 = white ? Color.FromArgb(200, 206, 230) : Color.FromArgb(24, 36, 170);
            }
            else if (l.Kind == KindBobomb && p.Power == PowerBoom)
            {
                double k = 0.5 + 0.5 * Math.Sin(p.Now / (140.0 - 100.0 * p.Fx));
                c1 = Mac.Mix(c1, Color.FromArgb(240, 50, 40), k * p.Fx);
                c2 = Mac.Mix(c2, Color.FromArgb(150, 16, 16), k * p.Fx);
            }
            else if (l.Kind == KindCreeper && p.Power == PowerHiss)
            {
                double k = (0.5 + 0.5 * Math.Sin(p.Now / 70.0)) * p.Fx;
                c1 = Mac.Mix(c1, Color.White, k * 0.85);
                c2 = Mac.Mix(c2, Color.FromArgb(230, 236, 230), k * 0.85);
            }
        }

        // ---- Each character's own lines, mixed in with the usual ones (null when it has none).
        public static string[] OwnLines(int kind)
        {
            if (IsKlei(kind)) return KleiLines(kind);
            if (IsToon(kind)) return ToonLines(kind);
            return OwnLinesOf(kind);
        }

        static string[] OwnLinesOf(int kind)
        {
            return I18n.IsSpanish ? OwnLinesEs(kind) : OwnLinesEn(kind);
        }

        static string[] OwnLinesEn(int kind)
        {
            switch (kind)
            {
                case KindNightFury: return new string[] { "Mrrr\u2026 got any fish?", "Plasma ready!" };
                case KindLightFury: return new string[] { "Now you see me\u2026 now you don't." };
                case KindNadder: return new string[] { "Check out these spines!" };
                case KindNightmare: return new string[] { "I light up whenever I want!" };
                case KindGronckle: return new string[] { "One more rock and I'm taking a nap." };
                case KindStitch: return new string[] { "Ohana!", "Ohana means family.", "Meega nala kweesta!" };
                case KindKirby: return new string[] { "Poyo!", "Hai!", "I'm hungry\u2026 poyo!" };
                case KindGrogu: return new string[] { "Mmm\u2026", "A little blue cookie?" };
                case KindEevee: return new string[] { "Vee!", "Eevee!" };
                case KindCharmander: return new string[] { "Char!", "Charmander!" };
                case KindPikachu: return new string[] { "Pika pika!", "Pikachu!", "Pika!" };
                case KindBmo: return new string[] { "Who wants to play video games?", "Beep boop, I'm BMO.", "BMO says: capture!" };
                case KindDoraemon: return new string[] { "I've got a gadget for that!", "Anyone got dorayaki?" };
                case KindTotoro: return new string[] { "Grooaar!", "Shall we wait for the bus?" };
                case KindBulbasaur: return new string[] { "Bulba!", "Bulbasaur!" };
                case KindSquirtle: return new string[] { "Squirtle!", "Everybody chill out!" };
                case KindJigglypuff: return new string[] { "Jiggly\u2026 puuuff \u266A", "Fell asleep? Then I'm drawing on your face!" };
                case KindSnorlax: return new string[] { "Zzz\u2026", "Is it time to eat yet?", "Five more minutes\u2026" };
                case KindGengar: return new string[] { "Gen\u2026 gar\u2026 hehehe", "I'm in your shadow." };
                case KindMew: return new string[] { "Mew!", "Mew mew \u266A" };
                case KindPsyduck: return new string[] { "Psy\u2026?", "My head hurts\u2026", "Psyduck?" };
                case KindTogepi: return new string[] { "Toge toge priii!", "Togepi!" };
                case KindMeowth: return new string[] { "Meowth, that's right!", "Is that shiny? It's mine!" };
                case KindPiplup: return new string[] { "Piplup!", "Pip pip!" };
                case KindMimikyu: return new string[] { "I just want a friend\u2026", "Mimiky\u2026" };
                case KindLucario: return new string[] { "I sense your aura.", "Aura Sphere!" };
                case KindDragonite: return new string[] { "Drago!", "Want me to fly you somewhere?" };
                case KindDitto: return new string[] { "Ditto!", "I can be anything you want." };
                case KindMewtwo: return new string[] { "It's what you do with your life that matters.", "I sense your mind\u2026" };
                case KindNoFace: return new string[] { "Ah\u2026 ah\u2026" };
                case KindCalcifer: return new string[] { "I'm a very powerful fire demon!", "Give me a log!", "Don't pour water on me\u2026" };
                case KindKodama: return new string[] { "Clack, clack, clack\u2026" };
                case KindYoshi: return new string[] { "Yoshi!", "Yoshi, Yoshi!" };
                case KindPacman: return new string[] { "Waka waka!", "Waka waka waka!", "Any dots left around here?" };
                case KindCreeper: return new string[] { "Sss\u2026", "Ssssss\u2026", "Relax, I just came for a hug." };
                case KindCrewmate: return new string[] { "It wasn't me.", "Shall we do some tasks?", "Emergency button!", "That's very sus\u2026" };
                case KindBlinky: return new string[] { "Booo!", "I'll get you, Pac-Man!" };
                case KindDqSlime: return new string[] { "A wild slime appears!", "Boing, boing." };
                case KindSnoopy: return new string[] { "Time to dance!", "I need my doghouse\u2026" };
                case KindKitty: return new string[] { "\u266A", "Hello, friend." };
                case KindGroot: return new string[] { "I am Groot.", "I\u2026 am\u2026 Groot." };
                case KindRilakkuma: return new string[] { "Relax a little\u2026", "Today is a do-nothing day." };
                case KindPusheen: return new string[] { "Prrr\u2026", "Any cookies?", "Meow \u266A" };
                case KindPochacco: return new string[] { "Let's run!", "Wanna play ball?" };
                case KindInvader: return new string[] { "Beep boop beep boop\u2026", "We come in peace. More or less." };
                case KindGoomba: return new string[] { "Don't step on me!", "Grrr\u2026" };
                case KindBoo: return new string[] { "Don't look at me\u2026", "Boo!", "I only move when you're not looking." };
                case KindToad: return new string[] { "The princess is in another castle!", "Wahoo!" };
                case KindMushroom: return new string[] { "Power up!", "Eat me and you'll grow." };
                case KindBobomb: return new string[] { "Tick, tock\u2026", "Don't let me blow up!" };
                case KindChomp: return new string[] { "Woof, woof!", "CHOMP!" };
                case KindRedBird: return new string[] { "Those pigs!", "Into the slingshot!" };
                case KindGreenPig: return new string[] { "Oink, oink.", "Hehehe, the eggs are ours." };
                case KindKnight: return new string[] { "\u2026", "*tilts its head*" };
                case KindCube: return new string[] { "The companion cube will never threaten you.", "Don't leave me behind." };
                case KindBomberman: return new string[] { "Bombs away!", "Here I go!" };
                case KindPooka: return new string[] { "Don't pump me up!" };
                case KindFallGuy: return new string[] { "Yippee!", "Go for the crown!" };
                case KindMcPig: return new string[] { "Oink.", "Got a carrot?" };
                case KindQBlock: return new string[] { "?", "Hit me from below!" };
                case KindMetroid: return new string[] { "Kriii\u2026" };
                case KindWaddleDee: return new string[] { "Wadi!", "Always ready!" };
                default: return null;
            }
        }

        static string[] OwnLinesEs(int kind)
        {
            switch (kind)
            {
                case KindNightFury: return new string[] { "Mrrr\u2026 \u00BFtienes pescado?", "\u00A1Plasma listo!" };
                case KindLightFury: return new string[] { "Ahora me ves\u2026 ahora no." };
                case KindNadder: return new string[] { "\u00A1Mira qu\u00E9 p\u00FAas!" };
                case KindNightmare: return new string[] { "\u00A1Me enciendo cuando quiera!" };
                case KindGronckle: return new string[] { "Una roca m\u00E1s y me echo la siesta." };
                case KindStitch: return new string[] { "\u00A1Ohana!", "Ohana significa familia.", "\u00A1Meega nala kweesta!" };
                case KindKirby: return new string[] { "\u00A1Poyo!", "\u00A1Hai!", "Tengo hambre\u2026 \u00A1poyo!" };
                case KindGrogu: return new string[] { "Mmm\u2026", "\u00BFUna galletita azul?" };
                case KindEevee: return new string[] { "\u00A1Vee!", "\u00A1Eevee!" };
                case KindCharmander: return new string[] { "\u00A1Char!", "\u00A1Charmander!" };
                case KindPikachu: return new string[] { "\u00A1Pika pika!", "\u00A1Pikachu!", "\u00A1Pika!" };
                case KindBmo: return new string[] { "\u00BFQui\u00E9n quiere jugar a videojuegos?", "Bip bop, soy BMO.", "BMO dice: \u00A1captura!" };
                case KindDoraemon: return new string[] { "\u00A1Tengo un invento para eso!", "\u00BFAlguien tiene dorayakis?" };
                case KindTotoro: return new string[] { "\u00A1Grooaar!", "\u00BFEsperamos el autob\u00FAs?" };
                case KindBulbasaur: return new string[] { "\u00A1Bulba!", "\u00A1Bulbasaur!" };
                case KindSquirtle: return new string[] { "\u00A1Squirtle!", "\u00A1Vamo a calmarno!" };
                case KindJigglypuff: return new string[] { "Jiggly\u2026 puuuff \u266A", "\u00BFTe has dormido? \u00A1Pues te pinto la cara!" };
                case KindSnorlax: return new string[] { "Zzz\u2026", "\u00BFYa es hora de comer?", "Cinco minutitos m\u00E1s\u2026" };
                case KindGengar: return new string[] { "Gen\u2026 gar\u2026 jijiji", "Estoy en tu sombra." };
                case KindMew: return new string[] { "\u00A1Mew!", "Mew mew \u266A" };
                case KindPsyduck: return new string[] { "\u00BFPsy\u2026?", "Me duele la cabeza\u2026", "\u00BFPsyduck?" };
                case KindTogepi: return new string[] { "\u00A1Toge toge priii!", "\u00A1Togepi!" };
                case KindMeowth: return new string[] { "\u00A1Meowth, as\u00ED es!", "\u00BFEso brilla? \u00A1Es m\u00EDo!" };
                case KindPiplup: return new string[] { "\u00A1Piplup!", "\u00A1Pip pip!" };
                case KindMimikyu: return new string[] { "Solo quiero un amigo\u2026", "Mimiky\u2026" };
                case KindLucario: return new string[] { "Siento tu aura.", "\u00A1Esfera aural!" };
                case KindDragonite: return new string[] { "\u00A1Drago!", "\u00BFTe llevo volando?" };
                case KindDitto: return new string[] { "\u00A1Ditto!", "Puedo ser lo que quieras." };
                case KindMewtwo: return new string[] { "Lo que importa es lo que haces con tu vida.", "Siento tu mente\u2026" };
                case KindNoFace: return new string[] { "Ah\u2026 ah\u2026" };
                case KindCalcifer: return new string[] { "\u00A1Soy un demonio de fuego muy poderoso!", "\u00A1Dame un tronco!", "No me eches agua\u2026" };
                case KindKodama: return new string[] { "Clac, clac, clac\u2026" };
                case KindYoshi: return new string[] { "\u00A1Yoshi!", "\u00A1Yoshi, Yoshi!" };
                case KindPacman: return new string[] { "\u00A1Waka waka!", "\u00A1Waka waka waka!", "\u00BFQuedan puntitos por aqu\u00ED?" };
                case KindCreeper: return new string[] { "Sss\u2026", "Ssssss\u2026", "Tranquilo, solo vengo a abrazarte." };
                case KindCrewmate: return new string[] { "Yo no he sido.", "\u00BFHacemos tareas?", "\u00A1Bot\u00F3n de emergencia!", "Eso es muy sus\u2026" };
                case KindBlinky: return new string[] { "\u00A1Buuu!", "\u00A1Te pillar\u00E9, Pac-Man!" };
                case KindDqSlime: return new string[] { "\u00A1Un slime salvaje aparece!", "Boing, boing." };
                case KindSnoopy: return new string[] { "\u00A1Hora de bailar!", "Necesito mi caseta\u2026" };
                case KindKitty: return new string[] { "\u266A", "Hola, amigo." };
                case KindGroot: return new string[] { "Yo soy Groot.", "Yo\u2026 soy\u2026 Groot." };
                case KindRilakkuma: return new string[] { "Rel\u00E1jate un poco\u2026", "Hoy es d\u00EDa de no hacer nada." };
                case KindPusheen: return new string[] { "Prrr\u2026", "\u00BFHay galletas?", "Miau \u266A" };
                case KindPochacco: return new string[] { "\u00A1A correr!", "\u00BFJugamos al bal\u00F3n?" };
                case KindInvader: return new string[] { "Bip bup bip bup\u2026", "Venimos en paz. M\u00E1s o menos." };
                case KindGoomba: return new string[] { "\u00A1No me pises!", "Grrr\u2026" };
                case KindBoo: return new string[] { "No me mires\u2026", "\u00A1Buu!", "Solo me muevo cuando no miras." };
                case KindToad: return new string[] { "\u00A1La princesa est\u00E1 en otro castillo!", "\u00A1Wahoo!" };
                case KindMushroom: return new string[] { "\u00A1Power up!", "C\u00F3meme y crecer\u00E1s." };
                case KindBobomb: return new string[] { "Tic, tac\u2026", "\u00A1Que no me explote!" };
                case KindChomp: return new string[] { "\u00A1Guau, guau!", "\u00A1CHOMP!" };
                case KindRedBird: return new string[] { "\u00A1Esos cerdos!", "\u00A1Al tirachinas!" };
                case KindGreenPig: return new string[] { "Oink, oink.", "Jejeje, los huevos son nuestros." };
                case KindKnight: return new string[] { "\u2026", "*inclina la cabeza*" };
                case KindCube: return new string[] { "El cubo de compa\u00F1\u00EDa nunca te amenazar\u00E1.", "No me dejes atr\u00E1s." };
                case KindBomberman: return new string[] { "\u00A1Bomba va!", "\u00A1All\u00E1 voy!" };
                case KindPooka: return new string[] { "\u00A1No me infles!" };
                case KindFallGuy: return new string[] { "\u00A1Yupiii!", "\u00A1A por la corona!" };
                case KindMcPig: return new string[] { "Oink.", "\u00BFTienes una zanahoria?" };
                case KindQBlock: return new string[] { "\u00BF?", "\u00A1Golp\u00E9ame desde abajo!" };
                case KindMetroid: return new string[] { "Kriii\u2026" };
                case KindWaddleDee: return new string[] { "\u00A1Wadi!", "\u00A1Siempre listo!" };
                default: return null;
            }
        }
    }

    public partial class Mascot
    {
        double shy;   // Boo hiding its face, 0-1

        int ArcadePower()
        {
            switch (Look.Kind)
            {
                case MascotParts.KindPacman: return MascotParts.PowerChomp;
                case MascotParts.KindBlinky: return MascotParts.PowerScared;
                case MascotParts.KindBoo: return MascotParts.PowerShy;
                case MascotParts.KindBobomb: return MascotParts.PowerBoom;
                case MascotParts.KindChomp: return MascotParts.PowerLunge;
                case MascotParts.KindCreeper: return MascotParts.PowerHiss;
                case MascotParts.KindRedBird: return MascotParts.PowerSling;
                case MascotParts.KindQBlock: return MascotParts.PowerCoin;
                default: return 0;
            }
        }

        // Its front: the way it last walked (the whole drawing turns for the characters that have one).
        double Front { get { return faceLeft ? -1 : 1; } }

        // One step of a trick; false when the power is not one of these.
        bool ArcadeStep(double t)
        {
            double dir = Front;
            switch (power)
            {
                case MascotParts.PowerChomp:
                    // A row of dots and a power pellet coming into its mouth, then a frightened ghost running off.
                    if (powerStage == 0)
                    {
                        powerStage = 1;
                        for (int i = 0; i < 4; i++)
                        {
                            double x = 0.36 + i * 0.12;
                            Emit('d', x * dir, 0.0, -0.9 * dir, 0, (x - 0.24) / 0.9 * 1000, 0.032);
                        }
                        Emit('D', 0.86 * dir, 0.0, -0.9 * dir, 0, (0.86 - 0.24) / 0.9 * 1000, 0.07);
                        SetMood(Mood.Happy, 1600);
                    }
                    else if (powerStage == 1 && t >= 700)
                    {
                        powerStage = 2;
                        jumpV -= 0.8;
                        Emit('G', 0.62 * dir, -0.02, 0.45 * dir, 0, 1400, 0.16);
                    }
                    if (t > 2200) powerStart = -1;
                    return true;
                case MascotParts.PowerScared:
                    // Blue and trembling for a while, flashing white as it wears off.
                    if (powerStage == 0) { powerStage = 1; SetMood(Mood.Surprised, 1200); }
                    if (now - lastEmit > 140) { lastEmit = now; tiltV += (rnd.NextDouble() - 0.5) * 120; }
                    if (t > 3000) powerStart = -1;
                    return true;
                case MascotParts.PowerShy:
                    // Hides its face (see shy), then pops out with a scare.
                    if (powerStage == 0 && t >= 2200)
                    {
                        powerStage = 1;
                        squashX = 1.25;
                        squashY = 1.2;
                        jumpV -= 1.4;
                        SetMood(Mood.Happy, 1200);
                        if (Talks) Say(I18n.T("\u00A1BUU!", "BOO!"), 1400);
                    }
                    if (t > 3300) powerStart = -1;
                    return true;
                case MascotParts.PowerBoom:
                    // Flashing red faster and faster, then a harmless puff of smoke and a dizzy moment.
                    if (powerStage == 0 && t >= 1700)
                    {
                        powerStage = 1;
                        for (int i = 0; i < 12; i++)
                        {
                            double a = rnd.NextDouble() * Math.PI * 2, v = 0.4 + rnd.NextDouble() * 0.5;
                            Emit('O', Math.Cos(a) * 0.1, -0.05 + Math.Sin(a) * 0.1, Math.Cos(a) * v, Math.Sin(a) * v - 0.1, 900 + rnd.NextDouble() * 400, 0.09 + rnd.NextDouble() * 0.05);
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            double a = rnd.NextDouble() * Math.PI * 2, v = 0.6 + rnd.NextDouble() * 0.4;
                            Emit('B', 0, -0.05, Math.Cos(a) * v, Math.Sin(a) * v, 420, 0.07);
                        }
                        squashX = 1.45;
                        squashY = 0.62;
                        jumpV -= 2.0;
                        SetMood(Mood.Dizzy, 1500);
                        if (Talks) Say(I18n.T("\u00A1PUM!", "BOOM!"), 1500);
                    }
                    if (t > 3300) powerStart = -1;
                    return true;
                case MascotParts.PowerLunge:
                    // Crouches, then lunges forward biting, tugging at its chain.
                    if (powerStage == 0) { powerStage = 1; squashY = 0.82; squashX = 1.12; }
                    else if (powerStage == 1 && t >= 300)
                    {
                        powerStage = 2;
                        jumpV -= 1.5;
                        tiltV += 90 * dir;
                        squashX = 1.25;
                        squashY = 0.9;
                        for (int i = 0; i < 4; i++) Emit('O', -0.3 * dir, 0.3, -(0.2 + rnd.NextDouble() * 0.3) * dir, -0.1 - rnd.NextDouble() * 0.15, 600, 0.05);
                    }
                    else if (powerStage == 2 && t >= 900) { powerStage = 3; tiltV -= 70 * dir; }
                    if (t > 1600) powerStart = -1;
                    return true;
                case MascotParts.PowerHiss:
                    // Swells while it hisses and flashes, then pops in a puff of smoke.
                    if (t < 1500)
                    {
                        double k = t / 1500.0;
                        squashX = 1 + 0.22 * k;
                        squashY = 1 + 0.18 * k;
                    }
                    else if (powerStage == 0)
                    {
                        powerStage = 1;
                        for (int i = 0; i < 14; i++)
                        {
                            double a = rnd.NextDouble() * Math.PI * 2, v = 0.4 + rnd.NextDouble() * 0.5;
                            Emit('O', Math.Cos(a) * 0.12, Math.Sin(a) * 0.12, Math.Cos(a) * v, Math.Sin(a) * v - 0.1, 1000 + rnd.NextDouble() * 400, 0.1 + rnd.NextDouble() * 0.05);
                        }
                        squashX = 0.8;
                        squashY = 0.85;
                        SetMood(Mood.Dizzy, 1200);
                        if (Talks) Say(I18n.T("\u00A1Pof!", "Poof!"), 1300);
                    }
                    if (t > 2800) powerStart = -1;
                    return true;
                case MascotParts.PowerSling:
                    // Pulled back on the slingshot, then launched spinning, leaving feathers.
                    if (powerStage == 0) { powerStage = 1; squashX = 1.22; squashY = 0.8; tiltV -= 60 * dir; }
                    else if (powerStage == 1 && t >= 520)
                    {
                        powerStage = 2;
                        jumpV -= 3.2;
                        spinV += 760 * dir;
                        squashX = 0.85;
                        squashY = 1.2;
                        SetMood(Mood.Happy, 1400);
                        for (int i = 0; i < 5; i++)
                        {
                            double a = (110 + rnd.NextDouble() * 60) * Math.PI / 180;
                            Emit('F', 0, 0.1, Math.Cos(a) * 0.5 * dir, Math.Sin(a) * 0.4, 1100, 0.06);
                        }
                    }
                    if (t > 2000) powerStart = -1;
                    return true;
                case MascotParts.PowerCoin:
                    // A bump, and a spinning coin pops out of the top; the block shows empty for a moment.
                    if (powerStage == 0) { powerStage = 1; jumpV -= 1.1; squashY = 1.12; squashX = 0.94; }
                    else if (powerStage == 1 && t >= 140)
                    {
                        powerStage = 2;
                        Emit('C', 0, -0.36, 0, -1.25, 760, 0.1);
                        Sparkles(0, -0.4, 6);
                    }
                    if (t > 1700) powerStart = -1;
                    return true;
            }
            return false;
        }

        // How strong a trick's own drawing is now (1 - progress for the ghost, so it flashes as it ends).
        double ArcadeFx()
        {
            if (powerStart < 0) return 0;
            double t = now - powerStart;
            switch (power)
            {
                case MascotParts.PowerScared: return Math.Max(0, 1 - t / 3000.0);
                case MascotParts.PowerBoom: return t < 1700 ? Math.Min(1, t / 1700.0) : 0;
                case MascotParts.PowerHiss: return t < 1500 ? Math.Min(1, t / 1500.0) : 0;
                case MascotParts.PowerLunge: return t < 1300 ? 1 : 0;
                case MascotParts.PowerCoin: return t < 1500 ? 1 : 0;
                case MascotParts.PowerChomp: return t < 1600 ? 1 : 0;
                default: return 0;
            }
        }

        // Boo hides its face while its trick starts, and whenever the pointer comes close.
        void ShyStep(double dt, double mx, double my, double d, PointF mouse)
        {
            bool want = false;
            if (Look.Kind == MascotParts.KindBoo)
            {
                double dx = mouse.X - mx, dy = mouse.Y - my;
                want = (power == MascotParts.PowerShy && powerStart >= 0 && powerStage == 0) || Math.Sqrt(dx * dx + dy * dy) < d * 0.75;
            }
            shy += ((want ? 1 : 0) - shy) * (1 - Math.Exp(-dt / 0.12));
            if (shy < 0.01 && !want) shy = 0;
        }

        static bool IsArcadeParticle(char k) { return k == 'd' || k == 'D' || k == 'G' || k == 'O' || k == 'B' || k == 'C' || k == 'F'; }

        static bool ArcadeMove(Particle p, double s)
        {
            switch (p.Kind)
            {
                case 'd':
                case 'D':
                case 'G':
                    p.X += p.Vx * s;
                    p.Y += p.Vy * s;
                    return true;
                case 'O':
                case 'B':
                    Decay(ref p.X, ref p.Vx, 0.9, s);
                    Decay(ref p.Y, ref p.Vy, 0.9, s);
                    p.Y -= 0.08 * s;   // smoke rises
                    return true;
                case 'C':
                    p.Y += p.Vy * s + 1.4 * s * s;
                    p.Vy += 2.8 * s;
                    return true;
                case 'F':
                    p.X += p.Vx * s + Math.Sin(p.Age / 160.0) * 0.004;
                    p.Y += p.Vy * s;
                    p.Vy += 0.5 * s;
                    p.Rot = Math.Sin(p.Age / 200.0) * 40;
                    return true;
            }
            return false;
        }

        // Drawn upright at the particle (the frame is already moved there).
        bool PaintArcadeParticle(Graphics g, Particle p, float s, int a)
        {
            switch (p.Kind)
            {
                case 'd':
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 206, 176), -s / 2, -s / 2, s, s);
                    return true;
                case 'D':
                {
                    float k = (float)(0.85 + 0.15 * Math.Sin(p.Age / 90.0));
                    MascotParts.Glow(g, 0, 0, s * 2.2f, s * 2.2f, Color.FromArgb(255, 220, 190), a / 2);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 206, 176), -s * k / 2, -s * k / 2, s * k, s * k);
                    return true;
                }
                case 'G':
                {
                    // A frightened ghost: blue (flashing white near the end), pale eyes and a wavy mouth.
                    bool white = p.Age / p.Life > 0.6 && ((long)(p.Age / 150.0)) % 2 == 0;
                    Color body = white ? Color.FromArgb(a, 245, 245, 255) : Color.FromArgb(a, 50, 80, 236);
                    Color face = white ? Color.FromArgb(a, 230, 40, 40) : Color.FromArgb(a, 255, 206, 176);
                    float r = s / 2;
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddArc(-r, -r, s, s, 180, 180);
                        gp.AddLine(r, 0, r, r * 0.9f);
                        bool alt = ((long)(p.Age / 180.0)) % 2 == 0;
                        PointF last = new PointF(r, r * 0.9f);
                        for (int i = 1; i <= 6; i++)
                        {
                            PointF q = new PointF(r - s * i / 6f, ((i % 2 == 1) ^ alt) ? r * 0.55f : r * 0.9f);
                            gp.AddLine(last, q);
                            last = q;
                        }
                        gp.CloseFigure();
                        MascotParts.FillSolid(g, gp, body);
                    }
                    using (SolidBrush b = new SolidBrush(face))
                    {
                        g.FillRectangle(b, -r * 0.45f, -r * 0.3f, r * 0.22f, r * 0.22f);
                        g.FillRectangle(b, r * 0.23f, -r * 0.3f, r * 0.22f, r * 0.22f);
                    }
                    using (Pen mp = new Pen(face, Math.Max(1f, s * 0.06f)))
                        g.DrawLines(mp, new PointF[] { new PointF(-r * 0.6f, r * 0.3f), new PointF(-r * 0.4f, r * 0.15f), new PointF(-r * 0.2f, r * 0.3f), new PointF(0, r * 0.15f),
                                                       new PointF(r * 0.2f, r * 0.3f), new PointF(r * 0.4f, r * 0.15f), new PointF(r * 0.6f, r * 0.3f) });
                    return true;
                }
                case 'O':
                {
                    // A puff of smoke that grows as it fades.
                    float k = 1 + (float)(p.Age / p.Life) * 1.2f;
                    MascotParts.Ellipse(g, Color.FromArgb(a * 2 / 3, 214, 214, 220), -s * k / 2, -s * k / 2, s * k, s * k);
                    MascotParts.Ellipse(g, Color.FromArgb(a / 2, 250, 250, 252), -s * k * 0.3f, -s * k * 0.36f, s * k * 0.36f, s * k * 0.3f);
                    return true;
                }
                case 'B':
                    MascotParts.Glow(g, 0, 0, s * 3f, s * 3f, Color.FromArgb(255, 150, 40), a);
                    MascotParts.Ellipse(g, Color.FromArgb(a, 255, 230, 120), -s * 0.4f, -s * 0.4f, s * 0.8f, s * 0.8f);
                    return true;
                case 'C':
                {
                    // A gold coin spinning on its axis.
                    float w = s * (float)Math.Max(0.12, Math.Abs(Math.Cos(p.Age / 55.0)));
                    RectangleF c = new RectangleF(-w / 2, -s * 0.6f, w, s * 1.2f);
                    using (GraphicsPath cp = new GraphicsPath())
                    {
                        cp.AddEllipse(c);
                        using (LinearGradientBrush br = new LinearGradientBrush(RectangleF.Inflate(c, 1, 1), Color.FromArgb(a, 255, 236, 110), Color.FromArgb(a, 220, 150, 20), 0f))
                            g.FillPath(br, cp);
                        MascotParts.Stroke(g, cp, Color.FromArgb(a, 150, 96, 10), Math.Max(1f, s * 0.08f));
                    }
                    if (w > s * 0.4f) MascotParts.Line(g, Color.FromArgb(a, 196, 130, 16), Math.Max(1f, s * 0.1f), 0, -s * 0.32f, 0, s * 0.32f);
                    return true;
                }
                case 'F':
                    using (GraphicsPath fp = new GraphicsPath())
                    {
                        fp.AddEllipse(-s / 2, -s * 0.18f, s, s * 0.36f);
                        MascotParts.FillSolid(g, fp, p.Tint % 2 == 0 ? Color.FromArgb(a, 226, 46, 46) : Color.FromArgb(a, 40, 30, 30));
                    }
                    return true;
            }
            return false;
        }
    }
}
