using System;
using System.Linq;
using Revora.Database;

namespace Revora.Game.Features
{
    // Preserve earned attributes; reincarnation grants no additional bonus.
    // Payment, pending recovery and exact saved preimages belong to the caller.
    internal static class ReincarnationRules
    {
        internal static bool Profession(byte value) {
            return RebirthRules.Classes.Any(c=>value>=c && value<=c/10*10+5);
        }
        internal static bool Eligible(byte profession,byte level,byte reborn) {
            return reborn==2 && level>=110 && Profession(profession);
        }
        internal static ushort Body(ushort oldBody,byte selected) {
            if((oldBody==1003 || oldBody==1004) && (selected==3 || selected==4))return (ushort)(1000+selected);
            if((oldBody==2001 || oldBody==2002) && (selected==1 || selected==2))return (ushort)(2000+selected);
            throw new ArgumentException("Invalid reincarnation body choice.");
        }
        internal static CharacterProgression.Profile Profile(CharacterProgression.Profile before,byte next,ushort pointsPerLevel) {
            if(before==null)throw new ArgumentNullException("before");
            var v=before.Values;
            if(!Eligible((byte)v[7],(byte)v[0],(byte)v[8]) || !RebirthRules.Classes.Contains(next)
                || !Profession((byte)v[9]) || !Profession((byte)v[10]) || v[11]<110 || v[12]<110)
                throw new ArgumentException("Invalid reincarnation history.");
            long total=v.Skip(2).Take(5).Sum()-checked((v[0]-15)*pointsPerLevel);
            var stats=DataHolder.StatsFor(next,15);
            long assigned=stats.Strength+stats.Agility+stats.Vitality+stats.Spirit;
            if(total<assigned || total-assigned>ushort.MaxValue)
                throw new InvalidOperationException("Reincarnation attribute budget cannot be represented.");
            return new CharacterProgression.Profile(15,0,stats.Strength,stats.Agility,stats.Vitality,stats.Spirit,total-assigned,
                next,2,v[10],v[7],v[12],v[0],v[13]);
        }
    }
}
