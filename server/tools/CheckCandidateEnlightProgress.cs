using System;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool enlightDayFixture;
    static void EnlightDayProcess(bool reload){
        enlightDayFixture=true;const uint uid=4294913201;var feature=Feature("EnlightenmentProgress");
        DateTime now=DateTime.Today.AddHours(23).AddMinutes(59).ToUniversalTime();if(reload)now=now.AddMinutes(22);
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_enlight_day_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=CASE WHEN uid=@a THEN 130 ELSE 100 END,reborn=0,enlightenpoints=800,enlightsreceived=0,enlightmentwait=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
            Sql("CREATE TABLE revora_enlight_day_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        var donor=EnlightEdgePlayer(uid);var recipient=EnlightEdgePlayer(uid+1);
        if((long)Sql("SELECT count(*) FROM revora_enlight_day_probe")==0){
            Require(!reload);ushort pointsBefore=donor.Entity.EnlightenPoints;
            bool alreadyAccepted=(long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND nonce=751001","uid",(long)uid)!=0;
            Enlight.Process(EnlightEdgeFrame(donor,recipient,751001),donor);
            Require(donor.Entity.EnlightenPoints==pointsBefore-(alreadyAccepted?0:100));
            System.Console.WriteLine("[enlight-day] points="+donor.Entity.EnlightenPoints+" count="+recipient.Entity.ReceivedEnlightenPoints+" wait="+recipient.Entity.EnlightmentTime+" donorFenced="+PirateFenced(donor)+" targetFenced="+PirateFenced(recipient));
            Require(recipient.Entity.ReceivedEnlightenPoints==1 && recipient.Entity.EnlightmentTime==20);
            now=now.AddMinutes(1);Call(feature,"Tick",donor);Call(feature,"Tick",recipient);
            Require(donor.Entity.EnlightenPoints==(ushort)Call(feature,"DailyPoints",donor) && recipient.Entity.EnlightenPoints==(ushort)Call(feature,"DailyPoints",recipient));
            Require(recipient.Entity.ReceivedEnlightenPoints==0 && recipient.Entity.EnlightmentTime==19);
            string reset=EnlightAdmissionState(donor);Call(feature,"Tick",donor);Require(reset==EnlightAdmissionState(donor));
            for(int i=0;i<18;i++){now=now.AddMinutes(1);Call(feature,"Tick",recipient);}
            Require(recipient.Entity.EnlightmentTime==1);string before=EnlightBalanceSnapshot(recipient);
            feature.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="tick")throw new InvalidOperationException("Injected deferred payout rollback");}));
            try{now=now.AddMinutes(1);Call(feature,"Tick",recipient);}finally{feature.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(recipient) && before==EnlightBalanceSnapshot(recipient));
            recipient=EnlightEdgePlayer(uid+1);Require(recipient.Entity.EnlightmentTime==1);
            now=now.AddMinutes(1);Call(feature,"Tick",recipient);Require(!PirateFenced(recipient) && recipient.Entity.EnlightmentTime==0);
            foreach(var p in new[]{donor,recipient}){Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_enlight_day_probe VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",EnlightBalanceSnapshot(p));}
        }
        foreach(var p in new[]{donor,recipient})Require(EnlightBalanceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_day_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
        Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND remaining=0 AND paid_at IS NOT NULL","uid",(long)uid)==1);
        System.Console.WriteLine("[career-process] career-enlight-day"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight day did not close.");
    }
    static bool enlightEdgesFixture;
    static GameState EnlightEdgePlayer(uint uid){nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.Entity.X=330;p.Entity.Y=300;Require(!PirateFenced(p));return p;}
    static byte[] EnlightEdgeFrame(GameState donor,GameState recipient,uint nonce){var frame=EnlightFrame(donor,recipient);Revora.Network.Writer.WriteUInt32(nonce,4,frame);return frame;}
    static void EnlightEdgesProcess(bool reload){
        enlightEdgesFixture=true;const uint uid=4294913101;var feature=Feature("EnlightenmentProgress");
        // Fixed local midday keeps the clock-controlled timer checks within one day.
        DateTime now=DateTime.Today.AddHours(12).ToUniversalTime();
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_enlight_edges_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+5)==0);
            nextUid=uid;for(int i=0;i<6;i++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=CASE WHEN uid%2=1 THEN 130 ELSE 100 END,reborn=0,enlightenpoints=800,enlightsreceived=0,enlightmentwait=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+5);
            Sql("CREATE TABLE revora_enlight_edges_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        var donor=EnlightEdgePlayer(uid);var recipient=EnlightEdgePlayer(uid+1);
        if((long)Sql("SELECT count(*) FROM revora_enlight_edges_probe")==0){
            Require(!reload);ushort initial=donor.Entity.EnlightenPoints;
            now=now.AddSeconds(59);var frame=EnlightEdgeFrame(donor,recipient,741001);
            System.Threading.Tasks.Parallel.For(0,8,_=>Enlight.Process(frame,donor));
            Require(!PirateFenced(donor) && !PirateFenced(recipient) && donor.Entity.EnlightenPoints==initial-100 && recipient.Entity.EnlightmentTime==20);
            now=now.AddSeconds(1);Call(feature,"Tick",recipient);Require(recipient.Entity.EnlightmentTime==20);
            now=now.AddSeconds(59);Call(feature,"Tick",recipient);Require(recipient.Entity.EnlightmentTime==19);
            for(uint n=2;n<=5;n++)Enlight.Process(EnlightEdgeFrame(donor,recipient,741000+n),donor);
            Require(recipient.Entity.ReceivedEnlightenPoints==5 && recipient.Entity.EnlightmentTime==99 && donor.Entity.EnlightenPoints==initial-500);
            string capped=EnlightAdmissionState(recipient);Enlight.Process(EnlightEdgeFrame(donor,recipient,741006),donor);Require(capped==EnlightAdmissionState(recipient));
            // Reconnect neither rounds99 up nor grants offline time.
            now=now.AddHours(2);recipient=EnlightEdgePlayer(uid+1);Call(feature,"Tick",recipient);Require(recipient.Entity.EnlightmentTime==99);
            ulong xp=recipient.Entity.Experience;
            for(int n=0;n<18;n++){now=now.AddMinutes(1);Call(feature,"Tick",recipient);}
            Require(recipient.Entity.EnlightmentTime==81 && recipient.Entity.Experience==xp);
            now=now.AddMinutes(1);Call(feature,"Tick",recipient);Require(recipient.Entity.EnlightmentTime==80 && recipient.Entity.Experience>xp);
            Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND paid_at IS NOT NULL","uid",(long)uid)==1);
            var rollbackDonor=EnlightEdgePlayer(uid+2);var rollbackRecipient=EnlightEdgePlayer(uid+3);
            string beforeDonor=EnlightBalanceSnapshot(rollbackDonor),beforeRecipient=EnlightBalanceSnapshot(rollbackRecipient);
            feature.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="accept")throw new InvalidOperationException("Injected acceptance rollback");}));
            try{Enlight.Process(EnlightEdgeFrame(rollbackDonor,rollbackRecipient,742001),rollbackDonor);}finally{feature.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(rollbackDonor) && PirateFenced(rollbackRecipient));
            Require(beforeDonor==EnlightBalanceSnapshot(rollbackDonor) && beforeRecipient==EnlightBalanceSnapshot(rollbackRecipient));
            Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid","uid",(long)uid+2)==0);
            var interruptedDonor=EnlightEdgePlayer(uid+4);var interruptedRecipient=EnlightEdgePlayer(uid+5);
            ushort interruptedPoints=interruptedDonor.Entity.EnlightenPoints;
            feature.GetField("BeforeInstall",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="accept")throw new InvalidOperationException("Injected committed install interruption");}));
            var interrupted=EnlightEdgeFrame(interruptedDonor,interruptedRecipient,743001);
            try{Enlight.Process(interrupted,interruptedDonor);}finally{feature.GetField("BeforeInstall",Hidden).SetValue(null,null);}
            Require(PirateFenced(interruptedDonor) && PirateFenced(interruptedRecipient));
            Require(!EntityTable.SaveEntity(interruptedDonor) && !EntityTable.SaveEntity(interruptedRecipient));
            interruptedDonor=EnlightEdgePlayer(uid+4);interruptedRecipient=EnlightEdgePlayer(uid+5);
            Require(interruptedDonor.Entity.EnlightenPoints==interruptedPoints-100 && interruptedRecipient.Entity.EnlightmentTime==20);
            string recovered=EnlightAdmissionState(interruptedRecipient);Enlight.Process(interrupted,interruptedDonor);Require(recovered==EnlightAdmissionState(interruptedRecipient));
            for(int n=0;n<19;n++){now=now.AddMinutes(1);Call(feature,"Tick",interruptedRecipient);}
            Require(interruptedRecipient.Entity.EnlightmentTime==1);xp=interruptedRecipient.Entity.Experience;
            feature.GetField("BeforeInstall",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="tick")throw new InvalidOperationException("Injected payout install interruption");}));
            try{now=now.AddMinutes(1);Call(feature,"Tick",interruptedRecipient);}finally{feature.GetField("BeforeInstall",Hidden).SetValue(null,null);}
            Require(PirateFenced(interruptedRecipient) && !EntityTable.SaveEntity(interruptedRecipient));
            interruptedRecipient=EnlightEdgePlayer(uid+5);Require(interruptedRecipient.Entity.EnlightmentTime==0 && interruptedRecipient.Entity.Experience>xp);
            recovered=EnlightAdmissionState(interruptedRecipient);Call(feature,"Tick",interruptedRecipient);Require(recovered==EnlightAdmissionState(interruptedRecipient));
            for(uint id=uid;id<=uid+5;id++){var p=EnlightEdgePlayer(id);Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_enlight_edges_probe VALUES(@uid,@snapshot)","uid",(long)id,"snapshot",EnlightBalanceSnapshot(p));}
        }
        for(uint id=uid;id<=uid+5;id++){var p=EnlightEdgePlayer(id);Require(EnlightBalanceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_edges_probe WHERE uid=@uid","uid",(long)id)));}
        Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND paid_at IS NULL","uid",(long)uid)==4);
        System.Console.WriteLine("[career-process] career-enlight-edges"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight edges did not close.");
    }
    static bool enlightProgressFixture;
    static void EnlightProgressProcess(bool reload){
        enlightProgressFixture=true;const uint uid=4294913001;
        var feature=Feature("EnlightenmentProgress");DateTime now=DateTime.UtcNow;
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_enlight_progress_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=CASE WHEN uid=@a THEN 130 ELSE 100 END,reborn=0,enlightenpoints=800,enlightsreceived=0,enlightmentwait=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
            Sql("CREATE TABLE revora_enlight_progress_probe(donor text,recipient text)");
        }
        nextUid=uid;var donor=Player(Kernel.Maps[1002],true);var recipient=Player(Kernel.Maps[1002],true);
        foreach(var p in new[]{donor,recipient}){p.LoadItemStats();p.Entity.X=330;p.Entity.Y=300;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if((long)Sql("SELECT count(*) FROM revora_enlight_progress_probe")==0){
            Require(!reload);ushort points=donor.Entity.EnlightenPoints;ulong before=recipient.Entity.Experience;
            var frame=EnlightFrame(donor,recipient);Revora.Network.Writer.WriteUInt32(731001,4,frame);
            Enlight.Process(frame,donor);
            Require(!PirateFenced(donor) && !PirateFenced(recipient));
            Require(donor.Entity.EnlightenPoints==points-100 && recipient.Entity.ReceivedEnlightenPoints==1 && recipient.Entity.EnlightmentTime==20 && recipient.Entity.Experience>before);
            string accepted=EnlightAdmissionState(recipient);Enlight.Process(frame,donor);Require(accepted==EnlightAdmissionState(recipient) && donor.Entity.EnlightenPoints==points-100);
            ulong immediate=recipient.Entity.Experience;
            for(int n=0;n<19;n++){now=now.AddMinutes(1);Call(feature,"Tick",recipient);Require(!PirateFenced(recipient));}
            Require(recipient.Entity.EnlightmentTime==1 && recipient.Entity.Experience==immediate);
            now=now.AddMinutes(1);Call(feature,"Tick",recipient);
            Require(!PirateFenced(recipient) && recipient.Entity.EnlightmentTime==0 && recipient.Entity.Experience>immediate);
            string paid=EnlightAdmissionState(recipient);Call(feature,"Tick",recipient);Require(paid==EnlightAdmissionState(recipient));
            Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND remaining=0 AND paid_at IS NOT NULL","uid",(long)uid)==1);
            Require(EntityTable.SaveEntity(donor) && EntityTable.SaveEntity(recipient));
            Sql("INSERT INTO revora_enlight_progress_probe VALUES(@a,@b)","a",EnlightBalanceSnapshot(donor),"b",EnlightBalanceSnapshot(recipient));
        }
        Require(EnlightBalanceSnapshot(donor)==Convert.ToString(Sql("SELECT donor FROM revora_enlight_progress_probe")));
        Require(EnlightBalanceSnapshot(recipient)==Convert.ToString(Sql("SELECT recipient FROM revora_enlight_progress_probe")));
        Require(EntityTable.SaveEntity(donor) && EntityTable.SaveEntity(recipient));
        System.Console.WriteLine("[career-process] career-enlight-progress-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight progress did not close.");
    }
}
