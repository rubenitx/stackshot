// Stackshot - A GIF of the piece marked in the player (E and S): the GIF frame rate of the Grabacion page, the editor's
// palette, at most 960 px wide, turned and with square pixels like the picture on screen, without sound.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Stackshot
{
    // The FFmpeg side, as pure functions. Two runs over the piece: the first writes the palette, the second the GIF with it.
    // The result is the editor's single run (split, palettegen, paletteuse), but frames are never held in memory until
    // the palette is ready (a 30 s piece would need gigabytes), and both runs report how far they are.
    internal static class PieceGif
    {
        public const double LongSeconds = 30;   // longer pieces ask first: a GIF that long weighs a lot
        public const int MaxWidth = 960;        // as the editor's GIF export
        // The editor's palette (VideoExport.Arguments): one per GIF counting every pixel, light dithering, only the part
        // that changes redrawn.
        internal const string PaletteGen = "palettegen=stats_mode=full";
        internal const string PaletteUse = "paletteuse=dither=sierra2_4a:diff_mode=rectangle";

        // The GIF frame rate of the Grabacion page (settings.GifFps, 5 to 30), never more than the video has.
        public static double Fps(int gifFps, double sourceFps)
        {
            double f = Math.Max(5, Math.Min(30, gifFps));
            return sourceFps > 0 && sourceFps < f ? sourceFps : f;
        }

        // The size the picture is seen at (turned, non-square pixels made square) scaled to at most 960 wide, both even.
        public static Size OutputSize(Size display)
        {
            if (display.Width <= 0 || display.Height <= 0) return new Size(2, 2);
            int w = Math.Max(2, Math.Min(display.Width, MaxWidth) & ~1);
            int h = Math.Max(2, (int)Math.Round(display.Height * (double)w / display.Width / 2, MidpointRounding.AwayFromZero) * 2);
            return new Size(w, h);
        }

        // The piece from a to b of src, read as the MP4 export reads one (an MPEG-TS seeks earlier and drops the extra).
        static string Input(string src, double a, double b)
        {
            double e = PlayerArgs.Early(src, a);
            return "-hide_banner -loglevel error -nostats -progress pipe:1 -y " + PlayerArgs.Seek(a - e) + "-to " + VideoExport.Seconds(b) + " " +
                   Recorder.SafeInput + "-i " + Recorder.Quote(src);
        }

        // Its frames at the GIF's rate (fps 0: a GIF keeps its own timing) and size. FFmpeg turns a rotated video itself.
        static string Frames(string src, double a, double b, Size size, double fps)
        {
            double e = PlayerArgs.Early(src, a);
            string f = "[0:v]";
            if (e > 0) f += "trim=start=" + VideoExport.Seconds(e) + ":end=" + VideoExport.Seconds(e + (b - a)) + ",setpts=PTS-STARTPTS,";
            if (fps > 0) f += "fps=" + VideoExport.Rate(fps) + ",";
            return f + "scale=" + size.Width + ":" + size.Height + ":flags=lanczos,setsar=1";
        }

        // The first run. FFmpeg reports nothing until every output has started, and the palette only comes at the end, so
        // its PNG gets a transparent stand-in frame at once and the palette rewrites it at the end (-update 1); a null
        // output takes the frames, which is what the report counts.
        public static string PaletteArgs(string src, double a, double b, Size size, double fps, string palette)
        {
            return Input(src, a, b) + " -filter_complex \"" + Frames(src, a, b, size, fps) + ",split[s][n];[s]" + PaletteGen + "[p];" +
                   "color=c=black@0:s=16x16,format=bgra,setsar=1,trim=end_frame=1[f];[f][p]concat=n=2:v=1:a=0[q]\" -map \"[q]\" -update 1 " +
                   Recorder.Quote(palette) + " -map \"[n]\" -f null NUL";
        }

        // The same palette without the stand-in (and without progress), if that way fails with some FFmpeg.
        public static string PlainPaletteArgs(string src, double a, double b, Size size, double fps, string palette)
        {
            return Input(src, a, b) + " -filter_complex \"" + Frames(src, a, b, size, fps) + "," + PaletteGen + "[p]\" -map \"[p]\" -frames:v 1 -update 1 " +
                   Recorder.Quote(palette);
        }

        // The second run. The palette is read as one picture, its name taken literally (a % in it is no pattern).
        public static string GifArgs(string src, double a, double b, Size size, double fps, string palette, string output)
        {
            return Input(src, a, b) + " -f image2 -pattern_type none -i " + Recorder.Quote(palette) + " -filter_complex \"" + Frames(src, a, b, size, fps) +
                   "[x];[x][1:v]" + PaletteUse + "[o]\" -map \"[o]\" -loop 0 " + Recorder.Quote(output);
        }

        // The flash once the GIF is on the stack: kept next to a saved video when it went into the save folder or one of its
        // dated subfolders (as the MP4 export says it).
        internal static string ReadyText(Settings s, string output)
        {
            return SaveFolders.Inside(s, Path.GetDirectoryName(output)) ? I18n.T("GIF listo y guardado junto al original", "GIF ready and saved next to original") : I18n.T("GIF listo: est\u00E1 en la pila", "GIF ready: added to stack");
        }

        // A real palette: 256 colours, some of them opaque (the stand-in frame is all transparent).
        public static bool PaletteOk(string png)
        {
            try
            {
                if (!File.Exists(png)) return false;
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(png)))
                using (Bitmap b = new Bitmap(ms))
                {
                    if (b.Width * b.Height != 256) return false;
                    for (int y = 0; y < b.Height; y++)
                        for (int x = 0; x < b.Width; x++)
                            if (b.GetPixel(x, y).A == 255) return true;
                }
            }
            catch (Exception) { }
            return false;
        }
    }

    internal sealed partial class PlayerWindow
    {
        // Tests answer the question about a long piece here (null: the user is asked).
        internal static Func<string, bool> AskLongGif { get; set; }

        bool CanGif { get { return info != null && info.CanPlay && exportThread == null && HasRange; } }

        bool GifSource { get { return string.Equals(Path.GetExtension(path ?? ""), ".gif", StringComparison.OrdinalIgnoreCase); } }

        // 0 for a GIF, which keeps its own timing.
        double GifRate { get { return GifSource || info == null ? 0 : PieceGif.Fps(settings.GifFps, info.Fps); } }

        string GifTip()
        {
            double f = GifRate;
            return I18n.Fmt("Crear un GIF con el tramo marcado (E y S) \u00B7 {0}  (G)", "Create a GIF from marked clip (E & S) \u00B7 {0}  (G)",
                (f > 0 ? f.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', I18n.IsSpanish ? ',' : '.') + " fps" : I18n.T("al ritmo del GIF", "at GIF frame rate")));
        }

        // In Mas opciones, greyed out until there is a piece.
        ToolStripMenuItem GifMenuItem()
        {
            ToolStripMenuItem it = TrayMenu.Item(I18n.T("Exportar GIF del tramo", "Export GIF of clip"), "gif", "G", delegate { ExportGif(); });
            it.Enabled = HasRange;
            return it;
        }

        bool LongGifWanted(double seconds)
        {
            string text = I18n.Fmt("El tramo dura {0}: un GIF tan largo pesa mucho y tarda en crearse. Para un trozo as\u00ED suele ir mejor \u00ABExportar tramo\u00BB (MP4).\n\n\u00BFCrear el GIF igualmente?",
                                   "Clip is {0} long: such a long GIF has a large file size and takes time to create. For such a clip, \u201CExport clip\u201D (MP4) is usually better.\n\nCreate GIF anyway?",
                                   PlayerKit.Clock(Math.Round(seconds)));
            Func<string, bool> ask = AskLongGif;
            if (ask != null) return ask(text);
            if (Quiet) return true;
            return Dialogs.Show(this, text, "Stackshot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // G, the bar's GIF button or Mas opciones: a GIF of the marked piece, next to the MP4 export and like it (progress,
        // Esc cancels, checked before it is offered, to the stack or next to the video when the player runs on its own).
        void ExportGif()
        {
            if (info == null || !info.CanPlay || exportThread != null) return;
            if (!HasRange) { Flash(I18n.T("Marca el tramo con E y S para crear el GIF", "Mark clip with E and S to create GIF")); return; }
            double a = markIn, b = markOut, dur = b - a;
            if (dur > PieceGif.LongSeconds && !LongGifWanted(dur)) return;
            if (exportThread != null || info == null) return;
            string dir = owner != null ? owner.ExportFolder(path) : Path.GetDirectoryName(path);
            string output;
            try
            {
                Directory.CreateDirectory(dir);
                output = ShotStack.Reserve(Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + " (tramo).gif"));
            }
            catch (Exception ex)
            {
                ShotStack.Log("Reproductor: GIF " + ex.Message);
                Flash(I18n.T("No se pudo preparar el GIF.", "Could not prepare GIF."));
                return;
            }
            if (play != null && Playing) play.Pause();
            Size size = PieceGif.OutputSize(info.Display);
            double fps = GifRate;
            string ff = ffmpeg, src = path;
            string palette = Path.Combine(Path.GetTempPath(), "stackshot-palette-" + Guid.NewGuid().ToString("N") + ".png");
            ShareExpect expect = new ShareExpect();
            expect.Gif = true;
            expect.Width = size.Width;
            expect.Height = size.Height;
            ExportAbort ab = new ExportAbort();
            exportAbort = ab;
            exportOut = output;
            exportText = I18n.T("Creando el GIF\u2026 0 %  \u00B7  Esc cancela", "Creating the GIF\u2026 0%  \u00B7  Esc cancels");
            Invalidate();
            ShotStack.Log("Reproductor: creando " + Path.GetFileName(output) + " (" + size.Width + "x" + size.Height + ", " +
                          (fps > 0 ? fps.ToString("0.##", CultureInfo.InvariantCulture) + " fps" : "ritmo del GIF") + ", " + dur.ToString("0.###", CultureInfo.InvariantCulture) + " s)");
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                VideoExport.Abort = ab;
                string err = "";
                int code = -1;
                ShareReport rep = null;
                int timeout = (int)Math.Min(6 * 3600 * 1000.0, Math.Max(120000, dur * 20000));
                try
                {
                    code = VideoExport.Run(ff, PieceGif.PaletteArgs(src, a, b, size, fps, palette), timeout, GifProgress(ab, dur, 0), out err);
                    if (!ab.Set && (code != 0 || !PieceGif.PaletteOk(palette)))
                    {
                        ShotStack.Log("Reproductor: paleta del GIF otra vez, sin progreso (" + code + "): " + Tail(err));
                        code = VideoExport.Run(ff, PieceGif.PlainPaletteArgs(src, a, b, size, fps, palette), timeout, delegate(string line) { }, out err);
                        if (code == 0 && !PieceGif.PaletteOk(palette)) { code = -1; err = I18n.T("La paleta del GIF no es v\u00E1lida.", "The GIF palette is not valid."); }
                    }
                    if (!ab.Set && code == 0)
                        code = VideoExport.Run(ff, PieceGif.GifArgs(src, a, b, size, fps, palette, output), timeout, GifProgress(ab, dur, 1), out err);
                    if (!ab.Set && code == 0 && File.Exists(output))
                    {
                        OnUi(delegate { if (exportAbort == ab) { exportText = I18n.T("Comprobando el GIF\u2026  \u00B7  Esc cancela", "Checking the GIF\u2026  \u00B7  Esc cancels"); Invalidate(); } });
                        rep = ShareCheck.Validate(ff, output, expect);
                    }
                }
                catch (Exception ex) { code = -1; err = ex.Message; }
                finally { try { if (File.Exists(palette)) File.Delete(palette); } catch (Exception) { } }
                OnUi(delegate { GifDone(ab, output, code, err, rep); });
            });
            t.IsBackground = true;
            exportThread = t;
            t.Start();
        }

        // Each run is half of the progress.
        Action<string> GifProgress(ExportAbort ab, double dur, int run)
        {
            return delegate(string line)
            {
                if (dur <= 0 || !line.StartsWith("out_time=", StringComparison.Ordinal)) return;
                TimeSpan ts;
                if (!TimeSpan.TryParse(line.Substring(9).Trim(), CultureInfo.InvariantCulture, out ts)) return;
                int pc = (int)Math.Round((run + Math.Max(0, Math.Min(1, ts.TotalSeconds / dur))) / 2 * 100);
                OnUi(delegate { if (exportAbort == ab) { exportText = I18n.Fmt("Creando el GIF\u2026 {0} %  \u00B7  Esc cancela", "Creating the GIF\u2026 {0}%  \u00B7  Esc cancels", pc); Invalidate(); } });
            };
        }

        void GifDone(ExportAbort ab, string output, int code, string err, ShareReport rep)
        {
            ShotStack.Unreserve(output);
            if (exportAbort != ab) return;
            exportAbort = null;
            exportThread = null;
            exportOut = null;
            exportText = null;
            Invalidate();
            if (ab.Set) { try { if (File.Exists(output)) File.Delete(output); } catch (Exception) { } return; }
            if (code != 0 || !File.Exists(output))
            {
                ShotStack.Log("Reproductor: GIF, FFmpeg " + code + ": " + err);
                try { if (File.Exists(output)) File.Delete(output); } catch (Exception) { }
                Alert(I18n.T("No se pudo crear el GIF.\n\n", "Could not create GIF.\n\n") + Tail(err));
                return;
            }
            ShotStack.Log("Reproductor: comprobaci\u00F3n " + (rep != null && rep.Passed ? "correcta" : "FALLIDA") + " de " + Path.GetFileName(output) + "\n" + rep);
            if (rep != null && rep.Passed)
            {
                exportNote = "ok";
                if (owner != null)
                {
                    owner.AddRecording(output);
                    Flash(PieceGif.ReadyText(settings, output));
                }
                else Flash(I18n.T("GIF listo", "GIF ready"));
                return;
            }
            // Not a normal result: kept under another name so it is never taken for a good export.
            string kept = output;
            try
            {
                kept = ShotStack.Unique(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + " (sin validar)" + Path.GetExtension(output)));
                File.Move(output, kept);
            }
            catch (Exception ex) { kept = output; ShotStack.Log("Reproductor: no se pudo renombrar: " + ex.Message); }
            Alert((rep != null ? rep.Summary : I18n.T("No se pudo comprobar el GIF.", "Could not verify GIF.")) + I18n.Fmt("\n\nEl archivo no se ha a\u00F1adido a la pila. Queda en:\n{0}", "\n\nThe file was not added to stack. Stored in:\n{0}", kept));
        }
    }
}
