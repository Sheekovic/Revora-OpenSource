using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Independent byte emulator of the approved client's registration builder at
/// 0x49171e–0x4919a5 and packet constructor at 0x56b403–0x56b550.
/// It intentionally does not call Revora's production encoder or parser.
/// </summary>
internal static class ExactClientRegistrationBuilder5165
{
    internal const int DeclaredLength = 104;
    internal const int WireLength = DeclaredLength + 8;
    internal const ushort PacketId = 1001;

    internal static byte[] BuildSmallMaleTaoist(string name, uint accountWord)
    {
        // 0x4918db–0x491928: even sex selector chooses the 1003 family;
        // body selector 1 contributes zero, producing Small Male 1003.
        ushort mesh = DeriveMesh(sexSelector: 0, bodySelector: 1);
        // Jump-table entries 0/1 both target 0x491937 and write profession 100.
        ushort profession = DeriveProfession(classSelector: 0);

        byte[] wire = new byte[WireLength];
        BinaryPrimitives.WriteUInt16LittleEndian(wire, DeclaredLength);
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(2), PacketId);
        // 0x49198e and 0x491990 push the literal empty string at 0x68f370.
        WriteExactClientString(name, wire.AsSpan(20, 16));
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(52), mesh);
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(54), profession);
        BinaryPrimitives.WriteUInt32LittleEndian(wire.AsSpan(56), accountWord);
        "TQClient"u8.CopyTo(wire.AsSpan(DeclaredLength));
        return wire;
    }

    private static ushort DeriveMesh(int sexSelector, int bodySelector)
    {
        int family = (sexSelector & 1) == 0 ? 1003 : 2001;
        return checked((ushort)(family + (bodySelector == 1 ? 0 : 1)));
    }

    private static ushort DeriveProfession(int classSelector) => classSelector switch
    {
        0 or 1 => 100,
        2 or 3 => 10,
        4 or 5 => 40,
        6 or 7 => 20,
        8 or 9 => 50,
        _ => throw new ArgumentOutOfRangeException(nameof(classSelector)),
    };

    private static void WriteExactClientString(string value, Span<byte> destination)
    {
        int length = Encoding.ASCII.GetByteCount(value);
        if (length >= destination.Length || value.Any(character => character > 0x7f))
            throw new ArgumentException("The exact-client fixture string is invalid.", nameof(value));
        Encoding.ASCII.GetBytes(value, destination);
    }
}
