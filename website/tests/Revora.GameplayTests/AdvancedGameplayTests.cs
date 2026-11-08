using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.GameplayTests;

internal static class AdvancedGameplayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly GameplayActor First = Actor("11111111-1111-1111-1111-111111111111", 1_000_001);
    private static readonly GameplayActor Second = Actor("22222222-2222-2222-2222-222222222222", 1_000_002);
    private static readonly GameplayActor Third = Actor("33333333-3333-3333-3333-333333333333", 1_000_003);

    public static int Run()
    {
        SpawnPlanningIsDeterministicAndBounded();
        CombatIsOwnershipBoundAndReplaySafe();
        DropsAreDeterministicAndBounded();
        ProgressionIsOwnershipBoundAndReplaySafe();
        TeamInvitationsAreBoundedAndReplaySafe();
        TradeSettlementIsAtomicAtThePersistenceBoundary();
        GuildMembershipAndFundsAreBounded();
        QuestProgressionIsObjectiveBoundAndIdempotent();
        EventRegistrationScoringAndPlacementAreDeterministic();
        ConstructorsRejectUnsafeBounds();
        return 10;
    }

    private static void SpawnPlanningIsDeterministicAndBounded()
    {
        var cells = new FixedWorldCells([new WorldPoint(1002, 11, 10)]);
        var planner = new DeterministicSpawnPlanner(cells);
        var monster = Monster(maximumHealth: 100, defense: 3);
        var region = new SpawnRegionDefinition(new SpawnRegionId(71), monster.Id, 1002, 4, 10, 10, 12, 11, true);

        var first = planner.Plan(monster, region);
        var second = planner.Plan(monster, region);
        Equal(4, first.Count);
        Equal(string.Join(',', first.Select(value => value.Id.Value)), string.Join(',', second.Select(value => value.Id.Value)));
        True(first.All(value => value.Position.MapId == 1002 && value.Position.X is >= 10 and <= 12 && value.Position.Y is >= 10 and <= 11));
        True(first.All(value => value.Position != new WorldPoint(1002, 11, 10)));
        Equal(0, planner.Plan(monster, region with { Enabled = false }).Count);
        Equal(0, planner.Plan(monster, region with { MapId = 9999 }).Count);
    }

    private static void CombatIsOwnershipBoundAndReplaySafe()
    {
        var ownership = new FixedOwnership(First, Second);
        var engine = new CombatEngine(new DeterministicCombatFormula(), ownership);
        var targetId = new MonsterInstanceId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
        engine.AddMonster(new MonsterInstance(targetId, new MonsterTypeId(1), new SpawnRegionId(1),
            new WorldPoint(1002, 11, 10), 20, 20, 5, MonsterLifeState.Alive));
        var command = Command("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stats = new CombatStats(30, 30, 0, 2);

        var applied = engine.Attack(command, First, new WorldPoint(1002, 10, 10), stats, targetId);
        Equal(CombatResultCode.Applied, applied.Code);
        Equal(20L, applied.Damage);
        Equal(true, applied.Killed);
        var replay = engine.Attack(command, First, new WorldPoint(1002, 10, 10), stats, targetId);
        Equal(true, replay.Replayed);
        Equal(0L, engine.FindMonster(targetId)!.Health);
        Equal(CombatResultCode.CommandConflict,
            engine.Attack(command, First, new WorldPoint(1002, 9, 10), stats, targetId).Code);

        var otherTarget = new MonsterInstanceId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"));
        engine.AddMonster(new MonsterInstance(otherTarget, new MonsterTypeId(1), new SpawnRegionId(1),
            new WorldPoint(1002, 20, 20), 100, 100, 5, MonsterLifeState.Alive));
        Equal(CombatResultCode.OwnershipMismatch,
            engine.Attack(Command(), Third, new WorldPoint(1002, 20, 20), stats, otherTarget).Code);
        Equal(CombatResultCode.OutOfRange,
            engine.Attack(Command(), First, new WorldPoint(1002, 1, 1), stats, otherTarget).Code);
    }

    private static void DropsAreDeterministicAndBounded()
    {
        var selector = new DeterministicDropSelector();
        var monster = new MonsterTypeId(7);
        var rules = new[]
        {
            new DropRule(monster, 2, new ItemDefinitionId(1002), 1_000_000),
            new DropRule(monster, 1, new ItemDefinitionId(1001), 1_000_000),
            new DropRule(new MonsterTypeId(8), 1, new ItemDefinitionId(1003), 1_000_000)
        };
        var selected = selector.Select(monster, rules, 500_000);
        Equal("1001,1002", string.Join(',', selected.Select(value => value.ItemDefinition.Value)));
        Throws<ArgumentOutOfRangeException>(() => selector.Select(monster, rules, 1_000_000));
        Throws<ArgumentOutOfRangeException>(() => new DropRule(monster, 0, new ItemDefinitionId(1001), 1_000_001).Validate());
    }

    private static void ProgressionIsOwnershipBoundAndReplaySafe()
    {
        var requirements = new FixedLevelRequirements(3, new Dictionary<byte, long> { [1] = 0, [2] = 100, [3] = 300 });
        var engine = new ProgressionEngine(new CharacterProgressionState(First.AccountId, First.CharacterUid, 1, 0), requirements);
        var command = Command("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Equal(DomainCommandCode.Applied, engine.AwardExperience(command, First, 350).Code);
        Equal((byte)3, engine.State.Level);
        Equal(350L, engine.State.TotalExperience);
        Equal(true, engine.AwardExperience(command, First, 350).Replayed);
        Equal(350L, engine.State.TotalExperience);
        Equal(DomainCommandCode.CommandConflict, engine.AwardExperience(command, First, 351).Code);
        Equal(DomainCommandCode.OwnershipMismatch, engine.AwardExperience(Command(), Second, 1).Code);
        Equal(DomainCommandCode.Rejected, engine.AwardExperience(Command(), First, -1).Code);

        var skill = new SkillProgress(new SkillTypeId(1000), 4, 1234).Validate();
        Equal((ushort)1000, skill.SkillType.Value);
    }

    private static void TeamInvitationsAreBoundedAndReplaySafe()
    {
        var ownership = new FixedOwnership(First, Second, Third);
        var team = new TeamAggregate(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), First, ownership, 2);
        var invitation = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var inviteCommand = Command();
        Equal(DomainCommandCode.Applied, team.Invite(inviteCommand, First, invitation, Second, Now, Now.AddMinutes(1)).Code);
        Equal(true, team.Invite(inviteCommand, First, invitation, Second, Now, Now.AddMinutes(1)).Replayed);
        Equal(DomainCommandCode.Applied, team.Accept(Command(), Second, invitation, Now.AddSeconds(1)).Code);
        Equal(2, team.Members.Count);

        var thirdInvitation = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
        Equal(DomainCommandCode.Applied, team.Invite(Command(), First, thirdInvitation, Third, Now, Now.AddMinutes(1)).Code);
        Equal(DomainCommandCode.CapacityReached, team.Accept(Command(), Third, thirdInvitation, Now.AddSeconds(1)).Code);

        var expired = new TeamAggregate(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccd"), First, ownership);
        var expiredInvitation = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
        expired.Invite(Command(), First, expiredInvitation, Second, Now, Now.AddSeconds(1));
        Equal(DomainCommandCode.Expired, expired.Accept(Command(), Second, expiredInvitation, Now.AddSeconds(2)).Code);
    }

    private static void TradeSettlementIsAtomicAtThePersistenceBoundary()
    {
        var committer = new RecordingTradeCommitter();
        var trade = new TradeAggregate(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), First, Second, Now.AddMinutes(2), committer);
        var item = new ItemInstance(
            new ItemInstanceId(Guid.Parse("dddddddd-0000-0000-0000-000000000001")),
            new LegacyItemUid(9_000_001), new ItemDefinitionId(410003), First.AccountId, First.CharacterUid,
            ItemLocation.Inventory(), 10, 0, 0, 0, 0, 0);
        Equal(DomainCommandCode.OwnershipMismatch, trade.OfferItem(Command(), Third, item, 1).Code);
        Equal(DomainCommandCode.Applied, trade.OfferItem(Command(), First, item, 1).Code);
        Equal(DomainCommandCode.Applied, trade.OfferCurrency(Command(), Second, CurrencyKind.Silver, 250).Code);
        Equal(DomainCommandCode.Applied, trade.Confirm(Command(), First, Now).Code);
        var finalCommand = Command("dddddddd-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Equal(DomainCommandCode.Applied, trade.Confirm(finalCommand, Second, Now).Code);
        Equal(TradeState.Committed, trade.State);
        Equal(1, committer.CommitCount);
        Equal(true, trade.Confirm(finalCommand, Second, Now).Replayed);
        Equal(1, committer.CommitCount);
        Equal(1, trade.Settlement!.Items.Count);
    }

    private static void GuildMembershipAndFundsAreBounded()
    {
        var ownership = new FixedOwnership(First, Second);
        var guild = new GuildAggregate(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"), "Revora", First, ownership);
        Equal(DomainCommandCode.OwnershipMismatch, guild.AddMember(Command(), First, Third).Code);
        Equal(DomainCommandCode.Applied, guild.AddMember(Command(), First, Second).Code);
        var fundCommand = Command();
        Equal(DomainCommandCode.Applied, guild.AdjustFund(fundCommand, Second, 100).Code);
        Equal(true, guild.AdjustFund(fundCommand, Second, 100).Replayed);
        Equal(100L, guild.SilverFund);
        Equal(DomainCommandCode.Rejected, guild.AdjustFund(Command(), Second, -101).Code);
        Equal(100L, guild.SilverFund);
    }

    private static void QuestProgressionIsObjectiveBoundAndIdempotent()
    {
        var definition = new QuestDefinition(91, "First steps", 1, null, QuestRepeatPolicy.Once,
        [
            new QuestStep(1, QuestObjectiveKind.DefeatMonster, 7, 2),
            new QuestStep(2, QuestObjectiveKind.TalkToNpc, 10_000, 1)
        ]);
        var quest = new QuestAggregate(definition,
            new CharacterQuestProgress(First.AccountId, First.CharacterUid, 91, 1, 0, QuestProgressState.Active));
        Equal(DomainCommandCode.Rejected,
            quest.Advance(Command(), First, QuestObjectiveKind.TalkToNpc, 10_000, 1).Code);
        var firstKill = Command();
        Equal(DomainCommandCode.Applied,
            quest.Advance(firstKill, First, QuestObjectiveKind.DefeatMonster, 7, 1).Code);
        Equal(true, quest.Advance(firstKill, First, QuestObjectiveKind.DefeatMonster, 7, 1).Replayed);
        Equal(1, quest.Progress.ProgressCount);
        quest.Advance(Command(), First, QuestObjectiveKind.DefeatMonster, 7, 1);
        Equal((short)2, quest.Progress.StepNumber);
        Equal(DomainCommandCode.Applied,
            quest.Advance(Command(), First, QuestObjectiveKind.TalkToNpc, 10_000, 1).Code);
        Equal(QuestProgressState.Completed, quest.Progress.State);
    }

    private static void EventRegistrationScoringAndPlacementAreDeterministic()
    {
        var ownership = new FixedOwnership(First, Second, Third);
        var tournament = new EventAggregate(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), 2, Now.AddMinutes(1), ownership);
        Equal(DomainCommandCode.Applied, tournament.Join(Command(), First, Now).Code);
        Equal(DomainCommandCode.Applied, tournament.Join(Command(), Second, Now).Code);
        Equal(DomainCommandCode.CapacityReached, tournament.Join(Command(), Third, Now).Code);
        tournament.Start(Now.AddMinutes(1));
        var score = Command();
        Equal(DomainCommandCode.Applied, tournament.AddScore(score, First, 10).Code);
        Equal(true, tournament.AddScore(score, First, 10).Replayed);
        Equal(DomainCommandCode.Applied, tournament.AddScore(Command(), Second, 10).Code);
        tournament.Complete();
        Equal(1, tournament.Participants[First.CharacterUid].Placement!.Value);
        Equal(2, tournament.Participants[Second.CharacterUid].Placement!.Value);
    }

    private static void ConstructorsRejectUnsafeBounds()
    {
        Throws<ArgumentOutOfRangeException>(() => new SkillTypeId(0));
        Throws<ArgumentOutOfRangeException>(() => new SpawnRegionDefinition(
            new SpawnRegionId(1), new MonsterTypeId(1), 1002, 10_001, 1, 1, 2, 2, true).Validate());
        Throws<ArgumentException>(() => new TeamAggregate(Guid.NewGuid(), First, new FixedOwnership(First), 1));
        Throws<ArgumentException>(() => new GuildAggregate(Guid.NewGuid(), Guid.NewGuid(), new string('x', 17), First, new FixedOwnership(First)));
    }

    private static MonsterDefinition Monster(long maximumHealth, int defense) => new MonsterDefinition(
        new MonsterTypeId(7), "Pheasant", 0, 1, 1, maximumHealth, defense, 0, 0, 10, 20, 0, 1, 1, 500, 5_000).Validate();

    private static GameplayActor Actor(string accountId, uint characterUid) =>
        new(new AccountId(Guid.Parse(accountId)), new CharacterUid(characterUid));

    private static GameplayCommandId Command(string? value = null) =>
        new(value is null ? Guid.NewGuid() : Guid.Parse(value));

    private static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class FixedWorldCells(IEnumerable<WorldPoint> blocked) : IWorldCellPolicy
    {
        private readonly HashSet<WorldPoint> blockedCells = [.. blocked];
        public bool IsKnownMap(int mapId) => mapId == 1002;
        public bool CanStand(WorldPoint point) => IsKnownMap(point.MapId) && !blockedCells.Contains(point);
    }

    private sealed class FixedOwnership(params GameplayActor[] actors) : ICharacterOwnershipPolicy
    {
        private readonly HashSet<GameplayActor> allowed = [.. actors];
        public bool Owns(GameplayActor actor) => allowed.Contains(actor);
    }

    private sealed class FixedLevelRequirements(byte maximumLevel, IReadOnlyDictionary<byte, long> requirements)
        : ILevelRequirementCatalog
    {
        public byte MaximumLevel { get; } = maximumLevel;
        public long RequiredTotalExperience(byte level) => requirements[level];
    }

    private sealed class RecordingTradeCommitter : ITradeSettlementCommitter
    {
        public int CommitCount { get; private set; }
        public DomainCommandResult Commit(TradeSettlement settlement)
        {
            CommitCount++;
            return new DomainCommandResult(DomainCommandCode.Applied, SafeCategory: "trade-committed");
        }
    }
}
