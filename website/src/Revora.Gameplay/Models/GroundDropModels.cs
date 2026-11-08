namespace Revora.Gameplay.Models;

public enum GroundDropKind : byte { Item = 1, Silver = 2 }
public enum GroundPickupCode : byte
{
    Applied = 1, Replay, Missing, WrongMap, OutOfRange, Protected, InventoryFull, WalletFull,
    Expired, OwnershipMismatch, Conflict
}

public sealed record GroundDropSnapshot(uint GroundUid, GroundDropKind Kind, uint ItemType,
    long Silver, GameplayActor Owner, CombatMapKey Map, ushort X, ushort Y,
    DateTimeOffset ProtectedUntil, DateTimeOffset ExpiresAt, long Version,
    uint? LegacyItemUid = null)
{
    public GroundDropSnapshot Validate()
    {
        if (GroundUid is < 300_000_000 or > 399_999_999 || ItemType == 0 ||
            (Kind == GroundDropKind.Item) == (LegacyItemUid is null) ||
            (Kind == GroundDropKind.Silver) == (Silver <= 0) || Version <= 0 ||
            ExpiresAt <= ProtectedUntil)
            throw new ArgumentOutOfRangeException(nameof(GroundUid));
        Map.Validate();
        return this;
    }
}

public sealed record MonsterRewardPlan(Guid DeathId, long SpawnGeneration,
    GameplayActor Killer, uint MonsterUid, int MonsterType, CombatMapKey Map,
    ushort X, ushort Y, long Experience, long SkillExperience,
    IReadOnlyList<PlannedGroundDrop> Drops);

public sealed record PlannedGroundDrop(GroundDropKind Kind, uint ItemType, long Silver,
    Guid? ItemSourceId, byte Plus, byte Sockets, byte Bless, ushort X, ushort Y);

public sealed record MonsterRewardCommitResult(bool Applied, bool Replayed,
    long ExperienceAfter, byte LevelAfter, IReadOnlyList<GroundDropSnapshot> Drops);

/// <summary>
/// Durable per-hit progression. Experience applies to every damaging monster hit;
/// weapon proficiency applies only to evidenced normal physical/ranged attacks.
/// Raw proficiency awards are resolved against authoritative equipped items inside
/// the same serializable transaction as the combat receipt.
/// </summary>
public sealed record CombatProgressionPlan(long CharacterExperienceAward,
    long WeaponProficiencyBaseAward)
{
    public CombatProgressionPlan Validate()
    {
        if (CharacterExperienceAward < 0 || WeaponProficiencyBaseAward < 0)
            throw new ArgumentOutOfRangeException(nameof(CharacterExperienceAward));
        return this;
    }
}

public sealed record WeaponProficiencyCommitResult(ushort ProficiencyType,
    long ExperienceAwarded, byte LevelAfter, uint ExperienceAfter);

public sealed record CombatProgressionCommitResult(long ExperienceAwarded,
    long ExperienceAfter, byte LevelAfter,
    IReadOnlyList<WeaponProficiencyCommitResult> Proficiencies);

public sealed record GroundPickupRequest(Guid OperationId, GameplayActor Actor, uint GroundUid,
    CombatMapKey Map, ushort X, ushort Y, DateTimeOffset UtcNow);

public sealed record GroundPickupResult(GroundPickupCode Code, GroundDropSnapshot? Drop,
    uint? LegacyItemUid, long? SilverBalance, byte? InventorySlot)
{
    public bool Succeeded => Code is GroundPickupCode.Applied or GroundPickupCode.Replay;
}
