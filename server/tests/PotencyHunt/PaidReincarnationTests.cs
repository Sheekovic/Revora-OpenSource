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
    static readonly Type PaidRite=typeof(Entity).Assembly.GetType("Revora.Game.Features.PaidReincarnation");
    static DateTime paidNow=new DateTime(2032,3,3,0,0,0,DateTimeKind.Utc);
    static GameState PaidPlayer(Map town,byte current=11,byte level=110,ulong exp=12345) {
        var p=SecondPlayer(town,55,15,120);SecondOffer(p,current);StoneNpc(p,180);
        if(Fenced(p) || p.Entity.Reborn!=2)throw new Exception("Paid reset prerequisite failed.");
        p.Entity.Atributes=checked((ushort)(p.Entity.Atributes+(level-15)*Config.AttributePointsPerLevel));
        p.Entity.Level=level;p.Entity.Experience=exp;p.Entity.ConquerPoints=50000;
        if(!EntityTable.SaveEntity(p))throw new Exception("Paid reset fixture save failed.");
        Pack(p,711083,1);return p;
    }
    static long PaidCount(GameState p){return Scalar("SELECT count(*) FROM revora_reincarnations WHERE uid=@uid",p.Entity.UID);}
    static long PaidPending(GameState p){return Scalar("SELECT count(*) FROM revora_reincarnations WHERE uid=@uid AND recovered_at IS NULL",p.Entity.UID);}
    static long PaidPointTotal(GameState p){return (long)p.Entity.Strength+p.Entity.Agility+p.Entity.Vitality+p.Entity.Spirit+p.Entity.Atributes;}
    static void PaidOffer(GameState p){StoneNpc(p,3);StoneNpc(p,5);}
    static void PaidRequest(GameState p,byte next=21,byte body=3){var packet=ReincarnationRequest();packet[8]=next;packet[12]=body;Revora.Network.GamePackets.Reincarnation.Process(packet,p);}
    static void PaidAtRecovery(GameState p,byte level=110,ulong exp=7) {
        p.Entity.Atributes=checked((ushort)(p.Entity.Atributes+(level-p.Entity.Level)*Config.AttributePointsPerLevel));
        p.Entity.Level=level;p.Entity.Experience=exp;
        if(!EntityTable.SaveEntity(p))throw new Exception("Paid recovery fixture save failed.");
    }
    static void VerifyPaidReincarnation(string runtime,Map town) {
        VerifySecondRebirth(runtime,town);
        Query("CREATE TABLE IF NOT EXISTS reincarnation(id bigserial PRIMARY KEY,uid bigint NOT NULL,level bigint NOT NULL,experience bigint NOT NULL)");
        PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>paidNow));
        foreach(byte old in rebirthClasses)foreach(byte next in rebirthClasses) {
            var p=PaidPlayer(town,old);long before=PaidPointTotal(p);uint cp=p.Entity.ConquerPoints;RebirthProf(p);RebirthSkill(p,1045);
            PaidOffer(p);PaidRequest(p,next,4);
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.Level==15 && p.Entity.Class==next
                && p.Entity.Reborn==2 && p.Entity.Experience==0 && p.Entity.Body==1004 && p.Entity.FirstRebornClass==15
                && p.Entity.FirstRebornLevel==120 && p.Entity.SecondRebornClass==old && p.Entity.SecondRebornLevel==110
                && PaidPointTotal(p)==before-95*Config.AttributePointsPerLevel && p.Entity.ConquerPoints==cp-Config.SerethReincarnationCp
                && !p.Inventory.Contains(711083,1) && PaidCount(p)==1 && PendingRows(p)==1,
                "ordinary reincarnation shifts class and level history with one exact payment and no bonus: "+old+" to "+next);
            Check(p.Spells[1045].Level==0 && p.Spells[1045].PreviousLevel==4 && p.Proficiencies[410].Level==0 && p.Proficiencies[410].PreviousLevel==12,
                "reincarnation keeps earned previous skill/proficiency levels: "+old+" to "+next);
        }
        var female=PaidPlayer(town);female.Entity.Body=2001;EntityTable.SaveEntity(female);PaidOffer(female);PaidRequest(female,132,2);
        Check(!Fenced(female) && female.Entity.Body==2002 && PaidCount(female)==1,"female reincarnation accepts the matching body selector");
        VerifyPaidFailures(town);VerifyPaidPurchase(town);VerifyPaidRecovery(town);VerifyPaidGear(town);VerifyPaidSkills(town);VerifyPaidBoundaries(town);VerifyAssassinRebirth(runtime,town);
    }
    static void VerifyPaidFailures(Map town) {
        foreach(string mode in new[]{"no-offer","cancel","expire","low","missing","locked","remote","trade","dead","fake","unloaded","replaced","bag","skill","wrong-npc","class","body","fee","balance"}) {
            var p=PaidPlayer(town);var dew=p.Inventory.Objects.Single(i=>i.ID==711083);uint cp=p.Entity.ConquerPoints;
            if(mode=="low")p.Entity.Level=109;if(mode=="missing")DisRemove(p,dew);if(mode=="locked")dew.Lock=1;
            if(mode!="no-offer")PaidOffer(p);
            if(mode=="cancel")StoneNpc(p,255);if(mode=="expire")paidNow=paidNow.AddMinutes(2);
            if(mode=="remote")p.Entity.X+=20;if(mode=="trade")p.Trade.InTrade=true;if(mode=="dead")p.Entity.Hitpoints=0;
            if(mode=="fake")p.Fake=true;if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="replaced")Kernel.GamePool.Remove(p.Entity.UID);
            if(mode=="bag")dew.StackSize=2;if(mode=="skill")RebirthSkill(p,1045);if(mode=="balance")p.Entity.ConquerPoints--;
            uint fee=Config.SerethReincarnationCp;
            if(mode=="fee")Config.SerethReincarnationCp++;
            try {
                if(mode=="wrong-npc")Npcs.GetDialog(new NpcRequest(true){NpcID=1,OptionID=5},p);
                PaidRequest(p,mode=="class"?(byte)255:(byte)21,mode=="body"?(byte)1:(byte)3);
            }finally{Config.SerethReincarnationCp=fee;}
            Check(PaidCount(p)==0 && PendingRows(p)==0 && p.Entity.Class==11 && p.Entity.Body==1003
                && p.Entity.ConquerPoints==(mode=="balance"?cp-1:cp) && (mode=="missing" || SavedRune(dew,"id")==711083),"rejected reincarnation preserves payment/profile: "+mode);
        }
        foreach(string target in new[]{"entities","items","skills","profs","reincarnation","revora_reincarnations"}) {
            var p=PaidPlayer(town);RebirthProf(p);PaidOffer(p);uint cp=p.Entity.ConquerPoints;var dew=p.Inventory.Objects.Single(i=>i.ID==711083);
            Query("CREATE FUNCTION suppress_paid() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_paid BEFORE INSERT OR UPDATE OR DELETE ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_paid()");
            try{PaidRequest(p);}finally{Query("DROP TRIGGER suppress_paid ON "+target+";DROP FUNCTION suppress_paid()");}
            Check(Fenced(p) && p.Entity.Level==110 && p.Entity.ConquerPoints==cp && PaidCount(p)==0 && PendingRows(p)==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp && SavedRune(dew,"id")==711083
                && Scalar("SELECT class FROM entities WHERE uid=@uid",p.Entity.UID)==11,"failed reincarnation write rolls back payment, profile and recovery: "+target);
        }
        var stale=PaidPlayer(town);PaidOffer(stale);Query("UPDATE entities SET experience=experience+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);PaidRequest(stale);
        Check(Fenced(stale) && PaidCount(stale)==0 && stale.Inventory.Contains(711083,1) && stale.Entity.ConquerPoints==50000,"external progression conflict cannot overwrite or charge");
        var race=PaidPlayer(town);PaidOffer(race);Parallel.For(0,20,_=>PaidRequest(race));
        Check(!Fenced(race) && PaidCount(race)==1 && race.Entity.ConquerPoints==50000-Config.SerethReincarnationCp,"concurrent native confirmations consume one offer and one payment");
        var pending=PaidPlayer(town,11,139);PaidOffer(pending);PaidRequest(pending);Pack(pending,711083,1);PaidOffer(pending);PaidRequest(pending);
        Check(PaidCount(pending)==1 && PaidPending(pending)==1 && pending.Inventory.Contains(711083,1),"pending former life cannot be replaced by another paid reset");
        var second=SecondPlayer(town);SecondOffer(second);StoneNpc(second,180);PaidAtRecovery(second);second.Entity.ConquerPoints=50000;Pack(second,711083,1);EntityTable.SaveEntity(second);PaidOffer(second);PaidRequest(second);
        Check(PaidCount(second)==0 && SecondPending(second)==1 && second.Entity.ConquerPoints==50000,"pending level-130 second-life recovery is preserved before reincarnation");
    }
    static void VerifyPaidPurchase(Map town) {
        var p=PaidPlayer(town);var existing=p.Inventory.Objects.Single(i=>i.ID==711083);DisRemove(p,existing);
        StoneNpc(p,6);Parallel.For(0,20,_=>StoneNpc(p,7));
        Check(!Fenced(p) && p.Entity.ConquerPoints==48000 && p.Inventory.Objects.Count(i=>i.ID==711083)==1
            && Scalar("SELECT count(*) FROM revora_oblivion_purchases WHERE uid=@uid",p.Entity.UID)==1,"ordinary Sereth dew purchase pays once despite duplicate confirmations");
        var loaded=ReloadBall(p);SkillTable.LoadProficiencies(loaded);
        Check(loaded.Entity.ConquerPoints==48000 && loaded.Inventory.Contains(711083,1),"paid dew and immediate balance survive production reload");
        foreach(string target in new[]{"items","entities","revora_oblivion_purchases"}) {
            var q=PaidPlayer(town);StoneNpc(q,6);
            Query("CREATE FUNCTION suppress_dew() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_dew BEFORE INSERT OR UPDATE ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_dew()");
            try{StoneNpc(q,7);}finally{Query("DROP TRIGGER suppress_dew ON "+target+";DROP FUNCTION suppress_dew()");}
            Check(Fenced(q) && q.Entity.ConquerPoints==50000 && q.Inventory.Objects.Count(i=>i.ID==711083)==1
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",q.Entity.UID)==50000
                && Scalar("SELECT count(*) FROM revora_oblivion_purchases WHERE uid=@uid",q.Entity.UID)==0,"dew purchase failure rolls back item, CP and receipt: "+target);
        }
    }
    static void VerifyPaidRecovery(Map town) {
        foreach(byte oldLevel in new byte[]{110,139}) {
            ulong saved=oldLevel==110?1000UL:5000000000UL;
            var p=PaidPlayer(town,11,oldLevel,saved);long original=PaidPointTotal(p);PaidOffer(p);PaidRequest(p);
            var loaded=ReloadBall(p);SkillTable.LoadProficiencies(loaded);
            loaded.LoadItemStats();loaded.CalculateStatBonus();loaded.CalculateHPBonus();loaded.GemAlgorithm();
            Check(loaded.Entity.MaxHitpoints>0,"reconnected recovery fixture loads ordinary combat stats");PaidAtRecovery(loaded);
            loaded.IncreaseExperience(3,false);
            Check(!Fenced(loaded) && loaded.Entity.Level==oldLevel && loaded.Entity.Experience==saved+10 && PaidPointTotal(loaded)==original
                && PaidPending(loaded)==0 && PendingRows(loaded)==0 && loaded.Entity.ConquerPoints==50000-Config.SerethReincarnationCp,
                "production reload restores exact paid life and EXP at level 110 without extra attributes: "+oldLevel);
            loaded.IncreaseExperience(1,false);Check(loaded.Entity.Experience==saved+11 && PaidPointTotal(loaded)==original,"paid restoration cannot repeat: "+oldLevel);
            // The recovered receipt must remain authoritative after another login.
            if(!EntityTable.SaveEntity(loaded))throw new Exception("Ordinary post-recovery experience save failed.");
            var again=ReloadBall(loaded);SkillTable.LoadProficiencies(again);again.IncreaseExperience(1,false);
            Check(again.Entity.Experience==saved+12 && PaidPending(again)==0,"consumed paid receipt survives reconnect: "+oldLevel);
        }
        foreach(byte originalLevel in new byte[]{110,139}) {
            var repeat=PaidPlayer(town,11,originalLevel);PaidOffer(repeat);PaidRequest(repeat);PaidAtRecovery(repeat);repeat.IncreaseExperience(0,false);
            Pack(repeat,711083,1);PaidOffer(repeat);PaidRequest(repeat,41);
            Check(!Fenced(repeat) && PaidCount(repeat)==2 && PaidPending(repeat)==1 && repeat.Entity.FirstRebornClass==11 && repeat.Entity.SecondRebornClass==21
                && repeat.Entity.Class==41 && repeat.Entity.ConquerPoints==50000-2*Config.SerethReincarnationCp,"recovered ordinary player can reincarnate again with shifted history and new receipt: "+originalLevel);
        }
        var crossing=PaidPlayer(town,11,139,5000000000UL);long points=PaidPointTotal(crossing);PaidOffer(crossing);PaidRequest(crossing);
        PaidAtRecovery(crossing,109,DataHolder.LevelExperience(109)-1);crossing.IncreaseExperience(1,false);
        Check(!Fenced(crossing) && crossing.Entity.Level==139 && crossing.Entity.Experience==5000000000UL && PaidPointTotal(crossing)==points
            && PaidPending(crossing)==0 && PendingRows(crossing)==0,"earning level 110 atomically restores the paid snapshot and all original points once");
        var fault=PaidPlayer(town,11,139);PaidOffer(fault);PaidRequest(fault);PaidAtRecovery(fault);
        Query("CREATE FUNCTION reject_paid_recovery() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$;");
        Query("CREATE TRIGGER reject_paid_recovery BEFORE DELETE ON reincarnation FOR EACH ROW EXECUTE FUNCTION reject_paid_recovery()");
        bool rejected=false;try{fault.IncreaseExperience(1,false);}catch(Npgsql.PostgresException){rejected=true;}
        finally{Query("DROP TRIGGER reject_paid_recovery ON reincarnation;DROP FUNCTION reject_paid_recovery()");}
        Check(rejected && Fenced(fault) && PaidPending(fault)==1 && PendingRows(fault)==1 && fault.Entity.Level==110
            && Scalar("SELECT level FROM entities WHERE uid=@uid",fault.Entity.UID)==110,"recovery failure keeps typed receipt and legacy snapshot with unchanged progression");
    }
    static void VerifyPaidGear(Map town) {
        var p=PaidPlayer(town);var dew=p.Inventory.Objects.Single(i=>i.ID==711083);dew.StackSize=2;
        Query("UPDATE items SET stacksize=2,bound=1 WHERE uid=@uid","uid",(long)dew.UID);dew.Bound=true;
        var main=RebirthGear(p,4);var alternate=RebirthGear(p,24);var hand=RebirthGear(p,5);var second=RebirthGear(p,25);
        PaidOffer(p);PaidRequest(p);
        Check(!Fenced(p) && dew.StackSize==1 && dew.Bound && p.Inventory.ContainsUID(hand.UID) && p.Inventory.ContainsUID(second.UID)
            && main.ID!=410339 && alternate.ID==main.ID && SavedRune(dew,"stacksize")==1,"bound stacked dew and both equipment sets commit together");
        var loaded=ReloadBall(p);SkillTable.LoadProficiencies(loaded);
        Check(loaded.Equipment.TryGetItem(4).ID==main.ID && loaded.Equipment.TryGetItem(24).ID==alternate.ID && loaded.Inventory.ContainsUID(hand.UID)
            && loaded.Inventory.ContainsUID(second.UID),"both lowered gear sets and displaced offhands survive production reload");
    }
    static void VerifyPaidSkills(Map town) {
        var same=PaidPlayer(town);byte learned=SpellTable.SpellInformations[3050].Keys.Max();same.Spells[3050].Level=learned;same.Spells[3050].PreviousLevel=learned;
        Query("UPDATE skills SET level=@level,previouslevel=@level WHERE entityid=@uid AND id=3050","uid",(long)same.Entity.UID,"level",(int)learned);
        PaidOffer(same);PaidRequest(same,11);
        Check(!Fenced(same) && same.Spells[3050].Level==0 && same.Spells[3050].PreviousLevel==learned
            && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=3050",same.Entity.UID)==1,
            "granting an already learned class bonus reuses its owned row and preserves previous level");
        var p=PaidPlayer(town,132);foreach(ushort id in new ushort[]{4010,4020,4050,4060,4070})RebirthSkill(p,id);
        PaidOffer(p);PaidRequest(p,21);
        Check(!Fenced(p) && p.Spells.ContainsKey(4010) && new ushort[]{4020,4050,4060,4070}.All(id=>!p.Spells.ContainsKey(id)),"only immediately previous water life's summon survives the shifted history");
    }
    static void VerifyPaidBoundaries(Map town) {
        var wire=PaidPlayer(town);SoulWire(wire,3,59558);SoulWire(wire,5,59558);
        var frame=ReincarnationRequest();frame[8]=41;
        for(int n=0;n<frame.Length;n++)Revora.Network.GamePackets.Reincarnation.Process(frame.Take(n).ToArray(),wire);
        var malformed=(byte[])frame.Clone();malformed[20]^=1;Revora.Network.GamePackets.Reincarnation.Process(malformed,wire);
        Check(PaidCount(wire)==0 && wire.Entity.ConquerPoints==50000,"truncated and unsealed native reset requests preserve the offered payment");
        Revora.Network.PacketHandler.HandlePacket(frame,wire);
        Check(!Fenced(wire) && PaidCount(wire)==1 && wire.Entity.Class==41,"actual bounded NPC continuation and 1066 dispatch complete the ordinary paid reset");
        var full=PaidPlayer(town);RebirthGear(full,5);RebirthGear(full,25);while(full.Inventory.Count<40)Pack(full,1088001,1);
        PaidOffer(full);PaidRequest(full);
        Check(!Fenced(full) && PaidCount(full)==0 && full.Inventory.Contains(711083,1) && full.Entity.ConquerPoints==50000,"full bag cannot consume dew when two offhands need more space");
        var exact=PaidPlayer(town);RebirthGear(exact,5);while(exact.Inventory.Count<40)Pack(exact,1088001,1);PaidOffer(exact);PaidRequest(exact);
        Check(!Fenced(exact) && PaidCount(exact)==1 && exact.Inventory.Count==40,"single dew frees exactly one offhand slot in a full bag");
        var stale=PaidPlayer(town);PaidOffer(stale);Query("UPDATE items SET bound=1 WHERE entityid=@uid AND id=711083","uid",(long)stale.Entity.UID);PaidRequest(stale);
        Check(Fenced(stale) && PaidCount(stale)==0 && stale.Entity.ConquerPoints==50000,"durable dew preimage change rolls back without payment");
        var expiry=PaidPlayer(town);PaidOffer(expiry);int calls=0;
        PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>++calls==1?paidNow:paidNow.AddMinutes(2)));
        try{PaidRequest(expiry);}finally{PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>paidNow));}
        Check(Fenced(expiry) && PaidCount(expiry)==0 && PendingRows(expiry)==0 && expiry.Entity.ConquerPoints==50000
            && Scalar("SELECT level FROM entities WHERE uid=@uid",expiry.Entity.UID)==110,"expiry after transactional writes rolls back the entire reset");
        var projection=PaidPlayer(town);PaidOffer(projection);calls=0;var arena=projection.ArenaStatistic;
        PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{if(++calls==2)projection.ArenaStatistic=null;return paidNow;}));
        try{PaidRequest(projection);}finally{projection.ArenaStatistic=arena;PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>paidNow));}
        Check(Fenced(projection) && PaidCount(projection)==1 && PaidPending(projection)==1 && PendingRows(projection)==1
            && Scalar("SELECT level FROM entities WHERE uid=@uid",projection.Entity.UID)==15
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",projection.Entity.UID)==50000-Config.SerethReincarnationCp,
            "postcommit projection failure fences cache while keeping exactly one complete durable reset");
        var reload=ReloadBall(projection);SkillTable.LoadProficiencies(reload);
        Check(reload.Entity.Level==15 && reload.Entity.Class==21 && !reload.Inventory.Contains(711083,1) && !Fenced(reload),"production reload repairs projection without repeating payment");
    }
}
