using System;
using System.Linq;
using Revora.Database;

namespace Revora.Game.Features
{
    // Pure second-life planning; activation belongs to the owned rite transaction.
    // Official requirements and formula: docs/SECOND_REBIRTH_RESEARCH.md.
    internal static class SecondRebirthRules
    {
        internal static bool Eligible(byte profession,byte level,byte reborn) {
            return reborn==1 && level>=120 && profession%10==5 && RebirthRules.Classes.Any(c=>c/10==profession/10);
        }
        internal static CharacterProgression.Profile Profile(byte firstClass,byte firstLevel,byte oldClass,byte oldLevel,byte nextClass) {
            if(!RebirthRules.Eligible(firstClass,firstLevel,0) || !Eligible(oldClass,oldLevel,1) || !RebirthRules.Classes.Contains(nextClass))
                throw new ArgumentException("Invalid second rebirth history, class or level.");
            // Published total: 30 system points plus first-life bonus, second-life
            // bonus, and the 52 base/ordinary level points at level 15.
            int total=82+RebirthRules.Bonus(firstLevel,firstClass)+RebirthRules.Bonus(oldLevel,oldClass);
            var stats=DataHolder.StatsFor(nextClass,15);
            int assigned=stats.Strength+stats.Agility+stats.Vitality+stats.Spirit;
            if(assigned>total)throw new InvalidOperationException("Second rebirth profile exceeds its attribute budget.");
            return new CharacterProgression.Profile(15,0,stats.Strength,stats.Agility,stats.Vitality,stats.Spirit,
                total-assigned,nextClass,2,firstClass,oldClass,firstLevel,oldLevel);
        }
    }
}
