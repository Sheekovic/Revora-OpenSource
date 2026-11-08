using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Revora.ClientEndpoint;

public sealed record ClientState(string Profile, string OriginalSha256, string CurrentSha256, bool Patched, string? Address, int? Port);
public sealed record PatchResult(byte[] Bytes, ClientState Before, string OutputSha256);

public static class EndpointPatch
{
    public const int EntryOffset = 0x11CEB6;
    public const int CaveOffset = 0x7662E4;
    public const int StubSize = 80;
    public const int ClientSize = 10280848;
    public const string GenuineHash = "3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60";
    public const string RevoraHash = "B6D6091CD026757BB9E6400557A44F72BA995D92586618D610861700F362D130";
    private static readonly byte[] Prologue = Convert.FromHexString("5581ECA8000000");
    private static readonly IReadOnlyDictionary<string, string> Profiles = new Dictionary<string, string>
    {
        [GenuineHash] = "Genuine 6711 DX9",
        [RevoraHash] = "Revora 6711 DX9 derivative"
    };

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static string ValidateAddress(string address)
    {
        string[] parts = address.Split('.');
        if (parts.Length != 4 || parts.Any(p => p.Length is < 1 or > 3 || p.Any(c => c is < '0' or > '9') ||
            (p.Length > 1 && p[0] == '0') || !byte.TryParse(p, out _)))
            throw new ArgumentException("Enter a canonical dotted-decimal IPv4 address, for example 192.168.100.200.");
        if (parts[0] == "0" || byte.Parse(parts[0]) >= 224)
            throw new ArgumentException("Use a unicast IPv4 address; unspecified, multicast and reserved broadcast ranges are not supported.");
        return address;
    }

    public static ClientState Inspect(byte[] input) => Inspect(input, Profiles);

    internal static ClientState Inspect(byte[] input, IReadOnlyDictionary<string, string> profiles)
    {
        var layout = ReadLayout(input);
        string current = Hash(input);
        if (profiles.TryGetValue(current, out string? name))
        {
            RequireOriginalSpans(input);
            return new(name, current, current, false, null, null);
        }
        if (!input.AsSpan(EntryOffset, 7).SequenceEqual(Branch(layout.EntryRva, layout.CaveRva)))
            throw new InvalidDataException("Unsupported executable. Only the two documented 6711 DX9 hashes and unchanged outputs from this tool are accepted. 7630/DX8/DX12 are not supported.");
        // Decode only this tool's fixed stub. Never accept arbitrary offsets or patch manifests.
        byte[] addressBytes = [.. input.AsSpan(CaveOffset + 32, 4), .. input.AsSpan(CaveOffset + 39, 4), .. input.AsSpan(CaveOffset + 46, 4), .. input.AsSpan(CaveOffset + 53, 4)];
        int end = Array.IndexOf(addressBytes, (byte)0);
        if (end < 1) throw new InvalidDataException("Invalid patched address.");
        string address = ValidateAddress(Encoding.ASCII.GetString(addressBytes, 0, end));
        int port = BinaryPrimitives.ReadInt32LittleEndian(input.AsSpan(CaveOffset + 59, 4));
        ValidatePort(port);
        if (!input.AsSpan(CaveOffset, StubSize).SequenceEqual(BuildStub(address, port, layout.EntryRva, layout.CaveRva)))
            throw new InvalidDataException("The endpoint stub was modified or belongs to a different tool.");
        byte[] original = RestoreSpans(input);
        string originalHash = Hash(original);
        if (!profiles.TryGetValue(originalHash, out name))
            throw new InvalidDataException("Changes outside the endpoint patch were detected. Restore is refused to preserve unrelated modifications.");
        return new(name, originalHash, current, true, address, port);
    }

    public static PatchResult Patch(byte[] input, string address, int port) => Patch(input, address, port, Profiles);

    internal static PatchResult Patch(byte[] input, string address, int port, IReadOnlyDictionary<string, string> profiles)
    {
        ValidateAddress(address); ValidatePort(port);
        ClientState before = Inspect(input, profiles);
        byte[] output = before.Patched ? RestoreSpans(input) : input.ToArray();
        var layout = ReadLayout(output);
        RequireOriginalSpans(output);
        Branch(layout.EntryRva, layout.CaveRva).CopyTo(output, EntryOffset);
        BuildStub(address, port, layout.EntryRva, layout.CaveRva).CopyTo(output, CaveOffset);
        return new(output, before, Hash(output));
    }

    public static PatchResult Restore(byte[] input) => Restore(input, Profiles);

