using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Byte emulator independently transcribed from exact-client instructions
/// 0x49171e–0x4919a5 and 0x56b403–0x56b550. It does not call Revora's
/// production registration encoder or parser.
/// </summary>
internal static class ExactClient5165RegistrationBuilder
{
    internal static byte[] BuildSmallMaleTaoist(string name, uint accountWord)
    {
        byte[] packet = new byte[104];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 104);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1001);

        // The exact call at 0x49198e/0x491990 supplies literal 0x68f370,
        // whose first byte is zero, for both Account and reserved fields.
        WriteClientString(name, packet.AsSpan(20, 16));

        // Even sex selector + body selector 1 => 1003 at 0x4918db–0x491928.
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(52), 1003);
        // Class selector 0/1 reaches 0x491937 and writes Taoist 100.
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(54), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(56), accountWord);
        return packet;
    }

    private static void WriteClientString(string value, Span<byte> destination)
    {
        if (value.Length >= destination.Length || value.Any(character => character > 0x7f))
            throw new ArgumentException("The exact-client fixture string is invalid.", nameof(value));
        Encoding.ASCII.GetBytes(value, destination);
    }
}
