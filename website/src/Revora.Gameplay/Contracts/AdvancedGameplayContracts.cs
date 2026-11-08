using Revora.Gameplay.Models;

namespace Revora.Gameplay.Contracts;

public interface IWorldCellPolicy
{
    bool IsKnownMap(int mapId);
    bool CanStand(WorldPoint point);
}

public interface IMonsterDefinitionCatalog
{
    MonsterDefinition? Find(MonsterTypeId id);
}

public interface ICombatFormula
{
    long CalculateDamage(CombatStats attacker, int targetDefense);
}

public interface ILevelRequirementCatalog
{
    byte MaximumLevel { get; }
    long RequiredTotalExperience(byte level);
}

public interface ICharacterOwnershipPolicy
{
    bool Owns(GameplayActor actor);
}

/// <summary>
/// A persistence adapter may consume this settlement only inside one serializable transaction,
/// rechecking item versions, ownership and currency immediately before commit.
/// </summary>
public interface ITradeSettlementCommitter
{
    DomainCommandResult Commit(TradeSettlement settlement);
}
