using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSnakeSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'hunts',(SELECT json_agg(r ORDER BY day) FROM revora_snake_hunts r WHERE uid=@uid),'attempts',(SELECT json_agg(r ORDER BY started_at,id) FROM revora_snake_attempts r WHERE uid=@uid))::text","prior",FreshSkySnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSnakeSave(GameState p,string stage,int kills,int hits){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_snake_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshSnakeSnapshot(p));}
    static void FreshSnakeProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_snake_probe') IS NULL")){
            Require(phase.EndsWith("earn") && FreshSkySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_sky_probe")) && (long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_snake_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_snake_probe VALUES('new',0,0,@snapshot)","snapshot",FreshSnakeSnapshot(p));
        }
        Require(FreshSnakeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snake_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_snake_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_snake_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_snake_probe"));
        if(phase.EndsWith("earn")){
            Require(stage=="new" && p.Inventory.Count<=37);FreshClassicTalk(p,1015,30115,0);string unpaid=FreshSnakeSnapshot(p);HeartTalk(p,30115,3);Require(unpaid==FreshSnakeSnapshot(p));HeartTalk(p,30115,1);Require(p.Entity.MapID==1063);
            var boss=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==3102 && !e.Dead);CareerJumpTo(p,boss.X,boss.Y);FreshClassicKill(p,1063,3102,ref kills,ref hits);
            Require(kills==1 && (long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND state='victory' AND NOT entered AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);new TitleStorage().Login(p);FreshSnakeSave(p,"earned",kills,hits);
        }else if(phase.EndsWith("claim")){
            Require(stage=="earned");Call(Feature("SnakeIslandsQuest"),"OnLogin",p);Require(p.Entity.MapID==1015);var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,30115,3);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();
            Require(added.Length==3 && added.All(i=>i.Bound) && added.Count(i=>i.ID==1088001)==2 && added.Count(i=>i.ID==4030101)==1 && prior.All(uid=>p.Inventory.ContainsUID(uid)));FreshSnakeSave(p,"paid",kills,hits);
        }else Require(phase.EndsWith("reload") && stage=="paid");
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11018 AND source='snake-islands-v1'","uid",(long)p.Entity.UID)==1);
        if(!phase.EndsWith("earn")){
            Require((long)Sql("SELECT count(*) FROM revora_snake_hunts r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND r.state='claimed' AND i.entityid=r.uid AND i.bound=1 AND i.id IN(1088001,4030101)","uid",(long)p.Entity.UID)==3);
            string exact=FreshSnakeSnapshot(p);HeartTalk(p,30115,3);HeartTalk(p,30115,1);Require(p.Entity.MapID==1015 && exact==FreshSnakeSnapshot(p));
        }
        Require(!PirateFenced(p) && FreshSnakeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snake_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Snake Islands did not close.");
    }
}
