namespace Revora.Gameplay.Models;

public enum NinjaProfession : byte
{
    InternNinja = 50,
    Ninja = 51,
    MiddleNinja = 52,
    DarkNinja = 53,
    MysticNinja = 54,
    NinjaMaster = 55
}

public static class NinjaProfessionRules
{
    private static readonly byte[] PromotionLevels = [1, 15, 40, 70, 100, 110];

    public static bool IsNinja(byte profession) => profession is >= 50 and <= 55;

    public static byte RequiredLevel(byte profession)
    {
        if (!IsNinja(profession)) throw new ArgumentOutOfRangeException(nameof(profession));
        return PromotionLevels[profession - 50];
    }

    public static bool CanPromote(byte currentProfession, byte requestedProfession, byte level) =>
        IsNinja(currentProfession) && IsNinja(requestedProfession) && requestedProfession == currentProfession + 1 &&
        level >= RequiredLevel(requestedProfession);

    public static bool IsCompatibleRequirement(byte characterProfession, byte requiredProfession) =>
        requiredProfession == 50 && IsNinja(characterProfession);
}

public static class NinjaItemRules
{
    public const uint MinimumKatanaType = 601000;
    public const uint MaximumKatanaType = 601339;

    public static bool IsKatana(uint itemType) =>
        itemType is >= MinimumKatanaType and <= MaximumKatanaType && itemType / 1000 == 601;

    public static bool IsNinjaHeadwear(uint itemType) =>
        itemType is >= 112000 and <= 112109 || itemType is >= 123000 and <= 123109;

    public static bool IsNinjaArmor(uint itemType) => itemType is >= 135000 and <= 135109;

    public static bool CanEquipKatana(byte profession, EquipmentSlot slot, uint itemType) =>
        NinjaProfessionRules.IsNinja(profession) && IsKatana(itemType) &&
        slot is EquipmentSlot.RightHand or EquipmentSlot.LeftHand;

    public static bool CanEquipClassItem(byte profession, uint itemType) =>
        (!IsKatana(itemType) && !IsNinjaHeadwear(itemType) && !IsNinjaArmor(itemType)) ||
        NinjaProfessionRules.IsNinja(profession);
}

public enum NinjaSkillId : ushort
{
    TwofoldBlades = 6000,
    ToxicFog = 6001,
    PoisonStar = 6002,
    CounterKill = 6003,
    ArcherBane = 6004,
    ShurikenVortex = 6010,
    FatalStrike = 6011
}

public enum NinjaTargetMode : byte
{
    Self = 1,
    Creature = 2,
    Player = 3,
    CreatureOrPlayer = 4
}

public enum NinjaStatusKind : byte
{
    ToxicFog = 1,
    PoisonStar = 2,
    CounterKill = 3,
    ArcherBane = 4,
    ShurikenVortex = 5,
    FatalStrike = 6
}

public sealed record NinjaSkillDefinition(
    NinjaSkillId Id,
    byte MaximumLevel,
    byte RequiredProfession,
    byte RequiredCharacterLevel,
    ushort StaminaCost,
    byte Range,
    int CooldownMilliseconds,
    NinjaTargetMode TargetMode,
    NinjaStatusKind? Status,
    int DurationMilliseconds,
    bool RequiresDualKatana,
    bool MonstersOnly)
{
    public NinjaSkillDefinition Validate()
    {
        if (!NinjaProfessionRules.IsNinja(RequiredProfession) || MaximumLevel > 9 ||
            StaminaCost > 1_000 || Range > 64 || CooldownMilliseconds is < 0 or > 600_000 ||
            DurationMilliseconds is < 0 or > 600_000)
            throw new ArgumentOutOfRangeException(nameof(RequiredProfession));
        return this;
    }
}

