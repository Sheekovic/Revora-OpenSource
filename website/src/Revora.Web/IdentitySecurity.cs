using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Revora.Web;

public interface IPasswordSecurity
{
    ValueTask<string> HashAsync(string password, CancellationToken cancellationToken);
    ValueTask<bool> VerifyAsync(string encodedHash, string password, CancellationToken cancellationToken);
}

public sealed class DisabledPasswordSecurity : IPasswordSecurity
{
    public ValueTask<string> HashAsync(string password, CancellationToken cancellationToken) => throw new InvalidOperationException("Durable reviewed password hashing is not active.");
    public ValueTask<bool> VerifyAsync(string encodedHash, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] left = SHA256.HashData(Encoding.UTF8.GetBytes(encodedHash));
        byte[] right = SHA256.HashData(Encoding.UTF8.GetBytes($"disabled:{password.Length}"));
        _ = CryptographicOperations.FixedTimeEquals(left, right);
        return ValueTask.FromResult(false);
    }
}

public sealed record WebSession(string Id, string UserId, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

public interface IWebSessionRegistry
{
    IReadOnlyCollection<WebSession> ForUser(string userId);
    void Register(WebSession session);
    bool Revoke(string userId, string sessionId);
    int RevokeAll(string userId);
}

public sealed class InMemoryWebSessionRegistry : IWebSessionRegistry
{
    private readonly ConcurrentDictionary<string, WebSession> sessions = new(StringComparer.Ordinal);
    public IReadOnlyCollection<WebSession> ForUser(string userId) => sessions.Values.Where(x => x.UserId == userId).OrderByDescending(x => x.LastSeenAt).ToArray();
    public void Register(WebSession session) => sessions[session.Id] = session;
    public bool Revoke(string userId, string sessionId) => sessions.TryGetValue(sessionId, out WebSession? session) && session.UserId == userId && sessions.TryRemove(sessionId, out _);
    public int RevokeAll(string userId)
    {
        int count = 0;
        foreach (WebSession session in sessions.Values.Where(x => x.UserId == userId)) if (sessions.TryRemove(session.Id, out _)) count++;
        return count;
    }
}

public static class ReturnUrlPolicy
{
    public static string SafeLocalPath(string? candidate, string culture)
    {
        string fallback = $"/{culture}/dashboard";
        if (string.IsNullOrWhiteSpace(candidate) || !candidate.StartsWith("/", StringComparison.Ordinal) || candidate.StartsWith("//", StringComparison.Ordinal) || candidate.Contains('\\') || candidate.Contains("://", StringComparison.Ordinal)) return fallback;
        return candidate;
    }
}
