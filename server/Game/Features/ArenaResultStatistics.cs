using System;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;

namespace Revora.Game.Features
{
    // Inactive until the coordinator replaces legacy result and reset writers.
    internal static class ArenaResultStatistics
    {
        internal sealed class Snapshot {
            internal GameState Player;internal QualifierStatistic Stats;
            internal uint Before,Points,Matches,Wins,TotalWins,TotalLosses;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_result_statistics(
run uuid NOT NULL REFERENCES revora_arena_matches(id),uid bigint NOT NULL,won boolean NOT NULL,
points_before bigint NOT NULL CHECK(points_before BETWEEN 0 AND 4294967295),
points_after bigint NOT NULL CHECK(points_after BETWEEN 0 AND 4294967295),PRIMARY KEY(run,uid));
ALTER TABLE revora_arena_result_statistics ADD COLUMN IF NOT EXISTS result_order bigserial;
ALTER TABLE revora_arena_result_statistics ADD COLUMN IF NOT EXISTS ordering_verified boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS revora_arena_result_order ON revora_arena_result_statistics(uid,result_order);
");}
        }
        // Revora rounding: floor 3%; cap the transfer at the winner's remaining capacity.
        internal static uint Transfer(uint winner,uint loser) {return (uint)Math.Min((ulong)loser*3/100,(ulong)uint.MaxValue-winner);}
        private static void Locked(GameState p) {
            if(!ArenaActors.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot) || p.ArenaStatistic==null || p.TeamArenaStatistic==null
                || p.ArenaStatistic.EntityID!=p.Entity.UID || p.TeamArenaStatistic.EntityID!=p.Entity.UID)
                throw new InvalidOperationException("Arena statistics require canonical locked players.");
        }
        internal static Snapshot[] Record(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(group==null || !Monitor.IsEntered(group.ParticipationSync) || t==null || t.Connection!=c || !group.Done || !group.OwnsImportedMap)
                throw new InvalidOperationException("Arena statistics require the owned result transaction.");
            var players=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();foreach(var p in players){Locked(p);SkillTable.LockOwner(c,t,p);if(TeamArenaResultStatistics.Pending(c,t,p.Entity.UID))throw new InvalidOperationException("Pending team result owns Arena points.");}
            uint winner,loser;string day;
            using(var q=Q.Command(c,t,"SELECT winner,loser,day FROM revora_arena_matches WHERE id=@run AND state='finished' FOR UPDATE","run",group.ParticipationKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Arena terminal result missing.");
                winner=checked((uint)r.GetInt64(0));loser=checked((uint)r.GetInt64(1));day=r.GetString(2);
            }
            if(group.Winner==null || group.Loser==null || group.Winner.Entity.UID!=winner || group.Loser.Entity.UID!=loser
                || !players.Any(p=>p.Entity.UID==winner) || !players.Any(p=>p.Entity.UID==loser))throw new InvalidOperationException("Arena outcome changed.");
            var result=players.Select(p=>new Snapshot{Player=p,Stats=p.ArenaStatistic,Before=p.ArenaStatistic.ArenaPoints}).ToArray();
            foreach(var row in result)using(var q=Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
            using(var r=q.ExecuteReader()) {
                var s=row.Stats;
                if(!r.Read() || Convert.ToUInt32(r[0])!=s.ArenaPoints || Convert.ToUInt32(r[1])!=s.TodayWin || Convert.ToUInt32(r[2])!=s.TodayBattles
                    || Convert.ToUInt32(r[3])!=s.TotalWin || Convert.ToUInt32(r[4])!=s.TotalLose || r.Read())throw new InvalidOperationException("Arena statistics require reload.");
            }
            foreach(var row in result) {
                var captured=ArenaResultIntents.SnapshotFor(c,t,group.ParticipationKey,row.Player.Entity.UID);
                if(captured!=null && !captured.Statistics.SequenceEqual(ArenaResultIntents.Statistics(row.Stats)))throw new InvalidOperationException("Arena captured statistic preimage changed.");
            }
            uint transfer=Transfer(group.Winner.ArenaStatistic.ArenaPoints,group.Loser.ArenaStatistic.ArenaPoints);
            foreach(var row in result) {
                uint uid=row.Player.Entity.UID;bool won=uid==winner;
                row.Points=won?checked(row.Before+transfer):row.Before-transfer;
                row.TotalWins=checked(row.Stats.TotalWin+(won?1u:0));row.TotalLosses=checked(row.Stats.TotalLose+(won?0u:1));
                if(Q.Execute(c,t,@"INSERT INTO revora_arena_result_statistics(run,uid,won,points_before,points_after,ordering_verified)
VALUES(@run,@uid,@won,@before,@after,true)","run",group.ParticipationKey,"uid",(long)uid,"won",won,"before",(long)row.Before,"after",(long)row.Points)!=1)
                    throw new InvalidOperationException("Arena statistic receipt was not saved.");
                using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won) FROM revora_arena_result_statistics s
JOIN revora_arena_matches m ON m.id=s.run WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)uid,"day",day))
                using(var r=q.ExecuteReader()){r.Read();row.Matches=checked((uint)r.GetInt64(0));row.Wins=checked((uint)r.GetInt64(1));}
                if(Q.Execute(c,t,@"UPDATE arena SET arenapoints=@points,todaywin=@wins,todaybattles=@matches,totalwin=@totalwin,totallose=@totallose WHERE entityid=@uid",
                    "points",(long)row.Points,"wins",(long)row.Wins,"matches",(long)row.Matches,"totalwin",(long)row.TotalWins,"totallose",(long)row.TotalLosses,"uid",(long)uid)!=1
                    || Q.Execute(c,t,"UPDATE teamarena SET arenapoints=@points WHERE entityid=@uid","points",(long)row.Points,"uid",(long)uid)!=1)
                    throw new InvalidOperationException("Arena result balances were not saved.");
            }
            foreach(var row in result)RecordFirstNine(c,t,row.Player.Entity.UID,day);
            RecordClassVictory(c,t,group.ParticipationKey,winner,ArenaResultIntents.SnapshotFor(c,t,group.ParticipationKey,winner));
            foreach(var row in result){Locked(row.Player);if(!ReferenceEquals(row.Player.ArenaStatistic,row.Stats) || row.Stats.ArenaPoints!=row.Before)throw new InvalidOperationException("Arena projection changed.");}
            if(!group.Done || !group.OwnsImportedMap || group.Winner.Entity.UID!=winner || group.Loser.Entity.UID!=loser)throw new InvalidOperationException("Arena result ownership changed.");
            return result;
        }
        internal static void RecordClassVictory(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint uid,ArenaResultIntents.Snapshot snapshot) {
            if(snapshot==null || !snapshot.OpponentClass.HasValue)return;
            byte profession=snapshot.OpponentClass.Value;
            if(profession%10>5)return;
            string key=run.ToString("D");
            switch(profession/10) {
                case 1: ProgressionEvidence.Complete(c,t,uid,11101,"arena-class-victory",key,1);break;
                case 2: ProgressionEvidence.Complete(c,t,uid,11102,"arena-class-victory",key,1);break;
                case 14: ProgressionEvidence.Complete(c,t,uid,11103,"arena-class-victory",key,1);break;
                case 13: ProgressionEvidence.Complete(c,t,uid,11104,"arena-class-victory",key,1);break;
                case 4: ProgressionEvidence.Complete(c,t,uid,11105,"arena-class-victory",key,1);break;
                case 5: ProgressionEvidence.Complete(c,t,uid,11106,"arena-class-victory",key,1);break;
                case 6: ProgressionEvidence.Complete(c,t,uid,11107,"arena-class-victory",key,1);break;
                case 7: ProgressionEvidence.Complete(c,t,uid,11108,"arena-class-victory",key,1);break;
                case 8: ProgressionEvidence.Complete(c,t,uid,11109,"arena-class-victory",key,1);break;
                case 16: ProgressionEvidence.Complete(c,t,uid,11309,"arena-class-victory",key,1);break;
            }
        }
        internal static void RecordFirstNine(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            if(t==null || t.Connection!=c)throw new InvalidOperationException("Arena achievement requires the result transaction.");
            // An unresolved fight must never disappear from the beginning of the day.
            string abandoned=ArenaStartupRecovery.Initialized
                ? " AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id)" : "";
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.state='active' AND (m.player1=@uid OR m.player2=@uid)"+abandoned+")","uid",(long)uid))
                if(Equals(q.ExecuteScalar(),true))return;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m
LEFT JOIN revora_arena_result_statistics s ON s.run=m.id AND s.uid=@uid
WHERE (m.player1=@uid OR m.player2=@uid) AND m.state='finished' AND m.day=@day
AND (s.uid IS NULL OR NOT s.ordering_verified OR s.won<>(m.winner=@uid)))","uid",(long)uid,"day",day))
                if(Equals(q.ExecuteScalar(),true))return;
            using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE won) FROM (
SELECT s.won FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE s.uid=@uid AND (m.player1=@uid OR m.player2=@uid) AND m.day=@day AND m.state='finished'
ORDER BY m.ended_at,s.result_order LIMIT 9) first_results","uid",(long)uid,"day",day))
            using(var r=q.ExecuteReader()){r.Read();if(r.GetInt64(0)!=9 || r.GetInt64(1)!=9)return;}
            ProgressionEvidence.Complete(c,t,uid,11112,"arena-first-nine",day,9);
        }
        // Caller commits first, retaining both player locks through cache projection.
        internal static void Publish(Snapshot[] rows) {
            foreach(var row in rows){Locked(row.Player);if(!ReferenceEquals(row.Player.ArenaStatistic,row.Stats) || row.Stats.ArenaPoints!=row.Before)throw new InvalidOperationException("Arena projection requires reload.");}
            foreach(var row in rows){row.Player.ArenaPoints=row.Points;row.Stats.TodayWin=row.Wins;row.Stats.TodayBattles=row.Matches;row.Stats.TotalWin=row.TotalWins;row.Stats.TotalLose=row.TotalLosses;}
            foreach(var row in rows){row.Player.Send(row.Stats);row.Player.TeamArenaStatistic.Send(row.Player);if(!row.Player.Fake)Q.Refresh(row.Player);}
        }
    }
}
