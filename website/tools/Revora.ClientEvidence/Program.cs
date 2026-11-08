using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Revora.NativeLogin.Core;

if (args.Length < 1) return Usage();
switch (args[0])
{
    case "tq-file-decrypt" when args.Length == 3:
        byte[] encryptedFile = await File.ReadAllBytesAsync(args[1]);
        if (encryptedFile.Length is 0 or > 16 * 1024 * 1024)
            throw new InvalidDataException("The TQ file is empty or exceeds the evidence-tool limit.");
        byte[] decryptedFile = TqFileCipher.Decrypt(encryptedFile, 9527);
        try
        {
            await File.WriteAllBytesAsync(args[2], decryptedFile);
            Console.WriteLine($"inputSha256={Hash(encryptedFile)} outputSha256={Hash(decryptedFile)} bytes={decryptedFile.Length} seed=9527");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decryptedFile);
        }
        return 0;
    case "decrypt" when args.Length == 4:
        byte[] executable = await File.ReadAllBytesAsync(args[1]);
        RsaMaterial original = RsaDatCodec.ExtractPublicMaterial(executable);
        await File.WriteAllBytesAsync(args[3], RsaDatCodec.Decrypt(await File.ReadAllBytesAsync(args[2]), original));
        Console.WriteLine($"clientSha256={Hash(executable)} modulusOffsets={original.Offsets.Length}");
        return 0;
    case "decrypt-reviewed" when args.Length == 5:
        byte[] reviewedExecutable = await File.ReadAllBytesAsync(args[1]);
        using (JsonDocument reviewedPatch = JsonDocument.Parse(await File.ReadAllTextAsync(args[2])))
        {
            int[] reviewedOffsets = reviewedPatch.RootElement.GetProperty("changes").EnumerateArray()
                .Select(change => change.GetProperty("Offset").GetInt32()).ToArray();
            RsaMaterial reviewedMaterial = RsaDatCodec.ExtractPublicMaterial(reviewedExecutable, reviewedOffsets);
            await File.WriteAllBytesAsync(args[4], RsaDatCodec.Decrypt(await File.ReadAllBytesAsync(args[3]), reviewedMaterial));
            Console.WriteLine($"clientSha256={Hash(reviewedExecutable)} modulusOffsets={reviewedOffsets.Length}");
        }
        return 0;
    case "build" when args.Length == 7:
        byte[] input = await File.ReadAllBytesAsync(args[1]);
        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(await File.ReadAllTextAsync(args[3]));
            RSAParameters privateParameters = rsa.ExportParameters(true);
            RsaMaterial current = RsaDatCodec.ExtractPublicMaterial(input);
            BinaryPatchResult patched = RsaDatCodec.PatchPublicModulus(input, current, privateParameters.Modulus!);
            byte[] plaintext = await File.ReadAllBytesAsync(args[2]);
            byte[] encrypted = RsaDatCodec.Encrypt(plaintext, privateParameters);
            BigInteger replacementModulus = new(privateParameters.Modulus!, isUnsigned: true, isBigEndian: true);
            if (!RsaDatCodec.Decrypt(encrypted, new(replacementModulus, [], [])).SequenceEqual(plaintext))
                throw new CryptographicException("RSA DAT round-trip validation failed.");
            await File.WriteAllBytesAsync(args[4], patched.Bytes);
            await File.WriteAllBytesAsync(args[5], encrypted);
            await File.WriteAllTextAsync(args[6], JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                inputSha256 = Hash(input),
                outputSha256 = Hash(patched.Bytes),
                changes = patched.Changes.Select(change => new { change.Offset, change.OriginalHex, change.ReplacementHex }),
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return 0;
    case "bootstrap" when args.Length == 5:
        byte[] originalInput = await File.ReadAllBytesAsync(args[1]);
        using (JsonDocument rsaPatch = JsonDocument.Parse(await File.ReadAllTextAsync(args[2])))
        {
            byte[] rsaPatched = originalInput.ToArray();
            var changes = new List<object>();
            if (Hash(originalInput) != rsaPatch.RootElement.GetProperty("inputSha256").GetString()) throw new CryptographicException("RSA patch input hash mismatch.");
            foreach (JsonElement change in rsaPatch.RootElement.GetProperty("changes").EnumerateArray())
            {
                int offset = change.GetProperty("Offset").GetInt32();
                byte[] before = Convert.FromHexString(change.GetProperty("OriginalHex").GetString()!);
                byte[] after = Convert.FromHexString(change.GetProperty("ReplacementHex").GetString()!);
                if (before.Length != after.Length || !rsaPatched.AsSpan(offset, before.Length).SequenceEqual(before)) throw new InvalidDataException("RSA patch byte mismatch.");
                after.CopyTo(rsaPatched, offset);
                changes.Add(new { Offset = offset, OriginalHex = Convert.ToHexString(before).ToLowerInvariant(), ReplacementHex = Convert.ToHexString(after).ToLowerInvariant(), Purpose = "Replace only the RSA public modulus used for reviewed loopback Server.dat.", Evidence = "Exact 64-word rolling-XOR modulus construction and retained root-only matching testing key." });
            }
            if (Hash(rsaPatched) != AutomaticAccountBootstrap5165.InputSha256) throw new CryptographicException("RSA-patched intermediate hash mismatch.");
            BootstrapPatchResult bootstrap = AutomaticAccountBootstrap5165.Apply(rsaPatched);
            changes.AddRange(bootstrap.Changes.Select(change => new { change.Offset, change.OriginalHex, change.ReplacementHex, change.Purpose, change.Evidence }));
            await File.WriteAllBytesAsync(args[3], bootstrap.Bytes);
            await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(new { schemaVersion = 2, inputSha256 = Hash(originalInput), intermediateSha256 = AutomaticAccountBootstrap5165.InputSha256, outputSha256 = Hash(bootstrap.Bytes), changes }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"inputSha256={Hash(originalInput)} outputSha256={Hash(bootstrap.Bytes)} changes={changes.Count}");
        }
        return 0;
    default: return Usage();
}

