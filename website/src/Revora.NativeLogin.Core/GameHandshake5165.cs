using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Revora.NativeLogin.Core;

public enum GameHandshakeState5165
{
    AwaitClientConnection,
    SendServerDh,
    AwaitClientDh,
    DeriveSharedSecret,
    ActivateBlowfish,
    AwaitGameAuthentication,
    Completed,
    Failed,
}

public sealed record GameHandshakeResult5165(
    GameHandshakeState5165 State,
    int ServerHandshakeLength,
    int? ClientDhReplyLength,
    bool SharedSecretDerived,
    bool BlowfishActivated,
    int? GamePacketLength,
    int? GamePacketId,
    string FooterValidation,
    string? FailureCategory);

/// <summary>The single canonical Game DH/Blowfish candidate supported by M2-H1.</summary>
public sealed class GameHandshake5165 : IDisposable
{
    public const string CandidateId = "5165-dh-blowfish-cfb64-v1";
    public const string PrimeHex = "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";
    public const string GeneratorHex = "05";
    private static readonly byte[] DefaultKey = "DR654dt34trg4UI6"u8.ToArray();
    private static readonly byte[] ServerFooter = "TQServer"u8.ToArray();
    private static readonly byte[] ClientFooter = "TQClient"u8.ToArray();
    private static readonly BigInteger Prime = ParseUnsignedHex(PrimeHex);
    private static readonly BigInteger Generator = ParseUnsignedHex(GeneratorHex);
    private readonly BlowfishCfb64 initialCipher = new(DefaultKey);
    private readonly byte[] serverEncryptIv = new byte[8];
    private readonly byte[] serverDecryptIv = new byte[8];
    private readonly byte[] privateBytes = new byte[32];
    private readonly List<byte> clientDhPlaintext = [];
    private readonly List<byte> gamePlaintext = [];
    private BigInteger privateExponent;
    private BlowfishCfb64? gameCipher;
    private int? expectedClientDhLength;
    private int? expectedGameWireLength;
    private int serverHandshakeLength;
    private int? clientDhReplyLength;
    private int? gamePacketLength;
    private int? gamePacketId;
    private string footerValidation = "not-validated";
    private string? failureCategory;
    private GameConnect5165? connect;

    public GameHandshake5165(ReadOnlySpan<byte> deterministicPrivate = default,
        ReadOnlySpan<byte> deterministicServerEncryptIv = default,
        ReadOnlySpan<byte> deterministicServerDecryptIv = default)
    {
        State = GameHandshakeState5165.AwaitClientConnection;
        FillSecret(privateBytes, deterministicPrivate);
        privateExponent = new BigInteger(privateBytes, isUnsigned: true, isBigEndian: true);
        privateExponent = 2 + (privateExponent % (Prime - 3));
        FillSecret(serverEncryptIv, deterministicServerEncryptIv);
        FillSecret(serverDecryptIv, deterministicServerDecryptIv);
    }

    public GameHandshakeState5165 State { get; private set; }
    public int? ExpectedClientDhWireLength => expectedClientDhLength;
    public GameConnect5165? Connect => connect;

