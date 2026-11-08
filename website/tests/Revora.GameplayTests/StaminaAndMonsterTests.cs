using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.GameplayTests;

internal static class StaminaAndMonsterTests
{
    public static int Run()
    {
        StaminaIsGenericMonotonicAndBounded();
        OwnerSourceCombatFormulasAreBoundedAndDeterministic();
        OwnerSourceProficiencyAwardsRespectDualWeaponsAndEligibility();
        MonsterCatalogValidationRejectsUnsafeRecords();
        return 4;
    }

    private static void OwnerSourceProficiencyAwardsRespectDualWeaponsAndEligibility()
    {
        IReadOnlyDictionary<ushort, long> dual = OwnerSourceProficiencyFormula.ResolveWeaponAwards(
            601_000, 601_339, 100);
        Equal(1, dual.Count); Equal(150L, dual[601]);
        IReadOnlyDictionary<ushort, long> mixed = OwnerSourceProficiencyFormula.ResolveWeaponAwards(
            410_003, 601_000, 100);
        Equal(100L, mixed[410]); Equal(50L, mixed[601]);
        Equal(350L, OwnerSourceProficiencyFormula.ScaleAward(100, 1, 1, 1));
        Equal(0L, OwnerSourceProficiencyFormula.ScaleAward(100, 12, 109, 1));
        Equal(200L, OwnerSourceProficiencyFormula.ScaleAward(100, 12, 110, 2));
        Equal(0, OwnerSourceProficiencyFormula.ResolveWeaponAwards(null, null, 100).Count);
    }

    private static void StaminaIsGenericMonotonicAndBounded()
    {
        var stamina = new StaminaResource(40, 0);
        Equal((ushort)43, stamina.Regenerate(1_000).Snapshot.Current);
        stamina.SetActivity(StaminaActivity.Sitting, 1_100);
        Equal((ushort)58, stamina.Regenerate(2_000).Snapshot.Current);
        stamina.SetActivity(StaminaActivity.Moving, 2_100);
        StaminaTickResult resumed = stamina.Regenerate(3_000);
        Equal(true, resumed.Changed);
        Equal((ushort)61, resumed.Snapshot.Current);
        Equal(StaminaActivity.Standing, resumed.Snapshot.Activity);
        Equal(true, stamina.TryConsume(30, 3_100, out StaminaSnapshot consumed));
        Equal((ushort)31, consumed.Current);
        Equal(false, stamina.TryConsume(32, 3_200, out _));
        StaminaTickResult attackResumed = stamina.Regenerate(4_000);
        Equal(true, attackResumed.Changed);
        Equal((ushort)34, attackResumed.Snapshot.Current);
        Equal(StaminaActivity.Standing, attackResumed.Snapshot.Activity);
        Equal((ushort)100, stamina.Revive(4_000).Current);
        Equal((ushort)100, stamina.Regenerate(100_000).Snapshot.Current);
    }

    private static void OwnerSourceCombatFormulasAreBoundedAndDeterministic()
    {
        var monster = new OwnerMonsterTemplate(1, "Pheasant", OwnerMonsterBehavior.HuntPlayers,
            104, 1, 33, 0, 0, 0, 5, 6, 1, 1, OwnerMonsterAttackType.Melee,
            0, 0, true, 1, 10, 20, 800, 10_000, true).Validate();
        var player = new PlayerCombatStatistics(1, 5, 2, 0, 4, 7, 1, 0, 0, 2, 0).Validate();
        long first = OwnerSourceCombatFormula.PlayerPhysicalDamage(player, monster, 7);
        Equal(first, OwnerSourceCombatFormula.PlayerPhysicalDamage(player, monster, 7));
        Equal(true, first >= 1);
        long incoming = OwnerSourceCombatFormula.MonsterDamage(monster, player, 9);
        Equal(true, incoming is >= 4 and <= 5);
        Equal(14L, OwnerSourceCombatFormula.ExperienceForDamage(10, 1, 10));
    }

    private static void MonsterCatalogValidationRejectsUnsafeRecords()
    {
        _ = new OwnerMonsterGenerator(1, 1, 1002, 1, 1, 2, 2, 1, true, "normal").Validate();
        Throws(() => new OwnerMonsterGenerator(1, 1, 1002, 2, 1, 1, 2, 1, true, "normal").Validate());
        Throws(() => new OwnerMonsterTemplate(1, "NameTooLongFor5165", OwnerMonsterBehavior.HuntPlayers,
            1, 1, 1, 0, 0, 0, 1, 1, 0, 0, OwnerMonsterAttackType.Melee,
            0, 0, true, 1, 0, 0, 1, 1, true).Validate());
    }

    private static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("The invalid owner monster record was accepted.");
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }
}
