using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

internal sealed class CommandReplayLedger<TResult>
{
    private readonly Dictionary<GameplayCommandId, (string Fingerprint, TResult Result)> receipts = [];
    private readonly Func<TResult, TResult> asReplay;
    private readonly Func<TResult> conflict;

    public CommandReplayLedger(Func<TResult, TResult> asReplay, Func<TResult> conflict)
    {
        this.asReplay = asReplay;
        this.conflict = conflict;
    }

    public bool TryReplay(GameplayCommandId commandId, string fingerprint, out TResult result)
    {
        if (!receipts.TryGetValue(commandId, out var receipt))
        {
            result = default!;
            return false;
        }

        result = receipt.Fingerprint == fingerprint ? asReplay(receipt.Result) : conflict();
        return true;
    }

    public TResult Record(GameplayCommandId commandId, string fingerprint, TResult result)
    {
        receipts.Add(commandId, (fingerprint, result));
        return result;
    }
}

public sealed class DeterministicSpawnPlanner(IWorldCellPolicy cells)
{
    public IReadOnlyList<MonsterInstance> Plan(MonsterDefinition monster, SpawnRegionDefinition region)
    {
        monster.Validate();
        region.Validate();
        if (monster.Id != region.MonsterType) throw new ArgumentException("Spawn monster type does not match the definition.");
        if (!region.Enabled || !cells.IsKnownMap(region.MapId)) return [];

        int width = region.MaximumX - region.MinimumX + 1;
        int height = region.MaximumY - region.MinimumY + 1;
        long area = checked((long)width * height);
        long offset = PositiveModulo(unchecked(region.Id.Value * 1_103_515_245L + monster.Id.Value * 12_345L), area);
        var result = new List<MonsterInstance>(region.RequestedPopulation);
        for (long probe = 0; probe < area && result.Count < region.RequestedPopulation; probe++)
        {
            long index = (offset + probe) % area;
            var point = new WorldPoint(
                region.MapId,
                checked((ushort)(region.MinimumX + index % width)),
                checked((ushort)(region.MinimumY + index / width)));
            if (!cells.CanStand(point)) continue;
            var id = DeterministicMonsterId(region.Id, result.Count);
            result.Add(new MonsterInstance(
                id, monster.Id, region.Id, point, monster.MaximumHealth, monster.MaximumHealth,
                monster.Defense, MonsterLifeState.Alive));
        }
        return result;
    }

    private static long PositiveModulo(long value, long modulus) => (value % modulus + modulus) % modulus;

    private static MonsterInstanceId DeterministicMonsterId(SpawnRegionId region, int ordinal)
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"revora-monster|{region.Value}|{ordinal}")));
        try { return new MonsterInstanceId(new Guid(digest.AsSpan(0, 16))); }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }
}

public sealed class DeterministicCombatFormula : ICombatFormula
{
    public long CalculateDamage(CombatStats attacker, int targetDefense)
    {
        attacker.Validate();
        if (targetDefense < 0) throw new ArgumentOutOfRangeException(nameof(targetDefense));
        long midpoint = attacker.MinimumAttack + ((long)attacker.MaximumAttack - attacker.MinimumAttack) / 2;
        return Math.Max(1, midpoint - targetDefense);
    }
}

public sealed class CombatEngine(ICombatFormula formula, ICharacterOwnershipPolicy ownership)
{
    private readonly Dictionary<MonsterInstanceId, MonsterInstance> monsters = [];
    private readonly CommandReplayLedger<CombatResult> ledger = new(
        result => result.AsReplay(),
        () => new CombatResult(CombatResultCode.CommandConflict));

    public void AddMonster(MonsterInstance monster) => monsters.Add(monster.Id, monster.Validate());
    public MonsterInstance? FindMonster(MonsterInstanceId id) => monsters.GetValueOrDefault(id);

    public CombatResult Attack(
        GameplayCommandId commandId,
        GameplayActor attacker,
        WorldPoint attackerPosition,
        CombatStats attackerStats,
        MonsterInstanceId targetId)
    {
        string fingerprint = Fingerprint("attack", attacker, attackerPosition.MapId, attackerPosition.X,
            attackerPosition.Y, targetId.Value, attackerStats.MinimumAttack, attackerStats.MaximumAttack,
            attackerStats.Defense, attackerStats.AttackRange);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!ownership.Owns(attacker))
            return ledger.Record(commandId, fingerprint, new CombatResult(CombatResultCode.OwnershipMismatch));
        if (!monsters.TryGetValue(targetId, out var target))
            return ledger.Record(commandId, fingerprint, new CombatResult(CombatResultCode.TargetNotFound));
        if (target.LifeState == MonsterLifeState.Dead)
            return ledger.Record(commandId, fingerprint, new CombatResult(CombatResultCode.TargetDead, RemainingHealth: 0));
        if (target.Position.MapId != attackerPosition.MapId)
            return ledger.Record(commandId, fingerprint, new CombatResult(CombatResultCode.WrongMap, RemainingHealth: target.Health));
        if (attackerPosition.ChebyshevDistance(target.Position) > attackerStats.AttackRange)
            return ledger.Record(commandId, fingerprint, new CombatResult(CombatResultCode.OutOfRange, RemainingHealth: target.Health));

