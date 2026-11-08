using System;
using Revora;

internal static class CollectionPrestigeTests
{
    static void Main()
    {
        PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        foreach (var category in new[] { AbilitiesSort.Garments, AbilitiesSort.MountArmors })
        {
            var ceiling = PerfectionManager.GetAbilityScore(category, 200);
            foreach (int count in new[] { 201, 203, 421 })
                if (!Object.ReferenceEquals(ceiling, PerfectionManager.GetAbilityScore(category, count)))
                    throw new Exception("Collection overflow changed configured prestige.");
            foreach (var row in PerfectionManager.AbilitiesScore[category])
                for (int count = row.Min; count <= row.Max; count++)
                {
                    var expected = PerfectionManager.AbilitiesScore[category].Find(r => r.Min <= count && r.Max >= count);
                    if (!Object.ReferenceEquals(expected, PerfectionManager.GetAbilityScore(category, count)))
                        throw new Exception("Existing bracket selection changed.");
                }
            Reject(category, 0);
            Reject(category, -1);
        }
        Reject(AbilitiesSort.Chi, 401);
        Reject(AbilitiesSort.NobilityRank, 2);
        System.Console.WriteLine("Collection prestige: existing brackets, 201/203/421 caps, and invalid-value rejection passed.");
    }

    static void Reject(AbilitiesSort category, int value)
    {
        try { PerfectionManager.GetAbilityScore(category, value); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Invalid score unexpectedly accepted: " + category);
    }
}
