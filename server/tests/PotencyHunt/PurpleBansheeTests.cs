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
    static readonly Type PurpleEncounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurpleBansheeEncounter");
    static readonly Type PurpleCombat=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurpleBansheeCombat");
    static DateTime purpleNow;
    static object[] PurpleActors(){return ((IDictionary)PurpleEncounter.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().ToArray();}
    static Entity PurpleBoss(GameState p){return (Entity)Field(PurpleActors().Single(a=>ReferenceEquals(Field(a,"Owner"),p)),"Body");}
    static long PurpleRuns(GameState p,string state){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid AND state=@state",c)){
        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("state",state);return Convert.ToInt64(q.ExecuteScalar());}}}
    static void PurpleConfirm(GameState p){p.ActiveNpc=3004259;SoulWire(p,1,3004259);}
    static uint PurpleHit(GameState p,Entity body,uint damage) {
        p.Entity.X=body.X;p.Entity.Y=body.Y;
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);return damage;
    }
    static void PurpleBuy(GameState p) {
        p.ArenaStatistic=new QualifierStatistic(true){EntityID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class};
        p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class};
        Query("INSERT INTO arena(entityid,entityname,level,currenthonor) VALUES(@uid,@name,120,0);INSERT INTO teamarena(entityid,entityname,level,currenthonor) VALUES(@uid,@name,120,0)","uid",(long)p.Entity.UID,"name",p.Entity.Name);
        p.CurrentHonor=35000;HonorBuy(p,3004259);
        Check(!Fenced(p) && p.CurrentHonor==0 && p.Inventory.Objects.Count(i=>i.ID==3004259)==1,"ordinary player buys the challenge item through the real 35000-Honor shop");
    }
    static GameState PurpleHunter(Map town,bool bead=false) {
        var p=HousePlayer(town);p.Entity.Level=120;p.LastResetTime=purpleNow.ToLocalTime();EntityTable.SaveEntity(p);
        if(bead){PriceStand(p);SoulNpc(p,7);SoulNpc(p,8);Check(!Fenced(p) && SoulBead(p).ID==729611,"ordinary hunter accepts a Normal Spirit Bead from the envoy");AdventureNear(p,30156);}
        PurpleBuy(p);BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);HouseNpc(p,2);return p;
    }
    static void PurpleSummon(GameState p) {var item=p.Inventory.Objects.First(i=>i.ID==3004259);ItemHandler.UseItem(item,p);PurpleConfirm(p);}
    static void VerifyPurpleBanshee(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);Call(SoulBall,null,"Initialize");Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.AnniversaryQuest"),null,"Initialize");MiningNpc(town,79882);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases"),null,"Initialize");HonorShop.Load();
        purpleNow=new DateTime(2040,1,4,20,0,0,DateTimeKind.Utc);PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>purpleNow));
        SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>purpleNow));
        Query("CREATE TABLE refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();
        Call(PurpleEncounter,null,"Initialize");Call(PurpleEncounter,null,"Initialize");
        try {
            var p=PurpleHunter(town,true);uint wardrobe=StoredQuestGarment(p);var token=p.Inventory.Objects.Single(i=>i.ID==3004259);
            PurpleConfirm(p);Check(PurpleRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"unoffered native confirmation cannot consume a Banshee Spirit");
            ItemHandler.UseItem(token,p);var bad=VisitFrame(1,3004259);bad[17]=30;PacketHandler.HandlePacket(bad,p);
            Check(PurpleRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"malformed Purple confirmation text makes no mutation");
            Parallel.For(0,8,n=>PurpleConfirm(p));var boss=PurpleBoss(p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(token.UID) && PurpleRuns(p,"active")==1 && boss.Hitpoints==54000000 && boss.MaxHitpoints==54000000
                && boss.Boss==1 && boss.MonsterInfo.Boss && boss.Body==951,"native confirmation spends exactly one Soul and registers one full-health Purple Banshee");
            Call(PurpleEncounter,null,"Initialize");Check(PurpleRuns(p,"active")==1 && !boss.Dead,"repeated same-process initialization cannot forfeit a live hunt");
            bool refused=false;try{Call(PurpleEncounter,null,"RecoverInterrupted");}catch(InvalidOperationException){refused=true;}
            Check(refused && PurpleRuns(p,"active")==1,"startup recovery refuses to close a registered live actor");
            var outsider=HousePlayer(town);outsider.Entity.Level=120;outsider.Entity.MapID=p.Entity.MapID;
            Check(PurpleHit(outsider,boss,500)==0 && boss.Hitpoints==54000000,"uninvited player cannot damage the owned boss");boss.Die(outsider.Entity);
            Check(!boss.Dead && PurpleRuns(p,"victory")==0,"foreign direct death callback cannot award victory");outsider.Entity.MapID=1002;
            PurpleHit(p,boss,500);Check(boss.Hitpoints==53999500 && PurpleRuns(p,"victory")==0,"real nonlethal damage updates health without rewarding a kill");
            Parallel.For(0,8,n=>PurpleHit(p,boss,54000000));
            Check(!Fenced(p) && boss.Dead && PurpleRuns(p,"victory")==1 && SoulSpirits(p)==0 && SoulBossCredits(p)==0 && p.Entity.SubClasses.StudyPoints==200,
                "lethal combat commits one owned victory, no unverified spirit bonus, loot and Study Points under concurrent callbacks");
            boss.Die(p.Entity);Check(SoulBossCredits(p)==0 && SoulSpirits(p)==0,"retired Purple corpse cannot replay its spirit award");
            var drops=PurpleDrops(p);Check(drops.Length==3,"genuine encounter victory creates its three persisted ground rewards");foreach(var drop in drops)Loot(p,drop);
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"boss admission, victory and reward pickup preserve the stored wardrobe garment");
            p=ReloadBall(p);Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==200 && SoulSpirits(p)==0 && p.Inventory.Objects.Count(i=>i.Bound && i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                "ordinary production reload retains encounter rewards without an unverified spirit bonus");
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525),"direct unarmed Purple kill survives production reload");
            var another=Pack(p,3004259,1);ItemHandler.UseItem(another,p);PurpleConfirm(p);Check(PurpleActors().Length==0 && p.Inventory.ContainsUID(another.UID),"completed daily encounter cannot be purchased twice");
            VerifyPurpleCombat(town);
            VerifyPurpleFailures(town);VerifyPurpleOffersAndCompanion(town);
            VerifyPurpleTeam(town);VerifyHouseBossNativeExclusion(town);
        } finally {
            foreach(var a in PurpleActors()){var owner=(GameState)Field(a,"Owner");owner.Entity.MapID=1002;}
            Call(PurpleEncounter,null,"Tick");Call(PurpleRewards,null,"ClearGround");PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
        }
    }
    static void VerifyPurpleCombat(Map town) {
        var p=PurpleHunter(town);PurpleSummon(p);var body=PurpleBoss(p);var actor=PurpleActors().Single(a=>ReferenceEquals(Field(a,"Owner"),p));
        p.Attackable=true;
        p.Entity.MaxHitpoints=p.Entity.Hitpoints=500000;body.Hitpoints-=10000000;
        // Begin the custom frost cycle with no pending cast.
        lock(PurpleEncounter.GetField("Gate",Hidden).GetValue(null))Call(PurpleCombat,null,"Clear",actor);
        for(int phase=0;phase<7;phase++) {
            p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;uint hp=p.Entity.Hitpoints,before=body.Hitpoints;
            Call(PurpleCombat,null,"Execute",body,p.Entity);Check(p.Entity.Hitpoints==hp && body.Hitpoints==before,"Banshee skill warns before effect: "+phase);
            purpleNow=purpleNow.AddSeconds(2);Call(PurpleCombat,null,"Tick",actor);
            Check(!Fenced(p) && (phase==6?body.Hitpoints==before+1080000:p.Entity.Hitpoints<hp),"Banshee skill applies owned damage or bounded healing: "+phase);
            if(phase==2)for(int pulse=1;pulse<6;pulse++){purpleNow=purpleNow.AddSeconds(1);Call(PurpleCombat,null,"Tick",actor);}
            if(phase==3 || phase==4) {
                Check((bool)Call(PurpleCombat,null,"IsStunned",p) && !p.Entity.Move(Enums.ConquerAngle.East,1),"Banshee freeze blocks actual player movement: "+phase);
                ushort oldX=p.Entity.X,oldY=p.Entity.Y;
                GeneralData.PlayerJump(new Data(true){dwParam=(uint)((oldX+2)|(oldY<<16))},p);
                Check(p.Entity.X==oldX && p.Entity.Y==oldY,"native jump handler also respects the Banshee freeze: "+phase);
                uint damage=100;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee},ref damage,null);
                Check(damage==0,"frozen player cannot attack through direct damage dispatch: "+phase);
                purpleNow=purpleNow.AddSeconds(phase==3?5:3);Check(!(bool)Call(PurpleCombat,null,"IsStunned",p),"owned freeze expires at its exact five/three-second boundary: "+phase);
            }
            purpleNow=purpleNow.AddSeconds(4);
        }
        Check((bool)Call(PurpleCombat,null,"Contains",2,10,0,Math.PI/9) && !(bool)Call(PurpleCombat,null,"Contains",2,10,0,Math.PI/3)
            && !(bool)Call(PurpleCombat,null,"Contains",4,5,0,0.0),"rotating hail cone and IceShock escape boundary follow the reviewed server geometry");
        p.Entity.MapID=1002;Call(PurpleEncounter,null,"Tick");
        Check(PurpleRuns(p,"failed")==1 && !(bool)Call(PurpleCombat,null,"IsStunned",p),"leaving the house retires the boss and its owned freeze effects");
    }    static void VerifyPurpleFailures(Map town) {
        foreach(string table in new[]{"revora_purple_banshee_loot","revora_purple_banshee_study","revora_achievement_evidence"}) {
            var p=PurpleHunter(town,true);PurpleSummon(p);var boss=PurpleBoss(p);
            Query("CREATE FUNCTION suppress_purple_victory() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_victory BEFORE "+("INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_purple_victory()");
            try{PurpleHit(p,boss,54000000);}finally{Query("DROP TRIGGER suppress_purple_victory ON "+table+";DROP FUNCTION suppress_purple_victory()");}
            Check(Fenced(p) && !boss.Dead && PurpleRuns(p,"active")==1 && SoulSpirits(p)==0 && SoulBossCredits(p)==0
                && Scalar("SELECT count(*) FROM revora_purple_banshee_loot WHERE owner=@uid",p.Entity.UID)==0,"suppressed participant rolls back victory, loot and spirits: "+table);
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"failed Purple victory cannot retain unarmed evidence: "+table);
            // Reset only the fixture fence to prove retry atomicity; a real disconnected owner forfeits the challenge.
            DemonReset(p);PurpleHit(p,boss,54000000);Check(!Fenced(p) && boss.Dead && PurpleRuns(p,"victory")==1 && SoulSpirits(p)==0,"same live combat can finish once after rolled-back write: "+table);
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525),"retried Purple victory earns unarmed credit: "+table);
        }
        var upgrade=PurpleHunter(town);PurpleSummon(upgrade);var active=PurpleBoss(upgrade);upgrade.Entity.MapID=1002;AdventureNear(upgrade,30156);
        BuyHouseCert(upgrade,2);var permit=upgrade.Inventory.Objects.Single(i=>i.ID==721174);HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==1 && upgrade.Inventory.ContainsUID(permit.UID),"house upgrade refuses an active encounter before cleanup without spending its permit");
        Call(PurpleEncounter,null,"Tick");HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==2 && PurpleRuns(upgrade,"failed")==1,"failed hunt cleanup permits the ordinary house upgrade");
        var reconnect=PurpleHunter(town);PurpleSummon(reconnect);reconnect=ReloadBall(reconnect);Call(PurpleEncounter,null,"Tick");
        Check(!Fenced(reconnect) && PurpleRuns(reconnect,"failed")==1,"disconnected owner's new session cannot adopt the old actor or reopen today's attempt");
        var expired=PurpleHunter(town);PurpleSummon(expired);purpleNow=purpleNow.AddMinutes(31);Call(PurpleEncounter,null,"Tick");
        Check(PurpleRuns(expired,"failed")==1,"thirty-minute challenge expiry closes the paid attempt");
        var interrupted=PurpleHunter(town);var lost=PurpleAdmit(interrupted,interrupted.Inventory.Objects.Single(i=>i.ID==3004259));
        purpleNow=purpleNow.AddSeconds(6);Call(PurpleEncounter,null,"Tick");Check(PurpleRuns(interrupted,"failed")==1,"orphan cleanup closes committed admission whose actor was never published");
        var boot=PurpleHunter(town);PurpleAdmit(boot,boot.Inventory.Objects.Single(i=>i.ID==3004259));Call(PurpleEncounter,null,"RecoverInterrupted");Call(PurpleEncounter,null,"RecoverInterrupted");
        Check(PurpleRuns(boot,"failed")==1,"startup recovery durably forfeits an interrupted paid attempt exactly once");
    }
    static void VerifyPurpleOffersAndCompanion(Map town) {
        var p=PurpleHunter(town);var token=p.Inventory.Objects.Single(i=>i.ID==3004259);ItemHandler.UseItem(token,p);
        purpleNow=purpleNow.AddMinutes(3);PurpleConfirm(p);
        Check(!Fenced(p) && PurpleRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"expired confirmation preserves the exact Banshee Spirit and daily allowance");
        ItemHandler.UseItem(token,p);token.Bound=!token.Bound;PurpleConfirm(p);token.Bound=!token.Bound;
        Check(!Fenced(p) && PurpleRuns(p,"active")==0 && p.Inventory.ContainsUID(token.UID),"changed offered binding requires a fresh Purple confirmation");
        PurpleSummon(p);var boss=PurpleBoss(p);p.Entity.X=boss.X;p.Entity.Y=boss.Y;
        var pet=new Entity(EntityFlag.Monster,true){UID=p.Entity.UID-200000,Owner=p,MonsterInfo=new MonsterInformation(),MapID=p.Entity.MapID,X=boss.X,Y=boss.Y,Hitpoints=1000};p.Companion=pet;
        boss.Die(pet);Check(!boss.Dead && PurpleRuns(p,"victory")==0,"unregistered companion cannot claim Purple Banshee victory");
        p.Map.Companions.Add(pet.UID,pet);ushort priorX=p.Entity.X;p.Entity.X=checked((ushort)(boss.X+30));
        boss.Die(pet);Check(!Fenced(p) && !boss.Dead && PurpleRuns(p,"victory")==0,"distant companion owner cannot bypass nearby reward eligibility or lose the attempt");
        p.Entity.X=priorX;boss.Die(pet);p.Map.Companions.Remove(pet.UID);p.Companion=null;
        Check(!Fenced(p) && boss.Dead && PurpleRuns(p,"victory")==1 && p.Entity.SubClasses.StudyPoints==200,"registered companion victory belongs to its ordinary owner");
        p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10525),"Purple companion kill cannot grant its owner unarmed credit");
        ushort house=p.Entity.MapID;p.Entity.MapID=1002;AdventureNear(p,30156);BuyHouseCert(p,2);HouseNpc(p,3);HouseNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==1,"uncollected Purple spoils prevent house replacement until their expiry");
        purpleNow=purpleNow.AddMinutes(3);Call(PurpleEncounter,null,"Tick");HouseNpc(p,3);HouseNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==2,"expired spoils release the ordinary house upgrade guard");
        var boot=PurpleHunter(town);PurpleAdmit(boot,boot.Inventory.Objects.Single(i=>i.ID==3004259));
        Query("CREATE FUNCTION suppress_purple_boot() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_boot BEFORE UPDATE ON revora_purple_banshee_runs FOR EACH ROW EXECUTE FUNCTION suppress_purple_boot()");
        bool refused=false;try{Call(PurpleEncounter,null,"RecoverInterrupted");}catch(InvalidOperationException){refused=true;}
        finally{Query("DROP TRIGGER suppress_purple_boot ON revora_purple_banshee_runs;DROP FUNCTION suppress_purple_boot()");}
        Check(refused && PurpleRuns(boot,"active")==1,"suppressed startup cleanup fails closed without pretending recovery succeeded");
        Call(PurpleEncounter,null,"RecoverInterrupted");Check(PurpleRuns(boot,"failed")==1,"interrupted recovery retries after the failed write");
    }
    static void VerifyPurpleTeam(Map town) {
        Query("CREATE TABLE IF NOT EXISTS friends AS TABLE public.friends WITH NO DATA");Call(FriendService,null,"Initialize");
        var pair=HomePair(town);var host=pair[0];var guest=pair[1];foreach(var p in pair){p.Entity.Level=120;EntityTable.SaveEntity(p);}PurpleBuy(host);
        InviteHome(host,guest);VisitNpc(guest,70);HouseNpc(host,2);TeratoTeam(host,guest);
        Check(HomeArrivals(guest)==1 && guest.Entity.MapID==host.Entity.MapID,"teammate reaches the owned house through a real accepted friend invitation");
        PurpleSummon(host);var boss=PurpleBoss(host);var team=guest.Team;guest.Team=null;
        Check(PurpleHit(guest,boss,500)==0 && boss.Hitpoints==54000000,"leaving the frozen team revokes combat eligibility immediately");
        guest.Team=team;PurpleHit(guest,boss,54000000);
        Check(!Fenced(host) && !Fenced(guest) && PurpleRuns(host,"victory")==1 && host.Entity.SubClasses.StudyPoints==200 && guest.Entity.SubClasses.StudyPoints==50,
            "invited ordinary teammate can kill the boss and summoner receives 200 and guest receives 50 Study Points");
        host.Entity.MyAchievement.Send();guest.Entity.MyAchievement.Send();
        Check(HasAchievement(guest,10525) && !HasAchievement(host,10525),"unarmed Purple credit belongs only to the actual teammate killer");
    }
    static void VerifyHouseBossNativeExclusion(Map town) {
        Call(SwordEncounter,null,"Initialize");
        foreach(bool purpleFirst in new[]{true,false}) {
            var p=PurpleHunter(town);var spirit=p.Inventory.Objects.Single(item=>item.ID==3004259);var sword=Pack(p,720842,1);
            if(purpleFirst) {
                PurpleSummon(p);ItemHandler.UseItem(sword,p);SwordConfirm(p);
                Check(PurpleRuns(p,"active")==1 && SwordRuns(p,"active")==0 && p.Inventory.ContainsUID(sword.UID),
                    "native Sword confirmation cannot overlap an active Purple hunt or consume its item");
            } else {
                ItemHandler.UseItem(sword,p);SwordConfirm(p);ItemHandler.UseItem(spirit,p);PurpleConfirm(p);
                Check(SwordRuns(p,"active")==1 && PurpleRuns(p,"active")==0 && p.Inventory.ContainsUID(spirit.UID),
                    "native Purple confirmation cannot overlap an active Sword hunt or consume its item");
            }
            p.Entity.MapID=1002;Call(PurpleEncounter,null,"Tick");Call(SwordEncounter,null,"Tick");
        }
        var dead=PurpleHunter(town);PurpleSummon(dead);dead.Entity.Hitpoints=0;Call(PurpleEncounter,null,"Tick");
        Check(PurpleRuns(dead,"failed")==1,"a house party with no living fighter forfeits its paid attempt");
    }

}
