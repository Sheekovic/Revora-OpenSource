using Revora.Gameplay.Models;

namespace Revora.Gameplay.Contracts;

public interface IItemDefinitionCatalog
{
    ItemDefinition? Find(ItemDefinitionId id);
}

public interface IShopCatalog
{
    ShopDefinition? Find(ShopId id);
}

public interface IItemInstanceIdGenerator
{
    ItemIdentity Next();
}

public readonly record struct ItemIdentity(ItemInstanceId InstanceId, LegacyItemUid LegacyUid);

public interface IGameplayTransaction
{
    CharacterGameplayState? FindCharacter(CharacterUid characterUid);
    ItemInstance? FindItem(ItemInstanceId itemInstanceId);
    IReadOnlyList<ItemInstance> GetItems(CharacterUid characterUid);
    GameplayCommandReceipt? FindReceipt(GameplayCommandId commandId);
    void SaveCharacter(CharacterGameplayState character);
    void AddItem(ItemInstance item);
    void SaveItem(ItemInstance item);
    void RemoveItem(ItemInstanceId itemInstanceId);
    void SaveReceipt(GameplayCommandReceipt receipt);
}

public interface IGameplayStore
{
    /// <summary>
    /// Executes one atomic unit. Implementations must expose an isolated transaction and commit all
    /// mutations, including the command receipt, together. An exception must leave durable state unchanged.
    /// </summary>
    ValueTask<TResult> ExecuteAsync<TResult>(
        Func<IGameplayTransaction, TResult> operation,
        CancellationToken cancellationToken);
}

public interface IEquipmentRequirementPolicy
{
    bool CanEquip(CharacterGameplayState character, ItemDefinition definition);
}

public interface IEquipmentStatCalculator
{
    ItemStatBlock Calculate(IEnumerable<ItemDefinition> equippedDefinitions);
}
