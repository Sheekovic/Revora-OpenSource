using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const uint SnapModule = 0x00000008;
    private const uint SnapModule32 = 0x00000010;
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);
    private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "d3d9.dll", "d3d9on12.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll" };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ModuleEntry32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Module32FirstW(IntPtr snapshot, ref ModuleEntry32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Module32NextW(IntPtr snapshot, ref ModuleEntry32 entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static int Main(string[] args)
    {
        uint pid;
        if (args.Length != 2 || args[0] != "--pid" || !uint.TryParse(args[1], out pid) || pid == 0)
        {
            Write("InvalidArguments", 0, new List<string>());
            return 2;
        }

        IntPtr snapshot = CreateToolhelp32Snapshot(SnapModule | SnapModule32, pid);
        if (snapshot == InvalidHandle)
        {
            int error = Marshal.GetLastWin32Error();
            Write(Classify(error), error, new List<string>());
            return 3;
        }

        try
        {
            var paths = new List<string>();
            var entry = new ModuleEntry32 { dwSize = (uint)Marshal.SizeOf(typeof(ModuleEntry32)) };
            if (!Module32FirstW(snapshot, ref entry))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 18) { Write("Observed", 0, paths); return 0; }
                Write(Classify(error), error, paths);
                return 4;
            }

            do
            {
                if (Names.Contains(entry.szModule) && !string.IsNullOrEmpty(entry.szExePath)) paths.Add(entry.szExePath);
                entry.dwSize = (uint)Marshal.SizeOf(typeof(ModuleEntry32));
            } while (Module32NextW(snapshot, ref entry));

            int finalError = Marshal.GetLastWin32Error();
            if (finalError != 0 && finalError != 18)
            {
                Write(Classify(finalError), finalError, paths);
                return 5;
            }

            Write("Observed", 0, paths);
            return 0;
        }
        finally { CloseHandle(snapshot); }
    }

    private static string Classify(int error)
    {
        if (error == 5) return "AccessDenied";
        if (error == 299) return "CrossArchitecturePartialCopy";
        if (error == 87 || error == 1168) return "ProcessUnavailable";
        return "SnapshotFailed";
    }

    private static void Write(string status, int error, IList<string> paths)
    {
        var json = new StringBuilder();
        json.Append("{\"status\":\"").Append(status).Append("\",\"win32Error\":").Append(error).Append(",\"modules\":[");
        for (int i = 0; i < paths.Count; i++)
        {
            if (i != 0) json.Append(',');
            json.Append("{\"path\":\"").Append(Escape(paths[i])).Append("\"}");
        }
        json.Append("]}");
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(json.ToString());
    }

    private static string Escape(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        foreach (char ch in value)
        {
            if (ch == '\\') result.Append("\\\\");
            else if (ch == '"') result.Append("\\\"");
            else if (ch < 0x20) result.Append("\\u").Append(((int)ch).ToString("x4"));
            else result.Append(ch);
        }
        return result.ToString();
    }
}
