using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type SwordEncounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.SwordMasterEncounter");
    static readonly Type SwordCombat=typeof(Entity).Assembly.GetType("Revora.Game.Features.SwordMasterCombat");
    static DateTime swordNow;
    static object[] SwordActors(){return ((IDictionary)SwordEncounter.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().ToArray();}
    static Entity SwordBoss(GameState p){return (Entity)Field(SwordActors().Single(a=>ReferenceEquals(Field(a,"Owner"),p)),"Body");}
    static long SwordRuns(GameState p,string state){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_sword_master_runs WHERE uid=@uid AND state=@state",c)){
        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("state",state);return Convert.ToInt64(q.ExecuteScalar());}}}
    static void SwordConfirm(GameState p){p.ActiveNpc=720842;SoulWire(p,1,720842);}
    static uint SwordHit(GameState p,Entity body,uint damage) {
        p.Entity.X=body.X;p.Entity.Y=body.Y;
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);return damage;
    }
    static void SwordBuy(GameState p) {
        p.ArenaStatistic=new QualifierStatistic(true){EntityID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class};
        p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class};
        Query("INSERT INTO arena(entityid,entityname,level,currenthonor) VALUES(@uid,@name,120,0);INSERT INTO teamarena(entityid,entityname,level,currenthonor) VALUES(@uid,@name,120,0)","uid",(long)p.Entity.UID,"name",p.Entity.Name);
        p.CurrentHonor=8000;HonorBuy(p);
        Check(!Fenced(p) && p.CurrentHonor==0 && p.Inventory.Objects.Count(i=>i.ID==720842)==1,"ordinary player buys the challenge item through the real 8000-Honor shop");
    }
    static GameState SwordHunter(Map town,bool bead=false,bool wardrobe=false) {
        var p=HousePlayer(town);if(wardrobe)StoredQuestGarment(p);p.Entity.Level=120;p.LastResetTime=swordNow.ToLocalTime();EntityTable.SaveEntity(p);
        if(bead){PriceStand(p);SoulNpc(p,7);SoulNpc(p,8);Check(!Fenced(p) && SoulBead(p).ID==729611,"ordinary hunter accepts a Normal Spirit Bead from the envoy");AdventureNear(p,30156);}
        SwordBuy(p);BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);HouseNpc(p,2);return p;
    }
    static void SwordSummon(GameState p) {var item=p.Inventory.Objects.First(i=>i.ID==720842);ItemHandler.UseItem(item,p);SwordConfirm(p);}
    static void VerifySwordMaster(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);Call(SoulBall,null,"Initialize");Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.AnniversaryQuest"),null,"Initialize");MiningNpc(town,79882);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases"),null,"Initialize");HonorShop.Load();
        swordNow=new DateTime(2040,1,4,20,0,0,DateTimeKind.Utc);SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>swordNow));
        SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>swordNow));
        Call(SwordEncounter,null,"Initialize");Call(SwordEncounter,null,"Initialize");
        try {
            var p=SwordHunter(town,true,true);uint wardrobe=p.Entity.StorageItems.Keys.Single();var token=p.Inventory.Objects.Single(i=>i.ID==720842);
            SwordConfirm(p);Check(SwordRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"unoffered native confirmation cannot consume a Sword Soul");
            ItemHandler.UseItem(token,p);var bad=VisitFrame(1,720842);bad[17]=30;PacketHandler.HandlePacket(bad,p);
            Check(SwordRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"malformed Sword confirmation text makes no mutation");
            Parallel.For(0,8,n=>SwordConfirm(p));var boss=SwordBoss(p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(token.UID) && SwordRuns(p,"active")==1 && boss.Hitpoints==15000000 && boss.MaxHitpoints==15000000
                && boss.Boss==1 && boss.MonsterInfo.Boss && boss.Body==953,"native confirmation spends exactly one Soul and registers one full-health Sword Master");
            Call(SwordEncounter,null,"Initialize");Check(SwordRuns(p,"active")==1 && !boss.Dead,"repeated same-process initialization cannot forfeit a live hunt");
            bool refused=false;try{Call(SwordEncounter,null,"RecoverInterrupted");}catch(InvalidOperationException){refused=true;}
            Check(refused && SwordRuns(p,"active")==1,"startup recovery refuses to close a registered live actor");
            var outsider=HousePlayer(town);outsider.Entity.Level=120;outsider.Entity.MapID=p.Entity.MapID;
            Check(SwordHit(outsider,boss,500)==0 && boss.Hitpoints==15000000,"uninvited player cannot damage the owned boss");boss.Die(outsider.Entity);
            Check(!boss.Dead && SwordRuns(p,"victory")==0,"foreign direct death callback cannot award victory");outsider.Entity.MapID=1002;
            SwordHit(p,boss,500);Check(boss.Hitpoints==14999500 && SwordRuns(p,"victory")==0,"real nonlethal damage updates health without rewarding a kill");
            Parallel.For(0,8,n=>SwordHit(p,boss,15000000));
            Check(!Fenced(p) && boss.Dead && SwordRuns(p,"victory")==1 && SoulSpirits(p)==1000 && SoulBossCredits(p)==1 && p.Entity.SubClasses.StudyPoints==20,
                "lethal combat commits one owned victory, 1000 spirits, loot and Study Points under concurrent callbacks");
            boss.Die(p.Entity);Check(SoulBossCredits(p)==1 && SoulSpirits(p)==1000,"retired Sword corpse cannot replay its spirit award");
            p.Entity.MyAchievement.Send();
            Check(HasAchievement(p,10528) && !HasAchievement(outsider,10528)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10528 AND source='sword-master-kill-v1'",p.Entity.UID)==1,
                "Sword Master victory and replay grant its kill achievement once to the canonical killer only");
            var drops=SwordDrops(p);Check(drops.Length==3,"genuine encounter victory creates its three persisted ground rewards");foreach(var drop in drops)Loot(p,drop);
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"boss admission, victory and reward pickup preserve the stored wardrobe garment");
            p=ReloadBall(p);Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==20 && SoulSpirits(p)==1000 && p.Inventory.Objects.Count(i=>i.Bound && i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                "ordinary production reload retains encounter rewards and owned spirit credit");
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10528) && HasAchievement(p,10525),"Sword Master kill and direct unarmed achievements survive production login");
            var another=Pack(p,720842,1);ItemHandler.UseItem(another,p);SwordConfirm(p);Check(SwordActors().Length==0 && p.Inventory.ContainsUID(another.UID),"completed daily encounter cannot be purchased twice");
            VerifySwordCombat(town);
            VerifySwordFailures(town);VerifySwordOffersAndCompanion(town);
            VerifySwordTeam(town);
        } finally {
            foreach(var a in SwordActors()){var owner=(GameState)Field(a,"Owner");owner.Entity.MapID=1002;}
            Call(SwordEncounter,null,"Tick");Call(SwordRewards,null,"ClearGround");SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
        }
    }
    static void VerifySwordCombat(Map town) {
        var p=SwordHunter(town);SwordSummon(p);var body=SwordBoss(p);p.Attackable=true;p.Entity.MaxHitpoints=p.Entity.Hitpoints=200000;body.Hitpoints-=1000000;
        for(int phase=0;phase<7;phase++) {
            p.Entity.X=(ushort)(body.X+(phase==5?5:1));p.Entity.Y=body.Y;uint hp=p.Entity.Hitpoints,before=body.Hitpoints;
            Call(SwordCombat,null,"Execute",body,p.Entity);Check(p.Entity.Hitpoints==hp && body.Hitpoints==before,"Sword skill has a real warning before execution: "+phase);
            swordNow=swordNow.AddSeconds(2);Call(SwordCombat,null,"Tick",SwordActors().Single(a=>ReferenceEquals(Field(a,"Owner"),p)));
            Check(!Fenced(p) && (phase==6?body.Hitpoints==before+300000:p.Entity.Hitpoints<hp),"Sword skill applies its owned damage or bounded heal: "+phase);
            swordNow=swordNow.AddSeconds(4);
        }
        Check(!(bool)Call(SwordCombat,null,"Contains",5,0,0) && (bool)Call(SwordCombat,null,"Contains",5,4,0)
            && !(bool)Call(SwordCombat,null,"Contains",2,3,0),"storm's safe center and Immolation escape boundary match Revora's telegraphs");
        p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;Call(SwordCombat,null,"Execute",body,p.Entity);uint health=p.Entity.Hitpoints;
        p.Entity.X=(ushort)(body.X+15);swordNow=swordNow.AddSeconds(2);Call(SwordCombat,null,"Tick",SwordActors().Single(a=>ReferenceEquals(Field(a,"Owner"),p)));
        Check(p.Entity.Hitpoints==health,"leaving Slash reach during its warning avoids damage");
        p.Entity.MapID=1002;Call(SwordEncounter,null,"Tick");Check(SwordRuns(p,"failed")==1 && !SwordActors().Any(a=>ReferenceEquals(Field(a,"Owner"),p)),"leaving the house forfeits the attempt and cleans up its actor");
    }
    static void VerifySwordFailures(Map town) {
        foreach(string table in new[]{"revora_sword_master_loot","revora_soul_ball_runs","revora_soul_ball_bosses","revora_achievement_evidence"}) {
            var p=SwordHunter(town,true);SwordSummon(p);var boss=SwordBoss(p);
            Query("CREATE FUNCTION suppress_sword_victory() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_victory BEFORE "+(table=="revora_soul_ball_runs"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_sword_victory()");
            try{SwordHit(p,boss,15000000);}finally{Query("DROP TRIGGER suppress_sword_victory ON "+table+";DROP FUNCTION suppress_sword_victory()");}
            Check(Fenced(p) && !boss.Dead && SwordRuns(p,"active")==1 && SoulSpirits(p)==0 && SoulBossCredits(p)==0
                && Scalar("SELECT count(*) FROM revora_sword_master_loot WHERE owner=@uid",p.Entity.UID)==0,"suppressed participant rolls back victory, loot and spirits: "+table);
            // Reset only the fixture fence to prove retry atomicity; a real disconnected owner forfeits the challenge.
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10528",p.Entity.UID)==0,
                "failed Sword victory cannot leave a kill achievement receipt: "+table);
            DemonReset(p);SwordHit(p,boss,15000000);Check(!Fenced(p) && boss.Dead && SwordRuns(p,"victory")==1 && SoulSpirits(p)==1000,"same live combat can finish once after rolled-back write: "+table);
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10528),"retried Sword victory records its kill achievement: "+table);
        }
        var upgrade=SwordHunter(town);SwordSummon(upgrade);var active=SwordBoss(upgrade);upgrade.Entity.MapID=1002;AdventureNear(upgrade,30156);
        BuyHouseCert(upgrade,2);var permit=upgrade.Inventory.Objects.Single(i=>i.ID==721174);HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==1 && upgrade.Inventory.ContainsUID(permit.UID),"house upgrade refuses an active encounter before cleanup without spending its permit");
        Call(SwordEncounter,null,"Tick");HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==2 && SwordRuns(upgrade,"failed")==1,"failed hunt cleanup permits the ordinary house upgrade");
        var reconnect=SwordHunter(town);SwordSummon(reconnect);reconnect=ReloadBall(reconnect);Call(SwordEncounter,null,"Tick");
        Check(!Fenced(reconnect) && SwordRuns(reconnect,"failed")==1,"disconnected owner's new session cannot adopt the old actor or reopen today's attempt");
        var expired=SwordHunter(town);SwordSummon(expired);swordNow=swordNow.AddMinutes(31);Call(SwordEncounter,null,"Tick");
        Check(SwordRuns(expired,"failed")==1,"thirty-minute challenge expiry closes the paid attempt");
        var interrupted=SwordHunter(town);var lost=SwordAdmit(interrupted,interrupted.Inventory.Objects.Single(i=>i.ID==720842));
        swordNow=swordNow.AddSeconds(6);Call(SwordEncounter,null,"Tick");Check(SwordRuns(interrupted,"failed")==1,"orphan cleanup closes committed admission whose actor was never published");
        var boot=SwordHunter(town);SwordAdmit(boot,boot.Inventory.Objects.Single(i=>i.ID==720842));Call(SwordEncounter,null,"RecoverInterrupted");Call(SwordEncounter,null,"RecoverInterrupted");
        Check(SwordRuns(boot,"failed")==1,"startup recovery durably forfeits an interrupted paid attempt exactly once");
    }
    static void VerifySwordOffersAndCompanion(Map town) {
        var p=SwordHunter(town);var token=p.Inventory.Objects.Single(i=>i.ID==720842);ItemHandler.UseItem(token,p);
        swordNow=swordNow.AddMinutes(3);SwordConfirm(p);
        Check(!Fenced(p) && SwordRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"expired confirmation preserves the exact Sword Soul and daily allowance");
        ItemHandler.UseItem(token,p);token.Bound=!token.Bound;SwordConfirm(p);token.Bound=!token.Bound;
        Check(!Fenced(p) && SwordRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"changed offered binding requires a fresh Sword confirmation");
        SwordSummon(p);var boss=SwordBoss(p);p.Entity.X=boss.X;p.Entity.Y=boss.Y;
        var pet=new Entity(EntityFlag.Monster,true){UID=p.Entity.UID-200000,Owner=p,MonsterInfo=new MonsterInformation(),MapID=p.Entity.MapID,X=boss.X,Y=boss.Y,Hitpoints=1000};p.Companion=pet;
        boss.Die(pet);Check(!boss.Dead && SwordRuns(p,"victory")==0,"unregistered companion cannot claim Sword Master victory");
        p.Map.Companions.Add(pet.UID,pet);ushort priorX=p.Entity.X;p.Entity.X=checked((ushort)(boss.X+30));
        boss.Die(pet);Check(!Fenced(p) && !boss.Dead && SwordRuns(p,"victory")==0,"distant companion owner cannot bypass nearby reward eligibility or lose the attempt");
        p.Entity.X=priorX;boss.Die(pet);p.Map.Companions.Remove(pet.UID);p.Companion=null;
        Check(!Fenced(p) && boss.Dead && SwordRuns(p,"victory")==1 && p.Entity.SubClasses.StudyPoints==20,"registered companion victory belongs to its ordinary owner");
        p.Entity.MyAchievement.Send();Check(HasAchievement(p,10528),"registered companion credits its owner with the Sword Master kill achievement");
        ushort house=p.Entity.MapID;p.Entity.MapID=1002;AdventureNear(p,30156);BuyHouseCert(p,2);HouseNpc(p,3);HouseNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==1,"uncollected Sword spoils prevent house replacement until their expiry");
        swordNow=swordNow.AddMinutes(3);Call(SwordEncounter,null,"Tick");HouseNpc(p,3);HouseNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==2,"expired spoils release the ordinary house upgrade guard");
        var boot=SwordHunter(town);SwordAdmit(boot,boot.Inventory.Objects.Single(i=>i.ID==720842));
        Query("CREATE FUNCTION suppress_sword_boot() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_boot BEFORE UPDATE ON revora_sword_master_runs FOR EACH ROW EXECUTE FUNCTION suppress_sword_boot()");
        bool refused=false;try{Call(SwordEncounter,null,"RecoverInterrupted");}catch(InvalidOperationException){refused=true;}
        finally{Query("DROP TRIGGER suppress_sword_boot ON revora_sword_master_runs;DROP FUNCTION suppress_sword_boot()");}
        Check(refused && SwordRuns(boot,"active")==1,"suppressed startup cleanup fails closed without pretending recovery succeeded");
        Call(SwordEncounter,null,"RecoverInterrupted");Check(SwordRuns(boot,"failed")==1,"interrupted recovery retries after the failed write");
    }
    static void VerifySwordTeam(Map town) {
        Query("CREATE TABLE IF NOT EXISTS friends AS TABLE public.friends WITH NO DATA");Call(FriendService,null,"Initialize");
        var pair=HomePair(town);var host=pair[0];var guest=pair[1];foreach(var p in pair){p.Entity.Level=120;EntityTable.SaveEntity(p);}SwordBuy(host);
        InviteHome(host,guest);VisitNpc(guest,70);HouseNpc(host,2);TeratoTeam(host,guest);
        Check(HomeArrivals(guest)==1 && guest.Entity.MapID==host.Entity.MapID,"teammate reaches the owned house through a real accepted friend invitation");
        SwordSummon(host);var boss=SwordBoss(host);var team=guest.Team;guest.Team=null;
        Check(SwordHit(guest,boss,500)==0 && boss.Hitpoints==15000000,"leaving the frozen team revokes combat eligibility immediately");
        guest.Team=team;SwordHit(guest,boss,15000000);
        Check(!Fenced(host) && !Fenced(guest) && SwordRuns(host,"victory")==1 && host.Entity.SubClasses.StudyPoints==20 && guest.Entity.SubClasses.StudyPoints==20,
            "invited ordinary teammate can kill the boss and both frozen teammates receive twenty Study Points");
    }
}
