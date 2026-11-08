using System;
using System.Collections.Generic;
using Revora.Game.Features;

internal static class RulesTests
{
    static int checks;
    static void Check(bool value){if(!value)throw new Exception("Pirate rules failed at "+checks);checks++;}
    static void Main() {
        var items=new HashSet<uint>{611439,612439,671439,670439,611301,3307451};
        Func<uint,bool> catalog=items.Contains;
        Check(PirateEpicRules.ConversionSlot(611439,612439,catalog)==4);
        Check(PirateEpicRules.ConversionSlot(671439,612439,catalog)==5);
        Check(PirateEpicRules.ConversionSlot(671439,670439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(611301,612439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(0,612439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(421439,612439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(612439,611439,catalog)==0);
        items.Remove(671439);
        Check(PirateEpicRules.ConversionSlot(611439,612439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(671439,612439,catalog)==0);
        items.Add(671439);items.Remove(670439);
        Check(PirateEpicRules.ConversionSlot(671439,612439,catalog)==0);
        Check(PirateEpicRules.ConversionSlot(611439,612439,catalog)==4);
        Check(PirateEpicRules.EpicWeapon(3307451,id=>true)==0);
        Check(PirateEpicRules.EpicWeapon(uint.MaxValue,id=>true)==0);
        Check(PirateEpicRules.EpicWeapon(611999,id=>true)==0);
        Check(PirateEpicRules.MapStars(3307448)==0);
        Check(PirateEpicRules.MapStars(3307442)==0);
        Check(PirateEpicRules.MapStars(3307443)==1);
        Check(PirateEpicRules.MapStars(3307447)==5);
        for(int p=0;p<256;p++) {
            Check(PirateEpicRules.Eligible((byte)p,2,70)==(p>=70 && p<=75));
            Check(!PirateEpicRules.Eligible((byte)p,1,140));
            Check(!PirateEpicRules.Eligible((byte)p,2,69));
        }
        foreach(int n in new[]{-1,0,5,int.MaxValue}) {
            bool rejected=false;try{PirateEpicRules.MapRewardPercent(n);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected);
        }
        for(int stars=1;stars<=4;stars++)for(int roll=0;roll<100;roll++) {
            int limit=stars==1?4:5;
            for(int choice=0;choice<=limit-stars;choice++) {
                var rewards=PirateEpicRules.SearchRewards(3307442u+(uint)stars,n=>n==100?roll:choice);
                Check(rewards.Length==1 && rewards[0]==(roll<PirateEpicRules.MapRewardPercent(stars)?3307442u+(uint)(stars+choice):3307450u));
            }
        }
        for(int choice=0;choice<3;choice++)Check(PirateEpicRules.SearchRewards(3307448,n=>choice)[0]==3307443u+choice);
        var final=PirateEpicRules.SearchRewards(3307447,n=>{throw new Exception("Final map must not draw");});
        Check(final.Length==2 && final[0]==3307449 && final[1]==3307450);
        Console.WriteLine("Pirate rule checks passed: "+checks);
    }
}
