using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Revora.NativeLogin.Core;

public sealed record Test4iPatchAuditResult(string Sha256, uint EntryPoint, uint CounterAddress,
    string EventFilename, string MarkerFilename);

public static class Test4iPatchAudit5165
{
    public const string ExpectedSha256 = "6bb9c92f285cada26208fc625a12c45c7185e99482032a51c5c1de5a019618dc";
    private const int EventWriterOffset = 0x20C600;
    private const int EntryWrapperOffset = 0x20C97B;
    private const uint EventCounterAddress = 0x00695300;

    public static Test4iPatchAuditResult ValidateExactExecutable(string path)
    {
        byte[] image = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        if (hash != ExpectedSha256) throw new InvalidDataException("The Test 4i executable hash is not exact.");
        byte[] counterOperand = [0x68, 0x00, 0x53, 0x69, 0x00];
        if (!image.AsSpan(EventWriterOffset + 0x16, 5).SequenceEqual(counterOperand))
            throw new InvalidDataException("The event counter operand is not the reviewed writable address.");
        Section counter = FindSection(image, EventCounterAddress);
        if (!counter.Writable || counter.Executable)
            throw new InvalidDataException("The event counter is not isolated in writable non-executable image storage.");
        byte[] wrapper = [0xE8,0x80,0xFF,0xFF,0xFF,0x6A,0x01,0xE8,0x79,0xFC,0xFF,0xFF,0x83,0xC4,0x04,
            0x83,0xC4,0x04,0x55,0x89,0xE5,0x6A,0xFF,0xE9,0xD6,0x0C,0xF9,0xFF];
        if (!image.AsSpan(EntryWrapperOffset, wrapper.Length).SequenceEqual(wrapper))
            throw new InvalidDataException("The marker/event/entry control-flow graph differs from the reviewed payload.");
        string eventName = ReadAscii(image, 0x20C83F), markerName = ReadAscii(image, 0x20C997);
        if (eventName != ".revora-owner-test/test4i-events.bin" || markerName != ".revora-owner-test/test4i-startup.bin")
            throw new InvalidDataException("The managed reader and exact client writer paths differ.");
        if (Encoding.ASCII.GetString(image).Contains("test4h-events.bin", StringComparison.Ordinal))
            throw new InvalidDataException("A superseded Test 4h event target remains embedded.");
        uint entry = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(PeOffset(image) + 40));
        return new(hash, entry, EventCounterAddress, eventName, markerName);
    }

    private static string ReadAscii(byte[] image, int offset)
    {
        int end = Array.IndexOf(image, (byte)0, offset);
        if (end < 0 || end - offset > 128) throw new InvalidDataException("A diagnostic filename is not bounded and terminated.");
        return Encoding.ASCII.GetString(image, offset, end - offset);
    }

    private static int PeOffset(byte[] image)
    {
        if (image.Length < 0x100 || image[0] != 'M' || image[1] != 'Z') throw new InvalidDataException("Not a PE image.");
        int pe = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
        if (pe < 0 || pe > image.Length - 256 || !image.AsSpan(pe, 4).SequenceEqual("PE\0\0"u8))
            throw new InvalidDataException("Invalid PE header.");
        return pe;
    }

    private static Section FindSection(byte[] image, uint absoluteAddress)
    {
        int pe = PeOffset(image); ushort count = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 6));
        ushort optional = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 20));
        uint imageBase = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(pe + 52));
        uint rva = checked(absoluteAddress - imageBase); int section = pe + 24 + optional;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> header = image.AsSpan(section + index * 40, 40);
            uint virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
            uint virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            uint characteristics = BinaryPrimitives.ReadUInt32LittleEndian(header[36..]);
            if (rva >= virtualAddress && rva < virtualAddress + virtualSize)
                return new((characteristics & 0x80000000) != 0, (characteristics & 0x20000000) != 0);
        }
        throw new InvalidDataException("The event counter is outside mapped image storage.");
    }

    private sealed record Section(bool Writable, bool Executable);
}
