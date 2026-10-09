// Stackshot - Sound for recordings: what the computer plays (WASAPI loopback) and the microphone.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Stackshot
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out AudioDevices.PropertyKey key);
        [PreserveSig] int GetValue(ref AudioDevices.PropertyKey key, out AudioDevices.PropVariant value);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out int frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out int frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long qpcPosition);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(int frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(int frames, int flags);
    }

    [ComImport, Guid("CD63314F-3FBA-4A1B-812C-EF96358728E7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClock
    {
        [PreserveSig] int GetFrequency(out long frequency);
        [PreserveSig] int GetPosition(out long position, out long qpcPosition);
    }

    public static class AudioDevices
    {
        [StructLayout(LayoutKind.Sequential)] public struct PropertyKey { public Guid Fmtid; public int Pid; }
        [StructLayout(LayoutKind.Sequential)] public struct PropVariant { public short Vt; short r1, r2, r3; public IntPtr P; IntPtr p2; }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant pv);

        public const int Render = 0, Capture = 1, Console = 0;

        internal static IMMDeviceEnumerator Enumerator() { return (IMMDeviceEnumerator)new MMDeviceEnumeratorCom(); }

        internal static string Name(IMMDevice d)
        {
            IPropertyStore ps;
            if (d.OpenPropertyStore(0, out ps) < 0) return null;
            try
            {
                PropertyKey k = new PropertyKey();
                k.Fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"); // PKEY_Device_FriendlyName
                k.Pid = 14;
                PropVariant v;
                if (ps.GetValue(ref k, out v) < 0) return null;
                string s = v.Vt == 31 ? Marshal.PtrToStringUni(v.P) : null; // VT_LPWSTR
                PropVariantClear(ref v);
                return s;
            }
            finally { Marshal.ReleaseComObject(ps); }
        }

        // Active microphones: id and name.
        public static List<KeyValuePair<string, string>> Microphones()
        {
            return Endpoints(Capture, "Micr\u00F3fono");
        }

        // Active outputs (speakers, headphones, screens with sound): id and name.
        public static List<KeyValuePair<string, string>> Outputs()
        {
            return Endpoints(Render, "Salida de sonido");
        }

        static List<KeyValuePair<string, string>> Endpoints(int flow, string fallback)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            try
            {
                IMMDeviceEnumerator en = Enumerator();
                try { Collect(en, flow, list, fallback); }
                finally { Marshal.ReleaseComObject(en); }
            }
            catch (Exception ex) { ShotStack.Log("Dispositivos de sonido: " + ex.Message); }
            return list;
        }

        // Active endpoints of one kind; names are read only when a fallback name is given.
        static void Collect(IMMDeviceEnumerator en, int flow, List<KeyValuePair<string, string>> list, string fallback)
        {
            IMMDeviceCollection all;
            if (en.EnumAudioEndpoints(flow, 1, out all) < 0) return; // DEVICE_STATE_ACTIVE
            try
            {
                int n;
                if (all.GetCount(out n) < 0) return;
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d;
                    if (all.Item(i, out d) < 0) continue;
                    try
                    {
                        string id;
                        if (d.GetId(out id) >= 0 && id != null) list.Add(new KeyValuePair<string, string>(id, fallback == null ? "" : Name(d) ?? fallback));
                    }
                    finally { Marshal.ReleaseComObject(d); }
                }
            }
            finally { Marshal.ReleaseComObject(all); }
        }

        // Whether an endpoint is plugged in, enabled and of the given kind (an output id never opens as a microphone).
        internal static bool IsActive(IMMDeviceEnumerator en, int flow, string id)
        {
            if (string.IsNullOrEmpty(id) || en == null) return false;
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            try { Collect(en, flow, list, null); }
            catch { return false; }
            foreach (KeyValuePair<string, string> d in list) if (string.Equals(d.Key, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Name of the device Windows uses by default (null if there is none).
        public static string DefaultName(bool microphone)
        {
            try
            {
                IMMDeviceEnumerator en = Enumerator();
                IMMDevice d;
                string name = null;
                if (en.GetDefaultAudioEndpoint(microphone ? Capture : Render, Console, out d) >= 0)
                {
                    name = Name(d);
                    Marshal.ReleaseComObject(d);
                }
                Marshal.ReleaseComObject(en);
                return name;
            }
            catch { return null; }
        }
    }

    // One sound source, written as 48 kHz stereo float on the recording's timeline. Each packet carries the QPC time of
    // its first sample, so it lands exactly where it belongs: gaps (nothing playing, a glitch, a device switch) become
    // silence, overlaps are dropped, and the slow drift between the sound card's clock and the system clock is absorbed
    // one sample at a time, which is inaudible. The video uses the same clock, so sound and image stay in sync however
    // long the recording is.
    sealed class AudioCapture
    {
        public const int Rate = 48000;
        const int Shared = 0, Loopback = 0x20000, AutoConvert = unchecked((int)0x80000000), DefaultQuality = 0x08000000;
        const int Invalidated = unchecked((int)0x88890004), AccessDenied = unchecked((int)0x80070005), NotFound = unchecked((int)0x80070490);
        static readonly Guid IidAudioClient = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
        static readonly Guid IidCapture = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
        static readonly Guid IidRender = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
        static readonly Guid IidClock = new Guid("CD63314F-3FBA-4A1B-812C-EF96358728E7");

        public readonly bool Computer;   // what the computer plays; otherwise a microphone
        readonly string wantedId;         // null: Windows' default device, followed if it changes
        readonly string path;
        public string DeviceName = "";
        public string Error;              // why it could not start
        public volatile bool Muted;
        volatile float level;
        volatile bool stopping;
        Thread thread;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        FileStream file;
        long t0 = long.MinValue;          // timeline start, in 100 ns units of the QPC clock
        long written;                     // frames written to the file
        double drift;                     // smoothed timing error, in frames
        int glitches;

        // The device in use and its conversion state.
        IMMDeviceEnumerator enumerator;
        IAudioClient client, keepAlive;
        IAudioCaptureClient capture;
        IAudioRenderClient silence;
        IAudioClock keepClock;
        string openId;
        bool fellBack;                    // the chosen device was missing once (logged)
        int channels = 2, srcRate = Rate, bits = 32;
        bool isFloat = true;
        int keepFrames, keepRate;
        long keepWritten, nextLatencyCheck;
        double latency = -1;              // loopback: time from mixing to playing, in 100 ns units
        double resamplePos;               // fallback resampler state when the device can't give 48 kHz
        float lastL, lastR;
        float[] work = new float[0];

        public AudioCapture(bool computer, string deviceId, string path)
        {
            Computer = computer;
            wantedId = string.IsNullOrEmpty(deviceId) ? null : deviceId;
            this.path = path;
        }


        // Peak of the last moments, 0..1 (for the meter while recording).
        public float Level { get { return level; } }

        // Opens the device and starts listening (samples before Begin are discarded). False with Error set if it fails.
        public bool Start()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            ready.WaitOne(4000);
            if (Error == null && client == null) Error = "El dispositivo de sonido no responde.";
            if (Error != null) { stopping = true; thread.Join(2000); }
            return Error == null;
        }

        // Timeline start (Stopwatch ticks).
        public void Begin(long qpc)
        {
            Interlocked.Exchange(ref t0, (long)(qpc * (10000000.0 / Stopwatch.Frequency)));
        }

        // Stops and leaves exactly `frames` frames in the file.
        public void Finish(long frames)
        {
            stopping = true;
            if (thread != null) thread.Join(3000);
            try
            {
                if (file == null) return;
                if (written < frames) WriteSilence(frames - written);
                file.Flush();
                if (written > frames) file.SetLength(frames * 8);
                file.Dispose();
                file = null;
            }
            catch (Exception ex) { ShotStack.Log("Sonido: " + ex.Message); }
            if (glitches > 0) ShotStack.Log("Sonido (" + (Computer ? "equipo" : "micr\u00F3fono") + "): " + glitches + " ajustes de sincron\u00EDa");
            if (Computer) ShotStack.Log("Sonido (equipo): " + (latency < 0 ? "sin reloj del dispositivo" : "retardo de reproducci\u00F3n " + (latency / 10000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms"));
        }

        void Run()
        {
            try
            {
                enumerator = AudioDevices.Enumerator();
                string why = Open();
                if (why != null) { Error = why; ready.Set(); Close(); return; }
                file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            }
            catch (Exception ex) { Error = ex.Message; ready.Set(); Close(); return; }
            ready.Set();
            long nextCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            while (!stopping)
            {
                int hr = Drain();
                if (hr == Invalidated || hr < 0)
                {
                    // Unplugged, disabled or switched: reopen (the default one, if the chosen one is gone).
                    Close();
                    Thread.Sleep(200);
                    if (Open() != null) Thread.Sleep(300);
                    continue;
                }
                if (Stopwatch.GetTimestamp() > nextCheck)
                {
                    nextCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    // Off the chosen device (none chosen, or it is gone): follow Windows' default, and go back to the
                    // chosen one as soon as it returns.
                    bool onWanted = wantedId != null && capture != null && string.Equals(openId, wantedId, StringComparison.OrdinalIgnoreCase);
                    if (!onWanted && (capture == null || DefaultId() != openId || WantedActive())) { Close(); Open(); }
                }
                Thread.Sleep(4);
            }
            Drain();
            Close();
        }

        string DefaultId()
        {
            IMMDevice d;
            if (enumerator.GetDefaultAudioEndpoint(Computer ? AudioDevices.Render : AudioDevices.Capture, AudioDevices.Console, out d) < 0) return null;
            string id;
            d.GetId(out id);
            Marshal.ReleaseComObject(d);
            return id;
        }

        // The chosen device is plugged in, enabled and of the right kind.
        bool WantedActive()
        {
            if (wantedId == null || enumerator == null) return false;
            try { return AudioDevices.IsActive(enumerator, Computer ? AudioDevices.Render : AudioDevices.Capture, wantedId); }
            catch { return false; }
        }

        string Open()
        {
            IMMDevice dev = null;
            int hr = -1;
            if (WantedActive()) hr = enumerator.GetDevice(wantedId, out dev);
            else if (wantedId != null && !fellBack)
            {
                fellBack = true;
                ShotStack.Log("Sonido (" + (Computer ? "equipo" : "micr\u00F3fono") + "): el dispositivo elegido no est\u00E1 conectado, se usa el de Windows");
            }
            if (hr < 0) hr = enumerator.GetDefaultAudioEndpoint(Computer ? AudioDevices.Render : AudioDevices.Capture, AudioDevices.Console, out dev);
            if (hr < 0) return hr == NotFound ? (Computer ? "No hay ninguna salida de sonido." : "No hay ning\u00FAn micr\u00F3fono conectado.") : "No se encuentra el dispositivo (0x" + hr.ToString("X8") + ").";
            try
            {
                dev.GetId(out openId);
                DeviceName = AudioDevices.Name(dev) ?? "";
                object o;
                Guid iid = IidAudioClient;
                hr = dev.Activate(ref iid, 23, IntPtr.Zero, out o);
                if (hr < 0) return Denied(hr);
                client = (IAudioClient)o;
                // Ask Windows for 48 kHz stereo float whatever the device runs at (it converts with good quality).
                IntPtr fmt = Format(Rate, 2, 32, true);
                try { hr = client.Initialize(Shared, (Computer ? Loopback : 0) | AutoConvert | DefaultQuality, 2000000, 0, fmt, IntPtr.Zero); }
                finally { Marshal.FreeCoTaskMem(fmt); }
                if (hr >= 0) { channels = 2; srcRate = Rate; bits = 32; isFloat = true; }
                else
                {
                    // Older drivers: take the device's own format and convert here.
                    Marshal.ReleaseComObject(client);
                    hr = dev.Activate(ref iid, 23, IntPtr.Zero, out o);
                    if (hr < 0) return Denied(hr);
                    client = (IAudioClient)o;
                    IntPtr mix;
                    hr = client.GetMixFormat(out mix);
                    if (hr < 0) return Denied(hr);
                    try
                    {
                        ReadFormat(mix);
                        hr = client.Initialize(Shared, Computer ? Loopback : 0, 2000000, 0, mix, IntPtr.Zero);
                    }
                    finally { Marshal.FreeCoTaskMem(mix); }
                    if (hr < 0) return Denied(hr);
                    ShotStack.Log("Sonido: formato propio del dispositivo " + srcRate + " Hz, " + channels + " canales, " + bits + " bits");
                }
                object svc;
                iid = IidCapture;
                hr = client.GetService(ref iid, out svc);
                if (hr < 0) return Denied(hr);
                capture = (IAudioCaptureClient)svc;
                if (Computer) StartKeepAlive(dev);
                hr = client.Start();
                if (hr < 0) return Denied(hr);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
            finally { Marshal.ReleaseComObject(dev); }
        }

        string Denied(int hr)
        {
            if (hr == AccessDenied)
                return Computer ? "Windows no deja grabar el sonido del equipo." :
                       "Windows no deja usar el micr\u00F3fono. Act\u00EDvalo en Configuraci\u00F3n > Privacidad y seguridad > Micr\u00F3fono (\u00ABPermitir que las aplicaciones de escritorio accedan al micr\u00F3fono\u00BB).";
            return "El dispositivo de sonido no se puede usar (0x" + hr.ToString("X8") + ").";
        }

        // Loopback only delivers packets while something plays. A silent stream on the same device keeps them coming, so
        // the timing stays continuous, and its clock tells how long the device takes from mixing a sound to playing it.
        void StartKeepAlive(IMMDevice dev)
        {
            try
            {
                object o;
                Guid iid = IidAudioClient;
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) return;
                keepAlive = (IAudioClient)o;
                IntPtr mix;
                if (keepAlive.GetMixFormat(out mix) < 0) { StopKeepAlive(); return; }
                int hr;
                try
                {
                    keepRate = Marshal.ReadInt32(mix, 4);
                    hr = keepAlive.Initialize(Shared, 0, 2000000, 0, mix, IntPtr.Zero);
                }
                finally { Marshal.FreeCoTaskMem(mix); }
                if (hr < 0 || keepAlive.GetBufferSize(out keepFrames) < 0) { StopKeepAlive(); return; }
                object svc;
                iid = IidRender;
                if (keepAlive.GetService(ref iid, out svc) < 0) { StopKeepAlive(); return; }
                silence = (IAudioRenderClient)svc;
                iid = IidClock;
                if (keepAlive.GetService(ref iid, out svc) >= 0) keepClock = (IAudioClock)svc;
                keepWritten = 0;
                FeedSilence();
                keepAlive.Start();
            }
            catch (Exception ex) { ShotStack.Log("Sonido: " + ex.Message); StopKeepAlive(); }
        }

        void FeedSilence()
        {
            if (silence == null) return;
            int padding;
            if (keepAlive.GetCurrentPadding(out padding) < 0) return;
            int n = keepFrames - padding;
            IntPtr p;
            if (n > 0 && silence.GetBuffer(n, out p) >= 0)
            {
                silence.ReleaseBuffer(n, 2); // AUDCLNT_BUFFERFLAGS_SILENT
                keepWritten += n;
                padding += n;
            }
            if (keepClock == null || Stopwatch.GetTimestamp() < nextLatencyCheck) return;
            nextLatencyCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4;
            // Frames the engine has taken from our buffer minus frames the device has played = mixing-to-playing delay.
            long freq, pos, qpc;
            if (keepClock.GetFrequency(out freq) < 0 || keepClock.GetPosition(out pos, out qpc) < 0 || freq <= 0 || pos <= 0) return;
            double played = pos * (double)keepRate / freq + (Now100ns() - qpc) * keepRate / 1e7;
            double ms = (keepWritten - padding - played) * 1000.0 / keepRate;
            if (ms < 0 || ms > 1000) return;
            double value = ms * 10000;
            latency = latency < 0 ? value : latency * 0.8 + value * 0.2;
        }

        void StopKeepAlive()
        {
            try { if (keepAlive != null) keepAlive.Stop(); } catch { }
            if (keepClock != null) { Marshal.ReleaseComObject(keepClock); keepClock = null; }
            if (silence != null) { Marshal.ReleaseComObject(silence); silence = null; }
            if (keepAlive != null) { Marshal.ReleaseComObject(keepAlive); keepAlive = null; }
        }

        static long Now100ns() { return (long)(Stopwatch.GetTimestamp() * (10000000.0 / Stopwatch.Frequency)); }

        void Close()
        {
            StopKeepAlive();
            try { if (client != null) client.Stop(); } catch { }
            if (capture != null) { Marshal.ReleaseComObject(capture); capture = null; }
            if (client != null) { Marshal.ReleaseComObject(client); client = null; }
            if (stopping && enumerator != null) { Marshal.ReleaseComObject(enumerator); enumerator = null; }
        }

        static IntPtr Format(int rate, int ch, int bits, bool isFloat)
        {
            // WAVEFORMATEXTENSIBLE
            IntPtr p = Marshal.AllocCoTaskMem(40);
            Marshal.WriteInt16(p, 0, unchecked((short)0xFFFE));
            Marshal.WriteInt16(p, 2, (short)ch);
            Marshal.WriteInt32(p, 4, rate);
            Marshal.WriteInt32(p, 8, rate * ch * bits / 8);
            Marshal.WriteInt16(p, 12, (short)(ch * bits / 8));
            Marshal.WriteInt16(p, 14, (short)bits);
            Marshal.WriteInt16(p, 16, 22);
            Marshal.WriteInt16(p, 18, (short)bits);
            Marshal.WriteInt32(p, 20, ch == 2 ? 3 : 4); // front left + right, or center
            byte[] sub = new Guid(isFloat ? "00000003-0000-0010-8000-00AA00389B71" : "00000001-0000-0010-8000-00AA00389B71").ToByteArray();
            Marshal.Copy(sub, 0, new IntPtr(p.ToInt64() + 24), 16);
            return p;
        }

        void ReadFormat(IntPtr f)
        {
            int tag = Marshal.ReadInt16(f, 0) & 0xFFFF;
            channels = Marshal.ReadInt16(f, 2);
            srcRate = Marshal.ReadInt32(f, 4);
            bits = Marshal.ReadInt16(f, 14);
            isFloat = tag == 3;
            if (tag == 0xFFFE)
            {
                byte[] g = new byte[16];
                Marshal.Copy(new IntPtr(f.ToInt64() + 24), g, 0, 16);
                isFloat = new Guid(g) == new Guid("00000003-0000-0010-8000-00AA00389B71");
            }
        }

        // Reads every pending packet. Returns the first failure, if any.
        // When does each packet's first sample belong? Microphones stamp packets with the moment they were captured,
        // and those stamps are used when they make sense. Loopback stamps are not reliable (they run ~150 ms ahead on
        // some devices), so loopback is dated by arrival (the newest sample has just been mixed) plus the device's
        // measured delay from mixing to playing: the sound lands where it was heard, as players keep it in sync with
        // their picture.
        int Drain()
        {
            if (capture == null) return 0;
            FeedSilence();
            int queued;
            if (client.GetCurrentPadding(out queued) < 0) queued = 0;
            long arrival = Now100ns();
            while (true)
            {
                int n;
                int hr = capture.GetNextPacketSize(out n);
                if (hr < 0) return hr;
                if (n == 0) return 0;
                IntPtr data;
                int flags;
                long devPos, qpc;
                hr = capture.GetBuffer(out data, out n, out flags, out devPos, out qpc);
                if (hr < 0) return hr;
                if (hr == 0x08890001 || n == 0) { capture.ReleaseBuffer(n); return 0; } // AUDCLNT_S_BUFFER_EMPTY
                long byArrival = arrival - (long)(Math.Max(queued, n) * 1e7 / srcRate);
                queued = Math.Max(0, queued - n);
                long when;
                if (Computer) when = byArrival + (long)Math.Max(0, latency);
                else when = (flags & 4) == 0 && qpc <= arrival + 50000 && arrival - qpc < 2000000 ? qpc : byArrival;
                try { Packet(data, n, flags, when); }
                catch (Exception ex) { ShotStack.Log("Sonido: " + ex.Message); }
                capture.ReleaseBuffer(n);
            }
        }

        // One packet: to 48 kHz stereo float, onto the timeline. qpc: time of its first sample (100 ns units).
        void Packet(IntPtr data, int n, int flags, long qpc)
        {
            bool silent = (flags & 2) != 0 || Muted;
            int frames = ToStereo(data, n, silent);
            float peak = 0;
            for (int i = 0; i < frames * 2; i++) { float a = Math.Abs(work[i]); if (a > peak) peak = a; }
            level = Math.Max(peak, level * 0.85f);
            long start = Interlocked.Read(ref t0);
            if (start == long.MinValue || file == null) return;
            // Where this packet belongs, in frames from the start of the recording.
            double at = (qpc - start) * (Rate / 10000000.0);
            int skip = 0;
            if (at < 0)
            {
                skip = (int)Math.Min(frames, Math.Round(-at));
                at = 0;
                if (skip >= frames) return;
            }
            // The first sound starts exactly at its time.
            if (written == 0 && at >= 1) WriteSilence((long)Math.Round(at));
            double err = at - written;
            if (err > Rate * 0.03)
            {
                // A gap: silence until the packet's real time.
                WriteSilence((long)Math.Round(err));
                drift = 0;
                glitches++;
            }
            else if (err < -Rate * 0.03)
            {
                // Overlap: this part was already covered.
                skip = (int)Math.Min(frames, skip + Math.Round(-err));
                drift = 0;
                glitches++;
                if (skip >= frames) return;
            }
            else drift = drift * 0.98 + err * 0.02;
            int count = frames - skip, target = count;
            // Clock drift: stretch or squeeze this packet by one sample when the error passes 2 ms.
            if (drift > Rate * 0.002 && count > 32) { target = count + 1; drift -= 1; }
            else if (drift < -Rate * 0.002 && count > 32) { target = count - 1; drift += 1; }
            WriteFrames(skip, count, target);
        }

        // Converts n device frames into `work` as interleaved stereo float at 48 kHz; returns the frame count.
        int ToStereo(IntPtr data, int n, bool silent)
        {
            int bytes = bits / 8, stride = bytes * channels;
            if (srcRate == Rate && channels == 2 && isFloat && bits == 32)
            {
                Ensure(n * 2);
                if (silent) Array.Clear(work, 0, n * 2);
                else Marshal.Copy(data, work, 0, n * 2);
                return n;
            }
            float[] src = new float[n * 2];
            if (!silent)
            {
                byte[] raw = new byte[n * stride];
                Marshal.Copy(data, raw, 0, raw.Length);
                for (int i = 0; i < n; i++)
                {
                    float l = Sample(raw, i * stride, bytes), r = channels > 1 ? Sample(raw, i * stride + bytes, bytes) : l;
                    if (channels > 2) { float c = Sample(raw, i * stride + 2 * bytes, bytes) * 0.7071f; l += c; r += c; }
                    src[2 * i] = l;
                    src[2 * i + 1] = r;
                }
            }
            if (srcRate == Rate) { Ensure(n * 2); Array.Copy(src, work, n * 2); return n; }
            // Linear resampling, only for devices that refuse automatic conversion.
            double step = srcRate / (double)Rate;
            int outN = 0;
            Ensure((int)(n / step) * 2 + 8);
            while (resamplePos < n)
            {
                int i = (int)Math.Floor(resamplePos);
                double f = resamplePos - i;
                float l0 = i == 0 ? lastL : src[2 * (i - 1)], r0 = i == 0 ? lastR : src[2 * (i - 1) + 1];
                float l1 = src[2 * i], r1 = src[2 * i + 1];
                work[2 * outN] = (float)(l0 + (l1 - l0) * f);
                work[2 * outN + 1] = (float)(r0 + (r1 - r0) * f);
                outN++;
                resamplePos += step;
            }
            resamplePos -= n;
            lastL = src[2 * (n - 1)];
            lastR = src[2 * (n - 1) + 1];
            return outN;
        }

        float Sample(byte[] raw, int o, int bytes)
        {
            if (isFloat) return bytes == 4 ? BitConverter.ToSingle(raw, o) : (float)BitConverter.ToDouble(raw, o);
            if (bytes == 2) return BitConverter.ToInt16(raw, o) / 32768f;
            if (bytes == 3) return ((raw[o] << 8) | (raw[o + 1] << 16) | (raw[o + 2] << 24)) / 2147483648f;
            return BitConverter.ToInt32(raw, o) / 2147483648f;
        }

        void Ensure(int floats)
        {
            if (work.Length < floats) work = new float[floats];
        }

        byte[] outBytes = new byte[0];

        // Writes `count` frames of `work` starting at `skip`, resampled to `target` frames (target = count +-1 at most).
        void WriteFrames(int skip, int count, int target)
        {
            if (outBytes.Length < target * 8) outBytes = new byte[target * 8];
            if (target == count) Buffer.BlockCopy(work, skip * 8, outBytes, 0, count * 8);
            else
            {
                float[] tmp = new float[target * 2];
                double ratio = (count - 1) / (double)Math.Max(1, target - 1);
                for (int i = 0; i < target; i++)
                {
                    double pos = i * ratio;
                    int a = (int)pos;
                    int b = Math.Min(count - 1, a + 1);
                    float f = (float)(pos - a);
                    tmp[2 * i] = work[2 * (skip + a)] + (work[2 * (skip + b)] - work[2 * (skip + a)]) * f;
                    tmp[2 * i + 1] = work[2 * (skip + a) + 1] + (work[2 * (skip + b) + 1] - work[2 * (skip + a) + 1]) * f;
                }
                Buffer.BlockCopy(tmp, 0, outBytes, 0, target * 8);
            }
            file.Write(outBytes, 0, target * 8);
            written += target;
        }

        void WriteSilence(long frames)
        {
            byte[] zero = new byte[Math.Min(frames, 4800) * 8];
            while (frames > 0)
            {
                int n = (int)Math.Min(frames, 4800);
                file.Write(zero, 0, n * 8);
                written += n;
                frames -= n;
            }
        }
    }
}
