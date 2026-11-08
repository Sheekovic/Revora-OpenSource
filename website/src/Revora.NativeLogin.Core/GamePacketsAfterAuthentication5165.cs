using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Revora.NativeLogin.Core;

public sealed class CharacterRegistration5165 : IDisposable
{
    internal CharacterRegistration5165(string account, string name, byte[] sensitiveReserved, ushort mesh, ushort profession, uint accountId)
    {
        Account = account;
        Name = name;
        SensitiveReserved = sensitiveReserved;
        Mesh = mesh;
        Profession = profession;
        AccountId = accountId;
    }

    public string Account { get; }
    public string Name { get; }
    // Patch 5165 writes a 16-byte legacy/reserved field here. It may be empty and
    // must never be treated as an authentication authority or persisted.
    public byte[] SensitiveReserved { get; }
    public ushort Mesh { get; }
    public ushort Profession { get; }
    public uint AccountId { get; }

    public void Dispose() => CryptographicOperations.ZeroMemory(SensitiveReserved);
}

public readonly record struct UserInformation5165(
    uint Id,
    uint Mesh,
    ushort Hair,
    uint Silver,
    uint ConquerPoints,
    ulong Experience,
    ushort Strength,
    ushort Agility,
    ushort Vitality,
    ushort Spirit,
    ushort AttributePoints,
    ushort HealthPoints,
    ushort ManaPoints,
    short KillPoints,
    byte Level,
    byte Profession,
    string Name,
    string Spouse);

public readonly record struct GameAction5165(
    uint Id,
    uint Data,
    uint Timestamp,
    ushort Action,
    ushort Direction,
    ushort X,
    ushort Y);

public readonly record struct GameMovement5165(
    uint Direction,
    uint Id,
    uint Mode,
    uint Timestamp);

public readonly record struct NpcInteraction5165(uint Id, uint Data, ushort Event, ushort Type);

public readonly record struct NpcDialogAnswer5165(byte Option, byte Action, string Text);

public readonly record struct GameNpcSpawn5165(
    uint Id, ushort X, ushort Y, ushort Look, ushort Type, uint Sort);

public readonly record struct GamePlayerSpawn5165(
    uint Mesh,
    uint Id,
    ulong Status,
    uint Headwear,
    uint Garment,
    uint Armor,
    uint LeftHand,
    uint RightHand,
    uint Steed,
    ushort ArmorColor,
    ushort LeftHandColor,
    ushort HeadwearColor,
    ushort Hair,
    ushort X,
    ushort Y,
    byte Direction,
    byte Action,
    ushort Level,
    string Name);

public readonly record struct GamePlayerAppearance5165(
    ulong Status = 0,
    uint Headwear = 0,
    uint Garment = 0,
    uint Armor = 0,
    uint LeftHand = 0,
    uint RightHand = 0,
    uint Steed = 0,
    ushort ArmorColor = 0,
    ushort LeftHandColor = 0,
    ushort HeadwearColor = 0,
    byte Rebirth = 0,
    byte EntityType = 0);

public static class GamePacketsAfterAuthentication5165
{
    public const ushort RegisterPacketId = 1001;
    public const int RegisterLength = 104;
    public const ushort TalkPacketId = 1004;
    public const ushort UserInfoPacketId = 1006;
    public const ushort ActionPacketId = 10010;
    public const ushort MovementPacketId = 10005;
    public const int MovementPacketLength = 20;
    public const ushort PlayerSpawnPacketId = 10014;
    // The approved 5165 client handler at 0x589c9d classifies entity UIDs below
    // one million through its non-player path. Revora's early immutable character
    // UIDs predate that wire requirement, so a canary peer presentation uses this
    // deterministic display-only namespace without changing database identity.
    public const uint MinimumPlayerEntityUid = 1_000_000;
    public const ushort NpcSpawnPacketId = 2030;
    public const ushort NpcInteractionPacketId = 2031;
    public const ushort NpcDialogPacketId = 2032;
    public const int PlayerSpawnStringOffset = 136;
    public const int ActionPacketLength = 32;
    public const uint JumpTrailingSentinel = uint.MaxValue;
    public const ushort MapInfoPacketId = 1110;
    public const ushort RegistrationChannel = 2100;
    public const ushort EntranceChannel = 2101;
    public const ushort EnterMapAction = 74;
    public const ushort PortalAction = 85;
    public const ushort FlyMapAction = 86;
    public const ushort RemoveEntityAction = 135;
    public const ushort JumpAction = 137;
    public const ushort OpenShopAction = 113;
    public const ushort OpenWarehouseAction = 126;
    public const int UserInfoStringPackOffset = 87;
    public const int NpcSpawnPacketLength = 28;
    public const int NpcInteractionPacketLength = 16;
    public const int NpcDialogCloseLength = 16;
    public const int NpcDialogMinimumLength = 16;

