// Stackshot - The recording bar (time, sound meters, Stop, Discard, Pause) and the frame around the recorded area.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using WI = System.Windows.Media.Imaging;
using WM = System.Windows.Media;

namespace Stackshot
{
    // Floating HUD while recording: time, the sound being recorded (live meters; click a source to mute it), Stop and
    // Discard. On Stop it narrows into a pill with the saving progress, confirms with a check and fades out. A dark
    // capsule with a soft shadow, excluded from the capture; its empty parts drag it out of the way.
    class RecordBar : FloatWindow
    {
        enum Phase { Rec, Busy, Progress, Done, Gone }

        static string DiscardHint { get { return I18n.T("\u00BFDescartar?", "Discard?"); } }
        static string CancelText { get { return I18n.Cancel; } }
        static string CancelHint { get { return I18n.T("\u00BFCancelar?", "Cancel?"); } }
        static string PausedText { get { return I18n.T("En pausa", "Paused"); } }
        static string PauseTip { get { return I18n.T("Pausar", "Pause"); } }
        static string ResumeTip { get { return I18n.T("Reanudar", "Resume"); } }
        static readonly WM.Color Amber = Ds.Rgb(255, 179, 64);
        const double ArmMs = 3000;          // a first click on Discard asks for a second one within this time
        const double QuickDiscardMs = 3000; // shorter recordings are discarded at once
        static readonly WM.Color Red = Ds.Rgb(255, 59, 48), Accent = Ds.Rgb(10, 132, 255), Green = Ds.Rgb(48, 209, 88);

        readonly Session session;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        readonly bool hasSys, hasMic;
        readonly float[] meter = new float[2];
        readonly Tween cap = new Tween(0);     // capsule width; the window may be wider (transparent, click-through)
        readonly Tween fade = new Tween(1);    // opacity of the content shown
        static RecordBar previous;             // the last bar, which may still be saying "Guardado"
        // phase, label and progress say what the recording is doing; the face is what the capsule shows, which follows
        // them through a short fade (and keeps the recording controls while a discarded bar fades out).
        Phase phase = Phase.Rec, face = Phase.Rec;
        string label, faceLabel, shown;
        double progress = -1, faceProgress = -1, armedAt = -1, nudgeAt = -1;
        int hot = -1, pressed = -1, spin, faceW;
        bool dragging, longClock;
        Point dragFrom;
        double dragX, dragY;
        // Recording layout, in capsule coordinates.
        int recW, timeX, srcX, diskX, stopX, stopW, discardX, pauseX;
        bool paused, tipShown;
        // Low disk: "~N min de disco", amber (1) or red (2); null when there is room.
        string diskText;
        int diskLevel;
        WI.BitmapSource shadowL, shadowM, shadowR;
        WM.FormattedText[] clockGlyphs;          // 0-9 and the colon
        int clockFor;                            // the palette they were made with (HighContrast.Generation)
        double digitW;
        int shadowCore;

        public RecordBar(Session session, Rectangle area)
        {
            this.session = session;
            hasSys = session.SystemAudio != null;
            hasMic = session.Microphone != null;
            // A new recording started while the last bar still confirms its file: that one gets out of the way.
            if (previous != null && !previous.IsDisposed && previous.phase == Phase.Done) previous.StepAside();
            previous = this;
            Screen scr = Screen.FromRectangle(area);
            s = ShotStack.ScaleFor(scr);
            Pad = P(22);
            Plan();
            cap.Set(recW);
            Size sz = new Size(recW, P(46));
            Rectangle wa = scr.WorkingArea;
            int x0 = area.X + (area.Width - sz.Width) / 2;
            int y0 = area.Bottom + P(14);
            if (y0 + sz.Height > wa.Bottom) y0 = area.Top - sz.Height - P(14);
            // Full-screen area: inside it (it is excluded from the video anyway), above the taskbar.
            if (y0 < wa.Top) y0 = Math.Min(area.Bottom, wa.Bottom) - sz.Height - P(18);
            x0 = Math.Max(wa.Left + P(8), Math.Min(wa.Right - sz.Width - P(8), x0));
            SetSize(sz);
            JumpTo(x0, y0 + P(10));
            ShowQuiet();
            alpha.Go(1, 200, 0, Ease.OutCubic, null);
            MoveTo(x0, y0, 320, 0.78, 0);
            tick.Tick += delegate { Tick(); };
            Retime();
        }

        protected override bool PerPixel { get { return true; } }

        // Devices are being opened: nothing is recorded yet.
        public void Prepare(bool countdown)
        {
            if (phase == Phase.Rec && !IsDisposed) Become(Phase.Busy, countdown ? I18n.T("Cuenta atr\u00e1s\u2026", "Countdown\u2026") : I18n.T("Preparando\u2026", "Preparing\u2026"), -1);
        }

        // Everything is ready and recording starts: back to the recording controls.
        public void Ready()
        {
            if (phase != Phase.Busy || IsDisposed) return;
            phase = Phase.Rec;
            label = null;
            hot = pressed = -1;
            Morph(recW);
            fade.Go(0, fade.Value > 0.01 ? 90 : 1, 0, Ease.OutCubic, Swap);
            Retime();
            Anim.Wake(this);
        }

        // Recording stopped: saving starts.
        public void Saving()
        {
            if (phase == Phase.Rec && !IsDisposed) Become(Phase.Busy, I18n.T("Guardando\u2026", "Saving\u2026"), -1);
        }

        // The save was cancelled: what was recorded is being put away.
        public void Cancelling()
        {
            if (phase != Phase.Done && phase != Phase.Gone && !IsDisposed) Become(Phase.Busy, I18n.T("Conservando lo grabado\u2026", "Preserving recording\u2026"), -1);
        }

        // Little disk left while recording (null: enough again).
        public void Disk(string text, int level)
        {
            if (IsDisposed || (text == diskText && level == diskLevel)) return;
            diskText = text;
            diskLevel = level;
            Plan();
            if (phase == Phase.Rec && face == Phase.Rec) Morph(recW);
            Repaint();
            Anim.Wake(this);
        }

