using System.Buffers.Binary;

namespace Revora.Protocol5165;

public readonly record struct SkillAttack5165(
    uint SenderUid,
    uint TargetUid,
    ushort X,
    ushort Y,
    ushort SkillId,
    ushort SkillLevel,
    uint ClientTimestamp);

public readonly record struct SkillTarget5165(uint TargetUid, uint Damage);

public readonly record struct SkillUse5165(
    uint ActorUid,
    ushort X,
    ushort Y,
    ushort SkillId,
    ushort SkillLevel,
    IReadOnlyList<SkillTarget5165> Targets);

public readonly record struct SkillInformation5165(uint Experience, ushort SkillId, ushort SkillLevel);

public readonly record struct NormalAttack5165(
    uint SenderUid,
    uint TargetUid,
    ushort ClaimedTargetX,
    ushort ClaimedTargetY,
    uint Action,
    uint ClientTimestamp);

/// <summary>
/// Strict codecs for the exact approved patch-5165 skill packet family.
/// Declared lengths exclude the separately handled eight-byte TQClient footer.
/// </summary>
public static class NinjaPackets5165
{
    public const ushort InteractionPacketId = 1022;
    public const ushort SkillInformationPacketId = 1103;
    public const ushort SkillUsePacketId = 1105;
    public const uint MagicAction = 24;
    public const uint MeleeAction = 2;
    public const uint RangedAction = 28;
    public const int InteractionLength = 32;
    public const int SkillInformationLength = 12;
    public const int SkillUseHeaderLength = 20;
    public const int SkillTargetLength = 12;

