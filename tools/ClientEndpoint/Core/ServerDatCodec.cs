using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;

namespace Revora.ClientEndpoint;

// Only public key material from the two whole-hash-reviewed 6711 DX9 images.
// The 37 disp32 + 27 disp8 stores, rolling index 17, 32-bit word order and
// exponent 65537 were checked in the native constructor at VA 0x0099AF6C.
internal static class ServerDatCodec
{
    internal const int MaximumCiphertext = 1024 * 1024;
    internal const int MaximumXml = 2 * 1024 * 1024;
    internal static readonly int[] WordOffsets = BuildOffsets();
    private static readonly IReadOnlyDictionary<string, string> OriginalWords = new Dictionary<string, string>
    {
        [EndpointPatch.GenuineHash] = "5C3DAFA0BC08E8E4D1AE989A58921C4DFD7478D60590F03826CCD543BF54FCD6815092FF1A3B095E7D84D6660B23567C7BFFA8EF5F8416DF575630C884DB3AC9883DBA5E52E3FD733716DDD4DA68B8C3C696305DD3CF07C663C5265F3207C2CAC8A05C7779AC1983DBD449D9C92575D0AD11EA3961C3BEB8F3407D2C09B059FB792E3B0797B920CA86959884CA002846AA85B9A9D7A92646309A2DE0164CD8877646E54A262045C898060C2DA168BA5CB906819F3A054628DEA7C123D7F341FBE44EB445E8E2DFDE6644A04BB1E3B08BB2C83A48FDC0E8E1DBBBF8EF68FD1AABAFC7AA51EF699F33FB7EEC933137D1EBBBF04B42022872C8B140838CA7F22022",
        [EndpointPatch.RevoraHash] = "E062A450FF7C1B86C2943161E0858ED517FC0990C916A1D2CD0F25DBE41F837973DFE06E965C1623C34199AB1A3682FA3BEA097B05E54463CE0A545EC92BC745A9797865274784FE41A3670D94CA1B7D4AFB53C672D8D18951D22E2722B1CA4F628477A62310CB72C0C4B3AA19B1782EFB9963CE70D894A7B3515B8F08DE956DED9994ED6068A988550BDAC0BA6A2582855A3C8C5CAD9E48886EF23E35D68D902BB56FC99A99C38001EC3B64381C68EA671671C98EB8DE518DDE3071802E1888B339FD4EB7236A4B98EC12ACDDF522946C3E1216A42E2F12E3E5F5199657C460C024C7F646BD41AB95955EEA7651A13EE7E5FD1E0E876E848795DA5C4803E7C8"
    };

    private static int[] BuildOffsets()
    {
        var offsets = new int[64]; int cursor = 0x59A3A1;
        for (int i = 0; i < 64; i++) { offsets[i] = cursor + (i < 37 ? 6 : 3); cursor += i < 37 ? 10 : 7; }
        return offsets;
    }

    internal static byte[] RestorePublicWords(byte[] image, string originalHash)
    {
        byte[] output = image.ToArray();
        byte[] words = Convert.FromHexString(OriginalWords[originalHash]);
        for (int i = 0; i < 64; i++) words.AsSpan(i * 4, 4).CopyTo(output.AsSpan(WordOffsets[i], 4));
        return output;
    }