    public byte[] EncryptServerPacket(ReadOnlySpan<byte> packet)
    {
        if (State != GameHandshakeState5165.Completed || gameCipher is null)
            throw new InvalidOperationException("Game authentication must complete before sending packets.");
        byte[] plaintext = new byte[packet.Length + ServerFooter.Length];
        byte[] encrypted = new byte[plaintext.Length];
        try
        {
            packet.CopyTo(plaintext);
            ServerFooter.CopyTo(plaintext.AsSpan(packet.Length));
            gameCipher.EncryptServer(plaintext, encrypted);
            return encrypted;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void DecryptAuthenticatedClientBytes(ReadOnlySpan<byte> encrypted, Span<byte> plaintext)
    {
        if (State != GameHandshakeState5165.Completed || gameCipher is null)
            throw new InvalidOperationException("Game authentication must complete before decrypting packets.");
        if (encrypted.Length != plaintext.Length) throw new ArgumentException("Cipher buffers must have equal lengths.");
        gameCipher.DecryptClient(encrypted, plaintext);
    }

    public byte[] CreateServerHandshake(ReadOnlySpan<byte> deterministicJunk = default)
    {
        Require(GameHandshakeState5165.AwaitClientConnection);
        State = GameHandshakeState5165.SendServerDh;
        byte[] junk = new byte[23];
        if (deterministicJunk.IsEmpty) RandomNumberGenerator.Fill(junk);
        else if (deterministicJunk.Length == junk.Length) deterministicJunk.CopyTo(junk);
        else throw new ArgumentException("Deterministic junk must be exactly 23 bytes.", nameof(deterministicJunk));

        string publicHex = ToUnsignedHex(BigInteger.ModPow(Generator, privateExponent, Prime));
        int total = 36 + junk.Length + 8 + 8 + PrimeHex.Length + GeneratorHex.Length + publicHex.Length;
        byte[] plaintext = new byte[total];
        try
        {
            int offset = 0;
            junk.AsSpan(0, 11).CopyTo(plaintext); offset += 11;
            BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(offset), total - 11); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(offset), 12); offset += 4;
            junk.AsSpan(11, 12).CopyTo(plaintext.AsSpan(offset)); offset += 12;
            WriteField(plaintext, ref offset, serverEncryptIv);
            WriteField(plaintext, ref offset, serverDecryptIv);
            WriteField(plaintext, ref offset, Encoding.ASCII.GetBytes(PrimeHex));
            WriteField(plaintext, ref offset, Encoding.ASCII.GetBytes(GeneratorHex));
            WriteField(plaintext, ref offset, Encoding.ASCII.GetBytes(publicHex));
            ServerFooter.CopyTo(plaintext.AsSpan(offset)); offset += ServerFooter.Length;
            if (offset != total) throw new InvalidOperationException("The canonical handshake length is inconsistent.");
            byte[] encrypted = new byte[total];
            initialCipher.EncryptServer(plaintext, encrypted);
            serverHandshakeLength = total;
            State = GameHandshakeState5165.AwaitClientDh;
            return encrypted;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(junk);
        }
    }

    public bool ConsumeClientDh(ReadOnlySpan<byte> encrypted)
    {
        Require(GameHandshakeState5165.AwaitClientDh);
        if (encrypted.IsEmpty) return false;
        if (clientDhPlaintext.Count + encrypted.Length > 1024) return Fail("client-dh-oversized");
        byte[] plaintext = new byte[encrypted.Length];
        try
        {
            initialCipher.DecryptClient(encrypted, plaintext);
            clientDhPlaintext.AddRange(plaintext);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }

        if (clientDhPlaintext.Count >= 11 && expectedClientDhLength is null)
        {
            int declared = BinaryPrimitives.ReadInt32LittleEndian(CollectionsMarshal.AsSpan(clientDhPlaintext).Slice(7, 4));
            int total = checked(7 + declared);
            if (declared < 16 || total > 1024) return Fail("client-dh-length-invalid");
            expectedClientDhLength = total;
        }
        if (expectedClientDhLength is null || clientDhPlaintext.Count < expectedClientDhLength) return false;
        if (clientDhPlaintext.Count != expectedClientDhLength) return Fail("client-dh-coalesced-transition-unsupported");
        return CompleteClientDh(CollectionsMarshal.AsSpan(clientDhPlaintext));
    }

