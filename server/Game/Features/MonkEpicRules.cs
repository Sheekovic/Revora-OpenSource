using System;
namespace Revora.Game.Features
{
    internal static class MonkEpicRules
    {
        internal const ushort Map=3983;
        internal const string TerrainHash="43A5268B7A4FDB9696AD854586D533EF842AE96C5080ABACCCBC3F80A0FA05D5";
        internal const uint RedFlower=722732,WhiteFlower=722736,Gem=3007562,Necklace=3007563,Heart=3007564,Incense=3007565;
        internal const int FlowersPerAttempt=3,IncensePerScroll=10;
        internal enum Trial { Fortune=1,Altar=2,Victory=3 }
        internal sealed class Result {
            internal bool Success,RetryGem;internal uint Item;internal int Count;
        }
        // Revora economy: transparent 50% independent trials; later failures award 1/3 incense.
        // Victory failure also restores a Gem so the documented retry at Altar is actionable.
        internal static Result Resolve(Trial stage,int roll) {
            if(stage<Trial.Fortune || stage>Trial.Victory || roll<0 || roll>=100)throw new ArgumentOutOfRangeException();
            if(roll<50)return new Result{Success=true,Item=stage==Trial.Fortune?Gem:stage==Trial.Altar?Necklace:Heart,Count=1};
            return new Result{Item=stage==Trial.Fortune?0:Incense,Count=stage==Trial.Fortune?0:stage==Trial.Altar?1:3,RetryGem=stage==Trial.Victory};
        }
        internal static bool Eligible(byte profession,byte reborn,byte level){return profession>=60 && profession<=65 && reborn==2 && level>=70;}
        internal static uint Scroll(byte selection){switch(selection){case 1:return 3007568;case 2:return 3007566;case 3:return 3007567;default:throw new ArgumentOutOfRangeException("selection");}}
        internal static uint EpicBeads(uint id,Func<uint,bool> catalog){
            if(catalog==null)throw new ArgumentNullException("catalog");
            if(id/1000!=610 || !catalog(id))return 0;uint epic=622000+id%1000;return catalog(epic)?epic:0;
        }
    }
}
