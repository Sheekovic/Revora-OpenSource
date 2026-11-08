using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type SnowRewards=typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeRewards");
    static FloorItem[] SnowDrops(Map map) {return map.FloorItems.Values.Where(d=>(bool)Call(SnowRewards,null,"Managed",d)).ToArray();}
    // Persistence/pickup fixture, not proof of owned combat or team eligibility.
    static bool SnowRewardCommit(GameState p,object run,uint[] frozen,uint[] rewards=null,bool victory=true) {
        lock(p.ItemSyncRoot)lock(SnowGate)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())try {
            if(victory && !(bool)Call(SnowStore,null,"Victory",c,t,p.Map,run,p.Entity.UID,frozen,(ushort)370,(ushort)365,new Func<bool>(()=>true)))return false;
            Call(SnowRewards,null,"Record",c,t,(Guid)Field(run,"Id"),p,rewards??frozen);t.Commit();return true;
        }catch(InvalidOperationException){return false;}catch(PostgresException){return false;}}
    }
    static GameState SnowPlayer(Map town) {
        var p=HousePlayer(town);p.BindWorldMap(1927);p.Entity.X=370;p.Entity.Y=365;EntityTable.SaveEntity(p);return p;
    }
    static void VerifySnowBansheeRewards(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);var map=World(runtime,1927);Server.GamePool=new GameState[0];
        var now=new DateTime(2039,1,4,20,0,0,DateTimeKind.Utc);SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        SnowCall("Initialize",map);Call(SnowRewards,null,"Initialize");Call(SnowRewards,null,"Initialize");
        var random=SnowRewards.GetField("Roll",Hidden);var original=random.GetValue(null);
        try {
            int balls=0,souls=0,accessories=0,stones=0;bool grades=true;
            for(int n=0;n<100;n++){int branch=n,draw=0;random.SetValue(null,new Func<int,int>(max=>draw++==0?branch:0));uint type=(uint)Call(SnowRewards,null,"Draw");
                if(type==1088000)balls++;else if(type==730002)stones++;else if(type>=800000 && type<900000){souls++;uint grade=ConquerItemInformation.BaseInformations[type].PurificationLevel;grades&=grade>=4 && grade<=7;}else accessories++;}
            Check(balls==10 && souls==50 && accessories==10 && stones==30 && grades,"all one hundred branches reproduce Banshee's 10/50/10/30 categories with P4-P7 souls");
            foreach(int invalid in new[]{-1,100}){random.SetValue(null,new Func<int,int>(max=>invalid));SnowRefuses(()=>Call(SnowRewards,null,"Draw"),"invalid Banshee random draw fails closed: "+invalid);}
            int choice=0;random.SetValue(null,new Func<int,int>(max=>max==100?(choice++%2==0?70:60):0));
            var p=SnowPlayer(town);var mate=SnowPlayer(town);uint[] party={p.Entity.UID,mate.Entity.UID};var run=SnowAcquire(map,Guid.NewGuid());
            Check(!SnowRewardCommit(p,run,party,null,false),"unwon Banshee run cannot manufacture rewards");
            Check(!SnowRewardCommit(p,run,party,new[]{p.Entity.UID}),"omitting a frozen teammate rolls back victory");
            Check(!SnowRewardCommit(p,run,party,new[]{p.Entity.UID,p.Entity.UID+900000}),"foreign Banshee recipient rolls back victory");
            Check(SnowRewardCommit(p,run,party) && !SnowRewardCommit(p,run,party),"Banshee victory records exactly one full reward set");
            Parallel.For(0,8,n=>Call(SnowRewards,null,"PayStudy",p));Call(SnowRewards,null,"PayStudy",mate);
            Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==100 && mate.Entity.SubClasses.StudyPoints==100,"each frozen teammate receives exactly 100 Study Points");
            Call(SnowRewards,null,"Restore");var drops=SnowDrops(map);
            Check(drops.Length==3 && drops.Any(d=>d.ItemID==730002 && d.Item.Plus==2) && drops.Count(d=>d.Item.Bound)==1,"victory restores a real +2 Stone, accessory and bound P7");
            var outsider=SnowPlayer(town);Loot(outsider,drops[0]);Check(!drops[0].PickedUpAlready && outsider.Inventory.Count==0,"protected Banshee loot rejects unrelated players");
            var bad=LootPacket(drops[0]);bad[bad.Length-1]^=1;p.Entity.X=drops[0].X;p.Entity.Y=drops[0].Y;FloorItem.Process(bad,p);
            Check(!drops[0].PickedUpAlready && p.Inventory.Count==0,"invalid pickup seal cannot collect managed Banshee loot");
            while(p.Inventory.Count<40)Pack(p,1088001,1);Loot(p,drops[0]);Check(!drops[0].PickedUpAlready && !Fenced(p),"full inventory preserves Banshee entitlement");
            foreach(var filler in p.Inventory.Objects.ToArray())DisRemove(p,filler);
            foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;Parallel.For(0,8,n=>FloorItem.Process(LootPacket(drop),p));}
            Check(!Fenced(p) && p.Inventory.Count==3 && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE owner=@uid AND collector=@uid",p.Entity.UID)==3,"concurrent native pickups each commit one inventory item and receipt");
            p=ReloadBall(p);Call(SnowRewards,null,"PayStudy",p);
            Check(p.Entity.SubClasses.StudyPoints==100 && p.Inventory.Objects.Single(i=>i.ID==730002).Plus==2
                && p.Inventory.Objects.Single(i=>i.ID>=350000 && i.ID<390000).Days==7 && p.Inventory.Objects.Single(i=>i.Bound).ID>=800000,
                "production reload retains +2 Stone, seven-day accessory, bound P7 and Study Points");
            Call(SnowRewards,null,"ClearGround");now=now.AddMinutes(30);
            var artifactPool=(uint[])SnowRewards.GetField("souls",Hidden).GetValue(null);int p7index=Array.FindIndex(artifactPool,id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==7);
            choice=0;random.SetValue(null,new Func<int,int>(max=>max==100?(choice++%2==0?10:0):max==artifactPool.Length?p7index:0));
            var q=SnowPlayer(town);run=SnowAcquire(map,Guid.NewGuid());Check(SnowRewardCommit(q,run,new[]{q.Entity.UID}),"second scheduled victory records a weighted P7 and Dragon Ball");
            Call(SnowRewards,null,"Restore");drops=SnowDrops(map);
            Check(drops.Count(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==2 && drops.Any(d=>d.ItemID==1088000),"weighted P7 and guaranteed P7 are both bound");
            var gift=drops.First(d=>d.Item.Bound);gift.Item.Bound=false;Loot(q,gift);
            Check(Fenced(q) && !gift.PickedUpAlready && q.Inventory.Count==0,"tampered binding fences the session without consuming loot");gift.Item.Bound=true;q=ReloadBall(q);
            Query("CREATE FUNCTION snow_item_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER snow_item_fail BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION snow_item_fail()");
            try{Loot(q,gift);}finally{Query("DROP TRIGGER snow_item_fail ON items;DROP FUNCTION snow_item_fail()");}
            Check(Fenced(q) && !gift.PickedUpAlready && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE owner=@uid AND collector IS NOT NULL",q.Entity.UID)==0,"failed item insert rolls back Banshee pickup receipt");
            q=ReloadBall(q);Loot(q,gift);Check(!Fenced(q) && q.Inventory.Objects.Single().Bound,"reload permits exactly one retry of failed bound soul pickup");
            Call(SnowRewards,null,"ClearGround");Call(SnowRewards,null,"Restore");Check(SnowDrops(map).Length==2,"recovery restores only uncollected Banshee drops");
            q.Entity.SubClasses.StudyPoints=65500;Query("UPDATE entities SET studypoints=65500 WHERE uid=@uid","uid",(long)q.Entity.UID);Call(SnowRewards,null,"PayStudy",q);
            Check(!Fenced(q) && Scalar("SELECT count(*) FROM revora_snow_banshee_study WHERE uid=@uid AND paid_at IS NULL",q.Entity.UID)==1,"Study Point overflow defers the complete 100-point reward");
            q.Entity.SubClasses.StudyPoints=0;Query("UPDATE entities SET studypoints=0 WHERE uid=@uid","uid",(long)q.Entity.UID);Call(SnowRewards,null,"PayStudy",q);Check(q.Entity.SubClasses.StudyPoints==100,"pending Banshee Study reward resumes when balance permits");
            var late=SnowDrops(map)[0];now=now.AddMinutes(3);Loot(q,late);Check(!late.PickedUpAlready && q.Inventory.Count==1,"expired Banshee loot cannot be collected");Call(SnowRewards,null,"Restore");Check(SnowDrops(map).Length==0,"recovery removes expired drops");
            now=now.AddMinutes(30);var f=SnowPlayer(town);run=SnowAcquire(map,Guid.NewGuid());
            foreach(string table in new[]{"revora_snow_banshee_study","revora_snow_banshee_loot"}) {
                Query("CREATE FUNCTION snow_reward_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER snow_reward_fail BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION snow_reward_fail()");
                bool accepted;try{accepted=SnowRewardCommit(f,run,new[]{f.Entity.UID});}finally{Query("DROP TRIGGER snow_reward_fail ON "+table+";DROP FUNCTION snow_reward_fail()");}
                Check(!accepted && SnowCount("state='active'")==1 && Scalar("SELECT count(*) FROM revora_snow_banshee_study WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE owner=@uid",f.Entity.UID)==0,"failed reward insert rolls back victory and every entitlement: "+table);
            }
            Check(SnowRewardCommit(f,run,new[]{f.Entity.UID}),"failed Banshee victory can retry after persistence recovery");
            random.SetValue(null,new Func<int,int>(max=>0));
            foreach(string fault in new[]{"item","evidence","receipt"}) {
                now=now.AddMinutes(30);Call(SnowRewards,null,"ClearGround");
                var hunter=SnowPlayer(town);run=SnowAcquire(map,Guid.NewGuid());
                Check(SnowRewardCommit(hunter,run,new[]{hunter.Entity.UID}),"DragonBall achievement fixture saves a boss victory: "+fault);
                Call(SnowRewards,null,"Restore");var ball=SnowDrops(map).First(d=>d.OwnerUID==hunter.Entity.UID && d.ItemID==1088000);
                var imitation=new FloorItem(true){UID=ball.UID,Item=ball.Item,ItemID=ball.ItemID,MapID=ball.MapID,X=ball.X,Y=ball.Y};
                Check(!(bool)Call(SnowRewards,null,"Pickup",hunter,imitation) && !HasAchievement(hunter,10523),
                    "copied drop fields cannot establish monster-origin achievement: "+fault);
                string table=fault=="item"?"items":fault=="evidence"?"revora_achievement_evidence":"revora_snow_banshee_loot";
                string op=fault=="receipt"?"UPDATE":"INSERT";
                Query("CREATE FUNCTION suppress_dragon_pickup() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_dragon_pickup BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_dragon_pickup()");
                try{Loot(hunter,ball);}finally{Query("DROP TRIGGER suppress_dragon_pickup ON "+table+";DROP FUNCTION suppress_dragon_pickup()");}
                Check(!ball.PickedUpAlready && hunter.Inventory.Count==0 && !HasAchievement(hunter,10523)
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10523",hunter.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE collector=@uid",hunter.Entity.UID)==0,
                    "failed DragonBall pickup rolls back item, collector and achievement while preserving ground loot: "+fault);
                hunter=ReloadBall(hunter);hunter.Entity.X=ball.X;hunter.Entity.Y=ball.Y;
                Parallel.For(0,8,n=>FloorItem.Process(LootPacket(ball),hunter));
                Check(!Fenced(hunter) && hunter.Inventory.Objects.Count(i=>i.ID==1088000)==1 && HasAchievement(hunter,10523)
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10523",hunter.Entity.UID)==1,
                    "concurrent retry collects one monster DragonBall and its immediate achievement: "+fault);
                hunter=ReloadBall(hunter);hunter.Entity.MyAchievement.Send();FloorItem.Process(LootPacket(ball),hunter);
                Check(hunter.Inventory.Objects.Count(i=>i.ID==1088000)==1 && HasAchievement(hunter,10523)
                    && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE collector=@uid",hunter.Entity.UID)==1,
                    "full reload and replay preserve one DragonBall and historical pickup credit: "+fault);
            }

        } finally {Call(SnowRewards,null,"ClearGround");random.SetValue(null,original);SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
}