        long requestedDamage = formula.CalculateDamage(attackerStats, target.Defense);
        long damage = Math.Min(target.Health, requestedDamage);
        long remaining = target.Health - damage;
        bool killed = remaining == 0;
        monsters[targetId] = target with
        {
            Health = remaining,
            LifeState = killed ? MonsterLifeState.Dead : MonsterLifeState.Alive
        };
        return ledger.Record(commandId, fingerprint,
            new CombatResult(CombatResultCode.Applied, damage, remaining, killed));
    }

    internal static string Fingerprint(string operation, GameplayActor actor, params object[] values) =>
        string.Join('|', new[]
        {
            operation,
            actor.AccountId.Value.ToString("N", CultureInfo.InvariantCulture),
            actor.CharacterUid.Value.ToString(CultureInfo.InvariantCulture)
        }.Concat(values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)));
}

public sealed class DeterministicDropSelector
{
    public IReadOnlyList<DropRule> Select(MonsterTypeId monsterType, IEnumerable<DropRule> rules, int seedPartsPerMillion)
    {
        if (seedPartsPerMillion is < 0 or >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(seedPartsPerMillion));
        var selected = new List<DropRule>();
        foreach (var rule in rules.Where(rule => rule.MonsterType == monsterType).Select(rule => rule.Validate()).OrderBy(rule => rule.Ordinal))
        {
            int roll = (int)(((long)seedPartsPerMillion + (long)rule.Ordinal * 104_729L) % 1_000_000L);
            if (roll < rule.ChancePartsPerMillion) selected.Add(rule);
        }
        return selected;
    }
}

public sealed class ProgressionEngine(CharacterProgressionState initial, ILevelRequirementCatalog requirements)
{
    private readonly CommandReplayLedger<DomainCommandResult> ledger = NewLedger();
    public CharacterProgressionState State { get; private set; } = initial.Validate();

    public DomainCommandResult AwardExperience(GameplayCommandId commandId, GameplayActor actor, long amount)
    {
        string fingerprint = CombatEngine.Fingerprint("experience", actor, amount);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (State.AccountId != actor.AccountId || State.CharacterUid != actor.CharacterUid)
            return ledger.Record(commandId, fingerprint, Failure(DomainCommandCode.OwnershipMismatch, "ownership-mismatch"));
        if (amount is < 0 or > 1_000_000_000_000L)
            return ledger.Record(commandId, fingerprint, Failure(DomainCommandCode.Rejected, "experience-out-of-range"));
        long total;
        try { total = checked(State.TotalExperience + amount); }
        catch (OverflowException) { return ledger.Record(commandId, fingerprint, Failure(DomainCommandCode.Overflow, "experience-overflow")); }

        byte level = State.Level;
        while (level < requirements.MaximumLevel && requirements.RequiredTotalExperience((byte)(level + 1)) <= total)
            level++;
        State = State with { Level = level, TotalExperience = total };
        return ledger.Record(commandId, fingerprint, Success("experience-applied"));
    }

    internal static CommandReplayLedger<DomainCommandResult> NewLedger() => new(
        result => result.AsReplay(),
        () => Failure(DomainCommandCode.CommandConflict, "command-conflict"));
    internal static DomainCommandResult Success(string category) => new(DomainCommandCode.Applied, SafeCategory: category);
    internal static DomainCommandResult Failure(DomainCommandCode code, string category) => new(code, SafeCategory: category);
}

