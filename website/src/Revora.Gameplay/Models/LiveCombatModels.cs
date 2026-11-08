namespace Revora.Gameplay.Models;

public enum LiveEntityKind : byte
{
    Player = 1,
    Monster = 2
}

public readonly record struct CombatMapKey(int MapId, int InstanceId)
{
    public CombatMapKey Validate()
    {
        if (MapId <= 0 || InstanceId < 0) throw new ArgumentOutOfRangeException(nameof(MapId));
        return this;
    }
}

public sealed record LiveCombatEntitySnapshot(
    uint LegacyUid,
    LiveEntityKind Kind,
    GameplayActor? Owner,
    CombatMapKey Map,
    ushort X,
    ushort Y,
    long CurrentHealth,
    long MaximumHealth,
    bool Attackable,
    long Version,
    long LastTouchedMonotonicMilliseconds,
    bool IsFlying = false,
    long PersistenceVersion = 0)
{
    public bool Alive => CurrentHealth > 0;

    public LiveCombatEntitySnapshot Validate()
    {
        Map.Validate();
        if (LegacyUid == 0 || MaximumHealth <= 0 || CurrentHealth < 0 || CurrentHealth > MaximumHealth ||
            Version <= 0 || PersistenceVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(LegacyUid));
        if ((Kind == LiveEntityKind.Player) != Owner.HasValue)
            throw new ArgumentException("Only player entities carry authenticated ownership.");
        // Existing in-memory fixtures and monster entities have one version domain.
        // Players additionally retain the PostgreSQL vitals version so movement can
        // advance the live concurrency version without invalidating the next hit.
        return PersistenceVersion == 0 ? this with { PersistenceVersion = Version } : this;
    }
}

public sealed record NormalCombatRequest(
    uint Sequence,
    GameplayActor AuthenticatedActor,
    uint TargetUid,
    ushort ClaimedTargetX,
    ushort ClaimedTargetY,
    uint Action,
    long MonotonicMilliseconds,
    ushort? TargetStamina = null,
    bool TargetWasSitting = false);

public enum LiveCombatResultCode : byte
{
    Applied = 1,
    Replay,
    CommandConflict,
    InvalidSequence,
    AttackerMissing,
    OwnershipMismatch,
    AttackerDead,
    TargetMissing,
    TargetDead,
    TargetNotAttackable,
    WrongMapOrInstance,
    InvalidCoordinates,
    OutOfRange,
    PvpNotAllowed,
    Cooldown,
    PersistenceConflict,
    PersistenceFailure
}

public sealed record LiveCombatResult(
    LiveCombatResultCode Code,
    uint AttackerUid,
    uint TargetUid,
    long Damage = 0,
    long RemainingHealth = 0,
    bool Killed = false,
    bool Replayed = false,
    string SafeCategory = "applied",
    bool CounterKillTriggered = false,
    long CounterDamage = 0,
    uint CounterAttackerUid = 0,
    uint CounterTargetUid = 0,
    ushort CounterSkillLevel = 0,
    MonsterRewardCommitResult? Reward = null,
    ushort? StaminaAfter = null,
    bool SittingInterrupted = false,
    CombatProgressionCommitResult? Progression = null)
{
    public bool Succeeded => Code is LiveCombatResultCode.Applied or LiveCombatResultCode.Replay;
}

public sealed record CombatMutation(
    Guid OperationId,
    GameplayActor Attacker,
    LiveEntityKind TargetKind,
    uint TargetUid,
    CombatMapKey Map,
    ushort TargetX,
    ushort TargetY,
    long ExpectedTargetVersion,
    long PreviousHealth,
    long Damage,
    long RemainingHealth,
    bool Killed,
    string SafeCategory,
    MonsterRewardPlan? Reward = null,
    ushort? PreviousTargetStamina = null,
    ushort? RemainingTargetStamina = null,
    bool SittingInterrupted = false,
    CombatProgressionPlan? Progression = null);

