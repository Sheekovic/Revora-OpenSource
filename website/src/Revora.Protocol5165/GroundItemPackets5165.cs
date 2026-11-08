using System.Buffers.Binary;

namespace Revora.Protocol5165;

public readonly record struct GroundItemAction5165(uint GroundUid, uint ItemType, ushort X,
    ushort Y, ushort Category, ushort Action);

/// <summary>
/// Exact 20/1101 layout emitted and consumed by the authorized 5165 Owner Source.
/// Client pickup requests identify only the ground UID; all other fields remain claims.
/// </summary>
public static class GroundItemPackets5165
{
    public const ushort PacketId = 1101;
    public const int Length = 20;
    public const ushort ItemCategory = 3;
    public const ushort SpawnAction = 1;
    public const ushort RemoveAction = 2;

    public static byte[] Encode(uint groundUid, uint itemType, ushort x, ushort y, ushort action)
    {
        if (groundUid == 0 || itemType == 0 || action is not (SpawnAction or RemoveAction))
            throw new ArgumentOutOfRangeException(nameof(action));
        byte[] packet = new byte[Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), PacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), groundUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), itemType);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14), y);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), ItemCategory);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), action);
        return packet;
    }

    public static GroundItemAction5165 Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != Length || BinaryPrimitives.ReadUInt16LittleEndian(packet) != Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketId)
            throw new InvalidDataException("The patch-5165 ground-item frame is malformed.");
        uint uid = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        uint itemType = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        ushort category = BinaryPrimitives.ReadUInt16LittleEndian(packet[16..]);
        ushort action = BinaryPrimitives.ReadUInt16LittleEndian(packet[18..]);
        if (uid == 0 || category != ItemCategory || action > RemoveAction)
            throw new InvalidDataException("The patch-5165 ground-item semantics are invalid.");
        return new(uid, itemType, BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]), category, action);
    }

    public static uint ParsePickupUid(ReadOnlySpan<byte> packet)
    {
        // The genuine client constructor at 0x581426 is asymmetric with the
        // server spawn/remove layout: it writes the pickup discriminator 3 at
        // byte 18. Genuine production traffic proves the send path stamps an
        // opaque nonzero value at bytes 8..11 after construction, so that field
        // is not semantic input. Coordinates are claims only; the Host validates
        // authoritative session range and ownership.
        if (packet.Length != Length || BinaryPrimitives.ReadUInt16LittleEndian(packet) != Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketId)
            throw new InvalidDataException("The patch-5165 pickup frame is malformed.");
        uint uid = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        if (uid == 0 || BinaryPrimitives.ReadUInt16LittleEndian(packet[16..]) != 0 || packet[18] != ItemCategory ||
            packet[19] != 0)
            throw new InvalidDataException("The patch-5165 pickup semantics are invalid.");
        return uid;
    }
}