    internal static PatchResult Restore(byte[] input, IReadOnlyDictionary<string, string> profiles)
    {
        ClientState state = Inspect(input, profiles);
        if (!state.Patched) throw new InvalidOperationException("This executable is already an approved original; no restore is needed.");
        byte[] restored = RestoreSpans(input);
        if (Hash(restored) != state.OriginalSha256) throw new InvalidDataException("Restored hash mismatch.");
        return new(restored, state, state.OriginalSha256);
    }

    internal static byte[] RestoreSpans(byte[] input)
    {
        byte[] output = input.ToArray();
        Prologue.CopyTo(output, EntryOffset);
        output.AsSpan(CaveOffset, StubSize).Clear();
        return output;
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Account port must be between 1 and 65535.");
    }

    private static void RequireOriginalSpans(byte[] input)
    {
        if (!input.AsSpan(EntryOffset, 7).SequenceEqual(Prologue) || input.AsSpan(CaveOffset, StubSize).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("The reviewed prologue or the 80-byte zero-padding span does not match.");
    }

    internal static (int EntryRva, int CaveRva) ReadLayout(byte[] bytes)
    {
        if (bytes.Length != ClientSize || bytes[0] != 'M' || bytes[1] != 'Z') throw new InvalidDataException("Expected the documented 10,280,848-byte 6711 DX9 PE32 executable.");
        int pe = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3c, 4));
        if (pe < 64 || pe > bytes.Length - 256 || !bytes.AsSpan(pe, 4).SequenceEqual("PE\0\0"u8) ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 4, 2)) != 0x14c ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 24, 2)) != 0x10b)
            throw new InvalidDataException("Expected an x86 PE32 image.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 6, 2));
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 20, 2));
        int table = checked(pe + 24 + optionalSize);
        if (count is < 1 or > 96 || optionalSize < 96 || table > bytes.Length - count * 40) throw new InvalidDataException("Invalid PE section table.");
        int ToRva(int offset, int length)
        {
            int? result = null;
            for (int i = 0; i < count; i++)
            {
                int section = table + i * 40;
                uint rawSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(section + 16, 4));
                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(section + 20, 4));
                uint rva = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(section + 12, 4));
                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(section + 36, 4));
                if ((ulong)offset < raw || (ulong)offset + (uint)length > (ulong)raw + rawSize) continue;
                if ((ulong)raw + rawSize > (ulong)bytes.Length || (flags & 0x60000000) != 0x60000000 || result is not null)
                    throw new InvalidDataException("Patch spans must have one file-backed executable/readable section mapping.");
                result = checked((int)((long)rva + offset - raw));
            }
            return result ?? throw new InvalidDataException("Patch span is not inside an executable PE section.");
        }
        return (ToRva(EntryOffset, 7), ToRva(CaveOffset, StubSize));
    }

    private static byte[] Branch(int entryRva, int caveRva)
    {
        byte[] branch = [0xe9, 0, 0, 0, 0, 0x90, 0x90];
        BinaryPrimitives.WriteInt32LittleEndian(branch.AsSpan(1, 4), checked(caveRva - entryRva - 5));
        return branch;
    }

    internal static byte[] BuildStub(string address, int port, int entryRva, int caveRva)
    {
        byte[] paddedAddress = new byte[16]; Encoding.ASCII.GetBytes(address).CopyTo(paddedAddress, 0);
        var code = new List<byte>(StubSize);
        void Hex(string value) => code.AddRange(Convert.FromHexString(value));
        void Int32(int value) { byte[] b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, value); code.AddRange(b); }
        // Fall back for other types, null pointers or a signed capacity below 16.
        // EDX is volatile; ECX (the original this pointer) survives every fallback.
        Hex("837C240402753D8B44240885C07435837C240C107C2E8B54241085D27426C700"); code.AddRange(paddedAddress.AsSpan(0, 4).ToArray());
        Hex("C74004"); code.AddRange(paddedAddress.AsSpan(4, 4).ToArray());
        Hex("C74008"); code.AddRange(paddedAddress.AsSpan(8, 4).ToArray());
        Hex("C7400C"); code.AddRange(paddedAddress.AsSpan(12, 4).ToArray());
        Hex("C702"); Int32(port);
        Hex("B001C21000"); code.AddRange(Prologue);
        Hex("E9"); Int32(checked(entryRva + 7 - (caveRva + StubSize)));
        if (code.Count != StubSize) throw new InvalidOperationException("Unexpected stub length.");
        return code.ToArray();
    }
}
