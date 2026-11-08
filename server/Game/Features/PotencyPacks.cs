using System;
using System.Collections.Generic;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class PotencyPacks
    {
        internal static int Points(uint id)
        {
            return id == 3007108 ? 400 : id == 3007109 ? 600 : id == 3007110 ? 1000 : 0;
        }

        // Cumulative awards are committed with consumption. InnerPower.txt stores the
        // applied cumulative value so startup can recover a commit made before its save.
        internal static void Initialize()
        {
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var command = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS revora_potency_awards (entityid bigint PRIMARY KEY, points bigint NOT NULL CHECK (points >= 0))", connection))
                    command.ExecuteNonQuery();
                using (var command = new NpgsqlCommand("SELECT a.entityid,a.points,e.name FROM revora_potency_awards a JOIN entities e ON e.uid=a.entityid", connection))
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        uint uid = checked((uint)reader.GetInt64(0));
                        InnerPower inner;
                        if (!InnerPower.InnerPowerPolle.TryGetValue(uid, out inner))
                        {
                            inner = new InnerPower(reader.GetString(2), uid);
                            InnerPower.InnerPowerPolle[uid] = inner;
                        }
                        RecoverAwards(inner, reader.GetInt64(1));
                    }
            }
        }

        internal static void RecoverAwards(InnerPower inner, long total)
        {
            lock (inner.SyncRoot)
            {
                long pending = total - inner.AppliedPackPoints;
                if (pending < 0 || pending > int.MaxValue - (long)inner.Potency)
                    throw new InvalidOperationException("Potency award checkpoint mismatch");
                inner.Potency += (int)pending;
                inner.AppliedPackPoints = total;
            }
        }

        internal static void RecoverPlayer(InnerPower inner)
        {
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var command = new NpgsqlCommand("SELECT points FROM revora_potency_awards WHERE entityid=@uid", connection))
                {
                    command.Parameters.AddWithValue("uid", (long)inner.UID);
                    object value = command.ExecuteScalar();
                    RecoverAwards(inner, value == null ? 0 : Convert.ToInt64(value));
                }
            }
        }

        internal static void Pickup(GameState client, FloorItem drop)
        {
            if (!Eligible(client) || drop == null || drop.Item == null || Points(drop.Item.ID) == 0) return;
            lock (client.ItemSyncRoot)
            lock (drop)
            {
                FloorItem current;
                Map map = client.Map;
                if (!Eligible(client) || map == null || drop.MapID != map.ID || drop.PickedUpAlready
                    || client.Entity.X != drop.X || client.Entity.Y != drop.Y
                    || !map.FloorItems.TryGetValue(drop.UID, out current) || !ReferenceEquals(current, drop)) return;
                uint owner = drop.Owner == null || drop.Owner.Entity == null ? drop.OwnerUID : drop.Owner.Entity.UID;
                if (owner != 0 && owner != client.Entity.UID && Time32.Now < drop.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    && !(client.Team != null && client.Team.PickupItems && client.Team.IsTeammate(owner)))
                { client.Send(Constants.FloorItemNotAvailable); return; }
                if (client.Inventory.Count >= 40) { client.Send(Constants.FullInventory); return; }
                try
                {
                    // These farm drops are new single, unbound consumables. Persist before UI grant.
                    if (!drop.Item.MobDropped || drop.Item.Bound || drop.Item.StackSize != 1 || drop.Item.UID == 0) return;
                    using (var connection = DataHolder.MySqlConnection)
                    {
                        connection.Open();
                        ExecuteOne(connection, "INSERT INTO items(uid,id,entityid,stacksize,durability,maximdurability,color) VALUES(@uid,@id,@owner,1,@durability,@maximum,@color)",
                            "uid", (long)drop.Item.UID, "id", (int)drop.Item.ID, "owner", (long)client.Entity.UID,
                            "durability", (int)drop.Item.Durability, "maximum", (int)drop.Item.MaximDurability, "color", (int)drop.Item.Color);
                    }
                    ((ICollection<KeyValuePair<uint, FloorItem>>)map.FloorItems).Remove(new KeyValuePair<uint, FloorItem>(drop.UID, drop));
                    drop.PickedUpAlready = true; drop.Type = FloorItem.Remove;
                    map.Floor[drop.X, drop.Y, MapObjectType.Item, drop] = true;
                    if (!client.Inventory.Add(drop.Item, Enums.ItemUse.None)) throw new InvalidOperationException("Committed pack inventory requires reload");
                    drop.Item.Send(client);
                    client.RemoveScreenSpawn(drop, true);
                    client.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[drop.Item.ID].Name));
                }
                catch (Exception error) { Fail(client, error); }
            }
        }

        private static bool Eligible(GameState client)
        {
            return client != null && !client.Fake && client.Action == 2 && client.Entity != null
                && !client.Entity.Dead && !client.Entity.PersistenceRequiresReload && client.InnerPower != null
                && client.Inventory != null && client.ItemSyncRoot != null && client.Booth == null
                && client.Trade != null && !client.Trade.InTrade;
        }

        internal static void Use(GameState client, ConquerItem item)
        {
            if (!Eligible(client) || item == null || Points(item.ID) == 0) return;
            Exception failure = null;
            lock (client.ItemSyncRoot)
            lock (client.InnerPower.SyncRoot)
            {
                ConquerItem owned;
                if (!Eligible(client) || !client.Inventory.TryGetItem(item.UID, out owned)
                    || !ReferenceEquals(owned, item) || item.Position != 0) return;
                int points = Points(item.ID);
                uint exploits = item.ID == 3007108 ? 50u : item.ID == 3007109 ? 100u : 150u;
                uint fund = item.ID == 3007108 ? 100u : item.ID == 3007109 ? 160u : 360u;
                if (client.InnerPower.Potency < 0 || client.InnerPower.Potency > int.MaxValue - points
                    || client.InnerPower.AppliedPackPoints > long.MaxValue - points || client.UnionExploits > uint.MaxValue - exploits)
                { client.Send("Spend some Potency before opening another pack."); return; }
                int potency = client.InnerPower.Potency + points;
                long total = client.InnerPower.AppliedPackPoints + points;
                uint afterExploits = client.UnionExploits + exploits;
                ushort after = (ushort)(Math.Max(1, (int)item.StackSize) - 1);
                try
                {
                    using (var connection = DataHolder.MySqlConnection)
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            using (var command = new NpgsqlCommand("INSERT INTO revora_potency_awards(entityid,points) VALUES(@uid,0) ON CONFLICT(entityid) DO NOTHING", connection))
                            { command.Parameters.AddWithValue("uid", (long)client.Entity.UID); command.ExecuteNonQuery(); }
                            ExecuteOne(connection, "UPDATE revora_potency_awards SET points=@after WHERE entityid=@uid AND points=@before",
                                "uid", (long)client.Entity.UID, "after", total, "before", client.InnerPower.AppliedPackPoints);
                            ExecuteOne(connection, after == 0
                                ? "DELETE FROM items WHERE uid=@uid AND entityid=@owner AND id=@id AND position=0 AND stacksize=@before"
                                : "UPDATE items SET stacksize=@after WHERE uid=@uid AND entityid=@owner AND id=@id AND position=0 AND stacksize=@before",
                                "uid", (long)item.UID, "owner", (long)client.Entity.UID, "id", (int)item.ID,
                                "before", (int)item.StackSize, "after", (int)after);
                            ExecuteOne(connection, "UPDATE entities SET unionexploits=@exploits WHERE uid=@owner",
                                "owner", (long)client.Entity.UID, "exploits", (long)afterExploits);
                            transaction.Commit();
                        }
                    }
                    client.InnerPower.Potency = potency;
                    client.InnerPower.AppliedPackPoints = total;
                    client.Entity.UseItem = false;
                    if (after == 0) client.Inventory.Remove(item.UID, Enums.ItemUse.None, true);
                    else { item.StackSize = after; item.Mode = Enums.ItemMode.Update; item.Send(client); item.Mode = Enums.ItemMode.Default; }
                    client.InnerPower.AddPotency(client, 0);
                    client.UnionExploits = afterExploits;
                    if (client.Union != null) client.Union.UnionFund += fund;
                    client.Send("You received " + points + " Potency and " + exploits + " War Exploits."
                        + (client.Union == null ? "" : " Your Union received " + fund + " Fund."));
                }
                catch (Exception error) { failure = error; }
            }
            if (failure != null) Fail(client, failure);
        }

        private static void Fail(GameState client, Exception error)
        {
            Console.WriteLine("[potency-pack] persistence failure uid=" + client.Entity.UID + " type=" + error.GetType().Name);
            client.Entity.PersistenceRequiresReload = true;
            client.Action = 0;
            client.Send("Please reconnect before opening another pack.");
            client.Disconnect();
        }

        private static void ExecuteOne(NpgsqlConnection connection, string sql, params object[] values)
        {
            using (var command = new NpgsqlCommand(sql, connection))
            {
                for (int i = 0; i < values.Length; i += 2) command.Parameters.AddWithValue((string)values[i], values[i+1]);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Potency pack persistence conflict");
            }
        }
    }
}
