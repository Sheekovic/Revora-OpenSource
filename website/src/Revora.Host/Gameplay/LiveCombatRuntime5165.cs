using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Revora.Domain.Accounts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;
using Revora.Host.Logging;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;
using Revora.Protocol5165;

namespace Revora.Host.Gameplay;

public sealed record MonsterRuntimeMetrics(
    int ActiveEntities,
    int ActiveGroundItems,
    long AiTicks,
    long Attacks,
    long Deaths,
    long Respawns,
    long RewardTransactions,
    long SuccessfulPickups,
    long ExpiredGroundItems,
    long MaximumSchedulerDelayMilliseconds);

/// <summary>
/// Gated adapter between authenticated 5165 sessions and the generic combat pipeline.
/// It owns no authentication or character identity; both are supplied by the consumed
/// handoff-backed Game session.
/// </summary>
public sealed class LiveCombatRuntime5165 : IAsyncDisposable
{
    public const string FeatureKey = "combat.runtime";
    private readonly PostgreSqlCombatRuntime persistence;
    private readonly PostgreSqlGroundDropRuntime groundPersistence;
    private readonly IMapCatalog5165 maps;
    private readonly LiveCombatEntityRegistry registry;
    private readonly LiveCombatPipeline pipeline;
    private readonly SafeConsoleLog log;
    private readonly ConcurrentDictionary<uint, GameplaySession5165> sessions = new();
    private readonly ConcurrentDictionary<uint, PlayerCombatStatistics> playerStatistics = new();
    private readonly ConcurrentDictionary<uint, ActiveStamina> stamina = new();
    private readonly ConcurrentDictionary<uint, OwnerMonsterRuntimeDefinition> monsters = new();
    private readonly ConcurrentDictionary<uint, MonsterAgent> monsterAgents = new();
    private readonly ConcurrentDictionary<uint, GroundDropSnapshot> groundDrops = new();
    private IReadOnlyDictionary<int, OwnerMonsterDropRule[]> dropRules =
        new Dictionary<int, OwnerMonsterDropRule[]>();
    private IReadOnlyDictionary<ushort, OwnerEquipmentDropCandidate[]> equipmentDropCandidates =
        new Dictionary<ushort, OwnerEquipmentDropCandidate[]>();
    private IReadOnlyDictionary<uint, OwnerCommonDropItem> commonDropItems =
        new Dictionary<uint, OwnerCommonDropItem>();
    private readonly Dictionary<int, MonsterAgent[]> monsterAgentsByMap = [];
    private readonly ConcurrentDictionary<uint, HashSet<uint>> visibleMonsters = new();
    private readonly ConcurrentDictionary<uint, HashSet<uint>> visibleGroundDrops = new();
    private readonly CancellationTokenSource schedulerLifetime = new();
    private Task? staminaScheduler;
    private Task? monsterScheduler;
    private long monsterAiTicks;
    private long monsterAttacks;
    private long monsterDeaths;
    private long monsterRespawns;
    private long rewardTransactions;
    private long successfulPickups;
    private long expiredGroundItems;
    private long maximumSchedulerDelayMilliseconds;

    private sealed record ActiveStamina(GameplayActor Actor, GameplaySession5165 Session,
        StaminaResource Resource, SemaphoreSlim MutationGate);

    private sealed class MonsterAgent(OwnerMonsterRuntimeDefinition definition, long now,
        long spawnGeneration)
    {
        public OwnerMonsterRuntimeDefinition Definition { get; } = definition;
        public long NextMoveMilliseconds = now;
        public long NextAttackMilliseconds = now;
        public long DiedMilliseconds = -1;
        public bool CorpseRemoved;
        public uint Sequence;
        public long SpawnGeneration = spawnGeneration > 0 ? spawnGeneration :
            throw new ArgumentOutOfRangeException(nameof(spawnGeneration));
        public int TickClaim;
    }

