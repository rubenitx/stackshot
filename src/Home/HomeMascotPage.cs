// Stackshot - Mascot section: the mascot on a stage (name, character, friendship) over a dock with the whole wardrobe
// that magnifies under the mouse, filters as you type and keeps favorites first and saved looks in their own tab.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using D = System.Drawing;
using Cv = System.Windows.Controls.Canvas;
using Sh = System.Windows.Shapes;

namespace Stackshot
{
    public partial class HomeWindow
    {
        const double StageBox = 200, StageBoxTop = 214, DockTile = 62, DockSlot = 74, DockItemH = 82, DockMagnify = 1.34, DockBand = 26;
        const int MaxLooks = 12, SavedSlot = -1, SurpriseSlot = -2, SaveSlot = -3;
        // The stage light falls off along a smooth bell and its wash along a smoothstep, sampled finely: linear ramps
        // between a few stops leave kinks the eye reads as rings and bands.
        static readonly double[] GlowAt = { 0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1 };
        static readonly double[] WashAt = { 0, 0.2, 0.4, 0.6, 0.8, 1 };
        const double WashEnd = 0.72;
        const int SlotAnime = 0, SlotStyle = 1, SlotKind = 2, SlotColor = 3, SlotHat = 4, SlotOutfit = 5, SlotFace = 6, SlotEyes = 7, SlotAcc = 8, SlotAction = -4, ActionsTab = 7;
        static readonly string[] DockTabNames = { "Mis looks", "Personajes", "Cuerpo", "Gorros", "Ropa", "Cara", "Extras", "Acciones" };
        static readonly int[][] DockTabSlots =
        {
            new int[0], new int[] { SlotStyle, SlotAnime }, new int[] { SlotKind, SlotColor }, new int[] { SlotHat }, new int[] { SlotOutfit },
            new int[] { SlotEyes, SlotFace }, new int[] { SlotAcc }, new int[0]
        };
        static readonly string[] PersonalityHints = { "Animado y siempre con ganas de capturar", "Calmado: habla poco y se echa la siesta antes",
                                                      "Bromista, movido y un poco chulito" };
        static readonly Color StarYellow = Ds.Rgb(255, 196, 0), LovePink = Ds.Rgb(255, 92, 140);

        int dockTab = -1;                       // the tab and the search are kept while the window lives
        string dockQuery = "";
        Grid stageRoot;                         // survives look changes: only rebuilt with the page
        Cv stageFxBack, stageFxFront;
        Sh.Rectangle stageGrainLayer;
        ScaleTransform stageHostScale, stageFloorScale;
        GradientStop stageGlowCore;
        GradientStop[] stageGlow, stageWash;
        TextBlock stageInfo, stagePersona;
        Sh.Ellipse stagePersonaDot;
        HeartsMark stageHearts;
        Border stageLove;
        int stageLoveShown;                     // the friendship the page shows
        int stageFx;                            // effect elements still on screen
        Shelf dockShelf;
        DockTabsBar dockTabsBar;
        TextBox dockSearch;
        FrameworkElement dockEmpty, dockTabsView, dockSearchView;
        bool dockTabsDim, dockSearchDim;
        DispatcherTimer dockReflowSoon;
        TextBlock dockEmptyText, dockEmptyLink;
        int dockEmptyTarget = -1;
        Border dockFadeL, dockFadeR;
        MGroup dockGroup;
        bool dockDirty, dockKeysHooked, dockWheelHooked, shimmerOn;
        LinearGradientBrush shimmerBrush;
        TranslateTransform shimmerShift;
        readonly Dictionary<Image, ShelfTile> shimmering = new Dictionary<Image, ShelfTile>();

        void BuildMascot()
        {
            Palette pal = Ds.Brushes;
            body.MaxWidth = double.PositiveInfinity;
            body.Margin = new Thickness(0);
            dockGroup = null;
            dockShelf = null;
            dockTabsBar = null;
            dockSearch = null;
            dockDirty = false;
            dockTabsDim = dockSearchDim = false;
            if (dockReflowSoon != null) dockReflowSoon.Stop();
            stageFx = 0;
            StopShimmer();
            shimmering.Clear();
            shimmerBrush = null;
            if (dockTab < 0) dockTab = FavoriteKeys().Count > 0 || SavedLooks().Count > 0 ? 0 : 1;

            MascotLook cur = MascotLook.From(settings);
            Color tint = StageTintColor(cur);
            stageTint = tint;
            Grid st = new Grid { ClipToBounds = true };
            st.SetBinding(FrameworkElement.HeightProperty, new Binding("ViewportHeight") { Source = scroller });
            stageRoot = st;
            stageLoveShown = settings.MascotLove;
            // Captures made while the window was hidden show up (hearts filling, tiles opening) as soon as it is back.
            st.IsVisibleChanged += delegate { if (st.IsVisible && st == stageRoot) MascotPageOnCaptured(); };
            HookPageCaptures();
            LinearGradientBrush bg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            stageWash = new GradientStop[WashAt.Length];
            for (int i = 0; i < WashAt.Length; i++)
            {
                stageWash[i] = new GradientStop(WashColor(tint, i), WashAt[i] * WashEnd);
                bg.GradientStops.Add(stageWash[i]);
            }
            st.Background = bg;

            // The light behind the mascot (its color crossfades on every change) and the floor it stands on.
            double cy = StageBoxTop + StageBox * 0.46, feet = StageBoxTop + StageBox * 0.9;
            RadialGradientBrush glow = new RadialGradientBrush();
            stageGlow = new GradientStop[GlowAt.Length];
            for (int i = 0; i < GlowAt.Length; i++)
            {
                stageGlow[i] = new GradientStop(GlowColor(tint, i), GlowAt[i]);
                glow.GradientStops.Add(stageGlow[i]);
            }
            stageGlowCore = stageGlow[0];
            st.Children.Add(new Sh.Ellipse
            {
                Width = 520, Height = 520, Fill = glow, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, cy - 260, 0, 0)
            });
            if (settings.MascotOn)
            {
                RadialGradientBrush fb = new RadialGradientBrush(Ds.Argb(pal.Dark ? 0.42 : 0.15, 0, 0, 0), Ds.Argb(0, 0, 0, 0));
                fb.Freeze();
                stageFloorScale = new ScaleTransform(1, 1);
                st.Children.Add(new Sh.Ellipse
                {
                    Width = 300, Height = 36, Fill = fb, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, feet - 18, 0, 0),
                    RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = stageFloorScale
                });
            }
            else stageFloorScale = null;
            stageGrainLayer = new Sh.Rectangle { Fill = StageGrain(), IsHitTestVisible = false };
            st.Children.Add(stageGrainLayer);
            stageFxBack = new Cv { IsHitTestVisible = false };
            st.Children.Add(stageFxBack);

            if (settings.MascotOn)
            {
                MascotHost host = new MascotHost(mascot, StageBox, false);
                host.HorizontalAlignment = HorizontalAlignment.Center;
                host.VerticalAlignment = VerticalAlignment.Top;
                // The host is taller than the mascot (room for hats and the speech bubble): place the mascot box itself.
                host.Margin = new Thickness(0, StageBoxTop - (host.Height - StageBox - StageBox * 0.06), 0, 0);
                stageHostScale = new ScaleTransform(1, 1);
                host.RenderTransformOrigin = new Point(0.5, (host.Height - StageBox * 0.16) / host.Height);
                host.RenderTransform = stageHostScale;
                st.Children.Add(host);
                UseMascot(host);
            }
            else
            {
                stageHostScale = null;
                st.Children.Add(StageHidden());
            }
            stageFxFront = new Cv { IsHitTestVisible = false };
            st.Children.Add(stageFxFront);