    public bool ConsumeEncryptedGame(ReadOnlySpan<byte> encrypted)
    {
        Require(GameHandshakeState5165.AwaitGameAuthentication);
        if (encrypted.IsEmpty) return false;
        if (gamePlaintext.Count + encrypted.Length > 4096 + ClientFooter.Length) return Fail("game-frame-oversized");
        byte[] plaintext = new byte[encrypted.Length];
        try
        {
            gameCipher!.DecryptClient(encrypted, plaintext);
            gamePlaintext.AddRange(plaintext);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        if (gamePlaintext.Count >= 4 && expectedGameWireLength is null)
        {
            int declared = BinaryPrimitives.ReadUInt16LittleEndian(CollectionsMarshal.AsSpan(gamePlaintext));
            if (declared != GamePackets5165.ConnectLength) return Fail("unexpected-first-game-packet-length");
            expectedGameWireLength = declared + ClientFooter.Length;
        }
        if (expectedGameWireLength is null || gamePlaintext.Count < expectedGameWireLength) return false;
        if (gamePlaintext.Count != expectedGameWireLength) return Fail("coalesced-post-auth-data-rejected");
        Span<byte> frame = CollectionsMarshal.AsSpan(gamePlaintext);
        if (!frame[^ClientFooter.Length..].SequenceEqual(ClientFooter)) return Fail("client-footer-invalid");
        footerValidation = "tqclient-valid";
        gamePacketLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        gamePacketId = BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]);
        if (gamePacketId != GamePackets5165.ConnectPacketId) return Fail("unexpected-first-game-packet-id");
        connect = GamePackets5165.ParseConnect(frame[..GamePackets5165.ConnectLength]);
        State = GameHandshakeState5165.Completed;
        return true;
    }

    public GameHandshakeResult5165 Snapshot() => new(State, serverHandshakeLength, clientDhReplyLength,
        gameCipher is not null, State is GameHandshakeState5165.AwaitGameAuthentication or GameHandshakeState5165.Completed,
        gamePacketLength, gamePacketId, footerValidation, failureCategory);

    private bool CompleteClientDh(Span<byte> packet)
    {
        if (!packet[^ClientFooter.Length..].SequenceEqual(ClientFooter)) return Fail("client-dh-footer-invalid");
        int junkLength = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(11, 4));
        if (junkLength < 0 || junkLength > 256) return Fail("client-dh-junk-length-invalid");
        int offset = checked(15 + junkLength);
        if (offset + 4 > packet.Length - ClientFooter.Length) return Fail("client-dh-truncated");
        int keyLength = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(offset, 4)); offset += 4;
        if (keyLength < 1 || keyLength > 128 || offset + keyLength != packet.Length - ClientFooter.Length)
            return Fail("client-public-key-length-invalid");
        ReadOnlySpan<byte> keyText = packet.Slice(offset, keyLength);
        if (!IsHex(keyText)) return Fail("client-public-key-encoding-invalid");
        BigInteger peer = ParseUnsignedHex(Encoding.ASCII.GetString(keyText));
        if (peer < 2 || peer > Prime - 2) return Fail("client-public-key-range-invalid");

        State = GameHandshakeState5165.DeriveSharedSecret;
        BigInteger shared = BigInteger.ModPow(peer, privateExponent, Prime);
        privateExponent = BigInteger.Zero;
        CryptographicOperations.ZeroMemory(privateBytes);
        byte[] sharedBytes = shared.ToByteArray(isUnsigned: true, isBigEndian: true);
        try
        {
            if (sharedBytes.Length is < 1 or > 64) return Fail("shared-secret-length-invalid");
            gameCipher = new BlowfishCfb64(sharedBytes);
            // Server inbound uses the second IV on the wire; server outbound uses the first.
            gameCipher.SetServerIvs(serverDecryptIv, serverEncryptIv);
            clientDhReplyLength = packet.Length;
            State = GameHandshakeState5165.ActivateBlowfish;
            State = GameHandshakeState5165.AwaitGameAuthentication;
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(sharedBytes); }
    }

    private bool Fail(string category)
    {
        failureCategory = category;
        State = GameHandshakeState5165.Failed;
        return false;
    }

    private void Require(GameHandshakeState5165 expected)
    {
        if (State != expected) throw new InvalidOperationException($"Expected state {expected}; actual state {State}.");
    }

    private static void FillSecret(Span<byte> destination, ReadOnlySpan<byte> deterministic)
    {
        if (deterministic.IsEmpty) RandomNumberGenerator.Fill(destination);
        else if (deterministic.Length == destination.Length) deterministic.CopyTo(destination);
        else throw new ArgumentException("The deterministic fixture input has an invalid length.");
    }

    private static void WriteField(Span<byte> destination, ref int offset, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value.Length); offset += 4;
        value.CopyTo(destination[offset..]); offset += value.Length;
    }

    private static bool IsHex(ReadOnlySpan<byte> value)
    {
        foreach (byte current in value)
            if (!((current >= (byte)'0' && current <= (byte)'9') ||
                  (current >= (byte)'A' && current <= (byte)'F') ||
                  (current >= (byte)'a' && current <= (byte)'f'))) return false;
        return true;
    }

    private static BigInteger ParseUnsignedHex(string value) =>
        new(Convert.FromHexString(value.Length % 2 == 0 ? value : "0" + value), isUnsigned: true, isBigEndian: true);

    private static string ToUnsignedHex(BigInteger value) => Convert.ToHexString(value.ToByteArray(isUnsigned: true, isBigEndian: true));

    public void Dispose()
    {
        initialCipher.Dispose();
        gameCipher?.Dispose();
        CryptographicOperations.ZeroMemory(serverEncryptIv);
        CryptographicOperations.ZeroMemory(serverDecryptIv);
        CryptographicOperations.ZeroMemory(privateBytes);
        privateExponent = BigInteger.Zero;
        CollectionsMarshal.AsSpan(clientDhPlaintext).Clear();
        CollectionsMarshal.AsSpan(gamePlaintext).Clear();
        clientDhPlaintext.Clear(); gamePlaintext.Clear();
    }
}