    public static uint ToPlayerPresentationUid(uint immutableLegacyUid)
    {
        if (immutableLegacyUid == 0) throw new ArgumentOutOfRangeException(nameof(immutableLegacyUid));
        return immutableLegacyUid >= MinimumPlayerEntityUid
            ? immutableLegacyUid
            : checked(MinimumPlayerEntityUid + immutableLegacyUid);
    }

    private static readonly HashSet<ushort> ValidMeshes = [1003, 1004, 2001, 2002];
    // Exact-client jump table at 0x4919eb reaches 100, 10, 40, 20, and 50.
    private static readonly HashSet<ushort> ValidProfessions = [10, 20, 40, 50, 100];

    public static CharacterRegistration5165 ParseRegistration(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != RegisterLength || BinaryPrimitives.ReadUInt16LittleEndian(packet) != RegisterLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != RegisterPacketId)
            throw new InvalidDataException("The character-registration frame is invalid.");

        // The exact 5165 builder passes a fully zeroed Account field from the
        // literal empty string at 0x491990. Authentication authority is the
        // already-consumed Account-to-Game handoff and its Account word.
        if (packet.Slice(4, 16).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The account field is not the exact-client empty representation.");
        string account = ReadFixedAscii(packet.Slice(4, 16), "account", allowEmpty: true);
        string name = ReadFixedAscii(packet.Slice(20, 16), "character name", allowEmpty: false);
        byte[] sensitiveReserved = packet.Slice(36, 16).ToArray();
        try
        {
            return new(account, name, sensitiveReserved,
                BinaryPrimitives.ReadUInt16LittleEndian(packet[52..]),
                BinaryPrimitives.ReadUInt16LittleEndian(packet[54..]),
                BinaryPrimitives.ReadUInt32LittleEndian(packet[56..]));
        }
        catch
        {
            CryptographicOperations.ZeroMemory(sensitiveReserved);
            throw;
        }
    }

    public static bool IsValidMesh(ushort value) => ValidMeshes.Contains(value);
    public static bool IsValidProfession(ushort value) => ValidProfessions.Contains(value);

    public static bool IsValidCharacterName(string value)
    {
        if (value.Length is < 3 or > 15 || value.StartsWith("gm", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("admin", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (char current in value)
            if (!(current is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')) return false;
        return true;
    }

    public static byte[] EncodeTalk(ushort channel, string words)
    {
        string[] values = ["SYSTEM", "ALLUSERS", "", words];
        int length = 25 + values.Sum(value => 1 + Encoding.ASCII.GetByteCount(value));
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), TalkPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x00ffffff);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), channel);
        packet[24] = 4;
        int offset = 25;
        foreach (string value in values)
        {
            packet[offset++] = checked((byte)value.Length);
            offset += Encoding.ASCII.GetBytes(value, packet.AsSpan(offset));
        }
        return packet;
    }

    public static string ParseClientTalkText(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 29 || packet.Length > 512 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != TalkPacketId || packet[24] != 4)
            throw new InvalidDataException("The patch-5165 talk frame is invalid.");

        int offset = 25;
        string message = string.Empty;
        for (int field = 0; field < 4; field++)
        {
            if (offset >= packet.Length) throw new InvalidDataException("The talk string pack is truncated.");
            int length = packet[offset++];
            if (offset + length > packet.Length) throw new InvalidDataException("The talk string is truncated.");
            ReadOnlySpan<byte> value = packet.Slice(offset, length);
            if (value.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0 ||
                value.IndexOfAnyInRange((byte)0, (byte)0x1f) >= 0)
                throw new InvalidDataException("The talk string contains unsupported bytes.");
            if (field == 3) message = Encoding.ASCII.GetString(value);
            offset += length;
        }
        int trailingLength = packet.Length - offset;
        if (trailingLength != 0 &&
            (trailingLength != 3 || packet[offset..].IndexOfAnyExcept((byte)0) >= 0))
            throw new InvalidDataException("The talk string pack has an unsupported reserved tail.");
        return message;
    }

    public static byte[] EncodeUserInfo(uint id, string name, uint look, ushort profession)
        => EncodeUserInfo(id, name, look, profession, 1, 0, 0, 0, 5, 2, 3, 0, 0, 100, 0, 0);

