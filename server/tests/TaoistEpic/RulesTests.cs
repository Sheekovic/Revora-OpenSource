using System;
using Revora.Game.Features;

internal static class RulesTests
{
    static int checks;
    static void Check(bool condition){if(!condition)throw new Exception("Taoist rule check failed at "+checks);checks++;}
    static void Main() {
        for(int profession=0;profession<256;profession++) {
            bool expected=profession==100 || profession==101 || profession>=132 && profession<=135 || profession>=142 && profession<=145;
            Check(TaoistEpicRules.Eligible((byte)profession,2,70)==expected);
            Check(!TaoistEpicRules.Eligible((byte)profession,1,140));
            Check(!TaoistEpicRules.Eligible((byte)profession,2,69));
        }
        for(int stage=1;stage<=3;stage++)for(int roll=0;roll<100;roll++) {
            var result=TaoistEpicRules.Resolve((TaoistEpicRules.Trial)stage,roll);
            if(roll<10)Check(result.Count==1 && result.Item==(stage==1?3006013u:stage==2?3006014u:3006016u));
            else Check(result.Count==(stage==1?0:stage==2?1:3) && result.Item==(stage==1?0u:3006015u));
        }
        foreach(int roll in new[]{-1,100,int.MinValue,int.MaxValue}) {
            bool rejected=false;try{TaoistEpicRules.Resolve(TaoistEpicRules.Trial.Refined,roll);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected);
        }
        foreach(int stage in new[]{0,4,-1}) {
            bool rejected=false;try{TaoistEpicRules.Resolve((TaoistEpicRules.Trial)stage,0);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected);
        }
        Check(TaoistEpicRules.EpicBacksword(421439,id=>id==421439 || id==620439)==620439);
        Check(TaoistEpicRules.EpicBacksword(421301,id=>id==421301)==0);
        Check(TaoistEpicRules.EpicBacksword(421439,id=>id==620439)==0);
        Check(TaoistEpicRules.EpicBacksword(620439,id=>true)==0);
        Check(TaoistEpicRules.EpicBacksword(619439,id=>true)==0);
        Check(TaoistEpicRules.EpicBacksword(uint.MaxValue,id=>true)==0);
        for(byte n=1;n<=4;n++)Check(TaoistEpicRules.Scroll(n)==3006016u+n);
        Console.WriteLine("Taoist rule checks passed: "+checks);
    }
}
