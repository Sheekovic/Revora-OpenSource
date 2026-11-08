using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Revora.NativeLogin.Core;

public sealed class AccountLogin5165 : IDisposable
{
    public AccountLogin5165(string username, byte[] passwordBytes, string realm) =>
        (Username, PasswordBytes, Realm) = (username, passwordBytes, realm);
    public string Username { get; }
    public byte[] PasswordBytes { get; }
    public string Realm { get; }
    public void Dispose() => CryptographicOperations.ZeroMemory(PasswordBytes);
}

public sealed record AccountConnectSuccess5165(uint AccountWord, uint HandoffWord, string Address, uint Port);

/// <summary>Strict codecs for the exact-client 276/1086 request and 32-byte/1055 success family.</summary>
public static class AccountPackets5165
{
    public const int LoginLength = 276;
    public const ushort LoginPacketId = 1086;
    public const int ConnectSuccessLength = 32;
    public const ushort ConnectPacketId = 1055;

    public static AccountLogin5165 ParseLogin(Span<byte> plaintext)
    {
        if (plaintext.Length != LoginLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(plaintext) != LoginLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(plaintext[2..]) != LoginPacketId)
            throw new InvalidDataException("The Account login frame is invalid.");

        string username = ReadAscii(plaintext.Slice(4, 16), 3, 16);
        byte[] passwordBytes = new byte[16];
        try
        {
            LegacyPasswordCipher5165.Decrypt(plaintext.Slice(132, 16), passwordBytes);
            int passwordTerminator = passwordBytes.AsSpan().IndexOf((byte)0);
            int passwordLength = passwordTerminator < 0 ? passwordBytes.Length : passwordTerminator;
            _ = ReadAscii(passwordBytes.AsSpan(0, passwordLength), 1, 16);
            string realm = ReadAscii(plaintext.Slice(260, 16), 1, 16);
            byte[] ownedPassword = passwordBytes.AsSpan(0, passwordLength).ToArray();
            return new(username, ownedPassword, realm);
        }
        finally { CryptographicOperations.ZeroMemory(passwordBytes); }
    }

    public static void WriteSuccess(Span<byte> destination, ulong wireToken, string address, uint port)
    {
        if (destination.Length != ConnectSuccessLength) throw new ArgumentException("The response must be exactly 32 bytes.", nameof(destination));
        if (port is 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, ConnectSuccessLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], ConnectPacketId);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], wireToken);
        WriteAscii(address, destination.Slice(12, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], port);
    }

    /// <summary>Models the exact 5165 success-handler reads and its pre-connect IPv4 guard.</summary>
    public static AccountConnectSuccess5165 ParseSuccessAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != ConnectSuccessLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != ConnectSuccessLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != ConnectPacketId)
            throw new InvalidDataException("The Account success frame is invalid.");

        ReadOnlySpan<byte> addressField = packet.Slice(12, 16);
        int terminator = addressField.IndexOf((byte)0);
        if (terminator < 1)
            throw new InvalidDataException("The Game address is not terminated.");
        string addressText = Encoding.ASCII.GetString(addressField[..terminator]);
        if (!System.Net.IPAddress.TryParse(addressText, out System.Net.IPAddress? parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new InvalidDataException("The Game address is not a numeric IPv4 address.");

        byte firstOctet = parsed.GetAddressBytes()[0];
        // Exact client instructions at 0x589749-0x589766 reject this conjunction before 0x535187.
        if (firstOctet % 7 == 1 && firstOctet % 23 == 12)
            throw new InvalidDataException("The exact client rejects this Game address before connect.");

        uint port = BinaryPrimitives.ReadUInt32LittleEndian(packet[28..]);
        if (port is 0 or > 65535)
            throw new InvalidDataException("The Game port is invalid.");
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            addressText,
            port);
    }

    private static string ReadAscii(ReadOnlySpan<byte> source, int minimum, int maximum)
    {
        int terminator = source.IndexOf((byte)0);
        ReadOnlySpan<byte> value = terminator < 0 ? source : source[..terminator];
        if (value.Length < minimum || value.Length > maximum) throw new InvalidDataException("A bounded Account field is invalid.");
        foreach (byte current in value)
            if (current is < 0x21 or > 0x7e) throw new InvalidDataException("A bounded Account field contains an invalid character.");
        return Encoding.ASCII.GetString(value);
    }

    private static void WriteAscii(string value, Span<byte> destination)
    {
        int count = Encoding.ASCII.GetByteCount(value);
        if (count < 1 || count >= destination.Length || value.Any(character => character is < '!' or > '~'))
            throw new ArgumentException("The endpoint representation is invalid.", nameof(value));
        Encoding.ASCII.GetBytes(value, destination);
    }
}

