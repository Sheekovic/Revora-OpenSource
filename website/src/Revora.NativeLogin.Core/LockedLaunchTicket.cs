using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Revora.NativeLogin.Core;

public sealed class LockedLaunchTicket : IDisposable
{
    private readonly byte[] bytes;
    private GCHandle handle;
    private bool locked;
    private bool disposed;

    public LockedLaunchTicket(ReadOnlySpan<byte> ticket)
    {
        if (ticket.Length != 32) throw new ArgumentException("A launch ticket must be 32 bytes.", nameof(ticket));
        bytes = ticket.ToArray();
        handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        locked = OperatingSystem.IsWindows() && VirtualLock(handle.AddrOfPinnedObject(), (nuint)bytes.Length);
    }

    public bool IsPageLocked => locked;
    public ReadOnlyMemory<byte> Value => disposed ? throw new ObjectDisposedException(nameof(LockedLaunchTicket)) : bytes;

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(bytes);
        if (locked) { _ = VirtualUnlock(handle.AddrOfPinnedObject(), (nuint)bytes.Length); locked = false; }
        if (handle.IsAllocated) handle.Free();
        disposed = true;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualLock(IntPtr address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualUnlock(IntPtr address, nuint size);
}
