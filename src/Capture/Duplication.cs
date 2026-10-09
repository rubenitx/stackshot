// Stackshot - Screen frames for recordings: DXGI Desktop Duplication, GDI as a fallback.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace Stackshot
{
    // D3D11/DXGI through raw vtable slots (checked against the Windows SDK interface definitions), so no wrapper library
    // is needed.
    static class Dx
    {
        public static readonly Guid Factory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        public static readonly Guid Output1 = new Guid("00cddea8-939b-4b83-a340-a685226666cc");
        public static readonly Guid Output5 = new Guid("80a07424-ab52-42eb-833c-0c42fd282d98");
        public static readonly Guid Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid Surface1 = new Guid("4ae63092-6327-4c1b-80ae-bfe12ea32b86");
        public static readonly Guid VideoDevice = new Guid("10ec4d5b-975a-4689-b9e4-d0aac30fe333");
        public static readonly Guid VideoContext = new Guid("61f21c45-3c0e-4a74-9cea-67100d9ad5e4");

        public const int WaitTimeout = unchecked((int)0x887A0027), AccessLost = unchecked((int)0x887A0026);
        public const int Bgra = 87, BgraSrgb = 91, Nv12 = 103;

        [DllImport("dxgi.dll")] public static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);
        [DllImport("d3d11.dll")]
        public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr levels, uint count,
                                                   uint sdk, out IntPtr device, out int level, out IntPtr context);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OutputDesc
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public Native.RECT Coords;
            public int Attached, Rotation;
            public IntPtr Monitor;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct DuplDesc
        {
            public uint Width, Height, RefreshNum, RefreshDen;
            public int Format, Scanline, Scaling, Rotation, InSystemMemory;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct FrameInfo
        {
            public long LastPresentTime, LastMouseUpdateTime;
            public uint AccumulatedFrames;
            public int RectsCoalesced, ProtectedContentMaskedOut, PointerX, PointerY, PointerVisible;
            public uint TotalMetadataBufferSize, PointerShapeBufferSize;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct TexDesc
        {
            public uint Width, Height, MipLevels, ArraySize;
            public int Format;
            public uint SampleCount, SampleQuality;
            public int Usage;
            public uint BindFlags, CpuAccess, MiscFlags;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Mapped { public IntPtr Data; public uint RowPitch, DepthPitch; }
        [StructLayout(LayoutKind.Sequential)] public struct MappedRect { public int Pitch; public IntPtr Bits; }
        [StructLayout(LayoutKind.Sequential)] public struct Box { public uint Left, Top, Front, Right, Bottom, Back; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VpContent
        {
            public int InputFrameFormat;
            public uint InRateNum, InRateDen, InputWidth, InputHeight, OutRateNum, OutRateDen, OutputWidth, OutputHeight;
            public int Usage;
        }
        [StructLayout(LayoutKind.Sequential)] public struct VpInView { public uint FourCC; public int Dimension; public uint MipSlice, ArraySlice; }
        [StructLayout(LayoutKind.Sequential)] public struct VpOutView { public int Dimension; public uint MipSlice, Unused1, Unused2; }
        [StructLayout(LayoutKind.Sequential)]
        public struct VpStream
        {
            public int Enable;
            public uint OutputIndex, InputFrameOrField, PastFrames, FutureFrames;
            public IntPtr PastSurfaces, InputSurface, FutureSurfaces, PastSurfacesRight, InputSurfaceRight, FutureSurfacesRight;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int EnumFn(IntPtr self, uint index, out IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int OutputDescFn(IntPtr self, out OutputDesc desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DuplicateFn(IntPtr self, IntPtr device, out IntPtr dupl);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int Duplicate1Fn(IntPtr self, IntPtr device, uint flags, uint count, int[] formats, out IntPtr dupl);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DuplDescFn(IntPtr self, out DuplDesc desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int AcquireFn(IntPtr self, uint timeout, out FrameInfo info, out IntPtr resource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CallFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int MapRectFn(IntPtr self, out MappedRect rect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTexFn(IntPtr self, ref TexDesc desc, IntPtr init, out IntPtr tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GetPtrFn(IntPtr self, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CopyRegionFn(IntPtr self, IntPtr dst, uint dstSub, uint x, uint y, uint z, IntPtr src, uint srcSub, ref Box box);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CopyFn(IntPtr self, IntPtr dst, IntPtr src);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UpdateFn(IntPtr self, IntPtr dst, uint sub, ref Box box, IntPtr data, uint rowPitch, uint depthPitch);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int MapFn(IntPtr self, IntPtr res, uint sub, int type, uint flags, out Mapped map);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UnmapFn(IntPtr self, IntPtr res, uint sub);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetDcFn(IntPtr self, int discard, out IntPtr hdc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ReleaseDcFn(IntPtr self, IntPtr dirty);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int VpEnumFn(IntPtr self, ref VpContent desc, out IntPtr e);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int VpCreateFn(IntPtr self, IntPtr e, uint rateIndex, out IntPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int VpInViewFn(IntPtr self, IntPtr res, IntPtr e, ref VpInView desc, out IntPtr view);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int VpOutViewFn(IntPtr self, IntPtr res, IntPtr e, ref VpOutView desc, out IntPtr view);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void VpStreamIntFn(IntPtr self, IntPtr vp, uint stream, int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void VpStreamSpaceFn(IntPtr self, IntPtr vp, uint stream, ref uint space);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void VpOutSpaceFn(IntPtr self, IntPtr vp, ref uint space);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int VpBltFn(IntPtr self, IntPtr vp, IntPtr view, uint frame, uint count, ref VpStream stream);

        static readonly Dictionary<IntPtr, Delegate> fns = new Dictionary<IntPtr, Delegate>();

        // The delegate for vtable slot i of a COM object, cached by function address.
        public static T Fn<T>(IntPtr obj, int slot) where T : class
        {
            IntPtr f = Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size);
            lock (fns)
            {
                Delegate d;
                if (!fns.TryGetValue(f, out d)) fns[f] = d = Marshal.GetDelegateForFunctionPointer(f, typeof(T));
                return (T)(object)d;
            }
        }

        public static IntPtr Query(IntPtr obj, Guid iid)
        {
            IntPtr r;
            return Marshal.QueryInterface(obj, ref iid, out r) == 0 ? r : IntPtr.Zero;
        }

        public static void Release(ref IntPtr p)
        {
            if (p == IntPtr.Zero) return;
            Marshal.Release(p);
            p = IntPtr.Zero;
        }

        public static void Check(int hr, string what)
        {
            if (hr < 0) throw new ExternalException(what + " 0x" + hr.ToString("X8"), hr);
        }
    }

    // The recorded rectangle, frame by frame. Desktop Duplication hands over each new desktop image straight from the
    // compositor, with the time it was presented: no GDI readback per frame, hardware-accelerated content included and
    // frames that match what was really on screen. The rectangle is assembled on the GPU (one copy per monitor it
    // touches), the cursor is drawn on it through GDI interop, and it is read back as BGRA, or as NV12 after a hardware
    // color conversion (2.7x less data, for large areas). Up to three frames are in flight, so the GPU works on one
    // while the CPU reads an older one and nothing waits. Rotated monitors, areas spanning two graphics adapters or
    // machines without duplication use GDI instead.
    sealed class ScreenSource : IDisposable
    {
        public const int Depth = 3;
        public readonly Rectangle Area;
        public readonly int Width, Height;
        public bool Nv12 { get; private set; }           // NV12 (BT.709, limited range) instead of BGRA
        public bool Gpu { get { return !gdiMode; } }
        public int FrameBytes { get { return Nv12 ? Width * Height * 3 / 2 : Width * Height * 4; } }
        public uint RefreshNum = 60, RefreshDen = 1;     // refresh rate of the main monitor of the area
        public string Description = "GDI";
        public int Pending { get { return submitted - read; } }
        public long Presents;                            // new images of the main monitor taken
        public long LastPresent;                         // QPC time of the main monitor's latest image
        public bool Broken { get { return broken; } }

        // The last images, each with the time it was presented: one thread takes them as they come (Wait/Take) and
        // never waits for anything else, while the frame thread picks, for each video frame, the image that was on
        // screen at its moment (Pick). The D3D11 context is shared by both, behind `gpu`.
        const int Versions = 6;
        readonly IntPtr[] scenes = new IntPtr[Versions];
        readonly long[] presentOf = new long[Versions];
        int versions;
        readonly object gpu = new object();
        readonly Queue<long> newPresents = new Queue<long>();
        volatile bool broken;

        class Output
        {
            public IntPtr Out1, Dupl, Frame;
            public Rectangle Desktop, Part;
            public bool Held, SystemMemory;
            public long Present, RetryAt;
        }
        class Slot
        {
            public IntPtr Stamp, Surface, Nv12, InView, OutView, Staging;
            public Dib Gdi;
            public bool Valid;
            public Native.CURSORINFO Cursor;   // drawn on the CPU after reading (BGRA)
        }
        readonly List<Output> outputs = new List<Output>();
        readonly Slot[] ring = new Slot[Depth];
        Output primary;
        IntPtr device, context, videoDevice, videoContext, vpEnum, vp;
        int submitted, read;
        volatile bool gdiMode;
        bool warned;
        readonly bool layered;

        ScreenSource(Rectangle area, bool layered)
        {
            Area = area;
            Width = area.Width;
            Height = area.Height;
            this.layered = layered;
            for (int i = 0; i < Depth; i++) ring[i] = new Slot();
        }

        // wantNv12: deliver NV12 when the GPU can convert it. layered: the GDI fallback must include layered windows
        // (the camera bubble).
        public static ScreenSource Open(Rectangle area, bool wantNv12, bool layered)
        {
            ScreenSource s = new ScreenSource(area, layered);
            string why;
            try { why = s.InitGpu(wantNv12); }
            catch (Exception ex) { why = ex.Message; }
            if (why != null)
            {
                s.ToGdi();
                s.Description = "GDI (" + why + ")";
            }
            return s;
        }

        void ToGdi()
        {
            ReleaseGpu();
            Nv12 = false;
            gdiMode = true;
            foreach (Slot sl in ring) if (sl.Gdi == null) sl.Gdi = new Dib(Width, Height);
        }

        string InitGpu(bool wantNv12)
        {
            IntPtr factory, adapter = IntPtr.Zero;
            Guid fg = Dx.Factory1;
            Dx.Check(Dx.CreateDXGIFactory1(ref fg, out factory), "CreateDXGIFactory1");
            try
            {
                for (uint a = 0; ; a++)
                {
                    IntPtr ad;
                    if (Dx.Fn<Dx.EnumFn>(factory, 12)(factory, a, out ad) < 0) break; // EnumAdapters1
                    bool used = false;
                    for (uint o = 0; ; o++)
                    {
                        IntPtr output;
                        if (Dx.Fn<Dx.EnumFn>(ad, 7)(ad, o, out output) < 0) break; // EnumOutputs
                        Dx.OutputDesc d;
                        Dx.Fn<Dx.OutputDescFn>(output, 7)(output, out d);
                        Rectangle desk = Rectangle.FromLTRB(d.Coords.Left, d.Coords.Top, d.Coords.Right, d.Coords.Bottom);
                        Rectangle part = Rectangle.Intersect(desk, Area);
                        if (d.Attached != 0 && part.Width > 0 && part.Height > 0)
                        {
                            Output op = new Output();
                            op.Out1 = Dx.Query(output, Dx.Output1);
                            op.Desktop = desk;
                            op.Part = part;
                            outputs.Add(op);
                            used = true;
                        }
                        Dx.Release(ref output);
                    }
                    if (!used) { Dx.Release(ref ad); continue; }
                    if (adapter != IntPtr.Zero) { Dx.Release(ref ad); return "monitores en dos tarjetas gr\u00E1ficas"; }
                    adapter = ad;
                }
                if (outputs.Count == 0 || adapter == IntPtr.Zero) return "sin monitores";
                foreach (Output o in outputs) if (o.Out1 == IntPtr.Zero) return "DXGI 1.2 no disponible";
                int level;
                // VIDEO_SUPPORT enables the color converter; some drivers refuse it, so try without.
                int hr = Dx.D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0x20 | 0x800, IntPtr.Zero, 0, 7, out device, out level, out context);
                if (hr < 0) hr = Dx.D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out device, out level, out context);
                Dx.Check(hr, "D3D11CreateDevice");
            }
            finally
            {
                Dx.Release(ref adapter);
                Dx.Release(ref factory);
            }
            foreach (Output o in outputs)
            {
                string e = Duplicate(o);
                if (e != null) return e;
                if (primary == null || o.Part.Width * o.Part.Height > primary.Part.Width * primary.Part.Height) primary = o;
            }
            Dx.DuplDesc dd;
            Dx.Fn<Dx.DuplDescFn>(primary.Dupl, 7)(primary.Dupl, out dd);
            if (dd.RefreshNum > 0 && dd.RefreshDen > 0) { RefreshNum = dd.RefreshNum; RefreshDen = dd.RefreshDen; }

            for (int i = 0; i < Versions; i++) scenes[i] = Texture(Dx.Bgra, 0, 0x8, 0, 0); // DEFAULT, SHADER_RESOURCE
            if (wantNv12 && Width % 2 == 0 && Height % 2 == 0) Nv12 = InitConverter();
            // NV12 needs the cursor on the GPU (through GDI interop) before converting; BGRA gets it on the CPU after
            // reading, which spares the GPU two full-frame copies and a sync per frame.
            if (Nv12)
                foreach (Slot sl in ring)
                {
                    sl.Stamp = Texture(Dx.Bgra, 0, 0x20, 0, 0x200);         // DEFAULT, RENDER_TARGET, GDI_COMPATIBLE
                    sl.Surface = Dx.Query(sl.Stamp, Dx.Surface1);
                    if (sl.Surface == IntPtr.Zero || !SlotConverter(sl)) { Nv12 = false; ReleaseSlotConverters(); break; }
                }
            foreach (Slot sl in ring) sl.Staging = Texture(Nv12 ? Dx.Nv12 : Dx.Bgra, 3, 0, 0x20000, 0); // STAGING, CPU read

            // The first frame of a new duplication always carries the whole desktop.
            foreach (Output o in outputs) Poll(o, 500);
            Take();
            Description = "DXGI" + (outputs.Count > 1 ? " x" + outputs.Count : "") + (Nv12 ? " + NV12" : "") + ", " +
                          (RefreshNum / (double)RefreshDen).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " Hz";
            return null;
        }

        string Duplicate(Output o)
        {
            IntPtr out5 = Dx.Query(o.Out1, Dx.Output5), dupl;
            int hr;
            // DuplicateOutput1 asking for 8-bit BGRA also works on HDR desktops (Windows converts).
            if (out5 != IntPtr.Zero)
            {
                hr = Dx.Fn<Dx.Duplicate1Fn>(out5, 26)(out5, device, 0, 1, new int[] { Dx.Bgra }, out dupl);
                Dx.Release(ref out5);
            }
            else hr = Dx.Fn<Dx.DuplicateFn>(o.Out1, 22)(o.Out1, device, out dupl);
            if (hr < 0) return "DuplicateOutput 0x" + hr.ToString("X8");
            Dx.DuplDesc dd;
            Dx.Fn<Dx.DuplDescFn>(dupl, 7)(dupl, out dd);
            if (dd.Rotation > 1) { Dx.Release(ref dupl); return "monitor girado"; }
            if (dd.Format != Dx.Bgra && dd.Format != Dx.BgraSrgb) { Dx.Release(ref dupl); return "formato " + dd.Format; }
            o.SystemMemory = dd.InSystemMemory != 0;
            o.Dupl = dupl;
            return null;
        }

        IntPtr Texture(int format, int usage, uint bind, uint cpu, uint misc)
        {
            Dx.TexDesc d = new Dx.TexDesc();
            d.Width = (uint)Width; d.Height = (uint)Height; d.MipLevels = 1; d.ArraySize = 1; d.Format = format; d.SampleCount = 1;
            d.Usage = usage; d.BindFlags = bind; d.CpuAccess = cpu; d.MiscFlags = misc;
            IntPtr t;
            Dx.Check(Dx.Fn<Dx.CreateTexFn>(device, 5)(device, ref d, IntPtr.Zero, out t), "CreateTexture2D");
            return t;
        }

        // BGRA -> NV12 on the GPU's video processor: BT.709 matrix, limited range, no "enhancements".
        bool InitConverter()
        {
            try
            {
                videoDevice = Dx.Query(device, Dx.VideoDevice);
                videoContext = Dx.Query(context, Dx.VideoContext);
                if (videoDevice == IntPtr.Zero || videoContext == IntPtr.Zero) { ReleaseConverter(); return false; }
                Dx.VpContent cd = new Dx.VpContent();
                cd.InRateNum = cd.OutRateNum = 60; cd.InRateDen = cd.OutRateDen = 1;
                cd.InputWidth = cd.OutputWidth = (uint)Width;
                cd.InputHeight = cd.OutputHeight = (uint)Height;
                Dx.Check(Dx.Fn<Dx.VpEnumFn>(videoDevice, 10)(videoDevice, ref cd, out vpEnum), "CreateVideoProcessorEnumerator");
                Dx.Check(Dx.Fn<Dx.VpCreateFn>(videoDevice, 4)(videoDevice, vpEnum, 0, out vp), "CreateVideoProcessor");
                Dx.Fn<Dx.VpStreamIntFn>(videoContext, 27)(videoContext, vp, 0, 0);       // progressive
                uint rgbFull = 0, yuv709Limited = (1u << 2) | (1u << 4);
                Dx.Fn<Dx.VpStreamSpaceFn>(videoContext, 28)(videoContext, vp, 0, ref rgbFull);
                Dx.Fn<Dx.VpOutSpaceFn>(videoContext, 15)(videoContext, vp, ref yuv709Limited);
                Dx.Fn<Dx.VpStreamIntFn>(videoContext, 37)(videoContext, vp, 0, 0);       // auto processing off
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Grabaci\u00F3n: sin conversi\u00F3n NV12 por GPU (" + ex.Message + ")");
                ReleaseConverter();
                return false;
            }
        }

        bool SlotConverter(Slot sl)
        {
            try
            {
                sl.Nv12 = Texture(Dx.Nv12, 0, 0x20, 0, 0);
                Dx.VpInView iv = new Dx.VpInView();
                iv.Dimension = 1;
                Dx.Check(Dx.Fn<Dx.VpInViewFn>(videoDevice, 8)(videoDevice, sl.Stamp, vpEnum, ref iv, out sl.InView), "CreateVideoProcessorInputView");
                Dx.VpOutView ov = new Dx.VpOutView();
                ov.Dimension = 1;
                Dx.Check(Dx.Fn<Dx.VpOutViewFn>(videoDevice, 9)(videoDevice, sl.Nv12, vpEnum, ref ov, out sl.OutView), "CreateVideoProcessorOutputView");
                return true;
            }
            catch (Exception ex)
            {
                ShotStack.Log("Grabaci\u00F3n: sin conversi\u00F3n NV12 por GPU (" + ex.Message + ")");
                return false;
            }
        }

        // Waits up to timeoutMs for new desktop content in any monitor of the area; Take() then stores it as a new
        // image. present is the QPC time the main monitor showed it (0 if only other monitors changed).
        public bool Wait(int timeoutMs, out long present)
        {
            present = 0;
            if (gdiMode || broken || device == IntPtr.Zero)
            {
                if (timeoutMs > 0) Thread.Sleep(timeoutMs);
                return false;
            }
            bool any = false;
            foreach (Output o in outputs) if (o != primary && Poll(o, 0)) any = true;
            long until = Stopwatch.GetTimestamp() + (long)(Math.Max(0, timeoutMs) * (Stopwatch.Frequency / 1000.0));
            while (!primary.Held)
            {
                int left = any ? 0 : (int)Math.Max(0, (until - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency);
                if (Poll(primary, left) || left == 0) break;
            }
            if (primary.Held) present = primary.Present;
            return any || primary.Held;
        }

        bool Poll(Output o, int ms)
        {
            if (o.Held) return true;
            if (o.Dupl == IntPtr.Zero)
            {
                // Lost (another desktop such as UAC or the lock screen, a mode change...): try again now and then.
                long now = Stopwatch.GetTimestamp();
                if (now < o.RetryAt) { if (ms > 0) Thread.Sleep(Math.Min(ms, 50)); return false; }
                o.RetryAt = now + Stopwatch.Frequency / 4;
                if (Duplicate(o) != null) return false;
            }
            Dx.FrameInfo info;
            IntPtr res;
            int hr = Dx.Fn<Dx.AcquireFn>(o.Dupl, 8)(o.Dupl, (uint)ms, out info, out res);
            if (hr == Dx.WaitTimeout) return false;
            if (hr < 0)
            {
                if (!warned) { warned = true; ShotStack.Log("Grabaci\u00F3n: duplicaci\u00F3n interrumpida 0x" + hr.ToString("X8")); }
                Dx.Release(ref o.Dupl);
                o.RetryAt = 0;
                return false;
            }
            if (info.LastPresentTime == 0 || info.AccumulatedFrames == 0)
            {
                // Only the mouse moved: the cursor is drawn separately.
                Dx.Release(ref res);
                Dx.Fn<Dx.CallFn>(o.Dupl, 14)(o.Dupl); // ReleaseFrame
                return false;
            }
            o.Frame = Dx.Query(res, Dx.Texture2D);
            Dx.Release(ref res);
            if (o == primary) { Presents++; LastPresent = info.LastPresentTime; }
            o.Present = info.LastPresentTime;
            o.Held = true;
            return true;
        }

        // Stores the acquired frames as a new image (the previous one plus what changed).
        public void Take()
        {
            if (gdiMode || broken) return;
            lock (gpu)
            {
                int next = versions % Versions;
                IntPtr scene = scenes[next];
                if (versions > 0) Dx.Fn<Dx.CopyFn>(context, 47)(context, scene, scenes[(versions - 1) % Versions]);
                bool main = primary.Held;
                long present = main ? primary.Present : Stopwatch.GetTimestamp();
                Copy(scene);
                presentOf[next] = present;
                versions++;
                if (main) lock (newPresents) { newPresents.Enqueue(present); if (newPresents.Count > 600) newPresents.Dequeue(); }
            }
        }

        // Times of the main monitor's new images since the last call (for the frame thread's timing).
        public List<long> NewPresents()
        {
            lock (newPresents)
            {
                List<long> l = new List<long>(newPresents);
                newPresents.Clear();
                return l;
            }
        }

        // The image that was on screen at QPC time `at`: the newest one presented before it.
        public int Pick(double at)
        {
            lock (gpu)
            {
                int first = Math.Max(0, versions - Versions + 1), best = Math.Max(0, versions - 1);
                for (int v = versions - 1; v >= first; v--)
                {
                    best = v;
                    if (presentOf[v % Versions] <= at) break;
                }
                return best;
            }
        }

        void Copy(IntPtr scene)
        {
            foreach (Output o in outputs)
            {
                if (!o.Held) continue;
                Dx.Box src = new Dx.Box();
                src.Left = (uint)(o.Part.X - o.Desktop.X); src.Top = (uint)(o.Part.Y - o.Desktop.Y);
                src.Right = src.Left + (uint)o.Part.Width; src.Bottom = src.Top + (uint)o.Part.Height; src.Back = 1;
                uint dx = (uint)(o.Part.X - Area.X), dy = (uint)(o.Part.Y - Area.Y);
                if (o.SystemMemory)
                {
                    Dx.MappedRect mr;
                    if (Dx.Fn<Dx.MapRectFn>(o.Dupl, 12)(o.Dupl, out mr) >= 0)
                    {
                        Dx.Box dst = new Dx.Box();
                        dst.Left = dx; dst.Top = dy; dst.Right = dx + (uint)o.Part.Width; dst.Bottom = dy + (uint)o.Part.Height; dst.Back = 1;
                        IntPtr first = new IntPtr(mr.Bits.ToInt64() + (long)src.Top * mr.Pitch + src.Left * 4);
                        Dx.Fn<Dx.UpdateFn>(context, 48)(context, scene, 0, ref dst, first, (uint)mr.Pitch, 0);
                        Dx.Fn<Dx.CallFn>(o.Dupl, 13)(o.Dupl); // UnMapDesktopSurface
                    }
                }
                else if (o.Frame != IntPtr.Zero) Dx.Fn<Dx.CopyRegionFn>(context, 46)(context, scene, 0, dx, dy, 0, o.Frame, 0, ref src);
                Dx.Release(ref o.Frame);
                Dx.Fn<Dx.CallFn>(o.Dupl, 14)(o.Dupl);
                o.Held = false;
            }
        }

        // Starts the next frame: image `version` (from Pick) plus the cursor, converted if needed. The caller reads it
        // with Read() later (in order); before more than Depth frames are pending, it must read the oldest. cursor: where
        // the cursor was when that image was on screen (GDI captures right now, so it uses the current one).
        public void Submit(Native.CURSORINFO cursor, int version)
        {
            Slot sl = ring[submitted % Depth];
            submitted++;
            sl.Valid = false;
            if (gdiMode) { SubmitGdi(sl); return; }
            lock (gpu) SubmitGpu(sl, cursor, version);
        }

        void SubmitGdi(Slot sl)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            try { Native.BitBlt(sl.Gdi.Dc, 0, 0, Width, Height, screen, Area.X, Area.Y, Native.SRCCOPY | (layered ? Native.CAPTUREBLT : 0)); }
            finally { Native.ReleaseDC(IntPtr.Zero, screen); }
            Grabber.DrawCursor(sl.Gdi.Dc, Area.X, Area.Y);
            Native.GdiFlush();
            sl.Valid = true;
        }

        void SubmitGpu(Slot sl, Native.CURSORINFO cursor, int version)
        {
            if (broken || device == IntPtr.Zero) return;
            // The image may have been replaced meanwhile only if this thread fell far behind: then the oldest kept.
            version = Math.Max(version, Math.Max(0, versions - Versions));
            if (!Nv12)
            {
                Dx.Fn<Dx.CopyFn>(context, 47)(context, sl.Staging, scenes[version % Versions]); // CopyResource
                Dx.Fn<Dx.CallFn>(context, 111)(context); // Flush: start the GPU now
                sl.Cursor = cursor;
                sl.Valid = true;
                return;
            }
            Dx.Fn<Dx.CopyFn>(context, 47)(context, sl.Stamp, scenes[version % Versions]);
            IntPtr hdc;
            if (Dx.Fn<Dx.GetDcFn>(sl.Surface, 11)(sl.Surface, 0, out hdc) >= 0)
            {
                Grabber.DrawCursor(hdc, Area.X, Area.Y, cursor);
                Dx.Fn<Dx.ReleaseDcFn>(sl.Surface, 12)(sl.Surface, IntPtr.Zero);
            }
            if (Nv12)
            {
                Dx.VpStream st = new Dx.VpStream();
                st.Enable = 1;
                st.InputSurface = sl.InView;
                int hr = Dx.Fn<Dx.VpBltFn>(videoContext, 53)(videoContext, vp, sl.OutView, 0, 1, ref st);
                if (hr < 0) { Lost("VideoProcessorBlt 0x" + hr.ToString("X8")); return; }
                Dx.Fn<Dx.CopyFn>(context, 47)(context, sl.Staging, sl.Nv12);
            }
            else Dx.Fn<Dx.CopyFn>(context, 47)(context, sl.Staging, sl.Stamp);
            Dx.Fn<Dx.CallFn>(context, 111)(context); // Flush: start the GPU now
            sl.Valid = true;
        }

        // Writes the oldest submitted frame into dst (FrameBytes). False when it could not be produced (the caller repeats
        // the previous frame).
        public bool Read(IntPtr dst)
        {
            if (read >= submitted) return false;
            Slot sl = ring[read % Depth];
            read++;
            if (!sl.Valid) return false;
            sl.Valid = false;
            if (sl.Gdi != null && gdiMode)
            {
                Native.CopyMemory(dst, sl.Gdi.Bits, new UIntPtr((ulong)Width * (ulong)Height * 4));
                return true;
            }
            Dx.Mapped m;
            lock (gpu)
            {
                if (broken || device == IntPtr.Zero) return false;
                int hr = Dx.Fn<Dx.MapFn>(context, 14)(context, sl.Staging, 0, 1, 0, out m); // D3D11_MAP_READ
                if (hr < 0) { Lost("Map 0x" + hr.ToString("X8")); return false; }
            }
            // The copy runs outside the lock, so taking new images never waits for it.
            try
            {
                int row = Nv12 ? Width : Width * 4, rows = Nv12 ? Height + Height / 2 : Height;
                if (m.RowPitch == row) Native.CopyMemory(dst, m.Data, new UIntPtr((ulong)row * (ulong)rows));
                else
                {
                    // NV12: the chroma plane starts right after Height rows of luma.
                    for (int y = 0; y < rows; y++)
                        Native.CopyMemory(new IntPtr(dst.ToInt64() + (long)y * row), new IntPtr(m.Data.ToInt64() + (long)y * m.RowPitch), new UIntPtr((uint)row));
                }
            }
            finally { lock (gpu) Dx.Fn<Dx.UnmapFn>(context, 15)(context, sl.Staging, 0); }
            if (!Nv12) DrawCursor(dst, sl.Cursor);
            return true;
        }

        // The cursor on a BGRA frame in memory: only the patch under it goes through a small DIB, where GDI draws it
        // exactly as on screen (alpha, inverting I-beam...).
        Dib patch;
        IntPtr shapeOf;
        int curW, curH, hotX, hotY;

        void DrawCursor(IntPtr frame, Native.CURSORINFO ci)
        {
            if ((ci.flags & 1) == 0 || ci.hCursor == IntPtr.Zero) return; // CURSOR_SHOWING
            if (ci.hCursor != shapeOf && !Measure(ci.hCursor)) return;
            int x = ci.ptScreenPos.X - Area.X - hotX, y = ci.ptScreenPos.Y - Area.Y - hotY;
            Rectangle r = Rectangle.Intersect(new Rectangle(x, y, curW, curH), new Rectangle(0, 0, Width, Height));
            if (r.Width <= 0 || r.Height <= 0) return;
            if (patch == null || patch.Width < curW || patch.Height < curH)
            {
                if (patch != null) patch.Dispose();
                patch = new Dib(Math.Max(curW, 64), Math.Max(curH, 64));
            }
            int stride = Width * 4, row = r.Width * 4, ox = r.X - x, oy = r.Y - y;
            for (int i = 0; i < r.Height; i++)
                Native.CopyMemory(new IntPtr(patch.Bits.ToInt64() + (long)(oy + i) * patch.Width * 4 + ox * 4),
                                  new IntPtr(frame.ToInt64() + (long)(r.Y + i) * stride + r.X * 4), new UIntPtr((uint)row));
            Native.DrawIconEx(patch.Dc, 0, 0, ci.hCursor, 0, 0, 0, IntPtr.Zero, 3); // DI_NORMAL
            Native.GdiFlush();
            for (int i = 0; i < r.Height; i++)
                Native.CopyMemory(new IntPtr(frame.ToInt64() + (long)(r.Y + i) * stride + r.X * 4),
                                  new IntPtr(patch.Bits.ToInt64() + (long)(oy + i) * patch.Width * 4 + ox * 4), new UIntPtr((uint)row));
        }

        // Size and hot spot of a cursor shape (cached until the shape changes).
        bool Measure(IntPtr cursor)
        {
            Native.ICONINFO ii;
            if (!Native.GetIconInfo(cursor, out ii)) return false;
            try
            {
                Native.BITMAP bm;
                IntPtr src = ii.hbmColor != IntPtr.Zero ? ii.hbmColor : ii.hbmMask;
                if (Native.GetObject(src, Marshal.SizeOf(typeof(Native.BITMAP)), out bm) == 0) return false;
                curW = bm.bmWidth;
                curH = ii.hbmColor != IntPtr.Zero ? bm.bmHeight : bm.bmHeight / 2; // a monochrome mask holds AND and XOR halves
                hotX = ii.xHotspot;
                hotY = ii.yHotspot;
                shapeOf = cursor;
                return curW > 0 && curH > 0;
            }
            finally
            {
                if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero) Native.DeleteObject(ii.hbmColor);
            }
        }

        // The GPU is gone (driver update or crash): BGRA continues through GDI; NV12 repeats the last frame. Nothing is
        // released here (the other thread may still be using it); Dispose does that.
        void Lost(string what)
        {
            ShotStack.Log("Grabaci\u00F3n: GPU perdida (" + what + ")");
            broken = true;
            foreach (Slot sl in ring) sl.Valid = false;
            if (Nv12) return;
            foreach (Slot sl in ring) if (sl.Gdi == null) sl.Gdi = new Dib(Width, Height);
            gdiMode = true;
            Description = "GDI (GPU perdida)";
        }

        void ReleaseConverter()
        {
            Dx.Release(ref vp); Dx.Release(ref vpEnum); Dx.Release(ref videoContext); Dx.Release(ref videoDevice);
        }

        void ReleaseSlotConverters()
        {
            foreach (Slot sl in ring) { Dx.Release(ref sl.InView); Dx.Release(ref sl.OutView); Dx.Release(ref sl.Nv12); }
            ReleaseConverter();
        }

        void ReleaseGpu()
        {
            foreach (Output o in outputs)
            {
                if (o.Held && o.Dupl != IntPtr.Zero) { Dx.Release(ref o.Frame); Dx.Fn<Dx.CallFn>(o.Dupl, 14)(o.Dupl); }
                o.Held = false;
                Dx.Release(ref o.Dupl);
                Dx.Release(ref o.Out1);
            }
            outputs.Clear();
            primary = null;
            ReleaseSlotConverters();
            foreach (Slot sl in ring) { sl.Valid = sl.Gdi != null && sl.Valid; Dx.Release(ref sl.Surface); Dx.Release(ref sl.Stamp); Dx.Release(ref sl.Staging); }
            for (int i = 0; i < Versions; i++) Dx.Release(ref scenes[i]);
            Dx.Release(ref context);
            Dx.Release(ref device);
        }

        public void Dispose()
        {
            if (patch != null) { patch.Dispose(); patch = null; }
            ReleaseGpu();
            foreach (Slot sl in ring) if (sl.Gdi != null) { sl.Gdi.Dispose(); sl.Gdi = null; }
        }
    }
}
