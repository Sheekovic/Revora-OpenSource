using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

public sealed class LiveCombatEntity
{
    private readonly object stateGate = new();
    internal SemaphoreSlim MutationGate { get; } = new(1, 1);
    private LiveCombatEntitySnapshot snapshot;

    public LiveCombatEntity(LiveCombatEntitySnapshot initial) => snapshot = initial.Validate();

    public LiveCombatEntitySnapshot Snapshot()
    {
        lock (stateGate) return snapshot;
    }

    internal bool TryCommit(long expectedVersion, long health, bool attackable, long touchedAt,
        long? expectedPersistenceVersion = null)
    {
        lock (stateGate)
        {
            if (snapshot.Version != expectedVersion || expectedPersistenceVersion.HasValue &&
                snapshot.PersistenceVersion != expectedPersistenceVersion.Value) return false;
            if (health < 0 || health > snapshot.MaximumHealth) throw new ArgumentOutOfRangeException(nameof(health));
            snapshot = snapshot with
            {
                CurrentHealth = health,
                Attackable = attackable,
                Version = checked(snapshot.Version + 1),
                PersistenceVersion = expectedPersistenceVersion.HasValue
                    ? checked(snapshot.PersistenceVersion + 1) : snapshot.PersistenceVersion,
                LastTouchedMonotonicMilliseconds = touchedAt
            };
            return true;
        }
    }

    internal void Relocate(CombatMapKey map, ushort x, ushort y, long touchedAt)
    {
        map.Validate();
        lock (stateGate)
        {
            bool mapChanged = snapshot.Map != map;
            snapshot = snapshot with
            {
                Map = map,
                X = x,
                Y = y,
                IsFlying = mapChanged ? false : snapshot.IsFlying,
                Version = checked(snapshot.Version + 1),
                LastTouchedMonotonicMilliseconds = touchedAt
            };
        }
    }

    internal bool SetFlying(long expectedVersion, bool isFlying, long touchedAt)
    {
        lock (stateGate)
        {
            if (snapshot.Version != expectedVersion || snapshot.Kind != LiveEntityKind.Player) return false;
            snapshot = snapshot with
            {
                IsFlying = isFlying,
                LastTouchedMonotonicMilliseconds = touchedAt
            };
            return true;
        }
    }

    internal bool Revive(long expectedVersion, long health, long touchedAt)
    {
        if (health <= 0) throw new ArgumentOutOfRangeException(nameof(health));
        return TryCommit(expectedVersion, health, true, touchedAt);
    }
}

public sealed class LiveCombatEntityRegistry
{
    private readonly ConcurrentDictionary<uint, LiveCombatEntity> entities = new();
    private readonly ConcurrentDictionary<CombatMapKey, ConcurrentDictionary<uint, LiveCombatEntity>> byMap = new();
    private readonly int maximumEntities;

    public LiveCombatEntityRegistry(int maximumEntities = 50_000)
    {
        if (maximumEntities is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(maximumEntities));
        this.maximumEntities = maximumEntities;
    }

    public int Count => entities.Count;

    public bool TryRegister(LiveCombatEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        LiveCombatEntitySnapshot snapshot = entity.Snapshot();
        if (entities.Count >= maximumEntities) return false;
        if (!entities.TryAdd(snapshot.LegacyUid, entity)) return false;
        ConcurrentDictionary<uint, LiveCombatEntity> map = byMap.GetOrAdd(snapshot.Map, _ => new());
        if (map.TryAdd(snapshot.LegacyUid, entity)) return true;
        entities.TryRemove(new KeyValuePair<uint, LiveCombatEntity>(snapshot.LegacyUid, entity));
        return false;
    }

    public bool TryResolve(uint uid, out LiveCombatEntity entity) => entities.TryGetValue(uid, out entity!);

