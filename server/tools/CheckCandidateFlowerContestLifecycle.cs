using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime flowerContestNow;
    static void PrepareFlowerContest(string phase) {
        flowerContestNow=phase=="flower-grow"?new DateTime(DateTime.UtcNow.Year+1,1,15,19,0,0,DateTimeKind.Utc):
            DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_flower_process_probe"),DateTimeKind.Utc);
        foreach(string name in new[]{"MagnoliaQuest","MrMs2017"})Feature(name).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerContestNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerContestNow));
        Feature("MagnoliaQuest").GetField("SeedRoll",Hidden).SetValue(null,(Func<int,int>)(max=>80));
        Feature("MagnoliaQuest").GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?0:1));
    }
    static void FlowerGrow() {
        Player(Kernel.Maps[1002]);
        for(int category=0;category<2;category++)for(int rank=0;rank<3;rank++)Player(Kernel.Maps[1002],false,(ushort)(category==0?2001:1003));
        nextUid=FirstUid;
        var donor=Player(Kernel.Maps[1002],true);donor.LoadItemStats();
        Require(donor.Entity.ConquerPoints==0);
        for(int day=0;day<7;day++) {
            if(day>0)flowerContestNow=flowerContestNow.AddDays(1);
            donor.BindWorldMap(1002);HeartTalk(donor,79882,5);Require(donor.Entity.MapID==1806);
            HeartTalk(donor,44100,1);HeartTalk(donor,44100,3);
            var seed=donor.Inventory.Objects.Single(i=>i.ID==729306);
            if(day>0) {
                uint cp=donor.Entity.ConquerPoints;
                HeartTalk(donor,44100,5);HeartTalk(donor,44100,6);
                seed=donor.Inventory.Objects.Single(i=>i.ID==729308);
                Require(!seed.Bound && donor.Entity.ConquerPoints==cp-1);
            }
            donor.Entity.X=98;donor.Entity.Y=111;ItemHandler.UseItem(seed,donor);
            Require((long)Sql("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid AND completed AND NOT legacy","uid",(long)FirstUid)==day+1);
            Require(!donor.Inventory.ContainsUID(seed.UID));
            HeartTalk(donor,44100,2);Require(donor.Entity.MapID==1002);
        }
        Require(donor.Inventory.Objects.Where(i=>i.ID==753003 && !i.Bound).Sum(i=>(int)i.StackSize)==6);
        Require(EntityTable.SaveEntity(donor));
        Sql(@"CREATE TABLE revora_flower_process_probe(stamp timestamptz NOT NULL,season text NOT NULL,cp bigint NOT NULL,fingerprint text);
INSERT INTO revora_flower_process_probe(stamp,season,cp) VALUES(@stamp,@season,@cp)","stamp",new DateTimeOffset(flowerContestNow),"season",(string)Call(Feature("MrMs2017"),"SeasonOf",flowerContestNow),"cp",(long)donor.Entity.ConquerPoints);
        System.Console.WriteLine("[flower-process] flower-grow-passed");Environment.Exit(0);
    }
    static void FlowerDonate() {
        var donor=Player(Kernel.Maps[1002],true);donor.LoadItemStats();
        Require(donor.Entity.ConquerPoints==(long)Sql("SELECT cp FROM revora_flower_process_probe"));
        Require(donor.Inventory.Objects.Where(i=>i.ID==753003 && !i.Bound).Sum(i=>(int)i.StackSize)==6);
        for(int category=0;category<2;category++)for(int rank=0;rank<3;rank++) {
            var winner=Player(Kernel.Maps[1002],true);winner.LoadItemStats();
            HeartTalk(winner,380,240);HeartTalk(winner,380,241);
            HeartTalk(donor,380,240);HeartTalk(donor,380,category==0?(byte)244:(byte)243);
            HeartTalk(donor,380,(byte)(180+rank));HeartTalk(donor,380,190);HeartTalk(donor,380,247);HeartTalk(donor,380,247);
            Require(Convert.ToInt64(Sql("SELECT sum(points) FROM revora_mrms2017_donations WHERE candidate=@uid","uid",(long)winner.Entity.UID))==3);
        }
        Require(!donor.Inventory.Objects.Any(i=>i.ID==753003));
        Require((long)Sql("SELECT count(*) FROM revora_mrms2017_results WHERE season=@season","season",(string)Sql("SELECT season FROM revora_flower_process_probe"))==0);
        Require(EntityTable.SaveEntity(donor));
        var close=(DateTime)Call(Feature("MrMs2017"),"CloseOf",(string)Sql("SELECT season FROM revora_flower_process_probe"));
        Sql("UPDATE revora_flower_process_probe SET stamp=@stamp","stamp",new DateTimeOffset(close));
        System.Console.WriteLine("[flower-process] flower-donate-passed");Environment.Exit(0);
    }
    static void FlowerVerify() {
        var donor=Player(Kernel.Maps[1002],true);donor.LoadItemStats();
        Require(!donor.Inventory.Objects.Any(i=>i.ID==753003) && donor.Entity.ConquerPoints==(long)Sql("SELECT cp FROM revora_flower_process_probe"));
        Call(Feature("MrMs2017"),"ProcessDue");
        bool replay=Sql("SELECT fingerprint FROM revora_flower_process_probe")!=DBNull.Value;
        for(int category=0;category<2;category++)for(int rank=0;rank<3;rank++) {
            var winner=Player(Kernel.Maps[1002],true);winner.LoadItemStats();new TitleStorage().Login(winner);
            int champion=category==0?20402040:20412041,local=category==0?20442044:20452045;
            Require(Convert.ToInt32(Sql("SELECT placement FROM revora_mrms2017_results WHERE uid=@uid","uid",(long)winner.Entity.UID))==rank+1);
            if(replay)Require(winner.Entity.EquippedWing==60126012 && winner.Entity.EquippedTitle==(rank==0?champion:local));
            foreach(int key in rank==0?new[]{local,champion,60126012}:new[]{local,60126012}) {
                new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(winner);
                Require(key==60126012?winner.Entity.EquippedWing==key:winner.Entity.EquippedTitle==key);
            }
            Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source=@source AND expires_at IS NOT DISTINCT FROM @expiry",
                "uid",(long)winner.Entity.UID,"source","mrms-2017-wing-"+(rank+1)+"-v1",
                "expiry",rank==0?(object)DBNull.Value:new DateTimeOffset(flowerContestNow.AddDays(rank==1?360:180)))==1);
            if(rank>0)Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='mrms-2017-champion-v1'","uid",(long)winner.Entity.UID)==0);
        }
        Require((long)Sql(@"SELECT count(*) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND earned_at=@close
AND ((source='mrms-2017-local-v1' AND expires_at=earned_at+interval '8640 hours')
OR (source='mrms-2017-champion-v1' AND expires_at=earned_at+interval '8760 hours')
OR (source='mrms-2017-wing-1-v1' AND expires_at IS NULL)
OR (source='mrms-2017-wing-2-v1' AND expires_at=earned_at+interval '8640 hours')
OR (source='mrms-2017-wing-3-v1' AND expires_at=earned_at+interval '4320 hours'))","first",(long)FirstUid+1,"last",(long)FirstUid+6,"close",new DateTimeOffset(flowerContestNow))==14);
        string fingerprint=(string)Sql("SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text||':'||coalesce(expires_at::text,'permanent'),',' ORDER BY uid,title_key)) FROM revora_event_awards WHERE uid BETWEEN @first AND @last AND source LIKE 'mrms-2017%'","first",(long)FirstUid+1,"last",(long)FirstUid+6);
        Require(fingerprint!=null);
        if(replay)Require(fingerprint==(string)Sql("SELECT fingerprint FROM revora_flower_process_probe"));
        else Sql("UPDATE revora_flower_process_probe SET fingerprint=@value","value",fingerprint);
        System.Console.WriteLine("[flower-process] flower-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Flower contest did not close.");
    }
}