public sealed record MonsterCombatRequest(
    Guid OperationId,
    uint MonsterUid,
    GameplayActor Target,
    OwnerMonsterTemplate Monster,
    PlayerCombatStatistics TargetStatistics,
    long MonotonicMilliseconds,
    ushort? PreviousStamina = null,
    bool WasSitting = false);

public sealed record MonsterCombatMutation(
    Guid OperationId,
    uint MonsterUid,
    int MonsterType,
    GameplayActor Target,
    CombatMapKey Map,
    long ExpectedTargetVersion,
    long PreviousHealth,
    long Damage,
    long RemainingHealth,
    bool Killed,
    ushort? PreviousStamina,
    ushort? RemainingStamina,
    bool SittingInterrupted);

public enum CombatPersistenceCode : byte { Applied = 1, Replay, Conflict }

public readonly record struct CombatPersistenceResult(
    CombatPersistenceCode Code,
    MonsterRewardCommitResult? Reward = null,
    ushort? TargetStaminaAfter = null,
    bool SittingInterrupted = false,
    CombatProgressionCommitResult? Progression = null);

public enum RecoveryItemPersistenceCode : byte
{
    Applied = 1,
    Replay,
    Conflict,
    ItemNotFound,
    InvalidItem,
    CharacterDead
}

public sealed record RecoveryItemMutation(
    Guid OperationId,
    GameplayActor Actor,
    uint LegacyItemUid,
    uint ItemType,
    int HealthRecovery,
    int ManaRecovery,
    long ExpectedEntityVersion,
    long ExpectedPersistenceVersion,
    long PreviousHealth);

public readonly record struct RecoveryItemPersistenceResult(
    RecoveryItemPersistenceCode Code,
    long CurrentHealth,
    int CurrentMana,
    long PersistenceVersion)
{
    public bool Succeeded => Code is RecoveryItemPersistenceCode.Applied or RecoveryItemPersistenceCode.Replay;
    public bool Changed => Code == RecoveryItemPersistenceCode.Applied;
}

public readonly record struct DurableHealthMutation<T>(
    T Result,
    bool Changed,
    long ResultingHealth,
    long ResultingPersistenceVersion);

public sealed record NinjaLiveCombatRequest(
    uint Sequence,
    GameplayActor AuthenticatedActor,
    uint TargetUid,
    ushort ClaimedTargetX,
    ushort ClaimedTargetY,
    ushort SkillId,
    ushort SkillLevel,
    long MonotonicMilliseconds,
    NinjaCombatant Actor,
    NinjaCombatant? Target);

public sealed record NinjaCombatMutation(
    Guid OperationId,
    GameplayActor Attacker,
    ushort SkillId,
    ushort SkillLevel,
    ushort PreviousStamina,
    ushort RemainingStamina,
    LiveEntityKind? TargetKind,
    uint TargetUid,
    CombatMapKey Map,
    ushort AttackerX,
    ushort AttackerY,
    ushort TargetX,
    ushort TargetY,
    long ExpectedTargetVersion,
    long PreviousHealth,
    long Damage,
    long RemainingHealth,
    bool Killed,
    long SkillExperienceAward,
    MonsterRewardPlan? Reward = null,
    CombatProgressionPlan? Progression = null);

public sealed record NinjaLiveCombatResult(
    NinjaSkillResultCode Code,
    uint ActorUid,
    uint TargetUid,
    ushort SkillId,
    ushort SkillLevel,
    ushort RemainingStamina,
    long Damage,
    long RemainingHealth,
    bool Killed,
    NinjaStatusKind? AppliedStatus,
    bool Replayed,
    string SafeCategory,
    MonsterRewardCommitResult? Reward = null,
    CombatProgressionCommitResult? Progression = null)
{
    public bool Succeeded => Code is NinjaSkillResultCode.Applied or NinjaSkillResultCode.Replay;
}
