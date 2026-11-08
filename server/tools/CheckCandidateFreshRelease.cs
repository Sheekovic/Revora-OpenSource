using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshReleaseSnapshot(GameState p) { return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'souls',(SELECT json_agg(r ORDER BY day) FROM revora_soul_runs r WHERE uid=@uid),'achievement',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=11012))::text","prior",FreshPriceSnapshot(p),"uid",(long)p.Entity.UID)); }
    static void FreshReleaseSave(GameState p,string stage) { Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_release_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshReleaseSnapshot(p)); }
    static void FreshReleaseProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();var souls=Feature("ReleaseSoulsQuest");Call(souls,"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_release_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshPriceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_price_probe")) && (long)Sql("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_release_probe(stage text,snapshot text);INSERT INTO revora_fresh_release_probe VALUES('new',@snapshot)","snapshot",FreshReleaseSnapshot(p));
        }
        Require(FreshReleaseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_release_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_release_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");HeartTalk(p,79882,3);Require(p.Entity.MapID==1000);HeartTalk(p,301,1);
            int count=0;
            foreach(uint npc in new uint[]{65822,65823,65824}) {
                HeartTalk(p,npc,0);Require(p.Entity.OnCheckGuard);Call(souls,"Tick");Require(p.Entity.OnCheckGuard);
                System.Threading.Thread.Sleep(3100);Call(souls,"Tick");Require(!p.Entity.OnCheckGuard && !PirateFenced(p));count++;
                Require(Convert.ToInt32(Sql("SELECT cardinality(seen) FROM revora_soul_runs WHERE uid=@uid","uid",(long)p.Entity.UID))==count);
                HeartTalk(p,npc,0);Call(souls,"Tick");Require(!p.Entity.OnCheckGuard && Convert.ToInt32(Sql("SELECT cardinality(seen) FROM revora_soul_runs WHERE uid=@uid","uid",(long)p.Entity.UID))==count);
            }
            HeartTalk(p,301,0);FreshReleaseSave(p,"ready");
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            HeartTalk(p,301,3);Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailyDesertGuardianStudy);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==3 && rewards.Any(i=>i.ID==722136) && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==4030101 && i.Bound));
            Require((long)Sql("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid AND state='complete' AND cardinality(seen)=3 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11012","uid",(long)p.Entity.UID)==1);FreshReleaseSave(p,"paid");
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("earn")) { string before=FreshReleaseSnapshot(p);HeartTalk(p,301,1);HeartTalk(p,301,3);HeartTalk(p,301,4);Require(before==FreshReleaseSnapshot(p)); }
        Require(FreshReleaseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_release_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Release Souls did not close.");
    }
}
