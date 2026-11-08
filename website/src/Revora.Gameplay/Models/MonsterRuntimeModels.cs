namespace Revora.Gameplay.Models;

public enum OwnerMonsterBehavior : byte
{
    Passive = 0,
    HuntPlayers = 1,
    HuntMonsters = 2,
    HuntMonstersAndPlayers = 3,
    HuntBlueNames = 4,
    HuntMonstersAndBlueNames = 6
}

public enum OwnerMonsterAttackType : byte
{
    Melee = 2,
    Kill = 14,
    Magic = 21,
    Ranged = 28
}

public enum OwnerMonsterClassification : byte
{
    Normal = 0,
    Guard = 1,
    Boss = 2,
    EventSpecial = 3,
    Passive = 4
}

public enum PlayerPkStatus : byte
{
    Normal = 0,
    Flashing = 1,
    Red = 2,
    Black = 3
}

public sealed record OwnerMonsterTemplate(
    int MonsterType,
    string Name,
    OwnerMonsterBehavior Behavior,
    uint Mesh,
    ushort Level,
    long MaximumHealth,
    int Defense,
    int MagicDefense,
    int MagicAttack,
    int MinimumAttack,
    int MaximumAttack,
    byte DamageReduction,
    byte Dodge,
    OwnerMonsterAttackType AttackType,
    ushort MagicSkill,
    byte MagicLevel,
    bool GivesRewards,
    byte AttackDistance,
    int MinimumSilver,
    int MaximumSilver,
    int MoveDelayMilliseconds,
    long RespawnDelayMilliseconds,
    bool UseLevelDifferenceDamage,
    OwnerMonsterClassification Classification = OwnerMonsterClassification.Normal)
{
    public OwnerMonsterTemplate Validate()
    {
        if (MonsterType <= 0 || string.IsNullOrWhiteSpace(Name) || Name.Length > 32 || Mesh == 0 ||
            Level > 255 || MaximumHealth <= 0 || Defense < 0 || MagicDefense < 0 || MagicAttack < 0 ||
            MinimumAttack < 0 || MaximumAttack < MinimumAttack || DamageReduction == 0 ||
            AttackDistance > 64 || MinimumSilver < 0 || MaximumSilver < MinimumSilver ||
            MoveDelayMilliseconds < 0 || RespawnDelayMilliseconds < 0 ||
            AttackType == OwnerMonsterAttackType.Magic && MagicSkill == 0)
            throw new ArgumentOutOfRangeException(nameof(MonsterType));
        return this;
    }
}

public sealed record OwnerMonsterGenerator(
    long GeneratorId,
    int MonsterType,
    int MapId,
    int MinimumX,
    int MinimumY,
    int MaximumX,
    int MaximumY,
    int Population,
    bool Enabled,
    string ActivationCategory)
{
    public OwnerMonsterGenerator Validate()
    {
        if (GeneratorId <= 0 || MonsterType <= 0 || MapId <= 0 || Population is < 1 or > 10_000 ||
            MinimumX < 0 || MinimumY < 0 || MaximumX < MinimumX || MaximumY < MinimumY ||
            ActivationCategory is not ("normal" or "event-gated" or "incompatible"))
            throw new ArgumentOutOfRangeException(nameof(GeneratorId));
        return this;
    }
}

public readonly record struct OwnerMonsterUidAssignment(
    long GeneratorId,
    int Ordinal,
    uint LegacyUid)
{
    public OwnerMonsterUidAssignment Validate()
    {
        if (GeneratorId <= 0 || Ordinal < 0 || LegacyUid is <= 400_000 or >= 600_000)
            throw new ArgumentOutOfRangeException(nameof(LegacyUid));
        return this;
    }
}

public sealed record OwnerMonsterDropRule(
    int MonsterType,
    short Ordinal,
    Guid ItemSourceId,
    uint ItemType,
    int ChanceBasisPoints,
    byte Plus,
    byte Sockets,
    byte Bless);

public sealed record OwnerEquipmentDropCandidate(
    ushort MonsterLevel,
    Guid ItemSourceId,
    uint ItemType,
    short Ordinal);

