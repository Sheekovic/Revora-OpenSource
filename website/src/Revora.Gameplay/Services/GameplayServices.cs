using System.Globalization;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

public sealed class InventoryService(IGameplayStore store)
{
    public ValueTask<GameplayCommandResult> MoveToInventoryAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint("inventory", actor, itemId.Value);
        return store.ExecuteAsync(transaction =>
        {
            if (Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var result = ValidateActor(transaction, actor, out var character);
            if (result is not null) return Record(transaction, commandId, fingerprint, result);
            var item = transaction.FindItem(itemId);
            if (item is null) return Record(transaction, commandId, fingerprint, Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return Record(transaction, commandId, fingerprint, Failure(GameplayResultCode.ForeignItem));
            if (Count(transaction, actor.CharacterUid, ItemContainer.Inventory) >= character!.InventoryCapacity)
                return Record(transaction, commandId, fingerprint, Failure(GameplayResultCode.InventoryFull));

            ushort slot = NextFreeSlot(transaction, actor.CharacterUid, ItemContainer.Inventory,
                checked((ushort)character.InventoryCapacity));
            transaction.SaveItem(item with { Location = ItemLocation.Inventory(slot) });
            return Record(transaction, commandId, fingerprint, Success(item.Id));
        }, cancellationToken);
    }

    internal static int Count(IGameplayTransaction transaction, CharacterUid characterUid, ItemContainer container) =>
        transaction.GetItems(characterUid).Count(item => item.Location.Container == container);

    internal static ushort NextFreeSlot(IGameplayTransaction transaction, CharacterUid characterUid,
        ItemContainer container, ushort capacity)
    {
        HashSet<ushort> used = transaction.GetItems(characterUid)
            .Where(item => item.Location.Container == container)
            .Select(item => item.Location.Slot)
            .ToHashSet();
        for (ushort slot = 0; slot < capacity; slot++)
            if (!used.Contains(slot)) return slot;
        throw new InvalidOperationException("The validated container has no free slot.");
    }

    internal static GameplayCommandResult? ValidateActor(
        IGameplayTransaction transaction,
        GameplayActor actor,
        out CharacterGameplayState? character)
    {
        character = transaction.FindCharacter(actor.CharacterUid);
        if (character is null) return Failure(GameplayResultCode.CharacterNotFound);
        return character.AccountId == actor.AccountId ? null : Failure(GameplayResultCode.OwnershipMismatch);
    }

    internal static bool Replay(
        IGameplayTransaction transaction,
        GameplayCommandId commandId,
        string fingerprint,
        out GameplayCommandResult result)
    {
        var receipt = transaction.FindReceipt(commandId);
        if (receipt is null)
        {
            result = default!;
            return false;
        }

        result = receipt.Fingerprint == fingerprint
            ? receipt.Result.AsReplay()
            : Failure(GameplayResultCode.CommandConflict);
        return true;
    }

    internal static GameplayCommandResult Record(
        IGameplayTransaction transaction,
        GameplayCommandId commandId,
        string fingerprint,
        GameplayCommandResult result)
    {
        transaction.SaveReceipt(new GameplayCommandReceipt(commandId, fingerprint, result));
        return result;
    }

    internal static GameplayCommandResult Success(ItemInstanceId? itemId = null) =>
        new(GameplayResultCode.Success, ItemInstanceId: itemId);

    internal static GameplayCommandResult Failure(GameplayResultCode code) => new(code);

    internal static string Fingerprint(string operation, GameplayActor actor, params object[] values) =>
        string.Join('|', new[]
        {
            operation,
            actor.AccountId.Value.ToString("N", CultureInfo.InvariantCulture),
            actor.CharacterUid.Value.ToString(CultureInfo.InvariantCulture)
        }.Concat(values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)));
}

