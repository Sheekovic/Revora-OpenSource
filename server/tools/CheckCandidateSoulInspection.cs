using System;
using System.Collections;
using System.Linq;
using Revora;

internal static partial class CheckCandidateArenaLifecycle
{
    static string SoulInspectionSnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,position FROM items WHERE entityid=@uid) r),
'runs',(SELECT json_agg(r ORDER BY day) FROM revora_soul_runs r WHERE uid=@uid),
'purse',(SELECT row_to_json(r) FROM (SELECT conquerpoints,revorapoint,studypoints FROM entities WHERE uid=@uid) r),
'achievement',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=11012))::text","uid",(long)FirstUid));
    }
    static void SoulInspectionProcess(bool reload) {
        var souls=Feature("ReleaseSoulsQuest");
        var now=new DateTime(2033,2,6,20,0,0,DateTimeKind.Utc);
        souls.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(!reload){Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==Revora.Database.AccountTable.AccountState.Player);
        if(reload) {
            string expected=System.IO.File.ReadAllText("soul-inspection.snapshot");
            Require(SoulInspectionSnapshot()==expected);
            Call(souls,"OnLogin",p);p.BindWorldMap(1000);
            Require(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==1);
            Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT conquerpoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)));
            Require(p.Entity.RevoraPoint==Convert.ToUInt32(Sql("SELECT revorapoint FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)));
            Require(p.Entity.SubClasses.StudyPoints==Convert.ToUInt16(Sql("SELECT studypoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)));
            foreach(var item in p.Inventory.Objects)Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND entityid=@uid AND id=@id AND bound=@bound AND stacksize=@stack)","item",(long)item.UID,"uid",(long)p.Entity.UID,"id",(int)item.ID,"bound",item.Bound?1:0,"stack",(int)item.StackSize));
            Require(p.Inventory.Count==(long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0","uid",(long)p.Entity.UID));
            HeartTalk(p,301,3);HeartTalk(p,301,1);HeartTalk(p,65823,0);Call(souls,"Tick");
            Require(!p.Entity.OnCheckGuard && SoulInspectionSnapshot()==expected);
            System.Console.WriteLine("[soul-process] soul-inspection-reload-passed");
            Server.CommandsAI("@close");throw new Exception("Soul inspection reload did not close.");
        }
        p.BindWorldMap(1000);HeartTalk(p,301,1);HeartTalk(p,65822,0);
        var inspections=(IDictionary)souls.GetField("inspecting",Hidden).GetValue(null);
        Require(inspections.Contains(p.Entity.UID) && p.Entity.OnCheckGuard);
        now=now.AddSeconds(3);
        Exception failure=null;
        var worker=new System.Threading.Thread(()=>{try{Call(souls,"Tick");}catch(Exception ex){failure=ex;}});
        worker.IsBackground=true;
        try {
            lock(p.ItemSyncRoot) {
                worker.Start();var end=DateTime.UtcNow.AddSeconds(5);
                while((worker.ThreadState&System.Threading.ThreadState.WaitSleepJoin)==0 && DateTime.UtcNow<end)System.Threading.Thread.Sleep(10);
                Require((worker.ThreadState&System.Threading.ThreadState.WaitSleepJoin)!=0);
                // A second NPC request moves the character while the expired
                // inspection callback waits for the same player lock.
                HeartTalk(p,65823,0);
            }
        }finally {Require(worker.Join(5000));}
        if(failure!=null)throw failure;
        Require(!inspections.Contains(p.Entity.UID) && !p.Entity.OnCheckGuard);
        Require(Convert.ToInt32(Sql("SELECT cardinality(seen) FROM revora_soul_runs WHERE uid=@uid AND day='20330206'","uid",(long)p.Entity.UID))==0);
        HeartTalk(p,65823,0);Require(p.Entity.OnCheckGuard);
        now=now.AddSeconds(3);Call(souls,"Tick");
        Require(!inspections.Contains(p.Entity.UID) && !p.Entity.OnCheckGuard);
        Require((bool)Sql("SELECT seen=ARRAY[65823]::bigint[] FROM revora_soul_runs WHERE uid=@uid AND day='20330206'","uid",(long)p.Entity.UID));
        foreach(uint npc in new uint[]{65822,65824}) {
            HeartTalk(p,npc,0);now=now.AddSeconds(3);Call(souls,"Tick");
        }
        HeartTalk(p,301,3);
        Require(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==1);
        Require((long)Sql("SELECT count(*) FROM revora_soul_runs r JOIN items i ON i.uid=ANY(r.rewards) AND i.entityid=r.uid WHERE r.uid=@uid AND r.day='20330206' AND r.state='complete' AND cardinality(r.seen)=3 AND i.id=4030101 AND i.bound=1","uid",(long)p.Entity.UID)==1);
        string saved=SoulInspectionSnapshot();HeartTalk(p,301,3);Call(souls,"Tick");
        Require(SoulInspectionSnapshot()==saved);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11012","uid",(long)p.Entity.UID)==1);
        Require(Revora.Database.EntityTable.SaveEntity(p));
        System.IO.File.WriteAllText("soul-inspection.snapshot",SoulInspectionSnapshot());
        System.Console.WriteLine("[soul-process] soul-inspection-passed");
        Server.CommandsAI("@close");throw new Exception("Soul inspection did not close.");
    }
}
