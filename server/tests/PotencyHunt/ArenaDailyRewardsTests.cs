using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
internal static partial class PotencyHuntTests
{
    static readonly Type ArenaDaily=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDailyRewards");
    static void VerifyArenaDaily(string runtime,Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=coordinator.GetField("ReturnClock",Hidden);var prior=clock.GetValue(null);
        // Existing non-timing fixtures run after the postmatch return window.
        clock.SetValue(null,new Func<Time32>(()=>Time32.Now.AddSeconds(6)));
        try { VerifyArenaDailyCore(runtime,town);VerifyTeamReturnWindow(town); }
        finally { clock.SetValue(null,prior); }
    }
    static void VerifyTeamReturnWindow(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=coordinator.GetField("ReturnClock",Hidden);var prior=clock.GetValue(null);
        foreach(bool wrap in new[]{false,true}) {
            var group=TeamCoordinatorGroup(town);var owners=TeamFixtureOwners(group);var arena=group.Player1.Map;
            try {
                if(wrap)group.DoneStamp=new Time32(uint.MaxValue-2000);
                var now=group.DoneStamp;
                clock.SetValue(null,new Func<Time32>(()=>now));
                Check(Call(coordinator,null,"Complete",group).ToString()=="Committed","team outcome persists before postmatch return: wrap="+wrap);
                var points=owners.Select(p=>p.ArenaPoints).ToArray();
                foreach(uint elapsed in new uint[]{0,4999}) {
                    now=new Time32(unchecked(group.DoneStamp.Value+elapsed));group.Export();
                    Check(Call(coordinator,null,"Cleanup",group).ToString()=="NotReady" && Kernel.Maps.ContainsKey(arena.ID)
                        && owners.All(p=>ReferenceEquals(p.TeamQualifierGroup,group) && ReferenceEquals(p.Map,arena)),
                        "committed team retains fighters and map until five seconds: elapsed="+elapsed+" wrap="+wrap);
                }
                now=new Time32(unchecked(group.DoneStamp.Value+5000));
                Check(Call(coordinator,null,"Cleanup",group).ToString()=="Committed" && !Kernel.Maps.ContainsKey(arena.ID)
                    && owners.All(p=>p.TeamQualifierGroup==null && p.Entity.MapID==town.ID),
                    "team returns and releases its map at exact five-second boundary: wrap="+wrap);
                Check(Call(coordinator,null,"Cleanup",group).ToString()=="AlreadyCommitted" && owners.Select(p=>p.ArenaPoints).SequenceEqual(points)
                    && owners.All(p=>Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==1),
                    "postmatch return retry preserves one saved result per ordinary fighter: wrap="+wrap);
            } finally { clock.SetValue(null,prior);CleanupTeamFixture(group); }
        }
    }
    static long HonorEvidence(GameState p) {
        return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10320",p.Entity.UID);
    }
    static void VerifyHonorAchievementReload(GameState p) {
        var restored=new Achievement(p.Entity);restored.Load("");
        Call(typeof(Achievement),restored,"Refresh",DateTime.UtcNow,true);
        int position=(10320/100-101)*32+10320%100-1;
        Check((System.BitConverter.ToUInt32(restored.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0
            && HonorEvidence(p)==1,"fresh achievement loader restores first-Honor bit from one durable receipt");
    }
    static void VerifyFirstNineLoss(Map town) {
        var group=StatGroup(town,false);var a=group.Player1;var b=group.Player2;
        for(int n=0;n<10;n++) {
            if(n>0){group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);}
            group.End(n==0?a:b);
            Check(CommitArenaStatistics(group),"first-nine loss fixture commits owned result: "+n);
        }
        Check(a.ArenaStatistic.TodayWin==9 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",a.Entity.UID)==0,
            "early loss followed by nine wins cannot earn first-nine achievement");
        group.Export();
        group=StatGroup(town);a=group.Player1;b=group.Player2;
        for(int n=1;n<=10;n++) {
            if(n>1){group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(b);}
            if(n==9)Query("UPDATE revora_arena_result_statistics SET ordering_verified=false WHERE uid=@uid","uid",(long)a.Entity.UID);
            Check(CommitArenaStatistics(group),"unverified-order fixture commits result: "+n);
        }
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",a.Entity.UID)==0,
            "legacy unverified ordering prevents first-nine completion even after ten wins");
        group.Export();
    }
    static void VerifyArenaClassVictories(Map town) {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS class integer NOT NULL DEFAULT 0");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var classes=new byte[]{15,25,145,135,45,55,65,75,85,165};
        var ids=new int[]{11101,11102,11103,11104,11105,11106,11107,11108,11109,11309};
        foreach(bool cold in new[]{false,true})for(int n=0;n<classes.Length;n++) {
            var group=StatGroup(town);var a=group.Player1;var b=group.Player2;b.Entity.Class=classes[n];
            var ordered=new[]{a,b}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(group,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
                Call(intents,null,"Capture",group,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Defeat"));
            b.Entity.Class=10; // A delayed settlement must retain the observed opponent.
            bool committed=cold?ColdArenaRecover(ArenaGuid(group)):Call(coordinator,null,"Complete",group).ToString()=="Committed";
            Check(committed && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id="+ids[n],a.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id="+ids[n],b.Entity.UID)==0,
                "captured class victory survives opponent change and credits only winner: "+classes[n]+" cold="+cold);
            var restored=new Achievement(a.Entity);restored.Load("");Call(typeof(Achievement),restored,"Refresh",DateTime.UtcNow,true);
            int position=(ids[n]/100-101)*32+ids[n]%100-1;
            Check((System.BitConverter.ToUInt32(restored.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0,
                "production achievement loader restores Arena class victory: "+ids[n]+" cold="+cold);
            Check(cold?!ColdArenaRecover(ArenaGuid(group)):Call(coordinator,null,"Complete",group).ToString()=="AlreadyCommitted",
                "class-victory result replay cannot settle twice");
            group.Export();
        }
        {
            var group=StatGroup(town);var a=group.Player1;group.Player2.Entity.Class=165;
            Query("CREATE FUNCTION suppress_class_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.source=''arena-class-victory'' THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_class_receipt BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_class_receipt()");
            string outcome;try{outcome=Call(coordinator,null,"Complete",group).ToString();}
            finally{Query("DROP TRIGGER suppress_class_receipt ON revora_achievement_evidence;DROP FUNCTION suppress_class_receipt()");}
            Check(outcome=="Failed" && LedgerRows(group,"active")==1
                && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",a.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11309",a.Entity.UID)==0,
                "failed class receipt rolls back live result, statistics and achievement together");
            Check(ColdArenaRecover(ArenaGuid(group)) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11309",a.Entity.UID)==1,
                "cold recovery restores class victory after failed live receipt");
            group.Export();
        }
        foreach(bool unknown in new[]{false,true}) {
            var group=StatGroup(town);var a=group.Player1;var b=group.Player2;b.Entity.Class=unknown?(byte)99:(byte)165;
            var ordered=new[]{a,b}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(group,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
                Call(intents,null,"Capture",group,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Defeat"));
            if(!unknown)Query("UPDATE revora_arena_outcome_snapshots SET opponent_class=NULL WHERE run=@run","run",ArenaGuid(group));
            Check(ColdArenaRecover(ArenaGuid(group)) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='arena-class-victory'",a.Entity.UID)==0,
                "unknown or legacy absent class grants no guessed achievement: unknown="+unknown);
            group.Export();
        }
    }
    static void VerifyFirstNinePending(Map town) {
        var group=StatGroup(town);var a=group.Player1;var b=group.Player2;Guid pending=Guid.NewGuid();
        // Fault-injected overlap: normal admission should prevent this state.
        Query("INSERT INTO revora_arena_matches(id,player1,player2,map,started_at,state) SELECT @pending,player1,player2,map,started_at-interval '1 minute','active' FROM revora_arena_matches WHERE id=@run",
            "pending",pending,"run",ArenaGuid(group));
        for(int n=1;n<=9;n++) {
            if(n>1){group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(b);}
            Check(CommitArenaStatistics(group),"pending-outcome fixture commits later victory: "+n);
        }
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",a.Entity.UID)==0,
            "unsettled overlapping fight cannot be skipped when nine later wins complete");
        Query("UPDATE revora_arena_matches SET state='finished',winner=player2,loser=player1,ended_at=started_at,day=(SELECT day FROM revora_arena_matches WHERE id=@run) WHERE id=@pending",
            "run",ArenaGuid(group),"pending",pending);
        foreach(string state in new[]{"missing","contradictory","loss"}) {
            if(state=="contradictory")Query("INSERT INTO revora_arena_result_statistics(run,uid,won,points_before,points_after,ordering_verified) VALUES(@run,@uid,true,1000,1000,true)","run",pending,"uid",(long)a.Entity.UID);
            if(state=="loss")Query("UPDATE revora_arena_result_statistics SET won=false WHERE run=@run","run",pending);
            lock(a.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=new Npgsql.NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE",c,t)){q.Parameters.AddWithValue("uid",(long)a.Entity.UID);q.ExecuteScalar();}
                string day;using(var q=new Npgsql.NpgsqlCommand("SELECT day FROM revora_arena_matches WHERE id=@run",c,t)){q.Parameters.AddWithValue("run",ArenaGuid(group));day=(string)q.ExecuteScalar();}
                Call(ArenaResults,null,"RecordFirstNine",c,t,a.Entity.UID,day);t.Commit();
            }}
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",a.Entity.UID)==0,
                "earlier outcome blocks first-nine completion: "+state);
        }
        group.Export();
    }
    static void VerifyArenaDailyCore(string runtime,Map town) {
        VerifyArenaResults(runtime,town);VerifyArenaCustom(town);Query("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");Call(ArenaDaily,null,"Initialize");VerifyArenaCoordinator(town);VerifyArenaDepartures(town);VerifyArenaTimeouts(town);VerifyArenaCleanup(town);VerifyArenaSpectators(town);VerifyArenaStartup(town);
        VerifyArenaClassVictories(town);VerifyFirstNineLoss(town);VerifyFirstNinePending(town);
        var group=StatGroup(town);var winner=group.Player1;var loser=group.Player2;
        for(int match=1;match<=21;match++) {
            if(match>1){group.Export();group=new Group(winner,loser);group.Import();Call(ArenaLedger,null,"Start",group);group.End(loser);}
            if(match==9) {
                Query("CREATE FUNCTION suppress_arena_entitlement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_arena_entitlement BEFORE INSERT ON revora_arena_daily_rewards FOR EACH ROW EXECUTE FUNCTION suppress_arena_entitlement()");
                bool rejected=false;try{CommitArenaStatistics(group,true,true);}catch(InvalidOperationException){rejected=true;}
                finally{Query("DROP TRIGGER suppress_arena_entitlement ON revora_arena_daily_rewards;DROP FUNCTION suppress_arena_entitlement()");}
                Check(rejected && LedgerRows(group,"active")==1 && winner.ArenaStatistic.TodayWin==8
                    && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",winner.Entity.UID)==0,
                    "failed milestone receipt rolls back the ninth result and statistic changes");
            }
            Check(CommitArenaStatistics(group,true,true),"registered Arena result commits daily evidence: "+match);
            if(match==8)Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",winner.Entity.UID)==0,"eight wins cannot mint the nine-win reward");
            if(match==9)Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",winner.Entity.UID)==1,"ninth verified win records one daily entitlement");
            if(match==8 || match==9 || match==21)Check(
                Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",winner.Entity.UID)==(match<9?0:1)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",loser.Entity.UID)==0,
                "first-nine achievement requires nine wins and remains one receipt: "+match);
            if(match==19)Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",loser.Entity.UID)==0,"nineteen losses cannot mint the twenty-match reward");
        }
        Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",winner.Entity.UID)==2
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",loser.Entity.UID)==1,"twenty matches grant both milestones once to winner and participation once to loser");
        Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND rune_type=4030101",winner.Entity.UID)==2,
            "earned solo milestones freeze a bound combat rune without adding daily P7");
        Query("UPDATE revora_arena_daily_rewards SET rune_type=0 WHERE uid=@uid","uid",(long)loser.Entity.UID);
        winner.Entity.Vitality=100;uint wardrobe=StoredQuestGarment(winner);
        while(winner.Inventory.Count<40)Pack(winner,1088001,1);Call(ArenaDaily,null,"Pay",winner);
        Check(winner.CurrentHonor==0 && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==2,"full inventory retains Honor and item together as pending rewards");
        Check(HonorEvidence(winner)==0 && HonorEvidence(loser)==0,"unpaid Arena entitlements do not grant first-Honor achievement");
        foreach(var item in winner.Inventory.Objects.ToArray())DisRemove(winner,item);
        Query("CREATE FUNCTION suppress_honor_achievement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=10320 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_honor_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_honor_achievement()");
        try{Call(ArenaDaily,null,"Pay",winner);}finally{Query("DROP TRIGGER suppress_honor_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_honor_achievement()");}
        Check(Fenced(winner) && HonorEvidence(winner)==0 && winner.Inventory.Count==0
            && Scalar("SELECT COALESCE(currenthonor,0) FROM arena WHERE entityid=@uid",winner.Entity.UID)==0
            && Scalar("SELECT COALESCE(currenthonor,0) FROM teamarena WHERE entityid=@uid",winner.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==2,
            "failed first-Honor achievement rolls back payment, item and daily paid receipt");
        DemonReset(winner);
        Query("CREATE FUNCTION suppress_arena_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_arena_prize BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_arena_prize()");
        try{Call(ArenaDaily,null,"Pay",loser);}finally{Query("DROP TRIGGER suppress_arena_prize ON items;DROP FUNCTION suppress_arena_prize()");}
        Check(Fenced(loser) && loser.CurrentHonor==0 && loser.Inventory.Count==0
            && Scalar("SELECT COALESCE(currenthonor,0) FROM arena WHERE entityid=@uid",loser.Entity.UID)==0
            && Scalar("SELECT COALESCE(currenthonor,0) FROM teamarena WHERE entityid=@uid",loser.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",loser.Entity.UID)==1,
            "failed EXP Ball insert rolls back both Honor mirrors and keeps the entitlement pending");
        Check(HonorEvidence(loser)==0,"failed daily item delivery cannot grant first-Honor achievement");
        // Reset only the fixture fence to exercise retry after the rolled-back write.
        DemonReset(loser);
        while(winner.Inventory.Count<39)Pack(winner,1088001,1);Call(ArenaDaily,null,"Pay",winner);
        Check(winner.CurrentHonor==0 && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==2,
            "one free slot cannot partially deliver the solo EXP Ball and rune bundle");
        foreach(var filler in winner.Inventory.Objects.ToArray())DisRemove(winner,filler);
        Query("CREATE FUNCTION reject_arena_rune() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=4030101 THEN RAISE EXCEPTION ''fixture rune insert''; END IF; RETURN NEW; END';CREATE TRIGGER reject_arena_rune BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION reject_arena_rune()");
        try{Call(ArenaDaily,null,"Pay",winner);}finally{Query("DROP TRIGGER reject_arena_rune ON items;DROP FUNCTION reject_arena_rune()");}
        Check(Fenced(winner) && winner.Inventory.Count==0 && Scalar("SELECT COALESCE(currenthonor,0) FROM arena WHERE entityid=@uid",winner.Entity.UID)==0
            && Scalar("SELECT COALESCE(currenthonor,0) FROM teamarena WHERE entityid=@uid",winner.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==2,
            "rune insert failure rolls back EXP Ball, both Honor mirrors and solo entitlement");
        DemonReset(winner);
        Parallel.For(0,8,n=>Call(ArenaDaily,null,"Pay",winner));Call(ArenaDaily,null,"Pay",loser);
        Check(winner.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==2 && loser.Inventory.Objects.All(i=>i.ID!=4030101),
            "new solo milestones grant bound level-one runes while legacy entitlements retain their EXP Ball only");
        Check(!Fenced(winner) && !Fenced(loser) && winner.CurrentHonor==1000 && winner.HistoryHonor==1000 && loser.CurrentHonor==500
            && winner.Inventory.Objects.Count(i=>i.ID==723700)==2 && loser.Inventory.Objects.Count(i=>i.ID==723700)==1,
            "concurrent delivery pays earned Honor and EXP Balls exactly once");
        Check(HonorEvidence(winner)==1 && HonorEvidence(loser)==1,"concurrent daily payouts record one first-Honor achievement per recipient");
        VerifyHonorAchievementReload(winner);VerifyHonorAchievementReload(loser);
        foreach(var p in new[]{winner,loser}) {
            var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(saved.CurrentHonor==p.CurrentHonor && saved.HistoryHonor==p.HistoryHonor
                && p.Inventory.Objects.All(i=>ConquerItemTable.LoadItem(i.UID).ID==i.ID && ConquerItemTable.LoadItem(i.UID).Bound==i.Bound),"production statistic and item loaders preserve delivered daily prizes");
        }
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"earned daily Arena delivery preserves stored wardrobe design");
        var nextDay=DateTime.UtcNow.AddDays(1);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>nextDay));
        try {
            for(int match=0;match<9;match++) {
                group.Export();group=new Group(winner,loser);group.Import();Call(ArenaLedger,null,"Start",group);group.End(loser);
                if(!CommitArenaStatistics(group,true,true))throw new Exception("New-day result was rejected.");
            }
            Check(winner.ArenaStatistic.TodayWin==9 && winner.ArenaStatistic.TodayBattles==9
                && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",winner.Entity.UID)==3,
                "next Pacific day earns its own nine-win entitlement without replaying yesterday's prizes");
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
        VerifyArenaDelivery(winner);
        VerifyArenaSweep(town);VerifyArenaRanking(town);VerifyArenaRollover(town);VerifyArenaStartupPreparation(town);VerifyArenaSaveOwnership(town);VerifyArenaPointPurchases(town);VerifyArenaLoginRecovery(town);VerifyArenaCacheFailure(town);VerifyArenaSessionRelease(town);VerifyArenaDeferredDeparture(town);VerifyArenaFencedRecovery(town);VerifyArenaCaptureRecovery(town);VerifyArenaCompletionPump(town);VerifyArenaRolloverIdleOwnership(town);VerifyLegacyTeamResultBounds(town);VerifyTeamOutcomeOwnership(town);VerifyTeamAdmittedDamage(town);VerifyTeamResultTransactions(town);VerifyTeamStartupRecovery(town);VerifyTeamStartupSweep(town);VerifyArenaCrossModeCapture(town);VerifyTeamCoordinator(town);VerifyTeamRollover(town);VerifyTeamEndCoordinator(town);VerifyTeamPendingDeparture(town);VerifyTeamCompletionPump(town);VerifyJointArenaStartup(town);VerifyTeamAdmissions(town);VerifyTeamInterruptedTransfer(town);VerifyTeamMixedRecovery(town);VerifyTeamDisconnectObservation(town);VerifyTeamShutdownRetention(town);VerifySoloShutdownRetention(town);VerifyAcceptedArenaMidnight(town);VerifyScheduledArenaRanking(town);VerifyArenaShutdownDrain(town);VerifyArenaAdminBalances(town);
    }
    static void VerifyArenaAdminBalances(Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaAdminBalances");Call(service,null,"Initialize");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var group=StatGroup(town);var p=group.Player1;
        Check(Call(coordinator,null,"Complete",group).ToString()=="Committed","administrator balance fixture has a managed saved result");group.Export();
        uint points=p.ArenaPoints,honor=p.CurrentHonor,history=p.HistoryHonor;
        try {
            foreach(var state in new[]{AccountTable.AccountState.Player,AccountTable.AccountState.GameMaster}) {
                p.Account.State=state;
                Check(!(bool)Call(service,null,"Apply",p,false,3000u) && !(bool)Call(service,null,"Apply",p,true,50u)
                    && p.ArenaPoints==points && p.CurrentHonor==honor,
                    "Arena balance service rejects an unauthorized caller: "+state);
            }
            p.Account.State=AccountTable.AccountState.Administrador;
            Check((bool)Call(service,null,"Apply",p,false,3000u) && p.ArenaPoints==3000 && p.TeamArenaStatistic.ArenaPoints==3000
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==3000
                && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==3000,
                "administrator point setter commits both managed mirrors");
            Check((bool)Call(service,null,"Apply",p,true,50u) && p.CurrentHonor==honor+50 && p.HistoryHonor==history
                && ((QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID)).CurrentHonor==honor+50
                && ((TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID)).CurrentHonor==honor+50,
                "administrator Honor addition reloads from both rows without changing historical Honor");
            Parallel.For(0,4,n=>Call(service,null,"Apply",p,true,1u));
            Check(p.CurrentHonor==honor+54 && Scalar("SELECT count(*) FROM revora_arena_admin_balances WHERE uid=@uid",p.Entity.UID)==6,
                "concurrent explicit administrator requests serialize with one audit row per committed change");
            Check(!(bool)Call(service,null,"Apply",p,true,uint.MaxValue) && !Fenced(p) && p.CurrentHonor==honor+54,
                "administrator Honor addition rejects overflow");
            Qualifier.PlayerList[p.Entity.UID]=p;
            try{Check(!(bool)Call(service,null,"Apply",p,false,0u),"queued administrator cannot rewrite competitive points");}
            finally{Qualifier.PlayerList.Remove(p.Entity.UID);}
            foreach(string table in new[]{"teamarena","revora_arena_admin_balances"}) {
                Query("CREATE FUNCTION suppress_admin_balance() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_admin_balance BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_admin_balance()");
                try{Check(!(bool)Call(service,null,"Apply",p,true,100u) && Fenced(p),"administrator balance write failure fences the cache: "+table);}
                finally{Query("DROP TRIGGER suppress_admin_balance ON "+table+";DROP FUNCTION suppress_admin_balance()");}
                Check(p.CurrentHonor==honor+54 && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",p.Entity.UID)==honor+54
                    && Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",p.Entity.UID)==honor+54
                    && Scalar("SELECT count(*) FROM revora_arena_admin_balances WHERE uid=@uid",p.Entity.UID)==6,
                    "mirror or audit failure rolls back the complete administrator change: "+table);
                p.ArenaStatistic=(QualifierStatistic)Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID);p.TeamArenaStatistic=(TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID);DemonReset(p);
            }
            Check((bool)Call(service,null,"Apply",p,true,1u) && p.CurrentHonor==honor+55,
                "administrator adjustment succeeds after production cache recovery");
        } finally {p.Account.State=AccountTable.AccountState.Player;}
        var active=StatGroup(town,false);var actor=active.Player1;actor.Account.State=AccountTable.AccountState.Administrador;
        try {
            Check(!(bool)Call(service,null,"Apply",actor,false,1u),"administrator cannot alter an active Arena result");
            actor.QualifierGroup=null;
            try{Check(!(bool)Call(service,null,"Apply",actor,true,1u)
                && Scalar("SELECT count(*) FROM revora_arena_admin_balances WHERE uid=@uid",actor.Entity.UID)==0,
                "saved pending match blocks administrator changes even when its session reference is missing");}
            finally{actor.QualifierGroup=active;}
        }finally{actor.Account.State=AccountTable.AccountState.Player;active.End(active.Player2);Call(coordinator,null,"Complete",active);active.Export();}
    }
    static void VerifyArenaShutdownDrain(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaShutdownDrain");
        var startup=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupPreparation");
        var prepared=startup.GetField("<Prepared>k__BackingField",Hidden);var prior=prepared.GetValue(null);
        var stopping=type.GetField("stopping",Hidden);var work=type.GetField("work",Hidden);
        var restore=new System.Collections.Generic.List<Action>();
        Action<Type,string> isolate=(owner,name)=>{
            var field=owner.GetField(name,Hidden|System.Reflection.BindingFlags.Public);var old=field.GetValue(null);
            field.SetValue(null,Activator.CreateInstance(field.FieldType));restore.Add(()=>field.SetValue(null,old));
        };
        Action finish=()=>{var task=work.GetValue(null) as Task;if(task!=null && !task.Wait(5000))throw new Exception("Shutdown fixture worker did not stop.");};
        try {
            isolate(typeof(Kernel),"GamePool");isolate(typeof(Kernel),"DisconnectPool");
            foreach(var owner in new[]{typeof(Qualifier),typeof(TeamQualifier)}){isolate(owner,"PlayerList");isolate(owner,"WaitingPlayerList");}
            isolate(typeof(Qualifier.QualifierList),"Groups");isolate(typeof(TeamQualifier.QualifierList),"Groups");
            prepared.SetValue(null,false);
            Check((bool)Call(type,null,"TryDrain",20) && (int)stopping.GetValue(null)==0,
                "unprepared Arena shutdown leaves legacy admission untouched");
            prepared.SetValue(null,true);
            using(var entry=(IDisposable)Call(type,null,"Enter")) {
                Check(!(bool)Call(type,null,"TryDrain",30),"shutdown waits for an already running admission even with empty registries");
                finish();
                Check(Call(type,null,"Enter")==null,"shutdown closes subsequent admission leases");
            }
            stopping.SetValue(null,0);work.SetValue(null,null);
            var solo=StatGroup(town,false,false);var team=TeamCoordinatorGroup(town,false,false);
            var soloType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDisconnectRetention");
            var teamType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
            Action<GameState,bool> resume=(p,requested)=>{throw new Exception("Shutdown must not fabricate departures.");};
            var soloOwner=Activator.CreateInstance(soloType,Instance,null,new object[]{2,resume},null);
            var teamOwner=Activator.CreateInstance(teamType,Instance,null,new object[]{2,resume},null);
            Call(typeof(Qualifier),null,"ImportMatched",solo,soloOwner);Call(typeof(TeamQualifier),null,"ImportMatched",team,teamOwner);
            Check(solo.Player1.QualifierGroup==solo && team.Player1.TeamQualifierGroup==team,"shutdown fixtures enter owned solo and team fights");
            bool soloTickVisible=false,teamTickVisible=false;
            var soloPump=Field(soloOwner,"pump");var teamPump=Field(teamOwner,"pump");
            soloPump.GetType().GetField("Clock",Instance).SetValue(soloPump,new Func<DateTime>(()=>{
                soloTickVisible=(bool)soloType.GetProperty("HasScheduled",Hidden).GetValue(null,null);return DateTime.UtcNow;
            }));
            teamPump.GetType().GetField("Clock",Instance).SetValue(teamPump,new Func<DateTime>(()=>{
                teamTickVisible=(bool)teamType.GetProperty("HasScheduled",Hidden).GetValue(null,null);return DateTime.UtcNow;
            }));
            var queued=PointBuyer(town);Qualifier.PlayerList[queued.Entity.UID]=queued;
            Check(!(bool)Call(type,null,"TryDrain",40),"operator drain remains pending while ordinary fights are active");finish();
            Check(soloTickVisible && teamTickVisible,"scheduled completion stays visible while dequeued and executing");
            Check(!solo.Done && !team.Done && LedgerRows(solo,"active")==1
                && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",solo.Player1.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",team.Player1.Entity.UID)==0,
                "shutdown alone does not select a winner or commit a solo result");
            Check(!Qualifier.PlayerList.ContainsKey(queued.Entity.UID),"shutdown removes queued non-fighters");
            Qualifier.QualifyEngine.DoSignup(queued);TeamQualifier.QualifyEngine.DoSignup(queued);
            solo.BeginWatching(queued);team.BeginWatching(queued);
            Check(!Qualifier.PlayerList.ContainsKey(queued.Entity.UID) && !TeamQualifier.PlayerList.ContainsKey(queued.Entity.UID)
                && queued.WatchingGroup==null && queued.TeamWatchingGroup==null,"closed admissions reject ordinary signup and spectators in both modes");
            solo.End(solo.Player2);team.End(team.Player2);team.Export();
            Check((int)Field(team,"DurableResultReady")==0 && team.Player1.TeamQualifierGroup==team,
                "public team export cannot bypass retained result completion even after the return window");
            AdvanceOwnedPump(soloOwner);AdvanceOwnedPump(teamOwner);
            lock(solo.Player1.ItemSyncRoot) {
                Check(!(bool)Call(type,null,"TryDrain",40),"a busy result owner prevents successful shutdown");finish();
                Check(LedgerRows(solo,"active")==1,"busy completion leaves the solo result pending");
            }
            AdvanceOwnedPump(soloOwner);AdvanceOwnedPump(teamOwner);
            bool drained=false;
            for(int n=0;n<8 && !drained;n++) {
                var retryTime=DateTime.UtcNow.AddSeconds(10+n*2);
                foreach(var owner in new[]{soloOwner,teamOwner}) {
                    var pump=Field(owner,"pump");pump.GetType().GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>retryTime));
                }
                drained=(bool)Call(type,null,"TryDrain",100);finish();
            }
            Check(drained && LedgerRows(solo,"finished")==1 && solo.Player1.QualifierGroup==null && team.Player1.TeamQualifierGroup==null,
                "retry drains saved solo/team results and releases their owned maps and sessions"
                +" drained="+drained+" solo="+LedgerRows(solo,"finished")+" soloGroups="+Qualifier.QualifierList.Groups.Count
                +" teamGroups="+TeamQualifier.QualifierList.Groups.Count+" soloScheduled="+soloType.GetProperty("HasScheduled",Hidden).GetValue(null,null)
                +" teamScheduled="+teamType.GetProperty("HasScheduled",Hidden).GetValue(null,null));
            var points=solo.Player1.ArenaPoints;
            Check((bool)Call(type,null,"TryDrain",20) && solo.Player1.ArenaPoints==points
                && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",solo.Player1.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",team.Player1.Entity.UID)==1,"successful shutdown retry cannot duplicate the saved reward");
        } finally {
            finish();stopping.SetValue(null,0);work.SetValue(null,null);prepared.SetValue(null,prior);
            for(int n=restore.Count-1;n>=0;n--)restore[n]();
        }
    }
    static void VerifyAcceptedArenaMidnight(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches,revora_potency_validation.revora_arena_rank_days CASCADE");
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);
        var queued=Qualifier.PlayerList;var waiting=Qualifier.WaitingPlayerList;
        Qualifier.PlayerList=new System.Collections.Concurrent.ConcurrentDictionary<uint,GameState>();
        Qualifier.WaitingPlayerList=new System.Collections.Concurrent.ConcurrentDictionary<uint,GameState>();
        var now=new DateTime(2064,6,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            foreach(bool fault in new[]{false,true}) {
                var pair=new[]{PointBuyer(town),PointBuyer(town)};
                foreach(var p in pair) {
                    p.Entity.Level=140;p.ArenaStatistic.AcceptBox=false;p.ArenaState=Qualifier.FindMatch;
                    p.Account=new AccountTable(null){EntityID=p.Entity.UID,State=AccountTable.AccountState.Player};
                    Qualifier.QualifyEngine.DoSignup(p);
                    p.ArenaStatistic.TodayWin=7;p.ArenaStatistic.TodayBattles=9;
                    Query("UPDATE arena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);
                }
                Qualifier.EngagePlayers();var a=pair[0];var b=pair[1];
                Check(ReferenceEquals(a.ArenaStatistic.PlayWith,b) && ReferenceEquals(b.ArenaStatistic.PlayWith,a),
                    "ordinary queue creates a reciprocal invitation before midnight: "+fault);
                foreach(var p in pair){p.ArenaStatistic.AcceptBox=true;p.ArenaState=Qualifier.WaitForOther;}
                now=now.AddDays(1);
                if(!fault) {
                    var stranger=PointBuyer(town);b.ArenaStatistic.PlayWith=stranger;
                    Qualifier.VerifyAwaitingPeople();
                    Check(a.QualifierGroup==null && b.QualifierGroup==null && a.ArenaStatistic.TodayWin==7,
                        "accepted rollover rejects a replaced reciprocal invitation before resetting either player");
                    b.ArenaStatistic.PlayWith=a;
                    using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                        var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                        try{Qualifier.VerifyAwaitingPeople();
                            Check(a.QualifierGroup==null && a.ArenaStatistic.TodayWin==7 && b.ArenaStatistic.TodayWin==7,
                                "busy accepted participant delays midnight preparation without a partial reset or map transfer");
                        }finally{release.Set();task.Wait();}
                    }
                }
                if(fault)Query("CREATE FUNCTION suppress_accepted_day() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_accepted_day BEFORE INSERT ON revora_arena_day_rollovers FOR EACH ROW EXECUTE FUNCTION suppress_accepted_day()");
                try{Qualifier.VerifyAwaitingPeople();}
                finally{if(fault)Query("DROP TRIGGER suppress_accepted_day ON revora_arena_day_rollovers;DROP FUNCTION suppress_accepted_day()");}
                if(fault) {
                    Check(pair.All(p=>p.QualifierGroup==null && p.Entity.MapID==1002 && p.ArenaStatistic.TodayWin==7)
                        && pair.Any(Fenced) && pair.All(p=>Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",p.Entity.UID)==7),
                        "failed accepted-day receipt preserves saved counters and prevents either player entering a match");
                }else {
                    var group=a.QualifierGroup;
                    Check(group!=null && ReferenceEquals(group,b.QualifierGroup) && LedgerRows(group,"active")==1
                        && pair.All(p=>p.ArenaStatistic.TodayWin==0 && p.ArenaStatistic.TodayBattles==0
                            && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==2),
                        "accepted invitation crossing midnight resets both daily counters before registered entry");
                    group.End(b);RunOwnedArenaScheduler(false);
                    Check((bool)Field(group,"CleanupCompleted") && a.ArenaStatistic.TodayWin==1 && a.ArenaStatistic.TodayBattles==1
                        && b.ArenaStatistic.TodayWin==0 && b.ArenaStatistic.TodayBattles==1,
                        "first completed fight after a waiting invitation counts only toward the new day");
            var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
                    Check(saved.TodayWin==1 && saved.TodayBattles==1,"normal reload preserves the post-midnight result rather than yesterday's counters");
                }
                Qualifier.PlayerList.Clear();Qualifier.WaitingPlayerList.Clear();now=now.AddDays(8);
            }
        }finally{Qualifier.PlayerList=queued;Qualifier.WaitingPlayerList=waiting;clock.SetValue(null,prior);}
    }

    static void VerifyScheduledArenaRanking(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches,revora_potency_validation.revora_arena_rank_days CASCADE");
        var rank=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRankingSettlement");
        var startup=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupPreparation");
        var prepared=startup.GetField("<Prepared>k__BackingField",Hidden);var wasPrepared=prepared.GetValue(null);
        var clock=ArenaLedger.GetField("Clock",Hidden);var originalClock=clock.GetValue(null);
        var probe=rank.GetField("lastProbe",Hidden);var originalProbe=probe.GetValue(null);
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var now=new DateTime(2063,6,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            var first=StatGroup(town);
            Check(Call(coordinator,null,"Complete",first).ToString()=="Committed","scheduled ranking fixture earns its first result through the coordinator");first.Export();
            var pending=StatGroup(town,false);now=now.AddDays(1);
            prepared.SetValue(null,false);probe.SetValue(null,default(DateTime));
            Check(!(bool)Call(rank,null,"Tick") && Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==0,
                "ranking scheduler cannot activate before joint startup preparation");
            prepared.SetValue(null,true);RunOwnedArenaScheduler(false);
            Check(Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==0 && LedgerRows(pending,"active")==1,
                "real match scheduler leaves yesterday open while an admitted outcome is pending");
            pending.End(pending.Player2);
            Check(Call(coordinator,null,"Complete",pending).ToString()=="Committed","pending cross-midnight result commits before ranking retry");pending.Export();
            Check(!(bool)Call(rank,null,"Tick") && Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==0,
                "failed ranking probe is throttled until its next interval");
            now=now.AddSeconds(30);
            Query("CREATE FUNCTION suppress_scheduled_rank() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_scheduled_rank BEFORE INSERT ON revora_arena_rank_rewards FOR EACH ROW EXECUTE FUNCTION suppress_scheduled_rank()");
            try {RunOwnedArenaScheduler(false);
                Check(Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==0 && !Fenced(first.Player1),
                    "scheduled ranking write failure rolls back the day seal without invalidating player caches");
            }finally{Query("DROP TRIGGER suppress_scheduled_rank ON revora_arena_rank_rewards;DROP FUNCTION suppress_scheduled_rank()");}
            now=now.AddSeconds(30);int probes=0;
            Parallel.For(0,12,n=>{if((bool)Call(rank,null,"Tick"))System.Threading.Interlocked.Increment(ref probes);});
            Check(probes==1 && Scalar("SELECT count(*) FROM revora_arena_rank_days WHERE day='20630601'",0)==1
                && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE day='20630601'",0)==2,
                "concurrent scheduled retry seals yesterday once and excludes the result completed today");
            var p=first.Player1;p.Entity.Level=140;p.Account=new AccountTable(null){EntityID=p.Entity.UID,State=AccountTable.AccountState.Player};
            Qualifier.QualifyEngine.DoSignup(p);
            Check(Qualifier.PlayerList.ContainsKey(p.Entity.UID) && p.ArenaStatistic.LastSeasonRank>0 && p.ArenaStatistic.TodayWin==0,
                "automatic ranking settlement unblocks ordinary next-day signup and projects its saved previous rank");
            Qualifier.PlayerList.Remove(p.Entity.UID);p.ArenaStatistic.Status=Revora.Network.GamePackets.QualifierStatistic.NotSignedUp;
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRewardDelivery"),null,"Tick",p);
            Check(p.CurrentHonor==1000000 && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE uid=@uid AND paid_at IS NOT NULL",p.Entity.UID)==1,
                "scheduled ranking entitlement reaches the ordinary reward delivery service exactly once");
            now=now.AddDays(1);RunOwnedArenaScheduler(false);
            Check(Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==2 && Scalar("SELECT count(*) FROM revora_arena_rank_rewards",0)==4,
                "later scheduler passes settle the next completed day without rewriting earlier rewards");
            now=now.AddDays(-2);
            Check((bool)Call(rank,null,"Tick") && !(bool)Call(rank,null,"Tick")
                && Scalar("SELECT count(*) FROM revora_arena_rank_rewards",0)==4,
                "clock rewind allows one probe without replaying sealed ranking rewards");
        }finally{clock.SetValue(null,originalClock);probe.SetValue(null,originalProbe);prepared.SetValue(null,wasPrepared);}
    }

    static void VerifyArenaCustom(Map town) {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS viplevel integer NOT NULL DEFAULT 0");
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCustomRewards");Call(feature,null,"Initialize");
        var intentsType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");Call(intentsType,null,"Initialize");
        byte oldMin=Config.ArenaHundredWinsMinVip;bool oldEnabled=Config.ArenaFiftyLossesPenaltyEnabled;uint oldCp=Config.ArenaHundredWinsCp;
        try {
            Config.ArenaHundredWinsMinVip=5;Config.ArenaFiftyLossesPenaltyEnabled=true;Config.ArenaHundredWinsCp=5000;
            var group=StatGroup(town);var winner=group.Player1;var loser=group.Player2;
            winner.Entity.VIPLevel=5;loser.Entity.VIPLevel=0;
            Query("UPDATE entities SET viplevel=5 WHERE uid=@uid","uid",(long)winner.Entity.UID);
            Query("UPDATE entities SET viplevel=0 WHERE uid=@uid","uid",(long)loser.Entity.UID);
            for(int match=1;match<=101;match++) {
                if(match>1){group.Export();group=new Group(winner,loser);group.Import();Call(ArenaLedger,null,"Start",group);group.End(loser);}
                if(match==100) {
                    var players=new[]{winner,loser}.OrderBy(p=>p.Entity.UID).ToArray();
                    lock(Field(group,"ParticipationSync"))lock(players[0].ItemSyncRoot)lock(players[1].ItemSyncRoot)
                        Call(intentsType,null,"Capture",group,Enum.Parse(intentsType.GetNestedType("Cause",Hidden),"Defeat"));
                    Config.ArenaHundredWinsCp=123;Config.ArenaHundredWinsMinVip=6;winner.Entity.VIPLevel=0;
                    Query("UPDATE entities SET viplevel=0 WHERE uid=@uid","uid",(long)winner.Entity.UID);
                    Query("CREATE FUNCTION suppress_custom_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_custom_prize BEFORE INSERT ON revora_arena_custom_rewards FOR EACH ROW EXECUTE FUNCTION suppress_custom_prize()");
                    bool rejected=false;try{CommitArenaStatistics(group,true,false,true);}catch(InvalidOperationException){rejected=true;}
                    finally{Query("DROP TRIGGER suppress_custom_prize ON revora_arena_custom_rewards;DROP FUNCTION suppress_custom_prize()");}
                    Check(rejected && LedgerRows(group,"active")==1 && winner.ArenaStatistic.TodayWin==99
                        && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid",winner.Entity.UID)==0,"custom receipt failure rolls back hundredth result and counters");
                }
                if(!CommitArenaStatistics(group,true,false,true))throw new Exception("Custom reward match rejected.");
                if(match==49)Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid",loser.Entity.UID)==0,"forty-nine losses cannot earn the custom milestone");
                if(match==50) {
                    Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND milestone='losses50' AND NOT eligible AND recipient_vip=0",loser.Entity.UID)==1,
                        "loss eligibility uses the loser's VIP, not the winner's VIP");
                    loser.Entity.VIPLevel=5;Query("UPDATE entities SET viplevel=5 WHERE uid=@uid","uid",(long)loser.Entity.UID);
                }
                if(match==99)Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid",winner.Entity.UID)==0,"ninety-nine verified wins cannot earn a hundred-win prize");
                if(match==100) {
                    Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND eligible AND honor=500 AND cp=5000 AND cardinality(item_types)=6",winner.Entity.UID)==1,
                        "hundredth result uses captured VIP and economy despite changes before bundle retry");
                    Config.ArenaHundredWinsCp=123;
                }
            }
            Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND cp=5000",winner.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND NOT eligible",loser.Entity.UID)==1,
                "later matches, configuration and VIP changes cannot rewrite frozen decisions");
            Check(winner.CurrentHonor==0 && winner.Inventory.Count==0,"inactive entitlement participant does not run legacy or new payout");
            Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS conquerpoints bigint NOT NULL DEFAULT 0;ALTER TABLE entities ADD COLUMN IF NOT EXISTS revorapoint bigint NOT NULL DEFAULT 0;ALTER TABLE entities ADD COLUMN IF NOT EXISTS money bigint NOT NULL DEFAULT 0;CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
            uint beforeCp=winner.Entity.ConquerPoints,beforeRvp=winner.Entity.RevoraPoint;ulong beforeSilver=winner.Entity.Money;
            Query("UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@money WHERE uid=@uid","cp",(long)beforeCp,"rvp",(long)beforeRvp,"money",(long)beforeSilver,"uid",(long)winner.Entity.UID);
            winner.Entity.Vitality=100;uint wardrobe=StoredQuestGarment(winner);
            while(winner.Inventory.Count<35)Pack(winner,1088001,1);Call(feature,null,"Pay",winner);
            Check(!Fenced(winner) && winner.CurrentHonor==0 && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==1,"insufficient bag room retains the entire custom prize");
            foreach(var item in winner.Inventory.Objects.ToArray())DisRemove(winner,item);
            foreach(string table in new[]{"items","entities","teamarena","revora_arena_custom_rewards","revora_achievement_evidence"}) {
                Query("CREATE FUNCTION suppress_custom_payment() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_custom_payment BEFORE "+((table=="items" || table=="revora_achievement_evidence")?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_custom_payment()");
                try{Call(feature,null,"Pay",winner);}finally{Query("DROP TRIGGER suppress_custom_payment ON "+table+";DROP FUNCTION suppress_custom_payment()");}
                Check(Fenced(winner) && winner.CurrentHonor==0 && winner.Inventory.Count==0
                    && Scalar("SELECT COALESCE(currenthonor,0) FROM arena WHERE entityid=@uid",winner.Entity.UID)==0
                    && Scalar("SELECT COALESCE(conquerpoints,0) FROM entities WHERE uid=@uid",winner.Entity.UID)==beforeCp
                    && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND paid_at IS NULL",winner.Entity.UID)==1,
                    "custom payout failure rolls back Honor, currency, items and receipt: "+table);
                Check(HonorEvidence(winner)==0,"failed custom payment grants no first-Honor achievement: "+table);
                DemonReset(winner); // Retry fixture only; not a production reconnect claim.
            }
            Parallel.For(0,8,n=>Call(feature,null,"Pay",winner));Call(feature,null,"Pay",loser);
            Check(HonorEvidence(winner)==1 && HonorEvidence(loser)==0,"custom paid recipient earns first Honor once; ineligible loser does not");
            VerifyHonorAchievementReload(winner);
            Check(!Fenced(winner) && winner.CurrentHonor==500 && winner.HistoryHonor==500 && winner.Entity.ConquerPoints==beforeCp+5000
                && winner.Entity.RevoraPoint==beforeRvp+Config.ArenaHundredWinsRevoraPoints && winner.Entity.Money==beforeSilver+Config.ArenaHundredWinsGold
                && winner.Inventory.Objects.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{3009002,3004243,3004243,3004244,3004244,3004245}.OrderBy(i=>i))
                && winner.Inventory.Objects.All(i=>!i.Bound) && loser.CurrentHonor==0,"parallel custom delivery pays the frozen economy once and excludes ineligible recipients");
            var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",winner.Entity.UID);
            Check(saved.CurrentHonor==500 && saved.HistoryHonor==500 && winner.Inventory.Objects.All(i=>ConquerItemTable.LoadItem(i.UID).ID==i.ID)
                && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND paid_at IS NOT NULL AND cardinality(item_uids)=6",winner.Entity.UID)==1,
                "production statistic and item loaders retain the paid custom prize");
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"hundred-win Arena payout preserves stored wardrobe design through payment failures and retry");
            group.Export();
            Config.ArenaFiftyLossesPenaltyEnabled=false;var disabled=StatGroup(town);var a=disabled.Player1;var b=disabled.Player2;
            b.Entity.VIPLevel=6;Query("UPDATE entities SET viplevel=6 WHERE uid=@uid","uid",(long)b.Entity.UID);
            for(int match=1;match<=50;match++) {
                if(match>1){disabled.Export();disabled=new Group(a,b);disabled.Import();Call(ArenaLedger,null,"Start",disabled);disabled.End(b);}
                if(!CommitArenaStatistics(disabled,true,false,true))throw new Exception("Disabled-policy match rejected.");
            }
            Check(Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND NOT eligible AND NOT policy_enabled AND recipient_vip=6",b.Entity.UID)==1,
                "disabled fifty-loss policy stays disabled even for eligible VIP");disabled.Export();
            Config.ArenaFiftyLossesPenaltyEnabled=true;var nextDay=DateTime.UtcNow.AddDays(1);
            ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>nextDay));
            try {
                for(int match=1;match<=50;match++) {
                    disabled=new Group(a,b);disabled.Import();Call(ArenaLedger,null,"Start",disabled);disabled.End(b);
                    if(!CommitArenaStatistics(disabled,true,false,true))throw new Exception("Enabled loss-policy match rejected.");disabled.Export();
                }
                Call(feature,null,"Pay",b);
                Check(!Fenced(b) && b.CurrentHonor==500 && b.HistoryHonor==500 && b.Inventory.Count==0
                    && Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND eligible AND paid_at IS NOT NULL AND cardinality(item_uids)=0 AND cp=0 AND silver=0 AND rvp=0",b.Entity.UID)==1,
                    "new-day eligible fifty-loss decision pays Honor only and preserves prior disabled decision");
            }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}

        }finally{Config.ArenaHundredWinsMinVip=oldMin;Config.ArenaFiftyLossesPenaltyEnabled=oldEnabled;Config.ArenaHundredWinsCp=oldCp;}
    }

    static void VerifyArenaCoordinator(Map town) {
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");Call(feature,null,"Initialize");
        var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
        int committed=0;Parallel.For(0,12,n=>{if(Call(feature,null,"Complete",group).ToString()=="Committed")System.Threading.Interlocked.Increment(ref committed);});
        Check(committed==1 && a.ArenaStatistic.TodayWin==1 && a.ArenaStatistic.TodayBattles==1 && b.ArenaStatistic.TodayBattles==1,
            "coordinator commits both result statistics once across concurrent completion calls");
        Check(Call(feature,null,"Complete",group).ToString()=="AlreadyCommitted","coordinator recognizes only its durable complete transaction bundle");
        for(int match=2;match<=9;match++) {
            group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(b);
            if(match==9) {
                Query("CREATE FUNCTION suppress_bundle() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_bundle BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_bundle()");
                string outcome;try{outcome=Call(feature,null,"Complete",group).ToString();}finally{Query("DROP TRIGGER suppress_bundle ON revora_arena_result_commits;DROP FUNCTION suppress_bundle()");}
                Check(outcome=="Failed" && Fenced(a) && Fenced(b) && LedgerRows(group,"active")==1 && a.ArenaStatistic.TodayWin==8
                    && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==0,
                    "failure at final bundle receipt rolls back terminal result, both statistics and earned daily entitlement");
                Check(Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid",a.Entity.UID)==9,
                    "durable outcome survives the failed result bundle");
                DemonReset(a);DemonReset(b); // Transaction retry, not production reconnect proof.
                var tomorrow=DateTime.UtcNow.AddDays(1);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>tomorrow));
            }
            try{if(Call(feature,null,"Complete",group).ToString()!="Committed")throw new Exception("Coordinator did not commit owned match.");}
            finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
        }
        Check(a.ArenaStatistic.TodayWin==9 && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==1,
            "retry across midnight retains the observed result day and ninth-win entitlement");
        group.Export();Call(ArenaDaily,null,"Pay",a);
        Check(!Fenced(a) && a.CurrentHonor==500 && a.Inventory.Objects.Count(i=>i.ID==723700)==1,
            "post-export delivery pays Honor earned through the complete coordinator");
        Check(Call(feature,null,"Complete",group).ToString()=="NotReady","exported match cannot reenter completion");
        var legacy=StatGroup(town);CommitArenaStatistics(legacy);
        Check(Call(feature,null,"Complete",legacy).ToString()=="NotReady","partial legacy migration receipts cannot masquerade as a coordinated result");legacy.Export();
        var busy=StatGroup(town);using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var worker=Task.Run(()=>{lock(busy.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(feature,null,"Complete",busy).ToString()=="Busy" && LedgerRows(busy,"active")==1,"busy owner returns promptly without mutating or deadlocking the match");}
            finally{release.Set();worker.Wait();}
        }
        Check(Call(feature,null,"Complete",busy).ToString()=="Committed","busy completion retries after owner lock is released");busy.Export();
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");var causeType=intents.GetNestedType("Cause",Hidden);
        foreach(string reason in new[]{"Defeat","Timeout","Forfeit","Disconnect"}) {
            var pending=StatGroup(town);var players=new[]{pending.Player1,pending.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(pending,"ParticipationSync"))lock(players[0].ItemSyncRoot)lock(players[1].ItemSyncRoot)
                Check((bool)Call(intents,null,"Capture",pending,Enum.Parse(causeType,reason)),"owned outcome captures explicit cause: "+reason);
            Check(Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='"+reason+"'",pending.Player1.Entity.UID)==1,"saved result retains its cause: "+reason);
            if(reason=="Disconnect") {
                Kernel.GamePool.Remove(pending.Player2.Entity.UID);
                try{Check(Call(feature,null,"Complete",pending).ToString()=="NotReady" && LedgerRows(pending,"active")==1
                    && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid",pending.Player2.Entity.UID)==1,
                    "departed canonical session leaves a durable pending outcome for future recovery");}
                finally{Kernel.GamePool.Add(pending.Player2.Entity.UID,pending.Player2);}
            }
            Check(Call(feature,null,"Complete",pending).ToString()=="Committed"
                && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='"+reason+"'",pending.Player1.Entity.UID)==1,
                "coordinator completion preserves the first captured cause: "+reason);pending.Export();
        }
        var snapshotFault=StatGroup(town);
        Query("CREATE FUNCTION suppress_snapshot() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_snapshot BEFORE INSERT ON revora_arena_outcome_snapshots FOR EACH ROW EXECUTE FUNCTION suppress_snapshot()");
        try{Check(Call(feature,null,"Complete",snapshotFault).ToString()=="Failed" && LedgerRows(snapshotFault,"active")==1
            && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid",snapshotFault.Player1.Entity.UID)==0,
            "snapshot insertion failure rolls back the outcome intent as well");}
        finally{Query("DROP TRIGGER suppress_snapshot ON revora_arena_outcome_snapshots;DROP FUNCTION suppress_snapshot()");}
        var changed=StatGroup(town);var ordered=new[]{changed.Player1,changed.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(changed,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
            Call(intents,null,"Capture",changed,Enum.Parse(causeType,"Defeat"));
        changed.Player1.ArenaPoints++;
        Query("UPDATE arena SET arenapoints=arenapoints+1 WHERE entityid=@uid;UPDATE teamarena SET arenapoints=arenapoints+1 WHERE entityid=@uid","uid",(long)changed.Player1.Entity.UID);
        Check(Call(feature,null,"Complete",changed).ToString()=="Failed" && LedgerRows(changed,"active")==1
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",changed.Player1.Entity.UID)==1001,
            "captured preimage prevents a retry from overwriting later statistic changes");
        var captureFault=StatGroup(town);
        Query("CREATE FUNCTION suppress_result_intent() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_result_intent BEFORE INSERT ON revora_arena_result_intents FOR EACH ROW EXECUTE FUNCTION suppress_result_intent()");
        try{Check(Call(feature,null,"Complete",captureFault).ToString()=="Failed" && LedgerRows(captureFault,"active")==1
            && captureFault.Player1.ArenaStatistic.TodayWin==0 && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid",captureFault.Player1.Entity.UID)==0,
            "failed outcome capture cannot commit a result or invent recovery evidence");}
        finally{Query("DROP TRIGGER suppress_result_intent ON revora_arena_result_intents;DROP FUNCTION suppress_result_intent()");}

    }

    static bool ColdArenaRecover(Guid run,bool loadedTeam=false,int sweep=0,int prepare=0) {
        var pool=Kernel.GamePool;var cache=Qualifier.ArenaStatistics;var team=TeamQualifier.ArenaStatistics;var groups=Qualifier.QualifierList.Groups;
        Kernel.GamePool=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Client.GameState>();
        Qualifier.ArenaStatistics=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Network.GamePackets.QualifierStatistic>();
        Qualifier.QualifierList.Groups=new System.Collections.Concurrent.ConcurrentDictionary<uint,Group>();
        TeamQualifier.ArenaStatistics=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Network.GamePackets.TeamQualifierStatistic>();
        if(loadedTeam)TeamQualifier.ArenaStatistics.TryAdd(1,new Revora.Network.GamePackets.TeamQualifierStatistic(true));
        try{if(prepare>0){var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery");Call(type,null,"PrepareStartup",prepare);return (bool)type.GetProperty("Prepared",Hidden).GetValue(null,null);}return sweep>0?(int)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery"),null,"Sweep",sweep)>0:(bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery"),null,"Recover",run);}
        finally{Kernel.GamePool=pool;Qualifier.ArenaStatistics=cache;TeamQualifier.ArenaStatistics=team;Qualifier.QualifierList.Groups=groups;}
    }
    static void VerifyArenaStartup(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery");
        foreach(int threshold in new[]{9,100}) {
            var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
            a.Entity.VIPLevel=5;Query("UPDATE entities SET viplevel=5 WHERE uid=@uid","uid",(long)a.Entity.UID);
            for(int match=1;match<threshold;match++) {
                if(Call(coordinator,null,"Complete",group).ToString()!="Committed")throw new Exception("Startup prerequisite match rejected.");
                group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(b);
            }
            var ordered=new[]{a,b}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(group,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
                Call(intents,null,"Capture",group,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
            bool rejected=false;try{Call(recovery,null,"Recover",ArenaGuid(group));}catch(InvalidOperationException){rejected=true;}
            Check(rejected && LedgerRows(group,"active")==1,"startup recovery refuses live sessions and Arena caches");
            rejected=false;try{ColdArenaRecover(ArenaGuid(group),true);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && LedgerRows(group,"active")==1,"startup recovery refuses an already-loaded team Arena cache");
            foreach(string table in new[]{"arena","teamarena","revora_arena_result_statistics","revora_arena_result_commits","revora_achievement_evidence",threshold==9?"revora_arena_daily_rewards":"revora_arena_custom_rewards"}) {
                if(table=="revora_achievement_evidence" && threshold!=9)continue;
                string verb=table=="arena" || table=="teamarena"?"UPDATE":"INSERT";
                Query("CREATE FUNCTION suppress_recovery() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_recovery BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_recovery()");
                bool failed=false;try{ColdArenaRecover(ArenaGuid(group));}catch(InvalidOperationException){failed=true;}
                finally{Query("DROP TRIGGER suppress_recovery ON "+table+";DROP FUNCTION suppress_recovery()");}
                Check(failed && LedgerRows(group,"active")==1 && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",a.Entity.UID)==threshold-1,
                    "startup recovery rolls back result and both statistics on failure: "+threshold+"/"+table);
            }
            Check(ColdArenaRecover(ArenaGuid(group)) && !ColdArenaRecover(ArenaGuid(group)),"captured outcome recovers once across repeated startup calls: "+threshold);
                    Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11112",a.Entity.UID)==1,
                "startup recovered first-nine completion remains one receipt: "+threshold);
            var restoredAchievement=new Achievement(a.Entity);restoredAchievement.Load("");Call(typeof(Achievement),restoredAchievement,"Refresh",DateTime.UtcNow,true);
            int achievementPosition=(11112/100-101)*32+11112%100-1;
            Check((System.BitConverter.ToUInt32(restoredAchievement.ToArray(),16+achievementPosition/32*4)&(1u<<(achievementPosition%32)))!=0,
                "production achievement loader restores recovered first-nine completion");
            var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
            Check(saved.TodayWin==threshold && saved.TodayBattles==threshold && saved.TotalWin==threshold
                && Scalar("SELECT todaybattles FROM arena WHERE entityid=@uid",b.Entity.UID)==threshold,"production statistic loader reads both recovered participants");
            Check(threshold==9?Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND milestone='wins9' AND paid_at IS NULL",a.Entity.UID)==1
                :Scalar("SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND milestone='wins100' AND eligible AND recipient_vip=5 AND paid_at IS NULL",a.Entity.UID)==1,
                "startup recovery retains the original earned milestone as pending delivery: "+threshold);
            group.Export(); // Fixture cleanup only; production recovery has no live group.
            Kernel.GamePool.Remove(a.Entity.UID);Kernel.GamePool.Remove(b.Entity.UID);
        }
        var missing=StatGroup(town);Check(!ColdArenaRecover(ArenaGuid(missing)) && LedgerRows(missing,"active")==1,"abandoned match without captured outcome cannot invent a winner");missing.Export();
    }

    static void VerifyArenaSweep(Map town) {
        // Explicit disposable schema only; previous fixture evidence is no longer needed.
        Query("TRUNCATE revora_potency_validation.revora_arena_matches CASCADE");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery");
        var captured=StatGroup(town);var abandoned=StatGroup(town);var other=StatGroup(town);
        var ordered=new[]{captured.Player1,captured.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(captured,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
            Call(intents,null,"Capture",captured,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
        Query("CREATE FUNCTION suppress_abandonment() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_abandonment BEFORE INSERT ON revora_arena_abandoned FOR EACH ROW EXECUTE FUNCTION suppress_abandonment()");
        bool rejected=false;try{ColdArenaRecover(Guid.Empty,false,128);}catch(InvalidOperationException){rejected=true;}
        finally{Query("DROP TRIGGER suppress_abandonment ON revora_arena_abandoned;DROP FUNCTION suppress_abandonment()");}
        Check(rejected && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==0,"failed abandonment receipt leaves the interrupted match open for retry");
        long before=Scalar("SELECT count(*) FROM revora_arena_matches WHERE state='finished'",0);
        Check(ColdArenaRecover(Guid.Empty,false,1)
            && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)+Scalar("SELECT count(*) FROM revora_arena_matches WHERE state='finished'",0)==before+1,
            "startup sweep respects its per-call batch limit");
        Check(ColdArenaRecover(Guid.Empty,false,128) && !ColdArenaRecover(Guid.Empty,false,128)
            && LedgerRows(captured,"finished")==1 && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==2,
            "startup sweep recovers captured evidence and closes uncaptured interruptions once");
        Check(Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",abandoned.Player1.Entity.UID)==0
            && Scalar("SELECT totalwin FROM arena WHERE entityid=@uid",abandoned.Player1.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards",0)==0 && Scalar("SELECT count(*) FROM revora_arena_custom_rewards",0)==0,
            "uncaptured interruption gives no winner, loss, points or milestone reward");
        abandoned.Done=false;Check(!(bool)Call(ArenaLedger,null,"Start",abandoned),"closed interrupted match cannot be readmitted under its old identity");abandoned.Done=true;
        Check(!FinishLedger(abandoned),"closed interrupted match cannot acquire a late terminal result");
        ordered=new[]{abandoned.Player1,abandoned.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(abandoned,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
            Check(!(bool)Call(intents,null,"Capture",abandoned,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect")),"closed interrupted match cannot fabricate a late captured outcome");
        foreach(var group in new[]{captured,abandoned,other})group.Export();
        var broken=StatGroup(town);ordered=new[]{broken.Player1,broken.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(broken,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
            Call(intents,null,"Capture",broken,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
        Query("DELETE FROM revora_arena_outcome_snapshots WHERE uid=@uid","uid",(long)broken.Player2.Entity.UID);
        rejected=false;try{ColdArenaRecover(Guid.Empty,false,128);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && LedgerRows(broken,"active")==1 && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==2,
            "incomplete captured evidence stops startup recovery instead of discarding the result");
        rejected=false;try{Call(recovery,null,"Sweep",129);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"startup recovery rejects unbounded batch requests");
    }

    static void VerifyArenaRanking(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches CASCADE");
        var rank=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRankingSettlement");Call(rank,null,"Initialize");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        foreach(uint position in new uint[]{0,1,1000,1001,uint.MaxValue})
            Check((uint)Call(rank,null,"Honor",position)==(position==1?1000000u:position==1000?1000u:0u),"ranking Honor bounds avoid unsigned wrap: "+position);
        var now=new DateTime(2041,1,1,20,0,0,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
            if(Call(coordinator,null,"Complete",group).ToString()!="Committed")throw new Exception("Rank first result rejected.");group.Export();
            group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(a);
            if(Call(coordinator,null,"Complete",group).ToString()!="Committed")throw new Exception("Rank tied-timestamp result rejected.");group.Export();
            var interrupted=StatGroup(town);
            bool rejected=false;try{Call(rank,null,"Close","20410101");}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"current Pacific day cannot be settled early");
            now=now.AddDays(1);
            rejected=false;try{Call(rank,null,"Close","20410101");}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"unresolved interrupted outcome blocks day settlement");
            ColdArenaRecover(Guid.Empty,false,128);interrupted.Export();
            var tomorrow=StatGroup(town);if(Call(coordinator,null,"Complete",tomorrow).ToString()!="Committed")throw new Exception("Next-day result rejected.");tomorrow.Export();
            Query("UPDATE revora_arena_result_statistics SET ordering_verified=false WHERE uid=@uid","uid",(long)a.Entity.UID);
            rejected=false;try{Call(rank,null,"Close","20410101");}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"historical rows without verified ordering cannot manufacture daily ranking");
            Query("UPDATE revora_arena_result_statistics SET ordering_verified=true WHERE uid=@uid","uid",(long)a.Entity.UID);
            Query("CREATE FUNCTION suppress_rank() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rank BEFORE INSERT ON revora_arena_rank_rewards FOR EACH ROW EXECUTE FUNCTION suppress_rank()");
            rejected=false;try{Call(rank,null,"Close","20410101");}catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_rank ON revora_arena_rank_rewards;DROP FUNCTION suppress_rank()");}
            Check(rejected && Scalar("SELECT count(*) FROM revora_arena_rank_days",0)==0,"failed rank insert rolls back the closed day as well");
            int closed=0;Parallel.For(0,8,n=>{if((bool)Call(rank,null,"Close","20410101"))System.Threading.Interlocked.Increment(ref closed);});
            Check(closed==1 && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE day='20410101'",0)==2,
                "concurrent settlement snapshots each participant once without including later-day players");
            Check(Scalar("SELECT points FROM revora_arena_rank_rewards WHERE uid=@uid AND day='20410101'",a.Entity.UID)==1000
                && Scalar("SELECT rank FROM revora_arena_rank_rewards WHERE uid=@uid AND day='20410101'",Math.Min(a.Entity.UID,b.Entity.UID))==1,
                "last committed result resolves tied timestamps and equal points rank by UID");
            a.CurrentHonor=uint.MaxValue;a.HistoryHonor=uint.MaxValue;
            Query("UPDATE arena SET currenthonor=4294967295,historyhonor=4294967295 WHERE entityid=@uid;UPDATE teamarena SET currenthonor=4294967295,historyhonor=4294967295 WHERE entityid=@uid","uid",(long)a.Entity.UID);
            Call(rank,null,"Pay",a);
            Check(!Fenced(a) && a.CurrentHonor==uint.MaxValue && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE uid=@uid AND paid_at IS NULL",a.Entity.UID)==1,
                "ranking overflow retains the unpaid entitlement without wrapping Honor");
            a.CurrentHonor=0;a.HistoryHonor=0;
            Query("UPDATE arena SET currenthonor=0,historyhonor=0 WHERE entityid=@uid;UPDATE teamarena SET currenthonor=0,historyhonor=0 WHERE entityid=@uid","uid",(long)a.Entity.UID);
            foreach(string table in new[]{"arena","teamarena","revora_arena_rank_rewards","revora_achievement_evidence"}) {
                Query("CREATE FUNCTION suppress_rank_payment() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rank_payment BEFORE "+(table=="revora_achievement_evidence"?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_rank_payment()");
                try{Call(rank,null,"Pay",a);}finally{Query("DROP TRIGGER suppress_rank_payment ON "+table+";DROP FUNCTION suppress_rank_payment()");}
                Check(Fenced(a) && a.CurrentHonor==0 && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",a.Entity.UID)==0
                    && Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",a.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE uid=@uid AND paid_at IS NULL",a.Entity.UID)==1,
                    "failed ranking payout rolls back both Honor mirrors and paid receipt: "+table);
                Check(HonorEvidence(a)==0,"failed ranking payout creates no first-Honor credit: "+table);
                DemonReset(a); // Fixture retry; full reconnect remains a separate acceptance step.
            }
            while(a.Inventory.Count<40)Pack(a,1088001,1);
            Parallel.For(0,12,n=>Call(rank,null,"Pay",a));Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRewardDelivery"),null,"Tick",b);
            Check(HonorEvidence(a)==1 && HonorEvidence(b)==1,"paid ranking Honor grants exactly one achievement per ordinary recipient");
            VerifyHonorAchievementReload(a);VerifyHonorAchievementReload(b);
            uint expectedA=(uint)Scalar("SELECT honor FROM revora_arena_rank_rewards WHERE uid=@uid AND day='20410101'",a.Entity.UID);
            uint expectedB=(uint)Scalar("SELECT honor FROM revora_arena_rank_rewards WHERE uid=@uid AND day='20410101'",b.Entity.UID);
            Check(!Fenced(a) && !Fenced(b) && a.CurrentHonor==expectedA && a.HistoryHonor==expectedA && b.CurrentHonor==expectedB && b.HistoryHonor==expectedB
                && a.Inventory.Count==40 && Scalar("SELECT count(*) FROM revora_arena_rank_rewards WHERE paid_at IS NOT NULL",0)==2,
                "concurrent ranking delivery pays earned Honor once even with a full bag");
            var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
            Check(loaded.CurrentHonor==expectedA && loaded.HistoryHonor==expectedA,"production Arena loader retains ranking Honor");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases"),null,"Initialize");HonorShop.Load();
            foreach(var filler in a.Inventory.Objects.Take(2).ToArray())DisRemove(a,filler);
            uint spent=0;foreach(uint type in new uint[]{720842,3004259}) {
                uint cost=HonorShop.Shop.Items[type];HonorBuy(a,type);spent+=cost;
                Check(!Fenced(a) && a.CurrentHonor==expectedA-spent && a.HistoryHonor==expectedA
                    && a.Inventory.Objects.Count(i=>i.ID==type)==1
                    && Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",a.Entity.UID)==expectedA-spent,
                    "ordinary shop request spends earned ranking Honor on boss summon item: "+type);
            }
            loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
            Check(loaded.CurrentHonor==expectedA-spent && a.Inventory.Objects.Where(i=>i.ID==720842 || i.ID==3004259).All(i=>ConquerItemTable.LoadItem(i.UID).ID==i.ID),
                "production loaders retain earned Honor purchases and the remaining balance");
            now=now.AddDays(1);Check((bool)Call(rank,null,"Close","20410102") && !(bool)Call(rank,null,"Close","20410101"),"consecutive daily settlements do not replay earlier rewards");
            now=new DateTime(2041,1,1,21,0,0,DateTimeKind.Utc);var late=LedgerGroup(town);
            rejected=false;try{Call(ArenaLedger,null,"Start",late);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && LedgerRows(late,"active")==0,"closed day rejects late admission before any earned evidence is created");
            foreach(var date in new[]{new DateTime(2041,12,31,20,0,0,DateTimeKind.Utc),new DateTime(2042,1,1,20,0,0,DateTimeKind.Utc),new DateTime(2042,1,2,20,0,0,DateTimeKind.Utc),new DateTime(2042,1,3,20,0,0,DateTimeKind.Utc)}) {
                now=date;var missed=StatGroup(town);
                if(Call(coordinator,null,"Complete",missed).ToString()!="Committed")throw new Exception("Missed-day fixture result rejected.");missed.Export();
            }
            Check((int)Call(rank,null,"ClosePending",1)==1
                && Scalar("SELECT count(*) FROM revora_arena_rank_days WHERE day='20411231'",0)==1
                && Scalar("SELECT count(*) FROM revora_arena_rank_days WHERE day IN('20420101','20420102')",0)==0,
                "missed-day settlement starts with the oldest played day and respects its batch limit");
            Check((int)Call(rank,null,"ClosePending",31)==2 && (int)Call(rank,null,"ClosePending",31)==0
                && Scalar("SELECT count(*) FROM revora_arena_rank_days WHERE day='20420103'",0)==0,
                "year-crossing catch-up settles each completed played day once and leaves today open");
            rejected=false;try{Call(rank,null,"ClosePending",32);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"missed-day settlement refuses unbounded requests");
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaDelivery(Revora.Client.GameState p) {
        var delivery=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRewardDelivery");
        uint before=p.CurrentHonor;int gifts=p.Inventory.Objects.Count(i=>i.ID==723700);
        var now=DateTime.UtcNow.AddDays(2);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            while(p.Inventory.Count<40)Pack(p,1088001,1);
            Check((bool)Call(delivery,null,"Tick",p) && p.CurrentHonor==before
                && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",p.Entity.UID)==1,
                "delivery probe preserves the earned daily reward while inventory is full");
            DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1088001));
            Check(!(bool)Call(delivery,null,"Tick",p) && p.CurrentHonor==before,"repeated character ticks respect the delivery interval");
            now=now.AddSeconds(30);Call(delivery,null,"Tick",p);
            Check(!Fenced(p) && p.CurrentHonor==before && p.Inventory.Count==39,
                "periodic Arena delivery retains the complete bundle until two slots are free");
            DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1088001));
            now=now.AddSeconds(30);Parallel.For(0,12,n=>Call(delivery,null,"Tick",p));
            Check(!Fenced(p) && p.CurrentHonor==before+500 && p.Inventory.Objects.Count(i=>i.ID==723700)==gifts+1,
                "later concurrent ticks deliver the retained daily reward exactly once");
            now=now.AddMinutes(-1);Check((bool)Call(delivery,null,"Tick",p) && p.CurrentHonor==before+500,
                "clock rollback permits a fresh probe without replaying payment");
            Check(!(bool)Call(delivery,null,"Tick",p),"clock rollback does not permit repeated probes at the same timestamp");
            Check(Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",p.Entity.UID)==0,
                "all earned daily entitlements are paid after bounded retry");
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaRollover(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches CASCADE");
        var rollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDayRollover");Call(rollover,null,"Initialize");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var ranking=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaRankingSettlement");
        var now=new DateTime(2049,12,31,20,0,0,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var group=StatGroup(town);var p=group.Player1;p.Entity.Level=120;Query("UPDATE entities SET level=120 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Check(!(bool)Call(rollover,null,"Prepare",p),"rollover cannot change points inside an owned match");
            if(Call(coordinator,null,"Complete",group).ToString()!="Committed")throw new Exception("Rollover fixture outcome rejected.");group.Export();
            uint earned=p.ArenaPoints;
            Check((bool)Call(rollover,null,"Prepare",p) && p.ArenaPoints==earned && p.ArenaStatistic.TodayWin==1 && p.ArenaStatistic.TodayBattles==1,
                "first ownership of a day preserves its already-earned result points and counts");
            now=now.AddDays(1);
            Check(!(bool)Call(rollover,null,"Prepare",p) && p.ArenaPoints==earned,"rollover waits for the previous day's ranking snapshot");
            Call(ranking,null,"Close","20491231");
            foreach(string table in new[]{"arena","teamarena","revora_arena_day_rollovers"}) {
                Query("CREATE FUNCTION suppress_rollover() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_rollover BEFORE "+(table=="revora_arena_day_rollovers"?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_rollover()");
                try{Call(rollover,null,"Prepare",p);}finally{Query("DROP TRIGGER suppress_rollover ON "+table+";DROP FUNCTION suppress_rollover()");}
                Check(Fenced(p) && p.ArenaPoints==earned && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==earned
                    && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid AND day='20500101'",p.Entity.UID)==0,
                    "failed rollover leaves points, counters and receipt unchanged: "+table);DemonReset(p);
            }
            Parallel.For(0,8,n=>Call(rollover,null,"Prepare",p));
            Check(!Fenced(p) && p.ArenaPoints==4000 && p.TeamArenaStatistic.ArenaPoints==4000 && p.ArenaStatistic.TodayWin==0 && p.ArenaStatistic.TodayBattles==0
                && p.ArenaStatistic.TotalWin==1 && p.ArenaStatistic.LastSeasonRank==1 && p.ArenaStatistic.LastSeasonArenaPoints==earned
                && p.ArenaStatistic.LastSeasonWin==1 && p.ArenaStatistic.LastSeasonLose==0 && p.CurrentHonor==0
                && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid AND day='20500101'",p.Entity.UID)==1,
                "year-boundary rollover fills once and projects the saved ranking without duplicate Honor");
            var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(loaded.ArenaPoints==4000 && loaded.LastSeasonArenaPoints==earned && loaded.LastSeasonWin==1,
                "production reload preserves owned Pacific rollover instead of applying host-local reset");
            ArenaTable.Reset(p,p.ArenaStatistic);Check(p.CurrentHonor==0 && p.ArenaStatistic.LastSeasonArenaPoints==earned,
                "legacy reset cannot re-award or overwrite a managed daily result");
            now=now.AddDays(-1);Check(!(bool)Call(rollover,null,"Prepare",p) && p.ArenaPoints==4000,"clock rollback cannot reopen an earlier point-fill day");
            now=now.AddDays(2);Check((bool)Call(rollover,null,"Prepare",p) && p.ArenaStatistic.LastSeasonRank==0 && p.ArenaStatistic.TotalWin==1,
                "consecutive inactive day clears yesterday's ranking but preserves lifetime wins");
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaDepartures(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");var cause=intents.GetNestedType("Cause",Hidden);
        foreach(string name in new[]{"Defeat","Forfeit","Disconnect"}) {
            var group=StatGroup(town);group.Done=false;group.Winner=null;group.Loser=null;
            var outsider=ArenaPlayer(town);group.End(outsider);
            Check(!group.Done && group.Winner==null && group.Loser==null,"foreign session cannot end another player's Arena match");
            Check(Call(coordinator,null,"EndForPlayer",group,outsider,Enum.Parse(cause,name)).ToString()=="NotReady" && !group.Done,
                "departure entry point rejects an unrelated session: "+name);
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var worker=Task.Run(()=>{lock(group.Player1.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(coordinator,null,"EndForPlayer",group,group.Player2,Enum.Parse(cause,name)).ToString()=="Busy" && !group.Done,
                    "busy owner delays departure without freezing an uncommitted outcome: "+name);}
                finally{release.Set();worker.Wait();}
            }
            int commits=0;Parallel.For(0,8,n=>{if(Call(coordinator,null,"EndForPlayer",group,group.Player2,Enum.Parse(cause,name)).ToString()=="Committed")System.Threading.Interlocked.Increment(ref commits);});
            Check(commits==1 && ReferenceEquals(group.Loser,group.Player2) && group.Player1.ArenaStatistic.TodayWin==1 && group.Player2.ArenaStatistic.TodayBattles==1
                && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='"+name+"'",group.Player2.Entity.UID)==1,
                "canonical departure ends and commits exactly one result with its original cause: "+name);
            Check(Call(coordinator,null,"EndForPlayer",group,group.Player1,Enum.Parse(cause,"Disconnect")).ToString()=="AlreadyCommitted"
                && ReferenceEquals(group.Loser,group.Player2),"later departure cannot reverse an already committed outcome");group.Export();
        }
        var failed=StatGroup(town);failed.Done=false;failed.Winner=null;failed.Loser=null;
        Query("CREATE FUNCTION suppress_departure_intent() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_departure_intent BEFORE INSERT ON revora_arena_result_intents FOR EACH ROW EXECUTE FUNCTION suppress_departure_intent()");
        try{Check(Call(coordinator,null,"EndForPlayer",failed,failed.Player2,Enum.Parse(cause,"Disconnect")).ToString()=="Failed"
            && failed.Done && Fenced(failed.Player1) && Fenced(failed.Player2) && LedgerRows(failed,"active")==1 && failed.Player1.ArenaStatistic.TodayWin==0,
            "failed departure capture fences both sessions without paying an uncommitted result");}
        finally{Query("DROP TRIGGER suppress_departure_intent ON revora_arena_result_intents;DROP FUNCTION suppress_departure_intent()");}
        var removed=StatGroup(town);removed.Done=false;removed.Winner=null;removed.Loser=null;Kernel.GamePool.Remove(removed.Player2.Entity.UID);
        try{Check(Call(coordinator,null,"EndForPlayer",removed,removed.Player2,Enum.Parse(cause,"Disconnect")).ToString()=="NotReady" && !removed.Done,
            "departure capture must run before canonical session removal");}
        finally{Kernel.GamePool.Add(removed.Player2.Entity.UID,removed.Player2);}
        var invalid=Call(coordinator,null,"EndForPlayer",removed,removed.Player2,Enum.Parse(cause,"Timeout"));
        Check(invalid.ToString()=="NotReady" && !removed.Done,"player departure cannot impersonate a timeout selection");removed.End(removed.Player2);removed.Export();
    }

    static void VerifyArenaTimeouts(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var clock=coordinator.GetField("MatchClock",Hidden);var original=clock.GetValue(null);
        try {
            foreach(bool wrapped in new[]{false,true}) {
                var group=StatGroup(town);group.Done=false;group.Winner=null;group.Loser=null;
                group.ImportTime=new Time32(wrapped?int.MaxValue-1000:100000);group.CreateTime=group.ImportTime.AddMinutes(-20);
                var outsider=ArenaPlayer(town);group.UpdateDamage(outsider,uint.MaxValue);
                Check(group.Player1Damage==0 && group.Player2Damage==0,"foreign sessions cannot change Arena damage totals");
                Parallel.For(0,100,n=>group.UpdateDamage(group.Player1,10));group.UpdateDamage(group.Player2,900);
                Check(group.Player1Damage==1000 && group.Player2Damage==900,"concurrent participant damage accumulates without lost updates");
                var now=new Time32(unchecked(group.ImportTime.Value+179999));clock.SetValue(null,new Func<Time32>(()=>now));
                Check(Call(coordinator,null,"EndTimedOut",group).ToString()=="NotReady" && !group.Done,
                    "timeout uses three minutes after import, not old matchmaking creation time");
                now=new Time32(unchecked(group.ImportTime.Value+180000));int committed=0;
                Parallel.For(0,8,n=>{if(Call(coordinator,null,"EndTimedOut",group).ToString()=="Committed")System.Threading.Interlocked.Increment(ref committed);});
                Check(committed==1 && ReferenceEquals(group.Winner,group.Player1)
                    && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='Timeout'",group.Player1.Entity.UID)==1,
                    "deadline commits the damage winner once, including tick counter wrap: "+wrapped);
                group.UpdateDamage(group.Player2,uint.MaxValue);Check(group.Player2Damage==900,"late damage cannot rewrite a completed timeout");group.Export();
            }
            var tie=StatGroup(town);tie.Done=false;tie.Winner=null;tie.Loser=null;
            tie.UpdateDamage(tie.Player1,uint.MaxValue);tie.UpdateDamage(tie.Player1,100);tie.UpdateDamage(tie.Player2,uint.MaxValue);
            Check(tie.Player1Damage==uint.MaxValue && tie.Player2Damage==uint.MaxValue,"damage totals saturate rather than wrapping below an opponent");
            clock.SetValue(null,new Func<Time32>(()=>new Time32(unchecked(tie.ImportTime.Value+180000))));
            Check(Call(coordinator,null,"EndTimedOut",tie).ToString()=="Committed" && ReferenceEquals(tie.Winner,tie.Player2),
                "equal timeout damage preserves the existing player-two tie rule");tie.Export();
            var preexisting=StatGroup(town);
            Check(Call(coordinator,null,"EndTimedOut",preexisting).ToString()=="Committed"
                && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='Result'",preexisting.Player1.Entity.UID)==1,
                "timeout callback does not invent a timeout cause for an earlier RAM ending");preexisting.Export();
        }finally{clock.SetValue(null,original);}
    }

    static void VerifyArenaCleanup(Map town) {
        DMaps.MapPaths[1005]=DMaps.MapPaths[700]; // Spectator display base for the existing dynamic Arena map.
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var pending=StatGroup(town);var map=pending.Player1.Map;
        Check(Call(coordinator,null,"Cleanup",pending).ToString()=="NotReady" && ReferenceEquals(pending.Player1.Map,map)
            && Qualifier.QualifierList.Groups.ContainsKey(pending.ID),"cleanup retains an uncommitted match and its players");
        Check(Call(coordinator,null,"Complete",pending).ToString()=="Committed","cleanup fixture commits the full earned result");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(pending.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(coordinator,null,"Cleanup",pending).ToString()=="Busy" && ReferenceEquals(pending.Player1.Map,map),
                "busy cleanup changes neither fighter nor map");}finally{release.Set();task.Wait();}
        }
        pending.Player1.ArenaStatistic.PlayWith=pending.Player2;pending.Player2.ArenaStatistic.PlayWith=pending.Player1;
        pending.Player1.ArenaStatistic.AcceptBox=true;
        Qualifier.PlayerList[pending.Player1.Entity.UID]=pending.Player1;
        Qualifier.WaitingPlayerList[pending.Player1.Entity.UID]=pending.Player1;
        var earnedPoints=pending.Player1.ArenaPoints;var earnedHonor=pending.Player1.ArenaStatistic.CurrentHonor;
        int cleaned=0;Parallel.For(0,8,n=>{if(Call(coordinator,null,"Cleanup",pending).ToString()=="Committed")System.Threading.Interlocked.Increment(ref cleaned);});
        Check(cleaned==1 && pending.Player1.QualifierGroup==null && pending.Player2.QualifierGroup==null
            && !ReferenceEquals(pending.Player1.Map,map) && !ReferenceEquals(pending.Player2.Map,map)
            && !Kernel.Maps.ContainsKey(map.ID) && !Qualifier.QualifierList.Groups.ContainsKey(pending.ID),
            "committed cleanup returns both fighters and removes only its map and group once");
        Check(pending.Player1.ArenaStatistic.PlayWith==null && pending.Player2.ArenaStatistic.PlayWith==null
            && !pending.Player1.ArenaStatistic.AcceptBox
            && pending.Player1.ArenaStatistic.Status==Revora.Network.GamePackets.QualifierStatistic.NotSignedUp && pending.Player1.ArenaState==Qualifier.None
            && !Qualifier.PlayerList.ContainsKey(pending.Player1.Entity.UID) && !Qualifier.WaitingPlayerList.ContainsKey(pending.Player1.Entity.UID)
            && pending.Player1.ArenaPoints==earnedPoints && pending.Player1.ArenaStatistic.CurrentHonor==earnedHonor,
            "durable cleanup releases invitations and queues without changing earned balances");
        var next=new Group(pending.Player1,pending.Player2);next.Import();var nextMap=next.Player1.Map;
        Check(Call(coordinator,null,"Cleanup",pending).ToString()=="AlreadyCommitted" && ReferenceEquals(next.Player1.Map,nextMap)
            && ReferenceEquals(next.Player1.QualifierGroup,next),"repeated old cleanup cannot disturb a later match");next.End(next.Player2);next.Export();
        var moved=StatGroup(town);Call(coordinator,null,"Complete",moved);
        moved.Player1.Entity.Teleport(1002,408,330);moved.Player1.Entity.PKMode=Enums.PKMode.Peace;
        ushort x=moved.Player1.Entity.X,y=moved.Player1.Entity.Y;
        Check(Call(coordinator,null,"Cleanup",moved).ToString()=="Committed" && moved.Player1.Entity.X==x && moved.Player1.Entity.Y==y
            && moved.Player1.Entity.PKMode==Enums.PKMode.Peace,"cleanup leaves an already-departed player's position and PK mode alone");
        var replaced=StatGroup(town);Call(coordinator,null,"Complete",replaced);var old=replaced.Player1;var replacement=ArenaPlayer(town);
        Qualifier.PlayerList[old.Entity.UID]=replacement;Qualifier.WaitingPlayerList[old.Entity.UID]=old;
        Kernel.GamePool[old.Entity.UID]=replacement;var oldMap=old.Map;
        try{Check(Call(coordinator,null,"Cleanup",replaced).ToString()=="Committed" && ReferenceEquals(old.Map,oldMap)
            && ReferenceEquals(replacement.Map,town),"cleanup does not mutate a replaced session or its canonical replacement");}
        finally{Kernel.GamePool[old.Entity.UID]=old;}
        Check(ReferenceEquals(Qualifier.PlayerList[old.Entity.UID],replacement) && !Qualifier.WaitingPlayerList.ContainsKey(old.Entity.UID),
            "cleanup removes departed owner queue entries while retaining replacement entries");
        Qualifier.PlayerList.Remove(old.Entity.UID);
        var fencedAway=StatGroup(town);Call(coordinator,null,"Complete",fencedAway);
        fencedAway.Player1.Entity.Teleport(1002,408,330);fencedAway.Player1.ArenaStatistic.PlayWith=fencedAway.Player2;
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(fencedAway.Player1.Entity,true);
        Check(Call(coordinator,null,"Cleanup",fencedAway).ToString()=="NotReady"
            && ReferenceEquals(fencedAway.Player1.ArenaStatistic.PlayWith,fencedAway.Player2),
            "off-map fenced owner retains invitation until safe cleanup can resume");
        DemonReset(fencedAway.Player1);Call(coordinator,null,"Cleanup",fencedAway);
        var foreign=StatGroup(town);Call(coordinator,null,"Complete",foreign);var foreignMap=foreign.Player1.Map;
        foreign.Player1.ArenaStatistic.PlayWith=replacement;
        Check(Call(coordinator,null,"Cleanup",foreign).ToString()=="NotReady" && ReferenceEquals(foreign.Player1.Map,foreignMap)
            && ReferenceEquals(foreign.Player1.ArenaStatistic.PlayWith,replacement),"cleanup preserves a different invitation before teleporting");
        foreign.Player1.ArenaStatistic.PlayWith=foreign.Player2;Call(coordinator,null,"Cleanup",foreign);
        var legacy=StatGroup(town);legacy.Player1.ArenaStatistic.PlayWith=legacy.Player2;
        legacy.Player2.ArenaStatistic.PlayWith=legacy.Player1;
        Check(legacy.Export() && ReferenceEquals(legacy.Player1.ArenaStatistic.PlayWith,legacy.Player2)
            && ReferenceEquals(legacy.Player2.ArenaStatistic.PlayWith,legacy.Player1),"legacy export retains peer references required by its result writer");
        var watched=StatGroup(town);watched.Done=false;var spectator=ArenaPlayer(town);watched.BeginWatching(spectator);
        Check(ReferenceEquals(spectator.WatchingGroup,watched) && ReferenceEquals(spectator.Map,watched.Player1.Map),
            "cleanup fixture actually admits its spectator to the owned fight map");watched.End(watched.Player2);
        Call(coordinator,null,"Complete",watched);
        Check(Call(coordinator,null,"Cleanup",watched).ToString()=="Committed" && spectator.WatchingGroup==null
            && spectator.Entity.MapID==town.ID && watched.Watchers.Count==0,"committed cleanup returns its ordinary spectator and clears watcher ownership");
        var terminal=StatGroup(town);CommitArenaStatistics(terminal);
        Check(Call(coordinator,null,"Cleanup",terminal).ToString()=="NotReady" && ReferenceEquals(terminal.Player1.QualifierGroup,terminal),
            "terminal match without the complete reward bundle cannot be cleaned up");terminal.Export();
        var partial=StatGroup(town);Call(coordinator,null,"Complete",partial);
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(partial.Player2.Entity,true);
        var retained=partial.Player2.Map;
        Check(Call(coordinator,null,"Cleanup",partial).ToString()=="NotReady" && Kernel.Maps.ContainsKey(retained.ID)
            && ReferenceEquals(partial.Player2.QualifierGroup,partial),"fenced fighter retains the map and retryable cleanup after the other fighter leaves");
        DemonReset(partial.Player2);
        Check(Call(coordinator,null,"Cleanup",partial).ToString()=="Committed" && !Kernel.Maps.ContainsKey(retained.ID),
            "partial cleanup retries without requiring both players to remain on the fight map");
    }

    static void VerifyArenaSpectators(Map town) {
        var group=LedgerGroup(town);var spectator=ArenaPlayer(town);
        var watcherPath=DMaps.MapPaths[1005];DMaps.MapPaths.Remove(1005);
        try{group.BeginWatching(spectator);Check(spectator.WatchingGroup==null && group.Watchers.Count==0 && spectator.Entity.MapID==town.ID,
            "failed spectator teleport cannot leave a phantom membership");}finally{DMaps.MapPaths[1005]=watcherPath;}
        group.BeginWatching(spectator,uint.MaxValue);
        Check(spectator.WatchingGroup==null && group.Watchers.Count==0,"unknown fighter cannot admit a spectator");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(spectator.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{group.BeginWatching(spectator);Check(spectator.WatchingGroup==null && group.Watchers.Count==0,"busy spectator admission leaves map and ownership untouched");}
            finally{release.Set();task.Wait();}
        }
        Parallel.For(0,10,n=>group.BeginWatching(spectator,group.Player1.Entity.UID));
        Check(group.Watchers.Count==1 && ReferenceEquals(spectator.WatchingGroup,group) && ReferenceEquals(spectator.Map,group.Player1.Map),
            "concurrent ordinary spectator joins admit exactly one membership");
        Qualifier.QualifyEngine.DoCheer(spectator,uint.MaxValue);
        Check(group.Player1Cheers==0 && group.Player2Cheers==0,"unknown fighter cannot consume or receive a cheer");
        Parallel.For(0,10,n=>Qualifier.QualifyEngine.DoCheer(spectator,group.Player1.Entity.UID));
        Check(group.Player1Cheers==1 && group.Player2Cheers==0,"concurrent cheer packets count once for the chosen fighter");
        Parallel.For(0,10,n=>Qualifier.QualifyEngine.DoLeave(spectator));
        Check(group.Watchers.Count==0 && spectator.WatchingGroup==null && spectator.Entity.MapID==town.ID,
            "concurrent spectator departures remove ownership and return once");
        group.BeginWatching(spectator);group.End(group.Player2);var late=ArenaPlayer(town);group.BeginWatching(late);
        Check(late.WatchingGroup==null && late.Entity.MapID==town.ID,"ended fight rejects a late spectator before export");
        Qualifier.QualifyEngine.DoCheer(spectator,group.Player2.Entity.UID);
        Check(group.Player2Cheers==0,"ended fight rejects late cheers");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(spectator.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(!group.Export() && Qualifier.QualifierList.Groups.ContainsKey(group.ID) && ReferenceEquals(spectator.WatchingGroup,group),
                "busy legacy export retains group and spectator for the next lifecycle pass");}finally{release.Set();task.Wait();}
        }
        int exported=0;Parallel.For(0,10,n=>{if(group.Export())System.Threading.Interlocked.Increment(ref exported);});
        Check(exported==1 && spectator.WatchingGroup==null && spectator.Entity.MapID==town.ID,
            "legacy export succeeds once and returns its spectator before map disposal");
        group.BeginWatching(late,group.Player1.Entity.UID);
        Check(late.WatchingGroup==null && group.Watchers.Count==0,"disposed fight cannot admit a spectator through either overload");
        var first=LedgerGroup(town);var second=LedgerGroup(town);var shared=ArenaPlayer(town);
        Parallel.Invoke(()=>first.BeginWatching(shared),()=>second.BeginWatching(shared));
        Check(first.Watchers.Count+second.Watchers.Count==1 && shared.WatchingGroup!=null
            && ReferenceEquals(shared.Map,shared.WatchingGroup.Player1.Map),"competing fights cannot both own one spectator");
        first.End(first.Player2);second.End(second.Player2);first.Export();second.Export();
    }

    static void VerifyArenaStartupPreparation(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches CASCADE");
        var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupRecovery");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var now=new DateTime(2051,1,1,20,0,0,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var captured=StatGroup(town);var abandoned=StatGroup(town);
            var ordered=new[]{captured.Player1,captured.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(captured,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
                Call(intents,null,"Capture",captured,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
            bool rejected=false;try{Call(recovery,null,"PrepareStartup",1);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && LedgerRows(captured,"active")==1,"startup orchestration rejects live registries before recovering evidence");
            now=now.AddDays(1);
            Query("CREATE FUNCTION suppress_startup_rank() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_startup_rank BEFORE INSERT ON revora_arena_rank_days FOR EACH ROW EXECUTE FUNCTION suppress_startup_rank()");
            rejected=false;try{ColdArenaRecover(Guid.Empty,false,0,1);}catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_startup_rank ON revora_arena_rank_days;DROP FUNCTION suppress_startup_rank()");}
            Check(rejected && !(bool)recovery.GetProperty("Prepared",Hidden).GetValue(null,null) && LedgerRows(captured,"finished")==1
                && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==1 && Scalar("SELECT count(*) FROM revora_arena_rank_days WHERE day='20510101'",0)==0,
                "ranking failure keeps startup unprepared while retaining independently committed recovery");
            Check(ColdArenaRecover(Guid.Empty,false,0,1) && Scalar("SELECT count(*) FROM revora_arena_rank_rewards",0)==2,
                "startup retry settles recovered results before declaring preparation complete");
            Check(ColdArenaRecover(Guid.Empty,false,0,1) && Scalar("SELECT count(*) FROM revora_arena_result_statistics",0)==2
                && Scalar("SELECT count(*) FROM revora_arena_rank_rewards",0)==2,"repeated startup preparation cannot replay recovery or ranking rewards");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("UPDATE arena SET arenapointfill=0 WHERE entityid=@uid",c)) {
                q.Parameters.AddWithValue("uid",(long)captured.Player1.Entity.UID);q.ExecuteNonQuery();}}
            var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",captured.Player1.Entity.UID);
            Check(loaded.ArenaPoints==1030 && loaded.TodayWin==1 && loaded.TodayBattles==1
                && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",captured.Player1.Entity.UID)==0,
                "durable result ownership prevents legacy reload reset before the first rollover receipt");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("DELETE FROM revora_arena_result_commits WHERE run=@run",c)) {
                q.Parameters.AddWithValue("run",ArenaGuid(captured));q.ExecuteNonQuery();}}
            rejected=false;try{ColdArenaRecover(Guid.Empty,false,0,1);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && !(bool)recovery.GetProperty("Prepared",Hidden).GetValue(null,null),"incomplete finished bundle fails closed even when its ranking day was already sealed");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("INSERT INTO revora_arena_result_commits(run) VALUES(@run)",c)) {
                q.Parameters.AddWithValue("run",ArenaGuid(captured));q.ExecuteNonQuery();}}
            // Synthetic interrupted admissions exercise the bounded startup loop without 258 player sessions.
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(@"INSERT INTO revora_arena_matches(id,player1,player2,map,started_at,state)
SELECT md5('startup-limit-'||n::text)::uuid,@a,@b,701,@now,'active' FROM generate_series(1,129) n",c)) {
                q.Parameters.AddWithValue("a",(long)captured.Player1.Entity.UID);q.Parameters.AddWithValue("b",(long)captured.Player2.Entity.UID);
                q.Parameters.AddWithValue("now",NpgsqlTypes.NpgsqlDbType.TimestampTz,now);q.ExecuteNonQuery();}}
            rejected=false;try{ColdArenaRecover(Guid.Empty,false,0,1);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && !(bool)recovery.GetProperty("Prepared",Hidden).GetValue(null,null)
                && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==129,
                "exhausted startup budget stops after 128 recoveries without claiming readiness");
            Check(ColdArenaRecover(Guid.Empty,false,0,1) && Scalar("SELECT count(*) FROM revora_arena_abandoned",0)==130,
                "next startup pass resumes remaining admissions without reprocessing closed ones");
            rejected=false;try{Call(recovery,null,"PrepareStartup",101);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"startup rejects an unbounded batch budget");
            captured.Export();abandoned.Export();
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaSaveOwnership(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var legacy=StatGroup(town);legacy.Player1.ArenaPoints=222;legacy.Player1.CurrentHonor=99;
        ArenaTable.SaveArenaStatistics(legacy.Player1.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(legacy.Player1.TeamArenaStatistic);
        Check(Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",legacy.Player1.Entity.UID)==222
            && Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",legacy.Player1.Entity.UID)==99,
            "unmanaged players retain existing legacy statistic saves");legacy.Export();
        var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
        var ordered=new[]{a,b}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(group,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
            Call(intents,null,"Capture",group,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
        a.ArenaPoints=9999;a.CurrentHonor=777;a.HistoryHonor=888;
        a.ArenaStatistic.TodayWin=77;a.ArenaStatistic.TodayBattles=88;a.ArenaStatistic.TotalWin=99;a.ArenaStatistic.TotalLose=12;
        a.ArenaStatistic.LastSeasonRank=456;a.ArenaStatistic.LastSeasonArenaPoints=999;a.ArenaStatistic.Name="OwnedSave";
        a.TeamArenaStatistic.Name="OwnedTeam";a.TeamArenaStatistic.TodayWin=3;
        foreach(bool supplied in new[]{false,true}) {
            if(supplied)using(var c=DataHolder.MySqlConnection){c.Open();ArenaTable.SaveArenaStatistics(a.ArenaStatistic,c);TeamArenaTable.SaveArenaStatistics(a.TeamArenaStatistic,c);}
            else{ArenaTable.SaveArenaStatistics(a.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(a.TeamArenaStatistic);}
            Check(Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND arenapoints=1000 AND COALESCE(currenthonor,0)=0 AND COALESCE(historyhonor,0)=0 AND todaywin=0 AND todaybattles=0 AND totalwin=0 AND totallose=0 AND COALESCE(lastseasonrank,0)=0 AND entityname='OwnedSave'",a.Entity.UID)==1
                && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND arenapoints=1000 AND COALESCE(currenthonor,0)=0 AND COALESCE(historyhonor,0)=0 AND todaywin=3 AND entityname='OwnedTeam'",a.Entity.UID)==1,
                "captured outcome protects pending preimages while metadata and independent team counters save: "+supplied);
        }
        var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
        Check(loaded.ArenaPoints==1000 && loaded.TodayWin==0 && loaded.CurrentHonor==0,"pending outcome ownership also protects production reload before result recovery");
        a.ArenaPoints=1000;a.CurrentHonor=0;a.HistoryHonor=0;a.ArenaStatistic.TodayWin=0;a.ArenaStatistic.TodayBattles=0;
        a.ArenaStatistic.TotalWin=0;a.ArenaStatistic.TotalLose=0;
        Check(Call(coordinator,null,"Complete",group).ToString()=="Committed","protected preimage still permits the original captured outcome to commit");
        a.ArenaPoints=1;a.CurrentHonor=900000;a.ArenaStatistic.TodayWin=0;
        Parallel.For(0,8,n=>{ArenaTable.SaveArenaStatistics(a.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(a.TeamArenaStatistic);});
        Check(Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND arenapoints=1030 AND todaywin=1 AND totalwin=1 AND COALESCE(currenthonor,0)=0",a.Entity.UID)==1
            && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND arenapoints=1030 AND COALESCE(currenthonor,0)=0",a.Entity.UID)==1,
            "concurrent stale periodic saves cannot undo a committed match or create Honor");
        Kernel.GamePool.Remove(a.Entity.UID);a.ArenaStatistic.Name="Detached";a.TeamArenaStatistic.Name="Detached";
        try {
            using(var c=DataHolder.MySqlConnection){c.Open();ArenaTable.SaveArenaStatistics(a.ArenaStatistic,c);TeamArenaTable.SaveArenaStatistics(a.TeamArenaStatistic,c);}
            Check(Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND entityname='OwnedSave' AND arenapoints=1030",a.Entity.UID)==1
                && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND entityname='OwnedTeam' AND arenapoints=1030",a.Entity.UID)==1,
                "detached managed session cannot overwrite even metadata during a late final save");
        }finally{Kernel.GamePool[a.Entity.UID]=a;}
        group.Export();
    }

    static Revora.Client.GameState PointBuyer(Map town) {
        var p=ArenaPlayer(town);p.TeamArenaStatistic=new Revora.Network.GamePackets.TeamQualifierStatistic(true){EntityID=p.Entity.UID};
        p.ArenaPoints=0;p.ArenaStatistic.LastArenaPointFill=DateTime.Now;p.Entity.Money=27000000;
        Query("INSERT INTO arena(entityid,entityname,level,class,arenapoints,todaywin,todaybattles,totalwin,totallose,arenapointfill) VALUES(@uid,@name,40,10,0,0,0,0,0,@stamp);INSERT INTO teamarena(entityid,arenapoints) VALUES(@uid,0);UPDATE entities SET money=27000000 WHERE uid=@uid",
            "uid",(long)p.Entity.UID,"name",p.Entity.Name,"stamp",DateTime.Now.Ticks);return p;
    }
    static void VerifyArenaPointPurchases(Map town) {
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaPointPurchases");Call(feature,null,"Initialize");
        var now=DateTime.UtcNow;var clock=feature.GetField("Clock",Hidden);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            var p=PointBuyer(town);var packet=new byte[12];System.BitConverter.GetBytes(5u).CopyTo(packet,4);
            Parallel.For(0,8,n=>Qualifier.Statistics.MsgQualifyingInteractive(packet,p));
            Check(p.ArenaPoints==1500 && p.TeamArenaStatistic.ArenaPoints==1500 && p.Entity.Money==18000000
                && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",p.Entity.UID)==1
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==18000000,
                "ordinary Arena buy packets atomically charge once and update both point mirrors");
            var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(loaded.ArenaPoints==1500 && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1500,
                "production reload and team row retain purchased points");
            now=now.AddSeconds(-5);Call(feature,null,"Buy",p);
            Check(p.Entity.Money==18000000 && p.ArenaPoints==1500,"persisted purchase cooldown rejects a clock rollback replay");
            now=now.AddSeconds(6);Call(feature,null,"Buy",p);Call(feature,null,"Buy",p);
            Check(p.Entity.Money==9000000 && p.ArenaPoints==3000 && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",p.Entity.UID)==2,
                "existing 1500-point eligibility permits one later refill and rejects purchases above the threshold");
            foreach(string table in new[]{"arena","teamarena","entities","revora_arena_point_purchases"}) {
                var fault=PointBuyer(town);string action=table=="revora_arena_point_purchases"?"INSERT":"UPDATE";
                Query("CREATE FUNCTION suppress_points_purchase() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_points_purchase BEFORE "+action+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_points_purchase()");
                try{Call(feature,null,"Buy",fault);}finally{Query("DROP TRIGGER suppress_points_purchase ON "+table+";DROP FUNCTION suppress_points_purchase()");}
                Check(Fenced(fault) && fault.Entity.Money==27000000 && fault.ArenaPoints==0
                    && Scalar("SELECT money FROM entities WHERE uid=@uid",fault.Entity.UID)==27000000
                    && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",fault.Entity.UID)==0
                    && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",fault.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",fault.Entity.UID)==0,
                    "purchase failure rolls back silver, both point rows and receipt: "+table);
                DemonReset(fault);Call(feature,null,"Buy",fault);
                Check(fault.Entity.Money==18000000 && fault.ArenaPoints==1500,"purchase retries after failed transaction: "+table);
            }
            var poor=PointBuyer(town);poor.Entity.Money=8999999;Call(feature,null,"Buy",poor);
            Check(!Fenced(poor) && poor.ArenaPoints==0 && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",poor.Entity.UID)==0,
                "insufficient silver cannot buy Arena points");
            var pending=StatGroup(town);var owner=pending.Player1;owner.Entity.Money=27000000;
            Query("UPDATE entities SET money=27000000 WHERE uid=@uid","uid",(long)owner.Entity.UID);
            Call(feature,null,"Buy",owner);Check(owner.ArenaPoints==1000 && owner.Entity.Money==27000000,"active fight cannot change its captured point base through purchase");
            pending.Export();Call(feature,null,"Buy",owner);
            Check(owner.ArenaPoints==1000 && owner.Entity.Money==27000000 && !Fenced(owner),"unresolved durable admission blocks purchase even after its live group is removed");
            ColdArenaRecover(Guid.Empty,false,128);Call(feature,null,"Buy",owner);
            Check(owner.ArenaPoints==2500 && owner.Entity.Money==18000000,"closed uncaptured interruption allows ordinary point purchase again");
        }finally{clock.SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaLoginRecovery(Map town) {
        var p=PointBuyer(town);Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaPointPurchases"),null,"Buy",p);
        Query("UPDATE teamarena SET todaywin=300,todaybattles=400,currenthonor=250,historyhonor=350,lastseasonrank=7,lastseasonarenapoints=1200 WHERE entityid=@uid;UPDATE arena SET currenthonor=250,historyhonor=350 WHERE entityid=@uid",
            "uid",(long)p.Entity.UID);
        Qualifier.ArenaStatistics.Remove(p.Entity.UID);TeamQualifier.ArenaStatistics.Remove(p.Entity.UID);
        var solos=new object[8];var teams=new object[8];
        Parallel.For(0,8,n=>{solos[n]=Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID);teams[n]=Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID);});
        var solo=(Revora.Network.GamePackets.QualifierStatistic)solos[0];var team=(Revora.Network.GamePackets.TeamQualifierStatistic)teams[0];
        Check(solos.All(x=>ReferenceEquals(x,solo)) && teams.All(x=>ReferenceEquals(x,team))
            && solo.EntityID==p.Entity.UID && team.EntityID==p.Entity.UID,
            "concurrent login recovery publishes one canonical solo and team statistic object");
        Check(solo.ArenaPoints==1500 && team.ArenaPoints==1500 && solo.CurrentHonor==250 && team.CurrentHonor==250
            && team.HistoryHonor==350 && team.TodayWin==300 && team.TodayBattles==400 && team.LastSeasonRank==7 && team.LastSeasonArenaPoints==1200,
            "cache-miss recovery preserves purchased points, Honor and full-width team counters");
        Check(Scalar("SELECT count(*) FROM arena WHERE entityid=@uid",p.Entity.UID)==1 && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1,
            "recovery does not insert duplicate persisted Arena identities");
        solo.ArenaPoints=1450;team.TodayBattles=401;
        Check(ReferenceEquals(Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID),solo)
            && ReferenceEquals(Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID),team)
            && solo.ArenaPoints==1450 && team.TodayBattles==401,"valid live cache survives recovery without erasing unflushed changes");
        Qualifier.ArenaStatistics[p.Entity.UID]=new Revora.Network.GamePackets.QualifierStatistic(true);
        TeamQualifier.ArenaStatistics[p.Entity.UID]=new Revora.Network.GamePackets.TeamQualifierStatistic(true){EntityID=p.Entity.UID+1};
        solo=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID);
        team=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID);
        Check(solo.EntityID==p.Entity.UID && team.EntityID==p.Entity.UID && solo.ArenaPoints==1500 && team.TodayBattles==400,
            "zero and foreign cache identities are replaced by the owner's saved rows");
        var cache=TeamQualifier.ArenaStatistics;
        TeamQualifier.ArenaStatistics=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Network.GamePackets.TeamQualifierStatistic>();
        try{TeamArenaTable.Load();Check(TeamQualifier.ArenaStatistics[p.Entity.UID].TodayWin==300 && TeamQualifier.ArenaStatistics[p.Entity.UID].TodayBattles==400,
            "startup team loader preserves counters above the old byte limit");}finally{TeamQualifier.ArenaStatistics=cache;}
        Check(Call(typeof(TeamArenaTable),null,"RecoverExisting",0u)==null
            && Call(typeof(TeamArenaTable),null,"RecoverExisting",uint.MaxValue)==null,"missing team identity is left for genuine first-time creation");
        Query("INSERT INTO teamarena(entityid,arenapoints) VALUES(@uid,50)","uid",(long)p.Entity.UID);
        bool rejected=false;try{Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"ambiguous duplicate persisted team identities fail closed");
        Query("DELETE FROM teamarena WHERE entityid=@uid AND arenapoints=50","uid",(long)p.Entity.UID);
    }

    static void VerifyArenaCacheFailure(Map town) {
        var purchases=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaPointPurchases");
        var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var p=PointBuyer(town);Call(purchases,null,"Buy",p);var oldSolo=p.ArenaStatistic;var oldTeam=p.TeamArenaStatistic;
        Qualifier.ArenaStatistics[p.Entity.UID]=oldSolo;TeamQualifier.ArenaStatistics[p.Entity.UID]=oldTeam;
        // Model a cache projection failing after a real purchase commit. No fake reward row is inserted.
        p.ArenaPoints=1;p.CurrentHonor=900000;
        Call(recovery,null,"Fail",p,"arena-projection-fixture",new InvalidOperationException("fixture projection"));
        Check(Fenced(p) && !Qualifier.ArenaStatistics.ContainsKey(p.Entity.UID) && !TeamQualifier.ArenaStatistics.ContainsKey(p.Entity.UID)
            && (bool)Call(recovery,null,"RequiresReload",oldSolo) && (bool)Call(recovery,null,"RequiresReload",oldTeam),
            "failed projection fences both cached statistic identities before reconnect");
        var solo=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID);
        var team=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID);
        Check(!ReferenceEquals(solo,oldSolo) && !ReferenceEquals(team,oldTeam) && solo.ArenaPoints==1500 && team.ArenaPoints==1500
            && solo.CurrentHonor==0 && team.CurrentHonor==0 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==18000000
            && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",p.Entity.UID)==1,
            "login recovery uses the committed purchase rather than stale valid-ID cache objects");
        Kernel.GamePool.Remove(p.Entity.UID);oldSolo.Name="Expired";oldTeam.Name="Expired";
        try{
            ArenaTable.SaveArenaStatistics(oldSolo);TeamArenaTable.SaveArenaStatistics(oldTeam);
            using(var c=DataHolder.MySqlConnection){c.Open();ArenaTable.SaveArenaStatistics(oldSolo,c);TeamArenaTable.SaveArenaStatistics(oldTeam,c);}
            Check(Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1500
                && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1500
                && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND entityname='Expired'",p.Entity.UID)==0,
                "both final-save overloads reject invalid detached objects even without managed rollover ownership");
        }finally{Kernel.GamePool[p.Entity.UID]=p;}
        Call(recovery,null,"Fail",p,"arena-late-failure-fixture",new InvalidOperationException("late fixture failure"));
        Check(ReferenceEquals(Qualifier.ArenaStatistics[p.Entity.UID],solo) && ReferenceEquals(TeamQualifier.ArenaStatistics[p.Entity.UID],team),
            "late failure from the old session cannot evict replacement statistics");
        Qualifier.ArenaStatistics[p.Entity.UID]=oldSolo;TeamQualifier.ArenaStatistics[p.Entity.UID]=oldTeam;
        Check(!ReferenceEquals(Call(typeof(ArenaTable),null,"RecoverExisting",p.Entity.UID),oldSolo)
            && !ReferenceEquals(Call(typeof(TeamArenaTable),null,"RecoverExisting",p.Entity.UID),oldTeam),
            "reintroduced invalid cache objects cannot pass the valid-identity fast path");
        var fault=PointBuyer(town);Qualifier.ArenaStatistics[fault.Entity.UID]=fault.ArenaStatistic;TeamQualifier.ArenaStatistics[fault.Entity.UID]=fault.TeamArenaStatistic;
        Query("CREATE FUNCTION suppress_cache_purchase() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_cache_purchase BEFORE INSERT ON revora_arena_point_purchases FOR EACH ROW EXECUTE FUNCTION suppress_cache_purchase()");
        try{Call(purchases,null,"Buy",fault);}finally{Query("DROP TRIGGER suppress_cache_purchase ON revora_arena_point_purchases;DROP FUNCTION suppress_cache_purchase()");}
        Check(Fenced(fault) && (bool)Call(recovery,null,"RequiresReload",fault.ArenaStatistic)
            && !Qualifier.ArenaStatistics.ContainsKey(fault.Entity.UID) && !TeamQualifier.ArenaStatistics.ContainsKey(fault.Entity.UID)
            && Scalar("SELECT money FROM entities WHERE uid=@uid",fault.Entity.UID)==27000000,
            "actual purchase exception invalidates caches while retaining the rolled-back purse");
        var honor=PointBuyer(town);honor.CurrentHonor=50000;honor.HistoryHonor=50000;honor.TeamArenaStatistic.TodayWin=7;
        Query("CREATE FUNCTION suppress_cache_honor() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_cache_honor BEFORE INSERT ON revora_honor_purchases FOR EACH ROW EXECUTE FUNCTION suppress_cache_honor()");
        try{Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases"),null,"Buy",honor,720842u);}
        finally{Query("DROP TRIGGER suppress_cache_honor ON revora_honor_purchases;DROP FUNCTION suppress_cache_honor()");}
        var retained=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"RecoverExisting",honor.Entity.UID);
        Check(Fenced(honor) && retained.TodayWin==7 && retained.CurrentHonor==50000
            && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",honor.Entity.UID)==50000
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid",honor.Entity.UID)==0,
            "failed Honor purchase preserves previously earned team counters and mirrored Honor before cache recovery");
    }

    static void VerifyArenaSessionRelease(Map town) {
        var old=ArenaPlayer(town);int started=0;
        Parallel.For(0,12,n=>{if((bool)Call(typeof(Revora.Client.GameState),old,"TryBeginShutdown"))System.Threading.Interlocked.Increment(ref started);});
        Check(started==1,"concurrent shutdown callbacks elect one final-save owner");
        Kernel.DisconnectPool[old.Entity.UID]=old;
        Call(typeof(Revora.Client.GameState),old,"ReleaseSessionRegistration",false);
        Check(!Kernel.GamePool.ContainsKey(old.Entity.UID) && ReferenceEquals(Kernel.DisconnectPool[old.Entity.UID],old),
            "early disconnect release retains its login barrier until final cleanup");
        var newer=ArenaPlayer(town);uint originalUid=newer.Entity.UID;Kernel.GamePool.Remove(originalUid);newer.Entity.UID=old.Entity.UID;
        Kernel.GamePool[old.Entity.UID]=newer;
        Call(typeof(Revora.Client.GameState),old,"ReleaseSessionRegistration",true);
        Check(ReferenceEquals(Kernel.GamePool[old.Entity.UID],newer) && !Kernel.DisconnectPool.ContainsKey(old.Entity.UID),
            "old final cleanup releases its barrier without removing the newer session");
        Kernel.DisconnectPool[old.Entity.UID]=newer;
        Parallel.For(0,12,n=>Call(typeof(Revora.Client.GameState),old,"ReleaseSessionRegistration",true));
        Check(ReferenceEquals(Kernel.GamePool[old.Entity.UID],newer) && ReferenceEquals(Kernel.DisconnectPool[old.Entity.UID],newer),
            "repeated old callbacks preserve both registrations owned by a replacement");
        Check((bool)Call(typeof(Revora.Client.GameState),newer,"TryBeginShutdown"),"replacement owns an independent shutdown lifecycle");
        Call(typeof(Revora.Client.GameState),newer,"ReleaseSessionRegistration",true);
        Check(!Kernel.GamePool.ContainsKey(old.Entity.UID) && !Kernel.DisconnectPool.ContainsKey(old.Entity.UID),
            "replacement final cleanup removes its own registrations");
    }

    static void VerifyArenaDeferredDeparture(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var reason=intents.GetNestedType("Cause",Hidden);var disconnect=Enum.Parse(reason,"Disconnect");var forfeit=Enum.Parse(reason,"Forfeit");
        var now=new DateTime(2052,1,2,7,59,59,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            foreach(bool holdMatch in new[]{false,true}) {
                now=new DateTime(2052,1,2,7,59,59,DateTimeKind.Utc);
                var group=StatGroup(town);group.Done=false;group.Winner=null;group.Loser=null;
                var heldLock=holdMatch?Field(group,"ParticipationSync"):group.Player2.ItemSyncRoot;
                using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                    var task=Task.Run(()=>{lock(heldLock){held.Set();release.Wait();}});held.Wait();
                    try {
                        Check(Call(coordinator,null,"EndForPlayer",group,group.Player2,disconnect).ToString()=="Busy" && !group.Done,
                            "busy departure stores a request without freezing or releasing owners: "+holdMatch);
                        Check(Call(coordinator,null,"EndForPlayer",group,group.Player1,forfeit).ToString()=="Busy",
                            "competing departure cannot replace the first request while its locks are busy");
                    }finally{release.Set();task.Wait();}
                }
                group.UpdateDamage(group.Player1,1000);var spectator=ArenaPlayer(town);group.BeginWatching(spectator);
                Check(group.Player1Damage==0 && spectator.WatchingGroup==null,"pending departure blocks later score updates and spectator admission");
                group.End(spectator);Check(!group.Done,"foreign ending cannot freeze even a match with a pending request");
                now=now.AddSeconds(2);
                Check(Call(coordinator,null,"EndTimedOut",group).ToString()=="Committed" && ReferenceEquals(group.Loser,group.Player2),
                    "lifecycle timeout pass honors the first departure even before the fight timeout");
                Check(Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='Disconnect' AND day='20520101'",group.Player2.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_arena_matches WHERE loser=@uid AND day='20520101' AND state='finished'",group.Player2.Entity.UID)==1,
                    "deferred commit preserves the original cause and Pacific reward day across midnight");
                Check(Call(coordinator,null,"RetryDeparture",group).ToString()=="AlreadyCommitted",
                    "repeated departure retry recognizes its single durable result");group.Export();
            }
            now=now.AddDays(1);var retry=StatGroup(town);retry.Done=false;retry.Winner=null;retry.Loser=null;
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(retry.Player1.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Call(coordinator,null,"EndForPlayer",retry,retry.Player1,forfeit);}finally{release.Set();task.Wait();}
            }
            retry.End(retry.Player2);
            Check(ReferenceEquals(retry.Loser,retry.Player1) && Call(coordinator,null,"RetryDeparture",retry).ToString()=="Committed"
                && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='Forfeit'",retry.Player1.Entity.UID)==1,
                "an intervening legacy ending cannot replace a previously accepted departure");retry.Export();
            var untouched=StatGroup(town);Check(Call(coordinator,null,"RetryDeparture",untouched).ToString()=="NotReady",
                "retry without a request cannot manufacture a departure outcome");untouched.Export();
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaFencedRecovery(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
        Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady","fenced recovery cannot take over healthy gameplay owners");
        for(int n=0;n<8;n++) {
            if(Call(coordinator,null,"Complete",group).ToString()!="Committed")throw new Exception("Recovery fixture result did not commit.");
            group.Export();group=new Group(a,b);group.Import();Call(ArenaLedger,null,"Start",group);group.End(b);
        }
        Query("CREATE FUNCTION suppress_fenced_bundle() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_fenced_bundle BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_fenced_bundle()");
        try {
            Check(Call(coordinator,null,"Complete",group).ToString()=="Failed" && Fenced(a) && Fenced(b) && a.Action==0 && b.Action==0,
                "actual ninth-result failure retains both canonical fenced owners");
            Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Failed" && LedgerRows(group,"active")==1
                && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",a.Entity.UID)==8
                && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==0,
                "failed fenced recovery rolls back statistics and milestone alongside the bundle");
        }finally{Query("DROP TRIGGER suppress_fenced_bundle ON revora_arena_result_commits;DROP FUNCTION suppress_fenced_bundle()");}
        var replacement=ArenaPlayer(town);Kernel.GamePool[b.Entity.UID]=replacement;
        try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && LedgerRows(group,"active")==1,
            "replaced session cannot authorize recovery for the old owners");}finally{Kernel.GamePool[b.Entity.UID]=b;}
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Busy" && LedgerRows(group,"active")==1,
                "fenced recovery defers a busy owner without blocking teardown locks");}finally{release.Set();task.Wait();}
        }
        int committed=0;Parallel.For(0,8,n=>{if(Call(coordinator,null,"RecoverFenced",group).ToString()=="Committed")System.Threading.Interlocked.Increment(ref committed);});
        Check(committed==1 && LedgerRows(group,"finished")==1 && a.ArenaStatistic.TodayWin==8 && Fenced(a) && Fenced(b)
            && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",a.Entity.UID)==9
            && Scalar("SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND milestone='wins9' AND paid_at IS NULL",a.Entity.UID)==1,
            "fenced recovery commits the ninth win and pending prize once without re-enabling stale sessions");
        Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="AlreadyCommitted",
            "fenced recovery recognizes the complete saved bundle on replay");
        var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"RecoverExisting",a.Entity.UID);
        Check(loaded.TodayWin==9 && loaded.TotalWin==9 && loaded.ArenaPoints==(uint)Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",a.Entity.UID),
            "production recovery loader reads the repaired result while old caches remain fenced");
        Call(typeof(Revora.Client.GameState),a,"ReleaseSessionRegistration",true);Call(typeof(Revora.Client.GameState),b,"ReleaseSessionRegistration",true);
        Check(Call(coordinator,null,"Cleanup",group).ToString()=="Committed" && !Qualifier.QualifierList.Groups.ContainsKey(group.ID),
            "committed fenced result permits map cleanup after both session registrations are released");
        var missing=StatGroup(town);
        Call(cache,null,"Fail",missing.Player1,"arena-missing-intent-fixture",new InvalidOperationException("fixture"));
        Call(cache,null,"Fail",missing.Player2,"arena-missing-intent-fixture",new InvalidOperationException("fixture"));
        Check(Call(coordinator,null,"RecoverFenced",missing).ToString()=="NotReady" && LedgerRows(missing,"active")==1,
            "fenced RAM outcome without a durable intent cannot manufacture a recovered result");
    }

    static void VerifyArenaCaptureRecovery(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var now=new DateTime(2053,1,2,7,59,59,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            foreach(string table in new[]{"revora_arena_result_intents","revora_arena_outcome_snapshots"}) {
                now=new DateTime(2053,1,2,7,59,59,DateTimeKind.Utc);var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
                Query("CREATE FUNCTION suppress_initial_capture() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_initial_capture BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_initial_capture()");
                try {
                    Check(Call(coordinator,null,"Complete",group).ToString()=="Failed" && Fenced(a) && Fenced(b)
                        && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid",a.Entity.UID)==0,
                        "initial capture failure retains fenced owners but no partial durable intent: "+table);
                    Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Failed" && LedgerRows(group,"active")==1,
                        "continued capture failure cannot manufacture a completed result: "+table);
                }finally{Query("DROP TRIGGER suppress_initial_capture ON "+table+";DROP FUNCTION suppress_initial_capture()");}
                now=now.AddSeconds(2);
                Query("UPDATE arena SET arenapoints=1001 WHERE entityid=@uid","uid",(long)a.Entity.UID);
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Failed"
                    && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid",a.Entity.UID)==0
                    && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",a.Entity.UID)==1001,
                    "fenced recapture rejects changed durable preimages without replacing them: "+table);
                Query("UPDATE arena SET arenapoints=1000 WHERE entityid=@uid","uid",(long)a.Entity.UID); // Restore fixture preimage only.
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Committed" && Fenced(a) && Fenced(b)
                    && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='Result' AND day='20530101'",a.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_arena_matches WHERE winner=@uid AND state='finished' AND day='20530101'",a.Entity.UID)==1,
                    "strict recapture completes with the original observation day while owners remain fenced: "+table);
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="AlreadyCommitted"
                    && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",a.Entity.UID)==1,
                    "recaptured result replays without duplicate statistics: "+table);
                Call(typeof(Revora.Client.GameState),a,"ReleaseSessionRegistration",true);Call(typeof(Revora.Client.GameState),b,"ReleaseSessionRegistration",true);
                Call(coordinator,null,"Cleanup",group);
            }
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyArenaCompletionPump(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCompletionPump");
        var pump=Activator.CreateInstance(type,Instance,null,new object[]{2},null);
        var now=new DateTime(2054,1,1,20,0,0,DateTimeKind.Utc);type.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
        var first=StatGroup(town);var second=StatGroup(town);var third=StatGroup(town);
        Check((bool)Call(type,pump,"Track",first) && (bool)Call(type,pump,"Track",first) && (bool)Call(type,pump,"Track",second)
            && !(bool)Call(type,pump,"Track",third) && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==2,
            "completion queue deduplicates matches and enforces its capacity without touching rejected work");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(first.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try {
                Check((int)Call(type,pump,"Tick",1)==0 && !(bool)Call(type,null,"ResultReady",first),"busy first match stays owned and unready");
                Check((int)Call(type,pump,"Tick",1)==1 && (bool)Call(type,null,"ResultReady",second)
                    && !Qualifier.QualifierList.Groups.ContainsKey(second.ID),"round-robin retry lets another match commit and clean up despite a busy predecessor");
            }finally{release.Set();task.Wait();}
        }
        Check((bool)Call(type,pump,"Track",third) && (int)Call(type,pump,"Tick",2)==1 && !(bool)Call(type,null,"ResultReady",first),
            "freed queue capacity is reusable while a deferred entry respects its retry time");
        now=now.AddSeconds(1);
        Check((int)Call(type,pump,"Tick",2)==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==0
            && (bool)Call(type,null,"ResultReady",first),"released owner completes on the next due retry and leaves sticky result proof");
        Check((bool)Call(type,pump,"Track",first) && (int)Call(type,pump,"Tick",2)==0,
            "tracking an already completed match cannot enqueue or pay it again");
        var failed=StatGroup(town);Call(type,pump,"Track",failed);
        Query("CREATE SEQUENCE pump_fail_attempts;CREATE FUNCTION suppress_pump_bundle() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN PERFORM nextval(''pump_fail_attempts''); RETURN NULL; END';CREATE TRIGGER suppress_pump_bundle BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_pump_bundle()");
        try {
            Check((int)Call(type,pump,"Tick",1)==0 && Fenced(failed.Player1) && Fenced(failed.Player2)
                && Scalar("SELECT last_value FROM pump_fail_attempts",0)==1,"pump retains a failed result and its fenced owners");
            Call(type,pump,"Tick",1);Check(Scalar("SELECT last_value FROM pump_fail_attempts",0)==1,"repeated ticks do not hammer the failed database write");
            now=now.AddSeconds(1);Call(type,pump,"Tick",1);
            now=now.AddSeconds(1);Call(type,pump,"Tick",1);
            Check(Scalar("SELECT last_value FROM pump_fail_attempts",0)==2,"second failure increases the retry delay to two seconds");
            now=now.AddSeconds(-5);Call(type,pump,"Tick",1);Call(type,pump,"Tick",1);
            Check(Scalar("SELECT last_value FROM pump_fail_attempts",0)==3,"clock rollback allows one fresh attempt without a repeated-tick loop");
        }finally{Query("DROP TRIGGER suppress_pump_bundle ON revora_arena_result_commits;DROP FUNCTION suppress_pump_bundle();DROP SEQUENCE pump_fail_attempts");}
        now=now.AddSeconds(30);
        Check((int)Call(type,pump,"Tick",1)==0 && (bool)Call(type,null,"ResultReady",failed)
            && LedgerRows(failed,"finished")==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==1,
            "pump repairs the durable result but retains cleanup until fenced sessions are released");
        Call(typeof(Revora.Client.GameState),failed.Player1,"ReleaseSessionRegistration",true);
        Call(typeof(Revora.Client.GameState),failed.Player2,"ReleaseSessionRegistration",true);now=now.AddSeconds(1);
        Check((int)Call(type,pump,"Tick",1)==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==0
            && (bool)Call(type,null,"ResultReady",failed),"post-release retry cleans the map without replaying the repaired result");
        bool rejected=false;try{Call(type,pump,"Tick",129);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"completion pump rejects an unbounded per-tick budget");
    }

    static void VerifyArenaRolloverIdleOwnership(Map town) {
        var rollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDayRollover");
        var p=PointBuyer(town);var other=PointBuyer(town);p.Entity.Level=140;p.ArenaStatistic.Level=140;p.TeamArenaStatistic.Level=140;
        Query("UPDATE entities SET level=140 WHERE uid=@uid","uid",(long)p.Entity.UID);
        var solo=new Group(p,other);var team=new TeamQualifier.QualifierList.QualifierGroup(p,other);
        try {
            foreach(string state in new[]{"team-match","solo-watch","team-watch","solo-invitation","team-invitation","disconnect"}) {
                if(state=="team-match")p.TeamQualifierGroup=team;
                if(state=="solo-watch")p.WatchingGroup=solo;
                if(state=="team-watch")p.TeamWatchingGroup=team;
                if(state=="solo-invitation")p.ArenaStatistic.PlayWith=other;
                if(state=="team-invitation")p.TeamArenaStatistic.PlayWith=other;
                if(state=="disconnect")Kernel.DisconnectPool[p.Entity.UID]=p;
                try {
                    Check(!(bool)Call(rollover,null,"Prepare",p) && !Fenced(p) && p.ArenaPoints==0 && p.TeamArenaStatistic.ArenaPoints==0
                        && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==0
                        && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==0,
                        "day rollover preserves both point mirrors while player is not idle: "+state);
                }finally {
                    p.TeamQualifierGroup=null;p.WatchingGroup=null;p.TeamWatchingGroup=null;
                    p.ArenaStatistic.PlayWith=null;p.TeamArenaStatistic.PlayWith=null;Kernel.DisconnectPool.Remove(p.Entity.UID);
                }
            }
            Check((bool)Call(rollover,null,"Prepare",p) && p.ArenaPoints==4000 && p.TeamArenaStatistic.ArenaPoints==4000
                && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                "ordinary idle player receives the deferred day fill after match and disconnect ownership clear");
        }finally{Qualifier.QualifierList.Groups.Remove(solo.ID);TeamQualifier.QualifierList.Groups.Remove(team.ID);}
    }

    static void VerifyLegacyTeamResultBounds(Map town) {
        var p=PointBuyer(town);p.ArenaPoints=100000;
        Call(typeof(TeamQualifier),null,"ApplyLegacyResult",p,false,40u);
        Check(p.ArenaPoints==99960 && p.TeamArenaStatistic.ArenaPoints==99960
            && p.TeamArenaStatistic.TodayBattles==1 && p.TeamArenaStatistic.TotalLose==1,
            "team loss preserves legitimate balances above eighty thousand and counts one battle");
        p.ArenaPoints=5;Call(typeof(TeamQualifier),null,"ApplyLegacyResult",p,false,40u);
        Check(p.ArenaPoints==0 && p.TeamArenaStatistic.ArenaPoints==0,
            "team loss clamps at zero without unsigned wraparound");
        p.ArenaPoints=1000;Call(typeof(TeamQualifier),null,"ApplyLegacyResult",p,true,30u);
        Check(p.ArenaPoints==1030 && p.TeamArenaStatistic.TodayBattles==3 && p.TeamArenaStatistic.TodayWin==1
            && p.TeamArenaStatistic.TotalWin==1 && p.TeamArenaStatistic.TotalLose==2,
            "team victory counts the completed battle as well as the win");
        ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
        var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
        Check(saved.ArenaPoints==1030 && saved.TodayBattles==3 && saved.TodayWin==1 && saved.TotalWin==1 && saved.TotalLose==2
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1030,
            "ordinary legacy team results survive production saves and row reload with shared points");
        p.ArenaPoints=uint.MaxValue-2;p.TeamArenaStatistic.TodayBattles=uint.MaxValue;
        p.TeamArenaStatistic.TodayWin=uint.MaxValue;p.TeamArenaStatistic.TotalWin=uint.MaxValue;
        p.TeamArenaStatistic.TotalLose=uint.MaxValue;
        Call(typeof(TeamQualifier),null,"ApplyLegacyResult",p,true,49u);
        Check(p.ArenaPoints==uint.MaxValue && p.TeamArenaStatistic.TodayBattles==uint.MaxValue
            && p.TeamArenaStatistic.TodayWin==uint.MaxValue && p.TeamArenaStatistic.TotalWin==uint.MaxValue,
            "team victory saturates full-width points and counters instead of wrapping");
        Call(typeof(TeamQualifier),null,"ApplyLegacyResult",p,false,20u);
        Check(p.ArenaPoints==uint.MaxValue-20 && p.TeamArenaStatistic.TotalLose==uint.MaxValue,
            "team loss preserves full-width balances and saturated lifetime losses");
    }

    static void VerifyTeamOutcomeOwnership(Map town) {
        var first=PointBuyer(town);var second=PointBuyer(town);var stranger=PointBuyer(town);
        var group=new TeamQualifier.QualifierList.QualifierGroup(first,second);
        try {
            group.End(null);group.End(stranger);
            Check(!group.Done && !group.Finished && group.Winner==null && group.Loser==null,
                "team outcome ignores null and unrelated sessions before reading mutable team state");
            var replacement=PointBuyer(town);var replacementUid=replacement.Entity.UID;replacement.Entity.UID=first.Entity.UID;
            group.End(replacement);replacement.Entity.UID=replacementUid;
            Check(!group.Done,"same-UID replacement cannot end the original team match");
            first.Team=null;group.CheckEnd(first,true);
            Check(group.Done && group.Finished && ReferenceEquals(group.Loser,first) && ReferenceEquals(group.Winner,second),
                "original team leader can complete its departure after team membership is gone");
            group.End(second);group.End();
            Check(ReferenceEquals(group.Loser,first) && ReferenceEquals(group.Winner,second),
                "later timeout and opposing departure cannot overwrite the frozen team outcome");
        }finally{TeamQualifier.QualifierList.Groups.Remove(group.ID);}
        var race=new TeamQualifier.QualifierList.QualifierGroup(first,second);
        try {
            Parallel.For(0,24,n=>{if(n%3==0)race.End();else race.End(n%3==1?first:second);});
            var winner=race.Winner;var loser=race.Loser;
            Check(race.Done && race.Finished && !ReferenceEquals(winner,loser)
                && (ReferenceEquals(winner,first)||ReferenceEquals(winner,second))
                && (ReferenceEquals(loser,first)||ReferenceEquals(loser,second)),
                "competing team endings freeze one complete participant pair");
            race.End(winner);
            Check(ReferenceEquals(race.Winner,winner) && ReferenceEquals(race.Loser,loser),
                "frozen team result remains stable after concurrent ending callbacks");
        }finally{TeamQualifier.QualifierList.Groups.Remove(race.ID);}
        var timeout=new TeamQualifier.QualifierList.QualifierGroup(first,second){Player1Damage=100,Player2Damage=99};
        try{timeout.End();Check(ReferenceEquals(timeout.Winner,first),"team timeout preserves higher-damage victory");}
        finally{TeamQualifier.QualifierList.Groups.Remove(timeout.ID);}
    }

    static void VerifyTeamAdmittedDamage(Map town) {
        var a=PointBuyer(town);var b=PointBuyer(town);var member=PointBuyer(town);var outsider=PointBuyer(town);
        foreach(var p in new[]{a,b,member,outsider}){p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;}
        a.Team=new Revora.Game.ConquerStructures.Team(a);a.Team.Add(member);
        b.Team=new Revora.Game.ConquerStructures.Team(b);
        var group=new TeamQualifier.QualifierList.QualifierGroup(a,b);
        try {
            group.Import();var map=a.Map;
            var admitted=(Revora.Client.GameState[])Call(group.GetType(),group,"Participants",true);
            Check(map.BaseID==700 && map.ID!=700 && ReferenceEquals(member.Map,map) && admitted.Length==2
                && admitted.Contains(a) && admitted.Contains(member),"team import freezes the ordinary players actually admitted to its map");
            admitted[0]=outsider;
            Check(!((Revora.Client.GameState[])Call(group.GetType(),group,"Participants",true)).Contains(outsider),
                "team participant snapshot cannot be mutated by its caller");
            group.Import();Check(ReferenceEquals(a.Map,map),"repeated team import cannot allocate another fight map");
            group.UpdateDamage(member,100);group.UpdateDamage(b,40);group.UpdateDamage(member,10,true);
            Check(group.Player1Damage==100 && group.Player2Damage==50,"member damage uses the admitted side and explicit opposite-side credit");
            outsider.Entity.Teleport(700,map.ID,50,50);outsider.TeamQualifierGroup=group;
            group.UpdateDamage(outsider,999);
            Check(group.Player1Damage==100 && group.Player2Damage==50,"same-map outsider with a forged group reference cannot score team damage");
            a.Team.Add(outsider);
            a.Team=null;member.Team=null;
            group.UpdateDamage(member,25);
            Check(group.Player1Damage==125,"team disband cannot reassign an admitted member to the opposing side");
            Kernel.GamePool.Remove(member.Entity.UID);group.UpdateDamage(member,999);Kernel.GamePool[member.Entity.UID]=member;
            Check(group.Player1Damage==125,"departed session cannot continue scoring team damage");
            Parallel.For(0,40,n=>group.UpdateDamage(member,1));
            Check(group.Player1Damage==165,"concurrent admitted team damage increments are serialized");
            group.UpdateDamage(member,uint.MaxValue);group.UpdateDamage(member,1);
            Check(group.Player1Damage==uint.MaxValue,"team damage saturates instead of wrapping at the wire field limit");
            group.End();group.UpdateDamage(b,uint.MaxValue);
            Check(ReferenceEquals(group.Winner,a) && group.Player2Damage==50,"frozen team outcome rejects late damage");
            a.TeamArenaStatistic.PlayWith=b;b.TeamArenaStatistic.PlayWith=a;
            member.TeamArenaStatistic.HasBox=true;member.TeamArenaStatistic.Status=1;
            TeamQualifier.PlayerList[member.Entity.UID]=member;TeamQualifier.WaitingPlayerList[member.Entity.UID]=member;
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(member.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{group.Export();Check(ReferenceEquals(a.Map,map) && a.ArenaPoints==1000 && TeamQualifier.QualifierList.Groups.ContainsKey(group.ID),
                    "busy admitted owner defers team rewards and map cleanup");}finally{release.Set();task.Wait();}
            }
            group.Export();
            Check(a.ArenaPoints>=1030 && a.ArenaPoints<=1049 && member.ArenaPoints==a.ArenaPoints-10
                && b.ArenaPoints==2000-a.ArenaPoints && outsider.ArenaPoints==1000
                && outsider.TeamArenaStatistic.TodayBattles==0,
                "disbanded admitted team earns original leader/member awards while late joiner earns nothing");
            Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && member.Entity.MapID==1002
                && a.TeamQualifierGroup==null && member.TeamQualifierGroup==null && !Kernel.Maps.ContainsKey(map.ID),
                "team export returns disbanded admitted players before disposing the owned map");
            Check(!member.TeamArenaStatistic.HasBox && member.TeamArenaStatistic.Status==0
                && !TeamQualifier.PlayerList.ContainsKey(member.Entity.UID) && !TeamQualifier.WaitingPlayerList.ContainsKey(member.Entity.UID),
                "team export releases admitted member invitation and exact-session queue state");
            uint paid=a.ArenaPoints;group.Export();TeamQualifier.Win(a,b,group);
            Check(a.ArenaPoints==paid && a.TeamArenaStatistic.TodayWin==1 && member.TeamArenaStatistic.TodayWin==1,
                "repeated team export and winner callback cannot replay the admitted reward");
            foreach(var p in new[]{a,b,member}){ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);}
            var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",member.Entity.UID);
            Check(saved.ArenaPoints==member.ArenaPoints && saved.TodayWin==1 && saved.TodayBattles==1,
                "admitted ordinary member reward survives production team save and reload");
        }finally {
            foreach(var p in new[]{a,b,member,outsider}){p.Entity.Teleport(1002,408,330);p.TeamQualifierGroup=null;}
            var owned=(Map)Field(group,"dynamicMap");if(owned!=null)owned.Dispose();TeamQualifier.QualifierList.Groups.Remove(group.ID);
        }
    }

    static int CommitTeamResult(TeamQualifier.QualifierList.QualifierGroup group,bool rollback=false) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultStatistics");
        var owners=((Revora.Client.GameState[])Call(group.GetType(),group,"Participants",true))
            .Concat((Revora.Client.GameState[])Call(group.GetType(),group,"Participants",false)).OrderBy(p=>p.Entity.UID).ToArray();
        lock(group.GetType().GetProperty("ResultSync",Instance).GetValue(group,null)) {
            foreach(var p in owners)System.Threading.Monitor.Enter(p.ItemSyncRoot);
            try {
                Call(type,null,"Capture",group);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var rows=(Array)Call(type,null,"Record",c,t,group);
                    if(rollback){t.Rollback();return rows.Length;}
                    t.Commit();Call(type,null,"Publish",group,rows);return rows.Length;
                }}
            }finally{for(int n=owners.Length-1;n>=0;n--)System.Threading.Monitor.Exit(owners[n].ItemSyncRoot);}
        }
    }
    static void VerifyTeamResultTransactions(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultStatistics");Call(type,null,"Initialize");
        var a=PointBuyer(town);var b=PointBuyer(town);var member=PointBuyer(town);
        foreach(var p in new[]{a,b,member}) {
            p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;
            ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
        }
        a.Team=new Revora.Game.ConquerStructures.Team(a);a.Team.Add(member);b.Team=new Revora.Game.ConquerStructures.Team(b);
        var group=new TeamQualifier.QualifierList.QualifierGroup(a,b);
        try {
            group.Import();group.End(b);
            Query("ALTER TABLE revora_team_arena_intent_statistics ADD CONSTRAINT team_capture_failure CHECK(won) NOT VALID");
            bool captureFailed=false;try{CommitTeamResult(group,true);}catch{captureFailed=true;}
            Query("ALTER TABLE revora_team_arena_intent_statistics DROP CONSTRAINT team_capture_failure");
            Check(captureFailed && Scalar("SELECT count(*) FROM revora_team_arena_intents",0)==0
                && Scalar("SELECT count(*) FROM revora_team_arena_intent_statistics",0)==0 && a.ArenaPoints==1000,
                "failed team capture rolls back its outcome and entire roster without awarding points");
            Check(CommitTeamResult(group,true)==3 && a.ArenaPoints==1000 && member.ArenaPoints==1000
                && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==0 && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",a.Entity.UID)==1000,
                "parent rollback retains all team caches and rolls back the entire roster result");
            Check(Scalar("SELECT count(*) FROM revora_team_arena_intents",0)==1
                && Scalar("SELECT count(*) FROM revora_team_arena_intent_statistics",0)==3,
                "separately committed team intent survives a rolled-back reward transaction");
            uint original=member.ArenaPoints;member.ArenaPoints=888;
            ArenaTable.SaveArenaStatistics(member.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(member.TeamArenaStatistic);
            Check(Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",member.Entity.UID)==original
                && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",member.Entity.UID)==original,
                "pending team intent protects both preimage mirrors before any result receipt exists");
            member.ArenaPoints=original;
            var retainedGroup=member.TeamQualifierGroup;member.TeamQualifierGroup=null;
            try {
                var purchases=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaPointPurchases");
                ulong silver=member.Entity.Money;Call(purchases,null,"Buy",member);
                Check(!Fenced(member) && member.Entity.Money==silver && member.ArenaPoints==original
                    && Scalar("SELECT count(*) FROM revora_arena_point_purchases WHERE uid=@uid",member.Entity.UID)==0,
                    "captured team outcome blocks point purchase without charge even after group ownership disappears");
                Check(!(bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDayRollover"),null,"Prepare",member)
                    && !Fenced(member) && member.ArenaPoints==original
                    && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",member.Entity.UID)==0,
                    "captured team outcome defers daily point reset before it can replace the saved preimage");
                var priorMap=member.Map;var other=PointBuyer(town);Group solo=null;
                try {
                    member.Entity.Teleport(1002,408,330);solo=new Group(member,other);solo.Import();
                    Check(member.Entity.MapID==1002 && other.Entity.MapID==1002 && member.QualifierGroup==null
                        && Field(solo,"dynamicMap")==null && !(bool)Call(ArenaLedger,null,"Start",solo)
                        && Scalar("SELECT count(*) FROM revora_arena_matches WHERE player1=@uid OR player2=@uid",member.Entity.UID)==0,
                        "pending team outcome rejects solo import before map allocation, ownership or ledger entry");
                }finally {
                    if(solo!=null){solo.End(other);solo.Export();}
                    member.Entity.Teleport(700,priorMap.ID,50,50);
                }
            }finally{member.TeamQualifierGroup=retainedGroup;}
            Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT team_failure CHECK(won) NOT VALID");
            bool failed=false;try{CommitTeamResult(group);}catch{failed=true;}
            Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT team_failure");
            Check(failed && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==0
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics",0)==0
                && new[]{a,b,member}.All(p=>p.ArenaPoints==1000 && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1000
                    && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1000),
                "later team receipt failure rolls back both point mirrors and every participant receipt");
            Query("UPDATE teamarena SET todaywin=1 WHERE entityid=@uid","uid",(long)member.Entity.UID);
            member.TeamArenaStatistic.TodayWin=1;
            failed=false;try{CommitTeamResult(group);}catch{failed=true;}
            Check(failed && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==0 && a.ArenaPoints==1000,
                "changed member preimage rejects the whole team result before publication");
            Query("UPDATE teamarena SET todaywin=0 WHERE entityid=@uid","uid",(long)member.Entity.UID);
            member.TeamArenaStatistic.TodayWin=0;
            int committed=0;Parallel.For(0,8,n=>{if(CommitTeamResult(group)>0)System.Threading.Interlocked.Increment(ref committed);});
            uint delta=(uint)Field(group,"legacyDelta");
            Check(committed==1 && a.ArenaPoints==1000+delta && member.ArenaPoints==990+delta && b.ArenaPoints==1000-delta
                && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==1 && Scalar("SELECT count(*) FROM revora_team_arena_statistics",0)==3,
                "concurrent team result transactions commit one frozen roster and leader/member economy");
            Check(new[]{a,b,member}.All(p=>p.TeamArenaStatistic.TodayBattles==1
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==p.ArenaPoints
                && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==p.ArenaPoints),
                "team result publishes only committed counters and both shared point mirrors");
            var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",member.Entity.UID);
            Check(saved.ArenaPoints==member.ArenaPoints && saved.TodayWin==1 && saved.TodayBattles==1,
                "production team loader reconstructs transactionally earned member results");
            Check(!PendingTeamResult(member),"complete team result bundle releases its pending-owner guard");
            VerifyTeamManagedSaves(member);
            Query("DELETE FROM revora_team_arena_statistics WHERE uid=@uid","uid",(long)member.Entity.UID);
            failed=false;try{CommitTeamResult(group);}catch{failed=true;}
            Check(failed && a.ArenaPoints==1000+delta,"incomplete team receipt bundle cannot authorize replay or a second award");
            Check(PendingTeamResult(a),"incomplete member receipt keeps the entire team result pending for its leaders");
            Query("INSERT INTO revora_team_arena_statistics SELECT * FROM revora_team_arena_intent_statistics WHERE run=@run AND uid=@uid","run",(Guid)Field(group,"ResultKey"),"uid",(long)member.Entity.UID);
        }finally {
            foreach(var p in new[]{a,b,member}){p.Entity.Teleport(1002,408,330);p.TeamQualifierGroup=null;}
            var map=(Map)Field(group,"dynamicMap");if(map!=null)map.Dispose();TeamQualifier.QualifierList.Groups.Remove(group.ID);
        }
    }

    static void VerifyTeamManagedSaves(Revora.Client.GameState p) {
        uint points=p.ArenaPoints;
        p.ArenaPoints=777777;p.TeamArenaStatistic.TodayWin=888;p.TeamArenaStatistic.TodayBattles=999;
        p.TeamArenaStatistic.TotalWin=888;p.TeamArenaStatistic.TotalLose=999;
        p.TeamArenaStatistic.LastSeasonRank=777;p.TeamArenaStatistic.CurrentHonor=777;
        p.ArenaStatistic.TodayWin=888;p.ArenaStatistic.CurrentHonor=777;
        p.TeamArenaStatistic.Name="TeamOwned";p.ArenaStatistic.Name="SoloOwned";
        ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
        using(var c=DataHolder.MySqlConnection){c.Open();ArenaTable.SaveArenaStatistics(p.ArenaStatistic,c);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic,c);}
        Check(Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==points
            && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND todaywin=1 AND todaybattles=1 AND totalwin=1 AND totallose=0 AND COALESCE(lastseasonrank,0)=0 AND COALESCE(currenthonor,0)=0 AND entityname='TeamOwned'",p.Entity.UID)==1
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==points
            && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND todaywin=0 AND COALESCE(currenthonor,0)=0 AND entityname='SoloOwned'",p.Entity.UID)==1,
            "both legacy save overloads preserve committed team economics while refreshing canonical identity metadata");
        Kernel.GamePool.Remove(p.Entity.UID);
        try {
            p.TeamArenaStatistic.Name="DetachedTeam";p.ArenaStatistic.Name="DetachedSolo";
            TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);ArenaTable.SaveArenaStatistics(p.ArenaStatistic);
            Check(Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND entityname='TeamOwned'",p.Entity.UID)==1
                && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND entityname='SoloOwned'",p.Entity.UID)==1,
                "detached managed team session cannot save metadata or stale balances");
        }finally{Kernel.GamePool[p.Entity.UID]=p;}
        Query("UPDATE arena SET arenapointfill=0 WHERE entityid=@uid","uid",(long)p.Entity.UID);
        var rollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDayRollover");
        var initialized=rollover.GetField("<Initialized>k__BackingField",Hidden);bool prior=(bool)initialized.GetValue(null);
        try {
            initialized.SetValue(null,false);
            p.ArenaStatistic=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
            p.TeamArenaStatistic=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(p.ArenaPoints==points && p.TeamArenaStatistic.ArenaPoints==points,
                "team receipt protects shared point reload even before solo rollover initialization");
            TeamArenaTable.Reset(p.TeamArenaStatistic);ArenaTable.Reset(p,p.ArenaStatistic);
            Check(p.ArenaPoints==points && p.TeamArenaStatistic.TodayWin==1 && p.TeamArenaStatistic.TodayBattles==1
                && p.TeamArenaStatistic.TotalWin==1 && p.TeamArenaStatistic.CurrentHonor==0,
                "legacy solo and team reset cannot replace a durable team result or award duplicate Honor");
        }finally{initialized.SetValue(null,prior);}
    }

    static void ColdTeamRecovery(Action action) {
        var pool=Kernel.GamePool;var closing=Kernel.DisconnectPool;var solo=Qualifier.ArenaStatistics;var team=TeamQualifier.ArenaStatistics;
        var soloGroups=Qualifier.QualifierList.Groups;var teamGroups=TeamQualifier.QualifierList.Groups;
        Kernel.GamePool=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Client.GameState>();
        Kernel.DisconnectPool=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Client.GameState>();
        Qualifier.ArenaStatistics=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Network.GamePackets.QualifierStatistic>();
        TeamQualifier.ArenaStatistics=new System.Collections.Concurrent.ConcurrentDictionary<uint,Revora.Network.GamePackets.TeamQualifierStatistic>();
        Qualifier.QualifierList.Groups=new System.Collections.Concurrent.ConcurrentDictionary<uint,Group>();
        TeamQualifier.QualifierList.Groups=new System.Collections.Concurrent.ConcurrentDictionary<uint,TeamQualifier.QualifierList.QualifierGroup>();
        try{action();}finally{Kernel.GamePool=pool;Kernel.DisconnectPool=closing;Qualifier.ArenaStatistics=solo;
            TeamQualifier.ArenaStatistics=team;Qualifier.QualifierList.Groups=soloGroups;TeamQualifier.QualifierList.Groups=teamGroups;}
    }
    static void VerifyTeamStartupRecovery(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaStartupRecovery");
        var a=PointBuyer(town);var b=PointBuyer(town);var member=PointBuyer(town);
        foreach(var p in new[]{a,b,member}) {
            p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;
            ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
        }
        a.Team=new Revora.Game.ConquerStructures.Team(a);a.Team.Add(member);b.Team=new Revora.Game.ConquerStructures.Team(b);
        var group=new TeamQualifier.QualifierList.QualifierGroup(a,b);
        try {
            group.Import();group.End(b);CommitTeamResult(group,true);var run=(Guid)Field(group,"ResultKey");
            bool failed=false;try{Call(type,null,"Recover",run);}catch(InvalidOperationException){failed=true;}
            Check(failed,"team recovery rejects live sessions and loaded Arena state");
            ColdTeamRecovery(()=>{
                foreach(string state in new[]{"team-match","team-cache","disconnect"}) {
                    if(state=="team-match")TeamQualifier.QualifierList.Groups[group.ID]=group;
                    if(state=="team-cache")TeamQualifier.ArenaStatistics[a.Entity.UID]=a.TeamArenaStatistic;
                    if(state=="disconnect")Kernel.DisconnectPool[a.Entity.UID]=a;
                    try{failed=false;try{Call(type,null,"Recover",run);}catch(InvalidOperationException){failed=true;}
                        Check(failed,"team startup recovery refuses existing ownership: "+state);}
                    finally{TeamQualifier.QualifierList.Groups.Clear();TeamQualifier.ArenaStatistics.Clear();Kernel.DisconnectPool.Clear();}
                }
                Check(!(bool)Call(type,null,"Recover",Guid.NewGuid()),"team startup cannot invent an uncaptured outcome");
                var orphan=Guid.NewGuid();
                Query("INSERT INTO revora_team_arena_results(id,winner,loser,delta,participants,completed_at) SELECT @orphan,winner,loser,delta,participants,completed_at FROM revora_team_arena_intents WHERE id=@run","orphan",orphan,"run",run);
                try{failed=false;try{Call(type,null,"Recover",orphan);}catch(InvalidOperationException){failed=true;}
                    Check(failed,"startup rejects result evidence whose captured outcome is missing");}
                finally{Query("DELETE FROM revora_team_arena_results WHERE id=@run","run",orphan);}
                Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT team_recovery_failure CHECK(won) NOT VALID");
                failed=false;try{Call(type,null,"Recover",run);}catch{failed=true;}
                Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT team_recovery_failure");
                Check(failed && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",a.Entity.UID)==0
                    && new[]{a,b,member}.All(p=>Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1000
                        && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1000),
                    "failed offline team receipt rolls back the recovered result and every point mirror");
                Query("UPDATE arena SET arenapoints=1001 WHERE entityid=@uid","uid",(long)b.Entity.UID);
                failed=false;try{Call(type,null,"Recover",run);}catch(InvalidOperationException){failed=true;}
                Check(failed && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",a.Entity.UID)==0,
                    "offline team recovery rejects a changed saved point mirror");
                Query("UPDATE arena SET arenapoints=1000 WHERE entityid=@uid","uid",(long)b.Entity.UID);
                Query("UPDATE revora_team_arena_intent_statistics SET points_after=points_after+1 WHERE uid=@uid","uid",(long)member.Entity.UID);
                failed=false;try{Call(type,null,"Recover",run);}catch(InvalidOperationException){failed=true;}
                Check(failed,"offline team recovery rejects a captured reward inconsistent with its frozen economy");
                Query("UPDATE revora_team_arena_intent_statistics SET points_after=points_after-1 WHERE uid=@uid","uid",(long)member.Entity.UID);
                int settled=0;Parallel.For(0,8,n=>{if((bool)Call(type,null,"Recover",run))System.Threading.Interlocked.Increment(ref settled);});
                uint delta=(uint)Field(group,"legacyDelta");
                Check(settled==1 && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",a.Entity.UID)==1000+delta
                    && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",member.Entity.UID)==990+delta
                    && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",b.Entity.UID)==1000-delta && a.ArenaPoints==1000,
                    "concurrent startup recovery settles frozen team awards once without mutating old session caches");
                var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",member.Entity.UID);
                Check(saved.TodayWin==1 && saved.TodayBattles==1 && saved.ArenaPoints==990+delta,
                    "production team loader retains recovered ordinary member rewards");
                Query("DELETE FROM revora_team_arena_statistics WHERE uid=@uid","uid",(long)member.Entity.UID);
                failed=false;try{Call(type,null,"Recover",run);}catch(InvalidOperationException){failed=true;}
                Check(failed,"startup refuses an incomplete committed team bundle instead of paying again");
                Query("INSERT INTO revora_team_arena_statistics SELECT * FROM revora_team_arena_intent_statistics WHERE run=@run AND uid=@uid","run",run,"uid",(long)member.Entity.UID);
            });
        }finally {
            foreach(var p in new[]{a,b,member}){p.Entity.Teleport(1002,408,330);p.TeamQualifierGroup=null;}
            var map=(Map)Field(group,"dynamicMap");if(map!=null)map.Dispose();TeamQualifier.QualifierList.Groups.Remove(group.ID);
        }
    }

    static void VerifyTeamStartupSweep(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaStartupRecovery");
        var groups=new System.Collections.Generic.List<TeamQualifier.QualifierList.QualifierGroup>();
        try {
            for(int n=0;n<3;n++) {
                var a=PointBuyer(town);var b=PointBuyer(town);
                foreach(var p in new[]{a,b}) {
                    p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;
                    ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
                    p.Team=new Revora.Game.ConquerStructures.Team(p);
                }
                var group=new TeamQualifier.QualifierList.QualifierGroup(a,b);groups.Add(group);
                group.Import();group.End(b);CommitTeamResult(group,true);
            }
            ColdTeamRecovery(()=>{
                foreach(var value in new[]{0,129}) {
                    bool failed=false;try{Call(type,null,"Sweep",value);}catch(ArgumentOutOfRangeException){failed=true;}
                    Check(failed,"team recovery rejects invalid sweep bound: "+value);
                }
                foreach(var value in new[]{0,101}) {
                    bool failed=false;try{Call(type,null,"PrepareStartup",value);}catch(ArgumentOutOfRangeException){failed=true;}
                    Check(failed,"team preparation rejects invalid pass bound: "+value);
                }
                Check((int)Call(type,null,"Sweep",1)==1
                    && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",groups[0].Player1.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",groups[1].Player1.Entity.UID)==0,
                    "bounded team sweep settles the oldest captured match and leaves later work pending");
                Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT team_sweep_failure CHECK(won) NOT VALID");
                bool failedPrepare=false;try{Call(type,null,"PrepareStartup",1);}catch{failedPrepare=true;}
                Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT team_sweep_failure");
                Check(failedPrepare && !(bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)
                    && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",groups[0].Player1.Entity.UID)==1,
                    "failed team preparation remains unready while retaining independently committed recovery");
                Call(type,null,"PrepareStartup",1);
                Check((bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)
                    && groups.All(g=>Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",g.Player1.Entity.UID)==1)
                    && (int)Call(type,null,"Sweep",128)==0,
                    "team preparation drains pending matches and audits completed evidence before readiness");
                long earned=Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",groups[0].Player1.Entity.UID);
                Call(type,null,"PrepareStartup",1);
                Check(Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",groups[0].Player1.Entity.UID)==earned,
                    "repeated team startup preparation cannot replay settled points");
                uint uid=groups[2].Player1.Entity.UID;var run=(Guid)Field(groups[2],"ResultKey");
                Query("DELETE FROM revora_team_arena_statistics WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid);
                failedPrepare=false;try{Call(type,null,"PrepareStartup",1);}catch(InvalidOperationException){failedPrepare=true;}
                Check(failedPrepare && !(bool)type.GetProperty("Prepared",Hidden).GetValue(null,null),
                    "team preparation refuses incomplete historical receipts even when no matches remain pending");
                Query("INSERT INTO revora_team_arena_statistics SELECT * FROM revora_team_arena_intent_statistics WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid);
            });
        }finally {
            foreach(var group in groups) {
                foreach(var p in new[]{group.Player1,group.Player2}){p.Entity.Teleport(1002,408,330);p.TeamQualifierGroup=null;}
                var map=(Map)Field(group,"dynamicMap");if(map!=null)map.Dispose();TeamQualifier.QualifierList.Groups.Remove(group.ID);
            }
        }
    }

    static bool PendingTeamResult(Revora.Client.GameState p) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            return (bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultStatistics"),null,"Pending",c,t,p.Entity.UID,null);
        }}
    }

    static void VerifyArenaCrossModeCapture(Map town) {
        var a=PointBuyer(town);var b=PointBuyer(town);
        foreach(var p in new[]{a,b}){p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;
            ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);}
        var solo=new Group(a,b);solo.Import();Check((bool)Call(ArenaLedger,null,"Start",solo),"cross-mode fixture registers an actual solo admission");
        TeamQualifier.QualifierList.QualifierGroup team=null;
        try {
            foreach(var p in new[]{a,b}){p.Entity.Teleport(1002,408,330);p.QualifierGroup=null;p.Team=new Revora.Game.ConquerStructures.Team(p);}
            team=new TeamQualifier.QualifierList.QualifierGroup(a,b);team.Import();
            Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && Field(team,"dynamicMap")==null
                && a.TeamQualifierGroup==null && b.TeamQualifierGroup==null,
                "active solo admission rejects team import before transferring either player");
            team.End(b);
            bool failed=false;try{CommitTeamResult(team,true);}catch(InvalidOperationException){failed=true;}
            Check(failed && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",a.Entity.UID)==0
                && a.ArenaPoints==1000 && b.ArenaPoints==1000,
                "active solo ledger prevents overlapping team outcome capture even after its group references disappear");
        }finally {
            foreach(var p in new[]{a,b}){p.Entity.Teleport(1002,408,330);p.QualifierGroup=null;p.TeamQualifierGroup=null;}
            var soloMap=(Map)Field(solo,"dynamicMap");if(soloMap!=null)soloMap.Dispose();Qualifier.QualifierList.Groups.Remove(solo.ID);
            if(team!=null){var map=(Map)Field(team,"dynamicMap");if(map!=null)map.Dispose();TeamQualifier.QualifierList.Groups.Remove(team.ID);}
        }
    }

    static TeamQualifier.QualifierList.QualifierGroup TeamCoordinatorGroup(Map town, bool ended=true, bool imported=true) {
        var a=PointBuyer(town);var b=PointBuyer(town);var member=PointBuyer(town);
        foreach(var p in new[]{a,b,member}){p.Entity.Level=100;p.ArenaPoints=1000;p.TeamArenaStatistic.Name=p.Entity.Name;
            ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);}
        a.Team=new Revora.Game.ConquerStructures.Team(a);a.Team.Add(member);b.Team=new Revora.Game.ConquerStructures.Team(b);
        var group=new TeamQualifier.QualifierList.QualifierGroup(a,b);if(imported){group.Import();if(ended)group.End(b);}
        a.TeamArenaStatistic.PlayWith=b;b.TeamArenaStatistic.PlayWith=a;return group;
    }
    static Revora.Client.GameState[] TeamFixtureOwners(TeamQualifier.QualifierList.QualifierGroup group) {
        return ((Revora.Client.GameState[])Call(group.GetType(),group,"Participants",true))
            .Concat((Revora.Client.GameState[])Call(group.GetType(),group,"Participants",false)).ToArray();
    }
    static void CleanupTeamFixture(TeamQualifier.QualifierList.QualifierGroup group) {
        foreach(var p in TeamFixtureOwners(group)){p.Entity.Teleport(1002,408,330);p.TeamQualifierGroup=null;}
        var map=(Map)Field(group,"dynamicMap");if(map!=null)map.Dispose();TeamQualifier.QualifierList.Groups.Remove(group.ID);
    }
    static void VerifyTeamCoordinator(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var group=TeamCoordinatorGroup(town);var owners=TeamFixtureOwners(group);
        try {
            lock(Field(group,"outcomeSync"))Call(group.GetType(),group,"NotifyCommittedOutcome");
            Check(((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(group,"outcomeNotified")).Count==0,
                "ended team match does not attempt result dialogs before durable completion");
            Check(Call(type,null,"RecoverFenced",group).ToString()=="NotReady","fenced team recovery cannot take over healthy owners");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(owners[1].ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(type,null,"Complete",group).ToString()=="Busy"
                    && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",group.Player1.Entity.UID)==0,
                    "busy team coordinator defers before capturing or changing points");}finally{release.Set();task.Wait();}
            }
            int committed=0;Parallel.For(0,8,n=>{if(Call(type,null,"Complete",group).ToString()=="Committed")System.Threading.Interlocked.Increment(ref committed);});
            Check(committed==1 && (int)Field(group,"DurableResultReady")==1 && owners.All(p=>p.TeamArenaStatistic.TodayBattles==1 && !Fenced(p)),
                "team coordinator commits and projects one complete ordinary roster result");
            Check(((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(group,"outcomeNotified")).SetEquals(new[]{group.Winner,group.Loser}),
                "committed team result attempts each original leader dialog once after projection");
            var points=owners.Select(p=>p.ArenaPoints).ToArray();group.Export();
            Check(owners.Select(p=>p.ArenaPoints).SequenceEqual(points) && owners.All(p=>p.Entity.MapID==1002 && p.TeamQualifierGroup==null)
                && Call(type,null,"Complete",group).ToString()=="AlreadyCommitted",
                "export after durable team completion returns players without invoking legacy awards again");
        }finally{CleanupTeamFixture(group);}
        var failed=TeamCoordinatorGroup(town);owners=TeamFixtureOwners(failed);
        try {
            Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT team_coordinator_failure CHECK(won) NOT VALID");
            string outcome;try{outcome=Call(type,null,"Complete",failed).ToString();}
            finally{Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT team_coordinator_failure");}
            Check(outcome=="Failed" && owners.All(p=>Fenced(p) && p.ArenaPoints==1000)
                && (int)Field(failed,"DurableResultReady")==0
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",failed.Player1.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",failed.Player1.Entity.UID)==0,
                "team coordinator failure preserves captured evidence, rolls back awards and fences every cache");
            Check(((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(failed,"outcomeNotified")).Count==0,
                "rolled-back team result does not attempt victory or defeat dialogs");
            var map=failed.Player1.Map;failed.Export();
            Check(Kernel.Maps.ContainsKey(map.ID) && Call(type,null,"Complete",failed).ToString()=="NotReady",
                "failed team owners retain their map and cannot retry through invalidated caches");
            var original=owners[0];var replacement=PointBuyer(town);Kernel.GamePool[original.Entity.UID]=replacement;
            try{Check(Call(type,null,"RecoverFenced",failed).ToString()=="NotReady","fenced team recovery rejects a replaced original session");}
            finally{Kernel.GamePool[original.Entity.UID]=original;}
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(original.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(type,null,"RecoverFenced",failed).ToString()=="Busy","busy fenced team owner leaves recovery retryable");}
                finally{release.Set();task.Wait();}
            }
            Check(Call(type,null,"RecoverFenced",failed).ToString()=="Committed" && (int)Field(failed,"DurableResultReady")==1
                && owners.All(p=>Fenced(p) && p.ArenaPoints==1000)
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",failed.Player1.Entity.UID)==1,
                "fenced team recovery commits captured rewards without re-enabling or projecting stale owners");
            Check(Call(type,null,"RecoverFenced",failed).ToString()=="AlreadyCommitted","repeated fenced team recovery recognizes the single durable result");
            foreach(var p in owners)Kernel.GamePool.Remove(p.Entity.UID);
            try{failed.Export();Check(!Kernel.Maps.ContainsKey(map.ID),"recovered team map cleans up after original fenced registrations release");}
            finally{foreach(var p in owners)Kernel.GamePool[p.Entity.UID]=p;}
        }finally{CleanupTeamFixture(failed);}
        var capture=TeamCoordinatorGroup(town);owners=TeamFixtureOwners(capture);
        try {
            Query("ALTER TABLE revora_team_arena_intent_statistics ADD CONSTRAINT team_initial_capture_failure CHECK(won) NOT VALID");
            string result;try{result=Call(type,null,"Complete",capture).ToString();}
            finally{Query("ALTER TABLE revora_team_arena_intent_statistics DROP CONSTRAINT team_initial_capture_failure");}
            Check(result=="Failed" && owners.All(Fenced)
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",capture.Player1.Entity.UID)==0,
                "initial team capture failure retains only the ended in-memory evidence and fenced owners");
            Query("UPDATE arena SET arenapoints=1001 WHERE entityid=@uid","uid",(long)capture.Player1.Entity.UID);
            Check(Call(type,null,"RecoverFenced",capture).ToString()=="Failed"
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",capture.Player1.Entity.UID)==0,
                "fenced initial capture retry rejects changed database preimages without inventing a reward");
            Query("UPDATE arena SET arenapoints=1000 WHERE entityid=@uid","uid",(long)capture.Player1.Entity.UID);
            Check(Call(type,null,"RecoverFenced",capture).ToString()=="Committed" && owners.All(Fenced)
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",capture.Player1.Entity.UID)==1,
                "unchanged fenced owners can retry initial capture and commit the original team outcome");
        }finally{CleanupTeamFixture(capture);}
        var stale=TeamCoordinatorGroup(town);owners=TeamFixtureOwners(stale);
        try {
            CommitTeamResult(stale);
            foreach(var p in owners){p.ArenaPoints=1000;p.TeamArenaStatistic.TodayWin=0;p.TeamArenaStatistic.TodayBattles=0;p.TeamArenaStatistic.TotalWin=0;p.TeamArenaStatistic.TotalLose=0;}
            Check(Call(type,null,"Complete",stale).ToString()=="Failed" && (int)Field(stale,"DurableResultReady")==1 && owners.All(Fenced),
                "already committed team result with stale caches records durability but requires reload");
            Check(((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(stale,"outcomeNotified")).Count==0,
                "durable team result with failed projection cannot announce success through stale caches");
            var map=stale.Player1.Map;stale.Export();
            Check(Kernel.Maps.ContainsKey(map.ID) && owners.All(p=>p.ArenaPoints==1000),
                "committed-but-fenced team cleanup cannot pay legacy rewards or release the owned map early");
        }finally{CleanupTeamFixture(stale);}
    }

    static void VerifyTeamRollover(Map town) {
        var rollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDayRollover");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        // November 1 has 25 Pacific hours in 2026: [07:00 UTC, next day 08:00 UTC).
        var start=new DateTime(2026,11,1,7,0,0,DateTimeKind.Utc);
        var end=new DateTime(2026,11,2,8,0,0,DateTimeKind.Utc);
        ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>end.AddSeconds(-1)));
        try {
            foreach(var at in new[]{start.AddSeconds(-1),start,end.AddSeconds(-1),end}) {
                var group=TeamCoordinatorGroup(town);var owners=TeamFixtureOwners(group);
                try {
                    group.GetType().GetField("<OutcomeUtc>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(group,at);
                    foreach(var p in owners)Query("UPDATE entities SET level=100 WHERE uid=@uid","uid",(long)p.Entity.UID);
                    Check(Call(coordinator,null,"Complete",group).ToString()=="Committed","team rollover fixture commits the admitted outcome");
                    group.Export();
                    bool retain=at>=start && at<end;
                    foreach(var p in owners) {
                        uint earned=p.ArenaPoints,teamWins=p.TeamArenaStatistic.TodayWin,teamBattles=p.TeamArenaStatistic.TodayBattles;
                        uint expected=retain?earned:ArenaTable.ArenaPointFill(100);
                        Check((bool)Call(rollover,null,"Prepare",p) && !Fenced(p) && p.ArenaPoints==expected
                            && p.TeamArenaStatistic.ArenaPoints==expected && p.ArenaStatistic.TodayWin==0 && p.ArenaStatistic.TodayBattles==0
                            && p.TeamArenaStatistic.TodayWin==teamWins && p.TeamArenaStatistic.TodayBattles==teamBattles,
                            "Pacific rollover retains only today's team points without counting team games as solo games: "+at.ToString("O"));
                        Check(Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid AND day='20261101' AND retained_results="+(retain?"true":"false"),p.Entity.UID)==1
                            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==expected
                            && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",p.Entity.UID)==expected,
                            "team-derived point retention and both persisted mirrors commit together");
                        Check((bool)Call(rollover,null,"Prepare",p) && p.ArenaPoints==expected
                            && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                            "team-earned rollover repeats without refilling or a second receipt");
                        var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
                        Check(loaded.ArenaPoints==expected && loaded.TodayBattles==0,"ordinary reload preserves team-derived shared points and solo counters");
                    }
                }finally{CleanupTeamFixture(group);}
            }
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

    static void VerifyTeamEndCoordinator(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var group=TeamCoordinatorGroup(town,false);var owners=TeamFixtureOwners(group);
        try {
            var outsider=PointBuyer(town);
            Check(Call(type,null,"EndForPlayer",group,outsider).ToString()=="NotReady" && !group.Done,
                "team terminal coordinator rejects a foreign leader without freezing an outcome");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(owners[1].ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(type,null,"EndForPlayer",group,group.Player2).ToString()=="Busy" && !group.Done
                    && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE loser=@uid",group.Player2.Entity.UID)==0,
                    "busy admitted team member delays ending before RAM freeze or capture");}finally{release.Set();task.Wait();}
            }
            var original=owners[1];Kernel.GamePool[original.Entity.UID]=outsider;
            try{Check(Call(type,null,"EndForPlayer",group,group.Player2).ToString()=="NotReady" && !group.Done,
                "replacement of an admitted session prevents terminal team ownership");}
            finally{Kernel.GamePool[original.Entity.UID]=original;}
            int commits=0;Parallel.For(0,8,n=>{if(Call(type,null,"EndForPlayer",group,group.Player2).ToString()=="Committed")System.Threading.Interlocked.Increment(ref commits);});
            Check(commits==1 && ReferenceEquals(group.Loser,group.Player2) && owners.All(p=>p.TeamArenaStatistic.TodayBattles==1)
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",group.Player2.Entity.UID)==1,
                "concurrent terminal team callbacks freeze and save one complete admitted roster outcome");
            Check(Call(type,null,"EndForPlayer",group,group.Player1).ToString()=="AlreadyCommitted" && ReferenceEquals(group.Loser,group.Player2),
                "later opposing departure cannot reverse the saved team outcome");group.Export();
        }finally{CleanupTeamFixture(group);}
        var clock=type.GetField("MatchClock",Hidden);var originalClock=clock.GetValue(null);
        try {
            foreach(bool wrapped in new[]{false,true}) {
                var match=TeamCoordinatorGroup(town,false);
                try {
                    match.ImportTime=new Time32(wrapped?int.MaxValue-1000:100000);match.CreateTime=match.ImportTime.AddMinutes(-20);
                    match.UpdateDamage(match.Player1,wrapped?100u:200u);match.UpdateDamage(match.Player2,100);
                    var now=new Time32(unchecked(match.ImportTime.Value+180000));clock.SetValue(null,new Func<Time32>(()=>now));
                    Check(Call(type,null,"EndTimedOut",match).ToString()=="NotReady" && !match.Done,
                        "team fight remains active at the old three-minute deadline");
                    now=new Time32(unchecked(match.ImportTime.Value+299999));
                    Check(Call(type,null,"EndTimedOut",match).ToString()=="NotReady" && !match.Done,
                        "team timeout waits for five minutes after import regardless of older creation time");
                    now=new Time32(unchecked(match.ImportTime.Value+300000));
                    Check(Call(type,null,"EndTimedOut",match).ToString()=="Committed"
                        && ReferenceEquals(match.Winner,wrapped?match.Player2:match.Player1),
                        "team deadline commits damage winner or second-side tie under tick wrap");
                    match.Export();
                }finally{CleanupTeamFixture(match);}
            }
        }finally{clock.SetValue(null,originalClock);}
        var failed=TeamCoordinatorGroup(town,false);
        try {
            Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT team_terminal_failure CHECK(won) NOT VALID");
            string result;try{result=Call(type,null,"EndForPlayer",failed,failed.Player2).ToString();}
            finally{Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT team_terminal_failure");}
            Check(result=="Failed" && failed.Done && TeamFixtureOwners(failed).All(Fenced)
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE loser=@uid",failed.Player2.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",failed.Player2.Entity.UID)==0,
                "terminal team save failure retains captured ending while rolling back rewards");
            Check(Call(type,null,"RecoverFenced",failed).ToString()=="Committed" && ReferenceEquals(failed.Loser,failed.Player2),
                "failed terminal team outcome recovers under its original fenced roster");
        }finally{CleanupTeamFixture(failed);}
    }

    static void VerifyTeamPendingDeparture(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=ArenaLedger.GetField("Clock",Hidden);var originalClock=clock.GetValue(null);
        try {
            foreach(string retry in new[]{"RetryDeparture","EndTimedOut","LegacyEnd"}) {
                var group=TeamCoordinatorGroup(town,false);var observed=new DateTime(2055,1,1,7,59,59,DateTimeKind.Utc);
                var now=observed;clock.SetValue(null,new Func<DateTime>(()=>now));
                try {
                    Check(Call(type,null,"RetryDeparture",group).ToString()=="NotReady","team retry requires an observed original departure");
                    using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                        var task=Task.Run(()=>{lock(Field(group,"outcomeSync")){held.Set();release.Wait();}});held.Wait();
                        try {
                            Check(Call(type,null,"EndForPlayer",group,group.Player2).ToString()=="Busy" && !group.Done
                                && ReferenceEquals(Field(Field(group,"PendingDeparture"),"Player"),group.Player2),
                                "busy team match retains the first canonical leader departure without freezing combat outcome");
                            var first=Field(group,"PendingDeparture");now=now.AddMinutes(2);
                            Check(Call(type,null,"EndForPlayer",group,group.Player1).ToString()=="Busy" && ReferenceEquals(first,Field(group,"PendingDeparture"))
                                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE loser=@uid",group.Player2.Entity.UID)==0,
                                "opposing departure cannot replace the retained first request or capture while match ownership is busy");
                        }finally{release.Set();task.Wait();}
                    }
                    group.UpdateDamage(group.Player2,uint.MaxValue);
                    Check(group.Player2Damage==0,"team combat damage stops after a retained terminal request");
                    string result;
                    if(retry=="LegacyEnd"){group.End(group.Player1);result=Call(type,null,"Complete",group).ToString();}
                    else result=Call(type,null,retry,group).ToString();
                    var ended=(DateTime)group.GetType().GetProperty("OutcomeUtc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(group,null);
                    Check(result=="Committed" && ReferenceEquals(group.Loser,group.Player2) && ended==observed
                        && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid AND (completed_at AT TIME ZONE 'America/Los_Angeles')::date=DATE '2054-12-31'",group.Player2.Entity.UID)==1,
                        "team retry preserves first leader and Pacific observation day across midnight: "+retry);
                    Check(Call(type,null,"RetryDeparture",group).ToString()=="AlreadyCommitted"
                        && TeamFixtureOwners(group).All(p=>p.TeamArenaStatistic.TodayBattles==1),
                        "retained team request cannot pay the result again");group.Export();
                }finally{CleanupTeamFixture(group);}
            }
        }finally{clock.SetValue(null,originalClock);}
    }

    static void VerifyTeamCompletionPump(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaCompletionPump");
        var pump=Activator.CreateInstance(type,Instance,null,new object[]{2},null);
        var now=new DateTime(2054,1,1,20,0,0,DateTimeKind.Utc);type.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
        var first=TeamCoordinatorGroup(town);var second=TeamCoordinatorGroup(town);var third=TeamCoordinatorGroup(town);
        Check((bool)Call(type,pump,"Track",first) && (bool)Call(type,pump,"Track",first) && (bool)Call(type,pump,"Track",second)
            && !(bool)Call(type,pump,"Track",third) && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==2,
            "team completion queue deduplicates matches and enforces its capacity without touching rejected work");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(Field(first,"outcomeSync")){held.Set();release.Wait();}});held.Wait();
            try {
                Check((int)Call(type,pump,"Tick",1)==0 && !(bool)Call(type,null,"ResultReady",first),"busy first match stays owned and unready");
                Check((int)Call(type,pump,"Tick",1)==1 && (bool)Call(type,null,"ResultReady",second)
                    && !TeamQualifier.QualifierList.Groups.ContainsKey(second.ID),"round-robin retry lets another match commit and clean up despite a busy predecessor");
            }finally{release.Set();task.Wait();}
        }
        Check((bool)Call(type,pump,"Track",third) && (int)Call(type,pump,"Tick",2)==1 && !(bool)Call(type,null,"ResultReady",first),
            "freed queue capacity is reusable while a deferred entry respects its retry time");
        now=now.AddSeconds(1);
        Check((int)Call(type,pump,"Tick",2)==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==0
            && (bool)Call(type,null,"ResultReady",first),"released owner completes on the next due retry and leaves sticky result proof");
        Check((bool)Call(type,pump,"Track",first) && (int)Call(type,pump,"Tick",2)==0,
            "tracking an already completed match cannot enqueue or pay it again");
        var failed=TeamCoordinatorGroup(town);Call(type,pump,"Track",failed);
        Query("CREATE SEQUENCE team_pump_fail_attempts;CREATE FUNCTION suppress_team_pump_bundle() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN PERFORM nextval(''team_pump_fail_attempts''); RETURN NULL; END';CREATE TRIGGER suppress_team_pump_bundle BEFORE INSERT ON revora_team_arena_results FOR EACH ROW EXECUTE FUNCTION suppress_team_pump_bundle()");
        try {
            Check((int)Call(type,pump,"Tick",1)==0 && TeamFixtureOwners(failed).All(Fenced)
                && Scalar("SELECT last_value FROM team_pump_fail_attempts",0)==1,"pump retains a failed result and its fenced owners");
            Call(type,pump,"Tick",1);Check(Scalar("SELECT last_value FROM team_pump_fail_attempts",0)==1,"repeated ticks do not hammer the failed database write");
            now=now.AddSeconds(1);Call(type,pump,"Tick",1);
            now=now.AddSeconds(1);Call(type,pump,"Tick",1);
            Check(Scalar("SELECT last_value FROM team_pump_fail_attempts",0)==2,"second failure increases the retry delay to two seconds");
            now=now.AddSeconds(-5);Call(type,pump,"Tick",1);Call(type,pump,"Tick",1);
            Check(Scalar("SELECT last_value FROM team_pump_fail_attempts",0)==3,"clock rollback allows one fresh attempt without a repeated-tick loop");
        }finally{Query("DROP TRIGGER suppress_team_pump_bundle ON revora_team_arena_results;DROP FUNCTION suppress_team_pump_bundle();DROP SEQUENCE team_pump_fail_attempts");}
        now=now.AddSeconds(30);
        Check((int)Call(type,pump,"Tick",1)==0 && (bool)Call(type,null,"ResultReady",failed)
            && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",failed.Player1.Entity.UID)==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==1,
            "pump repairs the durable result but retains cleanup until fenced sessions are released");
        foreach(var p in TeamFixtureOwners(failed))Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);now=now.AddSeconds(1);
        Check((int)Call(type,pump,"Tick",1)==1 && (int)type.GetProperty("Count",Instance).GetValue(pump,null)==0
            && (bool)Call(type,null,"ResultReady",failed),"post-release retry cleans the map without replaying the repaired result");
        bool rejected=false;try{Call(type,pump,"Tick",129);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"team completion pump rejects an unbounded per-tick budget");
    }


    static void VerifyJointArenaStartup(Map town) {
        Query("TRUNCATE revora_potency_validation.revora_arena_matches,revora_potency_validation.revora_team_arena_results,revora_potency_validation.revora_team_arena_intents CASCADE");
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaStartupPreparation");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var clock=ArenaLedger.GetField("Clock",Hidden);var originalClock=clock.GetValue(null);
        var now=new DateTime(2056,1,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        var team=TeamCoordinatorGroup(town);
        try {
            CommitTeamResult(team,true);
            var solo=StatGroup(town);var ordered=new[]{solo.Player1,solo.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
            lock(Field(solo,"ParticipationSync"))lock(ordered[0].ItemSyncRoot)lock(ordered[1].ItemSyncRoot)
                Call(intents,null,"Capture",solo,Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect"));
            bool refused=false;try{Call(type,null,"Prepare",1);}catch(InvalidOperationException){refused=true;}
            Check(refused && !(bool)type.GetProperty("Prepared",Hidden).GetValue(null,null),
                "joint Arena preparation rejects loaded sessions and does not expose readiness");
            var run=(Guid)Field(solo,"ParticipationKey");uint original=solo.Player1.Entity.UID;
            Query("UPDATE revora_arena_matches SET player1=@uid WHERE id=@run","uid",(long)team.Player1.Entity.UID,"run",run);
            try {
                ColdTeamRecovery(()=>{
                    bool rejected=false;try{Call(type,null,"Prepare",1);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==0 && LedgerRows(solo,"active")==1,
                        "joint startup rejects contradictory cross-mode ownership before settling either result");
                });
            }finally{Query("UPDATE revora_arena_matches SET player1=@uid WHERE id=@run","uid",(long)original,"run",run);}
            now=now.AddDays(1);
            ColdTeamRecovery(()=>{
                Query("CREATE FUNCTION suppress_joint_rank() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_joint_rank BEFORE INSERT ON revora_arena_rank_days FOR EACH ROW EXECUTE FUNCTION suppress_joint_rank()");
                bool failed=false;try{Call(type,null,"Prepare",1);}catch(InvalidOperationException){failed=true;}
                finally{Query("DROP TRIGGER suppress_joint_rank ON revora_arena_rank_days;DROP FUNCTION suppress_joint_rank()");}
                Check(failed && !(bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)
                    && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",team.Player1.Entity.UID)==1
                    && LedgerRows(solo,"finished")==1,
                    "later ranking failure keeps joint startup closed while retaining independently committed recoveries");
                long teamPoints=Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",team.Player1.Entity.UID);
                Call(type,null,"Prepare",1);Call(type,null,"Prepare",1);
                Check((bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)
                    && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",team.Player1.Entity.UID)==teamPoints
                    && Scalar("SELECT count(*) FROM revora_team_arena_results",0)==1
                    && Scalar("SELECT count(*) FROM revora_arena_result_statistics",0)==2,
                    "joint startup retry audits both modes without duplicating settled points or receipts");
                var savedTeam=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",team.Player1.Entity.UID);
                var savedSolo=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",solo.Player1.Entity.UID);
                Check(savedTeam.ArenaPoints==teamPoints && savedTeam.TodayWin==1 && savedSolo.TodayWin==1,
                    "ordinary production loaders retain both jointly recovered earning paths");
                Kernel.DisconnectPool.TryAdd(team.Player1.Entity.UID,team.Player1);
                try {
                    bool rejected=false;try{Call(type,null,"Prepare",1);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected && !(bool)type.GetProperty("Prepared",Hidden).GetValue(null,null),
                        "a queued disconnect invalidates prior joint readiness before another preparation attempt");
                }finally{Revora.Client.GameState removed;Kernel.DisconnectPool.TryRemove(team.Player1.Entity.UID,out removed);}
                foreach(int count in new[]{128,129,1}) {
                    const uint fixtureLeader=4294900000;
                    Query(@"INSERT INTO revora_team_arena_admissions(id,first_leader,second_leader,created_at)
SELECT md5(@prefix||n::text)::uuid,@first,@second,CURRENT_TIMESTAMP FROM generate_series(1,@count) n",
                        "prefix",Guid.NewGuid().ToString(),"first",(long)fixtureLeader,"second",(long)fixtureLeader+1,"count",count);
                    if(count==1)Query("CREATE FUNCTION suppress_admission_close() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_admission_close BEFORE INSERT ON revora_team_arena_admission_abandoned FOR EACH ROW EXECUTE FUNCTION suppress_admission_close()");
                    bool rejected=false;
                    try{Call(type,null,"Prepare",1);}catch(InvalidOperationException){rejected=true;}
                    finally{if(count==1)Query("DROP TRIGGER suppress_admission_close ON revora_team_arena_admission_abandoned;DROP FUNCTION suppress_admission_close()");}
                    Check(rejected==(count!=128) && (bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)==(count==128)
                        && Scalar("SELECT count(*) FROM revora_team_arena_admissions a WHERE first_leader=@uid AND NOT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned x WHERE x.run=a.id)",fixtureLeader)==(count==128?0:1),
                        "joint startup checks remaining work after bounded admission closure: rows="+count);
                    Call(type,null,"Prepare",1);
                    Check((bool)type.GetProperty("Prepared",Hidden).GetValue(null,null)
                        && Scalar("SELECT count(*) FROM revora_team_arena_admissions a WHERE first_leader=@uid AND NOT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned x WHERE x.run=a.id)",fixtureLeader)==0,
                        "joint startup resumes admission closure without losing completed work: rows="+count);
                }
            });
        }finally{clock.SetValue(null,originalClock);CleanupTeamFixture(team);}
    }

    static void VerifyTeamAdmissions(Map town) {
        var admission=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaAdmissions");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var group=TeamCoordinatorGroup(town,false,false);
        try {
            Query("ALTER TABLE revora_team_arena_admission_players ADD CONSTRAINT reject_admission CHECK(uid=0) NOT VALID");
            try{group.Import();}finally{Query("ALTER TABLE revora_team_arena_admission_players DROP CONSTRAINT reject_admission");}
            Check(!group.Inside && (int)Field(group,"importStarted")==0 && group.Player1.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_team_arena_admissions WHERE first_leader=@uid",group.Player1.Entity.UID)==0,
                "failed admission roster write rolls back reservation before any transfer or import latch");
            group.Import();var owners=TeamFixtureOwners(group);
            Check(group.Inside && owners.Length==3 && owners.All(PendingTeamResult)
                && Scalar("SELECT count(*) FROM revora_team_arena_admission_players WHERE run=(SELECT id FROM revora_team_arena_admissions WHERE first_leader=@uid)",group.Player1.Entity.UID)==3,
                "real team import reserves both leaders and ordinary member before combat");
            group.Import();Check(Scalar("SELECT count(*) FROM revora_team_arena_admissions WHERE first_leader=@uid",group.Player1.Entity.UID)==1,
                "repeated team import does not reserve or transfer the roster twice");
            Check(Call(coordinator,null,"EndForPlayer",group,group.Player2).ToString()=="Committed" && owners.All(p=>!PendingTeamResult(p)),
                "committed team result releases durable admission ownership for every reserved participant");group.Export();
        }finally{CleanupTeamFixture(group);}
        var interrupted=TeamCoordinatorGroup(town,false);
        try {
            var owners=TeamFixtureOwners(interrupted);var points=owners.Select(p=>p.ArenaPoints).ToArray();
            bool refused=false;try{Call(admission,null,"AbandonInterrupted",128);}catch(InvalidOperationException){refused=true;}
            Check(refused && owners.All(PendingTeamResult),"live registries cannot abandon a team's reserved admission");
            ColdTeamRecovery(()=>{
                Check((int)Call(admission,null,"AbandonInterrupted",128)==1 && (int)Call(admission,null,"AbandonInterrupted",128)==0,
                    "cold startup records interrupted no-outcome admission once without inventing a winner");
            });
            Check(owners.All(p=>!PendingTeamResult(p)) && owners.Select(p=>p.ArenaPoints).SequenceEqual(points)
                && owners.All(p=>Scalar("SELECT todaybattles FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0),
                "abandoned team reservation releases shared points without awarding counters or prizes");
            Check(Call(coordinator,null,"EndForPlayer",interrupted,interrupted.Player2).ToString()=="Failed"
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",interrupted.Player2.Entity.UID)==0,
                "stale pre-restart actors cannot award an abandoned admission");
        }finally{CleanupTeamFixture(interrupted);}
        var captured=TeamCoordinatorGroup(town);
        try {
            CommitTeamResult(captured,true);
            ColdTeamRecovery(()=>{
                Check((int)Call(admission,null,"AbandonInterrupted",128)==0,"startup preserves reserved teams that already captured an outcome");
                Check((bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaStartupRecovery"),null,"Recover",(Guid)Field(captured,"ResultKey")),
                    "startup recovers an admitted captured team outcome against its reserved roster");
            });
            Check(TeamFixtureOwners(captured).All(p=>!PendingTeamResult(p)),"recovered team result releases all durable reservation owners");
        }finally{CleanupTeamFixture(captured);}
    }

    static void VerifyTeamInterruptedTransfer(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var broken=TeamCoordinatorGroup(town,false,false);var source=Kernel.Maps[700];
        try {
            try{Kernel.Maps[700]=null;broken.Import();}finally{Kernel.Maps[700]=source;}
            Check((bool)Field(broken,"DurableAdmissionCancelled") && !broken.Inside && !broken.Done
                && !TeamQualifier.QualifierList.Groups.ContainsKey(broken.ID) && broken.Player1.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",broken.Player1.Entity.UID)==0,
                "actual map-creation failure cancels saved admission and cleans ownership without rewards");
        }finally{CleanupTeamFixture(broken);}
        var group=TeamCoordinatorGroup(town,false);var owners=TeamFixtureOwners(group);var points=owners.Select(p=>p.ArenaPoints).ToArray();
        try {
            Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="NotReady" && owners.All(PendingTeamResult),
                "cancellation cannot erase an eligible active team fight");
            // Simulate an interrupted transfer after real import: one leader did not stay in the map.
            group.Player2.Entity.Teleport(1002,408,330);group.Player2.TeamQualifierGroup=null;
            group.GetType().GetField("admissionReady",Instance).SetValue(group,false);
            group.GetType().GetField("AdmissionInterrupted",Instance).SetValue(group,true);
            var map=group.Player1.Map;
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(owners[1].ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="Busy"
                    && owners.All(PendingTeamResult) && Kernel.Maps.ContainsKey(map.ID),
                    "busy interrupted-transfer owner retains the reservation and map for retry");}finally{release.Set();task.Wait();}
            }
            Query("CREATE FUNCTION suppress_team_cancel() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_team_cancel BEFORE INSERT ON revora_team_arena_admission_abandoned FOR EACH ROW EXECUTE FUNCTION suppress_team_cancel()");
            try{Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="Failed" && owners.All(PendingTeamResult)
                && !(bool)Field(group,"DurableAdmissionCancelled"),"failed cancellation receipt does not release shared point ownership");}
            finally{Query("DROP TRIGGER suppress_team_cancel ON revora_team_arena_admission_abandoned;DROP FUNCTION suppress_team_cancel()");}
            var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaCompletionPump");
            var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);
            Check((bool)Call(pumpType,pump,"Track",group) && (int)Call(pumpType,pump,"Tick",1)==1
                && !Kernel.Maps.ContainsKey(map.ID) && !TeamQualifier.QualifierList.Groups.ContainsKey(group.ID)
                && owners.All(p=>p.Entity.MapID==1002 && p.TeamQualifierGroup==null && !PendingTeamResult(p)),
                "completion queue cancels interrupted admission and returns entered players before releasing the map");
            Check(owners.Select(p=>p.ArenaPoints).SequenceEqual(points) && owners.All(p=>p.TeamArenaStatistic.TodayBattles==0
                && Scalar("SELECT todaybattles FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0)
                && Scalar("SELECT count(*) FROM revora_team_arena_intents WHERE winner=@uid",group.Player1.Entity.UID)==0,
                "partial-transfer cleanup preserves both balances and counters without manufacturing a result");
            Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="AlreadyCommitted"
                && Call(coordinator,null,"Cleanup",group).ToString()=="AlreadyCommitted", "cancelled transfer cleanup is idempotent");
        }finally{CleanupTeamFixture(group);}
    }

    static void VerifyTeamMixedRecovery(Map town) {
        VerifyTeamPendingFencedRecovery(town);
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var group=TeamCoordinatorGroup(town);var owners=TeamFixtureOwners(group);var invalid=owners[owners.Length-1];
        try {
            CommitTeamResult(group,true);
            lock(invalid.ItemSyncRoot)Call(cache,null,"Fail",invalid,"mixed-team-fixture",new InvalidOperationException("fixture"));
            var original=owners[0];var replacement=PointBuyer(town);Kernel.GamePool[original.Entity.UID]=replacement;
            try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && !Fenced(original) && !Fenced(replacement),
                "mixed team recovery cannot take over a replaced session or invalidate healthy originals on rejection");}
            finally{Kernel.GamePool[original.Entity.UID]=original;}
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(original.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Busy" && !Fenced(original),
                    "busy healthy member delays mixed team recovery before invalidating additional caches");}finally{release.Set();task.Wait();}
            }
            var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaCompletionPump");
            var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);
            var now=DateTime.UtcNow;pumpType.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
            Call(pumpType,pump,"Track",group);var map=group.Player1.Map;
            Check((int)Call(pumpType,pump,"Tick",1)==0 && owners.All(Fenced) && owners.All(p=>p.ArenaPoints==1000)
                && (int)Field(group,"DurableResultReady")==1 && Kernel.Maps.ContainsKey(map.ID)
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",group.Player1.Entity.UID)==1,
                "mixed team recovery fences the complete original roster, repairs one result and retains cleanup");
            foreach(var p in owners)Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);
            now=now.AddSeconds(1);
            Check((int)Call(pumpType,pump,"Tick",1)==1 && !Kernel.Maps.ContainsKey(map.ID),
                "repaired mixed team result cleans up after the original roster registrations release");
            foreach(var p in owners) {
                var loaded=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
                Check(loaded.TodayBattles==1 && loaded.ArenaPoints==Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID),
                    "ordinary loader retains each mixed-recovery participant result and shared point mirror");
            }
        }finally{CleanupTeamFixture(group);}
        var changed=TeamCoordinatorGroup(town);owners=TeamFixtureOwners(changed);
        try {
            CommitTeamResult(changed,true);
            lock(owners[0].ItemSyncRoot)Call(cache,null,"Fail",owners[0],"mixed-preimage-fixture",new InvalidOperationException("fixture"));
            Query("UPDATE arena SET arenapoints=1001 WHERE entityid=@uid","uid",(long)owners[1].Entity.UID);
            Check(Call(coordinator,null,"RecoverFenced",changed).ToString()=="Failed" && owners.All(Fenced)
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid",changed.Player1.Entity.UID)==0
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",owners[1].Entity.UID)==1001,
                "mixed recovery rejects a changed saved preimage without overwriting it or partially paying");
            Query("UPDATE arena SET arenapoints=1000 WHERE entityid=@uid","uid",(long)owners[1].Entity.UID);
            Check(Call(coordinator,null,"RecoverFenced",changed).ToString()=="Committed" && owners.All(Fenced),
                "restored fixture evidence permits retry while all old caches remain fenced");
        }finally{CleanupTeamFixture(changed);}
    }

    static void VerifyTeamDisconnectObservation(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var group=TeamCoordinatorGroup(town,false,false);var extra=PointBuyer(town);
        extra.Entity.Level=100;extra.ArenaPoints=1000;extra.TeamArenaStatistic.Name=extra.Entity.Name;
        ArenaTable.SaveArenaStatistics(extra.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(extra.TeamArenaStatistic);
        group.Player1.Team.Add(extra);group.Import();var owners=TeamFixtureOwners(group);
        var member=owners.First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2) && !ReferenceEquals(p,extra));
        try {
            var outsider=PointBuyer(town);outsider.TeamQualifierGroup=group;
            Check(Call(type,null,"ObserveDisconnect",group,outsider).ToString()=="NotReady"
                && ((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(group,"departedParticipants")).Count==0,
                "team disconnect observation rejects a forged group reference outside the admitted roster");
            Check(Call(type,null,"ObserveDisconnect",group,member).ToString()=="Retained" && !group.Done
                && Call(type,null,"ObserveDisconnect",group,member).ToString()=="Retained"
                && ((System.Collections.Generic.HashSet<Revora.Client.GameState>)Field(group,"departedParticipants")).Count==1,
                "nonleader departure stays retained without ending a team that still has living fighters");
            group.UpdateDamage(member,999);group.UpdateDamage(group.Player1,10);
            Check(group.Player1Damage==10,"retained disconnected member cannot score while a living teammate still can");
            member.Team=null;group.Player1.Entity.Hitpoints=0;
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(Field(group,"outcomeSync")){held.Set();release.Wait();}});held.Wait();
                try {
                    Check(Call(type,null,"ObserveDisconnect",group,extra).ToString()=="Busy" && !group.Done
                        && ReferenceEquals(Field(Field(group,"PendingDeparture"),"Player"),group.Player1),
                        "last living member departure records its original losing leader despite an earlier retained member");
                    Check(Call(type,null,"ObserveDisconnect",group,group.Player2).ToString()=="Busy"
                        && ReferenceEquals(Field(Field(group,"PendingDeparture"),"Player"),group.Player1),
                        "later opposing leader disconnect cannot reverse the retained team loss");
                }finally{release.Set();task.Wait();}
            }
            Check(Call(type,null,"RetryDeparture",group).ToString()=="Committed" && ReferenceEquals(group.Loser,group.Player1)
                && owners.All(p=>p.TeamArenaStatistic.TodayBattles==1),
                "deferred disconnect outcome pays the original admitted roster exactly once");group.Export();
        }finally{CleanupTeamFixture(group);}
        var combat=TeamCoordinatorGroup(town,false);
        try {
            var member2=TeamFixtureOwners(combat).First(p=>!ReferenceEquals(p,combat.Player1) && !ReferenceEquals(p,combat.Player2));
            Call(type,null,"ObserveDisconnect",combat,member2);combat.Player1.Entity.Hitpoints=0;
            combat.CheckEnd(combat.Player1);
            Check(combat.Done && ReferenceEquals(combat.Loser,combat.Player1)
                && Call(type,null,"Complete",combat).ToString()=="Committed",
                "all-dead combat check excludes retained departed members from the living side");combat.Export();
        }finally{CleanupTeamFixture(combat);}
    }

    static void VerifyTeamShutdownRetention(Map town) {
        VerifyOwnedTeamEntry(town);
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        foreach(bool failure in new[]{false,true}) {
            var group=TeamCoordinatorGroup(town,false);var owners=TeamFixtureOwners(group);
            int resumed=0,forced=0;bool postpone=!failure;
            Action<Revora.Client.GameState,bool> resume=(p,requested)=>{
                if(postpone && ReferenceEquals(p,group.Player1)){postpone=false;return;}
                if(!requested)forced++;
                if((bool)Call(typeof(Revora.Client.GameState),p,"TryBeginShutdown")){resumed++;Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);}
            };
            var retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
            try {
                Check((bool)Call(type,retention,"Attach",group) && (bool)Call(type,retention,"Attach",group),
                    "team shutdown retention attaches the original roster idempotently");
                var now=DateTime.UtcNow;var pump=Field(retention,"pump");pump.GetType().GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
                var member=owners.First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2));
                if(!failure) {
                    Check(!(bool)Call(typeof(Revora.Client.GameState),member,"TryBeginShutdown") && (int)Field(member,"shutdownStarted")==0
                        && Kernel.GamePool.ContainsKey(member.Entity.UID) && ReferenceEquals(Kernel.DisconnectPool[member.Entity.UID],member),
                        "actual shutdown gate retains nonterminal member and reconnect barrier without consuming one-shot teardown");
                    using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                        var task=Task.Run(()=>{lock(group.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                        try{Check(!(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown")
                            && (int)Field(group.Player1,"shutdownStarted")==0 && resumed==0,
                            "busy terminal team shutdown retains canonical actors before any teardown callback");}finally{release.Set();task.Wait();}
                    }
                } else {
                    member.Entity.Hitpoints=0; // Leader is the last living contestant in this failure fixture.
                    Query("ALTER TABLE revora_team_arena_statistics ADD CONSTRAINT retain_shutdown_failure CHECK(won) NOT VALID");
                    try{Check(!(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown") && owners.All(Fenced)
                        && owners.All(p=>Kernel.GamePool.ContainsKey(p.Entity.UID)) && resumed==0,
                        "failed team result keeps shutdown unconsumed and all original registrations available to recovery");}
                    finally{Query("ALTER TABLE revora_team_arena_statistics DROP CONSTRAINT retain_shutdown_failure");}
                }
                var map=group.Player1.Map;Call(type,retention,"Tick",1);
                if(!failure) {
                    Check((int)Field(group.Player1,"shutdownStarted")==0 && Kernel.GamePool.ContainsKey(group.Player1.Entity.UID)
                        && ReferenceEquals(Field(group.Player1,"ArenaShutdownRetention"),retention),
                        "resume without teardown keeps the original session retained for another attempt");
                    Call(type,retention,"Tick",1);
                }
                Check((int)Field(group,"DurableResultReady")==1 && resumed==(failure?3:2) && forced==(failure?2:0)
                    && owners.Where(p=>failure || !ReferenceEquals(p,group.Player2)).All(p=>!Kernel.GamePool.ContainsKey(p.Entity.UID)
                        && !Kernel.DisconnectPool.ContainsKey(p.Entity.UID) && (int)Field(p,"shutdownStarted")==1),
                    "settled team retention resumes requested shutdown once and releases fenced peers only after recovery");
                if(!failure)Check(Kernel.GamePool.ContainsKey(group.Player2.Entity.UID) && (int)Field(group.Player2,"shutdownStarted")==0,
                    "unrequested healthy teammate stays online after other sessions finish teardown");
                now=now.AddSeconds(1);Call(type,retention,"Tick",1);
                Check((int)type.GetProperty("Count",Instance).GetValue(retention,null)==0 && !Kernel.Maps.ContainsKey(map.ID)
                    && !(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown"),
                    "retention stays scheduled through map cleanup and cannot replay consumed shutdown");
            }finally{CleanupTeamFixture(group);}
        }
    }

    static void VerifySoloShutdownRetention(Map town) {
        VerifyOwnedSoloEntry(town);
        VerifySoloAdmissions(town);
        VerifyFencedArenaTimeouts(town);
        VerifySoloMixedRecovery(town);
        VerifySoloPendingFencedRecovery(town);
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDisconnectRetention");
        foreach(bool failure in new[]{false,true}) {
            var group=StatGroup(town,false);var owners=new[]{group.Player1,group.Player2};
            int resumed=0,forced=0;bool postpone=!failure;
            Action<Revora.Client.GameState,bool> resume=(p,requested)=>{
                if(postpone && ReferenceEquals(p,group.Player1)){postpone=false;return;}
                if(!requested)forced++;
                if((bool)Call(typeof(Revora.Client.GameState),p,"TryBeginShutdown")){resumed++;Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);}
            };
            var retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
            Check((bool)Call(type,retention,"Attach",group) && (bool)Call(type,retention,"Attach",group),
                "solo shutdown retention attaches only once to the admitted original pair");
            var now=DateTime.UtcNow;var pump=Field(retention,"pump");pump.GetType().GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
            if(!failure) {
                using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                    var task=Task.Run(()=>{lock(group.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                    try{Check(!(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown")
                        && (int)Field(group.Player1,"shutdownStarted")==0 && Kernel.GamePool.ContainsKey(group.Player1.Entity.UID)
                        && ReferenceEquals(Kernel.DisconnectPool[group.Player1.Entity.UID],group.Player1),
                        "busy solo disconnect preserves its canonical actor and reconnect barrier before teardown");}finally{release.Set();task.Wait();}
                }
            }else {
                Query("CREATE FUNCTION suppress_solo_retention() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_solo_retention BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_solo_retention()");
                try{Check(!(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown") && owners.All(Fenced)
                    && owners.All(p=>Kernel.GamePool.ContainsKey(p.Entity.UID)) && resumed==0,
                    "failed solo result retains both original sessions for recovery without consuming shutdown");}
                finally{Query("DROP TRIGGER suppress_solo_retention ON revora_arena_result_commits;DROP FUNCTION suppress_solo_retention()");}
            }
            var map=group.Player1.Map;Call(type,retention,"Tick",1);
            if(!failure) {
                Check((int)Field(group.Player1,"shutdownStarted")==0 && ReferenceEquals(Field(group.Player1,"ArenaShutdownRetention"),retention),
                    "solo resume callback that does not start teardown remains retryable");
                Call(type,retention,"Tick",1);
            }
            Check((int)Field(group,"DurableResultReady")==1 && resumed==(failure?2:1) && forced==(failure?1:0)
                && LedgerRows(group,"finished")==1 && !Kernel.GamePool.ContainsKey(group.Player1.Entity.UID)
                && !Kernel.DisconnectPool.ContainsKey(group.Player1.Entity.UID),
                "solo retention resumes requested teardown only after one durable result and releases invalidated peer after repair");
            if(!failure)Check(Kernel.GamePool.ContainsKey(group.Player2.Entity.UID) && (int)Field(group.Player2,"shutdownStarted")==0,
                "healthy solo opponent remains online when the departing player finishes teardown");
            now=now.AddSeconds(1);Call(type,retention,"Tick",1);
            Check((int)type.GetProperty("Count",Instance).GetValue(retention,null)==0 && !Kernel.Maps.ContainsKey(map.ID)
                && !(bool)Call(typeof(Revora.Client.GameState),group.Player1,"TryBeginShutdown"),
                "solo retention drains through owned cleanup without replaying the one-shot shutdown");
            var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",group.Player1.Entity.UID);
            Check(saved.TodayBattles==1 && saved.TotalLose==1,"ordinary reload retains the departing solo player's saved loss");
        }
    }

    static void VerifyOwnedTeamEntry(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        int resumed=0;bool postpone=true;
        Action<GameState,bool> resume=(p,requested)=>{if(postpone){postpone=false;return;}if((bool)Call(typeof(GameState),p,"TryBeginShutdown")){resumed++;Call(typeof(GameState),p,"ReleaseSessionRegistration",true);}};
        var retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
        var first=TeamCoordinatorGroup(town,false,false);var second=TeamCoordinatorGroup(town,false,false);
        Call(typeof(TeamQualifier),null,"ImportMatched",first,retention);
        Check(TeamFixtureOwners(first).Length==3
            && TeamFixtureOwners(first).All(p=>ReferenceEquals(Field(p,"ArenaShutdownRetention"),retention)),
            "owned team import retains every selected member before reservation and transfer");
        ImportWithBusyPlayer(typeof(TeamQualifier),second,second.Player1,retention);
        RunOwnedArenaScheduler(true);
        AdvanceOwnedPump(retention);
        var full=TeamCoordinatorGroup(town,false,false);Call(typeof(TeamQualifier),null,"ImportMatched",full,retention);
        Check(!TeamQualifier.QualifierList.Groups.ContainsKey(full.ID) && !(bool)full.GetType().GetProperty("ImportStarted",Instance).GetValue(full,null)
            && Scalar("SELECT count(*) FROM revora_team_arena_admissions WHERE first_leader=@uid",full.Player1.Entity.UID)==0,
            "uncontended team capacity refusal cleans invitation without reservation or transfer");
        Check(!(bool)second.GetType().GetProperty("ImportStarted",Instance).GetValue(second,null)
            && second.Player1.Entity.MapID==1002
            && !TeamQualifier.QualifierList.Groups.ContainsKey(second.ID) && second.Player1.TeamArenaStatistic.PlayWith==null
            && Scalar("SELECT count(*) FROM revora_team_arena_admissions WHERE first_leader=@uid",second.Player1.Entity.UID)==0,
            "team retention capacity rejects another roster before durable admission");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(first.Player1.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(!(bool)Call(typeof(GameState),first.Player2,"TryBeginShutdown"),"owned team entry retains departing sole-side player while settlement is busy");}
            finally{release.Set();task.Wait();}
        }
        RunOwnedArenaScheduler(true);
        Check(resumed==0 && !TeamQualifier.QualifierList.Groups.ContainsKey(first.ID)
            && (int)type.GetProperty("Count",Instance).GetValue(retention,null)==1,
            "team scheduler retains shutdown work after committed map cleanup removes its group");
        RunOwnedArenaScheduler(true);
        Check(resumed==1 && (int)type.GetProperty("Count",Instance).GetValue(retention,null)==0
            && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",first.Player2.Entity.UID)==1
            && Kernel.GamePool.ContainsKey(first.Player1.Entity.UID),"owned team entry settles once and keeps healthy opponents online");
        TeamQualifier.QualifierList.Groups.Remove(second.ID);
        foreach(bool disconnect in new[]{true,false}) {
            var group=TeamCoordinatorGroup(town,false,false);resumed=0;
            foreach(var p in new[]{group.Player1,group.Player2}) {
                TeamQualifier.PlayerList[p.Entity.UID]=p;TeamQualifier.WaitingPlayerList[p.Entity.UID]=p;
                p.TeamArenaStatistic.AcceptBox=p.TeamArenaStatistic.HasBox=true;
            }
            retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
            var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);bool fired=false,held=false;
            var source=Kernel.Maps[700];
            try {
                if(disconnect)clock.SetValue(null,new Func<DateTime>(()=>{
                    if(!fired){fired=true;held=!(bool)Call(typeof(GameState),group.Player1,"TryBeginShutdown");}
                    return DateTime.UtcNow;
                }));
                else Kernel.Maps[700]=null;
                Check(!(bool)Call(type,retention,"Import",group),"owned team entry rejects interrupted admission: disconnect="+disconnect);
            }finally{clock.SetValue(null,prior);Kernel.Maps[700]=source;}
            RunOwnedArenaScheduler(true);
            Check((int)type.GetProperty("Count",Instance).GetValue(retention,null)==0
                && (disconnect?(fired && held && resumed==1):(bool)Field(group,"DurableAdmissionCancelled"))
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid OR winner=@uid",group.Player1.Entity.UID)==0,
                "owned team rejection or cancelled transfer drains without awarding a result: disconnect="+disconnect);
            if(disconnect) {
                Check(!TeamQualifier.QualifierList.Groups.ContainsKey(group.ID)
                    && new[]{group.Player1,group.Player2}.All(p=>!TeamQualifier.PlayerList.ContainsKey(p.Entity.UID)
                        && !TeamQualifier.WaitingPlayerList.ContainsKey(p.Entity.UID) && !p.TeamArenaStatistic.AcceptBox
                        && !p.TeamArenaStatistic.HasBox && p.TeamArenaStatistic.PlayWith==null),
                    "rejected team admission clears both original invitations and queue registrations");
                group.CreateTime=Time32.Now.AddMinutes(-4);RunOwnedArenaScheduler(true);
                Check(!group.Done,"legacy scheduler cannot award a rejected owned team invitation after timeout");
            }
            TeamQualifier.QualifierList.Groups.Remove(group.ID);
        }
    }

    static void VerifyOwnedSoloEntry(Map town) {
        VerifySoloSignupRollover(town);
        VerifyTeamCounterRollover(town);
        VerifyTeamDailyMilestones(town);
        VerifyTeamDailyCounterRecovery(town);
        VerifyTeamAdmissionDays(town);
        VerifyUnstartedArenaResults(town);
        VerifyArenaSignupWithdrawals(town);
        VerifyWithdrawalAdmissionRaces(town);
        VerifyTeamSpectatorOwnership(town);
        VerifyOwnedSoloDialogs(town);
        VerifyOwnedArenaCallbacks(town);
        VerifyTeamCommandAuthority(town);
        VerifyTeamMemberWithdrawal(town);
        VerifyTeamRemovalCallbacks(town);
        VerifyTeamLeaderDisconnectSurvival(town);
        VerifyTeamPreparationCombat(town);
        VerifyTeamReceivedDamage(town);
        VerifyTeamPoisonOwnership(town);
        VerifyTeamBreakingTouchOwnership(town);
        VerifyTeamFlameOwnership(town);
        VerifyRejectedInvitationOwnership(town);
        VerifyPreparedMatchmakers(town);
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDisconnectRetention");
        int resumed=0;
        Action<GameState,bool> resume=(p,requested)=>{if((bool)Call(typeof(GameState),p,"TryBeginShutdown")){resumed++;Call(typeof(GameState),p,"ReleaseSessionRegistration",true);}};
        var retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
        var first=StatGroup(town,false,false);var second=StatGroup(town,false,false);
        Call(typeof(Qualifier),null,"ImportMatched",first,retention);
        Check(LedgerRows(first,"active")==1
            && ReferenceEquals(Field(first.Player1,"ArenaShutdownRetention"),retention)
            && ReferenceEquals(Field(first.Player2,"ArenaShutdownRetention"),retention),
            "owned solo entry reserves session retention before its real registered import");
        ImportWithBusyPlayer(typeof(Qualifier),second,second.Player1,retention);
        RunOwnedArenaScheduler(false);
        AdvanceOwnedPump(retention);
        var full=StatGroup(town,false,false);Call(typeof(Qualifier),null,"ImportMatched",full,retention);
        Check(!Qualifier.QualifierList.Groups.ContainsKey(full.ID) && (int)Field(full,"imported")==0
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",full.Player1.Entity.UID)==0,
            "uncontended solo capacity refusal cleans invitation without reservation or transfer");
        Check((int)Field(second,"imported")==0
            && second.Player1.Entity.MapID==1002 && second.Player2.Entity.MapID==1002
            && !Qualifier.QualifierList.Groups.ContainsKey(second.ID) && second.Player1.ArenaState==Qualifier.None
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",second.Player1.Entity.UID)==0,
            "full solo retention capacity prevents reservation and transfer for another match");
        Check(!(bool)Call(typeof(GameState),first.Player1,"TryBeginShutdown"),"owned entry retains its departing original session for settlement");
        RunOwnedArenaScheduler(false);
        Check(resumed==1 && LedgerRows(first,"finished")==1 && (int)type.GetProperty("Count",Instance).GetValue(retention,null)==0
            && Kernel.GamePool.ContainsKey(first.Player2.Entity.UID),
            "owned entry completes the ordinary saved result and resumes only the departing player");
        Group removed;Qualifier.QualifierList.Groups.TryRemove(second.ID,out removed);
        foreach(int at in new[]{1,2}) {
            var group=StatGroup(town,false,false);resumed=0;
            if(at==1)foreach(var p in new[]{group.Player1,group.Player2}) {
                Qualifier.PlayerList[p.Entity.UID]=p;Qualifier.WaitingPlayerList[p.Entity.UID]=p;
            }
            retention=Activator.CreateInstance(type,Instance,null,new object[]{1,resume},null);
            var pump=Field(retention,"pump");var now=DateTime.UtcNow;
            pump.GetType().GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
            var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);
            int calls=0;bool fired=false,held=false;
            clock.SetValue(null,new Func<DateTime>(()=>{
                if(++calls==at && !fired) {
                    fired=true;
                    held=!(bool)Call(typeof(GameState),group.Player1,"TryBeginShutdown")
                        && (int)Field(group.Player1,"shutdownStarted")==0;
                }
                return now;
            }));
            try{Check(!(bool)Call(type,retention,"Import",group) && fired && held && resumed==0,
                "entry-time disconnect remains retained before any teardown: stage="+at);}
            finally{clock.SetValue(null,prior);}
            Check(LedgerRows(group,"active")==0 && ArenaReceipts(group.Player1)==0 && ArenaReceipts(group.Player2)==0,
                "entry-time disconnect cannot commit a partial registered start: stage="+at);
            Call(type,retention,"Tick",1);now=now.AddSeconds(2);Call(type,retention,"Tick",1);
            Check(resumed==(at==1?1:2) && (int)type.GetProperty("Count",Instance).GetValue(retention,null)==0
                && !Kernel.DisconnectPool.ContainsKey(group.Player1.Entity.UID)
                && !group.Done && (at==1 || (bool)Field(group,"CleanupCompleted")),
                "rejected or rolled-back owned entry drains shutdown and cleanup without a result: stage="+at);
            if(at==1)Check(!Qualifier.QualifierList.Groups.ContainsKey(group.ID)
                && new[]{group.Player1,group.Player2}.All(p=>!Qualifier.PlayerList.ContainsKey(p.Entity.UID)
                    && !Qualifier.WaitingPlayerList.ContainsKey(p.Entity.UID) && !p.ArenaStatistic.AcceptBox
                    && p.ArenaStatistic.PlayWith==null && p.ArenaState==Qualifier.None),
                "rejected solo admission clears both original invitations and queue registrations");
            Qualifier.QualifierList.Groups.TryRemove(group.ID,out removed);
        }
    }

    static void VerifyRejectedInvitationOwnership(Map town) {
        foreach(bool team in new[]{false,true}) {
            object group;GameState a,b;
            if(team){var g=TeamCoordinatorGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;}
            else{var g=StatGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;}
            var replacement=PointBuyer(town);
            var queued=team?TeamQualifier.PlayerList:Qualifier.PlayerList;
            var waiting=team?TeamQualifier.WaitingPlayerList:Qualifier.WaitingPlayerList;
            Kernel.GamePool[a.Entity.UID]=replacement;queued[a.Entity.UID]=replacement;waiting[a.Entity.UID]=replacement;
            queued[b.Entity.UID]=b;waiting[b.Entity.UID]=b;
            if(team){b.TeamArenaStatistic.PlayWith=replacement;b.TeamArenaStatistic.AcceptBox=true;}
            else{b.ArenaStatistic.PlayWith=replacement;b.ArenaStatistic.AcceptBox=true;}
            try {
                Check((bool)Call(group.GetType(),group,"RejectUnimported")
                    && ReferenceEquals(Kernel.GamePool[a.Entity.UID],replacement)
                    && ReferenceEquals(queued[a.Entity.UID],replacement) && ReferenceEquals(waiting[a.Entity.UID],replacement)
                    && ReferenceEquals(queued[b.Entity.UID],b) && ReferenceEquals(waiting[b.Entity.UID],b)
                    && (team?b.TeamArenaStatistic.AcceptBox:b.ArenaStatistic.AcceptBox)
                    && ReferenceEquals(team?b.TeamArenaStatistic.PlayWith:b.ArenaStatistic.PlayWith,replacement),
                    "rejected invitation cleanup preserves replacement registrations and newer opponent: team="+team);
            }finally {
                Kernel.GamePool[a.Entity.UID]=a;GameState removed;
                foreach(var p in new[]{a,b}){queued.TryRemove(p.Entity.UID,out removed);waiting.TryRemove(p.Entity.UID,out removed);}
            }
        }
    }

    static void ImportWithBusyPlayer(Type qualifier,object group,GameState player,object owner) {
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(player.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try {
                Call(qualifier,null,"ImportMatched",group,owner);
                Check((bool)Field(group,"OwnedEntryRequested") && Field(group,"CompletionOwner")==null
                    && (int)owner.GetType().GetProperty("Count",Instance).GetValue(owner,null)==1,
                    "busy matchmaking rejection keeps retry ownership without another retention slot: "+qualifier.Name);
            }finally{release.Set();task.Wait();}
        }
    }

    static void AdvanceOwnedPump(object owner) {
        var pump=Field(owner,"pump");var now=DateTime.UtcNow.AddSeconds(2);
        pump.GetType().GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
    }

    static void VerifyPreparedMatchmakers(Map town) {
        var solo=StatGroup(town,false,false);Group removed;Qualifier.QualifierList.Groups.TryRemove(solo.ID,out removed);
        foreach(var p in new[]{solo.Player1,solo.Player2}) {
            Query("UPDATE entities SET level=@level WHERE uid=@uid","level",(int)p.Entity.Level,"uid",(long)p.Entity.UID);
            p.ArenaStatistic.PlayWith=ReferenceEquals(p,solo.Player1)?solo.Player2:solo.Player1;
            p.ArenaStatistic.AcceptBox=true;p.ArenaState=Qualifier.WaitForOther;Qualifier.WaitingPlayerList[p.Entity.UID]=p;
        }
        Qualifier.VerifyAwaitingPeople();var admitted=solo.Player1.QualifierGroup;
        Check(admitted!=null && (bool)Field(admitted,"OwnedEntryRequested") && Field(admitted,"CompletionOwner")!=null
            && LedgerRows(admitted,"active")==1,"prepared solo matchmaker creates a retained registered match for ordinary players");
        admitted.End(admitted.Player2);RunOwnedArenaScheduler(false);
        Check(LedgerRows(admitted,"finished")==1 && (bool)Field(admitted,"CleanupCompleted")
            && solo.Player1.ArenaStatistic.PlayWith==null,"real solo matchmaking and scheduler complete saved result and invitation cleanup");

        var team=TeamCoordinatorGroup(town,false,false);TeamQualifier.QualifierList.Groups.Remove(team.ID);
        foreach(var p in team.Player1.Team.Teammates.Concat(team.Player2.Team.Teammates)) {
            p.Entity.Level=p.ArenaStatistic.Level=p.TeamArenaStatistic.Level=70;
            Query("UPDATE entities SET level=70 WHERE uid=@uid;UPDATE arena SET level=70 WHERE entityid=@uid;UPDATE teamarena SET level=70 WHERE entityid=@uid", "uid",(long)p.Entity.UID);
        }
        var underLevel=PointBuyer(town);underLevel.Entity.Level=69;team.Player1.Team.Add(underLevel);
        team.Player1.TeamArenaStatistic.PlayWith=null;
        team.Player1.TeamArenaStatistic.Status=TeamQualifierStatistic.WaitingForOpponent;
        team.Player1.Entity.Level=69;
        Check(!(bool)Call(typeof(TeamQualifier),null,"CanJoin",team.Player1,Time32.Now),"level-69 ordinary leader cannot be matched");
        team.Player1.Entity.Level=70;
        Check((bool)Call(typeof(TeamQualifier),null,"CanJoin",team.Player1,Time32.Now),"level-70 ordinary leader is eligible for matching");
        team.Player1.TeamArenaStatistic.PlayWith=team.Player2;
        foreach(var p in new[]{team.Player1,team.Player2}) {
            p.TeamArenaStatistic.AcceptBox=p.TeamArenaStatistic.HasBox=true;TeamQualifier.WaitingPlayerList[p.Entity.UID]=p;
        }
        TeamQualifier.VerifyAwaitingPeople();var admittedTeam=team.Player1.TeamQualifierGroup;
        Check(admittedTeam!=null && (bool)Field(admittedTeam,"OwnedEntryRequested") && Field(admittedTeam,"CompletionOwner")!=null
            && TeamFixtureOwners(admittedTeam).Length==3 && TeamFixtureOwners(admittedTeam).All(p=>p.Entity.Level==70)
            && underLevel.TeamQualifierGroup==null && underLevel.Entity.MapID==town.ID,
            "prepared team matchmaker admits level-70 leaders and member while excluding the level-69 teammate");
        admittedTeam.End(team.Player2);RunOwnedArenaScheduler(true);
        Check((bool)admittedTeam.GetType().GetProperty("ResultCleanupCompleted",Instance).GetValue(admittedTeam,null)
            && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",team.Player2.Entity.UID)==1
            && team.Player1.TeamArenaStatistic.PlayWith==null,"real team matchmaking and scheduler complete saved result and invitation cleanup");
        Check(Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",underLevel.Entity.UID)==0
            && ((TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",team.Player1.Entity.UID)).TodayWin==1,
            "level-70 result survives production reload without awarding the excluded teammate");
    }

    static void VerifyTeamCommandAuthority(Map town) {
        var leader=PointBuyer(town);var member=PointBuyer(town);var target=PointBuyer(town);
        foreach(var p in new[]{leader,member,target}) {
            p.Partners=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.TradePartner>();
            p.Apprentices=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Apprentice>();
            p.Friends=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Friend>();
            p.Enemy=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Enemy>();
        }
        var team=new Revora.Game.ConquerStructures.Team(leader);leader.Team=team;team.Add(member);team.Add(target);
        Action<GameState,uint,uint> send=(p,kind,uid)=>Revora.Network.GamePackets.Team.Process(
            new Revora.Network.GamePackets.Team{Type=kind,UID=uid}.ToArray(),p);
        foreach(uint kind in new uint[]{6,7,8,9,10,11,12,13}) {
            team.ForbidJoin=kind==9;team.PickupMoney=kind!=11;team.PickupItems=kind!=13;
            bool join=team.ForbidJoin,money=team.PickupMoney,items=team.PickupItems;
            send(member,kind,target.Entity.UID);send(member,kind,leader.Entity.UID);
            Check(team.Members.Count==3 && ReferenceEquals(team.Lider,leader) && ReferenceEquals(member.Team,team)
                && team.ForbidJoin==join && team.PickupMoney==money && team.PickupItems==items,
                "member cannot forge team administration with target or leader UID: "+kind);
        }
        foreach(uint kind in new uint[]{8,9,10,11,12,13}) {
            send(leader,kind,leader.Entity.UID);
            Check(kind==8?team.ForbidJoin:kind==9?!team.ForbidJoin:kind==10?!team.PickupMoney:
                kind==11?team.PickupMoney:kind==12?!team.PickupItems:team.PickupItems,
                "ordinary current leader retains team settings command: "+kind);
        }
        var replacement=PointBuyer(town);uint replacementUid=replacement.Entity.UID;
        try {
            replacement.Entity.UID=leader.Entity.UID;replacement.Team=team;Kernel.GamePool[leader.Entity.UID]=replacement;
            team.ForbidJoin=false;send(leader,8,0);send(replacement,8,0);
            Check(!team.ForbidJoin,"neither stale leader nor same-UID replacement inherits team authority");
        }finally{Kernel.GamePool[leader.Entity.UID]=leader;replacement.Entity.UID=replacementUid;replacement.Team=null;}
        Revora.Network.GamePackets.Team.Process(null,leader);
        Revora.Network.GamePackets.Team.Process(new byte[11],leader);
        Check(team.Members.Count==3,"short or absent team request cannot mutate membership");
        send(leader,7,target.Entity.UID);
        Check(target.Team==null && team.Members.Count==2,"current ordinary leader can kick a member");
        send(member,2,leader.Entity.UID);
        Check(member.Team==null && team.Members.Count==1 && ReferenceEquals(leader.Team,team),
            "member may exit only its own membership regardless of packet target");
        send(leader,6,leader.Entity.UID);
        Check(leader.Team==null && team.Members.Count==0,"current leader retains dismiss command");
        send(target,6,target.Entity.UID);
        Check(target.Team==null,"teamless dismiss is harmless");
        team=new Revora.Game.ConquerStructures.Team(leader);leader.Team=team;team.Add(member);
        send(member,0,member.Entity.UID);send(leader,0,leader.Entity.UID);
        Check(ReferenceEquals(member.Team,team) && ReferenceEquals(leader.Team,team) && team.Members.Count==2,
            "Create Team cannot abandon existing leader or member ownership");
        send(leader,5,uint.MaxValue);
        Check(team.Members.Count==2,"accepting a missing team applicant is harmless");
        send(target,3,member.Entity.UID);
        Check(target.Team==null && team.Members.Count==2,"invitation acceptance must name the actual current leader");
        target.Entity.Hitpoints=0;send(leader,5,target.Entity.UID);
        Check(target.Team==null && team.Members.Count==2,"leader cannot admit a dead applicant");
        target.Entity.Hitpoints=target.Entity.MaxHitpoints;
        send(target,3,leader.Entity.UID);send(leader,5,target.Entity.UID);
        Check(target.Team==null,"neither acceptance direction can create membership without the other player's consent");
        send(leader,4,target.Entity.UID);send(target,1,leader.Entity.UID);
        try {
            replacement.Entity.UID=leader.Entity.UID;replacement.Team=team;Kernel.GamePool[leader.Entity.UID]=replacement;
            send(replacement,5,target.Entity.UID);send(target,3,leader.Entity.UID);
            Check(target.Team==null && team.Members.Count==2,
                "same-UID replacement cannot admit applicants or act as an invitation's original leader");
        }finally{Kernel.GamePool[leader.Entity.UID]=leader;replacement.Entity.UID=replacementUid;replacement.Team=null;}
        send(target,3,leader.Entity.UID);send(target,3,leader.Entity.UID);
        Check(ReferenceEquals(target.Team,team) && team.Members.Count==3,
            "ordinary invitation acceptance joins the current leader's team exactly once");
        send(target,2,target.Entity.UID);send(leader,5,target.Entity.UID);
        Check(target.Team==null,"a consumed application cannot be replayed after leaving the team");
        send(target,1,leader.Entity.UID);send(leader,5,target.Entity.UID);send(leader,5,target.Entity.UID);
        Check(ReferenceEquals(target.Team,team) && team.Members.Count==3,
            "ordinary leader acceptance joins a current living applicant exactly once");
        send(target,2,target.Entity.UID);
        var entryClock=typeof(Revora.Network.GamePackets.Team).GetField("EntryClock",Hidden);var originalClock=entryClock.GetValue(null);
        var entryNow=new Time32(uint.MaxValue-30000);entryClock.SetValue(null,new Func<Time32>(()=>entryNow));
        try {
            send(leader,4,target.Entity.UID);send(leader,5,target.Entity.UID);
            Check(target.Team==null,"an invitation cannot be consumed as a player join application");
            entryNow=new Time32(unchecked(entryNow.Value+60000));send(target,3,leader.Entity.UID);
            Check(target.Team==null,"team consent expires at sixty seconds across a wrapped timer");
            send(leader,4,target.Entity.UID);
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(leader.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{send(target,3,leader.Entity.UID);Check(target.Team==null,"busy leader defers acceptance without consuming consent");}
                finally{release.Set();task.Wait();}
            }
            Parallel.For(0,4,n=>send(target,3,leader.Entity.UID));
            Check(ReferenceEquals(target.Team,team) && team.Members.Count==3,"concurrent invitation retries join once after busy ownership clears");
            send(target,2,target.Entity.UID);send(leader,4,target.Entity.UID);
            var old=team;send(leader,6,leader.Entity.UID);send(leader,0,leader.Entity.UID);send(target,3,leader.Entity.UID);
            Check(target.Team==null && !ReferenceEquals(leader.Team,old),"consent to an old team cannot admit a player into a newly created team");
        }finally{entryClock.SetValue(null,originalClock);}
        VerifyTeamMembershipOwnership(town);
    }

    static void VerifyTeamMembershipOwnership(Map town) {
        var left=PointBuyer(town);var right=PointBuyer(town);var candidate=PointBuyer(town);
        var a=new Revora.Game.ConquerStructures.Team(left);var b=new Revora.Game.ConquerStructures.Team(right);
        candidate.Entity.Level=5;
        Parallel.Invoke(()=>a.Add(candidate),()=>b.Add(candidate));
        var joined=candidate.Team;var other=ReferenceEquals(joined,a)?b:a;
        Check(joined!=null && a.Members.Count+b.Members.Count==3 && joined.Members.ContainsKey(candidate.Entity.UID)
            && !other.Members.ContainsKey(candidate.Entity.UID),"concurrent core team additions cannot own one player in two teams");
        var membership=joined.Members[candidate.Entity.UID];
        Parallel.For(0,4,n=>joined.Add(candidate));
        Check(joined.Members.Count==2 && ReferenceEquals(joined.Members[candidate.Entity.UID],membership),
            "repeated core additions preserve the original membership entry");
        var replacement=PointBuyer(town);uint replacementUid=replacement.Entity.UID;
        try {
            replacement.Entity.UID=candidate.Entity.UID;replacement.Team=joined;joined.Remove(replacement);
            Check(joined.Members.Count==2 && ReferenceEquals(candidate.Team,joined),
                "same-UID stale removal cannot remove the original team member");
        }finally{replacement.Entity.UID=replacementUid;replacement.Team=null;}
        Parallel.For(0,4,n=>joined.Remove(candidate));
        Check(candidate.Team==null && joined.Members.Count==1 && joined.LowestLevel==joined.Lider.Entity.Level
            && joined.LowestLevelsUID==joined.Lider.Entity.UID,"concurrent removals happen once and recalculate lowest-level ownership");
        other.Add(candidate);membership.Dispose();
        Check(ReferenceEquals(candidate.Team,other) && other.Members.ContainsKey(candidate.Entity.UID),
            "disposing an old membership cannot clear a newer team's ownership");
        var applicants=Enumerable.Range(0,7).Select(n=>PointBuyer(town)).ToArray();
        Parallel.ForEach(applicants,p=>joined.Add(p));
        Check(joined.Members.Count==5 && applicants.Count(p=>ReferenceEquals(p.Team,joined))==4
            && applicants.All(p=>p.Team==null || joined.Members.ContainsKey(p.Entity.UID)),
            "concurrent core additions enforce capacity without orphaned player team references");
        var oldLeader=joined.Lider;var survivors=joined.Members.Values.Where(m=>!ReferenceEquals(m.entry,oldLeader)).ToArray();
        joined.Remove(oldLeader);
        Check(oldLeader.Team==null && joined.Members.Count==4 && survivors.All(m=>ReferenceEquals(joined.Members[m.entry.Entity.UID],m)
            && ReferenceEquals(m.entry.Team,joined)) && joined.Members.Values.Count(m=>m.Lider)==1,
            "leadership transfer preserves every surviving membership instead of removing and re-adding teammates");
        VerifyTeamCommandInterleavings(town);
    }

    static void VerifyTeamCommandInterleavings(Map town) {
        var leader=PointBuyer(town);var member=PointBuyer(town);var target=PointBuyer(town);
        var team=new Revora.Game.ConquerStructures.Team(leader);team.Add(member);team.Add(target);
        Action<GameState,uint,uint> send=(p,kind,uid)=>Revora.Network.GamePackets.Team.Process(
            new Revora.Network.GamePackets.Team{Type=kind,UID=uid}.ToArray(),p);
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(target.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{send(leader,7,target.Entity.UID);send(target,2,0);
                Check(ReferenceEquals(target.Team,team) && team.Members.Count==3,
                    "busy target rejects kick and exit before any team mutation");}
            finally{release.Set();task.Wait();}
        }
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var gate=Field(team,"MembershipSync");var task=Task.Run(()=>{lock(gate){held.Set();release.Wait();}});held.Wait();
            try{send(leader,8,0);Check(!team.ForbidJoin,"busy roster ownership rejects settings changes without waiting or mutation");}
            finally{release.Set();task.Wait();}
        }
        send(leader,8,0);send(leader,9,0);
        Check(!team.ForbidJoin,"ordinary leader settings retry succeeds after roster ownership clears");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(leader.ItemSyncRoot){held.Set();release.Wait();team.Remove(leader);}});held.Wait();
            try{send(leader,7,target.Entity.UID);}
            finally{release.Set();task.Wait();}
        }
        send(leader,7,target.Entity.UID);send(leader,8,0);
        Check(leader.Team==null && ReferenceEquals(target.Team,team) && !team.ForbidJoin && ReferenceEquals(team.Lider,member),
            "a retried command cannot reuse leadership lost while original ownership was busy");
        Parallel.Invoke(()=>send(member,7,target.Entity.UID),()=>send(target,2,0));
        Check(target.Team==null && team.Members.Count==1 && ReferenceEquals(member.Team,team),
            "concurrent authorized kick and voluntary exit remove exactly one original membership");
    }

    static void VerifyTeamMemberWithdrawal(Map town) {
        var retentionType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        var service=typeof(TeamQualifier).GetField("SignupWithdrawals",Hidden).GetValue(null);
        foreach(bool giveUp in new[]{false,true})foreach(bool lastAlive in new[]{false,true}) {
            var group=TeamCoordinatorGroup(town,false,false);
            var owner=Activator.CreateInstance(retentionType,Instance,null,new object[]{1,null},null);
            Call(typeof(TeamQualifier),null,"ImportMatched",group,owner);
            var member=TeamFixtureOwners(group).First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2));
            uint points=member.ArenaPoints;
            Action invoke=()=>{if(giveUp)TeamQualifier.QualifyEngine.DoGiveUp(member);else TeamQualifier.QualifyEngine.DoQuit(member);};
            if(lastAlive)group.Player1.Entity.Hitpoints=0;
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(member.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    invoke();invoke();
                    Check((bool)Call(service.GetType(),service,"Pending",member) && member.Entity.MapID!=1002
                        && member.ArenaPoints==points,"busy member withdrawal retains one request without releasing rewards");
                }finally{release.Set();task.Wait();}
            }
            Call(service.GetType(),service,"Tick");
            Check(member.Entity.MapID==1002 && ReferenceEquals(member.TeamQualifierGroup,group)
                && TeamFixtureOwners(group).Contains(member) && member.ArenaPoints==points,
                "voluntary return preserves admitted member and pending statistic ownership");
            uint damage=group.Player1Damage;group.UpdateDamage(member,1234);
            TeamQualifier.QualifyEngine.DoSignup(member);
            Check(group.Player1Damage==damage && !TeamQualifier.PlayerList.ContainsKey(member.Entity.UID),
                "returned member cannot add match damage or re-enlist before settlement");
            invoke();
            Check((Field(group,"PendingDeparture")!=null)==lastAlive && !group.Done,
                "member exit retains surviving side or records defeat for its last living contestant");
            if(!lastAlive)TeamQualifier.QualifyEngine.DoGiveUp(group.Player2);
            RunOwnedArenaScheduler(true);RunOwnedArenaScheduler(true);
            Check(member.TeamQualifierGroup==null && member.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",member.Entity.UID)==1
                && Scalar("SELECT todaybattles FROM teamarena WHERE entityid=@uid",member.Entity.UID)==1
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",member.Entity.UID)==member.ArenaPoints,
                "returned member receives one durable whole-roster result before ownership cleanup");
            uint settled=member.ArenaPoints;RunOwnedArenaScheduler(true);
            Check(member.ArenaPoints==settled && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",member.Entity.UID)==1,
                "member departure replay cannot duplicate its saved result");
        }
    }

    static void VerifyTeamRemovalCallbacks(Map town) {
        var retentionType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        foreach(string action in new[]{"member-exit","kick","leader-exit","dismiss"}) {
            var group=TeamCoordinatorGroup(town,false,false);
            var owner=Activator.CreateInstance(retentionType,Instance,null,new object[]{1,null},null);
            Call(typeof(TeamQualifier),null,"ImportMatched",group,owner);
            var member=TeamFixtureOwners(group).First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2));
            bool leaderLeaves=action=="leader-exit" || action=="dismiss";
            var departing=leaderLeaves?group.Player1:member;
            var sender=action=="member-exit"?member:group.Player1;
            var packet=new Revora.Network.GamePackets.Team{Type=action=="kick"?7u:action=="dismiss"?6u:2u,UID=departing.Entity.UID}.ToArray();
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(group.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    Revora.Network.GamePackets.Team.Process(packet,sender);
                    Check(departing.Team==null && ReferenceEquals(departing.TeamQualifierGroup,group)
                        && TeamFixtureOwners(group).Length==3 && departing.ArenaPoints==1000,
                        "team removal preserves every admitted reward owner while result is busy: "+action);
                    Check(leaderLeaves?Field(group,"PendingDeparture")!=null:departing.Entity.MapID==1002 && Field(group,"PendingDeparture")==null,
                        "team command routes original leader forfeit or member return: "+action);
                    if(leaderLeaves)Check(ReferenceEquals(member.Team.Lider,member),
                        "ordinary team leadership transfer does not replace original Arena leader: "+action);
                    // A teamless replay must not clear the retained match reference.
                    Revora.Network.GamePackets.Team.Process(packet,sender);
                    Check(ReferenceEquals(departing.TeamQualifierGroup,group) && departing.ArenaPoints==1000,
                        "replayed removal cannot prematurely pay or release the Arena member: "+action);
                }finally{release.Set();task.Wait();}
            }
            if(!leaderLeaves)TeamQualifier.QualifyEngine.DoGiveUp(group.Player2);
            RunOwnedArenaScheduler(true);RunOwnedArenaScheduler(true);
            Check(TeamFixtureOwners(group).All(p=>p.TeamQualifierGroup==null && p.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==1
                && Scalar("SELECT todaybattles FROM teamarena WHERE entityid=@uid",p.Entity.UID)==1
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==p.ArenaPoints),
                "team removal settles and cleans the original complete roster exactly once: "+action);
        }
    }

    static void VerifyTeamLeaderDisconnectSurvival(Map town) {
        var retentionType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        foreach(bool memberWins in new[]{false,true}) {
            var group=TeamCoordinatorGroup(town,false,false);int resumed=0;
            Action<GameState,bool> resume=(p,requested)=>{if((bool)Call(typeof(GameState),p,"TryBeginShutdown")) {
                resumed++;Call(typeof(GameState),p,"ReleaseSessionRegistration",true);
            }};
            var owner=Activator.CreateInstance(retentionType,Instance,null,new object[]{1,resume},null);
            Call(typeof(TeamQualifier),null,"ImportMatched",group,owner);
            var owners=TeamFixtureOwners(group);var leader=group.Player1;
            var member=owners.First(p=>!ReferenceEquals(p,leader) && !ReferenceEquals(p,group.Player2));
            Check(!(bool)Call(typeof(GameState),leader,"TryBeginShutdown") && Field(group,"PendingDeparture")==null
                && !group.Done && resumed==0 && Kernel.GamePool.ContainsKey(leader.Entity.UID),
                "leader disconnect retains the original actor without defeating its surviving teammate");
            group.UpdateDamage(leader,500);group.UpdateDamage(member,25);
            RunOwnedArenaScheduler(true);
            Check(group.Player1Damage==25 && !group.Done && resumed==0
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE winner=@uid OR loser=@uid",leader.Entity.UID)==0,
                "living member keeps scoring while disconnected leader cannot score or mint an early result");
            if(memberWins){group.Player2.Entity.Hitpoints=0;Call(typeof(GameState),group.Player2,"EndQualifier");}
            else {member.Entity.Hitpoints=0;Call(typeof(GameState),member,"EndQualifier");}
            AdvanceOwnedPump(owner);
            RunOwnedArenaScheduler(true);RunOwnedArenaScheduler(true);
            Check(group.Done && ReferenceEquals(group.Winner,memberWins?leader:group.Player2)
                && owners.All(p=>Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==1)
                && Scalar("SELECT todaywin FROM teamarena WHERE entityid=@uid",leader.Entity.UID)==(memberWins?1:0),
                "surviving member's combat decides one persisted original-roster outcome: wins="+memberWins);
            Check(resumed==1 && !Kernel.GamePool.ContainsKey(leader.Entity.UID)
                && !Kernel.DisconnectPool.ContainsKey(leader.Entity.UID) && member.TeamQualifierGroup==null
                && member.Entity.MapID==1002 && Kernel.GamePool.ContainsKey(member.Entity.UID),
                "only requested leader teardown resumes after whole-roster settlement and member cleanup");
            RunOwnedArenaScheduler(true);
            Check(resumed==1 && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",leader.Entity.UID)==1,
                "repeated scheduler cannot repeat departed leader reward or teardown");
        }
    }

    static void VerifyTeamPreparationCombat(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=type.GetField("MatchClock",Hidden);var prior=clock.GetValue(null);
        try {
            foreach(bool wrap in new[]{false,true}) {
                var group=TeamCoordinatorGroup(town,false);var a=group.Player1;var b=group.Player2;
                try {
                    a.Entity.PKMode=b.Entity.PKMode=Revora.Game.Enums.PKMode.PK;a.Attackable=b.Attackable=true;
                    group.ImportTime=new Time32(wrap?int.MaxValue-1000:100000);
                    var now=group.ImportTime;clock.SetValue(null,new Func<Time32>(()=>now));
                    Func<bool> attack=()=>Revora.Game.Attacking.Handle.CanAttack(a.Entity,b.Entity,null,true);
                    Check(!attack(),"shared attack permission blocks Team Arena damage immediately after import");
                    now=new Time32(unchecked(group.ImportTime.Value+9999));
                    Check(!attack(),"team preparation protects both sides until ten seconds have elapsed");
                    now=new Time32(unchecked(group.ImportTime.Value+10000));
                    Check(attack(),"ordinary opposing contestants may attack at the exact ten-second boundary under tick wrap="+wrap);
                    a.TeamQualifierGroup=null;now=group.ImportTime;
                    Check(!attack(),"target preparation protection cannot be bypassed by an attacker without its group reference");
                    a.TeamQualifierGroup=group;
                    now=new Time32(unchecked(group.ImportTime.Value+10000));
                    var member=TeamFixtureOwners(group).First(p=>!ReferenceEquals(p,a) && !ReferenceEquals(p,b));
                    member.Entity.PKMode=Revora.Game.Enums.PKMode.PK;member.Attackable=true;
                    Check(!Revora.Game.Attacking.Handle.CanAttack(a.Entity,member.Entity,null,true),
                        "full PK mode cannot attack an originally admitted teammate");
                    var oldTeam=member.Team;member.Team=null;
                    Check(!Revora.Game.Attacking.Handle.CanAttack(a.Entity,member.Entity,null,true),
                        "ordinary team removal cannot turn an admitted teammate into an Arena target");member.Team=oldTeam;
                    using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                        var task=Task.Run(()=>{lock(Field(group,"outcomeSync")){held.Set();release.Wait();}});held.Wait();
                        try{Check(!attack(),"busy match ownership declines attack permission without waiting for settlement");}
                        finally{release.Set();task.Wait();}
                    }
                    var outsider=PointBuyer(town);outsider.TeamQualifierGroup=group;
                    Check(!Revora.Game.Attacking.Handle.CanAttack(outsider.Entity,b.Entity,null,true),
                        "forged match reference does not admit an outside attacker");outsider.TeamQualifierGroup=null;
                    now=new Time32(unchecked(group.ImportTime.Value+300000));
                    Check(!attack(),"elapsed Team Arena deadline blocks new attacks before scheduler settlement");
                    now=new Time32(unchecked(group.ImportTime.Value+10000));
                    Call(group.GetType(),group,"RecordDisconnect",a);
                    Check(!attack() && !Revora.Game.Attacking.Handle.CanAttack(b.Entity,a.Entity,null,true)
                        && Revora.Game.Attacking.Handle.CanAttack(member.Entity,b.Entity,null,true),
                        "retained disconnected actor cannot attack or be attacked while a live teammate fights");
                    group.End(b);
                    Check(!Revora.Game.Attacking.Handle.CanAttack(member.Entity,b.Entity,null,true),
                        "completed match blocks further combat before result cleanup");
                }finally{a.TeamQualifierGroup=group;CleanupTeamFixture(group);}
            }
        }finally{clock.SetValue(null,prior);}
    }

    static void VerifyTeamReceivedDamage(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=type.GetField("MatchClock",Hidden);var prior=clock.GetValue(null);
        var group=TeamCoordinatorGroup(town,false,false);var a=group.Player1;var b=group.Player2;
        var retentionType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDisconnectRetention");
        var owner=Activator.CreateInstance(retentionType,Instance,null,new object[]{1,null},null);
        Call(typeof(TeamQualifier),null,"ImportMatched",group,owner);
        try {
            a.Entity.PKMode=b.Entity.PKMode=Revora.Game.Enums.PKMode.PK;a.Attackable=b.Attackable=true;
            b.Entity.Hitpoints=1000;var now=group.ImportTime;clock.SetValue(null,new Func<Time32>(()=>now));
            Func<uint> hit=()=>{uint damage=50;Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,
                new Revora.Network.GamePackets.Attack(true){AttackType=Revora.Network.GamePackets.Attack.Melee,Attacker=a.Entity.UID,Attacked=b.Entity.UID},ref damage,null);return damage;};
            Check(hit()==0 && b.Entity.Hitpoints==1000 && group.Player1Damage==0,
                "direct damage receiver cannot bypass Team Arena preparation");
            now=new Time32(unchecked(group.ImportTime.Value+10000));
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(hit()==0 && b.Entity.Hitpoints==1000 && group.Player1Damage==0,
                    "busy target ownership rejects direct damage without health or score mutation");}
                finally{release.Set();task.Wait();}
            }
            uint applied=hit();
            Check(applied>0 && b.Entity.Hitpoints==1000-applied && group.Player1Damage==applied,
                "owned direct hit applies health and admitted-side score together after preparation");
            now=new Time32(unchecked(group.ImportTime.Value+300000));uint health=b.Entity.Hitpoints,score=group.Player1Damage;
            Check(hit()==0 && b.Entity.Hitpoints==health && group.Player1Damage==score,
                "delayed direct hit after Arena deadline cannot change health or score");
            now=new Time32(unchecked(group.ImportTime.Value+10000));b.Entity.Hitpoints=1;hit();
            Check(group.Done && ReferenceEquals(group.Loser,b) && (int)Field(group,"DurableResultReady")==1
                && TeamFixtureOwners(group).All(p=>Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==1),
                "lethal receiver callback commits one complete roster result under the combat lease");
            health=b.Entity.Hitpoints;score=group.Player1Damage;
            Check(hit()==0 && b.Entity.Hitpoints==health && group.Player1Damage==score,
                "direct damage receiver rejects a frozen result before cleanup");
            RunOwnedArenaScheduler(true);
            Check(TeamFixtureOwners(group).All(p=>p.TeamQualifierGroup==null && p.Entity.MapID==1002),
                "lethal combat releases its gates for normal owned scheduler cleanup");
        }finally{clock.SetValue(null,prior);CleanupTeamFixture(group);}
    }

    static void VerifyTeamPoisonOwnership(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaPoison");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=coordinator.GetField("MatchClock",Hidden);var prior=clock.GetValue(null);
        var group=TeamCoordinatorGroup(town,false);var target=group.Player2;
        var caster=TeamFixtureOwners(group).First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,target));
        var spell=new SpellInformation{ID=6001,PowerPercent=0.1f,Duration=40};
        var now=new Time32(unchecked(group.ImportTime.Value+10000));clock.SetValue(null,new Func<Time32>(()=>now));
        Func<bool> apply=()=>(bool)Call(type,null,"Apply",caster.Entity,target.Entity,spell);
        Action tick=()=>Call(type,null,"Tick",target,now);
        try {
            target.Entity.Hitpoints=1000;
            Check(apply() && target.Entity.ToxicFogLeft==20,"Arena Toxic Fog application records an admitted nonleader caster");
            now=new Time32(unchecked(now.Value+1999));tick();
            Check(target.Entity.Hitpoints==1000 && target.Entity.ToxicFogLeft==20,"owned poison waits for its two-second tick");
            now=new Time32(unchecked(now.Value+1));tick();tick();
            Check(target.Entity.Hitpoints==900 && group.Player1Damage==100 && group.Player2Damage==0 && target.Entity.ToxicFogLeft==19,
                "poison credits actual HP removed to the original caster side once per tick");
            target.Entity.AddFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.AzureShield,40,true);
            target.Entity.AzureShieldDefence=200;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==900 && target.Entity.AzureShieldDefence==110 && group.Player1Damage==100,
                "absorbed poison consumes shield without granting phantom Arena damage");
            target.Entity.AzureShieldDefence=20;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==830 && target.Entity.AzureShieldDefence==0 && group.Player1Damage==170,
                "partially absorbed poison credits only actual remaining HP damage");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(caster.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                now=new Time32(unchecked(now.Value+2000));byte remaining=target.Entity.ToxicFogLeft;
                try{tick();Check(target.Entity.Hitpoints==830 && target.Entity.ToxicFogLeft==remaining,
                    "busy poison caster defers tick without consuming duration or changing health");}
                finally{release.Set();task.Wait();}
            }
            tick();Check(target.Entity.Hitpoints==747 && group.Player1Damage==253,"deferred poison retries once with the same original caster");
            caster.TeamQualifierGroup=null;tick();
            Check(target.Entity.ToxicFogLeft==0 && target.Entity.Hitpoints==747 && Field(target.Entity,"ArenaPoison")==null,
                "poison cannot follow its caster out of the original match");caster.TeamQualifierGroup=group;
            target.Entity.ToxicFogLeft=10;target.Entity.ToxicFogPercent=0.5f;tick();
            Check(target.Entity.ToxicFogLeft==0 && target.Entity.Hitpoints==747,
                "unowned carried-in poison cannot earn Team Arena damage");
            Check(apply(),"new valid poison can replace a cleared effect");
            now=new Time32(unchecked(group.ImportTime.Value+300000));tick();
            Check(target.Entity.ToxicFogLeft==0 && target.Entity.Hitpoints==747,"Arena deadline cancels pending poison before health mutation");
        }finally{caster.TeamQualifierGroup=group;clock.SetValue(null,prior);CleanupTeamFixture(group);}
    }

    static void VerifyTeamBreakingTouchOwnership(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaBreakingTouch");
        var flame=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaFlame");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=coordinator.GetField("MatchClock",Hidden);var prior=clock.GetValue(null);
        var group=TeamCoordinatorGroup(town,false);var target=group.Player2;
        var caster=TeamFixtureOwners(group).First(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,target));
        var now=new Time32(unchecked(group.ImportTime.Value+10000));clock.SetValue(null,new Func<Time32>(()=>now));
        Func<bool> apply=()=>{
            target.Entity.AddFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.lianhuaran,20,true);
            bool accepted=(bool)Call(type,null,"Record",caster.Entity,target.Entity);
            if(accepted){target.Entity.lianhuaranLeft=20;target.Entity.lianhuaranPercent=0.1f;}
            return accepted;
        };
        Action tick=()=>Call(type,null,"Tick",target,now);
        try {
            target.Entity.Hitpoints=1000;
            target.Entity.FlameOwner=caster;typeof(Entity).GetField("FlameMatch",Instance).SetValue(target.Entity,group);target.Entity.FlameTime=60;
            Check(apply() && target.Entity.lianhuaranLeft==20,"Arena Breaking Touch application records an admitted nonleader caster");
            Call(flame,null,"Expire",target,now);
            Check(target.Entity.FlameOwner==null && Field(target.Entity,"FlameMatch")==null
                && target.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran) && target.Entity.lianhuaranLeft==20,
                "Breaking Touch replaces old flame ownership and survives the character callback's unrelated expiry timer");
            target.Entity.AddFlag((ulong)FlagsUpdate.Flags.lianhuaran04,20,true);
            Call(flame,null,"Pulse",target,target,now);
            Check(target.Entity.Hitpoints==1000 && group.Player1Damage==0,
                "shared fourth-stack flags cannot make Breaking Touch emit a stale Searing Touch pulse");
            target.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
            now=new Time32(unchecked(now.Value+1999));tick();
            Check(target.Entity.Hitpoints==1000 && target.Entity.lianhuaranLeft==20,"owned Breaking Touch waits for its two-second tick");
            now=new Time32(unchecked(now.Value+1));tick();tick();
            Check(target.Entity.Hitpoints==900 && group.Player1Damage==100 && group.Player2Damage==0 && target.Entity.lianhuaranLeft==19,
                "Breaking Touch credits actual HP removed to the original caster side once per tick");
            target.Entity.AddFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.AzureShield,40,true);
            target.Entity.AzureShieldDefence=200;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==900 && target.Entity.AzureShieldDefence==110 && group.Player1Damage==100,
                "absorbed Breaking Touch consumes shield without granting phantom Arena damage");
            target.Entity.AzureShieldDefence=20;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==830 && target.Entity.AzureShieldDefence==0 && group.Player1Damage==170,
                "partially absorbed Breaking Touch credits only actual remaining HP damage");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(caster.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                now=new Time32(unchecked(now.Value+2000));var remaining=target.Entity.lianhuaranLeft;
                try{tick();Check(target.Entity.Hitpoints==830 && target.Entity.lianhuaranLeft==remaining,
                    "busy Breaking Touch caster defers tick without consuming duration or changing health");}
                finally{release.Set();task.Wait();}
            }
            tick();Check(target.Entity.Hitpoints==747 && group.Player1Damage==253,"deferred Breaking Touch retries once with the same original caster");
            caster.TeamQualifierGroup=null;tick();
            Check(target.Entity.lianhuaranLeft==0 && target.Entity.Hitpoints==747 && Field(target.Entity,"ArenaBreakingTouch")==null,
                "Breaking Touch cannot follow its caster out of the original match");caster.TeamQualifierGroup=group;
            target.Entity.lianhuaranLeft=10;target.Entity.lianhuaranPercent=0.5f;tick();
            Check(target.Entity.lianhuaranLeft==0 && target.Entity.Hitpoints==747,
                "unowned carried-in Breaking Touch cannot earn Team Arena damage");
            Check(apply(),"new valid Breaking Touch can replace a cleared effect");
            target.Entity.RemoveFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.AzureShield);
            target.Entity.Detoxication=100;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==747 && group.Player1Damage==253,"full detoxification prevents Breaking Touch damage and score");
            target.Entity.Detoxication=0;target.Entity.Hitpoints=1;now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==1 && group.Player1Damage==253,"Breaking Touch cannot kill or score against a one-HP target");
            target.Entity.Hitpoints=747;
            target.Entity.RemoveFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.lianhuaran);
            now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==747 && target.Entity.lianhuaranLeft==0 && group.Player1Damage==253,
                "removed Breaking Touch stack cancels its percentage timer without ghost damage");
            target.Entity.lianhuaranLeft=20;target.Entity.lianhuaranPercent=0.5f;
            Check((bool)Call(type,null,"Record",caster.Entity,target.Entity) && target.Entity.lianhuaranLeft==0
                && Field(target.Entity,"ArenaBreakingTouch")==null,
                "post-burst recorder cannot restart damage using a removed stack percentage");
            Check(apply(),"new stack can apply after prior stack cancellation");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(caster.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(!(bool)Call(flame,null,"Apply",caster.Entity,target.Entity,now,20)
                    && target.Entity.lianhuaranLeft==20 && Field(target.Entity,"ArenaBreakingTouch")!=null
                    && target.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran),
                    "busy Searing Touch admission preserves the prior stack and percentage owner without partial flag removal");}
                finally{release.Set();task.Wait();}
            }
            Check((bool)Call(flame,null,"Apply",caster.Entity,target.Entity,now,20)
                && target.Entity.lianhuaranLeft==0 && Field(target.Entity,"ArenaBreakingTouch")==null
                && ReferenceEquals(target.Entity.FlameOwner,caster)
                && target.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran02),
                "Searing Touch applies its existing next stack while replacing the percentage timer");
            now=new Time32(unchecked(now.Value+2000));tick();
            Check(target.Entity.Hitpoints==747 && target.Entity.lianhuaranLeft==0,
                "replaced Breaking Touch cannot apply percentage damage through Searing Touch flags");
            Call(flame,null,"Expire",target,new Time32(unchecked(target.Entity.FlameStamp.Value+60001)));
            Check(target.Entity.FlameOwner==null && Field(target.Entity,"FlameMatch")==null
                && !target.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran02),
                "expired Searing Touch clears its stack and stale caster ownership");
            Check(apply(),"Breaking Touch can own a new stack after Searing Touch expires");
            now=new Time32(unchecked(group.ImportTime.Value+300000));tick();
            Check(target.Entity.lianhuaranLeft==0 && target.Entity.Hitpoints==747,"Arena deadline cancels pending Breaking Touch before health mutation");
        }finally{caster.TeamQualifierGroup=group;clock.SetValue(null,prior);CleanupTeamFixture(group);}
    }

    static void VerifyTeamFlameOwnership(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaFlame");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var clock=coordinator.GetField("MatchClock",Hidden);var prior=clock.GetValue(null);
        var group=TeamCoordinatorGroup(town,false,false);var caster=group.Player1;var carrier=group.Player2;
        var target=PointBuyer(town);target.Entity.Level=100;target.ArenaPoints=1000;target.TeamArenaStatistic.Name=target.Entity.Name;
        ArenaTable.SaveArenaStatistics(target.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(target.TeamArenaStatistic);
        carrier.Team.Add(target);group.Import();
        var now=new Time32(unchecked(group.ImportTime.Value+10000));clock.SetValue(null,new Func<Time32>(()=>now));
        Action pulse=()=>Call(type,null,"Pulse",carrier,target,now);
        try {
            Check(TeamFixtureOwners(group).Contains(target) && (bool)group.GetType().GetProperty("ResultAdmissionReady",Instance).GetValue(group,null),
                "flame fixture target completes admission with the original eligible roster");
            carrier.Entity.X=target.Entity.X=50;carrier.Entity.Y=target.Entity.Y=50;
            caster.Entity.PKMode=Revora.Game.Enums.PKMode.PK;target.Attackable=true;
            carrier.Entity.FlameOwner=caster;typeof(Entity).GetField("FlameMatch",Instance).SetValue(carrier.Entity,group);
            carrier.Entity.FlameStamp=now;carrier.Entity.FlameTime=60;
            carrier.Entity.AddFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.lianhuaran04,60,true);
            target.Entity.Hitpoints=3000;pulse();
            Check(target.Entity.Hitpoints==1000 && group.Player1Damage==2000,"Arena Searing Touch pulse owns HP and credits its original caster side");
            pulse();Check(target.Entity.Hitpoints==1 && group.Player1Damage==2999,"searing pulse stays nonlethal and scores only removed HP");
            pulse();Check(target.Entity.Hitpoints==1 && group.Player1Damage==2999,"one-HP target cannot generate phantom searing score");
            target.Entity.Hitpoints=3000;
            target.Attackable=false;pulse();Check(target.Entity.Hitpoints==3000,"Arena flame preserves ordinary target attackability protection");target.Attackable=true;
            var originalTeam=caster.Team;caster.Team=null;
            try{Check(Revora.Game.Attacking.Handle.CanAttack(caster.Entity,target.Entity,null,false),
                "admitted opponent permission does not dereference departed ordinary team membership");}
            finally{caster.Team=originalTeam;}
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(carrier.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{pulse();Check(target.Entity.Hitpoints==3000 && group.Player1Damage==2999,"busy flame carrier declines damage without partial mutation");}
                finally{release.Set();task.Wait();}
            }
            target.Entity.X=70;pulse();Check(target.Entity.Hitpoints==3000,"flame pulse rechecks its carrier radius under ownership");target.Entity.X=50;
            carrier.TeamQualifierGroup=null;pulse();Check(target.Entity.Hitpoints==3000,"returned carrier cannot continue its former Arena pulse");carrier.TeamQualifierGroup=group;
            carrier.Entity.FlameOwner=null;pulse();Check(target.Entity.Hitpoints==3000,"missing flame caster cannot fall through to legacy Arena damage");carrier.Entity.FlameOwner=caster;
            now=new Time32(unchecked(carrier.Entity.FlameStamp.Value+60000));pulse();
            Check(target.Entity.Hitpoints==3000,"expired Searing Touch pulse cannot change Arena health");
        }finally{carrier.TeamQualifierGroup=group;clock.SetValue(null,prior);CleanupTeamFixture(group);}
    }

    static void VerifyOwnedArenaCallbacks(Map town) {
        foreach(bool team in new[]{false,true})foreach(string callback in new[]{"DoQuit","DoGiveUp","Win","Combat"}) {
            object group;GameState a,b;
            if(team){var g=TeamCoordinatorGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;}
            else{var g=StatGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;a.ArenaStatistic.PlayWith=b;b.ArenaStatistic.PlayWith=a;}
            var ownerType=typeof(Entity).Assembly.GetType("Revora.Game.Features."+(team?"TeamArenaDisconnectRetention":"ArenaDisconnectRetention"));
            var owner=Activator.CreateInstance(ownerType,Instance,null,new object[]{1,null},null);
            Call(team?typeof(TeamQualifier):typeof(Qualifier),null,"ImportMatched",group,owner);
            if(team && (callback=="Combat" || callback=="DoQuit")) {
                var members=(GameState[])Call(group.GetType(),group,"Participants",true);
                var member=members.First(p=>!ReferenceEquals(p,a));
                if(callback=="Combat"){member.Entity.Hitpoints=0;Call(typeof(GameState),member,"EndQualifier");}
                else ((TeamQualifier.QualifierList.QualifierGroup)group).CheckEnd(member,true);
                Check(Field(group,"PendingDeparture")==null && a.ArenaPoints==1000
                    && (callback=="Combat" || ((System.Collections.Generic.HashSet<GameState>)Field(group,"departedParticipants")).Contains(member)),
                    "owned team callback preserves a fighting leader after member death or disconnect: "+callback);
            }
            Action<GameState,GameState> invoke=(loser,winner)=>{
                if(callback=="Combat") {
                    loser.Entity.Hitpoints=0;
                    if(team)foreach(var member in (GameState[])Call(group.GetType(),group,"Participants",ReferenceEquals(loser,a)))member.Entity.Hitpoints=0;
                    Call(typeof(GameState),loser,"EndQualifier");return;
                }
                if(team){if(callback=="Win")TeamQualifier.Win(winner,loser);else if(callback=="DoQuit")TeamQualifier.QualifyEngine.DoQuit(loser);else TeamQualifier.QualifyEngine.DoGiveUp(loser);}
                else{if(callback=="Win")Qualifier.Win(winner,loser);else if(callback=="DoQuit")Qualifier.QualifyEngine.DoQuit(loser);else Qualifier.QualifyEngine.DoGiveUp(loser);}
            };
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    invoke(a,b);
                    Check(Field(group,"PendingDeparture")!=null && a.ArenaPoints==1000 && b.ArenaPoints==1000
                        && ReferenceEquals(team?a.TeamArenaStatistic.PlayWith:a.ArenaStatistic.PlayWith,b)
                        && ReferenceEquals(team?b.TeamArenaStatistic.PlayWith:b.ArenaStatistic.PlayWith,a),
                        "owned public callback retains first result and invitation while busy: "+team+"/"+callback);
                }finally{release.Set();task.Wait();}
            }
            invoke(b,a);invoke(a,b);RunOwnedArenaScheduler(team);
            Check(team?Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",a.Entity.UID)==1:
                Scalar(callback=="Win" || callback=="Combat"?"SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='Defeat'":
                    "SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='Forfeit'",a.Entity.UID)==1,
                "public callback retries preserve original loser and commit once: "+team+"/"+callback);
            Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && (team?a.TeamQualifierGroup==null:a.QualifierGroup==null),
                "public callback result drains through owned scheduler cleanup: "+team+"/"+callback);
        }
    }

    static void VerifyOwnedSoloDialogs(Map town) {
        var ownerType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaDisconnectRetention");
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        foreach(bool fail in new[]{false,true}) {
            var group=StatGroup(town,false,false);
            Action<GameState,bool> resume=(p,requested)=>{if((bool)Call(typeof(GameState),p,"TryBeginShutdown"))Call(typeof(GameState),p,"ReleaseSessionRegistration",true);};
            var owner=Activator.CreateInstance(ownerType,Instance,null,new object[]{1,resume},null);
            Call(typeof(Qualifier),null,"ImportMatched",group,owner);group.End(group.Player2);
            var notified=(System.Collections.Generic.HashSet<GameState>)Field(group,"outcomeNotified");
            lock(Field(group,"ParticipationSync"))Call(group.GetType(),group,"NotifyCommittedOutcome");
            Check(notified.Count==0 && !(bool)Field(group,"ResultNotificationReady"),"owned combat ending cannot authorize solo result dialogs before commit");
            if(fail)Query("ALTER TABLE revora_arena_result_statistics ADD CONSTRAINT solo_dialog_failure CHECK(won) NOT VALID");
            string outcome;
            try{outcome=Call(coordinator,null,"Complete",group).ToString();}
            finally{if(fail)Query("ALTER TABLE revora_arena_result_statistics DROP CONSTRAINT solo_dialog_failure");}
            if(fail) {
                Check(outcome=="Failed" && notified.Count==0 && !(bool)Field(group,"ResultNotificationReady")
                    && LedgerRows(group,"active")==1,"rolled-back solo bundle does not authorize or attempt result dialogs");
                RunOwnedArenaScheduler(false);AdvanceOwnedPump(owner);RunOwnedArenaScheduler(false);
                Check(LedgerRows(group,"finished")==1 && notified.Count==0 && (bool)Field(group,"CleanupCompleted"),
                    "fenced recovery settles failed combat without sending stale solo result dialogs");
            }else {
                Check(outcome=="Committed" && notified.SetEquals(new[]{group.Winner,group.Loser})
                    && (bool)Field(group,"ResultNotificationReady"),"saved and projected solo combat attempts both original result dialogs");
                Check(Call(coordinator,null,"Complete",group).ToString()=="AlreadyCommitted" && notified.Count==2,
                    "repeated solo completion cannot attempt duplicate result dialogs");
                RunOwnedArenaScheduler(false);
            }
        }
    }

    static void VerifyTeamSpectatorOwnership(Map town) {
        var group=TeamCoordinatorGroup(town,false);var spectator=PointBuyer(town);
        var path=DMaps.MapPaths[1005];DMaps.MapPaths.Remove(1005);
        try{group.BeginWatching(spectator);Check(spectator.TeamWatchingGroup==null && group.Watchers.Count==0,
            "failed team spectator transfer leaves no phantom membership");}finally{DMaps.MapPaths[1005]=path;}
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(spectator.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{group.BeginWatching(spectator);Check(spectator.TeamWatchingGroup==null,"busy team spectator cannot enter without its actor lock");}
            finally{release.Set();task.Wait();}
        }
        Parallel.For(0,10,n=>group.BeginWatching(spectator));
        Check(group.Watchers.Count==1 && ReferenceEquals(spectator.TeamWatchingGroup,group)
            && ReferenceEquals(spectator.Map,group.Player1.Map),"concurrent team spectator joins acquire one actual map membership");
        TeamQualifier.QualifyEngine.DoCheer(spectator,uint.MaxValue);
        Check(group.Cheerers.Count==0 && group.Player2Cheers==0,"unknown team fighter cannot consume a cheer");
        Parallel.For(0,10,n=>TeamQualifier.QualifyEngine.DoCheer(spectator,group.Player1.Entity.UID));
        Check(group.Player1Cheers==1 && group.Cheerers.Count==1,"concurrent team cheers count once for the selected fighter");
        var replacement=PointBuyer(town);Kernel.GamePool[spectator.Entity.UID]=replacement;
        try{TeamQualifier.QualifyEngine.DoLeave(spectator);Check(group.Watchers.Contains(spectator)
            && ReferenceEquals(Kernel.GamePool[spectator.Entity.UID],replacement),"late team spectator leave preserves replacement ownership");}
        finally{Kernel.GamePool[spectator.Entity.UID]=spectator;}
        Parallel.For(0,10,n=>TeamQualifier.QualifyEngine.DoLeave(spectator));
        Check(group.Watchers.Count==0 && spectator.TeamWatchingGroup==null && spectator.Entity.MapID==town.ID,
            "concurrent team spectator leave returns the original actor and removes membership once");
        group.BeginWatching(spectator);group.End(group.Player2);var late=PointBuyer(town);group.BeginWatching(late);
        TeamQualifier.QualifyEngine.DoCheer(spectator,group.Player2.Entity.UID);
        Check(late.TeamWatchingGroup==null && group.Player2Cheers==0,"ended team match rejects late joins and cheers");
        TeamQualifier.QualifyEngine.DoLeave(spectator);CleanupTeamFixture(group);
        var first=TeamCoordinatorGroup(town,false);var second=TeamCoordinatorGroup(town,false);var shared=PointBuyer(town);
        Parallel.Invoke(()=>first.BeginWatching(shared),()=>second.BeginWatching(shared));
        Check(first.Watchers.Count+second.Watchers.Count==1 && shared.TeamWatchingGroup!=null,
            "competing team matches cannot both acquire one spectator");
        TeamQualifier.QualifyEngine.DoLeave(shared);CleanupTeamFixture(first);CleanupTeamFixture(second);
    }

    static void VerifyTeamDailyMilestones(Map town) {
        var rewards=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDailyRewards");
        var group=TeamCoordinatorGroup(town,false,false);var a=group.Player1;var b=group.Player2;
        var member=a.Team.Teammates.First(p=>!ReferenceEquals(p,a));
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);var now=new DateTime(2060,5,1,20,0,0,DateTimeKind.Utc);
        // Start all test results on one explicit Pacific date, including the first group.
        CleanupTeamFixture(group);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            for(int match=1;match<=20;match++) {
                group=new TeamQualifier.QualifierList.QualifierGroup(a,b);group.Import();group.End(b);
                if(match==9) {
                    Query("CREATE FUNCTION suppress_team_daily() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_team_daily BEFORE INSERT ON revora_team_arena_daily_rewards FOR EACH ROW EXECUTE FUNCTION suppress_team_daily()");
                    bool rejected=false;
                    try{CommitTeamResult(group);}catch(InvalidOperationException){rejected=true;}
                    finally{Query("DROP TRIGGER suppress_team_daily ON revora_team_arena_daily_rewards;DROP FUNCTION suppress_team_daily()");}
                    Check(rejected && a.TeamArenaStatistic.TodayWin==8 && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==0,
                        "suppressed team daily receipt rolls back the ninth result and statistic update");
                }
                if(match==20) {
                    Check(CommitTeamResult(group,true)==3 && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",b.Entity.UID)==0,
                        "rolled-back twentieth team result retains its capture without granting a daily item");
                    var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaStartupRecovery");var run=(Guid)Field(group,"ResultKey");
                    ColdTeamRecovery(()=>{
                        Check((bool)Call(recovery,null,"Recover",run),"cold recovery reconstructs the twentieth team result and daily entitlement together");
                        Call(recovery,null,"Recover",run);
                    });
                    // Reload the fixture actors from durable statistics, as a new session would.
                    foreach(var p in new[]{a,b,member}) {
                        p.ArenaStatistic=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
                        p.TeamArenaStatistic=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
                    }
                    a.TeamArenaStatistic.PlayWith=b;b.TeamArenaStatistic.PlayWith=a;
                }else Check(CommitTeamResult(group)==3,"ordinary team result records its complete daily roster: "+match);
                if(match==8)Check(Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==0,"eight team wins cannot grant nine-win reward");
                if(match==9)Check(Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",member.Entity.UID)==1,"ninth team win grants its ordinary admitted member a daily entitlement");
                if(match==19)Check(Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",b.Entity.UID)==0,"nineteen team losses cannot grant twenty-match reward");
                CleanupTeamFixture(group);
            }
            Check(Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==2
                && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",member.Entity.UID)==2
                && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",b.Entity.UID)==1,
                "team milestones grant winners and admitted members twice and losing participant once per day");
            while(a.Inventory.Count<40)Pack(a,1088001,1);Call(rewards,null,"Pay",a);
            Check(a.CurrentHonor==0 && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",a.Entity.UID)==2,
                "full team reward inventory retains Honor and item together");
            Check(HonorEvidence(a)==0 && HonorEvidence(b)==0 && HonorEvidence(member)==0,"unpaid team milestones grant no first-Honor achievement");
            foreach(var item in a.Inventory.Objects.ToArray())DisRemove(a,item);
            while(a.Inventory.Count<39)Pack(a,1088001,1);Call(rewards,null,"Pay",a);
            Check(a.CurrentHonor==0 && Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL",a.Entity.UID)==2,
                "one free slot preserves the complete Team Arena milestone bundle");
            foreach(var filler in a.Inventory.Objects.ToArray())DisRemove(a,filler);
            Query("UPDATE revora_team_arena_daily_rewards SET rune_type=0 WHERE uid=@uid","uid",(long)b.Entity.UID);
            Parallel.For(0,8,n=>Call(rewards,null,"Pay",a));Call(rewards,null,"Pay",member);Call(rewards,null,"Pay",member);Call(rewards,null,"Pay",b);
            Check(a.CurrentHonor==1000 && member.CurrentHonor==1000 && b.CurrentHonor==500
                && a.Inventory.Objects.Count(i=>i.ID==723700)==2 && b.Inventory.Objects.Count(i=>i.ID==723700)==1,
                "concurrent team daily payment delivers exactly the earned Honor and EXP Balls");
            Check(a.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==2 && member.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==2
                && b.Inventory.Objects.All(i=>i.ID!=4030101),"earned team milestones grant bound combat runes while older entitlements remain unchanged");
            foreach(var p in new[]{a,b,member}) {
                VerifyHonorAchievementReload(p);
                var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
                Check(saved.CurrentHonor==p.CurrentHonor && saved.HistoryHonor==p.HistoryHonor
                    && p.Inventory.Objects.All(i=>ConquerItemTable.LoadItem(i.UID).ID==i.ID && ConquerItemTable.LoadItem(i.UID).Bound==i.Bound),"team daily rewards survive ordinary statistic and item loading");
            }
            now=now.AddDays(1);group=new TeamQualifier.QualifierList.QualifierGroup(a,b);group.Import();group.End(b);CommitTeamResult(group);CleanupTeamFixture(group);
            Check(a.TeamArenaStatistic.TodayWin==1 && a.TeamArenaStatistic.TodayBattles==1
                && member.TeamArenaStatistic.TodayWin==1 && b.TeamArenaStatistic.TodayBattles==1,
                "new team result projects its own saved day's counters before any idle rollover");
            Check(Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",a.Entity.UID)==2,
                "a new Pacific day does not inherit yesterday's team milestone counts");
            a.TeamArenaStatistic.PlayWith=null;var points=a.ArenaPoints;var lifetime=a.TeamArenaStatistic.TotalWin;
            var rollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDayRollover");
            Check((bool)Call(rollover,null,"Prepare",a) && a.TeamArenaStatistic.TodayWin==1 && a.TeamArenaStatistic.TodayBattles==1
                && a.ArenaPoints==points && a.TeamArenaStatistic.TotalWin==lifetime,
                "team counter rollover retains today's saved result without resetting shared points or lifetime wins");
        }finally{clock.SetValue(null,prior);}
    }

    static void VerifyWithdrawalAdmissionRaces(Map town) {
        foreach(bool team in new[]{false,true})foreach(bool late in new[]{false,true})foreach(bool fault in !team && late?new[]{false,true}:new[]{false}) {
            object group;GameState a,b;GameState[] players;
            if(team){var g=TeamCoordinatorGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;players=a.Team.Teammates.Concat(b.Team.Teammates).ToArray();}
            else{var g=StatGroup(town,false,false);group=g;a=g.Player1;b=g.Player2;players=new[]{a,b};}
            var qualifier=team?typeof(TeamQualifier):typeof(Qualifier);
            var service=qualifier.GetField("SignupWithdrawals",Hidden).GetValue(null);
            var ownerType=typeof(Entity).Assembly.GetType("Revora.Game.Features."+(team?"TeamArenaDisconnectRetention":"ArenaDisconnectRetention"));
            var owner=Activator.CreateInstance(ownerType,Instance,null,new object[]{1,null},null);
            Task work=null;
            if(fault)Query("CREATE FUNCTION fail_withdrawal_entry() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''fixture entry failure''; END';CREATE TRIGGER fail_withdrawal_entry BEFORE INSERT ON revora_arena_participation FOR EACH ROW EXECUTE FUNCTION fail_withdrawal_entry()");
            try {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string sql=late?(team?"LOCK TABLE revora_team_arena_admissions IN SHARE MODE":"LOCK TABLE revora_arena_participation IN ACCESS EXCLUSIVE MODE")
                    :"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE";
                using(var q=new Npgsql.NpgsqlCommand(sql,c,t)){if(!late)q.Parameters.AddWithValue("uid",(long)a.Entity.UID);q.ExecuteNonQuery();}
                try {
                    work=Task.Run(()=>Call(qualifier,null,"ImportMatched",group,owner));
                    bool reached=System.Threading.SpinWait.SpinUntil(()=>{
                        if(late)using(var probe=new Npgsql.NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE relation=to_regclass(@relation) AND mode='RowExclusiveLock' AND NOT granted)",c,t)) {
                            probe.Parameters.AddWithValue("relation",team?"revora_potency_validation.revora_team_arena_admissions":"revora_potency_validation.revora_arena_participation");
                            return Equals(probe.ExecuteScalar(),true);
                        }
                        if(System.Threading.Monitor.TryEnter(a.ItemSyncRoot)){System.Threading.Monitor.Exit(a.ItemSyncRoot);return false;}
                        return true;
                    },TimeSpan.FromSeconds(10));
                    Check(reached && !work.IsCompleted,"admission reaches its controlled database checkpoint before withdrawal: "+team+"/"+late);
                    Call(service.GetType(),service,"Request",a);
                    Check((bool)Call(service.GetType(),service,"Pending",a),"signup withdrawal remains observable while import owns actor locks: "+team+"/"+late);
                }finally{t.Rollback();if(work!=null && !work.Wait(TimeSpan.FromSeconds(10)))throw new Exception("Admission did not finish after releasing fixture lock.");}
            }}
            }finally{if(fault)Query("DROP TRIGGER fail_withdrawal_entry ON revora_arena_participation;DROP FUNCTION fail_withdrawal_entry()");}
            if(fault) {
                Check((bool)Field(group,"EntryRecoveryRequired") && (bool)group.GetType().GetProperty("OwnsImportedMap",Instance).GetValue(group,null)
                    && players.All(Fenced),"real entry write failure concurrent with withdrawal preserves fenced map ownership for recovery");
                Check(Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator"),null,"RecoverEntry",group).ToString()=="Committed"
                    && (bool)Field(group,"DurableAdmissionCancelled"),"failed entry and pending withdrawal recover one cancelled reservation without a fight result");
                foreach(var p in players)Call(typeof(GameState),p,"ReleaseSessionRegistration",true);
                RunOwnedArenaScheduler(false);RunOwnedArenaScheduler(false);
                Check((bool)Field(group,"CleanupCompleted") && !(bool)Call(service.GetType(),service,"Pending",a)
                    && players.All(p=>Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1000
                        && Scalar("SELECT count(*) FROM revora_arena_participation WHERE uid=@uid",p.Entity.UID)==0),
                    "fenced original session release drains entry cleanup and withdrawal without earning or losing points");
                continue;
            }
            RunOwnedArenaScheduler(team);RunOwnedArenaScheduler(team);
            Check(!(bool)Call(service.GetType(),service,"Pending",a)
                && players.All(p=>!Fenced(p) && p.Entity.MapID==1002 && p.QualifierGroup==null && p.TeamQualifierGroup==null && p.ArenaPoints==1000
                    && Scalar("SELECT count(*) FROM revora_arena_participation WHERE uid=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==0),
                "withdrawal interrupts admission without fencing healthy players or awarding an unstarted fight: "+team+"/"+late);
            if(!team && late)Check(Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NOT NULL",a.Entity.UID)==1
                && (bool)Field(group,"DurableAdmissionCancelled") && (bool)Field(group,"CleanupCompleted"),
                "withdrawal during solo participation commit rolls back earning evidence and durably cancels the transferred reservation");
            else Check((team?Scalar("SELECT count(*) FROM revora_team_arena_admission_players WHERE uid=@uid",a.Entity.UID)
                    :Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",a.Entity.UID))==0,
                "withdrawal before reservation commit leaves no durable admission: "+team+"/"+late);
        }
    }

    static void VerifyArenaSignupWithdrawals(Map town) {
        foreach(bool team in new[]{false,true})foreach(bool giveUp in new[]{false,true}) {
            GameState a,b;
            if(team){var g=TeamCoordinatorGroup(town,false,false);a=g.Player1;b=g.Player2;TeamQualifier.QualifierList.Groups.Remove(g.ID);}
            else{var g=StatGroup(town,false,false);a=g.Player1;b=g.Player2;Qualifier.QualifierList.Groups.Remove(g.ID);}
            var service=(team?typeof(TeamQualifier):typeof(Qualifier)).GetField("SignupWithdrawals",Hidden).GetValue(null);
            var waiting=team?TeamQualifier.WaitingPlayerList:Qualifier.WaitingPlayerList;
            foreach(var p in new[]{a,b}) {
                p.Account=new AccountTable(null){EntityID=p.Entity.UID,State=AccountTable.AccountState.Player};
                if(team){p.TeamArenaStatistic.AcceptBox=p.TeamArenaStatistic.HasBox=true;}
                else{p.ArenaStatistic.PlayWith=ReferenceEquals(p,a)?b:a;p.ArenaStatistic.AcceptBox=true;p.ArenaState=Qualifier.WaitForOther;}
                waiting[p.Entity.UID]=p;
            }
            Action withdraw=()=>{if(team){if(giveUp)TeamQualifier.QualifyEngine.DoGiveUp(a);else TeamQualifier.QualifyEngine.DoQuit(a);}
                else{if(giveUp)Qualifier.QualifyEngine.DoGiveUp(a);else Qualifier.QualifyEngine.DoQuit(a);}};
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                // Exercise both a busy requester and a busy opposing actor.
                var locked=giveUp?b:a;var task=Task.Run(()=>{lock(locked.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    Parallel.For(0,8,n=>withdraw());
                    Check((bool)Call(service.GetType(),service,"Pending",a) && waiting.ContainsKey(a.Entity.UID)
                        && (team?ReferenceEquals(a.TeamArenaStatistic.PlayWith,b):ReferenceEquals(a.ArenaStatistic.PlayWith,b)),
                        "busy quit/refusal retains its original invitation and one withdrawal request: "+team+"/"+giveUp);
                    if(team){TeamQualifier.QualifyEngine.DoSignup(a);TeamQualifier.VerifyAwaitingPeople();}
                    else{Qualifier.QualifyEngine.DoSignup(a);Qualifier.VerifyAwaitingPeople();}
                    Check(a.QualifierGroup==null && a.TeamQualifierGroup==null && a.Entity.MapID==1002
                        && !(team?TeamQualifier.PlayerList:Qualifier.PlayerList).ContainsKey(a.Entity.UID),
                        "pending withdrawal blocks re-signup and accepted matchmaking before transfer: "+team+"/"+giveUp);
                }finally{release.Set();task.Wait();}
            }
            RunOwnedArenaScheduler(team);withdraw();RunOwnedArenaScheduler(team);
            Check(!(bool)Call(service.GetType(),service,"Pending",a) && !waiting.ContainsKey(a.Entity.UID) && !waiting.ContainsKey(b.Entity.UID)
                && (team?a.TeamArenaStatistic.PlayWith==null && b.TeamArenaStatistic.PlayWith==null:a.ArenaStatistic.PlayWith==null && b.ArenaStatistic.PlayWith==null)
                && a.ArenaPoints==1000 && b.ArenaPoints==1000 && a.ArenaStatistic.TodayWin==0 && a.TeamArenaStatistic.TodayWin==0,
                "scheduler completes queued quit/refusal without rewards or duplicate state changes: "+team+"/"+giveUp);
            var queue=team?TeamQualifier.PlayerList:Qualifier.PlayerList;queue[a.Entity.UID]=a;
            if(team)a.TeamArenaStatistic.Status=Revora.Network.GamePackets.TeamQualifierStatistic.WaitingForOpponent;
            else a.ArenaStatistic.Status=Revora.Network.GamePackets.QualifierStatistic.WaitingForOpponent;
            withdraw();
            Check(!queue.ContainsKey(a.Entity.UID) && !(bool)Call(service.GetType(),service,"Pending",a),
                "ordinary player can withdraw from an unpaired queue: "+team+"/"+giveUp);
            var next=PointBuyer(town);waiting[a.Entity.UID]=a;waiting[b.Entity.UID]=b;
            if(team){a.TeamArenaStatistic.PlayWith=b;b.TeamArenaStatistic.PlayWith=next;}
            else{a.ArenaStatistic.PlayWith=b;b.ArenaStatistic.PlayWith=next;}
            withdraw();
            Check(!(bool)Call(service.GetType(),service,"Pending",a) && !waiting.ContainsKey(a.Entity.UID) && waiting.ContainsKey(b.Entity.UID)
                && (team?a.TeamArenaStatistic.PlayWith==null && ReferenceEquals(b.TeamArenaStatistic.PlayWith,next)
                    :a.ArenaStatistic.PlayWith==null && ReferenceEquals(b.ArenaStatistic.PlayWith,next)),
                "withdrawal clears an orphaned invitation without cancelling the peer's newer opponent: "+team+"/"+giveUp);
            waiting.Remove(b.Entity.UID);if(team)b.TeamArenaStatistic.PlayWith=null;else b.ArenaStatistic.PlayWith=null;
            if(!giveUp) {
                var replacement=PointBuyer(town);uint replacementUid=replacement.Entity.UID;queue[a.Entity.UID]=a;
                using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                    var task=Task.Run(()=>{lock(a.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                    try {
                        withdraw();replacement.Entity.UID=a.Entity.UID;replacement.ArenaStatistic.EntityID=a.Entity.UID;replacement.TeamArenaStatistic.EntityID=a.Entity.UID;
                        Kernel.GamePool[a.Entity.UID]=replacement;queue[a.Entity.UID]=replacement;
                    }finally{release.Set();task.Wait();}
                }
                try {
                    RunOwnedArenaScheduler(team);GameState registered;
                    Check(!(bool)Call(service.GetType(),service,"Pending",a) && queue.TryGetValue(a.Entity.UID,out registered)
                        && ReferenceEquals(registered,replacement) && replacement.ArenaPoints==0,
                        "late withdrawal retires the old session without removing a replacement's queue registration: "+team);
                }finally {
                    queue.Remove(a.Entity.UID);Kernel.GamePool[a.Entity.UID]=a;replacement.Entity.UID=replacementUid;
                    replacement.ArenaStatistic.EntityID=replacementUid;replacement.TeamArenaStatistic.EntityID=replacementUid;
                }
            }
        }
    }

    static void VerifyUnstartedArenaResults(Map town) {
        foreach(bool team in new[]{false,true})foreach(bool busy in new[]{false,true}) {
            GameState a,b;object template;
            if(team){var g=TeamCoordinatorGroup(town,false,false);a=g.Player1;b=g.Player2;template=g;TeamQualifier.QualifierList.Groups.Remove(g.ID);}
            else{var g=StatGroup(town,false,false);a=g.Player1;b=g.Player2;template=g;Qualifier.QualifierList.Groups.Remove(g.ID);}
            var waiting=team?TeamQualifier.WaitingPlayerList:Qualifier.WaitingPlayerList;
            foreach(var p in new[]{a,b}) {
                p.Account=new AccountTable(null){EntityID=p.Entity.UID,State=AccountTable.AccountState.Player};
                if(team){p.TeamArenaStatistic.AcceptBox=false;p.TeamArenaStatistic.HasBox=true;p.TeamArenaStatistic.AcceptBoxShow=Time32.Now.AddMinutes(-2);}
                else{p.ArenaStatistic.PlayWith=ReferenceEquals(p,a)?b:a;p.ArenaStatistic.AcceptBox=false;p.ArenaStatistic.AcceptBoxShow=Time32.Now.AddMinutes(-2);p.ArenaState=Qualifier.WaitForBox;}
                waiting[p.Entity.UID]=p;
            }
            Func<int> pending=()=>team?TeamQualifier.QualifierList.Groups.Values.Count(g=>ReferenceEquals(g.Player1,a)||ReferenceEquals(g.Player2,a))
                :Qualifier.QualifierList.Groups.Values.Count(g=>ReferenceEquals(g.Player1,a)||ReferenceEquals(g.Player2,a));
            Action invoke=()=>{if(team)TeamQualifier.Win(a,b);else Qualifier.Win(a,b);};
            if(busy) {
                using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                    var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                    try{Parallel.For(0,12,n=>invoke());
                        Check(pending()==1 && a.ArenaPoints==1000 && b.ArenaPoints==1000 && waiting.ContainsKey(a.Entity.UID),
                            "concurrent unstarted result callbacks retain one busy cancellation without awarding points: "+team);
                    }finally{release.Set();task.Wait();}
                }
                RunOwnedArenaScheduler(team);
            }else {if(team)TeamQualifier.VerifyAwaitingPeople();else Qualifier.VerifyAwaitingPeople();}
            invoke();invoke();RunOwnedArenaScheduler(team);
            Check(pending()==0 && !waiting.ContainsKey(a.Entity.UID) && !waiting.ContainsKey(b.Entity.UID)
                && (team?a.TeamArenaStatistic.PlayWith==null && b.TeamArenaStatistic.PlayWith==null:a.ArenaStatistic.PlayWith==null && b.ArenaStatistic.PlayWith==null),
                "expired or deferred invitation cleans up both original registrations without a match: "+team+"/"+busy);
            Check(new[]{a,b}.All(p=>p.ArenaPoints==1000 && p.Entity.MapID==1002 && p.CurrentHonor==0
                && p.ArenaStatistic.TodayWin==0 && p.ArenaStatistic.TodayBattles==0
                && p.TeamArenaStatistic.TodayWin==0 && p.TeamArenaStatistic.TodayBattles==0
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1000
                && Scalar("SELECT count(*) FROM revora_arena_participation WHERE uid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid",p.Entity.UID)==0),
                "unstarted invitation cannot earn points, counters, participation or saved result rewards: "+team+"/"+busy);
        }
    }

    static void VerifyTeamAdmissionDays(Map town) {
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);
        var waiting=TeamQualifier.WaitingPlayerList;TeamQualifier.WaitingPlayerList=new System.Collections.Concurrent.ConcurrentDictionary<uint,GameState>();
        var now=new DateTime(2067,6,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            foreach(int failure in new[]{0,1,2}) {
                var template=TeamCoordinatorGroup(town,false,false);var a=template.Player1;var b=template.Player2;
                var member=a.Team.Teammates.First(p=>!ReferenceEquals(p,a));var players=new[]{a,b,member};
                TeamQualifier.QualifierList.Groups.Remove(template.ID);
                foreach(var p in players){p.TeamArenaStatistic.TodayWin=7;p.TeamArenaStatistic.TodayBattles=9;
                    Query("UPDATE teamarena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);}
                var excluded=PointBuyer(town);excluded.TeamArenaStatistic.TodayWin=7;excluded.TeamArenaStatistic.TodayBattles=9;
                TeamArenaTable.SaveArenaStatistics(excluded.TeamArenaStatistic);a.Team.Add(excluded);
                foreach(var p in new[]{a,b}){p.TeamArenaStatistic.AcceptBox=p.TeamArenaStatistic.HasBox=true;TeamQualifier.WaitingPlayerList[p.Entity.UID]=p;}
                now=now.AddDays(1);
                if(failure==1)member.TeamArenaStatistic.PlayWith=a;
                if(failure==2)Query(@"CREATE TABLE reject_admission_day(uid bigint PRIMARY KEY);
INSERT INTO reject_admission_day(uid) VALUES(@uid);
CREATE FUNCTION suppress_member_day() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF EXISTS(SELECT 1 FROM reject_admission_day WHERE uid=NEW.uid) THEN RETURN NULL; END IF; RETURN NEW; END';
CREATE TRIGGER suppress_member_day BEFORE INSERT ON revora_team_arena_day_rollovers FOR EACH ROW EXECUTE FUNCTION suppress_member_day()","uid",(long)member.Entity.UID);
                try{TeamQualifier.VerifyAwaitingPeople();}
                finally{if(failure==2)Query("DROP TRIGGER suppress_member_day ON revora_team_arena_day_rollovers;DROP FUNCTION suppress_member_day();DROP TABLE reject_admission_day");}
                if(failure==0) {
                    var group=a.TeamQualifierGroup;
                    Check(group!=null && TeamFixtureOwners(group).Length==3 && players.All(p=>ReferenceEquals(p.TeamQualifierGroup,group)
                        && p.TeamArenaStatistic.TodayWin==0 && p.TeamArenaStatistic.TodayBattles==0 && p.ArenaPoints==1000
                        && Scalar("SELECT count(*) FROM revora_team_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1),
                        "ordinary prepared team matchmaking resets the complete admitted roster before entry without changing points");
                    Check(excluded.TeamQualifierGroup==null && excluded.TeamArenaStatistic.TodayWin==7
                        && Scalar("SELECT count(*) FROM revora_team_arena_day_rollovers WHERE uid=@uid",excluded.Entity.UID)==0,
                        "team day preparation leaves excluded low-level members and their counters untouched");
                    group.End(b);RunOwnedArenaScheduler(true);
                    var loaded=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",member.Entity.UID);
                    Check(loaded.TodayWin==1 && loaded.TodayBattles==1 && loaded.TotalWin==1,
                        "ordinary admitted member reloads the new day's result after roster preparation");
                }else {
                    Check(players.All(p=>p.TeamQualifierGroup==null && p.Entity.MapID==1002)
                        && Scalar("SELECT count(*) FROM revora_team_arena_admission_players WHERE uid=@uid",member.Entity.UID)==0,
                        "unprepared member prevents every team reservation and map transfer: "+failure);
                    if(failure==1)Check(players.All(p=>p.TeamArenaStatistic.TodayWin==7
                        && Scalar("SELECT count(*) FROM revora_team_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==0)
                        && ReferenceEquals(member.TeamArenaStatistic.PlayWith,a),
                        "an unrelated member invitation prevents resets and remains owned by its existing interaction");
                    else Check(Fenced(member) && member.TeamArenaStatistic.TodayWin==7
                        && Scalar("SELECT todaywin FROM teamarena WHERE entityid=@uid",member.Entity.UID)==7
                        && a.TeamArenaStatistic.TodayWin==0 && b.TeamArenaStatistic.TodayWin==0,
                        "failed member receipt rolls back that reset while prior free leader resets remain saved without admitting a fight");
                    // This ordinary fixture has no network transport for Disconnect.
                    // Model its terminal session release, then let retention retire it.
                    if(failure==2)Call(typeof(GameState),member,"ReleaseSessionRegistration",true);
                    RunOwnedArenaScheduler(true);
                    Check(players.All(p=>Field(p,"ArenaShutdownRetention")==null),
                        "rejected team day preparation releases its retained original roster after session cleanup: "+failure);
                }
                TeamQualifier.WaitingPlayerList.Clear();now=now.AddDays(3);
            }
        }finally{TeamQualifier.WaitingPlayerList=waiting;clock.SetValue(null,prior);}
    }

    static void VerifyTeamDailyCounterRecovery(Map town) {
        var recovery=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaStartupRecovery");
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);
        var now=new DateTime(2065,6,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            foreach(bool legacy in new[]{false,true}) {
                var group=TeamCoordinatorGroup(town,false);var owners=TeamFixtureOwners(group);var a=group.Player1;var b=group.Player2;
                foreach(var p in owners){p.TeamArenaStatistic.TodayWin=8;p.TeamArenaStatistic.TodayBattles=19;
                    Query("UPDATE teamarena SET todaywin=8,todaybattles=19 WHERE entityid=@uid","uid",(long)p.Entity.UID);}
                now=now.AddDays(1);group.End(b);CommitTeamResult(group,true);var run=(Guid)Field(group,"ResultKey");
                Check(owners.All(p=>p.TeamArenaStatistic.TodayBattles==19)
                    && Scalar("SELECT counter_version FROM revora_team_arena_intents WHERE winner=@uid",a.Entity.UID)==2,
                    "team capture versions the saved-day counters before a rolled-back cross-midnight result: "+legacy);
                if(legacy) {
                    // Represent an older immutable capture whose arithmetic predates saved-day counters.
                    Query("UPDATE revora_team_arena_intents SET counter_version=1 WHERE id=@run;UPDATE revora_team_arena_intent_statistics SET wins_after=wins_before+CASE WHEN won THEN 1 ELSE 0 END,battles_after=battles_before+1 WHERE run=@run","run",run);
                }else {
                    Query("UPDATE revora_team_arena_intent_statistics SET wins_after=2,battles_after=2 WHERE run=@run","run",run);
                    ColdTeamRecovery(()=>{bool refused=false;try{Call(recovery,null,"Recover",run);}catch(InvalidOperationException){refused=true;}
                        Check(refused && owners.All(p=>Scalar("SELECT todaybattles FROM teamarena WHERE entityid=@uid",p.Entity.UID)==19),
                            "recovery rejects fabricated captured daily counts before applying any team result");});
                    Query("UPDATE revora_team_arena_intent_statistics SET wins_after=CASE WHEN won THEN 1 ELSE 0 END,battles_after=1 WHERE run=@run","run",run);
                }
                now=now.AddDays(1);
                ColdTeamRecovery(()=>Check((bool)Call(recovery,null,"Recover",run) && !(bool)Call(recovery,null,"Recover",run),
                    "delayed recovery retains the captured counter version and grants one result: "+legacy));
                foreach(var p in owners) {
                    p.ArenaStatistic=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
                    p.TeamArenaStatistic=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
                }
                Check(owners.All(p=>p.TeamArenaStatistic.TodayBattles==(legacy?20u:1u))
                    && a.TeamArenaStatistic.TodayWin==(legacy?9u:1u)
                    && owners.All(p=>Scalar("SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid",p.Entity.UID)==0),
                    "ordinary reload preserves captured counters without inventing daily milestone rewards: "+legacy);
                CleanupTeamFixture(group);
                if(!legacy) {
                    now=now.AddDays(-1);group=new TeamQualifier.QualifierList.QualifierGroup(a,b);group.Import();group.End(b);CommitTeamResult(group);CleanupTeamFixture(group);
                    Check(a.TeamArenaStatistic.TodayWin==2 && a.TeamArenaStatistic.TodayBattles==2,
                        "a later saved result on the same day advances reconstructed team counters");
                    ColdTeamRecovery(()=>Check(!(bool)Call(recovery,null,"Recover",run),
                        "replaying an earlier finished capture ignores later same-day counter growth"));
                }
                now=now.AddDays(7);
            }
        }finally{clock.SetValue(null,prior);}
    }

    static void VerifySoloSignupRollover(Map town) {
        var p=PointBuyer(town);p.Entity.Level=140;p.ArenaStatistic.AcceptBox=false;p.ArenaState=Qualifier.FindMatch;
        p.Account=new AccountTable(null){EntityID=p.Entity.UID,State=AccountTable.AccountState.Player};
        p.ArenaStatistic.TodayWin=7;p.ArenaStatistic.TodayBattles=9;
        Query("UPDATE entities SET level=140 WHERE uid=@uid;UPDATE arena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);var now=new DateTime(2062,6,1,20,0,0,DateTimeKind.Utc);
        clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            p.ArenaStatistic.PlayWith=PointBuyer(town);
            Qualifier.QualifyEngine.DoSignup(p);
            Check(!Qualifier.PlayerList.ContainsKey(p.Entity.UID) && p.ArenaStatistic.TodayWin==7
                && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==0,
                "prepared solo signup cannot reset or enqueue a player with an existing invitation");
            p.ArenaStatistic.PlayWith=null;
            Qualifier.QualifyEngine.DoSignup(p);
            Check(!Fenced(p) && Qualifier.PlayerList.ContainsKey(p.Entity.UID)
                && p.ArenaStatistic.TodayWin==0 && p.ArenaStatistic.TodayBattles==0
                && p.ArenaPoints==ArenaTable.ArenaPointFill(140) && p.TeamArenaStatistic.ArenaPoints==p.ArenaPoints,
                "ordinary prepared solo signup resets saved daily counters and restores empty points before queue eligibility");
            Qualifier.QualifyEngine.DoSignup(p);
            var loaded=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(loaded.TodayWin==0 && loaded.TodayBattles==0 && loaded.ArenaPoints==p.ArenaPoints
                && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                "repeated solo signup preserves one rollover receipt and normal reload sees its reset");
            Qualifier.PlayerList.Remove(p.Entity.UID);p.ArenaStatistic.Status=Revora.Network.GamePackets.QualifierStatistic.NotSignedUp;
            now=now.AddDays(1);p.ArenaStatistic.TodayWin=7;p.ArenaStatistic.TodayBattles=9;
            Query("UPDATE arena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);
            Query("CREATE FUNCTION suppress_signup_rollover() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_signup_rollover BEFORE INSERT ON revora_arena_day_rollovers FOR EACH ROW EXECUTE FUNCTION suppress_signup_rollover()");
            try {
                Qualifier.QualifyEngine.DoSignup(p);
                Check(Fenced(p) && !Qualifier.PlayerList.ContainsKey(p.Entity.UID)
                    && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",p.Entity.UID)==7
                    && Scalar("SELECT count(*) FROM revora_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                    "failed rollover through solo signup rolls back counters and refuses queue admission");
            } finally {Query("DROP TRIGGER suppress_signup_rollover ON revora_arena_day_rollovers;DROP FUNCTION suppress_signup_rollover()");}
        } finally {Qualifier.PlayerList.Remove(p.Entity.UID);clock.SetValue(null,prior);}
    }

    static void VerifyTeamCounterRollover(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaDayRollover");
        var group=TeamCoordinatorGroup(town,false,false);var p=group.Player1;CleanupTeamFixture(group);
        p.TeamArenaStatistic.TodayWin=7;p.TeamArenaStatistic.TodayBattles=9;
        Query("UPDATE teamarena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);var now=new DateTime(2061,6,1,20,0,0,DateTimeKind.Utc);
        clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            Check(!(bool)Call(type,null,"Prepare",p) && p.TeamArenaStatistic.TodayWin==7,"team invitation blocks counter rollover before mutation");
            p.TeamArenaStatistic.PlayWith=null;var points=p.ArenaPoints;
            Parallel.For(0,8,n=>Call(type,null,"Prepare",p));
            Check(!Fenced(p) && p.TeamArenaStatistic.TodayWin==0 && p.TeamArenaStatistic.TodayBattles==0 && p.ArenaPoints==points
                && Scalar("SELECT count(*) FROM revora_team_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                "concurrent idle team rollover clears old daily counters exactly once without changing points");
            p.TeamArenaStatistic.Rank=12;TeamArenaTable.Reset(p.TeamArenaStatistic);
            Check(p.TeamArenaStatistic.Rank==12,"rollover ownership prevents the legacy team reset from replacing managed state");
            var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
            Check(saved.TodayWin==0 && saved.TodayBattles==0 && saved.ArenaPoints==points,"team counter rollover survives normal statistics loading");
            now=now.AddDays(-1);Check(!(bool)Call(type,null,"Prepare",p),"team counter rollover rejects a clock rewind behind its saved date");now=now.AddDays(2);
            p.TeamArenaStatistic.TodayWin=7;p.TeamArenaStatistic.TodayBattles=9;
            Query("UPDATE teamarena SET todaywin=7,todaybattles=9 WHERE entityid=@uid","uid",(long)p.Entity.UID);
            Query("CREATE FUNCTION suppress_team_rollover() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_team_rollover BEFORE INSERT ON revora_team_arena_day_rollovers FOR EACH ROW EXECUTE FUNCTION suppress_team_rollover()");
            try{Check(!(bool)Call(type,null,"Prepare",p) && Fenced(p)
                && Scalar("SELECT todaywin FROM teamarena WHERE entityid=@uid",p.Entity.UID)==7
                && Scalar("SELECT count(*) FROM revora_team_arena_day_rollovers WHERE uid=@uid",p.Entity.UID)==1,
                "failed team rollover receipt rolls back the daily counter update and fences its cache");}
            finally{Query("DROP TRIGGER suppress_team_rollover ON revora_team_arena_day_rollovers;DROP FUNCTION suppress_team_rollover()");}
        }finally{clock.SetValue(null,prior);}
    }

    static void RunOwnedArenaScheduler(bool team) {
        // Other fixtures deliberately retain legacy groups; isolate them from a real scheduler pass.
        if(team) {
            var saved=TeamQualifier.QualifierList.Groups.Values.Where(g=>Field(g,"CompletionOwner")==null && !(bool)Field(g,"OwnedEntryRequested")).ToArray();
            foreach(var g in saved)TeamQualifier.QualifierList.Groups.Remove(g.ID);
            try{TeamQualifier.CheckGroups();}
            finally{foreach(var g in saved)TeamQualifier.QualifierList.Groups.Add(g.ID,g);}
        }else {
            var saved=Qualifier.QualifierList.Groups.Values.Where(g=>Field(g,"CompletionOwner")==null && !(bool)Field(g,"OwnedEntryRequested")).ToArray();
            Group removed;foreach(var g in saved)Qualifier.QualifierList.Groups.TryRemove(g.ID,out removed);
            try{Qualifier.CheckGroups();}
            finally{foreach(var g in saved)Qualifier.QualifierList.Groups.TryAdd(g.ID,g);}
        }
    }

    static void VerifySoloMixedRecovery(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var cause=Enum.Parse(intents.GetNestedType("Cause",Hidden),"Result");
        var group=StatGroup(town);var a=group.Player1;var b=group.Player2;
        lock(Field(group,"ParticipationSync"))lock(a.ItemSyncRoot)lock(b.ItemSyncRoot)
            Check((bool)Call(intents,null,"Capture",group,cause),"mixed solo fixture preserves an observed durable intent before cache failure");
        Call(cache,null,"Fail",a,"solo-mixed-fixture",new InvalidOperationException("fixture"));
        var replacement=ArenaPlayer(town);Kernel.GamePool[b.Entity.UID]=replacement;
        try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && !Fenced(b),
            "mixed solo recovery rejects replaced owners before invalidating the healthy original");}
        finally{Kernel.GamePool[b.Entity.UID]=b;}
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Busy" && !Fenced(b),
                "mixed solo recovery defers busy owners without fencing the healthy peer");}finally{release.Set();task.Wait();}
        }
        Query("CREATE FUNCTION suppress_mixed_solo() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_mixed_solo BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_mixed_solo()");
        try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Failed" && Fenced(a) && Fenced(b)
            && LedgerRows(group,"active")==1 && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",a.Entity.UID)==0,
            "mixed solo write failure fences both caches and rolls back the result bundle");}
        finally{Query("DROP TRIGGER suppress_mixed_solo ON revora_arena_result_commits;DROP FUNCTION suppress_mixed_solo()");}
        Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Committed"
            && Call(coordinator,null,"RecoverFenced",group).ToString()=="AlreadyCommitted" && Fenced(a) && Fenced(b)
            && Scalar("SELECT todaywin FROM arena WHERE entityid=@uid",a.Entity.UID)==1,
            "mixed solo recovery retries once durably without re-enabling stale sessions");
        var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",b.Entity.UID);
        Check(saved.TotalLose==1 && saved.TodayBattles==1,"mixed solo repaired loss survives ordinary statistics reload");
        foreach(var p in new[]{a,b})Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);
        Call(coordinator,null,"Cleanup",group);
        var noIntent=StatGroup(town);
        Call(cache,null,"Fail",noIntent.Player1,"solo-missing-intent-fixture",new InvalidOperationException("fixture"));
        Check(Call(coordinator,null,"RecoverFenced",noIntent).ToString()=="NotReady" && !Fenced(noIntent.Player2),
            "mixed solo RAM ending without observation evidence leaves the healthy peer untouched");
    }

    static void VerifySoloPendingFencedRecovery(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var intents=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultIntents");
        var cause=Enum.Parse(intents.GetNestedType("Cause",Hidden),"Disconnect");
        var group=StatGroup(town,false);var a=group.Player1;var b=group.Player2;
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(b.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(coordinator,null,"EndForPlayer",group,a,cause).ToString()=="Busy" && !group.Done,
                "busy solo departure retains its first cause before freezing the outcome");}finally{release.Set();task.Wait();}
        }
        Call(cache,null,"Fail",b,"pending-solo-fixture",new InvalidOperationException("fixture"));
        var replacement=ArenaPlayer(town);Kernel.GamePool[a.Entity.UID]=replacement;
        try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && !group.Done && !Fenced(a),
            "pending fenced recovery cannot freeze an outcome under a replacement session");}finally{Kernel.GamePool[a.Entity.UID]=a;}
        var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCompletionPump");
        var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);
        var now=DateTime.UtcNow;pumpType.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
        Check((bool)Call(pumpType,pump,"Track",group),"pending fenced original match remains trackable before terminal freeze");
        Query("CREATE FUNCTION suppress_pending_solo() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_pending_solo BEFORE INSERT ON revora_arena_result_commits FOR EACH ROW EXECUTE FUNCTION suppress_pending_solo()");
        try{Call(pumpType,pump,"Tick",1);Check(group.Done && ReferenceEquals(group.Loser,a) && Fenced(a) && Fenced(b)
            && LedgerRows(group,"active")==1 && (int)Field(group,"DurableResultReady")==0,
            "pending fenced pump freezes the original departure but retains failed settlement for retry");}
        finally{Query("DROP TRIGGER suppress_pending_solo ON revora_arena_result_commits;DROP FUNCTION suppress_pending_solo()");}
        now=now.AddSeconds(2);Call(pumpType,pump,"Tick",1);
        Check((int)Field(group,"DurableResultReady")==1 && LedgerRows(group,"finished")==1
            && Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE loser=@uid AND cause='Disconnect'",a.Entity.UID)==1,
            "pending fenced recovery commits the retained disconnect cause and original loser once");
        var saved=(Revora.Network.GamePackets.QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",a.Entity.UID);
        Check(saved.TotalLose==1 && saved.TodayBattles==1,"pending fenced loss survives production statistics reload");
        var map=a.Map;
        foreach(var p in new[]{a,b})Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);
        now=now.AddSeconds(2);Check((int)Call(pumpType,pump,"Tick",1)==1 && !Kernel.Maps.ContainsKey(map.ID),
            "pending fenced result releases its owned map after original sessions leave");
        var unobserved=StatGroup(town,false);
        Call(cache,null,"Fail",unobserved.Player1,"unobserved-solo-fixture",new InvalidOperationException("fixture"));
        Check(Call(coordinator,null,"RecoverFenced",unobserved).ToString()=="NotReady" && !unobserved.Done && !Fenced(unobserved.Player2),
            "cache failure alone cannot invent an unobserved active match result");
    }

    static void VerifyTeamPendingFencedRecovery(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaResultCoordinator");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        var group=TeamCoordinatorGroup(town,false);var owners=TeamFixtureOwners(group);var departing=group.Player1;
        foreach(var member in owners.Where(p=>!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2)))member.Entity.Hitpoints=0;
        try {
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var task=Task.Run(()=>{lock(owners.Last().ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Check(Call(coordinator,null,"ObserveDisconnect",group,departing).ToString()=="Busy" && !group.Done,
                    "busy team leader disconnect retains its terminal departure before result freeze");}finally{release.Set();task.Wait();}
            }
            Call(cache,null,"Fail",owners.Last(),"team-pending-fixture",new InvalidOperationException("fixture"));
            var replacement=PointBuyer(town);Kernel.GamePool[departing.Entity.UID]=replacement;
            try{Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && !group.Done && !Fenced(departing),
                "pending team recovery rejects replaced owners before freezing or fencing remaining players");}
            finally{Kernel.GamePool[departing.Entity.UID]=departing;}
            var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeamArenaCompletionPump");
            var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);
            var now=DateTime.UtcNow;pumpType.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
            Check((bool)Call(pumpType,pump,"Track",group),"pending fenced team remains scheduled before terminal freeze");
            Query("CREATE FUNCTION suppress_team_pending() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_team_pending BEFORE INSERT ON revora_team_arena_results FOR EACH ROW EXECUTE FUNCTION suppress_team_pending()");
            try{Call(pumpType,pump,"Tick",1);Check(group.Done && group.Finished && ReferenceEquals(group.Loser,departing)
                && owners.All(Fenced) && (int)Field(group,"DurableResultReady")==0
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",departing.Entity.UID)==0,
                "pending team recovery retains the first loser while a failed result transaction rolls back");}
            finally{Query("DROP TRIGGER suppress_team_pending ON revora_team_arena_results;DROP FUNCTION suppress_team_pending()");}
            now=now.AddSeconds(2);Call(pumpType,pump,"Tick",1);
            Check((int)Field(group,"DurableResultReady")==1 && owners.All(Fenced)
                && Scalar("SELECT count(*) FROM revora_team_arena_results WHERE loser=@uid",departing.Entity.UID)==1,
                "pending team recovery retries one durable roster result without re-enabling stale caches");
            foreach(var p in owners) {
                var saved=(Revora.Network.GamePackets.TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",p.Entity.UID);
                Check(saved.TodayBattles==1 && saved.ArenaPoints==Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID),
                    "pending team result and shared point mirror survive ordinary participant reload");
            }
            var map=departing.Map;foreach(var p in owners)Call(typeof(Revora.Client.GameState),p,"ReleaseSessionRegistration",true);
            now=now.AddSeconds(2);Check((int)Call(pumpType,pump,"Tick",1)==1 && !Kernel.Maps.ContainsKey(map.ID),
                "pending team result cleans its map after original registrations are released");
        }finally{CleanupTeamFixture(group);}
        var unobserved=TeamCoordinatorGroup(town,false);
        try {
            Call(cache,null,"Fail",unobserved.Player2,"team-unobserved-fixture",new InvalidOperationException("fixture"));
            Check(Call(coordinator,null,"RecoverFenced",unobserved).ToString()=="NotReady" && !unobserved.Done && !Fenced(unobserved.Player1),
                "team cache failure alone cannot create an unobserved active match result");
            // Close the fixture's reserved admission through its normal result path.
            unobserved.End(unobserved.Player2);Call(coordinator,null,"RecoverFenced",unobserved);
        }finally{CleanupTeamFixture(unobserved);}
    }

    static void VerifyFencedArenaTimeouts(Map town) {
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        foreach(bool team in new[]{false,true})foreach(bool tie in new[]{false,true}) {
            object group=team?(object)TeamCoordinatorGroup(town,false):StatGroup(town,false);
            Func<string,object> publicField=name=>group.GetType().GetField(name).GetValue(group);
            var owners=team?TeamFixtureOwners((Revora.Game.TeamQualifier.QualifierList.QualifierGroup)group)
                :new[]{((Group)group).Player1,((Group)group).Player2};
            var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features."+(team?"TeamArenaResultCoordinator":"ArenaResultCoordinator"));
            var clock=coordinator.GetField("MatchClock",Hidden);
            int duration=team?300000:180000;
            var start=new Time32(int.MaxValue-1000);var now=new Time32(unchecked(start.Value+duration-1));
            group.GetType().GetField("ImportTime").SetValue(group,start);
            group.GetType().GetField("Player1Damage").SetValue(group,100u);
            group.GetType().GetField("Player2Damage").SetValue(group,tie?100u:50u);
            clock.SetValue(null,new Func<Time32>(()=>now));
            try {
                Call(cache,null,"Fail",owners.Last(),"timeout-fenced-fixture",new InvalidOperationException("fixture"));
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="NotReady" && !(bool)publicField("Done") && !Fenced(owners[0]),
                    "fenced "+(team?"team":"solo")+" match cannot end before its actual timeout: tie="+tie);
                now=new Time32(unchecked(start.Value+duration));
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="Committed" && owners.All(Fenced)
                    && ReferenceEquals(publicField("Loser"),publicField(tie?"Player1":"Player2")),
                    "fenced "+(team?"team":"solo")+" timeout uses normal damage and tie rules across clock wrap: tie="+tie);
                Check(Call(coordinator,null,"RecoverFenced",group).ToString()=="AlreadyCommitted",
                    "fenced timeout replay cannot pay twice: team="+team+", tie="+tie);
                if(!team)Check(Scalar("SELECT count(*) FROM revora_arena_result_intents WHERE winner=@uid AND cause='Timeout'",((GameState)publicField("Winner")).Entity.UID)==1,
                    "fenced solo timeout persists its causal evidence");
                var map=owners[0].Map;
                foreach(var p in owners)Call(typeof(GameState),p,"ReleaseSessionRegistration",true);
                Check(Call(coordinator,null,"Cleanup",group).ToString()=="Committed" && !Kernel.Maps.ContainsKey(map.ID),
                    "fenced timeout result permits owned cleanup after session release: team="+team);
            }finally{clock.SetValue(null,new Func<Time32>(()=>Time32.Now));if(team)CleanupTeamFixture((Revora.Game.TeamQualifier.QualifierList.QualifierGroup)group);}
        }
    }

    static void VerifyTwentyArenaEntries(Map town,bool suppress=false) {
        var clock=ArenaLedger.GetField("Clock",Hidden);var prior=clock.GetValue(null);
        var now=new DateTime(2058,6,2,19,0,0,DateTimeKind.Utc);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            var group=StatGroup(town,false,false);var a=group.Player1;var b=group.Player2;
            for(int n=1;n<=21;n++) {
                if(n>1)group=new Group(a,b);
                group.GetType().GetField("RegisterLedgerOnEntry",Instance).SetValue(group,true);
                if(suppress && n==20) {
                    Query("CREATE FUNCTION suppress_twenty_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=11111 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_twenty_receipt BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_twenty_receipt()");
                    try{group.Import();}finally{Query("DROP TRIGGER suppress_twenty_receipt ON revora_achievement_evidence;DROP FUNCTION suppress_twenty_receipt()");}
                    Check(LedgerRows(group,"active")==0 && new[]{a,b}.All(p=>Fenced(p)
                        && Scalar("SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid",p.Entity.UID)==19
                        && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11111",p.Entity.UID)==0),
                        "suppressed twentieth-entry achievement rolls back both entry records and the new match");
                    var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
                    Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="Committed" && (bool)Field(group,"DurableAdmissionCancelled"),
                        "failed twentieth entry cancels its reservation through owned recovery");
                    foreach(var player in new[]{a,b})Call(typeof(GameState),player,"ReleaseSessionRegistration",true);
                    Check(Call(coordinator,null,"Cleanup",group).ToString()=="Committed","failed twentieth entry releases its map after fenced owners disconnect");
                    return;
                }
                group.Import();
                Check(LedgerRows(group,"active")==1 && Scalar("SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid",a.Entity.UID)==n,
                    "ordinary registered fight creates exactly one daily entry: "+n);
                if(n==19 || n==20)Check(new[]{a,b}.All(p=>Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11111",p.Entity.UID)==(n==20?1:0)),
                    "twenty-entry threshold is awarded on accepted entry to both fighters: "+n);
                group.Import();Call(ArenaProof,null,"RecordStart",group);
                Check(Scalar("SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid",a.Entity.UID)==n,"entry replay cannot inflate daily participation");
                group.End(b);Check(Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator"),null,"Complete",group).ToString()=="Committed","entered match completes before the next admission");group.Export();
            }
            now=now.AddDays(1);group=new Group(a,b);group.GetType().GetField("RegisterLedgerOnEntry",Instance).SetValue(group,true);group.Import();
            Check(Scalar("SELECT count(DISTINCT day) FROM revora_arena_daily_entries WHERE uid=@uid",a.Entity.UID)==2
                && Scalar("SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid AND day='20580603'",a.Entity.UID)==1,
                "next Pacific day starts a separate accepted-entry count");
            var restored=new Achievement(a.Entity);restored.Load("");Call(typeof(Achievement),restored,"Refresh",DateTime.UtcNow,true);
            int position=(11111/100-101)*32+11111%100-1;
            Check((System.BitConverter.ToUInt32(restored.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11111",a.Entity.UID)==1,
                "production achievement loader retains one twenty-entry completion across days");
            group.End(b);Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator"),null,"Complete",group);group.Export();
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifySoloAdmissions(Map town) {
        VerifyRegisteredArenaEntry(town);VerifyTwentyArenaEntries(town);VerifyTwentyArenaEntries(town,true);
        VerifySoloInterruptedTransfer(town);
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaAdmissions");
        var group=new Group(ArenaPlayer(town),ArenaPlayer(town));
        Query("ALTER TABLE revora_arena_admissions ADD CONSTRAINT reject_solo_reservation CHECK(player1=0) NOT VALID");
        try{group.Import();}finally{Query("ALTER TABLE revora_arena_admissions DROP CONSTRAINT reject_solo_reservation");}
        Check((int)Field(group,"imported")==0 && group.Player1.Entity.MapID==1002 && group.Player2.Entity.MapID==1002
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",group.Player1.Entity.UID)==0,
            "failed solo reservation rolls back before transfer or import latch consumption");
        group.Import();
        Check((bool)Field(group,"DurableAdmissionReserved") && group.Player1.Map.ID!=1002
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NULL",group.Player1.Entity.UID)==1,
            "solo import commits one durable original-pair reservation before its fight starts");
        using(var c=DataHolder.MySqlConnection){c.Open();Check((bool)Call(ArenaLedger,null,"Pending",c,null,group.Player1.Entity.UID)
            && (bool)Call(ArenaLedger,null,"Pending",c,null,group.Player2.Entity.UID),
            "both reserved solo owners are pending before match-ledger adoption");}
        group.Import();Check(Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",group.Player1.Entity.UID)==1,
            "replayed solo import cannot reserve or transfer a second time");
        Check((bool)Call(ArenaLedger,null,"Start",group) && LedgerRows(group,"active")==1,
            "match ledger adopts the exact admitted solo pair");
        var interrupted=LedgerGroup(town);
        ColdTeamRecovery(()=>Call(type,null,"PrepareStartup",1));
        Check(Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NOT NULL",interrupted.Player1.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NOT NULL",group.Player1.Entity.UID)==0,
            "cold solo admission recovery closes unadopted transfers and preserves active match ownership");
        bool refused=false;try{Call(ArenaLedger,null,"Start",interrupted);}catch(InvalidOperationException){refused=true;}
        Check(refused && LedgerRows(interrupted,"active")==0,
            "stale pre-restart solo actors cannot start an abandoned reservation");
        // Fixture teardown only: these matches intentionally have no reward outcome.
        foreach(var match in new[]{group,interrupted}) {
            var map=match.Player1.Map;
            foreach(var p in new[]{match.Player1,match.Player2}){p.Entity.Teleport(1002,408,330);p.QualifierGroup=null;}
            Group removed;Qualifier.QualifierList.Groups.TryRemove(match.ID,out removed);map.Dispose();
        }
    }

    static void VerifyRegisteredArenaEntry(Map town) {
        foreach(bool uninitialized in new[]{false,true}) {
            var rejected=new Group(ArenaPlayer(town),ArenaPlayer(town));
            rejected.GetType().GetField("RegisterLedgerOnEntry",Instance).SetValue(rejected,true);
            if(!uninitialized)rejected.Player1.Entity.Level=39;
            var enabled=ArenaProof.GetField("enabled",Hidden);var prior=enabled.GetValue(null);
            try {
                if(uninitialized)enabled.SetValue(null,false);
                rejected.Import();
                Check((int)Field(rejected,"imported")==0 && rejected.Player1.Entity.MapID==1002 && rejected.Player2.Entity.MapID==1002
                    && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid",rejected.Player1.Entity.UID)==0,
                    "registered entry refuses transfer before eligibility and participation initialization: storage="+uninitialized);
            }finally{enabled.SetValue(null,prior);Group removed;Qualifier.QualifierList.Groups.TryRemove(rejected.ID,out removed);}
        }
        foreach(string fault in new[]{"none","ledger","participation","daily-entry"}) {
            var group=new Group(ArenaPlayer(town),ArenaPlayer(town));var owners=new[]{group.Player1,group.Player2};
            foreach(var p in owners)p.TeamArenaStatistic=new Revora.Network.GamePackets.TeamQualifierStatistic(true){EntityID=p.Entity.UID};
            group.GetType().GetField("RegisterLedgerOnEntry",Instance).SetValue(group,true);
            if(fault=="ledger")Query("CREATE FUNCTION suppress_entry_ledger() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_entry_ledger BEFORE INSERT ON revora_arena_matches FOR EACH ROW EXECUTE FUNCTION suppress_entry_ledger()");
            if(fault=="participation")Query("ALTER TABLE revora_arena_participation ADD CONSTRAINT reject_entry_receipt CHECK(uid<opponent) NOT VALID");
            if(fault=="daily-entry")Query("ALTER TABLE revora_arena_daily_entries ADD CONSTRAINT reject_daily_entry CHECK(uid<opponent) NOT VALID");
            try{group.Import();}
            finally {
                if(fault=="daily-entry")Query("ALTER TABLE revora_arena_daily_entries DROP CONSTRAINT reject_daily_entry");
                if(fault=="ledger")Query("DROP TRIGGER suppress_entry_ledger ON revora_arena_matches;DROP FUNCTION suppress_entry_ledger()");
                if(fault=="participation")Query("ALTER TABLE revora_arena_participation DROP CONSTRAINT reject_entry_receipt");
            }
            if(fault=="none") {
                Check(LedgerRows(group,"active")==1 && owners.All(p=>ArenaReceipts(p)==1 && HasAchievement(p,11032) && !Fenced(p)),
                    "registered ordinary solo entry commits its ledger and both participation achievements together");
                group.Import();Call(ArenaProof,null,"RecordStart",group);
                Check(LedgerRows(group,"active")==1 && owners.All(p=>ArenaReceipts(p)==1),
                    "registered entry replay preserves one ledger and each first-participation receipt");
                // Simulate loss of the post-commit acknowledgement/projection, not a network test.
                var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
                foreach(var p in owners)Call(cache,null,"Fail",p,"entry-projection-fixture",new InvalidOperationException("fixture"));
                group.GetType().GetField("EntryRecoveryRequired",Instance).SetValue(group,true);
                group.GetType().GetField("ParticipationRecorded",Instance).SetValue(group,false);
                var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
                Query("DELETE FROM revora_arena_participation WHERE uid=@uid","uid",(long)group.Player1.Entity.UID);
                Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="Failed" && LedgerRows(group,"active")==1
                    && !(bool)Field(group,"DurableAdmissionCancelled") && (bool)Field(group,"EntryRecoveryRequired"),
                    "incomplete committed entry evidence cannot authorize cancellation or clear recovery ownership");
                Query("INSERT INTO revora_arena_participation(uid,opponent,match_id,rule_version) VALUES(@uid,@opponent,@run,1)",
                    "uid",(long)group.Player1.Entity.UID,"opponent",(long)group.Player2.Entity.UID,"run",ArenaGuid(group));
                Query("CREATE TABLE entry_fixture_copy AS SELECT * FROM revora_arena_daily_entries WHERE run=@run","run",ArenaGuid(group));
                foreach(string damage in new[]{"missing","opponent","day","version"}) {
                    if(damage=="missing")Query("DELETE FROM revora_arena_daily_entries WHERE run=@run AND uid=@uid","run",ArenaGuid(group),"uid",(long)group.Player1.Entity.UID);
                    if(damage=="opponent")Query("UPDATE revora_arena_daily_entries SET opponent=999999 WHERE run=@run","run",ArenaGuid(group));
                    if(damage=="day")Query("UPDATE revora_arena_daily_entries SET day='19000101' WHERE run=@run","run",ArenaGuid(group));
                    if(damage=="version")Query("UPDATE revora_arena_matches SET entry_evidence_version=2 WHERE id=@run","run",ArenaGuid(group));
                    Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="Failed" && (bool)Field(group,"EntryRecoveryRequired")
                        && !(bool)Field(group,"DurableAdmissionCancelled"),"malformed committed daily entry cannot authorize recovery: "+damage);
                    Query("DELETE FROM revora_arena_daily_entries WHERE run=@run;INSERT INTO revora_arena_daily_entries SELECT * FROM entry_fixture_copy;UPDATE revora_arena_matches SET entry_evidence_version=1 WHERE id=@run","run",ArenaGuid(group));
                }
                Query("DROP TABLE entry_fixture_copy");
                Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="Committed" && owners.All(Fenced)
                    && !(bool)Field(group,"EntryRecoveryRequired") && (bool)Field(group,"ParticipationRecorded")
                    && !(bool)Field(group,"DurableAdmissionCancelled") && LedgerRows(group,"active")==1,
                    "committed registered entry is recognized without replaying evidence or enabling stale caches");
            }else {
                Check(LedgerRows(group,"active")==0 && owners.All(p=>ArenaReceipts(p)==0 && !HasAchievement(p,11032) && Fenced(p))
                    && !(bool)Field(group,"ParticipationRecorded"),
                    "registered entry failure rolls back ledger and both participants' evidence: "+fault);
                Check(Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NULL",group.Player1.Entity.UID)==1,
                    "failed registered entry preserves its pre-transfer reservation for recovery: "+fault);
                group.End(group.Player1);Check(!group.Done,"failed registered entry cannot freeze an unearned fight result: "+fault);
                var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
                var replacement=ArenaPlayer(town);Kernel.GamePool[group.Player1.Entity.UID]=replacement;
                try{Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="NotReady" && !(bool)Field(group,"DurableAdmissionCancelled"),
                    "entry recovery rejects replacement session ownership: "+fault);}finally{Kernel.GamePool[group.Player1.Entity.UID]=group.Player1;}
                Query("CREATE FUNCTION suppress_entry_cancel() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_entry_cancel BEFORE UPDATE ON revora_arena_admissions FOR EACH ROW EXECUTE FUNCTION suppress_entry_cancel()");
                try{Check(Call(coordinator,null,"RecoverEntry",group).ToString()=="Failed" && (bool)Field(group,"EntryRecoveryRequired"),
                    "failed entry cancellation remains pending when its durable update is suppressed: "+fault);}
                finally{Query("DROP TRIGGER suppress_entry_cancel ON revora_arena_admissions;DROP FUNCTION suppress_entry_cancel()");}
                var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCompletionPump");
                var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);var now=DateTime.UtcNow;
                pumpType.GetField("Clock",Instance).SetValue(pump,new Func<DateTime>(()=>now));
                Check((bool)Call(pumpType,pump,"Track",group) && (int)Call(pumpType,pump,"Tick",1)==0
                    && (bool)Field(group,"DurableAdmissionCancelled") && owners.All(Fenced) && !group.Done && LedgerRows(group,"active")==0,
                    "completion pump cancels rolled-back entry without a result and retains fenced cleanup: "+fault);
                foreach(var p in owners)Call(typeof(GameState),p,"ReleaseSessionRegistration",true);
                now=now.AddSeconds(2);Check((int)Call(pumpType,pump,"Tick",1)==1 && (bool)Field(group,"CleanupCompleted"),
                    "cancelled entry finishes map cleanup after original sessions release: "+fault);
            }
            var map=group.Player1.Map;
            foreach(var p in owners)Call(typeof(GameState),p,"ReleaseSessionRegistration",true);
            Group removed;Qualifier.QualifierList.Groups.TryRemove(group.ID,out removed);if(!(bool)Field(group,"CleanupCompleted"))map.Dispose();
        }
    }

    static void VerifySoloInterruptedTransfer(Map town) {
        var coordinator=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultCoordinator");
        var broken=new Group(ArenaPlayer(town),ArenaPlayer(town));var source=Kernel.Maps[700];
        try{Kernel.Maps[700]=null;broken.Import();}finally{Kernel.Maps[700]=source;}
        Check((bool)Field(broken,"DurableAdmissionCancelled") && (bool)Field(broken,"CleanupCompleted")
            && !broken.Done && broken.Player1.Entity.MapID==1002 && broken.Player1.QualifierGroup==null
            && !Qualifier.QualifierList.Groups.ContainsKey(broken.ID),
            "actual solo map-creation failure cancels its reservation and releases match ownership");
        broken.Import();broken.End(broken.Player1);
        Check(!broken.Done && LedgerRows(broken,"active")==0,
            "cancelled solo import replay cannot enter again or manufacture a result");
        var group=LedgerGroup(town);var owners=new[]{group.Player1,group.Player2};
        Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="NotReady",
            "solo cancellation cannot erase an active admitted fight");
        var map=group.Player1.Map;var points=owners.Select(p=>p.ArenaPoints).ToArray();
        group.GetType().GetField("fightStarted",Instance).SetValue(group,false);
        group.End(group.Player2);
        Check(!group.Done,"reserved solo transfer cannot freeze an outcome before fight admission completes");
        group.GetType().GetField("AdmissionInterrupted",Instance).SetValue(group,true);
        group.Player2.Entity.Teleport(1002,408,330);
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var task=Task.Run(()=>{lock(group.Player2.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="Busy" && Kernel.Maps.ContainsKey(map.ID),
                "busy solo partial-transfer owner retains map and reservation for retry");}finally{release.Set();task.Wait();}
        }
        Query("CREATE FUNCTION suppress_solo_cancel() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_solo_cancel BEFORE UPDATE ON revora_arena_admissions FOR EACH ROW EXECUTE FUNCTION suppress_solo_cancel()");
        try{Check(Call(coordinator,null,"CancelInterrupted",group).ToString()=="Failed" && !(bool)Field(group,"DurableAdmissionCancelled")
            && Scalar("SELECT count(*) FROM revora_arena_admissions WHERE player1=@uid AND abandoned_at IS NULL",group.Player1.Entity.UID)==1,
            "suppressed solo cancellation cannot release durable ownership");}
        finally{Query("DROP TRIGGER suppress_solo_cancel ON revora_arena_admissions;DROP FUNCTION suppress_solo_cancel()");}
        var pumpType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCompletionPump");
        var pump=Activator.CreateInstance(pumpType,Instance,null,new object[]{1},null);
        Check((bool)Call(pumpType,pump,"Track",group) && (int)Call(pumpType,pump,"Tick",1)==1
            && !Kernel.Maps.ContainsKey(map.ID) && owners.All(p=>p.Entity.MapID==1002 && p.QualifierGroup==null)
            && group.Player1.Entity.PKMode==(Revora.Game.Enums.PKMode)Field(group,"P1Mode"),
            "solo completion pump cancels interrupted transfer and cleans the entered player and owned map");
        Check(!group.Done && owners.Select(p=>p.ArenaPoints).SequenceEqual(points) && LedgerRows(group,"active")==0
            && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",group.Player1.Entity.UID)==0
            && Call(coordinator,null,"CancelInterrupted",group).ToString()=="AlreadyCommitted",
            "solo transfer cancellation is replay-safe and cannot award statistics or change points");
    }

}