    public async ValueTask<bool> RemoveAsync(uint uid, LiveCombatEntity expected, CancellationToken cancellationToken)
    {
        await expected.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryRemove(new KeyValuePair<uint, LiveCombatEntity>(uid, expected))) return false;
            LiveCombatEntitySnapshot snapshot = expected.Snapshot();
            if (byMap.TryGetValue(snapshot.Map, out ConcurrentDictionary<uint, LiveCombatEntity>? map))
            {
                map.TryRemove(new KeyValuePair<uint, LiveCombatEntity>(uid, expected));
                if (map.IsEmpty) byMap.TryRemove(new KeyValuePair<CombatMapKey,
                    ConcurrentDictionary<uint, LiveCombatEntity>>(snapshot.Map, map));
            }
            return true;
        }
        finally { expected.MutationGate.Release(); }
    }

    public async ValueTask<bool> RelocateAsync(uint uid, CombatMapKey map, ushort x, ushort y, long touchedAt,
        CancellationToken cancellationToken)
    {
        if (!entities.TryGetValue(uid, out LiveCombatEntity? entity)) return false;
        await entity.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryGetValue(uid, out LiveCombatEntity? current) || !ReferenceEquals(current, entity)) return false;
            LiveCombatEntitySnapshot previous = entity.Snapshot();
            if (previous.Map != map)
            {
                ConcurrentDictionary<uint, LiveCombatEntity> destination = byMap.GetOrAdd(map, _ => new());
                if (!destination.TryAdd(uid, entity)) return false;
                if (byMap.TryGetValue(previous.Map, out ConcurrentDictionary<uint, LiveCombatEntity>? source))
                {
                    source.TryRemove(new KeyValuePair<uint, LiveCombatEntity>(uid, entity));
                    if (source.IsEmpty) byMap.TryRemove(new KeyValuePair<CombatMapKey,
                        ConcurrentDictionary<uint, LiveCombatEntity>>(previous.Map, source));
                }
            }
            entity.Relocate(map, x, y, touchedAt);
            return true;
        }
        finally { entity.MutationGate.Release(); }
    }

    public async ValueTask<bool> ReviveAsync(uint uid, long expectedVersion, long health, long touchedAt,
        CancellationToken cancellationToken)
    {
        if (!entities.TryGetValue(uid, out LiveCombatEntity? entity)) return false;
        await entity.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryGetValue(uid, out LiveCombatEntity? current) || !ReferenceEquals(current, entity))
                return false;
            return entity.Revive(expectedVersion, health, touchedAt);
        }
        finally { entity.MutationGate.Release(); }
    }

    public async ValueTask<bool> CommitDurableAsync(uint uid, long expectedVersion,
        long expectedPersistenceVersion, long resultingPersistenceVersion, long health,
        bool attackable, long touchedAt, CancellationToken cancellationToken)
    {
        if (resultingPersistenceVersion != checked(expectedPersistenceVersion + 1))
            throw new ArgumentOutOfRangeException(nameof(resultingPersistenceVersion));
        if (!entities.TryGetValue(uid, out LiveCombatEntity? entity)) return false;
        await entity.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryGetValue(uid, out LiveCombatEntity? current) || !ReferenceEquals(current, entity))
                return false;
            return entity.TryCommit(expectedVersion, health, attackable, touchedAt,
                expectedPersistenceVersion);
        }
        finally { entity.MutationGate.Release(); }
    }

    public async ValueTask<T> ExecuteDurableHealthMutationAsync<T>(uint uid,
        Func<LiveCombatEntitySnapshot, CancellationToken, ValueTask<DurableHealthMutation<T>>> mutation,
        long touchedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!entities.TryGetValue(uid, out LiveCombatEntity? entity))
            throw new InvalidOperationException("The authenticated live entity is unavailable.");
        await entity.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryGetValue(uid, out LiveCombatEntity? current) || !ReferenceEquals(current, entity))
                throw new InvalidOperationException("The authenticated live entity changed during mutation.");
            LiveCombatEntitySnapshot before = entity.Snapshot();
            DurableHealthMutation<T> durable = await mutation(before, cancellationToken);
            if (!durable.Changed) return durable.Result;
            if (durable.ResultingPersistenceVersion != checked(before.PersistenceVersion + 1) ||
                !entity.TryCommit(before.Version, durable.ResultingHealth,
                    durable.ResultingHealth > 0, touchedAt, before.PersistenceVersion))
                throw new InvalidOperationException("A committed durable health mutation lost its live version.");
            return durable.Result;
        }
        finally { entity.MutationGate.Release(); }
    }

    public async ValueTask<bool> SetFlyingAsync(uint uid, bool isFlying, long touchedAt,
        CancellationToken cancellationToken)
    {
        if (!entities.TryGetValue(uid, out LiveCombatEntity? entity)) return false;
        await entity.MutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!entities.TryGetValue(uid, out LiveCombatEntity? current) || !ReferenceEquals(current, entity))
                return false;
            LiveCombatEntitySnapshot snapshot = entity.Snapshot();
            return entity.SetFlying(snapshot.Version, isFlying, touchedAt);
        }
        finally { entity.MutationGate.Release(); }
    }

    public IReadOnlyList<LiveCombatEntitySnapshot> Nearby(CombatMapKey map, ushort x, ushort y, int radius, uint exceptUid = 0)
    {
        map.Validate();
        if (radius is < 0 or > 64) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!byMap.TryGetValue(map, out ConcurrentDictionary<uint, LiveCombatEntity>? indexed)) return [];
        return indexed.Values.Select(value => value.Snapshot())
            .Where(value => value.LegacyUid != exceptUid && value.Map == map &&
                Math.Max(Math.Abs(value.X - x), Math.Abs(value.Y - y)) <= radius)
            .OrderBy(value => value.LegacyUid)
            .ToArray();
    }

    public async ValueTask<int> RemoveAbandonedMonstersAsync(long olderThanMonotonicMilliseconds,
        CancellationToken cancellationToken)
    {
        int removed = 0;
        foreach ((uint uid, LiveCombatEntity entity) in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiveCombatEntitySnapshot snapshot = entity.Snapshot();
            if (snapshot.Kind != LiveEntityKind.Monster || snapshot.LastTouchedMonotonicMilliseconds >= olderThanMonotonicMilliseconds)
                continue;
            if (await RemoveAsync(uid, entity, cancellationToken)) removed++;
        }
        return removed;
    }
}

public sealed class ConservativeNormalDamageFormula : ILiveCombatDamageFormula
{
    private readonly long configuredDamage;

    public ConservativeNormalDamageFormula(long configuredDamage = 1)
    {
        if (configuredDamage is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(configuredDamage));
        this.configuredDamage = configuredDamage;
    }

    public long CalculateNormalDamage(LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot target,
        uint action, uint sequence)
    {
        if (action is not (2u or 28u)) throw new ArgumentOutOfRangeException(nameof(action));
        return Math.Min(target.CurrentHealth, configuredDamage);
    }
}

