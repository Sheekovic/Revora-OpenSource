using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Revora.NativeLogin.Core;

/// <summary>Bounded Blowfish CFB64 used only by the evidence-supported 5165 Game transport.</summary>
/// <remarks>The standard Blowfish constants and algorithm are independently adapted from the
/// BSD-3-Clause Comet reference identified in THIRD_PARTY_NOTICES.md.</remarks>
internal sealed class BlowfishCfb64 : IDisposable
{
    public const int BlockSize = 8;
    public const int MaximumKeyBytes = 72;
    private readonly uint[] p;
    private readonly uint[] s;
    private readonly byte[] decryptIv = new byte[BlockSize];
    private readonly byte[] encryptIv = new byte[BlockSize];
    private int decryptPosition;
    private int encryptPosition;

    public BlowfishCfb64(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty || key.Length > MaximumKeyBytes) throw new ArgumentOutOfRangeException(nameof(key));
        uint[] constants = LoadConstants();
        p = constants[..18];
        s = constants[18..];
        ExpandKey(key);
        Array.Clear(constants);
    }

    public void SetServerIvs(ReadOnlySpan<byte> inboundDecryptIv, ReadOnlySpan<byte> outboundEncryptIv)
    {
        if (inboundDecryptIv.Length != BlockSize || outboundEncryptIv.Length != BlockSize)
            throw new ArgumentException("Both IVs must be exactly eight bytes.");
        inboundDecryptIv.CopyTo(decryptIv);
        outboundEncryptIv.CopyTo(encryptIv);
        decryptPosition = encryptPosition = 0;
    }

    public void DecryptClient(ReadOnlySpan<byte> source, Span<byte> destination) => Transform(source, destination, decryptIv, ref decryptPosition, false);
    public void EncryptServer(ReadOnlySpan<byte> source, Span<byte> destination) => Transform(source, destination, encryptIv, ref encryptPosition, true);

    private void Transform(ReadOnlySpan<byte> source, Span<byte> destination, byte[] iv, ref int position, bool encrypt)
    {
        if (destination.Length < source.Length) throw new ArgumentException("Destination is too small.", nameof(destination));
        for (int index = 0; index < source.Length; index++)
        {
            if (position == 0)
            {
                uint left = BinaryPrimitives.ReadUInt32BigEndian(iv);
                uint right = BinaryPrimitives.ReadUInt32BigEndian(iv.AsSpan(4));
                Encipher(ref left, ref right);
                BinaryPrimitives.WriteUInt32BigEndian(iv, left);
                BinaryPrimitives.WriteUInt32BigEndian(iv.AsSpan(4), right);
            }
            byte input = source[index];
            byte output = (byte)(input ^ iv[position]);
            destination[index] = output;
            iv[position] = encrypt ? output : input;
            position = (position + 1) & 7;
        }
    }

    private void ExpandKey(ReadOnlySpan<byte> key)
    {
        int cursor = 0;
        for (int index = 0; index < p.Length; index++)
        {
            uint word = 0;
            for (int count = 0; count < 4; count++)
            {
                word = (word << 8) | key[cursor];
                cursor = (cursor + 1) % key.Length;
            }
            p[index] ^= word;
        }
        uint left = 0, right = 0;
        for (int index = 0; index < p.Length; index += 2)
        {
            Encipher(ref left, ref right);
            p[index] = left; p[index + 1] = right;
        }
        for (int index = 0; index < s.Length; index += 2)
        {
            Encipher(ref left, ref right);
            s[index] = left; s[index + 1] = right;
        }
    }

    private void Encipher(ref uint left, ref uint right)
    {
        left ^= p[0];
        for (int round = 1; round <= 16; round++)
        {
            right ^= p[round] ^ F(left);
            (left, right) = (right, left);
        }
        right ^= p[17];
        (left, right) = (right, left);
    }

    private uint F(uint value) => unchecked(((s[value >> 24] + s[0x100 + ((value >> 16) & 0xff)]) ^ s[0x200 + ((value >> 8) & 0xff)]) + s[0x300 + (value & 0xff)]);

    private static uint[] LoadConstants()
    {
        byte[] compressed = Convert.FromBase64String(Constants);
        byte[] raw = new byte[1042 * sizeof(uint)];
        using var input = new MemoryStream(compressed, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        zlib.ReadExactly(raw);
        if (zlib.ReadByte() != -1) throw new InvalidDataException("The Blowfish constant set is malformed.");
        uint[] values = new uint[1042];
        for (int index = 0; index < values.Length; index++) values[index] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(index * 4));
        CryptographicOperations.ZeroMemory(raw);
        return values;
    }

    public void Dispose()
    {
        Array.Clear(p); Array.Clear(s);
        CryptographicOperations.ZeroMemory(decryptIv);
        CryptographicOperations.ZeroMemory(encryptIv);
    }

    private const string Constants = "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";
}
