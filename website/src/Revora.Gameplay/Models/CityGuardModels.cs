namespace Revora.Gameplay.Models;

public enum PkNameState : byte { White, Red, Black }
public enum WantedHeatLevel : byte { None, One, Two, Three, Four, Five }
public enum PvpContext : byte { OpenWorld, Duel, Arena, GuildWar, FreePkMap, ServerEvent }

public sealed record CombatAttribution(
    Guid IncidentId, uint AttackerUid, uint VictimUid, PvpContext Context,
    bool AttackerInitiated, bool VictimWasFlashing, bool VictimWasRedOrBlack,
    bool EnvironmentOrMonsterOnly, bool DeliberateHostility, bool RepeatedVictim,
    bool ProtectedBeginner, bool IsGuard, bool IsServiceGuard = false);

public sealed record WantedState(
    WantedHeatLevel Heat, DateTimeOffset LastQualifyingCrimeUtc,
    DateTimeOffset HiddenSinceUtc, DateTimeOffset? LastObservedUtc,
    int? LastObservedMapId, bool TimerFrozen, Guid? BountyCycleId)
{
    public static WantedState Empty(DateTimeOffset now) =>
        new(WantedHeatLevel.None, now, now, null, null, false, null);
}

public sealed record WantedTransition(WantedState State, bool QualifyingCrime,
    bool BeginPursuit, bool SentenceToArena, bool CreateBountyCycle);

public sealed record SpawnProtection(DateTimeOffset ExpiresUtc, bool Active)
{
    public bool IsProtected(DateTimeOffset now) => Active && now < ExpiresUtc;
    public SpawnProtection EndForHostileAction() => this with { Active = false };
}

public enum HonorEligibilityDecision : byte { Award, Reject, HoldForReview }

public sealed record BountyEligibilityInput(
    Guid BountyCycleId, Guid KillerAccountId, Guid WantedAccountId, int PkPoints,
    int AwardedToday, DateTimeOffset? LastSamePairAwardUtc, bool SameAccount,
    bool ValidContext, decimal PlayerDamageShare, bool ConfidentSelfFarm,
    bool ReciprocalPattern);

public sealed record BountyEligibility(HonorEligibilityDecision Decision, int Points, string Reason);

public enum ArenaReleaseReason : byte { EliteDefeated, TributePaid, FallbackDefeats, FallbackTimeout }

public sealed record GuardArenaSentence(Guid SentenceId, uint CharacterUid, DateTimeOffset SentencedUtc,
    int Defeats, long EliteMaximumHealth, long EliteCurrentHealth, bool Active)
{
    public GuardArenaSentence RecordDefeat() => this with
    {
        Defeats = checked(Defeats + 1),
        EliteCurrentHealth = Math.Min(EliteMaximumHealth,
            checked(EliteCurrentHealth + (long)Math.Ceiling(EliteMaximumHealth * 0.20m)))
    };
}

public sealed record GuardTarget(uint CharacterUid, int PkPoints, WantedHeatLevel Heat,
    bool ViolentNow, bool Alive, bool Connected, bool SpawnProtected, int MapId, int InstanceId);