public sealed class TeamAggregate(
    Guid teamId,
    GameplayActor leader,
    ICharacterOwnershipPolicy ownership,
    int maximumMembers = 5)
{
    private readonly int capacity = maximumMembers is >= 2 and <= 40
        ? maximumMembers
        : throw new ArgumentOutOfRangeException(nameof(maximumMembers));
    private readonly HashSet<CharacterUid> members = [leader.CharacterUid];
    private readonly Dictionary<Guid, SocialInvitation> invitations = [];
    private readonly CommandReplayLedger<DomainCommandResult> ledger = ProgressionEngine.NewLedger();

    public Guid TeamId { get; } = teamId != Guid.Empty ? teamId : throw new ArgumentException("Team identifier cannot be empty.", nameof(teamId));
    public GameplayActor Leader { get; } = ownership.Owns(leader) ? leader : throw new ArgumentException("Leader ownership is invalid.", nameof(leader));
    public IReadOnlySet<CharacterUid> Members => members;

    public DomainCommandResult Invite(GameplayCommandId commandId, GameplayActor actor, Guid invitationId,
        GameplayActor recipient, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        string fingerprint = CombatEngine.Fingerprint("team-invite", actor, invitationId, recipient.CharacterUid.Value,
            now.UtcTicks, expiresAt.UtcTicks);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (actor != Leader || !ownership.Owns(actor) || !ownership.Owns(recipient))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "ownership-mismatch"));
        if (members.Contains(recipient.CharacterUid) || invitations.ContainsKey(invitationId))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Duplicate, "duplicate-invitation"));
        if (expiresAt <= now || expiresAt > now.AddMinutes(15))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Rejected, "invalid-expiry"));
        invitations.Add(invitationId, new SocialInvitation(
            invitationId, actor.CharacterUid, recipient.CharacterUid, InvitationKind.Team, now, expiresAt, InvitationState.Open).Validate());
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("invitation-created"));
    }

    public DomainCommandResult Accept(GameplayCommandId commandId, GameplayActor recipient, Guid invitationId, DateTimeOffset now)
    {
        string fingerprint = CombatEngine.Fingerprint("team-accept", recipient, invitationId, now.UtcTicks);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!ownership.Owns(recipient))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "ownership-mismatch"));
        if (!invitations.TryGetValue(invitationId, out var invitation) || invitation.Recipient != recipient.CharacterUid)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.NotFound, "invitation-not-found"));
        if (invitation.State != InvitationState.Open)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "invitation-not-open"));
        if (now >= invitation.ExpiresAt)
        {
            invitations[invitationId] = invitation with { State = InvitationState.Expired };
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Expired, "invitation-expired"));
        }
        if (members.Count >= capacity)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.CapacityReached, "team-full"));
        members.Add(recipient.CharacterUid);
        invitations[invitationId] = invitation with { State = InvitationState.Consumed };
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("team-joined"));
    }
}

public sealed class TradeAggregate(
    Guid tradeId,
    GameplayActor first,
    GameplayActor second,
    DateTimeOffset expiresAt,
    ITradeSettlementCommitter committer)
{
    private readonly Dictionary<ItemInstanceId, TradeItemOffer> items = [];
    private readonly Dictionary<(CharacterUid, CurrencyKind), TradeCurrencyOffer> currency = [];
    private readonly CommandReplayLedger<DomainCommandResult> ledger = ProgressionEngine.NewLedger();
    private bool firstConfirmed;
    private bool secondConfirmed;

    public Guid TradeId { get; } = tradeId != Guid.Empty && first != second ? tradeId : throw new ArgumentException("Trade identity or participants are invalid.");
    public TradeState State { get; private set; } = TradeState.Open;
    public TradeSettlement? Settlement { get; private set; }

    public DomainCommandResult OfferItem(GameplayCommandId commandId, GameplayActor actor, ItemInstance item, long expectedVersion)
    {
        string fingerprint = CombatEngine.Fingerprint("trade-item", actor, item.Id.Value, expectedVersion);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!IsParticipant(actor) || item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "foreign-item"));
        if (State is TradeState.Committed or TradeState.Cancelled)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "trade-closed"));
        var offer = new TradeItemOffer(actor.CharacterUid, item.Id, expectedVersion).Validate();
        if (items.TryGetValue(item.Id, out var existing) && existing != offer)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Duplicate, "item-already-offered"));
        items[item.Id] = offer;
        ResetConfirmations();
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("item-offered"));
    }

    public DomainCommandResult OfferCurrency(GameplayCommandId commandId, GameplayActor actor, CurrencyKind kind, long amount)
    {
        string fingerprint = CombatEngine.Fingerprint("trade-currency", actor, (byte)kind, amount);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!IsParticipant(actor))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "not-participant"));
        if (State is TradeState.Committed or TradeState.Cancelled)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "trade-closed"));
        currency[(actor.CharacterUid, kind)] = new TradeCurrencyOffer(actor.CharacterUid, kind, amount).Validate();
        ResetConfirmations();
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("currency-offered"));
    }

    public DomainCommandResult Confirm(GameplayCommandId commandId, GameplayActor actor, DateTimeOffset now)
    {
        string fingerprint = CombatEngine.Fingerprint("trade-confirm", actor, now.UtcTicks);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!IsParticipant(actor))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "not-participant"));
        if (now >= expiresAt)
        {
            State = TradeState.Cancelled;
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Expired, "trade-expired"));
        }
        if (State is TradeState.Committed or TradeState.Cancelled)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "trade-closed"));
        if (actor == first) firstConfirmed = true; else secondConfirmed = true;
        State = firstConfirmed && secondConfirmed ? TradeState.SecondConfirmed
            : firstConfirmed ? TradeState.FirstConfirmed : TradeState.SecondConfirmed;
        if (!(firstConfirmed && secondConfirmed))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("trade-confirmed"));

        var settlement = new TradeSettlement(TradeId, first.CharacterUid, second.CharacterUid,
            items.Values.OrderBy(item => item.ItemInstanceId.Value).ToArray(),
            currency.Values.OrderBy(value => value.OfferingCharacter.Value).ThenBy(value => value.Currency).ToArray());
        var committed = committer.Commit(settlement);
        if (!committed.Succeeded)
        {
            ResetConfirmations();
            return ledger.Record(commandId, fingerprint, committed);
        }
        Settlement = settlement;
        State = TradeState.Committed;
        return ledger.Record(commandId, fingerprint, committed);
    }

    private bool IsParticipant(GameplayActor actor) => actor == first || actor == second;
    private void ResetConfirmations() { firstConfirmed = false; secondConfirmed = false; State = TradeState.Open; }
}

