using System;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;
using Group = Revora.Game.TeamQualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    internal static class TeamArenaAdmissions
    {
        internal static bool Initialized { get; private set; }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_team_arena_admissions(
id uuid PRIMARY KEY,first_leader bigint NOT NULL,second_leader bigint NOT NULL,created_at timestamptz NOT NULL,
CHECK(first_leader>0 AND second_leader>0 AND first_leader<>second_leader));
CREATE TABLE IF NOT EXISTS revora_team_arena_admission_players(
run uuid NOT NULL REFERENCES revora_team_arena_admissions(id),uid bigint NOT NULL CHECK(uid>0),first_side boolean NOT NULL,PRIMARY KEY(run,uid));
CREATE INDEX IF NOT EXISTS revora_team_arena_admission_uid ON revora_team_arena_admission_players(uid);
CREATE TABLE IF NOT EXISTS revora_team_arena_admission_abandoned(
run uuid PRIMARY KEY REFERENCES revora_team_arena_admissions(id),closed_at timestamptz NOT NULL);");}Initialized=true;
        }
        internal static bool Pending(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Guid? except=null) {
            if(!Initialized)return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_admission_players p
WHERE p.uid=@uid AND (NOT @excepted OR p.run<>@except)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=p.run)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned a WHERE a.run=p.run))",
                "uid",(long)uid,"excepted",except.HasValue,"except",except??Guid.Empty))return Equals(q.ExecuteScalar(),true);
        }
        // Admission guard owns all actor/entity locks and commits this before any transfer.
        internal static void Reserve(NpgsqlConnection c,NpgsqlTransaction t,Group group,GameState[] first,GameState[] second) {
            if(!Initialized)return;
            if(t==null || t.Connection!=c || !Monitor.IsEntered(group.ResultSync)
                || first.Length<1 || first.Length>5 || second.Length<1 || second.Length>5
                || !first.Contains(group.Player1) || !second.Contains(group.Player2)
                || first.Concat(second).Any(p=>!Monitor.IsEntered(p.ItemSyncRoot) || !Q.Player(p,false)))
                throw new InvalidOperationException("Team reservation requires owned eligible sides.");
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team admission requires UTC.");
            int added=Q.Execute(c,t,@"INSERT INTO revora_team_arena_admissions(id,first_leader,second_leader,created_at)
VALUES(@id,@first,@second,@at) ON CONFLICT(id) DO NOTHING","id",group.ResultKey,"first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID,"at",now);
            if(added!=1)throw new InvalidOperationException("Team admission already exists; transfer recovery is required.");
            foreach(var p in first.Concat(second))
                if(Q.Execute(c,t,"INSERT INTO revora_team_arena_admission_players(run,uid,first_side) VALUES(@run,@uid,@first)",
                    "run",group.ResultKey,"uid",(long)p.Entity.UID,"first",first.Contains(p))!=1)
                    throw new InvalidOperationException("Team admission roster was not saved.");
        }
        internal static void ValidateOutcome(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(!Initialized)return;
            using(var q=Q.Command(c,t,"SELECT first_leader,second_leader FROM revora_team_arena_admissions WHERE id=@run FOR UPDATE","run",group.ResultKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read()) {
                    if(group.DurableAdmissionReserved)throw new InvalidOperationException("Reserved team admission is missing.");
                    return; // Previously captured migration evidence predates admission ownership.
                }
                if(r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID)throw new InvalidOperationException("Team admission leaders changed.");
            }
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned WHERE run=@run)","run",group.ResultKey))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Abandoned team admission cannot produce a result.");
            foreach(bool first in new[]{true,false})foreach(var p in group.Participants(first))
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_arena_admission_players WHERE run=@run AND uid=@uid AND first_side=@first)",
                    "run",group.ResultKey,"uid",(long)p.Entity.UID,"first",first))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team result contains an unreserved participant.");
        }
        internal static void ValidateCaptured(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            if(!Initialized)return;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_admissions a
JOIN revora_team_arena_intents i ON i.id=a.id WHERE a.id=@run AND
(i.winner NOT IN(a.first_leader,a.second_leader) OR i.loser NOT IN(a.first_leader,a.second_leader)
OR EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned x WHERE x.run=a.id)
OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics s LEFT JOIN revora_team_arena_admission_players p ON p.run=s.run AND p.uid=s.uid
WHERE s.run=a.id AND (p.uid IS NULL OR p.first_side IS DISTINCT FROM (s.won=(i.winner=a.first_leader))))))","run",run))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Captured team result contradicts its admission.");
        }
        internal static void Cancel(NpgsqlConnection c,NpgsqlTransaction t,Group group,GameState[] owners) {
            if(t==null || t.Connection!=c || !Monitor.IsEntered(group.ResultSync) || owners.Length<2
                || owners.Any(p=>!Monitor.IsEntered(p.ItemSyncRoot)))throw new InvalidOperationException("Team cancellation requires original owner locks.");
            foreach(var p in owners)using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Reserved team owner missing.");
            using(var q=Q.Command(c,t,"SELECT first_leader,second_leader FROM revora_team_arena_admissions WHERE id=@run FOR UPDATE","run",group.ResultKey))
            using(var r=q.ExecuteReader())if(!r.Read() || r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID)
                throw new InvalidOperationException("Cancelled admission identity changed.");
            var ids=new System.Collections.Generic.List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM revora_team_arena_admission_players WHERE run=@run ORDER BY uid","run",group.ResultKey))
            using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));
            if(!ids.SequenceEqual(owners.Select(p=>p.Entity.UID)))throw new InvalidOperationException("Cancelled admission roster changed.");
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_intents WHERE id=@run)
OR EXISTS(SELECT 1 FROM revora_team_arena_results WHERE id=@run)","run",group.ResultKey))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("An outcome must settle instead of being cancelled.");
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team cancellation requires UTC.");
            Q.Execute(c,t,"INSERT INTO revora_team_arena_admission_abandoned(run,closed_at) VALUES(@run,@now) ON CONFLICT(run) DO NOTHING","run",group.ResultKey,"now",now);
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned WHERE run=@run)","run",group.ResultKey))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team cancellation receipt was not saved.");
        }
        internal static int AbandonInterrupted(int limit) {
            if(!Initialized || limit<1 || limit>128)throw new ArgumentOutOfRangeException("limit");
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0 || Kernel.DisconnectPool.Count!=0
                || Qualifier.ArenaStatistics.Count!=0 || TeamQualifier.ArenaStatistics.Count!=0
                || Qualifier.QualifierList.Groups.Count!=0 || TeamQualifier.QualifierList.Groups.Count!=0)
                throw new InvalidOperationException("Interrupted team admissions close only at cold startup.");
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team abandonment requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();return Q.Execute(c,null,@"INSERT INTO revora_team_arena_admission_abandoned(run,closed_at)
SELECT a.id,@now FROM revora_team_arena_admissions a
WHERE NOT EXISTS(SELECT 1 FROM revora_team_arena_intents i WHERE i.id=a.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=a.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned x WHERE x.run=a.id)
ORDER BY a.created_at,a.id LIMIT @limit ON CONFLICT(run) DO NOTHING","now",now,"limit",limit);}
        }
        internal static bool HasInterrupted() {
            if(!Initialized)throw new InvalidOperationException("Team admissions are not initialized.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_team_arena_admissions a
WHERE NOT EXISTS(SELECT 1 FROM revora_team_arena_intents i WHERE i.id=a.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=a.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_admission_abandoned x WHERE x.run=a.id))"))return Equals(q.ExecuteScalar(),true);}
        }
    }
}
