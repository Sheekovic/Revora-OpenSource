using Revora.Domain.Accounts;

namespace Revora.Gameplay.Models;

public readonly record struct MonsterTypeId
{
    public MonsterTypeId(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }
}

public readonly record struct SpawnRegionId
{
    public SpawnRegionId(long value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public long Value { get; }
}

public readonly record struct MonsterInstanceId
{
    public MonsterInstanceId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Monster instance identifier cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct SkillTypeId
{
    public SkillTypeId(ushort value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public ushort Value { get; }
}

public readonly record struct WorldPoint
{
    public WorldPoint(int mapId, ushort x, ushort y)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        MapId = mapId;
        X = x;
        Y = y;
    }

    public int MapId { get; }
    public ushort X { get; }
    public ushort Y { get; }

    public int ChebyshevDistance(WorldPoint other) =>
        MapId == other.MapId ? Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y)) : int.MaxValue;
}

public sealed record MonsterDefinition(
    MonsterTypeId Id,
    string SafeName,
    byte Behavior,
    ushort Mesh,
    byte Level,
    long MaximumHealth,
    int Defense,
    int MagicDefense,
    int MagicAttack,
    int MinimumAttack,
    int MaximumAttack,
    byte Dodge,
    byte AttackType,
    byte AttackDistance,
    int MoveDelayMilliseconds,
    long RespawnDelayMilliseconds)
{
    public MonsterDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(SafeName) || SafeName.Length > 128 || SafeName.Any(char.IsControl))
            throw new ArgumentException("Monster name is invalid.", nameof(SafeName));
        if (MaximumHealth is <= 0 or > 1_000_000_000_000L) throw new ArgumentOutOfRangeException(nameof(MaximumHealth));
        if (Defense < 0 || MagicDefense < 0 || MagicAttack < 0 || MinimumAttack < 0 || MaximumAttack < MinimumAttack)
            throw new ArgumentOutOfRangeException(nameof(Defense));
        if (MoveDelayMilliseconds is < 0 or > 3_600_000) throw new ArgumentOutOfRangeException(nameof(MoveDelayMilliseconds));
        if (RespawnDelayMilliseconds is < 0 or > 86_400_000) throw new ArgumentOutOfRangeException(nameof(RespawnDelayMilliseconds));
        return this;
    }
}

public sealed record SpawnRegionDefinition(
    SpawnRegionId Id,
    MonsterTypeId MonsterType,
    int MapId,
    int RequestedPopulation,
    ushort MinimumX,
    ushort MinimumY,
    ushort MaximumX,
    ushort MaximumY,
    bool Enabled)
{
    public SpawnRegionDefinition Validate()
    {
        if (MapId <= 0) throw new ArgumentOutOfRangeException(nameof(MapId));
        if (RequestedPopulation is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(RequestedPopulation));
        if (MaximumX < MinimumX || MaximumY < MinimumY) throw new ArgumentException("Spawn bounds are inverted.");
        long area = checked((long)MaximumX - MinimumX + 1) * ((long)MaximumY - MinimumY + 1);
        if (area > 4_000_000) throw new ArgumentOutOfRangeException(nameof(MaximumX), "Spawn region is too large.");
        return this;
    }
}

public enum MonsterLifeState : byte
{
    Alive = 1,
    Dead = 2
}

public sealed record MonsterInstance(
    MonsterInstanceId Id,
    MonsterTypeId MonsterType,
    SpawnRegionId SpawnRegion,
    WorldPoint Position,
    long MaximumHealth,
    long Health,
    int Defense,
    MonsterLifeState LifeState)
{
    public MonsterInstance Validate()
    {
        if (MaximumHealth <= 0 || Health < 0 || Health > MaximumHealth || Defense < 0)
            throw new ArgumentOutOfRangeException(nameof(Health));
        if ((Health == 0) != (LifeState == MonsterLifeState.Dead)) throw new ArgumentException("Monster life state is inconsistent.");
        return this;
    }
}

public readonly record struct CombatStats(int MinimumAttack, int MaximumAttack, int Defense, byte AttackRange)
{
    public CombatStats Validate()
    {
        if (MinimumAttack < 0 || MaximumAttack < MinimumAttack || Defense < 0 || AttackRange > 64)
            throw new ArgumentOutOfRangeException(nameof(MinimumAttack));
        return this;
    }
}

public enum CombatResultCode : byte
{
    Applied = 1,
    TargetNotFound,
    TargetDead,
    WrongMap,
    OutOfRange,
    OwnershipMismatch,
    CommandConflict
}

public sealed record CombatResult(
    CombatResultCode Code,
    long Damage = 0,
    long RemainingHealth = 0,
    bool Killed = false,
    bool Replayed = false)
{
    public CombatResult AsReplay() => this with { Replayed = true };
}

public sealed record DropRule(
    MonsterTypeId MonsterType,
    short Ordinal,
    ItemDefinitionId ItemDefinition,
    int ChancePartsPerMillion,
    byte Plus = 0,
    byte Sockets = 0,
    byte Bless = 0)
{
    public DropRule Validate()
    {
        if (Ordinal < 0 || ChancePartsPerMillion is < 0 or > 1_000_000 || Plus > 12 || Sockets > 2 || Bless > 7)
            throw new ArgumentOutOfRangeException(nameof(ChancePartsPerMillion));
        return this;
    }
}

public sealed record CharacterProgressionState(
    AccountId AccountId,
    CharacterUid CharacterUid,
    byte Level,
    long TotalExperience,
    byte RebirthCount = 0,
    byte PreviousProfession = 0)
{
    public CharacterProgressionState Validate()
    {
        if (Level is < 1 or > 255 || TotalExperience < 0 || RebirthCount > 2)
            throw new ArgumentOutOfRangeException(nameof(Level));
        return this;
    }
}