public sealed class OwnerSourceLiveDamageFormula(
    Func<uint, PlayerCombatStatistics?> playerStatistics,
    Func<uint, OwnerMonsterTemplate?> monsterTemplate) : ILiveCombatDamageFormula
{
    public long CalculateNormalDamage(LiveCombatEntitySnapshot attacker,
        LiveCombatEntitySnapshot target, uint action, uint sequence)
    {
        if (action is not (2u or 28u)) throw new ArgumentOutOfRangeException(nameof(action));
        PlayerCombatStatistics stats = playerStatistics(attacker.LegacyUid)
            ?? throw new InvalidOperationException("The authenticated attacker has no combat-stat snapshot.");
        if (target.Kind == LiveEntityKind.Monster)
        {
            OwnerMonsterTemplate template = monsterTemplate(target.LegacyUid)
                ?? throw new InvalidOperationException("The target monster has no owner-source template.");
            return Math.Min(target.CurrentHealth,
                OwnerSourceCombatFormula.PlayerPhysicalDamage(stats, template, sequence));
        }
        PlayerCombatStatistics targetStats = playerStatistics(target.LegacyUid)
            ?? throw new InvalidOperationException("The authenticated target has no combat-stat snapshot.");
        long rolled = stats.MinimumAttack == stats.MaximumAttack ? stats.MinimumAttack :
            stats.MinimumAttack + Math.Abs(attacker.Version.GetHashCode() ^ target.Version.GetHashCode()) %
            checked(stats.MaximumAttack - stats.MinimumAttack + 1);
        return Math.Min(target.CurrentHealth, Math.Max(1, rolled + stats.Strength - targetStats.Defense));
    }
}

public sealed class DenyByDefaultPvpPolicy(IReadOnlySet<int>? allowedMaps = null) : ILivePvpPolicy
{
    private readonly IReadOnlySet<int> allowed = allowedMaps ?? new HashSet<int>();
    public bool CanAttack(LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot target) =>
        attacker.Kind == LiveEntityKind.Player && target.Kind == LiveEntityKind.Player &&
        attacker.Map == target.Map && allowed.Contains(attacker.Map.MapId);
}

public sealed class LiveCombatPipeline
{
    private const int MaximumReceiptsPerAttacker = 256;
    private const int MaximumClientCoordinateClaimDrift = 18;
    private readonly LiveCombatEntityRegistry registry;
    private readonly ILiveCombatDamageFormula formula;
    private readonly ILivePvpPolicy pvp;
    private readonly ILiveCombatPersistence persistence;
    private readonly INinjaCombatPersistence? ninjaPersistence;
    private readonly IMonsterCombatPersistence? monsterPersistence;
    private readonly NinjaSkillEngine ninja = new();
    private readonly ConcurrentDictionary<uint, AttackerState> attackers = new();
    private readonly ConcurrentDictionary<Guid, LiveCombatResult> monsterAttackResults = new();
    private readonly Queue<Guid> monsterAttackOrder = new();
    private readonly object monsterAttackReceiptGate = new();
    private readonly int meleeRange;
    private readonly int rangedRange;
    private readonly long minimumIntervalMilliseconds;