public sealed class OwnerCanaryGrantService(
    IGameplayStore store,
    IItemDefinitionCatalog definitions,
    IItemInstanceIdGenerator itemIds)
{
    public ValueTask<GameplayCommandResult> GrantAsync(GameplayCommandId commandId, GameplayActor actor,
        ItemDefinitionId definitionId, long silver, CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("owner-canary-grant", actor, definitionId.Value, silver);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out GameplayCommandResult replay)) return replay;
            GameplayCommandResult? validation = InventoryService.ValidateActor(transaction, actor, out CharacterGameplayState? character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            ItemDefinition? definition = definitions.Find(definitionId)?.Validate();
            if (definition is null)
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.DefinitionNotFound));
            if (silver <= 0)
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.InvalidQuantity));
            if (InventoryService.Count(transaction, actor.CharacterUid, ItemContainer.Inventory) >= character!.InventoryCapacity)
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.InventoryFull));
            long balance;
            try { balance = checked(character.Balance(CurrencyKind.Silver) + silver); }
            catch (OverflowException)
            {
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.CurrencyOverflow));
            }
            ItemIdentity identity = itemIds.Next();
            ushort slot = InventoryService.NextFreeSlot(transaction, actor.CharacterUid, ItemContainer.Inventory,
                checked((ushort)character.InventoryCapacity));
            transaction.SaveCharacter(character.WithBalance(CurrencyKind.Silver, balance));
            transaction.AddItem(new ItemInstance(identity.InstanceId, identity.LegacyUid, definition.Id,
                actor.AccountId, actor.CharacterUid, ItemLocation.Inventory(slot), definition.MaximumDurability));
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(identity.InstanceId));
        }, cancellationToken);
    }

    public ValueTask<GameplayCommandResult> RollbackAsync(GameplayCommandId commandId, GameplayActor actor,
        ItemInstanceId itemId, long silver, CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("owner-canary-rollback", actor, itemId.Value, silver);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out GameplayCommandResult replay)) return replay;
            GameplayCommandResult? validation = InventoryService.ValidateActor(transaction, actor, out CharacterGameplayState? character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            ItemInstance? item = transaction.FindItem(itemId);
            if (item is null) return InventoryService.Record(transaction, commandId, fingerprint,
                InventoryService.Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.ForeignItem));
            if (silver <= 0 || character!.Balance(CurrencyKind.Silver) < silver)
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.InsufficientFunds));
            transaction.RemoveItem(item.Id);
            transaction.SaveCharacter(character.WithBalance(CurrencyKind.Silver,
                character.Balance(CurrencyKind.Silver) - silver));
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(item.Id));
        }, cancellationToken);
    }
}

public sealed class CurrencyService(IGameplayStore store)
{
    public ValueTask<GameplayCommandResult> AdjustAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        CurrencyKind currency,
        long delta,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("currency", actor, (byte)currency, delta);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            long current = character!.Balance(currency);
            long next;
            try { next = checked(current + delta); }
            catch (OverflowException)
            {
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.CurrencyOverflow));
            }

            if (next < 0)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InsufficientFunds));
            transaction.SaveCharacter(character.WithBalance(currency, next));
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success());
        }, cancellationToken);
    }
}

public sealed class EquipmentService(
    IGameplayStore store,
    IItemDefinitionCatalog definitions,
    IEquipmentRequirementPolicy requirements)
{
    public ValueTask<GameplayCommandResult> EquipAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        CancellationToken cancellationToken) => EquipAsync(commandId, actor, itemId, null, cancellationToken);

    public ValueTask<GameplayCommandResult> EquipAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        EquipmentSlot? requestedSlot,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("equip", actor, itemId.Value,
            requestedSlot is null ? 0 : (byte)requestedSlot.Value);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            var item = transaction.FindItem(itemId);
            if (item is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ForeignItem));
            if (item.Location.Container != ItemContainer.Inventory)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            var definition = definitions.Find(item.DefinitionId);
            if (definition is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.DefinitionNotFound));
            if (definition.EquipmentSlot is not { } canonicalSlot)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            EquipmentSlot slot = requestedSlot ?? canonicalSlot;
            if (slot != canonicalSlot && !NinjaItemRules.CanEquipKatana(character!.Profession, slot, definition.Id.Value))
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            if (slot == EquipmentSlot.LeftHand && NinjaItemRules.IsKatana(definition.Id.Value) &&
                !transaction.GetItems(actor.CharacterUid).Any(existing =>
                    existing.Location == ItemLocation.Equipped(EquipmentSlot.RightHand) &&
                    definitions.Find(existing.DefinitionId) is { } right && NinjaItemRules.IsKatana(right.Id.Value)))
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.RequirementsNotMet));
            if (!requirements.CanEquip(character!, definition))
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.RequirementsNotMet));
            if (transaction.GetItems(actor.CharacterUid).Any(existing =>
                    existing.Location == ItemLocation.Equipped(slot) && existing.Id != item.Id))
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.EquipmentSlotOccupied));

            transaction.SaveItem(item with { Location = ItemLocation.Equipped(slot) });
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(item.Id));
        }, cancellationToken);
    }

    public ValueTask<GameplayCommandResult> UnequipAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("unequip", actor, itemId.Value);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            var item = transaction.FindItem(itemId);
            if (item is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ForeignItem));
            if (item.Location.Container != ItemContainer.Equipment)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            if (item.Location == ItemLocation.Equipped(EquipmentSlot.RightHand) &&
                transaction.GetItems(actor.CharacterUid).Any(existing =>
                    existing.Location == ItemLocation.Equipped(EquipmentSlot.LeftHand) &&
                    definitions.Find(existing.DefinitionId) is { } left && NinjaItemRules.IsKatana(left.Id.Value)))
                return InventoryService.Record(transaction, commandId, fingerprint,
                    InventoryService.Failure(GameplayResultCode.InvalidLocation));
            if (InventoryService.Count(transaction, actor.CharacterUid, ItemContainer.Inventory) >= character!.InventoryCapacity)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InventoryFull));
            ushort slot = InventoryService.NextFreeSlot(transaction, actor.CharacterUid, ItemContainer.Inventory,
                checked((ushort)character.InventoryCapacity));
            transaction.SaveItem(item with { Location = ItemLocation.Inventory(slot) });
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(item.Id));
        }, cancellationToken);
    }
}