        // Paused or resumed: the time stops, the dot turns into a pause sign, "En pausa" takes the place of the sources and
        // the button becomes Reanudar. The layout stays as it is, so the button stays under the pointer.
        public void Paused(bool on)
        {
            if (IsDisposed || paused == on) return;
            paused = on;
            meter[0] = meter[1] = 0;
            if (hot == 5) Tip(5);
            Retime();
            Repaint();
        }

        // The pause button's tooltip, under it (or over it at the bottom of the screen); excluded from captures too.
        void Tip(int h)
        {
            if (h == 5 && phase == Phase.Rec && face == Phase.Rec && !IsDisposed)
            {
                Rectangle r = PauseRect();
                r.Offset(Pad + CapLeft(), Pad);
                EdTip.Schedule(this, r, paused ? ResumeTip : PauseTip);
                tipShown = true;
            }
            else if (tipShown)
            {
                tipShown = false;
                EdTip.Cancel();
            }
        }

        // A final pass with its progress; fraction < 0: unknown.
        public void Converting(string text, double fraction)
        {
            if (phase == Phase.Done || phase == Phase.Gone || IsDisposed) return;
            if (phase == Phase.Progress && text == label && (fraction < 0) == (progress < 0))
            {
                progress = fraction;
                if (face == Phase.Progress && faceLabel == text) faceProgress = fraction;
                Repaint();
                return;
            }
            Become(Phase.Progress, text, fraction);
        }