    public LiveCombatRuntime5165(string connectionString, SafeConsoleLog log, IMapCatalog5165 maps,
        IReadOnlySet<int>? pvpMaps = null, int maximumEntities = 75_000)
    {
        persistence = new PostgreSqlCombatRuntime(connectionString);
        groundPersistence = new PostgreSqlGroundDropRuntime(connectionString);
        registry = new LiveCombatEntityRegistry(maximumEntities);
        pipeline = new LiveCombatPipeline(registry, new OwnerSourceLiveDamageFormula(
                uid => playerStatistics.TryGetValue(uid, out PlayerCombatStatistics? stats) ? stats : null,
                uid => monsters.TryGetValue(uid, out OwnerMonsterRuntimeDefinition? monster)
                    ? monster.Template : null),
            new DenyByDefaultPvpPolicy(pvpMaps), persistence);
        this.maps = maps ?? throw new ArgumentNullException(nameof(maps));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public int EntityCount => registry.Count;

    public MonsterRuntimeMetrics GetMetrics() => new(
        registry.Count, groundDrops.Count,
        Interlocked.Read(ref monsterAiTicks), Interlocked.Read(ref monsterAttacks),
        Interlocked.Read(ref monsterDeaths), Interlocked.Read(ref monsterRespawns),
        Interlocked.Read(ref rewardTransactions), Interlocked.Read(ref successfulPickups),
        Interlocked.Read(ref expiredGroundItems), Interlocked.Read(ref maximumSchedulerDelayMilliseconds));

    public bool IsItemUseSuppressed(uint characterUid) =>
        pipeline.IsNinjaItemUseSuppressed(new CharacterUid(characterUid), MonotonicMilliseconds());

    public async Task<RecoveryItemPersistenceResult> ConsumeRecoveryItemAsync(AccountId accountId,
        GameplaySession5165 session, Guid operationId, uint itemUid, uint itemType,
        int healthRecovery, int manaRecovery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        uint uid = checked((uint)session.Character.LegacyUid);
        if (!sessions.TryGetValue(uid, out GameplaySession5165? active) || !ReferenceEquals(active, session))
            return new(RecoveryItemPersistenceCode.Conflict, 0, 0, 0);
        var actor = new GameplayActor(accountId, new CharacterUid(uid));
        return await registry.ExecuteDurableHealthMutationAsync(uid, async (snapshot, token) =>
        {
            var mutation = new RecoveryItemMutation(operationId, actor, itemUid, itemType,
                healthRecovery, manaRecovery, snapshot.Version, snapshot.PersistenceVersion,
                snapshot.CurrentHealth);
            RecoveryItemPersistenceResult persisted = await persistence.ConsumeRecoveryItemAsync(mutation, token);
            return new DurableHealthMutation<RecoveryItemPersistenceResult>(persisted, persisted.Changed,
                persisted.CurrentHealth, persisted.PersistenceVersion);
        }, MonotonicMilliseconds(), cancellationToken);
    }

    /// <summary>
    /// Applies a server-authoritative flying transition after a validated class
    /// ability or controlled integration fixture. Client-supplied flags never
    /// reach this method directly.
    /// </summary>
    public async Task<bool> SetPlayerFlyingStateAsync(AccountId accountId, GameplaySession5165 session,
        bool isFlying, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        uint uid = checked((uint)session.Character.LegacyUid);
        if (!sessions.TryGetValue(uid, out GameplaySession5165? active) || !ReferenceEquals(active, session))
            return false;
        var actor = new GameplayActor(accountId, new CharacterUid(uid));
        if (!await persistence.IsEnabledAsync(accountId, actor.CharacterUid, FeatureKey, cancellationToken))
            return false;
        bool changed = await registry.SetFlyingAsync(uid, isFlying, MonotonicMilliseconds(), cancellationToken);
        if (changed)
            log.Audit("CombatFlyingStateChanged",
                $"A validated server-owned flying state changed (uid={uid}; flying={isFlying}).");
        return changed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        OwnerMonsterCatalog catalog = await persistence.LoadEnabledOwnerMonsterCatalogAsync(cancellationToken);
        IReadOnlyDictionary<uint, long> nextSpawnGenerations =
            await persistence.LoadNextMonsterSpawnGenerationsAsync(cancellationToken);
        dropRules = catalog.DropRules.GroupBy(value => value.MonsterType)
            .ToDictionary(value => value.Key, value => value.OrderBy(row => row.Ordinal).ToArray());
        equipmentDropCandidates = catalog.EquipmentDropCandidates.GroupBy(value => value.MonsterLevel)
            .ToDictionary(value => value.Key, value => value.OrderBy(row => row.Ordinal).ToArray());
        commonDropItems = catalog.CommonDropItems.GroupBy(value => value.ItemType)
            .ToDictionary(value => value.Key, value => value.First());
        int enabledPopulation = 0;
        var occupied = new HashSet<(int MapId, ushort X, ushort Y)>();
        IReadOnlyDictionary<(long GeneratorId, int Ordinal), uint> assignedUids =
            catalog.UidAssignments.ToDictionary(value => (value.GeneratorId, value.Ordinal),
                value => value.LegacyUid);
        foreach (OwnerMonsterGenerator generator in catalog.Generators)
        {
            if (!maps.TryGet(generator.MapId, out IMapValidation5165? map))
                throw new InvalidDataException("An enabled owner monster generator references an unavailable DMap.");
            OwnerMonsterTemplate template = catalog.Templates[generator.MonsterType];
            for (int ordinal = 0; ordinal < generator.Population; ordinal++)
            {
                if (!assignedUids.TryGetValue((generator.GeneratorId, ordinal), out uint uid))
                    throw new InvalidDataException("An enabled owner monster has no durable exact-client UID assignment.");
                (ushort x, ushort y) = ResolveSpawnCell(map, generator, ordinal, occupied);
                occupied.Add((generator.MapId, x, y));
                long now = MonotonicMilliseconds();
                var snapshot = new LiveCombatEntitySnapshot(uid, LiveEntityKind.Monster, null,
                    new CombatMapKey(generator.MapId, 0), x, y, template.MaximumHealth,
                    template.MaximumHealth, template.Classification != OwnerMonsterClassification.Guard,
                    1, now).Validate();
                var row = new OwnerMonsterRuntimeDefinition(uid, template, generator, x, y,
                    checked((byte)((generator.GeneratorId + ordinal) % 8)));
                byte[] validated = CombatPackets5165.EncodeMonsterSpawn(template.Mesh, uid,
                    checked((ushort)Math.Min(ushort.MaxValue, template.MaximumHealth)), template.Level,
                    x, y, row.Direction, 100, template.Name);
                Array.Clear(validated);
                if (!TryRegisterMonster(snapshot) || !monsters.TryAdd(uid, row))
                    throw new InvalidDataException("The reviewed owner monster catalog contains a duplicate live UID.");
                long spawnGeneration = nextSpawnGenerations.GetValueOrDefault(uid, 1);
                if (!monsterAgents.TryAdd(uid, new MonsterAgent(row, now, spawnGeneration)))
                    throw new InvalidDataException("The reviewed owner monster catalog contains a duplicate AI entity.");
                enabledPopulation++;
            }
        }
        foreach (IGrouping<int, MonsterAgent> map in monsterAgents.Values
                     .GroupBy(value => value.Definition.Generator.MapId))
            monsterAgentsByMap.Add(map.Key, map.OrderBy(value => value.Definition.LegacyUid).ToArray());
        foreach (GroundDropSnapshot drop in await groundPersistence.LoadActiveAsync(cancellationToken))
            if (!groundDrops.TryAdd(drop.GroundUid, drop))
                throw new InvalidDataException("A recovered ground-item UID is duplicated.");
        log.Audit("CombatMonsterCatalogLoaded",
            $"The owner monster catalog was loaded (templates={catalog.Templates.Count}; generators={catalog.Generators.Count}; enabledPopulation={enabledPopulation}; drops={catalog.DropRules.Count}).");
        staminaScheduler = RunStaminaSchedulerAsync(schedulerLifetime.Token);
        monsterScheduler = RunMonsterSchedulerAsync(schedulerLifetime.Token);
    }

    public async Task<bool> EnterPlayerAsync(AccountId accountId, GameplaySession5165 session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var actor = new GameplayActor(accountId,
            new CharacterUid(checked((uint)session.Character.LegacyUid)));
        if (!await persistence.IsEnabledAsync(accountId, actor.CharacterUid, FeatureKey, cancellationToken))
            return false;
        LiveCombatEntitySnapshot? snapshot = await persistence.LoadPlayerAsync(actor, 0,
            MonotonicMilliseconds(), cancellationToken);
        if (snapshot is null) throw new InvalidDataException("The authenticated combat player does not exist.");
        var entity = new LiveCombatEntity(snapshot);
        PlayerCombatStatistics stats = await persistence.LoadPlayerCombatStatisticsAsync(actor, cancellationToken)
            ?? throw new InvalidDataException("The authenticated combat statistics do not exist.");
        bool registered = registry.TryRegister(entity);
        if (!registered || !sessions.TryAdd(actor.CharacterUid.Value, session))
        {
            if (registered)
                await registry.RemoveAsync(actor.CharacterUid.Value, entity, cancellationToken);
            throw new InvalidDataException("The authenticated combat entity is already active.");
        }
        if (!playerStatistics.TryAdd(actor.CharacterUid.Value, stats))
            throw new InvalidDataException("The authenticated combat statistics are already active.");
        log.Audit("CombatEntityRegistered",
            $"An authenticated player entered the gated combat registry (uid={actor.CharacterUid.Value}; map={snapshot.Map.MapId}; instance={snapshot.Map.InstanceId}).");
        await SynchronizeMonstersAsync(session, cancellationToken);
        await SynchronizeGroundDropsAsync(session, cancellationToken);
        foreach (WeaponProficiencyCommitResult proficiency in
                 await persistence.LoadProficienciesAsync(actor, cancellationToken))
            await session.SendAsync(CombatPackets5165.EncodeProficiency(proficiency.ProficiencyType,
                proficiency.LevelAfter, proficiency.ExperienceAfter), cancellationToken);
        if (await persistence.IsEnabledAsync(accountId, actor.CharacterUid, "ninja.native", cancellationToken))
        {
            NinjaCombatant ninja = await persistence.LoadNinjaCombatantAsync(actor, cancellationToken)
                ?? throw new InvalidDataException("The enabled Ninja skill state is unavailable.");
            foreach (NinjaSkillId skillId in ninja.LearnedSkills.Keys.Order())
            {
                PersistedSkillState skill = await persistence.ReadSkillAsync(actor, (ushort)skillId,
                    cancellationToken) ?? throw new InvalidDataException("A learned Ninja skill row disappeared.");
                await session.SendAsync(NinjaPackets5165.EncodeSkillInformation(skill.Experience,
                    skill.SkillId, skill.Level), cancellationToken);
            }
        }
        return true;
    }

    public async Task EnterStaminaAsync(AccountId accountId, GameplaySession5165 session,
        CancellationToken cancellationToken)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        var actor = new GameplayActor(accountId, new CharacterUid(uid));
        ushort persisted = await persistence.LoadStaminaAsync(actor, cancellationToken)
            ?? throw new InvalidDataException("The authenticated player's Stamina state does not exist.");
        var active = new ActiveStamina(actor, session,
            new StaminaResource(persisted, MonotonicMilliseconds()), new SemaphoreSlim(1, 1));
        if (!stamina.TryAdd(uid, active))
            throw new InvalidDataException("The authenticated Stamina entity is already active.");
        await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(uid, 8, persisted), cancellationToken);
    }

    public void LeaveStamina(GameplaySession5165 session)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        if (stamina.TryGetValue(uid, out ActiveStamina? active) && ReferenceEquals(active.Session, session))
            stamina.TryRemove(new KeyValuePair<uint, ActiveStamina>(uid, active));
    }

    public async Task RelocatePlayerAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        if (!sessions.ContainsKey(uid)) return;
        if (registry.TryResolve(uid, out LiveCombatEntity? active) &&
            active.Snapshot().Map.MapId != session.Character.MapId)
            pipeline.ClearNinjaTransientState(new CharacterUid(uid));
        if (!await registry.RelocateAsync(uid, new CombatMapKey(session.Character.MapId, 0),
            checked((ushort)session.Character.X), checked((ushort)session.Character.Y),
            MonotonicMilliseconds(), cancellationToken))
            throw new InvalidDataException("The active combat player could not be relocated.");
        await SynchronizeMonstersAsync(session, cancellationToken);
        await SynchronizeGroundDropsAsync(session, cancellationToken);
        MarkStaminaActivity(session, StaminaActivity.Moving);
    }

    public void MarkStaminaActivity(GameplaySession5165 session, StaminaActivity activity)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        if (stamina.TryGetValue(uid, out ActiveStamina? active) && ReferenceEquals(active.Session, session))
            active.Resource.SetActivity(activity, MonotonicMilliseconds());
    }

    public async Task<bool> RevivePlayerAsync(AccountId accountId, GameplaySession5165 session,
        CancellationToken cancellationToken)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        if (!registry.TryResolve(uid, out LiveCombatEntity? entity) || entity.Snapshot().Alive)
            return false;
        (int MaximumHealth, ushort Stamina, long PersistenceVersion)? revived = await persistence.RevivePlayerAsync(
            new GameplayActor(accountId, new CharacterUid(uid)), cancellationToken);
        if (!revived.HasValue) return false;
        LiveCombatEntitySnapshot before = entity.Snapshot();
        if (!await registry.CommitDurableAsync(uid, before.Version, before.PersistenceVersion,
                revived.Value.PersistenceVersion, revived.Value.MaximumHealth, true,
                MonotonicMilliseconds(), cancellationToken))
            throw new InvalidOperationException("A committed player revive lost its live entity version.");
        pipeline.ClearNinjaTransientState(new CharacterUid(uid));
        if (stamina.TryGetValue(uid, out ActiveStamina? active))
            active.Resource.Revive(MonotonicMilliseconds());
        await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(uid, 1,
            checked((ulong)revived.Value.MaximumHealth)), cancellationToken);
        await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(uid, 8,
            revived.Value.Stamina), cancellationToken);
        return true;
    }

    public async Task LeavePlayerAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        uint uid = checked((uint)session.Character.LegacyUid);
        sessions.TryRemove(new KeyValuePair<uint, GameplaySession5165>(uid, session));
        playerStatistics.TryRemove(uid, out _);
        visibleMonsters.TryRemove(uid, out _);
        visibleGroundDrops.TryRemove(uid, out _);
        pipeline.RemoveAttackerState(uid);
        pipeline.ClearNinjaTransientState(new CharacterUid(uid));
        if (registry.TryResolve(uid, out LiveCombatEntity? entity))
            await registry.RemoveAsync(uid, entity, cancellationToken);
    }

    public bool TryRegisterMonster(LiveCombatEntitySnapshot monster)
    {
        if (monster.Kind != LiveEntityKind.Monster || monster.Owner is not null)
            throw new ArgumentException("Only a validated monster can be registered.", nameof(monster));
        return registry.TryRegister(new LiveCombatEntity(monster));
    }

    public async Task<LiveCombatResult> HandleNormalAttackAsync(AccountId accountId,
        GameplaySession5165 session, NormalAttack5165 attack, CancellationToken cancellationToken)
    {
        uint actorUid = checked((uint)session.Character.LegacyUid);
        if (!sessions.TryGetValue(actorUid, out GameplaySession5165? active) || !ReferenceEquals(active, session))
            return new(LiveCombatResultCode.AttackerMissing, actorUid, attack.TargetUid,
                SafeCategory: "combat-feature-disabled-or-session-missing");
        MonsterAgent? rewardAgent = monsterAgents.GetValueOrDefault(attack.TargetUid);
        ActiveStamina? hitStamina = attack.TargetUid != actorUid &&
            stamina.TryGetValue(attack.TargetUid, out ActiveStamina? foundStamina) ? foundStamina : null;
        if (hitStamina is not null) await hitStamina.MutationGate.WaitAsync(cancellationToken);
        LiveCombatResult result;
        NormalCombatRequest request;
        try
        {
            StaminaSnapshot? targetResource = hitStamina?.Resource.Snapshot();
            request = new NormalCombatRequest(
                attack.ClientTimestamp,
                new GameplayActor(accountId, new CharacterUid(actorUid)),
                attack.TargetUid,
                attack.ClaimedTargetX,
                attack.ClaimedTargetY,
                attack.Action,
                MonotonicMilliseconds(),
                targetResource?.Current,
                targetResource?.Activity == StaminaActivity.Sitting);
            result = await pipeline.ExecuteNormalAsync(request, cancellationToken,
                rewardAgent is null ? null : mutation => PlanMonsterReward(rewardAgent,
                    request.AuthenticatedActor, mutation),
                rewardAgent is null ? null : mutation => PlanCombatProgression(rewardAgent,
                    request.AuthenticatedActor, mutation.Damage, awardWeaponProficiency:
                    attack.Action is 2 or 28));
            if (result.Succeeded && result.StaminaAfter is ushort targetStamina)
                hitStamina?.Resource.Synchronize(targetStamina,
                    result.Killed ? StaminaActivity.Dead : result.SittingInterrupted
                        ? StaminaActivity.Standing : targetResource?.Activity ?? StaminaActivity.Standing,
                    MonotonicMilliseconds());
        }
        finally
        {
            hitStamina?.MutationGate.Release();
        }
        if (result.Code == LiveCombatResultCode.Applied)
        {
            ushort authoritativeTargetX = attack.ClaimedTargetX;
            ushort authoritativeTargetY = attack.ClaimedTargetY;
            if (registry.TryResolve(attack.TargetUid, out LiveCombatEntity? responseTarget))
            {
                LiveCombatEntitySnapshot responseSnapshot = responseTarget.Snapshot();
                authoritativeTargetX = responseSnapshot.X;
                authoritativeTargetY = responseSnapshot.Y;
            }
            if (result.Progression is not null)
                await SynchronizeCombatProgressionAsync(request.AuthenticatedActor,
                    result.Progression, cancellationToken);
            if (result.SittingInterrupted && sessions.TryGetValue(attack.TargetUid,
                    out GameplaySession5165? sittingTarget))
            {
                await sittingTarget.SendAsync(ItemEconomyPackets5165.EncodeStatus(attack.TargetUid, 8,
                    result.StaminaAfter ?? 0), cancellationToken);
                await BroadcastAsync(actorUid, attack.TargetUid,
                    GamePacketsAfterAuthentication5165.EncodeEntityAction(attack.TargetUid,
                        authoritativeTargetX, authoritativeTargetY, 100, attack.ClientTimestamp),
                    cancellationToken);
            }
            if (result.CounterKillTriggered)
            {
                await BroadcastAsync(result.CounterAttackerUid, result.CounterTargetUid,
                    NinjaPackets5165.EncodeSkillUse(result.CounterAttackerUid,
                        checked((ushort)session.Character.X), checked((ushort)session.Character.Y),
                        (ushort)NinjaSkillId.CounterKill, result.CounterSkillLevel,
                        [new SkillTarget5165(result.CounterTargetUid, checked((uint)result.CounterDamage))]),
                    cancellationToken);
                if (result.CounterDamage > 0 && registry.TryResolve(result.CounterTargetUid,
                        out LiveCombatEntity? counterTarget) && !counterTarget.Snapshot().Alive)
                {
                    pipeline.ClearNinjaTransientState(new CharacterUid(result.CounterTargetUid));
                    await BroadcastAsync(result.CounterAttackerUid, result.CounterTargetUid,
                        NinjaPackets5165.EncodeKillResult(result.CounterAttackerUid, result.CounterTargetUid,
                            attack.ClaimedTargetX, attack.ClaimedTargetY, attack.ClientTimestamp), cancellationToken);
                }
            }
            else
            {
            byte[] response = NinjaPackets5165.EncodeNormalAttackResult(actorUid, attack.TargetUid,
                authoritativeTargetX, authoritativeTargetY, attack.Action,
                checked((uint)result.Damage), attack.ClientTimestamp);
            await BroadcastAsync(actorUid, attack.TargetUid, response, cancellationToken);
            if (result.Killed)
            {
                if (monsterAgents.TryGetValue(attack.TargetUid, out MonsterAgent? killedAgent))
                {
                    killedAgent.DiedMilliseconds = MonotonicMilliseconds();
                    if (result.Reward is MonsterRewardCommitResult reward)
                        await BroadcastMonsterRewardAsync(killedAgent, request.AuthenticatedActor,
                            reward, cancellationToken);
                }
                if (registry.TryResolve(attack.TargetUid, out LiveCombatEntity? killedTarget) &&
                    killedTarget.Snapshot().Kind == LiveEntityKind.Player)
                    pipeline.ClearNinjaTransientState(new CharacterUid(attack.TargetUid));
                byte[] killed = NinjaPackets5165.EncodeKillResult(actorUid, attack.TargetUid,
                    authoritativeTargetX, authoritativeTargetY, attack.ClientTimestamp);
                await BroadcastAsync(actorUid, attack.TargetUid, killed, cancellationToken);
            }
            }
        }
        log.Audit(result.Succeeded ? "Combat1022Accepted" : "Combat1022Rejected",
            $"A normal combat request completed safely (attacker={actorUid}; target={attack.TargetUid}; action={attack.Action}; category={result.SafeCategory}; damage={result.Damage}; remaining={result.RemainingHealth}; killed={result.Killed}; replayed={result.Replayed}).");
        return result;
    }

    public async Task<NinjaLiveCombatResult> HandleNinjaSkillAsync(AccountId accountId,
        GameplaySession5165 session, SkillAttack5165 attack, CancellationToken cancellationToken)
    {
        uint actorUid = checked((uint)session.Character.LegacyUid);
        if (!stamina.TryGetValue(actorUid, out ActiveStamina? activeStamina))
            return new(NinjaSkillResultCode.InvalidState, actorUid, attack.TargetUid, attack.SkillId,
                attack.SkillLevel, 0, 0, 0, false, null, false, "stamina-state-unavailable");
        await activeStamina.MutationGate.WaitAsync(cancellationToken);
        try
        {
            return await HandleNinjaSkillCoreAsync(accountId, session, attack, activeStamina,
                cancellationToken);
        }
        finally
        {
            activeStamina.MutationGate.Release();
        }
    }

    private async Task<NinjaLiveCombatResult> HandleNinjaSkillCoreAsync(AccountId accountId,
        GameplaySession5165 session, SkillAttack5165 attack, ActiveStamina activeStamina,
        CancellationToken cancellationToken)
    {
        uint actorUid = checked((uint)session.Character.LegacyUid);
        var actorIdentity = new GameplayActor(accountId, new CharacterUid(actorUid));
        if (!sessions.TryGetValue(actorUid, out GameplaySession5165? active) || !ReferenceEquals(active, session) ||
            !await persistence.IsEnabledAsync(accountId, actorIdentity.CharacterUid, "ninja.native", cancellationToken))
            return new(NinjaSkillResultCode.InvalidState, actorUid, attack.TargetUid, attack.SkillId,
                attack.SkillLevel, 0, 0, 0, false, null, false, "ninja-feature-disabled");
        NinjaCombatant actor = await persistence.LoadNinjaCombatantAsync(actorIdentity, cancellationToken)
            ?? throw new InvalidDataException("The authenticated Ninja combat state is unavailable.");
        if (!NativeNinjaSkillCatalog5165.TryGet(attack.SkillId, out NinjaSkillDefinition definition))
            return new(NinjaSkillResultCode.SkillNotLearned, actorUid, attack.TargetUid, attack.SkillId,
                attack.SkillLevel, actor.Stamina, 0, 0, false, null, false, "unsupported-skill");
        if (!actor.LearnedSkills.TryGetValue((NinjaSkillId)attack.SkillId, out byte authoritativeLevel))
            return new(NinjaSkillResultCode.SkillNotLearned, actorUid, attack.TargetUid, attack.SkillId,
                0, actor.Stamina, 0, 0, false, null, false, "skill-not-learned");
        attack = attack with { SkillLevel = authoritativeLevel };
        NinjaCombatant? target = null;
        uint targetUid = attack.TargetUid;
        if (definition.TargetMode == NinjaTargetMode.Self)
            targetUid = actorUid;
        else if (registry.TryResolve(targetUid, out LiveCombatEntity? targetEntity))
        {
            LiveCombatEntitySnapshot targetSnapshot = targetEntity.Snapshot();
            if (targetSnapshot.Kind == LiveEntityKind.Player && targetSnapshot.Owner is GameplayActor owner)
                target = await persistence.LoadNinjaCombatantAsync(owner, cancellationToken);
            else if (targetSnapshot.Kind == LiveEntityKind.Monster &&
                monsters.TryGetValue(targetUid, out OwnerMonsterRuntimeDefinition? monster))
                target = new NinjaCombatant(new CharacterUid(targetUid), 0, checked((byte)monster.Template.Level),
                    new WorldPoint(targetSnapshot.Map.MapId, targetSnapshot.X, targetSnapshot.Y),
                    targetSnapshot.Alive, 0, null, null, new Dictionary<NinjaSkillId, byte>(),
                    IsMonster: true);
            if (target is not null)
                target = target with { IsFlying = targetSnapshot.IsFlying };
        }
        var request = new NinjaLiveCombatRequest(attack.ClientTimestamp, actorIdentity, targetUid,
            attack.X, attack.Y, attack.SkillId, attack.SkillLevel, MonotonicMilliseconds(), actor, target);
        MonsterAgent? rewardAgent = monsterAgents.GetValueOrDefault(targetUid);
        NinjaLiveCombatResult result = await pipeline.ExecuteNinjaAsync(request, cancellationToken,
            rewardAgent is null ? null : mutation => PlanMonsterReward(rewardAgent,
                actorIdentity, mutation),
            rewardAgent is null ? null : mutation => PlanCombatProgression(rewardAgent,
                actorIdentity, mutation.Damage, awardWeaponProficiency: false));
        if (result.Code == NinjaSkillResultCode.Applied)
        {
            if (result.Progression is not null)
                await SynchronizeCombatProgressionAsync(actorIdentity, result.Progression,
                    cancellationToken);
            activeStamina.Resource.Synchronize(result.RemainingStamina,
                StaminaActivity.Attacking, MonotonicMilliseconds());
            if (attack.SkillId == (ushort)NinjaSkillId.ArcherBane &&
                !await registry.SetFlyingAsync(targetUid, false, MonotonicMilliseconds(), cancellationToken))
                throw new InvalidDataException("An accepted Archer Bane target lost its authoritative flying state.");
            ushort x = checked((ushort)actor.Position.X);
            ushort y = checked((ushort)actor.Position.Y);
            if (definition.TargetMode != NinjaTargetMode.Self &&
                registry.TryResolve(targetUid, out LiveCombatEntity? responseTarget))
            {
                LiveCombatEntitySnapshot responseSnapshot = responseTarget.Snapshot();
                x = responseSnapshot.X;
                y = responseSnapshot.Y;
            }
            IReadOnlyList<SkillTarget5165> targets = definition.TargetMode == NinjaTargetMode.Self
                ? Array.Empty<SkillTarget5165>()
                : [new SkillTarget5165(targetUid, checked((uint)result.Damage))];
            await BroadcastAsync(actorUid, targetUid, NinjaPackets5165.EncodeSkillUse(actorUid, x, y,
                attack.SkillId, attack.SkillLevel, targets), cancellationToken);
            await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(actorUid, 8,
                result.RemainingStamina), cancellationToken);
            PersistedSkillState? skill = await persistence.ReadSkillAsync(actorIdentity, attack.SkillId,
                cancellationToken);
            if (skill.HasValue)
                await session.SendAsync(NinjaPackets5165.EncodeSkillInformation(skill.Value.Experience,
                    skill.Value.SkillId, skill.Value.Level), cancellationToken);
            if (result.Killed)
            {
                if (rewardAgent is not null)
                {
                    rewardAgent.DiedMilliseconds = MonotonicMilliseconds();
                    if (result.Reward is MonsterRewardCommitResult reward)
                        await BroadcastMonsterRewardAsync(rewardAgent, actorIdentity, reward,
                            cancellationToken);
                }
                if (registry.TryResolve(targetUid, out LiveCombatEntity? killedTarget) &&
                    killedTarget.Snapshot().Kind == LiveEntityKind.Player)
                    pipeline.ClearNinjaTransientState(new CharacterUid(targetUid));
                await BroadcastAsync(actorUid, targetUid, NinjaPackets5165.EncodeKillResult(actorUid,
                    targetUid, x, y, attack.ClientTimestamp), cancellationToken);
            }
        }
        log.Audit(result.Succeeded ? "Ninja1022Accepted" : "Ninja1022Rejected",
            $"A native Ninja skill completed safely (actor={actorUid}; target={targetUid}; skill={attack.SkillId}; level={attack.SkillLevel}; category={result.SafeCategory}; damage={result.Damage}; killed={result.Killed}; replayed={result.Replayed}).");
        return result;
    }

    private async Task SynchronizeMonstersAsync(GameplaySession5165 session,
        CancellationToken cancellationToken)
    {
        uint playerUid = checked((uint)session.Character.LegacyUid);
        HashSet<uint> visible = visibleMonsters.GetOrAdd(playerUid, _ => []);
        LiveCombatEntitySnapshot[] desired = registry.Nearby(
                new CombatMapKey(session.Character.MapId, 0), checked((ushort)session.Character.X),
                checked((ushort)session.Character.Y), GameplayWorld5165.ViewRadius, playerUid)
            .Where(entity => entity.Kind == LiveEntityKind.Monster && entity.Alive)
            .ToArray();
        HashSet<uint> desiredIds = desired.Select(entity => entity.LegacyUid).ToHashSet();
        uint[] remove;
        lock (visible)
        {
            remove = visible.Where(uid => !desiredIds.Contains(uid)).ToArray();
            foreach (uint uid in remove) visible.Remove(uid);
        }
        foreach (uint uid in remove)
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(uid), cancellationToken);
        foreach (LiveCombatEntitySnapshot entity in desired)
        {
            bool added;
            lock (visible) added = visible.Add(entity.LegacyUid);
            if (!added) continue;
            if (!monsters.TryGetValue(entity.LegacyUid, out OwnerMonsterRuntimeDefinition? definition))
                throw new InvalidDataException("A live monster has no reviewed spawn definition.");
            await session.SendAsync(CombatPackets5165.EncodeMonsterSpawn(definition.Template.Mesh, entity.LegacyUid,
                checked((ushort)Math.Min(ushort.MaxValue, entity.CurrentHealth)), definition.Template.Level,
                entity.X, entity.Y, definition.Direction, 100, definition.Template.Name), cancellationToken);
        }
    }

    private async Task SynchronizeGroundDropsAsync(GameplaySession5165 session,
        CancellationToken cancellationToken)
    {
        uint playerUid = checked((uint)session.Character.LegacyUid);
        HashSet<uint> visible = visibleGroundDrops.GetOrAdd(playerUid, _ => []);
        var map = new CombatMapKey(session.Character.MapId, 0);
        GroundDropSnapshot[] desired = groundDrops.Values.Where(drop => drop.Map == map &&
                Math.Max(Math.Abs(drop.X - session.Character.X), Math.Abs(drop.Y - session.Character.Y)) <=
                    GameplayWorld5165.ViewRadius)
            .OrderBy(drop => drop.GroundUid).ToArray();
        HashSet<uint> desiredIds = desired.Select(drop => drop.GroundUid).ToHashSet();
        uint[] remove;
        lock (visible)
        {
            remove = visible.Where(uid => !desiredIds.Contains(uid)).ToArray();
            foreach (uint uid in remove) visible.Remove(uid);
        }
        foreach (uint uid in remove)
            if (groundDrops.TryGetValue(uid, out GroundDropSnapshot? prior))
                await session.SendAsync(GroundItemPackets5165.Encode(prior.GroundUid, prior.ItemType,
                    prior.X, prior.Y, GroundItemPackets5165.RemoveAction), cancellationToken);
        foreach (GroundDropSnapshot drop in desired)
        {
            bool added;
            lock (visible) added = visible.Add(drop.GroundUid);
            if (added)
                await session.SendAsync(GroundItemPackets5165.Encode(drop.GroundUid, drop.ItemType,
                    drop.X, drop.Y, GroundItemPackets5165.SpawnAction), cancellationToken);
        }
    }

    private async Task BroadcastGroundDropAsync(GroundDropSnapshot drop, ushort action,
        CancellationToken cancellationToken)
    {
        byte[] packet = GroundItemPackets5165.Encode(drop.GroundUid, drop.ItemType, drop.X, drop.Y, action);
        foreach (LiveCombatEntitySnapshot player in registry.Nearby(drop.Map, drop.X, drop.Y,
                     GameplayWorld5165.ViewRadius).Where(value => value.Kind == LiveEntityKind.Player))
        {
            if (!sessions.TryGetValue(player.LegacyUid, out GameplaySession5165? recipient)) continue;
            HashSet<uint> visible = visibleGroundDrops.GetOrAdd(player.LegacyUid, _ => []);
            lock (visible)
            {
                if (action == GroundItemPackets5165.SpawnAction) visible.Add(drop.GroundUid);
                else visible.Remove(drop.GroundUid);
            }
            await recipient.SendAsync(packet.ToArray(), cancellationToken);
        }
        Array.Clear(packet);
    }

    private async Task BroadcastAsync(uint attackerUid, uint targetUid, byte[] packet,
        CancellationToken cancellationToken)
    {
        if (!registry.TryResolve(targetUid, out LiveCombatEntity? target)) return;
        LiveCombatEntitySnapshot snapshot = target.Snapshot();
        HashSet<uint> recipients = registry.Nearby(snapshot.Map, snapshot.X, snapshot.Y,
                GameplayWorld5165.ViewRadius)
            .Where(entity => entity.Kind == LiveEntityKind.Player)
            .Select(entity => entity.LegacyUid)
            .ToHashSet();
        recipients.Add(attackerUid);
        if (snapshot.Kind == LiveEntityKind.Player) recipients.Add(targetUid);
        foreach (uint uid in recipients.Order())
        {
            if (!sessions.TryGetValue(uid, out GameplaySession5165? recipient)) continue;
            await recipient.SendAsync(packet.ToArray(), cancellationToken);
        }
        Array.Clear(packet);
    }

    private static long MonotonicMilliseconds() =>
        checked((long)(Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency)));

    public static (ushort X, ushort Y) ResolveSpawnCell(IMapValidation5165 map,
        OwnerMonsterGenerator generator, int ordinal,
        IReadOnlySet<(int MapId, ushort X, ushort Y)>? occupied)
    {
        int width = checked(generator.MaximumX - generator.MinimumX + 1);
        int height = checked(generator.MaximumY - generator.MinimumY + 1);
        long cells = checked((long)width * height);
        int seed = checked((int)((generator.GeneratorId * 1103515245L + ordinal * 12345L) & 0x7fffffff));
        int attempts = checked((int)Math.Min(cells, 4_096));
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            long offset = (seed + attempt * 104729L) % cells;
            int x = checked(generator.MinimumX + (int)(offset % width));
            int y = checked(generator.MinimumY + (int)(offset / width));
            var candidate = (generator.MapId, checked((ushort)x), checked((ushort)y));
            if (map.CanStand(x, y) && (occupied is null || !occupied.Contains(candidate)))
                return (candidate.Item2, candidate.Item3);
        }
        // Several reviewed owner generators intentionally request multiple entities from a
        // one-cell spawn region. Preserve distinct cells where possible, then permit only
        // DMap-accessible co-location; entity identity remains the stable unique UID.
        if (occupied is not null)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                long offset = (seed + attempt * 104729L) % cells;
                int x = checked(generator.MinimumX + (int)(offset % width));
                int y = checked(generator.MinimumY + (int)(offset / width));
                if (map.CanStand(x, y)) return (checked((ushort)x), checked((ushort)y));
            }
        }
        throw new InvalidDataException(
            $"Owner monster generator {generator.GeneratorId} (monster={generator.MonsterType}; map={generator.MapId}; " +
            $"region={generator.MinimumX},{generator.MinimumY}-{generator.MaximumX},{generator.MaximumY}; " +
            $"population={generator.Population}; ordinal={ordinal}) has no bounded accessible unique spawn cell.");
    }

    private async Task RunStaminaSchedulerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                long now = MonotonicMilliseconds();
                foreach (GroundDropSnapshot expired in await groundPersistence.ExpireDueAsync(cancellationToken))
                {
                    Interlocked.Increment(ref expiredGroundItems);
                    groundDrops.TryRemove(expired.GroundUid, out _);
                    await BroadcastGroundDropAsync(expired, GroundItemPackets5165.RemoveAction,
                        cancellationToken);
                }
                foreach ((uint uid, ActiveStamina active) in stamina.ToArray())
                {
                    await active.MutationGate.WaitAsync(cancellationToken);
                    try
                    {
                    StaminaSnapshot before = active.Resource.Snapshot();
                    // A combat mutation may have advanced this resource while the scheduler
                    // waited for its gate. Sample the monotonic clock only after ownership.
                    StaminaTickResult tick = active.Resource.Regenerate(MonotonicMilliseconds());
                    if (!tick.Changed) continue;
                    try
                    {
                        if (!await persistence.TryUpdateStaminaAsync(active.Actor, before.Current,
                                tick.Snapshot.Current, cancellationToken))
                        {
                            stamina.TryRemove(new KeyValuePair<uint, ActiveStamina>(uid, active));
                            log.Error("GameServer", "StaminaPersistenceConflict",
                                "An authenticated Stamina state conflicted and was removed from scheduling.");
                            continue;
                        }
                        await active.Session.SendAsync(ItemEconomyPackets5165.EncodeStatus(uid, 8,
                            tick.Snapshot.Current), cancellationToken);
                    }
                    catch (Exception error) when (error is IOException or OperationCanceledException)
                    {
                        stamina.TryRemove(new KeyValuePair<uint, ActiveStamina>(uid, active));
                    }
                    }
                    finally { active.MutationGate.Release(); }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task RunMonsterSchedulerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        long expected = MonotonicMilliseconds() + 100;
        long nextMetrics = expected + 60_000;
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                long now = MonotonicMilliseconds();
                long delay = Math.Max(0, now - expected);
                InterlockedExtensions.Max(ref maximumSchedulerDelayMilliseconds, delay);
                expected = now + 100;
                HashSet<int> activeMaps = sessions.Values.Select(value => value.Character.MapId).ToHashSet();
                foreach (int mapId in activeMaps.Order())
                {
                    if (!monsterAgentsByMap.TryGetValue(mapId, out MonsterAgent[]? agents)) continue;
                    foreach (MonsterAgent agent in agents)
                    {
                        if (Interlocked.CompareExchange(ref agent.TickClaim, 1, 0) != 0) continue;
                        try
                        {
                            Interlocked.Increment(ref monsterAiTicks);
                            await TickMonsterAsync(agent, now, cancellationToken);
                        }
                        finally { Volatile.Write(ref agent.TickClaim, 0); }
                    }
                }
                if (now >= nextMetrics)
                {
                    log.Audit("MonsterRuntimeMetrics",
                        $"The bounded owner-monster scheduler is healthy (active={monsterAgents.Count}; activeMaps={activeMaps.Count}; ticks={Interlocked.Read(ref monsterAiTicks)}; attacks={Interlocked.Read(ref monsterAttacks)}; deaths={Interlocked.Read(ref monsterDeaths)}; respawns={Interlocked.Read(ref monsterRespawns)}; rewards={Interlocked.Read(ref rewardTransactions)}; pickups={Interlocked.Read(ref successfulPickups)}; ground={groundDrops.Count}; expired={Interlocked.Read(ref expiredGroundItems)}; maximumDelayMs={Interlocked.Read(ref maximumSchedulerDelayMilliseconds)})." );
                    nextMetrics = now + 60_000;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task TickMonsterAsync(MonsterAgent agent, long now, CancellationToken cancellationToken)
    {
        uint uid = agent.Definition.LegacyUid;
        if (!registry.TryResolve(uid, out LiveCombatEntity? entity)) return;
        LiveCombatEntitySnapshot monster = entity.Snapshot();
        if (!monster.Alive)
        {
            if (agent.DiedMilliseconds < 0) agent.DiedMilliseconds = now;
            if (!agent.CorpseRemoved && now - agent.DiedMilliseconds >= 2_000)
            {
                agent.CorpseRemoved = true;
                await RemoveMonsterFromScreensAsync(uid, cancellationToken);
            }
            long delay = checked(agent.Definition.Template.RespawnDelayMilliseconds +
                8_000 + uid % 12_001);
            if (delay > 0 && now - agent.DiedMilliseconds >= delay)
            {
                IMapValidation5165 map = maps.TryGet(agent.Definition.Generator.MapId, out IMapValidation5165? found)
                    ? found : throw new InvalidDataException("A live monster lost its reviewed DMap.");
                (ushort x, ushort y) = ResolveSpawnCell(map, agent.Definition.Generator,
                    checked((int)(uid % 10_000 + agent.Sequence)), null);
                await registry.RelocateAsync(uid, monster.Map, x, y, now, cancellationToken);
                LiveCombatEntitySnapshot relocated = entity.Snapshot();
                if (!await registry.ReviveAsync(uid, relocated.Version,
                        agent.Definition.Template.MaximumHealth, now, cancellationToken))
                    throw new InvalidDataException("A due owner monster could not respawn atomically.");
                agent.DiedMilliseconds = -1;
                agent.CorpseRemoved = false;
                agent.NextMoveMilliseconds = now;
                agent.NextAttackMilliseconds = now;
                agent.SpawnGeneration++;
                Interlocked.Increment(ref monsterRespawns);
                await SynchronizeMapPlayersAsync(monster.Map.MapId, cancellationToken);
            }
            return;
        }

        if (agent.Definition.Template.Behavior is OwnerMonsterBehavior.Passive or
            OwnerMonsterBehavior.HuntMonsters)
        {
            await PatrolAsync(agent, monster, now, cancellationToken);
            return;
        }
        LiveCombatEntitySnapshot? target = registry.Nearby(monster.Map, monster.X, monster.Y, 14, uid)
            .Where(value => value.Kind == LiveEntityKind.Player && value.Alive && value.Attackable &&
                MonsterMayTargetPlayer(agent.Definition.Template.Behavior, value.LegacyUid))
            .OrderBy(value => Math.Max(Math.Abs(value.X - monster.X), Math.Abs(value.Y - monster.Y)))
            .ThenBy(value => value.LegacyUid)
            .FirstOrDefault();
        if (target is null)
        {
            await PatrolAsync(agent, monster, now, cancellationToken);
            return;
        }
        int distance = Math.Max(Math.Abs(target.X - monster.X), Math.Abs(target.Y - monster.Y));
        if (distance > agent.Definition.Template.AttackDistance)
        {
            if (now >= agent.NextMoveMilliseconds)
                await MoveTowardAsync(agent, monster, target.X, target.Y, now, cancellationToken);
            return;
        }
        if (now < agent.NextAttackMilliseconds || target.Owner is not GameplayActor targetOwner) return;
        PlayerCombatStatistics? stats = playerStatistics.TryGetValue(target.LegacyUid,
            out PlayerCombatStatistics? foundStats) ? foundStats : null;
        if (stats is null || !stamina.TryGetValue(target.LegacyUid, out ActiveStamina? targetStamina)) return;
        uint sequence = unchecked(++agent.Sequence);
        Guid operation = MonsterOperationId(uid, target.LegacyUid, sequence);
        await targetStamina.MutationGate.WaitAsync(cancellationToken);
        LiveCombatResult result;
        try
        {
            StaminaSnapshot staminaBefore = targetStamina.Resource.Snapshot();
            result = await pipeline.ExecuteMonsterAttackAsync(new MonsterCombatRequest(
                operation, uid, targetOwner, agent.Definition.Template, stats, now,
                staminaBefore.Current, staminaBefore.Activity == StaminaActivity.Sitting), cancellationToken);
            if (result.Code == LiveCombatResultCode.Applied && result.StaminaAfter is ushort staminaAfter)
                targetStamina.Resource.Synchronize(staminaAfter,
                    result.Killed ? StaminaActivity.Dead : result.SittingInterrupted
                        ? StaminaActivity.Standing : staminaBefore.Activity, now);
        }
        finally { targetStamina.MutationGate.Release(); }
        int attackDelay = Math.Clamp(agent.Definition.Template.MoveDelayMilliseconds, 300, 60_000);
        agent.NextAttackMilliseconds = checked(now + attackDelay);
        if (result.Code != LiveCombatResultCode.Applied) return;
        Interlocked.Increment(ref monsterAttacks);
        if (agent.Definition.Template.AttackType == OwnerMonsterAttackType.Magic)
            await BroadcastAsync(uid, target.LegacyUid, NinjaPackets5165.EncodeSkillUse(uid,
                target.X, target.Y, agent.Definition.Template.MagicSkill,
                agent.Definition.Template.MagicLevel,
                [new SkillTarget5165(target.LegacyUid, checked((uint)result.Damage))]), cancellationToken);
        else
        {
            uint action = agent.Definition.Template.AttackType == OwnerMonsterAttackType.Ranged
                ? NinjaPackets5165.RangedAction : NinjaPackets5165.MeleeAction;
            await BroadcastAsync(uid, target.LegacyUid, NinjaPackets5165.EncodeNormalAttackResult(uid,
                target.LegacyUid, target.X, target.Y, action, checked((uint)result.Damage), sequence),
                cancellationToken);
        }
        if (result.SittingInterrupted && sessions.TryGetValue(target.LegacyUid,
                out GameplaySession5165? staminaSession))
        {
            await staminaSession.SendAsync(ItemEconomyPackets5165.EncodeStatus(target.LegacyUid, 8,
                result.StaminaAfter ?? 0), cancellationToken);
            await BroadcastAsync(uid, target.LegacyUid, GamePacketsAfterAuthentication5165.EncodeEntityAction(
                target.LegacyUid, target.X, target.Y, 100, sequence), cancellationToken);
        }
        if (result.Killed)
        {
            Interlocked.Increment(ref monsterDeaths);
            pipeline.ClearNinjaTransientState(targetOwner.CharacterUid);
            await BroadcastAsync(uid, target.LegacyUid, NinjaPackets5165.EncodeKillResult(uid,
                target.LegacyUid, target.X, target.Y, sequence), cancellationToken);
            if (sessions.TryGetValue(target.LegacyUid, out GameplaySession5165? targetSession))
                MarkStaminaActivity(targetSession, StaminaActivity.Dead);
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref long location, long value)
        {
            long current = Volatile.Read(ref location);
            while (value > current)
            {
                long observed = Interlocked.CompareExchange(ref location, value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }

    private async Task PatrolAsync(MonsterAgent agent, LiveCombatEntitySnapshot monster, long now,
        CancellationToken cancellationToken)
    {
        if (now < agent.NextMoveMilliseconds) return;
        uint sequence = unchecked(++agent.Sequence);
        int direction = checked((int)((agent.Definition.LegacyUid + sequence * 5u) % 8));
        (int dx, int dy) = DirectionDelta(direction);
        int x = monster.X + dx;
        int y = monster.Y + dy;
        OwnerMonsterGenerator generator = agent.Definition.Generator;
        if (x < generator.MinimumX || x > generator.MaximumX || y < generator.MinimumY || y > generator.MaximumY)
        {
            x = monster.X - dx;
            y = monster.Y - dy;
            direction = (direction + 4) % 8;
        }
        if (x < generator.MinimumX || x > generator.MaximumX || y < generator.MinimumY ||
            y > generator.MaximumY)
        {
            // A fixed/one-cell generator has no legal patrol step. Preserve its authoritative
            // position and bound future scheduler work instead of leaking outside its leash.
            agent.NextMoveMilliseconds = checked(now + Math.Clamp(
                agent.Definition.Template.MoveDelayMilliseconds, 500, 60_000));
            return;
        }
        await MoveMonsterAsync(agent, monster, x, y, direction, now, cancellationToken);
    }

    private async Task MoveTowardAsync(MonsterAgent agent, LiveCombatEntitySnapshot monster,
        ushort targetX, ushort targetY, long now, CancellationToken cancellationToken)
    {
        int dx = Math.Sign(targetX - monster.X);
        int dy = Math.Sign(targetY - monster.Y);
        int direction = DirectionFromDelta(dx, dy);
        await MoveMonsterAsync(agent, monster, monster.X + dx, monster.Y + dy, direction, now,
            cancellationToken);
    }

    private async Task MoveMonsterAsync(MonsterAgent agent, LiveCombatEntitySnapshot monster,
        int x, int y, int direction, long now, CancellationToken cancellationToken)
    {
        agent.NextMoveMilliseconds = checked(now + Math.Clamp(
            agent.Definition.Template.MoveDelayMilliseconds, 200, 60_000));
        if (!maps.TryGet(monster.Map.MapId, out IMapValidation5165? map) ||
            !map.CanMove(monster.X, monster.Y, x, y)) return;
        if (!await registry.RelocateAsync(monster.LegacyUid, monster.Map, checked((ushort)x),
                checked((ushort)y), now, cancellationToken)) return;
        byte[] movement = GamePacketsAfterAuthentication5165.EncodeMovement(checked((uint)direction),
            monster.LegacyUid, 0, unchecked(++agent.Sequence));
        await BroadcastFromEntityAsync(monster.LegacyUid, movement, cancellationToken);
        await SynchronizeMapPlayersAsync(monster.Map.MapId, cancellationToken);
    }

    private async Task BroadcastFromEntityAsync(uint uid, byte[] packet, CancellationToken cancellationToken)
    {
        if (!registry.TryResolve(uid, out LiveCombatEntity? entity)) return;
        LiveCombatEntitySnapshot source = entity.Snapshot();
        foreach (LiveCombatEntitySnapshot player in registry.Nearby(source.Map, source.X, source.Y,
                     GameplayWorld5165.ViewRadius, uid).Where(value => value.Kind == LiveEntityKind.Player))
            if (sessions.TryGetValue(player.LegacyUid, out GameplaySession5165? session))
                await session.SendAsync(packet.ToArray(), cancellationToken);
        Array.Clear(packet);
    }

    private async Task RemoveMonsterFromScreensAsync(uint uid, CancellationToken cancellationToken)
    {
        foreach ((uint playerUid, HashSet<uint> visible) in visibleMonsters)
        {
            bool removed;
            lock (visible) removed = visible.Remove(uid);
            if (removed && sessions.TryGetValue(playerUid, out GameplaySession5165? session))
                await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(uid),
                    cancellationToken);
        }
    }

    private async Task SynchronizeMapPlayersAsync(int mapId, CancellationToken cancellationToken)
    {
        foreach (GameplaySession5165 session in sessions.Values.Where(value => value.Character.MapId == mapId))
        {
            await SynchronizeMonstersAsync(session, cancellationToken);
            await SynchronizeGroundDropsAsync(session, cancellationToken);
        }
    }

    private static (int X, int Y) DirectionDelta(int direction) => (direction % 8) switch
    {
        0 => (0, 1), 1 => (-1, 1), 2 => (-1, 0), 3 => (-1, -1),
        4 => (0, -1), 5 => (1, -1), 6 => (1, 0), _ => (1, 1)
    };

    private bool MonsterMayTargetPlayer(OwnerMonsterBehavior behavior, uint playerUid)
    {
        if (behavior is OwnerMonsterBehavior.HuntPlayers or OwnerMonsterBehavior.HuntMonstersAndPlayers)
            return true;
        if (behavior is not (OwnerMonsterBehavior.HuntBlueNames or OwnerMonsterBehavior.HuntMonstersAndBlueNames))
            return false;
        return playerStatistics.TryGetValue(playerUid, out PlayerCombatStatistics? statistics) &&
            statistics.PkStatus == PlayerPkStatus.Flashing;
    }

    private static int DirectionFromDelta(int dx, int dy) => (dx, dy) switch
    {
        (0, 1) => 0, (-1, 1) => 1, (-1, 0) => 2, (-1, -1) => 3,
        (0, -1) => 4, (1, -1) => 5, (1, 0) => 6, _ => 7
    };

    private static Guid MonsterOperationId(uint monsterUid, uint targetUid, uint sequence)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, monsterUid);
        BitConverter.TryWriteBytes(bytes[4..], targetUid);
        BitConverter.TryWriteBytes(bytes[8..], sequence);
        BitConverter.TryWriteBytes(bytes[12..], 0x4d4f4e53u);
        return new Guid(bytes);
    }

    public async Task<IReadOnlyList<byte[]>> HandleGroundPickupAsync(AccountId accountId,
        GameplaySession5165 session, ReadOnlyMemory<byte> frame, ItemEconomySession5165 economy,
        CancellationToken token)
    {
        uint groundUid = GroundItemPackets5165.ParsePickupUid(frame.Span);
        uint characterUid = checked((uint)session.Character.LegacyUid);
        var actor = new GameplayActor(accountId, new CharacterUid(characterUid));
        GroundPickupResult result = await groundPersistence.PickupAsync(new GroundPickupRequest(
            GroundPickupOperationId(characterUid, groundUid), actor, groundUid,
            new CombatMapKey(session.Character.MapId, 0), checked((ushort)session.Character.X),
            checked((ushort)session.Character.Y), DateTimeOffset.UtcNow), token);
        if (!result.Succeeded || result.Drop is null && result.Code != GroundPickupCode.Replay)
            return [GamePacketsAfterAuthentication5165.EncodeTalk(2005, GroundPickupMessage(result.Code))];
        var packets = new List<byte[]>();
        if (result.Drop is GroundDropSnapshot drop)
        {
            if (result.Code == GroundPickupCode.Applied)
                Interlocked.Increment(ref successfulPickups);
            groundDrops.TryRemove(drop.GroundUid, out _);
            await BroadcastGroundDropAsync(drop, GroundItemPackets5165.RemoveAction, token);
        }
        if (result.LegacyItemUid is uint itemUid)
            packets.AddRange(await economy.CurrentItemPacketsAsync([itemUid], token));
        if (result.SilverBalance is long silver)
            packets.Add(ItemEconomyPackets5165.EncodeStatus(characterUid, 4, checked((ulong)silver)));
        return packets;
    }

    private MonsterRewardPlan? PlanMonsterReward(MonsterAgent agent, GameplayActor killer,
        CombatMutation combat)
        => PlanMonsterReward(agent, killer, combat.Map, combat.TargetX, combat.TargetY, combat.Damage);

    private MonsterRewardPlan? PlanMonsterReward(MonsterAgent agent, GameplayActor killer,
        NinjaCombatMutation combat)
        => PlanMonsterReward(agent, killer, combat.Map, combat.TargetX, combat.TargetY, combat.Damage);

    private MonsterRewardPlan? PlanMonsterReward(MonsterAgent agent, GameplayActor killer,
        CombatMapKey map, ushort targetX, ushort targetY, long damage)
    {
        OwnerMonsterRuntimeDefinition monster = agent.Definition;
        if (!monster.Template.GivesRewards || monster.Template.Classification is
            OwnerMonsterClassification.Guard or OwnerMonsterClassification.EventSpecial)
            return null;
        var deathPosition = new LiveCombatEntitySnapshot(monster.LegacyUid, LiveEntityKind.Monster,
            null, map, targetX, targetY, 0, monster.Template.MaximumHealth, false,
            1, MonotonicMilliseconds());
        Guid deathId = MonsterDeathId(monster.LegacyUid, agent.SpawnGeneration);
        var planned = new List<PlannedGroundDrop>();
        foreach (OwnerMonsterDropRule rule in dropRules.GetValueOrDefault(monster.Template.MonsterType, []))
        {
            if (!DeterministicChance(deathId, rule.Ordinal, rule.ChanceBasisPoints)) continue;
            if (planned.Count == 9) break; // Owner Source FindPlace exhausts the bounded 3x3 area.
            if (ResolveDropCell(monster, deathPosition, planned) is not { } cell) break;
            (ushort x, ushort y) = cell;
            planned.Add(new(GroundDropKind.Item, rule.ItemType, 0, rule.ItemSourceId,
                rule.Plus, rule.Sockets, rule.Bless, x, y));
        }
        foreach ((uint itemType, int chance, int ordinal) in new[]
                 {
                     (1_088_000u, 960, 30000), // Owner Source DragonBall 0.096%.
                     (1_088_001u, 7_800, 30001) // Owner Source Meteor 0.78%.
                 })
        {
            if (planned.Count == 9 || !DeterministicChance(deathId, ordinal, chance)) continue;
            if (!commonDropItems.TryGetValue(itemType, out OwnerCommonDropItem? common))
                throw new InvalidDataException("A reviewed Owner Source common-drop item disappeared.");
            if (ResolveDropCell(monster, deathPosition, planned) is not { } cell) break;
            (ushort x, ushort y) = cell;
            planned.Add(new(GroundDropKind.Item, itemType, 0, common.ItemSourceId, 0, 0, 0, x, y));
        }
        int[] noNormalEquipmentMaps = [1214,2020,1215,1210,1211,1212,1043,1044,1045,1046,1047,1048,1049,
            1351,1352,1353,1354,2021,2022,2023,2024,1051];
        if (!noNormalEquipmentMaps.Contains(monster.Generator.MapId) &&
            equipmentDropCandidates.TryGetValue(monster.Template.Level,
                out OwnerEquipmentDropCandidate[]? candidates) && candidates.Length > 0 &&
            DeterministicChance(deathId, 31000, 150000) && planned.Count < 9)
        {
            OwnerEquipmentDropCandidate item = candidates[DeterministicIndex(deathId, 31001, candidates.Length)];
            byte plus = DeterministicChance(deathId, 31002, 12000) ? (byte)1 : (byte)0;
            bool socketEligible = item.ItemType % 10 is 4 or 5;
            byte sockets = socketEligible && DeterministicChance(deathId, 31003, 60000) ? (byte)2 :
                socketEligible && DeterministicChance(deathId, 31004, 120000) ? (byte)1 : (byte)0;
            if (ResolveDropCell(monster, deathPosition, planned) is { } cell)
            {
                (ushort x, ushort y) = cell;
                planned.Add(new(GroundDropKind.Item, item.ItemType, 0, item.ItemSourceId,
                    plus, sockets, 0, x, y));
            }
        }
        if (monster.Template.MaximumSilver > 0 && DeterministicChance(deathId, 32000, 120000))
        {
            int roll = DeterministicIndex(deathId, 32001, 100) + 1;
            int quantity = roll <= 50 ? 1 : roll <= 75 ? 2 : roll <= 89 ? 3 : roll <= 96 ? 4 :
                roll <= 99 ? 5 : 6;
            for (int ordinal = 0; ordinal < quantity && planned.Count < 9; ordinal++)
            {
                long silver = DeterministicRange(deathId, monster.Template.MinimumSilver,
                    monster.Template.MaximumSilver, 32002 + ordinal);
                silver = Math.Min(25_000, checked(silver * 11 / 10));
                if (monster.Generator.MapId is 1351 or 1352 or 1353 or 1354) silver /= 2;
                if (silver <= 0) continue;
                if (ResolveDropCell(monster, deathPosition, planned) is not { } cell) break;
                (ushort x, ushort y) = cell;
                planned.Add(new(GroundDropKind.Silver, SilverDisplayItem(silver), silver, null,
                    0, 0, 0, x, y));
            }
        }
        long experience = monster.Template.MonsterType == 150 ? 0 : monster.Template.MaximumHealth / 10;
        return new MonsterRewardPlan(
            deathId, agent.SpawnGeneration, killer, monster.LegacyUid, monster.Template.MonsterType,
            deathPosition.Map, deathPosition.X, deathPosition.Y,
            experience, 0, planned);
    }

    private CombatProgressionPlan PlanCombatProgression(MonsterAgent agent, GameplayActor attacker,
        long damage, bool awardWeaponProficiency)
    {
        byte level = playerStatistics.TryGetValue(attacker.CharacterUid.Value,
            out PlayerCombatStatistics? stats) ? stats.Level : (byte)1;
        long experience = OwnerSourceCombatFormula.ExperienceForDamage(damage, level,
            agent.Definition.Template.Level);
        long proficiency = awardWeaponProficiency ? damage : 0;
        if (agent.Definition.Template.MonsterType == 150)
            proficiency = checked(proficiency * 3);
        return new CombatProgressionPlan(experience, proficiency).Validate();
    }

    private async Task SynchronizeCombatProgressionAsync(GameplayActor actor,
        CombatProgressionCommitResult result, CancellationToken token)
    {
        if (!sessions.TryGetValue(actor.CharacterUid.Value, out GameplaySession5165? session)) return;
        await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 5,
            checked((ulong)result.ExperienceAfter)), token);
        byte priorLevel = playerStatistics.TryGetValue(actor.CharacterUid.Value,
            out PlayerCombatStatistics? before) ? before.Level : result.LevelAfter;
        PlayerCombatStatistics refreshed = await persistence.LoadPlayerCombatStatisticsAsync(actor, token)
            ?? throw new InvalidDataException("A progressed player lost its authoritative statistics.");
        playerStatistics[actor.CharacterUid.Value] = refreshed;
        if (result.LevelAfter != priorLevel)
        {
            await session.SendAsync(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 12,
                result.LevelAfter), token);
            await BroadcastAsync(actor.CharacterUid.Value, actor.CharacterUid.Value,
                GamePacketsAfterAuthentication5165.EncodeEntityAction(actor.CharacterUid.Value,
                    checked((ushort)session.Character.X), checked((ushort)session.Character.Y), 92), token);
        }
        foreach (WeaponProficiencyCommitResult proficiency in result.Proficiencies)
            await session.SendAsync(CombatPackets5165.EncodeProficiency(proficiency.ProficiencyType,
                proficiency.LevelAfter, proficiency.ExperienceAfter), token);
    }

    private async Task BroadcastMonsterRewardAsync(MonsterAgent agent, GameplayActor killer,
        MonsterRewardCommitResult result, CancellationToken token)
    {
        if (result.Applied) Interlocked.Increment(ref rewardTransactions);
        OwnerMonsterRuntimeDefinition monster = agent.Definition;
        foreach (GroundDropSnapshot drop in result.Drops)
        {
            groundDrops[drop.GroundUid] = drop;
            await BroadcastGroundDropAsync(drop, GroundItemPackets5165.SpawnAction, token);
        }
    }

    private (ushort X, ushort Y)? ResolveDropCell(OwnerMonsterRuntimeDefinition monster,
        LiveCombatEntitySnapshot deathPosition, IReadOnlyCollection<PlannedGroundDrop> planned)
    {
        if (!maps.TryGet(monster.Generator.MapId, out IMapValidation5165? map))
            throw new InvalidDataException("A reward lost its reviewed DMap.");
        (int dx, int dy)[] offsets = [(0,0),(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,-1),(-1,1),(1,1)];
        int ordinal = planned.Count;
        for (int index = 0; index < offsets.Length; index++)
        {
            (int dx, int dy) = offsets[(index + ordinal) % offsets.Length];
            int x = deathPosition.X + dx, y = deathPosition.Y + dy;
            if (!map.CanStand(x, y)) continue;
            ushort candidateX = checked((ushort)x), candidateY = checked((ushort)y);
            if (planned.Any(drop => drop.X == candidateX && drop.Y == candidateY)) continue;
            if (groundDrops.Values.Any(drop =>
                drop.Map.MapId == monster.Generator.MapId && drop.Map.InstanceId == 0 &&
                drop.X == candidateX && drop.Y == candidateY)) continue;
            return (candidateX, candidateY);
        }
        // Owner Source FindPlace is bounded. Exhaustion omits the unplaceable remainder rather
        // than invalidating an already authoritative monster death and its other rewards.
        return null;
    }

    private static bool DeterministicChance(Guid death, int ordinal, int basisPoints)
    {
        Span<byte> bytes = stackalloc byte[20]; death.TryWriteBytes(bytes);
        BitConverter.TryWriteBytes(bytes[16..], ordinal);
        uint value = BitConverter.ToUInt32(SHA256.HashData(bytes), 0) % 1_000_000;
        return value < basisPoints;
    }

    private static int DeterministicRange(Guid death, int minimum, int maximum, int ordinal)
    {
        if (maximum <= minimum) return minimum;
        Span<byte> bytes = stackalloc byte[20]; death.TryWriteBytes(bytes);
        BitConverter.TryWriteBytes(bytes[16..], ordinal);
        return minimum + checked((int)(BitConverter.ToUInt32(SHA256.HashData(bytes), 0) %
            checked((uint)(maximum - minimum + 1))));
    }

    private static int DeterministicIndex(Guid death, int ordinal, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        return DeterministicRange(death, 0, count - 1, ordinal);
    }

    private static uint SilverDisplayItem(long value) => value switch
    {
        < 10 => 1090000, < 100 => 1090010, < 1000 => 1090020,
        < 3000 => 1091000, < 10000 => 1091010, _ => 1091020
    };

    private static Guid MonsterDeathId(uint uid, long generation)
    {
        Span<byte> value = stackalloc byte[16]; BitConverter.TryWriteBytes(value, uid);
        BitConverter.TryWriteBytes(value[4..], generation); BitConverter.TryWriteBytes(value[12..], 0x4454484du);
        return new Guid(value);
    }

    private static Guid GroundPickupOperationId(uint characterUid, uint groundUid)
    {
        Span<byte> value = stackalloc byte[16]; BitConverter.TryWriteBytes(value, characterUid);
        BitConverter.TryWriteBytes(value[4..], groundUid); BitConverter.TryWriteBytes(value[8..], 0x5049434bu);
        BitConverter.TryWriteBytes(value[12..], 0x55503131u); return new Guid(value);
    }

    private static string GroundPickupMessage(GroundPickupCode code) => code switch
    {
        GroundPickupCode.Protected => "You have to wait before picking up another player's drop.",
        GroundPickupCode.InventoryFull => "Your inventory is full.",
        _ => "That ground item is no longer available."
    };

    public async ValueTask DisposeAsync()
    {
        await schedulerLifetime.CancelAsync();
        if (staminaScheduler is not null)
        {
            try { await staminaScheduler; }
            catch (OperationCanceledException) { }
        }
        if (monsterScheduler is not null)
        {
            try { await monsterScheduler; }
            catch (OperationCanceledException) { }
        }
        schedulerLifetime.Dispose();
        await persistence.DisposeAsync();
        await groundPersistence.DisposeAsync();
    }
}
