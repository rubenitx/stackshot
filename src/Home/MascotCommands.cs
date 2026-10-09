// Stackshot - Commands the user can give the mascot (from Home, its page or the desktop menu) and the accessories it can wear.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackshot
{
    public static class MascotCmd
    {
        public const int Play = 0, Juggle = 1, Dance = 2, Sleep = 3, Jump = 4, Twirl = 5, Wave = 6, Flip = 7, Bow = 8, Cheer = 9, Love = 10,
                         Bubbles = 11, Coffee = 12, Read = 13, Console = 14, Photo = 15, Stretch = 16;
        public const int Count = 17;

        // What it may do on its own (settings bits).
        public const int AloneSmall = 1, AloneDance = 2, AloneProps = 4, AloneLook = 8, AloneAll = 15;

        public static readonly string[] Names =
        {
            "Ponte a jugar", "Haz malabares", "Baila", "Duerme", "Salta", "Gira", "Saluda", "Voltereta", "Reverencia", "Anima",
            "Dame cari\u00F1o", "Haz burbujas", "Tom\u00E1te un caf\u00E9", "Lee un rato", "Juega a la consola", "Haz una foto", "Est\u00EDrate"
        };

        public static readonly string[] Short =
        {
            "Jugar", "Malabares", "Baila", "Duerme", "Salta", "Gira", "Saluda", "Voltereta", "Reverencia", "Anima", "Cari\u00F1o", "Burbujas", "Caf\u00E9", "Leer", "Consola", "Foto", "Estirar"
        };

        public static readonly string[] Hints =
        {
            "Elige un juego al azar", "Tres bolas en el aire", "Mueve el esqueleto", "Bosteza y se echa a dormir", "Tres saltos seguidos", "Una pirueta completa",
            "Te saluda con la mano", "Un salto mortal hacia atr\u00E1s", "Una reverencia muy educada", "Confeti para celebrarlo", "Se llena de corazones",
            "Soplan pompas de jab\u00F3n", "Una pausa con aroma", "Un cap\u00EDtulo tranquilo", "Una partidita r\u00E1pida", "Sonr\u00EDe, que sale el p\u00E1jaro", "Un buen estir\u00F3n"
        };

        public static readonly string[] Glyphs =
        {
            "play", "sparkle", "note", "moon", "hop", "update", "heart", "redo", "bow", "sparkle", "heart", "bubble", "coffee", "book", "gamepad", "camera", "stretch"
        };

        // The chips on Home and the mascot page.
        public static readonly int[] Quick = { Play, Juggle, Dance, Sleep, Jump };

        public static bool IsProp(int cmd) { return cmd == Coffee || cmd == Read || cmd == Console || cmd == Photo || cmd == Stretch || cmd == Juggle; }
    }

    public static partial class MascotParts
    {
        public static readonly string[] AccessoryNames = { "Ninguno", "Gafas de coraz\u00F3n", "Gafas 3D", "Gafas de aviador", "Bufanda a rayas", "Collar de flores",
                                                           "Orejeras", "Mariposa", "Medalla de oro", "Estrellas en las mejillas", "Pecas" };

        static readonly Color[] Petals =
        {
            Color.FromArgb(255, 112, 150), Color.FromArgb(255, 214, 10), Color.FromArgb(255, 255, 255), Color.FromArgb(255, 150, 60), Color.FromArgb(180, 140, 255)
        };

        // Worn over the look: most pieces go under the hat, the butterfly sits on top of it.
        public static void PaintAccessory(Graphics g, MascotLook l, float D, double now, bool front)
        {
            int a = l.Accessory;
            if (a <= 0 || a >= AccessoryNames.Length || (a == 7) != front) return;
            Geo geo = GeoFor(l.Kind);
            float fy = geo.FaceY * D, gap = geo.EyeGap * D, ny = geo.NeckY * D, nw = geo.NeckW * D, hw = geo.HeadW * D / 2;
            switch (a)
            {
                case 1:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        using (GraphicsPath hp = Heart(s * gap, fy + 0.005f * D, 0.2f * D))
                        {
                            FillSolid(g, hp, Color.FromArgb(215, 255, 92, 140));
                            Stroke(g, hp, Color.FromArgb(235, 190, 24, 78), D * 0.013f);
                        }
                        Ellipse(g, Color.FromArgb(190, 255, 255, 255), s * gap - 0.05f * D, fy - 0.045f * D, 0.032f * D, 0.024f * D);
                    }
                    Line(g, Color.FromArgb(235, 190, 24, 78), D * 0.013f, -gap + 0.07f * D, fy - 0.012f * D, gap - 0.07f * D, fy - 0.012f * D);
                    break;
                case 2:
                    for (int i = 0; i < 2; i++)
                    {
                        RectangleF lens = new RectangleF((i == 0 ? -gap : gap) - 0.078f * D, fy - 0.05f * D, 0.156f * D, 0.104f * D);
                        using (GraphicsPath lp = Theme.Round(lens, 0.03f * D))
                        {
                            FillSolid(g, lp, i == 0 ? Color.FromArgb(205, 235, 50, 70) : Color.FromArgb(205, 40, 205, 235));
                            Stroke(g, lp, Color.FromArgb(245, 250, 250, 250), D * 0.015f);
                        }
                    }
                    Line(g, Color.FromArgb(245, 250, 250, 250), D * 0.015f, -gap + 0.078f * D, fy - 0.02f * D, gap - 0.078f * D, fy - 0.02f * D);
                    break;
                case 3:
                    foreach (int s in new int[] { -1, 1 })
                    {
                        RectangleF lens = new RectangleF(s * gap - 0.085f * D, fy - 0.045f * D, 0.17f * D, 0.15f * D);
                        using (GraphicsPath lp = new GraphicsPath())
                        {
                            lp.AddEllipse(lens);
                            Fill(g, lp, lens, Color.FromArgb(215, 46, 62, 84), Color.FromArgb(185, 140, 168, 190), 90f);
                            Stroke(g, lp, Gold2, D * 0.014f);
                        }
                        Line(g, Color.FromArgb(120, 255, 255, 255), D * 0.012f, s * gap - 0.04f * D, fy + 0.0f * D, s * gap - 0.01f * D, fy - 0.025f * D);
                    }
                    Line(g, Gold2, D * 0.014f, -gap + 0.085f * D, fy - 0.025f * D, gap - 0.085f * D, fy - 0.025f * D);
                    break;
                case 4:
                {
                    Color red = Color.FromArgb(226, 56, 70), cream = Color.FromArgb(252, 246, 238);
                    RectangleF band = new RectangleF(-nw * 0.53f, ny - 0.045f * D, nw * 1.06f, 0.09f * D);
                    using (GraphicsPath bp = Theme.Round(band, 0.04f * D))
                    {
                        FillSolid(g, bp, cream);
                        for (int i = 0; i < 6; i++)
                            Line(g, red, D * 0.03f, band.X + band.Width * (0.1f + i * 0.16f), band.Y + 0.012f * D, band.X + band.Width * (0.1f + i * 0.16f), band.Bottom - 0.012f * D);
                        Stroke(g, bp, Color.FromArgb(50, 0, 0, 0), D * 0.008f);
                    }
                    GraphicsState st = g.Save();
                    g.TranslateTransform(nw * 0.26f, ny + 0.03f * D);
                    g.RotateTransform(-6 + (float)Math.Sin(now / 480.0) * 5);
                    RectangleF tail = new RectangleF(-0.04f * D, 0, 0.08f * D, 0.17f * D);
                    using (GraphicsPath tp = Theme.Round(tail, 0.025f * D))
                    {
                        FillSolid(g, tp, cream);
                        Line(g, red, D * 0.03f, 0, 0.035f * D, 0, 0.065f * D);
                        Line(g, red, D * 0.03f, 0, 0.1f * D, 0, 0.13f * D);
                        Stroke(g, tp, Color.FromArgb(50, 0, 0, 0), D * 0.008f);
                    }
                    g.Restore(st);
                    break;
                }
                case 5:
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float t = (i - 3) / 3f, x = t * nw * 0.4f, y = ny + 0.05f * D + (1 - t * t) * 0.05f * D, r = i == 3 ? 0.032f * D : 0.022f * D;
                        if (i > 0)
                        {
                            float pt = (i - 1 - 3) / 3f;
                            Line(g, Color.FromArgb(150, 40, 150, 90), D * 0.008f, pt * nw * 0.4f, ny + 0.05f * D + (1 - pt * pt) * 0.05f * D, x, y);
                        }
                        Ellipse(g, Petals[i % Petals.Length], x - r, y - r, r * 2, r * 2);
                        Ellipse(g, Color.FromArgb(210, 255, 214, 10), x - r * 0.38f, y - r * 0.38f, r * 0.76f, r * 0.76f);
                    }
                    break;
                }
                case 6:
                {
                    float top = geo.Top * D - 0.012f * D, ph = (fy - 0.03f * D - top) * 2;
                    using (Pen p = new Pen(Color.FromArgb(235, 70, 74, 92), D * 0.03f))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawArc(p, -hw * 0.97f, top, hw * 1.94f, ph, 180, 180);
                    }
                    foreach (int s in new int[] { -1, 1 })
                    {
                        float x = s * hw * 0.97f, y = fy - 0.03f * D, r = 0.08f * D;
                        Ellipse(g, Color.FromArgb(235, 255, 150, 180), x - r, y - r, r * 2, r * 2);
                        Ellipse(g, Color.FromArgb(245, 255, 214, 226), x - r * 0.72f, y - r * 0.72f, r * 1.44f, r * 1.44f);
                    }
                    break;
                }
                case 7:
                {
                    float cx = hw * 0.55f, cy = geo.Top * D + 0.045f * D, flap = 0.45f + 0.55f * (float)Math.Abs(Math.Sin(now / 170.0));
                    GraphicsState st = g.Save();
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(14);
                    foreach (int s in new int[] { -1, 1 })
                    {
                        GraphicsState ws = g.Save();
                        g.ScaleTransform(flap * s, 1);
                        Ellipse(g, Color.FromArgb(240, 255, 120, 170), 0.004f * D, -0.07f * D, 0.075f * D, 0.07f * D);
                        Ellipse(g, Color.FromArgb(240, 120, 190, 255), 0.004f * D, 0.0f, 0.06f * D, 0.055f * D);
                        g.Restore(ws);
                    }
                    Line(g, Color.FromArgb(240, 60, 40, 80), D * 0.012f, 0, -0.045f * D, 0, 0.045f * D);
                    g.Restore(st);
                    break;
                }
                case 8:
                {
                    Line(g, Color.FromArgb(235, 70, 110, 220), D * 0.02f, -0.045f * D, ny - 0.02f * D, 0, ny + 0.085f * D);
                    Line(g, Color.FromArgb(235, 226, 56, 70), D * 0.02f, 0.045f * D, ny - 0.02f * D, 0, ny + 0.085f * D);
                    float r = 0.05f * D, cy = ny + 0.105f * D;
                    Ellipse(g, Gold2, -r, cy - r, r * 2, r * 2);
                    Ellipse(g, Gold1, -r * 0.8f, cy - r * 0.84f, r * 1.6f, r * 1.6f);
                    using (GraphicsPath sp = Star(0, cy, r * 0.55f, 0.45f)) FillSolid(g, sp, Gold2);
                    break;
                }
                case 9:
                    foreach (int s in new int[] { -1, 1 })
                        using (GraphicsPath sp = Star(s * (gap + 0.1f * D), fy + 0.075f * D, 0.03f * D, 0.45f)) FillSolid(g, sp, Color.FromArgb(235, 255, 206, 20));
                    break;
                default:
                    foreach (int s in new int[] { -1, 1 })
                        for (int i = 0; i < 3; i++)
                        {
                            float x = s * (gap + 0.07f * D + (i == 1 ? 0.03f * D : 0)) + (i == 2 ? -s * 0.015f * D : 0), y = fy + 0.055f * D + i * 0.022f * D, r = 0.0075f * D;
                            Ellipse(g, Color.FromArgb(170, 150, 84, 60), x - r, y - r, r * 2, r * 2);
                        }
                    break;
            }
        }
    }
}
