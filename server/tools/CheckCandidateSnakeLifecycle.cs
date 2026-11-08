using System;
using System.Linq;
using Revora;
using Revora.Database;

internal static partial class CheckCandidateArenaLifecycle
{
    static string SnakeSnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,position FROM items WHERE entityid=@uid) r),
'hunts',(SELECT json_agg(r ORDER BY day) FROM revora_snake_hunts r WHERE uid=@uid),
'attempts',(SELECT json_agg(r ORDER BY started_at,id) FROM revora_snake_attempts r WHERE uid=@uid),
'achievement',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=11018))::text","uid",(long)FirstUid));
    }
    static void IslandArrivalRace(Revora.Client.GameState p,string feature,ushort home,ushort island,Func<string> snapshot) {
        var quest=Feature(feature);Exception failure=null;
        var tick=new System.Threading.Thread(()=>{try{Call(quest,"Tick");}catch(Exception ex){failure=ex;}});
        string before=snapshot();
        lock(p.ItemSyncRoot) {
            // Recreate the pre-arrival map visible between active admission and
            // Travel while its owner lock is held. No quest record is supplied.
            p.BindWorldMap(home);quest.GetField("nextScan",Hidden).SetValue(null,DateTime.MinValue);
            tick.Start();
            bool waiting=System.Threading.SpinWait.SpinUntil(()=>
                (tick.ThreadState&System.Threading.ThreadState.WaitSleepJoin)!=0 || !tick.IsAlive,3000);
            p.BindWorldMap(island);
            Require(waiting && tick.IsAlive);
        }
        Require(tick.Join(5000) && failure==null);
        Require(before==snapshot());
        // A genuine departure must still cancel, and ordinary admission must
        // then permit a fresh attempt before this fixture earns its victory.
        p.BindWorldMap(home);quest.GetField("nextScan",Hidden).SetValue(null,DateTime.MinValue);Call(quest,"Tick");
        if(feature=="SnakeIslandsQuest") {
            Require((long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND NOT entered","uid",(long)FirstUid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_snake_attempts WHERE uid=@uid AND reason='leave' AND ended_at IS NOT NULL","uid",(long)FirstUid)==1);
        }else Require((string)Sql("SELECT phase FROM revora_sky_runs WHERE uid=@uid","uid",(long)FirstUid)=="abandoned");
        HeartTalk(p,feature=="SnakeIslandsQuest"?30115u:30055u,1);Require(p.Entity.MapID==island);
    }
    static void SnakeProcess(string phase) {
        var snake=Feature("SnakeIslandsQuest");
        // The old earned victory must remain claimable after Pacific midnight.
        snake.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>new DateTime(2033,2,phase=="snake-victory"?1:2,20,0,0,DateTimeKind.Utc)));
        if(phase=="snake-victory"){Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        if(phase=="snake-victory") {
            p.BindWorldMap(1015);HeartTalk(p,30115,1);Require(p.Entity.MapID==1063);
            Require((long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND state='hunting' AND entered","uid",(long)FirstUid)==1);
            IslandArrivalRace(p,"SnakeIslandsQuest",1015,1063,SnakeSnapshot);
            var boss=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==3102 && !e.Dead);
            EliminationHit(p,boss);
            System.Console.WriteLine("[snake-process] post-hit dead="+p.Entity.Dead+" hp="+p.Entity.Hitpoints+" map="+p.Entity.MapID+" state="+
                Sql("SELECT state||':'||entered::text FROM revora_snake_hunts WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)FirstUid)+" reason="+
                Sql("SELECT reason FROM revora_snake_attempts WHERE uid=@uid ORDER BY started_at DESC LIMIT 1","uid",(long)FirstUid));
            Require((long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND state='victory' AND boss_uid=@boss AND rune_reward AND NOT entered","uid",(long)FirstUid,"boss",(long)boss.UID)==1);
            Require(p.Inventory.Count==0);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11018 AND source='snake-islands-v1'","uid",(long)FirstUid)==1);
            Require(EntityTable.SaveEntity(p));System.IO.File.WriteAllText("snake-victory.snapshot",SnakeSnapshot());
            System.Console.WriteLine("[snake-process] snake-victory-passed");Environment.Exit(0);
        }
        Require(System.IO.File.ReadAllText(phase=="snake-claim"?"snake-victory.snapshot":"snake-claimed.snapshot")==SnakeSnapshot());
        IslandWire((connected,child)=>{p=connected;Require(p.Entity.MapID==1015);
        var actor=p.Map.Npcs[30115];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);
        if(phase=="snake-claim")TeamWireSend(child,"snake-claim");
        Require(p.Inventory.Objects.Count(i=>i.ID==1088001 && i.Bound)==2);
        Require(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==1 && p.Inventory.Count==3);
        Require((long)Sql("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND state='claimed' AND day='20330201' AND cardinality(rewards)=3","uid",(long)FirstUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_snake_hunts r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND i.entityid=r.uid AND i.bound=1","uid",(long)FirstUid)==3);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11018 AND source='snake-islands-v1'","uid",(long)FirstUid)==1);
        string earned=SnakeSnapshot();TeamWireSend(child,"snake-claim");TeamWireSend(child,"snake-claim");Require(earned==SnakeSnapshot());
        TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+string.Join(";",p.Inventory.Objects.OrderBy(i=>i.UID).Select(i=>i.UID+","+i.ID+","+(i.Bound?"1":"0")))+":");
        Require(EntityTable.SaveEntity(p));
        if(phase=="snake-claim")System.IO.File.WriteAllText("snake-claimed.snapshot",earned);
        Require(System.IO.File.ReadAllText("snake-claimed.snapshot")==SnakeSnapshot());
        },true);
        System.Console.WriteLine("[snake-process] "+phase+"-passed");Server.CommandsAI("@close");throw new Exception("Snake process did not close.");
    }
}
