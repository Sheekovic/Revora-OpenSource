using System;
using System.Linq;
using System.Collections.Generic;
using Npgsql;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Startup-only, invoked by joint preparation before workers, caches and listeners.
    internal static class ArenaStartupRecovery
    {
        private static readonly object Gate=new object();
        internal static bool Initialized {get;private set;}
        internal static bool Prepared {get;private set;}
        // The composition root must call this before loading Arena caches or opening listeners.
        // Bounded passes prevent startup from silently running forever on inconsistent evidence.
        internal static void PrepareStartup(int batchLimit) {
            if(batchLimit<1 || batchLimit>100)throw new ArgumentOutOfRangeException("batchLimit");
            lock(Gate) {
                RequireStartup();Prepared=false;
                ArenaResultCoordinator.Initialize();ArenaDayRollover.Initialize();
                for(int pass=0;pass<batchLimit;pass++) {
                    Sweep(128);
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_arena_matches m WHERE m.state='active'
AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id))")) {
                        if(!Equals(q.ExecuteScalar(),true))break;
                        if(pass==batchLimit-1)throw new InvalidOperationException("Arena startup recovery batch limit reached.");
                    }}
                }
                // A finished ledger is not proof that the statistics and reward transaction committed.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_arena_matches m WHERE m.state='finished' AND (
NOT EXISTS(SELECT 1 FROM revora_arena_result_commits b WHERE b.run=m.id)
OR (SELECT count(*) FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid IN(m.player1,m.player2))<>2))"))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Arena startup found an incomplete finished result.");}
                for(int pass=0;pass<batchLimit;pass++) {
                    ArenaRankingSettlement.ClosePending(31);
                    var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena startup requires UTC.");
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_arena_matches m WHERE m.state='finished' AND m.day<@today
AND NOT EXISTS(SELECT 1 FROM revora_arena_rank_days d WHERE d.day=m.day))","today",MagnoliaQuest.Day(now))) {
                        if(!Equals(q.ExecuteScalar(),true))break;
                        if(pass==batchLimit-1)throw new InvalidOperationException("Arena startup ranking batch limit reached.");
                    }}
                }
                RequireStartup();Prepared=true;
            }
        }

        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_abandoned(
run uuid PRIMARY KEY REFERENCES revora_arena_matches(id),closed_at timestamptz NOT NULL,reason varchar(32) NOT NULL CHECK(reason='startup-without-outcome'));");}Initialized=true;
        }
        internal static bool Abandoned(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            if(!Initialized)return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_abandoned WHERE run=@run)","run",run))return Equals(q.ExecuteScalar(),true);
        }
        internal static int Sweep(int limit) {
            if(limit<1 || limit>128)throw new ArgumentOutOfRangeException("limit");
            lock(Gate) {
                RequireStartup();if(!Initialized)throw new InvalidOperationException("Arena recovery schema is not initialized.");
                var runs=new List<Guid>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT m.id FROM revora_arena_matches m
LEFT JOIN revora_arena_result_intents i ON i.run=m.id WHERE m.state='active'
AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id)
ORDER BY COALESCE(i.observed_at,m.started_at),m.id LIMIT @limit","limit",limit))using(var r=q.ExecuteReader())while(r.Read())runs.Add(r.GetGuid(0));}
                int closed=0;foreach(var run in runs)if(Recover(run) || Abandon(run))closed++;return closed;
            }
        }
        private static bool Abandon(Guid run) {
            RequireStartup();var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena abandonment requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT state,started_at AT TIME ZONE 'UTC' FROM revora_arena_matches WHERE id=@run FOR UPDATE","run",run))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read() || r.GetString(0)!="active")return false;
                    if(now<DateTime.SpecifyKind(r.GetDateTime(1),DateTimeKind.Utc))throw new InvalidOperationException("Arena abandonment clock precedes admission.");
                }
                if(Abandoned(c,t,run))return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_result_intents WHERE run=@run)