public sealed class GuildAggregate(
    Guid guildId,
    Guid realmId,
    string name,
    GameplayActor leader,
    ICharacterOwnershipPolicy ownership)
{
    private readonly Dictionary<CharacterUid, GuildMember> members =
        new() { [leader.CharacterUid] = new GuildMember(leader.CharacterUid, GuildRank.Leader, 0) };
    private readonly CommandReplayLedger<DomainCommandResult> ledger = ProgressionEngine.NewLedger();

    public Guid GuildId { get; } = guildId != Guid.Empty ? guildId : throw new ArgumentException("Guild identifier cannot be empty.", nameof(guildId));
    public Guid RealmId { get; } = realmId != Guid.Empty ? realmId : throw new ArgumentException("Realm identifier cannot be empty.", nameof(realmId));
    public string Name { get; } = ValidateName(name);
    public GameplayActor Leader { get; } = ownership.Owns(leader)
        ? leader
        : throw new ArgumentException("Leader ownership is invalid.", nameof(leader));
    public long SilverFund { get; private set; }
    public IReadOnlyDictionary<CharacterUid, GuildMember> Members => members;

    public DomainCommandResult AddMember(GameplayCommandId commandId, GameplayActor actor, GameplayActor member)
    {
        string fingerprint = CombatEngine.Fingerprint("guild-add", actor,
            member.AccountId.Value, member.CharacterUid.Value);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (actor != Leader) return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "leader-required"));
        if (!ownership.Owns(member))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "member-ownership-invalid"));
        if (!members.TryAdd(member.CharacterUid, new GuildMember(member.CharacterUid, GuildRank.Member, 0)))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Duplicate, "already-member"));
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("member-added"));
    }

    public DomainCommandResult AdjustFund(GameplayCommandId commandId, GameplayActor actor, long delta)
    {
        string fingerprint = CombatEngine.Fingerprint("guild-fund", actor, delta);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!members.ContainsKey(actor.CharacterUid))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "membership-required"));
        long next;
        try { next = checked(SilverFund + delta); }
        catch (OverflowException) { return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Overflow, "fund-overflow")); }
        if (next < 0) return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Rejected, "insufficient-fund"));
        SilverFund = next;
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("fund-adjusted"));
    }

    private static string ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 16 || value.Any(char.IsControl))
            throw new ArgumentException("Guild name is invalid.", nameof(value));
        return value;
    }
}

public sealed class QuestAggregate(QuestDefinition definition, CharacterQuestProgress initial)
{
    private readonly CommandReplayLedger<DomainCommandResult> ledger = ProgressionEngine.NewLedger();
    public QuestDefinition Definition { get; } = definition.Validate();
    public CharacterQuestProgress Progress { get; private set; } = ValidateInitial(definition, initial);

