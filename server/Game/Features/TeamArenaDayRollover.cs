using System;
using System.Linq;
using System.Threading;
using Group=Revora.Game.TeamQualifier.QualifierList.QualifierGroup;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class TeamArenaDayRollover
    {
        private static bool initialized;
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_team_arena_day_rollovers(
uid bigint NOT NULL,day varchar(8) NOT NULL,wins bigint NOT NULL,battles bigint NOT NULL,
rolled_at timestamptz NOT NULL,PRIMARY KEY(uid,day),CHECK(wins>=0 AND battles>=wins));");}initialized=true;
        }
        private static bool Invitation(Client.GameState p,Group group) {
            if(p.TeamArenaStatistic.PlayWith==null)return true;
            return group!=null && Monitor.IsEntered(group.ResultSync) && Monitor.IsEntered(p.ItemSyncRoot)
                && (ReferenceEquals(p,group.Player1)?ReferenceEquals(p.TeamArenaStatistic.PlayWith,group.Player2)
                    :ReferenceEquals(p,group.Player2) && ReferenceEquals(p.TeamArenaStatistic.PlayWith,group.Player1));
        }
        private static bool Ready(Client.GameState p,Group group){return initialized && Q.Player(p)
            && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null && p.TeamArenaStatistic.EntityID==p.Entity.UID
            && p.ArenaStatistic.EntityID==p.Entity.UID && !ArenaCacheRecovery.RequiresReload(p.ArenaStatistic)
            && !ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic) && p.QualifierGroup==null && p.TeamQualifierGroup==null
            && p.WatchingGroup==null && p.TeamWatchingGroup==null && p.ArenaStatistic.PlayWith==null && Invitation(p,group)
            && !Kernel.DisconnectPool.ContainsKey(p.Entity.UID) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static bool Prepare(Client.GameState p) {return PrepareCore(p,null);}
        internal static bool PrepareAdmission(Group group,Client.GameState[] roster) {
            if(group==null || roster==null || !Monitor.IsEntered(group.ResultSync) || group.ImportStarted || group.Done
                || roster.Length<2 || roster.Length>10 || !roster.Contains(group.Player1) || !roster.Contains(group.Player2)
                || roster.Distinct().Count()!=roster.Length || roster.Any(p=>p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Ready(p,group)))return false;
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team admission requires UTC.");
            foreach(var p in roster)if(!PrepareCore(p,group))return false;
            return roster.All(p=>Ready(p,group)) && MagnoliaQuest.Day(now)==MagnoliaQuest.Day(ArenaMatchLedger.Clock());
        }
        private static bool PrepareCore(Client.GameState p,Group group) {
            if(!Ready(p,group))return false;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p,group))return false;
                var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team counter rollover requires UTC.");
                string day=MagnoliaQuest.Day(now);var stats=p.TeamArenaStatistic;
                var before=new[]{stats.ArenaPoints,stats.TodayWin,stats.TodayBattles,stats.TotalWin,stats.TotalLose};uint wins,battles;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Database.SkillTable.LockOwner(c,t,p);
                    if(TeamArenaResultStatistics.Pending(c,t,p.Entity.UID) || ArenaMatchLedger.Pending(c,t,p.Entity.UID))return false;
                    using(var q=Q.Command(c,t,"SELECT max(day) FROM revora_team_arena_day_rollovers WHERE uid=@uid","uid",(long)p.Entity.UID)) {
                        var latest=q.ExecuteScalar() as string;if(latest!=null && string.CompareOrdinal(latest,day)>=0)return latest==day;
                    }
                    using(var q=Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())if(!r.Read() || Enumerable.Range(0,5).Any(i=>Convert.ToUInt32(r[i])!=before[i]) || r.Read())throw new InvalidOperationException("Team counter preimage requires reload.");
                    using(var q=Q.Command(c,t,@"SELECT count(*) FILTER(WHERE s.won),count(*) FROM revora_team_arena_statistics s
JOIN revora_team_arena_results r ON r.id=s.run WHERE s.uid=@uid
AND to_char(r.completed_at AT TIME ZONE 'America/Los_Angeles','YYYYMMDD')=@day
AND r.participants=(SELECT count(*) FROM revora_team_arena_statistics members WHERE members.run=r.id)","uid",(long)p.Entity.UID,"day",day))
                    using(var r=q.ExecuteReader()){r.Read();wins=checked((uint)r.GetInt64(0));battles=checked((uint)r.GetInt64(1));}
                    if(Q.Execute(c,t,"UPDATE teamarena SET todaywin=@wins,todaybattles=@battles WHERE entityid=@uid","wins",(long)wins,"battles",(long)battles,"uid",(long)p.Entity.UID)!=1
                        || Q.Execute(c,t,"INSERT INTO revora_team_arena_day_rollovers(uid,day,wins,battles,rolled_at) VALUES(@uid,@day,@wins,@battles,@now)",
                            "uid",(long)p.Entity.UID,"day",day,"wins",(long)wins,"battles",(long)battles,"now",now)!=1)
                        throw new InvalidOperationException("Team counter rollover was not saved.");
                    if(!Ready(p,group) || !ReferenceEquals(p.TeamArenaStatistic,stats))throw new InvalidOperationException("Team counter owner changed.");
                    if(day!=MagnoliaQuest.Day(ArenaMatchLedger.Clock()))return false;
                    t.Commit();
                }}
                stats.TodayWin=wins;stats.TodayBattles=battles;stats.Send(p);return true;
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"team-arena-rollover",ex);return false;}
        }
    }
}
