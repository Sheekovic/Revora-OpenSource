using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime fashionNow;
    static void PrepareFashion(string phase) {
        // A February season expires in March before the next monthly judging.
        // Otherwise a legitimate new monthly award could mask the old expiry.
        fashionNow=phase=="fashion-buy"?new DateTime(DateTime.UtcNow.Year+1,2,15,12,0,0,DateTimeKind.Utc):DateTime.SpecifyKind((DateTime)Sql("SELECT closes AT TIME ZONE 'UTC' FROM revora_fashion_process_probe"),DateTimeKind.Utc);
        if(phase=="fashion-expire" || phase=="fashion-expired") {
            fashionNow=fashionNow.AddDays(30);
            if(phase=="fashion-expire")fashionNow=fashionNow.AddTicks(-1);
        }
        Feature("FashionCompetition").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>fashionNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>fashionNow));
    }
    static void FashionBuy() {
        var fashion=Feature("FashionCompetition");
        for(int n=0;n<120 && (bool)Call(fashion,"ProcessDue");n++){}
        DateTime closes=(DateTime)Call(fashion,"NextMonth",fashionNow);
        Require(DateTime.SpecifyKind((DateTime)Sql("SELECT next_close AT TIME ZONE 'UTC' FROM revora_fashion_calendar WHERE id=1"),DateTimeKind.Utc)==closes);
        Sql("CREATE TABLE revora_fashion_process_probe(closes timestamptz NOT NULL,fingerprint text); INSERT INTO revora_fashion_process_probe(closes) VALUES(@close)","close",new DateTimeOffset(closes));
        for(int i=0;i<3;i++)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var daily=Feature("DailyWorldBosses");DateTime now=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
        for(int rank=0;rank<3;rank++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Inventory.Count==0 && p.Entity.RevoraPoint==0);
            now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
            Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
            var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
            p.BindWorldMap(boss.MapID);EliminationHit(p,boss);Require(p.Entity.RevoraPoint==50000);
            p.BindWorldMap(1002);
            for(int design=0;design<3-rank;design++) {
                HeartTalk(p,900000014,(byte)(70+design));Require(p.Inventory.Count==1);
                var item=p.Inventory.Objects.Single();Require(!item.Bound);
                var add=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}};
                add.Handle(p);add.Handle(p);Require(item.InWardrobe && p.Inventory.Count==0);
            }
            Require(p.Entity.RevoraPoint==(uint)(50000-(3-rank)*2500));
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND inwardrobe=1","uid",(long)p.Entity.UID)==3-rank);
        }
        Require((long)Sql("SELECT count(*) FROM revora_fashion_results WHERE uid BETWEEN @first AND @last","first",(long)FirstUid,"last",(long)FirstUid+2)==0);
        System.Console.WriteLine("[fashion-process] fashion-buy-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fashion purchase process did not close.");
    }
    static void FashionExpiry(bool boundary) {
        Require((bool)Sql("SELECT next_close>@now FROM revora_fashion_calendar WHERE id=1","now",new DateTimeOffset(boundary?fashionNow.AddTicks(1):fashionNow)));
        for(int rank=0;rank<3;rank++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();new TitleStorage().Login(p);
            int key=20522052+rank*10001;
            Require(p.Entity.EquippedTitle==(boundary?key:0));
            var cp=p.Entity.ConquerPoints;var gold=p.Entity.Money;
            if(boundary)fashionNow=fashionNow.AddTicks(1);
            for(int replay=0;replay<2;replay++) {
                Call(Feature("FashionCompetition"),"Tick");
                new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
                new TitleStorage().Login(p);
                Require(p.Entity.EquippedTitle==0 && p.Entity.RevoraPoint==(uint)(50000-(3-rank)*2500)
                    && p.Entity.ConquerPoints==cp && p.Entity.Money==gold);
            }
            if(boundary)fashionNow=fashionNow.AddTicks(-1);
        }
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND source='fashion-revora-v1'","first",(long)FirstUid,"last",(long)FirstUid+2)==3);
        Require((string)Sql("SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text||':'||expires_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND source='fashion-revora-v1'","first",(long)FirstUid,"last",(long)FirstUid+2)==(string)Sql("SELECT fingerprint FROM revora_fashion_process_probe"));
        if(boundary)fashionNow=fashionNow.AddTicks(1);
        System.Console.WriteLine("[fashion-process] "+(boundary?"fashion-expire":"fashion-expired")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Fashion expiry process did not close.");
    }
    static void FashionVerify() {
        Call(Feature("FashionCompetition"),"Tick");
        bool replay=Sql("SELECT fingerprint FROM revora_fashion_process_probe")!=DBNull.Value;
        for(int rank=0;rank<3;rank++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();new TitleStorage().Login(p);
            int key=20522052+rank*10001;
            Require((long)Sql("SELECT placement FROM revora_fashion_results r JOIN revora_fashion_seasons s USING(season) WHERE r.uid=@uid AND s.closed_at=@close","uid",(long)p.Entity.UID,"close",new DateTimeOffset(fashionNow))==rank+1);
            if(replay)Require(p.Entity.EquippedTitle==key);
            new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
            Require(p.Entity.EquippedTitle==key && p.Entity.RevoraPoint==(uint)(50000-(3-rank)*2500));
        }
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND source='fashion-revora-v1' AND earned_at=@close AND expires_at=@expires","first",(long)FirstUid,"last",(long)FirstUid+2,"close",new DateTimeOffset(fashionNow),"expires",new DateTimeOffset(fashionNow.AddDays(30)))==3);
        string fingerprint=(string)Sql("SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text||':'||expires_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND source='fashion-revora-v1'","first",(long)FirstUid,"last",(long)FirstUid+2);
        if(replay)Require((string)Sql("SELECT fingerprint FROM revora_fashion_process_probe")==fingerprint);
        else Sql("UPDATE revora_fashion_process_probe SET fingerprint=@value","value",fingerprint);
        System.Console.WriteLine("[fashion-process] fashion-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fashion verification did not close.");
    }
}
