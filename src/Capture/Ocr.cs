// Stackshot - Text in an image, read with the OCR that comes with Windows 10 and 11 (Windows.Media.Ocr): nothing to
// download and nothing leaves the computer.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Stackshot
{
    public static class Ocr
    {
        public static string Unavailable { get { return I18n.T("Este Windows no tiene el reconocimiento de texto (OCR) instalado.", "This Windows does not have text recognition (OCR) installed."); } }
        public const int MaxSide = 10000;     // OcrEngine.MaxImageDimension on Windows 10 and 11
        const int Margin = 20;                // around each piece, in the prepared image
        // Tall images are read in pieces this high (source pixels), overlapping: Windows' engine misses lines on big
        // pieces, and a line on the edge between two pieces is read whole from one of them.
        public const int Band = 1200, Overlap = 160;

        // Reads a copy of the image on a worker thread, then calls done(text, error) on ui's thread: text is "" when
        // there is none, null (with error) when it could not be read.
        public static void ReadAsync(Bitmap image, Control ui, Action<string, string> done)
        {
            ReadAsync(image, false, ui, done);
        }

        // owned: the image is the caller's to give away (decoded or rendered for this): it is read as it is, with no copy
        // on the calling thread (a 9000x6000 copy takes seconds), and disposed afterwards.
        public static void ReadAsync(Bitmap image, bool owned, Control ui, Action<string, string> done)
        {
            Bitmap copy;
            if (owned) copy = image;
            else
            {
                try { copy = new Bitmap(image); }
                catch (Exception ex)
                {
                    ShotStack.Log("OCR: " + ex.Message);
                    done(null, ex is OutOfMemoryException ? I18n.T("No hay memoria suficiente para leer una imagen tan grande.", "Not enough memory to read such a large image.")
                                                          : I18n.T("No se ha podido leer la imagen.", "Could not read the image."));
                    return;
                }
            }
            // Without a window handle there is nobody to post to: the answer comes on the worker thread instead of never.
            bool post = ui != null && ui.IsHandleCreated;
            Thread t = new Thread(delegate()
            {
                string error, text;
                using (copy) text = Read(copy, out error);
                if (!post) { done(text, error); return; }
                try { ui.BeginInvoke((Action)delegate { done(text, error); }); }
                catch (Exception ex) { ShotStack.Log("OCR: " + ex.Message); }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        // The text of an image, "" when there is none, null with error when it could not be read. Blocks: call it off
        // the UI thread.
        public static string Read(Bitmap image, out string error)
        {
            error = null;
            try { return Recognize(image, out error); }
            catch (Exception ex)
            {
                ShotStack.Log("OCR: " + ex);
                error = Failure(ex);
                return null;
            }
        }

        // TypeLoadException and friends: a Windows without the API.
        static string Failure(Exception ex)
        {
            return ex is TypeLoadException || ex is FileNotFoundException || ex is System.Runtime.InteropServices.COMException && ((System.Runtime.InteropServices.COMException)ex).ErrorCode == unchecked((int)0x80040154)
                ? Unavailable : I18n.T("No se ha podido leer el texto: ", "Could not read the text: ") + ex.Message;
        }

        // A word read from an image: Box is where it is in the image given (pixels), Upright the same box where the text
        // reads left to right (they differ only for sideways text), Line the line it belongs to (words of a line share it
        // and come in reading order).
        public sealed class Word
        {
            public string Text;
            public RectangleF Box, Upright;
            public int Line;
        }

        // The words of an image and where each one is, read exactly as Read reads the text (same scale, pieces and
        // turns). Empty when there is no text, null with error when it could not be read. Blocks: call it off the UI thread.
        public static List<Word> ReadWords(Bitmap image, out string error)
        {
            error = null;
            try { return RecognizeWords(image, out error); }
            catch (Exception ex)
            {
                ShotStack.Log("OCR: " + ex);
                error = Failure(ex);
                return null;
            }
        }

        // Screen text is small (12 to 16 px at 100 %): Windows reads it much better at twice the size, so every image is
        // doubled while that fits the engine's limit (up to 4980 px wide: a 4K screen too), shrunk only past 10000.
        public static double Scale(int width)
        {
            return Math.Min(2, (MaxSide - 2.0 * Margin) / Math.Max(1, width));
        }

        // The pieces a tall image is read in (source rows): Band high, each overlapping the next by Overlap. A scrolling
        // capture of 15000 px is read at full size this way instead of being shrunk until its text is unreadable.
        public static List<Rectangle> Pieces(System.Drawing.Size image)
        {
            List<Rectangle> r = new List<Rectangle>();
            int w = Math.Max(1, image.Width), h = Math.Max(1, image.Height);
            if (h <= Band) { r.Add(new Rectangle(0, 0, w, h)); return r; }
            for (int y = 0; ; y += Band - Overlap)
            {
                int ph = Math.Min(Band, h - y);
                r.Add(new Rectangle(0, y, w, ph));
                if (y + ph >= h) break;
            }
            return r;
        }

        // One piece ready for the engine: scaled by k, with a margin so text that touches the edges stays readable, and
        // opaque on the colour of the image's corner, so transparent parts (a window's shadow) don't read as black.
        public static Bitmap Prepare(Bitmap src, Rectangle part, double k)
        {
            int ow = Math.Max(1, (int)Math.Round(part.Width * k)), oh = Math.Max(1, (int)Math.Round(part.Height * k));
            Color back = Opaque(src.GetPixel(0, 0));
            Bitmap o = new Bitmap(ow + 2 * Margin, oh + 2 * Margin, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics g = Graphics.FromImage(o))
                {
                    g.Clear(back);
                    g.InterpolationMode = k > 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingMode = CompositingMode.SourceOver;
                    using (ImageAttributes a = new ImageAttributes())
                    {
                        a.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(src, new Rectangle(Margin, Margin, ow, oh), part.X, part.Y, part.Width, part.Height, GraphicsUnit.Pixel, a);
                    }
                }
                return o;
            }
            catch { o.Dispose(); throw; }
        }

        // The whole image as one piece (for tests and small images).
        public static Bitmap Prepare(Bitmap src)
        {
            return Prepare(src, new Rectangle(0, 0, src.Width, src.Height), Scale(src.Width));
        }

        static Color Opaque(Color c)
        {
            if (c.A == 255) return c;
            // Over white, as most captures with transparency sit on a light page.
            int a = c.A;
            return Color.FromArgb((c.R * a + 255 * (255 - a)) / 255, (c.G * a + 255 * (255 - a)) / 255, (c.B * a + 255 * (255 - a)) / 255);
        }

        // The recognizer: Spanish when Windows has it (it reads English too, and knows ñ and accents), otherwise the
        // user's languages, otherwise any.
        static OcrEngine Engine()
        {
            foreach (Windows.Globalization.Language l in OcrEngine.AvailableRecognizerLanguages)
                if (l.LanguageTag.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return OcrEngine.TryCreateFromLanguage(l);
            OcrEngine e = OcrEngine.TryCreateFromUserProfileLanguages();
            if (e != null) return e;
            foreach (Windows.Globalization.Language l in OcrEngine.AvailableRecognizerLanguages) return OcrEngine.TryCreateFromLanguage(l);
            return null;
        }

        // Kept apart so a Windows without the API fails here, inside Read's try, when this method is compiled. Each line
        // belongs to the piece whose own part (its rows minus half the overlap at a shared edge) holds its centre, so the
        // overlap never gives a line twice and a line cut by a piece's edge is read whole from the other.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Recognize(Bitmap image, out string error)
        {
            error = null;
            OcrEngine engine = Engine();
            if (engine == null) { error = Unavailable; return null; }
            return Best(engine, image).Text;
        }

        // The words of the reading Recognize keeps, their boxes turned back to the image as given.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static List<Word> RecognizeWords(Bitmap image, out string error)
        {
            error = null;
            OcrEngine engine = Engine();
            if (engine == null) { error = Unavailable; return null; }
            Reading r = Best(engine, image);
            List<Word> o = new List<Word>();
            int line = 0;
            foreach (List<Word> words in r.Lines)
            {
                if (words.Count == 0) continue;
                foreach (Word w in words)
                {
                    w.Line = line;
                    w.Upright = w.Box;
                    w.Box = Unturn(w.Box, r.Turn, image.Size);
                    o.Add(w);
                }
                line++;
            }
            return o;
        }

        // A box found in the image turned a quarter (RotateFlip) back in the image as it was.
        internal static RectangleF Unturn(RectangleF b, RotateFlipType turn, System.Drawing.Size image)
        {
            if (turn == RotateFlipType.Rotate90FlipNone) return new RectangleF(b.Y, image.Height - b.Right, b.Height, b.Width);
            if (turn == RotateFlipType.Rotate270FlipNone) return new RectangleF(image.Width - b.Bottom, b.X, b.Height, b.Width);
            return b;
        }

        // The reading Recognize keeps for an image.
        static Reading Best(OcrEngine engine, Bitmap image)
        {
            double k = Scale(image.Width);
            Reading text = Pass(engine, image, k);
            // Doubling helps small screen text but can lose big text (a lone 20 pt word read as "" or "H Ola"). Nothing at
            // all: read again at its own size, then halfway.
            foreach (double other in new double[] { 1, 1.5 })
            {
                if (k <= other || text.Text.Trim().Length > 0) break;
                text = Pass(engine, image, other);
            }
            // Nothing, or nothing that reads like words: maybe the capture is sideways (a portrait screen, a rotated
            // scan). A quarter turn each way is tried, and the reading that looks most like text wins.
            // A turn wins when it reads like text and gives clearly more of it (a lone "o" from sideways lines is not text).
            int chars;
            double best = Plausible(text.Text, out chars);
            bool turned = false;
            if ((best < 0.5 || chars < 4) && image.Width > 8 && image.Height > 8)
            {
                foreach (RotateFlipType turn in new RotateFlipType[] { RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone })
                {
                    Reading other;
                    using (Bitmap r = new Bitmap(image))
                    {
                        r.RotateFlip(turn);
                        other = Pass(engine, r, Scale(r.Width));
                    }
                    other.Turn = turn;
                    int c;
                    double score = Plausible(other.Text, out c);
                    if (score >= 0.5 && (score > best + 0.2 || c > chars * 2 + 3)) { best = score; chars = c; text = other; turned = true; }
                }
            }
            // Upright text that is already big: at its own size too, kept when it reads better (letters joined again).
            if (!turned && k > 1 && text.LineHeight >= 22 && text.Text.Trim().Length > 0)
            {
                Reading again = Pass(engine, image, 1);
                if (Better(again.Text, text.Text)) text = again;
            }
            return text;
        }

        // What one reading gave: the text, the usual height of a line, and the words of each line kept (in the pixels of
        // the image read, turned when Turn says so).
        sealed class Reading
        {
            public string Text;
            public float LineHeight;
            public List<List<Word>> Lines;
            public RotateFlipType Turn = RotateFlipType.RotateNoneFlipNone;
        }

        // A line as the engine gives it for one piece: text, box and words (piece pixels).
        sealed class PieceLine
        {
            public string Text;
            public RectangleF Box;
            public List<KeyValuePair<string, RectangleF>> Words = new List<KeyValuePair<string, RectangleF>>();
        }

        // Whether a second reading beats the first: the first read nothing, or the second looks more like text, or as
        // much with fewer words (letters the first split apart: "H Ola" for "Hola").
        static bool Better(string second, string first)
        {
            if (second.Trim().Length == 0) return false;
            if (first.Trim().Length == 0) return true;
            double a = Plausible(second), b = Plausible(first);
            if (a > b + 0.01) return true;
            return a >= b - 0.01 && Words(second) < Words(first);
        }

        static int Words(string text)
        {
            return text.Split(new char[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        // Share of the words that look like words: letters only (with a vowel) or a number. Sideways text read as if
        // upright gives "eau!l epun6as": far below normal text, which is near 1.
        internal static double Plausible(string text)
        {
            int letters;
            return Plausible(text, out letters);
        }

        // Also how many characters the good words have: a lone "o" looks like a word but says little.
        internal static double Plausible(string text, out int goodChars)
        {
            int words = 0, good = 0;
            goodChars = 0;
            foreach (string w in (text ?? "").Split(new char[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = w.Trim('.', ',', ';', ':', '(', ')', '"', '\u00AB', '\u00BB', '\u00BF', '?', '\u00A1', '!');
                if (s.Length == 0) continue;
                words++;
                bool letters = true, digits = true, vowel = false;
                foreach (char c in s)
                {
                    if (!char.IsLetter(c) && c != '-' && c != '\'') letters = false;
                    if (!char.IsDigit(c) && c != '.' && c != ',' && c != '/' && c != ':' && c != '-') digits = false;
                    if ("aeiouAEIOU\u00E1\u00E9\u00ED\u00F3\u00FA\u00C1\u00C9\u00CD\u00D3\u00DA\u00FC\u00DCyY".IndexOf(c) >= 0) vowel = true;
                }
                if ((letters && (vowel || s.Length == 1)) || digits) { good++; goodChars += s.Length; }
            }
            return words == 0 ? 0 : good / (double)words;
        }

        // One reading of the image, piece by piece. Each piece keeps the lines whose centre falls in its own part, give or
        // take Cut pixels around each cut between pieces: the engine places a line a few pixels differently in each piece
        // (it estimates a slant for the whole piece), so near a cut both may keep it, and a line the piece before already
        // gave is not taken twice. Within a piece, a line read twice (once missing its first letters) is kept once, whole.
        static Reading Pass(OcrEngine engine, Bitmap image, double k)
        {
            List<string> texts = new List<string>();
            List<RectangleF> boxes = new List<RectangleF>();
            List<List<Word>> words = new List<List<Word>>();
            List<Rectangle> parts = Pieces(image.Size);
            int prevStart = 0;
            for (int p = 0; p < parts.Count; p++)
            {
                Rectangle part = parts[p];
                float own0 = p == 0 ? float.MinValue : part.Y + Overlap / 2f - Cut;
                float own1 = p == parts.Count - 1 ? float.MaxValue : part.Bottom - Overlap / 2f + Cut;
                int start = texts.Count;
                using (Bitmap piece = Prepare(image, part, k))
                    foreach (PieceLine line in ReadPiece(engine, piece))
                    {
                        RectangleF src = Source(line.Box, part, k);
                        float mid = src.Y + src.Height / 2;
                        if (mid >= own1 || (p > 0 && src.Y < part.Y + Edge)) continue;
                        // Over the cut, in the piece before's part, a line only mends one that piece read badly.
                        bool mend = mid < own0;
                        // Already read: the fuller reading stays (one piece may have missed the first letters).
                        int same = p > 0 && mid < own0 + 2 * Cut ? Taken(texts, boxes, prevStart, start, line.Text, src, 2.5f) : -1;
                        if (same < 0 && !mend) same = Taken(texts, boxes, start, texts.Count, line.Text, src, 0.5f);
                        if (same < 0 && mend) continue;
                        if (same >= 0)
                        {
                            if (Letters(line.Text).Length > Letters(texts[same]).Length) { texts[same] = line.Text; boxes[same] = src; words[same] = SourceWords(line, part, k); }
                            continue;
                        }
                        texts.Add(line.Text);
                        boxes.Add(src);
                        words.Add(SourceWords(line, part, k));
                    }
                prevStart = start;
            }
            // The usual height of a line (median, source pixels): how big the text is.
            List<float> heights = new List<float>();
            foreach (RectangleF b in boxes) heights.Add(b.Height);
            heights.Sort();
            Reading r = new Reading();
            r.LineHeight = heights.Count > 0 ? heights[heights.Count / 2] : 0;
            r.Text = Join(texts, boxes);
            r.Lines = words;
            return r;
        }

        // A box of a prepared piece in the pixels of the image read.
        static RectangleF Source(RectangleF b, Rectangle part, double k)
        {
            return new RectangleF((float)((b.X - Margin) / k), (float)((b.Y - Margin) / k + part.Y), (float)(b.Width / k), (float)(b.Height / k));
        }

        static List<Word> SourceWords(PieceLine line, Rectangle part, double k)
        {
            List<Word> o = new List<Word>();
            foreach (KeyValuePair<string, RectangleF> w in line.Words)
            {
                Word word = new Word();
                word.Text = w.Key;
                word.Box = Source(w.Value, part, k);
                o.Add(word);
            }
            return o;
        }

        const float Cut = 30;   // source pixels each piece reads past the middle of its overlap with the next
        const float Edge = 3;   // source pixels: a line this close to a piece's top may be cut there

        // The line among [from, end) that is this one: centred within `lines` line heights up or down, starting within
        // four across, and reading the same give or take a letter, or as its end (the engine missed the first letters).
        // -1 when there is none.
        static int Taken(List<string> texts, List<RectangleF> boxes, int from, int end, string text, RectangleF box, float lines)
        {
            string t = Letters(text);
            for (int i = from; i < end; i++)
            {
                RectangleF o = boxes[i];
                float h = Math.Max(o.Height, box.Height);
                if (Math.Abs((o.Y + o.Height / 2) - (box.Y + box.Height / 2)) > h * lines || Math.Abs(o.X - box.X) > h * 4) continue;
                string u = Letters(texts[i]);
                if (Similar(u, t) || (u.Length >= 4 && t.Length >= 4 && (u.EndsWith(t, StringComparison.Ordinal) || t.EndsWith(u, StringComparison.Ordinal)))) return i;
            }
            return -1;
        }

        static string Letters(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static string Digits(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s) if (char.IsDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        // Equal, or at most one letter in five different (a letter read another way, or one more or less). Numbers must
        // match exactly: "Total 10" and "Total 11" are two lines.
        static bool Similar(string a, string b)
        {
            if (a == b) return true;
            if (Digits(a) != Digits(b)) return false;
            int n = Math.Max(a.Length, b.Length);
            if (n == 0 || Math.Abs(a.Length - b.Length) > n / 5) return false;
            int[] prev = new int[b.Length + 1], cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                int[] sw = prev; prev = cur; cur = sw;
            }
            return prev[b.Length] <= n / 5;
        }

        static List<KeyValuePair<string, RectangleF>> Lines(OcrEngine engine, Bitmap piece)
        {
            List<KeyValuePair<string, RectangleF>> o = new List<KeyValuePair<string, RectangleF>>();
            foreach (PieceLine line in ReadPiece(engine, piece)) o.Add(new KeyValuePair<string, RectangleF>(line.Text, line.Box));
            return o;
        }

        static List<PieceLine> ReadPiece(OcrEngine engine, Bitmap piece)
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                piece.Save(ms, ImageFormat.Bmp);
                bytes = ms.ToArray();
            }
            List<PieceLine> o = new List<PieceLine>();
            using (InMemoryRandomAccessStream s = new InMemoryRandomAccessStream())
            {
                using (DataWriter w = new DataWriter(s))
                {
                    w.WriteBytes(bytes);
                    Wait(w.StoreAsync());
                    w.DetachStream();
                }
                s.Seek(0);
                BitmapDecoder dec = Wait(BitmapDecoder.CreateAsync(s));
                using (SoftwareBitmap bmp = Wait(dec.GetSoftwareBitmapAsync()))
                {
                    OcrResult r = Wait(engine.RecognizeAsync(bmp));
                    foreach (OcrLine line in r.Lines)
                    {
                        PieceLine pl = new PieceLine();
                        RectangleF box = RectangleF.Empty;
                        foreach (OcrWord word in line.Words)
                        {
                            Rect b = word.BoundingRect;
                            RectangleF wr = new RectangleF((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
                            box = box.IsEmpty ? wr : RectangleF.Union(box, wr);
                            pl.Words.Add(new KeyValuePair<string, RectangleF>(word.Text, wr));
                        }
                        pl.Text = line.Text;
                        pl.Box = box;
                        o.Add(pl);
                    }
                }
            }
            return o;
        }

        // Lines in reading order; a blank line where the gap above a line is clearly bigger than a line, so separate
        // paragraphs or blocks stay apart when pasted.
        public static string Join(IList<string> texts, IList<RectangleF> boxes)
        {
            StringBuilder sb = new StringBuilder();
            int prev = -1;
            for (int i = 0; i < texts.Count; i++)
            {
                string t = (texts[i] ?? "").Trim();
                if (t.Length == 0) continue;
                if (prev >= 0)
                {
                    sb.Append('\n');
                    RectangleF p = boxes[prev], c = boxes[i];
                    float lh = Math.Max(1, Math.Min(p.Height, c.Height));
                    if (c.Top - p.Bottom > lh * 1.2f) sb.Append('\n');
                }
                sb.Append(t);
                prev = i;
            }
            return sb.ToString();
        }

        static T Wait<T>(IAsyncOperation<T> op)
        {
            while (op.Status == AsyncStatus.Started) Thread.Sleep(2);
            if (op.Status != AsyncStatus.Completed) throw new InvalidOperationException("OCR: " + op.Status + (op.ErrorCode != null ? " " + op.ErrorCode.Message : ""));
            return op.GetResults();
        }

        // Few words about what was read, for a notice: "3 líneas: «Hola…»".
        public static string Describe(string text)
        {
            string[] lines = text.Split('\n');
            int n = 0;
            foreach (string l in lines) if (l.Trim().Length > 0) n++;
            string flat = string.Join(" ", text.Replace("\n", " ").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (flat.Length > 90)
            {
                // Never half an emoji (a surrogate pair) before the ellipsis.
                int cut = char.IsHighSurrogate(flat[87]) ? 87 : 88;
                flat = flat.Substring(0, cut).TrimEnd() + "\u2026";
            }
            string count = I18n.IsSpanish ? (n == 1 ? "1 l\u00EDnea" : n + " l\u00EDneas") : (n == 1 ? "1 line" : n + " lines");
            return I18n.IsSpanish ? count + ": \u00AB" + flat + "\u00BB" : count + ": \u201C" + flat + "\u201D";
        }
    }
}
