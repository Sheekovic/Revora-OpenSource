using System;
using System.Linq;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type SecondRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.SecondRebirthRules");
    static long[] SecondProfile(byte first,byte firstLevel,byte current,byte level,byte next) {
        var profile=Call(SecondRules,null,"Profile",first,firstLevel,current,level,next);
        return (long[])profile.GetType().GetField("Values",Hidden|System.Reflection.BindingFlags.Instance).GetValue(profile);
    }
    static void VerifySecondRebirthRules() {
        DataHolder.ReadStats();byte[] masters={15,25,45,55,65,75,85,135,145,165};
        foreach(byte current in masters) {
            Check(!(bool)Call(SecondRules,null,"Eligible",current,(byte)119,(byte)1)
                && (bool)Call(SecondRules,null,"Eligible",current,(byte)120,(byte)1)
                && !(bool)Call(SecondRules,null,"Eligible",current,(byte)140,(byte)0)
                && !(bool)Call(SecondRules,null,"Eligible",current,(byte)140,(byte)2)
                && !(bool)Call(SecondRules,null,"Eligible",(byte)(current-1),(byte)140,(byte)1),"second rite requires level 120 and exactly one promoted life: "+current);
        }
        int[] normal={0,1,3,6,10,15,21,28,36,45,55};int[] water={15,15,21,21,28,28,36,36,45,45,55};
        int combinations=0;
        foreach(byte first in masters)foreach(byte current in masters)foreach(byte next in rebirthClasses) {
            var p=SecondProfile(first,130,current,130,next);combinations++;
            if(p[0]!=15 || p[1]!=0 || p.Skip(2).Take(5).Sum()!=192 || p[7]!=next || p[8]!=2 || p[9]!=first || p[10]!=current || p[11]!=130 || p[12]!=130)
                throw new Exception("Second rebirth class history or total differs for "+first+"/"+current+"/"+next);
        }
        Check(combinations==1000,"all 1,000 supported three-life class combinations preserve history and exactly 192 total points at double 130");
        for(int index=0;index<11;index++)foreach(byte current in masters) {
            long[] p=SecondProfile(15,130,current,(byte)(120+index),11);int bonus=current==135?water[index]:normal[index];
            Check(p.Skip(2).Take(5).Sum()==137+bonus,"second-life bonus table at "+current+"/"+(120+index));
        }
        foreach(byte next in rebirthClasses) {
            var p=SecondProfile(135,110,15,120,next);var stats=Call(typeof(DataHolder),null,"StatsFor",next,(byte)15);
            var installed=new[]{"Strength","Agility","Vitality","Spirit"}.Select(name=>Convert.ToInt64(stats.GetType().GetField(name,Hidden|System.Reflection.BindingFlags.Instance).GetValue(stats)));
            Check(p.Skip(2).Take(5).Sum()==82 && p.Skip(2).Take(4).SequenceEqual(installed),
                "minimum first Water and second ordinary lives retain installed base distribution within 82 total points: "+next);
            Check(SecondProfile(15,140,135,140,next).Skip(2).Take(5).Sum()==192,"both historical level bonuses cap without duplicate base points: "+next);
        }
        foreach(byte profession in new byte[]{0,1,35,95,125,155,175,255})
            Check(!(bool)Call(SecondRules,null,"Eligible",profession,(byte)140,(byte)1),"unknown or non-promoted second-life class is rejected: "+profession);
        foreach(byte[] invalid in new[]{new byte[]{15,119,15,120,11},new byte[]{135,109,15,120,11},new byte[]{14,130,15,120,11},
            new byte[]{15,130,135,110,11},new byte[]{15,130,15,120,15},new byte[]{15,130,15,120,91}}) {
            bool rejected=false;try{SecondProfile(invalid[0],invalid[1],invalid[2],invalid[3],invalid[4]);}catch(ArgumentException){rejected=true;}
            Check(rejected,"invalid historical life or destination cannot produce a second-rebirth profile");
        }
    }
}
