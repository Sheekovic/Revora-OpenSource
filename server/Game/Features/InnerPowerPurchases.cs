using System;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    // NPC potency is a separate cumulative ledger from pack awards. The file
    // stores AppliedNpcPoints beside AppliedPackPoints, so neither recovery
    // can treat the other's total as still unapplied.
    internal static class InnerPowerPurchases
    {
        internal const uint SmallPrice = 500;
        internal const int SmallGrant = 2000;
        internal const uint LargePrice = 4000;
        internal const int LargeGrant = 16000;
        internal static Action<GameState> AfterCommit;
        static InnerPowerPurchases() { AfterCommit = null; }

        internal static void Initialize()
        {
            InnerPowerProgress.Initialize();
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var command = new NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS revora_inner_purchases (
entityid bigint PRIMARY KEY, points bigint NOT NULL CHECK (points >= 0))", connection))
                    command.ExecuteNonQuery();
            }
        }

        internal static void RecoverPlayer(InnerPower inner)
        {
            if (inner == null) return;
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var command = new NpgsqlCommand("SELECT points FROM revora_inner_purchases WHERE entityid=@uid", connection))
                {
                    command.Parameters.AddWithValue("uid", (long)inner.UID);
                    object value = command.ExecuteScalar();
                    Recover(inner, value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value));
                }
            }
            InnerPowerProgress.RecoverPlayer(inner);
        }

        internal static void Recover(InnerPower inner, long total)
        {
            lock (inner.SyncRoot)
            {
                long pending = total - inner.AppliedNpcPoints;
                if (pending == 0) return;
                if (pending < 0 || inner.Potency < 0 || pending > int.MaxValue - inner.Potency)
                    throw new InvalidOperationException("Inner Power purchase checkpoint mismatch.");
                inner.Potency += (int)pending;
                inner.AppliedNpcPoints = total;
            }
        }

        internal static void Offer(GameState client, Npcs dialog, uint price, int grant)
        {
            if (!((price == SmallPrice && grant == SmallGrant) || (price == LargePrice && grant == LargeGrant)))
                return;
            if (client == null || client.Entity == null || client.InnerPower == null || client.ItemSyncRoot == null)
            {
                Refuse(dialog, "Inner Power is not ready. Your Revora Points were not taken.");
                return;
            }
            if (client.Entity.RevoraPoint < price)
            {
                Refuse(dialog, "You need " + price + " Revora Points. Your Revora Points were not taken.");
                return;
            }
            try
            {
                lock (client.ItemSyncRoot)
                lock (client.InnerPower.SyncRoot)
                {
                    if (!Ready(client) || client.Entity.RevoraPoint < price)
                        throw new InvalidOperationException("Inner Power purchase is no longer ready.");
                    if (client.InnerPower.Potency < 0 || client.InnerPower.Potency > int.MaxValue - grant
                        || client.InnerPower.AppliedNpcPoints > long.MaxValue - grant)
                    {
                        Refuse(dialog, "Your Inner Power is already full. Your Revora Points were not taken.");
                        return;
                    }
                    uint nextRvp = client.Entity.RevoraPoint - price;
                    int nextPotency = client.InnerPower.Potency + grant;
                    long nextNpc = client.InnerPower.AppliedNpcPoints + grant;
                    Initialize();
                    using (var connection = DataHolder.MySqlConnection)
                    {
                        connection.Open();
                        using (var tx = connection.BeginTransaction())
                        {
                            long? savedCp, savedRvp;
                            PurseBalance.CopySaved(client, out savedCp, out savedRvp);
                            using (var query = QuestInventory.Command(connection, tx, "SELECT revorapoint FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            {
                                object value = query.ExecuteScalar();
                                long? stored = value == null || value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
                                if (value == null || stored != savedRvp)
                                    throw new InvalidOperationException("Inner Power purse changed.");
                            }
                            if (QuestInventory.Execute(connection, tx, @"UPDATE entities SET revorapoint=@rvp WHERE uid=@uid AND (CASE WHEN @isnull THEN revorapoint IS NULL ELSE revorapoint=@old END)",
                                "rvp", (long)nextRvp, "uid", (long)client.Entity.UID, "isnull", !savedRvp.HasValue, "old", savedRvp.GetValueOrDefault()) != 1)
                                throw new InvalidOperationException("Inner Power purse changed.");
                            using (var insert = new NpgsqlCommand("INSERT INTO revora_inner_purchases(entityid,points) VALUES(@uid,0) ON CONFLICT(entityid) DO NOTHING", connection, tx))
                            {
                                insert.Parameters.AddWithValue("uid", (long)client.Entity.UID);
                                insert.ExecuteNonQuery();
                            }
                            if (QuestInventory.Execute(connection, tx, "UPDATE revora_inner_purchases SET points=@after WHERE entityid=@uid AND points=@before",
                                "after", nextNpc, "uid", (long)client.Entity.UID, "before", client.InnerPower.AppliedNpcPoints) != 1)
                                throw new InvalidOperationException("Inner Power purchase changed.");
                            tx.Commit();
                        }
                    }
                    Action<GameState> hook = AfterCommit;
                    if (hook != null) hook(client);
                    client.Entity.ApplyCommittedRevoraPoints(nextRvp);
                    client.InnerPower.Potency = nextPotency;
                    client.InnerPower.AppliedNpcPoints = nextNpc;
                    client.InnerPower.AddPotency(client, 0);
                }
                // Save takes the global file gate, then every player's potency
                // lock. Never enter it with an individual potency lock held.
                // The ledger covers this gap; handle failures outside as well.
                InnerPowerTable.Save();
                dialog.Text("You received " + grant + " Inner Power. You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
                dialog.Option("Thank you.", 255);
                dialog.Send();
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(client, "inner-purchase", ex);
                Refuse(dialog, "The Inner Power purchase could not be confirmed. Please reconnect before trying again.");
            }
        }

        static bool Ready(GameState client)
        {
            GameState current;
            return client != null && !client.Fake && client.Entity != null && client.Entity.FullyLoaded
                && client.Action == 2 && !client.Entity.Dead && !client.Entity.PersistenceRequiresReload
                && client.InnerPower != null && client.ItemSyncRoot != null
                && Kernel.GamePool.TryGetValue(client.Entity.UID, out current) && ReferenceEquals(current, client);
        }

        static void Refuse(Npcs dialog, string text)
        {
            dialog.Text(text);
            dialog.Option("Thank you.", 255);
            dialog.Send();
        }
    }
}
