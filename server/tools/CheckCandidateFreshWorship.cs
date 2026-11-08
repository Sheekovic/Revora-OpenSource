using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshWorshipSnapshot(GameState p) { return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'worship',(SELECT json_agg(r ORDER BY day) FROM revora_worship_claims r WHERE uid=@uid))::text","prior",FreshReleaseSnapshot(p),"uid",(long)p.Entity.UID)); }
    static void FreshWorshipSave(GameState p,string stage) { Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_worship_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshWorshipSnapshot(p)); }
    static void FreshWorshipProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();DialyQuestTable.Load(p);var service=Feature("AncestorWorship");Call(service,"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_worship_probe') IS NULL")) {
            Require(phase.EndsWith("enter") && FreshReleaseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_release_probe")) && (long)Sql("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_worship_probe(stage text,snapshot text);INSERT INTO revora_fresh_worship_probe VALUES('new',@snapshot)","snapshot",FreshWorshipSnapshot(p));
        }
        Require(FreshWorshipSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_worship_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_worship_probe"));
        if(phase.EndsWith("enter")) {
            Require(stage=="new");p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,79882,1);Require(p.Entity.MapID==Config.DailyWorshipMap && p.Map.Npcs.ContainsKey(98130));
            Require((long)Sql("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid","uid",(long)p.Entity.UID)==0);FreshWorshipSave(p,"ready");
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;ulong gold=p.Entity.Money;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            HeartTalk(p,98130,1);Require(!PirateFenced(p) && p.Entity.HoringThe && p.Entity.ConquerPoints==cp+Config.DailyWorshipCp && p.Entity.RevoraPoint==rvp+Config.DailyWorshipRevoraPoints && p.Entity.SubClasses.StudyPoints==study && p.Entity.Money==gold);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==2 && rewards.Any(i=>i.ID==Config.DailyQuestChiToken && !i.Bound) && rewards.Any(i=>i.ID==4030101 && i.Bound));
            Require((long)Sql("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid AND NOT legacy AND statue=98130 AND chi_uid IS NOT NULL AND rune_uid IS NOT NULL AND soul_uid IS NULL","uid",(long)p.Entity.UID)==1);FreshWorshipSave(p,"paid");
        } else Require(phase.EndsWith("reload") && stage=="paid" && p.Entity.HoringThe);
        if(!phase.EndsWith("enter")) { HeartTalk(p,98130,0);string before=FreshWorshipSnapshot(p);HeartTalk(p,98130,1);HeartTalk(p,98130,1);Require(before==FreshWorshipSnapshot(p)); }
        Require(FreshWorshipSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_worship_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Worship did not close.");
    }
}