public sealed class DefaultEquipmentRequirementPolicy : IEquipmentRequirementPolicy
{
    public bool CanEquip(CharacterGameplayState character, ItemDefinition definition) =>
        NinjaItemRules.CanEquipClassItem(character.Profession, definition.Id.Value) &&
        character.Level >= definition.Requirements.MinimumLevel &&
        MeetsWeaponProficiencyRequirement(character, definition) &&
        character.Strength >= definition.Requirements.Strength &&
        character.Agility >= definition.Requirements.Agility;

    private static bool MeetsWeaponProficiencyRequirement(CharacterGameplayState character,
        ItemDefinition definition)
    {
        if (definition.Requirements.Profession is not byte requiredLevel) return true;
        if (definition.EquipmentSlot != EquipmentSlot.RightHand) return false;
        uint family = definition.Id.Value / 1000;
        if (family is < 410 or > 699) return false;
        return character.WeaponProficiencies.GetValueOrDefault(checked((ushort)family)) >= requiredLevel;
    }
}

public sealed class AdditiveEquipmentStatCalculator : IEquipmentStatCalculator
{
    public ItemStatBlock Calculate(IEnumerable<ItemDefinition> equippedDefinitions) =>
        equippedDefinitions.Aggregate(default(ItemStatBlock), static (total, definition) => total + definition.BaseStats);
}

