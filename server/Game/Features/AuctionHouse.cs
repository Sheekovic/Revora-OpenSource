using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Database rows own listings and entitlements. The native board is only a projection.
    internal static class AuctionHouse
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static readonly object Gate = new object();
        internal sealed class Listing
        {
            internal Guid Id;
            internal uint Item, Type, Seller, Bidder, Reserve, Buyout, Highest;
            internal string SellerName, BidderName;
            internal int Currency, Revision, State;
            internal DateTime Ends;
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_auctions(
id uuid PRIMARY KEY,item_uid bigint NOT NULL,item_type integer NOT NULL,seller bigint NOT NULL CHECK(seller>0),
seller_name varchar(32) NOT NULL,currency smallint NOT NULL CHECK(currency IN (1,2)),
reserve bigint NOT NULL CHECK(reserve BETWEEN 1 AND 4294967295),buyout bigint NOT NULL CHECK(buyout=0 OR buyout BETWEEN reserve AND 4294967295),
highest bigint NOT NULL DEFAULT 0 CHECK(highest BETWEEN 0 AND 4294967295),bidder bigint NOT NULL DEFAULT 0,
bidder_name varchar(32) NOT NULL DEFAULT '',revision integer NOT NULL DEFAULT 0 CHECK(revision>=0),
state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 3),fee bigint NOT NULL CHECK(fee IN (500,1100,2250)),
starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,closed_at timestamptz,
CHECK(ends_at>starts_at),CHECK((bidder=0 AND highest=0) OR (bidder>0 AND bidder<>seller AND highest>=reserve)),
CHECK((state=0 AND closed_at IS NULL) OR (state<>0 AND closed_at IS NOT NULL)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_auction_open_item ON revora_auctions(item_uid) WHERE state=0;
CREATE INDEX IF NOT EXISTS revora_auction_seller ON revora_auctions(seller,ends_at) WHERE state=0;
CREATE INDEX IF NOT EXISTS revora_auction_recent_listing ON revora_auctions(seller,item_uid,starts_at DESC);
CREATE INDEX IF NOT EXISTS revora_auction_bidder ON revora_auctions(bidder,ends_at) WHERE state=0 AND bidder>0;
CREATE TABLE IF NOT EXISTS revora_auction_events(
auction uuid NOT NULL REFERENCES revora_auctions(id),revision integer NOT NULL,kind varchar(16) NOT NULL,
actor bigint NOT NULL,amount bigint NOT NULL CHECK(amount>=0),occurred_at timestamptz NOT NULL,PRIMARY KEY(auction,revision));
CREATE TABLE IF NOT EXISTS revora_auction_deliveries(
id bigserial PRIMARY KEY,auction uuid NOT NULL REFERENCES revora_auctions(id),revision integer NOT NULL,
kind varchar(16) NOT NULL,recipient bigint NOT NULL CHECK(recipient>0),item_uid bigint NOT NULL DEFAULT 0,
currency smallint NOT NULL DEFAULT 0,amount bigint NOT NULL DEFAULT 0,created_at timestamptz NOT NULL,claimed_at timestamptz,
CHECK((item_uid>0 AND currency=0 AND amount=0) OR (item_uid=0 AND currency IN (1,2) AND amount BETWEEN 1 AND 4294967295)),
UNIQUE(auction,revision,kind,recipient));
CREATE INDEX IF NOT EXISTS revora_auction_pending ON revora_auction_deliveries(recipient,id) WHERE claimed_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS revora_auction_pending_item ON revora_auction_deliveries(item_uid) WHERE item_uid>0 AND claimed_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_auction_wire(
auction uuid PRIMARY KEY REFERENCES revora_auctions(id),wire_uid bigint NOT NULL UNIQUE CHECK(wire_uid BETWEEN 1 AND 4294967294));");
            }
        }
        internal static uint PersistWireId(NpgsqlConnection c, NpgsqlTransaction t, Guid id)
        {
            if (t == null) throw new InvalidOperationException("Auction identity requires its listing transaction.");
            using (var q = Q.Command(c, t, "SELECT pg_advisory_xact_lock(6711,1320)")) q.ExecuteNonQuery();
            using (var q = Q.Command(c, t, "SELECT wire_uid FROM revora_auction_wire WHERE auction=@id", "id", id))
            {
                var existing = q.ExecuteScalar();
                if (existing != null) return checked((uint)Convert.ToInt64(existing));
            }
            using (var q = Q.Command(c, t, "SELECT COALESCE(MAX(wire_uid),0) FROM revora_auction_wire"))
                ConquerItem.ItemUID.EnsureAtLeast(checked((uint)Convert.ToInt64(q.ExecuteScalar())));
            uint uid = ConquerItem.ItemUID.NextStrict;
            if (Q.Execute(c, t, "INSERT INTO revora_auction_wire(auction,wire_uid) VALUES(@id,@uid)", "id", id, "uid", (long)uid) != 1)
                throw new InvalidOperationException("Auction display identity was not saved.");
            return uid;
        }
        private static bool Ready(GameState p)
        {
            return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade && p.Entity.PokerTable == 0 && !p.InQualifier();
        }
        private static Listing Read(NpgsqlDataReader r)
        {
            return new Listing { Id = r.GetGuid(0), Item = checked((uint)r.GetInt64(1)), Type = checked((uint)r.GetInt32(2)),
                Seller = checked((uint)r.GetInt64(3)), SellerName = r.GetString(4), Currency = r.GetInt16(5),
                Reserve = checked((uint)r.GetInt64(6)), Buyout = checked((uint)r.GetInt64(7)), Highest = checked((uint)r.GetInt64(8)),
                Bidder = checked((uint)r.GetInt64(9)), BidderName = r.GetString(10), Revision = r.GetInt32(11), State = r.GetInt16(12),
                Ends = r.GetFieldValue<DateTimeOffset>(13).UtcDateTime };
        }
        private const string Columns = "id,item_uid,item_type,seller,seller_name,currency,reserve,buyout,highest,bidder,bidder_name,revision,state,ends_at";
        private static Listing Find(NpgsqlConnection c, NpgsqlTransaction t, Guid id)
        {
            using (var q = Q.Command(c, t, "SELECT " + Columns + " FROM revora_auctions WHERE id=@id FOR UPDATE", "id", id))
            using (var r = q.ExecuteReader()) return r.Read() ? Read(r) : null;
        }
        internal static Listing[] Board()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, "SELECT " + Columns + " FROM revora_auctions WHERE state=0 AND ends_at>@now ORDER BY ends_at,id LIMIT 10000", "now", Clock()))
                using (var r = q.ExecuteReader()) { var result = new List<Listing>(); while (r.Read()) result.Add(Read(r)); return result.ToArray(); }
            }
        }
        internal static Listing Get(Guid id)
        {
            using (var c = DataHolder.MySqlConnection) { c.Open(); return Find(c, null, id); }
        }
        internal static Listing[] Page(int currency, uint actor, bool bidding, int page, int size, out long total)
        {
            total = 0;
            if (currency < 0 || currency > 2 || page < 0 || page > ushort.MaxValue || size < 1 || size > 50) return new Listing[0];
            const string filter = " WHERE state=0 AND ends_at>@now AND (@currency=0 OR currency=@currency) AND (@actor=0 OR (CASE WHEN @bidding THEN bidder ELSE seller END)=@actor)";
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); var args = new object[] { "now", Clock(), "currency", currency, "actor", (long)actor, "bidding", bidding, "size", size, "offset", (long)page * size };
                using (var q = Q.Command(c, null, "SELECT count(*) FROM revora_auctions" + filter, args)) total = Convert.ToInt64(q.ExecuteScalar());
                using (var q = Q.Command(c, null, "SELECT " + Columns + " FROM revora_auctions" + filter + " ORDER BY ends_at,id LIMIT @size OFFSET @offset", args))
                using (var r = q.ExecuteReader()) { var rows = new List<Listing>(); while (r.Read()) rows.Add(Read(r)); return rows.ToArray(); }
            }
        }
        private static void Event(NpgsqlConnection c, NpgsqlTransaction t, Listing a, string kind, uint actor, uint amount, DateTime now)
        {
            if (Q.Execute(c, t, @"INSERT INTO revora_auction_events(auction,revision,kind,actor,amount,occurred_at)
VALUES(@id,@revision,@kind,@actor,@amount,@now)", "id", a.Id, "revision", a.Revision, "kind", kind, "actor", (long)actor, "amount", (long)amount, "now", now) != 1)
                throw new InvalidOperationException("Auction event was not recorded.");
        }
        private static void Delivery(NpgsqlConnection c, NpgsqlTransaction t, Listing a, string kind, uint recipient, uint item, uint amount, DateTime now)
        {
            if (Q.Execute(c, t, @"INSERT INTO revora_auction_deliveries(auction,revision,kind,recipient,item_uid,currency,amount,created_at)
VALUES(@id,@revision,@kind,@uid,@item,@currency,@amount,@now)", "id", a.Id, "revision", a.Revision, "kind", kind, "uid", (long)recipient,
                "item", (long)item, "currency", item == 0 ? a.Currency : 0, "amount", (long)amount, "now", now) != 1)
                throw new InvalidOperationException("Auction delivery was not recorded.");
        }
        private static void Debit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, int currency, uint amount)
        {
            if (currency == 1) SilverBalance.Persist(c, t, p, checked(p.Entity.Money - amount));
            else if (Q.Execute(c, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@before",
                "next", (long)checked(p.Entity.ConquerPoints - amount), "uid", (long)p.Entity.UID, "before", (long)p.Entity.ConquerPoints) != 1)
                throw new InvalidOperationException("Auction CP purse changed.");
        }
        private static void Debited(GameState p, int currency, uint amount)
        {
            if (currency == 1) SilverBalance.ApplyCommitted(p, checked(p.Entity.Money - amount));
            else p.Entity.ApplyCommittedConquerPoints(checked(p.Entity.ConquerPoints - amount));
        }
        private static void Escrow(NpgsqlConnection c, NpgsqlTransaction t, uint item, uint type)
        {
            using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE uid=@item AND id=@type AND entityid=0
AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE", "item", (long)item, "type", (int)type))
                if (q.ExecuteScalar() == null) throw new InvalidOperationException("Auction escrow requires repair.");
        }
        internal static Guid List(GameState p, uint itemUid, int currency, uint reserve, uint buyout, int hours)
        {
            if (!Ready(p) || (currency != 1 && currency != 2) || reserve == 0 || buyout != 0 && buyout < reserve) return Guid.Empty;
            uint fee = hours == 12 ? 500u : hours == 24 ? 1100u : hours == 48 ? 2250u : 0u;
            if (fee == 0) return Guid.Empty;
            lock (p.ItemSyncRoot) lock (Gate) try
            {
                ConquerItem item; ConquerItemBaseInformation info;
                if (!Ready(p) || !p.Inventory.TryGetItem(itemUid, out item) || item.Bound || item.Lock != 0 || item.Suspicious || item.InWardrobe || item.Inscribed
                    || item.Position != 0 || item.Warehouse != 0 || item.StackSize == 0 || item.Days != 0 || p.Entity.Money < fee
                    || !ConquerItemInformation.BaseInformations.TryGetValue(item.ID, out info) || info.Type != ConquerItemBaseInformation.ItemType.Dropable) return Guid.Empty;
                var a = new Listing { Id = Guid.NewGuid(), Item = itemUid, Type = item.ID, Seller = p.Entity.UID, Currency = currency, Reserve = reserve, Buyout = buyout };
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_auctions WHERE seller=@uid AND item_uid=@item AND starts_at>@now-interval '1 second')", "uid", (long)p.Entity.UID, "item", (long)itemUid, "now", Clock()))
                            if (Equals(q.ExecuteScalar(), true)) return Guid.Empty;
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_auctions WHERE seller=@uid AND state=0 AND ends_at>@now", "uid", (long)p.Entity.UID, "now", Clock()))
                            if (Convert.ToInt64(q.ExecuteScalar()) >= 50) return Guid.Empty;
                        using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type
AND position=0 AND warehouse=0 AND bound=0 AND locked=0 AND suspicious=0 AND stacksize=@stack
AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE", "item", (long)itemUid, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize))
                            if (q.ExecuteScalar() == null) throw new InvalidOperationException("Auction item ownership changed.");
                        var stored = ConquerItemTable.LoadItem(itemUid);
                        if (stored == null || stored.ID != item.ID || stored.Inscribed || stored.InWardrobe || AtomicTrade.Fingerprint(stored) != AtomicTrade.Fingerprint(item))
                            throw new InvalidOperationException("Auction item requires a saved, current image.");
                        if (SummerCrafting.Identity(item.ID) && !SummerCrafting.Transferable(c, t, p, item))
                            throw new InvalidOperationException("Auction item provenance changed.");
                        DateTime now = Clock();
                        Debit(c, t, p, 1, fee);
                        if (Q.Execute(c, t, "UPDATE items SET entityid=0 WHERE uid=@item AND entityid=@uid", "item", (long)itemUid, "uid", (long)p.Entity.UID) != 1)
                            throw new InvalidOperationException("Auction escrow transfer failed.");
                        if (Q.Execute(c, t, @"INSERT INTO revora_auctions(id,item_uid,item_type,seller,seller_name,currency,reserve,buyout,fee,starts_at,ends_at)
VALUES(@id,@item,@type,@seller,@name,@currency,@reserve,@buyout,@fee,@now,@ends)", "id", a.Id, "item", (long)itemUid, "type", (int)item.ID,
                            "seller", (long)p.Entity.UID, "name", p.Entity.Name, "currency", currency, "reserve", (long)reserve, "buyout", (long)buyout,
                            "fee", (long)fee, "now", now, "ends", now.AddHours(hours)) != 1) throw new InvalidOperationException("Auction listing failed.");
                        PersistWireId(c, t, a.Id);
                        Event(c, t, a, "list", p.Entity.UID, fee, now); t.Commit();
                    }
                }
                Debited(p, 1, fee); Q.Install(p, new[] { itemUid }, new ConquerItem[0]); return a.Id;
            }
            catch (Exception ex) { Q.Fail(p, "auction-list", ex); return Guid.Empty; }
        }
        internal static bool Bid(GameState p, Guid id, uint offer, bool buyout)
        {
            if (!Ready(p)) return false;
            lock (p.ItemSyncRoot) lock (Gate) try
            {
                if (!Ready(p)) return false;
                uint charge; int currency;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p); var a = Find(c, t, id); DateTime now = Clock();
                        if (a == null || a.State != 0 || a.Ends <= now || a.Seller == p.Entity.UID || buyout && a.Buyout == 0) return false;
                        Escrow(c, t, a.Item, a.Type);
                        bool sold = buyout || a.Buyout > 0 && offer >= a.Buyout;
                        if (!sold && a.Bidder != p.Entity.UID)
                            using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_auctions WHERE bidder=@uid AND state=0 AND ends_at>@now", "uid", (long)p.Entity.UID, "now", now))
                                if (Convert.ToInt64(q.ExecuteScalar()) >= 50) return false;
                        uint price = sold ? a.Buyout : offer;
                        if (price < a.Reserve || a.Bidder != 0 && price <= a.Highest) return false;
                        charge = price - (a.Bidder == p.Entity.UID ? a.Highest : 0); currency = a.Currency;
                        if (currency == 1 ? p.Entity.Money < charge : p.Entity.ConquerPoints < charge) return false;
                        Debit(c, t, p, currency, charge); a.Revision++;
                        if (a.Bidder != 0 && a.Bidder != p.Entity.UID) Delivery(c, t, a, "refund", a.Bidder, 0, a.Highest, now);
                        if (Q.Execute(c, t, @"UPDATE revora_auctions SET highest=@price,bidder=@bidder,bidder_name=@name,revision=@revision,
state=@state,closed_at=@closed WHERE id=@id AND state=0", "price", (long)price, "bidder", (long)p.Entity.UID, "name", p.Entity.Name,
                            "revision", a.Revision, "state", sold ? 1 : 0, "closed", sold ? (object)now : DBNull.Value, "id", id) != 1)
                            throw new InvalidOperationException("Auction bid was not saved.");
                        if (sold) { Delivery(c, t, a, "item", p.Entity.UID, a.Item, 0, now); Delivery(c, t, a, "proceeds", a.Seller, 0, price, now); }
                        Event(c, t, a, sold ? "buyout" : "bid", p.Entity.UID, price, now); t.Commit();
                    }
                }
                Debited(p, currency, charge); return true;
            }
            catch (Exception ex) { Q.Fail(p, "auction-bid", ex); return false; }
        }
        private static void Close(NpgsqlConnection c, NpgsqlTransaction t, Listing a, bool cancel, DateTime now)
        {
            Escrow(c, t, a.Item, a.Type);
            a.Revision++;
            int state = cancel ? 2 : a.Bidder == 0 ? 3 : 1;
            if (Q.Execute(c, t, "UPDATE revora_auctions SET state=@state,revision=@revision,closed_at=@now WHERE id=@id AND state=0",
                "state", state, "revision", a.Revision, "now", now, "id", a.Id) != 1) throw new InvalidOperationException("Auction closure changed.");
            if (cancel && a.Bidder != 0) Delivery(c, t, a, "refund", a.Bidder, 0, a.Highest, now);
            uint recipient = cancel || a.Bidder == 0 ? a.Seller : a.Bidder;
            Delivery(c, t, a, "item", recipient, a.Item, 0, now);
            if (!cancel && a.Bidder != 0) Delivery(c, t, a, "proceeds", a.Seller, 0, a.Highest, now);
            Event(c, t, a, cancel ? "cancel" : "expire", cancel ? a.Seller : 0, a.Highest, now);
        }
        internal static bool Cancel(GameState p, Guid id)
        {
            if (!Ready(p)) return false;
            lock (p.ItemSyncRoot) lock (Gate) try
            {
                if (!Ready(p)) return false;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        var a = Find(c, t, id); DateTime now = Clock();
                        if (a == null || a.State != 0 || a.Seller != p.Entity.UID || a.Ends <= now) return false;
                        Close(c, t, a, true, now); t.Commit(); return true;
                    }
                }
            }
            catch (Exception ex) { Q.Fail(p, "auction-cancel", ex); return false; }
        }
        internal static int Expire()
        {
            lock (Gate) using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    var due = new List<Listing>(); DateTime now = Clock();
                    using (var q = Q.Command(c, t, "SELECT " + Columns + " FROM revora_auctions WHERE state=0 AND ends_at<=@now ORDER BY ends_at,id LIMIT 64 FOR UPDATE", "now", now))
                    using (var r = q.ExecuteReader()) while (r.Read()) due.Add(Read(r));
                    foreach (var a in due) Close(c, t, a, false, now);
                    t.Commit(); return due.Count;
                }
            }
        }
        private sealed class Parcel { internal long Id, Amount; internal uint Item, Type; internal int Currency; }
        internal static int Claim(GameState p)
        {
            if (!Ready(p)) return 0;
            lock (p.ItemSyncRoot) try
            {
                if (!Ready(p)) return 0;
                ulong gold = p.Entity.Money; uint cp = p.Entity.ConquerPoints;
                var items = new List<ConquerItem>(); int claimed = 0;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p); var parcels = new List<Parcel>();
                        using (var q = Q.Command(c, t, @"SELECT d.id,d.item_uid,d.currency,d.amount,a.item_type
FROM revora_auction_deliveries d JOIN revora_auctions a ON a.id=d.auction
WHERE d.recipient=@uid AND d.claimed_at IS NULL AND
((d.item_uid>0 AND @space>0) OR (d.currency=1 AND d.amount<=@gold) OR (d.currency=2 AND d.amount<=@cp))
ORDER BY d.id LIMIT 16 FOR UPDATE OF d", "uid", (long)p.Entity.UID, "space", 40 - p.Inventory.Count,
                            "gold", (long)(Entity.MaximumSilver - gold), "cp", (long)(uint.MaxValue - cp)))
                        using (var r = q.ExecuteReader()) while (r.Read()) parcels.Add(new Parcel { Id = r.GetInt64(0), Item = checked((uint)r.GetInt64(1)),
                            Currency = r.GetInt16(2), Amount = r.GetInt64(3), Type = checked((uint)r.GetInt32(4)) });
                        if (parcels.Count == 0) return 0;
                        if (parcels.Any(i => i.Item > 0))
                            using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                                if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Auction inventory requires reload.");
                        DateTime now = Clock();
                        foreach (var parcel in parcels)
                        {
                            if (parcel.Item > 0)
                            {
                                if (p.Inventory.Count + items.Count >= 40) continue;
                                Escrow(c, t, parcel.Item, parcel.Type);
                                var item = ConquerItemTable.LoadItem(parcel.Item);
                                if (item == null || item.UID != parcel.Item || item.ID != parcel.Type) throw new InvalidOperationException("Auction parcel is missing.");
                                if (Q.Execute(c, t, "UPDATE items SET entityid=@uid WHERE uid=@item AND entityid=0", "uid", (long)p.Entity.UID, "item", (long)parcel.Item) != 1)
                                    throw new InvalidOperationException("Auction parcel ownership changed.");
                                items.Add(item);
                            }
                            else if (parcel.Currency == 1)
                            {
                                if ((ulong)parcel.Amount > Entity.MaximumSilver - gold) continue;
                                gold += (ulong)parcel.Amount;
                            }
                            else
                            {
                                if ((ulong)parcel.Amount > uint.MaxValue - cp) continue;
                                cp += (uint)parcel.Amount;
                            }
                            if (Q.Execute(c, t, "UPDATE revora_auction_deliveries SET claimed_at=@now WHERE id=@id AND recipient=@uid AND claimed_at IS NULL",
                                "now", now, "id", parcel.Id, "uid", (long)p.Entity.UID) != 1) throw new InvalidOperationException("Auction delivery already claimed.");
                            claimed++;
                        }
                        if (gold != p.Entity.Money) SilverBalance.Persist(c, t, p, gold);
                        if (cp != p.Entity.ConquerPoints && Q.Execute(c, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@before",
                            "next", (long)cp, "uid", (long)p.Entity.UID, "before", (long)p.Entity.ConquerPoints) != 1)
                            throw new InvalidOperationException("Auction recipient CP purse changed.");
                        t.Commit();
                    }
                }
                if (gold != p.Entity.Money) SilverBalance.ApplyCommitted(p, gold);
                if (cp != p.Entity.ConquerPoints) p.Entity.ApplyCommittedConquerPoints(cp);
                Q.Install(p, new uint[0], items);
                foreach (var item in items) item.SendExtras(p);
                return claimed;
            }
            catch (Exception ex) { Q.Fail(p, "auction-delivery", ex); return 0; }
        }
    }
}
