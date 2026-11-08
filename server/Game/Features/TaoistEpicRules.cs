using System;

namespace Revora.Game.Features
{
    // Revora quest contract; original trial odds and explicit local economy choices.
    internal static class TaoistEpicRules
    {
        internal const uint RedFlower=722732,WhiteFlower=722736,Leaf=3006013,Crystal=3006014,Fish=3006015,Panacea=3006016,Hossu=619439;
        // Revora economy, not claims about unpublished original exchange prices.
        internal const uint TrialCp=27;
        internal const int FlowersPerAttempt=3,EliteFailureFish=1,SuperFailureFish=3,FishPerScroll=10,FishPerHossu=50;
        internal enum Trial { Refined=1,Elite=2,Super=3 }
        internal sealed class Result {internal readonly uint Item;internal readonly int Count;internal Result(uint item,int count){Item=item;Count=count;}}
        internal static bool Eligible(byte profession,byte reborn,byte level) {
            return reborn==2 && level>=70 && (profession==100 || profession==101 || profession>=132 && profession<=135 || profession>=142 && profession<=145);
        }
        internal static Result Resolve(Trial trial,int roll) {
            if(trial<Trial.Refined || trial>Trial.Super || roll<0 || roll>=100)throw new ArgumentOutOfRangeException();
            if(roll<10)return new Result(trial==Trial.Refined?Leaf:trial==Trial.Elite?Crystal:Panacea,1);
            return trial==Trial.Refined?new Result(0,0):new Result(Fish,trial==Trial.Elite?EliteFailureFish:SuperFailureFish);
        }
        internal static uint Scroll(byte selection) {
            switch(selection){case 1:return 3006017;case 2:return 3006018;case 3:return 3006019;case 4:return 3006020;default:throw new ArgumentOutOfRangeException("selection");}
        }
        internal static uint EpicBacksword(uint id,Func<uint,bool> catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            if(id/1000!=421 || !catalog(id))return 0;
            uint target=620000+id%1000;
            return catalog(target)?target:0;
        }
    }
}
