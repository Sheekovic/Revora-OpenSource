using System;
using System.Linq;

namespace Revora.Game.Features
{
    // Dated tournament kinds are immutable persistence identities.
    internal static class LocalPkRules
    {
        internal static readonly int[] Kinds=new[]{0,1,10,11,12,13,14,15,16,17,18,19,20,21,22};
        internal static readonly int[] Families=new[]{10,20,40,50,60,70,80,130,140,160};
        internal static readonly string[] Classes=new[]{"Trojan","Warrior","Archer","Ninja","Monk","Pirate","Dragon Warrior","Water Taoist","Fire Taoist","Wind Walker"};
        internal static readonly ulong[] ClassFlags=new ulong[]{39,38,40,43,114,122,154,41,42,203};
        internal static readonly ulong[] LegacyKeys=new ulong[]{1,2,3,4,5,6,7,8,20,21,22,34,203};
        internal static bool ClassWar(int kind){return kind>=10 && kind<=19;}
        internal static bool BpWar(int kind){return kind>=20 && kind<=22;}
        internal static int ClassKind(int profession) {
            int index=Array.FindIndex(Families,start=>profession>=start && profession<=start+5);return index<0?-1:index+10;
        }
        internal static int JoinMinutes(int kind){return BpWar(kind)?10:ClassWar(kind)?15:19;}
        internal static int WinMinutes(int kind){return BpWar(kind)?10:ClassWar(kind)?15:22;}
        internal static int Duration(int kind){return ClassWar(kind)?75:60;}
        internal static int Days(int kind){return kind==1?30:7;}
        internal static int Balls(int kind){return kind==0?3:5;}
        internal static long Cps(int kind){return kind==0?100000:kind==1?250000:50000;}
        internal static long Rvps(int kind){return kind==0?1000:kind==1?2500:500;}
        internal static decimal Gold(int kind){return kind==0?1000000:kind==1?5000000:500000;}
        internal static ulong Halo(int kind){return kind==0?37UL:kind==1?36UL:kind==2?51UL:ClassWar(kind)?ClassFlags[kind-10]:0UL;}
        internal static string Name(int kind){return BpWar(kind)?"BP PK ("+BattlePowerTournament.Divisions[kind-20]+")":kind==0?"Weekly PK":kind==1?"Monthly PK":kind==2?"Couple PK":"Class PK ("+Classes[kind-10]+")";}
        internal static ulong[] ManagedFlags(){return ClassFlags.Concat(new[]{36UL,37UL,51UL}).ToArray();}
    }
}
