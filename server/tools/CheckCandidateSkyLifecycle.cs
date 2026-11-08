using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Database;

internal static partial class CheckCandidateArenaLifecycle
{
    static string SkySnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,position FROM items WHERE entityid=@uid) r),
'run',(SELECT row_to_json(r) FROM revora_sky_runs r WHERE uid=@uid),
'achievement',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=11019),
'claims',(SELECT json_agg(r ORDER BY day) FROM revora_sky_claims r WHERE uid=@uid))::text","uid",(long)FirstUid));
    }
    static uint SkyActor(int stage,int route,string field) {
        var challenge=Call(Feature("SkyPassWorld"),"At",stage,route);
        return (uint)challenge.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(challenge);
    }
    static void IslandWire(Action<Revora.Client.GameState,System.Diagnostics.Process> verify,bool snake=false) {
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe0");
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=FirstUid;account.Insert();}
        account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==FirstUid);
        Sql("UPDATE entities SET owner='TeamProbe0' WHERE uid=@uid","uid",(long)FirstUid);Kernel.GamePool.Remove(FirstUid);
        var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables[snake?"REVORA_SNAKE_WIRE":"REVORA_SKY_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]="0";
        start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=System.Diagnostics.Process.Start(start)) {
            password=null;start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[team-wire] ready");var p=Kernel.GamePool[FirstUid];
                Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);verify(p,child);
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            }finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}if(errors.Wait(1000)&&!string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);}
        }
    }
    static void SkyWireTalk(Revora.Client.GameState p,System.Diagnostics.Process child,uint npc,uint map) {
        var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);
        TeamWireSend(child,"sky-npc:"+npc+":"+map);
    }
    static void SkyProcess(string phase) {
        var sky=Feature("SkyPassQuest");
        sky.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>new DateTime(2033,2,1,20,0,0,DateTimeKind.Utc)));
        sky.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>4));
        if(phase=="sky-prize"){Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        if(phase=="sky-prize") {
            p.BindWorldMap(1012);HeartTalk(p,30055,1);Require(p.Entity.MapID==1040);
            IslandArrivalRace(p,"SkyPassQuest",1012,1040,SkySnapshot);
            for(int stage=1;stage<=4;stage++)HeartTalk(p,SkyActor(stage,4,"Gate"),1);
            HeartTalk(p,SkyActor(5,4,"Gate"),1);
            var mob=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==3024 && !e.Dead);
            EliminationHit(p,mob);Require(p.Inventory.Contains(721104,1));
            HeartTalk(p,SkyActor(5,4,"Guard"),1);
            Require(p.Inventory.Contains(721109,1) && !p.Inventory.Contains(721104,1));
            Require((string)Sql("SELECT phase FROM revora_sky_runs WHERE uid=@uid","uid",(long)FirstUid)=="prize");
            Require((long)Sql("SELECT count(*) FROM revora_sky_claims WHERE uid=@uid","uid",(long)FirstUid)==0);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11019","uid",(long)FirstUid)==0);
            Require(EntityTable.SaveEntity(p));
            System.IO.File.WriteAllText("sky-prize.snapshot",SkySnapshot());
            System.Console.WriteLine("[sky-process] sky-prize-passed");Environment.Exit(0);
        }
        Require(System.IO.File.ReadAllText(phase=="sky-claim"?"sky-prize.snapshot":"sky-claimed.snapshot")==SkySnapshot());
        IslandWire((connected,child)=>{p=connected;
        Require(p.Entity.MapID==1012);
        if(phase=="sky-claim") {
            SkyWireTalk(p,child,30055,1040);Require(p.Entity.MapID==1040 && p.Inventory.Contains(721109,1));
            SkyWireTalk(p,child,30054,1012);Require(p.Entity.MapID==1012);
        }
        Require(p.Inventory.Objects.Count(i=>i.ID==1088001 && i.Bound)==4);
        Require(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==1 && p.Inventory.Count==5);
        Require((long)Sql("SELECT count(*) FROM revora_sky_claims WHERE uid=@uid AND cardinality(rewards)=5","uid",(long)FirstUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11019 AND source='sky-pass-v1'","uid",(long)FirstUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_sky_claims r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND i.entityid=r.uid AND i.bound=1","uid",(long)FirstUid)==5);
        string earned=SkySnapshot();SkyWireTalk(p,child,30055,1012);Require(p.Entity.MapID==1012 && earned==SkySnapshot());
        TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+string.Join(";",p.Inventory.Objects.OrderBy(i=>i.UID).Select(i=>i.UID+","+i.ID+","+(i.Bound?"1":"0")))+":");
        Require(EntityTable.SaveEntity(p));
        if(phase=="sky-claim")System.IO.File.WriteAllText("sky-claimed.snapshot",earned);
        Require(System.IO.File.ReadAllText("sky-claimed.snapshot")==SkySnapshot());
        });
        System.Console.WriteLine("[sky-process] "+phase+"-passed");Server.CommandsAI("@close");throw new Exception("Sky process did not close.");
    }
}
