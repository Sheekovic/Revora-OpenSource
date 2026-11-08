using System;
using System.Collections.Generic;

namespace Revora.Game.Features
{
    // Tactical preferences, not alternative damage/cooldown implementations.
    // The combat adapter must intersect these with learned, legal 6711 skills.
    internal static class ArenaBotPolicy
    {
        internal const int HumanSignupThreshold=10, ReactionMilliseconds=240;
        internal sealed class Profile
        {
            internal readonly string Name;
            internal readonly int Range;
            internal readonly ushort[] Attacks,Defenses,Recovery;
            internal Profile(string name,int range,ushort[] attacks,ushort[] defenses,ushort[] recovery)
            {Name=name;Range=range;Attacks=attacks;Defenses=defenses;Recovery=recovery;}
        }
        private static readonly ushort[] None=new ushort[0];
        internal static Profile For(byte profession,bool knives,bool chaser)
        {
            switch(profession/10)
            {
                case 1:return new Profile("Trojan",3,new ushort[]{11990,11980,1045,1046,1115,10315},None,new ushort[]{1190});
                case 2:return new Profile("Warrior",3,new ushort[]{1045,1046,1051},new ushort[]{10470},new ushort[]{1190});
                case 4:return knives
                    ?new Profile("Assassin",8,new ushort[]{11660,11650},new ushort[]{11620,11670},None)
                    :new Profile("Archer",9,new ushort[]{8000,10313},new ushort[]{8003},None);
                case 5:return new Profile("Ninja",3,new ushort[]{12080,6000,6001,6002,6004},new ushort[]{6003},None);
                case 6:return new Profile("Monk",3,new ushort[]{10490,10381,10415},new ushort[]{10425,10410,10395},new ushort[]{10425});
                case 7:return new Profile("Pirate",6,new ushort[]{11120,11030,11070,11040,11110},new ushort[]{11130},None);
                case 8:return new Profile("Dragon Warrior",3,new ushort[]{12300,12350,12170,12320,12330,12340},new ushort[]{12200,12270},new ushort[]{12280});
                case 13:return new Profile("Water Taoist",8,new ushort[]{1000,1001,12370},new ushort[]{30000,1090,1095},new ushort[]{1175,1005,1195});
                case 14:return new Profile("Fire Taoist",8,new ushort[]{1002,1001,1000,1165,12380},new ushort[]{1090},new ushort[]{1195});
                case 16:return chaser
                    ?new Profile("Windwalker Chaser",8,new ushort[]{12830,12850,12890,12840},new ushort[]{13020},new ushort[]{12950})
                    :new Profile("Windwalker Stomper",4,new ushort[]{12830,12930,12940},new ushort[]{12960},new ushort[]{12950});
                default:return new Profile("Basic",2,new ushort[]{1045,1046,1000},None,None);
            }
        }
        internal static uint SelectRoster(int cursor,Func<uint,bool> available)
        {
            for(int offset=0;offset<ArenaBotStore.Capacity;offset++) {
                uint uid=ArenaBotStore.FirstUid+(uint)((cursor+offset)%ArenaBotStore.Capacity);
                if(available(uid))return uid;
            }
            return 0;
        }
        internal static bool WeaponAllows(Revora.Database.SpellInformation spell,uint right,uint left)
        {
            if(spell.OnlyWithThisWeaponSubtype.Count==0 || spell.ID==11200 || spell.ID==11160)return true;
            if(right==0)return false;
            ushort main=(ushort)(right/1000);if(main==421)main=420;
            return spell.OnlyWithThisWeaponSubtype.Contains(main) || left!=0 && spell.OnlyWithThisWeaponSubtype.Contains((ushort)(left/1000));
        }
        internal static bool HoldAttackPosition(int distance,int preferred,bool retreat,bool sight)
        { return !retreat && sight && distance>0 && distance<=preferred && distance>=Math.Max(1,preferred-2); }
        internal static bool ShouldFill(int realSignups,int waitingMilliseconds)
        {return realSignups>0 && realSignups<HumanSignupThreshold && waitingMilliseconds>=5000;}
        internal static int MovementScore(int distance,int preferred,bool threatened,bool obstructed,int repeatedTile)
        {
            if(obstructed)return int.MinValue;
            return 100-Math.Abs(distance-preferred)*12-(threatened?35:0)-repeatedTile*8;
        }
        internal static IEnumerable<ushort> Priorities(Profile profile,int healthPercent,int manaPercent,bool harmfulStatus,bool enemyClose)
        {
            if(harmfulStatus || healthPercent<45 || manaPercent<20)
                foreach(var spell in profile.Recovery)yield return spell;
            if(enemyClose || healthPercent<65)
                foreach(var spell in profile.Defenses)yield return spell;
            foreach(var spell in profile.Attacks)yield return spell;
        }
    }
}
