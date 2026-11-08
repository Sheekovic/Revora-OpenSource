using System;
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
    static readonly Type PriceQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DailyPriceQuest");
    static DateTime priceNow=DateTime.UtcNow.Date.AddHours(19);
    static int priceRoll;
    static long PriceRuns(GameState p) {return Scalar("SELECT count(*) FROM revora_price_runs WHERE uid=@uid",p.Entity.UID);}
    static long PriceDone(GameState p) {return Scalar("SELECT count(*) FROM revora_price_runs WHERE uid=@uid AND completed AND reward_uid IS NOT NULL",p.Entity.UID);}
    static void PriceStand(GameState p) {var xy=FlowerTile(p.Map,Config.DailyEnvoyX,Config.DailyEnvoyY);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;}
    static GameState PricePlayer(Map town) {
        var p=BallPlayer(town);PriceStand(p);p.Entity.ConquerPoints=20000;p.LastResetTime=priceNow.ToLocalTime();
        p.Quests=new Quests(p);Revora.Game.Features.QuestListSystem.EnsureCatalog();EntityTable.SaveEntity(p);return p;
    }
    static void PriceNpc(GameState p,byte option) {p.ActiveNpc=79882;Npcs.GetDialog(new NpcRequest(true){NpcID=79882,OptionID=option},p);}
    static void PriceTrip(GameState p,byte option=1) {
        var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=option;
        Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
    }
    static ConquerItem PriceMaterials(GameState p,int extra=0) {return Pack(p,p.Entity.PriceItemId,(ushort)(p.Entity.Priceamount+extra));}
    static void VerifyNoDailyP7(GameState p) {
        Check(!Fenced(p) && !p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0,"daily reward persists without P7 souls");
    }
    static void VerifyDailySoul(GameState p,string kind) {
        var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        Check(!Fenced(p) && souls.Length==0 && Scalar("SELECT count(*) FROM revora_"+kind+"_runs WHERE uid=@uid AND soul_uid IS NOT NULL",p.Entity.UID)==0,"daily "+kind+" pays no P7 soul or soul receipt");
        if(kind=="magnolia") {
            long promised=Scalar("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid AND completed AND rune_reward",p.Entity.UID);
            Check(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==promised
                && Scalar("SELECT count(*) FROM revora_magnolia_runs r JOIN items i ON i.uid=r.rune_uid AND i.entityid=r.uid WHERE r.uid=@uid AND r.completed AND r.rune_reward AND i.id=4030101 AND i.bound=1",p.Entity.UID)==promised,
                "Magnolia preserves each promised bound rune and exact durable receipt");
        }
        if(kind=="price" || kind=="rare") {
            long promised=Scalar("SELECT count(*) FROM revora_"+kind+"_runs WHERE uid=@uid AND completed AND rune_reward",p.Entity.UID);
            var runes=p.Inventory.Objects.Where(i=>i.ID==4030101).ToArray();
            Check(runes.Length==promised && runes.All(i=>i.Bound && i.StackSize==1)
                && Scalar("SELECT count(*) FROM revora_"+kind+"_runs r JOIN items i ON i.uid=r.rune_uid AND i.entityid=r.uid WHERE r.uid=@uid AND r.completed AND r.rune_reward AND i.id=4030101 AND i.bound=1",p.Entity.UID)==promised,
                "daily "+kind+" preserves each promised bound rune and its exact durable receipt");
        }
    }
    static void VerifyPriceQuest(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Call(PriceQuest,null,"Initialize");Call(PriceQuest,null,"Initialize");Call(HuntLoot,null,"Initialize");MiningNpc(town,79882);Call(MonsterLoot,null,"Initialize");
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        PriceQuest.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>priceNow));
        PriceQuest.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>priceRoll%max));
        HuntLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        Check(Config.DailyPriceRollExclusive==5,"default random pool includes the implemented fourth hunting assignment");
        foreach(ushort id in new ushort[]{1015,1020,1000,1011})World(runtime,id);
        Check((string)Call(PriceQuest,null,"Day",new DateTime(2026,7,3,6,59,59,DateTimeKind.Utc))=="20260702"
            && (string)Call(PriceQuest,null,"Day",new DateTime(2026,7,3,7,0,0,DateTimeKind.Utc))=="20260703"
            && (string)Call(PriceQuest,null,"Day",new DateTime(2026,1,3,8,0,0,DateTimeKind.Utc))=="20260103","daily hunt uses Pacific midnight through DST");
        string[] names={"BanditL97","Macaque","SandMonster","Bandit"};ushort[] destinations={1015,1020,1000,1011};
        bool money=Config.MoneyDropEnabled,killCp=Config.KillConquerPointsEnabled;Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try { for(int route=0;route<4;route++) {
            priceRoll=route;var p=PricePlayer(town);AdventureNear(p,79882);PriceNpc(p,2);
            Check(!Fenced(p) && PriceRuns(p)==1 && p.Entity.PriceQuestID==route+1 && p.Inventory.Contains(729087,1)
                && p.Entity.ConquerPoints==20000,"ordinary acceptance records its assignment and list atomically: "+names[route]);
            p=ReloadBall(p);Call(PriceQuest,null,"Refresh",p);var list=p.Inventory.Objects.Single(i=>i.ID==729087);ItemHandler.UseItem(list,p);PriceTrip(p);
            Check(!Fenced(p) && p.Entity.MapID==destinations[route] && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"owned list's native dialogue reaches its physical hunting ground after reconnect: "+names[route]);
            uint material=p.Entity.PriceItemId;int needed=p.Entity.Priceamount;
            for(int n=0;n<needed;n++) {
                var before=p.Map.FloorItems.Keys.ToArray();var mob=HuntMonster(p,names[route]);mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=false;mob.Die(p.Entity);
                var drop=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && i.ItemID==material);Loot(p,drop);
                if(Fenced(p))throw new Exception("Hunt route pickup failed.");
            }
            Check(HuntPickups(p)==needed && p.Inventory.Objects.Count(i=>i.ID==material)==needed,"actual deaths and native pickups supply the full hunt offering: "+names[route]);
            var gate=Pack(p,1060020,1);ItemHandler.UseItem(gate,p);AdventureNear(p,79882);ulong xp=p.Entity.Experience,award=p.ExpBall;
            uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;
            PriceNpc(p,2);PriceNpc(p,11);
            Check(!Fenced(p) && PriceDone(p)==1 && p.Entity.EvreyThink && p.Entity.Experience==xp+award && p.Entity.ConquerPoints==cp+Config.DailyQuestCp
                && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailyPriceQuestStudy
                && !p.Inventory.Contains(material,1) && !p.Inventory.Contains(729087,1) && p.Inventory.Contains(Config.DailyQuestChiToken,1),"walked return completes the whole earned daily reward: "+names[route]);
            p=ReloadBall(p);Call(PriceQuest,null,"Refresh",p);Check(!Fenced(p) && p.Entity.EvreyThink && PriceDone(p)==1 && p.Entity.Experience==xp+award
                && p.Inventory.Contains(Config.DailyQuestChiToken,1),"production reload retains completion and every earned reward: "+names[route]);
            VerifyStudyAward(p,"price");VerifyDailySoul(p,"price");
            PriceNpc(p,2);PriceNpc(p,13);Check(PriceDone(p)==1 && p.Entity.ConquerPoints==cp+Config.DailyQuestCp,"completed hunt cannot be charged or rewarded again: "+names[route]);
        }}finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=killCp;}
        priceRoll=0;var changed=PricePlayer(town);PriceNpc(changed,2);byte old=changed.Entity.PriceQuestID;
        Parallel.For(0,15,i=>PriceNpc(changed,12));
        Check(!Fenced(changed) && changed.Entity.PriceTimes==1 && changed.Entity.PriceQuestID!=old && changed.Entity.ConquerPoints==20000-Config.DailyPriceChangeCp
            && Scalar("SELECT reroll_fees FROM revora_price_runs WHERE uid=@uid",changed.Entity.UID)==Config.DailyPriceChangeCp,"concurrent reroll continuation charges once and changes the material");
        changed=ReloadBall(changed);PriceStand(changed);Call(PriceQuest,null,"Refresh",changed);PriceNpc(changed,12);
        Check(changed.Entity.PriceTimes==1 && changed.Entity.ConquerPoints==20000-Config.DailyPriceChangeCp,"reconnected session cannot replay an old paid offer");
        PriceNpc(changed,2);uint funds=changed.Entity.ConquerPoints;Parallel.For(0,15,i=>PriceNpc(changed,13));
        Check(!Fenced(changed) && PriceDone(changed)==1 && changed.Entity.ConquerPoints==funds-Config.DailyPriceSkipCpFlat-Config.DailyPriceSkipCpPerChange+Config.DailyQuestCp,
            "paid completion debits the reviewed fee and delivers all rewards once under concurrent requests");
        changed.Entity.EvreyThink=false;changed.Entity.PriceQuestID=0;DialyQuestTable.Load(changed);DialyQuestTable.Save(changed);PriceNpc(changed,2);PriceNpc(changed,13);
        Check(PriceDone(changed)==1 && changed.Entity.EvreyThink,"resetting legacy daily mirrors cannot reopen a completed hunt");
        priceNow=priceNow.AddDays(1);PriceNpc(changed,2);PriceNpc(changed,13);Check(!Fenced(changed) && PriceDone(changed)==2,"online midnight permits the next daily hunt");
        priceNow=priceNow.AddDays(-2);PriceNpc(changed,2);PriceNpc(changed,13);Check(!Fenced(changed) && PriceDone(changed)==2,"backward clock cannot reopen a previous daily reward");
        priceNow=DateTime.UtcNow.Date.AddHours(19);
        var limit=PricePlayer(town);PriceNpc(limit,2);for(int n=0;n<Config.DailyPriceChangeMax;n++){PriceNpc(limit,2);PriceNpc(limit,12);}
        funds=limit.Entity.ConquerPoints;PriceNpc(limit,2);PriceNpc(limit,12);Check(!Fenced(limit) && limit.Entity.PriceTimes==Config.DailyPriceChangeMax && limit.Entity.ConquerPoints==funds,"reroll cap preserves the current hunt and CPs");
        var canceled=PricePlayer(town);PriceNpc(canceled,2);PriceNpc(canceled,255);PriceNpc(canceled,13);
        Check(PriceDone(canceled)==0 && canceled.Entity.ConquerPoints==20000,"closed dialogue cancels its paid continuation");
        PriceNpc(canceled,2);priceNow=priceNow.AddMinutes(3);PriceNpc(canceled,13);priceNow=priceNow.AddMinutes(-3);
        Check(PriceDone(canceled)==0 && canceled.Entity.ConquerPoints==20000,"expired dialogue cannot charge a stale skip");
        var reissue=PricePlayer(town);PriceNpc(reissue,14);Check(PriceRuns(reissue)==0 && reissue.Inventory.Count==0,"list reissue cannot accept a forged new hunt");
        PriceNpc(reissue,2);var firstList=reissue.Inventory.Objects.Single();DisRemove(reissue,firstList);Parallel.For(0,12,i=>PriceNpc(reissue,14));
        Check(!Fenced(reissue) && reissue.Inventory.Count==1 && PriceRuns(reissue)==1
            && Scalar("SELECT cardinality(lists) FROM revora_price_runs WHERE uid=@uid",reissue.Entity.UID)==2,"lost list reissue saves one replacement under duplicate clicks");
        var full=PricePlayer(town);for(int n=0;n<40;n++)Pack(full,1088001,1);PriceNpc(full,2);
        Check(!Fenced(full) && PriceRuns(full)==0,"a full bag cannot accept a hunt without its list");
        foreach(string fault in new[]{"items","revora_price_runs"}) {
            var p=PricePlayer(town);Query("CREATE FUNCTION suppress_price() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_price BEFORE INSERT ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_price()");
            try{PriceNpc(p,2);}finally{Query("DROP TRIGGER suppress_price ON "+fault+";DROP FUNCTION suppress_price()");}
            Check(Fenced(p) && PriceRuns(p)==0 && p.Inventory.Count==0,"failed acceptance rolls back both list and daily assignment: "+fault);
            p=ReloadBall(p);PriceStand(p);PriceNpc(p,2);Check(!Fenced(p) && PriceRuns(p)==1 && p.Inventory.Count==1,"ordinary reconnect can accept after failed "+fault);
        }
        foreach(string fault in new[]{"evidence","profile","delete","stack","reward","rune","receipt","skills","recovery"}) {
            var p=fault=="recovery" ? PendingFirstLife(town) : PricePlayer(town);PriceStand(p);p.Entity.ConquerPoints=20000;EntityTable.SaveEntity(p);
            if(fault=="skills"){p.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(p);}
            PriceNpc(p,2);var materials=PriceMaterials(p,fault=="stack"?2:0);ulong xp=p.Entity.Experience;
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="profile"?"entities":fault=="receipt"?"revora_price_runs":fault=="skills"?"skills":fault=="recovery"?"revora_first_rebirths":"items";
            string verb=fault=="evidence"?"INSERT":fault=="delete"?"DELETE":fault=="reward" || fault=="rune" || fault=="skills"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_price() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_price BEFORE "+verb+" ON "+table+" FOR EACH ROW"+(fault=="rune"?" WHEN(NEW.id=4030101)":"")+" EXECUTE FUNCTION suppress_price()");
            try{PriceNpc(p,11);}finally{Query("DROP TRIGGER suppress_price ON "+table+";DROP FUNCTION suppress_price()");}
            Check(Fenced(p) && PriceDone(p)==0 && p.Entity.ConquerPoints==20000 && p.Entity.Experience==xp && p.Inventory.ContainsUID(materials.UID)
                && SavedRune(materials,"stacksize")==materials.StackSize,"failed "+fault+" retains offering, currencies and daily reward");
            Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0,"failed claim saves no P7 soul");
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);PriceStand(p);PriceNpc(p,2);PriceNpc(p,11);
            if(fault=="evidence")VerifyStudyAward(p,"price");VerifyDailySoul(p,"price");
            Check(!Fenced(p) && PriceDone(p)==1 && (fault!="recovery" || LifePending(p)==0),"ordinary reload retries the complete hunt after "+fault+" failure");
        }
        foreach(string balance in new[]{"conquerpoints","revorapoint","studypoints"}) {
            var p=PricePlayer(town);PriceNpc(p,2);var items=PriceMaterials(p);Query("UPDATE entities SET "+balance+"="+balance+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);PriceNpc(p,11);
            Check(Fenced(p) && PriceDone(p)==0 && p.Inventory.ContainsUID(items.UID),"stale "+balance+" cannot consume materials or overwrite another reward");
        }
        foreach(string state in new[]{"remote","trade","dead","fake","unloaded","low-level","replaced"}) {
            var p=PricePlayer(town);if(state=="remote")p.Entity.X=1;if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;if(state=="low-level")p.Entity.Level=59;
            if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);PriceNpc(p,2);PriceNpc(p,13);
            Check(!Fenced(p) && PriceRuns(p)==0 && p.Entity.ConquerPoints==20000,"ineligible hunt request cannot issue or charge: "+state);
        }
        foreach(string state in new[]{"missing","locked","suspicious","expired","curse","cp-full","rvp-full","study-full"}) {
            var p=PricePlayer(town);PriceNpc(p,2);var items=PriceMaterials(p);if(state=="missing")DisRemove(p,items);
            if(state=="locked")items.Lock=1;if(state=="suspicious")items.Suspicious=true;if(state=="expired"){items.Days=1;items.DayStamp=DateTime.Now.AddDays(-2);}
            if(state=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);if(state=="cp-full")p.Entity.ConquerPoints=uint.MaxValue;
            if(state=="rvp-full")p.Entity.RevoraPoint=uint.MaxValue;if(state=="study-full")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;
            EntityTable.SaveEntity(p);PriceNpc(p,11);Check(!Fenced(p) && PriceDone(p)==0 && p.Inventory.Contains(729087,1),"unclaimable hunt preserves its completion and list: "+state);
        }
        var bound=PricePlayer(town);PriceNpc(bound,2);var boundItems=PriceMaterials(bound,2);boundItems.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundItems.UID);PriceNpc(bound,11);
        Check(!Fenced(bound) && PriceDone(bound)==1 && boundItems.StackSize==2 && bound.Inventory.Objects.Single(i=>i.ID==Config.DailyQuestChiToken).Bound,"bound stacked materials debit exact quantity and keep Chi reward bound");
        var fullClaim=PricePlayer(town);PriceNpc(fullClaim,2);PriceMaterials(fullClaim,2);while(fullClaim.Inventory.Count<40)Pack(fullClaim,1088001,1);PriceNpc(fullClaim,11);
        Check(!Fenced(fullClaim) && PriceDone(fullClaim)==0,"one freed slot cannot hold both daily gifts");
        DisRemove(fullClaim,fullClaim.Inventory.Objects.First(i=>i.ID==1088001));PriceNpc(fullClaim,2);PriceNpc(fullClaim,11);
        Check(!Fenced(fullClaim) && PriceDone(fullClaim)==1 && fullClaim.Inventory.Count==40,"consumed list plus one free slot admits both daily gifts");
        PriceNpc(fullClaim,2);PriceNpc(fullClaim,11);
        Check(!Fenced(fullClaim) && PriceDone(fullClaim)==1 && fullClaim.Inventory.Count==40,"full inventory replay cannot pay the daily reward twice");VerifyDailySoul(fullClaim,"price");
        var cap=PricePlayer(town);cap.Entity.Level=Config.LevelCap;EntityTable.SaveEntity(cap);PriceNpc(cap,2);PriceNpc(cap,13);
        Check(!Fenced(cap) && PriceDone(cap)==1 && cap.Entity.Experience==5000,"maximum-level player keeps saved EXP and receives the non-EXP bundle");
        var frozen=PricePlayer(town);PriceNpc(frozen,2);uint originalCp=Config.DailyQuestCp,originalFee=Config.DailyPriceSkipCpFlat;
        try{Config.DailyQuestCp+=100;Config.DailyPriceSkipCpFlat+=1000;PriceNpc(frozen,13);}finally{Config.DailyQuestCp=originalCp;Config.DailyPriceSkipCpFlat=originalFee;}
        Check(!Fenced(frozen) && PriceDone(frozen)==1 && frozen.Entity.ConquerPoints==20000-originalFee+originalCp,"accepted hunt freezes its CP reward and skip tariff");
        foreach(bool complete in new[]{false,true}) {
            var p=PricePlayer(town);DialyQuestTable.Load(p);p.Entity.PriceQuestID=4;p.Entity.PriceItemId=729093;p.Entity.Priceamount=25;p.Entity.PriceTimes=2;p.Entity.EvreyThink=complete;DialyQuestTable.Save(p);
            PriceNpc(p,2);Check(!Fenced(p) && PriceRuns(p)==1 && p.Entity.PriceQuestID==4 && p.Entity.PriceTimes==2 && p.Entity.EvreyThink==complete,"saved current-day legacy hunt imports without reroll or duplicate reward: done="+complete);
        }
        var previous=PricePlayer(town);PriceNpc(previous,2);
        Query("UPDATE revora_price_runs SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)previous.Entity.UID);
        PriceNpc(previous,2);PriceNpc(previous,13);
        Check(!Fenced(previous) && PriceDone(previous)==1 && previous.Inventory.Count==1
            && Scalar("SELECT count(*) FROM revora_price_runs WHERE uid=@uid AND soul_uid IS NOT NULL",previous.Entity.UID)==0,"previous P7 flag cannot restore removed daily reward");
        var valuable=PricePlayer(town);PriceNpc(valuable,2);Parallel.For(0,12,i=>PriceNpc(valuable,13));VerifyDailySoul(valuable,"price");
        valuable=ReloadBall(valuable);PriceStand(valuable);PriceNpc(valuable,2);PriceNpc(valuable,13);VerifyDailySoul(valuable,"price");
        Call(PriceQuest,null,"Initialize");VerifyDailySoul(valuable,"price");
        VerifyPricePayments(town);VerifyPriceTrips(town);
    }
    static void VerifyPricePayments(Map town) {
        foreach(byte option in new byte[]{12,13})foreach(string table in new[]{"entities","revora_price_runs"}) {
            var p=PricePlayer(town);PriceNpc(p,2);byte task=p.Entity.PriceQuestID;
            Query("CREATE FUNCTION suppress_price() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_price BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_price()");
            try{PriceNpc(p,option);}finally{Query("DROP TRIGGER suppress_price ON "+table+";DROP FUNCTION suppress_price()");}
            Check(Fenced(p) && PriceDone(p)==0 && p.Entity.ConquerPoints==20000 && p.Entity.PriceQuestID==task && p.Entity.PriceTimes==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==20000,"failed paid option "+option+" rolls back charge and hunt transition: "+table);
            p=ReloadBall(p);PriceStand(p);PriceNpc(p,2);PriceNpc(p,option);
            Check(!Fenced(p) && (option==12 ? p.Entity.PriceTimes==1 : PriceDone(p)==1),"reconnected player can retry paid option "+option+" after "+table+" failure");
        }
        var poor=PricePlayer(town);poor.Entity.ConquerPoints=0;PriceNpc(poor,2);PriceNpc(poor,12);PriceNpc(poor,2);PriceNpc(poor,13);
        Check(!Fenced(poor) && PriceDone(poor)==0 && poor.Entity.PriceTimes==0 && poor.Entity.ConquerPoints==0,"insufficient CPs cannot change or settle a hunt");
        var noList=PricePlayer(town);PriceNpc(noList,2);DisRemove(noList,noList.Inventory.Objects.Single());PriceMaterials(noList,2);
        while(noList.Inventory.Count<40)Pack(noList,1088001,1);PriceNpc(noList,11);
        Check(!Fenced(noList) && PriceDone(noList)==0 && noList.Inventory.Count==40,"full bag with no consumed list and remaining material stack retains the reward");
        var nil=PricePlayer(town);Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL,studypoints=NULL WHERE uid=@uid","uid",(long)nil.Entity.UID);
        nil=ReloadBall(nil);PriceStand(nil);PriceNpc(nil,2);PriceMaterials(nil);PriceNpc(nil,11);
        Check(!Fenced(nil) && PriceDone(nil)==1 && nil.Entity.ConquerPoints==Config.DailyQuestCp && nil.Entity.RevoraPoint==Config.DailyQuestRevoraPoints
            && nil.Entity.SubClasses.StudyPoints==Config.DailyPriceQuestStudy,"legacy nullable zero balances can receive the whole earned hunt reward");
        var zero=PricePlayer(town);PriceNpc(zero,2);for(int n=0;n<zero.Entity.Priceamount;n++)Pack(zero,zero.Entity.PriceItemId,0);PriceNpc(zero,11);
        Check(!Fenced(zero) && PriceDone(zero)==1 && zero.Inventory.Count==2,"legacy zero-stack singleton materials count once each and debit exactly the offering");
        var race=PricePlayer(town);PriceNpc(race,2);var stack=PriceMaterials(race,3);
        Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(race);else PriceNpc(race,11);});
        Check(!Fenced(race) && PriceDone(race)==1 && stack.StackSize==3 && race.Entity.ConquerPoints==20000+Config.DailyQuestCp,"turn-in and autosave race consumes one exact offering and one reward");
        var minimum=PricePlayer(town);minimum.Entity.Level=Config.DailyPriceMinLevel;EntityTable.SaveEntity(minimum);PriceNpc(minimum,2);PriceNpc(minimum,13);
        Check(!Fenced(minimum) && PriceDone(minimum)==1,"exact configured minimum level can accept and finish the normal hunt");
        var reset=PricePlayer(town);PriceNpc(reset,2);bool resetEnabled=Config.DailyEnvoyShowReset;
        uint oldResetFee=Config.DailyEnvoyResetCp;byte assignment=reset.Entity.PriceQuestID;
        try {
            Config.DailyEnvoyResetCp=1000;
            foreach(bool enabled in new[]{false,true}) {
                Config.DailyEnvoyShowReset=enabled;
                for(int n=0;n<3;n++)PriceNpc(reset,18);
                Check(!Fenced(reset) && reset.Entity.ConquerPoints==20000 && PriceRuns(reset)==1
                    && reset.Entity.PriceQuestID==assignment && PriceDone(reset)==0,
                    "retired reset replay preserves funds and the active assignment with legacy toggle "+enabled);
            }
            EntityTable.SaveEntity(reset);reset=ReloadBall(reset);PriceStand(reset);Call(PriceQuest,null,"Refresh",reset);
            Check(reset.Entity.ConquerPoints==20000 && reset.Entity.PriceQuestID==assignment && PriceRuns(reset)==1,
                "retired reset rejection preserves funds and daily progress after production reload");
            PriceNpc(reset,2);PriceNpc(reset,13);uint completedCp=reset.Entity.ConquerPoints;
            for(int n=0;n<3;n++)PriceNpc(reset,18);
            Check(!Fenced(reset) && reset.Entity.ConquerPoints==completedCp && reset.Entity.EvreyThink && PriceDone(reset)==1,
                "retired reset cannot clear completed eligibility or purchase another daily reward");
        }finally{Config.DailyEnvoyShowReset=resetEnabled;Config.DailyEnvoyResetCp=oldResetFee;}
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        var mentor=PricePlayer(town);var child=PricePlayer(town);MentorLink(mentor,child,100);child.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(child);
        PriceNpc(child,2);PriceMaterials(child);
        Query("CREATE FUNCTION suppress_price() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_price BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_price()");
        try{PriceNpc(child,11);}finally{Query("DROP TRIGGER suppress_price ON apprentice;DROP FUNCTION suppress_price()");}
        Check(Fenced(child) && PriceDone(child)==0 && child.Entity.Level==135 && child.Entity.ConquerPoints==20000 && MentorBalance(child)==100,"mentor write failure retains the complete offering and reward");
        child=ReloadBall(child);PriceStand(child);Revora.Database.KnownPersons.LoadMentor(child);PriceNpc(child,2);PriceNpc(child,11);
        Check(!Fenced(child) && PriceDone(child)==1 && child.Entity.Level==136 && MentorBalance(child)==372,"reconnect commits hunt progression and mentor credit together");
    }
    static void VerifyPriceTrips(Map town) {
        foreach(string invalid in new[]{"no-offer","missing","substituted","expired","changed-hunt","completed","wrong-day","db-owner"}) {
            var p=PricePlayer(town);PriceNpc(p,2);var list=p.Inventory.Objects.Single();
            if(invalid!="no-offer")ItemHandler.UseItem(list,p);else p.ActiveNpc=729087;
            if(invalid=="missing")DisRemove(p,list);
            if(invalid=="substituted"){p.Inventory.Remove(list.UID,Enums.ItemUse.None,false);p.Inventory.Add(new ConquerItem(true){UID=list.UID,ID=list.ID,StackSize=1},Enums.ItemUse.None);}
            if(invalid=="expired")priceNow=priceNow.AddMinutes(3);
            if(invalid=="changed-hunt"){PriceNpc(p,2);PriceNpc(p,12);p.ActiveNpc=729087;}
            if(invalid=="completed"){PriceNpc(p,2);PriceNpc(p,13);p.ActiveNpc=729087;}
            if(invalid=="wrong-day")priceNow=priceNow.AddDays(1);
            if(invalid=="db-owner")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)list.UID);
            PriceTrip(p);Check(p.Entity.MapID==1002 && (invalid!="db-owner" || Fenced(p)),"stale or unowned list cannot teleport: "+invalid);
            if(invalid=="expired")priceNow=priceNow.AddMinutes(-3);if(invalid=="wrong-day")priceNow=priceNow.AddDays(-1);
        }
        var malformed=PricePlayer(town);PriceNpc(malformed,2);ItemHandler.UseItem(malformed.Inventory.Objects.Single(),malformed);
        foreach(string invalid in new[]{"short","oversize","trailing","input"}) {
            int length=invalid=="short"?18:invalid=="oversize"?121:20;var frame=new byte[length+8+(invalid=="trailing"?1:0)];
            Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=1;if(invalid=="input")frame[17]=20;
            Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);PacketHandler.HandlePacket(frame,malformed);
            Check(malformed.Entity.MapID==1002,"bounded list dialogue rejects malformed frame: "+invalid);
        }
        PriceTrip(malformed);Check(malformed.Entity.MapID!=1002,"valid native continuation still works after rejected malformed frames");
        var active=PricePlayer(town);PriceNpc(active,2);var owned=active.Inventory.Objects.Single();ItemHandler.UseItem(owned,active);PriceTrip(active);
        ushort map=active.Entity.MapID;active.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);active.ActiveNpc=729087;PriceTrip(active);
        Check(map!=1002 && active.Entity.MapID==1002,"one list continuation cannot replay a teleport");
    }
}
