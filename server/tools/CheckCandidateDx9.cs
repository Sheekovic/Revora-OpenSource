// Bounded local rendering preflight. No client launch, hooks or credentials.
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class CheckCandidateDx9
{
    [DllImport("d3d9.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr Direct3DCreate9(uint version);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Count(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Create(IntPtr self, uint adapter, uint type, IntPtr window,
        uint flags, ref Present parameters, out IntPtr device);
    [StructLayout(LayoutKind.Sequential)]
    private struct Present
    {
        public uint Width, Height, Format, Count, MultiSample, Quality, SwapEffect;
        public IntPtr Window;
        public int Windowed, AutoDepth;
        public uint DepthFormat, Flags, Refresh, Interval;
    }
    private static T Method<T>(IntPtr instance, int slot) where T : class
    {
        IntPtr function = Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer(function, typeof(T)) as T;
    }
    [STAThread]
    private static int Main()
    {
        if (IntPtr.Size != 4) { Console.WriteLine("Expected x86 probe."); return 2; }
        IntPtr d3d = Direct3DCreate9(32);
        if (d3d == IntPtr.Zero) { Console.WriteLine("Direct3DCreate9 returned null."); return 1; }
        try
        {
            uint count = Method<Count>(d3d, 4)(d3d);
            Console.WriteLine("DX9 adapters: " + count);
            if (count == 0) return 1;
            // Create a hidden HWND in this session; do not change the display mode.
            using (Form window = new Form())
            {
                Present parameters = new Present { Width = 64, Height = 64,
                    Count = 1, SwapEffect = 1, Window = window.Handle, Windowed = 1 };
                IntPtr device;
                // HAL device, software vertex processing: a modest native DX9 prerequisite.
                int result = Method<Create>(d3d, 16)(d3d, 0, 1, window.Handle,
                    0x20, ref parameters, out device);
                Console.WriteLine("DX9 windowed HAL CreateDevice HRESULT: 0x" + result.ToString("X8"));
                if (device != IntPtr.Zero) Method<Release>(device, 2)(device);
                Console.WriteLine("This preflight does not validate the game client or gameplay.");
                return result >= 0 ? 0 : 1;
            }
        }
        finally { Method<Release>(d3d, 2)(d3d); }
    }
}
