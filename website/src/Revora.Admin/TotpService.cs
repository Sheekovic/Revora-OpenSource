using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Revora.Admin;

public sealed class TotpService(TimeProvider? clock = null)
{
    private readonly TimeProvider timeProvider = clock ?? TimeProvider.System;

    public byte[] GenerateSecret()
    {
        byte[] secret = new byte[20];
        RandomNumberGenerator.Fill(secret);
        return secret;
    }

    public bool Verify(byte[] secret, string code)
    {
        if (secret.Length < 20 || code.Length != 6 || !code.All(char.IsAsciiDigit)) return false;
        long counter = timeProvider.GetUtcNow().ToUnixTimeSeconds() / 30;
        int supplied = int.Parse(code, System.Globalization.CultureInfo.InvariantCulture);
        int mismatch = 1;
        for (long offset = -1; offset <= 1; offset++) mismatch &= GenerateCode(secret, counter + offset) == supplied ? 0 : 1;
        return mismatch == 0;
    }

    public string EncodeSecret(byte[] secret) => Base32.Encode(secret);

    private static int GenerateCode(byte[] secret, long counter)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, counter);
        Span<byte> digest = stackalloc byte[20];
        HMACSHA1.HashData(secret, bytes, digest);
        int offset = digest[^1] & 0x0f;
        int binary = ((digest[offset] & 0x7f) << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3];
        CryptographicOperations.ZeroMemory(digest);
        return binary % 1_000_000;
    }
}

internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }
}
