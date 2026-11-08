using System.Buffers.Binary;
using System.Security.Cryptography;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;

namespace Revora.Host.Gameplay;

public sealed class ItemEconomyRuntime5165 : IItemDefinitionCatalog, IShopCatalog, IItemInstanceIdGenerator,
    IAsyncDisposable
{
    private readonly PostgreSqlEconomyRuntime persistence;
    private readonly IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> items;
    private readonly IReadOnlyDictionary<ShopId, ShopDefinition> shops;
    private readonly IReadOnlyDictionary<uint, WarehouseDefinition> warehouses;
    private readonly InventoryService inventory;
    private readonly EquipmentService equipment;
    private readonly ShopService commerce;
    private readonly WarehouseService warehouse;
    private readonly PostgreSqlProgressionRuntime progression;

    private ItemEconomyRuntime5165(PostgreSqlEconomyRuntime persistence, PostgreSqlProgressionRuntime progression,
        EconomyCatalogSnapshot catalog)
    {
        this.persistence = persistence;
        this.progression = progression;
        items = catalog.Items;
        shops = catalog.Shops;
        warehouses = catalog.Warehouses;
        inventory = new(persistence.Store);
        equipment = new(persistence.Store, this, new DefaultEquipmentRequirementPolicy());
        commerce = new(persistence.Store, this, this, this);
        warehouse = new(persistence.Store);
    }

    public static async Task<ItemEconomyRuntime5165> CreateAsync(string connectionString,
        CancellationToken cancellationToken)
    {
        var persistence = new PostgreSqlEconomyRuntime(connectionString);
        var progression = new PostgreSqlProgressionRuntime(connectionString);
        try
        {
            EconomyCatalogSnapshot catalog = await persistence.LoadCatalogAsync(cancellationToken);
            if (catalog.Items.Count != 8_956 || catalog.Shops.Count != 27 ||
                catalog.Shops.Sum(row => row.Value.Entries.Count) != 1_094)
                throw new InvalidDataException("The active item/economy catalog does not match the reviewed canonical release.");
            return new(persistence, progression, catalog);
        }
        catch
        {
            await progression.DisposeAsync();
            await persistence.DisposeAsync();
            throw;
        }
    }

    public int ItemDefinitionCount => items.Count;
    public int ShopCount => shops.Count;
    public int ShopItemCount => shops.Sum(row => row.Value.Entries.Count);
    public int WarehouseCount => warehouses.Count;

    public ItemDefinition? Find(ItemDefinitionId id) => items.GetValueOrDefault(id);
    public ShopDefinition? Find(ShopId id) => shops.GetValueOrDefault(id);
    public bool HasShopForNpc(uint npcUid) => shops.ContainsKey(new ShopId(npcUid));
    public bool HasWarehouseForNpc(uint npcUid) => TryFindWarehouse(npcUid, out _);
    public ItemIdentity Next() => persistence.NextItemIdentity();

    public async Task<ItemEconomySession5165> OpenSessionAsync(AccountId accountId, CharacterUid characterUid,
        CancellationToken cancellationToken)
    {
        var actor = new GameplayActor(accountId, characterUid);
        EconomyRolloutDecision rollout = await persistence.EvaluateRolloutAsync(actor, cancellationToken);
        return new(this, actor, rollout);
    }

    public async Task<ProgressionServiceSession5165> OpenProgressionSessionAsync(AccountId accountId,
        CharacterUid characterUid, CancellationToken cancellationToken) =>
        new(this, progression, accountId, characterUid,
            await progression.IsEnabledAsync(accountId, characterUid, cancellationToken));

    internal Task<GameplayPlayerSnapshot?> ReadPlayerAsync(GameplayActor actor, CancellationToken token) =>
        persistence.ReadPlayerAsync(actor, token);

    internal Task<ItemInstanceId?> ResolveOwnedItemAsync(GameplayActor actor, LegacyItemUid uid, CancellationToken token) =>
        persistence.ResolveOwnedItemAsync(actor, uid, token);

    internal Task EnsureWarehouseAsync(GameplayActor actor, WarehouseDefinition definition, CancellationToken token) =>
        persistence.EnsureWarehouseVaultAsync(actor, definition, token);

    internal Task PersistCalculatedStatsAsync(GameplayActor actor, CalculatedCharacterStats stats,
        CancellationToken token) => persistence.PersistCalculatedStatsAsync(actor, stats, token);

    internal bool TryFindWarehouse(uint npcUid, out WarehouseDefinition definition)
    {
        definition = warehouses.Values.SingleOrDefault(value => value.NpcUid == npcUid)!;
        return definition is not null;
    }

    internal InventoryService Inventory => inventory;
    internal EquipmentService Equipment => equipment;
    internal ShopService Commerce => commerce;
    internal WarehouseService Warehouse => warehouse;

    internal ItemDefinition RequireItemDefinition(uint itemType) =>
        Find(new ItemDefinitionId(itemType))?.Validate() ??
        throw new InvalidDataException("The item definition is not in the active reviewed release.");

    public async ValueTask DisposeAsync()
    {
        await progression.DisposeAsync();
        await persistence.DisposeAsync();
    }
}

