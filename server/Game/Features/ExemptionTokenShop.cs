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
    // Historical receipt table also covers non-stacking +1..+8 rune materials.
    internal static class ExemptionTokenShop
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_exemption_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,shop bigint NOT NULL,item_type integer NOT NULL,
quantity integer NOT NULL CHECK(quantity BETWEEN 1 AND 40),bound boolean NOT NULL,unit_price bigint NOT NULL CHECK(unit_price>0),
cost bigint NOT NULL CHECK(cost=quantity*unit_price),balance_before bigint NOT NULL CHECK(balance_before>=cost),
balance_after bigint NOT NULL CHECK(balance_after=balance_before-cost),item_uids bigint[] NOT NULL,
purchased_at timestamptz NOT NULL,CHECK(cardinality(item_uids)=quantity));
CREATE INDEX IF NOT EXISTS revora_exemption_recent ON revora_exemption_purchases(uid,shop,item_type,quantity,bound,purchased_at DESC);");
            }
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Map != null && p.Entity.MapID == p.Map.ID && p.Booth == null && p.Trade != null && !p.Trade.InTrade && p.Entity.PokerTable == 0; }
        internal static bool TryBuy(GameState p, ItemUsage request)
        {
            if (request == null) return false;
            byte[] frame = request.ToArray();
            if (frame == null || frame.Length < 16) return true;
            uint type = BitConverter.ToUInt32(frame, 12);
            if (type != 723701 && (type < 730001 || type > 730008)) return false;
            if (frame.Length < 38 || frame.Length > 128 || BitConverter.ToUInt16(frame, 0) != frame.Length - 8
                || BitConverter.ToUInt16(frame, 2) != 1009 || BitConverter.ToUInt64(frame, frame.Length - 8) != PacketHandler.ClientSeal) return true;
            ushort action = BitConverter.ToUInt16(frame, 20);
            if ((action != 1 && action != 55) || !Ready(p)) return true;
            uint amount = request.dwExtraInfo == 0 ? 1u : request.dwExtraInfo;
            if (amount > 40 || request.dwExtraInfo3 > 2) return true;
            bool bound = request.dwExtraInfo3 == 2;
            lock (p.ItemSyncRoot) try
            {
                INpc npc; ShopFile.Shop shop; ConquerItemBaseInformation info;
                if (!Ready(p) || EShopFile.Shops == null || !EShopFile.Shops.TryGetValue(request.UID, out shop) || shop == null
                    || shop.UID != request.UID || (shop.MoneyType != ShopFile.MoneyType.ConquerPoints && shop.MoneyType != ShopFile.MoneyType.BoundCps) || shop.Items == null || !shop.Items.Contains(type)
                    || !ConquerItemInformation.BaseInformations.TryGetValue(type, out info) || info.ID != type || info.ConquerPointsWorth == 0) return true;
                var currency = shop.MoneyType;
                bound = bound || currency == ShopFile.MoneyType.BoundCps;
                if (request.UID != 2888 && (p.Map.Npcs == null || !p.Map.Npcs.TryGetValue(request.UID, out npc)
                    || npc == null || npc.MapID != p.Entity.MapID || Kernel.GetDistance(p.Entity.X, p.Entity.Y, npc.X, npc.Y) > 17)) return true;
                if (p.Inventory.Count > 40 - amount) { p.Send(Constants.FullInventory); return true; }
                uint before = bound ? p.Entity.BoundCps : p.Entity.ConquerPoints; ulong cost = (ulong)info.ConquerPointsWorth * amount;
                if (before < cost) { p.Send("You do not have enough points."); return true; }
                uint after = checked((uint)(before - cost));
                ConquerItem[] items;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        DateTime now = Clock();
                        using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_exemption_purchases
WHERE uid=@uid AND shop=@shop AND item_type=@type AND quantity=@quantity AND bound=@bound AND purchased_at>@now-interval '1 second')",
                            "uid", (long)p.Entity.UID, "shop", (long)request.UID, "type", (int)info.ID, "quantity", (int)amount, "bound", bound, "now", now))
                            if (Equals(q.ExecuteScalar(), true)) return true;
                        var ids = new System.Collections.Generic.List<uint>();
                        using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read()) {
                            if (ids.Count >= 40) throw new InvalidOperationException("Token inventory exceeds its bound."); ids.Add(checked((uint)Convert.ToInt64(r[0])));
                        }
                        if (!ids.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i))) throw new InvalidOperationException("Token inventory requires reload.");
                        items = Enumerable.Range(0, (int)amount).Select(_ => Q.New(info.ID, bound)).ToArray();
                        string payment = bound ? "UPDATE entities SET boundcps=@after WHERE uid=@uid AND COALESCE(boundcps,0)=@before"
                            : "UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND COALESCE(conquerpoints,0)=@before";
                        if (Q.Execute(c, t, payment, "after", (long)after, "uid", (long)p.Entity.UID, "before", (long)before) != 1)
                            throw new InvalidOperationException("Token purchase balance changed.");
                        foreach (var item in items)
                        {
                            item.MaxStackSize = 1; item.Color = Enums.Color.Orange;
                            item.Plus = type == 723701 ? (byte)0 : (byte)(type - 730000);
                            if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@dur,3)", "item", (long)item.UID, "type", (int)item.ID,
                                "uid", (long)p.Entity.UID, "bound", bound ? 1 : 0, "plus", (int)item.Plus, "dur", (int)item.Durability) != 1)
                                throw new InvalidOperationException("Token purchase item insert failed.");
                        }
                        if (Q.Execute(c, t, @"INSERT INTO revora_exemption_purchases(id,uid,shop,item_type,quantity,bound,unit_price,cost,balance_before,balance_after,item_uids,purchased_at)
VALUES(@id,@uid,@shop,@type,@quantity,@bound,@price,@cost,@before,@after,@items,@now)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                            "shop", (long)request.UID, "type", (int)info.ID, "quantity", (int)amount, "bound", bound, "price", (long)info.ConquerPointsWorth,
                            "cost", (decimal)cost, "before", (decimal)before, "after", (decimal)after,
                            "items", items.Select(i => (long)i.UID).ToArray(), "now", now) != 1)
                            throw new InvalidOperationException("Token purchase receipt was not saved.");
                        if (!Ready(p) || (bound ? p.Entity.BoundCps : p.Entity.ConquerPoints) != before
                            || !EShopFile.Shops.TryGetValue(request.UID, out shop) || shop.MoneyType != currency || !shop.Items.Contains(type)
                            || request.UID != 2888 && (p.Map.Npcs == null || !p.Map.Npcs.TryGetValue(request.UID, out npc) || npc.MapID != p.Entity.MapID
                                || Kernel.GetDistance(p.Entity.X, p.Entity.Y, npc.X, npc.Y) > 17))
                            throw new InvalidOperationException("Token purchase context changed.");
                        t.Commit();
                    }
                }
                if (bound) p.Entity.ApplyCommittedBoundCps(after); else p.Entity.ApplyCommittedConquerPoints(after); Q.Install(p, new uint[0], items);
            }
            catch (Exception ex) { Q.Fail(p, "exemption-token-purchase", ex); }
            return true;
        }
    }
}
