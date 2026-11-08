using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool enlightMidnightFixture;
    static void EnlightMidnight(bool reload){
        enlightMidnightFixture=true;const uint uid=4294913601;var feature=Feature("EnlightenmentProgress");DateTime now=DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59).ToUniversalTime();
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("ExperienceConsumables").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now.ToLocalTime()));
        if((bool)Sql("SELECT to_regclass('revora_enlight_midnight_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=0,expballs=0,enlightenpoints=0,enlightsreceived=0,enlightmentwait=0 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_enlight_midnight_probe(snapshot text,clock timestamptz NOT NULL)");
        }
        bool finished=(long)Sql("SELECT count(*) FROM revora_enlight_midnight_probe")!=0;
        if(finished)now=DateTime.SpecifyKind((DateTime)Sql("SELECT clock AT TIME ZONE 'UTC' FROM revora_enlight_midnight_probe"),DateTimeKind.Utc);
        bool resume=!finished && (long)Sql("SELECT count(*) FROM revora_exp_ball_days WHERE uid=@uid AND used=10","uid",(long)uid)==1;
        if(resume)now=now.AddSeconds(1);
        var p=EnlightClaimPlayer(uid);
        if(!finished){
            Require(!reload);ConquerItem ball;string before;
            if(!resume){
                for(int i=0;i<10;i++)SoulClaimUse(p,RefineryClaimGive(p,723700));
                ball=RefineryClaimGive(p,723700);before=EnlightCreditSnapshot(p);
                RetainedUpgradeUsage(p,ball.UID,0,ItemUsage.Mode.EquipItem);Require(before==EnlightCreditSnapshot(p));now=now.AddSeconds(1);
            }else{ball=p.Inventory.Objects.Single(i=>i.ID==723700);Require(p.Entity.Level==120 && p.Entity.Experience==10*p.ExpBall);}
            ulong xp=p.Entity.Experience;byte level=p.Entity.Level;
            feature.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected midnight consumption rollback");}));
            try{RetainedUpgradeUsage(p,ball.UID,0,ItemUsage.Mode.EquipItem);}finally{feature.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(p));p=EnlightClaimPlayer(uid);
            ushort daily=(ushort)Call(feature,"DailyPoints",p);
            Require(p.Entity.EnlightenPoints==daily && p.Entity.Experience==xp && p.Entity.Level==level && p.Inventory.ContainsUID(ball.UID));
            Require(Convert.ToInt32(Sql("SELECT used FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)uid))==10);
            SoulClaimUse(p,p.Inventory.Objects.Single(i=>i.UID==ball.UID));Require(p.ExpBalls==1 && p.Entity.EnlightenPoints==daily+10);
            before=EnlightCreditSnapshot(p);RetainedUpgradeUsage(p,ball.UID,0,ItemUsage.Mode.EquipItem);Require(before==EnlightCreditSnapshot(p));
            Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_enlight_midnight_probe VALUES(@snapshot,@now)","snapshot",EnlightCreditSnapshot(p),"now",now);
        }
        Require(EnlightCreditSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_midnight_probe")));
        Require(p.ExpBalls==1 && p.Entity.EnlightenPoints==(ushort)Call(feature,"DailyPoints",p)+10);
        System.Console.WriteLine("[career-process] career-enlight-midnight"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight midnight did not close.");
    }
    static bool enlightCreditFixture;
    static string EnlightCreditSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'ball_day',(SELECT row_to_json(d) FROM revora_exp_ball_days d WHERE uid=@uid),
'point_day',(SELECT row_to_json(d) FROM revora_enlight_days d WHERE uid=@uid))::text","base",EnlightClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void EnlightCreditSupply(GameState p,ushort points){lock(p.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
        Call(Feature("EnlightenmentBalance"),"Persist",c,t,p,points,p.Entity.ReceivedEnlightenPoints,p.Entity.EnlightmentTime);t.Commit();
    }}Call(Feature("EnlightenmentBalance"),"ApplyCommitted",p,points,p.Entity.ReceivedEnlightenPoints,p.Entity.EnlightmentTime);}}
    static void EnlightCreditEdges(bool reload){
        enlightCreditFixture=true;const uint uid=4294913501;var feature=Feature("EnlightenmentProgress");DateTime now=DateTime.Today.AddHours(12).ToUniversalTime();
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("ExperienceConsumables").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now.ToLocalTime()));
        if((bool)Sql("SELECT to_regclass('revora_enlight_credit_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=0,expballs=0,enlightenpoints=0,enlightsreceived=0,enlightmentwait=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
            Sql("CREATE TABLE revora_enlight_credit_probe(uid bigint PRIMARY KEY,snapshot text,clock timestamptz NOT NULL)");
        }
        bool finished=(long)Sql("SELECT count(*) FROM revora_enlight_credit_probe")!=0;
        if(finished)now=DateTime.SpecifyKind((DateTime)Sql("SELECT clock AT TIME ZONE 'UTC' FROM revora_enlight_credit_probe ORDER BY uid LIMIT 1"),DateTimeKind.Utc);
        var p=EnlightClaimPlayer(uid);
        if(!finished){
            Require(!reload && p.ExpBalls==0);ushort points=p.Entity.EnlightenPoints;
            for(int i=0;i<10;i++){SoulClaimUse(p,RefineryClaimGive(p,723700));Require(p.ExpBalls==i+1 && p.Entity.EnlightenPoints==points+10*(i+1));}
            var eleventh=RefineryClaimGive(p,723700);string before=EnlightCreditSnapshot(p);
            RetainedUpgradeUsage(p,eleventh.UID,0,ItemUsage.Mode.EquipItem);Require(before==EnlightCreditSnapshot(p) && p.Inventory.ContainsUID(eleventh.UID) && !PirateFenced(p));
            now=now.AddDays(1);Call(feature,"Tick",p);ushort daily=(ushort)Call(feature,"DailyPoints",p);Require(p.Entity.EnlightenPoints==daily);
            before=EnlightCreditSnapshot(p);
            feature.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected next-day allowance rollback");}));
            try{RetainedUpgradeUsage(p,eleventh.UID,0,ItemUsage.Mode.EquipItem);}finally{feature.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(p) && before==EnlightCreditSnapshot(p));p=EnlightClaimPlayer(uid);Require(p.Inventory.ContainsUID(eleventh.UID));
            SoulClaimUse(p,p.Inventory.Objects.Single(i=>i.UID==eleventh.UID));Require(p.ExpBalls==1 && p.Entity.EnlightenPoints==daily+10);
            var ceiling=EnlightClaimPlayer(uid+1);EnlightCreditSupply(ceiling,65525);
            SoulClaimUse(ceiling,RefineryClaimGive(ceiling,723700));Require(ceiling.Entity.EnlightenPoints==65535);
            var overflow=RefineryClaimGive(ceiling,723700);before=EnlightCreditSnapshot(ceiling);
            RetainedUpgradeUsage(ceiling,overflow.UID,0,ItemUsage.Mode.EquipItem);
            Require(PirateFenced(ceiling) && before==EnlightCreditSnapshot(ceiling));ceiling=EnlightClaimPlayer(uid+1);
            Require(ceiling.Entity.EnlightenPoints==65535 && ceiling.Inventory.ContainsUID(overflow.UID));
            foreach(var player in new[]{p,ceiling}){Require(EntityTable.SaveEntity(player));Sql("INSERT INTO revora_enlight_credit_probe VALUES(@uid,@snapshot,@now)","uid",(long)player.Entity.UID,"snapshot",EnlightCreditSnapshot(player),"now",now);}
        }
        for(uint id=uid;id<=uid+1;id++){var player=EnlightClaimPlayer(id);Require(EnlightCreditSnapshot(player)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_credit_probe WHERE uid=@uid","uid",(long)id)));}
        System.Console.WriteLine("[career-process] career-enlight-credit-edges"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight credit edges did not close.");
    }
    static bool enlightClaimFixture;
    static string EnlightClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'progress',(SELECT json_build_array(level,experience,reborn,expballs,doubleexptime) FROM entities WHERE uid=@uid),
'awards',(SELECT json_agg(a ORDER BY id) FROM revora_enlight_awards a WHERE a.donor=@uid OR a.recipient=@uid),
'potions',(SELECT json_agg(a ORDER BY id) FROM revora_exp_potion_uses a WHERE a.uid=@uid))::text","base",EnlightBalanceSnapshot(p),"uid",(long)p.Entity.UID));}
    static GameState EnlightClaimPlayer(uint uid){var p=EnlightEdgePlayer(uid);ChiTable.Load(p);WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player);return p;}
    static void EnlightClaimProcess(bool reload){
        enlightClaimFixture=true;const uint uid=4294913301;var feature=Feature("EnlightenmentProgress");
        DateTime now=DateTime.UtcNow;feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_enlight_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+3)==0);
            nextUid=uid;for(int i=0;i<4;i++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=CASE WHEN uid=@b THEN 90 ELSE 120 END,reborn=CASE WHEN uid=@b THEN 0 ELSE 1 END,enlightenpoints=0,enlightsreceived=0,enlightmentwait=0 WHERE uid BETWEEN @a AND @last","a",(long)uid,"b",(long)uid+1,"last",(long)uid+3);
            Sql("CREATE TABLE revora_enlight_claim_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        var donor=EnlightClaimPlayer(uid);var recipient=EnlightClaimPlayer(uid+1);
        if((long)Sql("SELECT count(*) FROM revora_enlight_claim_probe")==0){
            Require(!reload && donor.Inventory.Count==0);
            // Supplied fixture prerequisite: spendable balance starts at zero.
            lock(donor.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Call(Feature("EnlightenmentBalance"),"Persist",c,t,donor,(ushort)0,(byte)0,(ushort)0);t.Commit();
            }}Call(Feature("EnlightenmentBalance"),"ApplyCommitted",donor,(ushort)0,(byte)0,(ushort)0);}
            string before=EnlightClaimSnapshot(donor);NobilityClaimPacket(donor,606);Require(before==EnlightClaimSnapshot(donor));
            Enlight.Process(EnlightEdgeFrame(donor,recipient,761000),donor);Require(recipient.Entity.ReceivedEnlightenPoints==0);
            var potion=RefineryClaimGive(donor,723017);SoulClaimUse(donor,potion);Require(donor.Entity.EnlightenPoints==10 && donor.Entity.DoubleExperienceTime==3600);
            var refused=RefineryClaimGive(donor,722700);before=EnlightClaimSnapshot(donor);
            RetainedUpgradeUsage(donor,refused.UID,0,ItemUsage.Mode.EquipItem);Require(before==EnlightClaimSnapshot(donor));
            for(int i=0;i<9;i++){var ball=RefineryClaimGive(donor,723700);SoulClaimUse(donor,ball);Require(donor.Entity.EnlightenPoints==20+10*i);}
            Require(donor.Entity.EnlightenPoints==100);
            before=EnlightClaimSnapshot(donor);NobilityClaimPacket(donor,606);Require(before==EnlightClaimSnapshot(donor));
            recipient.Entity.InAutoHunt=true;ulong autoBefore=recipient.Entity.AutoHuntEXP;ulong award=recipient.ExpBall/10;
            var frame=EnlightEdgeFrame(donor,recipient,761001);Enlight.Process(frame,donor);
            Require(!PirateFenced(donor) && !PirateFenced(recipient) && donor.Entity.EnlightenPoints==0 && recipient.Entity.ReceivedEnlightenPoints==1);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(donor,606));
            var meteors=donor.Inventory.Objects.Where(i=>i.ID==1088001).ToArray();Require(meteors.Length==3 && meteors.All(i=>i.Bound && i.StackSize==1));
            before=EnlightClaimSnapshot(donor);NobilityClaimPacket(donor,606);Enlight.Process(frame,donor);Require(before==EnlightClaimSnapshot(donor));
            uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==9 && i.Level>=15).OrderBy(i=>i.Level).First().ID;
            var boots=RefineryClaimGive(donor,type);var upgrades=Feature("EquipmentUpgrades");var roll=upgrades.GetField("Roll",Hidden);var clock=upgrades.GetField("Clock",Hidden);
            object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime upgradeNow=DateTime.UtcNow;
            try{roll.SetValue(null,(Func<int,int>)(max=>0));clock.SetValue(null,(Func<DateTime>)(()=>upgradeNow));foreach(var meteor in meteors){MountClaimUpgrade(donor,boots,meteor);upgradeNow=upgradeNow.AddSeconds(2);}}
            finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            ulong immediate=recipient.Entity.Experience;
            for(int i=0;i<20;i++){now=now.AddMinutes(1);Call(feature,"Tick",recipient);}
            Require(!PirateFenced(recipient) && recipient.Entity.EnlightmentTime==0 && recipient.Entity.Experience>immediate);
            Require(recipient.Entity.AutoHuntEXP==autoBefore+2*(award/Math.Max(1u,Config.AutoHuntExperienceDivisor)));recipient.Entity.InAutoHunt=false;
            // Rollback preserves the consumable, XP and points together.
            var rollback=EnlightClaimPlayer(uid+2);var rollbackBall=RefineryClaimGive(rollback,723700);before=EnlightClaimSnapshot(rollback);
            feature.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected point-credit rollback");}));
            try{RetainedUpgradeUsage(rollback,rollbackBall.UID,0,ItemUsage.Mode.EquipItem);}finally{feature.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(rollback) && before==EnlightClaimSnapshot(rollback));
            rollback=EnlightClaimPlayer(uid+2);Require(rollback.Inventory.ContainsUID(rollbackBall.UID));
            // A committed potion credit must survive failure before runtime installation.
            var interrupted=EnlightClaimPlayer(uid+3);var interruptedPotion=RefineryClaimGive(interrupted,723017);ushort points=interrupted.Entity.EnlightenPoints;
            feature.GetField("BeforeInstall",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected point-credit install interruption");}));
            try{RetainedUpgradeUsage(interrupted,interruptedPotion.UID,0,ItemUsage.Mode.EquipItem);}finally{feature.GetField("BeforeInstall",Hidden).SetValue(null,null);}
            Require(PirateFenced(interrupted) && !EntityTable.SaveEntity(interrupted));interrupted=EnlightClaimPlayer(uid+3);
            Require(interrupted.Entity.EnlightenPoints==points+10 && interrupted.Entity.DoubleExperienceTime==3600 && !interrupted.Inventory.ContainsUID(interruptedPotion.UID));
            before=EnlightClaimSnapshot(interrupted);RetainedUpgradeUsage(interrupted,interruptedPotion.UID,0,ItemUsage.Mode.EquipItem);Require(before==EnlightClaimSnapshot(interrupted));
            foreach(var p in new[]{donor,recipient,rollback,interrupted}){Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_enlight_claim_probe VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",EnlightClaimSnapshot(p));}
        }
        for(uint id=uid;id<=uid+3;id++){var p=EnlightClaimPlayer(id);Require(EnlightClaimSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_claim_probe WHERE uid=@uid","uid",(long)id)));}
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=606 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success","uid",(long)uid)==3);
        donor=EnlightClaimPlayer(uid);
        string exact=EnlightClaimSnapshot(donor);NobilityClaimPacket(donor,606);Require(exact==EnlightClaimSnapshot(donor));
        System.Console.WriteLine("[career-process] career-enlight-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight claim did not close.");
    }
}
