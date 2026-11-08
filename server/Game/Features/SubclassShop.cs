using System;
using System.Collections.Generic;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    // Native NPC shop: its UID is shared with the server/client Shop.dat entries.
    internal static class SubclassShop
    {
        internal const uint NpcUid = 91008;
        internal const uint InnerPowerNpcUid = 91009;
        internal static bool IsShop(uint uid) { return uid == NpcUid || uid == InnerPowerNpcUid || uid == P6SoulShop.NpcUid; }
        internal sealed class StackChange
        {
            internal ConquerItem Item;
            internal ushort Before, After;
            internal bool IsNew;
        }

        internal static List<StackChange> Plan(ConquerItem[] inventory, uint id, uint amount, ushort stackSize)
        {
            var changes = new List<StackChange>();
            uint remaining = amount;
            ushort maximum = Math.Max((ushort)1, stackSize);
            if (amount == 0 || inventory.Length > 40 || amount > 40u * maximum) return null;
            foreach (var item in inventory)
            {
                if (item.ID != id || item.Bound || item.Position != 0 || item.StackSize >= maximum) continue;
                uint count = Math.Max(1u, item.StackSize);
                uint added = Math.Min(remaining, (uint)maximum - count);
                if (added == 0) continue;
                changes.Add(new StackChange { Item = item, Before = item.StackSize, After = (ushort)(count + added) });
                remaining -= added;
                if (remaining == 0) return changes;
            }
            int slots = 40 - inventory.Length;
            while (remaining != 0 && slots-- > 0)
            {
                ushort count = (ushort)Math.Min(remaining, maximum);
                changes.Add(new StackChange { IsNew = true, After = count });
                remaining -= count;
            }
            return remaining == 0 ? changes : null;
        }

        internal static bool TryCost(uint price, uint amount, uint balance, out uint cost)
        {
            ulong total = (ulong)price * amount;
            cost = total <= uint.MaxValue ? (uint)total : 0;
            return price != 0 && amount != 0 && total <= balance;
        }

        private static bool Eligible(GameState client)
        {
            return client != null && !client.Fake && client.Action == 2 && client.Entity != null
                && !client.Entity.Dead && !client.Entity.PersistenceRequiresReload && client.Inventory != null && client.ItemSyncRoot != null
                && client.Trade != null && !client.Trade.InTrade;
        }

        internal static void Buy(GameState client, ItemUsage request)
        {
            if (!Eligible(client) || request == null || !IsShop(request.UID)) return;
            lock (client.ItemSyncRoot)
            {
                Interfaces.INpc npc;
                ShopFile.Shop shop;
                ConquerItemBaseInformation info;
                if (!Eligible(client) || client.Map == null || client.Map.ID != 1002
                    || !client.Map.Npcs.TryGetValue(request.UID, out npc) || npc.Type != Enums.NpcType.Shop
                    || Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17
                    || !ShopFile.Shops.TryGetValue(request.UID, out shop) || shop.MoneyType != ShopFile.MoneyType.ConquerPoints
                    || !shop.Items.Contains(request.dwParam)
                    || !ConquerItemInformation.BaseInformations.TryGetValue(request.dwParam, out info)) return;
                if (request.dwExtraInfo3 == 2) { client.Send("This shop accepts regular CPs only."); return; }
                uint amount = request.dwExtraInfo == 0 ? 1 : request.dwExtraInfo;
                Purchase(client, info, amount, info.ConquerPointsWorth, false);
            }
        }

        internal static bool BuyRune(GameState client, uint id, uint price)
        {
            if (!Eligible(client)) return false;
            lock (client.ItemSyncRoot)
            {
                Interfaces.INpc npc;
                ConquerItemBaseInformation info;
                if (!Eligible(client) || client.ActiveNpc != RevoraPointShopSystem.NpcId
                    || client.Map == null || client.Map.ID != 1002
                    || !client.Map.Npcs.TryGetValue(RevoraPointShopSystem.NpcId, out npc)
                    || Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17
                    || !ConquerItemInformation.BaseInformations.TryGetValue(id, out info)) return false;
                return Purchase(client, info, 1, price, true);
            }
        }

        private static bool Purchase(GameState client, ConquerItemBaseInformation info, uint amount, uint price, bool rvp)
        {
            uint cost;
            if (!TryCost(price, amount, rvp ? client.Entity.RevoraPoint : client.Entity.ConquerPoints, out cost))
            { client.Send(rvp ? "You do not have enough RVPs." : "You do not have enough CPs."); return false; }
            var plan = Plan(client.Inventory.Objects, info.ID, amount, info.StackSize);
            if (plan == null) { client.Send(Constants.FullInventory); return false; }
            uint before = rvp ? client.Entity.RevoraPoint : client.Entity.ConquerPoints;
            foreach (var change in plan) if (change.IsNew)
            {
                change.Item = new ConquerItem(true) { UID = ConquerItem.ItemUID.Next, ID = info.ID,
                    Durability = info.Durability, MaximDurability = info.Durability,
                    StackSize = change.After, Color = Enums.Color.Orange };
            }
            try
            {
                using (var connection = DataHolder.MySqlConnection)
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        ExecuteOne(connection, rvp ? "UPDATE entities SET revorapoint=@after WHERE uid=@owner"
                            : "UPDATE entities SET conquerpoints=@after WHERE uid=@owner AND conquerpoints=@before",
                            "after", (long)(before - cost), "owner", (long)client.Entity.UID, "before", (long)before);
                        foreach (var change in plan)
                        {
                            if (change.IsNew)
                                ExecuteOne(connection, "INSERT INTO items (uid,id,entityid,stacksize,durability,maximdurability,color) VALUES (@uid,@id,@owner,@after,@durability,@durability,3)",
                                    "uid", (long)change.Item.UID, "id", (int)change.Item.ID, "owner", (long)client.Entity.UID,
                                    "after", (int)change.After, "durability", (int)info.Durability);
                            else
                                ExecuteOne(connection, "UPDATE items SET stacksize=@after WHERE uid=@uid AND entityid=@owner AND id=@id AND position=0 AND bound=0 AND stacksize=@before",
                                    "uid", (long)change.Item.UID, "owner", (long)client.Entity.UID, "id", (int)change.Item.ID,
                                    "after", (int)change.After, "before", (int)change.Before);
                        }
                        transaction.Commit();
                    }
                }
                // No network notification or in-memory grant before the complete database commit.
                if (rvp) client.Entity.ApplyCommittedRevoraPoints(before - cost);
                else client.Entity.ApplyCommittedConquerPoints(before - cost);
                foreach (var change in plan)
                {
                    change.Item.StackSize = change.After;
                    if (change.IsNew && !client.Inventory.Add(change.Item, Enums.ItemUse.None))
                        throw new InvalidOperationException("Committed inventory requires reload");
                    change.Item.Mode = change.IsNew ? Enums.ItemMode.Default : Enums.ItemMode.Update;
                    change.Item.Send(client);
                    change.Item.Mode = Enums.ItemMode.Default;
                }
            }
            catch (Exception error) { Recover(client, error); return false; }
            return true;
        }

        internal static ushort BookPoints(uint id)
        {
            switch (id) { case 723340: return 5; case 723341: return 20; case 728883: return 50; case 723342: return 500; default: return 0; }
        }

        internal static void ReadBook(GameState client, ConquerItem item)
        {
            if (!Eligible(client) || item == null || BookPoints(item.ID) == 0) return;
            lock (client.ItemSyncRoot)
            {
                ConquerItem owned;
                ushort points = BookPoints(item.ID);
                if (!Eligible(client) || !client.Inventory.TryGetItem(item.UID, out owned) || !Object.ReferenceEquals(owned, item)) return;
                int study = client.Entity.SubClasses.StudyPoints + points;
                if (study > ushort.MaxValue) { client.Send("Spend some study points before reading another book."); return; }
                ushort after = (ushort)(Math.Max(1, (int)item.StackSize) - 1);
                try
                {
                    using (var connection = DataHolder.MySqlConnection)
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            ExecuteOne(connection, "UPDATE entities SET studypoints=@study WHERE uid=@owner", "study", study, "owner", (long)client.Entity.UID);
                            ExecuteOne(connection, after == 0
                                ? "DELETE FROM items WHERE uid=@uid AND entityid=@owner AND id=@id AND position=0 AND stacksize=@before"
                                : "UPDATE items SET stacksize=@after WHERE uid=@uid AND entityid=@owner AND id=@id AND position=0 AND stacksize=@before",
                                "uid", (long)item.UID, "owner", (long)client.Entity.UID, "id", (int)item.ID, "before", (int)item.StackSize, "after", (int)after);
                            transaction.Commit();
                        }
                    }
                    client.Entity.SubClasses.StudyPoints = (ushort)study;
                    client.Entity.UseItem = false;
                    if (after == 0) client.Inventory.Remove(item.UID, Enums.ItemUse.None, true);
                    else { item.StackSize = after; item.Mode = Enums.ItemMode.Update; item.Send(client); item.Mode = Enums.ItemMode.Default; }
                    client.Send(new SubClassShowFull(0) { ID = 8, Study = (ushort)study, StudyReceive = points }.ToArray());
                    client.Send("You received " + points + " study points.");
                }
                catch (Exception error) { Recover(client, error); }
            }
        }

        private static void ExecuteOne(NpgsqlConnection connection, string sql, params object[] values)
        {
            using (var command = new NpgsqlCommand(sql, connection))
            {
                for (int i = 0; i < values.Length; i += 2) command.Parameters.AddWithValue((string)values[i], values[i + 1]);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Shop persistence conflict");
            }
        }

        private static void Recover(GameState client, Exception error)
        {
            // Do not retry an ambiguous commit or save a stale in-memory balance over it.
            System.Console.WriteLine("[subclass-shop] persistence failure uid=" + client.Entity.UID + " type=" + error.GetType().Name);
            client.Entity.PersistenceRequiresReload = true;
            client.Action = 0;
            client.Send("The transaction could not be completed. Please reconnect before trying again.");
            client.Disconnect();
        }
    }
}
