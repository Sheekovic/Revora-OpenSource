using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime trojanClock;
    static void PrepareTrojan(string phase) {
        trojanClock=phase=="trojan-capture"?DateTime.UtcNow:DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_trojan_process_probe"),DateTimeKind.Utc);
        Feature("TrojanEpicStore").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>trojanClock));
    }
    static int TrojanProgress(GameState p,string field) {
        var progress=Call(Feature("TrojanEpicStore"),"Load",p);
        return Convert.ToInt32(progress.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(progress));
    }
    static void TrojanAction(GameState p,uint npc,byte option) { HeartTalk(p,npc,0);HeartTalk(p,npc,option); }
    static void TrojanCapture() {
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=120 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.ConquerPoints==0 && p.Inventory.Count==0);
        TrojanAction(p,10579,1);Require(TrojanProgress(p,"Stage")==1);
        for(int guard=0;TrojanProgress(p,"Stage")<3 && guard<60;guard++) {
            int revision=TrojanProgress(p,"Revision");
            TrojanAction(p,TrojanProgress(p,"Stage")==1?10580u:10579u,1);
            if(TrojanProgress(p,"Revision")==revision)trojanClock=trojanClock.AddDays(1);
        }
        Require(TrojanProgress(p,"Stage")==3 && TrojanProgress(p,"Energy")==27);
        TrojanAction(p,10579,1);Require(p.Map.BaseID==3825 && p.Map.ID!=3825);
        EntityTable.SaveEntity(p);
        Sql("CREATE TABLE revora_trojan_process_probe(stamp timestamptz NOT NULL,reward bigint,loan bigint,encounter uuid); INSERT INTO revora_trojan_process_probe(stamp) VALUES(@stamp)","stamp",new DateTimeOffset(trojanClock));
        System.Console.WriteLine("[trojan-process] trojan-capture-passed");
        // Deliberate private-process interruption with durable earned progress, no graceful scene exit.
        Environment.Exit(0);
    }
    static void TrojanFinish(bool recovering=false) {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Entity.MapID==1002 && TrojanProgress(p,"Stage")== (recovering?12:3) && TrojanProgress(p,"Energy")== (recovering?90:27));
        if(recovering) {
            var loan=p.Inventory.Objects.Single(i=>i.ID==3003340);
            Require(loan.UID==Convert.ToUInt32(Sql("SELECT loan FROM revora_trojan_process_probe")) && loan.Bound);
            Require(p.Inventory.Objects.All(i=>i.ID!=614439));
        }
        int revision=TrojanProgress(p,"Revision");TrojanAction(p,10579,1);
        Require(p.Map.BaseID==3825 && TrojanProgress(p,"Revision")==revision);
        int victories=recovering?19:0;
        for(int guard=0;TrojanProgress(p,"Stage")<14 && guard<200;guard++) {
            int stage=TrojanProgress(p,"Stage");revision=TrojanProgress(p,"Revision");
            uint npc=stage==4 || stage==8?10583u:stage==6?10582u:stage==9?10600u:stage==10?10602u:stage>=11?10603u:10581u;
            if(!p.Map.Npcs.ContainsKey(npc)) {
                uint general=p.Map.Npcs.Keys.Single(id=>id==10581 || id==10600 || id==10603);
                TrojanAction(p,general,3);Require(p.Map.Npcs.ContainsKey(npc));
            }
            TrojanAction(p,npc,1);
            var body=p.Map.Entities.Values.SingleOrDefault(e=>(bool)Call(Feature("TrojanEpicCombat"),"IsEncounter",e));
            if(body!=null && stage==12 && !recovering) {
                Require(victories==19 && TrojanProgress(p,"Energy")==90);
                var loan=p.Inventory.Objects.Single(i=>i.ID==3003340);Require(loan.Bound);
                EntityTable.SaveEntity(p);
                Sql("UPDATE revora_trojan_process_probe SET stamp=@stamp,loan=@loan,encounter=(SELECT id FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='active')",
                    "stamp",new DateTimeOffset(trojanClock),"loan",(long)loan.UID,"uid",(long)p.Entity.UID);
                Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='active' AND stage=12","uid",(long)p.Entity.UID)==1);
                System.Console.WriteLine("[trojan-process] trojan-finish-passed");Environment.Exit(0);
            }
            if(body!=null){EliminationHit(p,body);victories++;}
            int after=TrojanProgress(p,"Revision");
            if(after==revision){Require(stage==4 || stage==6 || stage==9);trojanClock=trojanClock.AddDays(1);}
            else Require(after==revision+1);
        }
        Require(TrojanProgress(p,"Stage")==14 && TrojanProgress(p,"Energy")==90 && victories==20);
        var reward=p.Inventory.Objects.Single(i=>i.ID==614439);
        Require(!reward.Bound && reward.Plus==12 && reward.Bless==7 && reward.SocketOne==Enums.Gem.EmptySocket && reward.SocketTwo==Enums.Gem.EmptySocket);
        Require(!p.Inventory.Objects.Any(i=>i.ID==3003340) && p.Entity.ConquerPoints==0 && p.Entity.Money==100000);
        HeartTalk(p,10603,1);Require(p.Inventory.Objects.Count(i=>i.ID==614439)==1);
        HeartTalk(p,10603,254);Require(p.Entity.MapID==1004);
        Sql("UPDATE revora_trojan_process_probe SET stamp=@stamp,reward=@reward","stamp",new DateTimeOffset(trojanClock),"reward",(long)reward.UID);
        Require(recovering);
        Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_encounters WHERE id=(SELECT encounter FROM revora_trojan_process_probe) AND state='retired'")==1);
        System.Console.WriteLine("[trojan-process] trojan-battle-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Trojan finish process did not close.");
    }
    static void TrojanVerify() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(TrojanProgress(p,"Stage")==14 && TrojanProgress(p,"Completed")==1 && TrojanProgress(p,"Energy")==90);
        var reward=p.Inventory.Objects.Single(i=>i.ID==614439);
        Require(reward.UID==Convert.ToUInt32(Sql("SELECT reward FROM revora_trojan_process_probe")) && !reward.Bound && reward.Plus==12 && reward.Bless==7
            && reward.SocketOne==Enums.Gem.EmptySocket && reward.SocketTwo==Enums.Gem.EmptySocket);
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==100000 && !p.Inventory.Objects.Any(i=>i.ID==3003340));
        Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_energy WHERE uid=@uid","uid",(long)p.Entity.UID)==90);
        Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='victory'","uid",(long)p.Entity.UID)==20);
        Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_rewards WHERE uid=@uid","uid",(long)p.Entity.UID)==1);
        Require((long)Sql("SELECT count(*) FROM revora_trojan_epic_blades WHERE uid=@uid AND state='returned' AND item_uid=(SELECT loan FROM revora_trojan_process_probe)","uid",(long)p.Entity.UID)==1);
        System.Console.WriteLine("[trojan-process] trojan-verify-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Trojan verification process did not close.");
    }
}
