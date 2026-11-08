using System.Security.Cryptography;

namespace Revora.Application.Authentication;

public sealed class LaunchTicket : IDisposable
{
    public const int Size = 32;
    private readonly byte[] value;

    public LaunchTicket(ReadOnlySpan<byte> value)
    {
        if (value.Length != Size) throw new ArgumentException("A launch ticket must contain exactly 32 bytes.", nameof(value));
        this.value = value.ToArray();
    }

    public ReadOnlyMemory<byte> Value => value;
    public void Dispose() => CryptographicOperations.ZeroMemory(value);
    public override string ToString() => "[REDACTED LAUNCH TICKET]";
}

public sealed record LaunchTicketContext(
    Guid AccountId,
    Guid RealmId,
    Guid LauncherSessionId,
    string ClientBuildId,
    DateTimeOffset ExpiresAt);