OR EXISTS(SELECT 1 FROM revora_arena_result_statistics WHERE run=@run)
OR EXISTS(SELECT 1 FROM revora_arena_result_commits WHERE run=@run)","run",run))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Arena result evidence cannot be discarded as abandoned.");
                if(Q.Execute(c,t,"INSERT INTO revora_arena_abandoned(run,closed_at,reason) VALUES(@run,@now,'startup-without-outcome')","run",run,"now",now)!=1)
                    throw new InvalidOperationException("Arena abandonment was not saved.");
                RequireStartup();t.Commit();return true;
            }}
        }

        private static void RequireStartup() {
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0 || Qualifier.ArenaStatistics.Count!=0 || TeamQualifier.ArenaStatistics.Count!=0 || Qualifier.QualifierList.Groups.Count!=0)
                throw new InvalidOperationException("Arena recovery requires startup before sessions, matches, caches and listeners.");
        }
        internal static bool Recover(Guid run) {lock(Gate)return RecoverCore(run,null);}
        internal static bool RecoverFenced(Qualifier.QualifierList.QualifierGroup group) {
            if(group==null)throw new ArgumentNullException("group");
            return RecoverCore(group.ParticipationKey,group);
        }
        private static void RequireRecovery(Qualifier.QualifierList.QualifierGroup group) {
            if(group==null){RequireStartup();return;}
            if(!System.Threading.Monitor.IsEntered(group.ParticipationSync) || !group.Done || !group.OwnsImportedMap)
                throw new InvalidOperationException("Arena recovery requires its ended match lock and map.");
            foreach(var p in new[]{group.Player1,group.Player2}) {
                Client.GameState canonical;
                if(p==null || p.Entity==null || (p.Fake && !ArenaActors.IsBot(p)) || !p.Entity.FullyLoaded || p.Action!=0 || !p.Entity.PersistenceRequiresReload
                    || !System.Threading.Monitor.IsEntered(p.ItemSyncRoot)
                    || !Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) || !ReferenceEquals(canonical,p)
                    || !ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) || !ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic))
                    throw new InvalidOperationException("Arena recovery requires both fenced canonical owners.");
            }
        }
        private static bool RecoverCore(Guid run,Qualifier.QualifierList.QualifierGroup group) {
                RequireRecovery(group);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    uint first,second,winner,loser;string state,day;DateTime observed,started;
                    using(var q=Q.Command(c,t,@"SELECT m.player1,m.player2,m.state,i.winner,i.loser,i.day,i.observed_at AT TIME ZONE 'UTC',m.started_at AT TIME ZONE 'UTC'
FROM revora_arena_matches m JOIN revora_arena_result_intents i ON i.run=m.id WHERE m.id=@run FOR UPDATE OF m,i","run",run))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())return false;first=checked((uint)r.GetInt64(0));second=checked((uint)r.GetInt64(1));state=r.GetString(2);
                        winner=checked((uint)r.GetInt64(3));loser=checked((uint)r.GetInt64(4));day=r.GetString(5);
                        observed=DateTime.SpecifyKind(r.GetDateTime(6),DateTimeKind.Utc);started=DateTime.SpecifyKind(r.GetDateTime(7),DateTimeKind.Utc);
                    }
                    if(group!=null && (first!=group.Player1.Entity.UID || second!=group.Player2.Entity.UID
                        || group.Winner==null || group.Loser==null || winner!=group.Winner.Entity.UID || loser!=group.Loser.Entity.UID))
                        throw new InvalidOperationException("Arena fenced recovery outcome changed.");
                    if(state=="finished" || Abandoned(c,t,run))return false;
                    if(state!="active" || first==second || winner==loser || !(winner==first && loser==second || winner==second && loser==first)
                        || observed<started || day!=MagnoliaQuest.Day(observed))throw new InvalidOperationException("Arena recovery outcome is inconsistent.");
                    var ids=new[]{first,second}.OrderBy(id=>id).ToArray();var snapshots=ids.Select(id=>ArenaResultIntents.SnapshotFor(c,t,run,id)).ToArray();
                    if(snapshots.Any(s=>s==null))throw new InvalidOperationException("Arena recovery snapshot missing.");
                    for(int n=0;n<ids.Length;n++) {
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)ids[n]))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Arena recovery owner missing.");
                        if(TeamArenaResultStatistics.Pending(c,t,ids[n]))throw new InvalidOperationException("Pending team result must settle before solo recovery.");
                        var saved=snapshots[n].Statistics;
                        using(var q=Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)ids[n]))
                        using(var r=q.ExecuteReader())if(!r.Read() || Enumerable.Range(0,5).Any(i=>Convert.ToInt64(r[i])!=saved[i]) || r.Read())throw new InvalidOperationException("Arena recovery statistic preimage changed.");
                        using(var q=Q.Command(c,t,"SELECT arenapoints FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)ids[n]))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt64(r[0])!=saved[0] || r.Read())throw new InvalidOperationException("Arena recovery point mirror changed.");
                    }
                    uint winnerPoints=checked((uint)snapshots[Array.IndexOf(ids,winner)].Statistics[0]);
                    uint loserPoints=checked((uint)snapshots[Array.IndexOf(ids,loser)].Statistics[0]);uint transfer=ArenaResultStatistics.Transfer(winnerPoints,loserPoints);
                    ArenaRankingSettlement.EnsureOpen(c,t,day);
                    if(Q.Execute(c,t,@"UPDATE revora_arena_matches SET state='finished',winner=@winner,loser=@loser,ended_at=@stamp,day=@day WHERE id=@run AND state='active'",
                        "winner",(long)winner,"loser",(long)loser,"stamp",observed,"day",day,"run",run)!=1)throw new InvalidOperationException("Arena recovered result was not saved.");
                    for(int n=0;n<ids.Length;n++) {
                        uint uid=ids[n];bool won=uid==winner;var snap=snapshots[n];var before=snap.Statistics;
                        uint points=won?checked((uint)before[0]+transfer):checked((uint)before[0]-transfer);
                        uint totalWin=checked((uint)before[3]+(won?1u:0u)),totalLose=checked((uint)before[4]+(won?0u:1u));
                        if(Q.Execute(c,t,@"INSERT INTO revora_arena_result_statistics(run,uid,won,points_before,points_after,ordering_verified) VALUES(@run,@uid,@won,@before,@after,true)",
                            "run",run,"uid",(long)uid,"won",won,"before",before[0],"after",(long)points)!=1)throw new InvalidOperationException("Arena recovered statistic receipt was not saved.");
                        uint matches,wins;using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won) FROM revora_arena_result_statistics s
JOIN revora_arena_matches m ON m.id=s.run WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)uid,"day",day))
                        using(var r=q.ExecuteReader()){r.Read();matches=checked((uint)r.GetInt64(0));wins=checked((uint)r.GetInt64(1));}
                        if(Q.Execute(c,t,@"UPDATE arena SET arenapoints=@points,todaywin=@wins,todaybattles=@matches,totalwin=@totalwin,totallose=@totallose WHERE entityid=@uid",
                            "points",(long)points,"wins",(long)wins,"matches",(long)matches,"totalwin",(long)totalWin,"totallose",(long)totalLose,"uid",(long)uid)!=1
                            || Q.Execute(c,t,"UPDATE teamarena SET arenapoints=@points WHERE entityid=@uid","points",(long)points,"uid",(long)uid)!=1)throw new InvalidOperationException("Arena recovered statistics were not saved.");
                        if(won)ArenaResultStatistics.RecordClassVictory(c,t,run,uid,snap);
                        ArenaResultStatistics.RecordFirstNine(c,t,uid,day);
                        Rewards(c,t,run,uid,day,wins,matches,snap,observed);
                    }
                    if(Q.Execute(c,t,"INSERT INTO revora_arena_result_commits(run) VALUES(@run)","run",run)!=1)throw new InvalidOperationException("Arena recovery bundle receipt missing.");
                    RequireRecovery(group);t.Commit();return true;
                }}
        }
        private static void Rewards(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint uid,string day,uint wins,uint matches,ArenaResultIntents.Snapshot snap,DateTime stamp) {
            if(ArenaBotStore.IsBotUid(uid))return;
            foreach(string milestone in new[]{"wins9","matches20"}) {
                if(milestone=="wins9"?wins<9:matches<20)continue;
                Q.Execute(c,t,@"INSERT INTO revora_arena_daily_rewards(uid,day,milestone,source_run,honor,item_type,rune_type)
VALUES(@uid,@day,@milestone,@run,500,723700,4030101) ON CONFLICT(uid,day,milestone) DO NOTHING","uid",(long)uid,"day",day,"milestone",milestone,"run",run);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND day=@day AND milestone=@milestone AND honor=500 AND item_type=723700",
                    "uid",(long)uid,"day",day,"milestone",milestone))if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Recovered Arena daily reward missing.");
            }
            foreach(string milestone in new[]{"wins100","losses50"}) {
                bool winning=milestone=="wins100";if(winning?wins!=100:matches-wins!=50)continue;bool policy=winning || snap.LossesEnabled;
                long[] items=winning?new long[]{3009002,3004243,3004243,3004244,3004244,3004245}:new long[0];
                Q.Execute(c,t,@"INSERT INTO revora_arena_custom_rewards(uid,day,milestone,source_run,eligible,recipient_vip,minimum_vip,policy_enabled,honor,cp,silver,rvp,item_types,recorded_at)
VALUES(@uid,@day,@milestone,@run,@eligible,@vip,@minimum,@policy,500,@cp,@silver,@rvp,@items,@stamp) ON CONFLICT(uid,day,milestone) DO NOTHING",
                    "uid",(long)uid,"day",day,"milestone",milestone,"run",run,"eligible",policy && snap.Vip>=snap.Minimum,"vip",(int)snap.Vip,"minimum",(int)snap.Minimum,
                    "policy",policy,"cp",winning?(long)snap.Cp:0L,"silver",winning?(decimal)snap.Silver:0m,"rvp",winning?(long)snap.Rvp:0L,"items",items,"stamp",stamp);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND day=@day AND milestone=@milestone","uid",(long)uid,"day",day,"milestone",milestone))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Recovered Arena custom reward missing.");
            }
        }
    }
}