            st.Children.Add(StageInfo());
            if (settings.MascotOn) st.Children.Add(StageCommands());
            st.Children.Add(StageActions());
            st.Children.Add(DockArea());
            Add(st);
            FillDock(true);
            HookDockKeys();
            HookPageDpi();
        }

        // Moved to a monitor with another scale: the grain and the dock previews are redone for its pixels (each tile
        // keeps showing its old preview until the new one arrives).
        bool pageDpiHooked;
        void HookPageDpi()
        {
            if (pageDpiHooked) return;
            pageDpiHooked = true;
            DpiChanged += delegate { Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)MascotPageForDpi); };
        }

        void MascotPageForDpi()
        {
            if (page != "mascot" || stageRoot == null || PresentationSource.FromVisual(stageRoot) == null) return;
            if (stageGrainLayer != null) stageGrainLayer.Fill = StageGrain();
            if (dockGroup == null) return;
            MascotLook cur = MascotLook.From(settings);
            foreach (MItem it in dockGroup.Items) if (it.Cell != null && (it.Slot >= 0 || it.Saved != null)) DockPreview(it, cur);
        }

        // ---- Stage

        Color StageTintColor(MascotLook l)
        {
            if (!settings.MascotOn) return Ds.Brushes.Dark ? Ds.Rgb(150, 150, 160) : Ds.Rgb(160, 160, 170);
            int ci = Math.Max(0, Math.Min(MascotParts.Colors.GetLength(0) - 1, l.Color));
            return W(MascotParts.Colors[ci, 0]);
        }

        // Gaussian bell that reaches exactly zero at the rim.
        static Color GlowColor(Color c, int i)
        {
            const double K = 5;
            double t = GlowAt[i], f = (Math.Exp(-K * t * t) - Math.Exp(-K)) / (1 - Math.Exp(-K));
            return Ds.WithAlpha(c, (Ds.Brushes.Dark ? 0.38 : 0.30) * f);
        }

        static Color WashColor(Color c, int i)
        {
            double t = WashAt[i];
            return MixColor(StageTop(c), Ds.Brushes.Window, t * t * (3 - 2 * t));
        }

        static Color StageTop(Color c)
        {
            return Ds.Brushes.Dark ? MixColor(Ds.Rgb(40, 38, 46), c, 0.10) : MixColor(Ds.Rgb(255, 250, 245), c, 0.07);
        }

        // A whisper of grain over the stage: it dithers the 8-bit gradients so their steps never show as bands. One
        // static tile per theme and pixel density, drawn 1:1 on device pixels.
        static ImageBrush grain;
        static string grainFor;

        Brush StageGrain()
        {
            bool dark = Ds.Brushes.Dark;
            double k = DeviceScale();
            string key = dark + "@" + k.ToString("0.###", CultureInfo.InvariantCulture);
            if (grain != null && grainFor == key) return grain;
            const int N = 128;
            byte[] px = new byte[N * N * 4];
            Random r = new Random(20261009);
            for (int i = 0; i < N * N; i++)
            {
                // Triangular noise of about one level: white lifts a pixel, black lowers it (premultiplied).
                double t = r.NextDouble() - r.NextDouble();
                byte a = (byte)Math.Round(Math.Abs(t) * (dark ? 3.2 : 3.6));
                byte v = t > 0 ? a : (byte)0;
                px[i * 4] = v; px[i * 4 + 1] = v; px[i * 4 + 2] = v; px[i * 4 + 3] = a;
            }
            BitmapSource b = BitmapSource.Create(N, N, 96 * k, 96 * k, PixelFormats.Pbgra32, null, px, N * 4);
            b.Freeze();
            ImageBrush g = new ImageBrush(b)
            {
                TileMode = TileMode.Tile, Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, N / k, N / k)
            };
            RenderOptions.SetBitmapScalingMode(g, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetCachingHint(g, CachingHint.Cache);
            g.Freeze();
            grain = g;
            grainFor = key;
            return g;
        }

        static Color MixColor(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
                                  (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        // The stage light follows the look being worn or tried on. Pieces that keep the color (most of them) leave it
        // alone, so sweeping over the dock doesn't restart its animations on every tile.
        Color stageTint;
        void StageGlowTo(Color c, double ms)
        {
            if (stageGlow == null || stageWash == null || c == stageTint) return;
            stageTint = c;
            for (int i = 0; i < stageGlow.Length; i++) FadeStop(stageGlow[i], GlowColor(c, i), ms);
            for (int i = 0; i < stageWash.Length; i++) FadeStop(stageWash[i], WashColor(c, i), ms);
        }

        static void FadeStop(GradientStop s, Color to, double ms)
        {
            ColorAnimation a = new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            s.BeginAnimation(GradientStop.ColorProperty, a, HandoffBehavior.SnapshotAndReplace);
        }

        FrameworkElement StageHidden()
        {
            Palette pal = Ds.Brushes;
            StackPanel off = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, StageBoxTop + 18, 0, 0) };
            GlyphView bot = new GlyphView("bot", 58, pal.Label3, 1.4);
            bot.HorizontalAlignment = HorizontalAlignment.Center;
            off.Children.Add(bot);
            TextBlock t = Label("Escondido", Ds.Title, 22, pal.Label, new Thickness(0, 10, 0, 0));
            t.HorizontalAlignment = HorizontalAlignment.Center;
            off.Children.Add(t);
            TextBlock s = Label("Descansa hasta que lo llames.", Ds.Regular, 12.5, pal.Label2, new Thickness(0, 3, 0, 0));
            s.HorizontalAlignment = HorizontalAlignment.Center;
            off.Children.Add(s);
            MacButton show = new MacButton("Mostrar la mascota", ButtonKind.Primary, null, null, 30);
            show.HorizontalAlignment = HorizontalAlignment.Center;
            show.Margin = new Thickness(0, 16, 0, 0);
            show.Click += ShowMascotAgain;
            off.Children.Add(show);
            return off;
        }

        // Top left: the name (click to rename), what it is, and its personality and friendship as pills.
        FrameworkElement StageInfo()
        {
            Palette pal = Ds.Brushes;
            StackPanel col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(26, 48, 0, 0) };
            col.Children.Add(StageName());
            stageInfo = Label("", Ds.Regular, 12.5, pal.Label2, new Thickness(6, 0, 0, 0));
            stageInfo.HorizontalAlignment = HorizontalAlignment.Left;
            col.Children.Add(stageInfo);
            StackPanel pills = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 13, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            pills.Children.Add(PersonalityPill());
            FrameworkElement love = FriendshipPill();
            love.Margin = new Thickness(7, 0, 0, 0);
            pills.Children.Add(love);
            col.Children.Add(pills);
            RefreshStageInfo();
            return col;
        }

        // Left: the usual commands as chips, and a menu with every one.
        FrameworkElement StageCommands()
        {
            FrameworkElement chips = CommandChips(true);
            chips.HorizontalAlignment = HorizontalAlignment.Left;
            chips.VerticalAlignment = VerticalAlignment.Top;
            chips.Margin = new Thickness(32, 172, 0, 0);
            return chips;
        }

        void RefreshStageInfo()
        {
            MascotLook cur = MascotLook.From(settings);
            int level = MascotParts.Level(Math.Max(0, settings.MascotLove));
            if (stageInfo != null) stageInfo.Text = MascotParts.Kinds[cur.Kind] + "  \u00B7  Nivel " + (level + 1) + "  \u00B7  " + MascotParts.LevelNames[level];
            if (stagePersona != null) stagePersona.Text = MascotParts.Personalities[cur.Personality];
            if (stagePersonaDot != null) stagePersonaDot.Fill = Ds.Brush(PersonalityTone(cur.Personality));
        }

        static Color PersonalityTone(int p)
        {
            Palette pal = Ds.Brushes;
            Color[] tones = { pal.Orange, pal.Teal, pal.Pink };
            return tones[Math.Max(0, p) % tones.Length];
        }

        // A capsule on the stage: hover lightens it, a click does the rest.
        static Border StagePill(FrameworkElement content, string tip)
        {
            Palette pal = Ds.Brushes;
            Color rest = pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.62, 255, 255, 255), over = pal.Dark ? Ds.Argb(0.15, 255, 255, 255) : Ds.Argb(0.98, 255, 255, 255);
            SolidColorBrush bg = new SolidColorBrush(rest);
            content.VerticalAlignment = VerticalAlignment.Center;
            Border b = new Border
            {
                Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(11, 0, 12, 0), Background = bg, Child = content,
                BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.09, 255, 255, 255) : Ds.Argb(0.08, 0, 0, 0)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, ToolTip = tip
            };
            b.MouseEnter += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(over, TimeSpan.FromMilliseconds(120))); };
            b.MouseLeave += delegate { bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(rest, TimeSpan.FromMilliseconds(220))); };
            return b;
        }

        FrameworkElement PersonalityPill()
        {
            Palette pal = Ds.Brushes;
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            stagePersonaDot = new Sh.Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 0) };
            row.Children.Add(stagePersonaDot);
            stagePersona = Label("", Ds.Medium, 12, pal.Label, new Thickness(0, 0, 0, 1));
            row.Children.Add(stagePersona);
            row.Children.Add(new GlyphView("down", 12, pal.Label3, 1.8) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 1, 0, 0) });
            Border pill = StagePill(row, "Cambiar su forma de ser");
            pill.Padding = new Thickness(10, 0, 10, 0);
            Press(pill, delegate { PersonalityMenu(pill); });
            return pill;
        }

        FrameworkElement FriendshipPill()
        {
            // One heart per level: the reached ones full, the next one filling up with every capture.
            HeartsMark hearts = new HeartsMark(MascotParts.LevelNames.Length, HeartsFill());
            Border pill = StagePill(hearts, FriendshipTip());
            pill.Padding = new Thickness(10, 0, 10, 0);
            Press(pill, delegate { FriendshipMenu(pill); });
            stageHearts = hearts;
            stageLove = pill;
            return pill;
        }

        // Hearts filled: one per level reached, plus the progress towards the next.
        double HeartsFill()
        {
            int love = Math.Max(0, settings.MascotLove), left;
            double prog = MascotParts.Progress(love, out left);
            return MascotParts.Level(love) + 1 + (left > 0 ? prog : 0);
        }

        string FriendshipTip()
        {
            int love = Math.Max(0, settings.MascotLove), level = MascotParts.Level(love), left;
            MascotParts.Progress(love, out left);
            return MascotParts.LevelNames[level] + "  \u00B7  " + love + (love == 1 ? " captura juntos" : " capturas juntos") +
                   (left > 0 ? "  \u00B7  faltan " + left + " para \u00AB" + MascotParts.LevelNames[level + 1].ToLowerInvariant() + "\u00BB" : "  \u00B7  nivel m\u00E1ximo");
        }

        // A capture while the page is open: the hearts fill up in place and whatever the new level brings opens up
        // on its tile. Nothing is rebuilt, so the dock keeps its scroll and the search field its text and focus.
        void MascotPageOnCaptured()
        {
            // Each capture once, whichever notice arrives first.
            if (page != "mascot" || stageRoot == null || PresentationSource.FromVisual(stageRoot) == null || stageLoveShown == settings.MascotLove) return;
            stageLoveShown = settings.MascotLove;
            RefreshStageInfo();
            if (stageHearts != null) stageHearts.FillTo(HeartsFill(), true);
            if (stageLove != null) stageLove.ToolTip = FriendshipTip();
            RefreshDockLocks(true);
        }

        // The window only passes captures on while the mascot is out; the friendship on this page moves on regardless.
        bool pageCapturesHooked;
        void HookPageCaptures()
        {
            if (pageCapturesHooked) return;
            pageCapturesHooked = true;
            Action<string> h = delegate { if (IsVisible) MascotPageOnCaptured(); };
            owner.Captured += h;
            Closed += delegate { owner.Captured -= h; };
        }

        // Locks follow the friendship level: tiles open (or close) where they are.
        void RefreshDockLocks(bool animate)
        {
            if (dockGroup == null) return;
            int lv = MascotParts.Level(Math.Max(0, settings.MascotLove));
            MItem hot = dockGroup.Hot;
            bool hotChanged = false;
            foreach (MItem it in dockGroup.Items)
            {
                if (it.Cell == null || it.Level <= 0) continue;
                bool locked = it.Level > lv;
                if (locked == it.Locked) continue;
                it.Locked = locked;
                if (it == hot) hotChanged = true;
                if (locked) it.Cell.Lock(it.Level);
                else it.Cell.Unlock(animate);
            }
            // The tile under the mouse just opened (or closed): its label says so and the mascot tries it on (or off).
            if (hotChanged)
            {
                DockHot(hot);
                dockGroup.TryOnHot();
            }
        }

        // Top right: a random look, its settings, and whether it also walks on the desktop.
        FrameworkElement StageActions()
        {
            StackPanel col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 52, 30, 0) };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            MacButton surprise = new MacButton("Sorpr\u00E9ndeme", ButtonKind.Secondary, "sparkle", null, 30);
            surprise.ToolTip = "Un look al azar con lo que ten\u00E9is desbloqueado";
            surprise.Click += Surprise;
            row.Children.Add(surprise);
            MacButton gear = new MacButton("Ajustes", ButtonKind.Secondary, "gear", null, 30);
            gear.Margin = new Thickness(8, 0, 0, 0);
            gear.ToolTip = "Saludos, disfraz de temporada, escritorio\u2026";
            gear.Click += delegate { SetPage("mascotset", true); };
            row.Children.Add(gear);
            col.Children.Add(row);
            FrameworkElement desk = DesktopPill();
            desk.HorizontalAlignment = HorizontalAlignment.Right;
            desk.Margin = new Thickness(0, 11, 0, 0);
            col.Children.Add(desk);
            return col;
        }

        FrameworkElement DesktopPill()
        {
            Palette pal = Ds.Brushes;
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new GlyphView("screen", 14, pal.Label2, 1.6) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            row.Children.Add(Label("En el escritorio", Ds.Medium, 12, pal.Label, new Thickness(0, 0, 9, 1)));
            MacSwitch sw = new MacSwitch(settings.MascotDesktop) { VerticalAlignment = VerticalAlignment.Center, LayoutTransform = new ScaleTransform(0.82, 0.82) };
            sw.Toggled += SetDesktop;
            row.Children.Add(sw);
            Border pill = StagePill(row, "Pasea junto a la barra de tareas mientras trabajas");
            pill.Padding = new Thickness(11, 0, 5, 0);
            Press(pill, delegate { sw.IsOn = !sw.IsOn; SetDesktop(sw.IsOn); });
            return pill;
        }

        // The name; a click turns it into a field. Enter or clicking elsewhere keeps it, Esc restores it.
        FrameworkElement StageName()
        {
            Palette pal = Ds.Brushes;
            Grid slot = new Grid { Height = 42, HorizontalAlignment = HorizontalAlignment.Left };
            Border view = new Border
            {
                CornerRadius = new CornerRadius(9), Padding = new Thickness(6, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = Cursors.IBeam, ToolTip = "Cambiar el nombre"
            };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock name = Label(settings.MascotName, Ds.Title, 26, pal.Label, new Thickness(0, 0, 0, 2));
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxWidth = 214;
            row.Children.Add(name);
            GlyphView pen = new GlyphView("edit", 15, pal.Label2, 1.6);
            pen.Margin = new Thickness(8, 2, 0, 0);
            pen.VerticalAlignment = VerticalAlignment.Center;
            pen.Opacity = 0;
            row.Children.Add(pen);
            view.Child = row;
            slot.Children.Add(view);

            TextBox box = new TextBox
            {
                Width = 236, Height = 38, MaxLength = 16, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed,
                FontFamily = Ds.Display, FontWeight = FontWeights.SemiBold, FontSize = 22, TextAlignment = TextAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 8, 0),
                Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(pal.Accent),
                Background = Ds.Brush(pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.8, 255, 255, 255)),
                BorderBrush = Ds.Brush(pal.Accent), BorderThickness = new Thickness(1.5), Template = HeroFieldTemplate()
            };
            slot.Children.Add(box);

            stageName = name;
            bool editing = false, pressed = false;
            Action<bool> stop = null;
            Action outside = null;
            stop = delegate(bool keep)
            {
                if (!editing) return;
                editing = false;
                if (nameClickOutside == outside) nameClickOutside = null;
                // The typing stays in the field (a save elsewhere meanwhile never stores half a name); it reaches the
                // settings, the disk and the sidebar once, here.
                string now = keep ? CleanName(box.Text) : settings.MascotName;
                if (now != settings.MascotName) { settings.MascotName = now; settings.Save(); BuildWidget(); }
                name.Text = settings.MascotName;
                if (stageName != null) stageName.Text = settings.MascotName;   // the page may have been rebuilt meanwhile
                box.Visibility = Visibility.Collapsed;
                view.Visibility = Visibility.Visible;
            };
            view.MouseEnter += delegate { view.Background = Ds.Brush(pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.05, 0, 0, 0)); pen.Opacity = 1; };
            view.MouseLeave += delegate { view.Background = Brushes.Transparent; pen.Opacity = 0; pressed = false; };
            view.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; pressed = true; };
            view.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (!pressed || editing) return;
                pressed = false;
                box.Text = settings.MascotName;
                editing = true;
                view.Visibility = Visibility.Hidden;
                box.Visibility = Visibility.Visible;
                // A click anywhere else in the window ends the edit (clicking a plain element doesn't move the focus).
                outside = delegate { if (!box.IsMouseOver) stop(true); };
                nameClickOutside = outside;
                if (!nameHooked)
                {
                    nameHooked = true;
                    PreviewMouseLeftButtonDown += delegate { Action a = nameClickOutside; if (a != null) a(); };
                }
                box.Focus();
                box.SelectAll();
                Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate { if (editing) { Keyboard.Focus(box); box.SelectAll(); } });
            };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key != Key.Enter && e.Key != Key.Escape) return;
                e.Handled = true;
                stop(e.Key == Key.Enter);
            };
            box.LostKeyboardFocus += delegate { stop(true); };
            return slot;
        }

        TextBlock stageName;        // the name on the stage now
        Action nameClickOutside;   // set while the name is being edited
        bool nameHooked;

        void SetMascotName(string text)
        {
            settings.MascotName = CleanName(text);
            settings.Save();
            BuildWidget();
        }

        static string CleanName(string text)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in text ?? "") if (!char.IsControl(c)) b.Append(c);
            string v = b.ToString().Trim();
            if (v.Length > 16) v = v.Substring(0, char.IsHighSurrogate(v[15]) ? 15 : 16).Trim();
            if (v.Length > 0 && char.IsHighSurrogate(v[v.Length - 1])) v = v.Substring(0, v.Length - 1).Trim();   // never half an emoji
            return v.Length > 0 ? v : "Pixel";
        }

        static ControlTemplate heroFieldTemplate, dockFieldTemplate;
        static ControlTemplate HeroFieldTemplate()
        {
            if (heroFieldTemplate != null) return heroFieldTemplate;
            heroFieldTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'>" +
                "<Border CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>" +
                "<ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/></Border></ControlTemplate>");
            return heroFieldTemplate;
        }

        // Frameless: the search field draws its own capsule around it.
        static ControlTemplate DockFieldTemplate()
        {
            if (dockFieldTemplate != null) return dockFieldTemplate;
            dockFieldTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'>" +
                "<Border Background='{TemplateBinding Background}'>" +
                "<ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/></Border></ControlTemplate>");
            return dockFieldTemplate;
        }

        void ShowMascotAgain()
        {
            if (settings.MascotOn) return;
            settings.MascotOn = true;
            mascot.PopIn();
            mascot.Greet(settings.MascotTalks ? "\u00A1He vuelto!" : null);
            owner.MascotChanged();
            Changed();
            Dispatcher.BeginInvoke((Action)Rebuild);
        }

        Popup PersonalityMenu(FrameworkElement anchor)
        {
            List<MenuEntry> m = new List<MenuEntry>();
            for (int n = 0; n < MascotParts.Personalities.Length; n++)
            {
                int i = n;
                MenuEntry e = MenuEntry.Item(MascotParts.Personalities[i], null, i == settings.MascotPersonality, delegate { SetPersonality(i); });
                e.Detail = i < PersonalityHints.Length ? PersonalityHints[i] : null;
                m.Add(e);
            }
            return MacMenu.Show(anchor, m, 230);
        }

        void SetPersonality(int i)
        {
            if (i < 0 || i >= MascotParts.Personalities.Length || i == settings.MascotPersonality) return;
            MascotLook l = MascotLook.From(settings);
            l.Personality = i;
            LookChanged(l, null);
            mascot.Say(MascotTalk.Hello(settings), 3000);
            RefreshStageInfo();
        }

        // Every friendship level: the ones reached are ticked, and each says what it unlocks.
        Popup FriendshipMenu(FrameworkElement anchor)
        {
            int love = Math.Max(0, settings.MascotLove), level = MascotParts.Level(love);
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title("Amistad  \u00B7  " + love + (love == 1 ? " captura" : " capturas")));
            for (int i = 0; i < MascotParts.LevelNames.Length; i++)
            {
                MenuEntry e = MenuEntry.Item(MascotParts.LevelNames[i], null, i <= level, null);
                string gets = Unlocks(i);
                if (i > level)
                {
                    e.Enabled = false;
                    e.Detail = "A las " + MascotParts.UnlockLove(i) + " capturas" + (gets != null ? "  \u00B7  " + gets : "");
                }
                else e.Detail = gets ?? (i == 0 ? "Donde empieza todo" : "Cada captura suma");
                m.Add(e);
            }
            return MacMenu.Show(anchor, m, 250);
        }

        static string Unlocks(int level)
        {
            if (level <= 0) return null;
            List<string> r = new List<string>();
            for (int i = 0; i < MascotParts.ColorNames.Length; i++) if (MascotParts.ColorUnlock(i) == level) r.Add("color " + MascotParts.ColorNames[i].ToLowerInvariant());
            for (int i = 0; i < MascotParts.HatNames.Length; i++) if (MascotParts.HatUnlock(i) == level) r.Add(MascotParts.HatNames[i].ToLowerInvariant());
            return r.Count > 0 ? "Desbloquea " + string.Join(", ", r.ToArray()) : null;
        }

        void SetDesktop(bool on)
        {
            settings.MascotDesktop = on;
            owner.MascotChanged();
            Changed();
        }

        // ---- Look changes: the stage reacts without rebuilding anything.

        // Shows a look on the big mascot without saving it (hovering the dock); null goes back to the saved one.
        void TryLook(MascotLook l)
        {
            TryOn(l);
            StageGlowTo(StageTintColor(l ?? MascotLook.From(settings)), l != null ? 260 : 420);
        }

        // After a change is saved: the light crossfades, a burst of sparkles, and the dock follows in place.
        void AfterLook(bool burst)
        {
            MascotLook cur = MascotLook.From(settings);
            RefreshStageInfo();
            Color c = StageTintColor(cur);
            StageGlowTo(c, 560);
            if (burst) StageBurst(c);
            if (dockGroup == null) return;
            foreach (MItem it in dockGroup.Items)
            {
                if (it.Cell == null) continue;
                if (it.Slot >= 0) DockPreview(it, cur);
                it.Cell.Set(IsWorn(it, cur), it.Fav);
            }
        }

        // A soft flash and a ring behind the mascot, sparkles flying out in front, and a springy bounce.
        void StageBurst(Color c)
        {
            if (stageRoot == null || stageFxBack == null || stageFxFront == null || !settings.MascotOn) return;
            Palette pal = Ds.Brushes;
            double w = stageRoot.ActualWidth > 10 ? stageRoot.ActualWidth : LW - Side, cx = w / 2, cy = StageBoxTop + StageBox * 0.46;
            Color light = MixColor(c, Colors.White, 0.4);
            // Clicking away fast: the light and the bounce still follow, without piling up more sparkles.
            if (stageFx > 36) { StageBounce(); return; }

            RadialGradientBrush fb = new RadialGradientBrush();
            fb.GradientStops.Add(new GradientStop(Ds.WithAlpha(light, pal.Dark ? 0.62 : 0.7), 0));
            fb.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, pal.Dark ? 0.24 : 0.2), 0.5));
            fb.GradientStops.Add(new GradientStop(Ds.WithAlpha(c, 0), 1));
            fb.Freeze();
            FxGrow(FxEllipse(stageFxBack, cx, cy, 330, fb, null, 0), 0.35, 1.2, 1, 0, 640, 0);
            // A wave of light rolling outwards: a soft band near the rim instead of a hard stroke.
            Color wc = pal.Dark ? light : c;
            double wa = pal.Dark ? 0.5 : 0.4;
            RadialGradientBrush wave = new RadialGradientBrush();
            wave.GradientStops.Add(new GradientStop(Ds.WithAlpha(wc, 0), 0.74));
            wave.GradientStops.Add(new GradientStop(Ds.WithAlpha(wc, wa * 0.35), 0.86));
            wave.GradientStops.Add(new GradientStop(Ds.WithAlpha(wc, wa), 0.93));
            wave.GradientStops.Add(new GradientStop(Ds.WithAlpha(wc, wa * 0.3), 0.97));
            wave.GradientStops.Add(new GradientStop(Ds.WithAlpha(wc, 0), 1));
            wave.Freeze();
            FxGrow(FxEllipse(stageFxBack, cx, cy, 250, wave, null, 0), 0.5, 1.36, 1, 0, 820, 50);

            int n = 14;
            for (int i = 0; i < n; i++)
            {
                double ang = Math.PI * 2 * i / n + Rng.NextDouble() * 0.45 - 0.2, dist = 112 + Rng.NextDouble() * 72, size = 9 + Rng.NextDouble() * 10;
                Color sc = i % 3 == 0 ? StarYellow : i % 3 == 1 ? light : pal.Dark ? Colors.White : c;
                FxSpark(new SparkMark(sc) { Width = size, Height = size }, cx, cy, ang, dist, 640 + Rng.NextDouble() * 280, i * 12);
            }
            StageBounce();
        }

        void StageBounce()
        {
            if (stageHostScale != null)
            {
                DoubleAnimation pop = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(680)) { EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 4, EasingMode = EasingMode.EaseOut } };
                stageHostScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                stageHostScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            }
            if (stageFloorScale != null)
            {
                DoubleAnimation squash = new DoubleAnimation(1, 0.78, TimeSpan.FromMilliseconds(190)) { AutoReverse = true, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                stageFloorScale.BeginAnimation(ScaleTransform.ScaleXProperty, squash);
            }
        }

        static Sh.Ellipse FxEllipse(Cv on, double cx, double cy, double d, Brush fill, Brush stroke, double thick)
        {
            Sh.Ellipse e = new Sh.Ellipse { Width = d, Height = d, Fill = fill, Stroke = stroke, StrokeThickness = thick, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
            Cv.SetLeft(e, cx - d / 2);
            Cv.SetTop(e, cy - d / 2);
            on.Children.Add(e);
            return e;
        }

        void FxGrow(UIElement e, double s0, double s1, double o0, double o1, double ms, double delay)
        {
            ScaleTransform st = new ScaleTransform(s0, s0);
            e.RenderTransform = st;
            e.Opacity = delay > 0 ? 0 : o0;
            TimeSpan at = TimeSpan.FromMilliseconds(delay);
            DoubleAnimation grow = new DoubleAnimation(s0, s1, TimeSpan.FromMilliseconds(ms)) { BeginTime = at, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            DoubleAnimation fade = new DoubleAnimation(o0, o1, TimeSpan.FromMilliseconds(ms)) { BeginTime = at, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            FxDone(e, fade);
            e.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        void FxSpark(SparkMark s, double cx, double cy, double ang, double dist, double ms, double delay)
        {
            Cv.SetLeft(s, cx - s.Width / 2);
            Cv.SetTop(s, cy - s.Height / 2);
            s.Opacity = 0;
            stageFxFront.Children.Add(s);
            ScaleTransform sc = new ScaleTransform(0.2, 0.2);
            RotateTransform rot = new RotateTransform(0);
            // From the edge of its body outwards, so the face stays clear.
            TranslateTransform tr = new TranslateTransform(Math.Cos(ang) * StageBox * 0.36, Math.Sin(ang) * StageBox * 0.36);
            TransformGroup g = new TransformGroup();
            g.Children.Add(sc);
            g.Children.Add(rot);
            g.Children.Add(tr);
            s.RenderTransformOrigin = new Point(0.5, 0.5);
            s.RenderTransform = g;
            TimeSpan at = TimeSpan.FromMilliseconds(delay), len = TimeSpan.FromMilliseconds(ms);
            IEasingFunction outE = new CubicEase { EasingMode = EasingMode.EaseOut };
            tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(Math.Cos(ang) * dist, len) { BeginTime = at, EasingFunction = outE });
            tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(Math.Sin(ang) * dist - 14, len) { BeginTime = at, EasingFunction = outE });
            DoubleAnimationUsingKeyFrames size = new DoubleAnimationUsingKeyFrames { Duration = len, BeginTime = at };
            size.KeyFrames.Add(new EasingDoubleKeyFrame(1.1, KeyTime.FromPercent(0.3), new CubicEase { EasingMode = EasingMode.EaseOut }));
            size.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, size);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, size);
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(Rng.Next(2) == 0 ? -110 : 110, len) { BeginTime = at, EasingFunction = outE });
            DoubleAnimationUsingKeyFrames fade = new DoubleAnimationUsingKeyFrames { Duration = len, BeginTime = at };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.12)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.55)));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            FxDone(s, fade);
            s.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // Effect elements leave the tree as soon as their animation ends: nothing keeps running.
        void FxDone(UIElement e, System.Windows.Media.Animation.Timeline t)
        {
            stageFx++;
            t.Completed += delegate
            {
                Panel p = VisualTreeHelper.GetParent(e) as Panel;
                if (p != null) p.Children.Remove(e);
                stageFx = Math.Max(0, stageFx - 1);
            };
        }

        // ---- Dock

        static Color DockColor() { return Ds.Brushes.Dark ? Ds.Rgb(44, 44, 48) : Colors.White; }

        FrameworkElement DockArea()
        {
            Palette pal = Ds.Brushes;
            StackPanel col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 18) };

            Grid bar = new Grid { Height = 30 };
            DockTabsBar tabs = new DockTabsBar(DockTabNames, dockTab);
            tabs.Changed += delegate(int i) { if (i == dockTab) return; dockTab = i; FillDock(true); };
            dockTabsBar = tabs;
            // Shrinks rather than clips if the font renders wider than here.
            Viewbox tabsView = new Viewbox
            {
                Child = tabs, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 186, 0)
            };
            bar.Children.Add(tabsView);
            dockTabsView = tabsView;
            FrameworkElement search = DockSearchField();
            search.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Children.Add(search);
            dockSearchView = search;
            col.Children.Add(bar);

            Color dc = DockColor();
            // Headroom for the magnified tiles and their star; the label over a tile may reach the bar above, which then
            // steps back while it is there.
            Grid dock = new Grid { Margin = new Thickness(0, DockBand, 0, 0) };
            dock.Children.Add(new SoftShadow(18, 22, 12, pal.Dark ? 0.55 : 0.13));
            dock.Children.Add(new Border { CornerRadius = new CornerRadius(18), Background = Ds.Brush(dc), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1) });
            Shelf shelf = new Shelf();
            dockShelf = shelf;
            shelf.MouseLeave += delegate { DockLeft(); };
            shelf.MouseEnter += delegate { HookDockWheel(); };
            shelf.Unloaded += delegate
            {
                if (dockShelf != shelf) return;
                shimmering.Clear();
                StopShimmer();
                // Left the page in the middle of a try-on: the mascot goes back to what it really wears.
                if (mascot.Look.Key != MascotLook.From(settings).Key) TryOn(null);
            };
            dock.Children.Add(shelf);
            // Soft edges where tiles scroll out of sight.
            dockFadeL = DockFade(dc, true);
            dockFadeR = DockFade(dc, false);
            dock.Children.Add(dockFadeL);
            dock.Children.Add(dockFadeR);
            shelf.Edges = delegate(double l, double r)
            {
                dockFadeL.Opacity = Math.Max(0, Math.Min(1, l / 24));
                dockFadeR.Opacity = Math.Max(0, Math.Min(1, r / 24));
            };
            shelf.HudMoved = delegate(Rect r) { DockHudAt(shelf, bar, r); };

            StackPanel empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0, IsHitTestVisible = false };
            GlyphView look = new GlyphView("search", 20, pal.Label3, 1.7) { HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(look);
            dockEmptyText = Label("", Ds.Medium, 12.5, pal.Label2, new Thickness(0, 6, 0, 0));
            dockEmptyText.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Children.Add(dockEmptyText);
            dockEmptyLink = Label("", Ds.Regular, 12, pal.Accent, new Thickness(0, 3, 0, 0));
            dockEmptyLink.HorizontalAlignment = HorizontalAlignment.Center;
            dockEmptyLink.Cursor = Cursors.Hand;
            dockEmptyLink.MouseEnter += delegate { dockEmptyLink.TextDecorations = TextDecorations.Underline; };
            dockEmptyLink.MouseLeave += delegate { dockEmptyLink.TextDecorations = null; };
            dockEmptyLink.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (dockEmptyTarget < 0) return;
                dockTab = dockEmptyTarget;
                FillDock(true);
            };
            empty.Children.Add(dockEmptyLink);
            dockEmpty = empty;
            dock.Children.Add(empty);
            col.Children.Add(dock);
            return col;
        }

        static Border DockFade(Color c, bool left)
        {
            LinearGradientBrush b = new LinearGradientBrush(left ? c : Ds.WithAlpha(c, 0), left ? Ds.WithAlpha(c, 0) : c, 0);
            b.Freeze();
            return new Border
            {
                Width = 34, Margin = new Thickness(1), Background = b, IsHitTestVisible = false, Opacity = 0,
                HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                CornerRadius = left ? new CornerRadius(17, 0, 0, 17) : new CornerRadius(0, 17, 17, 0)
            };
        }

        // The label over the hovered tile floats into the bar above: whatever it would cover (the tabs or the search
        // field) fades back while it is there, so the label always reads on a clean background.
        void DockHudAt(Shelf shelf, FrameworkElement bar, Rect r)
        {
            if (shelf != dockShelf) return;
            bool overTabs = false, overSearch = false;
            if (!r.IsEmpty && PresentationSource.FromVisual(shelf) != null && PresentationSource.FromVisual(bar) != null)
            {
                Rect h = shelf.TransformToVisual(bar).TransformBounds(r);
                h.Inflate(6, 1);
                overTabs = dockTabsView != null && h.IntersectsWith(Within(dockTabsView, bar));
                // Never the field being typed in.
                overSearch = dockSearchView != null && !dockSearchView.IsKeyboardFocusWithin && h.IntersectsWith(Within(dockSearchView, bar));
            }
            DimBar(dockTabsView, ref dockTabsDim, overTabs);
            DimBar(dockSearchView, ref dockSearchDim, overSearch);
        }

        static Rect Within(FrameworkElement e, Visual to)
        {
            return e.TransformToVisual(to).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
        }

        static void DimBar(FrameworkElement e, ref bool dim, bool on)
        {
            if (e == null || dim == on) return;
            dim = on;
            Glide(e, UIElement.OpacityProperty, on ? 0.16 : 1, on ? 140 : 240);
        }

        // Filters the dock by name (accents and case don't matter). Ctrl+F jumps here, Esc clears it.
        FrameworkElement DockSearchField()
        {
            Palette pal = Ds.Brushes;
            Grid g = new Grid { Width = 172, Height = 28 };
            Color rest = pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.62, 255, 255, 255), line = pal.Dark ? Ds.Argb(0.09, 255, 255, 255) : Ds.Argb(0.08, 0, 0, 0);
            Border frame = new Border { CornerRadius = new CornerRadius(8), Background = Ds.Brush(rest), BorderBrush = Ds.Brush(line), BorderThickness = new Thickness(1) };
            g.Children.Add(frame);
            GlyphView icon = new GlyphView("search", 13, pal.Label2, 1.7) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            g.Children.Add(icon);
            TextBlock hint = Label("Buscar", Ds.Regular, 12.5, pal.Label3, new Thickness(30, 0, 0, 1));
            hint.IsHitTestVisible = false;
            hint.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(hint);
            TextBox box = new TextBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(28, 0, 26, 1),
                FontFamily = Ds.Text, FontSize = 12.5, Foreground = Ds.Brush(pal.Label), CaretBrush = Ds.Brush(pal.Accent), SelectionBrush = Ds.Brush(pal.Accent),
                MaxLength = 30, VerticalContentAlignment = VerticalAlignment.Center, Template = DockFieldTemplate(), Text = dockQuery
            };
            g.Children.Add(box);
            g.Cursor = Cursors.IBeam;
            g.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (!box.IsKeyboardFocusWithin) { box.Focus(); e.Handled = true; } };
            Border clear = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Arrow, Background = Ds.Brush(pal.Dark ? Ds.Argb(0.32, 255, 255, 255) : Ds.Argb(0.28, 0, 0, 0)),
                ToolTip = "Borrar la b\u00FAsqueda"
            };
            clear.Child = new GlyphView("close", 10, pal.Dark ? Ds.Rgb(44, 44, 48) : Colors.White, 2.2) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            clear.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; };
            clear.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; box.Text = ""; box.Focus(); };
            g.Children.Add(clear);
            Action sync = delegate
            {
                bool any = box.Text.Length > 0;
                hint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
                clear.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            };
            sync();
            box.TextChanged += delegate { sync(); SetDockSearch(box.Text); };
            box.GotKeyboardFocus += delegate
            {
                DimBar(dockSearchView, ref dockSearchDim, false);
                frame.BorderBrush = Ds.Brush(Ds.WithAlpha(pal.Accent, 0.85));
                frame.BorderThickness = new Thickness(1.5);
                frame.Background = Ds.Brush(pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Colors.White);
                icon.Color = pal.Accent;
            };
            box.LostKeyboardFocus += delegate
            {
                frame.BorderBrush = Ds.Brush(line);
                frame.BorderThickness = new Thickness(1);
                frame.Background = Ds.Brush(rest);
                icon.Color = pal.Label2;
            };
            box.PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    // As in Spotlight: Enter takes the first result.
                    e.Handled = true;
                    MItem first = FirstMatch();
                    if (first != null) ItemClick(first);
                    return;
                }
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                if (box.Text.Length > 0) box.Text = "";
                else Focus();
            };
            dockSearch = box;
            return g;
        }

        // The first tile the search leaves in the dock (never the button that saves a look).
        MItem FirstMatch()
        {
            string q = Norm(dockQuery);
            if (dockGroup == null || q.Length == 0) return null;
            foreach (MItem it in dockGroup.Items)
                if (it.Slot != SaveSlot && !it.Gone && it.Find != null && it.Find.Contains(q)) return it;
            return null;
        }

        void HookDockKeys()
        {
            if (dockKeysHooked) return;
            dockKeysHooked = true;
            PreviewKeyDown += delegate(object o, KeyEventArgs e)
            {
                if (page != "mascot" || dockSearch == null || e.Key != Key.F || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
                e.Handled = true;
                dockSearch.Focus();
                dockSearch.SelectAll();
            };
        }

        // Horizontal wheels and touchpads swiping sideways scroll the dock too (WPF doesn't route WM_MOUSEHWHEEL).
        void HookDockWheel()
        {
            if (dockWheelHooked || Handle == IntPtr.Zero) return;
            HwndSource src = HwndSource.FromHwnd(Handle);
            if (src == null) return;
            dockWheelHooked = true;
            src.AddHook(DockSideWheel);
        }

        IntPtr DockSideWheel(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != 0x020E || page != "mascot" || dockShelf == null || !dockShelf.IsMouseOver) return IntPtr.Zero;
            if (DockTilt(SideWheelDelta(wParam))) handled = true;
            return IntPtr.Zero;
        }

        // WM_MOUSEHWHEEL carries the tilt in the high word of wParam as a signed short, positive to the right (the
        // low word holds the key flags; on 64-bit the upper half may be anything).
        static int SideWheelDelta(IntPtr wParam) { return (short)((wParam.ToInt64() >> 16) & 0xFFFF); }

        // Tilting right shows what is further right, as a touchpad swiping left does.
        bool DockTilt(int delta) { return dockShelf != null && delta != 0 && dockShelf.Wheel(-delta); }

        // One tab at a time; switching or reordering refills the dock, a look change only updates it.
        void FillDock(bool cascade) { FillDockCore(cascade, false); }

        // Refills the tab where it stands (favorites first again, removed looks gone): the scroll stays and every tile
        // glides from its old place to its new one; a tile that is new fades in.
        void ReflowDock()
        {
            if (dockShelf == null) return;
            Dictionary<string, double[]> was = new Dictionary<string, double[]>();
            if (dockGroup != null)
                foreach (MItem it in dockGroup.Items)
                {
                    string k = ItemKey(it);
                    double x = dockShelf.CenterOf(it);
                    if (k != null && !it.Gone && !double.IsNaN(x) && !was.ContainsKey(k)) was[k] = new double[] { x, dockShelf.ScaleOf(it) };
                }
            FillDockCore(false, true);
            dockShelf.Glide(delegate(object tag)
            {
                MItem it = tag as MItem;
                double[] w;
                return it != null && was.TryGetValue(ItemKey(it), out w) ? w : null;
            });
        }

        // After a star or a removal made away from the dock: the star pops where it is first, then the row reflows.
        void ReflowSoon()
        {
            dockDirty = true;
            if (dockReflowSoon == null)
            {
                dockReflowSoon = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(560) };
                dockReflowSoon.Tick += delegate
                {
                    dockReflowSoon.Stop();
                    if (!dockDirty || dockShelf == null || page != "mascot" || PresentationSource.FromVisual(dockShelf) == null) return;
                    if (!dockShelf.IsMouseOver) ReflowDock();   // under the mouse it waits until the mouse leaves
                };
            }
            dockReflowSoon.Stop();
            dockReflowSoon.Start();
        }

        static string ItemKey(MItem it)
        {
            if (it.Slot == SlotAction) return "act:" + it.Index;
            if (it.Saved != null) return "look:" + it.Saved.Key;
            if (it.Slot < 0) return "slot:" + it.Slot;
            return Slots[it.Slot].Id + ":" + it.Index;
        }

        void FillDockCore(bool cascade, bool keep)
        {
            if (dockShelf == null) return;
            dockDirty = false;
            if (dockReflowSoon != null) dockReflowSoon.Stop();
            dockTab = Math.Max(0, Math.Min(DockTabNames.Length - 1, dockTab));
            if (dockTabsBar != null) dockTabsBar.Selected = dockTab;
            shimmering.Clear();
            StopShimmer();
            MGroup g = new MGroup(this);
            dockGroup = g;
            dockShelf.Clear(keep);
            MascotLook cur = MascotLook.From(settings);
            if (dockTab == 0) FillLooksTab(g, cur);
            else if (dockTab == ActionsTab) FillActionsTab(g, cur);
            else
            {
                int[] sl = DockTabSlots[dockTab];
                for (int k = 0; k < sl.Length; k++)
                {
                    if (k > 0) DockGap();
                    foreach (int i in Ordered(sl[k])) AddDockItem(g, ItemFor(sl[k], i), cur);
                }
            }
            if (Norm(dockQuery).Length == 0 && !keep)
                foreach (MItem it in g.Items)
                    if (it.Slot >= 0 && it.Cell != null && it.Cell.Worn) { dockShelf.Reveal(it); break; }
            ApplySearch(false, false);
            if (cascade)
            {
                int k = 0;
                foreach (MItem it in g.Items) if (it.Cell != null) it.Cell.Arrive(Math.Min(k++, 14) * 16);
            }
        }

        // Saved looks after the button that saves one, then everything starred.
        void FillLooksTab(MGroup g, MascotLook cur)
        {
            AddDockItem(g, new MItem { Slot = SaveSlot, Name = "Guardar look" }, cur);
            List<MascotLook> looks = SavedLooks();
            foreach (MascotLook l in looks) AddDockItem(g, LookItem(l), cur);
            List<MItem> favs = new List<MItem>();
            for (int s = 0; s < Slots.Length; s++)
                for (int i = 0; i < Slots[s].Names.Length; i++)
                    if (IsFavorite(s, i)) favs.Add(ItemFor(s, i));
            if (looks.Count == 0 && favs.Count == 0)
            {
                DockNote("Guarda la combinaci\u00F3n que lleva puesta y marca con la estrella lo que m\u00E1s te guste: todo aparecer\u00E1 aqu\u00ED.");
                return;
            }
            DockGap();
            if (favs.Count == 0) DockNote("Pasa el rat\u00F3n por cualquier prenda y pulsa su estrella: aparecer\u00E1 aqu\u00ED y la primera de su lista.");
            foreach (MItem it in favs) AddDockItem(g, it, cur);
        }

        // Everything it can be asked to do; a click performs it.
        void FillActionsTab(MGroup g, MascotLook cur)
        {
            for (int i = 0; i < MascotCmd.Count; i++)
                AddDockItem(g, new MItem { Slot = SlotAction, Index = i, Name = MascotCmd.Names[i], Hint = MascotCmd.Hints[i] }, cur);
        }

        void DockGap()
        {
            Grid g = new Grid { Width = 18, Height = DockItemH };
            g.Children.Add(new Border { Width = 1, Height = 40, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 11, 0, 0), Background = Ds.Brush(Ds.Brushes.Separator) });
            dockShelf.Add(g, null, null, 18, false);
        }

        void DockNote(string text)
        {
            TextBlock t = Paragraph(text, 12, Ds.Brushes.Label2);
            t.MaxWidth = 330;
            t.VerticalAlignment = VerticalAlignment.Top;
            t.Margin = new Thickness(8, 13, 0, 0);
            Grid g = new Grid { Width = 350, Height = DockItemH };
            g.Children.Add(t);
            dockShelf.Add(g, null, null, 350, false);
        }

        static string DockCaption(MItem it)
        {
            if (it.Saved != null) return MascotParts.Kinds[it.Saved.Kind] + " " + MascotParts.ColorNames[it.Saved.Color].ToLowerInvariant();
            if (it.Slot == SlotAction) return MascotCmd.Short[it.Index];
            return it.Name;
        }

        void AddDockItem(MGroup g, MItem it, MascotLook cur)
        {
            ShelfTile.Badge badge = it.Slot == SavedSlot ? ShelfTile.Badge.Remove : it.Slot < 0 ? ShelfTile.Badge.None : ShelfTile.Badge.Star;
            ShelfTile c = new ShelfTile(DockCaption(it), badge);
            it.Cell = c;
            it.Find = Norm(it.Name + " " + (it.Hint ?? "") + (it.Saved != null ? " " + DescribeLook(it.Saved) : ""));
            if (it.Slot == SaveSlot) c.MakeAdd();
            else if (it.Slot == SlotAction) c.MakeAction(MascotCmd.Glyphs[it.Index]);
            else DockPreview(it, cur);
            if (it.Locked) c.Lock(it.Level);
            c.Set(IsWorn(it, cur), it.Fav);
            g.Items.Add(it);
            c.MouseEnter += delegate { g.Hover(it); };
            c.MouseLeave += delegate { g.Unhover(it); };
            c.Click += delegate { ItemClick(it); };
            c.MouseRightButtonUp += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; ItemMenu(it); };
            c.BadgeClick += delegate
            {
                if (it.Slot == SavedSlot) RemoveLook(it.Saved);
                else SetFavorite(it.Slot, it.Index, !IsFavorite(it.Slot, it.Index));
            };
            dockShelf.Add(c, c, it, DockSlot, true);
        }

        // The tile's preview: the current look wearing the item, rendered big enough to stay sharp when magnified.
        void DockPreview(MItem it, MascotLook cur)
        {
            ShelfTile c = it.Cell;
            MascotLook l = cur.Clone();
            l.Seasonal = false;
            ApplyItem(it, l);
            ShowPreview(c.Img, l, DockTile * DockMagnify, it.Slot == SavedSlot ? "look" + l.Key : Slots[it.Slot].Id + it.Index);
            // Something to show already (this preview, or the slot's last one while it renders): no placeholder. A tile
            // that was still waiting for another look ends its shimmer here, since that preview no longer reaches it.
            if (c.Img.Source != null) { PreviewArrived(c.Img); return; }
            if (shimmering.ContainsKey(c.Img)) return;
            c.Shimmer(ShimmerBrush());
            shimmering[c.Img] = c;
            StartShimmer();
        }

        // ---- Loading placeholders: one shared moving highlight, stopped as soon as the last preview arrives.

        Brush ShimmerBrush()
        {
            if (shimmerBrush != null) return shimmerBrush;
            Palette pal = Ds.Brushes;
            Color b = pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Argb(0.06, 0, 0, 0), hi = pal.Dark ? Ds.Argb(0.2, 255, 255, 255) : Ds.Argb(0.85, 255, 255, 255);
            shimmerShift = new TranslateTransform(-1, 0);
            shimmerBrush = new LinearGradientBrush { StartPoint = new Point(0, 0.3), EndPoint = new Point(1, 0.7), RelativeTransform = shimmerShift };
            shimmerBrush.GradientStops.Add(new GradientStop(b, 0));
            shimmerBrush.GradientStops.Add(new GradientStop(b, 0.32));
            shimmerBrush.GradientStops.Add(new GradientStop(hi, 0.5));
            shimmerBrush.GradientStops.Add(new GradientStop(b, 0.68));
            shimmerBrush.GradientStops.Add(new GradientStop(b, 1));
            return shimmerBrush;
        }

        void StartShimmer()
        {
            if (shimmerOn || shimmerShift == null) return;
            shimmerOn = true;
            shimmerShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-1, 1, TimeSpan.FromMilliseconds(1150)) { RepeatBehavior = RepeatBehavior.Forever });
        }

        void StopShimmer()
        {
            if (!shimmerOn) return;
            shimmerOn = false;
            if (shimmerShift != null) shimmerShift.BeginAnimation(TranslateTransform.XProperty, null);
        }

        void PreviewArrived(Image i)
        {
            ShelfTile c;
            if (!shimmering.TryGetValue(i, out c)) return;
            shimmering.Remove(i);
            c.Unshimmer();
            if (shimmering.Count == 0) StopShimmer();
        }

        // ---- Search

        static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Normalize throws on broken text (a pasted half of an emoji): such characters are simply left out.
            StringBuilder v = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { v.Append(s[i]).Append(s[++i]); continue; }
                if (!char.IsSurrogate(s[i])) v.Append(s[i]);
            }
            string d;
            try { d = v.ToString().Trim().Normalize(NormalizationForm.FormD); }
            catch (ArgumentException) { d = v.ToString().Trim(); }
            StringBuilder b = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) b.Append(char.ToLowerInvariant(c));
            try { return b.ToString().Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return b.ToString(); }
        }

        static string[][] slotFind;
        static string SlotFind(int slot, int i)
        {
            if (slotFind == null)
            {
                slotFind = new string[Slots.Length][];
                for (int s = 0; s < Slots.Length; s++)
                {
                    slotFind[s] = new string[Slots[s].Names.Length];
                    for (int n = 0; n < Slots[s].Names.Length; n++)
                        slotFind[s][n] = Norm(Slots[s].Names[n] + " " + (Slots[s].Hints != null && n < Slots[s].Hints.Length ? Slots[s].Hints[n] : ""));
                }
            }
            return slotFind[slot][i];
        }

        void SetDockSearch(string q)
        {
            q = q ?? "";
            if (dockSearch != null && dockSearch.Text != q) { dockSearch.Text = q; return; }   // comes back through TextChanged
            if (q == dockQuery) return;
            dockQuery = q;
            ApplySearch(true, true);
        }

        int DockMatches(int tab, string q)
        {
            int n = 0;
            if (tab == ActionsTab)
            {
                for (int i = 0; i < MascotCmd.Count; i++) if (Norm(MascotCmd.Names[i] + " " + MascotCmd.Hints[i]).Contains(q)) n++;
                return n;
            }
            if (tab == 0)
            {
                foreach (MascotLook l in SavedLooks()) if (Norm(DescribeLook(l)).Contains(q)) n++;
                for (int s = 0; s < Slots.Length; s++)
                    for (int i = 0; i < Slots[s].Names.Length; i++)
                        if (IsFavorite(s, i) && SlotFind(s, i).Contains(q)) n++;
                return n;
            }
            foreach (int s in DockTabSlots[tab])
                for (int i = 0; i < Slots[s].Names.Length; i++)
                    if (SlotFind(s, i).Contains(q)) n++;
            return n;
        }

        // Tiles that don't match shrink away; the tabs show where else there are matches.
        void ApplySearch(bool animate, bool home)
        {
            if (dockShelf == null || dockGroup == null) return;
            string q = Norm(dockQuery);
            bool searching = q.Length > 0;
            int shown = 0;
            dockShelf.Filter(delegate(object tag)
            {
                MItem it = tag as MItem;
                if (it == null) return !searching;
                bool keep = !it.Gone && (!searching || (it.Slot != SaveSlot && it.Find.Contains(q)));
                if (keep && it.Slot != SaveSlot) shown++;
                return keep;
            }, animate, home);
            // The tile under the mouse shrank away (filtered out or removed): its label and try-on go with it.
            MItem hot = dockGroup.Hot;
            if (hot != null && (hot.Gone || (searching && (hot.Slot == SaveSlot || !hot.Find.Contains(q)))))
            {
                dockGroup.Unhover(hot);
                dockShelf.HideHud();
                TryLook(null);
            }
            int[] counts = null;
            if (searching)
            {
                counts = new int[DockTabNames.Length];
                for (int t = 0; t < counts.Length; t++) counts[t] = DockMatches(t, q);
            }
            if (dockTabsBar != null) dockTabsBar.SetMatches(counts);

            bool none = searching && shown == 0;
            if (none && dockEmptyText != null)
            {
                dockEmptyText.Text = "Nada con \u00AB" + dockQuery.Trim() + "\u00BB en " + DockTabNames[dockTab];
                int best = -1;
                for (int t = 0; t < counts.Length; t++) if (t != dockTab && counts[t] > 0 && (best < 0 || counts[t] > counts[best])) best = t;
                dockEmptyTarget = best;
                dockEmptyLink.Text = best >= 0 ? "Ver " + counts[best] + " en " + DockTabNames[best] : "Prueba con otra palabra";
                dockEmptyLink.Foreground = Ds.Brush(best >= 0 ? Ds.Brushes.Accent : Ds.Brushes.Label3);
                dockEmptyLink.Cursor = best >= 0 ? Cursors.Hand : Cursors.Arrow;
            }
            if (dockEmpty != null)
            {
                dockEmpty.IsHitTestVisible = none;
                if (animate) Glide(dockEmpty, UIElement.OpacityProperty, none ? 1 : 0, none ? 240 : 120);
                else { dockEmpty.BeginAnimation(UIElement.OpacityProperty, null); dockEmpty.Opacity = none ? 1 : 0; }
            }
        }

        // The mouse left the dock: back to the saved look, and reorder if favorites changed meanwhile.
        void DockLeft()
        {
            TryLook(null);
            if (dockShelf != null) dockShelf.HideHud();
            Dispatcher.BeginInvoke((Action)delegate
            {
                if (dockShelf == null || page != "mascot" || dockShelf.IsMouseOver || PresentationSource.FromVisual(dockShelf) == null || !dockDirty) return;
                // Starred a moment ago: the pop plays out on its tile before the row reorders.
                if (StarPopping()) ReflowSoon();
                else ReflowDock();
            });
        }

        bool StarPopping()
        {
            if (dockGroup != null)
                foreach (MItem it in dockGroup.Items) if (it.Cell != null && it.Cell.StarPopping) return true;
            return false;
        }

        void DockHot(MItem it)
        {
            if (dockShelf != null && it != null) dockShelf.ShowHud(it, ItemText(it));
        }

        // ---- Catalogs, favorites and saved looks

        // A wardrobe catalog: names, how an item changes a look and the level that unlocks it.
        class MSlot
        {
            public readonly string Id, Title;
            public readonly string[] Names, Hints;
            public readonly Action<MascotLook, int> Apply;
            public readonly Func<MascotLook, int> Get;     // the piece worn now; null for whole costumes
            public readonly Func<int, int> Unlock;         // level that unlocks an item; null when nothing is locked

            public MSlot(string id, string title, string[] names, string[] hints, Action<MascotLook, int> apply, Func<MascotLook, int> get, Func<int, int> unlock)
            {
                Id = id; Title = title; Names = names; Hints = hints; Apply = apply; Get = get; Unlock = unlock;
            }

            public bool Whole { get { return Get == null; } }
        }

        // Ids are stored in the settings ("slot:index"), so they never change; new catalogs go at the end.
        static MSlot[] slots;
        static MSlot[] Slots
        {
            get
            {
                if (slots == null)
                    slots = new MSlot[]
                    {
                        new MSlot("anime", "Personaje", MascotParts.AnimeNames, MascotParts.AnimeInspiration, MascotParts.ApplyAnime, null, null),
                        new MSlot("style", "Estilo", MascotParts.StyleNames, null, MascotParts.ApplyStyle, null, null),
                        new MSlot("kind", "Especie", MascotParts.Kinds, null, delegate(MascotLook l, int i) { l.Kind = i; }, delegate(MascotLook l) { return l.Kind; }, null),
                        new MSlot("color", "Color", MascotParts.ColorNames, null, delegate(MascotLook l, int i) { l.Color = i; }, delegate(MascotLook l) { return l.Color; }, MascotParts.ColorUnlock),
                        new MSlot("hat", "Gorro", MascotParts.HatNames, null, delegate(MascotLook l, int i) { l.Hat = i; }, delegate(MascotLook l) { return l.Hat; }, MascotParts.HatUnlock),
                        new MSlot("outfit", "Ropa", MascotParts.OutfitNames, null, delegate(MascotLook l, int i) { l.Outfit = i; }, delegate(MascotLook l) { return l.Outfit; }, null),
                        new MSlot("face", "Accesorio", MascotParts.FaceNames, null, delegate(MascotLook l, int i) { l.Face = i; }, delegate(MascotLook l) { return l.Face; }, null),
                        new MSlot("eyes", "Ojos", MascotParts.EyeNames, null, delegate(MascotLook l, int i) { l.Eyes = i; }, delegate(MascotLook l) { return l.Eyes; }, null),
                        new MSlot("acc", "Extra", MascotParts.AccessoryNames, null, delegate(MascotLook l, int i) { l.Accessory = i; }, delegate(MascotLook l) { return l.Accessory; }, null)
                    };
                return slots;
            }
        }

        // One tile of the dock: a catalog item, a saved look, or the button that saves one.
        class MItem
        {
            public int Slot, Index, Level;     // Slot: a catalog, SavedSlot, SurpriseSlot or SaveSlot; Level: the one that unlocks it
            public string Name, Hint, Find;
            public MascotLook Saved;
            public bool Locked, Fav, Gone;
            public ShelfTile Cell;
        }

        MItem ItemFor(int slot, int i)
        {
            MSlot s = Slots[slot];
            MItem it = new MItem { Slot = slot, Index = i, Name = s.Names[i] };
            if (s.Hints != null && i < s.Hints.Length) it.Hint = s.Hints[i];
            if (s.Unlock != null)
            {
                it.Level = s.Unlock(i);
                it.Locked = it.Level > MascotParts.Level(settings.MascotLove);
            }
            it.Fav = IsFavorite(slot, i);
            return it;
        }

        MItem LookItem(MascotLook l)
        {
            MItem it = new MItem { Slot = SavedSlot, Saved = l, Name = DescribeLook(l) };
            it.Level = Math.Max(MascotParts.ColorUnlock(l.Color), MascotParts.HatUnlock(l.Hat));
            it.Locked = it.Level > MascotParts.Level(settings.MascotLove);
            return it;
        }

        static string DescribeLook(MascotLook l)
        {
            string s = MascotParts.Kinds[l.Kind] + " " + MascotParts.ColorNames[l.Color].ToLowerInvariant();
            if (l.Hat > 0) s += ", " + MascotParts.HatNames[l.Hat].ToLowerInvariant();
            if (l.Outfit > 0) s += ", " + MascotParts.OutfitNames[l.Outfit].ToLowerInvariant();
            if (l.Face > 0) s += ", " + MascotParts.FaceNames[l.Face].ToLowerInvariant();
            if (l.Accessory > 0) s += ", " + MascotParts.AccessoryNames[l.Accessory].ToLowerInvariant();
            return s;
        }

        static void ApplyItem(MItem it, MascotLook l)
        {
            if (it.Saved != null)
            {
                l.Kind = it.Saved.Kind; l.Color = it.Saved.Color; l.Eyes = it.Saved.Eyes;
                l.Hat = it.Saved.Hat; l.Outfit = it.Saved.Outfit; l.Face = it.Saved.Face; l.Accessory = it.Saved.Accessory;
            }
            else if (it.Slot >= 0 && it.Slot < Slots.Length) Slots[it.Slot].Apply(l, it.Index);
        }

        static bool IsWorn(MItem it, MascotLook cur)
        {
            if (it.Slot < 0 && it.Saved == null) return false;
            if (it.Slot >= 0 && !Slots[it.Slot].Whole) return Slots[it.Slot].Get(cur) == it.Index;
            MascotLook l = cur.Clone();
            ApplyItem(it, l);
            return SameLook(l, cur);
        }

        static bool SameLook(MascotLook a, MascotLook b)
        {
            return a.Kind == b.Kind && a.Color == b.Color && a.Eyes == b.Eyes && a.Hat == b.Hat && a.Outfit == b.Outfit && a.Face == b.Face && a.Accessory == b.Accessory;
        }

        // The label over a hovered tile: its full name, where a character comes from, and what a locked one needs.
        static string ItemText(MItem it)
        {
            if (it.Slot == SlotAction) return it.Name + "  \u00B7  " + it.Hint;
            if (it.Slot == SaveSlot) return "Guardar lo que lleva puesto";
            if (it.Slot == SurpriseSlot) return "Un look al azar con lo que ten\u00E9is desbloqueado";
            string s = it.Slot == SavedSlot ? DescribeLook(it.Saved) : it.Slot == SlotEyes ? "Ojos: " + it.Name : it.Name;
            if (it.Hint != null) s += "  \u00B7  " + it.Hint;
            if (it.Locked)
            {
                int lv = Math.Max(0, Math.Min(MascotParts.LevelNames.Length - 1, it.Level));
                s += "  \u00B7  llega con \u00AB" + MascotParts.LevelNames[lv].ToLowerInvariant() + "\u00BB (" + MascotParts.UnlockLove(lv) + " capturas)";
            }
            return s;
        }

        // Favorites ("slot:index"), cleaned up: known slots and indexes only, no repeats, in the order they were added.
        List<string> FavoriteKeys()
        {
            List<string> r = new List<string>();
            foreach (string raw in (settings.MascotFavorites ?? "").Split(','))
            {
                string t = raw.Trim();
                int c = t.IndexOf(':'), idx;
                if (c <= 0) continue;
                string id = t.Substring(0, c).Trim().ToLowerInvariant();
                int slot = -1;
                for (int s = 0; s < Slots.Length; s++) if (Slots[s].Id == id) slot = s;
                if (slot < 0 || !int.TryParse(t.Substring(c + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out idx)) continue;
                if (idx >= Slots[slot].Names.Length) continue;
                string key = Slots[slot].Id + ":" + idx;
                if (!r.Contains(key)) r.Add(key);
            }
            return r;
        }

        string favSource;
        HashSet<string> favSet;

        bool IsFavorite(int slot, int i)
        {
            if (favSet == null || favSource != settings.MascotFavorites)
            {
                favSet = new HashSet<string>(FavoriteKeys());
                favSource = settings.MascotFavorites;
            }
            return slot >= 0 && slot < Slots.Length && favSet.Contains(Slots[slot].Id + ":" + i);
        }

        bool LockedItem(int slot, int i)
        {
            MSlot s = Slots[slot];
            return s.Unlock != null && s.Unlock(i) > MascotParts.Level(settings.MascotLove);
        }

        // Catalog order, with the favorites first.
        List<int> Ordered(int slot)
        {
            List<int> first = new List<int>(), rest = new List<int>();
            for (int i = 0; i < Slots[slot].Names.Length; i++) (IsFavorite(slot, i) ? first : rest).Add(i);
            first.AddRange(rest);
            return first;
        }

        void SetFavorite(int slot, int i, bool on)
        {
            if (slot < 0 || slot >= Slots.Length || i < 0 || i >= Slots[slot].Names.Length) return;
            if (on && LockedItem(slot, i)) return;
            List<string> keys = FavoriteKeys();
            string key = Slots[slot].Id + ":" + i;
            if (keys.Contains(key) == on) return;
            if (on) keys.Add(key);
            else keys.Remove(key);
            settings.MascotFavorites = string.Join(",", keys.ToArray());
            settings.Save();
            if (dockGroup != null) dockGroup.MarkFavorite(slot, i, on);
            // The row reorders in place (same scroll, tiles gliding): under the mouse once it leaves, so no tile jumps
            // under the pointer; elsewhere (the context menu) once the star has popped.
            if (dockShelf != null)
            {
                // In Mis looks an unstarred piece leaves the row, shrinking away as a removed look does.
                if (!on && dockTab == 0 && dockGroup != null && PresentationSource.FromVisual(dockShelf) != null)
                {
                    foreach (MItem it in dockGroup.Items) if (it.Slot == slot && it.Index == i) it.Gone = true;
                    ApplySearch(true, false);
                }
                if (dockShelf.IsMouseOver) dockDirty = true;
                else ReflowSoon();
            }
            if (on && settings.MascotOn) { mascot.Hop(0.35); Wake(); }
        }

        // Saved looks: MascotLook keys ("kind.color.eyes.hat.outfit.face.seasonal[.accessory]"), newest first.
        static bool ParseLook(string key, out MascotLook look)
        {
            look = null;
            if (key == null) return false;
            string[] p = key.Trim().Split('.');
            if (p.Length < 6 || p.Length > 8) return false;
            int[] v = new int[p.Length];
            for (int i = 0; i < p.Length; i++)
                if (!int.TryParse(p[i], NumberStyles.None, CultureInfo.InvariantCulture, out v[i])) return false;
            if (v[0] >= MascotParts.Kinds.Length || v[1] >= MascotParts.ColorNames.Length || v[2] >= MascotParts.EyeNames.Length ||
                v[3] >= MascotParts.HatNames.Length || v[4] >= MascotParts.OutfitNames.Length || v[5] >= MascotParts.FaceNames.Length ||
                (p.Length == 8 && v[7] >= MascotParts.AccessoryNames.Length)) return false;
            look = new MascotLook { Kind = v[0], Color = v[1], Eyes = v[2], Hat = v[3], Outfit = v[4], Face = v[5], Seasonal = p.Length < 7 || v[6] != 0, Accessory = p.Length == 8 ? v[7] : 0 };
            return true;
        }

        List<MascotLook> SavedLooks()
        {
            List<MascotLook> r = new List<MascotLook>();
            foreach (string part in (settings.MascotLooks ?? "").Split('|'))
            {
                MascotLook l;
                if (!ParseLook(part, out l)) continue;
                bool dup = false;
                foreach (MascotLook o in r) if (SameLook(o, l)) dup = true;
                if (!dup) r.Add(l);
                if (r.Count >= MaxLooks) break;
            }
            return r;
        }

        void StoreLooks(List<MascotLook> looks)
        {
            List<string> keys = new List<string>();
            foreach (MascotLook l in looks) keys.Add(l.Key);
            settings.MascotLooks = string.Join("|", keys.ToArray());
            settings.Save();
        }

        void SaveLook()
        {
            MascotLook cur = MascotLook.From(settings);
            List<MascotLook> looks = SavedLooks();
            bool had = looks.RemoveAll(delegate(MascotLook l) { return SameLook(l, cur); }) > 0;
            looks.Insert(0, cur);
            if (looks.Count > MaxLooks) looks.RemoveRange(MaxLooks, looks.Count - MaxLooks);
            StoreLooks(looks);
            if (settings.MascotOn) mascot.Celebrate(settings.MascotTalks ? (had ? "Ya lo ten\u00EDas: lo pongo el primero." : "\u00A1Guardado en Mis looks!") : null);
            Wake();
            dockTab = 0;
            FillDock(true);
        }

        void RemoveLook(MascotLook gone)
        {
            if (gone == null) return;
            List<MascotLook> looks = SavedLooks();
            int at = looks.FindIndex(delegate(MascotLook l) { return SameLook(l, gone); });
            if (at < 0) return;
            looks.RemoveAt(at);
            StoreLooks(looks);
            TryLook(null);
            // The tile shrinks away and the dock closes the gap; the rest reflows once the mouse is gone.
            if (dockShelf != null && dockGroup != null && PresentationSource.FromVisual(dockShelf) != null)
            {
                foreach (MItem it in dockGroup.Items) if (it.Saved != null && SameLook(it.Saved, gone)) it.Gone = true;
                dockDirty = true;
                ApplySearch(true, false);
                if (!dockShelf.IsMouseOver) ReflowSoon();
            }
            else FillDock(false);
        }

        void Surprise()
        {
            MascotLook r = MascotLook.From(settings);
            MascotParts.Randomize(r, MascotParts.Level(settings.MascotLove), Rng);
            LookChanged(r, "\u00A1Tach\u00E1n! \u00BFQu\u00E9 te parece?");
            AfterLook(true);
        }

        void ItemClick(MItem it)
        {
            if (it.Slot == SlotAction) { MascotDo(it.Index); return; }
            if (it.Slot == SaveSlot) { SaveLook(); return; }
            if (it.Slot == SurpriseSlot) { Surprise(); return; }
            if (it.Locked)
            {
                int left = MascotParts.UnlockLove(it.Level) - settings.MascotLove;
                if (settings.MascotOn) mascot.Say("\u00A1A\u00FAn no! Te faltan " + left + (left == 1 ? " captura" : " capturas") + " para eso.", 2800);
                if (it.Cell != null) it.Cell.Shake();
                Wake();
                return;
            }
            MascotLook l = MascotLook.From(settings);
            string reaction;
            if (it.Saved != null)
            {
                if (SameLook(it.Saved, l)) return;
                ApplyItem(it, l);
                reaction = "\u00A1Me encanta este look!";
            }
            else
            {
                MSlot s = Slots[it.Slot];
                if (!s.Whole && s.Get(l) == it.Index) return;
                s.Apply(l, it.Index);
                if (it.Slot == SlotAnime) reaction = "\u00A1Hoy soy " + it.Name + "!";
                else if (it.Slot == SlotStyle) reaction = "\u00A1Modo " + it.Name.ToLowerInvariant() + " activado!";
                else if (it.Slot == SlotEyes) reaction = "\u00BFQu\u00E9 tal me ves as\u00ED?";
                else reaction = it.Index > 0 ? "\u00A1" + it.Name + "! \u00BFQu\u00E9 tal me queda?" : null;
            }
            LookChanged(l, reaction);
            AfterLook(true);
        }

        // Right click: wear it, and add to or remove from favorites (or the saved looks).
        Popup ItemMenu(MItem it)
        {
            if (it.Slot == SurpriseSlot || it.Slot == SaveSlot || it.Slot == SlotAction || it.Cell == null || PresentationSource.FromVisual(it.Cell) == null) return null;
            List<MenuEntry> m = new List<MenuEntry>();
            m.Add(MenuEntry.Title(it.Saved != null ? "Look guardado" : it.Name));
            if (!it.Locked) m.Add(MenuEntry.Item("Pon\u00E9rmelo", "sparkle", false, delegate { ItemClick(it); }));
            if (it.Saved != null) m.Add(MenuEntry.Item("Quitar de Mis looks", "trash", false, delegate { RemoveLook(it.Saved); }));
            else if (IsFavorite(it.Slot, it.Index)) m.Add(MenuEntry.Item("Quitar de favoritos", "minus", false, delegate { SetFavorite(it.Slot, it.Index, false); }));
            else if (!it.Locked) m.Add(MenuEntry.Item("A\u00F1adir a favoritos", "plus", false, delegate { SetFavorite(it.Slot, it.Index, true); }));
            Popup p = MacMenu.Show(it.Cell.Lift, m);
            p.Placement = PlacementMode.Top;
            p.VerticalOffset = -6;
            return p;
        }

        // The tiles of the dock: hover state, try-on and the label over the hovered one.
        class MGroup
        {
            readonly HomeWindow w;
            public readonly List<MItem> Items = new List<MItem>();
            MItem hot;

            public MGroup(HomeWindow w) { this.w = w; }

            public MItem Hot { get { return hot; } }

            public void Hover(MItem it)
            {
                if (hot == it) return;
                if (hot != null && hot.Cell != null) hot.Cell.SetHot(false);
                hot = it;
                it.Cell.SetHot(true);
                w.DockHot(it);
                TryOnHot();
            }

            // Try it on: the big mascot wears the hovered piece until the mouse leaves the dock.
            public void TryOnHot()
            {
                MItem it = hot;
                if (it == null) return;
                if ((it.Slot < 0 && it.Saved == null) || it.Locked) { w.TryLook(null); return; }
                MascotLook l = MascotLook.From(w.settings);
                ApplyItem(it, l);
                w.TryLook(l);
            }

            public void Unhover(MItem it)
            {
                if (hot != it) return;
                hot = null;
                if (it.Cell != null) it.Cell.SetHot(false);
            }

            public void MarkFavorite(int slot, int index, bool on)
            {
                foreach (MItem it in Items)
                    if (it.Slot == slot && it.Index == index) { it.Fav = on; if (it.Cell != null) it.Cell.SetFav(on, true); }
            }
        }

        // ---- The dock row

        // Tiles that magnify near the mouse (neighbours spread apart, as in the macOS Dock), scroll with the wheel and
        // shrink away when filtered out. A frame callback runs only while something still moves; at rest, nothing.
        class Shelf : Panel
        {
            public const double Pad = 12, Top = 12, Bottom = 8, Reach = 176;

            sealed class Entry
            {
                public FrameworkElement El;
                public ShelfTile Tile;
                public object Tag;
                public double W, Scale = 1, ScaleTo = 1, Vis = 1, VisTo = 1;
                public bool Magnify;
            }

            readonly List<Entry> list = new List<Entry>();
            readonly Grid hud = new Grid();
            readonly TextBlock hudText;
            Entry hudOn;
            Rect hudRect;
            Size clipFor;
            double offset, offsetTo, mouse = double.NaN, anchor = double.NaN, last, width;
            bool running, hudShown;
            object reveal;
            Rect hudReported = Rect.Empty;
            public Action<double, double> Edges;   // content hidden on the left and on the right
            public Action<Rect> HudMoved;          // where the label over the hovered tile is (Empty once it goes)

            public Shelf()
            {
                Palette pal = Ds.Brushes;
                Background = Brushes.Transparent;
                Height = Top + DockItemH + Bottom;
                hudText = new TextBlock { FontFamily = Ds.Text, FontWeight = FontWeights.Medium, FontSize = 12, Foreground = Ds.Brush(pal.Label) };
                Border face = new Border
                {
                    CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 3, 9, 4), Child = hudText,
                    Background = Ds.Brush(pal.Dark ? Ds.Rgb(58, 58, 62) : Colors.White), BorderBrush = Ds.Brush(pal.Hairline), BorderThickness = new Thickness(1)
                };
                hud.Children.Add(new SoftShadow(7, 8, 2, pal.Dark ? 0.45 : 0.14));
                hud.Children.Add(face);
                hud.IsHitTestVisible = false;
                hud.Opacity = 0;
                Children.Add(hud);
                Unloaded += delegate { Halt(); };
            }

            public bool Running { get { return running; } }
            public double Offset { get { return offset; } }
            public double OffsetTarget { get { return offsetTo; } }
            public string HudText { get { return hudShown ? hudText.Text : null; } }

            Entry Find(object tag)
            {
                if (tag == null) return null;
                foreach (Entry e in list) if (e.Tag == tag) return e;
                return null;
            }

            public double ScaleOf(object tag) { Entry e = Find(tag); return e != null ? e.Scale : 0; }
            public double VisOf(object tag) { Entry e = Find(tag); return e != null ? e.Vis : 0; }

            // Where a tile sits at rest (no magnification), in this panel's coordinates.
            public double CenterOf(object tag)
            {
                double x = Pad - offset;
                foreach (Entry e in list)
                {
                    double w = e.W * e.Vis;
                    if (e.Tag == tag) return x + w / 2;
                    x += w;
                }
                return double.NaN;
            }

            // keep: the same row again (reordered), so the scroll stays where it is.
            public void Clear(bool keep)
            {
                Halt();
                list.Clear();
                Children.Clear();
                Children.Add(hud);
                if (!keep) offset = offsetTo = 0;
                hudOn = null;
                hudShown = false;
                hud.BeginAnimation(OpacityProperty, null);
                hud.Opacity = 0;
                ReportHud(Rect.Empty);
                reveal = null;
                InvalidateMeasure();
            }

            // Tiles that were somewhere else a moment ago (rest center and magnification, or null for a new one) start
            // there and glide to their new place.
            public void Glide(Func<object, double[]> from)
            {
                double max = TargetMax();
                offsetTo = Math.Max(0, Math.Min(offsetTo, max));
                offset = Math.Max(0, Math.Min(offset, max));
                foreach (Entry e in list)
                {
                    if (e.Tile == null || e.VisTo <= 0) continue;
                    double[] was = from(e.Tag);
                    if (was == null) { e.Tile.Arrive(60); continue; }
                    double dx = was[0] - CenterOf(e.Tag);
                    if (e.Magnify && was[1] > 1)
                    {
                        e.Scale = was[1];
                        e.Tile.LiftScale.ScaleX = e.Tile.LiftScale.ScaleY = was[1];
                    }
                    if (Math.Abs(dx) > 0.5) e.Tile.Slide(dx);
                }
                Kick();
            }

            public void Add(FrameworkElement el, ShelfTile tile, object tag, double w, bool magnify)
            {
                list.Add(new Entry { El = el, Tile = tile, Tag = tag, W = w, Magnify = magnify });
                Children.Insert(Children.Count - 1, el);   // the label stays on top
                InvalidateMeasure();
            }

            // Scrolls (without animating) so this tile shows, once the width is known.
            public void Reveal(object tag) { reveal = tag; InvalidateArrange(); }

            public void Filter(Func<object, bool> keep, bool animate, bool home)
            {
                foreach (Entry e in list)
                {
                    e.VisTo = keep(e.Tag) ? 1 : 0;
                    e.El.IsHitTestVisible = e.VisTo > 0;
                    if (!animate) { e.Vis = e.VisTo; Show(e); }
                }
                if (home) offsetTo = 0;
                if (animate || !double.IsNaN(mouse)) Kick();
                else InvalidateArrange();
            }

            static void Show(Entry e)
            {
                e.El.Opacity = e.Vis;
                if (e.Tile != null) e.Tile.VisScale.ScaleX = e.Tile.VisScale.ScaleY = 0.55 + 0.45 * e.Vis;
            }

            public void ShowHud(object tag, string text)
            {
                Entry e = Find(tag);
                if (e == null) return;
                hudOn = e;
                hudText.Text = text;
                if (!hudShown)
                {
                    hudShown = true;
                    hud.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
                }
                InvalidateMeasure();
                InvalidateArrange();
            }

            public void HideHud()
            {
                if (!hudShown) return;
                hudShown = false;
                hudOn = null;
                hud.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
                ReportHud(Rect.Empty);
            }

            void ReportHud(Rect r)
            {
                if (r.IsEmpty ? hudReported.IsEmpty : !hudReported.IsEmpty && Math.Abs(r.X - hudReported.X) < 0.5 && Math.Abs(r.Y - hudReported.Y) < 0.5 &&
                    Math.Abs(r.Width - hudReported.Width) < 0.5 && Math.Abs(r.Height - hudReported.Height) < 0.5) return;
                hudReported = r;
                Action<Rect> h = HudMoved;
                if (h != null) h(r);
            }

            public Rect HudRect { get { return hudShown ? hudRect : Rect.Empty; } }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                MagnifyAt(e.GetPosition(this).X);
            }

            protected override void OnMouseLeave(MouseEventArgs e)
            {
                base.OnMouseLeave(e);
                MagnifyAt(double.NaN);
            }

            protected override void OnMouseWheel(MouseWheelEventArgs e)
            {
                base.OnMouseWheel(e);
                if (Wheel(e.Delta)) e.Handled = true;
            }

            public void MagnifyAt(double x)
            {
                mouse = x;
                if (!double.IsNaN(x)) anchor = x;
                Kick();
            }

            public bool Wheel(int delta)
            {
                double max = TargetMax();
                if (max <= 0) return false;
                offsetTo = Math.Max(0, Math.Min(max, offsetTo - delta * 1.25));
                Kick();
                return true;
            }

            double TargetMax()
            {
                double x = Pad * 2;
                foreach (Entry e in list) x += e.W * e.VisTo;
                return Math.Max(0, x - width);
            }

            void Kick()
            {
                if (running) return;
                running = true;
                last = Anim.Now;
                CompositionTarget.Rendering += Frame;
            }

            void Halt()
            {
                if (!running) return;
                running = false;
                CompositionTarget.Rendering -= Frame;
            }

            // Eases every value towards its target; stops as soon as all of them are there.
            void Frame(object sender, EventArgs args)
            {
                if (PresentationSource.FromVisual(this) == null) { Halt(); return; }
                double now = Anim.Now, dt = Math.Max(1, Math.Min(48, now - last));
                last = now;
                Targets();
                double km = 1 - Math.Exp(-dt / 58.0), kv = 1 - Math.Exp(-dt / 80.0), ko = 1 - Math.Exp(-dt / 95.0);
                bool busy = false;
                foreach (Entry en in list)
                {
                    double s = Toward(en.Scale, en.ScaleTo, km, 0.002, ref busy);
                    if (s != en.Scale)
                    {
                        en.Scale = s;
                        if (en.Tile != null) en.Tile.LiftScale.ScaleX = en.Tile.LiftScale.ScaleY = s;
                    }
                    double v = Toward(en.Vis, en.VisTo, kv, 0.003, ref busy);
                    if (v != en.Vis) { en.Vis = v; Show(en); }
                }
                offsetTo = Math.Min(offsetTo, TargetMax());
                offset = Toward(offset, offsetTo, ko, 0.4, ref busy);
                InvalidateArrange();
                if (!busy) Halt();
            }

            static double Toward(double v, double to, double k, double eps, ref bool busy)
            {
                if (v == to) return v;
                double n = v + (to - v) * k;
                if (Math.Abs(to - n) < eps) return to;
                busy = true;
                return n;
            }

            // A smooth bell around the mouse, measured on the resting layout (so the row never chases the pointer).
            void Targets()
            {
                double x = Pad - offset;
                foreach (Entry en in list)
                {
                    double w = en.W * en.Vis, c = x + w / 2, t = 1;
                    x += w;
                    if (en.Magnify && en.VisTo > 0 && !double.IsNaN(mouse))
                    {
                        double d = Math.Abs(c - mouse);
                        if (d < Reach) t = 1 + (DockMagnify - 1) * (Math.Cos(Math.PI * d / Reach) + 1) / 2;
                    }
                    en.ScaleTo = t;
                }
            }

            protected override Size MeasureOverride(Size avail)
            {
                foreach (UIElement c in Children) c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return new Size(double.IsInfinity(avail.Width) ? 400 : avail.Width, Top + DockItemH + Bottom);
            }

            protected override Size ArrangeOverride(Size size)
            {
                width = size.Width;
                int n = list.Count;
                double[] left = new double[n], bw = new double[n], ex = new double[n];
                double x = Pad, total = 0;
                for (int i = 0; i < n; i++)
                {
                    Entry e = list[i];
                    bw[i] = e.W * e.Vis;
                    left[i] = x;
                    x += bw[i];
                    ex[i] = e.Magnify ? (e.Scale - 1) * DockTile * e.Vis : 0;
                    total += ex[i];
                }
                double maxOff = Math.Max(0, x + Pad - size.Width);
                if (reveal != null && size.Width > 0)
                {
                    for (int i = 0; i < n; i++)
                        if (list[i].Tag == reveal)
                        {
                            if (left[i] + bw[i] > size.Width - Pad) offset = offsetTo = Math.Max(0, Math.Min(maxOff, left[i] + bw[i] / 2 - size.Width / 2));
                            break;
                        }
                    reveal = null;
                }
                offset = Math.Max(0, Math.Min(offset, maxOff));

                // The magnified tiles push their neighbours apart around the point under the mouse, which stays put.
                double m = !double.IsNaN(mouse) ? mouse : anchor, comp = 0;
                if (!double.IsNaN(m) && total > 0.01)
                {
                    double mb = m + offset;
                    for (int i = 0; i < n; i++)
                    {
                        if (bw[i] <= 0.01) continue;
                        if (mb >= left[i] + bw[i]) comp += ex[i];
                        else if (mb > left[i]) comp += ex[i] * (mb - left[i]) / bw[i];
                    }
                }
                double shift = -offset - comp, lo = Pad + shift, hi = x - Pad + total + shift, fix = 0;
                // ...but the row doesn't spill past the ends of the dock (the end tiles keep a little air from its edge).
                const double Air = 5;
                double needL = offset < 1 && lo < Air ? Air - lo : 0, needR = offset > maxOff - 1 && hi > size.Width - Air ? size.Width - Air - hi : 0;
                if (needL > 0 && needR < 0) fix = (needL + needR) / 2;
                else fix = needL > 0 ? needL : needR;
                shift += fix;

                double acc = 0, hudCx = 0, hudTop = 0;
                for (int i = 0; i < n; i++)
                {
                    Entry e = list[i];
                    double sl = left[i] + acc + shift, sw = bw[i] + ex[i];
                    acc += ex[i];
                    Size d = e.El.DesiredSize;
                    double aw = e.Tile != null ? Math.Max(d.Width, sw) : d.Width;
                    e.El.Arrange(new Rect(sl + (sw - aw) / 2, Top, aw, d.Height));
                    if (e == hudOn) { hudCx = sl + sw / 2; hudTop = Top + DockTile * (1 - e.Scale); }
                }
                if (hudOn != null)
                {
                    Size hs = hud.DesiredSize;
                    hudRect = new Rect(Math.Max(4, Math.Min(size.Width - 4 - hs.Width, hudCx - hs.Width / 2)), hudTop - 9 - hs.Height, hs.Width, hs.Height);
                    if (hudShown) ReportHud(hudRect);
                }
                hud.Arrange(hudRect);
                if (clipFor != size)
                {
                    clipFor = size;
                    Clip = new RectangleGeometry(new Rect(0, -90, size.Width, size.Height + 90));
                }
                if (Edges != null) Edges(offset, maxOff - offset);
                return size;
            }
        }

        // One tile: a rounded face with the preview, its name below, the worn ring, a padlock veil, a loading shimmer and a
        // corner badge (the favorite star, or the button that removes a saved look).
        class ShelfTile : Grid
        {
            public enum Badge { None, Star, Remove }
            public readonly Grid Lift = new Grid(), Inner = new Grid();
            public readonly ScaleTransform LiftScale = new ScaleTransform(1, 1), VisScale = new ScaleTransform(1, 1), PressScale = new ScaleTransform(1, 1);
            public readonly TranslateTransform Enter = new TranslateTransform();
            public readonly Image Img;
            readonly Border face, ring;
            readonly CrispText caption;
            readonly Badge kind;
            Grid badge;
            Border badgeFace, placeholder, veil;
            StarMark star;
            ScaleTransform starScale, starRingScale;
            Sh.Ellipse starRing;
            GlyphView cross, addIcon, actionIcon;
            bool worn, hot, fav, locked, adding, badgeHot, badgeDown, pressed;
            public event Action BadgeClick, Click;

            public ShelfTile(string text, Badge kind)
            {
                this.kind = kind;
                MinWidth = DockSlot;    // the dock widens it to its whole slot, so there are no dead gaps between tiles
                Height = DockItemH;
                Background = Brushes.Transparent;
                Cursor = Cursors.Hand;
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = VisScale;
                TransformGroup tg = new TransformGroup();
                tg.Children.Add(PressScale);
                tg.Children.Add(Enter);
                Inner.RenderTransformOrigin = new Point(0.5, 0.4);
                Inner.RenderTransform = tg;
                Children.Add(Inner);

                Lift.Width = Lift.Height = DockTile;
                Lift.HorizontalAlignment = HorizontalAlignment.Center;
                Lift.VerticalAlignment = VerticalAlignment.Top;
                Lift.RenderTransformOrigin = new Point(0.5, 1);
                Lift.RenderTransform = LiftScale;
                face = new Border { CornerRadius = new CornerRadius(14) };
                Lift.Children.Add(face);
                Img = new Image { Width = DockTile, Height = DockTile, Stretch = Stretch.Fill, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(Img, BitmapScalingMode.HighQuality);
                Lift.Children.Add(Img);
                ring = new Border { CornerRadius = new CornerRadius(16.5), Margin = new Thickness(-2.5), BorderThickness = new Thickness(2), IsHitTestVisible = false };
                Lift.Children.Add(ring);
                if (kind != Badge.None) BuildBadge();
                Inner.Children.Add(Lift);

                caption = new CrispText(text ?? "", 11, DockSlot - 2) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1) };
                Inner.Children.Add(caption);

                MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    pressed = true;
                    CaptureMouse();
                    Squeeze(0.92, 80);
                };
                MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    bool inside = pressed && new Rect(RenderSize).Contains(e.GetPosition(this));
                    pressed = false;
                    if (IsMouseCaptured) ReleaseMouseCapture();
                    Squeeze(1, 220);
                    Action h = Click;
                    if (inside && h != null) h();
                };
                LostMouseCapture += delegate { if (pressed) { pressed = false; Squeeze(1, 220); } };
                Paint();
            }

            public bool Worn { get { return worn; } }

            void Squeeze(double to, double ms)
            {
                DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                PressScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                PressScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }

            void BuildBadge()
            {
                Palette pal = Ds.Brushes;
                badge = new Grid { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, -6, 0), Cursor = Cursors.Hand };
                if (kind == Badge.Star)
                {
                    starRingScale = new ScaleTransform(1, 1);
                    starRing = new Sh.Ellipse { Stroke = Ds.Brush(StarYellow), StrokeThickness = 1.6, Opacity = 0, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = starRingScale };
                    badge.Children.Add(starRing);
                }
                badgeFace = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
                badge.Children.Add(badgeFace);
                if (kind == Badge.Star)
                {
                    starScale = new ScaleTransform(1, 1);
                    star = new StarMark { Width = 11, Height = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = starScale };
                    badgeFace.Child = star;
                }
                else
                {
                    cross = new GlyphView("close", 12, pal.Label2, 1.8) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    badgeFace.Child = cross;
                }
                badge.MouseEnter += delegate { badgeHot = true; Paint(); };
                badge.MouseLeave += delegate { badgeHot = false; badgeDown = false; Paint(); };
                badge.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; badgeDown = true; };
                badge.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    if (!badgeDown) return;
                    badgeDown = false;
                    Action h = BadgeClick;
                    if (h != null) h();
                };
                Lift.Children.Add(badge);
            }

            public void Set(bool worn, bool fav) { this.worn = worn; this.fav = fav; Paint(); }
            public void SetHot(bool on) { if (hot == on) return; hot = on; if (!on) badgeHot = false; Paint(); }

            // The star pops in with a ring when it turns on, and dips when it turns off.
            public void SetFav(bool on, bool animate)
            {
                bool was = fav;
                fav = on;
                Paint();
                if (!animate || star == null || was == on) return;
                if (on)
                {
                    popEnds = Anim.Now + 520;
                    DoubleAnimation grow = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 3, EasingMode = EasingMode.EaseOut } };
                    starScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                    starScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                    DoubleAnimation wave = new DoubleAnimation(0.8, 2.4, TimeSpan.FromMilliseconds(520)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    starRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, wave);
                    starRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, wave);
                    starRing.BeginAnimation(OpacityProperty, new DoubleAnimation(0.95, 0, TimeSpan.FromMilliseconds(520)));
                }
                else
                {
                    DoubleAnimation dip = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut } };
                    starScale.BeginAnimation(ScaleTransform.ScaleXProperty, dip);
                    starScale.BeginAnimation(ScaleTransform.ScaleYProperty, dip);
                }
            }

            double popEnds;
            public bool StarPopping { get { return Anim.Now < popEnds; } }

            // The tile that saves the current look: a dashed outline and a plus.
            public void MakeAdd()
            {
                Palette pal = Ds.Brushes;
                adding = true;
                Sh.Rectangle dash = new Sh.Rectangle
                {
                    RadiusX = 14, RadiusY = 14, Stroke = Ds.Brush(pal.Label3), StrokeThickness = 1.4, StrokeDashArray = new DoubleCollection { 3, 2.6 },
                    Margin = new Thickness(0.7), IsHitTestVisible = false
                };
                Lift.Children.Insert(1, dash);
                addIcon = new GlyphView("plus", 22, pal.Label2, 1.8) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Lift.Children.Insert(2, addIcon);
                Paint();
            }

            // A command: an icon on the face instead of a preview.
            public void MakeAction(string glyph)
            {
                actionIcon = new GlyphView(glyph, 26, Ds.Brushes.Label, 1.7) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                Lift.Children.Insert(Lift.Children.IndexOf(ring), actionIcon);
                Paint();
            }

            public void Lock(int level)
            {
                if (veil != null) Lift.Children.Remove(veil);
                locked = true;
                // Dimmed as macOS does: a dark veil in dark mode, a frosted one in light mode.
                bool dark = Ds.Brushes.Dark;
                Color ink = dark ? Colors.White : Ds.Rgb(110, 110, 118);
                veil = new Border { CornerRadius = new CornerRadius(14), Background = Ds.Brush(dark ? Ds.Argb(0.56, 18, 18, 20) : Ds.Argb(0.66, 248, 248, 250)), IsHitTestVisible = false };
                StackPanel p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                p.Children.Add(new PadlockGlyph(ink) { Width = 14, Height = 16, HorizontalAlignment = HorizontalAlignment.Center });
                TextBlock t = Label("Nv. " + (level + 1), Ds.Semibold, 10, ink, new Thickness(0, 3, 0, 0));
                t.HorizontalAlignment = HorizontalAlignment.Center;
                p.Children.Add(t);
                veil.Child = p;
                Lift.Children.Insert(Lift.Children.IndexOf(ring), veil);
                Paint();
            }

            public bool Locked { get { return locked; } }

            // Unlocked while on screen: the veil lifts off and dissolves, and the tile breathes in once.
            public void Unlock(bool animate)
            {
                if (!locked) return;
                locked = false;
                Border v = veil;
                veil = null;
                Paint();
                if (v == null) return;
                if (!animate) { Lift.Children.Remove(v); return; }
                ScaleTransform s = new ScaleTransform(1, 1);
                v.RenderTransformOrigin = new Point(0.5, 0.5);
                v.RenderTransform = s;
                DoubleAnimation lift = new DoubleAnimation(1, 1.16, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                s.BeginAnimation(ScaleTransform.ScaleXProperty, lift);
                s.BeginAnimation(ScaleTransform.ScaleYProperty, lift);
                DoubleAnimation fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                fade.Completed += delegate { Lift.Children.Remove(v); };
                v.BeginAnimation(OpacityProperty, fade);
                DoubleAnimationUsingKeyFrames breathe = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(640) };
                breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.09, KeyTime.FromPercent(0.4), new CubicEase { EasingMode = EasingMode.EaseOut }));
                breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }));
                PressScale.BeginAnimation(ScaleTransform.ScaleXProperty, breathe);
                PressScale.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);
            }

            // A soft placeholder while the preview renders.
            public void Shimmer(Brush b)
            {
                if (placeholder != null) return;
                placeholder = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(13), Background = b, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 17, 0, 0) };
                Lift.Children.Insert(Lift.Children.IndexOf(Img) + 1, placeholder);
                Img.Opacity = 0;
            }

            public bool Shimmering { get { return placeholder != null; } }

            public void Unshimmer()
            {
                Border p = placeholder;
                if (p == null) return;
                placeholder = null;
                DoubleAnimation fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
                fade.Completed += delegate { Lift.Children.Remove(p); };
                p.BeginAnimation(OpacityProperty, fade);
                Img.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
            }

            // Tiles cascade in when a tab opens.
            public void Arrive(double delay)
            {
                TimeSpan at = TimeSpan.FromMilliseconds(delay);
                Inner.Opacity = 0;
                Enter.Y = 14;
                Inner.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = at });
                Enter.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(380)) { BeginTime = at, EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut } });
            }

            // Seen dx away a moment ago (the row was reordered): glides from there to its place, longer trips a bit slower.
            public void Slide(double dx)
            {
                double ms = 300 + Math.Min(260, Math.Abs(dx) * 0.22);
                Enter.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(dx, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            }

            // A locked tile says no.
            public void Shake()
            {
                DoubleAnimationUsingKeyFrames k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
                double[] xs = { -5, 5, -4, 3, -1.5, 0 };
                for (int i = 0; i < xs.Length; i++) k.KeyFrames.Add(new EasingDoubleKeyFrame(xs[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60 * (i + 1)))));
                Enter.BeginAnimation(TranslateTransform.XProperty, k);
            }

            void Paint()
            {
                Palette pal = Ds.Brushes;
                Color rest = pal.Dark ? Ds.Argb(0.07, 255, 255, 255) : Ds.Rgb(242, 242, 245), over = pal.Dark ? Ds.Argb(0.12, 255, 255, 255) : Ds.Rgb(233, 233, 238);
                if (adding)
                {
                    face.Background = hot ? Ds.Brush(Ds.WithAlpha(pal.Accent, pal.Dark ? 0.18 : 0.08)) : Brushes.Transparent;
                    if (addIcon != null) addIcon.Color = hot ? pal.Accent : pal.Label2;
                }
                else face.Background = Ds.Brush(worn ? Ds.WithAlpha(pal.Accent, pal.Dark ? 0.26 : 0.13) : hot ? over : rest);
                ring.BorderBrush = worn ? Ds.Brush(pal.Accent) : Brushes.Transparent;
                caption.Set(worn || hot ? pal.Label : pal.Label2, worn);
                if (actionIcon != null) actionIcon.Color = hot ? pal.Accent : pal.Label;
                if (badge == null) return;
                bool show = kind == Badge.Remove ? hot : fav || (hot && !locked);
                badge.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (!show) return;
                badgeFace.Background = Ds.Brush(pal.Dark ? (badgeHot ? Ds.Rgb(80, 80, 85) : Ds.Rgb(62, 62, 66)) : (badgeHot ? Ds.Rgb(250, 250, 252) : Colors.White));
                badgeFace.BorderBrush = Ds.Brush(pal.Dark ? Ds.Argb(0.14, 255, 255, 255) : Ds.Argb(0.12, 0, 0, 0));
                if (star != null)
                {
                    star.Set(fav, fav ? StarYellow : badgeHot ? pal.Label : pal.Label2);
                    badge.ToolTip = fav ? "Quitar de favoritos" : "A\u00F1adir a favoritos";
                }
                if (cross != null)
                {
                    cross.Color = badgeHot ? pal.Label : pal.Label2;
                    badge.ToolTip = "Quitar de Mis looks";
                }
            }
        }

        // Five-pointed star, filled or outlined.
        // Small text drawn on whole device pixels in the hinted (display) mode, so it stays sharp wherever the tile sits.
        class CrispText : FrameworkElement
        {
            readonly string text;
            readonly double size, maxWidth;
            Color color;
            bool bold;
            FormattedText ft;

            public CrispText(string text, double size, double maxWidth)
            {
                this.text = text;
                this.size = size;
                this.maxWidth = maxWidth;
                color = Ds.Brushes.Label2;
                IsHitTestVisible = false;
                SnapsToDevicePixels = true;
            }

            public void Set(Color c, bool bold)
            {
                if (c == color && bold == this.bold && ft != null) return;
                color = c;
                this.bold = bold;
                ft = null;
                InvalidateMeasure();
                InvalidateVisual();
            }

            FormattedText Build()
            {
                if (ft == null)
                {
                    ft = Ink.Text(text, bold ? Ds.Semibold : Ds.Regular, size, color);
                    ft.MaxTextWidth = maxWidth;
                    ft.MaxLineCount = 1;
                    ft.Trimming = TextTrimming.CharacterEllipsis;
                }
                return ft;
            }

            protected override Size MeasureOverride(Size avail)
            {
                FormattedText t = Build();
                return new Size(Math.Ceiling(Math.Min(maxWidth, t.WidthIncludingTrailingWhitespace)), Math.Ceiling(t.Height));
            }

            protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
            {
                base.OnDpiChanged(oldDpi, newDpi);
                ft = null;
                InvalidateMeasure();
            }

            protected override void OnRender(DrawingContext dc) { dc.DrawText(Build(), new Point(0, 0)); }
        }

        class StarMark : FrameworkElement
        {
            bool filled;
            Color ink;

            public StarMark() { IsHitTestVisible = false; }

            public void Set(bool filled, Color ink)
            {
                if (this.filled == filled && this.ink == ink) return;
                this.filled = filled;
                this.ink = ink;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight, r = Math.Min(w, h) / 2;
                if (r <= 0) return;
                StreamGeometry g = new StreamGeometry();
                using (StreamGeometryContext c = g.Open())
                {
                    for (int i = 0; i < 10; i++)
                    {
                        double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : r * 0.47;
                        Point p = new Point(w / 2 + Math.Cos(a) * rr, h / 2 + r * 0.08 + Math.Sin(a) * rr);
                        if (i == 0) c.BeginFigure(p, true, true);
                        else c.LineTo(p, true, true);
                    }
                }
                g.Freeze();
                Pen pen = new Pen(Ds.Brush(ink), filled ? 1 : 1.3) { LineJoin = PenLineJoin.Round };
                pen.Freeze();
                dc.DrawGeometry(filled ? Ds.Brush(ink) : null, pen, g);
            }
        }

        // A row of hearts filled up to `fill` (2.4: two full, the third 40 % full), the rest faint. A new capture fills
        // it a little more and the heart that gets there beats; completing a heart (a new level) makes it pop with a ring.
        class HeartsMark : FrameworkElement
        {
            public static readonly DependencyProperty FillProperty = DependencyProperty.Register("HeartsFill", typeof(double), typeof(HeartsMark),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            public static readonly DependencyProperty BeatProperty = DependencyProperty.Register("HeartsBeat", typeof(double), typeof(HeartsMark),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            const double Size = 12, Step = 13.5;
            readonly int count;
            int beatAt = -1;
            bool beatBig;

            public HeartsMark(int count, double fill)
            {
                this.count = count;
                SetValue(FillProperty, Math.Max(0, Math.Min(count, fill)));
                Width = Step * (count - 1) + Size;
                Height = Size;
                IsHitTestVisible = false;
            }

            public double Fill { get { return (double)GetValue(FillProperty); } }

            public void FillTo(double to, bool animate)
            {
                to = Math.Max(0, Math.Min(count, to));
                double from = Fill;
                if (!animate || Math.Abs(to - from) < 1e-6)
                {
                    BeginAnimation(FillProperty, null);
                    SetValue(FillProperty, to);
                    return;
                }
                int fromWhole = (int)Math.Floor(from + 1e-6), toWhole = (int)Math.Floor(to + 1e-6);
                bool big = toWhole > fromWhole;
                int at = Math.Min(count - 1, big ? toWhole - 1 : toWhole);
                BeginAnimation(FillProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(big ? 620 : 420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
                // Captures in quick succession: the heart already beating (as big) plays on; any other beat starts from
                // rest, never from where the last one was (that heart would sit swollen until its turn came).
                double b = (double)GetValue(BeatProperty);
                if (b > 0 && b < 1 && at == beatAt && (beatBig || !big)) return;
                beatBig = big;
                beatAt = at;
                BeginAnimation(BeatProperty, null);
                BeginAnimation(BeatProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(big ? 720 : 460)) { BeginTime = TimeSpan.FromMilliseconds(big ? 380 : 120) });
            }

            protected override void OnRender(DrawingContext dc)
            {
                Palette pal = Ds.Brushes;
                Color off = pal.Dark ? Ds.Argb(0.2, 255, 255, 255) : Ds.Argb(0.14, 0, 0, 0);
                double fill = Fill, beat = (double)GetValue(BeatProperty);
                for (int i = 0; i < count; i++)
                {
                    double x = i * Step, f = Math.Max(0, Math.Min(1, fill - i));
                    bool beating = i == beatAt && beat > 0 && beat < 1;
                    if (beating)
                    {
                        double k = Math.Sin(Math.PI * beat) * (beatBig ? 0.42 : 0.2);
                        dc.PushTransform(new ScaleTransform(1 + k, 1 + k, x + Size / 2, 0.5 + Size / 2));
                    }
                    Glyph.Draw(dc, "heart!", x, 0.5, Size, f >= 1 ? LovePink : off, 1);
                    if (f > 0 && f < 1)
                    {
                        // The heart is drawn between x+1.5 and x+10.5 of its box: fill that span by the progress.
                        dc.PushClip(new RectangleGeometry(new Rect(x, 0, Size * 0.125 + Size * 0.75 * f, Size + 1)));
                        Glyph.Draw(dc, "heart!", x, 0.5, Size, LovePink, 1);
                        dc.Pop();
                    }
                    if (beating) dc.Pop();
                    if (beating && beatBig)
                    {
                        // A ring of the same pink rolling out of the completed heart.
                        Pen ring = new Pen(Ds.Brush(Ds.WithAlpha(LovePink, 0.55 * (1 - beat))), 1.3);
                        dc.DrawEllipse(null, ring, new Point(x + Size / 2, 0.5 + Size / 2), Size * (0.45 + 0.75 * beat), Size * (0.45 + 0.75 * beat));
                    }
                }
            }
        }

        // A four-pointed sparkle (the shape of the "sparkle" icon), filled.
        class SparkMark : FrameworkElement
        {
            readonly Color ink;

            public SparkMark(Color ink) { this.ink = ink; IsHitTestVisible = false; }

            protected override void OnRender(DrawingContext dc)
            {
                Geometry g = Glyph.Shape("sparkle");
                double s = Math.Min(ActualWidth, ActualHeight) / 24.0;
                if (g == null || s <= 0) return;
                dc.PushTransform(new ScaleTransform(s, s));
                dc.DrawGeometry(Ds.Brush(ink), null, g);
                dc.Pop();
            }
        }

        class PadlockGlyph : FrameworkElement
        {
            readonly Brush ink;

            public PadlockGlyph(Color c) { ink = Ds.Brush(c); }

            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight;
                Pen p = new Pen(ink, 1.8);
                StreamGeometry arc = new StreamGeometry();
                using (StreamGeometryContext c = arc.Open())
                {
                    c.BeginFigure(new Point(w * 0.25, h * 0.46), false, false);
                    c.LineTo(new Point(w * 0.25, h * 0.32), true, true);
                    c.ArcTo(new Point(w * 0.75, h * 0.32), new Size(w * 0.25, w * 0.25), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(w * 0.75, h * 0.46), true, true);
                }
                arc.Freeze();
                dc.DrawGeometry(null, p, arc);
                dc.DrawRoundedRectangle(ink, null, new Rect(0, h * 0.44, w, h * 0.56), 3, 3);
            }
        }

        // Category tabs with a selection that slides (and stretches) from one to the next. While searching, tabs with
        // results get a dot and the others dim.
        class DockTabsBar : Drawn
        {
            public static readonly DependencyProperty SlideProperty = DependencyProperty.Register("TabSlide", typeof(double), typeof(DockTabsBar),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
            const double PadX = 10, Inset = 2;
            readonly string[] labels;
            readonly double[] xs, ws;
            int selected;
            int[] matches;
            int hovered = -1;                     // the tab under the mouse; repainted only when it changes
            public event Action<int> Changed;

            public DockTabsBar(string[] labels, int selected)
            {
                this.labels = labels;
                xs = new double[labels.Length];
                ws = new double[labels.Length];
                double x = Inset;
                for (int i = 0; i < labels.Length; i++)
                {
                    ws[i] = Math.Ceiling(Text(labels[i], Ds.Semibold, 12, Colors.Black).WidthIncludingTrailingWhitespace) + PadX * 2;
                    xs[i] = x;
                    x += ws[i];
                }
                Width = x + Inset;
                Height = 28;
                this.selected = Math.Max(0, Math.Min(labels.Length - 1, selected));
                SetValue(SlideProperty, (double)this.selected);
            }

            public int Selected
            {
                get { return selected; }
                set
                {
                    value = Math.Max(0, Math.Min(labels.Length - 1, value));
                    if (value == selected) return;
                    selected = value;
                    Spring(this, SlideProperty, value, 360);
                }
            }

            public int[] Matches { get { return matches; } }
            public void SetMatches(int[] counts) { matches = counts; InvalidateVisual(); }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int h = At(e.GetPosition(this).X);
                if (h != hovered) { hovered = h; InvalidateVisual(); }
            }

            protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); hovered = -1; InvalidateVisual(); }

            int At(double x)
            {
                for (int i = 0; i < labels.Length; i++) if (x < xs[i] + ws[i]) return i;
                return labels.Length - 1;
            }

            protected override void Clicked(Point p)
            {
                int i = At(p.X);
                if (i == selected) return;
                Selected = i;
                Action<int> h = Changed;
                if (h != null) h(i);
            }

            protected override void OnRender(DrawingContext dc)
            {
                Palette pal = Ds.Brushes;
                double w = ActualWidth, h = ActualHeight, s = (double)GetValue(SlideProperty);
                int n = labels.Length;
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.08, 255, 255, 255) : Ds.Argb(0.055, 0, 0, 0)), null, new Rect(0, 0, w, h), 9, 9);
                int hov = Hot > 0 ? hovered : -1;
                if (hov >= 0 && hov != selected)
                    dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Argb(0.06, 255, 255, 255) : Ds.Argb(0.045, 0, 0, 0)), null, new Rect(xs[hov], Inset, ws[hov], h - Inset * 2), 7, 7);
                // The pill between two tabs takes the position and width of both, so it stretches as it travels.
                int i0 = Math.Max(0, Math.Min(n - 2, (int)Math.Floor(s)));
                double f = s - i0, px = xs[i0] + (xs[i0 + 1] - xs[i0]) * f, pw = ws[i0] + (ws[i0 + 1] - ws[i0]) * f;
                Rect pill = new Rect(px, Inset, Math.Max(10, pw), h - Inset * 2);
                if (!pal.Dark)
                {
                    Rect sh = pill;
                    sh.Offset(0, 0.7);
                    dc.DrawRoundedRectangle(Ds.Brush(Ds.Argb(0.14, 0, 0, 0)), null, sh, 7, 7);
                }
                dc.DrawRoundedRectangle(Ds.Brush(pal.Dark ? Ds.Rgb(99, 99, 104) : Colors.White), null, pill, 7, 7);
                for (int i = 0; i < n; i++)
                {
                    bool on = i == selected, dim = matches != null && matches[i] == 0 && !on;
                    Color c = on ? pal.Label : dim ? pal.Label3 : Mix(pal.Label2, pal.Label, i == hov ? 0.6 : 0);
                    FormattedText t = Text(labels[i], on ? Ds.Semibold : Ds.Regular, 12, c);
                    Ink.Center(dc, t, new Rect(xs[i], 0, ws[i], h));
                    if (matches != null && matches[i] > 0)
                    {
                        double dx = Math.Round(xs[i] + (ws[i] + t.WidthIncludingTrailingWhitespace) / 2) + 3.5;
                        dc.DrawEllipse(Ds.Brush(pal.Accent), null, new Point(dx, h / 2 - 5), 2.5, 2.5);
                    }
                }
            }
        }

        // ---- Previews: rendered on a worker thread, cached by look and size.

        static readonly Dictionary<string, BitmapSource> previewCache = new Dictionary<string, BitmapSource>();
        static readonly Dictionary<string, BitmapSource> previewInSlot = new Dictionary<string, BitmapSource>();
        static readonly Dictionary<string, List<Image>> previewWaiting = new Dictionary<string, List<Image>>();

        double DeviceScale()
        {
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null) return Math.Max(1, src.CompositionTarget.TransformToDevice.M11);
                return Math.Max(1, VisualTreeHelper.GetDpi(this).DpiScaleX);
            }
            catch { return 1; }
        }

        // Shows a still of the mascot wearing a look in img (dip x dip). Until a missing one is ready, the slot keeps
        // showing what it showed before, so changing the look never blanks the dock.
        void ShowPreview(Image img, MascotLook look, double dip, string slot)
        {
            int px = Math.Max(8, (int)Math.Round(dip * DeviceScale()));
            string key = look.Key + "@" + px;
            img.Tag = key;
            img.Uid = slot;
            BitmapSource b;
            if (previewCache.TryGetValue(key, out b)) { img.Source = b; previewInSlot[slot] = b; return; }
            if (previewInSlot.TryGetValue(slot, out b)) img.Source = b;
            List<Image> waiting;
            if (previewWaiting.TryGetValue(key, out waiting)) { waiting.Add(img); return; }
            waiting = new List<Image>();
            waiting.Add(img);
            previewWaiting[key] = waiting;
            MascotLook copy = look.Clone();
            Dispatcher ui = Dispatcher;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                D.Bitmap bmp = null;
                try
                {
                    bmp = new D.Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (D.Graphics g = D.Graphics.FromImage(bmp))
                    {
                        float d = px * 0.62f;
                        Mascot.RenderStill(g, new D.RectangleF((px - d) / 2, px * 0.22f, d, d), copy);
                    }
                }
                catch (Exception ex)
                {
                    ShotStack.Log("Vista previa: " + ex.Message);
                    if (bmp != null) { bmp.Dispose(); bmp = null; }
                }
                D.Bitmap done = bmp;
                ui.BeginInvoke((Action)delegate
                {
                    List<Image> imgs;
                    if (!previewWaiting.TryGetValue(key, out imgs)) imgs = new List<Image>();
                    previewWaiting.Remove(key);
                    if (done == null) { foreach (Image i in imgs) PreviewArrived(i); return; }
                    BitmapSource s;
                    try { s = Ink.FromGdi(done); }
                    finally { done.Dispose(); }
                    TrimPreviews();
                    previewCache[key] = s;
                    foreach (Image i in imgs)
                    {
                        if (!(i.Tag is string) || (string)i.Tag != key) continue;
                        i.Source = s;
                        string sl = i.Uid;
                        if (!string.IsNullOrEmpty(sl)) previewInSlot[sl] = s;
                        PreviewArrived(i);
                    }
                });
            });
        }

        // Keeps the cache bounded; previews still on screen stay.
        static void TrimPreviews()
        {
            if (previewCache.Count < 400) return;
            HashSet<BitmapSource> keep = new HashSet<BitmapSource>(previewInSlot.Values);
            foreach (KeyValuePair<string, BitmapSource> kv in new List<KeyValuePair<string, BitmapSource>>(previewCache))
                if (!keep.Contains(kv.Value)) previewCache.Remove(kv.Key);
        }
    }
}
