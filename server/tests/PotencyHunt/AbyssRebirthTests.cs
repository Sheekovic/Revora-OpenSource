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
    static void AbyssRiteOffer(GameState p,byte next=11) {AbyssOpen(p,59558);SoulWire(p,2,59558);SoulWire(p,(byte)(170+Array.IndexOf(rebirthClasses,next)),59558);}
    static GameState AbyssRiteReload(GameState p) {p=ReloadBall(p);SkillTable.LoadProficiencies(p);Call(AbyssQuest,null,"OnLogin",p);return p;}
    static void VerifyAbyssEarnedRite(GameState p,Map town,Map map) {
        SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>secondNow));
        RebirthProf(p);RebirthSkill(p,1045);var token=p.Inventory.Objects.Single(i=>i.ID==723701);
        // The original NPC-earned run is retained throughout rollback tests.
        foreach(string table in new[]{"revora_abyss_trials","entities","items","skills","profs","revora_second_rebirths","revora_achievement_evidence"}) {
            AbyssRiteOffer(p);
            Query("CREATE FUNCTION abyss_rite_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER abyss_rite_fault BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION abyss_rite_fault()");
            try {SoulWire(p,180,59558);}finally{Query("DROP TRIGGER abyss_rite_fault ON "+table);Query("DROP FUNCTION abyss_rite_fault()");}
            Check(Fenced(p) && p.Entity.Reborn==1 && !(bool)AbyssField(AbyssRead(p),"Consumed") && SecondCount(p)==0
                && SavedRune(token,"id")==723701 && Scalar("SELECT reborn FROM entities WHERE uid=@uid",p.Entity.UID)==1,
                "quest-funded rite rolls back completion/profile/gift/receipt and leaves the token untouched: "+table);
            p=AbyssRiteReload(p);
        }
        foreach(string mode in new[]{"version","bound","history","proof-owner","final-family","consumed"}) {
            AbyssRiteOffer(p);
            string change=mode=="version"?"UPDATE revora_abyss_trials SET version=version+1 WHERE uid=@uid"
                :mode=="bound"?"UPDATE revora_abyss_trials SET bound=true WHERE uid=@uid"
                :mode=="history"?"UPDATE revora_abyss_trials SET first_class=25 WHERE uid=@uid"
                :mode=="proof-owner"?"UPDATE revora_abyss_completions SET uid=0 WHERE uid=@uid"
                :mode=="final-family"?"UPDATE revora_abyss_encounters SET family=3645 WHERE id=(SELECT encounter FROM revora_abyss_completions WHERE uid=@uid)"
                :"UPDATE revora_abyss_trials SET consumed_at=now() WHERE uid=@uid";
            Query(change,"uid",(long)p.Entity.UID);SoulWire(p,180,59558);
            Check(Fenced(p) && p.Entity.Reborn==1 && SecondCount(p)==0 && SavedRune(token,"id")==723701,
                "quest-funded rite rejects changed owned completion preimage: "+mode);
            if(mode=="bound")Query("UPDATE revora_abyss_trials SET bound=false WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="history")Query("UPDATE revora_abyss_trials SET first_class=15 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="proof-owner")Query("UPDATE revora_abyss_completions SET uid=@uid WHERE uid=0","uid",(long)p.Entity.UID);
            if(mode=="final-family")Query("UPDATE revora_abyss_encounters SET family=3646 WHERE id=(SELECT encounter FROM revora_abyss_completions WHERE uid=@uid)","uid",(long)p.Entity.UID);
            if(mode=="consumed")Query("UPDATE revora_abyss_trials SET consumed_at=NULL WHERE uid=@uid","uid",(long)p.Entity.UID);
            p=AbyssRiteReload(p);
        }
        foreach(string mode in new[]{"cancel","expire","full-bag"}) {
            if(mode=="full-bag")while(p.Inventory.Count<40)Pack(p,1088001,1);
            AbyssRiteOffer(p);if(mode=="cancel")SoulWire(p,255,59558);if(mode=="expire")secondNow=secondNow.AddMinutes(3);SoulWire(p,180,59558);
            Check(!Fenced(p) && p.Entity.Reborn==1 && !(bool)AbyssField(AbyssRead(p),"Consumed") && SavedRune(token,"id")==723701,
                "quest-funded rite rejects before consuming completion or token: "+mode);
        }
        var filler=p.Inventory.Objects.First(i=>i.ID==1088001);DisRemove(p,filler);
        DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1088001));
        AbyssRiteOffer(p);Parallel.For(0,12,i=>SoulWire(p,180,59558));
        Check(!Fenced(p) && p.Entity.Reborn==2 && p.Entity.Level==15 && p.Inventory.Count==40 && SecondCount(p)==1
            && (bool)AbyssField(AbyssRead(p),"Consumed") && !(bool)Call(AbyssJournal,null,"Selected",p)
            && SavedRune(token,"id")==723701 && p.Inventory.ContainsUID(token.UID)
            && Scalar("SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid AND payment_kind='abyss-quest' AND token_uid=0 AND quest_run IS NOT NULL",p.Entity.UID)==1,
            "concurrent confirmations consume the earned NPC trial once, retain the token and use the two actual reward slots");
        Check(HasAchievement(p,10316) && HasAchievement(p,11031) && p.Quests.GetQuest((QuestID)2414).Kills==27,
            "actual quest-funded rite earns second rebirth and restores the original Cup journal");
        VerifySecondSoul(p);p=AbyssRiteReload(p);VerifySecondSoul(p);Check(!Fenced(p) && p.Entity.Reborn==2 && p.Inventory.ContainsUID(token.UID)
            && (bool)AbyssField(AbyssRead(p),"Consumed"),"quest-funded rite and untouched token survive production reload");
        // The independent world journey paid bound offerings and has real final-death proof.
        var bound=abyssWorldCompleter;AbyssKeeper(bound,3604,1);DisRemove(bound,bound.Inventory.Objects.Single(i=>i.ID==723701));
        var before=bound.Inventory.Objects.Select(i=>i.UID).ToArray();byte oldLevel=bound.Entity.Level;ulong oldExperience=bound.Entity.Experience;
        AbyssRiteOffer(bound);SoulWire(bound,180,59558);
        Check(!Fenced(bound) && bound.Entity.Reborn==2 && bound.Inventory.Objects.Any(i=>!before.Contains(i.UID) && i.ID==410077 && i.Bound)
            && !bound.Inventory.Contains(723701,1) && (bool)AbyssField(AbyssRead(bound),"Consumed"),
            "actual bound Abyss completion begins a third life with no token and binds its gift");
        VerifySecondSoul(bound);bound=AbyssRiteReload(bound);VerifySecondSoul(bound);bound.Entity.Level=129;bound.Entity.Experience=DataHolder.LevelExperience(129)-1;
        bound.Entity.Atributes=checked((ushort)(bound.Entity.Atributes+114*Config.AttributePointsPerLevel));EntityTable.SaveEntity(bound);
        bound.IncreaseExperience(1,false);
        Check(!Fenced(bound) && bound.Entity.Level==oldLevel && bound.Entity.Experience==oldExperience && SecondPending(bound)==0,
            "quest-funded second-life recovery restores recorded EXP once at level 130");
        VerifyAbyssRiteDebt(town,map);
    }
    static void VerifyAbyssRiteDebt(Map town,Map map) {
        var terminal=AbyssHunter(town,map);DisRemove(terminal,terminal.Inventory.Objects.Single(i=>i.ID==723701));
        Query("UPDATE revora_abyss_trials SET stage=5,meters=40000,spirits=72,forms=3 WHERE uid=@uid","uid",(long)terminal.Entity.UID);
        AbyssAt(terminal,town,59558);AbyssRiteOffer(terminal);SoulWire(terminal,180,59558);
        Check(!Fenced(terminal) && terminal.Entity.Reborn==1 && SecondCount(terminal)==0,
            "synthetic terminal counters without owned final combat proof cannot fund rebirth");
        var debt=AbyssHunter(town,map);foreach(uint type in new uint[]{722723,722724,722725})Pack(debt,type,1);
        var actor=AbyssActor(debt,map,3632);AbyssAt(debt,town,59558);AbyssRiteOffer(debt,61);SoulWire(debt,180,59558);
        Check(!Fenced(debt) && debt.Entity.Reborn==1 && debt.Inventory.Contains(723701,1),"token bypass cannot strand an outstanding paid Abyss encounter");
        AbyssAt(debt,map,3605);while(debt.Inventory.Count<40)Pack(debt,1088001,1);AbyssFight(debt,actor);
        DisRemove(debt,debt.Inventory.Objects.First(i=>i.ID==1088001));AbyssAt(debt,town,59558);AbyssRiteOffer(debt,61);SoulWire(debt,180,59558);
        Check(!Fenced(debt) && debt.Entity.Reborn==1 && AbyssPending(debt)==1 && debt.Inventory.Contains(723701,1),
            "token bypass waits for saved full-bag Abyss spoils even after a slot is freed");
        SoulWire(debt,184,59558);AbyssRiteOffer(debt,61);SoulWire(debt,180,59558);
        Check(!Fenced(debt) && debt.Entity.Reborn==2 && AbyssPending(debt)==0 && !debt.Inventory.Contains(723701,1)
            && !(bool)AbyssField(AbyssRead(debt),"Consumed") && !(bool)Call(AbyssJournal,null,"Selected",debt) && !HasAchievement(debt,11031),
            "claiming outstanding spoils permits token bypass, retires the overlay and invents no quest completion");
    }
}