public static class NativeNinjaSkillCatalog5165
{
    private static readonly IReadOnlyDictionary<NinjaSkillId, NinjaSkillDefinition> Definitions =
        new Dictionary<NinjaSkillId, NinjaSkillDefinition>
        {
            [NinjaSkillId.TwofoldBlades] = new(NinjaSkillId.TwofoldBlades, 4, 50, 40, 30, 3, 800,
                NinjaTargetMode.CreatureOrPlayer, null, 0, true, false),
            [NinjaSkillId.ToxicFog] = new(NinjaSkillId.ToxicFog, 4, 50, 70, 50, 15, 1_000,
                NinjaTargetMode.CreatureOrPlayer, NinjaStatusKind.ToxicFog, 60_000, false, false),
            [NinjaSkillId.PoisonStar] = new(NinjaSkillId.PoisonStar, 4, 50, 70, 50, 15, 1_000,
                NinjaTargetMode.Player, NinjaStatusKind.PoisonStar, 25_000, false, false),
            [NinjaSkillId.CounterKill] = new(NinjaSkillId.CounterKill, 4, 50, 70, 0, 0, 15_000,
                NinjaTargetMode.Self, NinjaStatusKind.CounterKill, 900_000, false, false),
            [NinjaSkillId.ArcherBane] = new(NinjaSkillId.ArcherBane, 0, 50, 70, 50, 15, 1_000,
                NinjaTargetMode.Player, NinjaStatusKind.ArcherBane, 0, false, false),
            [NinjaSkillId.ShurikenVortex] = new(NinjaSkillId.ShurikenVortex, 0, 50, 70, 0, 0, 0,
                NinjaTargetMode.Self, NinjaStatusKind.ShurikenVortex, 20_000, false, false),
            [NinjaSkillId.FatalStrike] = new(NinjaSkillId.FatalStrike, 0, 50, 70, 0, 0, 0,
                NinjaTargetMode.Self, NinjaStatusKind.FatalStrike, 30_000, false, true)
        };

    public static IReadOnlyCollection<NinjaSkillDefinition> All { get; } = Definitions.Values.ToArray();

    public static bool TryGet(ushort id, out NinjaSkillDefinition definition)
    {
        if (!Enum.IsDefined((NinjaSkillId)id))
        {
            definition = null!;
            return false;
        }
        return Definitions.TryGetValue((NinjaSkillId)id, out definition!);
    }
}

public sealed record NinjaBaseStats(byte Level, ushort Strength, ushort Agility, ushort Vitality, ushort Spirit);

public static class NativeNinjaStats5165
{
    // Checkpoints decoded from the owner-provided patch-5165 Stats.dat. The
    // migration importer owns the complete 120-level table; these values are
    // invariant sentinels used by validation and tests.
    private static readonly IReadOnlyDictionary<byte, NinjaBaseStats> Checkpoints =
        new Dictionary<byte, NinjaBaseStats>
        {
            [1] = new(1, 1, 8, 1, 0),
            [15] = new(15, 7, 43, 6, 0),
            [40] = new(40, 19, 106, 17, 0),
            [70] = new(70, 33, 181, 29, 0),
            [100] = new(100, 48, 256, 41, 0),
            [120] = new(120, 57, 306, 49, 0)
        };

    public static NinjaBaseStats Checkpoint(byte level) =>
        Checkpoints.TryGetValue(level, out NinjaBaseStats? value)
            ? value
            : throw new KeyNotFoundException("Only evidenced Ninja stat checkpoints are embedded in code.");
}

public enum NinjaSkillResultCode : byte
{
    Applied = 1,
    Replay,
    CommandConflict,
    WrongProfession,
    SkillNotLearned,
    InvalidSkillLevel,
    InvalidEquipment,
    InvalidTarget,
    TargetDead,
    WrongMap,
    OutOfRange,
    InsufficientStamina,
    Cooldown,
    InvalidState,
    PersistenceConflict,
    PersistenceFailure
}

public enum NinjaPromotionResultCode : byte
{
    Applied = 1,
    Replay,
    OwnershipMismatch,
    WrongProfession,
    InvalidTransition,
    InsufficientLevel,
    Conflict
}

public readonly record struct NinjaPromotionResult(
    NinjaPromotionResultCode Code,
    byte PreviousProfession,
    byte ResultingProfession,
    byte Level)
{
    public bool Succeeded => Code is NinjaPromotionResultCode.Applied or NinjaPromotionResultCode.Replay;
}

public sealed record NinjaCombatant(
    CharacterUid CharacterUid,
    byte Profession,
    byte Level,
    WorldPoint Position,
    bool Alive,
    ushort Stamina,
    uint? RightHandItemType,
    uint? LeftHandItemType,
    IReadOnlyDictionary<NinjaSkillId, byte> LearnedSkills,
    bool IsFlying = false,
    bool IsMonster = false);

public sealed record NinjaSkillResult(
    NinjaSkillResultCode Code,
    ushort RemainingStamina,
    NinjaStatusKind? AppliedStatus = null,
    long? StatusExpiresAtMonotonicMilliseconds = null,
    bool Replayed = false);
