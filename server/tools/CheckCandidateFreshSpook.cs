using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSpookSnapshot(GameState p) { return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'daily',(SELECT json_agg(r ORDER BY id) FROM revora_exorcism_daily r WHERE uid=@uid),'hunts',(SELECT json_agg(r ORDER BY id) FROM revora_exorcism_hunts r WHERE uid=@uid),'members',(SELECT json_agg(r ORDER BY run) FROM revora_exorcism_members r WHERE uid=@uid),'achievement',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=10529))::text","prior",FreshBeadSnapshot(p),"uid",(long)p.Entity.UID)); }
    static void FreshSpookSave(GameState p,string stage,int hits) { Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_spook_probe SET stage=@stage,hits=@hits,snapshot=@snapshot","stage",stage,"hits",hits,"snapshot",FreshSpookSnapshot(p)); }
    static void FreshSpookProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();DialyQuestTable.Load(p);Call(Feature("ExorcismQuest"),"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_spook_probe') IS NULL")) {
            Require(phase.EndsWith("hunt") && FreshBeadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_bead_probe")) && (long)Sql("SELECT count(*) FROM revora_exorcism_daily WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_spook_probe(stage text,hits integer,snapshot text);INSERT INTO revora_fresh_spook_probe VALUES('new',0,@snapshot)","snapshot",FreshSpookSnapshot(p));
        }
        Require(FreshSpookSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_spook_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_spook_probe"));int hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_spook_probe"));
        if(phase.EndsWith("hunt")) {
            Require(stage=="new");p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,79882,6);Require(p.Entity.MapID==1036);HeartTalk(p,12333,1);HeartTalk(p,12333,12);Require(p.Map.BaseID==1013 && p.Map.Npcs.ContainsKey(900000132));HeartTalk(p,900000132,3);
            var body=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9916201);Require(body.Hitpoints==10000000);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+1);p.Screen.Add(body);var timer=System.Diagnostics.Stopwatch.StartNew();uint hp=body.Hitpoints;
            while(!body.Dead && hits<30000 && timer.Elapsed.TotalSeconds<150) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);hits++;if(hits==1)Require(body.Hitpoints<hp);if(hits%500==0)System.Console.WriteLine("[fresh-spook] hits="+hits+" hp="+body.Hitpoints);}
            Require(body.Dead && !p.Entity.Dead && !PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_exorcism_daily WHERE uid=@uid AND state='victory' AND grade=1 AND rune_reward AND NOT valuable_reward","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10529","uid",(long)p.Entity.UID)==1);
            HeartTalk(p,900000132,2);Require(p.Entity.MapID==1036);HeartTalk(p,12333,0);FreshSpookSave(p,"ready",hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            HeartTalk(p,12333,11);Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailyExorcismStudy);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==3 && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==Config.DailyExorcismToken) && rewards.Any(i=>i.ID==4030101 && i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_exorcism_daily WHERE uid=@uid AND state='claimed' AND NOT paid AND cardinality(reward_uids)=3 AND paid_cp=0","uid",(long)p.Entity.UID)==1);FreshSpookSave(p,"paid",hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("hunt")) {string before=FreshSpookSnapshot(p);HeartTalk(p,12333,11);HeartTalk(p,12333,1);HeartTalk(p,12333,11);Require(before==FreshSpookSnapshot(p));}
        Require(FreshSpookSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_spook_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" hits="+hits+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Spook did not close.");
    }
}
