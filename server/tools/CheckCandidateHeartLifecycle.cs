using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime heartNow;
    static void PrepareHeart(string phase) {
        if(phase=="heart-capture") {
            heartNow=DateTime.UtcNow.AddDays(7);
            while(Call(Feature("PlanetWeekend"),"Current",heartNow)==null)heartNow=heartNow.AddDays(1);
        } else heartNow=DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_heart_process_probe"),DateTimeKind.Utc);
        if(phase=="heart-expire" || phase=="heart-expired") {
            heartNow=DateTime.SpecifyKind((DateTime)Sql("SELECT expires_at AT TIME ZONE 'UTC' FROM revora_heart_results WHERE uid=@uid","uid",(long)FirstUid),DateTimeKind.Utc);
            if(phase=="heart-expire")heartNow=heartNow.AddTicks(-1);
        }
        Feature("HeartCampaign").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>heartNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>heartNow));
    }
    static void HeartTalk(GameState p,uint npc,byte option) {
        var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);p.ActiveNpc=npc;
        Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);
    }
    static GameState HeartPlayer() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Call(Feature("HeartEncounter"),"OnLogin",p);return p;
    }
    static void HeartCapture() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=HeartPlayer();
        Require(p.Entity.ConquerPoints==0 && p.Entity.RevoraPoint==0);
        HeartTalk(p,380,160);HeartTalk(p,380,162);HeartTalk(p,380,163);HeartTalk(p,380,164);
        Require(p.Map.BaseID==700 && p.Map.ID!=700);
        EliminationHit(p,p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9926200));
        Require((long)Sql("SELECT count(*) FROM revora_heart_objectives o JOIN revora_heart_runs r ON r.id=o.run WHERE r.uid=@uid","uid",(long)FirstUid)==1);
        Sql("CREATE TABLE revora_heart_process_probe(stamp timestamptz NOT NULL,fingerprint text); INSERT INTO revora_heart_process_probe(stamp) VALUES(@now)","now",new DateTimeOffset(heartNow));
        System.Console.WriteLine("[heart-process] heart-capture-passed");Environment.Exit(0);
    }
    static void HeartFinish() {
        var p=HeartPlayer();Require(p.Entity.MapID==1002);
        bool replay=Sql("SELECT fingerprint FROM revora_heart_process_probe")!=DBNull.Value;
        if(!replay) {
            Require((long)Sql("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='abandoned' AND returned","uid",(long)FirstUid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_heart_objectives o JOIN revora_heart_runs r ON r.id=o.run WHERE r.uid=@uid","uid",(long)FirstUid)==1);
            for(int sortie=0;sortie<3;sortie++) {
                HeartTalk(p,380,160);HeartTalk(p,380,164);Require(p.Map.BaseID==700 && p.Map.ID!=700);
                int kills=0;
                while(kills<11) {
                    var mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9926200 && e.MonsterInfo.ID<=9926210 && !e.Dead);
                    if(mob==null)break;EliminationHit(p,mob);kills++;
                }
                Require(kills==(sortie==0?10:11));
                HeartTalk(p,900000191,0);HeartTalk(p,900000191,1);Require(p.Entity.MapID==1002);
                HeartTalk(p,380,160);HeartTalk(p,380,165);HeartTalk(p,380,165);
                Require((long)Sql("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL","uid",(long)FirstUid)==sortie+1);
            }
            Require((long)Sql("SELECT count(*) FROM revora_heart_results WHERE uid=@uid","uid",(long)FirstUid)==0);
            heartNow=DateTime.SpecifyKind((DateTime)Sql("SELECT w.ends_at AT TIME ZONE 'UTC' FROM revora_heart_weekends w JOIN revora_heart_entries e USING(weekend) WHERE e.uid=@uid","uid",(long)FirstUid),DateTimeKind.Utc);
            Call(Feature("HeartCampaign"),"Tick");
        }
        new TitleStorage().Login(p);
        if(replay)Require(p.Entity.EquippedWing==60186018 && p.Entity.ConquerPoints==5000 && p.Entity.RevoraPoint==1000);
        HeartTalk(p,380,160);HeartTalk(p,380,167);HeartTalk(p,380,167);
        Require(p.Entity.ConquerPoints==5000 && p.Entity.RevoraPoint==1000);
        new TitleStorage{Info=new TitleStorageProto{Type=6018,Id=6018,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=6018,Id=6018}}}.Handle(p);
        Require(p.Entity.EquippedWing==60186018);
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND mask=4095 AND delivered_at IS NOT NULL)=3
AND (SELECT count(*) FROM revora_heart_objectives o JOIN revora_heart_runs r ON r.id=o.run WHERE r.uid=@uid)=36
AND (SELECT count(*) FROM revora_heart_results WHERE uid=@uid AND award_recorded AND support=300 AND cp=5000 AND rvp=1000 AND expires_at=earned_at+interval '720 hours')=1
AND (SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1' AND expires_at=earned_at+interval '720 hours')=1","uid",(long)FirstUid));
        string fingerprint=(string)Sql("SELECT md5(weekend||':'||support::text||':'||earned_at::text||':'||expires_at::text||':'||award_recorded::text) FROM revora_heart_results WHERE uid=@uid","uid",(long)FirstUid);
        if(replay)Require((string)Sql("SELECT fingerprint FROM revora_heart_process_probe")==fingerprint);
        else Sql("UPDATE revora_heart_process_probe SET stamp=@now,fingerprint=@value","now",new DateTimeOffset(heartNow),"value",fingerprint);
        System.Console.WriteLine("[heart-process] heart-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Heart process did not close.");
    }
    static void HeartExpiry(bool boundary) {
        var p=HeartPlayer();new TitleStorage().Login(p);
        Require(p.Entity.EquippedWing==(boundary?60186018:0));
        if(boundary)heartNow=heartNow.AddTicks(1);
        new TitleStorage{Info=new TitleStorageProto{Type=6018,Id=6018,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=6018,Id=6018}}}.Handle(p);
        HeartTalk(p,380,160);HeartTalk(p,380,167);HeartTalk(p,380,167);
        new TitleStorage().Login(p);
        Require(p.Entity.EquippedWing==0 && p.Entity.ConquerPoints==5000 && p.Entity.RevoraPoint==1000);
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1' AND expires_at=@now)=1
AND (SELECT md5(weekend||':'||support::text||':'||earned_at::text||':'||expires_at::text||':'||award_recorded::text) FROM revora_heart_results WHERE uid=@uid)=(SELECT fingerprint FROM revora_heart_process_probe)","uid",(long)FirstUid,"now",new DateTimeOffset(heartNow)));
        System.Console.WriteLine("[heart-process] "+(boundary?"heart-expire":"heart-expired")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Heart expiry process did not close.");
    }
}
