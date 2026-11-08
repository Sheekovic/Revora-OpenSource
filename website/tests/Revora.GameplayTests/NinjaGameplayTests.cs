using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.GameplayTests;

internal static class NinjaGameplayTests
{
    private static readonly CharacterUid NinjaUid = new(2_000_001);
    private static readonly CharacterUid TargetUid = new(2_000_002);
    private static readonly WorldPoint NinjaPoint = new(1002, 350, 350);

    public static int Run()
    {
        ProfessionStagesAndStatsAreExact();
        KatanaAndEquipmentRangesAreBounded();
        NativeSkillCatalogIsComplete();
        TwofoldRequiresDualKatanaAndUsesExactDamage();
        ToxicFogIsBoundedAboveOneHealth();
        PoisonStarUsesLevelDurationAndExpires();
        CounterKillIsSelfBoundAndUsesExactDamage();
        ArcherBaneRequiresFlyingPlayerTarget();
        ExperienceSkillsAreSelfBoundAndDamageIsBounded();
        ClassRangeStaminaCooldownAndLifeAreValidated();
        ReplayAndConflictingReplayAreSafe();
        TransientStatesAreClearedAtLifecycleBoundaries();
        OtherClassRegressionRemainsClosed();
        return 13;
    }

    private static void ProfessionStagesAndStatsAreExact()
    {
        Equal((byte)1, NinjaProfessionRules.RequiredLevel(50));
        Equal((byte)15, NinjaProfessionRules.RequiredLevel(51));
        Equal((byte)110, NinjaProfessionRules.RequiredLevel(55));
        True(NinjaProfessionRules.CanPromote(50, 51, 15));
        False(NinjaProfessionRules.CanPromote(50, 52, 120));
        False(NinjaProfessionRules.CanPromote(55, 56, 120));
        Equal(new NinjaBaseStats(1, 1, 8, 1, 0), NativeNinjaStats5165.Checkpoint(1));
        Equal(new NinjaBaseStats(120, 57, 306, 49, 0), NativeNinjaStats5165.Checkpoint(120));
    }

    private static void KatanaAndEquipmentRangesAreBounded()
    {
        True(NinjaItemRules.IsKatana(601000));
        True(NinjaItemRules.IsKatana(601339));
        False(NinjaItemRules.IsKatana(600999));
        False(NinjaItemRules.IsKatana(601340));
        True(NinjaItemRules.CanEquipKatana(50, EquipmentSlot.RightHand, 601000));
        True(NinjaItemRules.CanEquipKatana(55, EquipmentSlot.LeftHand, 601339));
        False(NinjaItemRules.CanEquipKatana(40, EquipmentSlot.RightHand, 601000));
        False(NinjaItemRules.CanEquipKatana(50, EquipmentSlot.Headwear, 601000));
        True(NinjaItemRules.IsNinjaHeadwear(112109));
        True(NinjaItemRules.IsNinjaHeadwear(123000));
        True(NinjaItemRules.IsNinjaArmor(135109));
    }

    private static void NativeSkillCatalogIsComplete()
    {
        Equal(7, NativeNinjaSkillCatalog5165.All.Count);
        foreach (ushort id in new ushort[] { 6000, 6001, 6002, 6003, 6004, 6010, 6011 })
            True(NativeNinjaSkillCatalog5165.TryGet(id, out _));
        False(NativeNinjaSkillCatalog5165.TryGet(6012, out _));
    }

    private static void TwofoldRequiresDualKatanaAndUsesExactDamage()
    {
        var engine = new NinjaSkillEngine();
        NinjaSkillResult result = engine.Use(1, Actor(skills: Skill(NinjaSkillId.TwofoldBlades, 4)),
            Target(), 6000, 4, 1_000);
        Equal(NinjaSkillResultCode.Applied, result.Code);
        Equal((ushort)70, result.RemainingStamina);
        Equal(130u, NinjaSkillEngine.CalculateTwofoldDamage(100, 4));
        Equal(NinjaSkillResultCode.InvalidEquipment,
            new NinjaSkillEngine().Use(1, Actor(left: null, skills: Skill(NinjaSkillId.TwofoldBlades, 0)),
                Target(), 6000, 0, 1_000).Code);
    }

