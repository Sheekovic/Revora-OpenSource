using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;

internal static partial class PotencyHuntTests
{
    static readonly Type ChiRules=typeof(Entity).Assembly.GetType("Revora.ChiBonuses");
    static void ApplyChi(GameState p, uint percentage=100) { Call(ChiRules,null,"Apply",p,percentage); }
    static string Stats(GameState p)
    {
        var e=p.Entity;
        return string.Join(",",new object[]{e.ItemHP,e.MaxHitpoints,e.MinAttack,e.MaxAttack,e.MagicAttack,e.Defence,e.MagicDefence,e.BaseMagicDefence,e.CriticalStrike,e.SkillCStrike,e.Immunity,e.Breaktrough,e.Counteraction,e.PhysicalDamageIncrease,e.MagicDamageIncrease,e.PhysicalDamageDecrease,e.MagicDamageDecrease});
    }
    static void ZeroChi(GameState p)
    {
        var e=p.Entity;e.ItemHP=e.BaseMinAttack=e.BaseMaxAttack=e.BaseMagicAttack=e.BaseMagicDefence=0;
        e.CriticalStrike=e.SkillCStrike=e.Immunity=0;e.Breaktrough=e.Counteraction=0;
        e.PhysicalDamageIncrease=e.MagicDamageIncrease=e.PhysicalDamageDecrease=e.MagicDamageDecrease=0;
    }
    static void VerifyCultivation(Map map)
    {
        // Exercise every production bucket at both boundaries without lucky rolls.
        var general=typeof(Revora.Network.GamePackets.GeneralData);
        foreach(Enums.ChiAttribute attribute in Enum.GetValues(typeof(Enums.ChiAttribute))) {
            if(attribute==Enums.ChiAttribute.None)continue;
            int min=Enums.ChiMinValues(attribute),max=Enums.ChiMaxValues(attribute);
            for(int bucket=0;bucket<4;bucket++) {
                for(int edge=0;edge<2;edge++) {
                    int calls=0,low=0,high=0;
                    Func<int,int,bool> rate=(chance,total)=>calls++==bucket;
                    Func<int,int,int> next=(lo,hi)=>{low=lo;high=hi;return edge==0?lo:hi-1;};
                    ushort value=(ushort)Call(general,null,"RollChiValue",attribute,next,rate);
                    float step=(max-min)/10f;
                    int expectedLow=bucket==0?(int)(max-step):bucket==1?(int)(max-2*step):bucket==2?(int)(max-3*step):min;
                    int expectedHigh=bucket==0||bucket==3?max+1:bucket==1?(int)(max-step):(int)(max-2*step);
                    Check(low==expectedLow && high==expectedHigh && value>=min && value<=max
                        && value==(edge==0?low:high-1),"Chi roll legal inclusive endpoints: "+attribute+" bucket "+bucket+" edge "+edge);
                }
            }
        }
        int fullScore=0;
        for(int power=1;power<=4;power++) {
            var rolled=new ChiPowerStructure((Enums.ChiPowerType)power);
            for(int slot=0;slot<4;slot++) {
                var attribute=(Enums.ChiAttribute)(slot+1);
                ushort value=(ushort)Call(general,null,"RollChiValue",attribute,
                    (Func<int,int,int>)((lo,hi)=>hi-1),(Func<int,int,bool>)((chance,total)=>true));
                rolled.Attributes[slot]=new ChiAttribute(attribute,value);
            }
            rolled.CalculatePoints();fullScore+=rolled.Points;
        }
        Check(fullScore==1600,"production Chi rolls can produce the Grandmaster score without exceeding attribute limits");
        var p=RunePlayer(map);
        Check(p.Account.State==AccountTable.AccountState.Player,"cultivation uses an ordinary non-GM player");
        Check(p.Entity.BattlePowerCalc(null)==0 && p.Entity.BattlePowerCalc(new Entity(EntityFlag.Monster,false))==0,"battle power rejects null and ownerless entities before reading state");
        p.Entity.Level=100;p.Entity.ExtraBattlePower=20;
        Check(p.Entity.BattlePower==120,"twenty shared battle power contributes twenty, not forty");
        p.Entity.GuildSharedBp=256;
        Check(p.Entity.BattlePower==135,"guild contribution caps at fifteen instead of truncating to a byte");
        var other=RunePlayer(map);other.Entity.Level=80;other.Entity.ExtraBattlePower=7;other.Entity.GuildSharedBp=5;
        Check(p.Entity.BattlePowerCalc(other.Entity)==92,"battle power uses the requested entity's contributions exclusively");
        p.Entity.ExtraBattlePower=0;p.Entity.GuildSharedBp=0;
        Check(p.Entity.BattlePower==100,"removing shared power returns exactly to the level base");
        var armor=new Revora.Network.GamePackets.ConquerItem(true){UID=123,ID=130009,Position=3,Plus=12,IsWorn=true,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13};
        p.Equipment.Objects[2]=armor;
        Check(p.Entity.BattlePower==120,"super plus-twelve armor with two super gems contributes twenty battle power");
        armor.IsWorn=false;Check(p.Entity.BattlePower==100,"unworn alternate gear contributes no battle power");p.Equipment.Objects[2]=null;
        p.Entity.Level=140;
        for(int i=0;i<4;i++)p.ChiPowers.Add(new ChiPowerStructure((Enums.ChiPowerType)(i+1)));
        foreach(var power in p.ChiPowers) power.Attributes=new[]{
            new ChiAttribute(Enums.ChiAttribute.CriticalStrike,200),new ChiAttribute(Enums.ChiAttribute.Immunity,200),
            new ChiAttribute(Enums.ChiAttribute.Breakthrough,200),new ChiAttribute(Enums.ChiAttribute.HPAdd,3500)};
        p.ChiData.DragonRank=p.ChiData.PhoenixRank=p.ChiData.TigerRank=p.ChiData.TurtleRank=100;
        ApplyChi(p);
        Check(p.Entity.CriticalStrike==8000 && p.Entity.Immunity==8000 && p.Entity.Breaktrough==800 && p.Entity.ItemHP==14000,"all sixteen Chi attributes sum with consistent rate units");
        ZeroChi(p);ApplyChi(p,50);
        Check(p.Entity.CriticalStrike==4000 && p.Entity.Immunity==4000 && p.Entity.Breaktrough==400 && p.Entity.ItemHP==7000,"restricted Chi contribution scales criticals and immunity by 100, not 105");
        foreach(var power in p.ChiPowers){power.CalculatePoints();Check(power.Points==400,"maximum legal Chi attributes score 400");power.Attributes=new ChiAttribute[4];power.Points=0;}
        foreach(int rank in new[]{0,1,3,4,15,16,50,51}) {
            ZeroChi(p);p.ChiData.DragonRank=p.ChiData.PhoenixRank=p.ChiData.TigerRank=p.ChiData.TurtleRank=rank;ApplyChi(p);
            uint hp=rank==0||rank>50?0u:rank<=3?5000u:rank==4?3000u:rank==15?2000u:1500u;
            int crit=rank==0||rank>50?0:rank<=3?1500:rank==4?1100:rank==15?800:500;
            Check(p.Entity.ItemHP==hp && p.Entity.CriticalStrike==crit && p.Entity.Breaktrough==crit/10,"Chi ranking boundary "+rank);
        }
        p.ChiData.DragonRank=p.ChiData.PhoenixRank=p.ChiData.TigerRank=p.ChiData.TurtleRank=100;
        foreach(int score in new[]{398,399,400}) {
            foreach(var power in p.ChiPowers)power.Points=score;ZeroChi(p);ApplyChi(p);
            Check(p.Entity.ItemHP==(score==400?5000u:0u) && p.Entity.CriticalStrike==(score==400?1500:0),"only full 400 score receives rank-independent top-three bonuses: "+score);
        }
        p.ChiPowers.RemoveRange(1,3);p.ChiPowers[0].Points=0;ZeroChi(p);
        p.ChiData.DragonRank=p.ChiData.PhoenixRank=p.ChiData.TigerRank=p.ChiData.TurtleRank=1;ApplyChi(p);
        Check(p.Entity.ItemHP==5000 && p.Entity.BaseMaxAttack==0 && p.Entity.CriticalStrike==0 && p.Entity.Breaktrough==0,"locked Phoenix, Tiger and Turtle cannot grant ranking bonuses");
        var old=ChiTable.AllData;try {
            ChiTable.AllData=new System.Collections.Concurrent.ConcurrentDictionary<uint,ChiTable.ChiData>();
            p.ChiData.UID=p.Entity.UID;ChiTable.AllData.TryAdd(p.Entity.UID,p.ChiData);ChiTable.Sort();
            Check(ChiTable.Dragon.Length==1 && ChiTable.Phoenix.Length==0 && p.ChiData.PhoenixRank==0,"locked powers are absent from Chi leaderboards");
        } finally {ChiTable.AllData=old;ChiTable.Sort();}
        Revora.Database.JiangHu.LoadStatus();
        var a=Revora.Database.JiangHu.Attributes.First(x=>x.Value.Level==6 && x.Value.Type==(byte)Revora.Game.JiangHu.JiangStages.AtributesType.PAttack);
        var b=Revora.Database.JiangHu.Attributes.First(x=>x.Value.Level==6 && x.Value.Type==(byte)Revora.Game.JiangHu.JiangStages.AtributesType.MAttack);
        var j=new Revora.Game.JiangHu(0);var stage=j.Stagers[0];stage.Activate=true;
        for(int i=0;i<4;i++){var item=i<2?a:b;var star=stage.Stars[i];star.Activate=true;star.UID=item.Key;star.Level=6;star.Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)item.Value.Type;}
        ZeroChi(p);j.CreateStatusAtributes(p.Entity);
        Check(j.Inner_Strength==2200 && stage.Aligments.Count==2,"adjacent AA BB groups both receive alignment and score bonuses");
        Check(p.Entity.BaseMinAttack==2*(a.Value.Power+a.Value.Power/10) && p.Entity.BaseMagicAttack==2*(b.Value.Power+b.Value.Power/10),"each aligned pair adds the published ten-percent stat bonus");
        stage.Stars[1].Activate=false;stage.Stars[2].UID=a.Key;stage.Stars[2].Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)a.Value.Type;stage.Stars[3].Activate=false;
        ZeroChi(p);j.CreateStatusAtributes(p.Entity);
        Check(j.Inner_Strength==1000 && stage.Aligments.Count==0 && p.Entity.BaseMinAttack==a.Value.Power*2,"inactive gaps break alignment rather than joining separated stars");
        int[] bonuses={0,0,10,13,15,18,21,25,30,50};
        for(int length=2;length<=9;length++) {
            for(int i=0;i<9;i++){var star=stage.Stars[i];star.Activate=i<length;star.UID=a.Key;star.Level=6;star.Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)a.Value.Type;}
            ZeroChi(p);j.CreateStatusAtributes(p.Entity);
            Check(p.Entity.BaseMinAttack==length*(a.Value.Power+a.Value.Power*bonuses[length]/100),"JiangHu stat alignment length "+length);
        }
        foreach(var st in j.Stagers){st.Activate=true;foreach(var star in st.Stars){star.Activate=true;star.UID=a.Key;star.Level=6;star.Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)a.Value.Type;}}
        ZeroChi(p);j.CreateStatusAtributes(p.Entity);Check(j.Inner_Strength==81000,"full JiangHu score remains 81000");
        p.ChiPowers.Clear();p.Entity.MyJiang=null;
        p.Entity.BaseMinAttack=100;p.Entity.BaseMaxAttack=300;p.Entity.Strength=20;p.GemAlgorithm();
        Check(p.Entity.MinAttack==120 && p.Entity.MaxAttack==320,"equipment minimum and maximum attack are not reversed");
        p.Entity.MyJiang=j;
        p.ChiPowers.Add(new ChiPowerStructure(Enums.ChiPowerType.Dragon){Attributes=new[]{new ChiAttribute(Enums.ChiAttribute.CriticalStrike,200),new ChiAttribute(Enums.ChiAttribute.Immunity,200),new ChiAttribute(Enums.ChiAttribute.HPAdd,3500),new ChiAttribute(Enums.ChiAttribute.AddMagicAttack,2500)}});
        p.LoadItemStats();string expected=Stats(p);
        Check(p.Entity.CriticalStrike>=2000 && p.Entity.MinAttack>0 && p.Entity.MagicAttack>=2500,"concurrency fixture contains real Chi and aligned JiangHu attack bonuses");
        Parallel.For(0,64,i=>p.LoadItemStats());
        Check(Stats(p)==expected,"concurrent recalculation of one character cannot lose or duplicate stats");
        p.Entity.MyJiang=null;p.ChiPowers.Clear();ZeroChi(p);
        typeof(InnerPower).GetProperty("FinalMagicAttack").GetSetMethod(true).Invoke(p.InnerPower,new object[]{123u});
        typeof(InnerPower).GetProperty("FinalMagicDefense").GetSetMethod(true).Invoke(p.InnerPower,new object[]{234u});
        typeof(InnerPower).GetProperty("FinalDefense").GetSetMethod(true).Invoke(p.InnerPower,new object[]{321u});
        p.AddInnerPowerAtributes(p.InnerPower);
        Check(p.Entity.MagicDamageIncrease==123 && p.Entity.MagicDamageDecrease==234 && p.Entity.PhysicalDamageDecrease==321,"Inner Power final magic stats count once and do not use physical defense");
        for(byte vip=5;vip<=7;vip++) {
            string attackStats=null;
            foreach(byte cls in new byte[]{15,25,45,55,65,75,85,135,145,165}) {
                p.Entity.Class=cls;p.Entity.VIPLevel=vip;p.LoadItemStats();
                string actual=p.Entity.CriticalStrike+","+p.Entity.SkillCStrike+","+p.Entity.Immunity;
                if(attackStats==null)attackStats=actual;
                Check(actual==attackStats,"VIP "+vip+" has equal critical/immunity bonuses for class "+cls);
            }
        }
    }
}
