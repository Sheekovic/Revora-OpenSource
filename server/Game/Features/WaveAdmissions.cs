using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class WaveAdmissions
    {
        internal static bool HasPendingReturn(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = QuestInventory.Command(c, null,
                    "SELECT EXISTS(SELECT 1 FROM revora_wave_admissions WHERE uid=@uid AND NOT returned)", "uid", (long)uid))
                    return Convert.ToBoolean(q.ExecuteScalar());
            }
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_wave_admissions(
cycle varchar(36) NOT NULL,uid bigint NOT NULL,arena integer NOT NULL,pkmode integer NOT NULL,
ended boolean NOT NULL DEFAULT false,returned boolean NOT NULL DEFAULT false,
PRIMARY KEY(cycle,uid));
CREATE INDEX IF NOT EXISTS revora_wave_return_owner ON revora_wave_admissions(uid) WHERE NOT returned;");
            }
        }
        internal static void RecoverInterrupted()
        {
            // A process restart forfeits unfinished matches. Committed rankings
            // retain their prizes. Never infer winners from saved map position.
            using (var c = DataHolder.MySqlConnection)
            { c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_wave_admissions SET ended=true WHERE NOT ended"); }
        }
        internal static void Begin(string cycle, ushort arena, GameState[] players)
        {
            if (players.Length < 2 || players.Length > WaveDefenseSystem.MaxPlayers
                || players.Any(p => !QuestInventory.Player(p))
                || players.Select(p => p.Entity.UID).Distinct().Count() != players.Length)
                throw new InvalidOperationException("Wave Defense admission roster is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    QuestInventory.Execute(c, t, "SET LOCAL lock_timeout='1s'");
                    foreach (var p in players.OrderBy(p => p.Entity.UID))
                    {
                        using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_wave_admissions WHERE uid=@uid AND cycle<>@cycle AND NOT returned",
                            "uid", (long)p.Entity.UID, "cycle", cycle))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Wave Defense previous return is pending.");
                        QuestInventory.Execute(c, t, @"INSERT INTO revora_wave_admissions(cycle,uid,arena,pkmode)
VALUES(@cycle,@uid,@arena,@mode) ON CONFLICT(cycle,uid) DO NOTHING",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode);
                        using (var q = QuestInventory.Command(c, t, @"SELECT count(*) FROM revora_wave_admissions
WHERE cycle=@cycle AND uid=@uid AND arena=@arena AND pkmode=@mode AND NOT ended AND NOT returned",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 1) throw new InvalidOperationException("Wave Defense admission changed.");
                    }
                    using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_wave_admissions WHERE cycle=@cycle", "cycle", cycle))
                        if (Convert.ToInt32(q.ExecuteScalar()) != players.Length) throw new InvalidOperationException("Wave Defense roster changed.");
                    t.Commit();
                }
            }
        }
        internal static void End(string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_wave_admissions SET ended=true WHERE cycle=@cycle AND NOT ended", "cycle", cycle);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_wave_admissions WHERE cycle=@cycle AND NOT ended", "cycle", cycle))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Wave Defense end was not saved.");
            }
        }
        internal static bool Returned(string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = QuestInventory.Command(c, null,
                    "SELECT NOT EXISTS(SELECT 1 FROM revora_wave_admissions WHERE cycle=@cycle AND NOT returned)", "cycle", cycle))
                    return Equals(q.ExecuteScalar(), true);
            }
        }
        internal static void Forfeit(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_wave_admissions SET ended=true WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_wave_admissions WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Wave Defense forfeit was not saved.");
            }
        }
        internal static bool Return(GameState p)
        {
            if (!QuestInventory.Player(p, false) || !Monitor.TryEnter(p.ItemSyncRoot)) return false;
            bool virtue = false, pk = false;
            try
            {
                virtue = Monitor.TryEnter(p.VirtueSyncRoot);
                if (!virtue) return false;
                pk = Monitor.TryEnter(p.PkSyncRoot);
                if (!pk) return false;
                int? mode = null;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = QuestInventory.Command(c, null, @"SELECT pkmode FROM revora_wave_admissions
WHERE uid=@uid AND ended AND NOT returned ORDER BY cycle DESC LIMIT 1", "uid", (long)p.Entity.UID))
                    { object value = q.ExecuteScalar(); if (value != null) mode = Convert.ToInt32(value); }
                }
                if (!mode.HasValue) return true;
                if (mode.Value < 0 || mode.Value > byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode), (byte)mode.Value))
                    throw new InvalidOperationException("Wave Defense return mode is invalid.");
                if (!QuestInventory.Player(p, false)) return false;
                p.Entity.Teleport(1002, WaveDefenseSystem.TwinCityX, WaveDefenseSystem.TwinCityY);
                if (p.Entity.MapID != 1002) throw new InvalidOperationException("Wave Defense return did not reach town.");
                p.Entity.PKMode = (Enums.PKMode)mode.Value;
                p.Send(new Data(true) { UID = p.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)p.Entity.PKMode });
                if (!EntityTable.SaveEntity(p)) throw new InvalidOperationException("Wave Defense returned character was not saved.");
                // Save destination first. A failed acknowledgement retries a safe
                // return on reconnect; it cannot consume a prize or strand a player.
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    QuestInventory.Execute(c, null, "UPDATE revora_wave_admissions SET returned=true WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID);
                    using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_wave_admissions WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Wave Defense return acknowledgement failed.");
                }
                return true;
            }
            catch (Exception ex) { QuestInventory.Fail(p, "wave-return", ex); return false; }
            finally
            {
                if (pk) Monitor.Exit(p.PkSyncRoot);
                if (virtue) Monitor.Exit(p.VirtueSyncRoot);
                Monitor.Exit(p.ItemSyncRoot);
            }
        }
    }
}
