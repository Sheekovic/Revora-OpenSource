using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WeaponBookShop
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_weapon_book_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,shop bigint NOT NULL,item_type integer NOT NULL,
quantity integer NOT NULL CHECK(quantity BETWEEN 1 AND 40),unit_price bigint NOT NULL CHECK(unit_price>0),
cost bigint NOT NULL CHECK(cost=quantity*unit_price),silver_before bigint NOT NULL CHECK(silver_before>=cost),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-cost),item_uids bigint[] NOT NULL,
purchased_at timestamptz NOT NULL,CHECK(cardinality(item_uids)=quantity));
CREATE INDEX IF NOT EXISTS revora_weapon_book_recent ON revora_weapon_book_purchases(uid,shop,item_type,quantity,purchased_at DESC);");
            }
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Map != null && p.Entity.MapID == p.Map.ID && p.Booth == null && p.Trade != null && !p.Trade.InTrade && p.Entity.PokerTable == 0; }
        internal static bool TryBuy(GameState p, ItemUsage request)
        {
            if (request == null) return false;
            byte[] frame = request.ToArray();
            if (frame == null || frame.Length < 16) return true;
            // The installed Pickaxe uses this same protected silver-stock transaction.
            uint itemType = BitConverter.ToUInt32(frame, 12);
            bool pickaxe = itemType == 562000;
            ShopFile.Shop equipmentShop;
            int position = (int)ItemHandler.GetPositionFromID(itemType);
            bool silverEquipment = (position >= 1 && position <= 6 || position == 8)
                && ShopFile.Shops != null && ShopFile.Shops.TryGetValue(request.UID, out equipmentShop)
                && equipmentShop != null && equipmentShop.MoneyType == ShopFile.MoneyType.Gold;
            if (!pickaxe && !silverEquipment && !WeaponSkillBooks.IsBook(itemType)) return false;
            if (frame.Length < 38 || frame.Length > 128 || BitConverter.ToUInt16(frame, 0) != frame.Length - 8
                || BitConverter.ToUInt16(frame, 2) != 1009 || BitConverter.ToUInt64(frame, frame.Length - 8) != PacketHandler.ClientSeal) return true;
            ushort action = BitConverter.ToUInt16(frame, 20);
            if ((action != 1 && action != 55) || !Ready(p)) return true;
            uint amount = request.dwExtraInfo == 0 ? 1u : request.dwExtraInfo;
            if (amount > 40) return true;
            lock (p.ItemSyncRoot) try
            {
                INpc npc; ShopFile.Shop shop; ConquerItemBaseInformation info;
                if (!Ready(p) || p.Map.Npcs == null || !p.Map.Npcs.TryGetValue(request.UID, out npc)
                    || npc == null || npc.MapID != p.Entity.MapID || Kernel.GetDistance(p.Entity.X, p.Entity.Y, npc.X, npc.Y) > 17
                    || ShopFile.Shops == null || !ShopFile.Shops.TryGetValue(request.UID, out shop) || shop == null
                    || shop.UID != request.UID || shop.MoneyType != ShopFile.MoneyType.Gold || shop.Items == null
                    || !shop.Items.Contains(request.dwParam) || !ConquerItemInformation.BaseInformations.TryGetValue(request.dwParam, out info)
                    || info.ID != request.dwParam || info.GoldWorth == 0 || info.StackSize > 1) return true;
                if (p.Inventory.Count > 40 - amount) { p.Send(Constants.FullInventory); return true; }
                ulong before = p.Entity.Money, cost = (ulong)info.GoldWorth * amount;
                if (before < cost) { p.Send("You do not have enough silver."); return true; }
                ulong after = before - cost;
                ConquerItem[] items;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        SilverBalance.AssertSaved(c, t, p);
                        using (var q = Q.Command(c, t, "SELECT money FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                            if (!pickaxe && Convert.ToDecimal(q.ExecuteScalar()) != before)
                            { p.Send("Let your purse settle, then try again."); return true; }
                        DateTime now = Clock();
                        using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_weapon_book_purchases
WHERE uid=@uid AND shop=@shop AND item_type=@type AND quantity=@quantity AND purchased_at>@now-interval '1 second')",
                            "uid", (long)p.Entity.UID, "shop", (long)request.UID, "type", (int)info.ID, "quantity", (int)amount, "now", now))
                            if (Equals(q.ExecuteScalar(), true)) return true;
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                            if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count)
                                throw new InvalidOperationException("Book purchase inventory requires reload.");
                        items = Enumerable.Range(0, (int)amount).Select(_ => Q.New(info.ID, false)).ToArray();
                        if (pickaxe) SilverBalance.Persist(c, t, p, after);
                        else if (Q.Execute(c, t, "UPDATE entities SET money=@after WHERE uid=@uid AND money=@before",
                            "after", (decimal)after, "uid", (long)p.Entity.UID, "before", (decimal)before) != 1)
                            throw new InvalidOperationException("Book purchase purse changed.");
                        foreach (var item in items)
                        {
                            item.MaxStackSize = 1; item.Color = Enums.Color.Orange;
                            if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,1,0,@dur,@dur,3)", "item", (long)item.UID, "type", (int)item.ID,
                                "uid", (long)p.Entity.UID, "dur", (int)item.Durability) != 1)
                                throw new InvalidOperationException("Book purchase item insert failed.");
                        }
                        if (Q.Execute(c, t, @"INSERT INTO revora_weapon_book_purchases(id,uid,shop,item_type,quantity,unit_price,cost,silver_before,silver_after,item_uids,purchased_at)
VALUES(@id,@uid,@shop,@type,@quantity,@price,@cost,@before,@after,@items,@now)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                            "shop", (long)request.UID, "type", (int)info.ID, "quantity", (int)amount, "price", (long)info.GoldWorth,
                            "cost", (decimal)cost, "before", (decimal)before, "after", (decimal)after,
                            "items", items.Select(i => (long)i.UID).ToArray(), "now", now) != 1)
                            throw new InvalidOperationException("Book purchase receipt was not saved.");
                        t.Commit();
                    }
                }
                SilverBalance.ApplyCommitted(p, after); Q.Install(p, new uint[0], items);
            }
            catch (Exception ex) { Q.Fail(p, "weapon-book-purchase", ex); }
            return true;
        }
    }
}
