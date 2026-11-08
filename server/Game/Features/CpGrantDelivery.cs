using System;
using System.Collections.Generic;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class CpGrantDelivery
    {
        private static DateTime lastErrorUtc = DateTime.MinValue;

        public static void Pulse()
        {
            try
            {
                List<PendingGrant> pending = LoadPending(0, 32);
                for (int i = 0; i < pending.Count; i++)
                {
                    PendingGrant grant = pending[i];
                    Client.GameState client;
                    if (!Revora.Kernel.GamePool.TryGetValue(grant.Uid, out client) || client == null || client.Entity == null || !client.Entity.FullyLoaded)
                        continue;
                    Deliver(grant.OrderId, client, grant.Amount);
                }
            }
            catch (Exception exception)
            {
                LogError("[shop] grant pulse ", exception);
            }
        }

        public static void ApplyFor(Client.GameState client)
        {
            if (client == null || client.Entity == null) return;
            try
            {
                List<PendingGrant> pending = LoadPending(client.Entity.UID, 8);
                for (int i = 0; i < pending.Count; i++)
                    Deliver(pending[i].OrderId, client, pending[i].Amount);
            }
            catch (Exception exception)
            {
                LogError("[shop] grant login ", exception);
            }
        }

        private static void LogError(string prefix, Exception exception)
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastErrorUtc).TotalMinutes < 5) return;
            lastErrorUtc = now;
            Console.WriteLine(prefix + exception.Message);
        }

        private static List<PendingGrant> LoadPending(uint uid, int limit)
        {
            var list = new List<PendingGrant>();
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    if (uid == 0)
                        cmd.CommandText = "SELECT order_id, entity_uid, cp_amount FROM identity.cp_orders WHERE status='paid' AND delivered_at IS NULL ORDER BY paid_at ASC LIMIT " + limit;
                    else
                    {
                        cmd.CommandText = "SELECT order_id, entity_uid, cp_amount FROM identity.cp_orders WHERE status='paid' AND delivered_at IS NULL AND entity_uid=@uid ORDER BY paid_at ASC LIMIT " + limit;
                        DataHolder.AddParameter(cmd, "@uid", (long)uid);
                    }
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long entityUid = reader.GetInt64(1);
                            int amount = reader.GetInt32(2);
                            if (entityUid <= 0 || entityUid > uint.MaxValue || amount <= 0) continue;
                            list.Add(new PendingGrant(reader.GetGuid(0), (uint)entityUid, (uint)amount));
                        }
                    }
                }
            }
            return list;
        }

        private static void Deliver(Guid orderId, Client.GameState client, uint amount)
        {
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var claim = conn.CreateCommand())
                {
                    claim.CommandText = "UPDATE identity.cp_orders SET status='delivered', delivered_at=clock_timestamp() WHERE order_id=@order_id AND status='paid' AND delivered_at IS NULL";
                    DataHolder.AddParameter(claim, "@order_id", orderId);
                    if (claim.ExecuteNonQuery() != 1) return;
                }
            }
            uint current = client.Entity.ConquerPoints;
            if (amount > uint.MaxValue - current)
                amount = uint.MaxValue - current;
            if (amount == 0) return;
            client.Entity.ConquerPoints = current + amount;
            client.Send(new Message("You received " + amount + " Conquer Points from the Revora shop.", Color.Gold, Message.Talk));
        }

        private struct PendingGrant
        {
            public Guid OrderId;
            public uint Uid;
            public uint Amount;
            public PendingGrant(Guid orderId, uint uid, uint amount)
            {
                OrderId = orderId;
                Uid = uid;
                Amount = amount;
            }
        }
    }
}
