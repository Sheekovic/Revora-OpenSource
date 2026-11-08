using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type SoulBall=typeof(Entity).Assembly.GetType("Revora.Game.Features.DailySoulBall");
    static DateTime soulBallNow=DateTime.UtcNow.Date.AddHours(19);
    static long SoulRuns(GameState p){return Scalar("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid",p.Entity.UID);}
    static long SoulDone(GameState p){return Scalar("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid AND completed_day<>'' AND reward_uid IS NOT NULL",p.Entity.UID);}
    static long SoulSpirits(GameState p){return Scalar("SELECT COALESCE(sum(spirits),0) FROM revora_soul_ball_runs WHERE uid=@uid",p.Entity.UID);}
    static GameState SoulBallPlayer(Map town){var p=PricePlayer(town);p.LastResetTime=soulBallNow.ToLocalTime();EntityTable.SaveEntity(p);return p;}
    static void SoulNpc(GameState p,byte option){PriceNpc(p,option);}
    static ConquerItem SoulBead(GameState p){return p.Inventory.Objects.Single(i=>new uint[]{729611,729612,729613,729614,729703}.Contains(i.ID));}
    static GameState FullSoul(Map town,uint type=729703) {
        var p=SoulBallPlayer(town);Pack(p,type,1);DialyQuestTable.Load(p);p.Entity.spiritBeadsCount=2500;DialyQuestTable.Save(p);SoulNpc(p,7);return p;
    }
    static void VerifyBeadSoul(GameState p) {
        var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        Check(!Fenced(p) && souls.Length==0 && Scalar("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid AND soul_uid IS NOT NULL",p.Entity.UID)==0,"Soul Ball pays no P7 soul or soul receipt");
        long promised=Scalar("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid AND completed_day<>'' AND rune_reward",p.Entity.UID);
        Check(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==promised
            && Scalar("SELECT count(*) FROM revora_soul_ball_runs r JOIN items i ON i.uid=r.rune_uid AND i.entityid=r.uid WHERE r.uid=@uid AND r.completed_day<>'' AND r.rune_reward AND i.id=4030101 AND i.bound=1",p.Entity.UID)==promised,
            "Soul Ball preserves each promised bound rune and exact durable receipt");
    }
    static void VerifySoulBall(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Call(SoulBall,null,"Initialize");Call(SoulBall,null,"Initialize");MiningNpc(town,79882);
        SoulBall.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>soulBallNow));
        foreach(byte level in new byte[]{1,69,70,99,100,119,120,140,141})
            Check((uint)Call(SoulBall,null,"SpiritValue",level)==(level<70?1u:level<100?2u:level<120?3u:4u),"spirit level boundary: "+level);
        uint[] types={729611,729612,729613,729614,729703};uint[] needs={2500,2000,1500,1000,500};ushort[] studies={15,20,25,40,80};
        for(int grade=0;grade<5;grade++) {
            var p=SoulBallPlayer(town);
            if(grade==0 || grade==4) {
                SoulNpc(p,7);Parallel.For(0,8,i=>SoulNpc(p,grade==4?(byte)9:(byte)8));
                Check(!Fenced(p) && SoulRuns(p)==1 && SoulBead(p).ID==types[grade] && p.Entity.ConquerPoints==20000-(grade==4?800u:0)
                    && SoulSpirits(p)==0 && Scalar("SELECT need FROM revora_soul_ball_runs WHERE uid=@uid",p.Entity.UID)==needs[grade],"initial grade "+grade+" issues its exact owned bead and charges once");
                ItemHandler.UseItem(SoulBead(p),p);Check(SoulDone(p)==0,"empty grade "+grade+" cannot claim");
                p=ReloadBall(p);Call(SoulBall,null,"Refresh",p);Check(!Fenced(p) && SoulRuns(p)==1 && SoulBead(p).ID==types[grade],"grade "+grade+" issuance survives production reload");
            }
            // Existing saved progress is imported once; it never remains the daily authority.
            p=FullSoul(town,types[grade]);
            var bead=SoulBead(p);ulong xp=p.Entity.Experience,award=p.ExpBall;uint bless=p.Entity.HeavenBlessing;
            Parallel.For(0,8,i=>ItemHandler.UseItem(bead,p));
            Check(!Fenced(p) && SoulDone(p)==1 && p.Entity.ConquerPoints==22500 && p.Entity.RevoraPoint==15 && p.Entity.SubClasses.StudyPoints==studies[grade]
                && p.Entity.HeavenBlessing==bless+(grade==4?172800u:86400u) && p.Entity.Experience==xp+award && !p.Inventory.ContainsUID(bead.UID)
                && p.Inventory.Contains(Config.DailyQuestChiToken,1),"grade "+grade+" consumes the exact bead and commits its complete reward once");
            p=ReloadBall(p);Call(SoulBall,null,"Refresh",p);Check(!Fenced(p) && p.Entity.SpiritBeads && SoulDone(p)==1 && p.Entity.HeavenBlessing==bless+(grade==4?172800u:86400u)
                && p.Entity.ConquerPoints==22500 && p.Entity.SubClasses.StudyPoints==studies[grade],"grade "+grade+" reward and blessing survive production reload");
        }
        VerifySoulGrades(runtime,town);
        VerifySoulTerato(runtime,town);
        VerifySoulHunt(runtime,town);
        VerifySoulSourceGuards(town);
        VerifySoulWorlds(runtime,town);
        var anniversary=SoulBallPlayer(town);SoulNpc(anniversary,7);SoulNpc(anniversary,9);anniversary.Entity.SpiritBeads=true;
        Call(SoulBall,null,"Refresh",anniversary);SoulNpc(anniversary,7);
        Check(anniversary.Entity.SpiritBeads && !(bool)Call(SoulBall,null,"CompletedToday",anniversary),"daily display refresh cannot erase a separate Anniversary completion flag");
        var paid=SoulBallPlayer(town);SoulNpc(paid,7);Parallel.For(0,12,i=>SoulNpc(paid,31));
        Check(!Fenced(paid) && SoulDone(paid)==1 && paid.Entity.ConquerPoints==17500 && paid.Entity.SubClasses.StudyPoints==40 && paid.Entity.HeavenBlessing==172800,"paid finish without a bead charges and rewards once");
        VerifyBeadSoul(paid);paid=ReloadBall(paid);PriceStand(paid);VerifyBeadSoul(paid);
        DialyQuestTable.Load(paid);paid.Entity.SpiritBeads=false;DialyQuestTable.Save(paid);SoulNpc(paid,7);SoulNpc(paid,31);
        Check(SoulDone(paid)==1 && paid.Entity.SpiritBeads && paid.Entity.ConquerPoints==17500,"legacy reset cannot reopen a completed Soul Ball day");
        soulBallNow=soulBallNow.AddDays(1);SoulNpc(paid,7);SoulNpc(paid,31);Check(SoulDone(paid)==2,"next Pacific day permits one new reward");
        soulBallNow=soulBallNow.AddDays(-2);SoulNpc(paid,7);SoulNpc(paid,31);Check(SoulDone(paid)==2,"clock rollback cannot repeat an older day");soulBallNow=DateTime.UtcNow.Date.AddHours(19);
        foreach(string fault in new[]{"evidence","profile","blessing","bead","reward","rune","receipt","skills","recovery"}) {
            var p=fault=="recovery"?PendingFirstLife(town):FullSoul(town);PriceStand(p);p.Entity.ConquerPoints=20000;
            if(fault=="recovery"){Pack(p,729703,1);DialyQuestTable.Load(p);p.Entity.spiritBeadsCount=500;DialyQuestTable.Save(p);SoulNpc(p,7);}
            if(fault=="skills")p.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(p);
            if(fault=="rune")Query("UPDATE revora_soul_ball_runs SET rune_reward=true WHERE uid=@uid","uid",(long)p.Entity.UID);
            var bead=SoulBead(p);ulong xp=p.Entity.Experience;uint blessing=p.Entity.HeavenBlessing;
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="profile" || fault=="blessing"?"entities":fault=="receipt"?"revora_soul_ball_runs":fault=="skills"?"skills":fault=="recovery"?"revora_first_rebirths":"items";
            string verb=fault=="evidence"?"INSERT":fault=="bead"?"DELETE":fault=="reward" || fault=="rune" || fault=="skills"?"INSERT":fault=="blessing"?"UPDATE OF heavenblessingtime":"UPDATE";
            Query("CREATE FUNCTION suppress_soul_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_ball BEFORE "+verb+" ON "+table+" FOR EACH ROW"+(fault=="rune"?" WHEN(NEW.id=4030101)":"")+" EXECUTE FUNCTION suppress_soul_ball()");
            try{ItemHandler.UseItem(bead,p);}finally{Query("DROP TRIGGER suppress_soul_ball ON "+table+";DROP FUNCTION suppress_soul_ball()");}
            Check(Fenced(p) && SoulDone(p)==0 && p.Inventory.ContainsUID(bead.UID) && p.Entity.ConquerPoints==20000 && p.Entity.Experience==xp && p.Entity.HeavenBlessing==blessing
                && Scalar("SELECT heavenblessingtime FROM entities WHERE uid=@uid",p.Entity.UID)==blessing,"suppressed "+fault+" retains the complete bead reward");
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);ItemHandler.UseItem(SoulBead(p),p);Check(!Fenced(p) && SoulDone(p)==1 && (fault!="recovery" || LifePending(p)==0),"ordinary reconnect retries after "+fault+" failure");
            if(fault=="evidence")VerifyStudyAward(p,"soul-ball");
        }
        var noBead=SoulBallPlayer(town);for(int i=0;i<39;i++)Pack(noBead,1088001,1);
        SoulNpc(noBead,7);SoulNpc(noBead,31);
        Check(!Fenced(noBead) && SoulDone(noBead)==0 && noBead.Entity.ConquerPoints==20000,"paid completion without a bead needs two free slots before charging");
        DisRemove(noBead,noBead.Inventory.Objects.First());SoulNpc(noBead,7);SoulNpc(noBead,31);
        Check(!Fenced(noBead) && SoulDone(noBead)==1 && noBead.Inventory.Count==40,"paid completion without a bead admits both gifts in two free slots");VerifyBeadSoul(noBead);
        var valuable=SoulBallPlayer(town);SoulNpc(valuable,7);SoulNpc(valuable,9);
        Query("UPDATE revora_soul_ball_runs SET spirits=need WHERE uid=@uid","uid",(long)valuable.Entity.UID);
        Query("UPDATE revora_soul_ball_runs SET valuable_reward=true WHERE uid=@uid","uid",(long)valuable.Entity.UID);
        while(valuable.Inventory.Count<40)Pack(valuable,1088001,1);
        var oldBead=SoulBead(valuable);ItemHandler.UseItem(oldBead,valuable);
        Check(!Fenced(valuable) && SoulDone(valuable)==0 && valuable.Inventory.Count==40,"new bead contract needs a free slot beside the consumed bead");
        DisRemove(valuable,valuable.Inventory.Objects.First(i=>i.ID==1088001));ItemHandler.UseItem(oldBead,valuable);
        Check(!Fenced(valuable) && SoulDone(valuable)==1 && valuable.Inventory.Count==40,"consumed bead plus free slot fits both gifts without reviving old P7 flag");
        valuable=ReloadBall(valuable);ItemHandler.UseItem(oldBead,valuable);VerifyBeadSoul(valuable);
        foreach(string table in new[]{"entities","items","revora_soul_ball_runs"}) {
            var p=SoulBallPlayer(town);SoulNpc(p,7);string verb=table=="entities"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_soul_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_ball BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_soul_ball()");
            try{SoulNpc(p,9);}finally{Query("DROP TRIGGER suppress_soul_ball ON "+table+";DROP FUNCTION suppress_soul_ball()");}
            Check(Fenced(p) && SoulRuns(p)==0 && p.Inventory.Count==0 && p.Entity.ConquerPoints==20000,"failed "+table+" cannot charge for an unsaved Super bead");
            p=ReloadBall(p);PriceStand(p);SoulNpc(p,7);SoulNpc(p,9);Check(!Fenced(p) && SoulRuns(p)==1 && p.Entity.ConquerPoints==19200,"reconnect retries failed Super issuance: "+table);
        }
        foreach(string balance in new[]{"conquerpoints","revorapoint","studypoints","heavenblessingtime"}) {
            var p=FullSoul(town);var bead=SoulBead(p);Query("UPDATE entities SET "+balance+"=COALESCE("+balance+",0)+1 WHERE uid=@uid","uid",(long)p.Entity.UID);ItemHandler.UseItem(bead,p);
            Check(Fenced(p) && SoulDone(p)==0 && p.Inventory.ContainsUID(bead.UID),"stale "+balance+" cannot overwrite a newer reward");
        }
        foreach(string edit in new[]{"owner","stack","bound","locked","type"}) {
            var p=FullSoul(town);var bead=SoulBead(p);string mutation=edit=="owner"?"entityid=1":edit=="stack"?"stacksize=2":edit=="bound"?"bound=1":edit=="locked"?"locked=1":"id=id+1";
            Query("UPDATE items SET "+mutation+" WHERE uid=@uid","uid",(long)bead.UID);ItemHandler.UseItem(bead,p);Check(Fenced(p) && SoulDone(p)==0 && p.Inventory.ContainsUID(bead.UID),"stale bead "+edit+" is fenced before consumption");
        }
        foreach(string state in new[]{"locked","suspicious","expired","curse","cp-full","rvp-full","study-full","bless-full"}) {
            var p=FullSoul(town);var bead=SoulBead(p);if(state=="locked")bead.Lock=1;if(state=="suspicious")bead.Suspicious=true;if(state=="expired"){bead.Days=1;bead.DayStamp=DateTime.Now.AddDays(-2);}
            if(state=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);if(state=="cp-full")p.Entity.ConquerPoints=uint.MaxValue;if(state=="rvp-full")p.Entity.RevoraPoint=uint.MaxValue;
            if(state=="study-full")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;if(state=="bless-full")Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.BlessingBalance"),null,"Set",p,uint.MaxValue);
            EntityTable.SaveEntity(p);ItemHandler.UseItem(bead,p);Check(!Fenced(p) && SoulDone(p)==0 && p.Inventory.ContainsUID(bead.UID),"unclaimable "+state+" preserves the whole reward");
        }
        foreach(string state in new[]{"remote","trade","dead","fake","unloaded","low-level","replaced","no-review","cancel","expire","poor"}) {
            var p=SoulBallPlayer(town);if(state!="no-review")SoulNpc(p,7);if(state=="remote")p.Entity.X=1;if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;if(state=="low-level")p.Entity.Level=99;if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            if(state=="cancel")SoulNpc(p,255);if(state=="expire")soulBallNow=soulBallNow.AddMinutes(3);if(state=="poor")p.Entity.ConquerPoints=0;SoulNpc(p,9);SoulNpc(p,31);
            Check(!Fenced(p) && SoulRuns(p)==0 && p.Entity.ConquerPoints==(state=="poor"?0u:20000u),"ineligible or unreviewed request is not charged: "+state);if(state=="expire")soulBallNow=soulBallNow.AddMinutes(-3);
        }
        var full=SoulBallPlayer(town);while(full.Inventory.Count<40)Pack(full,1088001,1);SoulNpc(full,7);SoulNpc(full,9);Check(!Fenced(full) && SoulRuns(full)==0 && full.Entity.ConquerPoints==20000,"full bag cannot be charged for bead issuance");
        full=FullSoul(town);while(full.Inventory.Count<40)Pack(full,1088001,1);ItemHandler.UseItem(SoulBead(full),full);Check(!Fenced(full) && SoulDone(full)==1 && full.Inventory.Count==40,"consumed bead makes room for its Chi Token");
        var cap=FullSoul(town);cap.Entity.Level=Config.LevelCap;EntityTable.SaveEntity(cap);ItemHandler.UseItem(SoulBead(cap),cap);Check(!Fenced(cap) && SoulDone(cap)==1 && cap.Entity.HeavenBlessing==172800 && cap.Inventory.Contains(Config.DailyQuestChiToken,1),"level cap retains all non-EXP reward components");
        var mixed=SoulBallPlayer(town);Pack(mixed,729703,1);Pack(mixed,3001172,1);DialyQuestTable.Load(mixed);mixed.Entity.spiritBeadsCount=15000;DialyQuestTable.Save(mixed);SoulNpc(mixed,7);SoulNpc(mixed,31);ItemHandler.UseItem(SoulBead(mixed),mixed);
        Check(!Fenced(mixed) && SoulRuns(mixed)==0 && SoulDone(mixed)==0 && mixed.Entity.spiritBeadsCount==15000,"ambiguous Anniversary progress cannot authorize daily rewards");
        var frozen=FullSoul(town);uint cp=Config.DailyQuestCp,fee=Config.DailySpiritBeadSkipCp;ushort skip=Config.DailySoulBallStudy;
        try{Config.DailyQuestCp+=100;Config.DailySpiritBeadSkipCp+=100;Config.DailySoulBallStudy+=100;SoulNpc(frozen,7);SoulNpc(frozen,31);}
        finally{Config.DailyQuestCp=cp;Config.DailySpiritBeadSkipCp=fee;Config.DailySoulBallStudy=skip;}
        Check(!Fenced(frozen) && SoulDone(frozen)==1 && frozen.Entity.ConquerPoints==20000-fee+cp && frozen.Entity.SubClasses.StudyPoints==skip,"accepted bead freezes skip price and complete reward");
        foreach(string fault in new[]{"none","items","revora_soul_ball_runs"}) {
            var p=FullSoul(town);uint lost=SoulBead(p).UID;DisRemove(p,SoulBead(p));SoulNpc(p,7);
            if(fault!="none")Query("CREATE FUNCTION suppress_soul_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_ball BEFORE "+(fault=="items"?"INSERT":"UPDATE")+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_soul_ball()");
            try{Parallel.For(0,8,i=>SoulNpc(p,35));}finally{if(fault!="none")Query("DROP TRIGGER suppress_soul_ball ON "+fault+";DROP FUNCTION suppress_soul_ball()");}
            if(fault!="none") {
                Check(Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT bead FROM revora_soul_ball_runs WHERE uid=@uid",p.Entity.UID)==lost,"failed replacement preserves the original receipt: "+fault);
                p=ReloadBall(p);PriceStand(p);SoulNpc(p,7);SoulNpc(p,35);
            }
            Check(!Fenced(p) && SoulBead(p).UID!=lost && SoulBead(p).Bound && SoulSpirits(p)==500 && p.Entity.ConquerPoints==20000
                && Scalar("SELECT array_length(replaced_beads,1) FROM revora_soul_ball_runs WHERE uid=@uid",p.Entity.UID)==1,"lost bead replacement preserves earned spirits without charging again: "+fault);
            ItemHandler.UseItem(SoulBead(p),p);Check(!Fenced(p) && SoulDone(p)==1,"replacement can finish the original quest: "+fault);
        }
        var banked=FullSoul(town);var bankBead=SoulBead(banked);banked.Inventory.Remove(bankBead.UID,Revora.Game.Enums.ItemUse.None,true);
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)bankBead.UID);SoulNpc(banked,7);SoulNpc(banked,35);
        Check(!Fenced(banked) && banked.Inventory.Count==0 && Scalar("SELECT bead FROM revora_soul_ball_runs WHERE uid=@uid",banked.Entity.UID)==bankBead.UID,"stored bead cannot be duplicated by a lost-bead request");
        var nullable=FullSoul(town);Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL,studypoints=NULL,heavenblessingtime=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);
        nullable=ReloadBall(nullable);ItemHandler.UseItem(SoulBead(nullable),nullable);
        Check(!Fenced(nullable) && SoulDone(nullable)==1 && nullable.Entity.ConquerPoints==2500 && nullable.Entity.RevoraPoint==15 && nullable.Entity.HeavenBlessing==172800,"nullable legacy balances commit a complete earned reward from zero");
        var minimum=SoulBallPlayer(town);minimum.Entity.Level=100;EntityTable.SaveEntity(minimum);SoulNpc(minimum,7);SoulNpc(minimum,31);
        Check(!Fenced(minimum) && SoulDone(minimum)==1,"exact level-100 minimum may complete the service");
        var singleton=FullSoul(town);var zeroBead=SoulBead(singleton);zeroBead.StackSize=0;Query("UPDATE items SET stacksize=0 WHERE uid=@uid","uid",(long)zeroBead.UID);ItemHandler.UseItem(zeroBead,singleton);
        Check(!Fenced(singleton) && SoulDone(singleton)==1 && !singleton.Inventory.ContainsUID(zeroBead.UID),"legacy zero-stack singleton is consumed exactly once");
        var identity=FullSoul(town);var real=SoulBead(identity);ItemHandler.UseItem(new ConquerItem(true){UID=real.UID,ID=real.ID},identity);
        Check(!Fenced(identity) && SoulDone(identity)==0 && identity.Inventory.ContainsUID(real.UID),"matching UID and type on a forged object cannot redeem a full bead");
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");var mentor=SoulBallPlayer(town);var child=FullSoul(town);MentorLink(mentor,child,100);
        child.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(child);
        Query("CREATE FUNCTION suppress_soul_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_ball BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_soul_ball()");
        try{ItemHandler.UseItem(SoulBead(child),child);}finally{Query("DROP TRIGGER suppress_soul_ball ON apprentice;DROP FUNCTION suppress_soul_ball()");}
        Check(Fenced(child) && SoulDone(child)==0 && child.Entity.Level==135 && child.Entity.HeavenBlessing==0 && MentorBalance(child)==100,"mentor write failure rolls back bead, blessing and all reward components");
        child=ReloadBall(child);Revora.Database.KnownPersons.LoadMentor(child);ItemHandler.UseItem(SoulBead(child),child);
        Check(!Fenced(child) && SoulDone(child)==1 && child.Entity.Level==136 && MentorBalance(child)==372 && BlessingHours(child)==0 && child.Entity.HeavenBlessing==172800,"reloaded reward commits earned mentor EXP without treating quest blessing as a Praying Stone");
    }
    static void VerifySoulGrades(string runtime,Map town) {
        World(runtime,1015);var p=SoulBallPlayer(town);DateTime start=soulBallNow;
        uint[] types={729611,729612,729613,729614,729703};uint[] needs={2500,2000,1500,1000,500};
        try {
            for(int grade=0;grade<5;grade++) {
                p.BindWorldMap(1002);PriceStand(p);SoulNpc(p,7);uint cp=p.Entity.ConquerPoints;
                // Even the old paid-Super continuation must not charge for an earned Super.
                SoulNpc(p,grade==4?(byte)9:(byte)8);var bead=SoulBead(p);
                Check(!Fenced(p) && bead.ID==types[grade] && p.Entity.ConquerPoints==cp && p.Account.State==AccountTable.AccountState.Player,
                    "completed-hunt sequence awards free grade "+grade+" to an ordinary player");
                p.BindWorldMap(1015);var xy=FlowerTile(p.Map,396,528);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;EntityTable.SaveEntity(p);
                for(int count=0;count<needs[grade]/2;count++)Call(SoulBall,null,"OnDeath",HuntMonster(p,"BanditL97"),p.Entity);
                Check(!Fenced(p) && Scalar("SELECT spirits FROM revora_soul_ball_runs WHERE uid=@uid AND completed_day=''",p.Entity.UID)==needs[grade],"grade "+grade+" fills through its full default wilderness spirit requirement");
                ItemHandler.UseItem(bead,p);Check(!Fenced(p) && SoulDone(p)==grade+1,"grade "+grade+" completes once before advancing");
                p=ReloadBall(p);soulBallNow=soulBallNow.AddDays(1);
            }
            p.BindWorldMap(1002);PriceStand(p);SoulNpc(p,7);SoulNpc(p,8);
            Check(!Fenced(p) && SoulBead(p).ID==729611 && SoulRuns(p)==6,"completed Super resets the next free cycle to Normal after reload");
            var pending=SoulBallPlayer(town);SoulNpc(pending,7);SoulNpc(pending,8);uint original=SoulBead(pending).UID;
            soulBallNow=soulBallNow.AddDays(1);SoulNpc(pending,7);SoulNpc(pending,8);
            Check(!Fenced(pending) && SoulRuns(pending)==1 && SoulBead(pending).UID==original && SoulBead(pending).ID==729611,"waiting overnight without completion cannot raise a bead grade");
            var skipped=FullSoul(town,729614);SoulNpc(skipped,7);SoulNpc(skipped,31);soulBallNow=soulBallNow.AddDays(1);SoulNpc(skipped,7);SoulNpc(skipped,8);
            Check(!Fenced(skipped) && SoulBead(skipped).ID==729611,"paid direct completion restarts the next cycle at Normal");
            uint skipFee=Config.DailySpiritBeadSkipCp;GameState freeSkip;
            try {Config.DailySpiritBeadSkipCp=0;freeSkip=FullSoul(town,729612);SoulNpc(freeSkip,7);SoulNpc(freeSkip,31);}
            finally{Config.DailySpiritBeadSkipCp=skipFee;}
            soulBallNow=soulBallNow.AddDays(1);SoulNpc(freeSkip,7);SoulNpc(freeSkip,8);
            Check(!Fenced(freeSkip) && SoulBead(freeSkip).ID==729611,"zero-price direct completion retains its action identity for the next cycle");
            var historical=SoulBallPlayer(town);DialyQuestTable.Load(historical);historical.Entity.SpiritBeads=true;DialyQuestTable.Save(historical);SoulNpc(historical,7);
            soulBallNow=soulBallNow.AddDays(1);SoulNpc(historical,7);SoulNpc(historical,8);
            Check(!Fenced(historical) && SoulBead(historical).ID==729611,"completion-only legacy history does not invent a previously earned grade");
            var adopted=FullSoul(town,729613);ItemHandler.UseItem(SoulBead(adopted),adopted);soulBallNow=soulBallNow.AddDays(1);SoulNpc(adopted,7);SoulNpc(adopted,8);
            Check(!Fenced(adopted) && SoulBead(adopted).ID==729614,"a completed owned legacy Unique bead earns the next Elite grade");
        }finally{soulBallNow=start;}
    }
    static long SoulBossCredits(GameState p){return Scalar("SELECT count(*) FROM revora_soul_ball_bosses WHERE uid=@uid",p.Entity.UID);}
    static void SoulSummon(GameState p) {Pack(p,720598,1);Pack(p,1088001,5);TeratoNpc(p,1);TeratoNpc(p,2);}
    static void VerifySoulTerato(string runtime,Map town) {
        World(runtime,2056);teratoNow=DateTime.UtcNow;Terato.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>teratoNow));
        TeratoLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));Call(Terato,null,"Initialize");
        var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,8);uint cp=p.Entity.ConquerPoints;
        for(int n=0;n<3;n++) {
            SoulSummon(p);var boss=TeratoBoss(p);SlayTerato(p,boss);long expected=Math.Min(2500,1000*(n+1));
            Check(!Fenced(p) && boss.Dead && SoulSpirits(p)==expected && SoulBossCredits(p)==n+1 && TeratoRuns(p,"state='victory'")==n+1,
                "owned Terato victory credits exactly 1000 spirits up to the bead cap: hunt "+n);
            boss.Die(p.Entity);Check(SoulSpirits(p)==expected && SoulBossCredits(p)==n+1,"released Terato corpse cannot repeat its spirit bonus");
            p=ReloadBall(p);Check(!Fenced(p) && SoulSpirits(p)==expected,"Terato spirit credit survives production reload");Call(TeratoLoot,null,"ClearGround");
        }
        ushort study=p.Entity.SubClasses.StudyPoints;ItemHandler.UseItem(SoulBead(p),p);VerifyBeadSoul(p);
        Check(!Fenced(p) && SoulDone(p)==1 && p.Entity.ConquerPoints==cp+2500 && p.Entity.SubClasses.StudyPoints==study+15 && p.Entity.HeavenBlessing==86400,
            "three paid dragon hunts fill and redeem the whole default Normal bead reward");
        foreach(string table in new[]{"revora_soul_ball_runs","revora_soul_ball_bosses","revora_terato_loot"}) {
            p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,9);SoulSummon(p);var boss=TeratoBoss(p);
            Query("CREATE FUNCTION suppress_soul_dragon() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_dragon BEFORE "+(table=="revora_soul_ball_runs"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_soul_dragon()");
            try{SlayTerato(p,boss);}finally{Query("DROP TRIGGER suppress_soul_dragon ON "+table+";DROP FUNCTION suppress_soul_dragon()");}
            Check(Fenced(p) && !boss.Dead && SoulSpirits(p)==0 && SoulBossCredits(p)==0 && TeratoRuns(p,"state='active'")==1 && TeratoStudy(p)==0,
                "failed "+table+" rolls back victory, loot and spirit progress together");
            p=ReloadBall(p);ClearTerato();TeratoNpc(p,3);boss=TeratoBoss(p);SlayTerato(p,boss);
            Check(!Fenced(p) && boss.Dead && SoulSpirits(p)==500 && SoulBossCredits(p)==1 && TeratoRuns(p)==1,"ordinary reconnect resumes the paid dragon and credits spirits once after "+table+" failure");ClearTerato();
        }
        p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,8);SoulSummon(p);var ownedBoss=TeratoBoss(p);
        var stranger=SoulBallPlayer(town);SoulNpc(stranger,7);SoulNpc(stranger,8);stranger.Entity.X=ownedBoss.X;stranger.Entity.Y=ownedBoss.Y;ownedBoss.Die(stranger.Entity);
        Check(!ownedBoss.Dead && SoulBossCredits(stranger)==0 && SoulSpirits(stranger)==0,"bystander cannot earn another player's Terato spirit bonus");
        p.Entity.X=ownedBoss.X;p.Entity.Y=ownedBoss.Y;bool finished;
        lock(p.ItemSyncRoot) {var attempt=Task.Run(()=>ownedBoss.Die(p.Entity));finished=attempt.Wait(1000);}
        Check(finished && !ownedBoss.Dead && SoulSpirits(p)==0,"busy owner defers victory without waiting under the encounter lock");
        SlayTerato(p,ownedBoss);Check(!Fenced(p) && ownedBoss.Dead && SoulSpirits(p)==1000,"deferred victory retries when the owner is available");ClearTerato();
        var fake=HuntMonster(stranger,"Pheasant");fake.MonsterInfo.ID=9916100;fake.MonsterInfo.Name="TeratoDragon";fake.Hitpoints=100;fake.Die(stranger.Entity);
        Check(SoulSpirits(stranger)==0 && SoulBossCredits(stranger)==0,"matching boss name and family cannot forge a registered Terato encounter");
        p=SoulBallPlayer(town);SoulSummon(p);ownedBoss=TeratoBoss(p);SlayTerato(p,ownedBoss);
        Check(!Fenced(p) && ownedBoss.Dead && SoulRuns(p)==0 && SoulBossCredits(p)==0,"Terato victory without a bead cannot bank spirits for a future quest");ClearTerato();
        p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,8);SoulSummon(p);ownedBoss=TeratoBoss(p);p.Entity.X=ownedBoss.X;p.Entity.Y=ownedBoss.Y;
        var pet=new Entity(EntityFlag.Monster,true){UID=p.Entity.UID-200000,Owner=p,MonsterInfo=new MonsterInformation(),MapID=town.ID,X=ownedBoss.X,Y=ownedBoss.Y,Hitpoints=1000};p.Companion=pet;
        ownedBoss.Die(pet);Check(!ownedBoss.Dead && SoulBossCredits(p)==0,"unregistered companion cannot earn Terato spirits");
        town.Companions.Add(pet.UID,pet);ownedBoss.Die(pet);
        Check(!Fenced(p) && ownedBoss.Dead && SoulSpirits(p)==1000 && SoulBossCredits(p)==1,"registered companion earns the boss spirits for its canonical owner");
        town.Companions.Remove(pet.UID);p.Companion=null;ClearTerato();
        var leader=SoulBallPlayer(town);SoulNpc(leader,7);SoulNpc(leader,8);var member=SoulBallPlayer(town);SoulNpc(member,7);SoulNpc(member,8);
        TeratoSite(leader);TeratoSite(member);TeratoTeam(leader,member);SoulSummon(leader);ownedBoss=TeratoBoss(leader);
        leader.Entity.X=ownedBoss.X;leader.Entity.Y=ownedBoss.Y;SlayTerato(member,ownedBoss);
        Check(!Fenced(leader) && !Fenced(member) && ownedBoss.Dead && SoulSpirits(member)==1000 && SoulSpirits(leader)==0 && SoulBossCredits(member)==1
            && leader.Entity.SubClasses.StudyPoints==50 && member.Entity.SubClasses.StudyPoints==50,"only the admitted killer earns spirits while both nearby teammates retain their normal Study reward");ClearTerato();
    }
    static void VerifySoulSourceGuards(Map town) {
        foreach(string invalid in new[]{"alive","fake-family","guard","boss","companion","removed","other-map","wrong-owner","anniversary","no-bead","unloaded"}) {
            var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,9);var mob=HuntMonster(p,"Pheasant");
            if(invalid=="alive")mob.Hitpoints=100;if(invalid=="fake-family")mob.MonsterInfo.ID=uint.MaxValue;if(invalid=="guard")mob.MonsterInfo.Guard=true;
            if(invalid=="boss")mob.MonsterInfo.Boss=true;if(invalid=="companion")mob.Companion=true;if(invalid=="removed")town.Entities.Remove(mob.UID);
            if(invalid=="other-map")mob.MapID=1015;if(invalid=="wrong-owner")mob.MonsterInfo.Owner=null;if(invalid=="anniversary")Pack(p,3001172,1);
            if(invalid=="no-bead")DisRemove(p,SoulBead(p));if(invalid=="unloaded")p.Entity.FullyLoaded=false;
            Call(SoulBall,null,"OnDeath",mob,p.Entity);Check(!Fenced(p) && SoulSpirits(p)==0,"invalid spirit source earns nothing: "+invalid);
        }
        var owner=SoulBallPlayer(town);SoulNpc(owner,7);SoulNpc(owner,9);var corpse=HuntMonster(owner,"Pheasant");
        Parallel.For(0,12,i=>Call(SoulBall,null,"OnDeath",corpse,owner.Entity));corpse.DeathStamp=corpse.DeathStamp.AddSeconds(5);Call(SoulBall,null,"OnDeath",corpse,owner.Entity);
        Check(!Fenced(owner) && SoulSpirits(owner)==1,"one corpse credits once below the cap under concurrent replay and changed timestamp");
        corpse.Hitpoints=100;corpse.ClearFlagsMobs();Call(SoulBall,null,"Revived",corpse);corpse.Hitpoints=0;Call(SoulBall,null,"OnDeath",corpse,owner.Entity);
        Check(SoulSpirits(owner)==2,"a genuine new life of the same monster may earn another spirit");
        var mob2=HuntMonster(owner,"Pheasant");
        Query("CREATE FUNCTION suppress_soul_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul_ball BEFORE UPDATE ON revora_soul_ball_runs FOR EACH ROW EXECUTE FUNCTION suppress_soul_ball()");
        try{Call(SoulBall,null,"OnDeath",mob2,owner.Entity);}finally{Query("DROP TRIGGER suppress_soul_ball ON revora_soul_ball_runs;DROP FUNCTION suppress_soul_ball()");}
        Check(Fenced(owner) && SoulSpirits(owner)==2,"failed spirit write preserves already committed progress and fences the hunter");
        owner=ReloadBall(owner);Call(SoulBall,null,"OnDeath",mob2,owner.Entity);Check(!Fenced(owner) && SoulSpirits(owner)==3,"failed uncredited death can finish once after production reload");
        var forged=new ConquerItem(true){UID=SoulBead(owner).UID,ID=729703};ItemHandler.UseItem(forged,owner);
        Check(SoulDone(owner)==0 && SoulBead(owner).UID==forged.UID,"a forged item object cannot redeem the owned bead");
    }
    static void VerifySoulWorlds(string runtime,Map town) {
        ushort[] ids={1207,1208,1212,1214,1215,1217,1762,1927,1999,2054,2055,2056};
        string[] names={"CateranSoldier","ChiefCateran","WaterDevil","HugeApe","HugeSnake","AlienSerpent","IcySerpent","IcyBladeDevil","DarkLady","GrottoLady","SnakeHubbub","FrostBladeDevil"};
        for(int n=0;n<ids.Length;n++) {
            var map=ids[n]==1217?TravelAlias(runtime,1217,1075):World(runtime,ids[n]);
            var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,9);p.BindWorldMap(ids[n]);EntityTable.SaveEntity(p);
            var mob=HuntMonster(p,names[n]);long gain=mob.MonsterInfo.Level<70?1:mob.MonsterInfo.Level<100?2:mob.MonsterInfo.Level<120?3:4;
            Parallel.For(0,8,i=>Call(SoulBall,null,"OnDeath",mob,p.Entity));
            Check(!Fenced(p) && SoulSpirits(p)==gain,"public hunting world "+map.ID+" credits its catalog monster once");
            p=ReloadBall(p);Call(SoulBall,null,"OnDeath",mob,p.Entity);
            Check(!Fenced(p) && SoulSpirits(p)==gain,"world "+map.ID+" spirit progress survives reload without corpse replay");
        }
        foreach(string invalid in new[]{"copy","wrong-floor","missing-info","owned-info","replaced-map","event","potency"}) {
            var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,9);
            Map map=invalid=="copy"?TravelAlias(runtime,62009,1927):invalid=="event"?World(runtime,1039):invalid=="potency"?World(runtime,1075):Kernel.Maps[1927];
            p.BindWorldMap(map.ID);var mob=HuntMonster(p,"IcyBladeDevil");var info=MapsTable.MapInformations[map.ID];ushort original=map.BaseID;
            try {
                if(invalid=="wrong-floor")map.BaseID=1762;
                if(invalid=="missing-info")MapsTable.MapInformations.Remove(map.ID);
                if(invalid=="owned-info"){var owned=new MapsTable.MapInformation{ID=map.ID,BaseID=map.BaseID,Owner=42};MapsTable.MapInformations[map.ID]=owned;}
                if(invalid=="replaced-map")Kernel.Maps.Remove(map.ID);
                Call(SoulBall,null,"OnDeath",mob,p.Entity);
                Check(!Fenced(p) && SoulSpirits(p)==0,"public hunting gate rejects "+invalid+" without inventing spirits");
            }finally{map.BaseID=original;MapsTable.MapInformations[map.ID]=info;Kernel.Maps[map.ID]=map;}
        }
        var waiting=SoulBallPlayer(town);SoulNpc(waiting,7);SoulNpc(waiting,9);var stale=HuntMonster(waiting,"Pheasant");
        var worker=new System.Threading.Thread(()=>Call(SoulBall,null,"OnDeath",stale,waiting.Entity));bool blocked;
        lock(waiting.ItemSyncRoot){worker.Start();blocked=SpinWait.SpinUntil(()=> (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0,3000);town.Entities.Remove(stale.UID);}
        Check(worker.Join(3000) && blocked && !Fenced(waiting) && SoulSpirits(waiting)==0,"death eligibility is rechecked after waiting for the owner's transaction lock");
        // Ordinary Envoy admission, installed portals, actual death dispatch and reward.
        var desert=World(runtime,1000);MiningNpc(desert,27837);
        var hunter=SoulBallPlayer(town);SoulNpc(hunter,7);SoulNpc(hunter,9);hunter.BindWorldMap(1000);
        var envoy=desert.Npcs[27837];hunter.Entity.X=envoy.X;hunter.Entity.Y=envoy.Y;ExplorationNpc(hunter,27837,1);
        Check(!Fenced(hunter) && hunter.Entity.MapID==1762,"ordinary bead owner reaches Grotto 1 through its actual Envoy");
        var first=Kernel.Maps[1762];typeof(Map).GetMethod("LoadPortals",Instance).Invoke(first,null);
        var portal=first.Portals.First(v=>v.DestinationMapID==1927);hunter.Entity.X=portal.CurrentX;hunter.Entity.Y=portal.CurrentY;
        GeneralData.UsePortal(new Data(true){UID=hunter.Entity.UID},hunter);
        Check(!Fenced(hunter) && hunter.Entity.MapID==1927,"installed Grotto portal retains the eligible public hunting world");
        bool money=Config.MoneyDropEnabled,cp=Config.KillConquerPointsEnabled;Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try {
            for(int n=0;n<125;n++) {var mob=HuntMonster(hunter,"IcyBladeDevil");mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=false;mob.Die(hunter.Entity);
                if(n==61){hunter=ReloadBall(hunter);Check(!Fenced(hunter) && SoulSpirits(hunter)==248,"Grotto hunter reconnects with earned half-bead progress");}}
            Check(!Fenced(hunter) && SoulSpirits(hunter)==500,"125 real Grotto deaths fill the issued Super bead");
            ItemHandler.UseItem(SoulBead(hunter),hunter);hunter=ReloadBall(hunter);
            Check(!Fenced(hunter) && SoulDone(hunter)==1 && hunter.Entity.ConquerPoints==21700 && hunter.Entity.SubClasses.StudyPoints==80
                && hunter.Entity.HeavenBlessing==172800,"Grotto-earned bead grants and reloads its full configured reward");
            VerifyStudyAward(hunter,"soul-ball");
        }finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cp;}
    }
    static void VerifySoulHunt(string runtime,Map town) {
        Call(MonsterLoot,null,"Initialize");MonsterLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));World(runtime,1015);
        var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,9);uint beadUid=SoulBead(p).UID;
        // The existing owned hunt list supplies the ordinary journey to Bird Island.
        PriceQuest.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>soulBallNow));PriceQuest.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        PriceNpc(p,2);var list=p.Inventory.Objects.Single(i=>i.ID==729087);ItemHandler.UseItem(list,p);PriceTrip(p);
        Check(p.Entity.MapID==1015 && !Fenced(p),"Soul Ball hunter travels through the ordinary owned hunt list");
        bool money=Config.MoneyDropEnabled,cp=Config.KillConquerPointsEnabled;Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try {
            Entity corpse=null;for(int n=0;n<250;n++) {
                var mob=HuntMonster(p,"BanditL97");mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=false;mob.Die(p.Entity);corpse=mob;
                if(n==124){p=ReloadBall(p);Check(!Fenced(p) && SoulSpirits(p)==250 && SoulBead(p).UID==beadUid,"half-filled bead survives ordinary reload with earned spirits");}
            }
            Check(!Fenced(p) && SoulSpirits(p)==500,"250 actual level-97 monster deaths fill the default Super bead");
            Call(SoulBall,null,"OnDeath",corpse,p.Entity);corpse.DeathStamp=corpse.DeathStamp.AddSeconds(5);Call(SoulBall,null,"OnDeath",corpse,p.Entity);
            Check(SoulSpirits(p)==500,"corpse replay and changed death timestamp cannot exceed the spirit requirement");
            p.Entity.spiritBeadsCount=15000;DialyQuestTable.Save(p);ulong xp=p.Entity.Experience,award=p.ExpBall;uint bless=p.Entity.HeavenBlessing;
            ItemHandler.UseItem(SoulBead(p),p);Check(!Fenced(p) && SoulDone(p)==1 && p.Entity.ConquerPoints==21700 && p.Entity.HeavenBlessing==bless+172800
                && p.Entity.Experience==xp+award && p.Entity.SubClasses.StudyPoints==80,"earned hunt commits Super reward independently of the Anniversary counter");
            p=ReloadBall(p);Call(SoulBall,null,"Refresh",p);Check(!Fenced(p) && SoulDone(p)==1 && p.Entity.SpiritBeads && p.Entity.HeavenBlessing==bless+172800,"full issued-hunted-rewarded route survives production reload");
        }finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cp;}
        var pending=SoulBallPlayer(town);SoulNpc(pending,7);SoulNpc(pending,9);soulBallNow=soulBallNow.AddDays(1);SoulNpc(pending,7);SoulNpc(pending,31);
        Check(!Fenced(pending) && SoulRuns(pending)==1 && SoulDone(pending)==1 && pending.Entity.ConquerPoints==16700,"an unfinished bead carries over and counts against its completion day");
        SoulNpc(pending,7);SoulNpc(pending,31);Check(SoulDone(pending)==1,"carry-over completion cannot earn another reward the same day");soulBallNow=soulBallNow.AddDays(-1);
    }
}
