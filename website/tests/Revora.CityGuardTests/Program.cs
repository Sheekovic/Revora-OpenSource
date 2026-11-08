using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.CityGuardTests;

internal static class Program
{
    private static int passed;
    private static readonly DateTimeOffset Epoch = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    private static void Main()
    {
        Test("PK thresholds", () =>
        {
            Equal(PkNameState.White, CityGuardRules.PkName(0)); Equal(PkNameState.White, CityGuardRules.PkName(29));
            Equal(PkNameState.Red, CityGuardRules.PkName(30)); Equal(PkNameState.Red, CityGuardRules.PkName(99));
            Equal(PkNameState.Black, CityGuardRules.PkName(100));
        });
        Test("sanctioned and self-defense exclusions", () =>
        {
            False(CityGuardRules.IsCriminalKill(Incident(context: PvpContext.Arena)));
            False(CityGuardRules.IsCriminalKill(Incident(victimFlashing: true)));
            False(CityGuardRules.IsCriminalKill(Incident(victimCriminal: true)));
            False(CityGuardRules.IsCriminalKill(Incident(attackerInitiated: false)));
            True(CityGuardRules.IsCriminalKill(Incident()));
        });
        Test("wanted escalation and beginner repeat multiplier", () =>
        {
            WantedTransition result = CityGuardRules.ApplyIncident(WantedState.Empty(Epoch),
                Incident(repeat: true, beginner: true), Epoch.AddSeconds(1));
            Equal(WantedHeatLevel.Three, result.State.Heat); False(result.CreateBountyCycle); True(result.QualifyingCrime);
        });
        Test("guard direct attack and service exclusion", () =>
        {
            WantedTransition attack = CityGuardRules.ApplyIncident(WantedState.Empty(Epoch), Incident(guard: true), Epoch.AddSeconds(1));
            Equal(WantedHeatLevel.Two, attack.State.Heat); True(attack.SentenceToArena);
            WantedTransition commander = CityGuardRules.ApplyIncident(WantedState.Empty(Epoch), Incident(guard: true, service: true), Epoch.AddSeconds(1));
            Equal(WantedHeatLevel.None, commander.State.Heat);
        });
        Test("LOS pauses and hidden whole-minute decay", () =>
        {
            WantedState active = new(WantedHeatLevel.Five, Epoch, Epoch, Epoch, 1002, false, Guid.NewGuid());
            WantedState observed = CityGuardRules.Observe(active, Epoch.AddSeconds(50), 1002);
            Equal(WantedHeatLevel.Five, CityGuardRules.AdvanceHidden(observed, Epoch.AddSeconds(109)).Heat);
            WantedState decayed = CityGuardRules.AdvanceHidden(observed, Epoch.AddSeconds(170));
            Equal(WantedHeatLevel.Three, decayed.Heat);
            WantedState frozen = CityGuardRules.SetLogout(decayed, Epoch.AddSeconds(180), true);
            Equal(WantedHeatLevel.Three, CityGuardRules.AdvanceHidden(frozen, Epoch.AddHours(1)).Heat);
        });
        Test("wanted expiry forgets location", () =>
        {
            WantedState state = new(WantedHeatLevel.One, Epoch, Epoch, Epoch, 1002, false, Guid.NewGuid());
            WantedState expired = CityGuardRules.AdvanceHidden(state, Epoch.AddMinutes(1));
            Equal(WantedHeatLevel.None, expired.Heat); NullableEqual<int>(null, expired.LastObservedMapId);
        });
        Test("bounty schedule", () =>
        {
            Equal(0, CityGuardRules.BountyPoints(99)); Equal(1, CityGuardRules.BountyPoints(100));
            Equal(2, CityGuardRules.BountyPoints(150)); Equal(3, CityGuardRules.BountyPoints(250));
            Equal(4, CityGuardRules.BountyPoints(400)); Equal(5, CityGuardRules.BountyPoints(600));
            True(CityGuardRules.ShouldCreateBountyCycle(99, 100, null));
            False(CityGuardRules.ShouldCreateBountyCycle(100, 110, null));
        });
        Test("bounty cap pair and contribution", () =>
        {
            BountyEligibility ok = CityGuardRules.EvaluateBounty(Eligibility(pk: 600, today: 8), Epoch);
            Equal(HonorEligibilityDecision.Award, ok.Decision); Equal(2, ok.Points);
            Equal("same-pair-24h", CityGuardRules.EvaluateBounty(Eligibility(last: Epoch.AddHours(-23)), Epoch).Reason);
            Equal("insufficient-player-contribution", CityGuardRules.EvaluateBounty(Eligibility(share: .19m), Epoch).Reason);
            Equal(HonorEligibilityDecision.HoldForReview, CityGuardRules.EvaluateBounty(Eligibility(reciprocal: true), Epoch).Decision);
        });
        Test("spawn protection ends on hostility", () =>
        {
            var protection = new SpawnProtection(Epoch.AddSeconds(10), true);
            True(protection.IsProtected(Epoch)); False(protection.EndForHostileAction().IsProtected(Epoch));
        });
        Test("tribute bounds", () =>
        {
            Equal(10_000, CityGuardRules.Tribute(1, 0, 1));
            Equal(720_000, CityGuardRules.Tribute(255, 2, 100_000_000));
        });
        Test("arena recovery and fallback", () =>
        {
            var sentence = new GuardArenaSentence(Guid.NewGuid(), 1, Epoch, 0, 1000, 100, true).RecordDefeat();
            Equal(300L, sentence.EliteCurrentHealth);
            sentence = sentence.RecordDefeat().RecordDefeat();
            NullableEqual<ArenaReleaseReason>(ArenaReleaseReason.FallbackDefeats,
                CityGuardRules.FallbackRelease(sentence, Epoch.AddMinutes(1)));
        });
        Test("guard target legality caps and priority", () =>
        {
            GuardTarget[] targets =
            [
                new(1, 30, WantedHeatLevel.None, false, true, true, false, 1002, 0),
                new(2, 100, WantedHeatLevel.One, false, true, true, false, 1002, 0),
                new(3, 0, WantedHeatLevel.Five, true, true, true, false, 1002, 0),
                new(4, 500, WantedHeatLevel.Five, true, true, true, true, 1002, 0),
                new(5, 500, WantedHeatLevel.Five, true, true, true, false, 1002, 1)
            ];
            IReadOnlyList<GuardTarget> selected = CityGuardRules.SelectTargets(targets, 1002, 0, 6);
            Equal(3, selected.Count); Equal(2u, selected[0].CharacterUid);
        });
        Console.WriteLine($"Revora.CityGuardTests: {passed} passed");
    }

    private static CombatAttribution Incident(PvpContext context = PvpContext.OpenWorld,
        bool victimFlashing = false, bool victimCriminal = false, bool attackerInitiated = true,
        bool repeat = false, bool beginner = false, bool guard = false, bool service = false) =>
        new(Guid.NewGuid(), 1, 2, context, attackerInitiated, victimFlashing, victimCriminal,
            false, true, repeat, beginner, guard, service);

    private static BountyEligibilityInput Eligibility(int pk = 100, int today = 0,
        DateTimeOffset? last = null, decimal share = 1m, bool reciprocal = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pk, today, last, false, true, share, false, reciprocal);

    private static void Test(string name, Action test) { try { test(); passed++; } catch (Exception e) { throw new InvalidOperationException(name, e); } }
    private static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    private static void False(bool value) => True(!value);
    private static void Equal<T>(T expected, T actual) where T : notnull
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; got {actual}."); }
    private static void NullableEqual<T>(T? expected, T? actual) where T : struct
    { if (!EqualityComparer<T?>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; got {actual}."); }
}