static int Usage()
{
    Console.Error.WriteLine("Usage: tq-file-decrypt <encrypted.dat> <plaintext> | decrypt <Conquer.exe> <Server.dat> <plaintext> | decrypt-reviewed <patched.exe> <patch.json> <Server.dat> <plaintext> | build <Conquer.exe> <plaintext> <private.pem> <patched.exe> <Server.dat> <patch.json> | bootstrap <Conquer.exe> <rsa-patch.json> <patched.exe> <patch.json>");
    return 2;
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal static class TqFileCipher
{
    private const int KeyLength = 128;

    public static byte[] Decrypt(ReadOnlySpan<byte> encrypted, uint seed)
    {
        Span<byte> key = stackalloc byte[KeyLength];
        uint state = seed;
        for (int i = 0; i < key.Length; i++)
        {
            state = unchecked(state * 0x343fdu + 0x269ec3u);
            key[i] = (byte)(((state >> 16) & 0x7fffu) % 0x100u);
        }

        byte[] plaintext = new byte[encrypted.Length];
        for (int i = 0; i < plaintext.Length; i++)
        {
            byte value = (byte)(encrypted[i] ^ key[i % key.Length]);
            int shift = i & 7;
            plaintext[i] = shift == 0
                ? value
                : (byte)((value >> shift) | (value << (8 - shift)));
        }

        CryptographicOperations.ZeroMemory(key);
        return plaintext;
    }
}

internal sealed record RsaMaterial(BigInteger Modulus, int[] Offsets, uint[] ObfuscatedWords);
internal sealed record PatchChange(int Offset, string OriginalHex, string ReplacementHex);
internal sealed record BinaryPatchResult(byte[] Bytes, IReadOnlyList<PatchChange> Changes);

internal static class RsaDatCodec
{
    private const uint FirstObfuscatedWord = 0xa0af3d5c;
    private const uint LastObfuscatedWord = 0x2220f2a7;
    private const int WordCount = 64;
    private const int RollingStart = 17;

    public static RsaMaterial ExtractPublicMaterial(byte[] executable)
    {
        byte[] first = BitConverter.GetBytes(FirstObfuscatedWord);
        for (int start = 0; start <= executable.Length - 4; start++)
        {
            if (!executable.AsSpan(start, 4).SequenceEqual(first)) continue;
            if (TryCollectOffsets(executable, start, out int[] offsets))
            {
                uint[] obfuscated = offsets.Select(offset => BitConverter.ToUInt32(executable, offset)).ToArray();
                if (obfuscated[^1] != LastObfuscatedWord) continue;
                uint[] words = new uint[WordCount];
                int index = RollingStart;
                for (int j = 0; j < WordCount - 1; j++, index++) words[index % WordCount] = obfuscated[j] ^ obfuscated[j + 1];
                words[index % WordCount] = obfuscated[^1];
                byte[] modulus = words.SelectMany(word => ToBigEndian(word)).ToArray();
                return new(new BigInteger(modulus, isUnsigned: true, isBigEndian: true), offsets, obfuscated);
            }
        }
        throw new InvalidDataException("The exact reviewed RSA modulus construction was not found.");
    }

    public static RsaMaterial ExtractPublicMaterial(byte[] executable, int[] offsets)
    {
        if (offsets.Length != WordCount || offsets.Distinct().Count() != WordCount ||
            offsets.Any(offset => offset < 0 || offset > executable.Length - 4))
            throw new InvalidDataException("The reviewed RSA modulus offset set is invalid.");
        uint[] obfuscated = offsets.Select(offset => BitConverter.ToUInt32(executable, offset)).ToArray();
        uint[] words = new uint[WordCount];
        int index = RollingStart;
        for (int j = 0; j < WordCount - 1; j++, index++) words[index % WordCount] = obfuscated[j] ^ obfuscated[j + 1];
        words[index % WordCount] = obfuscated[^1];
        byte[] modulus = words.SelectMany(word => ToBigEndian(word)).ToArray();
        return new(new BigInteger(modulus, isUnsigned: true, isBigEndian: true), offsets.ToArray(), obfuscated);
    }

    public static byte[] Decrypt(byte[] ciphertext, RsaMaterial material)
    {
        if (ciphertext.Length == 0 || ciphertext.Length % 256 != 0) throw new InvalidDataException("RSA DAT length is invalid.");
        using var compressed = new MemoryStream();
        for (int offset = 0; offset < ciphertext.Length; offset += 256)
        {
            BigInteger value = new(ciphertext.AsSpan(offset, 256), isUnsigned: true, isBigEndian: true);
            byte[] encoded = ToFixed(BigInteger.ModPow(value, 65537, material.Modulus), 256);
            int separator = ValidateTypeOnePadding(encoded);
            compressed.Write(encoded, separator + 1, encoded.Length - separator - 1);
        }
        compressed.Position = 0;
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var plaintext = new MemoryStream();
        gzip.CopyTo(plaintext);
        return plaintext.ToArray();
    }

    public static byte[] Encrypt(byte[] plaintext, RSAParameters privateKey)
    {
        BigInteger modulus = new(privateKey.Modulus!, isUnsigned: true, isBigEndian: true);
        BigInteger exponent = new(privateKey.D!, isUnsigned: true, isBigEndian: true);
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) gzip.Write(plaintext);
        byte[] source = compressed.ToArray();
        using var output = new MemoryStream();
        for (int offset = 0; offset < source.Length; offset += 245)
        {
            int count = Math.Min(245, source.Length - offset);
            byte[] encoded = new byte[256];
            encoded[0] = 0; encoded[1] = 1;
            Array.Fill(encoded, (byte)0xff, 2, 253 - count);
            encoded[255 - count] = 0;
            source.AsSpan(offset, count).CopyTo(encoded.AsSpan(256 - count));
            BigInteger value = new(encoded, isUnsigned: true, isBigEndian: true);
            output.Write(ToFixed(BigInteger.ModPow(value, exponent, modulus), 256));
        }
        return output.ToArray();
    }

    public static BinaryPatchResult PatchPublicModulus(byte[] input, RsaMaterial current, byte[] replacementModulus)
    {
        if (replacementModulus.Length != 256) throw new InvalidDataException("A 2048-bit RSA modulus is required.");
        uint[] words = Enumerable.Range(0, WordCount).Select(i => ReadBigEndianUInt32(replacementModulus.AsSpan(i * 4, 4))).ToArray();
        uint[] replacement = new uint[WordCount];
        replacement[^1] = words[(RollingStart + WordCount - 1) % WordCount];
        for (int j = WordCount - 2; j >= 0; j--) replacement[j] = words[(RollingStart + j) % WordCount] ^ replacement[j + 1];
        byte[] output = input.ToArray();
        var changes = new List<PatchChange>(WordCount);
        for (int i = 0; i < WordCount; i++)
        {
            byte[] before = BitConverter.GetBytes(current.ObfuscatedWords[i]);
            byte[] after = BitConverter.GetBytes(replacement[i]);
            if (!output.AsSpan(current.Offsets[i], 4).SequenceEqual(before)) throw new InvalidDataException("Expected original patch bytes did not match.");
            after.CopyTo(output, current.Offsets[i]);
            changes.Add(new(current.Offsets[i], Convert.ToHexString(before).ToLowerInvariant(), Convert.ToHexString(after).ToLowerInvariant()));
        }
        return new(output, changes);
    }

    private static bool TryCollectOffsets(byte[] bytes, int start, out int[] offsets)
    {
        var found = new List<int> { start };
        int cursor = start;
        int endOffset = -1;
        while (found.Count < WordCount - 1)
        {
            int next = NextImmediate(bytes, cursor);
            if (next < 0 || next + 4 > bytes.Length || next - cursor > 64) { offsets = []; return false; }
            cursor = next;
            if (BitConverter.ToUInt32(bytes, next) == LastObfuscatedWord)
            {
                if (endOffset >= 0) { offsets = []; return false; }
                endOffset = next;
                continue;
            }
            found.Add(next);
        }
        if (endOffset < 0) { offsets = []; return false; }
        found.Add(endOffset);
        offsets = found.ToArray();
        return true;
    }

    private static int NextImmediate(byte[] bytes, int current)
    {
        int instruction = current + 4;
        for (int cursor = instruction; cursor < Math.Min(bytes.Length - 6, instruction + 64); cursor++)
        {
            if (bytes[cursor] == 0xc7 && bytes[cursor + 1] == 0x85) return cursor + 6;
            if (bytes[cursor] == 0xc7 && bytes[cursor + 1] == 0x45) return cursor + 3;
            if (bytes[cursor] is >= 0xb8 and <= 0xbf) return cursor + 1;
        }
        return -1;
    }

    private static int ValidateTypeOnePadding(byte[] encoded)
    {
        if (encoded[0] != 0 || encoded[1] != 1) throw new InvalidDataException("RSA DAT block padding is invalid.");
        int separator = Array.IndexOf(encoded, (byte)0, 2);
        if (separator < 10 || encoded.AsSpan(2, separator - 2).ContainsAnyExcept((byte)0xff)) throw new InvalidDataException("RSA DAT block padding is invalid.");
        return separator;
    }

    private static byte[] ToFixed(BigInteger value, int length)
    {
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length > length) throw new InvalidDataException("RSA result exceeded its block size.");
        byte[] fixedBytes = new byte[length];
        bytes.CopyTo(fixedBytes, length - bytes.Length);
        return fixedBytes;
    }

    private static byte[] ToBigEndian(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    private static uint ReadBigEndianUInt32(ReadOnlySpan<byte> bytes) => ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
}
