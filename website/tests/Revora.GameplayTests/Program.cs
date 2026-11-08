using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.GameplayTests;

internal static class Program
{
    private static readonly AccountId AccountOne = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly AccountId AccountTwo = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly CharacterUid CharacterOne = new(1_000_001);
    private static readonly CharacterUid CharacterTwo = new(1_000_002);
    private static readonly ItemDefinitionId SwordId = new(410_003);
    private static readonly ShopId Shop = new(10_001);

    private static async Task Main()
    {
        await BuyIsIdempotentAndBoundedByCapacity();
        await ReusedCommandWithDifferentPayloadIsRejected();
        await ForeignItemCannotBeEquippedSoldOrWarehoused();
        await WarehouseCapacityIsAtomic();
        await FailedCommitRollsBackCurrencyAndItems();
        await CurrencyCommandsAreIdempotentAndOwnershipBound();
        await EquipmentRequirementsAndSlotsAreEnforced();
        await NinjaDualKatanaSlotsAreAuthoritative();
        await OwnerCanaryGrantAndRollbackAreAtomicAndReplaySafe();
        await InvalidShopReferenceDataCannotMintCurrency();
        EquipmentStatsAreDeterministic();
        int advancedTests = AdvancedGameplayTests.Run();
        int ninjaTests = NinjaGameplayTests.Run();
        int liveCombatTests = await LiveCombatRuntimeTests.RunAsync();
        int staminaAndMonsterTests = StaminaAndMonsterTests.Run();
        Console.WriteLine($"Revora.GameplayTests: {11 + advancedTests + ninjaTests + liveCombatTests + staminaAndMonsterTests} passed");
    }

