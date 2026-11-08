using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using PartnerPacket = Revora.Network.GamePackets.TradePartner;
using PartnerStore = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type PartnerService = typeof(Entity).Assembly.GetType("Revora.Game.Features.TradePartnerships");
    static DateTime partnerNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    static GameState PartnerPlayer(Map map)
    {
        var p = AuctionPlayer(map); Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity); EvidenceRefresh(p);
        PartnerStore.LoadPartner(p); return p;
    }
    static byte[] PartnerFrame(byte action, uint uid)
    {
        var bytes = new PartnerPacket(true) { Type = action, UID = uid, Name = "untrusted" }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); return bytes;
    }
    static void PartnerSend(GameState p, byte action, GameState peer)
    { PartnerPacket.Process(PartnerFrame(action, peer.Entity.UID), p); }
    static long PartnerRows(GameState p)
    { return Scalar("SELECT count(*) FROM partners WHERE entityid=@uid", p.Entity.UID); }
    static long PartnerAwards(GameState p)
    { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10126 AND uid=@uid", p.Entity.UID); }
    static bool PartnerMature(GameState a, GameState b)
    { return (bool)Call(PartnerService, null, "Mature", a, b); }
    static void VerifyPartners(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE partners (LIKE public.partners INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); Call(PartnerService, null, "Initialize");
        PartnerService.GetField("Clock", Hidden).SetValue(null, new Func<DateTime>(() => partnerNow));
        var a = PartnerPlayer(map); var b = PartnerPlayer(map); var stranger = PartnerPlayer(map);
        Check(a.Account.State == AccountTable.AccountState.Player && b.Account.State == AccountTable.AccountState.Player && !a.Fake && !b.Fake,
            "partner fixtures use ordinary authenticated players");
        PartnerSend(b, 5, a); PartnerSend(a, 0, b); PartnerSend(stranger, 0, a);
        Check(PartnerRows(a) == 0 && PartnerAwards(a) == 0 && PartnerRows(stranger) == 0, "server-add forgery, one request and third-party response cannot establish partnership");
        PartnerSend(b, 0, a);
        Check(!Fenced(a) && !Fenced(b) && PartnerRows(a) == 1 && PartnerRows(b) == 1
            && a.Partners[b.Entity.UID].ID == b.Entity.UID && b.Partners[a.Entity.UID].ID == a.Entity.UID
            && b.Partners[a.Entity.UID].Name == a.Entity.Name, "reciprocal consent commits both directions with correct other-player identities");
        Check(PartnerAwards(a) == 1 && PartnerAwards(b) == 1 && HasAchievement(a, 10126) && HasAchievement(b, 10126), "saved partnership immediately earns achievement 10126 for both players");
        DateTime start = a.Partners[b.Entity.UID].ProbationStartedOn;
        Check(start.Kind == DateTimeKind.Utc && start == partnerNow && b.Partners[a.Entity.UID].ProbationStartedOn == start && !PartnerMature(a, b), "both partners share one UTC probation start");
        var packet = new PartnerPacket(true) { MinutesLeft = 4320, Name = new string('X', 1000) };
        Check(packet.HoursLeft == 72 && packet.MinutesLeft == 4320 && packet.ToArray().Skip(32).All(i => i == 0), "minute countdown and bounded partner name preserve packet suffix space");
        packet.HoursLeft = int.MaxValue; Check(packet.MinutesLeft == int.MaxValue, "legacy hour setter cannot wrap the countdown");
        var repeat = Enumerable.Range(0, 12).Select(i => Task.Run(() => PartnerSend(b, 0, a))).ToArray(); Task.WaitAll(repeat);
        Check(PartnerRows(a) == 1 && PartnerAwards(a) == 1 && a.Partners[b.Entity.UID].ProbationStartedOn == start, "parallel duplicate reciprocal requests cannot reset probation or repeat completion");
        partnerNow = start.AddDays(3).AddTicks(-1);
        Check(!PartnerMature(a, b) && (int)Call(PartnerService, null, "RemainingMinutes", start) == 1, "the final partial minute remains on probation");
        partnerNow = start.AddDays(3);
        Check(PartnerMature(a, b) && !a.Partners[b.Entity.UID].StillOnProbation, "partnership matures at exactly seventy-two hours");
        PartnerStore.LoadPartner(a); PartnerStore.LoadPartner(b);
        Check(PartnerMature(a, b) && a.Partners[b.Entity.UID].ProbationStartedOn == start, "production partner reload preserves receipt-based UTC maturity");
        GameState removed; Kernel.GamePool.TryRemove(b.Entity.UID, out removed); PartnerSend(a, 4, b); Kernel.GamePool[b.Entity.UID] = b; PartnerStore.LoadPartner(b);
        Check(PartnerRows(a) == 0 && PartnerRows(b) == 0 && a.Partners.Count == 0 && b.Partners.Count == 0 && PartnerAwards(b) == 1, "offline break removes both saved directions while earned achievement persists");
        PartnerSend(b, 0, a);
        Check(PartnerRows(a) == 0, "a late reciprocal packet after break cannot restore the old consent");
        PartnerSend(a, 1, b); partnerNow = partnerNow.AddSeconds(2); PartnerSend(a, 0, b); PartnerSend(b, 1, a); PartnerSend(b, 0, a);
        Check(PartnerRows(a) == 0, "reject revokes the owned invitation and later response starts only a new request");

        var distant = PartnerPlayer(map); var near = PartnerPlayer(map); ushort priorX = distant.Entity.X; distant.Entity.X = (ushort)(near.Entity.X + 100);
        PartnerSend(distant, 0, near); PartnerSend(near, 0, distant); distant.Entity.X = priorX;
        Check(PartnerRows(distant) == 0, "trade partners must meet within the same nearby map view");
        foreach (int size in new[] { 0, 1, 3, 8, 9, 20, 39, 129, 65536 }) PartnerPacket.Process(new byte[size], distant);
        foreach (int offset in new[] { 0, 2, 32 }) { var frame = PartnerFrame(0, near.Entity.UID); frame[offset]++; PartnerPacket.Process(frame, distant); }
        PartnerSend(near, 0, distant);
        Check(!Fenced(distant) && PartnerRows(distant) == 0, "malformed frame lengths, headers and suffixes cannot establish consent");
        PartnerSend(distant, 1, near); distant.Fake = true; PartnerSend(distant, 0, near); distant.Fake = false; PartnerSend(near, 0, distant);
        Check(PartnerRows(distant) == 0, "fake players cannot establish a trade partnership");
        var expires = PartnerPlayer(map); var receiver = PartnerPlayer(map); PartnerSend(expires, 0, receiver); partnerNow = partnerNow.AddMinutes(2); PartnerSend(receiver, 0, expires);
        Check(PartnerRows(expires) == 0, "expired reciprocal response cannot claim old consent");
        var old = PartnerPlayer(map); var peer = PartnerPlayer(map); PartnerSend(old, 0, peer);
        var current = PartnerPlayer(map); Kernel.GamePool.TryRemove(current.Entity.UID, out current); current.Entity.UID = old.Entity.UID; current.Account.EntityID = old.Entity.UID; Kernel.GamePool[old.Entity.UID] = current;
        PartnerSend(peer, 0, current);
        Check(PartnerRows(peer) == 0, "reconnected sender cannot inherit old pending consent");
        PartnerSend(current, 0, peer);
        Check(PartnerRows(peer) == 1 && PartnerAwards(current) == 1, "new canonical sessions may consent through a fresh reverse request");
        VerifyPartnerFailures(map); VerifyPartnerWaits(map); VerifyPartnerTrading(map);
    }
    static void VerifyPartnerFailures(Map map)
    {
        foreach (string failure in new[] { "first-direction", "second-direction", "consent", "evidence", "silent-evidence", "silent-direction", "silent-remove" })
        {
            var x = PartnerPlayer(map); var y = PartnerPlayer(map); PartnerSend(x, 0, y);
            bool removing = failure == "silent-remove"; if (removing) PartnerSend(y, 0, x);
            string table = failure == "consent" ? "revora_partner_consents" : failure.EndsWith("evidence") ? "revora_achievement_evidence" : "partners";
            string predicate = failure == "first-direction" ? "NEW.entityid=" + Math.Min(x.Entity.UID, y.Entity.UID) : failure == "second-direction" ? "NEW.entityid=" + Math.Max(x.Entity.UID, y.Entity.UID) : "TRUE";
            string effect = failure.StartsWith("silent") ? "RETURN NULL;" : "RAISE EXCEPTION 'fixture partner failure';";
            Query("CREATE FUNCTION fail_partner() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF " + predicate + " THEN " + effect.Replace("'", "''") + " END IF; RETURN NEW; END';CREATE TRIGGER fail_partner BEFORE " + (removing ? "DELETE" : "INSERT") + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION fail_partner()");
            PartnerSend(removing ? x : y, removing ? (byte)4 : (byte)0, removing ? y : x);
            Check(Fenced(x) && Fenced(y) && PartnerRows(x) == (removing ? 1 : 0) && PartnerRows(y) == (removing ? 1 : 0)
                && PartnerAwards(x) == (removing ? 1 : 0) && x.Partners.Count == (removing ? 1 : 0), "atomic partner rollback and stale-session fence: " + failure);
            Query("DROP TRIGGER fail_partner ON " + table + ";DROP FUNCTION fail_partner()"); DemonReset(x); DemonReset(y); PartnerStore.LoadPartner(x); PartnerStore.LoadPartner(y);
            PartnerSend(removing ? x : y, removing ? (byte)4 : (byte)0, removing ? y : x);
            Check(!Fenced(x) && !Fenced(y) && PartnerRows(x) == (removing ? 0 : 1), "partner operation retries safely after reload: " + failure);
        }
        var full = PartnerPlayer(map); var applicant = PartnerPlayer(map);
        Query("INSERT INTO partners(entityid,partnerid,partnername,probationstartedon) SELECT @uid,90000000+n,'Fixture',@start FROM generate_series(1,20) n", "uid", (long)full.Entity.UID, "start", partnerNow.ToLocalTime().Ticks);
        PartnerStore.LoadPartner(full); PartnerSend(applicant, 0, full); PartnerSend(full, 0, applicant);
        Check(PartnerRows(full) == 20 && PartnerRows(applicant) == 0 && PartnerAwards(applicant) == 0, "official twenty-partner limit is checked durably on both sides");
        var legacy = PartnerPlayer(map); var repair = PartnerPlayer(map);
        Query("INSERT INTO partners(entityid,partnerid,partnername,probationstartedon) VALUES(@a,@b,'Legacy',@start)", "a", (long)legacy.Entity.UID, "b", (long)repair.Entity.UID, "start", partnerNow.AddDays(-30).ToLocalTime().Ticks);
        PartnerStore.LoadPartner(legacy);
        Check(PartnerAwards(legacy) == 0 && !PartnerMature(legacy, repair), "a one-sided historical partnership does not grant achievement or locked-item eligibility");
        PartnerSend(legacy, 0, repair); PartnerSend(repair, 0, legacy);
        Check(PartnerRows(legacy) == 1 && PartnerRows(repair) == 1 && PartnerAwards(legacy) == 1 && !PartnerMature(legacy, repair)
            && legacy.Partners[repair.Entity.UID].ProbationStartedOn == partnerNow, "fresh consent repairs one-sided history and starts a new shared probation");
        Query("UPDATE partners SET probationstartedon=probationstartedon-1 WHERE entityid=@uid", "uid", (long)legacy.Entity.UID);
        bool refused = false; try { PartnerStore.LoadPartner(legacy); } catch (InvalidOperationException) { refused = true; }
        Check(refused && Fenced(legacy) && legacy.Partners[repair.Entity.UID].ProbationStartedOn == partnerNow,
            "loader preserves its old snapshot and fences a probation value that disagrees with its consent receipt");
        var duplicates = PartnerPlayer(map); var duplicatePeer = PartnerPlayer(map);
        Query("INSERT INTO partners(entityid,partnerid,partnername,probationstartedon) VALUES(@a,@b,'one',@start),(@a,@b,'two',@start)",
            "a", (long)duplicates.Entity.UID, "b", (long)duplicatePeer.Entity.UID, "start", partnerNow.ToLocalTime().Ticks);
        PartnerSend(duplicates, 0, duplicatePeer); PartnerSend(duplicatePeer, 0, duplicates);
        Check(Fenced(duplicates) && Fenced(duplicatePeer) && PartnerRows(duplicates) == 2 && PartnerRows(duplicatePeer) == 0 && PartnerAwards(duplicates) == 0,
            "duplicate historical directions remain intact for reviewed repair and cannot award partnership");
        var history = PartnerPlayer(map); var historyPeer = PartnerPlayer(map); DateTime historicalStart = partnerNow.AddDays(-4).ToLocalTime();
        Query("INSERT INTO partners(entityid,partnerid,partnername,probationstartedon) VALUES(@a,@b,'one',@start),(@b,@a,'two',@start)",
            "a", (long)history.Entity.UID, "b", (long)historyPeer.Entity.UID, "start", historicalStart.Ticks);
        PartnerStore.LoadPartner(history); PartnerStore.LoadPartner(historyPeer);
        Check(PartnerMature(history, historyPeer) && PartnerAwards(history) == 0 && history.Partners[historyPeer.Entity.UID].ProbationStartedOn == historicalStart.ToUniversalTime(),
            "matching mature legacy partners retain trading eligibility without retroactive achievement evidence");
        var dst = PartnerPlayer(map); var dstPeer = PartnerPlayer(map); partnerNow = new DateTime(2030, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        PartnerSend(dst, 0, dstPeer); PartnerSend(dstPeer, 0, dst); partnerNow = partnerNow.AddHours(71); PartnerStore.LoadPartner(dst); PartnerStore.LoadPartner(dstPeer);
        Check(!PartnerMature(dst, dstPeer) && (int)Call(PartnerService, null, "RemainingMinutes", dst.Partners[dstPeer.Entity.UID].ProbationStartedOn) == 60,
            "new consent keeps a full seventy-two-hour wait across a local daylight-saving transition");
        partnerNow = partnerNow.AddHours(1); Check(PartnerMature(dst, dstPeer), "UTC probation matures at the exact elapsed-time boundary after DST");
    }
    static void VerifyPartnerWaits(Map map)
    {
        foreach (bool move in new[] { false, true })
        {
            var a = PartnerPlayer(map); var b = PartnerPlayer(map); PartnerSend(a, 0, b);
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    using (var q = new Npgsql.NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", c, t))
                    { q.Parameters.AddWithValue("uid", (long)Math.Min(a.Entity.UID, b.Entity.UID)); q.ExecuteScalar(); }
                    var attempt = Task.Run(() => PartnerSend(b, 0, a));
                    bool blocked = System.Threading.SpinWait.SpinUntil(() => Scalar("SELECT count(*) FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%SELECT uid FROM entities WHERE uid=%' AND @uid>0", a.Entity.UID) > 0, 5000);
                    Task movement = Task.CompletedTask;
                    if (move)
                    {
                        movement = Task.Run(() => a.Entity.X = (ushort)(b.Entity.X + 100));
                        Check(System.Threading.SpinWait.SpinUntil(() => a.Entity.X == b.Entity.X + 100, 5000), "movement updates its live position before waiting to save it");
                    }
                    else partnerNow = partnerNow.AddMinutes(2);
                    t.Commit();
                    Check(attempt.Wait(5000) && movement.Wait(5000) && blocked && PartnerRows(a) == 0 && PartnerRows(b) == 0 && !Fenced(a) && !Fenced(b),
                        move ? "movement while waiting for database ownership invalidates partner admission" : "partner request expiry is rechecked after database owner-lock waits");
                }
            }
        }
    }
    static void VerifyPartnerTrading(Map map)
    {
        var atomic = typeof(Entity).Assembly.GetType("Revora.Game.Features.AtomicTrade"); Call(atomic, null, "Initialize");
        var a = PartnerPlayer(map); var b = PartnerPlayer(map); PartnerSend(a, 0, b); PartnerSend(b, 0, a);
        var item = Pack(a, 410009, 1); item.Lock = 1; Query("UPDATE items SET locked=1 WHERE uid=@uid", "uid", (long)item.UID);
        TradeOpen(a, b); TradeSend(a, Trade.AddItem, item.UID);
        Check(a.Trade.Items.Count == 0 && a.Inventory.ContainsUID(item.UID), "probationary partners cannot offer locked equipment"); TradeSend(a, Trade.Close, 0);
        partnerNow = partnerNow.AddDays(3); TradeOpen(a, b); TradeSend(a, Trade.AddItem, item.UID);
        Check(a.Trade.Items.Count == 1, "mature mutual partners can offer eligible locked equipment");
        Query("DELETE FROM partners WHERE entityid=@uid", "uid", (long)b.Entity.UID);
        TradeSend(a, Trade.Accept, 0); TradeSend(b, Trade.Accept, 0);
        Check(Fenced(a) && Fenced(b) && SavedRune(item, "entityid") == a.Entity.UID && a.Inventory.ContainsUID(item.UID)
            && Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid", a.Entity.UID) == 0,
            "settlement rejects stale partner caches when the saved reverse direction is missing");
        DemonReset(a); DemonReset(b); PartnerSend(a, 4, b); partnerNow = partnerNow.AddSeconds(2); PartnerSend(a, 0, b); PartnerSend(b, 0, a); partnerNow = partnerNow.AddDays(3);
        TradeOpen(a, b); TradeSend(a, Trade.AddItem, item.UID); TradeSend(b, Trade.SetMoney, 100); TradeSend(a, Trade.Accept, 0); TradeSend(b, Trade.Accept, 0);
        Check(!Fenced(a) && !Fenced(b) && SavedRune(item, "entityid") == b.Entity.UID && b.Inventory.ContainsUID(item.UID)
            && !a.Inventory.ContainsUID(item.UID) && a.Entity.Money == 100100 && b.Entity.Money == 99900,
            "ordinary mature partners atomically exchange the original locked item and exact payment");
        PartnerStore.LoadPartner(b); ConquerItem loaded = ConquerItemTable.LoadItem(item.UID);
        Check(loaded.UID == item.UID && loaded.Lock == 1 && PartnerMature(a, b), "locked-item identity and partnership survive production reload");
    }
}