public sealed class ItemEconomySession5165
{
    private readonly ItemEconomyRuntime5165 runtime;
    private readonly GameplayActor actor;
    private readonly Guid warehouseCommandNamespace = Guid.NewGuid();
    private readonly Dictionary<GameplayCommandId, IReadOnlyList<byte[]>> recoveryItemResponses = [];
    private readonly Queue<GameplayCommandId> recoveryItemResponseOrder = [];
    private ulong warehouseCommandSequence;

    internal ItemEconomySession5165(ItemEconomyRuntime5165 runtime, GameplayActor actor,
        EconomyRolloutDecision rollout)
    {
        this.runtime = runtime;
        this.actor = actor;
        Rollout = rollout;
    }

    public EconomyRolloutDecision Rollout { get; }
    public bool Enabled => Rollout.Enabled;
    public GameplayActor Actor => actor;

    public bool HasShopForNpc(uint npcUid) => Enabled && runtime.HasShopForNpc(npcUid);
    public bool HasWarehouseForNpc(uint npcUid) => Enabled && runtime.HasWarehouseForNpc(npcUid);

    public Task<GameplayPlayerSnapshot?> ReadAsync(CancellationToken token) => runtime.ReadPlayerAsync(actor, token);

    public async Task<GamePlayerAppearance5165> ReadAppearanceAsync(CancellationToken token)
    {
        GameplayPlayerSnapshot snapshot = await RequirePlayerAsync(token);
        uint headwear = 0, garment = 0, armor = 0, leftHand = 0, rightHand = 0, steed = 0;
        ushort headwearColor = 0, armorColor = 0, leftHandColor = 0;
        foreach (ItemInstance item in snapshot.Items.Where(value => value.Location.Container == ItemContainer.Equipment))
        {
            uint type = item.DefinitionId.Value;
            ushort color = item.Color;
            switch ((EquipmentSlot)item.Location.Slot)
            {
                case EquipmentSlot.Headwear: headwear = type; headwearColor = color; break;
                case EquipmentSlot.Garment: garment = type; break;
                case EquipmentSlot.Armor: armor = type; armorColor = color; break;
                case EquipmentSlot.LeftHand: leftHand = type; leftHandColor = color; break;
                case EquipmentSlot.RightHand: rightHand = type; break;
                case EquipmentSlot.Steed: steed = type; break;
            }
        }
        return new(Headwear: headwear, Garment: garment, Armor: armor,
            LeftHand: leftHand, RightHand: rightHand, Steed: steed, ArmorColor: armorColor,
            LeftHandColor: leftHandColor, HeadwearColor: headwearColor);
    }

