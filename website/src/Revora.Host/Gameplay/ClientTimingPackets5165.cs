using System.Buffers.Binary;

namespace Revora.Host.Gameplay;

public readonly record struct ClientClockFields5165(
    int Year,
    int Month,
    int DayOfYear,
    int Day,
    int Hour,
    int Minute,
    int Second);

public static class ClientTimingPackets5165
{
    public const ushort ServerClockPacketId = 1033;
    public const ushort ServerClockLength = 36;
    public const string CairoTimeZoneId = "Africa/Cairo";

    public static TimeZoneInfo ResolveCairoTimeZone() =>
        TimeZoneInfo.FindSystemTimeZoneById(CairoTimeZoneId);

    public static byte[] EncodeServerClock(DateTimeOffset utcNow, TimeZoneInfo cairoTimeZone)
    {
        ArgumentNullException.ThrowIfNull(cairoTimeZone);
        DateTimeOffset cairo = TimeZoneInfo.ConvertTime(utcNow, cairoTimeZone);
        byte[] packet = new byte[ServerClockLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ServerClockLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ServerClockPacketId);
        // Offset 4 is the exact-client action selector. Zero selects the seven-field clock branch.
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), checked((uint)(cairo.Year - 1900)));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), checked((uint)(cairo.Month - 1)));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), checked((uint)cairo.DayOfYear));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), checked((uint)cairo.Day));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), checked((uint)cairo.Hour));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(28), checked((uint)cairo.Minute));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), checked((uint)cairo.Second));
        return packet;
    }

    // This is an independent model of the approved client's 0x590227 receive branch. It deliberately
    // consumes seven UInt32 little-endian calendar fields rather than a Unix value or DateTime ticks.
    public static ClientClockFields5165 ParseAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != ServerClockLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != ServerClockLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != ServerClockPacketId ||
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]) != 0)
            throw new InvalidDataException("The patch-5165 server clock frame is invalid.");

        int year = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]) + 1900);
        int month = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]) + 1);
        int dayOfYear = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]));
        int day = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]));
        int hour = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[24..]));
        int minute = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[28..]));
        int second = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet[32..]));
        if (year is < 1970 or > 9999 || month is < 1 or > 12 || dayOfYear is < 1 or > 366 ||
            day < 1 || day > DateTime.DaysInMonth(year, month) || hour is < 0 or > 23 ||
            minute is < 0 or > 59 || second is < 0 or > 59)
            throw new InvalidDataException("The patch-5165 server clock fields are outside their valid ranges.");
        return new(year, month, dayOfYear, day, hour, minute, second);
    }

    public static uint CalculateClientRttMilliseconds(uint clientSendTick, uint clientReceiveTick) =>
        unchecked(clientReceiveTick - clientSendTick);

    public static bool IsPlausibleClientRtt(uint milliseconds, uint maximumMilliseconds = 60_000) =>
        milliseconds <= maximumMilliseconds;
}