        // The end: a short check when the file was saved, then it fades out and closes (at once when discarded or failed).
        public void Finish(bool saved)
        {
            if (phase == Phase.Done || phase == Phase.Gone || IsDisposed) return;
            double wait = 0;
            Tip(-1);
            if (saved) { Become(Phase.Done, I18n.T("Guardado", "Saved"), -1); wait = 900; }
            else phase = Phase.Gone;
            tick.Stop();
            dragging = false;
            Cursor = Cursors.Default;
            alpha.Go(0, saved ? 240 : 160, wait, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            if (!moving) MoveTo(tx, ty + P(6), 260, 1, wait);
            Anim.Wake(this);
        }

        // Out of the way at once (a new recording is starting).
        void StepAside()
        {
            phase = Phase.Gone;
            tick.Stop();
            alpha.Go(0, 120, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            Anim.Wake(this);
        }

        // Another recording was asked for while this one runs or is being saved: a short shake says "busy".
        public void Nudge()
        {
            if (phase == Phase.Gone || IsDisposed || dragging) return;
            nudgeAt = Anim.Now;
            Anim.Wake(this);
        }

        void Become(Phase p, string text, double fraction)
        {
            // One pass after another (Cine's master, then its check): a first click on Cancelar still counts.
            if (!KeepsArmed(phase == Phase.Progress, p == Phase.Progress)) armedAt = -1;
            phase = p;
            label = text;
            progress = fraction;
            hot = pressed = -1;
            Tip(-1);
            if (!dragging) Cursor = Cursors.Default;
            Morph(PillWidth());
            // The old content fades out quickly while the capsule changes shape, then the new one fades in.
            fade.Go(0, fade.Value > 0.01 ? 90 : 1, 0, Ease.OutCubic, Swap);
            Retime();
            Anim.Wake(this);
        }

        internal static bool KeepsArmed(bool fromProgress, bool toProgress) { return fromProgress && toProgress; }

        void Swap()
        {
            if (phase == Phase.Gone || IsDisposed) return;
            face = phase;
            faceLabel = label;
            faceProgress = progress;
            faceW = PillWidth();
            fade.Go(1, 200, 0, Ease.OutCubic, null);
        }

        // The timer only runs while something on the bar changes by itself: the meters and the clock, or the spinner.
        void Retime()
        {
            if (IsDisposed) { tick.Stop(); return; }
            bool spinning = phase == Phase.Busy || (phase == Phase.Progress && progress < 0);
            bool asking = phase == Phase.Progress && armedAt >= 0;   // the cancel question expires by itself
            if (phase == Phase.Rec) tick.Interval = Metering() ? 50 : NextSecond();
            else if (spinning) tick.Interval = 90;
            else if (asking) tick.Interval = 200;
            if (phase == Phase.Rec || spinning || asking) tick.Start(); else tick.Stop();
        }

        bool Metering() { return !paused && ((hasSys && !session.SystemAudio.Muted) || (hasMic && !session.Microphone.Muted)); }

        // Without live meters only the clock changes (and the discard question expires): wake just after that.
        int NextSecond()
        {
            double ms = 1000 - session.Elapsed.TotalMilliseconds % 1000 + 15;
            if (armedAt >= 0) ms = Math.Min(ms, ArmMs - (Anim.Now - armedAt) + 15);
            return (int)Math.Max(20, Math.Min(1015, ms));
        }

        void Tick()
        {
            if (IsDisposed) return;
            if (phase == Phase.Rec)
            {
                if (hasSys) meter[0] = Math.Max(session.SystemAudio.Muted ? 0 : session.SystemAudio.Level, meter[0] * 0.8f);
                if (hasMic) meter[1] = Math.Max(session.Microphone.Muted ? 0 : session.Microphone.Level, meter[1] * 0.8f);
                if (armedAt >= 0 && Anim.Now - armedAt > ArmMs) armedAt = -1;
                if (!longClock && session.Elapsed.TotalHours >= 1)
                {
                    longClock = true;
                    Plan();
                    Morph(recW);
                    Anim.Wake(this);
                }
                if (!Metering()) Retime();
            }
            else
            {
                spin++;
                if (armedAt >= 0 && Anim.Now - armedAt > ArmMs) { armedAt = -1; Retime(); }
            }
            Repaint();
        }

        // Repaints only when something visible changed (a still bar costs one paint per second).
        void Repaint()
        {
            if (Signature() != shown) Redraw();
        }

        string Signature()
        {
            if (face == Phase.Rec)
                return "r" + Clock(session.Elapsed) + "|" + Bars(0) + Bars(1) + "|" + hot + "|" + pressed + "|" + (armedAt >= 0) +
                       (hasSys && session.SystemAudio.Muted) + (hasMic && session.Microphone.Muted) + "|" + diskText + diskLevel + "|" + paused;
            return face + "|" + faceLabel + "|" + (int)Math.Round(faceProgress * 100) + "|" + spin + "|" + hot + "|" + pressed + "|" + (armedAt >= 0);
        }

        static string Clock(TimeSpan t)
        {
            int h = (int)t.TotalHours;
            string ms = t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            return h > 0 ? h + ":" + ms : ms;
        }

        // Every digit takes the widest one's room, like tabular figures: the time never jitters as it counts.
        WM.FormattedText ClockGlyph(char c)
        {
            if (clockGlyphs == null || clockFor != HighContrast.Generation)
            {
                clockFor = HighContrast.Generation;
                clockGlyphs = new WM.FormattedText[11];
                for (int i = 0; i < 11; i++)
                {
                    clockGlyphs[i] = Ink.Px(i < 10 ? ((char)('0' + i)).ToString() : ":", Ds.Semibold, P(14), Palette.HudLabel);
                    if (i < 10) digitW = Math.Max(digitW, clockGlyphs[i].WidthIncludingTrailingWhitespace);
                }
            }
            return clockGlyphs[c >= '0' && c <= '9' ? c - '0' : 10];
        }

        double ClockCell(char c) { WM.FormattedText g = ClockGlyph(c); return c >= '0' && c <= '9' ? digitW : g.WidthIncludingTrailingWhitespace; }

        double ClockWidth(string text)
        {
            double w = 0;
            foreach (char c in text) w += ClockCell(c);
            return w;
        }

        void DrawClock(WM.DrawingContext dc, string text, double x, double cy)
        {
            foreach (char c in text)
            {
                WM.FormattedText g = ClockGlyph(c);
                double cell = ClockCell(c);
                dc.DrawText(g, new System.Windows.Point(x + (cell - g.WidthIncludingTrailingWhitespace) / 2, Math.Round(cy - g.Height / 2)));
                x += cell;
            }
        }

        // Lit bars of a source's meter (0-3), from -50 dB to 0 dB.
        int Bars(int i)
        {
            if (paused || (i == 0 ? !hasSys || session.SystemAudio.Muted : !hasMic || session.Microphone.Muted)) return 0;
            float level = meter[i];
            double db = level > 0.0001 ? 20 * Math.Log10(level) : -100;
            double v = Math.Max(0, Math.Min(1, (db + 50) / 50));
            int n = 0;
            for (int k = 0; k < 3; k++) if (v > (k + 0.5) / 3.5) n++;
            return n;
        }

        // ---- layout ----

        void Plan()
        {
            double clockW = ClockWidth(longClock ? "0:00:00" : "00:00");
            WM.FormattedText hint = Ink.Px(DiscardHint, Ds.Semibold, P(13), Palette.HudLabel);
            WM.FormattedText stop = Ink.Px(I18n.T("Detener", "Stop"), Ds.Semibold, P(13), Palette.HudLabel);
            timeX = P(34);
            srcX = timeX + (int)Math.Ceiling(clockW) + P(10);
            int left = srcX + (hasSys ? P(44) : 0) + (hasMic ? P(44) : 0);
            diskX = left + P(2);
            if (diskText != null) left = diskX + (int)Math.Ceiling(Ink.Px(diskText, Ds.Semibold, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace) + P(10);
            // While Discard is armed its question takes the place of the dot, the time and the sources; while paused,
            // "En pausa" does (there is always room for it, so pausing never moves the buttons).
            WM.FormattedText pausedText = Ink.Px(PausedText, Ds.Semibold, P(12), Palette.HudLabel);
            int chipW = diskText != null ? (int)Math.Ceiling(Ink.Px(diskText, Ds.Semibold, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace) + P(10) : 0;
            pauseX = Math.Max(Math.Max(left, P(14) + (int)Math.Ceiling(hint.WidthIncludingTrailingWhitespace) + P(8)),
                              timeX + (int)Math.Ceiling(pausedText.WidthIncludingTrailingWhitespace) + P(8) + chipW);
            stopX = pauseX + P(30) + P(6);
            stopW = P(30) + (int)Math.Ceiling(stop.WidthIncludingTrailingWhitespace) + P(16);
            discardX = stopX + stopW + P(4);
            recW = discardX + P(30) + P(8);
        }

        Rectangle StopRect() { return new Rectangle(stopX, P(8), stopW, body.Height - P(16)); }
        Rectangle DiscardRect() { return new Rectangle(discardX, (body.Height - P(30)) / 2, P(30), P(30)); }
        Rectangle PauseRect() { return new Rectangle(pauseX, (body.Height - P(30)) / 2, P(30), P(30)); }
        Rectangle SourceRect(int i) { return new Rectangle(srcX + i * P(44), P(7), P(40), body.Height - P(14)); }

        int PillIcon { get { return P(42); } }

        int PillWidth()
        {
            WM.FormattedText t = Ink.Px(label ?? "", Ds.Semibold, P(13), Palette.HudLabel);
            int w = PillIcon + (int)Math.Ceiling(t.WidthIncludingTrailingWhitespace) + P(18);
            if (phase == Phase.Progress && progress >= 0) w += P(12) + PercentWidth();
            if (phase == Phase.Progress) w += P(10) + CancelWidth() - P(8);
            return Math.Max(P(120), w);
        }

        // A final pass can be cancelled from the pill (what was recorded is kept in recuperado).
        int CancelWidth()
        {
            double a = Ink.Px(CancelText, Ds.Semibold, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace;
            double b = Ink.Px(CancelHint, Ds.Semibold, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace;
            return (int)Math.Ceiling(Math.Max(a, b)) + P(18);
        }

        // In the pill's content coordinates (content width fw).
        Rectangle CancelRect(int fw)
        {
            int w = CancelWidth(), h = P(28);
            return new Rectangle(fw - P(9) - w, (body.Height - h) / 2, w, h);
        }

        int PercentWidth() { return (int)Math.Ceiling(Ink.Px("100 %", Ds.Medium, P(12), Palette.HudLabel).WidthIncludingTrailingWhitespace); }

        int CapLeft() { return (int)Math.Round((body.Width - Math.Round(cap.Value)) / 2); }

        // The capsule glides to a new width around its center; the window only grows (in one move) when it must.
        void Morph(int width)
        {
            if (width > body.Width && IsHandleCreated)
            {
                int grow = width - body.Width;
                grow += grow & 1;
                body = new Size(body.Width + grow, body.Height);
                // Centered on the old capsule, but never past the edges of the screen.
                Rectangle wa = Screen.FromHandle(Handle).WorkingArea;
                double nx = Math.Max(wa.Left + P(8), Math.Min(wa.Right - body.Width - P(8), tx - grow / 2));
                x += nx - tx;
                tx = nx;
                Native.SetWindowPos(Handle, IntPtr.Zero, (int)Math.Round(x) - Pad, (int)Math.Round(y) - Pad, body.Width + 2 * Pad, body.Height + 2 * Pad,
                                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                ApplyPos();
            }
            cap.Go(width, 360, 0, Ease.OutCubic, null);
        }

        protected override bool StepExtra(double now)
        {
            bool repaint = cap.Running || fade.Running;
            cap.Step(now);
            fade.Step(now);
            bool shaking = false;
            if (nudgeAt >= 0)
            {
                double t = (now - nudgeAt) / 420.0;
                if (t >= 1 || moving || dragging) { nudgeAt = -1; if (!moving && !dragging) x = tx; }
                else { x = tx + Math.Sin(t * Math.PI * 6) * (1 - t) * P(7); shaking = true; }
                if (!moving) ApplyPos();
            }
            if (repaint && !IsDisposed) Redraw();
            return cap.Running || fade.Running || shaking;
        }

        // ---- painting ----

        static System.Windows.Rect R(Rectangle r) { return new System.Windows.Rect(r.X, r.Y, r.Width, r.Height); }

        // One soft shadow rendered once; its middle column stretches to any capsule width.
        void Shadow(WM.DrawingContext dc, double left, double width, int h)
        {
            if (shadowL == null)
            {
                shadowCore = P(64);
                int w = shadowCore + 2 * Pad, half = Pad + shadowCore / 2;
                WI.BitmapSource sh = Ink.Shadow(w, h, new System.Windows.Rect(Pad, Pad + P(5), shadowCore, body.Height), P(14), P(16), Ds.Argb(0.45, 0, 0, 0));
                shadowL = Crop(sh, 0, half - 2, h);
                shadowM = Crop(sh, half - 2, 4, h);
                shadowR = Crop(sh, half + 2, w - half - 2, h);
            }
            double lx = left - Pad, rx = left + width - shadowCore / 2 + 2;
            dc.DrawImage(shadowL, new System.Windows.Rect(lx, 0, shadowL.PixelWidth, h));
            dc.DrawImage(shadowR, new System.Windows.Rect(rx, 0, shadowR.PixelWidth, h));
            double mx = lx + shadowL.PixelWidth;
            if (rx > mx) dc.DrawImage(shadowM, new System.Windows.Rect(mx, 0, rx - mx, h));
        }

        static WI.BitmapSource Crop(WI.BitmapSource src, int x0, int w, int h)
        {
            WI.CroppedBitmap c = new WI.CroppedBitmap(src, new System.Windows.Int32Rect(x0, 0, w, h));
            c.Freeze();
            return c;
        }

        protected override void PaintSurface(WM.DrawingContext dc, int w, int h)
        {
            shown = Signature();
            double cw = Math.Round(cap.Value), cx = CapLeft();
            if (!Ds.Brushes.High) Shadow(dc, Pad + cx, cw, h);
            dc.PushTransform(new WM.TranslateTransform(Pad + cx, Pad));
            System.Windows.Rect b = new System.Windows.Rect(0, 0, cw, body.Height);
            Ink.Round(dc, Palette.HudBack(Ds.Argb(0.93, 30, 30, 32)), b, P(14));
            Ink.Hairline(dc, Palette.HudLine, b, P(14));
            dc.PushClip(new WM.RectangleGeometry(b, P(14), P(14)));
            dc.PushOpacity(Math.Max(0, Math.Min(1, fade.Value)));
            // The content keeps its final layout, centered: the capsule's edges reveal or hide it while it changes size.
            int fw = face == Phase.Rec ? recW : faceW;
            dc.PushTransform(new WM.TranslateTransform(Math.Round((cw - fw) / 2), 0));
            if (face == Phase.Rec) PaintRec(dc);
            else PaintPill(dc, fw);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        void PaintRec(WM.DrawingContext dc)
        {
            double cy = body.Height / 2.0;
            WM.Color white = Palette.HudLabel, dim = Palette.HudLabel2;
            bool armed = armedAt >= 0;
            if (armed)
            {
                WM.FormattedText ht = Ink.Px(DiscardHint, Ds.Semibold, P(13), Palette.HudInk(Ds.Rgb(255, 138, 128)));
                dc.DrawText(ht, new System.Windows.Point(P(14), Math.Round(cy - ht.Height / 2)));
            }
            else if (paused) PaintPaused(dc, cy);
            else
            {
                dc.DrawEllipse(Ds.Brush(Palette.HudInk(Red)), null, new System.Windows.Point(P(20), cy), 4.5 * s, 4.5 * s);
                DrawClock(dc, Clock(session.Elapsed), timeX, cy);
                if (hasSys) PaintSource(dc, SourceRect(0), 0, session.SystemAudio.Muted ? "soundoff" : "sound", session.SystemAudio.Muted, hot == 3);
                if (hasMic) PaintSource(dc, SourceRect(hasSys ? 1 : 0), 1, session.Microphone.Muted ? "micoff" : "mic", session.Microphone.Muted, hot == 2);
                if (diskText != null)
                {
                    WM.FormattedText dt = Ink.Px(diskText, Ds.Semibold, P(12), Palette.HudInk(diskLevel >= 2 ? Ds.Rgb(255, 105, 97) : Ds.Rgb(255, 179, 64)));
                    dc.DrawText(dt, new System.Windows.Point(diskX, Math.Round(cy - dt.Height / 2)));
                }
            }
            PaintPauseButton(dc, cy);
            System.Windows.Rect sr = R(StopRect());
            Ink.Round(dc, Palette.HudFill(hot == 0 && pressed == 0 ? Ds.Rgb(222, 46, 36) : hot == 0 ? Ds.Rgb(255, 92, 82) : Red), sr, sr.Height / 2);
            double sq = 9 * s;
            Ink.Round(dc, Palette.OnHudFill(white), new System.Windows.Rect(Math.Round(sr.X + P(14)), Math.Round(cy - sq / 2), sq, sq), 2 * s);
            WM.FormattedText st = Ink.Px(I18n.T("Detener", "Stop"), Ds.Semibold, P(13), Palette.OnHudFill(white));
            dc.DrawText(st, new System.Windows.Point(Math.Round(sr.X + P(30)), Math.Round(cy - st.Height / 2)));
            System.Windows.Rect dr = R(DiscardRect());
            System.Windows.Point dcp = new System.Windows.Point(dr.X + dr.Width / 2, cy);
            if (armed) dc.DrawEllipse(Ds.Brush(Palette.HudFill(hot == 1 && pressed == 1 ? Ds.Rgb(222, 46, 36) : hot == 1 ? Ds.Rgb(255, 92, 82) : Red)), null, dcp, dr.Width / 2, dr.Width / 2);
            else if (hot == 1) dc.DrawEllipse(Ds.Brush(pressed == 1 ? Palette.HudWash(Ds.Argb(0.26, 255, 255, 255)) : Palette.HudHover), null, dcp, dr.Width / 2, dr.Width / 2);
            double gs = 15 * s;
            Glyph.Draw(dc, "trash", dcp.X - gs / 2, cy - gs / 2, gs, armed ? Palette.OnHudFill(white) : hot == 1 ? white : dim, Math.Max(1.4, 1.6 * s));
        }

        // Paused: an amber pause sign where the dot is and "En pausa"; then the disk warning, or else the stopped time
        // (dimmed) when it fits before the buttons.
        void PaintPaused(WM.DrawingContext dc, double cy)
        {
            double bw = 2.6 * s, bh = 9 * s, gap = 2.4 * s, mid = P(20);
            WM.Color amber = Palette.HudInk(Amber);
            Ink.Round(dc, amber, new System.Windows.Rect(mid - gap / 2 - bw, cy - bh / 2, bw, bh), 1.1 * s);
            Ink.Round(dc, amber, new System.Windows.Rect(mid + gap / 2, cy - bh / 2, bw, bh), 1.1 * s);
            WM.FormattedText pt = Ink.Px(PausedText, Ds.Semibold, P(12), amber);
            dc.DrawText(pt, new System.Windows.Point(timeX, Math.Round(cy - pt.Height / 2)));
            double x = timeX + Math.Ceiling(pt.WidthIncludingTrailingWhitespace) + P(8), end = pauseX - P(8);
            if (diskText != null)
            {
                WM.FormattedText dt = Ink.Px(diskText, Ds.Semibold, P(12), diskLevel >= 2 ? Palette.HudDanger : amber);
                dc.DrawText(dt, new System.Windows.Point(x, Math.Round(cy - dt.Height / 2)));
                return;
            }
            string time = Clock(session.Elapsed);
            if (x + ClockWidth(time) > end) return;
            dc.PushOpacity(0.55);
            DrawClock(dc, time, x, cy);
            dc.Pop();
        }

        // Pausar (two bars) or, while paused, Reanudar (a play sign on a lit circle).
        void PaintPauseButton(WM.DrawingContext dc, double cy)
        {
            System.Windows.Rect pr = R(PauseRect());
            System.Windows.Point pc = new System.Windows.Point(pr.X + pr.Width / 2, cy);
            bool lit = hot == 5;
            if (paused) dc.DrawEllipse(Ds.Brush(Palette.HudWash(lit && pressed == 5 ? Ds.Argb(0.34, 255, 255, 255) : lit ? Ds.Argb(0.28, 255, 255, 255) : Palette.HudHover)), HcPaint.Border(lit ? 2 : 1), pc, pr.Width / 2, pr.Width / 2);
            else if (lit) dc.DrawEllipse(Ds.Brush(pressed == 5 ? Palette.HudWash(Ds.Argb(0.26, 255, 255, 255)) : Palette.HudHover), null, pc, pr.Width / 2, pr.Width / 2);
            WM.Color ink = paused || lit ? Palette.HudLabel : Palette.HudLabel2;
            if (paused)
            {
                double gs = 15 * s;
                Glyph.Draw(dc, "play!", pc.X - gs / 2 + 0.6 * s, cy - gs / 2, gs, ink, 1);
                return;
            }
            double bw = 2.8 * s, bh = 11 * s, gap = 3.2 * s;
            Ink.Round(dc, ink, new System.Windows.Rect(pc.X - gap / 2 - bw, cy - bh / 2, bw, bh), 1.2 * s);
            Ink.Round(dc, ink, new System.Windows.Rect(pc.X + gap / 2, cy - bh / 2, bw, bh), 1.2 * s);
        }

        // An audio source: its icon and three bars that light up with the level; red and silent while muted.
        void PaintSource(WM.DrawingContext dc, Rectangle r, int i, string icon, bool muted, bool hover)
        {
            if (hover) Ink.Round(dc, pressed == (i == 0 ? 3 : 2) ? Palette.HudWash(Ds.Argb(0.26, 255, 255, 255)) : Palette.HudHover, R(r), P(8));
            double gs = 17 * s, cy = r.Y + r.Height / 2.0;
            Glyph.Draw(dc, icon, r.X + P(4), cy - gs / 2, gs, muted ? Palette.HudDanger : Palette.HudLabel, Math.Max(1.4, 1.6 * s));
            int lit = Bars(i);
            for (int k = 0; k < 3; k++)
            {
                double bh = P(4 + k * 4), bx = r.X + P(25) + k * 4.5 * s, by = cy + P(7) - bh;
                Ink.Round(dc, k < lit ? Palette.HudInk(Green) : Palette.HudFaint(Ds.Argb(muted ? 0.12 : 0.22, 255, 255, 255)), new System.Windows.Rect(bx, by, 2.5 * s, bh), 1.2 * s);
            }
        }

        void PaintPill(WM.DrawingContext dc, double cw)
        {
            double cy = body.Height / 2.0, ix = P(23), r = 8.5 * s, th = Math.Max(1.5, 2 * s);
            WM.Color white = Palette.HudLabel;
            bool ring = face == Phase.Progress && faceProgress >= 0;
            if (face == Phase.Done)
            {
                dc.DrawEllipse(Ds.Brush(Palette.HudFill(Green)), null, new System.Windows.Point(ix, cy), r + 1, r + 1);
                double gs = 13 * s;
                Glyph.Draw(dc, "check", ix - gs / 2, cy - gs / 2, gs, Palette.OnHudFill(white), Math.Max(1.8, 2.2 * s));
            }
            else if (ring) Ring(dc, ix, cy, r, th, faceProgress);
            else Spinner(dc, ix, cy, th);
            double right = cw - P(18);
            if (face == Phase.Progress)
            {
                Rectangle cr = CancelRect((int)cw);
                bool asking = armedAt >= 0 && phase == Phase.Progress;
                System.Windows.Rect crr = R(cr);
                if (asking) Ink.Round(dc, Palette.HudFill(hot == 4 && pressed == 4 ? Ds.Rgb(222, 46, 36) : hot == 4 ? Ds.Rgb(255, 92, 82) : Red), crr, crr.Height / 2);
                else if (hot == 4) Ink.Round(dc, pressed == 4 ? Palette.HudWash(Ds.Argb(0.26, 255, 255, 255)) : Palette.HudHover, crr, crr.Height / 2);
                WM.FormattedText ct = Ink.Px(asking ? CancelHint : CancelText, Ds.Semibold, P(12), asking ? Palette.OnHudFill(white) : hot == 4 ? white : Palette.HudLabel2);
                dc.DrawText(ct, new System.Windows.Point(Math.Round(crr.X + (crr.Width - ct.WidthIncludingTrailingWhitespace) / 2), Math.Round(cy - ct.Height / 2)));
                right = cr.X - P(10);
            }
            if (ring)
            {
                WM.FormattedText pt = Ink.Px((int)Math.Round(faceProgress * 100) + " %", Ds.Medium, P(12), Palette.HudLabel2);
                dc.DrawText(pt, new System.Windows.Point(Math.Round(right - pt.WidthIncludingTrailingWhitespace), Math.Round(cy - pt.Height / 2)));
                right -= PercentWidth() + P(12);
            }
            WM.FormattedText lt = Ink.Px(faceLabel, Ds.Semibold, P(13), white);
            lt.MaxTextWidth = Math.Max(1, right - PillIcon);
            lt.MaxLineCount = 1;
            lt.Trimming = System.Windows.TextTrimming.CharacterEllipsis;
            dc.DrawText(lt, new System.Windows.Point(PillIcon, Math.Round(cy - lt.Height / 2)));
        }

        // Determinate progress: a thin ring that fills clockwise from the top.
        static void Ring(WM.DrawingContext dc, double cx, double cy, double r, double th, double f)
        {
            WM.Pen track = new WM.Pen(Ds.Brush(Palette.HudFaint(Ds.Argb(0.2, 255, 255, 255))), th);
            track.Freeze();
            dc.DrawEllipse(null, track, new System.Windows.Point(cx, cy), r, r);
            f = Math.Max(0, Math.Min(1, f));
            if (f <= 0.005) return;
            WM.Pen pen = new WM.Pen(Ds.Brush(Palette.HudInk(Accent)), th);
            pen.StartLineCap = pen.EndLineCap = WM.PenLineCap.Round;
            pen.Freeze();
            if (f >= 0.995) { dc.DrawEllipse(null, pen, new System.Windows.Point(cx, cy), r, r); return; }
            double a = f * 2 * Math.PI;
            WM.StreamGeometry g = new WM.StreamGeometry();
            using (WM.StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new System.Windows.Point(cx, cy - r), false, false);
                c.ArcTo(new System.Windows.Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a)), new System.Windows.Size(r, r), 0, a > Math.PI,
                        WM.SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }

        // Unknown progress: the macOS spinner, eight spokes fading behind the leading one.
        void Spinner(WM.DrawingContext dc, double cx, double cy, double th)
        {
            const int n = 8;
            double r0 = 4 * s, r1 = 8.5 * s;
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n - Math.PI / 2;
                int age = ((spin - i) % n + n) % n;
                WM.Pen p = new WM.Pen(Ds.Brush(Ds.WithAlpha(Palette.HudLabel, Math.Max(0.2, 1 - age * 0.13))), th);
                p.StartLineCap = p.EndLineCap = WM.PenLineCap.Round;
                p.Freeze();
                dc.DrawLine(p, new System.Windows.Point(cx + Math.Cos(a) * r0, cy + Math.Sin(a) * r0), new System.Windows.Point(cx + Math.Cos(a) * r1, cy + Math.Sin(a) * r1));
            }
        }

        // ---- mouse ----

        Point Local(Point p) { return new Point(p.X - Pad, p.Y - Pad); }

        bool OnCapsule(Point p)
        {
            int cx = CapLeft();
            return p.X >= cx && p.X < cx + Math.Round(cap.Value) && p.Y >= 0 && p.Y < body.Height;
        }

        // 0 Stop, 1 Discard, 2 microphone, 3 computer sound, 4 cancel the save, 5 pause or resume; -1 none.
        int HitTest(Point p)
        {
            if (phase == Phase.Progress && face == Phase.Progress && !cap.Running)
            {
                int cw = (int)Math.Round(cap.Value);
                Point c = new Point(p.X - CapLeft() - (int)Math.Round((cw - faceW) / 2.0), p.Y);
                return CancelRect(faceW).Contains(c) ? 4 : -1;
            }
            if (phase != Phase.Rec || face != Phase.Rec || cap.Running) return -1;
            Point q = new Point(p.X - CapLeft(), p.Y);
            if (StopRect().Contains(q)) return 0;
            if (DiscardRect().Contains(q)) return 1;
            if (PauseRect().Contains(q)) return 5;
            if (armedAt >= 0 || paused) return -1;
            if (hasMic && SourceRect(hasSys ? 1 : 0).Contains(q)) return 2;
            if (hasSys && SourceRect(0).Contains(q)) return 3;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // Once it says "Guardado" (or is going) it is only leaving: nothing to press or drag.
            if (e.Button != MouseButtons.Left || phase == Phase.Gone || phase == Phase.Done) return;
            Tip(-1);
            Point p = Local(e.Location);
            pressed = HitTest(p);
            if (pressed >= 0) { Repaint(); return; }
            if (!OnCapsule(p)) return;
            dragging = true;
            dragFrom = PointToScreen(e.Location);
            dragX = moving ? tx : x;
            dragY = moving ? ty : y;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) { Drag(PointToScreen(e.Location)); return; }
            int h = HitTest(Local(e.Location));
            if (h != hot)
            {
                hot = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                Tip(h);
                Repaint();
            }
        }

        // Moves the bar with the mouse, keeping the capsule inside the screen's working area.
        void Drag(Point c)
        {
            if (phase == Phase.Done || phase == Phase.Gone) return;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;
            int cx = CapLeft(), cw = (int)Math.Round(cap.Value);
            double nx = Math.Max(wa.Left - cx, Math.Min(wa.Right - cx - cw, dragX + c.X - dragFrom.X));
            double ny = Math.Max(wa.Top, Math.Min(wa.Bottom - body.Height, dragY + c.Y - dragFrom.Y));
            x = tx = nx;
            y = ty = ny;
            vx = vy = 0;
            moving = false;
            nudgeAt = -1;
            ApplyPos();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            Point p = Local(e.Location);
            if (dragging)
            {
                dragging = false;
                hot = HitTest(p);
                Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
                Repaint();
                return;
            }
            int down = pressed;
            pressed = -1;
            if (down >= 0 && HitTest(p) == down) Act(down);
            if (!IsDisposed) Repaint();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture) return;
            dragging = false;
            if (pressed >= 0) { pressed = -1; Repaint(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Tip(-1);
            if (hot != -1 && !dragging) { hot = -1; Repaint(); }
        }

        void Act(int what)
        {
            if (what == 4)
            {
                // Like Discard: a first click asks, a second one within a few seconds cancels.
                if (phase != Phase.Progress) return;
                if (armedAt >= 0) { armedAt = -1; session.CancelSave(); }
                else { armedAt = Anim.Now; Retime(); Repaint(); }
                return;
            }
            if (phase != Phase.Rec) return;
            if (what == 5) { armedAt = -1; session.TogglePause(); Retime(); Repaint(); return; }
            if (what == 0) session.Stop();
            else if (what == 1)
            {
                // A long recording is only thrown away on a second click.
                if (armedAt >= 0 || session.Elapsed.TotalMilliseconds < QuickDiscardMs) session.Cancel();
                else { armedAt = Anim.Now; Retime(); Repaint(); }
            }
            else if (what == 2) { session.Microphone.Muted = !session.Microphone.Muted; meter[1] = 0; Retime(); Repaint(); }
            else if (what == 3) { session.SystemAudio.Muted = !session.SystemAudio.Muted; meter[0] = 0; Retime(); Repaint(); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Tip(-1);
            tick.Dispose();
            if (previous == this) previous = null;
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new BarAccess(this); }

        // For screen readers: what the bar is doing ("Grabando 1:23", "En pausa", "Guardando") and its buttons, each
        // pressed as a click would be (Descartar asks first, as with the mouse).
        sealed class BarAccess : PartsAccessible
        {
            readonly RecordBar b;
            public BarAccess(RecordBar b) : base(b) { this.b = b; }

            public override AccessibleRole Role { get { return AccessibleRole.ToolBar; } }
            public override string Name { get { return I18n.T("Barra de grabaci\u00F3n", "Recording bar"); } }
            public override string Value
            {
                get
                {
                    if (b.phase == Phase.Rec) return (b.paused ? PausedText : I18n.T("Grabando", "Recording")) + " " + Clock(b.session.Elapsed);
                    if (b.phase == Phase.Progress || b.phase == Phase.Busy) return I18n.T("Guardando la grabaci\u00F3n", "Saving recording");
                    return I18n.T("Grabaci\u00F3n guardada", "Recording saved");
                }
            }

            protected override string Signature()
            {
                return b.phase + "|" + b.paused + "|" + (b.armedAt >= 0) + "|" + b.CapLeft() + "|" + b.stopX + "|" + b.pauseX + "|" + b.discardX + "|" + b.srcX +
                       "|" + (b.hasMic && b.session.Microphone.Muted) + "|" + (b.hasSys && b.session.SystemAudio.Muted);
            }

            Rectangle Window(Rectangle capsule)
            {
                capsule.Offset(b.CapLeft() + b.Pad, b.Pad);
                return capsule;
            }

            protected override void Fill(List<PartAccessible> into)
            {
                if (b.phase != Phase.Rec) return;
                Add(into, I18n.T("Detener y guardar", "Stop and save"), b.StopRect(), 0);
                Add(into, b.paused ? ResumeTip : PauseTip, b.PauseRect(), 5);
                Add(into, b.armedAt >= 0 ? I18n.T("Descartar (pulsa otra vez para confirmar)", "Discard (press again to confirm)") : I18n.T("Descartar", "Discard"), b.DiscardRect(), 1);
                if (b.paused || b.armedAt >= 0) return;
                if (b.hasMic) Add(into, b.session.Microphone.Muted ? I18n.T("Activar el micr\u00F3fono", "Unmute microphone") : I18n.T("Silenciar el micr\u00F3fono", "Mute microphone"), b.SourceRect(b.hasSys ? 1 : 0), 2);
                if (b.hasSys) Add(into, b.session.SystemAudio.Muted ? I18n.T("Activar el sonido del equipo", "Unmute system audio") : I18n.T("Silenciar el sonido del equipo", "Mute system audio"), b.SourceRect(0), 3);
            }

            void Add(List<PartAccessible> into, string name, Rectangle capsule, int what)
            {
                PartAccessible p = NewPart(into, name, AccessibleRole.PushButton, Window(capsule));
                p.PartState = StateOf(true, false, false);
                p.PartAction = I18n.T("Pulsar", "Press");
                p.Do = delegate { if (!b.IsDisposed) b.Act(what); };
            }
        }
    }

    // Quiet frame around the recorded area: a fine translucent white line with white corner marks, on click-through
    // strips that never appear in the video. A faint dark edge keeps both visible on white content. A side on the edge
    // of its screen gets no strip: the screen's edge already marks it, and the strip would show on the next monitor.
    class FrameEdge : FloatWindow
    {
        readonly int side;     // 0 top, 1 bottom, 2 left, 3 right
        readonly int t, mark, k;
        readonly int lead, trail; // top and bottom strips: how far they reach past the area on the left and the right

        FrameEdge(Rectangle r, int side, int t, int mark, int k, int lead, int trail)
        {
            this.side = side;
            this.t = t;
            this.mark = mark;
            this.k = k;
            this.lead = lead;
            this.trail = trail;
            SetSize(r.Size);
            JumpTo(r.X, r.Y);
            ShowQuiet();
            alpha.Go(1, 180, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override bool PerPixel { get { return true; } }
        protected override bool Rounded { get { return false; } }

        // Recording stopped: a quick fade, then gone (saving goes on without it).
        public void Dismiss()
        {
            if (IsDisposed) return;
            alpha.Go(0, 120, 0, Ease.OutCubic, delegate { if (!IsDisposed) Close(); });
            Anim.Wake(this);
        }

        // Paused: the frame fades back (nothing is being recorded); resumed: full again.
        public void Dim(bool on)
        {
            if (IsDisposed) return;
            alpha.Go(on ? 0.35 : 1, 200, 0, Ease.OutCubic, null);
            Anim.Wake(this);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: clicks pass through
                return cp;
            }
        }

        protected override void PaintSurface(WM.DrawingContext dc, int w, int h)
        {
            WM.Brush line = Ds.Brush(Ds.Argb(0.6, 255, 255, 255)), halo = Ds.Brush(Ds.Argb(0.3, 0, 0, 0));
            WM.Brush white = Ds.Brush(Ds.Rgb(255, 255, 255)), edge = Ds.Brush(Ds.Argb(0.34, 0, 0, 0));
            // The line hugs the recorded area; the strip is t wide so the corner marks (k thick, o from the outside) fit.
            int o = t - k;
            if (side < 2)
            {
                bool top = side == 0;
                double ly = top ? t - 1 : 0, my = top ? o : 0, ey = top ? o - 1 : 0;
                dc.DrawRectangle(halo, null, new System.Windows.Rect(0, top ? ly - 1 : ly + 1, w, 1));
                dc.DrawRectangle(line, null, new System.Windows.Rect(lead, ly, w - lead - trail, 1));
                // Each mark covers the corner square when there is a side strip to meet, else it starts at the area.
                int a0 = Math.Max(0, lead - k), a1 = lead + mark, b0 = w - trail - mark, b1 = Math.Min(w, w - trail + k);
                int e0 = Math.Max(0, a0 - 1), e1 = Math.Min(w, b1 + 1);
                dc.DrawRectangle(edge, null, new System.Windows.Rect(e0, ey, a1 + 1 - e0, k + 1));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(b0 - 1, ey, e1 - b0 + 1, k + 1));
                dc.DrawRectangle(white, null, new System.Windows.Rect(a0, my, a1 - a0, k));
                dc.DrawRectangle(white, null, new System.Windows.Rect(b0, my, b1 - b0, k));
            }
            else
            {
                bool left = side == 2;
                double lx = left ? t - 1 : 0, mx = left ? o : 0, ex = left ? o - 1 : 0;
                dc.DrawRectangle(halo, null, new System.Windows.Rect(left ? lx - 1 : lx + 1, 0, 1, h));
                dc.DrawRectangle(line, null, new System.Windows.Rect(lx, 0, 1, h));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(ex, 0, k + 1, mark + 1));
                dc.DrawRectangle(edge, null, new System.Windows.Rect(ex, h - mark - 1, k + 1, mark + 1));
                dc.DrawRectangle(white, null, new System.Windows.Rect(mx, 0, k, mark));
                dc.DrawRectangle(white, null, new System.Windows.Rect(mx, h - mark, k, mark));
            }
        }

        public static FrameEdge[] Around(Rectangle a)
        {
            float s = ShotStack.ScaleFor(Screen.FromRectangle(a));
            int t = Math.Max(4, (int)Math.Round(4 * s)), mark = (int)Math.Round(18 * s), k = Math.Max(3, Math.Min(t - 1, (int)Math.Round(3 * s)));
            bool top = !OnScreenEdge(a, 0), bottom = !OnScreenEdge(a, 1), left = !OnScreenEdge(a, 2), right = !OnScreenEdge(a, 3);
            int l = left ? t : 0, r = right ? t : 0;
            List<FrameEdge> strips = new List<FrameEdge>();
            if (top) strips.Add(new FrameEdge(new Rectangle(a.X - l, a.Y - t, a.Width + l + r, t), 0, t, mark, k, l, r));
            if (bottom) strips.Add(new FrameEdge(new Rectangle(a.X - l, a.Bottom, a.Width + l + r, t), 1, t, mark, k, l, r));
            if (left) strips.Add(new FrameEdge(new Rectangle(a.X - t, a.Y, t, a.Height), 2, t, mark, k, 0, 0));
            if (right) strips.Add(new FrameEdge(new Rectangle(a.Right, a.Y, t, a.Height), 3, t, mark, k, 0, 0));
            return strips.ToArray();
        }

        // Whether that side of the area lies on (or past) the edge of the screen its middle is on.
        static bool OnScreenEdge(Rectangle a, int side)
        {
            Point p = side == 0 ? new Point(a.X + a.Width / 2, a.Y) : side == 1 ? new Point(a.X + a.Width / 2, a.Bottom - 1)
                    : side == 2 ? new Point(a.X, a.Y + a.Height / 2) : new Point(a.Right - 1, a.Y + a.Height / 2);
            Rectangle b = Screen.FromPoint(p).Bounds;
            if (!b.Contains(p)) return false;
            return side == 0 ? a.Top <= b.Top : side == 1 ? a.Bottom >= b.Bottom : side == 2 ? a.Left <= b.Left : a.Right >= b.Right;
        }
    }
}
