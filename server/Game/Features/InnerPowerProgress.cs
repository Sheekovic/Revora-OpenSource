using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    // The file remains the legacy baseline. This ledger owns all subsequent
    // unlocks/levels and cumulative spending; award ledgers recover before spending.
    internal static class InnerPowerProgress
    {
        internal static Action<GameState> AfterCommit;
        static InnerPowerProgress() { AfterCommit = null; }

        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_inner_progress (
entityid bigint PRIMARY KEY, revision bigint NOT NULL CHECK(revision>0),
spent bigint NOT NULL CHECK(spent>=0), state text NOT NULL)");
            }
        }

        static string Encode(InnerPower inner)
        {
            return string.Join(",", inner.Stages.SelectMany(s => s.NeiGongs)
                .Select(g => g.ID + ":" + (g.Unlocked ? 1 : 0) + ":" + g.level));
        }

        static InnerPower.Stage[] Decode(string state)
        {
            var entries = state.Split(','); int index = 0;
            var stages = InnerPowerTable.Stages.Select(s => new InnerPower.Stage {
                ID = s.ID, NeiGongs = s.NeiGongAtributes.Select(g => {
                    if (index >= entries.Length) throw new InvalidOperationException("Incomplete Inner Power state.");
                    string[] fields = entries[index++].Split(':');
                    if (fields.Length != 3 || byte.Parse(fields[0]) != g.ID || (fields[1] != "0" && fields[1] != "1"))
                        throw new InvalidOperationException("Invalid Inner Power state.");
                    byte level = byte.Parse(fields[2]); bool unlocked = fields[1] == "1";
                    if (g.MaxLevel == 0 || level > g.MaxLevel || (!unlocked && level != 0))
                        throw new InvalidOperationException("Invalid Inner Power level.");
                    return new InnerPower.Stage.NeiGong { ID = g.ID, Unlocked = unlocked, level = level,
                        Score = (byte)((100 * level + g.MaxLevel - 1) / g.MaxLevel), Complete = level == g.MaxLevel };
                }).ToArray()
            }).ToArray();
            if (index != entries.Length) throw new InvalidOperationException("Unexpected Inner Power state.");
            foreach (var stage in stages) stage.UnLocked = stage.NeiGongs.Any(g => g.Unlocked);
            return stages;
        }

        // Call only after recovering both cumulative award sources.
        internal static void RecoverPlayer(InnerPower inner)
        {
            lock (inner.SyncRoot)
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = QuestInventory.Command(c, null,
                    "SELECT revision,spent,state FROM revora_inner_progress WHERE entityid=@uid", "uid", (long)inner.UID))
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        if (inner.AppliedTrainingPoints != 0 || inner.ProgressRevision != 0)
                            throw new InvalidOperationException("Missing Inner Power progress ledger.");
                        return;
                    }
                    long revision = r.GetInt64(0), spent = r.GetInt64(1);
                    long pending = checked(spent - inner.AppliedTrainingPoints);
                    if (pending < 0 || pending > inner.Potency || revision < inner.ProgressRevision)
                        throw new InvalidOperationException("Inner Power spending checkpoint mismatch.");
                    var stages = Decode(r.GetString(2));
                    inner.Stages = stages; inner.Potency -= (int)pending;
                    inner.AppliedTrainingPoints = spent; inner.ProgressRevision = revision;
                    inner.UpdateStatus(); InnerPower.InnerPowerRank.UpdateRank(inner);
                }
            }
        }

        internal static void RecoverAll()
        {
            // Include committed unlocks missing from the last legacy file save.
            // Unmigrated characters need no new per-character startup queries.
            var migrated = new System.Collections.Generic.List<InnerPower>();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = QuestInventory.Command(c, null,
                    "SELECT p.entityid,e.name FROM revora_inner_progress p JOIN entities e ON e.uid=p.entityid"))
                using (var r = q.ExecuteReader())
                    while (r.Read())
                    {
                        uint uid = checked((uint)r.GetInt64(0));
                        if (!InnerPower.InnerPowerPolle.ContainsKey(uid)) new InnerPower(r.GetString(1), uid);
                        migrated.Add(InnerPower.InnerPowerPolle[uid]);
                    }
            }
            foreach (var inner in migrated)
            {
                PotencyPacks.RecoverPlayer(inner);
                InnerPowerPurchases.RecoverPlayer(inner);
            }
        }

        static bool Ready(GameState p)
        {
            return QuestInventory.Player(p) && p.InnerPower != null && p.InnerPower.UID == p.Entity.UID
                && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }

        static void CheckAwards(NpgsqlConnection c, NpgsqlTransaction tx, InnerPower inner)
        {
            foreach (bool pack in new[] { true, false })
            {
                QuestInventory.Execute(c, tx, pack
                    ? "INSERT INTO revora_potency_awards(entityid,points) VALUES(@uid,0) ON CONFLICT(entityid) DO NOTHING"
                    : "INSERT INTO revora_inner_purchases(entityid,points) VALUES(@uid,0) ON CONFLICT(entityid) DO NOTHING", "uid", (long)inner.UID);
                using (var q = QuestInventory.Command(c, tx, pack
                    ? "SELECT points FROM revora_potency_awards WHERE entityid=@uid FOR UPDATE"
                    : "SELECT points FROM revora_inner_purchases WHERE entityid=@uid FOR UPDATE", "uid", (long)inner.UID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != (pack ? inner.AppliedPackPoints : inner.AppliedNpcPoints))
                        throw new InvalidOperationException("Inner Power awards changed.");
            }
        }

        internal static void Process(GameState p, byte action, uint id)
        {
            if ((action != 3 && action != 4) || id > byte.MaxValue || !Ready(p)) return;
            lock (p.ItemSyncRoot)
            lock (p.InnerPower.SyncRoot)
            {
                if (!Ready(p)) return;
                InnerPowerTable.Stage dbStage; InnerPowerTable.Stage.NeiGong dbGong;
                InnerPower.Stage stage; InnerPower.Stage.NeiGong gong;
                if (!InnerPowerTable.GetDBInfo(id, out dbStage, out dbGong)
                    || !p.InnerPower.TryGetStageAndGong((byte)id, out stage, out gong)
                    || !dbGong.CheckAccount(p.Entity.Reborn, p.Entity.Level)) return;
                ConquerItem book = null; int cost = 0;
                if (action == 4)
                {
                    if (gong.Unlocked || !p.InnerPower.isUnlockedNeiGong((byte)id)) return;
                    book = p.Inventory.Objects.FirstOrDefault(i => i.ID == dbGong.ItemID && i.Position == 0 && i.Warehouse == 0);
                    if (book == null) return;
                }
                else
                {
                    if (!stage.UnLocked || !gong.Unlocked || gong.level >= dbGong.MaxLevel
                        || dbGong.ProgressNeiGongValue.Length == 0) return;
                    uint price = dbGong.ProgressNeiGongValue[Math.Min(gong.level, dbGong.ProgressNeiGongValue.Length - 1)];
                    if (price > int.MaxValue || p.InnerPower.Potency < price) return;
                    cost = (int)price;
                }
                try
                {
                    var inner = p.InnerPower;
                    var nextStages = Decode(Encode(inner));
                    var nextGong = nextStages.SelectMany(s => s.NeiGongs).Single(g => g.ID == id);
                    nextGong.Unlocked = true;
                    if (action == 3) nextGong.level++;
                    string nextState = string.Join(",", nextStages.SelectMany(s => s.NeiGongs)
                        .Select(g => g.ID + ":" + (g.Unlocked ? 1 : 0) + ":" + g.level));
                    nextStages = Decode(nextState);
                    long nextSpent = checked(inner.AppliedTrainingPoints + cost), revision = checked(inner.ProgressRevision + 1);
                    ushort remaining = book == null ? (ushort)0 : (ushort)(Math.Max(1, (int)book.StackSize) - 1);
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var tx = c.BeginTransaction())
                        {
                            CheckAwards(c, tx, inner);
                            int changed = inner.ProgressRevision == 0
                                ? QuestInventory.Execute(c, tx, "INSERT INTO revora_inner_progress(entityid,revision,spent,state) VALUES(@uid,@revision,@spent,@state) ON CONFLICT(entityid) DO NOTHING",
                                    "uid", (long)inner.UID, "revision", revision, "spent", nextSpent, "state", nextState)
                                : QuestInventory.Execute(c, tx, "UPDATE revora_inner_progress SET revision=@revision,spent=@spent,state=@state WHERE entityid=@uid AND revision=@before AND spent=@old",
                                    "uid", (long)inner.UID, "revision", revision, "spent", nextSpent, "state", nextState, "before", inner.ProgressRevision, "old", inner.AppliedTrainingPoints);
                            if (changed != 1) throw new InvalidOperationException("Inner Power progression changed.");
                            if (book != null && QuestInventory.Execute(c, tx, remaining == 0
                                ? "DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound"
                                : "UPDATE items SET stacksize=@remaining WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound",
                                "item", (long)book.UID, "uid", (long)inner.UID, "type", (int)book.ID, "stack", (int)book.StackSize, "bound", book.Bound ? 1 : 0, "remaining", (int)remaining) != 1)
                                throw new InvalidOperationException("Inner Power book ownership changed.");
                            tx.Commit();
                        }
                    }
                    if (AfterCommit != null) AfterCommit(p);
                    inner.Stages = nextStages; inner.Potency -= cost;
                    inner.AppliedTrainingPoints = nextSpent; inner.ProgressRevision = revision;
                    if (book != null)
                    {
                        if (remaining == 0) QuestInventory.Install(p, new[] { book.UID }, new ConquerItem[0]);
                        else { book.StackSize = remaining; book.Mode = Enums.ItemMode.Update; book.Send(p); book.Mode = Enums.ItemMode.Default; }
                    }
                    inner.UpdateStatus(); inner.AddPotency(p, 0); p.LoadItemStats();
                    InnerPower.InnerPowerRank.UpdateRank(inner);
                    var updated = inner.Stages.Single(s => s.ID == stage.ID);
                    p.Send(new InnerPowerGui(inner.GetNeiGongs()).ToArray());
                    p.Send(new InnerPowerStage(InnerPowerStage.ActionID.UpdateStage, inner.UID, updated).ToArray());
                    p.Send(new InnerPowerStage(InnerPowerStage.ActionID.UpdateScore, inner.UID, updated).ToArray());
                    PrestigeRanking.SendSelf(p); QuestInventory.Refresh(p);
                }
                catch (Exception ex) { QuestInventory.Fail(p, "inner-progress", ex); }
            }
        }
    }
}
