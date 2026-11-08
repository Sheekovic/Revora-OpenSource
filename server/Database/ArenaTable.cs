// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class ArenaTable
    {
        // A committed admission may outlive a failed cache projection. Reconnect
        // must recover that row instead of inserting another Arena identity.
        internal static QualifierStatistic ReadExisting(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Game.Features.QuestInventory.Command(c, null,
                    "SELECT * FROM arena WHERE entityid=@uid", "uid", (long)uid))
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    Func<string, uint> number = name => r[name] == DBNull.Value ? 0 : Convert.ToUInt32(r[name]);
                    var stat = new QualifierStatistic(true) { EntityID = number("entityid"),
                        Name = r["entityname"] == DBNull.Value ? "" : Convert.ToString(r["entityname"]),
                        LastSeasonRank = number("lastseasonrank"), LastSeasonArenaPoints = number("lastseasonarenapoints"),
                        ArenaPoints = number("arenapoints"), TodayWin = number("todaywin"), TodayBattles = number("todaybattles"),
                        LastSeasonWin = number("lastseasonwin"), LastSeasonLose = number("lastseasonlose"),
                        TotalWin = number("totalwin"), TotalLose = number("totallose"), HistoryHonor = number("historyhonor"),
                        CurrentHonor = number("currenthonor"), Level = checked((byte)number("level")),
                        Class = checked((byte)number("class")), Model = number("model"),
                        LastArenaPointFill = DateTime.FromBinary(r["arenapointfill"] == DBNull.Value ? 0 : Convert.ToInt64(r["arenapointfill"])),
                        Status = QualifierStatistic.NotSignedUp };
                    if (r.Read()) throw new InvalidOperationException("Duplicate Arena identity.");
                    if (!Game.Features.ArenaDayRollover.Initialized && !Game.Features.ArenaDayRollover.Managed(uid) && stat.LastArenaPointFill.Date != DateTime.Now.Date)
                    {
                        stat.LastSeasonArenaPoints = stat.ArenaPoints; stat.LastSeasonWin = stat.TodayWin;
                        stat.LastSeasonLose = stat.TodayBattles >= stat.TodayWin ? stat.TodayBattles - stat.TodayWin : 0;
                        stat.ArenaPoints = ArenaPointFill(stat.Level); stat.LastArenaPointFill = DateTime.Now;
                        stat.TodayWin = 0; stat.TodayBattles = 0;
                    }
                    return stat;
                }
            }
        }
        // Recover a durable identity before login attempts to create a new statistic row.
        internal static QualifierStatistic RecoverExisting(uint uid)
        {
            if (uid == 0) return null;
            QualifierStatistic cached;
            if (Game.Qualifier.ArenaStatistics.TryGetValue(uid, out cached) && cached != null && cached.EntityID == uid && !Game.Features.ArenaCacheRecovery.RequiresReload(cached)) return cached;
            var saved = ReadExisting(uid);
            if (saved == null) return null;
            return Game.Qualifier.ArenaStatistics.AddOrUpdate(uid, saved,
                (key, prior) => prior != null && prior.EntityID == uid && !Game.Features.ArenaCacheRecovery.RequiresReload(prior) ? prior : saved);
        }
        public static void Load()
        {
            Console.WriteLine("Loading Arena information");

            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("arena"))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    Network.GamePackets.QualifierStatistic stat = new Network.GamePackets.QualifierStatistic(true);
                    stat.EntityID = reader.ReadUInt32("EntityID");
                    stat.Name = reader.ReadString("EntityName");
                    stat.LastSeasonRank = reader.ReadUInt32("LastSeasonRank");
                    stat.LastSeasonArenaPoints = reader.ReadUInt32("LastSeasonArenaPoints");
                    stat.ArenaPoints = reader.ReadUInt32("ArenaPoints");
                    stat.TodayWin = reader.ReadUInt32("TodayWin");
                    stat.TodayBattles = reader.ReadUInt32("TodayBattles");
                    stat.LastSeasonWin =reader.ReadUInt32("LastSeasonWin");
                    stat.LastSeasonLose = reader.ReadUInt32("LastSeasonLose");
                    stat.TotalWin = reader.ReadUInt32("TotalWin");
                    stat.TotalLose = reader.ReadUInt32("TotalLose");
                    stat.HistoryHonor = reader.ReadUInt32("HistoryHonor");
                    stat.CurrentHonor = reader.ReadUInt32("CurrentHonor");
                    stat.Level = reader.ReadByte("Level");
                    stat.Class = reader.ReadByte("Class");
                    stat.Model = reader.ReadUInt32("Model");
                    stat.LastArenaPointFill = DateTime.FromBinary(reader.ReadInt64("ArenaPointFill"));

                    if (!Game.Features.ArenaDayRollover.Initialized && !Game.Features.ArenaDayRollover.Managed(stat.EntityID) && DateTime.Now.DayOfYear != stat.LastArenaPointFill.DayOfYear)
                    {
                        stat.LastSeasonArenaPoints = stat.ArenaPoints;
                        stat.LastSeasonWin = stat.TodayWin;
                        stat.LastSeasonLose = stat.TodayBattles - stat.TodayWin;
                        stat.ArenaPoints = ArenaPointFill(stat.Level);
                        stat.LastArenaPointFill = DateTime.Now;
                        stat.TodayWin = 0;
                        stat.TodayBattles = 0;
                    }
                    Game.Qualifier.ArenaStatistics.Add(stat.EntityID, stat);
                }
            }
            Game.Qualifier.Sort();
            Game.Qualifier.YesterdaySort();
            //Console.WriteLine("Done!");
        }
        public static uint ArenaPointFill(byte level)
        {
            if (level >= 70 && level < 100)
                return 1000;
            else if (level >= 100 && level < 110)
                return 2000;
            else if (level >= 110 && level < 120)
                return 3000;
            else if (level >= 120)
                return 4000;
            return 0;
        }
        public static void InsertArenaStatistic(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("arena")
              .Insert("EntityName", client.ArenaStatistic.Name).Insert("ArenaPoints", client.ArenaStatistic.ArenaPoints)
              .Insert("Level", client.ArenaStatistic.Level).Insert("Class", client.ArenaStatistic.Class).Insert("Model", client.ArenaStatistic.Model)
              .Insert("ArenaPointFill", client.ArenaStatistic.LastArenaPointFill.Ticks).Insert("EntityID", client.ArenaStatistic.EntityID))
                cmd.Execute();
        }
        public static void Reset(Client.GameState client, QualifierStatistic stat)
        {
            lock(client==null ? (object)stat : client.ItemSyncRoot) ResetCore(client,stat);
        }
        private static void ResetCore(Client.GameState client, QualifierStatistic stat)
        {
            if (Game.Features.ArenaDayRollover.Managed(stat.EntityID)) return;
            stat.LastSeasonArenaPoints = stat.ArenaPoints;
            stat.LastSeasonWin = stat.TodayWin;
            stat.LastSeasonLose = stat.TodayBattles - stat.TodayWin;
            stat.LastSeasonRank = stat.Rank;
            stat.TodayWin = 0;
            stat.TodayBattles = 0;
            if (stat.Rank != 0)
            {
                if (client == null)
                {
                    stat.CurrentHonor += (1001 - stat.Rank) * 1000;
                    stat.HistoryHonor += (1001 - stat.Rank) * 1000;
                }
                else
                {
                    client.CurrentHonor += (1001 - stat.Rank) * 1000;
                    client.HistoryHonor += (1001 - stat.Rank) * 1000;
                }
            }
            stat.Rank = 0;
            if (client == null)
                stat.ArenaPoints = Database.ArenaTable.ArenaPointFill(stat.Level);
            else
                client.ArenaPoints = Database.ArenaTable.ArenaPointFill(stat.Level);
            stat.LastArenaPointFill = DateTime.Now;
        }
        public static void SaveArenaStatistics(Network.GamePackets.QualifierStatistic stats)
        {
            if (stats == null || stats.EntityID == 0) return;
            object owner = SaveOwner(stats);
            lock (owner)
            {
                if (!CanSave(stats, owner)) return;
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    SaveArenaStatisticsCore(stats, conn);
                }
            }
        }
        private static object SaveOwner(QualifierStatistic stats)
        {
            Client.GameState player;
            return Kernel.GamePool.TryGetValue(stats.EntityID, out player) ? player.ItemSyncRoot ?? player : (object)stats;
        }
        private static bool CanSave(QualifierStatistic stats, object owner)
        {
            if (Game.Features.ArenaCacheRecovery.RequiresReload(stats)) return false;
            Client.GameState player;
            if (!Kernel.GamePool.TryGetValue(stats.EntityID, out player)) return ReferenceEquals(owner, stats);
            return ReferenceEquals(owner, player.ItemSyncRoot ?? player) && !player.Entity.PersistenceRequiresReload
                && ReferenceEquals(player.ArenaStatistic, stats);
        }
        public static void SaveArenaStatistics(Network.GamePackets.QualifierStatistic stats, Npgsql.NpgsqlConnection conn)
        {
            if (stats == null || stats.EntityID == 0) return;
            object owner = SaveOwner(stats);
            lock (owner)
            {
                if (!CanSave(stats, owner)) return;
                SaveArenaStatisticsCore(stats, conn);
            }
        }
        private static void SaveArenaStatisticsCore(QualifierStatistic stats, Npgsql.NpgsqlConnection conn)
        {
            bool managed = Game.Features.ArenaDayRollover.Managed(stats.EntityID, conn);
            Client.GameState canonical;
            if (managed && (!Kernel.GamePool.TryGetValue(stats.EntityID, out canonical)
                || !ReferenceEquals(canonical.ArenaStatistic, stats))) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("arena"))
            {
                cmd.Set("Level", stats.Level).Set("Class", stats.Class).Set("EntityName", stats.Name).Set("Model", stats.Model);
                // Durable result/rollover/payout participants own these fields after migration.
                // A stale final save may refresh identity metadata, never economic state.
                if (!managed)
                    cmd.Set("LastSeasonRank", stats.LastSeasonRank)
                        .Set("ArenaPoints", stats.ArenaPoints).Set("TodayWin", stats.TodayWin)
                        .Set("TodayBattles", stats.TodayBattles).Set("LastSeasonWin", stats.LastSeasonWin)
                        .Set("LastSeasonLose", stats.LastSeasonLose).Set("TotalWin", stats.TotalWin)
                        .Set("TotalLose", stats.TotalLose).Set("HistoryHonor", stats.HistoryHonor)
                        .Set("CurrentHonor", stats.CurrentHonor).Set("ArenaPointFill", stats.LastArenaPointFill.Ticks)
                        .Set("LastSeasonArenaPoints", stats.LastSeasonArenaPoints);
                cmd.Where("EntityID", stats.EntityID).Execute(conn);
            }
        }
    }
}
