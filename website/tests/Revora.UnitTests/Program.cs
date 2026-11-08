using System.Buffers.Binary;
using Revora.Application.Authentication;
using Revora.Domain.Accounts;
using Revora.Networking.Connections;
using Revora.Networking.Framing;
using Revora.Persistence.InMemory;

namespace Revora.UnitTests;

internal static class Program
{
    private static async Task Main()
    {
        FragmentedFrameIsReassembled();
        CoalescedFramesAreSeparated();
        MalformedLengthsAreRejected();
        FooterExcludedFromDeclaredLengthIsSupported();
        await BoundedQueueAppliesBackpressure();
        await CancellationAndCleanupAreDeterministic();
        AuthenticationRateLimitIsBounded();
        PasswordBytesVerifyWithoutStringConversion();
        await HandoffTokenIsSingleUseAndRealmBound();
        await ExpiredHandoffTokenIsRejected();
        Console.WriteLine("Revora.UnitTests: 10 passed");
    }

    private static void PasswordBytesVerifyWithoutStringConversion()
    {
        var hasher = new AdaptivePasswordHasher();
        string encoded = hasher.Hash("123456789012");
        byte[] valid = "123456789012"u8.ToArray();
        byte[] invalid = "123456789013"u8.ToArray();
        try
        {
            if (hasher.Verify(valid, encoded) != PasswordVerification.Valid) throw new InvalidOperationException("Password byte verification rejected a valid password.");
            if (hasher.Verify(invalid, encoded) != PasswordVerification.Failed) throw new InvalidOperationException("Password byte verification accepted an invalid password.");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(valid);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(invalid);
        }
    }

    private static void AuthenticationRateLimitIsBounded()
    {
        var clock = new ManualTimeProvider();
        var limiter = new AuthenticationRateLimiter(2, TimeSpan.FromMinutes(1), 1, clock);

        Equal(true, limiter.TryAcquire("source-a"));
        Equal(true, limiter.TryAcquire("source-a"));
        Equal(false, limiter.TryAcquire("source-a"));
        Equal(false, limiter.TryAcquire("source-b"));
        clock.Advance(TimeSpan.FromMinutes(1));
        Equal(true, limiter.TryAcquire("source-b"));
    }

    private static async Task HandoffTokenIsSingleUseAndRealmBound()
    {
        var store = new InMemoryHandoffTokenStore();
        var accountId = new AccountId(Guid.NewGuid());
        var token = await store.IssueAsync(accountId, "Revora", TimeSpan.FromMinutes(1), CancellationToken.None);

        Equal("[REDACTED HANDOFF TOKEN]", token.ToString());
        IsNull(await store.ConsumeAsync(token, "OtherRealm", CancellationToken.None));
        IsNull(await store.ConsumeAsync(token, "Revora", CancellationToken.None));

        var validToken = await store.IssueAsync(accountId, "Revora", TimeSpan.FromMinutes(1), CancellationToken.None);
        var ticket = await store.ConsumeAsync(validToken, "Revora", CancellationToken.None);
        Equal(accountId, ticket?.AccountId ?? default);
        IsNull(await store.ConsumeAsync(validToken, "Revora", CancellationToken.None));
    }

    private static async Task ExpiredHandoffTokenIsRejected()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryHandoffTokenStore(clock);
        var token = await store.IssueAsync(
            new AccountId(Guid.NewGuid()),
            "Revora",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(1));
        IsNull(await store.ConsumeAsync(token, "Revora", CancellationToken.None));
    }

    private static void FragmentedFrameIsReassembled()
    {
        var accumulator = new LittleEndianUInt16FrameAccumulator(64);
        var frame = MakeFrame(1, 2, 3, 4);

        Equal(0, accumulator.Append(frame.AsSpan(0, 1)).Count);
        Equal(0, accumulator.Append(frame.AsSpan(1, 2)).Count);
        var result = accumulator.Append(frame.AsSpan(3));

        Equal(1, result.Count);
        SequenceEqual(frame, result[0]);
        Equal(0, accumulator.BufferedByteCount);
    }

    private static void CoalescedFramesAreSeparated()
    {
        var accumulator = new LittleEndianUInt16FrameAccumulator(64);
        var first = MakeFrame(10);
        var second = MakeFrame(20, 21);
        var combined = first.Concat(second).ToArray();

        var result = accumulator.Append(combined);

        Equal(2, result.Count);
        SequenceEqual(first, result[0]);
        SequenceEqual(second, result[1]);
    }

    private static void MalformedLengthsAreRejected()
    {
        Throws<FrameProtocolException>(() =>
            new LittleEndianUInt16FrameAccumulator(64).Append(new byte[] { 1, 0 }));
        Throws<FrameProtocolException>(() =>
            new LittleEndianUInt16FrameAccumulator(64).Append(new byte[] { 65, 0 }));
        Throws<FrameProtocolException>(() =>
            new LittleEndianUInt16FrameAccumulator(8).Append(new byte[9]));
    }

    private static void FooterExcludedFromDeclaredLengthIsSupported()
    {
        var accumulator = new LittleEndianUInt16FrameAccumulator(64, trailingByteCount: 2);
        var packet = new byte[] { 4, 0, 7, 8, 9, 10 };
        var result = accumulator.Append(packet);

        Equal(1, result.Count);
        SequenceEqual(packet, result[0]);
    }

    private static async Task BoundedQueueAppliesBackpressure()
    {
        var queue = new BoundedConnectionQueue<int>(1);
        await queue.WriteAsync(1, CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await ThrowsAsync<OperationCanceledException>(async () =>
            await queue.WriteAsync(2, cancellation.Token));
        queue.TryComplete();
    }

    private static async Task CancellationAndCleanupAreDeterministic()
    {
        var cleanupCalls = 0;
        var lifetime = new ConnectionLifetime(() =>
        {
            Interlocked.Increment(ref cleanupCalls);
            return ValueTask.CompletedTask;
        });

        var token = lifetime.CancellationToken;
        await Task.WhenAll(
            lifetime.DisposeAsync().AsTask(),
            lifetime.DisposeAsync().AsTask());

        Equal(true, token.IsCancellationRequested);
        Equal(1, cleanupCalls);
    }

    private static byte[] MakeFrame(params byte[] body)
    {
        var frame = new byte[LittleEndianUInt16FrameAccumulator.LengthFieldSize + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        body.CopyTo(frame, LittleEndianUInt16FrameAccumulator.LengthFieldSize);
        return frame;
    }

    private static void Equal<T>(T expected, T actual) where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
        {
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
        }
    }

    private static void SequenceEqual(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException("Byte sequences differ.");
        }
    }

    private static void IsNull(object? actual)
    {
        if (actual is not null)
        {
            throw new InvalidOperationException("Expected null.");
        }
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan amount) => utcNow = utcNow.Add(amount);
    }
}