    private static async Task BuyIsIdempotentAndBoundedByCapacity()
    {
        var fixture = Fixture(inventoryCapacity: 2, silver: 1_000);
        var service = fixture.ShopService();
        var command = Command("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await service.BuyAsync(command, fixture.ActorOne, Shop, SwordId, 2, default);
        var replay = await service.BuyAsync(command, fixture.ActorOne, Shop, SwordId, 2, default);
        Equal(GameplayResultCode.Success, first.Code);
        Equal(true, replay.Replayed);
        Equal(2, fixture.Store.Items(CharacterOne).Count);
        Equal(800L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));

        var full = await service.BuyAsync(Command("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab"), fixture.ActorOne, Shop, SwordId, 1, default);
        Equal(GameplayResultCode.InventoryFull, full.Code);
        Equal(800L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
    }

    private static async Task ReusedCommandWithDifferentPayloadIsRejected()
    {
        var fixture = Fixture();
        var service = fixture.ShopService();
        var command = Command("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Equal(GameplayResultCode.Success, (await service.BuyAsync(command, fixture.ActorOne, Shop, SwordId, 1, default)).Code);
        Equal(GameplayResultCode.CommandConflict, (await service.BuyAsync(command, fixture.ActorOne, Shop, SwordId, 2, default)).Code);
        Equal(1, fixture.Store.Items(CharacterOne).Count);
    }

    private static async Task ForeignItemCannotBeEquippedSoldOrWarehoused()
    {
        var fixture = Fixture();
        var foreign = fixture.Store.SeedItem(AccountTwo, CharacterTwo, SwordId, ItemLocation.Inventory());
        Equal(GameplayResultCode.ForeignItem, (await fixture.Equipment.EquipAsync(Command(), fixture.ActorOne, foreign.Id, default)).Code);
        Equal(GameplayResultCode.ForeignItem, (await fixture.ShopService().SellAsync(Command(), fixture.ActorOne, Shop, foreign.Id, default)).Code);
        Equal(GameplayResultCode.ForeignItem, (await fixture.Warehouse.DepositAsync(Command(), fixture.ActorOne, foreign.Id, default)).Code);
        Equal(ItemContainer.Inventory, fixture.Store.Item(foreign.Id).Location.Container);
    }

    private static async Task WarehouseCapacityIsAtomic()
    {
        var fixture = Fixture(warehouseCapacity: 1);
        var first = fixture.Store.SeedItem(AccountOne, CharacterOne, SwordId, ItemLocation.Inventory());
        var second = fixture.Store.SeedItem(AccountOne, CharacterOne, SwordId, ItemLocation.Inventory());
        Equal(GameplayResultCode.Success, (await fixture.Warehouse.DepositAsync(Command(), fixture.ActorOne, first.Id, default)).Code);
        Equal(GameplayResultCode.WarehouseFull, (await fixture.Warehouse.DepositAsync(Command(), fixture.ActorOne, second.Id, default)).Code);
        Equal(ItemContainer.Inventory, fixture.Store.Item(second.Id).Location.Container);
    }

    private static async Task FailedCommitRollsBackCurrencyAndItems()
    {
        var fixture = Fixture(silver: 1_000);
        fixture.Store.FailNextCommit = true;
        await ThrowsAsync<InjectedCommitException>(() => fixture.ShopService()
            .BuyAsync(Command(), fixture.ActorOne, Shop, SwordId, 1, default).AsTask());
        Equal(1_000L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
        Equal(0, fixture.Store.Items(CharacterOne).Count);
    }

    private static async Task CurrencyCommandsAreIdempotentAndOwnershipBound()
    {
        var fixture = Fixture(silver: 100);
        var service = new CurrencyService(fixture.Store);
        var command = Command();
        Equal(GameplayResultCode.Success, (await service.AdjustAsync(command, fixture.ActorOne, CurrencyKind.Silver, -50, default)).Code);
        Equal(true, (await service.AdjustAsync(command, fixture.ActorOne, CurrencyKind.Silver, -50, default)).Replayed);
        Equal(50L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
        var wrongActor = new GameplayActor(AccountTwo, CharacterOne);
        Equal(GameplayResultCode.OwnershipMismatch, (await service.AdjustAsync(Command(), wrongActor, CurrencyKind.Silver, 10, default)).Code);
        Equal(50L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
    }

    private static async Task EquipmentRequirementsAndSlotsAreEnforced()
    {
        var fixture = Fixture(level: 10);
        var first = fixture.Store.SeedItem(AccountOne, CharacterOne, SwordId, ItemLocation.Inventory());
        var second = fixture.Store.SeedItem(AccountOne, CharacterOne, SwordId, ItemLocation.Inventory());
        Equal(GameplayResultCode.Success, (await fixture.Equipment.EquipAsync(Command(), fixture.ActorOne, first.Id, default)).Code);
        Equal(GameplayResultCode.EquipmentSlotOccupied, (await fixture.Equipment.EquipAsync(Command(), fixture.ActorOne, second.Id, default)).Code);

        var lowLevel = Fixture(level: 1);
        var item = lowLevel.Store.SeedItem(AccountOne, CharacterOne, SwordId, ItemLocation.Inventory());
        Equal(GameplayResultCode.RequirementsNotMet, (await lowLevel.Equipment.EquipAsync(Command(), lowLevel.ActorOne, item.Id, default)).Code);
    }

    private static async Task NinjaDualKatanaSlotsAreAuthoritative()
    {
        var katanaId = new ItemDefinitionId(601_000);
        var definition = new ItemDefinition(katanaId, "MetalKatana", 1_599, EquipmentSlot.RightHand,
            new ItemRequirements(MinimumLevel: 40, Profession: 12),
            new ItemStatBlock(MinimumAttack: 10, MaximumAttack: 20));
        var store = new InMemoryGameplayStore();
        store.SeedCharacter(new CharacterGameplayState(AccountOne, CharacterOne, 40, 40,
            level: 120, profession: 55, strength: 50, agility: 50,
            weaponProficiencies: new Dictionary<ushort, byte> { [601] = 12 }));
        store.SeedCharacter(new CharacterGameplayState(AccountTwo, CharacterTwo, 40, 40,
            level: 120, profession: 10, strength: 50, agility: 50));
        var service = new EquipmentService(store, new ItemCatalog([definition]),
            new DefaultEquipmentRequirementPolicy());
        ItemInstance right = store.SeedItem(AccountOne, CharacterOne, katanaId, ItemLocation.Inventory());
        ItemInstance left = store.SeedItem(AccountOne, CharacterOne, katanaId, ItemLocation.Inventory());
        ItemInstance warrior = store.SeedItem(AccountTwo, CharacterTwo, katanaId, ItemLocation.Inventory());

        Equal(GameplayResultCode.Success, (await service.EquipAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), right.Id, EquipmentSlot.RightHand, default)).Code);
        Equal(GameplayResultCode.Success, (await service.EquipAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), left.Id, EquipmentSlot.LeftHand, default)).Code);
        Equal(ItemLocation.Equipped(EquipmentSlot.RightHand), store.Item(right.Id).Location);
        Equal(ItemLocation.Equipped(EquipmentSlot.LeftHand), store.Item(left.Id).Location);
        Equal(GameplayResultCode.InvalidLocation, (await service.EquipAsync(Command(),
            new GameplayActor(AccountTwo, CharacterTwo), warrior.Id, EquipmentSlot.LeftHand, default)).Code);
        Equal(GameplayResultCode.InvalidLocation, (await service.UnequipAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), right.Id, default)).Code);
        Equal(GameplayResultCode.Success, (await service.UnequipAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), left.Id, default)).Code);
        Equal(GameplayResultCode.Success, (await service.UnequipAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), right.Id, default)).Code);
        var warehouse = new WarehouseService(store);
        Equal(GameplayResultCode.Success, (await warehouse.DepositAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), left.Id, default)).Code);
        Equal(GameplayResultCode.Success, (await warehouse.WithdrawAsync(Command(),
            new GameplayActor(AccountOne, CharacterOne), left.Id, default)).Code);
        Equal(ItemContainer.Inventory, store.Item(left.Id).Location.Container);
    }

    private static void EquipmentStatsAreDeterministic()
    {
        var calculator = new AdditiveEquipmentStatCalculator();
        var definitions = new[]
        {
            Definition() with { BaseStats = new ItemStatBlock(MinimumAttack: 1, MaximumAttack: 2, Defense: 3) },
            Definition() with { Id = new ItemDefinitionId(410_004), BaseStats = new ItemStatBlock(MinimumAttack: 4, MaximumAttack: 5, HitPoints: 6) }
        };
        Equal(new ItemStatBlock(5, 7, 3, HitPoints: 6), calculator.Calculate(definitions));
    }

    private static async Task InvalidShopReferenceDataCannotMintCurrency()
    {
        var store = new InMemoryGameplayStore();
        store.SeedCharacter(new CharacterGameplayState(
            AccountOne,
            CharacterOne,
            40,
            40,
            balances: new Dictionary<CurrencyKind, long> { [CurrencyKind.Silver] = 100 }));
        var definitions = new ItemCatalog(new[] { Definition() });
        var invalidEntry = new ShopEntry(SwordId, CurrencyKind.Silver, -10, 0);
        var shops = new ShopCatalog(new[]
        {
            new ShopDefinition(Shop, new Dictionary<ItemDefinitionId, ShopEntry> { [SwordId] = invalidEntry })
        });
        var service = new ShopService(store, definitions, shops, new SequentialItemIds());
        var result = await service.BuyAsync(Command(), new GameplayActor(AccountOne, CharacterOne), Shop, SwordId, 1, default);
        Equal(GameplayResultCode.InvalidReferenceData, result.Code);
        Equal(100L, store.Character(CharacterOne).Balance(CurrencyKind.Silver));
        Equal(0, store.Items(CharacterOne).Count);
    }

    private static async Task OwnerCanaryGrantAndRollbackAreAtomicAndReplaySafe()
    {
        var fixture = Fixture(silver: 100);
        var service = new OwnerCanaryGrantService(fixture.Store, fixture.Definitions, fixture.ItemIds);
        var grantId = Command("c1000000-0000-0000-0000-000000000001");
        GameplayCommandResult granted = await service.GrantAsync(grantId, fixture.ActorOne, SwordId, 500, default);
        Equal(GameplayResultCode.Success, granted.Code);
        Equal(true, (await service.GrantAsync(grantId, fixture.ActorOne, SwordId, 500, default)).Replayed);
        Equal(600L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
        Equal(1, fixture.Store.Items(CharacterOne).Count);
        var rollbackId = Command("c1000000-0000-0000-0000-000000000002");
        Equal(GameplayResultCode.Success, (await service.RollbackAsync(rollbackId, fixture.ActorOne,
            granted.ItemInstanceId!.Value, 500, default)).Code);
        Equal(true, (await service.RollbackAsync(rollbackId, fixture.ActorOne,
            granted.ItemInstanceId.Value, 500, default)).Replayed);
        Equal(100L, fixture.Store.Character(CharacterOne).Balance(CurrencyKind.Silver));
        Equal(0, fixture.Store.Items(CharacterOne).Count);
    }

    private static FixtureState Fixture(
        int inventoryCapacity = 40,
        int warehouseCapacity = 40,
        long silver = 10_000,
        byte level = 20)
    {
        var store = new InMemoryGameplayStore();
        store.SeedCharacter(new CharacterGameplayState(
            AccountOne,
            CharacterOne,
            inventoryCapacity,
            warehouseCapacity,
            level,
            profession: 10,
            strength: 50,
            agility: 50,
            balances: new Dictionary<CurrencyKind, long> { [CurrencyKind.Silver] = silver },
            weaponProficiencies: new Dictionary<ushort, byte> { [410] = 10 }));
        store.SeedCharacter(new CharacterGameplayState(AccountTwo, CharacterTwo, 40, 40));
        var definitions = new ItemCatalog(new[] { Definition() });
        var shopEntry = new ShopEntry(SwordId, CurrencyKind.Silver, 100, 25);
        var shops = new ShopCatalog(
            new[] { new ShopDefinition(Shop, new Dictionary<ItemDefinitionId, ShopEntry> { [SwordId] = shopEntry }) });
        var ids = new SequentialItemIds();
        return new FixtureState(
            store,
            definitions,
            shops,
            ids,
            new GameplayActor(AccountOne, CharacterOne),
            new EquipmentService(store, definitions, new DefaultEquipmentRequirementPolicy()),
            new WarehouseService(store));
    }

    private static ItemDefinition Definition() => new(
        SwordId,
        "TrainingSword",
        100,
        EquipmentSlot.RightHand,
        new ItemRequirements(MinimumLevel: 5, Profession: 10, Strength: 10),
        new ItemStatBlock(MinimumAttack: 5, MaximumAttack: 10));

    private static GameplayCommandId Command(string? value = null) =>
        new(value is null ? Guid.NewGuid() : Guid.Parse(value));

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }

    private static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed record FixtureState(
        InMemoryGameplayStore Store,
        ItemCatalog Definitions,
        ShopCatalog Shops,
        SequentialItemIds ItemIds,
        GameplayActor ActorOne,
        EquipmentService Equipment,
        WarehouseService Warehouse)
    {
        public ShopService ShopService() => new(Store, Definitions, Shops, ItemIds);
    }

    private sealed class ItemCatalog(IEnumerable<ItemDefinition> values) : IItemDefinitionCatalog
    {
        private readonly Dictionary<ItemDefinitionId, ItemDefinition> entries = values.ToDictionary(item => item.Id);
        public ItemDefinition? Find(ItemDefinitionId id) => entries.GetValueOrDefault(id);
    }

    private sealed class ShopCatalog(IEnumerable<ShopDefinition> values) : IShopCatalog
    {
        private readonly Dictionary<ShopId, ShopDefinition> entries = values.ToDictionary(shop => shop.Id);
        public ShopDefinition? Find(ShopId id) => entries.GetValueOrDefault(id);
    }

    private sealed class SequentialItemIds : IItemInstanceIdGenerator
    {
        private uint current = 10_000_000;
        public ItemIdentity Next()
        {
            uint value = current++;
            return new ItemIdentity(
                new ItemInstanceId(new Guid(checked((int)value), 0, 0, new byte[8])),
                new LegacyItemUid(value));
        }
    }

    private sealed class InjectedCommitException : Exception;

    private sealed class InMemoryGameplayStore : IGameplayStore
    {
        private Dictionary<CharacterUid, CharacterGameplayState> characters = [];
        private Dictionary<ItemInstanceId, ItemInstance> items = [];
        private Dictionary<GameplayCommandId, GameplayCommandReceipt> receipts = [];
        private uint nextItemId = 50_000_000;

        public bool FailNextCommit { get; set; }

        public ValueTask<TResult> ExecuteAsync<TResult>(Func<IGameplayTransaction, TResult> operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = new Transaction(characters, items, receipts);
            TResult result = operation(transaction);
            if (FailNextCommit)
            {
                FailNextCommit = false;
                throw new InjectedCommitException();
            }
            characters = transaction.Characters;
            items = transaction.Items;
            receipts = transaction.Receipts;
            return ValueTask.FromResult(result);
        }

        public void SeedCharacter(CharacterGameplayState character) => characters.Add(character.CharacterUid, character);

        public ItemInstance SeedItem(AccountId accountId, CharacterUid characterUid, ItemDefinitionId definitionId, ItemLocation location)
        {
            uint value = nextItemId++;
            var item = new ItemInstance(
                new ItemInstanceId(new Guid(checked((int)value), 0, 0, new byte[8])),
                new LegacyItemUid(value),
                definitionId,
                accountId,
                characterUid,
                location,
                100);
            items.Add(item.Id, item);
            return item;
        }

        public CharacterGameplayState Character(CharacterUid id) => characters[id];
        public ItemInstance Item(ItemInstanceId id) => items[id];
        public IReadOnlyList<ItemInstance> Items(CharacterUid id) => items.Values.Where(item => item.CharacterUid == id).ToArray();

        private sealed class Transaction : IGameplayTransaction
        {
            public Transaction(
                Dictionary<CharacterUid, CharacterGameplayState> characters,
                Dictionary<ItemInstanceId, ItemInstance> items,
                Dictionary<GameplayCommandId, GameplayCommandReceipt> receipts)
            {
                Characters = new Dictionary<CharacterUid, CharacterGameplayState>(characters);
                Items = new Dictionary<ItemInstanceId, ItemInstance>(items);
                Receipts = new Dictionary<GameplayCommandId, GameplayCommandReceipt>(receipts);
            }

            public Dictionary<CharacterUid, CharacterGameplayState> Characters { get; }
            public Dictionary<ItemInstanceId, ItemInstance> Items { get; }
            public Dictionary<GameplayCommandId, GameplayCommandReceipt> Receipts { get; }
            public CharacterGameplayState? FindCharacter(CharacterUid id) => Characters.GetValueOrDefault(id);
            public ItemInstance? FindItem(ItemInstanceId id) => Items.GetValueOrDefault(id);
            public IReadOnlyList<ItemInstance> GetItems(CharacterUid id) => Items.Values.Where(item => item.CharacterUid == id).ToArray();
            public GameplayCommandReceipt? FindReceipt(GameplayCommandId id) => Receipts.GetValueOrDefault(id);
            public void SaveCharacter(CharacterGameplayState character) => Characters[character.CharacterUid] = character;
            public void AddItem(ItemInstance item) => Items.Add(item.Id, item);
            public void SaveItem(ItemInstance item) => Items[item.Id] = item;
            public void RemoveItem(ItemInstanceId id) => Items.Remove(id);
            public void SaveReceipt(GameplayCommandReceipt receipt) => Receipts.Add(receipt.CommandId, receipt);
        }
    }
}
