using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

public static class CityGuardRules
{
    public static readonly TimeSpan BlueWarningGrace = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan PursuitWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan HiddenHeatStep = TimeSpan.FromMinutes(1);
    public const int DailyHonorCap = 10;

    public static PkNameState PkName(int pkPoints) => pkPoints switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(pkPoints)),
        < 30 => PkNameState.White,
        < 100 => PkNameState.Red,
        _ => PkNameState.Black
    };

    public static bool IsCriminalKill(CombatAttribution incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        return incident.Context == PvpContext.OpenWorld && incident.AttackerInitiated &&
            !incident.VictimWasFlashing && !incident.VictimWasRedOrBlack &&
            !incident.EnvironmentOrMonsterOnly && incident.DeliberateHostility && !incident.IsGuard;
    }

    public static WantedTransition ApplyIncident(WantedState current, CombatAttribution incident,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(incident);
        if (now < current.LastQualifyingCrimeUtc) throw new ArgumentOutOfRangeException(nameof(now));
        bool guardAttack = incident.IsGuard && !incident.IsServiceGuard && incident.DeliberateHostility;
        bool crime = IsCriminalKill(incident);
        if (!crime && !guardAttack) return new(current, false, false, false, false);
        int increase = guardAttack ? 2 : 1;
        if (incident.RepeatedVictim) increase++;
        if (incident.ProtectedBeginner) increase++;
        WantedHeatLevel heat = (WantedHeatLevel)Math.Min(5, (int)current.Heat + increase);
        WantedState state = current with
        {
            Heat = heat, LastQualifyingCrimeUtc = now, HiddenSinceUtc = now,
            TimerFrozen = false
        };
        return new(state, crime, guardAttack, guardAttack && incident.IsGuard, false);
    }

    public static WantedState Observe(WantedState current, DateTimeOffset now, int mapId)
    {
        if (now < current.LastQualifyingCrimeUtc) throw new ArgumentOutOfRangeException(nameof(now));
        return current with { LastObservedUtc = now, LastObservedMapId = mapId, HiddenSinceUtc = now };
    }

    public static WantedState AdvanceHidden(WantedState current, DateTimeOffset now)
    {
        if (current.TimerFrozen || current.Heat == WantedHeatLevel.None) return current;
        if (now < current.HiddenSinceUtc) throw new ArgumentOutOfRangeException(nameof(now));
        long steps = (long)((now - current.HiddenSinceUtc).Ticks / HiddenHeatStep.Ticks);
        if (steps <= 0) return current;
        int remaining = Math.Max(0, (int)current.Heat - checked((int)Math.Min(steps, 5)));
        DateTimeOffset hiddenSince = current.HiddenSinceUtc.AddTicks(steps * HiddenHeatStep.Ticks);
        return current with
        {
            Heat = (WantedHeatLevel)remaining, HiddenSinceUtc = hiddenSince,
            LastObservedUtc = remaining == 0 ? null : current.LastObservedUtc,
            LastObservedMapId = remaining == 0 ? null : current.LastObservedMapId
        };
    }

    public static WantedState SetLogout(WantedState current, DateTimeOffset now, bool loggedOut)
    {
        if (now < current.HiddenSinceUtc) throw new ArgumentOutOfRangeException(nameof(now));
        WantedState advanced = loggedOut ? AdvanceHidden(current, now) : current;
        return advanced with { TimerFrozen = loggedOut, HiddenSinceUtc = now };
    }

    public static int BountyPoints(int pkPoints) => pkPoints switch
    {
        < 100 => 0, < 150 => 1, < 250 => 2, < 400 => 3, < 600 => 4, _ => 5
    };

    public static bool ShouldCreateBountyCycle(int previousPkPoints, int currentPkPoints,
        Guid? activeCycleId) => previousPkPoints < 100 && currentPkPoints >= 100 && activeCycleId is null;

    public static BountyEligibility EvaluateBounty(BountyEligibilityInput input, DateTimeOffset now)
    {
        int points = BountyPoints(input.PkPoints);
        if (points == 0) return new(HonorEligibilityDecision.Reject, 0, "not-black-name");
        if (input.SameAccount || input.ConfidentSelfFarm)
            return new(HonorEligibilityDecision.Reject, 0, "linked-self-farming");
        if (!input.ValidContext) return new(HonorEligibilityDecision.Reject, 0, "invalid-context");
        if (input.LastSamePairAwardUtc is { } last && now - last < TimeSpan.FromHours(24))
            return new(HonorEligibilityDecision.Reject, 0, "same-pair-24h");
        if (input.PlayerDamageShare < 0.20m)
            return new(HonorEligibilityDecision.Reject, 0, "insufficient-player-contribution");
        if (input.ReciprocalPattern)
            return new(HonorEligibilityDecision.HoldForReview, 0, "reciprocal-farming-pattern");
        int available = Math.Max(0, DailyHonorCap - input.AwardedToday);
        return available == 0
            ? new(HonorEligibilityDecision.Reject, 0, "utc-daily-cap")
            : new(HonorEligibilityDecision.Award, Math.Min(points, available), "eligible");
    }

    public static int Tribute(byte level, byte rebirths, long silverBalance,
        int minimum = 10_000, int maximum = 1_000_000)
    {
        if (minimum < 0 || maximum < minimum || rebirths > 2 || silverBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        long progression = checked(10_000L + level * 2_000L + rebirths * 100_000L);
        long affordability = silverBalance / 20;
        return checked((int)Math.Clamp(Math.Min(progression, affordability), minimum, maximum));
    }

    public static ArenaReleaseReason? FallbackRelease(GuardArenaSentence sentence, DateTimeOffset now) =>
        sentence.Defeats >= 3 ? ArenaReleaseReason.FallbackDefeats :
        now - sentence.SentencedUtc >= TimeSpan.FromMinutes(10) ? ArenaReleaseReason.FallbackTimeout : null;

    public static bool IsLegalGuardTarget(GuardTarget target, int guardMapId, int guardInstanceId) =>
        target.Alive && target.Connected && !target.SpawnProtected && target.MapId == guardMapId &&
        target.InstanceId == guardInstanceId && (target.ViolentNow || target.PkPoints >= 30 || target.Heat > WantedHeatLevel.None);

    public static IReadOnlyList<GuardTarget> SelectTargets(IEnumerable<GuardTarget> candidates,
        int guardMapId, int guardInstanceId, int limit) => candidates
        .Where(value => IsLegalGuardTarget(value, guardMapId, guardInstanceId))
        .OrderByDescending(value => value.PkPoints >= 100)
        .ThenByDescending(value => value.ViolentNow)
        .ThenByDescending(value => value.Heat)
        .ThenBy(value => value.CharacterUid)
        .Take(Math.Clamp(limit, 0, 6)).ToArray();
}
