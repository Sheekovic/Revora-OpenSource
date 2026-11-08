using System.Collections.Concurrent;

namespace Revora.Application.Authentication;

public sealed class AuthenticationRateLimiter
{
    private readonly ConcurrentDictionary<string, Window> windows = new(StringComparer.Ordinal);
    private readonly int permittedAttempts;
    private readonly int maximumTrackedKeys;
    private readonly TimeSpan windowLength;
    private readonly TimeProvider timeProvider;

    public AuthenticationRateLimiter(
        int permittedAttempts,
        TimeSpan windowLength,
        int maximumTrackedKeys,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permittedAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTrackedKeys, 1);
        if (windowLength <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowLength));
        }

        this.permittedAttempts = permittedAttempts;
        this.windowLength = windowLength;
        this.maximumTrackedKeys = maximumTrackedKeys;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = timeProvider.GetUtcNow();

        if (windows.Count >= maximumTrackedKeys && !windows.ContainsKey(key))
        {
            RemoveExpired(now);
            if (windows.Count >= maximumTrackedKeys)
            {
                return false;
            }
        }

        var window = windows.GetOrAdd(key, _ => new Window(now));
        lock (window)
        {
            if (now - window.StartedAt >= windowLength)
            {
                window.StartedAt = now;
                window.Attempts = 0;
            }

            if (window.Attempts >= permittedAttempts)
            {
                return false;
            }

            window.Attempts++;
            return true;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in windows)
        {
            if (now - pair.Value.StartedAt >= windowLength)
            {
                windows.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class Window(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; set; } = startedAt;
        public int Attempts { get; set; }
    }
}
