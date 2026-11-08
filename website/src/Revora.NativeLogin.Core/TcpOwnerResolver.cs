using System.Net;
using System.Runtime.InteropServices;

namespace Revora.NativeLogin.Core;

public interface ITcpOwnerResolver
{
    int? Resolve(IPEndPoint clientEndpoint, IPEndPoint serverEndpoint);
}

public sealed class WindowsTcpOwnerResolver : ITcpOwnerResolver
{
    public int? Resolve(IPEndPoint clientEndpoint, IPEndPoint serverEndpoint)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("TCP process ownership is resolved only on Windows.");
        int size = 0;
        uint result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 5, 0);
        if (result != 122 || size is < 4 or > 16 * 1024 * 1024) throw new InvalidOperationException("TCP owner table sizing failed.");
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(buffer, ref size, false, 2, 5, 0);
            if (result != 0) throw new InvalidOperationException("TCP owner table query failed.");
            int count = Marshal.ReadInt32(buffer);
            if (count is < 0 or > 1_000_000 || 4L + count * 24L > size) throw new InvalidDataException("TCP owner table was malformed.");
            uint clientAddress = ToUInt32(clientEndpoint.Address);
            uint serverAddress = ToUInt32(serverEndpoint.Address);
            for (int index = 0; index < count; index++)
            {
                IntPtr row = buffer + 4 + index * 24;
                if (Marshal.ReadInt32(row) != 5) continue;
                uint localAddress = unchecked((uint)Marshal.ReadInt32(row, 4));
                int localPort = DecodePort(unchecked((uint)Marshal.ReadInt32(row, 8)));
                uint remoteAddress = unchecked((uint)Marshal.ReadInt32(row, 12));
                int remotePort = DecodePort(unchecked((uint)Marshal.ReadInt32(row, 16)));
                if (localAddress == clientAddress && localPort == clientEndpoint.Port && remoteAddress == serverAddress && remotePort == serverEndpoint.Port)
                    return Marshal.ReadInt32(row, 20);
            }
            return null;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static int DecodePort(uint value) => (int)(((value & 0xff) << 8) | ((value >> 8) & 0xff));
    private static uint ToUInt32(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != 4) throw new ArgumentException("Only loopback IPv4 is supported.", nameof(address));
        return BitConverter.ToUInt32(bytes);
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
}
