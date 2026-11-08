using System;
using System.Threading;
using Npgsql;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Staged solo reservation: committed before transfer, adopted by the match ledger.
    internal static class ArenaAdmissions
    {
        internal static bool Initialized {get;private set;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_admissions(
id uuid PRIMARY KEY,player1 bigint NOT NULL CHECK(player1>0),player2 bigint NOT NULL CHECK(player2>0 AND player2<>player1),
created_at timestamptz NOT NULL,abandoned_at timestamptz);");}Initialized=true;
        }
        internal static bool Pending(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(!Initialized)return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_admissions a
WHERE (a.player1=@uid OR a.player2=@uid) AND a.abandoned_at IS NULL
AND NOT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.id=a.id))","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        internal static void Reserve(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(!Initialized)return;
            if(t==null || t.Connection!=c || !Monitor.IsEntered(group.ParticipationSync)
                || !Monitor.IsEntered(group.Player1.ItemSyncRoot) || !Monitor.IsEntered(group.Player2.ItemSyncRoot)
                || !ArenaActors.Player(group.Player1,false) || !ArenaActors.Player(group.Player2,false))throw new InvalidOperationException("Solo admission requires locked original owners.");
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Solo admission requires UTC.");
            if(Q.Execute(c,t,@"INSERT INTO revora_arena_admissions(id,player1,player2,created_at)
VALUES(@id,@first,@second,@now) ON CONFLICT(id) DO NOTHING","id",group.ParticipationKey,
                "first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID,"now",now)!=1)
                throw new InvalidOperationException("Solo admission was not reserved.");
        }
        internal static void ValidateStart(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(!Initialized)return;
            using(var q=Q.Command(c,t,"SELECT player1,player2,abandoned_at FROM revora_arena_admissions WHERE id=@id FOR UPDATE","id",group.ParticipationKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read()){if(group.DurableAdmissionReserved)throw new InvalidOperationException("Solo reservation missing.");return;}
                if(r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID || !r.IsDBNull(2))
                    throw new InvalidOperationException("Solo reservation ownership changed or was abandoned.");
            }
        }
        internal static void PrepareStartup(int batchLimit) {
            if(!Initialized || batchLimit<1 || batchLimit>100)throw new ArgumentOutOfRangeException("batchLimit");
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0 || Kernel.DisconnectPool.Count!=0
                || Qualifier.ArenaStatistics.Count!=0 || TeamQualifier.ArenaStatistics.Count!=0
                || Qualifier.QualifierList.Groups.Count!=0 || TeamQualifier.QualifierList.Groups.Count!=0)
                throw new InvalidOperationException("Solo admission recovery requires cold startup.");
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Solo admission recovery requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();
                for(int pass=0;pass<batchLimit;pass++)if(Q.Execute(c,null,@"UPDATE revora_arena_admissions SET abandoned_at=@now WHERE id IN(
SELECT a.id FROM revora_arena_admissions a WHERE a.abandoned_at IS NULL
AND NOT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.id=a.id)
ORDER BY a.created_at,a.id LIMIT 128)","now",now)<128)break;
                using(var q=Q.Command(c,null,@"SELECT EXISTS(SELECT 1 FROM revora_arena_admissions a WHERE a.abandoned_at IS NULL
AND NOT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.id=a.id))"))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Solo admission recovery left unfinished reservations.");
            }
        }
        internal static void Cancel(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Solo cancellation requires UTC.");
            using(var q=Q.Command(c,t,@"SELECT player1,player2,abandoned_at FROM revora_arena_admissions WHERE id=@run FOR UPDATE","run",group.ParticipationKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read() || r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID)
                    throw new InvalidOperationException("Solo cancellation reservation differs.");
            }
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_matches WHERE id=@run)","run",group.ParticipationKey))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Adopted solo matches require result recovery.");
            Q.Execute(c,t,"UPDATE revora_arena_admissions SET abandoned_at=@now WHERE id=@run AND abandoned_at IS NULL","now",now,"run",group.ParticipationKey);
            using(var q=Q.Command(c,t,"SELECT abandoned_at IS NOT NULL FROM revora_arena_admissions WHERE id=@run","run",group.ParticipationKey))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Solo cancellation was not saved.");
        }
    }
}