    public static byte[] EncodeUserInfo(uint id, string name, uint look, ushort profession, byte level,
        uint silver, uint conquerPoints, ulong experience, ushort strength, ushort agility, ushort vitality,
        ushort spirit, ushort attributePoints, ushort health, ushort mana, short pkPoints)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        byte[] spouseBytes = "None"u8.ToArray();
        if (nameBytes.Length is < 1 or > 15 || profession > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(name), "The exact-client user information fields are out of range.");

        // Exact-client constructor 0x592056 registers its string pack at raw
        // packet offset 0x57 (87). The handler at 0x592135 reads the fixed
        // region through offset 86 before it resolves both packed strings.
        byte[] packet = new byte[UserInfoStringPackOffset + 3 + nameBytes.Length + spouseBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), UserInfoPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), look);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 410);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(14), silver);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(18), conquerPoints);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(22), experience);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(50), strength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(52), agility);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(54), vitality);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(56), spirit);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(58), attributePoints);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(60), health);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(62), mana);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(64), pkPoints);
        packet[66] = level;
        packet[67] = checked((byte)profession);
        packet[70] = 1;
        int offset = UserInfoStringPackOffset;
        packet[offset++] = 2;
        packet[offset++] = checked((byte)nameBytes.Length);
        nameBytes.CopyTo(packet, offset);
        offset += nameBytes.Length;
        packet[offset++] = checked((byte)spouseBytes.Length);
        spouseBytes.CopyTo(packet, offset);
        _ = ParseUserInfoAsExactClient(packet);
        return packet;
    }

    public static UserInformation5165 ParseUserInfoAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < UserInfoStringPackOffset + 3 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != UserInfoPacketId)
            throw new InvalidDataException("The exact-client user information frame is invalid.");

        int offset = UserInfoStringPackOffset;
        if (packet[offset++] != 2)
            throw new InvalidDataException("The exact-client user information string count is invalid.");
        string name = ReadPackedAscii(packet, ref offset, "character name", allowEmpty: false);
        string spouse = ReadPackedAscii(packet, ref offset, "spouse", allowEmpty: false);
        if (offset != packet.Length)
            throw new InvalidDataException("The exact-client user information frame has trailing data.");

        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[14..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[18..]),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[22..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[50..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[52..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[54..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[56..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[58..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[60..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[62..]),
            BinaryPrimitives.ReadInt16LittleEndian(packet[64..]),
            packet[66], packet[67], name, spouse);
    }

    public static GameAction5165 ParseAction(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != ActionPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != ActionPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != ActionPacketId)
            throw new InvalidDataException("The action frame is invalid.");
        ushort action = BinaryPrimitives.ReadUInt16LittleEndian(packet[16..]);
        uint trailing0 = BinaryPrimitives.ReadUInt32LittleEndian(packet[24..]);
        uint trailing1 = BinaryPrimitives.ReadUInt32LittleEndian(packet[28..]);
        // The exact 5165 special-action builder at 0x54da8c is used for both
        // jump (caller 0x517202) and portal activation (caller 0x518082). Both
        // pass a zero first trailing word and -1 as the final argument. Generic
        // actions built at 0x54d9f8 retain the all-zero reserved tail.
        if (action is JumpAction or PortalAction
            ? trailing0 != 0 || trailing1 != JumpTrailingSentinel
            : trailing0 != 0 || trailing1 != 0)
            throw new InvalidDataException("The action trailing fields are invalid.");
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]),
            action,
            BinaryPrimitives.ReadUInt16LittleEndian(packet[18..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[20..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[22..]));
    }

    public static GameMovement5165 ParseMovement(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != MovementPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != MovementPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != MovementPacketId ||
            packet.Slice(5, 3).IndexOfAnyExcept((byte)0) >= 0 ||
            packet.Slice(13, 3).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The patch-5165 movement frame is invalid.");

        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]));
    }

    public static byte[] EncodeMovement(uint direction, uint id, uint mode, uint timestamp)
    {
        if (direction > byte.MaxValue || mode > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(direction));
        byte[] packet = new byte[MovementPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, MovementPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), MovementPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), direction);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), mode);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), timestamp);
        return packet;
    }

    public static byte[] EncodeJump(uint id, ushort destinationX, ushort destinationY,
        ushort currentX, ushort currentY, ushort direction, uint timestamp)
    {
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8),
            (uint)destinationX | ((uint)destinationY << 16));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), JumpAction);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), direction);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(20), currentX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(22), currentY);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(28), JumpTrailingSentinel);
        return packet;
    }

    public static byte[] EncodePositionCorrection(uint id, ushort currentX, ushort currentY, uint timestamp)
    {
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), 108);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(20), currentX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(22), currentY);
        return packet;
    }

    public static byte[] EncodeNpcWindowAction(uint characterUid, uint data, ushort action, uint timestamp = 0)
    {
        // The exact 5165 32/10010 dispatcher consumes actions 113 and 126. The
        // authorized owner donor identifies action 113 as OpenShop (Data=shop/NPC UID)
        // and Warehouseman UID 8 emits action 126 with Data=4 before 1009/1102.
        if (characterUid == 0 || action is not (OpenShopAction or OpenWarehouseAction))
            throw new ArgumentOutOfRangeException(nameof(action));
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), characterUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), data);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), action);
        return packet;
    }

    public static byte[] EncodeEntityAction(uint characterUid, ushort x, ushort y, ushort action,
        uint timestamp = 0)
    {
        if (characterUid == 0) throw new ArgumentOutOfRangeException(nameof(characterUid));
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), characterUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), action);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(20), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(22), y);
        return packet;
    }

    public static byte[] EncodeFlyMap(uint id, int documentId, int x, int y, short direction, uint timestamp)
    {
        byte[] packet = EncodeEnterMap(id, documentId, x, y, direction, timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), FlyMapAction);
        return packet;
    }

    public static NpcInteraction5165 ParseNpcInteraction(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != NpcInteractionPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != NpcInteractionPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != NpcInteractionPacketId)
            throw new InvalidDataException("The patch-5165 NPC interaction frame is invalid.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]));
    }

    public static NpcDialogAnswer5165 ParseNpcDialogAnswer(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < NpcDialogCloseLength or > 272 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != NpcDialogPacketId)
            throw new InvalidDataException("The patch-5165 NPC dialog answer is invalid.");

        byte option = packet[10];
        byte action = packet[11];
        // Exact-client close path 0x41b265 calls constructor 0x54497a with an
        // empty packed-string collection, option 0, and action 102. Serializer
        // 0x599659 returns one byte for the empty collection's count, so the
        // constructor's declared size is 1 + 15 = 16. This follows a selected
        // Goodbye option and is intentionally idempotent after the dialog has
        // already been cleared.
        if (action == 102)
        {
            if (packet.Length != NpcDialogCloseLength || option != 0 ||
                packet[12..].IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("The patch-5165 NPC dialog close is invalid.");
            return new(option, action, string.Empty);
        }
        if (action != 101 || packet.Length < NpcDialogMinimumLength)
            throw new InvalidDataException("The patch-5165 NPC dialog selection action is invalid.");
        int offset = 12;
        int count = packet[offset++];
        if (count > 1) throw new InvalidDataException("The NPC dialog answer string count is invalid.");
        string text = string.Empty;
        if (count == 1) text = ReadPackedAscii(packet, ref offset, "NPC dialog answer", allowEmpty: true);
        if (packet[offset..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The NPC dialog answer has nonzero reserved bytes.");
        return new(option, action, text);
    }

    public static byte[] EncodeNpcSpawn(uint id, ushort x, ushort y, ushort look, ushort type, uint sort = 1)
    {
        byte[] packet = new byte[NpcSpawnPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, NpcSpawnPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), NpcSpawnPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), y);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), look);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14), type);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), sort);
        _ = ParseNpcSpawnAsExactClient(packet);
        return packet;
    }

    public static GameNpcSpawn5165 ParseNpcSpawnAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != NpcSpawnPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != NpcSpawnPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != NpcSpawnPacketId ||
            packet[20..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The exact-client NPC spawn frame is invalid.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]));
    }

    public static byte[] EncodeNpcDialogText(string text) => EncodeNpcDialogString(text, byte.MaxValue, 1);

    public static byte[] EncodeNpcDialogOption(string text, byte option) => EncodeNpcDialogString(text, option, 2);

    public static byte[] EncodeNpcDialogFace(ushort face)
    {
        byte[] packet = new byte[NpcDialogMinimumLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, NpcDialogMinimumLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), NpcDialogPacketId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), face);
        packet[10] = byte.MaxValue;
        packet[11] = 4;
        return packet;
    }

    public static byte[] EncodeNpcDialogFinish()
    {
        byte[] packet = new byte[NpcDialogMinimumLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, NpcDialogMinimumLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), NpcDialogPacketId);
        packet[10] = byte.MaxValue;
        packet[11] = 100;
        return packet;
    }

    private static byte[] EncodeNpcDialogString(string text, byte option, byte action)
    {
        ArgumentNullException.ThrowIfNull(text);
        byte[] value = Encoding.ASCII.GetBytes(text);
        if (value.Length is < 1 or > 240 || value.AsSpan().IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new ArgumentOutOfRangeException(nameof(text));
        // Exact client constructor 0x54497a serializes one packed string at
        // offset 12 and declares packed-length + 15, leaving three zero bytes.
        byte[] packet = new byte[17 + value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), NpcDialogPacketId);
        packet[10] = option;
        packet[11] = action;
        packet[12] = 1;
        packet[13] = checked((byte)value.Length);
        value.CopyTo(packet.AsSpan(14));
        return packet;
    }

    public static byte[] EncodeRemoveEntity(uint id)
    {
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), RemoveEntityAction);
        return packet;
    }

    public static byte[] EncodePlayerSpawn(uint id, string name, uint mesh, ushort hair,
        ushort x, ushort y, byte direction, byte action, ushort level,
        GamePlayerAppearance5165 appearance = default)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        if (nameBytes.Length is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(name));

        // Exact-client handler 0x589c9d consumes the fixed region through byte
        // 135 and then the one-string pack. The equipment offsets below are also
        // emitted by the retained 5165 owner source SpawnEntity(Character).
        // Values originate only from the authoritative server-side item state.
        byte[] packet = new byte[PlayerSpawnStringOffset + 2 + nameBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), PlayerSpawnPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), mesh);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), id);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(16), appearance.Status);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), appearance.Headwear);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(28), appearance.Garment);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), appearance.Armor);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(36), appearance.LeftHand);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), appearance.RightHand);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(44), appearance.Steed);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(48), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(56), hair);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(58), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(60), y);
        packet[62] = direction;
        packet[63] = action;
        packet[68] = appearance.Rebirth;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(69), level);
        packet[71] = appearance.EntityType;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(92), appearance.ArmorColor);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(94), appearance.LeftHandColor);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(96), appearance.HeadwearColor);
        packet[PlayerSpawnStringOffset] = 1;
        packet[PlayerSpawnStringOffset + 1] = checked((byte)nameBytes.Length);
        nameBytes.CopyTo(packet.AsSpan(PlayerSpawnStringOffset + 2));
        _ = ParsePlayerSpawnAsExactClient(packet);
        return packet;
    }

    public static GamePlayerSpawn5165 ParsePlayerSpawnAsExactClient(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < PlayerSpawnStringOffset + 3 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PlayerSpawnPacketId ||
            packet[PlayerSpawnStringOffset] != 1)
            throw new InvalidDataException("The patch-5165 player-spawn frame is invalid.");
        int nameLength = packet[PlayerSpawnStringOffset + 1];
        if (nameLength is < 1 or > 15 || packet.Length != PlayerSpawnStringOffset + 2 + nameLength)
            throw new InvalidDataException("The patch-5165 player-spawn name is invalid.");
        ReadOnlySpan<byte> nameBytes = packet.Slice(PlayerSpawnStringOffset + 2, nameLength);
        if (nameBytes.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new InvalidDataException("The patch-5165 player-spawn name is invalid.");
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[36..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[40..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[44..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[92..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[94..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[96..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[56..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[58..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[60..]),
            packet[62], packet[63], BinaryPrimitives.ReadUInt16LittleEndian(packet[69..]),
            Encoding.ASCII.GetString(nameBytes));
    }

    public static byte[] EncodeEnterMap(uint id, int mapId, int x, int y, short direction, uint timestamp = 0)
    {
        byte[] packet = new byte[ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), checked((uint)mapId));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), EnterMapAction);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), checked((ushort)direction));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(20), checked((ushort)x));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(22), checked((ushort)y));
        return packet;
    }

    public static byte[] EncodeMapInfo(int mapId)
    {
        byte[] packet = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), MapInfoPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), checked((uint)mapId));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), checked((uint)mapId));
        return packet;
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> field, string category, bool allowEmpty)
    {
        int terminator = field.IndexOf((byte)0);
        ReadOnlySpan<byte> value = terminator >= 0 ? field[..terminator] : field;
        if ((!allowEmpty && value.IsEmpty) || value.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new InvalidDataException($"The {category} field is invalid.");
        return Encoding.ASCII.GetString(value);
    }

    private static string ReadPackedAscii(ReadOnlySpan<byte> packet, ref int offset, string category, bool allowEmpty)
    {
        if (offset >= packet.Length)
            throw new InvalidDataException($"The {category} field is truncated.");
        int length = packet[offset++];
        if (length > packet.Length - offset)
            throw new InvalidDataException($"The {category} field is truncated.");
        ReadOnlySpan<byte> value = packet.Slice(offset, length);
        offset += length;
        if ((!allowEmpty && value.IsEmpty) || value.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) >= 0)
            throw new InvalidDataException($"The {category} field is invalid.");
        return Encoding.ASCII.GetString(value);
    }

}
