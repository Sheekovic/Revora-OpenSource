using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;

namespace Revora.Game.Features
{
    // Inactive migration participant. Do not enable beside the legacy reward writers.
    internal static class ArenaMatchLedger
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool initialized;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_matches(
id uuid PRIMARY KEY,player1 bigint NOT NULL CHECK(player1>0),player2 bigint NOT NULL CHECK(player2>0 AND player2<>player1),
map integer NOT NULL CHECK(map BETWEEN 1 AND 65535 AND map<>700),started_at timestamptz NOT NULL,
state varchar(12) NOT NULL CHECK(state IN('active','finished')),winner bigint,loser bigint,ended_at timestamptz,day varchar(8),
CHECK((state='active' AND winner IS NULL AND loser IS NULL AND ended_at IS NULL AND day IS NULL) OR
(state='finished' AND winner IS NOT NULL AND loser IS NOT NULL AND ended_at IS NOT NULL AND winner IN(player1,player2) AND loser IN(player1,player2) AND winner<>loser AND ended_at>=started_at AND day IS NOT NULL)));
ALTER TABLE revora_arena_matches ADD COLUMN IF NOT EXISTS entry_evidence_version integer NOT NULL DEFAULT 0;
CREATE INDEX IF NOT EXISTS revora_arena_match_player1 ON revora_arena_matches(player1,day) WHERE state='finished';
CREATE INDEX IF NOT EXISTS revora_arena_match_player2 ON revora_arena_matches(player2,day) WHERE state='finished';");}initialized=true;
        }
        private static DateTime Now() {var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena ledger requires UTC.");return now;}
        private static bool Player(GameState p) {return ArenaActors.Player(p,false) && p.Entity.Level>=40 && p.ArenaStatistic!=null
            && p.ArenaStatistic.EntityID==p.Entity.UID && p.ArenaStatistic.AcceptBox && p.ArenaState==Qualifier.Fight;}
        private static bool Owns(Group group) {return initialized && group!=null && group.OwnsImportedMap && Player(group.Player1) && Player(group.Player2)
            && group.Player1.Entity.UID!=group.Player2.Entity.UID;}
        internal static bool ReadyForAdmission(Group group) {return initialized && group!=null && Player(group.Player1) && Player(group.Player2)
            && group.Player1.Entity.UID!=group.Player2.Entity.UID && !group.Player1.Entity.Dead && !group.Player2.Entity.Dead;}
        internal static bool Pending(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(ArenaAdmissions.Pending(c,t,uid))return true;
            if(!initialized)return false;
            string sql=ArenaStartupRecovery.Initialized
                ? @"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.state='active' AND (m.player1=@uid OR m.player2=@uid)
AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id))"
                : "SELECT EXISTS(SELECT 1 FROM revora_arena_matches WHERE state='active' AND (player1=@uid OR player2=@uid))";
            using(var q=Q.Command(c,t,sql,"uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Start(Group group) {
            if(group==null)return false;
            lock(group.ParticipationSync) {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    if(!StartInTransaction(c,t,group))return false;
                    t.Commit();return true;
                }}
            }
        }
        internal static bool StartInTransaction(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
                if(group==null || t==null || t.Connection!=c || !Monitor.IsEntered(group.ParticipationSync))
                    throw new InvalidOperationException("Arena start requires an owned transaction and match lock.");
                if(!Owns(group) || !group.StartedOnOwnedMap || group.Player1.Entity.Dead || group.Player2.Entity.Dead)return false;
                uint first=group.Player1.Entity.UID,second=group.Player2.Entity.UID;ushort map=group.Player1.Map.ID;var now=Now();
                    if(TeamArenaResultStatistics.Initialized)
                        foreach(uint uid in new[]{Math.Min(first,second),Math.Max(first,second)}) {
                            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Arena admission owner missing.");
                            if(TeamArenaResultStatistics.Pending(c,t,uid))return false;
                        }
                    ArenaRankingSettlement.EnsureOpen(c,t,MagnoliaQuest.Day(now));
                    ArenaAdmissions.ValidateStart(c,t,group);
                    Q.Execute(c,t,@"INSERT INTO revora_arena_matches(id,player1,player2,map,started_at,state)
VALUES(@id,@first,@second,@map,@now,'active') ON CONFLICT(id) DO NOTHING",
                        "id",group.ParticipationKey,"first",(long)first,"second",(long)second,"map",(int)map,"now",now);
                    using(var q=Q.Command(c,t,"SELECT player1,player2,map,state FROM revora_arena_matches WHERE id=@id FOR UPDATE","id",group.ParticipationKey))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetInt64(0)!=first || r.GetInt64(1)!=second || r.GetInt32(2)!=map || r.GetString(3)!="active")
                            throw new InvalidOperationException("Arena start identity differs.");
                    }
                    if(ArenaStartupRecovery.Abandoned(c,t,group.ParticipationKey))return false;
                    if(!Owns(group) || !group.StartedOnOwnedMap || group.Player1.Entity.UID!=first || group.Player2.Entity.UID!=second || group.Player1.Map.ID!=map)
                        throw new InvalidOperationException("Arena start context changed.");
                    ArenaRankingSettlement.EnsureOpen(c,t,MagnoliaQuest.Day(now));
                    return true;
        }
        // Caller must hold the match lock and commit counters/rewards in this same transaction.
        // No client packet, invitation cancellation or exported group can authorize this record.
        internal static bool Finish(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(group==null || !Monitor.IsEntered(group.ParticipationSync) || t==null || t.Connection!=c)
                throw new InvalidOperationException("Arena result requires its owned transaction.");
            if(!Owns(group) || !group.Done || group.Winner==null || group.Loser==null || ReferenceEquals(group.Winner,group.Loser)
                || !(ReferenceEquals(group.Winner,group.Player1) && ReferenceEquals(group.Loser,group.Player2)
                || ReferenceEquals(group.Winner,group.Player2) && ReferenceEquals(group.Loser,group.Player1)))return false;
            uint first=group.Player1.Entity.UID,second=group.Player2.Entity.UID,winner=group.Winner.Entity.UID,loser=group.Loser.Entity.UID;
            ushort map=group.Player1.Map.ID;var now=Now();
            using(var q=Q.Command(c,t,"SELECT player1,player2,map,state,started_at AT TIME ZONE 'UTC' FROM revora_arena_matches WHERE id=@id FOR UPDATE","id",group.ParticipationKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return false;
                if(r.GetInt64(0)!=first || r.GetInt64(1)!=second || r.GetInt32(2)!=map)throw new InvalidOperationException("Arena result identity differs.");
                if(r.GetString(3)!="active" || now<DateTime.SpecifyKind(r.GetDateTime(4),DateTimeKind.Utc))return false;
            }
            if(ArenaStartupRecovery.Abandoned(c,t,group.ParticipationKey))return false;
            now=ArenaResultIntents.Read(c,t,group)??now;
            ArenaRankingSettlement.EnsureOpen(c,t,MagnoliaQuest.Day(now));
            if(Q.Execute(c,t,@"UPDATE revora_arena_matches SET state='finished',winner=@winner,loser=@loser,ended_at=@now,day=@day
WHERE id=@id AND state='active'","winner",(long)winner,"loser",(long)loser,"now",now,"day",MagnoliaQuest.Day(now),"id",group.ParticipationKey)!=1)
                throw new InvalidOperationException("Arena result was not saved.");
            if(!Owns(group) || !group.Done || group.Winner.Entity.UID!=winner || group.Loser.Entity.UID!=loser)
                throw new InvalidOperationException("Arena result context changed.");
            return true;
        }
    }
}
