using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Revora.NativeLogin.Core;

public sealed record BootstrapWindowEvidence(IntPtr WindowHandle, bool ClientWindowObserved, bool BootstrapMessageAttempted, bool BootstrapMessagePosted);

/// <summary>Posts the exact statically identified Account-start message once to the launcher-owned client window.</summary>
public sealed class AccountBootstrapMessage5165
{
    public const uint AccountStartMessage = 0x480;
    private int attempted;
    private int windowObserved;
    private int messageAttempted;
    private int messagePosted;

    public bool ClientWindowObserved => Volatile.Read(ref windowObserved) != 0;
    public bool BootstrapMessageAttempted => Volatile.Read(ref messageAttempted) != 0;
    public bool BootstrapMessagePosted => Volatile.Read(ref messagePosted) != 0;

    public async Task<BootstrapWindowEvidence> WaitAndPostOnceAsync(Process client, DateTimeOffset ticketExpiresAt, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The diagnostic message boundary is Windows-only.");
        if (Interlocked.Exchange(ref attempted, 1) != 0) throw new InvalidOperationException("The bootstrap message may be attempted only once.");

        EnsureEqualIntegrity(client);
        IntPtr stableWindow = IntPtr.Zero;
        DateTimeOffset stableSince = DateTimeOffset.MinValue;
        DateTimeOffset absoluteDeadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < absoluteDeadline && DateTimeOffset.UtcNow < ticketExpiresAt)
        {
            token.ThrowIfCancellationRequested();
            if (client.HasExited) throw new InvalidOperationException("The reviewed client exited before its window became ready.");
            client.Refresh();
            IntPtr current = client.MainWindowHandle;
            bool valid = current != IntPtr.Zero && IsWindowVisible(current) && OwnsWindow(current, client.Id) && IsResponsive(current);
            if (valid) Interlocked.Exchange(ref windowObserved, 1);
            if (!valid || current != stableWindow)
            {
                stableWindow = valid ? current : IntPtr.Zero;
                stableSince = valid ? DateTimeOffset.UtcNow : DateTimeOffset.MinValue;
            }
            else if (DateTimeOffset.UtcNow - stableSince >= TimeSpan.FromSeconds(2))
            {
                // Ensure the visible window has completed at least one later queue iteration.
                await Task.Delay(250, token);
                client.Refresh();
                IntPtr final = client.MainWindowHandle;
                if (client.HasExited || final != stableWindow || !IsWindowVisible(final) || !OwnsWindow(final, client.Id) || !IsResponsive(final) || DateTimeOffset.UtcNow >= ticketExpiresAt)
                    throw new InvalidOperationException("The exact client window lost its validated state before bootstrap.");
                Interlocked.Exchange(ref messageAttempted, 1);
                bool posted = PostMessage(final, AccountStartMessage, UIntPtr.Zero, IntPtr.Zero);
                if (!posted) throw new Win32Exception(Marshal.GetLastWin32Error(), "The bounded bootstrap message could not be queued.");
                Interlocked.Exchange(ref messagePosted, 1);
                return new(final, true, true, true);
            }
            await Task.Delay(100, token);
        }
        throw new TimeoutException("The exact client window did not reach the bounded stable state before ticket expiry.");
    }

    private static bool OwnsWindow(IntPtr window, int processId)
    {
        _ = GetWindowThreadProcessId(window, out uint owner);
        return owner == (uint)processId;
    }

    private static bool IsResponsive(IntPtr window) =>
        SendMessageTimeout(window, 0, UIntPtr.Zero, IntPtr.Zero, 0x0002, 500, out _) != IntPtr.Zero;

    private static void EnsureEqualIntegrity(Process target)
    {
        int current = IntegrityLevel(Process.GetCurrentProcess());
        int child = IntegrityLevel(target);
        if (current != child || current >= 0x3000) throw new UnauthorizedAccessException("Launcher and client must have the same standard-user integrity level.");
    }

    private static int IntegrityLevel(Process process)
    {
        if (!OpenProcessToken(process.Handle, 0x0008, out IntPtr token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            _ = GetTokenInformation(token, 25, IntPtr.Zero, 0, out int required);
            IntPtr buffer = Marshal.AllocHGlobal(required);
            try
            {
                if (!GetTokenInformation(token, 25, buffer, required, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                IntPtr countPointer = GetSidSubAuthorityCount(sid);
                byte count = Marshal.ReadByte(countPointer);
                if (count == 0) throw new InvalidDataException("The process integrity SID was empty.");
                return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { _ = CloseHandle(token); }
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr information, int length, out int returnLength);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
