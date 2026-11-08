using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Revora.Network;

internal static partial class PotencyHuntTests
{
    static readonly Type SecondRite = typeof(Entity).Assembly.GetType("Revora.Game.Features.SecondRebirth");
    static readonly Type SecondSkills = typeof(Entity).Assembly.GetType("Revora.Game.Features.SecondRebirthSkills");
    static DateTime secondNow = new DateTime(2032, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    static GameState SecondPlayer(Map town, byte first = 15, byte second = 15, byte level = 130, ulong exp = 5000) {
        var p = RebirthPlayer(town, first); RebirthOffer(p, rebirthClasses.Single(c => c / 10 == second / 10)); StoneNpc(p, 214);
        if (Fenced(p) || p.Entity.Reborn != 1) throw new Exception("Second-life prerequisite failed.");
        p.Entity.Class = second; p.Entity.Level = level; p.Entity.Experience = exp;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Second-life fixture save failed.");
        Pack(p, 723701, 1); return p;
    }
    static void SecondOffer(GameState p, byte next = 11) { StoneNpc(p, 2); StoneNpc(p, (byte)(170 + Array.IndexOf(rebirthClasses, next))); }
    static long SecondCount(GameState p) { return Scalar("SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid", p.Entity.UID); }
    static long SecondPending(GameState p) { return Scalar("SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid AND recovered_at IS NULL", p.Entity.UID); }
    static void VerifySecondRebirth(string runtime, Map town) {
        VerifyFirstRebirth(runtime, town); VerifySecondRebirthRules();
        SecondRite.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => secondNow));
        Call(SecondRite, null, "Initialize");
        foreach (byte source in new byte[] {15,25,45,55,65,75,85,135,145,165}) foreach (byte next in rebirthClasses) {
            var p = SecondPlayer(town, source, source); RebirthProf(p); RebirthSkill(p, 1045);
            SecondOffer(p, next); StoneNpc(p, 180);
            if(source==15 && next==11)Check(RiteReceiptUtc(p,true)==secondNow,"second-rite receipt uses its authoritative game UTC clock, independent of database wall time");
            Check(!Fenced(p) && p.Account.State == AccountTable.AccountState.Player && p.Entity.Reborn == 2 && p.Entity.Class == next
                && p.Entity.Level == 15 && p.Entity.Experience == 0 && p.Entity.FirstRebornClass == source && p.Entity.SecondRebornClass == source
                && p.Entity.FirstRebornLevel == 130 && p.Entity.SecondRebornLevel == 130
                && p.Entity.Strength + p.Entity.Agility + p.Entity.Vitality + p.Entity.Spirit + p.Entity.Atributes == 192
                && SecondCount(p) == 1 && !p.Inventory.Contains(723701, 1), "ordinary second rite preserves history and complete budget: " + source + " to " + next);
            Check(p.Spells[1045].Level == 0 && p.Spells[1045].PreviousLevel == 4 && p.Proficiencies[410].PreviousLevel == 12
                && p.Spells.ContainsKey(9876) && HasAchievement(p, 10316) && !HasAchievement(p, 11031),
                "second rite resets learned levels, grants Bless and only the earned achievement: " + source + " to " + next);
        }
        VerifySecondSkills(town); VerifySecondFailures(town); VerifySecondEquipment(town); VerifySecondRecovery(town);
    }
    static void VerifySecondSkills(Map town) {
        var p = SecondPlayer(town); ushort[] pure = {10315,10311,10313,6003,10405,11040,12300,30000,10310};
        byte[] schools = {1,2,4,5,6,7,8,13,14};
        foreach(ushort art in pure)p.Spells[art]=new Spell(true){ID=art};
        foreach (byte first in rebirthClasses) foreach (byte second in rebirthClasses) foreach (byte third in rebirthClasses) {
            p.Entity.FirstRebornClass = (byte)(first / 10 * 10 + 5); p.Entity.Class = (byte)(second / 10 * 10 + 5);
            var retained = (ushort[])Call(SecondSkills, null, "Retained", (byte)(first/10), (byte)(second/10), (byte)(third/10));
            var plan = (Spell[])Call(SecondSkills, null, "Plan", p, third);
            Check(plan.Any(s => s.ID == 9876) && pure.Select((art,index) => new {art,index}).All(x =>
                plan.Any(s => s.ID == x.art) == (first / 10 == schools[x.index] && second / 10 == schools[x.index] && third / 10 == schools[x.index]))
                && retained.Distinct().Count() == retained.Length, "all 1000 histories enforce pure-school gates: " + first + "/" + second + "/" + third);
        }
        int[][] rows = { new[]{135,55,61,1095,6000}, new[]{135,55,71,1090,6000}, new[]{45,75,11,11070,11040},
            new[]{165,25,11,12830,12840}, new[]{25,165,132,3060,12840}, new[]{15,25,132,1025,1115}, new[]{75,75,71,11070,6003} };
        foreach (var row in rows) {
            var actor = SecondPlayer(town,(byte)row[0],(byte)row[1]);
            if (!actor.Spells.ContainsKey((ushort)row[3])) RebirthSkill(actor,(ushort)row[3]);
            if (!actor.Spells.ContainsKey((ushort)row[4])) RebirthSkill(actor,(ushort)row[4]);
            SecondOffer(actor,(byte)row[2]); StoneNpc(actor,180);
            Check(!Fenced(actor) && actor.Spells.ContainsKey((ushort)row[3]) && !actor.Spells.ContainsKey((ushort)row[4]),
                "three-life retention applies learned arts without retaining unrelated/pure skills: " + row[0]+"/"+row[1]+"/"+row[2]);
            var loaded = ReloadBall(actor); SkillTable.LoadProficiencies(loaded);
            Check(loaded.Entity.Reborn == 2 && loaded.Spells.ContainsKey((ushort)row[3]) && !loaded.Spells.ContainsKey((ushort)row[4]), "second-life skill transition survives production reload");
        }
        var archer = SecondPlayer(town,45,75); SecondOffer(archer,11); StoneNpc(archer,180);
        Check(!Fenced(archer) && archer.Spells.ContainsKey(5002), "earned first-Archer arrow art survives the explicitly inferred Archer/Pirate/Trojan route");
        var unlearned = SecondPlayer(town,135,55); SecondOffer(unlearned,61); StoneNpc(unlearned,180);
        Check(!unlearned.Spells.ContainsKey(1095) && !unlearned.Spells.ContainsKey(6001), "retention matrix does not invent unlearned inherited skills");
    }
    static void VerifySecondFailures(Map town) {
        foreach (string mode in new[]{"no-offer","order","cancel","expire","low-water","rank","missing","locked","remote","trade","dead","fake","unloaded","replaced","changed-bag","changed-skill","wrong-npc"}) {
            var p = SecondPlayer(town,135,135); var token = p.Inventory.Objects.Single(i=>i.ID==723701);
            if(mode=="low-water")p.Entity.Level=119;if(mode=="rank")p.Entity.Class=134;
            if(mode=="missing")DisRemove(p,token);if(mode=="locked")token.Lock=1;
            if(mode!="no-offer")SecondOffer(p);
            if(mode=="order")StoneNpc(p,171);if(mode=="cancel")StoneNpc(p,255);if(mode=="expire")secondNow=secondNow.AddMinutes(2);
            if(mode=="remote")p.Entity.X+=20;if(mode=="trade")p.Trade.InTrade=true;if(mode=="dead")p.Entity.Hitpoints=0;
            if(mode=="fake")p.Fake=true;if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="replaced")Kernel.GamePool.Remove(p.Entity.UID);
            if(mode=="changed-bag")token.StackSize=2;if(mode=="changed-skill")RebirthSkill(p,1045);
            if(mode=="wrong-npc")Npcs.GetDialog(new NpcRequest(true){NpcID=1,OptionID=180},p);else StoneNpc(p,180);
            Check(SecondCount(p)==0 && p.Entity.Reborn==1 && (mode=="missing" || SavedRune(token,"id")==723701),"rejected second rite preserves token: "+mode);
        }
        foreach(string target in new[]{"entities","items","skills","profs","revora_second_rebirths","revora_achievement_evidence"}) {
            var p=SecondPlayer(town);RebirthProf(p);SecondOffer(p);var token=p.Inventory.Objects.Single(i=>i.ID==723701);
            Query("CREATE FUNCTION suppress_second() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_second BEFORE INSERT OR UPDATE OR DELETE ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_second()");
            try {StoneNpc(p,180);}finally{Query("DROP TRIGGER suppress_second ON "+target+";DROP FUNCTION suppress_second()");}
            Check(Fenced(p) && p.Entity.Reborn==1 && SecondCount(p)==0 && SavedRune(token,"id")==723701
                && Scalar("SELECT reborn FROM entities WHERE uid=@uid",p.Entity.UID)==1 && p.Proficiencies[410].Level==12,
                "suppressed durable write rolls back token/profile/skills/receipt: "+target);
        }
        foreach(string part in new[]{"profile","item","skill","prof","map"}) {
            var p=SecondPlayer(town);RebirthSkill(p,1045);RebirthProf(p);SecondOffer(p);
            Query(part=="profile"?"UPDATE entities SET experience=experience+1 WHERE uid=@uid":part=="map"?"UPDATE entities SET mapid=1004 WHERE uid=@uid":
                part=="item"?"UPDATE items SET bound=1 WHERE entityid=@uid":part=="skill"?"UPDATE skills SET level=5 WHERE entityid=@uid":"UPDATE profs SET level=13 WHERE entityid=@uid","uid",(long)p.Entity.UID);
            StoneNpc(p,180);Check(Fenced(p) && SecondCount(p)==0 && p.Inventory.Contains(723701,1),"stale second-life preimage is rejected: "+part);
        }
        var late=SecondPlayer(town);SecondOffer(late);int ticks=0;
        SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{if(++ticks==2)late.Trade.InTrade=true;return secondNow;}));
        try{StoneNpc(late,180);}finally{SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>secondNow));}
        Check(Fenced(late) && SecondCount(late)==0 && late.Inventory.Contains(723701,1),"last-moment loss of eligibility rolls back the entire second rite");
        var race=SecondPlayer(town);SecondOffer(race);Parallel.For(0,12,i=>StoneNpc(race,180));
        Check(!Fenced(race) && SecondCount(race)==1 && race.Entity.Reborn==2,"concurrent final clicks complete one second rite");
        var reconnect=SecondPlayer(town);SecondOffer(reconnect);var fresh=ReloadBall(reconnect);fresh.ActiveNpc=59558;StoneNpc(fresh,180);
        Check(SecondCount(fresh)==0 && fresh.Inventory.Contains(723701,1),"new session cannot reuse old second-life offer");
        var wire=SecondPlayer(town);SecondOffer(wire);var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);Writer.WriteUInt32(59558,8,frame);frame[14]=180;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);
        foreach(int length in new[]{0,3})PacketHandler.HandlePacket(frame.Take(length).ToArray(),wire);
        SoulWire(wire,180,59558,19);SoulWire(wire,180,59558,121);
        var wrong=(byte[])frame.Clone();Buffer.BlockCopy(System.BitConverter.GetBytes(1u),0,wrong,8,4);PacketHandler.HandlePacket(wrong,wire);
        Check(SecondCount(wire)==0,"truncated and wrong-NPC second-life packets cannot consume a token");
        SecondOffer(wire);PacketHandler.HandlePacket(frame,wire);
        Check(!Fenced(wire) && SecondCount(wire)==1,"bounded existing NPC continuation completes second rebirth");
    }
    static void VerifySecondSoul(GameState p){
        var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000).ToArray();
        Check(souls.Length==1 && souls[0].Bound && ConquerItemInformation.BaseInformations[souls[0].ID].PurificationLevel==7
            && Scalar("SELECT count(*) FROM revora_second_rebirths r JOIN items i ON i.uid=r.soul_uid WHERE r.uid=@uid AND i.entityid=@uid AND i.bound=1 AND i.id BETWEEN 800000 AND 899999",p.Entity.UID)==1,
            "second rite persists one bound catalog P7 soul with its completion receipt");
    }
    static void VerifySecondEquipment(Map town) {
        var fault=SecondPlayer(town);SecondOffer(fault);
        Query("CREATE FUNCTION second_soul_failure() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id BETWEEN 800000 AND 899999 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER second_soul_failure BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION second_soul_failure()");
        try{StoneNpc(fault,180);}finally{Query("DROP TRIGGER second_soul_failure ON items;DROP FUNCTION second_soul_failure()");}
        Check(Fenced(fault) && SecondCount(fault)==0 && fault.Entity.Reborn==1 && fault.Inventory.Contains(723701,1)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",fault.Entity.UID)==0,"failed P7 insertion rolls back second rebirth and token consumption");
        fault=ReloadBall(fault);fault.ActiveNpc=59558;SecondOffer(fault);StoneNpc(fault,180);VerifySecondSoul(fault);

        var p=SecondPlayer(town);var right=RebirthGear(p,4);var left=RebirthGear(p,5);var alt=RebirthGear(p,25);
        right.Plus=6;Query("UPDATE items SET plus=6 WHERE uid=@uid","uid",(long)right.UID);
        var token=p.Inventory.Objects.Single(i=>i.ID==723701);token.StackSize=2;token.Bound=true;
        Query("UPDATE items SET bound=1,stacksize=2 WHERE uid=@uid","uid",(long)token.UID);
        SecondOffer(p);StoneNpc(p,180);
        Check(!Fenced(p) && right.Plus==6 && right.ID%10==9 && ConquerItemInformation.BaseInformations[right.ID].Level<=20
            && p.Inventory.ContainsUID(left.UID) && p.Inventory.ContainsUID(alt.UID) && token.StackSize==1 && SavedRune(token,"stacksize")==1
            && p.Inventory.Objects.Any(i=>i.ID==410077 && i.Bound),"second rite lowers both sets, releases offhands and binds gift from stacked bound token");
        VerifySecondSoul(p);var loaded=ReloadBall(p);VerifySecondSoul(loaded);SkillTable.LoadProficiencies(loaded);
        Check(loaded.Equipment.TryGetItem(4).Plus==6 && loaded.Inventory.ContainsUID(left.UID) && loaded.Inventory.ContainsUID(alt.UID),"production reload retains lowered gear and off-hands");
        var full=SecondPlayer(town);RebirthGear(full,5);while(full.Inventory.Count<40)Pack(full,1088001,1);
        SecondOffer(full);StoneNpc(full,180);Check(!Fenced(full) && SecondCount(full)==0 && full.Inventory.Contains(723701,1),"full bag rejects before token debit when off-hand and gift need two slots");
        var exact=SecondPlayer(town);while(exact.Inventory.Count<39)Pack(exact,1088001,1);SecondOffer(exact);StoneNpc(exact,180);
        Check(!Fenced(exact) && exact.Entity.Reborn==2 && exact.Inventory.Count==40,"single token plus one free slot fits the weapon and P7 gifts");
        var zero=SecondPlayer(town);var single=zero.Inventory.Objects.Single(i=>i.ID==723701);single.StackSize=0;
        Query("UPDATE items SET stacksize=0 WHERE uid=@uid","uid",(long)single.UID);SecondOffer(zero,61);StoneNpc(zero,180);
        Check(!Fenced(zero) && zero.Entity.Reborn==2 && !zero.Inventory.ContainsUID(single.UID),"legacy zero-stack token is consumed exactly once");
        VerifySecondSoul(exact);
        var pirate=SecondPlayer(town,75,75);int before=pirate.Inventory.Count;SecondOffer(pirate,71);StoneNpc(pirate,180);
        Check(!Fenced(pirate) && pirate.Inventory.Count==before && pirate.Spells.ContainsKey(11040),"pure Pirate earns Scurvy Bomb and P7 without an obsolete second-rite blade");VerifySecondSoul(pirate);
    }
    static GameState PendingSecond(Map town, byte level=139, ulong exp=5000000000UL) {
        var p=SecondPlayer(town,15,15,level,exp);SecondOffer(p);StoneNpc(p,180);
        if(Fenced(p) || p.Entity.Reborn!=2)throw new Exception("Second recovery prerequisite failed.");
        p.Entity.Level=129;p.Entity.Experience=DataHolder.LevelExperience(129)-1;
        p.Entity.Atributes=checked((ushort)(p.Entity.Atributes+114*Config.AttributePointsPerLevel));EntityTable.SaveEntity(p);return p;
    }
    static void VerifySecondRecovery(Map town) {
        var p=PendingSecond(town);ushort before=p.Entity.Atributes;p.IncreaseExperience(0,false);
        Check(p.Entity.Level==129 && SecondPending(p)==1,"second-life receipt waits for level 130");
        p.IncreaseExperience(1,false);
        Check(!Fenced(p) && p.Entity.Level==139 && p.Entity.Experience==5000000000UL && p.Entity.Atributes==before+10*Config.AttributePointsPerLevel
            && SecondPending(p)==0,"second-life boundary restores exact EXP and every recovered attribute point once");
        var fresh=ReloadBall(p);SkillTable.LoadProficiencies(fresh);fresh.IncreaseExperience(1,false);
        Check(!Fenced(fresh) && fresh.Entity.Experience==5000000001UL && SecondPending(fresh)==0,"production reconnect never replays consumed second-life EXP");
        fresh.Entity.Level=129;fresh.Entity.Experience=DataHolder.LevelExperience(129)-1;EntityTable.SaveEntity(fresh);fresh.IncreaseExperience(1,false);
        Check(fresh.Entity.Level==130 && fresh.Entity.Experience==0,"consumed receipt suppresses old level-only shortcut");
        var early=PendingSecond(town);early.Entity.Level=110;early.Entity.Experience=0;EntityTable.SaveEntity(early);GeneralData.ReincarnationHash(early);
        Check(early.Entity.Level==110 && SecondPending(early)==1,"second rebirth never uses paid reincarnation level-110 threshold");
        early.Entity.Level=120;EntityTable.SaveEntity(early);Revora.Network.GamePackets.Reincarnation.Process(ReincarnationRequest(),early);
        Check(early.Entity.Level==120 && SecondPending(early)==1 && !Kernel.ReincarnatedCharacters.ContainsKey(early.Entity.UID),"paid reincarnation cannot replace pending second-life recovery");
        var equal=PendingSecond(town,130,1000);equal.Entity.Level=130;equal.Entity.Experience=17;EntityTable.SaveEntity(equal);equal.IncreaseExperience(0,false);
        Check(equal.Entity.Level==130 && equal.Entity.Experience==1017 && SecondPending(equal)==0,"same-level second-life receipt still owes exact saved EXP");
        var carry=PendingSecond(town);carry.IncreaseExperience(DataHolder.LevelExperience(139)+124,false);
        Check(carry.Entity.Level==140 && carry.Entity.Experience==5000000123UL && SecondPending(carry)==0,"remaining award follows restored level curve");
        foreach(string target in new[]{"entities","skills","revora_second_rebirths"}) {
            var failed=PendingSecond(town);Query("CREATE FUNCTION suppress_second_recovery() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_second_recovery BEFORE "+(target=="skills"?"INSERT":"UPDATE")+" ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_second_recovery()");
            bool rejected;try{rejected=RecoveryRejected(()=>failed.IncreaseExperience(1,false));}finally{Query("DROP TRIGGER suppress_second_recovery ON "+target+";DROP FUNCTION suppress_second_recovery()");}
            Check(rejected && Fenced(failed) && failed.Entity.Level==129 && SecondPending(failed)==1,"failed second recovery rolls back level and receipt: "+target);
            var retry=ReloadBall(failed);SkillTable.LoadProficiencies(retry);retry.IncreaseExperience(1,false);
            Check(!Fenced(retry) && retry.Entity.Level==139 && retry.Entity.Experience==5000000000UL && SecondPending(retry)==0,"reconnected recovery retries once: "+target);
        }
        foreach(string part in new[]{"experience","consumed","first","second","missing"}) {
            var stale=PendingSecond(town);LifeSnapshot(stale);
            Query(part=="missing"?"DELETE FROM revora_second_rebirths WHERE uid=@uid":"UPDATE revora_second_rebirths SET "+
                (part=="experience"?"old_experience=old_experience+1":part=="consumed"?"recovered_at=now()":part=="first"?"first_class=25":"old_class=25")+" WHERE uid=@uid","uid",(long)stale.Entity.UID);
            Check(RecoveryRejected(()=>stale.IncreaseExperience(1,false)) && Fenced(stale) && stale.Entity.Level==129,"changed second-life receipt fails its exact preimage: "+part);
        }
        var consumed=PendingSecond(town);consumed.IncreaseExperience(1,false);
        // A later paid reincarnation legitimately changes the history. A retired
        // second-life receipt must not fence that new life or restore twice.
        consumed.Entity.FirstRebornClass=25;consumed.Entity.SecondRebornClass=65;consumed.Entity.Class=41;
        EntityTable.SaveEntity(consumed);consumed=ReloadBall(consumed);consumed.IncreaseExperience(1,false);
        Check(!Fenced(consumed) && consumed.Entity.Experience==5000000001UL, "consumed second-life receipt tolerates a later reincarnation history");
        var malformed=PendingSecond(town);Query("UPDATE revora_second_rebirths SET old_experience=-1 WHERE uid=@uid", "uid", (long)malformed.Entity.UID);
        Check(RecoveryRejected(()=>malformed.IncreaseExperience(1,false)) && Fenced(malformed) && malformed.Entity.Level==129, "negative second-life EXP is rejected at receipt load");
        var race=PendingSecond(town);Parallel.For(0,20,i=>race.IncreaseExperience(1,false));
        Check(!Fenced(race) && race.Entity.Experience==5000000019UL && SecondPending(race)==0,"concurrent second-life awards preserve every earned EXP and recover once");
    }
}
