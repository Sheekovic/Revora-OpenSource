using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Partner = Revora.Game.ConquerStructures.Society.TradePartner;
using Packet = Revora.Network.GamePackets.TradePartner;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TradePartnerships
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Invite { internal Guid Id; internal GameState Sender; internal Entity SenderEntity, ReceiverEntity; internal DateTime Expires; }
        private sealed class State { internal Entity Entity; internal DateTime LastRequest; internal readonly Dictionary<uint, Invite> Pending = new Dictionary<uint, Invite>(); }
        private static readonly ConditionalWeakTable<GameState, State> states = new ConditionalWeakTable<GameState, State>();
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_partner_consents(
id uuid PRIMARY KEY,requester bigint NOT NULL CHECK(requester>0),receiver bigint NOT NULL CHECK(receiver>0 AND receiver<>requester),
accepted_at timestamptz NOT NULL,started_ticks bigint NOT NULL CHECK(started_ticks>0),removed_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_partner_active_pair ON revora_partner_consents
(LEAST(requester,receiver),GREATEST(requester,receiver)) WHERE removed_at IS NULL;
DO $revora_index$ BEGIN IF to_regclass('revora_partner_owner_lookup') IS NULL THEN CREATE INDEX IF NOT EXISTS revora_partner_owner_lookup ON partners(entityid,partnerid);
ELSIF NOT EXISTS (SELECT 1 FROM pg_index WHERE indexrelid=to_regclass('revora_partner_owner_lookup') AND indrelid='partners'::regclass AND indisvalid AND NOT indisunique AND indpred IS NULL AND indexprs IS NULL AND indkey::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='partners'::regclass AND attname='entityid' AND NOT attisdropped) || ' ' || (SELECT attnum::text FROM pg_attribute WHERE attrelid='partners'::regclass AND attname='partnerid' AND NOT attisdropped))) THEN RAISE EXCEPTION 'Revora index definition mismatch: revora_partner_owner_lookup'; END IF; END $revora_index$;");
            }
        }
        private static State For(GameState p)
        {
            var state = states.GetValue(p, _ => new State { Entity = p.Entity });
            if (!ReferenceEquals(state.Entity, p.Entity)) { states.Remove(p); state = states.GetValue(p, _ => new State { Entity = p.Entity }); }
            foreach (uint uid in state.Pending.Where(i => i.Value.Expires <= Clock()).Select(i => i.Key).ToArray()) state.Pending.Remove(uid);
            return state;
        }
        private static bool Ready(GameState p)
        { return Q.Player(p, false) && p.Account != null && p.Account.State == AccountTable.AccountState.Player && p.Partners != null; }
        private static bool CanMeet(GameState a, GameState b)
        { return ReferenceEquals(a.Map, b.Map) && a.Entity.MapID == b.Entity.MapID && Kernel.GetDistance(a.Entity.X, a.Entity.Y, b.Entity.X, b.Entity.Y) <= Constants.pScreenDistance; }
        private static void Reject(GameState p, GameState peer)
        {
            bool removed = For(p).Pending.Remove(peer.Entity.UID) | For(peer).Pending.Remove(p.Entity.UID);
            if (removed) peer.Send(Contact(p, Packet.RejectRequest));
        }
        internal static DateTime StartUtc(DateTime started)
        {
            if (started == DateTime.MinValue || started > DateTime.MaxValue.AddDays(-3)) throw new InvalidOperationException("Invalid partner probation.");
            return started.Kind == DateTimeKind.Utc ? started : DateTime.SpecifyKind(started, DateTimeKind.Local).ToUniversalTime();
        }
        internal static int RemainingMinutes(DateTime started)
        {
            double minutes = (StartUtc(started).AddDays(3) - Clock()).TotalMinutes;
            return minutes <= 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Ceiling(minutes));
        }
        internal static bool Mature(GameState a, GameState b)
        {
            Partner left, right;
            return a.Partners != null && b.Partners != null && a.Partners.TryGetValue(b.Entity.UID, out left)
                && b.Partners.TryGetValue(a.Entity.UID, out right) && left.ID == b.Entity.UID && right.ID == a.Entity.UID
                && StartUtc(left.ProbationStartedOn) == StartUtc(right.ProbationStartedOn) && RemainingMinutes(left.ProbationStartedOn) == 0;
        }
        private static SafeDictionary<uint, Partner> Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid)
        {
            var loaded = new SafeDictionary<uint, Partner>(20);
            using (var q = Q.Command(c, t, @"SELECT p.partnerid,p.partnername,p.probationstartedon,r.accepted_at,r.started_ticks
FROM partners p LEFT JOIN revora_partner_consents r ON r.removed_at IS NULL
AND ((r.requester=p.entityid AND r.receiver=p.partnerid) OR (r.receiver=p.entityid AND r.requester=p.partnerid))
WHERE p.entityid=@uid", "uid", (long)uid))
            using (var r = q.ExecuteReader()) while (r.Read())
            {
                uint peer = checked((uint)r.GetInt64(0)); long raw = r.GetInt64(2);
                if (peer == 0 || peer == uid || loaded.ContainsKey(peer)) throw new InvalidOperationException("Partner list requires repair.");
                DateTime start;
                if (!r.IsDBNull(3))
                {
                    if (r.IsDBNull(4) || r.GetInt64(4) != raw) throw new InvalidOperationException("Partner consent disagrees with probation.");
                    start = r.GetFieldValue<DateTimeOffset>(3).UtcDateTime;
                }
                else start = StartUtc(DateTime.FromBinary(raw));
                StartUtc(start);
                loaded.Add(peer, new Partner { ID = peer, Name = r.GetString(1), ProbationStartedOn = start });
            }
            return loaded;
        }
        // Trade settlement calls this after locking both database entity rows.
        internal static bool MatureSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState a, GameState b)
        {
            if (t == null || !System.Threading.Monitor.IsEntered(a.ItemSyncRoot) || !System.Threading.Monitor.IsEntered(b.ItemSyncRoot))
                throw new InvalidOperationException("Partner eligibility requires trade ownership.");
            Partner left, right; var first = Read(c, t, a.Entity.UID); var second = Read(c, t, b.Entity.UID);
            return first.TryGetValue(b.Entity.UID, out left) && second.TryGetValue(a.Entity.UID, out right)
                && left.ProbationStartedOn == right.ProbationStartedOn && RemainingMinutes(left.ProbationStartedOn) == 0;
        }
        internal static bool Frame(byte[] frame)
        {
            return frame != null && frame.Length >= 40 && frame.Length <= 128 && BitConverter.ToUInt16(frame, 0) == frame.Length - 8
                && BitConverter.ToUInt16(frame, 2) == 2046 && BitConverter.ToUInt64(frame, frame.Length - 8) == PacketHandler.ClientSeal;
        }
        internal static Packet Contact(GameState subject, byte type)
        {
            return new Packet(true) { UID = subject.Entity.UID, Type = type, Name = subject.Entity.Name, Online = true };
        }
        internal static RequestInformation InvitationInfo(GameState sender, GameState receiver)
        {
            return new RequestInformation { Requester = sender.Entity.UID, Receiver = receiver.Entity.UID,
                Level = sender.Entity.Level, BattlePower = (uint)sender.Entity.BattlePower };
        }
        private static void PairLocks(GameState a, GameState b, Action action)
        {
            if (b == null) { lock (a.ItemSyncRoot) action(); return; }
            var first = a.Entity.UID < b.Entity.UID ? a : b; var second = ReferenceEquals(first, a) ? b : a;
            lock (first.ItemSyncRoot) lock (second.ItemSyncRoot) action();
        }
        internal static void Process(byte[] frame, GameState p)
        {
            if (!Frame(frame) || !Ready(p)) return;
            byte type = frame[8]; uint uid = BitConverter.ToUInt32(frame, 4);
            if (uid == 0 || uid == p.Entity.UID) return;
            if (type == Packet.BreakPartnership) { Remove(p, uid); return; }
            if (type != Packet.RequestPartnership && type != Packet.RejectRequest) return;
            GameState peer;
            if (!Kernel.GamePool.TryGetValue(uid, out peer) || !Ready(peer)) return;
            PairLocks(p, peer, () =>
            {
                if (!Ready(p) || !Ready(peer)) return;
                try
                {
                    if (type == Packet.RejectRequest) { Reject(p, peer); return; }
                    if (!CanMeet(p, peer)) return;
                    Invite incoming;
                    if (For(p).Pending.TryGetValue(uid, out incoming) && ReferenceEquals(incoming.Sender, peer)
                        && ReferenceEquals(incoming.SenderEntity, peer.Entity) && ReferenceEquals(incoming.ReceiverEntity, p.Entity)) Accept(p, peer);
                    else Request(p, peer);
                }
                catch (Exception ex) { Q.Fail(p, "partner-consent", ex); Q.Fail(peer, "partner-consent", ex); }
            });
        }
        private static void Request(GameState sender, GameState receiver)
        {
            DateTime now = Clock(); var source = For(sender); var target = For(receiver);
            if (source.LastRequest > now.AddSeconds(-1)) return;
            source.LastRequest = now;
            if (sender.Partners.ContainsKey(receiver.Entity.UID) && receiver.Partners.ContainsKey(sender.Entity.UID)) return;
            if (target.Pending.ContainsKey(sender.Entity.UID) || target.Pending.Count >= 16) return;
            target.Pending.Add(sender.Entity.UID, new Invite { Id = Guid.NewGuid(), Sender = sender, SenderEntity = sender.Entity,
                ReceiverEntity = receiver.Entity, Expires = now.AddMinutes(2) });
            receiver.Send(InvitationInfo(sender, receiver)); receiver.Send(Contact(sender, Packet.RequestPartnership));
            sender.Send("Your partner request has been sent.");
        }
        private static void Accept(GameState receiver, GameState sender)
        {
            Invite invite; var target = For(receiver);
            if (!target.Pending.TryGetValue(sender.Entity.UID, out invite) || !ReferenceEquals(invite.Sender, sender)
                || !ReferenceEquals(invite.SenderEntity, sender.Entity) || !ReferenceEquals(invite.ReceiverEntity, receiver.Entity)) return;
            var first = sender.Entity.UID < receiver.Entity.UID ? sender : receiver;
            var second = ReferenceEquals(first, sender) ? receiver : sender;
            bool refreshFirst, refreshSecond;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    SkillTable.LockOwner(c, t, first); SkillTable.LockOwner(c, t, second);
                    DateTime now = Clock(); if (invite.Expires <= now || !Ready(sender) || !Ready(receiver) || !CanMeet(sender, receiver)) return;
                    now = new DateTime(now.Ticks / 10 * 10, DateTimeKind.Utc); long started = now.ToLocalTime().Ticks;
                    foreach (var owner in new[] { first, second })
                    {
                        var other = ReferenceEquals(owner, first) ? second : first; long existing, total;
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM partners WHERE entityid=@uid AND partnerid=@peer", "uid", (long)owner.Entity.UID, "peer", (long)other.Entity.UID)) existing = Convert.ToInt64(q.ExecuteScalar());
                        if (existing > 1) throw new InvalidOperationException("Duplicate partner direction requires repair.");
                        using (var q = Q.Command(c, t, "SELECT count(DISTINCT partnerid) FROM partners WHERE entityid=@uid", "uid", (long)owner.Entity.UID)) total = Convert.ToInt64(q.ExecuteScalar());
                        if (existing == 0 && total >= 20) { receiver.Send("One of your partner lists is full."); return; }
                        if (existing == 0 && Q.Execute(c, t, "INSERT INTO partners(entityid,partnerid,partnername,probationstartedon) VALUES(@uid,@peer,@name,@start)",
                            "uid", (long)owner.Entity.UID, "peer", (long)other.Entity.UID, "name", other.Entity.Name, "start", started) != 1)
                            throw new InvalidOperationException("Partner direction was not saved.");
                        if (existing == 1 && Q.Execute(c, t, "UPDATE partners SET partnername=@name,probationstartedon=@start WHERE entityid=@uid AND partnerid=@peer",
                            "uid", (long)owner.Entity.UID, "peer", (long)other.Entity.UID, "name", other.Entity.Name, "start", started) != 1)
                            throw new InvalidOperationException("Partner probation was not saved.");
                    }
                    using (var q = Q.Command(c, t, @"SELECT id FROM revora_partner_consents WHERE removed_at IS NULL
AND ((requester=@a AND receiver=@b) OR (requester=@b AND receiver=@a))", "a", (long)sender.Entity.UID, "b", (long)receiver.Entity.UID))
                        if (q.ExecuteScalar() != null) throw new InvalidOperationException("Active partner consent disagrees with the loaded list.");
                    if (Q.Execute(c, t, "INSERT INTO revora_partner_consents(id,requester,receiver,accepted_at,started_ticks) VALUES(@id,@a,@b,@now,@start)",
                        "id", invite.Id, "a", (long)sender.Entity.UID, "b", (long)receiver.Entity.UID, "now", now, "start", started) != 1)
                        throw new InvalidOperationException("Partner consent was not recorded.");
                    string key = invite.Id.ToString("N");
                    refreshFirst = ProgressionEvidence.Complete(c, t, first.Entity.UID, 10126, "mutual-partner-v1", key, 1);
                    refreshSecond = ProgressionEvidence.Complete(c, t, second.Entity.UID, 10126, "mutual-partner-v1", key, 1);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10126 AND uid=ANY(@uids)",
                        "uids", new[] { (long)first.Entity.UID, (long)second.Entity.UID }))
                        if (Convert.ToInt64(q.ExecuteScalar()) != 2) throw new InvalidOperationException("Partner achievement was not saved.");
                    t.Commit();
                }
            }
            target.Pending.Remove(sender.Entity.UID); For(sender).Pending.Remove(receiver.Entity.UID);
            Reload(first, true); Reload(second, true);
            if (refreshFirst) Q.Refresh(first); if (refreshSecond) Q.Refresh(second);
        }
        internal static void Remove(GameState p, uint uid)
        {
            if (!Ready(p) || uid == 0 || uid == p.Entity.UID) return;
            GameState peer;
            if (!Kernel.GamePool.TryGetValue(uid, out peer) || peer.Entity == null || peer.ItemSyncRoot == null) peer = null;
            bool committed = false;
            PairLocks(p, peer, () =>
            {
                if (!Ready(p)) return;
                try
                {
                    For(p).Pending.Remove(uid); if (peer != null) For(peer).Pending.Remove(p.Entity.UID);
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            bool owner = false;
                            using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=ANY(@uids) ORDER BY uid FOR UPDATE", "uids", new[] { (long)p.Entity.UID, (long)uid }))
                            using (var r = q.ExecuteReader()) while (r.Read()) if (r.GetInt64(0) == p.Entity.UID) owner = true;
                            if (!owner) throw new InvalidOperationException("Partner owner missing.");
                            if (!Ready(p)) return;
                            long expected;
                            using (var q = Q.Command(c, t, "SELECT count(*) FROM partners WHERE (entityid=@a AND partnerid=@b) OR (entityid=@b AND partnerid=@a)", "a", (long)p.Entity.UID, "b", (long)uid)) expected = Convert.ToInt64(q.ExecuteScalar());
                            if (Q.Execute(c, t, "DELETE FROM partners WHERE (entityid=@a AND partnerid=@b) OR (entityid=@b AND partnerid=@a)", "a", (long)p.Entity.UID, "b", (long)uid) != expected) throw new InvalidOperationException("Partner removal was incomplete.");
                            object active;
                            using (var q = Q.Command(c, t, @"SELECT id FROM revora_partner_consents WHERE removed_at IS NULL
AND ((requester=@a AND receiver=@b) OR (requester=@b AND receiver=@a)) FOR UPDATE", "a", (long)p.Entity.UID, "b", (long)uid)) active = q.ExecuteScalar();
                            if (active != null && Q.Execute(c, t, "UPDATE revora_partner_consents SET removed_at=@now WHERE id=@id AND removed_at IS NULL", "now", Clock(), "id", active) != 1)
                                throw new InvalidOperationException("Partner removal was not recorded.");
                            t.Commit(); committed = true;
                        }
                    }
                }
                catch (Exception ex) { Q.Fail(p, "partner-remove", ex); if (peer != null && !peer.Fake) Q.Fail(peer, "partner-remove", ex); }
            });
            // Resolve again after commit: a peer may have entered the pool while
            // an offline removal was waiting on database locks.
            if (committed) { ReloadOnline(p.Entity.UID); ReloadOnline(uid); }
        }
        private static void ReloadOnline(uint uid)
        {
            GameState p; if (Kernel.GamePool.TryGetValue(uid, out p) && !p.Fake && p.Entity != null && p.ItemSyncRoot != null) Reload(p, true);
        }
        internal static void Reload(GameState p, bool notify)
        {
            lock (p.ItemSyncRoot ?? p) try
            {
                SafeDictionary<uint, Partner> loaded;
                using (var c = DataHolder.MySqlConnection) { c.Open(); loaded = Read(c, null, p.Entity.UID); }
                var before = p.Partners; p.Partners = loaded;
                if (!notify || !Ready(p)) return;
                if (before != null) foreach (uint uid in before.Keys.Where(i => !loaded.ContainsKey(i)).ToArray())
                    p.Send(new Packet(true) { UID = uid, Type = Packet.BreakPartnership, Name = "", Online = false });
                foreach (var partner in loaded.Values.Where(i => before == null || !before.ContainsKey(i.ID) || before[i.ID].ProbationStartedOn != i.ProbationStartedOn))
                {
                    GameState other;
                    var packet = Kernel.GamePool.TryGetValue(partner.ID, out other) && Ready(other) ? Contact(other, Packet.AddPartner)
                        : new Packet(true) { UID = partner.ID, Type = Packet.AddPartner, Name = partner.Name, Online = false };
                    packet.MinutesLeft = RemainingMinutes(partner.ProbationStartedOn); p.Send(packet);
                }
            }
            catch (Exception ex) { Q.Fail(p, "partner-load", ex); throw; }
        }
    }
}
