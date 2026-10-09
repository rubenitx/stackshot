// Stackshot - Asking the mascot to do things: chips on Home and its page, a menu with every command, and its behavior settings.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Stackshot
{
    public partial class HomeWindow
    {
        // The behavior settings reach the shared mascot (cheap field copies, so it can run every tick).
        void ApplyMascotBehavior()
        {
            mascot.Talks = settings.MascotTalks;
            mascot.Energy = settings.MascotEnergy;
            mascot.Chatter = settings.MascotChatter;
            mascot.Alone = settings.MascotAlone;
            mascot.SleepIdle = settings.MascotSleepIdle;
        }

        // How long between its unprompted tips, by how talkative it is.
        double TipScale { get { return settings.MascotChatter == 0 ? 3.0 : settings.MascotChatter == 2 ? 0.5 : 1.0; } }

        void MascotDo(int cmd)
        {
            if (!settings.MascotOn) return;
            ApplyMascotBehavior();
            mascot.Perform(cmd);
            Wake();
        }

        MacButton CommandButton(int cmd, int height)
        {
            MacButton b = new MacButton(MascotCmd.Names[cmd], ButtonKind.Secondary, MascotCmd.Glyphs[cmd], null, height);
            b.ToolTip = MascotCmd.Hints[cmd];
            int c = cmd;
            b.Click += delegate { MascotDo(c); };
            return b;
        }

        // The usual commands side by side, and a button with all of them.
        FrameworkElement CommandChips(bool vertical)
        {
            Panel row = vertical ? (Panel)new StackPanel() : new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 560 };
            foreach (int cmd in MascotCmd.Quick)
            {
                MacButton b = CommandButton(cmd, 28);
                b.Margin = vertical ? new Thickness(0, 0, 0, 7) : new Thickness(4, 4, 4, 0);
                if (vertical) b.HorizontalAlignment = HorizontalAlignment.Left;
                row.Children.Add(b);
            }
            MacButton more = new MacButton("M\u00E1s", ButtonKind.Plain, "down", null, 28);
            more.ToolTip = "Todo lo que sabe hacer";
            more.Margin = vertical ? new Thickness(0) : new Thickness(4, 4, 4, 0);
            if (vertical) more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Click += delegate { CommandMenu(more); };
            row.Children.Add(more);
            return row;
        }

        // One small button that opens the menu, for places with no room for chips.
        FrameworkElement CommandPill()
        {
            MacButton b = new MacButton("Juega", ButtonKind.Secondary, "play", null, 28);
            b.ToolTip = "P\u00EDdele algo a " + settings.MascotName;
            b.Click += delegate { CommandMenu(b); };
            return b;
        }

        Popup CommandMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title("P\u00EDdele algo a " + settings.MascotName));
            for (int i = 0; i < MascotCmd.Count; i++)
            {
                int c = i;
                MenuEntry e = MenuEntry.Item(MascotCmd.Short[c], MascotCmd.Glyphs[c], false, delegate { MascotDo(c); });
                e.Detail = MascotCmd.Hints[c];
                m.Add(e);
                if (c == MascotCmd.Sleep || c == MascotCmd.Love) m.Add(MenuEntry.Line());
            }
            return MacMenu.ShowGrid(anchor, null, m, 4);
        }

        // ---- Behavior, in the mascot settings

        void BuildMascotBehavior()
        {
            Func<bool> shown = delegate { return settings.MascotOn; };
            Group("Comportamiento",
                new SegRow("Actividad", "Cada cu\u00E1nto hace cosas por su cuenta.", "hop", Mac.Orange, Mac.Pink, new string[] { "Tranquila", "Normal", "Juguetona" },
                           delegate { return settings.MascotEnergy; }, delegate(int i) { settings.MascotEnergy = i; ApplyMascotBehavior(); mascot.Perform(i == 0 ? MascotCmd.Bow : i == 1 ? MascotCmd.Wave : MascotCmd.Jump); Wake(); owner.MascotChanged(); }) { When = shown },
                new SegRow("Charla", "Cu\u00E1nto comenta lo que hace y te cuenta trucos.", "sparkle", Mac.Blue, Mac.Purple, new string[] { "Poca", "Normal", "Mucha" },
                           delegate { return settings.MascotChatter; }, delegate(int i) { settings.MascotChatter = i; ApplyMascotBehavior(); if (settings.MascotTalks) mascot.Say(MascotTalk.Hello(settings), 3000); Wake(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Se duerme sola", "Si no le haces caso un rato, bosteza y se echa una siesta.", "moon", Mac.Indigo, Mac.Purple,
                              delegate { return settings.MascotSleepIdle; }, delegate(bool v) { settings.MascotSleepIdle = v; ApplyMascotBehavior(); owner.MascotChanged(); }) { When = shown },
                new ToggleRow("Recorre todas las pantallas", "En el escritorio pasea de un monitor a otro en vez de quedarse en uno.", "screen", Mac.Teal, Mac.Blue,
                              delegate { return settings.MascotAllScreens; }, delegate(bool v) { settings.MascotAllScreens = v; owner.MascotChanged(); }) { When = shown });
            Group("Lo que hace por su cuenta",
                AloneRow("Saltitos y gestos", "Hops, gui\u00F1os y estiramientos.", "hop", MascotCmd.AloneSmall),
                AloneRow("Bailes y piruetas", "Bailar, girar, voltereta y pompas.", "sound", MascotCmd.AloneDance),
                AloneRow("Juegos con objetos", "Malabares, caf\u00E9, consola, lectura y fotos.", "play", MascotCmd.AloneProps),
                AloneRow("Saludos y miradas", "Saluda, mira alrededor y hace reverencias.", "heart", MascotCmd.AloneLook));
        }

        ToggleRow AloneRow(string title, string sub, string icon, int bit)
        {
            return new ToggleRow(title, sub, icon, Mac.Teal, Mac.Blue,
                                 delegate { return (settings.MascotAlone & bit) != 0; },
                                 delegate(bool v) { settings.MascotAlone = v ? settings.MascotAlone | bit : settings.MascotAlone & ~bit; ApplyMascotBehavior(); owner.MascotChanged(); })
                { When = delegate { return settings.MascotOn; } };
        }
    }
}