/// <summary>Fixed-key RC5-32/12/16 password transform used by the exact 5165 request builder.</summary>
public static class LegacyPasswordCipher5165
{
    private const int Rounds = 12;
    private static readonly byte[] Seed =
    [
        0x3c, 0xdc, 0xfe, 0xe8, 0xc4, 0x54, 0xd6, 0x7e,
        0x16, 0xa6, 0xf8, 0x1a, 0xe8, 0xd0, 0x38, 0xbe,
    ];

    public static void Decrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != 16 || destination.Length < 16) throw new ArgumentException("The password field must be exactly 16 bytes.");
        uint[] subkeys = Expand();
        try
        {
            for (int block = 0; block < 2; block++)
            {
                uint a = BinaryPrimitives.ReadUInt32LittleEndian(source[(block * 8)..]);
                uint b = BinaryPrimitives.ReadUInt32LittleEndian(source[(block * 8 + 4)..]);
                for (int round = Rounds; round > 0; round--)
                {
                    b = RotateRight(b - subkeys[2 * round + 1], a) ^ a;
                    a = RotateRight(a - subkeys[2 * round], b) ^ b;
                }
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(block * 8)..], a - subkeys[0]);
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(block * 8 + 4)..], b - subkeys[1]);
            }
        }
        finally { Array.Clear(subkeys); }
    }

    internal static void EncryptForTest(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != 16 || destination.Length < 16) throw new ArgumentException("The password field must be exactly 16 bytes.");
        uint[] subkeys = Expand();
        try
        {
            for (int block = 0; block < 2; block++)
            {
                uint a = BinaryPrimitives.ReadUInt32LittleEndian(source[(block * 8)..]) + subkeys[0];
                uint b = BinaryPrimitives.ReadUInt32LittleEndian(source[(block * 8 + 4)..]) + subkeys[1];
                for (int round = 1; round <= Rounds; round++)
                {
                    a = RotateLeft(a ^ b, b) + subkeys[2 * round];
                    b = RotateLeft(b ^ a, a) + subkeys[2 * round + 1];
                }
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(block * 8)..], a);
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(block * 8 + 4)..], b);
            }
        }
        finally { Array.Clear(subkeys); }
    }

    private static uint[] Expand()
    {
        uint[] key = new uint[4];
        for (int index = 0; index < key.Length; index++) key[index] = BinaryPrimitives.ReadUInt32LittleEndian(Seed.AsSpan(index * 4));
        uint[] subkeys = new uint[2 * (Rounds + 1)];
        subkeys[0] = 0xb7e15163;
        for (int index = 1; index < subkeys.Length; index++) subkeys[index] = subkeys[index - 1] - 0x61c88647;
        uint a = 0, b = 0;
        for (uint count = 0, i = 0, j = 0; count < 3 * subkeys.Length; count++)
        {
            a = subkeys[i] = RotateLeft(subkeys[i] + a + b, 3);
            b = key[j] = RotateLeft(key[j] + a + b, a + b);
            i = (i + 1) % (uint)subkeys.Length;
            j = (j + 1) % (uint)key.Length;
        }
        Array.Clear(key);
        return subkeys;
    }

    private static uint RotateLeft(uint value, uint count) => BitOperations.RotateLeft(value, (int)(count & 31));
    private static uint RotateRight(uint value, uint count) => BitOperations.RotateRight(value, (int)(count & 31));
}

public sealed record GameConnect5165(uint TokenLow, uint TokenHigh, ushort ClientPatch, string Language, uint Version);

public static class GamePackets5165
{
    public const int ConnectLength = 28;
    public const ushort ConnectPacketId = 1052;

    public static GameConnect5165 ParseConnect(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != ConnectLength || BinaryPrimitives.ReadUInt16LittleEndian(packet) != ConnectLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != ConnectPacketId)
            throw new InvalidDataException("The Game connect frame is invalid.");
        int end = packet.Slice(14, 10).IndexOf((byte)0);
        ReadOnlySpan<byte> language = end < 0 ? packet.Slice(14, 10) : packet.Slice(14, end);
        return new(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]), BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]), Encoding.ASCII.GetString(language),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[24..]));
    }
}
