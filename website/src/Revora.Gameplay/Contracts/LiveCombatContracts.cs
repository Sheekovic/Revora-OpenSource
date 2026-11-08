using Revora.Gameplay.Models;

namespace Revora.Gameplay.Contracts;

public interface ILiveCombatDamageFormula
{
    long CalculateNormalDamage(LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot target,
        uint action, uint sequence);
}

public interface ILivePvpPolicy
{
    bool CanAttack(LiveCombatEntitySnapshot attacker, LiveCombatEntitySnapshot target);
}

/// <summary>
/// Implementations commit the receipt and any durable HP/death mutation in one
/// transaction. Applied and replay are success; conflict leaves all state unchanged.
/// </summary>
public interface ILiveCombatPersistence
{
    ValueTask<CombatPersistenceResult> CommitAsync(CombatMutation mutation, CancellationToken cancellationToken);
}

public interface INinjaCombatPersistence
{
    ValueTask<CombatPersistenceResult> CommitSkillAsync(NinjaCombatMutation mutation,
        CancellationToken cancellationToken);
}

public interface IMonsterCombatPersistence
{
    ValueTask<CombatPersistenceResult> CommitMonsterAttackAsync(MonsterCombatMutation mutation,
        CancellationToken cancellationToken);
}
