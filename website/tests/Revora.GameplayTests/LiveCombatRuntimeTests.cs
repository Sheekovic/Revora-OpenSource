using Revora.Domain.Accounts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.GameplayTests;

internal static class LiveCombatRuntimeTests
{
    private static readonly GameplayActor First = Actor("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 3_000_001);
    private static readonly GameplayActor Second = Actor("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", 3_000_002);

    public static async Task<int> RunAsync()
    {
        await RegistrationLookupRelocationAndCleanupAreBounded();
        MapAndInstanceIsolationIsStrict();
        await PlayerVersusMonsterIsExactlyOnce();
        await PlayerVersusPlayerIsPolicyBound();
        await InvalidTargetsCoordinatesRangeAndFrequencyAreSafe();
        await PersistenceFailureLeavesHealthUnchanged();
        await ConcurrentAttacksKillExactlyOnce();
        NearbyDiscoveryIsMapInstanceAndRadiusBound();
        await NativeNinjaSkillsUseTheGenericAtomicPipeline();
        await TwofoldUsesTheAuthoritativeNormalDamageFormula();
        await NinjaExperienceStatusesAffectNormalCombatAuthoritatively();
        await CounterKillReactsOnceWithoutRecursion();
        await FlyingStateIsAuthoritativeAndLifecycleBounded();
        await TargetDeathClearsNinjaStatusBeforeRevive();
        await NinjaPersistenceFailureRollsBackEntityHealth();
        await MonsterHitInterruptsSittingAndMutatesStaminaExactlyOnce();
        await PlayerHitInterruptsSittingAndMutatesStaminaExactlyOnce();
        await MovementDoesNotInvalidateDurableCombatVersion();
        return 18;
    }

    private static async Task RegistrationLookupRelocationAndCleanupAreBounded()
    {
        var registry = new LiveCombatEntityRegistry(2);
        LiveCombatEntity player = Player(First, 1002, 0, 10, 10);
        LiveCombatEntity monster = Monster(4_000_001, 1002, 0, 11, 10, touched: 1);
        True(registry.TryRegister(player));
        False(registry.TryRegister(Player(First, 1002, 0, 10, 10)));
        True(registry.TryRegister(monster));
        False(registry.TryRegister(Monster(4_000_002, 1002, 0, 12, 10)));
        True(registry.TryResolve(First.CharacterUid.Value, out LiveCombatEntity? resolved));
        True(ReferenceEquals(player, resolved));
        True(await registry.RelocateAsync(First.CharacterUid.Value, new CombatMapKey(1004, 7), 20, 21, 2, default));
        Equal(new CombatMapKey(1004, 7), player.Snapshot().Map);
        Equal(1, await registry.RemoveAbandonedMonstersAsync(2, default));
        False(registry.TryResolve(4_000_001, out _));
        True(await registry.RemoveAsync(First.CharacterUid.Value, player, default));
        var dead = new LiveCombatEntity(new LiveCombatEntitySnapshot(First.CharacterUid.Value,
            LiveEntityKind.Player, First, new CombatMapKey(1004, 7), 20, 21,
            0, 100, false, 7, 2));
        True(registry.TryRegister(dead));
        True(await registry.ReviveAsync(First.CharacterUid.Value, 7, 100, 3, default));
        True(dead.Snapshot().Alive); True(dead.Snapshot().Attackable);
        True(await registry.RemoveAsync(First.CharacterUid.Value, dead, default));
        Equal(0, registry.Count);
    }

    private static void MapAndInstanceIsolationIsStrict()
    {
        var registry = new LiveCombatEntityRegistry();
        registry.TryRegister(Player(First, 1002, 1, 10, 10));
        registry.TryRegister(Monster(4_000_001, 1002, 2, 10, 10));
        Equal(0, registry.Nearby(new CombatMapKey(1002, 1), 10, 10, 18, First.CharacterUid.Value).Count);
    }

    private static async Task PlayerVersusMonsterIsExactlyOnce()
    {
        var fixture = Fixture(damage: 7);
        LiveCombatResult applied = await fixture.Pipeline.ExecuteNormalAsync(
            Request(1, First, 4_000_001, 11, 10, 2, 1_000), default,
            progressionPlanner: mutation => new CombatProgressionPlan(mutation.Damage * 2,
                mutation.Damage));
        Equal(LiveCombatResultCode.Applied, applied.Code);
        Equal(7L, applied.Damage);
        Equal(13L, applied.RemainingHealth);
        LiveCombatResult replay = await fixture.Pipeline.ExecuteNormalAsync(
            Request(1, First, 4_000_001, 11, 10, 2, 1_000), default);
        Equal(LiveCombatResultCode.Replay, replay.Code);
        Equal(13L, fixture.Monster.Snapshot().CurrentHealth);
        Equal(1, fixture.Persistence.Mutations.Count);
        Equal(new CombatProgressionPlan(14, 7), fixture.Persistence.Mutations.Single().Progression!);
        Equal(LiveCombatResultCode.CommandConflict, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(1, First, 4_000_001, 11, 10, 28, 2_000), default)).Code);
    }

    private static async Task PlayerVersusPlayerIsPolicyBound()
    {
        var denied = Fixture(includePlayerTarget: true, pvpMaps: new HashSet<int>());
        Equal(LiveCombatResultCode.PvpNotAllowed, (await denied.Pipeline.ExecuteNormalAsync(
            Request(2, First, Second.CharacterUid.Value, 11, 10, 2, 1_000), default)).Code);
        var allowed = Fixture(includePlayerTarget: true, pvpMaps: new HashSet<int> { 1002 });
        Equal(LiveCombatResultCode.Applied, (await allowed.Pipeline.ExecuteNormalAsync(
            Request(2, First, Second.CharacterUid.Value, 11, 10, 2, 1_000), default)).Code);
    }

    private static async Task InvalidTargetsCoordinatesRangeAndFrequencyAreSafe()
    {
        var fixture = Fixture();
        Equal(LiveCombatResultCode.TargetMissing, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(3, First, 9_999_999, 11, 10, 2, 1_000), default)).Code);
        Equal(LiveCombatResultCode.Applied, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(4, First, 4_000_001, 12, 10, 2, 1_000), default)).Code);
        Equal(LiveCombatResultCode.InvalidCoordinates, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(41, First, 4_000_001, 100, 100, 2, 1_600), default)).Code);
        await fixture.Registry.RelocateAsync(4_000_001, new CombatMapKey(1002, 0), 30, 30, 1, default);
        Equal(LiveCombatResultCode.OutOfRange, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(5, First, 4_000_001, 30, 30, 2, 1_000), default)).Code);
        await fixture.Registry.RelocateAsync(4_000_001, new CombatMapKey(1002, 0), 11, 10, 2, default);
        Equal(LiveCombatResultCode.Applied, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(6, First, 4_000_001, 11, 10, 2, 2_000), default)).Code);
        Equal(LiveCombatResultCode.Cooldown, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(7, First, 4_000_001, 11, 10, 2, 2_499), default)).Code);
    }

    private static async Task PersistenceFailureLeavesHealthUnchanged()
    {
        var fixture = Fixture();
        fixture.Persistence.FailNextCommit = true;
        Equal(LiveCombatResultCode.PersistenceFailure, (await fixture.Pipeline.ExecuteNormalAsync(
            Request(8, First, 4_000_001, 11, 10, 2, 1_000), default)).Code);
        Equal(20L, fixture.Monster.Snapshot().CurrentHealth);
        Equal(0, fixture.Persistence.Mutations.Count);
    }

    private static async Task ConcurrentAttacksKillExactlyOnce()
    {
        var fixture = Fixture(damage: 20, secondAttacker: true);
        Task<LiveCombatResult> first = fixture.Pipeline.ExecuteNormalAsync(
            Request(9, First, 4_000_001, 11, 10, 2, 1_000), default).AsTask();
        Task<LiveCombatResult> second = fixture.Pipeline.ExecuteNormalAsync(
            Request(10, Second, 4_000_001, 11, 10, 2, 1_000), default).AsTask();
        LiveCombatResult[] results = await Task.WhenAll(first, second);
        Equal(1, results.Count(result => result.Code == LiveCombatResultCode.Applied && result.Killed));
        Equal(1, results.Count(result => result.Code == LiveCombatResultCode.TargetDead));
        Equal(0L, fixture.Monster.Snapshot().CurrentHealth);
        Equal(1, fixture.Persistence.Mutations.Count);
    }

    private static void NearbyDiscoveryIsMapInstanceAndRadiusBound()
    {
        var registry = new LiveCombatEntityRegistry();
        registry.TryRegister(Player(First, 1002, 0, 10, 10));
        registry.TryRegister(Player(Second, 1002, 0, 28, 28));
        registry.TryRegister(Monster(4_000_001, 1002, 0, 29, 29));
        registry.TryRegister(Monster(4_000_002, 1002, 1, 10, 10));
        IReadOnlyList<LiveCombatEntitySnapshot> nearby = registry.Nearby(
            new CombatMapKey(1002, 0), 10, 10, 18, First.CharacterUid.Value);
        Equal(1, nearby.Count);
        Equal(Second.CharacterUid.Value, nearby[0].LegacyUid);
    }

    private static async Task NativeNinjaSkillsUseTheGenericAtomicPipeline()
    {
        foreach (NinjaSkillId skill in Enum.GetValues<NinjaSkillId>())
        {
            var fixture = Fixture(includePlayerTarget: skill is NinjaSkillId.PoisonStar or NinjaSkillId.ArcherBane,
                pvpMaps: new HashSet<int> { 1002 });
            if (skill == NinjaSkillId.ArcherBane)
                True(await fixture.Registry.SetFlyingAsync(Second.CharacterUid.Value, true, 900, default));
            NinjaSkillDefinition definition = NativeNinjaSkillCatalog5165.All.Single(value => value.Id == skill);
            uint targetUid = definition.TargetMode == NinjaTargetMode.Self
                ? First.CharacterUid.Value
                : definition.TargetMode == NinjaTargetMode.Player ? Second.CharacterUid.Value : 4_000_001;
            NinjaCombatant? target = definition.TargetMode == NinjaTargetMode.Self ? null :
                new NinjaCombatant(new CharacterUid(targetUid), definition.TargetMode == NinjaTargetMode.Player ? (byte)40 : (byte)0,
                    120, new WorldPoint(1002, 11, 10), true, 100, null, null,
                    new Dictionary<NinjaSkillId, byte>(), IsFlying: skill == NinjaSkillId.ArcherBane,
                    IsMonster: definition.TargetMode != NinjaTargetMode.Player);
            var actor = new NinjaCombatant(First.CharacterUid, 50, 120, new WorldPoint(1002, 10, 10), true,
                100, 601000, 601000, new Dictionary<NinjaSkillId, byte> { [skill] = 0 });
            NinjaLiveCombatResult result = await fixture.Pipeline.ExecuteNinjaAsync(new NinjaLiveCombatRequest(
                checked((uint)skill), First, targetUid,
                definition.TargetMode == NinjaTargetMode.Self ? (ushort)10 : (ushort)11,
                10, (ushort)skill, 0, 1_000, actor, target), default);
            Equal(NinjaSkillResultCode.Applied, result.Code);
            Equal(1, fixture.Persistence.SkillMutations.Count);
        }
    }

    private static async Task NinjaPersistenceFailureRollsBackEntityHealth()
    {
        var fixture = Fixture();
        fixture.Persistence.FailNextCommit = true;
        var actor = new NinjaCombatant(First.CharacterUid, 50, 120, new WorldPoint(1002, 10, 10), true,
            100, 601000, 601000, new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.TwofoldBlades] = 0 });
        var target = new NinjaCombatant(new CharacterUid(4_000_001), 0, 1,
            new WorldPoint(1002, 11, 10), true, 0, null, null,
            new Dictionary<NinjaSkillId, byte>(), IsMonster: true);
        NinjaLiveCombatResult result = await fixture.Pipeline.ExecuteNinjaAsync(new NinjaLiveCombatRequest(
            99, First, 4_000_001, 11, 10, 6000, 0, 1_000, actor, target), default);
        Equal(NinjaSkillResultCode.PersistenceFailure, result.Code);
        Equal(20L, fixture.Monster.Snapshot().CurrentHealth);
        Equal(0, fixture.Persistence.SkillMutations.Count);
    }

    private static async Task TwofoldUsesTheAuthoritativeNormalDamageFormula()
    {
        var fixture = Fixture(damage: 10);
        var actor = new NinjaCombatant(First.CharacterUid, 50, 120,
            new WorldPoint(1002, 10, 10), true, 100, 601000, 601000,
            new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.TwofoldBlades] = 0 });
        var target = new NinjaCombatant(new CharacterUid(4_000_001), 0, 1,
            new WorldPoint(1002, 11, 10), true, 0, null, null,
            new Dictionary<NinjaSkillId, byte>(), IsMonster: true);
        NinjaLiveCombatResult result = await fixture.Pipeline.ExecuteNinjaAsync(new NinjaLiveCombatRequest(
            6_000, First, 4_000_001, 11, 10, 6000, 0, 1_000, actor, target), default);
        Equal(NinjaSkillResultCode.Applied, result.Code);
        Equal(11L, result.Damage);
        Equal(9L, result.RemainingHealth);
    }

    private static async Task MonsterHitInterruptsSittingAndMutatesStaminaExactlyOnce()
    {
        var registry = new LiveCombatEntityRegistry();
        registry.TryRegister(Player(First, 1002, 0, 10, 10));
        registry.TryRegister(Monster(4_000_010, 1002, 0, 11, 10));
        var persistence = new InMemoryCombatPersistence();
        var pipeline = new LiveCombatPipeline(registry, new ConservativeNormalDamageFormula(1),
            new DenyByDefaultPvpPolicy(), persistence);
        var template = new OwnerMonsterTemplate(1, "Pheasant", OwnerMonsterBehavior.HuntPlayers,
            104, 1, 33, 0, 0, 0, 5, 6, 1, 1, OwnerMonsterAttackType.Melee,
            0, 0, true, 1, 10, 20, 800, 10_000, true).Validate();
        var stats = new PlayerCombatStatistics(1, 5, 2, 0, 4, 7, 1, 0, 0, 2, 0).Validate();
        Guid operation = Guid.NewGuid();
        LiveCombatResult applied = await pipeline.ExecuteMonsterAttackAsync(new MonsterCombatRequest(
            operation, 4_000_010, First, template, stats, 1_000, 60, true), default);
        Equal((ushort)30, applied.StaminaAfter!.Value); True(applied.SittingInterrupted);
        MonsterCombatMutation mutation = persistence.MonsterMutations.Single();
        Equal((ushort)60, mutation.PreviousStamina!.Value);
        Equal((ushort)30, mutation.RemainingStamina!.Value);
        LiveCombatResult replay = await pipeline.ExecuteMonsterAttackAsync(new MonsterCombatRequest(
            operation, 4_000_010, First, template, stats, 1_000, 60, true), default);
        True(replay.Replayed); Equal(1, persistence.MonsterMutations.Count);
    }

    private static async Task PlayerHitInterruptsSittingAndMutatesStaminaExactlyOnce()
    {
        var fixture = Fixture(includePlayerTarget: true, pvpMaps: new HashSet<int> { 1002 });
        var request = new NormalCombatRequest(6165, First, Second.CharacterUid.Value,
            11, 10, 2, 1_000, 55, true);
        LiveCombatResult applied = await fixture.Pipeline.ExecuteNormalAsync(request, default);
        Equal(LiveCombatResultCode.Applied, applied.Code);
        Equal((ushort)25, applied.StaminaAfter!.Value);
        True(applied.SittingInterrupted);
        CombatMutation mutation = fixture.Persistence.Mutations.Single();
        Equal((ushort)55, mutation.PreviousTargetStamina!.Value);
        Equal((ushort)25, mutation.RemainingTargetStamina!.Value);
        LiveCombatResult replay = await fixture.Pipeline.ExecuteNormalAsync(request, default);
        True(replay.Replayed);
        Equal((ushort)25, replay.StaminaAfter!.Value);
        Equal(1, fixture.Persistence.Mutations.Count);
    }

    private static async Task MovementDoesNotInvalidateDurableCombatVersion()
    {
        var registry = new LiveCombatEntityRegistry();
        LiveCombatEntity player = Player(First, 1002, 0, 10, 10);
        registry.TryRegister(player);
        registry.TryRegister(Monster(450_001, 1002, 0, 11, 10));
        var persistence = new InMemoryCombatPersistence();
        var pipeline = new LiveCombatPipeline(registry, new ConservativeNormalDamageFormula(1),
            new DenyByDefaultPvpPolicy(), persistence);

        True(await registry.RelocateAsync(First.CharacterUid.Value,
            new CombatMapKey(1002, 0), 12, 10, 100, default));
        Equal(2L, player.Snapshot().Version);
        Equal(1L, player.Snapshot().PersistenceVersion);

        var template = new OwnerMonsterTemplate(1, "Pheasant", OwnerMonsterBehavior.HuntPlayers,
            104, 1, 33, 0, 0, 0, 5, 6, 1, 1, OwnerMonsterAttackType.Melee,
            0, 0, true, 2, 10, 20, 800, 10_000, true).Validate();
        var stats = new PlayerCombatStatistics(1, 5, 2, 0, 4, 7, 1, 0, 0, 2, 0).Validate();
        LiveCombatResult result = await pipeline.ExecuteMonsterAttackAsync(new MonsterCombatRequest(
            Guid.NewGuid(), 450_001, First, template, stats, 1_000, 100, false), default);

        Equal(LiveCombatResultCode.Applied, result.Code);
        Equal(1L, persistence.MonsterMutations.Single().ExpectedTargetVersion);
        Equal(3L, player.Snapshot().Version);
        Equal(2L, player.Snapshot().PersistenceVersion);
    }

    private static async Task TargetDeathClearsNinjaStatusBeforeRevive()
    {
        var fixture = Fixture(damage: 20);
        var actor = new NinjaCombatant(First.CharacterUid, 50, 120,
            new WorldPoint(1002, 10, 10), true, 100, 601000, 601000,
            new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.ToxicFog] = 0 });
        var monster = new NinjaCombatant(new CharacterUid(4_000_001), 0, 1,
            new WorldPoint(1002, 11, 10), true, 0, null, null,
            new Dictionary<NinjaSkillId, byte>(), IsMonster: true);
        Equal(NinjaSkillResultCode.Applied, (await fixture.Pipeline.ExecuteNinjaAsync(
            new NinjaLiveCombatRequest(300, First, monster.CharacterUid.Value, 11, 10,
                6001, 0, 1_000, actor, monster), default)).Code);
        LiveCombatResult killed = await fixture.Pipeline.ExecuteNormalAsync(
            Request(301, First, monster.CharacterUid.Value, 11, 10, 2, 2_000), default);
        True(killed.Killed);
        Equal(3L, fixture.Monster.Snapshot().Version);
        True(await fixture.Registry.ReviveAsync(monster.CharacterUid.Value, 3, 20, 2_500, default));
        Equal(NinjaSkillResultCode.Applied, (await fixture.Pipeline.ExecuteNinjaAsync(
            new NinjaLiveCombatRequest(302, First, monster.CharacterUid.Value, 11, 10,
                6001, 0, 3_000, actor, monster), default)).Code);
    }

    private static async Task NinjaExperienceStatusesAffectNormalCombatAuthoritatively()
    {
        var fatal = Fixture(damage: 2);
        var fatalActor = new NinjaCombatant(First.CharacterUid, 50, 120,
            new WorldPoint(1002, 10, 10), true, 100, 601000, 601000,
            new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.FatalStrike] = 0 });
        Equal(NinjaSkillResultCode.Applied, (await fatal.Pipeline.ExecuteNinjaAsync(
            new NinjaLiveCombatRequest(200, First, First.CharacterUid.Value, 10, 10,
                6011, 0, 1_000, fatalActor, null), default)).Code);
        LiveCombatResult fatalHit = await fatal.Pipeline.ExecuteNormalAsync(
            Request(201, First, 4_000_001, 11, 10, 2, 2_000), default);
        Equal(10L, fatalHit.Damage);

        var vortex = Fixture(damage: 12, includePlayerTarget: true,
            pvpMaps: new HashSet<int> { 1002 });
        var vortexActor = new NinjaCombatant(Second.CharacterUid, 50, 120,
            new WorldPoint(1002, 11, 10), true, 100, 601000, 601000,
            new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.ShurikenVortex] = 0 });
        Equal(NinjaSkillResultCode.Applied, (await vortex.Pipeline.ExecuteNinjaAsync(
            new NinjaLiveCombatRequest(202, Second, Second.CharacterUid.Value, 11, 10,
                6010, 0, 1_000, vortexActor, null), default)).Code);
        LiveCombatResult reduced = await vortex.Pipeline.ExecuteNormalAsync(
            Request(203, First, Second.CharacterUid.Value, 11, 10, 2, 2_000), default);
        Equal(1L, reduced.Damage);
    }

    private static async Task CounterKillReactsOnceWithoutRecursion()
    {
        var fixture = Fixture(damage: 10, includePlayerTarget: true,
            pvpMaps: new HashSet<int> { 1002 });
        var defender = new NinjaCombatant(Second.CharacterUid, 50, 120,
            new WorldPoint(1002, 11, 10), true, 100, 601000, 601000,
            new Dictionary<NinjaSkillId, byte> { [NinjaSkillId.CounterKill] = 4 });
        Equal(NinjaSkillResultCode.Applied, (await fixture.Pipeline.ExecuteNinjaAsync(
            new NinjaLiveCombatRequest(400, Second, Second.CharacterUid.Value, 11, 10,
                6003, 4, 1_000, defender, null), default)).Code);

        LiveCombatResult? triggered = null;
        for (uint sequence = 401; sequence < 501 && triggered is null; sequence++)
        {
            LiveCombatResult result = await fixture.Pipeline.ExecuteNormalAsync(
                Request(sequence, First, Second.CharacterUid.Value, 11, 10, 2,
                    2_000 + checked((sequence - 401) * 500)), default);
            if (result.CounterKillTriggered) triggered = result;
        }
        True(triggered is not null);
        Equal(5L, triggered!.CounterDamage);
        Equal(0L, triggered.Damage);
        Equal(Second.CharacterUid.Value, triggered.CounterAttackerUid);
        Equal(First.CharacterUid.Value, triggered.CounterTargetUid);
        True(fixture.Registry.TryResolve(First.CharacterUid.Value, out LiveCombatEntity? attacker));
        Equal(95L, attacker.Snapshot().CurrentHealth);
    }

    private static async Task FlyingStateIsAuthoritativeAndLifecycleBounded()
    {
        var fixture = Fixture(includePlayerTarget: true, pvpMaps: new HashSet<int> { 1002 });
        long version = fixture.Registry.TryResolve(Second.CharacterUid.Value, out LiveCombatEntity? before)
            ? before.Snapshot().Version : throw new InvalidOperationException("The flying target was missing.");
        True(await fixture.Registry.SetFlyingAsync(Second.CharacterUid.Value, true, 1_000, default));
        True(fixture.Registry.TryResolve(Second.CharacterUid.Value, out LiveCombatEntity? target));
        True(target.Snapshot().IsFlying);
        Equal(version, target.Snapshot().Version);
        True(await fixture.Registry.RelocateAsync(Second.CharacterUid.Value,
            new CombatMapKey(1004, 0), 20, 20, 2_000, default));
        False(target.Snapshot().IsFlying);
    }

    private static FixtureState Fixture(long damage = 5, bool includePlayerTarget = false,
        bool secondAttacker = false, IReadOnlySet<int>? pvpMaps = null)
    {
        var registry = new LiveCombatEntityRegistry();
        registry.TryRegister(Player(First, 1002, 0, 10, 10));
        if (secondAttacker || includePlayerTarget) registry.TryRegister(Player(Second, 1002, 0, 11, 10));
        LiveCombatEntity monster = Monster(4_000_001, 1002, 0, 11, 10);
        registry.TryRegister(monster);
        var persistence = new InMemoryCombatPersistence();
        var pipeline = new LiveCombatPipeline(registry, new ConservativeNormalDamageFormula(damage),
            new DenyByDefaultPvpPolicy(pvpMaps), persistence);
        return new(registry, monster, persistence, pipeline);
    }

    private static NormalCombatRequest Request(uint sequence, GameplayActor actor, uint target,
        ushort x, ushort y, uint action, long now) => new(sequence, actor, target, x, y, action, now);

    private static LiveCombatEntity Player(GameplayActor actor, int map, int instance, ushort x, ushort y) =>
        new(new LiveCombatEntitySnapshot(actor.CharacterUid.Value, LiveEntityKind.Player, actor,
            new CombatMapKey(map, instance), x, y, 100, 100, true, 1, 0));

    private static LiveCombatEntity Monster(uint uid, int map, int instance, ushort x, ushort y,
        long touched = 0) => new(new LiveCombatEntitySnapshot(uid, LiveEntityKind.Monster, null,
            new CombatMapKey(map, instance), x, y, 20, 20, true, 1, touched));

    private static GameplayActor Actor(string account, uint uid) =>
        new(new AccountId(Guid.Parse(account)), new CharacterUid(uid));

    private static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true."); }
    private static void False(bool value) { if (value) throw new InvalidOperationException("Expected false."); }
    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }

    private sealed record FixtureState(LiveCombatEntityRegistry Registry, LiveCombatEntity Monster,
        InMemoryCombatPersistence Persistence, LiveCombatPipeline Pipeline);
}
