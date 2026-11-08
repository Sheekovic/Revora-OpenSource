using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

/// <summary>Pure, bounded adaptation of Owner Source Character.AddProfExp and Mob.TakeAttack.</summary>
public static class OwnerSourceProficiencyFormula
{
    public static IReadOnlyDictionary<ushort, long> ResolveWeaponAwards(uint? rightItemType,
        uint? leftItemType, long baseAward)
    {
        if (baseAward < 0) throw new ArgumentOutOfRangeException(nameof(baseAward));
        var result = new Dictionary<ushort, long>();
        if (baseAward == 0) return result;
        static ushort Family(uint itemType) => checked((ushort)(itemType / 1000));
        if (rightItemType.HasValue && leftItemType.HasValue &&
            Family(rightItemType.Value) == Family(leftItemType.Value))
            result[Family(rightItemType.Value)] = checked(baseAward + baseAward / 2);
        else
        {
            if (rightItemType.HasValue) result[Family(rightItemType.Value)] = baseAward;
            if (leftItemType.HasValue)
            {
                ushort family = Family(leftItemType.Value);
                result[family] = checked(result.GetValueOrDefault(family) + baseAward / 2);
            }
        }
        result.Remove(0);
        result.Remove(105); // Owner Source explicitly excludes the legacy blade family.
        return result;
    }

    public static long ScaleAward(long rawAward, byte proficiencyLevel, byte characterLevel,
        double violetGemMultiplier)
    {
        if (rawAward < 0 || proficiencyLevel > 20 || characterLevel is < 1 or > 130 ||
            violetGemMultiplier < 1 || !double.IsFinite(violetGemMultiplier))
            throw new ArgumentOutOfRangeException(nameof(rawAward));
        if (proficiencyLevel == 20 ||
            proficiencyLevel < 12 && characterLevel < proficiencyLevel * 10 - 10 ||
            proficiencyLevel >= 12 && characterLevel < 110)
            return 0;
        return checked((long)(rawAward * violetGemMultiplier *
            (proficiencyLevel < 12 ? 3.5d : 1d)));
    }
}
