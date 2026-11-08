using System.Buffers.Binary;
using System.Numerics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Revora.NativeLogin.Core;

internal sealed class ExactGameClient5165 : IDisposable
{
    private readonly BlowfishCfb64 initial;
    private readonly BlowfishCfb64 game;
    private readonly string publicHex;

    private ExactGameClient5165(BlowfishCfb64 initial, BlowfishCfb64 game, string publicHex) =>
        (this.initial, this.game, this.publicHex) = (initial, game, publicHex);

    internal static async Task<ExactGameClient5165> AcceptAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] encryptedPrefix = new byte[15];
        await stream.ReadExactlyAsync(encryptedPrefix, token);
        byte[] plaintextPrefix = new byte[15];
        using (var prefixCipher = new BlowfishCfb64("DR654dt34trg4UI6"u8))
            prefixCipher.DecryptClient(encryptedPrefix, plaintextPrefix);
        int total = checked(11 + BinaryPrimitives.ReadInt32LittleEndian(plaintextPrefix.AsSpan(11)));
        if (total is < 64 or > 1024) throw new InvalidDataException("The exact Game hello length is invalid.");
        byte[] encryptedHello = new byte[total];
        encryptedPrefix.CopyTo(encryptedHello, 0);
        await stream.ReadExactlyAsync(encryptedHello.AsMemory(encryptedPrefix.Length), token);
        CryptographicOperations.ZeroMemory(plaintextPrefix);
        CryptographicOperations.ZeroMemory(encryptedPrefix);
        try { return Accept(encryptedHello); }
        finally { CryptographicOperations.ZeroMemory(encryptedHello); }
    }

    internal byte[] CreateDhReply()
    {
        byte[] key = Encoding.ASCII.GetBytes(publicHex);
        byte[] plaintext = new byte[7 + 4 + 4 + 12 + 4 + key.Length + 8];
        for (int index = 0; index < 7; index++) plaintext[index] = (byte)(0xa0 + index);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(7), plaintext.Length - 7);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(11), 12);
        for (int index = 0; index < 12; index++) plaintext[15 + index] = (byte)(0xb0 + index);
        int offset = 27;
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(offset), key.Length); offset += 4;
        key.CopyTo(plaintext.AsSpan(offset)); offset += key.Length;
        "TQClient"u8.CopyTo(plaintext.AsSpan(offset));
        byte[] encrypted = new byte[plaintext.Length];
        initial.EncryptServer(plaintext, encrypted);
        CryptographicOperations.ZeroMemory(plaintext);
        CryptographicOperations.ZeroMemory(key);
        return encrypted;
    }

    internal byte[] EncryptClientPacket(ReadOnlySpan<byte> packet)
    {
        byte[] plaintext = new byte[packet.Length + 8];
        packet.CopyTo(plaintext);
        "TQClient"u8.CopyTo(plaintext.AsSpan(packet.Length));
        byte[] encrypted = new byte[plaintext.Length];
        game.EncryptServer(plaintext, encrypted);
        CryptographicOperations.ZeroMemory(plaintext);
        return encrypted;
    }

    internal async Task<byte[]> ReadServerPacketAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] encryptedHeader = new byte[4];
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(encryptedHeader, token);
        game.DecryptClient(encryptedHeader, header);
        int length = BinaryPrimitives.ReadUInt16LittleEndian(header);
        if (length is < 4 or > 4096) throw new InvalidDataException("The server packet length is invalid.");
        byte[] encryptedTail = new byte[length - 4 + 8];
        byte[] tail = new byte[encryptedTail.Length];
        try
        {
            await stream.ReadExactlyAsync(encryptedTail, token);
            game.DecryptClient(encryptedTail, tail);
            if (!tail.AsSpan(length - 4, 8).SequenceEqual("TQServer"u8))
                throw new InvalidDataException("The server footer is invalid.");
            byte[] packet = new byte[length];
            header.CopyTo(packet, 0);
            tail.AsSpan(0, length - 4).CopyTo(packet.AsSpan(4));
            return packet;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptedHeader);
            CryptographicOperations.ZeroMemory(header);
            CryptographicOperations.ZeroMemory(encryptedTail);
            CryptographicOperations.ZeroMemory(tail);
        }
    }

    private static ExactGameClient5165 Accept(byte[] encryptedHello)
    {
        var initial = new BlowfishCfb64("DR654dt34trg4UI6"u8);
        byte[] hello = new byte[encryptedHello.Length];
        initial.DecryptClient(encryptedHello, hello);
        if (!hello.AsSpan(^8).SequenceEqual("TQServer"u8)) throw new InvalidDataException("Game hello footer invalid.");
        int offset = 15;
        int junk = BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(offset)); offset += 4 + junk;
        byte[] serverEncryptIv = ReadField(hello, ref offset);
        byte[] serverDecryptIv = ReadField(hello, ref offset);
        string primeHex = Encoding.ASCII.GetString(ReadField(hello, ref offset));
        string generatorHex = Encoding.ASCII.GetString(ReadField(hello, ref offset));
        string serverPublicHex = Encoding.ASCII.GetString(ReadField(hello, ref offset));
        BigInteger prime = Parse(primeHex), generator = Parse(generatorHex), serverPublic = Parse(serverPublicHex);
        BigInteger exponent = new(Enumerable.Range(65, 32).Select(value => (byte)value).ToArray(), true, true);
        exponent = 2 + exponent % (prime - 3);
        BigInteger publicValue = BigInteger.ModPow(generator, exponent, prime);
        byte[] shared = BigInteger.ModPow(serverPublic, exponent, prime).ToByteArray(true, true);
        var game = new BlowfishCfb64(shared);
        game.SetServerIvs(serverEncryptIv, serverDecryptIv);
        CryptographicOperations.ZeroMemory(shared);
        CryptographicOperations.ZeroMemory(serverEncryptIv);
        CryptographicOperations.ZeroMemory(serverDecryptIv);
        CryptographicOperations.ZeroMemory(hello);
        return new(initial, game, Convert.ToHexString(publicValue.ToByteArray(true, true)));
    }

    private static byte[] ReadField(byte[] source, ref int offset)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset)); offset += 4;
        if (length is < 1 or > 256 || offset + length > source.Length - 8)
            throw new InvalidDataException("A Game hello field is invalid.");
        byte[] value = source.AsSpan(offset, length).ToArray(); offset += length; return value;
    }

    private static BigInteger Parse(string hex) =>
        new(Convert.FromHexString(hex.Length % 2 == 0 ? hex : "0" + hex), true, true);

    public void Dispose() { initial.Dispose(); game.Dispose(); }
}
