using System;
using Revora.Game.Features;
class RulesTests {
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    static int Main(){try {
        foreach(byte level in new byte[]{1,40,69,70,100,140})foreach(byte lives in new byte[]{0,1,2,3,255})
            Check(EliminationRules.Eligible(level,lives)==(level>=70 && (lives==1 || lives==2)),"reborn admission boundary");
        Check(EliminationRules.HerbsRequired==12 && EliminationRules.PillsRequired==10,"distinct material requirements");
        Check(EliminationRules.StudyPoints==50 && EliminationRules.ExperienceBalls==1,"recovered specific-guide reward");
        Check(EliminationRules.AtSpiceSite(1011,311,256),"installed spice site");
        Check(EliminationRules.AtSpiceSite(1011,305,250) && EliminationRules.AtSpiceSite(1011,317,262),"Revora interaction edges");
        Check(!EliminationRules.AtSpiceSite(1002,311,256),"unrelated map");
        Check(!EliminationRules.AtSpiceSite(1011,318,256) && !EliminationRules.AtSpiceSite(1011,311,249),"out of range");
        Check(!EliminationRules.AtSpiceSite(1011,int.MinValue,int.MaxValue),"overflow coordinates");
        ushort gathered=0,next;
        for(uint npc=EliminationRules.FirstHerb;npc<EliminationRules.FirstHerb+12;npc++) {
            var site=EliminationRules.Herb(npc);
            Check(site!=null,"herb identity");
            Check(!EliminationRules.TryGather(npc,1002,site.X,site.Y,gathered,out next) && next==gathered,"wrong map preserves mask");
            Check(!EliminationRules.TryGather(npc,1011,site.X+7,site.Y,gathered,out next),"distant plant");
            Check(!EliminationRules.TryGather(npc,1011,int.MinValue,int.MaxValue,gathered,out next),"overflow plant coordinates");
            Check(EliminationRules.TryGather(npc,1011,site.X-6,site.Y+6,gathered,out next),"distinct plant within reach");
            gathered=next;
            Check(!EliminationRules.TryGather(npc,1011,site.X,site.Y,gathered,out next) && next==gathered,"duplicate plant preserves mask");
        }
        Check(gathered==4095,"all twelve plants required");
        foreach(uint npc in new uint[]{0,EliminationRules.Georgia,EliminationRules.Landy,uint.MaxValue})
            Check(EliminationRules.Herb(npc)==null && !EliminationRules.TryGather(npc,1011,423,55,0,out next),"non-herb identity");
        Check(!EliminationRules.TryGather(EliminationRules.FirstHerb,1011,399,65,4096,out next) && next==4096,"corrupt mask rejected");
        byte killed=0,after;
        for(int slot=0;slot<EliminationRules.DenLackeys;slot++){
            Check(!EliminationRules.DenLeaderReady(killed),"leader cannot appear before every lackey");
            Check(EliminationRules.DenLackey(slot)!=null && EliminationRules.TryDenLackey(killed,slot,out after),"distinct lackey slot");
            EliminationRules.TryDenLackey(killed,slot,out after);killed=after;
            Check(!EliminationRules.TryDenLackey(killed,slot,out after) && after==killed,"duplicate lackey cannot advance progress");
        }
        Check(EliminationRules.DenLeaderReady(killed),"six distinct lackeys reveal leader");
        foreach(int slot in new[]{-1,6,int.MinValue,int.MaxValue})
            Check(EliminationRules.DenLackey(slot)==null && !EliminationRules.TryDenLackey(0,slot,out after),"invalid lackey slot rejected");
        foreach(byte mask in new byte[]{64,127,255})
            Check(!EliminationRules.DenLeaderReady(mask) && !EliminationRules.TryDenLackey(mask,0,out after) && after==mask,"corrupt den progress rejected");
        Console.WriteLine("Elimination rule checks passed: "+checks);return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