    private static void ToxicFogIsBoundedAboveOneHealth()
    {
        var engine = new NinjaSkillEngine();
        NinjaSkillResult result = engine.Use(2, Actor(skills: Skill(NinjaSkillId.ToxicFog, 4)),
            Target(), 6001, 4, 1_000);
        Equal(NinjaSkillResultCode.Applied, result.Code);
        Equal(NinjaStatusKind.ToxicFog, result.AppliedStatus!.Value);
        Equal(50u, NinjaSkillEngine.CalculateToxicFogCurrentHealth(100, 4));
        Equal(1u, NinjaSkillEngine.CalculateToxicFogCurrentHealth(2, 4));
        True(engine.HasStatus(TargetUid, NinjaStatusKind.ToxicFog, 2_000));
    }

    private static void PoisonStarUsesLevelDurationAndExpires()
    {
        var engine = new NinjaSkillEngine();
        NinjaSkillResult result = engine.Use(3, Actor(skills: Skill(NinjaSkillId.PoisonStar, 2)),
            Target(), 6002, 2, 10_000);
        Equal(25_000L, result.StatusExpiresAtMonotonicMilliseconds!.Value);
        True(engine.HasStatus(TargetUid, NinjaStatusKind.PoisonStar, 24_999));
        True(engine.IsItemUseSuppressed(TargetUid, 24_999));
        False(engine.HasStatus(TargetUid, NinjaStatusKind.PoisonStar, 25_000));
        False(engine.IsItemUseSuppressed(TargetUid, 25_000));
    }

    private static void CounterKillIsSelfBoundAndUsesExactDamage()
    {
        var engine = new NinjaSkillEngine();
        NinjaSkillResult result = engine.Use(4, Actor(skills: Skill(NinjaSkillId.CounterKill, 4)),
            null, 6003, 4, 1_000);
        Equal(NinjaSkillResultCode.Applied, result.Code);
        Equal(50u, NinjaSkillEngine.CalculateCounterKillDamage(100, 4));
        True(engine.HasStatus(NinjaUid, NinjaStatusKind.CounterKill, 2_000));
    }

