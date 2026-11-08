using System;
using Revora.Game.Features;

internal static class RulesTests
{
    private static void Check(bool valid,string name) { if(!valid) throw new Exception(name); }
    private static int Main()
    {
        byte[] classes={20,10,140,130,40,50,60,70,80,160};
        uint[] certificates={710016,710017,710018,710019,710020,710634,711335,711599,3004946,3301711};
        uint[] amulets={710011,710012,710013,710014,710015,710635,711336,711600,3004947,3301712};
        uint[] families={7001,7002,7004,7005,7003};
        AncientDevilRules.Trial trial;uint output;
        for(int school=0;school<10;school++)
        {
            for(int rank=0;rank<=5;rank++)
            {
                byte profession=(byte)(classes[school]+rank);
                for(uint guard=300011;guard<=300015;guard++)
                {
                    bool permitted=school>=5 || guard==300011+school;
                    bool accepted=AncientDevilRules.TryTrial(profession,50,guard,out trial);
                    Check(accepted==permitted,"class guard eligibility");
                    Check(!accepted ? trial==null : trial.Profession==profession && trial.Guard==guard
                        && trial.Family==families[guard-300011] && trial.Certificate==certificates[school]
                        && trial.Amulet==amulets[school],"trial contract");
                }
                Check(AncientDevilRules.TryExchange(profession,certificates[school],out output)
                    && output==amulets[school],"own certificate exchange");
                Check(!AncientDevilRules.TryExchange(profession,certificates[(school+1)%10],out output)
                    && output==0,"foreign certificate cannot change class");
            }
            Check(!AncientDevilRules.TryTrial(classes[school],49,300011,out trial),"minimum level");
            Check(!AncientDevilRules.TryTrial(classes[school],140,300010,out trial),"unknown guard below range");
            Check(!AncientDevilRules.TryTrial(classes[school],140,300016,out trial),"unknown guard above range");
        }
        for(int code=0;code<=255;code++)
        {
            bool known=false;
            foreach(byte profession in classes) if(code>=profession && code<=profession+5) known=true;
            if(known)continue;
            for(uint guard=300011;guard<=300015;guard++)
                Check(!AncientDevilRules.TryTrial((byte)code,140,guard,out trial) && trial==null,"unknown class");
            Check(!AncientDevilRules.TryExchange((byte)code,710016,out output) && output==0,"unknown exchange class");
        }
        // Every possible omitted class and both selection orders are legitimate.
        for(int omitted=0;omitted<10;omitted++)
        {
            var selected=new AncientDevilRules.Offering[9];int cursor=0;
            for(int i=0;i<10;i++)if(i!=omitted)selected[cursor++]=new AncientDevilRules.Offering((uint)(100+i),amulets[i]);
            Check(AncientDevilRules.ValidOffering(selected),"nine distinct classes");
            Array.Reverse(selected);Check(AncientDevilRules.ValidOffering(selected),"order independent");
            var old=selected[8];
            selected[8]=new AncientDevilRules.Offering(old.Uid,selected[0].Type);
            Check(!AncientDevilRules.ValidOffering(selected),"duplicate class");
            selected[8]=new AncientDevilRules.Offering(selected[0].Uid,old.Type);
            Check(!AncientDevilRules.ValidOffering(selected),"duplicate physical item");
            selected[8]=new AncientDevilRules.Offering(0,old.Type);
            Check(!AncientDevilRules.ValidOffering(selected),"zero identity");
            foreach(uint invalid in new uint[]{723583,710016,0,uint.MaxValue})
            {
                selected[8]=new AncientDevilRules.Offering(old.Uid,invalid);
                Check(!AncientDevilRules.ValidOffering(selected),"unrelated item or certificate");
            }
            selected[8]=null;Check(!AncientDevilRules.ValidOffering(selected),"missing item");
        }
        Check(!AncientDevilRules.ValidOffering(null),"missing selection");
        foreach(int size in new[]{0,1,8,10,40})
            Check(!AncientDevilRules.ValidOffering(new AncientDevilRules.Offering[size]),"exactly nine items");
        Console.WriteLine("PASS: class/guard matrix, certificate exchanges, all nine-class combinations and invalid offerings.");
        return 0;
    }
}
