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
            l.Kind = Clamp(s.MascotKind, MascotParts.Kinds.Length);
            l.Color = Clamp(s.MascotColor, MascotParts.ColorNames.Length);
            l.Eyes = Clamp(s.MascotEyes, MascotParts.EyeNames.Length);
            l.Hat = Clamp(s.MascotHat, MascotParts.HatNames.Length);
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

        // The hat actually worn: a seasonal costume replaces "no hat" around Halloween and Christmas.
        public int EffectiveHat(DateTime d)
        {
            if (Hat != 0 || !Seasonal) return Hat;
            if (d.Month == 10 || (d.Month == 11 && d.Day <= 2)) return MascotParts.HatPumpkin;
            if ((d.Month == 12 && d.Day >= 10) || (d.Month == 1 && d.Day <= 6)) return MascotParts.HatSanta;
            return 0;
        }

        static int Clamp(int v, int n) { return Math.Max(0, Math.Min(n - 1, v)); }
    }

    public static partial class MascotParts
    {
        public static readonly string[] Kinds = { "Robot", "Gato", "Conejo", "Fantasma", "Slime", "Drag\u00F3n", "Perro", "Chibi", "Ping\u00FCino", "Panda", "Zorro", "Rana", "Reno" };

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
            { Color.FromArgb(196, 128, 82), Color.FromArgb(120, 68, 38) }
        };
        public static readonly string[] ColorNames = { "Aurora", "Menta", "Coral", "Lavanda", "Oc\u00E9ano", "Sol", "Lima", "Grafito",
                                                       "Chicle", "Calabaza", "Noche", "Nieve", "Galaxia", "Cereza", "Hielo", "Bosque", "Oro", "Melocot\u00F3n", "Canela", "Turquesa", "Ceniza", "Arcilla", "Medianoche", "Chocolate" };

        public static readonly string[] EyeNames = { "Brillo", "Kawaii", "Puntitos", "Anime", "Chulo", "Estrella", "Pesta\u00F1as" };

        public const int HatPumpkin = 4, HatSanta = 11, HatCrown = 13, HatHalo = 14;
        public static readonly string[] HatNames = { "Ninguno", "Gorra", "Gorro de lana", "Sombrero de bruja", "Calabaza", "Cuernos",
                                                     "Chistera", "Fiesta", "Vaquero", "Auriculares", "Lazo", "Pap\u00E1 Noel", "Brote",
                                                     "Corona", "Aureola", "Boina", "Gorro de chef", "Flores", "Pelo rubio", "Pelo casta\u00F1o", "Pelo de punta", "Pelo blanco", "Pelo granate", "Capucha de oso", "Banda ninja", "Sombrero de paja", "Pelo negro", "Ninja rubio", "Casco de marine", "Casco de caballero", "Casco de astronauta", "Chispa",
                                                     "Pelo de punta oscuro", "Pelo magenta", "Ninja de pelo oscuro", "Pelo verde", "Pelo de llamas", "Melena negra",
                                                     "Ninja de pelo plateado", "Capucha de murci\u00E9lago", "Casco rel\u00E1mpago", "Pelo con rizo", "Casco de armadura",
                                                     "Sombrero de doctor" };
        public static readonly string[] OutfitNames = { "Nada", "Bufanda", "Pajarita", "Corbata", "Capa de vampiro", "Capa de h\u00E9roe",
                                                        "Cascabel", "Cadena de oro", "Capa verde", "Abrigo rojo", "Chaleco ninja", "Kimono de lucha", "Haori a cuadros", "Mochila", "Chaleco rojo", "Camiseta roja", "Botones de consola", "Panza del bosque", "Ch\u00E1ndal naranja",
                                                        "Hombrera", "Armadura verde", "Traje espacial", "Chaqueta verde", "Camiseta morada", "Traje de buf\u00F3n",
                                                        "Cuello alto azul", "Faja verde", "Armadura blanca", "Traje amarillo", "Kimono rosa", "Chaleco verde",
                                                        "Traje ar\u00E1cnido", "Traje de murci\u00E9lago", "Traje rel\u00E1mpago", "Traje de acero", "Armadura roja",
                                                        "Pantal\u00F3n corto" };
        public static readonly string[] FaceNames = { "Nada", "Gafas de sol", "Gafas pixel", "Gafas redondas", "Mon\u00F3culo", "Antifaz",
                                                      "Parche pirata", "Bigote", "Venda", "Cejas gordas", "Mofletes rojos", "Marcas de bigote", "Tatuaje y barba", "Barba de mago", "Terminal",
                                                      "Estrella y l\u00E1grima", "Bozal de bamb\u00FA", "M\u00E1scara ninja", "M\u00E1scara ar\u00E1cnida", "Nariz azul" };
        public static readonly string[] Personalities = { "Alegre", "Tranquilo", "Gamberro" };

        // ---- Friendship: one point per capture. A few items unlock along the way, like companions that grow with you.
        static readonly int[] LevelAt = { 0, 10, 30, 75, 150, 300 };
        public static readonly string[] LevelNames = { "Reci\u00E9n llegados", "Colegas", "Amigos", "Mejores amigos", "Inseparables", "Leyenda" };

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
        public static readonly string[] StyleNames = { "Cl\u00E1sico", "Halloween", "Chulo", "Cute", "Vampiro", "Dragoncito", "Pirata", "Chef", "Vaquero", "Doctor", "Brujita", "M\u00FAsico", "Invierno", "Enamorado", "Fiestero", "Aventurero", "Primavera", "Campe\u00F3n" };
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
            l.Kind = rnd.Next(Kinds.Length);
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
