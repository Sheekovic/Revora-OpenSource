using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool disClaimFixture;
    static string DisClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'entries',(SELECT json_agg(e ORDER BY run) FROM revora_dis_entries e WHERE uid=@uid),
'rules',(SELECT json_agg(r ORDER BY run) FROM revora_dis_run_rules r WHERE uid=@uid),
'run',(SELECT row_to_json(r) FROM revora_dis_runs r WHERE uid=@uid),
'steps',(SELECT json_agg(s ORDER BY run,step) FROM revora_dis_steps s WHERE uid=@uid))::text",
        "snapshot",PromotionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void DisClaimProcess(bool reload){
        disClaimFixture=true;const uint first=4294911601;
        if((bool)Sql("SELECT to_regclass('revora_dis_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+2)==0);
            nextUid=first;for(int n=0;n<3;n++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=110,reborn=0 WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+2);
            Sql("CREATE TABLE revora_dis_claim_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        var type=Feature("DisCityQuest");var clock=type.GetField("Clock",Hidden);object old=clock.GetValue(null);DateTime now=new DateTime(2026,10,2,20,0,0,DateTimeKind.Utc);
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            for(int difficulty=0;difficulty<3;difficulty++){
                uint uid=first+(uint)difficulty;nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);Call(type,"OnLogin",p);
                Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
                if((long)Sql("SELECT count(*) FROM revora_dis_claim_probe WHERE uid=@uid","uid",(long)uid)==0){Require(!reload);
                    string before=DisClaimSnapshot(p);NobilityClaimPacket(p,404);Require(before==DisClaimSnapshot(p));
                    now=new DateTime(2026,10,6,20,0,0,DateTimeKind.Utc);HeartTalk(p,3215,1);Require(p.Entity.MapID==1002 && p.Inventory.Count==0);
                    NobilityClaimPacket(p,404);Require(before==DisClaimSnapshot(p));now=new DateTime(2026,10,2,20,0,0,DateTimeKind.Utc);
                    if(difficulty==0){
                        Sql(@"CREATE FUNCTION revora_dis_claim_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.uid=4294911601 AND NEW.last_map<>OLD.last_map THEN RAISE EXCEPTION 'private arrival fault'; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_dis_claim_fault BEFORE UPDATE ON revora_dis_runs FOR EACH ROW EXECUTE FUNCTION revora_dis_claim_fault();");
                        try{HeartTalk(p,3215,1);Require(PirateFenced(p));}finally{Sql("DROP TRIGGER revora_dis_claim_fault ON revora_dis_runs;DROP FUNCTION revora_dis_claim_fault();");}
                        Require((long)Sql("SELECT count(*) FROM revora_dis_runs WHERE uid=@uid","uid",(long)uid)==1 && (long)Sql("SELECT count(*) FROM revora_dis_entries WHERE uid=@uid","uid",(long)uid)==0);
                        nextUid=uid;p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);Call(type,"OnLogin",p);
                        string failed=DisClaimSnapshot(p);NobilityClaimPacket(p,404);Require(failed==DisClaimSnapshot(p) && !PirateFenced(p));
                    }
                    if(difficulty==0)HeartTalk(p,3215,1);else{HeartTalk(p,3215,7);HeartTalk(p,3215,(byte)(10+difficulty));}
                    Require(p.Map.BaseID==2021 && !PirateFenced(p) && p.Map.Entities.Values.Any(e=>e.MonsterInfo!=null && !e.Dead));
                    Require((long)Sql("SELECT count(*) FROM revora_dis_entries e JOIN revora_dis_run_rules r ON r.run=e.run AND r.uid=e.uid WHERE e.uid=@uid AND r.difficulty=@difficulty","uid",(long)uid,"difficulty",difficulty)==1);
                    HeartTalk(p,3215,9);Require(p.Entity.MapID==1002);HeartTalk(p,3215,1);Require(p.Map.BaseID==2021);HeartTalk(p,3215,9);Require(p.Entity.MapID==1002);
                    var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,404));
                    var ball=p.Inventory.Objects.Single(i=>!prior.Contains(i.UID));Require(ball.ID==723700 && ball.Bound && ball.StackSize==1);
                    ulong exp=p.Entity.Experience;byte level=p.Entity.Level;SoulClaimUse(p,ball);Require(p.Entity.Level>level || p.Entity.Experience>exp);
                    Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_dis_claim_probe VALUES(@uid,@snapshot)","uid",(long)uid,"snapshot",DisClaimSnapshot(p));
                }
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=404 AND NOT legacy","uid",(long)uid)==1);
                Require((long)Sql("SELECT count(*) FROM revora_dis_entries WHERE uid=@uid","uid",(long)uid)==1);
                Require(Convert.ToInt32(Sql("SELECT used FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)uid))==1);
                string exact=DisClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_dis_claim_probe WHERE uid=@uid","uid",(long)uid)));
                NobilityClaimPacket(p,404);Require(exact==DisClaimSnapshot(p) && !PirateFenced(p));
            }
            uint historical=Convert.ToUInt32(Sql("SELECT e.uid FROM revora_dis_completions e WHERE NOT EXISTS(SELECT 1 FROM revora_dis_entries a WHERE a.uid=e.uid) ORDER BY e.uid LIMIT 1"));Require(historical!=0);
            using(var c=DataHolder.MySqlConnection){c.Open();Require((bool)Call(Feature("WayOfHeroesClaims"),"DisCityEntered",c,null,historical));}
        }finally{clock.SetValue(null,old);}
        Require((bool)Sql("SELECT to_regprocedure('revora_dis_claim_fault()') IS NULL"));
        System.Console.WriteLine("[career-process] career-dis-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Dis claim did not close.");
    }
}