    public LiveCombatPipeline(LiveCombatEntityRegistry registry, ILiveCombatDamageFormula formula,
        ILivePvpPolicy pvp, ILiveCombatPersistence persistence, int meleeRange = 3, int rangedRange = 18,
        long minimumIntervalMilliseconds = 500)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
        this.pvp = pvp ?? throw new ArgumentNullException(nameof(pvp));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        ninjaPersistence = persistence as INinjaCombatPersistence;
        monsterPersistence = persistence as IMonsterCombatPersistence;
        if (meleeRange is < 1 or > 8 || rangedRange is < 1 or > 64 || minimumIntervalMilliseconds is < 1 or > 60_000)
            throw new ArgumentOutOfRangeException(nameof(meleeRange));
        this.meleeRange = meleeRange;
        this.rangedRange = rangedRange;
        this.minimumIntervalMilliseconds = minimumIntervalMilliseconds;
    }

    public async ValueTask<LiveCombatResult> ExecuteMonsterAttackAsync(MonsterCombatRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OperationId == Guid.Empty || request.MonotonicMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (monsterAttackResults.TryGetValue(request.OperationId, out LiveCombatResult? cachedResult))
            return cachedResult with { Replayed = true };
        if (monsterPersistence is null)
            return new(LiveCombatResultCode.PersistenceFailure, request.MonsterUid,
                request.Target.CharacterUid.Value, SafeCategory: "monster-persistence-unavailable");
        if (!registry.TryResolve(request.MonsterUid, out LiveCombatEntity? attackerEntity))
            return new(LiveCombatResultCode.AttackerMissing, request.MonsterUid,
                request.Target.CharacterUid.Value, SafeCategory: "monster-attacker-missing");
        if (!registry.TryResolve(request.Target.CharacterUid.Value, out LiveCombatEntity? targetEntity))
            return new(LiveCombatResultCode.TargetMissing, request.MonsterUid,
                request.Target.CharacterUid.Value, SafeCategory: "monster-target-missing");
        LiveCombatEntity first = request.MonsterUid < request.Target.CharacterUid.Value
            ? attackerEntity : targetEntity;
        LiveCombatEntity second = ReferenceEquals(first, attackerEntity) ? targetEntity : attackerEntity;
        await first.MutationGate.WaitAsync(cancellationToken);
        await second.MutationGate.WaitAsync(cancellationToken);
        try
        {
            LiveCombatEntitySnapshot attacker = attackerEntity.Snapshot();
            LiveCombatEntitySnapshot target = targetEntity.Snapshot();
            if (attacker.Kind != LiveEntityKind.Monster || !attacker.Alive)
                return new(LiveCombatResultCode.AttackerDead, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-dead");
            if (target.Kind != LiveEntityKind.Player || target.Owner != request.Target)
                return new(LiveCombatResultCode.OwnershipMismatch, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-target-ownership");
            if (!target.Alive)
                return new(LiveCombatResultCode.TargetDead, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-target-dead");
            if (attacker.Map != target.Map)
                return new(LiveCombatResultCode.WrongMapOrInstance, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-map-instance");
            int distance = Math.Max(Math.Abs(attacker.X - target.X), Math.Abs(attacker.Y - target.Y));
            if (distance > request.Monster.AttackDistance)
                return new(LiveCombatResultCode.OutOfRange, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-range");
            long damage = Math.Min(target.CurrentHealth, OwnerSourceCombatFormula.MonsterDamage(
                request.Monster, request.TargetStatistics,
                unchecked((uint)request.OperationId.GetHashCode())));
            long remaining = checked(target.CurrentHealth - damage);
            bool sittingInterrupted = request.WasSitting;
            ushort? staminaAfter = request.PreviousStamina;
            if (request.WasSitting && request.PreviousStamina is ushort prior &&
                request.Monster.AttackType != OwnerMonsterAttackType.Magic)
                staminaAfter = prior > 30 ? checked((ushort)(prior - 30)) : (ushort)0;
            var mutation = new MonsterCombatMutation(request.OperationId, request.MonsterUid,
                request.Monster.MonsterType, request.Target, target.Map, target.PersistenceVersion,
                target.CurrentHealth, damage, remaining, remaining == 0, request.PreviousStamina,
                staminaAfter, sittingInterrupted);
            CombatPersistenceResult persisted;
            try { persisted = await monsterPersistence.CommitMonsterAttackAsync(mutation, cancellationToken); }
            catch
            {
                return new(LiveCombatResultCode.PersistenceFailure, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-persistence-failure");
            }
            if (persisted.Code == CombatPersistenceCode.Conflict)
                return new(LiveCombatResultCode.PersistenceConflict, request.MonsterUid,
                    target.LegacyUid, SafeCategory: "monster-persistence-conflict");
            if (persisted.Code == CombatPersistenceCode.Applied &&
                !targetEntity.TryCommit(target.Version, remaining, remaining > 0,
                    request.MonotonicMilliseconds, target.PersistenceVersion))
                throw new InvalidOperationException("A committed monster attack lost its live target version.");
            var result = new LiveCombatResult(LiveCombatResultCode.Applied, request.MonsterUid, target.LegacyUid,
                damage, remaining, remaining == 0, persisted.Code == CombatPersistenceCode.Replay,
                "monster-attack", StaminaAfter: persisted.TargetStaminaAfter ?? staminaAfter,
                SittingInterrupted: persisted.SittingInterrupted || sittingInterrupted);
            RecordMonsterAttack(request.OperationId, result);
            return result;
        }
        finally
        {
            second.MutationGate.Release();
            first.MutationGate.Release();
        }
    }

    public async ValueTask<LiveCombatResult> ExecuteNormalAsync(NormalCombatRequest request,
        CancellationToken cancellationToken, Func<CombatMutation, MonsterRewardPlan?>? rewardPlanner = null,
        Func<CombatMutation, CombatProgressionPlan?>? progressionPlanner = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.MonotonicMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Sequence == 0)
            return Failure(request, LiveCombatResultCode.InvalidSequence, "invalid-sequence");
        string fingerprint = Fingerprint(request);
        AttackerState attackerState = attackers.GetOrAdd(request.AuthenticatedActor.CharacterUid.Value, _ => new());
        if (attackerState.TryReplay(request.Sequence, fingerprint, out LiveCombatResult? replay)) return replay;

        if (!registry.TryResolve(request.AuthenticatedActor.CharacterUid.Value, out LiveCombatEntity? attackerEntity))
            return attackerState.Record(request.Sequence, fingerprint, Failure(request, LiveCombatResultCode.AttackerMissing, "attacker-missing"));
        LiveCombatEntitySnapshot attacker = attackerEntity.Snapshot();
        if (attacker.Kind != LiveEntityKind.Player || attacker.Owner != request.AuthenticatedActor)
            return attackerState.Record(request.Sequence, fingerprint, Failure(request, LiveCombatResultCode.OwnershipMismatch, "ownership-mismatch"));
        if (!attacker.Alive)
            return attackerState.Record(request.Sequence, fingerprint, Failure(request, LiveCombatResultCode.AttackerDead, "attacker-dead"));
        if (!registry.TryResolve(request.TargetUid, out LiveCombatEntity? targetEntity))
            return attackerState.Record(request.Sequence, fingerprint, Failure(request, LiveCombatResultCode.TargetMissing, "target-missing"));
        if (ReferenceEquals(attackerEntity, targetEntity))
            return attackerState.Record(request.Sequence, fingerprint,
                Failure(request, LiveCombatResultCode.PvpNotAllowed, "self-target"));

        LiveCombatEntity first = attacker.LegacyUid < targetEntity.Snapshot().LegacyUid ? attackerEntity : targetEntity;
        LiveCombatEntity second = ReferenceEquals(first, attackerEntity) ? targetEntity : attackerEntity;
        await first.MutationGate.WaitAsync(cancellationToken);
        await second.MutationGate.WaitAsync(cancellationToken);
        try
        {
            attacker = attackerEntity.Snapshot();
            LiveCombatEntitySnapshot target = targetEntity.Snapshot();
            LiveCombatResult? rejected = Validate(request, attacker, target);
            if (rejected is not null) return attackerState.Record(request.Sequence, fingerprint, rejected);
            if (!attackerState.TryAcquireFrequency(request.MonotonicMilliseconds, minimumIntervalMilliseconds))
                return attackerState.Record(request.Sequence, fingerprint,
                    Failure(request, LiveCombatResultCode.Cooldown, "attack-frequency", target.CurrentHealth));

            long requestedDamage = formula.CalculateNormalDamage(attacker, target, request.Action,
                request.Sequence);
            if (target.Kind == LiveEntityKind.Monster &&
                ninja.HasStatus(request.AuthenticatedActor.CharacterUid, NinjaStatusKind.FatalStrike,
                    request.MonotonicMilliseconds))
                requestedDamage = NinjaSkillEngine.CalculateFatalStrikeDamage(checked((uint)requestedDamage));
            if (target.Owner is GameplayActor targetOwner &&
                ninja.HasStatus(targetOwner.CharacterUid, NinjaStatusKind.ShurikenVortex,
                    request.MonotonicMilliseconds))
                requestedDamage = NinjaSkillEngine.ApplyShurikenVortexIncomingDamage(
                    checked((uint)requestedDamage));
            if (requestedDamage <= 0) throw new InvalidOperationException("The server damage formula returned a non-positive value.");
            byte counterLevel = 0;
            bool counterKill = target.Owner is GameplayActor counterOwner &&
                ninja.TryTriggerCounterKill(counterOwner.CharacterUid, attacker.LegacyUid, request.Sequence,
                    request.MonotonicMilliseconds, out counterLevel);
            LiveCombatEntity mutationEntity = targetEntity;
            LiveCombatEntitySnapshot mutationTarget = target;
            long damage;
            long remaining;
            bool killed;
            long counterDamage = 0;
            if (counterKill)
            {
                mutationEntity = attackerEntity;
                mutationTarget = attacker;
                long counterBase = formula.CalculateNormalDamage(target, attacker, 2, request.Sequence);
                counterDamage = Math.Min(attacker.CurrentHealth,
                    NinjaSkillEngine.CalculateCounterKillDamage(checked((uint)counterBase), counterLevel));
                damage = counterDamage;
                remaining = checked(attacker.CurrentHealth - counterDamage);
                killed = remaining == 0;
            }
            else
            {
                damage = Math.Min(target.CurrentHealth, requestedDamage);
                remaining = checked(target.CurrentHealth - damage);
                killed = remaining == 0;
            }
            Guid operationId = DeterministicOperationId(request.AuthenticatedActor.CharacterUid.Value, request.Sequence);
            bool sittingInterrupted = !counterKill && mutationTarget.Kind == LiveEntityKind.Player &&
                request.TargetWasSitting && request.TargetStamina.HasValue;
            ushort? targetStaminaAfter = request.TargetStamina;
            if (sittingInterrupted)
                targetStaminaAfter = request.TargetStamina!.Value > 30
                    ? checked((ushort)(request.TargetStamina.Value - 30)) : (ushort)0;
            var mutation = new CombatMutation(operationId, request.AuthenticatedActor, mutationTarget.Kind,
                mutationTarget.LegacyUid, mutationTarget.Map, mutationTarget.X, mutationTarget.Y,
                mutationTarget.Kind == LiveEntityKind.Player ? mutationTarget.PersistenceVersion : mutationTarget.Version,
                mutationTarget.CurrentHealth, damage, remaining, killed,
                counterKill ? "counter-kill" : "normal-attack", null,
                sittingInterrupted ? request.TargetStamina : null,
                sittingInterrupted ? targetStaminaAfter : null, sittingInterrupted);
            if (!counterKill && mutationTarget.Kind == LiveEntityKind.Monster && progressionPlanner is not null)
                mutation = mutation with { Progression = progressionPlanner(mutation)?.Validate() };
            if (killed && mutationTarget.Kind == LiveEntityKind.Monster && rewardPlanner is not null)
                mutation = mutation with { Reward = rewardPlanner(mutation) };
            CombatPersistenceResult persisted;
            try { persisted = await persistence.CommitAsync(mutation, cancellationToken); }
            catch
            {
                return attackerState.Record(request.Sequence, fingerprint,
                    Failure(request, LiveCombatResultCode.PersistenceFailure, "persistence-failure"));
            }
            if (persisted.Code == CombatPersistenceCode.Conflict)
                return attackerState.Record(request.Sequence, fingerprint,
                    Failure(request, LiveCombatResultCode.PersistenceConflict, "persistence-conflict"));
            if (persisted.Code == CombatPersistenceCode.Applied &&
                !mutationEntity.TryCommit(mutationTarget.Version, remaining,
                    !killed && mutationTarget.Attackable, request.MonotonicMilliseconds,
                    mutationTarget.Kind == LiveEntityKind.Player ? mutationTarget.PersistenceVersion : null))
                throw new InvalidOperationException("The serialized combat entity changed after persistence committed.");
            if (persisted.Code == CombatPersistenceCode.Applied && killed)
                ninja.OnDeath(new CharacterUid(mutationTarget.LegacyUid));

            var result = new LiveCombatResult(
                persisted.Code == CombatPersistenceCode.Replay ? LiveCombatResultCode.Replay : LiveCombatResultCode.Applied,
                request.AuthenticatedActor.CharacterUid.Value, request.TargetUid,
                counterKill ? 0 : damage, counterKill ? target.CurrentHealth : remaining, counterKill ? false : killed,
                persisted.Code == CombatPersistenceCode.Replay, counterKill ? "counter-kill" : "normal-attack",
                counterKill, counterDamage, counterKill ? target.LegacyUid : 0,
                counterKill ? attacker.LegacyUid : 0, counterKill ? counterLevel : (ushort)0,
                persisted.Reward, persisted.TargetStaminaAfter ?? targetStaminaAfter,
                persisted.SittingInterrupted || sittingInterrupted, persisted.Progression);
            return attackerState.Record(request.Sequence, fingerprint, result);
        }
        finally
        {
            second.MutationGate.Release();
            first.MutationGate.Release();
        }
    }

    public void RemoveAttackerState(uint characterUid) => attackers.TryRemove(characterUid, out _);

    private void RecordMonsterAttack(Guid operationId, LiveCombatResult result)
    {
        if (!monsterAttackResults.TryAdd(operationId, result)) return;
        lock (monsterAttackReceiptGate)
        {
            monsterAttackOrder.Enqueue(operationId);
            while (monsterAttackOrder.Count > 65_536)
                monsterAttackResults.TryRemove(monsterAttackOrder.Dequeue(), out _);
        }
    }

    public async ValueTask<NinjaLiveCombatResult> ExecuteNinjaAsync(NinjaLiveCombatRequest request,
        CancellationToken cancellationToken, Func<NinjaCombatMutation, MonsterRewardPlan?>? rewardPlanner = null,
        Func<NinjaCombatMutation, CombatProgressionPlan?>? progressionPlanner = null)
    {
        if (ninjaPersistence is null)
            return NinjaFailure(request, NinjaSkillResultCode.PersistenceFailure, "ninja-persistence-unavailable");
        if (!registry.TryResolve(request.AuthenticatedActor.CharacterUid.Value, out LiveCombatEntity? attackerEntity))
            return NinjaFailure(request, NinjaSkillResultCode.InvalidState, "attacker-missing");
        LiveCombatEntitySnapshot attacker = attackerEntity.Snapshot();
        if (attacker.Owner != request.AuthenticatedActor || attacker.Kind != LiveEntityKind.Player ||
            attacker.Map.MapId != request.Actor.Position.MapId || attacker.X != request.Actor.Position.X ||
            attacker.Y != request.Actor.Position.Y)
            return NinjaFailure(request, NinjaSkillResultCode.InvalidState, "authenticated-state-mismatch");
        if (!NativeNinjaSkillCatalog5165.TryGet(request.SkillId, out NinjaSkillDefinition definition))
            return NinjaFailure(request, NinjaSkillResultCode.SkillNotLearned, "unsupported-skill");

        LiveCombatEntity? targetEntity = null;
        LiveCombatEntitySnapshot? target = null;
        if (definition.TargetMode == NinjaTargetMode.Self &&
            (request.ClaimedTargetX != attacker.X || request.ClaimedTargetY != attacker.Y))
            return NinjaFailure(request, NinjaSkillResultCode.InvalidTarget, "self-coordinate-mismatch");
        if (definition.TargetMode != NinjaTargetMode.Self)
        {
            if (!registry.TryResolve(request.TargetUid, out targetEntity))
                return NinjaFailure(request, NinjaSkillResultCode.InvalidTarget, "target-missing");
            await targetEntity.MutationGate.WaitAsync(cancellationToken);
            target = targetEntity.Snapshot();
            if (request.Target is null || request.Target.CharacterUid.Value != request.TargetUid ||
                target.Map != attacker.Map || !CoordinateClaimIsPlausible(request.ClaimedTargetX,
                    request.ClaimedTargetY, target.X, target.Y))
            {
                targetEntity.MutationGate.Release();
                return NinjaFailure(request, NinjaSkillResultCode.InvalidTarget, "target-state-mismatch");
            }
            if (target.Kind == LiveEntityKind.Player && !pvp.CanAttack(attacker, target))
            {
                targetEntity.MutationGate.Release();
                return NinjaFailure(request, NinjaSkillResultCode.InvalidTarget, "pvp-not-allowed");
            }
        }
        try
        {
            NinjaCombatant? authoritativeTarget = request.Target is null || target is null ? null :
                request.Target with
                {
                    Position = new WorldPoint(target.Map.MapId, target.X, target.Y),
                    Alive = target.Alive,
                    IsFlying = target.IsFlying
                };
            NinjaSkillResult validated = ninja.Use(request.Sequence, request.Actor, authoritativeTarget,
                request.SkillId, request.SkillLevel, request.MonotonicMilliseconds);
            if (!validated.Code.Equals(NinjaSkillResultCode.Applied))
                return new(validated.Code, request.AuthenticatedActor.CharacterUid.Value, request.TargetUid,
                    request.SkillId, request.SkillLevel, validated.RemainingStamina, 0,
                    target?.CurrentHealth ?? attacker.CurrentHealth, false, validated.AppliedStatus,
                    validated.Replayed, "ninja-validation");

            long damage = CalculateNinjaDamage((NinjaSkillId)request.SkillId, request.SkillLevel,
                attacker, target, request.Sequence);
            long remaining = target is null ? 0 : checked(target.CurrentHealth - Math.Min(target.CurrentHealth, damage));
            bool killed = target is not null && remaining == 0;
            Guid operation = DeterministicSkillOperationId(request.AuthenticatedActor.CharacterUid.Value,
                request.Sequence, request.SkillId);
            var mutation = new NinjaCombatMutation(operation, request.AuthenticatedActor, request.SkillId,
                request.SkillLevel, request.Actor.Stamina, validated.RemainingStamina, target?.Kind,
                target?.LegacyUid ?? request.AuthenticatedActor.CharacterUid.Value, attacker.Map, attacker.X, attacker.Y,
                target?.X ?? attacker.X, target?.Y ?? attacker.Y,
                target is { Kind: LiveEntityKind.Player } ? target.PersistenceVersion : target?.Version ?? attacker.Version,
                target?.CurrentHealth ?? attacker.CurrentHealth, damage, target is null ? attacker.CurrentHealth : remaining,
                killed, Math.Max(1, damage));
            if (target?.Kind == LiveEntityKind.Monster && damage > 0 && progressionPlanner is not null)
                mutation = mutation with { Progression = progressionPlanner(mutation)?.Validate() };
            if (killed && target?.Kind == LiveEntityKind.Monster && rewardPlanner is not null)
                mutation = mutation with { Reward = rewardPlanner(mutation) };
            CombatPersistenceResult persisted;
            try { persisted = await ninjaPersistence.CommitSkillAsync(mutation, cancellationToken); }
            catch
            {
                ninja.OnDisconnect(request.Actor.CharacterUid);
                if (request.Target is not null) ninja.OnDisconnect(request.Target.CharacterUid);
                return NinjaFailure(request, NinjaSkillResultCode.PersistenceFailure, "persistence-failure");
            }
            if (persisted.Code == CombatPersistenceCode.Conflict)
            {
                ninja.OnDisconnect(request.Actor.CharacterUid);
                if (request.Target is not null) ninja.OnDisconnect(request.Target.CharacterUid);
                return NinjaFailure(request, NinjaSkillResultCode.PersistenceConflict, "persistence-conflict");
            }
            if (target is not null && persisted.Code == CombatPersistenceCode.Applied && damage > 0 &&
                !targetEntity!.TryCommit(target.Version, remaining, !killed && target.Attackable,
                    request.MonotonicMilliseconds,
                    target.Kind == LiveEntityKind.Player ? target.PersistenceVersion : null))
                throw new InvalidOperationException("The serialized Ninja target changed after persistence committed.");
            if (persisted.Code == CombatPersistenceCode.Applied && killed)
                ninja.OnDeath(new CharacterUid(target!.LegacyUid));
            return new(persisted.Code == CombatPersistenceCode.Replay ? NinjaSkillResultCode.Replay : validated.Code,
                request.AuthenticatedActor.CharacterUid.Value, request.TargetUid, request.SkillId,
                request.SkillLevel, validated.RemainingStamina, damage, remaining, killed,
                validated.AppliedStatus, persisted.Code == CombatPersistenceCode.Replay, "ninja-skill",
                persisted.Reward, persisted.Progression);
        }
        finally { targetEntity?.MutationGate.Release(); }
    }

    public void ClearNinjaTransientState(CharacterUid character) => ninja.OnDisconnect(character);

    public bool IsNinjaItemUseSuppressed(CharacterUid character, long monotonicMilliseconds) =>
        ninja.IsItemUseSuppressed(character, monotonicMilliseconds);

    private LiveCombatResult? Validate(NormalCombatRequest request,
        LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot target)
    {
        if (!target.Alive) return Failure(request, LiveCombatResultCode.TargetDead, "target-dead", target.CurrentHealth);
        if (!target.Attackable) return Failure(request, LiveCombatResultCode.TargetNotAttackable, "target-not-attackable", target.CurrentHealth);
        if (attacker.Map != target.Map) return Failure(request, LiveCombatResultCode.WrongMapOrInstance, "wrong-map-or-instance", target.CurrentHealth);
        // The genuine 5165 packet repeats the client's last displayed target coordinates.
        // They can lag one or more authoritative monster movements. Treat the values only
        // as a bounded visibility claim; UID resolution, map/instance and attack range all
        // continue to use the current server entity. The authorized owner handler likewise
        // resolves by UID and never substitutes these packet coordinates into combat state.
        if (!CoordinateClaimIsPlausible(request.ClaimedTargetX, request.ClaimedTargetY,
                target.X, target.Y))
            return Failure(request, LiveCombatResultCode.InvalidCoordinates, "target-coordinate-mismatch", target.CurrentHealth);
        int range = request.Action == 2 ? meleeRange : rangedRange;
        if (Math.Max(Math.Abs(attacker.X - target.X), Math.Abs(attacker.Y - target.Y)) > range)
            return Failure(request, LiveCombatResultCode.OutOfRange, "out-of-range", target.CurrentHealth);
        if (target.Kind == LiveEntityKind.Player && !pvp.CanAttack(attacker, target))
            return Failure(request, LiveCombatResultCode.PvpNotAllowed, "pvp-not-allowed", target.CurrentHealth);
        return null;
    }

    private static LiveCombatResult Failure(NormalCombatRequest request, LiveCombatResultCode code,
        string category, long remaining = 0) =>
        new(code, request.AuthenticatedActor.CharacterUid.Value, request.TargetUid,
            RemainingHealth: remaining, SafeCategory: category);

    private static string Fingerprint(NormalCombatRequest request) => string.Join('|',
        request.AuthenticatedActor.AccountId.Value.ToString("N", CultureInfo.InvariantCulture),
        request.AuthenticatedActor.CharacterUid.Value.ToString(CultureInfo.InvariantCulture),
        request.TargetUid.ToString(CultureInfo.InvariantCulture),
        request.ClaimedTargetX.ToString(CultureInfo.InvariantCulture),
        request.ClaimedTargetY.ToString(CultureInfo.InvariantCulture),
        request.Action.ToString(CultureInfo.InvariantCulture));

    private static Guid DeterministicOperationId(uint attacker, uint sequence)
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"revora-combat|{attacker}|{sequence}")));
        try { return new Guid(digest.AsSpan(0, 16)); }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    private static Guid DeterministicSkillOperationId(uint attacker, uint sequence, ushort skill)
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"revora-ninja|{attacker}|{sequence}|{skill}")));
        try { return new Guid(digest.AsSpan(0, 16)); }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    private long CalculateNinjaDamage(NinjaSkillId skill, ushort level,
        LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot? target, uint sequence) => skill switch
    {
        NinjaSkillId.TwofoldBlades when target is not null => NinjaSkillEngine.CalculateTwofoldDamage(
            checked((uint)formula.CalculateNormalDamage(attacker, target, 2, sequence)),
            checked((byte)level)),
        NinjaSkillId.ToxicFog when target is not null => NinjaSkillEngine.CalculateToxicFogCurrentHealth(
            checked((uint)target.CurrentHealth), checked((byte)level)),
        NinjaSkillId.ArcherBane when target is not null => NinjaSkillEngine.CalculateArcherBaneCurrentHealth(
            checked((uint)target.CurrentHealth)),
        _ => 0
    };

    private static bool CoordinateClaimIsPlausible(ushort claimedX, ushort claimedY,
        ushort authoritativeX, ushort authoritativeY) =>
        Math.Max(Math.Abs(claimedX - authoritativeX), Math.Abs(claimedY - authoritativeY)) <=
        MaximumClientCoordinateClaimDrift;

    private static NinjaLiveCombatResult NinjaFailure(NinjaLiveCombatRequest request,
        NinjaSkillResultCode code, string category) => new(code,
            request.AuthenticatedActor.CharacterUid.Value, request.TargetUid, request.SkillId,
            request.SkillLevel, request.Actor.Stamina, 0, 0, false, null, false, category);

    private sealed class AttackerState
    {
        private readonly object gate = new();
        private readonly Dictionary<uint, (string Fingerprint, LiveCombatResult Result)> receipts = [];
        private readonly Queue<uint> order = new();
        private long lastAttack = long.MinValue;

        public bool TryReplay(uint sequence, string fingerprint, out LiveCombatResult result)
        {
            lock (gate)
            {
                if (!receipts.TryGetValue(sequence, out var receipt)) { result = null!; return false; }
                result = receipt.Fingerprint == fingerprint
                    ? receipt.Result with { Code = LiveCombatResultCode.Replay, Replayed = true }
                    : receipt.Result with { Code = LiveCombatResultCode.CommandConflict, Damage = 0, Killed = false,
                        Replayed = false, SafeCategory = "command-conflict" };
                return true;
            }
        }

        public bool TryAcquireFrequency(long now, long interval)
        {
            lock (gate)
            {
                if (lastAttack != long.MinValue && now - lastAttack < interval) return false;
                lastAttack = now;
                return true;
            }
        }

        public LiveCombatResult Record(uint sequence, string fingerprint, LiveCombatResult result)
        {
            lock (gate)
            {
                if (receipts.TryGetValue(sequence, out var existing))
                    return existing.Fingerprint == fingerprint ? existing.Result with { Replayed = true } :
                        result with { Code = LiveCombatResultCode.CommandConflict, Damage = 0, Killed = false,
                            SafeCategory = "command-conflict" };
                receipts.Add(sequence, (fingerprint, result));
                order.Enqueue(sequence);
                while (order.Count > MaximumReceiptsPerAttacker) receipts.Remove(order.Dequeue());
                return result;
            }
        }
    }
}

