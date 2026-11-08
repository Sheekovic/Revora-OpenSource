using System;

namespace Revora.Game.Features
{
    // Reviewed rules. The NPC/store integration owns payment, RNG and projection.
    internal static class NinjaEpicRules
    {
        internal const uint RedFlower=722732,WhiteFlower=722736,Seal=3004462,Jade=3004463,Eye=3004464,Dust=3004465;
        internal const uint ShadowScroll=3004459,TwilightScroll=3004460,FatalScroll=3004461;
        internal const int FlowersPerAttempt=4,DustPerScroll=10,SingleCp=27,BulkCp=270;
        internal enum Trial { YinYang=1,Elemental=2,Wheel=3 }
        internal sealed class Result {
            internal readonly bool Success;internal readonly uint Item;internal readonly int Count;internal readonly ushort ReturnMap;
            internal Result(bool success,uint item,int count,ushort returnMap){Success=success;Item=item;Count=count;ReturnMap=returnMap;}
        }
        internal static bool Eligible(byte profession,byte reborn,byte level) {
            return profession>=50 && profession<=55 && reborn==2 && level>=70;
        }
        internal static bool BulkEligible(byte profession,byte reborn,byte level,byte vip) {
            return Eligible(profession,reborn,level) && vip>=3;
        }
        internal static Result Resolve(Trial trial,int roll) {
            if(trial<Trial.YinYang || trial>Trial.Wheel || roll<0 || roll>=100)throw new ArgumentOutOfRangeException();
            bool success=roll<10;
            if(success)return new Result(true,trial==Trial.YinYang?Seal:trial==Trial.Elemental?Jade:Eye,1,0);
            // The official guide identifies Stardust on failure. Counts preserve Revora's existing policy.
            return trial==Trial.YinYang?new Result(false,0,0,0):new Result(false,Dust,trial==Trial.Elemental?1:5,3849);
        }
        internal static int ResolveBulk(int[] rolls) {
            if(rolls==null || rolls.Length!=10)throw new ArgumentException("Ten independent trial draws are required.");
            int seals=0;foreach(int roll in rolls)if(Resolve(Trial.YinYang,roll).Success)seals++;return seals;
        }
        internal static uint Scroll(byte selection) {
            switch(selection){case 1:return ShadowScroll;case 2:return TwilightScroll;case 3:return FatalScroll;
                default:throw new ArgumentOutOfRangeException("selection");}
        }
        internal static ushort MapForNpc(uint npc) {
            switch(npc){case 4720:return 1004;case 199445:case 199464:case 199546:return 3849;
                case 199444:case 199465:case 199544:return 3850;case 199446:case 199466:case 199545:return 3851;
                default:return 0;}
        }
        internal static ushort[] Landing(ushort map) {
            switch(map){case 3849:return new ushort[]{29,28};case 3850:return new ushort[]{34,39};
                case 3851:return new ushort[]{33,35};default:throw new ArgumentOutOfRangeException("map");}
        }
        internal static uint EpicKatana(uint katana,Func<uint,bool> catalogContains) {
            if(catalogContains==null)throw new ArgumentNullException("catalogContains");
            if(katana/1000!=601 || !catalogContains(katana))return 0;
            uint epic=616000+katana%1000;return catalogContains(epic)?epic:0;
        }
    }
}
