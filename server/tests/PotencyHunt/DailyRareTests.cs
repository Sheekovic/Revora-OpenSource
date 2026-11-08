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
    static readonly Type RareQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DailyRareQuest");
    static DateTime rareNow=DateTime.UtcNow.Date.AddHours(19);
    static int rareRoll;
    static long RareRuns(GameState p){return Scalar("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid",p.Entity.UID);}
    static long RareDone(GameState p){return Scalar("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid AND completed AND reward_uid IS NOT NULL",p.Entity.UID);}
    static GameState RarePlayer(Map town){var p=PricePlayer(town);p.LastResetTime=rareNow.ToLocalTime();p.Entity.RevoraPoint=200;EntityTable.SaveEntity(p);return p;}
    static void RareNpc(GameState p,byte option){p.ActiveNpc=79882;Npcs.GetDialog(new NpcRequest(true){NpcID=79882,OptionID=option},p);}
    static ConquerItem RareGems(GameState p,int extra=0){return Pack(p,p.Entity.MaterialstemId,(ushort)(Config.DailyRareGemAmount+extra));}
    static void VerifyRareQuest(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Call(RareQuest,null,"Initialize");Call(RareQuest,null,"Initialize");MiningNpc(town,79882);
        RareQuest.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>rareNow));RareQuest.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>rareRoll%max));
        Check(Config.DailyRareRollExclusive==5,"Rare Materials default includes all four existing normal-gem assignments");
        VerifyRareMining(runtime,town);
        rareRoll=0;var paid=RarePlayer(town);RareNpc(paid,19);RareNpc(paid,34);
        Check(paid.Entity.MaterialsStar==1 && paid.Entity.RevoraPoint==200,"star purchase requires its own reviewed upgrade dialogue");
        RareNpc(paid,32);Parallel.For(0,12,i=>RareNpc(paid,34));
        Check(!Fenced(paid) && paid.Entity.MaterialsStar==2 && paid.Entity.RevoraPoint==200-Config.DailyRareStarRevoraPoints
            && Scalar("SELECT star_rvp FROM revora_rare_runs WHERE uid=@uid",paid.Entity.UID)==Config.DailyRareStarRevoraPoints,"concurrent upgrade clicks charge and raise a star once");
        paid=ReloadBall(paid);PriceStand(paid);Call(RareQuest,null,"Refresh",paid);RareNpc(paid,34);
        Check(!Fenced(paid) && paid.Entity.MaterialsStar==2 && paid.Entity.RevoraPoint==200-Config.DailyRareStarRevoraPoints,"paid star survives production reload and old offer replay cannot charge again");
        byte old=paid.Entity.PriceMaterialsID;RareNpc(paid,19);Parallel.For(0,12,i=>RareNpc(paid,29));
        Check(!Fenced(paid) && paid.Entity.PriceMaterialsID!=old && paid.Entity.MaterialsTimes==1 && paid.Entity.MaterialsStar==2
            && paid.Entity.RevoraPoint==200-Config.DailyRareStarRevoraPoints-Config.DailyRareChangeRevoraPoints,"paid reroll commits a different requirement once while preserving the purchased star");
        RareNpc(paid,19);ushort study=paid.Entity.SubClasses.StudyPoints;Parallel.For(0,12,i=>RareNpc(paid,30));
        Check(!Fenced(paid) && RareDone(paid)==1 && paid.Entity.ConquerPoints==20000-Config.DailyRareSkipCp+Config.DailyQuestCp
            && paid.Entity.SubClasses.StudyPoints==study+Config.DailyRareMaterialsStudy+Config.DailyRareStarStudyBonus,"paid completion delivers the purchased-star reward once under duplicate requests");
        DialyQuestTable.Load(paid);paid.Entity.RareMaterials=false;paid.Entity.MaterialsStar=0;DialyQuestTable.Save(paid);RareNpc(paid,19);RareNpc(paid,30);
        Check(!Fenced(paid) && RareDone(paid)==1 && paid.Entity.RareMaterials && paid.Entity.MaterialsStar==2,"legacy mirror reset cannot erase purchased stars or reopen completion");
        rareNow=rareNow.AddDays(1);RareNpc(paid,19);Check(paid.Entity.MaterialsStar==1 && RareRuns(paid)==2,"next day starts a new one-star commission without relogging");
        RareNpc(paid,30);rareNow=rareNow.AddDays(-2);RareNpc(paid,19);RareNpc(paid,30);Check(RareDone(paid)==2,"clock rollback cannot repeat an earlier Rare Materials reward");rareNow=DateTime.UtcNow.Date.AddHours(19);
        var limit=RarePlayer(town);RareNpc(limit,19);for(int n=1;n<Config.DailyRareStarMax;n++){RareNpc(limit,32);RareNpc(limit,34);}
        uint rvp=limit.Entity.RevoraPoint;RareNpc(limit,32);RareNpc(limit,34);Check(limit.Entity.MaterialsStar==Config.DailyRareStarMax && limit.Entity.RevoraPoint==rvp,"maximum star cannot be charged again");
        for(int n=0;n<Config.DailyRareChangeMax;n++){RareNpc(limit,19);RareNpc(limit,29);}rvp=limit.Entity.RevoraPoint;RareNpc(limit,19);RareNpc(limit,29);
        Check(limit.Entity.MaterialsTimes==Config.DailyRareChangeMax && limit.Entity.RevoraPoint==rvp,"maximum rerolls preserve the requirement and RVP balance");
        foreach(byte option in new byte[]{29,30,34})foreach(string table in new[]{"entities","revora_rare_runs"}) {
            var p=RarePlayer(town);RareNpc(p,19);if(option==34)RareNpc(p,32);uint item=p.Entity.MaterialstemId;
            Query("CREATE FUNCTION suppress_rare() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rare BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_rare()");
            try{RareNpc(p,option);}finally{Query("DROP TRIGGER suppress_rare ON "+table+";DROP FUNCTION suppress_rare()");}
            Check(Fenced(p) && RareDone(p)==0 && p.Entity.ConquerPoints==20000 && p.Entity.RevoraPoint==200 && p.Entity.MaterialsStar==1 && p.Entity.MaterialsTimes==0
                && p.Entity.MaterialstemId==item && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==200,"failed paid option "+option+" retains currency and commission: "+table);
            p=ReloadBall(p);PriceStand(p);RareNpc(p,option==34?(byte)32:(byte)19);RareNpc(p,option);
            Check(!Fenced(p) && (option==29?p.Entity.MaterialsTimes==1:option==34?p.Entity.MaterialsStar==2:RareDone(p)==1),"ordinary reconnect retries paid option "+option+" after "+table+" failure");
        }
        foreach(string fault in new[]{"evidence","profile","delete","stack","reward","rune","receipt","skills","recovery"}) {
            var p=fault=="recovery"?PendingFirstLife(town):RarePlayer(town);PriceStand(p);p.Entity.ConquerPoints=20000;p.Entity.RevoraPoint=200;EntityTable.SaveEntity(p);
            if(fault=="skills"){p.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(p);}
            RareNpc(p,19);var gems=RareGems(p,fault=="stack"?2:0);ulong xp=p.Entity.Experience;
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="profile"?"entities":fault=="receipt"?"revora_rare_runs":fault=="skills"?"skills":fault=="recovery"?"revora_first_rebirths":"items";
            string verb=fault=="evidence"?"INSERT":fault=="delete"?"DELETE":fault=="reward" || fault=="rune" || fault=="skills"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_rare() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rare BEFORE "+verb+" ON "+table+" FOR EACH ROW"+(fault=="rune"?" WHEN(NEW.id=4030101)":"")+" EXECUTE FUNCTION suppress_rare()");
            try{RareNpc(p,28);}finally{Query("DROP TRIGGER suppress_rare ON "+table+";DROP FUNCTION suppress_rare()");}
            Check(Fenced(p) && RareDone(p)==0 && p.Entity.ConquerPoints==20000 && p.Entity.RevoraPoint==200 && p.Entity.Experience==xp && p.Inventory.ContainsUID(gems.UID)
                && SavedRune(gems,"stacksize")==gems.StackSize,"failed "+fault+" rolls back the entire gem reward and offering");
            Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0,"failed claim saves no P7 soul");
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);PriceStand(p);RareNpc(p,19);RareNpc(p,28);Check(!Fenced(p) && RareDone(p)==1 && (fault!="recovery" || LifePending(p)==0),"production reload retries the complete commission after "+fault+" failure");
            if(fault=="evidence")VerifyStudyAward(p,"rare");VerifyDailySoul(p,"rare");
        }
        foreach(string balance in new[]{"conquerpoints","revorapoint","studypoints"}) {
            var p=RarePlayer(town);RareNpc(p,19);var gems=RareGems(p);Query("UPDATE entities SET "+balance+"="+balance+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);RareNpc(p,28);
            Check(Fenced(p) && RareDone(p)==0 && p.Inventory.ContainsUID(gems.UID),"stale "+balance+" cannot consume gems or overwrite a newer reward");
        }
        foreach(string edit in new[]{"owner","stack","bound","locked","type"}) {
            var p=RarePlayer(town);RareNpc(p,19);var gems=RareGems(p);
            string mutation=edit=="owner"?"entityid=1":edit=="stack"?"stacksize=2":edit=="bound"?"bound=1":edit=="locked"?"locked=1":"id=id+1";
            Query("UPDATE items SET "+mutation+" WHERE uid=@uid","uid",(long)gems.UID);RareNpc(p,28);
            Check(Fenced(p) && RareDone(p)==0 && p.Entity.ConquerPoints==20000 && p.Entity.Experience==5000 && p.Inventory.ContainsUID(gems.UID),"durable gem "+edit+" edit cannot be overwritten by stale turn-in");
        }
        var rejected=RarePlayer(town);
        Query("CREATE FUNCTION suppress_rare() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rare BEFORE INSERT ON revora_rare_runs FOR EACH ROW EXECUTE FUNCTION suppress_rare()");
        try{RareNpc(rejected,19);}finally{Query("DROP TRIGGER suppress_rare ON revora_rare_runs;DROP FUNCTION suppress_rare()");}
        Check(Fenced(rejected) && RareRuns(rejected)==0 && rejected.Entity.PriceMaterialsID==0,"suppressed initial record cannot create a RAM-only commission");
        rejected=ReloadBall(rejected);PriceStand(rejected);RareNpc(rejected,19);Check(!Fenced(rejected) && RareRuns(rejected)==1,"production reload can accept after a failed initial save");
        foreach(string state in new[]{"missing","wrong-grade","locked","suspicious","expired","curse","cp-full","rvp-full","study-full"}) {
            var p=RarePlayer(town);RareNpc(p,19);var gems=RareGems(p);if(state=="missing")DisRemove(p,gems);if(state=="wrong-grade")gems.ID++;
            if(state=="locked")gems.Lock=1;if(state=="suspicious")gems.Suspicious=true;if(state=="expired"){gems.Days=1;gems.DayStamp=DateTime.Now.AddDays(-2);}
            if(state=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);if(state=="cp-full")p.Entity.ConquerPoints=uint.MaxValue;
            if(state=="rvp-full")p.Entity.RevoraPoint=uint.MaxValue;if(state=="study-full")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;
            EntityTable.SaveEntity(p);RareNpc(p,28);Check(!Fenced(p) && RareDone(p)==0 && p.Entity.Experience==5000,"unclaimable commission preserves the whole reward: "+state);
        }
        foreach(string state in new[]{"remote","trade","dead","fake","unloaded","low-level","replaced"}) {
            var p=RarePlayer(town);if(state=="remote")p.Entity.X=1;if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;if(state=="low-level")p.Entity.Level=74;
            if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);RareNpc(p,19);RareNpc(p,30);
            Check(!Fenced(p) && RareRuns(p)==0 && p.Entity.ConquerPoints==20000,"ineligible service request cannot create or charge a commission: "+state);
        }
        var full=RarePlayer(town);RareNpc(full,19);var partial=RareGems(full,2);while(full.Inventory.Count<40)Pack(full,1088001,1);RareNpc(full,28);
        Check(!Fenced(full) && RareDone(full)==0 && partial.StackSize==5,"full bag with a remaining gem stack keeps the offering");
        DisRemove(full,partial);RareGems(full);RareNpc(full,19);RareNpc(full,28);
        Check(!Fenced(full) && RareDone(full)==0,"one freed slot cannot hold both daily gifts");
        DisRemove(full,full.Inventory.Objects.First(i=>i.ID==1088001));RareNpc(full,19);RareNpc(full,28);
        Check(!Fenced(full) && RareDone(full)==1 && full.Inventory.Count==40,"consumed gems plus one free slot admits both daily gifts");
        RareNpc(full,19);RareNpc(full,28);
        Check(!Fenced(full) && RareDone(full)==1 && full.Inventory.Count==40,"full bag replay cannot duplicate the daily Chi Token");VerifyDailySoul(full,"rare");
        var bound=RarePlayer(town);RareNpc(bound,19);var boundGems=RareGems(bound,2);boundGems.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundGems.UID);RareNpc(bound,28);
        Check(!Fenced(bound) && RareDone(bound)==1 && boundGems.StackSize==2 && bound.Inventory.Objects.Single(i=>i.ID==Config.DailyQuestChiToken).Bound,"bound input retains binding and only required stacked quantity is consumed");
        var zero=RarePlayer(town);RareNpc(zero,19);for(int n=0;n<Config.DailyRareGemAmount;n++)Pack(zero,zero.Entity.MaterialstemId,0);RareNpc(zero,28);
        Check(!Fenced(zero) && RareDone(zero)==1 && zero.Inventory.Count==2,"each legacy zero-stack gem counts as one owned singleton");
        var cap=RarePlayer(town);cap.Entity.Level=Config.LevelCap;EntityTable.SaveEntity(cap);RareNpc(cap,19);RareNpc(cap,30);
        Check(!Fenced(cap) && RareDone(cap)==1 && cap.Entity.Experience==5000,"maximum-level commission preserves saved EXP while granting the non-EXP bundle");
        var nil=RarePlayer(town);Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL,studypoints=NULL WHERE uid=@uid","uid",(long)nil.Entity.UID);
        nil=ReloadBall(nil);PriceStand(nil);RareNpc(nil,19);RareGems(nil);RareNpc(nil,28);
        Check(!Fenced(nil) && RareDone(nil)==1 && nil.Entity.ConquerPoints==Config.DailyQuestCp && nil.Entity.RevoraPoint==Config.DailyQuestRevoraPoints,"nullable legacy balances can receive the whole commission reward");
        var concurrent=RarePlayer(town);RareNpc(concurrent,19);var offering=RareGems(concurrent,2);
        Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(concurrent);else RareNpc(concurrent,28);});
        Check(!Fenced(concurrent) && RareDone(concurrent)==1 && offering.StackSize==2,"concurrent turn-in and autosave cannot repeat the offering or reward");
        var mirror=RarePlayer(town);RareNpc(mirror,19);RareGems(mirror);mirror.Entity.MaterialsStar=255;mirror.Entity.MaterialstemId=0;RareNpc(mirror,28);
        Check(!Fenced(mirror) && RareDone(mirror)==1 && mirror.Entity.MaterialsStar==1 && mirror.Entity.SubClasses.StudyPoints==Config.DailyRareMaterialsStudy,"mutable legacy mirrors cannot fabricate upgraded rewards or alter the saved offering");
        var previous=RarePlayer(town);RareNpc(previous,19);
        Query("UPDATE revora_rare_runs SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)previous.Entity.UID);
        RareNpc(previous,19);RareNpc(previous,30);
        Check(!Fenced(previous) && RareDone(previous)==1 && previous.Inventory.Count==1
            && Scalar("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid AND soul_uid IS NOT NULL",previous.Entity.UID)==0,"previous P7 flag cannot restore removed daily reward");
        var valuable=RarePlayer(town);RareNpc(valuable,19);Parallel.For(0,12,i=>RareNpc(valuable,30));VerifyDailySoul(valuable,"rare");
        valuable=ReloadBall(valuable);PriceStand(valuable);RareNpc(valuable,19);RareNpc(valuable,30);VerifyDailySoul(valuable,"rare");
        Call(RareQuest,null,"Initialize");VerifyDailySoul(valuable,"rare");
        VerifyRareLegacyAndOffers(town);
    }
    static void VerifyRareMining(string runtime,Map town) {
        Call(MiningService,null,"Initialize");Call(MinePassage,null,"Initialize");Call(BookShop,null,"Initialize");ShopFile.Load();
        MiningService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>miningNow));BookShop.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>miningNow));
        World(runtime,1028);World(runtime,1025);var phoenix=World(runtime,1011);MiningNpc(town,1);MiningNpc(town,10080);MiningNpc(phoenix,125);
        for(int route=0;route<4;route++) {
            rareRoll=route;var p=RarePlayer(town);p.Entity.Body=1003;p.Entity.Class=15;EntityTable.SaveEntity(p);RareNpc(p,19);
            byte stars=(byte)(1+route%3);for(int n=1;n<stars;n++){RareNpc(p,32);RareNpc(p,34);}uint type=p.Entity.MaterialstemId;
            p=ReloadBall(p);PriceStand(p);Call(RareQuest,null,"Refresh",p);
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.PriceMaterialsID==route+1 && p.Entity.MaterialsStar==stars
                && p.Entity.RevoraPoint==200-(stars-1)*Config.DailyRareStarRevoraPoints,"accepted commission and purchased star survive ordinary reload: gem="+type);
            AdventureNear(p,1);p.Entity.AdjustMoney(2200);ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);var tool=p.Inventory.Objects.Single(i=>i.ID==562000);
            ItemHandler.EquipItem(new ItemUsage(true){UID=tool.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            Check(!Fenced(p) && BookPurchases(p)==1 && p.Equipment.TryGetItem(4)==tool,"commission miner buys and equips its own pickaxe");
            if(route<3){AdventureNear(p,10080);p.ActiveNpc=10080;MineTravel(p);}else{AdventureGo(p,1011);AdventureNear(p,125);AdventureNpc(p,125);}
            Check(!Fenced(p) && p.Entity.MapID==(route<3?1028:1025),"ordinary route reaches the mine supplying gem "+type);
            int gemIndex=route<3?route:2;MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==10000?0:max==100?99:max==3 || max==4?gemIndex:0));
            StartMining(p);for(int n=0;n<Config.DailyRareGemAmount;n++){miningNow=miningNow.AddSeconds(2);MineTick(p);}
            Check(!Fenced(p) && MineCount(p)==Config.DailyRareGemAmount && p.Inventory.Objects.Count(i=>i.ID==type)==Config.DailyRareGemAmount,"native mining yields the complete owned gem offering: "+type);
            if(route==3)AdventureGo(p,1011);AdventureGo(p,1002);AdventureNear(p,79882);
            uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;ulong xp=p.Entity.Experience,award=p.ExpBall;
            RareNpc(p,19);RareNpc(p,28);
            Check(!Fenced(p) && RareDone(p)==1 && p.Entity.RareMaterials && !p.Inventory.Contains(type,1) && p.Inventory.Contains(Config.DailyQuestChiToken,1)
                && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.Experience==xp+award
                && p.Entity.SubClasses.StudyPoints==study+Config.DailyRareMaterialsStudy+(stars-1)*Config.DailyRareStarStudyBonus,"walked mine-and-return route commits the entire star reward: "+type);
            VerifyStudyAward(p,"rare");VerifyDailySoul(p,"rare");
            p=ReloadBall(p);Call(RareQuest,null,"Refresh",p);Check(!Fenced(p) && RareDone(p)==1 && p.Entity.RareMaterials && p.Entity.MaterialsStar==stars
                && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Inventory.Contains(Config.DailyQuestChiToken,1),"production reload preserves mined-gem reward and paid stars: "+type);
        }
    }
    static void VerifyRareLegacyAndOffers(Map town) {
        foreach(bool done in new[]{false,true}) {
            var p=RarePlayer(town);DialyQuestTable.Load(p);p.Entity.PriceMaterialsID=4;p.Entity.MaterialstemId=700031;p.Entity.MaterialsTimes=2;p.Entity.MaterialsStar=3;p.Entity.RareMaterials=done;DialyQuestTable.Save(p);RareNpc(p,19);
            Check(!Fenced(p) && RareRuns(p)==1 && p.Entity.PriceMaterialsID==4 && p.Entity.MaterialsTimes==2 && p.Entity.MaterialsStar==1 && p.Entity.RareMaterials==done,
                "saved current-day legacy state imports without inventing an unrecorded old paid star: done="+done);
        }
        var frozen=RarePlayer(town);RareNpc(frozen,19);uint cp=Config.DailyQuestCp,fee=Config.DailyRareStarRevoraPoints;ushort bonus=Config.DailyRareStarStudyBonus;
        try{Config.DailyQuestCp+=100;Config.DailyRareStarRevoraPoints+=100;Config.DailyRareStarStudyBonus+=100;RareNpc(frozen,32);RareNpc(frozen,34);RareNpc(frozen,19);RareNpc(frozen,30);}
        finally{Config.DailyQuestCp=cp;Config.DailyRareStarRevoraPoints=fee;Config.DailyRareStarStudyBonus=bonus;}
        Check(!Fenced(frozen) && RareDone(frozen)==1 && frozen.Entity.ConquerPoints==20000-Config.DailyRareSkipCp+cp
            && frozen.Entity.RevoraPoint==200-fee+Config.DailyQuestRevoraPoints && frozen.Entity.SubClasses.StudyPoints==Config.DailyRareMaterialsStudy+bonus,"accepted commission freezes its reward, upgrade fee and star bonus");
        foreach(string invalid in new[]{"no-accept","cancel","expire","wrong-kind","poor-rvp","poor-cp"}) {
            var p=RarePlayer(town);if(invalid!="no-accept")RareNpc(p,19);if(invalid=="cancel")RareNpc(p,255);
            if(invalid=="expire")rareNow=rareNow.AddMinutes(3);if(invalid=="wrong-kind")RareNpc(p,32);
            if(invalid=="poor-rvp"){p.Entity.RevoraPoint=0;EntityTable.SaveEntity(p);}if(invalid=="poor-cp")p.Entity.ConquerPoints=0;
            RareNpc(p,invalid=="poor-rvp"?(byte)29:(byte)30);
            Check(!Fenced(p) && RareDone(p)==0 && p.Entity.MaterialsTimes==0 && p.Entity.ConquerPoints==(invalid=="poor-cp"?0u:20000u),"unreviewed or unfunded continuation does not charge: "+invalid);
            if(invalid=="expire")rareNow=rareNow.AddMinutes(-3);
        }
        var minimum=RarePlayer(town);minimum.Entity.Level=Config.DailyRareMinLevel;EntityTable.SaveEntity(minimum);RareNpc(minimum,19);RareNpc(minimum,30);
        Check(!Fenced(minimum) && RareDone(minimum)==1,"exact level-75 minimum can accept and finish a commission");
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");var mentor=RarePlayer(town);var child=RarePlayer(town);MentorLink(mentor,child,100);
        child.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(child);RareNpc(child,19);RareGems(child);
        Query("CREATE FUNCTION suppress_rare() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rare BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_rare()");
        try{RareNpc(child,28);}finally{Query("DROP TRIGGER suppress_rare ON apprentice;DROP FUNCTION suppress_rare()");}
        Check(Fenced(child) && RareDone(child)==0 && child.Entity.Level==135 && child.Entity.ConquerPoints==20000 && MentorBalance(child)==100,"mentor failure retains gem offering and entire commission reward");
        child=ReloadBall(child);PriceStand(child);Revora.Database.KnownPersons.LoadMentor(child);RareNpc(child,19);RareNpc(child,28);
        Check(!Fenced(child) && RareDone(child)==1 && child.Entity.Level==136 && MentorBalance(child)==372,"ordinary reload commits commission progression and mentor credit together");
    }
}