public sealed record OwnerCommonDropItem(Guid ItemSourceId, uint ItemType);

public sealed record OwnerMonsterRuntimeDefinition(
    uint LegacyUid,
    OwnerMonsterTemplate Template,
    OwnerMonsterGenerator Generator,
    ushort SpawnX,
    ushort SpawnY,
    byte Direction);

public sealed record PlayerCombatStatistics(
    byte Level,
    int Strength,
    int Agility,
    int Spirit,
    int MinimumAttack,
    int MaximumAttack,
    int Defense,
    int MagicAttack,
    int MagicDefense,
    int Accuracy,
    int Dodge,
    byte TotalBless = 0,
    PlayerPkStatus PkStatus = PlayerPkStatus.Normal)
{
    public PlayerCombatStatistics Validate()
    {
        if (MinimumAttack < 0 || MaximumAttack < MinimumAttack || Defense < 0 || MagicAttack < 0 ||
            MagicDefense < 0 || Accuracy < 0 || Dodge < 0 || TotalBless > 100 ||
            !Enum.IsDefined(PkStatus))
            throw new ArgumentOutOfRangeException(nameof(MinimumAttack));
        return this;
    }
}

public static class OwnerSourceCombatFormula
{
    public static long PlayerPhysicalDamage(PlayerCombatStatistics attacker, OwnerMonsterTemplate target,
        uint sequence)
    {
        attacker.Validate(); target.Validate();
        int rolled = DeterministicRange(attacker.MinimumAttack, attacker.MaximumAttack, sequence);
        long damage = checked((long)rolled + attacker.Strength);
        damage = ApplyLevelDifference(damage, attacker.Level, target.Level,
            target.UseLevelDifferenceDamage);
        damage = Math.Max(1, damage - target.Defense);
        damage = Math.Max(1, damage / target.DamageReduction);
        return damage;
    }

    public static long MonsterDamage(OwnerMonsterTemplate attacker, PlayerCombatStatistics target,
        uint sequence)
    {
        attacker.Validate(); target.Validate();
        long raw = attacker.AttackType == OwnerMonsterAttackType.Magic
            ? attacker.MagicAttack
            : DeterministicRange(attacker.MinimumAttack, attacker.MaximumAttack, sequence);
        long reduced = attacker.AttackType switch
        {
            OwnerMonsterAttackType.Magic => Math.Max(1, raw - target.MagicDefense),
            OwnerMonsterAttackType.Ranged => Math.Max(1, raw * Math.Max(0, 106 - target.Dodge) / 100),
            _ => Math.Max(1, raw - target.Defense)
        };
        return Math.Max(1, reduced * (100 - target.TotalBless) / 100);
    }

    public static long ExperienceForDamage(long damage, byte attackerLevel, ushort monsterLevel)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        decimal factor = monsterLevel > attackerLevel + 4 ? 1.4m :
            monsterLevel >= attackerLevel ? 1.1m : monsterLevel + 4 < attackerLevel ? 0.1m : 1m;
        return checked((long)Math.Floor(damage * factor));
    }

    private static long ApplyLevelDifference(long damage, byte attacker, ushort target, bool enabled)
    {
        if (!enabled) return damage;
        int difference = attacker - target;
        decimal multiplier = difference switch
        {
            >= 20 => 1.5m,
            >= 10 => 1.25m,
            <= -20 => 0.25m,
            <= -10 => 0.5m,
            _ => 1m
        };
        return Math.Max(1, checked((long)Math.Floor(damage * multiplier)));
    }

    private static int DeterministicRange(int minimum, int maximum, uint sequence)
    {
        if (maximum <= minimum) return minimum;
        uint mixed = sequence * 747796405u + 2891336453u;
        mixed = ((mixed >> ((int)(mixed >> 28) + 4)) ^ mixed) * 277803737u;
        mixed = (mixed >> 22) ^ mixed;
        return checked(minimum + (int)(mixed % checked((uint)(maximum - minimum + 1))));
    }
}