public sealed record SkillProgress(SkillTypeId SkillType, byte Level, long Experience)
{
    public SkillProgress Validate()
    {
        if (Experience < 0) throw new ArgumentOutOfRangeException(nameof(Experience));
        return this;
    }
}

public enum DomainCommandCode : byte
{
    Applied = 1,
    Rejected,
    OwnershipMismatch,
    NotFound,
    Expired,
    InvalidState,
    CapacityReached,
    Duplicate,
    CommandConflict,
    Overflow
}

public sealed record DomainCommandResult(DomainCommandCode Code, bool Replayed = false, string SafeCategory = "applied")
{
    public bool Succeeded => Code == DomainCommandCode.Applied;
    public DomainCommandResult AsReplay() => this with { Replayed = true };
}

public enum InvitationKind : byte { Team = 1, Friend = 2 }
public enum InvitationState : byte { Open = 1, Consumed = 2, Expired = 3, Rejected = 4 }

public sealed record SocialInvitation(
    Guid InvitationId,
    CharacterUid Sender,
    CharacterUid Recipient,
    InvitationKind Kind,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    InvitationState State)
{
    public SocialInvitation Validate()
    {
        if (InvitationId == Guid.Empty || Sender == Recipient || ExpiresAt <= CreatedAt)
            throw new ArgumentException("Invitation is invalid.");
        return this;
    }
}

public enum SocialRelationKind : byte { Friend = 1, Enemy = 2 }
public sealed record SocialRelation(CharacterUid Owner, CharacterUid Other, SocialRelationKind Kind);

public enum TradeState : byte { Open = 1, FirstConfirmed = 2, SecondConfirmed = 3, Committed = 4, Cancelled = 5 }
public sealed record TradeItemOffer(CharacterUid OfferingCharacter, ItemInstanceId ItemInstanceId, long ExpectedItemVersion)
{
    public TradeItemOffer Validate()
    {
        if (ExpectedItemVersion <= 0) throw new ArgumentOutOfRangeException(nameof(ExpectedItemVersion));
        return this;
    }
}

public sealed record TradeCurrencyOffer(CharacterUid OfferingCharacter, CurrencyKind Currency, long Amount)
{
    public TradeCurrencyOffer Validate()
    {
        if (Amount is < 0 or > 1_000_000_000_000L) throw new ArgumentOutOfRangeException(nameof(Amount));
        return this;
    }
}

public sealed record TradeSettlement(
    Guid TradeId,
    CharacterUid FirstCharacter,
    CharacterUid SecondCharacter,
    IReadOnlyList<TradeItemOffer> Items,
    IReadOnlyList<TradeCurrencyOffer> Currency);

public enum GuildRank : byte { Leader = 1, Deputy = 2, Member = 3 }
public sealed record GuildMember(CharacterUid CharacterUid, GuildRank Rank, long Donation)
{
    public GuildMember Validate()
    {
        if (Donation < 0) throw new ArgumentOutOfRangeException(nameof(Donation));
        return this;
    }
}

public enum QuestObjectiveKind : byte { TalkToNpc = 1, VisitMapCell, CollectItem, DefeatMonster, DeliverItem }
public enum QuestRepeatPolicy : byte { Once = 1, Daily, Weekly, Repeatable }
public enum QuestProgressState : byte { Active = 1, Completed, Abandoned }

public sealed record QuestStep(short StepNumber, QuestObjectiveKind Objective, long TargetLegacyId, int RequiredCount)
{
    public QuestStep Validate()
    {
        if (StepNumber is < 1 or > 32767 || TargetLegacyId <= 0 || RequiredCount is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(StepNumber));
        return this;
    }
}

public sealed record QuestDefinition(
    long QuestId,
    string SafeName,
    byte MinimumLevel,
    byte? MaximumLevel,
    QuestRepeatPolicy RepeatPolicy,
    IReadOnlyList<QuestStep> Steps)
{
    public QuestDefinition Validate()
    {
        if (QuestId <= 0 || string.IsNullOrWhiteSpace(SafeName) || SafeName.Length > 160 || SafeName.Any(char.IsControl))
            throw new ArgumentException("Quest definition is invalid.");
        if (MinimumLevel == 0 || MaximumLevel is not null && MaximumLevel < MinimumLevel)
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel));
        if (Steps.Count == 0 || Steps.Count > 32767) throw new ArgumentOutOfRangeException(nameof(Steps));
        var validated = Steps.Select(step => step.Validate()).OrderBy(step => step.StepNumber).ToArray();
        if (validated.Select(step => step.StepNumber).Distinct().Count() != validated.Length ||
            validated.Where((step, index) => step.StepNumber != index + 1).Any())
            throw new ArgumentException("Quest steps must be unique and contiguous.", nameof(Steps));
        return this;
    }
}

public sealed record CharacterQuestProgress(
    AccountId AccountId,
    CharacterUid CharacterUid,
    long QuestId,
    short StepNumber,
    int ProgressCount,
    QuestProgressState State)
{
    public CharacterQuestProgress Validate()
    {
        if (QuestId <= 0 || StepNumber is < 1 or > 32767 || ProgressCount < 0)
            throw new ArgumentOutOfRangeException(nameof(QuestId));
        return this;
    }
}

public enum EventLifecycleState : byte { Registration = 1, Running, Completed, Cancelled }
public sealed record EventParticipant(CharacterUid CharacterUid, long Score, int? Placement = null);
