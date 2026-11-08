using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type AbyssRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialRules");
    static object AbyssProgress(int step,int meters=0,int spirits=0,int forms=0) {
        var phase=AbyssRules.GetNestedType("Stage",BindingFlags.NonPublic);
        try{return Activator.CreateInstance(AbyssRules.GetNestedType("Progress",BindingFlags.NonPublic),Instance,null,new object[]{Enum.ToObject(phase,step),meters,spirits,forms},null);}
        catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();throw;}
    }
    static int AbyssValue(object value,string field){return Convert.ToInt32(value.GetType().GetField(field,Instance).GetValue(value));}
    static Array AbyssCost(string method,uint id){return (Array)Call(AbyssRules,null,method,id);}
    static uint AbyssType(object material){return (uint)material.GetType().GetField("Type",Instance).GetValue(material);}
    static int AbyssCount(object material){return (ushort)material.GetType().GetField("Count",Instance).GetValue(material);}
    static void VerifyAbyssRules() {
        foreach(byte reborn in new byte[]{0,1,2,3})foreach(byte level in new byte[]{1,110,119,120,130,140})
            Check((bool)Call(AbyssRules,null,"Eligible",reborn,level)==(reborn==1 && level>=120),"Abyss admission requires exactly one rebirth and level 120: "+reborn+"/"+level);
        var stock=new Dictionary<uint,int>{{722723,117},{722724,117},{722725,117}};
        var fights=new Dictionary<uint,int>();for(int i=0;i<3;i++)AbyssCraft(722731,stock,fights);
        Check(stock[722731]==3 && new uint[]{722723,722724,722725}.All(id=>stock[id]==0)
            && stock.Where(pair=>pair.Key!=722731).All(pair=>pair.Value==0),"independent recursive three-vigor journey consumes exactly 117 of each base material without phantom leftovers");
        Check(fights.Count==4 && fights[3632]==81 && fights[3633]==27 && fights[3634]==9 && fights[3635]==3,
            "full material chain requires 81 Hill Spirits, 27 Swift Demons, nine Banshees and three Cleansing Devils");
        foreach(var pair in new[]{new uint[]{722721,722722},new uint[]{722728,722726},new uint[]{722730,722729}}) {
            var cost=AbyssCost("ExchangeCost",pair[0]);Check(cost.Length==1 && AbyssType(cost.GetValue(0))==pair[1] && AbyssCount(cost.GetValue(0))==3,"Arthur uses the installed three-for-one recipe: "+pair[0]);
        }
        var satan=AbyssCost("SummonCost",3644);Check(satan.Length==1 && AbyssType(satan.GetValue(0))==722727 && AbyssCount(satan.GetValue(0))==1,"first Satan summon requires exactly one Squama Bead");
        foreach(string method in new[]{"ExchangeCost","SummonCost","SummonReward"}) {
            bool denied=false;try{Call(AbyssRules,null,method,0u);}catch(ArgumentException){denied=true;}Check(denied,"unknown material recipe or reward fails closed: "+method);
        }
        int[] weights={1,6,20,500};
        for(uint family=3600;family<=3631;family++)Check((int)Call(AbyssRules,null,"GrievanceWeight",family)==weights[(family-3600)%4],"native named grievance group has the published weight: "+family);
        foreach(uint family in new uint[]{0,3599,3632,3633,3635,3644,uint.MaxValue})Check((int)Call(AbyssRules,null,"GrievanceWeight",family)==0,"unrelated and summoned bosses cannot accelerate grievance: "+family);
        var start=AbyssProgress(1);Check(ReferenceEquals(Call(AbyssRules,null,"Kill",start,3603u,true),start),"material stage cannot skip to grievance through a boss kill");
        foreach(int vigor in new[]{0,1,2,4,50})Check(ReferenceEquals(Call(AbyssRules,null,"TurnIn",start,3601u,vigor),start),"stage-one transaction must select the exact three-vigor debit: "+vigor);
        var grievance=Call(AbyssRules,null,"TurnIn",start,3601u,3);Check(AbyssValue(grievance,"Step")==2 && AbyssValue(grievance,"Meters")==0,"three-vigor turn-in starts an empty owned grievance stage");
        for(int i=0;i<79;i++)grievance=Call(AbyssRules,null,"Kill",grievance,3603u,false);
        Check(AbyssValue(grievance,"Meters")==39500 && ReferenceEquals(Call(AbyssRules,null,"TurnIn",grievance,3602u,0),grievance),"79 rare-lord kills cannot pass Bryan's 40,000-meter requirement");
        grievance=Call(AbyssRules,null,"Kill",grievance,3603u,false);grievance=Call(AbyssRules,null,"Kill",grievance,3603u,false);
        Check(AbyssValue(grievance,"Meters")==40000,"grievance caps exactly and never grows beyond the native counter");
        Check(AbyssValue(Call(AbyssRules,null,"Kill",AbyssProgress(2,39999),3603u,false),"Meters")==40000,"over-completing kill preserves the exact counter cap");
        var spirits=Call(AbyssRules,null,"TurnIn",grievance,3602u,0);
        uint[] order={3636,3637,3638,3639,3640,3641,3642,3643};
        for(int round=0;round<9;round++)foreach(uint family in order) {
            Check((uint)Call(AbyssRules,null,"NextSpirit",spirits)==family
                && ReferenceEquals(Call(AbyssRules,null,"Kill",spirits,family,false),spirits)
                && ReferenceEquals(Call(AbyssRules,null,"Kill",spirits,family==3643?3636u:family+1,true),spirits),
                "owned ordered spirit requirement holds in round "+(round+1)+" for "+family);
            spirits=Call(AbyssRules,null,"Kill",spirits,family,true);
        }
        Check(AbyssValue(spirits,"Spirits")==72 && (uint)Call(AbyssRules,null,"NextSpirit",spirits)==0
            && ReferenceEquals(Call(AbyssRules,null,"Kill",spirits,3643u,true),spirits),"exactly nine complete ordered rounds exhaust the binding curse");
        var final=Call(AbyssRules,null,"TurnIn",spirits,3603u,0);
        for(uint family=3644;family<=3646;family++) {
            Check(ReferenceEquals(Call(AbyssRules,null,"Kill",final,family,false),final)
                && ReferenceEquals(Call(AbyssRules,null,"Kill",final,family==3646?3644u:family+1,true),final),"foreign or out-of-order Satan form cannot complete the quest: "+family);
            final=Call(AbyssRules,null,"Kill",final,family,true);
            Check(AbyssValue(final,"SatanForms")==family-3643 && AbyssValue(final,"Step")== (family==3646?5:4),"Satan chain advances one verified form at a time: "+family);
        }
        Check(ReferenceEquals(Call(AbyssRules,null,"Kill",final,3646u,true),final),"completed quest never emits another logical completion from repeated death");
        foreach(uint family in Enumerable.Range(3600,48).Select(n=>(uint)n))
            Check((bool)Call(AbyssRules,null,"BeadSource",family)==new uint[]{3603,3607,3611,3619}.Contains(family),"only four catalog-backed lords qualify as bead sources: "+family);
        foreach(int[] bad in new[]{new[]{0,0,0,0},new[]{6,40000,72,3},new[]{1,1,0,0},new[]{2,-1,0,0},new[]{2,40001,0,0},new[]{2,0,1,0},new[]{3,0,0,0},new[]{3,40000,73,0},new[]{3,40000,1,1},new[]{4,40000,71,0},new[]{4,40000,72,3},new[]{5,40000,72,2}}) {
            bool denied=false;try{AbyssProgress(bad[0],bad[1],bad[2],bad[3]);}catch(ArgumentException){denied=true;}Check(denied,"malformed saved-stage candidate fails before any mutation: "+string.Join(",",bad));
        }
    }
    static void AbyssCraft(uint output,Dictionary<uint,int> stock,Dictionary<uint,int> fights) {
        uint family=output==722722?3632u:output==722726?3633u:output==722729?3634u:output==722731?3635u:0;
        var cost=AbyssCost(family==0?"ExchangeCost":"SummonCost",family==0?output:family);
        foreach(object material in cost) {
            uint type=AbyssType(material);int amount=AbyssCount(material);if(!stock.ContainsKey(type))stock[type]=0;
            while(stock[type]<amount) {if(type>=722723 && type<=722725)throw new Exception("Insufficient fixture base material");AbyssCraft(type,stock,fights);}
            stock[type]-=amount;
        }
        if(family!=0) {if((uint)Call(AbyssRules,null,"SummonReward",family)!=output)throw new Exception("Incorrect summon product");if(!fights.ContainsKey(family))fights[family]=0;fights[family]++;}
        if(!stock.ContainsKey(output))stock[output]=0;stock[output]++;
    }
}
