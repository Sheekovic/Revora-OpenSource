using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshDailyClaimSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'way',(SELECT row_to_json(r) FROM wayofheroes r WHERE uid=@uid),'claims',(SELECT json_agg(r ORDER BY stage_uid) FROM revora_way_of_heroes_claims r WHERE uid=@uid),'stones',(SELECT json_agg(r ORDER BY id) FROM revora_praying_stone_uses r WHERE uid=@uid))::text","prior",FreshRareSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshDailyClaimSave(GameState p,string stage) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_daily_claim_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshDailyClaimSnapshot(p));}
    static void FreshDailyClaimProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_daily_claim_probe') IS NULL")) {
            Require(phase.EndsWith("claim") && FreshRareSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rare_probe")) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=409","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_daily_claim_probe(stage text,reward bigint,blessing bigint,snapshot text);INSERT INTO revora_fresh_daily_claim_probe VALUES('new',NULL,@bless,@snapshot)","bless",(long)p.Entity.HeavenBlessing,"snapshot",FreshDailyClaimSnapshot(p));
        }
        Require(FreshDailyClaimSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_claim_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_daily_claim_probe"));
        if(phase.EndsWith("claim")) {
            Require(stage=="new");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;ushort study=p.Entity.SubClasses.StudyPoints;
            NobilityClaimPacket(p,409);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].ID==1200001 && added[0].Bound && added[0].StackSize==1 && prior.All(uid=>p.Inventory.ContainsUID(uid)) && !PirateFenced(p));
            Require(p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==silver && p.Entity.SubClasses.StudyPoints==study);Sql("UPDATE revora_fresh_daily_claim_probe SET reward=@item","item",(long)added[0].UID);FreshDailyClaimSave(p,"claimed");
        } else if(phase.EndsWith("use")) {
            Require(stage=="claimed");uint uid=Convert.ToUInt32(Sql("SELECT reward FROM revora_fresh_daily_claim_probe"));var stone=p.Inventory.Objects.Single(i=>i.UID==uid);Require(stone.ID==1200001 && stone.Bound);SoulClaimUse(p,stone);
            Require(p.Entity.HeavenBlessing==Convert.ToUInt32(Sql("SELECT blessing FROM revora_fresh_daily_claim_probe"))+604800);FreshDailyClaimSave(p,"used");
        } else Require(phase.EndsWith("reload") && stage=="used");
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=409 AND NOT legacy","uid",(long)p.Entity.UID)==1);
        if(!phase.EndsWith("claim"))Require((long)Sql("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid AND item_uid=(SELECT reward FROM revora_fresh_daily_claim_probe) AND item_type=1200001 AND bound AND seconds_after-seconds_before=604800","uid",(long)p.Entity.UID)==1);
        string before=FreshDailyClaimSnapshot(p);NobilityClaimPacket(p,409);NobilityClaimPacket(p,409);Require(before==FreshDailyClaimSnapshot(p) && !PirateFenced(p));Require(before==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_claim_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" blessing="+p.Entity.HeavenBlessing);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh daily claim did not close.");
    }
}