public sealed class ShopService(
    IGameplayStore store,
    IItemDefinitionCatalog definitions,
    IShopCatalog shops,
    IItemInstanceIdGenerator itemIds)
{
    public ValueTask<GameplayCommandResult> BuyAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ShopId shopId,
        ItemDefinitionId definitionId,
        int quantity,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("shop-buy", actor, shopId.Value, definitionId.Value, quantity);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            if (quantity is < 1 or > 40)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidQuantity));
            var shop = shops.Find(shopId);
            if (shop is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ShopNotFound));
            if (!shop.Entries.TryGetValue(definitionId, out var entry))
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ShopItemUnavailable));
            if (entry.BuyPrice < 0 || entry.SellPrice < 0 || entry.SellPrice > entry.BuyPrice)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidReferenceData));
            var definition = definitions.Find(definitionId)?.Validate();
            if (definition is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.DefinitionNotFound));
            if (InventoryService.Count(transaction, actor.CharacterUid, ItemContainer.Inventory) + quantity > character!.InventoryCapacity)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InventoryFull));
            long total;
            try { total = checked(entry.BuyPrice * quantity); }
            catch (OverflowException)
            {
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.CurrencyOverflow));
            }
            if (character.Balance(entry.Currency) < total)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InsufficientFunds));

            var generatedIds = Enumerable.Range(0, quantity).Select(_ => itemIds.Next()).ToArray();
            HashSet<ushort> occupied = transaction.GetItems(actor.CharacterUid)
                .Where(item => item.Location.Container == ItemContainer.Inventory)
                .Select(item => item.Location.Slot).ToHashSet();
            ushort[] slots = Enumerable.Range(0, character.InventoryCapacity)
                .Select(checkedValue => checked((ushort)checkedValue)).Where(slot => !occupied.Contains(slot))
                .Take(quantity).ToArray();
            if (slots.Length != quantity)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InventoryFull));
            transaction.SaveCharacter(character.WithBalance(entry.Currency, character.Balance(entry.Currency) - total));
            for (int index = 0; index < generatedIds.Length; index++)
            {
                ItemIdentity generatedId = generatedIds[index];
                transaction.AddItem(new ItemInstance(
                    generatedId.InstanceId,
                    generatedId.LegacyUid,
                    definition.Id,
                    actor.AccountId,
                    actor.CharacterUid,
                    ItemLocation.Inventory(slots[index]),
                    definition.MaximumDurability));
            }
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(generatedIds[0].InstanceId));
        }, cancellationToken);
    }

    public ValueTask<GameplayCommandResult> SellAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ShopId shopId,
        ItemInstanceId itemId,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint("shop-sell", actor, shopId.Value, itemId.Value);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            var item = transaction.FindItem(itemId);
            if (item is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ForeignItem));
            if (item.Location.Container != ItemContainer.Inventory)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            var shop = shops.Find(shopId);
            if (shop is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ShopNotFound));
            if (!shop.Entries.TryGetValue(item.DefinitionId, out var entry))
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ShopItemUnavailable));
            if (entry.BuyPrice < 0 || entry.SellPrice < 0 || entry.SellPrice > entry.BuyPrice)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidReferenceData));
            long balance;
            try { balance = checked(character!.Balance(entry.Currency) + entry.SellPrice); }
            catch (OverflowException)
            {
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.CurrencyOverflow));
            }
            transaction.RemoveItem(item.Id);
            transaction.SaveCharacter(character.WithBalance(entry.Currency, balance));
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(item.Id));
        }, cancellationToken);
    }
}

public sealed class WarehouseService(IGameplayStore store)
{
    public ValueTask<GameplayCommandResult> DepositAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        CancellationToken cancellationToken) =>
        MoveAsync(commandId, actor, itemId, ItemContainer.Inventory, ItemContainer.Warehouse, cancellationToken);

    public ValueTask<GameplayCommandResult> WithdrawAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        CancellationToken cancellationToken) =>
        MoveAsync(commandId, actor, itemId, ItemContainer.Warehouse, ItemContainer.Inventory, cancellationToken);

    private ValueTask<GameplayCommandResult> MoveAsync(
        GameplayCommandId commandId,
        GameplayActor actor,
        ItemInstanceId itemId,
        ItemContainer source,
        ItemContainer destination,
        CancellationToken cancellationToken)
    {
        string fingerprint = InventoryService.Fingerprint($"warehouse-{destination}", actor, itemId.Value);
        return store.ExecuteAsync(transaction =>
        {
            if (InventoryService.Replay(transaction, commandId, fingerprint, out var replay)) return replay;
            var validation = InventoryService.ValidateActor(transaction, actor, out var character);
            if (validation is not null) return InventoryService.Record(transaction, commandId, fingerprint, validation);
            var item = transaction.FindItem(itemId);
            if (item is null) return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ItemNotFound));
            if (item.AccountId != actor.AccountId || item.CharacterUid != actor.CharacterUid)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.ForeignItem));
            if (item.Location.Container != source)
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(GameplayResultCode.InvalidLocation));
            int capacity = destination == ItemContainer.Inventory ? character!.InventoryCapacity : character!.WarehouseCapacity;
            if (InventoryService.Count(transaction, actor.CharacterUid, destination) >= capacity)
            {
                var code = destination == ItemContainer.Inventory ? GameplayResultCode.InventoryFull : GameplayResultCode.WarehouseFull;
                return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Failure(code));
            }
            ushort slot = InventoryService.NextFreeSlot(transaction, actor.CharacterUid, destination,
                checked((ushort)capacity));
            var location = destination == ItemContainer.Inventory ? ItemLocation.Inventory(slot) : ItemLocation.Warehouse(slot);
            transaction.SaveItem(item with { Location = location });
            return InventoryService.Record(transaction, commandId, fingerprint, InventoryService.Success(item.Id));
        }, cancellationToken);
    }
}
