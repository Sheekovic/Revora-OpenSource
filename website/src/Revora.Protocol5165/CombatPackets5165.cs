using System.Buffers.Binary;
using System.Text;

namespace Revora.Protocol5165;

public readonly record struct MonsterSpawn5165(
    uint Mesh, uint Uid, ushort Health, ushort Level, ushort X, ushort Y,
    byte Direction, byte Action, string Name);

/// <summary>Exact bounded combat/entity layouts reconciled with the approved 5165 handler.</summary>
public static class CombatPackets5165
{
    public const ushort EntitySpawnPacketId = 10014;
    public const uint KillAction = 14;
    public const int MonsterStringPackOffset = 136;
    public const ushort ProficiencyPacketId = 1025;

    /// <summary>
    /// Exact 16/1025 layout consumed by the approved 5165 executable: DWORD
    /// proficiency at +4, level WORD at +8 (the client reads the low WORD), and
    /// experience DWORD at +12.
    /// </summary>
    public static byte[] EncodeProficiency(ushort proficiencyType, byte level, uint experience)
    {
        if (proficiencyType == 0 || level > 20)
            throw new ArgumentOutOfRangeException(nameof(proficiencyType));
        byte[] packet = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ProficiencyPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), proficiencyType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), level);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), experience);
        return packet;
    }

    public static byte[] EncodeMonsterSpawn(uint mesh, uint uid, ushort health, ushort level,
        ushort x, ushort y, byte direction, byte action, string name)
    {
        if (mesh == 0 || uid is <= 400_000 or >= 600_000 || health == 0)
            throw new ArgumentOutOfRangeException(nameof(uid));
        byte[] encodedName = Encoding.ASCII.GetBytes(name);
        if (encodedName.Length is < 1 or > 32 || encodedName.AsSpan().IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new ArgumentOutOfRangeException(nameof(name));
        byte[] packet = new byte[MonsterStringPackOffset + 2 + encodedName.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), EntitySpawnPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), mesh);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(52), health);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(54), level);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(58), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(60), y);
        packet[62] = direction;
        packet[63] = action;
        packet[MonsterStringPackOffset] = 1;
        packet[MonsterStringPackOffset + 1] = checked((byte)encodedName.Length);
        encodedName.CopyTo(packet.AsSpan(MonsterStringPackOffset + 2));
        _ = ParseMonsterSpawnAsExactClient(packet);
        return packet;
    }

    public static MonsterSpawn5165 ParseMonsterSpawnAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MonsterStringPackOffset + 3 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != EntitySpawnPacketId ||
            packet[MonsterStringPackOffset] != 1)
            throw new InvalidDataException("The patch-5165 monster-spawn frame is invalid.");
        int nameLength = packet[MonsterStringPackOffset + 1];
        if (nameLength is < 1 or > 32 || packet.Length != MonsterStringPackOffset + 2 + nameLength)
            throw new InvalidDataException("The patch-5165 monster-spawn string pack is invalid.");
        ReadOnlySpan<byte> name = packet.Slice(MonsterStringPackOffset + 2, nameLength);
        if (name.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new InvalidDataException("The patch-5165 monster name is invalid.");
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[52..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[54..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[58..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[60..]),
            packet[62], packet[63], Encoding.ASCII.GetString(name));
    }
}
