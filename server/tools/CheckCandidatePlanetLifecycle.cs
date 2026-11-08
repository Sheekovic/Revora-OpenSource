using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static string PlanetCollectionSnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'runs',(SELECT json_agg(r ORDER BY weekend) FROM revora_planet_runs r WHERE uid=@uid),
'completions',(SELECT json_agg(r ORDER BY weekend) FROM revora_planet_completions r WHERE uid=@uid),
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,inwardrobe FROM items WHERE entityid=@uid) r))::text","uid",(long)FirstUid));
    }
    static DateTime planetNow;
    static void PreparePlanet(string phase) {
        if(phase=="planet-capture") {
            planetNow=DateTime.UtcNow.AddDays(7);
            while(Call(Feature("PlanetWeekend"),"Current",planetNow)==null)planetNow=planetNow.AddDays(1);
        } else planetNow=DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_planet_process_probe"),DateTimeKind.Utc);
        if(phase=="planet-expire" || phase=="planet-expired") {
            planetNow=DateTime.SpecifyKind((DateTime)Sql("SELECT expires_at AT TIME ZONE 'UTC' FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'","uid",(long)FirstUid),DateTimeKind.Utc);
            if(phase=="planet-expire")planetNow=planetNow.AddTicks(-1);
        }
        foreach(string name in new[]{"PlanetWeekend","SummerHunt"})Feature(name).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
    }
    static GameState PlanetProcessPlayer() {
        var p=Player(Kernel.Maps[1002],true);new CoatStorage().Login(p);p.LoadItemStats();
        Call(Feature("PlanetEncounter"),"OnLogin",p);Call(Feature("PlanetWeekend"),"OnLogin",p);return p;
    }
    static void PlanetRowan(GameState p,byte option) {p.BindWorldMap(1002);HeartTalk(p,900000016,option);}
    static void PlanetBirdmen(GameState p,int count) {
        p.BindWorldMap(1015);
        var birds=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==18 && !e.Dead).Take(count).ToArray();
        Require(birds.Length==count);
        foreach(var bird in birds)EliminationHit(p,bird);
    }
    static void PlanetCapture() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=PlanetProcessPlayer();
        PlanetRowan(p,211);PlanetBirdmen(p,10);
        Require(Convert.ToInt64(Sql("SELECT sum(kills) FROM revora_planet_runs WHERE uid=@uid","uid",(long)FirstUid))==10);
        Sql("CREATE TABLE revora_planet_process_probe(stamp timestamptz NOT NULL,fingerprint text); INSERT INTO revora_planet_process_probe(stamp) VALUES(@now)","now",new DateTimeOffset(planetNow));
        System.Console.WriteLine("[planet-process] planet-capture-passed");Environment.Exit(0);
    }
    static void PlanetWeek() {
        var p=PlanetProcessPlayer();int before=(int)(long)Sql("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)FirstUid);
        Require(before>=0 && before<8);
        long kills=Convert.ToInt64(Sql("SELECT coalesce(sum(kills),0) FROM revora_planet_runs WHERE uid=@uid","uid",(long)FirstUid));
        Require(kills==(before==0?10:before*100));
        PlanetRowan(p,211);PlanetBirdmen(p,before==0?90:100);PlanetRowan(p,212);
        System.Console.WriteLine("[planet-process] admission completed="+before+" map="+p.Entity.MapID+" hp="+p.Entity.Hitpoints+" kills="+Sql("SELECT coalesce(sum(kills),0) FROM revora_planet_runs WHERE uid=@uid","uid",(long)FirstUid)+" runs="+Sql("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid","uid",(long)FirstUid));
        Require(p.Map.BaseID==700 && p.Map.ID!=700);
        var guards=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9926100 && e.MonsterInfo.ID<=9926102).ToArray();
        Require(guards.Length==3);foreach(var guard in guards)EliminationHit(p,guard);
        HeartTalk(p,900000190,0);HeartTalk(p,900000190,1);Require(p.Entity.MapID==1002);
        new TitleStorage().Login(p);PlanetRowan(p,210);
        string prior=PlanetCollectionSnapshot();
        if(before==0) {
            // Capacity controls are supplied, not expedition rewards.
            for(int n=0;n<37;n++)Require(p.Inventory.Add(611004,0,1));
            string full=PlanetCollectionSnapshot();PlanetRowan(p,213);
            Require(PlanetCollectionSnapshot()==full && p.Inventory.Count==37);
            foreach(var filler in p.Inventory.Objects.ToArray())Require(p.Inventory.Remove(filler,Enums.ItemUse.Remove));
            Require(PlanetCollectionSnapshot()==prior);
            Sql(@"CREATE FUNCTION revora_planet_gift_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'planet gift probe'; END $$;
CREATE TRIGGER revora_planet_gift_fail BEFORE INSERT ON items FOR EACH ROW WHEN(NEW.id=200595) EXECUTE FUNCTION revora_planet_gift_fail();");
            bool failed=false;try{Call(Feature("PlanetWeekend"),"Deliver",p);}catch{failed=true;}
            Sql("DROP TRIGGER revora_planet_gift_fail ON items; DROP FUNCTION revora_planet_gift_fail()");
            Require(failed && PlanetCollectionSnapshot()==prior && p.Inventory.Count==0);
        }
        PlanetRowan(p,213);string earned=PlanetCollectionSnapshot();PlanetRowan(p,213);Require(earned==PlanetCollectionSnapshot());
        foreach(var gift in p.Inventory.Objects.ToArray()) {
            Require(gift.Bound && gift.Days==0 && gift.TimeLeftInMinutes==0);
            var add=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=gift.UID,ItemId=(int)gift.ID}};
            add.Handle(p);Require(gift.InWardrobe);
        }
        Require(p.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)FirstUid)==before+1);
        Require((long)Sql("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory' AND defeated=7 AND returned","uid",(long)FirstUid)==before+1);
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'","uid",(long)FirstUid)==(before>=3?1:0));
        if(before==3) {
            new TitleStorage{Info=new TitleStorageProto{Type=6009,Id=6009,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=6009,Id=6009}}}.Handle(p);
            Require(p.Entity.EquippedWing==60096009);
            Sql("UPDATE revora_planet_process_probe SET fingerprint=(SELECT md5(earned_at::text||':'||expires_at::text||':'||source_key) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1')","uid",(long)FirstUid);
        }
        if(before<7)planetNow=planetNow.AddDays(14);
        Sql("UPDATE revora_planet_process_probe SET stamp=@now","now",new DateTimeOffset(planetNow));
        System.Console.WriteLine("[planet-process] planet-week-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Planet process did not close.");
    }
    static void PlanetExpiry(bool boundary) {
        var p=PlanetProcessPlayer();new TitleStorage().Login(p);
        if(boundary)new TitleStorage{Info=new TitleStorageProto{Type=6009,Id=6009,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=6009,Id=6009}}}.Handle(p);
        Require(p.Entity.EquippedWing==(boundary?60096009:0));
        var cp=p.Entity.ConquerPoints;var rvp=p.Entity.RevoraPoint;var gold=p.Entity.Money;
        if(boundary)planetNow=planetNow.AddTicks(1);
        for(int replay=0;replay<2;replay++) {
            new TitleStorage{Info=new TitleStorageProto{Type=6009,Id=6009,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=6009,Id=6009}}}.Handle(p);
            PlanetRowan(p,213);new TitleStorage().Login(p);
            Require(p.Entity.EquippedWing==0 && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==gold);
        }
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_planet_completions WHERE uid=@uid)=8
AND (SELECT sum(kills) FROM revora_planet_runs WHERE uid=@uid)=800
AND (SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1' AND expires_at=@now)=1
AND (SELECT md5(earned_at::text||':'||expires_at::text||':'||source_key) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1')=(SELECT fingerprint FROM revora_planet_process_probe)","uid",(long)FirstUid,"now",new DateTimeOffset(planetNow)));
        System.Console.WriteLine("[planet-process] "+(boundary?"planet-expire":"planet-expired")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Planet expiry process did not close.");
    }
    static void PlanetVerify() {
        var p=PlanetProcessPlayer();new TitleStorage().Login(p);Require(p.Entity.EquippedWing==0);
        Require((long)Sql("SELECT count(DISTINCT id) FROM items WHERE entityid=@uid AND inwardrobe=1 AND bound=1 AND COALESCE(NULLIF(days,''),'0')='0'","uid",(long)FirstUid)==19);
        Require((long)Sql("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='complete' AND collection_step BETWEEN 1 AND 8 AND cardinality(reward_uids)>0","uid",(long)FirstUid)==8);
        Require((long)Sql("SELECT count(*) FROM revora_planet_runs r CROSS JOIN LATERAL unnest(r.reward_uids) u JOIN items i ON i.uid=u AND i.entityid=r.uid WHERE r.uid=@uid AND i.inwardrobe=1 AND i.bound=1","uid",(long)FirstUid)==19);
        foreach(int key in new[]{20160017,20332033,20502050,20562056}) {
            new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
            Require(p.Entity.EquippedTitle==key);
        }
        string before=PlanetCollectionSnapshot();
        PlanetRowan(p,211);PlanetRowan(p,213);
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_planet_completions WHERE uid=@uid)=8
AND (SELECT sum(kills) FROM revora_planet_runs WHERE uid=@uid)=800
AND (SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1' AND expires_at=earned_at+interval '720 hours')=1
AND (SELECT md5(earned_at::text||':'||expires_at::text||':'||source_key) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1')=(SELECT fingerprint FROM revora_planet_process_probe)","uid",(long)FirstUid));
        Require(PlanetCollectionSnapshot()==before);
        System.Console.WriteLine("[planet-process] planet-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Planet verify did not close.");
    }
}
