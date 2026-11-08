using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AuctionGateway
    {
        private static readonly object WireGate = new object();
        private sealed class QueryState { internal DateTime Last; }
        private static readonly ConditionalWeakTable<GameState, QueryState> queries = new ConditionalWeakTable<GameState, QueryState>();
        private static volatile bool enabled;
        private static int ticking;
        private static DateTime nextTick;
        private static long lastRecipient;
        internal static void Initialize()
        {
            enabled = false;
            if (Kernel.AuctionItems.Count != 0) throw new InvalidOperationException("Unsaved legacy auctions require recovery before activation.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, "SELECT count(*) FROM auctionitems"))
                    if (Convert.ToInt64(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Legacy auction rows require recovery before activation.");
            }
            AuctionHouse.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT GREATEST(COALESCE((SELECT MAX(uid) FROM items),0),
COALESCE((SELECT MAX(uid) FROM itemadding),0),COALESCE((SELECT MAX(wire_uid) FROM revora_auction_wire),0))"))
                    ConquerItem.ItemUID.EnsureAtLeast(checked((uint)Convert.ToInt64(q.ExecuteScalar())));
            }
            nextTick = DateTime.MinValue; lastRecipient = 0; enabled = true;
        }
        internal static uint WireId(Guid id)
        {
            lock (WireGate) using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    uint uid = AuctionHouse.PersistWireId(c, t, id);
                    t.Commit(); return uid;
                }
            }
        }
        private static Guid Resolve(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, "SELECT auction FROM revora_auction_wire WHERE wire_uid=@uid", "uid", (long)uid))
                { var id = q.ExecuteScalar(); return id is Guid ? (Guid)id : Guid.Empty; }
            }
        }
        internal static ConquerItem Preview(AuctionHouse.Listing a)
        {
            var item = ConquerItemTable.LoadItem(a.Item);
            if (item == null || item.ID != a.Type || item.UID != a.Item) throw new InvalidOperationException("Auction preview item missing.");
            item.UID = WireId(a.Id); item.Mode = Enums.ItemMode.Auction;
            item.Purification.ItemUID = item.UID; item.ExtraEffect.ItemUID = item.UID;
            item.AuctionInfo = new Auction(true) { ItemUID = item.UID, MoneyType = (byte)a.Currency,
                BasePrice = a.Bidder == 0 ? a.Reserve : a.Highest, FixedPrice = a.Buyout,
                Time = (byte)((a.Ends - AuctionHouse.Clock()).TotalHours <= 12 ? 1 : 0), EndTime = a.Ends.ToLocalTime(),
                SellerUID = a.Seller, SellerName = a.SellerName, BidderUID = a.Bidder, BidderName = a.BidderName };
            return item;
        }
        private static bool Frame(byte[] frame, ushort type, int minimum)
        {
            return frame != null && frame.Length >= minimum && frame.Length <= 128 && BitConverter.ToUInt16(frame, 0) == frame.Length - 8
                && BitConverter.ToUInt16(frame, 2) == type && BitConverter.ToUInt64(frame, frame.Length - 8) == PacketHandler.ClientSeal;
        }
        internal static void Process(byte[] frame, GameState p)
        {
            if (!enabled || !Q.Player(p) || !Frame(frame, 1320, 44) || frame[8] > 3) return;
            try
            {
                var request = new Auction(true); request.Deserialize((byte[])frame.Clone());
                bool success; Guid id;
                if (request.Action == Auction.Mode.NewListing)
                {
                    id = AuctionHouse.List(p, request.ItemUID, request.MoneyType, request.BasePrice, request.FixedPrice, request.Time);
                    success = id != Guid.Empty;
                }
                else
                {
                    id = Resolve(request.ItemUID);
                    success = id != Guid.Empty && (request.Action == Auction.Mode.ListingCancel ? AuctionHouse.Cancel(p, id)
                        : AuctionHouse.Bid(p, id, request.BasePrice, request.Action == Auction.Mode.ListingBuyout));
                }
                if (p.Entity.PersistenceRequiresReload) return;
                if (success && request.Action == Auction.Mode.NewListing) request.ItemUID = WireId(id);
                request.Confirm(success).Send(p);
                if (!success) { p.Send("That auction could not be changed. Check the listing and your purse."); return; }
                var a = AuctionHouse.Get(id);
                if (a != null && a.State == 0)
                {
                    var item = Preview(a); item.Send(p);
                    p.Send(request.Action == Auction.Mode.NewListing ? Auction.AuctionInfo(item, 0, p) : Auction.AuctionQuery(item, 1, p));
                }
                AuctionHouse.Claim(p);
            }
            catch (Exception ex) { Q.Fail(p, "auction-request", ex); }
        }
        internal static void Query(byte[] frame, GameState p, bool personal)
        {
            ushort type = personal ? (ushort)1322 : (ushort)1321;
            if (!enabled || !Q.Player(p) || !Frame(frame, type, personal ? 28 : 60) || personal && frame[8] > 1 || !personal && frame[12] > 2) return;
            lock (p.ItemSyncRoot) try
            {
                if (!Q.Player(p)) return;
                var state = queries.GetValue(p, _ => new QueryState()); DateTime now = AuctionHouse.Clock();
                if (state.Last > now.AddSeconds(-1)) return;
                state.Last = now;
                ushort page = personal ? (ushort)0 : BitConverter.ToUInt16(frame, 50);
                long total;
                var rows = AuctionHouse.Page(personal ? 0 : frame[12], personal ? p.Entity.UID : 0, personal && frame[8] == 1, page, personal ? 50 : 10, out total);
                if (rows.Length == 0)
                {
                    var empty = new byte[personal ? 28 : 60]; Writer.WriteUInt16((ushort)(empty.Length - 8), 0, empty); Writer.WriteUInt16(type, 2, empty);
                    Writer.WriteUInt32(frame[8], 8, empty);
                    if (!personal) { Writer.WriteUInt16((ushort)Math.Min(total, ushort.MaxValue), 48, empty); Writer.WriteUInt16(page, 50, empty); }
                    p.Send(empty);
                }
                foreach (var a in rows)
                {
                    var item = Preview(a); item.Send(p);
                    var response = personal ? Auction.AuctionInfo(item, frame[8], p) : Auction.AuctionQuery(item, frame[8], p);
                    if (!personal)
                    {
                        Writer.WriteUInt16(1, 46, response); Writer.WriteUInt16((ushort)Math.Min(total, ushort.MaxValue), 48, response); Writer.WriteUInt16(page, 50, response);
                    }
                    p.Send(response);
                }
                AuctionHouse.Claim(p);
            }
            catch (Exception ex) { Q.Fail(p, "auction-query", ex); }
        }
        internal static void Tick()
        {
            if (!enabled || Interlocked.CompareExchange(ref ticking, 1, 0) != 0) return;
            try
            {
                DateTime now = AuctionHouse.Clock(); if (now < nextTick) return; nextTick = now.AddSeconds(10);
                AuctionHouse.Expire();
                long[] online = Kernel.GamePool.Keys.Select(uid => (long)uid).ToArray();
                if (online.Length == 0) return;
                long[] recipients;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = Q.Command(c, null, @"SELECT DISTINCT recipient FROM revora_auction_deliveries
WHERE claimed_at IS NULL AND recipient=ANY(@online) AND recipient>@after ORDER BY recipient LIMIT 32", "online", online, "after", lastRecipient))
                    using (var r = q.ExecuteReader()) { var ids = new System.Collections.Generic.List<long>(); while (r.Read()) ids.Add(r.GetInt64(0)); recipients = ids.ToArray(); }
                }
                if (recipients.Length == 0) { lastRecipient = 0; return; }
                foreach (long uid in recipients)
                {
                    lastRecipient = uid; GameState p;
                    if (!Kernel.GamePool.TryGetValue((uint)uid, out p) || !Q.Player(p) || !Monitor.TryEnter(p.ItemSyncRoot)) continue;
                    try { if (AuctionHouse.Claim(p) > 0) p.Send("Your auction delivery has arrived."); }
                    finally { Monitor.Exit(p.ItemSyncRoot); }
                }
            }
            catch (Exception ex) { Console.WriteLine("[auction] retry-pending error=" + ex.GetType().Name); }
            finally { Interlocked.Exchange(ref ticking, 0); }
        }
    }
}