public sealed class InMemoryCombatPersistence : ILiveCombatPersistence, INinjaCombatPersistence,
    IMonsterCombatPersistence
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, CombatMutation> receipts = [];
    private readonly Dictionary<Guid, NinjaCombatMutation> skillReceipts = [];
    private readonly Dictionary<Guid, MonsterCombatMutation> monsterReceipts = [];
    public bool FailNextCommit { get; set; }
    public IReadOnlyCollection<CombatMutation> Mutations { get { lock (gate) return receipts.Values.ToArray(); } }
    public IReadOnlyCollection<NinjaCombatMutation> SkillMutations { get { lock (gate) return skillReceipts.Values.ToArray(); } }
    public IReadOnlyCollection<MonsterCombatMutation> MonsterMutations { get { lock (gate) return monsterReceipts.Values.ToArray(); } }

    public ValueTask<CombatPersistenceResult> CommitAsync(CombatMutation mutation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (FailNextCommit) { FailNextCommit = false; throw new IOException("Injected persistence failure."); }
            if (receipts.TryGetValue(mutation.OperationId, out CombatMutation? existing))
                return ValueTask.FromResult(new CombatPersistenceResult(existing == mutation
                    ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict, null,
                    existing == mutation ? existing.RemainingTargetStamina : null,
                    existing == mutation && existing.SittingInterrupted));
            receipts.Add(mutation.OperationId, mutation);
            return ValueTask.FromResult(new CombatPersistenceResult(CombatPersistenceCode.Applied,
                null, mutation.RemainingTargetStamina, mutation.SittingInterrupted));
        }
    }

    public ValueTask<CombatPersistenceResult> CommitSkillAsync(NinjaCombatMutation mutation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (FailNextCommit) { FailNextCommit = false; throw new IOException("Injected persistence failure."); }
            if (skillReceipts.TryGetValue(mutation.OperationId, out NinjaCombatMutation? existing))
                return ValueTask.FromResult(new CombatPersistenceResult(existing == mutation
                    ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict));
            skillReceipts.Add(mutation.OperationId, mutation);
            return ValueTask.FromResult(new CombatPersistenceResult(CombatPersistenceCode.Applied));
        }
    }

    public ValueTask<CombatPersistenceResult> CommitMonsterAttackAsync(MonsterCombatMutation mutation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (FailNextCommit) { FailNextCommit = false; throw new IOException("Injected persistence failure."); }
            if (monsterReceipts.TryGetValue(mutation.OperationId, out MonsterCombatMutation? existing))
                return ValueTask.FromResult(new CombatPersistenceResult(existing == mutation
                    ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict, null,
                    existing == mutation ? existing.RemainingStamina : null,
                    existing == mutation && existing.SittingInterrupted));
            monsterReceipts.Add(mutation.OperationId, mutation);
            return ValueTask.FromResult(new CombatPersistenceResult(CombatPersistenceCode.Applied,
                null, mutation.RemainingStamina, mutation.SittingInterrupted));
        }
    }
}