    private static void ArcherBaneRequiresFlyingPlayerTarget()
    {
        var engine = new NinjaSkillEngine();
        Equal(NinjaSkillResultCode.InvalidTarget,
            engine.Use(5, Actor(skills: Skill(NinjaSkillId.ArcherBane, 0)), Target(), 6004, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.Applied,
            engine.Use(6, Actor(skills: Skill(NinjaSkillId.ArcherBane, 0)), Target(flying: true), 6004, 0, 2_000).Code);
        Equal(10u, NinjaSkillEngine.CalculateArcherBaneCurrentHealth(100));
    }

    private static void ExperienceSkillsAreSelfBoundAndDamageIsBounded()
    {
        var actor = Actor(skills: Skills((NinjaSkillId.ShurikenVortex, 0), (NinjaSkillId.FatalStrike, 0)));
        Equal(NinjaSkillResultCode.Applied,
            new NinjaSkillEngine().Use(7, actor, null, 6010, 0, 1_000).Code);
        Equal(1u, NinjaSkillEngine.ApplyShurikenVortexIncomingDamage(99_999));
        Equal(NinjaSkillResultCode.Applied,
            new NinjaSkillEngine().Use(8, actor, null, 6011, 0, 1_000).Code);
        Equal(500u, NinjaSkillEngine.CalculateFatalStrikeDamage(100));
    }

    private static void ClassRangeStaminaCooldownAndLifeAreValidated()
    {
        var learned = Skill(NinjaSkillId.ToxicFog, 0);
        Equal(NinjaSkillResultCode.WrongProfession,
            new NinjaSkillEngine().Use(9, Actor(profession: 40, skills: learned), Target(), 6001, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.OutOfRange,
            new NinjaSkillEngine().Use(10, Actor(skills: learned), Target(point: new WorldPoint(1002, 400, 400)), 6001, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.WrongMap,
            new NinjaSkillEngine().Use(11, Actor(skills: learned), Target(point: new WorldPoint(1004, 350, 350)), 6001, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.InsufficientStamina,
            new NinjaSkillEngine().Use(12, Actor(stamina: 49, skills: learned), Target(), 6001, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.InvalidState,
            new NinjaSkillEngine().Use(13, Actor(alive: false, skills: learned), Target(), 6001, 0, 1_000).Code);
        var engine = new NinjaSkillEngine();
        Equal(NinjaSkillResultCode.Applied, engine.Use(14, Actor(skills: learned), Target(), 6001, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.Cooldown, engine.Use(15, Actor(skills: learned), Target(), 6001, 0, 1_999).Code);
        Equal(NinjaSkillResultCode.InvalidState, engine.Use(16, Actor(skills: learned), Target(), 6001, 0, 2_000).Code);
        var twofold = new NinjaSkillEngine();
        var twofoldActor = Actor(skills: Skill(NinjaSkillId.TwofoldBlades, 0));
        Equal(NinjaSkillResultCode.Applied, twofold.Use(19, twofoldActor, Target(), 6000, 0, 1_000).Code);
        Equal(NinjaSkillResultCode.Cooldown, twofold.Use(20, twofoldActor, Target(), 6000, 0, 1_799).Code);
        Equal(NinjaSkillResultCode.Applied, twofold.Use(21, twofoldActor, Target(), 6000, 0, 1_800).Code);
    }

    private static void ReplayAndConflictingReplayAreSafe()
    {
        var engine = new NinjaSkillEngine();
        var actor = Actor(skills: Skill(NinjaSkillId.ToxicFog, 0));
        Equal(NinjaSkillResultCode.Applied, engine.Use(17, actor, Target(), 6001, 0, 1_000).Code);
        NinjaSkillResult replay = engine.Use(17, actor, Target(), 6001, 0, 1_000);
        Equal(NinjaSkillResultCode.Replay, replay.Code);
        True(replay.Replayed);
        Equal(NinjaSkillResultCode.CommandConflict, engine.Use(17, actor, Target(), 6001, 1, 1_000).Code);
    }

    private static void TransientStatesAreClearedAtLifecycleBoundaries()
    {
        foreach (Action<NinjaSkillEngine> cleanup in new Action<NinjaSkillEngine>[]
        {
            engine => engine.OnDeath(NinjaUid), engine => engine.OnRevive(NinjaUid),
            engine => engine.OnLogout(NinjaUid), engine => engine.OnDisconnect(NinjaUid),
            engine => engine.OnMapTransition(NinjaUid)
        })
        {
            var engine = new NinjaSkillEngine();
            engine.Use(18, Actor(skills: Skill(NinjaSkillId.CounterKill, 0)), null, 6003, 0, 1_000);
            True(engine.HasStatus(NinjaUid, NinjaStatusKind.CounterKill, 1_001));
            cleanup(engine);
            False(engine.HasStatus(NinjaUid, NinjaStatusKind.CounterKill, 1_001));
        }
    }

    private static void OtherClassRegressionRemainsClosed()
    {
        foreach (byte profession in new byte[] { 10, 20, 40, 100, 132, 142 })
            Equal(NinjaSkillResultCode.WrongProfession,
                new NinjaSkillEngine().Use(profession, Actor(profession: profession,
                    skills: Skill(NinjaSkillId.TwofoldBlades, 0)), Target(), 6000, 0, 1_000).Code);
    }

    private static NinjaCombatant Actor(
        byte profession = 50,
        bool alive = true,
        ushort stamina = 100,
        uint? right = 601000,
        uint? left = 601000,
        IReadOnlyDictionary<NinjaSkillId, byte>? skills = null) =>
        new(NinjaUid, profession, 120, NinjaPoint, alive, stamina, right, left,
            skills ?? new Dictionary<NinjaSkillId, byte>());

    private static NinjaCombatant Target(WorldPoint? point = null, bool flying = false) =>
        new(TargetUid, 40, 120, point ?? new WorldPoint(1002, 352, 350), true, 100,
            null, null, new Dictionary<NinjaSkillId, byte>(), flying);

    private static IReadOnlyDictionary<NinjaSkillId, byte> Skill(NinjaSkillId skill, byte level) =>
        new Dictionary<NinjaSkillId, byte> { [skill] = level };

    private static IReadOnlyDictionary<NinjaSkillId, byte> Skills(params (NinjaSkillId Skill, byte Level)[] values) =>
        values.ToDictionary(value => value.Skill, value => value.Level);

    private static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    private static void False(bool value) { if (value) throw new InvalidOperationException("Expected false."); }
    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }
}
