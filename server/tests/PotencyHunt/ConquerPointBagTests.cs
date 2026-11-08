using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type CpBags=typeof(Entity).Assembly.GetType("Revora.Game.Features.ConquerPointBags");
    static DateTime cpBagNow=DateTime.UtcNow;
    static long CpBagUses(GameState p){return Scalar("SELECT count(*) FROM revora_cp_bag_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenCpBag(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyRushRecoveryLockOrder(Map town,Action<bool,string> verify){
        var owner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(owner);
        owner["review-lock-order"]=1u;EntityVariableTable.Save(owner);
        Query("INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,902,2,false)","uid",(long)owner.Entity.UID);
        bool waiting=false,ledgerAvailable=false;Exception workerError=null;
        var worker=new System.Threading.Thread(delegate(){try{BossRushSystem.RecoverPending(owner);}catch(Exception ex){workerError=ex;}}){IsBackground=true};
        using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
            int pid;
            using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
            using(var q=new Npgsql.NpgsqlCommand("SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE",conn,tx)){
                q.Parameters.AddWithValue("uid",(long)owner.Entity.UID);q.ExecuteScalar();
            }
            worker.Start();
            waiting=System.Threading.SpinWait.SpinUntil(delegate{
                return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;
            },5000);
            if(waiting){try{
                using(var q=new Npgsql.NpgsqlCommand("SELECT run_id FROM revora_boss_rush_runs WHERE entityid=@uid FOR UPDATE NOWAIT",conn,tx)){
                    q.Parameters.AddWithValue("uid",(long)owner.Entity.UID);ledgerAvailable=q.ExecuteScalar()!=null;
                }
            }catch(Npgsql.NpgsqlException){ledgerAvailable=false;}}
            tx.Rollback();
        }}
        bool ended=worker.Join(10000);
        verify(waiting && ledgerAvailable && ended && workerError==null && (uint)owner[BossRushSystem.ClaimKey]==2u,
            "REVIEW-253: recovery waits for the vault before locking a ledger row, matching claim order");
    }

    static void VerifyRevoraNobilityRanks(Action<bool,string> verify) {
        var n=typeof(Revora.Game.ConquerStructures.Nobility);var old=Revora.Game.ConquerStructures.Nobility.Board;
        try {
            var board=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.NobilityInformation>();
            for(uint uid=4000170;uid>=4000001;uid--)board.Add(uid,new Revora.Game.ConquerStructures.NobilityInformation{EntityUID=uid,Donation=30000000,Position=-1});
            board.Add(3999998,new Revora.Game.ConquerStructures.NobilityInformation{EntityUID=3999998,Donation=0,Position=-1});
            board.Add(3999999,new Revora.Game.ConquerStructures.NobilityInformation{EntityUID=3999999,Donation=29999999,Rey=1,Position=-1});
            Revora.Game.ConquerStructures.Nobility.Board=board;Revora.Game.ConquerStructures.Nobility.Sort(0);board=Revora.Game.ConquerStructures.Nobility.Board;
            verify(board.Values.Count(e=>e.Rank==Revora.Game.ConquerStructures.NobilityRank.King)==5
                && board.Values.Count(e=>e.Rank==Revora.Game.ConquerStructures.NobilityRank.Prince)==50
                && board.Values.Count(e=>e.Rank==Revora.Game.ConquerStructures.NobilityRank.Duke)==117,
                "Revora ranks exactly five Kings, fifty Princes and unlimited Dukes, including zero and subminimum donations");
            verify(board[4000005].Rank==Revora.Game.ConquerStructures.NobilityRank.King && board[4000006].Rank==Revora.Game.ConquerStructures.NobilityRank.Prince
                && board[4000055].Rank==Revora.Game.ConquerStructures.NobilityRank.Prince && board[4000056].Rank==Revora.Game.ConquerStructures.NobilityRank.Duke
                && board[4000170].Rank==Revora.Game.ConquerStructures.NobilityRank.Duke,
                "rank boundaries and equal-donation UID ordering are stable beyond the old Duke cap");
            verify(board[3999999].Rank==Revora.Game.ConquerStructures.NobilityRank.Duke
                && board[3999998].Rank==Revora.Game.ConquerStructures.NobilityRank.Duke
                && (ulong)Call(n,null,"RequiredDonation",board[3999998],Revora.Game.ConquerStructures.NobilityRank.Duke)==0,
                "zero and subminimum donations receive free Duke; legacy Rey cannot add Kings");
            verify((ulong)Call(n,null,"RequiredDonation",board[4000170],Revora.Game.ConquerStructures.NobilityRank.King)==1
                && (ulong)Call(n,null,"RequiredDonation",board[4000170],Revora.Game.ConquerStructures.NobilityRank.Prince)==1
                && (ulong)Call(n,null,"RequiredDonation",board[4000170],Revora.Game.ConquerStructures.NobilityRank.Duke)==0,
                "next-rank amounts account for equal donation ties and unlimited Duke eligibility");
            board[4000170].Donation++;
            Revora.Game.ConquerStructures.Nobility.Sort(0);
            verify(board[4000170].Rank==Revora.Game.ConquerStructures.NobilityRank.King && board[4000005].Rank==Revora.Game.ConquerStructures.NobilityRank.Prince
                && board[4000055].Rank==Revora.Game.ConquerStructures.NobilityRank.Duke,
                "a higher donation promotes the new King and demotes both displaced boundaries");
        } finally {Revora.Game.ConquerStructures.Nobility.Board=old;Revora.Game.ConquerStructures.Nobility.Sort(0);}
    }
    static void VerifyEarnedBoundDonation(string runtime,Map town) {
        var p=ReloadBall(WelcomePlayer(town,140,2,15));p.Entity.VIPLevel=5;
        p.Entity.ConquerPoints=0;p.Entity.RevoraPoint=0;p.Entity.BoundCps=0;p.Entity.Money=0;
        p.Entity.DailySignDays=new byte[31];p.Entity.DailySignRewards=0;
        Check(EntityTable.SaveEntity(p),"bound donation journey starts with no supplied purses");
        foreach(ushort id in new ushort[]{1011,1020,1000})if(!Kernel.Maps.ContainsKey(id))World(runtime,id);
        object oldDaily=Daily.GetField("Clock",Hidden).GetValue(null);
        var sign=typeof(MsgSignIn);object oldSign=sign.GetField("Clock",Hidden).GetValue(null),oldPack=DailySign.GetField("Clock",Hidden).GetValue(null);
        var signFrame=SignInFrame(0);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,signFrame.Length-8,signFrame);
        DateTime now=new DateTime(2039,1,1,12,0,0,DateTimeKind.Local);
        try {
            dailyNow=now.Date.ToUniversalTime();Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));
            Call(Daily,null,"Create");var schedule=DailySchedule();
            for(int n=0;n<2;n++){
                DailyTick(schedule[n]);var boss=DailyActive();Check(boss!=null,"funding boss appears in its scheduled window");
                p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=boss.Y;
                uint damage=boss.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,AttackType=Attack.Melee},ref damage,null);
                Check(boss.Dead && p.Entity.RevoraPoint==50000u*(uint)(n+1),"ordinary boss victory earns sign-in RVP funding");
            }
            p.BindWorldMap(1002);p.Entity.X=394;p.Entity.Y=352;
            SignInTable.Load();Call(sign,null,"Initialize");Call(DailySign,null,"Initialize");
            sign.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));DailySign.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now.ToUniversalTime()));
            for(int day=1;day<=21;day++){
                now=new DateTime(2039,1,day,12,0,0,DateTimeKind.Local);
                uint before=p.Entity.RevoraPoint;MsgSignIn.Handle(p,signFrame);MsgSignIn.Handle(p,signFrame);
                Check(!Fenced(p) && p.Entity.RevoraPoint==before-4000 && Scalar("SELECT count(*) FROM revora_sign_in_claims WHERE uid=@uid",p.Entity.UID)==day,"earned RVPs pay each sign-in once: "+day);
                if(day==7 || day==21){uint id=day==7?3100014u:3100016u;var pack=p.Inventory.Objects.Single(i=>i.ID==id);OpenDailySign(p,pack);
                    Check(!Fenced(p) && !p.Inventory.ContainsUID(pack.UID) && p.Entity.BoundCps==(day==7?500000u:2000000u),"issued milestone pack supplies bound CPs: "+day);}
                if(day==7){p=ReloadBall(p);Check(p.Entity.BoundCps==500000 && p.Entity.RevoraPoint==72000,"mid-calendar reload preserves earned purse and first bound award");}
            }
            Check(p.Entity.RevoraPoint==36000 && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid",p.Entity.UID)==2 && DailySignUses(p)==2,"two boss wins fund21 sign-ins and exactly two bound-awarding pack uses");
            uint cp=p.Entity.ConquerPoints;ulong gold=p.Entity.Money;p.ActiveNpc=900042;
            NobilityDialog(p,900042,3);NobilityDialog(p,900042,4,"2000000");Parallel.For(0,8,n=>NobilityDialog(p,0,2));
            Check(!Fenced(p) && p.Entity.BoundCps==0 && p.NobilityInformation.Donation==20000000 && p.Entity.ConquerPoints==cp && p.Entity.Money==gold,"public donor spends only the earned bound CPs once");
            Revora.Game.ConquerStructures.Nobility.Board.Clear();NobilityTable.Load();p=ReloadBall(p);p.ActiveNpc=900042;NobilityDialog(p,0,2);MsgSignIn.Handle(p,signFrame);
            Check(p.Entity.BoundCps==0 && p.NobilityInformation.Donation==20000000 && p.Entity.ConquerPoints==cp && p.Entity.Money==gold && p.Entity.RevoraPoint==36000
                && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==1 && DailySignUses(p)==2,"earned funding chain and donation survive reload and replay exactly");
        } finally {Daily.GetField("Clock",Hidden).SetValue(null,oldDaily);sign.GetField("Clock",Hidden).SetValue(null,oldSign);DailySign.GetField("Clock",Hidden).SetValue(null,oldPack);}
    }
    static void VerifyPublicBoundDonor(Map town) {
        Query("CREATE TABLE IF NOT EXISTS nobility (LIKE public.nobility INCLUDING ALL)");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations"),null,"Initialize");
        var donor=new SobNpcSpawn{UID=900042,MapID=1002,X=394,Y=350,Mesh=46420,Type=Enums.NpcType.Talker,Name="Nobility Donor"};town.AddNpc(donor);
        foreach(string mode in new[]{"forged","cancel","switch-cp","switch-bound","invalid","remote","bound","cp"}) {
            var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.ConquerPoints=200000;p.Entity.BoundCps=4000000;
            p.NobilityInformation=new Revora.Game.ConquerStructures.NobilityInformation{EntityUID=p.Entity.UID};
            Check(EntityTable.SaveEntity(p),"public donor saves ordinary funded fixture");
            p.Entity.X=394;p.Entity.Y=352;p.ActiveNpc=900042;
            NobilityDialog(p,900042,0);NobilityDialog(p,900042,3);
            if(mode!="forged")NobilityDialog(p,900042,4,"2000000");
            if(mode=="cancel")NobilityDialog(p,0,255);
            if(mode=="switch-cp")NobilityDialog(p,900042,0);
            if(mode=="switch-bound")NobilityDialog(p,900042,3);
            if(mode=="invalid")NobilityDialog(p,900042,4,"1999999");
            if(mode=="remote")p.Entity.X=420;
            if(mode=="cp"){NobilityDialog(p,900042,0);NobilityDialog(p,900042,1,"100000");}
            Parallel.For(0,8,n=>NobilityDialog(p,0,2));
            bool paid=mode=="bound" || mode=="cp";
            ulong donation=mode=="bound"?20000000UL:mode=="cp"?5000000000UL:0UL;
            uint cps=mode=="cp"?100000u:200000u,bound=mode=="bound"?2000000u:4000000u;
            Check(!Fenced(p) && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.NobilityInformation.Donation==donation
                && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==(paid?1:0),
                "public donor binds confirmation currency and rejects stale or invalid offers: "+mode);
            if(paid){
                Check(Scalar("SELECT currency FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==(mode=="bound"?2:1),"receipt records selected NPC currency");
                Revora.Game.ConquerStructures.Nobility.Board.Clear();NobilityTable.Load();p=ReloadBall(p);p.ActiveNpc=900042;NobilityDialog(p,0,2);
                Check(p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.NobilityInformation.Donation==donation
                    && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==1,"public donor purses and donation survive character reload without confirmation replay");
            }
        }
    }
    static void VerifyNobilityPaymentGuards(Map town,Action<bool,string> verify) {
        Query("CREATE TABLE IF NOT EXISTS nobility (LIKE public.nobility INCLUDING ALL)");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");Call(service,null,"Initialize");
        foreach(string mode in new[]{"overflow","total-overflow","unknown-currency","failed-cp","insufficient-cp","insufficient-silver"}) {
            var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.ConquerPoints=100;p.Entity.Money=100;
            p.NobilityInformation=new Revora.Game.ConquerStructures.NobilityInformation {EntityUID=p.Entity.UID,Donation=mode=="total-overflow"?(ulong)long.MaxValue:0};
            ulong before=p.NobilityInformation.Donation;uint cp=p.Entity.ConquerPoints;ulong silver=p.Entity.Money;
            var request=new NobilityInfo(true){Type=NobilityInfo.Donate,MoneyType=1,NoblezaOro=50000};
            if(mode=="overflow")request.NoblezaOro=ulong.MaxValue;
            if(mode=="unknown-currency"){request.MoneyType=2;request.NoblezaOro=1;}
            if(mode=="insufficient-cp")request.NoblezaOro=5000001;
            if(mode=="insufficient-silver"){request.MoneyType=0;request.NoblezaOro=101;}
            if(mode=="failed-cp")Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Revora.Game.ConquerStructures.Nobility.Handle(request,p);
            if(mode=="failed-cp")Revora.Game.ConquerStructures.Nobility.Donate(50000,p);
            verify(p.NobilityInformation.Donation==before && p.Entity.ConquerPoints==cp && p.Entity.Money==silver
                && !Revora.Game.ConquerStructures.Nobility.Board.ContainsKey(p.Entity.UID)
                && (mode!="failed-cp" || Fenced(p)),"nobility rejects unpaid or unsupported donation without publishing rank: "+mode);
        }
    }
    static void VerifyAtomicNobility(Map town,Action<bool,string> verify) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");
        DateTime now=DateTime.UtcNow;service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            foreach(uint currency in new uint[]{0,1,2})foreach(string mode in new[]{"success","donation","receipt","purse"}) {
                var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.Money=100;p.Entity.ConquerPoints=100;p.Entity.BoundCps=100;
                verify(EntityTable.SaveEntity(p),"nobility fixture saves funded ordinary character");
                p.NobilityInformation=new Revora.Game.ConquerStructures.NobilityInformation{EntityUID=p.Entity.UID};
                ulong value=currency==0?75UL:currency==1?50001UL:20UL;uint charge=currency==0?75u:2u;
                string table=mode=="donation"?"nobility":mode=="receipt"?"revora_nobility_payments":"entities";
                string op=mode=="purse"?"UPDATE":"INSERT";
                if(mode!="success")Query("CREATE FUNCTION suppress_nobility() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_nobility BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_nobility()");
                try{Parallel.For(0,8,n=>Call(service,null,"Pay",p,value,currency));}
                finally{if(mode!="success")Query("DROP TRIGGER suppress_nobility ON "+table+";DROP FUNCTION suppress_nobility()");}
                long receipts=Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID);
                ulong balance=currency==0?p.Entity.Money:currency==1?p.Entity.ConquerPoints:p.Entity.BoundCps;
                if(mode=="success") {
                    verify(!Fenced(p) && balance==100-charge && p.NobilityInformation.Donation==value && receipts==1
                        && Scalar("SELECT donation FROM nobility WHERE entityuid=@uid",p.Entity.UID)==(long)value,
                        "atomic nobility concurrent payment commits exact debit and one receipt: "+currency);
                    Revora.Game.ConquerStructures.Nobility.Board.Clear();NobilityTable.Load();p=ReloadBall(p);
                    balance=currency==0?p.Entity.Money:currency==1?p.Entity.ConquerPoints:p.Entity.BoundCps;
                    verify(balance==100-charge && p.NobilityInformation.Donation==value,"cold board and full character reload preserve nobility payment: "+currency);
                    Call(service,null,"Pay",p,value,currency);
                    verify(Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==1,"reconnect replay cannot duplicate the recent nobility payment: "+currency);
                } else verify(Fenced(p) && balance==100 && p.NobilityInformation.Donation==0 && receipts==0
                    && Scalar("SELECT count(*) FROM nobility WHERE entityuid=@uid",p.Entity.UID)==0,
                    "failed nobility transaction rolls back donation, payment and receipt: "+currency+"/"+mode);
            }
        } finally {service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
    static void VerifyKingAchievement(Map town,Action<bool,string> verify) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");
        var donors=new GameState[6];
        for(int i=0;i<donors.Length;i++) {
            var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.ConquerPoints=1000;donors[i]=p;
            verify((bool)Call(service,null,"Pay",p,30000000UL,1u),"ordinary donor pays the ranked King threshold: "+i);
            verify(HasAchievement(p,10318) && HasAchievement(p,11120)==(i<5)
                && (p.Entity.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.King)==(i<5),
                "saved top-five rank agrees with King evidence for equal paid donations: "+i);
        }
        var sixth=donors[5];
        Parallel.For(0,8,n=>Call(service,null,"Pay",sixth,1UL,1u));
        verify(sixth.Entity.ConquerPoints==399 && sixth.NobilityInformation.Donation==30000001 && HasAchievement(sixth,11120)
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11120",sixth.Entity.UID)==1,
            "one extra paid silver value overtakes a tied King and concurrent replay records one achievement");
        var displaced=ReloadBall(donors[4]);displaced.Entity.MyAchievement.Send();sixth=ReloadBall(sixth);sixth.Entity.MyAchievement.Send();
        verify(displaced.Entity.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.Prince && HasAchievement(displaced,11120)
            && HasAchievement(sixth,11120) && sixth.Entity.ConquerPoints==399,
            "full reload preserves historical King achievement after demotion and the new King's exact purse");
        var failing=ReloadBall(WelcomePlayer(town,1,0,15));failing.Entity.ConquerPoints=1000;
        Query("CREATE FUNCTION suppress_king_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=11120 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_king_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_king_evidence()");
        try{Call(service,null,"Pay",failing,31000000UL,1u);}finally{Query("DROP TRIGGER suppress_king_evidence ON revora_achievement_evidence;DROP FUNCTION suppress_king_evidence()");}
        verify(Fenced(failing) && failing.Entity.ConquerPoints==1000
            && Scalar("SELECT count(*) FROM nobility WHERE entityuid=@uid",failing.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",failing.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10318,11120)",failing.Entity.UID)==0,
            "failed King evidence rolls back the purse, donation, payment receipt and first-rank evidence together");
        failing=ReloadBall(failing);failing.Entity.MyAchievement.Send();
        verify(!HasAchievement(failing,11120) && failing.Entity.ConquerPoints==1000,
            "production reload cannot resurrect a rolled-back King payment or title achievement");
    }
    static void VerifyNobilityAchievement(Map town,Action<bool,string> verify) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");
        DateTime now=DateTime.UtcNow;object oldClock=service.GetField("Clock",Hidden).GetValue(null);
        service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            foreach(uint currency in new uint[]{0,1,2}) {
                var p=ReloadBall(WelcomePlayer(town,1,0,15));
                verify(p.Entity.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.Duke && p.NobilityInformation.Donation==0,"ordinary zero-donation character loads as Duke");
                p.Entity.Money=40000000;p.Entity.ConquerPoints=1000;p.Entity.BoundCps=4000000;
                verify(EntityTable.SaveEntity(p) && p.Account.State==AccountTable.AccountState.Player,"ordinary nobility achievement fixture saves funds: "+currency);
                verify((bool)Call(service,null,"Pay",p,29950000UL,currency) && !HasAchievement(p,10318)
                    && p.Entity.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.Duke,
                    "baseline Duke below the paid achievement threshold cannot earn nobility achievement: "+currency);
                now=now.AddSeconds(2);
                Parallel.For(0,8,n=>Call(service,null,"Pay",p,50000UL,currency));
                verify(!Fenced(p) && p.NobilityInformation.Donation==30000000 && HasAchievement(p,10318)
                    && p.Entity.NobilityRank!=Revora.Game.ConquerStructures.NobilityRank.Serf
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10318",p.Entity.UID)==1,
                    "cumulative paid rank grants one immediate achievement under concurrent requests: "+currency);
                p=ReloadBall(p);p.Entity.MyAchievement.Send();
                verify(HasAchievement(p,10318) && p.NobilityInformation.Donation==30000000
                    && (currency==0?p.Entity.Money==10000000:currency==1?p.Entity.ConquerPoints==400:p.Entity.BoundCps==1000000),
                    "full character reload preserves nobility rank evidence and exact payment: "+currency);
            }
            foreach(string fault in new[]{"evidence","payment"}) {
                var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.ConquerPoints=1000;
                string table=fault=="evidence"?"revora_achievement_evidence":"revora_nobility_payments";
                Query("CREATE FUNCTION suppress_rank_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rank_evidence BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_rank_evidence()");
                try{Call(service,null,"Pay",p,30000000UL,1u);}finally{Query("DROP TRIGGER suppress_rank_evidence ON "+table+";DROP FUNCTION suppress_rank_evidence()");}
                verify(Fenced(p) && p.Entity.ConquerPoints==1000 && !HasAchievement(p,10318)
                    && Scalar("SELECT count(*) FROM nobility WHERE entityuid=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10318",p.Entity.UID)==0,
                    "failed rank transaction leaves no payment, donation or achievement: "+fault);
                p=ReloadBall(p);p.Entity.MyAchievement.Send();
                verify(p.Entity.ConquerPoints==1000 && !HasAchievement(p,10318),"reload confirms rank transaction rollback: "+fault);
            }
        } finally {service.GetField("Clock",Hidden).SetValue(null,oldClock);}
    }
    static void VerifyNobilityReset(Map town,Action<bool,string> verify) {
        var noble=typeof(Revora.Game.ConquerStructures.Nobility);
        var reset=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityReset");
        var donations=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");
        var clock=noble.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);
        var payClock=donations.GetField("Clock",Hidden);object oldPayClock=payClock.GetValue(null);
        DateTime now=DateTime.Now,payNow=DateTime.UtcNow;clock.SetValue(null,(Func<DateTime>)(()=>now));payClock.SetValue(null,(Func<DateTime>)(()=>payNow));
        try {
            foreach(string fault in new[]{"rows","marker","projection"}) {
                var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.ConquerPoints=1000;
                verify((bool)Call(donations,null,"Pay",p,30000000UL,1u),"reset fixture earns a rank through an actual CP donation: "+fault);
                long before=Scalar("SELECT extract(epoch from week)::bigint FROM revora_nobility_reset WHERE id=true AND @uid>0",p.Entity.UID);
                now=now.AddDays(7);payNow=payNow.AddSeconds(2);
                if(fault=="projection")reset.GetField("BeforeProjection",Hidden).SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture projection interruption");}));
                else {
                    string table=fault=="rows"?"nobility":"revora_nobility_reset";
                    Query("CREATE FUNCTION suppress_nobility_reset() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_nobility_reset BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_nobility_reset()");
                }
                bool failed=false;
                try{Revora.Game.ConquerStructures.Nobility.Pulse();}catch(InvalidOperationException){failed=true;}
                finally {
                    reset.GetField("BeforeProjection",Hidden).SetValue(null,null);
                    if(fault!="projection"){string table=fault=="rows"?"nobility":"revora_nobility_reset";Query("DROP TRIGGER suppress_nobility_reset ON "+table+";DROP FUNCTION suppress_nobility_reset()");}
                }
                verify(failed && p.NobilityInformation.Donation==30000000 && p.Entity.ConquerPoints==400,
                    "failed reset cannot prematurely publish a cleared rank or change the paid purse: "+fault);
                long saved=Scalar("SELECT donation FROM nobility WHERE entityuid=@uid",p.Entity.UID);
                long marker=Scalar("SELECT extract(epoch from week)::bigint FROM revora_nobility_reset WHERE id=true AND @uid>0",p.Entity.UID);
                verify(fault=="projection"?saved==0 && marker>before:saved==30000000 && marker==before,
                    "reset donation rows and checkpoint commit together before projection: "+fault);
                Parallel.For(0,6,n=>Revora.Game.ConquerStructures.Nobility.Pulse());
                verify(p.NobilityInformation.Donation==0 && p.Entity.NobalityDonation==0 && p.Entity.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.Duke,
                    "retry projects the committed reset exactly once: "+fault);
                verify((bool)Call(donations,null,"Pay",p,50000UL,1u),"ordinary player can donate in the recovered new week: "+fault);
                noble.GetField("LastWeeklyReset",Hidden).SetValue(null,DateTime.MinValue);
                Revora.Game.ConquerStructures.Nobility.Pulse();
                p=ReloadBall(p);p.Entity.MyAchievement.Send();
                verify(p.NobilityInformation.Donation==50000 && p.Entity.ConquerPoints==399 && HasAchievement(p,10318)
                    && Scalar("SELECT donation FROM nobility WHERE entityuid=@uid",p.Entity.UID)==50000,
                    "cold checkpoint recovery preserves new-week donation, purse and earned achievement: "+fault);
            }
        } finally {
            reset.GetField("BeforeProjection",Hidden).SetValue(null,null);clock.SetValue(null,oldClock);payClock.SetValue(null,oldPayClock);
        }
    }
    static void VerifyPowerRecovery(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookQuest",true);
        Call(quest,null,"Initialize");var p=ReloadBall(WelcomePlayer(town,115,0,15));
        Func<object> run=()=>Call(store,null,"Load",p);
        Action recover=()=>{p.ActiveNpc=900071841;p.Entity.X=466;p.Entity.Y=331;Npcs.GetDialog(new NpcRequest(true){NpcID=900071841,OptionID=4},p);};
        Action<uint> lose=uid=>{Query("DELETE FROM items WHERE uid=@uid","uid",(long)uid);p.Inventory.Remove(uid,Enums.ItemUse.None,true);};
        lock(p.ItemSyncRoot)Call(store,null,"Begin",p,(Func<bool>)(()=>true));var meteor=NestedPack(p,1088001,1,false);lock(p.ItemSyncRoot)Call(store,null,"BuyBook",p,meteor.UID,(Func<bool>)(()=>true));
        uint badge=(uint)Field(run(),"BadgeUid"),book=(uint)Field(run(),"BookUid");ulong silver=p.Entity.Money;uint soul=(uint)Field(run(),"Soul");
        lose(badge);lose(book);recover();uint replacement=(uint)Field(run(),"BookUid");
        Check(!Fenced(p) && CountId(p,721715)==1 && CountId(p,721924)==1 && replacement!=book && (int)Field(run(),"Stage")==2 && (uint)Field(run(),"Soul")==soul && p.Entity.Money==silver,"Power Book NPC restores genuinely lost badge/book without payment or progress changes");
        recover();Check((uint)Field(run(),"BookUid")==replacement && Scalar("SELECT COUNT(*) FROM revora_power_book_recoveries WHERE uid=@uid",p.Entity.UID)==2,"Power Book repeated recovery creates no additional items or receipts");
        p=ReloadBall(p);Check((uint)Field(run(),"BookUid")==replacement && CountId(p,721924)==1,"Power Book replacement UID survives reload");
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)replacement);p=ReloadBall(p);recover();
        Check(!Fenced(p) && CountId(p,721924)==0 && (uint)Field(run(),"BookUid")==replacement,"Power Book warehouse row prevents a replacement copy");
        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)replacement);p=ReloadBall(p);
        lose(replacement);while(p.Inventory.Count<40)Pack(p,1088000,1);recover();
        Check(!Fenced(p) && CountId(p,721924)==0 && (uint)Field(run(),"BookUid")==replacement,"Power Book full inventory leaves missing-book recovery retryable");
        lose(p.Inventory.Objects.First(i=>i.ID==1088000).UID);recover();Check(CountId(p,721924)==1 && (uint)Field(run(),"BookUid")!=replacement,"Power Book retry after freeing space restores one book");
        lose(p.Inventory.Objects.First(i=>i.ID==1088000).UID);
        var fang=NestedPack(p,721926,1,true);Query("UPDATE revora_power_book SET stage=6,material_uid=@item,material_type=721926,count_a=1 WHERE uid=@uid","item",(long)fang.UID,"uid",(long)p.Entity.UID);lose(fang.UID);recover();
        Check((uint)Field(run(),"MaterialUid")==0 && (int)Field(run(),"Stage")==6 && (int)Field(run(),"CountA")==1 && CountId(p,721926)==0,"Power Book lost fang reopens hunting without granting an unearned material or resetting progress");
        var plume=NestedPack(p,721931,1,true);Query("UPDATE revora_power_book SET stage=11,material_uid=@item,material_type=721931,count_a=4 WHERE uid=@uid","item",(long)plume.UID,"uid",(long)p.Entity.UID);
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)plume.UID);p=ReloadBall(p);recover();
        Check((uint)Field(run(),"MaterialUid")==plume.UID && (int)Field(run(),"CountA")==4,"Power Book stored plume keeps its ownership and earned burns");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)plume.UID);recover();
        Check((uint)Field(run(),"MaterialUid")==0 && (int)Field(run(),"CountA")==4 && (int)Field(run(),"Stage")==11,"Power Book genuinely lost plume reopens floor-three hunting without losing burns");
        uint currentBook=(uint)Field(run(),"BookUid");lose(currentBook);Query("UPDATE revora_power_book SET stage=14 WHERE uid=@uid","uid",(long)p.Entity.UID);recover();
        Check(!Fenced(p) && CountId(p,721924)==0 && (uint)Field(run(),"BookUid")==currentBook,"Power Book completed quest cannot manufacture replacement books");
    }

    static void PowerJournalCheck(GameState p,int stage){
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,list);
        var rows=list.Where(x=>(uint)x.UID>=1840 && (uint)x.UID<=1852).ToArray();
        Check(rows.Length==13 && rows.Select(x=>x.UID).Distinct().Count()==13 && rows.All(x=>{
            int step=(int)x.UID-1839;
            return x.Status==(stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available);
        }),"Power Book journal projects persisted stage "+stage);
    }
    static void VerifyPowerQuest(Map town,GameState continuing=null,Action<GameState> completed=null){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookQuest",true);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("Roll",Hidden);var old=clock.GetValue(null);var oldRoll=roll.GetValue(null);var now=new DateTime(2036,5,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));
        Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var p=continuing??ReloadBall(WelcomePlayer(town,115,0,15));
        Func<int> stage=()=> {int value=(int)Field(Call(store,null,"Load",p),"Stage");PowerJournalCheck(p,value);return value;};
        Action<uint,byte> talk=(npc,option)=>{p.ActiveNpc=npc;var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action pulse=()=>{now=now.AddSeconds(3);Call(quest,null,"Tick");};
        Action<uint> use=id=>ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==id),p);
        Action gate=()=>{p.Entity.X=140;p.Entity.Y=140;use(721924);};
        Action<int,bool> kill=(kind,herd)=>{
            var body=p.Map.Entities.Values.FirstOrDefault(b=>!b.Dead && b.MonsterInfo.ID==9918400+kind);
            if(body==null){pulse();body=p.Map.Entities.Values.First(b=>!b.Dead && b.MonsterInfo.ID==9918400+kind);}
            if(herd){body.X=77;body.Y=23;}p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;
            uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
            if(Fenced(p))throw new InvalidOperationException("Power Book journey fenced during hunt.");
        };
        try{
            p.Quests=new Quests(p);p.Quests.Load();
            for(uint mission=1840;mission<=1852;mission++){
                var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};
                begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);
                Check(!p.Quests.HasQuest((QuestID)mission),"Power Book client Begin cannot forge mission "+mission);
            }
            p.ActiveNpc=900071727;p.Entity.X=400;p.Entity.Y=400;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=41},p);
            Check(stage()==0,"Power Book remote NPC request cannot accept the trial");
            talk(900071727,41);talk(900071727,41);Check(stage()==1 && CountId(p,721715)==1,"Power Book ordinary NPC acceptance gives one durable badge");
            var meteor=continuing==null?NestedPack(p,1088001,1,false):p.Inventory.Objects.First(i=>i.ID==1088001);talk(900071841,1);talk(900071841,1);
            Check(stage()==2 && CountId(p,721924)==1 && !p.Inventory.Objects.Any(i=>i.UID==meteor.UID),"Power Book NPC purchase consumes one Meteor without replay charge");
            talk(900071841,2);Check(p.Map.BaseID==1500 && p.Map.Entities.Count==8,"Power Book ordinary NPC admission creates populated private cave");
            for(int i=0;i<20;i++)kill(0,false);Check(stage()==3,"Power Book player route completes first hunt");pulse();
            for(int i=0;i<10;i++)kill(1,true);Check(stage()==4,"Power Book player route completes herding objective");pulse();
            for(int i=0;i<15;i++){kill(0,false);kill(1,false);}gate();Check(stage()==5 && p.Map.BaseID==1501,"Power Book book route advances to second floor");
            kill(2,false);kill(2,false);PowerJournalCheck(p,6);pulse();kill(3,false);use(721926);Check(stage()==7,"Power Book ordinary item dispatcher consumes earned fang");pulse();
            for(int i=0;i<10;i++){kill(2,false);kill(3,false);}gate();Check(stage()==8 && p.Map.BaseID==1502,"Power Book book route advances to third floor");
            kill(4,false);kill(4,false);PowerJournalCheck(p,9);pulse();kill(5,false);
            use(721928);Check(CountId(p,721928)==1,"Power Book raw wing cannot be roasted away from fire");
            p.Entity.X=100;p.Entity.Y=66;use(721928);use(721929);Check(stage()==10,"Power Book earned wing cooks and eats through ordinary item dispatcher");pulse();
            for(int i=0;i<5;i++){kill(4,false);kill(5,false);}gate();Check(stage()==11 && p.Map.BaseID==1503 && CountId(p,721931)==1,"Power Book earned plume travels with player to final floor");
            for(int i=0;i<10;i++){kill(6,false);use(721931);}
            Check(stage()==12 && CountId(p,721931)==0,"Power Book ten actual corpse burns consume plume and finish trial");
            p.Entity.X=77;p.Entity.Y=27;use(721924);Check(p.Entity.MapID==1002 && Call(scenes,null,"Find",p)==null,"Power Book book exit saves return and releases cave");
            talk(900071841,3);Check(stage()==13,"Power Book real Beggar hand-in saves EXP and silver stage");
            talk(900071727,43);talk(900071727,43);
            Check(stage()==14 && CountId(p,728251)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && CountId(p,721715)==0 && CountId(p,721924)==0,"Power Book Chang delivers one bound reward pair without replay");
            uint uid=p.Entity.UID;p=ReloadBall(p);Check(stage()==14 && CountId(p,728251)==1 && Scalar("SELECT COUNT(*) FROM revora_power_book_rewards WHERE uid=@uid",uid)==1,"Power Book whole ordinary route survives reload with one reward receipt");if(completed!=null)completed(p);
        }finally{var scene=Call(scenes,null,"Find",p);if(scene!=null){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,old);roll.SetValue(null,oldRoll);}
    }

    static void VerifyPowerPopulation(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);var combat=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookCombat",true);
        Call(store,null,"Initialize");Call(scenes,null,"InitializeTravel");var clock=store.GetField("Clock",Hidden);var old=clock.GetValue(null);var now=new DateTime(2036,4,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        var p=ReloadBall(WelcomePlayer(town,140,0,15));object scene=null;Map map=null;
        Action<int> enter=floor=>{if(scene!=null){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}lock(p.ItemSyncRoot){scene=Call(scenes,null,"Create",p,floor);Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));}map=(Map)Field(scene,"Map");p.Entity.X=90;p.Entity.Y=49;};
        Action pulse=()=>{lock(p.ItemSyncRoot)Call(combat,null,"Populate",scene);};
        Action<Entity> kill=body=>{uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);};
        try{
            lock(p.ItemSyncRoot)Call(store,null,"Begin",p,(Func<bool>)(()=>true));var meteor=NestedPack(p,1088001,1,false);lock(p.ItemSyncRoot)Call(store,null,"BuyBook",p,meteor.UID,(Func<bool>)(()=>true));
            enter(0);pulse();Check(map.Entities.Count==8 && map.Entities.Values.All(b=>b.MonsterInfo.ID==9918400 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.X,b.Y)>=2),"Power Book population creates eight owned first-stage hunters off the player's cell");
            var first=map.Entities.Values.ToArray();pulse();Check(map.Entities.Count==8 && first.All(b=>map.Entities.ContainsKey(b.UID)),"Power Book repeated pulse does not duplicate its wave");
            for(int i=0;i<20;i++){
                var body=map.Entities.Values.First(b=>!b.Dead);kill(body);
                if(i%8==7){now=now.AddSeconds(3);pulse();}
            }
            Check((int)Field(Call(store,null,"Load",p),"Stage")==3 && !Fenced(p),"Power Book automatic replenishment supports twenty real kills without manually spawning actors");
            var stale=map.Entities.Values.ToArray();now=now.AddSeconds(3);pulse();
            Check(map.Entities.Count==8 && map.Entities.Values.All(b=>b.MonsterInfo.ID==9918401) && stale.All(b=>!map.Entities.ContainsKey(b.UID)),"Power Book stage transition retires old receipts and creates fresh Gold Ghosts");
            Check(stale.All(b=>(bool)Call(combat,null,"BlockAttack",p.Entity,b)),"Power Book removed stage actors cannot deal or receive damage");
            int[] stages={4,5,6,7,8,9,10,11};uint[][] expected={new uint[]{9918400,9918401},new uint[]{9918402},new uint[]{9918403},new uint[]{9918402,9918403},new uint[]{9918404},new uint[]{9918405},new uint[]{9918404,9918405},new uint[]{9918406}};
            for(int i=0;i<stages.Length;i++){
                lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);scene=null;
                Query("UPDATE revora_power_book SET stage=@stage,count_a=0,count_b=0 WHERE uid=@uid","stage",stages[i],"uid",(long)p.Entity.UID);enter(stages[i]<5?0:stages[i]<8?1:stages[i]<11?2:3);pulse();
                Check(map.Entities.Count==8 && expected[i].All(id=>map.Entities.Values.Count(b=>b.MonsterInfo.ID==id)==8/expected[i].Length),"Power Book stage "+stages[i]+" populates only its objective families");
            }
            var corpse=map.Entities.Values.First();kill(corpse);now=now.AddSeconds(3);pulse();
            Check(map.Entities.ContainsKey(corpse.UID) && corpse.Dead && map.Entities.Values.Count(b=>!b.Dead)==8,"Power Book replenishment retains burnable Minotaur corpse while replacing the living slot");
            now=now.AddSeconds(1);pulse();Check(!map.Entities.ContainsKey(corpse.UID),"Power Book corpse cleanup runs even between population polls");
            now=now.AddMinutes(31);int count=map.Entities.Count;pulse();Check(map.Entities.Count==count,"Power Book expired scene cannot replenish");
        }finally{if(scene!=null){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,old);}
    }

    static void VerifyPowerCombat(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);var combat=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookCombat",true);
        Call(store,null,"Initialize");Call(scenes,null,"InitializeTravel");var clock=store.GetField("Clock",Hidden);var old=clock.GetValue(null);var now=new DateTime(2036,3,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        var p=ReloadBall(WelcomePlayer(town,140,0,15));var other=ReloadBall(WelcomePlayer(town,140,0,15));object scene=null;Map map=null;
        Action<int> enter=floor=>{if(scene!=null){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}lock(p.ItemSyncRoot){scene=Call(scenes,null,"Create",p,floor);Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));}map=(Map)Field(scene,"Map");p.Entity.X=90;p.Entity.Y=49;};
        Func<int,Entity> spawn=kind=>{lock(p.ItemSyncRoot)return (Entity)Call(combat,null,"Spawn",scene,p,kind,(ushort)90,(ushort)50);};
        Action<Entity,Entity,uint> hit=(attacker,target,amount)=>{uint damage=amount;Revora.Game.Attacking.Handle.ReceiveAttack(attacker,target,new Attack(true){AttackType=Attack.Melee,Attacker=attacker.UID,Attacked=target.UID},ref damage,null);};
        try{
            lock(p.ItemSyncRoot)Call(store,null,"Begin",p,(Func<bool>)(()=>true));var meteor=NestedPack(p,1088001,1,false);lock(p.ItemSyncRoot)Call(store,null,"BuyBook",p,meteor.UID,(Func<bool>)(()=>true));
            enter(0);var body=spawn(0);uint hp=body.Hitpoints;
            hit(other.Entity,body,hp);Check(body.Hitpoints==hp,"Power Book real damage path blocks an unrelated player");
            hit(p.Entity,body,100);Check(!body.Dead && body.Hitpoints==hp-100 && (int)Field(Call(store,null,"Load",p),"CountA")==0,"Power Book nonlethal owner damage cannot advance quest progress");
            hit(p.Entity,body,body.Hitpoints);Check(body.Dead && !Fenced(p) && (int)Field(Call(store,null,"Load",p),"CountA")==1 && map.FloorItems.Count==0,
                "Power Book actual lethal handler records owned death without legacy floor loot");
            body.Die(p.Entity);Check((int)Field(Call(store,null,"Load",p),"CountA")==1,"Power Book production death replay cannot duplicate progress");
            now=now.AddSeconds(1);lock(p.ItemSyncRoot)Call(combat,null,"Prune",scene);Check(!map.Entities.ContainsKey(body.UID),"Power Book completed ordinary corpse is removed at its deadline");
            for(int i=1;i<20;i++){body=spawn(0);hit(p.Entity,body,body.Hitpoints);now=now.AddSeconds(1);lock(p.ItemSyncRoot)Call(combat,null,"Prune",scene);}
            Check(!Fenced(p) && (int)Field(Call(store,null,"Load",p),"Stage")==3,"Power Book twenty real lethal attacks complete first combat stage");
            lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);scene=null;
            Query("UPDATE revora_power_book SET stage=11 WHERE uid=@uid","uid",(long)p.Entity.UID);enter(2);body=spawn(4);
            var materialRoll=store.GetField("Roll",Hidden);var previousRoll=materialRoll.GetValue(null);materialRoll.SetValue(null,(Func<int,int>)(n=>0));
            try{hit(p.Entity,body,body.Hitpoints);}finally{materialRoll.SetValue(null,previousRoll);}
            uint plume=(uint)Field(Call(store,null,"Load",p),"MaterialUid");
            Check(plume!=0 && p.Inventory.ContainsUID(plume),"Power Book real Blue Bird kill supplies the plume for final corpse burning");
            enter(3);body=spawn(6);
            Check(body.MonsterInfo.Level==120 && !HasAchievement(p,10525),"Power Book final Minotaur qualifies while earlier profiles do not");
            Query("CREATE FUNCTION reject_book_corpse() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER reject_book_corpse BEFORE UPDATE ON revora_power_book_lives FOR EACH ROW WHEN(NEW.kind=6 AND NEW.consumed) EXECUTE FUNCTION reject_book_corpse()");
            try{hit(p.Entity,body,body.Hitpoints);}finally{Query("DROP TRIGGER reject_book_corpse ON revora_power_book_lives;DROP FUNCTION reject_book_corpse()");}
            Check(!body.Dead && !Fenced(p) && !HasAchievement(p,10525),"suppressed final corpse save leaves Minotaur alive without unarmed credit");
            Query("CREATE FUNCTION reject_book_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER reject_book_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_book_unarmed()");
            try{hit(p.Entity,body,body.Hitpoints);}finally{Query("DROP TRIGGER reject_book_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_book_unarmed()");}
            Check(!body.Dead && Scalar("SELECT count(*) FROM revora_power_book_lives WHERE uid=@uid AND kind=6 AND consumed",p.Entity.UID)==0,
                "failed unarmed credit rolls back consumed corpse and leaves same Minotaur retryable");
            var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
            victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
            try{hit(p.Entity,body,body.Hitpoints);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
            long deadAt=Scalar("SELECT extract(epoch FROM dead_at)::bigint FROM revora_power_book_lives WHERE uid=@uid AND kind=6 AND consumed",p.Entity.UID);
            Check(!body.Dead && deadAt>0,"uncertain final death retains the committed corpse timestamp");
            now=now.AddSeconds(1);hit(p.Entity,body,body.Hitpoints);body.Die(p.Entity);
            Check(body.Dead && HasAchievement(p,10525)
                && Scalar("SELECT extract(epoch FROM dead_at)::bigint FROM revora_power_book_lives WHERE uid=@uid AND kind=6 AND consumed",p.Entity.UID)==deadAt
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==1,
                "Power Book retry grants one unarmed achievement without restarting the saved burn window");
            bool burned,repeated;lock(p.ItemSyncRoot){burned=(bool)Call(store,null,"Burn",p,body,plume);repeated=(bool)Call(store,null,"Burn",p,body,plume);}
            Check(burned && !repeated && (int)Field(Call(store,null,"Load",p),"CountA")==1,"newly accepted Minotaur corpse burns once through its saved life receipt");
            now=now.AddSeconds(3);lock(p.ItemSyncRoot)Call(combat,null,"Prune",scene);Check(map.Entities.ContainsKey(body.UID) && body.Dead,"Power Book Minotaur corpse remains through its full burn window");
            now=now.AddSeconds(1);lock(p.ItemSyncRoot)Call(combat,null,"Prune",scene);Check(!map.Entities.ContainsKey(body.UID),"Power Book expired Minotaur corpse is cleaned up");
            body=spawn(6);hit(p.Entity,body,body.Hitpoints);now=now.AddSeconds(3);
            lock(p.ItemSyncRoot)burned=(bool)Call(store,null,"Burn",p,body,plume);
            Check(!burned && (int)Field(Call(store,null,"Load",p),"CountA")==1,"new final-death receipt rejects burning at the exact three-second boundary");
            body=spawn(6);var foreign=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,MonsterInfo=body.MonsterInfo};
            Check((bool)Call(combat,null,"BlockAttack",p.Entity,foreign),"Power Book family ID alone cannot impersonate a registered actor");
            ushort id=map.ID;lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);scene=null;
            Check(!Kernel.Maps.ContainsKey(id) && (bool)Call(combat,null,"BlockAttack",body,p.Entity),"Power Book released actors cannot attack after private return");
            p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525),"Power Book unarmed achievement survives real character reload");
        }finally{if(scene!=null){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,old);}
    }

    static void VerifyPowerTravel(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);
        Call(store,null,"Initialize");Call(scenes,null,"InitializeTravel");var clock=store.GetField("Clock",Hidden);var previous=clock.GetValue(null);var now=new DateTime(2036,2,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        var p=ReloadBall(WelcomePlayer(town,140,0,15));var other=ReloadBall(WelcomePlayer(town,140,0,15));object scene=null;Map map=null;
        Action allocate=()=>{lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p,0);map=(Map)Field(scene,"Map");};
        Action enter=()=>{lock(p.ItemSyncRoot)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));};
        try{
            lock(p.ItemSyncRoot)Call(store,null,"Begin",p,(Func<bool>)(()=>true));var meteor=NestedPack(p,1088001,1,false);lock(p.ItemSyncRoot)Call(store,null,"BuyBook",p,meteor.UID,(Func<bool>)(()=>true));
            allocate();other.Entity.Teleport(map.ID,77,25);p.Entity.Teleport(map.ID,77,25);
            Check(other.Entity.MapID==1002 && p.Entity.MapID==1002,"Power Book production teleports reject outsider and owner without server transfer");
            enter();Check(p.Entity.MapID==map.ID && Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid AND active",p.Entity.UID)==1
                && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID,"Power Book actual entry saves visit intent and arrival");
            enter();Check(Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid",p.Entity.UID)==1,"Power Book repeated entry does not create another visit");
            now=now.AddMinutes(30);Call(scenes,null,"Tick");
            Check(p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Power Book exact visit expiry returns player and releases map");scene=null;
            allocate();enter();var original=p;p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");
            Check(p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002
                && (int)Field(Call(store,null,"Load",p),"Stage")==2,"Power Book reconnect returns replacement session and preserves quest progress");scene=null;
            allocate();enter();uint uid=p.Entity.UID;Kernel.GamePool.Remove(uid);Call(scenes,null,"Tick");
            Check(!Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",uid)==1002 && Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid AND active",uid)==0,
                "Power Book disconnected owner is returned durably before scene disposal");scene=null;p=ReloadBall(p);
            allocate();enter();uid=p.Entity.UID;Kernel.GamePool.Remove(uid);Call(scenes,null,"Release",scene);scene=null;
            Query("CREATE FUNCTION suppress_power_return() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");Query("CREATE TRIGGER suppress_power_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_power_return()");
            bool failed=false;try{Call(scenes,null,"RecoverInterrupted");}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_power_return ON entities;DROP FUNCTION suppress_power_return()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid AND active",uid)==1,"Power Book suppressed startup return retains its retryable active receipt");
            Call(scenes,null,"RecoverInterrupted");Call(scenes,null,"RecoverInterrupted");p=ReloadBall(p);
            Check(p.Entity.MapID==1002 && (int)Field(Call(store,null,"Load",p),"Stage")==2 && Scalar("SELECT count(*) FROM revora_power_book_visits WHERE uid=@uid AND active",uid)==0,
                "Power Book interrupted visit recovery is idempotent and retains earned progress");
        }finally{if(scene!=null && !(bool)Field(scene,"Released")){p.BindWorldMap(1002);Call(scenes,null,"Release",scene);}clock.SetValue(null,previous);}
    }

    static void VerifyPowerBookHunts(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);
        Call(store,null,"Initialize");var clock=store.GetField("Clock",Hidden);var priorClock=clock.GetValue(null);var now=new DateTime(2036,1,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        var roll=store.GetField("Roll",Hidden);var priorRoll=roll.GetValue(null);roll.SetValue(null,(Func<int,int>)(max=>0));
        var p=ReloadBall(WelcomePlayer(town,115,0,15));object scene=null;Map map=null;
        Func<object> load=()=>Call(store,null,"Load",p);Func<int> stage=()=> (int)Field(load(),"Stage");
        Func<uint,bool> use=uid=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"UseMaterial",p,scene,uid);};
        Func<uint> material=()=> (uint)Field(load(),"MaterialUid");
        Action release=()=>{if(scene==null)return;p.BindWorldMap(1002);map.Entities.Clear();Call(scenes,null,"Release",scene);scene=null;};
        Action<int> enter=floor=>{release();lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p,floor);map=(Map)Field(scene,"Map");p.BindWorldMap(map.ID);p.Entity.X=77;p.Entity.Y=25;};
        Func<int,ushort,ushort,Entity> make=(kind,x,y)=>{
            var info=(MonsterInformation)Call(scenes,null,"Profile",kind);var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=x,Y=y,MonsterInfo=info};
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;map.Entities[body.UID]=body;
            lock(p.ItemSyncRoot)Call(store,null,"RegisterLife",p,scene,body);return body;
        };
        Func<Entity,bool> kill=body=>{body.Killer=p.Entity;body.Hitpoints=0;lock(p.ItemSyncRoot)return (bool)Call(store,null,"AcceptDeath",p,body);};
        try{
            lock(p.ItemSyncRoot)Call(store,null,"Begin",p,(Func<bool>)(()=>true));var meteor=NestedPack(p,1088001,1,false);
            lock(p.ItemSyncRoot)Call(store,null,"BuyBook",p,meteor.UID,(Func<bool>)(()=>true));ulong gold=p.Entity.Money;
            enter(0);var living=make(0,90,50);bool result;lock(p.ItemSyncRoot)result=(bool)Call(store,null,"AcceptDeath",p,living);
            Check(!result && (int)Field(load(),"CountA")==0,"Power Book living actor cannot earn a kill");
            Check(kill(living) && !kill(living) && (int)Field(load(),"CountA")==1,"Power Book corpse replay cannot duplicate saved count");
            var stale=make(0,90,50);for(int i=0;i<19;i++)kill(make(0,90,50));
            Check(stage()==3 && p.Entity.Money==gold+100,"Power Book twenty owned Slingers complete first hunt and silver atomically");
            Check(!kill(stale) && (int)Field(load(),"CountA")==0,"Power Book previous-stage actor cannot credit next stage");
            Check(!kill(make(1,90,50)) && (int)Field(load(),"CountA")==0,"Power Book ghosts outside herding regions do not count");
            for(int i=0;i<10;i++)kill(make(1,77,23));Check(stage()==4 && p.Entity.Money==gold+200,"Power Book ten ghosts at the herd point complete second hunt");
            for(int i=0;i<15;i++)kill(make(0,90,50));Check(stage()==4 && (int)Field(load(),"CountA")==15,"Power Book mixed hunt requires both monster families");
            for(int i=0;i<15;i++)kill(make(1,90,50));Check(stage()==5,"Power Book mixed hunt completes only after both counters");
            enter(1);kill(make(2,90,50));var deadline=(DateTime)Field(load(),"Window");now=deadline;kill(make(2,90,50));
            Check(stage()==5 && (int)Field(load(),"CountA")==1 && (DateTime)Field(load(),"Window")==now.AddSeconds(10),"Power Book exact ten-second boundary restarts instead of granting timed victory");
            var oldBody=make(2,90,50);release();EntityTable.SaveEntity(p);p=ReloadBall(p);
            Check(stage()==5 && (int)Field(load(),"CountA")==1 && (DateTime)Field(load(),"Window")==now.AddSeconds(10),"Power Book timed counter/deadline survive production reload");
            lock(p.ItemSyncRoot)result=(bool)Call(store,null,"AcceptDeath",p,oldBody);Check(!result,"Power Book replaced session cannot reuse an old actor token");
            enter(1);now=now.AddSeconds(9);kill(make(2,90,50));Check(stage()==6,"Power Book second kill inside the saved deadline completes timed stage");
            kill(make(3,90,50));uint fang=material();Check(fang!=0 && CountId(p,721926)==1,"Power Book owned Agile Rat supplies exact bound fang");
            var unrelated=NestedPack(p,721926,1,true);Check(!use(unrelated.UID) && stage()==6,"Power Book rejects an unrelated fang UID");
            roll.SetValue(null,(Func<int,int>)(max=>99));Check(use(fang) && stage()==6 && material()==0,"Power Book failed teeth mark consumes only its earned fang");
            roll.SetValue(null,(Func<int,int>)(max=>0));kill(make(3,90,50));fang=material();release();EntityTable.SaveEntity(p);p=ReloadBall(p);enter(1);
            Check(material()==fang && use(fang) && stage()==7 && !use(fang),"Power Book earned fang survives reload and advances once");
            for(int i=0;i<10;i++){kill(make(2,90,50));kill(make(3,90,50));}Check(stage()==8,"Power Book continuous route requires ten of each floor-two family");
            enter(2);kill(make(4,90,50));now=now.AddSeconds(20);kill(make(4,90,50));Check(stage()==8 && (int)Field(load(),"CountA")==1,"Power Book twenty-second boundary restarts Blue Bird trial");
            now=now.AddSeconds(19);kill(make(4,90,50));Check(stage()==9,"Power Book Blue Bird pair within twenty seconds completes timed trial");
            kill(make(5,90,50));uint wing=material();Check(wing!=0 && !use(wing) && material()==wing,"Power Book raw wing cannot cook away from fire");
            p.Entity.X=100;p.Entity.Y=66;roll.SetValue(null,(Func<int,int>)(max=>99));Check(use(wing) && stage()==9 && material()==0,"Power Book failed cooking consumes raw wing without progress");
            roll.SetValue(null,(Func<int,int>)(max=>0));kill(make(5,90,50));wing=material();Check(use(wing) && !use(wing) && CountId(p,721929)==1,"Power Book fire produces one exact roasted wing");
            uint roast=material();release();EntityTable.SaveEntity(p);p=ReloadBall(p);enter(2);Check(material()==roast && use(roast) && stage()==10 && !use(roast),"Power Book eating saved roast advances the continuous route once");
            for(int i=0;i<4;i++){kill(make(4,90,50));kill(make(5,90,50));}kill(make(4,90,50));var last=make(5,90,50);
            Query("ALTER TABLE revora_power_book ADD CONSTRAINT power_hunt_fault CHECK(stage<>11) NOT VALID");bool failed=false;
            try{kill(last);}catch(Npgsql.NpgsqlException){failed=true;}finally{Query("ALTER TABLE revora_power_book DROP CONSTRAINT power_hunt_fault");}
            Check(failed && stage()==10 && (int)Field(load(),"CountB")==4,"Power Book failed stage save rolls kill receipt and counts back");
            Check(kill(last) && stage()==11 && !kill(last),"Power Book same death safely retries after transaction rollback exactly once");
            uint plume=material();Check(plume!=0 && CountId(p,721931)==1,"Power Book Blue Bird hunt supplies one reusable plume");
            enter(3);Func<Entity,bool> burn=body=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Burn",p,body,plume);};
            var late=make(6,90,50);kill(late);p.Entity.X=90;p.Entity.Y=50;now=now.AddSeconds(3);
            Check(!burn(late) && (int)Field(load(),"CountA")==0,"Power Book corpse at exact three-second expiry cannot burn");
            for(int i=0;i<10;i++){
                var corpse=make(6,90,50);kill(corpse);
                if(i==0){p.Entity.X=77;p.Entity.Y=25;bool denied=false;try{burn(corpse);}catch(InvalidOperationException){denied=true;}Check(denied,"Power Book distant corpse interaction is rejected");p.Entity.X=90;p.Entity.Y=50;}
                Check(burn(corpse) && !burn(corpse),"Power Book fresh corpse burns exactly once: "+i);
            }
            Check(stage()==12 && material()==0 && CountId(p,721931)==0,"Power Book tenth corpse consumes plume and finishes all trial objectives");
            release();EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage()==12 && material()==0,"Power Book continuous objective completion survives production reload");
            ulong beforeGold=p.Entity.Money,beforeExp=p.Entity.Experience;byte beforeLevel=p.Entity.Level;
            lock(p.ItemSyncRoot)result=(bool)Call(store,null,"HandIn",p,(Func<bool>)(()=>true));
            Check(result && stage()==13 && p.Entity.Money==beforeGold+6000 && (p.Entity.Level>beforeLevel || p.Entity.Experience>beforeExp),"Power Book earned hand-in saves silver and normal hour EXP");
            lock(p.ItemSyncRoot)result=(bool)Call(store,null,"HandIn",p,(Func<bool>)(()=>true));Check(!result && p.Entity.Money==beforeGold+6000,"Power Book repeated hand-in cannot repeat silver or EXP");
            Query("ALTER TABLE items ADD CONSTRAINT power_claim_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool claimFailed=false;
            try{lock(p.ItemSyncRoot)Call(store,null,"Claim",p,(Func<bool>)(()=>true));}catch(Npgsql.NpgsqlException){claimFailed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT power_claim_fault");}
            Check(claimFailed && stage()==13 && CountId(p,721715)==1 && CountId(p,721924)==1 && CountId(p,728251)==0,
                "Power Book failed soul insertion restores quest items and prevents partial pack/completion");
            p=ReloadBall(p);uint promised=(uint)Field(load(),"Soul");int claims=0;
            Parallel.For(0,8,n=>{lock(p.ItemSyncRoot)if((bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true)))System.Threading.Interlocked.Increment(ref claims);});
            Check(claims==1 && stage()==14 && CountId(p,721715)==0 && CountId(p,721924)==0 && CountId(p,728251)==1
                && p.Inventory.Objects.Count(i=>i.ID==promised && i.Bound)==1,"Power Book complete earned route claims one bound pack and the frozen bound P7");
            var earnedIds=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();p=ReloadBall(p);
            Check(stage()==14 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(earnedIds),"Power Book exact final rewards survive production reload");
            Call(FixedPacks,null,"Initialize");var earnedPack=p.Inventory.Objects.Single(i=>i.ID==728251);OpenFixed(p,earnedPack);
            Check(CountId(p,728251)==0 && p.Inventory.Objects.Any(i=>i.ID==130109 && i.Bound) && p.Inventory.Objects.Any(i=>i.ID==723911 && i.Bound),
                "Power Book earned pack opens through ordinary item dispatch to class armor and bound EXP Ball");

        }finally{release();clock.SetValue(null,priorClock);roll.SetValue(null,priorRoll);}
    }

    static void VerifyPowerRewardPacks(Map town){
        Call(FixedPacks,null,"Initialize");byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] armors={130109,131109,133109,135109,136109,139109,138109,134109,134109,101109};
        for(int n=0;n<jobs.Length;n++){
            var p=ReloadBall(WelcomePlayer(town,140,2,jobs[n]));var pack=NestedPack(p,728251,1,n%2==0);OpenFixed(p,pack);
            Check(!Fenced(p) && CountId(p,728251)==0 && p.Inventory.Objects.Single(i=>i.ID==armors[n]).Bound==(n%2==0)
                && p.Inventory.Objects.Single(i=>i.ID==723911).Bound,"Power Book class armor and EXP Ball binding: "+jobs[n]);
            p=ReloadBall(p);Check(CountId(p,armors[n])==1 && CountId(p,723911)==1,"Power Book class pack outputs survive reload: "+jobs[n]);
        }
        var full=ReloadBall(WelcomePlayer(town,140,2,15));var source=NestedPack(full,728251,1,true);while(full.Inventory.Count<40)Pack(full,1088001,1);OpenFixed(full,source);
        Check(!Fenced(full) && full.Inventory.ContainsUID(source.UID) && CountId(full,130109)==0,"Power Book two-output pack refuses a full bag without consuming it");
        var fault=ReloadBall(WelcomePlayer(town,140,2,15));var failed=NestedPack(fault,728251,1,true);
        Query("ALTER TABLE items ADD CONSTRAINT power_pack_fault CHECK(id<>723911) NOT VALID");try{OpenFixed(fault,failed);}finally{Query("ALTER TABLE items DROP CONSTRAINT power_pack_fault");}
        Check(Fenced(fault) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",failed.UID)==1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=130109",fault.Entity.UID)==0,
            "Power Book failed EXP Ball insert rolls armor and source consumption back");
    }

    static void VerifyPowerBookStore(Map town){
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookStore",true);Call(type,null,"Initialize");Call(type,null,"Initialize");
        Func<bool> here=()=>true;
        Func<GameState,bool> begin=p=>{lock(p.ItemSyncRoot)return (bool)Call(type,null,"Begin",p,here);};
        Func<GameState,uint,bool> buy=(p,uid)=>{lock(p.ItemSyncRoot)return (bool)Call(type,null,"BuyBook",p,uid,here);};
        Func<GameState,int> stage=p=>(int)Field(Call(type,null,"Load",p),"Stage");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,140,life,15));int accepted=0;
            Parallel.For(0,8,n=>{if(begin(p))System.Threading.Interlocked.Increment(ref accepted);});
            uint badge=(uint)Field(Call(type,null,"Load",p),"BadgeUid"),soul=(uint)Field(Call(type,null,"Load",p),"Soul");
            Check(accepted==1 && stage(p)==1 && CountId(p,721715)==1 && p.Inventory.Objects.Single(i=>i.UID==badge).Bound,
                "Power Book concurrent acceptance issues one exact bound badge per life: "+life);
            p=ReloadBall(p);Check(stage(p)==1 && (uint)Field(Call(type,null,"Load",p),"Soul")==soul && p.Inventory.ContainsUID(badge),"Power Book accepted contract survives production reload: "+life);
            Check(!buy(p,123) && stage(p)==1,"Power Book missing Meteor cannot advance: "+life);
            var meteor=NestedPack(p,1088001,3,life==1);ulong silver=p.Entity.Money;int purchased=0;
            Parallel.For(0,8,n=>{if(buy(p,meteor.UID))System.Threading.Interlocked.Increment(ref purchased);});
            uint book=(uint)Field(Call(type,null,"Load",p),"BookUid");
            Check(purchased==1 && stage(p)==2 && meteor.StackSize==2 && p.Entity.Money==silver+100 && CountId(p,721924)==1
                && p.Inventory.Objects.Single(i=>i.UID==book).Bound,"Power Book concurrent purchase charges exactly one Meteor and saves book/silver: "+life);
            p=ReloadBall(p);Check(stage(p)==2 && p.Inventory.ContainsUID(book) && p.Inventory.Objects.Single(i=>i.UID==meteor.UID).StackSize==2
                && !buy(p,meteor.UID) && !begin(p),"Power Book purchase survives reload without repeat charge or acceptance: "+life);
        }
        foreach(bool stacked in new[]{false,true}){
            var p=ReloadBall(WelcomePlayer(town,140,0,15));begin(p);var meteor=NestedPack(p,1088001,stacked?(ushort)2:(ushort)1,false);
            while(p.Inventory.Count<40)Pack(p,1088000,1);
            bool result=buy(p,meteor.UID);
            Check(result!=stacked && p.Inventory.Count==40 && stage(p)==(stacked?1:2) && CountId(p,721924)==(stacked?0:1),"Power Book full bag respects Meteor remainder: "+stacked);
        }
        var fault=ReloadBall(WelcomePlayer(town,140,0,15));begin(fault);var payment=NestedPack(fault,1088001,2,false);ulong priorSilver=fault.Entity.Money;
        Query("ALTER TABLE items ADD CONSTRAINT power_book_fault CHECK(id<>721924) NOT VALID");bool failed=false;
        try{buy(fault,payment.UID);}catch(Npgsql.NpgsqlException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT power_book_fault");}
        Check(failed && stage(fault)==1 && payment.StackSize==2 && fault.Entity.Money==priorSilver
            && Scalar("SELECT count(*) FROM revora_power_book_purchases WHERE uid=@uid",fault.Entity.UID)==0,"Power Book failed item insert rolls back payment, silver, stage and receipt");
        fault=ReloadBall(fault);Check(buy(fault,payment.UID) && stage(fault)==2,"Power Book rollback safely retries after reload");
        var lost=ReloadBall(WelcomePlayer(town,140,0,15));int calls=0;failed=false;
        try{lock(lost.ItemSyncRoot)Call(type,null,"Begin",lost,(Func<bool>)(()=>++calls==1));}catch(InvalidOperationException){failed=true;}
        Check(failed && stage(lost)==0 && CountId(lost,721715)==0,"Power Book lost final context rolls acceptance and badge back");
        var blocked=ReloadBall(WelcomePlayer(town,140,0,15));begin(blocked);var locked=NestedPack(blocked,1088001,1,false);locked.Lock=1;
        Check(!buy(blocked,locked.UID) && stage(blocked)==1,"Power Book cannot spend locked Meteors");
        var full=ReloadBall(WelcomePlayer(town,140,0,15));while(full.Inventory.Count<40)Pack(full,1088000,1);
        Check(!begin(full) && stage(full)==0,"Power Book refuses acceptance without badge capacity");
    }

    static void VerifyPowerScenes(Map town){
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerBookScenes",true);
        var p=ReloadBall(WelcomePlayer(town,140,2,15));var other=ReloadBall(WelcomePlayer(town,140,2,15));
        int maps=Kernel.Maps.Count,metadata=MapsTable.MapInformations.Count;
        var original=MonsterInformation.MonsterInformations[3147];string name=original.Name;uint health=original.Hitpoints;byte level=original.Level;
        for(int kind=0;kind<7;kind++){
            var profile=(MonsterInformation)Call(type,null,"Profile",kind);
            Check(profile.ID==9918400u+kind && !profile.Boss && !profile.Guard && !profile.Reviver && !(bool)Field(profile,"LabirinthDrop")
                && profile.OwnItemID==0 && profile.MinMoneyDropAmount==0 && profile.MaxMoneyDropAmount==0 && profile.HPPotionID==0 && profile.MPPotionID==0,
                "Power Book private monster has no inherited boss or catalog loot: "+kind);
            if(kind==4)Check(profile.Name=="BlueBird" && profile.Level==115 && profile.Hitpoints==45000 && profile.Mesh==original.Mesh,
                "Power Book Blue Bird uses explicit trial stats, preserving only catalog appearance");
        }
        Check(original.Name==name && original.Level==level && original.Hitpoints==health,"Power Book profiles leave shared AzureBird boss unchanged");
        for(int floor=0;floor<4;floor++){
            object scene;lock(p.ItemSyncRoot)scene=Call(type,null,"Create",p,floor);var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1500+floor && map.ID!=map.BaseID && map.Entities.Count==0 && map.Npcs.Count==0 && map.Portals.Count==0
                && MapsTable.MapInformations[map.ID].Owner==p.Entity.UID,"Power Book floor is an empty owner-specific dynamic map: "+floor);
            Check((bool)Call(type,null,"Registered",scene,p) && !(bool)Call(type,null,"Registered",scene,other),"Power Book scene rejects unrelated owner: "+floor);
            bool rejected=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p,floor);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Power Book duplicate owner reservation refused: "+floor);
            ushort prior=p.Entity.MapID;p.Entity.MapID=map.ID;
            rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}finally{p.Entity.MapID=prior;}
            Check(rejected && Kernel.Maps.ContainsKey(map.ID),"Power Book cleanup cannot strand an occupant: "+floor);
            var ownedMetadata=MapsTable.MapInformations[map.ID];var replacement=new MapsTable.MapInformation{ID=map.ID,Owner=other.Entity.UID};MapsTable.MapInformations[map.ID]=replacement;
            rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && MapsTable.MapInformations[map.ID].Equals(replacement),"Power Book release preserves replaced metadata: "+floor);
            MapsTable.MapInformations[map.ID]=ownedMetadata;
            Call(type,null,"Release",scene);Call(type,null,"Release",scene);
            Check(!Kernel.Maps.ContainsKey(map.ID) && !MapsTable.MapInformations.ContainsKey(map.ID),"Power Book release removes only owned map metadata: "+floor);
        }
        bool invalid=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p,4);}catch(ArgumentOutOfRangeException){invalid=true;}
        Check(invalid && Kernel.Maps.Count==maps && MapsTable.MapInformations.Count==metadata,"Power Book invalid floor and repeated allocations leave no registry leaks");
        p.Fake=true;invalid=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p,0);}catch(InvalidOperationException){invalid=true;}finally{p.Fake=false;}
        Check(invalid,"Power Book scene rejects fake sessions");
    }

    static void AzureJournalCheck(GameState p,int stage){
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,list);
        var rows=list.Where(x=>(uint)x.UID>=1727 && (uint)x.UID<=1732).ToArray();
        Check(rows.Length==6 && rows.Select(x=>x.UID).Distinct().Count()==6 && rows.All(x=>{
            int step=(int)x.UID-1726;
            return x.Status==(stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available);
        }),"Azure journal projects persisted stage "+stage);
    }
    static void VerifyAzureQuest(string runtime,Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.AzurePillQuest",true);
        var map=World(runtime,1011);Call(service,null,"Create");
        var clock=service.GetField("Clock",Hidden);var drop=service.GetField("Drop",Hidden);object oldClock=clock.GetValue(null),oldDrop=drop.GetValue(null);
        var now=new DateTime(2036,1,1,12,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));drop.SetValue(null,(Func<bool>)(()=>true));
        uint chang=900071727,wu=900071728,mu=900071729,bian=900071730;
        Action<GameState,uint> visit=(p,npc)=>{ushort id=npc==wu || npc==mu?(ushort)1011:(ushort)1002;p.BindWorldMap(id);var n=p.Map.Npcs[npc];p.Entity.X=n.X;p.Entity.Y=n.Y;p.ActiveNpc=npc;};
        Action<GameState,uint> talk=(p,npc)=>{visit(p,npc);Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=1},p);};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_azure_pill WHERE uid=@uid",p.Entity.UID);
        try {
            for(byte life=0;life<3;life++) {
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)50:(byte)140,life,15));ulong beforeGold=p.Entity.Money;
                p.Quests=new Quests(p);p.Quests.Load();AzureJournalCheck(p,0);
                for(uint mission=1727;mission<=1732;mission++){
                    var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};
                    begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));
                    MsgTaskStatus.Process(begin.ToArray(),p);
                    Check(!p.Quests.HasQuest((QuestID)mission),"Azure client Begin cannot accept or finish stage "+mission);
                }
                talk(p,chang);AzureJournalCheck(p,1);talk(p,chang);Check(stage(p)==1,"Azure ordinary acceptance is once per rebirth: "+life);
                talk(p,wu);AzureJournalCheck(p,2);talk(p,mu);AzureJournalCheck(p,3);Check(stage(p)==3 && p.Entity.Money==beforeGold+200,"Azure scholar/pharmacist stages persist silver: "+life);
                talk(p,bian);talk(p,bian);Check(CountId(p,721697)==2,"Azure herbs issued once: "+life);
                p.BindWorldMap(1011);p.Entity.X=550;p.Entity.Y=300;
                for(int i=0;i<4;i++){var mob=HuntMonster(p,"FireRatL38");Call(service,null,"OnDeath",p,mob);Call(service,null,"OnDeath",p,mob);}
                Check(!Fenced(p) && CountId(p,721695)==1 && CountId(p,721696)==3,"Azure four owned deaths earn exact ingredients without corpse replay: "+life);
                EntityTable.SaveEntity(p);p=ReloadBall(p);talk(p,mu);
                AzureJournalCheck(p,4);Check(stage(p)==4 && CountId(p,721695)==0 && CountId(p,721696)==0 && CountId(p,721697)==0,"Azure brew consumes exact saved materials together: "+life);
                talk(p,mu);Check(stage(p)==4 && CountId(p,721698)==0,"Azure brewing deadline cannot be skipped: "+life);
                now=now.AddMinutes(2);p=ReloadBall(p);talk(p,mu);
                AzureJournalCheck(p,5);Check(stage(p)==5 && CountId(p,721698)==1,"Azure brewing deadline survives reconnect: "+life);
                ulong oldXp=p.Entity.Experience;byte oldLevel=p.Entity.Level;talk(p,wu);
                AzureJournalCheck(p,6);Check(!Fenced(p) && stage(p)==6 && CountId(p,721698)==0 && p.Entity.Money==beforeGold+1900
                    && (oldLevel>=Revora.Config.LevelCap || p.Entity.Level>oldLevel || p.Entity.Experience>oldXp),"Azure delivery consumes pill and saves silver/EXP: "+life);
                if(life==0){
                    Query("ALTER TABLE items ADD CONSTRAINT azure_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                    try{talk(p,chang);}finally{Query("ALTER TABLE items DROP CONSTRAINT azure_final_fault");}
                    Check(Fenced(p) && stage(p)==6 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=728238",p.Entity.UID)==0,"Azure late soul failure rolls back ring pack and completion");
                    p=ReloadBall(p);
                }
                talk(p,chang);
                Check(!Fenced(p) && stage(p)==7 && CountId(p,728238)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,
                    "Azure earned completion grants ring pack and bound P7: "+life);
                var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);talk(p,chang);
                AzureJournalCheck(p,7);Check(stage(p)==7 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids) && p.Entity.Money==beforeGold+1900,
                    "Azure exact rewards survive reload and completion replay: "+life);
                Call(FixedPacks,null,"Initialize");OpenFixed(p,p.Inventory.Objects.Single(i=>i.ID==728238));
                Check(CountId(p,151077)==1 && p.Inventory.Objects.Single(i=>i.ID==151077).Bound,"Azure earned ring pack opens through normal item use: "+life);
            }
        }finally{clock.SetValue(null,oldClock);drop.SetValue(null,oldDrop);}
    }

    static void ConnectorJournalCheck(GameState p,int stage){
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,list);
        var rows=list.Where(x=>(uint)x.UID>=1718 && (uint)x.UID<=1722).ToArray();
        Check(rows.Length==5 && rows.Select(x=>x.UID).Distinct().Count()==5 && rows.All(x=>{
            int step=(int)x.UID-1717;
            return x.Status==(stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available);
        }),"Connector journal projects saved stage "+stage);
    }
    static void VerifyConnectorQuest(string runtime,Map town){
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ConnectorQuest",true);World(runtime,1011);Call(service,null,"Create");Call(FixedPacks,null,"Initialize");
        CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==4437?(ushort)1011:(ushort)1002);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Func<GameState,long> stage=p=>{long value=Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_connector WHERE uid=@uid",p.Entity.UID);if(!Fenced(p))ConnectorJournalCheck(p,(int)value);return value;};
        Func<GameState,string,Entity> kill=(p,name)=>{var body=HuntMonster(p,name);body.MapObjType=MapObjectType.Monster;body.Hitpoints=body.MaxHitpoints=body.MonsterInfo.Hitpoints;body.IsDropped=true;
            uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);return body;};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)22:(byte)140,life,15));ulong money=p.Entity.Money;
            p.ActiveNpc=900071727;p.Entity.X=400;p.Entity.Y=400;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=51},p);
            Check(stage(p)==0,"Connector remote request cannot accept: "+life);
            p.Quests=new Quests(p);p.Quests.Load();
            for(uint mission=1718;mission<=1722;mission++){
                var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);
                Check(!p.Quests.HasQuest((QuestID)mission),"Connector client Begin cannot forge mission "+mission);
            }
            talk(p,900071727,51);talk(p,900071727,51);Check(stage(p)==1 && CountId(p,721715)==1,"Connector ordinary acceptance issues one badge: "+life);
            if(life==0){
                var badge=p.Inventory.Objects.Single(i=>i.ID==721715);ItemHandler.DropItem(new ItemUsage(true){UID=badge.UID},p);
                Check(CountId(p,721715)==0,"Connector ordinary bound-badge disposal deletes the source");
                talk(p,900071727,52);uint restored=p.Inventory.Objects.Single(i=>i.ID==721715).UID;talk(p,900071727,52);
                Check(!Fenced(p) && restored!=badge.UID && CountId(p,721715)==1 && Scalar("SELECT COUNT(*) FROM revora_connector_recoveries WHERE uid=@uid",p.Entity.UID)==1,"Connector badge recovery is durable and duplicate safe");
            }
            talk(p,4437,1);Check(stage(p)==1,"Connector cannot skip Wong's referral: "+life);
            talk(p,900071718,1);talk(p,4437,1);Check(stage(p)==3 && p.Entity.Money==money+200,"Connector referrals persist two silver steps: "+life);
            p.Entity.X=58;p.Entity.Y=388;var wrong=kill(p,"Bandit");Call(service,null,"OnDeath",p,wrong);
            Check(Scalar("SELECT kills FROM revora_connector WHERE uid=@uid",p.Entity.UID)==0,"Connector ordinary Bandit family earns no Castle Bandit credit: "+life);
            for(int i=0;i<4;i++){var body=kill(p,"CastleBandit");Call(service,null,"OnDeath",p,body);}
            talk(p,4437,1);Check(!Fenced(p) && stage(p)==3 && CountId(p,729942)==0 && Scalar("SELECT kills FROM revora_connector WHERE uid=@uid",p.Entity.UID)==4,"Connector four real deaths and replays cannot issue letter: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);p.Entity.X=58;p.Entity.Y=388;kill(p,"CastleBandit");
            if(life==0){
                while(p.Inventory.Count<40)Pack(p,1088000,1);talk(p,4437,1);
                Check(!Fenced(p) && stage(p)==3 && CountId(p,729942)==0 && Scalar("SELECT kills FROM revora_connector WHERE uid=@uid",p.Entity.UID)==5,"Connector full bag preserves five earned kills for letter retry");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
            }
            talk(p,4437,1);talk(p,4437,1);
            Check(!Fenced(p) && stage(p)==4 && CountId(p,729942)==1 && p.Entity.Money==money+300,"Connector fifth kill after reload earns one letter: "+life);
            if(life==0){
                uint letter=p.Inventory.Objects.Single(i=>i.ID==729942).UID;
                Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)letter);p=ReloadBall(p);talk(p,4437,2);
                Check(!Fenced(p) && CountId(p,729942)==0 && Scalar("SELECT letter FROM revora_connector WHERE uid=@uid",p.Entity.UID)==letter,"Connector stored letter cannot be copied by recovery");
                Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)letter);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=letter},p);talk(p,4437,2);talk(p,4437,2);
                Check(!Fenced(p) && CountId(p,729942)==1 && p.Inventory.Objects.Single(i=>i.ID==729942).UID!=letter && Scalar("SELECT kills FROM revora_connector WHERE uid=@uid",p.Entity.UID)==5,"Connector discarded earned letter is restored once without replaying the hunt");
            }
            ulong xp=p.Entity.Experience;byte level=p.Entity.Level;talk(p,900071718,1);
            Check(stage(p)==5 && CountId(p,729942)==0 && p.Entity.Money==money+1300 && (level>=Config.LevelCap || p.Entity.Level>level || p.Entity.Experience>xp),"Connector letter hand-in saves silver and ten-minute EXP: "+life);
            if(life==0){
                while(p.Inventory.Count<39)Pack(p,1088000,1);talk(p,900071727,51);
                Check(!Fenced(p) && stage(p)==5 && CountId(p,721715)==1 && CountId(p,728236)==0,"Connector final claim refuses insufficient space without consuming badge");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT connector_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                try{talk(p,900071727,51);}finally{Query("ALTER TABLE items DROP CONSTRAINT connector_final_fault");}
                Check(Fenced(p) && stage(p)==5 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728236",p.Entity.UID)==0,"Connector final failure rolls back pack, EXP Ball, badge removal and completion");p=ReloadBall(p);
            }
            talk(p,900071727,51);talk(p,900071727,51);
            Check(!Fenced(p) && stage(p)==6 && CountId(p,728236)==1 && CountId(p,723911)==1 && CountId(p,721715)==0 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Connector delivers one bound pack, EXP Ball and P7 without replay: "+life);
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);
            Check(stage(p)==6 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids) && p.Entity.Money==money+1300,"Connector completion and exact earned rewards survive reload: "+life);
            talk(p,900071727,52);Check(CountId(p,721715)==0 && CountId(p,729942)==0,"Connector completed recovery cannot recreate consumed quest items: "+life);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728236),p);p=ReloadBall(p);
            Check(!Fenced(p) && CountId(p,120046)==1 && p.Inventory.Objects.Single(i=>i.ID==120046).Bound,"Connector earned pack opens to bound necklace through normal item dispatch: "+life);
        }
    }

    static void VerifyMysticQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.MysticRingQuest",true);var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestAppearance",true);
        World(runtime,1011);Call(FixedPacks,null,"Initialize");CityGuardSystem.SpawnTalker(4501,1011,779,477,6767,"DealerShen",7);var shen=Kernel.Maps[1011].Npcs[4501];Call(quest,null,"Initialize");
        Check(ReferenceEquals(shen,Kernel.Maps[1011].Npcs[4501]) && Kernel.Maps[1011].Npcs.ContainsKey(900071742),"Mystic initialization reuses Shen's oriented actor and adds the separate Junior alias");
        CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==900071727?(ushort)1002:(ushort)1011);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID);
        Action<GameState> journal=p=>{int current=stage(p);int step=current<=4?current:current<=6?5:current<=8?6:current==9?7:8;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1738 && (uint)x.UID<=1744).ToArray();
            Check(own.Length==7 && own.All(x=>x.Status==(step>(int)x.UID-1737?MsgTaskStatus.QuestData.QuestStatus.Finished:step==(int)x.UID-1737?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Mystic saved journal stage "+current);};
        Action<GameState,string> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);Call(quest,null,"OnDeath",p,b);};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)48:(byte)140,life,15));ulong money=p.Entity.Money;p.Quests=new Quests(p);p.Quests.Load();
            p.ActiveNpc=900071727;p.Entity.X=400;p.Entity.Y=400;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=81},p);Check(stage(p)==0,"Mystic remote NPC acceptance rejected: "+life);
            for(uint mission=1738;mission<=1744;mission++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)mission),"Mystic forged Begin rejected: "+mission);}
            talk(p,900071727,81);talk(p,4501,1);Check(stage(p)==1,"Mystic acceptance cannot skip Minstrel: "+life);journal(p);
            talk(p,4500,1);talk(p,4501,1);talk(p,4501,1);Check(stage(p)==3 && p.Entity.Money==money+200,"Mystic referrals stop at missing medicines: "+life);journal(p);
            var health=NestedPack(p,1000000,15,false);var mana=NestedPack(p,1001000,12,true);talk(p,4501,1);talk(p,4501,1);Check(stage(p)==4 && health.StackSize==3 && !p.Inventory.Objects.Any(i=>i.UID==mana.UID),"Mystic NPC medicine route consumes exactly twelve units each: "+life);
            p.Entity.X=610;p.Entity.Y=429;for(int i=0;i<19;i++)kill(p,i%2==0?"Ratling":"FireRatL38");talk(p,4501,1);
            Check(stage(p)==4 && Scalar("SELECT kills FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID)==19,"Mystic ordinary death hook counts 19 without duplicate credit: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);p.Entity.X=610;p.Entity.Y=429;kill(p,"Ratling");talk(p,4501,1);Check(stage(p)==5 && CountId(p,721710)==1,"Mystic twentieth kill after reconnect earns the quest scroll: "+life);journal(p);
            if(life==0){var scroll=p.Inventory.Objects.Single(i=>i.ID==721710);ItemHandler.DropItem(new ItemUsage(true){UID=scroll.UID},p);talk(p,4501,2);talk(p,4501,2);Check(stage(p)==5 && CountId(p,721710)==1 && p.Inventory.Objects.Single(i=>i.ID==721710).UID!=scroll.UID,"Mystic NPC recovery replaces a discarded scroll exactly once");}
            talk(p,900071742,1);talk(p,900071742,1);Check(stage(p)==6 && CountId(p,721710)==0 && CountId(p,721707)==1,"Mystic ordinary Junior dialogue exchanges the owned quest scroll: "+life);journal(p);
            talk(p,4500,1);Check(stage(p)==6,"Mystic cannot skip the return to Dealer Shen: "+life);talk(p,4501,1);talk(p,4500,1);
            Check(stage(p)==8 && CountId(p,721707)==0 && CountId(p,721708)==1 && p.Entity.Money==money+500,"Mystic ordinary Shen/Minstrel exchanges reach the usable ring: "+life);journal(p);
            var ring=p.Inventory.Objects.Single(i=>i.ID==721708);p.Entity.X=610;p.Entity.Y=429;ItemHandler.UseItem(ring,p);Check(stage(p)==8 && CountId(p,721708)==1,"Mystic ring use away from Minstrel is rejected: "+life);
            talk(p,4500,0);ItemHandler.UseItem(ring,p);ItemHandler.UseItem(ring,p);Check(!Fenced(p) && stage(p)==9 && CountId(p,721708)==0 && p.Entity.Mesh==30040 && p.Entity.Money==money+2000,"Mystic normal ring use saves cosmetic completion and silver once: "+life);journal(p);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT mystic_npc_claim_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,81);}finally{Query("ALTER TABLE items DROP CONSTRAINT mystic_npc_claim_fault");}
                Check(Fenced(p) && stage(p)==9 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728240",p.Entity.UID)==0,"Mystic NPC claim failure fences the session without partial rewards");p=ReloadBall(p);Call(appearance,null,"Tick");}
            talk(p,900071727,81);talk(p,900071727,81);Call(appearance,null,"Tick");Check(!Fenced(p) && stage(p)==10 && CountId(p,728240)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && p.Entity.Mesh!=30040,"Mystic ordinary claim delivers bound pack/P7 and map cleanup restores appearance: "+life);journal(p);
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);talk(p,900071727,82);Check(stage(p)==10 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids),"Mystic completed recovery cannot recreate rings after reload: "+life);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728240),p);Check(!Fenced(p) && CountId(p,118057)==1 && p.Inventory.Objects.Single(i=>i.ID==118057).Bound,"Mystic ordinary earned headgear pack opens: "+life);
        }
    }

    static void VerifyMysticProgression(string runtime,Map town,bool finish=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MysticRingStore",true);World(runtime,1011);Call(store,null,"Initialize");
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,a<4?"Advance":"Progress",p,a,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID);
        Func<GameState,long> kills=p=>Scalar("SELECT kills FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)48:(byte)140,life,15));ulong money=p.Entity.Money;step(p,0);step(p,1);step(p,2);p.BindWorldMap(1011);p.Entity.X=610;p.Entity.Y=429;
            var early=kill(p,"Ratling");Check(!credit(p,early),"Mystic pre-medicine death does not count: "+life);NestedPack(p,1000000,12,false);NestedPack(p,1001000,12,true);step(p,3);
            Check(!credit(p,early) && !credit(p,kill(p,"Bandit")) && kills(p)==0,"Mystic prior-stage replay and wrong family cannot count: "+life);
            for(int i=0;i<19;i++){var b=kill(p,i%2==0?"Ratling":"FireRatL38");credit(p,b);credit(p,b);}
            Check(kills(p)==19 && !step(p,4) && CountId(p,721710)==0,"Mystic nineteen real deaths cannot earn the scroll: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);credit(p,kill(p,"Ratling"));credit(p,kill(p,"Ratling"));Check(kills(p)==20,"Mystic twentieth kill after reload caps at twenty: "+life);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!step(p,4) && stage(p)==4 && kills(p)==20,"Mystic full bag preserves hunt credit for scroll retry");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);}
            Check(step(p,4) && !step(p,4) && stage(p)==5 && CountId(p,721710)==1 && p.Entity.Money==money+400,"Mystic completed hunt issues one owned quest scroll: "+life);
            if(life==0){uint scroll=p.Inventory.Objects.Single(i=>i.ID==721710).UID;ItemHandler.DropItem(new ItemUsage(true){UID=scroll},p);NestedPack(p,1060023,1,false);
                Check(!step(p,5) && CountId(p,1060023)==1,"Mystic ordinary teleport scroll cannot replace the lost quest scroll");Check(recover(p) && !recover(p) && CountId(p,721710)==1,"Mystic lost quest scroll is replaced once");}
            Check(step(p,5) && !step(p,5) && CountId(p,721710)==0 && CountId(p,721707)==1 && stage(p)==6,"Mystic Junior exchange consumes exact scroll and issues Antique Ring: "+life);
            if(life==0){uint antique=p.Inventory.Objects.Single(i=>i.ID==721707).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)antique);p=ReloadBall(p);
                Check(!recover(p) && !step(p,6),"Mystic stored Antique Ring cannot be duplicated or handed in");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)antique);p=ReloadBall(p);}
            Check(step(p,6) && !step(p,6) && stage(p)==7 && CountId(p,721707)==1 && p.Entity.Money==money+500,"Mystic return to Shen preserves Antique Ring and pays once: "+life);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT mystic_ring_fault CHECK(id<>721708) NOT VALID");bool failed=false;try{step(p,7);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mystic_ring_fault");}
                Check(failed && stage(p)==7 && CountId(p,721707)==1 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=721708",p.Entity.UID)==0,"Mystic ring insertion failure rolls back Antique Ring consumption");p=ReloadBall(p);while(p.Inventory.Count<40)Pack(p,1088000,1);}
            Check(step(p,7) && !step(p,7) && stage(p)==8 && CountId(p,721707)==0 && CountId(p,721708)==1,"Mystic Minstrel exchanges Antique Ring atomically even with a full bag: "+life);
            var ring=p.Inventory.Objects.Single(i=>i.ID==721708);EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage(p)==8 && p.Inventory.Objects.Single(i=>i.UID==ring.UID).Bound && !step(p,8),"Mystic saved ring cannot bypass the pending cosmetic-use stage: "+life);
            ItemHandler.DropItem(new ItemUsage(true){UID=ring.UID},p);Check(recover(p) && !recover(p) && CountId(p,721708)==1 && kills(p)==20,"Mystic lost usable ring recovers once without resetting the hunt: "+life);
            if(finish)VerifyMysticRewardFinish(p,money,life);
        }
    }

    static void VerifyMysticRewardFinish(GameState p,ulong initialMoney,byte life){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MysticRingStore",true);var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestAppearance",true);
        var spun=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldAppearance",true);Call(FixedPacks,null,"Initialize");
        Func<bool> use=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"UseRing",p,p.Inventory.Objects.Single(i=>i.ID==721708).UID,(Func<bool>)(()=>true));};
        Func<bool> claim=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true));};
        Func<long> stage=()=>Scalar("SELECT stage FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID);
        uint original=p.Entity.Mesh,hp=p.Entity.Hitpoints,max=p.Entity.MaxHitpoints;ushort body=p.Entity.Body,face=p.Entity.Face;Guid token;
        lock(p.ItemSyncRoot)token=(Guid)Call(appearance,null,"Apply",p,30040u,(Func<bool>)(()=>true));
        Check(token!=Guid.Empty && p.Entity.Mesh==30040 && p.Entity.CharBuffer._Mesh==30040 && p.Entity.Body==body && p.Entity.Face==face && p.Entity.Hitpoints==hp && p.Entity.MaxHitpoints==max,"Mystic cosmetic mesh projects without changing identity or health: "+life);
        lock(p.ItemSyncRoot){Call(appearance,null,"Clear",p,token,2562005u);Check(p.Entity.Mesh==30040,"Mystic wrong-kind cleanup cannot clear mouse lease: "+life);Call(appearance,null,"Clear",p,token,30040u);token=(Guid)Call(spun,null,"Apply",p,(Func<bool>)(()=>true));}
        Check(!use() && stage()==8 && CountId(p,721708)==1 && p.Entity.Mesh==2562005,"Mystic ring cannot overwrite Spun Gold disguise or charge while busy: "+life);
        lock(p.ItemSyncRoot)Call(spun,null,"Clear",p,token);
        if(life==0){
            Query("ALTER TABLE revora_mystic_ring ADD CONSTRAINT mystic_use_fault CHECK(stage<>9) NOT VALID");bool failed=false;try{use();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_mystic_ring DROP CONSTRAINT mystic_use_fault");}
            Check(failed && stage()==8 && CountId(p,721708)==1 && p.Entity.Mesh==original && p.Entity.Money==initialMoney+500,"Mystic failed ring transaction restores appearance and retains ring/progress/silver");
            p=ReloadBall(p);Check(CountId(p,721708)==1 && stage()==8,"Mystic failed ring use preserves persisted source");
        }
        ulong xp=p.Entity.Experience;byte level=p.Entity.Level;uint source=p.Inventory.Objects.Single(i=>i.ID==721708).UID;
        Check(use() && stage()==9 && CountId(p,721708)==0 && p.Entity.Mesh==30040 && p.Entity.Money==initialMoney+2000 && (level>=Config.LevelCap || p.Entity.Level>level || p.Entity.Experience>xp),"Mystic owned ring use commits appearance stage, silver and twenty-minute EXP: "+life);
        lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"UseRing",p,source,(Func<bool>)(()=>true)) && (Guid)Call(spun,null,"Apply",p,(Func<bool>)(()=>true))==Guid.Empty,"Mystic consumed ring cannot replay and Spun Gold cannot overwrite mouse: "+life);
        var clock=spun.GetField("Clock",Hidden);var previous=clock.GetValue(null);clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddMinutes(6)));
        try{Call(appearance,null,"Tick");}finally{clock.SetValue(null,previous);}
        Check(p.Entity.Mesh==(uint)(p.Entity.Face*10000+p.Entity.Body) && stage()==9,"Mystic expiry restores identity without undoing earned completion: "+life);
        if(life==0){
            Check(p.Inventory.Count==39 && !claim() && stage()==9,"Mystic final claim waits for two reward slots");
            foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT mystic_reward_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{claim();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mystic_reward_fault");}
            Check(failed && stage()==9 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728240",p.Entity.UID)==0,"Mystic failed P7 insert rolls back headgear pack and completion");
        }
        int wins=0;Parallel.For(0,4,i=>{if(claim())System.Threading.Interlocked.Increment(ref wins);});
        Check(wins==1 && stage()==10 && CountId(p,728240)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Mystic concurrent final claims issue exactly one bound pack and P7: "+life);
        var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);
        Check(stage()==10 && p.Entity.Money==initialMoney+2000 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids),"Mystic final rewards and completion survive reload: "+life);
        ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728240),p);Check(!Fenced(p) && CountId(p,118057)==1 && p.Inventory.Objects.Single(i=>i.ID==118057).Bound,"Mystic earned headgear pack opens through normal dispatch: "+life);
    }

    static void VerifyMysticMedicine(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MysticRingStore",true);Call(store,null,"Initialize");
        Func<GameState,int,bool> advance=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_mystic_ring WHERE uid=@uid",p.Entity.UID);
        Func<GameState> prepare=()=>{var p=ReloadBall(WelcomePlayer(town,48,0,15));advance(p,0);advance(p,1);advance(p,2);return p;};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)48:(byte)140,life,15));ulong money=p.Entity.Money;
            Check(advance(p,0) && !advance(p,0) && !advance(p,3) && advance(p,1) && advance(p,2),"Mystic referrals are ordered and once per life: "+life);
            var bound=NestedPack(p,1000000,7,true);var remainder=NestedPack(p,1000000,8,false);var mana=NestedPack(p,1001000,20,false);
            int successes=0;Parallel.For(0,4,i=>{if(advance(p,3))System.Threading.Interlocked.Increment(ref successes);});
            Check(successes==1 && stage(p)==4 && p.Entity.Money==money+300 && !p.Inventory.Objects.Any(i=>i.UID==bound.UID) && remainder.StackSize==3 && mana.StackSize==8,"Mystic consumes exactly twelve of each medicine with bound-first partial stacks: "+life);
            Check(Scalar("SELECT SUM(taken) FROM revora_mystic_medicine WHERE uid=@uid",p.Entity.UID)==24 && Scalar("SELECT COUNT(*) FROM revora_mystic_medicine WHERE uid=@uid",p.Entity.UID)==3,"Mystic exact source receipts commit with the hand-in: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage(p)==4 && p.Entity.Money==money+300 && p.Inventory.Objects.Single(i=>i.UID==remainder.UID).StackSize==3 && p.Inventory.Objects.Single(i=>i.UID==mana.UID).StackSize==8 && !advance(p,3),"Mystic progress, silver and partial stacks survive reload without replay: "+life);
        }
        var shortBag=prepare();NestedPack(shortBag,1000000,11,false);NestedPack(shortBag,1001000,12,true);ulong shortMoney=shortBag.Entity.Money;
        Check(!advance(shortBag,3) && stage(shortBag)==3 && shortBag.Entity.Money==shortMoney && Scalar("SELECT COUNT(*) FROM revora_mystic_medicine WHERE uid=@uid",shortBag.Entity.UID)==0,"Mystic insufficient Stanchers consume neither medicine");
        var locked=prepare();var health=NestedPack(locked,1000000,12,false);NestedPack(locked,1001000,12,false);Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)health.UID);locked=ReloadBall(locked);
        Check(!advance(locked,3) && stage(locked)==3,"Mystic locked medicine is not selected");
        var failure=prepare();var hp=NestedPack(failure,1000000,15,false);var mp=NestedPack(failure,1001000,14,false);ulong before=failure.Entity.Money;
        Query("ALTER TABLE revora_mystic_medicine ADD CONSTRAINT mystic_receipt_fault CHECK(item_type<>1001000) NOT VALID");bool failed=false;
        try{advance(failure,3);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_mystic_medicine DROP CONSTRAINT mystic_receipt_fault");}
        Check(failed && stage(failure)==3 && hp.StackSize==15 && mp.StackSize==14 && failure.Entity.Money==before && Scalar("SELECT COUNT(*) FROM revora_mystic_medicine WHERE uid=@uid",failure.Entity.UID)==0,"Mystic second medicine receipt failure rolls back both debits and progress");
        failure=ReloadBall(failure);Check(failure.Inventory.Objects.Single(i=>i.UID==hp.UID).StackSize==15 && failure.Inventory.Objects.Single(i=>i.UID==mp.UID).StackSize==14,"Mystic failed hand-in preserves persisted stacks");
        int contexts=0;bool rejected=false;try{lock(failure.ItemSyncRoot)Call(store,null,"Advance",failure,3,(Func<bool>)(()=>++contexts==1));}catch(InvalidOperationException){rejected=true;}
        Check(rejected && stage(failure)==3 && Scalar("SELECT COUNT(*) FROM revora_mystic_medicine WHERE uid=@uid",failure.Entity.UID)==0,"Mystic changed end context rolls back all medicine writes");
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)hp.UID);bool stale=false;try{advance(failure,3);}catch(InvalidOperationException){stale=true;}finally{Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)hp.UID);}
        Check(stale && stage(failure)==3 && Scalar("SELECT COUNT(*) FROM revora_mystic_medicine WHERE uid=@uid",failure.Entity.UID)==0,"Mystic stale persisted lock cannot be spent from a usable memory snapshot");
        Check(advance(failure,3) && stage(failure)==4 && failure.Entity.Money==before+100,"Mystic rolled-back medicine hand-in retries once after valid ownership returns");
    }

    static void VerifyArtisanQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArtisanFeatherQuest",true);World(runtime,1020);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");
        CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var random=quest.GetField("Roll",Hidden);var previous=random.GetValue(null);random.SetValue(null,(Func<int,int>)(n=>0));
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(1002);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Func<GameState,long> stage=p=>Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_artisan_feather WHERE uid=@uid",p.Entity.UID);
        Func<GameState,long> count=p=>Scalar("SELECT feathers FROM revora_artisan_feather WHERE uid=@uid",p.Entity.UID);
        Func<GameState,string,Entity> kill=(p,name)=>{var body=HuntMonster(p,name);body.MapObjType=MapObjectType.Monster;body.Hitpoints=body.MaxHitpoints=body.MonsterInfo.Hitpoints;body.IsDropped=true;
            uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);return body;};
        Action<GameState> journal=p=>{long current=stage(p);var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1733 && (uint)x.UID<=1737).ToArray();
            Check(own.Length==5 && own.All(x=>x.Status==(current>(int)x.UID-1732?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1732?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Artisan saved journal stage "+current);};
        try{for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)45:(byte)140,life,15));ulong money=p.Entity.Money;p.Quests=new Quests(p);p.Quests.Load();
            p.ActiveNpc=900071727;p.Entity.X=400;p.Entity.Y=400;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=71},p);Check(stage(p)==0,"Artisan remote acceptance denied: "+life);
            for(uint mission=1733;mission<=1737;mission++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)mission),"Artisan forged Begin rejected: "+mission);}
            talk(p,900071727,71);talk(p,900071727,71);talk(p,4467,1);Check(stage(p)==1,"Artisan ordinary acceptance cannot skip Apprentice Luo: "+life);journal(p);
            talk(p,4466,1);talk(p,4467,1);Check(stage(p)==3 && p.Entity.Money==money+200,"Artisan ordinary Luo referrals pay once: "+life);journal(p);
            p.Entity.X=295;p.Entity.Y=684;var wrong=kill(p,"RobinCub");Check(count(p)==0,"Artisan RobinCub does not count as a required bird: "+life);
            string[] birds={"Pheasant","Turtledove","Robin"};Entity last=null;
            for(int i=0;i<99;i++){last=kill(p,birds[i%3]);Call(quest,null,"OnDeath",p,last);}
            Check(count(p)==99,"Artisan 99 actual mixed bird deaths and replays count exactly 99: "+life);
            talk(p,4467,1);Check(stage(p)==3,"Artisan 99 feathers cannot skip the hundredth: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);p.Entity.X=295;p.Entity.Y=684;last=kill(p,"Robin");kill(p,"Pheasant");Check(count(p)==100,"Artisan hundredth kill after reload reaches the cap: "+life);
            talk(p,4467,1);talk(p,4467,1);Check(stage(p)==4 && p.Entity.Money==money+300,"Artisan hundred-feather hand-in enables Vulture hunt once: "+life);journal(p);
            p.BindWorldMap(1020);p.Entity.X=713;p.Entity.Y=678;
            if(life==0){
                while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"Vulture");Check(CountId(p,729976)==0 && count(p)==100,"Artisan full bag retains first hunt without issuing feather");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Call(quest,null,"OnDeath",p,full);Check(CountId(p,729976)==0,"Artisan full-bag death cannot be replayed after space clears");
                random.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"Vulture");random.SetValue(null,(Func<int,int>)(n=>0));Call(quest,null,"OnDeath",p,miss);Check(CountId(p,729976)==0,"Artisan failed drop roll cannot be rerolled on the same death");
            }
            var vulture=kill(p,"Vulture");Call(quest,null,"OnDeath",p,vulture);Check(CountId(p,729976)==1 && p.Inventory.Objects.Single(i=>i.ID==729976).Bound,"Artisan genuine Vulture kill awards one bound feather: "+life);
            if(life==0){
                uint feather=p.Inventory.Objects.Single(i=>i.ID==729976).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)feather);p=ReloadBall(p);talk(p,4467,2);
                Check(CountId(p,729976)==0 && Scalar("SELECT feather FROM revora_artisan_feather WHERE uid=@uid",p.Entity.UID)==feather,"Artisan stored Vulture Feather cannot be copied or reset");
                Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)feather);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=feather},p);talk(p,4467,2);talk(p,4467,2);
                Check(CountId(p,729976)==0 && count(p)==100 && Scalar("SELECT feather FROM revora_artisan_feather WHERE uid=@uid",p.Entity.UID)==0 && Scalar("SELECT COUNT(*) FROM revora_artisan_feather_recoveries WHERE uid=@uid",p.Entity.UID)==1,"Artisan disposal recovery preserves 100 feathers and requires a new Vulture hunt");
                p.BindWorldMap(1020);p.Entity.X=713;p.Entity.Y=678;kill(p,"Vulture");
            }
            ulong xp=p.Entity.Experience;byte level=p.Entity.Level;talk(p,4467,1);talk(p,4467,1);
            Check(stage(p)==5 && CountId(p,729976)==0 && p.Entity.Money==money+1800 && (level>=Config.LevelCap || p.Entity.Level>level || p.Entity.Experience>xp),"Artisan earned feather hand-in grants silver and twenty-minute EXP once: "+life);journal(p);
            if(life==0){
                while(p.Inventory.Count<39)Pack(p,1088000,1);talk(p,900071727,71);Check(stage(p)==5 && CountId(p,728239)==0,"Artisan final claim requires two reward slots");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT artisan_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                try{talk(p,900071727,71);}finally{Query("ALTER TABLE items DROP CONSTRAINT artisan_final_fault");}
                Check(Fenced(p) && stage(p)==5 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728239",p.Entity.UID)==0,"Artisan failed soul insert rolls back pack and completion");p=ReloadBall(p);
            }
            talk(p,900071727,71);talk(p,900071727,71);Check(!Fenced(p) && stage(p)==6 && CountId(p,728239)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Artisan ordinary final claim delivers one bound weapon pack and P7: "+life);journal(p);
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);talk(p,4467,2);
            Check(stage(p)==6 && p.Entity.Money==money+1800 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids),"Artisan completion/rewards persist and completed recovery creates nothing: "+life);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728239),p);Check(!Fenced(p) && CountId(p,410097)==1 && p.Inventory.Objects.Single(i=>i.ID==410097).Bound,"Artisan earned pack opens through ordinary dispatch: "+life);
        }}finally{random.SetValue(null,previous);}
    }

    static void VerifySpunGoldQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldQuest",true);
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldStore",true);
        World(runtime,1011);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var random=store.GetField("Roll",Hidden);var saved=random.GetValue(null);random.SetValue(null,(Func<int,int>)(n=>0));
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==900071727?(ushort)1002:(ushort)1011);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_spun_gold WHERE uid=@uid",p.Entity.UID);
        Action<GameState> journal=p=>{int current=stage(p);int step=current<3?current:current<7?3:current==7?4:5;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);
            var own=rows.Where(x=>(uint)x.UID>=1723 && (uint)x.UID<=1726).ToArray();Check(own.Length==4 && own.All(x=>x.Status==(step>(int)x.UID-1722?MsgTaskStatus.QuestData.QuestStatus.Finished:step==(int)x.UID-1722?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Spun Gold journal projects saved stage "+current);};
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)30:(byte)140,life,15));ulong money=p.Entity.Money;p.Quests=new Quests(p);p.Quests.Load();
            p.ActiveNpc=900071727;p.Entity.X=400;p.Entity.Y=400;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=61},p);Check(stage(p)==0,"Spun Gold remote NPC requests cannot accept: "+life);
            for(uint mission=1723;mission<=1726;mission++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)mission),"Spun Gold forged journal Begin rejected: "+mission);}
            talk(p,900071727,61);talk(p,900071727,61);Check(stage(p)==1,"Spun Gold ordinary Chang acceptance is duplicate safe: "+life);journal(p);
            talk(p,4465,1);Check(p.Entity.Mesh!=2562005,"Spun Gold Zhao cannot disguise a player before the Offering stage: "+life);
            talk(p,4464,1);Check(stage(p)==2 && p.Entity.Money==money+100,"Spun Gold Hong referral advances through NPC route: "+life);journal(p);
            var bandit=HuntMonster(p,"Bandit");bandit.MapObjType=MapObjectType.Monster;bandit.Hitpoints=bandit.MaxHitpoints=bandit.MonsterInfo.Hitpoints;bandit.IsDropped=true;hit(p,bandit);
            Check(CountId(p,729971)==1,"Spun Gold ordinary combat dispatch earns the invitation: "+life);
            talk(p,4464,1);Check(stage(p)==3 && CountId(p,729972)==1 && CountId(p,729971)==0,"Spun Gold Hong exchanges the earned invitation: "+life);journal(p);
            var offering=p.Inventory.Objects.Single(i=>i.ID==729972);p.Entity.X=100;p.Entity.Y=430;ItemHandler.UseItem(offering,p);Check(stage(p)==3 && CountId(p,729972)==1,"Spun Gold Offering without disguise stays unconsumed: "+life);
            talk(p,4465,1);Check(p.Entity.Mesh==2562005,"Spun Gold Zhao grants owned disguise through NPC route: "+life);
            ItemHandler.UseItem(offering,p);Check(stage(p)==3,"Spun Gold Offering away from altar cannot summon: "+life);
            p.Entity.X=100;p.Entity.Y=430;ItemHandler.UseItem(offering,p);Check(stage(p)==4 && CountId(p,729972)==0,"Spun Gold normal Offering use reserves the owned summon: "+life);
            var cloud=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9921723 && !b.Dead);hit(p,cloud);
            Check(stage(p)==5 && CountId(p,729973)==1,"Spun Gold actual Cloud death earns box through normal combat: "+life);journal(p);
            var box=p.Inventory.Objects.Single(i=>i.ID==729973);ItemHandler.UseItem(box,p);ItemHandler.UseItem(box,p);Check(stage(p)==6 && CountId(p,729974)==1 && CountId(p,729973)==0,"Spun Gold ordinary box dispatcher consumes exact box once: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"Tick");talk(p,4464,1);Check(stage(p)==7 && p.Entity.Money==money+1200 && CountId(p,729974)==0,"Spun Gold earned armor survives reload and Hong hand-in: "+life);journal(p);
            talk(p,900071727,61);talk(p,900071727,61);Check(!Fenced(p) && stage(p)==8 && CountId(p,728237)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Spun Gold ordinary final claim grants bound pack and P7 once: "+life);journal(p);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage(p)==8 && CountId(p,728237)==1,"Spun Gold final rewards survive normal login recovery: "+life);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728237),p);Check(!Fenced(p) && CountId(p,130027)==1 && p.Inventory.Objects.Single(i=>i.ID==130027).Bound,"Spun Gold earned class pack opens through ordinary item dispatch: "+life);
        }}finally{random.SetValue(null,saved);Call(quest,null,"Tick");}
    }

    static void VerifySpunGoldCombat(string runtime,Map town){
        var combat=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldCombat",true);
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldStore",true);
        var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldAppearance",true);
        World(runtime,1011);Call(store,null,"Initialize");
        var random=store.GetField("Roll",Hidden);var saved=random.GetValue(null);random.SetValue(null,(Func<int,int>)(n=>0));
        var clock=appearance.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);var now=new DateTime(2036,8,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        Action<GameState,Entity,uint> hit=(p,b,amount)=>Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref amount,null);
        Func<GameState> prepare=()=>{var p=ReloadBall(WelcomePlayer(town,40,0,15));p.BindWorldMap(1011);p.Entity.X=100;p.Entity.Y=430;
            lock(p.ItemSyncRoot){Call(store,null,"Act",p,0,0u,(Func<bool>)(()=>true));Call(store,null,"Act",p,1,0u,(Func<bool>)(()=>true));}
            var b=HuntMonster(p,"Bandit");b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;hit(p,b,b.Hitpoints);
            lock(p.ItemSyncRoot){Call(store,null,"Invitation",p,b,(Func<bool>)(()=>true));Call(store,null,"Act",p,2,0u,(Func<bool>)(()=>true));}return p;};
        Func<GameState,Entity> summon=p=>{lock(p.ItemSyncRoot){var token=(Guid)Call(appearance,null,"Apply",p,(Func<bool>)(()=>true));return (Entity)Call(combat,null,"Summon",p,token,(Func<bool>)(()=>true));}};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_spun_gold WHERE uid=@uid",p.Entity.UID);
        try{
            var p=prepare();var cloud=summon(p);Check(cloud!=null && cloud.Hitpoints==1600 && stage(p)==4 && CountId(p,729972)==0,"Spun Gold summon consumes Offering and registers a real Cloud body");
            Check(summon(p)==null,"Spun Gold owner cannot create a second active summon");
            var other=prepare();var otherCloud=summon(other);Check(otherCloud!=null && otherCloud.UID!=cloud.UID && (otherCloud.X!=cloud.X || otherCloud.Y!=cloud.Y),"Spun Gold concurrent owners get separate occupied cells and actors");
            uint hp=cloud.Hitpoints;hit(other,cloud,hp);Check(cloud.Hitpoints==hp && !cloud.Dead && stage(p)==4,"Spun Gold foreign player damage is blocked at the receive boundary");
            Check((bool)Call(combat,null,"BlockAttack",cloud,other.Entity) && !(bool)Call(combat,null,"BlockAttack",cloud,p.Entity),"Spun Gold Cloud attacks only its owner");
            var rogue=HuntMonster(p,"Bandit");rogue.MonsterInfo.ID=9921723;Check((bool)Call(combat,null,"BlockAttack",p.Entity,rogue),"Spun Gold unregistered private-family actors fail closed");p.Map.RemoveEntity(rogue);
            hit(p,cloud,hp);Check(cloud.Dead && stage(p)==5 && CountId(p,729973)==1 && ReferenceEquals(cloud.Killer,p.Entity),"Spun Gold ordinary lethal attack earns the saved Wooden Box");
            Call(combat,null,"TryDie",cloud,p.Entity);Check(CountId(p,729973)==1,"Spun Gold duplicate death cannot issue another box");
            var petOwner=prepare();var petCloud=summon(petOwner);
            var pet=new Entity(EntityFlag.Monster,false){UID=899723,MapID=1011,Owner=petOwner,Companion=true,X=petCloud.X,Y=petCloud.Y};
            pet.MonsterInfo=petCloud.MonsterInfo.Copy();pet.MonsterInfo.ID=2141;pet.MonsterInfo.Owner=pet;pet.Hitpoints=pet.MaxHitpoints=1000;
            petOwner.Companion=pet;
            Action petHit=()=>{uint damage=petCloud.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(pet,petCloud,new Attack(true){AttackType=Attack.Melee,Attacker=pet.UID,Attacked=petCloud.UID},ref damage,null);};
            try{
                petHit();Check(!petCloud.Dead && petCloud.Hitpoints==1600 && stage(petOwner)==4,"Spun Gold unregistered companion cannot damage or earn a box");
                petOwner.Map.Companions.Add(pet.UID,pet);
                pet.Owner=other;petHit();Check(!petCloud.Dead && petCloud.Hitpoints==1600 && stage(petOwner)==4,"Spun Gold foreign companion cannot claim an owned encounter");pet.Owner=petOwner;
                petOwner.Companion=null;petHit();Check(!petCloud.Dead && petCloud.Hitpoints==1600 && stage(petOwner)==4,"Spun Gold replaced companion cannot claim an owned encounter");petOwner.Companion=pet;
                pet.MapID=1002;petHit();Check(!petCloud.Dead && petCloud.Hitpoints==1600 && stage(petOwner)==4,"Spun Gold companion on another map cannot claim victory");pet.MapID=1011;
                Check(!(bool)Call(combat,null,"BlockAttack",petCloud,pet),"Spun Gold registered owner companion is an eligible fighter");
                petHit();Check(petCloud.Dead && stage(petOwner)==5 && CountId(petOwner,729973)==1 && ReferenceEquals(petCloud.Killer,petOwner.Entity),"Spun Gold companion lethal receive callback credits its owner exactly once");
                uint box=petOwner.Inventory.Objects.Single(i=>i.ID==729973).UID;
                Call(combat,null,"TryDie",petCloud,pet);Check(CountId(petOwner,729973)==1,"Spun Gold repeated companion death credit cannot duplicate the box");
                Check(EntityTable.SaveEntity(petOwner),"Spun Gold companion winner saves normally");
                var restored=ReloadBall(petOwner);Check(stage(restored)==5 && restored.Inventory.Objects.Single(i=>i.ID==729973).UID==box,"Spun Gold companion-earned box identity survives ordinary reload");
            }finally{petOwner.Map.Companions.Remove(pet.UID);petOwner.Companion=null;}
            now=now.AddSeconds(2);Call(combat,null,"Tick");Check(!p.Map.Entities.ContainsKey(cloud.UID) && stage(p)==5,"Spun Gold completed corpse cleanup preserves victory");
            now=now.AddMinutes(5);Call(combat,null,"Tick");Check(!other.Map.Entities.ContainsKey(otherCloud.UID) && stage(other)==3,"Spun Gold expired fight removes actor and restores Offering stage");
            lock(other.ItemSyncRoot)Check((bool)Call(store,null,"Recover",other,(Func<bool>)(()=>true)),"Spun Gold expired attempt recovers its consumed Offering");
            Call(appearance,null,"Tick");otherCloud=summon(other);other.BindWorldMap(1002);Call(combat,null,"Tick");Check(stage(other)==3 && !Kernel.Maps[1011].Entities.ContainsKey(otherCloud.UID),"Spun Gold map departure cleans actor and leaves retryable progress");
            var disconnected=prepare();var oldBody=summon(disconnected);EntityTable.SaveEntity(disconnected);var replacement=ReloadBall(disconnected);
            lock(replacement.ItemSyncRoot)Check(!(bool)Call(combat,null,"RecoverAttempt",replacement,(Func<bool>)(()=>true)),"Spun Gold replacement session waits for old runtime owner cleanup");
            Call(combat,null,"Tick");lock(replacement.ItemSyncRoot)Check((bool)Call(combat,null,"RecoverAttempt",replacement,(Func<bool>)(()=>true)) && stage(replacement)==3 && !Kernel.Maps[1011].Entities.ContainsKey(oldBody.UID),"Spun Gold reconnect retires old actor and recovers durable attempt");
        }finally{now=now.AddHours(1);Call(combat,null,"Tick");Call(appearance,null,"Tick");random.SetValue(null,saved);clock.SetValue(null,oldClock);}
    }

    static void VerifySpunGoldStore(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldStore",true);
        var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldAppearance",true);
        World(runtime,1011);Call(store,null,"Initialize");
        var random=store.GetField("Roll",Hidden);var saved=random.GetValue(null);random.SetValue(null,(Func<int,int>)(n=>0));
        Func<GameState,long> stage=p=>Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_spun_gold WHERE uid=@uid",p.Entity.UID);
        Func<GameState,int,uint,bool> act=(p,a,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Act",p,a,id,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,Entity> kill=p=>{var b=HuntMonster(p,"Bandit");b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;
            uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Func<GameState,Entity,bool> invitation=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Invitation",p,b,(Func<bool>)(()=>true));};
        Func<GameState,uint,Guid> reserve=(p,uid)=>{p.BindWorldMap(1011);p.Entity.X=100;p.Entity.Y=430;lock(p.ItemSyncRoot){var token=(Guid)Call(appearance,null,"Apply",p,(Func<bool>)(()=>true));return (Guid)Call(store,null,"Reserve",p,token,uid,(Func<bool>)(()=>true));}};
        Func<GameState,Guid,Entity,bool> win=(p,id,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Win",p,id,b,(Func<bool>)(()=>true));};
        try{for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)30:(byte)140,life,15));ulong money=p.Entity.Money;
            Check(act(p,0,0) && !act(p,0,0) && stage(p)==1,"Spun Gold acceptance is once per life: "+life);
            Check(!act(p,5,0) && act(p,1,0) && !act(p,1,0) && p.Entity.Money==money+100,"Spun Gold ordered referral pays once: "+life);
            p.BindWorldMap(1011);p.Entity.X=100;p.Entity.Y=430;var bandit=kill(p);
            Check(invitation(p,bandit) && !invitation(p,bandit) && CountId(p,729971)==1,"Spun Gold actual Bandit death issues one invitation: "+life);
            if(life==0){
                uint old=p.Inventory.Objects.Single(i=>i.ID==729971).UID;ItemHandler.DropItem(new ItemUsage(true){UID=old},p);
                Check(recover(p) && !recover(p) && !invitation(p,bandit) && invitation(p,kill(p)),"Spun Gold lost invitation requires another earned death");
            }
            Check(act(p,2,0) && !act(p,2,0) && stage(p)==3 && CountId(p,729972)==1 && p.Entity.Money==money+200,"Spun Gold invitation exchange consumes once: "+life);
            if(life==0){
                uint offering=p.Inventory.Objects.Single(i=>i.ID==729972).UID;
                Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)offering);p=ReloadBall(p);
                Check(!recover(p),"Spun Gold stored offering cannot be duplicated");
                Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)offering);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=offering},p);
                Check(recover(p) && !recover(p) && CountId(p,729972)==1,"Spun Gold lost offering recovers exactly once");
                var abandoned=reserve(p,9821723);
                lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Abandon",p,abandoned,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Abandon",p,abandoned,(Func<bool>)(()=>true)),"Spun Gold abandoned summon cannot abandon twice");
                Check(stage(p)==3 && recover(p) && CountId(p,729972)==1,"Spun Gold interrupted summon restores earned offering");
            }
            var attempt=reserve(p,(uint)(9821730+life));var cloud=kill(p);cloud.MonsterInfo.ID=9921723;
            // Store fixture binds the actor UID to the reserved durable attempt; runtime registration is a later coordinator test.
            Query("UPDATE revora_spun_gold_attempts SET body=@body WHERE id=@id","body",(long)cloud.UID,"id",attempt);
            Check(stage(p)==4 && CountId(p,729972)==0 && !win(p,Guid.NewGuid(),cloud),"Spun Gold unowned attempt cannot win: "+life);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);}
            Check(win(p,attempt,cloud) && !win(p,attempt,cloud) && stage(p)==5,"Spun Gold victory is durable and duplicate safe: "+life);
            if(life==0){
                Check(CountId(p,729973)==0 && !recover(p),"Spun Gold full bag defers the earned box");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Check(recover(p) && !recover(p) && CountId(p,729973)==1,"Spun Gold deferred box delivers once after space clears");
            }
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(appearance,null,"Tick");uint box=p.Inventory.Objects.Single(i=>i.ID==729973).UID;
            Check(!act(p,3,box+1) && act(p,3,box) && !act(p,3,box) && CountId(p,729974)==1,"Spun Gold exact earned box survives reload and opens once: "+life);
            ulong xp=p.Entity.Experience;byte level=p.Entity.Level;
            Check(act(p,4,0) && !act(p,4,0) && stage(p)==7 && CountId(p,729974)==0 && p.Entity.Money==money+1200 && (level>=Config.LevelCap || p.Entity.Level>level || p.Entity.Experience>xp),"Spun Gold armor return saves silver and experience once: "+life);
            if(life==0){
                while(p.Inventory.Count<39)Pack(p,1088000,1);Check(!act(p,5,0) && stage(p)==7,"Spun Gold final claim waits for both reward slots");
                foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT spun_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                bool failed=false;try{act(p,5,0);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT spun_final_fault");}
                Check(failed && stage(p)==7 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728237",p.Entity.UID)==0,"Spun Gold failed soul insertion rolls back pack and completion");
            }
            Parallel.For(0,4,i=>act(p,5,0));
            Check(stage(p)==8 && CountId(p,728237)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !recover(p),"Spun Gold concurrent claims grant one bound pack and P7: "+life);
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();EntityTable.SaveEntity(p);p=ReloadBall(p);
            Check(stage(p)==8 && p.Entity.Money==money+1200 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids),"Spun Gold completion and exact rewards survive reload: "+life);
        }}finally{random.SetValue(null,saved);Call(appearance,null,"Tick");}
    }

    static void VerifySpunGoldAppearance(string runtime,Map town){
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.SpunGoldAppearance",true);World(runtime,1011);
        var field=type.GetField("Clock",Hidden);var previous=field.GetValue(null);var now=new DateTime(2036,7,1,0,0,0,DateTimeKind.Utc);field.SetValue(null,(Func<DateTime>)(()=>now));
        var p=ReloadBall(WelcomePlayer(town,40,0,15));p.BindWorldMap(1011);uint original=p.Entity.Mesh;ushort body=p.Entity.Body,face=p.Entity.Face;uint hp=p.Entity.Hitpoints,max=p.Entity.MaxHitpoints;Guid token=Guid.Empty;
        Func<Guid> apply=()=>{lock(p.ItemSyncRoot)return (Guid)Call(type,null,"Apply",p,(Func<bool>)(()=>true));};
        try{
            token=apply();Check((bool)Call(type,null,"Owns",p,token) && p.Entity.Mesh==2562005 && p.Entity.CharBuffer._Mesh==2562005,"Spun Gold disguise projects shared spawn/update mesh with owned token");
            Check(p.Entity.Body==body && p.Entity.Face==face && p.Entity.Hitpoints==hp && p.Entity.MaxHitpoints==max && p.Entity.TransformationID==0,"Spun Gold cosmetic disguise preserves identity, health and combat transformation state");
            now=now.AddMinutes(4);Check(apply()==token,"Spun Gold repeated application does not renew or duplicate a live lease");
            lock(p.ItemSyncRoot)Call(type,null,"Clear",p,Guid.NewGuid());Check(p.Entity.Mesh==2562005,"Spun Gold stale cleanup token cannot clear current disguise");
            p.Entity.Face=(ushort)(face+1);Check(p.Entity.Mesh==2562005,"Spun Gold ordinary face updates preserve active cosmetic mesh");
            now=now.AddMinutes(1);Call(type,null,"Tick");Check(!(bool)Call(type,null,"Owns",p,token) && p.Entity.Mesh==(uint)((face+1)*10000+body) && p.Entity.Hitpoints==hp,"Spun Gold expiry restores latest face without altering health");
            token=apply();p.Entity.TransformationID=111;Check(!(bool)Call(type,null,"Owns",p,token) && p.Entity.Mesh==(uint)(1110000000+p.Entity.Face*10000+body),"Spun Gold real transformation cancels disguise and preserves transformation mesh");p.Entity.TransformationID=0;
            token=apply();p.BindWorldMap(1002);Call(type,null,"Tick");Check(!(bool)Call(type,null,"Owns",p,token) && p.Entity.Mesh==(uint)(p.Entity.Face*10000+body),"Spun Gold map departure clears the disguise");
            p.BindWorldMap(1011);token=apply();p.Entity.Hitpoints=0;Call(type,null,"Tick");Check(!(bool)Call(type,null,"Owns",p,token) && p.Entity.Mesh!=(uint)2562005 && p.Entity.Hitpoints==0,"Spun Gold death cleanup cannot revive or heal the player");p.Entity.Hitpoints=hp;
            token=apply();EntityTable.SaveEntity(p);var old=p;p=ReloadBall(p);Call(type,null,"Tick");Check(p.Entity.Body==body && p.Entity.Face==face+1 && p.Entity.Mesh!=(uint)2562005 && !(bool)Call(type,null,"Owns",p,token) && old.Entity.Mesh!=(uint)2562005,"Spun Gold reconnect keeps real identity and retires previous-session appearance");
            p.BindWorldMap(1011);p.Entity.TransformationID=111;bool refused=false;try{apply();}catch(InvalidOperationException){refused=true;}Check(refused,"Spun Gold disguise refuses to replace an existing combat transformation");p.Entity.TransformationID=0;
            bool context=false;try{lock(p.ItemSyncRoot)Call(type,null,"Apply",p,(Func<bool>)(()=>false));}catch(InvalidOperationException){context=true;}Check(context && p.Entity.Mesh!=(uint)2562005,"Spun Gold invalid quest context cannot grant cosmetic lease");
        }finally{lock(p.ItemSyncRoot)Call(type,null,"Clear",p,token);field.SetValue(null,previous);}
    }

    static void VerifyMysticPack(Map town){
        VerifySingleEquipmentPack(town,728240,118057,"Mystic");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] headgear={118057,111057,113027,112057,143057,144057,148057,114057,114057,170057};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728240,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728240)==0 && CountId(p,headgear[i])==1 && p.Inventory.Objects.Single(x=>x.ID==headgear[i]).Bound,"Mystic earned-pack dispatcher chooses class headgear: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,headgear[i])==1 && FixedUses(p)==1,"Mystic class headgear and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728240,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728240)==1 && FixedUses(unsupported)==0,"Mystic unknown class retains the unopened pack");
    }
    static void VerifyFranticAppearance(Map town){
        var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestAppearance",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object previous=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,12,0,0,DateTimeKind.Utc);
        var p=ReloadBall(WelcomePlayer(town,50,0,15));p.Entity.X=440;p.Entity.Y=343;object scene=null;Guid token=Guid.Empty;
        Action tick=()=>Call(appearance,null,"Tick");Func<DateTime,Guid> apply=until=>{lock(p.ItemSyncRoot)return (Guid)Call(appearance,null,"ApplyMonkey",p,scene,until,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p);var map=(Map)Field(scene,"Map");
            ushort body=p.Entity.Body,face=p.Entity.Face;uint hp=p.Entity.Hitpoints,max=p.Entity.MaxHitpoints;byte job=p.Entity.Class;uint ordinary=p.Entity.Mesh;
            bool rejected=false;try{lock(p.ItemSyncRoot)Call(appearance,null,"Apply",p,156u,(Func<bool>)(()=>true));}catch(InvalidOperationException){rejected=true;}Check(rejected && p.Entity.Mesh==ordinary,"Frantic monkey disguise requires explicit scene reservation");
            rejected=false;try{apply(now.AddSeconds(61));}catch(InvalidOperationException){rejected=true;}Check(rejected && p.Entity.Mesh==ordinary,"Frantic disguise rejects duration above sixty seconds");
            rejected=false;try{lock(p.ItemSyncRoot)Call(appearance,null,"ApplyMonkey",p,scene,now.AddSeconds(60),(Func<bool>)(()=>false));}catch(InvalidOperationException){rejected=true;}Check(rejected && p.Entity.Mesh==ordinary,"Frantic failed disguise context changes no mesh");
            token=apply(now.AddSeconds(60));Check(token!=Guid.Empty && p.Entity.Mesh==156 && p.Entity.Body==body && p.Entity.Face==face && p.Entity.Class==job && p.Entity.Hitpoints==hp && p.Entity.MaxHitpoints==max,"Frantic monkey projection preserves identity and health");
            lock(p.ItemSyncRoot){Call(appearance,null,"Clear",p,token,30040u);Call(appearance,null,"Clear",p,Guid.NewGuid(),156u);}Check(p.Entity.Mesh==156,"Frantic wrong-kind or stale-token cleanup cannot remove disguise");
            p.Entity.Body=body;p.Entity.Face=face;Check(p.Entity.Mesh==156,"Frantic ordinary appearance recomputation preserves active lease");
            p.BindWorldMap(map.ID);p.Entity.X=795;p.Entity.Y=471;tick();Check(p.Entity.Mesh==156,"Frantic disguise survives transition into exact reserved trial map");
            now=now.AddSeconds(59);Check(apply(now.AddSeconds(60))==token,"Frantic repeated lease returns original token without extending expiry");tick();Check(p.Entity.Mesh==156,"Frantic monkey remains visible through second fifty-nine");
            now=now.AddSeconds(1);tick();Check(p.Entity.Mesh==ordinary && p.Entity.Hitpoints==hp && p.Entity.MaxHitpoints==max,"Frantic exact sixty-second expiry restores ordinary projection without HP change");
            token=apply(now.AddSeconds(60));p.BindWorldMap(1002);p.Entity.X=327;p.Entity.Y=303;tick();Check(p.Entity.Mesh==ordinary,"Frantic departure from reserved map and Doctor Li area clears lease");
            p.Entity.X=440;p.Entity.Y=343;token=apply(now.AddSeconds(60));lock(p.ItemSyncRoot)Call(appearance,null,"Clear",p,token,156u);Check(p.Entity.Mesh==ordinary,"Frantic explicit failed-admission cleanup restores appearance");
        }finally{lock(p.ItemSyncRoot)Call(appearance,null,"Clear",p,token,156u);p.BindWorldMap(1002);if(scene!=null)Call(scenes,null,"Release",scene);clock.SetValue(null,previous);}
    }

    static void VerifyFranticScenes(Map town){
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);
        var p=ReloadBall(WelcomePlayer(town,140,2,15));var other=ReloadBall(WelcomePlayer(town,140,2,15));
        int maps=Kernel.Maps.Count,metadata=MapsTable.MapInformations.Count;
        var original=MonsterInformation.MonsterInformations[10];string name=original.Name;uint health=original.Hitpoints;byte level=original.Level;
        for(int kind=0;kind<3;kind++){
            var profile=(MonsterInformation)Call(type,null,"Profile",kind);
            Check(profile.ID==9917450u+kind && !profile.Boss && !profile.Guard && !profile.Reviver && !(bool)Field(profile,"LabirinthDrop")
                && profile.OwnItemID==0 && profile.MinMoneyDropAmount==0 && profile.MaxMoneyDropAmount==0 && profile.HPPotionID==0 && profile.MPPotionID==0,
                "Frantic private monster has no inherited boss or catalog loot: "+kind);
            if(kind==2)Check(profile.Name=="ApeLeader" && profile.Level==50 && profile.Hitpoints==8800 && profile.Mesh==original.Mesh,
                "Frantic Ape Leader uses explicit trial stats, preserving only catalog appearance");
        }
        Check(original.Name==name && original.Level==level && original.Hitpoints==health,"Frantic profiles leave shared Macaque family unchanged");
        for(int floor=0;floor<3;floor++){
            object scene;lock(p.ItemSyncRoot)scene=Call(type,null,"Create",p);var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1020 && map.ID!=map.BaseID && map.Entities.Count==0 && map.Npcs.Count==0 && map.Portals.Count==0
                && MapsTable.MapInformations[map.ID].Owner==p.Entity.UID,"Frantic floor is an empty owner-specific dynamic map: "+floor);
            Check((bool)Call(type,null,"Registered",scene,p) && !(bool)Call(type,null,"Registered",scene,other),"Frantic scene rejects unrelated owner: "+floor);
            bool rejected=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Frantic duplicate owner reservation refused: "+floor);
            ushort prior=p.Entity.MapID;p.Entity.MapID=map.ID;
            rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}finally{p.Entity.MapID=prior;}
            Check(rejected && Kernel.Maps.ContainsKey(map.ID),"Frantic cleanup cannot strand an occupant: "+floor);
            var ownedMetadata=MapsTable.MapInformations[map.ID];var replacement=new MapsTable.MapInformation{ID=map.ID,Owner=other.Entity.UID};MapsTable.MapInformations[map.ID]=replacement;
            rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && MapsTable.MapInformations[map.ID].Equals(replacement),"Frantic release preserves replaced metadata: "+floor);
            MapsTable.MapInformations[map.ID]=ownedMetadata;
            Call(type,null,"Release",scene);Call(type,null,"Release",scene);
            Check(!Kernel.Maps.ContainsKey(map.ID) && !MapsTable.MapInformations.ContainsKey(map.ID),"Frantic release removes only owned map metadata: "+floor);
        }
        bool invalid=false;try{Call(type,null,"Profile",3);}catch(ArgumentOutOfRangeException){invalid=true;}
        Check(invalid && Kernel.Maps.Count==maps && MapsTable.MapInformations.Count==metadata,"Frantic invalid profile and repeated allocations leave no registry leaks");
        p.Fake=true;invalid=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p);}catch(InvalidOperationException){invalid=true;}finally{p.Fake=false;}
        Check(invalid,"Frantic scene rejects fake sessions");
        var clock=type.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object previous=clock.GetValue(null);
        try{clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2026,9,26)));invalid=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p);}catch(InvalidOperationException){invalid=true;}
            Check(invalid && Kernel.Maps.Count==maps,"Frantic non-UTC scene clock cannot allocate terrain");
        }finally{clock.SetValue(null,previous);}
    }

    static void VerifyFranticQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);
        World(runtime,1020);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object oldRoll=roll.GetValue(null);var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,29,12,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==4485?(ushort)1020:(ushort)1002);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,Entity> attack=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        Action<GameState> journal=p=>{int st=stage(p),current=st<=3?st:st<=6?4:st<=8?5:st==9?6:7;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1745 && (uint)x.UID<=1750).ToArray();Check(own.Length==6 && own.All(x=>x.Status==(current>(int)x.UID-1744?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1744?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Frantic saved journal stage "+st);};
        try{roll.SetValue(null,(Func<int,int>)(n=>0));clock.SetValue(null,(Func<DateTime>)(()=>now));
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)50:(byte)140,life,15));p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
            p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=91},p);Check(stage(p)==0,"Frantic remote acceptance cannot start the quest: "+life);
            for(uint id=1745;id<=1750;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Frantic forged Begin rejected: "+id);}
            talk(p,900071727,91);talk(p,4486,1);Check(stage(p)==1,"Frantic ordinary acceptance cannot skip Bao: "+life);journal(p);
            talk(p,4485,1);talk(p,4485,1);Check(stage(p)==2 && p.Entity.Money==money+100,"Frantic Bao waits for the earned heart: "+life);p.Entity.X=668;p.Entity.Y=673;
            var macaque=HuntMonster(p,life==1?"MacaqueL48":"Macaque");macaque.MapObjType=MapObjectType.Monster;macaque.Hitpoints=macaque.MaxHitpoints=macaque.MonsterInfo.Hitpoints;macaque.IsDropped=true;attack(p,macaque);Call(quest,null,"OnDeath",p,macaque);
            Check(CountId(p,721699)==1 && p.Inventory.Objects.Single(i=>i.ID==721699).Bound,"Frantic ordinary death dispatch grants one heart despite replay: "+life);
            talk(p,4485,1);talk(p,4486,1);talk(p,4486,1);Check(stage(p)==4 && CountId(p,721699)==0 && CountId(p,721700)==1 && p.Entity.Money==money+300,"Frantic normal referrals exchange heart for poison once: "+life);journal(p);
            var poison=p.Inventory.Objects.Single(i=>i.ID==721700);p.Entity.X=327;p.Entity.Y=303;ItemHandler.UseItem(poison,p);Check(stage(p)==4 && CountId(p,721700)==1,"Frantic ordinary poison use rejects remote position: "+life);talk(p,4486,0);ItemHandler.UseItem(poison,p);ItemHandler.UseItem(poison,p);
            Check(stage(p)==5 && p.Entity.Mesh==156 && p.Entity.MapID!=1002 && !Fenced(p),"Frantic ordinary item dispatch disguises and admits once: "+life);journal(p);
            now=now.AddSeconds(59);Call(quest,null,"Tick");Check(p.Entity.Mesh==156 && p.Map.Entities.Count==0,"Frantic scheduled coordinator preserves the poison wait: "+life);now=now.AddSeconds(4);Call(quest,null,"Tick");Check(p.Entity.Mesh!=156,"Frantic scheduled coordinator restores appearance after unlock: "+life);
            for(int i=0;i<5;i++){attack(p,p.Map.Entities.Values.First(b=>!b.Dead));now=now.AddSeconds(3);Call(quest,null,"Tick");}
            Check(stage(p)==6 && p.Entity.MapID==1002 && p.Entity.X==440 && p.Entity.Y==345,"Frantic normal trial completion returns beside Doctor Li: "+life);EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,4486,1);talk(p,4486,1);
            Check(stage(p)==7 && CountId(p,721701)==1 && p.Entity.Money==money+400,"Frantic saved trial earns one vial and payment after login: "+life);journal(p);
            var vial=p.Inventory.Objects.Single(i=>i.ID==721701);ItemHandler.UseItem(vial,p);Check(stage(p)==7 && CountId(p,721701)==1,"Frantic ordinary vial use rejects wrong map: "+life);p.BindWorldMap(1020);p.Entity.X=795;p.Entity.Y=471;ItemHandler.UseItem(vial,p);ItemHandler.UseItem(vial,p);
            for(int i=0;i<3;i++){var ape=p.Map.Entities.Values.Single(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID>=9917451 && b.MonsterInfo.ID<=9917452);attack(p,ape);now=now.AddSeconds(3);Call(quest,null,"Tick");}
            Check(stage(p)==8 && CountId(p,721701)==0 && CountId(p,721702)==1,"Frantic ordinary vial/combat route delivers sword: "+life);journal(p);
            if(life==0){uint sword=p.Inventory.Objects.Single(i=>i.ID==721702).UID;ItemHandler.DropItem(new ItemUsage(true){UID=sword},p);talk(p,4485,2);talk(p,4485,2);Check(stage(p)==8 && CountId(p,721702)==1,"Frantic NPC recovery replaces lost sword once");}
            byte beforeLevel=p.Entity.Level;ulong beforeExp=p.Entity.Experience;talk(p,4485,1);talk(p,4485,1);Check(stage(p)==9 && CountId(p,721702)==0 && p.Entity.Money==money+3400 && !Fenced(p),"Frantic Bao hand-in pays total 3400 silver once: "+life);Check(beforeLevel>=Config.LevelCap?p.Entity.Level==beforeLevel:p.Entity.Level>beforeLevel || p.Entity.Experience>beforeExp,"Frantic hand-in grants progression below cap and preserves capped level: "+life);journal(p);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT frantic_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,91);}finally{Query("ALTER TABLE items DROP CONSTRAINT frantic_final_fault");}Check(Fenced(p) && stage(p)==9 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728243",p.Entity.UID)==0,"Frantic final P7 failure rolls back weapon pack and completion");p=ReloadBall(p);Call(quest,null,"OnLogin",p);while(p.Inventory.Count<40)Pack(p,1088000,1);talk(p,900071727,91);Check(stage(p)==9,"Frantic full bag preserves final claim");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);}
            talk(p,900071727,91);talk(p,900071727,91);Check(stage(p)==10 && CountId(p,728243)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Frantic Chang grants one bound pack and bound P7: "+life);journal(p);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,900071727,92);Check(stage(p)==10 && CountId(p,721699)==0 && CountId(p,721700)==0 && CountId(p,721701)==0 && CountId(p,721702)==0 && CountId(p,728243)==1,"Frantic completed recovery creates no quest materials after reload: "+life);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728243),p);Check(CountId(p,410108)==1 && p.Inventory.Objects.Single(i=>i.ID==410108).Bound && !Fenced(p),"Frantic earned class weapon pack opens normally: "+life);
        }}finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
    }

    static void VerifyFranticApeRuntime(GameState p){
        var runtime=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysApes",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object previous=clock.GetValue(null);DateTime now=new DateTime(2026,9,28,12,0,0,DateTimeKind.Utc);
        Func<bool> start=()=> (bool)Call(runtime,null,"Start",p,p.Inventory.Objects.Single(i=>i.ID==721701));Action tick=()=>Call(runtime,null,"Tick");
        Func<Entity[]> actors=()=>Kernel.Maps[1020].Entities.Values.Where(b=>b.MonsterInfo!=null && b.MonsterInfo.ID>=9917451 && b.MonsterInfo.ID<=9917452).ToArray();
        Action<Entity,GameState> attack=(body,who)=>{uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(who.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=who.Entity.UID,Attacked=body.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));Check(!start(),"Frantic normal vial coordinator rejects wrong map: "+p.Entity.Reborn);p.BindWorldMap(1020);p.Entity.X=795;p.Entity.Y=471;
            Check(start() && !start() && actors().Length==1 && actors()[0].MonsterInfo.ID==9917451,"Frantic vial coordinator spawns one owned Venomous Ape without duplicate reservation: "+p.Entity.Reborn);
            var stale=actors()[0];
            if(p.Entity.Reborn==0){p.BindWorldMap(1002);tick();Check(actors().Length==0 && Call(store,null,"OpenApes",p)==null,"Frantic departure removes actor and abandons attempt");p.BindWorldMap(1020);p.Entity.X=795;p.Entity.Y=471;now=now.AddSeconds(59);Check(!start() && actors().Length==0,"Frantic runtime retry preserves one-minute cooldown");now=now.AddSeconds(1);}
            else if(p.Entity.Reborn==1){EntityTable.SaveEntity(p);p=ReloadBall(p);tick();bool recovered;lock(p.ItemSyncRoot)recovered=(bool)Call(runtime,null,"Recover",p);Check(recovered && actors().Length==0,"Frantic replacement session cleans old actors before abandoning saved attempt");now=now.AddSeconds(60);}
            else{now=now.AddMinutes(5);tick();Check(actors().Length==0 && Call(store,null,"OpenApes",p)==null,"Frantic exact five-minute expiry removes actor and releases saved attempt");}
            Check(start() && !Fenced(p),"Frantic cleaned encounter retries through normal coordinator: "+p.Entity.Reborn);
            Check((bool)Call(runtime,null,"BlockAttack",p.Entity,stale),"Frantic stale removed actor cannot re-enter combat: "+p.Entity.Reborn);
            var other=ReloadBall(WelcomePlayer(Kernel.Maps[1002],140,0,15));other.BindWorldMap(1020);other.Entity.X=795;other.Entity.Y=471;
            var target=actors().Single();uint hp=target.Hitpoints;attack(target,other);target.Die(other.Entity);Check(target.Hitpoints==hp && !target.Dead && (int)Field(Call(store,null,"OpenApes",p),"Kills")==0,"Frantic foreign normal damage and direct death cannot steal ape credit: "+p.Entity.Reborn);other.BindWorldMap(1002);
            while(p.Inventory.Count<40)Pack(p,1088000,1);
            for(int i=0;i<3;i++){
                target=actors().Single(b=>!b.Dead);Check(target.MonsterInfo.ID==(i<2?9917451u:9917452u),"Frantic runtime advances exact ape/ape/leader sequence: "+p.Entity.Reborn+"/"+i);
                attack(target,p);target.Die(p.Entity);Check(target.Dead && !Fenced(p),"Frantic normal owned attack records one death: "+p.Entity.Reborn+"/"+i);
                now=now.AddSeconds(3);tick();
            }
            Check(actors().Length==0 && Call(store,null,"OpenApes",p)==null && CountId(p,721701)==0 && CountId(p,721702)==1 && p.Inventory.Objects.Single(i=>i.ID==721702).Bound,"Frantic leader automatically exchanges vial for bound sword and cleans encounter despite full bag: "+p.Entity.Reborn);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(Scalar("SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID)==8 && CountId(p,721702)==1,"Frantic normal encounter sword and progress survive reload: "+p.Entity.Reborn);
        }finally{p.BindWorldMap(1002);tick();clock.SetValue(null,previous);}
    }

    static void VerifyFranticApes(GameState p){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object previous=clock.GetValue(null);DateTime now=new DateTime(2026,9,27,12,0,0,DateTimeKind.Utc);var bodies=new System.Collections.Generic.List<Entity>();
        Func<int> stage=()=>Convert.ToInt32(Scalar("SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID));
        Func<Guid,bool> reserve=id=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"ReserveApes",p,p.Inventory.Objects.Single(i=>i.ID==721701).UID,id,(Func<bool>)(()=>true));};
        Func<Guid,bool> abandon=id=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"AbandonApes",p,id,(Func<bool>)(()=>true));};
        Func<Guid,bool> deliver=id=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"DeliverSword",p,id,(Func<bool>)(()=>true));};
        Func<bool> recover=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<Guid,Entity,bool> credit=(id,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"ApeDeath",p,id,b,(Func<bool>)(()=>true));};
        Func<int,Entity> kill=kind=>{var map=p.Map;var info=(MonsterInformation)Call(scenes,null,"Profile",kind);var b=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1020,X=794,Y=470,MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,IsDropped=true};info.Owner=b;b.Hitpoints=b.MaxHitpoints=info.Hitpoints;map.AddEntity2(b);Call(typeof(Entity),b,"DieCore",p.Entity);bodies.Add(b);return b;};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));Check(!reserve(Guid.NewGuid()) && stage()==7,"Frantic vial cannot reserve an encounter outside the Ape City altar: "+p.Entity.Reborn);
            p.BindWorldMap(1020);p.Entity.X=795;p.Entity.Y=471;Guid first=Guid.NewGuid();Check(reserve(first) && !reserve(Guid.NewGuid()) && CountId(p,721701)==1,"Frantic reserves one encounter while retaining tracked vial: "+p.Entity.Reborn);
            var earlyLeader=kill(2);Check(!credit(first,earlyLeader) && !deliver(first),"Frantic leader before its two apes cannot win or issue sword: "+p.Entity.Reborn);
            uint oldVial=p.Inventory.Objects.Single(i=>i.ID==721701).UID;ItemHandler.DropItem(new ItemUsage(true){UID=oldVial},p);Check(!recover(),"Frantic live encounter prevents lost-vial replacement: "+p.Entity.Reborn);
            Check(abandon(first) && !abandon(first) && recover() && !recover(),"Frantic abandoned encounter permits one lost-vial replacement: "+p.Entity.Reborn);
            now=now.AddSeconds(59);EntityTable.SaveEntity(p);p=ReloadBall(p);Check(!reserve(Guid.NewGuid()),"Frantic vial cooldown survives abandonment and reload at second fifty-nine: "+p.Entity.Reborn);
            now=now.AddSeconds(1);Guid fight=Guid.NewGuid();Check(reserve(fight),"Frantic vial permits a fresh attempt at second sixty: "+p.Entity.Reborn);
            Check(!credit(first,kill(1)),"Frantic abandoned attempt rejects later corpse credit: "+p.Entity.Reborn);
            var one=kill(1);Check(credit(fight,one) && !credit(fight,one),"Frantic first owned ape death has one receipt: "+p.Entity.Reborn);
            var two=kill(1);Check(credit(fight,two) && !credit(fight,kill(1)) && !credit(fight,earlyLeader),"Frantic two apes require a fresh leader and reject extra apes: "+p.Entity.Reborn);
            Check(credit(fight,kill(2)) && !abandon(fight) && !reserve(Guid.NewGuid()),"Frantic leader saves a non-repeatable victory: "+p.Entity.Reborn);
            EntityTable.SaveEntity(p);p=ReloadBall(p);var open=Call(store,null,"OpenApes",p);Check((Guid)Field(open,"ID")==fight && (int)Field(open,"Kills")==3 && (int)Field(open,"State")==1,"Frantic victory and three death receipts survive reload: "+p.Entity.Reborn);
            uint vial=p.Inventory.Objects.Single(i=>i.ID==721701).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)vial);p=ReloadBall(p);Check(!deliver(fight) && !recover() && stage()==7,"Frantic stored vial cannot be consumed or copied and victory remains saved: "+p.Entity.Reborn);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)vial);p=ReloadBall(p);
            if(p.Entity.Reborn==0){Query("ALTER TABLE items ADD CONSTRAINT frantic_sword_fault CHECK(id<>721702) NOT VALID");bool failed=false;try{deliver(fight);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT frantic_sword_fault");}
                Check(failed && stage()==7 && CountId(p,721701)==1 && CountId(p,721702)==0,"Frantic failed sword insert preserves vial and won attempt");p=ReloadBall(p);while(p.Inventory.Count<40)Pack(p,1088000,1);}
            Check(deliver(fight) && !deliver(fight) && stage()==8 && CountId(p,721701)==0 && CountId(p,721702)==1 && p.Inventory.Objects.Single(i=>i.ID==721702).Bound,"Frantic full-bag-safe delivery exchanges exact vial for one bound sword: "+p.Entity.Reborn);
            p=ReloadBall(p);Check(Call(store,null,"OpenApes",p)==null && stage()==8 && CountId(p,721702)==1,"Frantic delivered encounter stays closed after reload: "+p.Entity.Reborn);
            uint sword=p.Inventory.Objects.Single(i=>i.ID==721702).UID;ItemHandler.DropItem(new ItemUsage(true){UID=sword},p);Check(recover() && !recover() && CountId(p,721702)==1 && stage()==8,"Frantic lost earned sword replaces once without replaying encounter: "+p.Entity.Reborn);
        }finally{foreach(var b in bodies)Kernel.Maps[1020].RemoveEntity(b);clock.SetValue(null,previous);}
    }

    static void VerifyFranticAdmission(GameState p,bool apes=false,bool runtimeApes=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);var admission=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysAdmission",true);var appearance=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestAppearance",true);
        Call(scenes,null,"InitializeTravel");var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object previous=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,12,0,0,DateTimeKind.Utc);object scene=null;
        Func<int> stage=()=>Convert.ToInt32(Scalar("SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID));Func<bool> start=()=> (bool)Call(admission,null,"Start",p,p.Inventory.Objects.Single(i=>i.ID==721700));
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));p.BindWorldMap(1002);p.Entity.X=440;p.Entity.Y=343;CityGuardSystem.SpawnTalker(4486,1002,440,343,6880,"RoyalDoctorLi",0);
            uint ordinary=p.Entity.Mesh,hp=p.Entity.Hitpoints,max=p.Entity.MaxHitpoints;ushort body=p.Entity.Body,face=p.Entity.Face;int maps=Kernel.Maps.Count;
            p.Entity.X=327;p.Entity.Y=303;Check(!start() && stage()==4 && CountId(p,721700)==1 && Kernel.Maps.Count==maps,"Frantic remote poison use reserves nothing: "+p.Entity.Reborn);p.Entity.X=440;p.Entity.Y=343;
            if(p.Entity.Reborn==0){Query("ALTER TABLE revora_frantic_trials ADD CONSTRAINT frantic_admission_fault CHECK(state=2) NOT VALID");bool result;try{result=start();}finally{Query("ALTER TABLE revora_frantic_trials DROP CONSTRAINT frantic_admission_fault");}
                Check(!result && Fenced(p) && stage()==4 && CountId(p,721700)==1 && p.Entity.Mesh==ordinary && Kernel.Maps.Count==maps,"Frantic failed attempt insert rolls back poison, appearance and scene");p=ReloadBall(p);
                Query("ALTER TABLE revora_frantic_visits ADD CONSTRAINT frantic_visit_fault CHECK(NOT active) NOT VALID");try{result=start();}finally{Query("ALTER TABLE revora_frantic_visits DROP CONSTRAINT frantic_visit_fault");}
                Check(!result && Fenced(p) && stage()==5 && CountId(p,721700)==0 && p.Entity.Mesh==ordinary && Kernel.Maps.Count==maps,"Frantic failed travel after consumption clears appearance and terrain but keeps recoverable attempt");p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"RecoverLogin",p);Check(stage()==4 && CountId(p,721700)==1,"Frantic post-commit admission failure recovers exactly one poison on reload");}
            var used=p.Inventory.Objects.Single(i=>i.ID==721700);Check(start() && stage()==5 && p.Entity.Mesh==156 && CountId(p,721700)==0,"Frantic coordinator disguises, consumes and enters owned trial: "+p.Entity.Reborn);scene=Call(scenes,null,"Find",p);var map=(Map)Field(scene,"Map");
            Check(p.Entity.MapID==map.ID && p.Entity.Body==body && p.Entity.Face==face && p.Entity.Hitpoints==hp && p.Entity.MaxHitpoints==max,"Frantic actual admission preserves character identity and health: "+p.Entity.Reborn);
            Check(!(bool)Call(admission,null,"Start",p,used) && !Fenced(p),"Frantic consumed poison replay cannot create another admission: "+p.Entity.Reborn);
            if(p.Entity.Reborn==1){now=now.AddMinutes(10);Call(scenes,null,"Tick");Check(p.Entity.MapID==1002 && p.Entity.Mesh==ordinary && CountId(p,721700)==1 && stage()==4 && Kernel.Maps.Count==maps,"Frantic expired disguised admission restores appearance and poison after release");scene=null;Check(start(),"Frantic expired admission can retry normally");scene=Call(scenes,null,"Find",p);map=(Map)Field(scene,"Map");}
            var trial=Call(store,null,"ActiveTrial",p);DateTime ready=(DateTime)Field(trial,"ReadyAt");Check(ready==now.AddSeconds(60),"Frantic appearance and saved trial share a sixty-second deadline: "+p.Entity.Reborn);
            now=ready.AddSeconds(-1);Call(scenes,null,"Tick");Check(p.Entity.Mesh==156 && map.Entities.Count==0,"Frantic scene tick keeps monkey disguise and blocks population before unlock: "+p.Entity.Reborn);
            now=ready;Call(scenes,null,"Tick");Check(p.Entity.Mesh==ordinary,"Frantic scene tick clears disguise at the saved unlock boundary: "+p.Entity.Reborn);
            now=now.AddSeconds(3);Call(scenes,null,"Tick");
            for(int i=0;i<5;i++){var mob=map.Entities.Values.First(b=>!b.Dead);uint damage=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);now=now.AddSeconds(3);Call(scenes,null,"Tick");}
            Check(stage()==6 && p.Entity.MapID==1002 && p.Entity.Mesh==ordinary && Kernel.Maps.Count==maps && !Fenced(p),"Frantic coordinated disguise/travel/combat returns cleanly after five real kills: "+p.Entity.Reborn);scene=null;
            lock(p.ItemSyncRoot)Call(store,null,"TrialReward",p,(Func<bool>)(()=>true));Check(stage()==7 && CountId(p,721701)==1 && p.Inventory.Objects.Single(i=>i.ID==721701).Bound,"Frantic coordinated trial can claim its saved bound vial: "+p.Entity.Reborn);
            if(runtimeApes)VerifyFranticApeRuntime(p);else if(apes)VerifyFranticApes(p);
        }finally{if(scene!=null && !(bool)Field(scene,"Released")){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}Call(appearance,null,"Tick");clock.SetValue(null,previous);}
    }

    static void VerifyFranticTrial(GameState p,bool combat=false,bool travel=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysScenes",true);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,12,0,0,DateTimeKind.Utc);object scene=null;Map map=null;
        Func<int> stage=()=>Convert.ToInt32(Scalar("SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID));
        Func<int> count=()=>Convert.ToInt32(Scalar("SELECT kills FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID));
        Func<bool> reward=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"TrialReward",p,(Func<bool>)(()=>true));};
        Action clear=()=>{if(scene==null)return;if(travel){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);scene=null;return;}p.BindWorldMap(1002);foreach(var b in map.Entities.Values.ToArray())map.RemoveEntity(b);Call(scenes,null,"Release",scene);scene=null;};
        Func<bool> begin=()=>{uint poison=p.Inventory.Objects.Single(i=>i.ID==721700).UID;lock(p.ItemSyncRoot)return (bool)Call(store,null,"BeginTrial",p,poison,scene,(Func<bool>)(()=>true));};
        Action create=()=>{lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p);map=(Map)Field(scene,"Map");};
        Func<Entity> kill=()=>{var info=(MonsterInformation)Call(scenes,null,"Profile",0);var b=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=794,Y=470,MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,IsDropped=true};info.Owner=b;b.Hitpoints=b.MaxHitpoints=info.Hitpoints;map.AddEntity2(b);uint damage=b.Hitpoints;Call(typeof(Entity),b,"DieCore",p.Entity);return b;};
        Func<Entity,bool> credit=b=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"TrialKill",p,scene,b,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));if(travel)Call(scenes,null,"InitializeTravel");create();uint original=p.Inventory.Objects.Single(i=>i.ID==721700).UID;bool failed=false;
            if(p.Entity.Reborn==0){int calls=0;try{lock(p.ItemSyncRoot)Call(store,null,"BeginTrial",p,original,scene,(Func<bool>)(()=>++calls==1));}catch(InvalidOperationException){failed=true;}
                Check(failed && stage()==4 && CountId(p,721700)==1 && Scalar("SELECT COUNT(*) FROM revora_frantic_trials WHERE uid=@uid",p.Entity.UID)==0,"Frantic final context failure rolls back poison consumption and trial receipt");}
            Check(begin() && stage()==5 && CountId(p,721700)==0,"Frantic trial consumes exact poison and saves active attempt: "+p.Entity.Reborn);
            Guid attempt=(Guid)Field(scene,"Visit");object active=Call(store,null,"ActiveTrial",p);Check((Guid)Field(active,"ID")==attempt && (DateTime)Field(active,"ReadyAt")==now.AddSeconds(60),"Frantic trial reload exposes original durable unlock time: "+p.Entity.Reborn);
            bool recovered;lock(p.ItemSyncRoot)recovered=(bool)Call(store,null,"RecoverTrial",p,attempt,(Func<bool>)(()=>true));Check(!recovered,"Frantic live scene prevents poison recovery: "+p.Entity.Reborn);
            if(combat){
                var guard=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysCombat",true);
                if(travel){
                    Action enter=()=>{lock(p.ItemSyncRoot)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));};
                    ushort previousMap=p.Entity.MapID;p.Entity.Teleport(map.ID,795,471);Check(p.Entity.MapID==previousMap,"Frantic owner cannot teleport into an unadmitted scene: "+p.Entity.Reborn);
                    enter();enter();Check(p.Entity.MapID==map.ID && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID && Scalar("SELECT COUNT(*) FROM revora_frantic_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Frantic actual entry durably saves one visit and arrival: "+p.Entity.Reborn);
                    if(p.Entity.Reborn==0){now=now.AddMinutes(10);Call(scenes,null,"Tick");Check(p.Entity.MapID==1002 && stage()==4 && CountId(p,721700)==1 && !Kernel.Maps.ContainsKey(map.ID),"Frantic exact expiry returns the owner, frees terrain and restores poison");scene=null;}
                    else if(p.Entity.Reborn==1){Kernel.GamePool.Remove(p.Entity.UID);Call(scenes,null,"Tick");Check(!Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,"Frantic disconnect saves home before releasing private terrain");scene=null;p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Check(stage()==4 && CountId(p,721700)==1,"Frantic reconnect restores one poison after offline trial cleanup");}
                    else{p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Call(scenes,null,"RecoverLogin",p);Check(p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(map.ID) && stage()==4 && CountId(p,721700)==1,"Frantic replacement session returns home before old scene cleanup and poison recovery");scene=null;}
                    create();Check(begin(),"Frantic recovered visit can reserve another timed trial: "+p.Entity.Reborn);enter();
                }else{p.BindWorldMap(map.ID);p.Entity.X=795;p.Entity.Y=471;scene.GetType().GetField("Entered",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(scene,true);}
                Action pulse=()=>{lock(p.ItemSyncRoot)Call(guard,null,"Populate",scene);};
                Action<Entity,GameState> attack=(body,who)=>{uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(who.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=who.Entity.UID,Attacked=body.UID},ref damage,null);};
                pulse();Check(map.Entities.Count==0,"Frantic runtime population stays empty during poison wait: "+p.Entity.Reborn);
                bool denied=false;try{lock(p.ItemSyncRoot)Call(guard,null,"Spawn",scene,p,0,(ushort)794,(ushort)470);}catch(InvalidOperationException){denied=true;}Check(denied,"Frantic direct spawn cannot bypass trial timer: "+p.Entity.Reborn);
                now=now.AddSeconds(59);pulse();Check(map.Entities.Count==0,"Frantic runtime still waits at fifty-nine seconds: "+p.Entity.Reborn);
                now=now.AddSeconds(3);pulse();Check(map.Entities.Count==4 && map.Entities.Values.All(v=>v.MonsterInfo.ID==9917450 && !v.Dead),"Frantic runtime populates only free audited cells after unlock: "+p.Entity.Reborn);
                var targets=map.Entities.Values.ToArray();pulse();Check(map.Entities.Count==4 && targets.All(v=>map.Entities.ContainsKey(v.UID)),"Frantic repeated pulse does not duplicate living actors: "+p.Entity.Reborn);
                var outsider=ReloadBall(WelcomePlayer(Kernel.Maps[1002],140,0,15));if(travel){outsider.Entity.Teleport(map.ID,795,471);Check(outsider.Entity.MapID==1002,"Frantic actual teleport rejects unrelated player: "+p.Entity.Reborn);}outsider.BindWorldMap(map.ID);outsider.Entity.X=795;outsider.Entity.Y=471;var victim=targets[0];uint hp=victim.Hitpoints;
                attack(victim,outsider);victim.Die(outsider.Entity);Check(!victim.Dead && victim.Hitpoints==hp && count()==0,"Frantic damage and direct death both reject foreign owner: "+p.Entity.Reborn);outsider.BindWorldMap(1002);
                var fake=kill();Check((bool)Call(guard,null,"BlockAttack",p.Entity,fake) && count()==0,"Frantic unregistered family clone cannot enter runtime combat: "+p.Entity.Reborn);map.RemoveEntity(fake);
                attack(victim,p);Check(victim.Dead && count()==1 && !Fenced(p),"Frantic normal attack commits its owned trial kill: "+p.Entity.Reborn);victim.Die(p.Entity);Check(count()==1,"Frantic repeated direct death cannot duplicate runtime credit: "+p.Entity.Reborn);
                now=now.AddSeconds(3);pulse();Check(!map.Entities.ContainsKey(victim.UID) && map.Entities.Values.Count(v=>!v.Dead)==4,"Frantic runtime retires corpse and supplies the fifth required life: "+p.Entity.Reborn);
                foreach(var body in map.Entities.Values.Where(v=>!v.Dead).ToArray())attack(body,p);
                Check(stage()==6 && count()==5 && !Fenced(p),"Frantic normal combat earns five kills and durable completion: "+p.Entity.Reborn);
                now=now.AddSeconds(3);pulse();Check(map.Entities.Count==0,"Frantic completed trial prunes corpses and stops population: "+p.Entity.Reborn);
                if(travel){Call(scenes,null,"Tick");Check(p.Entity.MapID==1002 && p.Entity.X==440 && p.Entity.Y==345 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT COUNT(*) FROM revora_frantic_visits WHERE uid=@uid AND active",p.Entity.UID)==0 && stage()==6,"Frantic completed combat returns automatically to Doctor Li and preserves reward eligibility: "+p.Entity.Reborn);scene=null;}
                return;
            }
            p.BindWorldMap(map.ID);p.Entity.X=795;p.Entity.Y=471;var early=kill();Check(early.Dead && !credit(early) && count()==0 && !reward(),"Frantic real deaths during the wait cannot skip trial: "+p.Entity.Reborn);
            now=now.AddSeconds(59);Check(!credit(kill()) && count()==0,"Frantic fifty-nine seconds is too early: "+p.Entity.Reborn);
            now=now.AddSeconds(1);Check(!credit(early),"Frantic pre-unlock corpse cannot be replayed after waiting: "+p.Entity.Reborn);
            var first=kill();Check(credit(first) && !credit(first) && count()==1,"Frantic exact sixty-second boundary credits one real death once: "+p.Entity.Reborn);
            if(p.Entity.Reborn==0){now=now.AddMinutes(10);Check(!credit(kill()) && count()==1,"Frantic expired trial rejects further kills");clear();lock(p.ItemSyncRoot)recovered=(bool)Call(store,null,"RecoverTrial",p,attempt,(Func<bool>)(()=>true));Check(recovered && stage()==4 && count()==0 && CountId(p,721700)==1,"Frantic released interrupted trial restores poison and resets partial kills");
                lock(p.ItemSyncRoot)recovered=(bool)Call(store,null,"RecoverTrial",p,attempt,(Func<bool>)(()=>true));Check(!recovered,"Frantic old attempt cannot duplicate restored poison");create();Check(begin(),"Frantic recovered poison starts a fresh timed attempt");attempt=(Guid)Field(scene,"Visit");p.BindWorldMap(map.ID);p.Entity.X=795;p.Entity.Y=471;now=now.AddSeconds(60);Check(credit(kill()),"Frantic fresh trial restarts its five-kill count");}
            for(int i=0;i<3;i++){var b=kill();credit(b);credit(b);}Check(count()==4 && stage()==5 && !reward(),"Frantic four kills cannot award the vial: "+p.Entity.Reborn);
            var fifth=kill();Check(credit(fifth) && !credit(fifth) && stage()==6 && count()==5 && Call(store,null,"ActiveTrial",p)==null,"Frantic fifth kill completes attempt once: "+p.Entity.Reborn);
            Check(!credit(kill()),"Frantic completed attempt cannot add kills: "+p.Entity.Reborn);clear();EntityTable.SaveEntity(p);p=ReloadBall(p);ulong silver=p.Entity.Money;
            if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!reward() && stage()==6,"Frantic full bag preserves won trial for later vial claim");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT frantic_vial_fault CHECK(id<>721701) NOT VALID");failed=false;try{reward();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT frantic_vial_fault");}Check(failed && stage()==6 && CountId(p,721701)==0 && p.Entity.Money==silver,"Frantic failed vial insertion preserves won trial and silver");}
            Check(reward() && !reward() && stage()==7 && CountId(p,721701)==1 && p.Inventory.Objects.Single(i=>i.ID==721701).Bound && p.Entity.Money==silver+100,"Frantic won trial issues one bound vial and one payment: "+p.Entity.Reborn);
            p=ReloadBall(p);Check(stage()==7 && CountId(p,721701)==1 && count()==5,"Frantic completed trial and vial survive reload: "+p.Entity.Reborn);
        }finally{clear();clock.SetValue(null,oldClock);}
    }

    static void VerifyFranticInvestigation(string runtime,Map town,bool trial=false,bool combat=false,bool travel=false,bool admission=false,bool apes=false,bool runtimeApes=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FranticMonkeysStore",true);World(runtime,1020);Call(store,null,"Initialize");
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object prior=roll.GetValue(null);try{roll.SetValue(null,(Func<int,int>)(n=>0));
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)50:(byte)140,life,15));ulong money=p.Entity.Money;p.BindWorldMap(1020);p.Entity.X=668;p.Entity.Y=673;
            Check(step(p,0) && !step(p,0) && stage(p)==1,"Frantic acceptance is once per saved life: "+life);
            uint soul=checked((uint)Scalar("SELECT soul FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID));var early=kill(p,"Macaque");Check(!credit(p,early),"Frantic pre-referral kill is not eligible: "+life);
            Check(step(p,1) && !step(p,1) && !step(p,2) && p.Entity.Money==money+100,"Frantic referral cannot be replayed or skip the heart: "+life);
            Check(!credit(p,early) && !credit(p,kill(p,"Ratling")),"Frantic old death and wrong family cannot earn a heart: "+life);
            if(life==0){roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"Macaque");Check(!credit(p,miss),"Frantic roll 50 misses the explicit fifty-percent chance");roll.SetValue(null,(Func<int,int>)(n=>49));Check(!credit(p,miss),"Frantic failed roll cannot be rerolled by replay");
                while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"Macaque");Check(!credit(p,full) && CountId(p,721699)==0,"Frantic full bag cannot receive the heart");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);Check(!credit(p,full),"Frantic full-bag kill cannot be replayed after freeing space");}
            var body=kill(p,life==1?"MacaqueL48":"Macaque");Check(credit(p,body) && !credit(p,body) && !credit(p,kill(p,"Macaque")) && CountId(p,721699)==1,"Frantic real death grants one tracked bound heart: "+life);
            Check(p.Inventory.Objects.Single(i=>i.ID==721699).Bound,"Frantic heart is bound: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(step(p,2) && !step(p,2) && stage(p)==3 && p.Entity.Money==money+200,"Frantic Bao inspection survives reload and pays once: "+life);
            if(life==0){uint heart=p.Inventory.Objects.Single(i=>i.ID==721699).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)heart);p=ReloadBall(p);Check(!recover(p) && !step(p,3),"Frantic stored heart cannot be copied or consumed");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)heart);p=ReloadBall(p);
                ItemHandler.DropItem(new ItemUsage(true){UID=heart},p);Check(recover(p) && !recover(p) && stage(p)==3 && CountId(p,721699)==0,"Frantic missing inspected heart resets tracking without awarding a replacement");
                Check(credit(p,kill(p,"Macaque")) && !step(p,2) && p.Entity.Money==money+200,"Frantic heart can be hunted again without another inspection payment");
                Query("ALTER TABLE items ADD CONSTRAINT frantic_poison_fault CHECK(id<>721700) NOT VALID");bool failed=false;try{step(p,3);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT frantic_poison_fault");}
                Check(failed && stage(p)==3 && CountId(p,721699)==1 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=721700",p.Entity.UID)==0,"Frantic failed poison insert preserves heart, stage and payment");p=ReloadBall(p);while(p.Inventory.Count<40)Pack(p,1088000,1);
            }
            Check(step(p,3) && !step(p,3) && stage(p)==4 && CountId(p,721699)==0 && CountId(p,721700)==1 && p.Entity.Money==money+300,"Frantic Li exchange atomically replaces heart with poison: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);uint poison=p.Inventory.Objects.Single(i=>i.ID==721700).UID;Check(!step(p,4) && stage(p)==4 && Scalar("SELECT soul FROM revora_frantic_monkeys WHERE uid=@uid",p.Entity.UID)==soul,"Frantic reload cannot skip the pending trial or reroll P7: "+life);
            ItemHandler.DropItem(new ItemUsage(true){UID=poison},p);Check(recover(p) && !recover(p) && CountId(p,721700)==1 && p.Inventory.Objects.Single(i=>i.ID==721700).Bound,"Frantic lost poison replacement is bound and one-for-one: "+life);
            Check(Scalar("SELECT COUNT(*) FROM revora_frantic_recoveries WHERE uid=@uid AND type=721700",p.Entity.UID)==1,"Frantic poison recovery has a durable receipt: "+life);
            if(admission)VerifyFranticAdmission(p,apes,runtimeApes);else if(trial)VerifyFranticTrial(p,combat,travel);
        }}finally{roll.SetValue(null,prior);}
    }

    static void VerifySoulGeneralQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.SoulGeneralQuest",true);var phantom=typeof(Entity).Assembly.GetType("Revora.Game.Features.SoulGeneralPhantom",true);
        World(runtime,1020);World(runtime,1011);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var cells=(ushort[,])phantom.GetField("SpawnPoints",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);for(int i=0;i<cells.GetLength(0);i++)Check(Kernel.Maps[1020].Floor[cells[i,0],cells[i,1],MapObjectType.Monster],"Soul phantom landing stays open after Amber NPC footprint: "+i);
        var clock=phantom.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_soul_general WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==4487 || npc==4489?(ushort)1020:(ushort)1002);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;hit(p,b);return b;};
        Action<GameState> journal=p=>{int st=stage(p),current=st==0?0:st==1?1:st<=3?2:st==4?3:st<=6?4:st<=8?5:st<=10?6:st==11?7:8;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1751 && (uint)x.UID<=1757).ToArray();Check(own.Length==7 && own.All(x=>x.Status==(current>(int)x.UID-1750?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1750?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Soul ordinary saved journal stage "+st);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)55:(byte)140,life,15));p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
            p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=101},p);Check(stage(p)==0,"Soul remote NPC request rejected: "+life);
            for(uint id=1751;id<=1757;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Soul forged Begin rejected: "+id);}
            talk(p,900071727,101);talk(p,4488,1);Check(stage(p)==1,"Soul Zhao cannot skip Amber referral: "+life);journal(p);talk(p,4487,1);talk(p,4488,1);talk(p,4487,1);Check(stage(p)==4 && CountId(p,721704)==1 && p.Entity.Money==money+200,"Soul normal referrals issue silk pack: "+life);journal(p);
            p.Entity.X=219;p.Entity.Y=505;var snake=kill(p,"Snakeman");Call(quest,null,"OnDeath",p,snake);Check(Scalar("SELECT snake FROM revora_soul_general WHERE uid=@uid",p.Entity.UID)==1,"Soul ordinary kill dispatch credits snake once: "+life);
            p.BindWorldMap(1011);p.Entity.X=923;p.Entity.Y=563;for(int i=0;i<10;i++)kill(p,"RockMonsterL42");p.BindWorldMap(1020);p.Entity.X=620;p.Entity.Y=367;for(int i=0;i<10;i++)kill(p,"GiantApeL53");
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,4487,1);talk(p,4489,1);talk(p,4487,1);talk(p,4490,1);Check(stage(p)==7 && CountId(p,721704)==0 && CountId(p,721705)==0,"Soul scents portrait and illness ordering through normal dialogue: "+life);talk(p,4488,1);talk(p,4490,1);Check(stage(p)==9 && p.Entity.Money==money+500,"Soul Guard referral opens confrontation: "+life);journal(p);
            talk(p,4487,1);talk(p,4487,1);Check(!Fenced(p) && (bool)Call(phantom,null,"HasOwner",p.Entity.UID),"Soul Amber dialogue starts one phantom: "+life);
            if(life==0){now=now.AddSeconds(299);Call(quest,null,"Tick");Check((bool)Call(phantom,null,"HasOwner",p.Entity.UID),"Soul phantom remains before five-minute boundary");now=now.AddSeconds(1);Call(quest,null,"Tick");Check(!(bool)Call(phantom,null,"HasOwner",p.Entity.UID) && stage(p)==9,"Soul scheduled expiry removes phantom and preserves stage");talk(p,4487,1);EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(!(bool)Call(phantom,null,"HasOwner",p.Entity.UID) && stage(p)==9,"Soul login clears previous session encounter");now=now.AddSeconds(61);talk(p,4487,1);Check((bool)Call(phantom,null,"HasOwner",p.Entity.UID),"Soul restart after login owns encounter; stage="+stage(p)+" fenced="+Fenced(p)+" attempts="+Scalar("SELECT COUNT(*) FROM revora_soul_phantoms WHERE uid=@uid AND state IN(0,1)",p.Entity.UID));while(p.Inventory.Count<40)Pack(p,1088000,1);}
            var target=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9917510);hit(p,target);now=now.AddSeconds(2);Call(quest,null,"Tick");
            if(life==0){Check(stage(p)==9,"Soul full bag retains won encounter through scheduled cleanup");EntityTable.SaveEntity(p);p=ReloadBall(p);Call(quest,null,"OnLogin",p);foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);talk(p,4487,2);talk(p,4487,2);}
            Check(stage(p)==10 && CountId(p,721706)==1,"Soul normal combat and recovery deliver one paper: "+life);journal(p);byte level=p.Entity.Level;ulong xp=p.Entity.Experience;talk(p,4488,1);talk(p,4488,1);Check(stage(p)==11 && p.Entity.Money==money+4500 && CountId(p,721706)==0,"Soul Zhao pays total 4500 silver once: "+life);Check(level>=Config.LevelCap?p.Entity.Level==level:p.Entity.Level>level || p.Entity.Experience>xp,"Soul experience rewards respect level cap: "+life);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT soul_quest_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,101);}finally{Query("ALTER TABLE items DROP CONSTRAINT soul_quest_fault");}Check(Fenced(p) && stage(p)==11 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id=728244",p.Entity.UID)==0,"Soul failed final NPC claim rolls back and fences session");p=ReloadBall(p);Call(quest,null,"OnLogin",p);}
            talk(p,900071727,101);talk(p,900071727,101);Check(stage(p)==12 && CountId(p,728244)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Soul ordinary final claim gives bound boots pack and P7 once: "+life);journal(p);
            p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,900071727,102);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==728244),p);Check(stage(p)==12 && CountId(p,160118)==1 && p.Inventory.Objects.Single(i=>i.ID==160118).Bound,"Soul completed recovery and ordinary pack opening survive reload: "+life);
        }}finally{clock.SetValue(null,old);}
    }
    static void VerifySoulGeneralStore(string runtime,Map town,bool phantom=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SoulGeneralStore",true);World(runtime,1020);World(runtime,1011);
        foreach(var m in MonsterInformation.MonsterInformations.Values.Where(m=>new[]{"Snakeman","RockMonsterL42","GiantApeL53","JadePhantom"}.Contains(m.Name)))System.Console.WriteLine("Soul catalog "+m.ID+" "+m.Name+" mesh="+m.Mesh+" level="+m.Level);
        Call(store,null,"Initialize");
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_soul_general WHERE uid=@uid",p.Entity.UID);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)55:(byte)140,life,15));ulong money=p.Entity.Money;p.BindWorldMap(1020);p.Entity.X=620;p.Entity.Y=367;
            Check(step(p,0) && !step(p,0) && stage(p)==1,"Soul General acceptance once per life: "+life);
            uint soul=(uint)Scalar("SELECT soul FROM revora_soul_general WHERE uid=@uid",p.Entity.UID);
            Check(!step(p,2) && step(p,1) && !step(p,1) && p.Entity.Money==money+100,"Soul General referral ordered and pays once: "+life);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!step(p,2) && stage(p)==2,"Soul General full bag preserves hair request");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);}
            Check(step(p,2) && !step(p,2) && CountId(p,721703)==1 && p.Inventory.Objects.Single(i=>i.ID==721703).Bound,"Soul General one bound Zhao hair: "+life);
            if(life==0){uint hair=p.Inventory.Objects.Single(i=>i.ID==721703).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)hair);p=ReloadBall(p);Check(!recover(p) && !step(p,3),"Soul General stored hair cannot be copied or consumed");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)hair);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=hair},p);Check(recover(p) && !recover(p) && CountId(p,721703)==1,"Soul General lost hair recovery once");
                Query("ALTER TABLE items ADD CONSTRAINT soul_silk_fault CHECK(id<>721704) NOT VALID");bool failed=false;try{step(p,3);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT soul_silk_fault");}Check(failed && stage(p)==3 && CountId(p,721703)==1 && p.Entity.Money==money+100,"Soul General failed silk insert rolls back hair payment and stage");}
            var early=kill(p,"Snakeman");Check(!credit(p,early),"Soul General scents need the issued pack: "+life);
            Check(step(p,3) && !step(p,3) && CountId(p,721703)==0 && CountId(p,721704)==1 && p.Entity.Money==money+200,"Soul General hair exchange issues silk and pays once: "+life);
            Check(!credit(p,early) && !credit(p,kill(p,"Macaque")) && !step(p,4),"Soul General rejects old corpse wrong family and early hand-in: "+life);
            var snake=kill(p,"Snakeman");Check(credit(p,snake) && !credit(p,snake) && !credit(p,kill(p,"Snakeman")),"Soul General snake scent capped and replay safe: "+life);
            Check(!credit(p,kill(p,"RockMonsterL42")),"Soul General rock scent requires Phoenix map: "+life);
            p.BindWorldMap(1011);p.Entity.X=923;p.Entity.Y=563;for(int n=0;n<10;n++)Check(credit(p,kill(p,"RockMonsterL42")),"Soul General rock scent "+n+" life "+life);
            Check(!credit(p,kill(p,"RockMonsterL42")),"Soul General rock counter capped: "+life);
            p.BindWorldMap(1020);p.Entity.X=620;p.Entity.Y=367;
            if(life==0){uint silk=p.Inventory.Objects.Single(i=>i.ID==721704).UID;ItemHandler.DropItem(new ItemUsage(true){UID=silk},p);Check(!credit(p,kill(p,"GiantApeL53")) && recover(p) && !recover(p),"Soul General missing silk blocks scents and restores once");}
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(Scalar("SELECT snake+rock+ape FROM revora_soul_general WHERE uid=@uid",p.Entity.UID)==11,"Soul General scent totals survive reload and recovery: "+life);
            for(int n=0;n<10;n++)Check(credit(p,kill(p,"GiantApeL53")),"Soul General ape scent "+n+" life "+life);
            Check(!credit(p,kill(p,"GiantApeL53")) && step(p,4) && !step(p,4) && CountId(p,721704)==0 && p.Entity.Money==money+300,"Soul General scent hand-in consumes silk and pays once: "+life);
            Check(step(p,5) && !step(p,5) && CountId(p,721705)==1 && p.Inventory.Objects.Single(i=>i.ID==721705).Bound,"Soul General painter issues one bound portrait: "+life);
            uint portrait=p.Inventory.Objects.Single(i=>i.ID==721705).UID;ItemHandler.DropItem(new ItemUsage(true){UID=portrait},p);Check(recover(p) && !recover(p) && step(p,6) && !step(p,6) && p.Entity.Money==money+400,"Soul General portrait recovery and hand-in once: "+life);
            Check(!step(p,8) && step(p,7) && !step(p,7) && step(p,8) && !step(p,8) && stage(p)==9 && p.Entity.Money==money+500,"Soul General illness referral requires Zhao before Guard and pays once: "+life);
            p=ReloadBall(p);Check(stage(p)==9 && Scalar("SELECT soul FROM revora_soul_general WHERE uid=@uid",p.Entity.UID)==soul && !recover(p),"Soul General investigation and frozen reward survive reload: "+life);
            if(phantom)VerifySoulGeneralPhantom(p,town);
        }
    }
    static void VerifySoulGeneralPhantom(GameState p,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SoulGeneralStore",true);var runtime=typeof(Entity).Assembly.GetType("Revora.Game.Features.SoulGeneralPhantom",true);
        var clock=runtime.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);
        Func<bool> start=()=> (bool)Call(runtime,null,"Start",p);Func<long> stage=()=>Scalar("SELECT stage FROM revora_soul_general WHERE uid=@uid",p.Entity.UID);
        Func<bool> recover=()=>{lock(p.ItemSyncRoot)return (bool)Call(runtime,null,"Recover",p);};
        Action tick=()=>{Call(runtime,null,"Tick");};Func<Entity> body=()=>p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9917510);
        Action<Entity,Entity> hit=(actor,target)=>{uint damage=target.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(actor,target,new Attack(true){AttackType=Attack.Melee,Attacker=actor.UID,Attacked=target.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));p.BindWorldMap(1020);p.Entity.X=620;p.Entity.Y=367;Check(!start(),"Soul phantom rejects remote summon: "+p.Entity.Reborn);p.Entity.X=865;p.Entity.Y=540;
            Check(start() && !start(),"Soul phantom admits one owned encounter: "+p.Entity.Reborn);var stale=body();
            p.BindWorldMap(1002);tick();Check(!(bool)Call(runtime,null,"HasOwner",p.Entity.UID) && stage()==9,"Soul phantom departure removes actors and preserves quest: "+p.Entity.Reborn);
            p.BindWorldMap(1020);p.Entity.X=865;p.Entity.Y=540;Check(!start(),"Soul phantom abandoned attempt keeps one-minute cooldown: "+p.Entity.Reborn);now=now.AddSeconds(61);Check(start(),"Soul phantom retries after cooldown: "+p.Entity.Reborn);
            var current=body();hit(p.Entity,stale);Check(!stale.Dead && !current.Dead,"Soul phantom stale actor cannot enter combat: "+p.Entity.Reborn);
            var other=ReloadBall(WelcomePlayer(town,140,0,15));other.BindWorldMap(1020);other.Entity.X=865;other.Entity.Y=540;hit(other.Entity,current);current.Die(other.Entity);
            Check(!current.Dead && stage()==9,"Soul phantom rejects foreign attack and direct death: "+p.Entity.Reborn);
            if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);}
            hit(p.Entity,current);Check(current.Dead,"Soul phantom dies through ordinary owned combat: "+p.Entity.Reborn);now=now.AddSeconds(2);tick();
            if(p.Entity.Reborn==0){Check(stage()==9 && Scalar("SELECT COUNT(*) FROM revora_soul_phantoms WHERE uid=@uid AND state=1",p.Entity.UID)==1,"Soul phantom full bag preserves saved victory");
                EntityTable.SaveEntity(p);p=ReloadBall(p);Check(!recover(),"Soul phantom saved victory waits while bag remains full");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT soul_paper_fault CHECK(id<>721706) NOT VALID");bool failed=false;try{recover();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT soul_paper_fault");}Check(failed && stage()==9 && CountId(p,721706)==0,"Soul phantom paper failure preserves victory");Check(recover(),"Soul phantom saved victory delivers after retry");}
            Check(stage()==10 && CountId(p,721706)==1 && p.Inventory.Objects.Single(i=>i.ID==721706).Bound && !recover() && !start(),"Soul phantom grants one bound tracked paper: "+p.Entity.Reborn);
            uint paper=p.Inventory.Objects.Single(i=>i.ID==721706).UID;ItemHandler.DropItem(new ItemUsage(true){UID=paper},p);lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true)),"Soul paper lost after victory recovers: "+p.Entity.Reborn);
            ulong silver=p.Entity.Money;lock(p.ItemSyncRoot){Check((bool)Call(store,null,"HandInPaper",p,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"HandInPaper",p,(Func<bool>)(()=>true)),"Soul Zhao hand-in pays once: "+p.Entity.Reborn);}
            Check(stage()==11 && p.Entity.Money==silver+4000 && CountId(p,721706)==0,"Soul paper consumption and 4000 silver commit together: "+p.Entity.Reborn);
            if(p.Entity.Reborn==0){Query("ALTER TABLE items ADD CONSTRAINT soul_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{lock(p.ItemSyncRoot)Call(store,null,"Claim",p,(Func<bool>)(()=>true));}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT soul_final_fault");}Check(failed && stage()==11 && CountId(p,728244)==0,"Soul P7 failure rolls back boots and final stage");}
            lock(p.ItemSyncRoot){Check((bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true)),"Soul final claim succeeds once: "+p.Entity.Reborn);}
            p=ReloadBall(p);Check(stage()==12 && CountId(p,728244)==1 && p.Inventory.Objects.Single(i=>i.ID==728244).Bound && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,"Soul completion and both bound rewards survive reload: "+p.Entity.Reborn);
        }finally{p.BindWorldMap(1002);tick();clock.SetValue(null,old);}
    }
    static void VerifySoulGeneralPack(Map town){
        VerifySingleEquipmentPack(town,728244,160118,"Soul General");
        foreach(byte job in new byte[]{15,25,45,55,65,75,85,135,145,165}){
            var p=ReloadBall(WelcomePlayer(town,140,0,job));var pack=NestedPack(p,728244,1,true);ItemHandler.UseItem(pack,p);ItemHandler.UseItem(pack,p);
            Check(!Fenced(p) && CountId(p,728244)==0 && CountId(p,160118)==1 && p.Inventory.Objects.Single(x=>x.ID==160118).Bound,"Soul General ordinary boot-pack dispatch and replay: "+job);
            p=ReloadBall(p);Check(CountId(p,160118)==1 && FixedUses(p)==1,"Soul General boots and receipt survive reload: "+job);
        }
    }
    static void VerifyMadManScenes(Map town){
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);var p=ReloadBall(WelcomePlayer(town,140,2,15));var other=ReloadBall(WelcomePlayer(town,140,2,15));
        var clock=type.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);int maps=Kernel.Maps.Count,metadata=MapsTable.MapInformations.Count;
        var scenes=new System.Collections.Generic.List<object>();try{clock.SetValue(null,(Func<DateTime>)(()=>now));
        for(int round=0;round<3;round++){
            object scene;lock(p.ItemSyncRoot)scene=Call(type,null,"Create",p);scenes.Add(scene);var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1020 && map.ID!=1020 && map.Entities.Count==0 && map.Portals.Count==0 && map.Npcs.Count==7 && MapsTable.MapInformations[map.ID].Owner==p.Entity.UID,"Mad trial has seven owned stones and no inherited actors: "+round);
            Check((DateTime)Field(scene,"Until")==now.AddMinutes(5) && map.Floor[553,584,MapObjectType.Player],"Mad trial reserves five minutes and an open entry: "+round);
            Check((bool)Call(type,null,"Registered",scene,p) && !(bool)Call(type,null,"Registered",scene,other),"Mad trial registration rejects foreign owner: "+round);
            foreach(var npc in map.Npcs.Values)Check(npc.Mesh/10==555+npc.UID-9917810 && map.Floor[npc.X-2,npc.Y,MapObjectType.Player],"Mad colour stone has correct model and usable approach: "+npc.UID);
            bool rejected=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Mad duplicate reservation refused: "+round);
            ushort prior=p.Entity.MapID;p.Entity.MapID=map.ID;rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}finally{p.Entity.MapID=prior;}Check(rejected && Kernel.Maps.ContainsKey(map.ID),"Mad cleanup cannot strand occupant: "+round);
            var first=map.Npcs[9917810];map.Npcs[9917810]=new NpcSpawn{UID=9917810,MapID=map.ID};rejected=false;try{Call(type,null,"Release",scene);}catch(InvalidOperationException){rejected=true;}finally{map.Npcs[9917810]=first;}Check(rejected,"Mad cleanup refuses replaced stone identity: "+round);
            Call(type,null,"Release",scene);Call(type,null,"Release",scene);scenes.Remove(scene);Check(!Kernel.Maps.ContainsKey(map.ID) && !MapsTable.MapInformations.ContainsKey(map.ID),"Mad cleanup idempotently removes owned map: "+round);
        }
        for(int i=0;i<8;i++){var owner=ReloadBall(WelcomePlayer(town,140,0,15));lock(owner.ItemSyncRoot)scenes.Add(Call(type,null,"Create",owner));}
        bool full=false;try{lock(p.ItemSyncRoot)Call(type,null,"Create",p);}catch(InvalidOperationException){full=true;}Check(full && Kernel.Maps.Count==maps+8,"Mad private scene capacity is bounded at eight");
        }finally{foreach(var scene in scenes)Call(type,null,"Release",scene);clock.SetValue(null,old);}
        Check(Kernel.Maps.Count==maps && MapsTable.MapInformations.Count==metadata,"Mad scene checks leave no terrain or metadata reservations");
    }
    static void VerifyGhostCityVisit(GameState p,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityScenes",true);Call(scenes,null,"InitializeTravel");CityGuardSystem.SpawnTalker(4505,1002,440,334,5640,"TaoistShine",0);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);object scene=null;
        Func<long> stage=()=>Scalar("SELECT stage FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
        Action create=()=>{lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p);};
        Func<bool> enter=()=>{lock(p.ItemSyncRoot)return (bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));};
        Func<string,bool> act=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,scene,(Func<bool>)(()=>true));};
        Func<string,bool> use=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Action<uint> approach=id=>{var map=(Map)Field(scene,"Map");var npc=map.Npcs[id];p.ActiveNpc=id;p.Entity.X=(ushort)(npc.X-2);p.Entity.Y=npc.Y;};
        Action gate=()=>{p.BindWorldMap(1002);p.Entity.X=440;p.Entity.Y=308;};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            uint charm=p.Inventory.Objects.Single(i=>i.ID==721713).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)charm);p=ReloadBall(p);create();gate();Check(!enter() && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid",p.Entity.UID)==0,"Ghost City stored Charm cannot reserve entry: "+p.Entity.Reborn);lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);Check((bool)Field(scene,"Released"),"Ghost City refused admission releases its unsaved reservation: "+p.Entity.Reborn);scene=null;
            Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)charm);p=ReloadBall(p);create();var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1780 && map.Npcs.Count==2 && map.Entities.Count==0 && map.Portals.Count==0 && map.Floor[150,150,MapObjectType.Player],"Ghost City private scene contains only owned investigation NPCs: "+p.Entity.Reborn);
            p.Entity.Teleport(map.ID,150,150);Check(p.Entity.MapID!=map.ID,"Ghost City naked teleport cannot enter reserved scene: "+p.Entity.Reborn);
            var foreign=ReloadBall(WelcomePlayer(town,140,0,15));lock(foreign.ItemSyncRoot)Check(!(bool)Call(scenes,null,"Enter",scene,foreign,(Func<bool>)(()=>true)),"Ghost City entry rejects unrelated owner: "+p.Entity.Reborn);
            p.Entity.X=300;p.Entity.Y=300;Check(!enter(),"Ghost City Charm requires one of the three marked gates: "+p.Entity.Reborn);gate();Check(enter() && enter() && p.Entity.MapID==map.ID && CountId(p,721713)==1 && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Ghost City real entry retains Charm and saves one visit: "+p.Entity.Reborn);
            Check(!act("MeetMoMo"),"Ghost City remote Mo Mo interaction rejected: "+p.Entity.Reborn);approach(9917911);Check(!act("CollectPowder"),"Ghost City pillar cannot skip meeting Mo Mo: "+p.Entity.Reborn);
            var original=map.Npcs[4526];map.Npcs[4526]=new NpcSpawn{UID=4526,MapID=map.ID,X=original.X,Y=original.Y};approach(4526);try{Check(!act("MeetMoMo"),"Ghost City replaced NPC object cannot grant progress: "+p.Entity.Reborn);}finally{map.Npcs[4526]=original;}
            Check(act("MeetMoMo") && !act("MeetMoMo"),"Ghost City meeting credits original Mo Mo once: "+p.Entity.Reborn);
            approach(9917911);
            if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!act("CollectPowder") && stage()==5,"Ghost City full bag preserves pillar entitlement");foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT ghost_powder_fault CHECK(id<>721712) NOT VALID");bool failed=false;try{act("CollectPowder");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT ghost_powder_fault");}Check(failed && stage()==5 && CountId(p,721712)==0 && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid AND powder_uid>0",p.Entity.UID)==0,"Ghost City powder insert failure rolls back item stage and receipt");
                now=now.AddMinutes(20);Check(!act("CollectPowder"),"Ghost City exact deadline rejects pillar collection");
                Query("CREATE FUNCTION suppress_ghost_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_ghost_return BEFORE UPDATE ON revora_ghost_visits FOR EACH ROW EXECUTE FUNCTION suppress_ghost_return()");try{Call(scenes,null,"Tick");}finally{Query("DROP TRIGGER suppress_ghost_return ON revora_ghost_visits;DROP FUNCTION suppress_ghost_return()");}
                Check(Fenced(p) && p.Entity.MapID==1002 && !(bool)Field(scene,"Released") && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Ghost City suppressed visit retirement fences owner and preserves unresolved scene");p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check(p.Entity.MapID==1002 && p.Entity.X==438 && p.Entity.Y==334 && CountId(p,721713)==1,"Ghost City expiry returns to clear tile beside Shine without losing Charm");
            }else if(p.Entity.Reborn==1){EntityTable.SaveEntity(p);uint uid=p.Entity.UID;Kernel.GamePool.Remove(uid);Call(scenes,null,"Tick");Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",uid)==1002 && Scalar("SELECT x FROM entities WHERE uid=@uid",uid)==438,"Ghost City offline cleanup persists safe return");p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);}
            else{EntityTable.SaveEntity(p);p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Call(scenes,null,"RecoverLogin",p);Check(p.Entity.MapID==1002,"Ghost City replacement session keeps safe loader fallback");}
            Check((bool)Field(scene,"Released") && !Kernel.Maps.ContainsKey(map.ID) && stage()==5 && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Ghost City interruption retires owned scene without resetting quest: "+p.Entity.Reborn);scene=null;
            now=now.AddSeconds(1);create();gate();Check(enter(),"Ghost City earned Charm can retry after interruption: "+p.Entity.Reborn);approach(9917911);Check(!act("CollectPowder"),"Ghost City previous visit's meeting cannot credit a new visit: "+p.Entity.Reborn);approach(4526);Check(act("MeetMoMo"),"Ghost City retry meets current Mo Mo: "+p.Entity.Reborn);approach(9917911);
            Check(act("CollectPowder") && !act("CollectPowder") && stage()==6 && CountId(p,721712)==1 && p.Inventory.Objects.Single(i=>i.ID==721712).Bound && CountId(p,721713)==1,"Ghost City pillar issues one tracked bound powder while retaining Charm: "+p.Entity.Reborn);
            Check(!use("InspectPowder"),"Ghost City inspection cannot occur inside private city: "+p.Entity.Reborn);Call(scenes,null,"Tick");Check((bool)Field(scene,"Released") && p.Entity.MapID==1002 && p.Entity.X==438 && p.Entity.Y==334,"Ghost City earned powder triggers safe completed return: "+p.Entity.Reborn);scene=null;
            uint powder=p.Inventory.Objects.Single(i=>i.ID==721712).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)powder);p=ReloadBall(p);Check(!use("Recover") && !use("InspectPowder"),"Ghost City stored powder cannot be copied or inspected: "+p.Entity.Reborn);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)powder);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=powder},p);
            Check(use("Recover") && !use("Recover") && p.Inventory.Objects.Single(i=>i.ID==721712).UID!=powder,"Ghost City earned lost powder gets one replacement: "+p.Entity.Reborn);
            charm=p.Inventory.Objects.Single(i=>i.ID==721713).UID;ItemHandler.DropItem(new ItemUsage(true){UID=charm},p);Check(use("Recover") && !use("Recover") && CountId(p,721712)==1 && CountId(p,721713)==1,"Ghost City Charm can be restored alongside earned powder: "+p.Entity.Reborn);
            ulong silver=p.Entity.Money;Check(use("InspectPowder") && !use("InspectPowder") && stage()==7 && p.Entity.Money==silver+100 && CountId(p,721712)==1 && CountId(p,721713)==1,"Ghost City Shine inspects powder once and retains both Dark City materials: "+p.Entity.Reborn);
            p=ReloadBall(p);Check(stage()==7 && p.Entity.Money==silver+100 && CountId(p,721712)==1 && CountId(p,721713)==1 && Scalar("SELECT COUNT(*) FROM revora_ghost_visits WHERE uid=@uid AND powder_uid>0",p.Entity.UID)==1,"Ghost City second-city entitlement and evidence persist: "+p.Entity.Reborn);
            VerifyDarkCity(p,town);
        }finally{if(scene!=null && !(bool)Field(scene,"Released")){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,prior);}
    }
    static void VerifyDarkCity(GameState p,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityScenes",true);var combat=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityCombat",true);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=clock.GetValue(null);var now=new DateTime(2035,1,1,14,0,0,DateTimeKind.Utc);object scene=null;
        Func<int> progress=()=> (int)Call(store,null,"CombatProgress",p);
        Action enter=()=>{p.BindWorldMap(1002);p.Entity.X=440;p.Entity.Y=308;lock(p.ItemSyncRoot){scene=Call(scenes,null,"Create",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Dark City material-backed admission: "+p.Entity.Reborn);}};
        Action tick=()=>{now=now.AddSeconds(3);Call(scenes,null,"Tick");};
        Action<Entity,Entity> hit=(who,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(who,b,new Attack(true){AttackType=Attack.Melee,Attacker=who.UID,Attacked=b.UID},ref damage,null);};
        Func<bool> collect=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"CollectBezoar",p,scene,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));p.LoadItemStats();enter();tick();var map=(Map)Field(scene,"Map");
            Check((bool)Field(scene,"Dark") && map.Entities.Count==9 && map.Entities.Values.All(b=>b.MonsterInfo.ID==9917920 || b.MonsterInfo.ID==9917921),"Dark City bounded ordinary wave before bosses: "+p.Entity.Reborn);
            Check(!collect() && CountId(p,721714)==0,"Dark City cannot collect unearned Bezoar: "+p.Entity.Reborn);
            var first=map.Entities.Values.First();p.Entity.X=first.X;p.Entity.Y=first.Y;
            var foreign=ReloadBall(WelcomePlayer(town,140,0,15));hit(foreign.Entity,first);first.Die(foreign.Entity);Check(!first.Dead && progress()==0,"Dark City foreign damage and death cannot credit: "+p.Entity.Reborn);
            bool refused=false;try{lock(p.ItemSyncRoot)Call(combat,null,"Spawn",scene,p,2,(ushort)150,(ushort)190);}catch(InvalidOperationException){refused=true;}Check(refused,"Dark City refuses an early boss: "+p.Entity.Reborn);
            if(p.Entity.Reborn==1){
                Query("ALTER TABLE revora_ghost_combat ADD CONSTRAINT dark_kill_fault CHECK(kills<>1) NOT VALID");try{hit(p.Entity,first);}finally{Query("ALTER TABLE revora_ghost_combat DROP CONSTRAINT dark_kill_fault");}
                Check(Fenced(p) && Scalar("SELECT COUNT(*) FROM revora_ghost_kills WHERE uid=@uid",p.Entity.UID)==0,"Dark City failed counter commit fences session and rolls back kill receipt");
                p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");scene=null;p.LoadItemStats();enter();tick();map=(Map)Field(scene,"Map");first=map.Entities.Values.First();p.Entity.X=first.X;p.Entity.Y=first.Y;Check(progress()==0,"Dark City failed kill may be earned on a fresh visit");
            }
            hit(p.Entity,first);first.Die(p.Entity);lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"CombatKill",p,scene,first,(Func<bool>)(()=>true)),"Dark City durable death receipt prevents replay: "+p.Entity.Reborn);
            Check(first.Dead && progress()==1 && !Fenced(p),"Dark City owned ordinary kill saves once: "+p.Entity.Reborn);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check((bool)Field(scene,"Released") && progress()==1,"Dark City interrupted wave cleans actors and preserves progress: "+p.Entity.Reborn);scene=null;p.LoadItemStats();enter();tick();map=(Map)Field(scene,"Map");hit(p.Entity,first);Check(progress()==1,"Dark City stale corpse cannot credit new visit: "+p.Entity.Reborn);
            for(int expected=2;expected<=102;expected++){
                var body=map.Entities.Values.First(b=>!b.Dead);uint family=body.MonsterInfo.ID;
                if(expected==101)Check(family==9917922 && body.MaxHitpoints==5000,"Dark City hundred combined kills unlock Hunter: "+p.Entity.Reborn);
                if(expected==102)Check(family==9917923 && body.MaxHitpoints==10000,"Dark City Hunter unlocks Siezer: "+p.Entity.Reborn);
                p.Entity.X=body.X;p.Entity.Y=body.Y;hit(p.Entity,body);if(Fenced(p) || progress()!=expected)throw new Exception("Dark City kill did not save at "+expected);tick();
            }
            Check(map.Entities.Count==0 && progress()==102 && CountId(p,721714)==0,"Dark City completed combat stops spawning and preserves claim: "+p.Entity.Reborn);
            if(p.Entity.Reborn==0){
                Query("ALTER TABLE items ADD CONSTRAINT dark_bezoar_fault CHECK(id<>721714) NOT VALID");bool failed=false;try{collect();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT dark_bezoar_fault");}
                Check(failed && CountId(p,721712)==1 && CountId(p,721714)==0 && progress()==102 && Scalar("SELECT stage FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==7,"Dark City failed item insertion rolls back powder and stage");
                while(p.Inventory.Count<40)Pack(p,1088000,1);
            }
            Check(collect() && !collect() && CountId(p,721714)==1 && CountId(p,721712)==0 && p.Inventory.Objects.Single(i=>i.ID==721714).Bound && Scalar("SELECT stage FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==8,"Dark City powder exchanges atomically for one bound Bezoar even in a full bag: "+p.Entity.Reborn);
            lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);scene=null;p=ReloadBall(p);Check(progress()==102 && CountId(p,721714)==1 && Scalar("SELECT COUNT(*) FROM revora_ghost_kills WHERE uid=@uid",p.Entity.UID)==102,"Dark City earned item and all kill receipts persist: "+p.Entity.Reborn);
            uint bezoar=p.Inventory.Objects.Single(i=>i.ID==721714).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)bezoar);p=ReloadBall(p);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true)),"Dark City stored Bezoar cannot be copied: "+p.Entity.Reborn);
            Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)bezoar);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=bezoar},p);
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true)),"Dark City lost earned Bezoar recovers once: "+p.Entity.Reborn);
            p.LoadItemStats();enter();tick();Check(p.Map.Entities.Count==0 && progress()==102,"Dark City rescued-material return does not repeat completed combat: "+p.Entity.Reborn);
            VerifyGhostCityRewards(p,scene);

        }finally{if(scene!=null && !(bool)Field(scene,"Released")){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,prior);}
    }
    static void VerifyGhostCityRewards(GameState p,object scene){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityScenes",true);
        Func<bool> feed=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"FeedMoMo",p,scene,(Func<bool>)(()=>true));};
        Func<string,bool> use=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<long> stage=()=>Scalar("SELECT stage FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
        ulong silver=p.Entity.Money;Check(!feed() && !use("ReportRescue") && !use("Claim"),"Ghost rescue rejects remote feeding and premature rewards: "+p.Entity.Reborn);
        var map=(Map)Field(scene,"Map");
        Func<int,bool> rescue=child=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"RescueChild",p,scene,child,(Func<bool>)(()=>true));};
        Check(map.Npcs.Count==7 && !rescue(-1) && !rescue(5) && !rescue(0),"Ghost children require exact owned NPC and valid child index: "+p.Entity.Reborn);
        if(p.Entity.Reborn<2){
            for(int child=0;child<5;child++){
                var kid=map.Npcs[(uint)(9917930+child)];p.ActiveNpc=kid.UID;p.Entity.X=(ushort)(kid.X-2);p.Entity.Y=kid.Y;
                if(child==0 && p.Entity.Reborn==0){
                    Query("CREATE FUNCTION suppress_ghost_child_progress() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_ghost_child_progress BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_ghost_child_progress()");bool failed=false;ulong before=p.Entity.Experience;
                    try{rescue(child);}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_ghost_child_progress ON entities;DROP FUNCTION suppress_ghost_child_progress()");}
                    Check(failed && Scalar("SELECT COUNT(*) FROM revora_ghost_children WHERE uid=@uid",p.Entity.UID)==0 && p.Entity.Experience==before,"Ghost failed child EXP write rolls back child receipt");
                }
                byte beforeLevel=p.Entity.Level;ulong beforeXp=p.Entity.Experience,xp=beforeLevel>=Config.LevelCap?0:p.ExpBall/6;
                Check(rescue(child) && !rescue(child) && CountId(p,721714)==1,"Ghost optional child rescue credits once and retains Bezoar: "+p.Entity.Reborn+":"+child);
                ulong gainedXp=p.Entity.Experience;for(byte l=beforeLevel;l<p.Entity.Level;l++)gainedXp+=DataHolder.LevelExperience(l);
                Check(gainedXp==beforeXp+xp,"Ghost child grants one-sixth EXP ball with level cap: "+p.Entity.Reborn+":"+child);
            }
        }
        var npc=map.Npcs[4526];p.ActiveNpc=4526;p.Entity.X=(ushort)(npc.X-2);p.Entity.Y=npc.Y;
        map.Npcs[4526]=new NpcSpawn{UID=4526,MapID=map.ID,X=npc.X,Y=npc.Y};try{Check(!feed(),"Ghost rescue rejects substituted Mo Mo: "+p.Entity.Reborn);}finally{map.Npcs[4526]=npc;}
        if(p.Entity.Reborn==0){
            Query("CREATE FUNCTION suppress_ghost_feed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_ghost_feed BEFORE UPDATE ON revora_ghost_city FOR EACH ROW EXECUTE FUNCTION suppress_ghost_feed()");
            bool failed=false;try{feed();}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_ghost_feed ON revora_ghost_city;DROP FUNCTION suppress_ghost_feed()");}
            Check(failed && stage()==8 && CountId(p,721714)==1 && CountId(p,721713)==1 && p.Entity.Money==silver && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)silver && !(bool)Field(scene,"Completed"),"Ghost feeding rollback retains materials money and scene entitlement");
        }
        Check(feed() && !feed() && stage()==9 && CountId(p,721714)==0 && CountId(p,721713)==0 && p.Entity.Money==silver+100,"Ghost Mo Mo consumes exact Bezoar and Charm and pays once: "+p.Entity.Reborn);
        Check(!use("ReportRescue"),"Ghost report waits for owned scene cleanup: "+p.Entity.Reborn);Call(scenes,null,"Tick");Check((bool)Field(scene,"Released") && p.Entity.MapID==1002,"Ghost feeding triggers safe automatic return: "+p.Entity.Reborn);
        p=ReloadBall(p);Check(stage()==9 && p.Entity.Money==silver+100 && CountId(p,721714)==0,"Ghost rescue completion survives reload: "+p.Entity.Reborn);
        Check(Scalar("SELECT COUNT(*) FROM revora_ghost_children WHERE uid=@uid",p.Entity.UID)==(p.Entity.Reborn<2?5:0),"Ghost optional child receipts survive reload without requiring all rescues: "+p.Entity.Reborn);
        if(p.Entity.Reborn==0){
            Query("CREATE FUNCTION suppress_ghost_report() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_ghost_report BEFORE UPDATE ON revora_ghost_city FOR EACH ROW EXECUTE FUNCTION suppress_ghost_report()");
            bool failed=false;ulong prior=p.Entity.Experience;try{use("ReportRescue");}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_ghost_report ON revora_ghost_city;DROP FUNCTION suppress_ghost_report()");}
            Check(failed && stage()==9 && p.Entity.Money==silver+100 && p.Entity.Experience==prior && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(silver+100),"Ghost report failure rolls back silver experience and completion");
        }
        byte level=p.Entity.Level;ulong experience=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall;
        Check(use("ReportRescue") && !use("ReportRescue") && stage()==10 && p.Entity.Money==silver+6100,"Ghost Aunt rewards 6000 silver exactly once: "+p.Entity.Reborn);
        ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);
        Check(gained==experience+award,"Ghost Aunt grants one EXP ball with level cap: "+p.Entity.Reborn);
        p=ReloadBall(p);Check(stage()==10 && p.Entity.Money==silver+6100 && !use("ReportRescue"),"Ghost Aunt reward survives reload: "+p.Entity.Reborn);
        uint soul=(uint)Scalar("SELECT soul FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
        if(p.Entity.Reborn==0){
            while(p.Inventory.Count<38)Pack(p,1088000,1);Check(!use("Claim") && stage()==10 && CountId(p,728246)==0,"Ghost final claim preserves rewards without three free slots");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT ghost_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{use("Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT ghost_final_fault");}
            Check(failed && stage()==10 && CountId(p,728246)==0 && CountId(p,723911)==0 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728246,723911)",p.Entity.UID)==0,"Ghost P7 insert failure rolls back all final items and claim");
        }
        int claims=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(use("Claim"))System.Threading.Interlocked.Increment(ref claims);});
        Check(claims==1 && stage()==11 && !use("Claim"),"Ghost concurrent final claims pay once: "+p.Entity.Reborn);
        p=ReloadBall(p);Check(stage()==11 && new uint[]{728246,723911,soul}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound) && Scalar("SELECT cardinality(rewards) FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==3 && !use("Claim"),"Ghost bound armor pack EXP ball frozen P7 and receipt persist: "+p.Entity.Reborn);
    }
    static void VerifyTomboyQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.TomboyQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TomboyStore",true);
        World(runtime,1000);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=roll.GetValue(null);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_tomboy WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==900071727?(ushort)1002:(ushort)1000);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,id)=>ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==id),p);
        Func<GameState,string,Entity> kill=(p,name)=>{p.BindWorldMap(1000);p.Entity.X=356;p.Entity.Y=358;var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Action<GameState> journal=p=>{int st=stage(p);var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1795 && (uint)x.UID<=1799).ToArray();Check(own.Length==5 && own.All(x=>x.Status==(st>(int)x.UID-1794?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-1794?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Tomboy ordinary saved journal stage "+st);};
        try{roll.SetValue(null,(Func<int,int>)(n=>0));
            var low=ReloadBall(WelcomePlayer(town,77,0,15));talk(low,900071727,131);Check(stage(low)==0,"Tomboy ordinary level-77 character cannot accept");
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)78:(byte)140,life,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=131},p);Check(stage(p)==0,"Tomboy remote acceptance rejected: "+life);journal(p);
                for(uint id=1795;id<=1799;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Tomboy forged journal Begin rejected: "+id);}
                talk(p,900071727,131);talk(p,4719,12);Check(stage(p)==1,"Tomboy early Carolyn answer cannot skip gathering: "+life);journal(p);talk(p,4718,1);journal(p);
                for(int i=0;i<10;i++){var body=kill(p,i%2==0?"RockMonster":"RockMonsterL78");Call(quest,null,"OnDeath",p,body);}Check(CountId(p,710581)==10 && p.Inventory.Objects.Where(i=>i.ID==710581).All(i=>i.Bound),"Tomboy ordinary monster deaths earn ten bound Marbles without replay: "+life);
                p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);talk(p,4718,1);Check(stage(p)==3 && CountId(p,710581)==0 && p.Entity.Money==money+200,"Tomboy Lynn consumes saved Marbles and pays once: "+life);journal(p);
                for(int kind=0;kind<3;kind++){int selected=kind;roll.SetValue(null,(Func<int,int>)(n=>n==3?selected:0));kill(p,"RockMonsterL78");kill(p,"RockMonsterL78");}Check(new uint[]{721951,721952,721953}.All(id=>CountId(p,id)==2),"Tomboy ordinary level-78 hunt gives three stone types: "+life);
                if(life==0){roll.SetValue(null,(Func<int,int>)(n=>50));use(p,721951);Check(CountId(p,721951)==1 && CountId(p,721952)==1 && CountId(p,710582)==0,"Tomboy normal failed fusion consumes one pair");for(int kind=0;kind<2;kind++){int selected=kind;roll.SetValue(null,(Func<int,int>)(n=>n==3?selected:0));kill(p,"RockMonsterL78");}}
                roll.SetValue(null,(Func<int,int>)(n=>0));use(p,721951);use(p,721952);use(p,721953);Check(new uint[]{710582,710583,710584}.All(id=>CountId(p,id)==1),"Tomboy ordinary item dispatch creates all three jades: "+life);
                uint jade=p.Inventory.Objects.Single(i=>i.ID==710582).UID;ItemHandler.DropItem(new ItemUsage(true){UID=jade},p);talk(p,4718,2);talk(p,4718,2);Check(CountId(p,710582)==1 && p.Inventory.Objects.Single(i=>i.ID==710582).UID!=jade,"Tomboy Lynn restores lost earned jade once: "+life);
                talk(p,4718,1);talk(p,4718,1);Check(stage(p)==4 && p.Entity.Money==money+300 && new uint[]{710582,710583,710584}.All(id=>CountId(p,id)==0),"Tomboy ordinary jade hand-in consumes materials once: "+life);journal(p);
                talk(p,4719,11);Check(stage(p)==4 && p.Entity.Money==money+300,"Tomboy wrong Carolyn option grants no reward: "+life);talk(p,4719,12);talk(p,4719,12);Check(stage(p)==5 && p.Entity.Money==money+3300,"Tomboy correct Carolyn option pays once: "+life);journal(p);
                if(life==0){Query("ALTER TABLE items ADD CONSTRAINT tomboy_quest_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,131);}finally{Query("ALTER TABLE items DROP CONSTRAINT tomboy_quest_fault");}Check(Fenced(p) && stage(p)==5 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728241,723911)",p.Entity.UID)==0,"Tomboy failed ordinary final claim fences session without partial rewards");p=ReloadBall(p);Call(quest,null,"OnLogin",p);}
                talk(p,900071727,131);talk(p,900071727,131);Check(stage(p)==6 && CountId(p,728241)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Tomboy Chang grants bound pack EXP Ball and P7 once: "+life);journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);use(p,728241);Check(stage(p)==6 && CountId(p,120158)==1 && p.Inventory.Objects.Single(i=>i.ID==120158).Bound && CountId(p,723911)==1,"Tomboy completed quest and necklace opening survive reload: "+life);
            }
        }finally{roll.SetValue(null,prior);}
    }
    static void VerifyTomboyStore(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TomboyStore",true);World(runtime,1000);Call(store,null,"Initialize");
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=roll.GetValue(null);
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> fuse=(p,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Fuse",p,id,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_tomboy WHERE uid=@uid",p.Entity.UID);
        try{roll.SetValue(null,(Func<int,int>)(n=>0));
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)78:(byte)140,life,15));p.BindWorldMap(1000);p.Entity.X=356;p.Entity.Y=358;ulong money=p.Entity.Money;
                Check(step(p,0) && !step(p,0) && !step(p,2) && stage(p)==1,"Tomboy acceptance freezes one ordered run: "+life);uint soul=(uint)Scalar("SELECT soul FROM revora_tomboy WHERE uid=@uid",p.Entity.UID);
                var early=kill(p,"RockMonster");Check(!credit(p,early) && step(p,1) && !step(p,1) && !credit(p,early),"Tomboy early corpse cannot be used after Lynn referral: "+life);
                for(int i=0;i<10;i++)NestedPack(p,710581,1,true);Check(!step(p,2),"Tomboy ten unearned Marbles cannot satisfy hand-in: "+life);foreach(var item in p.Inventory.Objects.Where(i=>i.ID==710581).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                Check(!credit(p,kill(p,"Macaque")),"Tomboy wrong monster family rejected: "+life);
                if(life==0){roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"RockMonster");Check(!credit(p,miss),"Tomboy marble drop misses at 50 percent boundary");roll.SetValue(null,(Func<int,int>)(n=>0));Check(!credit(p,miss),"Tomboy missed corpse cannot reroll");
                    while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"RockMonster");Check(!credit(p,full),"Tomboy full bag rejects drop");foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);Check(!credit(p,full),"Tomboy cleared bag cannot reclaim old corpse");
                    Query("ALTER TABLE items ADD CONSTRAINT tomboy_drop_fault CHECK(id<>710581) NOT VALID");bool failed=false;try{credit(p,kill(p,"RockMonster"));}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tomboy_drop_fault");}Check(failed && CountId(p,710581)==0 && Scalar("SELECT COUNT(*) FROM revora_tomboy_materials WHERE uid=@uid",p.Entity.UID)==0,"Tomboy failed drop rolls back item tracking");
                }
                for(int i=0;i<10;i++){var body=kill(p,i%2==0?"RockMonster":"RockMonsterL78");if(!credit(p,body) || credit(p,body))throw new Exception("Tomboy earned marble/replay mismatch");}
                Check(CountId(p,710581)==10 && !credit(p,kill(p,"RockMonster")) && p.Inventory.Objects.Where(i=>i.ID==710581).All(i=>i.Bound),"Tomboy ten earned bound Marbles cap further drops: "+life);
                uint marble=p.Inventory.Objects.First(i=>i.ID==710581).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)marble);p=ReloadBall(p);Check(!step(p,2) && !recover(p),"Tomboy stored Marble cannot be consumed or copied: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)marble);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=marble},p);Check(recover(p) && !recover(p) && CountId(p,710581)==9 && credit(p,kill(p,"RockMonster")),"Tomboy lost Marble clears entitlement and requires a fresh kill: "+life);
                Check(step(p,2) && !step(p,2) && stage(p)==3 && CountId(p,710581)==0 && p.Entity.Money==money+200,"Tomboy consumes ten tracked Marbles and pays once: "+life);
                Check(!credit(p,kill(p,"RockMonster")),"Tomboy stones require level-78 variant: "+life);
                for(int kind=0;kind<3;kind++){int selected=kind;roll.SetValue(null,(Func<int,int>)(n=>n==3?selected:0));for(int i=0;i<2;i++)if(!credit(p,kill(p,"RockMonsterL78")))throw new Exception("Tomboy stone not granted");Check(!credit(p,kill(p,"RockMonsterL78")),"Tomboy stone cap respects owned type: "+kind+" life "+life);}
                roll.SetValue(null,(Func<int,int>)(n=>0));uint leaf=p.Inventory.Objects.First(i=>i.ID==721951).UID;
                if(life==0){Query("ALTER TABLE items ADD CONSTRAINT tomboy_fuse_fault CHECK(id<>710582) NOT VALID");bool failed=false;try{fuse(p,leaf);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tomboy_fuse_fault");}Check(failed && p.Inventory.ContainsUID(leaf) && CountId(p,721952)==2 && CountId(p,710582)==0 && Scalar("SELECT COUNT(*) FROM revora_tomboy_fusions WHERE uid=@uid",p.Entity.UID)==0,"Tomboy failed jade insert rolls back both stones and fusion receipt");
                    roll.SetValue(null,(Func<int,int>)(n=>50));Check(fuse(p,leaf) && !fuse(p,leaf) && CountId(p,710582)==0 && CountId(p,721951)==1 && CountId(p,721952)==1,"Tomboy committed failed fusion consumes pair exactly once");
                    for(int kind=0;kind<2;kind++){int selected=kind;roll.SetValue(null,(Func<int,int>)(n=>n==3?selected:0));if(!credit(p,kill(p,"RockMonsterL78")))throw new Exception("Tomboy replacement stone missing");}
                }
                roll.SetValue(null,(Func<int,int>)(n=>0));
                foreach(uint type in new uint[]{721951,721952,721953}){uint clicked=p.Inventory.Objects.First(i=>i.ID==type).UID;int successes=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(fuse(p,clicked))System.Threading.Interlocked.Increment(ref successes);});Check(successes==1,"Tomboy concurrent recipe consumes one pair: "+type+" life "+life);}
                Check(new uint[]{710582,710583,710584}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound),"Tomboy all three clicked-stone recipes create their distinct jade: "+life);
                uint jade=p.Inventory.Objects.Single(i=>i.ID==710582).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)jade);p=ReloadBall(p);Check(!step(p,3) && !recover(p),"Tomboy stored jade cannot be handed in or replaced: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)jade);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=jade},p);
                Check(recover(p) && !recover(p) && CountId(p,710582)==1 && p.Inventory.Objects.Single(i=>i.ID==710582).UID!=jade,"Tomboy lost earned jade restores once from fusion proof: "+life);
                if(life==0){Query("CREATE FUNCTION suppress_tomboy_handin() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_tomboy_handin BEFORE UPDATE ON revora_tomboy FOR EACH ROW EXECUTE FUNCTION suppress_tomboy_handin()");bool failed=false;try{step(p,3);}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_tomboy_handin ON revora_tomboy;DROP FUNCTION suppress_tomboy_handin()");}Check(failed && stage(p)==3 && new uint[]{710582,710583,710584}.All(id=>CountId(p,id)==1) && p.Entity.Money==money+200 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(money+200),"Tomboy suppressed hand-in rolls back jades money and stage");}
                Check(step(p,3) && !step(p,3) && stage(p)==4 && new uint[]{710582,710583,710584}.All(id=>CountId(p,id)==0) && p.Entity.Money==money+300,"Tomboy Lynn consumes three earned jades and pays once: "+life);
                p=ReloadBall(p);Check(stage(p)==4 && p.Entity.Money==money+300 && Scalar("SELECT soul FROM revora_tomboy WHERE uid=@uid",p.Entity.UID)==soul && !recover(p),"Tomboy referral fusion hand-in and frozen soul persist: "+life);
                VerifyTomboyRewards(p);
            }
        }finally{roll.SetValue(null,prior);}
    }
    static void VerifyTomboyRewards(GameState p){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TomboyStore",true);
        Func<byte,bool> answer=value=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Answer",p,value,(Func<bool>)(()=>true));};
        Func<bool> claim=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true));};
        Func<long> stage=()=>Scalar("SELECT stage FROM revora_tomboy WHERE uid=@uid",p.Entity.UID);ulong money=p.Entity.Money;
        Check(!answer(1) && !answer(3) && !claim() && stage()==4,"Tomboy wrong answers and early claims preserve progress: "+p.Entity.Reborn);
        if(p.Entity.Reborn==0){
            Query("CREATE FUNCTION suppress_tomboy_answer() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_tomboy_answer BEFORE UPDATE ON revora_tomboy FOR EACH ROW EXECUTE FUNCTION suppress_tomboy_answer()");bool failed=false;ulong prior=p.Entity.Experience;
            try{answer(2);}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_tomboy_answer ON revora_tomboy;DROP FUNCTION suppress_tomboy_answer()");}
            Check(failed && stage()==4 && p.Entity.Money==money && p.Entity.Experience==prior && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)money,"Tomboy failed answer write rolls back silver EXP and stage");
        }
        byte level=p.Entity.Level;ulong experience=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall/2;
        Check(answer(2) && !answer(2) && stage()==5 && p.Entity.Money==money+3000,"Tomboy correct recipe answer pays 3000 silver once: "+p.Entity.Reborn);
        ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);Check(gained==experience+award,"Tomboy answer grants half EXP ball with level cap: "+p.Entity.Reborn);
        p=ReloadBall(p);p.BindWorldMap(1002);Check(stage()==5 && p.Entity.Money==money+3000,"Tomboy answered state and silver persist: "+p.Entity.Reborn);
        uint soul=(uint)Scalar("SELECT soul FROM revora_tomboy WHERE uid=@uid",p.Entity.UID);
        if(p.Entity.Reborn==0){
            while(p.Inventory.Count<38)Pack(p,1088000,1);Check(!claim() && stage()==5,"Tomboy final reward requires three free slots");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT tomboy_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{claim();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tomboy_final_fault");}
            Check(failed && stage()==5 && CountId(p,728241)==0 && CountId(p,723911)==0 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728241,723911)",p.Entity.UID)==0,"Tomboy failed P7 insert rolls back every final item");
        }
        int claims=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(claim())System.Threading.Interlocked.Increment(ref claims);});Check(claims==1 && stage()==6 && !claim(),"Tomboy concurrent final claims pay once: "+p.Entity.Reborn);
        EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage()==6 && new uint[]{728241,723911,soul}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound) && Scalar("SELECT cardinality(rewards) FROM revora_tomboy WHERE uid=@uid",p.Entity.UID)==3 && !claim(),"Tomboy bound necklace pack EXP Ball frozen P7 and receipts survive reload: "+p.Entity.Reborn);
    }
    static void VerifyDesertBloodStore(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertBloodStore",true);World(runtime,1000);World(runtime,1015);Call(store,null,"Initialize");
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=roll.GetValue(null);
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> read=(p,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"ReadItem",p,id,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID);
        try{roll.SetValue(null,(Func<int,int>)(n=>0));
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)85:(byte)140,life,15));p.BindWorldMap(1000);p.Entity.X=165;p.Entity.Y=276;ulong money=p.Entity.Money;
                Check(step(p,0) && !step(p,0) && !step(p,2),"Desert ordered acceptance: "+life);
                var early=kill(p,"BladeGhostL83");Check(!credit(p,early) && step(p,1) && !credit(p,early),"Desert early corpse cannot become a quest drop: "+life);
                Check(!credit(p,kill(p,"BladeGhost")) && !step(p,2),"Desert spell requires level-83 family and reading: "+life);
                roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"BladeGhostL83");Check(!credit(p,miss),"Desert 50 percent drop boundary: "+life);roll.SetValue(null,(Func<int,int>)(n=>0));Check(!credit(p,miss),"Desert failed roll cannot replay: "+life);
                Check(credit(p,kill(p,"BladeGhostL83")) && !credit(p,kill(p,"BladeGhostL83")),"Desert spell capped to one: "+life);
                uint spell=p.Inventory.Objects.Single(i=>i.ID==721843).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)spell);p=ReloadBall(p);Check(!read(p,spell) && !recover(p),"Desert stored spell cannot be read or copied: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)spell);p=ReloadBall(p);
                ItemHandler.DropItem(new ItemUsage(true){UID=spell},p);Check(recover(p) && !recover(p),"Desert lost spell recovers once: "+life);spell=p.Inventory.Objects.Single(i=>i.ID==721843).UID;
                Check(read(p,spell) && !read(p,spell) && step(p,2) && CountId(p,721843)==1,"Desert read spell retained for Crypt return: "+life);
                VerifyDesertCrypt(p);
                Check(!credit(p,kill(p,"Macaque")),"Desert wrong family does not count: "+life);
                while(p.Inventory.Count<40)Pack(p,1088000,1);
                for(int i=0;i<200;i++){var b=kill(p,i%2==0?"BladeGhost":"BladeGhostL83");if(!credit(p,b) || credit(p,b))throw new Exception("Desert kill/replay mismatch");}
                Check(Scalar("SELECT kills FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID)==200 && CountId(p,721845)==0 && !recover(p) && !step(p,3),"Desert full bag preserves 200 kills without losing sword entitlement: "+life);
                foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                p=ReloadBall(p);Check(recover(p) && !recover(p) && CountId(p,721845)==1,"Desert deferred sword survives reload and issues once: "+life);
                uint sword=p.Inventory.Objects.Single(i=>i.ID==721845).UID;ItemHandler.DropItem(new ItemUsage(true){UID=sword},p);Check(recover(p) && !recover(p) && step(p,3) && !step(p,3),"Desert lost sword restores and hand-in pays once: "+life);
                Check(!credit(p,kill(p,"BladeGhost")),"Desert fangs require level-83 variant: "+life);
                for(int i=0;i<5;i++)if(!credit(p,kill(p,"BladeGhostL83")))throw new Exception("Desert fang missing");
                Check(!credit(p,kill(p,"BladeGhostL83")) && CountId(p,721846)==5,"Desert fang cap: "+life);
                uint fang=p.Inventory.Objects.First(i=>i.ID==721846).UID;ItemHandler.DropItem(new ItemUsage(true){UID=fang},p);Check(recover(p) && !recover(p) && CountId(p,721846)==4 && !step(p,4) && credit(p,kill(p,"BladeGhostL83")),"Desert lost fang must be hunted again: "+life);
                if(life==0){Query("CREATE FUNCTION suppress_desert_step() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_desert_step BEFORE UPDATE ON revora_desert_blood FOR EACH ROW EXECUTE FUNCTION suppress_desert_step()");bool failed=false;try{step(p,4);}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_desert_step ON revora_desert_blood;DROP FUNCTION suppress_desert_step()");}Check(failed && stage(p)==4 && CountId(p,721846)==5 && p.Entity.Money==money+300,"Desert suppressed hand-in rolls back five fangs silver and stage");}
                Check(step(p,4) && step(p,5) && CountId(p,721841)==1 && step(p,6) && CountId(p,721841)==0 && CountId(p,721842)==1,"Desert earned letter becomes Peach Bag: "+life);
                Check(step(p,7) && CountId(p,721842)==0 && CountId(p,721843)==0 && CountId(p,721844)==1 && p.Entity.Money==money+600,"Desert delivery consumes bag and spell atomically: "+life);
                p=ReloadBall(p);uint note=p.Inventory.Objects.Single(i=>i.ID==721844).UID;Check(read(p,note) && !read(p,note) && stage(p)==9,"Desert readable note advances saved run once: "+life);
                Func<bool> report=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Report",p,(Func<bool>)(()=>true));};
                Func<bool> claim=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true));};
                Check(!report() && !claim(),"Desert report and final claim require correct map and stage: "+life);p.BindWorldMap(1015);
                if(life==0){Query("ALTER TABLE revora_desert_blood ADD CONSTRAINT desert_report_fault CHECK(stage<>10) NOT VALID");bool failed=false;try{report();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_blood DROP CONSTRAINT desert_report_fault");}Check(failed && CountId(p,721844)==1 && p.Entity.Money==money+600 && stage(p)==9,"Desert report rollback preserves note money and stage");}
                byte level=p.Entity.Level;ulong xp=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall;
                Check(report() && !report() && CountId(p,721844)==0 && p.Entity.Money==money+6600,"Desert report pays once: "+life);ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);Check(gained==xp+award,"Desert direct EXP reward respects cap: "+life);
                p=ReloadBall(p);p.BindWorldMap(1002);uint soul=(uint)Scalar("SELECT soul FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID);
                if(life==0){while(p.Inventory.Count<38)Pack(p,1088000,1);Check(!claim(),"Desert final rewards require three slots");foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                    Query("ALTER TABLE items ADD CONSTRAINT desert_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{claim();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT desert_final_fault");}Check(failed && stage(p)==10 && CountId(p,728242)==0 && CountId(p,723911)==0,"Desert failed soul insert rolls back entire reward");}
                int claims=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(claim())System.Threading.Interlocked.Increment(ref claims);});Check(claims==1 && !claim(),"Desert concurrent final claim pays once: "+life);
                EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage(p)==11 && p.Entity.Money==money+6600 && new uint[]{728242,723911,soul}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound) && Scalar("SELECT cardinality(rewards) FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID)==3,"Desert bound final rewards and receipts persist: "+life);
            }
        }finally{roll.SetValue(null,prior);}
    }
    static void VerifyDesertCrypt(GameState p){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertBloodStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertBloodScenes",true);Call(store,null,"InitializeVisits");
        object scene;lock(p.ItemSyncRoot)scene=Call(scenes,null,"TryCreate",p);var map=(Map)Field(scene,"Map");
        Check(map.BaseID==1801 && map.Npcs.Count==2 && map.Entities.Count==0 && map.Portals.Count==0,"Desert Crypt has isolated terrain and exactly its actors");
        lock(p.ItemSyncRoot){Check(Call(scenes,null,"TryCreate",p)==null,"Desert Crypt refuses duplicate owner allocation");Check(!(bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert Crypt rejects remote gate entry");}
        p.Entity.X=384;p.Entity.Y=103;
        var other=ReloadBall(WelcomePlayer(Kernel.Maps[1002],100,0,15));Check(!(bool)Call(scenes,null,"AllowTeleport",other,map.ID,(ushort)50,(ushort)55),"Desert Crypt rejects foreign teleport");other.Entity.Teleport(map.ID,50,55);Check(other.Entity.MapID!=map.ID,"Desert Crypt production teleport guard rejects intruder");
        lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert earned spell permits real private Crypt entry");
        Check(p.Entity.MapID==map.ID && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Desert arrival has durable active visit");
        Func<bool> blessing=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"ColumnExperience",p,scene,(Func<bool>)(()=>true));};
        Check(!blessing(),"Desert remote column reward rejected");p.ActiveNpc=9918011;p.Entity.X=42;p.Entity.Y=50;
        if(p.Entity.Reborn==0){Query("ALTER TABLE revora_desert_column ADD CONSTRAINT desert_column_fault CHECK(false) NOT VALID");bool failed=false;ulong before=p.Entity.Experience;try{blessing();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_column DROP CONSTRAINT desert_column_fault");}Check(failed && p.Entity.Experience==before && Scalar("SELECT COUNT(*) FROM revora_desert_column WHERE uid=@uid",p.Entity.UID)==0,"Desert column failed receipt leaves EXP unchanged");}
        byte level=p.Entity.Level;ulong xp=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall/12;int paid=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(blessing())System.Threading.Interlocked.Increment(ref paid);});Check(paid==1 && !blessing(),"Desert concurrent column blessing pays once");ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);Check(gained==xp+award,"Desert column pays one-twelfth EXP ball with level cap");
        lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);Check(p.Entity.MapID==1000 && p.Entity.X==384 && p.Entity.Y==103 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Desert return saves location retires visit and disposes owned map");
        lock(p.ItemSyncRoot){scene=Call(scenes,null,"TryCreate",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert read spell permits repeat visit");}
        p.ActiveNpc=9918011;p.Entity.X=42;p.Entity.Y=50;Check(!blessing(),"Desert reentry cannot repeat column reward");
        scene.GetType().GetField("Until",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(scene,DateTime.UtcNow.AddSeconds(-1));Call(scenes,null,"Tick");Check(p.Entity.MapID==1000 && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Desert expired visit returns owner and retires receipt");
        lock(p.ItemSyncRoot){scene=Call(scenes,null,"TryCreate",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert recovery fixture enters real Crypt");}
        map=(Map)Field(scene,"Map");Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check(p.Entity.MapID==1000 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1000 && !Kernel.Maps.ContainsKey(map.ID),"Desert reconnect recovery saves return and releases stale terrain");
        Call(scenes,null,"RecoverLogin",p);Check(Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==0 && Scalar("SELECT COUNT(*) FROM revora_desert_column WHERE uid=@uid",p.Entity.UID)==1,"Desert repeated recovery preserves single column receipt");
        if(p.Entity.Reborn==0){
            lock(p.ItemSyncRoot){scene=Call(scenes,null,"TryCreate",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert disconnect fixture enters earned visit");}
            map=(Map)Field(scene,"Map");GameState removed;Check(Kernel.GamePool.TryRemove(p.Entity.UID,out removed),"Desert disconnected session removed from active pool");Call(scenes,null,"Tick");
            Check(!Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1000 && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Desert offline tick saves return before retiring visit and terrain");Kernel.GamePool[p.Entity.UID]=p;p.BindWorldMap(1000);p.Entity.X=384;p.Entity.Y=103;
            lock(p.ItemSyncRoot){scene=Call(scenes,null,"TryCreate",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Desert interrupted fixture starts from actual saved admission");}
            map=(Map)Field(scene,"Map");Kernel.GamePool.TryRemove(p.Entity.UID,out removed);Call(scenes,null,"Release",scene);
            Query("CREATE FUNCTION suppress_desert_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_desert_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_desert_return()");bool failed=false;try{Call(scenes,null,"RecoverInterrupted");}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_desert_return ON entities;DROP FUNCTION suppress_desert_return()");}
            Check(failed && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Desert failed startup return retains active receipt for retry");
            Call(scenes,null,"RecoverInterrupted");Call(scenes,null,"RecoverInterrupted");Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1000 && Scalar("SELECT COUNT(*) FROM revora_desert_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Desert startup return retries atomically and is idempotent");Kernel.GamePool[p.Entity.UID]=p;p.BindWorldMap(1000);p.Entity.X=384;p.Entity.Y=103;
            var owners=Enumerable.Range(0,9).Select(i=>ReloadBall(WelcomePlayer(Kernel.Maps[1002],100,0,15))).ToArray();var allocated=new System.Collections.Generic.List<object>();int mapsBefore=Kernel.Maps.Count;
            try{for(int i=0;i<8;i++){lock(owners[i].ItemSyncRoot)allocated.Add(Call(scenes,null,"TryCreate",owners[i]));}Check(allocated.All(x=>x!=null) && Kernel.Maps.Count==mapsBefore+8,"Desert eight distinct owners reserve eight isolated scenes");lock(owners[8].ItemSyncRoot)Check(Call(scenes,null,"TryCreate",owners[8])==null && Kernel.Maps.Count==mapsBefore+8,"Desert ninth owner cannot exceed scene capacity");}
            finally{foreach(var allocatedScene in allocated)if(allocatedScene!=null)Call(scenes,null,"Release",allocatedScene);}
            Check(Kernel.Maps.Count==mapsBefore,"Desert capacity cleanup releases all reserved maps");
        }
    }
    static void VerifyDesertBloodQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertBloodQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertBloodStore",true);World(runtime,1000);World(runtime,1015);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object prior=roll.GetValue(null);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc!=4623 && npc!=9918011)p.BindWorldMap(npc==900071727?(ushort)1002:npc==4633?(ushort)1015:(ushort)1000);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,id)=>ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==id),p);
        Action<GameState,string> kill=(p,name)=>{p.BindWorldMap(1000);p.Entity.X=165;p.Entity.Y=276;var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);Call(quest,null,"OnDeath",p,b);};
        Action<GameState> journal=p=>{int st=stage(p),current=st==11?9:Math.Min(st,8);var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1800 && (uint)x.UID<=1807).ToArray();Check(own.Length==8 && own.All(x=>x.Status==(current>(int)x.UID-1799?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1799?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Desert ordinary journal stage "+st);};
        try{roll.SetValue(null,(Func<int,int>)(n=>0));var low=ReloadBall(WelcomePlayer(town,84,0,15));talk(low,900071727,141);Check(stage(low)==0,"Desert underlevel acceptance rejected");
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)85:(byte)140,life,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=141},p);Check(stage(p)==0,"Desert remote acceptance rejected");
                for(uint id=1800;id<=1807;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Desert forged journal Begin rejected: "+id);}
                talk(p,900071727,141);journal(p);talk(p,4621,1);journal(p);talk(p,4622,1);Check(p.Entity.MapID==1000,"Desert column rejects missing spell");kill(p,"BladeGhostL83");use(p,721843);talk(p,4622,1);Check(p.Map.BaseID==1801 && !Fenced(p),"Desert ordinary column enters private Crypt");talk(p,4623,1);Check(stage(p)==3,"Desert ordinary Warrior starts hunt");talk(p,9918011,1);talk(p,9918011,2);Check(p.Entity.MapID==1000,"Desert ordinary column returns player");journal(p);
                for(int i=0;i<200;i++)kill(p,i%2==0?"BladeGhost":"BladeGhostL83");Check(CountId(p,721845)==1 && Scalar("SELECT kills FROM revora_desert_blood WHERE uid=@uid",p.Entity.UID)==200,"Desert actual deaths award sword at 200 without replay");
                p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);talk(p,4622,1);talk(p,4623,1);Check(stage(p)==4 && CountId(p,721845)==0,"Desert saved sword hand-in accepted");talk(p,9918011,2);journal(p);
                for(int i=0;i<5;i++)kill(p,"BladeGhostL83");talk(p,4622,1);talk(p,4623,1);Check(stage(p)==5 && CountId(p,721846)==0,"Desert ordinary fang hand-in");talk(p,9918011,2);talk(p,4621,1);Check(stage(p)==6 && CountId(p,721841)==1,"Desert Harvey grants tracked letter");journal(p);
                talk(p,4622,1);talk(p,4623,1);Check(stage(p)==7 && CountId(p,721842)==1,"Desert Warrior exchanges letter for Peach Bag");talk(p,9918011,2);talk(p,4633,1);Check(stage(p)==8 && CountId(p,721844)==1 && CountId(p,721843)==0,"Desert Peach accepts delivery and gives note");use(p,721844);talk(p,4633,1);Check(stage(p)==10 && p.Entity.Money==money+6600,"Desert note report pays full silver once");journal(p);
                talk(p,900071727,141);talk(p,900071727,141);Check(stage(p)==11 && CountId(p,728242)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Desert final bound rewards paid once");journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);use(p,728242);Check(stage(p)==11 && CountId(p,150178)==1 && p.Inventory.Objects.Single(i=>i.ID==150178).Bound,"Desert completed route and ring survive reload");
            }
        }finally{roll.SetValue(null,prior);}
    }
    static void VerifyHeroWingsStore(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeroWingsStore",true);World(runtime,1015);Call(store,null,"Initialize");var roll=store.GetField("Roll",Hidden);var clock=store.GetField("Clock",Hidden);object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> pay=(p,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"PayMeteor",p,id,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> gather=(p,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Gather",p,id,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_hero_wings WHERE uid=@uid",p.Entity.UID);
        try{roll.SetValue(null,(Func<int,int>)(n=>0));clock.SetValue(null,(Func<DateTime>)(()=>now));
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)90:(byte)140,life,15));p.BindWorldMap(1015);p.Entity.X=800;p.Entity.Y=675;ulong money=p.Entity.Money;
                Check(step(p,0) && !step(p,0) && !step(p,2) && step(p,1) && step(p,2) && step(p,3) && !step(p,4),"Hero ordered referrals stop at Meteor payment: "+life);
                var meteor=NestedPack(p,1088001,2,life==1);meteor.Lock=1;Check(!pay(p,meteor.UID),"Hero locked Meteor rejected: "+life);meteor.Lock=0;
                if(life==0){Query("ALTER TABLE revora_hero_wings ADD CONSTRAINT hero_pay_fault CHECK(stage<>5) NOT VALID");bool failed=false;try{pay(p,meteor.UID);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_hero_wings DROP CONSTRAINT hero_pay_fault");}Check(failed && meteor.StackSize==2 && Scalar("SELECT stacksize FROM items WHERE entityid=@uid AND id=1088001",p.Entity.UID)==2 && stage(p)==4,"Hero failed progression rolls back Meteor debit");}
                Check(pay(p,meteor.UID) && !pay(p,meteor.UID) && meteor.StackSize==1 && p.Entity.Money==money+300,"Hero one bound or unbound Meteor debited once: "+life);
                for(int i=0;i<10;i++)NestedPack(p,721906,1,true);Check(!step(p,5),"Hero unearned Blood rejected: "+life);foreach(var item in p.Inventory.Objects.Where(i=>i.ID==721906).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"Birdman");Check(!credit(p,full) && CountId(p,721906)==0,"Hero full inventory rejects Blood without tracking a lost reward");foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);Check(!credit(p,full),"Hero clearing inventory cannot reuse rejected corpse");}
                Check(!credit(p,kill(p,"HawKing")),"Hero Blood rejects wrong family: "+life);roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"Birdman");Check(!credit(p,miss),"Hero Blood misses at 50 percent boundary: "+life);roll.SetValue(null,(Func<int,int>)(n=>0));Check(!credit(p,miss),"Hero drop miss cannot reroll corpse: "+life);
                for(int i=0;i<10;i++){var b=kill(p,i%2==0?"Birdman":"BirdmanL88");if(!credit(p,b) || credit(p,b))throw new Exception("Hero Blood replay mismatch");}Check(CountId(p,721906)==10 && !credit(p,kill(p,"Birdman")) && step(p,5) && CountId(p,721912)==1,"Hero ten earned Blood cap and produce Ginseng Map: "+life);
                Check(credit(p,kill(p,"HawKing")) && !credit(p,kill(p,"HawKing")),"Hero Gallbladder is earned and capped: "+life);uint gall=p.Inventory.Objects.Single(i=>i.ID==721909).UID;
                if(life==0){ItemHandler.DropItem(new ItemUsage(true){UID=gall},p);Check(recover(p) && !recover(p) && CountId(p,721909)==0,"Hero lost raw Gallbladder clears entitlement without free replacement");Check(credit(p,kill(p,"HawKing")),"Hero fresh HawKing can replace lost raw Gallbladder");gall=p.Inventory.Objects.Single(i=>i.ID==721909).UID;}
                Check(!gather(p,gall),"Hero gathering requires frozen site: "+life);p.Entity.X=450;p.Entity.Y=720;
                uint map=p.Inventory.Objects.Single(i=>i.ID==721912).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)map);p=ReloadBall(p);Check(!gather(p,gall) && !recover(p),"Hero stored map cannot be used or copied: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)map);p=ReloadBall(p);
                Check(gather(p,gall) && !gather(p,gall) && CountId(p,721908)==1,"Hero Gallbladder creates one tracked Ginseng: "+life);uint root=p.Inventory.Objects.Single(i=>i.ID==721908).UID;ItemHandler.DropItem(new ItemUsage(true){UID=root},p);Check(recover(p) && !recover(p) && CountId(p,721908)==1,"Hero prepared Ginseng recovery is tracked: "+life);Check(step(p,6) && CountId(p,721912)==0 && CountId(p,721908)==0,"Hero Ginseng hand-in consumes map and root: "+life);
                Check(!credit(p,kill(p,"HawKing")),"Hero timed claws require exact level-93 family: "+life);
                if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"HawkL93");Check(!credit(p,full) && Field(Call(store,null,"Load",p),"FirstKill")==null,"Hero full inventory cannot start timed pair");foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);Check(!credit(p,full),"Hero old full-inventory Hawk cannot start a pair later");}
                var first=kill(p,"HawkL93");Check(credit(p,first) && !credit(p,first) && CountId(p,721910)==0,"Hero first kill starts timer without claw or replay: "+life);p=ReloadBall(p);now=now.AddSeconds(61);Check(credit(p,kill(p,"HawkL93")) && CountId(p,721910)==0,"Hero expired pair cannot complete after reload: "+life);
                if(life==0){object timer=Field(Call(store,null,"Load",p),"FirstKill");var failedBody=kill(p,"HawkL93");Query("ALTER TABLE items ADD CONSTRAINT hero_claw_fault CHECK(id<>721910) NOT VALID");bool failed=false;try{credit(p,failedBody);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT hero_claw_fault");}Check(failed && object.Equals(timer,Field(Call(store,null,"Load",p),"FirstKill")) && CountId(p,721910)==0 && Scalar("SELECT COUNT(*) FROM revora_hero_wings_materials WHERE uid=@uid AND type=721910",p.Entity.UID)==0,"Hero failed claw insert rolls back pair completion and tracking");Check(!credit(p,failedBody),"Hero failed corpse cannot be rerolled after database recovery");}
                now=now.AddSeconds(60);Check(credit(p,kill(p,"HawkL93")) && CountId(p,721910)==1,"Hero pair accepts inclusive 60-second boundary: "+life);
                for(int i=0;i<4;i++){Check(credit(p,kill(p,"HawkL93")) && CountId(p,721910)==i+1,"Hero each claw requires a fresh first kill: "+i+" life "+life);now=now.AddSeconds(1);if(!credit(p,kill(p,"HawkL93")))throw new Exception("Hero pair incomplete");}
                if(life==0){uint claw=p.Inventory.Objects.First(i=>i.ID==721910).UID;ItemHandler.DropItem(new ItemUsage(true){UID=claw},p);Check(recover(p) && !recover(p) && CountId(p,721910)==4 && !step(p,7),"Hero lost raw Claw requires a new pair");Check(credit(p,kill(p,"HawkL93")) && CountId(p,721910)==4 && credit(p,kill(p,"HawkL93")) && CountId(p,721910)==5,"Hero replacement claw still costs two distinct kills");}
                Check(CountId(p,721910)==5 && !credit(p,kill(p,"HawkL93")) && step(p,7) && CountId(p,721911)==1,"Hero five claws cap and create Pill: "+life);
                uint pill=p.Inventory.Objects.Single(i=>i.ID==721911).UID;ItemHandler.DropItem(new ItemUsage(true){UID=pill},p);Check(recover(p) && !recover(p) && step(p,8) && CountId(p,721911)==0 && step(p,9),"Hero recovered Pill delivers once and referrals finish: "+life);
                Func<bool> report=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Report",p,(Func<bool>)(()=>true));};Func<bool> claim=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Claim",p,(Func<bool>)(()=>true));};byte level=p.Entity.Level;ulong xp=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall;
                Check(report() && !report() && p.Entity.Money==money+6800 && stage(p)==11,"Hero report pays 6800 total silver once: "+life);ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);Check(gained==xp+award,"Hero report grants capped EXP ball: "+life);p.BindWorldMap(1002);
                if(life==0){Query("ALTER TABLE items ADD CONSTRAINT hero_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{claim();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT hero_final_fault");}Check(failed && stage(p)==11 && CountId(p,728247)==0 && CountId(p,723911)==0,"Hero failed final soul insert rolls back entire reward");}
                int claims=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(claim())System.Threading.Interlocked.Increment(ref claims);});Check(claims==1 && !claim(),"Hero concurrent final claims pay once: "+life);uint soul=(uint)Scalar("SELECT soul FROM revora_hero_wings WHERE uid=@uid",p.Entity.UID);EntityTable.SaveEntity(p);p=ReloadBall(p);Check(stage(p)==12 && new uint[]{728247,723911,soul}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound),"Hero bound final rewards persist: "+life);
            }
        }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
    }
    static void VerifyHeroWingsQuest(string runtime,Map town,GameState continuing=null,Action<GameState> completed=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeroWingsQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeroWingsStore",true);World(runtime,1015);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var roll=store.GetField("Roll",Hidden);var clock=store.GetField("Clock",Hidden);object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_hero_wings WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==900071727?(ushort)1002:(ushort)1015);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(npc==4701?actor.X:actor.X-2);p.Entity.Y=(ushort)(npc==4701?actor.Y+2:actor.Y);p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,id)=>ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==id),p);
        Action<GameState,string> kill=(p,name)=>{p.BindWorldMap(1015);var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);int count=p.Inventory.Count;object timer=Field(Call(store,null,"Load",p),"FirstKill");Call(quest,null,"OnDeath",p,b);if(p.Inventory.Count!=count || !object.Equals(timer,Field(Call(store,null,"Load",p),"FirstKill")))throw new Exception("Hero production death hook did not consume corpse before replay");};
        Action<GameState> journal=p=>{int st=stage(p);var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1808 && (uint)x.UID<=1818).ToArray();Check(own.Length==11 && own.All(x=>x.Status==(st>(int)x.UID-1807?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-1807?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Hero ordinary journal stage "+st);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));var low=ReloadBall(WelcomePlayer(town,89,0,15));talk(low,900071727,151);Check(stage(low)==0,"Hero ordinary underlevel acceptance denied");
            for(byte life=0;life<3;life++){int site=life;roll.SetValue(null,(Func<int,int>)(n=>n==5?site:0));
                var p=life==1 && continuing!=null?continuing:ReloadBall(WelcomePlayer(town,life==0?(byte)90:(byte)140,life,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=151},p);Check(stage(p)==0,"Hero remote acceptance denied");
                for(uint id=1808;id<=1818;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Hero forged journal start denied: "+id);}
                talk(p,900071727,151);journal(p);talk(p,4700,1);talk(p,4701,1);talk(p,4700,1);
                if(life==1 && continuing!=null){int meteors=CountId(p,1088001);Check(stage(p)==4 && meteors==3,"Hero continuing character brings three earned Meteors");talk(p,4701,1);talk(p,4701,1);Check(stage(p)==5 && CountId(p,1088001)==meteors-1,"Hero consumes exactly one earned Meteor despite repeat dialogue");}
                else{talk(p,4701,1);Check(stage(p)==4,"Hero doctor requires actual Meteor");var meteor=NestedPack(p,1088001,2,life==1);talk(p,4701,1);talk(p,4701,1);Check(stage(p)==5 && meteor.StackSize==1,"Hero ordinary Meteor payment once");}journal(p);
                for(int i=0;i<10;i++)kill(p,i%2==0?"Birdman":"BirdmanL88");Check(CountId(p,721906)==10,"Hero ordinary deaths track Blood without replay");talk(p,4701,1);Check(stage(p)==6 && CountId(p,721912)==1,"Hero doctor awards Ginseng Map");use(p,721912);kill(p,"HawKing");Check(CountId(p,721909)==1,"Hero actual HawKing death grants Gallbladder");use(p,721909);Check(CountId(p,721909)==1 && CountId(p,721908)==0,"Hero wrong-site item use preserves ingredient");
                p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);var points=(ushort[,])store.GetField("Sites",Hidden).GetValue(null);p.Entity.X=points[site,0];p.Entity.Y=points[site,1];use(p,721909);Check(CountId(p,721908)==1 && CountId(p,721909)==0,"Hero saved Ginseng site guides actual item gathering");talk(p,4701,1);Check(stage(p)==7,"Hero doctor accepts prepared Ginseng");journal(p);
                kill(p,"HawkL93");now=now.AddSeconds(61);p=ReloadBall(p);p.LoadItemStats();kill(p,"HawkL93");Check(CountId(p,721910)==0,"Hero ordinary pair expires across reconnect");now=now.AddSeconds(1);kill(p,"HawkL93");for(int i=0;i<4;i++){kill(p,"HawkL93");now=now.AddSeconds(1);kill(p,"HawkL93");}Check(CountId(p,721910)==5,"Hero ordinary timed pairs yield exactly five claws");talk(p,4701,1);Check(stage(p)==8 && CountId(p,721911)==1,"Hero doctor produces Peel Pill");journal(p);
                talk(p,4700,1);talk(p,4701,1);talk(p,4700,1);talk(p,4700,1);Check(stage(p)==11 && p.Entity.Money==money+6800,"Hero final referrals pay once");talk(p,900071727,151);talk(p,900071727,151);Check(stage(p)==12 && CountId(p,728247)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Hero ordinary final bound reward once");journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);use(p,728247);Check(stage(p)==12 && CountId(p,118089)==1 && p.Inventory.Objects.Single(i=>i.ID==118089).Bound,"Hero full route and Super headgear persist");if(completed!=null)completed(p);
            }
        }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
    }
    static void VerifyHeroWingsPack(Map town){
        VerifySingleEquipmentPack(town,728247,118089,"Hero Wings");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};
        foreach(byte job in jobs){
            uint id=job==15?118089u:job==25?111089u:job==45?113069u:job==55?112089u:job==65?143089u:job==75?144089u:job==85?148089u:job==165?170089u:114089u;var p=ReloadBall(WelcomePlayer(town,140,0,job));var source=NestedPack(p,728247,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728247)==0 && CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound,"Hero Wings ordinary pack picks catalog class Super headgear: "+job);
            p=ReloadBall(p);Check(CountId(p,id)==1 && FixedUses(p)==1,"Hero Wings headgear and claim persist: "+job);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728247,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728247)==1 && FixedUses(unsupported)==0,"Hero Wings unknown class keeps pack unopened");
    }
    static void VerifyDesertBloodPack(Map town){
        VerifySingleEquipmentPack(town,728242,150178,"Desert Blood");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};
        foreach(byte job in jobs){
            uint id=job==135 || job==145?152168u:150178u;var p=ReloadBall(WelcomePlayer(town,140,0,job));var source=NestedPack(p,728242,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728242)==0 && CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound,"Desert Blood ordinary pack picks catalog class ring/bracelet: "+job);
            p=ReloadBall(p);Check(CountId(p,id)==1 && FixedUses(p)==1,"Desert Blood ring and claim persist: "+job);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728242,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728242)==1 && FixedUses(unsupported)==0,"Desert Blood unknown class keeps pack unopened");
    }
    static void VerifyFamilyFeudStore(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);Call(store,null,"Initialize");
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,20,0,0,DateTimeKind.Utc);
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,bool> renew=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"RenewDisguise",p,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var low=ReloadBall(WelcomePlayer(town,94,0,15));Check(!(bool)Call(store,null,"Eligible",low),"Family Feud rejects level 94");
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)95:(byte)140,life,15));ulong money=p.Entity.Money;
                Check(step(p,0) && !step(p,0) && !step(p,2) && stage(p)==1,"Family Feud acceptance ordered and unique: "+life);
                uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");Check(soul>=800000 && soul<900000 && step(p,1) && !step(p,1),"Family Feud freezes soul and pays referral once: "+life);
                for(int action=2;action<=6;action++){
                    uint type=710560u+(uint)action;var token=NestedPack(p,type,1,true);
                    Check(!step(p,action),"Family Feud untracked token rejected: "+life+"/"+action);
                    // Store fixture only: encounter earning is a separate, pending integration.
                    Query("INSERT INTO revora_family_feud_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)token.UID,"uid",(long)p.Entity.UID,"life",(int)life,"type",(int)type);
                    if(action==6 && life==0){
                        Query("ALTER TABLE items ADD CONSTRAINT family_token_fault CHECK(id<>710567) NOT VALID");bool failed=false;
                        try{step(p,action);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT family_token_fault");}
                        Check(failed && stage(p)==6 && CountId(p,710566)==1 && Scalar("SELECT state FROM revora_family_feud_materials WHERE item_uid=@uid",token.UID)==0 && p.Entity.Money==money+500,"Family Feud failed Golden Token insert rolls back token, stage and silver");
                    }
                    Check(step(p,action) && !step(p,action) && CountId(p,type)==0 && stage(p)==action+1,"Family Feud tracked hand-in commits once: "+life+"/"+action);
                    p=ReloadBall(p);Check(stage(p)==action+1 && (uint)Field(Call(store,null,"Load",p),"Soul")==soul,"Family Feud stage and frozen soul survive reload: "+life+"/"+action);
                }
                Check(p.Entity.Money==money+600 && CountId(p,710567)==1 && p.Inventory.Objects.Single(x=>x.ID==710567).Bound,"Family Feud six payouts and bound Golden Token persist: "+life);
                var until=(DateTime)Field(Call(store,null,"Load",p),"DisguiseUntil");Check(until==now.AddSeconds(90) && !renew(p) && !step(p,7),"Family Feud saved deadline cannot be extended or bypass chamber: "+life);
                now=until;Check(!renew(p),"Family Feud exact deadline does not renew active attempt: "+life);now=now.AddTicks(10);
                Check(renew(p) && !renew(p),"Family Feud expired attempt renews once: "+life);p=ReloadBall(p);
                Check((DateTime)Field(Call(store,null,"Load",p),"DisguiseUntil")==now.AddSeconds(90) && p.Entity.Money==money+600,"Family Feud renewed UTC deadline survives reload without payment: "+life);
            }
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyFamilyFeudRewards(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);Call(store,null,"Initialize");World(runtime,1015);
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)95:(byte)140,life,15));ulong money=p.Entity.Money;
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
            Check(!act(p,"Claim"),"Family Feud cannot claim before earning: "+life);p.BindWorldMap(1015);
            Check(!act(p,"Report"),"Family Feud cannot report before maze completion: "+life);
            // Isolate reward transactions: maze completion is not implemented by this fixture.
            Query("UPDATE revora_family_feud SET stage=8 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(life==0){Query("ALTER TABLE revora_family_feud ADD CONSTRAINT family_report_fault CHECK(stage<>9) NOT VALID");bool failed=false;ulong beforeXp=p.Entity.Experience;byte beforeLevel=p.Entity.Level;
                try{act(p,"Report");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_family_feud DROP CONSTRAINT family_report_fault");}
                p=ReloadBall(p);Check(failed && stage(p)==8 && p.Entity.Money==money && p.Entity.Experience==beforeXp && p.Entity.Level==beforeLevel,"Family Feud report failure rolls back silver, experience and stage");
            }
            byte reportLevel=p.Entity.Level;ulong reportXp=p.Entity.Experience,award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
            Check(act(p,"Report") && !act(p,"Report") && p.Entity.Money==money+6000 && stage(p)==9,"Family Feud report pays once: "+life);
            ulong gained=p.Entity.Experience;for(byte level=reportLevel;level<p.Entity.Level;level++)gained+=DataHolder.LevelExperience(level);Check(gained==reportXp+award,"Family Feud report grants the capped EXP amount: "+life);
            p=ReloadBall(p);Check(stage(p)==9 && p.Entity.Money==money+6000,"Family Feud report persists: "+life);
            Check(!act(p,"Claim"),"Family Feud final claim requires Twin City: "+life);p.BindWorldMap(1002);
            if(life==0){
                while(p.Inventory.Count<38)Pack(p,1088001,1);Check(!act(p,"Claim") && stage(p)==9 && CountId(p,728248)==0,"Family Feud final reward requires three free slots");
                foreach(var item in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT family_reward_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;
                try{act(p,"Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT family_reward_fault");}
                Check(failed && stage(p)==9 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728248,723911)",p.Entity.UID)==0,"Family Feud soul insert failure rolls back the entire final reward");
            }
            Check(act(p,"Claim") && !act(p,"Claim") && stage(p)==10 && CountId(p,728248)==1 && CountId(p,723911)==1 && CountId(p,soul)==1,"Family Feud final rewards claimed exactly once: "+life);
            uint[] ids=p.Inventory.Objects.Where(x=>x.ID==728248 || x.ID==723911 || x.ID==soul).Select(x=>x.UID).OrderBy(x=>x).ToArray();p=ReloadBall(p);
            Check(ids.Length==3 && p.Inventory.Objects.Where(x=>ids.Contains(x.UID)).All(x=>x.Bound) && ids.All(x=>p.Inventory.ContainsUID(x)) && !act(p,"Claim"),"Family Feud exact bound final rewards persist without replay: "+life);
        }
    }
    static void VerifyDesertExpeditionPopulation(string runtime,Map town){
        var map=World(runtime,1001);World(runtime,1000);
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionQuest",true);
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);
        Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("HuntRoll",Hidden);
        object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,28,4,0,0,DateTimeKind.Utc);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==4702?(ushort)1000:(ushort)1002);var n=p.Map.Npcs[npc];p.Entity.X=(ushort)(n.X-2);p.Entity.Y=n.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        Check(Scalar("SELECT CASE WHEN current_schema()='revora_potency_validation' THEN 1 ELSE 0 END",0)==1,"Expedition population restricted to disposable schema");
        Query("CREATE TABLE expedition_spawn_fixture AS SELECT * FROM monsterspawns WHERE mapid=1001");Entity[] spawned=new Entity[0];
        try{
            clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));
            Query("INSERT INTO monsterspawns SELECT * FROM public.monsterspawns WHERE mapid=1001 AND npctype IN(57,82) ON CONFLICT DO NOTHING");
            Query("DELETE FROM monsterspawns WHERE mapid=1001 AND npctype NOT IN(57,82)");
            int expected=(int)Scalar("SELECT COALESCE(SUM(max_per_gen),0) FROM monsterspawns WHERE mapid=1001",0);
            Check(expected>0,"Expedition configured bull population exists");map.LoadMonsters();
            var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);
            spawned=map.Entities.Values.Where(b=>b.MonsterInfo!=null && (b.MonsterInfo.ID==57 || b.MonsterInfo.ID==82)).ToArray();
            System.Console.WriteLine("EXPEDITION-POP|expected="+expected+"|actual="+spawned.Length);
            Check(spawned.Length==expected && spawned.All(b=>!b.Dead && b.X>=b.MonsterInfo.BoundX && b.X<b.MonsterInfo.BoundX+b.MonsterInfo.BoundCX && b.Y>=b.MonsterInfo.BoundY && b.Y<b.MonsterInfo.BoundY+b.MonsterInfo.BoundCY),"Expedition loader places all configured bulls inside bounds");
            foreach(uint family in new uint[]{57,82}){
                var body=spawned.First(b=>b.MonsterInfo.ID==family);var p=ReloadBall(WelcomePlayer(town,105,0,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();
                talk(p,900071727,181);talk(p,4702,1);p.BindWorldMap(1001);p.Entity.X=body.X;p.Entity.Y=body.Y;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721920),p);
                Check(Field(Call(store,null,"Load",p),"PillUntil")!=null,"Expedition ordinary pill activated for loaded family "+family);
                for(int death=1;death<=2;death++){
                    now=now.AddSeconds(1);p.Entity.X=body.X;p.Entity.Y=body.Y;body.IsDropped=true;uint damage=body.Hitpoints;
                    Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
                    Check(body.Dead && CountId(p,710259)==death,"Expedition loaded bull death earns tracked horn "+family+"/"+death);
                    body.DeathStamp=new Time32(1);Call(typeof(Map),null,"_timerCallBack",map,Time32.Now.GetHashCode());
                    Check(!body.Dead && body.Hitpoints==body.MonsterInfo.Hitpoints,"Expedition actual respawner restores bull "+family+"/"+death);
                }
                p=ReloadBall(p);Check(CountId(p,710259)==2,"Expedition loaded bull rewards persist "+family);
            }
        }finally{
            var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);
            foreach(var b in spawned)map.RemoveEntity(b);clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);
            Query("DELETE FROM monsterspawns WHERE mapid=1001;INSERT INTO monsterspawns SELECT * FROM expedition_spawn_fixture;DROP TABLE expedition_spawn_fixture");
        }
    }
    static void VerifyDesertExpeditionQuest(string runtime,Map town,GameState continuing=null,Action<GameState> completed=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionEncounters",true);
        World(runtime,1000);World(runtime,1001);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("HuntRoll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,28,1,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=player=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_desert_expedition WHERE uid=@uid",player.Entity.UID);
        Action<GameState,uint,byte> talk=(player,npc,option)=>{player.BindWorldMap(npc==900071727?(ushort)1002:npc==4702?(ushort)1000:(ushort)1001);var n=player.Map.Npcs[npc];player.Entity.X=(ushort)(n.X-2);player.Entity.Y=n.Y;player.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},player);};
        Action<GameState,Entity> hit=(player,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(player.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=player.Entity.UID,Attacked=b.UID},ref damage,null);};
        Action<GameState> kill=player=>{var b=HuntMonster(player,"BullMonster");b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;hit(player,b);};
        Action<GameState,uint> use=(player,type)=>ItemHandler.UseItem(player.Inventory.Objects.First(i=>i.ID==type),player);
        Action<GameState> journal=player=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(player,rows);int st=stage(player);var own=rows.Where(x=>(uint)x.UID>=1835 && (uint)x.UID<=1839).ToArray();Check(own.Length==5 && own.All(x=>x.Status==(st>(int)x.UID-1834?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-1834?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Desert Expedition ordinary journal matches stage "+st);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));
            var low=ReloadBall(WelcomePlayer(town,104,0,15));talk(low,900071727,181);Check(stage(low)==0,"Desert Expedition ordinary underlevel acceptance denied");
            for(byte life=0;life<3;life++){
                var p=life==1 && continuing!=null?continuing:ReloadBall(WelcomePlayer(town,life==0?(byte)105:(byte)140,life,life==2?(byte)135:(byte)15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=181},p);Check(stage(p)==0,"Desert Expedition remote acceptance denied "+life);
                for(uint id=1835;id<=1839;id++){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Desert Expedition forged journal cannot grant progress "+id);}
                talk(p,900071727,181);journal(p);talk(p,4702,1);Check(stage(p)==2 && CountId(p,721920)==1,"Desert Expedition Mo Mo grants tracked pill "+life);
                p.BindWorldMap(1001);p.Entity.X=173;p.Entity.Y=329;p.Entity.Hitpoints=1000;Query("UPDATE entities SET hitpoints=1000 WHERE uid=@uid","uid",(long)p.Entity.UID);DateTime activated=now;use(p,721920);
                var striking=HuntMonster(p,"BullMonster");striking.MinAttack=1;striking.MaxAttack=2;p.Attackable=true;
                new Revora.Game.Attacking.Handle(null,striking,p.Entity);Check(p.Entity.Hitpoints>0 && p.Entity.Hitpoints<1000,"Desert Expedition active pill permits real incoming monster attacks "+life);
                p.Entity.Hitpoints=1000;Query("UPDATE entities SET hitpoints=1000 WHERE uid=@uid","uid",(long)p.Entity.UID);
                for(int i=0;i<10;i++){now=now.AddSeconds(2);kill(p);}
                Check(CountId(p,710259)==10 && p.Entity.Hitpoints==810 && (int)Field(Call(store,null,"Load",p),"PillPaid")==2,"Desert Expedition actual kill hook pays due HP and earns ten horns "+life);
                talk(p,4702,1);Check(stage(p)==2 && CountId(p,710259)==10,"Desert Expedition early hand-in preserves unpaid fever cost "+life);
                now=activated.AddSeconds(35);p=ReloadBall(p);p.Quests=new Quests(p);p.Quests.Load();Call(quest,null,"OnLogin",p);Check(p.Entity.Hitpoints==729 && (bool)Field(p.Entity,"HealthSynchronizationRequired"),"Desert Expedition login pays missed tick before hunting "+life);
                now=activated.AddSeconds(60);Call(quest,null,"Tick");Check(p.Entity.Hitpoints==533 && !(bool)Field(p.Entity,"HealthSynchronizationRequired"),"Desert Expedition periodic tick pays full fever cost "+life);
                talk(p,4702,1);Check(stage(p)==3 && CountId(p,710259)==0 && CountId(p,721920)==0 && CountId(p,721921)==1,"Desert Expedition earned horns trade for mirror "+life);journal(p);
                ushort[,] sites={{172,399},{102,292},{109,369}};
                for(int site=0;site<3;site++){
                    p.BindWorldMap(1001);p.Entity.X=sites[site,0];p.Entity.Y=sites[site,1];now=now.AddSeconds(2);use(p,721921);
                    var body=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918350 && !b.Dead);p.Entity.X=(ushort)(sites[site,0]-3);
                    for(int n=0;n<20 && Kernel.GetDistance(body.X,body.Y,sites[site,0],sites[site,1])>1;n++){body.MonsterInfo.LastMove=new Time32(1);Call(fights,null,"DriveOwned",p,body,Time32.Now);}
                    hit(p,body);Call(quest,null,"Tick");Check(CountId(p,710261)==site+1,"Desert Expedition mirror/lure/combat earns ghost "+site+" life "+life);
                }
                talk(p,4702,1);Check(stage(p)==4 && CountId(p,710261)==0 && CountId(p,721921)==0,"Desert Expedition earned ghosts unlock Bull Master "+life);journal(p);
                p.BindWorldMap(1001);p.Entity.X=185;p.Entity.Y=359;now=now.AddSeconds(2);Call(quest,null,"Tick");var boss=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918351 && !b.Dead);hit(p,boss);Call(quest,null,"Tick");Check(CountId(p,710260)==1,"Desert Expedition automatic owned boss earns heart "+life);
                talk(p,4702,1);Check(stage(p)==5 && CountId(p,710260)==0 && p.Entity.Money==money+6300,"Desert Expedition ordinary report pays full silver total once "+life);journal(p);
                uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");talk(p,900071727,181);talk(p,900071727,181);Check(stage(p)==6 && CountId(p,728250)==1 && CountId(p,723911)==1 && CountId(p,soul)==1 && p.Inventory.Objects.Where(i=>i.ID==728250 || i.ID==723911 || i.ID==soul).All(i=>i.Bound),"Desert Expedition final bound bundle is awarded once "+life);journal(p);
                use(p,728250);p=ReloadBall(p);Check(stage(p)==6 && CountId(p,life==2?152229u:150219u)==1 && CountId(p,723911)==1 && CountId(p,soul)==1,"Desert Expedition ordinary class equipment and rewards persist "+life);now=now.AddMinutes(1);if(completed!=null)completed(p);
            }
        }finally{now=now.AddHours(1);Call(quest,null,"Tick");clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyDesertExpeditionHealthLease(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");Call(Healing,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,27,5,0,0,DateTimeKind.Utc);
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,115,0,15));p.BindWorldMap(1001);p.Entity.X=173;p.Entity.Y=329;
            lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));Call(store,null,"ActivatePill",p,p.Inventory.Objects.Single(i=>i.ID==721920).UID,(Func<bool>)(()=>true));}
            Check((bool)Field(p.Entity,"HealthSynchronizationRequired"),"Desert Expedition activation enables scoped health serialization");
            p=ReloadBall(p);Call(store,null,"Load",p);Check((bool)Field(p.Entity,"HealthSynchronizationRequired"),"Desert Expedition loading unpaid pill restores health serialization");
            p.Entity.Hitpoints=1000;p.Entity.MaxHitpoints=5000;Query("UPDATE entities SET hitpoints=1000 WHERE uid=@uid","uid",(long)p.Entity.UID);
            var opponent=ReloadBall(WelcomePlayer(town,115,0,15));opponent.BindWorldMap(1001);opponent.Entity.X=174;opponent.Entity.Y=329;var bull=opponent.Entity;var potion=NestedPack(p,1000000,1,true);var owner=p;
            Func<uint> damage=()=>{uint amount=50;Revora.Game.Attacking.Handle.ReceiveAttack(bull,owner.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=bull.UID,Attacked=owner.Entity.UID},ref amount,null);return amount;};
            int calls=0;now=now.AddSeconds(10);uint busyDamage=99,busyHeal=99;bool kept=false;var healthType=typeof(Entity).Assembly.GetType("Revora.Game.Features.HealthMutationLease",true);
            Func<bool> context=()=>{if(++calls==2){busyDamage=Task.Run(damage).Result;Task.Run(()=>Call(Healing,null,"TryUse",potion,owner)).Wait();busyHeal=Task.Run(()=>(uint)Call(healthType,null,"Heal",owner.Entity,50u)).Result;kept=owner.Inventory.ContainsUID(potion.UID);}return true;};
            lock(p.ItemSyncRoot)Check((int)Call(store,null,"SettlePill",p,context)==1,"Desert Expedition settlement completes with competing combat/medicine attempts");
            Check(busyDamage==0 && kept && p.Entity.Hitpoints==900 && HealUses(p)==0,"Desert Expedition contested health mutation changes no HP and consumes no medicine");
            uint applied=damage();Check(applied>0 && p.Entity.Hitpoints==900-applied,"Desert Expedition combat after settlement uses committed HP");
            Check(busyHeal==0,"Desert Expedition area-heal recipient rejects concurrent settlement mutation");
            uint oldHp=p.Entity.Hitpoints;Check((uint)Call(healthType,null,"Heal",p.Entity,50u)==50 && p.Entity.Hitpoints==oldHp+50,"Desert Expedition area-heal recipient applies after settlement without lost HP");
            now=now.AddSeconds(10);lock(p.ItemSyncRoot)Call(store,null,"SettlePill",p,(Func<bool>)(()=>true));uint before=p.Entity.Hitpoints;
            Call(Healing,null,"TryUse",potion,p);Check(!p.Inventory.ContainsUID(potion.UID) && p.Entity.Hitpoints==Math.Min(p.Entity.MaxHitpoints,before+ConquerItemInformation.BaseInformations[1000000].ItemHP) && HealUses(p)==1,"Desert Expedition medicine after settlement preserves debit and commits healing");
            now=now.AddMinutes(2);lock(p.ItemSyncRoot)Call(store,null,"SettlePill",p,(Func<bool>)(()=>true));Check(!(bool)Field(p.Entity,"HealthSynchronizationRequired"),"Desert Expedition fully paid pill releases scoped health serialization");
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyDesertExpeditionPayment(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime start=new DateTime(2026,9,27,4,0,0,DateTimeKind.Utc),now=start;
        Func<GameState,int> settle=player=>{lock(player.ItemSyncRoot)return (int)Call(store,null,"SettlePill",player,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            for(byte life=0;life<3;life++){
                now=start;var p=ReloadBall(WelcomePlayer(town,115,life,15));p.BindWorldMap(1001);p.Entity.X=173;p.Entity.Y=329;
                lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));Call(store,null,"ActivatePill",p,p.Inventory.Objects.Single(i=>i.ID==721920).UID,(Func<bool>)(()=>true));}
                p.Entity.Hitpoints=1000;Query("UPDATE entities SET hitpoints=1000 WHERE uid=@uid","uid",(long)p.Entity.UID);
                Check(settle(p)==0 && p.Entity.Hitpoints==1000,"Desert Expedition no HP payment before first tick: "+life);
                now=start.AddSeconds(10);Check(settle(p)==1 && p.Entity.Hitpoints==900 && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",p.Entity.UID)==900,"Desert Expedition first tick commits actual HP debit: "+life);
                var owner=p;now=start.AddSeconds(30);int ticks=0;Parallel.For(0,4,j=>System.Threading.Interlocked.Add(ref ticks,settle(owner)));
                Check(ticks==2 && p.Entity.Hitpoints==729 && (int)Field(Call(store,null,"Load",p),"PillPaid")==3 && Scalar("SELECT count(*) FROM revora_desert_expedition_pill_payments WHERE uid=@uid",p.Entity.UID)==2,"Desert Expedition concurrent catch-up pays due ticks once: "+life);
                now=start.AddSeconds(40);Query("ALTER TABLE revora_desert_expedition_pill_payments ADD CONSTRAINT expedition_payment_fault CHECK(paid_after<>4) NOT VALID");bool failed=false;
                try{settle(p);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_expedition_pill_payments DROP CONSTRAINT expedition_payment_fault");}
                Check(failed && p.Entity.Hitpoints==729 && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",p.Entity.UID)==729 && (int)Field(Call(store,null,"Load",p),"PillPaid")==3,"Desert Expedition failed receipt rolls back HP and paid ticks: "+life);
                p=ReloadBall(p);Check(p.Entity.Hitpoints==729 && settle(p)==1 && p.Entity.Hitpoints==657,"Desert Expedition failed payment retries after reconnect: "+life);
                now=start.AddMinutes(2);Check(settle(p)==2 && p.Entity.Hitpoints==533 && settle(p)==0,"Desert Expedition expiry catches up remaining HP cost exactly once: "+life);
                p=ReloadBall(p);Check(p.Entity.Hitpoints==533 && (int)Field(Call(store,null,"Load",p),"PillPaid")==6 && Scalar("SELECT sum(paid_after-paid_before) FROM revora_desert_expedition_pill_payments WHERE uid=@uid",p.Entity.UID)==6,"Desert Expedition full HP cost and six paid ticks persist: "+life);
            }
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyDesertExpeditionHunt(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("HuntRoll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime start=new DateTime(2026,9,27,3,0,0,DateTimeKind.Utc),now=start;int sample=0;
        Func<GameState,Entity,bool> credit=(player,b)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"Hunt",player,b,(Func<bool>)(()=>true));};
        Func<GameState,string,Entity> kill=(player,name)=>{var b=HuntMonster(player,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(player.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=player.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>sample));
            var p=ReloadBall(WelcomePlayer(town,115,0,15));p.LoadItemStats();p.BindWorldMap(1001);p.Entity.X=173;p.Entity.Y=329;
            lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
            uint pill=p.Inventory.Objects.Single(i=>i.ID==721920).UID;
            Check(!credit(p,kill(p,"BullMonster")),"Desert Expedition inactive pill cannot earn horns");
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"ActivatePill",p,pill,(Func<bool>)(()=>true)),"Desert Expedition hunting fixture activates tracked pill");
            Check(!credit(p,kill(p,"TombBat")),"Desert Expedition wrong monster family cannot earn horns");
            sample=50;var miss=kill(p,"BullMonster");Check(!credit(p,miss),"Desert Expedition horn chance excludes fifty-percent boundary");sample=49;Check(!credit(p,miss),"Desert Expedition losing death cannot reroll");
            var body=kill(p,"BullMonsterL113");var owner=p;int paid=0;Parallel.For(0,4,j=>{if(credit(owner,body))System.Threading.Interlocked.Increment(ref paid);});
            Check(paid==1 && CountId(p,710259)==1 && p.Inventory.Objects.Single(i=>i.ID==710259).Bound,"Desert Expedition concurrent qualifying kill grants one bound horn");
            now=start.AddSeconds(10);Check(!credit(p,kill(p,"BullMonster")),"Desert Expedition due unpaid HP tick blocks horn earning");
            // Isolate the horn gate: settlement is still pending, so paid ticks are seeded.
            Query("UPDATE revora_desert_expedition SET pill_paid=1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Check(credit(p,kill(p,"BullMonster")) && CountId(p,710259)==2,"Desert Expedition settled first tick permits next qualifying kill");
            p=ReloadBall(p);Check(CountId(p,710259)==2 && (int)Field(Call(store,null,"Load",p),"PillPaid")==1,"Desert Expedition earned horns and paid gate survive reconnect");
            while(p.Inventory.Count<40)Pack(p,1088001,1);var full=kill(p,"BullMonster");Check(!credit(p,full),"Desert Expedition full bag cannot receive horn");
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);Check(!credit(p,full),"Desert Expedition freeing bag cannot replay rejected corpse");
            Query("ALTER TABLE items ADD CONSTRAINT expedition_horn_fault CHECK(id<>710259) NOT VALID");bool failed=false;var fault=kill(p,"BullMonster");
            try{credit(p,fault);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT expedition_horn_fault");}
            Check(failed && CountId(p,710259)==2 && Scalar("SELECT count(*) FROM revora_desert_expedition_materials WHERE uid=@uid AND type=710259",p.Entity.UID)==2 && !credit(p,fault),"Desert Expedition failed horn insert leaves no entitlement and cannot replay death");
            now=start.AddSeconds(60);Query("UPDATE revora_desert_expedition SET pill_paid=6 WHERE uid=@uid","uid",(long)p.Entity.UID);Check(!credit(p,kill(p,"BullMonster")),"Desert Expedition expiry rejects horn even with all ticks paid");
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"ActivatePill",p,pill,(Func<bool>)(()=>true)),"Desert Expedition settled expiry starts another hunting minute");
            while(CountId(p,710259)<10)if(!credit(p,kill(p,"BullMonster")))throw new Exception("Desert Expedition horn cap fixture failed");
            Check(!credit(p,kill(p,"BullMonsterL113")) && CountId(p,710259)==10,"Desert Expedition horn awards cap at ten tracked entitlements");
            p=ReloadBall(p);Check(CountId(p,710259)==10 && p.Inventory.Objects.Where(i=>i.ID==710259).All(i=>i.Bound),"Desert Expedition ten earned bound horns persist");
        }finally{clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyDesertExpeditionEncounters(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionEncounters",true);
        Call(store,null,"Initialize");World(runtime,1001);var clock=store.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,27,2,0,0,DateTimeKind.Utc);
        Func<GameState,Entity> spawn=player=>(Entity)Call(fights,null,"Start",player);
        Func<GameState,uint,int,bool> place=(player,mirror,site)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"PlaceTrap",player,mirror,site,(Func<bool>)(()=>true));};
        Action<GameState,Entity> hit=(player,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(player.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=player.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,115,0,15));p.LoadItemStats();p.BindWorldMap(1001);p.Entity.X=172;p.Entity.Y=399;
            var thief=ReloadBall(WelcomePlayer(town,115,0,15));thief.LoadItemStats();thief.BindWorldMap(1001);thief.Entity.X=171;thief.Entity.Y=399;
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Query("UPDATE revora_desert_expedition SET stage=3 WHERE uid=@uid","uid",(long)p.Entity.UID);
            var mirror=NestedPack(p,721921,1,true);Check(!place(p,mirror.UID,0) && spawn(p)==null,"Desert Expedition untracked mirror cannot place or summon");
            Query("INSERT INTO revora_desert_expedition_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,721921)","item",(long)mirror.UID,"uid",(long)p.Entity.UID);
            Check(!place(p,mirror.UID,1) && !place(p,mirror.UID,3),"Desert Expedition trap placement enforces site/range");
            ushort[,] sites={{172,399},{102,292},{109,369}};
            for(int site=0;site<3;site++){
                p.Entity.X=sites[site,0];p.Entity.Y=sites[site,1];
                Check(place(p,mirror.UID,site) && !place(p,mirror.UID,site),"Desert Expedition one fixed-deadline trap per owner: "+site);
                var body=spawn(p);Check(body!=null && body.Name=="MadBull" && Kernel.GetDistance(body.X,body.Y,p.Entity.X,p.Entity.Y)>1 && spawn(p)==null,"Desert Expedition owned Mad Bull starts outside trap: "+site);
                uint before=body.Hitpoints;hit(thief,body);Check(body.Hitpoints==before && !(bool)Call(fights,null,"TryDie",new Entity(EntityFlag.Monster,false),thief.Entity),"Desert Expedition strangers cannot damage owned bull: "+site);
                if(site==0){hit(p,body);Check(CountId(p,710261)==0 && Scalar("SELECT count(*) FROM revora_desert_expedition_encounter_kills WHERE uid=@uid AND NOT won",p.Entity.UID)==1,"Desert Expedition player standing on trap cannot capture distant bull");Call(fights,null,"Tick");body=spawn(p);Check(body!=null,"Desert Expedition missed lure can retry during same trap");}
                p.Entity.X=(ushort)(sites[site,0]-3);
                for(int n=0;n<20 && Kernel.GetDistance(body.X,body.Y,sites[site,0],sites[site,1])>1;n++){body.MonsterInfo.LastMove=new Time32(1);Call(fights,null,"DriveOwned",p,body,Time32.Now);}
                Check(Kernel.GetDistance(body.X,body.Y,sites[site,0],sites[site,1])<=1,"Desert Expedition actual monster movement follows owner into trap: "+site);
                if(site==0){
                    Query("ALTER TABLE items ADD CONSTRAINT expedition_ghost_fault CHECK(id<>710261) NOT VALID");try{hit(p,body);}finally{Query("ALTER TABLE items DROP CONSTRAINT expedition_ghost_fault");}
                    Check(Fenced(p) && Scalar("SELECT count(*) FROM revora_desert_expedition_encounter_kills WHERE uid=@uid AND won",p.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_desert_expedition_traps WHERE uid=@uid AND used",p.Entity.UID)==0 && CountId(p,710261)==0,"Desert Expedition failed ghost insertion rolls back capture, trap and material");
                    Call(fights,null,"Tick");p=ReloadBall(p);body=spawn(p);Check(body!=null,"Desert Expedition failed capture preserves active trap for reconnect retry");
                    for(int n=0;n<20 && Kernel.GetDistance(body.X,body.Y,sites[site,0],sites[site,1])>1;n++){body.MonsterInfo.LastMove=new Time32(1);Call(fights,null,"DriveOwned",p,body,Time32.Now);}
                }
                hit(p,body);Call(fights,null,"TryDie",body,p.Entity);
                Check(CountId(p,710261)==site+1 && p.Inventory.Objects.Where(i=>i.ID==710261).All(i=>i.Bound),"Desert Expedition trapped death grants one bound ghost: "+site);
                Call(fights,null,"Tick");Check(!p.Map.Entities.ContainsKey(body.UID) && !place(p,mirror.UID,site),"Desert Expedition captured trap/site cannot award again: "+site);
                p=ReloadBall(p);Check(CountId(p,710261)==site+1,"Desert Expedition earned ghosts survive reconnect: "+site);
            }
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Advance",p,3,(Func<bool>)(()=>true)) && CountId(p,710261)==0 && CountId(p,721921)==0,"Desert Expedition actual three ghosts unlock Bull Master");
            p.Entity.X=185;p.Entity.Y=359;var boss=spawn(p);Check(boss!=null && boss.Name=="BullMaster" && boss.MaxHitpoints==100000,"Desert Expedition owned Bull Master spawns at verified hunting point");
            while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,boss);Call(fights,null,"Tick");Check(CountId(p,710260)==0 && !p.Map.Entities.ContainsKey(boss.UID),"Desert Expedition full bag gives no partial heart and cleans defeated boss");
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
            boss=spawn(p);hit(p,boss);Call(fights,null,"TryDie",boss,p.Entity);Call(fights,null,"Tick");p=ReloadBall(p);
            Check(CountId(p,710260)==1 && p.Inventory.Objects.Single(i=>i.ID==710260).Bound && spawn(p)==null && Scalar("SELECT count(*) FROM revora_desert_expedition_encounter_kills WHERE uid=@uid AND kind=1 AND won",p.Entity.UID)==1,"Desert Expedition bound earned heart and unique receipt survive reload");
            lock(thief.ItemSyncRoot)Call(store,null,"Advance",thief,0,(Func<bool>)(()=>true));Query("UPDATE revora_desert_expedition SET stage=4 WHERE uid=@uid","uid",(long)thief.Entity.UID);thief.Entity.X=184;thief.Entity.Y=359;
            var timed=spawn(thief);now=now.AddMinutes(5);Call(fights,null,"Tick");Check(timed!=null && !thief.Map.Entities.ContainsKey(timed.UID),"Desert Expedition expired owned boss cleans up");
            var replaced=spawn(thief);thief=ReloadBall(thief);Call(fights,null,"Tick");Check(replaced!=null && !thief.Map.Entities.ContainsKey(replaced.UID),"Desert Expedition replaced session removes old encounter");
        }finally{now=now.AddHours(1);Call(fights,null,"Tick");clock.SetValue(null,oldClock);}
    }
    static void VerifyDesertExpeditionPill(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);var now=new DateTime(2026,9,26,20,0,0,DateTimeKind.Utc);var start=now;
        Func<GameState,uint,bool> activate=(player,item)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"ActivatePill",player,item,(Func<bool>)(()=>true));};
        Func<GameState,object> load=player=>Call(store,null,"Load",player);
        Func<DateTime,int,int> due=(time,paid)=>(int)Call(store,null,"PillDue",start.AddSeconds(60),time,paid);
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            Check(due(start,0)==0 && due(start.AddSeconds(10).AddTicks(-1),0)==0 && due(start.AddSeconds(10),0)==1,"Desert Expedition first debit is due exactly at ten seconds");
            Check(due(start.AddSeconds(59),0)==5 && due(start.AddSeconds(60),0)==6 && due(start.AddDays(20),0)==6,"Desert Expedition expiry includes sixth debit and caps offline debt");
            Check(due(start.AddSeconds(30),2)==1 && due(start.AddSeconds(60),6)==0,"Desert Expedition paid debits are never scheduled twice");
            foreach(var time in new[]{start.AddTicks(-1),start.AddSeconds(19)}){bool rejected=false;try{due(time,time<start?0:2);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Desert Expedition rejects clock rollback before activation or paid tick");}
            Check((uint)Call(store,null,"PillHealth",1000u,6)==533 && (uint)Call(store,null,"PillHealth",1000u,1)==900,"Desert Expedition debits compound against current HP with integer rounding");
            Check((uint)Call(store,null,"PillHealth",0u,6)==0 && (uint)Call(store,null,"PillHealth",1u,6)==1 && (uint)Call(store,null,"PillHealth",2u,6)==1,"Desert Expedition rounding neither revives nor kills at low HP");
            Check((uint)Call(store,null,"PillHealth",uint.MaxValue,1)==3865470566u,"Desert Expedition HP arithmetic handles full uint without overflow");
            for(byte life=0;life<3;life++){
                now=start;var p=ReloadBall(WelcomePlayer(town,life==0?(byte)105:(byte)140,life,15));
                lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
                uint pill=p.Inventory.Objects.Single(i=>i.ID==721920).UID;
                Check(!activate(p,pill),"Desert Expedition pill requires hunting map: "+life);p.BindWorldMap(1001);
                var forged=NestedPack(p,721920,1,true);Check(!activate(p,forged.UID),"Desert Expedition rejects untracked pill: "+life);
                Query("ALTER TABLE revora_desert_expedition ADD CONSTRAINT expedition_pill_fault CHECK(pill_until IS NULL) NOT VALID");bool failed=false;
                try{activate(p,pill);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_expedition DROP CONSTRAINT expedition_pill_fault");}
                Check(failed && Field(load(p),"PillUntil")==null && p.Inventory.ContainsUID(pill),"Desert Expedition activation failure preserves item and absent schedule: "+life);
                var owner=p;int accepted=0;Parallel.For(0,4,j=>{if(activate(owner,pill))System.Threading.Interlocked.Increment(ref accepted);});
                Check(accepted==1 && (DateTime)Field(load(p),"PillUntil")==start.AddSeconds(60) && (int)Field(load(p),"PillPaid")==0 && p.Inventory.ContainsUID(pill),"Desert Expedition concurrent activation starts one reusable sixty-second pill: "+life);
                now=start.AddSeconds(25);p=ReloadBall(p);Check(!activate(p,pill) && (DateTime)Field(load(p),"PillUntil")==start.AddSeconds(60),"Desert Expedition reconnect/reclick cannot extend active deadline: "+life);
                p.Inventory.Remove(pill,Enums.ItemUse.Remove,true);lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721920u,(Func<bool>)(()=>true)),"Desert Expedition existing ground pill cannot be duplicated: "+life);
                Query("DELETE FROM items WHERE uid=@uid","uid",(long)pill);lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721920u,(Func<bool>)(()=>true)),"Desert Expedition lost active pill can recover: "+life);
                pill=p.Inventory.Objects.Single(i=>i.ID==721920 && i.UID!=forged.UID).UID;
                Check((DateTime)Field(load(p),"PillUntil")==start.AddSeconds(60) && !activate(p,pill),"Desert Expedition recovery preserves original timer and debt: "+life);
                now=start.AddSeconds(61);Check(!activate(p,pill),"Desert Expedition expiry cannot erase unpaid HP debits: "+life);
                for(int n=0;n<10;n++){var horn=NestedPack(p,710259,1,true);Query("INSERT INTO revora_desert_expedition_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,710259)","item",(long)horn.UID,"uid",(long)p.Entity.UID,"life",(int)life);}
                lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Advance",p,2,(Func<bool>)(()=>true)) && CountId(p,710259)==10 && p.Inventory.ContainsUID(pill),"Desert Expedition horn hand-in cannot erase unpaid debits: "+life);
                // Simulate completed settlement only; no HP drain is implemented by this fixture.
                Query("UPDATE revora_desert_expedition SET pill_paid=6 WHERE uid=@uid","uid",(long)p.Entity.UID);
                Check(activate(p,pill) && (DateTime)Field(load(p),"PillUntil")==now.AddSeconds(60) && (int)Field(load(p),"PillPaid")==0,"Desert Expedition settled expired pill can start a fresh minute: "+life);
            }
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyDesertExpeditionRewards(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");World(runtime,1000);
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)105:(byte)140,life,15));ulong money=p.Entity.Money;
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
            Check(!act(p,"Claim"),"Desert Expedition cannot claim before earning: "+life);p.BindWorldMap(1000);
            Check(!act(p,"Report"),"Desert Expedition cannot report before heart capture: "+life);
            // Isolate reward transactions: heart capture is not implemented by this fixture.
            Query("UPDATE revora_desert_expedition SET stage=4 WHERE uid=@uid","uid",(long)p.Entity.UID);
            var heart=NestedPack(p,710260,1,true);Check(!act(p,"Report"),"Desert Expedition untracked heart cannot report: "+life);
            Query("INSERT INTO revora_desert_expedition_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,710260)","item",(long)heart.UID,"uid",(long)p.Entity.UID,"life",(int)life);
            if(life==0){Query("ALTER TABLE revora_desert_expedition ADD CONSTRAINT expedition_report_fault CHECK(stage<>5) NOT VALID");bool failed=false;ulong beforeXp=p.Entity.Experience;byte beforeLevel=p.Entity.Level;
                try{act(p,"Report");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_expedition DROP CONSTRAINT expedition_report_fault");}
                p=ReloadBall(p);Check(failed && stage(p)==4 && p.Entity.Money==money && p.Entity.Experience==beforeXp && p.Entity.Level==beforeLevel && CountId(p,710260)==1,"Desert Expedition report failure rolls back silver, experience and stage");
            }
            byte reportLevel=p.Entity.Level;ulong reportXp=p.Entity.Experience,award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
            Check(act(p,"Report") && !act(p,"Report") && p.Entity.Money==money+6000 && stage(p)==5 && CountId(p,710260)==0,"Desert Expedition report pays once: "+life);
            ulong gained=p.Entity.Experience;for(byte level=reportLevel;level<p.Entity.Level;level++)gained+=DataHolder.LevelExperience(level);Check(gained==reportXp+award,"Desert Expedition report grants the capped EXP amount: "+life);
            p=ReloadBall(p);Check(stage(p)==5 && p.Entity.Money==money+6000,"Desert Expedition report persists: "+life);
            Check(!act(p,"Claim"),"Desert Expedition final claim requires Twin City: "+life);p.BindWorldMap(1002);
            if(life==0){
                while(p.Inventory.Count<38)Pack(p,1088001,1);Check(!act(p,"Claim") && stage(p)==5 && CountId(p,728250)==0,"Desert Expedition final reward requires three free slots");
                foreach(var item in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT expedition_reward_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;
                try{act(p,"Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT expedition_reward_fault");}
                Check(failed && stage(p)==5 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728250,723911)",p.Entity.UID)==0,"Desert Expedition soul insert failure rolls back the entire final reward");
            }
            var claimant=p;int winners=0;Parallel.For(0,4,j=>{if(act(claimant,"Claim"))System.Threading.Interlocked.Increment(ref winners);});
            Check(winners==1 && !act(p,"Claim") && stage(p)==6 && CountId(p,728250)==1 && CountId(p,723911)==1 && CountId(p,soul)==1,"Desert Expedition concurrent final claims pay exactly once: "+life);
            Check(Scalar("SELECT count(*) FROM revora_desert_expedition e WHERE uid=@uid AND cardinality(rewards)=3 AND (SELECT count(*) FROM items i WHERE i.uid=ANY(e.rewards) AND i.entityid=e.uid AND i.bound=1)=3",p.Entity.UID)==1,"Desert Expedition receipt identifies all three owned bound rewards: "+life);
            uint[] ids=p.Inventory.Objects.Where(x=>x.ID==728250 || x.ID==723911 || x.ID==soul).Select(x=>x.UID).OrderBy(x=>x).ToArray();p=ReloadBall(p);
            Check(ids.Length==3 && p.Inventory.Objects.Where(x=>ids.Contains(x.UID)).All(x=>x.Bound) && ids.All(x=>p.Inventory.ContainsUID(x)) && !act(p,"Claim"),"Desert Expedition exact bound final rewards persist without replay: "+life);
        }
    }
    static void VerifyDesertTreasureRewards(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");World(runtime,1000);
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)98:(byte)140,life,15));ulong money=p.Entity.Money;
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
            Check(!act(p,"Claim"),"Desert Treasure cannot claim before earning: "+life);p.BindWorldMap(1000);
            Check(!act(p,"Report"),"Desert Treasure cannot report before relic capture: "+life);
            // Isolate reward transactions: relic capture is not implemented by this fixture.
            Query("UPDATE revora_desert_treasure SET stage=6 WHERE uid=@uid","uid",(long)p.Entity.UID);
            var relic=NestedPack(p,721872,1,true);Check(!act(p,"Report"),"Desert Treasure untracked relic cannot report: "+life);
            Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,721872)","item",(long)relic.UID,"uid",(long)p.Entity.UID,"life",(int)life);
            if(life==0){Query("ALTER TABLE revora_desert_treasure ADD CONSTRAINT treasure_report_fault CHECK(stage<>7) NOT VALID");bool failed=false;ulong beforeXp=p.Entity.Experience;byte beforeLevel=p.Entity.Level;
                try{act(p,"Report");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_treasure DROP CONSTRAINT treasure_report_fault");}
                p=ReloadBall(p);Check(failed && stage(p)==6 && p.Entity.Money==money && p.Entity.Experience==beforeXp && p.Entity.Level==beforeLevel && CountId(p,721872)==1,"Desert Treasure report failure rolls back silver, experience and stage");
            }
            byte reportLevel=p.Entity.Level;ulong reportXp=p.Entity.Experience,award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
            Check(act(p,"Report") && !act(p,"Report") && p.Entity.Money==money+6000 && stage(p)==7 && CountId(p,721872)==0,"Desert Treasure report pays once: "+life);
            ulong gained=p.Entity.Experience;for(byte level=reportLevel;level<p.Entity.Level;level++)gained+=DataHolder.LevelExperience(level);Check(gained==reportXp+award,"Desert Treasure report grants the capped EXP amount: "+life);
            p=ReloadBall(p);Check(stage(p)==7 && p.Entity.Money==money+6000,"Desert Treasure report persists: "+life);
            Check(!act(p,"Claim"),"Desert Treasure final claim requires Twin City: "+life);p.BindWorldMap(1002);
            if(life==0){
                while(p.Inventory.Count<38)Pack(p,1088001,1);Check(!act(p,"Claim") && stage(p)==7 && CountId(p,728249)==0,"Desert Treasure final reward requires three free slots");
                foreach(var item in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT treasure_reward_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;
                try{act(p,"Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT treasure_reward_fault");}
                Check(failed && stage(p)==7 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728249,723911)",p.Entity.UID)==0,"Desert Treasure soul insert failure rolls back the entire final reward");
            }
            Check(act(p,"Claim") && !act(p,"Claim") && stage(p)==8 && CountId(p,728249)==1 && CountId(p,723911)==1 && CountId(p,soul)==1,"Desert Treasure final rewards claimed exactly once: "+life);
            uint[] ids=p.Inventory.Objects.Where(x=>x.ID==728249 || x.ID==723911 || x.ID==soul).Select(x=>x.UID).OrderBy(x=>x).ToArray();p=ReloadBall(p);
            Check(ids.Length==3 && p.Inventory.Objects.Where(x=>ids.Contains(x.UID)).All(x=>x.Bound) && ids.All(x=>p.Inventory.ContainsUID(x)) && !act(p,"Claim"),"Desert Treasure exact bound final rewards persist without replay: "+life);
        }
    }
    static void VerifyFamilyFeudHunt(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);Call(store,null,"Initialize");World(runtime,1015);
        var roll=store.GetField("Roll",Hidden);object prior=roll.GetValue(null);
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,int> count=p=>(int)Call(store,null,"KillCount",p);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        try{roll.SetValue(null,(Func<int,int>)(n=>0));
            var p=ReloadBall(WelcomePlayer(town,100,0,15));p.BindWorldMap(1015);p.Entity.X=362;p.Entity.Y=469;
            lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
            Check(!credit(p,kill(p,"BanditL98")) && count(p)==0,"Family Feud wrong family cannot advance leader counter");
            for(int i=0;i<50;i++){var b=kill(p,"BanditL97");if(!credit(p,b) || credit(p,b))throw new Exception("Family Feud count/replay mismatch");}
            Check(count(p)==50 && !credit(p,kill(p,"BanditL97")),"Family Feud fifty distinct kills cap leader prerequisite");p=ReloadBall(p);Check(count(p)==50,"Family Feud leader count survives reload");
            // Reward stages are seeded until owned leader/courier earning is integrated.
            Query("UPDATE revora_family_feud SET stage=3 WHERE uid=@uid","uid",(long)p.Entity.UID);
            roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"BanditL98");Check(!credit(p,miss),"Family Feud 50-percent boundary yields no Indigo");roll.SetValue(null,(Func<int,int>)(n=>0));Check(!credit(p,miss),"Family Feud losing corpse cannot reroll");
            var winner=kill(p,"BanditL98");Check(credit(p,winner) && !credit(p,winner) && CountId(p,710563)==1 && p.Inventory.Objects.Single(x=>x.ID==710563).Bound,"Family Feud earned Indigo is bound and single-use");
            uint token=p.Inventory.Objects.Single(x=>x.ID==710563).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)token);p=ReloadBall(p);
            Check(!recover(p) && !credit(p,kill(p,"BanditL98")),"Family Feud banked earned token cannot be copied or redropped");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)token);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=token},p);
            Query("ALTER TABLE items ADD CONSTRAINT family_recover_fault CHECK(id<>710563) NOT VALID");bool failed=false;
            try{recover(p);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT family_recover_fault");}
            Check(failed && Scalar("SELECT state FROM revora_family_feud_materials WHERE item_uid=@uid",token)==0 && Scalar("SELECT COUNT(*) FROM revora_family_feud_recoveries WHERE uid=@uid",p.Entity.UID)==0,"Family Feud failed recovery retains entitlement without a receipt");
            Check(recover(p) && !recover(p) && CountId(p,710563)==1 && p.Inventory.Objects.Single(x=>x.ID==710563).UID!=token,"Family Feud lost earned token recovers exactly once");p=ReloadBall(p);Check(CountId(p,710563)==1 && Scalar("SELECT COUNT(*) FROM revora_family_feud_recoveries WHERE uid=@uid",p.Entity.UID)==1,"Family Feud recovery item and receipt persist");
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Advance",p,3,(Func<bool>)(()=>true)),"Family Feud earned recovered Indigo advances the actual hand-in");
            for(int i=0;i<10;i++)if(!credit(p,kill(p,"Banditti")))throw new Exception("Family Feud courier prerequisite failed");Check(count(p)==10 && !credit(p,kill(p,"Banditti")),"Family Feud ten Banditti kills cap courier prerequisite");
            p=ReloadBall(p);Check(count(p)==10 && !recover(p),"Family Feud courier progress persists and consumed tokens cannot recover");
        }finally{roll.SetValue(null,prior);}
    }
    static void VerifyFamilyFeudEncounters(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudEncounters",true);Call(store,null,"Initialize");World(runtime,1015);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("Roll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,26,22,0,0,DateTimeKind.Utc);
        Func<GameState,Entity> spawn=p=>(Entity)Call(fights,null,"Start",p);
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));
            var p=ReloadBall(WelcomePlayer(town,100,0,15));p.LoadItemStats();p.BindWorldMap(1015);p.Entity.X=363;p.Entity.Y=468;
            var thief=ReloadBall(WelcomePlayer(town,100,0,15));thief.LoadItemStats();thief.BindWorldMap(1015);thief.Entity.X=363;thief.Entity.Y=468;
            lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
            Check(spawn(p)==null,"Family Feud leader cannot spawn before fifty kills");
            Query("INSERT INTO revora_family_feud_kills(uid,life,stage,kills) VALUES(@uid,0,2,50)","uid",(long)p.Entity.UID);
            var leader=spawn(p);Check(leader!=null && spawn(p)==null,"Family Feud owns at most one active leader");
            uint hp=leader.Hitpoints;hit(thief,leader);Check(leader.Hitpoints==hp && CountId(thief,710562)==0,"Family Feud other player cannot damage or steal owned leader");
            hit(p,leader);Check(leader.Dead && CountId(p,710562)==1 && !Fenced(p),"Family Feud actual combat death earns Red Token");Call(fights,null,"TryDie",leader,p.Entity);Call(fights,null,"Tick");Check(CountId(p,710562)==1 && !p.Map.Entities.ContainsKey(leader.UID),"Family Feud leader replay and corpse cleanup");
            Query("UPDATE revora_family_feud SET stage=4 WHERE uid=@uid","uid",(long)p.Entity.UID);p.Entity.X=526;p.Entity.Y=344;Check(spawn(p)==null,"Family Feud courier cannot spawn before ten Banditti");
            Query("INSERT INTO revora_family_feud_kills(uid,life,stage,kills) VALUES(@uid,0,4,10)","uid",(long)p.Entity.UID);var courier=spawn(p);hit(p,courier);Call(fights,null,"Tick");Check(CountId(p,710564)==1,"Family Feud courier actual death earns Black Token");
            Query("UPDATE revora_family_feud SET stage=5 WHERE uid=@uid","uid",(long)p.Entity.UID);
            for(int i=0;i<10;i++){var b=spawn(p);if(b==null)throw new Exception("Deliverer spawn missing");hit(p,b);Call(fights,null,"Tick");if(i<9 && CountId(p,710565)!=0)throw new Exception("Early Pitch Token");}
            Check(CountId(p,710565)==1 && (int)Call(store,null,"KillCount",p)==10 && spawn(p)==null,"Family Feud exactly ten deliverers earn one Pitch Token");
            Query("UPDATE revora_family_feud SET stage=6 WHERE uid=@uid","uid",(long)p.Entity.UID);p.Entity.X=571;p.Entity.Y=415;roll.SetValue(null,(Func<int,int>)(n=>50));var senior=spawn(p);hit(p,senior);Call(fights,null,"Tick");Check(CountId(p,710566)==0,"Family Feud Senior 50-percent miss grants no token");
            roll.SetValue(null,(Func<int,int>)(n=>0));senior=spawn(p);hit(p,senior);Call(fights,null,"Tick");Check(CountId(p,710566)==1,"Family Feud fresh Senior kill earns Scarlet");
            var pending=ReloadBall(WelcomePlayer(town,100,0,15));pending.LoadItemStats();pending.BindWorldMap(1015);pending.Entity.X=571;pending.Entity.Y=415;
            lock(pending.ItemSyncRoot)Call(store,null,"Advance",pending,0,(Func<bool>)(()=>true));Query("UPDATE revora_family_feud SET stage=6 WHERE uid=@uid","uid",(long)pending.Entity.UID);
            var timeout=spawn(pending);now=now.AddMinutes(5);Check((bool)Call(fights,null,"BlockAttack",pending.Entity,timeout),"Family Feud expired encounter blocks combat");Call(fights,null,"Tick");Check(!pending.Map.Entities.ContainsKey(timeout.UID),"Family Feud timeout removes only owned actor");
            var replaced=spawn(pending);pending=ReloadBall(pending);Call(fights,null,"Tick");Check(!pending.Map.Entities.ContainsKey(replaced.UID),"Family Feud replacement login cleans old session encounter");
            while(pending.Inventory.Count<40)Pack(pending,1088001,1);var full=spawn(pending);hit(pending,full);Call(fights,null,"Tick");
            Check(!Fenced(pending) && CountId(pending,710566)==0 && Scalar("SELECT COUNT(*) FROM revora_family_feud_encounter_kills WHERE uid=@uid",pending.Entity.UID)==0,"Family Feud full bag retains unclaimed encounter entitlement");
            foreach(var item in pending.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())pending.Inventory.Remove(item,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT family_encounter_fault CHECK(id<>710566) NOT VALID");var failedBody=spawn(pending);
            try{hit(pending,failedBody);}finally{Query("ALTER TABLE items DROP CONSTRAINT family_encounter_fault");}Call(fights,null,"Tick");
            Check(Fenced(pending) && Scalar("SELECT COUNT(*) FROM revora_family_feud_encounter_kills WHERE uid=@uid",pending.Entity.UID)==0 && Scalar("SELECT COUNT(*) FROM revora_family_feud_materials WHERE uid=@uid",pending.Entity.UID)==0,"Family Feud failed encounter reward rolls back receipt and tracking");
            pending=ReloadBall(pending);pending.LoadItemStats();var retry=spawn(pending);hit(pending,retry);Call(fights,null,"Tick");
            Check(CountId(pending,710566)==1 && Scalar("SELECT COUNT(*) FROM revora_family_feud_encounter_kills WHERE uid=@uid",pending.Entity.UID)==1,"Family Feud fresh encounter after failed reward commits once");

        }finally{now=now.AddHours(1);Call(fights,null,"Tick");clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyFamilyFeudChamber(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudScenes",true);Call(store,null,"Initialize");Call(store,null,"InitializeVisits");World(runtime,1015);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,23,0,0,DateTimeKind.Utc);object scene=null;GameState p=null;
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));p=ReloadBall(WelcomePlayer(town,100,0,15));p.BindWorldMap(1015);p.Entity.X=667;p.Entity.Y=780;
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));var gold=NestedPack(p,710567,1,true);
            Query("INSERT INTO revora_family_feud_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,710567)","item",(long)gold.UID,"uid",(long)p.Entity.UID);
            Query("UPDATE revora_family_feud SET stage=7,disguise_until=@until WHERE uid=@uid","until",new DateTimeOffset(now.AddSeconds(90)),"uid",(long)p.Entity.UID);
            lock(p.ItemSyncRoot)scene=Call(scenes,null,"TryCreate",p);var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1801 && map.Npcs.Count==6 && map.Entities.Count==0 && map.Portals.Count==0,"Family Feud chamber owns six actors on adapted installed terrain");
            var intruder=ReloadBall(WelcomePlayer(town,100,0,15));intruder.Entity.Teleport(map.ID,50,55);Check(intruder.Entity.MapID!=map.ID,"Family Feud actual teleport rejects intruder");
            lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Family Feud live disguise and tracked Golden Token admit owner");
            Check(p.Entity.MapID==map.ID && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Family Feud chamber admission persists");
            ushort beforeX=p.Entity.X,beforeY=p.Entity.Y;p.Entity.Teleport(map.ID,50,60);Check(p.Entity.X==beforeX && p.Entity.Y==beforeY,"Family Feud same-map teleport cannot solve puzzle");
            Func<string,bool> act=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,scene,(Func<bool>)(()=>true));};
            Action kerry=()=>{p.ActiveNpc=4707;p.Entity.X=54;p.Entity.Y=55;};
            Check(!act("StartPuzzle"),"Family Feud remote Kerry puzzle start denied");kerry();Check(act("StartPuzzle") && !act("StartPuzzle") && !act("FinishPuzzle"),"Family Feud starts ten-second trial once and rejects premature finish");
            p.Entity.X=54;p.Entity.Y=64;Check(!act("ObservePuzzle") && Scalar("SELECT puzzle_step FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Family Feud wrong first color resets trial");
            kerry();Check(act("StartPuzzle"),"Family Feud failed order can restart at Kerry");now=now.AddSeconds(11);p.Entity.X=50;p.Entity.Y=60;Check(!act("ObservePuzzle"),"Family Feud expired puzzle cannot advance");kerry();Check(act("StartPuzzle"),"Family Feud expired trial restarts");
            ushort[,] pads={{50,60},{54,64},{58,64},{60,60},{58,60}};DateTime began=now;
            for(int i=0;i<5;i++){now=began.AddSeconds(i==4?10:i+1);p.Entity.X=pads[i,0];p.Entity.Y=pads[i,1];bool done=act("ObservePuzzle");Check(done==(i==4) && Scalar("SELECT puzzle_step FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==i+1,"Family Feud ordered pad persists: "+i);Check(!act("ObservePuzzle"),"Family Feud repeated pad cannot advance twice: "+i);}
            Check(CountId(p,710567)==1 && Scalar("SELECT stage FROM revora_family_feud WHERE uid=@uid",p.Entity.UID)==7,"Family Feud solved eyes still require Kerry report");kerry();now=now.AddSeconds(20);Check(!act("StartPuzzle"),"Family Feud solved trial cannot be reset after deadline");
            Query("ALTER TABLE revora_family_feud ADD CONSTRAINT family_maze_fault CHECK(stage<>8) NOT VALID");bool failed=false;ulong money=p.Entity.Money;
            try{act("FinishPuzzle");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_family_feud DROP CONSTRAINT family_maze_fault");}
            Check(failed && CountId(p,710567)==1 && p.Entity.Money==money && Scalar("SELECT state FROM revora_family_feud_materials WHERE item_uid=@uid",gold.UID)==0,"Family Feud maze finish failure retains token and silver");
            Check(act("FinishPuzzle") && !act("FinishPuzzle") && p.Entity.Money==money+100 && CountId(p,710567)==0,"Family Feud Kerry consumes Golden Token and pays once");Call(scenes,null,"Tick");
            Check(p.Entity.MapID==1015 && p.Entity.X==667 && p.Entity.Y==780 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Family Feud completed chamber returns owner and releases terrain");
        }finally{if(scene!=null && !(bool)Field(scene,"Released")){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,prior);}
    }
    static void VerifyFamilyFeudQuest(string runtime,Map town,Action<GameState> completed=null,Action<GameState> initialize=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudScenes",true);World(runtime,1015);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("Roll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,27,0,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_family_feud WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc!=4707)p.BindWorldMap(npc==900071727?(ushort)1002:(ushort)1015);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        Action<GameState,string> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;hit(p,b);};
        Action<GameState,string,ushort,ushort> owned=(p,name,x,y)=>{p.BindWorldMap(1015);p.Entity.X=x;p.Entity.Y=y;now=now.AddSeconds(4);Call(quest,null,"Tick");var body=p.Map.Entities.Values.Single(b=>b.Name==name);hit(p,body);Call(quest,null,"Tick");};
        Action<GameState> journal=p=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);int st=stage(p);var own=rows.Where(x=>(uint)x.UID>=1819 && (uint)x.UID<=1827).ToArray();Check(own.Length==9 && own.All(x=>x.Status==(st>(int)x.UID-1818?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-1818?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Family Feud ordinary journal matches saved stage "+st);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));var low=ReloadBall(WelcomePlayer(town,94,0,15));talk(low,900071727,161);Check(stage(low)==0,"Family Feud ordinary underlevel acceptance denied");
            for(byte life=0;life<3;life++){
                var p=WelcomePlayer(town,life==0?(byte)95:(byte)140,life,15);if(initialize!=null)initialize(p);p=ReloadBall(p);p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=161},p);Check(stage(p)==0,"Family Feud remote acceptance denied: "+life);
                for(uint id=1819;id<=1827;id++){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Family Feud forged journal cannot grant progress: "+id);}
                talk(p,900071727,161);journal(p);talk(p,4705,1);Check(stage(p)==2,"Family Feud ordinary Felix referral: "+life);
                p.Entity.X=363;p.Entity.Y=468;for(int i=0;i<50;i++)kill(p,"BanditL97");owned(p,"BanditLeaderL97",363,468);Check(CountId(p,710562)==1,"Family Feud automatic leader earns Red: "+life);talk(p,4705,1);journal(p);
                p.Entity.X=363;p.Entity.Y=468;kill(p,"BanditL98");talk(p,4705,1);Check(stage(p)==4,"Family Feud actual Indigo hunt and hand-in: "+life);
                p.Entity.X=526;p.Entity.Y=344;for(int i=0;i<10;i++)kill(p,"Banditti");owned(p,"BanditCourier",526,344);talk(p,4705,1);Check(stage(p)==5,"Family Feud automatic courier earns Black: "+life);
                for(int i=0;i<10;i++)owned(p,"BanditDeliverer",526,344);talk(p,4705,1);Check(stage(p)==6,"Family Feud automatic ten Deliverers earn Pitch: "+life);
                owned(p,"SeniorBandit",571,415);talk(p,4705,1);Check(stage(p)==7 && CountId(p,710567)==1 && p.Entity.Mesh==201,"Family Feud Scarlet hand-in grants timed Bandit disguise: "+life);journal(p);ItemHandler.UseItem(p.Inventory.Objects.Single(x=>x.ID==710567),p);Check(!Fenced(p) && CountId(p,710567)==1,"Family Feud ordinary token use preserves ownership: "+life);
                if(life==0){now=now.AddSeconds(91);Call(quest,null,"Tick");Check(p.Entity.Mesh!=201,"Family Feud expired disguise restores appearance");talk(p,4706,1);Check(p.Entity.MapID==1015,"Family Feud expired disguise cannot enter chamber");talk(p,4705,1);Check(p.Entity.Mesh==201,"Family Feud Felix renews expired disguise");uint gold=p.Inventory.Objects.Single(x=>x.ID==710567).UID;ItemHandler.DropItem(new ItemUsage(true){UID=gold},p);talk(p,4705,2);talk(p,4705,2);Check(CountId(p,710567)==1 && p.Inventory.Objects.Single(x=>x.ID==710567).UID!=gold,"Family Feud ordinary lost Golden Token recovers once");}
                p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);Check(p.Entity.Mesh==201 && stage(p)==7,"Family Feud saved active disguise restores after login: "+life);
                talk(p,4706,1);var scene=Call(scenes,null,"Find",p);Check(scene!=null && p.Map.BaseID==1801 && p.Entity.Mesh!=201,"Family Feud ordinary Guide enters owned chamber and clears disguise: "+life);
                talk(p,4707,1);ushort[,] pads={{50,60},{54,64},{58,64},{60,60},{58,60}};
                for(int i=0;i<5;i++){now=now.AddSeconds(1);SkyJump(p,pads[i,0],pads[i,1]);Check(Scalar("SELECT puzzle_step FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==i+1,"Family Feud accepted jump advances ordered eye: "+i+" life "+life);}
                talk(p,4707,2);Check(stage(p)==8 && p.Entity.MapID==1015 && CountId(p,710567)==0,"Family Feud ordinary Kerry completes and returns owner: "+life);talk(p,4705,1);Check(stage(p)==9 && p.Entity.Money==money+6700,"Family Feud full route silver paid once: "+life);journal(p);
                talk(p,900071727,161);talk(p,900071727,161);Check(stage(p)==10 && CountId(p,728248)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(x=>x.ID>=800000 && x.ID<900000 && x.Bound)==1,"Family Feud ordinary final rewards granted once: "+life);journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage(p)==10 && CountId(p,728248)==1 && p.Entity.Money==money+6700,"Family Feud ordinary full route persists: "+life);
                if(completed!=null)completed(p);
            }
        }finally{now=now.AddHours(1);Call(quest,null,"Tick");clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyFamilyFeudRecovery(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudScenes",true);Call(store,null,"Initialize");Call(store,null,"InitializeVisits");World(runtime,1015);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,27,1,0,0,DateTimeKind.Utc);object scene=null;GameState p=null;
        Func<GameState> player=()=>{var who=ReloadBall(WelcomePlayer(town,100,0,15));who.BindWorldMap(1015);who.Entity.X=667;who.Entity.Y=780;lock(who.ItemSyncRoot)Call(store,null,"Advance",who,0,(Func<bool>)(()=>true));var gold=NestedPack(who,710567,1,true);Query("INSERT INTO revora_family_feud_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,710567)","item",(long)gold.UID,"uid",(long)who.Entity.UID);Query("UPDATE revora_family_feud SET stage=7,disguise_until=@until WHERE uid=@uid","uid",(long)who.Entity.UID,"until",new DateTimeOffset(now.AddSeconds(90)));return who;};
        Action enter=()=>{lock(p.ItemSyncRoot){scene=Call(scenes,null,"TryCreate",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Family Feud recovery fixture uses actual admission");}};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));p=player();enter();var map=(Map)Field(scene,"Map");
            now=now.AddMinutes(20);Call(scenes,null,"Tick");Check((bool)Field(scene,"Released") && p.Entity.MapID==1015 && CountId(p,710567)==1 && Scalar("SELECT stage FROM revora_family_feud WHERE uid=@uid",p.Entity.UID)==7,"Family Feud timed-out chamber preserves Golden Token and stage");
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"RenewDisguise",p,(Func<bool>)(()=>true)),"Family Feud timed-out journey can renew after return");enter();map=(Map)Field(scene,"Map");
            p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Call(scenes,null,"RecoverLogin",p);
            Check((bool)Field(scene,"Released") && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==0 && CountId(p,710567)==1,"Family Feud replacement login retires visit without consuming token");
            p.BindWorldMap(1015);p.Entity.X=667;p.Entity.Y=780;enter();map=(Map)Field(scene,"Map");GameState removed;Check(Kernel.GamePool.TryRemove(p.Entity.UID,out removed),"Family Feud disconnected fixture removes canonical session");Call(scenes,null,"Tick");
            Check((bool)Field(scene,"Released") && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1015 && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Family Feud offline cleanup saves return and retires visit");Kernel.GamePool[p.Entity.UID]=p;p.BindWorldMap(1015);p.Entity.X=667;p.Entity.Y=780;
            enter();map=(Map)Field(scene,"Map");Kernel.GamePool.TryRemove(p.Entity.UID,out removed);Call(scenes,null,"Release",scene);
            Query("CREATE FUNCTION suppress_family_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_family_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_family_return()");bool failed=false;
            try{Call(scenes,null,"RecoverInterrupted");}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_family_return ON entities;DROP FUNCTION suppress_family_return()");}
            Check(failed && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Family Feud suppressed startup return retains active visit for retry");
            Call(scenes,null,"RecoverInterrupted");Call(scenes,null,"RecoverInterrupted");Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1015 && Scalar("SELECT COUNT(*) FROM revora_family_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Family Feud startup retry is atomic and idempotent");Kernel.GamePool[p.Entity.UID]=p;p.BindWorldMap(1015);p.Entity.X=667;p.Entity.Y=780;
            var candidates=Enumerable.Range(0,9).Select(i=>ReloadBall(WelcomePlayer(town,100,0,15))).ToArray();var allocated=new System.Collections.Generic.List<object>();int before=Kernel.Maps.Count;
            try{for(int i=0;i<8;i++)lock(candidates[i].ItemSyncRoot)allocated.Add(Call(scenes,null,"TryCreate",candidates[i]));Check(allocated.All(x=>x!=null) && Kernel.Maps.Count==before+8,"Family Feud eight owned chambers reach bounded capacity");lock(candidates[8].ItemSyncRoot)Check(Call(scenes,null,"TryCreate",candidates[8])==null && Kernel.Maps.Count==before+8,"Family Feud ninth allocation cannot leak a map");}
            finally{foreach(var owned in allocated)if(owned!=null)Call(scenes,null,"Release",owned);}
            Check(Kernel.Maps.Count==before,"Family Feud unentered chamber cleanup releases all maps");
        }finally{if(p!=null && !Kernel.GamePool.ContainsKey(p.Entity.UID))Kernel.GamePool[p.Entity.UID]=p;if(scene!=null && !(bool)Field(scene,"Released"))lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);clock.SetValue(null,prior);}
    }
    static void VerifyFamilyFeudPopulation(string runtime,Map town){
        var map=World(runtime,1015);var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.FamilyFeudStore",true);
        Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        Check(Scalar("SELECT CASE WHEN current_schema()='revora_potency_validation' THEN 1 ELSE 0 END",0)==1,"Family Feud population mutations are restricted to the disposable schema");
        Query("CREATE TABLE family_spawn_fixture AS SELECT * FROM monsterspawns WHERE mapid=1015");
        Entity[] spawned=new Entity[0];
        try{
            Query("INSERT INTO monsterspawns SELECT * FROM public.monsterspawns WHERE mapid=1015 AND npctype=84 ON CONFLICT DO NOTHING");
            Query("DELETE FROM monsterspawns WHERE mapid=1015 AND npctype<>84");int expected=(int)Scalar("SELECT COALESCE(SUM(max_per_gen),0) FROM monsterspawns WHERE mapid=1015",0);
            Check(expected>0 && map.Entities.Count==0,"Family Feud population test starts with real configured Banditti rows");map.LoadMonsters();
            var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);
            spawned=map.Entities.Values.Where(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==84).ToArray();
            Check(spawned.Length==expected,"Family Feud actual loader preserves configured Banditti count");
            Check(spawned.All(b=>b.MonsterInfo.BoundCX==10 && b.MonsterInfo.BoundCY==10 && (b.MonsterInfo.BoundX==518 && b.MonsterInfo.BoundY==339 || b.MonsterInfo.BoundX==540 && b.MonsterInfo.BoundY==338) && b.X>=b.MonsterInfo.BoundX && b.X<b.MonsterInfo.BoundX+10 && b.Y>=b.MonsterInfo.BoundY && b.Y<b.MonsterInfo.BoundY+10),"Family Feud loaded monsters carry corrected spawn and respawn bounds");
            Check(Scalar("SELECT COUNT(*) FROM monsterspawns WHERE mapid=1015 AND npctype=84 AND ((bound_x=114 AND bound_y=312) OR (bound_x=293 AND bound_y=559))",0)==2,"Family Feud loader repair leaves database definitions unchanged");
            Call(quest,null,"Initialize");var p=ReloadBall(WelcomePlayer(town,100,0,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();p.BindWorldMap(1015);
            lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Query("UPDATE revora_family_feud SET stage=4 WHERE uid=@uid","uid",(long)p.Entity.UID);
            for(int i=0;i<10;i++){
                var body=spawned[i%spawned.Length];if(body.Dead){body.DeathStamp=new Time32(1);Call(typeof(Map),null,"_timerCallBack",map,Time32.Now.GetHashCode());}
                Check(!body.Dead && body.X>=body.MonsterInfo.BoundX && body.X<body.MonsterInfo.BoundX+10 && body.Y>=body.MonsterInfo.BoundY && body.Y<body.MonsterInfo.BoundY+10,"Family Feud real population provides living hunt target "+i);
                p.Entity.X=body.X;p.Entity.Y=body.Y;body.IsDropped=true;uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
            }
            Check((int)Call(store,null,"KillCount",p)==10 && (int)Call(store,null,"EncounterKind",p)==1,"Family Feud ten actual loaded population kills unlock courier");
            var killed=spawned[0];killed.DeathStamp=new Time32(1);Call(typeof(Map),null,"_timerCallBack",map,Time32.Now.GetHashCode());Check(!killed.Dead && killed.Hitpoints==killed.MonsterInfo.Hitpoints && killed.MonsterInfo.BoundCX==10,"Family Feud ordinary respawner restores the corrected population");
        }finally{
            var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);
            foreach(var b in spawned)map.RemoveEntity(b);
            Query("DELETE FROM monsterspawns WHERE mapid=1015;INSERT INTO monsterspawns SELECT * FROM family_spawn_fixture;DROP TABLE family_spawn_fixture");
        }
    }
    static void VerifyDesertTreasurePopulation(string runtime,Map town){
        var map=World(runtime,1001);World(runtime,1000);var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(quest,null,"Initialize");
        var population=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasurePopulation",true);var sample=new Entity(EntityFlag.Monster,false){MonsterInfo=MonsterInformation.MonsterInformations[20].Copy(),MapID=1001,X=0,Y=0};sample.MonsterInfo.BoundX=91;sample.MonsterInfo.BoundY=283;sample.MonsterInfo.BoundCX=sample.MonsterInfo.BoundCY=1;
        Check((bool)Call(population,null,"Place",map,sample) && sample.X==91 && sample.Y==283,"Desert Treasure deterministic fallback finds the only valid configured cell");
        map.Floor[91,283,MapObjectType.Monster,null]=false;try{sample.X=sample.Y=0;Check(!(bool)Call(population,null,"Place",map,sample),"Desert Treasure fallback cannot overfill occupied region");}finally{map.Floor[91,283,MapObjectType.Monster,null]=true;}
        var roll=store.GetField("HuntRoll",Hidden);object oldRoll=roll.GetValue(null);Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        Check(Scalar("SELECT CASE WHEN current_schema()='revora_potency_validation' THEN 1 ELSE 0 END",0)==1,"Desert Treasure population mutations restricted to disposable schema");
        Query("CREATE TABLE treasure_spawn_fixture AS SELECT * FROM monsterspawns WHERE mapid=1001");Entity[] spawned=new Entity[0];
        try{roll.SetValue(null,(Func<int,int>)(n=>0));Query("INSERT INTO monsterspawns SELECT * FROM public.monsterspawns WHERE mapid=1001 AND npctype IN(20,80,56,81,86) ON CONFLICT DO NOTHING");Query("DELETE FROM monsterspawns WHERE mapid=1001 AND npctype NOT IN(20,80,56,81,86)");
            int expected=(int)Scalar("SELECT COALESCE(SUM(max_per_gen),0) FROM monsterspawns WHERE mapid=1001",0);Check(expected>0,"Desert Treasure configured population exists");map.LoadMonsters();var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);
            spawned=map.Entities.Values.Where(b=>b.MonsterInfo!=null && new uint[]{20,80,56,81,86}.Contains(b.MonsterInfo.ID)).ToArray();System.Console.WriteLine("TREASURE-POP|expected="+expected+"|actual="+spawned.Length+"|outside="+spawned.Count(b=>b.X<b.MonsterInfo.BoundX || b.X>=b.MonsterInfo.BoundX+b.MonsterInfo.BoundCX || b.Y<b.MonsterInfo.BoundY || b.Y>=b.MonsterInfo.BoundY+b.MonsterInfo.BoundCY)+"|dead="+spawned.Count(b=>b.Dead));Check(spawned.Length==expected && spawned.All(b=>!b.Dead && b.X>=b.MonsterInfo.BoundX && b.X<b.MonsterInfo.BoundX+b.MonsterInfo.BoundCX && b.Y>=b.MonsterInfo.BoundY && b.Y<b.MonsterInfo.BoundY+b.MonsterInfo.BoundCY),"Desert Treasure real loader places configured count inside bounds");
            foreach(uint family in new uint[]{20,80,56,81,86}){
                var body=spawned.First(b=>b.MonsterInfo.ID==family);var p=ReloadBall(WelcomePlayer(town,110,0,15));p.LoadItemStats();p.BindWorldMap(1001);p.Entity.X=body.X;p.Entity.Y=body.Y;
                lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));bool key=family==20 || family==80;Query("UPDATE revora_desert_treasure SET stage=@stage WHERE uid=@uid","stage",key?2:5,"uid",(long)p.Entity.UID);uint type=key?721876u:721877u;
                body.IsDropped=true;uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
                Check(body.Dead && CountId(p,type)==1,"Desert Treasure actual loaded family earns material "+family);
                body.DeathStamp=new Time32(1);Call(typeof(Map),null,"_timerCallBack",map,Time32.Now.GetHashCode());Check(!body.Dead && body.Hitpoints==body.MonsterInfo.Hitpoints,"Desert Treasure actual respawner revives family "+family);
                if(!key){p.Entity.X=body.X;p.Entity.Y=body.Y;body.IsDropped=true;damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);Check(CountId(p,type)==2,"Desert Treasure revived corpse earns fresh essence "+family);}
            }
        }finally{var timer=(IDisposable)Field(map,"Timer");if(timer!=null)timer.Dispose();typeof(Map).GetField("Timer",Instance).SetValue(map,null);foreach(var b in spawned)map.RemoveEntity(b);roll.SetValue(null,oldRoll);Query("DELETE FROM monsterspawns WHERE mapid=1001;INSERT INTO monsterspawns SELECT * FROM treasure_spawn_fixture;DROP TABLE treasure_spawn_fixture");}
    }
    static void VerifyDesertTreasureQuest(string runtime,Map town,GameState continuing=null,Action<GameState> completed=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);World(runtime,1000);World(runtime,1001);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("HuntRoll",Hidden);var vault=store.GetField("VaultRoll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null),oldVault=vault.GetValue(null);DateTime now=new DateTime(2026,9,27,4,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_desert_treasure WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc!=9918281){p.BindWorldMap(npc==900071727?(ushort)1002:npc==4651?(ushort)1000:(ushort)1001);var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y-2);}p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        Action<GameState,string> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;hit(p,b);};
        Action<GameState,uint> use=(p,type)=>ItemHandler.UseItem(p.Inventory.Objects.First(x=>x.ID==type),p);
        Action<GameState> journal=p=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);int st=stage(p);var own=rows.Where(x=>(uint)x.UID>=1828 && (uint)x.UID<=1834).ToArray();Check(own.Length==7 && own.All(x=>x.Status==(st>(int)x.UID-1827?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-1827?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Desert Treasure ordinary journal matches stage "+st);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));vault.SetValue(null,(Func<int,int>)(n=>0));
            var low=ReloadBall(WelcomePlayer(town,97,0,15));talk(low,900071727,171);Check(stage(low)==0,"Desert Treasure ordinary underlevel acceptance denied");
            for(byte life=0;life<3;life++){
                var p=life==1 && continuing!=null?continuing:ReloadBall(WelcomePlayer(town,life==0?(byte)98:(byte)140,life,15));if(life!=1 || continuing==null){Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);}ulong openingMoney=p.Entity.Money;p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=171},p);Check(stage(p)==0,"Desert Treasure remote acceptance denied "+life);
                for(uint id=1828;id<=1834;id++){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Desert Treasure forged journal cannot grant progress "+id);}
                talk(p,900071727,171);journal(p);talk(p,4651,1);Check(stage(p)==2 && CountId(p,721873)==1,"Desert Treasure Louis grants tracked vial "+life);
                if(life==0){uint vialId=p.Inventory.Objects.Single(x=>x.ID==721873).UID;ItemHandler.DropItem(new ItemUsage(true){UID=vialId},p);talk(p,4651,33);talk(p,4651,33);Check(CountId(p,721873)==1 && p.Inventory.Objects.Single(x=>x.ID==721873).UID!=vialId,"Desert Treasure ordinary lost vial recovery once");}
                p.BindWorldMap(1001);p.Entity.X=333;p.Entity.Y=612;kill(p,"TombBat");Check(CountId(p,721876)==1,"Desert Treasure combat hook earns key "+life);
                p.Entity.X=90;p.Entity.Y=283;use(p,721873);p.Entity.Teleport(1001,91,283);talk(p,9918281,1);Check(CountId(p,721875)==0 && CountId(p,721876)==1,"Desert Treasure teleport cannot bypass vault step "+life);
                p.Entity.X=90;p.Entity.Y=283;SkyJump(p,91,283);Check(p.Entity.X==91 && p.Entity.Y==283 && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_vault WHERE uid=@uid AND stepped",p.Entity.UID)==1,"Desert Treasure accepted jump unlocks compartment "+life);
                talk(p,9918281,1);Check(CountId(p,721875)==1 && CountId(p,721876)==0,"Desert Treasure ordinary vault click grants box "+life);use(p,721875);talk(p,4651,1);Check(stage(p)==3 && CountId(p,721870)==0,"Desert Treasure box use and Scripture hand-in "+life);journal(p);
                talk(p,4652,12);talk(p,4652,12);Check(stage(p)==4 && CountId(p,721878)==1 && p.Entity.Money==openingMoney-99699,"Desert Treasure ordinary purchase charges once "+life);use(p,721878);talk(p,4652,1);Check(stage(p)==5,"Desert Treasure inspected portrait referral "+life);
                p.Entity.X=339;p.Entity.Y=439;foreach(string name in new[]{"BloodyBat","BloodyBatL108","VampireBat","BloodyBat","VampireBat"})kill(p,name);Check(CountId(p,721877)==5,"Desert Treasure ordinary five essence hunt "+life);
                talk(p,4651,0);use(p,721878);talk(p,4651,1);Check(stage(p)==6 && CountId(p,721874)==1 && CountId(p,721871)==0,"Desert Treasure timed portrait hand-in grants bottle "+life);journal(p);
                p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);p.BindWorldMap(1001);p.Entity.X=727;p.Entity.Y=675;now=now.AddSeconds(4);Call(quest,null,"Tick");var body=p.Map.Entities.Values.Single(x=>x.Name=="JinxTombBat");hit(p,body);Check(CountId(p,721872)==0,"Desert Treasure actual Jinx death requires bottle "+life);use(p,721874);Call(quest,null,"Tick");Check(CountId(p,721872)==1 && !p.Map.Entities.ContainsKey(body.UID),"Desert Treasure ordinary bottle capture and cleanup "+life);
                talk(p,4651,1);Check(stage(p)==7 && CountId(p,721872)==0 && p.Entity.Money==openingMoney-93499,"Desert Treasure complete silver payout less agreed purchase price "+life);journal(p);
                talk(p,900071727,171);talk(p,900071727,171);Check(stage(p)==8 && CountId(p,728249)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(x=>x.ID>=800000 && x.ID<900000 && x.Bound)==1,"Desert Treasure ordinary bound final rewards once "+life);journal(p);
                talk(p,4651,50);talk(p,4651,61);talk(p,4651,65);talk(p,4651,67);Check((int)Call(store,null,"QuizProgress",p)==3,"Desert Treasure optional ordinary quiz after main claim "+life);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage(p)==8 && CountId(p,728249)==1 && p.Entity.Money==openingMoney-93499 && !Fenced(p),"Desert Treasure full ordinary route persists "+life);if(completed!=null)completed(p);
            }
        }finally{now=now.AddHours(1);Call(quest,null,"Tick");clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);vault.SetValue(null,oldVault);}
    }
    static void VerifyDesertTreasureQuiz(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");World(runtime,1000);
        Func<GameState,int,int,bool> answer=(player,q,a)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"AnswerQuiz",player,q,a,(Func<bool>)(()=>true));};
        Func<GameState,int> progress=player=>(int)Call(store,null,"QuizProgress",player);
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)98:(byte)140,life,15));lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));
            p.BindWorldMap(1000);p.Entity.X=466;p.Entity.Y=656;Check(!answer(p,0,1) && progress(p)==-1,"Desert Treasure quiz requires completed relic report "+life);
            Query("UPDATE revora_desert_treasure SET stage=@stage WHERE uid=@uid","stage",life==2?8:7,"uid",(long)p.Entity.UID);p.Entity.X=460;Check(!answer(p,0,1),"Desert Treasure quiz requires Louis proximity "+life);p.Entity.X=466;
            Check(!answer(p,2,1) && !answer(p,0,9) && progress(p)==0,"Desert Treasure quiz rejects skipped and invalid answers "+life);
            Check(answer(p,0,1) && !answer(p,0,1) && progress(p)==1,"Desert Treasure quiz advances once per question "+life);p=ReloadBall(p);Check(progress(p)==1 && !answer(p,1,0) && progress(p)==0,"Desert Treasure wrong answer resets persisted sequence "+life);
            Check(answer(p,0,1) && answer(p,1,2),"Desert Treasure correct quiz sequence resumes "+life);
            byte level=p.Entity.Level;ulong xp=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall/2;
            if(life==0){Query("ALTER TABLE revora_desert_treasure_quiz ADD CONSTRAINT treasure_quiz_fault CHECK(step<>3) NOT VALID");bool failed=false;try{answer(p,2,1);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_treasure_quiz DROP CONSTRAINT treasure_quiz_fault");}p=ReloadBall(p);Check(failed && progress(p)==2 && p.Entity.Level==level && p.Entity.Experience==xp,"Desert Treasure failed quiz receipt rolls back experience");}
            GameState owner=p;Parallel.For(0,4,j=>answer(owner,2,1));ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);
            Check(progress(p)==3 && gained==xp+award,"Desert Treasure quiz grants half EXP Ball once under concurrent answers "+life);
            p=ReloadBall(p);Check(progress(p)==3 && !answer(p,0,1) && !answer(p,2,1),"Desert Treasure paid quiz persists and cannot restart "+life);
        }
    }
    static void VerifyDesertTreasureJinx(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureEncounter",true);Call(store,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,27,1,0,0,DateTimeKind.Utc);
        Func<GameState,Entity> spawn=player=>(Entity)Call(fights,null,"Start",player);
        Func<GameState,uint,bool> capture=(player,id)=>{lock(player.ItemSyncRoot)return (bool)Call(fights,null,"Capture",player,id);};
        Action<GameState,Entity> hit=(player,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(player.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=player.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,110,0,15));p.LoadItemStats();p.BindWorldMap(1001);p.Entity.X=727;p.Entity.Y=675;
            var thief=ReloadBall(WelcomePlayer(town,110,0,15));thief.LoadItemStats();thief.BindWorldMap(1001);thief.Entity.X=727;thief.Entity.Y=675;
            Check(spawn(p)==null,"Desert Treasure Jinx requires earned stage and bottle");lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Query("UPDATE revora_desert_treasure SET stage=6 WHERE uid=@uid","uid",(long)p.Entity.UID);
            var bottle=NestedPack(p,721874,1,true);Check(spawn(p)==null,"Desert Treasure untracked bottle cannot spawn Jinx");Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,721874)","item",(long)bottle.UID,"uid",(long)p.Entity.UID);
            var body=spawn(p);Check(body!=null && body.Name=="JinxTombBat" && body.MaxHitpoints==100000 && spawn(p)==null,"Desert Treasure one owned Jinx with reviewed bat profile");
            uint hp=body.Hitpoints;hit(thief,body);Check(body.Hitpoints==hp && !capture(thief,bottle.UID),"Desert Treasure outsider cannot damage or capture Jinx");Check(!capture(p,bottle.UID),"Desert Treasure living Jinx cannot yield relic");
            hit(p,body);Check(body.Dead && CountId(p,721872)==0 && CountId(p,721874)==1,"Desert Treasure combat death waits for bottle use");
            now=now.AddSeconds(3).AddTicks(10);Call(fights,null,"TryDie",body,p.Entity);Check(!capture(p,bottle.UID),"Desert Treasure repeated death cannot extend expired corpse");Call(fights,null,"Tick");Check(!p.Map.Entities.ContainsKey(body.UID) && CountId(p,721874)==1,"Desert Treasure expiry cleans corpse and preserves bottle");
            body=spawn(p);hit(p,body);p.Entity.X=740;Check(!capture(p,bottle.UID),"Desert Treasure distant bottle use rejected");p.Entity.X=727;
            Query("ALTER TABLE items ADD CONSTRAINT treasure_capture_fault CHECK(id<>721872) NOT VALID");bool failed=false;try{capture(p,bottle.UID);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT treasure_capture_fault");}
            Check(failed && CountId(p,721874)==1 && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_captures WHERE uid=@uid",p.Entity.UID)==0,"Desert Treasure capture failure retains bottle and no receipt");
            while(p.Inventory.Count<40)Pack(p,1088001,1);now=now.AddSeconds(3);Check(capture(p,bottle.UID) && !capture(p,bottle.UID) && CountId(p,721872)==1 && CountId(p,721874)==0 && p.Inventory.Count==40,"Desert Treasure exact corpse deadline replaces bottle in full inventory once");Call(fights,null,"Tick");Check(!p.Map.Entities.ContainsKey(body.UID) && spawn(p)==null,"Desert Treasure captured corpse cleans and cannot spawn surplus relic");p=ReloadBall(p);Check(CountId(p,721872)==1 && p.Inventory.Objects.Single(x=>x.ID==721872).Bound && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_captures WHERE uid=@uid",p.Entity.UID)==1,"Desert Treasure captured relic and receipt persist");
            Query("UPDATE revora_desert_treasure SET stage=6 WHERE uid=@uid","uid",(long)thief.Entity.UID); // No run: initialize a separate cleanup owner explicitly.
            lock(thief.ItemSyncRoot)Call(store,null,"Advance",thief,0,(Func<bool>)(()=>true));Query("UPDATE revora_desert_treasure SET stage=6 WHERE uid=@uid","uid",(long)thief.Entity.UID);var extra=NestedPack(thief,721874,1,true);Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,721874)","item",(long)extra.UID,"uid",(long)thief.Entity.UID);
            var timeout=spawn(thief);now=now.AddMinutes(5);Call(fights,null,"Tick");Check(timeout!=null && !thief.Map.Entities.ContainsKey(timeout.UID),"Desert Treasure encounter times out safely");var replaced=spawn(thief);thief=ReloadBall(thief);Call(fights,null,"Tick");Check(replaced!=null && !thief.Map.Entities.ContainsKey(replaced.UID),"Desert Treasure replacement session cleans old encounter");
        }finally{now=now.AddHours(1);Call(fights,null,"Tick");clock.SetValue(null,oldClock);}
    }
    static void VerifyDesertTreasureRecovery(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");
        Func<GameState,uint,bool> recover=(player,type)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"Recover",player,type,(Func<bool>)(()=>true));};
        var p=ReloadBall(WelcomePlayer(town,100,0,15));lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
        ulong money=p.Entity.Money;uint vial=p.Inventory.Objects.Single(x=>x.ID==721873).UID;
        Check(!recover(p,721873) && !recover(p,721872) && !recover(p,721876),"Desert Treasure recovery cannot copy owned or unearned items");
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)vial);p=ReloadBall(p);Check(!recover(p,721873),"Desert Treasure stored vial cannot be copied");
        var other=ReloadBall(WelcomePlayer(town,100,0,15));Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)other.Entity.UID,"uid",(long)vial);Check(!recover(p,721873),"Desert Treasure transferred vial cannot be copied");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)vial);
        Query("ALTER TABLE revora_desert_treasure_recoveries ADD CONSTRAINT treasure_recovery_fault CHECK(type<>721873) NOT VALID");bool failed=false;try{recover(p,721873);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_desert_treasure_recoveries DROP CONSTRAINT treasure_recovery_fault");}
        Check(failed && CountId(p,721873)==0 && Scalar("SELECT state FROM revora_desert_treasure_materials WHERE item_uid=@uid",vial)==0,"Desert Treasure failed receipt rolls back recovery and entitlement");
        while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!recover(p,721873),"Desert Treasure recovery requires free slot");foreach(var x in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(x,Enums.ItemUse.Remove);
        GameState owner=p;Parallel.For(0,4,j=>recover(owner,721873));Check(CountId(p,721873)==1 && p.Inventory.Objects.Single(x=>x.ID==721873).UID!=vial && Scalar("SELECT state FROM revora_desert_treasure_materials WHERE item_uid=@uid",vial)==2,"Desert Treasure concurrent recovery replaces lost UID once");
        p=ReloadBall(p);Check(!recover(p,721873) && p.Entity.Money==money && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_recoveries WHERE uid=@uid",p.Entity.UID)==1,"Desert Treasure recovered vial and receipt survive reload without payment");
        DateTime until=new DateTime(2026,9,26,1,0,0,DateTimeKind.Utc);
        Query("UPDATE revora_desert_treasure SET stage=5,portrait_until=@until WHERE uid=@uid","until",new DateTimeOffset(until),"uid",(long)p.Entity.UID);
        var real=NestedPack(p,721871,1,true);Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,721871)","item",(long)real.UID,"uid",(long)p.Entity.UID);p.Inventory.Remove(real,Enums.ItemUse.Remove);
        Check(recover(p,721871) && (DateTime)Field(Call(store,null,"Load",p),"PortraitUntil")==until && CountId(p,721871)==1,"Desert Treasure recovery preserves expired portrait deadline");
        Query("UPDATE revora_desert_treasure SET stage=7 WHERE uid=@uid","uid",(long)p.Entity.UID);var bottle=NestedPack(p,721874,1,true);Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,0,721874)","item",(long)bottle.UID,"uid",(long)p.Entity.UID);p.Inventory.Remove(bottle,Enums.ItemUse.Remove);Check(!recover(p,721874),"Desert Treasure completed stage cannot recover obsolete bottle");
    }
    static void VerifyDesertTreasureHunt(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");World(runtime,1001);
        var roll=store.GetField("HuntRoll",Hidden);var vault=store.GetField("VaultRoll",Hidden);var clock=store.GetField("Clock",Hidden);object old=roll.GetValue(null),oldVault=vault.GetValue(null),oldClock=clock.GetValue(null);int sample=0,boxRoll=0;DateTime now=new DateTime(2026,9,26,23,0,0,DateTimeKind.Utc);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,string,uint,bool> use=(p,name,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,id,(Func<bool>)(()=>true));};
        try{roll.SetValue(null,(Func<int,int>)(n=>sample));vault.SetValue(null,(Func<int,int>)(n=>boxRoll));clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,100,0,15));p.BindWorldMap(1001);p.Entity.X=333;p.Entity.Y=612;
            lock(p.ItemSyncRoot){Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));Call(store,null,"Advance",p,1,(Func<bool>)(()=>true));}
            Check(!credit(p,kill(p,"BloodyBat")),"Desert Treasure wrong family cannot give key");sample=50;var miss=kill(p,"TombBat");Check(!credit(p,miss),"Desert Treasure losing key boundary");sample=49;Check(!credit(p,miss),"Desert Treasure losing death cannot reroll");
            var body=kill(p,"TombBatL103");Check(credit(p,body) && !credit(p,body) && CountId(p,721876)==1,"Desert Treasure distinct Tomb Bat key once");
            Check(!credit(p,kill(p,"TombBat")),"Desert Treasure caps active keys");uint vial=p.Inventory.Objects.Single(x=>x.ID==721873).UID;
            Check(!use(p,"RevealVault",vial),"Desert Treasure vial requires vault location");p.Entity.X=91;p.Entity.Y=283;
            Check(use(p,"RevealVault",vial) && !use(p,"RevealVault",vial) && !act(p,"OpenVault"),"Desert Treasure reveal cannot extend and requires accepted step");
            Check(act(p,"StepVault"),"Desert Treasure step persists");p=ReloadBall(p);boxRoll=50;
            Check(!act(p,"OpenVault") && CountId(p,721876)==0 && CountId(p,721875)==0,"Desert Treasure failed compartment spends key once");
            Check(!act(p,"OpenVault"),"Desert Treasure failed compartment cannot replay");Check(credit(p,kill(p,"TombBat")),"Desert Treasure replacement key earned");
            Check(use(p,"RevealVault",vial) && act(p,"StepVault"),"Desert Treasure compartment retry earns step");boxRoll=49;
            Query("ALTER TABLE items ADD CONSTRAINT treasure_box_fault CHECK(id<>721875) NOT VALID");bool failed=false;try{act(p,"OpenVault");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT treasure_box_fault");}
            Check(failed && CountId(p,721876)==1 && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_vault WHERE uid=@uid AND stepped",p.Entity.UID)==1,"Desert Treasure box insert failure restores key and attempt");
            now=now.AddSeconds(60);Check(act(p,"OpenVault") && CountId(p,721876)==0 && CountId(p,721875)==1,"Desert Treasure compartment inclusive deadline awards box");
            uint box=p.Inventory.Objects.Single(x=>x.ID==721875).UID;Check(use(p,"OpenScripture",box) && !use(p,"OpenScripture",box) && CountId(p,721870)==1,"Desert Treasure box yields tracked Scripture once");
            Check(!credit(p,kill(p,"TombBat")),"Desert Treasure completed Scripture blocks extra keys");
            Query("UPDATE revora_desert_treasure SET stage=5 WHERE uid=@uid","uid",(long)p.Entity.UID);
            foreach(string name in new[]{"BloodyBat","BloodyBatL108","VampireBat","BloodyBat","VampireBat"})if(!credit(p,kill(p,name)))throw new Exception("Desert Treasure essence family rejected");
            Check(CountId(p,721877)==5 && !credit(p,kill(p,"BloodyBat")),"Desert Treasure three verified essence families cap at five");p=ReloadBall(p);Check(CountId(p,721877)==5 && p.Inventory.Objects.Where(x=>x.ID==721877).All(x=>x.Bound),"Desert Treasure earned essences persist bound");
        }finally{roll.SetValue(null,old);vault.SetValue(null,oldVault);clock.SetValue(null,oldClock);}
    }
    static void VerifyDesertTreasurePurchase(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");World(runtime,1001);
        var clock=store.GetField("Clock",Hidden);var roll=store.GetField("BidRoll",Hidden);object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,26,22,0,0,DateTimeKind.Utc);int sample=99;
        Func<GameState,int,bool> buy=(p,price)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Purchase",p,price,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>sample));
            foreach(int price in new[]{30000,60000,99999}){
                var p=ReloadBall(WelcomePlayer(town,140,0,15));Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);
                lock(p.ItemSyncRoot)Call(store,null,"Advance",p,0,(Func<bool>)(()=>true));
                Query("UPDATE revora_desert_treasure SET stage=3 WHERE uid=@uid","uid",(long)p.Entity.UID);
                Check(!buy(p,price),"Desert Treasure purchase rejects remote player "+price);p.BindWorldMap(1001);p.Entity.X=310;p.Entity.Y=629;
                Check(!buy(p,1) && CountId(p,721878)==0 && p.Entity.Money==200000,"Desert Treasure rejects unlisted bid "+price);
                if(price!=99999){sample=price==30000?30:60;Check(!buy(p,price) && p.Entity.Money==200000 && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_bids WHERE uid=@uid",p.Entity.UID)==1,"Desert Treasure losing boundary is free and recorded "+price);
                    sample=0;p=ReloadBall(p);Check(!buy(p,price) && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_bids WHERE uid=@uid",p.Entity.UID)==1,"Desert Treasure cooldown survives reconnect "+price);now=now.AddSeconds(5);
                }
                sample=price==30000?29:price==60000?59:99;
                if(price==99999){Query("ALTER TABLE items ADD CONSTRAINT treasure_purchase_fault CHECK(id<>721878) NOT VALID");bool failed=false;try{buy(p,price);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT treasure_purchase_fault");}
                    Check(failed && p.Entity.Money==200000 && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_bids WHERE uid=@uid",p.Entity.UID)==0 && (int)Field(Call(store,null,"Load",p),"Stage")==3,"Desert Treasure failed portrait insert retains payment and stage");
                    while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!buy(p,price) && p.Entity.Money==200000,"Desert Treasure full bag cannot charge");foreach(var x in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(x,Enums.ItemUse.Remove);
                }
                GameState owner=p;Parallel.For(0,4,j=>buy(owner,price));
                Check(CountId(p,721878)==1 && p.Inventory.Objects.Single(x=>x.ID==721878).Bound && p.Entity.Money==200100UL-(ulong)price && Scalar("SELECT COUNT(*) FROM revora_desert_treasure_bids WHERE uid=@uid AND won",p.Entity.UID)==1,"Desert Treasure concurrent purchase charges and pays journal once "+price);
                p=ReloadBall(p);Check(!buy(p,price) && CountId(p,721878)==1 && p.Entity.Money==200100UL-(ulong)price && (int)Field(Call(store,null,"Load",p),"Stage")==4,"Desert Treasure purchase persists and cannot replay "+price);
            }
        }finally{clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyDesertTreasureStore(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertTreasureStore",true);Call(store,null,"Initialize");
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,26,20,0,0,DateTimeKind.Utc);
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        Func<GameState,uint,uint> material=(p,type)=>{var item=NestedPack(p,type,1,true);Query("INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type);return item.UID;};
        Func<GameState,string,uint,bool> use=(p,name,id)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,id,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            Check(!(bool)Call(store,null,"Eligible",ReloadBall(WelcomePlayer(town,97,0,15))),"Desert Treasure rejects underlevel player");
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)98:(byte)140,life,15));ulong money=p.Entity.Money;
                Check(step(p,0) && !step(p,0) && !step(p,2),"Desert Treasure ordered unique acceptance "+life);
                uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
                if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!step(p,1) && stage(p)==1 && p.Entity.Money==money,"Desert Treasure full bag keeps referral unpaid");foreach(var x in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(x,Enums.ItemUse.Remove);}
                Check(step(p,1) && !step(p,1) && CountId(p,721873)==1 && p.Entity.Money==money+100,"Desert Treasure bound vial referral once "+life);
                var fake=NestedPack(p,721870,1,true);Check(!step(p,2),"Desert Treasure rejects untracked scripture "+life);
                material(p,721870);Check(step(p,2) && !step(p,2) && CountId(p,721870)==1,"Desert Treasure consumes only tracked scripture "+life);
                p=ReloadBall(p);Check(stage(p)==3 && (uint)Field(Call(store,null,"Load",p),"Soul")==soul && !step(p,3),"Desert Treasure reload preserves frozen soul and cannot skip purchase "+life);
                // Store fixture: purchase and hunting earning integrations are separate work.
                Query("UPDATE revora_desert_treasure SET stage=4 WHERE uid=@uid","uid",(long)p.Entity.UID);uint portrait=material(p,721878);
                Check(!step(p,4) && use(p,"InspectPortrait",portrait) && !use(p,"InspectPortrait",portrait) && step(p,4),"Desert Treasure requires inspection before referral "+life);
                for(int j=0;j<4;j++)material(p,721877);
                Check(!use(p,"RevealPortrait",portrait) && CountId(p,721877)==4,"Desert Treasure insufficient essence preserved "+life);material(p,721877);
                if(life==0){Query("ALTER TABLE items ADD CONSTRAINT treasure_portrait_fault CHECK(id<>721871) NOT VALID");bool failed=false;try{use(p,"RevealPortrait",portrait);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT treasure_portrait_fault");}Check(failed && CountId(p,721877)==5 && CountId(p,721878)==1 && Field(Call(store,null,"Load",p),"PortraitUntil")==null,"Desert Treasure reveal failure rolls back materials and deadline");}
                Check(use(p,"RevealPortrait",portrait) && CountId(p,721877)==0 && CountId(p,721878)==0 && CountId(p,721871)==1,"Desert Treasure five essences reveal once "+life);
                p=ReloadBall(p);uint real=p.Inventory.Objects.Single(x=>x.ID==721871).UID;DateTime until=(DateTime)Field(Call(store,null,"Load",p),"PortraitUntil");
                Check(until==now.AddSeconds(60) && !use(p,"RevealPortrait",real),"Desert Treasure UTC deadline survives reload and cannot extend "+life);
                now=until.AddTicks(10);Check(!step(p,5) && CountId(p,721871)==1,"Desert Treasure expired portrait remains recoverable "+life);
                for(int j=0;j<5;j++)material(p,721877);Check(use(p,"RevealPortrait",real) && CountId(p,721871)==1,"Desert Treasure expired portrait renews without duplication "+life);
                now=(DateTime)Field(Call(store,null,"Load",p),"PortraitUntil");Check(step(p,5) && !step(p,5) && CountId(p,721871)==0 && CountId(p,721874)==1,"Desert Treasure inclusive deadline grants bottle once "+life);
                p=ReloadBall(p);Check(stage(p)==6 && p.Entity.Money==money+400 && !step(p,6),"Desert Treasure saves four payouts and cannot skip corpse/report "+life);
            }
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyDesertTreasurePack(Map town){ VerifySingleEquipmentPack(town,728249,160199,"Desert Treasure"); }
    static void VerifyDefenseTalismanHouse(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanScenes",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanEncounters",true);
        Call(store,null,"Initialize");Call(scenes,null,"InitializeTravel");World(runtime,1015);Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,28,8,0,0,DateTimeKind.Utc);
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,object> create=p=>{string cwd=Environment.CurrentDirectory;try{Environment.CurrentDirectory=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/defense-talisman-scenes/server"));lock(p.ItemSyncRoot)return Call(scenes,null,"Create",p);}finally{Environment.CurrentDirectory=cwd;}};
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,100,0,15));p.LoadItemStats();p.BindWorldMap(1015);p.Entity.X=667;p.Entity.Y=780;
            Check(act(p,"Accept") && !act(p,"FishingReferral"),"Tower cannot skip first house for fishing");ulong money=p.Entity.Money;
            var scene=create(p);var map=(Map)Field(scene,"Map");
            Check(map.BaseID==1782 && map.Portals.Count==0 && map.Npcs.Count==0,"Tower private house excludes inherited actors and portal routing");
            var outsider=ReloadBall(WelcomePlayer(town,100,0,15));outsider.LoadItemStats();
            outsider.Entity.Teleport(map.ID,45,60);Check(outsider.Entity.MapID!=map.ID,"Tower scene denies unrelated teleport admission");
            lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Tower saved visit permits owned admission");
            Check(p.Entity.MapID==map.ID && !act(p,"HouseReport"),"Tower report requires five earned bandits");
            for(int count=1;count<=5;count++){
                var b=(Entity)Call(fights,null,"Start",p);Check(b!=null && b.Name=="TowerBandit","Tower pending house spawns bandit "+count);
                outsider.BindWorldMap(map.ID);outsider.Entity.X=45;outsider.Entity.Y=60;uint hp=b.Hitpoints;hit(outsider,b);Check(b.Hitpoints==hp,"Tower outsider cannot kill owned bandit "+count);outsider.BindWorldMap(1002);
                hit(p,b);Call(fights,null,"TryDie",b,p.Entity);Call(fights,null,"Tick");Check((int)Call(store,null,"HouseKills",p,(Guid)Field(scene,"Visit"))==count,"Tower death grants one durable bandit credit "+count);
            }
            Check(Call(fights,null,"Start",p)==null && act(p,"HouseReport") && !act(p,"HouseReport") && p.Entity.Money==money+100,"Tower report advances once after five actual kills");
            Call(scenes,null,"Tick");Check(p.Entity.MapID==1015 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT COUNT(*) FROM revora_tower_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Tower completed visit returns and retires owned map");
            Check(act(p,"FishingReferral") && !act(p,"FishingReferral") && p.Entity.Money==money+200,"Tower outside referral pays once and unlocks fishing");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==3 && p.Entity.Money==money+200,"Tower combat-earned fishing referral persists");
            var interrupted=ReloadBall(WelcomePlayer(town,100,0,15));interrupted.LoadItemStats();act(interrupted,"Accept");var pending=create(interrupted);var pendingMap=(Map)Field(pending,"Map");
            lock(interrupted.ItemSyncRoot)Call(scenes,null,"Enter",pending,interrupted,(Func<bool>)(()=>true));now=now.AddMinutes(10);Call(scenes,null,"Tick");
            Check(interrupted.Entity.MapID==1015 && !Kernel.Maps.ContainsKey(pendingMap.ID) && (int)Field(Call(store,null,"Load",interrupted),"Stage")==1,"Tower expired house returns without unearned referral");
            var resumed=create(interrupted);var resumedMap=(Map)Field(resumed,"Map");lock(interrupted.ItemSyncRoot)Call(scenes,null,"Enter",resumed,interrupted,(Func<bool>)(()=>true));
            var left=(Entity)Call(fights,null,"Start",interrupted);interrupted=ReloadBall(interrupted);Call(scenes,null,"RecoverLogin",interrupted);Call(scenes,null,"Tick");
            Check(left!=null && (interrupted.Entity.MapID==1015 || interrupted.Entity.MapID==1002) && !Kernel.Maps.ContainsKey(resumedMap.ID) && Scalar("SELECT COUNT(*) FROM revora_tower_visits WHERE uid=@uid AND active",interrupted.Entity.UID)==0,"Tower reconnect returns player and removes abandoned scene/actor");
            Call(scenes,null,"RecoverLogin",interrupted);Check((int)Field(Call(store,null,"Load",interrupted),"Stage")==1 && !act(interrupted,"FishingReferral"),"Tower repeated recovery cannot grant unearned progress");
        }finally{now=now.AddHours(1);Call(scenes,null,"Tick");Call(fights,null,"Tick");clock.SetValue(null,prior);}
    }
    static void VerifyDefenseTalismanQuest(string runtime,Map town,bool fanOpening=false,bool fanPublic=false,GameState continuing=null,Action<GameState> completed=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanStore",true);
        var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanScenes",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanEncounters",true);
        World(runtime,1000);World(runtime,1015);string previous=Environment.CurrentDirectory;
        Call(quest,null,"Initialize");Check(!(bool)quest.GetProperty("Enabled",Hidden).GetValue(null,null) && (bool)Call(quest,null,"Protect",520u),"Tower missing installed scenes keep activation off and legacy completion blocked");
        string root=System.IO.Path.GetFullPath(System.IO.Path.Combine(runtime,"../../obj/chi-refill-validation/tower-route"));string maps=System.IO.Path.Combine(root,"database/maps");System.IO.Directory.CreateDirectory(maps);
        foreach(int id in new[]{1000,1015,1782,1783,1784,1785,1786,1787}.Concat((fanOpening || fanPublic)?new[]{1025,1011,3937}:new int[0])){
            string from=id>=1782 && id<=1784?System.IO.Path.Combine(runtime,"../../obj/chi-refill-validation/defense-talisman-scenes/server/database/maps",id+".map"):System.IO.Path.Combine(runtime,"database/maps",id+".map");System.IO.File.Copy(from,System.IO.Path.Combine(maps,id+".map"),true);
        }
        var clock=store.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,28,1,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_defense_talisman WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc!=4567 && npc!=4568 && npc!=4574 && npc!=4578)p.BindWorldMap(npc==4572 || npc==4573?(ushort)1000:(ushort)1015);var n=p.Map.Npcs[npc];p.Entity.X=(ushort)(n.X+2);p.Entity.Y=n.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,type)=>ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==type),p);
        Action tick=()=>{now=now.AddSeconds(2);Call(quest,null,"Tick");};
        Action<GameState> kill=p=>{var b=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9905200 && e.MonsterInfo.ID<9905211 && !e.Dead);uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);tick();};
        Action<GameState> journal=p=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);int st=stage(p);var own=rows.Where(x=>(uint)x.UID>=520 && (uint)x.UID<=528).ToArray();Check(own.Length==9 && own.All(x=>x.Status==(st>(int)x.UID-519?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)x.UID-519?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Tower ordinary journal matches earned stage "+st);};
        try{Environment.CurrentDirectory=root;clock.SetValue(null,(Func<DateTime>)(()=>now));Call(quest,null,"Initialize");if(fanOpening)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanStore",true),null,"Initialize");
            var low=ReloadBall(WelcomePlayer(town,99,0,15));talk(low,4565,1);Check(stage(low)==0,"Tower ordinary underlevel admission denied");
            for(byte life=0;life<3;life++){
                var p=life==1 && continuing!=null?continuing:ReloadBall(WelcomePlayer(town,life==0?(byte)100:(byte)140,life,15));if(life!=1 || continuing==null){Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);}p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();
                p.BindWorldMap(1015);p.Entity.X=300;p.Entity.Y=300;p.ActiveNpc=4565;Npcs.GetDialog(new NpcRequest(true){NpcID=4565,OptionID=1},p);Check(stage(p)==0,"Tower remote ordinary admission denied "+life);
                for(uint id=520;id<=528;id++){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);}Check(stage(p)==0,"Tower client journal cannot forge progress "+life);
                talk(p,4565,1);journal(p);talk(p,4565,1);Check(p.Map.BaseID==1782,"Tower normal dialogue enters owned first house "+life);
                if(life==0){p.Entity.X=40;p.Entity.Y=57;tick();kill(p);ushort abandoned=p.Entity.MapID;var stale=p;p=ReloadBall(p);Call(quest,null,"OnLogin",p);tick();
                    Check(stage(p)==1 && !Kernel.Maps.ContainsKey(abandoned) && Scalar("SELECT COUNT(*) FROM revora_tower_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Tower reconnect retires abandoned house without granting referral");
                    stale.ActiveNpc=4567;Npcs.GetDialog(new NpcRequest(true){NpcID=4567,OptionID=1},stale);Check(stage(p)==1,"Tower replaced session cannot report abandoned combat");talk(p,4565,1);
                    talk(p,4567,1);Check(stage(p)==1,"Tower reentry requires fresh house kills");}
                p.Entity.X=40;p.Entity.Y=57;tick();for(int i=0;i<5;i++)kill(p);talk(p,4567,1);talk(p,4566,1);Check(stage(p)==3,"Tower ordinary combat and report earn fishing referral "+life);journal(p);
                for(int i=0;i<3;i++){talk(p,4579,1);p.Entity.X=310;p.Entity.Y=380;use(p,721779);kill(p);}talk(p,4569,1);Check(CountId(p,721780)==1 && CountId(p,721778)==0,"Tower ordinary net purchases and fishing earn poise "+life);
                talk(p,4575,1);talk(p,4576,1);talk(p,4577,1);Check(stage(p)==4 && p.Map.BaseID==1784,"Tower ordinary three-door route enters Hut "+life);talk(p,4568,1);talk(p,4568,2);
                p.BindWorldMap(1015);p.Entity.X=400;p.Entity.Y=650;now=now.AddMinutes(10-now.Minute%10);tick();kill(p);talk(p,4577,1);talk(p,4568,1);Check(stage(p)==5 && CountId(p,721777)==1,"Tower ordinary Shawn combat and Ruan report earn splinter "+life);
                talk(p,4572,1);p.Entity.X=166;p.Entity.Y=226;tick();kill(p);kill(p);talk(p,4566,1);talk(p,4566,1);Check(stage(p)==7 && p.Map.BaseID==1783,"Tower ordinary desert combat and handkerchief enter second house "+life);
                if(life==1){p.Entity.X=40;p.Entity.Y=57;tick();kill(p);ushort expired=p.Entity.MapID;now=now.AddMinutes(11);tick();
                    Check(stage(p)==7 && CountId(p,721786)==0 && p.Entity.MapID==1015 && !Kernel.Maps.ContainsKey(expired),"Tower expired ambush returns owner without unearned invocation");talk(p,4566,1);talk(p,4574,1);Check(stage(p)==7,"Tower new ambush cannot reuse expired visit kills");}
                p.Entity.X=40;p.Entity.Y=57;tick();for(int i=0;i<5;i++)kill(p);talk(p,4574,1);talk(p,4573,1);Check(stage(p)==8 && p.Map.BaseID==1785,"Tower ordinary rats invocation and Ghoul enter dungeon "+life);
                ushort[,] stands={{96,124},{70,64},{113,126},{62,85}};
                for(int i=0;i<4;i++){p.Entity.X=stands[i,0];p.Entity.Y=stands[i,1];tick();kill(p);if(life==2 && i==2){ushort abandoned=p.Entity.MapID;p=ReloadBall(p);Call(quest,null,"OnLogin",p);tick();Check(stage(p)==8 && CountId(p,721789)==1 && !Kernel.Maps.ContainsKey(abandoned),"Tower dungeon reconnect retains earned weapon and cleans scene");talk(p,4573,1);Check(p.Map.BaseID==1786 && CountId(p,721789)==1,"Tower ordinary Ghoul reentry resumes saved floor and weapon credit");}if(i==1 || i==3){p.Entity.X=(ushort)(i==1?70:62);p.Entity.Y=(ushort)(i==1?56:77);use(p,721786);}}
                Check(p.Map.BaseID==1787,"Tower ordinary invocation advances both dungeon gates "+life);tick();kill(p);
                for(byte i=10;i<14;i++)talk(p,4578,i);talk(p,4578,1);Check(stage(p)==9 && CountId(p,721785)==1 && p.Entity.MapID==1015,"Tower ordinary Cauldron reconstructs and returns owner "+life);journal(p);
                if(fanOpening){p.Entity.X=687;p.Entity.Y=599;lock(p.ItemSyncRoot)Check(!(bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanStore",true),null,"Accept",p,(Func<bool>)(()=>true)),"Fan rejects reconstruction before Tower final claim "+life);}
                uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");talk(p,4565,1);talk(p,4565,1);Check(stage(p)==10 && CountId(p,202008)==1 && CountId(p,soul)==1 && p.Inventory.Objects.Where(i=>i.ID==202008 || i.ID==soul).All(i=>i.Bound),"Tower complete ordinary route awards bound rewards once "+life);journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage(p)==10 && CountId(p,202008)==1 && CountId(p,soul)==1,"Tower ordinary final rewards survive login "+life);if(fanOpening)VerifyAttackTalismanOpening(runtime,town,p,life);if(fanPublic)VerifyAttackTalismanPublic(runtime,town,p,life,actor=>p=actor);if(completed!=null)completed(p);
            }
        }finally{now=now.AddHours(1);Call(quest,null,"Tick");clock.SetValue(null,oldClock);Environment.CurrentDirectory=previous;}
    }
    static void VerifyAttackTalismanPublic(string runtime,Map town,GameState p,byte life,Action<GameState> completed=null){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanScenes",true);
        if(!(bool)quest.GetProperty("Enabled",Hidden).GetValue(null,null))World(runtime,1011);Call(quest,null,"Initialize");Check((bool)quest.GetProperty("Enabled",Hidden).GetValue(null,null),"Fan public route initialized "+life);
        var clock=store.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);var roll=store.GetField("HuntRoll",Hidden);object oldRoll=roll.GetValue(null);DateTime now=new DateTime(2026,9,29,3+life*2,0,0,DateTimeKind.Utc);
        Func<int> stage=()=> (int)Field(Call(store,null,"Load",p),"Stage");
        Action<uint,byte> talk=(npc,option)=>{if(npc==4565 || npc>=9910410 && npc<=9910414)p.BindWorldMap(1015);else if(npc==4583)p.BindWorldMap(1000);else if(npc==4581 || npc==4582 || npc==9910430)p.BindWorldMap(1011);var n=p.Map.Npcs[npc];p.Entity.X=(ushort)(n.X+2);p.Entity.Y=n.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action tick=()=>{now=now.AddSeconds(2);Call(quest,null,"Tick");};
        Action<uint> use=type=>ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==type),p);
        Action kill=()=>{tick();var body=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID>=9905400 && b.MonsterInfo.ID<9905407 && !b.Dead);uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);tick();};
        Action journal=()=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(r=>(uint)r.UID>=400 && (uint)r.UID<=409).ToArray();int st=stage();Check(own.Length==10 && own.All(r=>r.Status==(st>(int)r.UID-399?MsgTaskStatus.QuestData.QuestStatus.Finished:st==(int)r.UID-399?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Fan ordinary journal tracks stage "+st+" life "+life);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>0));ulong money=p.Entity.Money;
            p.BindWorldMap(1015);p.Entity.X=300;p.Entity.Y=300;p.ActiveNpc=4565;Npcs.GetDialog(new NpcRequest(true){NpcID=4565,OptionID=81},p);Check(stage()==0,"Fan remote Sugar option cannot admit "+life);
            for(uint id=400;id<=409;id++){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);}Check(stage()==0,"Fan client journal cannot forge final reward "+life);
            talk(4565,0);talk(4565,80);talk(4565,81);Check(stage()==1 && CountId(p,710218)==1,"Fan Sugar menu admits completed-Tower ordinary player "+life);journal();
            talk(4582,1);talk(4581,1);talk(4581,1);Check(stage()==2 && CountId(p,721800)==1 && p.Entity.Money==money+100-10000,"Fan Wine Kiddo charges ordinary purchase once "+life);
            p.Entity.X=338;p.Entity.Y=126;for(int i=0;i<5;i++){var b=HuntMonster(p,"WingedSnake");b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);}
            Check(CountId(p,710220)==5,"Fan ordinary snake deaths award tracked meat through production hook "+life);talk(4582,1);talk(4565,81);talk(4583,1);Check(stage()==5 && CountId(p,721798)==1,"Fan ordinary meal and referrals earn cave admission "+life);journal();
            talk(4583,2);Check(p.Map.BaseID==1025,"Fan Kunlun dialogue enters owned cave "+life);p.Entity.X=54;p.Entity.Y=50;kill();p.Entity.X=58;p.Entity.Y=50;kill();use(721806);Check(p.Entity.MapID==1000 && CountId(p,721803)==1,"Fan earned Kettle ordinary item use returns with Fang "+life);
            talk(4583,1);use(721802);use(721804);talk(4582,1);Check(stage()==7,"Fan ordinary liquid recipe and poem report unlock ambush "+life);
            p.Entity.X=790;p.Entity.Y=460;for(int i=0;i<5;i++)kill();talk(4582,1);Check(stage()==8 && CountId(p,721799)==1 && CountId(p,710219)==1,"Fan ordinary ambush earns incense and hairpin "+life);journal();
            talk(9910430,1);Check(p.Map.BaseID==1011 && p.Entity.MapID!=1011,"Fan Lake Keeper opens owned lake "+life);p.Entity.X=475;p.Entity.Y=844;use(721799);talk(9910401,1);p.Entity.X=477;p.Entity.Y=844;kill();Check(p.Entity.MapID==1011 && CountId(p,710216)==1,"Fan ordinary spirit and Viper death return with Purple Eye "+life);
            talk(4582,1);int site=(int)Field(Call(store,null,"Load",p),"CourtesanSite");var points=(ushort[,])store.GetField("CourtesanSites",Hidden).GetValue(null);p.BindWorldMap(1015);p.Entity.X=(ushort)(points[site,0]-2);p.Entity.Y=points[site,1];kill();Check(CountId(p,710217)==1,"Fan ordinary tick spawns and credits selected Courtesan "+life);
            for(uint npc=9910410;npc<9910414;npc++)talk(npc,1);talk(9910414,1);Check(stage()==10 && p.Map.BaseID==3937,"Fan ordinary pillar conversations unlock garden "+life);journal();
            int winning=(int)Field(Call(store,null,"Load",p),"BoxWinner");int wrong=(winning+1)%3;talk(9910420u+(uint)wrong,1);p.Entity.X=(ushort)(52+wrong*6);p.Entity.Y=53;kill();talk(9910420u+(uint)winning,1);Check(CountId(p,710221)==1,"Fan ordinary wrong box ghost and winning box earn quest fan "+life);
            talk(9910423,1);Check(p.Entity.MapID==1015 && CountId(p,710222)==1,"Fan Mr Loneliness ordinary dialogue exchanges book and returns "+life);
            p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage()==10 && CountId(p,710222)==1,"Fan ordinary login restores final hand-in "+life);
            uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");int prior=CountId(p,soul);talk(4565,81);talk(4565,81);Check(stage()==11 && CountId(p,201009)==1 && CountId(p,soul)==prior+1 && p.Entity.Money==money-20000+900,"Fan ordinary Sugar claim grants bound final rewards once "+life);journal();
            p=ReloadBall(p);Call(quest,null,"OnLogin",p);Check(stage()==11 && p.Inventory.Objects.Single(i=>i.ID==201009).Bound && p.Inventory.Objects.Where(i=>i.ID==soul).All(i=>i.Bound),"Fan ordinary final rewards survive production loader "+life);if(completed!=null)completed(p);
        }finally{now=now.AddHours(1);Call(quest,null,"Tick");clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyAttackTalismanOpening(string runtime,Map town,GameState p,byte life){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanStore",true);Call(store,null,"Initialize");World(runtime,1011);
        Func<string,bool> act=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<int,bool> refer=stage=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Referral",p,stage,(Func<bool>)(()=>true));};
        Func<Entity,bool> credit=b=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<string,Entity> kill=name=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        var roll=store.GetField("HuntRoll",Hidden);object oldRoll=roll.GetValue(null);int sample=0;
        try{roll.SetValue(null,(Func<int,int>)(max=>sample));p.BindWorldMap(1015);p.Entity.X=687;p.Entity.Y=599;
            ulong before=p.Entity.Money;Check(act("Accept") && !act("Accept") && CountId(p,710218)==1 && p.Entity.Money==before,"Fan completed Tower admits once and grants bound Jadeite without charge "+life);
            uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");p=ReloadBall(p);Check((uint)Field(Call(store,null,"Load",p),"Soul")==soul && CountId(p,710218)==1 && p.Inventory.Objects.Single(i=>i.ID==710218).Bound,"Fan frozen reward and Jadeite survive reload "+life);
            Check(!refer(1) && !act("BuyWine"),"Fan referrals and wine reject remote requests "+life);p.BindWorldMap(1011);p.Entity.X=790;p.Entity.Y=456;Check(refer(1) && !refer(1) && p.Entity.Money==before+100,"Fan first referral persists and pays once "+life);
            p.Entity.X=184;p.Entity.Y=280;
            if(life==0){
                while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!act("BuyWine") && p.Entity.Money==before+100,"Fan full bag cannot charge wine");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT fan_wine_fault CHECK(id<>721800) NOT VALID");bool failed=false;try{act("BuyWine");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT fan_wine_fault");}
                Check(failed && p.Entity.Money==before+100 && CountId(p,721800)==0 && Scalar("SELECT COUNT(*) FROM revora_attack_talisman_purchases WHERE uid=@uid",p.Entity.UID)==0,"Fan failed wine insert rolls back price and receipt");
            }
            Check(act("BuyWine") && !act("BuyWine") && p.Entity.Money==before+100-10000 && CountId(p,721800)==1,"Fan wine costs exactly ten thousand silver once "+life);
            if(life==0){var lost=p.Inventory.Objects.Single(i=>i.ID==721800);p.Inventory.Remove(lost,Enums.ItemUse.Remove);Check(!act("BuyWine"),"Fan lost wine cannot trigger a second purchase");
                lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721800u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721800u,(Func<bool>)(()=>true)) && p.Entity.Money==before+100-10000,"Fan lost paid wine recovers once without another charge");}
            p.Entity.X=338;p.Entity.Y=126;
            if(life==0){sample=99;var missed=kill("WingedSnake");Check(!credit(missed),"Fan missed meat roll grants nothing");sample=0;Check(!credit(missed),"Fan repeated death cannot reroll failed meat");
                Query("ALTER TABLE items ADD CONSTRAINT fan_meat_fault CHECK(id<>710220) NOT VALID");bool failed=false;var fault=kill("WingedSnake");try{credit(fault);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT fan_meat_fault");}
                Check(failed && CountId(p,710220)==0 && Scalar("SELECT COUNT(*) FROM revora_attack_talisman_materials WHERE uid=@uid AND type=710220",p.Entity.UID)==0 && !credit(fault),"Fan failed meat write leaves no entitlement and cannot replay death");
                var wrong=kill("WingedSnakeMsgr");Check(!credit(wrong),"Fan messenger variant is not a qualifying meat source");
                while(p.Inventory.Count<40)Pack(p,1088001,1);var full=kill("WingedSnake");Check(!credit(full),"Fan full bag cannot create hidden meat");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);Check(!credit(full),"Fan freeing space cannot replay old death");
            }
            for(int i=0;i<5;i++){var b=kill(i%2==0?"WingedSnake":"WingedSnakeL28");Check(credit(b) && !credit(b) && CountId(p,710220)==i+1,"Fan actual snake death earns one tracked meat "+i+" life "+life);}
            Check(!credit(kill("WingedSnake")) && CountId(p,710220)==5,"Fan meat entitlements cap at five "+life);
            p=ReloadBall(p);p.BindWorldMap(1011);p.Entity.X=790;p.Entity.Y=456;Check(CountId(p,721800)==1 && CountId(p,710220)==5 && p.Inventory.Objects.Where(i=>i.ID==710220 || i.ID==721800).All(i=>i.Bound),"Fan purchased wine and earned meats persist bound "+life);
            if(life==0){Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_silk_fault CHECK(stage<>3) NOT VALID");bool failed=false;try{act("ExchangeMeal");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_silk_fault");}
                Check(failed && CountId(p,721800)==1 && CountId(p,710220)==5 && CountId(p,721798)==0,"Fan failed Silk exchange preserves all paid and earned inputs");while(p.Inventory.Count<40)Pack(p,1088001,1);}
            Check(act("ExchangeMeal") && !act("ExchangeMeal") && CountId(p,721800)==0 && CountId(p,710220)==0 && CountId(p,721798)==1 && p.Entity.Money==before+200-10000,"Fan meal exchange consumes six tracked inputs and awards Silk once "+life);
            foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            p.BindWorldMap(1015);p.Entity.X=687;p.Entity.Y=599;Check(refer(3) && !refer(3),"Fan Sugar Tang referral accepts earned Silk once "+life);
            p.BindWorldMap(1000);p.Entity.X=492;p.Entity.Y=618;Check(refer(4) && !refer(4),"Fan Kunlun referral advances once with tracked Silk "+life);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==5 && CountId(p,721798)==1 && CountId(p,710218)==1 && p.Entity.Money==before+400-10000,"Fan cave-ready checkpoint retains materials and exact balance "+life);VerifyAttackTalismanCave(runtime,town,p,life);
        }finally{roll.SetValue(null,oldRoll);}
    }
    static void VerifyAttackTalismanCave(string runtime,Map town,GameState p,byte life){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanScenes",true);var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackTalismanEncounters",true);Call(scenes,null,"InitializeTravel");
        var clock=store.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=new DateTime(2026,9,28,2,0,0,DateTimeKind.Utc);object scene=null;var gardenRoll=store.GetField("GardenRoll",Hidden);object oldGardenRoll=gardenRoll.GetValue(null);
        Func<object> create=()=>{lock(p.ItemSyncRoot)return Call(scenes,null,"Create",p);};Func<object,bool> enter=v=>{lock(p.ItemSyncRoot)return (bool)Call(scenes,null,"Enter",v,p,(Func<bool>)(()=>true));};Action<object> leave=v=>{lock(p.ItemSyncRoot)Call(scenes,null,"Exit",v,p);};
        Action home=()=>{p.BindWorldMap(1000);p.Entity.X=492;p.Entity.Y=618;};
        Action<GameState,Entity> hit=(player,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(player.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=player.Entity.UID,Attacked=b.UID},ref damage,null);};
        Func<bool> kettle=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"UseKettle",p,(Func<bool>)(()=>true));};
        try{gardenRoll.SetValue(null,(Func<int,int>)(max=>life));clock.SetValue(null,(Func<DateTime>)(()=>now));home();ulong before=p.Entity.Money;
            if(life==0){p.Entity.X=450;scene=create();Check(!enter(scene) && p.Entity.Money==before,"Fan remote cave admission cannot charge");leave(scene);home();
                Query("ALTER TABLE revora_fan_visits ADD CONSTRAINT fan_visit_fault CHECK(NOT active) NOT VALID");bool failed=false;scene=create();try{enter(scene);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_fan_visits DROP CONSTRAINT fan_visit_fault");}
                Check(failed && !(bool)Field(Call(store,null,"Load",p),"CavePaid") && p.Entity.Money==before && Scalar("SELECT COUNT(*) FROM revora_fan_visits WHERE uid=@uid",p.Entity.UID)==0,"Fan failed visit insert rolls back permit payment and receipt");leave(scene);}
            scene=create();Check(enter(scene) && enter(scene) && p.Entity.Money==before-10000 && (bool)Field(Call(store,null,"Load",p),"CavePaid"),"Fan owned cave permit charges exactly once "+life);
            var map=(Map)Field(scene,"Map");Check(p.Map.BaseID==1025 && p.Entity.X==52 && p.Entity.Y==50 && map.Portals.Count==0 && map.Entities.Count==0,"Fan cave uses verified entrance and excludes inherited actors/portals "+life);
            var outsider=ReloadBall(WelcomePlayer(town,100,0,15));outsider.LoadItemStats();outsider.Entity.Teleport(map.ID,52,50);Check(outsider.Entity.MapID!=map.ID,"Fan private cave rejects unrelated teleport "+life);
            if(life==0){now=now.AddMinutes(11);Call(scenes,null,"Tick");Check(!Kernel.Maps.ContainsKey(map.ID) && p.Entity.MapID==1000 && CountId(p,721803)==0,"Fan expired cave safely returns without free Fang");home();scene=create();Check(enter(scene) && p.Entity.Money==before-10000,"Fan expired paid permit reenters without second charge");}
            for(int kind=0;kind<2;kind++){
                p.Entity.X=(ushort)(kind==0?52:58);p.Entity.Y=50;var body=(Entity)Call(fights,null,"Start",p);Check(body!=null && body.MonsterInfo.ID==9905400+kind,"Fan cave spawns correct earned encounter "+kind+" life "+life);
                outsider.BindWorldMap(p.Entity.MapID);outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;uint hp=body.Hitpoints;hit(outsider,body);Check(body.Hitpoints==hp,"Fan outsider cannot steal cave material "+kind+" life "+life);outsider.BindWorldMap(1002);
                if(life==0 && kind==0){while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,body);Call(fights,null,"Tick");Check(CountId(p,721803)==0 && Scalar("SELECT COUNT(*) FROM revora_fan_cave_kills WHERE uid=@uid",p.Entity.UID)==0,"Fan full-bag cave kill preserves fresh encounter reward");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);body=(Entity)Call(fights,null,"Start",p);}
                hit(p,body);Call(fights,null,"TryDie",body,p.Entity);Call(fights,null,"Tick");Check(CountId(p,kind==0?721803u:721806u)==1 && Scalar("SELECT COUNT(*) FROM revora_fan_cave_kills WHERE uid=@uid",p.Entity.UID)==kind+1,"Fan actual owned cave kill earns one durable material "+kind+" life "+life);
                if(life==1 && kind==0){ushort abandoned=p.Entity.MapID;p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check(!Kernel.Maps.ContainsKey(abandoned) && CountId(p,721803)==1 && p.Entity.Money==before-10000,"Fan reconnect keeps Fang and paid permit while retiring scene");home();scene=create();Check(enter(scene) && p.Entity.Money==before-10000,"Fan reconnect reenters paid cave without duplicate charge");}
            }
            if(life==0){Query("ALTER TABLE revora_attack_talisman_materials ADD CONSTRAINT fan_kettle_fault CHECK(type<>721806 OR state<>1) NOT VALID");bool failed=false;try{kettle();}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman_materials DROP CONSTRAINT fan_kettle_fault");}Check(failed && CountId(p,721806)==1 && !(bool)Field(scene,"Completed"),"Fan failed kettle consumption preserves item and owned scene");}
            Check(kettle() && !kettle() && CountId(p,721806)==0,"Fan earned kettle consumes once "+life);Call(scenes,null,"Tick");Check(p.Entity.MapID==1000 && p.Entity.X==492 && p.Entity.Y==618 && CountId(p,721803)==1,"Fan kettle returns owner safely with earned Fang "+life);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==5 && (bool)Field(Call(store,null,"Load",p),"CavePaid") && CountId(p,721803)==1 && p.Inventory.Objects.Single(i=>i.ID==721803).Bound && p.Entity.Money==before-10000,"Fan earned Fang permit and exact balance persist "+life);
            Func<string,bool> act=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
            Func<uint,bool> transform=uid=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Transform",p,uid,(Func<bool>)(()=>true));};
            p.Entity.X=450;Check(!act("ClaimLiquid"),"Fan liquid requires Kunlun proximity "+life);home();ulong recipeSilver=p.Entity.Money;
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!act("ClaimLiquid") && CountId(p,721802)==0,"Fan full bag cannot create hidden liquid");var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);}
            Check(act("ClaimLiquid") && !act("ClaimLiquid") && CountId(p,721802)==1 && p.Entity.Money==recipeSilver,"Fan earned Fang claims one free tracked Kunlun Liquid "+life);
            if(life==0){var liquid=p.Inventory.Objects.Single(i=>i.ID==721802);p.Inventory.Remove(liquid,Enums.ItemUse.Remove);Check(!act("ClaimLiquid"),"Fan missing liquid cannot be claimed a second time");lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721802u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721802u,(Func<bool>)(()=>true)),"Fan lost earned liquid recovers once");
                Query("ALTER TABLE items ADD CONSTRAINT fan_water_fault CHECK(id<>721804) NOT VALID");bool failed=false;try{transform(p.Inventory.Objects.Single(i=>i.ID==721802).UID);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT fan_water_fault");}
                Check(failed && CountId(p,721803)==1 && CountId(p,721802)==1 && CountId(p,721804)==0 && (int)Field(Call(store,null,"Load",p),"Alchemy")==1,"Fan failed water output preserves Fang Liquid and recipe step");
                var fang=p.Inventory.Objects.Single(i=>i.ID==721803);fang.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)fang.UID);Check(!transform(p.Inventory.Objects.Single(i=>i.ID==721802).UID),"Fan locked ingredient prevents mixing");fang.Lock=0;Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)fang.UID);
            }
            uint mixed=p.Inventory.Objects.Single(i=>i.ID==(life%2==0?721802u:721803u)).UID;int mixBag=p.Inventory.Count;
            Check(transform(mixed) && !transform(mixed) && p.Inventory.Count==mixBag-1 && CountId(p,721803)==0 && CountId(p,721802)==0 && CountId(p,721804)==1,"Fan either ingredient mixes into one bound Sacred Water "+life);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Alchemy")==2 && CountId(p,721804)==1 && CountId(p,721798)==1,"Fan mixed water and retained Silk persist "+life);
            uint water=p.Inventory.Objects.Single(i=>i.ID==721804).UID;
            if(life==0){Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_dye_fault CHECK(stage<>6) NOT VALID");bool failed=false;try{transform(water);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_dye_fault");}
                Check(failed && CountId(p,721804)==1 && CountId(p,721798)==1 && CountId(p,721801)==0 && p.Entity.Money==recipeSilver && (int)Field(Call(store,null,"Load",p),"Alchemy")==2,"Fan failed dye stage preserves ingredients balance and recipe step");}
            Check(transform(water) && !transform(water) && CountId(p,721804)==0 && CountId(p,721798)==0 && CountId(p,721801)==1 && p.Entity.Money==recipeSilver+100,"Fan dye consumes water and Silk for Poem Silk and one stage payment "+life);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721803u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721798u,(Func<bool>)(()=>true)),"Fan consumed ingredients cannot recover after dye "+life);
            Check(!act("ReportPoem"),"Fan remote poem report is rejected "+life);p.BindWorldMap(1011);p.Entity.X=790;p.Entity.Y=456;
            Check(act("ReportPoem") && !act("ReportPoem") && p.Entity.Money==recipeSilver+200,"Fan earned poem report advances once toward ambush "+life);
            foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==7 && CountId(p,721801)==1 && CountId(p,710218)==1 && p.Inventory.Objects.Single(i=>i.ID==721801).Bound,"Fan ambush checkpoint and bound Poem Silk persist "+life);
            p.BindWorldMap(1011);p.Entity.X=790;p.Entity.Y=456;Check(!act("ClaimIncense"),"Fan cannot claim incense before owned ambush "+life);
            for(int i=0;i<5;i++){var claw=(Entity)Call(fights,null,"Start",p);Check(claw!=null && claw.Name=="CrimsonClaw","Fan owned Crimson Claw appears "+i+" life "+life);outsider.BindWorldMap(1011);outsider.Entity.X=790;outsider.Entity.Y=456;uint hp=claw.Hitpoints;hit(outsider,claw);Check(claw.Hitpoints==hp,"Fan outsider cannot steal ambush credit "+i+" life "+life);outsider.BindWorldMap(1002);
                hit(p,claw);Call(fights,null,"TryDie",claw,p.Entity);Call(fights,null,"Tick");Check(Scalar("SELECT COUNT(*) FROM revora_fan_ambush_kills WHERE uid=@uid",p.Entity.UID)==i+1,"Fan actual ambush death records once "+i+" life "+life);
                if(life==1 && i==1){p=ReloadBall(p);p.BindWorldMap(1011);p.Entity.X=790;p.Entity.Y=456;Check(Scalar("SELECT COUNT(*) FROM revora_fan_ambush_kills WHERE uid=@uid",p.Entity.UID)==2,"Fan partial public ambush progress persists across replacement session");}
            }
            Check(Call(fights,null,"Start",p)==null,"Fan completed ambush cannot spawn extra reward actors "+life);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!act("ClaimIncense") && CountId(p,721801)==1,"Fan full bag preserves completed ambush claim");var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_incense_fault CHECK(stage<>8) NOT VALID");bool failed=false;try{act("ClaimIncense");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_incense_fault");}
                Check(failed && CountId(p,721801)==1 && CountId(p,721799)==0 && CountId(p,710219)==0 && p.Entity.Money==recipeSilver+200,"Fan failed ambush claim preserves poem and both reward slots");}
            Check(act("ClaimIncense") && !act("ClaimIncense") && CountId(p,721801)==0 && CountId(p,721799)==1 && CountId(p,710219)==1 && p.Entity.Money==recipeSilver+300,"Fan five earned kills exchange poem for incense hairpin and one payment "+life);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==8 && p.Inventory.Objects.Where(i=>i.ID==721799 || i.ID==710219).Count()==2 && p.Inventory.Objects.Where(i=>i.ID==721799 || i.ID==710219).All(i=>i.Bound),"Fan lake-ready checkpoint and both bound materials persist "+life);
            Func<object> lake=()=>{lock(p.ItemSyncRoot)return Call(scenes,null,"CreateLake",p);};
            Action shore=()=>{p.BindWorldMap(1011);p.Entity.X=471;p.Entity.Y=844;};
            Func<uint,bool> burn=uid=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"BurnIncense",p,uid,(Func<bool>)(()=>true));};
            uint incense=p.Inventory.Objects.Single(i=>i.ID==721799).UID;
            Check(!burn(incense) && !act("ReportLake"),"Fan public incense and premature lake report rejected "+life);
            scene=lake();Check(!enter(scene),"Fan lake admission requires shore proximity "+life);leave(scene);shore();scene=lake();Check(enter(scene) && p.Map.BaseID==1011 && p.Entity.Money==recipeSilver+300,"Fan owned lake admission has no extra charge "+life);
            Check(p.Map.Npcs.Count==0 && Call(fights,null,"Start",p)==null,"Fan spirit and viper remain gated before incense "+life);
            Check(!burn(incense),"Fan incense requires audited lake point "+life);p.Entity.X=475;
            if(life==0){Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_burn_fault CHECK(lake_step<>1) NOT VALID");bool failed=false;try{burn(incense);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_burn_fault");}Check(failed && CountId(p,721799)==1 && p.Map.Npcs.Count==0,"Fan failed incense transaction preserves material and spirit gate");}
            Check(burn(incense) && !burn(incense) && CountId(p,721799)==0 && p.Map.Npcs.ContainsKey(9910401),"Fan incense consumes once and reveals owned spirit "+life);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721799u,(Func<bool>)(()=>true)),"Fan burned incense cannot recover "+life);
            if(life==1){ushort abandoned=p.Entity.MapID;p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");shore();scene=lake();Check(enter(scene) && !Kernel.Maps.ContainsKey(abandoned) && p.Map.Npcs.ContainsKey(9910401) && CountId(p,721799)==0,"Fan reconnect restores spirit without replacement incense");}
            p.Entity.X=477;p.Entity.Y=840;Check(act("SpeakHerbalist") && !act("SpeakHerbalist"),"Fan actual owned spirit conversation unlocks viper once "+life);
            if(life==2){now=now.AddMinutes(11);Call(scenes,null,"Tick");Check(p.Entity.MapID==1011 && p.Entity.X==473 && p.Entity.Y==844,"Fan lake expiry returns to audited shore");shore();scene=lake();Check(enter(scene) && (int)Field(Call(store,null,"Load",p),"LakeStep")==2,"Fan expiry preserves spirit conversation");}
            p.Entity.X=477;p.Entity.Y=844;var viper=(Entity)Call(fights,null,"Start",p);Check(viper!=null && viper.Name=="CrimsonViper" && viper.MaxHitpoints==50000,"Fan earned lake encounter uses documented profile "+life);
            outsider.BindWorldMap(p.Entity.MapID);outsider.Entity.X=477;outsider.Entity.Y=844;uint viperHp=viper.Hitpoints;hit(outsider,viper);Check(viper.Hitpoints==viperHp,"Fan lake outsider cannot steal Purple Eye "+life);outsider.BindWorldMap(1002);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,viper);Call(fights,null,"Tick");Check(CountId(p,710216)==0 && (int)Field(Call(store,null,"Load",p),"LakeStep")==2,"Fan full bag preserves unfinished lake reward");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);viper=(Entity)Call(fights,null,"Start",p);}
            hit(p,viper);Call(fights,null,"TryDie",viper,p.Entity);Call(fights,null,"Tick");Check(CountId(p,710216)==1 && Scalar("SELECT COUNT(*) FROM revora_fan_lake_kills WHERE uid=@uid",p.Entity.UID)==1,"Fan actual owned viper death earns one tracked Purple Eye "+life);
            Call(scenes,null,"Tick");Check(p.Entity.MapID==1011 && p.Entity.X==473 && p.Entity.Y==844 && !act("ReportLake"),"Fan completed lake safely returns and requires Chou report "+life);
            p.Entity.X=790;p.Entity.Y=456;var siteRoll=store.GetField("CourtesanRoll",Hidden);object oldSiteRoll=siteRoll.GetValue(null);
            try{siteRoll.SetValue(null,(Func<int,int>)(max=>life));Check(act("ReportLake") && !act("ReportLake") && p.Entity.Money==recipeSilver+400,"Fan lake report pays once and retains three gems "+life);}finally{siteRoll.SetValue(null,oldSiteRoll);}
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==9 && (int)Field(Call(store,null,"Load",p),"LakeStep")==3 && p.Inventory.Objects.Count(i=>i.ID==710216 || i.ID==710218 || i.ID==710219)==3 && p.Inventory.Objects.Single(i=>i.ID==710216).Bound,"Fan Courtesan-ready checkpoint and earned bound gems persist "+life);
            int site=(int)Field(Call(store,null,"Load",p),"CourtesanSite");Check(site==life,"Fan randomly selected Courtesan site persists "+life);
            var sites=(ushort[,])store.GetField("CourtesanSites",Hidden).GetValue(null);p.BindWorldMap(1015);p.Entity.X=685;p.Entity.Y=599;
            Check(Call(fights,null,"Start",p)==null,"Fan Courtesan cannot spawn away from selected site "+life);
            p.Entity.X=sites[(site+1)%4,0];p.Entity.Y=sites[(site+1)%4,1];Check(Call(fights,null,"Start",p)==null,"Fan another documented site cannot bypass saved selection "+life);
            p.Entity.X=(ushort)(sites[site,0]-2);p.Entity.Y=sites[site,1];var jade=(Entity)Call(fights,null,"Start",p);
            Check(jade!=null && jade.Name=="JadeCourtesan" && jade.MaxHitpoints==60000 && Call(fights,null,"Start",p)==null,"Fan one owned Courtesan at saved site "+life);
            outsider.BindWorldMap(1015);outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;uint jadeHp=jade.Hitpoints;hit(outsider,jade);Check(jade.Hitpoints==jadeHp,"Fan outsider cannot steal Jade Scepter "+life);outsider.BindWorldMap(1002);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,jade);Call(fights,null,"Tick");Check(CountId(p,710217)==0 && Scalar("SELECT COUNT(*) FROM revora_fan_courtesan_kills WHERE uid=@uid",p.Entity.UID)==0,"Fan full bag preserves Courtesan reward eligibility");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);jade=(Entity)Call(fights,null,"Start",p);}
            if(life==1){p=ReloadBall(p);Call(fights,null,"Tick");Check(!((bool)Call(fights,null,"HasOwner",p.Entity.UID)) && (int)Field(Call(store,null,"Load",p),"CourtesanSite")==site,"Fan reconnect retires old fight without rerolling Courtesan site");p.BindWorldMap(1015);p.Entity.X=(ushort)(sites[site,0]-2);p.Entity.Y=sites[site,1];jade=(Entity)Call(fights,null,"Start",p);}
            hit(p,jade);Call(fights,null,"TryDie",jade,p.Entity);Call(fights,null,"Tick");Check(CountId(p,710217)==1 && Scalar("SELECT COUNT(*) FROM revora_fan_courtesan_kills WHERE uid=@uid",p.Entity.UID)==1 && Call(fights,null,"Start",p)==null,"Fan actual Courtesan death awards one tracked Jade Scepter "+life);
            if(life==0){var lost=p.Inventory.Objects.Single(i=>i.ID==710217);p.Inventory.Remove(lost,Enums.ItemUse.Remove);lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,710217u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,710217u,(Func<bool>)(()=>true)),"Fan lost earned Scepter recovers without a second kill");}
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==9 && (int)Field(Call(store,null,"Load",p),"CourtesanSite")==site && p.Inventory.Objects.Count(i=>i.ID>=710216 && i.ID<=710219)==4 && p.Inventory.Objects.Single(i=>i.ID==710217).Bound && p.Entity.Money==recipeSilver+400,"Fan four earned pillar gems and exact balance persist "+life);
            Call(store,null,"InstallPillars");var pillarPoints=(ushort[,])store.GetField("PillarPoints",Hidden).GetValue(null);var gems=(uint[])store.GetField("PillarGems",Hidden).GetValue(null);
            Func<int,bool> offer=index=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"OfferPillar",p,index,(Func<bool>)(()=>true));};
            Func<object> garden=()=>{lock(p.ItemSyncRoot)return Call(scenes,null,"CreateGarden",p);};
            Action platform=()=>{p.BindWorldMap(1015);p.Entity.X=688;p.Entity.Y=723;p.ActiveNpc=9910414;};
            bool early=false;try{garden();}catch(InvalidOperationException){early=true;}Check(early && !offer(-1) && !offer(4),"Fan incomplete offerings and invalid pillar indices cannot unlock garden "+life);
            for(int i=0;i<4;i++){
                int pillar=(i+life)%4;uint gem=gems[pillar];p.BindWorldMap(1015);p.ActiveNpc=9910410u+(uint)pillar;p.Entity.X=650;p.Entity.Y=700;Check(!offer(pillar),"Fan remote pillar request preserves gem "+pillar+" life "+life);
                p.Entity.X=(ushort)(pillarPoints[pillar,0]+2);p.Entity.Y=pillarPoints[pillar,1];p.ActiveNpc=9910414;Check(!offer(pillar),"Fan wrong NPC cannot consume pillar gem "+pillar+" life "+life);p.ActiveNpc=9910410u+(uint)pillar;
                if(life==0 && i==0){Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_pillar_fault CHECK(pillars=0) NOT VALID");bool failed=false;try{offer(pillar);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_pillar_fault");}Check(failed && CountId(p,gem)==1 && (int)Field(Call(store,null,"Load",p),"Pillars")==0,"Fan failed pillar write rolls back consumed gem");}
                Check(offer(pillar) && !offer(pillar) && CountId(p,gem)==0 && ((int)Field(Call(store,null,"Load",p),"Pillars")&(1<<pillar))!=0,"Fan offering consumes correct tracked gem exactly once "+pillar+" life "+life);
                lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,gem,(Func<bool>)(()=>true)),"Fan offered gem cannot recover "+pillar+" life "+life);
                if(i==1){int mask=(int)Field(Call(store,null,"Load",p),"Pillars");p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Pillars")==mask && p.Inventory.Objects.Count(item=>item.ID>=710216 && item.ID<=710219)==2,"Fan partial offerings and remaining gems persist "+life);}
            }
            platform();scene=garden();p.ActiveNpc=0;Check(!enter(scene) && (int)Field(Call(store,null,"Load",p),"Stage")==9,"Fan garden entry requires actual platform interaction "+life);leave(scene);platform();
            if(life==0){Query("ALTER TABLE revora_fan_visits ADD CONSTRAINT fan_garden_fault CHECK(base_map<>3937) NOT VALID");scene=garden();bool failed=false;try{enter(scene);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_fan_visits DROP CONSTRAINT fan_garden_fault");}Check(failed && (int)Field(Call(store,null,"Load",p),"Stage")==9 && (int)Field(Call(store,null,"Load",p),"Pillars")==15 && p.Entity.Money==recipeSilver+400,"Fan failed garden visit preserves offerings stage and silver");leave(scene);}
            scene=garden();Check(enter(scene) && p.Map.BaseID==3937 && p.Entity.X==48 && p.Entity.Y==60 && p.Entity.Money==recipeSilver+500,"Fan four offerings unlock audited garden and one stage payment "+life);
            ushort gardenId=p.Entity.MapID;outsider.Entity.Teleport(gardenId,48,60);Check(outsider.Entity.MapID!=gardenId,"Fan garden rejects unrelated player entry "+life);
            if(life==1){p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check(!Kernel.Maps.ContainsKey(gardenId) && (int)Field(Call(store,null,"Load",p),"Pillars")==15,"Fan garden reconnect preserves consumed offerings");}
            else{now=now.AddMinutes(11);Call(scenes,null,"Tick");Check(p.Entity.MapID==1015 && p.Entity.X==688 && p.Entity.Y==723 && !Kernel.Maps.ContainsKey(gardenId),"Fan garden expiry returns to audited platform approach "+life);}
            platform();scene=garden();Check(enter(scene) && p.Entity.Money==recipeSilver+500,"Fan garden reentry neither consumes gems nor pays twice "+life);leave(scene);p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==10 && (int)Field(Call(store,null,"Load",p),"Pillars")==15 && p.Inventory.Objects.Count(item=>item.ID>=710216 && item.ID<=710219)==0,"Fan garden-ready durable state persists "+life);
            platform();scene=garden();Check(enter(scene) && (int)Field(Call(store,null,"Load",p),"BoxWinner")==life,"Fan winning box survives garden reentry "+life);
            Func<int,bool> box=index=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"OpenBox",p,index,(Func<bool>)(()=>true));};
            Action<int> boxAt=index=>{p.ActiveNpc=9910420u+(uint)index;p.Entity.X=(ushort)(52+index*6);p.Entity.Y=53;};
            Check(!box(-1) && !box(3) && !act("ExchangeBook") && !act("Claim"),"Fan invalid boxes and premature book/final claims rejected "+life);
            int winner=(int)Field(Call(store,null,"Load",p),"BoxWinner");
            for(int wrong=1;wrong<=2;wrong++){
                int index=(winner+wrong)%3;boxAt(index);p.Entity.Y=60;Check(!box(index),"Fan remote box cannot activate ghost "+wrong+" life "+life);boxAt(index);
                Check(box(index) && !box(index) && CountId(p,710221)==0,"Fan wrong box records one pending ghost without reward "+wrong+" life "+life);
                boxAt(winner);Check(!box(winner),"Fan pending ghost prevents skipping to treasure "+wrong+" life "+life);
                if(life==1 && wrong==1){ushort abandoned=p.Entity.MapID;p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");platform();scene=garden();Check(enter(scene) && !Kernel.Maps.ContainsKey(abandoned) && (int)Field(Call(store,null,"Load",p),"PendingGhost")==index && (int)Field(Call(store,null,"Load",p),"BoxWinner")==winner,"Fan pending ghost and winning box survive reconnect");}
                boxAt(index);var ghost=(Entity)Call(fights,null,"Start",p);Check(ghost!=null && ghost.Name==(wrong==1?"PoemGhost":"LyraGhost"),"Fan wrong box spawns correct owned ghost "+wrong+" life "+life);
                outsider.BindWorldMap(p.Entity.MapID);outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;uint ghostHp=ghost.Hitpoints;hit(outsider,ghost);Check(ghost.Hitpoints==ghostHp,"Fan outsider cannot clear garden ghost "+wrong+" life "+life);outsider.BindWorldMap(1002);
                hit(p,ghost);Call(fights,null,"TryDie",ghost,p.Entity);Call(fights,null,"Tick");Check((int)Field(Call(store,null,"Load",p),"PendingGhost")==-1 && Scalar("SELECT COUNT(*) FROM revora_fan_ghost_kills WHERE uid=@uid",p.Entity.UID)==wrong,"Fan actual ghost death clears pending gate exactly once "+wrong+" life "+life);
            }
            boxAt(winner);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!box(winner) && !(bool)Field(Call(store,null,"Load",p),"FanTaken"),"Fan full bag preserves unopened winning box");var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT fan_treasure_fault CHECK(id<>710221) NOT VALID");bool failed=false;try{box(winner);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT fan_treasure_fault");}Check(failed && CountId(p,710221)==0 && !(bool)Field(Call(store,null,"Load",p),"FanTaken"),"Fan failed treasure insertion preserves winning-box entitlement");}
            Check(box(winner) && !box(winner) && CountId(p,710221)==1 && p.Inventory.Objects.Single(i=>i.ID==710221).Bound,"Fan winning box grants one tracked bound quest fan "+life);
            p.Entity.X=60;p.Entity.Y=61;p.ActiveNpc=9910423;
            if(life==0){Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_book_fault CHECK(NOT book_given) NOT VALID");bool failed=false;try{act("ExchangeBook");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_book_fault");}Check(failed && CountId(p,710221)==1 && CountId(p,710222)==0,"Fan failed book exchange preserves earned quest fan");}
            Check(act("ExchangeBook") && !act("ExchangeBook") && CountId(p,710221)==0 && CountId(p,710222)==1,"Fan Mr Loneliness consumes quest fan for one bound book "+life);Call(scenes,null,"Tick");Check(p.Entity.MapID==1015 && p.Entity.X==688 && !act("Claim"),"Fan book exit returns safely and requires Sugar Tang report "+life);
            p=ReloadBall(p);Check((bool)Field(Call(store,null,"Load",p),"BookGiven") && CountId(p,710222)==1,"Fan earned book persists before final claim "+life);
            p.Entity.X=687;p.Entity.Y=599;uint fanSoul=(uint)Field(Call(store,null,"Load",p),"Soul");int priorSouls=CountId(p,fanSoul);
            if(life==0){Check(p.Inventory.Count==40 && !act("Claim") && CountId(p,710222)==1,"Fan full bag preserves final book and two-output reward");var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);
                Query("ALTER TABLE revora_attack_talisman ADD CONSTRAINT fan_final_fault CHECK(stage<>11) NOT VALID");bool failed=false;try{act("Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_attack_talisman DROP CONSTRAINT fan_final_fault");}Check(failed && CountId(p,710222)==1 && CountId(p,201009)==0 && CountId(p,fanSoul)==priorSouls && Scalar("SELECT COALESCE(array_length(rewards,1),0) FROM revora_attack_talisman WHERE uid=@uid",p.Entity.UID)==0,"Fan failed final receipt rolls back book and both rewards");}
            Check(act("Claim") && !act("Claim") && CountId(p,710222)==0 && CountId(p,201009)==1 && CountId(p,fanSoul)==priorSouls+1 && p.Entity.Money==recipeSilver+500,"Fan final exchange grants Super Fan and frozen bound P7 once "+life);
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==11 && Scalar("SELECT array_length(rewards,1) FROM revora_attack_talisman WHERE uid=@uid",p.Entity.UID)==2 && p.Inventory.Objects.Single(i=>i.ID==201009).Bound && p.Inventory.Objects.Where(i=>i.ID==fanSoul).All(i=>i.Bound),"Fan final receipt and bound rewards survive production loader "+life);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,710222u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Accept",p,(Func<bool>)(()=>true)),"Fan final book cannot recover or restart once-per-character quest "+life);

        }finally{now=now.AddHours(1);Call(scenes,null,"Tick");Call(fights,null,"Tick");clock.SetValue(null,oldClock);gardenRoll.SetValue(null,oldGardenRoll);}
    }
    static void VerifyDefenseTalismanFishing(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanStore",true);
        var fights=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanEncounters",true);Call(store,null,"Initialize");World(runtime,1015);
        var clock=store.GetField("Clock",Hidden);object prior=clock.GetValue(null);DateTime now=new DateTime(2026,9,28,6,0,0,DateTimeKind.Utc);
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,Entity> spawn=p=>(Entity)Call(fights,null,"Start",p);
        Action<GameState,Entity> hit=(p,b)=>{uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=ReloadBall(WelcomePlayer(town,100,0,15));Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);p.LoadItemStats();act(p,"Accept");
            p.BindWorldMap(1015);p.Entity.X=310;p.Entity.Y=380;Check(spawn(p)==null,"Tower no trout before fishing referral");
            // Only referral is seeded here; nets, fish and poise use real transactions/combat.
            Query("UPDATE revora_defense_talisman SET stage=3 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Check(spawn(p)==null,"Tower no trout without earned net");
            var outsider=ReloadBall(WelcomePlayer(town,100,0,15));outsider.LoadItemStats();outsider.BindWorldMap(1015);outsider.Entity.X=310;outsider.Entity.Y=380;
            for(int count=1;count<=3;count++){
                Check(act(p,"BuyNet"),"Tower buys next net for earned fish "+count);
                p.Entity.X=300;Check(spawn(p)==null,"Tower net cannot summon away from fishing site "+count);p.Entity.X=310;
                var b=spawn(p);Check(b!=null && spawn(p)==null,"Tower one owned trout per player "+count);
                uint before=b.Hitpoints;hit(outsider,b);Check(b.Hitpoints==before && CountId(outsider,721778)==0,"Tower outsider cannot damage or steal trout "+count);
                if(count==1){while(p.Inventory.Count<40)Pack(p,1088001,1);}
                hit(p,b);Check(b.Dead && CountId(p,721778)==count && CountId(p,721779)==0,"Tower combat consumes one net and earns one fish "+count);
                Call(fights,null,"TryDie",b,p.Entity);Call(fights,null,"Tick");Check(!p.Map.Entities.ContainsKey(b.UID) && CountId(p,721778)==count,"Tower death replay and corpse cleanup "+count);
                if(count==1)foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            }
            Check(!act(p,"BuyNet") && spawn(p)==null && act(p,"ExchangeFish") && CountId(p,721780)==1,"Tower three combat-earned fish exchange for poise and cap fishing");
            p=ReloadBall(p);Check(p.Entity.Money==110000 && CountId(p,721780)==1 && Scalar("SELECT COUNT(*) FROM revora_defense_talisman_fishing WHERE uid=@uid",p.Entity.UID)==3,"Tower combat fish receipts/payment/poise survive reload");
            var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanScenes",true);Call(scenes,null,"InitializeTravel");Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
            Func<GameState,object> hut=player=>{string cwd=Environment.CurrentDirectory;try{Environment.CurrentDirectory=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/defense-talisman-scenes/server"));lock(player.ItemSyncRoot)return Call(scenes,null,"CreateHut",player);}finally{Environment.CurrentDirectory=cwd;}};
            Func<GameState,int,bool> door=(player,site)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"OpenDoor",player,site,(Func<bool>)(()=>true));};
            p.BindWorldMap(1015);p.Entity.X=352;p.Entity.Y=402;Check(!door(p,1) && !door(p,-1) && !door(p,3),"Tower wrong-order and invalid doors preserve poise");
            var early=hut(p);lock(p.ItemSyncRoot){Check(!(bool)Call(scenes,null,"Enter",early,p,(Func<bool>)(()=>true)),"Tower Hut admission requires completed door route");Call(scenes,null,"Exit",early,p);}
            p.Entity.X=310;p.Entity.Y=380;Check(!door(p,0),"Tower remote door use denied");
            p.Entity.X=342;p.Entity.Y=404;Check(door(p,0) && !door(p,0) && CountId(p,721780)==1,"Tower first door records once without consuming poise");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"DoorStep")==1,"Tower partial door route survives reconnect");
            p.BindWorldMap(1015);p.Entity.X=352;p.Entity.Y=402;int tries=0;bool contextFailed=false;
            try{lock(p.ItemSyncRoot)Call(store,null,"OpenDoor",p,1,(Func<bool>)(()=>++tries==1));}catch(InvalidOperationException){contextFailed=true;}
            Check(contextFailed && (int)Field(Call(store,null,"Load",p),"DoorStep")==1 && CountId(p,721780)==1,"Tower changed door context rolls back progress");
            Check(door(p,1) && CountId(p,721780)==1,"Tower second door preserves poise until final hand-in");
            p.Entity.X=360;p.Entity.Y=397;Query("ALTER TABLE revora_defense_talisman ADD CONSTRAINT tower_door_fault CHECK(stage<>4) NOT VALID");bool doorFailed=false;
            try{door(p,2);}catch(Npgsql.PostgresException){doorFailed=true;}finally{Query("ALTER TABLE revora_defense_talisman DROP CONSTRAINT tower_door_fault");}
            Check(doorFailed && CountId(p,721780)==1 && p.Entity.Money==110000 && (int)Field(Call(store,null,"Load",p),"DoorStep")==2,"Tower failed final door preserves material/payment/progress");
            GameState doorOwner=p;Parallel.For(0,4,j=>door(doorOwner,2));
            Check(CountId(p,721780)==0 && p.Entity.Money==110100 && (int)Field(Call(store,null,"Load",p),"Stage")==4,"Tower concurrent final door consumes once and pays once");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"DoorStep")==3 && !door(p,2),"Tower completed door route persists without replay");
            var cabin=hut(p);var cabinMap=(Map)Field(cabin,"Map");lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Enter",cabin,p,(Func<bool>)(()=>true)),"Tower earned door route enters owned Hut");
            Check(cabinMap.BaseID==1784 && p.Entity.MapID==cabinMap.ID && Call(fights,null,"Start",p)==null,"Tower Hut uses dedicated alias without first-house combat");
            Check(!act(p,"RequestSpear"),"Tower Ruan request requires nearby position in owned Hut");p.Entity.X=53;p.Entity.Y=55;
            Check(act(p,"RequestSpear") && !act(p,"RequestSpear") && !act(p,"ExchangeSpear"),"Tower Ruan records request once and rejects unearned spear");
            outsider.Entity.Teleport(cabinMap.ID,45,60);Check(outsider.Entity.MapID!=cabinMap.ID,"Tower unrelated player cannot enter owned Hut");
            lock(p.ItemSyncRoot)Call(scenes,null,"Exit",cabin,p);Check(p.Entity.MapID==1015 && !Kernel.Maps.ContainsKey(cabinMap.ID) && p.Entity.Money==110100,"Tower Hut exit preserves earned progress and releases scene");
            p=ReloadBall(p);p.LoadItemStats();Check((bool)Field(Call(store,null,"Load",p),"SpearRequested"),"Tower spear request survives reconnect");
            p.BindWorldMap(1015);p.Entity.X=400;p.Entity.Y=650;now=now.AddMinutes(1);Check(spawn(p)==null,"Tower Shawn waits for ten-minute schedule window");now=now.AddMinutes(9);
            var full=spawn(p);Check(full!=null && full.Name=="BloodyShawn","Tower requested Shawn spawns in schedule window");
            while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,full);Call(fights,null,"Tick");
            Check(CountId(p,721781)==0 && Scalar("SELECT COUNT(*) FROM revora_tower_shawn_kills WHERE uid=@uid",p.Entity.UID)==0,"Tower full bag leaves spear entitlement unclaimed for retry");
            foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            var shawn=spawn(p);outsider.BindWorldMap(1015);outsider.Entity.X=400;outsider.Entity.Y=650;uint shawnHp=shawn.Hitpoints;hit(outsider,shawn);
            Check(shawn.Hitpoints==shawnHp && CountId(outsider,721781)==0,"Tower outsider cannot steal Shawn spear");
            now=now.AddMinutes(1);Call(fights,null,"Tick");Check(p.Map.Entities.ContainsKey(shawn.UID),"Tower active Shawn survives end of spawn minute");
            hit(p,shawn);Call(fights,null,"TryDie",shawn,p.Entity);Call(fights,null,"Tick");
            Check(CountId(p,721781)==1 && spawn(p)==null && Scalar("SELECT COUNT(*) FROM revora_tower_shawn_kills WHERE uid=@uid",p.Entity.UID)==1,"Tower actual Shawn death earns one tracked spear");
            uint spearUid=p.Inventory.Objects.Single(i=>i.ID==721781).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)spearUid);p=ReloadBall(p);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721781u,(Func<bool>)(()=>true)),"Tower stored spear cannot be copied");
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)spearUid);lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721781u,(Func<bool>)(()=>true)),"Tower lost earned spear recovers without another kill");
            var returnHut=hut(p);lock(p.ItemSyncRoot)Call(scenes,null,"Enter",returnHut,p,(Func<bool>)(()=>true));p.Entity.X=53;p.Entity.Y=55;
            Query("ALTER TABLE items ADD CONSTRAINT tower_splinter_fault CHECK(id<>721777) NOT VALID");bool splinterFailed=false;
            try{act(p,"ExchangeSpear");}catch(Npgsql.PostgresException){splinterFailed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tower_splinter_fault");}
            Check(splinterFailed && CountId(p,721781)==1 && CountId(p,721777)==0 && p.Entity.Money==110100,"Tower failed splinter insertion preserves spear/payment");
            Check(act(p,"ExchangeSpear") && !act(p,"ExchangeSpear") && CountId(p,721781)==0 && CountId(p,721777)==1 && p.Entity.Money==110200,"Tower earned spear exchanges for splinter and silver exactly once");
            Call(scenes,null,"Tick");p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==5 && CountId(p,721777)==1 && p.Entity.Money==110200,"Tower spear report and tracked splinter persist");
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721781u,(Func<bool>)(()=>true)),"Tower consumed spear cannot recover after report");
            Check(!act(p,"PlumReferral"),"Tower remote Plum referral rejected");World(runtime,1000);p.BindWorldMap(1000);p.Entity.X=474;p.Entity.Y=578;
            Check(act(p,"PlumReferral") && !act(p,"PlumReferral") && p.Entity.Money==110300 && CountId(p,721777)==1,"Tower Plum referral pays once and preserves splinter for investigation");
            p.BindWorldMap(1015);p.Entity.X=166;p.Entity.Y=226;Check(spawn(p)==null,"Tower desert encounters cannot spawn on wrong map");p.BindWorldMap(1000);
            var condor=spawn(p);Check(condor!=null && condor.Name=="DesertCondor","Tower investigation begins with owned Condor");
            outsider.BindWorldMap(1000);outsider.Entity.X=166;outsider.Entity.Y=226;uint condorHp=condor.Hitpoints;hit(outsider,condor);Check(condor.Hitpoints==condorHp,"Tower outsider cannot steal Condor credit");
            hit(p,condor);Call(fights,null,"TryDie",condor,p.Entity);Call(fights,null,"Tick");
            Check((int)Field(Call(store,null,"Load",p),"DesertStep")==1 && CountId(p,721777)==1 && CountId(p,721782)==0,"Tower Condor advances once without premature theft");
            p=ReloadBall(p);p.LoadItemStats();Check((int)Field(Call(store,null,"Load",p),"DesertStep")==1,"Tower intermediate desert kill persists");p.BindWorldMap(1000);p.Entity.X=166;p.Entity.Y=226;
            var leader=spawn(p);Check(leader!=null && leader.Name=="CapriceLeader","Tower persisted Condor credit unlocks Caprice Leader");
            while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,leader);Call(fights,null,"TryDie",leader,p.Entity);Call(fights,null,"Tick");
            Check(CountId(p,721777)==0 && CountId(p,721782)==1 && p.Inventory.Count==40 && (int)Field(Call(store,null,"Load",p),"DesertStep")==2,"Tower Caprice kill replaces earned splinter with handkerchief even in full bag");
            Check(spawn(p)==null && Scalar("SELECT COUNT(*) FROM revora_tower_desert_kills WHERE uid=@uid",p.Entity.UID)==2,"Tower desert receipts prevent repeated encounter rewards");
            foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721777u,(Func<bool>)(()=>true)),"Tower stolen splinter cannot be recovered after handkerchief award");
            Check(!act(p,"ReportHandkerchief"),"Tower handkerchief report requires Yang Yun location");p.BindWorldMap(1015);p.Entity.X=734;p.Entity.Y=516;
            Query("ALTER TABLE revora_defense_talisman ADD CONSTRAINT tower_cloth_fault CHECK(stage<>7) NOT VALID");bool clothFailed=false;
            try{act(p,"ReportHandkerchief");}catch(Npgsql.PostgresException){clothFailed=true;}finally{Query("ALTER TABLE revora_defense_talisman DROP CONSTRAINT tower_cloth_fault");}
            Check(clothFailed && CountId(p,721782)==1 && p.Entity.Money==110300 && (int)Field(Call(store,null,"Load",p),"Stage")==6,"Tower failed handkerchief report preserves item/stage/payment");
            Check(act(p,"ReportHandkerchief") && !act(p,"ReportHandkerchief") && CountId(p,721782)==0 && p.Entity.Money==110400,"Tower tracked handkerchief reports once for second-house referral");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==7 && p.Entity.Money==110400,"Tower complete desert investigation persists");
            object ambush;string beforeAmbush=Environment.CurrentDirectory;
            try{Environment.CurrentDirectory=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/defense-talisman-scenes/server"));lock(p.ItemSyncRoot)ambush=Call(scenes,null,"CreateAmbush",p);}finally{Environment.CurrentDirectory=beforeAmbush;}
            var ambushMap=(Map)Field(ambush,"Map");lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Enter",ambush,p,(Func<bool>)(()=>true)),"Tower handkerchief referral enters owned second house");
            Check(ambushMap.BaseID==1783 && ambushMap.Portals.Count==0,"Tower ambush uses dedicated scene without inherited portal routing");
            p.Entity.X=50;p.Entity.Y=50;Check(!act(p,"ClaimInvocation"),"Tower invocation cannot be claimed before clearing ambush");p.Entity.X=45;p.Entity.Y=60;
            for(int rat=1;rat<=5;rat++){
                var b=spawn(p);Check(b!=null && b.Name=="ViciousRat","Tower ambush spawns owned rat "+rat);
                outsider.BindWorldMap(ambushMap.ID);outsider.Entity.X=45;outsider.Entity.Y=60;uint hp=b.Hitpoints;hit(outsider,b);Check(b.Hitpoints==hp,"Tower outsider cannot steal ambush credit "+rat);outsider.BindWorldMap(1002);
                hit(p,b);Call(fights,null,"TryDie",b,p.Entity);Call(fights,null,"Tick");Check((int)Call(store,null,"HouseKills",p,(Guid)Field(ambush,"Visit"))==rat,"Tower ambush records each actual kill once "+rat);
            }
            Check(spawn(p)==null && !act(p,"ClaimInvocation"),"Tower cleared ambush stops spawning and requires reaching invocation");
            p.Entity.X=50;p.Entity.Y=50;while(p.Inventory.Count<40)Pack(p,1088001,1);
            Check(!act(p,"ClaimInvocation") && CountId(p,721786)==0 && (int)Field(Call(store,null,"Load",p),"Stage")==7,"Tower full bag preserves earned ambush completion for claim retry");
            foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT tower_invocation_fault CHECK(id<>721786) NOT VALID");bool invocationFailed=false;
            try{act(p,"ClaimInvocation");}catch(Npgsql.PostgresException){invocationFailed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tower_invocation_fault");}
            Check(invocationFailed && CountId(p,721786)==0 && p.Entity.Money==110400 && (int)Field(Call(store,null,"Load",p),"Stage")==7,"Tower failed invocation insertion preserves stage/payment");
            Check(act(p,"ClaimInvocation") && !act(p,"ClaimInvocation") && CountId(p,721786)==1 && p.Entity.Money==110500,"Tower cleared ambush earns invocation and silver once");
            Call(scenes,null,"Tick");p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==8 && CountId(p,721786)==1 && !Kernel.Maps.ContainsKey(ambushMap.ID),"Tower invocation/referral persist and completed ambush retires");
            uint invocationUid=p.Inventory.Objects.Single(i=>i.ID==721786).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)invocationUid);p=ReloadBall(p);
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721786u,(Func<bool>)(()=>true)),"Tower stored invocation cannot be copied");
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)invocationUid);lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721786u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721786u,(Func<bool>)(()=>true)),"Tower genuinely lost earned invocation recovers once");
            bool earlyDungeon=false;try{lock(p.ItemSyncRoot)Call(scenes,null,"CreateDungeon",p);}catch(InvalidOperationException){earlyDungeon=true;}
            Check(earlyDungeon && !act(p,"PrepareDungeon"),"Tower invocation alone cannot bypass Ghoul Kong or remote admission");
            p.BindWorldMap(1000);p.Entity.X=87;p.Entity.Y=313;int entryCalls=0;bool entryFailed=false;
            try{lock(p.ItemSyncRoot)Call(store,null,"PrepareDungeon",p,(Func<bool>)(()=>++entryCalls==1));}catch(InvalidOperationException){entryFailed=true;}
            Check(entryFailed && (int)Field(Call(store,null,"Load",p),"DungeonFloor")==0,"Tower changed Ghoul context rolls back dungeon unlock");
            Check(act(p,"PrepareDungeon") && act(p,"PrepareDungeon") && CountId(p,721786)==1 && p.Entity.Money==110500,"Tower Ghoul unlock is repeat-safe and keeps owned invocation");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"DungeonFloor")==1,"Tower entrance floor survives reconnect");
            uint entryKey=p.Inventory.Objects.Single(i=>i.ID==721786).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)entryKey);p=ReloadBall(p);
            object deniedDungeon;lock(p.ItemSyncRoot){deniedDungeon=Call(scenes,null,"CreateDungeon",p);Check(!(bool)Call(scenes,null,"Enter",deniedDungeon,p,(Func<bool>)(()=>true)),"Tower stored invocation cannot enter unlocked dungeon");Call(scenes,null,"Exit",deniedDungeon,p);}
            Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)entryKey);p=ReloadBall(p);
            object dungeon;lock(p.ItemSyncRoot){dungeon=Call(scenes,null,"CreateDungeon",p);Check((bool)Call(scenes,null,"Enter",dungeon,p,(Func<bool>)(()=>true)),"Tower saved unlock and owned invocation admit first dungeon floor");}
            var dungeonMap=(Map)Field(dungeon,"Map");Check(dungeonMap.BaseID==1785 && p.Entity.X==96 && p.Entity.Y==124 && dungeonMap.Portals.Count==0,"Tower floor one uses verified entrance and excludes inherited portals");
            outsider.Entity.Teleport(dungeonMap.ID,96,124);Check(outsider.Entity.MapID!=dungeonMap.ID,"Tower private dungeon rejects unrelated admission");
            lock(p.ItemSyncRoot)Call(scenes,null,"Exit",dungeon,p);p=ReloadBall(p);
            Check((int)Field(Call(store,null,"Load",p),"DungeonFloor")==1 && (int)Field(Call(store,null,"Load",p),"Stage")==8 && CountId(p,721786)==1 && !Kernel.Maps.ContainsKey(dungeonMap.ID),"Tower exit preserves key/entrance progress without advancing floors");
            lock(p.ItemSyncRoot){dungeon=Call(scenes,null,"CreateDungeon",p);Call(scenes,null,"Enter",dungeon,p,(Func<bool>)(()=>true));}
            uint[] generalWeapons={721784,721788,721789,721790};ushort[,] generalStand={{96,124},{70,64},{113,126},{62,85}};
            p.Entity.X=70;p.Entity.Y=56;lock(p.ItemSyncRoot)Check(!(bool)Call(scenes,null,"Descend",p,(Func<bool>)(()=>true)),"Tower first gate cannot skip unearned general weapons");
            for(int slot=0;slot<4;slot++){
                p.Entity.X=generalStand[slot,0];p.Entity.Y=generalStand[slot,1];var general=spawn(p);
                Check(general!=null && general.MonsterInfo.ID==9905206+slot,"Tower correct owned general appears for floor/slot "+slot);
                outsider.BindWorldMap(p.Entity.MapID);outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;uint hp=general.Hitpoints;hit(outsider,general);Check(general.Hitpoints==hp,"Tower outsider cannot steal general weapon "+slot);outsider.BindWorldMap(1002);
                if(slot==0){while(p.Inventory.Count<40)Pack(p,1088001,1);hit(p,general);Call(fights,null,"Tick");Check(CountId(p,generalWeapons[slot])==0 && Scalar("SELECT COUNT(*) FROM revora_tower_general_kills WHERE uid=@uid",p.Entity.UID)==0,"Tower full bag leaves general reward available for retry");foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);general=spawn(p);}
                hit(p,general);Call(fights,null,"TryDie",general,p.Entity);Call(fights,null,"Tick");Check(CountId(p,generalWeapons[slot])==1 && Scalar("SELECT COUNT(*) FROM revora_tower_general_kills WHERE uid=@uid",p.Entity.UID)==slot+1,"Tower actual general kill earns one durable weapon "+slot);
                if(slot==1 || slot==3){
                    Check(!act(p,"AdvanceDungeon"),"Tower floor gate rejects remote progression "+slot);p.Entity.X=(ushort)(slot==1?70:62);p.Entity.Y=(ushort)(slot==1?56:77);
                    if(slot==1){
                        var locked=p.Inventory.Objects.Single(i=>i.ID==721784);locked.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)locked.UID);Check(!act(p,"AdvanceDungeon"),"Tower floor gate requires usable owned weapons");locked.Lock=0;Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)locked.UID);
                        Query("ALTER TABLE revora_defense_talisman ADD CONSTRAINT tower_floor_fault CHECK(dungeon_floor<>2) NOT VALID");bool floorFailed=false;try{act(p,"AdvanceDungeon");}catch(Npgsql.PostgresException){floorFailed=true;}finally{Query("ALTER TABLE revora_defense_talisman DROP CONSTRAINT tower_floor_fault");}
                        Check(floorFailed && (int)Field(Call(store,null,"Load",p),"DungeonFloor")==1 && CountId(p,721784)==1 && CountId(p,721788)==1,"Tower failed floor save preserves current visit and weapons");
                    }
                    ushort oldFloor=p.Entity.MapID;lock(p.ItemSyncRoot)Check((bool)Call(scenes,null,"Descend",p,(Func<bool>)(()=>true)),"Tower earned weapons open next owned floor "+slot);
                    Check(p.Map.BaseID==(slot==1?1786:1787) && !Kernel.Maps.ContainsKey(oldFloor) && (int)Field(Call(store,null,"Load",p),"DungeonFloor")==slot/2+2,"Tower transition saves destination and retires old floor "+slot);
                }
            }
            Check(generalWeapons.All(id=>CountId(p,id)==1) && p.Entity.X==56 && p.Entity.Y==68,"Tower four combat-earned weapons arrive at final floor");
            var finalFloor=Call(scenes,null,"Find",p);lock(p.ItemSyncRoot)Call(scenes,null,"Exit",finalFloor,p);p=ReloadBall(p);
            Check((int)Field(Call(store,null,"Load",p),"DungeonFloor")==3 && generalWeapons.All(id=>CountId(p,id)==1),"Tower final-floor checkpoint and all earned weapons persist");
            lock(p.ItemSyncRoot){finalFloor=Call(scenes,null,"CreateDungeon",p);Call(scenes,null,"Enter",finalFloor,p,(Func<bool>)(()=>true));}
            Func<int,bool> smelt=slot=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Smelt",p,slot,(Func<bool>)(()=>true));};
            p.Entity.X=48;p.Entity.Y=39;Check(!smelt(0) && !act(p,"Reconstruct"),"Tower cannot smelt or reconstruct before Yang Feng");
            p.Entity.X=56;p.Entity.Y=68;var feng=spawn(p);Check(feng!=null && feng.Name=="YangFeng","Tower earned four weapons summon Yang Feng on final floor");
            outsider.BindWorldMap(p.Entity.MapID);outsider.Entity.X=56;outsider.Entity.Y=68;uint fengHp=feng.Hitpoints;hit(outsider,feng);Check(feng.Hitpoints==fengHp,"Tower outsider cannot steal final splinters");outsider.BindWorldMap(1002);
            while(p.Inventory.Count<38)Pack(p,1088001,1);hit(p,feng);Call(fights,null,"Tick");Check(CountId(p,721777)==0 && Scalar("SELECT COUNT(*) FROM revora_tower_feng_kills WHERE uid=@uid",p.Entity.UID)==0,"Tower insufficient room rolls back all three splinters and permits retry");
            foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            feng=spawn(p);hit(p,feng);Call(fights,null,"TryDie",feng,p.Entity);Call(fights,null,"Tick");Check(CountId(p,721777)==3 && Scalar("SELECT COUNT(*) FROM revora_tower_feng_kills WHERE uid=@uid",p.Entity.UID)==1 && spawn(p)==null,"Tower actual Yang Feng death awards exactly three splinters once");
            Check(!smelt(0) && !act(p,"Reconstruct"),"Tower Cauldron rejects remote exchanges");p.Entity.X=48;p.Entity.Y=39;
            uint[] clinkerTypes={721793,721794,721792,721791};
            Query("ALTER TABLE items ADD CONSTRAINT tower_smelt_fault CHECK(id<>721793) NOT VALID");bool smeltFailed=false;try{smelt(0);}catch(Npgsql.PostgresException){smeltFailed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tower_smelt_fault");}
            Check(smeltFailed && CountId(p,721784)==1 && CountId(p,721793)==0,"Tower failed smelting preserves weapon and entitlement");
            while(p.Inventory.Count<40)Pack(p,1088001,1);
            for(int slot=0;slot<4;slot++)Check(smelt(slot) && !smelt(slot) && CountId(p,generalWeapons[slot])==0 && CountId(p,clinkerTypes[slot])==1 && p.Inventory.Count==40,"Tower exact weapon-to-clinker replacement works in full bag "+slot);
            var lostClinker=p.Inventory.Objects.Single(i=>i.ID==721793);p.Inventory.Remove(lostClinker,Enums.ItemUse.Remove);
            Check(!act(p,"Reconstruct"),"Tower missing clinker prevents reconstruction");lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721793u,(Func<bool>)(()=>true)),"Tower lost earned clinker recovers without reissuing weapon");
            lock(p.ItemSyncRoot)Check(!(bool)Call(store,null,"Recover",p,721784u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721793u,(Func<bool>)(()=>true)),"Tower consumed weapon and existing clinker cannot recover again");
            Query("ALTER TABLE revora_defense_talisman ADD CONSTRAINT tower_restore_fault CHECK(stage<>9) NOT VALID");bool restoreFailed=false;try{act(p,"Reconstruct");}catch(Npgsql.PostgresException){restoreFailed=true;}finally{Query("ALTER TABLE revora_defense_talisman DROP CONSTRAINT tower_restore_fault");}
            Check(restoreFailed && CountId(p,721777)==3 && CountId(p,721786)==1 && clinkerTypes.All(id=>CountId(p,id)==1),"Tower failed reconstruction preserves all eight inputs");
            Check(act(p,"Reconstruct") && !act(p,"Reconstruct") && CountId(p,721785)==1 && CountId(p,721777)==0 && CountId(p,721786)==0 && clinkerTypes.All(id=>CountId(p,id)==0),"Tower reconstruction atomically consumes splinters clinkers and invocation");
            lock(p.ItemSyncRoot)Call(scenes,null,"Exit",finalFloor,p);p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==9 && CountId(p,721785)==1,"Tower reconstructed quest item and stage survive reload");
            var lostTower=p.Inventory.Objects.Single(i=>i.ID==721785);p.Inventory.Remove(lostTower,Enums.ItemUse.Remove);
            lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Recover",p,721785u,(Func<bool>)(()=>true)) && !(bool)Call(store,null,"Recover",p,721785u,(Func<bool>)(()=>true)),"Tower lost reconstructed quest item recovers once before final claim");
            Check(!act(p,"Claim"),"Tower reward requires Sugar Tang proximity");p.BindWorldMap(1015);p.Entity.X=685;p.Entity.Y=599;
            while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!act(p,"Claim") && CountId(p,721785)==1,"Tower full bag cannot consume final quest tower");var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);
            uint rewardSoul=(uint)Field(Call(store,null,"Load",p),"Soul");
            Query("ALTER TABLE revora_defense_talisman ADD CONSTRAINT tower_claim_fault CHECK(stage<>10) NOT VALID");bool claimFailed=false;try{act(p,"Claim");}catch(Npgsql.PostgresException){claimFailed=true;}finally{Query("ALTER TABLE revora_defense_talisman DROP CONSTRAINT tower_claim_fault");}
            Check(claimFailed && CountId(p,721785)==1 && CountId(p,202008)==0 && CountId(p,rewardSoul)==0,"Tower failed final receipt rolls back both rewards and consumption");
            Check(act(p,"Claim") && !act(p,"Claim") && CountId(p,721785)==0 && CountId(p,202008)==1 && CountId(p,rewardSoul)==1 && p.Inventory.Count==40,"Tower earned final hand-in grants Elite tower and frozen P7 once");
            Check(p.Inventory.Objects.Where(i=>i.ID==202008 || i.ID==rewardSoul).All(i=>i.Bound),"Tower final equipment and P7 rewards are bound");
            p=ReloadBall(p);Check((int)Field(Call(store,null,"Load",p),"Stage")==10 && CountId(p,202008)==1 && CountId(p,rewardSoul)==1 && !act(p,"Accept"),"Tower final rewards completion and once-per-character rule persist");
            var pending=ReloadBall(WelcomePlayer(town,100,0,15));Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)pending.Entity.UID);pending=ReloadBall(pending);pending.LoadItemStats();act(pending,"Accept");Query("UPDATE revora_defense_talisman SET stage=3 WHERE uid=@uid","uid",(long)pending.Entity.UID);act(pending,"BuyNet");pending.BindWorldMap(1015);pending.Entity.X=310;pending.Entity.Y=380;
            var expired=spawn(pending);now=now.AddMinutes(5);Call(fights,null,"Tick");Check(expired!=null && !pending.Map.Entities.ContainsKey(expired.UID) && CountId(pending,721779)==1,"Tower timeout cleans trout without consuming net");
            var disconnected=spawn(pending);pending=ReloadBall(pending);Call(fights,null,"Tick");Check(disconnected!=null && !Kernel.Maps[1015].Entities.ContainsKey(disconnected.UID) && CountId(pending,721779)==1,"Tower replacement session cleans encounter and retains paid net");
        }finally{now=now.AddHours(1);Call(fights,null,"Tick");clock.SetValue(null,prior);}
    }
    static void VerifyDefenseTalismanStore(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DefenseTalismanStore",true);Call(store,null,"Initialize");
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> recover=(p,type)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,type,(Func<bool>)(()=>true));};
        Action<GameState> fish=p=>{var i=NestedPack(p,721778,1,true);Query("INSERT INTO revora_defense_talisman_materials(item_uid,uid,type) VALUES(@item,@uid,721778)","item",(long)i.UID,"uid",(long)p.Entity.UID);};
        Check(!(bool)Call(store,null,"Eligible",ReloadBall(WelcomePlayer(town,99,0,15))),"Tower rejects level99");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)100:(byte)140,life,15));
            Query("UPDATE entities SET money=200000 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);
            Check(act(p,"Accept") && !act(p,"Accept") && !act(p,"BuyNet"),"Tower once-per-character acceptance cannot skip first encounter "+life);
            uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");p=ReloadBall(p);
            Check((int)Field(Call(store,null,"Load",p),"Stage")==1 && (uint)Field(Call(store,null,"Load",p),"Soul")==soul,"Tower admission and frozen reward survive reload "+life);
            // Foundation fixture only: encounters do not grant stage3 or fish yet.
            Query("UPDATE revora_defense_talisman SET stage=3 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(life==0){
                while(p.Inventory.Count<40)Pack(p,1088001,1);
                Check(!act(p,"BuyNet") && p.Entity.Money==200000,"Tower full bag does not charge net");
                foreach(var i in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                int calls=0;bool refused=false;try{lock(p.ItemSyncRoot)Call(store,null,"BuyNet",p,(Func<bool>)(()=>++calls==1));}catch(InvalidOperationException){refused=true;}
                Check(refused && CountId(p,721779)==0 && p.Entity.Money==200000 && Scalar("SELECT COUNT(*) FROM revora_defense_talisman_purchases WHERE uid=@uid",p.Entity.UID)==0,"Tower context failure rolls back item/payment/receipt");
            }
            GameState owner=p;Parallel.For(0,4,n=>act(owner,"BuyNet"));
            Check(CountId(p,721779)==1 && p.Entity.Money==170000 && Scalar("SELECT COUNT(*) FROM revora_defense_talisman_purchases WHERE uid=@uid",p.Entity.UID)==1,"Tower concurrent net purchase charges once "+life);
            uint net=p.Inventory.Objects.Single(i=>i.ID==721779).UID;
            Check(!recover(p,721779) && !recover(p,721781),"Tower owned net and unearned spear cannot recover "+life);
            Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)net);p=ReloadBall(p);
            Check(!recover(p,721779) && !act(p,"BuyNet"),"Tower stored net cannot duplicate or charge again "+life);
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)net);
            Check(recover(p,721779) && !recover(p,721779) && p.Entity.Money==170000,"Tower genuinely lost paid net recovers once free "+life);
            NestedPack(p,721778,1,true);fish(p);fish(p);
            Check(!act(p,"ExchangeFish"),"Tower two earned fish plus untracked fish cannot qualify "+life);fish(p);
            if(life==0){
                Query("ALTER TABLE items ADD CONSTRAINT tower_poise_fault CHECK(id<>721780) NOT VALID");bool failed=false;
                try{act(p,"ExchangeFish");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT tower_poise_fault");}
                Check(failed && CountId(p,721778)==4 && CountId(p,721780)==0,"Tower poise insertion failure preserves fish");
            }
            Check(act(p,"ExchangeFish") && !act(p,"ExchangeFish") && CountId(p,721778)==1 && CountId(p,721780)==1,"Tower consumes exactly three tracked fish for bound poise "+life);
            p=ReloadBall(p);Check(p.Entity.Money==170000 && CountId(p,721780)==1 && p.Inventory.Objects.Single(i=>i.ID==721780).Bound && !act(p,"BuyNet"),"Tower poise and payment persist without extra purchase "+life);
            if(life==0){p.Entity.Reborn=1;EntityTable.SaveEntity(p);p=ReloadBall(p);Check(!act(p,"Accept") && (uint)Field(Call(store,null,"Load",p),"Soul")==soul,"Tower rebirth cannot reset one-time quest or reroll reward");}
        }
    }
    static void VerifyDesertExpeditionStore(Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.DesertExpeditionStore",true);Call(store,null,"Initialize");
        Func<GameState,int,bool> step=(player,a)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"Advance",player,a,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> recover=(player,type)=>{lock(player.ItemSyncRoot)return (bool)Call(store,null,"Recover",player,type,(Func<bool>)(()=>true));};
        Func<GameState,int> stage=player=>(int)Field(Call(store,null,"Load",player),"Stage");
        Action<GameState,uint> material=(player,type)=>{var item=NestedPack(player,type,1,true);Query("INSERT INTO revora_desert_expedition_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)player.Entity.UID,"life",(int)player.Entity.Reborn,"type",(int)type);};
        Check(!(bool)Call(store,null,"Eligible",ReloadBall(WelcomePlayer(town,104,0,15))),"Desert Expedition rejects level104");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)105:(byte)140,life,15));ulong money=p.Entity.Money;
            Check(step(p,0) && !step(p,0) && !step(p,2),"Desert Expedition unique ordered acceptance "+life);uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088001,1);Check(!step(p,1) && stage(p)==1 && p.Entity.Money==money,"Desert Expedition full bag preserves referral");foreach(var item in p.Inventory.Objects.Where(x=>x.ID==1088001).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);}
            Check(step(p,1) && !step(p,1) && CountId(p,721920)==1 && p.Inventory.Objects.Single(x=>x.ID==721920).Bound,"Desert Expedition gives bound pill once "+life);
            uint pill=p.Inventory.Objects.Single(x=>x.ID==721920).UID;Check(!recover(p,721920) && !recover(p,710260),"Desert Expedition cannot duplicate owned or unearned materials "+life);
            if(life==0){Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)pill);p=ReloadBall(p);Check(!recover(p,721920),"Desert Expedition stored pill cannot be copied");Query("DELETE FROM items WHERE uid=@uid","uid",(long)pill);Check(recover(p,721920) && !recover(p,721920) && CountId(p,721920)==1,"Desert Expedition genuinely lost pill recovers once");}
            NestedPack(p,710259,1,true);for(int j=0;j<9;j++)material(p,710259);Check(!step(p,2) && stage(p)==2,"Desert Expedition nine tracked plus one untracked horn cannot qualify "+life);material(p,710259);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT expedition_mirror_fault CHECK(id<>721921) NOT VALID");bool failed=false;try{step(p,2);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT expedition_mirror_fault");}
                Check(failed && CountId(p,710259)==11 && CountId(p,721920)==1 && p.Entity.Money==money+100 && stage(p)==2,"Desert Expedition failed mirror insertion rolls back horns/tool/payment");}
            Check(step(p,2) && !step(p,2) && CountId(p,710259)==1 && CountId(p,721920)==0 && CountId(p,721921)==1,"Desert Expedition consumes ten earned horns and pill for mirror "+life);
            p=ReloadBall(p);Check(stage(p)==3 && (uint)Field(Call(store,null,"Load",p),"Soul")==soul && !recover(p,721920),"Desert Expedition persisted stage/soul prevents obsolete recovery "+life);
            for(int j=0;j<2;j++)material(p,710261);Check(!step(p,3),"Desert Expedition needs three earned ghosts "+life);material(p,710261);
            GameState owner=p;Parallel.For(0,4,j=>step(owner,3));Check(stage(p)==4 && CountId(p,710261)==0 && CountId(p,721921)==0 && p.Entity.Money==money+300,"Desert Expedition three ghosts and mirror hand in exactly once "+life);
            p=ReloadBall(p);Check(stage(p)==4 && p.Entity.Money==money+300 && !step(p,4),"Desert Expedition saved referral payouts cannot skip heart/report "+life);
        }
    }
    static void VerifyDesertExpeditionPack(Map town){
        VerifySingleEquipmentPack(town,728250,150219,"Desert Expedition");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};
        foreach(byte job in jobs){
            uint id=job==135 || job==145?152229u:150219u;var p=ReloadBall(WelcomePlayer(town,140,0,job));var source=NestedPack(p,728250,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728250)==0 && CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound,"Desert Expedition ordinary pack picks level-110 ring or level-115 Taoist bracelet: "+job);
            p=ReloadBall(p);Check(CountId(p,id)==1 && FixedUses(p)==1,"Desert Expedition equipment and claim persist: "+job);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728250,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728250)==1 && FixedUses(unsupported)==0,"Desert Expedition unknown class keeps pack unopened");
    }
    static void VerifyFamilyFeudPack(Map town){
        VerifySingleEquipmentPack(town,728248,120189,"Family Feud");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};
        foreach(byte job in jobs){
            uint id=job==135 || job==145?121189u:120189u;var p=ReloadBall(WelcomePlayer(town,140,0,job));var source=NestedPack(p,728248,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728248)==0 && CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound,"Family Feud ordinary pack picks level-97 Super class necklace: "+job);
            p=ReloadBall(p);Check(CountId(p,id)==1 && FixedUses(p)==1,"Family Feud necklace and claim persist: "+job);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728248,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728248)==1 && FixedUses(unsupported)==0,"Family Feud unknown class keeps pack unopened");
    }
    static void VerifyTomboyPack(Map town){
        VerifySingleEquipmentPack(town,728241,120158,"Tomboy Marriage");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};
        foreach(byte job in jobs){
            uint id=job==135 || job==145?121158u:120158u;var p=ReloadBall(WelcomePlayer(town,140,0,job));var source=NestedPack(p,728241,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728241)==0 && CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound,"Tomboy ordinary pack picks level-82 class necklace: "+job);
            p=ReloadBall(p);Check(CountId(p,id)==1 && FixedUses(p)==1,"Tomboy necklace and claim persist: "+job);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728241,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728241)==1 && FixedUses(unsupported)==0,"Tomboy unknown class keeps pack unopened");
    }
    static void VerifyGhostCityPack(Map town){
        VerifySingleEquipmentPack(town,728246,130078,"Ghost City");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] armor={130078,131078,133068,135078,136078,139078,138078,134078,134078,101078};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728246,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728246)==0 && CountId(p,armor[i])==1 && p.Inventory.Objects.Single(x=>x.ID==armor[i]).Bound,"Ghost City earned-pack dispatcher chooses class armor: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,armor[i])==1 && FixedUses(p)==1,"Ghost City class armor and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728246,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728246)==1 && FixedUses(unsupported)==0,"Ghost City unknown class retains the unopened pack");
    }
    static void VerifyGhostCityStore(string runtime,Map town,bool visit=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityStore",true);Call(store,null,"Initialize");
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,uint,bool> fire=(p,item)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Firework",p,item,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
        ushort[,] points={{440,308},{443,344},{465,356}};
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)75:(byte)140,life,15));ulong silver=p.Entity.Money;
            Check(step(p,0) && !step(p,0) && !step(p,2) && stage(p)==1,"Ghost City acceptance and ordered referral: "+life);
            uint soul=(uint)Scalar("SELECT soul FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
            Check(step(p,1) && !step(p,1) && p.Entity.Money==silver+100,"Ghost City Aunt pays referral once: "+life);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!step(p,2) && stage(p)==2 && p.Entity.Money==silver+100,"Ghost City Wang retains stage and silver when bag full");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT ghost_firework_fault CHECK(id<>721717) NOT VALID");bool failed=false;try{step(p,2);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT ghost_firework_fault");}Check(failed && stage(p)==2 && CountId(p,721717)==0 && p.Entity.Money==silver+100,"Ghost City firework insert failure rolls back payment and stage");}
            Check(step(p,2) && !step(p,2) && stage(p)==3 && CountId(p,721717)==1 && p.Inventory.Objects.Single(i=>i.ID==721717).Bound,"Ghost City Wang issues one bound tracked firework: "+life);
            uint item=p.Inventory.Objects.Single(i=>i.ID==721717).UID;p.BindWorldMap(1002);p.Entity.X=269;p.Entity.Y=215;Check(!fire(p,item) && !step(p,3),"Ghost City old guide location and incomplete report rejected: "+life);
            p.Entity.X=465;p.Entity.Y=356;Check(!fire(p,item),"Ghost City later firework spot cannot skip first: "+life);
            p.Entity.X=440;p.Entity.Y=308;Check(!fire(p,item+1) && fire(p,item) && !fire(p,item),"Ghost City first marked spot credits exact UID once: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(Scalar("SELECT sparks FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==1,"Ghost City firework progress persists: "+life);
            Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)item);p=ReloadBall(p);p.Entity.X=443;p.Entity.Y=344;Check(!recover(p) && !fire(p,item),"Ghost City stored firework cannot be copied or used: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)item);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=item},p);
            Check(recover(p) && !recover(p) && Scalar("SELECT sparks FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==1,"Ghost City lost firework replacement preserves visited spots: "+life);item=p.Inventory.Objects.Single(i=>i.ID==721717).UID;
            p.Entity.X=443;p.Entity.Y=344;Check(fire(p,item) && !fire(p,item),"Ghost City second firework spot credits once: "+life);p.Entity.X=465;p.Entity.Y=356;Check(fire(p,item) && !fire(p,item) && Scalar("SELECT sparks FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==3,"Ghost City third firework completes ordered search: "+life);
            Check(step(p,3) && !step(p,3) && CountId(p,721717)==0 && stage(p)==4 && p.Entity.Money==silver+300,"Ghost City report consumes exact firework and pays once: "+life);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT ghost_charm_fault CHECK(id<>721713) NOT VALID");bool failed=false;try{step(p,4);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT ghost_charm_fault");}Check(failed && stage(p)==4 && CountId(p,721713)==0 && p.Entity.Money==silver+300,"Ghost City Charm failure preserves referral payment and stage");}
            Check(step(p,4) && !step(p,4) && stage(p)==5 && p.Entity.Money==silver+400 && CountId(p,721713)==1 && p.Inventory.Objects.Single(i=>i.ID==721713).Bound,"Ghost City Shine grants one bound Charm: "+life);
            uint charm=p.Inventory.Objects.Single(i=>i.ID==721713).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)charm);p=ReloadBall(p);Check(!recover(p),"Ghost City stored Charm is not duplicated: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)charm);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=charm},p);
            Check(recover(p) && !recover(p) && p.Inventory.Objects.Single(i=>i.ID==721713).UID!=charm,"Ghost City lost Charm receives one tracked replacement: "+life);
            p=ReloadBall(p);Check(stage(p)==5 && Scalar("SELECT sparks FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==3 && Scalar("SELECT charm_uid FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==p.Inventory.Objects.Single(i=>i.ID==721713).UID && Scalar("SELECT soul FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==soul && p.Entity.Money==silver+400,"Ghost City frozen soul Charm counters and payments persist: "+life);if(visit)VerifyGhostCityVisit(p,town);
        }
    }
    static void VerifyGhostCityQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityQuest",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.GhostCityScenes",true);
        Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc==900071727 || npc==4503 || npc==4504 || npc==4505)p.BindWorldMap(1002);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(npc==4503?actor.X:actor.X-2);p.Entity.Y=(ushort)(npc==4503?actor.Y-2:actor.Y);p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,id)=>ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==id),p);
        Action<GameState> journal=p=>{int st=stage(p),current=st<=4?st:st<=6?5:st<=8?6:st-2;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1787 && (uint)x.UID<=1794).ToArray();Check(own.Length==8 && own.All(x=>x.Status==(current>(int)x.UID-1786?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1786?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Ghost ordinary saved journal stage "+st);};
        Action<GameState> enter=p=>{p.BindWorldMap(1002);p.Entity.X=440;p.Entity.Y=308;use(p,721713);Check(!Fenced(p) && p.Entity.MapID>=11000 && (bool)Call(scenes,null,"HasOwner",p.Entity.UID),"Ghost ordinary Charm dispatch enters private city: "+p.Entity.Reborn);};
        Action tick=()=>{now=now.AddSeconds(3);Call(quest,null,"Tick");};
        Action<GameState> kill=p=>{var b=p.Map.Entities.Values.First(x=>!x.Dead);p.Entity.X=b.X;p.Entity.Y=b.Y;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);tick();};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            var low=ReloadBall(WelcomePlayer(town,74,0,15));talk(low,900071727,121);Check(stage(low)==0,"Ghost ordinary level-74 character cannot accept");
            for(byte life=0;life<3;life++){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)75:(byte)140,life,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
                p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=121},p);Check(stage(p)==0,"Ghost remote acceptance rejected: "+life);
                for(uint id=1787;id<=1794;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Ghost forged journal Begin rejected: "+id);}
                journal(p);talk(p,900071727,121);talk(p,4505,1);Check(stage(p)==1,"Ghost Shine cannot skip referrals: "+life);journal(p);talk(p,4503,1);journal(p);talk(p,4504,1);journal(p);
                Check(stage(p)==3 && CountId(p,721717)==1 && p.Entity.Money==money+200,"Ghost ordinary referrals issue bound firework and pay once: "+life);
                p.Entity.X=465;p.Entity.Y=356;use(p,721717);Check(Scalar("SELECT sparks FROM revora_ghost_city WHERE uid=@uid",p.Entity.UID)==0,"Ghost out-of-order firework dispatch rejected: "+life);
                ushort[,] points={{440,308},{443,344},{465,356}};for(int i=0;i<3;i++){p.Entity.X=points[i,0];p.Entity.Y=points[i,1];use(p,721717);use(p,721717);}
                talk(p,4504,1);journal(p);talk(p,4505,1);Check(stage(p)==5 && CountId(p,721717)==0 && CountId(p,721713)==1 && p.Entity.Money==money+400,"Ghost ordinary Wang report and Shine Charm persist: "+life);journal(p);
                p.Entity.X=300;p.Entity.Y=300;use(p,721713);Check(!(bool)Call(scenes,null,"HasOwner",p.Entity.UID) && p.Entity.MapID==1002 && !Fenced(p),"Ghost wrong gate releases denied scene reservation: "+life);
                enter(p);
                if(life==0){now=now.AddMinutes(20);Call(quest,null,"Tick");Check(p.Entity.MapID==1002 && stage(p)==5 && CountId(p,721713)==1,"Ghost normal tick expires first visit without losing Charm");enter(p);}
                else if(life==1){EntityTable.SaveEntity(p);p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);Check(p.Entity.MapID==1002 && !(bool)Call(scenes,null,"HasOwner",p.Entity.UID),"Ghost normal login retires interrupted scene");enter(p);}
                talk(p,9917911,1);Check(stage(p)==5 && CountId(p,721712)==0,"Ghost ordinary pillar cannot skip Mo Mo: "+life);talk(p,4526,1);talk(p,9917911,1);
                Check(stage(p)==6 && p.Entity.MapID==1002 && CountId(p,721712)==1 && !(bool)Call(scenes,null,"HasOwner",p.Entity.UID),"Ghost ordinary pillar issues Powder and returns safely: "+life);journal(p);
                uint powder=p.Inventory.Objects.Single(i=>i.ID==721712).UID;ItemHandler.DropItem(new ItemUsage(true){UID=powder},p);talk(p,4505,2);talk(p,4505,2);Check(CountId(p,721712)==1 && p.Inventory.Objects.Single(i=>i.ID==721712).UID!=powder,"Ghost Shine replaces missing earned Powder once: "+life);
                talk(p,4505,1);journal(p);enter(p);tick();kill(p);
                EntityTable.SaveEntity(p);p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);Check(p.Entity.MapID==1002 && Scalar("SELECT kills FROM revora_ghost_combat WHERE uid=@uid",p.Entity.UID)==1,"Ghost ordinary reconnect preserves combat count: "+life);enter(p);tick();
                for(int i=1;i<102;i++){kill(p);if(Fenced(p))throw new Exception("Ghost normal combat fenced owner at "+i);}
                Check(stage(p)==8 && CountId(p,721714)==1 && CountId(p,721712)==0,"Ghost ordinary final boss automatically grants earned Bezoar: "+life);journal(p);
                for(uint npc=9917930;npc<9917935;npc++){talk(p,npc,1);talk(p,npc,1);}Check(Scalar("SELECT COUNT(*) FROM revora_ghost_children WHERE uid=@uid",p.Entity.UID)==5 && CountId(p,721714)==1,"Ghost ordinary optional child NPCs credit once: "+life);
                talk(p,4526,1);Check(stage(p)==9 && p.Entity.MapID==1002 && CountId(p,721714)==0 && CountId(p,721713)==0 && !(bool)Call(scenes,null,"HasOwner",p.Entity.UID),"Ghost ordinary Mo Mo hand-in consumes materials and returns: "+life);journal(p);
                talk(p,4503,1);talk(p,4503,1);Check(stage(p)==10 && p.Entity.Money==money+6600,"Ghost Aunt final report pays total 6600 silver once: "+life);journal(p);
                if(life==0){Query("ALTER TABLE items ADD CONSTRAINT ghost_quest_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,121);}finally{Query("ALTER TABLE items DROP CONSTRAINT ghost_quest_fault");}Check(Fenced(p) && stage(p)==10 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728246,723911)",p.Entity.UID)==0,"Ghost failed final NPC claim fences owner without partial rewards");p=ReloadBall(p);Call(quest,null,"OnLogin",p);}
                talk(p,900071727,121);talk(p,900071727,121);Check(stage(p)==11 && CountId(p,728246)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Ghost Chang grants bound armor EXP Ball and P7 once: "+life);journal(p);
                p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,900071727,122);use(p,728246);Check(stage(p)==11 && CountId(p,130078)==1 && p.Inventory.Objects.Single(i=>i.ID==130078).Bound && CountId(p,723911)==1,"Ghost completed quest and ordinary armor opening survive reload: "+life);
            }
        }finally{clock.SetValue(null,old);}
    }
    static void VerifyMadManQuest(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManQuest",true);var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);
        World(runtime,1020);Call(FixedPacks,null,"Initialize");Call(quest,null,"Initialize");CityGuardSystem.SpawnTalker(900071727,1002,473,300,12090,"TaskmasterChang",0);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object old=clock.GetValue(null),oldRoll=roll.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_mad_man WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{p.BindWorldMap(npc==4468?(ushort)1020:(ushort)1002);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action<GameState,uint> use=(p,id)=>ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==id),p);
        Action<GameState> fang=p=>{p.BindWorldMap(1020);p.Entity.X=255;p.Entity.Y=504;var b=HuntMonster(p,"Snakeman");b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);Call(quest,null,"OnDeath",p,b);};
        Action<GameState> journal=p=>{int st=stage(p),current=st<=4?st:st<=7?5:st==8?6:7;var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(x=>(uint)x.UID>=1781 && (uint)x.UID<=1786).ToArray();Check(own.Length==6 && own.All(x=>x.Status==(current>(int)x.UID-1780?MsgTaskStatus.QuestData.QuestStatus.Finished:current==(int)x.UID-1780?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available)),"Mad ordinary saved journal stage "+st);};
        Action<GameState> enter=p=>{talk(p,4470,0);use(p,729981);use(p,729980);Check(!Fenced(p) && stage(p)==6 && (bool)Call(scenes,null,"HasOwner",p.Entity.UID) && p.Entity.MapID>=11000 && Scalar("SELECT COUNT(*) FROM revora_mad_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Mad ordinary Blue-Green item dispatch enters one private trial: "+p.Entity.Reborn);};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(n=>0));
        var low=ReloadBall(WelcomePlayer(town,59,0,15));talk(low,900071727,111);Check(stage(low)==0,"Mad ordinary level-59 character cannot accept");
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)60:(byte)140,life,15));p.LoadItemStats();p.Quests=new Quests(p);p.Quests.Load();ulong money=p.Entity.Money;
            p.ActiveNpc=900071727;p.Entity.X=300;p.Entity.Y=300;Npcs.GetDialog(new NpcRequest(true){NpcID=900071727,OptionID=111},p);Check(stage(p)==0,"Mad remote NPC acceptance rejected: "+life);
            for(uint id=1781;id<=1786;id++){var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);Check(!p.Quests.HasQuest((QuestID)id),"Mad forged Begin rejected: "+id);}
            talk(p,900071727,111);talk(p,4470,1);Check(stage(p)==1,"Mad Dong cannot skip Du San and Judd: "+life);journal(p);talk(p,4468,1);talk(p,1611,1);fang(p);
            Check(stage(p)==3 && CountId(p,729978)==1 && p.Inventory.Objects.Single(i=>i.ID==729978).Bound,"Mad normal combat grants one bound fang without replay: "+life);
            talk(p,1611,1);talk(p,4470,1);Check(stage(p)==5 && p.Entity.Money==money+400 && CountId(p,729978)==0 && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1),"Mad ordinary referrals and fang extraction issue three toxins: "+life);journal(p);
            p.Entity.X=300;p.Entity.Y=300;use(p,729981);Check(Scalar("SELECT COUNT(*) FROM revora_mad_toxins WHERE uid=@uid AND state=1",p.Entity.UID)==0,"Mad remote toxin use cannot select or consume: "+life);
            talk(p,4470,0);use(p,729979);talk(p,4470,3);Check(Scalar("SELECT COUNT(*) FROM revora_mad_toxins WHERE uid=@uid AND state=1",p.Entity.UID)==0 && CountId(p,729979)==1,"Mad Dong cancels selection without consuming it: "+life);
            p.Entity.Hitpoints=Math.Max(1,p.Entity.MaxHitpoints/2);use(p,729979);use(p,729981);Check(p.Entity.Hitpoints==p.Entity.MaxHitpoints && stage(p)==5 && CountId(p,729979)==0 && CountId(p,729981)==0,"Mad wrong ordered recipe heals through ordinary item dispatch: "+life);
            now=now.AddSeconds(20);fang(p);talk(p,4470,1);Check(CountId(p,729978)==0 && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1) && p.Entity.Money==money+400,"Mad ordinary fresh-fang extraction replenishes spent toxins without another payment: "+life);
            enter(p);journal(p);
            if(life==0){while(p.Inventory.Count<40)Pack(p,1088000,1);now=now.AddMinutes(5);Call(quest,null,"Tick");Check(p.Entity.MapID==1020 && p.Entity.X==552 && p.Entity.Y==599 && stage(p)==6,"Mad ordinary timeout evacuates even with full bag");foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);talk(p,4470,2);Check(stage(p)==5 && CountId(p,729980)==1 && CountId(p,729981)==1,"Mad Dong recovery refunds interrupted pair after bag cleared");enter(p);}
            else if(life==1){EntityTable.SaveEntity(p);p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);Check(stage(p)==5 && !(bool)Call(scenes,null,"HasOwner",p.Entity.UID) && CountId(p,729980)==1 && CountId(p,729981)==1,"Mad ordinary reconnect retires trial and refunds once");enter(p);}
            p.ActiveNpc=9917810;Npcs.GetDialog(new NpcRequest(true){NpcID=9917810,OptionID=0},p);Check(CountId(p,729983)==0,"Mad remote stone cannot grant powder: "+life);
            for(uint npc=9917810;npc<9917817;npc++){var stone=p.Map.Npcs[npc];p.Entity.X=(ushort)(stone.X-2);p.Entity.Y=stone.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=0},p);Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=0},p);Check(CountId(p,729983+npc-9917810)==1,"Mad normal stone interaction grants each colour once: "+npc+" life "+life);}
            use(p,729983);Check(stage(p)==7 && CountId(p,729982)==1 && p.Entity.MapID==1020 && p.Entity.X==552 && p.Entity.Y==599 && !(bool)Call(scenes,null,"HasOwner",p.Entity.UID),"Mad ordinary powder use combines and returns to Du San: "+life);journal(p);
            uint rainbow=p.Inventory.Objects.Single(i=>i.ID==729982).UID;ItemHandler.DropItem(new ItemUsage(true){UID=rainbow},p);talk(p,4468,2);talk(p,4468,2);Check(CountId(p,729982)==1 && p.Inventory.Objects.Single(i=>i.ID==729982).UID!=rainbow,"Mad Du San replaces lost earned Rainbow once: "+life);
            talk(p,4468,1);talk(p,4468,1);Check(stage(p)==8 && p.Entity.Money==money+5400 && CountId(p,729982)==0,"Mad normal hand-in pays total 5400 silver once: "+life);journal(p);
            if(life==0){Query("ALTER TABLE items ADD CONSTRAINT mad_quest_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");try{talk(p,900071727,111);}finally{Query("ALTER TABLE items DROP CONSTRAINT mad_quest_fault");}Check(Fenced(p) && stage(p)==8 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728245,723911)",p.Entity.UID)==0,"Mad failed final NPC claim fences owner without partial rewards");p=ReloadBall(p);Call(quest,null,"OnLogin",p);}
            talk(p,900071727,111);talk(p,900071727,111);Check(stage(p)==9 && CountId(p,728245)==1 && CountId(p,723911)==1 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && !Fenced(p),"Mad Chang grants bound headgear EXP ball and P7 once: "+life);journal(p);
            p=ReloadBall(p);Call(quest,null,"OnLogin",p);talk(p,900071727,112);use(p,728245);Check(stage(p)==9 && CountId(p,118068)==1 && p.Inventory.Objects.Single(i=>i.ID==118068).Bound && CountId(p,723911)==1,"Mad completed quest and ordinary headgear opening survive reload: "+life);
        }}finally{clock.SetValue(null,old);roll.SetValue(null,oldRoll);}
    }
    static void VerifyMadManStore(string runtime,Map town,bool toxins=false,bool collection=false,bool travel=false,bool rewards=false,bool effects=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);World(runtime,1020);Call(store,null,"Initialize");
        var roll=store.GetField("Roll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);object old=roll.GetValue(null);
        Func<GameState,int,bool> step=(p,a)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Advance",p,a,(Func<bool>)(()=>true));};
        Func<GameState,Entity,bool> credit=(p,b)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,b,(Func<bool>)(()=>true));};
        Func<GameState,bool> recover=p=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Recover",p,(Func<bool>)(()=>true));};
        Func<GameState,long> stage=p=>Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID);
        Func<GameState,string,Entity> kill=(p,name)=>{var b=HuntMonster(p,name);b.MapObjType=MapObjectType.Monster;b.Hitpoints=b.MaxHitpoints=b.MonsterInfo.Hitpoints;b.IsDropped=true;uint damage=b.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,b,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=b.UID},ref damage,null);return b;};
        try{roll.SetValue(null,(Func<int,int>)(n=>0));
        for(byte life=0;life<3;life++){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)60:(byte)140,life,15));ulong money=p.Entity.Money;p.BindWorldMap(1020);p.Entity.X=255;p.Entity.Y=504;
            Check(step(p,0) && !step(p,0) && stage(p)==1,"Mad acceptance once per life: "+life);uint soul=(uint)Scalar("SELECT soul FROM revora_mad_man WHERE uid=@uid",p.Entity.UID);
            var early=kill(p,"Snakeman");Check(!credit(p,early) && !step(p,2),"Mad early hunt and skipped referral rejected: "+life);
            Check(step(p,1) && !step(p,1) && step(p,2) && !step(p,2) && stage(p)==3 && p.Entity.Money==money+200,"Mad referrals ordered and paid once: "+life);
            Check(!credit(p,early) && !credit(p,kill(p,"Macaque")) && !step(p,3),"Mad old corpse wrong family and absent fang rejected: "+life);
            if(life==0){roll.SetValue(null,(Func<int,int>)(n=>50));var miss=kill(p,"Snakeman");Check(!credit(p,miss),"Mad explicit fifty-percent fang chance misses at 50");roll.SetValue(null,(Func<int,int>)(n=>49));Check(!credit(p,miss),"Mad failed roll cannot be replayed");while(p.Inventory.Count<40)Pack(p,1088000,1);var full=kill(p,"Snakeman");Check(!credit(p,full),"Mad full bag refuses fang");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);Check(!credit(p,full),"Mad full-bag corpse cannot grant later");}
            var snake=kill(p,"Snakeman");Check(credit(p,snake) && !credit(p,snake) && !credit(p,kill(p,"Snakeman")) && CountId(p,729978)==1 && p.Inventory.Objects.Single(i=>i.ID==729978).Bound,"Mad real death grants one bound tracked fang: "+life);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(step(p,3) && !step(p,3) && stage(p)==4 && p.Entity.Money==money+300,"Mad Judd inspection persists and pays once: "+life);
            if(life==0){uint fang=p.Inventory.Objects.Single(i=>i.ID==729978).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)fang);p=ReloadBall(p);Check(!recover(p) && !step(p,4),"Mad stored fang cannot be copied or extracted");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)fang);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=fang},p);Check(recover(p) && !recover(p) && CountId(p,729978)==0 && credit(p,kill(p,"Snakeman")) && !step(p,3),"Mad lost inspected fang requires rehunt without repeated payment");
                while(p.Inventory.Count<39)Pack(p,1088000,1);Check(!step(p,4) && stage(p)==4 && CountId(p,729978)==1,"Mad three-toxin extraction requires two net free slots");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
                Query("ALTER TABLE items ADD CONSTRAINT mad_green_fault CHECK(id<>729980) NOT VALID");bool failed=false;try{step(p,4);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mad_green_fault");}Check(failed && stage(p)==4 && CountId(p,729978)==1 && CountId(p,729979)==0 && p.Entity.Money==money+300 && Scalar("SELECT COUNT(*) FROM revora_mad_toxins WHERE uid=@uid",p.Entity.UID)==0,"Mad failed green toxin rolls back red toxin fang payment and tracking");}
            Check(step(p,4) && !step(p,4) && stage(p)==5 && p.Entity.Money==money+400 && CountId(p,729978)==0 && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound),"Mad Dong issues three bound tracked toxins once: "+life);
            uint green=p.Inventory.Objects.Single(i=>i.ID==729980).UID;Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)green);p=ReloadBall(p);Check(!recover(p),"Mad stored toxin cannot be replaced: "+life);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)green);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=green},p);
            Check(recover(p) && !recover(p) && CountId(p,729980)==1 && p.Inventory.Objects.Single(i=>i.ID==729980).UID!=green,"Mad lost unused toxin gets one replacement: "+life);
            p=ReloadBall(p);Check(stage(p)==5 && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1) && Scalar("SELECT COUNT(*) FROM revora_mad_toxins WHERE uid=@uid AND state=0",p.Entity.UID)==3 && Scalar("SELECT soul FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==soul,"Mad extraction replacement and frozen reward survive reload: "+life);
            if(effects)VerifyMadManEffects(p,town);else if(travel)VerifyMadManTravel(p,town,rewards);else if(toxins)VerifyMadManToxins(p,town,collection);
        }}finally{roll.SetValue(null,old);}
    }
    static void VerifyMadManToxins(GameState p,Map town,bool collection=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);
        uint[,] pairs={{729981,729980,7},{729981,729979,2},{729979,729981,3},{729979,729980,4},{729980,729981,5},{729980,729979,6}};
        for(int i=0;i<6;i++)Check(Convert.ToInt32(Call(store,null,"Recipe",pairs[i,0],pairs[i,1]))==pairs[i,2],"Mad ordered recipe result: "+pairs[i,0]+" then "+pairs[i,1]);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);object scene=null;
        Func<uint,object,int> use=(uid,room)=>{lock(p.ItemSyncRoot){var result=Call(store,null,"UseToxin",p,uid,room,(Func<bool>)(()=>true));return Convert.ToInt32(Field(result,"Outcome"));}};
        Func<bool> cancel=()=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"CancelSelection",p,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));uint blue=p.Inventory.Objects.Single(i=>i.ID==729981).UID,green=p.Inventory.Objects.Single(i=>i.ID==729980).UID;
            Check(use(blue,null)==1 && use(blue,null)==0 && CountId(p,729981)==1,"Mad first selection is persisted but not consumed or replayed: "+p.Entity.Reborn);
            Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)blue);p=ReloadBall(p);Check(use(green,null)==0 && cancel() && !cancel(),"Mad stored first toxin cannot mix and selection cancellation is idempotent: "+p.Entity.Reborn);Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)blue);p=ReloadBall(p);
            Check(use(blue,null)==1 && use(green,null)==0 && CountId(p,729981)==1 && CountId(p,729980)==1,"Mad travel recipe refuses consumption without owned scene: "+p.Entity.Reborn);
            EntityTable.SaveEntity(p);p=ReloadBall(p);Check(use(blue,null)==0,"Mad first toxin choice survives reconnect: "+p.Entity.Reborn);
            var other=ReloadBall(WelcomePlayer(town,140,0,15));object foreign;lock(other.ItemSyncRoot)foreign=Call(scenes,null,"Create",other);try{Check(use(green,foreign)==0,"Mad recipe refuses another player's scene: "+p.Entity.Reborn);}finally{Call(scenes,null,"Release",foreign);}
            lock(p.ItemSyncRoot)scene=Call(scenes,null,"Create",p);
            if(p.Entity.Reborn==0){Query("ALTER TABLE revora_mad_mixes ADD CONSTRAINT mad_mix_fault CHECK(outcome<>7) NOT VALID");bool failed=false;try{use(green,scene);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_mad_mixes DROP CONSTRAINT mad_mix_fault");}Check(failed && CountId(p,729981)==1 && CountId(p,729980)==1 && Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==5 && Scalar("SELECT COUNT(*) FROM revora_mad_trials WHERE uid=@uid",p.Entity.UID)==0,"Mad receipt failure rolls back both toxins stage and trial reservation");}
            Check(use(green,scene)==7 && use(green,scene)==0 && CountId(p,729981)==0 && CountId(p,729980)==0 && CountId(p,729979)==1,"Mad Blue then Green commits once and preserves unused Red: "+p.Entity.Reborn);
            var trial=Call(store,null,"ActiveTrial",p);Check(trial!=null && (Guid)Field(trial,"ID")== (Guid)Field(scene,"Visit") && (DateTime)Field(trial,"Until")==now.AddMinutes(5) && (ushort)Field(trial,"Map")==((Map)Field(scene,"Map")).ID,"Mad saved trial is bound to exact scene and five-minute deadline: "+p.Entity.Reborn);
            if(collection)VerifyMadManCollection(p,scene,now);
            Call(scenes,null,"Release",scene);scene=null;p=ReloadBall(p);Check((Call(store,null,"ActiveTrial",p)!=null)==!collection && Scalar("SELECT COUNT(*) FROM revora_mad_mixes WHERE uid=@uid AND outcome=7 AND applied=false",p.Entity.UID)==1 && !cancel(),"Mad reserved travel and pending effect receipt survive reload without duplicate use: "+p.Entity.Reborn);
        }finally{if(scene!=null)Call(scenes,null,"Release",scene);clock.SetValue(null,old);}
    }
    static void VerifyMadManCollection(GameState p,object scene,DateTime now,bool remain=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var map=(Map)Field(scene,"Map");
        Func<int,bool> collect=i=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Collect",p,scene,9917810u+(uint)i,(Func<bool>)(()=>true));};
        Func<uint,bool> combine=uid=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"Combine",p,scene,uid,(Func<bool>)(()=>true));};
        Action<int> approach=i=>{var n=map.Npcs[9917810u+(uint)i];p.ActiveNpc=n.UID;p.Entity.X=(ushort)(n.X-2);p.Entity.Y=n.Y;};
        if(!remain){Check(!collect(0),"Mad collection rejects owner outside private scene: "+p.Entity.Reborn);p.BindWorldMap(map.ID);scene.GetType().GetField("Entered",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(scene,true);}
        try{
            approach(0);p.Entity.X=553;p.Entity.Y=584;Check(!collect(0),"Mad stone requires nearby interaction: "+p.Entity.Reborn);approach(0);
            if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!collect(0) && Scalar("SELECT mask FROM revora_mad_trials WHERE uid=@uid AND state=0",p.Entity.UID)==0,"Mad full bag preserves uncollected colour");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);}
            var original=map.Npcs[9917810];map.Npcs[9917810]=new NpcSpawn{UID=9917810,MapID=map.ID,X=original.X,Y=original.Y};try{Check(!collect(0),"Mad collection rejects replaced stone object: "+p.Entity.Reborn);}finally{map.Npcs[9917810]=original;}
            for(int i=0;i<7;i++){approach(i);Check(collect(i) && !collect(i) && CountId(p,729983u+(uint)i)==1 && p.Inventory.Objects.Single(x=>x.ID==729983u+(uint)i).Bound,"Mad stone grants one bound distinct colour: "+i+" life "+p.Entity.Reborn);if(i==0)Check(!combine(p.Inventory.Objects.Single(x=>x.ID==729983).UID),"Mad incomplete collection cannot combine: "+p.Entity.Reborn);}
            uint red=p.Inventory.Objects.Single(i=>i.ID==729983).UID;ItemHandler.DropItem(new ItemUsage(true){UID=red},p);approach(0);Check(collect(0) && !collect(0) && Scalar("SELECT mask FROM revora_mad_trials WHERE uid=@uid AND state=0",p.Entity.UID)==127,"Mad missing powder replacement preserves completed colour mask: "+p.Entity.Reborn);
            red=p.Inventory.Objects.Single(i=>i.ID==729983).UID;clock.SetValue(null,(Func<DateTime>)(()=>now.AddMinutes(5)));Check(!combine(red),"Mad composition rejects exact five-minute expiry: "+p.Entity.Reborn);clock.SetValue(null,(Func<DateTime>)(()=>now));
            if(p.Entity.Reborn==0){Query("ALTER TABLE items ADD CONSTRAINT mad_rainbow_fault CHECK(id<>729982) NOT VALID");bool failed=false;try{combine(red);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mad_rainbow_fault");}Check(failed && Enumerable.Range(0,7).All(i=>CountId(p,729983u+(uint)i)==1) && Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==6,"Mad failed Rainbow insertion restores all seven powders and active stage");}
            Check(combine(red) && !combine(red) && !collect(0) && CountId(p,729982)==1 && p.Inventory.Objects.Single(i=>i.ID==729982).Bound && Enumerable.Range(0,7).All(i=>CountId(p,729983u+(uint)i)==0),"Mad all-seven exchange yields one tracked bound Rainbow Powder: "+p.Entity.Reborn);
            Check(Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==7 && Scalar("SELECT COUNT(*) FROM revora_mad_trials WHERE uid=@uid AND state=1",p.Entity.UID)==1,"Mad composition commits quest and attempt completion together: "+p.Entity.Reborn);
        }finally{clock.SetValue(null,(Func<DateTime>)(()=>now));if(!remain){p.BindWorldMap(1002);EntityTable.SaveEntity(p);}}
    }
    static void VerifyMadManTravel(GameState p,Map town,bool rewards=false){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);Call(scenes,null,"InitializeTravel");CityGuardSystem.SpawnTalker(4468,1020,552,601,5520,"DuSan",0);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object old=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);object scene=null;
        Action reserve=()=>{lock(p.ItemSyncRoot){uint blue=p.Inventory.Objects.Single(i=>i.ID==729981).UID,green=p.Inventory.Objects.Single(i=>i.ID==729980).UID;Call(store,null,"UseToxin",p,blue,null,(Func<bool>)(()=>true));scene=Call(scenes,null,"Create",p);var r=Call(store,null,"UseToxin",p,green,scene,(Func<bool>)(()=>true));Check(Convert.ToInt32(Field(r,"Outcome"))==7,"Mad travel reserves earned Blue-Green recipe: "+p.Entity.Reborn);}};
        Action enter=()=>{lock(p.ItemSyncRoot)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true));};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));reserve();var map=(Map)Field(scene,"Map");ushort previous=p.Entity.MapID;p.Entity.Teleport(map.ID,553,584);Check(p.Entity.MapID==previous,"Mad naked teleport cannot enter reserved trial: "+p.Entity.Reborn);
            var other=ReloadBall(WelcomePlayer(town,140,0,15));other.Entity.Teleport(map.ID,553,584);Check(other.Entity.MapID!=map.ID,"Mad unrelated player cannot teleport into trial: "+p.Entity.Reborn);enter();enter();
            Check(p.Entity.MapID==map.ID && ReferenceEquals(p.Map,map) && Scalar("SELECT COUNT(*) FROM revora_mad_visits WHERE uid=@uid AND active",p.Entity.UID)==1,"Mad actual entry saves one owned visit: "+p.Entity.Reborn);
            var stone=map.Npcs[9917810];p.ActiveNpc=stone.UID;p.Entity.X=(ushort)(stone.X-2);p.Entity.Y=stone.Y;lock(p.ItemSyncRoot)Check((bool)Call(store,null,"Collect",p,scene,stone.UID,(Func<bool>)(()=>true)),"Mad actual visit can earn its first powder: "+p.Entity.Reborn);
            if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);now=now.AddSeconds(299);Call(scenes,null,"Tick");Check(p.Entity.MapID==map.ID,"Mad trial does not return before deadline");now=now.AddSeconds(1);Call(scenes,null,"Tick");Check(p.Entity.MapID==1020 && p.Entity.X==552 && p.Entity.Y==599 && Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==6,"Mad expiry safely returns before a full bag can defer refunds");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);Call(scenes,null,"RecoverLogin",p);}
            else if(p.Entity.Reborn==1){EntityTable.SaveEntity(p);uint uid=p.Entity.UID;Kernel.GamePool.Remove(uid);Call(scenes,null,"Tick");Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",uid)==1020,"Mad offline cleanup repairs saved location");p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);}
            else{EntityTable.SaveEntity(p);p=ReloadBall(p);Check(p.Entity.MapID==1002,"Mad entity loader evacuates private map to its safe fallback");Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Call(scenes,null,"RecoverLogin",p);Check(p.Entity.MapID==1002 && Scalar("SELECT COUNT(*) FROM revora_mad_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"Mad replacement-session recovery preserves safe fallback and retires visit");}
            Check((bool)Field(scene,"Released") && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==5 && CountId(p,729980)==1 && CountId(p,729981)==1 && CountId(p,729983)==0,"Mad interrupted trial removes partial powders and refunds exact pair once: "+p.Entity.Reborn);scene=null;Call(scenes,null,"RecoverLogin",p);Check(CountId(p,729980)==1 && CountId(p,729981)==1,"Mad refund replay cannot duplicate toxins: "+p.Entity.Reborn);
            now=now.AddMinutes(1);reserve();enter();VerifyMadManCollection(p,scene,now,true);Call(scenes,null,"Tick");Check((bool)Field(scene,"Released") && p.Entity.MapID==1020 && p.Entity.X==552 && p.Entity.Y==599 && CountId(p,729982)==1 && Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==7,"Mad completed collection returns through actual travel with Rainbow intact: "+p.Entity.Reborn);scene=null;if(rewards)VerifyMadManRewards(p);
        }finally{if(scene!=null && !(bool)Field(scene,"Released")){lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}clock.SetValue(null,old);}
    }
    static void VerifyMadManEffects(GameState p,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManScenes",true);var looks=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestAppearance",true);
        var clock=scenes.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object previous=clock.GetValue(null);var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);
        Func<string,bool> use=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<Guid,bool> apply=id=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,"ApplyMixture",p,id,(Func<bool>)(()=>true));};
        Func<uint,uint,Guid> mix=(a,b)=>{lock(p.ItemSyncRoot){Call(store,null,"UseToxin",p,p.Inventory.Objects.Single(i=>i.ID==a).UID,null,(Func<bool>)(()=>true));var r=Call(store,null,"UseToxin",p,p.Inventory.Objects.Single(i=>i.ID==b).UID,null,(Func<bool>)(()=>true));return (Guid)Field(r,"Receipt");}};
        Func<bool> hunt=()=>{p.BindWorldMap(1020);p.Entity.X=255;p.Entity.Y=504;var body=HuntMonster(p,"Snakeman");body.MapObjType=MapObjectType.Monster;body.Hitpoints=body.MaxHitpoints=body.MonsterInfo.Hitpoints;body.IsDropped=true;uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);lock(p.ItemSyncRoot)return (bool)Call(store,null,"Hunt",p,body,(Func<bool>)(()=>true));};
        uint[][] pairs={new uint[]{729981,729979},new uint[]{729979,729981},new uint[]{729979,729980},new uint[]{729980,729981},new uint[]{729980,729979}};
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));
            for(int i=0;i<pairs.Length;i++){
                p.BindWorldMap(1002);p.Entity.X=431;p.Entity.Y=329;uint ordinary=p.Entity.Mesh;p.LoadItemStats();p.Entity.Hitpoints=Math.Max(1,p.Entity.MaxHitpoints/2);if(!EntityTable.SaveEntity(p) || p.Entity.MaxHitpoints<2)throw new Exception("Mad effect health fixture failed");
                uint before=p.Entity.Hitpoints;Guid receipt=mix(pairs[i][0],pairs[i][1]);
                Check(receipt!=Guid.Empty && CountId(p,pairs[i][0])==0 && CountId(p,pairs[i][1])==0 && !apply(Guid.NewGuid()),"Mad wrong mixture consumes earned pair and rejects unknown receipt: "+i+" life "+p.Entity.Reborn);
                Check(!use("Replenish"),"Mad wrong mixture cannot refill without a fresh earned fang: "+i+" life "+p.Entity.Reborn);
                if(i==1 && p.Entity.Reborn==0){
                    Query("CREATE FUNCTION suppress_mad_effect() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_mad_effect BEFORE UPDATE ON revora_mad_mixes FOR EACH ROW EXECUTE FUNCTION suppress_mad_effect()");bool failed=false;
                    try{apply(receipt);}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_mad_effect ON revora_mad_mixes;DROP FUNCTION suppress_mad_effect()");}
                    Check(failed && p.Entity.Hitpoints==before && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",p.Entity.UID)==before,"Mad suppressed healing receipt rolls back saved health");
                }
                if(i==2){p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma,60,true);Check(!apply(receipt),"Mad mixture preserves an existing strength effect: "+p.Entity.Reborn);p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Stigma);}
                Check(apply(receipt) && !apply(receipt),"Mad effect acknowledgement applies once: "+i+" life "+p.Entity.Reborn);
                if(i==0)Check(p.Entity.ToxicFogLeft==10 && p.Entity.ToxicFogPercent==0.02f && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Poisoned),"Mad poison uses existing bounded ToxicFog path: "+p.Entity.Reborn);
                if(i==1){Check(p.Entity.Hitpoints==p.Entity.MaxHitpoints && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.MaxHitpoints,"Mad recovery heals and persists maximum health: "+p.Entity.Reborn);p.Entity.Hitpoints--;Check(!apply(receipt) && p.Entity.Hitpoints==p.Entity.MaxHitpoints-1,"Mad healing receipt cannot heal later damage again: "+p.Entity.Reborn);}
                if(i==2)Check(p.Entity.StigmaTime==20 && p.Entity.StigmaIncrease==1.15f && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma),"Mad strength uses existing Stigma path: "+p.Entity.Reborn);
                if(i==3)Check(p.Entity.FlyTime==20 && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly),"Mad floating uses existing flight path: "+p.Entity.Reborn);
                if(i==4)Check(p.Entity.Mesh==4352 && p.Entity.TransformationID==0,"Mad zombie uses owned cosmetic mesh without changing combat transformation: "+p.Entity.Reborn);
                Check(!hunt(),"Mad replacement fang cannot be earned before effect deadline: "+i+" life "+p.Entity.Reborn);
                now=now.AddSeconds(20);Call(looks,null,"Tick");p.Entity.ToxicFogLeft=0;p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned);p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Stigma);p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
                if(i==4)Check(p.Entity.Mesh==ordinary,"Mad zombie restores original appearance: "+p.Entity.Reborn);
                Check(hunt() && CountId(p,729978)==1,"Mad spent recipe permits a new earned fang after deadline: "+i+" life "+p.Entity.Reborn);
                if(i==0 && p.Entity.Reborn==0){
                    while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!use("Replenish") && CountId(p,729978)==1,"Mad full bag retains replenishment fang");foreach(var item in p.Inventory.Objects.Where(x=>x.ID==1088000).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
                    Query("ALTER TABLE items ADD CONSTRAINT mad_refill_fault CHECK(id<>729981) NOT VALID");bool failed=false;try{use("Replenish");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mad_refill_fault");}
                    Check(failed && CountId(p,729978)==1 && CountId(p,729979)==0 && CountId(p,729981)==0,"Mad refill insertion failure restores fang and both spent entitlements");
                }
                Check(use("Replenish") && !use("Replenish") && CountId(p,729978)==0 && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(x=>x.ID==id).Bound),"Mad fresh fang replenishes exact spent pair once: "+i+" life "+p.Entity.Reborn);
                EntityTable.SaveEntity(p);p=ReloadBall(p);Check(!apply(receipt) && new uint[]{729979,729980,729981}.All(id=>CountId(p,id)==1),"Mad acknowledged effect and replacement toxins survive reload: "+i+" life "+p.Entity.Reborn);now=now.AddSeconds(1);
            }
            p.BindWorldMap(1002);var expired=mix(729981,729979);now=now.AddSeconds(20);Check(apply(expired) && p.Entity.ToxicFogLeft==0 && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Poisoned),"Mad expired pending receipt retires without starting a late effect: "+p.Entity.Reborn);
        }finally{clock.SetValue(null,previous);Call(looks,null,"Tick");}
    }
    static void VerifyMadManRewards(GameState p){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.MadManStore",true);
        Func<string,bool> use=name=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Func<long> stage=()=>Scalar("SELECT stage FROM revora_mad_man WHERE uid=@uid",p.Entity.UID);
        uint rainbow=p.Inventory.Objects.Single(i=>i.ID==729982).UID;ulong silver=p.Entity.Money;
        Check(!use("Claim"),"Mad Chang cannot pay before Du San hand-in: "+p.Entity.Reborn);
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)rainbow);p=ReloadBall(p);
        Check(!use("Recover") && !use("HandInPowder"),"Mad stored Rainbow cannot be copied or handed in: "+p.Entity.Reborn);
        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)rainbow);p=ReloadBall(p);ItemHandler.DropItem(new ItemUsage(true){UID=rainbow},p);
        if(p.Entity.Reborn==0){while(p.Inventory.Count<40)Pack(p,1088000,1);Check(!use("Recover") && stage()==7,"Mad full bag preserves lost Rainbow entitlement");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);}
        Check(use("Recover") && !use("Recover") && p.Inventory.Objects.Single(i=>i.ID==729982).UID!=rainbow,"Mad earned lost Rainbow recovers exactly once: "+p.Entity.Reborn);
        rainbow=p.Inventory.Objects.Single(i=>i.ID==729982).UID;p=ReloadBall(p);Check(p.Inventory.ContainsUID(rainbow) && stage()==7,"Mad replacement Rainbow persists: "+p.Entity.Reborn);
        if(p.Entity.Reborn==0){
            Query("CREATE FUNCTION suppress_mad_reward() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_mad_reward BEFORE UPDATE ON revora_mad_man FOR EACH ROW EXECUTE FUNCTION suppress_mad_reward()");
            bool failed=false;ulong prior=p.Entity.Experience;try{use("HandInPowder");}catch(InvalidOperationException){failed=true;}finally{Query("DROP TRIGGER suppress_mad_reward ON revora_mad_man;DROP FUNCTION suppress_mad_reward()");}
            Check(failed && stage()==7 && p.Inventory.ContainsUID(rainbow) && p.Entity.Money==silver && p.Entity.Experience==prior && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)silver,"Mad suppressed hand-in rolls back powder silver and experience");
        }
        byte level=p.Entity.Level;ulong experience=p.Entity.Experience,award=level>=Config.LevelCap?0:p.ExpBall/6*5+(p.ExpBall%6)*5/6;
        Check(use("HandInPowder") && !use("HandInPowder") && !use("Recover") && stage()==8 && !p.Inventory.ContainsUID(rainbow) && p.Entity.Money==silver+5000,"Mad Du San pays 5000 silver once and consumes exact Rainbow: "+p.Entity.Reborn);
        ulong gained=p.Entity.Experience;for(byte l=level;l<p.Entity.Level;l++)gained+=DataHolder.LevelExperience(l);
        Check(gained==experience+award,"Mad hand-in grants five-sixths EXP ball, respecting level cap: "+p.Entity.Reborn);
        p=ReloadBall(p);Check(stage()==8 && p.Entity.Money==silver+5000 && !use("HandInPowder"),"Mad hand-in payment and progression survive reload: "+p.Entity.Reborn);
        uint soul=(uint)Scalar("SELECT soul FROM revora_mad_man WHERE uid=@uid",p.Entity.UID);
        if(p.Entity.Reborn==0){
            while(p.Inventory.Count<38)Pack(p,1088000,1);Check(!use("Claim") && stage()==8 && CountId(p,728245)==0,"Mad final claim requires three free slots");foreach(var i in p.Inventory.Objects.Where(i=>i.ID==1088000).ToArray())p.Inventory.Remove(i,Enums.ItemUse.Remove);
            Query("ALTER TABLE items ADD CONSTRAINT mad_final_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");bool failed=false;try{use("Claim");}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE items DROP CONSTRAINT mad_final_fault");}
            Check(failed && stage()==8 && CountId(p,728245)==0 && CountId(p,723911)==0 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid AND id IN(728245,723911)",p.Entity.UID)==0,"Mad P7 insert failure rolls back pack EXP ball and completion");
        }
        int claims=0;System.Threading.Tasks.Parallel.For(0,4,i=>{if(use("Claim"))System.Threading.Interlocked.Increment(ref claims);});
        Check(claims==1 && stage()==9 && !use("Claim"),"Mad concurrent final claims pay once: "+p.Entity.Reborn);
        p=ReloadBall(p);Check(stage()==9 && new uint[]{728245,723911,soul}.All(id=>CountId(p,id)==1 && p.Inventory.Objects.Single(i=>i.ID==id).Bound) && Scalar("SELECT cardinality(rewards) FROM revora_mad_man WHERE uid=@uid",p.Entity.UID)==3 && !use("Claim"),"Mad bound headgear pack EXP ball frozen P7 and three receipts persist: "+p.Entity.Reborn);
    }
    static void VerifyMadManPack(Map town){
        VerifySingleEquipmentPack(town,728245,118068,"Mad Man");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] headgear={118068,111068,113038,112068,143068,144068,148068,114068,114068,170068};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728245,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728245)==0 && CountId(p,headgear[i])==1 && p.Inventory.Objects.Single(x=>x.ID==headgear[i]).Bound,"Mad Man earned-pack dispatcher chooses class headgear: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,headgear[i])==1 && FixedUses(p)==1,"Mad Man class headgear and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728245,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728245)==1 && FixedUses(unsupported)==0,"Mad Man unknown class retains the unopened pack");
    }
    static void VerifyFranticPack(Map town){
        VerifySingleEquipmentPack(town,728243,410108,"Frantic");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] weapon={410108,510108,500098,601108,610108,611108,617108,421108,421108,626108};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728243,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728243)==0 && CountId(p,weapon[i])==1 && p.Inventory.Objects.Single(x=>x.ID==weapon[i]).Bound,"Frantic earned-pack dispatcher chooses class weapon: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,weapon[i])==1 && FixedUses(p)==1,"Frantic class weapon and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728243,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728243)==1 && FixedUses(unsupported)==0,"Frantic unknown class retains the unopened pack");
    }
    static void VerifyArtisanPack(Map town){
        VerifySingleEquipmentPack(town,728239,410097,"Artisan");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] weapon={410097,510097,500087,601097,610097,611097,617097,421087,421087,626097};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728239,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728239)==0 && CountId(p,weapon[i])==1 && p.Inventory.Objects.Single(x=>x.ID==weapon[i]).Bound,"Artisan earned-pack dispatcher chooses class weapon: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,weapon[i])==1 && FixedUses(p)==1,"Artisan class weapon and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728239,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728239)==1 && FixedUses(unsupported)==0,"Artisan unknown class retains the unopened pack");
    }
    static void VerifySpunGoldPack(Map town){
        VerifySingleEquipmentPack(town,728237,130027,"Spun Gold");
        byte[] jobs={15,25,45,55,65,75,85,135,145,165};uint[] armor={130027,131027,133017,135027,136027,139027,138027,134027,134027,101027};
        for(int i=0;i<jobs.Length;i++){
            var p=ReloadBall(WelcomePlayer(town,140,0,jobs[i]));var source=NestedPack(p,728237,1,true);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && CountId(p,728237)==0 && CountId(p,armor[i])==1 && p.Inventory.Objects.Single(x=>x.ID==armor[i]).Bound,"Spun Gold earned-pack dispatcher chooses class armor: "+jobs[i]);
            p=ReloadBall(p);Check(CountId(p,armor[i])==1 && FixedUses(p)==1,"Spun Gold class armor and receipt survive reload: "+jobs[i]);
        }
        var unsupported=ReloadBall(WelcomePlayer(town,140,0,15));unsupported.Entity.Class=99;EntityTable.SaveEntity(unsupported);var keep=NestedPack(unsupported,728237,1,true);OpenFixed(unsupported,keep);
        Check(!Fenced(unsupported) && CountId(unsupported,728237)==1 && FixedUses(unsupported)==0,"Spun Gold unknown class retains the unopened pack");
    }

    static void VerifyAzureRingPack(Map town) { VerifySingleEquipmentPack(town,728238,151077,"Azure"); }
    static void VerifyConnectorPack(Map town) { VerifySingleEquipmentPack(town,728236,120046,"Connector"); }
    static void VerifySingleEquipmentPack(Map town,uint sourceType,uint outputType,string label) {
        Call(FixedPacks,null,"Initialize");
        foreach(bool bound in new[]{false,true}) {
            var p=ReloadBall(WelcomePlayer(town,140,2,15));var pack=NestedPack(p,sourceType,2,bound);
            Parallel.For(0,4,i=>OpenFixed(p,pack));
            Check(!Fenced(p) && CountId(p,sourceType)==0 && CountId(p,outputType)==2 && FixedUses(p)==2
                && p.Inventory.Objects.Where(i=>i.ID==outputType).All(i=>i.Bound==bound && i.Plus==0),
                label+" ring pack delivers one catalog equipment item per unit with source binding: "+bound);
            var ids=p.Inventory.Objects.Where(i=>i.ID==outputType).Select(i=>i.UID).OrderBy(i=>i).ToArray();p=ReloadBall(p);
            Check(p.Inventory.Objects.Where(i=>i.ID==outputType && i.Bound==bound).Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids)
                && FixedUses(p)==2,label+" ring exact outputs and receipts survive reload: "+bound);
        }
        var full=ReloadBall(WelcomePlayer(town,140,2,15));var single=NestedPack(full,sourceType,1,true);
        while(full.Inventory.Count<40)Pack(full,1088001,1);OpenFixed(full,single);
        Check(!Fenced(full) && full.Inventory.Count==40 && CountId(full,outputType)==1 && CountId(full,sourceType)==0,
            label+" singleton replaces itself in a full bag");
        var stacked=ReloadBall(WelcomePlayer(town,140,2,15));var source=NestedPack(stacked,sourceType,2,true);
        while(stacked.Inventory.Count<40)Pack(stacked,1088001,1);OpenFixed(stacked,source);
        Check(!Fenced(stacked) && source.StackSize==2 && CountId(stacked,outputType)==0 && FixedUses(stacked)==0,
            label+" stacked pack requires space for its remainder");
        var fault=ReloadBall(WelcomePlayer(town,140,2,15));var failed=NestedPack(fault,sourceType,1,true);uint packUid=failed.UID;
        Query("ALTER TABLE items ADD CONSTRAINT azure_ring_fault CHECK(id<>"+outputType.ToString(System.Globalization.CultureInfo.InvariantCulture)+") NOT VALID");
        try{OpenFixed(fault,failed);}finally{Query("ALTER TABLE items DROP CONSTRAINT azure_ring_fault");}
        Check(Fenced(fault) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",packUid)==1 && FixedUses(fault)==0,
            label+" ring failure retains the source pack and no receipt");
        fault=ReloadBall(fault);OpenFixed(fault,fault.Inventory.Objects.Single(i=>i.UID==packUid));
        Check(!Fenced(fault) && CountId(fault,outputType)==1 && FixedUses(fault)==1,label+" failed pack retries once after reconnect");
    }

    static void VerifyMarketErrand(Map town) {
        var service=typeof(MarketErrandSystem);var clock=service.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);
        var now=new DateTime(2035,1,1,12,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        CpShopClerkSystem.Create();
        var market=World(Environment.CurrentDirectory,1036);bool addPath=!DMaps.MapPaths.ContainsKey(1036);
        if(addPath)DMaps.MapPaths.Add(1036,"market-errand-fixture");
        try {
            foreach(string mode in new[]{"success","item-fault","saved-full","external-rvp"}) {
                var p=ReloadBall(WelcomePlayer(town,140,2,15));EntityVariableTable.Load(p);
                p.Entity.X=CpShopClerkSystem.X;p.Entity.Y=CpShopClerkSystem.Y;p.ActiveNpc=CpShopClerkSystem.NpcId;
                uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
                p.Entity.PreviousMapID=1036;
                Check(((string)Call(service,null,"Act",p,true)).StartsWith("Walk through"),"Market Errand ordinary clerk acceptance: "+mode);
                Call(service,null,"Act",p,false);
                Check((int)p["market-errand"]==1 && p.Entity.ConquerPoints==cp,"Market Errand rejects visits predating acceptance: "+mode);
                var xy=market.RandomCoordinates();p.Entity.Teleport(1036,xy.Item1,xy.Item2);
                Check((int)p["market-errand"]==3,"Market Errand accepted teleport records durable visit: "+mode);
                p.Entity.Teleport(1002,CpShopClerkSystem.X,CpShopClerkSystem.Y);EntityTable.SaveEntity(p);
                p=ReloadBall(p);EntityVariableTable.Load(p);p.ActiveNpc=CpShopClerkSystem.NpcId;
                Check((int)p["market-errand"]==3,"Market Errand visit survives reconnect: "+mode);
                if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT market_rune_fault CHECK(id<>4030101) NOT VALID");
                if(mode=="saved-full")for(int i=0;i<40;i++){var item=Pack(p,1088000,1);p.Inventory.Remove(item,Enums.ItemUse.None,true);}
                if(mode=="external-rvp")Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)p.Entity.UID);
                try{Call(service,null,"Act",p,false);}finally{if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT market_rune_fault");}
                if(mode!="success") {
                    Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                        && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rp+(mode=="external-rvp"?7:0)
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=4030101",p.Entity.UID)==0,
                        "Market Errand failed claim grants nothing: "+mode);
                    if(mode=="saved-full")Query("DELETE FROM items WHERE entityid=@uid AND id=1088000","uid",(long)p.Entity.UID);
                    p=ReloadBall(p);EntityVariableTable.Load(p);p.ActiveNpc=CpShopClerkSystem.NpcId;
                    Check((int)p["market-errand"]==3,"Market Errand failed claim retains visited contract: "+mode);
                    Call(service,null,"Act",p,false);
                }
                var rune=p.Inventory.Objects.Single(i=>i.ID==4030101);uint runeUid=rune.UID;
                Check(!Fenced(p) && rune.Bound && (int)p["market-errand"]==2
                    && p.Entity.ConquerPoints==cp+Revora.Config.MarketErrandCp
                    && p.Entity.RevoraPoint==rp+Revora.Config.MarketErrandRevoraPoints+(mode=="external-rvp"?7:0)
                    && p.Entity.Money==gold+Revora.Config.MarketErrandGold,
                    "Market Errand pays one bound daily rune and configured purse: "+mode);
                Check(EntityTable.SaveEntity(p),"Market Errand committed balances autosave: "+mode);
                p=ReloadBall(p);EntityVariableTable.Load(p);p.ActiveNpc=CpShopClerkSystem.NpcId;
                Parallel.For(0,4,i=>Call(service,null,"Act",p,false));Call(service,null,"Act",p,true);
                Check((int)p["market-errand"]==2 && p.Inventory.Objects.Count(i=>i.ID==4030101 && i.UID==runeUid && i.Bound)==1
                    && p.Entity.ConquerPoints==cp+Revora.Config.MarketErrandCp,
                    "Market Errand daily receipt and exact rune survive reload/replay: "+mode);
                if(mode=="success") {
                    now=now.AddDays(1);Call(service,null,"Act",p,true);Call(service,null,"Act",p,false);
                    Check((int)p["market-errand"]==1 && p.Inventory.Objects.Count(i=>i.ID==4030101)==1,
                        "Market Errand next UTC day requires a fresh visit before another prize");
                    now=now.AddDays(-1);
                }
            }
        }finally{clock.SetValue(null,oldClock);if(addPath)DMaps.MapPaths.Remove(1036);}
    }

    static void VerifyNoblePrizes(Map town) {
        var service=typeof(NobleWarSystem);Call(service,null,"InitializeRewards");
        var brackets=(Array)service.GetField("Brackets",Hidden).GetValue(null);
        var cycle=service.GetField("CycleStart",Hidden);object oldCycle=cycle.GetValue(null);
        var date=new DateTime(2033,1,1,15,0,0);
        try {
            for(int index=0;index<4;index++) {
                var b=brackets.GetValue(index);var bt=b.GetType();
                var p=ReloadBall(WelcomePlayer(town,140,2,15));
                uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
                uint awardCp=(uint)bt.GetProperty("CpPrize").GetValue(b,null),awardRp=(uint)bt.GetProperty("RevoraPointPrize").GetValue(b,null);
                ulong awardGold=(ulong)bt.GetProperty("GoldPrize").GetValue(b,null);
                cycle.SetValue(null,date);bt.GetField("Running").SetValue(b,true);
                ((System.Collections.Generic.HashSet<uint>)bt.GetField("Alive").GetValue(b)).Add(p.Entity.UID);
                Call(service,null,"Finish",b,p);
                Check(!Fenced(p) && p.Entity.ConquerPoints==cp+awardCp && p.Entity.RevoraPoint==rp+awardRp && p.Entity.Money==gold+awardGold,
                    "NobleWar bracket "+index+" automatically pays configured purse");
                var soul=p.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);
                Check(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7
                    && Scalar("SELECT soul_uid FROM revora_noble_prizes WHERE uid=@uid",p.Entity.UID)==soul.UID,
                    "NobleWar bracket "+index+" stores exact bound P7 receipt");
                Check(EntityTable.SaveEntity(p),"NobleWar committed balances accept autosave: "+index);
                uint soulUid=soul.UID;p=ReloadBall(p);
                Check(p.Entity.ConquerPoints==cp+awardCp && p.Entity.RevoraPoint==rp+awardRp && p.Entity.Money==gold+awardGold
                    && p.Inventory.Objects.Any(i=>i.UID==soulUid && i.Bound),"NobleWar exact reward survives full reload: "+index);
                Parallel.For(0,4,i=>Call(service,null,"TryClaim",p));Call(service,null,"Finish",b,p);
                Check(p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000)==1
                    && Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid",p.Entity.UID)==1,"NobleWar claim and result replay grant nothing: "+index);
            }
            var bracket=brackets.GetValue(0);var type=bracket.GetType();
            foreach(string mode in new[]{"publish-fault","item-fault","saved-full","external-rvp"}) {
                var p=ReloadBall(WelcomePlayer(town,140,2,15));uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;
                date=date.AddDays(1);cycle.SetValue(null,date);type.GetField("Running").SetValue(bracket,true);
                ((System.Collections.Generic.HashSet<uint>)type.GetField("Alive").GetValue(bracket)).Add(p.Entity.UID);
                if(mode=="publish-fault")Query("ALTER TABLE revora_noble_prizes ADD CONSTRAINT noble_publish_fault CHECK(false) NOT VALID");
                if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT noble_item_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                if(mode=="saved-full")for(int i=0;i<40;i++){var item=Pack(p,1088000,1);p.Inventory.Remove(item,Enums.ItemUse.None,true);}
                if(mode=="external-rvp")Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)p.Entity.UID);
                try{Call(service,null,"Finish",bracket,p);}finally{
                    if(mode=="publish-fault")Query("ALTER TABLE revora_noble_prizes DROP CONSTRAINT noble_publish_fault");
                    if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT noble_item_fault");
                }
                if(mode=="publish-fault") {
                    Check((bool)type.GetField("Running").GetValue(bracket)
                        && Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid",p.Entity.UID)==0,
                        "NobleWar failed publication retains pending winner");
                    var pending=type.GetField("Pending").GetValue(bracket);
                    uint frozen=(uint)pending.GetType().GetField("Soul").GetValue(pending);
                    Kernel.GamePool.Remove(p.Entity.UID);Call(service,null,"SweepAndCheck",bracket,false);
                    Check(!(bool)type.GetField("Running").GetValue(bracket)
                        && Scalar("SELECT soul_type FROM revora_noble_prizes WHERE uid=@uid",p.Entity.UID)==frozen,
                        "NobleWar retry publishes frozen prize after winner disconnects");
                }
                Check(Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid AND NOT paid",p.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0
                    && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rp+(mode=="external-rvp"?7:0),
                    "NobleWar defers without partial payout: "+mode);
                if(mode=="saved-full")Query("DELETE FROM items WHERE entityid=@uid AND id=1088000","uid",(long)p.Entity.UID);
                p=ReloadBall(p);
                Check(((string)Call(service,null,"TryClaim",p)).Contains("bound P7")
                    && Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid AND paid",p.Entity.UID)==1,
                    "NobleWar deferred prize claims after reconnect: "+mode);
            }
            // Real Duke admission and elimination route on fixture-supplied map terrain.
            Query("CREATE TABLE IF NOT EXISTS npcs AS TABLE public.npcs WITH NO DATA;CREATE TABLE IF NOT EXISTS sobnpcs AS TABLE public.sobnpcs WITH NO DATA;CREATE TABLE IF NOT EXISTS furniture AS TABLE public.furniture WITH NO DATA;CREATE TABLE IF NOT EXISTS monsterspawns AS TABLE public.monsterspawns WITH NO DATA");
            var arena=World(Environment.CurrentDirectory,700);bool addPath=!DMaps.MapPaths.ContainsKey(700);
            if(addPath)DMaps.MapPaths.Add(700,"noble-fixture");
            var duke=brackets.GetValue(2);var dukeType=duke.GetType();object oldArena=dukeType.GetField("Arena").GetValue(duke);
            try {
                dukeType.GetField("Arena").SetValue(duke,arena);cycle.SetValue(null,date.AddDays(1));service.GetField("RegisterOpen",Hidden).SetValue(null,true);
                var a=ReloadBall(WelcomePlayer(town,140,2,15));var b=ReloadBall(WelcomePlayer(town,140,2,15));
                foreach(var p in new[]{a,b}){p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Duke;p.Entity.MaxHitpoints=10000;p.Entity.Hitpoints=10000;}
                a.Entity.PKMode=Enums.PKMode.Peace;b.Entity.PKMode=Enums.PKMode.Capture;
                Check(((string)Call(service,null,"TryRegister",a)).StartsWith("You are registered") && ((string)Call(service,null,"TryRegister",b)).StartsWith("You are registered"),
                    "NobleWar ordinary Duke players register");
                Check((bool)Call(service,null,"StartBracket",duke) && a.Entity.MapID==arena.ID && b.Entity.MapID==arena.ID,
                    "NobleWar ordinary Duke bracket enters arena");
                Check(Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND pkmode=1 AND NOT returned",a.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND pkmode=3 AND NOT returned",b.Entity.UID)==1,
                    "NobleWar saves admission modes before combat");
                b.Entity.Hitpoints=0;NobleWarSystem.OnDeath(b);
                Check(!(bool)dukeType.GetField("Running").GetValue(duke) && a.Entity.MapID==1002
                    && Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid AND paid",a.Entity.UID)==1,
                    "NobleWar last survivor earns and receives durable prize through death callback");
                Check(a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND ended AND returned",a.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND ended AND returned",b.Entity.UID)==1,
                    "NobleWar winner and loser return with original PK modes and saved acknowledgements");
                a=ReloadBall(a);Check(a.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000 && i.Bound),"NobleWar earned P7 survives reconnect");
                var admissions=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobleAdmissions");
                var c=ReloadBall(WelcomePlayer(town,140,2,15));var d=ReloadBall(WelcomePlayer(town,140,2,15));
                c.Entity.PKMode=Enums.PKMode.Team;d.Entity.PKMode=Enums.PKMode.Capture;
                Query("ALTER TABLE revora_noble_admissions ADD CONSTRAINT noble_admit_fault CHECK(pkmode<>3) NOT VALID");
                bool rejected=false;
                try{Call(admissions,null,"Begin","203401011500-9",arena.ID,new[]{c,d});}catch(Exception){rejected=true;}
                finally{Query("ALTER TABLE revora_noble_admissions DROP CONSTRAINT noble_admit_fault");}
                Check(rejected && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid",c.Entity.UID)==0,
                    "NobleWar partial admission failure rolls back the complete roster");
                Call(admissions,null,"Begin","203401011500-9",arena.ID,new[]{c,d});
                rejected=false;try{Call(admissions,null,"Begin","203401021500-9",arena.ID,new[]{c,d});}catch(Exception){rejected=true;}
                Check(rejected && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid",c.Entity.UID)==1,
                    "NobleWar pending admission blocks another event entry");
                c.Entity.Teleport(arena.BaseID,arena.ID,50,50);d.Entity.Teleport(arena.BaseID,arena.ID,51,50);
                c.Entity.PKMode=Enums.PKMode.PK;d.Entity.PKMode=Enums.PKMode.PK;
                EntityTable.SaveEntity(c);EntityTable.SaveEntity(d);
                Call(admissions,null,"RecoverInterrupted");
                c=ReloadBall(c);NobleWarSystem.OnLogin(c);
                Check(!Fenced(c) && c.Entity.MapID==1002 && c.Entity.PKMode==Enums.PKMode.Team
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND ended AND returned",c.Entity.UID)==1,
                    "NobleWar interrupted admission returns replacement owner and original mode");
                Check(Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid",c.Entity.UID)==0,
                    "NobleWar interruption does not invent a winner");
                Query("ALTER TABLE revora_noble_admissions ADD CONSTRAINT noble_return_fault CHECK(NOT returned OR pkmode<>3) NOT VALID");
                d=ReloadBall(d);
                try{NobleWarSystem.OnLogin(d);}finally{Query("ALTER TABLE revora_noble_admissions DROP CONSTRAINT noble_return_fault");}
                Check(Fenced(d) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",d.Entity.UID)==1002
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND ended AND NOT returned",d.Entity.UID)==1,
                    "NobleWar failed return acknowledgement preserves retry after saving safe town");
                d=ReloadBall(d);NobleWarSystem.OnLogin(d);
                Check(!Fenced(d) && d.Entity.PKMode==Enums.PKMode.Capture
                    && Scalar("SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND returned",d.Entity.UID)==1,
                    "NobleWar failed return retries on reconnect");
                Check(Scalar("SELECT count(*) FROM revora_noble_prizes WHERE uid=@uid AND paid",a.Entity.UID)==1,
                    "NobleWar interrupted-admission recovery retains completed prizes");
            } finally {dukeType.GetField("Arena").SetValue(duke,oldArena);service.GetField("RegisterOpen",Hidden).SetValue(null,false);if(addPath)DMaps.MapPaths.Remove(700);}
        } finally {cycle.SetValue(null,oldCycle);foreach(var b in brackets){b.GetType().GetField("Running").SetValue(b,false);b.GetType().GetField("Pending").SetValue(b,null);}}
    }

    static void VerifyDuelResults(Map town) {
        var service=typeof(DuelPitSystem);Call(service,null,"InitializeResults");
        var winners=Enumerable.Range(0,4).Select(i=>ReloadBall(WelcomePlayer(town,140,2,15))).ToArray();
        foreach(var p in winners)EntityVariableTable.Load(p);
        var scores=(System.Collections.Generic.Dictionary<uint,uint>)service.GetField("Wins",Hidden).GetValue(null);
        var eliminated=(System.Collections.Generic.Dictionary<uint,int>)service.GetField("ElimRound",Hidden).GetValue(null);
        var originalCycle=service.GetField("CycleStart",Hidden).GetValue(null);
        var originalMap=service.GetField("EventMap",Hidden).GetValue(null);
        bool addDuelPath=!DMaps.MapPaths.ContainsKey(700);
        try {
            service.GetField("EventMap",Hidden).SetValue(null,null);
            for(int run=0;run<2;run++) {
                scores.Clear();eliminated.Clear();
                for(int i=0;i<4;i++){scores[winners[i].Entity.UID]=(uint)(4-i);eliminated[winners[i].Entity.UID]=4-i;}
                service.GetField("King",Hidden).SetValue(null,winners[0].Entity.UID);
                service.GetField("Running",Hidden).SetValue(null,true);
                service.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2032,1,1,11+run*8,15,0));
                Kernel.GamePool.Remove(winners[1].Entity.UID);
                if(run==0)Query("ALTER TABLE revora_duel_results ADD CONSTRAINT duel_result_fault CHECK(place<>3) NOT VALID");
                try{Call(service,null,"Finish");}
                finally{if(run==0)Query("ALTER TABLE revora_duel_results DROP CONSTRAINT duel_result_fault");}
                if(run==0) {
                    Check(Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid",winners[0].Entity.UID)==0
                        && (bool)service.GetField("Running",Hidden).GetValue(null),"Duel result insertion fault rolls back every placement and retains retry");
                    // The retry must publish the frozen result, not changed mutable standings.
                    scores[winners[3].Entity.UID]=999;
                    Call(service,null,"Finish");
                }
                Check(!(bool)service.GetField("Running",Hidden).GetValue(null)
                    && Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND place=2",winners[1].Entity.UID)==run+1,
                    "Duel results preserve offline second place without shifting ranks: "+run);
                Check(Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND place=4",winners[3].Entity.UID)==run+1,
                    "Duel publication preserves frozen fourth-place result: "+run);
                Call(service,null,"Finish");
                Check(Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid",winners[0].Entity.UID)==run+1,
                    "Duel duplicate finish cannot mint another placement: "+run);
                winners[1]=ReloadBall(winners[1]);EntityVariableTable.Load(winners[1]);
            }
            var owner=winners[1];
            // Legacy outstanding entitlement must remain ahead of queued results.
            owner[DuelPitSystem.ClaimKey]=4u;EntityVariableTable.Save(owner);
            Check((bool)Call(service,null,"HasClaim",owner) && (uint)owner[DuelPitSystem.ClaimKey]==4
                && Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND imported",owner.Entity.UID)==0,
                "Duel pending legacy claim is not overwritten by later results");
            Call(service,null,"TryClaim",owner);
            Query("ALTER TABLE revora_duel_results ADD CONSTRAINT duel_import_fault CHECK(NOT imported) NOT VALID");
            try{Call(service,null,"HasClaim",owner);}finally{Query("ALTER TABLE revora_duel_results DROP CONSTRAINT duel_import_fault");}
            owner=ReloadBall(owner);EntityVariableTable.Load(owner);
            Check((uint)owner[DuelPitSystem.ClaimKey]==0
                && Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND imported",owner.Entity.UID)==0,
                "Duel failed import rolls back vault and result together");
            for(int i=0;i<2;i++) {
                Check((bool)Call(service,null,"HasClaim",owner) && (uint)owner[DuelPitSystem.ClaimKey]==2,
                    "Duel oldest saved offline placement imports after reconnect: "+i);
                owner=ReloadBall(owner);EntityVariableTable.Load(owner);
                Check(((string)Call(service,null,"TryClaim",owner)).Contains("bound P7"),"Duel imported placement survives reconnect and pays: "+i);
            }
            owner=ReloadBall(owner);EntityVariableTable.Load(owner);
            Check(!(bool)Call(service,null,"HasClaim",owner)
                && owner.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000)==3,
                "Duel drains legacy plus two queued prizes exactly once across reconnects");
            Query("CREATE TABLE IF NOT EXISTS npcs AS TABLE public.npcs WITH NO DATA;CREATE TABLE IF NOT EXISTS sobnpcs AS TABLE public.sobnpcs WITH NO DATA;CREATE TABLE IF NOT EXISTS furniture AS TABLE public.furniture WITH NO DATA;CREATE TABLE IF NOT EXISTS monsterspawns AS TABLE public.monsterspawns WITH NO DATA");
            var arena=World(Environment.CurrentDirectory,700);
            if(addDuelPath)DMaps.MapPaths.Add(700,"duel-fixture");
            service.GetField("EventMap",Hidden).SetValue(null,arena);
            service.GetField("RegisterOpen",Hidden).SetValue(null,true);
            service.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2032,1,2,11,15,0));
            var a=ReloadBall(WelcomePlayer(town,140,2,15));var b=ReloadBall(WelcomePlayer(town,140,2,15));
            EntityVariableTable.Load(a);EntityVariableTable.Load(b);
            a.Entity.PKMode=Enums.PKMode.Peace;b.Entity.PKMode=Enums.PKMode.Capture;
            a.Entity.MaxHitpoints=10000;a.Entity.Hitpoints=10000;b.Entity.MaxHitpoints=10000;b.Entity.Hitpoints=10000;
            Check(((string)Call(service,null,"TryRegister",a)).StartsWith("You are registered")
                && ((string)Call(service,null,"TryRegister",b)).StartsWith("You are registered"),"Duel ordinary fighters register through the production service");
            Call(service,null,"StartFight");
            Check(a.Entity.MapID==arena.ID && b.Entity.MapID==arena.ID,"Duel registered fighters enter arena through StartFight");
            Check(Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND pkmode=1 AND NOT ended AND NOT returned",a.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND pkmode=3 AND NOT ended AND NOT returned",b.Entity.UID)==1,
                "Duel admission records original modes before combat");
            uint challenger=(uint)service.GetField("Challenger",Hidden).GetValue(null);
            var loser=challenger==a.Entity.UID?a:b;var victor=ReferenceEquals(loser,a)?b:a;
            loser.Entity.Hitpoints=0;DuelPitSystem.OnDeath(loser);
            Check(scores[victor.Entity.UID]==1 && loser.Entity.MapID==1002,"Duel death callback records earned win and returns loser");
            var until=(DateTime)service.GetField("WaitUntil",Hidden).GetValue(null);
            Call(service,null,"Pulse",until.AddSeconds(1));
            Check(victor.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND place=1 AND score=1",victor.Entity.UID)==1,
                "Duel exhausted queue finalizes earned victory and returns winner");
            Check(a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
                && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND ended AND returned",victor.Entity.UID)==1
                && Scalar("SELECT mapid FROM entities WHERE uid=@uid",loser.Entity.UID)==1002,
                "Duel winner and loser restore original PK modes and save town before return acknowledgement");
            victor=ReloadBall(victor);EntityVariableTable.Load(victor);
            Check(((string)Call(service,null,"TryClaim",victor)).Contains("bound P7")
                && victor.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1,
                "Duel earned winner reconnects and claims bound P7 plus purse");
            var admissions=typeof(Entity).Assembly.GetType("Revora.Game.Features.DuelAdmissions");
            var c=ReloadBall(WelcomePlayer(town,140,2,15));var d=ReloadBall(WelcomePlayer(town,140,2,15));
            c.Entity.PKMode=Enums.PKMode.Team;d.Entity.PKMode=Enums.PKMode.Capture;
            Query("ALTER TABLE revora_duel_admissions ADD CONSTRAINT duel_admit_fault CHECK(pkmode<>3) NOT VALID");
            bool rejected=false;
            try{Call(admissions,null,"Begin","203201031115",arena.ID,new[]{c,d});}catch(Exception){rejected=true;}
            finally{Query("ALTER TABLE revora_duel_admissions DROP CONSTRAINT duel_admit_fault");}
            Check(rejected && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid",c.Entity.UID)==0,
                "Duel partial admission failure leaves no roster rows");
            Call(admissions,null,"Begin","203201031115",arena.ID,new[]{c,d});
            c.Entity.Teleport(arena.BaseID,arena.ID,50,50);d.Entity.Teleport(arena.BaseID,arena.ID,51,50);
            c.Entity.PKMode=Enums.PKMode.PK;d.Entity.PKMode=Enums.PKMode.PK;
            EntityTable.SaveEntity(c);EntityTable.SaveEntity(d);
            // Model startup abandoning unfinished admissions, then fresh character login.
            Call(admissions,null,"RecoverInterrupted");
            c=ReloadBall(c);DuelPitSystem.OnLogin(c);
            Check(!Fenced(c) && c.Entity.MapID==1002 && c.Entity.PKMode==Enums.PKMode.Team
                && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND ended AND returned",c.Entity.UID)==1,
                "Duel interrupted admission returns replacement owner and restores saved mode");
            Check(Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid",c.Entity.UID)==0,
                "Duel interrupted match does not invent a winner");
            Query("ALTER TABLE revora_duel_admissions ADD CONSTRAINT duel_return_fault CHECK(NOT returned OR pkmode<>3) NOT VALID");
            d=ReloadBall(d);
            try{DuelPitSystem.OnLogin(d);}finally{Query("ALTER TABLE revora_duel_admissions DROP CONSTRAINT duel_return_fault");}
            Check(Fenced(d) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",d.Entity.UID)==1002
                && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND ended AND NOT returned",d.Entity.UID)==1,
                "Duel failed return acknowledgement retains recovery after saving safe town");
            d=ReloadBall(d);DuelPitSystem.OnLogin(d);
            Check(!Fenced(d) && d.Entity.PKMode==Enums.PKMode.Capture
                && Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND returned",d.Entity.UID)==1,
                "Duel return acknowledgement retries safely on reconnect");
            Check(Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid AND imported",victor.Entity.UID)==1,
                "Duel startup recovery preserves already published and claimed results");
            var roster=Enumerable.Range(0,DuelPitSystem.Capacity).Select(i=>ReloadBall(WelcomePlayer(town,140,2,15))).ToArray();
            service.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2032,1,4,11,15,0));
            service.GetField("RegisterOpen",Hidden).SetValue(null,true);
            foreach(var p in roster){p.Entity.MaxHitpoints=10000;p.Entity.Hitpoints=10000;p.Entity.PKMode=Enums.PKMode.Peace;Call(service,null,"TryRegister",p);}
            Check(((string)Call(service,null,"TryRegister",c)).Contains("full"),"Duel capacity rejects a twenty-fifth registration");
            Call(service,null,"StartFight");
            for(int round=0;round<DuelPitSystem.MaxRounds;round++) {
                uint king=(uint)service.GetField("King",Hidden).GetValue(null);
                var beaten=roster.Single(p=>p.Entity.UID==king);beaten.Entity.Hitpoints=0;DuelPitSystem.OnDeath(beaten);
            }
            Check(!(bool)service.GetField("Running",Hidden).GetValue(null)
                && roster.All(p=>p.Entity.MapID==1002 && p.Entity.PKMode==Enums.PKMode.Peace),
                "Duel sixteen-round finish returns all twenty-four participants and restores their modes");
            Check(roster.Sum(p=>Scalar("SELECT count(*) FROM revora_duel_results WHERE uid=@uid",p.Entity.UID))==4,
                "Duel sixteen-round result publishes exactly four eligible placements");
            Check(roster.All(p=>Scalar("SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND ended AND returned",p.Entity.UID)==1),
                "Duel full roster has durable return acknowledgements including queued fighters");
        } finally {
            if(addDuelPath)DMaps.MapPaths.Remove(700);
            scores.Clear();eliminated.Clear();service.GetField("Running",Hidden).SetValue(null,false);
            service.GetField("PendingRank",Hidden).SetValue(null,null);
            service.GetField("CycleStart",Hidden).SetValue(null,originalCycle);service.GetField("EventMap",Hidden).SetValue(null,originalMap);
        }
    }

    static void VerifyDuelPrizes(Map town) {
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        var service=typeof(DuelPitSystem);
        foreach(string mode in new[]{"first","second","third","fourth","item-fault","external-rvp","saved-full","invalid","stale"}) {
            var p=ReloadBall(WelcomePlayer(town,140,2,15));
            EntityVariableTable.Load(p);
            uint place=mode=="second"?2u:mode=="third"?3u:mode=="fourth"?4u:mode=="invalid"?5u:1u;
            p[DuelPitSystem.ClaimKey]=place;EntityVariableTable.Save(p);EntityTable.SaveEntity(p);p=ReloadBall(p);EntityVariableTable.Load(p);
            uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            if(mode=="first") { VariableVault external;EntityVariableTable.Load(p.Entity.UID,out external);external["duel-unrelated"]=77u;EntityVariableTable.Save(p.Entity.UID,external); }
            if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT duel_soul_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
            if(mode=="external-rvp")Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="saved-full")for(int i=0;i<40;i++){var item=Pack(p,1088000,1);p.Inventory.Remove(item,Enums.ItemUse.None,true);}
            if(mode=="stale")ReloadBall(p);
            string result;
            try { result=(string)Call(service,null,"TryClaim",p); }
            finally {if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT duel_soul_fault");}
            bool success=mode=="first"||mode=="second"||mode=="third"||mode=="fourth";
            if(success) {
                uint cpPrize=place==1?Revora.Config.DuelPitFirstCp:place==2?Revora.Config.DuelPitSecondCp:place==3?Revora.Config.DuelPitThirdCp:Revora.Config.DuelPitFourthCp;
                uint rpPrize=place==1?Revora.Config.DuelPitFirstRevoraPoints:place==2?Revora.Config.DuelPitSecondRevoraPoints:place==3?Revora.Config.DuelPitThirdRevoraPoints:Revora.Config.DuelPitFourthRevoraPoints;
                ulong goldPrize=place==1?Revora.Config.DuelPitFirstGold:place==2?Revora.Config.DuelPitSecondGold:place==3?Revora.Config.DuelPitThirdGold:Revora.Config.DuelPitFourthGold;
                var soul=p.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);
                uint soulUid=soul.UID,soulType=soul.ID;
                if(mode=="first")Check((uint)p["duel-unrelated"]==77,"Duel Pit preserves unrelated external variable changes");
                Check(!Fenced(p) && result.Contains("bound P7") && soul.Bound && ConquerItemInformation.BaseInformations[soulType].PurificationLevel==7
                    && p.Entity.ConquerPoints==cp+cpPrize && p.Entity.RevoraPoint==rp+rpPrize && p.Entity.Money==gold+goldPrize,
                    "Duel Pit ordinary "+mode+" place receives configured purse and bound P7");
                Check(EntityTable.SaveEntity(p),"Duel Pit committed purse accepts full autosave: "+mode);
                p=ReloadBall(p);EntityVariableTable.Load(p);
                Check((uint)p[DuelPitSystem.ClaimKey]==0 && p.Inventory.Objects.Any(i=>i.UID==soulUid && i.ID==soulType && i.Bound)
                    && p.Entity.ConquerPoints==cp+cpPrize && p.Entity.RevoraPoint==rp+rpPrize && p.Entity.Money==gold+goldPrize,
                    "Duel Pit exact prize and consumed entitlement survive reload: "+mode);
                Parallel.For(0,4,i=>Call(service,null,"TryClaim",p));
                Check(p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000)==1 && p.Entity.ConquerPoints==cp+cpPrize,
                    "Duel Pit concurrent replay after reload grants nothing: "+mode);
            } else {
                Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0
                    && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                    && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)gold
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rp+(mode=="external-rvp"?7:0),
                    "Duel Pit failed claim has no partial payout: "+mode);
                p=ReloadBall(p);EntityVariableTable.Load(p);
                Check((uint)p[DuelPitSystem.ClaimKey]==place,"Duel Pit failed claim preserves durable entitlement: "+mode);
                if(mode=="item-fault")Check(((string)Call(service,null,"TryClaim",p)).Contains("bound P7"),"Duel Pit failed transaction can be claimed after recovery");
            }
        }
    }

    static void VerifyPurseReview(Map town){
        VerifyPowerCombat(town);
        VerifyPowerPopulation(town);
        VerifyPowerQuest(town);
        VerifyPowerRecovery(town);
        VerifyPowerTravel(town);
        VerifyPowerBookHunts(town);
        VerifyPowerRewardPacks(town);
        VerifyPowerBookStore(town);
        VerifyPowerScenes(town);
        VerifyAzureRingPack(town);
        VerifyConnectorPack(town);
        VerifySpunGoldPack(town);
        VerifyArtisanPack(town);
        VerifyFranticAppearance(town);
        VerifyFranticScenes(town);
        VerifyFranticPack(town);
        VerifySoulGeneralPack(town);
        VerifyMadManPack(town);
        VerifyGhostCityPack(town);
        VerifyTomboyPack(town);
        VerifyDesertBloodPack(town);
        VerifyHeroWingsPack(town);
        VerifyHeroWingsStore(Environment.CurrentDirectory,town);
        VerifyDesertBloodStore(Environment.CurrentDirectory,town);
        VerifyTomboyStore(Environment.CurrentDirectory,town);
        VerifyTomboyQuest(Environment.CurrentDirectory,town);
        VerifyDesertBloodQuest(Environment.CurrentDirectory,town);
        VerifyHeroWingsQuest(Environment.CurrentDirectory,town);
        VerifyGhostCityStore(Environment.CurrentDirectory,town,true);
        VerifyGhostCityQuest(Environment.CurrentDirectory,town);
        VerifyMadManStore(Environment.CurrentDirectory,town,true,true);
        VerifyMadManStore(Environment.CurrentDirectory,town,false,false,true,true);
        VerifyMadManStore(Environment.CurrentDirectory,town,false,false,false,false,true);
        VerifyMadManScenes(town);
        VerifyMadManQuest(Environment.CurrentDirectory,town);
        VerifySoulGeneralStore(Environment.CurrentDirectory,town,true);
        VerifySoulGeneralQuest(Environment.CurrentDirectory,town);
        VerifyFranticInvestigation(Environment.CurrentDirectory,town,true);
        VerifyFranticInvestigation(Environment.CurrentDirectory,town,true,true,true);
        VerifyFranticInvestigation(Environment.CurrentDirectory,town,false,false,false,true,true);
        VerifyFranticInvestigation(Environment.CurrentDirectory,town,false,false,false,true,false,true);
        VerifyMysticPack(town);
        VerifyMysticMedicine(town);
        VerifyMysticProgression(Environment.CurrentDirectory,town,true);
        VerifyMysticQuest(Environment.CurrentDirectory,town);
        VerifyFranticQuest(Environment.CurrentDirectory,town);
        VerifyArtisanQuest(Environment.CurrentDirectory,town);
        VerifySpunGoldAppearance(Environment.CurrentDirectory,town);
        VerifySpunGoldStore(Environment.CurrentDirectory,town);
        VerifySpunGoldCombat(Environment.CurrentDirectory,town);
        VerifySpunGoldQuest(Environment.CurrentDirectory,town);
        VerifyConnectorQuest(Environment.CurrentDirectory,town);
        VerifyAzureQuest(Environment.CurrentDirectory,town);
        VerifyMarketErrand(town);
        VerifyNoblePrizes(town);
        VerifyDuelPrizes(town);
        VerifyDuelResults(town);
        VerifyExtractionPurse(town);
        VerifyExtractionSoul(town);
        int failures=0;
        Action<bool,string> verify=(ok,name)=>{System.Console.WriteLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failures++;};
        VerifyRevoraNobilityRanks(verify);
        VerifyNobilityPaymentGuards(town,verify);
        VerifyAtomicNobility(town,verify);
        VerifyKingAchievement(town,verify);
        VerifyNobilityAchievement(town,verify);
        VerifyNobilityReset(town,verify);
        var cpCollision=ReloadBall(WelcomePlayer(town,1,0,15));uint cp=cpCollision.Entity.ConquerPoints;
        Query("UPDATE entities SET conquerpoints=conquerpoints+4 WHERE uid=@uid","uid",(long)cpCollision.Entity.UID);
        cpCollision.Entity.AddConquerPoints(4);
        verify(Fenced(cpCollision) && cpCollision.Entity.ConquerPoints==cp
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",cpCollision.Entity.UID)==cp+4,
            "a coincident external CP total is not proof of our reward commit");
        var rvpCollision=ReloadBall(WelcomePlayer(town,1,0,15));uint rvp=rvpCollision.Entity.RevoraPoint;
        Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)rvpCollision.Entity.UID);
        rvpCollision.Entity.AddRevoraPoints(7);
        verify(Fenced(rvpCollision) && rvpCollision.Entity.RevoraPoint==rvp
            && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",rvpCollision.Entity.UID)==rvp+7,
            "a coincident external RVP total is not proof of our reward commit");
        var explicitCp=ReloadBall(WelcomePlayer(town,1,0,15));cp=explicitCp.Entity.ConquerPoints;
        Query("UPDATE entities SET conquerpoints=conquerpoints+9 WHERE uid=@uid","uid",(long)explicitCp.Entity.UID);
        EntityTable.UpdateCps(explicitCp);
        verify(Fenced(explicitCp) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",explicitCp.Entity.UID)==cp+9,
            "an explicit CP save cannot overwrite an external durable award");
        var explicitRvp=ReloadBall(WelcomePlayer(town,1,0,15));rvp=explicitRvp.Entity.RevoraPoint;
        Query("UPDATE entities SET revorapoint=revorapoint+9 WHERE uid=@uid","uid",(long)explicitRvp.Entity.UID);
        EntityTable.UpdateRevoraPoint(explicitRvp);
        verify(Fenced(explicitRvp) && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",explicitRvp.Entity.UID)==rvp+9,
            "an explicit RVP save cannot overwrite an external durable award");
        // Production rewards call AddConquerPoints, which reads and stores inside
        // the owner lock. A raw ConquerPoints = before + n after an unlocked read
        // is still racy and is not this path.
        var compound=ReloadBall(WelcomePlayer(town,1,0,15));cp=compound.Entity.ConquerPoints;
        using(var ready=new System.Threading.Barrier(2)) {
            Parallel.For(0,2,n=>{ready.SignalAndWait();compound.Entity.AddConquerPoints(4);});
        }
        verify(!Fenced(compound) && compound.Entity.ConquerPoints==cp+8
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",compound.Entity.UID)==cp+8,
            "two locked CP rewards retain both entitlements");
        // Death processing may already own a character lock. Opposing transfers
        // must not wait on a global gate while holding either owner lock.
        var left=ReloadBall(WelcomePlayer(town,1,0,15));var right=ReloadBall(WelcomePlayer(town,1,0,15));
        left.Entity.ConquerPoints=100;right.Entity.ConquerPoints=100;
        var transfer=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurseBalance").GetMethod("TryTransferConquerPoints",Hidden);
        var both=new System.Threading.Barrier(2);int completed=0,paid=0;Exception failure=null;
        Action<GameState,GameState> run=(payer,payee)=>{
            try { lock(payer.ItemSyncRoot) {
                both.SignalAndWait();
                if(transfer.Invoke(null,new object[]{payer,payee,(uint)10}).ToString()=="Transferred")
                    System.Threading.Interlocked.Increment(ref paid);
            }} catch(Exception ex){failure=ex;} finally {System.Threading.Interlocked.Increment(ref completed);}
        };
        var a=new System.Threading.Thread(()=>run(left,right)){IsBackground=true};
        var b=new System.Threading.Thread(()=>run(right,left)){IsBackground=true};a.Start();b.Start();
        bool ended=a.Join(5000) && b.Join(5000);
        verify(ended && completed==2 && failure==null,"opposing transfers entered under owner locks finish without deadlock");
        if(ended){
            both.Dispose();
            verify(!Fenced(left) && !Fenced(right) && left.Entity.ConquerPoints+right.Entity.ConquerPoints==200
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",left.Entity.UID)+Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",right.Entity.UID)==200,
                "contended transfers preserve the combined purse without fencing");
        }
        VerifyPurseCallers(town,verify);
        Check(failures==0,"purse review failures: "+failures);
    }
    static void VerifyPurseCallers(Map town,Action<bool,string> verify){
        var paying=ReloadBall(WelcomePlayer(town,1,0,15));paying.Entity.ConquerPoints=100;
        bool paid=(bool)Call(typeof(RevoraMasterSystem),null,"Charge",paying,null,(uint)10);
        verify(paid && !Fenced(paying) && paying.Entity.ConquerPoints==90 && EntityTable.SaveEntity(paying)
            && ReloadBall(paying).Entity.ConquerPoints==90,"ordinary RevoraMaster payment remains durable through save and reload");
        var master=ReloadBall(WelcomePlayer(town,1,0,15));master.Entity.ConquerPoints=100;
        Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)master.Entity.UID);
        bool charged=(bool)Call(typeof(RevoraMasterSystem),null,"Charge",master,null,(uint)10);
        verify(!charged && Fenced(master) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",master.Entity.UID)==101,
            "RevoraMaster does not report success when its payment is rejected");

        var gender=ReloadBall(WelcomePlayer(town,1,0,15));gender.Entity.Body=1003;gender.Entity.ConquerPoints=GenderChange.Price+10;
        ushort body=gender.Entity.Body;uint genderCp=gender.Entity.ConquerPoints;
        Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)gender.Entity.UID);
        bool changed=GenderChange.Apply(gender);
        verify(!changed && Fenced(gender) && gender.Entity.Body==body
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",gender.Entity.UID)==genderCp+1,
            "gender service leaves appearance untouched after rejected payment");

        foreach(bool cp in new[]{true,false}){
            var generic=ReloadBall(WelcomePlayer(town,1,0,15));
            string column=cp?"conquerpoints":"revorapoint";bool rejected=false;
            long before=Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",generic.Entity.UID);
            Query("UPDATE entities SET "+column+"="+column+"+9 WHERE uid=@uid","uid",(long)generic.Entity.UID);
            try { EntityTable.UpdateData(generic,cp?"ConquerPoints":"RevoraPoint",(uint)(before+4)); }
            catch(InvalidOperationException){rejected=true;}
            verify(rejected && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",generic.Entity.UID)==before+9,
                "generic "+column+" writes cannot erase an outside award");

            var fault=ReloadBall(WelcomePlayer(town,1,0,15));
            if(cp)fault.Entity.ConquerPoints=123456;else fault.Entity.RevoraPoint=123456;
            Query("ALTER TABLE entities ADD CONSTRAINT purse_review_fault CHECK ("+column+" <> 123457)");
            bool escaped=false;
            try { if(cp)fault.Entity.AddConquerPoints(1);else fault.Entity.AddRevoraPoints(1); }
            catch { escaped=true; }
            finally { Query("ALTER TABLE entities DROP CONSTRAINT purse_review_fault"); }
            verify(!escaped && Fenced(fault) && (cp?fault.Entity.ConquerPoints:fault.Entity.RevoraPoint)==123456
                && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",fault.Entity.UID)==123456,
                "database failure fences "+column+" mutation without changing either balance");
        }

        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        foreach(string column in new[]{"conquerpoints","revorapoint"}){
            var rush=ReloadBall(WelcomePlayer(town,1,0,15));rush.Entity.ConquerPoints=100;EntityVariableTable.Load(rush);
            rush[BossRushSystem.ClaimKey]=2u;EntityVariableTable.Save(rush);
            Query("UPDATE entities SET "+column+"="+column+"+1 WHERE uid=@uid","uid",(long)rush.Entity.UID);
            string reply="";bool escaped=false;
            try { reply=(string)Call(typeof(BossRushSystem),null,"TryClaim",rush); }
            catch(InvalidOperationException){escaped=true;}
            VariableVault stored;EntityVariableTable.Load(rush.Entity.UID,out stored);
            verify(!escaped && Fenced(rush) && (uint)rush[BossRushSystem.ClaimKey]==2u && (uint)stored[BossRushSystem.ClaimKey]==2u
                && !reply.StartsWith("Claimed ") && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",rush.Entity.UID)==(column=="conquerpoints"?101:100),
                "Boss Rush preserves the whole unpaid claim after a "+column+" conflict");
        }
        var claimed=ReloadBall(WelcomePlayer(town,1,0,15));
        claimed.Entity.ConquerPoints=100;claimed.Entity.RevoraPoint=10;claimed.Entity.Money=1000;
        EntityVariableTable.Load(claimed);claimed[BossRushSystem.ClaimKey]=2u;EntityVariableTable.Save(claimed);
        string claimedReply=(string)Call(typeof(BossRushSystem),null,"TryClaim",claimed);
        ulong claimedGold=1000UL+Revora.Config.BossRushConsolationGold;
        verify(!Fenced(claimed) && claimedReply.StartsWith("Claimed ")
            && claimed.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp
            && claimed.Entity.RevoraPoint==10+Revora.Config.BossRushConsolationRevoraPoints
            && claimed.Entity.Money==claimedGold && (uint)claimed[BossRushSystem.ClaimKey]==0u,
            "Boss Rush consolation pays the configured purses and clears the live claim");
        VariableVault claimedStored;EntityVariableTable.Load(claimed.Entity.UID,out claimedStored);
        verify((uint)claimedStored[BossRushSystem.ClaimKey]==0u
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",claimed.Entity.UID)==100+Revora.Config.BossRushConsolationCp
            && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",claimed.Entity.UID)==10+Revora.Config.BossRushConsolationRevoraPoints
            && (ulong)Scalar("SELECT money FROM entities WHERE uid=@uid",claimed.Entity.UID)==claimedGold,
            "Boss Rush stores the cleared claim with the same purses");
        var claimedAgain=ReloadBall(claimed);EntityVariableTable.Load(claimedAgain);
        string secondClaim=(string)Call(typeof(BossRushSystem),null,"TryClaim",claimedAgain);
        verify(!Fenced(claimedAgain) && !secondClaim.StartsWith("Claimed ")
            && claimedAgain.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp
            && (uint)claimedAgain[BossRushSystem.ClaimKey]==0u,
            "reload keeps one Boss Rush consolation and a second claim does not pay");
        var raced=ReloadBall(WelcomePlayer(town,1,0,15));raced.Entity.ConquerPoints=100;
        EntityVariableTable.Load(raced);raced[BossRushSystem.ClaimKey]=2u;EntityVariableTable.Save(raced);
        int paidClaims=0;
        System.Threading.Tasks.Parallel.Invoke(
            delegate { if(((string)Call(typeof(BossRushSystem),null,"TryClaim",raced)).StartsWith("Claimed ")) System.Threading.Interlocked.Increment(ref paidClaims); },
            delegate { if(((string)Call(typeof(BossRushSystem),null,"TryClaim",raced)).StartsWith("Claimed ")) System.Threading.Interlocked.Increment(ref paidClaims); });
        verify(paidClaims==1 && !Fenced(raced) && raced.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",raced.Entity.UID)==100+Revora.Config.BossRushConsolationCp,
            "two Boss Rush claims on one saved prize pay once");
        var blocked=ReloadBall(WelcomePlayer(town,1,0,15));blocked.Entity.ConquerPoints=100;
        EntityVariableTable.Load(blocked);blocked[BossRushSystem.ClaimKey]=2u;EntityVariableTable.Save(blocked);
        uint blockedTotal=100+Revora.Config.BossRushConsolationCp;
        Query("ALTER TABLE entities ADD CONSTRAINT boss_rush_fault CHECK (conquerpoints <> "+blockedTotal+") NOT VALID");
        string blockedReply="";bool blockedEscaped=false;
        try { blockedReply=(string)Call(typeof(BossRushSystem),null,"TryClaim",blocked); }
        catch { blockedEscaped=true; }
        finally { Query("ALTER TABLE entities DROP CONSTRAINT boss_rush_fault"); }
        VariableVault blockedStored;EntityVariableTable.Load(blocked.Entity.UID,out blockedStored);
        verify(!blockedEscaped && Fenced(blocked) && (uint)blocked[BossRushSystem.ClaimKey]==2u && (uint)blockedStored[BossRushSystem.ClaimKey]==2u
            && !blockedReply.StartsWith("Claimed ") && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",blocked.Entity.UID)==100,
            "a Boss Rush statement error rolls the claim and payment back together");
        VerifyPurseVictoryAndShop(town,verify);
        VerifyInnerPowerAndIssuance(town,verify);
        VerifySilverBoothSettlement(town,verify);
        VerifyBoothContext(town,verify);
        var seller=ReloadBall(WelcomePlayer(town,1,0,15));var buyer=ReloadBall(WelcomePlayer(town,1,0,15));
        seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;
        var goods=Pack(seller,1088001,1);uint boothId=StageCpBooth(seller,buyer,goods,40);
        BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
        verify(!Fenced(seller) && !Fenced(buyer) && buyer.Entity.ConquerPoints==60 && seller.Entity.ConquerPoints==90
            && buyer.Inventory.ContainsUID(goods.UID) && !seller.Inventory.ContainsUID(goods.UID)
            && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==buyer.Entity.UID
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==60
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",seller.Entity.UID)==90,
            "a booth Conquer Point sale stores both purses and the item together");
        buyer=ReloadBall(buyer);seller=ReloadBall(seller);
        verify(buyer.Entity.ConquerPoints==60 && seller.Entity.ConquerPoints==90 && BoothAchievement(buyer)==1 && BoothAchievement(seller)==0,"reload keeps one booth sale and buyer-only achievement");
        ClearBooth(null,boothId);
        seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
        seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=5;
        goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,40);
        BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
        verify(!Fenced(seller) && !Fenced(buyer) && buyer.Entity.ConquerPoints==5 && seller.Entity.ConquerPoints==50
            && seller.Inventory.ContainsUID(goods.UID) && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==seller.Entity.UID,
            "a short booth buyer keeps the item and both purses");
        ClearBooth(seller,boothId);
        seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
        seller.Entity.ConquerPoints=uint.MaxValue;buyer.Entity.ConquerPoints=100;
        goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,1);
        BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
        verify(!Fenced(seller) && !Fenced(buyer) && buyer.Entity.ConquerPoints==100 && seller.Entity.ConquerPoints==uint.MaxValue
            && seller.Inventory.ContainsUID(goods.UID),
            "a full booth seller is not paid and keeps the item");
        ClearBooth(seller,boothId);
        foreach(bool buyerSide in new[]{true,false}){
            seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
            seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;
            goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,10);
            GameState outside=buyerSide?buyer:seller;
            Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)outside.Entity.UID);
            BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
            verify(Fenced(buyer) && Fenced(seller) && seller.Inventory.ContainsUID(goods.UID)
                && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==seller.Entity.UID
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==(buyerSide?101:100)
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",seller.Entity.UID)==(buyerSide?50:51),
                "a booth sale leaves both purses and the item unchanged after an external "+(buyerSide?"buyer":"seller")+" write");
            ClearBooth(seller,boothId);
        }
        seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
        seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;
        goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,10);
        Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)goods.UID);
        BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
        verify(Fenced(buyer) && Fenced(seller) && buyer.Entity.ConquerPoints==100
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==100
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",seller.Entity.UID)==50
            && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==1,
            "a booth sale does not pay when the item no longer belongs to the seller");
        ClearBooth(seller,boothId);
        seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
        seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;
        goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,10);
        var repeat=new ItemUsage(true){UID=goods.UID,dwParam=boothId};
        BoothItem.BuyFromBooth(repeat,buyer);BoothItem.BuyFromBooth(repeat,buyer);
        verify(!Fenced(buyer) && !Fenced(seller) && buyer.Entity.ConquerPoints==90 && seller.Entity.ConquerPoints==60
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==90,
            "a repeated booth packet pays for the item once");
        ClearBooth(seller,boothId);
        var leftSeller=ReloadBall(WelcomePlayer(town,1,0,15));var rightSeller=ReloadBall(WelcomePlayer(town,1,0,15));
        leftSeller.Entity.ConquerPoints=80;rightSeller.Entity.ConquerPoints=80;
        var leftGoods=Pack(leftSeller,1088001,1);var rightGoods=Pack(rightSeller,1088001,1);
        uint leftBooth=StageCpBooth(leftSeller,rightSeller,leftGoods,10);
        uint rightBooth=StageCpBooth(rightSeller,leftSeller,rightGoods,10);
        var oppose=new System.Threading.Barrier(2);int opposeDone=0;Exception opposeFailure=null;
        Action<GameState,GameState,uint,uint> opposeBuy=(stall,shopper,stallBooth,stallItem)=>{
            try { lock(shopper.ItemSyncRoot){ oppose.SignalAndWait();
                BoothItem.BuyFromBooth(new ItemUsage(true){UID=stallItem,dwParam=stallBooth},shopper); } }
            catch(Exception ex){ opposeFailure=ex; }
            finally { System.Threading.Interlocked.Increment(ref opposeDone); }
        };
        var leftThread=new System.Threading.Thread(()=>opposeBuy(leftSeller,rightSeller,leftBooth,leftGoods.UID)){IsBackground=true};
        var rightThread=new System.Threading.Thread(()=>opposeBuy(rightSeller,leftSeller,rightBooth,rightGoods.UID)){IsBackground=true};
        leftThread.Start();rightThread.Start();
        bool opposeEnded=leftThread.Join(5000) && rightThread.Join(5000);
        verify(opposeEnded && opposeDone==2 && opposeFailure==null,"opposing prelocked booth purchases finish without deadlock");
        if(opposeEnded) verify(leftSeller.Entity.ConquerPoints+rightSeller.Entity.ConquerPoints==160
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",leftSeller.Entity.UID)+Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",rightSeller.Entity.UID)==160,
            "opposing prelocked booth purchases preserve the combined purse");
        ClearBooth(leftSeller,leftBooth);ClearBooth(rightSeller,rightBooth);
        foreach(string mode in new[]{"bound-live","locked-live","bound-stored","stack-stored","retired-buyer","retired-seller"}){
            seller=ReloadBall(WelcomePlayer(town,1,0,15));buyer=ReloadBall(WelcomePlayer(town,1,0,15));
            seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;
            goods=Pack(seller,1088001,1);boothId=StageCpBooth(seller,buyer,goods,10);
            if(mode=="bound-live")goods.Bound=true;
            if(mode=="locked-live")goods.Lock=1;
            if(mode=="bound-stored")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)goods.UID);
            if(mode=="stack-stored")Query("UPDATE items SET stacksize=2 WHERE uid=@uid","uid",(long)goods.UID);
            GameState removed;
            if(mode=="retired-buyer")Kernel.GamePool.TryRemove(buyer.Entity.UID,out removed);
            if(mode=="retired-seller")Kernel.GamePool.TryRemove(seller.Entity.UID,out removed);
            BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=boothId},buyer);
            verify(buyer.Entity.ConquerPoints==100 && seller.Entity.ConquerPoints==50
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==100
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",seller.Entity.UID)==50
                && seller.Inventory.ContainsUID(goods.UID) && !buyer.Inventory.ContainsUID(goods.UID)
                && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==seller.Entity.UID,
                "booth purchase rechecks item and session eligibility: "+mode);
            ClearBooth(seller,boothId);
        }
    }
    static void VerifyRushFiveBossRoute(Map town,Action<bool,string> verify){
        Query("CREATE TABLE IF NOT EXISTS variablevault AS TABLE public.variablevault WITH NO DATA;CREATE TABLE IF NOT EXISTS npcs AS TABLE public.npcs WITH NO DATA;CREATE TABLE IF NOT EXISTS sobnpcs AS TABLE public.sobnpcs WITH NO DATA;CREATE TABLE IF NOT EXISTS furniture AS TABLE public.furniture WITH NO DATA;CREATE TABLE IF NOT EXISTS monsterspawns AS TABLE public.monsterspawns WITH NO DATA");
        var source=World(Environment.CurrentDirectory,700);source.Path="boss-rush-fixture";
        bool addPath=!DMaps.MapPaths.ContainsKey(700);if(addPath)DMaps.MapPaths.Add(700,source.Path);
        var p=ReloadBall(WelcomePlayer(town,140,2,15));EntityVariableTable.Load(p);
        p.Entity.X=BossRushSystem.ClaimX;p.Entity.Y=BossRushSystem.ClaimY;
        p.ActiveNpc=BossRushSystem.NpcId;
        var open=typeof(BossRushSystem).GetField("RegisterOpen",Hidden);bool before=(bool)open.GetValue(null);
        object fight=null;
        try{
            open.SetValue(null,true);
            string admission=(string)Call(typeof(BossRushSystem),null,"TryRegister",p);
            verify(admission.StartsWith("You are in."),"Boss Rush ordinary registration: "+admission);
            fight=Call(typeof(BossRushSystem),null,"FindByPlayer",p.Entity.UID);
            verify(fight!=null && p.Entity.MapID!=1002 && !p.Fake && p.Account.State==AccountTable.AccountState.Player,
                "Boss Rush ordinary NPC entry creates a private fight");
            if(fight==null)throw new Exception("Boss Rush route did not enter");
            var arena=(Map)fight.GetType().GetField("Arena").GetValue(fight);
            uint soul=(uint)Scalar("SELECT soul_type FROM revora_boss_rush_progress WHERE entityid=@uid ORDER BY run_id DESC LIMIT 1",p.Entity.UID);
            for(int wave=1;wave<=5;wave++){
                var alive=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead).ToArray();
                verify(alive.Length==1 && (int)fight.GetType().GetField("Boss").GetValue(fight)==wave,
                    "Boss Rush spawns exactly the next live boss: "+wave);
                if(alive.Length!=1)throw new Exception("Boss Rush live wave mismatch");
                var boss=alive[0];uint damage=boss.Hitpoints;
                // Controlled lethal damage traverses production attack/death handling.
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
                verify(boss.Dead,"Boss Rush production damage defeats boss: "+wave);
                if(wave==5){
                    var holdReturn=typeof(BossRushSystem).GetField("HoldReturn",Hidden);holdReturn.SetValue(null,true);
                    try{
                        Call(typeof(BossRushSystem),null,"TickInstance",fight,DateTime.Now);
                        Call(typeof(BossRushSystem),null,"DrainClosures");
                        verify(p.Entity.MapID==arena.ID && Kernel.Maps.ContainsKey(arena.ID)
                            && Scalar("SELECT returned::int FROM revora_boss_rush_progress WHERE entityid=@uid ORDER BY run_id DESC LIMIT 1",p.Entity.UID)==0,
                            "Boss Rush blocked return retains its owned arena and durable return intent");
                    }finally{holdReturn.SetValue(null,false);}
                    p=ReloadBall(p);EntityVariableTable.Load(p);
                    var visitor=ReloadBall(WelcomePlayer(town,140,2,15));visitor.Entity.MapID=arena.ID;
                    Call(typeof(BossRushSystem),null,"DrainClosures");
                    verify(p.Entity.MapID==1002 && visitor.Entity.MapID==arena.ID && Kernel.Maps.ContainsKey(arena.ID),
                        "Boss Rush returns the replacement owner but retains an arena with another occupant");
                    visitor.Entity.MapID=1002;
                    Call(typeof(BossRushSystem),null,"DrainClosures");
                    verify(!Kernel.Maps.ContainsKey(arena.ID) && !Constants.PKFreeMaps.Contains(arena.ID)
                        && Scalar("SELECT returned::int FROM revora_boss_rush_progress WHERE entityid=@uid ORDER BY run_id DESC LIMIT 1",p.Entity.UID)==1
                        && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,
                        "Boss Rush return retry saves town before acknowledging and disposing the owned arena");
                }else Call(typeof(BossRushSystem),null,"TickInstance",fight,DateTime.Now);
            }
            verify((uint)p[BossRushSystem.ClaimKey]==1 && p.Entity.MapID==1002,
                "Boss Rush five production deaths issue victory and return the player");
            p.ActiveNpc=BossRushSystem.NpcId;
            BossRushSystem.HandleNpc(p,new NpcRequest(true){NpcID=BossRushSystem.NpcId,OptionID=2});
            verify(CountId(p,soul)==1 && p.Inventory.Objects.Single(i=>i.ID==soul).Bound,
                "Boss Rush earned NPC claim grants its frozen bound P7");
            p=ReloadBall(p);EntityVariableTable.Load(p);
            verify(CountId(p,soul)==1 && (uint)p[BossRushSystem.ClaimKey]==0,
                "Boss Rush complete entry-to-prize route survives reload");
        }finally{
            open.SetValue(null,before);
            if(addPath)DMaps.MapPaths.Remove(700);
            if(fight!=null)Call(typeof(BossRushSystem),null,"RemoveMetadata",fight);
        }
    }
    static void VerifyRushSoulContract(Map town,Action<bool,string> verify){
        foreach(string mode in new[]{"victory","full-bag","item-fault","consolation"}){
            var p=ReloadBall(WelcomePlayer(town,1,0,15));EntityVariableTable.Load(p);
            ulong run=900000ul+p.Entity.UID;
            verify((int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",p,run,0,2,4,false)==1,
                "Boss Rush admission saves a new soul contract: "+mode);
            uint soul=(uint)Scalar("SELECT soul_type FROM revora_boss_rush_progress WHERE entityid=@uid",p.Entity.UID);
            verify(soul>=800000 && soul<900000 && ConquerItemInformation.BaseInformations[soul].PurificationLevel==7,
                "Boss Rush freezes a catalog P7 at admission: "+mode);
            Call(typeof(BossRushSystem),null,"SaveCheckpoint",p,run,0,2,4,false);
            verify(Scalar("SELECT soul_type FROM revora_boss_rush_progress WHERE entityid=@uid",p.Entity.UID)==soul,
                "Boss Rush duplicate admission preserves the selected soul: "+mode);
            bool win=mode!="consolation";
            verify((int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",p,run,win?5:3,1,win?3:2,false)==1,
                "Boss Rush checkpoints the test outcome: "+mode);
            verify((bool)Call(typeof(BossRushSystem),null,"TryIssue",p,win?1u:2u,run),"Boss Rush issues the test outcome: "+mode);
            var fillers=new System.Collections.Generic.List<ConquerItem>();
            if(mode=="full-bag")for(int n=0;n<38;n++)fillers.Add(Pack(p,1088001,1));
            if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT rush_soul_fault CHECK (id NOT BETWEEN 800000 AND 899999) NOT VALID");
            string reply;
            try{reply=(string)Call(typeof(BossRushSystem),null,"TryClaim",p);}
            finally{if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT rush_soul_fault");}
            if(mode=="full-bag" || mode=="item-fault"){
                verify(!reply.StartsWith("Claimed ") && Scalar("SELECT consumed::int FROM revora_boss_rush_runs WHERE entityid=@uid",p.Entity.UID)==0
                    && CountId(p,soul)==0,"Boss Rush soul failure retains the entire prize: "+mode);
                if(mode=="full-bag")foreach(var item in fillers)p.Inventory.Remove(item,Enums.ItemUse.Remove);
                p=ReloadBall(p);EntityVariableTable.Load(p);
                reply=(string)Call(typeof(BossRushSystem),null,"TryClaim",p);
            }
            verify(reply.StartsWith("Claimed ") && CountId(p,soul)==(win?1:0),"Boss Rush gives a soul only for victory: "+mode);
            p=ReloadBall(p);EntityVariableTable.Load(p);
            verify(CountId(p,soul)==(win?1:0) && p.Inventory.Objects.Where(i=>i.ID==soul).All(i=>i.Bound)
                && !((string)Call(typeof(BossRushSystem),null,"TryClaim",p)).StartsWith("Claimed "),
                "Boss Rush bound soul reload and replay: "+mode);
        }
    }
    static void VerifyPurseVictoryAndShop(Map town,Action<bool,string> verify){
        uint originalStone=Revora.Config.BossRushWinPerfectionStoneId;
        byte originalCount=Revora.Config.BossRushWinPerfectionStones;
        try {
            Revora.Config.BossRushWinPerfectionStoneId=3009002;
            Revora.Config.BossRushWinPerfectionStones=2;
            VerifyRushSoulContract(town,verify);
            VerifyRushFiveBossRoute(town,verify);
            foreach(string mode in new[]{"victory","missing-catalog","item-fault","overflow","full-bag"}){
                var p=ReloadBall(WelcomePlayer(town,1,0,15));
                p.Entity.ConquerPoints=mode=="overflow"?uint.MaxValue:100;
                p.Entity.RevoraPoint=10;p.Entity.Money=1000;
                if(!EntityTable.SaveEntity(p))throw new Exception("Boss Rush victory fixture save failed.");
                EntityVariableTable.Load(p);p[BossRushSystem.ClaimKey]=1u;EntityVariableTable.Save(p);
                if(mode=="full-bag")for(int n=0;n<39;n++)Pack(p,1088001,1);
                Revora.Config.BossRushWinPerfectionStoneId=mode=="missing-catalog"?uint.MaxValue:3009002;
                if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT rush_item_fault CHECK (id <> 3009002) NOT VALID");
                string reply;
                try { reply=(string)Call(typeof(BossRushSystem),null,"TryClaim",p); }
                finally { if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT rush_item_fault"); }
                VariableVault vault;EntityVariableTable.Load(p.Entity.UID,out vault);
                if(mode=="victory"){
                    verify(reply.StartsWith("Claimed ") && !Fenced(p) && CountId(p,3009002)==2
                        && p.Inventory.Objects.Where(i=>i.ID==3009002).All(i=>!i.Bound)
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3009002 AND bound=0",p.Entity.UID)==2
                        && p.Entity.ConquerPoints==100+Revora.Config.BossRushWinCp
                        && p.Entity.RevoraPoint==10+Revora.Config.BossRushWinRevoraPoints
                        && p.Entity.Money==1000+Revora.Config.BossRushWinGold && (uint)vault[BossRushSystem.ClaimKey]==0,
                        "Boss Rush victory preserves every configured purse and two unbound stones");
                    verify(EntityTable.SaveEntity(p),"Boss Rush victory full save succeeds");
                    p=ReloadBall(p);EntityVariableTable.Load(p);
                    verify(CountId(p,3009002)==2 && p.Inventory.Objects.Where(i=>i.ID==3009002).All(i=>!i.Bound)
                        && !((string)Call(typeof(BossRushSystem),null,"TryClaim",p)).StartsWith("Claimed "),
                        "Boss Rush victory stones survive reload without a second payment");
                } else {
                    verify(!reply.StartsWith("Claimed ") && (uint)vault[BossRushSystem.ClaimKey]==1u
                        && (uint)p[BossRushSystem.ClaimKey]==1u && CountId(p,3009002)==0
                        && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==(mode=="overflow"?uint.MaxValue:100)
                        && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==10
                        && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==1000,
                        "Boss Rush keeps the entire victory entitlement on "+mode);
                }
            }
        } finally {Revora.Config.BossRushWinPerfectionStoneId=originalStone;Revora.Config.BossRushWinPerfectionStones=originalCount;}
        foreach(string mode in new[]{"purchase","external","item-fault","soul","armor-soul","remote","fake","trade"}){
            var p=ReloadBall(WelcomePlayer(town,1,0,15));p.Entity.RevoraPoint=1000;
            p.ActiveNpc=RevoraPointShopSystem.NpcId;
            p.Entity.X=396;p.Entity.Y=340;
            town.Npcs[p.ActiveNpc]=new NpcSpawn{UID=p.ActiveNpc,MapID=1002,X=396,Y=340,Mesh=50110,Type=Enums.NpcType.Talker};
            bool soul=mode=="soul" || mode=="armor-soul";
            if(soul)p.Entity.RevoraPoint=15000;
            if(mode=="remote")p.Entity.X=420;
            if(mode=="fake")p.Fake=true;
            if(mode=="trade")p.Trade.InTrade=true;
            if(mode=="external")Query("UPDATE entities SET revorapoint=1001 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT vesper_item_fault CHECK (id <> 3009001) NOT VALID");
            try { RevoraPointShopSystem.HandleNpc(p,new NpcRequest(true){OptionID=(byte)(soul?(mode=="soul"?30:50):11)}); }
            finally {if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT vesper_item_fault");}
            if(soul){
                uint id=mode=="soul"?800020u:820073u,remaining=mode=="soul"?3000u:5000u;
                verify(!Fenced(p) && p.Entity.RevoraPoint==remaining && p.Inventory.Objects.Single(i=>i.ID==id).Bound,
                    "Vesper sells a bound P7 at its unchanged price: "+mode);
                verify(EntityTable.SaveEntity(p),"bound Vesper P7 full save succeeds: "+mode);p=ReloadBall(p);
                verify(p.Entity.RevoraPoint==remaining && p.Inventory.Objects.Single(i=>i.ID==id).Bound,
                    "Vesper P7 binding and exact debit survive production reload: "+mode);
            } else if(mode=="remote" || mode=="fake" || mode=="trade"){
                verify(!Fenced(p) && p.Entity.RevoraPoint==1000 && CountId(p,3009001)==0
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==1000,
                    "Vesper rejects an ineligible buyer without charging: "+mode);
                p.Fake=false;p.Trade.InTrade=false;
            } else if(mode=="purchase"){
                verify(!Fenced(p) && p.Entity.RevoraPoint==600 && CountId(p,3009001)==1
                    && p.Inventory.Objects.Single(i=>i.ID==3009001).Bound==false && EntityTable.SaveEntity(p),
                    "ordinary Vesper purchase commits its exact price and unbound stone");
                p=ReloadBall(p);
                verify(p.Entity.RevoraPoint==600 && CountId(p,3009001)==1,"Vesper item and price survive full save and reload");
            } else verify(Fenced(p) && CountId(p,3009001)==0
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==(mode=="external"?1001:1000)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3009001",p.Entity.UID)==0,
                "Vesper rolls back payment and item on "+mode);
        }
    }
    static long InnerField(InnerPower inner,string name){
        return (long)typeof(InnerPower).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(inner);
    }
    static void BuyInner(GameState p,byte option){
        p.ActiveNpc=900019;p.Entity.MapID=1002;p.Entity.FullyLoaded=true;p.Action=2;Kernel.GamePool[p.Entity.UID]=p;
        Npcs.GetDialog(new NpcRequest(true){NpcID=900019,OptionID=option},p);
    }
    static void SetRushRun(object inst,ulong id){
        inst.GetType().GetField("RunId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).SetValue(inst,id);
    }
    static void TrackRush(object inst){
        object live=typeof(BossRushSystem).GetField("Live",Hidden).GetValue(null);
        live.GetType().GetMethod("Add").Invoke(live,new object[]{inst});
    }
    static void UntrackRush(object inst){
        object live=typeof(BossRushSystem).GetField("Live",Hidden).GetValue(null);
        live.GetType().GetMethod("Remove").Invoke(live,new object[]{inst});
    }
    static object RushInstance(GameState p,int boss,DateTime until){
        var type=typeof(BossRushSystem).GetNestedType("Instance",System.Reflection.BindingFlags.NonPublic);
        object inst=Activator.CreateInstance(type,true);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public;
        type.GetField("Player",flags).SetValue(inst,p);
        type.GetField("Boss",flags).SetValue(inst,boss);
        type.GetField("FightUntil",flags).SetValue(inst,until);
        type.GetField("LivesLeft",flags).SetValue(inst,2);
        return inst;
    }
    static void VerifyInnerPowerAndIssuance(Map town,Action<bool,string> verify){
        string path=Path.Combine(Environment.CurrentDirectory,"database","InnerPower.txt");
        string backup=path+".purse-backup";
        bool backed=File.Exists(path);
        if(backed)File.Copy(path,backup,true);
        var shop=typeof(Entity).Assembly.GetType("Revora.Game.Features.InnerPowerPurchases");
        var packs=typeof(Entity).Assembly.GetType("Revora.Game.Features.PotencyPacks");
        try {
            Call(shop,null,"Initialize");
            var small=WelcomePlayer(town,1,0,15);small.Entity.RevoraPoint=500;
            BuyInner(small,1);
            verify(!Fenced(small) && small.Entity.RevoraPoint==0 && small.InnerPower.Potency==2000
                && InnerField(small.InnerPower,"AppliedNpcPoints")==2000 && InnerField(small.InnerPower,"AppliedPackPoints")==0
                && Scalar("SELECT points FROM revora_inner_purchases WHERE entityid=@uid",small.Entity.UID)==2000
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",small.Entity.UID)==0,
                "ordinary Inner Power purchase stores 500 Revora Points for 2000 potency");
            BuyInner(small,1);
            verify(!Fenced(small) && small.InnerPower.Potency==2000 && small.Entity.RevoraPoint==0,
                "a second Inner Power purchase without points does not grant potency again");
            var large=WelcomePlayer(town,1,0,15);large.Entity.RevoraPoint=4000;
            BuyInner(large,2);
            verify(!Fenced(large) && large.Entity.RevoraPoint==0 && large.InnerPower.Potency==16000
                && Scalar("SELECT points FROM revora_inner_purchases WHERE entityid=@uid",large.Entity.UID)==16000,
                "the larger Inner Power purchase stores 4000 Revora Points for 16000 potency");
            var shortBuyer=WelcomePlayer(town,1,0,15);shortBuyer.Entity.RevoraPoint=100;
            BuyInner(shortBuyer,1);
            verify(!Fenced(shortBuyer) && shortBuyer.InnerPower.Potency==0 && shortBuyer.Entity.RevoraPoint==100,
                "a short Inner Power buyer keeps both balances");
            var full=WelcomePlayer(town,1,0,15);full.Entity.RevoraPoint=500;full.InnerPower.Potency=int.MaxValue;
            BuyInner(full,1);
            verify(!Fenced(full) && full.InnerPower.Potency==int.MaxValue && full.Entity.RevoraPoint==500
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",full.Entity.UID)==500,
                "Inner Power overflow leaves the Revora Point balance untouched");
            var paired=WelcomePlayer(town,1,0,15);paired.Entity.RevoraPoint=500;paired.InnerPower.Potency=400;
            typeof(InnerPower).GetField("AppliedPackPoints",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(paired.InnerPower,400L);
            Query("INSERT INTO revora_potency_awards(entityid,points) VALUES(@uid,400)","uid",(long)paired.Entity.UID);
            BuyInner(paired,1);
            verify(!Fenced(paired) && paired.InnerPower.Potency==2400 && InnerField(paired.InnerPower,"AppliedPackPoints")==400
                && InnerField(paired.InnerPower,"AppliedNpcPoints")==2000
                && Scalar("SELECT points FROM revora_potency_awards WHERE entityid=@uid",paired.Entity.UID)==400
                && Scalar("SELECT points FROM revora_inner_purchases WHERE entityid=@uid",paired.Entity.UID)==2000,
                "an Inner Power purchase does not consume or replay a pack award");
            var recovered=new InnerPower("Recovered",paired.Entity.UID);
            Call(packs,null,"RecoverPlayer",recovered);Call(shop,null,"RecoverPlayer",recovered);
            Call(packs,null,"RecoverPlayer",recovered);Call(shop,null,"RecoverPlayer",recovered);
            verify(recovered.Potency==2400 && InnerField(recovered,"AppliedPackPoints")==400 && InnerField(recovered,"AppliedNpcPoints")==2000,
                "pack and Inner Power ledgers project exactly once on a cold load");
            var blocked=WelcomePlayer(town,1,0,15);blocked.Entity.RevoraPoint=500;
            Query("ALTER TABLE revora_inner_purchases ADD CONSTRAINT inner_purchase_fault CHECK (points <> 2000) NOT VALID");
            try { BuyInner(blocked,1); }
            finally { Query("ALTER TABLE revora_inner_purchases DROP CONSTRAINT inner_purchase_fault"); }
            verify(Fenced(blocked) && blocked.InnerPower.Potency==0
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",blocked.Entity.UID)==500,
                "an Inner Power statement error rolls the charge and potency back together");
            var projected=WelcomePlayer(town,1,0,15);projected.Entity.RevoraPoint=500;
            shop.GetField("AfterCommit",Hidden).SetValue(null,(Action<GameState>)delegate { throw new InvalidOperationException("projection"); });
            try { BuyInner(projected,1); }
            finally { shop.GetField("AfterCommit",Hidden).SetValue(null,null); }
            var projectedRecovered=new InnerPower("Projected",projected.Entity.UID);
            Call(shop,null,"RecoverPlayer",projectedRecovered);Call(shop,null,"RecoverPlayer",projectedRecovered);
            verify(Fenced(projected) && projected.InnerPower.Potency==0
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",projected.Entity.UID)==0
                && Scalar("SELECT points FROM revora_inner_purchases WHERE entityid=@uid",projected.Entity.UID)==2000
                && projectedRecovered.Potency==2000,
                "an Inner Power projection failure keeps one durable grant for the next load");
            var raced=WelcomePlayer(town,1,0,15);raced.Entity.RevoraPoint=1000;
            System.Threading.Tasks.Parallel.Invoke(delegate { BuyInner(raced,1); }, delegate { BuyInner(raced,1); });
            verify(!Fenced(raced) && raced.InnerPower.Potency==4000 && raced.Entity.RevoraPoint==0
                && Scalar("SELECT points FROM revora_inner_purchases WHERE entityid=@uid",raced.Entity.UID)==4000,
                "two Inner Power purchases retain both 2000 potency grants");

            var saving=WelcomePlayer(town,1,0,15);saving.Entity.RevoraPoint=500;
            object saveGate=typeof(InnerPowerTable).GetField("SaveGate",Hidden).GetValue(null);
            object innerGate=typeof(InnerPower).GetField("SyncRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(saving.InnerPower);
            var committed=new System.Threading.ManualResetEventSlim(false);
            Exception saveError=null;
            var saveThread=new System.Threading.Thread(delegate(){try{BuyInner(saving,1);}catch(Exception ex){saveError=ex;}});
            saveThread.IsBackground=true;
            bool innerAvailable=false;
            shop.GetField("AfterCommit",Hidden).SetValue(null,(Action<GameState>)delegate { committed.Set(); });
            try {
                lock(saveGate){
                    saveThread.Start();
                    if(committed.Wait(10000)){
                        innerAvailable=System.Threading.Monitor.TryEnter(innerGate,2000);
                        if(innerAvailable)System.Threading.Monitor.Exit(innerGate);
                    }
                }
                verify(saveThread.Join(10000) && saveError==null && innerAvailable && !Fenced(saving),
                    "Inner Power releases the player potency lock before waiting for the global file save");
            } finally {shop.GetField("AfterCommit",Hidden).SetValue(null,null);}

            var external=WelcomePlayer(town,1,0,15);external.Entity.RevoraPoint=500;
            Query("UPDATE entities SET revorapoint=501 WHERE uid=@uid","uid",(long)external.Entity.UID);
            BuyInner(external,1);
            verify(Fenced(external) && external.InnerPower.Potency==0
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",external.Entity.UID)==501
                && Scalar("SELECT count(*) FROM revora_inner_purchases WHERE entityid=@uid",external.Entity.UID)==0,
                "Inner Power external purse conflict leaves both payment and ledger untouched");

            InnerPowerTable.Save();
            var savedInners=InnerPower.InnerPowerPolle.ToArray();
            try {
                InnerPower.InnerPowerPolle.Clear();
                InnerPowerTable.Load();
                var disk=InnerPower.InnerPowerPolle[paired.Entity.UID];
                Call(packs,null,"RecoverPlayer",disk);Call(shop,null,"RecoverPlayer",disk);
                Call(packs,null,"RecoverPlayer",disk);Call(shop,null,"RecoverPlayer",disk);
                verify(disk.Potency==2400 && InnerField(disk,"AppliedPackPoints")==400 && InnerField(disk,"AppliedNpcPoints")==2000,
                    "actual InnerPower file save and cold loader retain both independent checkpoints without replay");
            } finally {
                InnerPower.InnerPowerPolle.Clear();
                foreach(var entry in savedInners)InnerPower.InnerPowerPolle[entry.Key]=entry.Value;
            }

            var memoryOnly=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(memoryOnly);
            memoryOnly[BossRushSystem.ClaimKey]=1u;
            object memoryWin=RushInstance(memoryOnly,5,DateTime.Now.AddMinutes(5));
            Call(typeof(BossRushSystem),null,"TickInstance",memoryWin,DateTime.Now);
            VariableVault memoryDisk;EntityVariableTable.Load(memoryOnly.Entity.UID,out memoryDisk);
            verify((uint)memoryDisk[BossRushSystem.ClaimKey]==1u,
                "a live-only Boss Rush claim is saved before the victory becomes terminal");

            var unsaved=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(unsaved);
            unsaved[BossRushSystem.ClaimKey]=1u;
            object unsavedWin=RushInstance(unsaved,5,DateTime.Now.AddMinutes(5));
            Call(typeof(BossRushSystem),null,"TickInstance",unsavedWin,DateTime.Now);
            VariableVault unsavedDisk;EntityVariableTable.Load(unsaved.Entity.UID,out unsavedDisk);
            verify((uint)unsavedDisk[BossRushSystem.ClaimKey]==1u,
                "REVIEW-246: live-only claim cannot terminate a victory without durable issuance");

            var staleVault=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(staleVault);
            staleVault["review-unrelated-progress"]=10u;EntityVariableTable.Save(staleVault);
            VariableVault newer;EntityVariableTable.Load(staleVault.Entity.UID,out newer);
            newer["review-unrelated-progress"]=20u;EntityVariableTable.Save(staleVault.Entity.UID,newer);
            object staleWin=RushInstance(staleVault,5,DateTime.Now.AddMinutes(5));
            Call(typeof(BossRushSystem),null,"TickInstance",staleWin,DateTime.Now);
            VariableVault latest;EntityVariableTable.Load(staleVault.Entity.UID,out latest);
            verify((uint)latest["review-unrelated-progress"]==20u,
                "REVIEW-246: Boss Rush issuance preserves newer unrelated stored progress");

            var pendingVault=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(pendingVault);
            pendingVault[BossRushSystem.ClaimKey]=1u;pendingVault["review-unrelated-progress"]=10u;EntityVariableTable.Save(pendingVault);
            pendingVault[BossRushSystem.ClaimKey]=0u;pendingVault["review-unrelated-progress"]=11u;
            object pendingWin=RushInstance(pendingVault,5,DateTime.Now.AddMinutes(5));
            VariableVault pendingSame=pendingVault.Variables;
            object pendingRoot=pendingVault.Variables.GetType().GetField("SyncRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pendingVault.Variables);
            Call(typeof(BossRushSystem),null,"TickInstance",pendingWin,DateTime.Now);
            verify(object.ReferenceEquals(pendingSame,pendingVault.Variables)
                && object.ReferenceEquals(pendingRoot,pendingVault.Variables.GetType().GetField("SyncRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pendingVault.Variables))
                && (uint)pendingVault["review-unrelated-progress"]==11u
                && (uint)pendingVault[BossRushSystem.ClaimKey]==1u,
                "REVIEW-246: saved-claim adoption preserves unrelated pending live progress");

            var winner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(winner);
            object victory=RushInstance(winner,5,DateTime.Now.AddMinutes(5));
            Call(typeof(BossRushSystem),null,"TickInstance",victory,DateTime.Now);
            VariableVault victoryStored;EntityVariableTable.Load(winner.Entity.UID,out victoryStored);
            verify(!Fenced(winner) && (uint)winner[BossRushSystem.ClaimKey]==1u && (uint)victoryStored[BossRushSystem.ClaimKey]==1u,
                "a Boss Rush win stores the victory claim before the instance ends");
            Call(typeof(BossRushSystem),null,"TickInstance",victory,DateTime.Now);
            verify((uint)winner[BossRushSystem.ClaimKey]==1u,"a second Boss Rush tick does not issue another victory");
            var finishing=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(finishing);
            object finishWin=RushInstance(finishing,5,DateTime.Now.AddMinutes(5));
            object rushGate=typeof(BossRushSystem).GetField("Gate",Hidden).GetValue(null);
            Exception finishError=null;
            var finishThread=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"Win",finishWin);}catch(Exception ex){finishError=ex;}});
            finishThread.IsBackground=true;
            bool finishOwnerAvailable=false;
            lock(rushGate){
                finishThread.Start();
                bool issued=System.Threading.SpinWait.SpinUntil(delegate{return (uint)finishing[BossRushSystem.ClaimKey]==1u;},10000);
                if(issued){
                    finishOwnerAvailable=System.Threading.Monitor.TryEnter(finishing.ItemSyncRoot,2000);
                    if(finishOwnerAvailable)System.Threading.Monitor.Exit(finishing.ItemSyncRoot);
                }
            }
            verify(finishThread.Join(10000) && finishError==null && finishOwnerAvailable,
                "Boss Rush releases its owner lock before waiting for registry removal");
            var fallen=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(fallen);
            object consolation=RushInstance(fallen,3,DateTime.Now.AddMinutes(-1));
            Call(typeof(BossRushSystem),null,"TickInstance",consolation,DateTime.Now);
            VariableVault consolationStored;EntityVariableTable.Load(fallen.Entity.UID,out consolationStored);
            verify(!Fenced(fallen) && (uint)fallen[BossRushSystem.ClaimKey]==2u && (uint)consolationStored[BossRushSystem.ClaimKey]==2u,
                "a boss-3 failure stores the consolation claim before the instance ends");
            var early=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(early);
            object earlyFail=RushInstance(early,1,DateTime.Now.AddMinutes(-1));
            Call(typeof(BossRushSystem),null,"TickInstance",earlyFail,DateTime.Now);
            verify((uint)early[BossRushSystem.ClaimKey]==0u,"an early Boss Rush failure stores no claim");
            var delayed=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(delayed);
            object delayedWin=RushInstance(delayed,5,DateTime.Now.AddMinutes(5));
            Query("ALTER TABLE variablevault RENAME TO variablevault_purse_away");
            try { Call(typeof(BossRushSystem),null,"TickInstance",delayedWin,DateTime.Now); }
            finally { Query("ALTER TABLE variablevault_purse_away RENAME TO variablevault"); }
            verify((uint)delayed[BossRushSystem.ClaimKey]==0u,"a failed Boss Rush claim write does not finish the instance");
            Call(typeof(BossRushSystem),null,"TickInstance",delayedWin,DateTime.Now.AddMinutes(10));
            VariableVault delayedStored;EntityVariableTable.Load(delayed.Entity.UID,out delayedStored);
            verify((uint)delayed[BossRushSystem.ClaimKey]==1u && (uint)delayedStored[BossRushSystem.ClaimKey]==1u,
                "the next Boss Rush tick preserves the failed-write victory even after the deadline");
            var delayedLoss=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(delayedLoss);
            object loss=RushInstance(delayedLoss,5,DateTime.Now.AddMinutes(5));
            Query("ALTER TABLE variablevault RENAME TO variablevault_purse_away");
            try { Call(typeof(BossRushSystem),null,"Fail",loss,"You left the field."); }
            finally { Query("ALTER TABLE variablevault_purse_away RENAME TO variablevault"); }
            Call(typeof(BossRushSystem),null,"TickInstance",loss,DateTime.Now);
            VariableVault lossStored;EntityVariableTable.Load(delayedLoss.Entity.UID,out lossStored);
            verify((uint)lossStored[BossRushSystem.ClaimKey]==2u && (uint)delayedLoss[BossRushSystem.ClaimKey]==2u,
                "a failed consolation write cannot turn into victory when the next tick sees no monsters");
            var uncertain=WelcomePlayer(town,1,0,15);uncertain.Entity.ConquerPoints=100;uncertain.Entity.RevoraPoint=10;uncertain.Entity.Money=1000;
            EntityVariableTable.Load(uncertain);uncertain[BossRushSystem.ClaimKey]=2u;EntityVariableTable.Save(uncertain);
            typeof(BossRushSystem).GetField("AfterClaimCommit",Hidden).SetValue(null,(Action<GameState>)delegate { throw new InvalidOperationException("projection"); });
            string uncertainReply="";
            try { uncertainReply=(string)Call(typeof(BossRushSystem),null,"TryClaim",uncertain); }
            finally { typeof(BossRushSystem).GetField("AfterClaimCommit",Hidden).SetValue(null,null); }
            var reloaded=ReloadBall(uncertain);EntityVariableTable.Load(reloaded);
            verify(Fenced(uncertain) && !uncertainReply.StartsWith("Claimed ")
                && reloaded.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp
                && reloaded.Entity.RevoraPoint==10+Revora.Config.BossRushConsolationRevoraPoints
                && reloaded.Entity.Money==1000+Revora.Config.BossRushConsolationGold
                && (uint)reloaded[BossRushSystem.ClaimKey]==0u
                && !((string)Call(typeof(BossRushSystem),null,"TryClaim",reloaded)).StartsWith("Claimed "),
                "a Boss Rush projection failure reloads the committed consolation exactly once");

            var quest=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(quest);
            quest["review-local-quest"]=1u;EntityVariableTable.Save(quest);
            VariableVault outside;EntityVariableTable.Load(quest.Entity.UID,out outside);
            outside["review-external-quest"]=7u;EntityVariableTable.Save(quest.Entity.UID,outside);
            quest["review-local-quest"]=2u;EntityVariableTable.Save(quest);
            VariableVault questDisk;EntityVariableTable.Load(quest.Entity.UID,out questDisk);
            verify((uint)questDisk["review-local-quest"]==2u && (uint)questDisk["review-external-quest"]==7u,
                "a normal variable save keeps an external key and this session's later edit");
            VariableVault rival;EntityVariableTable.Load(quest.Entity.UID,out rival);
            rival["review-local-quest"]=9u;EntityVariableTable.Save(quest.Entity.UID,rival);
            quest["review-local-quest"]=3u;
            bool conflicted=false;
            try { EntityVariableTable.Save(quest); }
            catch (InvalidOperationException) { conflicted=true; }
            VariableVault conflictDisk;EntityVariableTable.Load(quest.Entity.UID,out conflictDisk);
            verify(conflicted && (uint)conflictDisk["review-local-quest"]==9u && (uint)quest["review-local-quest"]==3u
                && (uint)conflictDisk["review-external-quest"]==7u,
                "a normal variable save keeps both edits when the same key changed in memory and storage");

            var twice=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(twice);
            twice["review-unrelated-progress"]=4u;EntityVariableTable.Save(twice);
            object firstRun=RushInstance(twice,3,DateTime.Now.AddMinutes(-1));
            SetRushRun(firstRun,11);
            Call(typeof(BossRushSystem),null,"TickInstance",firstRun,DateTime.Now);
            twice["review-unrelated-progress"]=5u;
            object secondRun=RushInstance(twice,4,DateTime.Now.AddMinutes(-1));
            SetRushRun(secondRun,22);
            Call(typeof(BossRushSystem),null,"TickInstance",secondRun,DateTime.Now);
            VariableVault parked;EntityVariableTable.Load(twice.Entity.UID,out parked);
            verify((uint)twice["review-unrelated-progress"]==5u && (uint)parked["review-unrelated-progress"]==5u
                && (uint)parked[BossRushSystem.ClaimKey]==2u && (ulong)parked[BossRushSystem.ClaimRunKey]==11ul
                && (uint)parked[BossRushSystem.PendingKindKey]==2u && (ulong)parked[BossRushSystem.PendingRunKey]==22ul,
                "a second Boss Rush outcome stays parked behind the outstanding prize");
            var revived=ReloadBall(twice);EntityVariableTable.Load(revived);
            BossRushSystem.RecoverPending(revived);
            verify((uint)revived[BossRushSystem.ClaimKey]==2u && (ulong)revived[BossRushSystem.ClaimRunKey]==11ul,
                "restart leaves the outstanding Boss Rush prize in front of the parked outcome");
            revived.Entity.ConquerPoints=100;revived.Entity.RevoraPoint=10;revived.Entity.Money=1000;
            string firstPrize=(string)Call(typeof(BossRushSystem),null,"TryClaim",revived);
            verify(firstPrize.StartsWith("Claimed ") && revived.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp
                && (uint)revived[BossRushSystem.ClaimKey]==2u && (ulong)revived[BossRushSystem.ClaimRunKey]==22ul
                && (uint)revived[BossRushSystem.PendingKindKey]==0u && (uint)revived["review-unrelated-progress"]==5u,
                "claiming the first Boss Rush prize promotes the parked outcome once");
            string secondPrize=(string)Call(typeof(BossRushSystem),null,"TryClaim",revived);
            string thirdPrize=(string)Call(typeof(BossRushSystem),null,"TryClaim",revived);
            verify(secondPrize.StartsWith("Claimed ") && !thirdPrize.StartsWith("Claimed ")
                && revived.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp*2
                && (uint)revived[BossRushSystem.ClaimKey]==0u,
                "the parked Boss Rush outcome pays once after the earlier prize");

            var orphan=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(orphan);
            orphan[BossRushSystem.ClaimKey]=0u;orphan[BossRushSystem.PendingKindKey]=2u;orphan[BossRushSystem.PendingRunKey]=77ul;
            EntityVariableTable.Save(orphan);
            var woken=ReloadBall(orphan);EntityVariableTable.Load(woken);
            BossRushSystem.RecoverPending(woken);BossRushSystem.RecoverPending(woken);
            VariableVault wokenDisk;EntityVariableTable.Load(woken.Entity.UID,out wokenDisk);
            verify((uint)woken[BossRushSystem.ClaimKey]==2u && (ulong)woken[BossRushSystem.ClaimRunKey]==77ul
                && (uint)wokenDisk[BossRushSystem.ClaimKey]==2u && (uint)wokenDisk[BossRushSystem.PendingKindKey]==0u,
                "a parked Boss Rush outcome with no active prize is promoted exactly once on reload");

            var identity=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(identity);
            identity.Entity.ConquerPoints=100;identity.Entity.RevoraPoint=10;identity.Entity.Money=1000;
            identity[BossRushSystem.ClaimKey]=2u;identity[BossRushSystem.ClaimRunKey]=101ul;
            EntityVariableTable.Save(identity);
            VariableVault replaced;EntityVariableTable.Load(identity.Entity.UID,out replaced);
            replaced[BossRushSystem.ClaimRunKey]=202ul;EntityVariableTable.Save(identity.Entity.UID,replaced);
            string wrongRun=(string)Call(typeof(BossRushSystem),null,"TryClaim",identity);
            VariableVault identityDisk;EntityVariableTable.Load(identity.Entity.UID,out identityDisk);
            verify(!wrongRun.StartsWith("Claimed ") && Fenced(identity)
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",identity.Entity.UID)==100
                && (uint)identityDisk[BossRushSystem.ClaimKey]==2u
                && (ulong)identityDisk[BossRushSystem.ClaimRunKey]==202ul,
                "REVIEW-250: a stale session cannot consume a different run's same-kind prize");

            var conflictRun=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(conflictRun);
            conflictRun[BossRushSystem.ClaimKey]=2u;conflictRun[BossRushSystem.ClaimRunKey]=301ul;
            conflictRun[BossRushSystem.PendingKindKey]=2u;conflictRun[BossRushSystem.PendingRunKey]=302ul;
            EntityVariableTable.Save(conflictRun);
            bool accepted=(bool)Call(typeof(BossRushSystem),null,"TryIssue",conflictRun,1u,302ul);
            VariableVault conflictRunDisk;EntityVariableTable.Load(conflictRun.Entity.UID,out conflictRunDisk);
            verify(!accepted && (uint)conflictRunDisk[BossRushSystem.PendingKindKey]==2u
                && (ulong)conflictRunDisk[BossRushSystem.PendingRunKey]==302ul,
                "REVIEW-250: a conflicting pending outcome cannot acknowledge victory issuance");
            conflictRun[BossRushSystem.ClaimKey]=0u;conflictRun[BossRushSystem.ClaimRunKey]=0ul;
            EntityVariableTable.Save(conflictRun);
            accepted=(bool)Call(typeof(BossRushSystem),null,"TryIssue",conflictRun,1u,302ul);
            EntityVariableTable.Load(conflictRun.Entity.UID,out conflictRunDisk);
            verify(!accepted && (uint)conflictRunDisk[BossRushSystem.PendingKindKey]==2u
                && (uint)conflictRunDisk[BossRushSystem.ClaimKey]==0u,
                "REVIEW-250: a conflicting orphan outcome cannot be overwritten by a retry");

            var mergedClaim=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(mergedClaim);
            mergedClaim.Entity.ConquerPoints=100;mergedClaim.Entity.RevoraPoint=10;mergedClaim.Entity.Money=1000;
            mergedClaim[BossRushSystem.ClaimKey]=2u;mergedClaim[BossRushSystem.ClaimRunKey]=401ul;
            mergedClaim["review-local-progress"]=10u;mergedClaim["review-stored-progress"]=10u;
            EntityVariableTable.Save(mergedClaim);
            VariableVault storedClaim;EntityVariableTable.Load(mergedClaim.Entity.UID,out storedClaim);
            storedClaim["review-stored-progress"]=20u;EntityVariableTable.Save(mergedClaim.Entity.UID,storedClaim);
            mergedClaim["review-local-progress"]=11u;
            VariableVault claimObject=mergedClaim.Variables;
            string mergedPrize=(string)Call(typeof(BossRushSystem),null,"TryClaim",mergedClaim);
            VariableVault mergedDisk;EntityVariableTable.Load(mergedClaim.Entity.UID,out mergedDisk);
            verify(mergedPrize.StartsWith("Claimed ") && object.ReferenceEquals(claimObject,mergedClaim.Variables)
                && (uint)mergedDisk["review-local-progress"]==11u && (uint)mergedDisk["review-stored-progress"]==20u
                && (uint)mergedClaim["review-local-progress"]==11u && (uint)mergedClaim["review-stored-progress"]==20u
                && (uint)mergedDisk[BossRushSystem.ClaimKey]==0u,
                "REVIEW-250: claim consumption preserves unrelated live and stored progress in the same vault");

            var uncertainIssue=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(uncertainIssue);
            object uncertainRun=RushInstance(uncertainIssue,3,DateTime.Now.AddMinutes(-1));SetRushRun(uncertainRun,501ul);
            typeof(BossRushSystem).GetField("AfterIssueCommit",Hidden).SetValue(null,(Action<GameState>)delegate {throw new IOException("review issue projection");});
            try { Call(typeof(BossRushSystem),null,"TickInstance",uncertainRun,DateTime.Now); }
            finally { typeof(BossRushSystem).GetField("AfterIssueCommit",Hidden).SetValue(null,null); }
            VariableVault issuedDisk;EntityVariableTable.Load(uncertainIssue.Entity.UID,out issuedDisk);
            bool awaitingRetry=!(bool)uncertainRun.GetType().GetField("Failed").GetValue(uncertainRun);
            Call(typeof(BossRushSystem),null,"TickInstance",uncertainRun,DateTime.Now);
            verify(awaitingRetry && (uint)issuedDisk[BossRushSystem.ClaimKey]==2u
                && (ulong)issuedDisk[BossRushSystem.ClaimRunKey]==501ul
                && (uint)uncertainIssue[BossRushSystem.ClaimKey]==2u
                && (bool)uncertainRun.GetType().GetField("Failed").GetValue(uncertainRun),
                "REVIEW-250: issue projection failure retries the committed run before marking it terminal");

            var replayOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(replayOwner);
            replayOwner.Entity.ConquerPoints=100;replayOwner.Entity.RevoraPoint=10;replayOwner.Entity.Money=1000;
            object replayRun=RushInstance(replayOwner,3,DateTime.Now.AddMinutes(-1));SetRushRun(replayRun,601ul);
            typeof(BossRushSystem).GetField("AfterIssueCommit",Hidden).SetValue(null,(Action<GameState>)delegate {throw new IOException("review ambiguous issue");});
            try { Call(typeof(BossRushSystem),null,"TickInstance",replayRun,DateTime.Now); }
            finally { typeof(BossRushSystem).GetField("AfterIssueCommit",Hidden).SetValue(null,null); }
            var reconnect=ReloadBall(replayOwner);EntityVariableTable.Load(reconnect);
            string paidOnce=(string)Call(typeof(BossRushSystem),null,"TryClaim",reconnect);
            Call(typeof(BossRushSystem),null,"TickInstance",replayRun,DateTime.Now);
            var replayClaimant=ReloadBall(reconnect);EntityVariableTable.Load(replayClaimant);
            string paidTwice=(string)Call(typeof(BossRushSystem),null,"TryClaim",replayClaimant);
            verify(paidOnce.StartsWith("Claimed ") && !paidTwice.StartsWith("Claimed ")
                && replayClaimant.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp,
                "REVIEW-250: reconnect claim followed by the same producer retry pays one run once");

            var lostOutcome=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(lostOutcome);
            object lostRun=RushInstance(lostOutcome,3,DateTime.Now.AddMinutes(-1));SetRushRun(lostRun,701ul);
            Query("ALTER TABLE variablevault RENAME TO variablevault_review_away");
            try { Call(typeof(BossRushSystem),null,"TickInstance",lostRun,DateTime.Now); }
            finally { Query("ALTER TABLE variablevault_review_away RENAME TO variablevault"); }
            var coldOutcome=ReloadBall(lostOutcome);EntityVariableTable.Load(coldOutcome);
            BossRushSystem.RecoverPending(coldOutcome);
            verify((uint)coldOutcome[BossRushSystem.ClaimKey]==2u,
                "REVIEW-250: restart after failed issuance recovers the earned boss-3 outcome");

            var third=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(third);
            third.Entity.ConquerPoints=100;third.Entity.RevoraPoint=10;third.Entity.Money=1000;
            object thirdA=RushInstance(third,3,DateTime.Now.AddMinutes(-1));SetRushRun(thirdA,801ul);
            Call(typeof(BossRushSystem),null,"TickInstance",thirdA,DateTime.Now);
            object thirdB=RushInstance(third,4,DateTime.Now.AddMinutes(-1));SetRushRun(thirdB,802ul);
            Call(typeof(BossRushSystem),null,"TickInstance",thirdB,DateTime.Now);
            object thirdC=RushInstance(third,5,DateTime.Now.AddMinutes(-1));SetRushRun(thirdC,803ul);
            Call(typeof(BossRushSystem),null,"TickInstance",thirdC,DateTime.Now);
            VariableVault thirdDisk;EntityVariableTable.Load(third.Entity.UID,out thirdDisk);
            verify((ulong)thirdDisk[BossRushSystem.ClaimRunKey]==801ul && (ulong)thirdDisk[BossRushSystem.PendingRunKey]==802ul,
                "a third Boss Rush outcome stays in the run ledger without replacing the visible prizes");
            string thirdOne=(string)Call(typeof(BossRushSystem),null,"TryClaim",third);
            string thirdTwo=(string)Call(typeof(BossRushSystem),null,"TryClaim",third);
            string thirdThree=(string)Call(typeof(BossRushSystem),null,"TryClaim",third);
            string thirdFour=(string)Call(typeof(BossRushSystem),null,"TryClaim",third);
            verify(thirdOne.StartsWith("Claimed ") && thirdTwo.StartsWith("Claimed ") && thirdThree.StartsWith("Claimed ")
                && !thirdFour.StartsWith("Claimed ")
                && third.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp*3,
                "three stored Boss Rush outcomes each pay once");

            var ledgerLost=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(ledgerLost);
            object ledgerLostRun=RushInstance(ledgerLost,3,DateTime.Now.AddMinutes(-1));SetRushRun(ledgerLostRun,901ul);
            Query("ALTER TABLE revora_boss_rush_runs ADD CONSTRAINT review_253_ledger_fault CHECK (entityid <> "+ledgerLost.Entity.UID+") NOT VALID");
            try { Call(typeof(BossRushSystem),null,"TickInstance",ledgerLostRun,DateTime.Now); }
            finally { Query("ALTER TABLE revora_boss_rush_runs DROP CONSTRAINT review_253_ledger_fault"); }
            var ledgerCold=ReloadBall(ledgerLost);EntityVariableTable.Load(ledgerCold);
            BossRushSystem.RecoverPending(ledgerCold);
            verify((uint)ledgerCold[BossRushSystem.ClaimKey]==2u,
                "REVIEW-253: cold recovery retains earned consolation when the ledger write fails");

            VerifyRushRecoveryLockOrder(town,verify);
            var projecting=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(projecting);
            projecting.Entity.ConquerPoints=100;projecting.Entity.RevoraPoint=10;projecting.Entity.Money=1000;
            bool firstProjection=(bool)Call(typeof(BossRushSystem),null,"TryIssue",projecting,2u,903ul);
            string concurrentClaim="";
            typeof(BossRushSystem).GetField("AfterRunRecorded",Hidden).SetValue(null,(Action<GameState>)delegate {
                var current=ReloadBall(projecting);EntityVariableTable.Load(current);
                concurrentClaim=(string)Call(typeof(BossRushSystem),null,"TryClaim",current);
            });
            bool replayProjection=false;
            try { replayProjection=(bool)Call(typeof(BossRushSystem),null,"TryIssue",projecting,2u,903ul); }
            finally { typeof(BossRushSystem).GetField("AfterRunRecorded",Hidden).SetValue(null,null); }
            var projectionCold=ReloadBall(projecting);EntityVariableTable.Load(projectionCold);
            BossRushSystem.RecoverPending(projectionCold);
            verify(firstProjection && replayProjection && concurrentClaim.StartsWith("Claimed ")
                && (uint)projectionCold[BossRushSystem.ClaimKey]==0u
                && projectionCold.Entity.ConquerPoints==100+Revora.Config.BossRushConsolationCp,
                "REVIEW-253: a claim between ledger commit and projection cannot restore a consumed prize");

            var queuedProgress=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(queuedProgress);
            Call(typeof(BossRushSystem),null,"SaveProgress",queuedProgress,1001ul,1,0,2);
            Call(typeof(BossRushSystem),null,"SaveProgress",queuedProgress,1002ul,3,0,2);
            var queuedCold=ReloadBall(queuedProgress);EntityVariableTable.Load(queuedCold);
            BossRushSystem.RecoverPending(queuedCold);
            verify((uint)queuedCold[BossRushSystem.ClaimKey]==2u && (ulong)queuedCold[BossRushSystem.ClaimRunKey]==1002ul,
                "REVIEW-256: a no-prize failure does not hide later earned progress");

            var stableProgress=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(stableProgress);
            Call(typeof(BossRushSystem),null,"SaveProgress",stableProgress,1003ul,3,0,2);
            Call(typeof(BossRushSystem),null,"SaveProgress",stableProgress,1003ul,1,2,1);
            verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1003",stableProgress.Entity.UID)==3
                && Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1003",stableProgress.Entity.UID)==0,
                "REVIEW-256: delayed active checkpoint preserves terminal boss and lives");
            Call(typeof(BossRushSystem),null,"SaveProgress",stableProgress,1003ul,5,1,3);
            verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1003",stableProgress.Entity.UID)==2,
                "REVIEW-256: conflicting checkpoint cannot change a chosen failure into victory");
            Call(typeof(BossRushSystem),null,"SaveProgress",stableProgress,1004ul,4,1,1);
            Call(typeof(BossRushSystem),null,"SaveProgress",stableProgress,1004ul,2,2,1);
            verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1004",stableProgress.Entity.UID)==4
                && Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1004",stableProgress.Entity.UID)==1,
                "REVIEW-256: out-of-order active checkpoints cannot reduce earned progress or restore lives");

            ulong sequenceFailure=999ul;
            Query("ALTER SEQUENCE revora_boss_rush_run_seq MAXVALUE 2 RESTART WITH 2");
            try {
                Call(typeof(BossRushSystem),null,"NextRunId");
                sequenceFailure=(ulong)Call(typeof(BossRushSystem),null,"NextRunId");
            } finally { Query("ALTER SEQUENCE revora_boss_rush_run_seq MAXVALUE 9223372036854775807 RESTART WITH 100000"); }
            verify(sequenceFailure==0ul,"REVIEW-256: sequence failure cannot allocate a reusable process-local run identity");

            var confirmedProgress=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(confirmedProgress);
            verify((bool)Call(typeof(BossRushSystem),null,"SaveProgress",confirmedProgress,1301ul,3,0,2)
                && (bool)Call(typeof(BossRushSystem),null,"SaveProgress",confirmedProgress,1301ul,3,0,2),
                "REVIEW-259: identical terminal checkpoint retries are confirmed");
            verify(!(bool)Call(typeof(BossRushSystem),null,"SaveProgress",confirmedProgress,1301ul,5,1,3),
                "REVIEW-259: a rejected terminal change is not reported as a confirmed checkpoint");
            Call(typeof(BossRushSystem),null,"SaveProgress",confirmedProgress,1302ul,4,1,1);
            verify(!(bool)Call(typeof(BossRushSystem),null,"SaveProgress",confirmedProgress,1302ul,2,2,1),
                "REVIEW-259: a stale active checkpoint cannot acknowledge restored lives or an earlier boss");
            object conflictingFinish=RushInstance(confirmedProgress,5,DateTime.Now.AddMinutes(5));SetRushRun(conflictingFinish,1301ul);
            Call(typeof(BossRushSystem),null,"Win",conflictingFinish);
            verify(Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1301 AND kind=1",confirmedProgress.Entity.UID)==0
                && (uint)confirmedProgress[BossRushSystem.ClaimKey]==0u,
                "REVIEW-259: Finish cannot issue victory against a durable failed checkpoint");
            var contendedRecovery=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(contendedRecovery);
            Call(typeof(BossRushSystem),null,"SaveProgress",contendedRecovery,1303ul,3,1,1);
            Exception recoveryError=null;bool returnedWhileGateHeld=false;
            var recoveryThread=new System.Threading.Thread(delegate(){try{BossRushSystem.RecoverPending(contendedRecovery);}catch(Exception ex){recoveryError=ex;}}){IsBackground=true};
            lock(rushGate){recoveryThread.Start();returnedWhileGateHeld=recoveryThread.Join(5000);}
            bool recoveryEnded=recoveryThread.Join(10000);
            verify(returnedWhileGateHeld && recoveryEnded && recoveryError==null && (uint)contendedRecovery[BossRushSystem.ClaimKey]==0u,
                "REVIEW-259: recovery does not wait for registry ownership while holding player and database locks");
            BossRushSystem.RecoverPending(contendedRecovery);
            verify((uint)contendedRecovery[BossRushSystem.ClaimKey]==2u,
                "REVIEW-259: deferred recovery remains recoverable after registry contention ends");

            var mapLeaver=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(mapLeaver);
            object mapRun=RushInstance(mapLeaver,5,DateTime.Now.AddMinutes(5));SetRushRun(mapRun,1304ul);
            var emptyArena=new Map(10999,1002,"fixture",false);
            mapRun.GetType().GetField("Arena").SetValue(mapRun,emptyArena);
            Call(typeof(BossRushSystem),null,"SaveProgress",mapLeaver,1304ul,5,2,1);
            Call(typeof(BossRushSystem),null,"TickInstance",mapRun,DateTime.Now);
            verify((uint)mapLeaver[BossRushSystem.ClaimKey]==2u
                && Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1304",mapLeaver.Entity.UID)==2,
                "REVIEW-259: leaving the arena settles failure before an empty boss map can award victory");

            var activeLost=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(activeLost);
            Call(typeof(BossRushSystem),null,"SaveProgress",activeLost,1101ul,3,1,1);
            var activeCold=ReloadBall(activeLost);EntityVariableTable.Load(activeCold);
            BossRushSystem.RecoverPending(activeCold);
            verify((uint)activeCold[BossRushSystem.ClaimKey]==2u,
                "REVIEW-256: cold login recovers earned boss-3 progress after a mid-fight restart");

            var stillOwned=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(stillOwned);
            Call(typeof(BossRushSystem),null,"SaveProgress",stillOwned,1201ul,3,2,1);
            object ownedRun=RushInstance(stillOwned,3,DateTime.Now.AddMinutes(5));SetRushRun(ownedRun,1201ul);
            TrackRush(ownedRun);
            try { BossRushSystem.RecoverPending(stillOwned); }
            finally { UntrackRush(ownedRun); }
            verify((uint)stillOwned[BossRushSystem.ClaimKey]==0u,
                "REVIEW-256: an active boss-3 fight that is still owned is not settled");
            BossRushSystem.RecoverPending(stillOwned);
            verify((uint)stillOwned[BossRushSystem.ClaimKey]==2u,
                "REVIEW-256: the same boss-3 fight is settled once the instance is gone");

            var finalWriteLost=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(finalWriteLost);
            Call(typeof(BossRushSystem),null,"SaveProgress",finalWriteLost,1102ul,3,1,1);
            object finalWriteRun=RushInstance(finalWriteLost,3,DateTime.Now.AddMinutes(-1));SetRushRun(finalWriteRun,1102ul);
            Query("ALTER TABLE revora_boss_rush_progress ADD CONSTRAINT review_256_progress_fault CHECK (run_id <> 1102 OR phase=1) NOT VALID");
            Query("ALTER TABLE revora_boss_rush_runs ADD CONSTRAINT review_256_outcome_fault CHECK (run_id <> 1102) NOT VALID");
            try { Call(typeof(BossRushSystem),null,"TickInstance",finalWriteRun,DateTime.Now); }
            finally {
                Query("ALTER TABLE revora_boss_rush_progress DROP CONSTRAINT review_256_progress_fault");
                Query("ALTER TABLE revora_boss_rush_runs DROP CONSTRAINT review_256_outcome_fault");
            }
            var finalWriteCold=ReloadBall(finalWriteLost);EntityVariableTable.Load(finalWriteCold);
            BossRushSystem.RecoverPending(finalWriteCold);
            verify((uint)finalWriteCold[BossRushSystem.ClaimKey]==2u,
                "REVIEW-256: timeout with rejected final writes recovers consolation from prior durable boss-3 progress");

            var busyOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(busyOld);
            Call(typeof(BossRushSystem),null,"SaveProgress",busyOld,1502ul,3,2,1);
            object busyRun=RushInstance(busyOld,3,DateTime.Now.AddMinutes(5));SetRushRun(busyRun,1502ul);TrackRush(busyRun);
            var busyNew=ReloadBall(busyOld);EntityVariableTable.Load(busyNew);Kernel.GamePool[busyNew.Entity.UID]=busyNew;
            var cycleField=typeof(BossRushSystem).GetField("CycleStart",Hidden);object beforeCycle=cycleField.GetValue(null);
            try {
                Exception busyError=null;bool busyReturned=false;
                var busyThread=new System.Threading.Thread(delegate(){try{BossRushSystem.RecoverPending(busyNew);}catch(Exception ex){busyError=ex;}}){IsBackground=true};
                lock(rushGate){busyThread.Start();busyReturned=busyThread.Join(5000);}
                verify(busyThread.Join(10000) && busyReturned && busyError==null,"REVIEW-262: contended replacement recovery returns without blocking");
                cycleField.SetValue(null,DateTime.Now.AddHours(1));
                BossRushSystem.Tick();BossRushSystem.Tick();
                verify((uint)busyNew[BossRushSystem.ClaimKey]==2u,
                    "REVIEW-262: production ticks retry contended login recovery without another login");
            } finally { cycleField.SetValue(null,beforeCycle);UntrackRush(busyRun); }

            var queuedOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(queuedOld);
            Call(typeof(BossRushSystem),null,"TryIssue",queuedOld,2u,1503ul);
            Call(typeof(BossRushSystem),null,"SaveProgress",queuedOld,1504ul,3,2,1);
            object queuedRun=RushInstance(queuedOld,3,DateTime.Now.AddMinutes(5));SetRushRun(queuedRun,1504ul);TrackRush(queuedRun);
            var queuedNew=ReloadBall(queuedOld);EntityVariableTable.Load(queuedNew);Kernel.GamePool[queuedNew.Entity.UID]=queuedNew;
            try {
                BossRushSystem.RecoverPending(queuedNew);
                Call(typeof(BossRushSystem),null,"TickInstance",queuedRun,DateTime.Now);
                verify(Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1504 AND kind=2",queuedNew.Entity.UID)==1,
                    "REVIEW-262: an existing prize does not prevent replacement retirement from queuing its earned outcome");
            } finally { UntrackRush(queuedRun); }

            var earlyOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(earlyOwner);
            Call(typeof(BossRushSystem),null,"SaveProgress",earlyOwner,1606ul,2,2,1);
            object earlyFight=RushInstance(earlyOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(earlyFight,1606ul);TrackRush(earlyFight);
            var earlySession=ReloadBall(earlyOwner);EntityVariableTable.Load(earlySession);Kernel.GamePool[earlySession.Entity.UID]=earlySession;
            try {
                BossRushSystem.RecoverPending(earlySession);
                verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1606",earlySession.Entity.UID)==2
                    && (uint)earlySession[BossRushSystem.ClaimKey]==0u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1606",earlySession.Entity.UID)==0,
                    "REVIEW-265: early abandonment is terminal without inventing a consolation prize");
            } finally { UntrackRush(earlyFight); }

            var advancingOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(advancingOwner);
            Call(typeof(BossRushSystem),null,"SaveProgress",advancingOwner,1607ul,2,2,1);
            object advancingFight=RushInstance(advancingOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(advancingFight,1607ul);
            advancingFight.GetType().GetField("Arena").SetValue(advancingFight,emptyArena);
            advancingOwner.Entity.MapID=emptyArena.ID;TrackRush(advancingFight);Kernel.GamePool[advancingOwner.Entity.UID]=advancingOwner;
            var advancingNew=ReloadBall(advancingOwner);EntityVariableTable.Load(advancingNew);
            Kernel.GamePool[advancingOwner.Entity.UID]=advancingOwner;
            Exception advancingError=null;bool advancingWait=false;
            var advancingThread=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"StartBoss",advancingFight,3);}catch(Exception ex){advancingError=ex;}}){IsBackground=true};
            try {
                using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
                    int pid;
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
                    using(var q=new Npgsql.NpgsqlCommand("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1607 FOR UPDATE",conn,tx)){
                        q.Parameters.AddWithValue("uid",(long)advancingOwner.Entity.UID);q.ExecuteScalar();
                    }
                    advancingThread.Start();
                    advancingWait=System.Threading.SpinWait.SpinUntil(delegate{
                        return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;
                    },5000);
                    Kernel.GamePool[advancingNew.Entity.UID]=advancingNew;
                    tx.Rollback();
                }}
                verify(advancingThread.Join(10000) && advancingWait && advancingError==null,
                    "REVIEW-265: production advancement waits at the checkpoint across session replacement");
                BossRushSystem.RecoverPending(advancingNew);
                verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1607",advancingNew.Entity.UID)==2
                    && (uint)advancingNew[BossRushSystem.ClaimKey]==0u,
                    "REVIEW-265: retired producer cannot advance to reward eligibility after its database wait");
            } finally { UntrackRush(advancingFight); }

            var admissionOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(admissionOwner);
            Kernel.GamePool[admissionOwner.Entity.UID]=admissionOwner;
            Call(typeof(BossRushSystem),null,"SaveProgress",admissionOwner,1608ul,1,2,4);
            BossRushSystem.RecoverPending(admissionOwner);
            verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1608",admissionOwner.Entity.UID)==4
                && (uint)admissionOwner[BossRushSystem.ClaimKey]==0u,
                "REVIEW-265: an in-flight boss-1 admission stays admitting through recovery");
            var admissionSession=ReloadBall(admissionOwner);EntityVariableTable.Load(admissionSession);Kernel.GamePool[admissionSession.Entity.UID]=admissionSession;
            BossRushSystem.RecoverPending(admissionSession);
            verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1608",admissionSession.Entity.UID)==2
                && (uint)admissionSession[BossRushSystem.ClaimKey]==0u
                && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1608",admissionSession.Entity.UID)==0,
                "REVIEW-268: a replaced admitting checkpoint terminalizes without a prize");

            var coldEarly=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(coldEarly);
            Call(typeof(BossRushSystem),null,"SaveProgress",coldEarly,1610ul,1,2,1);
            var coldEarlySession=ReloadBall(coldEarly);EntityVariableTable.Load(coldEarlySession);Kernel.GamePool[coldEarlySession.Entity.UID]=coldEarlySession;
            BossRushSystem.RecoverPending(coldEarlySession);
            verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1610",coldEarlySession.Entity.UID)==2
                && (uint)coldEarlySession[BossRushSystem.ClaimKey]==0u
                && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1610",coldEarlySession.Entity.UID)==0,
                "REVIEW-268: cold active progress before boss 3 terminalizes without a prize");

            var tickFirst=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(tickFirst);
            Call(typeof(BossRushSystem),null,"SaveProgress",tickFirst,1609ul,2,2,1);
            object tickFight=RushInstance(tickFirst,2,DateTime.Now.AddMinutes(5));SetRushRun(tickFight,1609ul);
            tickFight.GetType().GetField("Arena").SetValue(tickFight,emptyArena);
            tickFirst.Entity.MapID=emptyArena.ID;TrackRush(tickFight);
            var tickSession=ReloadBall(tickFirst);EntityVariableTable.Load(tickSession);Kernel.GamePool[tickSession.Entity.UID]=tickSession;
            try {
                Call(typeof(BossRushSystem),null,"TickInstance",tickFight,DateTime.Now);
                verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1609",tickSession.Entity.UID)==1
                    && !(bool)tickFight.GetType().GetField("Failed").GetValue(tickFight),
                    "REVIEW-265: a replaced session tick leaves the early run registered");
                BossRushSystem.RecoverPending(tickSession);
                verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1609",tickSession.Entity.UID)==2
                    && (uint)tickSession[BossRushSystem.ClaimKey]==0u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1609",tickSession.Entity.UID)==0,
                    "REVIEW-265: recovery still closes that early run after the old tick");
            } finally { UntrackRush(tickFight); }

            var replacementOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(replacementOld);
            Call(typeof(BossRushSystem),null,"SaveProgress",replacementOld,1401ul,3,2,1);
            object replacementRun=RushInstance(replacementOld,3,DateTime.Now.AddMinutes(5));SetRushRun(replacementRun,1401ul);
            TrackRush(replacementRun);
            var replacementNew=ReloadBall(replacementOld);EntityVariableTable.Load(replacementNew);
            Kernel.GamePool[replacementNew.Entity.UID]=replacementNew;
            try {
                BossRushSystem.RecoverPending(replacementNew);
                Call(typeof(BossRushSystem),null,"TickInstance",replacementRun,DateTime.Now);
                verify((uint)replacementNew[BossRushSystem.ClaimKey]==2u,
                    "REVIEW-259: replacement login recovers its abandoned run without requiring a second login after old Tick");
            } finally { UntrackRush(replacementRun); }

            var retryDeath=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(retryDeath);
            object retryDeathRun=RushInstance(retryDeath,5,DateTime.Now.AddMinutes(5));SetRushRun(retryDeathRun,1402ul);
            retryDeathRun.GetType().GetField("Arena").SetValue(retryDeathRun,town);
            Call(typeof(BossRushSystem),null,"SaveProgress",retryDeath,1402ul,5,2,1);
            retryDeath.Entity.Hitpoints=0;
            TrackRush(retryDeathRun);
            Query("ALTER TABLE revora_boss_rush_progress ADD CONSTRAINT review_259_death_fault CHECK (run_id <> 1402 OR lives=2) NOT VALID");
            try { BossRushSystem.OnDeath(retryDeath); }
            finally { Query("ALTER TABLE revora_boss_rush_progress DROP CONSTRAINT review_259_death_fault"); }
            try { Call(typeof(BossRushSystem),null,"TickInstance",retryDeathRun,DateTime.Now); }
            finally { UntrackRush(retryDeathRun); }
            verify(Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1402",retryDeath.Entity.UID)==1,
                "REVIEW-259: Tick retries the rejected nonterminal death checkpoint before advancing or finishing");

            verify(retryDeath.Entity.Hitpoints > 0,
                "REVIEW-262: confirmed death retry revives the player before combat continues");

            var currentRushOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(currentRushOwner);
            var staleRecoverer=ReloadBall(currentRushOwner);EntityVariableTable.Load(staleRecoverer);
            Kernel.GamePool[currentRushOwner.Entity.UID]=currentRushOwner;
            Call(typeof(BossRushSystem),null,"SaveProgress",currentRushOwner,1501ul,3,2,1);
            object currentOwnedRush=RushInstance(currentRushOwner,3,DateTime.Now.AddMinutes(5));SetRushRun(currentOwnedRush,1501ul);
            TrackRush(currentOwnedRush);
            try {
                BossRushSystem.RecoverPending(staleRecoverer);
                verify(!(bool)currentOwnedRush.GetType().GetField("Failed").GetValue(currentOwnedRush)
                    && (uint)staleRecoverer[BossRushSystem.ClaimKey]==0u,
                    "REVIEW-262: a stale recovery caller cannot retire the current session's combat");
            } finally { UntrackRush(currentOwnedRush); }

            verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1401",replacementNew.Entity.UID)==2
                && !(bool)Call(typeof(BossRushSystem),null,"SaveProgress",replacementOld,1401ul,5,2,3),
                "REVIEW-262: recovered abandonment freezes failure before a delayed producer can choose victory");

            // Recovery must retire all predecessors before projecting one prize.
            for(int recoveryCase=0;recoveryCase<3;recoveryCase++) {
                var queueOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(queueOwner);
                if(recoveryCase==0) {
                    queueOwner[BossRushSystem.PendingKindKey]=2u;queueOwner[BossRushSystem.PendingRunKey]=1601ul;
                    EntityVariableTable.Save(queueOwner);
                } else if(recoveryCase==1) {
                    Query("INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,1601,2,false)","uid",(long)queueOwner.Entity.UID);
                }
                Call(typeof(BossRushSystem),null,"SaveProgress",queueOwner,1602ul,3,2,1);
                Call(typeof(BossRushSystem),null,"SaveProgress",queueOwner,1603ul,4,2,1);
                object queueFight=RushInstance(queueOwner,4,DateTime.Now.AddMinutes(5));SetRushRun(queueFight,1603ul);TrackRush(queueFight);
                var queueSession=ReloadBall(queueOwner);EntityVariableTable.Load(queueSession);Kernel.GamePool[queueSession.Entity.UID]=queueSession;
                try {
                    BossRushSystem.RecoverPending(queueSession);
                    verify(Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id IN (1602,1603) AND kind=2",queueSession.Entity.UID)==2
                        && Scalar("SELECT count(*) FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id IN (1602,1603) AND phase=2",queueSession.Entity.UID)==2,
                        "REVIEW-265: recovery queues every predecessor with pending, ledger or multiple progress rows case "+recoveryCase);
                } finally { UntrackRush(queueFight); }
            }

            var retryOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(retryOwner);
            Call(typeof(BossRushSystem),null,"SaveProgress",retryOwner,1604ul,3,2,1);
            Kernel.GamePool[retryOwner.Entity.UID]=retryOwner;
            Query("ALTER TABLE revora_boss_rush_runs ADD CONSTRAINT review_265_recovery_fault CHECK (run_id <> 1604) NOT VALID");
            try { BossRushSystem.RecoverPending(retryOwner); }
            finally { Query("ALTER TABLE revora_boss_rush_runs DROP CONSTRAINT review_265_recovery_fault"); }
            object retryCycle=cycleField.GetValue(null);
            try {
                cycleField.SetValue(null,DateTime.Now.AddHours(1));BossRushSystem.Tick();
                verify((uint)retryOwner[BossRushSystem.ClaimKey]==2u,
                    "REVIEW-265: production tick retries recovery after a rejected outcome transaction");
            } finally { cycleField.SetValue(null,retryCycle); }

            var retryStale=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(retryStale);
            Call(typeof(BossRushSystem),null,"SaveProgress",retryStale,1605ul,3,2,1);
            Kernel.GamePool[retryStale.Entity.UID]=retryStale;
            Exception retryError=null;
            var scheduleThread=new System.Threading.Thread(delegate(){try{BossRushSystem.RecoverPending(retryStale);}catch(Exception ex){retryError=ex;}}){IsBackground=true};
            bool scheduled;
            lock(rushGate){scheduleThread.Start();scheduled=scheduleThread.Join(5000);}
            verify(scheduleThread.Join(10000) && scheduled && retryError==null,"REVIEW-265: recovery retry can be scheduled without waiting on registry");
            var retryCurrent=ReloadBall(retryStale);EntityVariableTable.Load(retryCurrent);Kernel.GamePool[retryCurrent.Entity.UID]=retryCurrent;
            try {
                cycleField.SetValue(null,DateTime.Now.AddHours(1));BossRushSystem.Tick();
                verify((uint)retryCurrent[BossRushSystem.ClaimKey]==2u && (uint)retryStale[BossRushSystem.ClaimKey]==0u,
                    "REVIEW-265: scheduled recovery resolves the current session after another replacement");
            } finally { cycleField.SetValue(null,retryCycle); }

            for (int rollbackBoss=2; rollbackBoss<=3; rollbackBoss++) {
                var rollbackOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(rollbackOld);
                Call(typeof(BossRushSystem),null,"SaveProgress",rollbackOld,1701ul,rollbackBoss,2,1);
                object rollbackFight=RushInstance(rollbackOld,rollbackBoss,DateTime.Now.AddMinutes(5));SetRushRun(rollbackFight,1701ul);TrackRush(rollbackFight);
                var rollbackNew=ReloadBall(rollbackOld);EntityVariableTable.Load(rollbackNew);Kernel.GamePool[rollbackNew.Entity.UID]=rollbackNew;
                Query("ALTER TABLE revora_boss_rush_progress ADD CONSTRAINT review_268_retire_fault CHECK (run_id <> 1701 OR phase=1) NOT VALID");
                try {
                    try { BossRushSystem.RecoverPending(rollbackNew); }
                    finally { Query("ALTER TABLE revora_boss_rush_progress DROP CONSTRAINT review_268_retire_fault"); }
                    verify(!(bool)rollbackFight.GetType().GetField("Failed").GetValue(rollbackFight)
                        && object.ReferenceEquals(Call(typeof(BossRushSystem),null,"FindByPlayer",rollbackOld.Entity.UID),rollbackFight),
                        "REVIEW-268: rejected retirement retains registry metadata for boss "+rollbackBoss);
                    object rollbackCycle=cycleField.GetValue(null);
                    try { cycleField.SetValue(null,DateTime.Now.AddHours(1));BossRushSystem.Tick(); }
                    finally { cycleField.SetValue(null,rollbackCycle); }
                    verify(Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1701",rollbackNew.Entity.UID)==2
                        && (uint)rollbackNew[BossRushSystem.ClaimKey]==(rollbackBoss>=3?2u:0u)
                        && (bool)rollbackFight.GetType().GetField("Failed").GetValue(rollbackFight)
                        && Call(typeof(BossRushSystem),null,"FindByPlayer",rollbackOld.Entity.UID)==null,
                        "REVIEW-268: normal tick completes rejected retirement with the correct prize for boss "+rollbackBoss);
                } finally { UntrackRush(rollbackFight); }
            }

            for(int durableCase=0;durableCase<3;durableCase++) {
                int durableBoss=durableCase==0?2:3;
                var durableOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(durableOld);
                Call(typeof(BossRushSystem),null,"SaveProgress",durableOld,1702ul,durableBoss,2,1);
                object durableFight=RushInstance(durableOld,durableBoss,DateTime.Now.AddMinutes(5));SetRushRun(durableFight,1702ul);TrackRush(durableFight);
                var durableNew=ReloadBall(durableOld);EntityVariableTable.Load(durableNew);Kernel.GamePool[durableNew.Entity.UID]=durableNew;
                // Simulate committed retirement whose acknowledgement/metadata
                // cleanup was lost, including a subsequently consumed receipt.
                Query("UPDATE revora_boss_rush_progress SET phase=2 WHERE entityid=@uid AND run_id=1702","uid",(long)durableNew.Entity.UID);
                if(durableCase!=0)Query("INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,1702,2,@consumed)","uid",(long)durableNew.Entity.UID,"consumed",durableCase==2);
                try {
                    BossRushSystem.RecoverPending(durableNew);
                    BossRushSystem.RecoverPending(durableNew);
                    verify((bool)durableFight.GetType().GetField("Failed").GetValue(durableFight)
                        && Call(typeof(BossRushSystem),null,"FindByPlayer",durableOld.Entity.UID)==null
                        && (uint)durableNew[BossRushSystem.ClaimKey]==(durableCase==1?2u:0u)
                        && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=1702",durableNew.Entity.UID)==(durableCase==0?0:1),
                        "REVIEW-268: durable retirement retries clean metadata and preserve receipts case "+durableCase);
                } finally { UntrackRush(durableFight); }
            }

            var commitOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(commitOld);
            Call(typeof(BossRushSystem),null,"SaveProgress",commitOld,1703ul,2,2,1);
            object commitFight=RushInstance(commitOld,2,DateTime.Now.AddMinutes(5));SetRushRun(commitFight,1703ul);
            commitFight.GetType().GetField("Arena").SetValue(commitFight,emptyArena);commitOld.Entity.MapID=emptyArena.ID;TrackRush(commitFight);
            var commitNew=ReloadBall(commitOld);EntityVariableTable.Load(commitNew);Kernel.GamePool[commitOld.Entity.UID]=commitOld;
            Query("CREATE FUNCTION review_268_commit_barrier() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.run_id=1703 THEN PERFORM pg_advisory_xact_lock(2681703); END IF; RETURN NEW; END'");
            Query("CREATE CONSTRAINT TRIGGER review_268_commit_barrier AFTER UPDATE ON revora_boss_rush_progress DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION review_268_commit_barrier()");
            Exception commitError=null;bool commitWait=false;
            var commitThread=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"StartBoss",commitFight,3);}catch(Exception ex){commitError=ex;}}){IsBackground=true};
            try {
                using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
                    int pid;
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_advisory_xact_lock(2681703)",conn,tx))q.ExecuteScalar();
                    commitThread.Start();
                    commitWait=System.Threading.SpinWait.SpinUntil(delegate{return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;},5000);
                    Kernel.GamePool[commitNew.Entity.UID]=commitNew;
                    tx.Rollback();
                }}
                verify(commitThread.Join(10000) && commitWait && commitError==null,
                    "REVIEW-268: advancement reaches the deferred constraint barrier while it still holds the session lease");
                BossRushSystem.RecoverPending(commitNew);
                verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1703",commitNew.Entity.UID)==2
                    && (uint)commitNew[BossRushSystem.ClaimKey]==0u,
                    "REVIEW-268: replacement during COMMIT cannot advance a retired producer into consolation eligibility");
            } finally {
                Query("DROP TRIGGER review_268_commit_barrier ON revora_boss_rush_progress; DROP FUNCTION review_268_commit_barrier()");
                UntrackRush(commitFight);
            }

            var installType=typeof(BossRushSystem).Assembly.GetType("Revora.Game.Features.SessionInstall");
            var orderedOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(orderedOld);
            Call(typeof(BossRushSystem),null,"SaveProgress",orderedOld,1704ul,2,2,1);
            object orderedFight=RushInstance(orderedOld,2,DateTime.Now.AddMinutes(5));SetRushRun(orderedFight,1704ul);
            orderedFight.GetType().GetField("Arena").SetValue(orderedFight,emptyArena);orderedOld.Entity.MapID=emptyArena.ID;TrackRush(orderedFight);
            var orderedNew=ReloadBall(orderedOld);EntityVariableTable.Load(orderedNew);Kernel.GamePool[orderedOld.Entity.UID]=orderedOld;
            long orderedEpoch=(long)Call(installType,null,"Epoch",orderedOld.Entity.UID);
            Query("CREATE FUNCTION review_268_install_barrier() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.run_id=1704 THEN PERFORM pg_advisory_xact_lock(2681704); END IF; RETURN NEW; END'");
            Query("CREATE CONSTRAINT TRIGGER review_268_install_barrier AFTER UPDATE ON revora_boss_rush_progress DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION review_268_install_barrier()");
            Exception orderedError=null;bool orderedWait=false;bool installedDuringBarrier=false;
            var orderedThread=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"StartBoss",orderedFight,3);}catch(Exception ex){orderedError=ex;}}){IsBackground=true};
            var installThread=new System.Threading.Thread(delegate(){Call(installType,null,"Install",orderedNew);installedDuringBarrier=true;}){IsBackground=true};
            try {
                using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
                    int pid;
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_advisory_xact_lock(2681704)",conn,tx))q.ExecuteScalar();
                    orderedThread.Start();
                    orderedWait=System.Threading.SpinWait.SpinUntil(delegate{return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;},5000);
                    installThread.Start();
                    System.Threading.Thread.Sleep(300);
                    verify(!installedDuringBarrier && (long)Call(installType,null,"Epoch",orderedOld.Entity.UID)==orderedEpoch,
                        "REVIEW-268: session installation does not finish during an in-flight checkpoint");
                    tx.Rollback();
                }}
                verify(orderedThread.Join(10000) && installThread.Join(10000) && orderedWait && orderedError==null && installedDuringBarrier,
                    "REVIEW-268: session installation continues after the checkpoint releases its lease");
                verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1704",orderedNew.Entity.UID)==3,
                    "REVIEW-268: an earlier checkpoint may commit before replacement installation");
                BossRushSystem.RecoverPending(orderedNew);
                verify((uint)orderedNew[BossRushSystem.ClaimKey]==2u,
                    "REVIEW-268: replacement after that commit recovers the committed boss");
            } finally {
                Query("DROP TRIGGER review_268_install_barrier ON revora_boss_rush_progress; DROP FUNCTION review_268_install_barrier()");
                UntrackRush(orderedFight);
            }

            var retiredFirst=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(retiredFirst);
            Call(typeof(BossRushSystem),null,"SaveProgress",retiredFirst,1705ul,2,2,1);
            object retiredFight=RushInstance(retiredFirst,2,DateTime.Now.AddMinutes(5));SetRushRun(retiredFight,1705ul);
            retiredFight.GetType().GetField("Arena").SetValue(retiredFight,emptyArena);retiredFirst.Entity.MapID=emptyArena.ID;TrackRush(retiredFight);
            var retiredNext=ReloadBall(retiredFirst);EntityVariableTable.Load(retiredNext);
            try {
                Call(installType,null,"Install",retiredNext);
                Call(typeof(BossRushSystem),null,"StartBoss",retiredFight,3);
                BossRushSystem.RecoverPending(retiredNext);
                verify(Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1705",retiredNext.Entity.UID)==2
                    && (uint)retiredNext[BossRushSystem.ClaimKey]==0u
                    && (int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",retiredFirst,1705ul,3,2,1,false)==2,
                    "REVIEW-268: a producer ordered after installation cannot commit or take consolation");
            } finally { UntrackRush(retiredFight); }

            var gateOld=WelcomePlayer(town,1,0,15);var gateNew=ReloadBall(gateOld);EntityVariableTable.Load(gateNew);
            bool gateInstalled=false;
            var gateThread=new System.Threading.Thread(delegate(){Call(installType,null,"Install",gateNew);gateInstalled=true;}){IsBackground=true};
            lock(rushGate){gateThread.Start();verify(gateThread.Join(2000) && gateInstalled,
                "REVIEW-268: session installation does not wait on the Boss Rush registry gate");}

            // A rejected monotonic merge must not commit its other fields.
            var mixedCheckpoint=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(mixedCheckpoint);
            Call(typeof(BossRushSystem),null,"SaveProgress",mixedCheckpoint,1801ul,2,1,1);
            verify((int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",mixedCheckpoint,1801ul,3,2,1,false)==2
                && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1801",mixedCheckpoint.Entity.UID)==2,
                "REVIEW-271: a conflicting life preimage cannot partially commit boss advancement");
            Call(typeof(BossRushSystem),null,"SaveProgress",mixedCheckpoint,1802ul,4,2,1);
            verify((int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",mixedCheckpoint,1802ul,2,1,2,false)==2
                && Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1802",mixedCheckpoint.Entity.UID)==1
                && Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1802",mixedCheckpoint.Entity.UID)==2,
                "REVIEW-271: a conflicting boss preimage cannot partially commit terminal phase or life loss");

            var removedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(removedOwner);
            Call(typeof(BossRushSystem),null,"SaveProgress",removedOwner,1803ul,2,2,1);
            object removedFight=RushInstance(removedOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(removedFight,1803ul);
            removedFight.GetType().GetField("Arena").SetValue(removedFight,emptyArena);removedOwner.Entity.MapID=emptyArena.ID;
            Call(installType,null,"Retire",removedOwner);
            Call(typeof(BossRushSystem),null,"TickInstance",removedFight,DateTime.Now);
            verify((int)removedFight.GetType().GetField("Boss").GetValue(removedFight)==2
                && (int)Call(typeof(BossRushSystem),null,"SaveCheckpoint",removedOwner,1803ul,3,2,1,false)==2
                && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1803",removedOwner.Entity.UID)==2,
                "REVIEW-271: removal through the session API rejects later tick and checkpoint producers");
            BossRushSystem.RecoverPending(removedOwner);
            verify((uint)removedOwner[BossRushSystem.ClaimKey]==0u
                && Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1803",removedOwner.Entity.UID)==1,
                "REVIEW-271: an offline session cannot settle its own abandoned progress");
            var removedCold=ReloadBall(removedOwner);EntityVariableTable.Load(removedCold);
            Call(installType,null,"Install",removedCold);BossRushSystem.RecoverPending(removedCold);
            verify((uint)removedCold[BossRushSystem.ClaimKey]==0u
                && Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1803",removedCold.Entity.UID)==2,
                "REVIEW-271: the next installed session closes a removed early run without a prize");

            Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL); CREATE TABLE IF NOT EXISTS sobnpcs (LIKE public.sobnpcs INCLUDING ALL); CREATE TABLE IF NOT EXISTS furniture (LIKE public.furniture INCLUDING ALL)");
            // Real Admit stops at the registry boundary after its durable reservation.
            var admissionOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(admissionOld);
            var admissionNew=ReloadBall(admissionOld);EntityVariableTable.Load(admissionNew);
            Kernel.GamePool[admissionOld.Entity.UID]=admissionOld;
            Exception admissionError=null;string admissionResult=null;bool admissionReserved=false,admissionInstalled=false;
            var admissionThread=new System.Threading.Thread(delegate(){try{admissionResult=(string)Call(typeof(BossRushSystem),null,"Admit",admissionOld,false,false);}catch(Exception ex){admissionError=ex;}}){IsBackground=true};
            var admissionInstall=new System.Threading.Thread(delegate(){Call(installType,null,"Install",admissionNew);admissionInstalled=true;}){IsBackground=true};
            long admissionRun=0;
            Map admissionSource=Kernel.Maps.ContainsKey(700)?Kernel.Maps[700]:(Kernel.Maps.ContainsKey(1005)?Kernel.Maps[1005]:town);
            string admissionPath=admissionSource.Path;admissionSource.Path="fixture";
            lock(rushGate){
                admissionThread.Start();
                admissionReserved=System.Threading.SpinWait.SpinUntil(delegate{return Scalar("SELECT count(*) FROM revora_boss_rush_progress WHERE entityid=@uid AND phase=4",admissionOld.Entity.UID)>0;},5000);
                if(admissionReserved)admissionRun=Scalar("SELECT run_id FROM revora_boss_rush_progress WHERE entityid=@uid AND phase=4",admissionOld.Entity.UID);
                admissionInstall.Start();
                admissionInstalled=admissionInstall.Join(2000);
            }
            bool admissionEnded=admissionThread.Join(10000) && admissionInstall.Join(10000);
            admissionSource.Path=admissionPath;
            if(admissionError!=null)System.Console.WriteLine("ADMISSION_PROBE_EXCEPTION="+admissionError.GetType().Name+" "+admissionError.StackTrace);
            verify(admissionReserved && admissionEnded && admissionError==null,
                "REVIEW-271-BLOCKER: actual admission and installation barrier setup completes");
            BossRushSystem.RecoverPending(admissionNew);
            object abandonedAdmission=Call(typeof(BossRushSystem),null,"FindByPlayer",admissionOld.Entity.UID);
            verify(!admissionInstalled || (abandonedAdmission==null && admissionOld.Entity.MapID==town.ID
                && Scalar("SELECT count(*) FROM revora_boss_rush_progress WHERE entityid=@uid AND phase=4",admissionNew.Entity.UID)==0),
                "REVIEW-271-BLOCKER: replacement ordered before admission registration prevents stale teleport and stranded admitting progress");
            if(abandonedAdmission!=null){
                UntrackRush(abandonedAdmission);
                var admissionArena=(Map)abandonedAdmission.GetType().GetField("Arena").GetValue(abandonedAdmission);
                var mapIndex=(System.Collections.IDictionary)typeof(BossRushSystem).GetField("ByMap",Hidden).GetValue(null);
                if(admissionArena!=null)mapIndex.Remove(admissionArena.ID);
            }
            if(admissionRun>0)Call(typeof(BossRushSystem),null,"AbandonAdmission",(ulong)admissionRun);

            // The login publication callback invokes real Boss Rush disconnect.
            var callbackOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(callbackOld);
            object callbackFight=RushInstance(callbackOld,2,DateTime.Now.AddMinutes(5));SetRushRun(callbackFight,1804ul);TrackRush(callbackFight);
            var callbackNew=ReloadBall(callbackOld);EntityVariableTable.Load(callbackNew);Kernel.GamePool[callbackOld.Entity.UID]=callbackOld;
            Exception callbackError=null;bool callbackReturned=false;
            var callbackThread=new System.Threading.Thread(delegate(){try{Call(installType,null,"Replace",callbackOld.Entity.UID,callbackNew,(Action)delegate{BossRushSystem.OnDisconnect(callbackOld);});}catch(Exception ex){callbackError=ex;}}){IsBackground=true};
            try {
                lock(rushGate){callbackThread.Start();callbackReturned=callbackThread.Join(2000);}
                verify(callbackThread.Join(10000) && callbackError==null,
                    "REVIEW-271-BLOCKER: installation with actual event-disconnect callback completes after registry release");
                verify(callbackReturned,
                    "REVIEW-271-BLOCKER: installation with event-disconnect callback does not wait on the global registry gate");
            } finally {UntrackRush(callbackFight);}

            // A busy drain must retain the entire batch, including players
            // after the first contended lookup.
            var leaveOne=WelcomePlayer(town,1,0,15);
            var leaveTwo=WelcomePlayer(town,1,0,15);
            var pendingLeaves=(System.Collections.IList)typeof(BossRushSystem).GetField("PendingLeave",Hidden).GetValue(null);
            Exception leaveError=null;
            lock(pendingLeaves){pendingLeaves.Clear();pendingLeaves.Add(leaveOne);pendingLeaves.Add(leaveTwo);}
            var leaveDrain=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"DrainLeaves");}catch(Exception ex){leaveError=ex;}}){IsBackground=true};
            bool leaveReturned;
            lock(rushGate){leaveDrain.Start();leaveReturned=leaveDrain.Join(2000);}
            verify(leaveReturned && leaveError==null && pendingLeaves.Contains(leaveOne) && pendingLeaves.Contains(leaveTwo),
                "REVIEW-274: a busy registry preserves every queued disconnect for retry");
            lock(pendingLeaves)pendingLeaves.Clear();

            // Login publishes under Account.EntityID before loading the entity.
            var partialLogin=new GameState(null);
            partialLogin.Fake=false;
            partialLogin.Account=new AccountTable(null);partialLogin.Account.EntityID=leaveOne.Entity.UID;
            partialLogin.Entity=new Entity(EntityFlag.Monster,false);
            Call(installType,null,"Replace",partialLogin.Account.EntityID,partialLogin,null);
            GameState partialCurrent;
            verify(partialLogin.Entity.UID==0 && Kernel.GamePool.TryGetValue(partialLogin.Account.EntityID,out partialCurrent)
                && object.ReferenceEquals(partialCurrent,partialLogin),
                "REVIEW-274: partial login fixture publishes an ordinary session with a zero-UID placeholder");
            Call(installType,null,"Retire",partialLogin);
            verify(!Kernel.GamePool.TryGetValue(partialLogin.Account.EntityID,out partialCurrent),
                "REVIEW-274: retirement removes a published partial login with a zero-UID placeholder entity");
            Call(installType,null,"Install",leaveOne);
            Call(installType,null,"Retire",partialLogin);
            verify(Kernel.GamePool.TryGetValue(leaveOne.Entity.UID,out partialCurrent) && object.ReferenceEquals(partialCurrent,leaveOne),
                "REVIEW-274: partial login retirement cannot remove a replacement session");

            var gateStale=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(gateStale);
            object gateFight=RushInstance(gateStale,2,DateTime.Now.AddMinutes(5));SetRushRun(gateFight,1901ul);
            gateFight.GetType().GetField("Arena").SetValue(gateFight,emptyArena);
            Call(typeof(BossRushSystem),null,"SaveProgress",gateStale,1901ul,2,2,1);
            Call(installType,null,"Retire",gateStale);
            lock(rushGate)Call(typeof(BossRushSystem),null,"StartBoss",gateFight,3);
            verify((int)gateFight.GetType().GetField("Boss").GetValue(gateFight)==2
                && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1901",gateStale.Entity.UID)==2,
                "REVIEW-274: holding the registry gate cannot bypass checkpoint ownership and spawn a stale boss");

            // Two production ticks observe the same cleared boss while the
            // first transition is stopped at its durable row checkpoint.
            var doubleOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(doubleOwner);
            var doubleArena=new Map(10998,1002,"fixture",false);
            doubleArena.Floor=town.Floor;doubleArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            doubleArena.EntityUIDCounter=new Counter(600000);
            object doubleFight=RushInstance(doubleOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(doubleFight,1902ul);
            doubleFight.GetType().GetField("Arena").SetValue(doubleFight,doubleArena);
            doubleFight.GetType().GetField("SpawnX").SetValue(doubleFight,(ushort)408);
            doubleFight.GetType().GetField("SpawnY").SetValue(doubleFight,(ushort)330);
            doubleOwner.Entity.MapID=doubleArena.ID;TrackRush(doubleFight);
            MonsterInformation savedDoubleMob;
            bool hadDoubleMob=MonsterInformation.MonsterInformations.TryGetValue(4212,out savedDoubleMob);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Review boss",Level=140,Hitpoints=6000000};
            Call(typeof(BossRushSystem),null,"SaveProgress",doubleOwner,1902ul,2,2,1);
            Exception doubleErrorA=null,doubleErrorB=null;bool doubleWait=false,doubleSecondWaiting=false;
            var doubleTickA=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"TickInstance",doubleFight,DateTime.Now);}catch(Exception ex){doubleErrorA=ex;}}){IsBackground=true};
            var doubleTickB=new System.Threading.Thread(delegate(){try{Call(typeof(BossRushSystem),null,"TickInstance",doubleFight,DateTime.Now);}catch(Exception ex){doubleErrorB=ex;}}){IsBackground=true};
            try {
                using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
                    int pid;
                    using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
                    using(var q=new Npgsql.NpgsqlCommand("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1902 FOR UPDATE",conn,tx)){
                        q.Parameters.AddWithValue("uid",(long)doubleOwner.Entity.UID);q.ExecuteScalar();
                    }
                    doubleTickA.Start();
                    doubleWait=System.Threading.SpinWait.SpinUntil(delegate{return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;},5000);
                    doubleTickB.Start();
                    doubleSecondWaiting=System.Threading.SpinWait.SpinUntil(delegate{return (doubleTickB.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0;},2000);
                    tx.Rollback();
                }}
                bool doubleJoined=doubleTickA.Join(10000) && doubleTickB.Join(10000);
                if(doubleErrorA!=null)System.Console.WriteLine("DOUBLE_TICK_A="+doubleErrorA.GetType().Name+" "+doubleErrorA.StackTrace);
                if(doubleErrorB!=null)System.Console.WriteLine("DOUBLE_TICK_B="+doubleErrorB.GetType().Name+" "+doubleErrorB.StackTrace);
                verify(doubleWait && doubleSecondWaiting && doubleJoined && doubleErrorA==null && doubleErrorB==null,
                    "REVIEW-274: concurrent production ticks reach checkpoint and lease waits without exceptions");
                int doubleMobs=(int)Call(typeof(BossRushSystem),null,"RemainingMobs",doubleFight);
                System.Console.WriteLine("REVIEW-274-LIVE-BOSSES="+doubleMobs);
                verify(doubleMobs==1 && (int)doubleFight.GetType().GetField("Boss").GetValue(doubleFight)==3
                    && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=1902",doubleOwner.Entity.UID)==3,
                    "REVIEW-274: concurrent ticks advance and spawn a cleared boss exactly once");
            } finally {
                UntrackRush(doubleFight);
                if(hadDoubleMob)MonsterInformation.MonsterInformations[4212]=savedDoubleMob;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            // Stop real admission inside an actual teleport guard, after its
            // registry publication and last admission ownership lookup.
            var enteringOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(enteringOld);
            var enteringNew=ReloadBall(enteringOld);EntityVariableTable.Load(enteringNew);
            Kernel.GamePool[enteringOld.Entity.UID]=enteringOld;
            object teleportGate=typeof(BossRushSystem).Assembly.GetType("Revora.Game.Features.WarriorEpicScenes").GetField("gate",Hidden).GetValue(null);
            Exception enteringError=null,enteringInstallError=null;bool enteringPublished=false,enteringBlocked=false,enteringInstalled=false;
            string enteringResult=null;
            var enteringThread=new System.Threading.Thread(delegate(){try{enteringResult=(string)Call(typeof(BossRushSystem),null,"Admit",enteringOld,false,false);}catch(Exception ex){enteringError=ex;}}){IsBackground=true};
            var enteringInstall=new System.Threading.Thread(delegate(){try{Call(installType,null,"Replace",enteringOld.Entity.UID,enteringNew,(Action)delegate{BossRushSystem.OnDisconnect(enteringOld);});}catch(Exception ex){enteringInstallError=ex;}}){IsBackground=true};
            object enteringFight=null;
            string enteringPath=admissionSource.Path;admissionSource.Path="fixture";
            string enteringMapPath;
            bool hadEnteringMapPath=DMaps.MapPaths.TryGetValue(admissionSource.ID,out enteringMapPath);
            DMaps.MapPaths[admissionSource.ID]="fixture";
            try {
                lock(teleportGate){
                    enteringThread.Start();
                    enteringPublished=System.Threading.SpinWait.SpinUntil(delegate{enteringFight=Call(typeof(BossRushSystem),null,"FindByPlayer",enteringOld.Entity.UID);return enteringFight!=null;},5000);
                    enteringBlocked=System.Threading.SpinWait.SpinUntil(delegate{return (enteringThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0;},2000);
                    enteringInstall.Start();enteringInstalled=enteringInstall.Join(2000);
                }
                bool enteringJoined=enteringThread.Join(10000) && enteringInstall.Join(10000);
                if(enteringError!=null)System.Console.WriteLine("ENTERING_ERROR="+enteringError.GetType().Name+" "+enteringError.StackTrace);
                verify(enteringPublished && enteringBlocked && enteringJoined && enteringError==null && enteringInstallError==null,
                    "REVIEW-274-BLOCKER: real admission reaches the teleport guard after registry publication");
                verify(!enteringInstalled && object.ReferenceEquals(Kernel.GamePool[enteringNew.Entity.UID],enteringNew),
                    "REVIEW-280: replacement with the event disconnect callback waits for entry and then publishes");
                verify(!enteringInstalled || enteringOld.Entity.MapID==town.ID,
                    "REVIEW-274: replaced admission ends on the original map after the teleport guard is released");
                verify(!enteringInstalled || enteringOld.Entity.PreviousMapID != ((Map)enteringFight.GetType().GetField("Arena").GetValue(enteringFight)).ID,
                    "REVIEW-277-BLOCKER: entry cannot teleport the old session through the arena after replacement publishes");
                BossRushSystem.RecoverPending(enteringNew);
            } finally {
                admissionSource.Path=enteringPath;
                if(hadEnteringMapPath)DMaps.MapPaths[admissionSource.ID]=enteringMapPath;
                else DMaps.MapPaths.Remove(admissionSource.ID);
                if(enteringFight!=null)Call(typeof(BossRushSystem),null,"RemoveMetadata",enteringFight);
            }

            // Both initial death and pending-life retry must retain ownership
            // after the checkpoint, through the actual revive teleport guard.
            foreach(bool retryLife in new[]{false,true}) {
                var lifeOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(lifeOld);
                var lifeNew=ReloadBall(lifeOld);EntityVariableTable.Load(lifeNew);
                Kernel.GamePool[lifeOld.Entity.UID]=lifeOld;
                ulong lifeRun=retryLife?2102ul:2101ul;
                object lifeFight=RushInstance(lifeOld,2,DateTime.Now.AddMinutes(5));SetRushRun(lifeFight,lifeRun);
                lifeFight.GetType().GetField("Arena").SetValue(lifeFight,town);
                lifeFight.GetType().GetField("SpawnX").SetValue(lifeFight,(ushort)408);
                lifeFight.GetType().GetField("SpawnY").SetValue(lifeFight,(ushort)330);
                lifeFight.GetType().GetField("LifeDropPending").SetValue(lifeFight,retryLife);
                lifeFight.GetType().GetField("PendingLives").SetValue(lifeFight,1);
                TrackRush(lifeFight);
                Call(typeof(BossRushSystem),null,"SaveProgress",lifeOld,lifeRun,2,2,1);
                Exception lifeError=null,lifeInstallError=null;bool lifeBlocked=false,lifeInstalled=false;
                var lifeThread=new System.Threading.Thread(delegate(){try{
                    if(retryLife)Call(typeof(BossRushSystem),null,"TickInstance",lifeFight,DateTime.Now);
                    else BossRushSystem.OnDeath(lifeOld);
                }catch(Exception ex){lifeError=ex;}}){IsBackground=true};
                var lifeInstall=new System.Threading.Thread(delegate(){try{Call(installType,null,"Install",lifeNew);}catch(Exception ex){lifeInstallError=ex;}}){IsBackground=true};
                try {
                    lock(teleportGate){
                        lifeThread.Start();
                        lifeBlocked=System.Threading.SpinWait.SpinUntil(delegate{
                            return Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid",lifeOld.Entity.UID)==1
                                && (lifeThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0;
                        },5000);
                        lifeInstall.Start();lifeInstalled=lifeInstall.Join(1000);
                    }
                    bool lifeJoined=lifeThread.Join(10000) && lifeInstall.Join(10000);
                    verify(lifeBlocked && lifeJoined && lifeError==null && lifeInstallError==null,
                        "REVIEW-280: life transition reaches the real revive guard and workers finish: retry="+retryLife);
                    verify(!lifeInstalled && object.ReferenceEquals(Kernel.GamePool[lifeNew.Entity.UID],lifeNew),
                        "REVIEW-280: replacement waits through committed life transition and live revival: retry="+retryLife);
                } finally {UntrackRush(lifeFight);}
                BossRushSystem.RecoverPending(lifeNew);
            }

            // Missing monster data must not manufacture boss-3 eligibility on disk.
            var missingBossOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(missingBossOwner);
            object missingBossFight=RushInstance(missingBossOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(missingBossFight,2001ul);
            missingBossFight.GetType().GetField("Arena").SetValue(missingBossFight,doubleArena);
            Call(typeof(BossRushSystem),null,"SaveProgress",missingBossOwner,2001ul,2,2,1);
            MonsterInformation missingPrimary,missingFallback;
            bool hadMissingPrimary=MonsterInformation.MonsterInformations.TryGetValue(4212,out missingPrimary);
            bool hadMissingFallback=MonsterInformation.MonsterInformations.TryGetValue(2,out missingFallback);
            try {
                MonsterInformation.MonsterInformations.Remove(4212);
                MonsterInformation.MonsterInformations.Remove(2);
                Call(typeof(BossRushSystem),null,"StartBoss",missingBossFight,3);
                verify((int)missingBossFight.GetType().GetField("Boss").GetValue(missingBossFight)==2
                    && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2001",missingBossOwner.Entity.UID)==2,
                    "REVIEW-277: missing boss catalogs preserve both live and durable boss-2 progress");
                var missingBossNext=ReloadBall(missingBossOwner);EntityVariableTable.Load(missingBossNext);
                Call(installType,null,"Install",missingBossNext);
                BossRushSystem.RecoverPending(missingBossNext);
                verify((uint)missingBossNext[BossRushSystem.ClaimKey]==0u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2001",missingBossOwner.Entity.UID)==0,
                    "REVIEW-277: reconnect cannot earn consolation for a boss that never spawned");
            } finally {
                if(hadMissingPrimary)MonsterInformation.MonsterInformations[4212]=missingPrimary;
                if(hadMissingFallback)MonsterInformation.MonsterInformations[2]=missingFallback;
            }

            {
            // Recovery must own the session until the committed prize is also
            // applied to memory. Stop the real transaction on its vault row.
            var recoveringOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(recoveringOld);
            var recoveringNew=ReloadBall(recoveringOld);EntityVariableTable.Load(recoveringNew);
            Call(installType,null,"Install",recoveringOld);
            Query("INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,2002,2,false)","uid",(long)recoveringOld.Entity.UID);
            Exception projectionError=null,recoveryInstallError=null;
            bool recoveryWait=false,recoveryInstalledEarly=false;
            var projectionThread=new System.Threading.Thread(delegate(){try{BossRushSystem.RecoverPending(recoveringOld);}catch(Exception ex){projectionError=ex;}}){IsBackground=true};
            var recoveryInstall=new System.Threading.Thread(delegate(){try{Call(installType,null,"Install",recoveringNew);}catch(Exception ex){recoveryInstallError=ex;}}){IsBackground=true};
            using(var conn=DataHolder.MySqlConnection){conn.Open();using(var tx=conn.BeginTransaction()){
                int pid;
                using(var q=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",conn,tx))pid=Convert.ToInt32(q.ExecuteScalar());
                using(var q=new Npgsql.NpgsqlCommand("SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE",conn,tx)){
                    q.Parameters.AddWithValue("uid",(long)recoveringOld.Entity.UID);q.ExecuteScalar();
                }
                projectionThread.Start();
                recoveryWait=System.Threading.SpinWait.SpinUntil(delegate{return Scalar("SELECT count(*) FROM pg_stat_activity WHERE @uid=ANY(pg_blocking_pids(pid))",(uint)pid)>0;},5000);
                recoveryInstall.Start();recoveryInstalledEarly=recoveryInstall.Join(300);
                tx.Rollback();
            }}
            bool recoveryJoined=projectionThread.Join(10000) && recoveryInstall.Join(10000);
            verify(recoveryWait && recoveryJoined && projectionError==null && recoveryInstallError==null,
                "REVIEW-277: production recovery reaches a vault-row wait and both workers finish without exceptions");
            verify(!recoveryInstalledEarly && (uint)recoveringOld[BossRushSystem.ClaimKey]==2u,
                "REVIEW-277: replacement waits for recovery commit and live projection");
            EntityVariableTable.Load(recoveringNew);
            BossRushSystem.RecoverPending(recoveringNew);
            verify((uint)recoveringNew[BossRushSystem.ClaimKey]==2u
                && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2002",recoveringNew.Entity.UID)==1,
                "REVIEW-277: the installed successor recovers the same single prize");

            }
            // Fault after the real progress commit but before map insertion.
            // This represents a failed spawn; no durable evidence says it landed.
            var interruptedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(interruptedOwner);
            var interruptedArena=new Map(10996,1002,"fixture",false);
            interruptedArena.Floor=town.Floor;interruptedArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            interruptedArena.EntityUIDCounter=null;
            object interruptedFight=RushInstance(interruptedOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(interruptedFight,2201ul);
            interruptedFight.GetType().GetField("Arena").SetValue(interruptedFight,interruptedArena);
            interruptedFight.GetType().GetField("SpawnX").SetValue(interruptedFight,(ushort)408);
            interruptedFight.GetType().GetField("SpawnY").SetValue(interruptedFight,(ushort)330);
            interruptedOwner.Entity.MapID=interruptedArena.ID;TrackRush(interruptedFight);
            MonsterInformation interruptedTemplate;
            bool hadInterruptedTemplate=MonsterInformation.MonsterInformations.TryGetValue(4212,out interruptedTemplate);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Interrupted boss",Level=140,Hitpoints=6000000};
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",interruptedOwner,2201ul,2,2,1);
                bool spawnThrew=false;
                try{Call(typeof(BossRushSystem),null,"StartBoss",interruptedFight,3);}
                catch(NullReferenceException){spawnThrew=true;}
                verify(spawnThrew && (int)Call(typeof(BossRushSystem),null,"RemainingMobs",interruptedFight)==0
                    && (int)interruptedFight.GetType().GetField("Boss").GetValue(interruptedFight)==2
                    && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2201",interruptedOwner.Entity.UID)==3,
                    "REVIEW-280-FAULT: actual spawn exception follows durable boss-3 checkpoint with no spawned boss");
                var interruptedNext=ReloadBall(interruptedOwner);EntityVariableTable.Load(interruptedNext);
                Call(installType,null,"Install",interruptedNext);BossRushSystem.RecoverPending(interruptedNext);
                verify((uint)interruptedNext[BossRushSystem.ClaimKey]==0u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2201",interruptedNext.Entity.UID)==0,
                    "REVIEW-280-BLOCKER: a checkpoint without a spawned wave cannot award consolation on reconnect");
            } finally {
                UntrackRush(interruptedFight);
                if(hadInterruptedTemplate)MonsterInformation.MonsterInformations[4212]=interruptedTemplate;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            // The missed wave stays unresolved until a later tick inserts it once.
            var waveRetryOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(waveRetryOwner);
            var retryArena=new Map(10995,1002,"fixture",false);
            retryArena.Floor=town.Floor;retryArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            retryArena.EntityUIDCounter=null;
            object retryFight=RushInstance(waveRetryOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(retryFight,2202ul);
            retryFight.GetType().GetField("Arena").SetValue(retryFight,retryArena);
            retryFight.GetType().GetField("SpawnX").SetValue(retryFight,(ushort)408);
            retryFight.GetType().GetField("SpawnY").SetValue(retryFight,(ushort)330);
            waveRetryOwner.Entity.MapID=retryArena.ID;Kernel.GamePool[waveRetryOwner.Entity.UID]=waveRetryOwner;TrackRush(retryFight);
            MonsterInformation retryTemplate;bool hadRetryTemplate=MonsterInformation.MonsterInformations.TryGetValue(4212,out retryTemplate);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Retry boss",Level=140,Hitpoints=6000000};
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",waveRetryOwner,2202ul,2,2,1);
                bool retryThrew=false;
                try{Call(typeof(BossRushSystem),null,"StartBoss",retryFight,3);}
                catch(NullReferenceException){retryThrew=true;}
                verify(retryThrew && (int)retryFight.GetType().GetField("Boss").GetValue(retryFight)==2
                    && (int)retryFight.GetType().GetField("PendingWave").GetValue(retryFight)==3
                    && (int)Call(typeof(BossRushSystem),null,"RemainingMobs",retryFight)==0,
                    "REVIEW-282: spawn failure before insertion keeps the intent without a monster");
                retryArena.EntityUIDCounter=new Counter(610000);
                var collisionMonster=new Entity(EntityFlag.Monster,false);
                collisionMonster.UID=610001; // A new entity has zero HP.
                retryArena.Entities.Add(collisionMonster.UID,collisionMonster);
                Call(typeof(BossRushSystem),null,"TickInstance",retryFight,DateTime.Now);
                verify((int)retryFight.GetType().GetField("Boss").GetValue(retryFight)==2
                    && (int)retryFight.GetType().GetField("PendingWave").GetValue(retryFight)==3
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2202",waveRetryOwner.Entity.UID)==0,
                    "REVIEW-283: a silent map insertion collision cannot confirm an absent boss");
                Call(typeof(BossRushSystem),null,"TickInstance",retryFight,DateTime.Now);
                Call(typeof(BossRushSystem),null,"TickInstance",retryFight,DateTime.Now);
                verify((int)Call(typeof(BossRushSystem),null,"RemainingMobs",retryFight)==1
                    && (int)retryFight.GetType().GetField("Boss").GetValue(retryFight)==3
                    && (int)retryFight.GetType().GetField("PendingWave").GetValue(retryFight)==0
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2202",waveRetryOwner.Entity.UID)==1,
                    "REVIEW-282: in-process retry inserts the missed wave once and then confirms it");
            } finally {
                UntrackRush(retryFight);
                if(hadRetryTemplate)MonsterInformation.MonsterInformations[4212]=retryTemplate;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            // Boss 3 was confirmed. The next intent must not erase that consolation.
            var earnedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(earnedOwner);
            var earnedArena=new Map(10994,1002,"fixture",false);
            earnedArena.Floor=town.Floor;earnedArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            earnedArena.EntityUIDCounter=null;
            object earnedFight=RushInstance(earnedOwner,3,DateTime.Now.AddMinutes(5));SetRushRun(earnedFight,2203ul);
            earnedFight.GetType().GetField("Arena").SetValue(earnedFight,earnedArena);
            earnedFight.GetType().GetField("SpawnX").SetValue(earnedFight,(ushort)408);
            earnedFight.GetType().GetField("SpawnY").SetValue(earnedFight,(ushort)330);
            earnedOwner.Entity.MapID=earnedArena.ID;Kernel.GamePool[earnedOwner.Entity.UID]=earnedOwner;TrackRush(earnedFight);
            MonsterInformation earnedTemplate;bool hadEarnedTemplate=MonsterInformation.MonsterInformations.TryGetValue(7487,out earnedTemplate);
            MonsterInformation.MonsterInformations[7487]=new MonsterInformation{ID=7487,Mesh=100,Name="Next intent",Level=140,Hitpoints=9000000};
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",earnedOwner,2203ul,3,2,1);
                bool earnedThrew=false;
                try{Call(typeof(BossRushSystem),null,"StartBoss",earnedFight,4);}
                catch(NullReferenceException){earnedThrew=true;}
                verify(earnedThrew && (int)earnedFight.GetType().GetField("Boss").GetValue(earnedFight)==3
                    && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2203",earnedOwner.Entity.UID)==4
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2203",earnedOwner.Entity.UID)==0,
                    "REVIEW-282: a later unconfirmed wave keeps confirmed boss 3 on disk");
                var earnedNext=ReloadBall(earnedOwner);EntityVariableTable.Load(earnedNext);
                Call(installType,null,"Install",earnedNext);BossRushSystem.RecoverPending(earnedNext);
                BossRushSystem.RecoverPending(earnedNext);
                verify((uint)earnedNext[BossRushSystem.ClaimKey]==2u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2203",earnedNext.Entity.UID)==1,
                    "REVIEW-282: cold recovery pays the confirmed boss once when the next wave never spawned");
            } finally {
                UntrackRush(earnedFight);
                if(hadEarnedTemplate)MonsterInformation.MonsterInformations[7487]=earnedTemplate;
                else MonsterInformation.MonsterInformations.Remove(7487);
            }

            var spawnedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(spawnedOwner);
            var spawnedArena=new Map(10993,1002,"fixture",false);
            spawnedArena.Floor=town.Floor;spawnedArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            spawnedArena.EntityUIDCounter=new Counter(620000);
            object spawnedFight=RushInstance(spawnedOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(spawnedFight,2204ul);
            spawnedFight.GetType().GetField("Arena").SetValue(spawnedFight,spawnedArena);
            spawnedFight.GetType().GetField("SpawnX").SetValue(spawnedFight,(ushort)408);
            spawnedFight.GetType().GetField("SpawnY").SetValue(spawnedFight,(ushort)330);
            spawnedOwner.Entity.MapID=spawnedArena.ID;Kernel.GamePool[spawnedOwner.Entity.UID]=spawnedOwner;TrackRush(spawnedFight);
            MonsterInformation spawnedTemplate;bool hadSpawnedTemplate=MonsterInformation.MonsterInformations.TryGetValue(4212,out spawnedTemplate);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Earned boss",Level=140,Hitpoints=6000000};
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",spawnedOwner,2204ul,2,2,1);
                Call(typeof(BossRushSystem),null,"StartBoss",spawnedFight,3);
                verify((int)spawnedFight.GetType().GetField("Boss").GetValue(spawnedFight)==3
                    && (int)Call(typeof(BossRushSystem),null,"RemainingMobs",spawnedFight)==1
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2204",spawnedOwner.Entity.UID)==1,
                    "REVIEW-282: a completed boss-3 spawn confirms the wave before it can pay");
                BossRushSystem.OnDisconnect(spawnedOwner);
                verify((uint)spawnedOwner[BossRushSystem.ClaimKey]==2u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2204",spawnedOwner.Entity.UID)==1,
                    "REVIEW-282: leaving a confirmed boss-3 wave still stores one consolation");
            } finally {
                UntrackRush(spawnedFight);
                if(hadSpawnedTemplate)MonsterInformation.MonsterInformations[4212]=spawnedTemplate;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            var ambiguousOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(ambiguousOwner);
            var ambiguousArena=new Map(10992,1002,"fixture",false);
            ambiguousArena.Floor=town.Floor;ambiguousArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            ambiguousArena.EntityUIDCounter=new Counter(630000);
            object ambiguousFight=RushInstance(ambiguousOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(ambiguousFight,2205ul);
            ambiguousFight.GetType().GetField("Arena").SetValue(ambiguousFight,ambiguousArena);
            ambiguousFight.GetType().GetField("SpawnX").SetValue(ambiguousFight,(ushort)408);
            ambiguousFight.GetType().GetField("SpawnY").SetValue(ambiguousFight,(ushort)330);
            ambiguousOwner.Entity.MapID=ambiguousArena.ID;Kernel.GamePool[ambiguousOwner.Entity.UID]=ambiguousOwner;TrackRush(ambiguousFight);
            MonsterInformation ambiguousTemplate;bool hadAmbiguousTemplate=MonsterInformation.MonsterInformations.TryGetValue(4212,out ambiguousTemplate);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Ambiguous boss",Level=140,Hitpoints=6000000};
            var holdConfirm=typeof(BossRushSystem).GetField("HoldWaveConfirm",Hidden);
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",ambiguousOwner,2205ul,2,2,1);
                holdConfirm.SetValue(null,true);
                Call(typeof(BossRushSystem),null,"StartBoss",ambiguousFight,3);
                verify((int)Call(typeof(BossRushSystem),null,"RemainingMobs",ambiguousFight)==1
                    && (int)ambiguousFight.GetType().GetField("Boss").GetValue(ambiguousFight)==2
                    && (int)ambiguousFight.GetType().GetField("PendingWave").GetValue(ambiguousFight)==3
                    && Scalar("SELECT boss FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2205",ambiguousOwner.Entity.UID)==3
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2205",ambiguousOwner.Entity.UID)==0,
                    "REVIEW-282: insertion without confirmation leaves one monster and an unconfirmed boss 3");
                Call(typeof(BossRushSystem),null,"TickInstance",ambiguousFight,DateTime.Now);
                verify((int)Call(typeof(BossRushSystem),null,"RemainingMobs",ambiguousFight)==1
                    && (int)ambiguousFight.GetType().GetField("Boss").GetValue(ambiguousFight)==2
                    && !(bool)ambiguousFight.GetType().GetField("Won").GetValue(ambiguousFight)
                    && !(bool)ambiguousFight.GetType().GetField("Failed").GetValue(ambiguousFight),
                    "REVIEW-282: an inserted unconfirmed wave does not advance, duplicate, or finish");
                BossRushSystem.OnDeath(ambiguousOwner);
                verify((int)ambiguousFight.GetType().GetField("LivesLeft").GetValue(ambiguousFight)==1
                    && Scalar("SELECT lives FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2205",ambiguousOwner.Entity.UID)==1,
                    "REVIEW-283: a death during delayed wave confirmation is checkpointed instead of forgotten");
                BossRushSystem.OnDisconnect(ambiguousOwner);
                verify(object.Equals(ambiguousFight.GetType().GetField("PendingVictory").GetValue(ambiguousFight),false)
                    && !(bool)ambiguousFight.GetType().GetField("Failed").GetValue(ambiguousFight),
                    "REVIEW-283: leaving during delayed confirmation retains the terminal choice for retry");
                holdConfirm.SetValue(null,false);
                Call(typeof(BossRushSystem),null,"TickInstance",ambiguousFight,DateTime.Now);
                verify((int)Call(typeof(BossRushSystem),null,"RemainingMobs",ambiguousFight)==1
                    && (int)ambiguousFight.GetType().GetField("Boss").GetValue(ambiguousFight)==3
                    && (int)ambiguousFight.GetType().GetField("PendingWave").GetValue(ambiguousFight)==0
                    && !(bool)ambiguousFight.GetType().GetField("Won").GetValue(ambiguousFight)
                    && (bool)ambiguousFight.GetType().GetField("Failed").GetValue(ambiguousFight)
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2205",ambiguousOwner.Entity.UID)==1,
                    "REVIEW-282: confirming an inserted wave does not spawn a replacement boss");
                BossRushSystem.OnDisconnect(ambiguousOwner);
                verify((uint)ambiguousOwner[BossRushSystem.ClaimKey]==2u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2205",ambiguousOwner.Entity.UID)==1,
                    "REVIEW-282: the confirmed inserted wave keeps its consolation");
            } finally {
                holdConfirm.SetValue(null,false);
                UntrackRush(ambiguousFight);
                if(hadAmbiguousTemplate)MonsterInformation.MonsterInformations[4212]=ambiguousTemplate;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            var deadlineOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(deadlineOwner);
            var deadlineArena=new Map(10991,1002,"fixture",false);
            deadlineArena.Floor=town.Floor;deadlineArena.Zones=new System.Collections.Generic.List<Zoning.Zone>();
            deadlineArena.EntityUIDCounter=null;
            object deadlineFight=RushInstance(deadlineOwner,2,DateTime.Now.AddMinutes(5));SetRushRun(deadlineFight,2206ul);
            deadlineFight.GetType().GetField("Arena").SetValue(deadlineFight,deadlineArena);
            deadlineFight.GetType().GetField("SpawnX").SetValue(deadlineFight,(ushort)408);
            deadlineFight.GetType().GetField("SpawnY").SetValue(deadlineFight,(ushort)330);
            deadlineOwner.Entity.MapID=deadlineArena.ID;Kernel.GamePool[deadlineOwner.Entity.UID]=deadlineOwner;TrackRush(deadlineFight);
            MonsterInformation deadlineTemplate;bool hadDeadlineTemplate=MonsterInformation.MonsterInformations.TryGetValue(4212,out deadlineTemplate);
            MonsterInformation.MonsterInformations[4212]=new MonsterInformation{ID=4212,Mesh=100,Name="Deadline boss",Level=140,Hitpoints=6000000};
            try {
                Call(typeof(BossRushSystem),null,"SaveProgress",deadlineOwner,2206ul,2,2,1);
                bool deadlineThrew=false;
                try{Call(typeof(BossRushSystem),null,"StartBoss",deadlineFight,3);}
                catch(NullReferenceException){deadlineThrew=true;}
                deadlineFight.GetType().GetField("FightUntil").SetValue(deadlineFight,DateTime.Now.AddMinutes(-1));
                Call(typeof(BossRushSystem),null,"TickInstance",deadlineFight,DateTime.Now);
                verify(deadlineThrew && (bool)deadlineFight.GetType().GetField("Failed").GetValue(deadlineFight)
                    && !(bool)deadlineFight.GetType().GetField("Won").GetValue(deadlineFight)
                    && (int)deadlineFight.GetType().GetField("Boss").GetValue(deadlineFight)==2
                    && (uint)deadlineOwner[BossRushSystem.ClaimKey]==0u
                    && Scalar("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=2206",deadlineOwner.Entity.UID)==0
                    && Scalar("SELECT phase FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2206",deadlineOwner.Entity.UID)==2,
                    "REVIEW-282: a deadline during an unmaterialized wave ends at the last confirmed boss");
            } finally {
                UntrackRush(deadlineFight);
                if(hadDeadlineTemplate)MonsterInformation.MonsterInformations[4212]=deadlineTemplate;
                else MonsterInformation.MonsterInformations.Remove(4212);
            }

            var confirmReadback=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(confirmReadback);
            Call(typeof(BossRushSystem),null,"SaveProgress",confirmReadback,2301ul,2,2,1);
            Call(typeof(BossRushSystem),null,"SaveProgress",confirmReadback,2301ul,3,2,1);
            Query("CREATE FUNCTION review_283_confirm_suppressed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.run_id=2301 THEN NEW.spawned=false; END IF; RETURN NEW; END'");
            Query("CREATE TRIGGER review_283_confirm_suppressed BEFORE UPDATE ON revora_boss_rush_progress FOR EACH ROW EXECUTE FUNCTION review_283_confirm_suppressed()");
            try {
                verify(!(bool)Call(typeof(BossRushSystem),null,"ConfirmWave",confirmReadback,2301ul,3,2)
                    && Scalar("SELECT CASE WHEN spawned THEN 1 ELSE 0 END FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=2301",confirmReadback.Entity.UID)==0,
                    "REVIEW-283: confirmation requires reading back spawned true from the database");
            } finally {
                Query("DROP TRIGGER review_283_confirm_suppressed ON revora_boss_rush_progress; DROP FUNCTION review_283_confirm_suppressed()");
            }

            var generationFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var generationField=typeof(GameState).GetField("SessionGeneration",generationFlags);
            Action clearLoginHooks=delegate{
                installType.GetField("AfterEarlyLogin",Hidden).SetValue(null,null);
                installType.GetField("BeforeLoginRelease",Hidden).SetValue(null,null);
                installType.GetField("BeforeLateLogin",Hidden).SetValue(null,null);
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,null);
            };
            clearLoginHooks();
            var loginTimerPool=Revora.Thread.GenericThreadPool;
            if(loginTimerPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
            Pet.CreateTimerFactories();
            Revora.Game.Screen.CreateTimerFactories();
            Query(@"DO $$ DECLARE t text; BEGIN
              FOREACH t IN ARRAY ARRAY['quests','claimitems','detaineditems','subclasses','profs','enemy','apprentice','partners','friends','wayofheroes','chi'] LOOP
                IF to_regclass('public.'||t) IS NOT NULL AND to_regclass(t) IS NULL THEN
                  EXECUTE format('CREATE TABLE %I (LIKE public.%I INCLUDING ALL)', t, t);
                END IF;
              END LOOP; END $$");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorDepartures"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorStoneClaims"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorBlessingClaims"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.TradePartnerships"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Friendships"),null,"Initialize");
            var generationOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(generationOwner);
            uint generationUid=generationOwner.Entity.UID;
            generationOwner.Fake=false;generationOwner.Account.EntityID=generationUid;
            if(generationOwner.Socket!=null){generationOwner.Socket.Alive=false;generationOwner.Socket.IP="127.0.0.1";}
            Call(installType,null,"Replace",generationUid,generationOwner,null);
            long generation=(long)generationField.GetValue(generationOwner);
            verify(generation!=0 && (bool)Call(installType,null,"CompleteLogin",generationUid,generationOwner)
                && object.ReferenceEquals(Kernel.GamePool[generationUid],generationOwner),
                "REVIEW-285: publishing a session captures that attempt's generation");
            var generationNext=ReloadBall(generationOwner);EntityVariableTable.Load(generationNext);
            generationNext.Fake=false;generationNext.Account.EntityID=generationUid;generationNext.Entity.UID=generationUid;
            if(generationNext.Socket!=null){generationNext.Socket.Alive=false;generationNext.Socket.IP="127.0.0.2";}
            Call(installType,null,"Replace",generationUid,generationNext,(Action)delegate{generationOwner.Disconnect();});
            verify(object.ReferenceEquals(Kernel.GamePool[generationUid],generationNext)
                && !(bool)Call(installType,null,"CompleteLogin",generationUid,generationOwner)
                && (bool)Call(installType,null,"CompleteLogin",generationUid,generationNext),
                "REVIEW-285: a successor invalidates the previous login generation");
            Call(installType,null,"Retire",generationOwner);
            verify(object.ReferenceEquals(Kernel.GamePool[generationUid],generationNext),
                "REVIEW-285: retiring the previous session cannot remove its successor");
            var generationStranger=new GameState(null);
            generationStranger.Fake=false;generationStranger.Account=new AccountTable(null);generationStranger.Account.EntityID=generationUid;
            generationStranger.Entity=new Entity(EntityFlag.Player,false);generationStranger.Entity.UID=generationUid;
            bool generationClaimed=(bool)Call(installType,null,"TryClaim",generationUid,generationStranger);
            GameState generationSeen;bool generationSame=Kernel.GamePool.TryGetValue(generationUid,out generationSeen)&&object.ReferenceEquals(generationSeen,generationNext);
            verify(!generationClaimed && generationSame,
                "REVIEW-285: character creation does not claim an id owned by another session claimed="+generationClaimed+" same="+generationSame);
            var generationFake=new GameState(null);
            generationFake.Account=new AccountTable(null);generationFake.Account.EntityID=generationUid;
            generationFake.Entity=new Entity(EntityFlag.Player,false);generationFake.Entity.UID=generationUid;
            verify(!(bool)Call(installType,null,"TryInstallFake",generationUid,generationFake)
                && !(bool)Call(installType,null,"TryRemoveFake",generationUid,generationFake)
                && object.ReferenceEquals(Kernel.GamePool[generationUid],generationNext),
                "REVIEW-285: a fake actor cannot replace or remove a real session");

            var thrownOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(thrownOld);
            thrownOld.Fake=false;thrownOld.Account.EntityID=thrownOld.Entity.UID;
            if(thrownOld.Socket!=null)thrownOld.Socket.Alive=false;
            Call(installType,null,"Replace",thrownOld.Entity.UID,thrownOld,null);
            var thrownNew=ReloadBall(thrownOld);EntityVariableTable.Load(thrownNew);
            thrownNew.Fake=false;thrownNew.Entity.UID=thrownOld.Entity.UID;thrownNew.Account.EntityID=thrownOld.Entity.UID;
            if(thrownNew.Socket!=null)thrownNew.Socket.Alive=false;
            Exception thrownError=null;
            try{Call(installType,null,"Replace",thrownNew.Entity.UID,thrownNew,(Action)delegate{thrownOld.Disconnect();throw new InvalidOperationException("review");});}
            catch(Exception ex){thrownError=ex;}
            verify(thrownError==null && object.ReferenceEquals(Kernel.GamePool[thrownNew.Entity.UID],thrownNew)
                && !(bool)Call(installType,null,"CompleteLogin",thrownOld.Entity.UID,thrownOld),
                "REVIEW-285: a throwing disconnect callback still publishes the replacement");

            var partialOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(partialOwner);
            partialOwner.Fake=false;partialOwner.Account.EntityID=partialOwner.Entity.UID;
            if(partialOwner.Socket!=null){partialOwner.Socket.Alive=false;partialOwner.Socket.IP="127.0.0.3";}
            Call(installType,null,"Replace",partialOwner.Entity.UID,partialOwner,null);
            bool partialLate=false,partialReady=false;Exception partialError=null;
            var partialInventory=partialOwner.Inventory;
            installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){partialReady=c.Pet!=null && c.Screen!=null && c.Inventory!=null && !ReferenceEquals(partialInventory,c.Inventory);c.Entity.FullyLoaded=false;return true;});
            installType.GetField("BeforeLateLogin",Hidden).SetValue(null,(Action<GameState>)delegate{partialLate=true;});
            try{GeneralData.DoLogin(partialOwner);}catch(Exception ex){partialError=ex;}
            finally{clearLoginHooks();}
            GameState partialSeen;bool partialStill=Kernel.GamePool.TryGetValue(partialOwner.Entity.UID,out partialSeen)&&object.ReferenceEquals(partialSeen,partialOwner);
            verify(partialReady && partialError==null && !partialLate && !partialOwner.LoggedIn && !partialStill,
                "REVIEW-285: a partial load does not register or remain the published session"+(partialError==null?"":" "+partialError.ToString().Replace("\r"," ").Replace("\n"," | ")));

            var betweenOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(betweenOld);
            betweenOld.Fake=false;betweenOld.Account.EntityID=betweenOld.Entity.UID;
            if(betweenOld.Socket!=null){betweenOld.Socket.Alive=false;betweenOld.Socket.IP="127.0.0.4";}
            Call(installType,null,"Replace",betweenOld.Entity.UID,betweenOld,null);
            var betweenNew=ReloadBall(betweenOld);EntityVariableTable.Load(betweenNew);
            betweenNew.Fake=false;betweenNew.Entity.UID=betweenOld.Entity.UID;betweenNew.Account.EntityID=betweenOld.Entity.UID;
            if(betweenNew.Socket!=null){betweenNew.Socket.Alive=false;betweenNew.Socket.IP="127.0.0.5";}
            betweenOld.Entity.FullyLoaded=true;
            Call(installType,null,"Replace",betweenOld.Entity.UID,betweenOld,null);
            bool betweenLate=false;Exception betweenError=null;
            installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){if(c.Entity!=null)c.Entity.FullyLoaded=true;return true;});
            installType.GetField("AfterEarlyLogin",Hidden).SetValue(null,(Action<GameState>)delegate{
                Call(installType,null,"Replace",betweenNew.Entity.UID,betweenNew,(Action)delegate{betweenOld.Disconnect();});
            });
            installType.GetField("BeforeLateLogin",Hidden).SetValue(null,(Action<GameState>)delegate{betweenLate=true;});
            try{GeneralData.DoLogin(betweenOld);}catch(Exception ex){betweenError=ex;}
            finally{clearLoginHooks();}
            verify(betweenError==null && !betweenLate && !betweenOld.LoggedIn
                && object.ReferenceEquals(Kernel.GamePool[betweenNew.Entity.UID],betweenNew),
                "REVIEW-285: late login effects do not run after that attempt's generation is replaced"+(betweenError==null?"":" error="+betweenError.GetType().Name));

            var waitingOld=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(waitingOld);
            waitingOld.Fake=false;waitingOld.Account.EntityID=waitingOld.Entity.UID;
            if(waitingOld.Socket!=null){waitingOld.Socket.Alive=false;waitingOld.Socket.IP="127.0.0.6";}
            Call(installType,null,"Replace",waitingOld.Entity.UID,waitingOld,null);
            var waitingNew=ReloadBall(waitingOld);EntityVariableTable.Load(waitingNew);
            waitingNew.Fake=false;waitingNew.Entity.UID=waitingOld.Entity.UID;waitingNew.Account.EntityID=waitingOld.Entity.UID;
            if(waitingNew.Socket!=null){waitingNew.Socket.Alive=false;waitingNew.Socket.IP="127.0.0.7";}
            Call(installType,null,"Replace",waitingOld.Entity.UID,waitingOld,null);
            bool waitingEntered=false;Exception waitingLoginError=null,waitingReplaceError=null;
            var waitingRelease=new System.Threading.ManualResetEvent(false);
            waitingOld.Entity.FullyLoaded=true;
            installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){if(c.Entity!=null)c.Entity.FullyLoaded=true;return true;});
            installType.GetField("BeforeLoginRelease",Hidden).SetValue(null,(Action<GameState>)delegate{waitingEntered=true;waitingRelease.WaitOne();});
            var waitingLogin=new System.Threading.Thread(delegate(){try{GeneralData.DoLogin(waitingOld);}catch(Exception ex){waitingLoginError=ex;}}){IsBackground=true};
            var waitingReplace=new System.Threading.Thread(delegate(){try{Call(installType,null,"Replace",waitingNew.Entity.UID,waitingNew,(Action)delegate{waitingOld.Disconnect();});}catch(Exception ex){waitingReplaceError=ex;}}){IsBackground=true};
            try{
                waitingLogin.Start();
                bool waitingSaw=System.Threading.SpinWait.SpinUntil(delegate{return waitingEntered;},15000);
                waitingReplace.Start();
                bool waitingEarly=waitingReplace.Join(300);
                waitingRelease.Set();
                bool waitingJoined=waitingLogin.Join(20000)&&waitingReplace.Join(20000);
                string waitingDetail="";
                if(waitingLoginError!=null)waitingDetail+=" login="+waitingLoginError.GetType().Name+":"+waitingLoginError.Message.Replace("\r"," ").Replace("\n"," ");
                if(waitingReplaceError!=null)waitingDetail+=" replace="+waitingReplaceError.GetType().Name+":"+waitingReplaceError.Message.Replace("\r"," ").Replace("\n"," ");
                verify(waitingSaw && !waitingEarly && waitingJoined && waitingLoginError==null && waitingReplaceError==null
                    && object.ReferenceEquals(Kernel.GamePool[waitingNew.Entity.UID],waitingNew),
                    "REVIEW-285: replacement waits through the early login lease and then becomes the only session saw="+waitingSaw+" early="+waitingEarly+" joined="+waitingJoined+waitingDetail);
            } finally { waitingRelease.Set(); clearLoginHooks(); }

            var loadThrowOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(loadThrowOwner);
            loadThrowOwner.Fake=false;loadThrowOwner.Account.EntityID=loadThrowOwner.Entity.UID;
            if(loadThrowOwner.Socket!=null){loadThrowOwner.Socket.Alive=false;loadThrowOwner.Socket.IP="127.0.0.8";}
            Call(installType,null,"Replace",loadThrowOwner.Entity.UID,loadThrowOwner,null);
            bool loadThrowLate=false;
            installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate { throw new InvalidOperationException("review load failure"); });
            installType.GetField("BeforeLateLogin",Hidden).SetValue(null,(Action<GameState>)delegate { loadThrowLate=true; });
            try { GeneralData.DoLogin(loadThrowOwner); } catch(InvalidOperationException) { }
            finally { clearLoginHooks(); }
            GameState loadThrowSeen;
            verify(!loadThrowLate && !loadThrowOwner.LoggedIn
                && (!Kernel.GamePool.TryGetValue(loadThrowOwner.Entity.UID,out loadThrowSeen) || !ReferenceEquals(loadThrowSeen,loadThrowOwner)),
                "REVIEW-286: an exception during entity loading retires the failed login");

            var fakeFirst=new GameState(null);fakeFirst.Account=new AccountTable(null);
            fakeFirst.Entity=new Entity(EntityFlag.Player,false);fakeFirst.Entity.UID=1900286u;
            var fakeSecond=new GameState(null);fakeSecond.Account=new AccountTable(null);
            fakeSecond.Entity=new Entity(EntityFlag.Player,false);fakeSecond.Entity.UID=fakeFirst.Entity.UID;
            bool fakeInstalled=(bool)Call(installType,null,"TryInstallFake",fakeFirst.Entity.UID,fakeFirst);
            bool fakeCollision=(bool)Call(installType,null,"TryInstallFake",fakeFirst.Entity.UID,fakeSecond);
            verify(fakeInstalled && !fakeCollision && ReferenceEquals(Kernel.GamePool[fakeFirst.Entity.UID],fakeFirst),
                "REVIEW-286: a fake UID collision preserves the existing actor");
            Call(installType,null,"TryRemoveFake",fakeFirst.Entity.UID,fakeFirst);
            Call(installType,null,"TryRemoveFake",fakeSecond.Entity.UID,fakeSecond);

            var releaseOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(releaseOwner);
            releaseOwner.Fake=false;releaseOwner.Account.EntityID=releaseOwner.Entity.UID;
            if(releaseOwner.Socket!=null)releaseOwner.Socket.Alive=false;
            Call(installType,null,"Replace",releaseOwner.Entity.UID,releaseOwner,null);
            var releaseStarted=new System.Threading.ManualResetEvent(false);
            Exception releaseError=null;bool releaseEarly=false;
            var releaseThread=new System.Threading.Thread(delegate(){releaseStarted.Set();try{Call(typeof(GameState),releaseOwner,"ReleaseSessionRegistration",false);}catch(Exception ex){releaseError=ex;}}){IsBackground=true};
            Call(installType,null,"Enter",releaseOwner.Entity.UID);
            try {
                releaseThread.Start();
                verify(releaseStarted.WaitOne(5000),"REVIEW-286-SETUP: real shutdown registration release started while the login lease is held");
                releaseEarly=releaseThread.Join(1000);
                GameState releaseSeen;
                verify(!releaseEarly && Kernel.GamePool.TryGetValue(releaseOwner.Entity.UID,out releaseSeen) && ReferenceEquals(releaseSeen,releaseOwner),
                    "REVIEW-286-BLOCKER: shutdown cannot remove the session while login owns its lease");
            } finally {Call(installType,null,"Exit",releaseOwner.Entity.UID);}
            verify(releaseThread.Join(10000) && releaseError==null,"REVIEW-286-SETUP: shutdown release drains after the lease is released");

            var disconnectOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(disconnectOwner);
            disconnectOwner.Fake=false;disconnectOwner.Account.EntityID=disconnectOwner.Entity.UID;
            if(disconnectOwner.Socket!=null)disconnectOwner.Socket.Alive=false;
            Call(installType,null,"Replace",disconnectOwner.Entity.UID,disconnectOwner,null);
            var disconnectCallback=new System.Threading.ManualResetEvent(false);
            disconnectOwner.OnDisconnect=delegate { disconnectCallback.Set(); };
            Exception disconnectError=null;
            var disconnectThread=new System.Threading.Thread(delegate(){try{disconnectOwner.Disconnect(false);}catch(Exception ex){disconnectError=ex;}}){IsBackground=true};
            Call(installType,null,"Enter",disconnectOwner.Entity.UID);
            try {
                disconnectThread.Start();
                bool callbackEarly=disconnectCallback.WaitOne(1000);
                verify(!callbackEarly,"REVIEW-286-BLOCKER: actual Disconnect callbacks cannot run inside another thread's login lease");
            } finally {Call(installType,null,"Exit",disconnectOwner.Entity.UID);}
            verify(disconnectThread.Join(10000) && disconnectError==null && disconnectCallback.WaitOne(0),
                "REVIEW-286-SETUP: actual Disconnect completes and invokes its callback after lease release");

            var cleanupOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(cleanupOwner);
            cleanupOwner.Fake=false;cleanupOwner.Account.EntityID=cleanupOwner.Entity.UID;
            if(cleanupOwner.Socket!=null){cleanupOwner.Socket.Alive=false;cleanupOwner.Socket.Connector=new object();}
            Call(installType,null,"Replace",cleanupOwner.Entity.UID,cleanupOwner,null);
            cleanupOwner.OnDisconnect=delegate { throw new InvalidOperationException("inside disconnect"); };
            Exception cleanupError=null;
            try{cleanupOwner.Disconnect(false);}catch(Exception ex){cleanupError=ex;}
            GameState cleanupSeen;
            bool cleanupStarted=(bool)typeof(GameState).GetProperty("ShutdownStarted",Instance).GetValue(cleanupOwner,null);
            verify(cleanupError!=null && cleanupError.GetType()==typeof(InvalidOperationException) && cleanupStarted
                && cleanupOwner.Socket.Connector==null
                && (!Kernel.GamePool.TryGetValue(cleanupOwner.Entity.UID,out cleanupSeen) || !ReferenceEquals(cleanupSeen,cleanupOwner)),
                "REVIEW-288: a throw inside Disconnect still shuts the socket down and retires that session");

            var creationOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(creationOwner);
            creationOwner.Fake=false;creationOwner.Account.EntityID=creationOwner.Entity.UID;
            Call(installType,null,"Replace",creationOwner.Entity.UID,creationOwner,null);
            var creationStranger=new GameState(null);
            creationStranger.Fake=false;creationStranger.Account=new AccountTable(null);creationStranger.Account.EntityID=creationOwner.Entity.UID;
            creationStranger.Entity=new Entity(EntityFlag.Player,false);creationStranger.Entity.UID=creationOwner.Entity.UID;
            bool creationPersist=false;
            bool creationOwned=(bool)Call(installType,null,"TryOwnCreation",creationOwner.Entity.UID,creationStranger,(Func<bool>)delegate{creationPersist=true;return true;});
            var creationFree=new GameState(null);
            creationFree.Fake=false;creationFree.Account=new AccountTable(null);
            creationFree.Entity=new Entity(EntityFlag.Player,false);creationFree.Entity.UID=1900288u;creationFree.Account.EntityID=creationFree.Entity.UID;
            bool creationSaw=false;bool creationSaved=false;
            bool creationFreeOwned=(bool)Call(installType,null,"TryOwnCreation",creationFree.Entity.UID,creationFree,(Func<bool>)delegate{
                GameState creationCurrent;creationSaw=Kernel.GamePool.TryGetValue(creationFree.Entity.UID,out creationCurrent)&&ReferenceEquals(creationCurrent,creationFree);return true;});
            bool creationFailed=(bool)Call(installType,null,"TryOwnCreation",creationFree.Entity.UID,creationFree,(Func<bool>)delegate{creationSaved=true;return false;});
            GameState creationAfter;
            verify(!creationOwned && !creationPersist && creationFreeOwned && creationSaw && creationSaved && !creationFailed
                && (!Kernel.GamePool.TryGetValue(creationFree.Entity.UID,out creationAfter) || !ReferenceEquals(creationAfter,creationFree))
                && ReferenceEquals(Kernel.GamePool[creationOwner.Entity.UID],creationOwner),
                "REVIEW-288: creation persists only while that attempt owns the id and a failure removes it");

            var lateOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(lateOwner);
            lateOwner.Fake=false;lateOwner.Account.EntityID=lateOwner.Entity.UID;
            if(lateOwner.Socket!=null){lateOwner.Socket.Alive=false;lateOwner.Socket.IP="127.0.0.9";}
            Call(installType,null,"Replace",lateOwner.Entity.UID,lateOwner,null);
            var lateNext=ReloadBall(lateOwner);EntityVariableTable.Load(lateNext);
            lateNext.Fake=false;lateNext.Entity.UID=lateOwner.Entity.UID;lateNext.Account.EntityID=lateOwner.Entity.UID;
            if(lateNext.Socket!=null){lateNext.Socket.Alive=false;lateNext.Socket.IP="127.0.0.10";}
            Call(installType,null,"Replace",lateOwner.Entity.UID,lateOwner,null);
            bool lateEntered=false;Exception lateLoginError=null,lateReplaceError=null;
            var lateRelease=new System.Threading.ManualResetEvent(false);
            lateOwner.Entity.FullyLoaded=true;
            installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){if(c.Entity!=null)c.Entity.FullyLoaded=true;return true;});
            installType.GetField("BeforeLateLogin",Hidden).SetValue(null,(Action<GameState>)delegate{lateEntered=true;lateRelease.WaitOne();});
            var lateLogin=new System.Threading.Thread(delegate(){try{GeneralData.DoLogin(lateOwner);}catch(Exception ex){lateLoginError=ex;}}){IsBackground=true};
            var lateReplace=new System.Threading.Thread(delegate(){try{Call(installType,null,"Replace",lateNext.Entity.UID,lateNext,(Action)delegate{lateOwner.Disconnect();});}catch(Exception ex){lateReplaceError=ex;}}){IsBackground=true};
            try{
                lateLogin.Start();
                bool lateSaw=System.Threading.SpinWait.SpinUntil(delegate{return lateEntered;},15000);
                lateReplace.Start();
                bool lateEarly=lateReplace.Join(300);
                lateRelease.Set();
                bool lateJoined=lateLogin.Join(20000)&&lateReplace.Join(20000);
                string lateDetail="";
                if(lateLoginError!=null)lateDetail+=" login="+lateLoginError.GetType().Name+":"+lateLoginError.Message.Replace("\r"," ").Replace("\n"," ");
                if(lateReplaceError!=null)lateDetail+=" replace="+lateReplaceError.GetType().Name+":"+lateReplaceError.Message.Replace("\r"," ").Replace("\n"," ");
                verify(lateSaw && !lateEarly && lateJoined && lateLoginError==null && lateReplaceError==null
                    && object.ReferenceEquals(Kernel.GamePool[lateNext.Entity.UID],lateNext),
                    "REVIEW-288: replacement waits through the late login lease saw="+lateSaw+" early="+lateEarly+" joined="+lateJoined+lateDetail);
            } finally { lateRelease.Set(); clearLoginHooks(); }

            int savedCap=Server.PlayerCap;Server.PlayerCap=int.MaxValue;
            Revora.Thread savedWorld=Server.Thread;
            var activityType=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
            var leaseClass=activityType.GetField("LeaseClassId",Hidden);
            var leaseObject=activityType.GetField("LeaseObjectId",Hidden);
            int savedLeaseClass=(int)leaseClass.GetValue(null);
            int savedLeaseObject=(int)leaseObject.GetValue(null);
            try{Call(activityType,null,"Relinquish");}catch(Exception){}
            leaseObject.SetValue(null,288);
            var ordinary=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(ordinary);
            ordinary.Fake=false;ordinary.Account.EntityID=ordinary.Entity.UID;ordinary.Account.Username="login"+ordinary.Entity.UID;
            ordinary.Account.exists=true;ordinary.Entity.FullyLoaded=true;
            typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(ordinary.Entity,true);
            if(ordinary.Socket!=null){ordinary.Socket.Alive=false;ordinary.Socket.IP="127.0.0.11";}
            if(ordinary.Entity.MyAchievement==null)ordinary.Entity.MyAchievement=new Achievement(ordinary.Entity);
            Call(installType,null,"Replace",ordinary.Entity.UID,ordinary,null);
            Exception ordinaryError=null;
            bool ordinaryRegistered=false;
            var ordinaryTrace=new System.IO.StringWriter();
            var ordinaryOut=System.Console.Out;
            try{
                System.Console.SetOut(ordinaryTrace);
                Call(activityType,null,"Initialize");
                Query("CREATE TABLE IF NOT EXISTS accounts(username varchar(25) PRIMARY KEY,password text,ip text,entityid bigint,lastcheck bigint,state integer,email text,idreferido integer)");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES(@name,'',@uid,2)","name",ordinary.Account.Username,"uid",(long)ordinary.Entity.UID);
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES(lower(@name),clock_timestamp(),clock_timestamp(),1)","name",ordinary.Account.Username);
                Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS online integer NOT NULL DEFAULT 0");
                Action<GameState,int> idle=delegate{};
                var world=(Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
                world.Buffers=new System.Threading.Generic.TimerRule<GameState>(idle,1000);
                world.Characters=new System.Threading.Generic.TimerRule<GameState>(idle,1000);
                world.AutoAttack=new System.Threading.Generic.TimerRule<GameState>(idle,1000);
                world.Companions=new System.Threading.Generic.TimerRule<GameState>(idle,1000);
                world.Prayer=new System.Threading.Generic.TimerRule<GameState>(idle,1000);
                Server.Thread=world;
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){if(c.Entity!=null)c.Entity.FullyLoaded=true;return true;});
                try{GeneralData.DoLogin(ordinary);}catch(Exception ex){ordinaryError=ex;}
                ordinaryRegistered=ordinary.TimerSubscriptions!=null;
                long openSessions=Scalar("SELECT count(*) FROM revora_account_sessions WHERE character_uid=@uid AND closed_at IS NULL",ordinary.Entity.UID);
                long onlineFlag=Scalar("SELECT COALESCE(online,0) FROM entities WHERE uid=@uid",ordinary.Entity.UID);
                clearLoginHooks();
                if(ordinaryRegistered&&Server.Thread!=null)Server.Thread.UnRegister(ordinary);
                System.Console.SetOut(ordinaryOut);
                string ordinaryDetail=" logged="+ordinary.LoggedIn+" action="+ordinary.Action+" reg="+ordinaryRegistered+" sessions="+openSessions+" online="+onlineFlag;
                if(ordinaryError!=null)ordinaryDetail+=" "+ordinaryError.GetType().Name+":"+ordinaryError.Message.Replace("\r"," ").Replace("\n"," ");
                if(ordinaryError!=null || !ordinary.LoggedIn || !ordinaryRegistered || openSessions!=1 || onlineFlag!=1){
                    string ordinaryText=ordinaryTrace.ToString().Replace("\r"," ").Replace("\n"," | ");
                    if(ordinaryText.Length>400)ordinaryText=ordinaryText.Substring(ordinaryText.Length-400);
                    ordinaryDetail+=" trace="+ordinaryText;
                }
                verify(ordinaryError==null && ordinary.LoggedIn && ordinary.Action==2 && ordinaryRegistered
                    && openSessions==1 && onlineFlag==1 && ReferenceEquals(Kernel.GamePool[ordinary.Entity.UID],ordinary),
                    "REVIEW-288: ordinary login reaches presence, registration, online status and recovery"+ordinaryDetail);
            } catch (Exception ex) {
                try{System.Console.SetOut(ordinaryOut);}catch(Exception){}
                verify(false,"REVIEW-288: ordinary login reaches presence, registration, online status and recovery "+ex.GetType().Name+":"+ex.Message.Replace("\r"," ").Replace("\n"," "));
            } finally {
                try{System.Console.SetOut(ordinaryOut);}catch(Exception){}
                clearLoginHooks();
                Server.PlayerCap=savedCap;Server.Thread=savedWorld;
                try{Call(activityType,null,"Relinquish");}catch(Exception){}
                leaseClass.SetValue(null,savedLeaseClass);
                leaseObject.SetValue(null,savedLeaseObject);
            }

            var resumedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(resumedOwner);
            resumedOwner.Fake=false;resumedOwner.Account.EntityID=resumedOwner.Entity.UID;
            resumedOwner.Socket.Alive=false;resumedOwner.Socket.Connector=new object();
            Call(installType,null,"Replace",resumedOwner.Entity.UID,resumedOwner,null);
            var resumeStarted=new System.Threading.ManualResetEvent(false);
            Exception resumeError=null;
            var resumeThread=new System.Threading.Thread(delegate(){resumeStarted.Set();try{Call(typeof(GameState),resumedOwner,"ResumeArenaShutdown");}catch(Exception ex){resumeError=ex;}}){IsBackground=true};
            Call(installType,null,"Enter",resumedOwner.Entity.UID);
            try {
                resumeThread.Start();
                verify(resumeStarted.WaitOne(5000),"REVIEW-290-SETUP: arena shutdown resumption started");
                bool resumedEarly=System.Threading.SpinWait.SpinUntil(delegate{return (bool)typeof(GameState).GetProperty("ShutdownStarted",Instance).GetValue(resumedOwner,null);},1000);
                verify(!resumedEarly && resumedOwner.Socket.Connector!=null,
                    "REVIEW-290: arena shutdown resumption cannot begin while another thread owns the login lease");
            } finally {Call(installType,null,"Exit",resumedOwner.Entity.UID);}
            verify(resumeThread.Join(10000) && resumeError==null,"REVIEW-290-SETUP: arena shutdown resumption drains");

            var closedCreator=new GameState(null);
            closedCreator.Fake=false;closedCreator.Account=new AccountTable(null);
            closedCreator.Entity=new Entity(EntityFlag.Player,false);
            // A socket can close before character creation assigns any character UID.
            closedCreator.Disconnect(false);
            closedCreator.Entity.UID=1900290u;closedCreator.Account.EntityID=closedCreator.Entity.UID;
            bool closedPersist=false;
            bool closedAccepted=(bool)Call(installType,null,"TryOwnCreation",closedCreator.Entity.UID,closedCreator,(Func<bool>)delegate{closedPersist=true;return true;});
            verify(!closedAccepted && !closedPersist,"REVIEW-290: a disconnected creation attempt cannot publish or persist");
            Call(installType,null,"Retire",closedCreator);

            var retiredCreator=new GameState(null);
            retiredCreator.Fake=false;retiredCreator.Account=new AccountTable(null);
            retiredCreator.Entity=new Entity(EntityFlag.Player,false);
            retiredCreator.Entity.UID=1900291u;retiredCreator.Account.EntityID=retiredCreator.Entity.UID;
            Call(installType,null,"Replace",retiredCreator.Entity.UID,retiredCreator,null);
            Call(installType,null,"Retire",retiredCreator);
            bool retiredPersist=false;
            bool retiredAccepted=(bool)Call(installType,null,"TryOwnCreation",retiredCreator.Entity.UID,retiredCreator,(Func<bool>)delegate{retiredPersist=true;return true;});
            verify(!retiredAccepted && !retiredPersist,"REVIEW-290: a retired creation generation cannot publish or persist");
            Call(installType,null,"Retire",retiredCreator);

            var staleShutdown=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(staleShutdown);
            staleShutdown.Fake=false;staleShutdown.Account.EntityID=staleShutdown.Entity.UID;
            staleShutdown.Socket.Alive=false;staleShutdown.Socket.Connector=new object();
            Call(installType,null,"Replace",staleShutdown.Entity.UID,staleShutdown,null);
            var shutdownSuccessor=ReloadBall(staleShutdown);EntityVariableTable.Load(shutdownSuccessor);
            shutdownSuccessor.Fake=false;shutdownSuccessor.Account.EntityID=shutdownSuccessor.Entity.UID;
            Call(installType,null,"Replace",shutdownSuccessor.Entity.UID,shutdownSuccessor,null);
            Query("UPDATE entities SET online=1 WHERE uid=@uid","uid",(long)shutdownSuccessor.Entity.UID);
            Call(typeof(GameState),staleShutdown,"ResumeArenaShutdown");
            verify(Scalar("SELECT online FROM entities WHERE uid=@uid",shutdownSuccessor.Entity.UID)==1
                && ReferenceEquals(Kernel.GamePool[shutdownSuccessor.Entity.UID],shutdownSuccessor),
                "REVIEW-290-BLOCKER: a stale shutdown must not mark the current successor offline");

            var retainedOwner=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(retainedOwner);
            retainedOwner.Fake=false;retainedOwner.Account.EntityID=retainedOwner.Entity.UID;
            retainedOwner.Socket.Alive=false;retainedOwner.Socket.Connector=new object();
            Call(installType,null,"Replace",retainedOwner.Entity.UID,retainedOwner,null);
            var retentionType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDisconnectRetention");
            var retainedManager=Activator.CreateInstance(retentionType,Instance,null,new object[]{1,null},null);
            var retainedEntryType=retentionType.GetNestedType("Entry",System.Reflection.BindingFlags.NonPublic);
            var retainedEntry=Activator.CreateInstance(retainedEntryType,true);
            retainedEntryType.GetField("Preparing",Instance).SetValue(retainedEntry,true);
            var retainedPlayers=(System.Collections.IDictionary)retentionType.GetField("players",Instance).GetValue(retainedManager);
            retainedPlayers.Add(retainedOwner,retainedEntry);
            typeof(GameState).GetField("ArenaShutdownRetention",Instance).SetValue(retainedOwner,retainedManager);
            retainedOwner.Disconnect(false);
            GameState retainedCurrent;
            verify(!(bool)typeof(GameState).GetProperty("ShutdownStarted",Instance).GetValue(retainedOwner,null)
                && Kernel.GamePool.TryGetValue(retainedOwner.Entity.UID,out retainedCurrent) && ReferenceEquals(retainedCurrent,retainedOwner)
                && ReferenceEquals(Kernel.DisconnectPool[retainedOwner.Entity.UID],retainedOwner),
                "REVIEW-290-BLOCKER: actual Disconnect preserves the canonical actor while Arena retention defers teardown");
            var retentionSuccessor=new GameState(null);
            retentionSuccessor.Account=new AccountTable(null);retentionSuccessor.Account.EntityID=retainedOwner.Entity.UID;
            retentionSuccessor.Entity=new Entity(EntityFlag.Player,false);retentionSuccessor.Entity.UID=retainedOwner.Entity.UID;
            Call(installType,null,"Replace",retainedOwner.Entity.UID,retentionSuccessor,(Action)delegate{retainedOwner.Disconnect(false);});
            verify(ReferenceEquals(Kernel.GamePool[retainedOwner.Entity.UID],retainedOwner),
                "REVIEW-290: replacement cannot evict an actor whose shutdown remains retained");
            verify(!(bool)Call(installType,null,"CompleteLogin",retainedOwner.Entity.UID,retainedOwner),
                "REVIEW-290: an actor retained for teardown cannot resume login effects");
            typeof(GameState).GetField("ArenaShutdownRetention",Instance).SetValue(retainedOwner,null);
            Call(typeof(GameState),retainedOwner,"ResumeArenaShutdown");
            verify((bool)typeof(GameState).GetProperty("ShutdownStarted",Instance).GetValue(retainedOwner,null)
                && !Kernel.GamePool.ContainsKey(retainedOwner.Entity.UID) && !Kernel.DisconnectPool.ContainsKey(retainedOwner.Entity.UID),
                "REVIEW-290: retained shutdown drains after its retention releases");

            var failingFake=new GameState(null);failingFake.Fake=true;
            bool fakeUnpublished=false;Exception fakeLoadError=null;
            try { Call(typeof(GameState),failingFake,"RunFakeLoad",1900292u,(Action)delegate {
                fakeUnpublished=!Kernel.GamePool.ContainsKey(1900292u);
                throw new InvalidOperationException("synthetic load failure");
            }); } catch(Exception ex) { fakeLoadError=ex; }
            verify(fakeUnpublished && fakeLoadError!=null && !Kernel.GamePool.ContainsKey(1900292u),
                "REVIEW-290: fake loading stays unpublished and failed initialization cannot strand a pool entry");
            var missingFake=new GameState(null);missingFake.Fake=true;
            missingFake.FakeLoad(1900293u);
            verify(!Kernel.GamePool.ContainsKey(1900293u) && !missingFake.FakeLoaded,
                "REVIEW-290: actual fake loading of a missing character leaves no actor or registration");

            var bornCounter=Server.EntityUID;
            if(bornCounter==null)Server.EntityUID=new Counter(1901000);
            var bornHook=typeof(Entity).Assembly.GetType("Revora.Database.CharacterCreation");
            var bornGate=bornHook.GetField("BeforeCreateCommit",Hidden);
            int bornCap=Server.PlayerCap;Server.PlayerCap=int.MaxValue;
            var bornWorld=Server.Thread;
            leaseObject.SetValue(null,295);
            try{
                Call(activityType,null,"Initialize");
                Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS owner text, ADD COLUMN IF NOT EXISTS class integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS level integer NOT NULL DEFAULT 1, ADD COLUMN IF NOT EXISTS hitpoints integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS mana integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS body integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS face integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS hairstyle integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS strength integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS warehousepw text NOT NULL DEFAULT '', ADD COLUMN IF NOT EXISTS agility integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS vitality integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS spirit integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS windwalker integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS onlinepoints integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS conquerpoints bigint NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS claimedteampk integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS claimedsteampk integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS firstcredit integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS claimedelitepk integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS cursedtime integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS swordsoul integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS bansheespirit integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS claimedexp integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS vipleveldays integer NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS viplevel integer NOT NULL DEFAULT 0");
                Query("CREATE TABLE IF NOT EXISTS configuration(server text PRIMARY KEY, entityid bigint NOT NULL DEFAULT 0)");
                Query("INSERT INTO configuration(server,entityid) VALUES('Revora',1901000) ON CONFLICT(server) DO NOTHING");
                Query("CREATE TABLE IF NOT EXISTS accounts(username varchar(25) PRIMARY KEY,password text,ip text,entityid bigint,lastcheck bigint,state integer,email text,idreferido integer)");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born293','',0,2)");
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES('born293',clock_timestamp(),clock_timestamp(),1)");
                var born=new GameState(null);born.Fake=false;born.Account=new AccountTable("born293");
                if(born.Socket!=null)born.Socket.Alive=false;
                var bornPacket=new EnitityCreate();bornPacket.Name="HeroBorn293";bornPacket.Body=1003;bornPacket.Class=2;
                string bornMessage="";
                bool bornCreated=EntityTable.CreateEntity(bornPacket,born,ref bornMessage);
                long bornRows=Scalar("SELECT count(*) FROM entities WHERE lower(name)=lower('HeroBorn293') AND (@uid>=0)",(uint)born.Entity.UID);
                long bornPoints=Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",born.Entity.UID);
                long bornAccount=Scalar("SELECT entityid FROM accounts WHERE username='born293' AND (@uid>=0)",born.Entity.UID);
                long bornReceipt=Scalar("SELECT count(*) FROM revora_character_creation WHERE account_name='born293' AND character_uid=@uid AND NOT starter_issued",born.Entity.UID);
                long bornCounterRow=Scalar("SELECT entityid FROM configuration WHERE server='Revora' AND (@uid>=0)",born.Entity.UID);
                verify(bornCreated && bornMessage=="ANSWER_OK" && bornRows==1 && bornPoints==10000 && bornAccount==born.Entity.UID && bornReceipt==1 && bornCounterRow>=born.Entity.UID,
                    "REVIEW-293: ordinary creation commits the character, account, counter and gift receipt together created="+bornCreated+" msg="+bornMessage+" rows="+bornRows+" cp="+bornPoints+" account="+bornAccount+" receipt="+bornReceipt);
                uint bornUid=born.Entity.UID;
                verify(born.Entity.MapID==1002 && born.Entity.X==327 && born.Entity.Y==303
                    && Scalar("SELECT count(*) FROM entities WHERE uid=@uid AND mapid=1002 AND x=327 AND y=303",bornUid)==1
                    && Scalar("SELECT count(*) FROM revora_character_creation WHERE character_uid=@uid AND revora_starter_pending",bornUid)==1,
                    "Revora starter: ordinary creation saves Twin City spawn and pending custom pack atomically");
                string bornAgainMessage="";
                bool bornAgain=EntityTable.CreateEntity(bornPacket,born,ref bornAgainMessage);
                verify(!bornAgain && Scalar("SELECT count(*) FROM entities WHERE lower(owner)=lower('born293') AND (@uid>=0)",bornUid)==1,
                    "REVIEW-293: a second creation for the same account does not insert another character");
                object bornRetry=Call(bornHook,null,"Commit",born,born.Entity.Name,bornUid,10000u);
                verify(bornRetry.ToString()=="Saved" && Scalar("SELECT count(*) FROM entities WHERE uid=@uid",bornUid)==1,
                    "REVIEW-293: retrying the committed character id keeps that one row");
                object changedRetry=Call(bornHook,null,"Commit",born,"Changed295",bornUid,10000u);
                verify(changedRetry.ToString()!="Saved",
                    "REVIEW-295: a committed creation retry revalidates the durable character name");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born295','',0,2)");
                var noCounter=new GameState(null);noCounter.Fake=false;noCounter.Account=new AccountTable("born295");
                noCounter.Entity=new Entity(EntityFlag.Player,false);noCounter.Entity.UID=1901295u;noCounter.Entity.Class=10;
                object noCounterResult=null;
                Query("UPDATE configuration SET server='Missing295' WHERE server='Revora'");
                try { noCounterResult=Call(bornHook,null,"Commit",noCounter,"NoCounter295",1901295u,10000u); }
                finally { Query("UPDATE configuration SET server='Revora' WHERE server='Missing295'"); }
                verify(noCounterResult.ToString()!="Saved" && Scalar("SELECT count(*) FROM entities WHERE uid=@uid",1901295u)==0
                    && Scalar("SELECT COALESCE(entityid,0) FROM accounts WHERE username='born295' AND (@uid>=0)",1u)==0,
                    "REVIEW-295: a missing server counter rolls back the character and account pointer");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born293b','',0,2)");
                var bornRival=new GameState(null);bornRival.Fake=false;bornRival.Account=new AccountTable("born293b");
                var bornRivalPacket=new EnitityCreate();bornRivalPacket.Name="HeroBorn293";bornRivalPacket.Body=1003;bornRivalPacket.Class=2;
                string bornRivalMessage="";
                bool bornRivalCreated=EntityTable.CreateEntity(bornRivalPacket,bornRival,ref bornRivalMessage);
                verify(!bornRivalCreated && bornRivalMessage=="The chosen name is already in use." && Scalar("SELECT count(*) FROM entities WHERE lower(name)=lower('HeroBorn293') AND (@uid>=0)",bornUid)==1,
                    "REVIEW-293: a conflicting name does not create a second character msg="+bornRivalMessage);
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born293c','',0,2)");
                bornGate.SetValue(null,(Action<Npgsql.NpgsqlConnection,Npgsql.NpgsqlTransaction>)delegate{throw new InvalidOperationException("create boundary");});
                var bornBurst=new GameState(null);bornBurst.Fake=false;bornBurst.Account=new AccountTable("born293c");
                var bornBurstPacket=new EnitityCreate();bornBurstPacket.Name="HeroBurst293";bornBurstPacket.Body=1003;bornBurstPacket.Class=2;
                string bornBurstMessage="";
                bool bornBurstCreated=EntityTable.CreateEntity(bornBurstPacket,bornBurst,ref bornBurstMessage);
                verify(!bornBurstCreated && Scalar("SELECT count(*) FROM entities WHERE lower(owner)=lower('born293c') AND (@uid>=0)",1u)==0
                    && Scalar("SELECT count(*) FROM revora_character_creation WHERE account_name='born293c' AND (@uid>=0)",1u)==0
                    && Scalar("SELECT COALESCE(entityid,0) FROM accounts WHERE username='born293c' AND (@uid>=0)",1u)==0,
                    "REVIEW-293: a failure before commit leaves no character, receipt or account pointer");
                bornGate.SetValue(null,null);
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born293d','',0,2)");
                var bornLeft=new GameState(null);bornLeft.Fake=false;bornLeft.Account=new AccountTable("born293d");
                typeof(GameState).GetField("disconnectOnce",Instance).SetValue(bornLeft,1);
                var bornLeftPacket=new EnitityCreate();bornLeftPacket.Name="HeroLeft293";bornLeftPacket.Body=1003;bornLeftPacket.Class=2;
                string bornLeftMessage="";
                bool bornLeftCreated=EntityTable.CreateEntity(bornLeftPacket,bornLeft,ref bornLeftMessage);
                verify(!bornLeftCreated && Scalar("SELECT count(*) FROM entities WHERE lower(owner)=lower('born293d') AND (@uid>=0)",1u)==0,
                    "REVIEW-293: a disconnected creation does not publish a character");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born293e','',0,2)");
                var bornOne=new GameState(null);bornOne.Fake=false;bornOne.Account=new AccountTable("born293e");
                bornOne.Entity=new Entity(EntityFlag.Player,false);bornOne.Entity.Name="HeroOne293";bornOne.Entity.UID=1901201u;bornOne.Entity.Class=10;bornOne.Entity.Hitpoints=100;bornOne.Entity.Mana=20;bornOne.Entity.Body=1003;
                var bornTwo=new GameState(null);bornTwo.Fake=false;bornTwo.Account=new AccountTable("born293e");
                bornTwo.Entity=new Entity(EntityFlag.Player,false);bornTwo.Entity.Name="HeroTwo293";bornTwo.Entity.UID=1901202u;bornTwo.Entity.Class=10;bornTwo.Entity.Hitpoints=100;bornTwo.Entity.Mana=20;bornTwo.Entity.Body=1003;
                int bornWins=0;
                Exception bornRaceError=null;
                var bornRaceA=new System.Threading.Thread(delegate(){try{if((bool)Call(installType,null,"TryOwnCreation",bornOne.Entity.UID,bornOne,(Func<bool>)delegate{return Call(bornHook,null,"Commit",bornOne,"HeroOne293",bornOne.Entity.UID,10000u).ToString()=="Saved";}))System.Threading.Interlocked.Increment(ref bornWins);}catch(Exception ex){bornRaceError=ex;}});
                var bornRaceB=new System.Threading.Thread(delegate(){try{if((bool)Call(installType,null,"TryOwnCreation",bornTwo.Entity.UID,bornTwo,(Func<bool>)delegate{return Call(bornHook,null,"Commit",bornTwo,"HeroTwo293",bornTwo.Entity.UID,10000u).ToString()=="Saved";}))System.Threading.Interlocked.Increment(ref bornWins);}catch(Exception ex){bornRaceError=ex;}});
                bornRaceA.IsBackground=true;bornRaceB.IsBackground=true;bornRaceA.Start();bornRaceB.Start();
                bool bornRaced=bornRaceA.Join(20000)&&bornRaceB.Join(20000);
                long bornRaceRows=Scalar("SELECT count(*) FROM entities WHERE lower(owner)=lower('born293e') AND (@uid>=0)",1u);
                long bornRaceAccount=Scalar("SELECT entityid FROM accounts WHERE username='born293e' AND (@uid>=0)",1u);
                verify(bornRaced && bornRaceError==null && bornWins==1 && bornRaceRows==1 && (bornRaceAccount==1901201 || bornRaceAccount==1901202),
                    "REVIEW-293: concurrent creation of one account keeps a single character wins="+bornWins+" rows="+bornRaceRows+" account="+bornRaceAccount+(bornRaceError==null?"":" "+bornRaceError.GetType().Name));
                born.JustCreated=true;
                if(born.Socket!=null)born.Socket.IP="127.0.0.21";
                typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(born.Entity,true);
                Action<GameState,int> bornIdle=delegate{};
                var bornThread=(Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
                bornThread.Buffers=new System.Threading.Generic.TimerRule<GameState>(bornIdle,1000);
                bornThread.Characters=new System.Threading.Generic.TimerRule<GameState>(bornIdle,1000);
                bornThread.AutoAttack=new System.Threading.Generic.TimerRule<GameState>(bornIdle,1000);
                bornThread.Companions=new System.Threading.Generic.TimerRule<GameState>(bornIdle,1000);
                bornThread.Prayer=new System.Threading.Generic.TimerRule<GameState>(bornIdle,1000);
                Server.Thread=bornThread;
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){if(c.Entity!=null)c.Entity.FullyLoaded=true;return true;});
                var packsBefore=bornHook.GetField("BeforePacksCommit",Hidden);
                packsBefore.SetValue(null,(Action<Npgsql.NpgsqlConnection,Npgsql.NpgsqlTransaction>)delegate{throw new InvalidOperationException("pack boundary");});
                try {
                    GeneralData.DoLogin(born);
                    verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",bornUid)==0
                        && Scalar("SELECT CASE WHEN packs_issued OR starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",bornUid)==0,
                        "REVIEW-295: legacy retirement failure keeps the initialization receipt retryable");
                } finally { packsBefore.SetValue(null,null); }
                Exception bornLoginError=null;
                try{GeneralData.DoLogin(born);}catch(Exception ex){bornLoginError=ex;}
                long bornPacks=Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",bornUid);
                bool bornIssued=Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",bornUid)==1;
                born.JustCreated=false;
                try{GeneralData.DoLogin(born);}catch(Exception){}
                long bornPacksAgain=Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",bornUid);
                verify(Scalar("SELECT count(*) FROM revora_character_creation WHERE character_uid=@uid AND revora_starter_pending",bornUid)==1,
                    "Revora starter entitlement survives initialization and repeated login without claiming undefined rewards");
                verify(bornLoginError==null && bornPacks==0 && bornIssued && bornPacksAgain==0,
                    "REVIEW-293: retired legacy packs are absent across repeated login packs="+bornPacks+" again="+bornPacksAgain+" issued="+bornIssued+(bornLoginError==null?"":" "+bornLoginError.GetType().Name+":"+bornLoginError.Message.Replace("\r"," ").Replace("\n"," ")));
                Query("UPDATE revora_character_creation SET starter_issued=false WHERE account_name='born293'");
                Query("DELETE FROM items WHERE entityid=@uid AND id IN(727026,723753)","uid",(long)bornUid);
                foreach(var consumed in born.Inventory.Objects.Where(i=>i.ID==727026u||i.ID==723753u).ToArray())
                    born.Inventory.Remove(consumed.UID,Revora.Game.Enums.ItemUse.None,true);
                GeneralData.DoLogin(born);
                verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",bornUid)==0
                    && Scalar("SELECT CASE WHEN packs_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",bornUid)==1,
                    "REVIEW-295: consumed starter packs are not reissued while class gear remains pending");
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('born295post','',0,2)");
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES('born295post',clock_timestamp(),clock_timestamp(),1)");
                var postBorn=new GameState(null);postBorn.Fake=false;postBorn.Account=new AccountTable("born295post");
                var postPacket=new EnitityCreate();postPacket.Name="PostBorn295";postPacket.Body=1003;postPacket.Class=2;
                string postMessage="";
                bool postCreated=EntityTable.CreateEntity(postPacket,postBorn,ref postMessage);
                uint postUid=postBorn.Entity.UID;
                postBorn.JustCreated=true;postBorn.Socket.Alive=false;postBorn.Socket.IP="127.0.0.23";
                var packsAfter=bornHook.GetField("AfterPacksCommit",Hidden);
                packsAfter.SetValue(null,(Action)delegate{throw new InvalidOperationException("pack projection");});
                try { GeneralData.DoLogin(postBorn); } catch(Exception) { }
                finally { packsAfter.SetValue(null,null); }
                verify(postCreated && !Kernel.GamePool.ContainsKey(postUid)
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",postUid)==0
                    && Scalar("SELECT CASE WHEN packs_issued AND NOT starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",postUid)==1,
                    "REVIEW-295: interruption after legacy retirement retains its receipt without issuing old packs");
                var postReload=new GameState(null);postReload.Fake=false;postReload.Account=new AccountTable("born295post");
                postReload.Entity=new Entity(EntityFlag.Player,false);postReload.Entity.Owner=postReload;
                postReload.Entity.UID=postUid;postReload.Entity.Class=10;postReload.Socket.Alive=false;postReload.Socket.IP="127.0.0.24";
                typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(postReload.Entity,true);
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){
                    ConquerItemTable.LoadItems(c);c.Entity.FullyLoaded=true;return true;
                });
                Call(installType,null,"Replace",postUid,postReload,(Action)null);
                GeneralData.DoLogin(postReload);
                verify(postReload.LoggedIn && postReload.Inventory.Objects.Count(i=>i.ID==727026u||i.ID==723753u)==0
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",postUid)==0,
                    "REVIEW-295: fresh reload after committed retirement issues no legacy packs");
                var actorFake=new GameState(null);actorFake.Fake=true;
                if(actorFake.Socket!=null)actorFake.Socket.IP="127.0.0.22";
                Exception actorLoadError=null;
                try{actorFake.FakeLoad2(1900294u,"Actor294");}catch(Exception ex){actorLoadError=ex;}
                string actorTrace="";
                if(actorLoadError!=null){actorTrace=(actorLoadError.StackTrace??"").Replace("\r"," ").Replace("\n"," | ");if(actorTrace.Length>240)actorTrace=actorTrace.Substring(0,240);}
                var actorReal=new GameState(null);actorReal.Fake=false;actorReal.Account=new AccountTable(null);actorReal.Account.EntityID=1900294u;
                actorReal.Entity=new Entity(EntityFlag.Player,false);actorReal.Entity.UID=1900294u;actorReal.Entity.Name="kept";
                bool actorRegistered=actorFake.TimerSubscriptions!=null;
                Call(installType,null,"Replace",1900294u,actorReal,(Action)delegate{Call(installType,null,"TakeoverPrevious",actorFake,actorReal);});
                var actorScreenTimers=actorFake.Screen==null?null:actorFake.Screen.GetType().GetField("TimerSubscriptions",Instance).GetValue(actorFake.Screen) as System.IDisposable[];
                bool actorTimersGone=actorScreenTimers==null||actorScreenTimers.Length==0||actorScreenTimers[0]==null;
                verify(actorLoadError==null && actorRegistered && actorFake.TimerSubscriptions==null && actorTimersGone && ReferenceEquals(Kernel.GamePool[1900294u],actorReal) && actorReal.Entity.Name=="kept",
                    "REVIEW-293: a real login disposes the fake actor's timers and registration without replacing the successor"+(actorLoadError==null?"":" "+actorLoadError.GetType().Name+":"+actorLoadError.Message.Replace("\r"," ").Replace("\n"," ")+" "+actorTrace));
                var actorScreenHook=typeof(GameState).GetField("BeforeSyntheticScreenDispose",Hidden);
                actorScreenHook.SetValue(null,(Action<GameState>)delegate(GameState who){throw new InvalidOperationException("screen");});
                var actorFake2=new GameState(null);actorFake2.Fake=true;
                if(actorFake2.Socket!=null)actorFake2.Socket.IP="127.0.0.25";
                Exception actorLoad2=null;
                try{actorFake2.FakeLoad2(1900295u,"Actor295");}catch(Exception ex){actorLoad2=ex;}
                var actorReal2=new GameState(null);actorReal2.Fake=false;actorReal2.Account=new AccountTable(null);actorReal2.Account.EntityID=1900295u;
                actorReal2.Entity=new Entity(EntityFlag.Player,false);actorReal2.Entity.UID=1900295u;actorReal2.Entity.Name="kept2";
                try{
                    Call(installType,null,"Replace",1900295u,actorReal2,(Action)delegate{Call(installType,null,"TakeoverPrevious",actorFake2,actorReal2);});
                    var actorPetTimers=actorFake2.Pet==null?null:actorFake2.Pet.GetType().GetField("TimerSubscriptions",Instance).GetValue(actorFake2.Pet) as System.IDisposable[];
                    bool actorPetGone=actorPetTimers==null||actorPetTimers.Length==0||actorPetTimers[0]==null;
                    verify(actorLoad2==null && actorPetGone && actorFake2.TimerSubscriptions==null && actorFake2.Screen!=null && actorFake2.Screen.Owner==null
                        && ReferenceEquals(Kernel.GamePool[1900295u],actorReal2) && actorReal2.Entity.Name=="kept2",
                        "REVIEW-299: a screen cleanup failure still releases the fake's other resources without changing the successor"+(actorLoad2==null?"":" "+actorLoad2.GetType().Name));
                } finally { actorScreenHook.SetValue(null,null); }
                Query("DELETE FROM items WHERE entityid=@uid AND position<>0","uid",(long)bornUid);
                GeneralData.DoLogin(born);
                verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",bornUid)==0
                    && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",bornUid)==1,
                    "REVIEW-299: consumed class gear is not granted again after its receipt is set");
                byte[] kitPacketClass={2,6,4,8,10,12,14,0,16};
                int[] kitCount={2,2,2,2,2,2,2,2,4};
                int kitOk=0;
                for(int kitIndex=0;kitIndex<kitPacketClass.Length;kitIndex++){
                    string kitUser="kit299"+kitIndex;
                    string kitName="KitHero"+kitIndex;
                    Query("INSERT INTO accounts(username,password,entityid,state) VALUES(@name,'',0,2)","name",kitUser);
                    Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES(@name,clock_timestamp(),clock_timestamp(),1)","name",kitUser);
                    var kitPlayer=new GameState(null);kitPlayer.Fake=false;kitPlayer.Account=new AccountTable(kitUser);
                    if(kitPlayer.Socket!=null){kitPlayer.Socket.Alive=false;kitPlayer.Socket.IP="127.0.0.30";}
                    var kitCreate=new EnitityCreate();kitCreate.Name=kitName;kitCreate.Body=1003;kitCreate.Class=kitPacketClass[kitIndex];
                    string kitMessage="";
                    bool kitCreated=EntityTable.CreateEntity(kitCreate,kitPlayer,ref kitMessage);
                    kitPlayer.JustCreated=true;
                    try{GeneralData.DoLogin(kitPlayer);}catch(Exception){}
                    long kitGear=Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitPlayer.Entity.UID);
                    long kitIssued=Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitPlayer.Entity.UID);
                    if(kitCreated && kitMessage=="ANSWER_OK" && kitGear==kitCount[kitIndex] && kitIssued==1) kitOk++;
                }
                verify(kitOk==kitPacketClass.Length,"REVIEW-299: every supported class stores its full starter kit with the receipt ok="+kitOk);
                var missingGear=ConquerItemInformation.BaseInformations[130006u];
                ConquerItemInformation.BaseInformations.Remove(130006u);
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('kitmiss','',0,2)");
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES('kitmiss',clock_timestamp(),clock_timestamp(),1)");
                var kitMiss=new GameState(null);kitMiss.Fake=false;kitMiss.Account=new AccountTable("kitmiss");
                if(kitMiss.Socket!=null){kitMiss.Socket.Alive=false;kitMiss.Socket.IP="127.0.0.31";}
                var kitMissPacket=new EnitityCreate();kitMissPacket.Name="KitMiss299";kitMissPacket.Body=1003;kitMissPacket.Class=2;
                string kitMissMessage="";
                try{
                    EntityTable.CreateEntity(kitMissPacket,kitMiss,ref kitMissMessage);
                    kitMiss.JustCreated=true;
                    try{GeneralData.DoLogin(kitMiss);}catch(Exception){}
                    verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitMiss.Entity.UID)==0
                        && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitMiss.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",kitMiss.Entity.UID)==0,
                        "REVIEW-299: a missing gear catalog leaves the kit pending and does not mark it delivered");
                } finally { ConquerItemInformation.BaseInformations[130006u]=missingGear; }
                var gearBefore=bornHook.GetField("BeforeGearCommit",Hidden);
                gearBefore.SetValue(null,(Action<Npgsql.NpgsqlConnection,Npgsql.NpgsqlTransaction>)delegate{throw new InvalidOperationException("gear boundary");});
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('kitroll','',0,2)");
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES('kitroll',clock_timestamp(),clock_timestamp(),1)");
                var kitRoll=new GameState(null);kitRoll.Fake=false;kitRoll.Account=new AccountTable("kitroll");
                if(kitRoll.Socket!=null){kitRoll.Socket.Alive=false;kitRoll.Socket.IP="127.0.0.32";}
                var kitRollPacket=new EnitityCreate();kitRollPacket.Name="KitRoll299";kitRollPacket.Body=1003;kitRollPacket.Class=2;
                string kitRollMessage="";
                try{
                    EntityTable.CreateEntity(kitRollPacket,kitRoll,ref kitRollMessage);
                    kitRoll.JustCreated=true;
                    try{GeneralData.DoLogin(kitRoll);}catch(Exception){}
                    verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitRoll.Entity.UID)==0
                        && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitRoll.Entity.UID)==0
                        && Scalar("SELECT CASE WHEN packs_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitRoll.Entity.UID)==1,
                        "REVIEW-299: a gear transaction failure rolls back the kit and leaves the receipt pending");
                } finally { gearBefore.SetValue(null,null); }
                // LoadData owns item loading for these login attempts as well.
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){c.Entity.FullyLoaded=true;return true;});
                Query("INSERT INTO items(uid,id,entityid,position,warehouse,bound,stacksize) VALUES(1990302,131006,@uid,3,0,1,1)","uid",(long)kitRoll.Entity.UID);
                GeneralData.DoLogin(kitRoll);
                verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitRoll.Entity.UID)==1
                    && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitRoll.Entity.UID)==0,
                    "REVIEW-302: occupied equipment slots keep the kit pending without duplicate equipped rows");
                Query("DELETE FROM items WHERE entityid=@uid AND position<>0","uid",(long)kitRoll.Entity.UID);
                Query("UPDATE revora_character_creation SET starter_issued=false WHERE character_uid=@uid","uid",(long)kitRoll.Entity.UID);
                kitRoll.Equipment=new Revora.Game.ConquerStructures.Equipment(kitRoll);
                Query("CREATE FUNCTION suppress_kit_item_302() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=410009 THEN RETURN NULL; END IF; RETURN NEW; END'");
                Query("CREATE TRIGGER suppress_kit_item_302 BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_kit_item_302()");
                try {
                    GeneralData.DoLogin(kitRoll);
                    verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitRoll.Entity.UID)==0
                        && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitRoll.Entity.UID)==0,
                        "REVIEW-302: a suppressed second kit insert rolls back the first item and receipt");
                } finally { Query("DROP TRIGGER suppress_kit_item_302 ON items"); }
                Query("CREATE FUNCTION suppress_kit_receipt_302() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.starter_issued THEN RETURN NULL; END IF; RETURN NEW; END'");
                Query("CREATE TRIGGER suppress_kit_receipt_302 BEFORE UPDATE ON revora_character_creation FOR EACH ROW EXECUTE FUNCTION suppress_kit_receipt_302()");
                try {
                    GeneralData.DoLogin(kitRoll);
                    verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitRoll.Entity.UID)==0
                        && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitRoll.Entity.UID)==0,
                        "REVIEW-302: a suppressed kit receipt rolls back every kit item");
                } finally { Query("DROP TRIGGER suppress_kit_receipt_302 ON revora_character_creation"); }
                var gearAfter=bornHook.GetField("AfterGearCommit",Hidden);
                gearAfter.SetValue(null,(Action)delegate{throw new InvalidOperationException("gear projection");});
                try { GeneralData.DoLogin(kitRoll); } catch(Exception) { }
                finally { gearAfter.SetValue(null,null); }
                uint kitReloadUid=kitRoll.Entity.UID;
                verify(!Kernel.GamePool.ContainsKey(kitReloadUid)
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitReloadUid)==2
                    && Scalar("SELECT CASE WHEN starter_issued THEN 1 ELSE 0 END FROM revora_character_creation WHERE character_uid=@uid",kitReloadUid)==1,
                    "REVIEW-302: interruption after gear commit retires the projection and preserves the complete kit");
                var kitReload=new GameState(null);kitReload.Fake=false;kitReload.Account=new AccountTable("kitroll");
                kitReload.Entity=new Entity(EntityFlag.Player,false);kitReload.Entity.Owner=kitReload;
                kitReload.Entity.UID=kitReloadUid;kitReload.Entity.Class=10;kitReload.Socket.Alive=false;kitReload.Socket.IP="127.0.0.34";
                typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(kitReload.Entity,true);
                // DoLogin.LoadData performs the real item load; do not load a second
                // copy in the entity hook and make the loader relocate occupied slots.
                installType.GetField("LoadEntityHook",Hidden).SetValue(null,(Func<GameState,bool>)delegate(GameState c){c.Entity.FullyLoaded=true;return true;});
                Call(installType,null,"Replace",kitReloadUid,kitReload,(Action)null);
                GeneralData.DoLogin(kitReload);
                verify(kitReload.LoggedIn && kitReload.Equipment.TryGetItem((byte)ConquerItem.Armor)!=null
                    && kitReload.Equipment.TryGetItem((byte)ConquerItem.RightWeapon)!=null
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitReloadUid)==2,
                    "REVIEW-302: fresh item loading recovers committed gear without granting a second kit logged="+kitReload.LoggedIn
                    +" armor="+(kitReload.Equipment.TryGetItem((byte)ConquerItem.Armor)!=null)
                    +" weapon="+(kitReload.Equipment.TryGetItem((byte)ConquerItem.RightWeapon)!=null)
                    +" rows="+Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitReloadUid));
                Query("UPDATE items SET position=0 WHERE entityid=@uid AND position<>0","uid",(long)kitReloadUid);
                kitReload.Equipment=new Revora.Game.ConquerStructures.Equipment(kitReload);
                GeneralData.DoLogin(kitReload);
                verify(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(130006,410009)",kitReloadUid)==2
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",kitReloadUid)==0,
                    "REVIEW-302: moving delivered gear leaves the receipt paid and grants no replacement");
                var bornNotes=new System.Collections.Generic.List<string>();
                var bornSend=typeof(GameState).GetField("BeforePacketSend",Hidden);
                bornSend.SetValue(null,(Action<GameState,Revora.Interfaces.IPacket>)delegate(GameState who, Revora.Interfaces.IPacket pkt){
                    var talk=pkt as Message; if(talk!=null) bornNotes.Add(talk.__Message); });
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES('kitwire','',0,2)");
                Query("INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count) VALUES('kitwire',clock_timestamp(),clock_timestamp(),1)");
                var kitWire=new GameState(null);kitWire.Fake=false;kitWire.Action=1;kitWire.Account=new AccountTable("kitwire");
                if(kitWire.Socket!=null){kitWire.Socket.Alive=false;kitWire.Socket.IP="127.0.0.33";}
                var wire=new byte[80];
                var wireName=System.Text.Encoding.Default.GetBytes("WireHero299");
                System.Buffer.BlockCopy(wireName,0,wire,24,wireName.Length);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes((ushort)1003),0,wire,72,2);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes((ushort)2),0,wire,74,2);
                try{EnitityCreate.Process(wire,kitWire);}catch(Exception){}
                uint wireUid=kitWire.Entity==null?0:kitWire.Entity.UID;
                bool wireOk=false; foreach(var note in bornNotes) if(note=="ANSWER_OK") wireOk=true;
                bornNotes.Clear();
                kitWire.Action=1;
                try{EnitityCreate.Process(wire,kitWire);}catch(Exception){}
                bool wireAgain=false; foreach(var note in bornNotes) if(note=="ANSWER_OK") wireAgain=true;
                verify(wireOk && !wireAgain && wireUid!=0 && Scalar("SELECT count(*) FROM entities WHERE uid=@uid",wireUid)==1
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(727026,723753)",wireUid)==0
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position<>0",wireUid)==2
                    && Scalar("SELECT count(*) FROM revora_character_creation WHERE character_uid=@uid AND revora_starter_pending",wireUid)==1
                    && Scalar("SELECT entityid FROM configuration WHERE server='Revora' AND (@uid>=0)",wireUid)>=wireUid,
                    "REVIEW-299: the creation packet commits one character, sends success once, and stores only basic class gear");
                bornSend.SetValue(null,null);
            } catch(Exception ex) {
                verify(false,"REVIEW-293: character creation "+ex.GetType().Name+":"+ex.Message.Replace("\r"," ").Replace("\n"," "));
            } finally {
                if(bornGate!=null)bornGate.SetValue(null,null);
                bornHook.GetField("BeforePacksCommit",Hidden).SetValue(null,null);
                bornHook.GetField("AfterPacksCommit",Hidden).SetValue(null,null);
                bornHook.GetField("BeforeGearCommit",Hidden).SetValue(null,null);
                bornHook.GetField("AfterGearCommit",Hidden).SetValue(null,null);
                typeof(GameState).GetField("BeforePacketSend",Hidden).SetValue(null,null);
                typeof(GameState).GetField("BeforeSyntheticScreenDispose",Hidden).SetValue(null,null);
                try{Call(activityType,null,"Relinquish");}catch(Exception){}
                leaseObject.SetValue(null,savedLeaseObject);
                clearLoginHooks();
                Server.PlayerCap=bornCap;Server.Thread=bornWorld;Server.EntityUID=bornCounter;
            }

            Revora.Thread.GenericThreadPool=loginTimerPool;

            var departed=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(departed);
            object leftRush=RushInstance(departed,3,DateTime.Now.AddMinutes(5));
            TrackRush(leftRush);
            try { BossRushSystem.OnDisconnect(departed); }
            finally { UntrackRush(leftRush); }
            VariableVault leftDisk;EntityVariableTable.Load(departed.Entity.UID,out leftDisk);
            verify((uint)departed[BossRushSystem.ClaimKey]==2u && (uint)leftDisk[BossRushSystem.ClaimKey]==2u,
                "leaving a boss-3 Boss Rush stores the consolation before the instance ends");

            var slain=WelcomePlayer(town,1,0,15);EntityVariableTable.Load(slain);
            object deathRush=RushInstance(slain,4,DateTime.Now.AddMinutes(5));
            deathRush.GetType().GetField("LivesLeft",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).SetValue(deathRush,1);
            TrackRush(deathRush);
            try { BossRushSystem.OnDeath(slain); }
            finally { UntrackRush(deathRush); }
            VariableVault deathDisk;EntityVariableTable.Load(slain.Entity.UID,out deathDisk);
            verify((uint)slain[BossRushSystem.ClaimKey]==2u && (uint)deathDisk[BossRushSystem.ClaimKey]==2u,
                "dying out of lives on a later boss stores the consolation before the instance ends");
        } finally {
            if(backed){File.Copy(backup,path,true);File.Delete(backup);}
        }
    }
    static uint boothStage=860000;
    static void VerifyBoothContext(Map town,Action<bool,string> verify) {
        foreach(bool silver in new[]{false,true})foreach(string mode in new[]{"edge","far","seller-moved","wrong-map","wrong-owner","unregistered","stale-booth","null-owner"}) {
            var seller=ReloadBall(WelcomePlayer(town,1,0,15));var buyer=ReloadBall(WelcomePlayer(town,1,0,15));
            seller.Entity.ConquerPoints=50;buyer.Entity.ConquerPoints=100;seller.Entity.Money=50;buyer.Entity.Money=100;
            EntityTable.SaveEntity(seller);EntityTable.SaveEntity(buyer);
            seller.Entity.X=300;seller.Entity.Y=300;buyer.Entity.X=300;buyer.Entity.Y=300;
            var item=Pack(seller,1088001,1);uint id=StageCpBooth(seller,buyer,item,10);var booth=seller.Booth;
            if(silver){var listing=booth.ItemList[item.UID];listing.Cost_Type=Revora.Game.ConquerStructures.BoothItem.CostType.Silvers;booth.ItemList[item.UID]=listing;}
            if(mode=="edge" || mode=="far")buyer.Entity.X=(ushort)(300+Constants.nScreenDistance+(mode=="far"?1:0));
            if(mode=="seller-moved")seller.Entity.Y=302;
            if(mode=="wrong-map")booth.Base.MapID=1000;
            if(mode=="wrong-owner")booth.Base.Owner=buyer;
            if(mode=="null-owner")booth.Base.Owner=null;
            if(mode=="unregistered")ClearBooth(seller,id);
            if(mode=="stale-booth")seller.Booth=new Revora.Game.ConquerStructures.Booth();
            BoothItem.BuyFromBooth(new ItemUsage(true){UID=item.UID,dwParam=id},buyer);
            bool success=mode=="edge";
            verify(!Fenced(buyer) && !Fenced(seller) && BoothAchievement(buyer)==(success?1:0)
                && buyer.Entity.ConquerPoints==(success && !silver?90u:100u) && seller.Entity.ConquerPoints==(success && !silver?60u:50u)
                && buyer.Entity.Money==(success && silver?90UL:100UL) && seller.Entity.Money==(success && silver?60UL:50UL)
                && Scalar("SELECT entityid FROM items WHERE uid=@uid",item.UID)==(success?buyer.Entity.UID:seller.Entity.UID),
                "booth access validates active owner and screen boundary: silver="+silver+" "+mode);
            ClearBooth(seller,id);
        }
    }
    static long BoothAchievement(GameState p) {
        return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10128",p.Entity.UID);
    }
    static void VerifySilverBoothSettlement(Map town,Action<bool,string> verify) {
        foreach(string mode in new[]{"success","buyer-write","seller-write","item-write","achievement-write","insufficient","overflow","external-buyer","external-seller","bound","full-bag","large-price"}) {
            var seller=ReloadBall(WelcomePlayer(town,1,0,15));var buyer=ReloadBall(WelcomePlayer(town,1,0,15));
            ulong price=mode=="large-price"?5000000000UL:40UL;
            buyer.Entity.Money=mode=="insufficient"?1:price+60;
            seller.Entity.Money=mode=="overflow"?(ulong)long.MaxValue:50;
            EntityTable.SaveEntity(buyer);EntityTable.SaveEntity(seller);
            ulong buyerBefore=buyer.Entity.Money,sellerBefore=seller.Entity.Money;
            var goods=Pack(seller,1088001,1);uint id=StageCpBooth(seller,buyer,goods,price);
            var listing=seller.Booth.ItemList[goods.UID];listing.Cost_Type=Revora.Game.ConquerStructures.BoothItem.CostType.Silvers;seller.Booth.ItemList[goods.UID]=listing;
            if(mode=="bound")goods.Bound=true;
            if(mode=="full-bag")while(buyer.Inventory.Count<40)Pack(buyer,1088001,1);
            if(mode.StartsWith("external-"))Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)(mode=="external-buyer"?buyer:seller).Entity.UID);
            string table=mode=="item-write"?"items":mode=="achievement-write"?"revora_achievement_evidence":"entities";
            bool fault=mode.EndsWith("-write");
            if(fault) {
                string condition=mode=="buyer-write"?"NEW.uid="+buyer.Entity.UID:mode=="seller-write"?"NEW.uid="+seller.Entity.UID:"true";
                Query("CREATE FUNCTION suppress_silver_booth() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF "+condition+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_silver_booth BEFORE "+(mode=="achievement-write"?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_silver_booth()");
            }
            try {System.Threading.Tasks.Parallel.For(0,4,n=>BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=id},buyer));}
            finally {if(fault)Query("DROP TRIGGER suppress_silver_booth ON "+table+";DROP FUNCTION suppress_silver_booth()");}
            bool success=mode=="success" || mode=="large-price";
            verify(Scalar("SELECT money FROM entities WHERE uid=@uid",buyer.Entity.UID)==(long)(success?buyerBefore-price:buyerBefore)+(mode=="external-buyer"?1:0)
                && Scalar("SELECT money FROM entities WHERE uid=@uid",seller.Entity.UID)==(long)(success?sellerBefore+price:sellerBefore)+(mode=="external-seller"?1:0)
                && Scalar("SELECT entityid FROM items WHERE uid=@uid",goods.UID)==(success?buyer.Entity.UID:seller.Entity.UID)
                && BoothAchievement(buyer)==(success?1:0) && BoothAchievement(seller)==0,
                "silver booth atomically preserves funds, item ownership and buyer-only achievement: "+mode);
            ClearBooth(seller,id);
            if(success) {
                buyer=ReloadBall(buyer);seller=ReloadBall(seller);buyer.Entity.MyAchievement.Send();
                verify(buyer.Entity.Money==buyerBefore-price && seller.Entity.Money==sellerBefore+price
                    && buyer.Inventory.ContainsUID(goods.UID) && !seller.Inventory.ContainsUID(goods.UID)
                    && HasAchievement(buyer,10128) && BoothAchievement(buyer)==1,
                    "silver booth production login preserves single transfer and achievement: "+mode);
            }
        }
    }
    static uint StageCpBooth(GameState seller,GameState buyer,ConquerItem item,ulong cost){
        if(seller.Trade==null)seller.Trade=new Revora.Game.ConquerStructures.Trade();
        if(buyer.Trade==null)buyer.Trade=new Revora.Game.ConquerStructures.Trade();
        var booth=new Revora.Game.ConquerStructures.Booth();
        var spawn=new SobNpcSpawn();
        uint id=++boothStage;spawn.UID=id;spawn.Owner=seller;spawn.MapID=seller.Entity.MapID;spawn.X=seller.Entity.X;spawn.Y=seller.Entity.Y;booth.Base=spawn;seller.Booth=booth;
        booth.ItemList.Add(item.UID,new Revora.Game.ConquerStructures.BoothItem{Item=item,Cost=cost,Cost_Type=Revora.Game.ConquerStructures.BoothItem.CostType.ConquerPoints});
        var map=(System.Collections.Generic.Dictionary<uint,Revora.Game.ConquerStructures.Booth>)typeof(Revora.Game.ConquerStructures.Booth).GetField("Booths",Hidden).GetValue(null);
        lock(Revora.Game.ConquerStructures.Booth.SyncRoot)map[id]=booth;return id;
    }
    static void ClearBooth(GameState seller,uint id){
        var map=(System.Collections.Generic.Dictionary<uint,Revora.Game.ConquerStructures.Booth>)typeof(Revora.Game.ConquerStructures.Booth).GetField("Booths",Hidden).GetValue(null);
        lock(Revora.Game.ConquerStructures.Booth.SyncRoot)map.Remove(id);
        if(seller!=null)seller.Booth=null;
    }
    static void VerifyPurseCompatibility(Map town){
        Call(CpBags,null,"Initialize");Call(QuestGifts,null,"Initialize");
        QuestGifts.GetField("SoulRoll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        int failures=0;
        Action<bool,string> verify=(ok,name)=>{System.Console.WriteLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failures++;};
        var pending=ReloadBall(WelcomePlayer(town,1,0,15));var bag=Pack(pending,720656,1);
        uint cp=pending.Entity.ConquerPoints;pending.Entity.AddConquerPoints(4);OpenCpBag(pending,bag);
        verify(!Fenced(pending) && CpBagUses(pending)==1 && pending.Entity.ConquerPoints==cp+14 && EntityTable.SaveEntity(pending),
            "a normal CP gain followed by a CP bag preserves both awards without fencing");
        var gift=ReloadBall(WelcomePlayer(town,1,0,15));var pack=Pack(gift,721269,1);
        cp=gift.Entity.ConquerPoints;uint rvp=gift.Entity.RevoraPoint;OpenGift(gift,pack);
        verify(!Fenced(gift) && GiftReceipts(gift)==1 && gift.Entity.ConquerPoints==cp+5000 && gift.Entity.RevoraPoint==rvp+1000,
            "the ordinary custom gift commits its exact CP and RVP entitlement");
        verify(EntityTable.SaveEntity(gift) && !Fenced(gift),
            "a committed RVP reward refreshes its saved image before full save");
        var explicitSave=ReloadBall(WelcomePlayer(town,1,0,15));explicitSave.Entity.AddConquerPoints(4);
        EntityTable.UpdateCps(explicitSave);
        verify(EntityTable.SaveEntity(explicitSave) && !Fenced(explicitSave),
            "an explicit legacy CP save remains compatible with the next full save");
        var setter=ReloadBall(WelcomePlayer(town,1,0,15));cp=setter.Entity.ConquerPoints;
        setter.Entity.ConquerPoints=cp+4;
        verify(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",setter.Entity.UID)==cp+4,
            "the CP setter preserves the immediate persistence contract used by shop delivery");
        var spentBag=ReloadBall(WelcomePlayer(town,1,0,15));cp=spentBag.Entity.ConquerPoints;
        spentBag.Entity.AddConquerPoints(9);verify(spentBag.Entity.TrySpendConquerPoints(3),"a loaded player can spend Conquer Points");
        OpenCpBag(spentBag,Pack(spentBag,720656,1));
        verify(!Fenced(spentBag) && spentBag.Entity.ConquerPoints==cp+16 && EntityTable.SaveEntity(spentBag),"a spend followed by a CP bag keeps both changes");
        spentBag=ReloadBall(spentBag);verify(!Fenced(spentBag) && spentBag.Entity.ConquerPoints==cp+16,"reload keeps the spent bag balance");
        var pendingGift=ReloadBall(WelcomePlayer(town,1,0,15));cp=pendingGift.Entity.ConquerPoints;uint giftRvp=pendingGift.Entity.RevoraPoint;
        pendingGift.Entity.AddConquerPoints(4);pendingGift.Entity.AddRevoraPoints(7);
        OpenGift(pendingGift,Pack(pendingGift,721269,1));
        verify(!Fenced(pendingGift) && pendingGift.Entity.ConquerPoints==cp+5004 && pendingGift.Entity.RevoraPoint==giftRvp+1007
            && EntityTable.SaveEntity(pendingGift),"a gift includes pending CP and Revora Points and stays saveable");
        Query("CREATE TABLE IF NOT EXISTS accounts(username text PRIMARY KEY, entityid bigint NOT NULL)");
        Query("CREATE TABLE IF NOT EXISTS xtremetop_votes(vote_id uuid PRIMARY KEY, token text, account_id uuid, username text NOT NULL, entity_uid bigint NOT NULL, entity_name text, status text NOT NULL, voting_ip text, cp_amount integer NOT NULL, rvp_amount integer NOT NULL, gold_amount bigint NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), voted_at timestamptz, delivered_at timestamptz)");
        VoteGrants.GetField("Relation",Hidden).SetValue(null,"xtremetop_votes");
        var voteSpend=VotePlayer(town,"PurseSpend",100,20,1000);
        verify(voteSpend.Entity.TrySpendConquerPoints(30),"vote fixture can spend CP");
        voteSpend.Entity.RevoraPoint=voteSpend.Entity.RevoraPoint-5;
        VoteRow(voteSpend,"voted",2500,50,50000);VoteGrantDelivery.ApplyFor(voteSpend);
        verify(!Fenced(voteSpend) && voteSpend.Entity.ConquerPoints==2570 && voteSpend.Entity.RevoraPoint==65 && VoteRows(voteSpend,"delivered")==1
            && EntityTable.SaveEntity(voteSpend),"a spend is included in the vote and the following save");
        voteSpend=ReloadBall(voteSpend);ArmVote(voteSpend);
        verify(!Fenced(voteSpend) && voteSpend.Entity.ConquerPoints==2570 && voteSpend.Entity.RevoraPoint==65,"reload keeps the spent vote purse");
        var payer=ReloadBall(WelcomePlayer(town,1,0,15));var payee=ReloadBall(WelcomePlayer(town,1,0,15));
        payer.Entity.ConquerPoints=2000;payee.Entity.ConquerPoints=10;
        var purseType=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurseBalance");
        var transfer=purseType.GetMethod("TryTransferConquerPoints",Hidden);
        verify(transfer.Invoke(null,new object[]{payer,payee,(uint)1500000}).ToString()=="Insufficient"
            && payer.Entity.ConquerPoints==2000 && payee.Entity.ConquerPoints==10,"JiangHu leaves both purses unchanged when the payer is short");
        payee.Entity.ConquerPoints=uint.MaxValue;
        verify(transfer.Invoke(null,new object[]{payer,payee,(uint)1}).ToString()=="Overflow"
            && payer.Entity.ConquerPoints==2000 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",payer.Entity.UID)==2000,"JiangHu does not debit when the recipient cannot hold the reward");
        payee.Entity.ConquerPoints=10;
        verify(transfer.Invoke(null,new object[]{payer,payee,(uint)1500}).ToString()=="Transferred"
            && payer.Entity.ConquerPoints==500 && payee.Entity.ConquerPoints==1510,"JiangHu transfers only after both balances fit");
        verify(EntityTable.SaveEntity(payer) && EntityTable.SaveEntity(payee),"JiangHu purses remain saveable");
        payer=ReloadBall(payer);payee=ReloadBall(payee);
        verify(payer.Entity.ConquerPoints==500 && payee.Entity.ConquerPoints==1510,"reload keeps one JiangHu transfer");
        payer.Entity.ConquerPoints=800;payee.Entity.ConquerPoints=800;
        verify(transfer.Invoke(null,new object[]{payer,payee,(uint)100}).ToString()=="Transferred"
            && transfer.Invoke(null,new object[]{payee,payer,(uint)40}).ToString()=="Transferred"
            && payer.Entity.ConquerPoints==740 && payee.Entity.ConquerPoints==860,"opposing JiangHu transfers keep both balances");
        var hook=purseType.GetField("AfterTransferCommit",Hidden);
        hook.SetValue(null,(Action<GameState,GameState>)((left,right)=>{throw new InvalidOperationException("projection");}));
        try{
            payer.Entity.ConquerPoints=900;payee.Entity.ConquerPoints=100;
            verify(transfer.Invoke(null,new object[]{payer,payee,(uint)100}).ToString()=="Rejected","a projection failure after the JiangHu commit fences both sessions");
        }finally{hook.SetValue(null,null);}
        verify(Fenced(payer) && Fenced(payee),"both JiangHu sessions reload after a projection failure");
        payer=ReloadBall(payer);payee=ReloadBall(payee);
        verify(payer.Entity.ConquerPoints==800 && payee.Entity.ConquerPoints==200,"reload shows the committed JiangHu transfer exactly once");
        payer.Entity.ConquerPoints=80;payee.Entity.ConquerPoints=200;
        long pool=Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",payer.Entity.UID)+Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",payee.Entity.UID);
        int wins=0;Parallel.For(0,2,n=>{if(transfer.Invoke(null,new object[]{payer,payee,(uint)50}).ToString()=="Transferred")System.Threading.Interlocked.Increment(ref wins);});
        payer=ReloadBall(payer);payee=ReloadBall(payee);
        verify(wins==1 && payer.Entity.ConquerPoints==30 && payee.Entity.ConquerPoints==250
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",payer.Entity.UID)+Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",payee.Entity.UID)==pool,
            "two simultaneous JiangHu debits cannot spend the same purse twice");
        Check(failures==0,"shared purse compatibility failures: "+failures);
    }
    static void VerifyConquerPointBags(string runtime,Map town){
        Call(CpBags,null,"Initialize");Call(CpBags,null,"Initialize");
        CpBags.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>cpBagNow));
        // Exercise the loaded purse owner, then save before reconnecting. Reloading
        // immediately after the award hides a stale committed balance image.
        var saved=ReloadBall(WelcomePlayer(town,1,0,15));
        var savedBag=Pack(saved,720656,1);uint savedBefore=saved.Entity.ConquerPoints;
        uint savedRvp=saved.Entity.RevoraPoint;saved.Entity.AddRevoraPoints(7);
        OpenCpBag(saved,savedBag);
        Check(!Fenced(saved) && CpBagUses(saved)==1 && saved.Entity.ConquerPoints==savedBefore+10,
            "a production-loaded ordinary character receives the CP bag award");
        Check(EntityTable.SaveEntity(saved) && !Fenced(saved),
            "a committed CP bag refreshes the saved purse image before the next full save");
        saved=ReloadBall(saved);
        Check(saved.Entity.ConquerPoints==savedBefore+10 && saved.Entity.RevoraPoint==savedRvp+7 && CpBagUses(saved)==1,
            "a CP-only commit preserves unsaved RVP through save and reload");
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        Check(handler.IndexOf("CP bag must use the owned service.")>=0 && handler.IndexOf("case 720685:")>=0,
            "CP bags are fail-closed on the live handler");
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ConquerPointBags.cs")));
        Check(service.IndexOf("if(item.ID!=720685)p.Send(new Message(\"You received \"")>=0 && service.IndexOf("if(item.ID==720685)Kernel.SendWorldMessage")>=0,
            "Nimbus 720685 keeps its legacy world-only announcement while ordinary CP bags retain personal receipts");
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit enumerates the exact 518 numeric top-level UseItem case IDs without synthetic rows");
        Check(File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs"))).IndexOf("GetPositionFromID")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs"))).IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        var rows=new[]{
            new object[]{729911u,3u},new object[]{3001133u,5u},new object[]{720657u,5u},new object[]{729912u,6u},
            new object[]{720656u,10u},new object[]{3001134u,10u},new object[]{3001135u,20u},new object[]{720655u,20u},
            new object[]{720658u,25u},new object[]{720663u,50u},new object[]{720659u,50u},new object[]{720660u,100u},
            new object[]{720664u,100u},new object[]{720665u,200u},new object[]{720675u,250u},new object[]{720653u,270u},
            new object[]{720681u,500u},new object[]{720676u,500u},new object[]{3001136u,500u},new object[]{720677u,1000u},
            new object[]{720682u,1000u},new object[]{720687u,1000u},new object[]{720661u,1350u},new object[]{720683u,2000u},
            new object[]{720688u,2000u},new object[]{720693u,2500u},new object[]{720666u,2700u},new object[]{720689u,4000u},
            new object[]{720694u,5000u},new object[]{720662u,6900u},new object[]{720695u,10000u},new object[]{720678u,13500u},
            new object[]{720667u,13800u},new object[]{720684u,27000u},new object[]{720685u,138000u}};
        foreach(var row in rows){
            uint id=(uint)row[0];uint pay=(uint)row[1];
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id),"installed catalog contains CP bag "+id);
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,id,1);uint before=p.Entity.ConquerPoints;OpenCpBag(p,item);
            Check(!Fenced(p) && CpBagUses(p)==1 && p.Entity.ConquerPoints==before+pay && !p.Inventory.ContainsUID(item.UID),
                "CP bag "+id+" pays "+pay+" from ordinary action-4");
            p=ReloadBall(p);Check(CpBagUses(p)==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==before+pay,"CP bag "+id+" survives production reload");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,720656,stack,bound);uint before=p.Entity.ConquerPoints;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenCpBag(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && CpBagUses(p)==expected && p.Entity.ConquerPoints==before+10u*(uint)expected && CountId(p,720656)==0,
                "CP bag 720656 pays 10 CP with stack remainder semantics: "+bound+"/"+stack);
        }
        var overflow=WelcomePlayer(town,1,0,15);Query("UPDATE entities SET conquerpoints=4294967295 WHERE uid=@uid","uid",(long)overflow.Entity.UID);overflow=ReloadBall(overflow);var rich=Pack(overflow,720695,1);OpenCpBag(overflow,rich);
        Check(!Fenced(overflow) && CpBagUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.Entity.ConquerPoints==uint.MaxValue,"CP overflow preserves the bag");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720656,1);uint before=p.Entity.ConquerPoints;
            string table=fault=="receipt"?"revora_cp_bag_uses":fault=="output"?"entities":"items";
            string verb=fault=="source"?"DELETE":fault=="output"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_cp_bag() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_cp_bag BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_cp_bag()");OpenCpBag(p,item);
            Check(Fenced(p) && CpBagUses(p)==0 && p.Inventory.ContainsUID(item.UID) && p.Entity.ConquerPoints==before,"suppressed "+fault+" preserves CP bag source and purse");
            Query("DROP TRIGGER suppress_cp_bag ON "+table+";DROP FUNCTION suppress_cp_bag()");p=ReloadBall(p);OpenCpBag(p,p.Inventory.Objects.Single(i=>i.ID==720656));
            Check(!Fenced(p) && CpBagUses(p)==1 && p.Entity.ConquerPoints==before+10,"production reload retries the CP bag after "+fault+" rollback");
        }
        var stale=WelcomePlayer(town,1,0,15);var supply=Pack(stale,720656,1);Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);uint staleBefore=stale.Entity.ConquerPoints;OpenCpBag(stale,supply);
        Check(Fenced(stale) && CpBagUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==staleBefore+1,"stale CP preimage cannot open a CP bag");
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720656,1);uint cp=p.Entity.ConquerPoints;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=cpBagNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenCpBag(p,item);
            Check(CpBagUses(p)==0 && p.Entity.ConquerPoints==cp && p.Inventory.ContainsUID(item.UID),"ineligible CP bag leaves source unchanged: "+guard);
        }
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720656,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && CpBagUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded CP bag rejects "+bad+" frame");
        }OpenCpBag(frames,wire);Check(!Fenced(frames) && CpBagUses(frames)==1,"ordinary CP bag works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,720656,1);uint uid=once.UID;OpenCpBag(replay,once);
        ItemHandler.UseItem(once,replay);Check(CpBagUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed CP bag UID cannot replay");
        var leftover=WelcomePlayer(town,1,0,15);var box=Pack(leftover,723268,1);
        Check(!(bool)Call(CpBags,null,"TryUse",box,leftover) && leftover.Inventory.ContainsUID(box.UID),"MeteorBox 723268 stays outside ConquerPointBags");
    }

    static void VerifyTempestPermit(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsStore",true);
        Call(store,null,"Initialize");Call(store,null,"Initialize");World(runtime,1004);World(runtime,1011);
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Action<GameState,ushort,ushort,ushort> move=(p,map,x,y)=>{p.Entity.MapID=map;p.Entity.X=x;p.Entity.Y=y;};
        var low=ReloadBall(WelcomePlayer(town,99,0,15));Check(!(bool)Call(store,null,"Eligible",low),"Tempest rejects unreborn level99");
        foreach(byte life in new byte[]{0,1,2}){
            var p=ReloadBall(WelcomePlayer(town,life==0?(byte)100:(byte)15,life,15));
            Check((bool)Call(store,null,"Eligible",p),"Tempest official level100 or reborn eligibility "+life);
            Check(!act(p,"Accept"),"Tempest cannot accept away from Chang");move(p,1004,51,49);
            Check(act(p,"Accept") && !act(p,"Accept") && stage(p)==1,"Tempest once-only admission "+life);
            uint soul=(uint)Field(Call(store,null,"Load",p),"Soul");
            move(p,1011,212,196);var first=NestedPack(p,720027,2,false);var second=NestedPack(p,720027,2,true);
            Check(!act(p,"Permit") && stage(p)==1 && first.StackSize==2 && second.StackSize==2,"Tempest short payment preserves both stacks");
            var third=NestedPack(p,720027,3,true);third.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)third.UID);
            Check(!act(p,"Permit"),"Tempest locked scrolls cannot complete payment");third.Lock=0;Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)third.UID);
            int calls=0;bool rolled=false;try{lock(p.ItemSyncRoot)Call(store,null,"Permit",p,(Func<bool>)(()=>++calls==1));}catch(InvalidOperationException){rolled=true;}
            Check(rolled && stage(p)==1 && Scalar("SELECT COUNT(*) FROM revora_tempest_permits WHERE uid=@uid",p.Entity.UID)==0 && SavedRune(first,"stacksize")==2 && SavedRune(third,"stacksize")==3,"Tempest late context failure rolls back every debit and receipt");
            Query("UPDATE items SET stacksize=4 WHERE uid=@uid","uid",(long)third.UID);bool mismatch=false;
            try{act(p,"Permit");}catch(InvalidOperationException){mismatch=true;}
            Check(mismatch && stage(p)==1 && SavedRune(first,"stacksize")==2 && Scalar("SELECT COUNT(*) FROM revora_tempest_payments WHERE uid=@uid",p.Entity.UID)==0,"Tempest stale final stack rolls back earlier debits");
            Query("UPDATE items SET stacksize=3 WHERE uid=@uid","uid",(long)third.UID);
            uint cp=p.Entity.ConquerPoints;ulong silver=p.Entity.Money;
            System.Threading.Tasks.Parallel.For(0,8,n=>{if(!act(p,"Permit"))throw new Exception("Paid reentry rejected");});
            Check(stage(p)==2 && !p.Inventory.ContainsUID(first.UID) && !p.Inventory.ContainsUID(second.UID) && third.StackSize==2 && SavedRune(third,"stacksize")==2,"Tempest concurrent entry consumes exactly five across mixed-bound stacks");
            Check(Scalar("SELECT SUM(spent) FROM revora_tempest_payments WHERE uid=@uid",p.Entity.UID)==5 && p.Entity.ConquerPoints==cp && p.Entity.Money==silver,"Tempest payment receipts total five without currency charges");
            p=ReloadBall(p);move(p,1011,212,196);
            Check(stage(p)==2 && (uint)Field(Call(store,null,"Load",p),"Soul")==soul && act(p,"Permit") && SavedRune(third,"stacksize")==2,"Tempest reload preserves paid reentry and frozen reward "+life);
            Query("DELETE FROM revora_tempest_payments WHERE uid=@uid","uid",(long)p.Entity.UID);bool missing=false;
            try{act(p,"Permit");}catch(InvalidOperationException){missing=true;}
            Check(missing && SavedRune(third,"stacksize")==2,"Tempest refuses corrupted permit without charging again");
        }
    }

    static void VerifyTempestEncounter(string runtime,Map town){
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsStore",true);
        var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsScenes",true);
        var combat=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsEncounters",true);
        Call(store,null,"Initialize");Call(scenes,null,"InitializeTravel");Call(store,null,"InitializeEncounter");
        World(runtime,1004);World(runtime,1011);string previous=Environment.CurrentDirectory;
        string root=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/tempest-route"));Directory.CreateDirectory(Path.Combine(root,"database/maps"));
        File.Copy(Path.Combine(runtime,"../../obj/chi-refill-validation/tempest-scene/server/database/maps/tempest-1100.map"),Path.Combine(root,"database/maps/tempest-1100.map"),true);
        File.Copy(Path.Combine(runtime,"database/maps/1100.map"),Path.Combine(root,"database/maps/1100.map"),true);
        var clock=store.GetField("Clock",Hidden);var old=clock.GetValue(null);DateTime now=new DateTime(2026,10,1,0,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Field(Call(store,null,"Load",p),"Stage");
        Func<GameState,string,bool> act=(p,name)=>{lock(p.ItemSyncRoot)return (bool)Call(store,null,name,p,(Func<bool>)(()=>true));};
        Action<GameState,ushort,ushort,ushort> move=(p,map,x,y)=>{p.BindWorldMap(map);p.Entity.X=x;p.Entity.Y=y;};
        try{Environment.CurrentDirectory=root;clock.SetValue(null,(Func<DateTime>)(()=>now));Call(scenes,null,"ValidateProfiles");
            byte[] correct=File.ReadAllBytes(Path.Combine(root,"database/maps/tempest-1100.map"));
            foreach(byte[] bad in new[]{new byte[7],new byte[8],new byte[]{1,0,0,0,1,0,0,0,2},new byte[]{255,255,255,127,255,255,255,127}}){
                File.WriteAllBytes(Path.Combine(root,"database/maps/tempest-invalid.map"),bad);int count=Kernel.Maps.Count;bool rejected=false;
                try{Call(typeof(Map),null,"CreateDynamicCompact",(ushort)1100,"maps\\tempest-invalid.map");}catch(InvalidDataException){rejected=true;}
                Check(rejected && Kernel.Maps.Count==count,"Tempest malformed compact floor rejected before map publication");
            }
            var compact=(Map)Call(typeof(Map),null,"CreateDynamicCompact",(ushort)1100,"maps\\tempest-1100.map");
            Check(Enumerable.Range(0,224*224).All(i=>compact.Floor[(ushort)(i%224),(ushort)(i/224),MapObjectType.Player]==(correct[8+i]==0)),"Tempest explicit compact path overrides unrelated public1100 for every terrain cell");compact.Dispose();

            foreach(byte life in new byte[]{0,1,2}){
                var p=ReloadBall(WelcomePlayer(town,100,life,15));object scene=null;
                try{
                    move(p,1004,51,49);Check(act(p,"Accept"),"Tempest encounter accepted "+life);move(p,1011,212,196);var scrolls=NestedPack(p,720027,5,false);
                    if(life==0)foreach(string table in new[]{"revora_tempest_permits","revora_tempest_payments"}){
                        Query("CREATE FUNCTION suppress_tempest_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                        Query("CREATE TRIGGER suppress_tempest_receipt BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_tempest_receipt()");
                        bool rejected=false;try{act(p,"Permit");}catch(InvalidOperationException){rejected=true;}finally{Query("DROP TRIGGER suppress_tempest_receipt ON "+table+";DROP FUNCTION suppress_tempest_receipt()");}
                        Check(rejected && stage(p)==1 && SavedRune(scrolls,"stacksize")==5,"Tempest suppressed "+table+" receipt rolls back full payment");
                    }
                    Check(act(p,"Permit"),"Tempest encounter paid "+life);
                    var legacy=NestedPack(p,3006392,1,true);Check(!act(p,"Claim") && p.Inventory.ContainsUID(legacy.UID),"Tempest historical feather cannot bypass Roc");
                    lock(p.ItemSyncRoot){scene=Call(scenes,null,"Create",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Tempest paid owned travel saves arrival");}
                    var map=(Map)Field(scene,"Map");Check(map.BaseID==1100 && map.Entities.Count==0 && map.Portals.Count==0 && map.Npcs.Count==1,"Tempest scene inherits no public monsters or portals");
                    var outsider=ReloadBall(WelcomePlayer(town,100,life,15));outsider.Entity.Teleport(map.ID,141,111);Check(outsider.Entity.MapID!=map.ID,"Tempest dynamic map rejects outsider teleport");
                    var body=(Entity)Call(combat,null,"Start",p);Check(body!=null && body.IsDropped && Call(combat,null,"Start",p)==null,"Tempest creates one owned Roc with legacy loot suppressed");
                    uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(outsider.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=outsider.Entity.UID,Attacked=body.UID},ref damage,null);
                    Check(!body.Dead && damage==0,"Tempest foreign damage cannot kill owned Roc");
                    if(life==0){now=now.AddMinutes(16);Call(scenes,null,"Tick");Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID),"Tempest timeout evacuates and releases the active Roc");}
                    else if(life==1){Kernel.GamePool.Remove(p.Entity.UID);Call(scenes,null,"Tick");Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1011 && !Kernel.Maps.ContainsKey(map.ID),"Tempest disconnected owner is returned durably before scene disposal");p=ReloadBall(p);}
                    else{p=ReloadBall(p);Call(scenes,null,"RecoverLogin",p);Call(scenes,null,"Tick");Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID),"Tempest replacement login retires old scene safely");}
                    scene=null;Check((bool)Call(combat,null,"BlockAttack",p.Entity,body) && stage(p)==2 && act(p,"Permit"),"Tempest interrupted fight cannot be reused and paid entry survives");
                    lock(p.ItemSyncRoot){scene=Call(scenes,null,"Create",p);Check((bool)Call(scenes,null,"Enter",scene,p,(Func<bool>)(()=>true)),"Tempest saved permit permits a fresh owned visit");}
                    map=(Map)Field(scene,"Map");body=(Entity)Call(combat,null,"Start",p);Check(body!=null,"Tempest fresh visit creates a new Roc");
                    var fillers=new System.Collections.Generic.List<uint>();while(p.Inventory.Count<40)fillers.Add(Pack(p,1000000,1).UID);
                    p.Entity.X=136;p.Entity.Y=105;damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
                    Check(body.Dead && stage(p)==3 && !Fenced(p) && CountId(p,3006392)==1 && Scalar("SELECT COUNT(*) FROM revora_tempest_kills WHERE uid=@uid",p.Entity.UID)==1,"Tempest real death saves full-bag entitlement without trusting legacy feather");
                    body.Die(p.Entity);Check(Scalar("SELECT COUNT(*) FROM revora_tempest_kills WHERE uid=@uid",p.Entity.UID)==1,"Tempest repeated death cannot duplicate kill receipt");
                    lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);Check(p.Entity.MapID==1011 && p.Entity.X==213 && p.Entity.Y==199 && !Kernel.Maps.ContainsKey(map.ID),"Tempest exit persists Phoenix return and releases actors/terrain");scene=null;
                    p=ReloadBall(p);Check(stage(p)==3 && !act(p,"Feather"),"Tempest full-bag earned feather survives reload pending delivery");
                    foreach(uint id in fillers.Take(2)){Query("DELETE FROM items WHERE uid=@uid","uid",(long)id);p.Inventory.Remove(id,Enums.ItemUse.None,true);}
                    if(life==0){
                        Query("CREATE FUNCTION suppress_tempest_feather() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_tempest_feather BEFORE INSERT ON revora_tempest_feathers FOR EACH ROW EXECUTE FUNCTION suppress_tempest_feather()");
                        bool rejected=false;try{act(p,"Feather");}catch(InvalidOperationException){rejected=true;}finally{Query("DROP TRIGGER suppress_tempest_feather ON revora_tempest_feathers;DROP FUNCTION suppress_tempest_feather()");}
                        Check(rejected && Scalar("SELECT feather_uid FROM revora_tempest_wings WHERE uid=@uid",p.Entity.UID)==0 && CountId(p,3006392)==1,"Tempest suppressed feather receipt rolls back item and keeps kill entitlement");
                    }
                    Check(act(p,"Feather") && !act(p,"Feather"),"Tempest saved kill delivers one tracked feather");
                    uint feather=checked((uint)Scalar("SELECT feather_uid FROM revora_tempest_wings WHERE uid=@uid",p.Entity.UID));
                    Query("DELETE FROM items WHERE uid=@uid","uid",(long)feather);p.Inventory.Remove(feather,Enums.ItemUse.None,true);
                    Check(act(p,"Feather") && Scalar("SELECT COUNT(*) FROM revora_tempest_feathers WHERE uid=@uid",p.Entity.UID)==2,"Tempest lost earned feather recovers with replacement receipt");
                    var full=Pack(p,1000000,1);Check(p.Inventory.Count==40 && !act(p,"Claim") && stage(p)==3,"Tempest full bag defers two-item reward without consumption");Query("DELETE FROM items WHERE uid=@uid","uid",(long)full.UID);p.Inventory.Remove(full.UID,Enums.ItemUse.None,true);
                    int calls=0;bool rollback=false;try{lock(p.ItemSyncRoot)Call(store,null,"Claim",p,(Func<bool>)(()=>++calls==1));}catch(InvalidOperationException){rollback=true;}
                    Check(rollback && stage(p)==3 && CountId(p,204008)==0 && CountId(p,3006392)==2,"Tempest late reward failure rolls back tracked consumption and both rewards");
                    Check(act(p,"Claim") && !act(p,"Claim") && stage(p)==4 && p.Inventory.ContainsUID(legacy.UID),"Tempest consumes only earned feather and claims once");
                    p=ReloadBall(p);Check(stage(p)==4 && CountId(p,204008)==1 && p.Inventory.Objects.Single(i=>i.ID==204008).Bound && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound)==1 && Scalar("SELECT array_length(rewards,1) FROM revora_tempest_wings WHERE uid=@uid",p.Entity.UID)==2,"Tempest bound Elite wing and bound P7 survive ordinary reload "+life);
                    Check(!act(p,"Permit") && !act(p,"Feather"),"Tempest completed quest cannot reenter or remint material");
                }finally{if(scene!=null)lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}
            }
        }finally{clock.SetValue(null,old);Environment.CurrentDirectory=previous;}
    }

    static void VerifyTempestPublic(string runtime,Map town){
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsQuest",true);
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsStore",true);
        var scenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.TempestWingsScenes",true);
        World(runtime,1004);World(runtime,1011);Call(quest,null,"Initialize");
        Check(!(bool)quest.GetProperty("Enabled",Hidden).GetValue(null,null) && (bool)Call(quest,null,"Protect",200u),"Tempest missing installed terrain keeps activation off and journal protected");
        string previous=Environment.CurrentDirectory,root=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/tempest-public"));Directory.CreateDirectory(Path.Combine(root,"database/maps"));
        File.Copy(Path.Combine(runtime,"../../obj/chi-refill-validation/tempest-scene/server/database/maps/tempest-1100.map"),Path.Combine(root,"database/maps/tempest-1100.map"),true);
        var clock=store.GetField("Clock",Hidden);var old=clock.GetValue(null);DateTime now=new DateTime(2026,10,2,0,0,0,DateTimeKind.Utc);
        Func<GameState,int> stage=p=>(int)Scalar("SELECT COALESCE(MAX(stage),0) FROM revora_tempest_wings WHERE uid=@uid",p.Entity.UID);
        Action<GameState,uint,byte> talk=(p,npc,option)=>{if(npc!=9910500)p.BindWorldMap(npc==8796?(ushort)1004:(ushort)1011);var n=p.Map.Npcs[npc];p.Entity.X=(ushort)(n.X+2);p.Entity.Y=n.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        Action tick=()=>{now=now.AddSeconds(2);Call(quest,null,"Tick");};
        Action<GameState> journal=p=>{var rows=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,rows);var own=rows.Where(r=>(uint)r.UID==200).ToArray();int st=stage(p);Check(own.Length==1 && own[0].Status==(st==4?MsgTaskStatus.QuestData.QuestStatus.Finished:st>0?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available) && !rows.Any(r=>(uint)r.UID==3429),"Tempest journal uses saved earning and omits unverified3429");};
        try{Environment.CurrentDirectory=root;clock.SetValue(null,(Func<DateTime>)(()=>now));Call(quest,null,"Initialize");Call(quest,null,"Initialize");
            Check(Kernel.Maps[1004].Npcs.Values.Count(n=>n.X==51 && n.Y==49)==1 && Kernel.Maps[1011].Npcs.Values.Count(n=>n.X==212 && n.Y==196)==1,"Tempest public initialization creates one Chang and one Wonder");
            var low=ReloadBall(WelcomePlayer(town,99,0,15));talk(low,8796,1);Check(stage(low)==0,"Tempest ordinary underlevel admission refused");
            foreach(byte life in new byte[]{0,1,2}){
                var p=ReloadBall(WelcomePlayer(town,life==0?(byte)100:(byte)15,life,new byte[]{15,55,165}[life]));object scene=null;p.LoadItemStats();
                try{
                    Check(p.Account.State==AccountTable.AccountState.Player,"Tempest fixture is an ordinary player "+life);journal(p);
                    foreach(uint id in new uint[]{200,3429}){var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)id,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);}
                    Check(stage(p)==0 && !p.Quests.HasQuest((QuestID)200) && !p.Quests.HasQuest((QuestID)3429),"Tempest forged journal cannot grant progress");
                    p.ActiveNpc=8796;Npcs.GetDialog(new NpcRequest(true){NpcID=8796,OptionID=1},p);Check(stage(p)==0,"Tempest remote NPC packet cannot accept");
                    talk(p,8796,1);talk(p,8796,1);Check(stage(p)==1,"Tempest real Chang starts once "+life);journal(p);
                    talk(p,10850,1);Check(p.Entity.MapID==1011 && stage(p)==1 && !Fenced(p),"Tempest Wonder refuses unpaid passage");
                    var scrolls=NestedPack(p,720027,8,false);uint cp=p.Entity.ConquerPoints;talk(p,10850,1);scene=Call(scenes,null,"Find",p);
                    Check(scene!=null && p.Map.BaseID==1100 && stage(p)==2 && scrolls.StackSize==3 && p.Entity.ConquerPoints==cp,"Tempest real Wonder charges five scrolls and enters private sky");
                    var map=p.Map;GeneralData.UsePortal(new Data(true){dwParam=190u|(162u<<16)},p);Check(p.Entity.MapID==map.ID,"Tempest forged distant portal cannot move player");
                    tick();var body=map.Entities.Values.Single();Check(body.MonsterInfo.ID==9905500 && body.Hitpoints==100000,"Tempest ordinary tick spawns reviewed Roc automatically");
                    talk(p,9910500,1);Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID) && stage(p)==2,"Tempest SkyKeeper returns unfinished hunter without losing permit");scene=null;
                    p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);talk(p,10850,1);scene=Call(scenes,null,"Find",p);tick();map=p.Map;body=map.Entities.Values.Single();
                    Check(stage(p)==2 && SavedRune(scrolls,"stacksize")==3,"Tempest public reentry after reload does not charge twice");
                    p.Entity.X=136;p.Entity.Y=105;uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);tick();
                    Check(stage(p)==3 && CountId(p,3006392)==1 && !Fenced(p),"Tempest real combat grants the owned feather");journal(p);
                    if(life==2){p.Entity.X=190;p.Entity.Y=162;GeneralData.UsePortal(new Data(true){dwParam=190u|(162u<<16)},p);Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID),"Tempest inherited portal returns only its nearby owner");}else talk(p,9910500,1);scene=null;
                    p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);uint feather=p.Inventory.Objects.Single(x=>x.ID==3006392).UID;Query("DELETE FROM items WHERE uid=@uid","uid",(long)feather);p.Inventory.Remove(feather,Enums.ItemUse.None,true);
                    talk(p,10850,2);talk(p,10850,2);Check(CountId(p,3006392)==1,"Tempest Wonder recovers one lost earned feather");talk(p,10850,3);talk(p,10850,3);
                    Check(stage(p)==4 && CountId(p,204008)==1 && p.Inventory.Objects.Single(x=>x.ID==204008).Bound && p.Inventory.Objects.Count(x=>x.ID>=800000 && x.ID<900000 && x.Bound)==1,"Tempest real Wonder grants one bound wing/P7 pair");journal(p);
                    p=ReloadBall(p);p.LoadItemStats();Call(quest,null,"OnLogin",p);journal(p);Check(stage(p)==4 && CountId(p,204008)==1 && Scalar("SELECT array_length(rewards,1) FROM revora_tempest_wings WHERE uid=@uid",p.Entity.UID)==2,"Tempest ordinary completion and rewards survive reload "+life);
                    p.ActiveNpc=600065;uint before=p.Entity.ConquerPoints;Npcs.GetDialog(new NpcRequest(true){NpcID=600065,OptionID=3},p);Check(p.Entity.ConquerPoints==before && p.Entity.MapID==1011,"Tempest legacy paid teleport stays retired");
                }finally{if(scene!=null && !Fenced(p))lock(p.ItemSyncRoot)Call(scenes,null,"Exit",scene,p);}
            }
        }finally{clock.SetValue(null,old);Environment.CurrentDirectory=previous;}
    }
}
