using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests
{
    static readonly Type PurpleRewards=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurpleBansheeRewards");
    static FloorItem[] PurpleDrops(GameState p) {return p.Map.FloorItems.Values.Where(d=>(bool)Call(PurpleRewards,null,"Managed",d)).ToArray();}
    // Storage fixture only: a future owned-combat coordinator must prove the death before this transaction.
    static bool PurpleRewardCommit(GameState p,object run,uint[] party=null,bool markVictory=true) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())try {
            if(markVictory)using(var q=new Npgsql.NpgsqlCommand("UPDATE revora_purple_banshee_runs SET state='victory',ended_at=@now,killer=@uid WHERE id=@run AND state='active'",c,t)) {
                q.Parameters.AddWithValue("now",(DateTime)((Func<DateTime>)PurpleAdmission.GetField("Clock",Hidden).GetValue(null))());
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("run",(Guid)Field(run,"Id"));q.ExecuteNonQuery();
            }
            Call(PurpleRewards,null,"Record",c,t,(Guid)Field(run,"Id"),p,party??new[]{p.Entity.UID});t.Commit();return true;
        }catch(Exception){t.Rollback();return false;}}
    }
    static void VerifyPurpleRewards(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);Call(PurpleAdmission,null,"Initialize");
        Query("CREATE TABLE refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();Call(PurpleRewards,null,"Initialize");Call(PurpleRewards,null,"Initialize");
        var now=new DateTime(2039,1,4,20,0,0,DateTimeKind.Utc);
        PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        var random= PurpleRewards.GetField("Roll",Hidden);var original=random.GetValue(null);
        try {
            int artifacts=0,refineries=0;
            for(int n=0;n<100;n++){int branch=n,draw=0;random.SetValue(null,new Func<int,int>(max=>draw++==0?branch:0));uint type=(uint)Call(PurpleRewards,null,"Draw");
                if(ConquerItemInformation.BaseInformations[type].PurificationLevel>=5){Check(ConquerItemInformation.BaseInformations[type].PurificationLevel>=5 && ConquerItemInformation.BaseInformations[type].PurificationLevel<=6,"Purple artifact stays within P5-P6");artifacts++;}else {Check(Kernel.DatabaseRefinery.ContainsKey(type) && Kernel.DatabaseRefinery[type].Level>=5 && Kernel.DatabaseRefinery[type].Level<=6 && !Kernel.DatabaseRefinery[type].Untradable,"refinery branch stays in the reviewed tradable level-5/6 pool");refineries++;};}
            Check(artifacts==60 && refineries==40,"all one hundred branch outcomes give the documented 60/40 split");
            foreach(int invalid in new[]{-1,100}){random.SetValue(null,new Func<int,int>(max=>invalid));bool rejected=false;try{Call(PurpleRewards,null,"Draw");}catch(InvalidOperationException){rejected=true;}Check(rejected,"invalid Purple loot draw fails closed");}
            int choice=0;random.SetValue(null,new Func<int,int>(max=>max==100?(choice++%2==0?59:60):0));
            var p=SwordHousePlayer(town);var mate=HousePlayer(town);mate.Entity.Level=100;EntityTable.SaveEntity(mate);
            // Admission's team snapshot is exercised here; protected team travel belongs to encounter acceptance.
            mate.Entity.MapID=p.Entity.MapID;mate.Entity.X=p.Entity.X;mate.Entity.Y=p.Entity.Y;TeratoTeam(p,mate);
            var run=PurpleAdmit(p,Pack(p,3004259,1));
            Check(!PurpleRewardCommit(p,run,null,false),"active attempt cannot manufacture victory rewards");
            Check(!PurpleRewardCommit(p,run,new[]{p.Entity.UID,p.Entity.UID+900000}),"foreign reward recipient rolls back the victory");
            Check(PurpleRewardCommit(p,run,new[]{p.Entity.UID,mate.Entity.UID}),"victory stores frozen-team study and all three drop entitlements atomically");
            Check(!PurpleRewardCommit(p,run,new[]{p.Entity.UID,mate.Entity.UID}),"replayed victory cannot issue a second reward set");
            Parallel.For(0,8,n=>Call(PurpleRewards,null,"PayStudy",p));Call(PurpleRewards,null,"PayStudy",mate);
            Check(!Fenced(p) && !Fenced(mate) && p.Entity.SubClasses.StudyPoints==200 && mate.Entity.SubClasses.StudyPoints==50,
                "ordinary teammates receive 200 summoner and 50 teammate Study Points under repeated callbacks");
            Call(PurpleRewards,null,"Restore");var drops=PurpleDrops(p);
            Check(drops.Length==3 && drops.Count(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==1,
                "owned house restores two original drops plus exactly one bound P7 event reward");
            foreach(string mode in new[]{"short","length","family","seal","oversize"}) {
                var packet=LootPacket(drops[0]);if(mode=="short")Array.Resize(ref packet,12);else if(mode=="length")packet[0]--;else if(mode=="family")packet[2]++;else if(mode=="seal")packet[packet.Length-1]^=1;else Array.Resize(ref packet,129);
                p.Entity.X=drops[0].X;p.Entity.Y=drops[0].Y;FloorItem.Process(packet,p);
                Check(!Fenced(p) && !drops[0].PickedUpAlready && p.Inventory.Count==0,"malformed Purple pickup makes no mutation: "+mode);
            }
            while(p.Inventory.Count<40)Pack(p,1088001,1);Loot(p,drops[0]);
            Check(!Fenced(p) && !drops[0].PickedUpAlready && p.Inventory.Count==40,"full bag leaves Purple drop and claim untouched");
            foreach(var filler in p.Inventory.Objects.ToArray())DisRemove(p,filler);
            foreach(var d in drops){p.Entity.X=d.X;p.Entity.Y=d.Y;Parallel.For(0,8,n=>FloorItem.Process(LootPacket(d),p));}
            Check(!Fenced(p) && p.Inventory.Count==3 && Scalar("SELECT count(*) FROM revora_purple_banshee_loot WHERE owner=@uid AND collector=@uid",p.Entity.UID)==3,
                "native pickup commits each Purple drop once");
            Check(p.Inventory.Objects.Single(i=>Kernel.DatabaseRefinery.ContainsKey(i.ID)).Days==0,"refinery reward is an untimed material");
            p=ReloadBall(p);Call(PurpleRewards,null,"PayStudy",p);
            Check(p.Entity.SubClasses.StudyPoints==200 && p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                "production reload preserves bound event P7 and exactly-once Study Points");
            foreach(string table in new[]{"revora_purple_banshee_study","revora_purple_banshee_loot"}) {
                var f=SwordHousePlayer(town);var attempt=PurpleAdmit(f,Pack(f,3004259,1));
                Query("CREATE FUNCTION suppress_purple_reward() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_reward BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_purple_reward()");
                bool accepted;try{accepted=PurpleRewardCommit(f,attempt);}finally{Query("DROP TRIGGER suppress_purple_reward ON "+table+";DROP FUNCTION suppress_purple_reward()");}
                Check(!accepted && Scalar("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid AND state='active'",f.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_purple_banshee_study WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_purple_banshee_loot WHERE owner=@uid",f.Entity.UID)==0,"failed reward insert rolls back the entire victory: "+table);
                Check(PurpleRewardCommit(f,attempt),"failed Purple victory can retry exactly once: "+table);
            }
            var recover=SwordHousePlayer(town);var rr=PurpleAdmit(recover,Pack(recover,3004259,1));Check(PurpleRewardCommit(recover,rr),"recovery fixture earns pending Purple rewards");
            recover.Entity.SubClasses.StudyPoints=65520;Query("UPDATE entities SET studypoints=65520 WHERE uid=@uid","uid",(long)recover.Entity.UID);Call(PurpleRewards,null,"PayStudy",recover);
            Check(!Fenced(recover) && recover.Entity.SubClasses.StudyPoints==65520 && Scalar("SELECT count(*) FROM revora_purple_banshee_study WHERE uid=@uid AND paid_at IS NULL",recover.Entity.UID)==1,"Study Point overflow retains the pending entitlement");
            recover.Entity.SubClasses.StudyPoints=0;Query("UPDATE entities SET studypoints=0 WHERE uid=@uid","uid",(long)recover.Entity.UID);
            Query("CREATE FUNCTION suppress_purple_study() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_study BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_purple_study()");
            try{Call(PurpleRewards,null,"PayStudy",recover);}finally{Query("DROP TRIGGER suppress_purple_study ON entities;DROP FUNCTION suppress_purple_study()");}
            Check(Fenced(recover) && Scalar("SELECT count(*) FROM revora_purple_banshee_study WHERE uid=@uid AND paid_at IS NULL",recover.Entity.UID)==1,"suppressed Study credit rolls back claim and fences stale session");
            recover=ReloadBall(recover);Call(PurpleRewards,null,"PayStudy",recover);Check(!Fenced(recover) && recover.Entity.SubClasses.StudyPoints==200,"reload recovers unpaid Purple Study Points");
            Call(PurpleRewards,null,"ClearGround");Call(PurpleRewards,null,"Restore");var gift=PurpleDrops(recover).Single(d=>d.Item.Bound);
            Query("CREATE FUNCTION suppress_purple_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_purple_item()");
            try{Loot(recover,gift);}finally{Query("DROP TRIGGER suppress_purple_item ON items;DROP FUNCTION suppress_purple_item()");}
            Check(Fenced(recover) && !gift.PickedUpAlready && Scalar("SELECT count(*) FROM revora_purple_banshee_loot WHERE owner=@uid AND collector IS NOT NULL",recover.Entity.UID)==0,"failed Purple pickup preserves floor loot and rolls back receipt");
            recover=ReloadBall(recover);gift.Item.Bound=false;Loot(recover,gift);
            Check(Fenced(recover) && !gift.PickedUpAlready && Scalar("SELECT count(*) FROM revora_purple_banshee_loot WHERE owner=@uid AND collector IS NOT NULL",recover.Entity.UID)==0,"altered P7 binding cannot enter inventory or consume its receipt");
            gift.Item.Bound=true;recover=ReloadBall(recover);Loot(recover,gift);Check(!Fenced(recover) && recover.Inventory.Objects.Single().Bound,"bound P7 pickup retries after production reload");
            Call(PurpleRewards,null,"ClearGround");var metadata=MapsTable.MapInformations[recover.Entity.MapID];uint owner=metadata.Owner;metadata.Owner++;MapsTable.MapInformations[recover.Entity.MapID]=metadata;
            try{Call(PurpleRewards,null,"Restore");Check(PurpleDrops(recover).Length==0,"changed house owner prevents loot restoration into a foreign instance");}finally{metadata.Owner=owner;MapsTable.MapInformations[recover.Entity.MapID]=metadata;}
            Call(PurpleRewards,null,"Restore");Check(PurpleDrops(recover).Length==2,"restoration retains uncollected original drops without recreating collected P7");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())
                Check((bool)Call(PurpleRewards,null,"BlocksHouseChange",c,t,recover.Entity.UID),"uncollected Purple spoils block house replacement after victory");}
            var summoner=SwordHousePlayer(town);var killer=HousePlayer(town);
            killer.Entity.MapID=summoner.Entity.MapID;killer.Entity.X=summoner.Entity.X;killer.Entity.Y=summoner.Entity.Y;TeratoTeam(summoner,killer);
            var teamRun=PurpleAdmit(summoner,Pack(summoner,3004259,1));
            Check(!PurpleRewardCommit(killer,teamRun,new[]{killer.Entity.UID}),"killer cannot omit the summoner from Purple Study rewards");
            Check(PurpleRewardCommit(killer,teamRun,new[]{summoner.Entity.UID,killer.Entity.UID}),"teammate kill records the summoner's distinct entitlement");
            Call(PurpleRewards,null,"PayStudy",summoner);Call(PurpleRewards,null,"PayStudy",killer);
            Check(!Fenced(summoner) && !Fenced(killer) && summoner.Entity.SubClasses.StudyPoints==200 && killer.Entity.SubClasses.StudyPoints==50,
                "teammate killer receives 50 while the house summoner retains 200");
            var expired=PurpleDrops(recover)[0];int retained=recover.Inventory.Count;now=now.AddMinutes(3);Loot(recover,expired);
            Check(!Fenced(recover) && recover.Inventory.Count==retained,"late native pickup cannot collect expired Purple loot");Call(PurpleRewards,null,"Restore");Check(PurpleDrops(recover).Length==0,"expired Purple loot cannot reappear after recovery");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())
                Check(!(bool)Call(PurpleRewards,null,"BlocksHouseChange",c,t,recover.Entity.UID),"expired Purple spoils release house replacement");}
        } finally {Call(PurpleRewards,null,"ClearGround");random.SetValue(null,original);PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
}
