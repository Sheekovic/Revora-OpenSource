using System;
using System.Threading;
using System.Linq;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Stored before the result bundle so a failed bundle cannot erase the observed outcome.
    internal static class ArenaResultIntents
    {
        internal enum Cause { Result, Defeat, Timeout, Forfeit, Disconnect }
        internal static bool Initialized {get;private set;}
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_result_intents(
run uuid PRIMARY KEY REFERENCES revora_arena_matches(id),winner bigint NOT NULL,loser bigint NOT NULL CHECK(loser<>winner),
cause varchar(12) NOT NULL CHECK(cause IN('Result','Defeat','Timeout','Forfeit','Disconnect')),
observed_at timestamptz NOT NULL,day varchar(8) NOT NULL);
CREATE TABLE IF NOT EXISTS revora_arena_outcome_snapshots(
run uuid NOT NULL REFERENCES revora_arena_result_intents(run),uid bigint NOT NULL,statistics bigint[] NOT NULL CHECK(cardinality(statistics)=5),
vip integer NOT NULL CHECK(vip BETWEEN 0 AND 255),minimum_vip integer NOT NULL CHECK(minimum_vip BETWEEN 0 AND 255),losses_enabled boolean NOT NULL,
cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),silver numeric(20,0) NOT NULL CHECK(silver BETWEEN 0 AND 18446744073709551615),
rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),PRIMARY KEY(run,uid));
ALTER TABLE revora_arena_outcome_snapshots ADD COLUMN IF NOT EXISTS opponent_class integer CHECK(opponent_class BETWEEN 0 AND 255);
CREATE INDEX IF NOT EXISTS revora_arena_snapshot_owner ON revora_arena_outcome_snapshots(uid);");}Initialized=true;
        }
        private static void Owned(Group group,bool fenced=false) {
            if(!Initialized || group==null || !Monitor.IsEntered(group.ParticipationSync) || !group.Done || !group.OwnsImportedMap
                || group.Winner==null || group.Loser==null || ReferenceEquals(group.Winner,group.Loser)
                || !(ReferenceEquals(group.Winner,group.Player1) && ReferenceEquals(group.Loser,group.Player2)
                    || ReferenceEquals(group.Winner,group.Player2) && ReferenceEquals(group.Loser,group.Player1)))throw new InvalidOperationException("Arena intent requires an owned outcome.");
            foreach(var p in new[]{group.Player1,group.Player2}) {
                Client.GameState canonical;
                bool ready=fenced?p!=null && p.Entity!=null && (!p.Fake || ArenaActors.IsBot(p)) && p.Entity.FullyLoaded && p.Action==0 && p.Entity.PersistenceRequiresReload
                    && Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) && ReferenceEquals(canonical,p)
                    && ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) && ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic):ArenaActors.Player(p,false);
                if(!ready || p.ItemSyncRoot==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Arena intent requires canonical locked participants.");
            }
        }
        internal static bool Capture(Group group,Cause cause) {return CaptureAt(group,cause,ArenaMatchLedger.Clock());}
        internal static bool CaptureAt(Group group,Cause cause,DateTime now) {return CaptureCore(group,cause,now,false);}
        internal static bool CaptureFenced(Group group) {
            Owned(group,true);
            var observed=Volatile.Read(ref group.PendingDeparture);
            if(observed==null || !ReferenceEquals(observed.Player,group.Loser))return false;
            return CaptureCore(group,observed.Cause,observed.Observed,true);
        }
        private static bool CaptureCore(Group group,Cause cause,DateTime now,bool fenced) {
            Owned(group,fenced);if(!Enum.IsDefined(typeof(Cause),cause))throw new ArgumentOutOfRangeException("cause");
            if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena intent requires UTC.");
            uint winner=group.Winner.Entity.UID,loser=group.Loser.Entity.UID;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string state;
                using(var q=Q.Command(c,t,"SELECT player1,player2,map,state,started_at AT TIME ZONE 'UTC' FROM revora_arena_matches WHERE id=@run FOR UPDATE","run",group.ParticipationKey))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read())return false;state=r.GetString(3);
                    if(r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID || r.GetInt32(2)!=group.Player1.Map.ID
                        || (r.GetString(3)!="active" && r.GetString(3)!="finished") || now<DateTime.SpecifyKind(r.GetDateTime(4),DateTimeKind.Utc))throw new InvalidOperationException("Arena intent admission changed.");
                }
                if(ArenaStartupRecovery.Abandoned(c,t,group.ParticipationKey))return false;
                if(state=="finished")return Read(c,t,group).HasValue;
                int inserted=Q.Execute(c,t,@"INSERT INTO revora_arena_result_intents(run,winner,loser,cause,observed_at,day)
VALUES(@run,@winner,@loser,@cause,@now,@day) ON CONFLICT(run) DO NOTHING","run",group.ParticipationKey,"winner",(long)winner,"loser",(long)loser,
                    "cause",cause.ToString(),"now",now,"day",MagnoliaQuest.Day(now));
                if(!Read(c,t,group).HasValue)throw new InvalidOperationException("Arena intent was not saved.");
                if(inserted==1)CaptureSnapshots(c,t,group,fenced);
                foreach(var p in new[]{group.Player1,group.Player2})if(SnapshotFor(c,t,group.ParticipationKey,p.Entity.UID)==null)throw new InvalidOperationException("Arena outcome snapshot missing.");
                Owned(group,fenced);if(group.Winner.Entity.UID!=winner || group.Loser.Entity.UID!=loser)throw new InvalidOperationException("Arena intent outcome changed.");t.Commit();return true;
            }}
        }
        internal sealed class Snapshot {
            internal long[] Statistics;internal byte? OpponentClass;internal byte Vip,Minimum;internal bool LossesEnabled;internal uint Cp,Rvp;internal ulong Silver;
        }
        internal static long[] Statistics(Revora.Network.GamePackets.QualifierStatistic s){return new long[]{s.ArenaPoints,s.TodayWin,s.TodayBattles,s.TotalWin,s.TotalLose};}
        private static void CaptureSnapshots(NpgsqlConnection c,NpgsqlTransaction t,Group group,bool fenced) {
            uint cp=Config.ArenaHundredWinsCp,rvp=Config.ArenaHundredWinsRevoraPoints;ulong silver=Config.ArenaHundredWinsGold;
            byte minimum=Config.ArenaHundredWinsMinVip;bool losses=Config.ArenaFiftyLossesPenaltyEnabled;
            foreach(var p in new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID)) {
                if(!fenced)Database.SkillTable.LockOwner(c,t,p);
                else using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Arena fenced capture owner missing.");
                if(TeamArenaResultStatistics.Pending(c,t,p.Entity.UID))throw new InvalidOperationException("Pending team result owns Arena capture preimages.");
                if(p.ArenaStatistic==null || p.ArenaStatistic.EntityID!=p.Entity.UID)throw new InvalidOperationException("Arena outcome statistics missing.");
                var stats=Statistics(p.ArenaStatistic);byte vip=p.Entity.VIPLevel;
                byte opponentClass=ReferenceEquals(p,group.Player1)?group.Player2.Entity.Class:group.Player1.Entity.Class;
                using(var q=Q.Command(c,t,@"SELECT a.arenapoints,a.todaywin,a.todaybattles,a.totalwin,a.totallose,COALESCE(e.viplevel,0)
FROM arena a JOIN entities e ON e.uid=a.entityid WHERE a.entityid=@uid FOR UPDATE OF a","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read() || Enumerable.Range(0,5).Any(n=>Convert.ToInt64(r[n])!=stats[n]) || !VipState.MatchesSavedLevel(p,Convert.ToInt64(r[5])) || r.Read())throw new InvalidOperationException("Arena outcome preimage requires reload.");
                }
                if(Q.Execute(c,t,@"INSERT INTO revora_arena_outcome_snapshots(run,uid,statistics,vip,minimum_vip,losses_enabled,cp,silver,rvp,opponent_class)
VALUES(@run,@uid,@stats,@vip,@minimum,@losses,@cp,@silver,@rvp,@opponent)","run",group.ParticipationKey,"uid",(long)p.Entity.UID,"stats",stats,"vip",(int)vip,"minimum",(int)minimum,
                    "opponent",(int)opponentClass,"losses",losses,"cp",(long)cp,"silver",(decimal)silver,"rvp",(long)rvp)!=1)throw new InvalidOperationException("Arena outcome snapshot was not saved.");
            }
        }
        internal static Snapshot SnapshotFor(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint uid) {
            if(!Initialized)return null;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_result_intents WHERE run=@run)","run",run))if(!Equals(q.ExecuteScalar(),true))return null;
            using(var q=Q.Command(c,t,"SELECT statistics,vip,minimum_vip,losses_enabled,cp,silver,rvp,opponent_class FROM revora_arena_outcome_snapshots WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Arena captured outcome lacks its snapshot.");
                return new Snapshot{Statistics=(long[])r[0],Vip=Convert.ToByte(r[1]),Minimum=Convert.ToByte(r[2]),LossesEnabled=r.GetBoolean(3),
                    Cp=Convert.ToUInt32(r[4]),Silver=Convert.ToUInt64(r[5]),Rvp=Convert.ToUInt32(r[6]),OpponentClass=r.IsDBNull(7)?(byte?)null:Convert.ToByte(r[7])};
            }
        }
        internal static DateTime? Read(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(!Initialized)return null;
            using(var q=Q.Command(c,t,"SELECT winner,loser,observed_at AT TIME ZONE 'UTC',day FROM revora_arena_result_intents WHERE run=@run FOR UPDATE","run",group.ParticipationKey))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                var stamp=DateTime.SpecifyKind(r.GetDateTime(2),DateTimeKind.Utc);
                if(group.Winner==null || group.Loser==null || r.GetInt64(0)!=group.Winner.Entity.UID || r.GetInt64(1)!=group.Loser.Entity.UID
                    || r.GetString(3)!=MagnoliaQuest.Day(stamp))throw new InvalidOperationException("Saved Arena intent differs.");return stamp;
            }
        }
    }
}