    public DomainCommandResult Advance(GameplayCommandId commandId, GameplayActor actor,
        QuestObjectiveKind objective, long targetLegacyId, int amount)
    {
        string fingerprint = CombatEngine.Fingerprint("quest-advance", actor, (byte)objective, targetLegacyId, amount);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (Progress.AccountId != actor.AccountId || Progress.CharacterUid != actor.CharacterUid)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "ownership-mismatch"));
        if (Progress.State != QuestProgressState.Active)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "quest-not-active"));
        if (amount is < 1 or > 1_000_000)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Rejected, "invalid-progress"));
        var step = Definition.Steps.Single(candidate => candidate.StepNumber == Progress.StepNumber);
        if (step.Objective != objective || step.TargetLegacyId != targetLegacyId)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Rejected, "objective-mismatch"));
        int count = Math.Min(step.RequiredCount, checked(Progress.ProgressCount + amount));
        if (count < step.RequiredCount)
            Progress = Progress with { ProgressCount = count };
        else if (Progress.StepNumber == Definition.Steps.Count)
            Progress = Progress with { ProgressCount = count, State = QuestProgressState.Completed };
        else
            Progress = Progress with { StepNumber = checked((short)(Progress.StepNumber + 1)), ProgressCount = 0 };
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("quest-advanced"));
    }

    private static CharacterQuestProgress ValidateInitial(QuestDefinition definition, CharacterQuestProgress initial)
    {
        definition.Validate();
        initial.Validate();
        if (initial.QuestId != definition.QuestId || initial.StepNumber > definition.Steps.Count)
            throw new ArgumentException("Quest progress does not match the definition.", nameof(initial));
        return initial;
    }
}

public sealed class EventAggregate(
    Guid eventInstanceId,
    int maximumParticipants,
    DateTimeOffset startsAt,
    ICharacterOwnershipPolicy ownership)
{
    private readonly int capacity = maximumParticipants is >= 2 and <= 10_000
        ? maximumParticipants
        : throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
    private readonly Dictionary<CharacterUid, EventParticipant> participants = [];
    private readonly CommandReplayLedger<DomainCommandResult> ledger = ProgressionEngine.NewLedger();
    public Guid EventInstanceId { get; } = eventInstanceId != Guid.Empty ? eventInstanceId : throw new ArgumentException("Event identifier cannot be empty.", nameof(eventInstanceId));
    public EventLifecycleState State { get; private set; } = EventLifecycleState.Registration;
    public IReadOnlyDictionary<CharacterUid, EventParticipant> Participants => participants;

    public DomainCommandResult Join(GameplayCommandId commandId, GameplayActor actor, DateTimeOffset now)
    {
        string fingerprint = CombatEngine.Fingerprint("event-join", actor, now.UtcTicks);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!ownership.Owns(actor))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "ownership-mismatch"));
        if (State != EventLifecycleState.Registration || now >= startsAt)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "registration-closed"));
        if (participants.Count >= capacity)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.CapacityReached, "event-full"));
        if (!participants.TryAdd(actor.CharacterUid, new EventParticipant(actor.CharacterUid, 0)))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Duplicate, "already-joined"));
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("event-joined"));
    }

    public DomainCommandResult AddScore(GameplayCommandId commandId, GameplayActor actor, long delta)
    {
        string fingerprint = CombatEngine.Fingerprint("event-score", actor, delta);
        if (ledger.TryReplay(commandId, fingerprint, out var replay)) return replay;
        if (!ownership.Owns(actor) || !participants.TryGetValue(actor.CharacterUid, out var participant))
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.OwnershipMismatch, "participant-required"));
        if (State != EventLifecycleState.Running || delta is < -1_000_000_000 or > 1_000_000_000)
            return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.InvalidState, "score-rejected"));
        long score;
        try { score = checked(participant.Score + delta); }
        catch (OverflowException) { return ledger.Record(commandId, fingerprint, ProgressionEngine.Failure(DomainCommandCode.Overflow, "score-overflow")); }
        participants[actor.CharacterUid] = participant with { Score = score };
        return ledger.Record(commandId, fingerprint, ProgressionEngine.Success("score-applied"));
    }

    public void Start(DateTimeOffset now)
    {
        if (State != EventLifecycleState.Registration || now < startsAt) throw new InvalidOperationException("Event cannot start.");
        State = EventLifecycleState.Running;
    }

    public void Complete()
    {
        if (State != EventLifecycleState.Running) throw new InvalidOperationException("Event is not running.");
        int placement = 1;
        foreach (var participant in participants.Values.OrderByDescending(value => value.Score).ThenBy(value => value.CharacterUid.Value).ToArray())
            participants[participant.CharacterUid] = participant with { Placement = placement++ };
        State = EventLifecycleState.Completed;
    }
}
