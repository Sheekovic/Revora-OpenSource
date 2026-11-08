using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type PkRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.LocalPkRules");
    static void ClassNpc(GameState p,byte option){MoonNpc(p,705,option);}
    static GameState ClassPlayer(Map town,byte profession,byte level=140){var p=PkPlayer(town,level);p.Entity.Class=profession;return p;}
    static void VerifyClassPk(string runtime,Map town) {
        PreparePkReloadSchema();
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL); CREATE TABLE status (LIKE public.status INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID=new Counter(7000000);Server.GamePool=new GameState[0];Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        Call(Pk,null,"PlaceGeneral");Check(town.Npcs[705].Mesh==1360 && Kernel.GetDistance(town.Npcs[705].X,town.Npcs[705].Y,452,291)<=8,"genuine installed Class PK Envoy is restored near its official plaza");
        int[] families={10,20,40,50,60,70,80,130,140,160};ulong[] flags={39,38,40,43,114,122,154,41,42,203};
        for(int i=0;i<families.Length;i++) {
            for(int step=0;step<=5;step++)Check((int)Call(PkRules,null,"ClassKind",families[i]+step)==i+10,"class family/promotion identity "+(families[i]+step));
            Check((ulong)Call(PkRules,null,"Halo",i+10)==flags[i],"class halo uses its installed effect index "+families[i]);
        }
        Check((int)Call(PkRules,null,"ClassKind",100)==-1 && (int)Call(PkRules,null,"ClassKind",150)==-1 && (int)Call(PkRules,null,"ClassKind",30)==-1,"unspecialized and unused client-only professions are not silently assigned to another class");
        pkStart=pkNow=new DateTime(2026,9,15,2,15,0,DateTimeKind.Utc);var e=NewPk(runtime);Call(Pk,e,"Tick");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE kind BETWEEN 10 AND 19")==40,"Monday registration opens four level divisions for each supported class");
        Check((DateTime)Call(Pk,null,"ScheduleStart",new DateTime(2026,12,15,3,15,0,DateTimeKind.Utc),10)==new DateTime(2026,12,15,3,15,0,DateTimeKind.Utc),"class registration follows Pacific standard time in winter");
        Query("UPDATE revora_pk_wars SET valuable_reward=false WHERE kind=11"); // Old Warrior contract before enrollment.
        var winners=families.Select(c=>ClassPlayer(town,(byte)(c+5))).ToArray();var rivals=families.Select(c=>ClassPlayer(town,(byte)(c+5))).ToArray();
        var novice=ClassPlayer(town,100);ClassNpc(novice,1);Check(PkEntries(novice)==0 && novice.Entity.MapID==1002,"a novice Taoist must choose its school before registering");
        var fake=ClassPlayer(town,165);fake.Fake=true;ClassNpc(fake,1);Check(PkEntries(fake)==0,"fake adventurers cannot enter Class PK");fake.Fake=false;
        for(int i=0;i<winners.Length;i++){ClassNpc(winners[i],1);ClassNpc(rivals[i],1);}
        PkTick(e,.1);
        Check(winners.Select(p=>p.Entity.MapID).Distinct().Count()==10 && winners.All(p=>p.Map.BaseID==1730 && p.Entity.MapID!=1730),"ten owned class arenas remain distinct from each other and Power Arena");
        Check(winners[7].Entity.MapID!=winners[8].Entity.MapID && winners[9].Entity.MapID==rivals[9].Entity.MapID,"Water, Fire and Wind Walker entrants reach their proper separate fields");
        foreach(var p in winners){LabNear(p,706);Check(Kernel.GetDistance(p.Entity.X,p.Entity.Y,p.Map.Npcs[706].X,p.Map.Npcs[706].Y)<=6,"class escort is reachable through ordinary jumps for "+p.Entity.Class);}
        uint hp=rivals[0].Entity.Hitpoints;EpkDamage(winners[0],rivals[0],100);Check(rivals[0].Entity.Hitpoints==hp,"all registration remains peaceful until 19:30");
        Check(winners.All(p=>!HasAchievement(p,11131) && !HasAchievement(p,11116) && !HasAchievement(p,11117)),"class attendance creates no winner or unrelated weekly/monthly achievement");
        PkTick(e,15);ClassNpc(fake,1);Check(PkEntries(fake)==0,"class admission closes at exactly the battle start");
        winners[0].Attackable=rivals[0].Attackable=true;
        Check(Revora.Game.Attacking.Handle.CanAttack(winners[0].Entity,rivals[0].Entity,null,true) && !(bool)Call(Pk,null,"BlockSpell",winners[0]),"normal attack and spell entry points open for the fighting class field");
        EpkDamage(winners[1],rivals[0],100);Check(rivals[0].Entity.Hitpoints==hp,"different class fields cannot damage one another");
        Parallel.For(0,winners.Length,i=>{EpkDamage(winners[i],rivals[i],100);rivals[i].Entity.Die(winners[i].Entity);});
        Query("ALTER TABLE revora_pk_wars ADD CONSTRAINT class_result_failure CHECK(kind<>19 OR winner=0) NOT VALID");PkTick(e,15.1);
        Check(Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid",winners[9].Entity.UID)==0,"failed Wind Walker result cannot invent its achievement or honor");
        Query("ALTER TABLE revora_pk_wars DROP CONSTRAINT class_result_failure");PkTick(e,15.2);
        Check(winners.All(p=>p.Entity.MapID==1002) && CtfCount("SELECT count(*) FROM revora_pk_wars WHERE winner<>0")==10,"concurrent class battles produce ten distinct persisted champions");
        for(int i=0;i<winners.Length;i++) {
            var p=winners[i];EvidenceRefresh(p);Call(Pk,null,"SynchronizeHonors",p,pkNow);
            Check(HasAchievement(p,11131) && p.Entity.ContainsFlag(flags[i]) && flags.Where(f=>f!=flags[i]).All(f=>!p.Entity.ContainsFlag(f)),"only the winning class halo and Class PK achievement are granted for "+p.Entity.Class);
        }
        Check(rivals.All(p=>!HasAchievement(p,11131)),"losers cannot inherit their class champion's evidence");
        SavePkProfileBeforeClaim(winners[0]);
        Query("ALTER TABLE revora_pk_claims ADD CONSTRAINT class_claim_failure CHECK(cp=0) NOT VALID");ClassNpc(winners[0],3);
        Check(Fenced(winners[0]) && winners[0].Entity.ConquerPoints==0 && winners[0].Inventory.Count==0,"failed class purse transaction rolls back its currencies and bound items");
        Query("ALTER TABLE revora_pk_claims DROP CONSTRAINT class_claim_failure");DemonReset(winners[0]);MoonNear(winners[0],705);
        Parallel.For(0,8,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=705,OptionID=3},winners[0]));
        Check(winners[0].Entity.ConquerPoints==50000 && winners[0].Entity.RevoraPoint==500 && winners[0].Entity.Money==500000 && winners[0].Inventory.Count==6,
            "concurrent Class PK claims install one saved ordinary-player purse");
        Check(winners[0].Inventory.Objects.Count(item=>item.Bound && item.ID==722136)==5 && PkSoul(winners[0]) && Scalar("SELECT money FROM entities WHERE uid=@uid",winners[0].Entity.UID)==500000,
            "Class PK delivers five bound EXP Balls, a bound P7 soul and durable currency");
        ReloadPkPrize(winners[0]);winners[0]=ReconnectPkPrize(winners[0],true);
        Call(Pk,null,"SynchronizeHonors",winners[0],pkStart.AddMinutes(15.1).AddDays(7));Check(!winners[0].Entity.ContainsFlag(39),"class halo expires exactly seven days from its saved result");
        // Next week's field exercises frozen eligibility and a normal escort exit.
        pkStart=pkNow=pkStart.AddDays(7);Call(Pk,e,"Tick");var changing=ClassPlayer(town,15);var promotion=ClassPlayer(town,14);var levelChange=ClassPlayer(town,45,129);var levelRival=ClassPlayer(town,45,129);
        foreach(var p in new[]{changing,promotion,levelChange,levelRival})ClassNpc(p,1);PkTick(e,.1);
        promotion.Entity.Class=15;changing.Entity.Class=25;levelChange.Entity.Level=130;PkTick(e,.2);
        Check(changing.Entity.MapID==1002 && levelChange.Entity.MapID==1002 && promotion.Entity.MapID!=1002,"changing family or division forfeits admission while an in-family promotion remains eligible");
        LabNear(promotion,706);Npcs.GetDialog(new NpcRequest(true){NpcID=706,OptionID=4},promotion);
        Check(promotion.Entity.MapID==1002 && promotion.Entity.OnDeath==null,"ordinary class escort exit releases tournament ownership");
        // Every old class/nobility purse handler is intercepted even off its former arena.
        foreach(uint id in new[]{706u,745u,70500u}) {town.AddNpc(new NpcSpawn{UID=id,MapID=1002,X=novice.Entity.X,Y=novice.Entity.Y,Mesh=1370,Type=Enums.NpcType.Talker});novice.ActiveNpc=id;
            Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=1},novice);}
        Check(novice.Entity.ConquerPoints==0 && novice.Entity.RevoraPoint==0 && novice.Entity.Money==0,"old class/nobility NPC identities cannot pay unverified purses");
        e=NewPk(runtime);ClassNpc(winners[9],3);Check(winners[9].Entity.ConquerPoints==50000 && winners[9].Inventory.Count==6 && PkSoul(winners[9]),"unclaimed Wind Walker champion purse survives coordinator restart");
        ClassNpc(winners[1],3);ClassNpc(winners[1],3);
        Check(winners[1].Inventory.Count==5 && winners[1].Inventory.Objects.All(i=>i.ID==722136 && i.Bound)
            && Scalar("SELECT count(*) FROM revora_pk_claims WHERE uid=@uid AND cardinality(rewards)=5",winners[1].Entity.UID)==1,
            "older class tournament retains its earned EXP-only item bundle across restart and replay");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE state='open'")==0,"restart cancels unfinished class divisions");
        Pk.GetField("Current",Hidden).SetValue(null,null);
    }
}
