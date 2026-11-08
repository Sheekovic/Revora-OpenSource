using System.Collections.Concurrent;
using System.Security.Cryptography;
using Revora.Application.Authentication;
using Revora.Domain.Accounts;

namespace Revora.Persistence.InMemory;

public sealed class InMemoryHandoffTokenStore(TimeProvider? timeProvider = null) : IHandoffTokenStore
{
    private readonly ConcurrentDictionary<string, HandoffTicket> tickets = new(StringComparer.Ordinal);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public ValueTask<HandoffToken> IssueAsync(
        AccountId accountId,
        string realm,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var token = new HandoffToken(bytes);
        var key = GetLookupKey(token);
        var ticket = new HandoffTicket(accountId, realm, clock.GetUtcNow().Add(lifetime));
        if (!tickets.TryAdd(key, ticket))
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new CryptographicException("A cryptographically random handoff token collision occurred.");
        }

        CryptographicOperations.ZeroMemory(bytes);
        return ValueTask.FromResult(token);
    }

    public ValueTask<HandoffTicket?> ConsumeAsync(
        HandoffToken token,
        string realm,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        cancellationToken.ThrowIfCancellationRequested();

        if (!tickets.TryRemove(GetLookupKey(token), out var ticket) ||
            ticket.ExpiresAt <= clock.GetUtcNow() ||
            !string.Equals(ticket.Realm, realm, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<HandoffTicket?>(null);
        }

        return ValueTask.FromResult<HandoffTicket?>(ticket);
    }

    private static string GetLookupKey(HandoffToken token)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(token.Value.Span, digest);
        return Convert.ToHexString(digest);
    }
}
