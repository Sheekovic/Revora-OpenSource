// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures;

namespace Revora.Database
{
    public unsafe class TeamArenaTable
    {
        // Recover a durable identity before login attempts to create a new statistic row.
        internal static TeamQualifierStatistic RecoverExisting(uint uid)
        {
            if (uid == 0) return null;
            TeamQualifierStatistic cached;
            if (Game.TeamQualifier.ArenaStatistics.TryGetValue(uid, out cached) && cached != null && cached.EntityID == uid && !Game.Features.ArenaCacheRecovery.RequiresReload(cached)) return cached;
            var saved = ReadExisting(uid);
            if (saved == null) return null;
            return Game.TeamQualifier.ArenaStatistics.AddOrUpdate(uid, saved,
                (key, prior) => prior != null && prior.EntityID == uid && !Game.Features.ArenaCacheRecovery.RequiresReload(prior) ? prior : saved);
        }
        internal static TeamQualifierStatistic ReadExisting(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Game.Features.QuestInventory.Command(c, null,
                    "SELECT * FROM teamarena WHERE entityid=@uid", "uid", (long)uid))
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    Func<string, uint> number = name => r[name] == DBNull.Value ? 0 : Convert.ToUInt32(r[name]);
                    var stat = new TeamQualifierStatistic(true) { EntityID = number("entityid"),
                        Name = r["entityname"] == DBNull.Value ? "" : Convert.ToString(r["entityname"]),
                        LastSeasonRank = number("lastseasonrank"), LastSeasonArenaPoints = number("lastseasonarenapoints"),
                        ArenaPoints = number("arenapoints"), TodayWin = number("todaywin"), TodayBattles = number("todaybattles"),
                        LastSeasonWin = number("lastseasonwin"), LastSeasonLose = number("lastseasonlose"),
                        TotalWin = number("totalwin"), TotalLose = number("totallose"), HistoryHonor = number("historyhonor"),
                        CurrentHonor = number("currenthonor"), Level = checked((byte)number("level")),
                        Class = checked((byte)number("class")), Model = number("model"),
                        Status = TeamQualifierStatistic.NotSignedUp };
                    if (r.Read()) throw new InvalidOperationException("Duplicate Team Arena identity.");
                    return stat;
                }
            }
        }
        public static void Load()
        {
            Console.WriteLine("Loading TeamArena information!");
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("teamarena"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    TeamQualifierStatistic stat = new TeamQualifierStatistic(true);
                    stat.EntityID = reader.ReadUInt32("EntityID");
                    stat.Name = reader.ReadString("EntityName");
                    stat.LastSeasonRank = reader.ReadUInt32("LastSeasonRank");
                    stat.LastSeasonArenaPoints = reader.ReadUInt32("LastSeasonArenaPoints");
                    stat.ArenaPoints = reader.ReadUInt32("ArenaPoints");
                    stat.TodayWin = reader.ReadUInt32("TodayWin");
                    stat.TodayBattles = reader.ReadUInt32("TodayBattles");
                    stat.LastSeasonWin = reader.ReadUInt32("LastSeasonWin");
                    stat.LastSeasonLose = reader.ReadUInt32("LastSeasonLose");
                    stat.TotalWin = reader.ReadUInt32("TotalWin");
                    stat.TotalLose = reader.ReadUInt32("TotalLose");
                    stat.HistoryHonor = reader.ReadUInt32("HistoryHonor");
                    stat.CurrentHonor = reader.ReadUInt32("CurrentHonor");
                    stat.Level = reader.ReadByte("Level");
                    stat.Class = reader.ReadByte("Class");
                    stat.Model = reader.ReadUInt32("Model");
                    TeamQualifier.ArenaStatistics.Add(stat.EntityID, stat);
                }
            }
            TeamQualifier.Sort();
            TeamQualifier.YesterdaySort();
            //Console.WriteLine("Done!");
        }
        public static uint ArenaPointFill(byte level)
        {
            if (level >= 70 && level < 100) return 1000;
            else if (level >= 100 && level < 110) return 2000;
            else if (level >= 110 && level < 120) return 3000;
            else if (level >= 120) return 4000;
            return 0;
        }
        private static object SaveOwner(TeamQualifierStatistic stats) {
            Client.GameState player;
            return Kernel.GamePool.TryGetValue(stats.EntityID,out player) ? player.ItemSyncRoot ?? player : (object)stats;
        }
        private static bool CanSave(TeamQualifierStatistic stats,object owner) {
            if (Game.Features.ArenaCacheRecovery.RequiresReload(stats)) return false;
            Client.GameState player;
            if(!Kernel.GamePool.TryGetValue(stats.EntityID,out player))return ReferenceEquals(owner,stats);
            return ReferenceEquals(owner,player.ItemSyncRoot ?? player) && !player.Entity.PersistenceRequiresReload
                && ReferenceEquals(player.TeamArenaStatistic,stats);
        }
        public static void SaveArenaStatistics(TeamQualifierStatistic stats) {
            if(stats==null || stats.EntityID==0)return;
            object owner=SaveOwner(stats);
            lock(owner) {
                if(!CanSave(stats,owner))return;
                using(var conn=DataHolder.MySqlConnection){conn.Open();SaveArenaStatisticsCore(stats,conn);}
            }
        }
        public static void SaveArenaStatistics(TeamQualifierStatistic stats,Npgsql.NpgsqlConnection conn) {
            if(stats==null || stats.EntityID==0)return;
            object owner=SaveOwner(stats);
            lock(owner){if(CanSave(stats,owner))SaveArenaStatisticsCore(stats,conn);}
        }
        private static void SaveArenaStatisticsCore(TeamQualifierStatistic stats,Npgsql.NpgsqlConnection conn) {
            bool managed = Game.Features.ArenaDayRollover.Managed(stats.EntityID, conn);
            bool teamManaged = Game.Features.TeamArenaResultStatistics.Managed(stats.EntityID, conn);
            Client.GameState canonical;
            if (managed && (!Kernel.GamePool.TryGetValue(stats.EntityID, out canonical)
                || !ReferenceEquals(canonical.TeamArenaStatistic, stats))) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("teamarena"))
            {
                cmd.Set("Level", stats.Level).Set("Class", stats.Class).Set("EntityName", stats.Name).Set("Model", stats.Model);
                // After a durable team result, only identity metadata belongs to legacy saves.
                if (!teamManaged)
                    cmd.Set("LastSeasonRank", stats.LastSeasonRank)
                        .Set("TodayWin", stats.TodayWin).Set("TodayBattles", stats.TodayBattles)
                        .Set("LastSeasonWin", stats.LastSeasonWin).Set("LastSeasonLose", stats.LastSeasonLose)
                        .Set("TotalWin", stats.TotalWin).Set("TotalLose", stats.TotalLose)
                        .Set("LastSeasonArenaPoints", stats.LastSeasonArenaPoints);
                if (!managed)
                    cmd.Set("ArenaPoints", stats.ArenaPoints).Set("HistoryHonor", stats.HistoryHonor).Set("CurrentHonor", stats.CurrentHonor);
                cmd.Where("EntityID", stats.EntityID).Execute(conn);
            }
        }
        public static void InsertArenaStatistic(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("teamarena"))
                cmd.Insert("ArenaPoints", client.ArenaPoints)
                .Insert("Level", client.TeamArenaStatistic.Level)
                .Insert("EntityName", client.TeamArenaStatistic.Name)
                .Insert("Class", client.TeamArenaStatistic.Class)
                .Insert("Model", client.TeamArenaStatistic.Model)
                .Insert("EntityID", client.TeamArenaStatistic.EntityID).Execute();
        }
        public static void Reset(TeamQualifierStatistic stat)
        {
            if (stat == null) return;
            lock (SaveOwner(stat))
            {
                if (Game.Features.ArenaCacheRecovery.RequiresReload(stat) || Game.Features.TeamArenaResultStatistics.Managed(stat.EntityID)) return;
                ResetCore(stat);
            }
        }
        private static void ResetCore(TeamQualifierStatistic stat)
        {
            stat.LastSeasonArenaPoints = stat.ArenaPoints;
            stat.LastSeasonWin = stat.TodayWin;
            stat.LastSeasonLose = stat.TodayBattles - stat.TodayWin;
            stat.LastSeasonRank = stat.Rank;
            stat.TodayWin = 0;
            stat.TodayBattles = 0;
            stat.Rank = 0;
        }
    }
}