    internal static byte[] ReadModulus(byte[] image)
    {
        if (image.Length != EndpointPatch.ClientSize) throw new InvalidDataException("Unexpected executable size.");
        uint[] obfuscated = new uint[64]; uint[] words = new uint[64];
        for (int i = 0; i < 64; i++)
        {
            int offset = WordOffsets[i], instruction = offset - (i < 37 ? 6 : 3);
            int displacement = i < 37 ? BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(instruction + 2, 4)) : (sbyte)image[instruction + 2];
            if (image[instruction] != 0xc7 || image[instruction + 1] != (i < 37 ? 0x85 : 0x45) || displacement != -276 + i * 4)
                throw new InvalidDataException("The reviewed 6711 public-key constructor does not match.");
            obfuscated[i] = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset, 4));
        }
        for (int i = 0; i < 63; i++) words[(17 + i) % 64] = obfuscated[i] ^ obfuscated[i + 1];
        words[16] = obfuscated[63];
        byte[] modulus = new byte[256];
        for (int i = 0; i < 64; i++) BinaryPrimitives.WriteUInt32BigEndian(modulus.AsSpan(i * 4, 4), words[i]);
        ValidateModulus(modulus); return modulus;
    }

    internal static byte[] ReplaceModulus(byte[] image, byte[] modulus)
    {
        ValidateModulus(modulus); _ = ReadModulus(image);
        uint[] words = new uint[64], obfuscated = new uint[64];
        for (int i = 0; i < 64; i++) words[i] = BinaryPrimitives.ReadUInt32BigEndian(modulus.AsSpan(i * 4, 4));
        obfuscated[63] = words[16];
        for (int i = 62; i >= 0; i--) obfuscated[i] = words[(17 + i) % 64] ^ obfuscated[i + 1];
        byte[] output = image.ToArray();
        for (int i = 0; i < 64; i++) BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(WordOffsets[i], 4), obfuscated[i]);
        if (!ReadModulus(output).SequenceEqual(modulus)) throw new CryptographicException("Public-key replacement verification failed.");
        return output;
    }

    internal static void ValidateModulus(byte[] modulus)
    {
        if (modulus.Length != 256 || (modulus[0] & 0x80) == 0 || (modulus[^1] & 1) == 0)
            throw new CryptographicException("A 2048-bit odd RSA modulus is required.");
    }

    internal static byte[] Decode(byte[] ciphertext, byte[] modulus)
    {
        ValidateModulus(modulus);
        if (ciphertext.Length is 0 or > MaximumCiphertext || ciphertext.Length % 256 != 0)
            throw new InvalidDataException("Server.dat must contain complete 256-byte RSA blocks (maximum 1 MiB).");
        BigInteger n = new(modulus, true, true);
        using var compressed = new MemoryStream();
        for (int offset = 0; offset < ciphertext.Length; offset += 256)
        {
            BigInteger value = new(ciphertext.AsSpan(offset, 256), true, true);
            if (value >= n) throw new CryptographicException("RSA block is outside the modulus.");
            byte[] block = Fixed(BigInteger.ModPow(value, 65537, n));
            int separator = Array.IndexOf(block, (byte)0, 2);
            if (block[0] != 0 || block[1] != 1 || separator < 10 || block.AsSpan(2, separator - 2).ContainsAnyExcept((byte)0xff))
                throw new CryptographicException("Server.dat signature/padding does not match this executable's public key.");
            compressed.Write(block, separator + 1, 255 - separator);
        }
        compressed.Position = 0;
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var xml = new MemoryStream();
        byte[] buffer = new byte[8192]; int read;
        while ((read = gzip.Read(buffer)) != 0)
        {
            if (xml.Length + read > MaximumXml) throw new InvalidDataException("Expanded Server.dat exceeds 2 MiB.");
            xml.Write(buffer, 0, read);
        }
        if (xml.Length == 0) throw new InvalidDataException("Server.dat contains no XML.");
        return xml.ToArray();
    }

    internal static byte[] Encode(byte[] xml, RSA privateKey)
    {
        if (xml.Length is 0 or > MaximumXml) throw new InvalidDataException("XML size is out of range.");
        RSAParameters key = privateKey.ExportParameters(true);
        try
        {
            if (key.Modulus is null || key.D is null || key.Exponent is null || !key.Exponent.SequenceEqual(new byte[] { 1, 0, 1 }))
                throw new CryptographicException("An RSA-2048 private key with exponent 65537 is required.");
            ValidateModulus(key.Modulus);
            BigInteger n = new(key.Modulus, true, true), d = new(key.D, true, true);
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, true)) gzip.Write(xml);
            byte[] source = compressed.ToArray();
            using var output = new MemoryStream();
            for (int offset = 0; offset < source.Length; offset += 245)
            {
                int count = Math.Min(245, source.Length - offset);
                byte[] block = new byte[256]; block[1] = 1;
                Array.Fill(block, (byte)0xff, 2, 253 - count);
                source.AsSpan(offset, count).CopyTo(block.AsSpan(256 - count));
                // Legacy format is a raw PKCS#1 type-one private operation, not confidential encryption.
                // Blind the private operation with fresh randomness; this is a local offline file tool.
                BigInteger r;
                do { r = new BigInteger(RandomNumberGenerator.GetBytes(256), true, true) % n; } while (r < 2 || BigInteger.GreatestCommonDivisor(r, n) != 1);
                BigInteger blinded = new BigInteger(block, true, true) * BigInteger.ModPow(r, 65537, n) % n;
                BigInteger signed = BigInteger.ModPow(blinded, d, n) * Inverse(r, n) % n;
                output.Write(Fixed(signed));
                if (output.Length > MaximumCiphertext) throw new InvalidDataException("Signed Server.dat exceeds 1 MiB.");
            }
            byte[] result = output.ToArray();
            if (!Decode(result, key.Modulus).SequenceEqual(xml)) throw new CryptographicException("Signed file round-trip failed.");
            return result;
        }
        finally
        {
            foreach (byte[]? secret in new[] { key.D, key.P, key.Q, key.DP, key.DQ, key.InverseQ })
                if (secret is not null) CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static BigInteger Inverse(BigInteger value, BigInteger modulus)
    {
        BigInteger oldR = modulus, r = value, oldT = 0, t = 1;
        while (r != 0) { BigInteger q = oldR / r; (oldR, r) = (r, oldR - q * r); (oldT, t) = (t, oldT - q * t); }
        if (oldR != 1) throw new CryptographicException("RSA blinding inverse failed.");
        return (oldT % modulus + modulus) % modulus;
    }

    private static byte[] Fixed(BigInteger value)
    {
        byte[] bytes = value.ToByteArray(true, true);
        if (bytes.Length > 256) throw new CryptographicException("RSA block overflow.");
        byte[] output = new byte[256]; bytes.CopyTo(output, 256 - bytes.Length); return output;
    }
}