    public async Task<IReadOnlyList<byte[]>> CurrentItemPacketsAsync(IEnumerable<uint> legacyItemUids,
        CancellationToken token)
    {
        HashSet<uint> requested = legacyItemUids.ToHashSet();
        GameplayPlayerSnapshot snapshot = await RequirePlayerAsync(token);
        ItemInstance[] items = snapshot.Items.Where(item => requested.Contains(item.LegacyUid.Value)).ToArray();
        if (items.Length != requested.Count)
            throw new InvalidDataException("A committed service item was not found in the authenticated inventory.");
        return items.OrderBy(item => item.Location.Slot)
            .Select(item => ItemEconomyPackets5165.EncodeItemInfo(item,
                runtime.RequireItemDefinition(item.DefinitionId.Value))).ToArray();
    }

    public async Task<IReadOnlyList<byte[]>> InitialPacketsAsync(CancellationToken token)
    {
        if (!Enabled) return [];
        GameplayPlayerSnapshot snapshot = await RequirePlayerAsync(token);
        var packets = snapshot.Items
            .Where(item => item.Location.Container is ItemContainer.Inventory or ItemContainer.Equipment)
            .OrderBy(item => item.Location.Container).ThenBy(item => item.Location.Slot)
            .Select(item => ItemEconomyPackets5165.EncodeItemInfo(item, RequireDefinition(item.DefinitionId)))
            .ToList();
        packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 4,
            checked((ulong)snapshot.Character.Balance(CurrencyKind.Silver))));
        packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 29,
            checked((ulong)snapshot.Character.Balance(CurrencyKind.ConquerPoints))));
        return packets;
    }

    public async Task<IReadOnlyList<byte[]>> HandleItemActionAsync(ReadOnlyMemory<byte> frame,
        Func<uint, bool> canInteractWithNpc, bool itemUseSuppressed,
        Func<Guid, uint, uint, int, int, CancellationToken, Task<RecoveryItemPersistenceResult>> consumeRecoveryItem,
        CancellationToken token)
    {
        if (!Enabled) return [Error("This gameplay feature is not enabled for this character.")];
        ItemAction5165 action = ItemEconomyPackets5165.ParseItemAction(frame.Span);
        if (action.Action == ItemEconomyPackets5165.Ping)
        {
            // Exact client receive handler 0x537da5 computes unsigned timeGetTime() - packet[+16].
            // Echo its original monotonic send tick unchanged; a server clock cannot participate in RTT.
            return
            [
                ItemEconomyPackets5165.EncodeItemAction(action.Subject, action.Command,
                    ItemEconomyPackets5165.Ping, action.Timestamp),
            ];
        }
        GameplayCommandId commandId = CommandId(frame.Span);
        if (recoveryItemResponses.TryGetValue(commandId, out IReadOnlyList<byte[]>? replayedRecovery))
            return replayedRecovery.Select(packet => packet.ToArray()).ToArray();
        GameplayPlayerSnapshot before = await RequirePlayerAsync(token);
        switch (action.Action)
        {
            case ItemEconomyPackets5165.ShopBuy:
            {
                var shopId = new ShopId(action.Subject);
                if (!canInteractWithNpc(action.Subject) || runtime.Find(shopId) is null)
                    return [Error("The shop is not available from this position.")];
                int quantity = checked((byte)action.Argument);
                if (quantity == 0) quantity = 1;
                GameplayCommandResult result = await runtime.Commerce.BuyAsync(commandId, actor, shopId,
                    new ItemDefinitionId(action.Command), quantity, token);
                return await MutationPacketsAsync(result, before, token);
            }
            case ItemEconomyPackets5165.ShopSell:
            {
                var shopId = new ShopId(action.Subject);
                if (!canInteractWithNpc(action.Subject) || runtime.Find(shopId) is null)
                    return [Error("The shop is not available from this position.")];
                ItemInstanceId item = await RequireOwnedItemAsync(action.Command, token);
                GameplayCommandResult result = await runtime.Commerce.SellAsync(commandId, actor, shopId, item, token);
                return await MutationPacketsAsync(result, before, token);
            }
            case ItemEconomyPackets5165.Equip:
            {
                if (action.Command == 0)
                {
                    ItemInstance? recoveryItem = before.Items.SingleOrDefault(value =>
                        value.LegacyUid.Value == action.Subject &&
                        value.Location.Container == ItemContainer.Inventory);
                    if (recoveryItem is null)
                        return [Error("That recovery item is no longer available.")];
                    ItemDefinition recoveryDefinition = RequireDefinition(recoveryItem.DefinitionId);
                    int healthRecovery = recoveryDefinition.BaseStats.HitPoints;
                    int manaRecovery = recoveryDefinition.BaseStats.Mana;
                    if (recoveryDefinition.EquipmentSlot is not null ||
                        !IsReviewedRecoveryItem(recoveryItem.DefinitionId.Value) ||
                        healthRecovery <= 0 && manaRecovery <= 0)
                        return [Error("That item action is not available.")];
                    if (itemUseSuppressed)
                        return [Error("You cannot use recovery items while Poison Star is active.")];
                    RecoveryItemPersistenceResult consumed = await consumeRecoveryItem(commandId.Value,
                        action.Subject, recoveryItem.DefinitionId.Value, healthRecovery, manaRecovery, token);
                    if (!consumed.Succeeded)
                        return [Error(consumed.Code == RecoveryItemPersistenceCode.CharacterDead
                            ? "You cannot use that item while dead."
                            : "That recovery item could not be used.")];
                    var packets = new List<byte[]>();
                    if (healthRecovery > 0)
                        packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 0,
                            checked((ulong)consumed.CurrentHealth)));
                    if (manaRecovery > 0)
                        packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 2,
                            checked((ulong)consumed.CurrentMana)));
                    packets.Add(ItemEconomyPackets5165.EncodeItemAction(action.Subject, 0,
                        ItemEconomyPackets5165.InventoryRemove));
                    CacheRecoveryResponse(commandId, packets);
                    return packets;
                }
                ItemInstanceId itemId = await RequireOwnedItemAsync(action.Subject, token);
                ItemInstance item = before.Items.Single(value => value.Id == itemId);
                ItemDefinition definition = RequireDefinition(item.DefinitionId);
                if (definition.EquipmentSlot is null ||
                    action.Command != (uint)definition.EquipmentSlot.Value &&
                    !NinjaItemRules.CanEquipKatana(before.Character.Profession,
                        checked((EquipmentSlot)action.Command), definition.Id.Value))
                    return [Error("That item cannot be equipped in the requested slot.")];
                GameplayCommandResult result = await runtime.Equipment.EquipAsync(commandId, actor, itemId,
                    checked((EquipmentSlot)action.Command), token);
                return await MutationPacketsAsync(result, before, token);
            }
            case ItemEconomyPackets5165.Unequip:
            {
                ItemInstanceId itemId = await RequireOwnedItemAsync(action.Subject, token);
                GameplayCommandResult result = await runtime.Equipment.UnequipAsync(commandId, actor, itemId, token);
                return await MutationPacketsAsync(result, before, token);
            }
            case ItemEconomyPackets5165.WarehouseOpen:
            {
                if (!canInteractWithNpc(action.Subject) || !runtime.TryFindWarehouse(action.Subject, out WarehouseDefinition warehouse))
                    return [Error("The warehouse is not available from this position.")];
                await runtime.EnsureWarehouseAsync(actor, warehouse, token);
                return
                [
                    // Revora has not activated warehouse-money banking. The exact 5165 field is
                    // warehouse savings, not carried silver, so its authoritative safe value is zero.
                    ItemEconomyPackets5165.EncodeWarehouseOpen(action.Subject, 0),
                ];
            }
            default:
                return [Error("That item action is not available.")];
        }
    }

    public async Task<IReadOnlyList<byte[]>> HandleWarehouseActionAsync(ReadOnlyMemory<byte> frame,
        Func<uint, bool> canInteractWithNpc, CancellationToken token)
    {
        if (!Enabled) return [Error("This gameplay feature is not enabled for this character.")];
        WarehouseAction5165 action = ItemEconomyPackets5165.ParseWarehouseAction(frame.Span);
        if (!canInteractWithNpc(action.NpcUid) || !runtime.TryFindWarehouse(action.NpcUid, out WarehouseDefinition warehouse))
            return [Error("The warehouse is not available from this position.")];
        await runtime.EnsureWarehouseAsync(actor, warehouse, token);
        if (action.Action == 0)
        {
            GameplayPlayerSnapshot current = await RequirePlayerAsync(token);
            ItemInstance[] items = current.Items
                .Where(item => item.Location.Container == ItemContainer.Warehouse)
                .ToArray();
            // The exact 5165 client requests contents after accepting 20/1009. The reviewed donor
            // returns no 1102 list for an empty vault; a non-empty vault is returned at this point.
            return items.Length == 0
                ? []
                : [ItemEconomyPackets5165.EncodeWarehouseList(action.NpcUid, items, runtime)];
        }

        if (action.Action is not (1 or 2)) return [Error("That warehouse action is not available.")];

        GameplayPlayerSnapshot before = await RequirePlayerAsync(token);
        ItemInstanceId itemId = await RequireOwnedItemAsync(action.ItemUid, token);
        ItemInstance currentItem = before.Items.Single(item => item.Id == itemId);
        if (action.Action == 1 && currentItem.Location.Container == ItemContainer.Warehouse)
        {
            // A duplicate deposit is a client-correction operation, not another mutation. This also
            // avoids relying on packet bytes as a lifetime-global receipt key: the exact client emits
            // identical 1102 bytes again after a later legitimate withdraw/deposit cycle.
            return
            [
                ItemEconomyPackets5165.EncodeItemAction(action.ItemUid, 0, ItemEconomyPackets5165.InventoryRemove),
                ItemEconomyPackets5165.EncodeWarehouseLiveAdd(action.NpcUid, currentItem, runtime),
            ];
        }
        if (action.Action == 2 && currentItem.Location.Container == ItemContainer.Inventory)
        {
            return
            [
                ItemEconomyPackets5165.EncodeWarehouseLiveRemove(action.NpcUid, currentItem),
                ItemEconomyPackets5165.EncodeItemInfo(currentItem, RequireDefinition(currentItem.DefinitionId)),
            ];
        }
        if ((action.Action == 1 && currentItem.Location.Container != ItemContainer.Inventory) ||
            (action.Action == 2 && currentItem.Location.Container != ItemContainer.Warehouse))
            return [Error("That item is not in the expected container.")];

        GameplayCommandId commandId = NextWarehouseCommandId(frame.Span);
        GameplayCommandResult result = action.Action == 1
            ? await runtime.Warehouse.DepositAsync(commandId, actor, itemId, token)
            : await runtime.Warehouse.WithdrawAsync(commandId, actor, itemId, token);
        if (!result.Succeeded) return [Error(ResultMessage(result.Code))];
        GameplayPlayerSnapshot after = await RequirePlayerAsync(token);
        ItemInstance changed = after.Items.Single(value => value.Id == itemId);
        var packets = new List<byte[]>();
        if (action.Action == 1)
        {
            // Exact 5165 donor order: remove the inventory copy before adding the same immutable
            // Item UID to the warehouse window.
            packets.Add(ItemEconomyPackets5165.EncodeItemAction(action.ItemUid, 0, ItemEconomyPackets5165.InventoryRemove));
            packets.Add(ItemEconomyPackets5165.EncodeWarehouseLiveAdd(action.NpcUid, changed, runtime));
        }
        else
        {
            packets.Add(ItemEconomyPackets5165.EncodeWarehouseLiveRemove(action.NpcUid, changed));
            packets.Add(ItemEconomyPackets5165.EncodeItemInfo(changed, RequireDefinition(changed.DefinitionId)));
        }
        return packets;
    }

    private GameplayCommandId NextWarehouseCommandId(ReadOnlySpan<byte> frame)
    {
        Span<byte> input = stackalloc byte[44 + frame.Length];
        actor.AccountId.Value.TryWriteBytes(input);
        BinaryPrimitives.WriteUInt32LittleEndian(input[16..], actor.CharacterUid.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(input[20..], checked(++warehouseCommandSequence));
        warehouseCommandNamespace.TryWriteBytes(input[28..]);
        frame.CopyTo(input[44..]);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        var result = new GameplayCommandId(new Guid(digest[..16]));
        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(digest);
        return result;
    }

    private void CacheRecoveryResponse(GameplayCommandId commandId, IReadOnlyList<byte[]> packets)
    {
        recoveryItemResponses[commandId] = packets.Select(packet => packet.ToArray()).ToArray();
        recoveryItemResponseOrder.Enqueue(commandId);
        while (recoveryItemResponseOrder.Count > 64)
            recoveryItemResponses.Remove(recoveryItemResponseOrder.Dequeue());
    }

    private static bool IsReviewedRecoveryItem(uint itemType) => itemType is
        1_000_000 or 1_000_010 or 1_000_020 or 1_000_030 or
        1_001_000 or 1_001_010 or 1_001_020 or 1_001_030 or 1_001_040 or
        1_002_000 or 1_002_010 or 1_002_020 or 1_002_030 or 1_002_040 or 1_002_050;

    private async Task<IReadOnlyList<byte[]>> MutationPacketsAsync(GameplayCommandResult result,
        GameplayPlayerSnapshot before, CancellationToken token)
    {
        if (!result.Succeeded) return [Error(ResultMessage(result.Code))];
        GameplayPlayerSnapshot after = await RequirePlayerAsync(token);
        var packets = new List<byte[]>();
        Dictionary<ItemInstanceId, ItemInstance> oldItems = before.Items.ToDictionary(item => item.Id);
        Dictionary<ItemInstanceId, ItemInstance> newItems = after.Items.ToDictionary(item => item.Id);
        foreach (ItemInstance removed in before.Items.Where(item => !newItems.ContainsKey(item.Id)))
            packets.Add(ItemEconomyPackets5165.EncodeItemAction(removed.LegacyUid.Value, 0,
                ItemEconomyPackets5165.InventoryRemove));
        foreach (ItemInstance item in after.Items.Where(item => !oldItems.TryGetValue(item.Id, out ItemInstance? old) || old != item))
        {
            ItemDefinition definition = RequireDefinition(item.DefinitionId);
            if (!oldItems.TryGetValue(item.Id, out ItemInstance? old))
            {
                packets.Add(ItemEconomyPackets5165.EncodeItemInfo(item, definition));
                continue;
            }

            if (old.Location.Container == ItemContainer.Inventory &&
                item.Location.Container == ItemContainer.Equipment)
            {
                // Exact 5165 sequence: remove from inventory, add at the equipment position,
                // then issue the equipment item update at that same position.
                packets.Add(ItemEconomyPackets5165.EncodeItemAction(item.LegacyUid.Value, 0,
                    ItemEconomyPackets5165.InventoryRemove));
                packets.Add(ItemEconomyPackets5165.EncodeItemInfo(item, definition));
                packets.Add(ItemEconomyPackets5165.EncodeItemInfo(item, definition, action: 3));
                continue;
            }

            if (old.Location.Container == ItemContainer.Equipment &&
                item.Location.Container == ItemContainer.Inventory)
            {
                // Add back to inventory with Position zero before clearing the old equipment slot.
                packets.Add(ItemEconomyPackets5165.EncodeItemInfo(item, definition));
                packets.Add(ItemEconomyPackets5165.EncodeItemAction(item.LegacyUid.Value,
                    old.Location.Slot, ItemEconomyPackets5165.Unequip));
                continue;
            }

            packets.Add(ItemEconomyPackets5165.EncodeItemInfo(item, definition));
        }
        foreach (CurrencyKind currency in new[] { CurrencyKind.Silver, CurrencyKind.ConquerPoints })
            if (before.Character.Balance(currency) != after.Character.Balance(currency))
                packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value,
                    currency == CurrencyKind.Silver ? 4u : 29u,
                    checked((ulong)after.Character.Balance(currency))));
        bool equipmentChanged = before.Items.Where(item => item.Location.Container == ItemContainer.Equipment)
            .OrderBy(item => item.LegacyUid.Value).ToArray() is var oldEquipment &&
            after.Items.Where(item => item.Location.Container == ItemContainer.Equipment)
                .OrderBy(item => item.LegacyUid.Value).ToArray() is var newEquipment &&
            !oldEquipment.SequenceEqual(newEquipment);
        if (equipmentChanged)
        {
            ItemStatBlock equipmentStats = new AdditiveEquipmentStatCalculator().Calculate(
                newEquipment.Select(item => RequireDefinition(item.DefinitionId)));
            int maximumHealth = Math.Max(100, checked(after.Character.Vitality * 24 +
                after.Character.Strength * 3 + after.Character.Agility * 3 +
                after.Character.Spirit * 3 + equipmentStats.HitPoints));
            int manaMultiplier = after.Character.Profession is >= 130 and <= 145 ? 15 : 5;
            int maximumMana = checked(after.Character.Spirit * manaMultiplier + equipmentStats.Mana);
            var calculated = new CalculatedCharacterStats(maximumHealth, maximumMana, equipmentStats);
            await runtime.PersistCalculatedStatsAsync(actor, calculated, token);
            packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 1,
                checked((ulong)maximumHealth)));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(actor.CharacterUid.Value, 3,
                checked((ulong)maximumMana)));
        }
        if (result.Replayed && result.ItemInstanceId is { } replayedId &&
            newItems.TryGetValue(replayedId, out ItemInstance? replayedItem))
            packets.Add(ItemEconomyPackets5165.EncodeItemInfo(replayedItem, RequireDefinition(replayedItem.DefinitionId)));
        if (packets.Count == 0 && !result.Replayed)
            packets.Add(Error("The request completed without a client-visible change."));
        return packets;
    }

    private async Task<GameplayPlayerSnapshot> RequirePlayerAsync(CancellationToken token) =>
        await runtime.ReadPlayerAsync(actor, token) ?? throw new InvalidDataException("The authenticated gameplay owner was not found.");

    private async Task<ItemInstanceId> RequireOwnedItemAsync(uint legacyUid, CancellationToken token) =>
        await runtime.ResolveOwnedItemAsync(actor, new LegacyItemUid(legacyUid), token) ??
        throw new InvalidDataException("The requested Item UID is not owned by the authenticated character.");

    private ItemDefinition RequireDefinition(ItemDefinitionId id) => runtime.Find(id)?.Validate() ??
        throw new InvalidDataException("The item definition is not in the active reviewed release.");

    private GameplayCommandId CommandId(ReadOnlySpan<byte> frame)
    {
        Span<byte> input = stackalloc byte[20 + frame.Length];
        actor.AccountId.Value.TryWriteBytes(input);
        BinaryPrimitives.WriteUInt32LittleEndian(input[16..], actor.CharacterUid.Value);
        frame.CopyTo(input[20..]);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        var result = new GameplayCommandId(new Guid(digest[..16]));
        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(digest);
        return result;
    }

    private static byte[] Error(string message) =>
        GamePacketsAfterAuthentication5165.EncodeTalk(2005, message);

    private static string ResultMessage(GameplayResultCode code) => code switch
    {
        GameplayResultCode.InventoryFull => "Your inventory is full.",
        GameplayResultCode.WarehouseFull => "Your warehouse is full.",
        GameplayResultCode.InsufficientFunds => "You do not have enough money.",
        GameplayResultCode.RequirementsNotMet => "You do not meet the item requirements.",
        GameplayResultCode.EquipmentSlotOccupied => "That equipment slot is occupied.",
        GameplayResultCode.CommandConflict => "The request was rejected safely.",
        _ => "The item request could not be completed.",
    };
}