    public static SkillAttack5165 ParseSkillAttack(ReadOnlySpan<byte> packet, uint authenticatedCharacterUid)
    {
        RequireHeader(packet, InteractionLength, InteractionPacketId);
        if (authenticatedCharacterUid == 0) throw new ArgumentOutOfRangeException(nameof(authenticatedCharacterUid));

        uint sender = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        if (sender != authenticatedCharacterUid)
            throw new InvalidDataException("Skill sender does not match the authenticated character.");
        uint action = BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]);
        if (action != MagicAction) throw new InvalidDataException("Interaction is not a skill action.");
        if (packet[28..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Skill reserved bytes are nonzero.");

        uint encodedTarget = BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]);
        ushort encodedX = BinaryPrimitives.ReadUInt16LittleEndian(packet[16..]);
        ushort encodedY = BinaryPrimitives.ReadUInt16LittleEndian(packet[18..]);
        ushort encodedSkill = BinaryPrimitives.ReadUInt16LittleEndian(packet[24..]);
        // The genuine 5165 send constructor supplies action, encoded skill,
        // encoded target and coordinates only. Bytes 26..27 are scratch/padding
        // and varied on the owner run; skill level is authoritative server state.

        ushort skill = unchecked((ushort)(RotateLeft16(
            unchecked((ushort)(encodedSkill ^ 0x915d ^ (ushort)sender)), 3) - 0xeb42));
        ushort x = unchecked((ushort)(RotateLeft16(
            unchecked((ushort)(encodedX ^ (ushort)sender ^ 0x2ed6)), 1) - 0x22ee));
        ushort y = unchecked((ushort)(RotateLeft16(
            unchecked((ushort)(encodedY ^ (ushort)sender ^ 0xb99b)), 5) - 0x8922));
        uint target = unchecked(RotateRight(encodedTarget, 13) ^ 0x5f2d2463u ^ sender);
        target = unchecked(target - 0x746f4ae6u);

        return new SkillAttack5165(sender, target, x, y, skill, 0,
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]));
    }

    public static NormalAttack5165 ParseNormalAttack(ReadOnlySpan<byte> packet, uint authenticatedCharacterUid)
    {
        RequireHeader(packet, InteractionLength, InteractionPacketId);
        if (authenticatedCharacterUid == 0) throw new ArgumentOutOfRangeException(nameof(authenticatedCharacterUid));
        uint sender = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        if (sender != authenticatedCharacterUid)
            throw new InvalidDataException("Attack sender does not match the authenticated character.");
        uint action = BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]);
        if (action is not (MeleeAction or RangedAction))
            throw new InvalidDataException("The normal attack action is unsupported.");
        if (packet[24..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Normal attack reserved bytes are nonzero.");
        uint target = BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]);
        if (target == 0 || target == sender) throw new InvalidDataException("Normal attack target is invalid.");
        return new NormalAttack5165(
            sender,
            target,
            BinaryPrimitives.ReadUInt16LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[18..]),
            action,
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]));
    }

    public static byte[] EncodeNormalAttackResult(
        uint attackerUid,
        uint targetUid,
        ushort targetX,
        ushort targetY,
        uint action,
        uint damage,
        uint timestamp)
    {
        if (attackerUid == 0 || targetUid == 0 || attackerUid == targetUid)
            throw new ArgumentOutOfRangeException(nameof(attackerUid));
        if (action is not (MeleeAction or RangedAction)) throw new ArgumentOutOfRangeException(nameof(action));
        var packet = new byte[InteractionLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, InteractionLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), timestamp);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), attackerUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), targetUid);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), targetX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), targetY);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), action);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), damage);
        return packet;
    }

    public static byte[] EncodeKillResult(uint attackerUid, uint targetUid, ushort targetX,
        ushort targetY, uint timestamp) => EncodeAttackResult(attackerUid, targetUid, targetX,
            targetY, CombatPackets5165.KillAction, 1, timestamp);

    private static byte[] EncodeAttackResult(uint attackerUid, uint targetUid, ushort targetX,
        ushort targetY, uint action, uint damage, uint timestamp)
    {
        if (attackerUid == 0 || targetUid == 0 || attackerUid == targetUid)
            throw new ArgumentOutOfRangeException(nameof(attackerUid));
        if (action is not (MeleeAction or RangedAction or CombatPackets5165.KillAction))
            throw new ArgumentOutOfRangeException(nameof(action));
        var packet = new byte[InteractionLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, InteractionLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), timestamp);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), attackerUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), targetUid);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), targetX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), targetY);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), action);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), damage);
        return packet;
    }

    public static byte[] EncodeSkillUse(
        uint actorUid,
        ushort x,
        ushort y,
        ushort skillId,
        ushort skillLevel,
        IReadOnlyList<SkillTarget5165> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (actorUid == 0 || skillId == 0) throw new ArgumentOutOfRangeException(nameof(actorUid));
        if (targets.Count > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(targets));
        int length = checked(SkillUseHeaderLength + targets.Count * SkillTargetLength);
        if (length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(targets));

        var packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), SkillUsePacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), actorUid);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), y);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), skillId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14), skillLevel);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), checked((uint)targets.Count));
        for (int index = 0; index < targets.Count; index++)
        {
            SkillTarget5165 target = targets[index];
            if (target.TargetUid == 0) throw new ArgumentOutOfRangeException(nameof(targets));
            int offset = SkillUseHeaderLength + index * SkillTargetLength;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), target.TargetUid);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset + 4), target.Damage);
            // offset +8..+11 remains the exact-client-required reserved zero word.
        }
        return packet;
    }

    public static SkillUse5165 ParseSkillUseAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < SkillUseHeaderLength) throw new InvalidDataException("Skill-use packet is truncated.");
        ushort declared = BinaryPrimitives.ReadUInt16LittleEndian(packet);
        if (declared != packet.Length || BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != SkillUsePacketId)
            throw new InvalidDataException("Skill-use header is invalid.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]);
        if (count > byte.MaxValue || packet.Length != checked(SkillUseHeaderLength + (int)count * SkillTargetLength))
            throw new InvalidDataException("Skill-use target count is invalid.");

        var targets = new SkillTarget5165[count];
        for (int index = 0; index < targets.Length; index++)
        {
            int offset = SkillUseHeaderLength + index * SkillTargetLength;
            if (packet.Slice(offset + 8, 4).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("Skill-use target reserved bytes are nonzero.");
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(packet[offset..]);
            if (uid == 0) throw new InvalidDataException("Skill-use target UID is invalid.");
            targets[index] = new SkillTarget5165(uid, BinaryPrimitives.ReadUInt32LittleEndian(packet[(offset + 4)..]));
        }
        return new SkillUse5165(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]),
            targets);
    }

    public static byte[] EncodeSkillInformation(uint experience, ushort skillId, ushort skillLevel)
    {
        if (skillId == 0) throw new ArgumentOutOfRangeException(nameof(skillId));
        var packet = new byte[SkillInformationLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, SkillInformationLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), SkillInformationPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), experience);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), skillId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), skillLevel);
        return packet;
    }

    public static SkillInformation5165 ParseSkillInformation(ReadOnlySpan<byte> packet)
    {
        RequireHeader(packet, SkillInformationLength, SkillInformationPacketId);
        return new SkillInformation5165(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[10..]));
    }

    private static void RequireHeader(ReadOnlySpan<byte> packet, int expectedLength, ushort expectedId)
    {
        if (packet.Length != expectedLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != expectedLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != expectedId)
            throw new InvalidDataException("Packet header is invalid.");
    }

    private static ushort RotateLeft16(ushort value, int count) =>
        unchecked((ushort)((value << count) | (value >> (16 - count))));

    private static uint RotateRight(uint value, int count) => (value >> count) | (value << (32 - count));
}
